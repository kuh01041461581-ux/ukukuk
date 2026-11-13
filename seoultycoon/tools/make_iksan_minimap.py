"""지도 팝업 미니맵(mn.d, 104x94)의 윗부분(서울 윤곽+한강)을 익산시 윤곽 + 만경강으로 다시 그리고,
기존과 같은 십자 도로로 4개 권역(북부·동부·서부·남부)을 나눈다. 아래쪽 범례 글자(y>=70)와 가운데 시청 아이콘은 그대로 둔다.
윤곽은 사용자가 찍어 준 익산시 관광 지도(남북으로 긴 모양, 서쪽 만경강 경계)를 따라 손으로 단순화했다.
→ st2android/mod/mn.d   사용: python3 -I tools/make_iksan_minimap.py [미리보기.png]"""
import os, sys
from PIL import Image, ImageDraw, ImageChops
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
def mp(p): return (min(100, max(4, round(55 + (17 + (p[0] - 835) / 630 * 70 - 55) * 1.3))), round(2 + (p[1] - 85) / 820 * 66))      # 부지 표시(가로로 넓게 퍼짐)가 윤곽 안에 들도록 가로 1.3배
poly = [mp(p) for p in pts]
mask = Image.new('L', (W, H), 0); ImageDraw.Draw(mask).polygon(poly, fill=255)
# 게임이 찍는 부지 표시 31곳(화면 좌표를 미니맵 좌표로 환산)이 모두 땅 안에 들도록, 부지 둘레를 둥글게 덧붙인다
LOTS = [(246,158),(228,162),(266,162),(246,172),(224,174),(266,174),(242,184),(226,190),(182,210),(154,212),(304,212),(318,214),(172,218),(190,222),(292,222),(328,222),(156,226),(314,230),(170,234),(260,240),(184,242),(228,242),(306,244),(246,246),(320,248),(172,250),(262,254),(220,256),(242,262),(220,270),(234,276)]
md = ImageDraw.Draw(mask)
for hx, hy in LOTS:
    lx, ly = (hx - 120) / 2.3077, (hy - 143) / 2.1; md.ellipse([lx - 6, ly - 5, lx + 6, ly + 5], fill=255)
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

# ---- 2배 해상도 층: 같은 도형을 8배 크기로 그려 4배로 줄여(안티앨리어싱) 매끈한 해안선·강·도로를 만든다(범례 글자·아이콘은 정수배 확대) ----
import io
SS = 8; Z = SS * 2
mk = Image.new('L', (W * Z, H * Z), 0); ImageDraw.Draw(mk).polygon([(x * Z, y * Z) for x, y in poly], fill=255)
m2 = ImageDraw.Draw(mk)
for hx_, hy_ in LOTS:
    lx, ly = (hx_ - 120) / 2.3077, (hy_ - 143) / 2.1; m2.ellipse([(lx - 6) * Z, (ly - 5) * Z, (lx + 6) * Z, (ly + 5) * Z], fill=255)
big = Image.new('RGBA', (W * Z, H * Z), (0, 0, 0, 0)); big.paste(Image.new('RGBA', big.size, GREEN), (0, 0), mk)
rd2 = Image.new('L', big.size, 0); r2 = ImageDraw.Draw(rd2)
r2.line([(0, (cy + 0.5 * cx) * Z), (W * Z, (cy - 0.5 * (W - cx)) * Z)], fill=255, width=int(Z * 0.9)); r2.line([(0, (cy - 0.5 * cx) * Z), (W * Z, (cy + 0.5 * (W - cx)) * Z)], fill=255, width=int(Z * 0.9))
big.paste(Image.new('RGBA', big.size, LIGHT), (0, 0), ImageChops.multiply(rd2, mk))
bd = ImageDraw.Draw(big)
for (x0, y0), (x1, y1) in zip(west, west[1:]):
    bd.line([((x0 + 2) * Z, y0 * Z), ((x1 + 2) * Z, y1 * Z)], fill=RIVER_D, width=int(Z * 2)); bd.line([((x0 + 2) * Z, y0 * Z), ((x1 + 2) * Z, y1 * Z)], fill=RIVER, width=int(Z * 1.1))
bd.line([(a_ * Z, b_ * Z) for a_, b_ in [mp((1000, 88)), mp((905, 98)), (west[0][0] + 2, west[0][1])]], fill=RIVER, width=int(Z * 1.1))
hd = big.resize((W * 2, H * 2), Image.LANCZOS)
leg = orig.crop((0, 70, W, H)).resize((W * 2, (H - 70) * 2), Image.NEAREST); hd.paste(leg, (0, 140))
hd.alpha_composite(icon.resize((icon.width * 2, icon.height * 2), Image.NEAREST), (94, 44))
b_ = io.BytesIO(); img.save(b_, format='PNG'); b2_ = io.BytesIO(); hd.save(b2_, format='PNG')
open(root + '/st2android/mod/mn.d', 'wb').write(b_.getvalue() + b2_.getvalue())
if len(sys.argv) > 1:
    bg = Image.new('RGBA', (W, H), (20, 20, 20, 255)); bg.alpha_composite(img); bg.resize((W * 6, H * 6), Image.NEAREST).save(sys.argv[1])
print('미니맵 → mn.d')
