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
    stripes(p, [(a0, b1, z0), (a1, b1, z0), (a1, b1, z1), (a0, b1, z1)], L1, L2, tex, bands)
    stripes(p, [(a1, b0, z0), (a1, b1, z0), (a1, b1, z1), (a1, b0, z1)], R1, R2, tex, rb)
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
        box(p, ca - ow, ca + ow, cb - ow, cb + ow, z, z + 1.8, shade(g, 0.9)); z += 1.8
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
    pagoda(p, 0.5, 0.5, 0.20, 5, 9)
    return finish(p)

# ---------------------------------------------------------------- 백제왕궁(왕궁리 오층석탑) (b38)
def baekje():
    p = Px(38); ground(p, '9cc27a', [(0.08, 0.92, 0.70, 0.90, 'cfc8b2'), (0.40, 0.60, 0.30, 0.70, 'd8d2c0')])
    for a in (0.10, 0.90):                                                                                                    # 궁궐 담장(흙담 + 기와 얹음)
        box(p, a - 0.02, a + 0.02, 0.10, 0.60, 0, 4, hx('c9a878')); box(p, a - 0.035, a + 0.035, 0.10, 0.60, 4, 5.6, hx('5a6a66'))
    box(p, 0.10, 0.90, 0.07, 0.11, 0, 4, hx('c9a878')); box(p, 0.09, 0.91, 0.06, 0.12, 4, 5.6, hx('5a6a66'))
    shadow_poly(p, [(0.56, 0.46, 0), (0.88, 0.42, 0), (0.96, 0.62, 0), (0.62, 0.68, 0)], 0.78)
    pagoda(p, 0.5, 0.45, 0.17, 5, 8)
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
def baseball():
    p = Px(33); ground(p, '7ab858', [(0.06, 0.94, 0.06, 0.94, '5ea446')])
    checker(p, [(0.14, 0.14, 0), (0.86, 0.14, 0), (0.86, 0.86, 0), (0.14, 0.86, 0)], hx('68b04c'), hx('5ea446'), 0)
    p.poly([(0.15, 0.15, 0), (0.60, 0.15, 0), (0.15, 0.60, 0)], hx('d8a868'), None)                                             # 내야 흙
    checker(p, [(0.15, 0.15, 0), (0.58, 0.15, 0), (0.15, 0.58, 0)], hx('dcae6c'), hx('c89a5c'), 0)
    p.line((0.15, 0.15, 0.2), (0.86, 0.15, 0.2), hx('ffffff')); p.line((0.15, 0.15, 0.2), (0.15, 0.86, 0.2), hx('ffffff'))
    for (a, b) in ((0.40, 0.15), (0.15, 0.40), (0.40, 0.40)): p.poly([(a, b, 0.3), (a + 0.03, b, 0.3), (a + 0.03, b + 0.03, 0.3), (a, b + 0.03, 0.3)], hx('ffffff'), None)
    # 관중석: 뒤 두 변에 계단식 좌석(파랑/흰 체크)
    for i in range(5):
        z0 = 1 + i * 1.6; off = 0.02 + i * 0.012
        for (a0, a1, b0, b1) in ((0.02 + off, 0.98, 0.02, 0.02 + off + 0.05), (0.02, 0.02 + off + 0.05, 0.02 + off, 0.98)):
            pts = [(a0, b0, z0), (a1, b0, z0), (a1, b1, z0), (a0, b1, z0)]
            checker(p, pts, hx('3a6cc8') if i % 2 == 0 else hx('e8ecf8'), hx('2a54a8') if i % 2 == 0 else hx('c8d0e8'), 0)
            front = [(a0, b1, z0 - 1.4), (a1, b1, z0 - 1.4), (a1, b1, z0), (a0, b1, z0)] if a1 > a0 + 0.2 else [(a1, b0, z0 - 1.4), (a1, b1, z0 - 1.4), (a1, b1, z0), (a1, b0, z0)]
            p.poly(front, shade(hx('3a6cc8'), 0.7) if i % 2 == 0 else hx('b8c0d8'), None)
    box(p, 0.10, 0.40, 0.00, 0.06, 6, 9, hx('2a3a68'))                                                                          # 전광판
    for (a, b) in ((0.04, 0.04), (0.96, 0.04), (0.04, 0.96)):                                                                  # 조명탑
        p.line((a, b, 0), (a, b, 17), hx('9aa2b8')); box(p, a - 0.03, a + 0.03, b - 0.02, b + 0.02, 17, 19, hx('f4f0c0'))
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
def church():
    p = Px(13); ground(p, '9cc27a', [(0.30, 0.58, 0.60, 1.0, 'd8d0b8')])
    box(p, 0.16, 0.86, 0.10, 0.34, 0, 9, hx('7a5a3a')); hip_roof(p, 0.16, 0.86, 0.10, 0.34, 9, 5, hx('4a4a58'), hx('3a3a46'), over=0.05)          # 뒤 한옥 예배당
    box(p, 0.30, 0.62, 0.38, 0.62, 0, 14, hx('c8c4b8'))                                                                       # 서양식 정면 종탑 몸체
    p.poly([(0.30, 0.62, 14), (0.62, 0.62, 14), (0.46, 0.62, 21)], hx('8a8a98'), None); p.poly([(0.62, 0.62, 14), (0.62, 0.38, 14), (0.46, 0.50, 21)], hx('5e5e70'), None)
    p.line((0.46, 0.62, 21), (0.46, 0.62, 25), hx('f0e8c0')); p.line((0.43, 0.62, 23), (0.49, 0.62, 23), hx('f0e8c0'))                                    # 십자가
    p.rect_wall(0.40, 0.52, 0.62, 0, 7, hx('6a4a2a'), None); p.poly([(0.38, 0.62, 7), (0.54, 0.62, 7), (0.46, 0.62, 10)], hx('d8d4c8'), None)       # 입구 아치 문
    wins(p, 'f', 0.62, 0.32, 0.60, 8, 12.5, 2, 1, gl=('c8d8f0', '98b4dc', '6e90c0', '4a6e9e'), pad=0.2)
    box(p, 0.16, 0.30, 0.40, 0.58, 0, 6, hx('c8c4b8')); box(p, 0.62, 0.78, 0.40, 0.58, 0, 6, hx('c8c4b8'))
    shadow_poly(p, [(0.78, 0.38, 0), (0.98, 0.38, 0), (0.98, 0.72, 0), (0.80, 0.62, 0)], 0.78)
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
