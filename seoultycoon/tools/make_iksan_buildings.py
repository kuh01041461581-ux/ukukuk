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
    ZS = 1.0
    OVR = None            # (캔버스 높이, 바닥 마름모의 가장 넓은 줄): 게임 조각 규칙용(2:1 마름모). tools/strips.py 참고
    def __init__(self, H, seed=1):
        ovr = Sc.OVR
        if ovr: H = ovr[0]
        self.H, self.WS, self.HS = H, W * S, H * S; self.im = Image.new('RGBA', (self.WS, self.HS), (0, 0, 0, 0)); self.rnd = random.Random(seed); self.nrs = np.random.RandomState(seed)
        self.T = (48, H - 43); self.R = (95, H - 24); self.L = (1, H - 24)           # 바닥 다이아몬드(아래 맞춤)
        if ovr: self.T = (48, ovr[1] - 24); self.R = (95, ovr[1]); self.L = (1, ovr[1])
    FS = 1.0           # 건물 규모 배율(바닥 중심 기준 가로·세로로 키운다). 바닥 마름모(ground)는 키우지 않는다
    def G(self, a, b, z=0.0):
        if Sc.FS != 1.0 and not getattr(self, '_gr', False): a = 0.5 + (a - 0.5) * Sc.FS; b = 0.5 + (b - 0.5) * Sc.FS
        T, R, L = self.T, self.R, self.L
        return ((T[0] + a * (R[0] - T[0]) + b * (L[0] - T[0])) * S, (T[1] + a * (R[1] - T[1]) + b * (L[1] - T[1]) - z * Sc.ZS) * S)      # ZS: 높이 배율(원작처럼 캔버스 윗부분까지 건물을 키울 때 build_sprites 가 자동으로 정한다)
    def ground_poly(self):
        self._gr = True
        try: return [self.G(0, 0), self.G(1, 0), self.G(1, 1), self.G(0, 1)]
        finally: self._gr = False
    def grad(self, pts, c0, c1, d=(0, 1), noise=0.0):
        m = Image.new('L', self.im.size, 0); ImageDraw.Draw(m).polygon(pts, fill=255); bb = m.getbbox()
        if not bb: return
        x0, y0, x1, y1 = bb; ys, xs = np.mgrid[y0:y1, x0:x1]; proj = xs * d[0] + ys * d[1]; t = ((proj - proj.min()) / max(1e-6, proj.max() - proj.min()))[..., None]
        col = np.array(c0[:3]) * (1 - t) + np.array(c1[:3]) * t
        if noise: col = col + self.nrs.normal(0, noise, col.shape[:2])[..., None]
        arr = np.zeros((y1 - y0, x1 - x0, 4), np.uint8); arr[..., :3] = np.clip(col, 0, 255).astype(np.uint8); arr[..., 3] = np.asarray(m.crop(bb))
        self.im.alpha_composite(Image.fromarray(arr, 'RGBA'), (x0, y0))
    def _blend(self, fn, pts, c, w=0):
        """반투명 색은 덮어쓰지 않고 섞는다(덮어쓰면 반투명 구멍이 생긴다)."""
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]; m = w + 2
        x0, y0 = max(0, int(min(xs)) - m), max(0, int(min(ys)) - m); x1, y1 = min(self.im.width, int(max(xs)) + m + 1), min(self.im.height, int(max(ys)) + m + 1)
        if x1 <= x0 or y1 <= y0: return
        tmp = Image.new('RGBA', (x1 - x0, y1 - y0), (0, 0, 0, 0)); fn(ImageDraw.Draw(tmp), [(x - x0, y - y0) for x, y in pts], c, w); self.im.alpha_composite(tmp, (x0, y0))
    def flat(self, pts, c):
        if len(c) > 3 and c[3] < 255: self._blend(lambda d, p, cc, w: d.polygon(p, fill=cc), pts, c)
        else: ImageDraw.Draw(self.im).polygon(pts, fill=c)
    def line(self, pts, c, w):
        if len(c) > 3 and c[3] < 255: self._blend(lambda d, p, cc, ww: d.line(p, fill=cc, width=ww), pts, c, w)
        else: ImageDraw.Draw(self.im).line(pts, fill=c, width=w)
    def shadow(self, pts, a=110, blur=9):
        sh = Image.new('L', self.im.size, 0); ImageDraw.Draw(sh).polygon(pts, fill=a); sh = sh.filter(ImageFilter.GaussianBlur(blur))
        clip = Image.new('L', self.im.size, 0); ImageDraw.Draw(clip).polygon(self.ground_poly(), fill=255); sh = Image.fromarray(np.minimum(np.asarray(sh), np.asarray(clip)))
        lay = Image.new('RGBA', self.im.size, (26, 30, 48, 255)); lay.putalpha(sh); self.im.alpha_composite(lay)
    # ---- 바닥: 포장 광장(연보라 체크) + 두께 ----
    def ground(self, plaza=True):
        self._gr = True
        try: self._ground(plaza)
        finally: self._gr = False
    def _ground(self, plaza=True):
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
    LAST_HD = None        # 방금 finish() 한 그림의 2배 해상도판(리마스터 HD 층용). build_sprites 가 가져간다
    def finish(self):
        if S % 2 == 0: Sc.LAST_HD = self._down(2)
        return self._down(1)
    def _down(self, k):
        """8배로 그린 그림을 k배 해상도(k=1: 원래 크기, k=2: HD)로 면적 평균해 줄인다."""
        a = np.asarray(self.im).astype(float); al = a[..., 3] / 255.0; H = self.H * k; Wk = W * k; q = S // k
        al_s = al.reshape(H, q, Wk, q).mean((1, 3))
        rgb = (a[..., :3] * al[..., None]).reshape(H, q, Wk, q, 3).sum((1, 3)) / np.maximum(al.reshape(H, q, Wk, q).sum((1, 3)), 1e-6)[..., None]
        res = np.zeros((H, Wk, 4), np.uint8); res[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8); res[..., 3] = (al_s > 0.5) * 255
        im = Image.fromarray(res, 'RGBA'); sh = im.convert('RGB').filter(ImageFilter.UnsharpMask(radius=0.7, percent=45, threshold=2))
        return Image.fromarray(np.dstack([np.asarray(sh), res[..., 3]]), 'RGBA')

