"""익산시청+익산시의회 그림 생성기(고품질) (서울타이쿤2 시청 자리 = 지도 청크 d64/d74/d65/d75 2x2, 128x128).
원본 청크(assets/st2_pak/png)에서 기존 시청(ㄷ자 건물)을 지우고 8배 해상도에서 광원·유리 반사·그림자를 그린 뒤 면적 평균으로 줄여 익산시청을 그려
work/pak_mod/{d64,d74,d65,d75}.png 로 쓴다. tools/st2_pak_build.py 가 이를 data1.pak 에 묶는다.
참고한 실물: 회색 격자창 12층 슬래브, 정면 한쪽이 계단식으로 깎인 유리 보이드, 베이지색 저층부, 옥상 모서리 다색 로고, 청색 현수막.
사용: python3 -I tools/make_iksan_cityhall.py [미리보기.png]"""
import os, sys
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = root + '/assets/st2_pak/png/'
out = root + '/work/pak_mod'
CH = {'d64': (0, 0), 'd74': (64, 0), 'd65': (0, 64), 'd75': (64, 64)}

base = Image.new('RGBA', (128, 128))
for n, p in CH.items(): base.alpha_composite(Image.open(src + n + '.png').convert('RGBA'), p)
orig = base.copy()
px = base.load()

def hexc(h): return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)
PL_A, PL_B = hexc('92ac75'), hexc('989887')            # 광장 바닥 체크(주기 4x2)

# ---- 1) 기존 시청 지우기: 건물·그림자 영역을 광장 바닥으로 (잔디는 남김) ----
old = [(12, 42), (50, 24), (64, 21), (74, 24), (86, 32), (103, 38), (104, 52), (101, 70), (84, 88), (54, 88), (12, 64)]
mask = Image.new('L', (128, 128), 0); ImageDraw.Draw(mask).polygon(old, fill=255)
mk = mask.load()
# 폴리곤 밖에 있는 색 = 바닥/주변 색. 폴리곤 안에서 그 색이 아닌 것만 건물·그림자로 보고 지운다.
ground = set(px[x, y] for y in range(128) for x in range(128) if not mk[x, y])
def is_lawn(c): return c[1] > c[0] + 25 and c[1] > c[2] + 40 and c[1] > 120 and c[0] < 140
def nearest_ground(x, y):
    for r in range(1, 12):
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                X, Y = x + dx, y + dy
                if 0 <= X < 128 and 0 <= Y < 128 and not mk[X, Y]: return px[X, Y]
    return PL_A
orig_px = orig.load()
erase = [(x, y) for y in range(128) for x in range(128) if mk[x, y] and not is_lawn(orig_px[x, y])]
for x, y in erase:
    a = ((x % 4) in (0, 3)) != (y % 2 == 1)
    px[x, y] = PL_A if a else PL_B


# ---- 2) 8배 해상도로 새 건물 그리기: 바닥은 지운 원본을 그대로 확대(줄이면 원본 픽셀이 그대로 남는다) ----
S = 8
scene = base.resize((128 * S, 128 * S), Image.NEAREST).convert('RGBA')
rnd = random.Random(5)
F = (50, 92); P_LEN, Q_LEN, H = 30, 34, 48
def P(p, q, z): return ((F[0] + p - q) * S, (F[1] - (p + q) / 2.0 - z) * S)
def L(c0, c1, t): return tuple(int(c0[i] * (1 - t) + c1[i] * t) for i in range(3)) + (255,)
def grad(pts, c0, c1, d=(0, 1), noise=0.0):
    m = Image.new('L', scene.size, 0); ImageDraw.Draw(m).polygon(pts, fill=255); bb = m.getbbox()
    if not bb: return
    x0, y0, x1, y1 = bb; ys, xs = np.mgrid[y0:y1, x0:x1]; proj = xs * d[0] + ys * d[1]; t = ((proj - proj.min()) / max(1e-6, proj.max() - proj.min()))[..., None]
    col = np.array(c0[:3]) * (1 - t) + np.array(c1[:3]) * t
    if noise: col = col + np.random.RandomState(1).normal(0, noise, col.shape[:2])[..., None]
    arr = np.zeros((y1 - y0, x1 - x0, 4), np.uint8); arr[..., :3] = np.clip(col, 0, 255).astype(np.uint8); arr[..., 3] = np.asarray(m.crop(bb))
    scene.alpha_composite(Image.fromarray(arr, 'RGBA'), (x0, y0))
