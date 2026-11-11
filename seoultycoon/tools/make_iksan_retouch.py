"""1번 방식: 원본 건물 시트의 같은 화소 배치를 그대로 두고 외국/서울 상표 글자만 지우고 익산 상호 글자를 덧쓴다 + 색 보정.
조각 배치를 건드리지 않으므로 게임 안에서 깨지지 않는다.  → work/pak_mod/b{3,9,15,19,44}.png    사용: python3 -I tools/make_iksan_retouch.py [미리보기.png]"""
import os, sys
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import strips as R
out = root + '/work/pak_mod'
G3 = {  # 3x5 화소 글꼴
 'A': '010101111101101', 'C': '011100100100011', 'E': '111100110100111', 'H': '101101111101101', 'I': '111010010010111',
 'L': '100100100100111', 'M': '101111111101101', 'O': '010101101101010', 'R': '110101110101101', 'S': '011100010001110', 'T': '111010010010010', 'K': '101110100110101', 'D': '110101101101110', 'N': '110101101101101', 'V': '101101101101010', 'U': '101101101101111', 'P': '110101110100100', 'G': '011100101101011', 'B': '110101110101110', 'Y': '101101010010010', 'W': '101101111111101'}
def text(img, x0, y0, s, color, slope=0.0, gap=1, sx=1, sy=1):
    px = img.load(); x = x0; col = 0
    for ch in s:
        g = G3.get(ch)
        if g is None: x += 4; col += 4; continue
        for r in range(5):
            for c in range(3):
                if g[r * 3 + c] == '1':
                    for ax in range(sx):
                        for ay in range(sy):
                            xx = x + c * sx + ax; yy = y0 + r * sy + ay + int(round((xx - x0) * slope))
                            if 0 <= xx < img.width and 0 <= yy < img.height and px[xx, yy][3] > 0: px[xx, yy] = color + (255,)
        x += 3 * sx + gap
def clear(img, box, pred):
    px = img.load(); x0, y0, x1, y1 = box
    for y in range(y0, y1):
        for x in range(x0, x1):
            if x < img.width and y < img.height and px[x, y][3] > 0 and pred(*px[x, y][:3]): px[x, y] = (0, 0, 0, 0)
