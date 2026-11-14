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
n = 0
for nm in [f'ff{i}' for i in range(18)] + [f'fh{i}' for i in range(7)]:
    mp = f'{mod}/{nm}.d'; sp = f'{src}/{nm}.d'
    raw = open(mp if os.path.exists(mp) else sp, 'rb').read(); i = raw.find(b'IEND') + 8; base = Image.open(io.BytesIO(raw[:i])).convert('RGBA')
    hd = VC.vec_hd(base)
    a = np.asarray(hd).astype(np.float32); al = a[..., 3:4] > 0
    lum = a[..., :3] @ np.array([0.30, 0.59, 0.11], np.float32); skin = (a[..., 0] > 150) & (a[..., 0] > a[..., 2] + 30)           # 피부: 아주 약한 밝기 보정(촉촉한 느낌)
    a[..., :3] = np.where(skin[..., None], np.clip(a[..., :3] * 1.03 + 2, 0, 255), a[..., :3])
    if False and nm.startswith('ff'): a = eye_shine(a)      # 눈이 너무 작아 효과가 미미하고 잡티처럼 보여 꺼 둠
    hd = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA')
    b = io.BytesIO(); hd.save(b, format='PNG', optimize=True); open(mp, 'wb').write(raw[:i] + b.getvalue()); n += 1
print('시장 얼굴·머리 벡터풍 2배 층', n, '장')
