"""익산 공공·생활 건물 4종(8배 해상도 렌더 → 면적 평균): 경찰서(b10) 은행(b12) 성당(b13=나바위성당) 도서관(b20)
make_iksan_buildings.Sc 를 그대로 쓴다. → work/pak_mod/{b10,b12,b13,b20}.png   사용: python3 -I tools/make_iksan_civic.py [미리보기.png]"""
import os, sys, math, random
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, lerp, W, S, out, FONT_K, FONT_B

def sign(s, a0, a1, b, z0, z1, txt, bg, fg, kk=0.70, size=60):
    G = s.G
    s.grad([G(a0, b, z0), G(a1, b, z0), G(a1, b, z1), G(a0, b, z1)], bg[0], bg[1], (0.2, 1))
    fw = G(a1 - 0.02, b)[0] - G(a0 + 0.02, b)[0]
    s.text_face(txt, (G(a0 + 0.02, b)[0], G(a0 + 0.02, b, z1 - 0.5)[1]), 0, 0, fw * kk / (size * 0.55 * len(txt)), fg, font=FONT_K, size=size, shear=0.5)

# ---------------- b10 익산경찰서 (사진: 흰 석재 5층 장방형 + 청록 유리 창 격자 + 중앙 안테나탑 + 파란 현관 캐노피) ----------------
def police():
    s = Sc(59, 10); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1, HH = 0.05, 0.93, 0.28, 0.54, 22.0
    s.shadow([G(0.10, B1), G(A1, B1), G(1.0, 0.68), G(0.20, 0.70)], 100, 10)
    s.box(A0, A1, B0, B1, HH, (hx('f6f6f2'), hx('cfcfc8')), (hx('d6d6d0'), hx('a4a49c')), (hx('dcdcd6'), hx('b8b8b0')))
    # 옥상 난간 아래 작은 창 띠 + 4개 층의 청록 유리 창(흰 프레임) 격자
    s.grad([G(A0, B1, 19.6), G(A1, B1, 19.6), G(A1, B1, 21.2), G(A0, B1, 21.2)], hx('4a5260'), hx('2a303c'), (0, 1))
    for k in range(1, 40): s.line([G(A0 + (A1 - A0) * k / 40, B1, 19.6), G(A0 + (A1 - A0) * k / 40, B1, 21.2)], hx('e8e8e0', 200), 2)
    cols = 13
    for fl in range(4):
        z0 = 4.4 + fl * 3.8; z1 = z0 + 2.8
        for i in range(cols):
            a0 = A0 + 0.03 + (A1 - A0 - 0.06) * (i + 0.12) / cols; a1 = A0 + 0.03 + (A1 - A0 - 0.06) * (i + 0.88) / cols
            s.grad([G(a0, B1, z0), G(a1, B1, z0), G(a1, B1, z1), G(a0, B1, z1)], hx('6ab4a0'), hx('1e5a52'), (0.2, 1))
            s.line([G((a0 + a1) / 2, B1, z0), G((a0 + a1) / 2, B1, z1)], hx('e8f0ec', 170), 2)
    for fl in range(4):
        z0 = 4.4 + fl * 3.8; z1 = z0 + 2.8
        for i in range(3):
            b0 = B0 + 0.03 + (B1 - B0 - 0.06) * (i + 0.12) / 3; b1 = B0 + 0.03 + (B1 - B0 - 0.06) * (i + 0.88) / 3
            s.grad([G(A1, b0, z0), G(A1, b1, z0), G(A1, b1, z1), G(A1, b0, z1)], hx('4a9488'), hx('164640'), (0.2, 1))
    # 간판 띠 '익산경찰서'(2층 위 흰 벽 위의 짙은 글씨)
    fw = G(0.66, B1)[0] - G(0.34, B1)[0]
    s.text_face('익산경찰서', (G(0.36, B1)[0], G(0.36, B1, 19.4)[1]), 0, 0, fw * 0.60 / 320.0, hx('1c3a5c'), font=FONT_K, size=60, shear=0.5)
    # 1층 중앙 유리 현관 + 파란 캐노피
    s.grad([G(0.40, B1, 0), G(0.60, B1, 0), G(0.60, B1, 4.0), G(0.40, B1, 4.0)], hx('9cd4cc'), hx('2a6a64'), (0, 1))
    s.box(0.36, 0.64, B1, B1 + 0.07, 1.6, (hx('2a58b4'), hx('163a84')), (hx('1c3c88'), hx('102a64')), (hx('4a78d0'), hx('2a58b4')), z0=4.2)
    s.line([G(0.36, B1 + 0.07, 4.2), G(0.64, B1 + 0.07, 4.2)], hx('e8f0ff'), 2)
    # 중앙 안테나 철탑(격자 탑)
    cx, cy = G(0.50, 0.41, HH); d.rectangle([cx - 22, cy - 30, cx + 22, cy], fill=hx('d0d0c8')); d.polygon([(cx - 22, cy - 30), (cx + 22, cy - 30), (cx + 15, cy - 38), (cx - 15, cy - 38)], fill=hx('e4e4de'))
    for lv in range(5):
        y0 = cy - 38 - lv * 17; y1 = y0 - 17; hw0 = 13 - lv * 1.4; hw1 = 13 - (lv + 1) * 1.4
        d.line([(cx - hw0, y0), (cx - hw1, y1)], fill=hx('a8aab4'), width=3); d.line([(cx + hw0, y0), (cx + hw1, y1)], fill=hx('a8aab4'), width=3)
        d.line([(cx - hw0, y0), (cx + hw1, y1)], fill=hx('8a8c98'), width=2); d.line([(cx + hw0, y0), (cx - hw1, y1)], fill=hx('8a8c98'), width=2); d.line([(cx - hw1, y1), (cx + hw1, y1)], fill=hx('c4c6d0'), width=3)
    d.line([(cx, cy - 38 - 5 * 17), (cx, cy - 38 - 5 * 17 - 24)], fill=hx('d8dae2'), width=3)
    # 왼쪽 별채(흰 단층) + 오른쪽 주차장(차량 몇 대) + 앞 화단 + 가로수
    s.box(0.0, 0.12, 0.62, 0.78, 4.4, (hx('f4f4f0'), hx('c8c8c0')), (hx('c8c8c0'), hx('98988e')), (hx('e0e0da'), hx('c0c0b8')))
    for (a, b, col) in ((0.62, 0.82, 'd8dce4'), (0.74, 0.82, '3a4458'), (0.84, 0.76, 'e8e8ee')):
        s.box(a, a + 0.08, b, b + 0.05, 2.4, (hx(col), hx('7a8090')), (hx('9aa0ac'), hx('5c6270')), (hx('f0f2f6'), hx('c4c8d2')))
    for k in range(7):
        x, y = G(0.18 + 0.075 * k, 0.72); d.ellipse([x - 7, y - 5, x + 7, y + 4], fill=hx('3e7a3c')); d.ellipse([x - 4, y - 9, x + 3, y - 3], fill=hx('5ca058'))
    s.line([G(0.14, 0.80), G(0.58, 0.80)], hx('c8c6d8'), 4)
    s.tree(0.03, 0.88, 0.6); s.tree(0.50, 0.92, 0.6); s.tree(0.95, 0.40, 0.6)
    return s.finish()

