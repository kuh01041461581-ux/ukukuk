"""익산 모드 추가 건물: 익산예술의전당(b42=국립극장 자리), 익산 야구장(b33=월드컵경기장 자리), 백제왕궁(b38: 왕궁리 오층석탑 + 궁궐 정전).
make_iksan_buildings.Sc 의 8배 해상도 렌더를 그대로 쓴다.  → work/pak_mod/{b42,b33,b38}.png   사용: python3 -I tools/make_iksan_more.py [미리보기.png]"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_iksan_landmarks as LM
from make_iksan_buildings import Sc, hx, lerp, W, S, out, FONT_K, FONT_B

# ---------------- 익산예술의전당 (대공연장 + 유리 로비) ----------------
def arts():
    s = Sc(60, 21); G = s.G; s.ground(); rs = random.Random(5)
    s.shadow([G(0.10, 0.58), G(0.88, 0.58), G(1.0, 0.70), G(0.22, 0.72)], 100, 10)
    # 뒤쪽 대공연장(무대 타워): 회백색 석재 + 세로 핀
    s.box(0.40, 0.88, 0.10, 0.40, 21, (hx('f2f0ea'), hx('c4c0b6')), (hx('cac6ba'), hx('96928a')), (hx('d4d2cc'), hx('aeaca6')))
    for k in range(1, 14): s.line([G(0.40 + 0.48 * k / 14, 0.40, 0), G(0.40 + 0.48 * k / 14, 0.40, 21)], hx('a8a49a', 110), 2)
    for k in range(1, 6): s.line([G(0.88, 0.10 + 0.30 * k / 6, 0), G(0.88, 0.10 + 0.30 * k / 6, 21)], hx('76726a', 110), 2)
    s.grad([G(0.40, 0.40, 17), G(0.88, 0.40, 17), G(0.88, 0.40, 21), G(0.40, 0.40, 21)], hx('3a4a68'), hx('1a2440'), (0.2, 1))
    # 앞쪽 유리 로비: 하늘색 반사 유리 + 흰 기둥 + 앞으로 내민 경사 지붕
    s.box(0.10, 0.82, 0.40, 0.64, 11, (hx('bcd6ec'), hx('4a78a8')), (hx('7aa0c8'), hx('2a4e7c')), (hx('e6eaf2'), hx('b8c0d0')))
    for k in range(0, 15): s.line([G(0.10 + 0.72 * k / 14, 0.64, 0), G(0.10 + 0.72 * k / 14, 0.64, 11)], hx('f6f8fc'), 3)
    for z in (3.6, 7.2): s.line([G(0.10, 0.64, z), G(0.82, 0.64, z)], hx('e8eef8', 190), 2)
    for k in range(5, 10): s.line([G(0.10 + 0.72 * (k - 1) / 14, 0.64, 0), G(0.10 + 0.72 * (k - 1) / 14, 0.64, 9)], hx('ffffff', 60), 2)
    s.grad([G(0.06, 0.64, 11.4), G(0.86, 0.64, 11.4), G(0.86, 0.78, 8.0), G(0.06, 0.78, 8.0)], hx('f4f6fa'), hx('c0c6d4'), (1, 0.6))        # 경사 캐노피
    s.grad([G(0.06, 0.78, 8.0), G(0.86, 0.78, 8.0), G(0.86, 0.78, 7.0), G(0.06, 0.78, 7.0)], hx('e0e4ee'), hx('9aa2b6'), (0, 1))
    for a in (0.12, 0.34, 0.58, 0.80): s.line([G(a, 0.76, 0), G(a, 0.76, 7.0)], hx('f4f4f8'), 6)
    s.grad([G(0.40, 0.76, 0), G(0.54, 0.76, 0), G(0.54, 0.76, 5.4), G(0.40, 0.76, 5.4)], hx('26344e'), hx('4a5c82'), (0, 1))
    # 간판 "예술의전당" (금빛 글씨, 어두운 띠 위)
    s.grad([G(0.14, 0.64, 11), G(0.80, 0.64, 11), G(0.80, 0.64, 14.6), G(0.14, 0.64, 14.6)], hx('1e2a44'), hx('0e1628'), (0.2, 1))
    fw = G(0.78, 0.64)[0] - G(0.16, 0.64)[0]
    s.text_face('예술의전당', (G(0.17, 0.64)[0], G(0.17, 0.64, 13.6)[1] + 1), 0, 0, fw * 0.70 / 330.0, hx('f4d478'), font=FONT_K, size=60, shear=0.5)
    # 앞 광장 분수 + 가로수
    x, y = G(0.52, 0.90); d = ImageDraw.Draw(s.im); d.ellipse([x - 40, y - 12, x + 40, y + 12], fill=hx('7aa4d0')); d.ellipse([x - 32, y - 9, x + 32, y + 9], fill=hx('bcdcf4')); d.line([(x, y), (x, y - 26)], fill=hx('f0f8ff'), width=4); d.ellipse([x - 8, y - 32, x + 8, y - 22], fill=hx('e8f4ff'))
    s.tree(0.04, 0.80, 0.8); s.tree(0.95, 0.30, 0.8)
    return s.finish()

# ---------------- 익산 야구장 ----------------
def baseball():
    s = Sc(57, 22); G = s.G; s.ground(); rs = random.Random(6)
    A0, A1, B0, B1 = 0.05, 0.95, 0.05, 0.95
    s.shadow([G(A0, B1), G(A1, B1), G(1.0, 1.0), G(0.1, 1.0)], 70, 8)
    # 바깥 벽(외야 쪽 낮은 담: 앞 두 면) + 경기장 바닥(잔디 줄무늬 + 내야 흙 + 흰 선)
    s.grad([G(0.12, 0.12, 0.4), G(0.88, 0.12, 0.4), G(0.88, 0.88, 0.4), G(0.12, 0.88, 0.4)], hx('5ab04a'), hx('3c8a34'), (0.4, 1), 3)
    for k in range(0, 8): s.flat([G(0.12 + 0.095 * k, 0.12, 0.45), G(0.12 + 0.095 * k + 0.048, 0.12, 0.45), G(0.12 + 0.095 * k + 0.048, 0.88, 0.45), G(0.12 + 0.095 * k, 0.88, 0.45)], hx('6cc05a', 90))
    dirt = [(0.12 + 0.04 * i, 0.12 + 0.04 * j) for i, j in ((0, 0), (14, 0), (14, 14), (0, 14))]
    s.grad([G(0.14, 0.14, 0.5), G(0.52, 0.14, 0.5), G(0.52, 0.52, 0.5), G(0.14, 0.52, 0.5)], hx('d4a064'), hx('b07c44'), (0.4, 1), 4)       # 내야 흙
    s.grad([G(0.20, 0.20, 0.55), G(0.46, 0.20, 0.55), G(0.46, 0.46, 0.55), G(0.20, 0.46, 0.55)], hx('5ab04a'), hx('3c8a34'), (0.4, 1), 3)        # 내야 잔디
    d = ImageDraw.Draw(s.im)
    for p in ([G(0.14, 0.14, 0.6), G(0.88, 0.14, 0.6)], [G(0.14, 0.14, 0.6), G(0.14, 0.88, 0.6)]): d.line(p, fill=hx('ffffff', 230), width=3)           # 파울 라인
    for (a, b) in ((0.32, 0.17), (0.17, 0.32), (0.33, 0.33), (0.145, 0.145)):                                                                    # 베이스 + 홈
        x, y = G(a, b, 0.7); d.polygon([(x - 6, y), (x, y - 3), (x + 6, y), (x, y + 3)], fill=hx('fafafa'))
    x, y = G(0.30, 0.30, 0.7); d.ellipse([x - 9, y - 4, x + 9, y + 4], fill=hx('c88c50'))
    # 뒤쪽 관중석 2면(안쪽 경사면이 보임): 파랑/흰 좌석 줄 + 계단 통로
    for k in range(6):                                                                                                                          # b 작은 쪽 스탠드(오른쪽 위)
        t0, t1 = k / 6, (k + 1) / 6; col = hx('2a4aa8') if k % 2 else hx('e8ecf8')
        s.flat([G(0.10, 0.12 - 0.07 * t0, 0.4 + 8.0 * t0), G(0.90, 0.12 - 0.07 * t0, 0.4 + 8.0 * t0), G(0.90, 0.12 - 0.07 * t1, 0.4 + 8.0 * t1), G(0.10, 0.12 - 0.07 * t1, 0.4 + 8.0 * t1)], col)
    for k in range(6):                                                                                                                          # a 작은 쪽 스탠드(왼쪽 위)
        t0, t1 = k / 6, (k + 1) / 6; col = hx('1e3a90') if k % 2 else hx('d6dcf0')
        s.flat([G(0.12 - 0.07 * t0, 0.10, 0.4 + 8.0 * t0), G(0.12 - 0.07 * t0, 0.90, 0.4 + 8.0 * t0), G(0.12 - 0.07 * t1, 0.90, 0.4 + 8.0 * t1), G(0.12 - 0.07 * t1, 0.10, 0.4 + 8.0 * t1)], col)
    for a in (0.30, 0.50, 0.70): s.line([G(a, 0.12, 0.4), G(a, 0.05, 8.4)], hx('e8ecf8', 190), 4)
    s.grad([G(0.05, 0.05, 8.4), G(0.95, 0.05, 8.4), G(0.95, 0.05, 10.0), G(0.05, 0.05, 10.0)], hx('e8ecf6'), hx('b4bcd4'), (0, 1))                  # 지붕 처마 띠
    s.grad([G(0.05, 0.05, 8.4), G(0.05, 0.95, 8.4), G(0.05, 0.95, 10.0), G(0.05, 0.05, 10.0)], hx('c4cce4'), hx('8c96b8'), (0, 1))
    # 앞쪽 외야 담(낮은 초록 펜스) + 전광판
    s.grad([G(0.12, 0.88, 0.4), G(0.88, 0.88, 0.4), G(0.88, 0.88, 3.0), G(0.12, 0.88, 3.0)], hx('2a8a44'), hx('176a2a'), (0, 1))
    s.grad([G(0.88, 0.12, 0.4), G(0.88, 0.88, 0.4), G(0.88, 0.88, 3.0), G(0.88, 0.12, 3.0)], hx('1a6a30'), hx('0e4a20'), (0, 1))
    s.grad([G(0.30, 0.88, 3.0), G(0.62, 0.88, 3.0), G(0.62, 0.88, 8.0), G(0.30, 0.88, 8.0)], hx('1a2238'), hx('0a1020'), (0, 1))
    s.text_face('IKSAN', (G(0.33, 0.88)[0], G(0.33, 0.88, 7.2)[1] + 1), 0, 0, 0.13, hx('ffd84a'), size=44, shear=0.5)
    # 조명탑 4개
    for (a, b) in ((0.04, 0.04), (0.96, 0.04), (0.04, 0.96), (0.96, 0.96)):
        x, y = G(a, b); d.line([(x, y), (x, y - 11 * S)], fill=hx('6a7288'), width=4); d.rectangle([x - 18, y - 11 * S - 12, x + 18, y - 11 * S + 2], fill=hx('e8ecf6'))
        for i in range(-2, 3): d.ellipse([x + i * 7 - 2, y - 11 * S - 9, x + i * 7 + 2, y - 11 * S - 5], fill=hx('fff6c0'))
    return s.finish()

# ---------------- 백제왕궁 (왕궁리 유적: 오층석탑 + 정전) ----------------
def pagoda5(s, cx, cy, f, rnd):
    """오층석탑을 중심 (cx,cy), 크기 배율 f 로 그린다(LM.layered_roof 사용)."""
    G = s.G
    s.box(cx - 0.17 * f, cx + 0.17 * f, cy - 0.17 * f, cy + 0.17 * f, 1.8 * f, (hx('c4bca6'), hx('8e866e')), (hx('8e866e'), hx('5a543e')), LM.STONE_T, 0)
    z = 1.8 * f
    s.box(cx - 0.11 * f, cx + 0.11 * f, cy - 0.11 * f, cy + 0.11 * f, 6.2 * f, (hx('d8ccb0'), hx('a89c80')), (hx('a89c80'), hx('6a604a')), LM.STONE_T, z); LM.weathered(s, cx - 0.11 * f, cx + 0.11 * f, cy + 0.11 * f, z, 6.2 * f, rnd, 12)
    z += 6.2 * f
    for (w, h, rw) in ((0.095, 2.2, 0.15), (0.082, 2.2, 0.133), (0.07, 2.0, 0.116), (0.058, 1.9, 0.10), (0.048, 1.7, 0.083)):
        z = LM.layered_roof(s, cx, cy, rw * f, z, rnd, layers=4, thick=0.5 * f, taper=0.01 * f)
        s.box(cx - w * f, cx + w * f, cy - w * f, cy + w * f, h * f, LM.STONE_L, LM.STONE_R, LM.STONE_T, z); z += h * f
    z = LM.layered_roof(s, cx, cy, 0.07 * f, z, rnd, layers=3, thick=0.45 * f, taper=0.01 * f)
    x, y = G(cx, cy, z + 1.2 * f); ImageDraw.Draw(s.im).ellipse([x - 5, y - 6, x + 5, y + 3], fill=hx('d8d0bc'))

def baekje():
    s = Sc(84, 23); G = s.G; rnd = random.Random(7); d = ImageDraw.Draw(s.im)
    s.grad([G(0, 1), G(1, 1), G(1, 1, -3.4), G(0, 1, -3.4)], hx('a48048'), hx('6a4c28'), (0, 1), 4)
    s.grad([G(1, 0), G(1, 1), G(1, 1, -3.4), G(1, 0, -3.4)], hx('82602e'), hx('4e3818'), (0, 1), 4)
    s.grad(s.ground_poly(), hx('d2c870'), hx('a4a448'), (0.4, 1), 5)
    for i in range(1400):
        a, b = rnd.random(), rnd.random(); x, y = G(a, b); d.line([(x, y), (x + rnd.randint(-2, 2), y - rnd.randint(4, 9))], fill=rnd.choice([hx('e8e286', 150), hx('7e8a34', 140), hx('b4ae54', 130)]), width=2)
    d.line([G(0, 1), G(1, 1), G(1, 0)], fill=hx('ece698', 200), width=3)
    # 궁장(宮牆): 뒤 두 변을 따라 낮은 흙·기와 담 + 앞쪽 문
    for (p0, p1) in (((0.04, 0.04), (0.96, 0.04)), ((0.04, 0.04), (0.04, 0.96))):
        s.grad([G(p0[0], p0[1], 0), G(p1[0], p1[1], 0), G(p1[0], p1[1], 3.4), G(p0[0], p0[1], 3.4)], hx('c8b890'), hx('8e8060'), (0, 1), 4)
        s.line([G(p0[0], p0[1], 3.4), G(p1[0], p1[1], 3.4)], hx('4a4c58'), 7); s.line([G(p0[0], p0[1], 3.9), G(p1[0], p1[1], 3.9)], hx('7a7c8a'), 5)
    s.shadow([G(0.46, 0.50), G(0.88, 0.50), G(1.0, 0.62), G(0.58, 0.64)], 100, 10)
    s.shadow([G(0.14, 0.14), G(0.52, 0.14), G(0.60, 0.26), G(0.22, 0.26)], 90, 9)
    pagoda5(s, 0.30, 0.30, 0.95, rnd)                                                           # 석탑(뒤쪽 왼편)
    # 정전: 기단(2단 + 계단) + 붉은 기둥 + 크림 벽 + 회색 기와 팔작지붕
    s.box(0.50, 0.92, 0.46, 0.90, 1.6, (hx('cbc4ae'), hx('8e866e')), (hx('8e866e'), hx('5a543e')), LM.STONE_T, 0)
    s.box(0.54, 0.88, 0.50, 0.86, 1.2, LM.STONE_L, LM.STONE_R, LM.STONE_T, 1.6)
    for k in range(4): s.box(0.66 - 0.01 * k, 0.76 + 0.01 * k, 0.86, 0.86 + 0.04 * (4 - k) * 0.5, 0.45 * (k + 1), (hx('dcd5c1'), hx('aaa28e')), (hx('aaa28e'), hx('6f6858')), LM.STONE_T, 0)
    A0, A1, B0, B1, z0, HW = 0.58, 0.84, 0.54, 0.82, 2.8, 5.6
    s.box(A0, A1, B0, B1, HW, (hx('f2e8cc'), hx('cdbf9a')), (hx('cdbf9a'), hx('9a8c68')), (hx('8a8c98'), hx('5a5c6a')), z0)
    for i in range(5):
        a = A0 + (A1 - A0) * i / 4; s.line([G(a, B1, z0), G(a, B1, z0 + HW)], hx('b8281e'), 7); s.line([G(a - 0.004, B1, z0), G(a - 0.004, B1, z0 + HW)], hx('e8604a'), 2)
    for i in range(4):
        a = A0 + (A1 - A0) * (i + 0.18) / 4; s.grad([G(a, B1, z0 + 0.6), G(a + 0.045, B1, z0 + 0.6), G(a + 0.045, B1, z0 + HW - 0.6), G(a, B1, z0 + HW - 0.6)], hx('3a2a1c') if i in (1, 2) else hx('c8b890'), hx('1a1008') if i in (1, 2) else hx('9a8a68'), (0, 1))
    zr = z0 + HW; RZ = 5.2; ov = 0.04
    s.grad([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.05, (B0 + B1) / 2, zr + RZ), G(A0 + 0.05, (B0 + B1) / 2, zr + RZ)], hx('7a7c8c'), hx('424454'), (0.2, 1), 4)
    s.grad([G(A1 + ov, B0 - ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.05, (B0 + B1) / 2, zr + RZ)], hx('5a5c6c'), hx('2e303c'), (0.2, 1), 4)
    for k in range(1, 8):
        t = k / 8; bb = (B1 + ov) + ((B0 + B1) / 2 - (B1 + ov)) * t; zz = zr + RZ * t; aL = A0 - ov + (0.09 + ov) * t; aR = A1 + ov - (0.09 + ov) * t
        s.line([G(aL, bb, zz), G(aR, bb, zz)], hx('26283a', 170), 3)
    s.line([G(A0 + 0.05, (B0 + B1) / 2, zr + RZ), G(A1 - 0.05, (B0 + B1) / 2, zr + RZ)], hx('a4a6b8'), 5)
    s.line([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr)], hx('c4c6d8'), 4)
    s.line([G(A0 - ov, B1 + ov, zr), G(A0 - ov - 0.012, B1 + ov, zr + 0.9)], hx('c4c6d8'), 4); s.line([G(A1 + ov, B1 + ov, zr), G(A1 + ov + 0.012, B1 + ov, zr + 0.9)], hx('c4c6d8'), 4)
    # 붉은 말뚝 + 흰 줄(석탑 앞마당)
    ps = [(t, 0.50) for t in np.linspace(0.10, 0.44, 5)] + [(0.46, t) for t in np.linspace(0.10, 0.44, 5)]
    for (a, b) in ps:
        x, y = G(a, b); d.polygon([(x - 3, y), (x + 3, y), (x + 3, y - 12), (x - 3, y - 12)], fill=hx('c8382a')); d.ellipse([x - 3.5, y - 14, x + 3.5, y - 10], fill=hx('f4dcd2'))
    s.tree(0.04, 0.70, 0.8); s.tree(0.30, 0.92, 0.8)
    return s.finish()

if __name__ == '__main__':
    items = {'b42': arts, 'b33': baseball, 'b38': baekje}
    sheet = Image.new('RGBA', (3 * 96 * 7 + 40, 84 * 7), (150, 70, 160, 255)); x = 0
    for n, fn in items.items():
        im = fn(); im.save(f'{out}/{n}.png'); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im); sheet.paste(bg.resize((im.width * 7, im.height * 7), Image.NEAREST), (x, 0)); x += 96 * 7 + 20
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('추가 건물 →', out)