# ============================ b19 영화관 롯데시네마 (익산: 흰 곡면 건물 + 청회색 마름모 지붕 + 구리색 띠 + 빨간 LOTTE CINEMA) ============================
def cinema():
    s = Sc(57, 2); G = s.G; s.ground(); rs = random.Random(8)
    A0, A1, B0, B1, HH = 0.10, 0.90, 0.20, 0.66, 14.0
    s.shadow([G(0.14, B1), G(A1, B1), G(1.0, 0.76), G(0.26, 0.78)], 100, 10)
    s.box(A0, A1, B0, B1, HH, (hx('f8f6ee'), hx('d6d2c4')), (hx('d8d4c6'), hx('a8a498')), (hx('c0c2dc'), hx('9a9cc0')))
    for k in range(1, 12): s.line([G(A0 + (A1 - A0) * k / 12, B1, 4.5), G(A0 + (A1 - A0) * k / 12, B1, HH)], hx('c4c0b0', 90), 2)
    # 층별 띠창(청록 유리) — 위층일수록 짧게(곡면 느낌)
    for fl, (z0, z1, a_end) in enumerate([(4.6, 6.4, 0.88), (7.2, 9.0, 0.82), (9.8, 11.6, 0.74)]):
        s.grad([G(A0 + 0.04, B1, z0), G(a_end, B1, z0), G(a_end, B1, z1), G(A0 + 0.04, B1, z1)], hx('a6d0d8'), hx('3a6a82'), (0.2, 1))
        for k in range(1, 10): s.line([G(A0 + 0.04 + (a_end - A0 - 0.04) * k / 10, B1, z0), G(A0 + 0.04 + (a_end - A0 - 0.04) * k / 10, B1, z1)], hx('f0f0e8'), 3)
    # 1층 상가 유리 + 흰 기둥
    s.grad([G(A0, B1, 0), G(A1, B1, 0), G(A1, B1, 4.0), G(A0, B1, 4.0)], hx('4a5a78'), hx('1e2842'), (0.2, 1))
    for k in range(0, 9): s.line([G(A0 + (A1 - A0) * k / 8, B1, 0), G(A0 + (A1 - A0) * k / 8, B1, 4.3)], hx('f2f0ea'), 6)
    s.grad([G(A1, B0, 0), G(A1, B1, 0), G(A1, B1, 3.8), G(A1, B0, 3.8)], hx('5a6a88'), hx('2a3450'), (0.2, 1))
    # 구리색 메시 곡선 띠(대각선으로 흐르는 장식)
    for t in np.linspace(0, 1, 60):
        a = A0 + 0.02 + (A1 - A0 - 0.04) * t; z = 2.4 + 7.4 * (t ** 1.1); w = 1.2 + 0.9 * math.sin(t * math.pi)
        s.flat([G(a, B1 + 0.004, z), G(a + 0.016, B1 + 0.004, z), G(a + 0.016, B1 + 0.004, z + w), G(a, B1 + 0.004, z + w)], hx('c88a6a') if int(t * 60) % 2 else hx('a86a50'))
    # 지붕 외장: 청회색 마름모 무늬 판(앞 띠 + 윗면) + 빨간 LOTTE CINEMA
    RZ = 3.6
    s.grad([G(A0 - 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH + RZ), G(A0 - 0.02, B1 + 0.02, HH + RZ)], hx('b4b6dc'), hx('7a7cae'), (0.2, 1))
    s.grad([G(A1 + 0.02, B0, HH), G(A1 + 0.02, B1 + 0.02, HH), G(A1 + 0.02, B1 + 0.02, HH + RZ), G(A1 + 0.02, B0, HH + RZ)], hx('8a8cb8'), hx('5a5c88'), (0.2, 1))
    s.grad([G(A0 - 0.02, B0, HH + RZ), G(A1 + 0.02, B0, HH + RZ), G(A1 + 0.02, B1 + 0.02, HH + RZ), G(A0 - 0.02, B1 + 0.02, HH + RZ)], hx('d2d4ec'), hx('a8aad0'), (1, 0.4))
    for i in range(0, 26):
        for j in range(0, 3):
            a = A0 - 0.01 + 0.032 * i + (0.016 if j % 2 else 0); z = HH + 0.5 + j * 1.3
            if a < A1: s.flat([G(a, B1 + 0.02, z + 0.45), G(a + 0.016, B1 + 0.02, z), G(a + 0.032, B1 + 0.02, z + 0.45), G(a + 0.016, B1 + 0.02, z + 0.9)], hx('5a5c92', 200))
    fw = G(0.86, B1)[0] - G(0.12, B1)[0]
    s.text_face('LOTTE CINEMA', (G(0.14, B1)[0], G(0.14, B1, HH + 3.2)[1] + 1), 0, 0, fw * 0.74 / 560.0, hx('d6203a'), size=60, bold_outline=hx('a0121e'), shear=0.5)
    # ---- 세부 묘사: 영화관다운 특징 ----
    d = ImageDraw.Draw(s.im)
    # (1) 1층 로비: 따뜻한 불빛 창 + 가운데 마퀴(전구 캐노피)
    for k in range(0, 8):
        a0 = A0 + 0.012 + (A1 - A0) * k / 8
        s.grad([G(a0, B1 + 0.003, 0.4), G(a0 + 0.068, B1 + 0.003, 0.4), G(a0 + 0.068, B1 + 0.003, 3.6), G(a0, B1 + 0.003, 3.6)], hx('ffe08a'), hx('c87a2a'), (0, 1))
    s.box(0.36, 0.64, B1, B1 + 0.09, 1.2, (hx('d6203a'), hx('8a1020')), (hx('a81428'), hx('6a0c1a')), (hx('f4486a'), hx('d6203a')), 4.4)
    for k in range(0, 14): x, y = G(0.37 + 0.267 * k / 13, B1 + 0.092, 4.9); d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=hx('fff6b0'))
    # (2) 영화 포스터 보드: 오른쪽 벽에 큼직한 포스터 3장(색·실루엣이 다르다)
    for i, (c0, c1) in enumerate(((hx('f06a3a'), hx('7a1e10')), (hx('4a8af0'), hx('10286a')), (hx('f4d24a'), hx('8a5a10')))):
        b0 = B0 + 0.04 + 0.13 * i; s.grad([G(A1 + 0.004, b0, 4.6), G(A1 + 0.004, b0 + 0.10, 4.6), G(A1 + 0.004, b0 + 0.10, 11.2), G(A1 + 0.004, b0, 11.2)], c0, c1, (0, 1))
        x, y = G(A1 + 0.005, b0 + 0.05, 8.0); d.ellipse([x - 5, y - 8, x + 5, y + 2], fill=hx('1a1a22')); d.polygon([(x - 9, y + 14), (x + 9, y + 14), (x + 5, y), (x - 5, y)], fill=hx('1a1a22'))
        s.line([G(A1 + 0.005, b0, 4.6), G(A1 + 0.005, b0 + 0.10, 4.6), G(A1 + 0.005, b0 + 0.10, 11.2), G(A1 + 0.005, b0, 11.2), G(A1 + 0.005, b0, 4.6)], hx('f4f4f4'), 2)
    # (3) 세로 간판탑: 오른쪽 앞 모서리에 솟은 빨간 LOTTE 간판
    s.box(0.88, 0.95, 0.56, 0.62, 20.0, (hx('e02a44'), hx('9a1020')), (hx('b81c30'), hx('7a0c1a')), (hx('f4546e'), hx('e02a44')), 0)
    for q in range(0, 4): x, y = G(0.915, 0.62, 4.0 + q * 3.6); d.rectangle([x - 5, y - 6, x + 5, y + 6], fill=hx('ffffff') if q % 2 == 0 else hx('ffd0d6'))
    # (4) 옥상 설비 + 안테나
    s.box(0.18, 0.34, 0.30, 0.44, 2.6, (hx('d0d2e4'), hx('9a9cba')), (hx('a8aac4'), hx('7a7c9a')), (hx('e4e6f4'), hx('c0c2dc')), HH + RZ)
    s.box(0.44, 0.52, 0.34, 0.42, 1.8, (hx('d0d2e4'), hx('9a9cba')), (hx('a8aac4'), hx('7a7c9a')), (hx('e4e6f4'), hx('c0c2dc')), HH + RZ)
    x, y = G(0.60, 0.36, HH + RZ); d.line([(x, y), (x, y - 50)], fill=hx('e4e6f0'), width=4); d.ellipse([x - 5, y - 55, x + 5, y - 45], fill=hx('ff3030'))
    # 앞 도로 쪽 자동차 + 가로수
    for (a, cc) in ((0.22, '#e8e8f0'), (0.50, '#2a2a34'), (0.74, '#8a98b0')):
        c = hx(cc[1:]); b = 0.86
        s.grad([G(a, b, 0.3), G(a + 0.06, b, 0.3), G(a + 0.06, b, 2.8), G(a, b, 2.8)], lerp(c, (255, 255, 255), 0.25), c, (0.3, 1)); s.grad([G(a, b - 0.05, 2.8), G(a + 0.06, b - 0.05, 2.8), G(a + 0.06, b, 2.8), G(a, b, 2.8)], lerp(c, (255, 255, 255), 0.4), c, (1, 0.4))
    s.tree(0.94, 0.30, 0.8); s.tree(0.04, 0.80, 0.8)
    return s.finish()

