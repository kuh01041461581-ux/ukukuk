# -*- coding: utf-8 -*-
"""suite.ico 생성 (Pillow 필요).  python3 suite/app/make_icon.py"""
import pathlib
from PIL import Image, ImageDraw

SS = 4                      # 슈퍼샘플링 배율(가장자리를 부드럽게)
S = 256 * SS
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

# 인디고 → 바이올렛 대각선 그라데이션
grad = Image.new("RGBA", (S, S))
px = grad.load()
c1, c2 = (91, 109, 248), (117, 87, 245)
for y in range(S):
    for x in range(S):
        t = (x + y) / (2 * S - 2)
        px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(c1, c2)) + (255,)

mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.23), fill=255)
img.paste(grad, (0, 0), mask)

# 흰색 2×2 타일 (대각선은 불투명, 나머지는 반투명)
d = ImageDraw.Draw(img, "RGBA")
m, g, r = int(S * 0.23), int(S * 0.07), int(S * 0.07)
w = (S - 2 * m - g) // 2
for i, (cx, cy, a) in enumerate([(0, 0, 255), (1, 0, 175), (0, 1, 175), (1, 1, 255)]):
    x0, y0 = m + cx * (w + g), m + cy * (w + g)
    d.rounded_rectangle((x0, y0, x0 + w, y0 + w), radius=r, fill=(255, 255, 255, a))

img = img.resize((256, 256), Image.LANCZOS)
out = pathlib.Path(__file__).with_name("suite.ico")
img.save(out, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)])
print(out, out.stat().st_size, "bytes")
