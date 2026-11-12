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
    """완성 그림 F(폭 96, 높이 canvas(k)[0])를 시트로.
    조각의 화면 위치(왼쪽 위)는 고정이지만 크기와 시트 안 위치는 자유이므로, 메인보다 위에 놓이는 조각마다 '화면 오른쪽 끝(96)·메인 윗변'까지 넓힌 칸을 주고
    새 시트에 세로로 차례로 쌓는다: [위쪽 조각들][메인][바닥 끝 8줄]. 메인 위에 덧그려지는 조각(차·깃발 등)은 끈다(1x1).
    반환 (시트, 새 조각 사각형 목록)."""
    r, main, MY = info(k); rs = r['rects']; mh = main[3]
    pos = {}
    for e in entries(k): pos.setdefault(e[0], (e[1], e[2]))
    ups = sorted([i for i in pos if i != 0 and pos[i][1] < MY and rs[i][2] >= 2], key=lambda i: (pos[i][1], pos[i][0]))
    # 같은 범위를 더 넓게 덮는 조각이 있으면 뺀다
    keep = []
    for i in ups:
        x, y = pos[i]
        if any(pos[j][0] <= x and pos[j][1] <= y for j in keep): continue
        keep.append(i)
    new = [(95, 0, 1, 1)] * len(rs); sy = 1; parts = []
    for i in keep:
        x, y = pos[i]; w, h = 96 - x, MY - y
        new[i] = (0, sy, w, h); parts.append((i, sy)); sy += h
    new[0] = (0, sy, 96, mh); H = sy + mh + 8
    S = Image.new('RGBA', (96, H), (0, 0, 0, 0))
    for i, y0 in parts:
        x, y = pos[i]; q = new[i]; S.paste(F.crop((x, y, x + q[2], y + q[3])), (0, y0))
    S.paste(F.crop((0, MY, 96, MY + mh)), (0, sy))
    for rr in range(8):
        S.paste(F.crop((16, MY + mh + rr, 80, MY + mh + rr + 1)), (0, H - 8 + rr)); S.paste(F.crop((32, MY + mh + 8 + rr, 64, MY + mh + 9 + rr)), (64, H - 8 + rr))
    px = S.load()
    for x in range(96): px[x, 0] = (0, 0, 0, 0)                 # 줄 0 은 비워 둔다(꺼진 조각이 가리키는 (95,0))
    return S, new
def coverage(k):
    """새 도트가 보일 수 있는 영역(조립 그림 좌표의 불리언 배열)."""
    Hc, yc = canvas(k); F = Image.new('RGBA', (96, Hc), (255, 255, 255, 255)); S, new = to_sheet(k, F)
    return np.asarray(compose(k, S, new))[:, :96, 3] > 0
def patch_yx(d, k, rects):
    start = TABLE[k]['start']
    for i, r in enumerate(rects): d[132 + 4 * (start + i): 136 + 4 * (start + i)] = bytes(r)
