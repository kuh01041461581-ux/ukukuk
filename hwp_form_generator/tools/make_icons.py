# -*- coding: utf-8 -*-
"""위젯 아이콘(선 아이콘, 20px, 같은 굵기·같은 둥근 끝)을 PIL 로 그려 assets/icons 에 저장.
   색 두 벌: 파랑(보통) / 흰색(강조 단추 위). 다시 만들기: python tools/make_icons.py"""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "icons")
S = 24          # 최종 크기
K = 12          # 확대 배율(부드럽게)
W = 2.7         # 선 굵기(그릴 때 px 기준 — 배지 안으로 줄여 넣으므로 굵게)


def canvas():
    im = Image.new("RGBA", (S * K, S * K), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def line(d, pts, col):
    pts = [(x * K, y * K) for x, y in pts]
    d.line(pts, fill=col, width=int(W * K), joint="curve")
    r = W * K / 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse((x - r, y - r, x + r, y + r), fill=col)


def rrect(d, box, rad, col):
    x0, y0, x1, y1 = [v * K for v in box]
    d.rounded_rectangle((x0, y0, x1, y1), radius=rad * K, outline=col, width=int(W * K))


def circle(d, c, r, col, fill=False):
    x, y = c
    d.ellipse(((x - r) * K, (y - r) * K, (x + r) * K, (y + r) * K), outline=col, width=int(W * K),
              fill=col if fill else None)


ICONS = {}


def icon(name):
    def deco(fn):
        ICONS[name] = fn
        return fn
    return deco


@icon("doc")
def _(d, c):
    rrect(d, (4.5, 2.5, 15.5, 17.5), 2, c)
    line(d, [(7.5, 7), (12.5, 7)], c); line(d, [(7.5, 10.2), (12.5, 10.2)], c); line(d, [(7.5, 13.4), (10.5, 13.4)], c)


@icon("event")        # 깃발
def _(d, c):
    line(d, [(5, 17.5), (5, 2.8)], c)
    line(d, [(5, 3.8), (15, 3.8), (12.6, 7), (15, 10.2), (5, 10.2)], c)


@icon("project")      # 폴더
def _(d, c):
    line(d, [(2.8, 6), (2.8, 15.5), (17.2, 15.5), (17.2, 7.2), (9.5, 7.2), (8, 4.8), (2.8, 4.8), (2.8, 6)], c)


@icon("mayor")        # 청사(기둥 건물)
def _(d, c):
    line(d, [(2.6, 8), (10, 3), (17.4, 8), (2.6, 8)], c)
    for x in (5.4, 8.5, 11.5, 14.6):
        line(d, [(x, 10), (x, 14.6)], c)
    line(d, [(2.6, 17), (17.4, 17)], c)


@icon("cover")        # 책
def _(d, c):
    rrect(d, (4, 2.5, 16, 17.5), 2, c)
    line(d, [(7.6, 2.8), (7.6, 17.2)], c)
    line(d, [(10.4, 7), (13.4, 7)], c)


@icon("plan")         # 체크 목록
def _(d, c):
    line(d, [(3, 5.4), (4.4, 6.8), (6.6, 4)], c)
    line(d, [(9.2, 5.5), (17, 5.5)], c)
    line(d, [(3, 11.4), (4.4, 12.8), (6.6, 10)], c)
    line(d, [(9.2, 11.5), (17, 11.5)], c)
    line(d, [(9.2, 16.5), (14, 16.5)], c)


@icon("schedule")     # 달력
def _(d, c):
    rrect(d, (3, 4.2, 17, 17.2), 2.2, c)
    line(d, [(3.2, 8.4), (16.8, 8.4)], c)
    line(d, [(7, 2.6), (7, 5.6)], c); line(d, [(13, 2.6), (13, 5.6)], c)
    line(d, [(7, 12), (7.1, 12)], c); line(d, [(10, 12), (10.1, 12)], c); line(d, [(13, 12), (13.1, 12)], c)


@icon("user")
def _(d, c):
    circle(d, (10, 6.8), 3.2, c)
    line(d, [(3.6, 17), (3.6, 15.4), (6, 12.8), (14, 12.8), (16.4, 15.4), (16.4, 17)], c)


@icon("plus")
def _(d, c):
    line(d, [(10, 4), (10, 16)], c); line(d, [(4, 10), (16, 10)], c)


@icon("link")         # 연결
def _(d, c):
    rrect(d, (2.8, 7.4, 10.4, 12.6), 2.6, c)
    rrect(d, (9.6, 7.4, 17.2, 12.6), 2.6, c)


@icon("sliders")
def _(d, c):
    for y, x in ((5, 7), (10, 13), (15, 9)):
        line(d, [(3, y), (17, y)], c)
        circle(d, (x, y), 1.7, "#ffffff" if c != "#ffffff" else "#5e6ad2", fill=True)
        circle(d, (x, y), 1.7, c)


@icon("clip")         # 참고자료(클립)
def _(d, c):
    line(d, [(13.4, 6.4), (7.6, 12.2), (7.6, 12.2)], c)
    line(d, [(6.4, 9.4), (11.2, 4.6), (14.8, 8.2), (8.2, 15), (4.8, 11.6), (10.6, 5.8)], c)


@icon("minus")
def _(d, c):
    line(d, [(4.5, 10), (15.5, 10)], c)


@icon("dock")         # 옆에 붙이기
def _(d, c):
    rrect(d, (2.8, 3.6, 17.2, 16.4), 2, c)
    line(d, [(12, 3.8), (12, 16.2)], c)


@icon("tray")         # 트레이로 보내기(아래 화살표 + 받침)
def _(d, c):
    line(d, [(10, 3.4), (10, 11.6)], c)
    line(d, [(6.4, 8.2), (10, 11.8), (13.6, 8.2)], c)
    line(d, [(3.6, 12.8), (3.6, 16.4), (16.4, 16.4), (16.4, 12.8)], c)


@icon("table")        # 표
def _(d, c):
    rrect(d, (3, 3.5, 17, 16.5), 2, c)
    line(d, [(3.2, 8.4), (16.8, 8.4)], c); line(d, [(3.2, 12.6), (16.8, 12.6)], c); line(d, [(8.6, 3.7), (8.6, 16.3)], c)


@icon("date")         # 오늘 날짜(달력+점)
def _(d, c):
    rrect(d, (3, 4.2, 17, 17.2), 2.6, c)
    line(d, [(7, 2.6), (7, 5.6)], c); line(d, [(13, 2.6), (13, 5.6)], c)
    circle(d, (10, 11.6), 1.6, c, fill=True)


@icon("pagebreak")    # 쪽 나누기
def _(d, c):
    rrect(d, (5, 2.6, 15, 7.6), 1.6, c)
    rrect(d, (5, 12.4, 15, 17.4), 1.6, c)
    line(d, [(2.6, 10), (4.6, 10)], c); line(d, [(8.2, 10), (11.8, 10)], c); line(d, [(15.4, 10), (17.4, 10)], c)


@icon("text")         # 자주 쓰는 문구(말풍선)
def _(d, c):
    rrect(d, (3, 3.6, 17, 13.6), 3, c)
    line(d, [(7, 13.8), (6.4, 17), (10.4, 13.8)], c)
    line(d, [(7, 7.4), (13, 7.4)], c); line(d, [(7, 10.2), (11, 10.2)], c)


@icon("calc")         # 계산기
def _(d, c):
    rrect(d, (4, 2.6, 16, 17.4), 2.4, c)
    line(d, [(6.8, 6.4), (13.2, 6.4)], c)
    for x, y in ((7.4, 10.6), (10, 10.6), (12.6, 10.6), (7.4, 14), (10, 14), (12.6, 14)):
        line(d, [(x, y), (x + 0.1, y)], c)


@icon("swap")         # 서식 바꾸기
def _(d, c):
    line(d, [(3.6, 7), (15.6, 7)], c); line(d, [(12.6, 4), (15.8, 7), (12.6, 10)], c)
    line(d, [(16.4, 13), (4.4, 13)], c); line(d, [(7.4, 10), (4.2, 13), (7.4, 16)], c)


@icon("nation")       # 국가예산(별)
def _(d, c):
    import math
    pts = []
    for i in range(10):
        r = 7.6 if i % 2 == 0 else 3.6
        a = -math.pi / 2 + i * math.pi / 5
        pts.append((10 + r * math.cos(a), 10.4 + r * math.sin(a)))
    line(d, pts + [pts[0]], c)


# 아이콘마다 파스텔 색(배지) — 동글동글 귀엽게
BADGE = {"doc": "#7c8cf5", "event": "#ff8a7a", "project": "#ffb04a", "mayor": "#6c9cf0", "cover": "#b68cf0", "plan": "#4cc2a0",
         "schedule": "#f27fb0", "user": "#7c8cf5", "plus": "#4cc2a0", "link": "#7c8cf5", "sliders": "#9aa3c0", "clip": "#ffb04a",
         "minus": "#9aa3c0", "dock": "#7c8cf5", "tray": "#9aa3c0", "table": "#4cb7e8", "date": "#f27fb0", "pagebreak": "#b68cf0",
         "text": "#ff8a7a", "calc": "#4cc2a0", "swap": "#ffb04a", "nation": "#ffb04a"}


def badge(fn, name, white):
    from PIL import ImageFilter           # noqa: F401
    big = S * K
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    col = BADGE.get(name, "#7c8cf5")
    d.ellipse((0.5 * K, 0.5 * K, big - 0.5 * K, big - 0.5 * K), fill="#ffffff" if white else col)
    g, gd = canvas()
    fn(gd, col if white else "#ffffff")
    g = g.resize((int(big * 0.66), int(big * 0.66)), Image.LANCZOS)
    im.alpha_composite(g, ((big - g.width) // 2, (big - g.height) // 2 + int(0.2 * K)))
    return im.resize((S, S), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in ICONS.items():
        for tag in ("b", "w"):
            badge(fn, name, tag == "w").save(os.path.join(OUT, "%s_%s.png" % (name, tag)))
    print(len(ICONS) * 2, "개 →", OUT)


if __name__ == "__main__":
    main()
