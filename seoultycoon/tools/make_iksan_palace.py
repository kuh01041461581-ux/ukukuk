"""왕궁리: 서울 궁궐 지형 조각(d84·d93·d94·d95·da4)을 원본 위에 덧칠 — 조선 궁궐(붉은 담·청록 기와)을 백제 왕궁(황토·석재 담장, 회청색 기와)으로 바꾸고,
d84 안뜰의 연못을 잔디로 덮은 뒤 왕궁리 오층석탑을 세운다.   → work/pak_mod/{d84,d93,d94,d95,da4}.png   사용: python3 -I tools/make_iksan_palace.py [미리보기.png]"""
import os, sys
import numpy as np
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = root + '/work/pak_mod'
def ensure():
    if not os.path.exists('/tmp/pk/d84.d'):
        import subprocess; subprocess.run([sys.executable, '-I', root + '/tools/extract_pak.py', root + '/work/st2/data1.pak', '/tmp/pk'], capture_output=True)
MAP = {  # 청록 기와 → 회청색 기와 / 붉은 담 → 황토·석재 담
 (10, 72, 88): (54, 56, 66), (46, 110, 87): (88, 92, 104), (15, 108, 87): (76, 80, 92), (34, 160, 162): (136, 142, 156), (105, 169, 171): (164, 170, 182),
 (104, 152, 168): (148, 154, 168), (84, 142, 168): (122, 130, 146), (139, 168, 170): (170, 174, 186), (120, 152, 168): (136, 142, 158), (136, 152, 168): (150, 156, 170),
 (152, 44, 23): (178, 150, 116), (107, 44, 13): (126, 102, 78), (198, 95, 95): (214, 190, 158), (209, 182, 168): (222, 204, 176)}
def recolor(im):
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in MAP: px[x, y] = MAP[(r, g, b)] + (a,)
    return im
def pond_to_lawn(im, box):
    px = im.load(); x0, y0, x1, y1 = box
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a and b > 150 and r < 140: px[x, y] = ((104, 152, 8) if (x + y) % 5 else (120, 160, 14)) + (255,)
            elif a and (r, g, b) == (105, 169, 171) and False: pass
def pagoda(im, cx, base_y):
    """왕궁리 오층석탑(작은 도트): 낮은 이중 기단 + 키 큰 1층 몸돌 + 위로 좁아지는 4개 지붕돌·몸돌 + 상륜. cx=가운데 x, base_y=기단 바닥 y."""
    px = im.load(); W, H = im.size
    L, M, D, T = (226, 218, 192), (196, 186, 160), (140, 130, 108), (246, 240, 220)
    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H: px[x, y] = c + (255,)
    def block(x0, x1, y0, y1, split=None):                      # 정면(왼쪽 밝음·오른쪽 어두움) 직육면체 면
        mid = (x0 + x1) // 2 if split is None else split
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1): put(x, y, L if x <= mid else M)
        for y in range(y0, y1 + 1): put(x1, y, D)
        for x in range(x0, x1 + 1): put(x, y1, D)
    def roof(xc, w, y):                                         # 얇은 지붕돌 한 켜: 위는 밝고 아래 그림자
        for x in range(xc - w, xc + w + 1): put(x, y, T if x <= xc else L)
        for x in range(xc - w + 1, xc + w): put(x, y + 1, M if x <= xc else D)
    # 기단(넓고 낮은 이중)
    block(cx - 10, cx + 10, base_y - 3, base_y - 1); block(cx - 8, cx + 8, base_y - 6, base_y - 4)
    y = base_y - 7
    # 1층 몸돌(크다) + 모서리 기둥 선
    h = 9; block(cx - 5, cx + 5, y - h + 1, y)
    for yy in range(y - h + 1, y + 1): put(cx - 5, yy, M); put(cx + 5, yy, D)
    for xx in (cx - 2, cx + 1): put(xx, y - 3, D)               # 문 암시
    y -= h
    for (w, bh) in ((9, 5), (7, 4), (6, 3), (5, 3)):
        roof(cx, w, y - 1); y -= 2
        for k in range(2): roof(cx, w - 1 - k, y); y -= 1 if k else 0
        block(cx - (w - 3), cx + (w - 3), y - bh + 1, y); y -= bh
    roof(cx, 4, y - 1); y -= 2
    for yy in range(y - 4, y): put(cx, yy, L); put(cx + 1, yy, D)            # 상륜
    put(cx, y - 5, T)
    return im
def build():
    ensure(); res = {}
    for n in ('d84', 'd93', 'd94', 'd95', 'da4'):
        im = recolor(Image.open(f'/tmp/pk/{n}.d').convert('RGBA'))
        if n == 'd84':
            pond_to_lawn(im, (8, 20, 33, 33)); pagoda(im, 41, 40)
        res[n] = im
    return res
if __name__ == '__main__':
    res = build()
    for n, im in res.items(): im.save(f'{out}/{n}.png')
    if len(sys.argv) > 1:
        w = Image.new('RGBA', (len(res) * 140, 70), (40, 40, 50, 255))
        for i, (n, im) in enumerate(res.items()):
            o = Image.open(f'/tmp/pk/{n}.d').convert('RGBA'); w.alpha_composite(o, (i * 140, 3)); w.alpha_composite(im, (i * 140 + 68, 3))
        w.resize((w.width * 3, w.height * 3), Image.NEAREST).save(sys.argv[1])
    print('왕궁리 구역:', ', '.join(res))
