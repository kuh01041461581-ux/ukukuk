"""사용자가 GPT 로 다시 그려 정리해 준 시장 얼굴(표정 18장)·초상·머리 그림(work/gpt/*.png)을 게임의 2배 해상도 층으로 반영한다.
원본 파일(.d, PNG) 뒤에 2배 크기 PNG 를 덧붙인다(GfxLib.decode 가 PNG 뒤의 PNG 를 고해상도 층으로 읽는다). 원본 그림의 '불투명 영역 상자'에 맞춰 배치한다.
work/gpt 가 없으면 아무것도 하지 않는다.   → st2android/mod/{ff,f,fa,fb,fh}*.d"""
import os, io, sys
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
from scipy import ndimage as ndi
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); gp = root + '/work/gpt'; src = root + '/work/st2'; mod = root + '/st2android/mod'
if not os.path.exists(gp + '/sheet.png'): print('gpt 이미지 없음 — 건너뜀'); sys.exit(0)
K = 2

def cut(im, box, bgc, thr=70):
    """box 안에서 배경색과 다른 덩어리만 남긴 RGBA(구멍은 채우고 가장자리는 부드럽게)"""
    x0, y0, x1, y1 = box; a = np.asarray(im.crop(box).convert('RGB')).astype(int)
    m = np.abs(a - bgc).sum(2) > thr; m = ndi.binary_closing(m, iterations=1)
    lab, n = ndi.label(m)
    if n == 0: return None
    area = ndi.sum(m, lab, range(1, n + 1)); keep = [i + 1 for i, s in enumerate(area) if s >= 0.04 * area.max()]      # 라벨 글자 같은 작은 조각은 버린다
    m = np.isin(lab, keep); m = ndi.binary_fill_holes(m)
    al = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)).point(lambda v: 255 if v > 150 else (0 if v < 60 else int((v - 60) * 255 / 90)))
    out = im.crop(box).convert('RGBA'); out.putalpha(al); bb = out.getbbox(); return out.crop(bb) if bb else None

def noblue(spr):
    if spr is None: return None
    a = np.asarray(spr).copy(); bad = (a[..., 2].astype(int) > a[..., 0].astype(int) + 25) & (a[..., 3] > 0); a[bad, 3] = 0
    return Image.fromarray(a, 'RGBA')
def apply(name, spr, fmt='.d'):
    p = f'{src}/{name}{fmt}'
    if spr is None or not os.path.exists(p): return False
    raw = open(p, 'rb').read(); base = Image.open(io.BytesIO(raw)).convert('RGBA'); w, h = base.size
    ob = base.getchannel('A').getbbox() or (0, 0, w, h)
    big = spr.resize(((ob[2] - ob[0]) * K, (ob[3] - ob[1]) * K), Image.LANCZOS)
    hd = Image.new('RGBA', (w * K, h * K), (0, 0, 0, 0)); hd.alpha_composite(big, (ob[0] * K, ob[1] * K))
    i = raw.find(b'IEND'); first = raw[:i + 8]; b = io.BytesIO(); hd.save(b, format='PNG', optimize=True)
    open(f'{mod}/{name}{fmt}', 'wb').write(first + b.getvalue()); return True

