"""원본 건물처럼 96폭 저해상도 캔버스에 '평평한 색 면 + 어두운 윤곽선'을 안티앨리어싱 없이 직접 찍는 도트 그리기 도구.
아이소메트릭 좌표 G(a,b,z): a 는 위 꼭짓점→오른쪽, b 는 위 꼭짓점→왼쪽 방향(0~1), z 는 높이(화소). 면마다 밝은 면/중간/어두운 면 3톤으로 칠한다.
make_pixel_chunpo() → (96, Hc) RGBA. build_sprites 의 ITEMS 에서 불러 쓴다."""
import os, sys
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import strips as ST

def hx(h): return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)

class Px:
    def __init__(self, k):
        self.Hc, self.yc = ST.canvas(k); self.im = Image.new('RGBA', (96, self.Hc), (0, 0, 0, 0)); self.d = ImageDraw.Draw(self.im)
    def G(self, a, b, z=0.0): return (round(48 + 47 * (a - b)), round(self.yc - 24 + 24 * (a + b) - z))
    def poly(self, pts, fill, line=None):
        P = [self.G(*p) for p in pts]; self.d.polygon(P, fill=fill, outline=line or fill)
    def line(self, p, q, c): self.d.line([self.G(*p), self.G(*q)], fill=c, width=1)
    def rect_wall(self, a0, a1, b, z0, z1, fill, line=None): self.poly([(a0, b, z0), (a1, b, z0), (a1, b, z1), (a0, b, z1)], fill, line)
    def rect_side(self, a, b0, b1, z0, z1, fill, line=None): self.poly([(a, b0, z0), (a, b1, z0), (a, b1, z1), (a, b0, z1)], fill, line)
    def outline(self, c=(40, 36, 44, 255)):
        a = self.im.getchannel('A'); import numpy as np
        al = np.asarray(a) > 0; ed = al & ~(np.roll(al, 1, 0) & np.roll(al, -1, 0) & np.roll(al, 1, 1) & np.roll(al, -1, 1))
        arr = np.asarray(self.im).copy(); arr[ed] = c; self.im = Image.fromarray(arr, 'RGBA'); self.d = ImageDraw.Draw(self.im)

def make_pixel_chunpo():
    p = Px(21)
    GROUND = hx('c9c3b0'); GROUNDD = hx('a39d8a'); INK = hx('3a3440')
    CREAM = (hx('f2e6a8'), hx('d9c885'), hx('b3a15f')); MINT = (hx('86d8b8'), hx('4eb08e'))
    SL = (hx('7a9a94'), hx('55766f'), hx('3c5651')); BLUE = (hx('4a98e0'), hx('2a6cb4'), hx('1c4c88'))
    A0, A1, B0, B1, H = 0.10, 0.90, 0.20, 0.56, 14
    # 바닥(승강장 포함)
    p.poly([(0.02, 0.02, 0), (0.98, 0.02, 0), (0.98, 0.98, 0), (0.02, 0.98, 0)], GROUND, GROUNDD)
    p.poly([(0.06, 0.70, 0), (0.94, 0.70, 0), (0.94, 0.82, 0), (0.06, 0.82, 0)], hx('e4dfce'), GROUNDD)          # 승강장
    p.line((0.06, 0.90, 0), (0.94, 0.90, 0), hx('6e6a60')); p.line((0.06, 0.95, 0), (0.94, 0.95, 0), hx('6e6a60'))      # 선로
    for i in range(1, 12): t = 0.06 + 0.88 * i / 12; p.line((t, 0.88, 0), (t, 0.97, 0), hx('8a6a4a'))
    # 본채 벽: 왼쪽(밝음) + 오른쪽(어두움)
    p.rect_wall(A0, A1, B1, 0, H, CREAM[0], INK); p.rect_side(A1, B0, B1, 0, H, CREAM[1], INK)
    p.rect_wall(A0, A1, B1, 0, 3, MINT[0], INK); p.rect_side(A1, B0, B1, 0, 3, MINT[1], INK)                      # 아래 민트 띠
    # 문·창(앞벽)
    p.rect_wall(0.22, 0.34, B1, 3, 11, BLUE[0], INK); p.line((0.28, B1, 3), (0.28, B1, 11), INK)                  # 쌍여닫이 문
    p.rect_wall(0.40, 0.46, B1, 3, 10, BLUE[1], INK)
    for a in (0.62, 0.74): p.rect_wall(a, a + 0.07, B1, 4, 11, hx('bfe0f8'), BLUE[2]); p.line((a + 0.035, B1, 4), (a + 0.035, B1, 11), BLUE[2])
    p.rect_side(A1, 0.30, 0.46, 4, 10, hx('9cc8ec'), BLUE[2])
    # 지붕: 앞 경사(밝음) + 오른쪽 경사(어두움), 기와 줄무늬
    R = 7; o = 0.04; bm = (B0 + B1) / 2
    p.poly([(A0 - o, B1 + o, H), (A1 + o, B1 + o, H), (A1 - 0.06, bm, H + R), (A0 + 0.06, bm, H + R)], SL[0], INK)
    p.poly([(A1 + o, B0 - o, H), (A1 + o, B1 + o, H), (A1 - 0.06, bm, H + R)], SL[1], INK)
    for i in range(1, 5):
        t = i / 5; z = H + R * t; b = (B1 + o) + (bm - B1 - o) * t
        p.line((A0 - o + (0.06 + o) * t, b, z), (A1 + o - (0.06 + o) * t, b, z), SL[2])
    p.line((A0 + 0.06, bm, H + R), (A1 - 0.06, bm, H + R), hx('b8d4cc'))                                          # 용마루
    # 앞 박공 현관(지붕에 이어지는 작은 삼각 지붕)
    p.rect_wall(0.44, 0.64, B1 + 0.04, 0, H - 1, CREAM[0], INK)
    p.poly([(0.42, B1 + 0.04, H - 1), (0.66, B1 + 0.04, H - 1), (0.54, B1 + 0.04, H + 5)], hx('fffdf0'), INK)
    p.poly([(0.42, B1 + 0.04, H - 1), (0.54, B1 + 0.04, H + 5), (0.54, bm, H + R - 1)], SL[0], INK)
    p.rect_wall(0.50, 0.58, B1 + 0.04, 4, 10, BLUE[0], INK)
    p.rect_side(0.64, B1 - 0.02, B1 + 0.04, 0, H - 1, CREAM[1], INK)
    # 오른쪽 부속채(벽돌색)
    p.rect_wall(0.88, 0.99, 0.22, 0, 6, hx('c07a5a'), INK); p.rect_side(0.99, 0.22, 0.44, 0, 6, hx('8e4e38'), INK)
    p.poly([(0.88, 0.44, 6), (0.99, 0.44, 6), (0.99, 0.22, 6), (0.88, 0.22, 6)], BLUE[0], INK)
    p.outline()
    return p.im

if __name__ == '__main__':
    im = make_pixel_chunpo(); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im)
    bg.resize((im.width * 6, im.height * 6), Image.NEAREST).save(sys.argv[1] if len(sys.argv) > 1 else '/tmp/claude-0/prev/px_chunpo.png')
