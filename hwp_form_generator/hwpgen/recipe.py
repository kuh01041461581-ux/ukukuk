# -*- coding: utf-8 -*-
"""'만드는 순서'(레시피) 변환기와 재생 엔진.

참고 프로그램(범정부 오피스)은 양식 파일을 끼워 넣지 않고, 한글 명령(여백·표 만들기·칸 색·글꼴·글 입력…)을 차례로 실행해
문서를 만든다. 한글이 표와 글을 직접 만들므로 '파일 손상'이 날 수 없다. 이 모듈은 같은 방식을 쓴다.

 - extract(pkg): 우리가 만든 양식(.hwpx 묶음)에서 '만드는 순서'(JSON 으로 저장할 수 있는 사전)를 뽑는다
 - Player(br, recipe).run(): 그 순서를 열려 있는 한글에 명령으로 재생한다

레시피 모양:
  {"v": 1, "page": {...}|None, "steps": [ 줄 | 표 | 그림 ]}
  줄  {"k":"p","para":{align,left,right,indent,prev,next,ls},"pb":bool,"runs":[{"text","cs":{font,h,bold,italic,ul,color,ratio,spacing}}]}
  표  {"k":"tbl","para":{...},"rows","cols","colw":[HWPUNIT],"rowh":[HWPUNIT],
       "cells":[{"r","c","rs","cs","fill","bd":{"l","r","t","b":[종류,굵기mm]},"va","paras":[줄…]}]}
  그림 {"k":"pic","img":"파일이름","w","h"}
단위는 HWPUNIT(1/7200 인치, 1pt = 100). 글자 크기 h 는 1/100 pt.
"""
import os
import re
import tempfile

from . import package as P
from .package import HH, HP, local

HC = "{http://www.hancom.co.kr/hwpml/2011/core}"


# ===================================================================== 변환기
from .pagecheck import Styles as _Styles, _hex, _mm  # noqa: E402,F401


def _text_of(t):
    out = [t.text or ""]
    for ch in t:
        n = local(ch)
        out.append({"tab": "\t", "lineBreak": "\n", "fwSpace": "　", "nbSpace": " "}.get(n, ""))
        out.append(ch.tail or "")
    return "".join(out)


def _flat_text(el):
    return " ".join(x for x in ("".join(t.text or "" for t in el.iter(HP + "t")).split()) if x)


def _paras(pkg, st, plist, images, in_cell=False):
    steps = []
    for p in plist:
        steps.extend(_para(pkg, st, p, images, in_cell))
    return steps


def _para(pkg, st, p, images, in_cell):
    ps = dict(st.para.get(p.get("paraPrIDRef"), st.para.get("0", {})))
    pb = p.get("pageBreak") == "1"
    steps, cur = [], None

    def newp():
        nonlocal cur, pb
        cur = {"k": "p", "para": ps, "pb": pb, "runs": []}
        pb = False
        steps.append(cur)
    for run in p.findall(HP + "run"):
        cs = st.char.get(run.get("charPrIDRef"), st.char.get("0", {}))
        for ch in run:
            n = local(ch)
            if n == "t":
                tx = _text_of(ch)
                if tx:
                    if cur is None:
                        newp()
                    cur["runs"].append({"text": tx, "cs": cs})
            elif n == "tbl":
                if in_cell:                                # 칸 안의 표는 글만(한 줄로)
                    tx = _flat_text(ch)
                    if tx:
                        if cur is None:
                            newp()
                        cur["runs"].append({"text": tx, "cs": cs})
                else:
                    t = _table(pkg, st, ch, images)
                    t["para"], t["pb"] = ps, pb
                    pb = False
                    steps.append(t)
                    cur = None
            elif n == "pic":
                img = ch.find(".//" + HC + "img")
                href = st.bin.get(img.get("binaryItemIDRef")) if img is not None else None
                sz = ch.find(HP + "sz")
                if href and href in pkg.files:                 # 표 칸 안의 그림(로고 등)도 그 칸에 넣는다
                    name = os.path.basename(href)
                    images[name] = pkg.files[href]
                    steps.append({"k": "pic", "img": name, "para": ps, "pb": pb,
                                  "w": int(sz.get("width", "0")) if sz is not None else 0,
                                  "h": int(sz.get("height", "0")) if sz is not None else 0})
                    pb = False
                    cur = None
            elif n in ("rect", "ellipse", "container", "polygon", "curve", "arc", "line", "textart"):
                for sub in ch.iter(HP + "p"):               # 글상자 안 글은 그냥 줄로 풀어 쓴다
                    tx = P.ptext(sub).strip()
                    if tx and not list(sub.iter(HP + "tbl")):
                        steps.append({"k": "p", "para": ps, "pb": False, "runs": [{"text": tx, "cs": cs}]})
                cur = None
    if not steps:
        newp()                                             # 빈 줄도 한 줄
    elif pb and steps:
        steps[0]["pb"] = True
    return steps


