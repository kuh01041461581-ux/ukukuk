"""익산 새 건물들을 저해상도 직접 도트로 찍는다(docs/PIXEL_STYLE_SPEC.md 규격: 윤곽선 없음, 체크 디더·2열 줄무늬 질감, 면 3톤, 그림자는 바닥색을 어둡게).
각 함수는 build_sprites 의 ITEMS 형식 — 인자 없이 (96, Hc) RGBA 를 돌려준다."""
import os, sys, random
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pixel_direct as PD
from pixel_direct import Px, hx, shade, checker, stripes, tree, mask_of

def rshade(c, f=0.66): return (int(c[0] * f * 0.98), int(c[1] * f * 0.92), min(255, int(c[2] * f * 1.12)), 255)       # 오른쪽 면: 어둡게 + 보라 쪽으로

def ground(p, base='9cc27a', paved=None, edge='e8e4f4'):
    p.poly([(0.0, 0.0, 0), (1.0, 0.0, 0), (1.0, 1.0, 0), (0.0, 1.0, 0)], hx(base), None)
    checker(p, [(0.0, 0.0, 0), (1.0, 0.0, 0), (1.0, 1.0, 0), (0.0, 1.0, 0)], hx(base), shade(hx(base), 0.94), 0)
    if paved:
        for (a0, a1, b0, b1, c) in paved:
            checker(p, [(a0, b0, 0), (a1, b0, 0), (a1, b1, 0), (a0, b1, 0)], hx(c), shade(hx(c), 0.95), 0)

