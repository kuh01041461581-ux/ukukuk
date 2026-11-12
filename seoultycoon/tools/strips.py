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
def compose(k, S, rects=None, frame=-1, pos=None):
    r, main, MY = info(k); rs = rects or r['rects']; H = S.size[1]; Hc = MY + main[3] + 16
    C = Image.new('RGBA', (max(96, S.size[0]) + 8, Hc), (0, 0, 0, 0))
    C.alpha_composite(S.crop((64, H - 8, 96, H)), (32, MY + main[3] + 8)); C.alpha_composite(S.crop((0, H - 8, 64, H)), (16, MY + main[3]))
    for (i, x, y) in r['frames'][frame]:
        q = rs[i]
        if q[2] <= 1: continue
        if pos and i in pos: x, y = pos[i]
        C.alpha_composite(S.crop((q[0], q[1], q[0] + q[2], q[1] + q[3])), (x, y))
    return C
def to_sheet(k, F):
    """완성 그림 F(폭 96, 높이 canvas(k)[0])를 시트로. 반환 (시트, 새 조각 사각형 목록, 새 화면 위치 {조각: (x, y)}).
    조각의 시트 칸·크기·화면 위치는 모두 yx.d 의 값이므로, 메인 위쪽 전체(96 x MY)를 조각 하나에 담아 화면 (0,0)에 놓는다 → 건물 위쪽이 어디서도 잘리지 않는다.
    그 조각은 모든 프레임에 나오는 것 중에서 고르고, 나머지 덧조각은 끈다(1x1). 시트: [줄 0 비움][위쪽 조각][메인][바닥 끝 8줄]."""
    r, main, MY = info(k); rs = r['rects']; mh = main[3]
    common = [i for i in range(1, len(rs)) if all(any(e[0] == i for e in f) for f in r['frames'])]
    new = [(95, 0, 1, 1)] * len(rs); pos = {}; sy = 1; up = None
    if MY > 0 and common:
        up = common[0]; new[up] = (0, sy, 96, MY); pos[up] = (0, 0); sy += MY
    new[0] = (0, sy, 96, mh); H = sy + mh + 8
    S = Image.new('RGBA', (96, H), (0, 0, 0, 0))
    if up is not None: S.paste(F.crop((0, 0, 96, MY)), (0, 1))
    S.paste(F.crop((0, MY, 96, MY + mh)), (0, sy))
    for rr in range(8):
        S.paste(F.crop((16, MY + mh + rr, 80, MY + mh + rr + 1)), (0, H - 8 + rr)); S.paste(F.crop((32, MY + mh + 8 + rr, 64, MY + mh + 9 + rr)), (64, H - 8 + rr))
    px = S.load()
    for x in range(96): px[x, 0] = (0, 0, 0, 0)
    return S, new, pos
def coverage(k):
    """새 도트가 보일 수 있는 영역(조립 그림 좌표의 불리언 배열)."""
    Hc, yc = canvas(k); F = Image.new('RGBA', (96, Hc), (255, 255, 255, 255)); S, new, pos = to_sheet(k, F)
    return np.asarray(compose(k, S, new, pos=pos))[:, :96, 3] > 0
def patch_yx(d, k, rects, pos=None):
    r = TABLE[k]; start = r['start']
    for i, q in enumerate(rects): d[132 + 4 * (start + i): 136 + 4 * (start + i)] = bytes(q)
    for f, offs in zip(r['frames'], r['offs']):
        for e, o in zip(f, offs):
            if pos and e[0] in pos: d[o + 1] = pos[e[0]][0]; d[o + 2] = pos[e[0]][1]
