"""사용자가 GPT 로 다시 그려 정리해 준 시장 얼굴(표정 18장)·초상·머리 그림(work/gpt/*.png)을 게임의 2배 해상도 층으로 반영한다.
원본 파일(.d, PNG) 뒤에 2배 크기 PNG 를 덧붙인다(GfxLib.decode 가 PNG 뒤의 PNG 를 고해상도 층으로 읽는다). 원본 그림의 '불투명 영역 상자'에 맞춰 배치한다.
work/gpt 가 없으면 아무것도 하지 않는다.   → st2android/mod/{ff,f,fa,fb,fh}*.d"""
import os, io, sys
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); gp = root + '/work/gpt'; src = root + '/work/st2'; mod = root + '/st2android/mod'
if not os.path.exists(gp + '/faces18.png'): print('gpt 이미지 없음 — 건너뜀'); sys.exit(0)
K = 2

def cut(im, box, bgc, thr=70):
    """box 안에서 배경색과 다른 덩어리만 남긴 RGBA(구멍은 채우고 가장자리는 부드럽게)"""
    x0, y0, x1, y1 = box; a = np.asarray(im.crop(box).convert('RGB')).astype(int)
    m = np.abs(a - bgc).sum(2) > thr; m = ndi.binary_closing(m, iterations=1)
    lab, n = ndi.label(m)
    if n == 0: return None
    area = ndi.sum(m, lab, range(1, n + 1)); keep = [i + 1 for i, s in enumerate(area) if s >= 0.04 * area.max()]      # 라벨 글자 같은 작은 조각은 버린다
    m = np.isin(lab, keep); m = ndi.binary_fill_holes(m)
    al = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)).point(lambda v: 255 if v > 150 else (0 if v < 60 else int((v - 60) * 255 / 90)))
    out = im.crop(box).convert('RGBA'); out.putalpha(al); bb = out.getbbox(); return out.crop(bb) if bb else None

def apply(name, spr, fmt='.d'):
    p = f'{src}/{name}{fmt}'
    if spr is None or not os.path.exists(p): return False
    raw = open(p, 'rb').read(); base = Image.open(io.BytesIO(raw)).convert('RGBA'); w, h = base.size
    ob = base.getchannel('A').getbbox() or (0, 0, w, h)
    big = spr.resize(((ob[2] - ob[0]) * K, (ob[3] - ob[1]) * K), Image.LANCZOS)
    hd = Image.new('RGBA', (w * K, h * K), (0, 0, 0, 0)); hd.alpha_composite(big, (ob[0] * K, ob[1] * K))
    i = raw.find(b'IEND'); first = raw[:i + 8]; b = io.BytesIO(); hd.save(b, format='PNG', optimize=True)
    open(f'{mod}/{name}{fmt}', 'wb').write(first + b.getvalue()); return True

n = 0
# 1) 표정 18장 (ff0~ff17): 9열 x 2행
F = Image.open(gp + '/faces18.png').convert('RGB'); bgF = np.median(np.asarray(F)[:8, :8].reshape(-1, 3), axis=0)
xs = [2, 248, 490, 729, 976, 1219, 1467, 1713, 1943]; ws = [234, 230, 227, 235, 231, 236, 238, 222, 193]
for r, (y0, hh) in enumerate(((104, 238), (461, 241))):
    for c in range(9):
        n += apply(f'ff{r * 9 + c}', cut(F, (max(0, xs[c] - 3), y0, xs[c] + ws[c] + 3, y0 + hh), bgF, 60))
# 2) 초상·상체·장식(sheet.png): 행별로 서로 붙은 인물을 가장 좁은 열에서 나눈다
S = Image.open(gp + '/sheet.png').convert('RGB'); bgS = np.median(np.asarray(S)[:8, :8].reshape(-1, 3), axis=0)
def label_free(box):                                           # 칸 왼쪽 위의 번호 글자 자리를 배경색으로 덮는다
    x0, y0 = box[0], box[1]; ImageDrawRect(x0, y0)
