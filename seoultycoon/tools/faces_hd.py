"""시장 얼굴(ff)·머리(fh)를 원본 도트 그대로 두고, 벡터풍 2배 해상도 층만 덧댄다(vectorize.vec_hd: 색 영역 경계를 매끈하게, 색은 원본 팔레트 유지).
GPT 그림은 '이렇게 선명하고 부드러운 느낌'의 참고용이고, 모양은 원본 그대로라 얼굴·머리의 이질감이 없다.   → st2android/mod/ff*.d, fh*.d"""
import os, io, sys, glob
import numpy as np
from PIL import Image, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vectorize as VC
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); src = root + '/work/st2'; mod = root + '/st2android/mod'
from scipy import ndimage as ndi
def eye_shine(a):
    """눈동자에 작은 하이라이트(GPT 그림의 또렷한 눈 느낌)를 얹는다: 눈 높이대의 어두운 덩어리 중 좌·우 눈동자를 찾아 오른쪽 위에 밝은 점을 찍는다."""
    h, w = a.shape[:2]; lum = a[..., :3] @ np.array([0.30, 0.59, 0.11], np.float32); y0, y1 = int(h * 0.40), int(h * 0.58)
    dark = (lum < 80) & (a[..., 3] > 0); dark[:y0] = False; dark[y1:] = False
    lab, k = ndi.label(dark); comps = []
    for i in range(1, k + 1):
        ys, xs = np.where(lab == i)
        if 5 <= len(ys) <= 80 and (ys.max() - ys.min()) >= 2: comps.append((len(ys), xs.mean(), ys.min(), xs.max(), ys.mean()))
    left = [c for c in comps if c[1] < w / 2]; right = [c for c in comps if c[1] >= w / 2]
    for grp in (left, right):
        if not grp: continue
        c = max(grp, key=lambda t: t[0]); x, y = int(c[3]) - 1, int(c[2]) + 1
        for dy in range(2):
            for dx in range(2):
                yy, xx = y + dy, x - dx
                if 0 <= yy < h and 0 <= xx < w: a[yy, xx, :3] = (252, 250, 244)
    return a
from PIL import ImageEnhance
def crisp(im):
    """밋밋함 해소: 불투명 화소의 평균을 기준으로 대비를 올리고 윤곽(눈썹·눈·입)을 더 짙게, 가장자리는 선명하게(언샤프)."""
    al = np.asarray(im.getchannel('A')); a = np.asarray(im.convert('RGB')).astype(np.float32); op = al > 0
    lum = a @ np.array([0.30, 0.59, 0.11], np.float32); mean = lum[op].mean()
    a = (a - mean) * 1.0 + mean                                                # 대비
    g = (a @ np.array([0.30, 0.59, 0.11], np.float32))[..., None]; a = g + (a - g) * 1.0      # 채도(약하게)
    lum = a @ np.array([0.30, 0.59, 0.11], np.float32); a *= np.where(lum < 100, 0.86, 1.0)[..., None]   # 짙은 선
    rgb = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGB').filter(ImageFilter.UnsharpMask(radius=1.0, percent=55, threshold=3))
    out = rgb.convert('RGBA'); out.putalpha(im.getchannel('A')); return out
MALE = {'ff0', 'ff1', 'ff2', 'ff6', 'ff7', 'ff8', 'ff12'}
def sideburns(hd):
    """남자 얼굴 위쪽 양옆(관자놀이)에 구레나룻을 그려, 머리 조각(얼굴보다 넓은 모자 모양)과 이어지게 한다. 대머리 단계에서는 옆머리 술이 된다."""
    a = np.asarray(hd).copy(); h, w = a.shape[:2]; dk = np.array([26, 20, 30], np.uint8)
    for y in range(0, 16):                                          # 위 8줄(2배 층 16행): 폭이 위에서 아래로 좁아진다
        t = y / 16.0; wd = int(round(7 - 5.0 * t))
        for x in range(0, wd):
            for xx in (x, w - 1 - x):
                if a[y, xx, 3] > 0: a[y, xx, :3] = dk * (1 - 0.0) ; a[y, xx, 3] = 255
    return Image.fromarray(a, 'RGBA')
