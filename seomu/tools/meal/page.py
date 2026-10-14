# -*- coding: utf-8 -*-
from PySide6.QtCore import Qt, QTimer
from PySide6.QtWidgets import QHBoxLayout, QLabel, QLineEdit, QPushButton, QSpinBox, QTextEdit, QVBoxLayout, QWidget
from ...core import excel
from ...ui.theme import theme
from ...ui.widgets import (Button, DataTable, DownloadBar, Empty, Field, FileInput, FormGrid, Note, StatRow, ask_save, fmt_size, label,
                           open_folder)
from ..base import ToolPage
from . import logic


class MealPage(ToolPage):
    def build(self):
        self.ot: logic.Overtime | None = None
        self.template_names = None
        self.vendors: list[list] = [["", 0]]
        # 1 기존 양식
        c1 = self.card("1", "기존 양식 파일", "이전 달에 쓰던 <b>급식비 지급내역</b> 정산 엑셀을 올리면 \"급식지급 상세내역\" 시트의 <b>직원 명단과 순서</b>를 그대로 이어받습니다. "
                       "올리지 않으면 초과근무 내역에서 발견되는 순서대로 명단을 만듭니다.", tag="선택")
        self.tpl_in = FileInput("클릭하거나 파일을 여기로 끌어다 놓으세요", ".xls / .xlsx · 선택 사항", [".xls", ".xlsx"], compact=True, kind="엑셀 파일")
        self.tpl_in.file_changed.connect(self.on_template)
        c1.add(self.tpl_in)
        self.tpl_note = Note("", "info")
        self.tpl_note.hide()
        c1.add(self.tpl_note)
        # 2 초과근무
        c2 = self.card("2", "초과근무 내역 엑셀 올리기", "인사랑에서 내려받은 \"초과근무 내역\" 엑셀(.xls / .xlsx)을 올려 주세요. No, 근무일자, 부서, 직급, 성명, 휴일구분, 출근(실제), 퇴근(실제), 총초과(분), 근무내역 열을 자동으로 찾아 분석합니다.")
        self.ot_in = FileInput("클릭하거나 파일을 여기로 끌어다 놓으세요", ".xls / .xlsx 파일 지원", [".xls", ".xlsx"], kind="엑셀 파일")
        self.ot_in.file_changed.connect(self.on_overtime)
        c2.add(self.ot_in)
        self.ot_note = Note("", "info")
        self.ot_note.hide()
        c2.add(self.ot_note)
        self.empty = Empty("엑셀 파일을 올리면 여기에 인식 결과와 다운로드 버튼이 나타납니다.")
        self.v.addWidget(self.empty)
        # 인식 결과
        self.c_sum = self.card("✓", "인식 결과", "최대 급식 가능 인원은 <b>휴일구분 = 평일</b>이면서 <b>퇴근(실제) 20:00 이후</b>인 날의 (직원×일자) 조합 수입니다. "
                               "실제 O표시는 아래에서 입력한 급식인원 합계만큼만 날짜별로 나눠 배분됩니다.")
        self.c_sum.badge.set_done(True)
        self.stats = StatRow()
        self.c_sum.add(self.stats)
        self.toggle = QPushButton("▸  급식지급 상세내역 미리보기 열기")
        self.toggle.setCursor(Qt.PointingHandCursor)
        self.toggle.setStyleSheet("QPushButton { background:transparent; border:0; color:%s; font-weight:600; text-align:left; min-height:24px; padding:0; }" % theme.c("primary"))
        self.toggle.clicked.connect(self.toggle_preview)
        self.c_sum.add(self.toggle)
        self.prev_note = label("", "small", wrap=True, rich=True)
        self.c_sum.add(self.prev_note)
        self.preview = QTextEdit()
        self.preview.setReadOnly(True)
        self.preview.setFixedHeight(340)
        self.preview.hide()
        self.c_sum.add(self.preview)
        # 3 급식처
        self.c_v = self.card("3", "급식처 · 급식인원 입력", "식사를 제공한 급식처(식당)와 인원을 나눠서 입력하세요. 지급액은 인원 × 9,000원으로 자동 계산됩니다.")
        self.vtable = DataTable([("연번", 64, "c"), ("급식처", None, "l"), ("급식인원", 150, "c"), ("단가", 110, "c"), ("지급액", 140, "r"), ("", 60, "c")], max_rows=8)
        self.c_v.add(self.vtable)
        row = QHBoxLayout()
        add = Button("+ 급식처 추가", "secondary", size="sm")
        add.clicked.connect(self.add_vendor)
        row.addWidget(add)
        row.addStretch(1)
        self.c_v.add_layout(row)
        self.total_note = Note("", "info")
        self.c_v.add(self.total_note)
        # 4 확인자
        self.c_o = self.card("4", "확인자 정보", "급식확인서 하단에 표시될 확인자 직급·성명입니다. 비워 두면 공란으로 출력됩니다.", tag="선택")
        g = FormGrid(2)
        self.rank = QLineEdit()
        self.rank.setPlaceholderText("예) 행정6급")
        self.oname = QLineEdit()
        self.oname.setPlaceholderText("예) 홍 길 동")
        g.add(Field("확인자 직급", self.rank))
        g.add(Field("확인자 성명", self.oname))
        self.c_o.add(g)
        # 5 생성
        self.c_g = self.card("5", "정산 엑셀 만들기", "버튼을 누르면 <b>급식확인서 · 급식지급 상세내역 · 초과근무내역</b> 3개 시트가 들어 있는 엑셀 파일을 저장합니다.")
        self.gen = Button("정산 엑셀 저장", "primary", "down", "lg")
        self.gen.clicked.connect(self.generate)
        self.c_g.add(self.gen)
        self.result_note = Note("", "ok")
        self.result_note.hide()
        self.c_g.add(self.result_note)
        self.last_path = None
        self.open_btn = Button("저장한 폴더 열기", "ghost", "file", "sm")
        self.open_btn.clicked.connect(lambda: open_folder(self.last_path))
        self.open_btn.hide()
        self.c_g.add(self.open_btn)
        for c in (self.c_sum, self.c_v, self.c_o, self.c_g):
            c.hide()
        self.render_vendors()

    # ---- 파일 -------------------------------------------------------
    def on_template(self, path):
        if not path:
            self.template_names = None
            self.tpl_note.hide()
        else:
            try:
                names = logic.parse_template_names(excel.read_sheets(path))
            except Exception as e:
                names = None
                self.tpl_note.set_text(f"기존양식 파일을 읽는 중 오류가 발생했습니다: {e}", "err")
                self.tpl_note.show()
                self.template_names = None
                return
            if not names:
                self.tpl_note.set_text("기존양식에서 직원 명단(성명 열)을 찾지 못했습니다. 올리지 않은 것과 같게 처리합니다.", "warn")
            else:
                self.tpl_note.set_text(f"기존양식에서 직원 {len(names)}명의 명단과 순서를 읽었습니다.", "ok")
            self.tpl_note.show()
            self.template_names = names
        if self.ot:
            logic.apply_master(self.ot, self.template_names)
            self.refresh_all()

    def on_overtime(self, path):
        if not path:
            self.ot = None
            for c in (self.c_sum, self.c_v, self.c_o, self.c_g):
                c.hide()
            self.empty.show()
            self.ot_note.hide()
            return
        try:
            ot = logic.parse_overtime(excel.read_sheets(path))
        except logic.ParseError as e:
            self.ot_note.set_text(str(e), "err")
            self.ot_note.show()
            return
        except Exception as e:
            self.ot_note.set_text(f"파일을 읽는 중 오류가 발생했습니다: {e}", "err")
            self.ot_note.show()
            return
        logic.apply_master(ot, self.template_names)
        self.ot = ot
        self.vendors = [["", 0]]
        self.ot_note.set_text(f"분석 완료: {len(ot.records)}건 중 {ot.max_meal}건이 급식 지급 대상입니다.", "ok")
        self.ot_note.show()
        self.empty.hide()
        for c in (self.c_sum, self.c_v, self.c_o, self.c_g):
            c.show()
        self.refresh_all()

    # ---- 화면 갱신 -----------------------------------------------------
    def refresh_all(self):
        ot = self.ot
        self.stats.set_items([("대상 연월", f"{ot.year}.{ot.month}"), ("총 초과근무 기록", f"{len(ot.records)}건"),
                              ("전체 직원 수", f"{len(ot.master or ot.employees)}명"), ("최대 급식 가능 인원(끼)", f"{ot.max_meal}끼", True)])
        self.render_vendors()
        self.refresh_preview()

    def toggle_preview(self):
        vis = not self.preview.isVisible()
        self.preview.setVisible(vis)
        self.toggle.setText("▾  미리보기 닫기" if vis else "▸  급식지급 상세내역 미리보기 열기")
        if vis:
            self.refresh_preview()

    def total_count(self) -> int:
        return sum(max(0, int(v[1] or 0)) for v in self.vendors)

    def refresh_preview(self):
        ot = self.ot
        if not ot:
            return
        target = self.total_count()
        self.prev_note.setText(f"현재 입력된 급식인원 합계 <b>{target}명</b> 기준으로 배분된 미리보기입니다." if target else
                               "급식처 단계에서 급식인원을 입력하면 그 인원수만큼 날짜별로 배분된 O표시가 여기에 나타납니다.")
        if not self.preview.isVisible():
            return
        c = theme.tok
        sel = logic.allocate(ot, target)
        th = f"background:{c['surface2']}; color:{c['text2']}; font-weight:600; border-bottom:1px solid {c['border_strong']};"
        html = [f'<table cellspacing="0" cellpadding="5" style="border-collapse:collapse; font-size:11px; color:{c["text"]};"><tr>',
                f'<th style="{th}">연번</th><th style="{th}">성명</th><th style="{th}">계</th>']
        for d in range(1, ot.days + 1):
            col = c["danger"] if logic.is_red_day(ot, d) else (c["primary"] if logic.is_blue_day(ot, d) else c["text2"])
            html.append(f'<th style="{th} color:{col};">{d}</th>')
        html.append("</tr>")
        for i, emp in enumerate(ot.master or ot.employees):
            qs = sel.get(emp, set())
            html.append(f'<tr><td align="center">{i + 1}</td><td align="center">{emp}</td><td align="center">{len(qs)}</td>')
            for d in range(1, ot.days + 1):
                ds = f"{ot.year}-{ot.month:02d}-{d:02d}"
                html.append(f'<td align="center" style="color:{c["primary"]}; font-weight:700;">{"O" if ds in qs else ""}</td>')
            html.append("</tr>")
        html.append("</table>")
        self.preview.setStyleSheet(f"QTextEdit {{ background:{c['surface']}; border:1px solid {c['border']}; border-radius:8px; }}")
        self.preview.setHtml("".join(html))

    def add_vendor(self):
        self.vendors.append(["", 0])
        self.render_vendors()

    def render_vendors(self):
        rows = []
        for i, (name, count) in enumerate(self.vendors):
            e = QLineEdit(name)
            e.setProperty("cell", True)
            e.setPlaceholderText("예) 원광반점")
            e.textChanged.connect(lambda t, i=i: self._v_name(i, t))
            sp = QSpinBox()
            sp.setProperty("cell", True)
            sp.setRange(0, 9999)
            sp.setAlignment(Qt.AlignCenter)
            sp.setValue(int(count or 0))
            amount = QLabel(f"{int(count or 0) * logic.UNIT_PRICE:,}원")
            amount.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
            amount.setStyleSheet("padding-right:10px; background:transparent;")
            sp.valueChanged.connect(lambda v, i=i, a=amount: self._v_count(i, v, a))
            rm = Button("", "flat", "trash", "icon")
            rm.setEnabled(len(self.vendors) > 1)
            rm.clicked.connect(lambda _=False, i=i: self.remove_vendor(i))
            idx = QLabel(str(i + 1))
            idx.setAlignment(Qt.AlignCenter)
            price = QLabel("9,000원")
            price.setAlignment(Qt.AlignCenter)
            rows.append([idx, e, sp, price, amount, rm])
        self.vtable.clear()
        for r in rows:
            self.vtable.add_row(r)
        self.update_total()

    def _v_name(self, i, t):
        self.vendors[i][0] = t
        self.update_total(preview=False)

    def _v_count(self, i, v, amount_lb):
        self.vendors[i][1] = v
        amount_lb.setText(f"{v * logic.UNIT_PRICE:,}원")
        self.update_total()

    def remove_vendor(self, i):
        if len(self.vendors) > 1:
            self.vendors.pop(i)
            self.render_vendors()

    def update_total(self, preview=True):
        if not self.ot:
            return
        total = self.total_count()
        diff = self.ot.max_meal - total
        msg = f"입력한 급식인원 합계 <b>{total}명</b> · 지급액 합계 <b>{total * logic.UNIT_PRICE:,}원</b> · 최대 가능 {self.ot.max_meal}명"
        if diff < 0:
            self.total_note.set_text(msg + f" &nbsp;최대치보다 <b>{-diff}명</b> 초과했습니다.", "warn")
        else:
            self.total_note.set_text(msg + f" &nbsp;(남음 {diff}명)", "ok")
        if preview:
            self._deb()

    def _deb(self):
        if not hasattr(self, "_t"):
            self._t = QTimer(self)
            self._t.setSingleShot(True)
            self._t.timeout.connect(self.refresh_preview)
        self._t.start(250)

    # ---- 저장 ----------------------------------------------------------
    def generate(self):
        vendors = [(n.strip(), int(c)) for n, c in self.vendors if n.strip() and int(c) > 0]
        if not vendors:
            self.result_note.set_text("급식처와 인원을 최소 1건 이상 입력해주세요.", "err")
            self.result_note.show()
            return
        ot = self.ot
        fn = ask_save(self, f"{ot.year}. {ot.month}월 급식비 지급내역.xlsx", "엑셀 파일 (*.xlsx)")
        if not fn:
            return
        try:
            wb = logic.build_workbook(ot, vendors, self.rank.text().strip(), self.oname.text().strip())
            wb.save(fn)
        except Exception as e:
            self.result_note.set_text(f"엑셀 생성 중 오류: {e}", "err")
            self.result_note.show()
            return
        self.last_path = fn
        self.result_note.set_text(f"저장했습니다 — <b>{fn}</b>", "ok")
        self.result_note.show()
        self.open_btn.show()
        self.toast("정산 엑셀을 저장했습니다")
