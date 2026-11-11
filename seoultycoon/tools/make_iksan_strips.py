"""조각(strip) 규칙에 맞춘 건물 시트 생성기.
게임은 건물 시트를 한 장으로 그리지 않고, work/st2/yx.d 의 사각형 목록(조각)을 건물 종류별로 정해진 화면 위치에 쌓아 붙인다.
→ 원하는 '완성 그림 F' 를 먼저 그리고, 조각마다 F 의 해당 영역을 시트의 조각 칸에 옮긴다. 조각 폭은 yx.d 를 고쳐 넓힐 수 있다.
b2(미륵석탑): 조각 좌표계 P (메인 조각 좌상단=(0,0)):  메인 (0,23,96,37)@(0,0)  바닥끝 (9,60,55,7)@(25,37)
   TL1 @(36,-23)  TL2 @(39,-46)  첨탑 @(47,-56)    → 사용: python3 -I tools/make_iksan_strips.py [미리보기.png]"""
import os, sys, random
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_iksan_landmarks as LM
from make_iksan_buildings import Sc, hx, W, S, out
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def tall_scene(Hc, yc, half_h=24):
    """높이 Hc 의 렌더 화면. 바닥 마름모는 게임 타일과 같은 2:1(반높이 24), 가장 넓은 줄이 yc."""
    s = Sc(Hc, 11)
    s.T = (48, yc - half_h); s.R = (95, yc); s.L = (1, yc)
    return s

# b2: 조각 정의 (시트 사각형, 화면 위치 P)
B2_STRIPS = {
    'main':  ((0, 23, 96, 37), (0, 0)),
    'bot':   ((9, 60, 55, 7), (25, 37)),
    'tl1':   ((0, 0, 40, 23), (36, -23)),
    'tl2':   ((40, 0, 30, 23), (39, -46)),
    'spire': ((70, 0, 6, 10), (47, -56)),
}
B2_OFFY = 56            # F 의 줄 r = P_y + 56

def compose_sheet(F, strips, offy, H, W_=96):
    sheet = Image.new('RGBA', (W_, H), (0, 0, 0, 0))
    for name, ((sx, sy, w, h), (px, py)) in strips.items():
        piece = F.crop((px, py + offy, px + w, py + offy + h)); sheet.alpha_composite(piece, (sx, sy))
    return sheet

def b2():
    Hc = 112; s = tall_scene(Hc, 85)
    s.T = (48, 61); s.R = (95, 85); s.L = (1, 85)
    F = LM.mireuksa(s)                                                       # 완성 그림(96x112)
    return F, compose_sheet(F, B2_STRIPS, B2_OFFY, 68)

def composite_preview(sheet, strips, offy, Hc=112):
    """게임이 하는 대로 조각을 쌓아 본 모습(검증용)"""
    c = Image.new('RGBA', (96, Hc), (0, 0, 0, 0))
    for name, ((sx, sy, w, h), (px, py)) in strips.items():
        c.alpha_composite(sheet.crop((sx, sy, sx + w, sy + h)), (px, py + offy))
    return c

def patch_yx(path_out):
    d = bytearray(open(root + '/work/st2/yx.d', 'rb').read())
    pat = bytes([0, 23, 96, 37, 50, 0, 18, 23, 71, 13, 2, 10, 0, 0, 24, 23, 25, 0, 24, 23]); i = d.find(pat); assert i >= 0
    new = bytes([0, 23, 96, 37, 40, 0, 30, 23, 70, 0, 6, 10, 0, 0, 40, 23, 95, 0, 1, 1])      # 변형 조각(25,0,24,23)은 비워 둔다
    d[i:i + len(pat)] = new
    open(path_out, 'wb').write(d)

if __name__ == '__main__':
    F, sheet = b2(); sheet.save(f'{out}/b2.png'); patch_yx(root + '/st2android/mod/yx.d')
    comp = composite_preview(sheet, B2_STRIPS, B2_OFFY)
    if len(sys.argv) > 1:
        bg = Image.new('RGBA', (96 * 2 + 140, 112), (150, 70, 160, 255)); bg.alpha_composite(F, (0, 0)); bg.alpha_composite(comp, (100, 0))
        sb = Image.new('RGBA', (96, 68), (150, 70, 160, 255)); sb.alpha_composite(sheet); bg.alpha_composite(sb, (200, 0))
        bg.resize((bg.width * 4, bg.height * 4), Image.NEAREST).save(sys.argv[1])
    print('b2 조각 시트 + yx.d 패치 완료')
