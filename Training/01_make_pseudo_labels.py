# -*- coding: utf-8 -*-
"""
1단계: 의사 라벨(pseudo label) 생성 + YOLO 데이터셋 구성

사람이 만든 정답 라벨이 없으므로, 참조 프로젝트(References\\CellCount)에 들어있는
기존 세포 검출 ONNX 모델 4종(v8n, v10n, v10s, v10m)을 "교사(teacher)"로 사용한다.
각 모델을 원본/좌우반전/상하반전 이미지에 적용(TTA)한 뒤, 서로 겹치는 박스를 묶어
여러 모델이 동의한 박스만 라벨로 채택한다.

사용법 (yolov26 conda 환경):
    python 01_make_pseudo_labels.py              # 전체 데이터셋 생성
    python 01_make_pseudo_labels.py --preview 4  # 4장만 처리하고 오버레이 저장(품질 확인용)
"""
import argparse
import os
import random
import re

import cv2
import numpy as np
from ultralytics import YOLO

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMAGE_ROOT = os.path.join(ROOT, "Cell images")
TEACHER_DIR = os.path.join(ROOT, "References", "CellCount", "YoloOnnxRuntime", "bin", "net4.8", "cellcount")
TEACHERS = ["cellcountv8n.onnx", "cellcountv10nsim10000.onnx", "cellcountv10ssim10000.onnx", "cellcountv10msim10000.onnx"]
DATASET = os.path.join(ROOT, "Training", "dataset")

TEACHER_CONF = 0.10     # 교사 모델 개별 검출 최소 신뢰도
CLUSTER_IOU = 0.55      # 같은 세포로 묶는 IoU
MIN_MODELS = 2          # 최소 동의 모델 수
MIN_MEAN_SCORE = 0.12   # (점수 합 / 전체 패스 수) 최소값
DEBRIS_DARK = 120       # max(R,G,B) 가 이 값보다 작으면 '어두운 픽셀'
DEBRIS_FRACTION = 0.30  # 박스 중앙부의 어두운 픽셀 비율이 이보다 크면 이물질로 간주
CONTAIN_RATIO = 0.92   # 교집합 / 작은 박스 면적이 이보다 크면 중복으로 간주
SAVE_WIDTH = 1600      # 학습용 이미지 가로 크기 (원본 3088 -> 1600)


def imread(path):
    return cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_COLOR)


def imwrite(path, img, quality=95):
    ok, buf = cv2.imencode(".jpg", img, [cv2.IMWRITE_JPEG_QUALITY, quality])
    buf.tofile(path)


def iou_one_to_many(box, boxes):
    x1 = np.maximum(box[0], boxes[:, 0]); y1 = np.maximum(box[1], boxes[:, 1])
    x2 = np.minimum(box[2], boxes[:, 2]); y2 = np.minimum(box[3], boxes[:, 3])
    inter = np.clip(x2 - x1, 0, None) * np.clip(y2 - y1, 0, None)
    a = (box[2] - box[0]) * (box[3] - box[1])
    b = (boxes[:, 2] - boxes[:, 0]) * (boxes[:, 3] - boxes[:, 1])
    return inter / (a + b - inter + 1e-9)


def teacher_boxes(models, img):
    """모든 교사 x TTA 결과를 (x1,y1,x2,y2,score,model_idx) 배열로 반환"""
    h, w = img.shape[:2]
    out = []
    views = [("o", img), ("h", cv2.flip(img, 1)), ("v", cv2.flip(img, 0))]
    for mi, m in enumerate(models):
        for tag, view in views:
            r = m.predict(view, conf=TEACHER_CONF, iou=0.5, max_det=3000, verbose=False)[0]
            if len(r.boxes) == 0:
                continue
            b = r.boxes.xyxy.cpu().numpy().copy()
            s = r.boxes.conf.cpu().numpy()
            if tag == "h":
                b[:, [0, 2]] = w - b[:, [2, 0]]
            elif tag == "v":
                b[:, [1, 3]] = h - b[:, [3, 1]]
            out.append(np.column_stack([b, s, np.full(len(b), mi)]))
    return np.concatenate(out) if out else np.zeros((0, 6)), len(models) * len(views)


def fuse(dets, n_pass):
    """점수 순으로 탐욕적 군집화 -> 가중 평균 박스"""
    if len(dets) == 0:
        return np.zeros((0, 5))
    dets = dets[np.argsort(-dets[:, 4])]
    used = np.zeros(len(dets), bool)
    fused = []
    for i in range(len(dets)):
        if used[i]:
            continue
        ious = iou_one_to_many(dets[i, :4], dets[:, :4])
        idx = np.where((ious >= CLUSTER_IOU) & (~used))[0]
        used[idx] = True
        c = dets[idx]
        n_models = len(np.unique(c[:, 5]))
        mean_score = c[:, 4].sum() / n_pass
        if n_models >= MIN_MODELS and mean_score >= MIN_MEAN_SCORE:
            wgt = c[:, 4:5]
            box = (c[:, :4] * wgt).sum(0) / wgt.sum()
            fused.append([*box, mean_score])
    return np.array(fused) if fused else np.zeros((0, 5))


