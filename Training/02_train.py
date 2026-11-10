# -*- coding: utf-8 -*-
"""
2단계: YOLO26 Object Detection 학습

    python 02_train.py                       # yolo26s, 1280px, 80 epoch
    python 02_train.py --model yolo26n.pt    # 더 가벼운 모델
"""
import argparse
import os

from ultralytics import YOLO

HERE = os.path.dirname(os.path.abspath(__file__))
PRETRAINED_DIR = r"C:\Users\YDRnD\yolov26"   # COCO 사전학습 가중치(yolo26n/s/m.pt) 위치


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="yolo26s.pt")
    ap.add_argument("--epochs", type=int, default=40)
    ap.add_argument("--imgsz", type=int, default=1280)
    ap.add_argument("--batch", type=int, default=4)
    ap.add_argument("--name", default="cellcount_yolo26s")
    args = ap.parse_args()

    weights = os.path.join(PRETRAINED_DIR, args.model)
    model = YOLO(weights if os.path.exists(weights) else args.model)
    model.train(
        data=os.path.join(HERE, "dataset", "cell.yaml"),
        epochs=args.epochs,
        imgsz=args.imgsz,
        batch=args.batch,
        device=0,
        workers=4,
        project=os.path.join(HERE, "runs"),
        name=args.name,
        exist_ok=True,
        single_cls=True,
        max_det=1000,        # 한 장에 300개 이상 세포가 있으므로 기본값(300)으로는 부족
        # 현미경 영상은 방향성이 없으므로 상하/좌우 반전 모두 사용
        flipud=0.5, fliplr=0.5,
        # 염색 색상이 곧 정보이므로 색조(H) 변형은 작게, 채도/명도는 염색 편차를 흉내낼 만큼만
        hsv_h=0.01, hsv_s=0.4, hsv_v=0.25,
        scale=0.3, mosaic=1.0, close_mosaic=10,
        patience=30,
    )


if __name__ == "__main__":
    main()
