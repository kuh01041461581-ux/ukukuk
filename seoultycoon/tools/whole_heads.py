"""시장 통머리: 사용자가 정리한 '머리+얼굴 한 장' 시트(work/gpt/whole.png, 2줄 x 9칸 = 표정 ff0~ff17 순서)를 쓴다.
각 표정 조각(ff#)의 PNG 뒤에 'BIGH'+오프셋+통머리 PNG 를 붙이고, 조립용 바탕(fd)·머리(fh) 조각에는 'SKIP'을 붙여 게임이 그리지 않게 한다(GfxLib).
통머리는 얼굴 폭이 표정 조각 폭(24)에 맞고 턱 끝이 표정 조각 아래에 오도록 놓는다.  → st2android/mod/ff*.d fd*.d fh*.d"""
import os, io, sys
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); gp = root + '/work/gpt/whole.png'; mod = root + '/st2android/mod'; src = root + '/work/st2'
if not os.path.exists(gp): print('whole.png 없음 — 건너뜀'); sys.exit(0)
K = 2
im = Image.open(gp).convert('RGB'); a = np.asarray(im).astype(int); bg = np.median(a[:8, :8].reshape(-1, 3), axis=0)
mask = ndi.binary_closing(np.abs(a - bg).sum(2) > 60, iterations=1)
def rows_of(m):
    r = m.sum(1) > 8; out = []; s = None
    for y, v in enumerate(r):
        if v and s is None: s = y
        if not v and s is not None:
            if y - s > 100: out.append((s, y))
            s = None
    if s is not None and len(r) - s > 100: out.append((s, len(r)))
    return out
def cols_split(m, y0, y1, n=9):
    xs = np.where(m[y0:y1].any(0))[0]; x0, x1 = xs.min(), xs.max() + 1; cnt = m[y0:y1].sum(0); w = (x1 - x0) / n; cuts = [x0]
    for k in range(1, n):
        c = int(x0 + w * k); lo, hi = c - int(w * 0.25), c + int(w * 0.25); cuts.append(lo + int(np.argmin(cnt[lo:hi])))
    return cuts + [x1]
def skinmask(arr): return (arr[..., 3] > 200) & (arr[..., 0] > 190) & (arr[..., 1] > 120) & (arr[..., 2] > 70) & (arr[..., 0] > arr[..., 2] + 40)
heads = []
for (y0, y1) in rows_of(mask)[:2]:
    cs = cols_split(mask, y0, y1)
    for k in range(9):
        box = (cs[k], y0, cs[k + 1], y1); sub = im.crop(box); sa = np.asarray(sub).astype(int)
        mm = np.abs(sa - bg).sum(2) > 60; mm = ndi.binary_closing(mm, iterations=1); lab, n = ndi.label(mm)
        if n == 0: continue
        sizes = ndi.sum(mm, lab, range(1, n + 1)); mm = lab == (1 + int(np.argmax(sizes))); mm = ndi.binary_fill_holes(mm)
        al = Image.fromarray((mm * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)).point(lambda v: 255 if v > 150 else (0 if v < 70 else int((v - 70) * 255 / 80)))
        o = sub.convert('RGBA'); o.putalpha(al)
        arr = np.asarray(o).copy(); arr[(arr[..., 2].astype(int) > arr[..., 0].astype(int) + 25), 3] = 0
        o = Image.fromarray(arr, 'RGBA'); heads.append(o.crop(o.getbbox()))
print('통머리', len(heads))
if os.environ.get('DBG'):
    bgi = Image.new('RGB', heads[0].size, (40, 160, 40)); bgi.paste(heads[0], (0, 0), heads[0]); bgi.save('/tmp/claude-0/prev/h0raw.png')
def place(head, k):
    """얼굴 폭 24 → HD 48, 턱 끝을 표정 조각 맨 아래(52)에 맞춘다. 반환: 크기 조정된 머리, 오프셋(bx, by)"""
    a2 = np.asarray(head).astype(int); sk = skinmask(a2); ys = np.where(sk.any(1))[0]; h = head.height
    r0, r1 = int(ys.min() + (ys.max() - ys.min()) * 0.30), int(ys.min() + (ys.max() - ys.min()) * 0.60)           # 눈~코 높이대 피부 폭 = 얼굴 폭
    widths = [(np.where(sk[r])[0].max() - np.where(sk[r])[0].min()) for r in range(r0, r1) if sk[r].any()]; fw = max(widths) if widths else head.width * 0.6
    f = ((62 if k in (3, 4, 5, 9, 10, 11, 12, 13, 14) else 56) / head.width); big = head.resize((max(1, int(head.width * f)), max(1, int(head.height * f))), Image.LANCZOS)
    chin = int(ys.max() * f); cols = np.where(sk[(ys.min() + ys.max()) // 2])[0]; cx = int(cols.mean() * f)
    # 목: 턱 아래에 피부색 목을 붙여 상체와 이어지게 한다
    sa = np.asarray(big).astype(int); skb = skinmask(sa); ysb = np.where(skb.any(1))[0]; ch = int(ysb.max())
    rows = [r for r in range(max(0, ch - 8), ch - 2) if skb[r].any()]; col = np.median(sa[rows][skb[rows]][:, :3], axis=0).astype(int) if rows else np.array([235, 180, 130])
    NH = 6 if k in (3, 4, 5, 9, 10, 11, 12, 13, 14) else 0; canvas = Image.new('RGBA', (big.width, big.height + NH), (0, 0, 0, 0)); nw = int(big.width * 0.26); ncx = int(np.where(skb[ch - 6])[0].mean()) if skb[ch - 6].any() else big.width // 2
    neck = Image.new('RGBA', canvas.size, (0, 0, 0, 0)); from PIL import ImageDraw; dn = ImageDraw.Draw(neck); dn.rectangle([ncx - nw // 2, ch - 8, ncx + nw // 2, ch + NH + 3], fill=tuple(int(v * 0.93) for v in col) + (255,))
    if NH: canvas.alpha_composite(neck); canvas.alpha_composite(big, (0, 0)); big = canvas
    return big, (24 * K // 2 - cx), (26 * K - chin + (7 if k in (3, 4, 5, 9, 10, 11, 12, 13, 14) else 1))
n = 0
for k, head in enumerate(heads[:18]):
    big, bx, by = place(head, k); p = f'{mod}/ff{k}.d'; raw = open(p if os.path.exists(p) else f'{src}/ff{k}.d', 'rb').read()
    i = raw.find(b'IEND') + 8; j = raw.find(b'BIGH'); raw = raw[:j] if j > 0 else raw
    b = io.BytesIO(); big.save(b, format='PNG', optimize=True)
    open(p, 'wb').write(raw + b'BIGH' + int(bx).to_bytes(2, 'big', signed=True) + int(by).to_bytes(2, 'big', signed=True) + b.getvalue()); n += 1
for nm in ['fd0', 'fd1', 'fd2'] + [f'fh{i}' for i in range(7)]:
    p = f'{mod}/{nm}.d'; raw = open(p if os.path.exists(p) else f'{src}/{nm}.d', 'rb').read()
    if not raw.endswith(b'SKIP'): open(p, 'wb').write(raw + b'SKIP')
print('통머리 적용', n, '장')
