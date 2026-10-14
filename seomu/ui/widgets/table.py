# -*- coding: utf-8 -*-
"""표 — 모든 도구가 같은 모양의 표를 쓴다"""
from PySide6.QtCore import QRectF, Qt, Signal
from PySide6.QtGui import QBrush, QColor, QPainter, QPainterPath, QPen, QRegion
from PySide6.QtWidgets import (QAbstractItemView, QFrame, QHeaderView, QTableWidget, QTableWidgetItem, QVBoxLayout, QWidget)
from .. import tokens as T
from ..theme import theme

AL = {"l": Qt.AlignLeft | Qt.AlignVCenter, "c": Qt.AlignCenter, "r": Qt.AlignRight | Qt.AlignVCenter}


class DataTable(QFrame):
    """columns: [(제목, 너비(px)|None=늘어남, 정렬 'l'|'c'|'r', 편집가능 bool)]"""
    cell_edited = Signal(int, int, str)

    def __init__(self, columns: list[tuple], row_height: int = 44, max_rows: int = 10, parent=None):
        super().__init__(parent)
        self.columns = columns
        self.row_h = row_height
        self.max_rows = max_rows
        lay = QVBoxLayout(self)
        lay.setContentsMargins(1, 1, 1, 1)
        lay.setSpacing(0)
        self.table = QTableWidget(0, len(columns))
        self.table.setHorizontalHeaderLabels([c[0] for c in columns])
        self.table.verticalHeader().hide()
        self.table.verticalHeader().setDefaultSectionSize(row_height)
        self.table.horizontalHeader().setHighlightSections(False)
        self.table.horizontalHeader().setDefaultAlignment(Qt.AlignLeft | Qt.AlignVCenter)
        self.table.setShowGrid(False)
        self.table.setFrameShape(QFrame.NoFrame)
        self.table.setSelectionMode(QAbstractItemView.NoSelection)
        self.table.setFocusPolicy(Qt.NoFocus)
        self.table.setEditTriggers(QAbstractItemView.DoubleClicked | QAbstractItemView.EditKeyPressed | QAbstractItemView.AnyKeyPressed)
        self.table.setVerticalScrollMode(QAbstractItemView.ScrollPerPixel)
        self.table.setHorizontalScrollMode(QAbstractItemView.ScrollPerPixel)
        self.table.setHorizontalScrollBarPolicy(Qt.ScrollBarAsNeeded)
        h = self.table.horizontalHeader()
        for i, col in enumerate(columns):
            w = col[1]
            if w is None:
                h.setSectionResizeMode(i, QHeaderView.Stretch)
            else:
                h.setSectionResizeMode(i, QHeaderView.Fixed)
                self.table.setColumnWidth(i, w)
        lay.addWidget(self.table)
        self.table.itemChanged.connect(self._item_changed)
        self._loading = False
        self._style()
        theme.changed.connect(self._style)
        self._fit()

    # ---- 데이터 -------------------------------------------------
    def clear(self):
        self.table.setRowCount(0)
        self._fit()

    def set_rows(self, rows: list[list]):
        self._loading = True
        self.table.setRowCount(0)
        for r in rows:
            self._add(r)
        self._loading = False
        self._fit()

    def add_row(self, values: list):
        self._loading = True
        self._add(values)
        self._loading = False
        self._fit()
        return self.table.rowCount() - 1

    def _add(self, values: list):
        r = self.table.rowCount()
        self.table.insertRow(r)
        for c, v in enumerate(values):
            if isinstance(v, QWidget):
                self.table.setCellWidget(r, c, v)
                continue
            it = QTableWidgetItem("" if v is None else str(v))
            it.setTextAlignment(AL[self.columns[c][2] if len(self.columns[c]) > 2 else "l"])
            if not (len(self.columns[c]) > 3 and self.columns[c][3]):
                it.setFlags(it.flags() & ~Qt.ItemIsEditable)
            self.table.setItem(r, c, it)

    def set_cell(self, r: int, c: int, text):
        self._loading = True
        it = self.table.item(r, c)
        if it:
            it.setText(str(text))
        self._loading = False

    def text(self, r: int, c: int) -> str:
        it = self.table.item(r, c)
        return it.text() if it else ""

    def row_count(self) -> int:
        return self.table.rowCount()

    def _item_changed(self, it):
        if not self._loading:
            self.cell_edited.emit(it.row(), it.column(), it.text())

    # ---- 크기 ---------------------------------------------------
    def _fit(self):
        n = self.table.rowCount()
        visible = min(max(n, 1), self.max_rows)
        self.table.setVerticalScrollBarPolicy(Qt.ScrollBarAlwaysOff if n <= self.max_rows else Qt.ScrollBarAsNeeded)
        self.setFixedHeight(38 + visible * self.row_h + 6)

    def _style(self):
        c = theme.tok
        self.setStyleSheet(f"DataTable {{ background:{c['surface']}; border:1px solid {c['border']}; border-radius:{T.R_INPUT}px; }}")
        self.update()

    def resizeEvent(self, e):
        super().resizeEvent(e)
        path = QPainterPath()
        path.addRoundedRect(QRectF(self.rect()), T.R_INPUT, T.R_INPUT)
        self.setMask(QRegion(path.toFillPolygon().toPolygon()))


# ── 표 안에 넣는 작은 부품 ───────────────────────────────────────
from PySide6.QtWidgets import QCheckBox, QHBoxLayout, QLineEdit, QLabel  # noqa: E402


def cell_edit(text: str = "", placeholder: str = "", on_change=None, align=None) -> QLineEdit:
    e = QLineEdit(text)
    e.setProperty("cell", True)
    e.setPlaceholderText(placeholder)
    if align:
        e.setAlignment(align)
    if on_change:
        e.textChanged.connect(on_change)
    return e


def cell_center(w: QWidget) -> QWidget:
    box = QWidget()
    lay = QHBoxLayout(box)
    lay.setContentsMargins(0, 0, 0, 0)
    lay.setAlignment(Qt.AlignCenter)
    lay.addWidget(w)
    return box


def cell_check(checked: bool, on_change=None) -> QWidget:
    cb = QCheckBox()
    cb.setChecked(checked)
    if on_change:
        cb.toggled.connect(on_change)
    box = cell_center(cb)
    box.cb = cb
    return box


def cell_text(text: str, align: str = "l", bold: bool = False, small: bool = False, color: str | None = None) -> QLabel:
    lb = QLabel(text)
    lb.setAlignment(AL[align])
    st = "background:transparent; padding:0 10px;"
    if bold:
        st += " font-weight:700;"
    if small:
        st += " font-size:12px;"
    st += f" color:{color or theme.c('text')};"
    lb.setStyleSheet(st)
    return lb