# ============================ b3 대형마트 코스트코 ============================
def costco():
    s = Sc(73, 3); G = s.G; s.ground()
    s.box(0.0, 0.0, 0.0, 0.0, 0, (hx('000000'), hx('000000')), (hx('000000'), hx('000000')), (hx('000000'), hx('000000'))) if False else None
    # 주차장 아스팔트 + 주차선 + 차량
    s.grad([G(0.04, 0.58), G(0.96, 0.58), G(0.96, 0.96), G(0.04, 0.96)], hx('6a6e80'), hx('50546a'), (0.5, 1), 4)
    for i in range(1, 12): s.line([G(0.04 + 0.077 * i, 0.60), G(0.04 + 0.077 * i, 0.74)], hx('f0f0f0', 200), 2); s.line([G(0.04 + 0.077 * i, 0.80), G(0.04 + 0.077 * i, 0.94)], hx('f0f0f0', 200), 2)
    rs = random.Random(5)
    for (a, b, c) in ((0.13, 0.66, '#d84a3a'), (0.37, 0.68, '#e8e8f0'), (0.60, 0.65, '#3a6ac8'), (0.22, 0.86, '#2a2a34'), (0.53, 0.87, '#e0b02a'), (0.78, 0.85, '#d84a3a'), (0.83, 0.67, '#7a8aa0')):
        cc = hx(c[1:]); s.grad([G(a, b, 0), G(a + 0.06, b, 0), G(a + 0.06, b, 3.4), G(a, b, 3.4)], lerp(cc, (255, 255, 255), 0.25), cc, (0.3, 1)); s.grad([G(a + 0.06, b - 0.05, 0), G(a + 0.06, b, 0), G(a + 0.06, b, 3.4), G(a + 0.06, b - 0.05, 3.4)], lerp(cc, (0, 0, 0), 0.3), lerp(cc, (0, 0, 0), 0.5), (0.3, 1))
        s.grad([G(a, b - 0.05, 3.4), G(a + 0.06, b - 0.05, 3.4), G(a + 0.06, b, 3.4), G(a, b, 3.4)], lerp(cc, (255, 255, 255), 0.45), cc, (1, 0.4))
    s.shadow([G(0.06, 0.58), G(0.94, 0.58), G(1.02, 0.66), G(0.14, 0.66)], 105, 10)
    # 지상 3층 창고형(익산 왕궁면 건립 추진안): 1층 매장 + 위 두 층 주차 데크
    A0, A1, B0, B1 = 0.06, 0.94, 0.10, 0.58; H1 = 11.0; DK = 6.2
    s.box(A0, A1, B0, B1, H1, (hx('f4efe0'), hx('c8c0a8')), (hx('d4ccb4'), hx('9c947c')), (hx('b8bcc8'), hx('9498a8')))
    for k_ in range(10): s.line([G(A0 + 0.088 * k_, B1, 0), G(A0 + 0.088 * k_, B1, H1)], hx('b0a888', 90), 2)
    # 빨간 코스트코 간판 띠(정면) + 흰 글자 + 파란 라인
    s.grad([G(A0, B1, 5.6), G(A1, B1, 5.6), G(A1, B1, H1), G(A0, B1, H1)], hx('f04a58'), hx('c01830'), (0.2, 1))
    s.line([G(A0, B1, 5.6), G(A1, B1, 5.6)], hx('005dab'), 8)
    face_w = G(0.92, B1)[0] - G(0.08, B1)[0]
    s.text_face('COSTCO', (G(0.12, B1)[0], G(0.12, B1, 10.2)[1] + 1), 0, 0, face_w * 0.78 / 300.0, hx('ffffff'), size=70, bold_outline=hx('8a0c1c'), shear=0.5)
    s.text_face('WHOLESALE', (G(0.60, B1)[0], G(0.60, B1, 6.8)[1] + 1), 0, 0, 0.15, hx('ffffff'), size=40, shear=0.5)
    s.grad([G(0.40, B1, 0), G(0.58, B1, 0), G(0.58, B1, 5.0), G(0.40, B1, 5.0)], hx('88b8e8'), hx('28487c'), (0.2, 1))
    s.line([G(0.49, B1, 0), G(0.49, B1, 5.0)], hx('f0f0f0'), 3)
    for k_ in range(5): x, y = G(0.30 + 0.025 * k_, B1 + 0.02, 0); ImageDraw.Draw(s.im).rectangle([x - 6, y - 14, x + 6, y - 2], outline=hx('c8ccd8'), width=2)
    # 주차 데크 2층·3층: 얇은 바닥판 + 기둥 + 난간 + 자동차
    for lv in range(2):
        z0 = H1 + lv * DK
        s.grad([G(A0, B0, z0 + 0.1), G(A1, B0, z0 + 0.1), G(A1, B1, z0 + 0.1), G(A0, B1, z0 + 0.1)], hx('c4c8d4'), hx('a4a8b8'), (1, 0.4))              # 바닥(콘크리트)
        for i in range(0, 11): s.line([G(A0 + 0.088 * i, B1, z0), G(A0 + 0.088 * i, B1, z0 + DK - 0.8)], hx('d8d8e0'), 5)                              # 기둥
        s.grad([G(A0, B1, z0 + DK - 0.8), G(A1, B1, z0 + DK - 0.8), G(A1, B1, z0 + DK), G(A0, B1, z0 + DK)], hx('e8e8f0'), hx('b8b8c8'), (0, 1))     # 앞 보
        s.grad([G(A1, B0, z0 + DK - 0.8), G(A1, B1, z0 + DK - 0.8), G(A1, B1, z0 + DK), G(A1, B0, z0 + DK)], hx('b8b8c8'), hx('8c8ca0'), (0, 1))
        s.grad([G(A1, B0, z0), G(A1, B1, z0), G(A1, B1, z0 + DK - 0.8), G(A1, B0, z0 + DK - 0.8)], hx('9a9ab0'), hx('6e6e88'), (0.3, 1))              # 오른쪽 어두운 면(안쪽이 보임)
        for j in range(7):                                                                                                                         # 데크 위 차량
            if (j + lv) % 3 == 1: continue
            cc = [hx('d84a3a'), hx('e8e8f0'), hx('3a6ac8'), hx('2a2a34'), hx('e0b02a')][(j * 2 + lv) % 5]; a = A0 + 0.04 + j * 0.125; b = B0 + 0.12 + (j % 2) * 0.14
            s.grad([G(a, b + 0.04, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 2.6), G(a, b + 0.04, z0 + 2.6)], lerp(cc, (255, 255, 255), 0.25), cc, (0.3, 1))
            s.grad([G(a + 0.06, b - 0.01, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 0.1), G(a + 0.06, b + 0.04, z0 + 2.6), G(a + 0.06, b - 0.01, z0 + 2.6)], lerp(cc, (0, 0, 0), 0.3), lerp(cc, (0, 0, 0), 0.5), (0.3, 1))
    zt = H1 + 2 * DK
    s.grad([G(A0, B0, zt), G(A1, B0, zt), G(A1, B1, zt), G(A0, B1, zt)], hx('d0d4e0'), hx('b0b4c4'), (1, 0.4))
    for k_ in range(3): s.grad([G(0.22 + 0.2 * k_, 0.22, zt), G(0.32 + 0.2 * k_, 0.22, zt), G(0.32 + 0.2 * k_, 0.36, zt), G(0.22 + 0.2 * k_, 0.36, zt)], hx('b8d8f0'), hx('78a0c8'), (1, 0.4))
    # 오른쪽 경사로(주차 진입)
    s.grad([G(A1, 0.16, H1), G(A1, 0.52, H1 + 0.1), G(A1 + 0.05, 0.52, 0), G(A1 + 0.05, 0.16, 0)], hx('9898b0'), hx('6a6a88'), (0.3, 1))
    # 주유소 캐노피(왼쪽 앞)
    return s.finish()