def flat(pts, c): ImageDraw.Draw(scene).polygon(pts, fill=c)
def soft_shadow(pts, a=120, blur=10, col=(30, 38, 26)):
    sh = Image.new('L', scene.size, 0); ImageDraw.Draw(sh).polygon(pts, fill=a); sh = sh.filter(ImageFilter.GaussianBlur(blur))
    lay = Image.new('RGBA', scene.size, col + (255,)); lay.putalpha(sh); scene.alpha_composite(lay)
def line(pts, c, w): ImageDraw.Draw(scene).line(pts, fill=c, width=w)

# 접지 그림자(오른쪽 아래로 길게) — 시청 / 의회 / 저층부
soft_shadow([P(0, 0, 0), P(P_LEN, 0, 0), P(P_LEN + 14, -12, 0), P(16, -14, 0)], 125, 12)
C0, C1, CQ, CZ = P_LEN + 4, P_LEN + 26, 20, 20
soft_shadow([P(C0, 0, 0), P(C1, 0, 0), P(C1 + 9, -8, 0), P(C0 + 6, -9, 0)], 105, 9)

# ================= 익산시의회(뒤쪽이므로 먼저) =================
grad([P(C0, 0, 0), P(C0, CQ, 0), P(C0, CQ, CZ), P(C0, 0, CZ)], (232, 235, 240), (196, 201, 212), (0.2, 1))       # 왼면(밝음)
grad([P(C0, 0, 0), P(C1, 0, 0), P(C1, 0, CZ), P(C0, 0, CZ)], (190, 196, 208), (150, 158, 172), (0.2, 1))        # 오른면(정면)
grad([P(C0, 0, CZ), P(C1, 0, CZ), P(C1, CQ, CZ), P(C0, CQ, CZ)], (246, 248, 250), (222, 226, 232), (1, 0.4))
for z0 in (2.5, 7.5, 12.5):                                                                            # 띠창(기둥 간격 6)
    for p in range(C0 + 1, C1 - 1, 6):
        flat([P(p, 0, z0), P(p + 5, 0, z0), P(p + 5, 0, z0 + 3.2), P(p, 0, z0 + 3.2)], (60, 90, 130, 255))
        grad([P(p + .2, 0, z0 + .2), P(p + 4.8, 0, z0 + .2), P(p + 4.8, 0, z0 + 3.0), P(p + .2, 0, z0 + 3.0)], (130, 170, 205), (56, 86, 124), (0.3, 1))
        line([P(p + 1, 0, z0 + 3.0), P(p + 2.4, 0, z0 + .4)], (210, 232, 250, 150), 3)
    for q in range(1, CQ - 1, 6):
        grad([P(C0, q + .2, z0 + .2), P(C0, q + 4.8, z0 + .2), P(C0, q + 4.8, z0 + 3.0), P(C0, q + .2, z0 + 3.0)], (150, 190, 224), (70, 104, 146), (0.3, 1))
line([P(C0, 0, CZ - 1), P(C1, 0, CZ - 1)], (214, 176, 80, 255), 8); line([P(C0, 0, CZ - 1), P(C0, CQ, CZ - 1)], (232, 196, 100, 255), 8)   # 금색 처마
for p in range(C0 + 3, C1, 6): line([P(p, 0, 0), P(p, 0, CZ - 1)], (214, 220, 230, 255), 5)               # 기둥 핀
sx0, sx1, sz0, sz1 = C0 + 5, C0 + 21, CZ + 0.5, CZ + 9.5                                                 # 금빛 루버 간판 + 상징
soft_shadow([P(sx0, 0, sz0), P(sx1, 0, sz0), P(sx1 + 3, -3, sz0 - 1), P(sx0 + 3, -3, sz0 - 1)], 80, 6)
grad([P(sx0, 0, sz0), P(sx1, 0, sz0), P(sx1, 0, sz1), P(sx0, 0, sz1)], (224, 186, 96), (150, 110, 44), (0.2, 1), 3)
for z in np.arange(sz0 + 0.8, sz1, 1.2): line([P(sx0, 0, z), P(sx1, 0, z)], (110, 78, 28, 200), 3); line([P(sx0, 0, z - 0.25), P(sx1, 0, z - 0.25)], (250, 224, 150, 200), 2)
sx, sy = P((sx0 + sx1) / 2, 0, (sz0 + sz1) / 2); ImageDraw.Draw(scene).ellipse([sx - 20, sy - 20, sx + 20, sy + 20], fill=(250, 242, 214, 255)); ImageDraw.Draw(scene).ellipse([sx - 12, sy - 12, sx + 12, sy + 12], outline=(160, 120, 50, 255), width=3)
line([P(sx0, 0, sz0), P(sx0, 0, sz1), P(sx1, 0, sz1), P(sx1, 0, sz0)], (96, 70, 26, 255), 4)

