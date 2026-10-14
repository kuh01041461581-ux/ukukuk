# -*- coding: utf-8 -*-
import os
import tempfile
import time
from PySide6.QtCore import QDate, QDateTime, QTime, QMarginsF, QObject, Qt, QTimer, QUrl
from PySide6.QtGui import QDesktopServices
from PySide6.QtWidgets import (QButtonGroup, QCheckBox, QComboBox, QDialog, QDoubleSpinBox, QFileDialog, QFrame, QHBoxLayout, QLabel, QLineEdit,
                               QMessageBox, QRadioButton, QScrollArea, QSpinBox, QTextBrowser, QVBoxLayout, QWidget, QDateTimeEdit)
from ...core.worker import run_async
from ...ui.theme import theme
from ...ui.widgets import (Accordion, Button, DataTable, DownloadBar, Empty, Field, FormGrid, Note, Segmented, StatRow, Tag, ask_save, cell_center,
                           cell_check, cell_edit, cell_text, DropZone, label, open_folder)
from ..base import ToolPage
from . import logic as L
from .tpl_data import default_template

XLSX = "엑셀 파일 (*.xlsx)"


def _spin(lo=0, hi=99999999, step=1000, val=0):
    s = QSpinBox()
    s.setRange(lo, hi)
    s.setSingleStep(step)
    s.setValue(val)
    s.setGroupSeparatorShown(True)
    return s


def _dspin(lo=0.0, hi=99999.0, step=1.0, val=0.0, dec=1):
    s = QDoubleSpinBox()
    s.setRange(lo, hi)
    s.setDecimals(dec)
    s.setSingleStep(step)
    s.setValue(val)
    return s


def _combo(options, value=None):
    c = QComboBox()
    for v, t in options:
        c.addItem(t, v)
    if value is not None:
        c.setCurrentIndex(max(0, c.findData(value)))
    return c


def _row(*widgets, stretch=0):
    box = QWidget()
    l = QHBoxLayout(box)
    l.setContentsMargins(0, 0, 0, 0)
    l.setSpacing(8)
    for i, w in enumerate(widgets):
        l.addWidget(w, 1 if i == stretch else 0)
    return box


class AutoBrowser(QTextBrowser):
    """내용 높이에 맞게 늘어나는 읽기 전용 미리보기 (종이 느낌의 흰 바탕)"""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFrameShape(QFrame.NoFrame)
        self.setVerticalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self.setStyleSheet(f"QTextBrowser {{ background:#FFFFFF; color:#000000; border:1px solid {theme.c('border_strong')}; padding:18px 26px; }}")

    def resizeEvent(self, e):
        super().resizeEvent(e)
        self._fit()

    def _fit(self):
        d = self.document()
        d.setTextWidth(max(200, self.viewport().width()))
        h = int(d.size().height()) + 40
        if h != self.height():
            self.setFixedHeight(h)

    def set_page(self, html: str):
        self.setHtml(f'<body style="color:#000000; background:#FFFFFF;">{html}</body>')
        self._fit()


class OilRow(QObject):
    """유종 → 유가 조회 → 운임단가(원/km) 한 묶음 (출장 입력창과 거리 계산기가 같이 쓴다)"""

    def __init__(self, page, fuel, price, rate, unit, src, date_getter=None):
        super().__init__(page)
        self.page, self.fuel, self.price, self.rate, self.unit, self.src = page, fuel, price, rate, unit, src
        self.date_getter = date_getter or (lambda: "")
        self.seq = 0
        self.pending = False
        self.price.valueChanged.connect(self.update_rate)

    def update_rate(self, *_):
        self.rate.setValue(L.calc_rate_per_km(self.fuel.currentData(), self.price.value(), self.date_getter()))

    def set_price(self, v):
        self.price.blockSignals(True)
        self.price.setValue(v)
        self.price.blockSignals(False)
        self.update_rate()

    def _src(self, html, kind=""):
        col = {"warn": theme.c("warning"), "err": theme.c("danger"), "ok": theme.c("text3")}.get(kind, theme.c("text3"))
        self.src.setStyleSheet(f"color:{col}; font-size:12px; background:transparent;")
        self.src.setText(html)

    def refresh(self, *_):
        fuel = self.fuel.currentData()
        self.unit.setText(L.FUEL_PRICE_UNIT.get(fuel, "원/L"))
        self.seq += 1
        seq = self.seq
        self.set_price(L.best_known_oil_price(fuel, self.page.opinet_cache))
        self._src("⏳ 오피넷에서 최신 유가를 조회하는 중...")
        self.pending = True
        key = self.page.opinet_key()
        cache = self.page.opinet_cache
        self.page.bg(lambda: L.fetch_oil_price(fuel, key, cache), lambda ok, res: self._done(seq, ok, res))

    def _done(self, seq, ok, res):
        if seq != self.seq:
            return
        self.pending = False
        if not ok:
            self._src("⚠️ 유가 조회 실패: " + str(res), "err")
            return
        if res.get("list"):
            self.page.save_opinet_cache(res["list"])
        self.set_price(res["price"])
        if res["source"] == "opinet":
            self._src("✅ 오피넷 전국 평균가" + (f" ({res['date']} 기준)" if res.get("date") else "") + " · 조회 경로: " + str(res.get("via") or ""), "ok")
        elif res["source"] == "cache":
            ts = time.strftime("%m/%d %H:%M", time.localtime((res.get("cachedAt") or 0) / 1000)) if res.get("cachedAt") else ""
            self._src(f"⚠️ 이번 조회는 실패해서 마지막 성공값({ts} 조회" + (f", {res['date']} 기준" if res.get("date") else "") + f")을 사용했습니다. [{res.get('reason', '')}] '유가 조회'를 눌러 다시 시도하세요.", "warn")
        else:
            self._src("⚠️ " + (res.get("reason") or "조회 실패") + " — 예시값을 채웠으니 확인 후 수정하세요.", "err")


# ───────────────────────────── 주소 검색 ─────────────────────────────
class AddrDialog(QDialog):
    def __init__(self, page, title, text):
        super().__init__(page)
        self.page = page
        self.setWindowTitle(title)
        self.resize(640, 520)
        self.chosen = None
        self.cands = []
        lay = QVBoxLayout(self)
        lay.setSpacing(12)
        row = QHBoxLayout()
        self.q = QLineEdit(text)
        self.q.setPlaceholderText("예: 익산시청, 서울특별시 종로구 세종대로 1")
        self.q.returnPressed.connect(self.search)
        go = Button("검색", "primary", "search")
        go.clicked.connect(self.search)
        row.addWidget(self.q, 1)
        row.addWidget(go)
        lay.addLayout(row)
        self.msg = label("", "small")
        lay.addWidget(self.msg)
        self.tbl = DataTable([("장소 / 주소", 220, "l"), ("도로명 주소", None, "l")], row_height=44, max_rows=8)
        self.tbl.table.cellClicked.connect(self.pick)
        lay.addWidget(self.tbl)
        close = Button("닫기", "ghost")
        close.clicked.connect(self.reject)
        lay.addWidget(close, 0, Qt.AlignRight)

    def search(self):
        q = self.q.text().strip()
        if not q:
            self.msg.setText("검색어를 입력해주세요.")
            return
        self.msg.setText("검색 중...")
        key = self.page.kakao_key()
        self.page.bg(lambda: L.search_address_candidates(q, key), self._done)

    def _done(self, ok, res):
        if not ok:
            self.msg.setText(str(res))
            return
        self.cands = res
        self.msg.setText("" if res else "검색 결과가 없습니다.")
        self.tbl.set_rows([[c["title"] or c["roadAddr"], c["roadAddr"]] for c in res])

    def pick(self, r, _c):
        if 0 <= r < len(self.cands):
            self.chosen = self.cands[r]["roadAddr"]
            self.accept()


