"""남자 시장 표정 조각(ff)을 아래 바탕 얼굴(fd0)에 자연스럽게 잇는다: 살색 톤을 같은 자리 바탕 얼굴 색으로 저주파 보정,
위(이마)·좌우 가장자리는 거리 기반으로 서서히 투명하게, 맨 끝 열·행도 경계로 계산. 표정 조각은 바탕 얼굴보다 저해상도 5칸(HD 10칸) 오른쪽.  → st2android/mod/ff{0,1,2,6,7,8,15,16,17}.d"""
import os, io
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); mod = root + '/st2android/mod'
MALE = (0, 1, 2, 6, 7, 8, 15, 16, 17); DX = 5
def layers(n):
    raw = open(f'{mod}/{n}.d', 'rb').read(); e1 = raw.find(b'IEND') + 8; e2 = raw.find(b'IEND', e1) + 8
    return raw, e1, e2, np.asarray(Image.open(io.BytesIO(raw[:e1])).convert('RGBA')).astype(float), np.asarray(Image.open(io.BytesIO(raw[e1:e2])).convert('RGBA')).astype(float)
_, _, _, FL, FH = layers('fd0')
def skin(a): return (a[..., 3] > 200) & (a[..., 0] > 150) & (a[..., 0] > a[..., 2] + 30) & (a[..., :3].mean(2) > 110)
def fit(a, base, k):
    a = a.copy(); h, w = a.shape[:2]; s = 2 // k; dx = DX * (2 // s) if False else DX * (1 if k == 1 else 2)
    under = np.zeros((h, w, 4)); bh, bw = base.shape[:2]
    for y in range(h):
        for x in range(w):
            X = x + dx
            if 0 <= y < bh and 0 <= X < bw: under[y, x] = base[y, X]
    sk, ub = skin(a), skin(under); both = sk & ub; sig = 6.0 / k
    if both.sum() > 10:
        wgt = ndi.gaussian_filter(both.astype(float), sig) + 1e-3
        diff = np.dstack([ndi.gaussian_filter(np.where(both, under[..., c] - a[..., c], 0), sig) for c in range(3)]) / wgt[..., None]
        a[..., :3] = np.clip(a[..., :3] + np.clip(diff, -50, 50) * sk[..., None] * 0.85, 0, 255)
    d = ndi.distance_transform_edt(np.pad(a[..., 3] > 30, 1))[1:-1, 1:-1]; side = np.clip(d / (6.0 / k), 0, 1)
    top = np.clip((np.arange(h)[:, None] + 0.5) / (10.0 / k), 0, 1) ** 1.3
    fade = np.minimum(side, np.broadcast_to(top, side.shape)); a[..., 3] = a[..., 3] * np.where(ub, fade, 1.0)     # 아래가 바탕 살색인 곳만 흐린다
    return a
for i in MALE:
    n = f'ff{i}'; raw, e1, e2, lo, hd = layers(n); b1, b2 = io.BytesIO(), io.BytesIO()
    Image.fromarray(fit(lo, FL, 1).astype(np.uint8), 'RGBA').save(b1, format='PNG'); Image.fromarray(fit(hd, FH, 2).astype(np.uint8), 'RGBA').save(b2, format='PNG')
    open(f'{mod}/{n}.d', 'wb').write(b1.getvalue() + b2.getvalue() + raw[e2:])
print('남자 표정 조각 이음새 정리', len(MALE))