def box(p, a0, a1, b0, b1, z0, z1, base, tex=2, topc=None, glass=False):
    """아이소 상자: 앞(밝음) 줄무늬 + 오른쪽(어두움) 줄무늬 + 윗면. 위쪽이 약간 밝은 3단 그라데이션."""
    L1, L2 = base, shade(base, 0.93); R1 = rshade(base); R2 = shade(R1, 0.93)
    yt = p.G(a0, b1, z1)[1]; yb = p.G(a0, b1, z0)[1]; h = max(yb - yt, 3)
    bands = [(yt, yt + h // 3, shade(L1, 1.08), shade(L2, 1.08)), (yt + h // 3, yt + 2 * h // 3, L1, L2), (yt + 2 * h // 3, yt + h + 40, shade(L1, 0.9), shade(L2, 0.9))]
    rb = [(a, b, shade(c, 0.99), shade(d, 0.99)) for (a, b, c, d) in [(yt, yt + h // 2, R1, R2), (yt + h // 2, yt + h + 40, shade(R1, 0.9), shade(R2, 0.9))]]
    F = (lambda pts, c1, c2, bd: stripes(p, pts, c1, c2, tex, bd)) if isinstance(tex, int) else (lambda pts, c1, c2, bd: checker(p, pts, c1, c2, 0, bd))
    F([(a0, b1, z0), (a1, b1, z0), (a1, b1, z1), (a0, b1, z1)], L1, L2, bands)
    F([(a1, b0, z0), (a1, b1, z0), (a1, b1, z1), (a1, b0, z1)], R1, R2, rb)
    p.poly([(a0, b0, z1), (a1, b0, z1), (a1, b1, z1), (a0, b1, z1)], topc or shade(base, 1.16), None)
    p.line((a0, b1, z1), (a1, b1, z1), shade(base, 1.25)); p.line((a1, b1, z0), (a1, b1, z1), shade(base, 0.8)); p.line((a0, b1, z0), (a1, b1, z0), shade(base, 0.78))

def wins(p, face, fixed, lo, hi, z0, z1, cols, rows, gl=('dff3ff', 'b0d8f4', '82b8e0', '5c92c2'), pad=0.25):
    """면 위에 창 격자. face='f'(앞면, fixed=b, lo/hi=a), 's'(옆면, fixed=a, lo/hi=b)."""
    GL = [hx(c) for c in gl]; cw = (hi - lo) / cols; ch = (z1 - z0) / rows
    for r in range(rows):
        for c in range(cols):
            u0 = lo + cw * (c + pad); u1 = lo + cw * (c + 1 - pad); w0 = z0 + ch * (r + pad); w1 = z0 + ch * (r + 1 - pad)
            for i in range(4):
                za, zb = w1 - (w1 - w0) * (i + 1) / 4, w1 - (w1 - w0) * i / 4
                (p.rect_wall(u0, u1, fixed, za, zb, GL[i], GL[i]) if face == 'f' else p.rect_side(fixed, u0, u1, za, zb, shade(GL[i], 0.85), shade(GL[i], 0.85)))
            (p.line((u0, fixed, w0 - 0.3), (u1, fixed, w0 - 0.3), hx('b79255')) if face == 'f' else None)

def hip_roof(p, a0, a1, b0, b1, z, rise, c1, c2, over=0.04, inset=0.07):
    """우진각 지붕: 앞 경사(밝음, 체크 디더) + 옆 경사(어두움)."""
    bm = (b0 + b1) / 2; o = over; front = [(a0 - o, b1 + o, z), (a1 + o, b1 + o, z), (a1 - inset, bm, z + rise), (a0 + inset, bm, z + rise)]
    ry = p.G(a0, bm, z + rise)[1]
    checker(p, front, c1, c2, 0, bands=[(ry - 2, ry + 3, shade(c1, 1.18), shade(c2, 1.18)), (ry + 3, ry + 7, c1, c2), (ry + 7, ry + 30, shade(c1, 0.82), shade(c2, 0.82))])
    side = [(a1 + o, b0 - o, z), (a1 + o, b1 + o, z), (a1 - inset, bm, z + rise)]
    checker(p, side, shade(c1, 0.62), shade(c2, 0.62), 1)
    p.line((a0 + inset, bm, z + rise), (a1 - inset, bm, z + rise), shade(c1, 1.4)); p.line((a0 - o, b1 + o, z), (a1 + o, b1 + o, z), shade(c2, 0.7))

def pagoda(p, ca, cb, base_w, tiers, h0, color=(0.82, 0.8, 0.74)):
    """석탑: 계단식 기단 + 각 층 몸돌(줄무늬) + 지붕돌(넓은 얇은 판, 아래 그림자)."""
    g = hx('d8d4c6'); g = tuple(int(v * 255) for v in color) + (255,)
    w = base_w
    box(p, ca - w, ca + w, cb - w, cb + w, 0, 2, shade(g, 0.95)); w -= 0.025
    box(p, ca - w, ca + w, cb - w, cb + w, 2, 4, g); z = 4
    for i in range(tiers):
        w *= 0.84 if i else 0.80
        bh = h0 * (0.9 - i * 0.04)
        box(p, ca - w * 0.8, ca + w * 0.8, cb - w * 0.8, cb + w * 0.8, z, z + bh, shade(g, 1.0))
        z += bh; ow = w * 1.2
        box(p, ca - ow, ca + ow, cb - ow, cb + ow, z, z + 1.8, shade(g, 0.72)); z += 1.8
    return z

def shadow_poly(p, pts, f=0.74):
    m = mask_of(p, pts)
    for y in range(p.Hc):
        for x in range(96):
            if m.getpixel((x, y)) and p.im.getpixel((x, y))[3]: p.im.putpixel((x, y), shade(p.im.getpixel((x, y)), f))

def pond(p, a0, a1, b0, b1):
    p.poly([(a0 - 0.02, b0 - 0.02, 0), (a1 + 0.02, b0 - 0.02, 0), (a1 + 0.02, b1 + 0.02, 0), (a0 - 0.02, b1 + 0.02, 0)], hx('b9b4a4'), None)
    checker(p, [(a0, b0, 0), (a1, b0, 0), (a1, b1, 0), (a0, b1, 0)], hx('5ea2d8'), hx('4a8cc4'), 0, bands=[(0, 200, hx('5ea2d8'), hx('4a8cc4'))])
    for t in (0.3, 0.6):
        x, y = p.G(a0 + (a1 - a0) * t, b0 + (b1 - b0) * 0.4, 0); p.d.line([(x - 2, y), (x + 2, y)], fill=hx('a8d4f0'))

def finish(p): p.outline_soft(); return p.im

def _soft(self):
    import numpy as np
    al = np.asarray(self.im.getchannel('A')) > 0; ed = al & ~(np.roll(al, 1, 0) & np.roll(al, -1, 0) & np.roll(al, 1, 1) & np.roll(al, -1, 1))
    arr = np.asarray(self.im).copy(); arr[ed, :3] = (arr[ed, :3] * 0.6).astype(np.uint8); self.im = Image.fromarray(arr, 'RGBA'); self.d = ImageDraw.Draw(self.im)
Px.outline_soft = _soft

def T(p, a, b, s=0.6): tree(p, a, b, s)

# ---------------------------------------------------------------- 미륵사지 석탑 (b2)
def mireuksa():
    p = Px(2); ground(p, '9cc27a', [(0.30, 0.70, 0.30, 0.70, 'd8d2c0')]); pond(p, 0.06, 0.26, 0.55, 0.95); pond(p, 0.74, 0.94, 0.05, 0.40)
    shadow_poly(p, [(0.52, 0.50, 0), (0.84, 0.44, 0), (0.96, 0.60, 0), (0.60, 0.70, 0)], 0.78)
    pagoda(p, 0.5, 0.5, 0.20, 5, 9, (0.72, 0.69, 0.62))
    return finish(p)

# ---------------------------------------------------------------- 백제왕궁(왕궁리 오층석탑) (b38)
def baekje():
    p = Px(38); ground(p, '9cc27a', [(0.08, 0.92, 0.70, 0.90, 'cfc8b2'), (0.40, 0.60, 0.30, 0.70, 'd8d2c0')])
    for a in (0.10, 0.90):                                                                                                    # 궁궐 담장(흙담 + 기와 얹음)
        box(p, a - 0.02, a + 0.02, 0.10, 0.60, 0, 4, hx('c9a878')); box(p, a - 0.035, a + 0.035, 0.10, 0.60, 4, 5.6, hx('5a6a66'))
    box(p, 0.10, 0.90, 0.07, 0.11, 0, 4, hx('c9a878')); box(p, 0.09, 0.91, 0.06, 0.12, 4, 5.6, hx('5a6a66'))
    shadow_poly(p, [(0.56, 0.46, 0), (0.88, 0.42, 0), (0.96, 0.62, 0), (0.62, 0.68, 0)], 0.78)
    pagoda(p, 0.5, 0.45, 0.17, 5, 8, (0.80, 0.62, 0.38))
    return finish(p)

# ---------------------------------------------------------------- 익산예술의전당 (b42)
def arts():
    p = Px(42); ground(p, 'b9b6c4', [(0.05, 0.95, 0.66, 0.96, 'cfcce0')])
    p.d.ellipse([p.G(0.30, 0.82)[0] - 11, p.G(0.30, 0.82)[1] - 4, p.G(0.30, 0.82)[0] + 11, p.G(0.30, 0.82)[1] + 4], fill=hx('c8a070'))
    box(p, 0.34, 0.86, 0.10, 0.34, 0, 20, hx('f2f4fa'))                                                                      # 뒤 타워(흰 직육면체)
    box(p, 0.16, 0.94, 0.30, 0.62, 0, 11, hx('dfe3ee'))                                                                      # 공연장 본체
    wins(p, 'f', 0.62, 0.20, 0.90, 1, 9, 6, 1, gl=('bfe3ff', '8ec0f0', '5a9ae0', '3a78c0'), pad=0.12)                        # 유리 세로 띠
    for a in (0.24, 0.40, 0.56, 0.72):
        z1 = 10 + (a * 7 % 3); p.poly([(a, 0.62, 0), (a + 0.04, 0.62, 0), (a + 0.04, 0.62, z1), (a, 0.62, z1 - 1)], hx('c4cad6'), None)   # 비스듬한 판
    box(p, 0.04, 0.22, 0.36, 0.60, 0, 6, hx('eef2fa')); wins(p, 'f', 0.60, 0.05, 0.21, 1, 5, 3, 1, pad=0.12)                      # 왼쪽 유리 별관
    box(p, 0.38, 0.70, 0.62, 0.70, 0, 1.4, hx('dfe3ee'))
    shadow_poly(p, [(0.86, 0.10, 0), (0.98, 0.10, 0), (0.98, 0.70, 0), (0.92, 0.62, 0)], 0.78)
    return finish(p)

# ---------------------------------------------------------------- 야구장 (b33)
SEAT_O = [(hx('3a6cc8'), hx('2a54a8')), (hx('e8ecf8'), hx('c8d0e8')), (hx('3a6cc8'), hx('2a54a8'))]

def baseball():
    p = Px(33); rr = random.Random(7); ground(p, '7ab858')
    A, B = 0.16, 0.16                                                                                                          # 경기장 안쪽 경계
    checker(p, [(A, B, 0), (0.88, B, 0), (0.88, 0.88, 0), (A, 0.88, 0)], hx('6cb450'), hx('5ea446'), 0)                       # 잔디(두 톤 체크)
    for i in range(0, 7):                                                                                                      # 잔디 깎은 줄무늬
        a0 = A + 0.10 * i
        if i % 2 == 0 and a0 < 0.84: p.poly([(a0, B, 0), (min(a0 + 0.05, 0.88), B, 0), (min(a0 + 0.05, 0.88), 0.88, 0), (a0, 0.88, 0)], hx('74be56'), None)
    infield = [(A, B, 0), (0.62, B, 0), (0.50, 0.30, 0), (0.30, 0.50, 0), (B, 0.62, 0)]
    checker(p, [(A, B, 0.1), (0.60, B, 0.1), (A, 0.60, 0.1)], hx('dcae6c'), hx('c89a5c'), 0)                                    # 내야 흙(체크)
    p.poly([(0.24, 0.24, 0.2), (0.44, 0.24, 0.2), (0.44, 0.44, 0.2), (0.24, 0.44, 0.2)], hx('5ea446'), None)                    # 내야 잔디
    checker(p, [(0.24, 0.24, 0.2), (0.44, 0.24, 0.2), (0.44, 0.44, 0.2), (0.24, 0.44, 0.2)], hx('6cb450'), hx('5ea446'), 1)
    p.line((A, B, 0.2), (0.88, B, 0.2), hx('ffffff')); p.line((A, B, 0.2), (A, 0.88, 0.2), hx('ffffff'))                          # 파울 라인
    for (pa, pb) in (((0.17, 0.17), (0.42, 0.17)), ((0.17, 0.17), (0.17, 0.42)), ((0.42, 0.17), (0.42, 0.42)), ((0.17, 0.42), (0.42, 0.42))):   # 주루 흙길(두 줄 폭)
        for off in (0.0, 0.012):
            p.line((pa[0] + (off if pa[0] == pb[0] else 0), pa[1] + (off if pa[1] == pb[1] else 0), 0.25), (pb[0] + (off if pa[0] == pb[0] else 0), pb[1] + (off if pa[1] == pb[1] else 0), 0.25), hx('dcae6c'))
    x, y = p.G(0.32, 0.32, 0.4); p.d.ellipse([x - 5, y - 2, x + 5, y + 2], fill=hx('dcae6c'))                                   # 마운드 주변 흙 원
    for (a, b) in ((0.42, 0.17), (0.17, 0.42), (0.42, 0.42)):                                                                    # 베이스: 흰 윗면 + 회청색 앞면 + 그림자
        x, y = p.G(a, b, 0.3); p.d.polygon([(x - 2, y), (x, y - 1), (x + 2, y), (x, y + 1)], fill=hx('ffffff')); p.d.line([(x - 2, y + 1), (x, y + 2), (x + 2, y + 1)], fill=hx('9aa6c0')); p.d.point((x + 3, y + 2), fill=hx('b8a070'))
    x, y = p.G(0.17, 0.17, 0.3); p.d.polygon([(x - 2, y - 1), (x + 2, y - 1), (x + 2, y), (x, y + 2), (x - 2, y)], fill=hx('ffffff'))     # 홈플레이트(오각형)
    for (da, db) in ((-0.045, 0), (0.045, 0)):                                                                                   # 타석 박스
        pass
    p.poly([(0.20, 0.13, 0.3), (0.24, 0.13, 0.3), (0.24, 0.21, 0.3), (0.20, 0.21, 0.3)], None, None) if False else None
    p.line((0.20, 0.13, 0.3), (0.24, 0.13, 0.3), hx('f4f4f4')); p.line((0.24, 0.13, 0.3), (0.24, 0.21, 0.3), hx('f4f4f4')); p.line((0.20, 0.21, 0.3), (0.24, 0.21, 0.3), hx('f4f4f4'))
    p.line((0.13, 0.20, 0.3), (0.13, 0.24, 0.3), hx('f4f4f4')); p.line((0.13, 0.24, 0.3), (0.21, 0.24, 0.3), hx('f4f4f4')); p.line((0.21, 0.20, 0.3), (0.21, 0.24, 0.3), hx('f4f4f4'))
    # 외야 펜스(녹색 패드 + 노란 윗선) — 앞쪽 두 변
    for (pts, c) in (([(0.88, B, 0), (0.88, 0.88, 0), (0.88, 0.88, 2.6), (0.88, B, 2.6)], hx('2a8a4a')), ([(A, 0.88, 0), (0.88, 0.88, 0), (0.88, 0.88, 2.6), (A, 0.88, 2.6)], hx('1f7a3e'))):
        p.poly(pts, c, None)
    p.line((0.88, B, 2.6), (0.88, 0.88, 2.6), hx('f4e44a')); p.line((A, 0.88, 2.6), (0.88, 0.88, 2.6), hx('d8c838'))
    # 외야 관중석(앞 두 변): 5단 계단식 — 안쪽 낮은 단에서 바깥 높은 단으로, 좌석면(체크) + 앞면
    for k in range(5):
        z = 2.6 + 1.25 * k; w = 0.022; c1, c2 = SEAT_O[k % 3]
        a0 = 0.89 + w * k
        p.poly([(a0, B, z - 1.25), (a0, 0.90, z - 1.25), (a0, 0.90, z), (a0, B, z)], shade(c1, 0.6), None)
        checker(p, [(a0, B, z), (a0 + w, B, z), (a0 + w, 0.90, z), (a0, 0.90, z)], shade(c1, 0.9), shade(c2, 0.9), 0)
        b0 = 0.89 + w * k
        p.poly([(A, b0, z - 1.25), (0.90, b0, z - 1.25), (0.90, b0, z), (A, b0, z)], shade(c1, 0.8), None)
        checker(p, [(A, b0, z), (0.90, b0, z), (0.90, b0 + w, z), (A, b0 + w, z)], c1, c2, 1)
        for _ in range(7):
            t = rr.uniform(0.18, 0.88); col = rr.choice((hx('f0c8a0'), hx('e84b4b'), hx('3a3a4a'), hx('fff0a0'), hx('f4a8c0')))
            x, y = p.G(a0 + 0.006, t, z + 0.6); p.d.point((x, y), fill=shade(col, 0.85)); x, y = p.G(t, b0 + 0.006, z + 0.6); p.d.point((x, y), fill=col)
    # 뒤 관중석 두 변: 9단 계단식(파랑·흰·파랑 줄) + 관중 점
    TI = 9; step = 0.013
    SEAT = [(hx('3a6cc8'), hx('2a54a8')), (hx('3a6cc8'), hx('2a54a8')), (hx('e8ecf8'), hx('c8d0e8'))]
    for i in range(TI - 1, -1, -1):
        z = 1.2 + i * 1.15; c1, c2 = SEAT[i % 3]
        b0 = B - 0.01 - step * (i + 1); b1 = b0 + step                                                                         # 왼쪽 뒤 관중석(a 방향)
        p.poly([(A - 0.04, b1, z - 1.15), (0.99, b1, z - 1.15), (0.99, b1, z), (A - 0.04, b1, z)], shade(c1, 0.72), None)          # 단 앞면(어둡게)
        checker(p, [(A - 0.04, b0, z), (0.99, b0, z), (0.99, b1, z), (A - 0.04, b1, z)], c1, c2, 0)                              # 좌석면
        a0 = A - 0.01 - step * (i + 1); a1 = a0 + step                                                                         # 오른쪽 뒤 관중석(b 방향)
        p.poly([(a1, A - 0.04, z - 1.15), (a1, 0.99, z - 1.15), (a1, 0.99, z), (a1, A - 0.04, z)], shade(c1, 0.62), None)
        checker(p, [(a0, A - 0.04, z), (a1, A - 0.04, z), (a1, 0.99, z), (a0, 0.99, z)], shade(c1, 0.85), shade(c2, 0.85), 1)
        for _ in range(9):                                                                                                     # 관중(머리 색 점)
            t = rr.uniform(0.18, 0.98); col = rr.choice((hx('f0c8a0'), hx('e84b4b'), hx('3a3a4a'), hx('fff0a0'), hx('f4a8c0'), hx('2c2c38')))
            x, y = p.G(t, b0 + 0.006, z + 0.6); p.d.point((x, y), fill=col)
            x, y = p.G(a0 + 0.006, t, z + 0.6); p.d.point((x, y), fill=shade(col, 0.8))
    # 폴대: 양쪽 파울 라인 끝에 노랗고 긴 기둥(왼쪽 밝은 면 + 오른쪽 어두운 면 + 흰 그물 줄)
    for (a, b) in ((0.88, B), (A, 0.88)):
        x, y = p.G(a, b, 0)
        for yy in range(y - 24, y + 1): p.d.point((x, yy), fill=hx('f8e84a')); p.d.point((x + 1, yy), fill=hx('c8b020'))
        for yy in range(y - 24, y - 2, 3): p.d.point((x - 1, yy), fill=hx('ffffff')); p.d.point((x + 2, yy), fill=hx('ffffff'))
        p.d.rectangle([x - 1, y - 26, x + 2, y - 24], fill=hx('f8e84a'))
    # 전광판(뒤 중앙) + 지붕 캐노피 + 조명탑 + 더그아웃
    box(p, 0.32, 0.62, 0.0, 0.05, 11, 15, hx('222a48'))
    for k in range(5): p.rect_wall(0.35 + 0.05 * k, 0.375 + 0.05 * k, 0.05, 12, 14, hx('7af0a8') if k % 2 == 0 else hx('ffd84a'), None)
    p.poly([(0.0, 0.0, 12), (0.99, 0.0, 12), (0.99, 0.05, 12), (0.0, 0.05, 12)], hx('f4f6fc'), None); p.poly([(0.0, 0.0, 12), (0.05, 0.0, 12), (0.05, 0.99, 12), (0.0, 0.99, 12)], hx('d8dcea'), None)
    box(p, 0.30, 0.40, 0.88, 0.93, 0, 2.4, hx('2a4aa8')); box(p, 0.88, 0.93, 0.30, 0.40, 0, 2.4, hx('2a4aa8'))
    for (a, b) in ((0.02, 0.02), (0.97, 0.02), (0.02, 0.97)):
        p.line((a, b, 0), (a, b, 19), hx('9aa2b8')); box(p, a - 0.03, a + 0.03, b - 0.02, b + 0.02, 19, 20.5, hx('f4f0c0'))
    return finish(p)

# ---------------------------------------------------------------- 롯데시네마 (b19)
def cinema():
    p = Px(19); ground(p, 'cfcce0', [(0.05, 0.95, 0.66, 0.96, 'b8b4d0')])
    box(p, 0.10, 0.84, 0.16, 0.56, 0, 12, hx('f2f2f6'))
    wins(p, 'f', 0.56, 0.14, 0.80, 2, 8, 6, 1, pad=0.15)
    hip_roof(p, 0.10, 0.84, 0.16, 0.56, 12, 4, hx('7a98b8'), hx('5e7e9e'))
    p.rect_wall(0.10, 0.84, 0.56, 9.5, 11.4, hx('d82a2a'), None); p.rect_wall(0.10, 0.84, 0.56, 8.6, 9.5, hx('b08050'), None)       # 빨간 간판 띠 + 구리 띠
    box(p, 0.30, 0.52, 0.56, 0.68, 0, 3, hx('e8e8f0'))
    box(p, 0.84, 0.96, 0.22, 0.44, 0, 6, hx('c4cad8'))
    for (a, b) in ((0.08, 0.84), (0.60, 0.88)):                                                                                # 주차 차량
        box(p, a, a + 0.10, b, b + 0.05, 0, 2, hx('d83a3a'))
    shadow_poly(p, [(0.84, 0.14, 0), (0.98, 0.14, 0), (0.98, 0.62, 0), (0.90, 0.56, 0)], 0.78)
    return finish(p)

# ---------------------------------------------------------------- 익산도서관 (b20)
def library():
    p = Px(20); ground(p, '9cc27a', [(0.30, 0.56, 0.56, 1.0, 'd8d0b8'), (0.05, 0.95, 0.80, 0.98, 'cfc8b0')])
    box(p, 0.10, 0.86, 0.18, 0.56, 0, 11, hx('e8d6a8'))
    wins(p, 'f', 0.56, 0.14, 0.82, 2.5, 9, 5, 1, pad=0.2)
    for a in (0.34, 0.40, 0.46, 0.52, 0.58):
        p.rect_wall(a, a + 0.018, 0.62, 0, 11, hx('fff8e0'), None); p.rect_wall(a + 0.018, a + 0.03, 0.62, 0, 11, hx('c4b080'), None)   # 정면 기둥
    p.poly([(0.30, 0.62, 11), (0.64, 0.62, 11), (0.47, 0.62, 15)], hx('f4eac8'), None)
    hip_roof(p, 0.10, 0.86, 0.18, 0.56, 11, 3, hx('8a5a4a'), hx('6e4638'))
    box(p, 0.30, 0.64, 0.56, 0.62, 0, 1, hx('d8d0b8'))
    shadow_poly(p, [(0.86, 0.16, 0), (0.98, 0.16, 0), (0.98, 0.66, 0), (0.90, 0.58, 0)], 0.78)
    return finish(p)

# ---------------------------------------------------------------- 나바위성당 (b13)
def lawn(p):
    """잔디 질감: 연/진 초록 잎덩이 점무늬 + 풀포기 + 작은 들꽃(고정 난수)."""
    import random; r = random.Random(13)
    class _D:                                                                                                                   # 풀색 바닥 위에만 찍는다(자갈길은 보존)
        def point(self, xy, fill):
            px = p.im.getpixel(xy) if 0 <= xy[0] < 96 and 0 <= xy[1] < p.Hc else None
            if px and px[3] and px[1] > px[0] + 25: p.d.point(xy, fill=fill)
    D = _D()
    tones = [hx('b4e08a'), hx('a2d478'), hx('7cb85a'), hx('68a44a'), hx('589040')]
    for _ in range(520):
        a, b = r.uniform(0.02, 0.98), r.uniform(0.02, 0.98); x, y = p.G(a, b, 0); x, y = int(x), int(y)
        t = r.random()
        if t < 0.45: D.point((x, y), fill=tones[0]); D.point((x + 1, y), fill=tones[1])
        elif t < 0.8: D.point((x, y), fill=tones[3]); D.point((x, y - 1), fill=tones[2]); D.point((x + 1, y), fill=tones[4])
        else: D.point((x, y), fill=tones[2]); D.point((x - 1, y), fill=tones[3])
    for _ in range(26):
        a, b = r.uniform(0.04, 0.96), r.uniform(0.04, 0.96); x, y = p.G(a, b, 0); D.point((int(x), int(y)), fill=r.choice([hx('fff4a0'), hx('ffffff'), hx('ffb0c0')]))

def church():
    """나바위성당(사진 기준): 넓은 한옥식 본당(붉은 벽돌 벽 + 목조 기둥 열 + 회색 기와 이중 처마)의 정면 가운데에 서양식 벽돌 종탑이 튀어나온다 — 첨탑 좌우로 한옥 본당이 드러난다.
    종탑 출입구는 우하단(오른쪽 면)을 향하고, 시계(둥근 창)는 두 면 모두 면 중앙에 있다. 마당은 잔디 + 자갈길."""
    p = Px(13); p.zs = 1.6; ground(p, '8cc46c', [(0.96, 1.0, 0.10, 0.44, 'd8c8a0'), (0.60, 1.0, 0.52, 0.66, 'cfc8b0'), (0.04, 0.60, 0.84, 0.97, 'cfc8b0')])
    lawn(p)
    BR = hx('cc6e56'); BRL = hx('dc826a'); GR = hx('8a847a'); WOOD = hx('9a4a30'); ROOF = (hx('5c5c68'), hx('484854')); SPIRE = (hx('564a44'), hx('3a302c'))
    HA0, HA1, HB0, HB1 = 0.04, 0.80, 0.06, 0.50                                                                                 # 본당
    box(p, HA0, HA1, HB0, HB1, 0, 3, GR, tex='checker'); box(p, HA0, HA1, HB0, HB1, 3, 10, BR, tex='checker')
    for i in range(7): p.line((HA0, HB1, 3.6 + i), (HA1, HB1, 3.6 + i), shade(BRL, 0.96) if i % 2 == 0 else shade(BR, 0.9))
    cols = [0.07 + 0.68 * i / 11 for i in range(12)]                                    # 목조 기둥 열: 종탑 좌우에만 보인다
    for a in cols:
        p.rect_wall(a, a + 0.014, HB1, 0, 9, WOOD, None); p.rect_wall(a + 0.014, a + 0.024, HB1, 0, 9, shade(WOOD, 0.62), None)
        p.rect_wall(a + 0.03, a + 0.052, HB1, 3.5, 8, hx('6a8aa8'), None)
    p.rect_wall(HA0, HA1, HB1, 9, 10, shade(WOOD, 0.8), None)
    hip_roof(p, HA0 - 0.02, HA1 + 0.02, HB0 - 0.02, HB1 + 0.02, 10, 2, ROOF[0], ROOF[1], over=0.04, inset=0.04)               # 지붕은 한 벌: 아래 차양 + 위 지붕, 같은 회색 기와
    hip_roof(p, 0.12, 0.76, 0.18, 0.38, 12, 6, ROOF[0], ROOF[1], over=0.03, inset=0.10)
    p.poly([(0.12, 0.38, 12), (0.76, 0.38, 12), (0.76, 0.38, 13.2), (0.12, 0.38, 13.2)], shade(WOOD, 0.9), None)
    # 종탑(정면 가운데, 본당보다 앞으로 튀어나옴)
    TA0, TA1, TB0, TB1 = 0.76, 0.96, 0.17, 0.37
    box(p, TA0, TA1, TB0, TB1, 0, 16, BR, tex='checker')
    for (a, bb) in ((TA0, TB1), (TA1 - 0.015, TB1)): p.rect_wall(a, a + 0.015, bb, 0, 16, GR, None)                              # 왼쪽 앞 면 모서리 회색 띠
    for b in (TB0, TB1 - 0.014): p.rect_side(TA1, b, b + 0.014, 0, 16, shade(GR, 0.7), None)                                   # 오른쪽 면 모서리 띠
    p.rect_wall(TA0, TA1, TB1, 0, 3, GR, None); p.rect_side(TA1, TB0, TB1, 0, 3, shade(GR, 0.7), None)
    for z in (8, 12): p.line((TA0, TB1, z), (TA1, TB1, z), hx('d8d0c0')); p.line((TA1, TB0, z), (TA1, TB1, z), hx('b0a898'))
    cb = (TB0 + TB1) / 2; ca = (TA0 + TA1) / 2
    import math
    def clock(face, c, zc, ra=0.047, rz=2.6):
        """시계창: 벽면(아이소 평행사변형 면) 위의 원이므로 면 좌표(a 또는 b, z)로 원을 그려 투영 — 면 방향에 맞게 기울어진 타원이 된다."""
        P = (lambda u, z: p.G(u, TB1, z)) if face == 'L' else (lambda u, z: p.G(TA1, u, z))
        ring = [P(c + ra * math.cos(t * math.pi / 18), zc + rz * math.sin(t * math.pi / 18)) for t in range(36)]
        inner = [P(c + (ra - 0.011) * math.cos(t * math.pi / 18), zc + (rz - 0.9) * math.sin(t * math.pi / 18)) for t in range(36)]
        p.d.polygon(ring, fill=hx('5a5460')); p.d.polygon(inner, fill=hx('f4f0e4'))
        o = P(c, zc); p.d.line([o, P(c, zc + rz * 0.6)], fill=hx('4a4a56')); p.d.line([o, P(c + ra * 0.55, zc)], fill=hx('4a4a56'))
        for t in (0, 9, 18, 27): q = P(c + (ra - 0.016) * math.cos(t * math.pi / 18), zc + (rz - 1.1) * math.sin(t * math.pi / 18)); p.d.point(q, fill=hx('8a847a'))
    clock('L', ca, 8.2); clock('R', cb, 8.2)
    for a in (ca - 0.05, ca + 0.01): p.rect_wall(a, a + 0.04, TB1, 12, 15, hx('3a3236'), None); p.rect_wall(a + 0.012, a + 0.028, TB1, 12.4, 14.6, hx('c8b8a0'), None)
    for b in (cb - 0.05, cb + 0.01): p.rect_side(TA1, b, b + 0.04, 12, 15, hx('2a2428'), None); p.rect_side(TA1, b + 0.012, b + 0.028, 12.4, 14.6, hx('8a7e72'), None)
    p.rect_side(TA1, cb - 0.06, cb + 0.06, 0, 5.5, hx('3a2a22'), None); p.poly([(TA1, cb - 0.075, 5.5), (TA1, cb + 0.075, 5.5), (TA1, cb, 8)], shade(GR, 0.75), None)   # 우하단 출입구(아치)
    p.rect_side(TA1, cb - 0.035, cb + 0.035, 0, 4, hx('5a3a28'), None); p.line((TA1, cb, 0), (TA1, cb, 4), hx('8a6a40'))
    for i in range(3): box(p, TA1 + 0.012 * (i + 1), TA1 + 0.012 * (i + 2), cb - 0.09, cb + 0.09, 0, 1.6 - 0.5 * i, hx('c8c0b0'))
    ap = (ca, cb, 29)                                                                                                           # 뾰족 첨탑 + 십자가
    checker(p, [(TA0 - 0.01, TB1 + 0.01, 16), (TA1 + 0.01, TB1 + 0.01, 16), ap], SPIRE[0], shade(SPIRE[0], 0.88), 0); checker(p, [(TA1 + 0.01, TB1 + 0.01, 16), (TA1 + 0.01, TB0 - 0.01, 16), ap], SPIRE[1], shade(SPIRE[1], 0.88), 1)
    p.line(ap, (ca, cb, 32), hx('3a302c')); p.line((ca - 0.015, cb, 31), (ca + 0.015, cb, 31), hx('3a302c'))
    shadow_poly(p, [(0.94, 0.10, 0), (1.0, 0.10, 0), (1.0, 0.50, 0), (0.94, 0.44, 0)], 0.78)
    return finish(p)

# ---------------------------------------------------------------- 익산 통합 전수관 (b26)
def jeonsu():
    p = Px(26); ground(p, 'b8b4c8', [(0.40, 0.92, 0.50, 0.92, 'd0ccc0')])
    box(p, 0.08, 0.94, 0.10, 0.30, 0, 12, hx('d8dce4')); wins(p, 'f', 0.30, 0.12, 0.90, 2, 10, 8, 1, pad=0.2)
    p.rect_wall(0.08, 0.94, 0.30, 11, 12, hx('3e4250'), None)
    box(p, 0.08, 0.28, 0.30, 0.78, 0, 9, hx('d8dce4')); wins(p, 's', 0.28, 0.34, 0.74, 2, 8, 5, 1, pad=0.2)
    box(p, 0.44, 0.88, 0.52, 0.88, 0, 3, hx('c8c2b0'))                                                                        # 돌 기단
    for a in (0.48, 0.58, 0.72, 0.84):
        for b in (0.55, 0.84): p.line((a, b, 3), (a, b, 11), hx('8a5a34')); p.line((a + 0.01, b, 3), (a + 0.01, b, 11), hx('b07a48'))        # 목조 기둥
    hip_roof(p, 0.44, 0.88, 0.52, 0.88, 11, 6, hx('565a66'), hx('3e424e'), over=0.07)
    shadow_poly(p, [(0.90, 0.50, 0), (0.98, 0.50, 0), (0.98, 0.94, 0), (0.92, 0.90, 0)], 0.78)
    return finish(p)