def face_top_color():
    """정리 시트 얼굴(ff)의 이마 쪽 평균색(머리 조각 아래 피부와 이음새를 맞추기 위해)"""
    acc = []
    for i in range(18):
        p = f'{mod}/ff{i}.d'
        if not os.path.exists(p): continue
        raw = open(p, 'rb').read(); k = raw.find(b'IEND') + 8
        try: im = np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(float)
        except Exception: continue
        top = im[0:6]; m = top[..., 3] > 200; acc.append(top[m][:, :3])
    return np.clip(np.concatenate(acc).mean(0) * 1.0, 0, 255) if acc else np.array([240., 200., 155.])
TOP = face_top_color()
def match_skin(a):
    """머리 조각 안의 이마 피부를 위쪽은 원래 색, 아래(얼굴과 닿는 쪽)는 얼굴 이마색으로 서서히 바꿔 가로 이음새를 없앤다."""
    a = a.copy(); h = a.shape[0]
    skin = (a[..., 3] > 0) & (a[..., 0] > 190) & (a[..., 1] > 110) & (a[..., 0] > a[..., 2] + 40)
    ys = np.where(skin.any(1))[0]
    if len(ys) == 0: return a
    y0, y1 = ys.min(), h - 1
    for y in range(y0, h):
        t = min(1.0, (y - y0) / max(1, y1 - y0)) ** 0.8 * (1.0 if y >= h - 6 else 0.92)
        row = skin[y]; a[y, row, :3] = a[y, row, :3] * (1 - t) + TOP * t
        u = (y - y0) / max(1, y1 - y0); a[y, row, :3] *= (0.80 + 0.20 * min(1.0, u * 3.0) ** 0.7)           # 머리 바로 아래 이마는 그늘, 아래로 갈수록 얼굴색
    return a
n = 0
for nm in [f'fh{i}' for i in range(7)]:          # 표정(ff)·바탕 얼굴(fd)은 정리 시트(gpt_import)를 쓴다
    mp = f'{mod}/{nm}.d'; sp = f'{src}/{nm}.d'
    raw = open(mp if os.path.exists(mp) else sp, 'rb').read(); i = raw.find(b'IEND') + 8; base = Image.open(io.BytesIO(raw[:i])).convert('RGBA')
    GPT_HAIR = os.path.exists(root + '/work/gpt/hair.png') and len(raw) > i + 8
    hd = Image.open(io.BytesIO(raw[i:])).convert('RGBA') if GPT_HAIR else VC.vec_hd(base)       # 정리 시트 머리가 있으면 그 2배 층을 그대로 쓴다
    a = np.asarray(hd).astype(np.float32); al = a[..., 3:4] > 0
    lum = a[..., :3] @ np.array([0.30, 0.59, 0.11], np.float32); skin = (a[..., 0] > 150) & (a[..., 0] > a[..., 2] + 30)           # 피부: 아주 약한 밝기 보정(촉촉한 느낌)
    a[..., :3] = np.where(skin[..., None], np.clip(a[..., :3] * 1.03 + 2, 0, 255), a[..., :3])
    if nm.startswith('fh'):
        a = match_skin(a)
        for r in (a.shape[0] - 1, a.shape[0] - 2):                       # 맨 아랫줄이 비면 배경이 선으로 비친다 — 위 줄을 복사
            m = (a[r, :, 3] < 255) & (a[r - 1, :, 3] > 200); a[r, m] = a[r - 1, m]
    if False and nm.startswith('ff'): a = eye_shine(a)      # 눈이 너무 작아 효과가 미미하고 잡티처럼 보여 꺼 둠
    hd = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA')
    if nm.startswith('ff'): hd = crisp(hd)
    # (남자 구레나룻 덧그리기는 어색해서 쓰지 않는다)
    b = io.BytesIO(); hd.save(b, format='PNG', optimize=True); open(mp, 'wb').write(raw[:i] + b.getvalue()); n += 1
print('시장 얼굴·머리 벡터풍 2배 층', n, '장')

