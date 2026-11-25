"""PC 판 여자 시장 상의: 게임 원래 상체 스프라이트(work/st2/fb1.d)를 그대로 쓰고 2배 층은 Scale2x 로(또렷한 원래 도트). 얼굴·머리는 새 베이스 그대로.  → st2android/mod/fb1.d"""
import os, io, sys
import numpy as np
from PIL import Image
here = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, here); root = os.path.dirname(here)
import pc_hd
o = Image.open(root + '/work/st2/fb1.d').convert('RGBA'); a = np.asarray(o).copy()
# 위쪽 목 둘레의 어두운 화소(원래 쪽진 머리·그늘)는 목 살색으로: 새 머리(단발)와 맞지 않아 검은 홈처럼 보인다
sk = (a[..., 3] > 0) & (a[..., 0] > 180) & (a[..., 0] > a[..., 2] + 40); skc = np.median(a[sk][:, :3], axis=0) if sk.any() else np.array([240, 190, 150])
for y in range(0, 7):
    for x in range(a.shape[1]):
        if a[y, x, 3] > 0 and a[y, x, :3].mean() < 80:
            xs = np.where(sk[y])[0]
            if len(xs) and xs.min() - 6 <= x <= xs.max() + 6: a[y, x, :3] = (skc * (0.82 + 0.03 * y)).astype(np.uint8)
hd = pc_hd.scale2x(a); b1, b2 = io.BytesIO(), io.BytesIO(); Image.fromarray(a, 'RGBA').save(b1, format='PNG'); Image.fromarray(hd, 'RGBA').save(b2, format='PNG')
open(root + '/st2android/mod/fb1.d', 'wb').write(b1.getvalue() + b2.getvalue()); print('fb1: 원래 상체 스프라이트')
