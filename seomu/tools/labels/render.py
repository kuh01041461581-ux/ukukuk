# -*- coding: utf-8 -*-
"""라벨지 그리기 — 미리보기 · PDF · 인쇄가 모두 같은 함수(draw_page)를 쓴다.

QPainter 의 좌표 단위는 1 = 1mm (호출하는 쪽이 scale 을 걸어 준다).  글자는 큰 기준 크기(EM 픽셀)의 글꼴로
배치한 뒤 크기만큼 줄여서 그리므로, 어떤 장치(화면·PDF·프린터)에서도 줄바꿈 위치가 똑같다."""
from __future__ import annotations

import os

from PySide6.QtCore import QMarginsF, QPointF, QRectF, Qt
from PySide6.QtGui import (QColor, QFont, QFontMetricsF, QPageLayout, QPageSize, QPainter, QPdfWriter, QPen, QPixmap, QTextLayout,
                           QTextOption)

from . import logic as L

EM = 200                     # 글꼴 기준 크기(논리 단위)
PT = 25.4 / 72.0             # pt → mm
_ASSETS = os.path.join(os.path.dirname(__file__), "assets")
FALLBACKS = ["Noto Sans KR", "Malgun Gothic", "맑은 고딕", "NanumGothic", "sans-serif"]
ZIP_RED = "#cc0000"
_wm_cache: dict = {}


def watermark() -> QPixmap:
    if "pm" not in _wm_cache:
        _wm_cache["pm"] = QPixmap(os.path.join(_ASSETS, "character.png"))
    return _wm_cache["pm"]


# ── 글자 배치 ───────────────────────────────────────────────────────
def make_font(family: str, bold=False, italic=False, underline=False, spacing_em: float = 0.0) -> QFont:
    f = QFont()
    f.setFamilies([family] + [x for x in FALLBACKS if x != family])
    f.setPixelSize(EM)
    f.setBold(bold)
    f.setItalic(italic)
    f.setUnderline(underline)
    f.setHintingPreference(QFont.PreferNoHinting)
    if spacing_em:
        f.setLetterSpacing(QFont.AbsoluteSpacing, spacing_em * EM)
    return f


class Block:
    """배치가 끝난 글자 덩어리. width/height/line_count 는 mm"""

    def __init__(self, tl: QTextLayout | None, scale: float, height: float, lines: int, width: float):
        self.tl, self.s, self.height, self.lines, self.width = tl, scale, height, lines, width

    def draw(self, p: QPainter, x: float, y: float, color: str):
        if not self.tl:
            return
        p.save()
        p.translate(x, y)
        p.scale(self.s, self.s)
        p.setPen(QColor(color))
        self.tl.draw(p, QPointF(0, 0))
        p.restore()


def layout_text(text: str, font: QFont, size_mm: float, width_mm: float, line_h: float | None, align: str = "left", wrap: bool = True) -> Block:
    """line_h: 글자 크기 대비 줄 높이 배수 (None = 글꼴 기본). align: left|center|right"""
    if not text or size_mm <= 0:
        return Block(None, 1.0, 0.0, 0, 0.0)
    s = size_mm / EM
    wu = max(width_mm, 0.01) / s
    natural = QFontMetricsF(font).height()
    lh = (line_h * EM) if line_h else natural
    tl = QTextLayout(text.replace("\n", " "), font)
    opt = QTextOption()
    opt.setWrapMode(QTextOption.WrapAnywhere if wrap else QTextOption.NoWrap)
    tl.setTextOption(opt)
    tl.beginLayout()
    y, n, widest = 0.0, 0, 0.0
    while True:
        ln = tl.createLine()
        if not ln.isValid():
            break
        ln.setLineWidth(wu)
        nw = ln.naturalTextWidth()
        widest = max(widest, nw)
        x = 0.0
        if align == "right":
            x = wu - nw
        elif align == "center":
            x = (wu - nw) / 2
        ln.setPosition(QPointF(x, y + (lh - natural) / 2))
        y += lh
        n += 1
    tl.endLayout()
    return Block(tl, s, y * s, n, widest * s)