# 바탕 얼굴 맨 윗부분(머리 조각과 닿는 6줄)의 피부를 이마색으로 서서히 맞춰 가로 이음새를 줄인다
for nm in ('fd0', 'fd1', 'fd2'):
    mp = f'{mod}/{nm}.d'; raw = open(mp, 'rb').read(); i = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[i:])).convert('RGBA')).astype(np.float32)
    sk = (a[..., 3] > 0) & (a[..., 0] > 150) & (a[..., 0] > a[..., 2] + 40)
    for y in range(0, 8):
        t = 0.85 * (1 - y / 8.0); r = sk[y]; a[y, r, :3] = a[y, r, :3] * (1 - t) + TOP * t
    b = io.BytesIO(); Image.fromarray(a.astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(mp, 'wb').write(raw[:i] + b.getvalue())

# 이음새 줄 정리: 표정(ff)·바탕(fd)의 맨 윗 2줄과 머리(fh)의 맨 아랫 2줄을 이웃 줄과 같게 복사(가로 띠 제거)
def _force(nm, top):
    p = f'{mod}/{nm}.d'; raw = open(p, 'rb').read(); i = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[i:])).convert('RGBA')).copy()
    if top: a[0] = a[3]; a[1] = a[3]; a[2] = a[3]
    else: a[-1] = a[-4]; a[-2] = a[-4]; a[-3] = a[-4]
    b = io.BytesIO(); Image.fromarray(a, 'RGBA').save(b, format='PNG', optimize=True); open(p, 'wb').write(raw[:i] + b.getvalue())
for i in range(18): _force(f'ff{i}', True)
for i in range(3): _force(f'fd{i}', True)
for i in range(6): _force(f'fh{i}', False)

