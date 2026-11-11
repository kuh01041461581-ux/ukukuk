"""게임의 건물 조각 규칙(yx.d)에 맞춰 시트를 만드는 공용 도구.

게임이 건물을 그리는 법(건설 목록 미리보기·메뉴 화면의 실제 그리기 기록으로 확인):
  시트 = [위쪽 띠 조각들(줄 0..my)] + [메인 (0,my,96,mh)] + [맨 아래 8줄 = 바닥 끝 조각]
  · 메인은 제자리.  · 메인에 바로 얹힌 띠 조각(아랫변이 my 에 닿는 것, 그 위로 이어 쌓인 것)은 '세로는 제자리, 가로만 조각마다 정해진 만큼 이동'.
  · 그 밖의 조각(옆에 따로 놓인 탑 꼭대기·차·깃발 등)은 따로 정해진 자리에 그려진다 → 새로 그린 건물에서는 yx.d 에서 1x1 로 줄여 끈다.
  · 맨 아래 8줄: 왼쪽 64칸은 (16, my+mh) 에, 오른쪽 32칸은 (32, my+mh+8) 에 그려져 마름모 바닥의 아래 끝을 만든다.
완성 그림 F(폭 96, 높이 H+8; 바닥 마름모는 2:1, 가장 넓은 줄 yc)를 받아 시트로 잘라 넣는다."""
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
GROUPS, YX_RAW = yx_table.load()
# 표 해석 보정(메인이 없는 건물 때문에 밀린 묶음)
_g33 = GROUPS[33][1]
GROUPS[35] = (GROUPS[33][0] + 8, _g33[8:13]); GROUPS[36] = (GROUPS[33][0] + 13, _g33[13:17]); GROUPS[33] = (GROUPS[33][0], _g33[:3])
GROUPS[37] = GROUPS[34]; del GROUPS[34]
# 게임 기록으로 확인한 띠 조각의 화면 왼쪽 x (건물: {조각: x})
KNOWN_L = {11: {(0, 0, 70, 24): 12}, 10: {(0, 19, 62, 9): 10, (0, 0, 51, 19): 9}, 19: {(13, 0, 50, 26): 18}, 9: {(27, 0, 59, 26): 27}, 47: {(14, 0, 63, 20): 14},
           40: {(28, 0, 57, 21): 28}, 0: {(0, 17, 19, 6): 22, (19, 0, 27, 23): 41}, 52: {(0, 0, 78, 17): 8}, 49: {(44, 0, 53, 14): 23}, 35: {(20, 0, 51, 20): 20}}
def orig(k): return Image.open(f'{PK}/b{k}.d').convert('RGBA')
def bands(k):
    """메인 위에 제자리로 쌓이는 띠 조각 [(rect, below_rect)] (아래에서 위로). 층마다 가장 큰 조각 하나만 띠로 쓴다(나머지는 따로 그려지는 덧조각)."""
    g = GROUPS[k][1]; main = g[0]; out = []; base = main
    while True:
        cands = [p for p in g[1:] if p[2] >= 12 and p[3] >= 3 and p[1] + p[3] == base[1] and (base is main or (p[0] < base[0] + base[2] and base[0] < p[0] + p[2]))]
        if not cands: break
        p = max(cands, key=lambda r: r[2] * r[3]); out.append((p, base)); base = p
    return out
def solve_L(k, p, base, baseL):
    """원본 그림의 이어짐으로 띠 조각 p 의 화면 왼쪽 x 를 추정(아래 조각 base 의 화면 왼쪽 baseL)."""
    S = np.asarray(orig(k)).astype(float); pc = S[p[1]:p[1] + p[3], p[0]:p[0] + p[2]]; bc = S[base[1]:base[1] + base[3], base[0]:base[0] + base[2]]
    best = None
    for L in range(-10, 60):
        sc = 0; bad = 0
        for r in range(min(2, pc.shape[0], bc.shape[0])):
            for i in range(pc.shape[1]):
                c = pc[pc.shape[0] - 1 - r, i]
                if c[3] == 0: continue
                xb = L + i - baseL
                if xb < 0 or xb >= bc.shape[1] or bc[r, xb, 3] == 0: bad += 1; continue
                sc += 1 / (1 + np.abs(c[:3] - bc[r, xb, :3]).sum() / 40)
        s = sc - 2 * bad - abs(L - p[0]) * 0.05                        # 비기면 제자리(원래 x)를 우선
        if best is None or s > best[0]: best = (s, L)
    return best[1]
