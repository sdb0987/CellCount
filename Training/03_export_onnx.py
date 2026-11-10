# -*- coding: utf-8 -*-
"""
3단계: ONNX 내보내기 (C# 프로그램에서 사용)

    python 03_export_onnx.py
    -> C:\\project\\CellCount\\Models\\cellcount_yolo26s.onnx

YOLO26 은 NMS-free(end-to-end) 모델이라 출력이 [1, max_det, 6] = (x1, y1, x2, y2, score, class) 이다.
기본 max_det 은 300 이므로 반드시 1000 으로 늘려서 내보낸다(이미지당 세포 300~550개).
입력 크기는 원본 비율(3088x2076)에 맞춘 1280x864 고정.
"""
import argparse
import os
import shutil

from ultralytics import YOLO

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", default="cellcount_yolo26s")
    ap.add_argument("--max-det", type=int, default=1000)
    args = ap.parse_args()

    best = os.path.join(HERE, "runs", args.name, "weights", "best.pt")
    model = YOLO(best)
    onnx_path = model.export(format="onnx", imgsz=(864, 1280), opset=17, simplify=True, max_det=args.max_det, device="cpu")
    os.makedirs(os.path.join(ROOT, "Models"), exist_ok=True)
    dst = os.path.join(ROOT, "Models", args.name + ".onnx")
    shutil.copyfile(onnx_path, dst)
    print("saved:", dst)


if __name__ == "__main__":
    main()