def erase(img, box, pred, maskf=None):
    """상자 안에서 pred(r,g,b)가 참인 화소(글자)를 주변 배경색으로 메운다."""
    px = img.load(); x0, y0, x1, y1 = box
    bad = {(x, y) for y in range(y0, y1) for x in range(x0, x1) if x < img.width and y < img.height and px[x, y][3] > 0 and pred(*px[x, y][:3]) and (maskf is None or maskf(x, y))}
    for _ in range(8):
        if not bad: break
        new = {}
        for (x, y) in list(bad):
            cs = [px[x + dx, y + dy] for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx or dy) and 0 <= x + dx < img.width and 0 <= y + dy < img.height and (x + dx, y + dy) not in bad and px[x + dx, y + dy][3] > 0]
            if cs: cs.sort(key=lambda c: c[:3]); new[(x, y)] = cs[len(cs) // 2]
        for (x, y), c in new.items(): px[x, y] = c; bad.discard((x, y))
def swap_red_blue(img, box=None):
    px = img.load(); x0, y0, x1, y1 = box or (0, 0, img.width, img.height)
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a and r >= 150 and g < 130 and b < 130: px[x, y] = (b, g, r, a)

def b15():                                                                   # 넥슨모바일 → 테슬라 모터스
    im = R.Comp(15).S.copy() if False else Image.open('/tmp/pk/b15.d').convert('RGBA')
    erase(im, (36, 1, 63, 19), lambda r, g, b: b == 171 and g >= 219)
    text(im, 38, 3, 'TESLA', (255, 255, 171), 0.27); text(im, 38, 11, 'MOTORS', (255, 219, 171), 0.27); return im
def b19():                                                                   # 메가박스 → 롯데시네마
    im = Image.open('/tmp/pk/b19.d').convert('RGBA')
    erase(im, (26, 0, 58, 13), lambda r, g, b: b == 171 and r >= 146 and g >= 146 or (r, g, b) in ((0, 36, 171), (36, 36, 85)) and False)
    text(im, 28, 2, 'LOTTE', (255, 255, 171), 0.3, gap=0, sx=2, sy=1); return im
def b9():                                                                    # 카트라이더 → 승마(HORSE)
    im = Image.open('/tmp/pk/b9.d').convert('RGBA')
    erase(im, (38, 0, 86, 34), lambda r, g, b: (r, g, b) in ((255, 219, 0), (109, 73, 0), (73, 181, 0), (36, 73, 0), (255, 182, 0), (182, 109, 0), (73, 182, 1), (36, 109, 0)))
    text(im, 41, 2, 'HORSE', (255, 219, 0), 0.62, gap=2, sx=2, sy=2); return im
def b3():                                                                    # 테크노마트 TM → 코스트코(CO)
    im = Image.open('/tmp/pk/b3.d').convert('RGBA')
    erase(im, (66, 0, 82, 11), lambda r, g, b: (r, g, b) in ((255, 0, 85), (0, 182, 255)))
    erase(im, (33, 0, 45, 16), lambda r, g, b: (r, g, b) in ((255, 0, 85), (0, 182, 255), (182, 0, 85), (0, 146, 255), (146, 36, 0), (219, 255, 255)))
    erase(im, (19, 8, 28, 15), lambda r, g, b: (r, g, b) in ((255, 0, 85), (0, 146, 255)))
    erase(im, (30, 22, 52, 34), lambda r, g, b: (r, g, b) in ((238, 51, 62), (255, 109, 171)))
    text(im, 68, 2, 'CO', (255, 0, 85), 0.3); text(im, 34, 1, 'CO', (255, 0, 85), 0.3); text(im, 35, 10, 'CO', (0, 182, 255), 0.3); text(im, 21, 9, 'CO', (255, 0, 85), 0.2)
    text(im, 33, 24, 'CO', (238, 51, 62), 0.3); return im
def b44():                                                                   # 왕도날드(빨강) → 하림푸드(파랑)
    im = Image.open('/tmp/pk/b44.d').convert('RGBA'); swap_red_blue(im); return im

def b32():                                                                  # 종합운동장 COREA → IKSAN
    im = Image.open('/tmp/pk/b32.d').convert('RGBA')
    clear(im, (0, 0, 19, 6), lambda r, g, b: r >= 180 and g < 80)
    px = im.load(); text(_Transparent(im), 1, 0, 'IKSAN', (219, 0, 0)); return im
class _Transparent:
    """투명 칸에도 글자를 쓸 수 있게 알파 검사를 끈 래퍼"""
    def __init__(self, im): self.im = im; self.width = im.width; self.height = im.height; self._px = im.load()
    def load(self): return _Px(self._px)
class _Px:
    def __init__(self, px): self.px = px
    def __getitem__(self, k): return (0, 0, 0, 255)
    def __setitem__(self, k, v): self.px[k] = v

def b45():                                                                  # 베니건스(BENNIGANS 네온) → 마룡이식당(MARYONG)
    im = Image.open('/tmp/pk/b45.d').convert('RGBA')
    erase(im, (28, 7, 64, 41), lambda r, g, b: r >= 140 and g >= 200 and b >= 200)
    text(im, 30, 36, 'MARYONG', (255, 255, 255), -0.85, gap=1, sx=1, sy=2); return im

def b8():                                                                   # 방송국 Onmedianet 간판 → IKSAN TV
    im = Image.open('/tmp/pk/b8.d').convert('RGBA')
    band = lambda x, y: abs((y - (50 - (x - 25) * 0.64))) <= 6 and 24 <= x <= 60                  # (25,50)→(58,29) 사선 띠
    erase(im, (22, 24, 62, 54), lambda r, g, b: (r, g, b) in ((219, 255, 171), (146, 219, 85), (255, 255, 255), (36, 182, 171)), band); return im

if __name__ == '__main__':
    R.ensure_pk(); items = {'b32': b32, 'b45': b45, 'b8': b8}; ims = {}
    for n, fn in items.items(): ims[n] = fn(); ims[n].save(f'{out}/{n}.png')
    if len(sys.argv) > 1:
        W = sum(i.width * 5 + 10 for i in ims.values()); H = max(i.height * 5 for i in ims.values()); w = Image.new('RGBA', (W, H), (60, 60, 70, 255)); x = 0
        for i in ims.values():
            bg = Image.new('RGBA', i.size, (150, 70, 160, 255)); bg.alpha_composite(i); w.paste(bg.resize((i.width * 5, i.height * 5), Image.NEAREST), (x, 0)); x += i.width * 5 + 10
        w.save(sys.argv[1])
    print('retouch 완료:', ', '.join(items))
