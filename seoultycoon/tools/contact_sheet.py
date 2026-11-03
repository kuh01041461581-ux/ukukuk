"""폴더 안 PNG들을 한 장에 모아 미리보기 시트로 만든다. 사용: contact_sheet.py 입력폴더 출력.png [열수] [접두사]"""
import sys, os, re
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 12
pre = sys.argv[4] if len(sys.argv) > 4 else ''
def key(n): return [int(t) if t.isdigit() else t for t in re.split(r'(\d+)', n)]
ims = []
for n in sorted(os.listdir(src), key=key):
    if not n.startswith(pre): continue
    try: ims.append((n, Image.open(os.path.join(src, n)).convert('RGBA')))
    except Exception: pass
cw = min(max(i.width for _, i in ims), 160); ch = min(max(i.height for _, i in ims), 160)
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGBA', (cols * (cw + 4), rows * (ch + 4)), (200, 80, 200, 255))
for k, (n, im) in enumerate(ims):
    im = im.crop((0, 0, cw, ch))
    sheet.alpha_composite(im, ((k % cols) * (cw + 4), (k // cols) * (ch + 4)))
sheet.convert('RGB').save(out); print(len(ims), sheet.size)
