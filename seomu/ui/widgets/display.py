# -*- coding: utf-8 -*-
"""보여 주는 부품: 태그 · 알림 · 요약 타일 · 내려받기 줄 · 진행 · 기록 · 접이식"""
from PySide6.QtCore import QRectF, Qt, Signal
from PySide6.QtGui import QColor, QLinearGradient, QPainter, QPen
from PySide6.QtWidgets import (QFrame, QHBoxLayout, QLabel, QProgressBar, QPushButton, QSizePolicy,
                               QTextEdit, QVBoxLayout, QWidget)
from .. import icons, tokens as T
from ..theme import theme
from .buttons import Button
from .common import label, repolish


class Tag(QLabel):
    """작은 알약. kind: neutral · pri · ok · warn · err"""

    def __init__(self, text: str, kind: str = "neutral", parent=None):
        super().__init__(text, parent)
        self.kind = kind
        self.setAlignment(Qt.AlignCenter)
        self.setFixedHeight(20)
        self.setContentsMargins(9, 0, 9, 0)
        self._style()
        theme.changed.connect(self._style)

    def set_kind(self, kind: str, text: str | None = None):
        self.kind = kind
        if text is not None:
            self.setText(text)
        self._style()

    def _style(self):
        c = theme.tok
        bg, fg = {"neutral": (c["surface3"], c["text2"]), "pri": (c["primary_tint"], c["primary"]),
                  "ok": (c["success_tint"], c["success"]), "warn": (c["warning_tint"], c["warning"]),
                  "err": (c["danger_tint"], c["danger"])}[self.kind]
        self.setStyleSheet(f"background:{bg}; color:{fg}; border-radius:10px; font-size:11px; font-weight:600;")


class Note(QFrame):
    """안내 상자. kind: info · ok · warn · err · plain"""

    def __init__(self, text: str = "", kind: str = "info", parent=None):
        super().__init__(parent)
        self.kind = kind
        lay = QHBoxLayout(self)
        lay.setContentsMargins(14, 11, 14, 11)
        lay.setSpacing(10)
        self.ic = QLabel()
        self.ic.setFixedSize(18, 18)
        lay.addWidget(self.ic, 0, Qt.AlignTop)
        self.text = QLabel(text)
        self.text.setWordWrap(True)
        self.text.setTextFormat(Qt.RichText)
        self.text.setOpenExternalLinks(True)
        self.text.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Preferred)
        lay.addWidget(self.text, 1)
        self._style()
        theme.changed.connect(self._style)

    def set_text(self, text: str, kind: str | None = None):
        self.text.setText(text)
        if kind:
            self.kind = kind
            self._style()

    def _style(self):
        c = theme.tok
        bg, bd, fg, ic = {
            "info": (c["primary_tint"], c["primary_border"], c["text2"], c["primary"]),
            "ok": (c["success_tint"], "transparent", c["success"], c["success"]),
            "warn": (c["warning_tint"], "transparent", c["warning"], c["warning"]),
            "err": (c["danger_tint"], "transparent", c["danger"], c["danger"]),
            "plain": (c["surface2"], c["border"], c["text2"], c["text3"]),
        }[self.kind]
        self.setStyleSheet(f"Note {{ background:{bg}; border:1px solid {bd}; border-radius:{T.R_INPUT}px; }}")
        self.text.setStyleSheet(f"color:{fg}; font-size:13px; background:transparent;")
        name = {"ok": "check", "warn": "warn", "err": "warn"}.get(self.kind, "info")
        self.ic.setPixmap(icons.pixmap(name, ic, 16, 2.0))


class Stat(QFrame):
    def __init__(self, label_text: str, value: str = "-", highlight: bool = False, parent=None):
        super().__init__(parent)
        self.hl = highlight
        lay = QVBoxLayout(self)
        lay.setContentsMargins(16, 13, 16, 14)
        lay.setSpacing(2)
        self.l = QLabel(label_text)
        self.v = QLabel(value)
        lay.addWidget(self.l)
        lay.addWidget(self.v)
        theme.changed.connect(self._style)
        self._style()

    def set_value(self, v: str):
        self.v.setText(v)

    def _style(self):
        c = theme.tok
        if self.hl:
            self.setStyleSheet(f"Stat {{ background: qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['g1']}, stop:1 {c['g2']}); border-radius:{T.R_INPUT}px; border:0; }}")
            lc, vc = "rgba(255,255,255,0.85)", "#FFFFFF"
        else:
            self.setStyleSheet(f"Stat {{ background: qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['soft1']}, stop:1 {c['soft2']}); "
                               f"border:1px solid {c['primary_border']}; border-radius:{T.R_INPUT}px; }}")
            lc, vc = c["text2"], c["primary"]
        self.l.setStyleSheet(f"color:{lc}; font-size:12px; font-weight:500; background:transparent;")
        self.v.setStyleSheet(f"color:{vc}; font-size:21px; font-weight:700; background:transparent;")


