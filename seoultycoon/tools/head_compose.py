"""시장(여) 머리 방법 3: 기본 통머리(work/gpt/fhead.png)에 표정 시트(work/gpt/faces18.png 아랫줄 9~17)의 눈썹·눈·코·입 부분을
눈 두 점/입 점 기준 유사변환으로 정렬하고, 피부톤을 기본 머리 볼 색에 맞춘 뒤 넓게 흐린 마스크로 합성해 표정별 통머리를 만든다.
  python3 tools/head_compose.py [표정번호...] → work/gpt/heads/h{n}.png """
import os, sys
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); gp = root + '/work/gpt'
base = Image.open(gp + '/fhead.png').convert('RGB'); sheet = Image.open(gp + '/faces_final.png').convert('RGB')
BL, BR, BM = (640, 428), (1010, 434), (805, 775)                                      # 기본 머리의 왼눈·오른눈·입 중심(px)
def faces(row):
    a = np.asarray(sheet).astype(int); y0, y1 = (84, 340) if row == 0 else (448, 720); sub = a[y0:y1]
    m = (sub[..., 0] > 150) & (sub[..., 0] - sub[..., 2] > 40)                          # 살색(파란 배경 제외)
    m = ndi.binary_opening(m, iterations=2); out = []; cols = m.sum(0) > 20; s = None
    for x, v in enumerate(list(cols) + [False]):
        if v and s is None: s = x
        if not v and s is not None:
            if x - s > 120: ys = np.where(m[:, s:x].any(1))[0]; out.append((s, ys.min() + y0, x, ys.max() + y0 + 1))
            s = None
    return out
def landmarks(b):
    """표정 칸 안 얼굴 bbox 안에서 눈(눈썹 아래 띠의 어두운 화소 무게중심, 좌/우 반쪽)과 입(붉은·어두운 화소, 아래 띠)을 직접 찾는다. 감은 눈도 맞는다."""
    x0, y0, x1, y1 = b; w, h = x1 - x0, y1 - y0; a = np.asarray(sheet).astype(int); lum = a.sum(2) / 3
    def cen(xa, xb, ya, yb, pred):
        sub = a[int(ya):int(yb), int(xa):int(xb)]; m = pred(sub)
        if m.sum() < 8: return None
        ys, xs = np.nonzero(m); return (xa + xs.mean(), ya + ys.mean())
    dark = lambda sub: sub.sum(2) / 3 < 95
    # 눈썹(위) 다음에 오는 눈 줄: 띠 안에서 가장 어두운 가로 줄 무게중심
    L = cen(x0 + 0.12 * w, x0 + 0.46 * w, y0 + 0.26 * h, y0 + 0.44 * h, dark) or (x0 + 0.27 * w, y0 + 0.34 * h)
    R = cen(x0 + 0.54 * w, x0 + 0.88 * w, y0 + 0.26 * h, y0 + 0.44 * h, dark) or (x0 + 0.73 * w, y0 + 0.34 * h)
    M = cen(x0 + 0.28 * w, x0 + 0.72 * w, y0 + 0.62 * h, y0 + 0.86 * h, lambda sub: (sub[..., 0] - sub[..., 1] > 70) | (sub.sum(2) / 3 < 90)) or (x0 + 0.5 * w, y0 + 0.72 * h)
    return L, R, M
def sim(src, dst):                                                                      # 눈 두 점 → 유사변환 계수(회전·스케일)
    (a, b), (c, d) = src[:2], dst[:2]; sa = np.array([c[0] - a[0], c[1] - a[1]]) if False else None
    v1 = np.array([b[0] - a[0], b[1] - a[1]]); v2 = np.array([d[0] - c[0], d[1] - c[1]]); s = np.hypot(*v2) / np.hypot(*v1)
    th = np.arctan2(v2[1], v2[0]) - np.arctan2(v1[1], v1[0]); return s, th
