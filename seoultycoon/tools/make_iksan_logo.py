"""타이틀 로고(0.m, 124x69) 를 'IKSAN TYCOON 2' 로 새로 그린다 → st2android/mod/0.m (게임은 st2mod 를 먼저 읽는다).
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

iksan = shrink(text_mask('IKSAN', (4, 9, 79, 37), -1))
tycoon = shrink(text_mask('TYCOON', (16, 42, 82, 58), -1))
two = shrink(text_mask('2', (78, 4, 119, 62)))
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