# ── 문서 (쪽 → 칸 → 그리기) ──────────────────────────────────────────
class Doc:
    geom: L.Geometry
    pages: list

    def page_count(self) -> int:
        return len(self.pages)

    def item_at(self, page: int, cell: int):
        return self.pages[page][cell]

    def skipped(self, page: int, cell: int) -> bool:
        return False

    def draw_cell(self, p: QPainter, rect: tuple, item) -> None:
        raise NotImplementedError

    def draw_page(self, p: QPainter, page: int, guides: bool = False, hover: int | None = None, marks: dict | None = None):
        g = self.geom
        for i in range(g.per_page):
            rect = g.cell_rect(i)
            item = self.item_at(page, i)
            if item is not None:
                self.draw_cell(p, rect, item)
        if guides:
            self._draw_guides(p, page, hover, marks or {})

    def _draw_guides(self, p: QPainter, page: int, hover, marks: dict):
        g = self.geom
        p.save()
        pen = QPen(QColor(55, 53, 47, 80), 0.18, Qt.DashLine)
        for i in range(g.per_page):
            x, y, w, h = g.cell_rect(i)
            r = QRectF(x, y, w, h)
            if self.skipped(page, i):
                p.fillRect(r, QColor(120, 120, 130, 38))
                p.setPen(QPen(QColor(120, 120, 130, 110), 0.2))
                p.drawLine(QPointF(x + 1, y + 1), QPointF(x + w - 1, y + h - 1))
                p.drawLine(QPointF(x + w - 1, y + 1), QPointF(x + 1, y + h - 1))
            col = marks.get(i)
            if col is not None:
                p.fillRect(r, col)
            if hover == i:
                p.fillRect(r, QColor(79, 93, 240, 28))
            p.setPen(QPen(QColor(79, 93, 240, 230), 0.35) if hover == i else pen)
            p.setBrush(Qt.NoBrush)
            p.drawRect(r)
        p.restore()


class AddressDoc(Doc):
    def __init__(self, spec: L.AddressSpec, recipients: list[L.Recipient], style: L.AddressStyle, start: int = 0, dy: float = 0.0, dx: float = 0.0):
        self.spec, self.recipients, self.style = spec, recipients, style
        self.start = max(0, min(start, spec.per_page - 1))
        self.geom = spec.geometry(dy, dx)
        idx = L.paginate(len(recipients), spec.per_page, self.start)
        self.index = idx
        self.pages = [[recipients[k] if k is not None else None for k in pg] for pg in idx]

    def skipped(self, page: int, cell: int) -> bool:
        return page == 0 and cell < self.start

    def recipient_index(self, page: int, cell: int) -> int | None:
        return self.index[page][cell]

    # 한 장 그리기 ---------------------------------------------------
    def draw_cell(self, p: QPainter, rect: tuple, rec: L.Recipient):
        sp, st = self.spec, self.style
        x, y, w, h = rect
        p.save()
        p.setClipRect(QRectF(x, y, w, h))
        if st.watermark and not watermark().isNull():
            size = w * 0.22
            p.setOpacity(max(0.0, min(1.0, st.wm_opacity)))
            p.drawPixmap(QRectF(x + w - 2.0 - size, y + h - 1.5 - size, size, size), watermark(), QRectF(watermark().rect()))
            p.setOpacity(1.0)
        pt_, pr_, pb_, pl_ = sp.pad
        ix, iy, iw, ih = x + pl_, y + pt_, w - pl_ - pr_, h - pt_ - pb_
        m = st.mult
        a_align = "left" if st.align == "default" else st.align
        n_align = "right" if st.align == "default" else st.align
        fam = st.family
        a_font = make_font(fam, st.bold, st.italic, st.underline)
        n_font = make_font(fam, st.bold, st.italic, st.underline, 0.06)
        d_font = make_font(fam, st.bold)
        name = L.honorific(rec.name) if st.honorific else rec.name.strip()
        addr = rec.addr.strip().replace("\n", " ")
        zip_h = sp.digit_h_px * L.PX + 1.5 * L.PX if st.show_zip else 0.0
        f = 1.0
        while True:
            a = layout_text(addr, a_font, sp.addr_px * m * L.PX * f, iw, 1.35, a_align)
            n = layout_text(name, n_font, sp.name_px * m * L.PX, iw, None, n_align)
            heights = [a.height, n.height] + ([zip_h] if st.show_zip else [])
            if not st.autoshrink or sum(heights) <= ih + 0.01 or f <= 0.5:
                break
            f -= 0.05
        ys = L.space_between(heights, ih)
        ac = st.color or "#222222"
        nc = st.color or "#000000"
        a.draw(p, ix, iy + ys[0], ac)
        n.draw(p, ix, iy + ys[1], nc)
        if st.show_zip:
            dw, dh, gap = sp.digit_w_px * L.PX, sp.digit_h_px * L.PX, 2 * L.PX
            total = 5 * dw + 4 * gap
            bx = ix + iw - total
            by = iy + ys[2]
            bw = L.PX
            digits = (rec.zip or "").ljust(5)[:5]
            for k in range(5):
                rx = bx + k * (dw + gap)
                p.setPen(QPen(QColor(ZIP_RED), bw))
                p.setBrush(Qt.NoBrush)
                p.drawRect(QRectF(rx + bw / 2, by + bw / 2, dw - bw, dh - bw))
                ch = digits[k]
                if ch.strip():
                    blk = layout_text(ch, d_font, sp.digit_fs_px * m * L.PX, dw, None, "center")
                    blk.draw(p, rx, by + (dh - blk.height) / 2, st.color or "#000000")
        p.restore()


