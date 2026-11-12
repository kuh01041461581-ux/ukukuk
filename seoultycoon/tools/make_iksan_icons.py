"""한눈에 알아볼 특징을 넣은 건물 도트(원작처럼 크고 진하게, 상징물 포함).
  소방서: 빨간 차고문 3칸 + 옥상 '119' 간판 + 망루(사이렌) + 앞마당 소방차
  경찰서: 흰·파란 청사 + 'POLICE' 띠 + 옥탑의 금색 독수리 마크 + 순찰차 + 포돌이
캔버스 크기와 바닥은 build_sprites 가 Sc.OVR 로 정해 준다(조각 규칙)."""
import os, sys, math, random
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, lerp, W, S, FONT_K, FONT_B

def wheel(d, x, y, r=9):
    d.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=hx('1a1a22')); d.ellipse([x - r * 0.45, y - r * 0.36, x + r * 0.45, y + r * 0.36], fill=hx('b8bcc8'))
def vehicle(s, a, b, la, lb, h, body, body_d, top, stripe=None, cab=None, bar=None, wheels=True):
    """a 방향으로 선 차. body=(밝은면,어두운면) 색쌍."""
    G = s.G; d = ImageDraw.Draw(s.im)
    s.shadow([G(a - 0.02, b + lb), G(a + la, b + lb), G(a + la + 0.05, b + lb + 0.05), G(a + 0.02, b + lb + 0.06)], 110, 5)
    s.box(a, a + la, b, b + lb, h, body, body_d, top, 1.0)
    if stripe: s.grad([G(a, b + lb, 1.0 + h * 0.36), G(a + la, b + lb, 1.0 + h * 0.36), G(a + la, b + lb, 1.0 + h * 0.62), G(a, b + lb, 1.0 + h * 0.62)], stripe, stripe, (0, 1))
    if cab:                                                               # 운전석(앞쪽 = a 큰 쪽) 유리
        ca0 = a + la * 0.62
        s.box(ca0, a + la * 0.96, b + lb * 0.08, b + lb * 0.92, h * 0.55, (hx('bfe6ff'), hx('5a9ad8')), (hx('8ac0ec'), hx('3a70b0')), cab, 1.0 + h)
    if bar:
        x, y = G(a + la * 0.5, b + lb * 0.5, 1.0 + h * (1.62 if cab else 1.05)); d.rectangle([x - 12, y - 5, x - 1, y + 3], fill=bar[0]); d.rectangle([x + 1, y - 5, x + 12, y + 3], fill=bar[1])
    if wheels:
        for t in (0.2, 0.78):
            x, y = G(a + la * t, b + lb, 0.9); wheel(d, x, y)

