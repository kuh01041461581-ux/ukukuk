"""앱 아이콘(assets/icon.ico, icon.png) 생성. 실행: python make_icon.py  (Pillow 필요)"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "assets"
S = 1024  # 큰 그림으로 그린 뒤 줄여서 계단 현상 없애기


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def make():
    # 배경: 대각선 그라데이션(진한 남색 → 파랑) 둥근 사각형
    top, bot = (24, 44, 112), (42, 120, 214)
    grad = Image.new("RGB", (S, S))
    px = grad.load()
    for y in range(S):
        for x in range(S):
            px[x, y] = lerp(top, bot, (x * 0.35 + y * 0.65) / S)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.225), fill=255)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img.paste(grad, (0, 0), mask)

    # 은은한 윗쪽 하이라이트
    hl = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(hl).ellipse((-S * 0.2, -S * 0.75, S * 1.2, S * 0.45), fill=(255, 255, 255, 40))
    hl = hl.filter(ImageFilter.GaussianBlur(40))
    hl.putalpha(Image.composite(hl.getchannel("A"), Image.new("L", (S, S), 0), mask))
    img = Image.alpha_composite(img, hl)

    d = ImageDraw.Draw(img)
    # 막대 3개 (오르는 집행률) — 흰색, 마지막 막대는 따뜻한 노랑
    base = int(S * 0.74)
    bw, gap = int(S * 0.17), int(S * 0.075)
    x0 = int((S - (3 * bw + 2 * gap)) / 2)
    heights = [0.20, 0.33, 0.50]
    cols = [(255, 255, 255, 205), (255, 255, 255, 235), (255, 199, 61, 255)]
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    for i, (h, c) in enumerate(zip(heights, cols)):
        x = x0 + i * (bw + gap)
        box = (x, base - int(S * h), x + bw, base)
        sd.rounded_rectangle((box[0], box[1] + 14, box[2], box[3] + 14), radius=int(bw * 0.28), fill=(5, 15, 50, 110))
    shadow = shadow.filter(ImageFilter.GaussianBlur(14))
    img = Image.alpha_composite(img, shadow)
    d = ImageDraw.Draw(img)
    for i, (h, c) in enumerate(zip(heights, cols)):
        x = x0 + i * (bw + gap)
        d.rounded_rectangle((x, base - int(S * h), x + bw, base), radius=int(bw * 0.28), fill=c)
    # 바닥선
    d.rounded_rectangle((x0 - int(S * 0.04), base + int(S * 0.04), x0 + 3 * bw + 2 * gap + int(S * 0.04), base + int(S * 0.062)),
                        radius=int(S * 0.011), fill=(255, 255, 255, 120))
    # 위쪽: 동전(원) — 예산을 상징. 안에 작은 체크 대신 가로 두 줄(₩ 느낌)
    cx, cy, r = int(S * 0.305), int(S * 0.30), int(S * 0.105)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(255, 255, 255, 235), width=int(S * 0.026))
    for dy in (-0.030, 0.030):
        d.rounded_rectangle((cx - int(S * 0.056), cy + int(S * dy) - int(S * 0.008), cx + int(S * 0.056), cy + int(S * dy) + int(S * 0.008)),
                            radius=int(S * 0.008), fill=(255, 255, 255, 235))
    return img


def main():
    OUT.mkdir(exist_ok=True)
    img = make()
    img.resize((512, 512), Image.LANCZOS).save(OUT / "icon.png")
    sizes = [16, 24, 32, 48, 64, 128, 256]
    img.resize((256, 256), Image.LANCZOS).save(OUT / "icon.ico", sizes=[(s, s) for s in sizes])
    # 작은 크기는 따로 줄여서 선명하게
    print("완성:", OUT / "icon.ico")


if __name__ == "__main__":
    main()
