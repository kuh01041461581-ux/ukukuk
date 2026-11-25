"""PC 판 전용 2배 층: 원본 도트(저해상도 층)를 Scale2x(EPX) 로 키워 또렷한 도트로 만든다(벡터풍으로 뭉개진 2배 층 대신).
Scale2x 는 화소 색을 새로 만들지 않고 계단만 매끈하게 하므로 도트 느낌이 그대로 남는다.
work/pak_mod/*.png → work/pak_mod_pc/*.png (2배 층만 교체, 뒤에 붙은 BIGH 등 꼬리는 유지)."""
import os, io, sys
import numpy as np
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); src = root + '/work/pak_mod'; dst = root + '/work/pak_mod_pc'
def scale2x(a):
    h, w = a.shape[:2]; P = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode='edge').astype(np.int32)
    key = (P[..., 0] << 24) | (P[..., 1] << 16) | (P[..., 2] << 8) | P[..., 3]
    E = key[1:-1, 1:-1]; B = key[:-2, 1:-1]; H = key[2:, 1:-1]; D = key[1:-1, :-2]; F = key[1:-1, 2:]
    cE = a; cB = P[:-2, 1:-1]; cH = P[2:, 1:-1]; cD = P[1:-1, :-2]; cF = P[1:-1, 2:]
    ok = (B != H) & (D != F)
    e0 = np.where((ok & (D == B))[..., None], cD, cE); e1 = np.where((ok & (B == F))[..., None], cF, cE)
    e2 = np.where((ok & (D == H))[..., None], cD, cE); e3 = np.where((ok & (H == F))[..., None], cF, cE)
    out = np.zeros((h * 2, w * 2, 4), np.uint8); out[0::2, 0::2] = e0; out[0::2, 1::2] = e1; out[1::2, 0::2] = e2; out[1::2, 1::2] = e3
    return out
def conv(data):
    e1 = data.find(b'IEND') + 8
    if e1 < 8: return data
    lo = np.asarray(Image.open(io.BytesIO(data[:e1])).convert('RGBA'))
    e2 = data.find(b'IEND', e1)
    if e2 < 0 or data[e1:e1 + 8] != b'\x89PNG\r\n\x1a\n': return data                      # 2배 층이 없는 그림은 그대로
    e2 += 8; hd = Image.open(io.BytesIO(data[e1:e2]))
    if hd.size != (lo.shape[1] * 2, lo.shape[0] * 2): return data
    b = io.BytesIO(); Image.fromarray(scale2x(lo), 'RGBA').save(b, format='PNG', optimize=True)
    return data[:e1] + b.getvalue() + data[e2:]
KEEP = {f'b{k}.png' for k in (2, 13, 20, 21, 26, 33, 38, 42)}     # 새로 만든 건물·도트화한 석탑은 이미 또렷한 2배 층이 있다
if __name__ == '__main__':
    os.makedirs(dst, exist_ok=True); n = 0; only = set(sys.argv[1:])
    for f in sorted(os.listdir(src)):
        if not f.endswith('.png') or (only and f[:-4] not in only): continue
        d = open(f'{src}/{f}', 'rb').read(); nd = d if f in KEEP else conv(d); open(f'{dst}/{f}', 'wb').write(nd); n += nd is not d
    print('PC 2배 층(Scale2x)', n, '장')