def ImageDrawRect(x0, y0): pass
rows = [(14, 226, [0, 194, 396, 607, 799, 992, 1160, 1358], ['f0', 'f1', 'f2', 'f3', 'f4', 'f5', 'f6']),
        (230, 440, [0, 188, 376, 598, 803, 996, 1168, 1358], ['f7', 'f8', 'f9', 'f10', 'f11', 'f12', 'f13']),
        (442, 640, [0, 194, 390], ['f14', 'f15'])]
for y0, y1, cuts, names in rows:
    for i, nm in enumerate(names):
        S2 = S.copy(); from PIL import ImageDraw; ImageDraw.Draw(S2).rectangle([cuts[i], y0, cuts[i] + 40, y0 + 34], fill=tuple(int(v) for v in bgS))
        n += apply(nm, cut(S2, (cuts[i], y0, cuts[i + 1], y1), bgS, 70))
for nm, box in (('fa0', (515, 442, 597, 495)), ('fa1', (599, 442, 686, 600)), ('fa2', (689, 442, 800, 600)), ('fb0', (804, 458, 991, 536)), ('fb1', (992, 458, 1161, 536)), ('fb2', (1164, 458, 1358, 536))):
    S2 = S.copy(); ImageDraw.Draw(S2).rectangle([box[0], box[1] - 12, box[0] + 40, box[1] + 8], fill=tuple(int(v) for v in bgS)); n += apply(nm, cut(S2, box, bgS, 70))
# 3) 머리 스트라이프: 작은 것 5장(fh0~fh4) + 큰 여자 머리(fh6)
hair = [(444, 805, 543, 862), (546, 805, 646, 862), (648, 805, 747, 862), (749, 805, 847, 862), (849, 805, 946, 862)]
for i, box in enumerate(hair): n += apply(f'fh{i}', cut(S, box, bgS, 70))
n += apply('fh6', cut(S, (944, 806, 1098, 960), bgS, 70))
# 4) 여자 시장 상체(fb1): 아래쪽에 부드러운 음영(곡면 그늘)과 하이라이트를 넣어 앞가슴이 도톰해 보이게 한다(가슴 자체는 그림 밖이라 음영으로만 표현)
from PIL import ImageDraw
raw = open(f'{mod}/fb1.d', 'rb').read(); i = raw.find(b'IEND') + 8; first = raw[:i]; hd = Image.open(io.BytesIO(raw[i:])).convert('RGBA'); w, h = hd.size
sh = Image.new('RGBA', (w * 4, h * 4), (0, 0, 0, 0)); d = ImageDraw.Draw(sh); hl = Image.new('RGBA', (w * 4, h * 4), (0, 0, 0, 0)); e = ImageDraw.Draw(hl)
for cx in (44, 104):                                               # 좌·우 앞판의 둥근 면: 아래쪽 초승달 그늘 + 위쪽 하이라이트
    d.pieslice([(cx - 26) * 4, 4 * 4, (cx + 26) * 4, 46 * 4], 15, 165, fill=(110, 10, 60, 190)); e.ellipse([(cx - 13) * 4, 18 * 4, (cx + 4) * 4, 27 * 4], fill=(255, 205, 230, 130))
sh = sh.filter(ImageFilter.GaussianBlur(10)).resize((w, h), Image.LANCZOS); hl = hl.filter(ImageFilter.GaussianBlur(8)).resize((w, h), Image.LANCZOS)
al = hd.getchannel('A')
for layer in (sh, hl):
    la = layer.getchannel('A'); layer.putalpha(Image.fromarray((np.minimum(np.asarray(la), np.asarray(al))).astype(np.uint8)))      # 옷 밖으로 번지지 않게
hd.alpha_composite(sh); hd.alpha_composite(hl)
b = io.BytesIO(); hd.save(b, format='PNG', optimize=True); open(f'{mod}/fb1.d', 'wb').write(first + b.getvalue())
print('GPT 그림 반영', n, '장')
