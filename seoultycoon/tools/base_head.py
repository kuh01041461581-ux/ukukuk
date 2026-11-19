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
_H, _W = a.shape[:2]; _yy, _xx = np.mgrid[0:_H, 0:_W]; _reg = (((_xx - 825) / 320.0) ** 2 + ((_yy - 560) / 420.0) ** 2) < 1.0
_sk0 = (a[..., 0] > 190) & (a[..., 1] > 130) & (a[..., 2] > 80) & (a[..., 0] > a[..., 2] + 40)
_lab, _n = ndi.label(ndi.binary_closing(_sk0, iterations=3)); _face = _lab == 1 + int(np.argmax(ndi.sum(_sk0, _lab, range(1, _n + 1))))
_reg = _reg & ndi.binary_dilation(ndi.binary_fill_holes(ndi.binary_closing(_face, iterations=25)), iterations=3)   # 얼굴 살색 덩어리 안쪽만(머리카락·귀는 손대지 않음)
_lum = a.mean(2); _good = _reg & _sk0 & (_lum > 178) & ~((a[..., 0] - a[..., 1] > 90))
_w = ndi.gaussian_filter(_good.astype(float), 40); _fill = np.dstack([ndi.gaussian_filter(np.where(_good, a[..., c], 0).astype(float), 40) for c in range(3)]) / np.maximum(_w, 1e-3)[..., None]
_brow = (_yy > 250) & (_yy < 370) & (_xx > 470) & (_xx < 1180) & ~ndi.binary_dilation(~ndi.binary_fill_holes(ndi.binary_closing(_face, iterations=25)), iterations=2)   # 눈썹 띠는 얼굴 안쪽이면 전부 메운다(머리카락 경계 제외)
_reg = _reg | _brow
_soft = ndi.gaussian_filter(_reg.astype(float), 6)[..., None]; a = (a * (1 - _soft) + _fill * _soft).astype(int); im = Image.fromarray(a.astype(np.uint8))
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
CUT = int(os.environ.get('FF_CUT', '0')); ch_ff = np.median([np.where(skin(hd_of(f'ff{i}')).any(1))[0].max() for i in FEM]) - 2 * CUT;   # 아래 CUT 줄을 잘라 낸 턱 끝에 베이스 턱을 붙인다
f = (ch_ff - ey_ff) / (960 - 431) * float(os.environ.get('BASE_SCALE', '1.0'))
cx_ff = (el[0] + er[0]) / 2; ey_ff = (el[1] + er[1]) / 2; cx_b = 825 - BB[0]; ey_b = 431 - BB[1]
big = rgba.resize((max(1, round(rgba.width * f)), max(1, round(rgba.height * f))), Image.LANCZOS)
WX = float(os.environ.get('HAIR_WX', '1.06')); PADX = int(np.ceil(big.width * (WX - 1) / 2)) + 1
G = np.asarray(big).astype(float); gh, gw = G.shape[:2]; out = np.zeros((gh, gw + 2 * PADX, 4)); ccx = cx_b * f
eyb = ey_b * f; mob = (775 - BB[1]) * f                                               # 눈 높이까지는 WX, 입 높이에서 1.0
for y in range(gh):
    t = 1.0 if y <= eyb else max(0.0, 1.0 - (y - eyb) / max(1.0, mob - eyb)); sx = 1.0 + (WX - 1.0) * t
    xs = (np.arange(gw + 2 * PADX) - PADX - ccx) / sx + ccx; x0 = np.floor(xs).astype(int); fr = xs - x0
    ok = (x0 >= 0) & (x0 + 1 < gw)
    for c in range(4): out[y, ok, c] = G[y, x0[ok], c] * (1 - fr[ok]) + G[y, x0[ok] + 1, c] * fr[ok]
