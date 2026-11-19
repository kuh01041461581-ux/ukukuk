# -*- coding: utf-8 -*-
"""양식의 쪽 높이를 글자·표 크기로 어림한다(한글 없이). 쪽을 거의 채우는 양식 뒤에 빈 줄을 붙여 빈 쪽이 생기는 것을 막는 데 쓴다."""
import math

import re

from . import package as P
from .package import HH, HP

HC = "{http://www.hancom.co.kr/hwpml/2011/core}"

def _hex(c):
    c = (c or "").strip()
    return c if re.fullmatch(r"#[0-9a-fA-F]{6}", c) else None


def _mm(w):
    m = re.match(r"([\d.]+)", w or "")
    return float(m.group(1)) if m else 0.1


class Styles:
    def __init__(self, pkg):
        h = pkg.header()
        self.fonts = {}
        self.fonts_by = {}
        for ff in h.iter(HH + "fontface"):
            lg = (ff.get("lang") or "").lower()
            self.fonts_by[lg] = {f.get("id"): f.get("face") for f in ff.findall(HH + "font")}
            if lg == "hangul":
                self.fonts = self.fonts_by[lg]
        self.char = {}
        LANGS = ("hangul", "latin", "hanja", "japanese", "other", "symbol", "user")
        for cp in h.iter(HH + "charPr"):
            fr, ra, sp = (cp.find(HH + t) for t in ("fontRef", "ratio", "spacing"))
            ul = cp.find(HH + "underline")
            self.char[cp.get("id")] = {
                "font": self.fonts.get(fr.get("hangul")) if fr is not None else None,
                "fonts": {lg: self.fonts_by.get(lg, {}).get(fr.get(lg)) for lg in LANGS} if fr is not None else {},
                "h": int(cp.get("height", "1000")),
                "bold": cp.find(HH + "bold") is not None, "italic": cp.find(HH + "italic") is not None,
                "ul": ul is not None and ul.get("type", "NONE") not in ("NONE", ""),
                "color": _hex(cp.get("textColor")) or "#000000",
                "ratio": int(ra.get("hangul", "100")) if ra is not None else 100,
                "spacing": int(sp.get("hangul", "0")) if sp is not None else 0,
                "ratios": {lg: int(ra.get(lg, "100")) for lg in LANGS} if ra is not None else {},
                "spacings": {lg: int(sp.get(lg, "0")) for lg in LANGS} if sp is not None else {},
            }
        self.para = {}
        for pp in h.iter(HH + "paraPr"):
            al = pp.find(HH + "align")
            mg = next(pp.iter(HH + "margin"), None)
            ls = next(pp.iter(HH + "lineSpacing"), None)

            def mv(tag):
                e = mg.find(HC + tag) if mg is not None else None
                return int(e.get("value", "0")) if e is not None else 0
            self.para[pp.get("id")] = {
                "align": al.get("horizontal", "JUSTIFY") if al is not None else "JUSTIFY",
                "left": mv("left"), "right": mv("right"), "indent": mv("intent"), "prev": mv("prev"), "next": mv("next"),
                "ls": int(ls.get("value", "160")) if (ls is not None and ls.get("type") == "PERCENT") else 160,
            }
        self.bf = {}
        for b in h.iter(HH + "borderFill"):
            d = {}
            for k, tag in (("l", "leftBorder"), ("r", "rightBorder"), ("t", "topBorder"), ("b", "bottomBorder")):
                e = b.find(HH + tag)
                d[k] = [e.get("type", "NONE"), _mm(e.get("width"))] if e is not None else ["NONE", 0.1]
            fill = None
            wb = next(b.iter(HC + "winBrush"), None)
            if wb is not None:
                fill = _hex(wb.get("faceColor"))
            self.bf[b.get("id")] = {"bd": d, "fill": fill}
        self.bin = {}
        hpf = pkg.files.get("Contents/content.hpf")
        if hpf:
            root = P.parse_xml(hpf)
            for it in root.iter(P.OPF + "item"):
                if it.get("href", "").startswith("BinData/"):
                    self.bin[it.get("id")] = it.get("href")




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
    st = Styles(pkg)
    return sum(para_height(p, st, DEFAULT_BODY[1]) for p in pkg.section(0).findall(HP + "p"))


def fit_page_tables(pkg, body_h=None, margin=2200, min_scale=0.75):
    """쪽 한 장에 가까운 큰 표가 같은 쪽의 앞 문단(제목 등)과 합쳐 쪽을 넘으면 표의 줄 높이를 줄여 한 쪽에 들어가게 한다.
    (예: 주간업무계획 표지의 '순 서' 제목 + 국 이름 표가 한 쪽에 안 들어가 제목만 한 쪽을 차지하고 표가 다음 쪽으로 밀리던 것)
    body_h: 본문 영역 높이(기본: 한글 기본 용지). 원래 높이의 min_scale 배까지만 줄인다. → 줄인 표 수"""
    body_h = body_h or DEFAULT_BODY[0]
    st = Styles(pkg)
    width = DEFAULT_BODY[1]
    n, used = 0, 0
    for p in pkg.section(0).findall(HP + "p"):
        if p.get("pageBreak") == "1":
            used = 0
        tbls = [t for t in p.iter(HP + "tbl")]
        sizes = [(int(t.find(HP + "sz").get("height", "0")), t) for t in tbls if t.find(HP + "sz") is not None]
        big = max(sizes, key=lambda x: x[0])[1] if sizes else None
        h = para_height(p, st, width)
        if big is not None and big.find(HP + "sz") is not None:
            th = int(big.find(HP + "sz").get("height", "0"))
            avail = body_h - margin - used
            if th >= 40000 and th > avail and avail >= th * min_scale:
                k = avail / float(th)
                for tc in big.findall(HP + "tr/" + HP + "tc"):
                    sz = tc.find(HP + "cellSz")
                    if sz is not None:
                        sz.set("height", str(int(int(sz.get("height", "0")) * k)))
                big.find(HP + "sz").set("height", str(int(th * k)))
                h = max(0, h - (th - int(th * k)))
                n += 1
        used += h
        if used > body_h:
            used = h
    return n
