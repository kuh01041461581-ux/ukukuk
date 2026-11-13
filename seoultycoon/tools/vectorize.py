"""도트 그림을 '벡터풍'으로 다시 그린다(색 면의 윤곽을 매끈한 곡선으로, 면 안은 평평한 색, 경계는 또렷하게 — 흐리게 섞지 않는다).
1) 격자 디더(두 색 체크무늬)는 두 색의 평균 한 면으로 합친다  2) 색마다 '그 색인 곳' 지도를 K배로 키워 살짝 번지게 한 뒤 화소마다 가장 센 색을 고른다(경계가 매끈한 곡선이 되고 틈이 생기지 않는다)
3) 투명도도 한 '색'으로 같이 다뤄 실루엣도 매끈하게."""
import numpy as np
from PIL import Image, ImageFilter
def undither(a):
    """a: (h,w,4) uint8. 체크무늬 디더를 평균색으로."""
    h, w = a.shape[:2]; rgb = a[..., :3].astype(np.int32); al = a[..., 3] > 0; out = a.copy()
    def same(x, y): return (x == y).all(-1)
    for dy in range(1, h - 1):
        for dx in range(1, w - 1):
            if not al[dy, dx]: continue
            p = rgb[dy, dx]; n = [rgb[dy - 1, dx], rgb[dy + 1, dx], rgb[dy, dx - 1], rgb[dy, dx + 1]]
            if (n[0] == n[1]).all() and (n[2] == n[3]).all() and (n[0] == n[2]).all() and not (n[0] == p).all():
                dg = [rgb[dy - 1, dx - 1], rgb[dy - 1, dx + 1], rgb[dy + 1, dx - 1], rgb[dy + 1, dx + 1]]
                if sum((d == p).all() for d in dg) >= 3 and np.abs(p - n[0]).sum() < 150: out[dy, dx, :3] = ((p + n[0]) // 2).astype(np.uint8)
    return out
def vectorize(im, K=4, sigma=0.42, dither=True):
    """im: RGBA PIL 이미지 → K배 크기 RGBA PIL 이미지."""
    a = np.asarray(im.convert('RGBA')).copy(); h, w = a.shape[:2]
    if dither: a = undither(a)
    key = (a[..., 0].astype(np.int64) << 24) | (a[..., 1].astype(np.int64) << 16) | (a[..., 2].astype(np.int64) << 8) | np.where(a[..., 3] > 0, 1, 0)
    cols, inv = np.unique(key, return_inverse=True); inv = inv.reshape(h, w); n = len(cols)
    best = np.full((h * K, w * K), -1.0, np.float32); lab = np.zeros((h * K, w * K), np.int32)
    pad = 2
    for ci in range(n):
        m = (inv == ci).astype(np.uint8) * 255
        mp = np.pad(m, pad, mode='edge'); mi = Image.fromarray(mp).resize(((w + 2 * pad) * K, (h + 2 * pad) * K), Image.NEAREST).filter(ImageFilter.GaussianBlur(sigma * K))
        v = np.asarray(mi).astype(np.float32)[pad * K:(h + pad) * K, pad * K:(w + pad) * K]
        up = v > best; best[up] = v[up]; lab[up] = ci
    pal = np.zeros((n, 4), np.uint8)
    for ci, c in enumerate(cols): pal[ci] = ((c >> 24) & 255, (c >> 16) & 255, (c >> 8) & 255, 255 if (c & 1) else 0)
    return Image.fromarray(pal[lab], 'RGBA')
def vec_hd(im, K=2, S=4, dither=False):
    """벡터풍으로 S배에서 그린 뒤 K배로 줄인다(면적 평균 → 윤곽이 부드럽게 안티앨리어싱된다)."""
    v = vectorize(im, S, dither=dither)
    return v if S == K else v.resize((im.width * K, im.height * K), Image.BOX)
def vec_sheet(im, rects, K=2, S=4):
    """건물 시트: 게임이 쓰는 조각 사각형마다 따로 그려서(조각 이음새로 색이 번지지 않게) 같은 자리에 붙인다."""
    out = Image.new('RGBA', (im.width * K, im.height * K), (0, 0, 0, 0)); done = set()
    for (x, y, w, h) in rects:
        if w < 2 or h < 2 or (x, y, w, h) in done: continue
        done.add((x, y, w, h)); x1, y1 = min(im.width, x + w), min(im.height, y + h)
        if x1 <= x or y1 <= y: continue
        out.alpha_composite(vec_hd(im.crop((x, y, x1, y1)), K, S), (x * K, y * K))
    return out
if __name__ == '__main__':
    import sys, os
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import strips as ST
    out = []
    for k in (11, 10):
        c = ST.compose(k, ST.orig(k)); v = vectorize(c, 4); nn = c.resize((c.width * 4, c.height * 4), Image.NEAREST)
        out.append((nn, v))
    W = sum(a.width + b.width + 20 for a, b in out); H = max(a.height for a, _ in out); w = Image.new('RGBA', (W, H), (150, 70, 160, 255)); x = 0
    for a, b in out: w.alpha_composite(a, (x, 0)); w.alpha_composite(b, (x + a.width + 10, 0)); x += a.width + b.width + 20
    w.save(sys.argv[1])
