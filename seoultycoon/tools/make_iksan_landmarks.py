"""익산 랜드마크 그림: 미륵사지 석탑(b2 자리=서울타워), 왕궁리 오층석탑(b38 자리), 황등석산(산 지형 조각 d15/d25/d50).
사용자가 준 사진(미륵사지 6층 석탑 — 윗부분이 오른쪽으로 무너진 모양 / 왕궁리 오층석탑 / 황등 석산 계단식 채석장)을 따라 8배 해상도로 그린 뒤 줄인다.
→ work/pak_mod/{b2,b38,d15,d25,d50}.png   사용: python3 -I tools/make_iksan_landmarks.py [미리보기.png]"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, lerp, W, S, out

STONE_L = (hx('d6cfbc'), hx('a8a08c')); STONE_R = (hx('a8a08c'), hx('6f6858')); STONE_T = (hx('ece6d4'), hx('c8c0aa'))
ROOF_L = (hx('c9c2ae'), hx('8e8672')); ROOF_R = (hx('9a9380'), hx('5e5848')); ROOF_T = (hx('f0eadb'), hx('d2cab4'))

def courses(s, a0, a1, b0, b1, z0, h, rnd, step=1.4):
    """돌 층(가로 줄눈)과 세로 이음을 두 면에 그린다."""
    G = s.G
    z = z0 + step
    while z < z0 + h - 0.2:
        s.line([G(a0, b1, z), G(a1, b1, z)], hx('6a6250', 120), 2); s.line([G(a1, b0, z), G(a1, b1, z)], hx('3e382c', 130), 2); z += step
    for _ in range(int((a1 - a0) * 60)):
        a = rnd.uniform(a0, a1); zz = z0 + rnd.uniform(0, h); s.line([G(a, b1, zz), G(a, b1, min(z0 + h, zz + step))], hx('6a6250', 90), 2)

def stone_tier(s, cx, cy, w, h, z0, rw, rt, rnd, door=False, pillars=0):
    G = s.G; a0, a1, b0, b1 = cx - w, cx + w, cy - w, cy + w
    s.shadow([G(a0, b1), G(a1, b1), G(a1 + 0.16, b1 + 0.08), G(a0 + 0.1, b1 + 0.1)], 70, 8) if z0 < 4 else None
    s.box(a0, a1, b0, b1, h, STONE_L, STONE_R, STONE_T, z0)
    courses(s, a0, a1, b0, b1, z0, h, rnd)
    if pillars:
        for i in range(pillars + 1):
            a = a0 + (a1 - a0) * i / pillars; s.line([G(a, b1, z0), G(a, b1, z0 + h)], hx('7a7260'), 6); s.line([G(a - 0.004, b1, z0), G(a - 0.004, b1, z0 + h)], hx('e8e2cf'), 2)
    if door:
        da = (a0 + a1) / 2; dw = (a1 - a0) * 0.12
        s.grad([G(da - dw, b1, z0), G(da + dw, b1, z0), G(da + dw, b1, z0 + h * 0.78), G(da - dw, b1, z0 + h * 0.78)], hx('2a2622'), hx('0e0c0a'), (0, 1))
        s.line([G(da - dw, b1, z0), G(da - dw, b1, z0 + h * 0.78), G(da + dw, b1, z0 + h * 0.78), G(da + dw, b1, z0)], hx('b8b09a'), 3)
    # 처마(지붕돌): 넓고 얇게, 아래에 그림자 띠
    ra0, ra1, rb0, rb1 = cx - rw, cx + rw, cy - rw, cy + rw
    s.shadow([G(ra0, rb1, z0 + h), G(ra1, rb1, z0 + h), G(ra1, rb1, z0 + h - 2), G(ra0, rb1, z0 + h - 2)], 90, 4)
    s.box(ra0, ra1, rb0, rb1, rt, ROOF_L, ROOF_R, ROOF_T, z0 + h)
    for i in range(1, 8): s.line([G(ra0 + (ra1 - ra0) * i / 8, rb1, z0 + h), G(ra0 + (ra1 - ra0) * i / 8, rb1, z0 + h + rt)], hx('7a7260', 80), 2)
    # 처마 끝 살짝 들린 모서리
    s.line([G(ra0, rb1, z0 + h + rt), G(ra0 - 0.012, rb1, z0 + h + rt + 0.5)], hx('f6f0de'), 3); s.line([G(ra1, rb1, z0 + h + rt), G(ra1 + 0.012, rb1, z0 + h + rt + 0.5)], hx('f6f0de'), 3)
    return z0 + h + rt

def layered_roof(s, cx, cy, rw, z0, rnd, layers=4, thick=0.8, taper=0.018, hw=0.0):
    """지붕돌: 아래에서 위로 층층이 넓어지는 여러 장의 얇은 돌(사진의 '켜켜이 쌓인 처마')."""
    z = z0
    for k in range(layers):
        w = rw - taper * (layers - 1 - k)
        s.box(cx - w, cx + w, cy - w, cy + w, thick, ROOF_L, ROOF_R, ROOF_T if k == layers - 1 else ROOF_T, z)
        s.line([s.G(cx - w, cy + w, z + thick * 0.5), s.G(cx + w, cy + w, z + thick * 0.5)], hx('6a6250', 90), 2)
        z += thick
    return z

def weathered(s, a0, a1, b1, z0, h, rnd, n=26):
    """벽돌에 주황·회색 풍화 얼룩(사진의 붉은 기운 도는 돌)."""
    G = s.G
    for _ in range(n):
        a = rnd.uniform(a0, a1 - 0.03); z = z0 + rnd.uniform(0, max(0.1, h - 1.0)); w = rnd.uniform(0.02, 0.05); hh = rnd.uniform(0.6, 1.6)
        col = rnd.choice([hx('c89a6c', 120), hx('d8b48a', 110), hx('8e8878', 120), hx('e6c8a0', 100)])
        s.flat([G(a, b1, z), G(a + w, b1, z), G(a + w, b1, z + hh), G(a, b1, z + hh)], col)

# ---------------- 미륵사지 석탑 (b2, 96x68): 아래 두 층은 온전, 윗부분은 오른쪽으로 비스듬히 무너진 모양 ----------------
def mireuksa():
    s = Sc(68, 11); G = s.G; rnd = random.Random(3); s.ground()
    s.shadow([G(0.2, 0.6), G(0.8, 0.6), G(1.02, 0.78), G(0.4, 0.82)], 105, 11)
    cx = cy = 0.5
    s.box(0.12, 0.88, 0.12, 0.88, 1.3, (hx('cbc4ae'), hx('8a8270')), (hx('8a8270'), hx('5a5444')), (hx('dcd5c0'), hx('b8b19c')), 0)          # 낮고 넓은 기단
    s.box(0.18, 0.82, 0.18, 0.82, 1.4, STONE_L, STONE_R, STONE_T, 1.3)
    for k in range(5):                                                                                                                     # 정면 계단
        s.box(0.42 - 0.01 * k, 0.58 + 0.01 * k, 0.82, 0.82 + 0.05 * (5 - k) * 0.5, 0.45 * (k + 1), (hx('dcd5c1'), hx('aaa28e')), (hx('aaa28e'), hx('6f6858')), STONE_T, 0)
    z = 2.7
    # 1층: 넓은 몸돌 + 네 기둥 + 가운데 어두운 문
    s.box(0.20, 0.80, 0.20, 0.80, 7.8, STONE_L, STONE_R, STONE_T, z); courses(s, 0.20, 0.80, 0.20, 0.80, z, 7.8, rnd, 1.3)
    for i in range(6):
        a = 0.20 + 0.60 * i / 5; s.line([G(a, 0.80, z), G(a, 0.80, z + 7.8)], hx('7a7260'), 7); s.line([G(a - 0.006, 0.80, z), G(a - 0.006, 0.80, z + 7.8)], hx('eee8d4'), 3)
    s.grad([G(0.45, 0.80, z), G(0.55, 0.80, z), G(0.55, 0.80, z + 6.0), G(0.45, 0.80, z + 6.0)], hx('2a2622'), hx('0c0a08'), (0, 1))
    s.line([G(0.45, 0.80, z), G(0.45, 0.80, z + 6.0), G(0.55, 0.80, z + 6.0), G(0.55, 0.80, z)], hx('c4bca6'), 3)
    weathered(s, 0.2, 0.8, 0.80, z, 7.8, rnd, 10)
    z += 7.8
    z = layered_roof(s, cx, cy, 0.40, z, rnd, layers=4, thick=0.75, taper=0.02)                                                              # 1층 지붕돌(넓고 두껍다)
    # 2층: 낮은 몸돌 + 창 4칸
    s.box(0.25, 0.75, 0.25, 0.75, 4.6, STONE_L, STONE_R, STONE_T, z); courses(s, 0.25, 0.75, 0.25, 0.75, z, 4.6, rnd, 1.2)
    for i in range(4):
        a = 0.28 + 0.12 * i; s.grad([G(a, 0.75, z + 1.2), G(a + 0.07, 0.75, z + 1.2), G(a + 0.07, 0.75, z + 3.6), G(a, 0.75, z + 3.6)], hx('7a7466'), hx('46423a'), (0, 1))
    z += 4.6
    z = layered_roof(s, cx, cy, 0.33, z, rnd, layers=3, thick=0.7, taper=0.02)                                                              # 2층 지붕돌
    # 3층 이상: 오른쪽(a 방향)으로 비스듬히 무너진 돌더미(왼쪽 가장자리는 곧은 사선, 오른쪽에는 지붕돌이 계단식으로 튀어나옴)
    b0, b1 = 0.37, 0.63; zt = z + 13.0
    front = [G(0.31, b1, z), G(0.69, b1, z), G(0.70, b1, z + 5), G(0.72, b1, z + 9), G(0.69, b1, zt), G(0.52, b1, zt), G(0.45, b1, zt - 2)]
    s.grad([G(0.31, b1, z), G(0.69, b1, z), G(0.69, b1, zt), G(0.52, b1, zt)], hx('e0d8c4'), hx('b4ac96'), (0.3, 1), 5)
    for k in range(13):                                                                                                                    # 돌 켜(가로줄)와 어긋난 세로 이음
        zz = z + 0.5 + k * 1.0; aL = 0.31 + (0.21 * (k / 13.0)); s.line([G(aL, b1, zz), G(0.70, b1, zz)], hx('6a6250', 120), 2)
        for j in range(int((0.70 - aL) * 28)): a = aL + 0.035 * (j + (0.5 if k % 2 else 0)); s.line([G(min(a, 0.69), b1, zz), G(min(a, 0.69), b1, zz + 1.0)], hx('6a6250', 90), 2)
    weathered(s, 0.34, 0.69, b1, z, 12, rnd, 34)
    s.grad([G(0.69, b0, z), G(0.69, b1, z), G(0.69, b1, zt), G(0.69, b0, zt)], hx('a89f8a'), hx('6a634f'), (0.3, 1))                        # 오른쪽 어두운 면
    s.grad([G(0.52, b0, zt), G(0.69, b0, zt), G(0.69, b1, zt), G(0.52, b1, zt)], hx('f0eadb'), hx('d6cfba'), (1, 0.5))
    for k in range(5):                                                                                                                     # 오른쪽으로 튀어나온 지붕돌 5개(계단식)
        zz = z + 1.0 + k * 2.6; a1 = 0.70 + 0.012 * k
        s.box(0.60 + 0.012 * k, a1 + 0.10 - 0.012 * k, 0.33, 0.67, 0.8, ROOF_L, ROOF_R, ROOF_T, zz)
        s.line([G(0.62 + 0.012 * k, 0.67, zz + 0.4), G(a1 + 0.10 - 0.012 * k, 0.67, zz + 0.4)], hx('5a5444', 120), 2)
    s.box(0.58, 0.70, 0.40, 0.60, 0.9, ROOF_L, ROOF_R, ROOF_T, zt)
    s.tree(0.04, 0.86, 0.9); s.tree(0.94, 0.18, 0.8)
    return s.finish()

# ---------------- 왕궁리 오층석탑 (b38, 96x84): 키 큰 1층 몸돌, 켜켜이 쌓인 얇은 지붕돌 5개 ----------------
def wanggungni():
    s = Sc(84, 12); G = s.G; rnd = random.Random(4)
    s.grad([G(0, 1), G(1, 1), G(1, 1, -3.4), G(0, 1, -3.4)], hx('a48048'), hx('6a4c28'), (0, 1), 4)
    s.grad([G(1, 0), G(1, 1), G(1, 1, -3.4), G(1, 0, -3.4)], hx('82602e'), hx('4e3818'), (0, 1), 4)
    s.grad(s.ground_poly(), hx('d2c870'), hx('a4a448'), (0.4, 1), 5)
    d = ImageDraw.Draw(s.im)
    for i in range(1500):
        a, b = rnd.random(), rnd.random(); x, y = G(a, b); d.line([(x, y), (x + rnd.randint(-2, 2), y - rnd.randint(4, 9))], fill=rnd.choice([hx('e8e286', 150), hx('7e8a34', 140), hx('b4ae54', 130)]), width=2)
    d.line([G(0, 1), G(1, 1), G(1, 0)], fill=hx('ece698', 200), width=3)
    s.shadow([G(0.3, 0.64), G(0.7, 0.64), G(0.98, 0.84), G(0.5, 0.88)], 110, 11)
    cx = cy = 0.5
    s.box(0.28, 0.72, 0.28, 0.72, 2.6, (hx('c4bca6'), hx('8e866e')), (hx('8e866e'), hx('5a543e')), STONE_T, 0)                                # 넓은 기단(석판 면)
    for i in range(1, 7): s.line([G(0.28 + 0.44 * i / 7, 0.72, 0), G(0.28 + 0.44 * i / 7, 0.72, 2.6)], hx('5a543e', 110), 2)
    z = 2.6
    # 1층 몸돌: 키가 크고 세로 판석(주황 풍화), 모서리 기둥
    s.box(0.34, 0.66, 0.34, 0.66, 9.0, (hx('d8ccb0'), hx('a89c80')), (hx('a89c80'), hx('6a604a')), STONE_T, z)
    for i in range(1, 9): a = 0.34 + 0.32 * i / 9; s.line([G(a, 0.66, z), G(a, 0.66, z + 9.0)], hx('6a604a', 130), 2)
    weathered(s, 0.34, 0.66, 0.66, z, 9.0, rnd, 40)
    z += 9.0
    for (w, h, rw, lay) in ((0.135, 3.0, 0.215, 5), (0.118, 3.0, 0.19, 5), (0.102, 2.8, 0.165, 5), (0.086, 2.6, 0.14, 5), (0.07, 2.4, 0.115, 4)):
        z = layered_roof(s, cx, cy, rw, z, rnd, layers=lay, thick=0.62, taper=0.012)                                                          # 켜켜이 쌓인 지붕돌
        s.box(cx - w, cx + w, cy - w, cy + w, h, STONE_L, STONE_R, STONE_T, z); weathered(s, cx - w, cx + w, cy + w, z, h, rnd, 8); z += h
    z = layered_roof(s, cx, cy, 0.095, z, rnd, layers=4, thick=0.55, taper=0.012)
    s.box(cx - 0.02, cx + 0.02, cy - 0.02, cy + 0.02, 1.8, STONE_L, STONE_R, STONE_T, z)                                                       # 상륜(보주)
    x, y = G(cx, cy, z + 2.6); d.ellipse([x - 7, y - 8, x + 7, y + 4], fill=hx('d8d0bc')); d.ellipse([x - 5, y - 7, x - 1, y - 2], fill=hx('f2ecdc'))
    ps = [(t, 0.88) for t in np.linspace(0.14, 0.86, 9)] + [(0.88, t) for t in np.linspace(0.14, 0.86, 9)]                                    # 붉은 말뚝 + 흰 줄
    for (a, b) in ps:
        x, y = G(a, b); d.polygon([(x - 3, y), (x + 3, y), (x + 3, y - 14), (x - 3, y - 14)], fill=hx('c8382a')); d.polygon([(x - 3, y), (x - 0.5, y), (x - 0.5, y - 14), (x - 3, y - 14)], fill=hx('e86a58')); d.ellipse([x - 3.5, y - 16, x + 3.5, y - 12], fill=hx('f4dcd2'))
    for (p1, p2) in zip(ps[:8], ps[1:9]): d.line([G(*p1, 1.6), G(*p2, 1.6)], fill=hx('f4f4f4', 220), width=2)
    for (p1, p2) in zip(ps[9:17], ps[10:18]): d.line([G(*p1, 1.6), G(*p2, 1.6)], fill=hx('f4f4f4', 220), width=2)
    s.tree(0.03, 0.62, 0.7); s.tree(0.93, 0.18, 0.8)
    return s.finish()

# ---------------- 황등석산 (산 지형 조각 64x64) ----------------
def quarry(seed):
    rnd = random.Random(seed); N = 64; SS = 8
    im = Image.new('RGBA', (N * SS, N * SS), (0, 0, 0, 255)); d = ImageDraw.Draw(im)
    # 하늘 대신 위쪽 먼 산 능선(어두운 녹색) → 계단식 화강암 단 → 흙길
    d.rectangle([0, 0, N * SS, N * SS], fill=hx('6a8a3c'))
    levels = 5; top = 5
    for k in range(levels):
        y0 = top + k * 11.5; x = 0
        while x < N * SS:
            bw = rnd.randint(5, 11) * SS // 2 + 4; j0 = rnd.uniform(-1.2, 1.2) * SS; j1 = rnd.uniform(-1.2, 1.2) * SS
            yt = y0 * SS + j0; yb = (y0 + 11) * SS + j1
            d.polygon([(x, yt), (x + bw, yt), (x + bw, yt + 3.4 * SS), (x, yt + 3.4 * SS)], fill=hx('c8aa70') if rnd.random() < 0.7 else hx('b89860'))            # 단 윗면(흙)
            shade = rnd.randint(-16, 14); top_c = (204 + shade, 202 + shade, 196 + shade); bot_c = (140 + shade, 138 + shade, 134 + shade)
            for yy in range(int(yt + 3.4 * SS), int(yb)):
                t = (yy - (yt + 3.4 * SS)) / max(1, yb - (yt + 3.4 * SS)); col = tuple(int(top_c[c] * (1 - t) + bot_c[c] * t) for c in range(3)) + (255,)
                d.line([(x, yy), (x + bw, yy)], fill=col)
            d.line([(x, yt + 3.4 * SS), (x, yb)], fill=(100, 98, 92, 255), width=3); d.line([(x + bw - 2, yt + 3.4 * SS), (x + bw - 2, yb)], fill=(236, 234, 226, 150), width=2)
            for jj in range(2): yj = yt + (5.2 + jj * 2.8) * SS; d.line([(x, yj), (x + bw, yj + rnd.uniform(-0.8, 0.8) * SS)], fill=(120, 118, 112, 130), width=2)
            x += bw
        d.line([(0, y0 * SS + 0.3 * SS), (N * SS, y0 * SS + 0.3 * SS)], fill=hx('3a9a58'), width=4)                    # 초록 안전망 띠
        d.line([(0, (y0 + 3.4) * SS), (N * SS, (y0 + 3.4) * SS)], fill=hx('f0ead6', 200), width=3)
    # 굴곡진 흙길(사진의 노란 흙길)
    pts = [(rnd.uniform(0, 14) * SS, 0)]
    for k in range(1, 8): pts.append(((10 + k * 6 + rnd.uniform(-5, 5)) * SS, k * 9 * SS))
    d.line(pts, fill=hx('d4b070'), width=int(5.5 * SS), joint='curve'); d.line(pts, fill=hx('ecd49a', 150), width=int(2.6 * SS), joint='curve')
    # 석재 블록 더미
    for _ in range(9):
        x, y = rnd.uniform(4, 58) * SS, rnd.uniform(10, 60) * SS; w, h = rnd.uniform(3, 6) * SS, rnd.uniform(2, 3.5) * SS
        d.polygon([(x, y), (x + w, y), (x + w, y + h), (x, y + h)], fill=hx('c8c4b8')); d.polygon([(x, y), (x + w, y), (x + w - 1.2 * SS, y - 1.4 * SS), (x - 1.2 * SS, y - 1.4 * SS)], fill=hx('e8e4d6')); d.line([(x, y), (x + w, y)], fill=hx('8a8678'), width=2)
    noise = np.random.RandomState(seed).normal(0, 6, (N * SS, N * SS, 1)); arr = np.clip(np.asarray(im).astype(float)[..., :3] + noise, 0, 255)
    arr = arr.reshape(N, SS, N, SS, 3).mean((1, 3)); return Image.fromarray(arr.astype(np.uint8), 'RGB')

if __name__ == '__main__':
    items = {'b2': mireuksa(), 'b38': wanggungni()}
    q = {'d15': quarry(1), 'd25': quarry(2), 'd50': quarry(3).crop((0, 0, 41, 48))}
    for n, im in items.items(): im.save(f'{out}/{n}.png')
    for n, im in q.items(): im.save(f'{out}/{n}.png')
    if len(sys.argv) > 1:
        sheet = Image.new('RGBA', (96 * 6 * 2 + 24 + 64 * 3 * 4, 84 * 6), (150, 70, 160, 255)); x = 0
        for n, im in items.items():
            bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im); sheet.paste(bg.resize((im.width * 6, im.height * 6), Image.NEAREST), (x, 0)); x += im.width * 6 + 12
        for n, im in q.items(): sheet.paste(im.convert('RGBA').resize((im.width * 4, im.height * 4), Image.NEAREST), (x, 0)); x += im.width * 4 + 4
        sheet.save(sys.argv[1])
    print('랜드마크 →', out)
