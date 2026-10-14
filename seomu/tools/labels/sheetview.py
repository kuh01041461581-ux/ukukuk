# -*- coding: utf-8 -*-
"""라벨지 미리보기 — 모든 쪽을 세로로 이어 보여 주는 스크롤 화면 (확대/축소 · 칸 위에 마우스 · 칸 누르기)"""
from __future__ import annotations

from PySide6.QtCore import QPointF, QRectF, QSize, Qt, Signal
from PySide6.QtGui import QColor, QFont, QPainter, QPen
from PySide6.QtWidgets import QFrame, QScrollArea, QToolTip, QVBoxLayout, QWidget

from ...ui import tokens as T
from ...ui.theme import theme
from . import logic as L
from .render import Doc

MARGIN = 26          # 쪽 둘레 여백(px)
CAPTION = 26         # 쪽 위 글자 줄(px)
GAP = 20             # 쪽 사이(px)
CSS_PX_PER_MM = 96 / 25.4


class SheetCanvas(QWidget):
    cell_clicked = Signal(int, int)

    def __init__(self, view: "SheetView"):
        super().__init__()
        self.view = view
        self.doc: Doc | None = None
        self.ppmm = 3.0
        self.hover: tuple[int, int] | None = None
        self.marks: dict[tuple[int, int], QColor] = {}
        self.empty_text = ""
        self.pick_mode = False
        self.tip_fn = None
        self.setMouseTracking(True)

    # ── 크기 ──
    def page_h(self) -> float:
        return L.PAGE_H * self.ppmm

    def page_top(self, i: int) -> float:
        return MARGIN + i * (self.page_h() + CAPTION + GAP) + CAPTION

    def page_left(self) -> float:
        return (self.width() - L.PAGE_W * self.ppmm) / 2

    def content_size(self) -> QSize:
        n = self.doc.page_count() if self.doc else 1
        w = int(L.PAGE_W * self.ppmm + 2 * MARGIN)
        h = int(MARGIN * 2 + n * (self.page_h() + CAPTION) + (n - 1) * GAP)
        return QSize(w, h)

    # ── 그리기 ──
    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        p.setRenderHint(QPainter.TextAntialiasing)
        p.fillRect(e.rect(), theme.q("surface3"))
        if not self.doc:
            return
        pw, ph = L.PAGE_W * self.ppmm, self.page_h()
        x0 = self.page_left()
        total = self.doc.page_count()
        cap_font = QFont()
        cap_font.setPixelSize(12)
        for i in range(total):
            top = self.page_top(i)
            if top + ph < e.rect().top() or top - CAPTION > e.rect().bottom():
                continue
            p.setFont(cap_font)
            p.setPen(theme.q("text3"))
            p.drawText(QRectF(x0, top - CAPTION, pw, CAPTION - 4), Qt.AlignLeft | Qt.AlignBottom, f"{i + 1} / {total} 쪽")
            r = QRectF(x0, top, pw, ph)
            for k in range(5, 0, -1):          # 부드러운 그림자
                c = QColor(0, 0, 0, 7 if not theme.dark else 22)
                p.setPen(Qt.NoPen)
                p.setBrush(c)
                p.drawRoundedRect(r.adjusted(-k, -k + 2, k, k + 3), 3 + k, 3 + k)
            p.setBrush(QColor("#FFFFFF"))
            p.setPen(QPen(QColor(0, 0, 0, 40), 1))
            p.drawRect(r)
            p.save()
            p.translate(x0, top)
            p.scale(self.ppmm, self.ppmm)
            p.setClipRect(QRectF(0, 0, L.PAGE_W, L.PAGE_H))
            hv = self.hover[1] if (self.hover and self.hover[0] == i) else None
            marks = {c: col for (pg, c), col in self.marks.items() if pg == i}
            self.doc.draw_page(p, i, guides=True, hover=hv, marks=marks)
            p.restore()
            if i == 0 and self.empty_text:
                p.setPen(theme.q("text3"))
                f = QFont(cap_font)
                f.setPixelSize(15)
                p.setFont(f)
                p.fillRect(QRectF(x0 + pw * 0.12, top + ph * 0.44, pw * 0.76, 54), QColor(255, 255, 255, 235))
                p.setPen(QColor("#6E7890"))
                p.drawText(QRectF(x0 + pw * 0.12, top + ph * 0.44, pw * 0.76, 54), Qt.AlignCenter | Qt.TextWordWrap, self.empty_text)
        p.end()

    # ── 마우스 ──
    def _hit(self, pos: QPointF):
        if not self.doc:
            return None
        pw, ph = L.PAGE_W * self.ppmm, self.page_h()
        x0 = self.page_left()
        for i in range(self.doc.page_count()):
            top = self.page_top(i)
            if x0 <= pos.x() <= x0 + pw and top <= pos.y() <= top + ph:
                cell = self.doc.geom.cell_at((pos.x() - x0) / self.ppmm, (pos.y() - top) / self.ppmm)
                return (i, cell) if cell is not None else None
        return None

    def mouseMoveEvent(self, e):
        h = self._hit(e.position())
        if h != self.hover:
            self.hover = h
            self.update()
            self.setCursor(Qt.PointingHandCursor if h else Qt.ArrowCursor)
            if h and self.tip_fn:
                t = self.tip_fn(*h)
                if t:
                    QToolTip.showText(e.globalPosition().toPoint(), t, self)
                else:
                    QToolTip.hideText()
            else:
                QToolTip.hideText()

    def leaveEvent(self, e):
        self.hover = None
        self.update()

    def mousePressEvent(self, e):
        if e.button() == Qt.LeftButton:
            h = self._hit(e.position())
            if h:
                self.cell_clicked.emit(*h)