class GeneralDoc(Doc):
    PAD = 2.0

    def __init__(self, spec: L.GeneralSpec, texts: list[str], style: L.GeneralStyle, start: int = 0, dy: float = 0.0, dx: float = 0.0):
        self.spec, self.texts, self.style = spec, texts, style
        self.start = max(0, min(start, spec.per_page - 1))
        self.geom = spec.geometry(dy, dx)
        idx = L.paginate(len(texts), spec.per_page, self.start)
        self.index = idx
        self.pages = [[texts[k] if k is not None else None for k in pg] for pg in idx]

    def skipped(self, page: int, cell: int) -> bool:
        return page == 0 and cell < self.start

    def entry_index(self, page: int, cell: int) -> int | None:
        return self.index[page][cell]

    def draw_cell(self, p: QPainter, rect: tuple, text: str):
        st = self.style
        x, y, w, h = rect
        pad = min(self.PAD, 0.1 * min(w, h))
        ix, iy, iw, ih = x + pad, y + pad, w - 2 * pad, h - 2 * pad
        if not text.strip():
            return
        if st.vertical:
            text = "\n".join(L.vertical_lines(text))
            ah = "center"
        else:
            ah = st.align_h
        size = st.size_pt
        while True:
            font = make_font(st.family, st.bold, st.italic, st.underline, (st.letter_spacing / size) if size else 0)
            blk = layout_text(text, font, size * PT, iw, st.line_height, ah)
            if not st.autofit or blk.height <= ih + 0.01 or size <= 4:
                break
            size = max(4.0, size - 0.5)
        oy = {"top": 0.0, "middle": 0.5, "bottom": 1.0}.get(st.align_v, 0.5) * (ih - blk.height)
        p.save()
        p.setClipRect(QRectF(x, y, w, h))
        blk.draw(p, ix, iy + max(0.0, oy) if blk.height <= ih else iy, st.color or "#000000")
        p.restore()


class GridTestDoc(Doc):
    """정렬 확인용 — 글자 없이 칸 테두리만 (일반 A4 에 뽑아 라벨지에 겹쳐 본다)"""

    def __init__(self, geom: L.Geometry):
        self.geom = geom
        self.pages = [[i + 1 for i in range(geom.per_page)]]

    def draw_cell(self, p: QPainter, rect: tuple, n: int):
        x, y, w, h = rect
        p.save()
        p.setPen(QPen(QColor(0, 0, 0), 0.2))
        p.setBrush(Qt.NoBrush)
        p.drawRect(QRectF(x, y, w, h))
        p.drawLine(QPointF(x + w / 2 - 2, y + h / 2), QPointF(x + w / 2 + 2, y + h / 2))
        p.drawLine(QPointF(x + w / 2, y + h / 2 - 2), QPointF(x + w / 2, y + h / 2 + 2))
        blk = layout_text(str(n), make_font("Noto Sans KR"), 2.6, w, None, "left")
        blk.draw(p, x + 1.2, y + 0.8, "#666666")
        p.restore()


# ── 출력 ─────────────────────────────────────────────────────────────
def paint_all(p: QPainter, doc: Doc, dpi: float, new_page) -> None:
    """모든 쪽을 장치에 그린다. new_page(): 다음 쪽으로 넘기는 함수"""
    for pg in range(doc.page_count()):
        if pg:
            new_page()
        p.save()
        p.scale(dpi / 25.4, dpi / 25.4)
        doc.draw_page(p, pg, guides=False)
        p.restore()


def export_pdf(doc: Doc, path: str, title: str = "라벨") -> None:
    w = QPdfWriter(path)
    w.setPageSize(QPageSize(QPageSize.A4))
    w.setPageMargins(QMarginsF(0, 0, 0, 0), QPageLayout.Millimeter)
    w.setResolution(600)
    w.setTitle(title)
    w.setCreator("서무 업무 자동화")
    p = QPainter(w)
    try:
        paint_all(p, doc, 600, w.newPage)
    finally:
        p.end()


def print_doc(doc: Doc, printer) -> None:
    """QPrinter 로 보낸다 (용지 A4 · 여백 0 · 장치 해상도 그대로)"""
    printer.setPageSize(QPageSize(QPageSize.A4))
    printer.setFullPage(True)
    printer.setPageMargins(QMarginsF(0, 0, 0, 0), QPageLayout.Millimeter)
    p = QPainter(printer)
    try:
        paint_all(p, doc, printer.resolution(), printer.newPage)
    finally:
        p.end()
