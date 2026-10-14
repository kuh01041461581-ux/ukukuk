# -*- coding: utf-8 -*-
"""HWPX 얼굴 자동 블러 — 얼굴 찾기(YuNet + 측면 보완) · 모자이크/가우시안 블러 · HWPX 다시 묶기

 모든 처리는 이 PC 안에서만 이뤄진다. 모델 파일(assets/models)이 없으면 OpenCV 내장 검출기로 대신한다."""
import io
import os
import pathlib
import sys
from dataclasses import dataclass, field

import numpy as np
from PIL import Image, ImageFilter, ImageOps

from ...core import hwpx as H

try:
    import cv2
except Exception:  # noqa
    cv2 = None


def _assets() -> pathlib.Path:
    base = getattr(sys, "_MEIPASS", None)
    return pathlib.Path(base) / "seomu" / "assets" if base else pathlib.Path(__file__).resolve().parents[2] / "assets"


Box = tuple   # (x, y, w, h) 원본 픽셀


def iou(a: Box, b: Box) -> float:
    x1, y1 = max(a[0], b[0]), max(a[1], b[1])
    x2, y2 = min(a[0] + a[2], b[0] + b[2]), min(a[1] + a[3], b[1] + b[3])
    inter = max(0, x2 - x1) * max(0, y2 - y1)
    union = a[2] * a[3] + b[2] * b[3] - inter
    return inter / union if union > 0 else 0.0


def merge_boxes(primary: list, secondary: list, thresh: float = 0.3) -> list:
    """1차(YuNet) 기준으로 2차가 새로 찾은 얼굴만 더한다. 겹치는 얼굴은 2차의 좌표로 교체."""
    merged, used = [], set()
    for sb in primary:
        best, best_iou = -1, 0.0
        for i, mb in enumerate(secondary):
            if i in used:
                continue
            v = iou(sb, mb)
            if v > best_iou:
                best, best_iou = i, v
        if best >= 0 and best_iou > thresh:
            merged.append(secondary[best])
            used.add(best)
        else:
            merged.append(sb)
    merged += [mb for i, mb in enumerate(secondary) if i not in used]
    return merged


class FaceDetector:
    def __init__(self, sensitivity: float = 0.5):
        if cv2 is None:
            raise RuntimeError("OpenCV 를 불러오지 못했습니다.")
        self.score = float(sensitivity)
        self.yunet_path = _assets() / "models" / "face_detection_yunet_2023mar.onnx"
        self.has_yunet = self.yunet_path.exists() and hasattr(cv2, "FaceDetectorYN")
        data = pathlib.Path(cv2.data.haarcascades)
        self.haar = [cv2.CascadeClassifier(str(data / n)) for n in ("haarcascade_frontalface_default.xml", "haarcascade_profileface.xml")
                     if (data / n).exists()]

    @property
    def engines(self) -> list:
        return (["YuNet"] if self.has_yunet else []) + (["OpenCV 내장 검출기"] if self.haar else [])

    def _yunet(self, bgr: np.ndarray, scale: float) -> list:
        h, w = bgr.shape[:2]
        det = cv2.FaceDetectorYN.create(str(self.yunet_path), "", (w, h), self.score, 0.3, 5000)
        det.setInputSize((w, h))
        _, faces = det.detect(bgr)
        out = []
        if faces is not None:
            for f in faces:
                x, y, fw, fh = f[:4]
                out.append((max(0, x / scale), max(0, y / scale), fw / scale, fh / scale))
        return out

    def _haar(self, bgr: np.ndarray, scale: float) -> list:
        gray = cv2.equalizeHist(cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY))
        out = []
        min_n = 3 if self.score > 0.6 else (2 if self.score > 0.4 else 1)
        for k, c in enumerate(self.haar):
            rects = c.detectMultiScale(gray, 1.1, min_n + 2, minSize=(24, 24))
            for (x, y, w, h) in rects:
                out.append((x / scale, y / scale, w / scale, h / scale))
            if k == 1:      # 반대쪽 측면(좌우 반전)
                flipped = cv2.flip(gray, 1)
                for (x, y, w, h) in c.detectMultiScale(flipped, 1.1, min_n + 2, minSize=(24, 24)):
                    out.append(((gray.shape[1] - x - w) / scale, y / scale, w / scale, h / scale))
        return out

    def detect(self, img: Image.Image) -> list:
        rgb = np.array(img.convert("RGB"))
        bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
        h, w = bgr.shape[:2]
        scale = min(1.0, 1600 / max(w, h))
        small = cv2.resize(bgr, (round(w * scale), round(h * scale))) if scale < 1 else bgr
        primary = self._yunet(small, scale) if self.has_yunet else []
        secondary = self._haar(small, scale) if self.haar else []
        if not self.has_yunet:
            primary, secondary = secondary, []
        boxes = merge_boxes(primary, secondary)
        # 중복 제거(같은 얼굴 여러 번)
        keep: list = []
        for b in sorted(boxes, key=lambda b: -b[2] * b[3]):
            if all(iou(b, k) < 0.5 for k in keep):
                keep.append(b)
        return [tuple(float(v) for v in b) for b in keep]


