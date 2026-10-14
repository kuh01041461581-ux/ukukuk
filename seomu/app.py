# -*- coding: utf-8 -*-
"""서무 업무 자동화 — 시작점"""
import os
import pathlib
import sys

from PySide6.QtGui import QFont, QFontDatabase, QIcon
from PySide6.QtWidgets import QApplication

from .core.settings import Settings
from .ui.shell import MainWindow

VERSION = os.environ.get("SEOMU_VERSION", "dev")
ASSETS = pathlib.Path(getattr(sys, "_MEIPASS", pathlib.Path(__file__).parent)) / ("seomu/assets" if hasattr(sys, "_MEIPASS") else "assets")


def load_fonts():
    for n in ("Regular", "Medium", "Bold"):
        QFontDatabase.addApplicationFont(str(ASSETS / "fonts" / f"NotoSansKR-{n}.ttf"))


def make_app(argv=None) -> QApplication:
    app = QApplication.instance() or QApplication(argv or sys.argv)
    load_fonts()
    f = QFont("Noto Sans KR")
    f.setPixelSize(14)
    app.setFont(f)
    app.setApplicationName("서무 업무 자동화")
    return app


def make_window(settings: Settings | None = None) -> MainWindow:
    s = settings or Settings()
    s.set("__version", VERSION, save=False)
    w = MainWindow(s)
    return w


def main():
    app = make_app()
    ico = ASSETS / "icon.png"
    if ico.exists():
        app.setWindowIcon(QIcon(str(ico)))
    w = make_window()
    w.show()
    sys.exit(app.exec())
