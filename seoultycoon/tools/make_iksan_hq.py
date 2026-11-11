"""시청 수준 품질 재작업: 창 하나하나(프레임·유리 그라데이션·반사·점등), 기둥 핀, 층 띠, 접지 그림자, 큰 건물 규모로
호텔(b4) · 롯데시네마(b19) · 테슬라 본사(b15) · KTX익산역(b22) · 예술의전당(b42) · 하림(b44) · 코스트코(b3) · 야구장(b33) 를 다시 그린다.
make_iksan_buildings / make_iksan_more 보다 나중에 실행해 같은 파일을 덮어쓴다 → work/pak_mod/{b4,b19,b15,b22,b42,b44,b3,b33}.png
사용: python3 -I tools/make_iksan_hq.py [미리보기.png]"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, lerp, W, S, out, FONT_K, FONT_B

def shade(c, t): return lerp(c, (0, 0, 0), t) if t > 0 else c

def facade(s, face, fixed, lo, hi, z0, z1, floors, bays, pal, sh=0.0, win_h=0.58, fin_w=0.30, seed=1):
    """면 하나에 시청 같은 창 파사드: 층 띠(스팬드럴) + 세로 핀 + 창(프레임·그라데이션·반사·가끔 점등)."""
    G = s.G; rnd = random.Random(seed)
    P = (lambda u, z: G(u, fixed, z)) if face == 'F' else (lambda u, z: G(fixed, u, z))
    s.grad([P(lo, z0), P(hi, z0), P(hi, z1), P(lo, z1)], shade(pal['span'], sh * 0.6), shade(pal['span2'], sh), (0.2, 1), 3)
    for i in range(bays):
        u0 = lo + (hi - lo) * i / bays; u1 = lo + (hi - lo) * (i + 1) / bays; uw = u1 - u0
        wa, wb = u0 + uw * 0.08, u0 + uw * (1 - fin_w)
        for j in range(floors):
            zf0 = z0 + (z1 - z0) * j / floors; zf1 = z0 + (z1 - z0) * (j + 1) / floors; fh = zf1 - zf0
            wz0 = zf0 + fh * 0.16; wz1 = wz0 + fh * win_h
            s.flat([P(wa, wz0), P(wb, wz0), P(wb, wz1), P(wa, wz1)], shade(pal['frame'], sh * 0.5))
            iu = (wb - wa) * 0.10; iz = (wz1 - wz0) * 0.10
            warm = rnd.random() < pal.get('lit', 0.0)
            top = hx('ffe8a8') if warm else pal['sky']; bot = hx('c89a48') if warm else pal['deep']
            s.grad([P(wa + iu, wz0 + iz), P(wb - iu, wz0 + iz), P(wb - iu, wz1 - iz), P(wa + iu, wz1 - iz)], shade(top, sh * 0.8), shade(bot, sh * 0.8), (0.15, 1), 2)
            if not warm: s.line([P(wa + (wb - wa) * 0.30, wz1 - iz * 1.5), P(wa + (wb - wa) * 0.62, wz0 + iz * 1.5)], hx('ffffff', 110), 2)
            s.line([P(wa, wz0), P(wb, wz0)], shade(pal['sill'], sh * 0.4), 3)
        s.flat([P(wb, z0), P(u1, z0), P(u1, z1), P(wb, z1)], shade(pal['fin'], sh)); s.line([P(u1, z0), P(u1, z1)], shade(pal['fin2'], sh), 2)
    for j in range(floors + 1):
        z = z0 + (z1 - z0) * j / floors; s.line([P(lo, z), P(hi, z)], shade(pal['span2'], sh * 0.9 + 0.12), 2)

CREAM = dict(sky=hx('a8cdea'), deep=hx('3a5a88'), frame=hx('5a4a38'), fin=hx('efe6d0'), fin2=hx('b8a888'), span=hx('e8dcc0'), span2=hx('c4b490'), sill=hx('f4ecd8'), lit=0.08)
STEEL = dict(sky=hx('9cc4e8'), deep=hx('2c4a78'), frame=hx('3a4658'), fin=hx('e2e6ee'), fin2=hx('aab2c2'), span=hx('d0d6e2'), span2=hx('aab2c4'), sill=hx('f0f4fa'), lit=0.05)
BLACK = dict(sky=hx('6a8ac0'), deep=hx('0e1428'), frame=hx('0a0e1a'), fin=hx('2a3246'), fin2=hx('141a2c'), span=hx('1a2236'), span2=hx('0c1222'), sill=hx('4a5878'), lit=0.0)
NAVY = dict(sky=hx('5a86c8'), deep=hx('0e2048'), frame=hx('0c1a3a'), fin=hx('2a4676'), fin2=hx('12264c'), span=hx('1c3866'), span2=hx('0c1a3a'), sill=hx('5a78a8'), lit=0.0)
GREENG = dict(sky=hx('b4e0e0'), deep=hx('2a6a78'), frame=hx('e8ecea'), fin=hx('f6f4ee'), fin2=hx('c8c4b4'), span=hx('f2f0e6'), span2=hx('cac6b6'), sill=hx('ffffff'), lit=0.0)

def pad_shadow(s, a0, a1, b0, b1, w=0.10):
    G = s.G
    s.shadow([G(a0, b1), G(a1, b1), G(a1 + w, b1 + w * 0.6), G(a0 + w, b1 + w * 0.7)], 105, 10)
    s.shadow([G(a1, b0), G(a1, b1), G(a1 + w, b1 + w * 0.6), G(a1 + w * 0.8, b0 + 0.02)], 85, 9)

def roof_gear(s, a, b, z, w, h, ht, rnd=None):
    G = s.G
    s.grad([G(a, b + h, z), G(a + w, b + h, z), G(a + w, b + h, z + ht), G(a, b + h, z + ht)], hx('b8bece'), hx('8a92a6'), (0.2, 1))
    s.grad([G(a + w, b, z), G(a + w, b + h, z), G(a + w, b + h, z + ht), G(a + w, b, z + ht)], hx('8a92a6'), hx('5a6278'), (0.2, 1))
    s.grad([G(a, b, z + ht), G(a + w, b, z + ht), G(a + w, b + h, z + ht), G(a, b + h, z + ht)], hx('e8ecf4'), hx('c4cadc'), (1, 0.4))

# ======================= 웨스턴라이프 호텔 (b4) =======================
def hotel():
    s = Sc(63, 4); G = s.G; s.ground(); rs = random.Random(3)
    pad_shadow(s, 0.12, 0.76, 0.08, 0.48, 0.12)
    TA0, TA1, TB0, TB1, TH = 0.12, 0.76, 0.06, 0.48, 25
    s.box(TA0, TA1, TB0, TB1, TH, (hx('efe6d0'), hx('c4b598')), (hx('c8bba0'), hx('968a70')), (hx('d4d0c8'), hx('aeaaa0')))
    facade(s, 'F', TB1, TA0 + 0.02, TA1 - 0.01, 4.2, TH - 1.0, 8, 10, CREAM, 0.0, seed=1)
    facade(s, 'R', TA1, TB0 + 0.015, TB1 - 0.01, 4.2, TH - 1.0, 8, 5, CREAM, 0.32, seed=2)
    s.line([G(TA0, TB1, TH), G(TA1, TB1, TH), G(TA1, TB0, TH)], hx('ffffff', 200), 4); s.line([G(TA0, TB1, 0), G(TA0, TB1, TH)], hx('8a7e66'), 5); s.line([G(TA1, TB1, 0), G(TA1, TB1, TH)], hx('4a4234'), 5)
    roof_gear(s, 0.30, 0.16, TH, 0.14, 0.10, 2.6); roof_gear(s, 0.56, 0.24, TH, 0.09, 0.08, 1.8)
    # 로비 날개: 유리 + 금빛 캐노피 + 기둥
    LA0, LA1, LB0, LB1, LH = 0.12, 0.62, 0.48, 0.72, 7.0
    s.box(LA0, LA1, LB0, LB1, LH, (hx('e8d8b4'), hx('c0ac80')), (hx('c8b48a'), hx('9c8860')), (hx('8a7a60'), hx('6c5e48')))
    facade(s, 'F', LB1, LA0 + 0.02, LA1 - 0.02, 0.8, 5.6, 1, 7, GREENG | dict(sky=hx('9cc0e4'), deep=hx('2a3c60'), frame=hx('3a3020'), span=hx('d8c898'), span2=hx('b0a070'), fin=hx('e8d8a8')), 0.0, win_h=0.85, seed=3)
    s.grad([G(0.08, LB1, LH + 0.4), G(0.68, LB1, LH + 0.4), G(0.68, 0.84, LH + 0.4), G(0.08, 0.84, LH + 0.4)], hx('d8b45a'), hx('8a6c2c'), (1, 0.3))
    s.grad([G(0.08, 0.84, LH + 0.4), G(0.68, 0.84, LH + 0.4), G(0.68, 0.84, LH - 0.8), G(0.08, 0.84, LH - 0.8)], hx('f0d078'), hx('9a7a34'), (0, 1))
    for i in range(5): s.line([G(0.12 + 0.13 * i, 0.84, LH - 0.8), G(0.12 + 0.13 * i, 0.84, 0)], hx('f6f0de'), 5)
    s.grad([G(0.30, 0.84, 0), G(0.46, 0.84, 0), G(0.46, 0.84, 5.2), G(0.30, 0.84, 5.2)], hx('24324c'), hx('46587a'), (0, 1))
    # 옥상 간판
    s.grad([G(0.20, TB1, TH), G(0.68, TB1, TH), G(0.68, TB1, TH + 5.4), G(0.20, TB1, TH + 5.4)], hx('2e2418'), hx('120e08'), (0.2, 1))
    s.line([G(0.20, TB1, TH + 5.4), G(0.68, TB1, TH + 5.4)], hx('e6c468'), 4); s.line([G(0.20, TB1, TH), G(0.68, TB1, TH)], hx('e6c468'), 3)
    fw = G(0.66, TB1)[0] - G(0.22, TB1)[0]
    s.text_face('WESTERN LIFE', (G(0.22, TB1)[0] + 4, G(0.22, TB1, TH + 4.6)[1] + 1), 0, 0, fw * 0.90 / 590.0, hx('f4d478'), size=56, shear=0.5)
    s.tree(0.84, 0.78, 0.8); s.tree(0.05, 0.74, 0.7); s.tree(0.90, 0.30, 0.7)
    return s.finish()

# ======================= 롯데시네마 (b19) =======================
def cinema():
    s = Sc(57, 5); G = s.G; s.ground(); rs = random.Random(8)
    A0, A1, B0, B1, HH = 0.07, 0.93, 0.14, 0.66, 14.5
    pad_shadow(s, A0, A1, B0, B1, 0.10)
    s.box(A0, A1, B0, B1, HH, (hx('f8f6ee'), hx('d6d2c4')), (hx('d8d4c6'), hx('a8a498')), (hx('c0c2dc'), hx('9a9cc0')))
    # 1층 상가(어두운 유리 + 흰 기둥), 2~4층 청록 띠창
    facade(s, 'F', B1, A0 + 0.02, A1 - 0.02, 0.2, 4.2, 1, 12, dict(GREENG, sky=hx('7a9ac0'), deep=hx('141c34'), frame=hx('e8e8f0'), span=hx('dcdcd0'), span2=hx('b0b0a4')), 0.0, win_h=0.9, seed=4)
    facade(s, 'F', B1, A0 + 0.03, A1 - 0.03, 4.4, HH - 0.4, 3, 11, GREENG, 0.0, win_h=0.66, fin_w=0.22, seed=5)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.02, 0.2, 4.2, 1, 6, dict(GREENG, sky=hx('6a82a8'), deep=hx('101830'), frame=hx('e8e8f0')), 0.30, win_h=0.9, seed=6)
    facade(s, 'R', A1, B0 + 0.03, B1 - 0.03, 4.4, HH - 0.4, 3, 5, GREENG, 0.30, win_h=0.66, fin_w=0.22, seed=7)
    for t in np.linspace(0, 1, 70):                                                                 # 구리색 메시 곡선 띠
        a = A0 + 0.02 + (A1 - A0 - 0.04) * t; z = 2.0 + 7.6 * (t ** 1.1); w = 1.1 + 0.9 * math.sin(t * math.pi)
        s.flat([G(a, B1 + 0.004, z), G(a + 0.016, B1 + 0.004, z), G(a + 0.016, B1 + 0.004, z + w), G(a, B1 + 0.004, z + w)], hx('c88a6a') if int(t * 70) % 2 else hx('a86a50'))
    RZ = 3.8
    s.grad([G(A0 - 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH + RZ), G(A0 - 0.02, B1 + 0.02, HH + RZ)], hx('b4b6dc'), hx('7a7cae'), (0.2, 1))
    s.grad([G(A1 + 0.02, B0, HH), G(A1 + 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH + RZ), G(A1 + 0.02, B0, HH + RZ)], hx('8a8cb8'), hx('5a5c88'), (0.2, 1))
    s.grad([G(A0 - 0.02, B0, HH + RZ), G(A1 + 0.02, B0, HH + RZ), G(A1 + 0.02, B1 + 0.02, HH + RZ), G(A0 - 0.02, B1 + 0.02, HH + RZ)], hx('d2d4ec'), hx('a8aad0'), (1, 0.4))
    for i in range(0, 30):
        for j in range(0, 3):
            a = A0 - 0.01 + 0.03 * i + (0.015 if j % 2 else 0); z = HH + 0.5 + j * 1.15
            if a < A1: s.flat([G(a, B1 + 0.02, z + 0.4), G(a + 0.015, B1 + 0.02, z), G(a + 0.03, B1 + 0.02, z + 0.4), G(a + 0.015, B1 + 0.02, z + 0.8)], hx('5a5c92', 200))
    fw = G(0.88, B1)[0] - G(0.10, B1)[0]
    s.text_face('LOTTE CINEMA', (G(0.12, B1)[0], G(0.12, B1, HH + 3.3)[1] + 1), 0, 0, fw * 0.76 / 560.0, hx('d6203a'), size=60, bold_outline=hx('a0121e'), shear=0.5)
    for (a, cc) in ((0.18, '#e8e8f0'), (0.46, '#2a2a34'), (0.70, '#8a98b0'), (0.84, '#c84a3a')):
        c = hx(cc[1:]); b = 0.86
        s.grad([G(a, b, 0.3), G(a + 0.06, b, 0.3), G(a + 0.06, b, 2.8), G(a, b, 2.8)], lerp(c, (255, 255, 255), 0.25), c, (0.3, 1)); s.grad([G(a, b - 0.05, 2.8), G(a + 0.06, b - 0.05, 2.8), G(a + 0.06, b, 2.8), G(a, b, 2.8)], lerp(c, (255, 255, 255), 0.4), c, (1, 0.4))
        s.grad([G(a + 0.06, b - 0.05, 0.3), G(a + 0.06, b, 0.3), G(a + 0.06, b, 2.8), G(a + 0.06, b - 0.05, 2.8)], lerp(c, (0, 0, 0), 0.3), lerp(c, (0, 0, 0), 0.5), (0.3, 1))
    s.tree(0.96, 0.34, 0.8); s.tree(0.03, 0.80, 0.8)
    return s.finish()

# ======================= 테슬라 본사 (b15) =======================
def tesla():
    s = Sc(60, 6); G = s.G; s.ground(); rs = random.Random(5)
    pad_shadow(s, 0.10, 0.92, 0.08, 0.64, 0.12)
    # 흰 공장동(뒤쪽) + 태양광 지붕
    s.box(0.34, 0.94, 0.06, 0.34, 10.5, (hx('fafafc'), hx('d4d6e0')), (hx('d8dae4'), hx('aeb0c0')), (hx('2a3a62'), hx('1c284a')))
    for i in range(1, 18): s.line([G(0.34 + 0.6 * i / 18, 0.06, 10.5), G(0.34 + 0.6 * i / 18, 0.34, 10.5)], hx('7a90c8', 150), 2)
    for j in range(1, 5): s.line([G(0.34, 0.06 + 0.28 * j / 5, 10.5), G(0.94, 0.06 + 0.28 * j / 5, 10.5)], hx('7a90c8', 150), 2)
    facade(s, 'F', 0.34, 0.37, 0.92, 1.0, 9.6, 2, 12, dict(STEEL, frame=hx('2a3650'), span=hx('f2f4f8'), span2=hx('c8ccd8'), fin=hx('eceef4')), 0.0, win_h=0.5, seed=8)
    facade(s, 'R', 0.94, 0.08, 0.32, 1.0, 9.6, 2, 5, dict(STEEL, span=hx('d8dae4'), span2=hx('aeb0c0')), 0.34, win_h=0.5, seed=9)
    # 검은 유리 본사 타워(앞쪽) + 빨간 T 로고 판
    TA0, TA1, TB0, TB1, TH = 0.10, 0.50, 0.30, 0.64, 22
    s.box(TA0, TA1, TB0, TB1, TH, (hx('2c3446'), hx('0e121c')), (hx('1c2230'), hx('06080e')), (hx('3a4458'), hx('2a3244')))
    facade(s, 'F', TB1, TA0 + 0.02, TA1 - 0.01, 1.5, TH - 0.8, 6, 8, BLACK, 0.0, win_h=0.62, fin_w=0.24, seed=10)
    facade(s, 'R', TA1, TB0 + 0.015, TB1 - 0.01, 1.5, TH - 0.8, 6, 5, BLACK, 0.3, win_h=0.62, fin_w=0.24, seed=11)
    s.line([G(TA0, TB1, TH), G(TA1, TB1, TH), G(TA1, TB0, TH)], hx('e82127'), 6)
    fw = G(0.50, TB1)[0] - G(0.10, TB1)[0]
    s.grad([G(0.16, TB1, TH + 0.8), G(0.46, TB1, TH + 0.8), G(0.46, TB1, TH + 6.6), G(0.16, TB1, TH + 6.6)], hx('f6f6fa'), hx('d0d2dc'), (0.2, 1))
    s.text_face('TESLA', (G(0.18, TB1)[0], G(0.18, TB1, TH + 5.8)[1] + 1), 0, 0, fw * 0.60 / 200.0, hx('e82127'), size=62, shear=0.5)
    for i in range(5):                                                                                   # 슈퍼차저 기둥
        x, y = G(0.60 + 0.07 * i, 0.76); s.grad([(x - 3, y - 20), (x + 3, y - 20), (x + 3, y), (x - 3, y)], hx('f6f6fa'), hx('b0b2c0'), (0.3, 1)); ImageDraw.Draw(s.im).rectangle([x - 3, y - 20, x + 3, y - 14], fill=hx('e82127'))
    a, b = 0.62, 0.86
    s.grad([G(a, b, 0.5), G(a + 0.13, b, 0.5), G(a + 0.13, b, 3.8), G(a, b, 3.8)], hx('ff5a60'), hx('b01018'), (0.3, 1)); s.grad([G(a + 0.13, b - 0.07, 0.5), G(a + 0.13, b, 0.5), G(a + 0.13, b, 3.8), G(a + 0.13, b - 0.07, 3.8)], hx('a0141c'), hx('6a0a10'), (0.3, 1))
    s.grad([G(a + 0.02, b, 3.8), G(a + 0.11, b, 3.8), G(a + 0.10, b, 5.6), G(a + 0.04, b, 5.6)], hx('c8dcf4'), hx('5a78a8'), (0.2, 1))
    s.tree(0.94, 0.62, 0.7); s.tree(0.04, 0.80, 0.7)
    return s.finish()

# ======================= KTX익산역 (b22) =======================
def station(H=67):
    s = Sc(H, 7); G = s.G; s.ground(); rs = random.Random(9)
    for i in range(1, 18): s.line([G(i / 18, 0.46), G(i / 18, 1)], hx('b4b4d4', 110), 2)
    s.grad([G(0.03, 0.70), G(0.44, 0.70), G(0.44, 0.97), G(0.03, 0.97)], hx('8cc04a'), hx('5a9a30'), (0.4, 1), 4)
    pad_shadow(s, 0.06, 0.96, 0.12, 0.56, 0.10)
    # 왼쪽 흰 날개 / 오른쪽 파란 날개(KORAIL)
    s.box(0.03, 0.28, 0.24, 0.54, 14, (hx('f6f6fa'), hx('d2d4e0')), (hx('d4d6e4'), hx('a8abc0')), (hx('eceef8'), hx('c8cade')))
    for k in range(1, 8): s.line([G(0.03 + 0.25 * k / 8, 0.54, 0), G(0.03 + 0.25 * k / 8, 0.54, 14)], hx('b8bacc', 100), 2)
    facade(s, 'F', 0.54, 0.05, 0.26, 6.0, 12.6, 2, 3, dict(STEEL, span=hx('f4f4fa'), span2=hx('d0d2e0')), 0.0, win_h=0.55, seed=12)
    s.box(0.70, 0.97, 0.26, 0.56, 10, (hx('4a78d4'), hx('2a4ea4')), (hx('2e56b0'), hx('1a3a84')), (hx('7a9ae0'), hx('4a6cc4')))
    facade(s, 'F', 0.56, 0.72, 0.95, 0.8, 4.6, 1, 5, dict(GREENG, sky=hx('a0d8c8'), deep=hx('2a6a68'), frame=hx('f4f4fc'), span=hx('4a78d4'), span2=hx('2a4ea4')), 0.0, win_h=0.9, seed=13)
    s.line([G(0.70, 0.56, 7.8), G(0.97, 0.56, 7.8)], hx('f4f4fc'), 5)
    s.text_face('KORAIL', (G(0.76, 0.56)[0], G(0.76, 0.56, 7.6)[1] + 2), 0, 0, 0.12, hx('ffffff'), size=40, shear=0.5)
    # 중앙 남색 유리 박스
    CA0, CA1, CB0, CB1, CH = 0.28, 0.70, 0.14, 0.50, 21
    s.box(CA0, CA1, CB0, CB1, CH, (hx('2c4a82'), hx('0c1a3c')), (hx('1c3262'), hx('08122c')), (hx('3a5a92'), hx('1c3262')))
    facade(s, 'F', CB1, CA0 + 0.015, CA1 - 0.01, 6.8, CH - 0.4, 4, 11, NAVY, 0.0, win_h=0.78, fin_w=0.18, seed=14)
    facade(s, 'R', CA1, CB0 + 0.015, CB1 - 0.01, 6.8, CH - 0.4, 4, 5, NAVY, 0.3, win_h=0.78, fin_w=0.18, seed=15)
    fw = G(0.68, CB1)[0] - G(0.30, CB1)[0]
    s.text_face('익산역', (G(0.34, CB1)[0], G(0.34, CB1, CH - 1.8)[1] + 1), 0, 0, fw * 0.58 / 120.0, hx('ffffff'), font=FONT_K, size=64, bold_outline=hx('ffffff'), shear=0.5)
    s.text_face('Iksan Station', (G(0.34, CB1)[0], G(0.34, CB1, CH - 8.0)[1] + 1), 0, 0, fw * 0.52 / 420.0, hx('f0f4ff'), size=44, shear=0.5)
    s.grad([G(0.56, CB1, CH - 14.2), G(0.67, CB1, CH - 14.2), G(0.67, CB1, CH - 12.0), G(0.56, CB1, CH - 12.0)], hx('e8242c'), hx('a0101a'), (0.2, 1))
    s.text_face('KTX', (G(0.575, CB1)[0], G(0.575, CB1, CH - 12.4)[1] + 1), 0, 0, fw * 0.16 / 100.0, hx('ffffff'), size=50, shear=0.5)
    # 파란 철골 캐노피 + 흰 원형 기둥
    s.grad([G(0.20, CB1, 6.6), G(0.78, CB1, 6.6), G(0.78, 0.68, 6.6), G(0.20, 0.68, 6.6)], hx('3c8cec'), hx('1a5cb8'), (1, 0.3))
    for k in range(1, 8): s.line([G(0.20 + 0.58 * k / 8, CB1, 6.6), G(0.20 + 0.58 * k / 8, 0.68, 6.6)], hx('e8f0ff', 170), 2)
    s.grad([G(0.20, 0.68, 6.6), G(0.78, 0.68, 6.6), G(0.78, 0.68, 5.8), G(0.20, 0.68, 5.8)], hx('f4f4fc'), hx('b4b8cc'), (0, 1))
    for a in (0.24, 0.74): s.line([G(a, 0.66, 0), G(a - 0.03, CB1 + 0.04, 6.4)], hx('f0f0f8'), 9); s.line([G(a - 0.004, 0.66, 0), G(a - 0.034, CB1 + 0.04, 6.4)], hx('ffffff'), 3)
    s.grad([G(0.38, CB1, 0), G(0.60, CB1, 0), G(0.60, CB1, 6.0), G(0.38, CB1, 6.0)], hx('1a2a48'), hx('3a4a70'), (0, 1))
    for k in range(3): x, y = G(0.40 + 0.07 * k, CB1 + 0.02); ImageDraw.Draw(s.im).rectangle([x - 2, y - 14, x + 2, y - 2], fill=hx('e8c890'))      # 승객 실루엣
    # 마룡이 동상(받침 + 청록 용)
    x, y = G(0.20, 0.84); d = ImageDraw.Draw(s.im)
    d.polygon([(x - 20, y), (x + 20, y), (x + 20, y - 6), (x - 20, y - 6)], fill=hx('6a4a3a')); d.polygon([(x - 20, y - 6), (x + 20, y - 6), (x + 16, y - 9), (x - 16, y - 9)], fill=hx('8a6a52'))
    d.ellipse([x - 13, y - 34, x + 13, y - 8], fill=hx('2aa8b8')); d.ellipse([x - 8, y - 29, x + 6, y - 12], fill=hx('f0f6f8'))
    d.ellipse([x - 11, y - 44, x + 11, y - 25], fill=hx('2aa8b8')); d.ellipse([x - 8, y - 37, x + 2, y - 29], fill=hx('f0f6f8')); d.ellipse([x - 4, y - 40, x - 1, y - 37], fill=hx('10242c')); d.ellipse([x + 3, y - 40, x + 6, y - 37], fill=hx('10242c'))
    d.polygon([(x + 11, y - 30), (x + 30, y - 38), (x + 24, y - 20)], fill=hx('9ad4ee')); d.polygon([(x - 11, y - 16), (x - 30, y - 12), (x - 13, y - 6)], fill=hx('1a7a9a'))
    d.polygon([(x - 9, y - 44), (x - 14, y - 54), (x - 3, y - 45)], fill=hx('1a8a9a')); d.polygon([(x + 6, y - 44), (x + 12, y - 53), (x + 11, y - 42)], fill=hx('1a8a9a'))
    s.tree(0.02, 0.62, 0.7); s.tree(0.97, 0.74, 0.7)
    return s.finish()

# ======================= 예술의전당 (b42) =======================
def arts():
    s = Sc(60, 8); G = s.G; s.ground(); rs = random.Random(5)
    pad_shadow(s, 0.10, 0.90, 0.08, 0.66, 0.12)
    s.box(0.40, 0.90, 0.08, 0.42, 22, (hx('f2f0ea'), hx('c4c0b6')), (hx('cac6ba'), hx('96928a')), (hx('d4d2cc'), hx('aeaca6')))
    facade(s, 'F', 0.42, 0.42, 0.88, 3.0, 20.5, 5, 12, dict(STEEL, sky=hx('c8d4e4'), deep=hx('6a7a94'), frame=hx('8a8478'), span=hx('f0eee6'), span2=hx('c8c4b8'), fin=hx('f8f6ee'), fin2=hx('b8b4a8')), 0.0, win_h=0.34, fin_w=0.42, seed=16)
    facade(s, 'R', 0.90, 0.10, 0.40, 3.0, 20.5, 5, 6, dict(STEEL, sky=hx('c8d4e4'), deep=hx('6a7a94'), frame=hx('8a8478'), span=hx('d4d2ca'), span2=hx('a8a49a'), fin=hx('e0ddd2'), fin2=hx('96928a')), 0.34, win_h=0.34, fin_w=0.42, seed=17)
    s.grad([G(0.40, 0.42, 22), G(0.90, 0.42, 22), G(0.90, 0.42, 25.5), G(0.40, 0.42, 25.5)], hx('3a4a68'), hx('1a2440'), (0.2, 1))
    roof_gear(s, 0.58, 0.16, 25.5, 0.16, 0.12, 2.4)
    LA0, LA1, LB0, LB1, LH = 0.10, 0.82, 0.42, 0.66, 11
    s.box(LA0, LA1, LB0, LB1, LH, (hx('bcd6ec'), hx('4a78a8')), (hx('7aa0c8'), hx('2a4e7c')), (hx('e6eaf2'), hx('b8c0d0')))
    facade(s, 'F', LB1, LA0 + 0.02, LA1 - 0.01, 0.5, LH - 3.4, 2, 14, dict(STEEL, sky=hx('d4e8f8'), deep=hx('3a6a98'), frame=hx('f4f6fa'), span=hx('c8d8ea'), span2=hx('8aa8c8'), fin=hx('f6f8fc')), 0.0, win_h=0.78, fin_w=0.14, seed=18)
    facade(s, 'R', LA1, LB0 + 0.015, LB1 - 0.01, 0.5, LH - 3.4, 2, 5, dict(STEEL, sky=hx('b8d0e8'), deep=hx('2a5a88'), frame=hx('e8eef6')), 0.34, win_h=0.78, fin_w=0.14, seed=19)
    s.grad([G(LA0 + 0.01, LB1, LH - 3.2), G(LA1, LB1, LH - 3.2), G(LA1, LB1, LH), G(LA0 + 0.01, LB1, LH)], hx('1e2a44'), hx('0e1628'), (0.2, 1))
    fw = G(0.80, LB1)[0] - G(0.14, LB1)[0]
    s.text_face('예술의전당', (G(0.15, LB1)[0], G(0.15, LB1, LH - 0.5)[1] + 1), 0, 0, fw * 0.72 / 330.0, hx('f4d478'), font=FONT_K, size=60, shear=0.5)
    s.grad([G(0.06, LB1, LH + 0.4), G(0.86, LB1, LH + 0.4), G(0.86, 0.80, LH - 2.6), G(0.06, 0.80, LH - 2.6)], hx('f4f6fa'), hx('b6bed0'), (1, 0.6))
    s.grad([G(0.06, 0.80, LH - 2.6), G(0.86, 0.80, LH - 2.6), G(0.86, 0.80, LH - 3.6), G(0.06, 0.80, LH - 3.6)], hx('e0e4ee'), hx('9aa2b6'), (0, 1))
    for a in (0.12, 0.34, 0.58, 0.80): s.line([G(a, 0.78, 0), G(a, 0.78, LH - 3.6)], hx('f4f4f8'), 7)
    s.grad([G(0.40, 0.78, 0), G(0.54, 0.78, 0), G(0.54, 0.78, 5.2), G(0.40, 0.78, 5.2)], hx('26344e'), hx('4a5c82'), (0, 1))
    x, y = G(0.52, 0.92); d = ImageDraw.Draw(s.im); d.ellipse([x - 44, y - 13, x + 44, y + 13], fill=hx('7aa4d0')); d.ellipse([x - 36, y - 10, x + 36, y + 10], fill=hx('bcdcf4')); d.line([(x, y), (x, y - 28)], fill=hx('f0f8ff'), width=4); d.ellipse([x - 9, y - 34, x + 9, y - 24], fill=hx('e8f4ff'))
    s.tree(0.04, 0.80, 0.8); s.tree(0.96, 0.30, 0.8)
    return s.finish()

# ======================= 하림 (b44) =======================
def harim():
    s = Sc(61, 9); G = s.G; s.ground(); rs = random.Random(7)
    pad_shadow(s, 0.06, 0.84, 0.10, 0.56, 0.12)
    A0, A1, B0, B1, HH = 0.06, 0.82, 0.10, 0.56, 14
    s.box(A0, A1, B0, B1, HH, (hx('fafbff'), hx('ced4e6')), (hx('d8deee'), hx('a4abc4')), (hx('2a64c0'), hx('1a4290')))
    for k in range(1, 16): s.line([G(A0 + (A1 - A0) * k / 16, B0, HH), G(A0 + (A1 - A0) * k / 16, B1, HH)], hx('78a0e8', 170), 2)
    facade(s, 'F', B1, A0 + 0.02, A1 - 0.01, 0.6, 6.0, 1, 8, dict(STEEL, sky=hx('b0c8e8'), deep=hx('3a5a88'), frame=hx('5a6682'), span=hx('d8deee'), span2=hx('b0b8d0')), 0.0, win_h=0.78, seed=20)
    s.grad([G(A0, B1, 6.4), G(A1, B1, 6.4), G(A1, B1, HH), G(A0, B1, HH)], hx('1a64c8'), hx('0a3a88'), (0.2, 1))
    s.line([G(A0, B1, 6.4), G(A1, B1, 6.4)], hx('5ab43a'), 8)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.01, 0.6, 6.0, 1, 4, dict(STEEL, span=hx('b8c0d8'), span2=hx('9098b4')), 0.34, win_h=0.78, seed=21)
    s.grad([G(A1, B0, 6.4), G(A1, B1, 6.4), G(A1, B1, HH), G(A1, B0, HH)], hx('1454b0'), hx('08306e'), (0.2, 1))
    fw = G(0.80, B1)[0] - G(0.08, B1)[0]
    s.text_face('HARIM', (G(0.11, B1)[0], G(0.11, B1, 13.0)[1] + 1), 0, 0, fw * 0.62 / 290.0, hx('ffffff'), size=64, bold_outline=hx('0a3a88'), shear=0.5)
    s.text_face('FOOD POLIS', (G(0.58, B1)[0], G(0.58, B1, 9.6)[1] + 1), 0, 0, fw * 0.28 / 330.0, hx('d8f0c8'), size=44, shear=0.5)
    def silo(a, b, r, hh):
        x, y = G(a, b); rr = r * 47 * S; d = ImageDraw.Draw(s.im); xs0, xs1 = x - rr, x + rr; ytop = y - hh * S
        for xx in range(int(xs0), int(xs1)):
            t = max(0.0, (xx - xs0) / (xs1 - xs0)); c = lerp(hx('f6f8ff'), hx('8a92b0'), t ** 1.3); d.line([(xx, y), (xx, ytop)], fill=c, width=1)
        d.ellipse([xs0, y - rr * 0.5, xs1, y + rr * 0.5], fill=hx('9aa2bc')); d.ellipse([xs0, ytop - rr * 0.5, xs1, ytop + rr * 0.5], fill=hx('e8ecfa')); d.ellipse([xs0 + 6, ytop - rr * 0.3, xs1 - 6, ytop + rr * 0.3], fill=hx('1a64c8'))
        for z in range(1, 6): d.line([(xs0 + 4, y - hh * S * z / 6), (xs1 - 4, y - hh * S * z / 6)], fill=hx('8a92b0', 120), width=2)
        d.line([(xs1 - 4, y), (xs1 - 4, ytop)], fill=hx('5a6280'), width=3)
    silo(0.74, 0.76, 0.058, 22); silo(0.89, 0.64, 0.05, 19)
    x, y = G(0.12, 0.16, HH); d = ImageDraw.Draw(s.im); d.rectangle([x - 5, y - 42, x + 5, y], fill=hx('c8cee0')); d.rectangle([x - 5, y - 42, x - 1, y], fill=hx('f0f2fa')); d.rectangle([x - 6, y - 46, x + 6, y - 42], fill=hx('d8363a'))
    a, b = 0.18, 0.80
    s.grad([G(a, b, 0.4), G(a + 0.15, b, 0.4), G(a + 0.15, b, 5.6), G(a, b, 5.6)], hx('fafbff'), hx('c4cadc'), (0.3, 1)); s.line([G(a, b, 3.0), G(a + 0.15, b, 3.0)], hx('1a64c8'), 6)
    s.grad([G(a + 0.15, b - 0.04, 0.4), G(a + 0.15, b, 0.4), G(a + 0.15, b, 5.6), G(a + 0.15, b - 0.04, 5.6)], hx('a4abc4'), hx('6e7694'), (0.3, 1))
    s.grad([G(a + 0.17, b, 0.4), G(a + 0.22, b, 0.4), G(a + 0.22, b, 3.4), G(a + 0.17, b, 3.4)], hx('4a7ae0'), hx('1a3a88'), (0.3, 1))
    s.tree(0.03, 0.84, 0.8); s.tree(0.96, 0.86, 0.7)
    return s.finish()

# ======================= 코스트코 (b3) =======================
def costco():
    s = Sc(73, 10); G = s.G; s.ground(); rs = random.Random(3)
    s.grad([G(0.04, 0.62), G(0.96, 0.62), G(0.96, 0.97), G(0.04, 0.97)], hx('6a6e80'), hx('50546a'), (0.5, 1), 4)
    for i in range(1, 13): s.line([G(0.04 + 0.0716 * i, 0.64), G(0.04 + 0.0716 * i, 0.76)], hx('f0f0f0', 200), 2); s.line([G(0.04 + 0.0716 * i, 0.82), G(0.04 + 0.0716 * i, 0.95)], hx('f0f0f0', 200), 2)
    for (a, b, c) in ((0.13, 0.68, '#d84a3a'), (0.37, 0.70, '#e8e8f0'), (0.60, 0.67, '#3a6ac8'), (0.22, 0.88, '#2a2a34'), (0.53, 0.89, '#e0b02a'), (0.78, 0.87, '#d84a3a'), (0.83, 0.69, '#7a8aa0')):
        cc = hx(c[1:]); s.grad([G(a, b + 0.04, 0.1), G(a + 0.06, b + 0.04, 0.1), G(a + 0.06, b + 0.04, 2.6), G(a, b + 0.04, 2.6)], lerp(cc, (255, 255, 255), 0.25), cc, (0.3, 1))
        s.grad([G(a + 0.06, b - 0.01, 0.1), G(a + 0.06, b + 0.04, 0.1), G(a + 0.06, b + 0.04, 2.6), G(a + 0.06, b - 0.01, 2.6)], lerp(cc, (0, 0, 0), 0.3), lerp(cc, (0, 0, 0), 0.5), (0.3, 1))
        s.grad([G(a, b - 0.01, 2.6), G(a + 0.06, b - 0.01, 2.6), G(a + 0.06, b + 0.04, 2.6), G(a, b + 0.04, 2.6)], lerp(cc, (255, 255, 255), 0.45), cc, (1, 0.4))
    pad_shadow(s, 0.06, 0.94, 0.10, 0.60, 0.12)
    A0, A1, B0, B1, H1, DK = 0.06, 0.94, 0.10, 0.60, 11.0, 6.2
    s.box(A0, A1, B0, B1, H1, (hx('f4efe0'), hx('c8c0a8')), (hx('d4ccb4'), hx('9c947c')), (hx('b8bcc8'), hx('9498a8')))
    for k_ in range(1, 11): s.line([G(A0 + 0.088 * k_, B1, 0), G(A0 + 0.088 * k_, B1, H1)], hx('b0a888', 90), 2)
    s.grad([G(A0, B1, 5.4), G(A1, B1, 5.4), G(A1, B1, H1), G(A0, B1, H1)], hx('f04a58'), hx('c01830'), (0.2, 1)); s.line([G(A0, B1, 5.4), G(A1, B1, 5.4)], hx('005dab'), 8)
    face_w = G(0.92, B1)[0] - G(0.08, B1)[0]
    s.text_face('COSTCO', (G(0.12, B1)[0], G(0.12, B1, 10.2)[1] + 1), 0, 0, face_w * 0.78 / 300.0, hx('ffffff'), size=70, bold_outline=hx('8a0c1c'), shear=0.5)
    s.text_face('WHOLESALE', (G(0.60, B1)[0], G(0.60, B1, 6.6)[1] + 1), 0, 0, 0.15, hx('ffffff'), size=40, shear=0.5)
    facade(s, 'F', B1, 0.38, 0.62, 0.0, 5.0, 1, 4, dict(STEEL, sky=hx('c8e0f8'), deep=hx('3a6aa0'), frame=hx('e8ecf2'), span=hx('f0f0f4'), span2=hx('c8ccd8'), fin=hx('f6f6fa')), 0.0, win_h=0.95, fin_w=0.1, seed=22)
    for k_ in range(5): x, y = G(0.30 + 0.025 * k_, B1 + 0.02, 0); ImageDraw.Draw(s.im).rectangle([x - 6, y - 14, x + 6, y - 2], outline=hx('c8ccd8'), width=2)
    for lv in range(2):
        z0 = H1 + lv * DK
        s.grad([G(A0, B0, z0 + 0.1), G(A1, B0, z0 + 0.1), G(A1, B1, z0 + 0.1), G(A0, B1, z0 + 0.1)], hx('c4c8d4'), hx('a4a8b8'), (1, 0.4))
        s.grad([G(A1, B0, z0), G(A1, B1, z0), G(A1, B1, z0 + DK - 0.8), G(A1, B0, z0 + DK - 0.8)], hx('9a9ab0'), hx('6e6e88'), (0.3, 1))
        for i in range(0, 12): s.line([G(A0 + 0.0733 * i, B1, z0), G(A0 + 0.0733 * i, B1, z0 + DK - 0.8)], hx('d8d8e0'), 5)
        s.grad([G(A0, B1, z0 + DK - 0.8), G(A1, B1, z0 + DK - 0.8), G(A1, B1, z0 + DK), G(A0, B1, z0 + DK)], hx('e8e8f0'), hx('b8b8c8'), (0, 1))
        s.grad([G(A1, B0, z0 + DK - 0.8), G(A1, B1, z0 + DK - 0.8), G(A1, B1, z0 + DK), G(A1, B0, z0 + DK)], hx('b8b8c8'), hx('8c8ca0'), (0, 1))
        for j in range(8):
            if (j + lv) % 3 == 1: continue
            cc = [hx('d84a3a'), hx('e8e8f0'), hx('3a6ac8'), hx('2a2a34'), hx('e0b02a')][(j * 2 + lv) % 5]; a = A0 + 0.04 + j * 0.108; b = B0 + 0.12 + (j % 2) * 0.14
            s.grad([G(a, b + 0.04, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 2.4), G(a, b + 0.04, z0 + 2.4)], lerp(cc, (255, 255, 255), 0.25), cc, (0.3, 1))
            s.grad([G(a + 0.06, b - 0.01, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 2.4), G(a + 0.06, b - 0.01, z0 + 2.4)], lerp(cc, (0, 0, 0), 0.3), lerp(cc, (0, 0, 0), 0.5), (0.3, 1))
    zt = H1 + 2 * DK
    s.grad([G(A0, B0, zt), G(A1, B0, zt), G(A1, B1, zt), G(A0, B1, zt)], hx('d0d4e0'), hx('b0b4c4'), (1, 0.4))
    for k_ in range(3): s.grad([G(0.22 + 0.2 * k_, 0.22, zt), G(0.32 + 0.2 * k_, 0.22, zt), G(0.32 + 0.2 * k_, 0.36, zt), G(0.22 + 0.2 * k_, 0.36, zt)], hx('b8d8f0'), hx('78a0c8'), (1, 0.4))
    s.grad([G(A1, 0.16, H1), G(A1, 0.54, H1 + 0.1), G(A1 + 0.05, 0.54, 0), G(A1 + 0.05, 0.16, 0)], hx('9898b0'), hx('6a6a88'), (0.3, 1))
    return s.finish()

# ======================= 야구장 (b33) =======================
def baseball():
    s = Sc(57, 22); G = s.G; s.ground(); rs = random.Random(6); d = ImageDraw.Draw(s.im)
    F0, F1 = 0.15, 0.86
    s.shadow([G(0.04, 0.96), G(0.96, 0.96), G(1.02, 1.0), G(0.10, 1.0)], 60, 7)
    s.grad([G(F0, F0, 0.4), G(F1, F0, 0.4), G(F1, F1, 0.4), G(F0, F1, 0.4)], hx('c89860'), hx('a67a44'), (0.4, 1), 4)
    s.grad([G(F0, F0, 0.5), G(F1 - 0.05, F0, 0.5), G(F1 - 0.05, F1 - 0.05, 0.5), G(F0, F1 - 0.05, 0.5)], hx('62b852'), hx('3e8c36'), (0.4, 1), 3)
    for k in range(0, 9):
        a0 = F0 + 0.08 * k
        if a0 < F1 - 0.05: s.flat([G(a0, F0, 0.55), G(min(a0 + 0.04, F1 - 0.05), F0, 0.55), G(min(a0 + 0.04, F1 - 0.05), F1 - 0.05, 0.55), G(a0, F1 - 0.05, 0.55)], hx('78cc66', 100))
    dirt = [G(0.17, 0.17, 0.6)] + [G(0.17 + 0.46 * np.cos(t), 0.17 + 0.46 * np.sin(t), 0.6) for t in np.linspace(0.0, np.pi / 2, 18)]
    s.grad(dirt, hx('dcae72'), hx('b88846'), (0.4, 1), 4)
    s.grad([G(0.24, 0.24, 0.65), G(0.46, 0.24, 0.65), G(0.46, 0.46, 0.65), G(0.24, 0.46, 0.65)], hx('64ba54'), hx('3e8c36'), (0.4, 1), 3)
    for p in ([G(0.17, 0.17, 0.7), G(F1 - 0.03, 0.17, 0.7)], [G(0.17, 0.17, 0.7), G(0.17, F1 - 0.03, 0.7)]): s.line(p, hx('ffffff', 235), 3)
    x, y = G(0.34, 0.34, 0.8); d.ellipse([x - 8, y - 4, x + 8, y + 4], fill=hx('d8aa70')); d.rectangle([x - 2, y - 1, x + 2, y + 1], fill=hx('ffffff'))
    for (a, b) in ((0.44, 0.19), (0.19, 0.44), (0.44, 0.44), (0.175, 0.175)):
        x, y = G(a, b, 0.8); d.polygon([(x - 6, y), (x, y - 3), (x + 6, y), (x, y + 3)], fill=hx('fcfcfc')); d.line([(x - 6, y), (x, y + 3), (x + 6, y)], fill=hx('b8b8b8'), width=1)
    for (a0, a1, b0, b1) in ((0.30, 0.42, 0.115, 0.15), (0.115, 0.15, 0.30, 0.42)):
        s.box(a0, a1, b0, b1, 2.4, (hx('2a4aa8'), hx('1a3280')), (hx('1a3280'), hx('0e2060')), (hx('e8ecf8'), hx('b4bcd4')), 0.4)
    N = 9; ZR = 0.95
    def rows(k, along):
        z0 = 0.5 + k * ZR; z1 = z0 + ZR; off0 = 0.15 - 0.0105 * k; off1 = 0.15 - 0.0105 * (k + 1)
        col = [hx('2850b8'), hx('e8ecf8')][(k // 2) % 2] if k < 6 else hx('f4c43a'); dark = lerp(col, (0, 0, 0), 0.35)
        if along == 'b': return [G(0.13, off0, z0), G(0.90, off0, z0), G(0.90, off0, z1), G(0.13, off0, z1)], [G(0.13, off0, z1), G(0.90, off0, z1), G(0.90, off1, z1), G(0.13, off1, z1)], col, dark
        return [G(off0, 0.13, z0), G(off0, 0.90, z0), G(off0, 0.90, z1), G(off0, 0.13, z1)], [G(off0, 0.13, z1), G(off0, 0.90, z1), G(off1, 0.90, z1), G(off1, 0.13, z1)], col, dark
    for k in range(N - 1, -1, -1):
        for along in ('b', 'a'):
            rq, tq, col, dark = rows(k, along); s.flat(tq, lerp(col, (255, 255, 255), 0.30)); s.grad(rq, lerp(col, (255, 255, 255), 0.10), dark, (0, 1))
            for t in np.linspace(0.16, 0.86, 22):                                                                 # 관중(작은 점)
                if rs.random() < 0.55:
                    P = G(0.13 + 0.77 * t, 0.15 - 0.0105 * (k + 0.5), 0.5 + (k + 0.6) * ZR) if along == 'b' else G(0.15 - 0.0105 * (k + 0.5), 0.13 + 0.77 * t, 0.5 + (k + 0.6) * ZR)
                    d.ellipse([P[0] - 2, P[1] - 4, P[0] + 2, P[1] + 1], fill=rs.choice([hx('f0c8a0'), hx('e84b4b'), hx('3a3a4a'), hx('fff0a0'), hx('7ab0ff')]))
    for a in (0.30, 0.50, 0.70): s.line([G(a, 0.15, 0.5), G(a, 0.15 - 0.0105 * N, 0.5 + N * ZR)], hx('dcdce8'), 6)
    for b in (0.30, 0.50, 0.70): s.line([G(0.15, b, 0.5), G(0.15 - 0.0105 * N, b, 0.5 + N * ZR)], hx('dcdce8'), 6)
    s.grad([G(0.13, 0.15, 0.4), G(0.90, 0.15, 0.4), G(0.90, 0.15, 1.5), G(0.13, 0.15, 1.5)], hx('1a2a68'), hx('0e1844'), (0, 1))
    s.grad([G(0.15, 0.13, 0.4), G(0.15, 0.90, 0.4), G(0.15, 0.90, 1.5), G(0.15, 0.13, 1.5)], hx('1a2a68'), hx('0e1844'), (0, 1))
    ztop = 0.5 + N * ZR
    for along in ('b', 'a'):                                                                                   # 얇은 지붕 캐노피(맨 위 두 줄만)
        o = 0.15 - 0.0105 * N
        q = [G(0.12, o + 0.02, ztop - 1.6), G(0.92, o + 0.02, ztop - 1.6), G(0.92, 0.02, ztop + 0.8), G(0.12, 0.02, ztop + 0.8)] if along == 'b' else [G(o + 0.02, 0.12, ztop - 1.6), G(o + 0.02, 0.92, ztop - 1.6), G(0.02, 0.92, ztop + 0.8), G(0.02, 0.12, ztop + 0.8)]
        s.grad(q, hx('f6f8fc'), hx('b8c0d8'), (0.2, 1)); s.line([q[0], q[1]], hx('2a50b8'), 4)
    s.box(0.12, 0.36, 0.012, 0.06, 3.0, (hx('8ec0ee'), hx('2a5a98')), (hx('4a78b0'), hx('1a3a68')), (hx('f0f4fa'), hx('c0c8dc')), ztop - 0.6)
    for k in range(1, 7): s.line([G(0.12 + 0.24 * k / 7, 0.06, ztop - 0.6), G(0.12 + 0.24 * k / 7, 0.06, ztop + 2.4)], hx('f0f4fa'), 2)
    s.grad([G(F0, F1, 0.4), G(F1, F1, 0.4), G(F1, F1, 2.6), G(F0, F1, 2.6)], hx('2a9a50'), hx('145a2c'), (0, 1))
    s.grad([G(F1, F0, 0.4), G(F1, F1, 0.4), G(F1, F1, 2.6), G(F1, F0, 2.6)], hx('1a7a3a'), hx('0c4a22'), (0, 1))
    s.line([G(F0, F1, 2.6), G(F1, F1, 2.6)], hx('f4e44a'), 3); s.line([G(F1, F0, 2.6), G(F1, F1, 2.6)], hx('d8c838'), 3)
    s.grad([G(0.36, F1, 2.6), G(0.68, F1, 2.6), G(0.68, F1, 8.0), G(0.36, F1, 8.0)], hx('1e2640'), hx('0a1020'), (0, 1))
    s.line([G(0.36, F1, 2.6), G(0.36, F1, 8.0), G(0.68, F1, 8.0), G(0.68, F1, 2.6)], hx('c8ccdc'), 3)
    s.text_face('IKSAN', (G(0.38, F1)[0], G(0.38, F1, 7.4)[1] + 1), 0, 0, 0.13, hx('ffd84a'), size=44, shear=0.5)
    s.text_face('0 : 0', (G(0.45, F1)[0], G(0.45, F1, 4.4)[1] + 1), 0, 0, 0.12, hx('6af0a0'), size=40, shear=0.5)
    for (a, b) in ((F1, F0), (F0, F1)):
        x, y = G(a, b, 0.4); d.line([(x, y), (x, y - 9 * S)], fill=hx('f8e040'), width=4)
    for k in range(3):
        s.grad([G(F1 - 0.02 - 0.012 * k, 0.62, 0.4 + 0.9 * k), G(F1 - 0.02 - 0.012 * k, 0.84, 0.4 + 0.9 * k), G(F1 - 0.02 - 0.012 * k, 0.84, 1.3 + 0.9 * k), G(F1 - 0.02 - 0.012 * k, 0.62, 1.3 + 0.9 * k)], hx('4a6ad0') if k % 2 else hx('f0f2fa'), hx('2a40a0'), (0, 1))
    for (a, b) in ((0.07, 0.07), (0.94, 0.07), (0.07, 0.94), (0.94, 0.94)):
        x, y = G(a, b); hh = 13.5 * S
        s.shadow([(x - 4, y), (x + 4, y), (x + 26, y + 10), (x + 14, y + 10)], 50, 4)
        d.line([(x - 3, y), (x - 1, y - hh)], fill=hx('8a92aa'), width=3); d.line([(x + 3, y), (x + 1, y - hh)], fill=hx('8a92aa'), width=3)
        for z in range(1, 6): zz = y - hh * z / 6; d.line([(x - 3 + z * 0.3, zz), (x + 3 - z * 0.3, zz + hh / 12)], fill=hx('b8bed2'), width=1)
        d.rectangle([x - 20, y - hh - 14, x + 20, y - hh + 2], fill=hx('2a3050'))
        for r in range(2):
            for c in range(-3, 4): d.ellipse([x + c * 5.4 - 2.4, y - hh - 12 + r * 7, x + c * 5.4 + 2.4, y - hh - 6 + r * 7], fill=hx('fff8d0'))
    s.box(0.40, 0.54, 0.93, 0.99, 3.0, (hx('f4f4fa'), hx('c0c4d8')), (hx('c0c4d8'), hx('8a90ac')), (hx('2a50b8'), hx('1a3480')))
    x, y = G(0.50, 0.99, 0.0); d.line([(x, y), (x, y - 11 * S)], fill=hx('9aa0b8'), width=3); d.polygon([(x, y - 11 * S), (x + 22, y - 10 * S + 6), (x, y - 9 * S + 4)], fill=hx('e84b4b'))
    return s.finish()

BRICK = dict(sky=hx('a8cde8'), deep=hx('3a5a88'), frame=hx('f2ecdc'), fin=hx('b4583e'), fin2=hx('7a3424'), span=hx('c8664a'), span2=hx('96402c'), sill=hx('f6efe0'), lit=0.06)
APT = dict(sky=hx('b8d8f0'), deep=hx('4a6a98'), frame=hx('e8eaf0'), fin=hx('f2eee2'), fin2=hx('c8c0ac'), span=hx('ece6d6'), span2=hx('c4bca4'), sill=hx('ffffff'), lit=0.10)

# ======================= 원광대학병원 (b27, 96x50) =======================
def hospital():
    s = Sc(50, 31); G = s.G; s.ground(); rs = random.Random(2)
    pad_shadow(s, 0.08, 0.88, 0.08, 0.54, 0.10)
    A0, A1, B0, B1, HH = 0.08, 0.88, 0.10, 0.54, 11.5
    s.box(A0, A1, B0, B1, HH, (hx('f8f8fc'), hx('d0d4e2')), (hx('d4d8e6'), hx('a4abc0')), (hx('dfe3ee'), hx('b4bcd0')))
    facade(s, 'F', B1, A0 + 0.02, A1 - 0.01, 0.6, HH - 0.6, 3, 11, dict(STEEL, sky=hx('c8e4f4'), deep=hx('4a84a8'), span=hx('f4f6fa'), span2=hx('cdd2e0'), fin=hx('f8f8fc')), 0.0, win_h=0.55, seed=30)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.01, 0.6, HH - 0.6, 3, 5, dict(STEEL, sky=hx('b0d4e8'), deep=hx('3a7498'), span=hx('d8dce8'), span2=hx('aab0c4')), 0.34, win_h=0.55, seed=31)
    s.grad([G(0.38, B1 + 0.01, 0), G(0.58, B1 + 0.01, 0), G(0.58, B1 + 0.01, 4.8), G(0.38, B1 + 0.01, 4.8)], hx('7aa8c8'), hx('1e3a58'), (0.2, 1))             # 정면 현관
    s.grad([G(0.32, B1, 5.0), G(0.64, B1, 5.0), G(0.64, B1 + 0.10, 5.0), G(0.32, B1 + 0.10, 5.0)], hx('f4f6fa'), hx('b8c0d0'), (1, 0.4))
    cx, cy = G(0.52, B1, HH + 0.3); d = ImageDraw.Draw(s.im)
    d.rectangle([cx - 28, cy - 70, cx + 28, cy - 14], fill=hx('f6f8fc')); d.rectangle([cx - 22, cy - 62, cx + 22, cy - 22], fill=hx('2ea04a'))      # 지붕 위 녹십자 표지
    d.rectangle([cx - 5, cy - 60, cx + 5, cy - 24], fill=hx('ffffff')); d.rectangle([cx - 20, cy - 46, cx + 20, cy - 38], fill=hx('ffffff'))
    roof_gear(s, 0.18, 0.18, HH, 0.12, 0.10, 2.4); roof_gear(s, 0.66, 0.22, HH, 0.10, 0.08, 1.8)
    a, b = 0.14, 0.76                                                                                                                                  # 구급차
    s.grad([G(a, b, 0.4), G(a + 0.16, b, 0.4), G(a + 0.16, b, 4.6), G(a, b, 4.6)], hx('fafbff'), hx('c4cadc'), (0.3, 1)); s.line([G(a, b, 2.4), G(a + 0.16, b, 2.4)], hx('e8242c'), 6)
    s.grad([G(a + 0.16, b - 0.05, 0.4), G(a + 0.16, b, 0.4), G(a + 0.16, b, 4.6), G(a + 0.16, b - 0.05, 4.6)], hx('a4abc4'), hx('6e7694'), (0.3, 1))
    s.tree(0.94, 0.30, 0.7); s.tree(0.04, 0.84, 0.7); s.tree(0.72, 0.86, 0.7)
    return s.finish()

# ======================= 부영아파트 (b37, 96x70) =======================
def apartments():
    s = Sc(70, 32); G = s.G; s.ground(); rs = random.Random(3)
    for (a0, a1, b0, b1, h, fl, bf, br) in ((0.06, 0.34, 0.08, 0.30, 31, 14, 4, 3), (0.40, 0.68, 0.12, 0.34, 35, 16, 4, 3), (0.12, 0.40, 0.44, 0.66, 27, 12, 4, 3)):
        pad_shadow(s, a0, a1, b0, b1, 0.07)
        s.box(a0, a1, b0, b1, h, (hx('f2eee2'), hx('c8c0ac')), (hx('d4ccb8'), hx('a49c88')), (hx('e0dac8'), hx('b8b09c')))
        facade(s, 'F', b1, a0 + 0.012, a1 - 0.005, 1.2, h - 1.6, fl, bf, APT, 0.0, win_h=0.5, fin_w=0.30, seed=int(a0 * 100 + h))
        facade(s, 'R', a1, b0 + 0.01, b1 - 0.005, 1.2, h - 1.6, fl, br, APT, 0.34, win_h=0.5, fin_w=0.30, seed=int(b0 * 100 + h + 7))
        s.grad([G(a0, b1, h - 1.6), G(a1, b1, h - 1.6), G(a1, b1, h), G(a0, b1, h)], hx('4a6ab8'), hx('2a407a'), (0, 1))                                     # 상단 띠(파랑)
        roof_gear(s, a0 + 0.05, b0 + 0.04, h, 0.08, 0.06, 2.0)
        for k in range(3): x, y = G(a0 + 0.04 + 0.07 * k, b1, 3.0); ImageDraw.Draw(s.im).rectangle([x - 2, y - 14, x + 2, y - 2], fill=hx('2a4a8a'))
    s.grad([G(0.70, 0.62), G(0.96, 0.62), G(0.96, 0.92), G(0.70, 0.92)], hx('8cc04a'), hx('5a9a30'), (0.4, 1), 4)                                           # 단지 잔디 + 놀이터
    s.grad([G(0.76, 0.70), G(0.88, 0.70), G(0.88, 0.80), G(0.76, 0.80)], hx('e8c890'), hx('c8a468'), (0.4, 1))
    s.tree(0.52, 0.80, 0.9); s.tree(0.30, 0.88, 0.8); s.tree(0.92, 0.20, 0.8)
    return s.finish()

# ======================= 익산종합초중고 (b35, 97x64) =======================
def school():
    s = Sc(64, 33); G = s.G; s.ground(); rs = random.Random(4)
    s.grad([G(0.06, 0.62), G(0.94, 0.62), G(0.94, 0.96), G(0.06, 0.96)], hx('7ab84a'), hx('4e9232'), (0.4, 1), 4)                                            # 운동장 + 트랙
    s.grad([G(0.12, 0.68), G(0.88, 0.68), G(0.88, 0.92), G(0.12, 0.92)], hx('c8663a'), hx('a04a28'), (0.4, 1)); s.grad([G(0.18, 0.72), G(0.82, 0.72), G(0.82, 0.88), G(0.18, 0.88)], hx('62b852'), hx('3e8c36'), (0.4, 1))
    pad_shadow(s, 0.08, 0.86, 0.26, 0.56, 0.10)
    A0, A1, B0, B1, HH = 0.08, 0.86, 0.26, 0.56, 13
    s.box(A0, A1, B0, B1, HH, (hx('d8785a'), hx('a8503a')), (hx('b4583e'), hx('7a3424')), (hx('8a8c98'), hx('5a5c6a')))
    facade(s, 'F', B1, A0 + 0.02, A1 - 0.01, 0.8, HH - 0.6, 3, 12, BRICK, 0.0, win_h=0.58, seed=32)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.01, 0.8, HH - 0.6, 3, 5, BRICK, 0.34, win_h=0.58, seed=33)
    s.grad([G(A0 - 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH + 1.2), G(A0 - 0.02, B1 + 0.02, HH + 1.2)], hx('f2ecdc'), hx('c4bca8'), (0, 1))
    s.box(0.40, 0.56, 0.26, 0.42, 22, (hx('d8785a'), hx('a8503a')), (hx('b4583e'), hx('7a3424')), (hx('7a7c88'), hx('4a4c58')))                                  # 시계탑
    facade(s, 'F', 0.42, 0.42, 0.55, 8.0, 17.0, 2, 3, BRICK, 0.0, win_h=0.55, seed=34)
    x, y = G(0.48, 0.42, 19.8); d = ImageDraw.Draw(s.im); d.ellipse([x - 22, y - 22, x + 22, y + 22], fill=hx('f6f2e4')); d.ellipse([x - 22, y - 22, x + 22, y + 22], outline=hx('5a4a38'), width=4)
    d.line([(x, y), (x, y - 14)], fill=hx('2a2018'), width=3); d.line([(x, y), (x + 10, y + 3)], fill=hx('2a2018'), width=3)
    s.grad([G(0.38, 0.44, 22), G(0.58, 0.44, 22), G(0.48, 0.34, 27.5)], hx('8a8c98'), hx('4a4c58'), (0, 1))
    s.grad([G(0.44, B1, 0), G(0.52, B1, 0), G(0.52, B1, 4.8), G(0.44, B1, 4.8)], hx('3a2a1c'), hx('1a1008'), (0, 1))
    x, y = G(0.88, 0.60); d.line([(x, y), (x, y - 15 * S)], fill=hx('9a9a9a'), width=3); d.polygon([(x, y - 15 * S), (x + 26, y - 14 * S + 6), (x, y - 13 * S + 4)], fill=hx('2a5ac8'))
    s.tree(0.04, 0.60, 0.8); s.tree(0.96, 0.62, 0.8)
    im = s.finish(); out97 = Image.new('RGBA', (97, im.height), (0, 0, 0, 0)); out97.paste(im, (0, 0)); return out97

# ======================= 익산도서관 (b43, 96x79) =======================
def library():
    s = Sc(79, 34); G = s.G; s.ground(); rs = random.Random(5)
    pad_shadow(s, 0.08, 0.88, 0.12, 0.58, 0.12)
    LS = dict(sky=hx('b4d0e8'), deep=hx('3a5a82'), frame=hx('4a4234'), fin=hx('e8e0c8'), fin2=hx('b0a688'), span=hx('d8ceb0'), span2=hx('aea48a'), sill=hx('f4ecd4'), lit=0.05)
    s.box(0.08, 0.88, 0.12, 0.58, 18, (hx('eae2c8'), hx('c0b494')), (hx('c8bea0'), hx('9a9078')), (hx('c8c4b8'), hx('9a968a')))
    facade(s, 'F', 0.58, 0.10, 0.86, 1.0, 17.0, 4, 11, LS, 0.0, win_h=0.52, seed=35)
    facade(s, 'R', 0.88, 0.14, 0.56, 1.0, 17.0, 4, 5, LS, 0.34, win_h=0.52, seed=36)
    for i in range(7):                                                                                                  # 정면 포르티코 기둥 + 박공
        a = 0.30 + 0.06 * i; s.grad([G(a - 0.014, 0.66, 0), G(a + 0.014, 0.66, 0), G(a + 0.014, 0.66, 17), G(a - 0.014, 0.66, 17)], hx('f6f0dc'), hx('b4aa8c'), (0.3, 1))
    s.grad([G(0.26, 0.66, 17), G(0.70, 0.66, 17), G(0.70, 0.66, 18.6), G(0.26, 0.66, 18.6)], hx('e8e0c8'), hx('b8ae92'), (0, 1))
    s.grad([G(0.26, 0.66, 18.6), G(0.70, 0.66, 18.6), G(0.48, 0.66, 24.2)], hx('f4ecd4'), hx('c4bca0'), (0, 1))
    s.grad([G(0.26, 0.66, 18.6), G(0.70, 0.66, 18.6), G(0.70, 0.58, 18.6), G(0.26, 0.58, 18.6)], hx('d8ceb0'), hx('b0a68a'), (1, 0.4))
    s.grad([G(0.40, 0.66, 0), G(0.56, 0.66, 0), G(0.56, 0.66, 8.5), G(0.40, 0.66, 8.5)], hx('2a2218'), hx('120e08'), (0, 1))
    for k in range(6): s.box(0.30 - 0.01 * k, 0.66 + 0.01 * k, 0.66, 0.66 + 0.05 * (6 - k) * 0.6, 0.4 * (k + 1), (hx('e8e0cc'), hx('b4aa90')), (hx('b4aa90'), hx('827a64')), (hx('f4ecd8'), hx('d4ccb4')), 0)
    s.text_face('LIBRARY', (G(0.31, 0.66)[0], G(0.31, 0.66, 18.2)[1] + 1), 0, 0, 0.1, hx('6a5a38'), size=40, shear=0.5)
    s.tree(0.04, 0.80, 0.9); s.tree(0.94, 0.78, 0.9); s.tree(0.96, 0.22, 0.8)
    return s.finish()

# ======================= 익산소방서 (b11, 96x62) =======================
def firestation():
    s = Sc(62, 35); G = s.G; s.ground(); rs = random.Random(6)
    pad_shadow(s, 0.08, 0.88, 0.10, 0.54, 0.10)
    A0, A1, B0, B1, HH = 0.08, 0.88, 0.10, 0.54, 15
    s.box(A0, A1, B0, B1, HH, (hx('f6f0e8'), hx('d0c8b8')), (hx('d4ccbc'), hx('a89f8f')), (hx('c8c4bc'), hx('9a968e')))
    s.grad([G(A0, B1, 0), G(A1, B1, 0), G(A1, B1, 6.8), G(A0, B1, 6.8)], hx('f0e4d0'), hx('c8bca4'), (0.2, 1))
    for i in range(3):                                                                                                    # 차고 문 3개(빨강 셔터)
        a = A0 + 0.05 + 0.245 * i; s.grad([G(a, B1, 0), G(a + 0.20, B1, 0), G(a + 0.20, B1, 6.0), G(a, B1, 6.0)], hx('e84040'), hx('a01818'), (0, 1))
        for z in np.arange(0.8, 6.0, 1.1): s.line([G(a, B1, z), G(a + 0.20, B1, z)], hx('7a1010', 170), 2)
    facade(s, 'F', B1, A0 + 0.02, A1 - 0.01, 7.4, HH - 0.6, 1, 10, STEEL, 0.0, win_h=0.62, seed=37)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.01, 0.8, HH - 0.6, 2, 5, STEEL, 0.34, win_h=0.5, seed=38)
    s.grad([G(A0, B1, 6.8), G(A1, B1, 6.8), G(A1, B1, 7.5), G(A0, B1, 7.5)], hx('e84040'), hx('a01818'), (0, 1))
    s.box(0.74, 0.88, 0.10, 0.22, 25, (hx('f0e4d0'), hx('c8bca4')), (hx('c8bca4'), hx('968a74')), (hx('d4ccbc'), hx('a89f8f')))                           # 호스 건조탑
    for z in np.arange(3, 24, 3.2): s.line([G(0.74, 0.22, z), G(0.88, 0.22, z)], hx('8a7e66', 140), 2)
    x, y = G(0.40, B1, HH + 0.3); d = ImageDraw.Draw(s.im); d.rectangle([x - 30, y - 24, x + 30, y - 4], fill=hx('e84040')); d.rectangle([x - 28, y - 22, x + 28, y - 6], fill=hx('fff8f0'))
    s.text_face('119', (G(0.33, B1)[0], G(0.33, B1, HH + 2.2)[1] + 1), 0, 0, 0.12, hx('d82020'), size=60, shear=0.5)
    a, b = 0.26, 0.74                                                                                                      # 소방차
    s.grad([G(a, b, 0.5), G(a + 0.20, b, 0.5), G(a + 0.20, b, 4.4), G(a, b, 4.4)], hx('f05858'), hx('b01818'), (0.3, 1)); s.line([G(a, b, 2.2), G(a + 0.20, b, 2.2)], hx('fafafa'), 4)
    s.grad([G(a + 0.20, b - 0.06, 0.5), G(a + 0.20, b, 0.5), G(a + 0.20, b, 4.4), G(a + 0.20, b - 0.06, 4.4)], hx('a01818'), hx('6a0c0c'), (0.3, 1))
    s.line([G(a + 0.02, b - 0.02, 4.6), G(a + 0.18, b - 0.02, 5.4)], hx('d8d8e0'), 4)
    s.tree(0.04, 0.84, 0.7); s.tree(0.95, 0.72, 0.7)
    return s.finish()


def grass_ground(s, c0='7ab84a', c1='4e9232'):
    G = s.G
    s.grad([G(0, 1), G(1, 1), G(1, 1, -3.2), G(0, 1, -3.2)], hx('a07040'), hx('6a4426'), (0, 1), 3); s.grad([G(1, 0), G(1, 1), G(1, 1, -3.2), G(1, 0, -3.2)], hx('7a5030'), hx('4a2e18'), (0, 1), 3)
    s.grad(s.ground_poly(), hx(c0), hx(c1), (0.4, 1), 5)
    d = ImageDraw.Draw(s.im); rs = random.Random(4)
    for i in range(1200):
        a, b = rs.random(), rs.random(); x, y = G(a, b); d.line([(x, y), (x + rs.randint(-2, 2), y - rs.randint(4, 9))], fill=rs.choice([hx('c4ec7c', 150), hx('5f9a30', 150), hx('8fc455', 140)]), width=2)
    d.line([G(0, 1), G(1, 1), G(1, 0)], fill=hx('c8e58e', 200), width=3)

def bus(s, a, b, col='2a9a3a'):
    G = s.G; c = hx(col)
    s.grad([G(a, b, 0.4), G(a + 0.22, b, 0.4), G(a + 0.22, b, 4.6), G(a, b, 4.6)], lerp(c, (255, 255, 255), 0.25), c, (0.3, 1))
    for k in range(5): s.grad([G(a + 0.015 + 0.04 * k, b, 2.2), G(a + 0.045 + 0.04 * k, b, 2.2), G(a + 0.045 + 0.04 * k, b, 4.0), G(a + 0.015 + 0.04 * k, b, 4.0)], hx('c8e4f8'), hx('3a5a88'), (0.2, 1))
    s.grad([G(a + 0.22, b - 0.06, 0.4), G(a + 0.22, b, 0.4), G(a + 0.22, b, 4.6), G(a + 0.22, b - 0.06, 4.6)], lerp(c, (0, 0, 0), 0.3), lerp(c, (0, 0, 0), 0.5), (0.3, 1))
    s.grad([G(a, b - 0.06, 4.6), G(a + 0.22, b - 0.06, 4.6), G(a + 0.22, b, 4.6), G(a, b, 4.6)], lerp(c, (255, 255, 255), 0.45), c, (1, 0.4))

# ======================= 익산터미널 (b41, 96x67) =======================
def terminal():
    s = Sc(67, 40); G = s.G; s.ground(); rs = random.Random(7)
    s.grad([G(0.04, 0.60), G(0.96, 0.60), G(0.96, 0.98), G(0.04, 0.98)], hx('6a6e80'), hx('50546a'), (0.5, 1), 4)
    for i in range(1, 6): s.line([G(0.04 + 0.153 * i, 0.64), G(0.04 + 0.153 * i, 0.96)], hx('f4e44a', 200), 3)
    pad_shadow(s, 0.08, 0.90, 0.10, 0.56, 0.10)
    A0, A1, B0, B1, HH = 0.08, 0.90, 0.10, 0.56, 11
    s.box(A0, A1, B0, B1, HH, (hx('fbf1c8'), hx('d6c690')), (hx('d8c88e'), hx('a89860')), (hx('c8c4b4'), hx('9a968a')))
    facade(s, 'F', B1, A0 + 0.02, A1 - 0.01, 1.0, HH - 3.2, 2, 13, dict(STEEL, sky=hx('c8e4f4'), deep=hx('4a84a8'), frame=hx('f4f0e0'), span=hx('f6e8b0'), span2=hx('d0bc78'), fin=hx('fff6d0')), 0.0, win_h=0.62, seed=40)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.01, 1.0, HH - 3.2, 2, 5, dict(STEEL, span=hx('d8c88e'), span2=hx('b0a068')), 0.34, win_h=0.62, seed=41)
    s.grad([G(A0, B1, HH - 3.0), G(A1, B1, HH - 3.0), G(A1, B1, HH), G(A0, B1, HH)], hx('2a8a3a'), hx('0e5a22'), (0, 1))
    fw = G(0.88, B1)[0] - G(0.10, B1)[0]
    s.text_face('익산 고속버스터미널', (G(0.11, B1)[0], G(0.11, B1, HH - 0.4)[1] + 1), 0, 0, fw * 0.80 / 640.0, hx('ffffff'), font=FONT_K, size=56, shear=0.5)
    s.grad([G(0.06, B1, 4.4), G(0.92, B1, 4.4), G(0.92, 0.70, 4.4), G(0.06, 0.70, 4.4)], hx('f4f4fa'), hx('b8bed0'), (1, 0.3))                  # 승차장 캐노피
    s.grad([G(0.06, 0.70, 4.4), G(0.92, 0.70, 4.4), G(0.92, 0.70, 3.6), G(0.06, 0.70, 3.6)], hx('e8ecf6'), hx('9aa2b6'), (0, 1))
    for i in range(7): s.line([G(0.08 + 0.135 * i, 0.69, 3.6), G(0.08 + 0.135 * i, 0.69, 0)], hx('e8ecf6'), 5)
    bus(s, 0.12, 0.84, '2a9a3a'); bus(s, 0.42, 0.88, '2a7ad8'); bus(s, 0.68, 0.84, '2a9a3a')
    s.tree(0.02, 0.58, 0.7); s.tree(0.97, 0.56, 0.7)
    return s.finish()

# ======================= 버스정류장 (b40, 96x56) =======================
def busstop():
    s = Sc(56, 41); G = s.G; s.ground(); rs = random.Random(8)
    s.grad([G(0.0, 0.52), G(1.0, 0.52), G(1.0, 1.0), G(0.0, 1.0)], hx('5a5e70'), hx('42465a'), (0.5, 1), 4)                                  # 도로
    s.line([G(0.0, 0.76), G(1.0, 0.76)], hx('f4e44a'), 4)
    for i in range(1, 12): s.line([G(0.08 * i, 0.88), G(0.08 * i + 0.04, 0.88)], hx('f0f0f0', 200), 3)
    pad_shadow(s, 0.20, 0.62, 0.20, 0.40, 0.08)
    s.box(0.22, 0.62, 0.22, 0.40, 6.0, (hx('c8e0f4'), hx('5a88b8')), (hx('7aa4cc'), hx('34587e')), (hx('2a50b8'), hx('1a3480')))                     # 유리 쉼터 + 파란 지붕
    for i in range(1, 8): s.line([G(0.22 + 0.4 * i / 8, 0.40, 0), G(0.22 + 0.4 * i / 8, 0.40, 5.4)], hx('f0f4fa'), 2)
    s.grad([G(0.20, 0.40, 6.0), G(0.64, 0.40, 6.0), G(0.64, 0.40, 7.0), G(0.20, 0.40, 7.0)], hx('3a70e0'), hx('1a3c98'), (0, 1))
    s.line([G(0.26, 0.40, 1.8), G(0.58, 0.40, 1.8)], hx('8a5a2a'), 5)                                                                        # 벤치
    x, y = G(0.70, 0.40); d = ImageDraw.Draw(s.im); d.line([(x, y), (x, y - 14 * S)], fill=hx('8a92aa'), width=4); d.rectangle([x - 18, y - 14 * S - 14, x + 18, y - 14 * S + 8], fill=hx('2a50b8')); d.rectangle([x - 15, y - 14 * S - 10, x + 15, y - 14 * S + 4], fill=hx('f4f6fc'))
    bus(s, 0.18, 0.62, '2a7ad8')
    s.tree(0.04, 0.16, 0.7); s.tree(0.88, 0.14, 0.8)
    return s.finish()

# ======================= 서동유치원 (b47, 96x55) =======================
def kindergarten():
    s = Sc(55, 42); G = s.G; grass_ground(s, '86c456', '5aa23a'); rs = random.Random(9)
    pad_shadow(s, 0.14, 0.78, 0.10, 0.40, 0.08)
    # 알록달록 낮은 건물 3동
    for (a0, a1, col) in ((0.14, 0.38, ('f06a8a', 'c8385e')), (0.38, 0.60, ('f4c43a', 'c8961a')), (0.60, 0.80, ('4aa8e8', '2a6eb4'))):
        s.box(a0, a1, 0.10, 0.40, 6.0, (hx(col[0]), hx(col[1])), (lerp(hx(col[0]), (0, 0, 0), 0.25), lerp(hx(col[1]), (0, 0, 0), 0.4)), (hx('fafafc'), hx('d4d8e4')))
        facade(s, 'F', 0.40, a0 + 0.015, a1 - 0.01, 0.8, 5.2, 1, 3, dict(STEEL, sky=hx('d4ecfa'), deep=hx('5a98c8'), frame=hx('ffffff'), span=hx(col[0]), span2=hx(col[1]), fin=hx('ffffff')), 0.0, win_h=0.62, seed=int(a0 * 100))
        s.grad([G(a0, 0.40, 6.0), G(a1, 0.40, 6.0), G(a1, 0.40, 7.4), G(a0, 0.40, 7.4)], hx('fafafc'), hx('c8ccd8'), (0, 1))
    x, y = G(0.50, 0.40, 8.0); d = ImageDraw.Draw(s.im); d.ellipse([x - 12, y - 12, x + 12, y + 12], fill=hx('ffd84a')); d.ellipse([x - 8, y - 8, x + 8, y + 8], fill=hx('ffec8a'))                  # 해님 간판
    for k in range(8): ang = k * math.pi / 4; d.line([(x + math.cos(ang) * 13, y + math.sin(ang) * 13), (x + math.cos(ang) * 19, y + math.sin(ang) * 19)], fill=hx('ffd84a'), width=3)
    # 놀이터: 미끄럼틀 + 모래밭 + 울타리
    s.grad([G(0.14, 0.58), G(0.40, 0.58), G(0.40, 0.84), G(0.14, 0.84)], hx('ecd8a0'), hx('c8b072'), (0.4, 1), 4)
    s.box(0.44, 0.52, 0.60, 0.68, 6.0, (hx('e84b4b'), hx('a82828')), (hx('a82828'), hx('701818')), (hx('f08a8a'), hx('c85a5a')))
    s.grad([G(0.52, 0.62, 5.4), G(0.58, 0.62, 5.4), G(0.66, 0.78, 0.4), G(0.60, 0.78, 0.4)], hx('4aa8e8'), hx('1a68b4'), (0.5, 1))
    for (a, c) in ((0.66, 'e84b4b'), (0.74, 'f4c43a'), (0.82, '4a9a3a')): s.box(a, a + 0.06, 0.64, 0.70, 1.8 + (a * 7 % 2), (hx(c), lerp(hx(c), (0, 0, 0), 0.4)), (lerp(hx(c), (0, 0, 0), 0.3), lerp(hx(c), (0, 0, 0), 0.55)), (lerp(hx(c), (255, 255, 255), 0.5), hx(c)), 0)
    s.tree(0.04, 0.80, 0.8); s.tree(0.95, 0.30, 0.8); s.tree(0.88, 0.84, 0.7)
    return s.finish()

# ======================= 전국일등학교 (b48, 96x64) =======================
def topschool():
    s = Sc(64, 43); G = s.G; s.ground(); rs = random.Random(10)
    s.grad([G(0.06, 0.64), G(0.94, 0.64), G(0.94, 0.96), G(0.06, 0.96)], hx('7ab84a'), hx('4e9232'), (0.4, 1), 4)
    pad_shadow(s, 0.08, 0.86, 0.14, 0.58, 0.10)
    SCH = dict(sky=hx('b4d6f0'), deep=hx('3c5e88'), frame=hx('f6f0e0'), fin=hx('f0e4c6'), fin2=hx('bca880'), span=hx('e8d8b2'), span2=hx('c0ac80'), sill=hx('ffffff'), lit=0.06)
    A0, A1, B0, B1, HH = 0.08, 0.86, 0.14, 0.58, 19
    s.box(A0, A1, B0, B1, HH, (hx('f2e6c8'), hx('c8b68a')), (hx('d0c096'), hx('a08e66')), (hx('9a948a'), hx('6a655a')))
    facade(s, 'F', B1, A0 + 0.02, A1 - 0.01, 0.8, HH - 0.8, 4, 12, SCH, 0.0, win_h=0.56, seed=43)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.01, 0.8, HH - 0.8, 4, 5, SCH, 0.34, win_h=0.56, seed=44)
    for i in range(5):                                                                                                                     # 현관 기둥 + 박공
        a = 0.36 + 0.07 * i; s.grad([G(a - 0.014, B1 + 0.05, 0), G(a + 0.014, B1 + 0.05, 0), G(a + 0.014, B1 + 0.05, 8.4), G(a - 0.014, B1 + 0.05, 8.4)], hx('faf6ec'), hx('b8ae94'), (0.3, 1))
    s.grad([G(0.32, B1 + 0.05, 8.4), G(0.70, B1 + 0.05, 8.4), G(0.51, B1 + 0.05, 12.4)], hx('f4ecd8'), hx('c4b898'), (0, 1))
    s.grad([G(0.32, B1 + 0.05, 8.4), G(0.70, B1 + 0.05, 8.4), G(0.70, B1, 8.4), G(0.32, B1, 8.4)], hx('d8ccae'), hx('a89c80'), (1, 0.4))
    s.grad([G(0.44, B1 + 0.05, 0), G(0.58, B1 + 0.05, 0), G(0.58, B1 + 0.05, 5.8), G(0.44, B1 + 0.05, 5.8)], hx('3a2a1c'), hx('1a1008'), (0, 1))
    s.text_face('1', (G(0.46, B1 + 0.05)[0], G(0.46, B1 + 0.05, 11.4)[1] + 1), 0, 0, 0.10, hx('c8382a'), size=60, bold_outline=hx('8a1c12'), shear=0.5)
    x, y = G(0.92, 0.62); d = ImageDraw.Draw(s.im); d.line([(x, y), (x, y - 17 * S)], fill=hx('9a9a9a'), width=3); d.polygon([(x, y - 17 * S), (x + 28, y - 16 * S + 6), (x, y - 15 * S + 4)], fill=hx('2a5ac8'))
    s.tree(0.04, 0.62, 0.8); s.tree(0.96, 0.66, 0.8)
    return s.finish()

# ======================= 국립익산박물관 (b26, 96x65) =======================
def museum():
    s = Sc(65, 44); G = s.G; s.ground(); rs = random.Random(11)
    pad_shadow(s, 0.08, 0.88, 0.14, 0.56, 0.12)
    MU = dict(sky=hx('bcd8ec'), deep=hx('4a6e92'), frame=hx('e8e4d8'), fin=hx('f2eee0'), fin2=hx('c4bca4'), span=hx('ece6d6'), span2=hx('c8c0a8'), sill=hx('ffffff'), lit=0.04)
    A0, A1, B0, B1, HH = 0.08, 0.88, 0.14, 0.56, 12
    s.box(A0, A1, B0, B1, HH, (hx('f4efe2'), hx('cfc8b4')), (hx('d4ceba'), hx('a8a28e')), (hx('c8c8c0'), hx('9a9a92')))
    for i in range(1, 22): s.line([G(A0 + 0.8 * i / 22, B1, 0), G(A0 + 0.8 * i / 22, B1, HH)], hx('b8b19c', 80), 2)                    # 석재 패널 이음
    for j in range(1, 6): s.line([G(A0, B1, HH * j / 6), G(A1, B1, HH * j / 6)], hx('b8b19c', 80), 2)
    facade(s, 'F', B1, 0.40, 0.82, 1.0, 9.0, 1, 4, dict(MU, sky=hx('d0e6f6'), deep=hx('4a7aa2'), frame=hx('3a3a38'), span=hx('f0ecde'), span2=hx('c8c0a8')), 0.0, win_h=0.9, fin_w=0.08, seed=45)
    facade(s, 'R', A1, B0 + 0.02, B1 - 0.01, 1.0, 9.0, 1, 3, dict(MU, sky=hx('b0cce0'), deep=hx('3a5e82')), 0.34, win_h=0.5, fin_w=0.3, seed=46)
    s.grad([G(A0 - 0.02, B1 + 0.03, HH), G(A1 + 0.02, B1 + 0.03, HH), G(A1 + 0.02, B1 + 0.03, HH + 1.4), G(A0 - 0.02, B1 + 0.03, HH + 1.4)], hx('faf8f0'), hx('d0c8b0'), (0, 1))
    s.grad([G(A0 - 0.02, B0, HH + 1.4), G(A1 + 0.02, B0, HH + 1.4), G(A1 + 0.02, B1 + 0.03, HH + 1.4), G(A0 - 0.02, B1 + 0.03, HH + 1.4)], hx('e8e4d6'), hx('cfc8b4'), (1, 0.4))
    s.box(0.14, 0.34, 0.56, 0.74, 5.0, (hx('f4efe2'), hx('cfc8b4')), (hx('d4ceba'), hx('a8a28e')), (hx('e8e4d6'), hx('cfc8b4')))                  # 입구 돌출부
    s.grad([G(0.18, 0.74, 0), G(0.30, 0.74, 0), G(0.30, 0.74, 3.8), G(0.18, 0.74, 3.8)], hx('7aa4c8'), hx('1e3a58'), (0.2, 1))
    for k in range(5): s.box(0.40 - 0.01 * k, 0.60 + 0.01 * k, 0.74, 0.74 + 0.05 * (5 - k) * 0.5, 0.36 * (k + 1), (hx('e8e4d8'), hx('b4ae9a')), (hx('b4ae9a'), hx('827c68')), (hx('f4f0e4'), hx('d6d0bc')), 0)
    fw = G(0.86, B1)[0] - G(0.12, B1)[0]
    s.text_face('국립익산박물관', (G(0.12, B1)[0], G(0.12, B1, 11.2)[1] + 1), 0, 0, fw * 0.62 / 460.0, hx('4a4638'), font=FONT_K, size=56, shear=0.5)
    s.tree(0.04, 0.78, 0.8); s.tree(0.95, 0.40, 0.8); s.tree(0.80, 0.86, 0.7)
    return s.finish()

# ======================= 놀이터 (b52, 96x42) =======================
def playground():
    s = Sc(42, 45); G = s.G; grass_ground(s, '86c456', '5aa23a'); rs = random.Random(12)
    s.grad([G(0.14, 0.14), G(0.86, 0.14), G(0.86, 0.86), G(0.14, 0.86)], hx('ecd8a0'), hx('c8b072'), (0.4, 1), 4)                              # 모래/탄성 바닥
    s.shadow([G(0.2, 0.30), G(0.5, 0.30), G(0.6, 0.42), G(0.3, 0.42)], 80, 6)
    for (a, b, c) in ((0.22, 0.24, 'e84b4b'), (0.58, 0.60, '4a9a3a')):                                                                         # 미끄럼틀 탑 + 사다리
        s.box(a, a + 0.12, b, b + 0.12, 5.4, (hx(c), lerp(hx(c), (0, 0, 0), 0.4)), (lerp(hx(c), (0, 0, 0), 0.3), lerp(hx(c), (0, 0, 0), 0.55)), (lerp(hx(c), (255, 255, 255), 0.5), hx(c)), 0)
        s.grad([G(a + 0.04, b + 0.12, 4.8), G(a + 0.09, b + 0.12, 4.8), G(a + 0.20, b + 0.30, 0.4), G(a + 0.14, b + 0.30, 0.4)], hx('4aa8e8'), hx('1a68b4'), (0.5, 1))
    for (a, b) in ((0.58, 0.18), (0.70, 0.18)):                                                                                                # 그네 틀
        x, y = G(a, b); d = ImageDraw.Draw(s.im)
        d.line([(x - 18, y), (x - 10, y - 5 * S)], fill=hx('2a6ad8'), width=4); d.line([(x + 18, y), (x + 10, y - 5 * S)], fill=hx('2a6ad8'), width=4); d.line([(x - 10, y - 5 * S), (x + 10, y - 5 * S)], fill=hx('2a6ad8'), width=4)
        d.line([(x - 4, y - 5 * S), (x - 4, y - 1.6 * S)], fill=hx('888888'), width=2); d.line([(x + 4, y - 5 * S), (x + 4, y - 1.6 * S)], fill=hx('888888'), width=2); d.rectangle([x - 8, y - 1.6 * S, x + 8, y - 1.2 * S], fill=hx('e8a020'))
    s.box(0.32, 0.50, 0.66, 0.84, 0.8, (hx('b08850'), hx('7a5a30')), (hx('7a5a30'), hx('4a3418')), (hx('e8d098'), hx('c8a868')), 0)                      # 모래 놀이틀
    for a in (0.20, 0.40, 0.62): s.line([G(a, 0.90, 0), G(a, 0.90, 1.8)], hx('8a5a2a'), 5); s.line([G(a - 0.03, 0.90, 1.8), G(a + 0.03, 0.90, 1.8)], hx('8a5a2a'), 5)
    s.tree(0.06, 0.20, 0.8); s.tree(0.94, 0.70, 0.7)
    return s.finish()

# ======================= 배산체육공원 (b53, 96x42) =======================
def sportspark():
    s = Sc(42, 46); G = s.G; grass_ground(s, '7ab84a', '4e9232'); rs = random.Random(13)
    s.grad([G(0.10, 0.10), G(0.48, 0.10), G(0.48, 0.46), G(0.10, 0.46)], hx('2a8a5a'), hx('1a6a42'), (0.4, 1), 3)                                # 테니스 코트
    for p in ([G(0.12, 0.12), G(0.46, 0.12), G(0.46, 0.44), G(0.12, 0.44), G(0.12, 0.12)],): s.line(p, hx('ffffff', 230), 3)
    s.line([G(0.29, 0.12), G(0.29, 0.44)], hx('ffffff', 230), 3); s.line([G(0.12, 0.28), G(0.46, 0.28)], hx('f4f4f4'), 4)
    s.grad([G(0.54, 0.10), G(0.92, 0.10), G(0.92, 0.46), G(0.54, 0.46)], hx('c8663a'), hx('a04a28'), (0.4, 1), 3)                                # 러닝 트랙(타원)
    s.grad([G(0.62, 0.18), G(0.84, 0.18), G(0.84, 0.38), G(0.62, 0.38)], hx('62b852'), hx('3e8c36'), (0.4, 1), 3)
    d = ImageDraw.Draw(s.im)
    for k in range(1, 3): s.line([G(0.56, 0.10 + 0.12 * k), G(0.90, 0.10 + 0.12 * k)], hx('f4f4f4', 160), 2)
    s.grad([G(0.10, 0.58), G(0.48, 0.58), G(0.48, 0.90), G(0.10, 0.90)], hx('d8c896'), hx('b8a468'), (0.4, 1), 3)                                # 농구 코트 + 골대
    x, y = G(0.14, 0.74); d.line([(x, y), (x, y - 5 * S)], fill=hx('8a92aa'), width=4); d.rectangle([x - 8, y - 5 * S - 8, x + 8, y - 5 * S + 4], fill=hx('f4f6fc')); d.ellipse([x - 5, y - 5 * S + 2, x + 5, y - 5 * S + 8], outline=hx('e8582a'), width=2)
    for (a, b) in ((0.58, 0.64), (0.58, 0.78)):                                                                                                # 운동기구 + 벤치
        s.line([G(a, b, 0), G(a, b, 2.8)], hx('4a7ae0'), 5); s.line([G(a - 0.04, b, 2.8), G(a + 0.04, b, 2.8)], hx('e8a020'), 5)
    for a in (0.74, 0.84): s.line([G(a, 0.88, 1.2), G(a + 0.07, 0.88, 1.2)], hx('8a5a2a'), 5)
    s.tree(0.04, 0.54, 0.8); s.tree(0.96, 0.52, 0.8); s.tree(0.50, 0.96, 0.7)
    return s.finish()


if __name__ == '__main__':
    items = {'b4': hotel, 'b19': cinema, 'b15': tesla, 'b22': station, 'b42': arts, 'b44': harim, 'b3': costco, 'b33': baseball, 'b27': hospital, 'b37': apartments, 'b35': school, 'b43': library, 'b11': firestation, 'b41': terminal, 'b40': busstop, 'b47': kindergarten, 'b48': topschool, 'b26': museum, 'b52': playground, 'b53': sportspark}
    sheet = Image.new('RGBA', (5 * 96 * 5 + 50, 4 * 84 * 5 + 40), (150, 70, 160, 255))
    for k, (n, fn) in enumerate(items.items()):
        im = fn(); im.save(f'{out}/{n}.png'); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im)
        sheet.paste(bg.resize((im.width * 5, im.height * 5), Image.NEAREST), ((k % 5) * (96 * 5 + 10), (k // 5) * (84 * 5 + 10)))
        if len(sys.argv) > 1: bg.resize((im.width * 8, im.height * 8), Image.NEAREST).save(sys.argv[1].replace('.png', f'_{n}.png'))
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('HQ 건물 →', out)