# ============================ b4 호텔 웨스턴라이프 (익산 동서로) ============================
def hotel():
    s = Sc(63, 4); G = s.G; s.ground()
    s.shadow([G(0.18, 0.54), G(0.74, 0.54), G(0.86, 0.64), G(0.30, 0.66)], 100, 10)
    # 본관: 크림색 석재 + 짙은 청동색 유리 세로 띠 (10층 느낌)
    s.box(0.18, 0.74, 0.12, 0.50, 25, (hx('efe6d0'), hx('c4b598')), (hx('c8bba0'), hx('968a70')), (hx('d4d0c8'), hx('aeaaa0')))
    for i in range(7):
        a0 = 0.20 + 0.075 * i; s.grad([G(a0, 0.50, 5), G(a0 + 0.045, 0.50, 5), G(a0 + 0.045, 0.50, 24), G(a0, 0.50, 24)], hx('9a8a72'), hx('3a3226'), (0.2, 1))
        for j in range(1, 9): s.line([G(a0, 0.50, 5 + j * 2.1), G(a0 + 0.045, 0.50, 5 + j * 2.1)], hx('d8c8a8', 160), 2)
    for i in range(5):
        b0 = 0.14 + 0.07 * i; s.grad([G(0.74, b0, 5), G(0.74, b0 + 0.04, 5), G(0.74, b0 + 0.04, 24), G(0.74, b0, 24)], hx('8a7a62'), hx('2e271c'), (0.2, 1))
    for j in range(1, 8): s.line([G(0.18, 0.50, 5 + j * 2.6), G(0.74, 0.50, 5 + j * 2.6)], hx('b8a888', 120), 2)
    # 저층 로비 + 유리 캐노피
    s.box(0.18, 0.62, 0.50, 0.70, 6.5, (hx('e8d8b4'), hx('c0ac80')), (hx('c8b48a'), hx('9c8860')), (hx('8a7a60'), hx('6c5e48')))
    s.grad([G(0.20, 0.70, 0.8), G(0.60, 0.70, 0.8), G(0.60, 0.70, 5.4), G(0.20, 0.70, 5.4)], hx('88a4c0'), hx('24344c'), (0.2, 1))
    s.grad([G(0.14, 0.70, 7.2), G(0.66, 0.70, 7.2), G(0.66, 0.82, 7.2), G(0.14, 0.82, 7.2)], hx('c8a458'), hx('8a6c2c'), (1, 0.3))
    s.grad([G(0.14, 0.82, 7.2), G(0.66, 0.82, 7.2), G(0.66, 0.82, 6.0), G(0.14, 0.82, 6.0)], hx('e8c878'), hx('9a7a34'), (0, 1))
    for i in range(4): s.line([G(0.18 + 0.14 * i, 0.82, 6.0), G(0.18 + 0.14 * i, 0.82, 0)], hx('f4eedc'), 4)
    # 옥상 금색 간판판(WESTERN LIFE)
    s.grad([G(0.24, 0.50, 25), G(0.68, 0.50, 25), G(0.68, 0.50, 30), G(0.24, 0.50, 30)], hx('2a2218'), hx('120e08'), (0.2, 1))
    s.line([G(0.24, 0.50, 30), G(0.68, 0.50, 30)], hx('e6c468'), 4); s.line([G(0.24, 0.50, 25), G(0.68, 0.50, 25)], hx('e6c468'), 3)
    fw = G(0.66, 0.50)[0] - G(0.26, 0.50)[0]
    s.text_face('WESTERN LIFE', (G(0.26, 0.50)[0] + 4, G(0.26, 0.50, 29.0)[1] + 1), 0, 0, fw * 0.92 / 590.0, hx('f4d478'), size=56, shear=0.5)
    s.tree(0.82, 0.80, 0.8); s.tree(0.06, 0.72, 0.7)
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