# ───────────────────────────── 출장 입력 창 ─────────────────────────────
class TripDialog(QDialog):
    def __init__(self, page, trip: dict | None):
        super().__init__(page)
        self.page = page
        self.editing = trip
        t = trip or L.blank_trip(page.default_dept(), page.best_price())
        self.t = t
        self.setWindowTitle("출장 내역 수정" if trip else "출장자 추가")
        self.resize(780, 760)
        outer = QVBoxLayout(self)
        outer.setSpacing(12)
        sc = QScrollArea()
        sc.setWidgetResizable(True)
        sc.setFrameShape(QFrame.NoFrame)
        body = QWidget()
        sc.setWidget(body)
        v = QVBoxLayout(body)
        v.setContentsMargins(4, 4, 12, 4)
        v.setSpacing(16)
        outer.addWidget(sc, 1)
        # ── 기본 ──
        g = FormGrid(2)
        self.pick = _combo([("", "직접입력...")] + [(str(i), f"{s.get('name')} ({s.get('position') or '-'})") for i, s in enumerate(page.staff)])
        self.pick.currentIndexChanged.connect(self.on_pick)
        self.name = QLineEdit(t["name"])
        self.name.setPlaceholderText("예: 안지훈")
        self.position = QLineEdit(t["position"])
        self.position.setPlaceholderText("예: 지방시설주사보")
        self.dept = QLineEdit(t["dept"])
        self.start = self._dt(t["start"])
        self.end = self._dt(t["end"])
        self.dest = QLineEdit(t["dest"])
        self.dest.setPlaceholderText("예: 대한민국 국회")
        self.dest.editingFinished.connect(self.on_dest)
        self.purpose = QLineEdit(t["purpose"])
        self.purpose.setPlaceholderText("예: 정책세미나 참석")
        self.grade = QLineEdit(t["grade"])
        self.grade.setPlaceholderText("예: 제2호")
        self.transport = _combo(L.TRANSPORTS, t["transport"])
        self.transport.currentIndexChanged.connect(self.toggle)
        for f in (Field("부서원 불러오기", self.pick), Field("성명", self.name), Field("직급", self.position), Field("부서", self.dept),
                  Field("출장 시작", self.start), Field("출장 종료", self.end), Field("출장지", self.dest), Field("출장목적/용무", self.purpose),
                  Field("여비등급", self.grade), Field("교통수단", self.transport)):
            g.add(f)
        v.addWidget(g)
        self.distance = _dspin(0, 99999, 10, float(t.get("distance") or 0))
        v.addWidget(Field("왕복 거리(km)", self.distance, "편도가 아닌 왕복 총거리 기준입니다. 100km 이상이면 식비 3끼(25,000원) 지급 조건에 해당될 수 있어요."))
        # ── 자차 ──
        self.car = QWidget()
        cv = QVBoxLayout(self.car)
        cv.setContentsMargins(0, 0, 0, 0)
        cv.setSpacing(14)
        self.role = Segmented([("driver", "운전자(운임료 지급)"), ("passenger", "동승자(운임료 제외, 일비만)")], t.get("carRole") or "driver")
        cv.addWidget(Field("탑승 구분", self.role))
        cg = FormGrid(2)
        self.fuel = _combo(L.CAR_FUEL_OPTIONS, t.get("fuelType") or "GASOLINE")
        self.cartype = _combo(L.CAR_TYPE_OPTIONS, t.get("carType") or 3)
        self.oil = _spin(0, 99999, 10, int(t.get("oilPrice") or page.best_price()))
        self.rate = _spin(0, 99999, 1, int(round(t.get("rate") or 0)))
        self.unit = QLabel("원/L")
        self.src = QLabel("예시값(직접 확인 후 수정 권장)")
        self.src.setWordWrap(True)
        refresh = Button("유가 조회", "ghost", "refresh", "sm")
        self.toll = _spin(0, 9999999, 1000, int(t.get("toll") or 0))
        self.toll_rc = QCheckBox("통행료 영수증 첨부함")
        self.toll_rc.setChecked(bool(t.get("tollReceipt")))
        cg.add(Field("유종", self.fuel))
        cg.add(Field("차종 (통행료 참고용 · 운임단가에는 영향 없음)", self.cartype))
        cg.add(Field("유가 (원/L · 전기 원/kWh · 수소 원/kg)", _row(self.oil, self.unit, refresh)))
        cg.add(Field("운임단가(원/km) = 유가 ÷ 유종별 연비", self.rate))
        cg.add(Field("통행료(원)", self.toll))
        cg.add(Field("", self.toll_rc))
        cv.addWidget(cg)
        cv.addWidget(self.src)
        cv.addWidget(Note("같은 차로 함께 이동한 인원이 여러 명이면, <b>운전자 1명만 \"운전자\"</b>로 선택하고 나머지는 \"동승자\"로 선택하세요. 동승자는 자차 운임료(거리×단가+통행료)를 받지 않고 "
                          "일비·식비·숙박비만 지급됩니다. 여러 명에게 한 번에 거리를 적용하려면 \"거리 계산기·자차 입력\" 단계를 이용하세요.", "info"))
        v.addWidget(self.car)
        self.oilrow = OilRow(page, self.fuel, self.oil, self.rate, self.unit, self.src, lambda: self.start_str())
        self.fuel.currentIndexChanged.connect(self.oilrow.refresh)
        refresh.clicked.connect(self.oilrow.refresh)
        self.start.dateTimeChanged.connect(self.oilrow.update_rate)
        # ── 기차 ──
        self.train = QWidget()
        tv = QVBoxLayout(self.train)
        tv.setContentsMargins(0, 0, 0, 0)
        tv.setSpacing(14)
        self.one_way = QCheckBox("왕복이 아니라 가는 편·오는 편을 따로 입력(편도, 귀임 교통수단이 다를 때)")
        self.one_way.setChecked(bool(t.get("trainOneWay")))
        self.one_way.toggled.connect(self.toggle)
        tv.addWidget(self.one_way)
        self.round_box = QWidget()
        rb = QVBoxLayout(self.round_box)
        rb.setContentsMargins(0, 0, 0, 0)
        rb.setSpacing(10)
        self.fare = _spin(0, 9999999, 100, int(t.get("trainFare") or 0))
        self.ticket = QCheckBox("승차권 첨부함")
        self.ticket.setChecked(bool(t.get("trainTicket")))
        rb.addWidget(Field("기차/KTX 요금(원)", self.fare, "왕복 합계 금액입니다. 승차권(매표)에 적힌 금액을 직접 입력해주세요."))
        rb.addWidget(self.ticket)
        tv.addWidget(self.round_box)
        self.ow_box = QWidget()
        ob = QVBoxLayout(self.ow_box)
        ob.setContentsMargins(0, 0, 0, 0)
        ob.setSpacing(10)
        self.fare_go = _spin(0, 9999999, 100, int(t.get("trainFareGo") or 0))
        self.ticket_go = QCheckBox("가는 편 승차권 첨부함")
        self.ticket_go.setChecked(bool(t.get("trainTicketGo")))
        self.ret_tr = _combo(L.RETURN_TRANSPORTS, t.get("returnTransport") or "기차")
        self.ret_tr.currentIndexChanged.connect(self.toggle)
        self.fare_ret = _spin(0, 9999999, 100, int(t.get("trainFareReturn") or 0))
        self.ticket_ret = QCheckBox("오는 편 승차권 첨부함")
        self.ticket_ret.setChecked(bool(t.get("trainTicketReturn")))
        ob.addWidget(label("▸ 가는 편(출발 → 목적지)", "muted"))
        ob.addWidget(Field("가는 편 요금(원)", self.fare_go))
        ob.addWidget(self.ticket_go)
        ob.addWidget(label("▸ 오는 편(목적지 → 복귀)", "muted"))
        ob.addWidget(Field("오는 편 교통수단", self.ret_tr, "오는 편을 관용차·대중교통으로 선택하면 오는 편 교통비는 지급되지 않습니다(무료)."))
        self.ret_box = QWidget()
        rv = QVBoxLayout(self.ret_box)
        rv.setContentsMargins(0, 0, 0, 0)
        rv.setSpacing(10)
        rv.addWidget(Field("오는 편 요금(원)", self.fare_ret))
        rv.addWidget(self.ticket_ret)
        ob.addWidget(self.ret_box)
        tv.addWidget(self.ow_box)
        v.addWidget(self.train)
        # ── 숙박·비고 ──
        lg = FormGrid(2)
        self.nights = _spin(0, 365, 1, int(t.get("nights") or 0))
        self.region = _combo(L.LODGE_REGIONS, t.get("lodgeRegion") or L.guess_lodge_region(t["dest"]))
        self.lodge = _spin(0, 99999999, 1000, int(t.get("lodge") or 0))
        self.notes = QLineEdit(t.get("notes") or "")
        self.notes.setPlaceholderText("선택 입력")
        lg.add(Field("숙박 일수", self.nights))
        lg.add(Field("숙박 지역", self.region))
        lg.add(Field("숙박비(영수증)", self.lodge))
        lg.add(Field("비고", self.notes))
        v.addWidget(lg)
        v.addStretch(1)
        # ── 버튼 ──
        bt = QHBoxLayout()
        bt.addStretch(1)
        c = Button("취소", "ghost")
        c.clicked.connect(self.reject)
        s = Button("저장", "primary", "save")
        s.clicked.connect(self.save)
        bt.addWidget(c)
        bt.addWidget(s)
        outer.addLayout(bt)
        self.toggle()

    # ---- 도우미 ----
    def _dt(self, s):
        w = QDateTimeEdit()
        w.setDisplayFormat("yyyy-MM-dd HH:mm")
        w.setCalendarPopup(True)
        w.setMinimumDateTime(QDateTime(QDate(2000, 1, 1), QTime(0, 0)))
        w.setSpecialValueText("(미정)")
        d = L.parse_dt(s)
        if d:
            w.setDateTime(QDateTime(QDate(d.year, d.month, d.day), QTime(d.hour, d.minute)))
        else:
            w.setDateTime(w.minimumDateTime())
        return w

    @staticmethod
    def _dts(w):
        return "" if w.dateTime() == w.minimumDateTime() else w.dateTime().toString("yyyy-MM-dd'T'HH:mm")

    def start_str(self):
        return self._dts(self.start)

    def on_pick(self):
        i = self.pick.currentData()
        if i == "":
            return
        s = self.page.staff[int(i)]
        self.name.setText(s.get("name", ""))
        self.position.setText(s.get("position") or "")
        self.dept.setText(s.get("dept") or self.page.default_dept())

    def on_dest(self):
        self.region.setCurrentIndex(max(0, self.region.findData(L.guess_lodge_region(self.dest.text()))))

    def toggle(self, *_):
        tr = self.transport.currentData()
        self.car.setVisible(tr == "자차")
        self.train.setVisible(tr == "기차")
        ow = self.one_way.isChecked()
        self.round_box.setVisible(not ow)
        self.ow_box.setVisible(ow)
        self.ret_box.setVisible(self.ret_tr.currentData() == "기차")

    def save(self):
        name = self.name.text().strip()
        if not name:
            QMessageBox.warning(self, "출장자", "성명을 입력해주세요.")
            return
        s = self.start_str()
        rate = self.rate.value() or L.calc_rate_per_km("GASOLINE", self.page.best_price(), s)
        rec = {
            "id": (self.editing or {}).get("id") or L.uid(), "name": name, "position": self.position.text(), "dept": self.dept.text(),
            "start": s, "end": self._dts(self.end), "dest": self.dest.text(), "purpose": self.purpose.text(), "grade": self.grade.text(),
            "transport": self.transport.currentData(), "carRole": self.role.value(), "distance": self.distance.value(),
            "fuelType": self.fuel.currentData() or "GASOLINE", "carType": self.cartype.currentData() or 3, "oilPrice": self.oil.value(), "rate": rate,
            "toll": self.toll.value(), "tollReceipt": self.toll_rc.isChecked(), "trainFare": self.fare.value(), "trainTicket": self.ticket.isChecked(),
            "trainOneWay": self.one_way.isChecked(), "trainFareGo": self.fare_go.value(), "trainTicketGo": self.ticket_go.isChecked(),
            "returnTransport": self.ret_tr.currentData(), "trainFareReturn": self.fare_ret.value(), "trainTicketReturn": self.ticket_ret.isChecked(),
            "nights": self.nights.value(), "lodgeRegion": self.region.currentData(), "lodge": self.lodge.value(), "notes": self.notes.text(),
            "mealOverride": None, "mealFeeOverride": None, "dailyOverride": None, "transOverride": None, "lodgeOverride": None,
        }
        if self.editing:        # 이전에 직접 고친 금액은 그대로 유지, 결의서에서 고친 은행·계좌도 유지
            for k in ("mealOverride", "mealFeeOverride", "dailyOverride", "transOverride", "lodgeOverride", "bankName", "account"):
                if k in self.editing:
                    rec[k] = self.editing[k]
        self.result_rec = rec
        self.accept()