# ---------------- b12 은행 (NH농협은행 익산: 흰 석재 + 기둥 + 녹색 간판) ----------------
def bank():
    s = Sc(55, 12); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1, HH = 0.12, 0.86, 0.22, 0.62, 15.0
    s.shadow([G(0.16, B1), G(A1, B1), G(1.0, 0.72), G(0.28, 0.74)], 100, 10)
    s.box(A0, A1, B0, B1, HH, (hx('f6f2e6'), hx('d4ccb4')), (hx('d4ccb8'), hx('a49c86')), (hx('e6e0cc'), hx('c4bca4')))
    s.win_grid_L(A0 + 0.04, A1 - 0.04, B1, 4.4, 11.0, 7, 2, hx('9cc8d8'), hx('2c5a74'), pad=0.22)
    s.win_grid_R(B0 + 0.03, B1 - 0.03, A1, 4.4, 11.0, 3, 2, hx('78a8c0'), hx('204860'), pad=0.22)
    # 앞 기둥 열주(고전식) + 처마
    for k in range(6): s.line([G(0.20 + 0.52 * k / 5, B1 + 0.05, 0), G(0.20 + 0.52 * k / 5, B1 + 0.05, 11.4)], hx('fffaf0'), 6); s.line([G(0.20 + 0.52 * k / 5 + 0.012, B1 + 0.05, 0), G(0.20 + 0.52 * k / 5 + 0.012, B1 + 0.05, 11.4)], hx('b8b098', 160), 2)
    s.grad([G(0.16, B1 + 0.06, 11.4), G(0.76, B1 + 0.06, 11.4), G(0.76, B1 + 0.06, 12.6), G(0.16, B1 + 0.06, 12.6)], hx('f2eee0'), hx('c0b8a0'), (0, 1))
    s.grad([G(0.16, B1 + 0.06, 12.6), G(0.76, B1 + 0.06, 12.6), G(0.76, B1 - 0.02, 14.0), G(0.16, B1 - 0.02, 14.0)], hx('e8e2d0'), hx('b4ac94'), (0, 1))
    s.grad([G(0.20, B1 + 0.05, 0), G(0.72, B1 + 0.05, 0), G(0.72, B1 + 0.05, 3.4), G(0.20, B1 + 0.05, 3.4)], hx('3a4a58'), hx('5a6c7c'), (0, 1))
    # 간판: NH농협은행 (녹색 바탕)
    sign(s, 0.18, 0.78, B1 + 0.06, 12.9, 15.6, 'NH농협은행', (hx('00963c'), hx('006a2a')), hx('ffffff'), 0.62)
    x, y = G(0.82, B1, 11.0); d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=hx('00963c')); d.arc([x - 6, y - 6, x + 6, y + 6], 200, 340, fill=hx('f0c800'), width=3)
    # 옆 ATM 부스 + 국기 게양대
    s.box(0.88, 0.97, 0.30, 0.44, 5.0, (hx('c8e0ec'), hx('5c8aa4')), (hx('9cc0d4'), hx('3c6a88')), (hx('f0f4f8'), hx('c8d0d8')))
    x, y = G(0.10, 0.86); d.line([(x, y), (x, y - 62)], fill=hx('d8dce4'), width=4); d.polygon([(x + 2, y - 62), (x + 30, y - 56), (x + 2, y - 46)], fill=hx('f6f6f6'))
    d.ellipse([x + 10, y - 58, x + 20, y - 50], fill=hx('d83030'))
    s.tree(0.04, 0.72, 0.7); s.tree(0.94, 0.22, 0.7)
    return s.finish()

