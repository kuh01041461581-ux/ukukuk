"""게임의 건물 조각 규칙(yx.d)에 맞춰 시트를 만들고, 게임이 조립하는 그대로 다시 맞춰 보는 공용 도구. (규칙: docs/SPRITE_RULES.md, 표 해석: yx_table.py)

조립 그림 좌표: 폭 96, 위가 0. 메인 조각은 (0, MY), 높이 mh. 바닥 마름모는 2:1 이고 가장 넓은 줄 = MY + mh - 8, 아래 끝 = MY + mh + 16.
  compose(k, 시트)      : 게임이 그리는 대로 조립 (원본·새 도트 모두)
  canvas(k)            : 새 도트를 그릴 캔버스 (높이 Hc, 마름모 줄 yc) → Sc.OVR 에 넣는다
  to_sheet(k, F)       : 완성 그림 F 를 조각 칸에 나눠 담은 시트 + 새 조각 사각형 목록(오른쪽으로 넓힘, 겹치거나 두 번 쓰이는 조각은 끔)"""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import yx_table
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PK = '/tmp/pk'
def ensure_pk():
    if not os.path.exists(PK + '/b0.d'):
        import subprocess; subprocess.run([sys.executable, '-I', root + '/tools/extract_pak.py', root + '/work/st2/data1.pak', PK], capture_output=True)
ensure_pk()
TABLE, YX_RAW = yx_table.load()
def orig(k): return Image.open(f'{PK}/b{k}.d').convert('RGBA')
def info(k):
    r = TABLE[k]; main = r['rects'][0]; fr = r['frames'][-1]; MY = [e for e in fr if e[0] == 0][0][2]
    return r, main, MY
def canvas(k):
    r, main, MY = info(k); return (MY + main[3] + 16, MY + main[3] - 8)
def entries(k):
    """모든 프레임에 나오는 (조각번호, x, y) 를 그리는 순서대로(중복 제거)."""
    r = TABLE[k]; out = []
    for f in r['frames']:
        for e in f:
            if e not in out: out.append(e)
    return out
def compose(k, S, rects=None, frame=-1):
    r, main, MY = info(k); rs = rects or r['rects']; H = S.size[1]; Hc = MY + main[3] + 16
    C = Image.new('RGBA', (max(96, S.size[0]) + 8, Hc), (0, 0, 0, 0))
    C.alpha_composite(S.crop((64, H - 8, 96, H)), (32, MY + main[3] + 8)); C.alpha_composite(S.crop((0, H - 8, 64, H)), (16, MY + main[3]))
    for (i, x, y) in r['frames'][frame]:
        q = rs[i]
        if q[2] <= 1: continue
        C.alpha_composite(S.crop((q[0], q[1], q[0] + q[2], q[1] + q[3])), (x, y))
    return C
def to_sheet(k, F):
    r, main, MY = info(k); rs = r['rects']; H = orig(k).size[1]; W = orig(k).size[0]; mh = main[3]
    S = Image.new('RGBA', (W, H), (0, 0, 0, 0)); new = [(95, 0, 1, 1)] * len(rs)
    ents = entries(k); cnt = {}
    for e in ents: cnt[e[0]] = cnt.get(e[0], 0) + 1
    pos = {}
    for e in ents: pos.setdefault(e[0], (e[1], e[2]))
    order = sorted(pos, key=lambda i: -(rs[i][2] * rs[i][3])); taken = []
    def overlap(a, b): return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]
    for i in order:
        q = rs[i]
        if q[2] < 2 or q[3] < 2: continue
        if cnt[i] > 1 and i != 0: continue                       # 같은 조각이 여러 자리에 찍히는 것(깃발 등)은 끈다
        if any(overlap(q, t) for t in taken): continue           # 시트에서 다른 조각과 겹치는 덧조각은 끈다
        taken.append(q); new[i] = q
    # 오른쪽으로 넓히기: 같은 줄대의 다른 조각·시트 끝·조립 그림 끝(96)까지
    for i in order:
        q = new[i]
        if q[2] <= 1 or i == 0: continue
        x, y = pos[i]; lim = min(W - 1 - q[0], 96 - x)
        for t in taken:
            if t is not rs[i] and t[1] < q[1] + q[3] and q[1] < t[1] + t[3] and t[0] >= q[0] + q[2]: lim = min(lim, t[0] - q[0])
        if lim > q[2]:
            nq = (q[0], q[1], lim, q[3]); taken[taken.index(rs[i])] = nq; new[i] = nq
    for i in order:
        q = new[i]
        if q[2] <= 1: continue
        x, y = pos[i]; S.paste(F.crop((x, y, x + q[2], y + q[3])), (q[0], q[1]))
    for rr in range(8):
        S.paste(F.crop((16, MY + mh + rr, 80, MY + mh + rr + 1)), (0, H - 8 + rr)); S.paste(F.crop((32, MY + mh + 8 + rr, 64, MY + mh + 9 + rr)), (64, H - 8 + rr))
    px = S.load()
    if not any(t[0] <= 95 < t[0] + t[2] and t[1] <= 0 < t[1] + t[3] for t in new if t[2] > 1) and W > 95: px[95, 0] = (0, 0, 0, 0)
    return S, new
def patch_yx(d, k, rects):
    start = TABLE[k]['start']
    for i, r in enumerate(rects): d[132 + 4 * (start + i): 136 + 4 * (start + i)] = bytes(r)