# ───────────────────────────── 본 화면 ─────────────────────────────
class OutstationPage(ToolPage):
    def build(self):
        st = self.ctx.settings
        self.staff: list = st.get("staff_db", []) or []
        self.trips: list = st.get("trip_db", []) or []
        self.opinet_cache = st.get("opinet_cache")
        self.last_dist = None
        self.doc_ids = None            # 결의서 대상으로 체크된 출장 id
        self.known_ids = set()
        self.expanded = set()
        self.tpl_bytes = default_template()
        self.tpl_name = L.DEFAULT_TEMPLATE_NAME
        self.hwpx_done = False
        self.last_path = None
        self._cbs = {}
        self._seq = 0
        self._shown = False
        self._dirty = set()
        self._timer = QTimer(self)
        self._timer.setSingleShot(True)
        self._timer.setInterval(500)
        self._timer.timeout.connect(self.flush)
        if L.migrate_old_rates(self.trips, L.best_known_oil_price("GASOLINE", self.opinet_cache)):
            self.save_trips()
        self.build_staff()
        self.build_trips()
        self.build_distance()
        self.build_calc()
        self.build_output()
        self.render_staff()
        self.render_trips()
        self.refresh_all()

    # ═══════════════ 공통 도우미 ═══════════════
    def default_dept(self) -> str:
        return self.ctx.info("dept") or L.DEFAULT_DEPT

    def kakao_key(self) -> str:
        return self.k_kakao.text().strip() or self.ctx.key("kakao") or L.MAP_API_KEY

    def opinet_key(self) -> str:
        return self.k_opinet.text().strip() or self.ctx.key("opinet") or L.OPINET_API_KEY

    def best_price(self, fuel="GASOLINE"):
        return L.best_known_oil_price(fuel, self.opinet_cache)

    def save_opinet_cache(self, lst):
        self.opinet_cache = {"list": lst, "ts": int(time.time() * 1000)}
        self.ctx.settings.set("opinet_cache", self.opinet_cache)

    def mk_trip(self, f):
        return L.new_trip(f, self.staff, self.default_dept(), self.best_price())

    def bg(self, fn, cb):
        """fn() 을 뒤에서 돌리고 끝나면 cb(ok, 결과|오류문구) 를 화면 스레드에서 부른다"""
        self._seq += 1
        token = self._seq
        self._cbs[token] = cb

        def job(progress=None):
            try:
                return token, True, fn()
            except Exception as e:  # noqa
                return token, False, f"{type(e).__name__}: {e}"
        run_async(job, on_done=self._bg_done)

    def _bg_done(self, res):
        token, ok, val = res
        cb = self._cbs.pop(token, None)
        if cb:
            try:
                cb(ok, val)
            except RuntimeError:     # 이미 닫힌 창
                pass

    def save_staff(self):
        self._dirty.add("staff")
        self._timer.start()

    def save_trips(self):
        self._dirty.add("trips")
        self._timer.start()

    def flush(self):
        st = self.ctx.settings
        if "staff" in self._dirty:
            st.set("staff_db", self.staff, save=False)
        if "trips" in self._dirty:
            st.set("trip_db", self.trips, save=False)
        if self._dirty:
            st.set("outstation_saved_at", time.strftime("%Y-%m-%dT%H:%M:%S"), save=False)
            st.save()
        self._dirty.clear()

    def hideEvent(self, e):
        self.flush()
        super().hideEvent(e)

    def msg(self, title, text):
        QMessageBox.information(self, title, text)

    def refresh_all(self):
        self.render_calc()
        self.render_doc_select()
        self.render_dist_apply()
        self.update_status()

    def update_status(self):
        t = self.trips
        cars = [x for x in t if x.get("transport") == "자차"]
        dist_ok = bool(t) and all((x.get("distance") or 0) > 0 for x in cars)
        for card, done in ((self.c1, bool(self.staff)), (self.c2, bool(t)), (self.c3, dist_ok), (self.c4, bool(t)), (self.c51, self.hwpx_done)):
            card.badge.set_done(done)

    # ═══════════════ 1. 부서원 계좌 ═══════════════
    def build_staff(self):
        c = self.card("1", "부서원 계좌 정보", "지출결의서 발행에 쓰일 부서원 이름·직급·계좌번호를 관리합니다. 공식 \"거래처입력\" 엑셀 양식으로 언제든 저장/불러오기 할 수 있습니다.")
        self.c1 = c
        bar = QHBoxLayout()
        bar.setSpacing(8)
        add = Button("부서원 추가", "primary", "plus")
        add.clicked.connect(self.add_staff)
        imp = Button("엑셀 불러오기(거래처입력 양식)", "ghost", "upload")
        imp.clicked.connect(self.pick_staff_file)
        exp = Button("엑셀 내보내기(거래처입력 양식)", "ghost", "excel")
        exp.clicked.connect(self.export_staff)
        clr = Button("전체 삭제", "danger", "trash")
        clr.clicked.connect(self.clear_staff)
        for b in (add, imp, exp):
            bar.addWidget(b)
        bar.addStretch(1)
        bar.addWidget(clr)
        c.add_layout(bar)
        dz = DropZone("거래처입력 엑셀을 여기로 끌어다 놓으세요", "파일을 이 영역으로 드래그하거나 클릭해서 선택하면 바로 불러옵니다.", [".xlsx", ".xls"], compact=True, kind="엑셀 파일")
        dz.picked.connect(lambda fs: self.import_staff(fs[0]))
        c.add(dz)
        self.stable = DataTable([("#", 44, "c"), ("성명", 110, "l"), ("부서", 160, "l"), ("직급", 160, "l"), ("은행코드", 90, "l"), ("은행명", 140, "l"), ("계좌번호", None, "l"),
                                ("삭제", 64, "c")], row_height=48, max_rows=8)
        c.add(self.stable)
        c.add(label("※ 은행코드는 공식 거래처입력 양식 기준입니다 (예: 011=농협은행, 004=국민은행, 090=카카오뱅크, 092=토스뱅크). 코드를 모르면 은행명만 입력해도 계산에는 문제 없습니다.", "small", wrap=True))

    def render_staff(self):
        self.stable.clear()
        for i, s in enumerate(self.staff):
            def ed(field, ph, i=i):
                return cell_edit(s.get(field) or "", ph, lambda tx, f=field, k=i: self._upd_staff(k, f, tx))
            d = Button("", "flat", "trash", "icon")
            d.setToolTip("삭제")
            d.clicked.connect(lambda _=False, k=i: self.del_staff(k))
            self.stable.add_row([cell_text(str(i + 1), "c"), ed("name", "성명"), ed("dept", "부서"), ed("position", "직급"), ed("bankCode", "011"), ed("bankName", "은행명"),
                                 ed("account", "계좌번호"), cell_center(d)])

    def _upd_staff(self, i, field, val):
        self.staff[i][field] = val
        self.save_staff()

    def add_staff(self):
        self.staff.append({"name": "", "dept": self.default_dept(), "position": "", "bankCode": "", "bankName": "", "account": ""})
        self.save_staff()
        self.render_staff()
        self.stable.table.scrollToBottom()
        self.update_status()

    def del_staff(self, i):
        self.staff.pop(i)
        self.save_staff()
        self.render_staff()
        self.refresh_all()

    def clear_staff(self):
        if QMessageBox.question(self, "부서원", "부서원 명단을 전부 삭제할까요?") != QMessageBox.Yes:
            return
        self.staff.clear()
        self.save_staff()
        self.render_staff()
        self.refresh_all()

    def pick_staff_file(self):
        f, _ = QFileDialog.getOpenFileName(self, "거래처입력 엑셀 선택", "", "엑셀 파일 (*.xlsx *.xls)")
        if f:
            self.import_staff(f)

    def import_staff(self, path):
        from ...core import excel
        dept = self.default_dept()

        def work():
            return L.parse_staff_rows(L.pick_staff_sheet(excel.read_sheets(path)), dept)

        def done(ok, res):
            if not ok:
                QMessageBox.warning(self, "불러오기", f"파일을 읽는 중 오류가 발생했습니다: {res}")
                return
            self.staff.extend(res)
            self.save_staff()
            self.render_staff()
            self.refresh_all()
            self.toast(f"{len(res)}명을 불러왔습니다.")
        self.bg(work, done)

    def export_staff(self):
        fn = ask_save(self, "거래처입력_부서원계좌.xlsx", XLSX)
        if not fn:
            return
        try:
            L.build_staff_workbook(self.staff).save(fn)
        except Exception as e:
            QMessageBox.warning(self, "저장", f"저장 중 오류: {e}")
            return
        self.saved(fn)

    def saved(self, fn):
        self.last_path = fn
        self.toast("저장했습니다 — " + os.path.basename(fn))

    # ═══════════════ 2. 출장 내역 ═══════════════
    def build_trips(self):
        c = self.card("2", "출장 내역 입력", "출장자별 출장 일시·교통수단·숙박 정보를 입력합니다. <b>관외출장 집계표, 출장월별집계표, 기안상신내역서, 출장내역서</b> 파일(엑셀 .xlsx/.xls 또는 PDF)을 그대로 불러오거나, "
                      "직접 한 명씩 추가할 수 있습니다. 정해진 양식이 아니더라도 표 안에 성명·출장일시(또는 출장기간)·출장지·출장목적 같은 정보가 있으면 헤더를 스스로 찾아 읽어들입니다.")
        self.c2 = c
        bar = QHBoxLayout()
        bar.setSpacing(8)
        add = Button("출장자 추가(직접입력)", "primary", "plus")
        add.clicked.connect(lambda: self.open_trip(None))
        imp = Button("파일 불러오기(집계표/기안상신내역서/출장내역서)", "ghost", "upload")
        imp.clicked.connect(self.pick_trip_file)
        clr = Button("전체 삭제", "danger", "trash")
        clr.clicked.connect(self.clear_trips)
        bar.addWidget(add)
        bar.addWidget(imp)
        bar.addStretch(1)
        bar.addWidget(clr)
        c.add_layout(bar)
        dz = DropZone("집계표·기안상신내역서·출장내역서 파일을 여기로 끌어다 놓으세요", "엑셀(.xlsx/.xls) 또는 PDF · 정확히 같은 서식이 아니어도 표 안의 성명·일시·출장지 헤더를 인식해 읽습니다.",
                      [".xlsx", ".xls", ".pdf"], compact=True, kind="출장 파일")
        dz.picked.connect(lambda fs: self.import_trips(fs[0]))
        c.add(dz)
        self.ttable = DataTable([("#", 36, "c"), ("성명", 92, "l"), ("직급", 116, "l"), ("출장기간", 118, "l"), ("출장지", 92, "l"), ("출장목적", None, "l"), ("교통수단", 150, "l"),
                                 ("세부내역", 150, "l"), ("숙박", 84, "l"), ("관리", 76, "c")], row_height=66, max_rows=8)
        c.add(self.ttable)

    def _wrap(self, text, color=None):
        lb = QLabel(text)
        lb.setWordWrap(True)
        lb.setStyleSheet(f"font-size:12px; padding:0 8px; background:transparent; color:{color or theme.c('text')};")
        return lb

    def render_trips(self):
        self.ttable.clear()
        for i, t in enumerate(self.trips):
            txt, kind, warn = L.transport_badge(t)
            col = {"pri": theme.c("primary"), "ok": theme.c("success")}.get(kind, theme.c("text"))
            tw = QWidget()
            tl = QVBoxLayout(tw)
            tl.setContentsMargins(0, 2, 0, 2)
            tl.setSpacing(2)
            tl.addWidget(self._wrap(txt, col))
            if warn:
                tag = Tag("확인필요", "warn")
                tl.addWidget(tag, 0, Qt.AlignLeft)
            e = Button("", "flat", "pencil", "icon")
            e.setToolTip("수정")
            e.clicked.connect(lambda _=False, tid=t["id"]: self.open_trip(tid))
            d = Button("", "flat", "trash", "icon")
            d.setToolTip("삭제")
            d.clicked.connect(lambda _=False, tid=t["id"]: self.del_trip(tid))
            act = QWidget()
            al = QHBoxLayout(act)
            al.setContentsMargins(0, 0, 0, 0)
            al.setSpacing(2)
            al.setAlignment(Qt.AlignCenter)
            al.addWidget(e)
            al.addWidget(d)
            self.ttable.add_row([cell_text(str(i + 1), "c"), cell_text(t["name"], "l", bold=True), self._wrap(t.get("position") or "-"), self._wrap(L.fmt_range(t)),
                                 self._wrap(t.get("dest") or "-"), self._wrap(t.get("purpose") or "-"), tw, self._wrap(L.detail_text(t)),
                                 self._wrap(f"{t['nights']}박 {L.won(t.get('lodge'))}" if (t.get("nights") or 0) > 0 else "-"), act])

    def open_trip(self, tid):
        trip = next((x for x in self.trips if x["id"] == tid), None) if tid else None
        dlg = TripDialog(self, trip)
        if dlg.exec() != QDialog.Accepted:
            return
        rec = dlg.result_rec
        if trip:
            self.trips[self.trips.index(trip)] = rec
        else:
            self.trips.append(rec)
        self.after_trips()

    def after_trips(self):
        self.save_trips()
        self.render_trips()
        self.refresh_all()

    def del_trip(self, tid):
        if QMessageBox.question(self, "출장 내역", "이 출장 내역을 삭제할까요?") != QMessageBox.Yes:
            return
        self.trips[:] = [x for x in self.trips if x["id"] != tid]
        self.after_trips()

    def clear_trips(self):
        if QMessageBox.question(self, "출장 내역", "출장 내역을 전부 삭제할까요?") != QMessageBox.Yes:
            return
        self.trips.clear()
        self.after_trips()

    def pick_trip_file(self):
        f, _ = QFileDialog.getOpenFileName(self, "출장 파일 선택", "", "출장 파일 (*.xlsx *.xls *.pdf)")
        if f:
            self.import_trips(f)

    def import_trips(self, path):
        self.toast("파일을 읽는 중입니다…")
        staff, dept, price = list(self.staff), self.default_dept(), self.best_price()
        mk = lambda f: L.new_trip(f, staff, dept, price)  # noqa
        self.bg(lambda: L.import_trip_file(path, mk), self._trips_loaded)

    def _trips_loaded(self, ok, res):
        if not ok:
            QMessageBox.warning(self, "출장 파일", f"파일을 읽는 중 오류가 발생했습니다: {res}")
            return
        kind, lst = res
        if not lst:
            QMessageBox.warning(self, "출장 파일", f"[{kind}] 형식에서 관외출장 내역을 찾지 못했습니다. 서식을 확인해 주세요.")
            return
        self.trips.extend(lst)
        self.after_trips()
        need = sum(1 for t in lst if (t.get("notes") or "").startswith("⚠"))
        m = f"[{kind}] 서식에서 {len(lst)}건의 출장 내역을 불러왔습니다."
        m += (f" 이 중 {need}건은 관용차 미사용만 확인되어 실제 이동수단을 알 수 없어 \"확인필요\"로 표시했습니다 — 목록에서 확인 후 직접 수정해주세요."
              if need else " 자차인 경우 거리·운임을 확인해주세요.")
        QMessageBox.information(self, "불러오기 완료", m)

    # ═══════════════ 3. 거리 계산기 ═══════════════
    def build_distance(self):
        c = self.card("3", "거리 계산기 · 자차 이용 입력", "출발지·도착지 주소로 왕복 거리(km)를 자동 계산해 출장자에게 바로 적용합니다. 자차로 여러 명이 함께 이동하는 경우, "
                      "<b>운전자 1명만 자차 운임료를 지급받고 동승자는 운임료 없이 일비만 지급</b>됩니다.")
        self.c3 = c
        acc = Accordion("API 키", "내장 키 사용 중")
        self.k_kakao = QLineEdit()
        self.k_kakao.setEchoMode(QLineEdit.Password)
        self.k_kakao.setPlaceholderText("비워두면 설정 화면의 키 → 내장 키 순서로 사용합니다")
        self.k_opinet = QLineEdit()
        self.k_opinet.setEchoMode(QLineEdit.Password)
        self.k_opinet.setPlaceholderText("비워두면 설정 화면의 키 → 내장 키 순서로 사용합니다")
        kg = FormGrid(2)
        kg.add(Field("카카오 API 키", self.k_kakao))
        kg.add(Field("오피넷 API 키", self.k_opinet))
        acc.body.addWidget(kg)
        c.add(acc)
        g = FormGrid(2)
        self.origin = QLineEdit(L.DEFAULT_ORIGIN_ADDR)
        self.dest = QLineEdit()
        self.dest.setPlaceholderText("예: 대한민국 국회")
        b1 = Button("검색", "ghost", "search", "sm")
        b1.clicked.connect(lambda: self.addr_search("origin"))
        b2 = Button("검색", "ghost", "search", "sm")
        b2.clicked.connect(lambda: self.addr_search("dest"))
        g.add(Field("출발지", _row(self.origin, b1)))
        g.add(Field("도착지(출장지)", _row(self.dest, b2)))
        self.d_fuel = _combo(L.CAR_FUEL_OPTIONS, "GASOLINE")
        self.d_car = _combo(L.CAR_TYPE_OPTIONS, 3)
        g.add(Field("유종", self.d_fuel))
        g.add(Field("차종 (카카오모빌리티 통행료 참고용 · 운임단가에는 영향 없음)", self.d_car))
        self.d_oil = _spin(0, 99999, 10, self.best_price())
        self.d_unit = QLabel("원/L")
        self.d_src = QLabel("예시값(직접 확인 후 수정 권장)")
        self.d_src.setWordWrap(True)
        rf = Button("유가 조회", "ghost", "refresh", "sm")
        self.d_rate = _spin(0, 99999, 1, 0)
        self.d_rate.setEnabled(False)
        g.add(Field("도착지역 유가", _row(self.d_oil, self.d_unit, rf)))
        g.add(Field("운임단가(원/km)", self.d_rate))
        c.add(g)
        c.add(self.d_src)
        self.d_oilrow = OilRow(self, self.d_fuel, self.d_oil, self.d_rate, self.d_unit, self.d_src)
        self.d_fuel.currentIndexChanged.connect(self.d_oilrow.refresh)
        rf.clicked.connect(self.d_oilrow.refresh)
        self.d_oilrow.update_rate()
        row = QHBoxLayout()
        row.setSpacing(8)
        self.d_run = Button("왕복 거리 계산", "primary", "route")
        self.d_run.clicked.connect(self.run_distance)
        self.d_map = Button("카카오맵에서 경로 확인하기", "ghost", "map")
        self.d_map.setEnabled(False)
        self.d_map.clicked.connect(lambda: QDesktopServices.openUrl(QUrl(self.last_dist["mapUrl"])) if self.last_dist else None)
        row.addWidget(self.d_run)
        row.addWidget(self.d_map)
        row.addStretch(1)
        c.add_layout(row)
        self.d_res = Note("아직 계산된 거리가 없습니다.", "plain")
        c.add(self.d_res)
        self.d_warn = Note("", "warn")
        self.d_warn.hide()
        c.add(self.d_warn)
        # 적용 인원
        self.d_apply = QWidget()
        al = QVBoxLayout(self.d_apply)
        al.setContentsMargins(0, 0, 0, 0)
        al.setSpacing(12)
        self.d_apply_note = Note("이번 자차 동행 인원을 선택하세요. <b>운전자는 1명만</b> 선택하며, 자차 운임료는 운전자에게만 지급되고 동승자는 운임료 없이 일비만 지급됩니다.", "info")
        al.addWidget(self.d_apply_note)
        self.d_empty = Empty("먼저 \"출장 내역 입력\" 단계에서 출장자를 등록해주세요.")
        al.addWidget(self.d_empty)
        self.d_tbl = DataTable([("운전자", 80, "c"), ("동승", 70, "c"), ("성명", 130, "l"), ("출장지", None, "l")], row_height=44, max_rows=7)
        al.addWidget(self.d_tbl)
        self.d_btn = Button("선택 인원에게 왕복 거리 적용", "primary", "check")
        self.d_btn.clicked.connect(self.apply_distance)
        al.addWidget(self.d_btn, 0, Qt.AlignLeft)
        self.d_apply.hide()
        c.add(self.d_apply)
        c.add(label("※ API 키가 없어도 거리 계산은 동작합니다: 주소검색은 무료 오픈스트리트맵으로, 거리는 두 지점의 직선거리에 도로 보정계수(1.3)를 곱한 추정값으로 대체됩니다. "
                    "출발지를 따로 입력하지 않으면 익산시청으로 고정됩니다. 운임단가(원/km) = 유가 ÷ 유종별 연비이며, \"유가 조회\"는 오피넷 전국 평균 유가를 채웁니다"
                    "(도착지역 유가는 직접 확인해 덮어쓰는 것을 권장합니다). 계산된 단가는 각 출장자 입력 창에서 직접 수정할 수도 있습니다.", "small", wrap=True))
        self.d_group = QButtonGroup(self)
        self.d_checks = []

    def addr_search(self, which):
        src = self.origin if which == "origin" else self.dest
        dlg = AddrDialog(self, "출발지 주소 검색" if which == "origin" else "도착지(출장지) 주소 검색", src.text().strip())
        if dlg.exec() == QDialog.Accepted and dlg.chosen:
            src.setText(dlg.chosen)

    def run_distance(self):
        dest = self.dest.text().strip()
        if not dest:
            QMessageBox.information(self, "거리 계산", "도착지(출장지)를 입력해주세요.")
            return
        self.d_run.setEnabled(False)
        self.d_res.set_text("계산 중...", "plain")
        self._wait_oil(time.time())

    def _wait_oil(self, t0):
        """오피넷 유가 조회가 아직 진행 중이면 끝날 때까지(최대 25초) 기다린 뒤 최신 유가로 계산"""
        if self.d_oilrow.pending and time.time() - t0 < 25:
            self.d_res.set_text("오피넷 유가 조회 결과를 기다리는 중...", "plain")
            QTimer.singleShot(250, lambda: self._wait_oil(t0))
            return
        fuel = self.d_fuel.currentData()
        price = self.d_oil.value()
        if not price > 0:
            price = self.best_price(fuel)
            self.d_oilrow.set_price(price)
        origin, dest, car, key = self.origin.text(), self.dest.text().strip(), self.d_car.currentData(), self.kakao_key()
        self.d_res.set_text("계산 중...", "plain")
        self.bg(lambda: L.run_distance_calc(origin, dest, fuel, car, price, key), self._dist_done)

    def _dist_done(self, ok, res):
        self.d_run.setEnabled(True)
        if not ok:
            self.d_res.set_text(L.escape_xml(res), "err")
            return
        self.last_dist = res
        self.d_rate.setValue(res["rate"])
        self.d_res.set_text(L.format_dist_result(res), "info")
        w = L.dist_warning(res)
        self.d_warn.setVisible(bool(w))
        if w:
            self.d_warn.set_text(w, "warn")
        self.d_map.setEnabled(True)
        self.render_dist_apply()

    def render_dist_apply(self):
        if not hasattr(self, "d_apply"):
            return
        self.d_apply.setVisible(self.last_dist is not None)
        has = bool(self.trips)
        self.d_empty.setVisible(not has)
        self.d_tbl.setVisible(has)
        self.d_btn.setVisible(has)
        self.d_apply_note.setVisible(has)
        for b in self.d_group.buttons():
            self.d_group.removeButton(b)
        self.d_checks = []
        self.d_tbl.clear()
        for t in self.trips:
            rb = QRadioButton()
            self.d_group.addButton(rb)
            cb = QCheckBox()
            self.d_checks.append((t["id"], rb, cb))
            self.d_tbl.add_row([cell_center(rb), cell_center(cb), cell_text(t["name"], "l", bold=True), cell_text(t.get("dest") or "-", "l", small=True)])

    def apply_distance(self):
        if not self.last_dist:
            QMessageBox.information(self, "거리 적용", "먼저 거리를 계산해주세요.")
            return
        driver = next((i for i, rb, cb in self.d_checks if rb.isChecked()), None)
        riders = [i for i, rb, cb in self.d_checks if cb.isChecked() and i != driver]
        if not driver and not riders:
            QMessageBox.information(self, "거리 적용", "운전자 또는 동승자를 1명 이상 선택해주세요.")
            return
        n = L.apply_distance(self.trips, self.last_dist, driver, riders)
        self.after_trips()
        self.toast(f"{n}명에게 왕복 {L.num_str(self.last_dist['roundKm'])}km를 적용했습니다.")

    # ═══════════════ 4. 여비 계산 결과 ═══════════════
    def build_calc(self):
        c = self.card("4", "여비 계산 결과", "아래 계산 규칙에 따라 자동 산출됩니다. 금액이 다른 특별한 사정이 있으면 표에서 직접 수정할 수 있습니다(수정한 금액은 <b>파란색</b>으로 표시됩니다).")
        self.c4 = c
        c.add(Note("<b>적용 규칙</b> — 일비: 4시간 이상 출장 시 최대 25,000원 (공무용차량 사용 시 절반인 12,500원), 4시간 미만은 미지급 · "
                   "식비: 출장 시 기본 1끼(9,000원) 제공, 점심시간(11:00~13:00)이 포함되면 2끼(18,000원), 09:00~18:00 범위를 벗어나거나 왕복 100km 이상 원거리 출장이면 3끼(25,000원) · "
                   "교통비: 기차는 승차권 첨부 시 요금 지급, 자차는 거리(km)×운임단가(유가÷유종별 연비) + 통행료(영수증 첨부 시) · "
                   "숙박비: 1박 기준 서울 100,000원, 광역시 80,000원, 그 밖의 지역 70,000원 한도 내에서 영수증 금액 지급", "info"))
        self.cstats = StatRow()
        c.add(self.cstats)
        self.calc_empty = Empty("출장 내역을 입력하면 여기에 계산 결과가 나타납니다.")
        c.add(self.calc_empty)
        self.ctable = DataTable([("성명", 84, "l"), ("계좌번호", 150, "l"), ("출장기간", None, "l"), ("산정식수", 84, "c"), ("일비", 106, "r"), ("식비", 106, "r"), ("교통비", 106, "r"),
                                 ("숙박비", 106, "r"), ("합계", 110, "r")], row_height=48, max_rows=8)
        c.add(self.ctable)
        self.crow = {}

    def _amt_edit(self, t, field, value, active, setter):
        e = cell_edit(str(value), "", align=Qt.AlignRight)
        self._mark(e, active)
        e.editingFinished.connect(lambda ed=e: setter(t["id"], ed.text().strip()))
        return e

    def _mark(self, e, active):
        e.setStyleSheet(f"color:{theme.c('primary')}; font-weight:700;" if active else "")

    def render_calc(self):
        t_all = self.trips
        self.calc_empty.setVisible(not t_all)
        self.ctable.setVisible(bool(t_all))
        self.ctable.clear()
        self.crow = {}
        sd = sm = st = sl = 0
        for t in t_all:
            daily, meal, trans, lodge = L.calc_daily(t), L.calc_meal_fee(t), L.calc_transport_fee(t), L.calc_lodge_fee(t)
            sd += daily
            sm += meal
            st += trans
            sl += lodge
            acct = L.find_account(self.staff, t["name"])
            combo = QComboBox()
            combo.setProperty("cell", True)
            for n in (1, 2, 3):
                combo.addItem(f"{n}끼", n)
            combo.setCurrentIndex(max(0, L.calc_meal_count(t) - 1))
            combo.currentIndexChanged.connect(lambda _=0, cb=combo, tid=t["id"]: self.ov_meal(tid, cb.currentData()))
            self._mark_combo(combo, t.get("mealOverride") is not None)
            ed_d = self._amt_edit(t, "dailyOverride", L.js_round(daily), t.get("dailyOverride") is not None, self.ov_daily)
            ed_m = self._amt_edit(t, "mealFeeOverride", L.js_round(meal), t.get("mealFeeOverride") is not None or t.get("mealOverride") is not None, self.ov_mealfee)
            ed_t = self._amt_edit(t, "transOverride", L.js_round(trans), t.get("transOverride") is not None, self.ov_trans)
            ed_l = self._amt_edit(t, "lodgeOverride", L.js_round(lodge), t.get("lodgeOverride") is not None, self.ov_lodge)
            tot = cell_text(L.won(daily + meal + trans + lodge), "r", bold=True)
            self.crow[t["id"]] = (combo, ed_d, ed_m, ed_t, ed_l, tot)
            acc = cell_text(acct, "l", small=True) if acct else cell_text("미등록", "l", small=True, color=theme.c("danger"))
            self.ctable.add_row([cell_text(t["name"], "l", bold=True), acc, cell_text(L.fmt_range(t), "l", small=True), combo, ed_d, ed_m, ed_t, ed_l, tot])
        self.cstats.set_items([("출장 건수", f"{len(t_all)}건"), ("일비 합계", L.won(sd)), ("식비 합계", L.won(sm)), ("교통비 합계", L.won(st)), ("숙박비 합계", L.won(sl)),
                               ("총 지급액", L.won(sd + sm + st + sl), True)])

    def _mark_combo(self, combo, active):
        combo.setStyleSheet(f"color:{theme.c('primary')}; font-weight:700;" if active else "")

    def update_calc_row(self, tid):
        """표를 다시 그리지 않고(입력 포커스 유지) 한 줄과 요약만 갱신"""
        t = next((x for x in self.trips if x["id"] == tid), None)
        r = self.crow.get(tid)
        if not t or not r:
            return self.render_calc()
        combo, ed_d, ed_m, ed_t, ed_l, tot = r
        vals = (L.calc_daily(t), L.calc_meal_fee(t), L.calc_transport_fee(t), L.calc_lodge_fee(t))
        for e, v, act in ((ed_d, vals[0], t.get("dailyOverride") is not None), (ed_m, vals[1], t.get("mealFeeOverride") is not None or t.get("mealOverride") is not None),
                          (ed_t, vals[2], t.get("transOverride") is not None), (ed_l, vals[3], t.get("lodgeOverride") is not None)):
            e.blockSignals(True)
            e.setText(str(L.js_round(v)))
            e.blockSignals(False)
            self._mark(e, act)
        combo.blockSignals(True)
        combo.setCurrentIndex(max(0, L.calc_meal_count(t) - 1))
        combo.blockSignals(False)
        self._mark_combo(combo, t.get("mealOverride") is not None)
        tot.setText(L.won(sum(vals)))
        sd, sm, st, sl = (sum(L.calc_daily(x) for x in self.trips), sum(L.calc_meal_fee(x) for x in self.trips),
                          sum(L.calc_transport_fee(x) for x in self.trips), sum(L.calc_lodge_fee(x) for x in self.trips))
        self.cstats.set_items([("출장 건수", f"{len(self.trips)}건"), ("일비 합계", L.won(sd)), ("식비 합계", L.won(sm)), ("교통비 합계", L.won(st)), ("숙박비 합계", L.won(sl)),
                               ("총 지급액", L.won(sd + sm + st + sl), True)])

    def _after_override(self, tid):
        self.save_trips()
        self.update_calc_row(tid)
        self.update_doc_views(tid)

    def _trip(self, tid):
        return next(x for x in self.trips if x["id"] == tid)

    def ov_meal(self, tid, val):
        t = self._trip(tid)
        if val in ("", None):
            t["mealOverride"] = t["mealFeeOverride"] = None
        else:
            t["mealOverride"], t["mealFeeOverride"] = max(0, min(3, L.parse_int(val))), None
        self._after_override(tid)

    def ov_mealfee(self, tid, val):
        self._trip(tid)["mealFeeOverride"] = None if val == "" else L.parse_float(val)
        self._after_override(tid)

    def ov_daily(self, tid, val):
        self._trip(tid)["dailyOverride"] = L.parse_float(val)
        self._after_override(tid)

    def ov_trans(self, tid, val):
        self._trip(tid)["transOverride"] = None if val == "" else L.parse_float(val)
        self._after_override(tid)

    def ov_lodge(self, tid, val):
        self._trip(tid)["lodgeOverride"] = None if val == "" else L.parse_float(val)
        self._after_override(tid)

    # ═══════════════ 5. 서식 출력 ═══════════════
    def build_output(self):
        c = self.card("5-1", "출장자 선택 · 여비지출결의서 미리보기", "결의서를 만들 출장자를 체크하세요. 선택한 출장자마다 아래에 접힌 줄이 생기고, \"열기(수정)\"를 누르면 실제 .hwpx 서식과 같은 표 구성의 "
                      "미리보기와 수정 칸이 펼쳐집니다. 수정한 값은 즉시 저장되어 \".hwpx 생성\"에 그대로 반영됩니다. \"PDF로 미리보기\"는 인쇄·저장용 PDF를 만들어 엽니다.")
        self.c51 = c
        self.sel_empty = Empty("먼저 \"출장 내역 입력\" 단계에서 출장자를 등록해주세요.")
        c.add(self.sel_empty)
        self.sel_tbl = DataTable([("", 44, "c"), ("성명", 96, "l"), ("직급", 130, "l"), ("출장지", None, "l"), ("출장기간", 190, "l"), ("합계", 110, "r")], row_height=44, max_rows=8)
        c.add(self.sel_tbl)
        self.prev_box = QVBoxLayout()
        self.prev_box.setSpacing(10)
        c.add_layout(self.prev_box)
        self.prev_empty = Empty("위 목록에서 출장자를 선택하면 결의서 미리보기가 이곳에 표시됩니다.")
        c.add(self.prev_empty)
        # 5-2
        c2 = self.card("5-2", "여비지출결의서 (HWPX 원본 서식 그대로 생성)", "서식은 건드리지 않고, 결의서(.hwpx) 파일의 표·글자 배치·문구를 그대로 유지한 채 이름·금액·계좌 등 <b>내용만</b> 바꿔 넣어 새 .hwpx 파일을 만듭니다. "
                       "여러 명을 선택하면 원본처럼 한 파일 안에 인원수만큼 페이지가 이어집니다.")
        self.tpl_note = Note("", "ok")
        c2.add(self.tpl_note)
        self.set_tpl_note()
        row = QHBoxLayout()
        row.setSpacing(8)
        b1 = Button("다른 결의서(.hwpx) 서식으로 교체", "ghost", "file")
        b1.clicked.connect(self.pick_template)
        b2 = Button("기본 서식으로 되돌리기", "ghost", "refresh")
        b2.clicked.connect(self.reset_template)
        row.addWidget(b1)
        row.addWidget(b2)
        row.addStretch(1)
        c2.add_layout(row)
        dz = DropZone("결의서 .hwpx 서식 파일을 여기로 끌어다 놓으세요", "그 서식을 그대로 사용합니다. (서식의 표/문구/글씨체는 수정되지 않습니다)", [".hwpx"], compact=True, kind="한글 문서")
        dz.picked.connect(lambda fs: self.load_template(fs[0]))
        c2.add(dz)
        self.gen_btn = Button("선택한 출장자 여비지출결의서(.hwpx) 생성·다운로드", "primary", "down")
        self.gen_btn.clicked.connect(self.generate)
        c2.add(self.gen_btn)
        self.gen_note = Note("", "ok")
        self.gen_note.hide()
        c2.add(self.gen_note)
        self.open_btn = Button("저장한 폴더 열기", "ghost", "file", "sm")
        self.open_btn.clicked.connect(lambda: open_folder(self.last_path) if self.last_path else None)
        self.open_btn.hide()
        c2.add(self.open_btn)
        # 5-3
        c3 = self.card("5-3", "엑셀 서식 다운로드", "차세대 지방재정 시스템에 그대로 업로드할 수 있는 공식 엑셀 양식으로 내보냅니다.")
        r3 = QHBoxLayout()
        r3.setSpacing(8)
        e1 = Button("지급폼 다운로드 (거래처입력 양식 · 계좌번호 포함)", "primary", "excel")
        e1.clicked.connect(self.export_payment)
        e2 = Button("여비상세입력폼 다운로드", "primary", "excel")
        e2.clicked.connect(self.export_detail)
        r3.addWidget(e1)
        r3.addWidget(e2)
        r3.addStretch(1)
        c3.add_layout(r3)

    # ---- 선택 목록 ----
    def sync_doc_ids(self):
        cur = [t["id"] for t in self.trips]
        if self.doc_ids is None:
            self.doc_ids = set(cur)
        else:
            self.doc_ids &= set(cur)
            self.doc_ids |= {i for i in cur if i not in self.known_ids}     # 새로 추가된 출장자는 기본 선택
        self.known_ids = set(cur)

    def selected_trips(self):
        return [t for t in self.trips if self.doc_ids and t["id"] in self.doc_ids]

    def render_doc_select(self):
        self.sync_doc_ids()
        has = bool(self.trips)
        self.sel_empty.setVisible(not has)
        self.sel_tbl.setVisible(has)
        self.sel_tbl.clear()
        for t in self.trips:
            self.sel_tbl.add_row([cell_check(t["id"] in self.doc_ids, lambda on, tid=t["id"]: self.toggle_doc(tid, on)), cell_text(t["name"], "l", bold=True),
                                  cell_text(t.get("position") or "-", "l", small=True), cell_text(t.get("dest") or "-", "l", small=True), cell_text(L.fmt_range(t), "l", small=True),
                                  cell_text(L.won(L.calc_total(t)), "r", bold=True)])
        self.render_previews()

    def update_doc_views(self, tid=None):
        """금액이 바뀌었을 때: 선택 목록의 합계 칸과 열려 있는 미리보기를 갱신"""
        for r, t in enumerate(self.trips):
            if r < self.sel_tbl.row_count():
                w = self.sel_tbl.table.cellWidget(r, 5)
                if w:
                    w.setText(L.won(L.calc_total(t)))
        self.render_previews(only=tid)

    def toggle_doc(self, tid, on):
        (self.doc_ids.add if on else self.doc_ids.discard)(tid)
        self.render_previews()

    def person(self, t):
        return L.build_person_data(t, self.staff)

    def render_previews(self, only=None):
        sel = self.selected_trips()
        self.prev_empty.setVisible(not sel and bool(self.trips))
        have = {i: w for i, w in getattr(self, "_prev", {}).items()}
        if only and only in have and any(t["id"] == only for t in sel):
            have[only].refresh()
            return
        while self.prev_box.count():
            w = self.prev_box.takeAt(0).widget()
            if w:
                w.hide()
                w.setParent(None)
                w.deleteLater()
        self._prev = {}
        for t in sel:
            w = PreviewItem(self, t)
            self._prev[t["id"]] = w
            self.prev_box.addWidget(w)

    # ---- 서식 ----
    def set_tpl_note(self):
        self.tpl_note.set_text("현재 서식: <b>" + L.escape_xml(self.tpl_name) + "</b>", "ok")

    def pick_template(self):
        f, _ = QFileDialog.getOpenFileName(self, "결의서 서식 선택", "", "한글 문서 (*.hwpx)")
        if f:
            self.load_template(f)

    def load_template(self, path):
        if not path.lower().endswith(".hwpx"):
            QMessageBox.warning(self, "서식", ".hwpx 파일만 서식으로 사용할 수 있습니다.")
            return
        try:
            with open(path, "rb") as f:
                data = f.read()
            L.validate_template(data)
        except Exception:
            QMessageBox.warning(self, "서식", "올바른 결의서(.hwpx) 파일이 아닌 것 같습니다.")
            return
        self.tpl_bytes, self.tpl_name = data, os.path.basename(path)
        self.set_tpl_note()
        self.toast("새 결의서 서식을 적용했습니다: " + self.tpl_name)

    def reset_template(self):
        self.tpl_bytes, self.tpl_name = default_template(), L.DEFAULT_TEMPLATE_NAME
        self.set_tpl_note()
        self.toast("기본 서식으로 되돌렸습니다.")

    def generate(self):
        self.hwpx_done = True
        self.update_status()
        sel = self.selected_trips()
        if not sel:
            QMessageBox.information(self, "결의서", "출장자를 1명 이상 선택해주세요.")
            return
        fn = ask_save(self, f"여비지출결의서_{L.today_str()}.hwpx", "한글 문서 (*.hwpx)")
        if not fn:
            return
        people = [self.person(t) for t in sel]
        tpl = self.tpl_bytes
        self.gen_btn.setEnabled(False)
        self.gen_btn.setText("생성 중...")

        def done(ok, res):
            self.gen_btn.setEnabled(True)
            self.gen_btn.setText("선택한 출장자 여비지출결의서(.hwpx) 생성·다운로드")
            if not ok:
                QMessageBox.warning(self, "결의서", f"결의서 생성 중 오류가 발생했습니다: {res}")
                return
            self.last_path = fn
            self.gen_note.set_text(f"{len(people)}명분 여비지출결의서를 저장했습니다 — <b>{L.escape_xml(fn)}</b>", "ok")
            self.gen_note.show()
            self.open_btn.show()
            self.toast(f"{len(people)}명분 여비지출결의서(.hwpx)를 생성했습니다.")
        self.bg(lambda: L.generate_hwpx(tpl, people, fn), done)

    def export_payment(self):
        if not self.trips:
            QMessageBox.information(self, "엑셀", "출장 내역이 없습니다.")
            return
        fn = ask_save(self, f"관외출장_지급폼_{L.today_str()}.xlsx", XLSX)
        if fn:
            self._save_wb(lambda: L.build_payment_workbook(self.trips, self.staff), fn)

    def export_detail(self):
        if not self.trips:
            QMessageBox.information(self, "엑셀", "출장 내역이 없습니다.")
            return
        fn = ask_save(self, f"관외출장_여비상세폼_{L.today_str()}.xlsx", XLSX)
        if fn:
            self._save_wb(lambda: L.build_detail_workbook(self.trips, self.staff), fn)

    def _save_wb(self, build, fn):
        try:
            build().save(fn)
        except Exception as e:
            QMessageBox.warning(self, "저장", f"저장 중 오류: {e}")
            return
        self.saved(fn)

    def pdf_preview(self, t):
        from PySide6.QtGui import QPageLayout, QPageSize, QTextDocument
        from PySide6.QtPrintSupport import QPrinter
        html = L.build_receipt_html(self.person(t), pdf=True)
        path = os.path.join(tempfile.gettempdir(), f"여비지출결의서_{t['name']}_{L.today_str()}.pdf")
        pr = QPrinter(QPrinter.HighResolution)
        pr.setOutputFormat(QPrinter.PdfFormat)
        pr.setPageSize(QPageSize(QPageSize.A4))
        pr.setPageMargins(QMarginsF(14, 15, 14, 15), QPageLayout.Millimeter)
        pr.setOutputFileName(path)
        doc = QTextDocument()
        doc.setHtml(f'<body style="color:#000000;">{html}</body>')
        doc.print_(pr)
        QDesktopServices.openUrl(QUrl.fromLocalFile(path))

    # ═══════════════ 처음 열릴 때 ═══════════════
    def on_show(self):
        if self._shown:
            return
        self._shown = True
        QTimer.singleShot(300, self.d_oilrow.refresh)       # 거리계산용 유가를 한 번 조회 (실패해도 마지막 성공값 유지)
        if self.staff or self.trips:
            QTimer.singleShot(0, self.ask_startup)

    def ask_startup(self):
        parts = []
        if self.staff:
            parts.append(f"직원 {len(self.staff)}명")
        if self.trips:
            parts.append(f"출장 내역 {len(self.trips)}건")
        desc = ", ".join(parts) + "이(가) 저장되어 있습니다."
        ts = self.ctx.settings.get("outstation_saved_at")
        if ts:
            desc += f" (최근 저장: {str(ts).replace('T', ' ')[:16]})"
        desc += " 이어서 작업하시겠습니까, 새로 시작하시겠습니까?"
        box = QMessageBox(self)
        box.setWindowTitle("이전 작업이 있습니다")
        box.setText(desc)
        cont = box.addButton("이어서 작업하기", QMessageBox.AcceptRole)
        new = box.addButton("새로 시작하기", QMessageBox.DestructiveRole)
        cb = QCheckBox("새로 시작할 때 직원 명단도 함께 초기화")
        box.setCheckBox(cb)
        box.setDefaultButton(cont)
        box.exec()
        if box.clickedButton() is not new:
            return
        also = cb.isChecked()
        if QMessageBox.question(self, "새로 시작", "출장 내역과 직원 명단을 모두 초기화하고 새로 시작할까요?" if also else "출장 내역을 초기화하고 새로 시작할까요? (직원 명단은 유지됩니다)") != QMessageBox.Yes:
            return
        self.trips.clear()
        if also:
            self.staff.clear()
            self.save_staff()
            self.render_staff()
        self.after_trips()
        self.toast("새 작업을 시작합니다.")


