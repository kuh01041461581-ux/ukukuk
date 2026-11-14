"""춘포역(구 춘포역사, 등록문화재) — 사진 기준: 크림색 회벽 단층 + 아랫단 민트색 띠, 녹회색 마름모 기와(슬레이트) 팔작지붕, 가운데 박공 돌출부(흰 박공 + 민트 처마 + 역명 현판),
왼쪽 긴 차양 베란다(민트 기둥), 파란 문·창(파랑 창틀), 오른쪽 낮은 부속채. 앞에 승강장과 선로."""
import os, sys, random
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_iksan_buildings import Sc, hx, FONT_K

def chunpo():
    s = Sc(48, 13); G = s.G; s.ground(); d = ImageDraw.Draw(s.im); rs = random.Random(10)
    CR = (hx('f0e2a8'), hx('cbb97a')); CRR = (hx('d8c88a'), hx('a89658')); MINT = (hx('7fd6b4'), hx('3fa684')); MINTR = (hx('5cb896'), hx('2e8068'))
    SL = (hx('6e8e8a'), hx('3e5c5c')); SLR = (hx('567674'), hx('2c4646')); BL = (hx('3a8ee0'), hx('1c5cb0'))
    # 승강장 + 선로(앞쪽 b=0.80~1.0 에 길게)
    s.grad([G(0.04, 0.78), G(0.96, 0.78), G(0.96, 0.88), G(0.04, 0.88)], hx('d8d2c0'), hx('b4ae9c'), (0, 1))
    s.grad([G(0.04, 0.88), G(0.96, 0.88), G(0.96, 0.99), G(0.04, 0.99)], hx('7a7468'), hx('5a5448'), (0, 1))
    for k in range(0, 36): s.line([G(0.06 + 0.026 * k, 0.89), G(0.06 + 0.026 * k, 0.98)], hx('4a3a2a'), 3)
    for b in (0.91, 0.96): s.line([G(0.04, b), G(0.96, b)], hx('c8ccd6'), 3)
    s.shadow([G(0.14, 0.72), G(0.96, 0.72), G(1.0, 0.80), G(0.24, 0.80)], 105, 8)
    # --- 본채(긴 몸체): 크림 벽 + 아랫단 민트 띠 ---
    A0, A1, B0, B1, HH = 0.10, 0.88, 0.22, 0.58, 16.0
    s.box(A0, A1, B0, B1, HH, CR, CRR, (hx('e8d89a'), hx('c0ae70')))
    s.grad([G(A0, B1, 0), G(A1, B1, 0), G(A1, B1, 2.4), G(A0, B1, 2.4)], MINT[0], MINT[1], (0, 1)); s.grad([G(A1, B0, 0), G(A1, B1, 0), G(A1, B1, 2.4), G(A1, B0, 2.4)], MINTR[0], MINTR[1], (0, 1))
    # 회벽 질감
    for _ in range(260):
        a = rs.uniform(A0, A1); z = rs.uniform(2.8, HH); x, y = G(a, B1, z); d.rectangle([x, y, x + 3, y + 2], fill=hx('d8c88a', 120) if rs.random() < 0.5 else hx('f8ecb8', 120))
    # 파란 문·창: 중앙 박공 왼쪽에 큰 쌍여닫이 문(파란) + 창, 오른쪽에 쌍창
    def win(a0, a1, z0, z1, b=B1):
        s.grad([G(a0, b, z0), G(a1, b, z0), G(a1, b, z1), G(a0, b, z1)], hx('b8dcf4'), hx('3a6a98'), (0.2, 1))
        s.line([G(a0, b, z0), G(a1, b, z0), G(a1, b, z1), G(a0, b, z1), G(a0, b, z0)], hx('2a78d0'), 3)
        s.line([G((a0 + a1) / 2, b, z0), G((a0 + a1) / 2, b, z1)], hx('2a78d0'), 2); s.line([G(a0, b, (z0 + z1) / 2), G(a1, b, (z0 + z1) / 2)], hx('2a78d0'), 2)
    s.grad([G(0.20, B1, 0), G(0.34, B1, 0), G(0.34, B1, 10.6), G(0.20, B1, 10.6)], BL[0], BL[1], (0, 1))                 # 쌍여닫이 파란 문
    s.line([G(0.27, B1, 0), G(0.27, B1, 10.6)], hx('0c3c80'), 2); s.line([G(0.20, B1, 8.4), G(0.34, B1, 8.4)], hx('d8ecff'), 2)
    for k in range(2):
        a = 0.215 + 0.065 * k; s.grad([G(a, B1, 6.2), G(a + 0.05, B1, 6.2), G(a + 0.05, B1, 9.8), G(a, B1, 9.8)], hx('b8dcf4'), hx('3a6a98'), (0.2, 1))
    s.grad([G(0.40, B1, 0), G(0.46, B1, 0), G(0.46, B1, 10.0), G(0.40, B1, 10.0)], BL[0], BL[1], (0, 1))                  # 작은 파란 문
    win(0.12, 0.17, 5.0, 10.4)
    win(0.64, 0.70, 4.0, 12.0); win(0.72, 0.78, 4.0, 12.0)                                                                # 오른쪽 쌍창(키가 큼)
    # 왼쪽 긴 차양 베란다: 낮은 지붕 + 민트 기둥
    s.grad([G(0.04, B1 + 0.10, 11.6), G(0.50, B1 + 0.10, 11.6), G(0.50, B1 - 0.02, 13.6), G(0.04, B1 - 0.02, 13.6)], SL[0], SL[1], (0, 1))
    for k in range(0, 14): s.line([G(0.04 + 0.46 * k / 13, B1 + 0.10, 11.6), G(0.04 + 0.46 * k / 13, B1 - 0.02, 13.6)], hx('3e5c5c', 120), 2)
    s.line([G(0.04, B1 + 0.10, 11.6), G(0.50, B1 + 0.10, 11.6)], hx('7fd6b4'), 5)
    for a in (0.06, 0.20, 0.34, 0.48): s.line([G(a, B1 + 0.09, 0), G(a, B1 + 0.09, 11.5)], hx('5cc8a4'), 5); s.line([G(a + 0.008, B1 + 0.09, 0), G(a + 0.008, B1 + 0.09, 11.5)], hx('2e8068'), 2)
    # --- 큰 지붕: 녹회색 마름모 무늬 슬레이트 ---
    ov = 0.035; zr = HH + 0.2; RZ = 6.0; bm = (B0 + B1) / 2
    s.grad([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.04, bm, zr + RZ), G(A0 + 0.04, bm, zr + RZ)], SL[0], SL[1], (0, -1), 3)
    s.grad([G(A0 - ov, B0 - ov, zr), G(A1 + ov, B0 - ov, zr), G(A1 - 0.04, bm, zr + RZ), G(A0 + 0.04, bm, zr + RZ)], SLR[0], SLR[1], (0, 1))       # 뒤쪽 경사면
    s.grad([G(A1 + ov, B0 - ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.04, bm, zr + RZ)], SLR[0], SLR[1], (0.3, 1))
    for k in range(1, 26):                                                       # 마름모 기와 무늬
        t = k / 26; s.line([G(A0 - ov + (A1 - A0 + 2 * ov) * t, B1 + ov, zr), G(A0 + 0.04 + (A1 - A0 - 0.08) * t, bm, zr + RZ)], hx('b8d4cc', 90), 2)
    for k in range(1, 5):
        zz = zr + RZ * k / 5; aL = A0 - ov + (0.04 + ov) * k / 5; aR = A1 + ov - (0.04 + ov) * k / 5; bb = (B1 + ov) + (bm - B1 - ov) * k / 5
        s.line([G(aL, bb, zz), G(aR, bb, zz)], hx('4a6a68', 140), 2)
    s.line([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr)], hx('7fd6b4'), 5); s.line([G(A0 + 0.04, bm, zr + RZ), G(A1 - 0.04, bm, zr + RZ)], hx('a4d4c4'), 4)
    # --- 가운데 박공 돌출부: 흰 박공 + 민트 처마 + 현판 ---
    PA0, PA1, PB1 = 0.46, 0.66, B1 + 0.04
    s.box(PA0, PA1, B1 - 0.16, PB1, HH + 1.2, CR, CRR, (hx('e8d89a'), hx('c0ae70')))
    s.grad([G(PA0, PB1, 0), G(PA1, PB1, 0), G(PA1, PB1, 2.6), G(PA0, PB1, 2.6)], MINT[0], MINT[1], (0, 1))
    pk = G((PA0 + PA1) / 2, PB1, HH + 1.2 + 5.0)
    s.grad([G(PA0, PB1, HH + 1.2), G(PA1, PB1, HH + 1.2), pk], hx('fffdf0'), hx('e4dcc0'), (0.2, 1))
    s.grad([G(PA1, B1 - 0.16, HH + 1.2), G(PA1, PB1, HH + 1.2), pk], SLR[0], SLR[1], (0.3, 1))
    s.line([G(PA0 - 0.012, PB1 + 0.01, HH + 1.1), pk, G(PA1 + 0.012, PB1 + 0.01, HH + 1.1)], hx('7fd6b4'), 6)
    x, y = pk; d.ellipse([x - 4, y - 9, x + 4, y - 1], fill=hx('5cc8a4'))                                              # 박공 꼭대기 장식
    wx, wy = G((PA0 + PA1) / 2, PB1, HH + 3.4); d.rectangle([wx - 20, wy - 8, wx + 20, wy + 8], fill=hx('e8d8a8')); d.rectangle([wx - 20, wy - 8, wx + 20, wy + 8], outline=hx('3a6a5c'), width=3)
    d.text((wx - 14, wy - 7), '春浦', font=__import__('PIL.ImageFont', fromlist=['x']).truetype('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc', 15), fill=hx('2a4a40')) if os.path.exists('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc') else None
    x, y = G(0.55, PB1, HH + 1.8); d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=hx('fff6c0'))                          # 처마 등
    # 오른쪽 낮은 부속채(파란 지붕·붉은 벽돌 느낌) + 나무 울타리 말뚝
    s.box(0.90, 0.99, 0.20, 0.44, 4.6, (hx('b86a4a'), hx('82402c')), (hx('9c5238'), hx('6a3020')), (hx('3a9ad8'), hx('2a70b0')))
    for k in range(0, 9): x, y = G(0.10 + 0.10 * k, 0.74); d.rectangle([x - 2, y - 8, x + 2, y], fill=hx('a8743c'))
    s.tree(0.03, 0.68, 0.7)
    return s.finish()
