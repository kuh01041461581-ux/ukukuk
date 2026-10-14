# -*- coding: utf-8 -*-
from PySide6.QtCore import QPropertyAnimation, QTimer, Qt
from PySide6.QtWidgets import QGraphicsOpacityEffect, QLabel, QWidget
from ..theme import theme


class Toast(QLabel):
    """창 아래 가운데에 잠깐 떴다 사라지는 알림"""

    def __init__(self, parent: QWidget):
        super().__init__(parent)
        self.setAlignment(Qt.AlignCenter)
        self.hide()
        self._eff = QGraphicsOpacityEffect(self)
        self.setGraphicsEffect(self._eff)
        self._anim = QPropertyAnimation(self._eff, b"opacity", self)
        self._timer = QTimer(self)
        self._timer.setSingleShot(True)
        self._timer.timeout.connect(self._fade)

    def show_message(self, text: str, ms: int = 2400):
        c = theme.tok
        self.setStyleSheet(f"background:{c['text']}; color:{c['bg']}; border-radius:10px; padding:10px 20px; font-weight:600;")
        self.setText(text)
        self.adjustSize()
        p = self.parentWidget()
        self.move((p.width() - self.width()) // 2, p.height() - self.height() - 36)
        self.raise_()
        self._eff.setOpacity(1)
        self.show()
        self._timer.start(ms)

    def _fade(self):
        self._anim.stop()
        self._anim.setDuration(300)
        self._anim.setStartValue(1.0)
        self._anim.setEndValue(0.0)
        self._anim.finished.connect(self.hide)
        self._anim.start()