n = 0
# 1) 시장 표정 ff0~ff17 · 바탕 얼굴 fd0~fd2: 사용자가 정리해 준 얼굴 시트(faceA: 전체, faceB: 9~11번만, fd_gpt: 바탕 얼굴)
def comps(img_path, thr, minh):
    im = Image.open(img_path).convert('RGB'); a = np.asarray(im).astype(int); bg = np.median(a[:8, :8].reshape(-1, 3), axis=0)
    m = ndi.binary_closing(np.abs(a - bg).sum(2) > thr, iterations=2); lab, k = ndi.label(m); out = []
    for sl in ndi.find_objects(lab):
        h = sl[0].stop - sl[0].start; w = sl[1].stop - sl[1].start
        if h >= minh and w >= minh: out.append((sl[1].start, sl[0].start, sl[1].stop, sl[0].stop))
    return im, bg, sorted(out, key=lambda b: (b[1] // 250, b[0]))
if os.path.exists(gp + '/faceA.png'):
    IA, bgA, cA = comps(gp + '/faceA.png', 60, 120); IB, bgB, cB = comps(gp + '/faceB.png', 60, 120)
    for i in range(18):
        if i in (9, 10, 11): im_, bg_, box = IB, bgB, (cB[i - 1][0], 456, cB[i - 1][2], cB[i - 1][3])                           # 9·10·11번은 세 번째 그림에서(그 그림의 둘째 줄은 8번이 앞줄에 합쳐져 한 칸씩 당겨진다)
        else: im_, bg_, box = IA, bgA, cA[i]
        n += apply(f'ff{i}', noblue(cut(im_, (box[0] - 2, max(0, box[1] - 2), box[2] + 2, box[3] + 2), bg_, 60)))
    IF, bgF2, cF = comps(gp + '/fd_gpt.png', 60, 150)
    for i in range(3): n += apply(f'fd{i}', noblue(cut(IF, (cF[i][0] - 2, cF[i][1] - 2, cF[i][2] + 2, cF[i][3] + 2), bgF2, 60)))
def fill_top(name, rows=2):
    """2배 층 맨 윗줄이 비면 뒤 배경이 가는 선으로 비친다 — 아랫줄을 복사해 채운다"""
    p = f'{mod}/{name}.d'; raw = open(p, 'rb').read(); i = raw.find(b'IEND') + 8; a = np.asarray(Image.open(io.BytesIO(raw[i:])).convert('RGBA')).copy()
    for r in range(rows - 1, -1, -1):
        m = (a[r, :, 3] < 255) & (a[r + 1, :, 3] > 200); a[r, m] = a[r + 1, m]
    bb = io.BytesIO(); Image.fromarray(a, 'RGBA').save(bb, format='PNG', optimize=True); open(p, 'wb').write(raw[:i] + bb.getvalue())
if os.path.exists(gp + '/faceA.png'):
    for i in range(18): fill_top(f'ff{i}')
    for i in range(3): fill_top(f'fd{i}', 3)
S0 = None
# 2) 초상·상체·장식(sheet.png): 행별로 서로 붙은 인물을 가장 좁은 열에서 나눈다
S = Image.open(gp + '/sheet.png').convert('RGB'); bgS = np.median(np.asarray(S)[:8, :8].reshape(-1, 3), axis=0)
def label_free(box):                                           # 칸 왼쪽 위의 번호 글자 자리를 배경색으로 덮는다
    x0, y0 = box[0], box[1]; ImageDrawRect(x0, y0)
def ImageDrawRect(x0, y0): pass
PORTRAITS = ['f%d' % i for i in range(16)]                                          # 반영할 인물 초상 번호(예: ['f3', 'f5']). 비어 있으면 원본 유지
P2 = Image.open(gp + '/sheet2.png').convert('RGB') if os.path.exists(gp + '/sheet2.png') else S           # 비서 얼굴은 새로 받은 시트(sheet2)에서
bgP = np.median(np.asarray(P2)[:6, :6].reshape(-1, 3), axis=0)
rows = [(0, 211, [0, 194, 396, 607, 799, 992, 1160, 1358], ['f0', 'f1', 'f2', 'f3', 'f4', 'f5', 'f6']),
        (213, 424, [0, 188, 376, 598, 803, 996, 1168, 1358], ['f7', 'f8', 'f9', 'f10', 'f11', 'f12', 'f13']),
        (428, 628, [0, 194, 391], ['f14', 'f15'])]
for y0, y1, cuts, names in rows:                        # 번호 글자는 작은 조각이라 cut() 이 알아서 버린다(머리를 가리는 네모칸 지우기는 쓰지 않는다)
    for i, nm in enumerate(names):
        if nm in PORTRAITS: n += apply(nm, cut(P2, (max(0, cuts[i] - 6), y0, min(P2.width, cuts[i + 1] + 6), y1), bgP, 70))
for nm, box in (('fa0', (515, 442, 597, 495)), ('fa1', (599, 442, 686, 600)), ('fa2', (689, 442, 800, 600)), ('fb0', (804, 458, 991, 536)), ('fb1', (992, 458, 1161, 536)), ('fb2', (1164, 458, 1358, 536))):
    S2 = S.copy(); ImageDraw.Draw(S2).rectangle([box[0], box[1] - 12, box[0] + 40, box[1] + 8], fill=tuple(int(v) for v in bgS)); n += apply(nm, cut(S2, box, bgS, 70))
# 3) 머리 스트라이프(fh)는 원본 유지
if os.path.exists(gp + '/fb1_new.png'):                          # 사용자가 GPT 로 다시 그려 준 여자 시장 상체(목선이 조금 열린 판)를 그대로 쓴다
    N = Image.open(gp + '/fb1_new.png').convert('RGB'); bgN = np.median(np.asarray(N)[:6, :6].reshape(-1, 3), axis=0); n += apply('fb1', noblue(cut(N, (0, 0, N.width, N.height), bgN, 50)))
    raw = open(f'{mod}/fb1.d', 'rb').read(); i = raw.find(b'IEND') + 8; hd1 = np.asarray(Image.open(io.BytesIO(raw[i:])).convert('RGBA')).copy()      # 목 밑선 제거: 맨 윗줄이 비면 배경이 선으로 비치므로 아래 줄을 복사해 채운다
    for r in (1, 0):
        m = (hd1[r, :, 3] < 255) & (hd1[r + 1, :, 3] > 0); hd1[r, m] = hd1[r + 1, m]
    bb = io.BytesIO(); Image.fromarray(hd1, 'RGBA').save(bb, format='PNG', optimize=True); open(f'{mod}/fb1.d', 'wb').write(raw[:i] + bb.getvalue()); print('fb1: 사용자 제공 그림')
# 4) 여자 시장 상체(fb1): 아래쪽에 부드러운 음영(곡면 그늘)과 하이라이트를 넣어 앞가슴이 도톰해 보이게 한다(가슴 자체는 그림 밖이라 음영으로만 표현)
from PIL import ImageDraw
if os.path.exists(gp + '/fb1_new.png'): sys.exit(0)
raw = open(f'{mod}/fb1.d', 'rb').read(); i = raw.find(b'IEND') + 8; first = raw[:i]; hd = Image.open(io.BytesIO(raw[i:])).convert('RGBA'); w, h = hd.size
sh = Image.new('RGBA', (w * 4, h * 4), (0, 0, 0, 0)); d = ImageDraw.Draw(sh); hl = Image.new('RGBA', (w * 4, h * 4), (0, 0, 0, 0)); e = ImageDraw.Draw(hl)
for cx in (44, 104):                                               # 좌·우 앞판의 둥근 면: 아래쪽 초승달 그늘 + 위쪽 하이라이트
    d.pieslice([(cx - 26) * 4, 4 * 4, (cx + 26) * 4, 46 * 4], 15, 165, fill=(110, 10, 60, 190)); e.ellipse([(cx - 13) * 4, 18 * 4, (cx + 4) * 4, 27 * 4], fill=(255, 205, 230, 130))
sh = sh.filter(ImageFilter.GaussianBlur(10)).resize((w, h), Image.LANCZOS); hl = hl.filter(ImageFilter.GaussianBlur(8)).resize((w, h), Image.LANCZOS)
al = hd.getchannel('A')
for layer in (sh, hl):
    la = layer.getchannel('A'); layer.putalpha(Image.fromarray((np.minimum(np.asarray(la), np.asarray(al))).astype(np.uint8)))      # 옷 밖으로 번지지 않게
hd.alpha_composite(sh); hd.alpha_composite(hl)
b = io.BytesIO(); hd.save(b, format='PNG', optimize=True); open(f'{mod}/fb1.d', 'wb').write(first + b.getvalue())
print('GPT 그림 반영', n, '장 (시장 표정·머리·fd 는 원본 유지)')
