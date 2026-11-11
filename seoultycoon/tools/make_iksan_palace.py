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
def pagoda(im, cx, base_y, scale=0.62):
    """왕궁리 오층석탑: 8배 해상도로 기단·키 큰 1층 몸돌·켜켜이 쌓인 얇은 지붕돌 5개·상륜을 그려 줄여 붙인다(cx=가운데 x, base_y=기단 바닥 y)."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import make_iksan_landmarks as LM
    from make_iksan_buildings import Sc, hx
    s = Sc(78, 12); G = s.G; rnd = __import__('random').Random(4); cx_ = cy_ = 0.5
    s.shadow([G(0.30, 0.70), G(0.72, 0.70), G(0.98, 0.86), G(0.50, 0.90)], 90, 9)
    s.box(0.24, 0.76, 0.24, 0.76, 1.6, (hx('d8d0b8'), hx('9c9478')), (hx('9c9478'), hx('6a6450')), LM.STONE_T, 0)                 # 넓은 낮은 기단
    s.box(0.30, 0.70, 0.30, 0.70, 1.6, (hx('d0c8ae'), hx('948c70')), (hx('948c70'), hx('625c48')), LM.STONE_T, 1.6)
    z = 3.2
    s.box(0.35, 0.65, 0.35, 0.65, 9.0, (hx('d8ccb0'), hx('a89c80')), (hx('a89c80'), hx('6a604a')), LM.STONE_T, z)                    # 키 큰 1층 몸돌
    for k in range(1, 9): a = 0.35 + 0.30 * k / 9; s.line([G(a, 0.65, z), G(a, 0.65, z + 9.0)], hx('6a604a', 130), 2)
    LM.weathered(s, 0.35, 0.65, 0.65, z, 9.0, rnd, 40)
    z += 9.0
    for (w, h, rw, lay) in ((0.135, 3.0, 0.215, 5), (0.118, 3.0, 0.19, 5), (0.102, 2.8, 0.165, 5), (0.086, 2.6, 0.14, 5), (0.07, 2.4, 0.115, 4)):
        z = LM.layered_roof(s, cx_, cy_, rw, z, rnd, layers=lay, thick=0.62, taper=0.012)
        s.box(cx_ - w, cx_ + w, cy_ - w, cy_ + w, h, LM.STONE_L, LM.STONE_R, LM.STONE_T, z); LM.weathered(s, cx_ - w, cx_ + w, cy_ + w, z, h, rnd, 8); z += h
    z = LM.layered_roof(s, cx_, cy_, 0.095, z, rnd, layers=4, thick=0.55, taper=0.012)
    s.box(cx_ - 0.02, cx_ + 0.02, cy_ - 0.02, cy_ + 0.02, 1.8, LM.STONE_L, LM.STONE_R, LM.STONE_T, z)
    from PIL import ImageDraw
    d = ImageDraw.Draw(s.im); x, y = G(cx_, cy_, z + 2.6); d.ellipse([x - 7, y - 8, x + 7, y + 4], fill=hx('d8d0bc')); d.ellipse([x - 5, y - 7, x - 1, y - 2], fill=hx('f2ecdc'))
    F = s.finish(); bb = F.getbbox(); F = F.crop(bb)
    F = F.resize((max(1, int(F.width * scale)), max(1, int(F.height * scale))), Image.LANCZOS)
    # 알파를 이진으로(도트 느낌) 하고 기단 바닥 중앙을 (cx, base_y)에 맞춘다
    a = np.asarray(F).copy(); a[..., 3] = np.where(a[..., 3] > 110, 255, 0); F = Image.fromarray(a, 'RGBA')
    im.alpha_composite(F, (cx - F.width // 2, base_y - F.height + 2))
    px = im.load()
    for (gx, gy) in ((cx - 15, base_y + 1), (cx - 12, base_y + 3), (cx + 12, base_y + 3), (cx + 15, base_y + 1), (cx - 9, base_y + 5), (cx + 9, base_y + 5)):   # 건물터 초석
        for dx in (0, 1):
            if 0 <= gx + dx < im.width and 0 <= gy < im.height: px[gx + dx, gy] = (150, 146, 128, 255)
    return im
def build():
    ensure(); res = {}
    for n in ('d84', 'd93', 'd94', 'd95', 'da4'):
        im = recolor(Image.open(f'/tmp/pk/{n}.d').convert('RGBA'))
        if n == 'd84':
            pagoda(im, 47, 41)
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
