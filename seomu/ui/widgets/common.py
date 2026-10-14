# -*- coding: utf-8 -*-
"""공통 위젯이 함께 쓰는 작은 도우미"""
from PySide6.QtCore import Qt
from PySide6.QtGui import QColor
from PySide6.QtWidgets import QFrame, QGraphicsDropShadowEffect, QLabel, QWidget


def label(text: str = "", role: str | None = None, wrap: bool = False, rich: bool = False) -> QLabel:
    lb = QLabel(text)
    if role:
        lb.setProperty("role", role)
    lb.setWordWrap(wrap)
    if rich:
        lb.setTextFormat(Qt.RichText)
        lb.setOpenExternalLinks(True)
    lb.setTextInteractionFlags(Qt.TextSelectableByMouse) if wrap else None
    return lb


def hline() -> QFrame:
    f = QFrame()
    f.setProperty("hline", True)
    f.setFixedHeight(1)
    return f


def repolish(w: QWidget):
    w.style().unpolish(w)
    w.style().polish(w)
    w.update()


def set_prop(w: QWidget, **props):
    for k, v in props.items():
        w.setProperty(k, v)
    repolish(w)


def soft_shadow(w: QWidget, color: str = "rgba(79,93,240,110)", blur: int = 16, dy: int = 5) -> QGraphicsDropShadowEffect:
    eff = QGraphicsDropShadowEffect(w)
    eff.setBlurRadius(blur)
    eff.setOffset(0, dy)
    c = QColor(79, 93, 240, 105)
    eff.setColor(c)
    w.setGraphicsEffect(eff)
    return eff


def fmt_size(n: float) -> str:
    if n >= 1024 * 1024:
        return f"{n / 1024 / 1024:.2f} MB"
    return f"{n / 1024:.1f} KB"


def fmt_won(n: float) -> str:
    return f"{int(round(n)):,}원"


def open_folder(path: str):
    """파일이 있는 폴더를 탐색기로 연다"""
    import os, subprocess, sys
    folder = os.path.dirname(os.path.abspath(path))
    try:
        if sys.platform.startswith("win"):
            subprocess.Popen(["explorer", "/select,", os.path.abspath(path)])
        elif sys.platform == "darwin":
            subprocess.Popen(["open", "-R", path])
        else:
            subprocess.Popen(["xdg-open", folder])
    except Exception:
        pass


def ask_save(parent, default_name: str, filt: str) -> str | None:
    from PySide6.QtWidgets import QFileDialog
    fn, _ = QFileDialog.getSaveFileName(parent, "저장", default_name, filt)
    return fn or None
