# -*- coding: utf-8 -*-
"""
4단계: 검증셋에서 "세포 수" 기준 평가 + 기본 Threshold 선정

내보낸 ONNX 모델을 검증 이미지에 적용하여, Threshold 별로
라벨 개수 대비 검출 개수의 오차(MAE, 평균 편차)를 출력한다.

    python 04_evaluate_counts.py
"""
import glob
import os

import cv2
import numpy as np
from ultralytics import YOLO

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)


def main():
    model = YOLO(os.path.join(ROOT, "Models", "cellcount_yolo26s.onnx"), task="detect")
    images = sorted(glob.glob(os.path.join(HERE, "dataset", "images", "val", "*.jpg")))
    thresholds = [0.10, 0.15, 0.20, 0.25, 0.30, 0.35, 0.40, 0.50]
    gt, pred = [], {t: [] for t in thresholds}
    for p in images:
        lab = os.path.join(HERE, "dataset", "labels", "val", os.path.splitext(os.path.basename(p))[0] + ".txt")
        with open(lab) as f:
            gt.append(sum(1 for line in f if line.strip()))
        img = cv2.imdecode(np.fromfile(p, dtype=np.uint8), cv2.IMREAD_COLOR)
        r = model.predict(img, conf=0.05, imgsz=(864, 1280), max_det=1000, verbose=False)[0]
        scores = r.boxes.conf.cpu().numpy()
        for t in thresholds:
            pred[t].append(int((scores >= t).sum()))
    gt = np.array(gt, float)
    print(f"val images={len(images)}  mean labeled cells={gt.mean():.1f}")
    print("conf   mean_pred   bias(pred-label)   MAE    MAPE%   corr")
    for t in thresholds:
        p = np.array(pred[t], float)
        print(f"{t:.2f}   {p.mean():8.1f}   {np.mean(p - gt):+8.1f}        {np.mean(np.abs(p - gt)):6.1f}  {100 * np.mean(np.abs(p - gt) / gt):5.1f}   {np.corrcoef(p, gt)[0, 1]:.3f}")


if __name__ == "__main__":
    main()
