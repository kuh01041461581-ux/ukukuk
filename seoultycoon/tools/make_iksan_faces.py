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
        e = smooth_face(EN.enhance(999, im=o)); e.save(f'{mod}/{n}.d', format='PNG'); show.append((n, o, e))
    if len(sys.argv) > 1:
        pick = [s for s in show if s[0] in ('fd0', 'fd1', 'fd2', 'ff0', 'ff5', 'f0', 'f3', 'fb0', 'fh6')]
        w = Image.new('RGBA', (sum(max(o.width, 30) * 2 + 10 for _, o, _ in pick), 70), (60, 90, 140, 255)); x = 0
        for n, o, e in pick: w.alpha_composite(o, (x, 2)); w.alpha_composite(e, (x + o.width + 2, 2)); x += o.width * 2 + 10
        w.resize((w.width * 4, w.height * 4), Image.NEAREST).save(sys.argv[1])
    print('얼굴·초상 보정', len(show), '장')
