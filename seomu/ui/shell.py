# -*- coding: utf-8 -*-
"""메인 창 — 사이드바 · 상단바 · 화면 전환 · 빠른 이동 · 홈 · 설정"""
import importlib
import json
import time
from PySide6.QtCore import QEvent, QPointF, QRectF, QSize, Qt, Signal
from PySide6.QtGui import QColor, QFont, QKeySequence, QLinearGradient, QPainter, QPen, QRadialGradient, QShortcut
from PySide6.QtWidgets import (QApplication, QDialog, QFileDialog, QFrame, QGridLayout, QHBoxLayout, QLabel, QLineEdit,
                               QListWidget, QListWidgetItem, QMainWindow, QMessageBox, QPushButton, QScrollArea, QSizePolicy,
                               QStackedWidget, QVBoxLayout, QWidget)
from .. import registry
from ..core.settings import Settings
from . import icons, tokens as T
from .theme import build_qss, theme
from .widgets import Button, Card, Field, FormGrid, Note, StepCard, Toast, label
from .widgets.common import soft_shadow

SIDEBAR_W, RAIL_W = 248, 68


class AppContext:
    """도구 화면들이 공유하는 것들"""

    def __init__(self, window: "MainWindow", settings: Settings):
        self.window = window
        self.settings = settings

    def toast(self, text: str):
        self.window.toast.show_message(text)

    def key(self, name: str) -> str:
        return self.settings.sub("keys", name, "")

    def info(self, name: str) -> str:
        return self.settings.sub("info", name, "")


# ───────────────────────────── 사이드바 ─────────────────────────────
class NavButton(QPushButton):
    def __init__(self, icon_name: str, text: str, hint: str = "", parent=None):
        super().__init__(parent)
        self.icon_name, self.text_, self.hint = icon_name, text, hint
        self.setCheckable(True)
        self.setCursor(Qt.PointingHandCursor)
        self.setFixedHeight(40)
        self.setFocusPolicy(Qt.NoFocus)
        self._hover = False
        self.collapsed = False
        theme.changed.connect(self.update)

    def enterEvent(self, e):
        self._hover = True
        self.update()

    def leaveEvent(self, e):
        self._hover = False
        self.update()

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(1, 1, -1, -1)
        on = self.isChecked()
        if on:
            p.setBrush(theme.q("surface"))
            p.setPen(QPen(theme.q("border"), 1))
            p.drawRoundedRect(r, T.R_BTN, T.R_BTN)
            g = QLinearGradient(0, 0, 0, self.height())
            g.setColorAt(0, QColor(theme.c("g1")))
            g.setColorAt(1, QColor(theme.c("g2")))
            p.setPen(Qt.NoPen)
            p.setBrush(g)
            p.drawRoundedRect(QRectF(-4, 10, 7, self.height() - 20), 3, 3)
        elif self._hover:
            p.setBrush(theme.q("hover"))
            p.setPen(Qt.NoPen)
            p.drawRoundedRect(r, T.R_BTN, T.R_BTN)
        col = theme.c("primary") if on else (theme.c("text") if self._hover else theme.c("text2"))
        pm = icons.pixmap(self.icon_name, col, 18, 1.8)
        if self.collapsed:
            p.drawPixmap(int((self.width() - 18) / 2), int((self.height() - 18) / 2), pm)
            return
        p.drawPixmap(14, int((self.height() - 18) / 2), pm)
        f = QFont(self.font())
        f.setPixelSize(14)
        f.setWeight(QFont.DemiBold if on else QFont.Medium)
        p.setFont(f)
        p.setPen(theme.q("text") if on else theme.q("text2"))
        p.drawText(QRectF(44, 0, self.width() - 60, self.height()), Qt.AlignVCenter | Qt.AlignLeft, self.text_)
        if self.hint:
            f2 = QFont(self.font())
            f2.setPixelSize(10)
            p.setFont(f2)
            w = p.fontMetrics().horizontalAdvance(self.hint) + 14
            kr = QRectF(self.width() - w - 10, (self.height() - 18) / 2, w, 18)
            p.setPen(Qt.NoPen)
            p.setBrush(theme.q("surface3"))
            p.drawRoundedRect(kr, 5, 5)
            p.setPen(theme.q("text3"))
            p.drawText(kr, Qt.AlignCenter, self.hint)


