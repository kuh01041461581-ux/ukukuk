"""익산시청 건물 그림 생성기 (서울타이쿤2 시청 자리 = 지도 청크 d64/d74/d65/d75 2x2, 128x128).
원본 청크(assets/st2_pak/png)에서 기존 시청(ㄷ자 건물)을 지우고 같은 아이소메트릭(2:1) 도트로 익산시청을 그려
work/pak_mod/{d64,d74,d65,d75}.png 로 쓴다. tools/st2_pak_build.py 가 이를 data1.pak 에 묶는다.
참고한 실물: 회색 격자창 12층 슬래브, 정면 한쪽이 계단식으로 깎인 유리 보이드, 베이지색 저층부, 옥상 모서리 다색 로고, 청색 현수막.
사용: python3 -I tools/make_iksan_cityhall.py [미리보기.png]"""
import os, sys
from PIL import Image, ImageDraw
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

# ---- 2) 새 건물 (기준: 앞 모서리 바닥 F) ----
d = ImageDraw.Draw(base)
F = (58, 90)             # 앞 모서리 바닥점
P_LEN, Q_LEN, H = 34, 40, 48      # 오른면 길이(p), 왼면 길이(q), 높이
def pt(p, q, z): return (F[0] + p - q, F[1] - (p + q) // 2 - z)
def poly(pts, col): d.polygon(pts, fill=col)

L_BASE, L_SHADE = hexc('c7ccd2'), hexc('aeb4bc')     # 왼면(남서, 밝음)
R_BASE, R_SHADE = hexc('9aa1ab'), hexc('858c97')     # 오른면(남동, 어두움)
WIN_L, WIN_R = hexc('6f8fb4'), hexc('4d6585')
ROOF = hexc('dfe3e8'); ROOF_E = hexc('8d949e'); EDGE = hexc('565d68')

# 바닥 그림자 (오른쪽 아래로 길게)
sh = [pt(0, 0, 0), pt(P_LEN, 0, 0), pt(P_LEN + 10, -6, 0), pt(10, -6, 0)]
for yy in range(128):
    for xx in range(128):
        pass
shadow = Image.new('RGBA', (128, 128), (0, 0, 0, 0)); ImageDraw.Draw(shadow).polygon(
    [pt(0, 0, 0), pt(P_LEN, 0, 0), pt(P_LEN + 12, -8, 0), pt(14, -8, 0)], fill=(40, 45, 30, 110))
base.alpha_composite(shadow); d = ImageDraw.Draw(base); px = base.load()

# 왼면(q 방향 면: p=0): 꼭짓점 (0,0)->(0,Q_LEN)
poly([pt(0, 0, 0), pt(0, Q_LEN, 0), pt(0, Q_LEN, H), pt(0, 0, H)], L_BASE)
# 오른면(p 방향 면: q=0)
poly([pt(0, 0, 0), pt(P_LEN, 0, 0), pt(P_LEN, 0, H), pt(0, 0, H)], R_BASE)
# 지붕
poly([pt(0, 0, H), pt(P_LEN, 0, H), pt(P_LEN, Q_LEN, H), pt(0, Q_LEN, H)], ROOF)
# 뒷면은 안 보임. 지붕 테두리
d.line([pt(0, 0, H), pt(P_LEN, 0, H), pt(P_LEN, Q_LEN, H), pt(0, Q_LEN, H), pt(0, 0, H)], fill=ROOF_E)
d.line([pt(0, 0, 0), pt(0, 0, H)], fill=EDGE)                       # 앞 모서리 세로선
d.line([pt(0, Q_LEN, 0), pt(0, Q_LEN, H)], fill=R_SHADE)
d.line([pt(P_LEN, 0, 0), pt(P_LEN, 0, H)], fill=R_SHADE)

def put(p, q, z, col):
    x, y = pt(p, q, z)
    if 0 <= x < 128 and 0 <= y < 128: px[x, y] = col

# 창 격자: 층당 4px, 가로 4px 간격(창 2px)
FL = 4
for q in range(1, Q_LEN - 1):                      # 왼면
    for z in range(FL, H - 1):
        if (q % 4) in (1, 2) and (z % FL) in (1, 2, 3): put(0, q, z, WIN_L if (z // FL) % 2 else hexc('7c9bbd'))
for p in range(1, P_LEN - 1):                      # 오른면
    for z in range(FL, H - 1):
        if (p % 4) in (1, 2) and (z % FL) in (1, 2, 3): put(p, 0, z, WIN_R)
# 층 구분선(콘크리트 띠)
for z in range(FL * 2, H, FL * 3):
    for q in range(0, Q_LEN): put(0, q, z, L_SHADE)
    for p in range(0, P_LEN): put(p, 0, z, R_SHADE)

# 계단식 유리 보이드(오른면 가운데): p 12~30, z 8~36 — 왼쪽 가장자리가 계단
VOID_G, VOID_D, LOUV = hexc('34506f'), hexc('263d57'), hexc('b8bfc8')
steps = [(8, 14), (14, 12), (20, 10), (26, 8)]        # (z 시작, p 시작): 위로 갈수록 오른쪽으로
for z in range(8, 37):
    p0 = 12
    for zs, ps in steps[::-1]:
        pass
    p0 = 12 + max(0, (z - 8) // 7) * 2 if z < 22 else 12 + (36 - z) // 7 * 2
    for p in range(p0, 31):
        put(p, 0, z, VOID_G if (z % 3) else VOID_D)
for z in range(8, 37, 4):                             # 층 유리 난간선
    for p in range(12 + (z - 8) // 7 * 2 if z < 22 else 12, 31): put(p, 0, z, hexc('6d8fb3'))
for p in range(30, 33):                                # 보이드 오른쪽 루버(대각 선)
    for z in range(8, 37, 2): put(p, 0, z, LOUV)

# 저층부(베이지 2층 부속동): 앞-왼쪽으로 튀어나옴 p∈[-14,12], q∈[10,34], z∈[0,11]
BEI_L, BEI_R, BEI_T, BEI_W = hexc('e8d9a6'), hexc('cdbd86'), hexc('f1e6bd'), hexc('7f98b3')
pp0, pp1, qq0, qq1, ZH = -18, 0, 6, 36, 12      # pp1=0: 타워 왼쪽 면(p=0)에 딱 붙임
# 접지 그림자: 저층부 바닥 둘레(오른쪽·아래)에 어두운 띠를 먼저 깐다
gs = Image.new('RGBA', (128, 128), (0, 0, 0, 0)); gd = ImageDraw.Draw(gs)
def ptg(p, q, z): return pt(p, q, z)
gd.polygon([ptg(pp0, qq0, 0), ptg(pp1, qq0, 0), (ptg(pp1, qq0, 0)[0] + 5, ptg(pp1, qq0, 0)[1] + 3), (ptg(pp0, qq0, 0)[0] + 5, ptg(pp0, qq0, 0)[1] + 3)], fill=(30, 40, 30, 120))
gd.polygon([ptg(pp0, qq1, 0), ptg(pp0, qq0, 0), (ptg(pp0, qq0, 0)[0] - 2, ptg(pp0, qq0, 0)[1] + 3), (ptg(pp0, qq1, 0)[0] - 3, ptg(pp0, qq1, 0)[1] + 2)], fill=(30, 40, 30, 90))
base.alpha_composite(gs); d = ImageDraw.Draw(base); px = base.load()
poly([pt(pp0, qq0, 0), pt(pp0, qq1, 0), pt(pp0, qq1, ZH), pt(pp0, qq0, ZH)], BEI_L)       # 왼면(p=pp0)
poly([pt(pp0, qq0, 0), pt(pp1, qq0, 0), pt(pp1, qq0, ZH), pt(pp0, qq0, ZH)], BEI_R)       # 오른면(q=qq0)
poly([pt(pp0, qq0, ZH), pt(pp1, qq0, ZH), pt(pp1, qq1, ZH), pt(pp0, qq1, ZH)], BEI_T)      # 지붕
d.line([pt(pp0, qq0, 0), pt(pp0, qq0, ZH)], fill=hexc('9c8c5c'))
for q in range(qq0 + 2, qq1 - 1):                    # 왼면 세로창
    for z in (2, 3, 4, 7, 8, 9):
        if q % 3 != 0: put(pp0, q, z, BEI_W)
for p in range(pp0 + 2, pp1 - 1):
    for z in (2, 3, 4, 7, 8, 9):
        if p % 3 != 0: put(p, qq0, z, hexc('6f87a3'))
d.line([pt(pp0, qq0, 6), pt(pp1, qq0, 6)], fill=hexc('b3a46e'))
for p in range(-11, -6):                              # 정문(어두운 유리문)
    for z in range(0, 5): put(p, qq0, z, hexc('3b4f66') if z else hexc('242f3f'))
d.line([pt(pp0, qq0, ZH), pt(pp1, qq0, ZH)], fill=hexc('fff6d6'))                # 지붕 앞 가장자리 밝은 선
d.line([pt(pp0, qq0, 0), pt(pp1, qq0, 0)], fill=hexc('8d7e52'))                  # 바닥 접지선
d.line([pt(pp0, qq0, 0), pt(pp0, qq1, 0)], fill=hexc('8d7e52'))

# 현수막(왼면 위쪽, 파랑·흰색): q 3~15, z 22~38
for q in range(3, 16):
    for z in range(22, 39):
        t = (z - 22) / 16
        col = hexc('f4f7fb') if z > 31 or q % 5 == 0 else (hexc('2f86c6') if z > 26 else hexc('1c6aa5'))
        if z < 26 and (q + z) % 4 == 0: col = hexc('9fd0ee')
        put(0, q, z, col)
d.line([pt(0, 3, 22), pt(0, 3, 38)], fill=hexc('50a0d0')); d.line([pt(0, 15, 22), pt(0, 15, 38)], fill=hexc('50a0d0'))

# 옥상 다색 로고 블록(익산 시 상징처럼 층진 색 블록) + 작은 옥탑
LOGO = [hexc('e84b8a'), hexc('f1a31a'), hexc('3aa0e6'), hexc('7a3fb0'), hexc('3bb55e')]
for i, c in enumerate(LOGO):
    p0 = 4 + i * 2
    for dp in range(2):
        for dz in range(2): put(p0 + dp, 1, H + 1 + dz + (i % 2), c); put(p0 + dp, 1, H + 1 + dz + (i % 2), c)
for p in range(6, 14):
    for q in range(8, 16):
        pass
# 옥탑 박스
poly([pt(18, 10, H), pt(26, 10, H), pt(26, 10, H + 4), pt(18, 10, H + 4)], hexc('a7aeb8'))
poly([pt(18, 10, H), pt(18, 18, H), pt(18, 18, H + 4), pt(18, 10, H + 4)], hexc('c0c6ce'))
poly([pt(18, 10, H + 4), pt(26, 10, H + 4), pt(26, 18, H + 4), pt(18, 18, H + 4)], hexc('e8ebef'))

os.makedirs(out, exist_ok=True)
for n, (ox, oy) in CH.items():
    base.crop((ox, oy, ox + 64, oy + 64)).convert('RGB').save(f'{out}/{n}.png')
prev = sys.argv[1] if len(sys.argv) > 1 else None
if prev:
    t = Image.new('RGB', (128 * 2 * 3 + 8, 128 * 3)); t.paste(orig.convert('RGB').resize((384, 384), Image.NEAREST), (0, 0))
    t.paste(base.convert('RGB').resize((384, 384), Image.NEAREST), (392, 0)); t.save(prev)
print('written', out)
