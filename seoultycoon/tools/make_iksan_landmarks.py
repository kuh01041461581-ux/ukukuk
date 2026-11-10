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

# ---------------- 미륵사지 석탑 (b2, 96x68) ----------------
def mireuksa():
    s = Sc(68, 11); G = s.G; rnd = random.Random(3); s.ground()
    # 연한 모래빛 광장 + 마당 돌 하나씩
    s.shadow([G(0.2, 0.6), G(0.8, 0.6), G(1.02, 0.78), G(0.4, 0.82)], 105, 11)
    cx = cy = 0.5
    z = 0
    s.box(0.16, 0.84, 0.16, 0.84, 1.6, (hx('c2bba6'), hx('8a8270')), (hx('8a8270'), hx('5a5444')), (hx('d6cfba'), hx('b4ad98')), 0)     # 아래 기단
    s.box(0.22, 0.78, 0.22, 0.78, 1.6, STONE_L, STONE_R, STONE_T, 1.6)                                                                      # 위 기단
    for k in range(4):                                                                                                                     # 정면 계단(오른쪽 아래 면=a, 왼쪽 아래 면=b 중 정면 b)
        s.box(0.44 - 0.012 * k, 0.56 + 0.012 * k, 0.78, 0.78 + 0.04 * (4 - k) * 0.5, 0.5 * (k + 1), (hx('d8d1bd'), hx('a8a08c')), (hx('a8a08c'), hx('6f6858')), STONE_T, 0)
    z = 3.2
    z = stone_tier(s, cx, cy, 0.25, 8.2, z, 0.315, 1.5, rnd, door=True, pillars=4)
    z = stone_tier(s, cx, cy, 0.205, 5.6, z, 0.265, 1.4, rnd, pillars=3)
    z = stone_tier(s, cx, cy, 0.172, 4.8, z, 0.225, 1.3, rnd)
    z = stone_tier(s, cx, cy, 0.145, 4.0, z, 0.19, 1.2, rnd)
    # 5~6층: 사진처럼 오른쪽(a 방향)으로 기울어 무너진 돌 더미 — 층마다 오른쪽으로 밀리고 한쪽이 잘려 나간 모양
    for i, (dx, w, h, cut) in enumerate([(0.02, 0.12, 2.6, 0.30), (0.05, 0.10, 2.4, 0.45), (0.085, 0.078, 2.2, 0.55)]):
        a0, a1 = cx - w + dx, cx + w * (1 - cut) + dx; b0, b1 = cy - w * 0.9, cy + w * 0.9
        s.box(a0, a1, b0, b1, h, STONE_L, STONE_R, STONE_T, z); courses(s, a0, a1, b0, b1, z, h, rnd, 0.9)
        s.box(a0 - 0.02, a1 + 0.035, b0 - 0.02, b1 + 0.02, 0.9, ROOF_L, ROOF_R, ROOF_T, z + h)
        for j in range(3):                                                                                       # 무너져 틈이 보이는 돌
            aa = a0 + (a1 - a0) * rnd.random(); s.line([G(aa, b1, z + 0.3 + j * 0.7), G(aa + 0.03, b1, z + 0.3 + j * 0.7)], hx('4a4436'), 4)
        z += h + 0.9
    for k in range(6):                                                                                       # 떨어진 돌 조각
        a = 0.62 + k * 0.035; b = 0.60 + (k % 2) * 0.05; s.box(a, a + 0.03, b, b + 0.03, 1.2 + (k % 3) * 0.3, STONE_L, STONE_R, STONE_T, 1.6)
    s.tree(0.04, 0.84, 0.9); s.tree(0.94, 0.20, 0.8)
    return s.finish()

# ---------------- 왕궁리 오층석탑 (b38, 96x84) ----------------
def wanggungni():
    s = Sc(84, 12); G = s.G; rnd = random.Random(4)
    # 바닥: 풀 언덕(사진: 누런 잔디 언덕 위의 탑)
    G_ = s.G
    s.grad([G_(0, 1), G_(1, 1), G_(1, 1, -3.4), G_(0, 1, -3.4)], hx('a48048'), hx('6a4c28'), (0, 1), 4)
    s.grad([G_(1, 0), G_(1, 1), G_(1, 1, -3.4), G_(1, 0, -3.4)], hx('82602e'), hx('4e3818'), (0, 1), 4)
    s.grad(s.ground_poly(), hx('c8c46a'), hx('9aa044'), (0.4, 1), 5)
    d = ImageDraw.Draw(s.im)
    for i in range(1500):
        a, b = rnd.random(), rnd.random(); x, y = G(a, b); d.line([(x, y), (x + rnd.randint(-2, 2), y - rnd.randint(4, 9))], fill=rnd.choice([hx('e4de80', 150), hx('7e8a34', 140), hx('b0aa50', 130)]), width=2)
    d.line([G(0, 1), G(1, 1), G(1, 0)], fill=hx('e8e290', 200), width=3)
    s.shadow([G(0.3, 0.64), G(0.7, 0.64), G(0.98, 0.84), G(0.5, 0.88)], 110, 11)
    cx = cy = 0.5
    s.box(0.30, 0.70, 0.30, 0.70, 2.4, (hx('bdb59e'), hx('867e68')), (hx('867e68'), hx('544e3c')), STONE_T, 0)                       # 넓은 기단
    s.box(0.34, 0.66, 0.34, 0.66, 2.0, STONE_L, STONE_R, STONE_T, 2.4)
    for k in range(3):                                                                                                                # 계단
        s.box(0.46, 0.54, 0.66, 0.66 + 0.03 * (3 - k), 0.7 * (k + 1), (hx('d8d1bd'), hx('a8a08c')), (hx('a8a08c'), hx('6f6858')), STONE_T, 0)
    z = 4.4
    for (w, h, rw) in ((0.135, 7.4, 0.19), (0.115, 6.4, 0.165), (0.098, 5.6, 0.142), (0.082, 5.0, 0.12), (0.068, 4.4, 0.10)):
        z = stone_tier(s, cx, cy, w, h, z, rw, 1.2, rnd, pillars=2 if w > 0.1 else 0)
    # 상륜부(찰주): 복발 + 보주
    s.box(cx - 0.02, cx + 0.02, cy - 0.02, cy + 0.02, 2.0, STONE_L, STONE_R, STONE_T, z); s.box(cx - 0.012, cx + 0.012, cy - 0.012, cy + 0.012, 3.2, STONE_L, STONE_R, STONE_T, z + 2.0)
    x, y = G(cx, cy, z + 5.8); d.ellipse([x - 7, y - 9, x + 7, y + 5], fill=hx('dcd5c0')); d.ellipse([x - 5, y - 8, x, y - 2], fill=hx('f4eee0'))
    # 붉은 말뚝 + 흰 줄 울타리(사진 아래쪽)
    ps = []
    for t in np.linspace(0.14, 0.86, 9): ps.append((t, 0.88))
    for t in np.linspace(0.14, 0.86, 9): ps.append((0.88, t))
    for (a, b) in ps:
        x, y = G(a, b); d.polygon([(x - 3, y), (x + 3, y), (x + 3, y - 14), (x - 3, y - 14)], fill=hx('d83a2a')); d.polygon([(x - 3, y), (x - 0.5, y), (x - 0.5, y - 14), (x - 3, y - 14)], fill=hx('f26a58')); d.ellipse([x - 3.5, y - 16, x + 3.5, y - 12], fill=hx('f8e0d8'))
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
