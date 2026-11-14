"""익산 모드 도트 전체를 한 번에 만든다(빌드 스크립트가 이것만 부른다).
건물은 게임의 조각 규칙(tools/strips.py)에 맞춰: 2:1 바닥 + 높이 H+8 로 그린 완성 그림을 조각 칸에 잘라 넣고, yx.d 의 덧조각을 끈다.
사용: python3 -I tools/build_sprites.py [전시그림.png]"""
import os, sys, subprocess, glob
from PIL import Image
here = os.path.dirname(os.path.abspath(__file__)); root = os.path.dirname(here); sys.path.insert(0, here)
out = root + '/work/pak_mod'; os.makedirs(out, exist_ok=True)
for f in glob.glob(out + '/*.png'): os.remove(f)
def run(tool): subprocess.run([sys.executable, '-I', f'{here}/{tool}'], check=True, stdout=subprocess.DEVNULL)
# 1) 지형·기타(조각 규칙과 무관)
for tool in ('make_iksan_cityhall.py', 'make_iksan_quarry3d.py', 'make_iksan_palace.py', 'make_iksan_retouch.py', 'make_iksan_minimap.py', 'make_iksan_logo.py', 'make_iksan_misc.py', 'make_iksan_secnames.py', 'make_iksan_faces.py', 'gpt_import.py', 'faces_hd.py', 'make_maryong.py'): run(tool)
# 2) 건물: 조각 규칙 변환
import strips as ST
import pixel_style as PS
from make_iksan_buildings import Sc
import make_iksan_buildings as MB, make_iksan_hq as HQ, make_iksan_more as MO, make_iksan_civic as CV, make_iksan_landmarks as LM, make_iksan_icons as IC, make_iksan_chunpo as CK, make_iksan_arts as AR, make_iksan_jeonsu as JS
# 익산 고유 건물만 새 도트를 쓴다. 그 밖의 건물(소방서·경찰서·학교·아파트·버스정류장·유치원·놀이터·체육공원 등)은 원본 도트를 그대로 두고 퀄리티만 올린다(enhance_orig).
ITEMS = {2: LM.mireuksa, 13: CV.church, 20: CV.library, 21: CK.chunpo, 19: MB.cinema, 33: HQ.baseball, 38: MO.baekje, 42: AR.arts, 26: JS.jeonsu}
# 롯데시네마·코스트코·테슬라·하림·농협은행·공설운동장·방송국·마룡이식당은 원본 도트에 간판만 고친다(make_iksan_retouch) → 아래에서 퀄리티 보정까지 받는다
yx = bytearray(open(root + '/work/st2/yx.d', 'rb').read()); show = []; hd_show = []
def on_tile(F, yc, s=1):
    """바닥 마름모의 아래쪽 변보다 밑(바닥 두께·그림자)은 지운다 → 원본처럼 타일에 딱 붙는다(공중에 뜬 것처럼 보이지 않게)."""
    F = F.copy(); px = F.load()
    for y in range(yc * s, F.height):
        for x in range(F.width):
            if y / s > yc + 24 - abs(x / s - 48) * 24 / 47.0: px[x, y] = (0, 0, 0, 0)
    return F
def horse(dx=0, dy=0, hd=False):
    Hc, yc = ST.canvas(9); tmp = '/tmp/_iksan_b9.png'
    subprocess.run([sys.executable, '-I', f'{here}/make_iksan_horse.py'], check=True, stdout=subprocess.DEVNULL, env=dict(os.environ, IKSAN_OVR=f'{Hc},{yc}', IKSAN_OUT=tmp, IKSAN_DX=str(dx), IKSAN_DY=str(dy)))
    return Image.open(tmp + '.hd.png' if hd else tmp).convert('RGBA')
