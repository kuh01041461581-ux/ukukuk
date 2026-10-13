"""assets/app.ico 생성 (Pillow 필요). 아이콘을 바꾸고 싶을 때만 실행."""
from PIL import Image, ImageDraw

N = 512
img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
# 바이올렛 → 인디고 그라데이션 라운드 사각형
grad = Image.new("RGBA", (N, N))
gp = grad.load()
for y in range(N):
    for x in range(N):
        t = (x + y) / (2 * N)
        gp[x, y] = (int(124 + (84 - 124) * t), int(108 + (96 - 108) * t), int(255 + (235 - 255) * t), 255)
mask = Image.new("L", (N, N), 0)
ImageDraw.Draw(mask).rounded_rectangle((16, 16, N - 16, N - 16), radius=112, fill=255)
img.paste(grad, (0, 0), mask)
# 달력 모양: 상단 바 + 체크
d.rounded_rectangle((96, 120, N - 96, N - 96), radius=48, fill=(255, 255, 255, 235))
d.rounded_rectangle((96, 120, N - 96, 200), radius=48, fill=(255, 255, 255, 255))
d.rectangle((96, 160, N - 96, 200), fill=(255, 255, 255, 255))
for x in (176, N - 176):
    d.rounded_rectangle((x - 16, 84, x + 16, 160), radius=16, fill=(40, 36, 90, 255))
d.line([(168, 330), (232, 390), (352, 262)], fill=(124, 108, 255, 255), width=34, joint="curve")
for p in ((168, 330), (232, 390), (352, 262)):
    d.ellipse((p[0] - 17, p[1] - 17, p[0] + 17, p[1] + 17), fill=(124, 108, 255, 255))
img.save("assets/app.ico", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
