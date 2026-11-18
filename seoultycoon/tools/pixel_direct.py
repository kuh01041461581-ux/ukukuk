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
TREE = ["....BCCB.....", "..BCCCACD....", ".BCCACCACD...", "BCCACADADCD..", "CCAACADDADDI.", "CACCADADDIDI.", "ACCADDDIDIDJ.",
        "DAADDDDIIIJJ.", "DDADDIDIIJJJ.", ".DDDIIIIIJJJ.", "..IIIJIIIJJ..", "...JJJJJJJ..."]
TCOL = {'A': hx('6db601'), 'B': hx('a2e601'), 'C': hx('67db00'), 'D': hx('009200'), 'I': hx('006d00'), 'J': hx('004900')}
def tree(p, a, b, scale=1.0):
    """시장저택(b17) 나무 구조를 옮긴 템플릿: 왼쪽 위 밝은 군집, 오른쪽 아래 어두운 군집, 두 톤 디더 경계. 줄기 갈색 2열 + 오른쪽으로 번지는 그림자."""
    x, y = p.G(a, b, 0); d = p.d
    for yy in range(y - 2, y + 3):                                                                    # 그림자는 바닥 색을 어둡게(색을 새로 칠하지 않는다)
        for xx in range(x - 3, x + 10):
            if ((xx - (x + 3)) / 6.5) ** 2 + ((yy - y) / 2.6) ** 2 <= 1 and 0 <= xx < 96 and 0 <= yy < p.Hc and p.im.getpixel((xx, yy))[3]: p.im.putpixel((xx, yy), shade(p.im.getpixel((xx, yy)), 0.72))
    d.rectangle([x - 1, y - 4, x, y], fill=hx('b64900')); d.rectangle([x + 1, y - 4, x + 1, y], fill=hx('7a4a14')); d.point((x - 1, y - 2), fill=hx('feb500'))
    ox, oy = x - 6, y - 4 - len(TREE)
    for j, row in enumerate(TREE):
        for i, ch in enumerate(row):
            if ch != '.': p.im.putpixel((ox + i, oy + j), TCOL[ch]) if 0 <= ox + i < 96 and 0 <= oy + j < p.Hc else None