ITEMS[9] = horse
for k, fn in sorted(ITEMS.items()):
    Hc, yc = ST.canvas(k)
    Sc.OVR = (Hc, yc); Sc.LAST_HD = None; Sc.ZS = 1.0; Sc.FS = {20: 1.16, 21: 1.16, 41: 1.12}.get(k, 1.0)      # 레퍼런스가 뚜렷하지 않은 건물은 규모를 키운다
    try:
        F = fn()
        if k not in (9, 38):                                          # 원작 건물처럼 캔버스 윗부분까지 쓰도록 높이를 자동으로 키운다(최대 1.5배, 미륵석탑은 1.3배)
            ys = [y for y in range(F.height) if any(F.getpixel((x, y))[3] for x in range(F.width))]
            if ys and ys[0] > 6:
                Sc.ZS = max(1.0, min(1.3 if k == 2 else (1.9 if k == 13 else (2.2 if k == 21 else 1.6)), (yc - 5) / max(1.0, yc - ys[0]))); Sc.LAST_HD = None
                if Sc.ZS > 1.02: F = fn()
    finally: Sc.OVR = None; Sc.ZS = 1.0; Sc.FS = 1.0
    F2 = horse(0, 0, True) if k == 9 else Sc.LAST_HD                                              # 2배 해상도판(없으면 1배 그림만)
    if F.size != (96, Hc): Fx = Image.new('RGBA', (96, Hc), (0, 0, 0, 0)); Fx.alpha_composite(F.crop((0, 0, 96, min(F.height, Hc)))); F = Fx
    F = on_tile(F, yc)
    if k != 9: F = PS.stylize(F)
    # 애니메이션 프레임: 구성물이 실제로 움직이는 변형 그림을 우선 쓰고(승마장: 말이 울타리 안을 걷는다), 없으면 불빛 변형
    vs = [on_tile(horse(1.0, 0.5), yc)] if k == 9 else ([PS.twinkle(F, 0, 1, 23, True), PS.twinkle(F, 1, 1, 23, True)] if k == 42 else [PS.twinkle(F, 0), PS.twinkle(F, 1)])
    S, rects, pos = ST.to_sheet(k, F, vs); ST.patch_yx(yx, k, rects, pos)
    import io
    buf = io.BytesIO(); S.save(buf, format='PNG'); data = buf.getvalue()
    if F2 is not None and F2.size == (192, Hc * 2):
        # HD 층: 색을 줄이거나 다시 깎지 않고 고해상도 그림 그대로(채도·대비만 원작에 맞춤) 같은 조각 배치로 담아 PNG 뒤에 붙인다
        F2 = PS.grade(on_tile(F2, yc, 2)); v2 = [PS.grade(on_tile(horse(1.0, 0.5, True), yc, 2))] if k == 9 else [PS.twinkle(F2, 0, 2, 23, True), PS.twinkle(F2, 1, 2, 23, True)] if k == 42 else [PS.twinkle(F2, 0, 2), PS.twinkle(F2, 1, 2)]
        S2 = ST.hd_sheet(k, F2, v2, rects, pos, 2); b2 = io.BytesIO(); S2.save(b2, format='PNG'); data += b2.getvalue(); hd_show.append((k, F2))
    open(f'{out}/b{k}.png', 'wb').write(data)
    for fi in range(len(ST.TABLE[k]['frames'])): show.append((k, F, ST.compose(k, S, rects, frame=fi, pos=pos)))
open(root + '/st2android/mod/yx.d', 'wb').write(yx)
import enhance_orig as EN
# 승마장(b9): 말이 울타리 안을 천천히 도는 12단계 동작을 고해상도 층에 넣는다(작게 움직여서 튀지 않게)
def horse_phases(N=12):
    import io, math, vectorize as VC
    Hc, yc = ST.canvas(9); F0 = on_tile(horse(0, 0), yc); S0, rects, pos = ST.to_sheet(9, F0, []); hds = []
    for q in range(N):
        a = 2 * math.pi * q / N; Fq = on_tile(horse(2.6 * math.cos(a), 1.3 * math.sin(a) - 0.5 * abs(math.sin(2 * a))), yc); Sq, _, _ = ST.to_sheet(9, Fq, [])
        hds.append(VC.vec_sheet(Sq, list(rects) + [(0, Sq.height - 8, 64, 8), (64, Sq.height - 8, 32, 8)]))
    b = io.BytesIO(); S0.save(b, format='PNG'); data = b.getvalue()
    for h in hds: bb = io.BytesIO(); h.save(bb, format='PNG', optimize=True); data += bb.getvalue()
    open(f'{out}/b9.png', 'wb').write(data); ST.patch_yx(yx, 9, rects, pos); open(root + '/st2android/mod/yx.d', 'wb').write(yx)
