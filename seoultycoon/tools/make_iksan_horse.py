"""카트라이더파크(b9, 96x69) 자리를 '익산공공승마장'으로 새로 그린다 → work/pak_mod/b9.png (tools/st2_pak_build.py 가 data1.pak 에 묶음).
고품질 도트: 8배 해상도에서 광원(왼쪽 위)·부드러운 그림자·재질 질감(잔디 한 올, 모래 갈퀴, 널판, 기와, 나뭇잎 덩이)을 그린 뒤
면적 평균으로 96x69 로 줄인다(색을 제한하지 않아 계조가 부드럽고, 윤곽만 이진 투명). 크기·아이소 바닥 다이아몬드는 원본과 같다.
사용: python3 -I tools/make_iksan_horse.py [미리보기.png]"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = root + '/work/pak_mod'; os.makedirs(out, exist_ok=True)
S, W, H = 8, 96, 69
WS, HS = W * S, H * S
rnd = random.Random(11); nrs = np.random.RandomState(3)
def hx(h, a=255): return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)
def lerp(c0, c1, t): return tuple(int(c0[i] * (1 - t) + c1[i] * t) for i in range(3)) + (255,)

# 아이소 바닥(다이아몬드): 지면 좌표 (a: 오른쪽 아래로, b: 왼쪽 아래로), z: 위로(1x 픽셀)
TT, RR, LL = (48, 26), (95, 45), (1, 45)
def G(a, b, z=0.0):
    return ((TT[0] + a * (RR[0] - TT[0]) + b * (LL[0] - TT[0])) * S, (TT[1] + a * (RR[1] - TT[1]) + b * (LL[1] - TT[1]) - z) * S)
def newlayer(): return Image.new('RGBA', (WS, HS), (0, 0, 0, 0))
canvas = newlayer()

def gradfill(img, pts, c0, c1, d=(0, 1), noise=0.0, alpha=1.0):
    """다각형을 방향 d 로 c0→c1 그라데이션(+잡음)으로 칠한다."""
    m = Image.new('L', img.size, 0); ImageDraw.Draw(m).polygon(pts, fill=255)
    bb = m.getbbox()
    if not bb: return
    x0, y0, x1, y1 = bb; ys, xs = np.mgrid[y0:y1, x0:x1]
    proj = xs * d[0] + ys * d[1]; lo, hi = proj.min(), proj.max(); t = ((proj - lo) / max(1e-6, hi - lo))[..., None]
    col = np.array(c0[:3]) * (1 - t) + np.array(c1[:3]) * t
    if noise: col = col + nrs.normal(0, noise, col.shape[:2])[..., None]
    arr = np.zeros((y1 - y0, x1 - x0, 4), np.uint8); arr[..., :3] = np.clip(col, 0, 255).astype(np.uint8); arr[..., 3] = (np.asarray(m.crop(bb)) * alpha).astype(np.uint8)
    img.alpha_composite(Image.fromarray(arr, 'RGBA'), (x0, y0))

def shadow(pts, strength=110, blur=7):
    sh = Image.new('L', (WS, HS), 0); ImageDraw.Draw(sh).polygon(pts, fill=strength); sh = sh.filter(ImageFilter.GaussianBlur(blur))
    clipm = Image.new('L', (WS, HS), 0); ImageDraw.Draw(clipm).polygon([G(0, 0), G(1, 0), G(1, 1), G(0, 1)], fill=255)
    sh = Image.fromarray(np.minimum(np.asarray(sh), np.asarray(clipm)))
    lay = Image.new('RGBA', (WS, HS), (22, 36, 20, 255)); lay.putalpha(sh); canvas.alpha_composite(lay)

# ===================== 1) 바닥: 흙 두께 + 줄무늬 잔디 =====================
SOIL = 3.2
gradfill(canvas, [G(0, 1), G(1, 1), G(1, 1, -SOIL), G(0, 1, -SOIL)], hx('a07040'), hx('6a4426'), (0, 1), 6)     # 왼쪽 앞 옆면
gradfill(canvas, [G(1, 0), G(1, 1), G(1, 1, -SOIL), G(1, 0, -SOIL)], hx('7a5030'), hx('4a2e18'), (0, 1), 6)     # 오른쪽 앞 옆면
gradfill(canvas, [G(0, 0), G(1, 0), G(1, 1), G(0, 1)], hx('a6cf68'), hx('7fae48'), (0.5, 1.0), 4)
# 줄무늬(깎은 잔디) + 잔디 결
yy, xx = np.mgrid[0:HS, 0:WS]
bcoord = ((xx / S) - TT[0]) / -47.0 * 0.5 + ((yy / S) - TT[1]) / 19.0 * 0.5        # 지면 b 좌표(대략)
acoord = ((yy / S) - TT[1]) / 19.0 * 0.5 + ((xx / S) - TT[0]) / 47.0 * 0.5
dm = Image.new('L', (WS, HS), 0); ImageDraw.Draw(dm).polygon([G(0, 0), G(1, 0), G(1, 1), G(0, 1)], fill=255); dmk = np.asarray(dm) > 0
stripe = (np.sin(bcoord * 2 * math.pi * 5.0) * 0.5 + 0.5)
cv = np.asarray(canvas).astype(float)
cv[..., :3] *= (0.93 + 0.12 * stripe)[..., None]
canvas = Image.fromarray(np.clip(cv, 0, 255).astype(np.uint8), 'RGBA')
d = ImageDraw.Draw(canvas)
for i in range(2600):                                                                   # 잔디 한 올
    a, b = rnd.random(), rnd.random(); x, y = G(a, b); L_ = rnd.randint(5, 11)
    col = rnd.choice([hx('c4ec7c', 160), hx('5f9a30', 150), hx('8fc455', 140), hx('4c8a28', 120)])
    d.line([(x, y), (x + rnd.randint(-3, 3), y - L_)], fill=col, width=2)
for i in range(34):                                                                     # 들꽃
    a, b = rnd.random(), rnd.random(); x, y = G(a, b); r = rnd.randint(3, 5)
    d.ellipse([x - r, y - r, x + r, y + r], fill=rnd.choice([hx('ffffff'), hx('fff0a0'), hx('ffc0d8')])); d.ellipse([x - 1.5, y - 1.5, x + 1.5, y + 1.5], fill=hx('e8a020'))
# 다이아몬드 밖은 비운다
cv = np.asarray(canvas).copy(); cv[~dmk & (np.asarray(canvas)[..., 3] > 0) & (yy < G(0.5, 1)[1] - 1)] = 0
# (옆면은 위 조건 때문에 남는다: 윗부분만 마스크)
sidemask = Image.new('L', (WS, HS), 0); ImageDraw.Draw(sidemask).polygon([G(0, 1), G(1, 1), G(1, 1, -SOIL), G(0, 1, -SOIL)], fill=255); ImageDraw.Draw(sidemask).polygon([G(1, 0), G(1, 1), G(1, 1, -SOIL), G(1, 0, -SOIL)], fill=255)
canvas = Image.fromarray(cv, 'RGBA'); d = ImageDraw.Draw(canvas)
# 옆면 재배치(잔디가 덮지 않게 다시 칠함)
gradfill(canvas, [G(0, 1), G(1, 1), G(1, 1, -SOIL), G(0, 1, -SOIL)], hx('a07040'), hx('6a4426'), (0, 1), 6)
gradfill(canvas, [G(1, 0), G(1, 1), G(1, 1, -SOIL), G(1, 0, -SOIL)], hx('7a5030'), hx('4a2e18'), (0, 1), 6)
d.line([G(0, 1), G(1, 1), G(1, 0)], fill=hx('c8e58e', 200), width=3)               # 앞쪽 모서리 풀 하이라이트
d.line([G(0, 1, 0.3), G(1, 1, 0.3)], fill=hx('5a8c2c', 160), width=2)

# ===================== 2) 모래 마장 =====================
AC, BC, RA = 0.50, 0.56, 0.335
def ring(r, n=96, c=(AC, BC), sq=1.0): return [G(c[0] + r * sq * math.cos(2 * math.pi * k / n), c[1] + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
shadow(ring(RA + 0.02), 70, 8)
gradfill(canvas, ring(RA + 0.028), hx('9a7a48'), hx('6a5030'), (0, 1))                  # 마장 둘레 흙 턱
gradfill(canvas, ring(RA), hx('ecd9a4'), hx('d2bb82'), (0.4, 1.0), 6)
for k, r in enumerate([0.31, 0.27, 0.23, 0.19, 0.15, 0.11]):                           # 갈퀴 자국(동심 타원)
    pts = ring(r); dd = ImageDraw.Draw(canvas); dd.line(pts + [pts[0]], fill=hx('c8b078', 120) if k % 2 else hx('f6e6b8', 110), width=3)
d = ImageDraw.Draw(canvas)
for i in range(420):                                                                     # 모래알
    ang = rnd.random() * 2 * math.pi; r = RA * math.sqrt(rnd.random()) * 0.97; x, y = G(AC + r * math.cos(ang), BC + r * math.sin(ang))
    d.ellipse([x - 1.5, y - 1, x + 1.5, y + 1], fill=rnd.choice([hx('b9a26a', 150), hx('fff2c8', 150), hx('a08a58', 120)]))
for (a, b) in [(0.36, 0.64), (0.40, 0.67), (0.44, 0.69), (0.57, 0.70), (0.61, 0.66), (0.35, 0.50), (0.66, 0.50)]:   # 말발굽 자국
    x, y = G(a, b); d.arc([x - 7, y - 4, x + 7, y + 4], 20, 200, fill=hx('a48e58', 190), width=3)

# ===================== 3) 그림자(오브젝트 아래) =====================
def hxs(a, b, w, h): return [G(a, b), G(a + w, b), G(a + w, b + h), G(a, b + h)]
# ===================== 4) 오브젝트들 (뒤→앞 순서) =====================
objs = []
def fence(front):
    def draw():
        posts = []
        n = 28
        for k in range(n):
            ang = 2 * math.pi * k / n; fy = math.sin(ang) + 0.0                       # 화면상 앞쪽 = 지면 b+a 가 큰 쪽
            a, b = AC + (RA + 0.03) * math.cos(ang), BC + (RA + 0.03) * math.sin(ang)
            isfront = (a + b) > (AC + BC)
            if isfront == front: posts.append((k, a, b))
        d2 = ImageDraw.Draw(canvas)
        for z, c0 in ((2.4, hx('f2c486')), (5.6, hx('f8d6a0'))):                      # 가로대(두 줄)
            for (k, a, b), (k2, a2, b2) in zip(posts, posts[1:]):
                if k2 - k == 1:
                    p1, p2 = G(a, b, z), G(a2, b2, z); d2.line([p1, p2], fill=hx('7a4c24'), width=7); d2.line([(p1[0], p1[1] - 1.5), (p2[0], p2[1] - 1.5)], fill=c0, width=4)
        for (k, a, b) in posts:
            x, y = G(a, b); top = G(a, b, 7.2)
            d2.polygon([(x - 4, y), (x + 4, y), (top[0] + 4, top[1]), (top[0] - 4, top[1])], fill=hx('a8703a'))
            d2.polygon([(x - 4, y), (x - 0.5, y), (top[0] - 0.5, top[1]), (top[0] - 4, top[1])], fill=hx('d0985a'))
            d2.ellipse([top[0] - 4.5, top[1] - 2.5, top[0] + 4.5, top[1] + 2.5], fill=hx('e6bd82'))
            sh = Image.new('L', (WS, HS), 0); ImageDraw.Draw(sh).ellipse([x + 1, y - 2, x + 18, y + 3], fill=70); sh = sh.filter(ImageFilter.GaussianBlur(2))
            lay = Image.new('RGBA', (WS, HS), (22, 36, 20, 255)); lay.putalpha(sh); canvas.alpha_composite(lay)
    return draw
objs.append((AC + BC - 0.9, fence(False)))

def jump(a, b, along):
    def draw():
        da, db = (0.055, -0.055) if along else (0.055, 0.055)
        d2 = ImageDraw.Draw(canvas)
        for sgn in (-1, 1):
            x, y = G(a + sgn * da, b + sgn * db); top = G(a + sgn * da, b + sgn * db, 8)
            d2.polygon([(x - 4, y), (x + 4, y), (top[0] + 4, top[1]), (top[0] - 4, top[1])], fill=hx('f4f4f4')); d2.polygon([(x - 4, y), (x - 0.5, y), (top[0] - 0.5, top[1]), (top[0] - 4, top[1])], fill=hx('ffffff'))
            d2.ellipse([x - 5, y - 2, x + 5, y + 2], fill=hx('6a5030'))
        for z, k in ((2.2, 0), (4.4, 1), (6.6, 0)):
            p1, p2 = G(a - da, b - db, z), G(a + da, b + db, z)
            n = 7
            for i in range(n):
                s0 = (p1[0] + (p2[0] - p1[0]) * i / n, p1[1] + (p2[1] - p1[1]) * i / n); s1 = (p1[0] + (p2[0] - p1[0]) * (i + 1) / n, p1[1] + (p2[1] - p1[1]) * (i + 1) / n)
                d2.line([s0, s1], fill=hx('d83030') if (i + k) % 2 else hx('fafafa'), width=6); d2.line([(s0[0], s0[1] - 2), (s1[0], s1[1] - 2)], fill=hx('ffffff', 160), width=2)
    return draw
objs.append((0.46 + 0.62, jump(0.30, 0.62, True))); objs.append((0.40 + 0.40, jump(0.66, 0.44, False)))

def barn():
    a0, a1, b0, b1, Hw, Hr = 0.60, 0.92, 0.03, 0.22, 10.5, 17.0
    def draw():
        shadow([G(a0, b1), G(a1, b1), G(a1 + 0.12, b1 + 0.06), G(a0 + 0.1, b1 + 0.08)], 95, 8)
        d2 = ImageDraw.Draw(canvas)
        # 정면(b=b1 면) 널판 벽
        gradfill(canvas, [G(a0, b1), G(a1, b1), G(a1, b1, Hw), G(a0, b1, Hw)], hx('f7ecd0'), hx('d9c9a2'), (0.3, 1), 4)
        for k in range(1, 22):
            a = a0 + (a1 - a0) * k / 22; d2.line([G(a, b1, 0), G(a, b1, Hw)], fill=hx('b8a47a', 140), width=2)
        gradfill(canvas, [G(a1, b0), G(a1, b1), G(a1, b1, Hw), G(a1, b0, Hw)], hx('c5b58c'), hx('9c8c68'), (0.3, 1), 4)         # 오른쪽 면(그늘)
        for k in range(1, 9):
            b = b0 + (b1 - b0) * k / 9; d2.line([G(a1, b, 0), G(a1, b, Hw)], fill=hx('8a7a56', 140), width=2)
        gradfill(canvas, [G(a1, b0, Hw), G(a1, b1, Hw), G(a1, (b0 + b1) / 2, Hr)], hx('c5b58c'), hx('a89868'), (0, 1))            # 박공 삼각형
        for p, q in (((a0, b1), (a0, b1)),):
            pass
        for (aa, bb) in ((a0, b1), (a1, b1), (a1, b0)):                                                                              # 모서리 기둥
            d2.line([G(aa, bb, 0), G(aa, bb, Hw)], fill=hx('7a5a34'), width=6); d2.line([G(aa, bb, 0), G(aa, bb, Hw)], fill=hx('b08850'), width=2)
        # 문(반쪽문) + 말 얼굴
        for da in (0.10, 0.34):
            x0, x1 = a0 + da * (a1 - a0), a0 + (da + 0.18) * (a1 - a0)
            gradfill(canvas, [G(x0, b1, 0), G(x1, b1, 0), G(x1, b1, 7.6), G(x0, b1, 7.6)], hx('6e4a2a'), hx('3a2414'), (0, 1))
            gradfill(canvas, [G(x0, b1, 0.6), G(x1, b1, 0.6), G(x1, b1, 3.6), G(x0, b1, 3.6)], hx('7a5434'), hx('4a2e18'), (0, 1))
            for z in (0, 7.6): d2.line([G(x0 - 0.01, b1, z), G(x1 + 0.01, b1, z)], fill=hx('5a3a1c'), width=5)
            hxm, hym = G((x0 + x1) / 2, b1, 5.6); d2.ellipse([hxm - 11, hym - 8, hxm + 11, hym + 10], fill=hx('a8622c')); d2.ellipse([hxm - 11, hym - 8, hxm, hym + 4], fill=hx('c98342'))
            d2.polygon([(hxm - 5, hym - 8), (hxm - 8, hym - 18), (hxm - 1, hym - 9)], fill=hx('8a4a1e')); d2.polygon([(hxm + 3, hym - 8), (hxm + 6, hym - 18), (hxm + 9, hym - 6)], fill=hx('8a4a1e'))
            d2.ellipse([hxm - 5, hym - 2, hxm - 2, hym + 1], fill=hx('141008')); d2.ellipse([hxm + 3, hym - 2, hxm + 6, hym + 1], fill=hx('141008')); d2.ellipse([hxm - 3, hym + 3, hxm + 3, hym + 9], fill=hx('e8c8a8'))
        # 창(오른쪽 면): 유리 반사
        gradfill(canvas, [G(a1, b0 + 0.04, 4.0), G(a1, b0 + 0.13, 4.0), G(a1, b0 + 0.13, 8.0), G(a1, b0 + 0.04, 8.0)], hx('b8dcf4'), hx('5e8cb8'), (1, 1))
        d2.line([G(a1, b0 + 0.085, 4.0), G(a1, b0 + 0.085, 8.0)], fill=hx('5a3a1c'), width=3); d2.line([G(a1, b0 + 0.04, 6.0), G(a1, b0 + 0.13, 6.0)], fill=hx('5a3a1c'), width=3)
        # 지붕: 앞 경사(+b)
        bm = (b0 + b1) / 2; ov = 0.025
        gradfill(canvas, [G(a0 - ov, b1 + ov, Hw - 0.5), G(a1 + ov, b1 + ov, Hw - 0.5), G(a1 + ov, bm, Hr), G(a0 - ov, bm, Hr)], hx('e05a48'), hx('a83428'), (0.2, 1), 5)
        rows = 8
        for k in range(1, rows):                                                              # 기와 줄과 개별 기와 틈
            t = k / rows; bb = (b1 + ov) + (bm - (b1 + ov)) * t; z = (Hw - 0.5) + (Hr - (Hw - 0.5)) * t
            d2.line([G(a0 - ov, bb, z), G(a1 + ov, bb, z)], fill=hx('7a1e18', 200), width=3)
            nt = 16
            for j in range(nt):
                a = a0 - ov + (a1 - a0 + 2 * ov) * (j + (0.5 if k % 2 else 0)) / nt
                x, y = G(a, bb, z); d2.line([(x, y), (x, y + 7)], fill=hx('8a2a20', 150), width=2)
        d2.line([G(a0 - ov, bm, Hr), G(a1 + ov, bm, Hr)], fill=hx('f6a590'), width=5)                       # 용마루
        d2.line([G(a0 - ov, b1 + ov, Hw - 0.5), G(a1 + ov, b1 + ov, Hw - 0.5)], fill=hx('fbc0a8'), width=4)   # 처마 끝
        d2.line([G(a1 + ov, b1 + ov, Hw - 0.5), G(a1 + ov, bm, Hr)], fill=hx('7a1e18'), width=4)             # 박공 가장자리
        x, y = G((a0 + a1) / 2, bm, Hr); d2.line([(x, y), (x, y - 40)], fill=hx('3a3a3a'), width=3); d2.polygon([(x, y - 40), (x + 22, y - 33), (x, y - 26)], fill=hx('e84b3a')); d2.ellipse([x - 4, y - 44, x + 4, y - 36], fill=hx('f8d84a'))
        # 건초더미 2개
        for (ha, hb) in ((0.52, 0.30), (0.62, 0.34)):
            x, y = G(ha, hb)
            gradfill(canvas, [(x - 20, y - 22), (x + 20, y - 22), (x + 20, y + 4), (x - 20, y + 4)], hx('f4d768'), hx('b89226'), (0.2, 1), 6)
            for k in range(8): d2.line([(x - 18 + k * 5, y - 22), (x - 20 + k * 5, y + 3)], fill=hx('8a6a18', 150), width=2)
            d2.ellipse([x - 20, y - 26, x + 20, y - 18], fill=hx('f8e290'))
    return (a1 + b1 + 0.1, draw)
objs.append(barn())

def tree(a, b, s=1.0, hue=0):
    def draw():
        x, y = G(a, b)
        shadow([(x - 24 * s, y + 4 * s), (x + 46 * s, y + 4 * s), (x + 56 * s, y + 18 * s), (x - 10 * s, y + 18 * s)], 95, 8)
        d2 = ImageDraw.Draw(canvas)
        d2.polygon([(x - 6 * s, y + 2), (x + 6 * s, y + 2), (x + 3.5 * s, y - 52 * s), (x - 3.5 * s, y - 52 * s)], fill=hx('7a5030')); d2.polygon([(x - 6 * s, y + 2), (x - 1 * s, y + 2), (x - 0.5 * s, y - 52 * s), (x - 3.5 * s, y - 52 * s)], fill=hx('a8784a'))
        circles = []
        rs = random.Random(int(a * 977 + b * 331))
        for i in range(16):
            ang = rs.random() * 2 * math.pi; rr = rs.random() ** 0.6 * 40 * s
            circles.append((x + math.cos(ang) * rr * 1.1, y - 66 * s + math.sin(ang) * rr * 0.8, (13 + rs.random() * 11) * s))
        circles.sort(key=lambda c: -c[1])
        for (cx, cy, r) in circles:                                                      # 아래쪽 덩이부터(어두움)
            dk = max(0.0, min(1.0, (cy - (y - 106 * s)) / (60 * s)))
            col = lerp((130, 205, 80), (36, 112, 48), dk)
            gradfill(canvas, [(cx + math.cos(t) * r, cy + math.sin(t) * r * 0.86) for t in [k * math.pi / 12 for k in range(24)]], lerp((170, 232, 110), col, 0.5), lerp(col, (22, 80, 36), 0.6), (0.5, 1), 5)
        for i in range(120):                                                             # 잎 반점(빛)
            cx, cy, r = circles[rs.randrange(len(circles))]; ang = rs.random() * 2 * math.pi; q = rs.random() ** 0.5 * r
            px_, py_ = cx + math.cos(ang) * q, cy + math.sin(ang) * q * 0.86
            lit = (px_ - cx) + (py_ - cy) < 0
            d2.ellipse([px_ - 3, py_ - 2, px_ + 3, py_ + 2], fill=hx('d8f8a0', 150) if lit else hx('1c5a2a', 120))
    return (a + b, draw)
objs.append(tree(0.10, 0.17, 1.35)); objs.append(tree(0.05, 0.43, 1.2))

def flag(a, b):
    def draw():
        x, y = G(a, b); d2 = ImageDraw.Draw(canvas)
        d2.line([(x, y), (x, y - 120)], fill=hx('8a8a92'), width=4); d2.line([(x - 1, y), (x - 1, y - 120)], fill=hx('d8d8e0'), width=1); d2.ellipse([x - 5, y - 128, x + 5, y - 118], fill=hx('f8d84a'))
        for i in range(40):                                                          # 물결치는 깃발(3색 가로줄)
            t = i / 40; X = x + 3 + t * 56; wob = math.sin(t * 6) * 4
            for (z0, z1, col) in ((0, 14, hx('e84b8a')), (14, 28, hx('fff4d0')), (28, 42, hx('3aa0e6'))):
                d2.polygon([(X, y - 114 + z0 + wob), (X + 1.8, y - 114 + z0 + wob), (X + 1.8, y - 114 + z1 + wob), (X, y - 114 + z1 + wob)], fill=tuple(int(c * (0.88 + 0.12 * math.cos(t * 6))) for c in col[:3]) + (255,))
        d2.ellipse([x - 8, y - 3, x + 8, y + 3], fill=hx('5a4a38'))
    return (a + b, draw)
objs.append(flag(0.04, 0.80))

def trough(a, b):
    def draw():
        x, y = G(a, b); shadow([(x - 6, y + 2), (x + 62, y + 2), (x + 72, y + 14), (x + 6, y + 14)], 90, 5)
        d2 = ImageDraw.Draw(canvas)
        gradfill(canvas, [(x - 22, y - 16), (x + 22, y - 16), (x + 22, y + 4), (x - 22, y + 4)], hx('b98650'), hx('6e4a28'), (0.2, 1), 5)
        d2.ellipse([x - 20, y - 22, x + 20, y - 10], fill=hx('7aa8d8')); d2.ellipse([x - 17, y - 20, x + 6, y - 14], fill=hx('bfe0f8', 190)); d2.arc([x - 20, y - 22, x + 20, y - 10], 0, 360, fill=hx('5a3a1c'), width=3)
    return (a + b, draw)
objs.append(trough(0.84, 0.74))

# ---------------- 말과 기수 ----------------
class Obj:
    def __init__(self, w, h, S_, k=1.0): self.w, self.h, self.S, self.k = w, h, S_, k; self.im = Image.new('RGBA', (int(w * S_), int(h * S_)), (0, 0, 0, 0))
    def mask(self, fn): m = Image.new('L', self.im.size, 0); fn(ImageDraw.Draw(m), self.S * self.k); return m
    def fill(self, mask, c_light, c_dark, angle=(0.35, 0.65), noise=0.0):
        a = np.asarray(mask) > 0
        if not a.any(): return
        ys, xs = np.nonzero(a); x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        yy_, xx_ = np.mgrid[y0:y1, x0:x1]
        t = ((xx_ - x0) / max(1, x1 - x0) * angle[0] + (yy_ - y0) / max(1, y1 - y0) * angle[1]); t = np.clip(t / (angle[0] + angle[1]), 0, 1)[..., None]
        col = np.array(c_light[:3]) * (1 - t) + np.array(c_dark[:3]) * t
        if noise: col = col + nrs.normal(0, noise, col.shape[:2])[..., None]
        layer = np.zeros((y1 - y0, x1 - x0, 4), np.uint8); layer[..., :3] = np.clip(col, 0, 255).astype(np.uint8); layer[..., 3] = a[y0:y1, x0:x1] * 255
        self.im.alpha_composite(Image.fromarray(layer, 'RGBA'), (int(x0), int(y0)))
    def soft(self, mask, col, alpha, blur=3, clip=None):
        m = mask.filter(ImageFilter.GaussianBlur(blur)); m = Image.fromarray((np.asarray(m).astype(float) * alpha / 255).astype(np.uint8))
        if clip is not None: m = Image.fromarray(np.minimum(np.asarray(m), np.asarray(clip)))
        lay = Image.new('RGBA', self.im.size, col[:3] + (255,)); lay.putalpha(m); self.im.alpha_composite(lay)

def horse_rider():
    o = Obj(34, 30, S, 1.0); k = o.S * o.k
    def poly(pts, cl, cd, ang=(0.35, 0.65), noise=0.0):
        m = o.mask(lambda d_, s: d_.polygon([(x * s, y * s) for x, y in pts], fill=255)); o.fill(m, cl, cd, ang, noise); return m
    def ell(cx_, cy_, rx_, ry_, cl, cd):
        m = o.mask(lambda d_, s: d_.ellipse([(cx_ - rx_) * s, (cy_ - ry_) * s, (cx_ + rx_) * s, (cy_ + ry_) * s], fill=255)); o.fill(m, cl, cd); return m
    def line(p1, p2, col, w):
        m = o.mask(lambda d_, s: d_.line([(p1[0] * s, p1[1] * s), (p2[0] * s, p2[1] * s)], fill=255, width=max(1, int(w * s)))); lay = Image.new('RGBA', o.im.size, col); o.im.paste(lay, (0, 0), m)
    # 꼬리(뒤)
    poly([(6.5, 13.2), (3.4, 14.6), (2.0, 21), (3.6, 23), (4.6, 18), (7.2, 15.5)], hx('5a2c14'), hx('1e0e06'), (0.4, 0.6), 6)
    for i in range(6): line((6.2 - i * 0.3, 14), (3.0 + i * 0.25, 21.5 + i * 0.12), hx('8a4a24', 150), 0.18)
    # 뒷다리(먼 쪽→가까운 쪽)
    poly([(7.8, 17), (10.2, 17), (10.0, 22), (10.6, 26.4), (8.8, 26.4), (8.0, 22.4)], hx('b8703a'), hx('5a2c10'))
    poly([(8.6, 26.0), (10.8, 26.0), (10.8, 28.2), (8.4, 28.2)], hx('f6edd8'), hx('b8ac90')); poly([(8.4, 28.0), (10.9, 28.0), (10.9, 28.7), (8.3, 28.7)], hx('3a2a1c'), hx('120a04'))
    poly([(5.8, 17), (8.0, 17), (7.6, 22), (8.0, 26.4), (6.4, 26.4), (6.0, 22.2)], hx('a8622c'), hx('4a2410'))
    poly([(6.2, 26.0), (8.2, 26.0), (8.2, 28.2), (6.0, 28.2)], hx('2a1608'), hx('120a04'))
    # 몸통(+근육 음영)
    body = ell(13.4, 15.2, 8.3, 4.7, hx('e0903e'), hx('84401a'))
    o.soft(o.mask(lambda d_, s: d_.ellipse([(7.2) * s, (12.2) * s, (15.2) * s, (15.4) * s], fill=255)), hx('f8bb6a'), 150, 7, body.point(lambda v: v))      # 등 하이라이트
    o.soft(o.mask(lambda d_, s: d_.ellipse([(6.4) * s, (16.6) * s, (20.6) * s, (21.4) * s], fill=255)), hx('3a1808'), 150, 7, body)                         # 배 그림자
    o.soft(o.mask(lambda d_, s: d_.ellipse([(5.6) * s, (12) * s, (9.6) * s, (18) * s], fill=255)), hx('f2a458'), 90, 6, body)                              # 엉덩이 근육 빛
    # 앞다리
    poly([(17.2, 17), (19.6, 17), (19.4, 22), (20.6, 26.4), (18.8, 26.4), (17.4, 22.4)], hx('b8703a'), hx('5a2c10'))
    poly([(18.6, 26.0), (20.8, 26.0), (20.8, 28.2), (18.6, 28.2)], hx('f6edd8'), hx('b8ac90')); poly([(18.4, 28.0), (20.9, 28.0), (20.9, 28.7), (18.3, 28.7)], hx('3a2a1c'), hx('120a04'))
    poly([(14.6, 17), (17.0, 17), (16.0, 22), (14.8, 24.6), (13.6, 24.2), (14.4, 21.8)], hx('a8622c'), hx('4a2410'))
    poly([(13.4, 24.2), (15.2, 24.6), (15.0, 26.4), (13.2, 26.0)], hx('2a1608'), hx('120a04'))
    # 목·머리
    poly([(17.4, 11.6), (20.8, 5.6), (23.2, 6.4), (21.6, 13.2), (18.6, 14.6)], hx('e8984a'), hx('8a4a1e'), (0.5, 0.5), 5)
    head = poly([(21.0, 5.2), (26.4, 7.8), (27.6, 11.2), (25.8, 12.4), (22.6, 10.2), (20.6, 7.8)], hx('eea058'), hx('8e4c20'), (0.5, 0.5))
    poly([(24.4, 8.0), (27.2, 9.6), (27.6, 11.2), (25.8, 11.8)], hx('faf2de'), hx('d8cdb0'))
    poly([(20.4, 3.6), (21.8, 5.4), (20.0, 5.9)], hx('b8682c'), hx('5a2c10'))
    poly([(17.0, 9.4), (20.2, 4.2), (21.0, 5.6), (18.0, 12.0)], hx('4a2410'), hx('1e0e06'))
    for i in range(5): line((17.2 + i * 0.25, 10.8 - i * 1.4), (19.0 + i * 0.2, 12.6 - i * 1.3), hx('7a4220', 160), 0.2)
    # 안장·굴레
    poly([(9.4, 11.6), (14.8, 11.6), (14.2, 14.4), (9.8, 14.4)], hx('e04444'), hx('7a1616'))
    line((12.0, 14.4), (12.4, 17.4), hx('3a2410'), 0.28)
    line((20.6, 7.4), (26.0, 9.4), hx('3a2410'), 0.45); line((22.0, 6.4), (22.6, 9.4), hx('3a2410'), 0.28)
    ell(23.0, 8.0, 0.55, 0.55, hx('2a1c10'), hx('0c0804')); ell(22.85, 7.85, 0.2, 0.2, hx('ffffff'), hx('ffffff'))     # 눈 + 반짝임
    # 기수
    poly([(11.0, 13.4), (13.4, 13.4), (13.4, 19.6), (12.0, 19.6)], hx('ffffff'), hx('b0b6c6'))
    poly([(11.6, 19.4), (13.6, 19.4), (13.8, 23.0), (12.0, 23.0)], hx('3a3a4a'), hx('0e0e16'))
    poly([(9.6, 6.6), (13.4, 6.6), (14.0, 13.6), (9.8, 13.6)], hx('f86858'), hx('a02420'), (0.4, 0.6), 4)
    poly([(13.0, 7.8), (17.6, 10.6), (17.2, 11.8), (12.6, 9.4)], hx('ec5646'), hx('962018'))
    ell(17.6, 11.2, 0.55, 0.55, hx('f4d0a8'), hx('c89060'))
    ell(11.8, 4.4, 2.2, 2.2, hx('ffdcb8'), hx('cf9262'))
    poly([(9.4, 4.0), (10.0, 2.0), (12.0, 1.4), (14.0, 2.4), (14.2, 4.0), (9.4, 4.0)], hx('4a4a6a'), hx('0e0e1c'))
    poly([(13.6, 3.6), (15.6, 3.8), (15.4, 4.4), (13.6, 4.2)], hx('2a2a40'), hx('0e0e1c'))
    o.soft(o.mask(lambda d_, s: d_.ellipse([10.4 * s, 1.8 * s, 12.4 * s, 3.0 * s], fill=255)), hx('b8b8d8'), 160, 3)       # 헬멧 광택
    return o.im
hr = horse_rider()
def horse_draw():
    shadow([G(0.40, 0.60), G(0.56, 0.60), G(0.60, 0.70), G(0.42, 0.72)], 105, 7)
    canvas.alpha_composite(hr, (int(27 * S), int(26.5 * S)))
objs.append((AC + BC + 0.05, horse_draw))
objs.append((AC + BC + 0.9, fence(True)))

objs.sort(key=lambda t: t[0])
for _, fn in objs: fn()

# ===================== 5) 줄이기: 면적 평균(안티앨리어싱), 윤곽은 이진 투명 =====================
a = np.asarray(canvas).astype(float); al = a[..., 3] / 255.0
al_s = al.reshape(H, S, W, S).mean((1, 3))
rgb = (a[..., :3] * al[..., None]).reshape(H, S, W, S, 3).sum((1, 3)) / np.maximum(al.reshape(H, S, W, S).sum((1, 3)), 1e-6)[..., None]
res = np.zeros((H, W, 4), np.uint8); res[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8); res[..., 3] = (al_s > 0.5) * 255
# 약한 선명화로 도트 윤곽 정리
im = Image.fromarray(res, 'RGBA'); rgb_im = im.convert('RGB').filter(ImageFilter.UnsharpMask(radius=0.8, percent=60, threshold=2)); res2 = np.asarray(rgb_im).copy()
final = np.dstack([res2, res[..., 3]]); fin = Image.fromarray(final, 'RGBA'); fin.save(out + '/b9.png')
if len(sys.argv) > 1:
    bg = Image.new('RGBA', fin.size, (150, 70, 160, 255)); bg.alpha_composite(fin); bg.resize((W * 8, H * 8), Image.NEAREST).save(sys.argv[1])
print('승마장 → b9.png')
