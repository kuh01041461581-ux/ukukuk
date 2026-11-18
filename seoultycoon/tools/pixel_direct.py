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
        if fill is None: return
        P = [self.G(*p) for p in pts]; self.d.polygon(P, fill=fill, outline=(tuple(int(v * 0.74) for v in fill[:3]) + (255,)) if line else fill)      # 검은 윤곽선 대신 같은 색의 한 단계 어두운 톤으로 면 경계를 구분(서울타이쿤 도트 방식)
    def line(self, p, q, c): self.d.line([self.G(*p), self.G(*q)], fill=c, width=1)
    def rect_wall(self, a0, a1, b, z0, z1, fill, line=None): self.poly([(a0, b, z0), (a1, b, z0), (a1, b, z1), (a0, b, z1)], fill, line)
    def rect_side(self, a, b0, b1, z0, z1, fill, line=None): self.poly([(a, b0, z0), (a, b1, z0), (a, b1, z1), (a, b0, z1)], fill, line)
    def outline(self, c=(40, 36, 44, 255)):
        a = self.im.getchannel('A'); import numpy as np
        al = np.asarray(a) > 0; ed = al & ~(np.roll(al, 1, 0) & np.roll(al, -1, 0) & np.roll(al, 1, 1) & np.roll(al, -1, 1))
        arr = np.asarray(self.im).copy(); arr[ed, :3] = (arr[ed, :3] * 0.62).astype(np.uint8); self.im = Image.fromarray(arr, 'RGBA'); self.d = ImageDraw.Draw(self.im)

def shade(c, f): return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (255,)

def band_wall(p, a0, a1, b, z0, z1, base, line, n=4, k=0.07, side=False, b0=None):
    """벽을 위(밝음)→아래(어둠) n 단계 가로띠로 칠해 입체감을 준다."""
    for i in range(n):
        za, zb = z0 + (z1 - z0) * i / n, z0 + (z1 - z0) * (i + 1) / n; c = shade(base, 1.0 + k * (n / 2 - i - 0.5) * 2)
        if side: p.rect_side(a0, b0, b, za, zb, c, c)
        else: p.rect_wall(a0, a1, b, za, zb, c, c)
    if side: p.rect_side(a0, b0, b, z0, z1, None or base, line) if False else None

def tile_roof(p, pts_rows, c1, c2):
    """지붕 경사 줄마다 두 톤을 번갈아 찍어 마름모 기와 느낌을 낸다."""
    for (pa, pb, ci) in pts_rows:
        xa, ya = p.G(*pa); xb, yb = p.G(*pb); n = max(abs(xb - xa), 1)
        for i in range(0, n + 1):
            t = i / n; x = round(xa + (xb - xa) * t); y = round(ya + (yb - ya) * t)
            if 0 <= x < 96 and 0 <= y < p.Hc and p.im.getpixel((x, y))[3]: p.im.putpixel((x, y), c1 if (i + ci) % 2 == 0 else c2)

def mask_of(p, pts):
    m = Image.new('L', (96, p.Hc), 0); ImageDraw.Draw(m).polygon([p.G(*q) for q in pts], fill=255, outline=255); return m

def checker(p, pts, c1, c2, phase=0, bands=None):
    """다각형 안을 두 톤 체크 디더로 채운다. bands=[(y0,y1,c1,c2)...] 이면 높이대마다 색쌍을 바꿔 부드러운 그라데이션을 만든다."""
    m = mask_of(p, pts); W, H = m.size
    for y in range(H):
        for x in range(W):
            if m.getpixel((x, y)):
                a, b = c1, c2
                if bands:
                    for (y0, y1, ca, cb) in bands:
                        if y0 <= y < y1: a, b = ca, cb
                p.im.putpixel((x, y), a if (x + y + phase) % 2 == 0 else b)

