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

# ---------------- b10 익산경찰서 (흰 석재 + 남색 유리창 + 파란 간판, 지붕 위 경광 안테나, 순찰차) ----------------
def police():
    s = Sc(59, 10); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1, HH = 0.10, 0.86, 0.20, 0.60, 24.0
    s.shadow([G(0.14, B1), G(A1, B1), G(1.0, 0.72), G(0.26, 0.74)], 100, 10)
    s.box(A0, A1, B0, B1, HH, (hx('f4f6fa'), hx('c8ceda')), (hx('c8ceda'), hx('9aa2b4')), (hx('d4d8e4'), hx('aab0c2')))
    # 1층 석재 기단 + 3개 층 창 격자
    s.grad([G(A0, B1, 0), G(A1, B1, 0), G(A1, B1, 4.2), G(A0, B1, 4.2)], hx('8a92a8'), hx('5a627c'), (0.2, 1))
    s.win_grid_L(A0 + 0.03, A1 - 0.03, B1, 5.0, 19.0, 9, 3, hx('7aa4d4'), hx('1e3a68'), pad=0.20)
    s.win_grid_R(B0 + 0.03, B1 - 0.03, A1, 5.0, 19.0, 4, 3, hx('5a84b4'), hx('16305a'), pad=0.20)
    for k in range(1, 9): s.line([G(A0 + 0.03 + (A1 - A0 - 0.06) * k / 9, B1, 4.4), G(A0 + 0.03 + (A1 - A0 - 0.06) * k / 9, B1, 19.6)], hx('e8ecf4', 150), 2)
    # 파란 간판 띠 + 글씨
    sign(s, A0, A1, B1, 19.6, 24.0, '익산경찰서', (hx('1c4ca0'), hx('0e2c6c')), hx('ffffff'), 0.66)
    s.line([G(A0, B1, 19.6), G(A1, B1, 19.6)], hx('e8c030'), 3)
    # 중앙 현관 돌출 캐노피 + 기둥
    s.box(0.40, 0.62, B1, B1 + 0.10, 5.6, (hx('e4e8f2'), hx('b8c0d4')), (hx('b0b8cc'), hx('8890a8')), (hx('f4f6fa'), hx('d0d6e4')))
    s.grad([G(0.44, B1 + 0.10, 0), G(0.58, B1 + 0.10, 0), G(0.58, B1 + 0.10, 4.2), G(0.44, B1 + 0.10, 4.2)], hx('2a3a60'), hx('4c6090'), (0, 1))
    for a in (0.41, 0.61): s.line([G(a, B1 + 0.09, 0), G(a, B1 + 0.09, 5.4)], hx('f4f6fc'), 5)
    # 옥상 안테나 + 경광등
    x, y = G(0.30, 0.34, HH); d.line([(x, y), (x, y - 70)], fill=hx('dfe3ec'), width=5); d.ellipse([x - 9, y - 82, x + 9, y - 64], fill=hx('e83030'))
    x, y = G(0.66, 0.30, HH); d.line([(x, y), (x, y - 44)], fill=hx('dfe3ec'), width=4)
    # 태극 마크(간판 왼쪽)
    x, y = G(0.14, B1, 22.0); d.ellipse([x - 10, y - 10, x + 10, y + 10], fill=hx('e03030')); d.pieslice([x - 10, y - 10, x + 10, y + 10], 0, 180, fill=hx('2a50b0'))
    # 앞마당: 순찰차(흰 바탕 파란 줄) + 가로수
    for (a, b) in ((0.76, 0.84),):
        p = [G(a, b - 0.05), G(a + 0.20, b - 0.05), G(a + 0.20, b + 0.05), G(a, b + 0.05)]
        s.shadow([G(a - 0.02, b + 0.02), G(a + 0.22, b + 0.02), G(a + 0.24, b + 0.08), G(a, b + 0.08)], 90, 5)
        s.box(a, a + 0.20, b - 0.05, b + 0.05, 3.4, (hx('f6f8fc'), hx('c0c8d8')), (hx('d4dae8'), hx('9aa4ba')), (hx('f0f2f8'), hx('d4d8e4')))
        s.line([G(a, b + 0.05, 1.7), G(a + 0.20, b + 0.05, 1.7)], hx('2a58c0'), 4)
        s.box(a + 0.04, a + 0.15, b - 0.04, b + 0.04, 2.0, (hx('9cc4e8'), hx('385e8c')), (hx('7aa0cc'), hx('284870')), (hx('e8ecf4'), hx('c0c8d8')), z0=3.4)
        x, y = G(a + 0.10, b, 5.4); d.rectangle([x - 5, y - 3, x + 5, y + 1], fill=hx('e83030')); d.rectangle([x - 12, y - 3, x - 6, y + 1], fill=hx('2a58e0'))
    s.tree(0.04, 0.82, 0.7); s.tree(0.94, 0.30, 0.7)
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

