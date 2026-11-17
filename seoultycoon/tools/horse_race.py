"""승마대회 미니게임의 카트 그림(hhorse.d 20x26 부품 시트, hbgh.d 29x33 배경 큰 그림)을 말 색(밤색 몸·검은 발굽·흰 갈기/이마 점)으로 바꾼다.
부품 모양(조각 위치)은 게임이 조립하므로 그대로 두고 색만 말 느낌으로 바꾼다: 붉은 차체 → 밤색 말 몸통, 회색 바퀴 → 어두운 발굽·다리, 흰 체크 → 흰 무늬. 기수(파랑/빨강 헬멧)는 경주복색으로 유지.
→ st2android/mod/hhorse.d, hbgh.d"""
import os
import numpy as np
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); src = root + '/work/st2'; mod = root + '/st2android/mod'
RED = {(0xdb, 0x00, 0x00): (0x9a, 0x55, 0x28), (0x6d, 0x00, 0x00): (0x5c, 0x30, 0x16), (0x49, 0x00, 0x00): (0x3d, 0x20, 0x0f), (0xb6, 0x24, 0x00): (0xa8, 0x62, 0x30),
       (0xff, 0x24, 0x00): (0xc6, 0x7a, 0x38), (0xff, 0x6d, 0x55): (0xd9, 0x9c, 0x5e), (0xff, 0x92, 0x55): (0xe2, 0xb2, 0x78)}
GREY = {(0x92, 0x92, 0xab): (0x4a, 0x38, 0x28), (0x49, 0x49, 0x55): (0x26, 0x1a, 0x10)}
def recolor(name, rows=None, alpha_ok=True):
    im = Image.open(f'{src}/{name}.d').convert('RGBA'); a = np.asarray(im).copy(); h = a.shape[0]
    for y in range(h):
        if rows is not None and not (rows[0] <= y < rows[1]): continue
        for x in range(a.shape[1]):
            if a[y, x, 3] == 0: continue
            c = tuple(int(v) for v in a[y, x, :3])
            if c in RED: a[y, x, :3] = RED[c]
            elif c in GREY: a[y, x, :3] = GREY[c]
            elif c[0] > 120 and c[1] < 90 and c[2] < 90: a[y, x, :3] = (int(c[0] * 0.7), int(c[1] * 1.5 + 30), int(c[2] * 1.2 + 10))      # 남은 붉은 계열은 밤색으로
    Image.fromarray(a, 'RGBA').save(f'{mod}/{name}.d', format='PNG'); return a
recolor('hhorse', rows=(11, 26))      # 위 11줄은 기수(헬멧)라 유지
recolor('hbgh')
print('승마대회 그림 말 색으로')
