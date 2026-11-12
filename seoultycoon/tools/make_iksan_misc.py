"""연출·미니게임용 그림에 박힌 '서울' 글자를 '익산'으로: 결말 문구(e_t.d: 서울 시장 재선/사망/구속/파면), 비행선(i1.d: 서울타이쿤). → st2android/mod/"""
import os
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); src = root + '/work/st2'; mod = root + '/st2android/mod'
def stamp(px, x, y, rows, col):
    for r, line in enumerate(rows):
        for c, ch in enumerate(line):
            if ch == '#': px[x + c, y + r] = col
IK = ['.##..#', '#..#.#', '#..#.#', '#..#.#', '.##..#', '.....#', '######', '.....#', '.....#', '.....#']
SAN = ['.#..#.', '.#..#.', '.#..##', '#.#.#.', '#.#.#.', '....#.', '#.....', '#.....', '#.....', '#####.']
im = Image.open(src + '/e_t.d').convert('RGBA'); px = im.load(); BG, GR, WH = (0, 33, 82, 255), (99, 99, 115, 255), (255, 255, 255, 255)
for r in (2, 3, 4, 5):
    y0 = 13 * r
    for y in range(y0 + 1, y0 + 13):
        for x in range(0, 17): px[x, y] = BG
    stamp(px, 2, y0 + 2, IK, WH); stamp(px, 9, y0 + 2, SAN, WH)
    for y in range(y0 + 1, y0 + 13):                                   # 회색 테두리
        for x in range(0, 17):
            if px[x, y] == BG and any(0 <= x + dx < 54 and px[x + dx, y + dy] == WH for dx in (-1, 0, 1) for dy in (-1, 0, 1)): px[x, y] = GR
im.save(mod + '/e_t.d', format='PNG')
im = Image.open(src + '/i1.d').convert('RGBA'); px = im.load(); RED, CR = (219, 36, 0, 255), (255, 219, 171, 255)
for y in range(5, 14):
    for x in range(3, 17): px[x, y] = RED
stamp(px, 4, 6, ['.#..#', '#.#.#', '.#..#', '....#', '#####', '....#', '....#'], CR); stamp(px, 10, 6, ['.#.#.', '.#.##', '#.##.', '...#.', '#....', '#....', '####.'], CR)
im.save(mod + '/i1.d', format='PNG')
print('e_t.d, i1.d 익산화')