# ---------------- b13 나바위성당 (한식 팔작지붕 + 회벽돌 + 앞 종탑 + 십자가) ----------------
def church():
    s = Sc(69, 13); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1, HH = 0.14, 0.84, 0.26, 0.58, 9.0
    s.shadow([G(0.16, B1), G(A1, B1), G(1.0, 0.70), G(0.28, 0.72)], 100, 10)
    # 본당 벽: 회청색 벽돌 + 줄눈
    s.box(A0, A1, B0, B1, HH, (hx('c8c4bc'), hx('9c978c')), (hx('a8a498'), hx('787468')), (hx('484c58'), hx('2c2e38')))
    for z in range(1, 9): s.line([G(A0, B1, z), G(A1, B1, z)], hx('78746a', 90), 2)
    for k in range(1, 8): s.line([G(A1, B0 + (B1 - B0) * k / 8, 0), G(A1, B0 + (B1 - B0) * k / 8, HH)], hx('606258', 80), 2)
    # 아치창(스테인드글라스 푸른색)
    for k in range(5):
        a = A0 + 0.07 + (A1 - A0 - 0.14) * k / 4
        s.grad([G(a - 0.022, B1, 2.4), G(a + 0.022, B1, 2.4), G(a + 0.022, B1, 6.6), G(a, B1, 7.6), G(a - 0.022, B1, 6.6)], hx('6a9ad4'), hx('243e8c'), (0.2, 1))
        s.line([G(a, B1, 2.4), G(a, B1, 7.4)], hx('e8e0d0'), 2)
    # 한식 팔작지붕: 앞·뒤 경사면 + 처마 곡선 + 기와 줄
    ov = 0.07; zr = HH + 0.4; RZ = 8.0; bm = (B0 + B1) / 2
    s.grad([G(A0 - ov, B0 - ov, zr), G(A1 + ov, B0 - ov, zr), G(A1 - 0.06, bm, zr + RZ), G(A0 + 0.06, bm, zr + RZ)], hx('4a4c58'), hx('2a2c36'), (0, 1))
    s.grad([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.06, bm, zr + RZ), G(A0 + 0.06, bm, zr + RZ)], hx('7a7c8a'), hx('3e404c'), (0, -1), 3)
    s.grad([G(A1 + ov, B0 - ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.06, bm, zr + RZ)], hx('5a5c6a'), hx('34364a'), (0.3, 1))
    for k in range(1, 22):
        t = k / 22; a = A0 - ov + (A1 - A0 + 2 * ov) * t; at = A0 + 0.06 + (A1 - A0 - 0.12) * t
        s.line([G(a, B1 + ov, zr), G(at, bm, zr + RZ)], hx('2a2c38', 120), 2)
    s.line([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr)], hx('c8c4d0'), 4)
    s.line([G(A0 + 0.06, bm, zr + RZ), G(A1 - 0.06, bm, zr + RZ)], hx('b4b0c0'), 5)
    for aa in (A0 - ov, A1 + ov):                                                        # 추녀 끝 살짝 들림
        x, y = G(aa, B1 + ov, zr); d.line([(x, y), (x + (14 if aa > 0.5 else -14), y - 8)], fill=hx('c8c4d0'), width=4)
    # 앞쪽 작은 종탑(출입부) + 십자가
    s.box(0.40, 0.58, B1, B1 + 0.12, 12.0, (hx('d8d4cc'), hx('aaa69a')), (hx('b4b0a4'), hx('807c70')), (hx('4a4c58'), hx('2e303a')))
    s.grad([G(0.45, B1 + 0.12, 0), G(0.53, B1 + 0.12, 0), G(0.53, B1 + 0.12, 5.6), G(0.49, B1 + 0.12, 6.8), G(0.45, B1 + 0.12, 5.6)], hx('4a2c1c'), hx('2c1810'), (0, 1))
    s.grad([G(0.38, B1 + 0.14, 12.0), G(0.60, B1 + 0.14, 12.0), G(0.49, B1 + 0.06, 19.0)], hx('6c6e7c'), hx('34364a'), (0, 1))
    s.grad([G(0.49, B1 + 0.06, 19.0), G(0.60, B1 + 0.14, 12.0), G(0.60, B1 - 0.02, 12.0)], hx('4a4c5c'), hx('2a2c3a'), (0.3, 1))
    x, y = G(0.49, B1 + 0.06, 19.0); d.line([(x, y), (x, y - 40)], fill=hx('f4f0e8'), width=6); d.line([(x - 12, y - 28), (x + 12, y - 28)], fill=hx('f4f0e8'), width=6)
    s.tree(0.05, 0.80, 0.7); s.tree(0.93, 0.78, 0.7)
    return s.finish()