class StatRow(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.lay = QHBoxLayout(self)
        self.lay.setContentsMargins(0, 0, 0, 0)
        self.lay.setSpacing(12)
        self.stats: list[Stat] = []

    def set_items(self, items: list[tuple]):
        """[(라벨, 값, 강조?), …]"""
        while self.lay.count():
            w = self.lay.takeAt(0).widget()
            if w:
                w.deleteLater()
        self.stats = []
        for it in items:
            s = Stat(it[0], it[1], it[2] if len(it) > 2 else False)
            self.lay.addWidget(s, 1)
            self.stats.append(s)


class DownloadBar(QFrame):
    clicked = Signal()

    def __init__(self, button_text: str = "내려받기", parent=None):
        super().__init__(parent)
        lay = QHBoxLayout(self)
        lay.setContentsMargins(16, 14, 16, 14)
        lay.setSpacing(14)
        self.ic = QLabel()
        self.ic.setFixedSize(40, 40)
        self.ic.setAlignment(Qt.AlignCenter)
        lay.addWidget(self.ic)
        col = QVBoxLayout()
        col.setSpacing(1)
        self.name = QLabel("-")
        self.sub = QLabel("")
        col.addWidget(self.name)
        col.addWidget(self.sub)
        lay.addLayout(col, 1)
        self.btn = Button(button_text, "primary", "down")
        self.btn.clicked.connect(self.clicked)
        lay.addWidget(self.btn)
        self._style()
        theme.changed.connect(self._style)

    def set_info(self, name: str, sub: str):
        self.name.setText(name)
        self.sub.setText(sub)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"DownloadBar {{ background:{c['success_tint']}; border:1px solid {c['success']}33; border-radius:{T.R_CARD}px; }}")
        self.ic.setStyleSheet(f"background: qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['success_g1']}, stop:1 {c['success_g2']}); border-radius:11px;")
        self.ic.setPixmap(icons.pixmap("down", "#FFFFFF", 20, 2.0))
        self.name.setStyleSheet(f"font-size:14px; font-weight:700; color:{c['text']}; background:transparent;")
        self.sub.setStyleSheet(f"font-size:12.5px; color:{c['text2']}; background:transparent;")


class Empty(QFrame):
    def __init__(self, text: str, parent=None):
        super().__init__(parent)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(16, 28, 16, 28)
        self.lb = QLabel(text)
        self.lb.setAlignment(Qt.AlignCenter)
        self.lb.setWordWrap(True)
        lay.addWidget(self.lb)
        self._style()
        theme.changed.connect(self._style)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"Empty {{ background:{c['surface2']}; border:1.5px dashed {c['border']}; border-radius:{T.R_CARD}px; }}")
        self.lb.setStyleSheet(f"color:{c['text3']}; font-size:13px; background:transparent;")


class ProgressBlock(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(7)
        self.bar = QProgressBar()
        self.bar.setRange(0, 100)
        self.bar.setTextVisible(False)
        lay.addWidget(self.bar)
        row = QHBoxLayout()
        self.pct = label("0%", "small")
        self.meta = label("", "small")
        row.addWidget(self.pct)
        row.addStretch(1)
        row.addWidget(self.meta)
        lay.addLayout(row)

    def set(self, value: int, meta: str = ""):
        self.bar.setValue(value)
        self.pct.setText(f"{value}%")
        self.meta.setText(meta)


class LogView(QTextEdit):
    """진행 기록. add(text, kind) — kind: '' · ok · warn · err"""

    def __init__(self, parent=None, max_height: int = 220):
        super().__init__(parent)
        self.setReadOnly(True)
        self.setMaximumHeight(max_height)
        self.setMinimumHeight(90)
        self._style()
        theme.changed.connect(self._style)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"QTextEdit {{ background:{c['surface2']}; border:1px solid {c['border']}; border-radius:{T.R_INPUT}px; "
                           f"padding:10px 14px; font-family:Consolas,'Cascadia Mono',monospace; font-size:12px; color:{c['text2']}; }}")

    def add(self, text: str, kind: str = ""):
        c = theme.tok
        col = {"ok": c["success"], "warn": c["warning"], "err": c["danger"]}.get(kind, c["text2"])
        dot = {"ok": c["success"], "warn": c["warning"], "err": c["danger"]}.get(kind, c["text3"])
        esc = text.replace("&", "&amp;").replace("<", "&lt;")
        self.append(f'<div style="color:{col}; margin:1px 0;"><span style="color:{dot}">●</span>&nbsp; {esc}</div>')
        self.verticalScrollBar().setValue(self.verticalScrollBar().maximum())


class Accordion(QFrame):
    """접이식 상자 (선택 항목·고급 설정용)"""

    def __init__(self, title: str, tag: str | None = None, parent=None, open_: bool = False):
        super().__init__(parent)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(0)
        self.head = QPushButton()
        self.head.setCursor(Qt.PointingHandCursor)
        self.head.setFlat(True)
        hl = QHBoxLayout(self.head)
        hl.setContentsMargins(14, 0, 14, 0)
        hl.setSpacing(8)
        self.chev = QLabel()
        self.chev.setFixedSize(14, 14)
        hl.addWidget(self.chev)
        self.title = QLabel(title)
        hl.addWidget(self.title)
        hl.addStretch(1)
        self.tag = Tag(tag) if tag else None
        if self.tag:
            hl.addWidget(self.tag)
        self.head.setFixedHeight(44)
        self.head.clicked.connect(self.toggle)
        lay.addWidget(self.head)
        self.content = QWidget()
        self.body = QVBoxLayout(self.content)
        self.body.setContentsMargins(14, 2, 14, 14)
        self.body.setSpacing(12)
        lay.addWidget(self.content)
        self.set_open(open_)
        self._style()
        theme.changed.connect(self._style)

    def set_open(self, on: bool):
        self._open = on
        self.content.setVisible(on)
        self._style()

    def toggle(self):
        self.set_open(not self._open)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"Accordion {{ background:{c['surface2']}; border:1px solid {c['border']}; border-radius:{T.R_INPUT}px; }}")
        self.head.setStyleSheet("QPushButton { background:transparent; border:0; text-align:left; min-height:0; padding:0; }")
        self.title.setStyleSheet(f"font-weight:600; font-size:13px; color:{c['text']}; background:transparent;")
        self.chev.setPixmap(icons.pixmap("chev_down" if self._open else "chev_right", c["text3"], 14, 2.2))
