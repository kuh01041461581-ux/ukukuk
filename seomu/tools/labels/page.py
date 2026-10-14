# -*- coding: utf-8 -*-
"""우편 라벨지 — 왼쪽 설정 패널 + 오른쪽 큰 미리보기 (전체 화면 편집 도구)"""
from __future__ import annotations

import os

from PySide6.QtCore import Qt, QTimer
from PySide6.QtGui import QColor, QFontDatabase
from PySide6.QtWidgets import (QCheckBox, QColorDialog, QComboBox, QDialog, QDialogButtonBox, QDoubleSpinBox, QFormLayout, QFrame,
                               QHBoxLayout, QLabel, QListWidget, QListWidgetItem, QMenu, QMessageBox, QPlainTextEdit, QScrollArea,
                               QSizePolicy, QSpinBox, QStackedWidget, QVBoxLayout, QWidget)

from ...core import excel
from ...core.worker import run_async
from ...ui.theme import theme
from ...ui.widgets import (Button, DataTable, DropZone, Field, FormGrid, Note, RangeField, Segmented, StepCard, ask_save, label,
                           open_folder)
from . import logic as L
from . import render as R
from .sheetview import SheetView

PANEL_W = 438
SIZE_OPTIONS = [(0.6 + 0.1 * i, f"{8 + i}pt") for i in range(11)]       # 원본의 '글자 크기' 8~18pt (배율 0.6~1.6)
FONT_CANDIDATES = [("Noto Sans KR", "노토 산스 KR (기본)"), ("맑은 고딕", "맑은 고딕"), ("Malgun Gothic", "맑은 고딕"), ("나눔고딕", "나눔고딕"),
                   ("NanumGothic", "나눔고딕"), ("나눔바른고딕", "나눔바른고딕"), ("나눔명조", "나눔명조"), ("NanumMyeongjo", "나눔명조"),
                   ("나눔스퀘어", "나눔스퀘어"), ("함초롬돋움", "함초롬돋움"), ("함초롬바탕", "함초롬바탕"), ("굴림", "굴림"), ("돋움", "돋움"),
                   ("바탕", "바탕"), ("궁서", "궁서"), ("HY헤드라인M", "HY헤드라인M"), ("Arial", "Arial")]


def available_fonts() -> list[tuple[str, str]]:
    fams = set(QFontDatabase.families())
    out, seen = [("Noto Sans KR", "노토 산스 KR (기본)")], {"노토 산스 KR (기본)"}
    for fam, disp in FONT_CANDIDATES[1:]:
        if fam in fams and disp not in seen:
            out.append((fam, disp))
            seen.add(disp)
    return out


def _row(*ws, stretch=None) -> QHBoxLayout:
    l = QHBoxLayout()
    l.setSpacing(8)
    for i, w in enumerate(ws):
        l.addWidget(w, (stretch[i] if stretch else 0))
    return l


