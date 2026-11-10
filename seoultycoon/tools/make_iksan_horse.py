"""카트라이더파크(b9, 96x69) 자리를 '익산공공승마장'으로 새로 그린다 → work/pak_mod/b9.png (tools/st2_pak_build.py 가 data1.pak 에 묶음).
16비트급 도트를 위해 요소(말과 기수, 나무, 마구간 지붕 등)는 고해상도로 그린 뒤 줄여서 색을 제한(양자화)하고 윤곽선을 넣는다.
같은 크기·같은 아이소 바닥 다이아몬드: 줄무늬 잔디 + 모래 마장(갈퀴 자국) + 이중 나무 울타리 + 장애물 + 마구간 + 말/기수 + 나무 + 깃발.
사용: python3 -I tools/make_iksan_horse.py [미리보기.png]"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = root + '/work/pak_mod'; os.makedirs(out, exist_ok=True)
def hx(h, a=255): return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)
W, H = 96, 69
rnd = random.Random(7)

# ---------------- 도구: 고해상도 오브젝트 → 도트 ----------------
class Obj:
    """w x h(최종 픽셀) 오브젝트. S배 크기로 그리고 줄인다."""
    def __init__(self, w, h, S=8, k=1.0): self.w, self.h, self.S, self.k = w, h, S, k; self.im = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    def mask(self, fn):
        m = Image.new('L', self.im.size, 0); fn(ImageDraw.Draw(m), self.S * self.k); return m
    def fill(self, mask, c_light, c_dark, angle=(0.35, 0.65)):
        """마스크 영역을 왼쪽 위가 밝고 오른쪽 아래가 어두운 그라데이션으로 칠한다."""
        a = np.asarray(mask) > 0
        if not a.any(): return
        ys, xs = np.nonzero(a); x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        yy, xx = np.mgrid[y0:y1, x0:x1]
        t = ((xx - x0) / max(1, x1 - x0) * angle[0] + (yy - y0) / max(1, y1 - y0) * angle[1]); t = np.clip(t / (angle[0] + angle[1]), 0, 1)[..., None]
        col = np.array(c_light[:3]) * (1 - t) + np.array(c_dark[:3]) * t
        layer = np.zeros((y1 - y0, x1 - x0, 4), np.uint8); layer[..., :3] = col.astype(np.uint8); layer[..., 3] = a[y0:y1, x0:x1] * 255
        self.im.alpha_composite(Image.fromarray(layer, 'RGBA'), (int(x0), int(y0)))
    def flat(self, mask, c):
        layer = Image.new('RGBA', self.im.size, c); self.im.paste(layer, (0, 0), mask)
    def done(self, colors=14, outline=None):
        a = np.asarray(self.im).astype(float); S = self.S; h, w = self.h, self.w
        al = a[..., 3] / 255.0
        al_s = al.reshape(h, S, w, S).mean((1, 3))
        rgb = (a[..., :3] * al[..., None]).reshape(h, S, w, S, 3).sum((1, 3)) / np.maximum(al.reshape(h, S, w, S).sum((1, 3)), 1e-6)[..., None]
        opaque = al_s > 0.5
        im = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), 'RGB')
        bgfill = Image.new('RGB', im.size, (255, 0, 255)); bgfill.paste(im, (0, 0), Image.fromarray((opaque * 255).astype(np.uint8)))
        q = bgfill.quantize(colors=colors + 1, method=Image.MEDIANCUT, dither=Image.NONE).convert('RGB')
        res = Image.new('RGBA', im.size, (0, 0, 0, 0)); res.paste(q, (0, 0), Image.fromarray((opaque * 255).astype(np.uint8)))
        if outline:
            ar = np.asarray(res)[..., 3] > 0; pad = np.pad(ar, 1); ring = np.zeros_like(ar)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)): ring |= np.roll(np.roll(pad, dy, 0), dx, 1)[1:-1, 1:-1]
            ring &= ~ar; o = np.asarray(res).copy(); o[ring] = outline; res = Image.fromarray(o, 'RGBA')
        return res

def hash2(x, y, s=0):
    n = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xffffffff; n = (n ^ (n >> 13)) * 1274126177 & 0xffffffff; return ((n ^ (n >> 16)) & 0xffff) / 65535.0

img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); px = img.load()
DIA = [(48, 26), (95, 45), (48, 64), (1, 45)]
dmask = Image.new('L', (W, H), 0); ImageDraw.Draw(dmask).polygon(DIA, fill=255); dm = dmask.load()

# ---------------- 1) 바닥: 흙 두께 + 줄무늬 잔디 ----------------
SOIL_L, SOIL_R, SOIL_D = hx('94693a'), hx('6e4a28'), hx('4a3018')
d = ImageDraw.Draw(img)
d.polygon([(1, 45), (48, 64), (48, 68), (1, 49)], fill=SOIL_L); d.polygon([(48, 64), (95, 45), (95, 49), (48, 68)], fill=SOIL_R)
d.line([(1, 49), (48, 68), (95, 49)], fill=SOIL_D)
for y in range(H):
    for x in range(W):
        if dm[x, y] > 0:
            stripe = ((x + 2 * y) // 7) % 2                      # 아이소 잔디 깎은 줄무늬
            base = [hx('9cc25c'), hx('8ab34e')][stripe]
            r = hash2(x, y)
            if r < 0.10: base = hx('76a03e')
            elif r > 0.95: base = hx('b4d870')
            px[x, y] = base
d.line(DIA + [DIA[0]], fill=hx('5f8a2c'))
for i in range(14):                                          # 들꽃
    x, y = rnd.randint(6, 90), rnd.randint(30, 62)
    if dm[x, y] > 0 and abs(x - 44) > 30: px[x, y] = hx('fff0a0') if i % 2 else hx('ffffff')

# ---------------- 2) 모래 마장 ----------------
cx, cy, rx, ry = 44, 48, 27, 12.5
yy0 = int(cy - ry - 3)
for y in range(H):
    for x in range(W):
        e = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
        if e <= 1.0 and dm[x, y] > 0:
            ring = int(math.sqrt(e) * 7)
            base = [hx('e6d29c'), hx('dcc78c')][ring % 2]            # 갈퀴 자국(동심 타원 줄무늬)
            if e > 0.80: base = hx('c9b176')                          # 바깥쪽 다져진 가장자리
            r = hash2(x, y, 3)
            if r < 0.07: base = hx('c2a96c')
            elif r > 0.94: base = hx('f2e3b4')
            px[x, y] = base
# 말발굽 자국
for (x, y) in [(33, 55), (36, 56), (39, 56), (50, 57), (53, 56), (56, 54), (30, 52)]: px[x, y] = hx('b49a5c')

def fence(front):
    """이중 가로대 나무 울타리. front=False 는 뒤쪽 반(말 뒤), True 는 앞쪽 반."""
    pts = []
    for k in range(0, 360, 6):
        a = math.radians(k); sy = math.sin(a)
        if (sy >= 0) == front: pts.append((cx + (rx + 2.2) * math.cos(a), cy + (ry + 1.1) * sy, k))
    for (x, y, k) in pts:
        X, Y = round(x), round(y)
        if not (0 <= X < W and 0 <= Y < H): continue
        for z, col in ((2, hx('d8a05c')), (5, hx('e8b878'))):                   # 가로대 두 줄
            if 0 <= Y - z < H: px[X, Y - z] = col
    for (x, y, k) in pts:
        if k % 18: continue
        X, Y = round(x), round(y)
        for z in range(0, 7):
            if 0 <= Y - z < H and 0 <= X < W: px[X, Y - z] = hx('a8703a') if z < 6 else hx('f4d29a')
        if 0 <= X - 1 < W and 0 <= Y - 3 < H: px[X - 1, Y - 3] = hx('4a2c14') if front else px[X - 1, Y - 3]
fence(False)

# ---------------- 3) 장애물(빨강·흰 막대) ----------------
def jump(x, y):
    for dx in (0, 9):
        for z in range(0, 7): px[x + dx, y - z] = hx('f0f0f0') if z % 2 else hx('c83030')
    for i in range(9):
        c = hx('f4f4f4') if (i // 2) % 2 == 0 else hx('d03030')
        px[x + i, y - 5 + i // 5 * 0] = c
        px[x + i, y - 4] = hx('a02020') if (i // 2) % 2 else hx('c0c0c0')
    for dx in (0, 9): px[x + dx, y + 1] = hx('8a6a3a')
jump(52, 51); jump(24, 49)

# ---------------- 4) 마구간(오른쪽 뒤) ----------------
FX, FY, P, Q, WH = 71, 43, 15, 11, 9
def P3(p, q, z): return (FX + p - q, FY - (p + q) // 2 - z)
d = ImageDraw.Draw(img)
WALL_L, WALL_R, TRIM = hx('efe4c8'), hx('c9bb98'), hx('6a4426')
d.polygon([P3(0, 0, 0), P3(0, Q, 0), P3(0, Q, WH), P3(0, 0, WH)], fill=WALL_L)           # 왼면(밝음)
d.polygon([P3(0, 0, 0), P3(P, 0, 0), P3(P, 0, WH), P3(0, 0, WH)], fill=WALL_R)           # 오른면(그늘)
# 널판 결
for p in range(1, P, 2):
    for z in range(0, WH):
        x, y = P3(p, 0, z); px[x, y] = hx('b9ab88')
for q in range(1, Q, 2):
    for z in range(0, WH):
        x, y = P3(0, q, z); px[x, y] = hx('ddd0b0')
# 박공 지붕: 용마루 높이 WH+5
RZ = WH + 5
ROOF_A, ROOF_B, ROOF_C, ROOF_D = hx('d04a3c'), hx('b23a30'), hx('8e2a24'), hx('6a1e1a')
roofL = [P3(-1, -1, WH), P3(P + 1, -1, WH), P3(P + 1, Q // 2, RZ), P3(-1, Q // 2, RZ)]          # 앞(오른쪽 면 쪽) 경사
roofR = [P3(-1, Q + 1, WH), P3(P + 1, Q + 1, WH), P3(P + 1, Q // 2, RZ), P3(-1, Q // 2, RZ)]
d.polygon(roofL, fill=ROOF_B); d.polygon(roofR, fill=ROOF_C)
# 기와 줄(경사를 따라 층층이)
for i in range(1, 5):
    z0 = WH + i; q0 = i
    a, b = P3(-1, q0 - 1 + 0, z0 - 0), P3(P + 1, q0 - 1 + 0, z0 - 0)
    d.line([a, b], fill=ROOF_D)
for i in range(1, 5):
    a, b = P3(-1, Q - i + 1, WH + i - 1), P3(P + 1, Q - i + 1, WH + i - 1)
    d.line([a, b], fill=hx('5a1814'))
d.line([P3(-1, Q // 2, RZ), P3(P + 1, Q // 2, RZ)], fill=hx('f09a82'))                         # 용마루 하이라이트
d.line([P3(-1, -1, WH), P3(P + 1, -1, WH)], fill=hx('f09a82'))                                  # 처마 끝 밝은 선
d.line([P3(0, 0, 0), P3(0, 0, WH)], fill=TRIM); d.line([P3(P, 0, 0), P3(P, 0, WH)], fill=TRIM)
d.line([P3(0, Q, 0), P3(0, Q, WH)], fill=TRIM)
# 문(반쪽문 + 말 얼굴)과 창
for p0 in (3, 9):
    for dp in range(3):
        for z in range(0, 6):
            x, y = P3(p0 + dp, 0, z); px[x, y] = hx('3a281a') if z > 2 else hx('5a3a1e')
    x, y = P3(p0 + 1, 0, 4); px[x, y] = hx('a8622c'); px[x + 1, y] = hx('7a4620'); px[x, y - 1] = hx('a8622c')   # 말 얼굴
    for z in (0, 6):
        for dp in range(-1, 4): x, y = P3(p0 + dp, 0, z); px[x, y] = TRIM
for q0 in (3,):
    for dq in range(3):
        for z in range(3, 7): x, y = P3(0, q0 + dq, z); px[x, y] = hx('7fa4c8') if (dq + z) % 2 else hx('a8c8e0')
    for dq in range(-1, 4): x, y = P3(0, q0 + dq, 7); px[x, y] = TRIM; x, y = P3(0, q0 + dq, 2); px[x, y] = TRIM
# 건초 더미 + 풍향계
for (hx0, hy0) in ((FX + 3, FY + 5), (FX + 8, FY + 3)):
    for dy in range(3):
        for dx in range(5): px[hx0 + dx, hy0 + dy] = hx('e8c24a') if (dx + dy) % 2 else hx('f4d768')
    for dx in range(5): px[hx0 + dx, hy0 + 3] = hx('a8861c')
wx, wy = P3(P // 2, Q // 2, RZ)
for z in range(0, 4): px[wx, wy - z] = hx('3a3a3a')
px[wx - 1, wy - 3] = hx('d8d8d8'); px[wx + 1, wy - 3] = hx('d8d8d8'); px[wx, wy - 4] = hx('e84b3a')
# 건물 그림자
sh = Image.new('RGBA', (W, H), (0, 0, 0, 0)); ImageDraw.Draw(sh).polygon([P3(0, 0, 0), P3(P, 0, 0), (P3(P, 0, 0)[0] + 6, P3(P, 0, 0)[1] + 4), (P3(0, 0, 0)[0] + 3, P3(0, 0, 0)[1] + 4)], fill=(30, 40, 20, 55))
sh.putalpha(Image.fromarray((np.minimum(np.asarray(sh.getchannel('A')), np.asarray(dmask)))))
img.alpha_composite(sh); px = img.load()

# ---------------- 5) 나무 2그루(왼쪽 뒤) ----------------
def tree(w=18, h=24):
    o = Obj(w, h, 8)
    trunk = o.mask(lambda d, S: d.polygon([(8.2 * S, 14 * S), (9.8 * S, 14 * S), (10.4 * S, 23 * S), (7.6 * S, 23 * S)], fill=255))
    o.fill(trunk, hx('8a5a2c'), hx('4a2c14'))
    for (x, y, r, cl, cd) in ((9, 8, 7.2, hx('7fcf5a'), hx('2a7a2c')), (5.6, 11, 4.6, hx('62b84a'), hx('226a28')), (12.8, 11.2, 4.8, hx('62b84a'), hx('1f6226'))):
        m = o.mask(lambda d, S: d.ellipse([(x - r) * S, (y - r * 0.9) * S, (x + r) * S, (y + r * 0.9) * S], fill=255)); o.fill(m, cl, cd)
    hl = o.mask(lambda d, S: d.ellipse([5 * S, 3.5 * S, 10 * S, 7 * S], fill=255)); o.flat(hl, hx('b4f08a', 140))
    return o.done(colors=9, outline=hx('153a1a'))
t1 = tree(); t2 = tree(16, 22)
img.alpha_composite(t1, (6, 17)); img.alpha_composite(t2, (22, 14)); px = img.load()
ts = Image.new('RGBA', (W, H), (0, 0, 0, 0)); ImageDraw.Draw(ts).ellipse([9, 38, 27, 44], fill=(30, 50, 20, 70)); ImageDraw.Draw(ts).ellipse([24, 33, 38, 38], fill=(30, 50, 20, 60))
base = img.copy(); img = Image.alpha_composite(ts, Image.new('RGBA', (W, H), (0, 0, 0, 0))); img = Image.alpha_composite(base, Image.new('RGBA', (W, H), (0, 0, 0, 0))); px = img.load()

# ---------------- 6) 말과 기수 ----------------
def horse_rider(w=30, h=26):
    o = Obj(w, h, 10, 0.86)
    S = o.S
    CH_L, CH_D = hx('d8883e'), hx('7a3e18')          # 밤색 말
    def poly(pts, cl, cd, ang=(0.35, 0.65)):
        m = o.mask(lambda d, s: d.polygon([(x * s, y * s) for x, y in pts], fill=255)); o.fill(m, cl, cd, ang); return m
    def ell(cx_, cy_, rx_, ry_, cl, cd):
        m = o.mask(lambda d, s: d.ellipse([(cx_ - rx_) * s, (cy_ - ry_) * s, (cx_ + rx_) * s, (cy_ + ry_) * s], fill=255)); o.fill(m, cl, cd); return m
    # 꼬리(뒤)
    poly([(6.5, 13.2), (3.2, 15), (2.2, 21), (4.0, 22.4), (4.8, 17.5), (7.2, 15.5)], hx('4a2410'), hx('1e0e06'))
    # 뒷다리
    poly([(7.8, 17), (10.2, 17), (10.0, 22), (10.6, 26.4), (8.8, 26.4), (8.0, 22.4)], hx('c27634'), hx('6a3414'))
    poly([(8.8, 26.4), (10.8, 26.4), (10.8, 28.2), (8.6, 28.2)], hx('f4ead4'), hx('c8bca0'))    # 흰 양말
    poly([(5.8, 17), (8.0, 17), (7.6, 22), (8.0, 26.4), (6.4, 26.4), (6.0, 22.2)], hx('b06a2e'), hx('5a2c10'))
    poly([(6.2, 26.4), (8.2, 26.4), (8.2, 28.2), (6.0, 28.2)], hx('2a1608'), hx('140a04'))
    # 몸통
    ell(13.4, 15.2, 8.2, 4.6, CH_L, CH_D)
    # 앞다리
    poly([(17.2, 17), (19.6, 17), (19.4, 22), (20.6, 26.4), (18.8, 26.4), (17.4, 22.4)], hx('c27634'), hx('6a3414'))
    poly([(18.6, 26.4), (20.8, 26.4), (20.8, 28.2), (18.6, 28.2)], hx('f4ead4'), hx('c8bca0'))
    poly([(14.6, 17), (17.0, 17), (16.0, 22), (14.8, 24.6), (13.6, 24.2), (14.4, 21.8)], hx('b06a2e'), hx('5a2c10'))
    poly([(13.4, 24.2), (15.2, 24.6), (15.0, 26.4), (13.2, 26.0)], hx('2a1608'), hx('140a04'))
    # 목·머리
    poly([(17.4, 11.6), (20.8, 5.6), (23.2, 6.4), (21.6, 13.2), (18.6, 14.6)], hx('e09448'), hx('8a4a1e'))
    poly([(21.0, 5.2), (26.4, 7.8), (27.6, 11.2), (25.8, 12.4), (22.6, 10.2), (20.6, 7.8)], hx('e69a4e'), hx('8e4c20'))
    poly([(24.4, 8.0), (27.2, 9.6), (27.6, 11.2), (25.8, 11.8)], hx('f4ead4'), hx('d8cdb0'))      # 흰 코 줄무늬
    poly([(20.4, 3.6), (21.8, 5.4), (20.0, 5.9)], hx('b8682c'), hx('5a2c10'))                      # 귀
    # 갈기
    poly([(17.0, 9.4), (20.2, 4.2), (21.0, 5.6), (18.0, 12.0)], hx('4a2410'), hx('1e0e06'))
    # 안장·굴레
    poly([(9.4, 11.6), (14.8, 11.6), (14.2, 14.4), (9.8, 14.4)], hx('d83a3a'), hx('7a1616'))
    o.flat(o.mask(lambda d, s: d.line([(20.6 * s, 7.4 * s), (26.0 * s, 9.4 * s)], fill=255, width=int(0.5 * s))), hx('3a2410'))
    o.flat(o.mask(lambda d, s: d.ellipse([22.6 * s, 7.6 * s, 23.4 * s, 8.4 * s], fill=255)), hx('141008'))   # 눈
    # 기수: 다리(흰 바지)+부츠, 몸(빨간 재킷), 팔, 머리, 헬멧
    poly([(11.0, 13.4), (13.4, 13.4), (13.4, 19.6), (12.0, 19.6)], hx('fafafa'), hx('b8bcc8'))
    poly([(11.6, 19.4), (13.6, 19.4), (13.8, 23.0), (12.0, 23.0)], hx('2a2a38'), hx('0e0e16'))
    poly([(9.6, 6.6), (13.4, 6.6), (14.0, 13.6), (9.8, 13.6)], hx('f05a4a'), hx('a22420'))
    poly([(13.0, 7.8), (17.6, 10.6), (17.2, 11.8), (12.6, 9.4)], hx('e24a3a'), hx('9a2018'))   # 고삐 잡은 팔
    ell(11.8, 4.4, 2.2, 2.2, hx('ffd8b0'), hx('d89868'))
    poly([(9.4, 4.0), (10.0, 2.0), (12.0, 1.4), (14.0, 2.4), (14.2, 4.0), (9.4, 4.0)], hx('3a3a52'), hx('0e0e1c'))
    poly([(13.6, 3.6), (15.6, 3.8), (15.4, 4.4), (13.6, 4.2)], hx('2a2a40'), hx('0e0e1c'))      # 헬멧 챙
    return o.done(colors=22, outline=hx('24140a'))
hr = horse_rider()
shd = Image.new('RGBA', (W, H), (0, 0, 0, 0)); ImageDraw.Draw(shd).ellipse([34, 54, 55, 59], fill=(60, 50, 20, 85))
img.alpha_composite(shd); img.alpha_composite(hr, (31, 33)); px = img.load()

fence(True)

# ---------------- 7) 깃발·급수통 ----------------
d = ImageDraw.Draw(img)
for y in range(36, 53): px[10, y] = hx('9a9a9a') if y % 2 else hx('c8c8c8')
for y in range(37, 44):
    for x in range(11, 19):
        px[x, y] = hx('e84b8a') if y < 40 else (hx('fff4d0') if y < 42 else hx('3aa0e6'))
px[10, 36] = hx('f8d84a')

# ---------------- 8) 전체 팔레트 정리 ----------------
a = np.asarray(img).copy(); op = a[..., 3] > 0
rgb = Image.fromarray(a[..., :3], 'RGB'); m = Image.new('RGB', rgb.size, (255, 0, 255)); m.paste(rgb, (0, 0), Image.fromarray((op * 255).astype(np.uint8)))
q = m.quantize(colors=56, method=Image.MEDIANCUT, dither=Image.NONE).convert('RGB')
fin = Image.new('RGBA', (W, H), (0, 0, 0, 0)); fin.paste(q, (0, 0), Image.fromarray((op * 255).astype(np.uint8)))
fin.save(out + '/b9.png')
if len(sys.argv) > 1:
    bg = Image.new('RGBA', fin.size, (150, 70, 160, 255)); bg.alpha_composite(fin); bg.resize((W * 8, H * 8), Image.NEAREST).save(sys.argv[1])
print('승마장 → b9.png')
