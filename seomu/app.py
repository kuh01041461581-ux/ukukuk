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


def selftest() -> int:
    """exe 안에 글꼴·모델·모든 화면이 들어 있는지 점검 (CI 에서 실행)"""
    os.environ.setdefault("SEOMU_HOME", os.path.join(os.environ.get("TEMP", "/tmp"), "seomu_selftest"))
    app = make_app()
    from .registry import TOOLS
    problems = []
    for f in ("NotoSansKR-Regular.ttf", "NotoSansKR-Bold.ttf"):
        if not (ASSETS / "fonts" / f).exists():
            problems.append("글꼴 없음: " + f)
    if not (ASSETS / "models" / "face_detection_yunet_2023mar.onnx").exists():
        problems.append("얼굴 인식 모델 없음")
    w = make_window()
    for t in TOOLS:
        try:
            w.show_page(t.id)
        except Exception as e:  # noqa
            problems.append(f"{t.id}: {type(e).__name__}: {e}")
    for p in problems:
        print("FAIL", p)
    print("OK" if not problems else "FAILED", len(TOOLS), "tools")
    return 1 if problems else 0


def main():
    if "--selftest" in sys.argv:
        sys.exit(selftest())
    app = make_app()
    ico = ASSETS / "icon.png"
    if ico.exists():
        app.setWindowIcon(QIcon(str(ico)))
    w = make_window()
    w.show()
    sys.exit(app.exec())
