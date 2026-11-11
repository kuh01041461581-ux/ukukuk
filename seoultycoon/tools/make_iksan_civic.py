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

# ---------------- b12 은행: 원본 도트 그대로 + NH농협 마크(파랑·초록 'NH')만 얹는다 ----------------
def pak_entry(name):
    import struct, io
    d = open(root_pak, 'rb').read(); total, n = struct.unpack('>II', d[:8]); base = 8 + n * 16
    for i in range(n):
        e = d[8 + i * 16: 8 + (i + 1) * 16]
        if e[:8].split(b'\0')[0].decode() == name:
            off, sz = struct.unpack('>II', e[8:16]); return Image.open(io.BytesIO(d[base + off: base + off + sz])).convert('RGBA')
root_pak = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'work/st2/data1.pak')
def nh_badge(im, x, y):
    """10x7 흰 판에 파란 N + 초록 H"""
    B, Gc, Wt = (26, 86, 168, 255), (0, 150, 60, 255), (255, 255, 255, 255)
    px = im.load()
    for yy in range(7):
        for xx in range(10): px[x + xx, y + yy] = Wt
    N = ['1001', '1101', '1011', '1001', '1001']; Hh = ['101', '101', '111', '101', '101']
    for r in range(5):
        for c in range(4):
            if N[r][c] == '1': px[x + 1 + c, y + 1 + r] = B
        for c in range(3):
            if Hh[r][c] == '1': px[x + 6 + c, y + 1 + r] = Gc
def bank():
    im = pak_entry('b12.d')
    nh_badge(im, 17, 9)                                                       # 높은 파란 간판 기둥 위
    return im

