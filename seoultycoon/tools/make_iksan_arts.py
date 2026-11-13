"""익산예술의전당 — 사진 기준: 회백색 큰 판(패널)을 비스듬히 세운 공연장 정면(판마다 높이가 다름) + 파란 유리 세로 띠 + 청록 사선 줄무늬, 가운데 아래 유리 로비와 캐노피,
뒤에 솟은 흰 직육면체(간판 '익산예술의전당'), 왼쪽 낮은 파란 유리 별관, 앞 광장의 둥근 화단과 소나무."""
import os, sys, random, math
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, FONT_K
from make_iksan_civic import sign

def pine(s, a, b, h=1.0):
    G = s.G; d = ImageDraw.Draw(s.im); x, y = G(a, b)
    s.shadow([(x - 14 * h, y + 2), (x + 30 * h, y + 2), (x + 36 * h, y + 12 * h), (x - 6 * h, y + 12 * h)], 70, 5)
    d.rectangle([x - 2 * h, y - 14 * h, x + 2 * h, y + 2], fill=hx('6a4a30'))
    for k, (w, yy) in enumerate(((20, 16), (16, 30), (12, 44), (8, 56))):
        c0, c1 = ((52, 122, 70), (24, 80, 48))
        d.polygon([(x - w * h, y - yy * h + 14 * h), (x + w * h, y - yy * h + 14 * h), (x, y - yy * h - 8 * h)], fill=hx('2e7a48')); d.polygon([(x, y - yy * h - 8 * h), (x + w * h, y - yy * h + 14 * h), (x + w * 0.3 * h, y - yy * h + 14 * h)], fill=hx('1c5a34'))