def _table(pkg, st, tbl, images):
    rows, cols = int(tbl.get("rowCnt", "1")), int(tbl.get("colCnt", "1"))
    cells, colw, rowh = [], {}, {}
    for tr in tbl.findall(HP + "tr"):
        for tc in tr.findall(HP + "tc"):
            a, sp, sz = tc.find(HP + "cellAddr"), tc.find(HP + "cellSpan"), tc.find(HP + "cellSz")
            c, r = int(a.get("colAddr", "0")), int(a.get("rowAddr", "0"))
            cs_, rs_ = int(sp.get("colSpan", "1")) if sp is not None else 1, int(sp.get("rowSpan", "1")) if sp is not None else 1
            w, h = (int(sz.get("width", "0")), int(sz.get("height", "0"))) if sz is not None else (0, 0)
            if cs_ == 1:
                colw[c] = max(colw.get(c, 0), w)
            if rs_ == 1:
                rowh[r] = max(rowh.get(r, 0), h)
            sub = tc.find(HP + "subList")
            b = st.bf.get(tc.get("borderFillIDRef"), {"bd": {}, "fill": None})
            mg = tc.find(HP + "cellMargin")
            cells.append({
                "r": r, "c": c, "rs": rs_, "cs": cs_, "w": w, "h": h, "fill": b.get("fill"), "bd": b.get("bd"),
                "va": (sub.get("vertAlign", "TOP") if sub is not None else "TOP"),
                "margin": [int(mg.get(k, "0")) for k in ("left", "right", "top", "bottom")] if mg is not None else None,
                "paras": _paras(pkg, st, sub.findall(HP + "p"), images, True) if sub is not None else [],
            })
    for cell in cells:                                      # 합친 칸만 있는 줄·칸은 합친 너비/높이에서 빼서 채운다
        if cell["cs"] > 1 and all(colw.get(cell["c"] + i, 0) == 0 for i in range(cell["cs"])):
            for i in range(cell["cs"]):
                colw[cell["c"] + i] = cell["w"] // cell["cs"]
        if cell["rs"] > 1 and all(rowh.get(cell["r"] + i, 0) == 0 for i in range(cell["rs"])):
            for i in range(cell["rs"]):
                rowh[cell["r"] + i] = cell["h"] // cell["rs"]
    cells.sort(key=lambda x: (x["r"], x["c"]))
    return {"k": "tbl", "rows": rows, "cols": cols, "colw": [colw.get(i, 0) or 4000 for i in range(cols)],
            "rowh": [rowh.get(i, 0) or 1000 for i in range(rows)], "cells": cells}


def unsupported_parts(pkg):
    """명령으로는 원본과 똑같이 만들 수 없는 부분(도형·글상자, 떠 있는 그림, 바탕쪽, 머리말·꼬리말, 쪽 번호) 목록"""
    out = []
    sec = pkg.section(0)
    shapes = [e for t in ("rect", "ellipse", "container", "line", "polygon", "curve", "arc", "textart") for e in sec.iter(HP + t)]
    if shapes:
        out.append("도형·글상자 %d개" % len(shapes))
    floating = [pc for pc in sec.iter(HP + "pic") if (pc.find(HP + "pos") is not None and pc.find(HP + "pos").get("treatAsChar") != "1")]
    if floating:
        out.append("글자처럼 취급하지 않는(떠 있는) 그림 %d개" % len(floating))
    if any(re.fullmatch(r"Contents/masterpage\d+\.xml", n) for n in pkg.files):
        out.append("바탕쪽")
    if list(sec.iter(HP + "header")) or list(sec.iter(HP + "footer")):
        out.append("머리말·꼬리말")
    if list(sec.iter(HP + "pageNum")) or list(sec.iter(HP + "pageNumCtrl")):
        out.append("쪽 번호")
    return out


