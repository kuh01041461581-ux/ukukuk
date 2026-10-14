# -*- coding: utf-8 -*-
"""입력 부품: 끌어다 놓기 · 파일 칩 · 파일 입력 · 필드/폼 · 분할 버튼 · 슬라이더"""
import os
from PySide6.QtCore import QRectF, Qt, Signal
from PySide6.QtGui import QColor, QPainter, QPen
from PySide6.QtWidgets import (QButtonGroup, QCheckBox, QFileDialog, QFrame, QGridLayout, QHBoxLayout, QLabel, QPushButton,
                               QSizePolicy, QSlider, QVBoxLayout, QWidget)
from .. import icons, tokens as T
from ..theme import theme
from .buttons import Button
from .common import fmt_size, label, set_prop


def _exts(accept: list[str]) -> list[str]:
    return [a.lower() for a in accept]


class DropZone(QFrame):
    """파일을 끌어다 놓거나 클릭해서 고른다.  accept: ['.xlsx', '.xls'] …"""
    picked = Signal(list)

    def __init__(self, title: str, sub: str = "", accept: list[str] | None = None, multiple: bool = False,
                 compact: bool = False, kind: str = "파일", parent=None):
        super().__init__(parent)
        self.accept = _exts(accept or [])
        self.multiple = multiple
        self.compact = compact
        self.kind = kind
        self._hover = False
        self._drag = False
        self.setAcceptDrops(True)
        self.setCursor(Qt.PointingHandCursor)
        self.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Fixed)
        if compact:
            lay = QHBoxLayout(self)
            lay.setContentsMargins(16, 12, 16, 12)
            lay.setSpacing(12)
        else:
            lay = QVBoxLayout(self)
            lay.setContentsMargins(20, 26, 20, 26)
            lay.setSpacing(2)
            lay.setAlignment(Qt.AlignCenter)
        self.ic = QLabel()
        sz = 34 if compact else 44
        self.ic.setFixedSize(sz, sz)
        self.ic.setAlignment(Qt.AlignCenter)
        self.t = QLabel(title)
        self.s = QLabel(sub)
        if compact:
            lay.addWidget(self.ic)
            col = QVBoxLayout()
            col.setSpacing(0)
            col.addWidget(self.t)
            col.addWidget(self.s)
            lay.addLayout(col, 1)
        else:
            lay.addWidget(self.ic, 0, Qt.AlignHCenter)
            lay.addSpacing(8)
            lay.addWidget(self.t, 0, Qt.AlignHCenter)
            lay.addWidget(self.s, 0, Qt.AlignHCenter)
        self.s.setVisible(bool(sub))
        self._style()
        theme.changed.connect(self._style)

    def _style(self):
        c = theme.tok
        self.ic.setStyleSheet(f"background: qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['soft1']}, stop:1 {c['soft2']}); "
                              f"border:1px solid {c['primary_border']}; border-radius:{10 if self.compact else 12}px;")
        self.ic.setPixmap(icons.pixmap("upload", c["primary"], 17 if self.compact else 22, 1.9))
        self.t.setStyleSheet(f"font-size:{13.5 if self.compact else 14}px; font-weight:600; color:{c['text']}; background:transparent;")
        self.s.setStyleSheet(f"font-size:12.5px; color:{c['text3']}; background:transparent;")
        self.update()

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(1, 1, -1, -1)
        active = self._hover or self._drag
        p.setBrush(theme.q("soft1") if active else theme.q("surface2"))
        pen = QPen(theme.q("primary") if active else theme.q("border_strong"), 1.5, Qt.DashLine)
        pen.setDashPattern([4, 3])
        p.setPen(pen)
        p.drawRoundedRect(r, T.R_CARD, T.R_CARD)

    def enterEvent(self, e):
        self._hover = True
        self.update()

    def leaveEvent(self, e):
        self._hover = False
        self.update()

    def _ok(self, path: str) -> bool:
        return (not self.accept) or os.path.splitext(path)[1].lower() in self.accept

    def dragEnterEvent(self, e):
        urls = [u.toLocalFile() for u in e.mimeData().urls()]
        if urls and all(self._ok(u) for u in urls):
            self._drag = True
            self.update()
            e.acceptProposedAction()

    def dragLeaveEvent(self, e):
        self._drag = False
        self.update()

    def dropEvent(self, e):
        self._drag = False
        self.update()
        urls = [u.toLocalFile() for u in e.mimeData().urls() if self._ok(u.toLocalFile())]
        if urls:
            self.picked.emit(urls if self.multiple else urls[:1])

    def mouseReleaseEvent(self, e):
        if e.button() == Qt.LeftButton and self.rect().contains(e.position().toPoint()):
            flt = f"{self.kind} ({' '.join('*' + a for a in self.accept)})" if self.accept else "모든 파일 (*)"
            if self.multiple:
                files, _ = QFileDialog.getOpenFileNames(self, "파일 선택", "", flt)
            else:
                f, _ = QFileDialog.getOpenFileName(self, "파일 선택", "", flt)
                files = [f] if f else []
            if files:
                self.picked.emit(files)


