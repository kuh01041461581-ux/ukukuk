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
for tool in ('make_iksan_cityhall.py', 'make_iksan_quarry3d.py', 'make_iksan_palace.py', 'make_iksan_retouch.py', 'make_iksan_minimap.py', 'make_iksan_logo.py', 'make_maryong.py', 'make_iksan_horse.py'): run(tool)
# 2) 건물: 조각 규칙 변환
import strips as ST
from make_iksan_buildings import Sc
import make_iksan_buildings as MB, make_iksan_hq as HQ, make_iksan_more as MO, make_iksan_civic as CV, make_iksan_landmarks as LM
ITEMS = {4: HQ.hotel, 19: HQ.cinema, 15: HQ.tesla, 22: HQ.station, 42: HQ.arts, 44: HQ.harim, 3: HQ.costco, 33: HQ.baseball, 27: HQ.hospital, 37: HQ.apartments, 35: HQ.school, 43: HQ.library,
         11: HQ.firestation, 41: HQ.terminal, 40: HQ.busstop, 47: HQ.kindergarten, 48: HQ.topschool, 26: HQ.museum, 52: HQ.playground, 53: HQ.sportspark,
         21: MB.chunpo, 38: MO.baekje, 10: CV.police, 13: CV.church, 12: CV.bank_design, 20: CV.library, 2: LM.mireuksa}
yx = bytearray(open(root + '/work/st2/yx.d', 'rb').read()); show = []
for k, fn in sorted(ITEMS.items()):
    Hc, yc = ST.canvas(k)
    Sc.OVR = (Hc, yc)
    try: F = fn()
    finally: Sc.OVR = None
    if F.size != (96, Hc): F2 = Image.new('RGBA', (96, Hc), (0, 0, 0, 0)); F2.alpha_composite(F.crop((0, 0, 96, min(F.height, Hc)))); F = F2
    S, rects = ST.to_sheet(k, F); S.save(f'{out}/b{k}.png'); ST.patch_yx(yx, k, rects); show.append((k, F, ST.compose(k, S, rects)))
open(root + '/st2android/mod/yx.d', 'wb').write(yx)
print('건물', len(ITEMS), '종 변환 완료')
if len(sys.argv) > 1:
    cols = 7; cw, ch = 100, 110; rows = (len(show) + cols - 1) // cols
    w = Image.new('RGBA', (cols * cw, rows * ch), (150, 70, 160, 255))
    for i, (k, F, C) in enumerate(show): w.alpha_composite(C.crop((0, max(0, C.height - ch), 96, C.height)), ((i % cols) * cw + 2, (i // cols) * ch))
    w.resize((w.width * 3, w.height * 3), Image.NEAREST).save(sys.argv[1])
