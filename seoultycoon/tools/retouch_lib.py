"""원본 건물 시트를 '조각 합성 그림(F0)'으로 조립해 고친 뒤, 같은 조각 칸에 되돌려 넣는 덧칠 도구.
조각 위치는 yx_solve 의 연속성 추정(±2px)이며, 같은 위치로 되돌리므로 고치지 않은 화소는 원본 그대로다."""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import yx_solve as YS
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PK = '/tmp/pk/b%d.d'
def ensure_pk():
    if not os.path.exists('/tmp/pk/b0.d'):
        import subprocess; subprocess.run([sys.executable, '-I', root + '/tools/extract_pak.py', root + '/work/st2/data1.pak', '/tmp/pk'], capture_output=True)

class Comp:
    """k번 건물: 합성 캔버스 F(좌표계: 메인 조각 좌상단=(PX,PY))."""
    def __init__(self, k):
        ensure_pk(); self.k = k; self.S = Image.open(PK % k).convert('RGBA'); placed = YS.solve(k, PK)
        self.strips = []                       # (rect, dx, y)   y: 메인 윗변 기준(위쪽 음수)
        main = YS.groups[k][0]; self.strips.append((main, 0, 0))
        for p, dx, y in placed[1:]: self.strips.append((p, dx, y))
        top = min(s[2] for s in self.strips); self.PY = -top + 8; self.PX = 40
        hh = self.PY + main[3] + 12
        self.F = Image.new('RGBA', (96 + 80, hh), (0, 0, 0, 0))
        for (r, dx, y) in self.strips:
            x0, y0, w, h = r; self.F.alpha_composite(self.S.crop((x0, y0, x0 + w, y0 + h)), (self.PX + dx, self.PY + y))
    def save_back(self):
        out = self.S.copy()
        for (r, dx, y) in self.strips:
            x0, y0, w, h = r; piece = self.F.crop((self.PX + dx, self.PY + y, self.PX + dx + w, self.PY + y + h)); out.paste(piece, (x0, y0))
        return out
    def preview(self, scale=5, grid=True):
        from PIL import ImageDraw
        bg = Image.new('RGBA', self.F.size, (150, 70, 160, 255)); bg.alpha_composite(self.F); c = bg.resize((bg.width * scale, bg.height * scale), Image.NEAREST)
        if grid:
            d = ImageDraw.Draw(c)
            for x in range(0, self.F.width, 10): d.line([(x * scale, 0), (x * scale, c.height)], fill=(255, 255, 255, 40)); d.text((x * scale + 1, 1), str(x), fill=(255, 255, 0, 255))
            for y in range(0, self.F.height, 10): d.line([(0, y * scale), (c.width, y * scale)], fill=(255, 255, 255, 40)); d.text((1, y * scale + 1), str(y), fill=(255, 255, 0, 255))
        return c