class FileChip(QFrame):
    removed = Signal()

    def __init__(self, path: str, parent=None):
        super().__init__(parent)
        lay = QHBoxLayout(self)
        lay.setContentsMargins(12, 10, 12, 10)
        lay.setSpacing(12)
        self.ic = QLabel()
        self.ic.setFixedSize(34, 34)
        self.ic.setAlignment(Qt.AlignCenter)
        lay.addWidget(self.ic)
        self.name = QLabel()
        lay.addWidget(self.name, 1)
        self.x = Button("제거", "flat", "x", "sm")
        self.x.clicked.connect(self.removed)
        lay.addWidget(self.x)
        self.set_path(path)
        self._style()
        theme.changed.connect(self._style)

    def set_path(self, path: str):
        try:
            size = fmt_size(os.path.getsize(path))
        except OSError:
            size = ""
        c = theme.tok
        self._n, self._m = os.path.basename(path), size
        self.name.setText(f'<b>{self._n}</b> &nbsp;<span style="color:{c["text3"]}; font-size:12px">{size}</span>')
        self.name.setToolTip(path)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"FileChip {{ background:{c['surface2']}; border:1px solid {c['border']}; border-radius:{T.R_INPUT}px; }}")
        self.ic.setStyleSheet(f"background:{c['primary_tint']}; border-radius:9px;")
        self.ic.setPixmap(icons.pixmap("file", c["primary"], 18, 1.8))
        self.name.setStyleSheet(f"font-size:13px; color:{c['text']}; background:transparent;")
        self.set_path(self.name.toolTip() or self._n)


class FileInput(QWidget):
    """드롭 영역 + 고른 파일 칩. 단일 파일 입력의 표준 부품."""
    file_changed = Signal(object)   # str | None

    def __init__(self, title: str, sub: str = "", accept: list[str] | None = None, compact: bool = False, kind: str = "파일", parent=None):
        super().__init__(parent)
        self.path: str | None = None
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(10)
        self.drop = DropZone(title, sub, accept, False, compact, kind)
        self.drop.picked.connect(lambda fs: self.set_file(fs[0]))
        lay.addWidget(self.drop)
        self.chip: FileChip | None = None
        self._lay = lay

    def set_file(self, path: str | None):
        if self.chip is not None:
            self.chip.deleteLater()
            self.chip = None
        self.path = path
        if path:
            self.chip = FileChip(path)
            self.chip.removed.connect(lambda: self.set_file(None))
            self._lay.addWidget(self.chip)
        self.file_changed.emit(path)

    def clear(self):
        self.set_file(None)


class Field(QWidget):
    """라벨(위) + 입력 + 도움말(아래)"""

    def __init__(self, text: str, widget: QWidget, help_text: str | None = None, parent=None):
        super().__init__(parent)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(6)
        self.lb = QLabel(text)
        self.lb.setStyleSheet("font-size:12.5px; font-weight:600; background:transparent;")
        lay.addWidget(self.lb)
        lay.addWidget(widget)
        self.widget = widget
        if help_text:
            h = label(help_text, "small", wrap=True)
            lay.addWidget(h)


