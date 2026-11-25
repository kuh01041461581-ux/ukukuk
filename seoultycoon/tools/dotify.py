"""옛 석탑(미륵사지·왕궁리)의 모양은 살리고, 질감만 원본 서울타이쿤식 도트로: 나무 제거 → 색을 소수 팔레트로 모으고 인접 두 색을 체크 디더로 섞음 → 윤곽선은 검정 대신 한 단계 어두운 톤."""
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
def untree(F):
    a = np.asarray(F.convert('RGBA')).astype(int).copy(); tree = (a[..., 3] > 0) & (a[..., 1] > a[..., 0] + 22) & (a[..., 1] > a[..., 2] + 22) & ((a[..., 1] < 200))
    tree = ndi.binary_dilation(tree, iterations=1)
    ground = (a[..., 3] > 0) & ~tree
    for y, x in zip(*np.nonzero(tree)):
        ys, xs = slice(max(0, y - 6), y + 7), slice(max(0, x - 6), x + 7); g = ground[ys, xs]
        if g.any(): a[y, x] = np.median(a[ys, xs][g], axis=0).astype(int)
    return Image.fromarray(a.astype(np.uint8), 'RGBA')
def dotify(F, ncol=22):
    a = np.asarray(F.convert('RGBA')).astype(float); al = a[..., 3] > 127; rgb = a[..., :3]
    pal = np.asarray(Image.fromarray(rgb.astype(np.uint8)).quantize(colors=ncol, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert('RGB').getcolors(1 << 20) and
                     [c for _, c in sorted(Image.fromarray(rgb.astype(np.uint8)).quantize(colors=ncol, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert('RGB').getcolors(1 << 20), reverse=True)][:ncol], float)
    H, W = al.shape; out = np.zeros((H, W, 4), np.uint8); B = np.array([[0.25, 0.75], [1.0, 0.5]])
    flat = rgb.reshape(-1, 3); d = ((flat[:, None, :] - pal[None]) ** 2).sum(2); idx = np.argsort(d, axis=1)[:, :2]
    for y in range(H):
        for x in range(W):
            if not al[y, x]: continue
            i1, i2 = idx[y * W + x]; p1, p2 = pal[i1], pal[i2]; v = rgb[y, x]; den = ((p2 - p1) ** 2).sum()
            t = float(((v - p1) * (p2 - p1)).sum() / den) if den > 0 else 0.0; t = min(max(t, 0.0), 1.0)
            c = p2 if t > B[y % 2, x % 2] * 0.999 + (1e-6) and t >= 0.15 else p1
            out[y, x, :3] = c; out[y, x, 3] = 255
    edge = al & ~(np.roll(al, 1, 0) & np.roll(al, -1, 0) & np.roll(al, 1, 1) & np.roll(al, -1, 1))
    out[edge, :3] = (out[edge, :3] * 0.7).astype(np.uint8)
    return Image.fromarray(out, 'RGBA')

def tint(F, mul, add=(0, 0, 0)):
    """돌 색(채도 낮고 밝은 화소)만 사진 색으로 물들인다."""
    a = np.asarray(F.convert('RGBA')).astype(float).copy(); mx = a[..., :3].max(2); mn = a[..., :3].min(2); sat = (mx - mn) / np.maximum(mx, 1); lum = a[..., :3].mean(2)
    m = (a[..., 3] > 0) & (sat < 0.30) & (lum > 120)
    for c in range(3): a[..., c] = np.where(m, np.clip(lum * mul[c] + add[c], 0, 255), a[..., c])
    return Image.fromarray(a.astype(np.uint8), 'RGBA')

LAST_HD = None
def pixelize2(F1, F2, ncol=26, mul=None):
    """옛 석탑처럼 고해상도로 그려 둔 그림을 그대로 도트화: 2배판(F2)을 소수 팔레트로 모으고(디더 없음) 3x3 다수결로 잡티를 지운 뒤,
    1배판은 2x2 블록 다수결로 만든다. 체크 디더 잡음이 없어 또렷하다. 돌 색 보정(mul)과 나무 제거도 여기서."""
    global LAST_HD
    F2 = untree(F2)
    if mul: F2 = tint(F2, mul)
    a = np.asarray(F2.convert('RGBA')); al = a[..., 3] > 127
    q = Image.fromarray(a[..., :3]).quantize(colors=ncol, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    idx = np.asarray(q).astype(int); pal = np.asarray(q.getpalette()[:ncol * 3]).reshape(-1, 3)
    from scipy import ndimage as _nd
    def mode3(ix):
        best = np.zeros_like(ix); cnt = np.full(ix.shape, -1)
        for c in np.unique(ix[al]):
            n = _nd.uniform_filter((ix == c).astype(float), 3)
            m = n > cnt; best[m] = c; cnt[m] = n[m]
        return best
    idx = np.where(al, mode3(idx), idx)
    rgb = pal[idx].astype(np.uint8); e = al & ~(np.roll(al, 1, 0) & np.roll(al, -1, 0) & np.roll(al, 1, 1) & np.roll(al, -1, 1))
    rgb[e] = (rgb[e] * 0.72).astype(np.uint8)
    hd = Image.fromarray(np.dstack([rgb, (al * 255).astype(np.uint8)]), 'RGBA'); LAST_HD = hd
    # 1배: 2x2 블록에서 가장 많은 팔레트 색
    h, w = idx.shape; h2, w2 = h // 2, w // 2; lo = np.zeros((h2, w2, 4), np.uint8)
    for y in range(h2):
        for x in range(w2):
            blk = idx[2 * y:2 * y + 2, 2 * x:2 * x + 2].ravel(); ab = al[2 * y:2 * y + 2, 2 * x:2 * x + 2].ravel()
            if ab.sum() < 2: continue
            vals, cts = np.unique(blk[ab], return_counts=True); c = vals[np.argmax(cts)]; lo[y, x, :3] = pal[c]; lo[y, x, 3] = 255
    la = lo[..., 3] > 0; e1 = la & ~(np.roll(la, 1, 0) & np.roll(la, -1, 0) & np.roll(la, 1, 1) & np.roll(la, -1, 1)); lo[e1, :3] = (lo[e1, :3] * 0.72).astype(np.uint8)
    return Image.fromarray(lo, 'RGBA')

def flatdot(F, ncol=24, mul=None):
    """1배 그림을 그대로 도트화: 나무 제거·돌 색 보정 → 팔레트 ncol 색(디더 없음) → 외톨이 화소만 주변 다수색으로 → 윤곽은 한 단계 어두운 톤."""
    F = untree(F)
    if mul: F = tint(F, mul)
    a = np.asarray(F.convert('RGBA')); al = a[..., 3] > 127
    q = Image.fromarray(a[..., :3]).quantize(colors=ncol, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    idx = np.asarray(q).astype(int); pal = np.asarray(q.getpalette()[:ncol * 3]).reshape(-1, 3)
    H, W = idx.shape; out = idx.copy()
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if not al[y, x]: continue
            nb = idx[y - 1:y + 2, x - 1:x + 2].ravel(); same = (nb == idx[y, x]).sum() - 1
            if same == 0:
                m = al[y - 1:y + 2, x - 1:x + 2].ravel(); v, c = np.unique(nb[m], return_counts=True); out[y, x] = v[np.argmax(c)]
    rgb = pal[out].astype(np.uint8); e = al & ~(np.roll(al, 1, 0) & np.roll(al, -1, 0) & np.roll(al, 1, 1) & np.roll(al, -1, 1)); rgb[e] = (rgb[e] * 0.72).astype(np.uint8)
    return Image.fromarray(np.dstack([rgb, (al * 255).astype(np.uint8)]), 'RGBA')