# ───────────────────── 블러 적용 ─────────────────────
def _pixelate(img: Image.Image, box):
    x, y, w, h = box
    if w <= 0 or h <= 0:
        return
    block = max(4, round(min(w, h) / 10))
    region = img.crop((x, y, x + w, y + h))
    small = region.resize((max(1, w // block), max(1, h // block)), Image.BOX)
    img.paste(small.resize((w, h), Image.NEAREST), (x, y))


def _blur(img: Image.Image, box):
    x, y, w, h = box
    if w <= 0 or h <= 0:
        return
    px = max(8, round(min(w, h) / 5))
    bleed = px * 2
    sx, sy = max(0, x - bleed), max(0, y - bleed)
    ex, ey = min(img.width, x + w + bleed), min(img.height, y + h + bleed)
    patch = img.crop((sx, sy, ex, ey)).filter(ImageFilter.GaussianBlur(px))
    patch = patch.crop((x - sx, y - sy, x - sx + w, y - sy + h))
    # 타원형으로 부드럽게 섞는 마스크 (중앙 62% 까지 불투명 → 가장자리 투명)
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xx + 0.5 - w / 2) / (w / 2)) ** 2 + ((yy + 0.5 - h / 2) / (h / 2)) ** 2)
    alpha = np.clip((1.0 - d) / (1.0 - 0.62), 0, 1)
    mask = Image.fromarray((alpha * 255).astype("uint8"), "L")
    base = img.crop((x, y, x + w, y + h))
    img.paste(Image.composite(patch, base, mask), (x, y))


def apply_blur(img: Image.Image, boxes: list, mode: str = "pixelate", pad: float = 0.3) -> Image.Image:
    out = img.convert("RGB").copy() if img.mode not in ("RGB", "RGBA", "L") else img.copy()
    for (bx, by, bw, bh) in boxes:
        px, py = bw * pad, bh * pad
        x, y = max(0, bx - px), max(0, by - py)
        w, h = min(out.width - x, bw + px * 2), min(out.height - y, bh + py * 2)
        region = (int(round(x)), int(round(y)), int(round(w)), int(round(h)))
        (_pixelate if mode == "pixelate" else _blur)(out, region)
    return out


# ───────────────────── 문서 단위 ─────────────────────
@dataclass
class ImageEntry:
    name: str
    image: Image.Image
    boxes: list = field(default_factory=list)


@dataclass
class Document:
    path: str
    entries: list          # [(이름, bytes)] 원본 그대로
    images: list           # [ImageEntry]


def load_document(path: str, progress=lambda *a: None) -> Document:
    z = H.open_hwpx(path)
    entries, images = [], []
    names = [i.filename for i in z.infolist() if not i.is_dir()]
    for n in names:
        data = z.read(n)
        entries.append((n, data))
        if H.is_bindata_image(n) and not n.lower().endswith((".tif", ".tiff")):
            try:
                im = Image.open(io.BytesIO(data))
                im = ImageOps.exif_transpose(im)
                im.load()
                images.append(ImageEntry(n, im.convert("RGB") if im.mode not in ("RGB", "RGBA") else im))
            except Exception:
                pass
    return Document(path, entries, images)


def detect_all(doc: Document, sensitivity: float, progress=lambda *a: None) -> list:
    det = FaceDetector(sensitivity)
    logs = []
    for i, e in enumerate(doc.images):
        progress(int(i / max(len(doc.images), 1) * 100), e.name)
        e.boxes = det.detect(e.image)
        logs.append((e.name, len(e.boxes)))
    progress(100, "완료")
    return logs


def render(doc: Document, mode: str, pad: float) -> list:
    """보정된 박스를 반영한 이미지 목록 [(이름, PIL.Image)]"""
    return [(e.name, apply_blur(e.image, e.boxes, mode, pad)) for e in doc.images]


def _encode(name: str, im: Image.Image) -> bytes:
    ext = os.path.splitext(name)[1].lower()
    out = io.BytesIO()
    if ext in (".jpg", ".jpeg"):
        im.convert("RGB").save(out, "JPEG", quality=92)
    elif ext == ".bmp":
        im.convert("RGB").save(out, "BMP")
    elif ext == ".gif":
        im.convert("P", palette=Image.ADAPTIVE).save(out, "GIF")
    else:
        im.save(out, "PNG")
    return out.getvalue()


def build_hwpx(doc: Document, rendered: list) -> bytes:
    new = {n: _encode(n, im) for n, im in rendered}
    entries = [(n, new.get(n, d)) for n, d in doc.entries]
    return H.write_hwpx(entries)
