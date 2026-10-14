# -*- coding: utf-8 -*-
from PySide6.QtCore import QSize, Qt
from PySide6.QtWidgets import QPushButton
from .. import icons
from ..theme import theme
from .common import soft_shadow


class Button(QPushButton):
    """variant: primary(그라데이션) · secondary(연한 포인트) · ghost(테두리) · danger · success · flat
       size: sm · md · lg · icon"""

    def __init__(self, text: str = "", variant: str = "primary", icon: str | None = None, size: str = "md", parent=None):
        super().__init__(text, parent)
        self._icon_name = icon
        self._variant = variant
        self.setProperty("variant", variant)
        self.setProperty("size", size)
        self.setCursor(Qt.PointingHandCursor)
        self.setFocusPolicy(Qt.StrongFocus)
        if size == "icon":
            self.setFixedSize(32, 32)
        self._shadow = None
        if variant in ("primary", "success") and size != "icon":
            self._shadow = soft_shadow(self, blur=14, dy=4)
        self._apply_icon()
        theme.changed.connect(self._apply_icon)

    def _icon_color(self) -> str:
        if not self.isEnabled():
            return theme.c("text3")
        return {"primary": "#FFFFFF", "success": "#FFFFFF", "secondary": theme.c("primary"),
                "danger": theme.c("danger"), "ghost": theme.c("text2"), "flat": theme.c("text2")}.get(self._variant, theme.c("text2"))

    def _apply_icon(self):
        if self._icon_name:
            self.setIcon(icons.icon(self._icon_name, self._icon_color(), 16, 2.0))
            self.setIconSize(QSize(16, 16))

    def setEnabled(self, on: bool):
        super().setEnabled(on)
        if self._shadow is not None:
            self._shadow.setEnabled(on)
        self._apply_icon()

    def set_icon_name(self, name: str | None):
        self._icon_name = name
        self._apply_icon()
