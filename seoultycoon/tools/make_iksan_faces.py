"""시장 얼굴·머리·옷, 비서 초상의 퀄리티 보정: 원본 그림의 모양은 그대로 두고 격자 디더를 실제 중간색으로 풀고, 피부·머리의 계단진 음영을 부드럽게 잇는다. → st2android/mod/*.d
(마룡이 초상 f7 은 make_maryong 이 만든다)"""
import os, sys, glob, re
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import enhance_orig as EN
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); src = root + '/work/st2'; mod = root + '/st2android/mod'
def smooth_face(im, passes=2):
    """경계 보존 다듬기: 색이 가까운 이웃끼리만 섞는다(윤곽선·눈·입은 그대로)."""
    a = np.asarray(im).astype(np.float32); al = a[..., 3] > 0; rgb = a[..., :3]; h, w = al.shape
    for _ in range(passes):
        out = rgb.copy()
        for y in range(h):
            for x in range(w):
                if not al[y, x]: continue
                c = rgb[y, x]; acc = c * 3.0; wt = 3.0
                for dy, dx, k in ((-1, 0, 1.0), (1, 0, 1.0), (0, -1, 1.0), (0, 1, 1.0), (-1, -1, 0.5), (-1, 1, 0.5), (1, -1, 0.5), (1, 1, 0.5)):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < h and 0 <= xx < w and al[yy, xx] and np.abs(rgb[yy, xx] - c).sum() < 96: acc = acc + rgb[yy, xx] * k; wt += k
                out[y, x] = acc / wt
        rgb = out
    m = rgb.mean(-1, keepdims=True); rgb = m + (rgb - m) * 1.08                                   # 혈색 약간
    r = np.zeros((h, w, 4), np.uint8); r[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8); r[..., 3] = al * 255
    return Image.fromarray(r, 'RGBA')
def beautify(im):
    """여자 시장 얼굴(ff3~5, ff9~11)을 원작 그림 그대로 살리면서 젊고 화사하게: 피부 그늘을 밝고 맑게, 볼에 은은한 홍조, 입술은 장밋빛, 눈매(짙은 선)는 그대로."""
    a = np.asarray(im).astype(np.float32); al = a[..., 3] > 0; rgb = a[..., :3]; h, w = al.shape
    lum = rgb @ np.array([0.30, 0.59, 0.11], dtype=np.float32); light = np.array([255, 234, 210], dtype=np.float32)
    for y in range(h):
        for x in range(w):
            if not al[y, x]: continue
            c = rgb[y, x]; L = lum[y, x]
            xs = np.where(al[y])[0]; e = min(x - xs[0], xs[-1] - x)                     # 얼굴 윤곽까지의 거리
            edge = e <= 0 or y >= 23
            if L >= 105 and not edge: c = c + (light - c) * (0.78 if L < 205 else 0.45)        # 피부: 그늘·주름을 지우고 밝고 고르게
            if 15 <= y <= 21 and 7 <= x <= 16 and L < 150 and c[0] > c[2] + 20 and not (c[0] > 230 and c[1] > 230):
                c = c * 0.35 + np.array([222, 92, 104], dtype=np.float32) * 0.65                    # 입술
            if 11 <= y <= 13 and (3 <= x <= 6 or 17 <= x <= 20) and L >= 120: c = c * 0.72 + np.array([255, 160, 150], dtype=np.float32) * 0.28   # 홍조
            rgb[y, x] = c
    r = np.zeros((h, w, 4), np.uint8); r[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8); r[..., 3] = al * 255
    return Image.fromarray(r, 'RGBA')
