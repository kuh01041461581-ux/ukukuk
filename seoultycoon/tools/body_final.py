"""여자 시장 상체: 사용자 제공 그림(work/gpt/body_c.png, 조각 비율 2.8:1 로 만든 목·어깨·옷 그림)을 상체 조각(56x20, HD 112x40)에 통째로 맞춘다.
피부색은 얼굴에, 머리카락 색은 바탕 얼굴의 머리색에 맞춘다. 환경변수 BODY=c 일 때만 동작(없으면 건너뜀).  → st2android/mod/fb1.d"""
import os, io, sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); mod = root + '/st2android/mod'
which = os.environ.get('BODY', 'c')                                  # 기본: c
p = f'{root}/work/gpt/body_{which}.png'
if not os.path.exists(p): print('body 그림 없음 — 건너뜀'); sys.exit(0)
def ld(nm):
    r = open(f'{mod}/{nm}.d', 'rb').read(); k = r.find(b'IEND') + 8; return np.asarray(Image.open(io.BytesIO(r[k:])).convert('RGBA')).astype(np.float32)
sk2 = lambda x: (x[..., 3] > 200) & (x[..., 0] > 170) & (x[..., 1] > 100) & (x[..., 0] > x[..., 2] + 35) & (x[..., 2] < x[..., 1] - 5)
im = Image.open(p).convert('RGB'); a = np.asarray(im).astype(int); bg = np.median(a[:6, :6].reshape(-1, 3), axis=0)
m = np.abs(a - bg).sum(2) > 50; m = ndi.binary_closing(m, iterations=1); lab, n = ndi.label(m); sizes = ndi.sum(m, lab, range(1, n + 1)); m = ndi.binary_fill_holes(lab == 1 + int(np.argmax(sizes)))
o = im.convert('RGBA'); o.putalpha(Image.fromarray((m * 255).astype(np.uint8)))
arr = np.asarray(o).copy(); arr[(arr[..., 2].astype(int) > arr[..., 0].astype(int) + 30) & (arr[..., 3] > 0) & (arr[..., 0] < 200), 3] = 0; o = Image.fromarray(arr, 'RGBA')
W, H = 112, 40
SCX = float(os.environ.get('SCX', '1.04')); NECK_SRC = float(os.environ.get('NECK_SRC', '58')); NECK_DST = float(os.environ.get('NECK_DST', '64'))     # 그림의 목 중심(56)을 얼굴 목 중심(63)에 맞추며 가로를 줄여 오른쪽이 안 잘리게
CROP = float(os.environ.get('CROP_TOP', '0.12')); o = o.crop((0, int(o.height * CROP), o.width, o.height))      # 위쪽 목 부분을 잘라 내고(어깨 윗선이 턱 밑에 닿게) 나머지를 조각 높이에 맞춰 키운다
sm = o.resize((int(round(W * SCX)), H), Image.LANCZOS); dx = int(round(NECK_DST - NECK_SRC * SCX)); dx = int(round(NECK_DST - NECK_SRC * SCX)); dx = min(dx, 0 + max(0, W - sm.width)) if sm.width <= W else dx
cv = Image.new('RGBA', (W, H), (0, 0, 0, 0)); cv.alpha_composite(sm, (dx, 0)); fa = np.asarray(cv).astype(np.float32)
lum = fa[..., :3] @ np.array([.3, .59, .11])
skin = sk2(fa)
fd = ld('fd1'); fsk = np.concatenate([ld(n)[sk2(ld(n))][:, :3] for n in ('ff3', 'ff4', 'ff9', 'fd1')]).mean(0)
hm = (fd[..., 3] > 250) & ~sk2(fd) & ((fd[..., :3] @ np.array([.3, .59, .11])) < 150); fhair = fd[hm][:, :3].mean(0)
hair = (fa[..., 3] > 200) & ~skin & (fa[..., 0] > fa[..., 2]) & (lum < 140) & (np.arange(H)[:, None] < 22)
fa[skin, :3] += (fsk - fa[skin][:, :3].mean(0)) * 0.6 * (1 - np.clip((lum[skin] - 225) / 25.0, 0, 1))[:, None]
if hair.any(): fa[hair, :3] += (fhair - fa[hair][:, :3].mean(0)) * 0.9
cutm = (fa[..., 3] > 0) & ~skin & (lum < 150) & (np.arange(H)[:, None] < int(os.environ.get('HAIRCUT', '0')))                   # 목 위쪽 짧은 머리 끝자락은 지운다(얼굴 머리가 대신 덮는다)
fa[cutm, 3] = 0
pass            # 목 맨 윗줄은 피부만 안쪽 줄 복사(밝은 가로선 방지)
hd = Image.fromarray(np.clip(fa, 0, 255).astype(np.uint8), 'RGBA')
raw = open(f'{mod}/fb1.d', 'rb').read(); i = raw.find(b'IEND') + 8
lo = hd.resize((56, 20), Image.LANCZOS); aa = np.asarray(lo).copy(); aa[..., 3] = (aa[..., 3] > 127) * 255
b1 = io.BytesIO(); Image.fromarray(aa, 'RGBA').save(b1, format='PNG'); b2 = io.BytesIO(); hd.save(b2, format='PNG', optimize=True)
open(f'{mod}/fb1.d', 'wb').write(b1.getvalue() + b2.getvalue()); print('상체 적용', which)
