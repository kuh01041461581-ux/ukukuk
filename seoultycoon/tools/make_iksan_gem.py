"""왕궁보석박물관(왕궁보석테마관광지의 보석박물관) — 사진 기준: 넓은 흰 본체 + 주황 띠 외벽, 사선으로 솟은 회색 지붕,
정면 왼쪽의 파란 유리 피라미드 입구, 앞 광장의 다이아몬드 분수. 바닥부터 쌓는다: 광장 → 본체 → 띠·창 → 지붕 → 피라미드 → 분수·나무."""
import os, sys, random, math
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, FONT_K
from make_iksan_civic import sign

def gem():
    s = Sc(56, 9); G = s.G; s.ground(); d = ImageDraw.Draw(s.im); rs = random.Random(9)
    WH = (hx('f2f3f6'), hx('c4c8d2')); WHR = (hx('cdd1da'), hx('9ba1b0')); ROOF = (hx('8d94a4'), hx('5d6475')); ROOFR = (hx('6f7688'), hx('474d5d'))
    s.shadow([G(0.14, 0.62), G(0.98, 0.62), G(1.0, 0.82), G(0.22, 0.84)], 100, 9)
    # 1) 광장 박석
    for k in range(0, 10): s.line([G(0.10 + 0.09 * k, 0.56), G(0.10 + 0.09 * k, 0.98)], hx('c4c0d4', 110), 2)
    # 2) 본체: 넓은 흰 직육면체
    A0, A1, B0, B1, H = 0.12, 0.90, 0.16, 0.56, 17.0
    s.box(A0, A1, B0, B1, H, WH, WHR, ROOF)
    # 3) 주황 띠 외벽 + 어두운 창띠(정면 B1)
    s.grad([G(A0, B1, 4.0), G(A1, B1, 4.0), G(A1, B1, 10.0), G(A0, B1, 10.0)], hx('f08a3a'), hx('c25a18'), (0, 1))
    for k in range(1, 18): s.line([G(A0 + (A1 - A0) * k / 18, B1, 4.0), G(A0 + (A1 - A0) * k / 18, B1, 9.0)], hx('a8480e', 150), 2)
    s.grad([G(A0, B1, 10.6), G(A1, B1, 10.6), G(A1, B1, 13.6), G(A0, B1, 13.6)], hx('3a4a6a'), hx('1e2840'), (0, 1))                 # 창띠
    for k in range(1, 26): s.line([G(A0 + (A1 - A0) * k / 26, B1, 10.6), G(A0 + (A1 - A0) * k / 26, B1, 13.6)], hx('8aa0c8', 120), 2)
    s.grad([G(A1, B0, 3.0), G(A1, B1, 3.0), G(A1, B1, 9.0), G(A1, B0, 9.0)], hx('e0e3ea'), hx('b0b6c4'), (0, 1))                       # 오른쪽 벽 장식 띠
    # 4) 사선 지붕: 앞쪽이 낮고 뒤로 솟는 회색 면 + 가운데 유리 천창
    zr, RZ = H, 5.5
    s.grad([G(A0 - 0.02, B1 + 0.02, zr), G(A1 + 0.02, B1 + 0.02, zr), G(A1 - 0.02, B0 + 0.06, zr + RZ), G(A0 + 0.02, B0 + 0.06, zr + RZ)], ROOF[0], ROOF[1], (0, -1), 3)
    s.grad([G(A1 + 0.02, B1 + 0.02, zr), G(A1 + 0.02, B0 - 0.02, zr), G(A1 - 0.02, B0 + 0.06, zr + RZ)], ROOFR[0], ROOFR[1], (0.3, 1))
    s.grad([G(0.46, B1 - 0.02, zr + 1.2), G(0.80, B1 - 0.02, zr + 1.2), G(0.78, B0 + 0.10, zr + RZ - 0.8), G(0.48, B0 + 0.10, zr + RZ - 0.8)], hx('9ad0f0'), hx('3a78b8'), (0.2, 1))   # 천창
    for k in range(1, 6): t = k / 6; s.line([G(0.46 + 0.34 * t, B1 - 0.02, zr + 1.2), G(0.48 + 0.30 * t, B0 + 0.10, zr + RZ - 0.8)], hx('e8f4ff', 150), 2)
    s.line([G(A0 - 0.02, B1 + 0.02, zr), G(A1 + 0.02, B1 + 0.02, zr)], hx('d6dae4'), 4)
    sign(s, 0.50, 0.88, B1, 5.0, 9.4, '왕궁보석박물관', (hx('fff4dc'), hx('f0d8a8')), hx('7a3a10'), 0.62, 56)
    # 5) 파란 유리 피라미드 입구(정면 왼쪽, 앞으로 튀어나온다)
    PA0, PA1, PB0, PB1 = 0.14, 0.44, 0.52, 0.80; apex = G((PA0 + PA1) / 2, (PB0 + PB1) / 2, 21.0)
    s.grad([G(PA0, PB1, 0), G(PA1, PB1, 0), apex], hx('b8e4ff'), hx('3a86d0'), (0.2, 1))                                  # 앞(왼쪽) 면
    s.grad([G(PA1, PB1, 0), G(PA1, PB0, 0), apex], hx('5aa8e8'), hx('1f58a0'), (0.3, 1))                                  # 오른쪽 면
    for k in range(1, 6):                                                                                                  # 유리 격자
        t = k / 6; s.line([G(PA0 + (PA1 - PA0) * t, PB1, 0), apex], hx('e8f6ff', 130), 2); s.line([G(PA1, PB1 - (PB1 - PB0) * t, 0), apex], hx('b8dcff', 110), 2)
    for z in (4.0, 8.0, 12.0):
        f = 1 - z / 21.0; xa, ya = G(PA0 + (PA1 - PA0) * (1 - f) / 2, PB1 - (PB1 - PB0) * (1 - f) / 2, z), G(PA1 - (PA1 - PA0) * (1 - f) / 2, PB1 - (PB1 - PB0) * (1 - f) / 2, z)
        s.line([xa, G(PA1 - (PA1 - PA0) * (1 - f) / 2, PB1 - (PB1 - PB0) * (1 - f) / 2, z)], hx('e8f6ff', 120), 2)
    s.line([G(PA0, PB1, 0), apex, G(PA1, PB1, 0)], hx('f4fbff'), 4); s.line([G(PA1, PB1, 0), apex, G(PA1, PB0, 0)], hx('cfe8ff'), 3)
    # 6) 다이아몬드 분수(앞 오른쪽 광장): 둥근 못 + 푸른 보석
    x, y = G(0.70, 0.78); d.ellipse([x - 34, y - 11, x + 34, y + 11], fill=hx('9ad4f2')); d.ellipse([x - 28, y - 8, x + 28, y + 8], fill=hx('5aaee0'))
    d.polygon([(x, y - 30), (x + 13, y - 14), (x, y + 2), (x - 13, y - 14)], fill=hx('4a9ae8')); d.polygon([(x, y - 30), (x - 13, y - 14), (x, y - 14)], fill=hx('a8dcff')); d.polygon([(x, y - 30), (x + 13, y - 14), (x, y - 14)], fill=hx('7cc0f8'))
    d.line([(x - 13, y - 14), (x, y + 2), (x + 13, y - 14)], fill=hx('1f5aa8'), width=2)
    s.tree(0.04, 0.84, 0.8); s.tree(0.94, 0.70, 0.7); s.tree(0.90, 0.92, 0.7)
    return s.finish()