# ---------------- b13 나바위성당 (사진: 붉은 벽돌 + 회색 석재 모서리, 검은 기와 겹지붕 장방 본당, 오른쪽 앞 높은 종탑) ----------------
def church():
    s = Sc(69, 13); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1 = 0.08, 0.76, 0.30, 0.56
    BR = (hx('c05a46'), hx('84382c')); BR2 = (hx('9c4636'), hx('642a22')); ST = (hx('b8b4a8'), hx('807c70'))
    s.shadow([G(0.12, B1 + 0.14), G(A1 + 0.12, B1 + 0.14), G(1.0, 0.72), G(0.22, 0.74)], 100, 10)
    # 본당 벽: 낮은 붉은 벽돌 + 처마 아래 가는 창 띠(잔 창 열) → 위로 붉은 벽
    s.box(A0, A1, B0, B1, 8.6, BR, BR2, (hx('7a3a2e'), hx('5a2a22')))
    for z in range(1, 9): s.line([G(A0, B1, z), G(A1, B1, z)], hx('6a2e24', 70), 2)
    for k in range(0, 17):
        a = A0 + 0.04 + (A1 - A0 - 0.08) * k / 16
        s.grad([G(a - 0.008, B1, 5.4), G(a + 0.008, B1, 5.4), G(a + 0.008, B1, 7.4), G(a - 0.008, B1, 7.4)], hx('e8e4d4'), hx('607890'), (0, 1))
    # 앞 낮은 회랑(붉은 기둥 + 큰 창) + 그 위 한식 아래 지붕
    for k in range(0, 9):
        a = A0 + 0.02 + (A1 - A0 - 0.04) * k / 8
        s.line([G(a, B1 + 0.06, 0), G(a, B1 + 0.06, 4.2)], hx('8a3e30'), 4)
    for k in range(0, 8):
        a0 = A0 + 0.02 + (A1 - A0 - 0.04) * (k + 0.2) / 8; a1 = A0 + 0.02 + (A1 - A0 - 0.04) * (k + 0.8) / 8
        s.grad([G(a0, B1 + 0.06, 0.5), G(a1, B1 + 0.06, 0.5), G(a1, B1 + 0.06, 3.8), G(a0, B1 + 0.06, 3.8)], hx('7a8aa0'), hx('2a3448'), (0, 1))
    s.grad([G(A0 - 0.02, B1 + 0.12, 4.0), G(A1 + 0.04, B1 + 0.12, 4.0), G(A1 + 0.04, B1 - 0.01, 8.8), G(A0 - 0.02, B1 - 0.01, 8.8)], hx('4c4e5a'), hx('22242e'), (0, -1), 3)
    s.line([G(A0 - 0.02, B1 + 0.12, 4.0), G(A1 + 0.04, B1 + 0.12, 4.0)], hx('b8b4c8'), 4)
    # 본당 큰 지붕: 앞면(밝은 회색 기와) · 뒷면 · 박공 측면 + 기와 줄무늬 + 용마루
    zr = 8.8; RZ = 7.0; bm = (B0 + B1) / 2
    s.grad([G(A0 - 0.03, B0 - 0.02, zr), G(A1 + 0.03, B0 - 0.02, zr), G(A1, bm, zr + RZ), G(A0, bm, zr + RZ)], hx('3c3e48'), hx('20222a'), (0, 1))
    s.grad([G(A0 - 0.03, B1 + 0.02, zr), G(A1 + 0.03, B1 + 0.02, zr), G(A1, bm, zr + RZ), G(A0, bm, zr + RZ)], hx('767888'), hx('3a3c4a'), (0, -1), 3)
    s.grad([G(A0 - 0.03, B0 - 0.02, zr), G(A0 - 0.03, B1 + 0.02, zr), G(A0, bm, zr + RZ)], hx('4a4c58'), hx('2a2c36'), (0.3, 1))
    for k in range(1, 30):
        t = k / 30; s.line([G(A0 - 0.03 + (A1 - A0 + 0.06) * t, B1 + 0.02, zr), G(A0 + (A1 - A0) * t, bm, zr + RZ)], hx('24262e', 130), 2)
    s.line([G(A0 - 0.03, B1 + 0.02, zr), G(A1 + 0.03, B1 + 0.02, zr)], hx('d8d4e4'), 4); s.line([G(A0, bm, zr + RZ), G(A1, bm, zr + RZ)], hx('c4c0d4'), 5)
    # 박공 끝 작은 십자가
    x, y = G(A0, bm, zr + RZ); d.line([(x, y), (x, y - 20)], fill=hx('e8e4dc'), width=4); d.line([(x - 6, y - 14), (x + 6, y - 14)], fill=hx('e8e4dc'), width=4)
    # 오른쪽 앞 종탑(높은 사각탑): 붉은 벽돌 + 회색 돌띠 + 둥근 창 + 맨 위 루버 아치창 + 붉은 뾰족 지붕 + 십자가
    TA0, TA1, TB0, TB1, TH = 0.66, 0.90, 0.54, 0.78, 21.0
    s.shadow([G(TA0, TB1), G(TA1, TB1), G(TA1 + 0.10, TB1 + 0.10), G(TA0 + 0.08, TB1 + 0.10)], 90, 6)
    s.box(TA0, TA1, TB0, TB1, TH, BR, BR2, (hx('9a4436'), hx('6a2c24')))
    for z in (5.0, 10.0, 15.0): s.grad([G(TA0, TB1, z), G(TA1, TB1, z), G(TA1, TB1, z + 0.9), G(TA0, TB1, z + 0.9)], ST[0], ST[1], (0, 1)); s.grad([G(TA1, TB0, z), G(TA1, TB1, z), G(TA1, TB1, z + 0.9), G(TA1, TB0, z + 0.9)], hx('9c988c'), hx('686458'), (0, 1))
    for a in (TA0, TA1 - 0.012): s.grad([G(a, TB1, 0), G(a + 0.012, TB1, 0), G(a + 0.012, TB1, TH), G(a, TB1, TH)], ST[0], ST[1], (0.2, 1))
    s.grad([G(TA0 + 0.05, TB1, 0), G(TA0 + 0.17, TB1, 0), G(TA0 + 0.17, TB1, 4.0), G(TA0 + 0.11, TB1, 5.4), G(TA0 + 0.05, TB1, 4.0)], hx('2a2220'), hx('141010'), (0, 1))
    xx, yy = G(TA0 + 0.12, TB1, 11.6); d.ellipse([xx - 12, yy - 12, xx + 12, yy + 12], fill=hx('e0e4dc')); d.ellipse([xx - 8, yy - 8, xx + 8, yy + 8], outline=hx('9a9a90'), width=2)
    for k in range(2):
        a = TA0 + 0.06 + k * 0.09
        s.grad([G(a, TB1, 16.6), G(a + 0.05, TB1, 16.6), G(a + 0.05, TB1, 19.4), G(a + 0.025, TB1, 20.4), G(a, TB1, 19.4)], hx('dcd8c8'), hx('6a6a60'), (0, 1))
        for z in (17.2, 17.9, 18.6): s.line([G(a, TB1, z), G(a + 0.05, TB1, z)], hx('5a5a52'), 2)
    px, py = G((TA0 + TA1) / 2, (TB0 + TB1) / 2, TH + 0.2)
    s.grad([G(TA0 - 0.01, TB1 + 0.01, TH), G(TA1 + 0.01, TB1 + 0.01, TH), (px * 1.0, py - 7.0 * S * 1.0 / 1)], hx('c8604a'), hx('8c3828'), (0, 1)) if False else None
    apex = (px, py - 64)
    s.grad([G(TA0 - 0.01, TB1 + 0.01, TH), G(TA1 + 0.01, TB1 + 0.01, TH), apex], hx('d0684c'), hx('8a382a'), (0, 1))
    s.grad([G(TA1 + 0.01, TB0 - 0.01, TH), G(TA1 + 0.01, TB1 + 0.01, TH), apex], hx('a84a38'), hx('6a281e'), (0.3, 1))
    d.line([apex, (apex[0], apex[1] - 22)], fill=hx('f0ece4'), width=5); d.line([(apex[0] - 8, apex[1] - 14), (apex[0] + 8, apex[1] - 14)], fill=hx('f0ece4'), width=5)
    # 출입 아치(종탑 아래 회색 돌 아치) + 마당 가을 나무
    s.tree(0.04, 0.86, 0.7); s.tree(0.50, 0.94, 0.6); s.tree(0.96, 0.22, 0.7)
    return s.finish()

