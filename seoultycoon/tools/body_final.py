"""여자 시장 상체: 사용자 제공 큰 그림(work/gpt/body_X.png, 목이 가운데 위에 있는 상반신)을 상체 조각(56x20, HD 112x40)에 맞춰 넣는다.
조각 폭(112)에 맞춰 줄이고 위에서부터 40줄만 쓴다(목~어깨~가슴 윗부분). 환경변수 BODY=a|b 로 고른다. work/gpt/body_X.png 가 없으면 건너뜀."""
import os, io, sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); mod = root + '/st2android/mod'
which = os.environ.get('BODY', ''); p = f'{root}/work/gpt/body_{which}.png'
if not which or not os.path.exists(p): print('body 그림 없음 — 건너뜀'); sys.exit(0)
im = Image.open(p).convert('RGB'); a = np.asarray(im).astype(int); bg = np.median(a[:6, :6].reshape(-1, 3), axis=0)
m = np.abs(a - bg).sum(2) > 50; m = ndi.binary_closing(m, iterations=1); lab, n = ndi.label(m); sizes = ndi.sum(m, lab, range(1, n + 1)); m = ndi.binary_fill_holes(lab == 1 + int(np.argmax(sizes)))
al = Image.fromarray((m * 255).astype(np.uint8)); o = im.convert('RGBA'); o.putalpha(al)
arr = np.asarray(o).copy(); arr[(arr[..., 2].astype(int) > arr[..., 0].astype(int) + 30) & (arr[..., 3] > 0), 3] = 0; o = Image.fromarray(arr, 'RGBA'); o = o.crop(o.getbbox())
W, H = 112, 40; f = float(os.environ.get('SCALE', '0.074')) * (1536.0 / im.width) if False else float(os.environ.get('SCALE', '0.074')) * 1536.0 / o.width * (o.width / 1536.0) * (1.0)
f = float(os.environ.get('SCALE', '0.074')) * (im.width / o.width) * (o.width / im.width)       # 원본 그림 폭 기준 배율
big = o.resize((max(1, int(o.width * f)), max(1, int(o.height * f))), Image.LANCZOS)
nz = np.asarray(big)[:6, :, 3] > 0; ncx = float(np.where(nz.any(0))[0].mean()); NECK_X = float(os.environ.get('NECKX', '58')); TOP = int(os.environ.get('TOPOFF', '0'))
hd = Image.new('RGBA', (W, H), (0, 0, 0, 0)); hd.alpha_composite(big.crop((0, TOP, big.width, TOP + H)) if False else big, (int(round(NECK_X - ncx)), -TOP)) if False else None
canvas = Image.new('RGBA', (W + 400, H + 400), (0, 0, 0, 0)); canvas.alpha_composite(big, (200 + int(round(NECK_X - ncx)), 200 - TOP)); hd = canvas.crop((200, 200, 200 + W, 200 + H))
raw = open(f'{mod}/fb1.d', 'rb').read(); i = raw.find(b'IEND') + 8; first = raw[:i]
lo = Image.open(io.BytesIO(first)).convert('RGBA'); lo2 = hd.resize((56, 20), Image.LANCZOS); aa = np.asarray(lo2).copy(); aa[..., 3] = (aa[..., 3] > 127) * 255; lo2 = Image.fromarray(aa, 'RGBA')
b1 = io.BytesIO(); lo2.save(b1, format='PNG'); b2 = io.BytesIO(); hd.save(b2, format='PNG', optimize=True); open(f'{mod}/fb1.d', 'wb').write(b1.getvalue() + b2.getvalue()); print('상체 적용', which, big.size)