# horse_phases()  # 승마장은 게임 프레임 표(커서가 올라갔을 때 2프레임)를 그대로 쓴다
n_en = 0
for k in sorted(ST.TABLE):
    if k >= 59 or k in ITEMS or k == 9: continue                  # b9 승마장은 전용 도트
    p = f'{out}/b{k}.png'; src = Image.open(p).convert('RGBA') if os.path.exists(p) else None      # 간판만 고친 원본(b8·b32·b45)은 그 위에 보정
    EN.enhance(k, im=src).save(p); n_en += 1
# 지형 조각(d#)·인물 초상(f#)도 같은 방식으로 일괄 보정(격자 디더 풀기·경계 보존 다듬기·약한 명암). 이미 따로 손본 조각(석산·왕궁리 등)은 건드리지 않는다.
import glob as _gl, re as _re
n_t = 0
for f in sorted(_gl.glob('/tmp/pk/d*.d')):
    n = os.path.basename(f)[:-2]
    if not _re.match(r'^d[0-9a-f]+$', n) or os.path.exists(f'{out}/{n}.png'): continue
    try: EN.enhance(999, im=Image.open(f).convert('RGBA')).save(f'{out}/{n}.png'); n_t += 1
    except Exception: pass
print('지형 조각 보정', n_t, '장')
# ---- 벡터풍 2배 해상도 층: 건물·지형 그림 전부(이미 고해상도판이 붙은 새 건물은 그대로) ----
import vectorize as VC, io, struct, zlib, yx_table as _YT, shutil
def has_hd(data):
    i = data.find(b'IEND'); return i >= 0 and data[i + 8:i + 12] == b'\x89PNG'
T2, _ = _YT.load(root + '/st2android/mod/yx.d'); n_v = 0
for f in sorted(_gl.glob('/tmp/pk/[bd]*.d')):
    n = os.path.basename(f)[:-2]
    if not _re.match(r'^[bd][0-9a-f]+$', n): continue
    p = f'{out}/{n}.png'
    data = open(p, 'rb').read() if os.path.exists(p) else open(f, 'rb').read()
    if has_hd(data): continue
    try: im = Image.open(io.BytesIO(data)).convert('RGBA')
    except Exception: continue
    k = int(n[1:]) if n[0] == 'b' and n[1:].isdigit() else -1
    if 0 <= k < 59 and k in T2:
        H = im.height; rects = list(T2[k]['rects']) + [(0, H - 8, 64, 8), (64, H - 8, 32, 8)]; hd = VC.vec_sheet(im, rects)
    else: hd = VC.vec_hd(im)
    b = io.BytesIO(); im.save(b, format='PNG'); b2 = io.BytesIO(); hd.save(b2, format='PNG', optimize=True)
    open(p, 'wb').write(b.getvalue() + b2.getvalue()); n_v += 1
print('벡터풍 2배 해상도 층', n_v, '장')

print('새 도트', len(ITEMS), '종 변환, 원본 퀄리티 보정', n_en, '종')
if len(sys.argv) > 2:
    w = Image.new('RGBA', (len(hd_show) * 196, max(f.height for _, f in hd_show)), (150, 70, 160, 255))
    for i, (k, f) in enumerate(hd_show): w.alpha_composite(f, (i * 196, w.height - f.height))
    w.save(sys.argv[2])
if len(sys.argv) > 1:
    cols = 7; cw, ch = 100, 110; rows = (len(show) + cols - 1) // cols
    w = Image.new('RGBA', (cols * cw, rows * ch), (150, 70, 160, 255))
    for i, (k, F, C) in enumerate(show): w.alpha_composite(C.crop((0, max(0, C.height - ch), 96, C.height)), ((i % cols) * cw + 2, (i // cols) * ch))
    w.resize((w.width * 3, w.height * 3), Image.NEAREST).save(sys.argv[1])