class Sidebar(QFrame):
    navigate = Signal(str)
    toggled = Signal(bool)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFixedWidth(SIDEBAR_W)
        self.collapsed = False
        lay = QVBoxLayout(self)
        lay.setContentsMargins(12, 16, 12, 12)
        lay.setSpacing(2)
        # 브랜드
        self.brand = QWidget()
        bl = QHBoxLayout(self.brand)
        bl.setContentsMargins(6, 4, 6, 14)
        bl.setSpacing(11)
        self.mark = QLabel()
        self.mark.setFixedSize(34, 34)
        bl.addWidget(self.mark)
        self.brand_text = QWidget()
        bt = QVBoxLayout(self.brand_text)
        bt.setContentsMargins(0, 0, 0, 0)
        bt.setSpacing(0)
        self.b1 = QLabel("서무 업무 자동화")
        self.b2 = QLabel("General Affairs Suite")
        bt.addWidget(self.b1)
        bt.addWidget(self.b2)
        bl.addWidget(self.brand_text, 1)
        lay.addWidget(self.brand)
        # 메뉴
        self.buttons: dict[str, NavButton] = {}
        self.group_labels: list[QLabel] = []
        self._add("home", "home", "홈")
        n = 0
        for g in registry.GROUPS:
            self._group(g)
            for t in [x for x in registry.TOOLS if x.group == g]:
                n += 1
                self._add(t.id, t.icon, t.label, f"Alt {n}")
        self._group("기타")
        self._add("settings", "gear", "설정")
        lay.addStretch(1)
        # 바닥
        self.foot = QLabel("모든 처리는 이 PC 안에서만\n이뤄지며 서버로 전송되지 않습니다.")
        self.foot.setWordWrap(True)
        self.ver = QLabel("")
        fl = QHBoxLayout()
        fl.setContentsMargins(6, 8, 0, 0)
        col = QVBoxLayout()
        col.setSpacing(4)
        col.addWidget(self.foot)
        col.addWidget(self.ver)
        fl.addLayout(col, 1)
        self.collapse_btn = Button("", "flat", "chev_left", "icon")
        self.collapse_btn.clicked.connect(self.toggle)
        fl.addWidget(self.collapse_btn, 0, Qt.AlignBottom)
        lay.addLayout(fl)
        self._style()
        theme.changed.connect(self._style)

    def _group(self, text: str):
        lb = QLabel(text)
        lb.setContentsMargins(10, 14, 0, 4)
        self.group_labels.append(lb)
        self.layout().addWidget(lb)

    def _add(self, key: str, icon_name: str, text: str, hint: str = ""):
        b = NavButton(icon_name, text, hint)
        b.clicked.connect(lambda _=False, k=key: self.navigate.emit(k))
        self.buttons[key] = b
        self.layout().addWidget(b)

    def set_current(self, key: str):
        for k, b in self.buttons.items():
            b.setChecked(k == key)

    def toggle(self):
        self.set_collapsed(not self.collapsed)

    def set_collapsed(self, on: bool):
        self.collapsed = on
        self.setFixedWidth(RAIL_W if on else SIDEBAR_W)
        for b in self.buttons.values():
            b.collapsed = on
            b.update()
        for lb in self.group_labels:
            lb.setText("" if on else lb.property("full"))
            lb.setFixedHeight(18 if not on else 14)
        self.brand_text.setVisible(not on)
        self.foot.setVisible(not on)
        self.ver.setVisible(not on)
        self.collapse_btn.set_icon_name("chev_right" if on else "chev_left")
        self.toggled.emit(on)

    def _style(self):
        c = theme.tok
        self.mark.setStyleSheet(f"background: qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['g1']}, stop:1 {c['g2']}); border-radius:10px;")
        self.mark.setPixmap(icons.pixmap("grid", "#FFFFFF", 18, 1.9))
        self.mark.setAlignment(Qt.AlignCenter)
        self.b1.setStyleSheet(f"font-size:14.5px; font-weight:700; color:{c['text']}; background:transparent;")
        self.b2.setStyleSheet(f"font-size:11px; color:{c['text3']}; background:transparent;")
        for lb in self.group_labels:
            if lb.property("full") is None:
                lb.setProperty("full", lb.text())
            lb.setStyleSheet(f"font-size:11px; font-weight:700; color:{c['text3']}; background:transparent;")
        self.foot.setStyleSheet(f"font-size:11px; color:{c['text3']}; background:transparent;")
        self.ver.setStyleSheet(f"font-size:11px; color:{c['text3']}; background:transparent;")

    def paintEvent(self, e):
        p = QPainter(self)
        g = QLinearGradient(0, 0, 0, self.height())
        g.setColorAt(0, theme.q("sidebar_top"))
        g.setColorAt(1, theme.q("sidebar_bottom"))
        p.fillRect(self.rect(), g)
        p.setPen(QPen(theme.q("border"), 1))
        p.drawLine(self.width() - 1, 0, self.width() - 1, self.height())


# ───────────────────────────── 상단바 ─────────────────────────────
class SearchPill(QPushButton):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setCursor(Qt.PointingHandCursor)
        self.setFixedHeight(34)
        self.setMinimumWidth(320)
        self.setMaximumWidth(380)
        self._hover = False
        theme.changed.connect(self.update)

    def enterEvent(self, e):
        self._hover = True
        self.update()

    def leaveEvent(self, e):
        self._hover = False
        self.update()

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(1, 1, -1, -1)
        p.setBrush(theme.q("surface"))
        p.setPen(QPen(theme.q("primary") if self._hover else theme.q("border_strong"), 1))
        p.drawRoundedRect(r, T.R_BTN, T.R_BTN)
        p.drawPixmap(12, 9, icons.pixmap("search", theme.c("text3"), 15, 2.0))
        f = QFont(self.font())
        f.setPixelSize(13)
        p.setFont(f)
        p.setPen(theme.q("text3"))
        p.drawText(QRectF(36, 0, self.width() - 100, self.height()), Qt.AlignVCenter | Qt.AlignLeft, "업무·메뉴 빠른 이동")
        kr = QRectF(self.width() - 64, 8, 54, 18)
        p.setPen(Qt.NoPen)
        p.setBrush(theme.q("surface3"))
        p.drawRoundedRect(kr, 5, 5)
        f.setPixelSize(11)
        p.setFont(f)
        p.setPen(theme.q("text3"))
        p.drawText(kr, Qt.AlignCenter, "Ctrl K")


