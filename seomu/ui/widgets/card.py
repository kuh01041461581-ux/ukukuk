# -*- coding: utf-8 -*-
"""카드 — 모든 단계 화면의 기본 틀 (윗면 하이라이트 + 부드러운 그림자 + 은은한 그라데이션)"""
from PySide6.QtCore import QRectF, Qt
from PySide6.QtGui import QColor, QLinearGradient, QPainter, QPen, QPainterPath
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QSizePolicy, QVBoxLayout, QWidget
from .. import tokens as T
from ..theme import theme, rgba
from .common import label


class Card(QFrame):
    def __init__(self, parent=None, margins=(31, 27, 31, 31)):
        super().__init__(parent)
        self.setAttribute(Qt.WA_StyledBackground, False)
        self.lay = QVBoxLayout(self)
        self.lay.setContentsMargins(*margins)
        self.lay.setSpacing(14)
        theme.changed.connect(self.update)

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(7, 5, -7, -9)
        # 그림자: 바깥으로 갈수록 옅어지는 둥근 사각형을 겹쳐 그린다
        base = QColor(0, 0, 0) if theme.dark else QColor(40, 52, 130)
        for i in range(7):
            spread = 7 - i
            c = QColor(base)
            c.setAlpha((5 + i) if not theme.dark else (10 + i * 2))
            p.setPen(Qt.NoPen)
            p.setBrush(c)
            p.drawRoundedRect(r.adjusted(-spread * 0.7, -spread * 0.2 + 1, spread * 0.7, spread * 0.9 + 1), T.R_CARD + spread * 0.6, T.R_CARD + spread * 0.6)
        g = QLinearGradient(r.topLeft(), r.bottomLeft())
        g.setColorAt(0, theme.q("card_top"))
        g.setColorAt(1, theme.q("card_bottom"))
        p.setBrush(g)
        p.setPen(QPen(theme.q("border"), 1))
        p.drawRoundedRect(r, T.R_CARD, T.R_CARD)
        # 윗면 하이라이트
        hl = QColor(255, 255, 255, 190 if not theme.dark else 14)
        p.setPen(QPen(hl, 1))
        p.drawLine(int(r.left() + T.R_CARD), int(r.top() + 1), int(r.right() - T.R_CARD), int(r.top() + 1))


class NumberBadge(QLabel):
    def __init__(self, text: str, done: bool = False, parent=None):
        super().__init__(text, parent)
        self.done = done
        self.setAlignment(Qt.AlignCenter)
        self.setFixedHeight(24)
        self.setMinimumWidth(24)
        self.setContentsMargins(6, 0, 6, 0)
        self.setStyleSheet("color:#FFFFFF; font-size:12px; font-weight:700; background:transparent;")
        theme.changed.connect(self.update)

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect())
        g = QLinearGradient(r.topLeft(), r.bottomRight())
        if self.done:
            g.setColorAt(0, QColor(theme.c("success_g1")))
            g.setColorAt(1, QColor(theme.c("success_g2")))
        else:
            g.setColorAt(0, QColor(theme.c("g1")))
            g.setColorAt(1, QColor(theme.c("g2")))
        p.setPen(Qt.NoPen)
        p.setBrush(g)
        p.drawRoundedRect(r, 8, 8)
        p.end()
        super().paintEvent(e)

    def set_done(self, done: bool, text: str | None = None):
        self.done = done
        if text is not None:
            self.setText(text)
        self.update()


class StepCard(Card):
    """번호 배지 · 제목 · 설명 · (오른쪽 도구) 머리글 + body 레이아웃"""

    def __init__(self, number: str | None, title: str, desc: str | None = None, tag: str | None = None, parent=None):
        super().__init__(parent)
        head = QHBoxLayout()
        head.setSpacing(12)
        head.setAlignment(Qt.AlignTop)
        self.badge = None
        if number is not None:
            self.badge = NumberBadge(number)
            head.addWidget(self.badge, 0, Qt.AlignTop)
        col = QVBoxLayout()
        col.setSpacing(3)
        trow = QHBoxLayout()
        trow.setSpacing(8)
        self.title_lb = label(title, "h2")
        trow.addWidget(self.title_lb)
        self.tag_lb = None
        if tag:
            from .display import Tag
            self.tag_lb = Tag(tag)
            trow.addWidget(self.tag_lb)
        trow.addStretch(1)
        col.addLayout(trow)
        self.desc_lb = None
        if desc:
            self.desc_lb = label(desc, "desc", wrap=True, rich=True)
            col.addWidget(self.desc_lb)
        head.addLayout(col, 1)
        self.right = QHBoxLayout()
        self.right.setSpacing(8)
        head.addLayout(self.right)
        self.lay.addLayout(head)
        self.body = QVBoxLayout()
        self.body.setSpacing(14)
        self.lay.addLayout(self.body)

    def set_desc(self, text: str):
        if self.desc_lb:
            self.desc_lb.setText(text)

    def add(self, w):
        self.body.addWidget(w)
        return w

    def add_layout(self, l):
        self.body.addLayout(l)
        return l
