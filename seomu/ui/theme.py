# -*- coding: utf-8 -*-
"""테마(라이트/다크) 관리와 전체 스타일시트(QSS) 생성"""
from PySide6.QtCore import QObject, Signal
from PySide6.QtGui import QColor
from . import tokens as T
from . import icons


def rgba(hex_or_rgba: str, alpha: float | None = None) -> QColor:
    if hex_or_rgba.startswith("rgba"):
        r, g, b, a = [float(x) for x in hex_or_rgba[5:-1].split(",")]
        return QColor(int(r), int(g), int(b), int(a * 255))
    c = QColor(hex_or_rgba)
    if alpha is not None:
        c.setAlphaF(alpha)
    return c


class Theme(QObject):
    changed = Signal()

    def __init__(self):
        super().__init__()
        self.dark = False

    @property
    def tok(self) -> dict:
        return T.DARK if self.dark else T.LIGHT

    def c(self, key: str) -> str:
        return self.tok[key]

    def q(self, key: str, alpha: float | None = None) -> QColor:
        return rgba(self.tok[key], alpha)

    def set_dark(self, dark: bool):
        if dark != self.dark:
            self.dark = dark
            self.changed.emit()


theme = Theme()


def build_qss() -> str:
    c = theme.tok
    grad = f"qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['g1']}, stop:1 {c['g2']})"
    gradh = f"qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['g1h']}, stop:1 {c['g2h']})"
    ok_grad = f"qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 {c['success_g1']}, stop:1 {c['success_g2']})"
    fam = T.FONT_FALLBACK
    chev = icons.file_path('chev_down', c['text3'], 12)
    chk = icons.file_path('check', '#FFFFFF', 12, stroke=3)
    return f"""
* {{ font-family: {fam}; font-size: {T.FS['body']}px; color: {c['text']}; outline: 0; }}
QWidget {{ background: transparent; }}
QMainWindow, QDialog {{ background: {c['bg']}; }}
QToolTip {{ background: {c['text']}; color: {c['bg']}; border: 0; padding: 6px 10px; border-radius: 6px; }}

QLabel {{ background: transparent; }}
QLabel[role="title"] {{ font-size: {T.FS['title']}px; font-weight: 700; }}
QLabel[role="h2"] {{ font-size: {T.FS['h2']}px; font-weight: 700; }}
QLabel[role="desc"] {{ font-size: {T.FS['desc']}px; color: {c['text2']}; }}
QLabel[role="small"] {{ font-size: {T.FS['small']}px; color: {c['text3']}; }}
QLabel[role="muted"] {{ color: {c['text2']}; }}
QLabel[role="eyebrow"] {{ font-size: 11.5px; font-weight: 700; color: {c['primary']}; letter-spacing: 1px; }}

QLineEdit, QSpinBox, QDoubleSpinBox, QDateTimeEdit, QDateEdit, QTimeEdit, QComboBox, QPlainTextEdit, QTextEdit {{
  background: {c['surface']}; border: 1px solid {c['border_strong']}; border-radius: {T.R_INPUT}px;
  padding: 0 12px; min-height: 36px; selection-background-color: {c['primary_tint2']}; selection-color: {c['text']};
}}
QPlainTextEdit, QTextEdit {{ padding: 8px 12px; }}
QLineEdit:hover, QSpinBox:hover, QDoubleSpinBox:hover, QComboBox:hover, QDateTimeEdit:hover {{ border-color: {c['text3']}; }}
QLineEdit:focus, QSpinBox:focus, QDoubleSpinBox:focus, QComboBox:focus, QDateTimeEdit:focus, QPlainTextEdit:focus, QTextEdit:focus {{
  border: 1px solid {c['primary']}; }}
QLineEdit:read-only {{ background: {c['surface2']}; color: {c['text2']}; }}
QLineEdit:disabled, QComboBox:disabled, QSpinBox:disabled {{ background: {c['surface3']}; color: {c['text3']}; }}
QLineEdit[cell="true"], QSpinBox[cell="true"], QComboBox[cell="true"] {{
  background: {c['surface']}; border: 1px solid {c['border']}; min-height: 30px; padding: 0 8px; border-radius: 6px; }}
QLineEdit[cell="true"]:hover, QComboBox[cell="true"]:hover {{ border-color: {c['border_strong']}; }}
QLineEdit[cell="true"]:focus, QComboBox[cell="true"]:focus {{ background: {c['surface']}; border-color: {c['primary']}; }}

QComboBox {{ padding-right: 28px; }}
QComboBox::drop-down {{ border: 0; width: 26px; }}
QComboBox::down-arrow {{ image: url({chev}); width: 12px; height: 12px; }}
QComboBox QAbstractItemView {{ background: {c['surface']}; border: 1px solid {c['border_strong']}; border-radius: 8px; padding: 4px;
  selection-background-color: {c['primary_tint']}; selection-color: {c['primary']}; outline: 0; }}
QSpinBox::up-button, QSpinBox::down-button, QDoubleSpinBox::up-button, QDoubleSpinBox::down-button {{ width: 0; border: 0; }}

QPushButton {{ border-radius: {T.R_BTN}px; min-height: 38px; padding: 0 16px; font-weight: 600; border: 1px solid transparent; }}
QPushButton[variant="primary"] {{ background: {grad}; color: #FFFFFF; }}
QPushButton[variant="primary"]:hover {{ background: {gradh}; }}
QPushButton[variant="primary"]:pressed {{ padding-top: 1px; }}
QPushButton[variant="secondary"] {{ background: {c['primary_tint']}; color: {c['primary']}; border: 1px solid {c['primary_border']}; }}
QPushButton[variant="secondary"]:hover {{ background: {c['primary_tint2']}; }}
QPushButton[variant="ghost"] {{ background: {c['surface']}; color: {c['text2']}; border: 1px solid {c['border_strong']}; }}
QPushButton[variant="ghost"]:hover {{ background: {c['hover']}; color: {c['text']}; }}
QPushButton[variant="danger"] {{ background: {c['surface']}; color: {c['danger']}; border: 1px solid {c['danger']}; }}
QPushButton[variant="danger"]:hover {{ background: {c['danger_tint']}; }}
QPushButton[variant="success"] {{ background: {ok_grad}; color: #FFFFFF; }}
QPushButton[variant="flat"] {{ background: transparent; color: {c['text2']}; border: 0; padding: 0 10px; }}
QPushButton[variant="flat"]:hover {{ background: {c['hover']}; color: {c['text']}; }}
QPushButton[size="sm"] {{ min-height: 30px; padding: 0 11px; font-size: 12.5px; }}
QPushButton[size="lg"] {{ min-height: 46px; font-size: 14.5px; }}
QPushButton[size="icon"] {{ min-height: 32px; min-width: 32px; max-width: 32px; padding: 0; }}
QPushButton:disabled {{ background: {c['surface3']}; color: {c['text3']}; border: 1px solid transparent; }}

QCheckBox, QRadioButton {{ spacing: 9px; background: transparent; }}
QCheckBox::indicator {{ width: 17px; height: 17px; border-radius: 5px; border: 1.5px solid {c['border_strong']}; background: {c['surface']}; }}
QCheckBox::indicator:hover {{ border-color: {c['primary']}; }}
QCheckBox::indicator:checked {{ background: {grad}; border-color: {c['primary']}; image: url({chk}); }}
QRadioButton::indicator {{ width: 17px; height: 17px; border-radius: 9px; border: 1.5px solid {c['border_strong']}; background: {c['surface']}; }}
QRadioButton::indicator:checked {{ border: 5px solid {c['primary']}; background: {c['surface']}; }}

QSlider::groove:horizontal {{ height: 6px; border-radius: 3px; background: {c['surface3']}; }}
QSlider::sub-page:horizontal {{ border-radius: 3px; background: {grad}; }}
QSlider::handle:horizontal {{ width: 16px; height: 16px; margin: -6px 0; border-radius: 8px; background: {c['surface']}; border: 2px solid {c['primary']}; }}

QProgressBar {{ height: 8px; border: 0; border-radius: 4px; background: {c['surface3']}; text-align: center; color: transparent; }}
QProgressBar::chunk {{ border-radius: 4px; background: {grad}; }}

QScrollArea {{ border: 0; background: transparent; }}
QScrollArea > QWidget > QWidget {{ background: transparent; }}
QScrollBar:vertical {{ width: 12px; background: transparent; margin: 2px; }}
QScrollBar::handle:vertical {{ min-height: 36px; border-radius: 4px; background: {c['border_strong']}; margin: 0 2px; }}
QScrollBar::handle:vertical:hover {{ background: {c['text3']}; }}
QScrollBar:horizontal {{ height: 12px; background: transparent; margin: 2px; }}
QScrollBar::handle:horizontal {{ min-width: 36px; border-radius: 4px; background: {c['border_strong']}; margin: 2px 0; }}
QScrollBar::handle:horizontal:hover {{ background: {c['text3']}; }}
QScrollBar::add-line, QScrollBar::sub-line {{ width: 0; height: 0; }}
QScrollBar::add-page, QScrollBar::sub-page {{ background: transparent; }}

QTableWidget, QTableView {{ background: {c['surface']}; border: 0; gridline-color: {c['border']}; selection-background-color: {c['primary_tint']};
  selection-color: {c['text']}; alternate-background-color: {c['surface2']}; }}
QTableWidget::item {{ padding: 4px 10px; border-bottom: 1px solid {c['border']}; }}
QTableWidget::item:hover {{ background: {c['hover']}; }}
QTableWidget::item:selected {{ background: {c['primary_tint']}; color: {c['text']}; }}
QHeaderView {{ background: {c['surface2']}; }}
QHeaderView::section {{ background: {c['surface2']}; color: {c['text2']}; font-weight: 600; font-size: 12px; padding: 0 10px; border: 0;
  border-bottom: 1px solid {c['border_strong']}; min-height: 38px; }}
QTableCornerButton::section {{ background: {c['surface2']}; border: 0; }}

QMenu {{ background: {c['surface']}; border: 1px solid {c['border_strong']}; border-radius: 10px; padding: 6px; }}
QMenu::item {{ padding: 8px 18px; border-radius: 6px; }}
QMenu::item:selected {{ background: {c['primary_tint']}; color: {c['primary']}; }}
QMenu::separator {{ height: 1px; background: {c['border']}; margin: 6px 4px; }}

QMessageBox {{ background: {c['surface']}; }}
QMessageBox QLabel {{ color: {c['text']}; }}
QGroupBox {{ border: 0; }}
QFrame[hline="true"] {{ background: {c['border']}; max-height: 1px; min-height: 1px; border: 0; }}
"""