# ============================ b21 익산역 (KTX): 남색 유리 박스 + 흰 "익산역" + 파란 캐노피 + 마룡이 동상 ============================
def station(H=48):
    s = Sc(H, 6); G = s.G; rs = random.Random(9)
    s.ground()
    # 회색 벽돌 광장 줄눈 + 잔디 화단(마룡이 동상 자리)
    for i in range(1, 16): s.line([G(i / 16, 0.50), G(i / 16, 1)], hx('b4b4d4', 120), 2)
    s.grad([G(0.04, 0.66), G(0.50, 0.66), G(0.50, 0.96), G(0.04, 0.96)], hx('8cc04a'), hx('5a9a30'), (0.4, 1), 4)
    s.shadow([G(0.08, 0.56), G(0.94, 0.56), G(1.0, 0.64), G(0.14, 0.64)], 100, 9)
    # 왼쪽 흰 날개(패널) / 오른쪽 파란 날개(KORAIL 띠)
    s.box(0.04, 0.30, 0.28, 0.52, 11, (hx('f6f6fa'), hx('d2d4e0')), (hx('d4d6e4'), hx('a8abc0')), (hx('eceef8'), hx('c8cade')))
    for k in range(1, 9): s.line([G(0.04 + 0.26 * k / 9, 0.52, 0), G(0.04 + 0.26 * k / 9, 0.52, 11)], hx('b8bacc', 100), 2)
    s.box(0.68, 0.96, 0.30, 0.54, 8, (hx('4a78d4'), hx('2a4ea4')), (hx('2e56b0'), hx('1a3a84')), (hx('7a9ae0'), hx('4a6cc4')))
    s.grad([G(0.72, 0.54, 1.2), G(0.94, 0.54, 1.2), G(0.94, 0.54, 3.6), G(0.72, 0.54, 3.6)], hx('a0d8c8'), hx('3a7a78'), (0.2, 1))
    s.line([G(0.70, 0.54, 6.6), G(0.96, 0.54, 6.6)], hx('f4f4fc'), 5)
    s.text_face('KORAIL', (G(0.74, 0.54)[0], G(0.74, 0.54, 6.4)[1] + 2), 0, 0, 0.12, hx('0a3a9a'), size=40, shear=0.5)
    # 중앙 남색 유리 박스 + 흰 큰 글자 "익산역" + Iksan Station
    s.box(0.30, 0.68, 0.22, 0.50, 14, (hx('2c4a82'), hx('0c1a3c')), (hx('1c3262'), hx('08122c')), (hx('3a5a92'), hx('1c3262')))
    for i in range(8):
        for j in range(3):
            a0 = 0.31 + 0.045 * i; s.grad([G(a0, 0.50, 1.0 + j * 4.2), G(a0 + 0.04, 0.50, 1.0 + j * 4.2), G(a0 + 0.04, 0.50, 4.6 + j * 4.2), G(a0, 0.50, 4.6 + j * 4.2)], hx('4a76c4'), hx('142a5a'), (0.2, 1))
    s.line([G(0.30, 0.50, 5.8), G(0.68, 0.50, 5.8)], hx('f4f4fc'), 3)
    fw = G(0.66, 0.50)[0] - G(0.32, 0.50)[0]
    s.text_face('익산역', (G(0.34, 0.50)[0], G(0.34, 0.50, 13.2)[1] + 1), 0, 0, fw * 0.58 / 120.0, hx('ffffff'), font=FONT_K, size=64, bold_outline=hx('ffffff'), shear=0.5)
    s.text_face('Iksan Station', (G(0.34, 0.50)[0], G(0.34, 0.50, 6.6)[1] + 1), 0, 0, fw * 0.52 / 420.0, hx('f0f4ff'), size=44, shear=0.5)
    s.grad([G(0.58, 0.50, 11.6), G(0.67, 0.50, 11.6), G(0.67, 0.50, 13.6), G(0.58, 0.50, 13.6)], hx('e8242c'), hx('a0101a'), (0.2, 1))             # KTX 표지
    s.text_face('KTX', (G(0.585, 0.50)[0], G(0.585, 0.50, 13.3)[1] + 1), 0, 0, fw * 0.16 / 100.0, hx('ffffff'), size=50, shear=0.5)
    # 파란 철골 캐노피 + 흰 원형 기둥
    s.grad([G(0.24, 0.50, 6.6), G(0.74, 0.50, 6.6), G(0.74, 0.66, 6.6), G(0.24, 0.66, 6.6)], hx('3c8cec'), hx('1a5cb8'), (1, 0.3))
    for k in range(1, 7): s.line([G(0.24 + 0.5 * k / 7, 0.50, 6.6), G(0.24 + 0.5 * k / 7, 0.66, 6.6)], hx('e8f0ff', 170), 2)
    s.grad([G(0.24, 0.66, 6.6), G(0.74, 0.66, 6.6), G(0.74, 0.66, 5.9), G(0.24, 0.66, 5.9)], hx('f4f4fc'), hx('b4b8cc'), (0, 1))
    for a in (0.27, 0.71): s.line([G(a, 0.64, 0), G(a - 0.03, 0.54, 6.4)], hx('f0f0f8'), 8); s.line([G(a - 0.004, 0.64, 0), G(a - 0.034, 0.54, 6.4)], hx('ffffff'), 3)
    s.grad([G(0.40, 0.50, 0), G(0.58, 0.50, 0), G(0.58, 0.50, 5.2), G(0.40, 0.50, 5.2)], hx('1a2a48'), hx('3a4a70'), (0, 1))
    # 마룡이 동상(청록 용 마스코트): 받침 + 몸 + 흰 배 + 하늘색 날개
    x, y = G(0.22, 0.80); d = ImageDraw.Draw(s.im)
    d.polygon([(x - 18, y), (x + 18, y), (x + 18, y - 5), (x - 18, y - 5)], fill=hx('6a4a3a'))
    d.ellipse([x - 12, y - 30, x + 12, y - 5], fill=hx('2aa8b8')); d.ellipse([x - 7, y - 26, x + 5, y - 9], fill=hx('f0f6f8'))
    d.ellipse([x - 10, y - 40, x + 10, y - 22], fill=hx('2aa8b8')); d.ellipse([x - 7, y - 33, x + 1, y - 26], fill=hx('f0f6f8')); d.ellipse([x - 3, y - 36, x - 1, y - 34], fill=hx('10242c'))
    d.polygon([(x + 10, y - 26), (x + 26, y - 32), (x + 22, y - 18)], fill=hx('9ad4ee')); d.polygon([(x - 10, y - 14), (x - 26, y - 10), (x - 12, y - 6)], fill=hx('1a7a9a'))
    d.polygon([(x - 8, y - 40), (x - 12, y - 48), (x - 3, y - 41)], fill=hx('1a8a9a')); d.polygon([(x + 5, y - 40), (x + 10, y - 47), (x + 9, y - 38)], fill=hx('1a8a9a'))
    s.tree(0.02, 0.60, 0.7); s.tree(0.96, 0.70, 0.7)
    return s.finish()

