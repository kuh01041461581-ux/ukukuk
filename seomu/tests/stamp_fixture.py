#!/usr/bin/env python3
"""make_scan_fixture.mjs 가 부른다: clean.pdf 를 150dpi 그림으로 바꾸고, 근무일 칸·청구인 칸에 빨간 도장을 그려 그림 PDF 로 저장."""
import sys, json, subprocess, glob, datetime, pathlib
from PIL import Image, ImageDraw
import numpy as np
tmp, out = sys.argv[1], sys.argv[2]
subprocess.run(["pdftoppm", "-r", "150", "-png", f"{tmp}/clean.pdf", f"{tmp}/pg"], check=True)
pages = sorted(glob.glob(f"{tmp}/pg-*.png"))
worked = json.load(open(f"{tmp}/worked.json"))
def grp(a, gap=4):
    o = []
    for x in a:
        if o and x - o[-1][-1] <= gap: o[-1].append(x)
        else: o.append([x])
    return [int(np.mean(g)) for g in o]
def stamp(d, cx, cy, r=26):
    d.ellipse([cx - r, cy - r * 1.25, cx + r, cy + r * 1.25], outline=(210, 40, 50), width=5)
    d.text((cx - 10, cy - 12), "인", fill=(210, 40, 50))
first = datetime.date(2026, 10, 1); start = first - datetime.timedelta(days=first.weekday())
ims = []
for i, p in enumerate(pages):
    im = Image.open(p).convert("RGB"); g = np.asarray(im.convert("L")).astype(int); H, W = g.shape; d = ImageDraw.Draw(im)
    if i < 2:   # 일일점검부
        hl = grp([y for y in range(H) if (g[y] < 205).mean() > 0.45]); y1 = hl[-1]
        for k, y0 in enumerate(hl[:-1]):
            xs = grp([x for x in range(W) if (g[y0:y1, x] < 205).mean() > 0.6])
            if len(xs) == 9 and sum((g[y0 + 2:hl[k + 1] - 2, x - 2:x + 3] < 225).mean(0).max() > 0.7 for x in xs) >= 8: break
        ys = grp([y for y in range(y0, y1 + 1) if (g[y, xs[0]:xs[-1]] < 205).mean() > 0.6])
        assert len(xs) == 9 and len(ys) == 13, (len(xs), len(ys))
        for iso in worked[i]:
            dt = datetime.date.fromisoformat(iso); k = (dt - start).days; wi, ci = k // 7, k % 7
            stamp(d, (xs[ci + 1] + xs[ci + 2]) // 2, (ys[wi * 2 + 1] + ys[wi * 2 + 2]) // 2)
    else:       # 청구서: 청구인 칸 근처
        stamp(d, int(W * 0.83), int(H * 0.31))
    ims.append(im)
ims[0].save(out, save_all=True, append_images=ims[1:], resolution=150)
print("pages", len(ims))