# ---------------- b20 익산도서관 (유리 커튼월 + 목재 루버 + 책 모양 돌출) ----------------
def library():
    s = Sc(68, 20); G = s.G; s.ground(); d = ImageDraw.Draw(s.im)
    A0, A1, B0, B1, HH = 0.10, 0.88, 0.20, 0.62, 21.0
    s.shadow([G(0.14, B1), G(A1, B1), G(1.0, 0.74), G(0.26, 0.76)], 100, 10)
    s.box(A0, A1, B0, B1, HH, (hx('f2f4f6'), hx('c6ccd4')), (hx('c6ccd4'), hx('9aa2ae')), (hx('dadee4'), hx('b0b6c0')))
    # 유리 커튼월 4개 층 + 층별 가로 띠
    s.grad([G(A0 + 0.02, B1, 3.6), G(A1 - 0.02, B1, 3.6), G(A1 - 0.02, B1, 17.6), G(A0 + 0.02, B1, 17.6)], hx('a8d0e4'), hx('2e5a86'), (0.2, 1))
    for z in (7.1, 10.6, 14.1): s.line([G(A0 + 0.02, B1, z), G(A1 - 0.02, B1, z)], hx('f4f6f8'), 4)
    for k in range(1, 13): s.line([G(A0 + 0.02 + (A1 - A0 - 0.04) * k / 13, B1, 3.6), G(A0 + 0.02 + (A1 - A0 - 0.04) * k / 13, B1, 17.6)], hx('e8eef4', 150), 2)
    for k in range(3, 8): s.line([G(A0 + 0.02 + (A1 - A0 - 0.04) * (k - 0.5) / 13, B1, 3.6), G(A0 + 0.02 + (A1 - A0 - 0.04) * (k + 0.5) / 13, B1, 17.6)], hx('ffffff', 70), 2)
    s.win_grid_R(B0 + 0.03, B1 - 0.03, A1, 4.0, 17.0, 5, 3, hx('88b4cc'), hx('244e78'), pad=0.20)
    # 목재 루버 띠(상단) + 간판
    s.grad([G(A0, B1, 17.6), G(A1, B1, 17.6), G(A1, B1, 21.0), G(A0, B1, 21.0)], hx('c88c4c'), hx('8a5a2c'), (0.2, 1), 4)
    for k in range(1, 40): s.line([G(A0 + (A1 - A0) * k / 40, B1, 17.6), G(A0 + (A1 - A0) * k / 40, B1, 21.0)], hx('5a3818', 120), 2)
    sign(s, 0.40, 0.84, B1, 18.2, 20.6, '익산도서관', (hx('284a78'), hx('142a50')), hx('ffffff'), 0.66, 56)
    # 1층 출입 캐노피 + 책 모양 돌출(앞 왼쪽 낮은 동)
    s.box(0.10, 0.34, B1, B1 + 0.14, 6.4, (hx('f6f8fa'), hx('c8d0d8')), (hx('b4bcc8'), hx('8a94a4')), (hx('ffffff'), hx('dce2ea')))
    s.grad([G(0.14, B1 + 0.14, 0.6), G(0.30, B1 + 0.14, 0.6), G(0.30, B1 + 0.14, 4.8), G(0.14, B1 + 0.14, 4.8)], hx('bce0f0'), hx('3a6a90'), (0, 1))
    for k in range(1, 4): s.line([G(0.14 + 0.16 * k / 4, B1 + 0.14, 0.6), G(0.14 + 0.16 * k / 4, B1 + 0.14, 4.8)], hx('f4f6f8'), 2)
    # 옥상 정원 난간 + 앞마당 책 모양 조형물
    s.line([G(A0, B1, 21.0), G(A1, B1, 21.0), G(A1, B0, 21.0)], hx('f6f8fa'), 4)
    x, y = G(0.66, 0.86); s.shadow([(x - 30, y + 2), (x + 40, y + 2), (x + 48, y + 12), (x - 22, y + 12)], 80, 5)
    d.polygon([(x - 26, y - 6), (x, y - 20), (x, y - 4), (x - 26, y + 8)], fill=hx('f4f0e4')); d.polygon([(x, y - 20), (x + 26, y - 6), (x + 26, y + 8), (x, y - 4)], fill=hx('d8d0bc'))
    d.line([(x, y - 20), (x, y - 4)], fill=hx('8a6a3a'), width=3)
    s.tree(0.04, 0.84, 0.7); s.tree(0.94, 0.26, 0.7)
    return s.finish()

if __name__ == '__main__':
    items = {'b10': police, 'b12': bank, 'b13': church, 'b20': library}
    sheet = Image.new('RGBA', (4 * (96 * 6 + 20), 72 * 6), (150, 70, 160, 255)); x = 0
    for n, fn in items.items():
        im = fn(); im.save(f'{out}/{n}.png'); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im); sheet.paste(bg.resize((im.width * 6, im.height * 6), Image.NEAREST), (x, 0)); x += 96 * 6 + 20
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('공공·생활 건물 →', out)