# ============================ b21 춘포역 (옛 역사: 낮은 목조 건물 + 회색 기와 지붕) ============================
def chunpo():
    s = Sc(48, 13); G = s.G; s.ground(); rs = random.Random(10)
    # 선로 2줄 + 침목 + 승강장
    s.grad([G(0.02, 0.02), G(0.98, 0.02), G(0.98, 0.14), G(0.02, 0.14)], hx('7a7468'), hx('5a5448'), (0, 1))
    for k in range(0, 40): s.line([G(0.04 + 0.024 * k, 0.03), G(0.04 + 0.024 * k, 0.13)], hx('4a3a2a'), 3)
    for b in (0.05, 0.11): s.line([G(0.02, b), G(0.98, b)], hx('c8ccd6'), 3)
    s.grad([G(0.02, 0.14), G(0.98, 0.14), G(0.98, 0.24), G(0.02, 0.24)], hx('d8d2c0'), hx('b8b2a0'), (0, 1))
    s.shadow([G(0.14, 0.58), G(0.86, 0.58), G(0.94, 0.66), G(0.22, 0.68)], 95, 8)
    A0, A1, B0, B1, HW = 0.14, 0.86, 0.28, 0.58, 6.2
    # 벽: 흰 널판 + 짙은 갈색 문틀/기둥
    s.box(A0, A1, B0, B1, HW, (hx('f6f1e0'), hx('d4ceb8')), (hx('d4ceb8'), hx('a8a28c')), (hx('9a948a'), hx('7a746a')))
    for k in range(1, 20): s.line([G(A0 + (A1 - A0) * k / 20, B1, 0), G(A0 + (A1 - A0) * k / 20, B1, HW)], hx('b8b29c', 110), 2)
    for k in range(0, 8): s.line([G(A0 + (A1 - A0) * k / 7, B1, 0), G(A0 + (A1 - A0) * k / 7, B1, HW)], hx('6a4a32'), 5)
    for k in range(0, 3): s.line([G(A0, B1, 0.2 + k * 2.9), G(A1, B1, 0.2 + k * 2.9)], hx('6a4a32'), 3)
    for i in range(5):                                                                                       # 창 + 출입문
        a = A0 + 0.06 + 0.13 * i
        if i == 2: s.grad([G(a, B1, 0), G(a + 0.07, B1, 0), G(a + 0.07, B1, 4.6), G(a, B1, 4.6)], hx('3a281a'), hx('1a1008'), (0, 1))
        else: s.grad([G(a, B1, 1.6), G(a + 0.07, B1, 1.6), G(a + 0.07, B1, 4.4), G(a, B1, 4.4)], hx('a8c4d8'), hx('4a6078'), (0.2, 1))
    # 회색 기와 팔작지붕(앞 경사 + 옆 경사) + 용마루 + 처마 끝 기와 줄
    RZ = 5.2; ov = 0.04
    s.grad([G(A0 - ov, B1 + ov, HW), G(A1 + ov, B1 + ov, HW), G(A1 - 0.06, (B0 + B1) / 2, HW + RZ), G(A0 + 0.06, (B0 + B1) / 2, HW + RZ)], hx('8a8c98'), hx('4e505e'), (0.2, 1), 4)
    s.grad([G(A1 + ov, B0 - ov, HW), G(A1 + ov, B1 + ov, HW), G(A1 - 0.06, (B0 + B1) / 2, HW + RZ)], hx('6a6c7a'), hx('3a3c48'), (0.2, 1), 4)
    for k in range(1, 7):
        t = k / 7; bb = (B1 + ov) + ((B0 + B1) / 2 - (B1 + ov)) * t; z = HW + RZ * t; aL = A0 - ov + (0.10 + ov) * t; aR = A1 + ov - (0.10 + ov) * t
        s.line([G(aL, bb, z), G(aR, bb, z)], hx('2e303c', 170), 3)
    s.line([G(A0 + 0.06, (B0 + B1) / 2, HW + RZ), G(A1 - 0.06, (B0 + B1) / 2, HW + RZ)], hx('b0b2c0'), 5)
    s.line([G(A0 - ov, B1 + ov, HW), G(A1 + ov, B1 + ov, HW)], hx('c8cad6'), 4)
    # 가운데 박공 현관(작은 맞배지붕) + 간판
    s.box(0.40, 0.60, 0.58, 0.66, 5.0, (hx('f6f1e0'), hx('d4ceb8')), (hx('d4ceb8'), hx('a8a28c')), (hx('8a8c98'), hx('5a5c6a')))
    s.grad([G(0.38, 0.66, 5.0), G(0.62, 0.66, 5.0), G(0.50, 0.62, 8.2)], hx('8a8c98'), hx('4e505e'), (0, 1))
    s.grad([G(0.44, 0.66, 0.0), G(0.56, 0.66, 0.0), G(0.56, 0.66, 3.8), G(0.44, 0.66, 3.8)], hx('3a281a'), hx('1a1008'), (0, 1))
    s.grad([G(0.42, 0.66, 3.9), G(0.58, 0.66, 3.9), G(0.58, 0.66, 5.0), G(0.42, 0.66, 5.0)], hx('1e3a68'), hx('0e2244'), (0, 1))
    s.text_face('춘포역', (G(0.425, 0.66)[0], G(0.425, 0.66, 4.9)[1] + 1), 0, 0, 0.09, hx('ffffff'), font=FONT_K, size=36, shear=0.5)
    # 벤치와 가로등 + 느티나무
    for a in (0.24, 0.62): s.line([G(a, 0.22, 1.6), G(a + 0.10, 0.22, 1.6)], hx('6a4a32'), 5)
    s.tree(0.06, 0.78, 0.9); s.tree(0.94, 0.74, 0.8)
    return s.finish()