def extract(pkg):
    """양식 묶음 → (레시피, {그림이름: 바이트})"""
    st = _Styles(pkg)
    images = {}
    steps = []
    for name in pkg.section_names()[:1]:
        sec = pkg.xml(name)
        steps = _paras(pkg, st, sec.findall(HP + "p"), images, False)
    try:
        page = P.page_setup_of(pkg)
    except Exception:                                       # noqa: BLE001
        page = None
    return {"v": 1, "page": page, "steps": steps, "unsupported": unsupported_parts(pkg)}, images


# ===================================================================== 재생 엔진
_BD_MM = (0.1, 0.12, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.7, 1.0, 1.5, 2.0, 3.0, 4.0, 5.0)


def bd_index(mm):
    """선 굵기(mm) → 한글 굵기 번호(0=0.1mm … 6=0.4mm …)"""
    return min(range(len(_BD_MM)), key=lambda i: abs(_BD_MM[i] - mm))


_VA = {"TOP": 0, "CENTER": 1, "BOTTOM": 2}


class Player:
    """레시피를 한글에 재생. br 는 HwpBridge(또는 같은 메서드를 가진 시험용 가짜).
    fresh=True: 지금 커서가 새(빈) 문단 맨 앞이다(빈 문서나 새 줄에서 시작). 문서 끝에서 이어 만든다."""

    def __init__(self, br, recipe, images=None, fresh=True, first_page=True, progress=None):
        self.br, self.rc, self.images = br, recipe, images or {}
        self.fresh, self.first = fresh, first_page
        self.progress = progress
        self._cs = self._ps = None

    # ---- 글자·문단 모양 (같으면 다시 지정하지 않는다)
    def _char(self, cs):
        key = tuple(sorted((cs or {}).items()))
        if key != self._cs:
            self.br.rp_char(cs or {})
            self._cs = key

    def _para(self, ps):
        key = tuple(sorted((ps or {}).items()))
        if key != self._ps:
            self.br.rp_para(ps or {})
            self._ps = key

    def _begin(self, step):
        """새 문단을 시작: 쪽 나눔 / 문단 나눔(이미 빈 문단 위면 그대로)"""
        pb = step.get("pb") and not self.first
        if pb:
            self.br.run_action("BreakPage")
            self.fresh = True
        elif not self.fresh:
            self.br.run_action("BreakPara")
        self.first = False
        self._ps = None                                    # 새 문단은 모양이 바뀌었을 수 있으니 다시 지정

    def run(self):
        steps = self.rc.get("steps", [])
        for i, s in enumerate(steps):
            if self.progress:
                self.progress(i + 1, len(steps))
            k = s.get("k")
            if k == "p":
                self._text_para(s)
            elif k == "tbl":
                self._table(s)
            elif k == "pic":
                self._pic(s)
        return True

    def _text_para(self, s):
        self._begin(s)
        self._para(s.get("para"))
        for r in s.get("runs", []):
            self._char(r.get("cs"))
            tx = r.get("text", "")
            if tx:
                self.br.insert_text(tx.replace("\n", "\r\n"))
        self.fresh = False

    def _pic(self, s):
        self._begin(s)
        self._para(s.get("para"))
        self._pic_here(s)
        self.fresh = False

    def _pic_here(self, s):
        path = os.path.join(tempfile.gettempdir(), "hwpform_%s" % s["img"])
        with open(path, "wb") as f:
            f.write(self.images.get(s["img"], b""))
        try:
            self.br.rp_picture(path)
        finally:
            try:
                os.remove(path)
            except OSError:
                pass

    def _table(self, s):
        br = self.br
        self._begin(s)
        self._para(s.get("para"))
        br.rp_table(s["rows"], s["cols"], s["colw"], s["rowh"])
        cells = s["cells"]
        for i, c in enumerate(cells):
            if c["rs"] > 1 or c["cs"] > 1:                 # 칸 합치기: 블록을 잡아 합친 뒤 그 칸에 쓴다
                br.rp_merge(c["rs"], c["cs"])
            self._ps = self._cs = None
            first = True
            for p in c["paras"] or [{"k": "p", "runs": [], "para": None}]:
                if not first:
                    br.run_action("BreakPara")
                    self._ps = None
                first = False
                self._para(p.get("para"))
                if p.get("k") == "pic":
                    self._pic_here(p)
                    continue
                for r in p.get("runs", []):
                    self._char(r.get("cs"))
                    if r.get("text"):
                        br.insert_text(r["text"].replace("\n", "\r\n"))
            br.rp_cell_format(c)                           # 칸 배경색·테두리·세로 맞춤
            if i < len(cells) - 1:
                br.run_action("TableRightCell")
        br.rp_table_leave()
        self._ps = self._cs = None
        self.fresh = False
