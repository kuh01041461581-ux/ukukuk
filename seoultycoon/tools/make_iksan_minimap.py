"""지도 팝업 미니맵(mn.d, 104x94)의 윗부분(서울 윤곽+한강)을 익산시 윤곽 + 만경강으로 다시 그리고,
기존과 같은 십자 도로로 4개 권역(북부·동부·서부·남부)을 나눈다. 아래쪽 범례 글자(y>=70)와 가운데 시청 아이콘은 그대로 둔다.
윤곽은 사용자가 찍어 준 익산시 관광 지도(남북으로 긴 모양, 서쪽 만경강 경계)를 따라 손으로 단순화했다.
→ st2android/mod/mn.d   사용: python3 -I tools/make_iksan_minimap.py [미리보기.png]"""
import os, sys
from PIL import Image, ImageDraw
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
orig = Image.open(root + '/work/st2/mn.d').convert('RGBA')
W, H = orig.size
img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
img.paste(orig.crop((0, 70, W, H)), (0, 70))                    # 범례 글자·돋보기·N 표시 유지
icon = orig.crop((47, 22, 65, 37))                              # 가운데 시청 아이콘
GREEN, LIGHT, RIVER, RIVER_D = (74, 148, 0, 255), (186, 218, 174, 255), (26, 144, 232, 255), (0, 80, 176, 255)
# 익산시 윤곽(관광 지도 좌표 → 미니맵 좌표)
pts = [(905,98),(1000,88),(1060,125),(1105,120),(1118,160),(1190,115),(1200,175),(1260,180),(1300,230),(1330,300),(1380,330),(1440,370),(1465,390),
       (1420,420),(1400,470),(1385,560),(1400,640),(1420,700),(1350,760),(1250,800),(1180,845),(1140,905),(1050,905),(960,870),(860,790),(930,740),(930,700),
       (880,650),(860,600),(840,540),(835,470),(840,400),(870,340),(880,270),(860,230),(870,170)]
def mp(p): return (round(17 + (p[0] - 835) / 630 * 70), round(2 + (p[1] - 85) / 820 * 66))
poly = [mp(p) for p in pts]
mask = Image.new('L', (W, H), 0); ImageDraw.Draw(mask).polygon(poly, fill=255)
base = Image.new('RGBA', (W, H), GREEN); img.paste(base, (0, 0), mask.crop((0, 0, W, H)).point(lambda v: v if True else 0))
d = ImageDraw.Draw(img)
# 십자 도로(4개 권역 경계): 중심(55,40)
cx, cy = 55, 40
road = Image.new('L', (W, H), 0); rd = ImageDraw.Draw(road)
rd.line([(0, cy + 0.5 * cx), (W, cy - 0.5 * (W - cx))], fill=255, width=1)      # "/" 방향
rd.line([(0, cy - 0.5 * cx), (W, cy + 0.5 * (W - cx))], fill=255, width=1)      # "\\" 방향
road = Image.composite(road, Image.new('L', (W, H), 0), mask)
img.paste(Image.new('RGBA', (W, H), LIGHT), (0, 0), road)
# 만경강: 서쪽 경계를 따라 굽이쳐 흐르는 강(윤곽의 왼쪽 가장자리 안쪽)
west = [mp(p) for p in [(870,170),(860,230),(880,270),(870,340),(840,400),(835,470),(840,540),(860,600),(880,650),(930,700)]]
for (x0, y0), (x1, y1) in zip(west, west[1:]):
    d.line([(x0 + 2, y0), (x1 + 2, y1)], fill=RIVER_D, width=2); d.line([(x0 + 2, y0), (x1 + 2, y1)], fill=RIVER, width=1)
d.line([mp((1000, 88)), mp((905, 98)), (west[0][0] + 2, west[0][1])], fill=RIVER, width=1)   # 북서쪽 하구 쪽
img.paste(icon, (47, 22), icon)
img.save(root + '/st2android/mod/mn.d', format='PNG')
if len(sys.argv) > 1:
    bg = Image.new('RGBA', (W, H), (20, 20, 20, 255)); bg.alpha_composite(img); bg.resize((W * 6, H * 6), Image.NEAREST).save(sys.argv[1])
print('미니맵 → mn.d')