def clean(boxes, img):
    """의사 라벨 정제: (1) 짙은 남색 이물질(debris) 박스 제거 (2) 거의 포함되는 중복 박스 제거"""
    if len(boxes) == 0:
        return boxes
    h, w = img.shape[:2]
    # 이물질: 모든 채널이 어둡고 푸른색 우세(B >= R). 진한 분홍/빨강 세포(R > B)는 보존한다.
    maxch = np.where(img[:, :, 0] >= img[:, :, 2], img.max(axis=2), 255)
    keep = []
    for b in boxes:
        bw, bh = b[2] - b[0], b[3] - b[1]
        x1, x2 = int(max(0, b[0] + bw * 0.2)), int(min(w, b[2] - bw * 0.2))
        y1, y2 = int(max(0, b[1] + bh * 0.2)), int(min(h, b[3] - bh * 0.2))
        if x2 <= x1 or y2 <= y1:
            continue
        if (maxch[y1:y2, x1:x2] < DEBRIS_DARK).mean() > DEBRIS_FRACTION:
            continue
        keep.append(b)
    boxes = np.array(keep) if keep else np.zeros((0, 5))
    if len(boxes) < 2:
        return boxes
    boxes = boxes[np.argsort(-boxes[:, 4])]
    area = (boxes[:, 2] - boxes[:, 0]) * (boxes[:, 3] - boxes[:, 1])
    alive = np.ones(len(boxes), bool)
    for i in range(len(boxes)):
        if not alive[i]:
            continue
        j = np.arange(i + 1, len(boxes))
        x1 = np.maximum(boxes[i, 0], boxes[j, 0]); y1 = np.maximum(boxes[i, 1], boxes[j, 1])
        x2 = np.minimum(boxes[i, 2], boxes[j, 2]); y2 = np.minimum(boxes[i, 3], boxes[j, 3])
        inter = np.clip(x2 - x1, 0, None) * np.clip(y2 - y1, 0, None)
        alive[j[inter / np.minimum(area[i], area[j]) > CONTAIN_RATIO]] = False
    return boxes[alive]


def group_key(folder, name):
    """누출 방지를 위한 분할 단위: (농도, Lot, 샘플번호)"""
    stem = os.path.splitext(name)[0]
    t = stem.split(" ")
    return folder, t[0], " ".join(t[2:])


def is_val(folder, name):
    # 검증셋: '레브로' Lot 전체 + EZC Lot 의 9_ 샘플 + 2603 Lot 의 05 샘플
    _, lot, sample = group_key(folder, name)
    return lot == "레브로" or sample.startswith("9_") or (lot == "2603" and sample == "05")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview", type=int, default=0)
    args = ap.parse_args()

    models = [YOLO(os.path.join(TEACHER_DIR, t), task="detect") for t in TEACHERS]

    items = []
    for folder in sorted(os.listdir(IMAGE_ROOT)):
        d = os.path.join(IMAGE_ROOT, folder)
        if os.path.isdir(d):
            items += [(folder, n) for n in sorted(os.listdir(d)) if n.lower().endswith((".jpg", ".jpeg", ".png"))]

    if args.preview:
        random.seed(1)
        items = random.sample(items, args.preview)
        pv = os.path.join(ROOT, "Training", "preview")
        os.makedirs(pv, exist_ok=True)
    else:
        for s in ("train", "val"):
            os.makedirs(os.path.join(DATASET, "images", s), exist_ok=True)
            os.makedirs(os.path.join(DATASET, "labels", s), exist_ok=True)

    counts = []
    for k, (folder, name) in enumerate(items):
        img = imread(os.path.join(IMAGE_ROOT, folder, name))
        h, w = img.shape[:2]
        dets, n_pass = teacher_boxes(models, img)
        boxes = clean(fuse(dets, n_pass), img)
        counts.append(len(boxes))
        stem = re.sub(r"\s+", "_", f"{folder}_{os.path.splitext(name)[0]}")

        if args.preview:
            o = img.copy()
            for b in boxes:
                cv2.rectangle(o, (int(b[0]), int(b[1])), (int(b[2]), int(b[3])), (255, 0, 0), 3)
            imwrite(os.path.join(pv, stem + ".jpg"), o, 85)
            print(f"{folder}/{name}: {len(boxes)}", flush=True)
            continue

        split = "val" if is_val(folder, name) else "train"
        scale = SAVE_WIDTH / w
        small = cv2.resize(img, (SAVE_WIDTH, round(h * scale)), interpolation=cv2.INTER_AREA)
        imwrite(os.path.join(DATASET, "images", split, stem + ".jpg"), small)
        with open(os.path.join(DATASET, "labels", split, stem + ".txt"), "w") as f:
            for b in boxes:
                x1, y1, x2, y2 = np.clip(b[0], 0, w), np.clip(b[1], 0, h), np.clip(b[2], 0, w), np.clip(b[3], 0, h)
                if x2 - x1 < 4 or y2 - y1 < 4:
                    continue
                f.write(f"0 {(x1 + x2) / 2 / w:.6f} {(y1 + y2) / 2 / h:.6f} {(x2 - x1) / w:.6f} {(y2 - y1) / h:.6f}\n")
        if k % 20 == 0:
            print(f"[{k + 1}/{len(items)}] {folder}/{name}: {len(boxes)} cells -> {split}", flush=True)

    if not args.preview:
        with open(os.path.join(DATASET, "cell.yaml"), "w", encoding="utf-8") as f:
            f.write(f"path: {DATASET.replace(os.sep, '/')}\ntrain: images/train\nval: images/val\nnames:\n  0: cell\n")
    print(f"done. images={len(items)} mean cells/image={np.mean(counts):.1f} min={min(counts)} max={max(counts)}")


if __name__ == "__main__":
    main()