# ───────────────────────────── 결의서 미리보기 한 줄 ─────────────────────────────
class PreviewItem(QWidget):
    """선택한 출장자 한 명: 접힌 줄(요약 + PDF 미리보기) → 펼치면 수정 칸 + 결의서 표 미리보기"""

    def __init__(self, page: OutstationPage, t: dict):
        super().__init__()
        self.page, self.tid = page, t["id"]
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(8)
        self.acc = Accordion(self._title(t), "수정 가능", open_=t["id"] in page.expanded)
        self.acc.head.clicked.connect(self._toggled)
        lay.addWidget(self.acc)
        b = self.acc.body
        top = QHBoxLayout()
        top.addStretch(1)
        pdf = Button("PDF로 미리보기", "ghost", "printer", "sm")
        pdf.clicked.connect(lambda: page.pdf_preview(self._t()))
        top.addWidget(pdf)
        b.addLayout(top)
        g = FormGrid(3)
        self.purpose = QLineEdit(t.get("purpose") or "")
        self.dest = QLineEdit(t.get("dest") or "")
        p = page.person(t)
        self.bank = QLineEdit(p["bankName"])
        self.acct = QLineEdit(p["account"])
        self.purpose.editingFinished.connect(lambda: self._txt("purpose", self.purpose.text()))
        self.dest.editingFinished.connect(lambda: self._txt("dest", self.dest.text()))
        self.bank.editingFinished.connect(lambda: self._txt("bankName", self.bank.text()))
        self.acct.editingFinished.connect(lambda: self._txt("account", self.acct.text()))
        g.add(Field("적요 / 용무", self.purpose), 2)
        g.add(Field("출장지", self.dest))
        g.add(Field("거래은행", self.bank))
        g.add(Field("계좌번호", self.acct), 2)
        self.meal_n = _combo([(1, "1끼"), (2, "2끼"), (3, "3끼")], 1)
        self.meal_n.currentIndexChanged.connect(lambda: page.ov_meal(self.tid, self.meal_n.currentData()))
        self.daily = QLineEdit()
        self.meal = QLineEdit()
        self.trans = QLineEdit()
        self.lodge = QLineEdit()
        for e, fn in ((self.daily, page.ov_daily), (self.meal, page.ov_mealfee), (self.trans, page.ov_trans), (self.lodge, page.ov_lodge)):
            e.setAlignment(Qt.AlignRight)
            e.editingFinished.connect(lambda e=e, fn=fn: fn(self.tid, e.text().strip().replace(",", "")))
        g.add(Field("식 (끼니)", self.meal_n))
        g.add(Field("일비 (원)", self.daily))
        g.add(Field("식비 (원)", self.meal))
        g.add(Field("교통비 (원)", self.trans))
        g.add(Field("숙박비 (원)", self.lodge))
        b.addWidget(g)
        rs = QHBoxLayout()
        rs.setSpacing(6)
        for text, keys in (("교통비 되돌리기", ["transOverride"]), ("일비 되돌리기", ["dailyOverride"]), ("식비 되돌리기", ["mealOverride", "mealFeeOverride"]),
                           ("숙박비 되돌리기", ["lodgeOverride"]), ("전체 계산값으로 되돌리기", ["dailyOverride", "mealOverride", "mealFeeOverride", "transOverride", "lodgeOverride"])):
            bt = Button(text, "ghost", None, "sm")
            bt.clicked.connect(lambda _=False, k=keys: self.reset(k))
            rs.addWidget(bt)
        rs.addStretch(1)
        b.addLayout(rs)
        self.browser = AutoBrowser()
        b.addWidget(self.browser)
        self.refresh(first=True)

    def _t(self):
        return next(x for x in self.page.trips if x["id"] == self.tid)

    def _title(self, t):
        return f"{t['name']} ({t.get('position') or '-'}) · {t.get('dest') or '-'} · {L.won(L.calc_total(t))}"

    def _toggled(self):
        (self.page.expanded.add if self.acc._open else self.page.expanded.discard)(self.tid)
        if self.acc._open:
            self.refresh()

    def _txt(self, field, val):
        t = self._t()
        cur = self.page.person(t)[field] if field in ("bankName", "account") else t.get(field)
        if (cur or "") == val:
            return
        t[field] = val
        self.page.save_trips()
        self.page.render_trips()
        self.page.render_calc()
        self.page.update_doc_views(self.tid)

    def reset(self, keys):
        t = self._t()
        for k in keys:
            t[k] = None
        self.page.save_trips()
        self.page.render_calc()
        self.page.update_doc_views(self.tid)
        if len(keys) > 1:
            self.page.toast("기본값으로 되돌렸습니다.")

    def refresh(self, first=False):
        t = self._t()
        p = self.page.person(t)
        self.acc.title.setText(self._title(t))
        for e, v in ((self.daily, p["dailyFee"]), (self.meal, p["mealFee"]), (self.trans, p["transportFee"]), (self.lodge, p["lodgeFee"])):
            e.blockSignals(True)
            e.setText(str(L.js_round(v)))
            e.blockSignals(False)
        self.meal_n.blockSignals(True)
        self.meal_n.setCurrentIndex(max(0, p["mealCount"] - 1))
        self.meal_n.blockSignals(False)
        if self.acc._open:
            self.browser.set_page(L.build_receipt_html(p))