class UserChip(QPushButton):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setCursor(Qt.PointingHandCursor)
        self.setFixedHeight(36)
        self.name = "서무 담당자"
        theme.changed.connect(self.update)

    def set_name(self, name: str):
        self.name = name or "서무 담당자"
        f = QFont(self.font())
        f.setPixelSize(12.5)
        w = QApplication.fontMetrics().horizontalAdvance(self.name) + 66
        self.setFixedWidth(max(110, w))
        self.update()

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(1, 1, -1, -1)
        p.setBrush(theme.q("surface"))
        p.setPen(QPen(theme.q("border"), 1))
        p.drawRoundedRect(r, r.height() / 2, r.height() / 2)
        g = QLinearGradient(0, 0, 28, 28)
        g.setColorAt(0, QColor(theme.c("g1")))
        g.setColorAt(1, QColor(theme.c("g2")))
        p.setPen(Qt.NoPen)
        p.setBrush(g)
        p.drawEllipse(QRectF(5, 5, 26, 26))
        f = QFont(self.font())
        f.setPixelSize(12)
        f.setBold(True)
        p.setFont(f)
        p.setPen(QColor("#FFFFFF"))
        p.drawText(QRectF(5, 5, 26, 26), Qt.AlignCenter, self.name[:1])
        f.setBold(False)
        f.setPixelSize(12.5)
        p.setFont(f)
        p.setPen(theme.q("text2"))
        p.drawText(QRectF(40, 0, self.width() - 48, self.height()), Qt.AlignVCenter | Qt.AlignLeft, self.name)


class Topbar(QFrame):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFixedHeight(56)
        lay = QHBoxLayout(self)
        lay.setContentsMargins(28, 0, 28, 0)
        lay.setSpacing(12)
        self.crumb_g = QLabel("홈")
        self.sep = QLabel("/")
        self.crumb_t = QLabel("서무 업무 자동화")
        lay.addWidget(self.crumb_g)
        lay.addWidget(self.sep)
        lay.addWidget(self.crumb_t)
        lay.addStretch(1)
        self.search = SearchPill()
        lay.addWidget(self.search)
        lay.addStretch(1)
        self.theme_btn = Button("", "flat", "moon", "icon")
        lay.addWidget(self.theme_btn)
        self.user = UserChip()
        lay.addWidget(self.user)
        self._style()
        theme.changed.connect(self._style)

    def set_crumb(self, group: str, title: str):
        self.crumb_g.setText(group)
        self.crumb_t.setText(title)

    def _style(self):
        c = theme.tok
        self.crumb_g.setStyleSheet(f"color:{c['text3']}; font-size:13px; background:transparent;")
        self.sep.setStyleSheet(f"color:{c['text3']}; background:transparent;")
        self.crumb_t.setStyleSheet(f"color:{c['text']}; font-size:13.5px; font-weight:600; background:transparent;")
        self.theme_btn.set_icon_name("sun" if theme.dark else "moon")

    def paintEvent(self, e):
        p = QPainter(self)
        col = QColor(theme.c("bg"))
        col.setAlpha(150)
        p.fillRect(self.rect(), col)
        p.setPen(QPen(theme.q("border"), 1))
        p.drawLine(0, self.height() - 1, self.width(), self.height() - 1)


