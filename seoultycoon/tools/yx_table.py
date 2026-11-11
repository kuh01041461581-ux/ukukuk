"""yx.d 해석(완전판).
  [0..131]   머리(건물별 값·표)
  [132..1331] 조각 사각형 300개 (x,y,w,h 각 1바이트) — 건물 순서대로 이어짐
  [1332..]   건물 65개 기록: p0,p1, 프레임수 nF, (시작,개수)*nF, 그리고 프레임마다 (조각번호, 화면x, 화면y)*개수
화면 좌표는 '조립 그림'의 좌표(위가 0). 조각번호 0 = 메인(가로 96). 마지막 프레임이 덧조각까지 포함한 전체 모습.
맨 아래 8줄(바닥 끝)은 표에 없고 항상: 시트 (0,H-8,64,8) → (16, 메인y+mh),  (64,H-8,32,8) → (32, 메인y+mh+8)."""
import os, re, glob
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
def sheet_sizes(pk='/tmp/pk'):
    return {int(re.findall(r'b(\d+)', os.path.basename(f))[0]): Image.open(f).size for f in glob.glob(pk + '/b*.d')}
def load(path=None):
    d = open(path or root + '/work/st2/yx.d', 'rb').read()
    pos = 1332; recs = []
    while pos < len(d) - 3:
        p0, p1, nF = d[pos], d[pos + 1], d[pos + 2]; q = pos + 3
        fr = [(d[q + 2 * i], d[q + 2 * i + 1]) for i in range(nF)]; q += 2 * nF; frames = []
        for (st, cnt) in fr:
            frames.append([(d[q + 3 * j], d[q + 3 * j + 1], d[q + 3 * j + 2]) for j in range(cnt)]); q += 3 * cnt
        recs.append({'p': (p0, p1), 'frames': frames}); pos = q
    H = sheet_sizes(); nrect = (1332 - 132) // 4; R = [tuple(d[132 + 4 * i:136 + 4 * i]) for i in range(nrect)]
    start = 0; out = {}
    for k, r in enumerate(recs):
        n = max(e[0] for f in r['frames'] for e in f) + 1
        if k in H:                                             # 쓰이지 않는 조각이 끼어 있을 수 있어, 시트 높이에 맞는 메인을 앞쪽에서 찾아 맞춘다
            for s in range(start, min(nrect, start + 8)):
                x, y, w, h = R[s]
                if x == 0 and w >= 94 and y + h + 8 == H[k][1]: start = s; break
        r['start'] = start; r['rects'] = R[start:start + n]; start += n; out[k] = r
    return out, d
if __name__ == '__main__':
    t, d = load(); H = sheet_sizes(); bad = 0
    for k, r in t.items():
        m = r['rects'][0]; ok = (k in H and m[1] + m[3] + 8 == H[k][1]); bad += (not ok)
        print(k, 'H', H.get(k), 'main', m, 'OK' if ok else '??', 'frames', [len(f) for f in r['frames']])
    print('rects total', sum(len(r['rects']) for r in t.values()), 'mismatch', bad)
