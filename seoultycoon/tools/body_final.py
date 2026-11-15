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
WB = int(os.environ.get('WIDE', '126'))                                # 상체를 조각 폭(112)보다 넓은 WB 로 그린다(게임 쪽 BIGH 통그림 기능이 조각 밖까지 그린다)
CROP = float(os.environ.get('CROP_TOP', '0.11')); o = o.crop((0, int(o.height * CROP), o.width, o.height))      # 위쪽 목 부분을 잘라 낸다
NECK_DST = float(os.environ.get('NECK_DST', '64')); NECK_FRAC = float(os.environ.get('NECK_FRAC', str(58 / 112.0)))
UP = int(os.environ.get('UP', '0')); H = 40 + UP                         # 위로 UP줄 더 그려 얼굴 아래에 겹치게(상체 머리가 얼굴 머리 밑으로 이어진다)
cv = o.resize((WB, H), Image.LANCZOS); fa = np.asarray(cv).astype(np.float32); BIGX = int(round(NECK_DST - NECK_FRAC * WB))
W = WB
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
raw = open(f'{mod}/fb1.d', 'rb').read(); i = raw.find(b'IEND') + 8; j = raw.find(b'BIGH'); first = raw[:i]
b2 = io.BytesIO(); hd.save(b2, format='PNG', optimize=True)
open(f'{mod}/fb1.d', 'wb').write(first + b'BIGH' + int(BIGX).to_bytes(2, 'big', signed=True) + int(-UP).to_bytes(2, 'big', signed=True) + b2.getvalue()); print('상체(와이드) 적용', WB, BIGX)
