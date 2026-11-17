"""여자 시장 기본 머리: 사용자가 정리한 '머리+얼굴 한 장'(work/gpt/fhead.png)을 바탕 얼굴(fd1) 자리에 통그림(BIGH)으로 그리고, 조립용 머리 조각(fh1)은 그리지 않는다(SKIP).
표정(ff) 조각은 그대로 위에 덮는다. 통그림은 표정 조각의 얼굴 폭·턱 위치에 맞춰 크기·위치를 잡는다.   → st2android/mod/fd1.d, fh1.d"""
import os, io, sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); mod = root + '/st2android/mod'; p = root + '/work/gpt/fhead.png'
if not os.path.exists(p): print('fhead 없음 — 건너뜀'); sys.exit(0)
def ld(nm):
    r = open(f'{mod}/{nm}.d', 'rb').read(); k = r.find(b'IEND') + 8; return r, k, np.asarray(Image.open(io.BytesIO(r[k:])).convert('RGBA')).astype(np.float32)
sk2 = lambda x: (x[..., 3] > 200) & (x[..., 0] > 170) & (x[..., 1] > 100) & (x[..., 0] > x[..., 2] + 35) & (x[..., 2] < x[..., 1] - 5)
im = Image.open(p).convert('RGB'); a = np.asarray(im).astype(int); bg = np.median(a[:6, :6].reshape(-1, 3), axis=0)
m = ndi.binary_closing(np.abs(a - bg).sum(2) > 60, iterations=1); lab, n = ndi.label(m); sizes = ndi.sum(m, lab, range(1, n + 1)); m = ndi.binary_fill_holes(lab == 1 + int(np.argmax(sizes)))
al = Image.fromarray((m * 255).astype(np.uint8)); o = im.convert('RGBA'); o.putalpha(al)
arr = np.asarray(o).copy(); arr[(arr[..., 2].astype(int) > arr[..., 0].astype(int) + 40) & (arr[..., 3] > 0) & (arr[..., 1] > 100) & (arr[..., 0] < 190), 3] = 0     # 푸른 귀걸이는 살린다(채도 높은 푸른색만 제외하지 않음)
o = Image.fromarray(np.asarray(o), 'RGBA'); o = o.crop(o.getbbox())
# 기준: 표정 ff3 의 얼굴(피부) 영역 — 화면 위치 (232,270), HD 48x52
_, _, f3 = ld('ff3'); ys, xs = np.where(sk2(f3)); fx0, fx1, fy1 = xs.min(), xs.max(), ys.max(); FFW = fx1 - fx0; FCX = 232 + (fx0 + fx1) / 2.0; FCHIN = 270 + fy1
oa = np.asarray(o).astype(np.float32); ys, xs = np.where(sk2(oa) & (oa[..., 1] > 120)); big_ys, big_xs = ys, xs
# 얼굴 영역: 가장 큰 피부 덩어리
labs, nn = ndi.label(sk2(oa)); sz = ndi.sum(sk2(oa), labs, range(1, nn + 1)); fm = labs == 1 + int(np.argmax(sz)); ys, xs = np.where(fm)
bx0, bx1, by1 = xs.min(), xs.max(), ys.max(); BW = bx1 - bx0
s = FFW / float(BW) * float(os.environ.get('FSCALE', '1.0'))
new = o.resize((int(o.width * s), int(o.height * s)), Image.LANCZOS)
cx = (bx0 + bx1) / 2.0 * s; chin = by1 * s
left = int(round(FCX - cx)) + int(os.environ.get('DX', '0')); top = int(round(FCHIN - chin)) + int(os.environ.get('DY', '0'))
BIGX, BIGY = left - 214, top - 270
raw = open(f'{mod}/fd1.d', 'rb').read(); k = raw.find(b'IEND') + 8; first = raw[:k]; b = io.BytesIO(); new.save(b, format='PNG', optimize=True)
open(f'{mod}/fd1.d', 'wb').write(first + b'BIGH' + int(BIGX).to_bytes(2, 'big', signed=True) + int(BIGY).to_bytes(2, 'big', signed=True) + b.getvalue())
raw = open(f'{mod}/fh1.d', 'rb').read()
if not raw.endswith(b'SKIP'): open(f'{mod}/fh1.d', 'wb').write(raw + b'SKIP')
print('여자 기본 머리 통그림', new.size, 'scale', round(s, 3), 'at', left, top)

# 표정 조각(ff 여자)의 바깥 둘레를 부드럽게 투명하게: 윗(이마)·양옆·턱 가장자리를 타원 마스크로 서서히 비워, 아래 통머리의 이마·볼과 사각형 경계 없이 이어지게 한다.
# 피부색은 통머리 얼굴 평균색에 맞춘다(둘이 같은 색이라 알파가 섞여도 색이 변하지 않는다).
head = np.asarray(new).astype(np.float32); hs = head[sk2(head)][:, :3].mean(0)
FEM = (3, 4, 5, 9, 10, 11, 12, 13, 14)
for i in FEM:
    raw, k, a = ld(f'ff{i}'); h, w = a.shape[:2]; yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    m = sk2(a); a[m, :3] += (hs - a[m][:, :3].mean(0)) * 0.7
    nx = (xx - (w - 1) / 2.0) / (w * 0.5); ny = (yy - h * 0.55) / (h * 0.55)
    d = np.sqrt(nx ** 2 + ny ** 2)                                      # 0=중심, 1=조각 경계
    fade = np.clip((0.98 - d) / 0.28, 0, 1)                             # 경계에서 0.28 안쪽까지 서서히
    a[..., 3] = a[..., 3] * fade
    b = io.BytesIO(); Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(f'{mod}/ff{i}.d', 'wb').write(raw[:k] + b.getvalue())
print('표정 조각 둘레 투명 처리', len(FEM))
