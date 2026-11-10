"""익산 모드 추가 건물: 익산예술의전당(b42=국립극장 자리), 익산 야구장(b33=월드컵경기장 자리), 백제왕궁(b38: 왕궁리 오층석탑 + 궁궐 정전).
make_iksan_buildings.Sc 의 8배 해상도 렌더를 그대로 쓴다.  → work/pak_mod/{b42,b33,b38}.png   사용: python3 -I tools/make_iksan_more.py [미리보기.png]"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_iksan_landmarks as LM
from make_iksan_buildings import Sc, hx, lerp, W, S, out, FONT_K, FONT_B

# ---------------- 익산예술의전당 (대공연장 + 유리 로비) ----------------
def arts():
    s = Sc(60, 21); G = s.G; s.ground(); rs = random.Random(5)
    s.shadow([G(0.10, 0.58), G(0.88, 0.58), G(1.0, 0.70), G(0.22, 0.72)], 100, 10)
    # 뒤쪽 대공연장(무대 타워): 회백색 석재 + 세로 핀
    s.box(0.40, 0.88, 0.10, 0.40, 21, (hx('f2f0ea'), hx('c4c0b6')), (hx('cac6ba'), hx('96928a')), (hx('d4d2cc'), hx('aeaca6')))
    for k in range(1, 14): s.line([G(0.40 + 0.48 * k / 14, 0.40, 0), G(0.40 + 0.48 * k / 14, 0.40, 21)], hx('a8a49a', 110), 2)
    for k in range(1, 6): s.line([G(0.88, 0.10 + 0.30 * k / 6, 0), G(0.88, 0.10 + 0.30 * k / 6, 21)], hx('76726a', 110), 2)
    s.grad([G(0.40, 0.40, 17), G(0.88, 0.40, 17), G(0.88, 0.40, 21), G(0.40, 0.40, 21)], hx('3a4a68'), hx('1a2440'), (0.2, 1))
    # 앞쪽 유리 로비: 하늘색 반사 유리 + 흰 기둥 + 앞으로 내민 경사 지붕
    s.box(0.10, 0.82, 0.40, 0.64, 11, (hx('bcd6ec'), hx('4a78a8')), (hx('7aa0c8'), hx('2a4e7c')), (hx('e6eaf2'), hx('b8c0d0')))
    for k in range(0, 15): s.line([G(0.10 + 0.72 * k / 14, 0.64, 0), G(0.10 + 0.72 * k / 14, 0.64, 11)], hx('f6f8fc'), 3)
    for z in (3.6, 7.2): s.line([G(0.10, 0.64, z), G(0.82, 0.64, z)], hx('e8eef8', 190), 2)
    for k in range(5, 10): s.line([G(0.10 + 0.72 * (k - 1) / 14, 0.64, 0), G(0.10 + 0.72 * (k - 1) / 14, 0.64, 9)], hx('ffffff', 60), 2)
    s.grad([G(0.06, 0.64, 11.4), G(0.86, 0.64, 11.4), G(0.86, 0.78, 8.0), G(0.06, 0.78, 8.0)], hx('f4f6fa'), hx('c0c6d4'), (1, 0.6))        # 경사 캐노피
    s.grad([G(0.06, 0.78, 8.0), G(0.86, 0.78, 8.0), G(0.86, 0.78, 7.0), G(0.06, 0.78, 7.0)], hx('e0e4ee'), hx('9aa2b6'), (0, 1))
    for a in (0.12, 0.34, 0.58, 0.80): s.line([G(a, 0.76, 0), G(a, 0.76, 7.0)], hx('f4f4f8'), 6)
    s.grad([G(0.40, 0.76, 0), G(0.54, 0.76, 0), G(0.54, 0.76, 5.4), G(0.40, 0.76, 5.4)], hx('26344e'), hx('4a5c82'), (0, 1))
    # 간판 "예술의전당" (금빛 글씨, 어두운 띠 위)
    s.grad([G(0.14, 0.64, 11), G(0.80, 0.64, 11), G(0.80, 0.64, 14.6), G(0.14, 0.64, 14.6)], hx('1e2a44'), hx('0e1628'), (0.2, 1))
    fw = G(0.78, 0.64)[0] - G(0.16, 0.64)[0]
    s.text_face('예술의전당', (G(0.17, 0.64)[0], G(0.17, 0.64, 13.6)[1] + 1), 0, 0, fw * 0.70 / 330.0, hx('f4d478'), font=FONT_K, size=60, shear=0.5)
    # 앞 광장 분수 + 가로수
    x, y = G(0.52, 0.90); d = ImageDraw.Draw(s.im); d.ellipse([x - 40, y - 12, x + 40, y + 12], fill=hx('7aa4d0')); d.ellipse([x - 32, y - 9, x + 32, y + 9], fill=hx('bcdcf4')); d.line([(x, y), (x, y - 26)], fill=hx('f0f8ff'), width=4); d.ellipse([x - 8, y - 32, x + 8, y - 22], fill=hx('e8f4ff'))
    s.tree(0.04, 0.80, 0.8); s.tree(0.95, 0.30, 0.8)
    return s.finish()

# ---------------- 익산 야구장 (홈 뒤 관중석 + 기자석 + 지붕, 외야 개방, 조명탑) ----------------
def baseball():
    s = Sc(57, 22); G = s.G; s.ground(); rs = random.Random(6); d = ImageDraw.Draw(s.im)
    F0, F1 = 0.15, 0.86                                      # 경기장 바닥 범위(a,b 같은 값)
    s.shadow([G(0.04, 0.96), G(0.96, 0.96), G(1.02, 1.0), G(0.10, 1.0)], 60, 7)
    # ---- 경기장 바닥: 바깥 흙 경고 트랙 -> 잔디(줄무늬) -> 내야 흙 -> 내야 잔디 ----
    s.grad([G(F0, F0, 0.4), G(F1, F0, 0.4), G(F1, F1, 0.4), G(F0, F1, 0.4)], hx('c89860'), hx('a67a44'), (0.4, 1), 4)                          # 경고 트랙(외야 담 앞 흙)
    s.grad([G(F0, F0, 0.5), G(F1 - 0.05, F0, 0.5), G(F1 - 0.05, F1 - 0.05, 0.5), G(F0, F1 - 0.05, 0.5)], hx('62b852'), hx('3e8c36'), (0.4, 1), 3)       # 잔디
    for k in range(0, 9):
        a0 = F0 + 0.08 * k
        if a0 < F1 - 0.05: s.flat([G(a0, F0, 0.55), G(min(a0 + 0.04, F1 - 0.05), F0, 0.55), G(min(a0 + 0.04, F1 - 0.05), F1 - 0.05, 0.55), G(a0, F1 - 0.05, 0.55)], hx('78cc66', 100))
    inf = [G(0.16 + 0.34 * np.cos(t) * 0 + 0.30 * (0.5 + 0.5 * np.cos(t)) * 0 + x, 0, 0) for t, x in ((0, 0),)]  # (자리 표시, 사용 안 함)
    # 내야 흙: 홈 주변 부채꼴 + 베이스 사이 길
    dirt = [G(0.17, 0.17, 0.6)] + [G(0.17 + 0.46 * np.cos(t), 0.17 + 0.46 * np.sin(t), 0.6) for t in np.linspace(0.0, np.pi / 2, 18)]
    s.grad(dirt, hx('dcae72'), hx('b88846'), (0.4, 1), 4)
    s.grad([G(0.24, 0.24, 0.65), G(0.46, 0.24, 0.65), G(0.46, 0.46, 0.65), G(0.24, 0.46, 0.65)], hx('64ba54'), hx('3e8c36'), (0.4, 1), 3)                  # 내야 잔디
    for p in ([G(0.17, 0.17, 0.7), G(F1 - 0.03, 0.17, 0.7)], [G(0.17, 0.17, 0.7), G(0.17, F1 - 0.03, 0.7)]): s.line(p, hx('ffffff', 235), 3)               # 파울 라인
    x, y = G(0.34, 0.34, 0.8); d.ellipse([x - 8, y - 4, x + 8, y + 4], fill=hx('d8aa70')); d.rectangle([x - 2, y - 1, x + 2, y + 1], fill=hx('ffffff'))        # 마운드 + 투수판
    for (a, b) in ((0.44, 0.19), (0.19, 0.44), (0.44, 0.44), (0.175, 0.175)):                                                                         # 1루·3루·2루·홈
        x, y = G(a, b, 0.8); d.polygon([(x - 6, y), (x, y - 3), (x + 6, y), (x, y + 3)], fill=hx('fcfcfc')); d.line([(x - 6, y), (x, y + 3), (x + 6, y)], fill=hx('b8b8b8'), width=1)
    x, y = G(0.185, 0.185, 0.8); d.ellipse([x - 10, y - 5, x + 10, y + 5], outline=hx('fcfcfc', 200), width=2)                                          # 타석 원
    # 더그아웃(1루·3루): 낮은 지붕 상자
    for (a0, a1, b0, b1) in ((0.30, 0.42, 0.115, 0.15), (0.115, 0.15, 0.30, 0.42)):
        s.box(a0, a1, b0, b1, 2.4, (hx('2a4aa8'), hx('1a3280')), (hx('1a3280'), hx('0e2060')), (hx('e8ecf8'), hx('b4bcd4')), 0.4)
    # ---- 홈 뒤 관중석(ㄱ자): 안쪽 면이 보이는 계단식 좌석 ----
    N = 9; ZR = 0.95
    def seat_row(k, along):
        z0 = 0.5 + k * ZR; z1 = z0 + ZR; off0 = 0.15 - 0.0105 * k; off1 = 0.15 - 0.0105 * (k + 1)
        col = [hx('2850b8'), hx('e8ecf8')][(k // 2) % 2] if k < 6 else hx('f4c43a')            # 아래는 파랑/흰, 위는 노랑 띠
        dark = lerp(col, (0, 0, 0), 0.35)
        if along == 'b':                                                                           # b 작은 쪽(오른쪽 뒤) 스탠드: 폭은 a 방향
            r = lambda a0, a1: [G(a0, off0, z0), G(a1, off0, z0), G(a1, off0, z1), G(a0, off0, z1)]
            t = lambda a0, a1: [G(a0, off0, z1), G(a1, off0, z1), G(a1, off1, z1), G(a0, off1, z1)]
            return r(0.13, 0.90), t(0.13, 0.90), col, dark
        r = lambda b0, b1: [G(off0, b0, z0), G(off0, b1, z0), G(off0, b1, z1), G(off0, b0, z1)]
        t = lambda b0, b1: [G(off0, b0, z1), G(off0, b1, z1), G(off1, b1, z1), G(off1, b0, z1)]
        return r(0.13, 0.90), t(0.13, 0.90), col, dark
    for k in range(N - 1, -1, -1):
        for along in ('b', 'a'):
            rq, tq, col, dark = seat_row(k, along)
            s.flat(tq, lerp(col, (255, 255, 255), 0.30)); s.grad(rq, lerp(col, (255, 255, 255), 0.10), dark, (0, 1))
    # 통로 계단(밝은 회색 세로띠) + 난간선
    for a in (0.30, 0.50, 0.70):
        s.line([G(a, 0.15, 0.5), G(a, 0.15 - 0.0105 * N, 0.5 + N * ZR)], hx('dcdce8'), 6)
    for b in (0.30, 0.50, 0.70):
        s.line([G(0.15, b, 0.5), G(0.15 - 0.0105 * N, b, 0.5 + N * ZR)], hx('dcdce8'), 6)
    # 필드 앞 광고 펜스(진파랑 띠)
    s.grad([G(0.13, 0.15, 0.4), G(0.90, 0.15, 0.4), G(0.90, 0.15, 1.5), G(0.13, 0.15, 1.5)], hx('1a2a68'), hx('0e1844'), (0, 1))
    s.grad([G(0.15, 0.13, 0.4), G(0.15, 0.90, 0.4), G(0.15, 0.90, 1.5), G(0.15, 0.13, 1.5)], hx('1a2a68'), hx('0e1844'), (0, 1))
    # 지붕 캐노피(관중석 위) + 지지 기둥
    ztop = 0.5 + N * ZR
    for along in ('b', 'a'):
        if along == 'b':
            q = [G(0.12, 0.15 - 0.0105 * N + 0.05, ztop - 3.2), G(0.92, 0.15 - 0.0105 * N + 0.05, ztop - 3.2), G(0.92, 0.015, ztop + 1.0), G(0.12, 0.015, ztop + 1.0)]
        else:
            q = [G(0.15 - 0.0105 * N + 0.05, 0.12, ztop - 3.2), G(0.15 - 0.0105 * N + 0.05, 0.92, ztop - 3.2), G(0.015, 0.92, ztop + 1.0), G(0.015, 0.12, ztop + 1.0)]
        s.grad(q, hx('f6f8fc'), hx('b8c0d8'), (0.2, 1)); s.line([q[0], q[1]], hx('2a50b8'), 4); s.line([q[3], q[2]], hx('d8dcec'), 3)
    for t in (0.2, 0.45, 0.7, 0.9):
        a = 0.12 + 0.78 * t; s.line([G(a, 0.15 - 0.0105 * N + 0.04, ztop - 3.2), G(a, 0.02, ztop - 3.2)], hx('8a92b0', 120), 2)
    # 기자석(홈 뒤 가운데 모서리 위): 파란 유리 상자
    s.box(0.12, 0.34, 0.012, 0.062, 3.2, (hx('8ec0ee'), hx('2a5a98')), (hx('4a78b0'), hx('1a3a68')), (hx('f0f4fa'), hx('c0c8dc')), ztop - 1.2)
    for k in range(1, 7): s.line([G(0.12 + 0.22 * k / 7, 0.062, ztop - 1.2), G(0.12 + 0.22 * k / 7, 0.062, ztop + 2.0)], hx('f0f4fa'), 2)
    # ---- 외야: 낮은 초록 펜스 + 전광판 + 파울 폴 + 외야 응원석 일부 ----
    s.grad([G(F0, F1, 0.4), G(F1, F1, 0.4), G(F1, F1, 2.6), G(F0, F1, 2.6)], hx('2a9a50'), hx('145a2c'), (0, 1))
    s.grad([G(F1, F0, 0.4), G(F1, F1, 0.4), G(F1, F1, 2.6), G(F1, F0, 2.6)], hx('1a7a3a'), hx('0c4a22'), (0, 1))
    s.line([G(F0, F1, 2.6), G(F1, F1, 2.6)], hx('f4e44a'), 3); s.line([G(F1, F0, 2.6), G(F1, F1, 2.6)], hx('d8c838'), 3)
    s.grad([G(0.38, F1, 2.6), G(0.66, F1, 2.6), G(0.66, F1, 7.6), G(0.38, F1, 7.6)], hx('1e2640'), hx('0a1020'), (0, 1))                         # 전광판 틀
    s.line([G(0.38, F1, 2.6), G(0.38, F1, 7.6), G(0.66, F1, 7.6), G(0.66, F1, 2.6)], hx('c8ccdc'), 3)
    s.text_face('IKSAN', (G(0.40, F1)[0], G(0.40, F1, 7.0)[1] + 1), 0, 0, 0.13, hx('ffd84a'), size=44, shear=0.5)
    s.text_face('0 : 0', (G(0.45, F1)[0], G(0.45, F1, 4.2)[1] + 1), 0, 0, 0.12, hx('6af0a0'), size=40, shear=0.5)
    for (a, b) in ((F1, F0), (F0, F1)):                                                                                                             # 파울 폴(노랑)
        x, y = G(a, b, 0.4); d.line([(x, y), (x, y - 9 * S)], fill=hx('f8e040'), width=4)
    for k in range(5):                                                                                                                              # 오른쪽 외야 응원석(낮은 3단)
        z0 = 2.6 + k * 0.0
    for k in range(3):
        s.grad([G(F1 - 0.02 - 0.012 * k, 0.62, 0.4 + 0.9 * k), G(F1 - 0.02 - 0.012 * k, 0.84, 0.4 + 0.9 * k), G(F1 - 0.02 - 0.012 * k, 0.84, 1.3 + 0.9 * k), G(F1 - 0.02 - 0.012 * k, 0.62, 1.3 + 0.9 * k)], hx('4a6ad0') if k % 2 else hx('f0f2fa'), hx('2a40a0'), (0, 1))
    # ---- 조명탑 4개(철탑 + 램프 배열) ----
    for (a, b) in ((0.07, 0.07), (0.94, 0.07), (0.07, 0.94), (0.94, 0.94)):
        x, y = G(a, b); hh = 13.5 * S
        s.shadow([(x - 4, y), (x + 4, y), (x + 26, y + 10), (x + 14, y + 10)], 50, 4)
        d.line([(x - 3, y), (x - 1, y - hh)], fill=hx('8a92aa'), width=3); d.line([(x + 3, y), (x + 1, y - hh)], fill=hx('8a92aa'), width=3)
        for z in range(1, 6): zz = y - hh * z / 6; d.line([(x - 3 + z * 0.3, zz), (x + 3 - z * 0.3, zz + hh / 12)], fill=hx('b8bed2'), width=1)
        d.rectangle([x - 20, y - hh - 14, x + 20, y - hh + 2], fill=hx('2a3050'))
        for r in range(2):
            for c in range(-3, 4): d.ellipse([x + c * 5.4 - 2.4, y - hh - 12 + r * 7, x + c * 5.4 + 2.4, y - hh - 6 + r * 7], fill=hx('fff8d0'))
    # ---- 입구 광장: 매표소 + 깃발 ----
    s.box(0.40, 0.54, 0.93, 0.99, 3.0, (hx('f4f4fa'), hx('c0c4d8')), (hx('c0c4d8'), hx('8a90ac')), (hx('2a50b8'), hx('1a3480')))
    x, y = G(0.50, 0.99, 0.0); d.line([(x, y), (x, y - 11 * S)], fill=hx('9aa0b8'), width=3); d.polygon([(x, y - 11 * S), (x + 22, y - 10 * S + 6), (x, y - 9 * S + 4)], fill=hx('e84b4b'))
    return s.finish()

# ---------------- 백제왕궁 (왕궁리 유적: 오층석탑 + 정전) ----------------
def pagoda5(s, cx, cy, f, rnd):
    """오층석탑을 중심 (cx,cy), 크기 배율 f 로 그린다(LM.layered_roof 사용)."""
    G = s.G
    s.box(cx - 0.17 * f, cx + 0.17 * f, cy - 0.17 * f, cy + 0.17 * f, 1.8 * f, (hx('c4bca6'), hx('8e866e')), (hx('8e866e'), hx('5a543e')), LM.STONE_T, 0)
    z = 1.8 * f
    s.box(cx - 0.11 * f, cx + 0.11 * f, cy - 0.11 * f, cy + 0.11 * f, 6.2 * f, (hx('d8ccb0'), hx('a89c80')), (hx('a89c80'), hx('6a604a')), LM.STONE_T, z); LM.weathered(s, cx - 0.11 * f, cx + 0.11 * f, cy + 0.11 * f, z, 6.2 * f, rnd, 12)
    z += 6.2 * f
    for (w, h, rw) in ((0.095, 2.2, 0.15), (0.082, 2.2, 0.133), (0.07, 2.0, 0.116), (0.058, 1.9, 0.10), (0.048, 1.7, 0.083)):
        z = LM.layered_roof(s, cx, cy, rw * f, z, rnd, layers=4, thick=0.5 * f, taper=0.01 * f)
        s.box(cx - w * f, cx + w * f, cy - w * f, cy + w * f, h * f, LM.STONE_L, LM.STONE_R, LM.STONE_T, z); z += h * f
    z = LM.layered_roof(s, cx, cy, 0.07 * f, z, rnd, layers=3, thick=0.45 * f, taper=0.01 * f)
    x, y = G(cx, cy, z + 1.2 * f); ImageDraw.Draw(s.im).ellipse([x - 5, y - 6, x + 5, y + 3], fill=hx('d8d0bc'))

def baekje():
    s = Sc(84, 23); G = s.G; rnd = random.Random(7); d = ImageDraw.Draw(s.im)
    s.grad([G(0, 1), G(1, 1), G(1, 1, -3.4), G(0, 1, -3.4)], hx('a48048'), hx('6a4c28'), (0, 1), 4)
    s.grad([G(1, 0), G(1, 1), G(1, 1, -3.4), G(1, 0, -3.4)], hx('82602e'), hx('4e3818'), (0, 1), 4)
    s.grad(s.ground_poly(), hx('d2c870'), hx('a4a448'), (0.4, 1), 5)
    for i in range(1400):
        a, b = rnd.random(), rnd.random(); x, y = G(a, b); d.line([(x, y), (x + rnd.randint(-2, 2), y - rnd.randint(4, 9))], fill=rnd.choice([hx('e8e286', 150), hx('7e8a34', 140), hx('b4ae54', 130)]), width=2)
    d.line([G(0, 1), G(1, 1), G(1, 0)], fill=hx('ece698', 200), width=3)
    # 궁장(宮牆): 뒤 두 변을 따라 낮은 흙·기와 담 + 앞쪽 문
    for (p0, p1) in (((0.04, 0.04), (0.96, 0.04)), ((0.04, 0.04), (0.04, 0.96))):
        s.grad([G(p0[0], p0[1], 0), G(p1[0], p1[1], 0), G(p1[0], p1[1], 3.4), G(p0[0], p0[1], 3.4)], hx('c8b890'), hx('8e8060'), (0, 1), 4)
        s.line([G(p0[0], p0[1], 3.4), G(p1[0], p1[1], 3.4)], hx('4a4c58'), 7); s.line([G(p0[0], p0[1], 3.9), G(p1[0], p1[1], 3.9)], hx('7a7c8a'), 5)
    s.shadow([G(0.46, 0.50), G(0.88, 0.50), G(1.0, 0.62), G(0.58, 0.64)], 100, 10)
    s.shadow([G(0.14, 0.14), G(0.52, 0.14), G(0.60, 0.26), G(0.22, 0.26)], 90, 9)
    pagoda5(s, 0.30, 0.30, 0.95, rnd)                                                           # 석탑(뒤쪽 왼편)
    # 정전: 기단(2단 + 계단) + 붉은 기둥 + 크림 벽 + 회색 기와 팔작지붕
    s.box(0.50, 0.92, 0.46, 0.90, 1.6, (hx('cbc4ae'), hx('8e866e')), (hx('8e866e'), hx('5a543e')), LM.STONE_T, 0)
    s.box(0.54, 0.88, 0.50, 0.86, 1.2, LM.STONE_L, LM.STONE_R, LM.STONE_T, 1.6)
    for k in range(4): s.box(0.66 - 0.01 * k, 0.76 + 0.01 * k, 0.86, 0.86 + 0.04 * (4 - k) * 0.5, 0.45 * (k + 1), (hx('dcd5c1'), hx('aaa28e')), (hx('aaa28e'), hx('6f6858')), LM.STONE_T, 0)
    A0, A1, B0, B1, z0, HW = 0.58, 0.84, 0.54, 0.82, 2.8, 5.6
    s.box(A0, A1, B0, B1, HW, (hx('f2e8cc'), hx('cdbf9a')), (hx('cdbf9a'), hx('9a8c68')), (hx('8a8c98'), hx('5a5c6a')), z0)
    for i in range(5):
        a = A0 + (A1 - A0) * i / 4; s.line([G(a, B1, z0), G(a, B1, z0 + HW)], hx('b8281e'), 7); s.line([G(a - 0.004, B1, z0), G(a - 0.004, B1, z0 + HW)], hx('e8604a'), 2)
    for i in range(4):
        a = A0 + (A1 - A0) * (i + 0.18) / 4; s.grad([G(a, B1, z0 + 0.6), G(a + 0.045, B1, z0 + 0.6), G(a + 0.045, B1, z0 + HW - 0.6), G(a, B1, z0 + HW - 0.6)], hx('3a2a1c') if i in (1, 2) else hx('c8b890'), hx('1a1008') if i in (1, 2) else hx('9a8a68'), (0, 1))
    zr = z0 + HW; RZ = 5.2; ov = 0.04
    s.grad([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.05, (B0 + B1) / 2, zr + RZ), G(A0 + 0.05, (B0 + B1) / 2, zr + RZ)], hx('7a7c8c'), hx('424454'), (0.2, 1), 4)
    s.grad([G(A1 + ov, B0 - ov, zr), G(A1 + ov, B1 + ov, zr), G(A1 - 0.05, (B0 + B1) / 2, zr + RZ)], hx('5a5c6c'), hx('2e303c'), (0.2, 1), 4)
    for k in range(1, 8):
        t = k / 8; bb = (B1 + ov) + ((B0 + B1) / 2 - (B1 + ov)) * t; zz = zr + RZ * t; aL = A0 - ov + (0.09 + ov) * t; aR = A1 + ov - (0.09 + ov) * t
        s.line([G(aL, bb, zz), G(aR, bb, zz)], hx('26283a', 170), 3)
    s.line([G(A0 + 0.05, (B0 + B1) / 2, zr + RZ), G(A1 - 0.05, (B0 + B1) / 2, zr + RZ)], hx('a4a6b8'), 5)
    s.line([G(A0 - ov, B1 + ov, zr), G(A1 + ov, B1 + ov, zr)], hx('c4c6d8'), 4)
    s.line([G(A0 - ov, B1 + ov, zr), G(A0 - ov - 0.012, B1 + ov, zr + 0.9)], hx('c4c6d8'), 4); s.line([G(A1 + ov, B1 + ov, zr), G(A1 + ov + 0.012, B1 + ov, zr + 0.9)], hx('c4c6d8'), 4)
    # 붉은 말뚝 + 흰 줄(석탑 앞마당)
    ps = [(t, 0.50) for t in np.linspace(0.10, 0.44, 5)] + [(0.46, t) for t in np.linspace(0.10, 0.44, 5)]
    for (a, b) in ps:
        x, y = G(a, b); d.polygon([(x - 3, y), (x + 3, y), (x + 3, y - 12), (x - 3, y - 12)], fill=hx('c8382a')); d.ellipse([x - 3.5, y - 14, x + 3.5, y - 10], fill=hx('f4dcd2'))
    s.tree(0.04, 0.70, 0.8); s.tree(0.30, 0.92, 0.8)
    return s.finish()

if __name__ == '__main__':
    items = {'b42': arts, 'b33': baseball, 'b38': baekje}
    sheet = Image.new('RGBA', (3 * 96 * 7 + 40, 84 * 7), (150, 70, 160, 255)); x = 0
    for n, fn in items.items():
        im = fn(); im.save(f'{out}/{n}.png'); bg = Image.new('RGBA', im.size, (150, 70, 160, 255)); bg.alpha_composite(im); sheet.paste(bg.resize((im.width * 7, im.height * 7), Image.NEAREST), (x, 0)); x += 96 * 7 + 20
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('추가 건물 →', out)
