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

# 구레나룻: 바탕 얼굴(fd0) 옆, 머리 조각(fh0~fh5) 아래로 비는 자리(배경이 비침)를 머리카락 색으로 메운다
def save_layers(n, raw, e2, lo, hd):
    b1, b2 = io.BytesIO(), io.BytesIO(); Image.fromarray(lo.astype(np.uint8), 'RGBA').save(b1, format='PNG'); Image.fromarray(hd.astype(np.uint8), 'RGBA').save(b2, format='PNG')
    open(f'{mod}/{n}.d', 'wb').write(b1.getvalue() + b2.getvalue() + raw[e2:])
def sideburn(a, k):
    a = a.copy(); h, w = a.shape[:2]; op = a[..., 3] > 100; lum = a[..., :3].mean(2)
    hair = op & (lum < 90); hc = np.median(a[hair][:, :3], axis=0) if hair.any() else np.array([50., 30., 30.])
    sk = skin(a)
    for y in range(int(h * 0.62)):
        xs = np.where(sk[y])[0]
        if len(xs) < 2: continue
        L, R = xs.min(), xs.max(); wd = max(1, int(round((0.62 * h - y) / (0.62 * h) * 5 * k / 2 + 1)))   # 위는 넓게, 귀 아래로 갈수록 좁게
        for x in range(max(0, L - wd), L):
            if a[y, x, 3] < 100: a[y, x, :3] = hc * (0.9 + 0.1 * (x - L + wd) / wd); a[y, x, 3] = 255
        for x in range(R + 1, min(w, R + 1 + wd)):
            if a[y, x, 3] < 100: a[y, x, :3] = hc * (0.9 + 0.1 * (R + wd - x) / wd); a[y, x, 3] = 255
    return a
for n in ('fd0', 'fd2'):
    if os.path.exists(f'{mod}/{n}.d'):
        raw, e1, e2, lo, hd = layers(n); save_layers(n, raw, e2, sideburn(lo, 1), sideburn(hd, 2))
# 어깨: 상체 조각(fb0, fb2) 위쪽 줄의 안쪽 빈칸(목과 어깨 사이)을 양옆 색으로 메운다
def fill_top(a, rows):
    a = a.copy(); h, w = a.shape[:2]
    for y in range(min(rows, h)):
        op = np.where(a[y, :, 3] > 100)[0]
        if len(op) < 2: continue
        for x in range(op.min(), op.max() + 1):
            if a[y, x, 3] <= 100:
                l = x - 1
                while l > op.min() and a[y, l, 3] <= 100: l -= 1
                r = x + 1
                while r < op.max() and a[y, r, 3] <= 100: r += 1
                a[y, x] = a[y, l] if (x - l) <= (r - x) else a[y, r]
    return a
for n in ('fb0', 'fb2'):
    if os.path.exists(f'{mod}/{n}.d'):
        raw, e1, e2, lo, hd = layers(n)
        if b'BIGH' not in raw: save_layers(n, raw, e2, fill_top(lo, 6), fill_top(hd, 12))
print('남자 구레나룻·어깨 빈칸 메움')
