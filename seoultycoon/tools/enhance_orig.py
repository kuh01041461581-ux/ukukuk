"""원본 건물 도트의 모양·조각 배치는 그대로 두고 퀄리티만 올린다.
원본은 색 수가 적어 격자무늬(디더)로 중간색을 흉내 냈다 → 격자 디더를 실제 중간색의 부드러운 음영으로 풀어 준다(색 깊이 확장).
조각 경계를 넘어 섞지 않도록 조각 칸마다 따로 처리한다(게임 조립 뒤에도 이음새가 맞는다)."""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import strips as ST
def labels(k, W, H):
    lab = -np.ones((H, W), int)
    if k in ST.TABLE and k < 59:
        rs = ST.TABLE[k]['rects']
        for i in sorted(range(len(rs)), key=lambda i: -(rs[i][2] * rs[i][3])):       # 작은 조각이 위에 오도록
            x, y, w, h = rs[i]; lab[y:y + h, x:x + w] = i
        lab[H - 8:H, 0:64] = 100; lab[H - 8:H, 64:W] = 101
    else: lab[:, :] = 0
    return lab
def enhance(k, strength=1.0, im=None):
    """원본 그림의 선명함과 특색을 지키면서 색만 또렷하게: 채도·대비를 조금 올리고 윗면은 살짝 밝게, 아래는 살짝 어둡게. 디더(격자 질감)를 풀거나 이웃 색을 섞지 않는다(흐려지고 특색이 사라지므로)."""
    im = im or ST.orig(k); a = np.asarray(im).astype(np.float32); H, W = a.shape[:2]; rgb = a[..., :3]; al = a[..., 3] > 0
    g = rgb.mean(-1, keepdims=True); out = g + (rgb - g) * (1.0 + 0.12 * strength); out = (out - 128) * (1.0 + 0.06 * strength) + 128
    yy = np.linspace(1.0, 0.0, H)[:, None, None]; out = out * (1.0 + 0.05 * strength * (yy - 0.5))
    res = np.zeros((H, W, 4), np.uint8); res[..., :3] = np.clip(out, 0, 255).astype(np.uint8); res[..., 3] = al * 255
    return Image.fromarray(res, 'RGBA')
if __name__ == '__main__':
    ks = [int(x) for x in sys.argv[2:]] or [11, 10, 0, 49]
    w = Image.new('RGBA', (len(ks) * 208, 110), (150, 70, 160, 255))
    for i, k in enumerate(ks):
        for j, s in enumerate((ST.orig(k), enhance(k))):
            c = ST.compose(k, s); t = max(0, c.height - 110); pc = c.crop((0, t, 100, c.height)); w.alpha_composite(pc, (i * 208 + j * 104, 110 - pc.height))
    w.resize((w.width * 5, w.height * 5), Image.NEAREST).save(sys.argv[1])