def firestation():
    s = Sc(62, 35); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1, HH = 0.14, 0.70, 0.12, 0.58, 15.0
    s.shadow([G(0.16, B1), G(0.90, B1), G(1.0, 0.72), G(0.28, 0.76)], 115, 9)
    # 망루(호스 건조탑): 크림색 + 빨간 띠 + 꼭대기 사이렌
    TA0, TA1, TB0, TB1, TH = 0.70, 0.88, 0.12, 0.32, 33.0
    s.box(TA0, TA1, TB0, TB1, TH, (hx('fff0c8'), hx('f2c878')), (hx('e0aa50'), hx('b47a28')), (hx('e03a1c'), hx('b02410')))
    for z in (9.0, 18.0, 27.0):
        s.grad([G(TA0, TB1, z), G(TA1, TB1, z), G(TA1, TB1, z + 2.6), G(TA0, TB1, z + 2.6)], hx('f03a20'), hx('c81c0c'), (0, 1)); s.grad([G(TA1, TB0, z), G(TA1, TB1, z), G(TA1, TB1, z + 2.6), G(TA1, TB0, z + 2.6)], hx('b82410'), hx('8a1408'), (0, 1))
    s.grad([G(TA0 + 0.04, TB1, 21.5), G(TA1 - 0.04, TB1, 21.5), G(TA1 - 0.04, TB1, 25.5), G(TA0 + 0.04, TB1, 25.5)], hx('8ad8ff'), hx('2a70c0'), (0, 1))
    x, y = G((TA0 + TA1) / 2, (TB0 + TB1) / 2, TH); d.rectangle([x - 5, y - 30, x + 5, y], fill=hx('c8ccd8')); d.ellipse([x - 16, y - 52, x + 16, y - 24], fill=hx('ff3020')); d.ellipse([x - 9, y - 47, x + 1, y - 37], fill=hx('ffd0c0'))
    # 본관: 크림색 벽 + 빨간 지붕
    s.box(A0, A1, B0, B1, HH, (hx('fff0c8'), hx('f6cc7c')), (hx('e4ac54'), hx('b8802c')), (hx('e8402a'), hx('b82814')))
    for k in range(1, 8): s.line([G(A0 + (A1 - A0) * k / 8, B0, HH), G(A0 + (A1 - A0) * k / 8, B1, HH)], hx('8a1c0c', 150), 2)
    # 빨간 차고문 3칸(흰 가로줄 + 창)
    for i in range(3):
        a0 = A0 + 0.03 + i * 0.178; a1 = a0 + 0.15
        s.grad([G(a0, B1, 0), G(a1, B1, 0), G(a1, B1, 9.6), G(a0, B1, 9.6)], hx('ff4a30'), hx('c41c0c'), (0.2, 1))
        for z in (2.4, 4.8, 7.2): s.line([G(a0, B1, z), G(a1, B1, z)], hx('8a1206'), 3)
        s.grad([G(a0 + 0.02, B1, 5.4), G(a1 - 0.02, B1, 5.4), G(a1 - 0.02, B1, 6.9), G(a0 + 0.02, B1, 6.9)], hx('c8f0ff'), hx('5aa8e8'), (0, 1))
        s.line([G(a0, B1, 0), G(a0, B1, 9.6), G(a1, B1, 9.6), G(a1, B1, 0)], hx('fff8e0'), 4)
    # 노란 띠 + 오른쪽 벽 창
    s.grad([G(A0, B1, 10.6), G(A1, B1, 10.6), G(A1, B1, 12.6), G(A0, B1, 12.6)], hx('ffe040'), hx('f0b000'), (0, 1))
    s.win_grid_R(B0 + 0.04, B1 - 0.04, A1, 3.0, 12.0, 3, 2, hx('a8e4ff'), hx('2a6ab8'), pad=0.2)
    # 옥상 '119' 큰 간판(흰 판 + 빨간 글씨 + 빨간 테)
    SA0, SA1, SZ0, SZ1 = A0 + 0.06, A0 + 0.48, HH + 1.0, HH + 10.5
    for a in (SA0 + 0.04, SA1 - 0.04): s.line([G(a, B1 - 0.02, HH), G(a, B1 - 0.02, SZ0)], hx('6a6e7c'), 5)
    s.grad([G(SA0, B1 - 0.02, SZ0), G(SA1, B1 - 0.02, SZ0), G(SA1, B1 - 0.02, SZ1), G(SA0, B1 - 0.02, SZ1)], hx('ffffff'), hx('e8ecf4'), (0, 1))
    s.line([G(SA0, B1 - 0.02, SZ0), G(SA1, B1 - 0.02, SZ0), G(SA1, B1 - 0.02, SZ1), G(SA0, B1 - 0.02, SZ1), G(SA0, B1 - 0.02, SZ0)], hx('e02010'), 6)
    fw = G(SA1, B1)[0] - G(SA0, B1)[0]
    s.text_face('119', (G(SA0 + 0.035, B1)[0], G(SA0 + 0.035, B1 - 0.02, SZ1 - 1.2)[1]), 0, 0, fw * 0.80 / 108.0, hx('e01408'), font=FONT_B, size=60, shear=0.5)
    # 앞마당 소방차: 빨간 차체 + 흰 사다리 + 파란 경광등
    vehicle(s, 0.30, 0.72, 0.40, 0.15, 5.4, (hx('ff3c24'), hx('c81808')), (hx('c82410'), hx('8e1206')), (hx('ff6a50'), hx('e03a20')), stripe=hx('fff4e0'), cab=(hx('ff5a40'), hx('d83018')), bar=(hx('3a8cff'), hx('ff3030')))
    for t in (0.1, 0.25, 0.4, 0.55):
        s.line([G(0.30 + 0.40 * t, 0.755, 6.6), G(0.30 + 0.40 * t, 0.835, 6.6)], hx('f4f6fa'), 3)
    s.line([G(0.31, 0.755, 6.6), G(0.55, 0.755, 6.6)], hx('f4f6fa'), 3); s.line([G(0.31, 0.835, 6.6), G(0.55, 0.835, 6.6)], hx('f4f6fa'), 3)
    # 소화전 + 나무
    x, y = G(0.10, 0.86); d.rectangle([x - 6, y - 22, x + 6, y], fill=hx('e82818')); d.ellipse([x - 8, y - 30, x + 8, y - 18], fill=hx('ff5a40')); d.rectangle([x - 11, y - 14, x + 11, y - 9], fill=hx('c01808'))
    s.tree(0.93, 0.56, 0.7)
    return s.finish()

