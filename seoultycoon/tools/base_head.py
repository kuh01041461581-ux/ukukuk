"""여자 시장 고정 베이스: 바탕 얼굴 조각(fd1) 자리에 사용자가 준 기본 머리·얼굴 그림(work/gpt/fhead.png: 머리카락·이마·귀·귀걸이·얼굴)을
한 장(BIGH)으로 그리고, 머리 조각(fh1)은 그리지 않는다(SKIP). 표정 조각(ff)은 그 위에 얹히며 가장자리가 흐려져 베이스 얼굴과 이어진다.
베이스는 표정 조각의 눈 높이·얼굴 중심·얼굴 폭에 맞춰 크기와 위치를 정한다.  → st2android/mod/fd1.d, fh1.d"""
import os, io
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); mod = root + '/st2android/mod'; gp = root + '/work/gpt/fhead.png'
if not os.path.exists(gp): print('fhead.png 없음 — 건너뜀'); raise SystemExit(0)
FEM = (3, 4, 5, 9, 10, 11, 12, 13, 14); FF_DX, FF_DY = 18, 0                # 표정 조각은 바탕 얼굴보다 HD 18칸 오른쪽에 그려진다
def hd_of(n):
    raw = open(f'{mod}/{n}.d', 'rb').read(); e1 = raw.find(b'IEND') + 8; e2 = raw.find(b'IEND', e1) + 8
    return np.asarray(Image.open(io.BytesIO(raw[e1:e2])).convert('RGBA')).astype(int)
def skin(a): return (a[..., 3] > 200) & (a[..., 0] > 170) & (a[..., 1] > 110) & (a[..., 0] > a[..., 2] + 35)
def eyes(a, sk):                                                               # 눈 줄: 얼굴 위 20~50% 띠에서 가장 어두운 줄
    ys = np.where(sk.any(1))[0]; y0, y1 = ys.min(), ys.max(); lum = a[..., :3].mean(2); best = None
    for y in range(int(y0 + (y1 - y0) * 0.2), int(y0 + (y1 - y0) * 0.5)):
        xs = np.where(sk[y] | (lum[y] < 90) & (a[y, :, 3] > 200))[0]
        if len(xs) == 0: continue
        v = (lum[y, xs.min():xs.max() + 1] < 90).sum()
        if best is None or v > best[0]: best = (v, y)
    return best[1]
def eye_pts(a, sk, ey):                                                        # 눈 줄 ±3 안에서 왼/오른쪽 어두운 화소 무게중심
    lum = a[..., :3].mean(2); xs = np.where(sk.any(0))[0]; cx = (xs.min() + xs.max()) / 2; out = []
    for lo, hi in ((xs.min(), cx), (cx, xs.max() + 1)):
        sub = (lum[ey - 3:ey + 4, int(lo):int(hi)] < 95) & (a[ey - 3:ey + 4, int(lo):int(hi), 3] > 200); yy, xx = np.nonzero(sub)
        out.append((lo + xx.mean(), ey - 3 + yy.mean()) if len(xx) else (lo + (hi - lo) / 2, ey))
    return out
# 표정 조각들(HD)의 평균 눈 높이·얼굴 중심·얼굴 폭
E_y, C_x, F_w, EL, ER = [], [], [], [], []
for i in FEM:
    a = hd_of(f'ff{i}'); sk = skin(a); E_y.append(eyes(a, sk)); ey = E_y[-1]; xs = np.where(sk[ey + 4])[0]; C_x.append((xs.min() + xs.max()) / 2); F_w.append(xs.max() - xs.min() + 1); l, r = eye_pts(a, sk, ey); EL.append(l); ER.append(r)