# ================= 시청 타워 =================
BASE_L, TOP_L = (214, 220, 230), (190, 198, 212)       # 왼면(밝음)
BASE_R, TOP_R = (170, 178, 192), (140, 148, 164)       # 오른면
grad([P(0, 0, 0), P(0, Q_LEN, 0), P(0, Q_LEN, H), P(0, 0, H)], TOP_L, BASE_L, (0.3, -1))                  # 위가 더 밝은 하늘 반사
grad([P(0, 0, 0), P(P_LEN, 0, 0), P(P_LEN, 0, H), P(0, 0, H)], TOP_R, BASE_R, (0.3, -1))
grad([P(0, 0, H), P(P_LEN, 0, H), P(P_LEN, Q_LEN, H), P(0, Q_LEN, H)], (246, 248, 251), (222, 228, 236), (1, 0.5))
# 창: 한 층 4px, 베이 3px(창 2 + 핀 1)
def win(face, u, z, w, h, sky):
    t = z / H; c0 = L((150, 190, 222) if sky else (118, 150, 186), (60, 90, 128), 0.25 + 0.5 * t * 0)
    if face == 'L': pts = [P(0, u, z), P(0, u + w, z), P(0, u + w, z + h), P(0, u, z + h)]
    else:           pts = [P(u, 0, z), P(u + w, 0, z), P(u + w, 0, z + h), P(u, 0, z + h)]
    shade = 0.0 if face == 'L' else 0.18
    flat(pts, L((70, 100, 140), (40, 64, 100), shade))
    inner = [(pts[0][0] + 1.5, pts[0][1] - 1.5), (pts[1][0] - 1.5, pts[1][1] - 1.5 + 0), (pts[2][0] - 1.5, pts[2][1] + 1.5), (pts[3][0] + 1.5, pts[3][1] + 1.5)]
    grad(inner, L((170, 208, 236), (130, 170, 205), shade), L((64, 96, 136), (40, 66, 104), shade), (0.2, 1), 3)
    ImageDraw.Draw(scene).line([((pts[2][0] + pts[3][0]) / 2 - 3, pts[2][1] + 3), (pts[1][0] - 3, pts[1][1] - 3)], fill=(230, 244, 255, 140), width=2)       # 반사 줄
for u in range(1, Q_LEN - 1, 3):
    flat([P(0, u + 2, 3), P(0, u + 3, 3), P(0, u + 3, H - 1), P(0, u + 2, H - 1)], (232, 236, 242, 255))                  # 세로 핀(밝음)
    line([P(0, u + 3, 3), P(0, u + 3, H - 1)], (150, 158, 172, 255), 2)
    for f in range(0, 11):
        if u + 2 <= Q_LEN - 1: win('L', u, 4 + f * 4 + 0.8, 2.0, 2.7, f % 3 == 0)
for u in range(1, P_LEN - 1, 3):
    flat([P(u + 2, 0, 3), P(u + 3, 0, 3), P(u + 3, 0, H - 1), P(u + 2, 0, H - 1)], (196, 202, 214, 255))
    line([P(u + 3, 0, 3), P(u + 3, 0, H - 1)], (110, 118, 134, 255), 2)
    for f in range(0, 11):
        if u + 2 <= P_LEN - 1: win('R', u, 4 + f * 4 + 0.8, 2.0, 2.7, f % 3 == 0)
for z in range(4, H, 4):                                                                                    # 층 그림자 띠(스팬드럴 아래)
    line([P(0, 0, z), P(0, Q_LEN, z)], (140, 150, 166, 120), 2); line([P(0, 0, z), P(P_LEN, 0, z)], (96, 104, 120, 120), 2)