# ---------------- b20 도서관 (사진: 흰 모더니즘 3단 매스 · 층마다 내민 흰 슬래브 + 세로 핀 열주 + 초승달 조형물) ----------------
def library():
    s = Sc(68, 20); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    s.shadow([G(0.10, 0.74), G(0.92, 0.74), G(1.0, 0.82), G(0.18, 0.84)], 100, 10)
    GL = (hx('b4dcea'), hx('3a6a92')); WL = (hx('fbfbf8'), hx('d0d0ca')); WR = (hx('d8d8d2'), hx('a6a69e')); RF = (hx('ececea'), hx('c4c4be'))
    # 1층: 안쪽으로 들어간 유리 로비 + 흰 필로티 기둥
    s.box(0.14, 0.86, 0.24, 0.52, 5.0, GL, (hx('8ab4c8'), hx('2c5878')), RF)
    for k in range(0, 10): s.line([G(0.14 + 0.72 * k / 9, 0.60, 0), G(0.14 + 0.72 * k / 9, 0.60, 6.4)], hx('fdfdfa'), 5)
    # 2층 큰 슬래브(앞으로 내밈) + 앞 열주 핀
    s.box(0.08, 0.90, 0.22, 0.60, 6.4, WL, WR, RF, z0=5.0)
    s.grad([G(0.12, 0.60, 6.2), G(0.86, 0.60, 6.2), G(0.86, 0.60, 10.4), G(0.12, 0.60, 10.4)], GL[0], GL[1], (0.2, 1))
    for k in range(0, 22): s.line([G(0.12 + 0.74 * k / 21, 0.60, 5.4), G(0.12 + 0.74 * k / 21, 0.60, 11.0)], hx('fdfdfa'), 3)
    s.win_grid_R(0.22 + 0.03, 0.60 - 0.03, 0.90, 6.2, 10.4, 3, 1, hx('90bcd0'), hx('2c587c'), pad=0.18)
    # 3층: 뒤로 물러난 매스 + 세로 핀(루버) + 오른쪽 짙은 유리
    s.box(0.14, 0.82, 0.24, 0.48, 6.6, WL, WR, RF, z0=11.4)
    s.grad([G(0.30, 0.48, 12.0), G(0.78, 0.48, 12.0), G(0.78, 0.48, 17.4), G(0.30, 0.48, 17.4)], hx('4a6a8c'), hx('1c3252'), (0.2, 1))
    for k in range(0, 17): s.line([G(0.30 + 0.48 * k / 16, 0.485, 12.0), G(0.30 + 0.48 * k / 16, 0.485, 17.6)], hx('f8f8f4'), 3)
    s.grad([G(0.14, 0.48, 12.0), G(0.28, 0.48, 12.0), G(0.28, 0.48, 17.0), G(0.14, 0.48, 17.0)], hx('f4f4ee'), hx('cacac2'), (0.2, 1))
    for z in (13.6, 15.2): s.line([G(0.14, 0.485, z), G(0.28, 0.485, z)], hx('b8b8b0'), 2)
    s.grad([G(0.14, 0.50, 17.4), G(0.84, 0.50, 17.4), G(0.84, 0.50, 18.4), G(0.14, 0.50, 18.4)], hx('f6f6f2'), hx('c8c8c2'), (0, 1))
    # 간판: 초록·남색 '익산도서관'
    s.grad([G(0.16, 0.50, 18.0), G(0.50, 0.50, 18.0), G(0.50, 0.50, 20.6), G(0.16, 0.50, 20.6)], hx('1c2e56'), hx('0e1a38'), (0.2, 1))
    fw = G(0.50, 0.50)[0] - G(0.17, 0.50)[0]
    s.text_face('익산도서관', (G(0.17, 0.50)[0], G(0.17, 0.50, 20.2)[1]), 0, 0, fw * 0.74 / 320.0, hx('f8f8f0'), font=FONT_K, size=60, shear=0.5)
    # 현수막(오른쪽 세로 배너) + 앞마당 초승달 조형물 + 연석 화단 간판석
    s.grad([G(0.58, 0.50, 11.6), G(0.68, 0.50, 11.6), G(0.68, 0.50, 18.4), G(0.58, 0.50, 18.4)], hx('f6e8d0'), hx('e0a878'), (0.2, 1))
    for k in range(5): s.line([G(0.59, 0.505, 12.6 + k * 1.2), G(0.67, 0.505, 12.6 + k * 1.2)], hx('c85a3a', 160), 3)
    s.box(0.12, 0.40, 0.78, 0.84, 1.8, (hx('4a5058'), hx('2a2e34')), (hx('343840'), hx('1a1c20')), (hx('7a8a60'), hx('5a6a44')))
    fw = G(0.40, 0.84)[0] - G(0.14, 0.84)[0]
    s.text_face('익산도서관', (G(0.14, 0.84)[0], G(0.14, 0.84, 1.6)[1]), 0, 0, fw * 0.72 / 320.0, hx('4ad07a'), font=FONT_K, size=60, shear=0.5)
    cx, cy = G(0.62, 0.86, 0)
    d.pieslice([cx - 34, cy - 66, cx + 34, cy + 2], 40, 320, fill=hx('f2ead6')); d.pieslice([cx - 22, cy - 62, cx + 46, cy + 6], 20, 340, fill=hx('7da46a'))
    d.arc([cx - 34, cy - 66, cx + 34, cy + 2], 40, 320, fill=hx('d4c8a8'), width=3)
    d.ellipse([cx + 30, cy - 12, cx + 52, cy + 4], fill=hx('f6f2e8'))
    # 왼쪽 태양광 지붕 주차장 + 차
    s.box(0.0, 0.10, 0.62, 0.84, 0.7, (hx('3a4a68'), hx('1e2a44')), (hx('2a3858'), hx('141c34')), (hx('4a5c84'), hx('2c3a5c')), z0=5.0)
    for (a, b) in ((0.01, 0.70), (0.01, 0.78)):
        s.box(a, a + 0.07, b, b + 0.05, 2.2, (hx('e4e8ee'), hx('9aa2b0')), (hx('b4bcc8'), hx('7a8494')), (hx('f2f4f8'), hx('cfd4de')))
    s.tree(0.50, 0.66, 0.6); s.tree(0.93, 0.88, 0.7); s.tree(0.02, 0.94, 0.6)
    return s.finish()

if __name__ == '__main__':
    items = {'b10': police, 'b12': bank, 'b13': church, 'b20': library}
    sheet = Image.new('RGBA', (4 * (96 * 6 + 20), 72 * 6), (150, 70, 160, 255)); x = 0
    for n, fn in items.items():
        im = fn(); im.save(f'{out}/{n}.png'); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im); sheet.paste(bg.resize((im.width * 6, im.height * 6), Image.NEAREST), (x, 0)); x += 96 * 6 + 20
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('공공·생활 건물 →', out)
