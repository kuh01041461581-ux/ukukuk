"""타이틀 로고(0.m, 124x69) 를 'IKSAN TYCOON' 로 새로 그린다 → st2android/mod/0.m (게임은 st2mod 를 먼저 읽는다).
원본과 같은 구성: 흰 이탤릭 글자 + 검은 외곽선 + 빨간 두꺼운 외곽/그림자 + 노란 '2'. 사용: python3 -I tools/make_iksan_logo.py [미리보기.png]"""
import os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
W, H, S = 124, 69, 8
FONT = '/usr/share/fonts/truetype/freefont/FreeSansBoldOblique.ttf'

def text_mask(txt, box, tracking=0):
    """글자를 큰 글꼴로 그려 글자 모양 경계 상자를 잘라 box=(x0,y0,x1,y1)(최종 픽셀)에 맞춰 늘려 붙인다."""
    f = ImageFont.truetype(FONT, 200); t = Image.new('L', (200 * len(txt) * 2 + 300, 500), 0); d = ImageDraw.Draw(t); x = 150
    for ch in txt:
        d.text((x, 40), ch, font=f, fill=255); x += d.textlength(ch, font=f) + tracking * 3
    t = t.crop(t.getbbox())
    x0, y0, x1, y1 = box; t = t.resize(((x1 - x0) * S, (y1 - y0) * S), Image.LANCZOS)
    m = Image.new('L', (W * S, H * S), 0); m.paste(t, (x0 * S, y0 * S)); return m
def shrink(m): return m.resize((W, H), Image.LANCZOS).point(lambda v: 255 if v > 110 else 0)
def grow(m, px): return m.filter(ImageFilter.MaxFilter(px * 2 + 1)) if px > 0 else m
def shift(m, dx, dy): return ImageChops.offset(m, dx, dy)

iksan = shrink(text_mask('IKSAN', (6, 8, 118, 40), -1))
tycoon = shrink(text_mask('TYCOON', (14, 44, 110, 62), -1))
two = Image.new('L', (W, H), 0)                                  # 2 는 뺀다
white = ImageChops.lighter(iksan, tycoon)
allm = ImageChops.lighter(white, two)

img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); px = img.load()
def paint(mask, col):
    mk = mask.load()
    for y in range(H):
        for x in range(W):
            if mk[x, y]: px[x, y] = col
RED, DRED, BLK = (184, 8, 24, 255), (84, 20, 8, 255), (0, 0, 0, 255)
paint(shift(grow(allm, 3), 1, 2), DRED)          # 그림자
paint(grow(allm, 3), RED)                        # 빨간 두꺼운 외곽
paint(grow(allm, 2), (168, 8, 8, 255))
paint(grow(white, 1), BLK)                       # 흰 글자 검은 외곽
paint(grow(two, 1), (111, 15, 9, 255))           # 2 의 어두운 테두리
paint(white, (255, 255, 255, 255))
# 흰 글자 아래쪽 은은한 음영
sh = ImageChops.multiply(white, shift(white, 0, -1).point(lambda v: 255 - v)); paint(sh, (200, 205, 215, 255))
# 노란 2: 위 밝은 노랑, 아래 짙은 노랑
mk = two.load()
for y in range(H):
    for x in range(W):
        if mk[x, y]: px[x, y] = (248, 248, 8, 255) if y < 22 else ((248, 232, 8, 255) if y < 45 else (238, 212, 8, 255))
out = root + '/st2android/mod/0.m'; os.makedirs(os.path.dirname(out), exist_ok=True); img.save(out, format='PNG')
if len(sys.argv) > 1:
    bg = Image.new('RGBA', (W, H), (40, 150, 220, 255)); bg.alpha_composite(img); bg.resize((W * 4, H * 4), Image.NEAREST).save(sys.argv[1])
print('logo →', out)

# 타이틀 화면의 통신사·배급사 표기 지우기 (3.m: PRESS ANY KEY 말풍선 옆에 magic / KTF / n / NEXON MOBILE Corporation 이 한 장에 들어 있다)
_m3 = Image.open(root + '/work/st2/3.m').convert('RGBA'); _p = _m3.load()
for (x0, y0, x1, y1) in ((76, 0, 111, 19), (94, 19, 111, 54), (88, 29, 111, 54), (54, 30, 111, 54), (0, 43, 111, 54)):
    for _y in range(y0, min(y1, _m3.height)):
        for _x in range(x0, min(x1, _m3.width)): _p[_x, _y] = (0, 0, 0, 0)
