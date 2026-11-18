"""여자 표정 조각(ff)의 좌우 가장자리를 투명하게 서서히 흐려, 아래 바탕 얼굴(fd1)과 자연스럽게 이어지게 한다(조립 이음새 제거).
저해상도 층은 한 칸, 2배 층은 네 칸에 걸쳐 알파를 낮춘다.  → st2android/mod/ff{3,4,5,9..14}.d"""
import os, io
import numpy as np
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); mod = root + '/st2android/mod'
def feather(a, ramp):
    a = a.copy(); h, w = a.shape[:2]; n = len(ramp)
    for i, r in enumerate(ramp):
        a[:, i, 3] = (a[:, i, 3] * r).astype(np.uint8); a[:, w - 1 - i, 3] = (a[:, w - 1 - i, 3] * r).astype(np.uint8)
    return a
for i in (3, 4, 5, 9, 10, 11, 12, 13, 14):
    p = f'{mod}/ff{i}.d'; raw = open(p, 'rb').read(); e1 = raw.find(b'IEND') + 8; e2 = raw.find(b'IEND', e1) + 8
    lo = np.asarray(Image.open(io.BytesIO(raw[:e1])).convert('RGBA')); hd = np.asarray(Image.open(io.BytesIO(raw[e1:e2])).convert('RGBA'))
    b1, b2 = io.BytesIO(), io.BytesIO()
    Image.fromarray(feather(lo, (0.55,)), 'RGBA').save(b1, format='PNG'); Image.fromarray(feather(hd, (0.2, 0.45, 0.7, 0.88)), 'RGBA').save(b2, format='PNG')
    open(p, 'wb').write(b1.getvalue() + b2.getvalue() + raw[e2:])
print('여자 표정 가장자리 흐림')