def detail(im):
    """밝게 편 얼굴에 입체감을 다시 넣는다: 광대 아래·턱 옆 그늘(갸름한 턱선), 콧대 하이라이트와 콧방울, 쌍꺼풀 선, 한 줄 키운 눈, 윗입술·아랫입술."""
    a = np.asarray(im).astype(np.float32); al = a[..., 3] > 0; rgb = a[..., :3]; h, w = al.shape
    lum = lambda c: float(c[0] * 0.30 + c[1] * 0.59 + c[2] * 0.11)
    def mix(x, y, col, k):
        if 0 <= x < w and 0 <= y < h and al[y, x]: rgb[y, x] = rgb[y, x] * (1 - k) + np.array(col, dtype=np.float32) * k
    SH, SH2 = (226, 168, 132), (196, 132, 100)
    # 1) 윤곽 음영: 관자놀이~볼 옆은 얇게, 광대 아래부터 턱까지는 넓고 짙게 → 얼굴이 작고 갸름해 보인다
    for y in range(h):
        xs = np.where(al[y])[0]
        if len(xs) == 0: continue
        for x in xs:
            e = min(x - xs[0], xs[-1] - x)
            if lum(rgb[y, x]) < 105: continue
            wdt = 2 if y < 11 else (4 if y < 16 else 5)
            if e == 0: mix(x, y, SH2, 0.90)
            elif e < wdt: mix(x, y, SH2 if e == 1 and y >= 11 else SH, 0.85 - 0.17 * e)
    for (x, y, k) in ((4, 15, .35), (5, 16, .35), (6, 17, .25), (19, 15, .35), (18, 16, .35), (17, 17, .25)): mix(x, y, SH, k)      # 광대 아래 그늘
    # 2) 코: 콧대 하이라이트(미간~코끝) + 한쪽 옆 그늘 + 코끝·콧방울
    for y in range(7, 14): mix(11, y, (255, 246, 232), 0.55); mix(13, y, SH, 0.50 if y > 9 else 0.28); mix(10, y, SH, 0.12)
    mix(10, 14, SH, 0.55); mix(11, 14, SH2, 0.55); mix(12, 14, SH2, 0.65); mix(13, 14, SH, 0.6); mix(11, 13, (255, 252, 244), 0.9)
    # 3) 눈: 가장 짙은 줄(윗속눈썹)을 찾아 그 위에 쌍꺼풀 선, 아래로 눈동자를 한 줄 키우고 반사광
    for (c0, c1) in ((1, 10), (13, 22)):
        rows = {y: [x for x in range(c0, c1 + 1) if al[y, x] and lum(rgb[y, x]) < 95] for y in range(5, 10)}
        ye = max(rows, key=lambda y: len(rows[y])); xs = rows[ye]
        if len(xs) < 3: continue
        x0, x1 = min(xs), max(xs)
        for x in range(x0, x1 + 1):
            if lum(rgb[ye - 1, x]) >= 95: mix(x, ye - 1, (176, 112, 88), 0.70)                    # 쌍꺼풀
        mid = (x0 + x1) // 2
        for x in (mid - 1, mid, mid + 1):
            if lum(rgb[ye + 1, x]) >= 95: mix(x, ye + 1, (74, 44, 36), 0.85)                      # 눈동자 한 줄 더
        for x in (x0, x1):
            if lum(rgb[ye + 1, x]) >= 95: mix(x, ye + 1, (255, 250, 246), 0.7)                     # 흰자
        mix(mid - 1 if c0 < 12 else mid + 1, ye + 1, (255, 255, 255), 0.9)                         # 반사광
        for x in range(x0 + 1, x1): mix(x, ye + 2, SH, 0.35)                                       # 애교살 아래 그늘
    # 4) 입술: 입 선(짙은 줄) 위는 윗입술, 아래는 도톰한 아랫입술 + 광택
    best = None
    for y in range(16, 22):
        xs = [x for x in range(6, 18) if al[y, x] and lum(rgb[y, x]) < 150 and rgb[y, x][0] > rgb[y, x][2] + 15]
        if best is None or len(xs) > len(best[1]): best = (y, xs)
    if best and len(best[1]) >= 3:
        y, xs = best; x0, x1 = min(xs), max(xs)
        for x in range(x0, x1 + 1): mix(x, y, (196, 60, 84), 0.75)
        for x in range(x0 + 1, x1): 
            if lum(rgb[y - 1, x]) > 150 and not (rgb[y - 1, x] > 235).all(): mix(x, y - 1, (226, 96, 112), 0.60)
        for x in range(x0 + 1, x1):
            if not (rgb[y + 1, x] > 235).all(): mix(x, y + 1, (238, 112, 128), 0.80)
        mix((x0 + x1) // 2, y + 1, (255, 206, 210), 0.85)
        for x in range(x0 + 2, x1 - 1): mix(x, y + 2, SH, 0.40)
    r = np.zeros((h, w, 4), np.uint8); r[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8); r[..., 3] = al * 255
    return Image.fromarray(r, 'RGBA')
FEMALE = {'ff3', 'ff4', 'ff5', 'ff9', 'ff10', 'ff11'}
def names():
    out = []
    for f in sorted(glob.glob(src + '/*.d')):
        n = os.path.basename(f)[:-2]
        if re.match(r'^(fd\d+|ff\d+|fb\d+|fh\d+|fa\d+|f\d+)$', n) and n != 'f7': out.append(n)
    return out
if __name__ == '__main__':
    ns = names(); show = []
    for n in ns:
        try: o = Image.open(f'{src}/{n}.d').convert('RGBA')
        except Exception: continue
        e = smooth_face(EN.enhance(999, im=o))
        if n in FEMALE: e = detail(beautify(e))
        e.save(f'{mod}/{n}.d', format='PNG'); show.append((n, o, e))
    if len(sys.argv) > 1:
        pick = [s for s in show if s[0] in ('ff3', 'ff4', 'ff5', 'ff9', 'ff10', 'ff11')]
        w = Image.new('RGBA', (sum(max(o.width, 30) * 2 + 10 for _, o, _ in pick), 70), (60, 90, 140, 255)); x = 0
        for n, o, e in pick: w.alpha_composite(o, (x, 2)); w.alpha_composite(e, (x + o.width + 2, 2)); x += o.width * 2 + 10
        w.resize((w.width * 8, w.height * 8), Image.NEAREST).save(sys.argv[1])
    print('얼굴·초상 보정', len(show), '장')