# 계단식 유리 보이드(오른면): 왼쪽 가장자리가 계단, 오른쪽에 대각 루버
V0, V1 = 10, P_LEN - 3
def p0(z): return V0 + (max(0, (z - 8) // 7) * 2 if z < 22 else (36 - z) // 7 * 2)
zs = list(range(8, 37, 1))
vpts = [P(p0(z), 0, z) for z in zs] + [P(V1, 0, 36), P(V1, 0, 8)]
vp = [P(p0(8), 0, 8)] + [P(p0(z), 0, z) for z in zs] + [P(V1, 0, 36), P(V1, 0, 8)]
grad(vp, (32, 56, 92), (74, 112, 152), (0.2, -1), 3)
for z in range(8, 37, 4): line([P(p0(z), 0, z), P(V1, 0, z)], (150, 190, 224, 190), 3)                     # 층 난간
for z in range(8, 37, 7):                                                                                   # 계단 모서리 그림자
    soft_shadow([P(p0(z), 0, z), P(p0(z) + 6, 0, z), P(p0(z) + 6, 0, z - 6), P(p0(z), 0, z - 6)], 90, 4, (10, 20, 40))
for z in range(9, 36, 2): line([P(V1 + 0.2, 0, z), P(V1 + 2.6, 0, z + 1.2)], (200, 206, 216, 255), 3)      # 루버
flat([P(V1 + 2.6, 0, 8), P(V1 + 3.2, 0, 8), P(V1 + 3.2, 0, 37), P(V1 + 2.6, 0, 37)], (160, 168, 182, 255))
# 지붕 가장자리 + 장비 + 로고 블록 + 안테나
line([P(0, 0, H), P(P_LEN, 0, H), P(P_LEN, Q_LEN, H)], (120, 128, 142, 255), 4); line([P(0, 0, H), P(0, Q_LEN, H)], (200, 206, 216, 255), 4)
line([P(0, 0, 0), P(0, 0, H)], (84, 92, 108, 255), 5)                                                       # 앞 모서리 그림자 선
for (a, b, w, h, ht) in ((18, 10, 8, 8, 4), (8, 22, 6, 6, 3), (22, 24, 5, 5, 2.5)):                       # 옥상 설비 박스
    grad([P(a, b, H), P(a + w, b, H), P(a + w, b, H + ht), P(a, b, H + ht)], (180, 186, 198), (140, 148, 164), (0, 1))
    grad([P(a, b, H), P(a, b + h, H), P(a, b + h, H + ht), P(a, b, H + ht)], (206, 212, 222), (170, 176, 190), (0, 1))
    grad([P(a, b, H + ht), P(a + w, b, H + ht), P(a + w, b + h, H + ht), P(a, b + h, H + ht)], (240, 242, 246), (218, 222, 230), (1, 0))
for i, c in enumerate([(232, 74, 138), (244, 166, 28), (58, 160, 230), (122, 63, 176), (59, 181, 94)]):       # 시 상징 로고(옥상 층진 색 블록)
    a = 3 + i * 2.2; flat([P(a, 1, H), P(a + 2, 1, H), P(a + 2, 1, H + 1.6 + (i % 2) * 1.2), P(a, 1, H + 1.6 + (i % 2) * 1.2)], c + (255,))
    flat([P(a, 1, H + 1.6 + (i % 2) * 1.2), P(a + 2, 1, H + 1.6 + (i % 2) * 1.2), P(a + 2, 2.6, H + 1.6 + (i % 2) * 1.2), P(a, 2.6, H + 1.6 + (i % 2) * 1.2)], L(c + (255,), (255, 255, 255), 0.35))
x, y = P(24, 14, H + 4); line([(x, y), (x, y - 70)], (90, 96, 108, 255), 4); ImageDraw.Draw(scene).ellipse([x - 5, y - 76, x + 5, y - 66], fill=(230, 60, 50, 255))
# 현수막(왼면, 파랑·흰색 물결)
bq0, bq1, bz0, bz1 = 3, 15, 22, 39
bpts = [P(0, bq0, bz1), P(0, bq1, bz1), P(0, bq1, bz0 + 1.5)] + [P(0, bq1 - k, bz0 + 1.5 - (1.4 * __import__('math').sin(k * 0.9))) for k in range(0, 13)] + [P(0, bq0, bz0)]
grad(bpts, (252, 253, 255), (236, 244, 252), (0, 1))
wave = [P(0, bq0 + k * 0.5, bz0 + 7 + 1.5 * __import__('math').sin(k * 0.7)) for k in range(0, 25)]
flat(wave + [P(0, bq1, bz0 + 1.5), P(0, bq0, bz0)], (40, 128, 196, 255)); grad(wave + [P(0, bq1, bz0 + 1.5), P(0, bq0, bz0)], (90, 170, 226), (18, 88, 160), (0.3, 1))
for k in range(0, 24, 3): line([P(0, bq0 + k * 0.5, bz0 + 7 + 1.5 * __import__('math').sin(k * 0.7)), P(0, bq0 + k * 0.5 + 1.2, bz0 + 3)], (210, 236, 250, 160), 3)
for z in (bz1 - 2, bz1 - 4): line([P(0, bq0 + 1.5, z), P(0, bq1 - 1.5, z)], (150, 160, 178, 255), 2)               # 글줄(흐릿한 글자)
line([P(0, bq0, bz0), P(0, bq0, bz1), P(0, bq1, bz1), P(0, bq1, bz0 + 1.5)], (120, 128, 144, 255), 3)

# ================= 저층부(베이지 부속동) =================
pp0, pp1, qq0, qq1, ZH = -14, 0, 6, 30, 12
soft_shadow([P(pp0, qq0, 0), P(pp1, qq0, 0), P(pp1 + 7, qq0 - 6, 0), P(pp0 + 7, qq0 - 6, 0)], 115, 7)
grad([P(pp0, qq0, 0), P(pp0, qq1, 0), P(pp0, qq1, ZH), P(pp0, qq0, ZH)], (246, 232, 186), (212, 194, 140), (0.2, 1), 3)
grad([P(pp0, qq0, 0), P(pp1, qq0, 0), P(pp1, qq0, ZH), P(pp0, qq0, ZH)], (214, 196, 142), (176, 158, 108), (0.2, 1), 3)
grad([P(pp0, qq0, ZH), P(pp1, qq0, ZH), P(pp1, qq1, ZH), P(pp0, qq1, ZH)], (252, 244, 214), (236, 224, 184), (1, 0.5))
for z0 in (1.8, 7.0):
    for q in range(qq0 + 2, qq1 - 1, 3):
        grad([P(pp0, q, z0), P(pp0, q + 1.6, z0), P(pp0, q + 1.6, z0 + 3.0), P(pp0, q, z0 + 3.0)], (176, 210, 236), (84, 112, 150), (0.2, 1))
    for p in range(pp0 + 2, pp1 - 2, 3):
        grad([P(p, qq0, z0), P(p + 1.6, qq0, z0), P(p + 1.6, qq0, z0 + 3.0), P(p, qq0, z0 + 3.0)], (150, 188, 220), (66, 94, 132), (0.2, 1))
line([P(pp0, qq0, 5.6), P(pp1, qq0, 5.6)], (178, 162, 112, 255), 4)
for p in range(-9, -4): flat([P(p, qq0, 0), P(p + 1, qq0, 0), P(p + 1, qq0, 4.6), P(p, qq0, 4.6)], (46, 64, 90, 255))     # 정문
line([P(-10, qq0, 5.0), P(-4, qq0, 5.0)], (238, 230, 196, 255), 6)                                                    # 캐노피
line([P(pp0, qq0, ZH), P(pp1, qq0, ZH)], (255, 249, 226, 255), 4); line([P(pp0, qq0, ZH), P(pp0, qq1, ZH)], (255, 249, 226, 255), 4)
line([P(pp0, qq0, 0), P(pp1, qq0, 0)], (120, 106, 70, 255), 3); line([P(pp0, qq0, 0), P(pp0, qq1, 0)], (120, 106, 70, 255), 3)

# ---- 3) 면적 평균으로 줄이기 ----
arr = np.asarray(scene).astype(float)[..., :3].reshape(128, S, 128, S, 3).mean((1, 3))
res = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB').convert('RGBA')
# 건물이 닿지 않은 바닥은 원본 픽셀 그대로(보정 오차 제거)
od = orig.convert('RGB'); keep = Image.new('L', (128, 128), 0); keep_px = keep.load()
sc_arr = np.asarray(scene)[..., :3].astype(int); up = np.asarray(base.resize((128 * S, 128 * S), Image.NEAREST).convert('RGB')).astype(int)
changed = (np.abs(sc_arr - up).sum(2) > 6).reshape(128, S, 128, S).any((1, 3))
resa = np.asarray(res).copy(); basea = np.asarray(base.convert('RGBA'))
resa[~changed] = basea[~changed]
base = Image.fromarray(resa, 'RGBA')

os.makedirs(out, exist_ok=True)
for n, (ox, oy) in CH.items():
    base.crop((ox, oy, ox + 64, oy + 64)).convert('RGB').save(f'{out}/{n}.png')
prev = sys.argv[1] if len(sys.argv) > 1 else None
if prev:
    t = Image.new('RGB', (128 * 6 + 8, 128 * 3)); t.paste(orig.convert('RGB').resize((384, 384), Image.NEAREST), (0, 0))
    t.paste(base.convert('RGB').resize((384, 384), Image.NEAREST), (392, 0)); t.save(prev)
print('written', out)