# ───────────────────────────── 빠른 이동 ─────────────────────────────
class Palette(QDialog):
    chosen = Signal(str)

    def __init__(self, parent):
        super().__init__(parent, Qt.Dialog | Qt.FramelessWindowHint)
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setModal(True)
        self.setFixedWidth(580)
        outer = QVBoxLayout(self)
        outer.setContentsMargins(10, 10, 10, 18)
        self.box = QFrame()
        self.box.setObjectName("palbox")
        bl = QVBoxLayout(self.box)
        bl.setContentsMargins(0, 0, 0, 0)
        bl.setSpacing(0)
        self.edit = QLineEdit()
        self.edit.setPlaceholderText("이동할 업무나 메뉴를 입력하세요 (예: 여비, 급식, 블러)")
        self.edit.textChanged.connect(self._filter)
        bl.addWidget(self.edit)
        self.list = QListWidget()
        self.list.setFixedHeight(300)
        self.list.itemActivated.connect(self._pick)
        self.list.itemClicked.connect(self._pick)
        bl.addWidget(self.list)
        self.foot = QLabel("↑ ↓ 선택     Enter 열기     Esc 닫기")
        bl.addWidget(self.foot)
        outer.addWidget(self.box)
        soft_shadow(self.box, blur=40, dy=12)
        self.items: list[tuple] = []
        self.edit.installEventFilter(self)

    def set_items(self, items):
        self.items = items

    def _style(self):
        c = theme.tok
        self.box.setStyleSheet(f"QFrame#palbox {{ background:{c['surface']}; border:1px solid {c['border']}; border-radius:16px; }}")
        self.edit.setStyleSheet("QLineEdit { border:0; border-bottom:1px solid %s; border-radius:0; min-height:54px; padding:0 20px; font-size:15px; background:transparent; }" % c["border"])
        self.list.setStyleSheet(f"QListWidget {{ background:transparent; border:0; padding:8px; }} QListWidget::item {{ padding:10px 12px; border-radius:9px; }} "
                                f"QListWidget::item:selected {{ background:{c['primary_tint']}; color:{c['primary']}; }}")
        self.foot.setStyleSheet(f"background:{c['surface2']}; color:{c['text3']}; font-size:11.5px; padding:9px 18px; border-bottom-left-radius:16px; border-bottom-right-radius:16px; border-top:1px solid {c['border']};")

    def open(self):
        self._style()
        self.edit.clear()
        self._filter("")
        p = self.parentWidget()
        g = p.geometry()
        self.move(p.mapToGlobal(p.rect().topLeft()).x() + (p.width() - self.width()) // 2, p.mapToGlobal(p.rect().topLeft()).y() + 110)
        self.show()
        self.edit.setFocus()

    def _filter(self, text: str):
        q = text.strip().lower()
        self.list.clear()
        for key, label_, sub, ic, kw in self.items:
            if q and q not in (label_ + " " + sub + " " + kw).lower():
                continue
            it = QListWidgetItem(icons.icon(ic, theme.c("primary"), 18, 1.9), f"  {label_}      {sub}")
            it.setData(Qt.UserRole, key)
            self.list.addItem(it)
        if self.list.count():
            self.list.setCurrentRow(0)

    def _pick(self, it):
        self.hide()
        self.chosen.emit(it.data(Qt.UserRole))

    def eventFilter(self, obj, ev):
        if obj is self.edit and ev.type() == QEvent.KeyPress:
            k = ev.key()
            if k in (Qt.Key_Down, Qt.Key_Up):
                n = self.list.count()
                if n:
                    self.list.setCurrentRow((self.list.currentRow() + (1 if k == Qt.Key_Down else -1)) % n)
                return True
            if k in (Qt.Key_Return, Qt.Key_Enter):
                if self.list.currentItem():
                    self._pick(self.list.currentItem())
                return True
        return super().eventFilter(obj, ev)


# ───────────────────────────── 홈 ─────────────────────────────
class Hero(QFrame):
    def __init__(self, ctx: AppContext, parent=None):
        super().__init__(parent)
        self.setFixedHeight(236)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(34, 28, 34, 28)
        lay.setSpacing(0)
        self.date = QLabel()
        self.h1 = QLabel()
        self.p = QLabel("자주 하는 서무 업무를 한 곳에 모았습니다. 업무를 고르고 파일을 올리면 끝입니다. 모든 처리는 이 PC 안에서만 이뤄집니다.")
        self.p.setWordWrap(True)
        self.p.setMaximumWidth(560)
        lay.addWidget(self.date)
        lay.addSpacing(4)
        lay.addWidget(self.h1)
        lay.addSpacing(6)
        lay.addWidget(self.p)
        lay.addStretch(1)
        row = QHBoxLayout()
        row.setSpacing(8)
        self.search_btn = QPushButton("  업무 빠른 이동   Ctrl K")
        self.set_btn = QPushButton("  기본 정보 · API 키")
        for b, solid in ((self.search_btn, True), (self.set_btn, False)):
            b.setCursor(Qt.PointingHandCursor)
            b.setFixedHeight(36)
            if solid:
                b.setStyleSheet("QPushButton { background:#FFFFFF; color:#4B5CF0; border:1px solid #FFFFFF; border-radius:8px; padding:0 16px; font-weight:600; font-size:13px; }"
                                "QPushButton:hover { background:#F2F4FF; }")
            else:
                b.setStyleSheet("QPushButton { background:rgba(255,255,255,0.16); color:#FFFFFF; border:1px solid rgba(255,255,255,0.38); border-radius:8px; padding:0 16px; font-weight:600; font-size:13px; }"
                                "QPushButton:hover { background:rgba(255,255,255,0.28); }")
            row.addWidget(b)
        row.addStretch(1)
        lay.addLayout(row)
        self.date.setStyleSheet("color:rgba(255,255,255,0.86); font-size:12.5px; font-weight:500; background:transparent;")
        self.h1.setStyleSheet("color:#FFFFFF; font-size:28px; font-weight:700; background:transparent;")
        self.p.setStyleSheet("color:rgba(255,255,255,0.9); font-size:14px; background:transparent;")
        self.refresh(ctx)

    def refresh(self, ctx: AppContext):
        t = time.localtime()
        wd = "월화수목금토일"[t.tm_wday]
        dept = ctx.info("dept")
        self.date.setText(f"{t.tm_year}년 {t.tm_mon}월 {t.tm_mday}일 ({wd})" + (f" · {dept}" if dept else ""))
        greet = "좋은 아침입니다" if t.tm_hour < 12 else ("안녕하세요" if t.tm_hour < 18 else "수고하셨습니다")
        name = ctx.info("name")
        self.h1.setText(greet + (f", {name}님" if name else ""))

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(0, 0, 0, -6)
        # 그림자
        for i, a in enumerate((18, 14, 10, 6)):
            p.setPen(Qt.NoPen)
            p.setBrush(QColor(79, 93, 240, a))
            p.drawRoundedRect(r.adjusted(4 - i * 1.5, 10 - i, -4 + i * 1.5, 6 + i * 1.5), 22, 22)
        g = QLinearGradient(r.topLeft(), r.bottomRight())
        g.setColorAt(0, QColor("#4B5CF0"))
        g.setColorAt(0.55, QColor("#6D55F2"))
        g.setColorAt(1, QColor("#8B5CF6"))
        p.setBrush(g)
        p.drawRoundedRect(r, 20, 20)
        p.setClipPath(self._clip(r))
        rg = QRadialGradient(QPointF(r.right() - 90, r.top() - 20), 320)
        rg.setColorAt(0, QColor(255, 255, 255, 72))
        rg.setColorAt(1, QColor(255, 255, 255, 0))
        p.setBrush(rg)
        p.drawRect(r)
        rg2 = QRadialGradient(QPointF(r.right() - 20, r.bottom() + 20), 190)
        rg2.setColorAt(0, QColor(255, 255, 255, 46))
        rg2.setColorAt(1, QColor(255, 255, 255, 0))
        p.setBrush(rg2)
        p.drawRect(r)

    def _clip(self, r):
        from PySide6.QtGui import QPainterPath
        path = QPainterPath()
        path.addRoundedRect(r, 20, 20)
        return path


class ToolCard(QFrame):
    clicked = Signal(str)

    def __init__(self, info, number: int, parent=None):
        super().__init__(parent)
        self.info = info
        self._hover = False
        self.setCursor(Qt.PointingHandCursor)
        self.setMinimumHeight(232)
        self.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Fixed)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(20, 18, 20, 18)
        lay.setSpacing(8)
        top = QHBoxLayout()
        self.tile = QLabel()
        self.tile.setFixedSize(40, 40)
        top.addWidget(self.tile)
        top.addStretch(1)
        self.alt = QLabel(f"Alt {number}")
        top.addWidget(self.alt, 0, Qt.AlignTop)
        lay.addLayout(top)
        lay.addSpacing(2)
        self.t = QLabel(info.title)
        lay.addWidget(self.t)
        self.d = QLabel(info.desc)
        self.d.setWordWrap(True)
        self.d.setAlignment(Qt.AlignTop | Qt.AlignLeft)
        lay.addWidget(self.d, 1)
        tags = QHBoxLayout()
        tags.setSpacing(5)
        from .widgets import Tag
        for t in info.tags:
            tags.addWidget(Tag(t))
        tags.addStretch(1)
        lay.addLayout(tags)
        self.go = QLabel("열기 →")
        lay.addWidget(self.go)
        self._style()
        theme.changed.connect(self._style)

    def _style(self):
        c = theme.tok
        self.tile.setStyleSheet(f"background: qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['g1']}, stop:1 {c['g2']}); border-radius:11px;")
        self.tile.setPixmap(icons.pixmap(self.info.icon, "#FFFFFF", 20, 1.9))
        self.tile.setAlignment(Qt.AlignCenter)
        self.alt.setStyleSheet(f"background:{c['surface3']}; color:{c['text3']}; font-size:11px; font-weight:600; border-radius:5px; padding:1px 7px;")
        self.t.setStyleSheet(f"font-size:15.5px; font-weight:700; color:{c['text']}; background:transparent;")
        self.d.setStyleSheet(f"font-size:13px; color:{c['text2']}; background:transparent;")
        self.go.setStyleSheet(f"font-size:12.5px; font-weight:600; color:{c['primary']}; background:transparent;")

    def enterEvent(self, e):
        self._hover = True
        self.update()

    def leaveEvent(self, e):
        self._hover = False
        self.update()

    def mouseReleaseEvent(self, e):
        if e.button() == Qt.LeftButton:
            self.clicked.emit(self.info.id)

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(1.5, 1, -1.5, -5)
        if self._hover:
            r = r.translated(0, -2)
        for grow, a in ((4, 6), (3, 8), (2, 10), (1, 12)):
            p.setPen(Qt.NoPen)
            p.setBrush(QColor(79, 93, 240, a * (2 if self._hover else 1)) if not theme.dark else QColor(0, 0, 0, a * 3))
            p.drawRoundedRect(r.adjusted(-grow + 2, grow + 1, grow - 2, grow + 1), T.R_CARD + 2, T.R_CARD + 2)
        g = QLinearGradient(r.topLeft(), r.bottomLeft())
        g.setColorAt(0, theme.q("card_top"))
        g.setColorAt(1, theme.q("card_bottom"))
        p.setBrush(g)
        p.setPen(QPen(theme.q("primary_border") if self._hover else theme.q("border"), 1))
        p.drawRoundedRect(r, T.R_CARD, T.R_CARD)