# 바탕 얼굴(fd0~2) 머리카락 색을 새 머리(갈색) 색으로 통일
for nm in ('fd0', 'fd1', 'fd2'):
    mp = f'{mod}/{nm}.d'; raw = open(mp, 'rb').read(); i = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[i:])).convert('RGBA')).astype(np.float32)
    lum = a[..., :3] @ np.array([.3, .59, .11], np.float32); hair = (a[..., 3] > 0) & (lum < 95) & (a[..., 0] < 140)
    if hair.sum() == 0: continue
    tgt = np.array([60., 33., 34.], np.float32); k = np.clip(lum[hair] / max(lum[hair].mean(), 1), 0.4, 2.2)[:, None]
    a[hair, :3] = np.clip(tgt * k * 1.1, 0, 255)
    b = io.BytesIO(); Image.fromarray(a.astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(mp, 'wb').write(raw[:i] + b.getvalue())

# 헤어라인 맞춤: 머리 조각 맨 아래 14줄의 이마(피부) 폭을 바탕 얼굴 맨 윗줄의 피부 폭으로 서서히 넓히거나 좁혀, 이마에서 얼굴로 이어지는 선의 단차를 없앤다
def _skin2(a): return (a[..., 3] > 0) & (a[..., 0] > 170) & (a[..., 1] > 100) & (a[..., 0] > a[..., 2] + 35)
_fd = np.asarray(Image.open(io.BytesIO(open(f'{mod}/fd0.d', 'rb').read()[open(f'{mod}/fd0.d', 'rb').read().find(b'IEND') + 8:])).convert('RGBA')).astype(np.float32)
_sk = np.where(_skin2(_fd[4]))[0]; FD_L, FD_R = int(_sk.min()), int(_sk.max())
for i in range(6):
    p = f'{mod}/fh{i}.d'; raw = open(p, 'rb').read(); k = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(np.float32); h, w = a.shape[:2]
    hairc = a[(a[..., 3] > 250) & ((a[..., :3] @ np.array([.3, .59, .11])) < 80)][:, :3]; hc = hairc.mean(0) if len(hairc) else np.array([40., 25., 28.])
    FDL, FDR = FD_L, FD_R
    if i == 1:                                                           # 여자 머리 조각: 바탕 얼굴은 fd1(37폭), 머리보다 3칸(HD 6) 왼쪽에서 시작한다
        _f1 = np.asarray(Image.open(io.BytesIO(open(f'{mod}/fd1.d', 'rb').read()[open(f'{mod}/fd1.d', 'rb').read().find(b'IEND') + 8:])).convert('RGBA')).astype(np.float32)
        _s1 = np.where(_skin2(_f1[4]))[0]; FDL, FDR = int(_s1.min()) - 6, int(_s1.max()) - 6
    N = 7; r0 = h - N; sk0 = np.where(_skin2(a[r0]))[0]
    if len(sk0) == 0: continue
    L0, R0 = int(sk0.min()), int(sk0.max())
    for r in range(r0, h):
        t = ((r - r0 + 1) / N) ** 0.8; L = int(round(L0 * (1 - t) + FDL * t)); R = int(round(R0 * (1 - t) + FDR * t))
        row = a[r]; skm = _skin2(row); tone = row[np.where(skm)[0][len(np.where(skm)[0]) // 2], :3].copy() if skm.any() else TOP
        for x in range(w):
            inside = L <= x <= R
            if inside and not skm[x]: row[x, :3] = tone; row[x, 3] = 255
            elif (not inside) and skm[x]: row[x, :3] = hc; row[x, 3] = 255
    b = io.BytesIO(); Image.fromarray(a.astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(p, 'wb').write(raw[:k] + b.getvalue())

# 마지막 정리: 머리 조각 아래 이마 피부를 '실제로 아래에 덮이는 표정 조각 맨 윗줄'의 평균색으로 맞춘다(선처럼 나뉘어 보이는 색 차이 제거)
def _row_mean(nm, r):
    raw = open(f'{mod}/{nm}.d', 'rb').read(); k = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(np.float32); m = _skin2(a[r])
    return a[r][m][:, :3].mean(0) if m.any() else None
_male = [_row_mean(f'ff{i}', 3) for i in (0, 1, 2, 6, 7, 8, 15, 16, 17)]; _fem = [_row_mean(f'ff{i}', 3) for i in (3, 4, 5, 9, 10, 11, 12, 13, 14)]
TM = np.mean([c for c in _male if c is not None], axis=0); TF = np.mean([c for c in _fem if c is not None], axis=0)
for i in range(6):
    p = f'{mod}/fh{i}.d'; raw = open(p, 'rb').read(); k = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(np.float32); h = a.shape[0]
    tgt = TF if i == 1 else TM; N = 12
    for r in range(h - N, h):
        t = min(1.0, (r - (h - N) + 1) / (N * 0.6)); m = _skin2(a[r])
        a[r, m, :3] = a[r, m, :3] * (1 - t) + tgt * t
    b = io.BytesIO(); Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(p, 'wb').write(raw[:k] + b.getvalue())

# 표정 조각 맨 윗 12줄의 피부를 같은 성별 평균색(위에서 정한 머리 조각 아랫줄 색)으로 시작해 서서히 원래 색으로 풀어, 경계의 색 단차를 없앤다
for i in range(18):
    p = f'{mod}/ff{i}.d'; raw = open(p, 'rb').read(); k = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(np.float32)
    tgt = TF if i in (3, 4, 5, 9, 10, 11, 12, 13, 14) else TM
    for r in range(0, 14):
        t = 1.0 - r / 14.0; m = _skin2(a[r]); a[r, m, :3] = a[r, m, :3] * (1 - t) + tgt * t
    b = io.BytesIO(); Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(p, 'wb').write(raw[:k] + b.getvalue())
for i in range(3):
    p = f'{mod}/fd{i}.d'; raw = open(p, 'rb').read(); k = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(np.float32); tgt = TF if i == 1 else TM
    for r in range(0, 14):
        t = 1.0 - r / 14.0; m = _skin2(a[r]); a[r, m, :3] = a[r, m, :3] * (1 - t) + tgt * t
    b = io.BytesIO(); Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(p, 'wb').write(raw[:k] + b.getvalue())

# 이마 음영: 머리카락 바로 아래는 그늘, 이마 가운데는 살짝 밝게, 양옆(관자놀이)은 어둡게 — 얼굴처럼 입체감을 주되 맨 아랫줄은 얼굴 윗줄과 같은 색으로 끝낸다
from scipy import ndimage as _ndi
for i in range(6):
    p = f'{mod}/fh{i}.d'; raw = open(p, 'rb').read(); k = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(np.float32); h, w = a.shape[:2]
    sk = _skin2(a)
    if sk.sum() < 30: continue
    dist = _ndi.distance_transform_edt(sk); ys, xs = np.where(sk); cx = (xs.min() + xs.max()) / 2.0; hw = max(1.0, (xs.max() - xs.min()) / 2.0); cy = ys.min() + (ys.max() - ys.min()) * 0.45
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    shadow = 0.16 * np.exp(-dist / 5.0)                                         # 머리카락 가장자리 그늘
    side = 0.10 * np.clip((np.abs(xx - cx) / hw - 0.55) / 0.45, 0, 1) ** 1.5     # 관자놀이 쪽 어둡게
    light = 0.07 * np.exp(-(((xx - cx) / (hw * 0.55)) ** 2 + ((yy - cy) / (max(4.0, (ys.max() - ys.min()) * 0.35)) ** 2)))   # 이마 중앙 하이라이트
    fade = np.clip((h - 1 - yy) / 8.0, 0, 1)                                      # 아래로 갈수록 효과 0
    f = (1 - shadow - side + light) * fade + (1 - fade)
    a[sk, :3] = np.clip(a[sk, :3] * f[sk][:, None], 0, 255)
    b = io.BytesIO(); Image.fromarray(a.astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(p, 'wb').write(raw[:k] + b.getvalue())

# 목·옷깃·잔머리 정리
def _load(nm):
    raw = open(f'{mod}/{nm}.d', 'rb').read(); k = raw.find(b'IEND') + 8; return raw, k, np.asarray(Image.open(io.BytesIO(raw[k:])).convert('RGBA')).astype(np.float32)
def _save(nm, raw, k, a):
    b = io.BytesIO(); Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(b, format='PNG', optimize=True); open(f'{mod}/{nm}.d', 'wb').write(raw[:k] + b.getvalue())
for nm in ('fd0', 'fd2'):                                     # 남자 바탕 얼굴 아래쪽: 푸른 옷깃 조각을 지우고(상체 그림이 대신 보인다), 턱 밑에 은은한 그늘
    raw, k, a = _load(nm); h = a.shape[0]
    for r in range(h - 16, h):
        bl = (a[r, :, 3] > 0) & (a[r, :, 2] >= a[r, :, 0] - 5) & ((a[r, :, :3] @ np.array([.3, .59, .11])) < 170); a[r, bl, 3] = 0
    for r in range(h - 8, h):
        t = (r - (h - 8) + 1) / 8.0 * 0.14; m = _skin2(a[r]); a[r, m, :3] *= (1 - t)
    _save(nm, raw, k, a)
raw, k, a = _load('fd1'); h = a.shape[0]
_save('fd1', raw, k, a); chin = TF * 0.93                       # 목 맨 윗줄은 턱 그늘색
raw, k, a = _load('fb1')                                       # 여자 상체(사용자 GPT 그림): 모양은 그대로 두고 피부 톤만 얼굴(fd1 목 색)에 맞춘다
_fdn = _load('fd1')[2]; _nm = _skin2(_fdn[-6]); NECKC = _fdn[-6][_nm][:, :3].mean(0) if _nm.any() else TF
_sk = _skin2(a) & (a[..., 2] < a[..., 1] - 5); cur = a[_sk][:, :3].mean(0); delta = NECKC - a[:12][_sk[:12]][:, :3].mean(0) if _sk[:12].any() else NECKC - cur
for r in range(a.shape[0]):
    t = max(0.0, 1.0 - r / 28.0); m = _sk[r]; a[r, m, :3] += delta * t        # 목 윗부분은 얼굴색으로, 아래로 갈수록 원래 색
_save('fb1', raw, k, a)
# 여자 머리 조각(fh6): 앞머리 몇 가닥만 — 정수리·앞머리 윗부분만 남기고 옆·아랫머리는 바탕 얼굴(fd1)의 머리가 담당
raw, k, a = _load('fh6'); h6, w6 = a.shape[:2]
ys_, xs_ = np.where(_skin2(a)); top = int(ys_.min()) if len(ys_) else int(h6 * 0.4)
keep = np.zeros((h6, w6), bool); keep[:top + 4] = True
a[~keep, 3] = 0
for r in range(top - 2, top + 4):
    a[r, :, 3] *= max(0.0, 1.0 - (r - (top - 2)) / 6.0) ** 0.8
_save('fh6', raw, k, a)


# 목 옆 머리타래(상체 그림 위쪽의 검은 조각)를 바탕 얼굴의 머리색으로 맞춰 이어 보이게 한다
raw, k, a = _load('fb1'); _f = _load('fd1')[2]; _hm = (_f[..., 3] > 250) & ((_f[..., :3] @ np.array([.3, .59, .11])) < 120) & (~_skin2(_f))
hc = _f[_hm][:, :3].mean(0); lum_h = (_f[_hm][:, :3] @ np.array([.3, .59, .11])).mean()
dm = (a[:14, :, 3] > 0) & ((a[:14, :, :3] @ np.array([.3, .59, .11])) < 45)
sh = (hc.mean() / max(1.0, 1.0)) ; a[:14][dm, :3] = hc[None, :]
_save('fb1', raw, k, a)