class SheetView(QFrame):
    page_changed = Signal(int, int)       # 현재 쪽(0부터), 전체
    cell_clicked = Signal(int, int)
    zoom_changed = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.zoom = None      # None = 가로 맞춤 · "page" = 한 쪽이 다 보이게 · 숫자 = 배율(1 = 화면 100%)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(1, 1, 1, 1)
        self.scroll = QScrollArea()
        self.scroll.setFrameShape(QFrame.NoFrame)
        self.scroll.setWidgetResizable(False)
        self.scroll.setAlignment(Qt.AlignHCenter | Qt.AlignTop)
        self.canvas = SheetCanvas(self)
        self.scroll.setWidget(self.canvas)
        self.canvas.cell_clicked.connect(self.cell_clicked)
        lay.addWidget(self.scroll)
        self.scroll.verticalScrollBar().valueChanged.connect(lambda _: self._emit_page())
        self._style()
        theme.changed.connect(self._style)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"SheetView {{ background:{c['surface3']}; border:1px solid {c['border']}; border-radius:{T.R_CARD}px; }}"
                           f"QScrollArea {{ background:transparent; }}")
        self.canvas.update()

    # ── 문서 ──
    def set_doc(self, doc: Doc, empty_text: str = ""):
        keep = self.scroll.verticalScrollBar().value()
        self.canvas.doc = doc
        self.canvas.empty_text = empty_text
        self.canvas.marks = {}
        self.canvas.hover = None
        self._relayout()
        self.scroll.verticalScrollBar().setValue(keep)
        self._emit_page()

    def set_marks(self, marks: dict):
        self.canvas.marks = marks
        self.canvas.update()

    # ── 확대 ──
    def current_ppmm(self) -> float:
        if self.zoom is None or self.zoom == "page":
            avail = self.scroll.viewport().width() - 2 * MARGIN
            ppmm = avail / L.PAGE_W
            if self.zoom == "page":
                ppmm = min(ppmm, (self.scroll.viewport().height() - 2 * MARGIN - CAPTION) / L.PAGE_H)
            return max(1.2, min(6.0, ppmm))
        return self.zoom * CSS_PX_PER_MM

    def _relayout(self):
        self.canvas.ppmm = self.current_ppmm()
        sz = self.canvas.content_size()
        sz.setWidth(max(sz.width(), self.scroll.viewport().width()))
        self.canvas.setFixedSize(sz)
        self.canvas.update()
        self.zoom_changed.emit("가로 맞춤" if self.zoom is None else "한 쪽" if self.zoom == "page" else f"{round(self.zoom * 100)}%")

    def set_zoom(self, z):
        # 보고 있던 위치(쪽 안 비율)를 유지
        bar = self.scroll.verticalScrollBar()
        rel = bar.value() / max(1, self.canvas.height())
        self.zoom = z if (z is None or z == "page") else max(0.3, min(3.0, z))
        self._relayout()
        bar.setValue(int(rel * self.canvas.height()))

    def zoom_step(self, factor: float):
        base = self.canvas.ppmm / CSS_PX_PER_MM if (self.zoom is None or self.zoom == "page") else self.zoom
        self.set_zoom(round(base * factor, 2))

    def resizeEvent(self, e):
        super().resizeEvent(e)
        self._relayout()

    # ── 쪽 ──
    def page_count(self) -> int:
        return self.canvas.doc.page_count() if self.canvas.doc else 1

    def current_page(self) -> int:
        v = self.scroll.verticalScrollBar().value() + self.scroll.viewport().height() // 3
        step = self.canvas.page_h() + CAPTION + GAP
        return max(0, min(self.page_count() - 1, int((v - MARGIN) // step)))

    def go_page(self, i: int):
        i = max(0, min(self.page_count() - 1, i))
        self.scroll.verticalScrollBar().setValue(int(self.canvas.page_top(i) - CAPTION - 8))

    def _emit_page(self):
        self.page_changed.emit(self.current_page(), self.page_count())
