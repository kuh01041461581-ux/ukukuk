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
            if y >= 7 and 1 <= e <= 2 and L >= 105: c = c * (0.80 if e == 1 else 0.92)             # 볼·턱 옆선에 그늘을 넣어 갸름하게
            if 15 <= y <= 21 and 7 <= x <= 16 and L < 150 and c[0] > c[2] + 20 and not (c[0] > 230 and c[1] > 230):
                c = c * 0.35 + np.array([222, 92, 104], dtype=np.float32) * 0.65                    # 입술
            if 11 <= y <= 13 and (3 <= x <= 6 or 17 <= x <= 20) and L >= 120: c = c * 0.72 + np.array([255, 160, 150], dtype=np.float32) * 0.28   # 홍조
            rgb[y, x] = c
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
        if n in FEMALE: e = beautify(e)
        e.save(f'{mod}/{n}.d', format='PNG'); show.append((n, o, e))
    if len(sys.argv) > 1:
        pick = [s for s in show if s[0] in ('ff3', 'ff4', 'ff5', 'ff9', 'ff10', 'ff11')]
        w = Image.new('RGBA', (sum(max(o.width, 30) * 2 + 10 for _, o, _ in pick), 70), (60, 90, 140, 255)); x = 0
        for n, o, e in pick: w.alpha_composite(o, (x, 2)); w.alpha_composite(e, (x + o.width + 2, 2)); x += o.width * 2 + 10
        w.resize((w.width * 6, w.height * 6), Image.NEAREST).save(sys.argv[1])
    print('얼굴·초상 보정', len(show), '장')
