"""여자 시장 얼굴(ff3~5, ff9~11)만 손본다: 원본 그림 그대로 두고 피부톤을 조금 밝게, 잔주름(피부 안의 가는 그늘)을 약하게 편다.
눈·눈썹·입·윤곽은 건드리지 않는다. 다른 얼굴·초상은 원본 그대로 쓴다(모드 파일을 만들지 않는다).  → st2android/mod/ff*.d"""
import os, sys, glob, re
import numpy as np
from PIL import Image
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); src = root + '/work/st2'; mod = root + '/st2android/mod'
FEMALE = ['ff3', 'ff4', 'ff5', 'ff9', 'ff10', 'ff11']
def soften(im, bright=0.22, smooth=0.55):
    a = np.asarray(im).astype(np.float32); al = a[..., 3] > 0; rgb = a[..., :3]; h, w = al.shape
    lum = rgb @ np.array([0.30, 0.59, 0.11], dtype=np.float32); skin = al & (lum >= 120)
    out = rgb.copy()
    for y in range(h):                                     # 잔주름: 피부 화소를 가까운 색의 피부 이웃과 조금 섞는다(짙은 선은 이웃에서 제외)
        for x in range(w):
            if not skin[y, x]: continue
            c = rgb[y, x]; acc = np.zeros(3, np.float32); wt = 0.0
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if (dy or dx) and 0 <= yy < h and 0 <= xx < w and skin[yy, xx] and np.abs(rgb[yy, xx] - c).sum() < 110: acc += rgb[yy, xx]; wt += 1
            if wt >= 3:
                m = acc / wt
                if m.sum() > c.sum(): out[y, x] = c + (m - c) * smooth          # 주변보다 어두운 화소(주름)만 밝은 쪽으로
    for y in range(h):                                     # 피부톤: 조금 밝고 맑게
        for x in range(w):
            if skin[y, x]: c = out[y, x]; out[y, x] = c + (np.array([255, 240, 226], dtype=np.float32) - c) * bright
    r = np.zeros((h, w, 4), np.uint8); r[..., :3] = np.clip(out, 0, 255).astype(np.uint8); r[..., 3] = al * 255
    return Image.fromarray(r, 'RGBA')
if __name__ == '__main__':
    for f in glob.glob(mod + '/*.d'):                      # 예전 보정본 지우기(마룡이 f7 은 남긴다)
        n = os.path.basename(f)[:-2]
        if re.match(r'^(fd\d+|ff\d+|fb\d+|fh\d+|fa\d+|f\d+)$', n) and n != 'f7': os.remove(f)
    show = []
    for n in FEMALE:
        o = Image.open(f'{src}/{n}.d').convert('RGBA'); e = soften(o); e.save(f'{mod}/{n}.d', format='PNG'); show.append((o, e))
    if len(sys.argv) > 1:
        w = Image.new('RGBA', (len(show) * 56, 28), (60, 90, 140, 255))
        for i, (o, e) in enumerate(show): w.alpha_composite(o, (i * 56 + 1, 1)); w.alpha_composite(e, (i * 56 + 27, 1))
        w.resize((w.width * 6, w.height * 6), Image.NEAREST).save(sys.argv[1])
    print('여자 시장 얼굴 6장: 피부톤·잔주름만 보정')
