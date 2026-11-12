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
def stylize(im, sat=1.45, con=1.22, dither=10.0, max_colors=48):
    """새 도트를 원작 화풍으로 정리한다(docs/PIXEL_ART_GUIDE.md):
    채도·대비 → 원작 팔레트 맞춤 → 건물당 색 수 제한 → 외톨이 화소 제거 → 색상 이동된 윤곽(바깥 윤곽은 그 자리 색을 어둡고 푸르게, 면 경계는 한 단 어둡게)."""
    a = np.asarray(im).astype(np.float32); rgb = a[..., :3]; al = a[..., 3] > 0; h, w = al.shape
    g = rgb.mean(-1, keepdims=True); rgb = g + (rgb - g) * sat; rgb = (rgb - 128) * con + 128
    yy, xx = np.mgrid[0:h, 0:w]; rgb = np.clip(rgb + (BAYER[yy % 2, xx % 2] * dither)[..., None], 0, 255)
    pal = palette(); W3 = np.array([0.30, 0.59, 0.11], dtype=np.float32)
    def nearest(colors, table):
        out = np.empty(len(colors), int)
        for s in range(0, len(colors), 4096):
            c = colors[s:s + 4096]; out[s:s + 4096] = (((c[:, None, :] - table[None, :, :]) ** 2) * W3).sum(-1).argmin(1)
        return out
    idx = nearest(rgb.reshape(-1, 3), pal).reshape(h, w)
    # 색 수 제한: 많이 쓰인 색만 남기고 나머지는 그중 가장 가까운 색으로
    used, cnt = np.unique(idx[al], return_counts=True)
    if len(used) > max_colors:
        keep = used[np.argsort(-cnt)[:max_colors]]; kt = pal[keep]; remap = {int(u): int(keep[nearest(pal[[u]], kt)[0]]) for u in used}
        for y in range(h):
            for x in range(w):
                if al[y, x]: idx[y, x] = remap[int(idx[y, x])]
    # 외톨이 화소 제거: 8방향 이웃 어디에도 같은 색이 없으면 가장 많은 이웃 색으로
    for _ in range(2):
        nxt = idx.copy()
        for y in range(h):
            for x in range(w):
                if not al[y, x]: continue
                nb = [idx[y + dy, x + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dy or dx) and 0 <= y + dy < h and 0 <= x + dx < w and al[y + dy, x + dx]]
                if len(nb) >= 5 and idx[y, x] not in nb: vals, c = np.unique(nb, return_counts=True); nxt[y, x] = vals[c.argmax()]
        idx = nxt
    rgb = pal[idx]; lum = rgb @ W3; res = rgb.copy()
    HS = np.array([0.50, 0.46, 0.66], dtype=np.float32)                      # 윤곽: 어둡게 + 푸른 쪽으로(색상 이동)
    for y in range(h):
        for x in range(w):
            if not al[y, x]: continue
            below = not (y + 1 < h and al[y + 1, x]); side = not (0 <= x - 1 and al[y, x - 1]) or not (x + 1 < w and al[y, x + 1]); above = not (y - 1 >= 0 and al[y - 1, x])
            if below or side: res[y, x] = rgb[y, x] * HS
            elif above: res[y, x] = rgb[y, x] * (HS * 0.4 + 0.6)                 # 하늘과 닿는 윗윤곽은 약하게
            else:
                for dy, dx in ((1, 0), (0, 1)):
                    if y + dy < h and x + dx < w and al[y + dy, x + dx] and lum[y, x] - lum[y + dy, x + dx] < -46: res[y, x] = rgb[y, x] * 0.80; break
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
