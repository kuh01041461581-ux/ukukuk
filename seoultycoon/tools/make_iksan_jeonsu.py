"""익산 통합 전수관(중요무형문화재 전수관·시립국악원) — 조감도 기준: 회색 현대식 건물 두 동이 ㄱ자로 이어지고, 안뜰 앞에 검은 기와 팔작지붕의 한옥 누각.
국립익산박물관 자리(b26)를 대신한다."""
import os, sys, random
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, FONT_K
from make_iksan_civic import sign

def jeonsu():
    s = Sc(40, 3); G = s.G; s.ground(); d = ImageDraw.Draw(s.im); rs = random.Random(7)
    GR = (hx('e6e8ea'), hx('b6bac2')); GRR = (hx('bcc0c8'), hx('8a909c')); SLATE = (hx('555a66'), hx('363a46')); SLR = (hx('3e424e'), hx('252831'))
    WOOD = hx('8a5a34'); WOODL = hx('b07a48')
    s.shadow([G(0.16, 0.70), G(0.98, 0.70), G(1.0, 0.88), G(0.26, 0.90)], 100, 9)
    # 안뜰 박석
    for k in range(0, 9): s.line([G(0.30 + 0.07 * k, 0.32), G(0.30 + 0.07 * k, 0.96)], hx('c0bcd0', 110), 2)
    # 뒤 긴 동(회색 현대식 + 검은 기와 지붕)
    s.box(0.08, 0.94, 0.10, 0.30, 12.0, GR, GRR, SLATE)
    s.grad([G(0.06, 0.32, 12.0), G(0.96, 0.32, 12.0), G(0.96, 0.30, 13.6), G(0.06, 0.30, 13.6)], SLATE[0], SLATE[1], (0, 1))       # 처마
    for k in range(0, 14): s.line([G(0.10 + 0.06 * k, 0.30, 2.0), G(0.10 + 0.06 * k, 0.30, 10.0)], hx('8a909c', 130), 2)               # 세로 살
    s.line([G(0.08, 0.30, 6.0), G(0.94, 0.30, 6.0)], hx('9aa0ac', 120), 2)
    sign(s, 0.36, 0.80, 0.30, 7.0, 10.4, '익산통합전수관', (hx('f4f1e6'), hx('dcd6c0')), hx('4a3a24'), 0.62, 56)
    # 왼쪽 동
    s.box(0.08, 0.28, 0.30, 0.78, 9.5, GR, GRR, SLATE)
    s.grad([G(0.06, 0.80, 9.5), G(0.30, 0.80, 9.5), G(0.30, 0.78, 11.0), G(0.06, 0.78, 11.0)], SLATE[0], SLATE[1], (0, 1))
    for k in range(0, 6): s.line([G(0.28, 0.34 + 0.07 * k, 2.0), G(0.28, 0.34 + 0.07 * k, 8.0)], hx('7a8aa0', 150), 3)               # 유리 창
    # 한옥 누각: 돌 기단 + 목조 기둥 + 검은 기와 팔작지붕
    A0, A1, B0, B1 = 0.44, 0.88, 0.50, 0.88
    s.box(A0 - 0.03, A1 + 0.03, B0 - 0.03, B1 + 0.03, 2.6, (hx('c8c2b0'), hx('9a947f')), (hx('a8a290'), hx('7c7664')), (hx('d8d2c0'), hx('b0aa98')))
    for a in (A0 + 0.02, 0.58, 0.72, A1 - 0.02):
        for b in (B0 + 0.03, B1 - 0.02):
            s.line([G(a, b, 2.6), G(a, b, 10.6)], WOOD, 6); s.line([G(a - 0.006, b, 2.6), G(a - 0.006, b, 10.6)], WOODL, 2)
    for z in (10.0, 7.0):
        s.line([G(A0 + 0.02, B1 - 0.02, z), G(A1 - 0.02, B1 - 0.02, z)], WOOD, 4); s.line([G(A1 - 0.02, B0 + 0.03, z), G(A1 - 0.02, B1 - 0.02, z)], WOOD, 4)
    s.grad([G(A0 + 0.02, B1 - 0.02, 3.0), G(A1 - 0.02, B1 - 0.02, 3.0), G(A1 - 0.02, B1 - 0.02, 6.8), G(A0 + 0.02, B1 - 0.02, 6.8)], hx('6a4a30', 110), hx('4a3220', 110), (0, 1))   # 난간 그늘
    ov = 0.07; zr = 10.8; RZ = 6.2; bm = (B0 + B1) / 2
    s.grad([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.07, bm, zr + RZ), G(A0 + 0.07, bm, zr + RZ)], SLATE[0], SLATE[1], (0, -1), 3)
    s.grad([G(A1 + ov, B0 - ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.07, bm, zr + RZ)], SLR[0], SLR[1], (0.3, 1))
    for k in range(1, 22):
        t = k / 22; s.line([G(A0 - ov + (A1 - A0 + 2 * ov) * t, B1 + ov, zr), G(A0 + 0.07 + (A1 - A0 - 0.14) * t, bm, zr + RZ)], hx('20232b', 120), 2)
    s.line([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr)], hx('9aa0ac'), 4); s.line([G(A1 + ov, B0 - ov, zr), G(A1 + ov, B1 + ov, zr)], hx('7a808c'), 4)
    s.line([G(A0 + 0.07, bm, zr + RZ), G(A1 - 0.07, bm, zr + RZ)], hx('b4bac4'), 4)
    x, y = G(A0 - ov, B1 + ov, zr); d.polygon([(x, y), (x - 7, y - 4), (x + 2, y - 1)], fill=hx('9aa0ac'))                          # 처마 끝 들림
    x, y = G(A1 + ov, B1 + ov, zr); d.polygon([(x, y), (x + 7, y - 3), (x - 2, y - 1)], fill=hx('9aa0ac'))
    s.tree(0.02, 0.90, 0.8); s.tree(0.94, 0.62, 0.7)
    return s.finish()
