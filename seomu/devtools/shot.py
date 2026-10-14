# -*- coding: utf-8 -*-
"""화면 캡처 도구 (시험·검수용). 창을 띄우지 않고 화면을 PNG 로 저장한다.
   python -m seomu.devtools.shot home out.png [--dark] [--w 1440 --h 900]"""
import os
import sys

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
os.environ.setdefault("SEOMU_HOME", "/tmp/seomu_shot_home")


def capture(page: str, out: str, dark: bool = False, w: int = 1440, h: int = 900, before=None):
    from ..app import make_app, make_window
    from ..core.settings import Settings
    from ..ui.theme import theme
    app = make_app([])
    s = Settings()
    s.data.clear()
    win = make_window(s)
    theme.set_dark(dark)
    win.resize(w, h)
    win.show()
    win.show_page(page)
    app.processEvents()
    if before:
        before(win)
    for _ in range(4):
        app.processEvents()
    win.grab().save(out, "PNG")
    return win


if __name__ == "__main__":
    a = sys.argv[1:]
    page, out = a[0], a[1]
    capture(page, out, "--dark" in a)