CUTB = int(os.environ.get('BASE_CUT', '1')) * 2; chb = int(round((960 - BB[1]) * f))
if CUTB: out = np.concatenate([out[:chb - CUTB], out[chb:]], axis=0)                 # 베이스 얼굴 턱 바로 위 CUTB 줄 삭제 → 턱이 1줄 올라간다
_rgb = out[..., :3]; _l = _rgb.mean(2); _med = np.dstack([ndi.median_filter(_rgb[..., c], size=7) for c in range(3)]); _ml = _med.mean(2)
_sk = (out[..., 3] > 200) & (_med[..., 0] > 190) & (_med[..., 0] > _med[..., 2] + 40); _spot = _sk & (_l < _ml - 18)
out[..., :3] = np.where(_spot[..., None], _med, _rgb)                                  # 얼굴 살색 안의 작은 어두운 점(지우고 남은 눈썹 끝 등) 제거
big = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA')
bx = round(FF_DX + cx_ff - ccx - PADX); by = round(FF_DY + ey_ff - ey_b * f)
buf = io.BytesIO(); big.save(buf, format='PNG', optimize=True)
p = f'{mod}/fd1.d'; raw = open(p, 'rb').read(); j = raw.find(b'BIGH'); raw = raw[:j] if j > 0 else raw
if raw.endswith(b'SKIP'): raw = raw[:-4]
open(p, 'wb').write(raw + b'BIGH' + int(bx).to_bytes(2, 'big', signed=True) + int(by).to_bytes(2, 'big', signed=True) + buf.getvalue())
p = f'{mod}/fh1.d'; raw = open(p, 'rb').read()
if not raw.endswith(b'SKIP'): open(p, 'wb').write(raw + b'SKIP')
# 표정 조각을 베이스에 맞춘다: (1) 살색 톤을 아래 베이스의 같은 자리 색으로 저주파 보정(이마·볼 톤 연결) (2) 바깥 윤곽에서 안쪽으로 거리 기반 알파 흐림(얼굴 라인 이음새 제거)
B = np.asarray(big).astype(float); BW, BH = big.size; CUT = int(os.environ.get('FF_CUT', '1'))
def fit_ff(name):
    p = f'{mod}/{name}.d'; raw = open(p, 'rb').read(); e1 = raw.find(b'IEND') + 8; e2 = raw.find(b'IEND', e1) + 8
    lo = np.asarray(Image.open(io.BytesIO(raw[:e1])).convert('RGBA')).astype(float); hd = np.asarray(Image.open(io.BytesIO(raw[e1:e2])).convert('RGBA')).astype(float)
    def fit(a, k):
        a = a.copy(); a[a.shape[0] - CUT * (2 // k):, :, 3] = 0                       # 표정 조각 맨 아래 CUT 줄(저해상도 기준) 제거 — 턱이 베이스 턱선에 맞도록
        _t = 6 // k; _dk = (a[:_t, :, :3].mean(2) < 90) | (a[:_t, :, 2] > a[:_t, :, 0] - 5); _t2 = 12 // k; a[:_t2, :, 3] = np.where(a[:_t2, :, 2] > a[:_t2, :, 0] - 5, 0, a[:_t2, :, 3]); a[:_t, :, 3] = np.where(_dk, 0, a[:_t, :, 3])        # 이마 맨 위 모서리의 어두운 점(머리카락 찌꺼기) 제거
        h, w = a.shape[:2]; under = np.zeros((h, w, 4))
        for y in range(h):                                                   # 이 조각 화소 아래에 깔리는 베이스 색 (표정 조각 위치 = 베이스 기준 FF_DX,FF_DY 에서 bx,by 를 뺀 자리)
            for x in range(w):
                X = int((x * 2 // k + 0.5) if False else (x * (2 // k) + FF_DX - bx)); Y = int(y * (2 // k) + FF_DY - by)
                if 0 <= X < BW and 0 <= Y < BH: under[y, x] = B[Y, X]
        sk = (a[..., 3] > 200) & (a[..., 0] > 150) & (a[..., 0] > a[..., 2] + 30) & (a[..., :3].mean(2) > 120)
        ub = (under[..., 3] > 200) & (under[..., 0] > 150) & (under[..., 0] > under[..., 2] + 30)
        both = sk & ub; sig = 6.0 / k
        if both.sum() > 10:
            wgt = ndi.gaussian_filter(both.astype(float), sig) + 1e-3
            diff = np.dstack([ndi.gaussian_filter(np.where(both, under[..., c] - a[..., c], 0), sig) for c in range(3)]) / wgt[..., None]
            diff = np.clip(diff, -60, 60); a = a.copy(); edge_w = np.clip(1.0 - ndi.distance_transform_edt(a[..., 3] > 30) / (14.0 / k), 0, 1)[..., None]   # 가장자리로 갈수록 베이스 색에 완전히 맞춘다
            a[..., :3] = np.clip(a[..., :3] + diff * sk[..., None] * (0.75 + 0.25 * edge_w), 0, 255)
        _d0 = ndi.distance_transform_edt(a[..., 3] > 30); _lum = a[..., :3].mean(2); _med = np.median(_lum[sk]) if sk.any() else 200
        _cont = (_d0 < 15.0 / k) & ((_lum < _med * 0.9) | (a[..., 0] - a[..., 2] < 70)) & ub & (a[..., 3] > 30)            # 표정 조각 바깥 윤곽선(어두운 얼굴선)이 베이스 살색 위에 남으면 베이스 색으로 바꾼다
        a[..., :3] = np.where(_cont[..., None], under[..., :3], a[..., :3])
        dist = ndi.distance_transform_edt(a[..., 3] > 30); R = 11.0 / k                # 바깥 윤곽에서 R 화소 안쪽까지 서서히 불투명
        fade = np.clip(dist / R, 0, 1) ** 1.2
        h_ = a.shape[0]; top = np.clip((np.arange(h_)[:, None] - 1.0 / k) / (12.0 / k), 0, 1) ** 1.5   # 이마 쪽은 더 길게(12화소) 흐려 위 경계 띠를 없앤다
        fade = np.minimum(fade, np.broadcast_to(top, fade.shape))
        ubs = ndi.binary_erosion(ub, iterations=1)                                     # 아래가 머리카락(베이스 살색 아님)인 곳은 흐리지 않는다 — 흐리면 머리카락이 비쳐 턱에 검은 얼룩이 생긴다
        hh, ww = a.shape[:2]; yy, xx = np.mgrid[0:hh, 0:ww].astype(float); eyk = ey_ff * k / 2.0
        top = eyk - 15.0 * k / 2.0; bot = hh - 1.0; cyo = (top + bot) / 2 + (bot - top) * 0.08; ryo = (bot - top) / 2 + (bot - top) * 0.08; rxo = ww / 2.0 + 1.0
        rr = (np.abs((xx - (ww - 1) / 2.0) / rxo) ** 3.5 + np.abs((yy - cyo) / ryo) ** 3.5) ** (1 / 3.5)          # 표정 조각을 사각형이 아닌 타원으로: 눈썹 위부터 턱까지, 바깥 띠는 서서히 투명
        oval = np.clip((1.0 - rr) / 0.16, 0, 1) ** 1.2
        a[..., 3] = a[..., 3] * np.where(yy < eyk + 2 * k / 2.0, oval, np.maximum(oval, (yy >= eyk).astype(float) * fade))
        inside = ndi.binary_dilation(ub, iterations=1); soft_in = ndi.gaussian_filter(inside.astype(float), 0.8 / k + 0.3)
        a[..., 3] = a[..., 3] * np.where(ubs, fade, 1.0) * np.clip(soft_in * 1.6 - 0.3, 0, 1)   # 얼굴 라인은 베이스 턱선을 따른다: 베이스 살색 밖(머리카락·배경)으로 나간 표정 조각은 잘라 낸다
        return a
    lo2, hd2 = fit(lo, 1), fit(hd, 2); b1, b2 = io.BytesIO(), io.BytesIO()
    Image.fromarray(lo2.astype(np.uint8), 'RGBA').save(b1, format='PNG'); Image.fromarray(hd2.astype(np.uint8), 'RGBA').save(b2, format='PNG')
    open(p, 'wb').write(b1.getvalue() + b2.getvalue() + raw[e2:])
for i in FEM: fit_ff(f'ff{i}')
print('여자 시장 고정 베이스', big.size, '오프셋', bx, by, '표정 눈높이', ey_ff, '얼굴폭', fw_ff)