class LabelsPage(QWidget):
    """info: registry.ToolInfo   ctx: shell.AppContext"""

    def __init__(self, info, ctx, parent=None):
        super().__init__(parent)
        self.info, self.ctx = info, ctx
        self.sets: dict = dict(ctx.settings.get("labels", {}) or {})
        self.mode = self.sets.get("mode", "addr") if self.sets.get("mode") in ("addr", "gen") else "addr"
        # 주소 라벨 상태
        self.recipients: list[L.Recipient] = []
        self.sheets: dict = {}
        self.table: L.Table | None = None
        self.a_spec = L.ADDRESS_SPECS.get(self.sets.get("a_spec"), L.ADDRESS_SPECS[L.DEFAULT_ADDRESS])
        self.a_style = L.style_from_dict(L.AddressStyle, self.sets.get("a_style"))
        # 일반 라벨 상태
        self.entries: list[L.GeneralEntry] = [L.GeneralEntry("", 1)]
        self.g_spec = L.GENERAL_SPECS.get(self.sets.get("g_spec"), L.GENERAL_SPECS[L.DEFAULT_GENERAL])
        self.g_style = L.style_from_dict(L.GeneralStyle, self.sets.get("g_style"))
        self._g_auto_vertical = False
        self._loading = False
        self.pick = False
        self.doc: R.Doc | None = None
        self.fonts = available_fonts()
        self._busy = False
        self._t_refresh = QTimer(self)
        self._t_refresh.setSingleShot(True)
        self._t_refresh.setInterval(60)
        self._t_refresh.timeout.connect(self._refresh)
        self._t_save = QTimer(self)
        self._t_save.setSingleShot(True)
        self._t_save.setInterval(500)
        self._t_save.timeout.connect(self._save_now)
        self._build()
        self._apply_mode(self.mode, first=True)
        self._refresh()

    # ══════════════ 설정 저장 · 복원 ══════════════
    def _key(self) -> str:
        return ("A:" + self.a_spec.key) if self.mode == "addr" else ("G:" + self.g_spec.key)

    def _spec(self):
        return self.a_spec if self.mode == "addr" else self.g_spec

    def _offsets(self) -> tuple[float, float]:
        v = (self.sets.get("offsets") or {}).get(self._key(), [0.0, 0.0])
        return float(v[0]), float(v[1])

    def _start(self) -> int:
        v = int((self.sets.get("starts") or {}).get(self._key(), 0) or 0)
        return max(0, min(v, self._spec().per_page - 1))

    def _remember(self, group: str, value):
        d = dict(self.sets.get(group) or {})
        if value in (None, 0, [0.0, 0.0]):
            d.pop(self._key(), None)
        else:
            d[self._key()] = value
        self.sets[group] = d

    def _changed(self, save=True):
        if self._loading:
            return
        self._t_refresh.start()
        if save:
            self._t_save.start()

    def _save_now(self):
        self.sets["mode"] = self.mode
        self.sets["a_spec"], self.sets["g_spec"] = self.a_spec.key, self.g_spec.key
        self.sets["a_style"], self.sets["g_style"] = L.style_to_dict(self.a_style), L.style_to_dict(self.g_style)
        self.ctx.settings.set("labels", self.sets)

    # ══════════════ 화면 짓기 ══════════════
    def _build(self):
        root = QHBoxLayout(self)
        root.setContentsMargins(24, 18, 24, 20)
        root.setSpacing(16)
        # ── 왼쪽 ──
        left = QWidget()
        left.setFixedWidth(PANEL_W)
        lv = QVBoxLayout(left)
        lv.setContentsMargins(0, 0, 0, 0)
        lv.setSpacing(10)
        self.mode_seg = Segmented([("addr", "주소 라벨"), ("gen", "일반 라벨")], self.mode)
        self.mode_seg.changed.connect(self._apply_mode)
        lv.addWidget(self.mode_seg)
        self.stack = QStackedWidget()
        self.stack.addWidget(self._scroll(self._build_addr()))
        self.stack.addWidget(self._scroll(self._build_gen()))
        lv.addWidget(self.stack, 1)
        root.addWidget(left)
        # ── 오른쪽 ──
        right = QVBoxLayout()
        right.setSpacing(10)
        right.addLayout(self._build_toolbar())
        self.view = SheetView()
        self.view.cell_clicked.connect(self._cell_clicked)
        self.view.page_changed.connect(self._page_changed)
        self.view.zoom_changed.connect(lambda t: self.zoom_lb.setText(t))
        self.view.canvas.tip_fn = self._tip
        right.addWidget(self.view, 1)
        root.addLayout(right, 1)

    def _scroll(self, inner: QWidget) -> QScrollArea:
        sa = QScrollArea()
        sa.setWidgetResizable(True)
        sa.setFrameShape(QFrame.NoFrame)
        sa.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        sa.setWidget(inner)
        return sa

    def _card(self, v: QVBoxLayout, number, title, desc=None, tag=None) -> StepCard:
        c = StepCard(number, title, desc, tag)
        c.lay.setContentsMargins(20, 16, 20, 20)
        c.lay.setSpacing(10)
        c.body.setSpacing(10)
        v.addWidget(c)
        return c

    def _panel(self) -> tuple[QWidget, QVBoxLayout]:
        w = QWidget()
        v = QVBoxLayout(w)
        v.setContentsMargins(0, 0, 8, 24)
        v.setSpacing(6)
        return w, v

    def _make_style_row(self, st, fam_cb: QComboBox, handler) -> QWidget:
        """굵게·기울임·밑줄 체크 줄"""
        w = QWidget()
        l = QHBoxLayout(w)
        l.setContentsMargins(0, 0, 0, 0)
        l.setSpacing(16)
        cbs = []
        for attr, text in (("bold", "굵게"), ("italic", "기울임"), ("underline", "밑줄")):
            cb = QCheckBox(text)
            cb.setChecked(getattr(st, attr))
            cb.toggled.connect(lambda on, a=attr, s=st: (setattr(s, a, on), handler()))
            l.addWidget(cb)
            cbs.append(cb)
        l.addStretch(1)
        w.cbs = cbs
        return w

    def _font_combo(self, current: str, setter) -> QComboBox:
        cb = QComboBox()
        for fam, disp in self.fonts:
            cb.addItem(disp, fam)
        i = cb.findData(current)
        cb.setCurrentIndex(max(0, i))
        cb.currentIndexChanged.connect(lambda _: (setter(cb.currentData()), self._changed()))
        return cb

    def _color_button(self, get, set_) -> Button:
        b = Button("", "ghost", size="sm")

        def paint():
            col = get()
            b.setText(f"글자색: {col}" if col else "글자색: 기본")

        def pick():
            c = QColorDialog.getColor(QColor(get() or "#000000"), self, "글자색")
            if c.isValid():
                set_(c.name())
                paint()
                self._changed()

        def reset():
            set_("")
            paint()
            self._changed()
        b.clicked.connect(pick)
        b.setContextMenuPolicy(Qt.CustomContextMenu)
        b.customContextMenuRequested.connect(lambda _: reset())
        b.setToolTip("누르면 색을 고르고, 오른쪽 버튼을 누르면 기본색으로 되돌립니다")
        paint()
        b.repaint_label = paint
        return b

    # ───────────── 주소 라벨 패널 ─────────────
    def _build_addr(self) -> QWidget:
        w, v = self._panel()
        # 1 불러오기
        c1 = self._card(v, "1", "주소 목록", "엑셀·CSV 파일에서 받는 분(이름·주소·우편번호)을 불러오거나 직접 붙여 넣습니다.")
        self.drop = DropZone("파일을 놓거나 클릭해서 선택", ".xlsx · .xls · .csv · .txt", [".xlsx", ".xls", ".csv", ".txt"], compact=True, kind="주소 목록")
        self.drop.picked.connect(lambda fs: self.load_file(fs[0]))
        c1.add(self.drop)
        row = _row(*(b := [Button("붙여넣기", "secondary", "pencil", "sm"), Button("한 줄 추가", "ghost", "plus", "sm")]))
        b[0].clicked.connect(self.paste_dialog)
        b[1].clicked.connect(self.add_row)
        row.addStretch(1)
        c1.add_layout(row)
        self.map_box = QWidget()
        mb = QVBoxLayout(self.map_box)
        mb.setContentsMargins(0, 0, 0, 0)
        mb.setSpacing(8)
        self.sheet_cb = QComboBox()
        self.name_cb, self.addr_cb, self.zip_cb = QComboBox(), QComboBox(), QComboBox()
        g = FormGrid(2)
        g.g.setVerticalSpacing(8)
        g.g.setHorizontalSpacing(10)
        self.sheet_f = Field("시트", self.sheet_cb)
        g.add(self.sheet_f, 2)
        g.add(Field("이름 열", self.name_cb))
        g.add(Field("주소 열", self.addr_cb))
        g.add(Field("우편번호 열", self.zip_cb), 2)
        mb.addWidget(g)
        self.sheet_cb.currentIndexChanged.connect(self._sheet_changed)
        self.reimport = Button("이 열 설정으로 다시 가져오기", "secondary", "refresh", "sm")
        self.reimport.clicked.connect(lambda: self._import_table(replace=True))
        mb.addWidget(self.reimport)
        self.map_note = Note("", "info")
        mb.addWidget(self.map_note)
        self.map_box.hide()
        c1.add(self.map_box)
        # 목록
        self.rcount = label("", "small")
        c1.add(self.rcount)
        self.rtable = DataTable([("#", 40, "c"), ("이름", 98, "l", True), ("주소", None, "l", True), ("우편번호", 78, "c", True)], row_height=34, max_rows=9)
        t = self.rtable.table
        t.setSelectionMode(t.SelectionMode.ExtendedSelection)
        t.setSelectionBehavior(t.SelectionBehavior.SelectRows)
        t.setFocusPolicy(Qt.ClickFocus)
        self.rtable.cell_edited.connect(self._cell_edited)
        c1.add(self.rtable)
        r2 = _row(*(b2 := [Button("선택 삭제", "ghost", "trash", "sm"), Button("모두 비우기", "ghost", None, "sm"), Button("우편번호 채우기", "secondary", "map", "sm")]))
        b2[0].clicked.connect(self.delete_selected)
        b2[1].clicked.connect(self.clear_all)
        b2[2].clicked.connect(self.fill_zips)
        self.zip_btn = b2[2]
        c1.add_layout(r2)
        self.zip_note = label("", "small", wrap=True)
        c1.add(self.zip_note)
        # 2 라벨지
        c2 = self._card(v, "2", "라벨지 규격 · 시작 칸")
        self.a_maker, self.a_specs = self._spec_combos(c2, L.ADDRESS_SPECS, self.a_spec, self._a_spec_chosen)
        self.a_info = label("", "small", wrap=True)
        c2.add(self.a_info)
        self.a_start = self._start_spin(c2)
        # 3 서식
        c3 = self._card(v, "3", "글꼴 · 서식")
        st = self.a_style
        self.a_font = self._font_combo(st.family, lambda f: setattr(st, "family", f))
        self.a_size = QComboBox()
        for m, t_ in SIZE_OPTIONS:
            self.a_size.addItem(t_, m)
        self.a_size.setCurrentIndex(min(range(len(SIZE_OPTIONS)), key=lambda i: abs(SIZE_OPTIONS[i][0] - st.mult)))
        self.a_size.currentIndexChanged.connect(lambda _: (setattr(st, "mult", self.a_size.currentData()), self._changed()))
        g3 = FormGrid(2)
        g3.g.setHorizontalSpacing(10)
        g3.g.setVerticalSpacing(8)
        g3.add(Field("글꼴", self.a_font))
        g3.add(Field("글자 크기", self.a_size))
        c3.add(g3)
        self.a_styles = self._make_style_row(st, self.a_font, self._changed)
        c3.add(self.a_styles)
        self.a_align = Segmented([("default", "기본"), ("left", "왼쪽"), ("center", "가운데"), ("right", "오른쪽")], st.align)
        self.a_align.changed.connect(lambda val: (setattr(st, "align", val), self._changed()))
        c3.add(Field("정렬", self.a_align, "기본은 주소 왼쪽 · 이름 오른쪽입니다."))
        self.a_color = self._color_button(lambda: st.color, lambda c_: setattr(st, "color", c_))
        c3.add(self.a_color)
        for attr, text in (("honorific", "이름 뒤에 ‘귀하’ 붙이기"), ("show_zip", "우편번호 칸(빨간 5칸) 그리기"),
                           ("autoshrink", "긴 주소는 글자를 줄여 칸 안에 맞추기"), ("watermark", "캐릭터 워터마크 넣기")):
            cb = QCheckBox(text)
            cb.setChecked(getattr(st, attr))
            cb.toggled.connect(lambda on, a=attr: (setattr(st, a, on), self._changed()))
            c3.add(cb)
            setattr(self, "a_cb_" + attr, cb)
        self.a_wm = RangeField(2, 30, 1, int(round(st.wm_opacity * 100)), "%")
        self.a_wm.changed.connect(lambda val: (setattr(st, "wm_opacity", val / 100), self._changed()))
        c3.add(Field("워터마크 진하기", self.a_wm))
        # 4 출력
        self.a_out = self._build_output(v, "4")
        v.addStretch(1)
        return w

    # ───────────── 일반 라벨 패널 ─────────────
    def _build_gen(self) -> QWidget:
        w, v = self._panel()
        c1 = self._card(v, "1", "라벨지 규격 · 시작 칸", "제조사별 라벨지 102종 — 같은 크기 칸이 반복되는 규격입니다.")
        self.g_maker, self.g_specs = self._spec_combos(c1, L.GENERAL_SPECS, self.g_spec, self._g_spec_chosen)
        self.g_info = label("", "small", wrap=True)
        c1.add(self.g_info)
        self.g_start = self._start_spin(c1)
        # 내용
        c2 = self._card(v, "2", "라벨 내용", "항목마다 글(여러 줄 가능)과 반복 횟수를 정하면 칸에 차례로 채워집니다. 미리보기의 칸을 누르면 그 항목이 선택됩니다.")
        self.elist = QListWidget()
        self.elist.setFixedHeight(128)
        self.elist.currentRowChanged.connect(self._entry_selected)
        c2.add(self.elist)
        self.etext = QPlainTextEdit()
        self.etext.setPlaceholderText("여기에 라벨에 찍을 글을 입력하세요 (Enter 로 줄바꿈)")
        self.etext.setFixedHeight(96)
        self.etext.textChanged.connect(self._entry_text_changed)
        c2.add(self.etext)
        self.erep = QSpinBox()
        self.erep.setRange(1, 9999)
        self.erep.valueChanged.connect(self._entry_repeat_changed)
        fill = Button("한 장 채우기", "secondary", size="sm")
        fill.setToolTip("선택한 항목의 반복 횟수를 이 쪽의 남은 칸 수에 맞춥니다")
        fill.clicked.connect(self.fill_sheet)
        c2.add_layout(_row(label("반복 횟수"), self.erep, fill, stretch=[0, 0, 0]))
        bs = [Button("항목 추가", "ghost", "plus", "sm"), Button("복제", "ghost", None, "sm"), Button("삭제", "ghost", "trash", "sm"),
              Button("엑셀에서 가져오기", "secondary", "excel", "sm")]
        bs[0].clicked.connect(lambda: self.entry_add())
        bs[1].clicked.connect(self.entry_dup)
        bs[2].clicked.connect(self.entry_del)
        bs[3].clicked.connect(self.entry_import)
        c2.add_layout(_row(*bs[:3]))
        c2.add(bs[3])
        self.g_note = label("", "small", wrap=True)
        c2.add(self.g_note)
        # 서식
        c3 = self._card(v, "3", "글꼴 · 서식")
        st = self.g_style
        self.g_font = self._font_combo(st.family, lambda f: setattr(st, "family", f))
        self.g_size = QDoubleSpinBox()
        self.g_size.setRange(4, 200)
        self.g_size.setDecimals(1)
        self.g_size.setSingleStep(1)
        self.g_size.setSuffix(" pt")
        self.g_size.setValue(st.size_pt)
        self.g_size.valueChanged.connect(lambda x: (setattr(st, "size_pt", x), self._changed()))
        g3 = FormGrid(2)
        g3.g.setHorizontalSpacing(10)
        g3.g.setVerticalSpacing(8)
        g3.add(Field("글꼴", self.g_font))
        g3.add(Field("글자 크기", self.g_size))
        self.g_lh = QDoubleSpinBox()
        self.g_lh.setRange(0.8, 3.0)
        self.g_lh.setSingleStep(0.05)
        self.g_lh.setDecimals(2)
        self.g_lh.setValue(st.line_height)
        self.g_lh.valueChanged.connect(lambda x: (setattr(st, "line_height", x), self._changed()))
        self.g_ls = QDoubleSpinBox()
        self.g_ls.setRange(-2.0, 20.0)
        self.g_ls.setSingleStep(0.5)
        self.g_ls.setSuffix(" pt")
        self.g_ls.setValue(st.letter_spacing)
        self.g_ls.valueChanged.connect(lambda x: (setattr(st, "letter_spacing", x), self._changed()))
        g3.add(Field("줄 간격", self.g_lh))
        g3.add(Field("자간", self.g_ls))
        c3.add(g3)
        self.g_styles = self._make_style_row(st, self.g_font, self._changed)
        c3.add(self.g_styles)
        self.g_ah = Segmented([("left", "왼쪽"), ("center", "가운데"), ("right", "오른쪽")], st.align_h)
        self.g_av = Segmented([("top", "위"), ("middle", "중앙"), ("bottom", "아래")], st.align_v)
        self.g_ah.changed.connect(lambda val: (setattr(st, "align_h", val), self._changed()))
        self.g_av.changed.connect(lambda val: (setattr(st, "align_v", val), self._changed()))
        c3.add(Field("가로 위치", self.g_ah))
        c3.add(Field("세로 위치", self.g_av))
        self.g_color = self._color_button(lambda: st.color if st.color != "#000000" else "", lambda c_: setattr(st, "color", c_ or "#000000"))
        c3.add(self.g_color)
        self.g_cb_autofit = QCheckBox("칸보다 길면 글자를 줄여 맞추기")
        self.g_cb_autofit.setChecked(st.autofit)
        self.g_cb_autofit.toggled.connect(lambda on: (setattr(st, "autofit", on), self._changed()))
        c3.add(self.g_cb_autofit)
        self.g_cb_vertical = QCheckBox("세로쓰기 (글자를 위에서 아래로 한 글자씩)")
        self.g_cb_vertical.setChecked(st.vertical)
        self.g_cb_vertical.toggled.connect(lambda on: (setattr(st, "vertical", on), setattr(self, "_g_auto_vertical", False), self._changed()))
        c3.add(self.g_cb_vertical)
        self.g_out = self._build_output(v, "4")
        v.addStretch(1)
        self._refill_entries()
        return w

    # ───────────── 공통 부품 ─────────────
    def _spec_combos(self, card: StepCard, specs: dict, current, chosen):
        groups = L.by_maker(specs)
        mk, sp = QComboBox(), QComboBox()
        for m in groups:
            mk.addItem(m, m)
        card.add(Field("제조사", mk))
        card.add(Field("규격", sp))

        def fill_specs(maker, select_key=None):
            sp.blockSignals(True)
            sp.clear()
            for s in groups.get(maker, []):
                sp.addItem(s.title if isinstance(s, L.GeneralSpec) else f"{s.model}  ·  {s.w:g}×{s.h:g}mm · {s.per_page}칸", s.key)
            if select_key:
                sp.setCurrentIndex(max(0, sp.findData(select_key)))
            sp.blockSignals(False)
        mk.setCurrentIndex(max(0, mk.findData(current.maker)))
        fill_specs(current.maker, current.key)
        mk.currentIndexChanged.connect(lambda _: (fill_specs(mk.currentData()), chosen(sp.currentData())))
        sp.currentIndexChanged.connect(lambda _: chosen(sp.currentData()))
        return mk, sp

    def _start_spin(self, card: StepCard) -> QSpinBox:
        sb = QSpinBox()
        sb.setRange(1, 999)
        sb.setSuffix(" 번째 칸부터")
        sb.valueChanged.connect(self._start_changed)
        pick = Button("미리보기에서 고르기", "secondary", "grid", "sm")
        pick.setCheckable(True)
        pick.clicked.connect(self._pick_toggle)
        card.add(Field("시작 위치 (쓰고 남은 라벨지를 이어 쓸 때)", sb, "앞쪽 칸은 비워 둔 채 인쇄됩니다. 규격마다 기억합니다."))
        card.add(pick)
        if not hasattr(self, "pick_btns"):
            self.pick_btns = []
        self.pick_btns.append(pick)
        return sb

    def _build_output(self, v: QVBoxLayout, number: str):
        c = self._card(v, number, "인쇄 위치 보정 · 내보내기", "프린터마다 급지 위치가 달라 라벨이 밀리면 아래 값으로 맞춥니다.")
        up, lf = QDoubleSpinBox(), QDoubleSpinBox()
        for s in (up, lf):
            s.setRange(-10, 10)
            s.setSingleStep(0.25)
            s.setDecimals(2)
            s.setSuffix(" mm")
        up.valueChanged.connect(self._offset_changed)
        lf.valueChanged.connect(self._offset_changed)
        g = FormGrid(2)
        g.g.setHorizontalSpacing(10)
        g.add(Field("아래로 (+) / 위로 (−)", up))
        g.add(Field("오른쪽 (+) / 왼쪽 (−)", lf))
        c.add(g)
        reset = Button("보정 초기화", "ghost", "refresh", "sm")
        reset.clicked.connect(lambda: (up.setValue(0), lf.setValue(0)))
        c.add(reset)
        pdf = Button("PDF 로 저장", "primary", "down")
        pdf.clicked.connect(self.save_pdf)
        prn = Button("인쇄", "secondary", "printer")
        prn.clicked.connect(self.print_dialog)
        c.add_layout(_row(pdf, prn, stretch=[1, 1]))
        test = Button("정렬 테스트지 (칸 테두리만)", "ghost", "grid", "sm")
        menu = QMenu(test)
        menu.addAction("테스트지 PDF 로 저장", lambda: self.save_pdf(test=True))
        menu.addAction("테스트지 인쇄", lambda: self.print_dialog(test=True))
        test.setMenu(menu)
        c.add(test)
        c.add(label("먼저 일반 A4 에 ‘정렬 테스트지’를 뽑아 라벨지와 겹쳐 빛에 비춰 보고, 어긋난 만큼 위 값을 조정하세요. 인쇄 창에서는 용지 A4 · 배율 100%(실제 크기)를 유지하세요.", "small", wrap=True))
        return up, lf, c

    def _build_toolbar(self) -> QHBoxLayout:
        bar = QHBoxLayout()
        bar.setSpacing(8)
        self.prev_btn = Button("", "ghost", "chev_left", "icon")
        self.next_btn = Button("", "ghost", "chev_right", "icon")
        self.prev_btn.clicked.connect(lambda: self.view.go_page(self.view.current_page() - 1))
        self.next_btn.clicked.connect(lambda: self.view.go_page(self.view.current_page() + 1))
        self.page_lb = QLabel("1 / 1 쪽")
        self.page_lb.setMinimumWidth(62)
        self.page_lb.setAlignment(Qt.AlignCenter)
        zo, zi = Button("축소", "ghost", size="sm"), Button("확대", "ghost", size="sm")
        zo.clicked.connect(lambda: self.view.zoom_step(1 / 1.2))
        zi.clicked.connect(lambda: self.view.zoom_step(1.2))
        fit = Button("가로 맞춤", "ghost", size="sm")
        fit.clicked.connect(lambda: self.view.set_zoom(None))
        fit1 = Button("한 쪽", "ghost", size="sm")
        fit1.clicked.connect(lambda: self.view.set_zoom("page"))
        self.zoom_lb = QLabel("가로 맞춤")
        self.zoom_lb.setMinimumWidth(62)
        self.zoom_lb.setAlignment(Qt.AlignCenter)
        self.status_lb = label("", "small")
        self.status_lb.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        for w in (self.prev_btn, self.page_lb, self.next_btn):
            bar.addWidget(w)
        bar.addSpacing(14)
        for w in (zo, self.zoom_lb, zi, fit, fit1):
            bar.addWidget(w)
        bar.addStretch(1)
        bar.addWidget(self.status_lb)
        return bar

    # ══════════════ 모드 · 규격 ══════════════
    def _apply_mode(self, mode: str, first=False):
        self.mode = mode
        self.mode_seg.set_value(mode)
        self.stack.setCurrentIndex(0 if mode == "addr" else 1)
        self._set_pick(False)
        self._sync_spec_widgets()
        if not first:
            self._changed()

    def _sync_spec_widgets(self):
        """규격이 바뀌었을 때 시작 칸·보정값·안내문을 다시 채운다"""
        self._loading = True
        spec = self._spec()
        up, lf, _ = self.a_out if self.mode == "addr" else self.g_out
        dy, dx = self._offsets()
        up.setValue(dy)
        lf.setValue(dx)
        sb = self.a_start if self.mode == "addr" else self.g_start
        sb.setMaximum(spec.per_page)
        sb.setValue(self._start() + 1)
        (self.a_info if self.mode == "addr" else self.g_info).setText(spec.info())
        self._loading = False

    def _a_spec_chosen(self, key):
        if key in L.ADDRESS_SPECS and key != self.a_spec.key:
            self.a_spec = L.ADDRESS_SPECS[key]
            self._sync_spec_widgets()
            self._changed()

    def _g_spec_chosen(self, key):
        if key in L.GENERAL_SPECS and key != self.g_spec.key:
            self.g_spec = L.GENERAL_SPECS[key]
            if self.g_spec.vertical and not self.g_style.vertical:
                self._g_auto_vertical = True
                self.g_cb_vertical.setChecked(True)
            elif not self.g_spec.vertical and self._g_auto_vertical:
                self._g_auto_vertical = False
                self.g_cb_vertical.setChecked(False)
            self._sync_spec_widgets()
            self._changed()

    def _start_changed(self, n: int):
        if self._loading:
            return
        self._remember("starts", n - 1)
        self._changed()

    def _offset_changed(self):
        if self._loading:
            return
        up, lf, _ = self.a_out if self.mode == "addr" else self.g_out
        self._remember("offsets", [round(up.value(), 2), round(lf.value(), 2)])
        self._changed()

    def _set_pick(self, on: bool):
        self.pick = on
        for b in getattr(self, "pick_btns", []):
            b.setChecked(on)
        self.view.canvas.pick_mode = on if hasattr(self, "view") else False

    def _pick_toggle(self):
        self._set_pick(not self.pick)
        if self.pick:
            self.view.go_page(0)
            self.ctx.toast("미리보기 첫 쪽에서 시작할 칸을 눌러 주세요")

    # ══════════════ 미리보기 ══════════════
    def _make_doc(self, test=False) -> R.Doc:
        dy, dx = self._offsets()
        start = self._start()
        if self.mode == "addr":
            if test:
                return R.GridTestDoc(self.a_spec.geometry(dy, dx))
            return R.AddressDoc(self.a_spec, self.recipients, self.a_style, start, dy, dx)
        if test:
            return R.GridTestDoc(self.g_spec.geometry(dy, dx))
        return R.GeneralDoc(self.g_spec, L.expand_entries(self.entries), self.g_style, start, dy, dx)

    def _refresh(self):
        self.doc = self._make_doc()
        empty = ""
        if self.mode == "addr" and not self.recipients:
            empty = "왼쪽에서 주소 목록을 불러오면\n라벨이 이 위치에 나타납니다"
        elif self.mode == "gen" and not any(e.text.strip() for e in self.entries):
            empty = "왼쪽에서 라벨 내용을 입력하면\n칸에 채워진 모습이 나타납니다"
        self.view.set_doc(self.doc, empty)
        n = len(self.recipients) if self.mode == "addr" else len(L.expand_entries(self.entries))
        spec = self._spec()
        self.status_lb.setText(f"라벨 {n:,}장 · {self.doc.page_count()}쪽 · {spec.per_page}칸/쪽" + (f" · {self._start()}칸 건너뜀" if self._start() else ""))
        self._mark_selection()

    def _page_changed(self, cur: int, total: int):
        self.page_lb.setText(f"{cur + 1} / {total} 쪽")
        self.prev_btn.setEnabled(cur > 0)
        self.next_btn.setEnabled(cur < total - 1)

    def _tip(self, page: int, cell: int):
        d = self.doc
        if d is None:
            return ""
        if self.pick:
            return f"{cell + 1}번째 칸부터 시작" if page == 0 else "시작 칸은 첫 쪽에서만 고를 수 있어요"
        if isinstance(d, R.AddressDoc):
            k = d.recipient_index(page, cell)
            if k is not None:
                r = self.recipients[k]
                return f"#{k + 1}  {r.name}\n{r.addr}"
        elif isinstance(d, R.GeneralDoc):
            k = d.entry_index(page, cell)
            if k is not None:
                return f"{page * d.geom.per_page + cell + 1}번째 칸"
        return ""

    def _cell_clicked(self, page: int, cell: int):
        if self.pick:
            if page != 0:
                self.ctx.toast("시작 칸은 첫 쪽에서만 고를 수 있어요")
                return
            sb = self.a_start if self.mode == "addr" else self.g_start
            sb.setValue(cell + 1)
            self._set_pick(False)
            return
        d = self.doc
        if isinstance(d, R.AddressDoc):
            k = d.recipient_index(page, cell)
            if k is not None:
                self.rtable.table.selectRow(k)
                self.rtable.table.scrollToItem(self.rtable.table.item(k, 1))
        elif isinstance(d, R.GeneralDoc):
            k = d.entry_index(page, cell)
            if k is not None:
                # 칸 번호 → 항목: 반복을 펼친 순서와 같다
                n = 0
                for ei, e in enumerate(self.entries):
                    if n + e.repeat > k:
                        self.elist.setCurrentRow(ei)
                        break
                    n += e.repeat
        self._mark_selection()

    def _mark_selection(self):
        marks = {}
        d = self.doc
        col = QColor(79, 93, 240, 40)
        if isinstance(d, R.AddressDoc):
            sel = {i.row() for i in self.rtable.table.selectionModel().selectedRows()} if self.rtable.table.selectionModel() else set()
            for pg in range(d.page_count()):
                for c in range(d.geom.per_page):
                    if d.recipient_index(pg, c) in sel:
                        marks[(pg, c)] = col
        elif isinstance(d, R.GeneralDoc):
            ei = self.elist.currentRow()
            if ei >= 0:
                lo = sum(e.repeat for e in self.entries[:ei])
                hi = lo + self.entries[ei].repeat
                for pg in range(d.page_count()):
                    for c in range(d.geom.per_page):
                        k = d.entry_index(pg, c)
                        if k is not None and lo <= k < hi:
                            marks[(pg, c)] = col
        self.view.set_marks(marks)

    # ══════════════ 주소 목록 ══════════════
    def load_file(self, path: str):
        ext = os.path.splitext(path)[1].lower()
        try:
            if ext == ".txt":
                raw = open(path, "rb").read()
                try:
                    text = raw.decode("utf-8-sig")
                except UnicodeDecodeError:
                    text = raw.decode("cp949", "replace")
                recs = L.parse_text(text)
                self._apply_new(recs, f"{os.path.basename(path)} — 글에서 이름·주소를 추측했습니다. 표에서 잘못된 곳을 고쳐 주세요.")
                return
            if ext in (".csv", ".tsv"):
                self.sheets = {"CSV": L.read_csv_rows(path)}
            else:
                self.sheets = excel.read_sheets(path)
        except Exception as e:  # noqa
            self.ctx.toast(f"파일을 읽지 못했습니다 — {type(e).__name__}")
            return
        self.sheets = {k: v for k, v in self.sheets.items() if any(any(str(c).strip() for c in r) for r in v)}
        if not self.sheets:
            self.ctx.toast("파일에 데이터가 없습니다")
            return
        self._file = os.path.basename(path)
        self.sheet_cb.blockSignals(True)
        self.sheet_cb.clear()
        for k in self.sheets:
            self.sheet_cb.addItem(k)
        self.sheet_cb.blockSignals(False)
        self.sheet_f.setVisible(len(self.sheets) > 1)
        self._sheet_changed(initial=True)

    def _sheet_changed(self, *_, initial=False):
        name = self.sheet_cb.currentText()
        if name not in self.sheets:
            return
        self.table = L.parse_table(self.sheets[name])
        labels = self.table.labels()
        for cb, cur, none in ((self.name_cb, self.table.name_col, False), (self.addr_cb, self.table.addr_col, False), (self.zip_cb, self.table.zip_col, True)):
            cb.clear()
            if none:
                cb.addItem("(사용 안 함)", None)
            for i, t in enumerate(labels):
                cb.addItem(t, i)
            cb.setCurrentIndex(max(0, cb.findData(cur)) if (cur is not None) else 0)
        self.map_box.show()
        auto = all(c is not None for c in (self.table.name_col, self.table.addr_col))
        self.map_note.set_text(f"<b>{self._file}</b> · 데이터 {len(self.table.rows):,}줄 — 열을 자동으로 인식했습니다. 다르면 고른 뒤 ‘다시 가져오기’를 누르세요." if auto
                               else "열을 자동으로 인식하지 못했습니다. 이름·주소 열을 직접 골라 주세요.", "info" if auto else "warn")
        if initial:
            self._import_table(replace=None)

    def _import_table(self, replace):
        if not self.table:
            return
        recs = self.table.recipients(self.name_cb.currentData(), self.addr_cb.currentData(), self.zip_cb.currentData())
        if not recs:
            self.ctx.toast("가져올 이름·주소가 없습니다 — 열 선택을 확인해 주세요")
            return
        self._apply_new(recs, None, replace)

    def _apply_new(self, recs: list[L.Recipient], msg: str | None, replace=None):
        if replace is None and self.recipients:
            box = QMessageBox(self)
            box.setWindowTitle("주소 목록")
            box.setText(f"이미 {len(self.recipients):,}건이 있습니다. 새로 가져온 {len(recs):,}건을 어떻게 할까요?")
            add = box.addButton("뒤에 추가", QMessageBox.AcceptRole)
            rep = box.addButton("바꾸기", QMessageBox.DestructiveRole)
            box.addButton("취소", QMessageBox.RejectRole)
            box.exec()
            if box.clickedButton() is add:
                replace = False
            elif box.clickedButton() is rep:
                replace = True
            else:
                return
        self.recipients = (recs if replace in (True, None) else self.recipients + recs)
        if msg:
            self.map_box.show()
            self.map_note.set_text(msg, "info")
        self._fill_table()
        self.ctx.toast(f"{len(recs):,}건을 가져왔습니다")
        self._changed(save=False)

    def _fill_table(self):
        self._loading = True
        self.rtable.set_rows([[i + 1, r.name, r.addr, r.zip] for i, r in enumerate(self.recipients)])
        self._loading = False
        nz = sum(1 for r in self.recipients if r.zip)
        self.rcount.setText(f"받는 분 {len(self.recipients):,}명 · 우편번호 {nz:,}건 입력됨" if self.recipients else "아직 불러온 주소가 없습니다.")

    def _cell_edited(self, r: int, c: int, text: str):
        if r >= len(self.recipients):
            return
        rec = self.recipients[r]
        if c == 1:
            rec.name = text.strip()
        elif c == 2:
            rec.addr = text.strip()
        elif c == 3:
            rec.zip = L.norm_zip(text)
            self.rtable.set_cell(r, 3, rec.zip)
        self._fill_count()
        self._changed(save=False)

    def _fill_count(self):
        nz = sum(1 for r in self.recipients if r.zip)
        self.rcount.setText(f"받는 분 {len(self.recipients):,}명 · 우편번호 {nz:,}건 입력됨")

    def add_row(self):
        self.recipients.append(L.Recipient())
        self._fill_table()
        t = self.rtable.table
        t.setCurrentCell(len(self.recipients) - 1, 1)
        t.scrollToBottom()
        t.editItem(t.item(len(self.recipients) - 1, 1))
        self._changed(save=False)

    def delete_selected(self):
        rows = sorted({i.row() for i in self.rtable.table.selectionModel().selectedRows()}, reverse=True)
        if not rows:
            self.ctx.toast("지울 줄을 먼저 선택해 주세요")
            return
        for r in rows:
            if r < len(self.recipients):
                del self.recipients[r]
        self._fill_table()
        self._changed(save=False)

    def clear_all(self):
        if self.recipients and QMessageBox.question(self, "모두 비우기", f"받는 분 {len(self.recipients):,}명을 모두 지울까요?") != QMessageBox.Yes:
            return
        self.recipients = []
        self._fill_table()
        self._changed(save=False)

    def paste_dialog(self):
        dlg = QDialog(self)
        dlg.setWindowTitle("주소 붙여넣기")
        dlg.resize(560, 380)
        v = QVBoxLayout(dlg)
        v.addWidget(label("이름과 주소를 한 줄에 하나씩 붙여 넣으세요. 탭·공백 2칸·쉼표로 나뉜 목록, 빈 줄로 나뉜 묶음, 이름/주소가 번갈아 나온 줄도 알아봅니다.", "desc", wrap=True))
        te = QPlainTextEdit()
        te.setPlaceholderText("홍길동\t전북특별자치도 익산시 인북로 32\n김철수\t서울특별시 종로구 세종대로 1")
        v.addWidget(te, 1)
        bb = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        bb.button(QDialogButtonBox.Ok).setText("가져오기")
        bb.button(QDialogButtonBox.Cancel).setText("취소")
        bb.accepted.connect(dlg.accept)
        bb.rejected.connect(dlg.reject)
        v.addWidget(bb)
        if dlg.exec() == QDialog.Accepted:
            recs = L.parse_text(te.toPlainText())
            if recs:
                self._apply_new(recs, "붙여 넣은 글에서 이름·주소를 추측했습니다. 표에서 잘못된 곳을 고쳐 주세요.")
            else:
                self.ctx.toast("가져올 내용이 없습니다")

    def fill_zips(self):
        todo = [r for r in self.recipients if not r.zip and r.addr.strip()]
        if not todo:
            self.ctx.toast("우편번호가 빈 줄이 없습니다")
            return
        if self._busy:
            return
        self._busy = True
        self.zip_btn.setEnabled(False)
        key = self.ctx.key("kakao") or L.KAKAO_KEY
        self.zip_note.setText(f"우편번호 조회 중… 0 / {len(todo):,}")

        def job(items, key, progress=None):
            from concurrent.futures import ThreadPoolExecutor, as_completed
            res, done = {}, 0
            addrs = {}
            for r in items:
                addrs.setdefault(r.addr.strip(), None)
            with ThreadPoolExecutor(8) as ex:
                futs = {ex.submit(L.lookup_zip, a, key): a for a in addrs}
                for f in as_completed(futs):
                    addrs[futs[f]] = f.result()
                    done += 1
                    progress(int(done * 100 / len(futs)), f"{done:,} / {len(futs):,}")
            return addrs

        def fin(addrs):
            ok = 0
            for r in todo:
                z = addrs.get(r.addr.strip())
                if z:
                    r.zip = z
                    ok += 1
            self._busy = False
            self.zip_btn.setEnabled(True)
            self.zip_note.setText(f"우편번호 {ok:,}건을 채웠습니다" + (f" (찾지 못한 {len(todo) - ok:,}건은 표에서 직접 입력하세요)" if ok < len(todo) else ""))
            self._fill_table()
            self._changed(save=False)

        def err(msg):
            self._busy = False
            self.zip_btn.setEnabled(True)
            self.zip_note.setText("우편번호를 조회하지 못했습니다 — 인터넷 연결을 확인해 주세요.")
        run_async(job, todo, key, on_done=fin, on_error=err, on_progress=lambda p, t: self.zip_note.setText(f"우편번호 조회 중… {t}"))

    # ══════════════ 일반 라벨 항목 ══════════════
    def _entry_title(self, i: int) -> str:
        e = self.entries[i]
        first = (e.text.strip().splitlines() or ["(비어 있음)"])[0]
        return f"{i + 1}. {first[:22]}{'…' if len(first) > 22 else ''}   ×{e.repeat}"

    def _refill_entries(self, select: int = 0):
        self._loading = True
        self.elist.clear()
        for i in range(len(self.entries)):
            self.elist.addItem(QListWidgetItem(self._entry_title(i)))
        self._loading = False
        self.elist.setCurrentRow(max(0, min(select, len(self.entries) - 1)))
        self._update_note()

    def _update_note(self):
        total = sum(e.repeat for e in self.entries if e.text.strip())
        self.g_note.setText(f"총 {total:,}장 · {self.doc.page_count() if self.doc else 1}쪽 (한 쪽 {self.g_spec.per_page}칸)")

    def _entry_selected(self, row: int):
        if row < 0 or row >= len(self.entries):
            return
        self._loading = True
        self.etext.setPlainText(self.entries[row].text)
        self.erep.setValue(self.entries[row].repeat)
        self._loading = False
        if self.doc:
            self._mark_selection()

    def _entry_text_changed(self):
        r = self.elist.currentRow()
        if self._loading or r < 0:
            return
        self.entries[r].text = self.etext.toPlainText()
        self.elist.item(r).setText(self._entry_title(r))
        self._changed(save=False)
        self._update_note()

    def _entry_repeat_changed(self, v: int):
        r = self.elist.currentRow()
        if self._loading or r < 0:
            return
        self.entries[r].repeat = v
        self.elist.item(r).setText(self._entry_title(r))
        self._changed(save=False)
        self._update_note()

    def entry_add(self, text: str = ""):
        self.entries.append(L.GeneralEntry(text, 1))
        self._refill_entries(len(self.entries) - 1)
        self.etext.setFocus()
        self._changed(save=False)

    def entry_dup(self):
        r = self.elist.currentRow()
        if r >= 0:
            e = self.entries[r]
            self.entries.insert(r + 1, L.GeneralEntry(e.text, e.repeat))
            self._refill_entries(r + 1)
            self._changed(save=False)

    def entry_del(self):
        r = self.elist.currentRow()
        if r >= 0:
            del self.entries[r]
            if not self.entries:
                self.entries.append(L.GeneralEntry("", 1))
            self._refill_entries(r)
            self._changed(save=False)

    def fill_sheet(self):
        r = self.elist.currentRow()
        if r < 0:
            return
        spec = self.g_spec
        others = sum(e.repeat for i, e in enumerate(self.entries) if i != r)
        per = spec.per_page
        rem = (-(self._start() + others)) % per          # 마지막 쪽에 남은 칸
        self.erep.setValue(rem or per)

    def entry_import(self):
        from PySide6.QtWidgets import QFileDialog
        path, _ = QFileDialog.getOpenFileName(self, "목록 파일 선택", "", "엑셀·CSV (*.xlsx *.xls *.csv *.txt)")
        if not path:
            return
        try:
            ext = os.path.splitext(path)[1].lower()
            if ext in (".csv", ".txt"):
                sheets = {"CSV": L.read_csv_rows(path)}
            else:
                sheets = excel.read_sheets(path)
        except Exception as e:  # noqa
            self.ctx.toast(f"파일을 읽지 못했습니다 — {type(e).__name__}")
            return
        vals = self._column_dialog(sheets)
        if vals:
            if len(self.entries) == 1 and not self.entries[0].text.strip():
                self.entries = []
            self.entries.extend(L.GeneralEntry(v, 1) for v in vals)
            self._refill_entries(len(self.entries) - len(vals))
            self.ctx.toast(f"{len(vals):,}개 항목을 가져왔습니다")
            self._changed(save=False)

    def _column_dialog(self, sheets: dict) -> list[str]:
        dlg = QDialog(self)
        dlg.setWindowTitle("열 선택")
        dlg.resize(440, 300)
        v = QVBoxLayout(dlg)
        v.addWidget(label("라벨에 찍을 글이 들어 있는 열을 고르세요. 한 줄이 한 항목이 됩니다.", "desc", wrap=True))
        sh, col, hdr = QComboBox(), QComboBox(), QCheckBox("첫 줄은 제목이라 제외")
        sh.addItems(list(sheets))
        info = label("", "small")

        def rows():
            return sheets[sh.currentText()]

        def fill_cols():
            n = max((len(r) for r in rows()), default=0)
            col.clear()
            for i in range(n):
                head = L.cell_text(rows()[0][i]) if rows() and i < len(rows()[0]) else ""
                col.addItem(f"{head}  ({L.col_letter(i)}열)" if head else f"{L.col_letter(i)}열", i)
            hdr.setChecked(n > 1)
            vals_()

        def vals_():
            ci = col.currentData() or 0
            rs = rows()[1:] if hdr.isChecked() else rows()
            out = [L.cell_text(r[ci]) for r in rs if ci < len(r)]
            out = [x for x in out if x]
            info.setText(f"{len(out):,}개 항목 · 예: {' / '.join(out[:3])}")
            return out
        sh.currentIndexChanged.connect(lambda _: fill_cols())
        col.currentIndexChanged.connect(lambda _: vals_())
        hdr.toggled.connect(lambda _: vals_())
        f = QFormLayout()
        f.addRow("시트", sh)
        f.addRow("열", col)
        v.addLayout(f)
        v.addWidget(hdr)
        v.addWidget(info)
        bb = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        bb.button(QDialogButtonBox.Ok).setText("가져오기")
        bb.button(QDialogButtonBox.Cancel).setText("취소")
        bb.accepted.connect(dlg.accept)
        bb.rejected.connect(dlg.reject)
        v.addWidget(bb)
        fill_cols()
        return vals_() if dlg.exec() == QDialog.Accepted else []

    # ══════════════ 내보내기 ══════════════
    def _confirm_empty(self, test: bool) -> bool:
        if test:
            return True
        n = len(self.recipients) if self.mode == "addr" else len(L.expand_entries(self.entries))
        if n == 0:
            self.ctx.toast("찍을 내용이 없습니다 — 먼저 목록이나 내용을 입력해 주세요")
            return False
        return True

    def save_pdf(self, test=False, path: str | None = None):
        test = test is True
        if not self._confirm_empty(test):
            return
        spec = self._spec()
        name = f"{'정렬테스트' if test else ('우편라벨' if self.mode == 'addr' else '일반라벨')}_{spec.key if self.mode == 'gen' else spec.model}.pdf"
        path = path or ask_save(self, name, "PDF 파일 (*.pdf)")
        if not path:
            return
        try:
            R.export_pdf(self._make_doc(test), path, "정렬 테스트지" if test else "우편 라벨")
        except Exception as e:  # noqa
            self.ctx.toast(f"PDF 를 저장하지 못했습니다 — {type(e).__name__}: {e}")
            return
        self.ctx.toast("PDF 를 저장했습니다")
        open_folder(path)

    def print_dialog(self, test=False):
        test = test is True
        if not self._confirm_empty(test):
            return
        from PySide6.QtGui import QPageSize
        from PySide6.QtPrintSupport import QPrintDialog, QPrinter
        pr = QPrinter(QPrinter.HighResolution)
        pr.setPageSize(QPageSize(QPageSize.A4))
        pr.setFullPage(True)
        dlg = QPrintDialog(pr, self)
        dlg.setWindowTitle("인쇄")
        if dlg.exec() == QDialog.Accepted:
            try:
                R.print_doc(self._make_doc(test), pr)
                self.ctx.toast("인쇄를 보냈습니다")
            except Exception as e:  # noqa
                self.ctx.toast(f"인쇄하지 못했습니다 — {type(e).__name__}")

    # ══════════════ 창 ══════════════
    def on_show(self):
        self.view._relayout()
