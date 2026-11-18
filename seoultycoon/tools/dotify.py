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