# ---------------- b13 나바위성당: 앞(출입·첨탑)은 서양식 붉은 벽돌 고딕, 뒤 예배당은 한옥식(검은 기와 팔작지붕) ----------------
def church():
    s = Sc(69, 13); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    BR = (hx('c45e48'), hx('883a2e')); BR2 = (hx('a04838'), hx('662c24')); ST = (hx('c4c0b4'), hx('86827a'))
    s.shadow([G(0.14, 0.86), G(0.84, 0.86), G(1.0, 0.76), G(0.30, 0.90)], 100, 10)
    # ---- 뒤쪽 한옥 예배당(앞뒤로 긴 건물): 붉은 벽 + 격자 창, 아래 처마 + 큰 팔작지붕 ----
    a0, a1, b0, b1, zw = 0.34, 0.74, 0.08, 0.66, 5.8
    s.box(a0, a1, b0, b1, zw, (hx('b86a52'), hx('8a4a3a')), (hx('b0604a'), hx('7e3e30')), (hx('5a2a22'), hx('3e1c18')))
    for k in range(0, 9):                                                     # 오른쪽 벽 기둥 + 창(한옥식 살창)
        b = b0 + 0.05 + (b1 - b0 - 0.10) * k / 8
        s.line([G(a1, b, 0), G(a1, b, zw)], hx('5a2c22'), 4)
    for k in range(0, 8):
        bb0 = b0 + 0.05 + (b1 - b0 - 0.10) * (k + 0.2) / 8; bb1 = b0 + 0.05 + (b1 - b0 - 0.10) * (k + 0.8) / 8
        s.grad([G(a1, bb0, 1.0), G(a1, bb1, 1.0), G(a1, bb1, 4.6), G(a1, bb0, 4.6)], hx('e8dcc0'), hx('8a8470'), (0, 1))
        s.line([G(a1, (bb0 + bb1) / 2, 1.0), G(a1, (bb0 + bb1) / 2, 4.6)], hx('6a4a32'), 2)
        s.line([G(a1, bb0, 2.8), G(a1, bb1, 2.8)], hx('6a4a32'), 2)
    # 아래 처마 지붕(오른쪽으로 길게 내민 검은 기와) + 끝 들림
    ov = 0.11; zr = zw + 0.2
    s.grad([G(a1 + ov, b0 - 0.04, zr - 1.6), G(a1 + ov, b1 + 0.04, zr - 1.6), G(a1 - 0.01, b1 + 0.04, zr + 1.4), G(a1 - 0.01, b0 - 0.04, zr + 1.4)], hx('62646e'), hx('2c2e38'), (1, 0.3))
    for k in range(1, 26):
        b = b0 - 0.04 + (b1 - b0 + 0.08) * k / 26; s.line([G(a1 + ov, b, zr - 1.6), G(a1 - 0.01, b, zr + 1.4)], hx('22242c', 140), 2)
    s.line([G(a1 + ov, b0 - 0.04, zr - 1.6), G(a1 + ov, b1 + 0.04, zr - 1.6)], hx('d4d0e0'), 4)
    # 큰 지붕: 오른쪽 경사면(밝은 기와) + 왼쪽 경사면 + 용마루 (용마루는 앞뒤 방향)
    RZ = 7.4; am = (a0 + a1) / 2; zt = zr + 1.4
    s.grad([G(a0 - 0.02, b0 - 0.04, zt), G(a0 - 0.02, b1 + 0.04, zt), G(am, b1 - 0.02, zt + RZ), G(am, b0 + 0.02, zt + RZ)], hx('3a3c46'), hx('1e2028'), (1, 0.2))
    s.grad([G(a1 + 0.02, b0 - 0.04, zt), G(a1 + 0.02, b1 + 0.04, zt), G(am, b1 - 0.02, zt + RZ), G(am, b0 + 0.02, zt + RZ)], hx('7e8090'), hx('3e404e'), (-1, 0.3), 3)
    for k in range(1, 28):
        b = b0 - 0.04 + (b1 - b0 + 0.08) * k / 28; bb = b0 + 0.02 + (b1 - b0 - 0.04) * k / 28
        s.line([G(a1 + 0.02, b, zt), G(am, bb, zt + RZ)], hx('26283a', 130), 2)
    s.line([G(am, b0 + 0.02, zt + RZ), G(am, b1 - 0.02, zt + RZ)], hx('c4c0d4'), 5)
    s.line([G(a1 + 0.02, b0 - 0.04, zt), G(a1 + 0.02, b1 + 0.04, zt)], hx('dcd8e8'), 4)
    # ---- 앞쪽 서양식 고딕 파사드: 붉은 벽돌 + 회색 석재 + 뾰족 아치 출입구 + 박공 + 십자가 ----
    fa0, fa1, fb0, fb1, fh = 0.08, 0.48, 0.64, 0.78, 10.5
    s.shadow([G(fa0, fb1), G(fa1, fb1), G(fa1 + 0.10, fb1 + 0.10), G(fa0 + 0.08, fb1 + 0.10)], 80, 6)
    s.box(fa0, fa1, fb0, fb1, fh, BR, BR2, (hx('7a3a2e'), hx('5a2a22')))
    for z in range(1, 11): s.line([G(fa0, fb1, z), G(fa1, fb1, z)], hx('6a2e24', 60), 2)
    # 박공(삼각) 앞면 + 석재 테두리 + 십자가
    apx = G((fa0 + fa1) / 2, fb1, fh + 6.4)
    s.grad([G(fa0, fb1, fh), G(fa1, fb1, fh), apx], BR[0], BR[1], (0.2, 1))
    s.line([G(fa0, fb1, fh), apx, G(fa1, fb1, fh)], hx('cfcbbd'), 5)
    s.line([G(fa0, fb1, fh), G(fa1, fb1, fh)], hx('cfcbbd'), 5)
    s.grad([G(fa1, fb0, fh), G(fa1, fb1, fh), apx], hx('9c4434'), hx('6a2a22'), (0.3, 1))
    d.line([apx, (apx[0], apx[1] - 18)], fill=hx('f0ece2'), width=5); d.line([(apx[0] - 6, apx[1] - 12), (apx[0] + 6, apx[1] - 12)], fill=hx('f0ece2'), width=5)
    # 석재 모서리 + 뾰족 아치 문 3개 + 위 장미창
    for a in (fa0, fa1 - 0.012): s.grad([G(a, fb1, 0), G(a + 0.012, fb1, 0), G(a + 0.012, fb1, fh), G(a, fb1, fh)], ST[0], ST[1], (0.2, 1))
    for k in range(3):
        a = fa0 + 0.05 + k * 0.11
        s.grad([G(a - 0.04, fb1, 0), G(a + 0.04, fb1, 0), G(a + 0.04, fb1, 4.2), G(a, fb1, 6.0), G(a - 0.04, fb1, 4.2)], ST[0], ST[1], (0.2, 1))
        s.grad([G(a - 0.028, fb1, 0), G(a + 0.028, fb1, 0), G(a + 0.028, fb1, 3.8), G(a, fb1, 5.2), G(a - 0.028, fb1, 3.8)], hx('2c1c18'), hx('140c0a'), (0, 1))
    xx, yy = G((fa0 + fa1) / 2, fb1, 8.2); d.ellipse([xx - 11, yy - 11, xx + 11, yy + 11], fill=hx('e0e4dc')); d.ellipse([xx - 7, yy - 7, xx + 7, yy + 7], outline=hx('8a8a80'), width=2); d.line([(xx - 7, yy), (xx + 7, yy)], fill=hx('8a8a80'), width=2); d.line([(xx, yy - 7), (xx, yy + 7)], fill=hx('8a8a80'), width=2)
    # ---- 오른쪽 앞 첨탑(사각 종탑): 붉은 벽돌 + 회색 돌띠 + 둥근 창 + 루버 아치창 + 뾰족 지붕 + 십자가 ----
    TA0, TA1, TB0, TB1, TH = 0.48, 0.72, 0.62, 0.84, 21.0
    s.shadow([G(TA0, TB1), G(TA1, TB1), G(TA1 + 0.10, TB1 + 0.10), G(TA0 + 0.08, TB1 + 0.10)], 90, 6)
    s.box(TA0, TA1, TB0, TB1, TH, BR, BR2, (hx('9a4436'), hx('6a2c24')))
    for z in (5.0, 10.0, 15.0):
        s.grad([G(TA0, TB1, z), G(TA1, TB1, z), G(TA1, TB1, z + 0.9), G(TA0, TB1, z + 0.9)], ST[0], ST[1], (0, 1)); s.grad([G(TA1, TB0, z), G(TA1, TB1, z), G(TA1, TB1, z + 0.9), G(TA1, TB0, z + 0.9)], hx('9c988c'), hx('686458'), (0, 1))
    for a in (TA0, TA1 - 0.012): s.grad([G(a, TB1, 0), G(a + 0.012, TB1, 0), G(a + 0.012, TB1, TH), G(a, TB1, TH)], ST[0], ST[1], (0.2, 1))
    s.grad([G(TA0 + 0.05, TB1, 0), G(TA0 + 0.17, TB1, 0), G(TA0 + 0.17, TB1, 4.0), G(TA0 + 0.11, TB1, 5.4), G(TA0 + 0.05, TB1, 4.0)], hx('2a2220'), hx('141010'), (0, 1))
    xx, yy = G(TA0 + 0.12, TB1, 11.6); d.ellipse([xx - 12, yy - 12, xx + 12, yy + 12], fill=hx('e0e4dc')); d.ellipse([xx - 8, yy - 8, xx + 8, yy + 8], outline=hx('9a9a90'), width=2)
    for k in range(2):
        a = TA0 + 0.06 + k * 0.09
        s.grad([G(a, TB1, 16.6), G(a + 0.05, TB1, 16.6), G(a + 0.05, TB1, 19.4), G(a + 0.025, TB1, 20.4), G(a, TB1, 19.4)], hx('dcd8c8'), hx('6a6a60'), (0, 1))
        for z in (17.2, 17.9, 18.6): s.line([G(a, TB1, z), G(a + 0.05, TB1, z)], hx('5a5a52'), 2)
    px, py = G((TA0 + TA1) / 2, (TB0 + TB1) / 2, TH + 0.2); apex = (px, py - 64)
    s.grad([G(TA0 - 0.01, TB1 + 0.01, TH), G(TA1 + 0.01, TB1 + 0.01, TH), apex], hx('d0684c'), hx('8a382a'), (0, 1))
    s.grad([G(TA1 + 0.01, TB0 - 0.01, TH), G(TA1 + 0.01, TB1 + 0.01, TH), apex], hx('a84a38'), hx('6a281e'), (0.3, 1))
    d.line([apex, (apex[0], apex[1] - 22)], fill=hx('f0ece4'), width=5); d.line([(apex[0] - 8, apex[1] - 14), (apex[0] + 8, apex[1] - 14)], fill=hx('f0ece4'), width=5)
    s.tree(0.04, 0.88, 0.7); s.tree(0.94, 0.30, 0.7); s.tree(0.10, 0.36, 0.6)
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