ey_ff, cx_ff, fw_ff = np.median(E_y), np.median(C_x), np.median(F_w); el = np.median(EL, axis=0); er = np.median(ER, axis=0)
# 기본 머리 그림: 배경 제거 → 같은 기준으로 눈 높이·얼굴 중심·폭 측정 → 크기 맞춤
im = Image.open(gp).convert('RGB'); a = np.asarray(im).astype(int)
# 베이스 얼굴의 눈·눈썹·코·입을 지우고 매끈한 살색으로 메운다(표정 조각 가장자리 흐림 밑으로 베이스 이목구비가 비치지 않게)
_H, _W = a.shape[:2]; _yy, _xx = np.mgrid[0:_H, 0:_W]; _reg = (((_xx - 825) / 300.0) ** 2 + ((_yy - 600) / 360.0) ** 2) < 1.0
_lum = a.mean(2); _good = _reg & (_lum > 150) & ~((a[..., 0] - a[..., 1] > 90))
_w = ndi.gaussian_filter(_good.astype(float), 40); _fill = np.dstack([ndi.gaussian_filter(np.where(_good, a[..., c], 0).astype(float), 40) for c in range(3)]) / np.maximum(_w, 1e-3)[..., None]
_soft = ndi.gaussian_filter(_reg.astype(float), 25)[..., None]; a = (a * (1 - _soft) + _fill * _soft).astype(int); im = Image.fromarray(a.astype(np.uint8))
bg = np.median(a[:6, :6].reshape(-1, 3), axis=0)
m = ndi.binary_closing(np.abs(a - bg).sum(2) > 60, iterations=2); lab, n = ndi.label(m); m = ndi.binary_fill_holes(lab == 1 + int(np.argmax(ndi.sum(m, lab, range(1, n + 1)))))
W0, H0 = im.size; mk = Image.new('L', (W0, H0), 255); d = ImageDraw.Draw(mk); R = 230; xl, xr = 236, 1232          # 원본 머리 꼭대기가 잘려 있어 윗 모서리를 둥글게
d.rectangle([xl, 0, xl + R, R], fill=0); d.ellipse([xl, 0, xl + 2 * R, 2 * R], fill=255); d.rectangle([xr - R, 0, xr, R], fill=0); d.ellipse([xr - 2 * R, 0, xr, 2 * R], fill=255)
d.rectangle([0, 0, xl - 1, R], fill=0); d.rectangle([xr + 1, 0, W0, R], fill=0)
al = (m * 255).astype(np.uint8) & np.asarray(mk)
rgba = Image.fromarray(np.dstack([a.astype(np.uint8), al]), 'RGBA'); BB = rgba.getbbox(); rgba = rgba.crop(BB)
b = np.asarray(rgba).astype(int); skb = skin(b); ey_b = eyes(b, skb); xs = np.where(skb[ey_b + int(rgba.height * 0.04)])[0]
cx_b = (xs.min() + xs.max()) / 2; fw_b = xs.max() - xs.min() + 1
# 크기: 표정 조각의 눈~턱 길이(HD) = 베이스 그림의 눈(431)~턱(960) 길이. 위치: 두 눈 가운데(825,431)를 표정 조각의 두 눈 가운데에 맞춘다(원본 좌표, 이목구비를 지우기 전 측정)
ch_ff = np.median([np.where(skin(hd_of(f'ff{i}')).any(1))[0].max() for i in FEM]); f = (ch_ff - ey_ff) / (960 - 431) * float(os.environ.get('BASE_SCALE', '1.0'))
cx_ff = (el[0] + er[0]) / 2; ey_ff = (el[1] + er[1]) / 2; cx_b = 825 - BB[0]; ey_b = 431 - BB[1]
big = rgba.resize((max(1, round(rgba.width * f)), max(1, round(rgba.height * f))), Image.LANCZOS)
bx = round(FF_DX + cx_ff - cx_b * f); by = round(FF_DY + ey_ff - ey_b * f)
buf = io.BytesIO(); big.save(buf, format='PNG', optimize=True)
p = f'{mod}/fd1.d'; raw = open(p, 'rb').read(); j = raw.find(b'BIGH'); raw = raw[:j] if j > 0 else raw
if raw.endswith(b'SKIP'): raw = raw[:-4]
open(p, 'wb').write(raw + b'BIGH' + int(bx).to_bytes(2, 'big', signed=True) + int(by).to_bytes(2, 'big', signed=True) + buf.getvalue())
p = f'{mod}/fh1.d'; raw = open(p, 'rb').read()
if not raw.endswith(b'SKIP'): open(p, 'wb').write(raw + b'SKIP')
print('여자 시장 고정 베이스', big.size, '오프셋', bx, by, '표정 눈높이', ey_ff, '얼굴폭', fw_ff)
