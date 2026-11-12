"""원본 건물 도트의 모양·조각 배치는 그대로 두고 퀄리티만 올린다.
원본은 색 수가 적어 격자무늬(디더)로 중간색을 흉내 냈다 → 격자 디더를 실제 중간색의 부드러운 음영으로 풀어 준다(색 깊이 확장).
조각 경계를 넘어 섞지 않도록 조각 칸마다 따로 처리한다(게임 조립 뒤에도 이음새가 맞는다)."""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import strips as ST
def labels(k, W, H):
    lab = -np.ones((H, W), int)
    if k in ST.TABLE and k < 59:
        rs = ST.TABLE[k]['rects']
        for i in sorted(range(len(rs)), key=lambda i: -(rs[i][2] * rs[i][3])):       # 작은 조각이 위에 오도록
            x, y, w, h = rs[i]; lab[y:y + h, x:x + w] = i
        lab[H - 8:H, 0:64] = 100; lab[H - 8:H, 64:W] = 101
    else: lab[:, :] = 0
    return lab
def enhance(k, strength=1.0, im=None):
    im = im or ST.orig(k); a = np.asarray(im).astype(np.float32); H, W = a.shape[:2]; rgb = a[..., :3]; al = a[..., 3] > 0; lab = labels(k, W, H)
    out = rgb.copy()
    def same(y, x, yy, xx): return 0 <= yy < H and 0 <= xx < W and al[yy, xx] and lab[yy, xx] == lab[y, x]
    def eq(c1, c2): return np.abs(c1 - c2).sum() < 6
    for y in range(H):
        for x in range(W):
            if not al[y, x]: continue
            c = rgb[y, x]
            n4 = [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]; nd = [(y - 1, x - 1), (y - 1, x + 1), (y + 1, x - 1), (y + 1, x + 1)]
            v4 = [rgb[p] for p in n4 if same(y, x, *p)]; vd = [rgb[p] for p in nd if same(y, x, *p)]
            if len(v4) < 3 or len(vd) < 2: continue
            diff4 = sum(1 for v in v4 if not eq(v, c)); samed = sum(1 for v in vd if eq(v, c))
            # 격자 디더: 상하좌우는 다른 색(서로는 같은 색), 대각은 자기와 같은 색
            if diff4 >= len(v4) - 0 and samed >= max(2, len(vd) - 1):
                m = np.mean(v4, axis=0)
                if all(eq(v, v4[0]) for v in v4) and np.abs(m - c).sum() < 240:      # 두 색의 격자 → 중간색
                    out[y, x] = c * (1 - 0.5 * strength) + m * (0.5 * strength)
    # 경계 보존 매끄럽게: 색이 비슷한 이웃끼리만 조금 섞어 계단진 음영을 풀어 준다(윤곽·글자는 그대로)
    sm = out.copy()
    for y in range(H):
        for x in range(W):
            if not al[y, x]: continue
            c = out[y, x]; acc = c * 2.0; wt = 2.0
            for p in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if same(y, x, *p):
                    dd = np.abs(out[p] - c).sum()
                    if dd < 70: acc = acc + out[p]; wt += 1.0
            sm[y, x] = acc / wt
    out = sm
    # 위쪽은 살짝 밝게, 아래쪽은 살짝 어둡게(조립 그림의 세로 위치 기준 → 조각이 달라도 이어진다) + 채도 약간
    if k in ST.TABLE and k < 59:
        r, main, MY = ST.info(k); pos = {}
        for e in ST.entries(k): pos.setdefault(e[0], (e[1], e[2]))
        Hc = MY + main[3] + 16; rs = r['rects']
        for y in range(H):
            for x in range(W):
                if not al[y, x]: continue
                i = lab[y, x]
                if i == 100: cy = MY + main[3] + (y - (H - 8))
                elif i == 101: cy = MY + main[3] + 8 + (y - (H - 8))
                elif i >= 0 and i in pos: cy = pos[i][1] + (y - rs[i][1])
                else: continue
                g = 1.07 - 0.13 * (cy / max(1, Hc))
                c = out[y, x] * g; m = c.mean(); out[y, x] = m + (c - m) * 1.10
    res = np.zeros((H, W, 4), np.uint8); res[..., :3] = np.clip(out, 0, 255).astype(np.uint8); res[..., 3] = al * 255
    return Image.fromarray(res, 'RGBA')
if __name__ == '__main__':
    ks = [int(x) for x in sys.argv[2:]] or [11, 10, 0, 49]
    w = Image.new('RGBA', (len(ks) * 208, 110), (150, 70, 160, 255))
    for i, k in enumerate(ks):
        for j, s in enumerate((ST.orig(k), enhance(k))):
            c = ST.compose(k, s); t = max(0, c.height - 110); pc = c.crop((0, t, 100, c.height)); w.alpha_composite(pc, (i * 208 + j * 104, 110 - pc.height))
    w.resize((w.width * 5, w.height * 5), Image.NEAREST).save(sys.argv[1])
