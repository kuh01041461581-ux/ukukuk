"""PC 판 전용 하단 키 안내 그림(53.m 의 PC 판): 'C:정보  V:지도  0:나가기'. 안드로이드 판은 원래 그림(*:INFO #:MAP 0:EXIT)을 그대로 쓴다.
→ st2android/mod_pc/53.m  (PC 빌드가 jar 에 st2mod 보다 먼저 읽히는 이름으로 넣는다)"""
import os
from PIL import Image, ImageDraw, ImageFont
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = Image.open(root + '/work/st2/53.m').convert('RGBA'); out = Image.new('RGBA', src.size, (0, 0, 0, 0))
BG, FG = (0, 0, 85, 255), (109, 182, 255, 255)
# 원본 글씨를 그대로 쓰되 맨 앞 기호만 바꾼다: '*'→'C', '#'→'V' (원본 5x7 도트 글꼴과 같은 모양으로 직접 찍는다)
GLY = {'C': ['.###.', '#...#', '#....', '#....', '#....', '#...#', '.###.'], 'V': ['#...#', '#...#', '#...#', '#...#', '.#.#.', '.#.#.', '..#..']}
px = src.load(); o = out.load()
for y in range(src.height):
    for x in range(src.width): o[x, y] = px[x, y]
for r, ch in ((0, 'C'), (1, 'V')):
    y0 = (0, 7)[r]; h = (7, 8)[r]
    for y in range(y0, y0 + h):
        for x in range(0, 6): o[x, y] = (0, 0, 0, 0)                            # 원본 기호 칸(왼쪽 6칸)을 비운다
    g = GLY[ch]
    for yy, row in enumerate(g):
        for xx, c in enumerate(row):
            if c == '#': o[xx, y0 + yy + (0 if r == 0 else 1)] = FG
os.makedirs(root + '/st2android/mod_pc', exist_ok=True); out.save(root + '/st2android/mod_pc/53.m', format='PNG')
bg = Image.new('RGBA', out.size, (40, 60, 120, 255)); bg.alpha_composite(out); bg.resize((out.width * 8, out.height * 8), Image.NEAREST).save('/tmp/claude-0/prev/m53pc.png'); print('PC 키 안내 그림 완료')