def police():
    s = Sc(59, 10); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1, HH = 0.10, 0.80, 0.14, 0.58, 20.0
    s.shadow([G(0.12, B1), G(0.92, B1), G(1.0, 0.72), G(0.24, 0.76)], 115, 9)
    # 청사: 흰 벽 + 파란 띠 + 하늘색 창
    s.box(A0, A1, B0, B1, HH, (hx('ffffff'), hx('d4e2f8')), (hx('b4c8ec'), hx('7c98cc')), (hx('6a88c4'), hx('40609c')))
    for (z0, z1) in ((1.6, 5.4), (7.6, 11.4)):
        s.grad([G(A0 + 0.03, B1, z0), G(A1 - 0.03, B1, z0), G(A1 - 0.03, B1, z1), G(A0 + 0.03, B1, z1)], hx('a8e8ff'), hx('2a78d0'), (0.2, 1))
        for k in range(1, 10): s.line([G(A0 + 0.03 + (A1 - A0 - 0.06) * k / 10, B1, z0), G(A0 + 0.03 + (A1 - A0 - 0.06) * k / 10, B1, z1)], hx('ffffff'), 4)
        s.grad([G(A1, B0 + 0.03, z0), G(A1, B1 - 0.03, z0), G(A1, B1 - 0.03, z1), G(A1, B0 + 0.03, z1)], hx('6ab0f0'), hx('1c50a0'), (0.2, 1))
    s.grad([G(A0, B1, 5.8), G(A1, B1, 5.8), G(A1, B1, 7.2), G(A0, B1, 7.2)], hx('2a5ed8'), hx('1a3ea8'), (0, 1))
    # 맨 위 남색 띠에 흰 글씨 POLICE
    s.grad([G(A0, B1, 12.4), G(A1, B1, 12.4), G(A1, B1, 20.0), G(A0, B1, 20.0)], hx('2450c8'), hx('122c88'), (0.2, 1))
    s.grad([G(A1, B0, 12.4), G(A1, B1, 12.4), G(A1, B1, 20.0), G(A1, B0, 20.0)], hx('183a9c'), hx('0c2064'), (0.2, 1))
    fw = G(A1, B1)[0] - G(A0, B1)[0]
    s.text_face('POLICE', (G(A0 + 0.05, B1)[0], G(A0 + 0.05, B1, 18.8)[1]), 0, 0, fw * 0.84 / 218.0, hx('ffffff'), font=FONT_B, size=60, shear=0.5)
    s.line([G(A0, B1, 12.4), G(A1, B1, 12.4)], hx('ffd428'), 4)
    # 현관: 파란 캐노피 + 유리문
    s.grad([G(0.38, B1, 0), G(0.54, B1, 0), G(0.54, B1, 5.0), G(0.38, B1, 5.0)], hx('18243c'), hx('3a5078'), (0, 1))
    s.box(0.34, 0.58, B1, B1 + 0.08, 1.4, (hx('3a6ee8'), hx('1c44b0')), (hx('1c44b0'), hx('102c7c')), (hx('6a98ff'), hx('3a6ee8')), 5.2)
    # 옥탑 + 금색 독수리 마크(경찰 마크): 금색 원판 위에 날개 편 독수리
    TA0, TA1, TB0, TB1, TH = 0.34, 0.60, 0.20, 0.44, 11.0
    s.box(TA0, TA1, TB0, TB1, TH, (hx('ffffff'), hx('d4e2f8')), (hx('b4c8ec'), hx('7c98cc')), (hx('6a88c4'), hx('40609c')), HH)
    cx, cy = G((TA0 + TA1) / 2, TB1, HH + 5.6)
    d.ellipse([cx - 34, cy - 30, cx + 34, cy + 30], fill=hx('ffd428')); d.ellipse([cx - 34, cy - 30, cx + 34, cy + 30], outline=hx('a86c00'), width=5)
    d.polygon([(cx - 28, cy - 8), (cx - 12, cy - 16), (cx, cy - 4), (cx + 12, cy - 16), (cx + 28, cy - 8), (cx + 14, cy + 4), (cx + 5, cy + 2), (cx, cy + 18), (cx - 5, cy + 2), (cx - 14, cy + 4)], fill=hx('1a2c7c'))
    d.ellipse([cx - 6, cy - 20, cx + 6, cy - 8], fill=hx('ffffff')); d.polygon([(cx + 4, cy - 14), (cx + 12, cy - 12), (cx + 4, cy - 9)], fill=hx('ff9a10'))
    x, y = G(TA0 + 0.04, TB0 + 0.04, HH + TH); d.line([(x, y), (x, y - 60)], fill=hx('e0e4ee'), width=5); d.polygon([(x + 2, y - 60), (x + 30, y - 52), (x + 2, y - 42)], fill=hx('ffffff')); d.ellipse([x + 9, y - 56, x + 19, y - 47], fill=hx('e03030'))
    # 순찰차: 흰 차 + 파란 줄 + 빨강·파랑 경광등
    vehicle(s, 0.56, 0.74, 0.30, 0.13, 3.6, (hx('ffffff'), hx('d0d8e8')), (hx('c0cadc'), hx('8a98b4')), (hx('ffffff'), hx('e4e8f2')), stripe=hx('2458e0'))
    s.box(0.63, 0.80, 0.755, 0.855, 2.6, (hx('bfe6ff'), hx('4a8ad0')), (hx('8ac0ec'), hx('3068a8')), (hx('ffffff'), hx('e0e6f0')), 4.6)
    x, y = G(0.715, 0.805, 7.6); d.rectangle([x - 13, y - 6, x - 1, y + 3], fill=hx('ff2a2a')); d.rectangle([x + 1, y - 6, x + 13, y + 3], fill=hx('2a6aff'))
    # 포돌이: 파란 제복 몸 + 노란 얼굴 + 파란 경찰모(금색 표지) + 큰 눈, 받침대 위
    px_, py_ = G(0.16, 0.84)
    d.polygon([(px_ - 22, py_ + 4), (px_ + 22, py_ + 4), (px_ + 18, py_ - 6), (px_ - 18, py_ - 6)], fill=hx('b8bccc'))
    d.rounded_rectangle([px_ - 16, py_ - 44, px_ + 16, py_ - 6], radius=8, fill=hx('2a5ee0')); d.rectangle([px_ - 3, py_ - 44, px_ + 3, py_ - 8], fill=hx('ffffff'))
    d.ellipse([px_ - 26, py_ - 40, px_ - 14, py_ - 28], fill=hx('ffffff')); d.ellipse([px_ + 14, py_ - 40, px_ + 26, py_ - 28], fill=hx('ffffff'))
    d.ellipse([px_ - 22, py_ - 86, px_ + 22, py_ - 42], fill=hx('ffd428'))
    d.ellipse([px_ - 13, py_ - 70, px_ - 3, py_ - 56], fill=hx('ffffff')); d.ellipse([px_ + 3, py_ - 70, px_ + 13, py_ - 56], fill=hx('ffffff'))
    d.ellipse([px_ - 10, py_ - 66, px_ - 5, py_ - 59], fill=hx('101018')); d.ellipse([px_ + 5, py_ - 66, px_ + 10, py_ - 59], fill=hx('101018'))
    d.pieslice([px_ - 24, py_ - 98, px_ + 24, py_ - 62], 180, 360, fill=hx('1c44b8')); d.rectangle([px_ - 28, py_ - 82, px_ + 28, py_ - 76], fill=hx('122c88')); d.ellipse([px_ - 5, py_ - 95, px_ + 5, py_ - 85], fill=hx('ffd428'))
    s.tree(0.94, 0.50, 0.7)
    return s.finish()
