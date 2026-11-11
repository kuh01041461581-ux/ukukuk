# -*- coding: utf-8 -*-
"""실행 아이콘(assets/app.ico, app_256.png)을 Pillow 로 그린다. 다시 만들기: python tools/make_app_icon.py"""
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(HERE, "assets")
N = 1024


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def build():
    c1, c2 = (0x3f, 0x4f, 0xd0), (0x93, 0x9f, 0xf7)           # 포인트 인디고: 진한 쪽 → 밝은 쪽
    # 바탕: 둥근 사각형 + 대각선 그라데이션
    bg = Image.new("RGB", (N, N))
    px = bg.load()
    for y in range(N):
        for x in range(N):
            px[x, y] = lerp(c1, c2, (x * 0.45 + y * 0.9) / (N * 1.35))
    mask = Image.new("L", (N, N), 0)
    ImageDraw.Draw(mask).rounded_rectangle((24, 24, N - 24, N - 24), radius=230, fill=255)
    icon = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    icon.paste(bg, (0, 0), mask)
    # 윗면 은은한 빛
    glow = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((-200, -520, N + 200, 420), fill=(255, 255, 255, 46))
    glow.putalpha(ImageChops.multiply(glow.getchannel("A"), mask))
    icon.alpha_composite(glow)
    # 문서(접힌 모서리) + 그림자
    x0, y0, x1, y1, fold = 270, 190, 754, 834, 150
    shadow = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((x0 + 6, y0 + 30, x1 + 6, y1 + 30), radius=54, fill=(20, 28, 90, 120))
    icon.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(26)))
    page = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(page)
    d.polygon([(x0 + 54, y0), (x1 - fold, y0), (x1, y0 + fold), (x1, y1 - 54), (x1 - 54, y1), (x0 + 54, y1), (x0, y1 - 54), (x0, y0 + 54)],
              fill=(255, 255, 255, 255))
    d.pieslice((x0, y0, x0 + 108, y0 + 108), 180, 270, fill=(255, 255, 255, 255))
    d.pieslice((x1 - 108, y1 - 108, x1, y1), 0, 90, fill=(255, 255, 255, 255))
    d.pieslice((x0, y1 - 108, x0 + 108, y1), 90, 180, fill=(255, 255, 255, 255))
    d.polygon([(x1 - fold, y0), (x1 - fold, y0 + fold - 40), (x1 - fold + 40, y0 + fold), (x1, y0 + fold)], fill=(214, 220, 252, 255))
    # 문서 안 글줄(제목 굵게 + 본문)
    ink, soft = (63, 79, 208, 255), (176, 186, 240, 255)
    d.rounded_rectangle((x0 + 70, y0 + 150, x0 + 300, y0 + 196), radius=23, fill=ink)
    for i, w in enumerate((344, 344, 250)):
        yy = y0 + 270 + i * 82
        d.rounded_rectangle((x0 + 70, yy, x0 + 70 + w, yy + 34), radius=17, fill=soft)
    d.rounded_rectangle((x0 + 70, y0 + 520, x0 + 250, y0 + 554), radius=17, fill=soft)
    icon.alpha_composite(page)
    # 우하단 배지: 흰 테두리의 원 + 체크(양식이 만들어졌다는 표시)
    cx, cy, r = 742, 760, 150
    badge = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    bd = ImageDraw.Draw(badge)
    bd.ellipse((cx - r - 22, cy - r - 22, cx + r + 22, cy + r + 22), fill=(0, 0, 0, 90))
    icon.alpha_composite(badge.filter(ImageFilter.GaussianBlur(14)))
    badge = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    bd = ImageDraw.Draw(badge)
    bd.ellipse((cx - r - 16, cy - r - 16, cx + r + 16, cy + r + 16), fill=(255, 255, 255, 255))
    gb = Image.new("RGB", (N, N))
    gp = gb.load()
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            gp[x, y] = lerp((0x2f, 0xb4, 0x8c), (0x5f, 0xd8, 0xb0), ((x - (cx - r)) + (y - (cy - r))) / (4.0 * r))
    gm = Image.new("L", (N, N), 0)
    ImageDraw.Draw(gm).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    badge.paste(gb, (0, 0), gm)
    bd = ImageDraw.Draw(badge)
    pts = [(cx - 70, cy + 4), (cx - 20, cy + 56), (cx + 76, cy - 54)]
    bd.line(pts, fill=(255, 255, 255, 255), width=40, joint="curve")
    for p in (pts[0], pts[-1]):
        bd.ellipse((p[0] - 20, p[1] - 20, p[0] + 20, p[1] + 20), fill=(255, 255, 255, 255))
    icon.alpha_composite(badge)
    return icon


def main():
    big = build()
    os.makedirs(OUT, exist_ok=True)
    big.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "app_256.png"))
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    frames = [big.resize((s, s), Image.LANCZOS) for s in sizes]
    frames[-1].save(os.path.join(OUT, "app.ico"), format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
    print("아이콘 저장:", OUT)


if __name__ == "__main__":
    main()