class HomePage(QScrollArea):
    navigate = Signal(str)

    def __init__(self, ctx: AppContext, parent=None):
        super().__init__(parent)
        self.ctx = ctx
        self.setWidgetResizable(True)
        self.setFrameShape(QFrame.NoFrame)
        self.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        outer = QWidget()
        self.setWidget(outer)
        ol = QHBoxLayout(outer)
        ol.setContentsMargins(36, 26, 36, 40)
        ol.addStretch(1)
        self.inner = QWidget()
        self.inner.setMaximumWidth(1180)
        ol.addWidget(self.inner, 100)
        ol.addStretch(1)
        self.v = QVBoxLayout(self.inner)
        self.v.setContentsMargins(0, 0, 0, 0)
        self.v.setSpacing(0)
        self.hero = Hero(ctx)
        self.hero.search_btn.clicked.connect(lambda: ctx.window.open_palette())
        self.hero.set_btn.clicked.connect(lambda: self.navigate.emit("settings"))
        self.v.addWidget(self.hero)
        self.recent_box = QVBoxLayout()
        self.v.addLayout(self.recent_box)
        n = 0
        for g in registry.GROUPS:
            self.v.addSpacing(22)
            self.v.addWidget(self._group_title(g))
            self.v.addSpacing(12)
            grid = QGridLayout()
            grid.setHorizontalSpacing(16)
            grid.setVerticalSpacing(16)
            for c in range(3):
                grid.setColumnStretch(c, 1)
            tools = [x for x in registry.TOOLS if x.group == g]
            for i, t in enumerate(tools):
                n += 1
                card = ToolCard(t, n)
                card.clicked.connect(self.navigate)
                grid.addWidget(card, i // 3, i % 3)
            self.v.addLayout(grid)
        self.v.addSpacing(26)
        self.v.addWidget(Note("<b>개인정보는 밖으로 나가지 않습니다.</b> 엑셀·HWPX·라벨 처리는 모두 이 PC 안에서 이뤄집니다. "
                              "단, 공과금 고지서 인식은 입력한 AI 키로 이미지를 Google(Gemini) 또는 Anthropic(Claude)에 보내 글자를 읽는 기능이므로, 개인정보가 많은 문서에는 주의하세요.", "info"))
        self.v.addStretch(1)

    def _group_title(self, text: str) -> QWidget:
        w = QWidget()
        l = QHBoxLayout(w)
        l.setContentsMargins(0, 0, 0, 0)
        l.setSpacing(10)
        lb = QLabel(text)
        lb.setStyleSheet(f"font-size:12px; font-weight:700; color:{theme.c('text3')}; letter-spacing:1px; background:transparent;")
        line = QFrame()
        line.setFixedHeight(1)
        line.setStyleSheet(f"background:{theme.c('border')};")
        l.addWidget(lb)
        l.addWidget(line, 1)
        return w

    def on_show(self):
        self.hero.refresh(self.ctx)


# ───────────────────────────── 설정 ─────────────────────────────
class SettingsPage(QScrollArea):
    info_changed = Signal()

    KEYS = [("kakao", "카카오 REST API 키", "관외출장 · 주소검색/거리계산"), ("opinet", "오피넷 인증키", "관외출장 · 전국 평균 유가"),
            ("gemini", "Gemini API 키", "공과금 고지서 · 기본 AI"), ("claude", "Claude API 키", "공과금 고지서 · 선택 AI")]

    def __init__(self, ctx: AppContext, parent=None):
        super().__init__(parent)
        self.ctx = ctx
        self.setWidgetResizable(True)
        self.setFrameShape(QFrame.NoFrame)
        outer = QWidget()
        self.setWidget(outer)
        ol = QHBoxLayout(outer)
        ol.setContentsMargins(36, 26, 36, 40)
        ol.addStretch(1)
        inner = QWidget()
        inner.setMaximumWidth(860)
        ol.addWidget(inner, 100)
        ol.addStretch(1)
        v = QVBoxLayout(inner)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(16)
        from ..tools.base import IconTile
        h = QHBoxLayout()
        h.setSpacing(16)
        h.addWidget(IconTile("gear"), 0, Qt.AlignTop)
        col = QVBoxLayout()
        col.setSpacing(2)
        col.addWidget(label("설정", "eyebrow"))
        col.addWidget(label("기본 정보 · API 키 · 백업", "title"))
        col.addWidget(label("한 번만 입력해 두면 모든 도구가 함께 씁니다. 입력한 내용은 이 PC의 프로그램 저장소에만 보관됩니다.", "desc", wrap=True))
        h.addLayout(col, 1)
        v.addLayout(h)
        v.addSpacing(6)
        # 기본 정보
        c1 = StepCard(None, "기본 정보", "홈 인사말과 관외출장의 기본 부서명에 쓰입니다.")
        g = FormGrid(2)
        self.name = QLineEdit(ctx.info("name"))
        self.name.setPlaceholderText("예: 홍길동")
        self.dept = QLineEdit(ctx.info("dept"))
        self.dept.setPlaceholderText("예: 문화유산과")
        g.add(Field("담당자 이름", self.name, "상단 우측에 표시"))
        g.add(Field("부서명", self.dept, "관외출장 기본값"))
        c1.add(g)
        row = QHBoxLayout()
        save = Button("저장", "primary")
        save.clicked.connect(self.save_info)
        row.addWidget(save)
        self.info_note = label("", "small")
        row.addWidget(self.info_note)
        row.addStretch(1)
        c1.add_layout(row)
        v.addWidget(c1)
        # 키
        c2 = StepCard(None, "API 키", "비워 두면 해당 기능은 키 없이 가능한 범위(추정 거리 등)로만 동작합니다.")
        self.key_edits = {}
        g2 = FormGrid(2)
        for k, lab, sub in self.KEYS:
            e = QLineEdit(ctx.key(k))
            e.setEchoMode(QLineEdit.Password)
            e.setPlaceholderText("입력하지 않으면 사용 안 함")
            self.key_edits[k] = e
            g2.add(Field(lab, e, sub))
        c2.add(g2)
        row2 = QHBoxLayout()
        ksave = Button("저장", "primary")
        ksave.clicked.connect(self.save_keys)
        kclear = Button("모두 지우기", "ghost")
        kclear.clicked.connect(self.clear_keys)
        row2.addWidget(ksave)
        row2.addWidget(kclear)
        self.key_note = label("", "small")
        row2.addWidget(self.key_note)
        row2.addStretch(1)
        c2.add_layout(row2)
        c2.add(Note("공용 PC나 다른 사람이 함께 쓰는 계정에서는 키를 저장하지 마세요. 저장된 키는 이 프로그램을 쓰는 누구나 볼 수 있습니다.", "warn"))
        v.addWidget(c2)
        # 백업
        c3 = StepCard(None, "작업 데이터 백업 · 복원", "관외출장의 부서원 명단과 출장 내역을 파일로 저장하거나 불러옵니다. PC를 바꾸기 전에 백업해 두세요. (API 키는 백업에 넣지 않습니다.)")
        row3 = QHBoxLayout()
        b1 = Button("백업 파일 저장", "secondary", "down")
        b1.clicked.connect(self.backup)
        b2 = Button("백업 불러오기", "ghost", "up")
        b2.clicked.connect(self.restore)
        row3.addWidget(b1)
        row3.addWidget(b2)
        row3.addStretch(1)
        c3.add_layout(row3)
        v.addWidget(c3)
        v.addStretch(1)

    def save_info(self):
        self.ctx.settings.set_sub("info", "name", self.name.text().strip())
        self.ctx.settings.set_sub("info", "dept", self.dept.text().strip())
        self.info_note.setText("저장했습니다")
        self.info_changed.emit()

    def save_keys(self):
        for k, e in self.key_edits.items():
            self.ctx.settings.set_sub("keys", k, e.text().strip())
        self.key_note.setText("저장했습니다")

    def clear_keys(self):
        if QMessageBox.question(self, "API 키", "저장된 API 키를 모두 지울까요?") == QMessageBox.Yes:
            for k, e in self.key_edits.items():
                e.clear()
                self.ctx.settings.set_sub("keys", k, "")
            self.key_note.setText("지웠습니다")

    def backup(self):
        fn, _ = QFileDialog.getSaveFileName(self, "백업 파일 저장", f"서무업무자동화_백업_{time.strftime('%Y%m%d')}.json", "백업 파일 (*.json)")
        if fn:
            with open(fn, "w", encoding="utf-8") as f:
                json.dump(self.ctx.settings.export_backup(), f, ensure_ascii=False, indent=1)
            self.ctx.toast("백업 파일을 저장했습니다")

    def restore(self):
        fn, _ = QFileDialog.getOpenFileName(self, "백업 불러오기", "", "백업 파일 (*.json)")
        if not fn:
            return
        try:
            with open(fn, encoding="utf-8") as f:
                obj = json.load(f)
            if QMessageBox.question(self, "복원", "현재 저장된 부서원 명단·출장 내역을 백업 내용으로 바꿀까요?") != QMessageBox.Yes:
                return
            self.ctx.settings.import_backup(obj)
            self.info_changed.emit()
            self.ctx.toast("복원했습니다")
        except Exception as e:
            QMessageBox.warning(self, "복원", f"백업 파일을 읽지 못했습니다.\n{e}")

    def on_show(self):
        pass


# ───────────────────────────── 메인 창 ─────────────────────────────
class Backdrop(QWidget):
    """바탕: 옅은 색 위에 은은한 빛 번짐 두 개"""

    def paintEvent(self, e):
        p = QPainter(self)
        p.fillRect(self.rect(), theme.q("bg"))
        a = 34 if not theme.dark else 40
        rg = QRadialGradient(QPointF(self.width() * 0.2, -40), 620)
        rg.setColorAt(0, QColor(99, 102, 241, a))
        rg.setColorAt(1, QColor(99, 102, 241, 0))
        p.fillRect(self.rect(), rg)
        rg2 = QRadialGradient(QPointF(self.width() * 0.95, -20), 520)
        rg2.setColorAt(0, QColor(168, 85, 247, int(a * 0.8)))
        rg2.setColorAt(1, QColor(168, 85, 247, 0))
        p.fillRect(self.rect(), rg2)


class MainWindow(QMainWindow):
    def __init__(self, settings: Settings):
        super().__init__()
        self.settings = settings
        self.ctx = AppContext(self, settings)
        self.setWindowTitle("서무 업무 자동화")
        self.resize(1440, 900)
        self.setMinimumSize(1100, 700)
        root = Backdrop()
        self.setCentralWidget(root)
        h = QHBoxLayout(root)
        h.setContentsMargins(0, 0, 0, 0)
        h.setSpacing(0)
        self.sidebar = Sidebar()
        h.addWidget(self.sidebar)
        right = QVBoxLayout()
        right.setContentsMargins(0, 0, 0, 0)
        right.setSpacing(0)
        self.topbar = Topbar()
        right.addWidget(self.topbar)
        self.stack = QStackedWidget()
        right.addWidget(self.stack, 1)
        h.addLayout(right, 1)
        self.toast = Toast(root)
        self.pages: dict[str, QWidget] = {}
        self.current = None
        self._auto_collapsed = False
        self.palette = Palette(self)
        self.palette.chosen.connect(self.show_page)
        self._build_palette_items()
        # 연결
        self.sidebar.navigate.connect(self.show_page)
        self.topbar.search.clicked.connect(self.open_palette)
        self.topbar.theme_btn.clicked.connect(self.toggle_theme)
        self.topbar.user.clicked.connect(lambda: self.show_page("settings"))
        self.topbar.user.set_name(self.ctx.info("name"))
        for i, t in enumerate(registry.TOOLS):
            QShortcut(QKeySequence(f"Alt+{i + 1}"), self, activated=lambda tid=t.id: self.show_page(tid))
        QShortcut(QKeySequence("Ctrl+K"), self, activated=self.open_palette)
        QShortcut(QKeySequence("Ctrl+B"), self, activated=self.sidebar.toggle)
        self.sidebar.ver.setText(self.settings.get("__version", ""))
        theme.set_dark(bool(settings.get("dark", False)))
        self.apply_theme()
        theme.changed.connect(self.apply_theme)
        self.show_page("home")

    # ---- 테마 --------------------------------------------------------
    def apply_theme(self):
        QApplication.instance().setStyleSheet(build_qss())
        self.palette._style()
        self.update()

    def toggle_theme(self):
        theme.set_dark(not theme.dark)
        self.settings.set("dark", theme.dark)

    # ---- 화면 ----------------------------------------------------------
    def _build_palette_items(self):
        items = [("home", "홈", "시작 화면", "home", "")]
        for i, t in enumerate(registry.TOOLS):
            items.append((t.id, t.title, f"{t.group} · Alt {i + 1}", t.icon, t.label + " " + " ".join(t.tags) + " " + t.desc))
        items.append(("settings", "설정", "기본 정보 · API 키 · 백업", "gear", "키 백업 부서 이름 복원"))
        items.append(("__theme", "다크/라이트 모드 전환", "화면 색", "moon", "테마 다크 라이트 어둡게"))
        self.palette.set_items(items)

    def open_palette(self):
        self.palette.open()

    def _make(self, key: str) -> QWidget:
        if key == "home":
            w = HomePage(self.ctx)
            w.navigate.connect(self.show_page)
        elif key == "settings":
            w = SettingsPage(self.ctx)
            w.info_changed.connect(self._info_changed)
        else:
            info = registry.by_id(key)
            mod, cls = info.factory.split(":")
            w = getattr(importlib.import_module(mod), cls)(info, self.ctx)
        self.pages[key] = w
        self.stack.addWidget(w)
        return w

    def _info_changed(self):
        self.topbar.user.set_name(self.ctx.info("name"))
        if "home" in self.pages:
            self.pages["home"].on_show()

    def show_page(self, key: str):
        if key == "__theme":
            self.toggle_theme()
            return
        info = registry.by_id(key)
        if info is None and key not in ("home", "settings"):
            key = "home"
        w = self.pages.get(key) or self._make(key)
        self.stack.setCurrentWidget(w)
        self.sidebar.set_current(key)
        if info:
            self.topbar.set_crumb(info.group, info.title)
        else:
            self.topbar.set_crumb("기타" if key == "settings" else "홈", "설정" if key == "settings" else "서무 업무 자동화")
        # 전체 화면 편집 도구는 사이드바를 자동으로 접는다
        if info and info.fill:
            if not self.sidebar.collapsed:
                self.sidebar.set_collapsed(True)
                self._auto_collapsed = True
        elif self._auto_collapsed:
            self.sidebar.set_collapsed(False)
            self._auto_collapsed = False
        if hasattr(w, "on_show"):
            w.on_show()
        self.current = key
        if info:
            rec = [x for x in self.settings.get("recent", []) if x != key]
            rec.insert(0, key)
            self.settings.set("recent", rec[:4])

    def resizeEvent(self, e):
        super().resizeEvent(e)
        if self.toast.isVisible():
            self.toast.show_message(self.toast.text(), 800)
