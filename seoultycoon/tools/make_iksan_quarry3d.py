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
RAMP = [(96, 86, 80), (124, 112, 104), (150, 138, 128), (174, 162, 150), (196, 184, 170), (214, 202, 188)]
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
    lo, hi_ = np.percentile(hb[mask], 4), np.percentile(hb[mask], 96); N = 6
    tt = np.clip((hb - lo) / max(1e-6, hi_ - lo), 0, 1)                      # 0(골짜기)~1(능선)
    lev = np.clip((tt * N).astype(int), 0, N - 1)
    res = a.copy(); B4 = ((0, 2), (3, 1))
    for y in range(h):
        for x in range(w):
            if not mask[y, x]: continue
            t = tt[y, x]; dth = (B4[y % 2][x % 2] - 1.5) / 4.0 * 0.10
            # 산자락(낮은 곳)은 원래 숲을 그대로 둔다 → 숲 속을 깎아 낸 채석장으로 보인다. 경계는 흙·풀을 섞는다
            if t + dth < 0.26: continue
            if t + dth < 0.36:
                if (x * 7 + y * 13) % 3: res[y, x, :3] = SOIL[(x + y) % 3]
                continue
            # 화강암: 높이에 따라 이어지는 색(격자 디더로 단을 부드럽게) + 벤치(단) 가장자리 음영
            f = min(len(RAMP) - 1.001, max(0.0, (t + dth - 0.30) / 0.70 * (len(RAMP) - 1))); i0 = int(f); fr = f - i0
            c = [RAMP[i0][q] * (1 - fr) + RAMP[i0 + 1][q] * fr for q in range(3)]
            L = lev[y, x]; up = lev[y - 1, x] if y > 0 else L; lf = lev[y, x - 1] if x > 0 else L; dn = lev[y + 1, x] if y < h - 1 else L
            if up > L or lf > L: c = [v * 0.72 for v in c]                  # 단 아래 그늘(수직 절개면)
            elif dn < L: c = [min(255, v * 1.08 + 4) for v in c]             # 단 끝 밝은 모서리
            nz = rnd.random()
            if nz < 0.05: c = [v * 0.90 for v in c]
            elif nz > 0.985 and t > 0.55: c = [70 + rnd.randrange(30), 140 + rnd.randrange(30), 60]      # 바위틈 풀
            res[y, x, :3] = [int(v) for v in c]
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
