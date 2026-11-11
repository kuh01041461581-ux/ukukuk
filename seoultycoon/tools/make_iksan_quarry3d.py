"""황등석산 3D: 원본 산 지형 조각(d#)의 산 화소만 골라, 원래의 명암(능선·골)을 높이로 읽어 계단식 화강암 벤치로 다시 칠한다.
도로·울타리·집은 건드리지 않고(마스크), 원본 명암에서 만들므로 이웃 조각과 이음새가 맞는다.   → work/pak_mod/d{5,6,15,16,25,26,50,51,60,70}.png
사용: python3 -I tools/make_iksan_quarry3d.py [미리보기.png]"""
import os, sys, random
import numpy as np
from PIL import Image, ImageFilter
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = root + '/work/pak_mod'
MT = {(21,154,70),(71,149,73),(8,104,8),(106,151,81),(24,120,24),(10,82,9),(109,179,83),(76,175,80),(8,152,8),(8,120,8),(24,152,27),(8,136,8),(8,120,24),(45,146,41),(30,136,26),(24,152,8),(24,136,8),(74,143,48)}
# 화강암(황등석: 분홍빛 회색) 단계별 색: 어두운 골 → 밝은 면
RAMP = [(58, 52, 54), (88, 80, 80), (116, 106, 102), (142, 130, 124), (168, 154, 146), (192, 178, 168), (214, 200, 188)]
SOIL = [(120, 84, 52), (150, 108, 66), (178, 136, 84)]

def load(n):
    ensure()
    return Image.open(f'/tmp/pk/{n}.d').convert('RGBA')
def ensure():
    if not os.path.exists('/tmp/pk/d15.d'):
        import subprocess; subprocess.run([sys.executable, '-I', root + '/tools/extract_pak.py', root + '/work/st2/data1.pak', '/tmp/pk'], capture_output=True)

def quarry(n, seed):
    im = load(n); a = np.asarray(im).astype(np.int32); h, w = a.shape[:2]; rnd = random.Random(seed)
    rgb = a[..., :3]; alpha = a[..., 3] > 0
    mask = np.zeros((h, w), bool)
    for y in range(h):
        for x in range(w):
            if alpha[y, x] and tuple(rgb[y, x]) in MT: mask[y, x] = True
    if mask.sum() < 20: return None
    lum = (rgb[..., 0] * 0.3 + rgb[..., 1] * 0.59 + rgb[..., 2] * 0.11)
    # 높이: 산 화소의 밝기를 주변으로 번지게 한 값(산이 아닌 칸은 평균으로 채움)
    mean = lum[mask].mean(); hf = np.where(mask, lum, mean)
    hi = Image.fromarray(np.clip(hf, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.6)); hb = np.asarray(hi).astype(float)
    lo, hi_ = np.percentile(hb[mask], 4), np.percentile(hb[mask], 96); N = 7
    lev = np.clip(((hb - lo) / max(1e-6, hi_ - lo) * N).astype(int), 0, N - 1)
    res = a.copy()
    for y in range(h):
        for x in range(w):
            if not mask[y, x]: continue
            L = lev[y, x]; c = list(RAMP[L])
            # 벤치 가장자리: 왼쪽·위가 한 단 높으면(그림자 쪽) 어둡게, 오른쪽·아래가 낮으면(빛 받는 모서리) 밝게
            up = lev[y - 1, x] if y > 0 else L; lf = lev[y, x - 1] if x > 0 else L; dn = lev[y + 1, x] if y < h - 1 else L; rt = lev[y, x + 1] if x < w - 1 else L
            if up > L or lf > L: c = [int(v * 0.62) for v in c]
            elif dn < L or rt < L: c = [min(255, int(v * 1.12) + 6) for v in c]
            # 화강암 결: 작은 잡티 + 가끔 세로 드릴 자국
            nz = rnd.random()
            if nz < 0.06: c = [int(v * 0.92) for v in c]
            elif nz > 0.96: c = [min(255, int(v * 1.05)) for v in c]
            # 가장 낮은 단은 흙(채굴된 바닥), 가장 높은 단 일부는 풀
            if L == 0 and rnd.random() < 0.55: c = list(SOIL[rnd.randrange(3)])
            res[y, x, :3] = c
    # 벤치 수직면 질감: 단 경계 바로 아래 한 줄을 어둡게 + 세로 줄무늬 (3D 느낌)
    for y in range(1, h):
        for x in range(w):
            if mask[y, x] and mask[y - 1, x] and lev[y, x] < lev[y - 1, x]:
                res[y, x, :3] = (res[y, x, :3] * 0.72).astype(int)
    return Image.fromarray(np.clip(res, 0, 255).astype(np.uint8), 'RGBA')

NAMES = ['d5', 'd6', 'd15', 'd16', 'd25', 'd26', 'd50', 'd51', 'd60', 'd70']
if __name__ == '__main__':
    ims = []
    for i, n in enumerate(NAMES):
        q = quarry(n, 100 + i)
        if q is not None: q.save(f'{out}/{n}.png'); ims.append((n, load(n), q))
    if len(sys.argv) > 1:
        w = Image.new('RGBA', (len(ims) * 70, 150), (40, 40, 50, 255))
        for i, (n, o, q) in enumerate(ims): w.alpha_composite(o, (i * 70, 0)); w.alpha_composite(q, (i * 70, 75))
        w.resize((w.width * 3, w.height * 3), Image.NEAREST).save(sys.argv[1])
    print('황등석산 3D:', ', '.join(n for n, _, _ in ims))
