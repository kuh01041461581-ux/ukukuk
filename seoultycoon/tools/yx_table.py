"""yx.d 조각표 해석: 건물 k 의 조각 묶음 = [메인(0,my,96,mh), 위쪽 조각들...].  시트 높이 H = my + mh + 8 (맨 아래 8줄은 바닥 끝 조각)."""
import os, re, glob
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
def sheet_sizes(pk='/tmp/pk'):
    return {int(re.findall(r'b(\d+)', os.path.basename(f))[0]): Image.open(f).size for f in glob.glob(pk + '/b*.d')}
def load(path=None, pk='/tmp/pk'):
    d = open(path or root + '/work/st2/yx.d', 'rb').read()
    rects = [tuple(d[132 + 4 * i:136 + 4 * i]) for i in range((len(d) - 132) // 4)]
    H = sheet_sizes(pk); groups = {}; starts = {}; cur = 0
    for k in sorted(H):
        hk = H[k][1]; found = None
        for i in range(cur, min(len(rects), cur + 40)):
            x, y, w, h = rects[i]
            if x == 0 and w >= 95 and y + h + 8 == hk: found = i; break
        if found is None: continue
        starts[k] = found; cur = found + 1
    ks = sorted(starts)
    for a, k in enumerate(ks):
        end = starts[ks[a + 1]] if a + 1 < len(ks) else min(len(rects), starts[k] + 8)
        groups[k] = (starts[k], rects[starts[k]:end])
    return groups, d
if __name__ == '__main__':
    g, _ = load()
    for k in sorted(g): print(k, g[k][0], g[k][1])
