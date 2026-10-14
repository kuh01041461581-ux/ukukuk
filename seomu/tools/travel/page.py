# -*- coding: utf-8 -*-
from PySide6.QtCore import Qt
from PySide6.QtWidgets import QCheckBox, QDoubleSpinBox, QFrame, QHBoxLayout, QLabel, QLineEdit, QMessageBox, QSpinBox, QVBoxLayout, QWidget
from ...core import excel
from ...ui.theme import theme
from ...ui.widgets import (Accordion, Button, DataTable, Empty, Field, FileInput, FormGrid, Note, StatRow, Tag, ask_save, cell_center,
                           cell_check, cell_edit, cell_text, label, open_folder)
from ..base import ToolPage
from . import logic as L


class TravelPage(ToolPage):
    def build(self):
        self.emps: dict = {}
        self.order: list = []
        self.results: dict = {}
        self.selected: dict = {}
        self.ov: dict = {}
        self.order_names = None
        self.bank_map: dict = {}
        self.ref_set = None
        self.vehicle = None
        self.ehojo_path = None
        self.filter = ""
        # ── 1 원본 올리기 ──
        c1 = self.card("1", "출장 원본 파일 올리기", "\"출장 월별집계\" 형태의 원본 엑셀(예: 7월_출장_여비.xlsx)을 올려 주세요. 사람별 출장 기록이 자동으로 인식됩니다.")
        self.src = FileInput("여기에 파일을 놓거나 클릭해서 선택하세요", ".xlsx / .xls 파일 · 이 PC 안에서만 처리되며 서버로 전송되지 않습니다", [".xlsx", ".xls"], kind="엑셀 파일")
        self.src.file_changed.connect(self.on_source)
        c1.add(self.src)
        self.src_note = Note("", "err")
        self.src_note.hide()
        c1.add(self.src_note)
        acc = Accordion("지급 산정 규칙 설정", "기본값 적용 중")
        g = FormGrid(3)
        self.r_month = self._spin(140000, 0, 300000, 10000)
        self.r_day = self._spin(20000, 0, 1000000, 10000)
        self.r_full = self._dspin(4, 0.5)
        self.r_split = self._dspin(1, 0.5)
        self.r_veh = self._spin(10000, 0, 1000000, 1000)
        self.r_kw = QLineEdit("관외")
        for w in (self.r_month, self.r_day, self.r_full, self.r_split, self.r_veh):
            w.valueChanged.connect(self.recompute)
        self.r_kw.editingFinished.connect(self.recompute)
        g.add(Field("1인 월 최대 지급액 (원, 300,000원 초과 불가)", self.r_month))
        g.add(Field("1일 최대 지급액 (원)", self.r_day))
        g.add(Field("단일(통) 출장 인정 최소시간 (시간)", self.r_full))
        g.add(Field("분할 출장 1건당 인정 최소시간 (시간)", self.r_split))
        g.add(Field("차량 운행일 1일 감액액 (원)", self.r_veh))
        g.add(Field("관외(타지역) 판정 키워드 (쉼표로 구분)", self.r_kw))
        acc.body.addWidget(g)
        acc.body.addWidget(Note("<b>계산 로직</b> · 같은 날 출장기록이 <b>1건</b>이고 시간이 단일인정기준(기본 4시간) 이상이면 1일 최대액 전액, 분할인정기준(기본 1시간) 이상이면 절반, 그 미만이면 0원. "
                                "<b>2건 이상</b>이면 건당 1시간 이상인 건마다 절반씩 지급하되 합산은 1일 최대액을 넘지 않습니다. "
                                "<b>차량 감액</b>은 차량배차 현황 파일에 <b>관내 목적지</b> 배차 기록이 있는 날에만 적용(관외는 제외). "
                                "<b>월 한도</b>는 금액이 큰 날부터 채운 뒤 남는 한도를 반액인 날로 메워 한도를 최대한 소진합니다.", "info"))
        c1.add(acc)
        c1.add(label("선택 항목 · 올리면 결과가 더 정확해집니다", "small"))
        self.order_in = self._opt(c1, "지급대상자 목록", "성명·은행명·계좌번호가 있는 목록 엑셀(연번 순서 포함)을 올리면 그 순서대로 정렬되고, 목록에 있는 사람만 자동 선택되며 은행명·계좌번호도 채워집니다. 가장 오른쪽(최신) 시트를 씁니다.",
                                  ".xlsx/.xls · 연번/성명/은행명/계좌번호 열", [".xlsx", ".xls"], self.on_order)
        self.veh_in = self._opt(c1, "차량배차 현황", "공무용차량 배차대장을 올리면, 배차·운행된 날짜 중 <b>목적지가 관내인 건</b>에 한해 해당 일자 여비에서 차량 감액액을 차감합니다. .csv(CP949/EUC-KR)와 .xlsx/.xls를 지원합니다.",
                                ".csv/.xlsx/.xls · 배차대장(사용일시/사용자/목적지 등)", [".csv", ".xlsx", ".xls"], self.on_vehicle)
        self.eho_in = self._opt(c1, "e호조 지출폼 원본 양식", "기존 e호조 관내여비 지출폼 원본을 올리면, 다른 시트(은행 코드표 등)와 데이터 유효성 검사는 그대로 두고 \"거래처입력\" 시트 값만 채워 저장합니다. 올리지 않으면 서식을 새로 만듭니다.",
                                ".xlsx · e호조 관내여비 지출폼 원본", [".xlsx"], self.on_ehojo)
        self.empty = Empty("파일을 올리면 여기에 결과가 나타납니다.")
        self.v.addWidget(self.empty)
        # ── 2 대상자 ──
        self.c2 = self.card("2", "대상자 선택 · 확인", "인식된 인원 목록입니다. 지급명세서에 넣을 사람만 체크하고, 필요한 항목(은행·계좌·출장목적)을 직접 채워 주세요.")
        self.stats = StatRow()
        self.c2.add(self.stats)
        bar = QHBoxLayout()
        bar.setSpacing(12)
        self.all_cb = QCheckBox("전체 선택")
        self.all_cb.setChecked(True)
        self.all_cb.toggled.connect(self.toggle_all)
        bar.addWidget(self.all_cb)
        self.search = QLineEdit()
        self.search.setPlaceholderText("이름으로 검색")
        self.search.setFixedWidth(220)
        self.search.textChanged.connect(self._search)
        bar.addWidget(self.search)
        bar.addStretch(1)
        rc = Button("다시 계산", "ghost", "refresh", "sm")
        rc.clicked.connect(self.recompute)
        bar.addWidget(rc)
        self.c2.add_layout(bar)
        self.etable = DataTable([("", 44, "c"), ("No", 44, "c"), ("성명", 84, "l"), ("소속/직급", 130, "l"), ("전체건수", 70, "c"), ("지급일수", 112, "c"),
                                 ("산정금액", 90, "r"), ("은행명", 100, "l"), ("계좌번호", 130, "l"), ("예금주명", 92, "l"), ("출장목적", None, "l")], row_height=48, max_rows=9)
        self.c2.add(self.etable)
        # ── 3 미리보기·저장 ──
        self.c3 = self.card("3", "지급명세서 미리보기 · 저장", "아래 표가 실제로 저장될 지급명세서 내용입니다. 상단 정보를 입력한 뒤 엑셀로 저장하세요.")
        mg = FormGrid(4)
        self.m_ym = QLineEdit("2026년 7월")
        self.m_dept = QLineEdit("문화유산과")
        self.m_rank = QLineEdit()
        self.m_name = QLineEdit()
        for e in (self.m_ym, self.m_dept, self.m_rank, self.m_name):
            e.textChanged.connect(self.render_preview)
        mg.add(Field("문서 제목 (연/월)", self.m_ym))
        mg.add(Field("부서명", self.m_dept))
        mg.add(Field("대조자 직급", self.m_rank))
        mg.add(Field("대조자 성명", self.m_name))
        self.c3.add(mg)
        self.l_title = QLabel("")
        self.l_title.setAlignment(Qt.AlignCenter)
        self.l_title.setStyleSheet("font-size:19px; font-weight:700; background:transparent;")
        self.c3.add(self.l_title)
        self.l_sub = QLabel("")
        self.l_sub.setStyleSheet(f"color:{theme.c('text2')}; font-size:12.5px; background:transparent;")
        self.c3.add(self.l_sub)
        self.ltable = DataTable([("연번", 56, "c"), ("성명", 90, "c"), ("금액(원)", 100, "r"), ("은행명", 100, "c"), ("계좌번호", 140, "c"), ("출장기간(일)", None, "l"), ("출장목적", 170, "l")], row_height=42, max_rows=10)
        self.c3.add(self.ltable)
        row = QHBoxLayout()
        self.foot = label("", "small")
        row.addWidget(self.foot, 1)
        b1 = Button("e호조 지출폼(.xlsx)", "ghost", "down")
        b1.clicked.connect(self.save_ehojo)
        b2 = Button("지급명세서(총괄 서식) 엑셀", "primary", "down")
        b2.clicked.connect(self.save_ledger)
        row.addWidget(b1)
        row.addWidget(b2)
        self.c3.add_layout(row)
        self.saved_note = Note("", "ok")
        self.saved_note.hide()
        self.c3.add(self.saved_note)
        self.last_path = None
        self.open_btn = Button("저장한 폴더 열기", "ghost", "file", "sm")
        self.open_btn.clicked.connect(lambda: open_folder(self.last_path))
        self.open_btn.hide()
        self.c3.add(self.open_btn)
        self.c2.hide()
        self.c3.hide()
        # 참고 사항
        n = self.card(None, "참고 사항")
        n.add(label("· 이 프로그램은 이 PC 안에서만 동작하며, 업로드한 파일은 외부로 전송되지 않습니다.<br>"
                    "· 은행명/계좌번호/출장목적은 원본 파일에 없는 경우가 많아 자동 채움 값(추정)이므로, 저장 전에 반드시 직접 확인·수정해 주세요.<br>"
                    "· 1인 월 최대 지급액은 규칙 설정과 무관하게 <b>300,000원을 넘지 않도록</b> 항상 상한이 적용됩니다(기본 140,000원).<br>"
                    "· 지급명세서의 \"출장기간(일)\" 칸에는 금액이 반영되지 않은 날짜를 포함해 원본에 등장하는 모든 출장 일자가 표시됩니다.", "small", wrap=True, rich=True))

    # ── 도우미 ──
    def _spin(self, v, lo, hi, step):
        s = QSpinBox()
        s.setRange(lo, hi)
        s.setSingleStep(step)
        s.setValue(v)
        s.setGroupSeparatorShown(True)
        return s

    def _dspin(self, v, step):
        s = QDoubleSpinBox()
        s.setRange(0, 24)
        s.setSingleStep(step)
        s.setDecimals(1)
        s.setValue(v)
        return s

    def _opt(self, card, title, desc, sub, exts, handler):
        acc = Accordion(title, "선택")
        d = label(desc, "desc", wrap=True, rich=True)
        acc.body.addWidget(d)
        fi = FileInput("여기에 놓거나 클릭해서 선택", sub, exts, compact=True, kind="파일")
        fi.file_changed.connect(handler)
        acc.body.addWidget(fi)
        card.add(acc)
        return fi

    def rules(self) -> L.Rules:
        return L.Rules(self.r_month.value(), self.r_day.value(), self.r_full.value(), self.r_split.value(), self.r_veh.value(),
                       [k.strip() for k in self.r_kw.text().split(",") if k.strip()])

    # ── 파일 처리 ──
    def on_source(self, path):
        if not path:
            self.emps, self.order = {}, []
            self.c2.hide()
            self.c3.hide()
            self.empty.show()
            self.src_note.hide()
            return
        try:
            rows = L.sheet_rows(excel.read_sheets(path), "first")
            emps = L.parse_employees(rows)
        except Exception as e:
            self.src_note.set_text(str(e) if isinstance(e, L.ParseError) else f"파일을 읽는 중 오류가 발생했습니다: {e}", "err")
            self.src_note.show()
            return
        if not emps:
            self.src_note.set_text("출장 기록을 찾지 못했습니다. 파일 형식을 확인해주세요.", "err")
            self.src_note.show()
            return
        self.src_note.hide()
        self.emps = emps
        self.order = list(emps)
        self.selected = {i: True for i in self.order}
        self.ov = {i: L.Override("", "", e.name, e.common_purpose()) for i, e in emps.items()}
        self.recompute(render=False)
        self.empty.hide()
        self.c2.show()
        self.c3.show()
        self.apply_reference()
        self.render_all()

    def on_order(self, path):
        if not path:
            self.order_names, self.bank_map, self.ref_set = None, {}, None
            for i in self.order:
                self.selected[i] = True
        else:
            try:
                names = L.parse_order_file(L.sheet_rows(excel.read_sheets(path), "last"))
            except Exception as e:
                QMessageBox.warning(self, "지급대상자 목록", f"참고 파일을 읽는 중 오류가 발생했습니다: {e}")
                return
            if not names:
                QMessageBox.warning(self, "지급대상자 목록", "가장 최신 시트에서 \"연번\"/\"성명\" 열을 찾지 못했습니다. 지급대상자 목록 형식인지 확인해주세요.")
                return
            self.order_names, self.bank_map = names
            self.ref_set = set(self.order_names)
        if self.order:
            self.apply_reference()
            self.render_all()

    def on_vehicle(self, path):
        if not path:
            self.vehicle = None
        else:
            try:
                p = path.lower()
                rows = L.read_csv_rows(path) if p.endswith(".csv") else L.sheet_rows(excel.read_sheets(path), "last")
                rows = [[L.cell_text(c) for c in r] for r in rows]
                usage = L.parse_vehicle_file(rows)
            except Exception as e:
                QMessageBox.warning(self, "차량배차 현황", f"차량배차 파일을 읽는 중 오류가 발생했습니다: {e}")
                return
            if not usage:
                QMessageBox.warning(self, "차량배차 현황", "이 파일에서 사용일시/사용자 등 배차대장 열을 찾지 못했거나 유효한 배차 기록이 없습니다.")
                return
            self.vehicle = usage
        if self.order:
            self.recompute()

    def on_ehojo(self, path):
        self.ehojo_path = path

    def apply_reference(self):
        for i in self.order:
            e = self.emps[i]
            if self.ref_set is not None:
                self.selected[i] = e.name in self.ref_set
            ref = self.bank_map.get(e.name)
            if ref:
                if ref["bank"]:
                    self.ov[i].bank = ref["bank"]
                if ref["account"]:
                    self.ov[i].account = ref["account"]

    def display_order(self) -> list:
        if not self.order_names:
            return list(self.order)
        rank = {}
        for i, n in enumerate(self.order_names):
            rank.setdefault(n, i)
        return sorted(self.order, key=lambda i: (rank.get(self.emps[i].name, 10 ** 9), self.order.index(i)))

    def recompute(self, *_, render=True):
        if not self.order:
            return
        rules = self.rules()
        for i in self.order:
            self.results[i] = L.compute(self.emps[i], rules, self.vehicle)
        if render:
            self.render_all()

    # ── 그리기 ──
    def render_all(self):
        self.render_stats()
        self.render_table()
        self.render_preview()

    def sel_ids(self):
        return [i for i in self.display_order() if self.selected.get(i)]

    def render_stats(self):
        sel = self.sel_ids()
        total = sum(self.results[i].total for i in sel)
        capped = sum(1 for i in sel if self.results[i].capped)
        items = [("인식된 전체 출장 기록", f"{sum(e.all_count for e in self.emps.values()):,}건"), ("인식된 인원 수", f"{len(self.order)}명"),
                 ("선택된 지급 대상자", f"{len(sel)}명"), (f"선택 대상 합계 지급액 · 한도도달 {capped}명", f"{total:,}원", True)]
        if self.vehicle is not None:
            items.append(("차량 운행 감액 총액", f"{sum(self.results[i].vehicle_total for i in sel):,}원"))
        self.stats.set_items(items)

    def _on_sel(self, pid, on):
        self.selected[pid] = on
        self.render_stats()
        self.render_preview()

    def render_table(self):
        q = self.filter.strip()
        rows = []
        for n, pid in enumerate(self.display_order(), 1):
            e, r, o = self.emps[pid], self.results[pid], self.ov[pid]
            if q and q not in e.name:
                continue
            kind = "err" if r.total <= 0 else ("warn" if r.capped else "pri")
            tagbox = QWidget()
            tl = QHBoxLayout(tagbox)
            tl.setContentsMargins(0, 0, 0, 0)
            tl.setSpacing(4)
            tl.setAlignment(Qt.AlignCenter)
            tl.addWidget(Tag("0일" if r.total <= 0 else f"{len(r.included_days)}일" + (" (한도)" if r.capped else ""), kind))
            if r.vehicle_days:
                t = Tag(f"차량 -{len(r.vehicle_days)}")
                t.setToolTip(f"차량 운행으로 감액된 날짜: {','.join(map(str, r.vehicle_days))}일")
                tl.addWidget(t)

            def mk(field, ph, pid=pid):
                return cell_edit(getattr(o, field), ph, lambda t, f=field, p=pid: self._edit(p, f, t))
            row = [cell_check(self.selected.get(pid, True), lambda on, p=pid: self._on_sel(p, on)), cell_text(str(n), "c"),
                   cell_text(e.name, "l", bold=True), cell_text(e.dept, "l", small=True, color=theme.c("text2")), cell_text(str(e.all_count), "c"), tagbox,
                   cell_text(f"{r.total:,}", "r"), mk("bank", "은행명"), mk("account", "계좌번호"), mk("holder", "예금주명"), mk("purpose", "출장목적")]
            rows.append(row)
        self.etable.clear()
        for r in rows:
            self.etable.add_row(r)

    def _edit(self, pid, field, text):
        setattr(self.ov[pid], field, text)
        self.render_preview()

    def toggle_all(self, on):
        for i in self.order:
            self.selected[i] = on
        self.render_stats()
        self.render_table()
        self.render_preview()

    def _search(self, text):
        self.filter = text
        self.render_table()

    def render_preview(self):
        if not self.order:
            return
        ym = self.m_ym.text()
        self.l_title.setText((ym + " 국내여비 지급 명세서").strip())
        self.l_sub.setText(f"지 출 명 세 서        출장명령부 대조자 · 직급: {self.m_rank.text() or '-'} · 성명: {self.m_name.text() or '-'} (인)")
        sel = self.sel_ids()
        grand = sum(self.results[i].total for i in sel)
        rows = [["", "계", f"{grand:,}", "", "", "", ""]]
        for n, pid in enumerate(sel, 1):
            e, r, o = self.emps[pid], self.results[pid], self.ov[pid]
            rows.append([str(n), e.name, f"{r.total:,}", o.bank, o.account, ",".join(map(str, r.all_days)), o.purpose])
        self.ltable.set_rows(rows)
        rl = self.rules().normalized()
        self.foot.setText(f"대상 {len(sel)}명 · 합계 {grand:,}원 · 생성 규칙: 월한도 {rl.monthly_cap:,}원 / 일한도 {rl.daily_cap:,}원")

    # ── 저장 ──
    def _done(self, path):
        self.last_path = path
        self.saved_note.set_text(f"저장했습니다 — <b>{path}</b>", "ok")
        self.saved_note.show()
        self.open_btn.show()
        self.toast("엑셀을 저장했습니다")

    def save_ledger(self):
        sel = self.sel_ids()
        if not sel:
            QMessageBox.information(self, "저장", "선택된 대상자가 없습니다.")
            return
        ym = self.m_ym.text()
        safe = "".join(ch for ch in ym if ch.isalnum()) or "결과"
        fn = ask_save(self, f"출장여비_지급명세서_{safe}.xlsx", "엑셀 파일 (*.xlsx)")
        if not fn:
            return
        rows = [(self.emps[i].name, self.results[i].total, self.ov[i].bank, self.ov[i].account, ",".join(map(str, self.results[i].all_days)), self.ov[i].purpose) for i in sel]
        try:
            L.build_ledger((ym + " 국내여비 지급 명세서").strip(), self.m_rank.text(), self.m_name.text(), rows).save(fn)
        except Exception as e:
            QMessageBox.warning(self, "저장", f"저장 중 오류: {e}")
            return
        self._done(fn)

    def save_ehojo(self):
        sel = self.sel_ids()
        if not sel:
            QMessageBox.information(self, "저장", "선택된 대상자가 없습니다.")
            return
        missing = [self.emps[i].name for i in sel if not self.ov[i].account]
        if missing and QMessageBox.question(self, "계좌번호 확인", f"계좌번호가 비어있는 대상자가 있습니다: {', '.join(missing)}\n그대로 진행할까요?") != QMessageBox.Yes:
            return
        safe = "".join(ch for ch in self.m_ym.text() if ch.isalnum()) or "결과"
        fn = ask_save(self, f"e호조_관내여비지출폼_{safe}.xlsx", "엑셀 파일 (*.xlsx)")
        if not fn:
            return
        rows = [(self.emps[i].name, self.ov[i].bank, self.ov[i].holder, self.ov[i].account, self.results[i].total) for i in sel]
        try:
            wb, used = L.build_ehojo(rows, self.ehojo_path)
            wb.save(fn)
        except Exception as e:
            QMessageBox.warning(self, "저장", f"저장 중 오류: {e}")
            return
        self._done(fn)
