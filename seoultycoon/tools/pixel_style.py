"""새 도트에 원작 화풍을 입힌다: 채도·대비를 올리고, 원작 건물 시트에서 모은 팔레트로 색을 맞추고(약한 격자 디더), 바깥 윤곽과 면 경계에 어두운 선을 넣는다."""
import os, sys, glob
import numpy as np
from PIL import Image
_PAL = None
def palette(pk='/tmp/pk'):
    global _PAL
    if _PAL is None:
        cs = set()
        for f in glob.glob(pk + '/b*.d'):
            a = np.asarray(Image.open(f).convert('RGBA')); m = a[..., 3] > 0; cs.update(map(tuple, a[m][:, :3]))
        _PAL = np.array(sorted(cs), dtype=np.float32)
    return _PAL
BAYER = np.array([[0, 2], [3, 1]], dtype=np.float32) / 4.0 - 0.375
def stylize(im, sat=1.45, con=1.22, dither=14.0, outline=0.50, edge=0.78):
    a = np.asarray(im).astype(np.float32); rgb = a[..., :3]; al = a[..., 3] > 0; h, w = al.shape
    # 채도·대비
    g = rgb.mean(-1, keepdims=True); rgb = g + (rgb - g) * sat; rgb = (rgb - 128) * con + 128
    # 격자 디더 뒤 원작 팔레트로
    yy, xx = np.mgrid[0:h, 0:w]; rgb = rgb + (BAYER[yy % 2, xx % 2] * dither)[..., None]; rgb = np.clip(rgb, 0, 255)
    pal = palette(); flat = rgb.reshape(-1, 3); out = np.empty_like(flat)
    for s in range(0, len(flat), 4096):
        c = flat[s:s + 4096]; d = ((c[:, None, :] - pal[None, :, :]) ** 2 * np.array([0.30, 0.59, 0.11])).sum(-1); out[s:s + 4096] = pal[d.argmin(1)]
    rgb = out.reshape(h, w, 3)
    # 면 경계(밝기가 크게 꺾이는 곳)와 바깥 윤곽을 어둡게
    lum = rgb @ np.array([0.30, 0.59, 0.11]); res = rgb.copy()
    for y in range(h):
        for x in range(w):
            if not al[y, x]: continue
            outer = any(not (0 <= y + dy < h and 0 <= x + dx < w and al[y + dy, x + dx]) for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if outer: res[y, x] = rgb[y, x] * outline; continue
            for dy, dx in ((1, 0), (0, 1)):
                if y + dy < h and x + dx < w and al[y + dy, x + dx] and lum[y, x] - lum[y + dy, x + dx] < -46: res[y, x] = rgb[y, x] * edge; break
    o = np.zeros((h, w, 4), np.uint8); o[..., :3] = np.clip(res, 0, 255).astype(np.uint8); o[..., 3] = al * 255
    return Image.fromarray(o, 'RGBA')

def twinkle(im, phase):
    """애니메이션용 변형 프레임: 유리창(파란 계열)의 일부를 불 켠 색으로 바꾼다. 유리가 거의 없으면 윗윤곽에 반짝임을 넣는다. phase 마다 켜지는 자리가 다르다."""
    a = np.asarray(im).copy(); h, w = a.shape[:2]; r, g, b, al = (a[..., i].astype(int) for i in range(4))
    glass = (al > 0) & (b > r + 40) & (g > r + 12) & (b > 120)      # 광장 바닥(연보라: 빨강=초록)은 뺀다
    if glass.sum() >= 40:
        for y in range(h):
            for x in range(w):
                if glass[y, x] and ((x // 2) * 7 + (y // 2) * 13 + phase * 3) % 5 == 0: a[y, x, :3] = (255, 240, 150)
    else:
        n = 0
        for x in range(4, w - 4):
            ys = np.where(al[:, x] > 0)[0]
            if len(ys) == 0: continue
            y = ys[0]; n += 1
            if (n + phase * 4) % 9 == 0 and y + 2 < h:
                for dx, dy in ((0, 0), (0, 1), (0, 2), (-1, 1), (1, 1)):
                    if al[y + dy, x + dx] > 0: a[y + dy, x + dx, :3] = (255, 255, 255) if (dx, dy) == (0, 1) else (255, 244, 170)
    return Image.fromarray(a, 'RGBA')