class FormGrid(QWidget):
    def __init__(self, cols: int = 2, parent=None):
        super().__init__(parent)
        self.cols = cols
        self.g = QGridLayout(self)
        self.g.setContentsMargins(0, 0, 0, 0)
        self.g.setHorizontalSpacing(20)
        self.g.setVerticalSpacing(16)
        for i in range(cols):
            self.g.setColumnStretch(i, 1)
        self._i = 0

    def add(self, w: QWidget, span: int = 1):
        span = min(span, self.cols)
        col = self._i % self.cols
        if col + span > self.cols:
            self._i += self.cols - col
            col = 0
        row = self._i // self.cols
        self.g.addWidget(w, row, col, 1, span)
        self._i += span
        return w


class Segmented(QFrame):
    changed = Signal(object)

    def __init__(self, options: list[tuple], value=None, parent=None):
        super().__init__(parent)
        lay = QHBoxLayout(self)
        lay.setContentsMargins(3, 3, 3, 3)
        lay.setSpacing(2)
        self.group = QButtonGroup(self)
        self.group.setExclusive(True)
        self.buttons: dict = {}
        for i, (val, text) in enumerate(options):
            b = QPushButton(text)
            b.setCheckable(True)
            b.setCursor(Qt.PointingHandCursor)
            b.setFocusPolicy(Qt.NoFocus)
            self.group.addButton(b, i)
            self.buttons[val] = b
            lay.addWidget(b, 1)
            b.clicked.connect(lambda _=False, v=val: self.changed.emit(v))
        first = value if value is not None else options[0][0]
        self.buttons[first].setChecked(True)
        self._style()
        theme.changed.connect(self._style)

    def value(self):
        for v, b in self.buttons.items():
            if b.isChecked():
                return v

    def set_value(self, v):
        self.buttons[v].setChecked(True)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"Segmented {{ background:{c['surface3']}; border:1px solid {c['border']}; border-radius:{T.R_BTN}px; }}"
                           f"QPushButton {{ background:transparent; border:1px solid transparent; border-radius:6px; min-height:30px; padding:0 12px; "
                           f"color:{c['text2']}; font-weight:500; }}"
                           f"QPushButton:hover {{ color:{c['text']}; }}"
                           f"QPushButton:checked {{ background:{c['surface']}; border:1px solid {c['border']}; color:{c['primary']}; font-weight:600; }}")


class RangeField(QWidget):
    changed = Signal(int)

    def __init__(self, lo: int, hi: int, step: int, value: int, suffix: str = "", parent=None):
        super().__init__(parent)
        lay = QHBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(12)
        self.s = QSlider(Qt.Horizontal)
        self.s.setRange(lo, hi)
        self.s.setSingleStep(step)
        self.s.setPageStep(step)
        self.s.setValue(value)
        self.s.setFixedHeight(24)
        self.suffix = suffix
        self.val = QLabel()
        self.val.setMinimumWidth(66)
        self.val.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        lay.addWidget(self.s, 1)
        lay.addWidget(self.val)
        self.step = step
        self.s.valueChanged.connect(self._ch)
        self._ch(value)
        self._style()
        theme.changed.connect(self._style)

    def _ch(self, v):
        v = round(v / self.step) * self.step
        if v != self.s.value():
            self.s.setValue(v)
            return
        self.val.setText(f"{v}{self.suffix}")
        self.changed.emit(v)

    def value(self) -> int:
        return self.s.value()

    def _style(self):
        self.val.setStyleSheet(f"font-weight:700; color:{theme.c('primary')}; background:transparent;")


def check(text: str, sub: str | None = None, checked: bool = False) -> QWidget:
    """체크 + 아래 작은 설명"""
    w = QWidget()
    lay = QVBoxLayout(w)
    lay.setContentsMargins(0, 0, 0, 0)
    lay.setSpacing(2)
    cb = QCheckBox(text)
    cb.setChecked(checked)
    lay.addWidget(cb)
    if sub:
        l = label(sub, "small", wrap=True)
        l.setContentsMargins(26, 0, 0, 0)
        lay.addWidget(l)
    w.cb = cb
    return w
