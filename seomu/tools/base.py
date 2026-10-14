# -*- coding: utf-8 -*-
"""모든 도구 화면의 공통 뼈대 — 머리글(아이콘·제목·설명·진행 순서) + 단계 카드를 세로로 쌓는다"""
from PySide6.QtCore import Qt
from PySide6.QtGui import QColor, QLinearGradient, QPainter
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QScrollArea, QVBoxLayout, QWidget
from ..ui import icons
from ..ui.theme import theme
from ..ui.widgets import StepCard, label

CONTENT_MAX = 1180


class IconTile(QLabel):
    def __init__(self, name: str, size: int = 46, parent=None):
        super().__init__(parent)
        self.name = name
        self.setFixedSize(size, size)
        self.setAlignment(Qt.AlignCenter)
        self._size = size
        theme.changed.connect(self.update)

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        g = QLinearGradient(0, 0, self.width(), self.height())
        g.setColorAt(0, QColor(theme.c("g1")))
        g.setColorAt(1, QColor(theme.c("g2")))
        p.setPen(Qt.NoPen)
        p.setBrush(g)
        p.drawRoundedRect(self.rect(), self._size * 0.28, self._size * 0.28)
        pm = icons.pixmap(self.name, "#FFFFFF", int(self._size * 0.5), 1.9)
        s = int(self._size * 0.5)
        p.drawPixmap((self.width() - s) // 2, (self.height() - s) // 2, pm)


class StepChips(QWidget):
    def __init__(self, steps: list[str], parent=None):
        super().__init__(parent)
        lay = QHBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(6)
        for i, s in enumerate(steps):
            if i:
                a = QLabel("›")
                a.setStyleSheet(f"color:{theme.c('text3')}; background:transparent;")
                lay.addWidget(a)
            lay.addWidget(self._chip(i + 1, s))
        lay.addStretch(1)

    def _chip(self, n: int, text: str) -> QWidget:
        w = QFrame()
        w.setObjectName("chip")
        l = QHBoxLayout(w)
        l.setContentsMargins(5, 4, 12, 4)
        l.setSpacing(7)
        b = QLabel(str(n))
        b.setFixedSize(18, 18)
        b.setAlignment(Qt.AlignCenter)
        c = theme.tok
        b.setStyleSheet(f"background:{c['soft1']}; color:{c['primary']}; border:1px solid {c['primary_border']}; border-radius:9px; font-size:11px; font-weight:700;")
        t = QLabel(text)
        t.setStyleSheet(f"color:{c['text2']}; font-size:12.5px; font-weight:500; background:transparent;")
        l.addWidget(b)
        l.addWidget(t)
        w.setStyleSheet(f"QFrame#chip {{ background:{c['surface']}; border:1px solid {c['border']}; border-radius:14px; }}")
        return w


class ToolPage(QScrollArea):
    """info: registry.ToolInfo   ctx: shell.AppContext"""

    def __init__(self, info, ctx, parent=None):
        super().__init__(parent)
        self.info = info
        self.ctx = ctx
        self.setWidgetResizable(True)
        self.setFrameShape(QFrame.NoFrame)
        self.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        outer = QWidget()
        self.setWidget(outer)
        ol = QHBoxLayout(outer)
        ol.setContentsMargins(29, 20, 29, 34)
        ol.addStretch(1)
        self.inner = QWidget()
        self.inner.setMaximumWidth(CONTENT_MAX)
        self.inner.setMinimumWidth(640)
        ol.addWidget(self.inner, 100)
        ol.addStretch(1)
        self.v = QVBoxLayout(self.inner)
        self.v.setContentsMargins(0, 0, 0, 0)
        self.v.setSpacing(16)
        if not info.fill:
            self.v.addLayout(self._header())
            self.v.addSpacing(6)
        self.build()
        self.v.addStretch(1)

    # ---- 머리글 -----------------------------------------------------
    def _header(self):
        i = self.info
        h = QHBoxLayout()
        h.setSpacing(16)
        h.addWidget(IconTile(i.icon), 0, Qt.AlignTop)
        col = QVBoxLayout()
        col.setSpacing(2)
        col.addWidget(label(i.group, "eyebrow"))
        col.addWidget(label(i.title, "title"))
        d = label(i.desc, "desc", wrap=True)
        d.setStyleSheet(f"font-size:14px; color:{theme.c('text2')}; background:transparent;")
        d.setMaximumWidth(780)
        col.addWidget(d)
        if i.steps:
            col.addSpacing(8)
            col.addWidget(StepChips(i.steps))
        h.addLayout(col, 1)
        return h

    # ---- 하위 클래스가 채움 ---------------------------------------------
    def build(self):
        raise NotImplementedError

    def card(self, number, title, desc=None, tag=None) -> StepCard:
        c = StepCard(number, title, desc, tag)
        self.v.addWidget(c)
        return c

    def toast(self, text: str):
        self.ctx.toast(text)

    def on_show(self):
        """화면이 보일 때마다 불림 (필요하면 재정의)"""
