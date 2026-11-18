# -*- coding: utf-8 -*-
"""양식의 쪽 높이를 글자·표 크기로 어림한다(한글 없이). 쪽을 거의 채우는 양식 뒤에 빈 줄을 붙여 빈 쪽이 생기는 것을 막는 데 쓴다."""
import math

from . import package as P
from .package import HP

PAGE_H = 84188
# 한글 기본 용지(위 20·아래 15·머리말/꼬리말 10mm), 좌우 30mm 일 때의 본문 영역 / 샘플 자체 여백
DEFAULT_BODY = (PAGE_H - 5669 - 4252 - 2835 - 2835, 59528 - 2 * 8504)


def body_area(pkg):
    sp = next(pkg.section(0).iter(HP + "secPr"), None)
    if sp is None:
        return DEFAULT_BODY
    pg = sp.find(HP + "pagePr")
    m = pg.find(HP + "margin")
    g = lambda k: int(m.get(k, "0"))  # noqa: E731
    return (int(pg.get("height")) - g("top") - g("bottom") - g("header") - g("footer"), int(pg.get("width")) - g("left") - g("right"))


def para_height(p, st, width):
    ps = st.para.get(p.get("paraPrIDRef"), {"ls": 160, "prev": 0, "next": 0, "left": 0, "right": 0})
    runs = p.findall(HP + "run")
    cs = st.char.get(runs[0].get("charPrIDRef")) if runs else None
    h = (cs or {"h": 1000})["h"]
    objs = []
    for e in p.iter():
        n = P.local(e)
        if n in ("tbl", "pic", "container", "rect", "ellipse"):
            pos, sz = e.find(HP + "pos"), e.find(HP + "sz")
            if sz is not None and (pos is None or pos.get("treatAsChar") == "1"):
                objs.append(int(sz.get("height", "0")))
    own = "".join(t.text or "" for r in runs for t in r.findall(HP + "t"))
    avail = max(4000, width - ps.get("left", 0) - ps.get("right", 0))
    cw = h * 0.55
    lines = max(1, math.ceil(len(own) * cw / avail)) if own else 1
    line_h = int(h * ps.get("ls", 160) / 100)
    text_h = lines * line_h
    tall = max(objs) if objs else 0
    return (max(text_h if (own or not objs) else 0, tall) + ps.get("prev", 0) + ps.get("next", 0))




def estimate_height(pkg):
    """양식 본문 전체 높이(HWPUNIT) 어림값"""
    from . import recipe
    st = recipe._Styles(pkg)
    return sum(para_height(p, st, DEFAULT_BODY[1]) for p in pkg.section(0).findall(HP + "p"))
