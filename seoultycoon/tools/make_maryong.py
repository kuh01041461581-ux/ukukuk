"""익산 마스코트 '마룡이' 도트 초상화 생성 → st2android/mod/f7.d(만능비서 초상 62x64).
원본 사진은 work/maryong_src.jpg (개인용, 커밋하지 않음). 없으면 건너뛴다.
사용: python3 -I tools/make_maryong.py [미리보기.png]"""
import os, sys
from PIL import Image, ImageFilter, ImageChops
import numpy as np
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
srcp = root + '/work/maryong_src.jpg'
if not os.path.exists(srcp): print('마룡이 원본 없음 → 건너뜀'); sys.exit(0)
im = Image.open(srcp).convert('RGB'); a = np.asarray(im).astype(int)
H, Wd = a.shape[:2]
# 배경(178 회색) 제거: 회색 무채색이고 밝기가 배경~그림자 범위(≈130~185)인 픽셀 + 왼쪽 아래 검은 동그라미
r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
sat = a.max(2) - a.min(2); lum = a.mean(2)
yy, xx = np.arange(H)[:, None], np.arange(Wd)[None, :]
bg = (sat < 5) & (np.abs(lum - 178) < 5)                      # 바깥 배경(178 회색)
bg |= (sat < 8) & (np.abs(lum - 144) < 8) & (yy > 545)         # 발밑 그림자
bg |= (xx < 80) & (yy > 545)                                   # 왼쪽 아래 동그라미(H)
# 바깥에서 이어진 배경만 (캐릭터 안쪽 흰/회색은 보존)
from collections import deque
seen = np.zeros_like(bg); dq = deque()
for x in range(Wd):
    for y in (0, H - 1):
        if bg[y, x] and not seen[y, x]: seen[y, x] = True; dq.append((y, x))
for y in range(H):
    for x in (0, Wd - 1):
        if bg[y, x] and not seen[y, x]: seen[y, x] = True; dq.append((y, x))
while dq:
    y, x = dq.popleft()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        Y, X = y + dy, x + dx
        if 0 <= Y < H and 0 <= X < Wd and bg[Y, X] and not seen[Y, X]: seen[Y, X] = True; dq.append((Y, X))
alpha = Image.fromarray(((~seen) * 255).astype('uint8'))
alpha = alpha.filter(ImageFilter.MedianFilter(5))
lit = Image.fromarray(np.clip(a * 1.32, 0, 255).astype('uint8'))      # 어두운 오버레이 복원
rgba = lit.convert('RGBA'); rgba.putalpha(alpha)
full = rgba.copy()                                              # 머리 아이콘용(원본 좌표)
bbox = alpha.point(lambda v: 255 if v > 128 else 0).getbbox(); rgba = rgba.crop(bbox)

def pixelate(img, size, colors=22, outline=(18, 52, 58, 255)):
    w, h = size; iw, ih = img.size; s = min((w - 2) / iw, (h - 2) / ih)
    nw, nh = max(1, round(iw * s)), max(1, round(ih * s))
    sm = img.resize((nw, nh), Image.LANCZOS)
    al = sm.getchannel('A').point(lambda v: 255 if v > 110 else 0)
    rgb = sm.convert('RGB'); q = rgb.quantize(colors=colors, method=Image.MEDIANCUT, dither=Image.NONE).convert('RGB')
    cv = Image.new('RGBA', (w, h), (0, 0, 0, 0)); ox, oy = (w - nw) // 2, h - nh - 1
    body = q.convert('RGBA'); body.putalpha(al)
    # 바깥 1px 외곽선(도트 느낌)
    pad = Image.new('L', (w, h), 0); pad.paste(al, (ox, oy))
    ring = ImageChops.subtract(pad.filter(ImageFilter.MaxFilter(3)), pad)
    ol = Image.new('RGBA', (w, h), outline); ol.putalpha(ring.point(lambda v: 255 if v > 0 else 0))
    cv.alpha_composite(ol); cv.alpha_composite(body, (ox, oy)); return cv
upper = rgba.crop((0, 0, rgba.width, int(rgba.height * 0.82)))
big = pixelate(upper, (62, 64), colors=28)
# 얼굴 아이콘: 머리(위 60%)만 잘라 24x26
head = full.crop((105, 30, 465, 360))                           # 더듬이~볼(날개 제외)
small = pixelate(head, (24, 26), colors=16)
md = root + '/st2android/mod'; os.makedirs(md, exist_ok=True)
big.save(md + '/f7.d', format='PNG')      # 만능비서(머든지) 초상 자리 = f7 (비서 순서와 초상 순서가 같다는 추정)
if len(sys.argv) > 1:
    bgc = Image.new('RGBA', (66 * 2 + 24 * 2 + 30, 63 * 1), (185, 30, 100, 255)); bgc.alpha_composite(big, (4, 0)); bgc.alpha_composite(small, (80, 10))
    bgc.resize((bgc.width * 5, bgc.height * 5), Image.NEAREST).save(sys.argv[1])
print('마룡이 → f7.d')