def stripes(p, pts, c1, c2, w=2, bands=None):
    """다각형 안을 세로 줄무늬 두 톤(열 폭 w)으로 채운다(원본 벽·커튼월 질감)."""
    m = mask_of(p, pts); W, H = m.size
    for y in range(H):
        for x in range(W):
            if m.getpixel((x, y)):
                a, b = c1, c2
                if bands:
                    for (y0, y1, ca, cb) in bands:
                        if y0 <= y < y1: a, b = ca, cb
                p.im.putpixel((x, y), a if (x // w) % 2 == 0 else b)

GREENS = [hx('a2e601'), hx('67db00'), hx('6db601'), hx('499201'), hx('009200'), hx('006d00'), hx('004900')]
def tree(p, a, b, scale=1.0):
    """b17 나무 구조: 잎덩이 여러 개가 겹친 울퉁불퉁한 덩어리. 덩이마다 왼쪽 위 하이라이트, 오른쪽 아래·틈 어두운 초록, 짧은 갈색 줄기와 오른쪽으로 번지는 그림자."""
    x, y = p.G(a, b, 0); d = p.d
    d.ellipse([x - 1, y - 1, x + 7, y + 2], fill=hx('3e6a22'))                                    # 접지 그림자(오른쪽 아래로)
    d.rectangle([x - 1, y - 5, x, y], fill=hx('b64900')); d.point((x, y - 4), fill=hx('feb500')); d.point((x, y - 2), fill=hx('feb500')); d.rectangle([x + 1, y - 5, x + 1, y], fill=hx('7a4a14'))
    blobs = [(-3, -9, 4), (3, -9, 4), (0, -13, 4.2), (-4, -6, 3.2), (4, -6, 3.2), (0, -8, 3.6)]
    for (dx, dy, r) in blobs:                                                                  # 뒤→앞
        r = r * scale; cx, cy = x + dx, y + dy
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=GREENS[3])
        d.ellipse([cx - r + 0.5, cy - r + 0.5, cx + r - 1.5, cy + r - 1.5], fill=GREENS[2])
        d.ellipse([cx - r * 0.7, cy - r * 0.8, cx + r * 0.2, cy + r * 0.1], fill=GREENS[1])
        d.point((round(cx - r * 0.45), round(cy - r * 0.55)), fill=GREENS[0]); d.point((round(cx - r * 0.2), round(cy - r * 0.6)), fill=GREENS[0]); d.point((round(cx - r * 0.55), round(cy - r * 0.2)), fill=GREENS[0])
        d.arc([cx - r, cy - r, cx + r, cy + r], 330, 150, fill=GREENS[4]); d.arc([cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1], 20, 110, fill=GREENS[5])
        d.point((round(cx + r * 0.5), round(cy + r * 0.6)), fill=GREENS[6])

def make_pixel_chunpo():
    p = Px(21)
    INK = hx('8a8070'); GROUND = hx('cfc9b6'); GROUNDD = hx('a8a290')
    CREAM = hx('f3e7ab'); MINT = hx('86d8b8'); MINTD = hx('3e9c7c')
    SL = hx('7c9c96'); SLD = hx('566f6a'); SLL = hx('a4c2ba')
    BLU = hx('3f8fe0'); BLUD = hx('1f5aa4'); GLASS = hx('bfe3fa')
    A0, A1, B0, B1, H = 0.10, 0.88, 0.20, 0.56, 14
    # 바닥·잔디·그림자
    p.poly([(0.02, 0.02, 0), (0.98, 0.02, 0), (0.98, 0.98, 0), (0.02, 0.98, 0)], hx('9cc27a'), hx('6f9a54'))
    p.poly([(0.04, 0.60, 0), (0.96, 0.60, 0), (0.96, 0.84, 0), (0.04, 0.84, 0)], GROUND, GROUNDD)                      # 승강장
    for i in range(1, 8): t = 0.04 + 0.92 * i / 8; p.line((t, 0.60, 0), (t, 0.84, 0), shade(GROUND, 0.93))                 # 박석 줄
    p.line((0.04, 0.82, 0), (0.96, 0.82, 0), hx('f0d24a'))                                                                  # 안전선
    p.poly([(0.04, 0.88, 0), (0.96, 0.88, 0), (0.96, 0.98, 0), (0.04, 0.98, 0)], hx('7a746a'), GROUNDD)                    # 선로 자갈
    for r in (0.90, 0.96): p.line((0.04, r, 0), (0.96, r, 0), hx('d0d4de'))
    for i in range(1, 14): t = 0.04 + 0.92 * i / 14; p.line((t, 0.89, 0), (t, 0.97, 0), hx('5a3f2a'))
    p.poly([(0.92, 0.18, 0), (1.0, 0.18, 0), (1.0, 0.60, 0), (0.92, 0.60, 0)], shade(hx('9cc27a'), 0.8), None)               # 건물 그림자(오른쪽 땅)
    # 본채 벽(밝은 앞벽 / 어두운 옆벽) + 석고 질감
    wallpts = [(A0, B1, 0), (A1, B1, 0), (A1, B1, H), (A0, B1, H)]; yt = p.G(A0, B1, H)[1]
    stripes(p, wallpts, hx('fffbc4'), hx('f6e9a8'), 2, bands=[(yt, yt + 4, hx('fffbc4'), hx('f6ec ae'.replace(' ', ''))), (yt + 4, yt + 8, hx('f6e9a8'), hx('e8d78c')), (yt + 8, yt + 30, hx('e8d78c'), hx('d6c076'))])
    sidepts = [(A1, B0, 0), (A1, B1, 0), (A1, B1, H), (A1, B0, H)]
    stripes(p, sidepts, hx('d9a85c'), hx('c79552'), 2, bands=[(yt, yt + 6, hx('e0b068'), hx('cf9c56')), (yt + 6, yt + 30, hx('c98f4e'), hx('b27c44'))])
    for (x0, x1, y0, y1) in ((0, 1, 0, 1),): pass
    p.line((A0, B1, 0), (A0, B1, H), INK); p.line((A1, B1, 0), (A1, B1, H), INK); p.line((A1, B0, 0), (A1, B0, H), INK)
    p.rect_wall(A0, A1, B1, 0, 3, MINT, INK); p.rect_side(A1, B0, B1, 0, 3, MINTD, INK)                                   # 아랫단 민트 띠
    # 문·창(앞벽) — 문틀·유리 반사·창살
    def window(a, w, z0, z1):
        p.rect_wall(a, a + w, B1, z0, z1, GLASS, BLUD)
        p.line((a + w / 2, B1, z0), (a + w / 2, B1, z1), BLUD); p.line((a, B1, (z0 + z1) / 2), (a + w, B1, (z0 + z1) / 2), BLUD)
        p.d.point(p.G(a + w * 0.2, B1, z1 - 1), fill=hx('ffffff')); p.d.point(p.G(a + w * 0.7, B1, z0 + 1), fill=hx('e8f6ff'))
    p.rect_wall(0.20, 0.34, B1, 3, 11, BLU, INK); p.line((0.27, B1, 3), (0.27, B1, 11), INK); p.rect_wall(0.205, 0.265, B1, 8, 10, GLASS, None); p.rect_wall(0.275, 0.335, B1, 8, 10, GLASS, None)
    p.d.point(p.G(0.255, B1, 6), fill=hx('f0d24a')); p.d.point(p.G(0.285, B1, 6), fill=hx('f0d24a'))                          # 손잡이
    window(0.38, 0.06, 4, 11)
    window(0.64, 0.07, 4, 11); window(0.74, 0.07, 4, 11)
    # 옆벽 창
    p.rect_side(A1, 0.28, 0.44, 4, 10, shade(GLASS, 0.85), BLUD); p.line((A1, 0.36, 4), (A1, 0.36, 10), BLUD)
    # 왼쪽 긴 차양(베란다): 얇은 지붕 + 민트 기둥
    p.poly([(0.04, B1 + 0.10, 10.5), (0.34, B1 + 0.10, 10.5), (0.34, B1 - 0.01, 12), (0.04, B1 - 0.01, 12)], SL, INK)
    for a in (0.06, 0.19, 0.32): p.line((a, B1 + 0.09, 0), (a, B1 + 0.09, 10), MINTD); p.line((a + 0.006, B1 + 0.09, 0), (a + 0.006, B1 + 0.09, 10), MINT)
    # 지붕(앞 경사 밝음 / 옆 경사 어두움) + 마름모 기와 + 용마루
    R = 7; o = 0.04; bm = (B0 + B1) / 2
    front = [(A0 - o, B1 + o, H), (A1 + o, B1 + o, H), (A1 - 0.06, bm, H + R), (A0 + 0.06, bm, H + R)]; ry = p.G(A0, bm, H + R)[1]
    checker(p, front, hx('6cb496'), hx('4f9a80'), 0, bands=[(ry - 2, ry + 3, hx('9adcbc'), hx('78c0a2')), (ry + 3, ry + 7, hx('78c0a2'), hx('5ca88c')), (ry + 7, ry + 12, hx('5ca88c'), hx('46907a')), (ry + 12, ry + 40, hx('46907a'), hx('367a66'))])
    side_r = [(A1 + o, B0 - o, H), (A1 + o, B1 + o, H), (A1 - 0.06, bm, H + R)]
    checker(p, side_r, hx('3a7e68'), hx('2d6a58'), 1, bands=[(ry - 2, ry + 8, hx('467e72'), hx('366a60')), (ry + 8, ry + 40, hx('366a60'), hx('2a5650'))])
    p.line((A0 - o, B1 + o, H), (A1 + o, B1 + o, H), hx('3a5450')); p.line((A1 + o, B0 - o, H), (A1 + o, B1 + o, H), hx('2f4744'))
    p.line((A0 + 0.06, bm, H + R), (A1 - 0.06, bm, H + R), hx('d4e8e0'))
    for i in range(1, 5):                                                                                                    # 옆 경사 기와 줄
        t = i / 5; za, zb = H + R * t, H + R * t
        p.line((A1 + o - (o + 0.06) * t, B0 - o + (bm - B0 + o) * t, za), (A1 + o - (o + 0.06) * t, B1 + o - (B1 + o - bm) * t, zb), shade(SLD, 0.8))
    p.line((A0 - o, B1 + o, H), (A1 + o, B1 + o, H), MINT)                                                                  # 처마 민트 선
    # 앞 박공 현관: 흰 박공 면 + 지붕 이음 + 현판 + 문
    GA0, GA1, GB = 0.44, 0.66, B1 + 0.05
    p.rect_wall(GA0, GA1, GB, 0, H - 1, shade(CREAM, 1.03), INK); p.rect_side(GA1, B1 - 0.02, GB, 0, H - 1, shade(CREAM, 0.8), INK)
    p.poly([(GA0 - 0.02, GB, H - 1), (GA1 + 0.02, GB, H - 1), ((GA0 + GA1) / 2, GB, H + 6)], hx('fffdf2'), INK)
    p.poly([((GA0 + GA1) / 2, GB, H + 6), (GA1 + 0.02, GB, H - 1), (GA1 + 0.02, B1 - 0.02, H - 1)], SLD, INK)
    p.poly([(GA0 - 0.02, GB, H - 1), ((GA0 + GA1) / 2, GB, H + 6), ((GA0 + GA1) / 2, bm, H + R - 1)], SL, INK)
    p.line((GA0 - 0.02, GB, H - 1), ((GA0 + GA1) / 2, GB, H + 6), MINT); p.line((GA1 + 0.02, GB, H - 1), ((GA0 + GA1) / 2, GB, H + 6), MINT)
    p.rect_wall(0.50, 0.60, GB, 11, 13, hx('e8d8a8'), hx('3a6a5c'))                                                           # 현판
    p.rect_wall(0.50, 0.60, GB, 2, 10, BLU, INK); p.line((0.55, GB, 2), (0.55, GB, 10), INK); p.rect_wall(0.505, 0.545, GB, 7, 9, GLASS, None); p.rect_wall(0.555, 0.595, GB, 7, 9, GLASS, None)
    # 오른쪽 부속채(벽돌 + 파란 지붕) + 울타리 말뚝 + 나무
    p.rect_wall(0.88, 0.99, 0.22, 0, 6, hx('c47a5a'), INK); p.rect_side(0.99, 0.22, 0.46, 0, 6, hx('8c4c36'), INK)
    p.poly([(0.86, 0.48, 6.5), (1.0, 0.48, 6.5), (1.0, 0.20, 6.5), (0.86, 0.20, 6.5)], BLU, INK); p.line((0.86, 0.34, 6.5), (1.0, 0.34, 6.5), BLUD)
    for i in range(0, 8): p.rect_wall(0.06 + 0.1 * i, 0.075 + 0.1 * i, 0.66, 0, 3, hx('a8743c'), hx('6a4a22'))
    tree(p, 0.02, 0.46, 0.62); tree(p, 0.97, 0.84, 0.62)
    p.outline(INK)
    return p.im

if __name__ == '__main__':
    im = make_pixel_chunpo(); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im)
    bg.resize((im.width * 6, im.height * 6), Image.NEAREST).save(sys.argv[1] if len(sys.argv) > 1 else '/tmp/claude-0/prev/px_chunpo.png')
