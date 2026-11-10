"""익산 모드 건물 스프라이트 5종을 8배 해상도로 그려 면적 평균으로 줄인다 → work/pak_mod/b{19,3,4,15,21}.png
  b19 영화관(CGV)  b3 대형마트(코스트코)  b4 호텔(베스트웨스턴)  b15 본사(테슬라)  b21 익산역(KTX)
게임은 건물 그림을 '한 장 그대로 제자리에' 그리므로(앞 건물과 겹치는 구간만 나눠 그림) 각 그림은 이어진 한 장으로 만든다.
크기는 원본과 같고 바닥 다이아몬드(포장 광장)는 아래에 맞춘다. 사용: python3 -I tools/make_iksan_buildings.py [미리보기.png]"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = root + '/work/pak_mod'; os.makedirs(out, exist_ok=True)
S, W = 8, 96
FONT_B = '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
FONT_K = root + '/android/assets/galmuri11.ttf'
def hx(h, a=255): return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)
def lerp(c0, c1, t): return tuple(int(c0[i] * (1 - t) + c1[i] * t) for i in range(3)) + (255,)

class Sc:
    def __init__(self, H, seed=1):
        self.H, self.WS, self.HS = H, W * S, H * S; self.im = Image.new('RGBA', (self.WS, self.HS), (0, 0, 0, 0)); self.rnd = random.Random(seed); self.nrs = np.random.RandomState(seed)
        self.T = (48, H - 43); self.R = (95, H - 24); self.L = (1, H - 24)           # 바닥 다이아몬드(아래 맞춤)
    def G(self, a, b, z=0.0):
        T, R, L = self.T, self.R, self.L
        return ((T[0] + a * (R[0] - T[0]) + b * (L[0] - T[0])) * S, (T[1] + a * (R[1] - T[1]) + b * (L[1] - T[1]) - z) * S)
    def ground_poly(self): return [self.G(0, 0), self.G(1, 0), self.G(1, 1), self.G(0, 1)]
    def grad(self, pts, c0, c1, d=(0, 1), noise=0.0):
        m = Image.new('L', self.im.size, 0); ImageDraw.Draw(m).polygon(pts, fill=255); bb = m.getbbox()
        if not bb: return
        x0, y0, x1, y1 = bb; ys, xs = np.mgrid[y0:y1, x0:x1]; proj = xs * d[0] + ys * d[1]; t = ((proj - proj.min()) / max(1e-6, proj.max() - proj.min()))[..., None]
        col = np.array(c0[:3]) * (1 - t) + np.array(c1[:3]) * t
        if noise: col = col + self.nrs.normal(0, noise, col.shape[:2])[..., None]
        arr = np.zeros((y1 - y0, x1 - x0, 4), np.uint8); arr[..., :3] = np.clip(col, 0, 255).astype(np.uint8); arr[..., 3] = np.asarray(m.crop(bb))
        self.im.alpha_composite(Image.fromarray(arr, 'RGBA'), (x0, y0))
    def flat(self, pts, c): ImageDraw.Draw(self.im).polygon(pts, fill=c)
    def line(self, pts, c, w): ImageDraw.Draw(self.im).line(pts, fill=c, width=w)
    def shadow(self, pts, a=110, blur=9):
        sh = Image.new('L', self.im.size, 0); ImageDraw.Draw(sh).polygon(pts, fill=a); sh = sh.filter(ImageFilter.GaussianBlur(blur))
        clip = Image.new('L', self.im.size, 0); ImageDraw.Draw(clip).polygon(self.ground_poly(), fill=255); sh = Image.fromarray(np.minimum(np.asarray(sh), np.asarray(clip)))
        lay = Image.new('RGBA', self.im.size, (26, 30, 48, 255)); lay.putalpha(sh); self.im.alpha_composite(lay)
    # ---- 바닥: 포장 광장(연보라 체크) + 두께 ----
    def ground(self, plaza=True):
        G = self.G
        self.grad([G(0, 1), G(1, 1), G(1, 1, -3.2), G(0, 1, -3.2)], hx('c9c4e8'), hx('8a86b8'), (0, 1), 3)
        self.grad([G(1, 0), G(1, 1), G(1, 1, -3.2), G(1, 0, -3.2)], hx('aaa6d2'), hx('6e6a9c'), (0, 1), 3)
        self.grad(self.ground_poly(), hx('dcdcff'), hx('c6c6f2'), (0.4, 1), 3)
        d = ImageDraw.Draw(self.im)
        n = 14
        for i in range(n):                                                    # 블록 포장 줄눈(아이소 격자)
            t = i / n
            d.line([G(t, 0), G(t, 1)], fill=hx('a8a8e0', 110), width=2); d.line([G(0, t), G(1, t)], fill=hx('a8a8e0', 110), width=2)
        d.line([G(0, 1), G(1, 1), G(1, 0)], fill=hx('f4f2ff', 220), width=3)
    def text_face(self, txt, p0, du, dv, k, color, font=FONT_B, size=60, bold_outline=None, shear=0.5, sx=1.0):
        """글자를 아이소 면에 붙인다. p0=면의 글자 왼쪽 위(고해상도 좌표), 면의 가로 기울기 shear(+이면 오른쪽 아래), k=배율."""
        f = ImageFont.truetype(font, size); tmp = Image.new('L', (20, 20)); bb = ImageDraw.Draw(tmp).textbbox((0, 0), txt, font=f)
        tw, th = bb[2] - bb[0], bb[3] - bb[1]; T = Image.new('RGBA', (tw + 8, th + 8), (0, 0, 0, 0)); dd = ImageDraw.Draw(T)
        if bold_outline: dd.text((4 - bb[0], 4 - bb[1]), txt, font=f, fill=bold_outline, stroke_width=3, stroke_fill=bold_outline)
        dd.text((4 - bb[0], 4 - bb[1]), txt, font=f, fill=color)
        ox, oy = p0; s = shear
        data = (1.0 / k, 0, -ox / k, -s / k, 1.0 / k, -oy / k + s * ox / k)
        out = T.transform(self.im.size, Image.AFFINE, data, resample=Image.BICUBIC)
        self.im.alpha_composite(out)
        return (tw * k, th * k)
    # ---- 상자 건물: b=b1 면(왼쪽 아래 향함), a=a1 면(오른쪽 아래 향함) ----
    def box(self, a0, a1, b0, b1, h, wl, wr, roof, z0=0.0):
        G = self.G
        self.grad([G(a0, b1, z0), G(a1, b1, z0), G(a1, b1, z0 + h), G(a0, b1, z0 + h)], wl[0], wl[1], (0.3, -1), 3)
        self.grad([G(a1, b0, z0), G(a1, b1, z0), G(a1, b1, z0 + h), G(a1, b0, z0 + h)], wr[0], wr[1], (0.3, -1), 3)
        self.grad([G(a0, b0, z0 + h), G(a1, b0, z0 + h), G(a1, b1, z0 + h), G(a0, b1, z0 + h)], roof[0], roof[1], (1, 0.5), 3)
        self.line([G(a0, b1, z0 + h), G(a1, b1, z0 + h), G(a1, b0, z0 + h)], hx('ffffff', 170), 3)
        self.line([G(a1, b1, z0), G(a1, b1, z0 + h)], hx('3a3e58', 190), 4)
    def win_grid_L(self, a0, a1, b1, z0, z1, cols, rows, c_top, c_bot, mullion=None, pad=0.18):
        """b=b1 면(가로=a)에 창 격자."""
        G = self.G
        for i in range(cols):
            for j in range(rows):
                aa0 = a0 + (a1 - a0) * (i + pad) / cols; aa1 = a0 + (a1 - a0) * (i + 1 - pad) / cols
                zz0 = z0 + (z1 - z0) * (j + pad) / rows; zz1 = z0 + (z1 - z0) * (j + 1 - pad) / rows
                self.grad([G(aa0, b1, zz0), G(aa1, b1, zz0), G(aa1, b1, zz1), G(aa0, b1, zz1)], c_top, c_bot, (0.2, 1))
                self.line([G(aa0 + (aa1 - aa0) * 0.25, b1, zz1 - 0.2), G(aa0 + (aa1 - aa0) * 0.7, b1, zz0 + 0.3)], hx('ffffff', 110), 2)
    def win_grid_R(self, b0, b1, a1, z0, z1, cols, rows, c_top, c_bot, pad=0.18):
        G = self.G
        for i in range(cols):
            for j in range(rows):
                bb0 = b0 + (b1 - b0) * (i + pad) / cols; bb1 = b0 + (b1 - b0) * (i + 1 - pad) / cols
                zz0 = z0 + (z1 - z0) * (j + pad) / rows; zz1 = z0 + (z1 - z0) * (j + 1 - pad) / rows
                self.grad([G(a1, bb0, zz0), G(a1, bb1, zz0), G(a1, bb1, zz1), G(a1, bb0, zz1)], c_top, c_bot, (0.2, 1))
    def tree(self, a, b, s=1.0):
        x, y = self.G(a, b); d = ImageDraw.Draw(self.im); rs = random.Random(int(a * 977 + b * 331))
        self.shadow([(x - 20 * s, y + 3), (x + 40 * s, y + 3), (x + 48 * s, y + 14 * s), (x - 8 * s, y + 14 * s)], 80, 6)
        d.polygon([(x - 4 * s, y + 2), (x + 4 * s, y + 2), (x + 2.5 * s, y - 38 * s), (x - 2.5 * s, y - 38 * s)], fill=hx('7a5030'))
        cs = []
        for i in range(11):
            ang = rs.random() * 6.28; rr = rs.random() ** 0.6 * 26 * s; cs.append((x + math.cos(ang) * rr, y - 48 * s + math.sin(ang) * rr * 0.8, (10 + rs.random() * 8) * s))
        cs.sort(key=lambda c: -c[1])
        for (cx, cy, r) in cs:
            dk = max(0.0, min(1.0, (cy - (y - 80 * s)) / (50 * s))); col = lerp((130, 205, 80), (36, 112, 48), dk)
            self.grad([(cx + math.cos(t) * r, cy + math.sin(t) * r * 0.86) for t in [k * math.pi / 12 for k in range(24)]], lerp((170, 232, 110), col, 0.5), lerp(col, (22, 80, 36), 0.6), (0.5, 1), 4)
    def finish(self):
        a = np.asarray(self.im).astype(float); al = a[..., 3] / 255.0; H = self.H
        al_s = al.reshape(H, S, W, S).mean((1, 3))
        rgb = (a[..., :3] * al[..., None]).reshape(H, S, W, S, 3).sum((1, 3)) / np.maximum(al.reshape(H, S, W, S).sum((1, 3)), 1e-6)[..., None]
        res = np.zeros((H, W, 4), np.uint8); res[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8); res[..., 3] = (al_s > 0.5) * 255
        im = Image.fromarray(res, 'RGBA'); sh = im.convert('RGB').filter(ImageFilter.UnsharpMask(radius=0.7, percent=45, threshold=2))
        return Image.fromarray(np.dstack([np.asarray(sh), res[..., 3]]), 'RGBA')

# ============================ b19 영화관 CGV ============================
def cinema():
    s = Sc(57, 2); G = s.G; s.ground()
    s.shadow([G(0.15, 0.62), G(0.9, 0.62), G(1.0, 0.7), G(0.25, 0.72)], 100, 10)
    # 뒤쪽 큰 상영관 타워(빨강, CGV 대형 간판)
    s.box(0.46, 0.88, 0.12, 0.40, 23, (hx('f04838'), hx('b3180e')), (hx('c42418'), hx('7a0e08')), (hx('ff6a58'), hx('e03a2c')))
    for j in range(4): s.line([G(0.46, 0.40, 3 + j * 5.0), G(0.88, 0.40, 3 + j * 5.0)], hx('ff9a8a', 70), 3)
        # 글자: 면 길이에 맞춰 배율 결정
    face_w = (G(0.86, 0.40)[0] - G(0.50, 0.40)[0]); k = face_w / 140.0
    s.text_face('CGV', (G(0.50, 0.40)[0] + 4, G(0.50, 0.40, 19.5)[1] + 0.5 * 2), 0, 0, k, hx('ffffff'), size=60, bold_outline=hx('7a0e08'), shear=0.5)
    # 앞쪽 유리 로비(파랑 반사)
    s.box(0.12, 0.84, 0.40, 0.66, 11, (hx('4a80e0'), hx('1e3c9a')), (hx('2c56b8'), hx('14286e')), (hx('d6e0f4'), hx('aabbe0')))
    s.win_grid_L(0.12, 0.84, 0.66, 1.0, 10, 9, 2, hx('9cd0ff'), hx('2a54a8'))
    s.win_grid_R(0.40, 0.66, 0.84, 1.0, 10, 4, 2, hx('78a8ec'), hx('1c3a88'))
    # 캐노피 + 포스터
    s.grad([G(0.20, 0.66, 7), G(0.56, 0.66, 7), G(0.56, 0.76, 7), G(0.20, 0.76, 7)], hx('fafafa'), hx('d8d8ec'), (1, 0.4))
    s.grad([G(0.20, 0.76, 7), G(0.56, 0.76, 7), G(0.56, 0.76, 6.0), G(0.20, 0.76, 6.0)], hx('e8e8f8'), hx('a0a0c0'), (0, 1))
    for i in range(10): x, y = G(0.20 + 0.036 * i, 0.76, 6.2); ImageDraw.Draw(s.im).ellipse([x - 3, y - 2, x + 3, y + 4], fill=hx('fff0a0'))
    for k_, c in enumerate([hx('e84b6a'), hx('f4b430'), hx('3ab0e8'), hx('6ac84a')]):                  # 영화 포스터
        a0 = 0.16 + k_ * 0.16; s.grad([G(a0, 0.66, 2.5), G(a0 + 0.10, 0.66, 2.5), G(a0 + 0.10, 0.66, 8), G(a0, 0.66, 8)], lerp(c, (255, 255, 255), 0.35), c, (0.2, 1))
    # 입구 어두운 문
    s.grad([G(0.30, 0.76, 0), G(0.46, 0.76, 0), G(0.46, 0.76, 5.5), G(0.30, 0.76, 5.5)], hx('1c2440'), hx('3c4c80'), (0, 1))
    s.tree(0.05, 0.84, 0.8); s.tree(0.93, 0.30, 0.8)
    return s.finish()

# ============================ b3 대형마트 코스트코 ============================
def costco():
    s = Sc(73, 3); G = s.G; s.ground()
    # 주차장 아스팔트 + 주차선 + 차량
    s.grad([G(0.04, 0.58), G(0.96, 0.58), G(0.96, 0.96), G(0.04, 0.96)], hx('6a6e80'), hx('50546a'), (0.5, 1), 4)
    for i in range(1, 12): s.line([G(0.04 + 0.077 * i, 0.60), G(0.04 + 0.077 * i, 0.74)], hx('f0f0f0', 200), 2); s.line([G(0.04 + 0.077 * i, 0.80), G(0.04 + 0.077 * i, 0.94)], hx('f0f0f0', 200), 2)
    rs = random.Random(5)
    for (a, b, c) in ((0.13, 0.66, '#d84a3a'), (0.37, 0.68, '#e8e8f0'), (0.60, 0.65, '#3a6ac8'), (0.22, 0.86, '#2a2a34'), (0.53, 0.87, '#e0b02a'), (0.78, 0.85, '#d84a3a'), (0.83, 0.67, '#7a8aa0')):
        cc = hx(c[1:]); s.grad([G(a, b, 0), G(a + 0.06, b, 0), G(a + 0.06, b, 3.4), G(a, b, 3.4)], lerp(cc, (255, 255, 255), 0.25), cc, (0.3, 1)); s.grad([G(a + 0.06, b - 0.05, 0), G(a + 0.06, b, 0), G(a + 0.06, b, 3.4), G(a + 0.06, b - 0.05, 3.4)], lerp(cc, (0, 0, 0), 0.3), lerp(cc, (0, 0, 0), 0.5), (0.3, 1))
        s.grad([G(a, b - 0.05, 3.4), G(a + 0.06, b - 0.05, 3.4), G(a + 0.06, b, 3.4), G(a, b, 3.4)], lerp(cc, (255, 255, 255), 0.45), cc, (1, 0.4))
    s.shadow([G(0.06, 0.58), G(0.94, 0.58), G(1.02, 0.66), G(0.14, 0.66)], 105, 10)
    # 창고형 본관: 크림색 벽 + 회색 지붕
    s.box(0.06, 0.94, 0.10, 0.58, 24, (hx('f4efe0'), hx('c8c0a8')), (hx('d4ccb4'), hx('9c947c')), (hx('b8bcc8'), hx('9498a8')))
    for k_ in range(10): s.line([G(0.06 + 0.088 * k_, 0.58, 0), G(0.06 + 0.088 * k_, 0.58, 24)], hx('b0a888', 90), 2)
    # 빨간 코스트코 간판 띠(정면) + 흰 글자 + 파란 라인
    s.grad([G(0.06, 0.58, 14.5), G(0.94, 0.58, 14.5), G(0.94, 0.58, 24), G(0.06, 0.58, 24)], hx('f04a58'), hx('c01830'), (0.2, 1))
    s.line([G(0.06, 0.58, 14.5), G(0.94, 0.58, 14.5)], hx('005dab'), 8)
    face_w = G(0.92, 0.58)[0] - G(0.08, 0.58)[0]
    s.text_face('COSTCO', (G(0.12, 0.58)[0], G(0.12, 0.58, 22.5)[1] + 0.5 * 2), 0, 0, face_w * 0.78 / 300.0, hx('ffffff'), size=70, bold_outline=hx('8a0c1c'), shear=0.5)
    s.text_face('WHOLESALE', (G(0.60, 0.58)[0], G(0.60, 0.58, 16.4)[1] + 0.5 * 2), 0, 0, 0.17, hx('ffffff'), size=40, shear=0.5)
    # 입구(유리) + 카트
    s.grad([G(0.40, 0.58, 0), G(0.58, 0.58, 0), G(0.58, 0.58, 11), G(0.40, 0.58, 11)], hx('88b8e8'), hx('28487c'), (0.2, 1))
    s.line([G(0.49, 0.58, 0), G(0.49, 0.58, 11)], hx('f0f0f0'), 3)
    for k_ in range(5): x, y = G(0.30 + 0.025 * k_, 0.60, 0); ImageDraw.Draw(s.im).rectangle([x - 6, y - 14, x + 6, y - 2], outline=hx('c8ccd8'), width=2)
    # 오른쪽 면 + 지붕 환기창
    s.grad([G(0.94, 0.10, 15.5), G(0.94, 0.58, 15.5), G(0.94, 0.58, 24), G(0.94, 0.10, 24)], hx('c42034'), hx('8a1020'), (0.2, 1))
    for k_ in range(4): s.grad([G(0.20 + 0.18 * k_, 0.22, 24), G(0.32 + 0.18 * k_, 0.22, 24), G(0.32 + 0.18 * k_, 0.34, 24), G(0.20 + 0.18 * k_, 0.34, 24)], hx('b8d8f0'), hx('78a0c8'), (1, 0.4))
    # 주유소 캐노피(왼쪽 앞)
    return s.finish()

# ============================ b4 호텔 베스트웨스턴 ============================
def hotel():
    s = Sc(63, 4); G = s.G; s.ground()
    s.shadow([G(0.18, 0.54), G(0.72, 0.54), G(0.84, 0.64), G(0.30, 0.66)], 100, 10)
    s.box(0.18, 0.72, 0.12, 0.50, 25, (hx('f2e6cc'), hx('cdbb98')), (hx('d4c4a0'), hx('a8966e')), (hx('c8c8d0'), hx('a4a4b4')))
    s.win_grid_L(0.18, 0.72, 0.50, 4, 24, 9, 5, hx('b8d4ec'), hx('5c7aa6'), pad=0.22)
    s.win_grid_R(0.12, 0.50, 0.72, 4, 24, 6, 5, hx('a0c0e0'), hx('4a668c'), pad=0.22)
    for j in range(1, 5): s.line([G(0.18, 0.50, 4 + j * 4.0), G(0.72, 0.50, 4 + j * 4.0)], hx('a8966e', 130), 2)                 # 발코니 줄
    # 저층 로비 날개 + 처마
    s.box(0.18, 0.60, 0.50, 0.70, 7, (hx('e8d8b4'), hx('c0ac80')), (hx('c8b48a'), hx('9c8860')), (hx('7a8aa8'), hx('5c6c8c')))
    s.win_grid_L(0.18, 0.60, 0.70, 0.8, 5.6, 6, 1, hx('c0dcf4'), hx('5c7aa6'))
    s.grad([G(0.14, 0.70, 6.8), G(0.64, 0.70, 6.8), G(0.64, 0.80, 6.8), G(0.14, 0.80, 6.8)], hx('1e4aa0'), hx('0a2a6a'), (1, 0.3))
    s.grad([G(0.14, 0.80, 6.8), G(0.64, 0.80, 6.8), G(0.64, 0.80, 5.6), G(0.14, 0.80, 5.6)], hx('2c5cbc'), hx('0a2a6a'), (0, 1))
    for i in range(4): s.line([G(0.18 + 0.12 * i, 0.80, 5.6), G(0.18 + 0.12 * i, 0.80, 0)], hx('e8e8f0'), 4)               # 기둥
    # 옥상 파란 간판판(BW) + 금빛 왕관 장식
    s.grad([G(0.28, 0.50, 25), G(0.62, 0.50, 25), G(0.62, 0.50, 31), G(0.28, 0.50, 31)], hx('2a5cc0'), hx('0a2a6e'), (0.2, 1))
    s.line([G(0.28, 0.50, 31), G(0.62, 0.50, 31)], hx('f4d060'), 5); s.line([G(0.28, 0.50, 25), G(0.62, 0.50, 25)], hx('f4d060'), 4)
    fw = G(0.60, 0.50)[0] - G(0.30, 0.50)[0]
    s.text_face('BW', (G(0.30, 0.50)[0] + 6, G(0.30, 0.50, 30.4)[1] + 1), 0, 0, fw * 0.62 / 120.0, hx('ffe27a'), size=70, shear=0.5)
    s.text_face('BEST WESTERN', (G(0.30, 0.50)[0] + 6, G(0.30, 0.50, 27.0)[1] + 1), 0, 0, fw * 0.92 / 330.0, hx('ffffff'), size=34, shear=0.5)
    s.tree(0.80, 0.80, 0.8); s.tree(0.06, 0.70, 0.7)
    return s.finish()

# ============================ b15 본사 테슬라 ============================
def tesla():
    s = Sc(60, 5); G = s.G; s.ground()
    s.shadow([G(0.14, 0.58), G(0.86, 0.58), G(0.98, 0.68), G(0.26, 0.70)], 100, 10)
    # 긴 흰색 공장동 + 태양광 지붕
    s.box(0.10, 0.90, 0.14, 0.40, 11, (hx('fafafc'), hx('d4d6e0')), (hx('d8dae4'), hx('aeb0c0')), (hx('2a3a62'), hx('1c284a')))
    for i in range(1, 16): s.line([G(0.10 + 0.05 * i, 0.14, 11), G(0.10 + 0.05 * i, 0.40, 11)], hx('7a90c8', 150), 2)
    for j in range(1, 4): s.line([G(0.10, 0.14 + 0.065 * j, 11), G(0.90, 0.14 + 0.065 * j, 11)], hx('7a90c8', 150), 2)
    for i in range(7): s.grad([G(0.12 + 0.11 * i, 0.40, 3), G(0.20 + 0.11 * i, 0.40, 3), G(0.20 + 0.11 * i, 0.40, 8), G(0.12 + 0.11 * i, 0.40, 8)], hx('90b0e8'), hx('2a3c70'), (0.2, 1))
    # 검은 유리 본사 타워(앞 왼쪽) + 빨간 T 로고 판
    s.box(0.14, 0.52, 0.42, 0.62, 21, (hx('2c3446'), hx('0e121c')), (hx('1c2230'), hx('06080e')), (hx('3a4458'), hx('2a3244')))
    s.win_grid_L(0.14, 0.52, 0.62, 1.5, 20, 7, 6, hx('7a9ad0'), hx('141c34'), pad=0.12)
    s.win_grid_R(0.42, 0.62, 0.52, 1.5, 20, 4, 6, hx('5a7ab4'), hx('0c1224'), pad=0.12)
    s.line([G(0.14, 0.62, 21), G(0.52, 0.62, 21)], hx('e82127'), 6)
    fw = G(0.52, 0.62)[0] - G(0.14, 0.62)[0]
    s.grad([G(0.20, 0.62, 22), G(0.46, 0.62, 22), G(0.46, 0.62, 28), G(0.20, 0.62, 28)], hx('f6f6fa'), hx('d0d2dc'), (0.2, 1))
    s.text_face('TESLA', (G(0.22, 0.62)[0], G(0.22, 0.62, 27.4)[1] + 1), 0, 0, fw * 0.58 / 200.0, hx('e82127'), size=62, shear=0.5)
    # 슈퍼차저 기둥 + 빨간 전기차
    for i in range(5):
        x, y = G(0.60 + 0.07 * i, 0.74); s.grad([(x - 3, y - 20), (x + 3, y - 20), (x + 3, y), (x - 3, y)], hx('f6f6fa'), hx('b0b2c0'), (0.3, 1)); ImageDraw.Draw(s.im).rectangle([x - 3, y - 20, x + 3, y - 14], fill=hx('e82127'))
    cc = hx('e0202a'); a, b = 0.66, 0.84
    s.grad([G(a, b, 0.5), G(a + 0.12, b, 0.5), G(a + 0.12, b, 3.8), G(a, b, 3.8)], hx('ff5a60'), hx('b01018'), (0.3, 1)); s.grad([G(a + 0.12, b - 0.07, 0.5), G(a + 0.12, b, 0.5), G(a + 0.12, b, 3.8), G(a + 0.12, b - 0.07, 3.8)], hx('a0141c'), hx('6a0a10'), (0.3, 1))
    s.grad([G(a + 0.02, b, 3.8), G(a + 0.10, b, 3.8), G(a + 0.09, b, 5.6), G(a + 0.04, b, 5.6)], hx('c8dcf4'), hx('5a78a8'), (0.2, 1))
    s.tree(0.92, 0.62, 0.7)
    return s.finish()

# ============================ b21 익산역 (KTX) ============================
def station():
    s = Sc(48, 6); G = s.G; s.ground()
    # 플랫폼 + KTX 열차(뒤쪽, a축 방향)
    s.grad([G(0.02, 0.02), G(0.98, 0.02), G(0.98, 0.16), G(0.02, 0.16)], hx('b8b8cc'), hx('9c9cb4'), (0, 1))
    s.shadow([G(0.04, 0.16), G(0.96, 0.16), G(1.0, 0.22), G(0.08, 0.22)], 90, 7)
    s.box(0.06, 0.94, 0.04, 0.14, 7, (hx('fafcff'), hx('c8d4e8')), (hx('dfe6f2'), hx('a6b4cc')), (hx('eef2f8'), hx('c0cadc')))
    s.line([G(0.06, 0.14, 3.2), G(0.94, 0.14, 3.2)], hx('2a52b8'), 6); s.line([G(0.06, 0.14, 2.2), G(0.94, 0.14, 2.2)], hx('e8242c'), 3)
    for i in range(14): s.grad([G(0.09 + 0.06 * i, 0.14, 4), G(0.12 + 0.06 * i, 0.14, 4), G(0.12 + 0.06 * i, 0.14, 6.2), G(0.09 + 0.06 * i, 0.14, 6.2)], hx('88b0e4'), hx('2a4478'), (0.2, 1))
    # 역사: 흰/유리 모던 건물 + 큰 곡선 처마 + 파란 간판
    s.shadow([G(0.10, 0.54), G(0.90, 0.54), G(1.0, 0.62), G(0.20, 0.62)], 100, 9)
    s.box(0.10, 0.90, 0.26, 0.54, 12, (hx('f6f4ee'), hx('cfc9bc')), (hx('d6d1c4'), hx('aca597')), (hx('d0d8e8'), hx('aab6cc')))
    s.win_grid_L(0.10, 0.90, 0.54, 1.0, 10.5, 10, 2, hx('b4d6f0'), hx('3e6290'), pad=0.12)
    s.win_grid_R(0.26, 0.54, 0.90, 1.0, 10.5, 4, 2, hx('9cc0e0'), hx('34527c'), pad=0.12)
    # 가운데 맞배 유리 지붕(삼각 박공 느낌) + 처마
    s.grad([G(0.30, 0.54, 12), G(0.70, 0.54, 12), G(0.70, 0.40, 17), G(0.30, 0.40, 17)], hx('cfe4f8'), hx('6a98c8'), (0.2, 1))
    s.line([G(0.30, 0.54, 12), G(0.30, 0.40, 17), G(0.70, 0.40, 17), G(0.70, 0.54, 12)], hx('ffffff'), 4)
    for i in range(1, 8): s.line([G(0.30 + 0.05 * i, 0.54, 12), G(0.30 + 0.05 * i, 0.40, 17)], hx('8ab4dc', 140), 2)
    s.grad([G(0.08, 0.54, 12.5), G(0.92, 0.54, 12.5), G(0.92, 0.62, 12.5), G(0.08, 0.62, 12.5)], hx('fafafa'), hx('d0d0e0'), (1, 0.3))
    s.grad([G(0.08, 0.62, 12.5), G(0.92, 0.62, 12.5), G(0.92, 0.62, 11.2), G(0.08, 0.62, 11.2)], hx('e4e4f4'), hx('9a9ab8'), (0, 1))
    s.grad([G(0.34, 0.62, 9.6), G(0.66, 0.62, 9.6), G(0.66, 0.62, 11.2), G(0.34, 0.62, 11.2)], hx('2a5cc0'), hx('0a2a78'), (0.2, 1))
    fw = G(0.66, 0.62)[0] - G(0.34, 0.62)[0]
    s.text_face('익산역', (G(0.36, 0.62)[0], G(0.36, 0.62, 10.9)[1] + 1), 0, 0, fw * 0.42 / 100.0, hx('ffffff'), font=FONT_K, size=40, shear=0.5)
    s.text_face('KTX', (G(0.56, 0.62)[0], G(0.56, 0.62, 10.8)[1] + 1), 0, 0, fw * 0.30 / 110.0, hx('ff5a5a'), size=42, shear=0.5)
    for i in range(4): s.line([G(0.14 + 0.2 * i, 0.62, 11.2), G(0.14 + 0.2 * i, 0.62, 0)], hx('e8e8f0'), 4) if i != 2 else None
    s.tree(0.04, 0.84, 0.7); s.tree(0.96, 0.72, 0.7)
    return s.finish()

if __name__ == '__main__':
    items = {'b19': cinema, 'b3': costco, 'b4': hotel, 'b15': tesla, 'b21': station}
    sheet = Image.new('RGBA', (5 * 96 * 4 + 40, 80 * 4), (150, 70, 160, 255))
    for k, (n, fn) in enumerate(items.items()):
        im = fn(); im.save(f'{out}/{n}.png'); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im)
        sheet.paste(bg.resize((im.width * 4, im.height * 4), Image.NEAREST), (k * (96 * 4 + 8), 0))
        if len(sys.argv) > 1: bg.resize((im.width * 8, im.height * 8), Image.NEAREST).save(sys.argv[1].replace('.png', f'_{n}.png'))
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('건물 5종 →', out)