def arts():
    s = Sc(60, 8); G = s.G; s.ground(); d = ImageDraw.Draw(s.im); rs = random.Random(5)
    PN = (hx('dfe3ea'), hx('a8b0c0')); PN2 = (hx('c4cad6'), hx('8e98ac')); PR = (hx('9aa4b8'), hx('6a748a')); TOP = (hx('f2f4f8'), hx('cfd5e0'))
    s.shadow([G(0.16, 0.78), G(0.98, 0.78), G(1.0, 0.90), G(0.26, 0.92)], 105, 10)
    # 광장: 박석(연회색 격자) + 둥근 화단
    for k in range(0, 9): s.line([G(0.10 + 0.10 * k, 0.60), G(0.10 + 0.10 * k, 0.99)], hx('b8bcd0', 110), 2)
    for k in range(0, 5): s.line([G(0.08, 0.60 + 0.10 * k), G(0.98, 0.60 + 0.10 * k)], hx('b8bcd0', 110), 2)
    x, y = G(0.34, 0.80); d.ellipse([x - 40, y - 12, x + 40, y + 12], fill=hx('c8a070')); d.ellipse([x - 34, y - 9, x + 34, y + 9], fill=hx('d8b080'))
    # ---- 뒤 타워: 흰 직육면체 + 간판 ----
    s.box(0.34, 0.80, 0.08, 0.30, 30.0, (hx('e8ecf2'), hx('b4bccc')), (hx('b4bccc'), hx('808aa0')), TOP)
    for z in (10.0, 17.0, 24.0): s.line([G(0.34, 0.30, z), G(0.80, 0.30, z)], hx('c0c6d2', 90), 2)
    sign(s, 0.37, 0.77, 0.30, 24.5, 28.2, '익산예술의전당', (hx('f2f4f8'), hx('dde2ea')), hx('4a4e58'), 0.62, 56)
    x, y = G(0.395, 0.30, 26.4); d.rectangle([x - 8, y - 5, x + 3, y + 5], fill=hx('e07030')); d.rectangle([x - 8, y - 5, x - 3, y + 5], fill=hx('2a9a4a'))        # 로고
    # ---- 왼쪽 별관: 낮은 건물 + 파란 유리 띠 ----
    s.box(0.04, 0.24, 0.34, 0.60, 8.0, (hx('f4f6fa'), hx('cfd5df')), (hx('cfd5df'), hx('a0a8b6')), TOP)
    s.grad([G(0.06, 0.60, 1.6), G(0.22, 0.60, 1.6), G(0.22, 0.60, 6.2), G(0.06, 0.60, 6.2)], hx('4aa0e8'), hx('1c5cb0'), (0.2, 1))
    for k in range(1, 5): s.line([G(0.06 + 0.16 * k / 5, 0.60, 1.6), G(0.06 + 0.16 * k / 5, 0.60, 6.2)], hx('e8f2ff'), 2)
    s.line([G(0.04, 0.60, 7.2), G(0.20, 0.60, 7.8)], hx('3a2a7a'), 3)
    # ---- 공연장 정면: 비스듬한 판들(판마다 높이·기울기가 다르다) ----
    A0, A1, BF = 0.22, 0.94, 0.62; n = 7; hs = [15, 12.5, 16, 12, 15.5, 13.5, 16.5]
    s.box(A0, A1, 0.28, BF - 0.04, 6.0, PN2, PR, TOP)                                           # 판 뒤 몸체
    for i in range(n):
        a0 = A0 + (A1 - A0) * i / n; a1 = A0 + (A1 - A0) * (i + 1) / n; hl = hs[i]; hr = hs[(i + 1) % n] if i % 2 == 0 else hs[i] - 2.0
        face = [G(a0, BF, 0), G(a1, BF, 0), G(a1, BF, hr), G(a0, BF, hl)]
        pc = PN if i % 2 == 0 else PN2
        s.grad(face, pc[0], pc[1], (0.2, 1), 3)
        s.line([G(a0, BF, 0), G(a0, BF, hl)], hx('5a6478'), 3)
        s.grad([G(a1, BF, 0), G(a1, BF - 0.05, 0), G(a1, BF - 0.05, hr), G(a1, BF, hr)], PR[0], PR[1], (0, 1))                            # 판 오른쪽 두께면
        s.line([G(a0, BF, hl), G(a1, BF, hr)], hx('ffffff'), 4)                                                                    # 윗모서리 하이라이트
    # 파란 유리 세로 띠(판 사이사이): 평행사변형으로 기울여 세운다
    for (ca, w, z1, z0) in ((0.29, 0.060, 15.5, 0.8), (0.50, 0.030, 13.0, 4.0), (0.615, 0.024, 11.0, 4.0), (0.82, 0.070, 16.0, 0.8)):
        sk = 0.018
        gl = [G(ca, BF + 0.002, z0), G(ca + w, BF + 0.002, z0), G(ca + w + sk, BF + 0.002, z1), G(ca + sk, BF + 0.002, z1)]
        s.grad(gl, hx('4eb0f8'), hx('1a4c9c'), (0.3, 1))
        for q in range(1, int((z1 - z0) / 1.6)): zz = z0 + 1.6 * q; s.line([G(ca + sk * (zz - z0) / (z1 - z0), BF + 0.003, zz), G(ca + w + sk * (zz - z0) / (z1 - z0), BF + 0.003, zz)], hx('0c2c70', 150), 2)
        s.line(gl[:2] + [gl[2], gl[3], gl[0]], hx('e8f4ff', 200), 2)
    # 청록 사선 줄무늬(판을 가로지르는 가는 선)
    for (a0, z0, a1, z1) in ((0.24, 12.0, 0.46, 7.0), (0.26, 4.5, 0.44, 5.5), (0.50, 9.5, 0.70, 5.0), (0.56, 4.0, 0.80, 6.0), (0.66, 13.5, 0.90, 11.5), (0.72, 7.0, 0.94, 3.0), (0.40, 15.0, 0.52, 10.0)):
        s.line([G(a0, BF + 0.004, z0), G(a1, BF + 0.004, z1)], hx('0aa898'), 6); s.line([G(a0, BF + 0.004, z0 + 0.6), G(a1, BF + 0.004, z1 + 0.6)], hx('7af0e0'), 3)
    # 가운데 유리 로비 + 캐노피
    s.grad([G(0.44, BF + 0.012, 0), G(0.68, BF + 0.012, 0), G(0.68, BF + 0.012, 5.2), G(0.44, BF + 0.012, 5.2)], hx('2a3c60'), hx('0e1830'), (0.2, 1))
    for k in range(1, 12): s.line([G(0.44 + 0.24 * k / 12, BF + 0.013, 0), G(0.44 + 0.24 * k / 12, BF + 0.013, 5.2)], hx('8ec4f0', 200), 2)
    for z in (1.8, 3.4): s.line([G(0.44, BF + 0.013, z), G(0.68, BF + 0.013, z)], hx('8ec4f0', 200), 2)
    s.box(0.40, 0.72, BF, BF + 0.08, 0.9, (hx('dfe3ea'), hx('b0b8c6')), (hx('b0b8c6'), hx('8890a0')), (hx('f4f6fa'), hx('d4d8e2')), 5.4)
    # 소나무 3그루(둥근 화단 위) + 가로수 + 벤치
    pine(s, 0.30, 0.80, 0.95); pine(s, 0.36, 0.84, 1.10); pine(s, 0.40, 0.78, 0.85)
    pine(s, 0.97, 0.66, 0.5)
    for k in range(3): x, y = G(0.56 + 0.10 * k, 0.94); d.rectangle([x - 8, y - 4, x + 8, y], fill=hx('7a5a3a')); d.rectangle([x - 8, y - 9, x + 8, y - 6], fill=hx('8a6a48'))
    return s.finish()