def make_pixel_chunpo():
    p = Px(21)
    INK = hx('8a8070'); GROUND = hx('cfc9b6'); GROUNDD = hx('a8a290')
    CREAM = hx('f3e7ab'); MINT = hx('86d8b8'); MINTD = hx('3e9c7c')
    SL = hx('7c9c96'); SLD = hx('566f6a'); SLL = hx('a4c2ba')
    BLU = hx('3f8fe0'); BLUD = hx('1f5aa4'); GLASS = hx('bfe3fa')
    A0, A1, B0, B1, H = 0.10, 0.88, 0.20, 0.56, 14
    # 바닥·잔디·그림자
    p.poly([(0.02, 0.02, 0), (0.98, 0.02, 0), (0.98, 0.98, 0), (0.02, 0.98, 0)], hx('9cc27a'), hx('6f9a54'))
    PH = 2                                                                                                                     # 승강장은 선로보다 2화소 솟아 있다
    p.rect_wall(0.04, 0.96, 0.84, 0, PH, hx('9a9482'), hx('8a8472'))                                                          # 승강장 앞 옆면(그늘)
    p.poly([(0.04, 0.60, PH), (0.96, 0.60, PH), (0.96, 0.84, PH), (0.04, 0.84, PH)], hx('dcd6c4'), hx('c4bea9'))              # 승강장 윗면
    checker(p, [(0.04, 0.60, PH), (0.96, 0.60, PH), (0.96, 0.84, PH), (0.04, 0.84, PH)], hx('dcd6c4'), hx('d2ccb8'), 0)
    for i in range(1, 8): t = 0.04 + 0.92 * i / 8; p.line((t, 0.60, PH), (t, 0.84, PH), hx('bfb9a4'))
    p.line((0.04, 0.82, PH), (0.96, 0.82, PH), hx('e8c84a')); p.line((0.04, 0.835, PH), (0.96, 0.835, PH), hx('b89a30'))       # 안전선(밝은 노랑 + 어두운 노랑)
    # 선로 도상(자갈): 두 톤 체크 + 오른쪽 어두운 가장자리
    bed = [(0.04, 0.86, 0), (0.96, 0.86, 0), (0.96, 0.98, 0), (0.04, 0.98, 0)]
    checker(p, bed, hx('8a8476'), hx('767064'), 0)
    for i in range(0, 15):                                                                                                     # 침목: 윗면 밝은 갈색 + 앞쪽 가장자리 어두운 갈색
        t = 0.05 + 0.90 * i / 14; p.poly([(t, 0.875, 0.6), (t + 0.02, 0.875, 0.6), (t + 0.02, 0.965, 0.6), (t, 0.965, 0.6)], hx('7a5a3a'), None)
        p.line((t, 0.965, 0.6), (t + 0.02, 0.965, 0.6), hx('4e3824'))
    for r, (hi, lo) in ((0.90, (hx('e4e8f0'), hx('8a8e9c'))), (0.94, (hx('e4e8f0'), hx('8a8e9c')))):                            # 레일: 윗면 하이라이트 + 한 줄 아래 그림자
        p.line((0.04, r, 1.2), (0.96, r, 1.2), hi); p.line((0.04, r + 0.012, 0.2), (0.96, r + 0.012, 0.2), lo)
    p.poly([(0.04, 0.98, 0), (0.96, 0.98, 0), (0.96, 1.0, 0), (0.04, 1.0, 0)], hx('5a5448'), None)
    p.poly([(0.90, 0.16, 0), (1.0, 0.16, 0), (1.0, 0.66, 0), (0.88, 0.60, 0)], hx('6d9a54'), None)                                   # 오른쪽 땅 그림자(해가 왼쪽 위)
    p.poly([(0.08, 0.66, 0), (0.36, 0.66, 0), (0.36, 0.78, 0), (0.08, 0.78, 0)], shade(GROUND, 0.82), None)                           # 베란다 아래 승강장 그림자
    p.poly([(0.16, 0.57, 0), (0.88, 0.57, 0), (0.92, 0.64, 0), (0.20, 0.64, 0)], hx('b4ae9a'), None)                                  # 건물 앞 접지 그림자 띠
    # 본채 벽(밝은 앞벽 / 어두운 옆벽) + 석고 질감
    wallpts = [(A0, B1, 0), (A1, B1, 0), (A1, B1, H), (A0, B1, H)]; yt = p.G(A0, B1, H)[1]
    stripes(p, wallpts, hx('fffbc4'), hx('f6e9a8'), 2, bands=[(yt, yt + 4, hx('fffbc4'), hx('f6ec ae'.replace(' ', ''))), (yt + 4, yt + 8, hx('f6e9a8'), hx('e8d78c')), (yt + 8, yt + 30, hx('e8d78c'), hx('d6c076'))])
    sidepts = [(A1, B0, 0), (A1, B1, 0), (A1, B1, H), (A1, B0, H)]
    stripes(p, sidepts, hx('d9a85c'), hx('c79552'), 2, bands=[(yt, yt + 6, hx('e0b068'), hx('cf9c56')), (yt + 6, yt + 30, hx('c98f4e'), hx('b27c44'))])
    for (x0, x1, y0, y1) in ((0, 1, 0, 1),): pass
    import random as _r
    rr_ = _r.Random(4); wm = mask_of(p, wallpts)
    for _ in range(46):                                                                                                        # 석고 얼룩: 3x2 화소 패치를 밝게/어둡게
        x = rr_.randint(0, 95); y = rr_.randint(0, p.Hc - 1)
        if wm.getpixel((x, y)):
            f = rr_.choice((0.93, 0.96, 1.04))
            for dx, dy in ((0, 0), (1, 0), (2, 0), (0, 1), (1, 1)):
                if 0 <= x + dx < 96 and 0 <= y + dy < p.Hc and wm.getpixel((x + dx, y + dy)): p.im.putpixel((x + dx, y + dy), shade(p.im.getpixel((x + dx, y + dy)), f))
    for a_ in (A0 + 0.005, 0.36, 0.49, 0.61, 0.73, 0.855):                                                                  # 벽기둥(밝은 왼쪽 면 + 어두운 오른쪽 면)
        p.rect_wall(a_, a_ + 0.012, B1, 0, H, hx('fff6c8'), None); p.rect_wall(a_ + 0.012, a_ + 0.022, B1, 0, H, hx('c9b070'), None)
    p.rect_wall(A0, A1, B1, 3, 4, hx('c8b274'), None); p.rect_wall(A0, A1, B1, 4, 4.6, hx('fffbd0'), None)                    # 걸레받이 + 윗선
    p.rect_wall(A0, A1, B1, H - 1.5, H, hx('b8a468'), None)                                                                   # 코니스(처마 아래 띠)
    for a_ in (0.12, 0.30):                                                                                                    # 베란다 지붕이 벽에 드리우는 그림자
        pass
    p.rect_wall(A0, 0.36, B1, 5, 11, shade(hx('f3e7ab'), 0.82), None) if False else None
    p.line((A0, B1, 0), (A0, B1, H), INK); p.line((A1, B1, 0), (A1, B1, H), INK); p.line((A1, B0, 0), (A1, B0, H), INK)
    p.rect_wall(A0, A1, B1, 0, 3, MINT, INK); p.rect_side(A1, B0, B1, 0, 3, MINTD, INK)                                   # 아랫단 민트 띠
    # 문·창(앞벽) — 문틀·유리 반사·창살
    GL = [hx('dff3ff'), hx('b0d8f4'), hx('82b8e0'), hx('5c92c2')]
    def window(a, w, z0, z1):
        n = 4
        for i in range(n):                                                                  # 유리: 위→아래 4단계(하늘 반사 → 방 안 어두움)
            p.rect_wall(a, a + w, B1, z1 - (z1 - z0) * (i + 1) / n, z1 - (z1 - z0) * i / n, GL[i], GL[i])
        p.line((a, B1, z0), (a, B1, z1), hx('fff7d0')); p.line((a + w, B1, z0), (a + w, B1, z1), BLUD)                      # 창틀(밝은 왼쪽 / 어두운 오른쪽)
        p.line((a, B1, z1), (a + w, B1, z1), hx('fff7d0')); p.line((a + w / 2, B1, z0), (a + w / 2, B1, z1), hx('e8d890'))     # 윗틀·세로 창살
        p.line((a, B1, (z0 + z1) / 2 + 0.5), (a + w, B1, (z0 + z1) / 2 + 0.5), hx('e8d890'))
        p.line((a - 0.01, B1, z0 - 0.4), (a + w + 0.01, B1, z0 - 0.4), hx('b79255'))                                      # 창턱
        x0, y0 = p.G(a + w * 0.15, B1, z1 - 1); p.d.point((x0, y0), fill=hx('ffffff')); p.d.point((x0 + 1, y0 + 1), fill=hx('f4fbff'))   # 사선 반사
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
    for zz, f in ((H - 1, 0.78), (H - 2, 0.88)):                                                                              # 처마 밑 벽 그림자(윗부분 어둡게)
        xa, ya = p.G(A0, B1, zz); xb, yb = p.G(A1, B1, zz)
        for i in range(xb - xa + 1):
            x = xa + i; y = round(ya + (yb - ya) * i / max(xb - xa, 1))
            if 0 <= x < 96 and 0 <= y < p.Hc and p.im.getpixel((x, y))[3]: p.im.putpixel((x, y), shade(p.im.getpixel((x, y)), f))
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
    wf = [(0.88, 0.46, 0), (0.99, 0.46, 0), (0.99, 0.46, 6), (0.88, 0.46, 6)]; ws = [(0.99, 0.22, 0), (0.99, 0.46, 0), (0.99, 0.46, 6), (0.99, 0.22, 6)]
    stripes(p, [(0.88, 0.22, 0), (0.99, 0.22, 0), (0.99, 0.22, 6), (0.88, 0.22, 6)], hx('d49070'), hx('c47a5a'), 2)
    stripes(p, ws, hx('9a5a40'), hx('8a4e38'), 2)
    ay = p.G(0.88, 0.34, 8)[1]
    checker(p, [(0.86, 0.50, 6), (1.0, 0.50, 6), (1.0, 0.34, 9), (0.86, 0.34, 9)], hx('7aa4d0'), hx('5e88b8'), 0, bands=[(ay - 4, ay + 2, hx('a4c8e8'), hx('86acd4')), (ay + 2, ay + 5, hx('7aa4d0'), hx('5e88b8')), (ay + 5, ay + 20, hx('5e88b8'), hx('46709c'))])   # 앞 경사
    checker(p, [(1.0, 0.50, 6), (1.0, 0.18, 6), (1.0, 0.34, 9)], hx('46709c'), hx('385c86'), 1)                                                        # 옆 경사(어둡게)
    p.line((0.86, 0.50, 6), (1.0, 0.50, 6), hx('2c4a70')); p.line((0.86, 0.34, 9), (1.0, 0.34, 9), hx('cfe2f4'))
    p.poly([(0.88, 0.50, 0), (0.99, 0.50, 0), (1.0, 0.56, 0), (0.88, 0.56, 0)], hx('7a9a60'), None)                                                    # 처마 아래 땅 그림자
    for i in range(0, 8): p.rect_wall(0.06 + 0.1 * i, 0.075 + 0.1 * i, 0.66, 0, 3, hx('a8743c'), hx('6a4a22'))
    tree(p, 0.94, 0.10, 0.62); tree(p, 0.985, 0.72, 0.55)
    p.outline(INK)
    return p.im

if __name__ == '__main__':
    im = make_pixel_chunpo(); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im)
    bg.resize((im.width * 6, im.height * 6), Image.NEAREST).save(sys.argv[1] if len(sys.argv) > 1 else '/tmp/claude-0/prev/px_chunpo.png')