# ============================ b44 하림 (익산 푸드폴리스: 식품 공장 + 사일로) ============================
def harim():
    s = Sc(61, 7); G = s.G; s.ground()
    s.shadow([G(0.10, 0.56), G(0.92, 0.56), G(1.02, 0.66), G(0.20, 0.68)], 105, 10)
    # 긴 흰색 공장동 + 파란 지붕 줄 + 하림 파랑 간판
    s.box(0.08, 0.84, 0.14, 0.56, 13, (hx('fafbff'), hx('ced4e6')), (hx('d8deee'), hx('a4abc4')), (hx('2a64c0'), hx('1a4290')))
    for k in range(1, 14): s.line([G(0.08 + 0.76 * k / 14, 0.14, 13), G(0.08 + 0.76 * k / 14, 0.56, 13)], hx('78a0e8', 170), 2)
    for k in range(1, 12): s.line([G(0.08 + 0.76 * k / 12, 0.56, 0), G(0.08 + 0.76 * k / 12, 0.56, 13)], hx('b8c0d8', 90), 2)
    s.grad([G(0.08, 0.56, 6.6), G(0.84, 0.56, 6.6), G(0.84, 0.56, 13), G(0.08, 0.56, 13)], hx('1a64c8'), hx('0a3a88'), (0.2, 1))
    s.line([G(0.08, 0.56, 6.6), G(0.84, 0.56, 6.6)], hx('5ab43a'), 8)                                                                                # 초록 띠(하림 포인트색)
    fw = G(0.82, 0.56)[0] - G(0.10, 0.56)[0]
    s.text_face('HARIM', (G(0.13, 0.56)[0], G(0.13, 0.56, 12.2)[1] + 1), 0, 0, fw * 0.62 / 290.0, hx('ffffff'), size=64, bold_outline=hx('0a3a88'), shear=0.5)
    s.text_face('FOOD POLIS', (G(0.60, 0.56)[0], G(0.60, 0.56, 9.4)[1] + 1), 0, 0, fw * 0.28 / 330.0, hx('d8f0c8'), size=44, shear=0.5)
    for k in range(5): s.grad([G(0.12 + 0.14 * k, 0.56, 0.6), G(0.20 + 0.14 * k, 0.56, 0.6), G(0.20 + 0.14 * k, 0.56, 5.0), G(0.12 + 0.14 * k, 0.56, 5.0)], hx('a8c8e8'), hx('3a5a88'), (0.2, 1))    # 상차장 문
    # 사일로 2개(원통): 흰/파랑 + 사다리
    def silo(a, b, r, hh):
        x, y = G(a, b); rr = r * 47 * S
        d = ImageDraw.Draw(s.im); xs0, xs1 = x - rr, x + rr; ytop = y - hh * S
        for xx in range(int(xs0), int(xs1)):
            t = max(0.0, (xx - xs0) / (xs1 - xs0)); c = lerp(hx('f6f8ff'), hx('8a92b0'), t ** 1.3); d.line([(xx, y), (xx, ytop)], fill=c, width=1)
        d.ellipse([xs0, y - rr * 0.5, xs1, y + rr * 0.5], fill=hx('9aa2bc')); d.ellipse([xs0, ytop - rr * 0.5, xs1, ytop + rr * 0.5], fill=hx('e8ecfa')); d.ellipse([xs0 + 6, ytop - rr * 0.3, xs1 - 6, ytop + rr * 0.3], fill=hx('1a64c8'))
        d.line([(xs1 - 4, y), (xs1 - 4, ytop)], fill=hx('5a6280'), width=3)
    silo(0.74, 0.74, 0.060, 20); silo(0.88, 0.62, 0.052, 17)
    # 굴뚝 + 하림 트럭(흰 탑차 + 파랑 줄)
    x, y = G(0.14, 0.18, 13); d = ImageDraw.Draw(s.im); d.rectangle([x - 5, y - 40, x + 5, y], fill=hx('c8cee0')); d.rectangle([x - 5, y - 40, x - 1, y], fill=hx('f0f2fa')); d.rectangle([x - 6, y - 44, x + 6, y - 40], fill=hx('d8363a'))
    a, b = 0.22, 0.80
    s.grad([G(a, b, 0.4), G(a + 0.15, b, 0.4), G(a + 0.15, b, 5.6), G(a, b, 5.6)], hx('fafbff'), hx('c4cadc'), (0.3, 1)); s.line([G(a, b, 3.0), G(a + 0.15, b, 3.0)], hx('1a64c8'), 6)
    s.grad([G(a + 0.15, b - 0.04, 0.4), G(a + 0.15, b, 0.4), G(a + 0.15, b, 5.6), G(a + 0.15, b - 0.04, 5.6)], hx('a4abc4'), hx('6e7694'), (0.3, 1))
    s.grad([G(a + 0.17, b, 0.4), G(a + 0.22, b, 0.4), G(a + 0.22, b, 3.4), G(a + 0.17, b, 3.4)], hx('4a7ae0'), hx('1a3a88'), (0.3, 1))
    s.tree(0.04, 0.84, 0.8); s.tree(0.96, 0.84, 0.7)
    return s.finish()

if __name__ == '__main__':
    items = {'b19': cinema, 'b3': costco, 'b4': hotel, 'b15': tesla, 'b21': chunpo, 'b22': lambda: station(67), 'b44': harim}
    sheet = Image.new('RGBA', (7 * 96 * 4 + 56, 80 * 4), (150, 70, 160, 255))
    for k, (n, fn) in enumerate(items.items()):
        im = fn(); im.save(f'{out}/{n}.png'); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im)
        sheet.paste(bg.resize((im.width * 4, im.height * 4), Image.NEAREST), (k * (96 * 4 + 8), 0))
        if len(sys.argv) > 1: bg.resize((im.width * 8, im.height * 8), Image.NEAREST).save(sys.argv[1].replace('.png', f'_{n}.png'))
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('건물 5종 →', out)