_m3.save(root + '/st2android/mod/3.m', format='PNG')

# 시작 화면의 제작사 로고 두 장(25.m 넥슨모바일, 24.m 치즈케이크) → 마룡이 전신 / 익산시 로고 를 큰 도트로.
# 사용자가 준 그림(assets/iksan_ref/)을 면적 평균으로 줄이고 색 수를 줄여 도트 느낌을 낸다. 흰 바탕은 투명 처리(시작 화면 바탕이 흰색).
import numpy as _np
def _dot(path, tw, th, colors=48):
    im = Image.open(path).convert('RGBA'); bg = Image.new('RGBA', im.size, (255, 255, 255, 255)); bg.alpha_composite(im); im = bg.convert('RGB')
    a = _np.asarray(im).astype(int); ink = (a.sum(-1) < 735); ys, xs = _np.where(ink); im = im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    k = min(tw / im.width, th / im.height); sm = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
    q = sm.quantize(colors=colors, method=Image.MEDIANCUT, dither=Image.NONE).convert('RGB'); b = _np.asarray(q).astype(int)
    out = _np.zeros((q.height, q.width, 4), _np.uint8); out[..., :3] = b; out[..., 3] = _np.where(b.sum(-1) >= 738, 0, 255)
    return Image.fromarray(out, 'RGBA')
_ref = root + '/assets/iksan_ref'
if os.path.exists(_ref + '/maryong_full.png'): _dot(_ref + '/maryong_full.png', 168, 180, 56).save(root + '/st2android/mod/25.m', format='PNG')
if os.path.exists(_ref + '/iksan_logo.jpg'): _dot(_ref + '/iksan_logo.jpg', 200, 90, 40).save(root + '/st2android/mod/24.m', format='PNG')

# 타이틀 그림(1.m, 176x178)은 화면(240) 가운데에만 그려져 좌우가 파란 띠로 빈다 → 좌우 32칸씩을 가장자리 그림을 뒤집어 이어 붙여 화면 폭을 채운다
_t1 = Image.open(root + '/work/st2/1.m').convert('RGBA'); _wide = Image.new('RGBA', (240, _t1.height), (0, 0, 0, 0)); _wide.alpha_composite(_t1, (32, 0))
_SW = 6                                                             # 가장자리 6칸(하늘·구름·풀밭만 있는 부분)을 뒤집어 가며 이어 붙인다(건물이 복제되지 않게)
_ls = _t1.crop((0, 0, _SW, _t1.height)); _rs = _t1.crop((176 - _SW, 0, 176, _t1.height))
for _k in range(0, 32 // _SW + 1):
    _a = _ls.transpose(Image.FLIP_LEFT_RIGHT) if _k % 2 == 0 else _ls; _wide.alpha_composite(_a, (max(0, 32 - _SW * (_k + 1)), 0)) if 32 - _SW * (_k + 1) >= -_SW + 1 else None
    _b = _rs.transpose(Image.FLIP_LEFT_RIGHT) if _k % 2 == 0 else _rs
    if 208 + _SW * _k < 240: _wide.alpha_composite(_b, (208 + _SW * _k, 0))
_wide.alpha_composite(_t1, (32, 0))
_wide.save(root + '/st2android/mod/1.m', format='PNG')
# 그림 윗변이 배경 파랑과 선으로 갈라져 보이지 않게, 위 14줄을 배경색으로 서서히 섞는다
_BG = (44, 164, 220); _pw = _wide.load()
for _y in range(14):
    _k = 1 - _y / 14.0
    for _x in range(240):
        _r, _g, _b, _a = _pw[_x, _y]
        if _a: _pw[_x, _y] = (int(_r + (_BG[0] - _r) * _k), int(_g + (_BG[1] - _g) * _k), int(_b + (_BG[2] - _b) * _k), 255)
        else: _pw[_x, _y] = _BG + (255,)
_wide.save(root + '/st2android/mod/1.m', format='PNG')
