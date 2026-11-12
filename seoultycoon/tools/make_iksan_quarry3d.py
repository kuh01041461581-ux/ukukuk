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
RAMP = [(74, 72, 70), (104, 100, 96), (132, 128, 122), (158, 154, 146), (182, 178, 168), (204, 200, 190)]      # 회색 화강암(도로·건물의 회색과 어울리는 톤)
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
    res = a.copy(); B8 = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))
    # 원본 산 그림의 붓터치(능선·골의 명암)를 그대로 살리고 색만 바꾼다: 각 화소의 밝기를 화강암 색띠에 대응시키고,
    # 높은 곳일수록 바위가 드러나도록 4x4 격자 디더로 숲과 섞는다(낮은 곳은 숲 그대로) → 주변 산·숲과 이어진다.
    l0, l1 = np.percentile(lum[mask], 3), np.percentile(lum[mask], 97)
    for y in range(h):
        for x in range(w):
            if not mask[y, x]: continue
            t = tt[y, x]; p = (t - 0.34) / 0.36; p = 0.0 if p < 0 else (1.0 if p > 1 else p * p * (3 - 2 * p))
            if p * 16.0 <= B8[y % 4][x % 4] + 0.5: continue                      # 숲(원본 화소) 유지
            v = max(0.0, min(0.999, (lum[y, x] - l0) / max(1e-6, l1 - l0))); f = v * (len(RAMP) - 1); i0 = int(f); fr = f - i0
            c = [RAMP[i0][q] * (1 - fr) + RAMP[i0 + 1][q] * fr for q in range(3)]
            L = lev[y, x]; up = lev[y - 1, x] if y > 0 else L
            if up > L and t > 0.5: c = [v_ * 0.80 for v_ in c]                    # 채석 단(벤치) 아래 가는 그늘선
            if rnd.random() > 0.975: c = [58 + rnd.randrange(24), 128 + rnd.randrange(24), 52]     # 바위틈 풀
            res[y, x, :3] = [int(v_) for v_ in c]
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