def layout(k):
    """[(rect, L, Cy_top)]: 메인 포함. C 좌표 = 시트 좌표와 같은 줄, x 는 L 부터."""
    g = GROUPS[k][1]; main = g[0]; res = [(main, 0, main[1])]; Ls = {main: 0}
    for p, base in bands(k):
        L = KNOWN_L.get(k, {}).get(p)
        if L is None: L = solve_L(k, p, base, Ls[base])
        Ls[p] = L; res.append((p, L, p[1]))
    return res
def yc_of(k):
    """바닥 마름모의 가장 넓은 줄(시트 줄 번호)."""
    a = np.asarray(orig(k))[..., 3] > 0; main = GROUPS[k][1][0]
    w = [(a[y].sum(), -abs(y - (main[1] + main[3] - 8)), y) for y in range(main[1], main[1] + main[3])]
    return max(w)[2]
def to_sheet(k, F, widen=True):
    """완성 그림 F(96 x (H+8))를 k 번 건물 시트로. 반환 (시트, yx 조각 새 목록)."""
    H = orig(k).size[1]; W = orig(k).size[0]; g = GROUPS[k][1]; main = g[0]; my, mh = main[1], main[3]
    S = Image.new('RGBA', (W, H), (0, 0, 0, 0)); new_rects = {main: main}
    S.alpha_composite(F.crop((0, my, 96, my + mh)), (0, my))
    lay = layout(k)
    for (p, L, cy) in lay[1:]:
        sx, sy, w, h = p
        if widen:                                                         # 오른쪽 끝까지 넓힌다(시트 안에서 겹치지 않는 범위)
            w2 = min(96 - L, 95 - sx)
            for (q, _, _) in lay[1:]:
                if q is not p and q[1] < sy + h and sy < q[1] + q[3] and q[0] > sx: w2 = min(w2, q[0] - sx)
            w = max(w, w2) if w2 > 0 else w
        S.alpha_composite(F.crop((L, cy, L + w, cy + h)), (sx, sy)); new_rects[p] = (sx, sy, w, h)
    for r in range(8):                                                    # 바닥 끝 두 조각
        row1 = F.crop((16, my + mh + r, 80, my + mh + r + 1)); S.paste(row1, (0, H - 8 + r))
        row2 = F.crop((32, my + mh + 8 + r, 64, my + mh + 8 + r + 1)); S.paste(row2, (64, H - 8 + r))
    px = S.load(); px[95, 0] = (0, 0, 0, 0)
    rects = [new_rects.get(p, (95, 0, 1, 1)) for p in g]                # 띠가 아닌 조각은 끈다
    return S, rects
def compose(k, S, rects=None):
    """게임이 그리는 대로 시트를 조립한 모습(검증·전시용). 반환 96 x (H+8)."""
    H = S.size[1]; g = GROUPS[k][1]; main = g[0]; my, mh = main[1], main[3]
    C = Image.new('RGBA', (96, H + 8), (0, 0, 0, 0))
    for r in range(8):
        C.alpha_composite(S.crop((64, H - 8 + r, 96, H - 7 + r)), (32, my + mh + 8 + r)); C.alpha_composite(S.crop((0, H - 8 + r, 64, H - 7 + r)), (16, my + mh + r))
    C.alpha_composite(S.crop((0, my, 96, my + mh)), (0, my))
    for i, (p, L, cy) in enumerate(layout(k)[1:]):
        q = p
        if rects is not None:
            q = rects[g.index(p)]
        C.alpha_composite(S.crop((q[0], q[1], q[0] + q[2], q[1] + q[3])), (L, cy))
    return C
def patch_yx(d, k, rects):
    start = GROUPS[k][0]; g = GROUPS[k][1]
    for i, r in enumerate(rects): d[132 + 4 * (start + i): 136 + 4 * (start + i)] = bytes(r)
if __name__ == '__main__':
    for k in (11, 10, 19, 9, 47, 40, 0, 52, 49, 12, 3, 4, 13, 15, 20, 33):
        g = GROUPS[k][1]; main = g[0]; Ls = {main: 0}; out = []
        for p, base in bands(k):
            s = solve_L(k, p, base, Ls[base]); kn = KNOWN_L.get(k, {}).get(p); Ls[p] = kn if kn is not None else s; out.append((p, 'solve', s, 'known', kn))
        print(k, 'H', orig(k).size[1], 'yc', yc_of(k), out)