def compose(n):
    fs = faces(n // 9)
    if n % 9 >= len(fs): raise SystemExit(f'얼굴 {len(fs)}개만 찾음')
    b = fs[n % 9]; L = landmarks(b); s, th = sim(L, (BL, BR, BM)); th = 0.0; W, H = base.size
    mid = ((L[0][0] + L[1][0]) / 2, (L[0][1] + L[1][1]) / 2); bmid = ((BL[0] + BR[0]) / 2, (BL[1] + BR[1]) / 2)
    cs, sn = np.cos(th) / s, np.sin(th) / s                                             # 출력→입력 역변환
    M = (cs, sn, mid[0] - cs * bmid[0] - sn * bmid[1], -sn, cs, mid[1] + sn * bmid[0] - cs * bmid[1])
    sw = sheet.transform((W, H), Image.AFFINE, M, resample=Image.BICUBIC)
    bw = np.asarray(base).astype(float); ew = np.asarray(sw).astype(float)
    yy, xx = np.mgrid[0:H, 0:W]; cx, cy = (BL[0] + BR[0]) / 2, 650
    mk = (((xx - cx) / 235.0) ** 2 + ((yy - cy) / 380.0) ** 2) < 1.0; # 눈썹~턱 위 타원(위쪽은 흐리게 이어짐), 머리카락·귀·턱 가장자리는 기본 머리 유지
    valid = ndi.binary_erosion(ndi.binary_fill_holes(ndi.binary_closing((ew[..., 0] > 150) & (ew[..., 0] - ew[..., 2] > 40), iterations=6)), iterations=30); mk &= valid
    bsk = (bw[..., 0] > 190) & (bw[..., 1] > 120) & (bw[..., 2] > 70) & (bw[..., 0] > bw[..., 2] + 40); bsk = ndi.binary_erosion(ndi.binary_fill_holes(ndi.binary_closing(bsk, iterations=4)), iterations=26); mk &= bsk     # 기본 머리의 얼굴 살색 안쪽에서만 합성(머리카락·귀로 번지지 않게)
    mk = ndi.gaussian_filter(mk.astype(float), 36)[..., None]
    sk = lambda im, c, r=22: np.median(im[c[1] - r:c[1] + r, c[0] - r:c[0] + r].reshape(-1, 3), axis=0)   # 볼 부분 평균색으로 피부톤 맞춤
    bt = (sk(bw, (680, 560)) + sk(bw, (950, 560))) / 2; et = (sk(ew, (680, 560)) + sk(ew, (950, 560))) / 2
    ew2 = np.clip(ew + (bt - et), 0, 255)
    out = bw * (1 - mk) + ew2 * mk; im = Image.fromarray(out.astype(np.uint8))
    from PIL import ImageDraw                                                           # 원본 그림은 머리 꼭대기가 잘려 있어, 윗 모서리를 둥글게 깎아 둥근 머리 모양으로 만든다
    bgc = tuple(int(v) for v in bw[2, 2]); m = Image.new('L', (W, H), 255); d = ImageDraw.Draw(m); R = 230; xl, xr = 236, 1232
    for (cx0, sgn) in ((xl, 1), (xr, -1)):
        d.rectangle([cx0 - (0 if sgn > 0 else R), 0, cx0 + (R if sgn > 0 else 0), R], fill=0); d.ellipse([cx0 + (0 if sgn > 0 else -2 * R), 0, cx0 + (2 * R if sgn > 0 else 0), 2 * R], fill=255)
    d.rectangle([0, 0, xl - 1, R], fill=0); d.rectangle([xr + 1, 0, W, R], fill=0)
    out2 = Image.composite(im, Image.new('RGB', (W, H), bgc), m); return out2
if __name__ == '__main__':
    os.makedirs(gp + '/heads', exist_ok=True)
    for n in [int(a) for a in sys.argv[1:]] or [3, 4, 5, 9, 10, 11, 12, 13, 14]: compose(n).save(f'{gp}/heads/h{n}.png'); print('h', n)
