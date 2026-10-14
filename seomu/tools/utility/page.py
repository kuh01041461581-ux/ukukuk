# -*- coding: utf-8 -*-
import datetime
import os
import threading
import time
from PySide6.QtCore import Qt
from PySide6.QtGui import QImage, QPixmap
from PySide6.QtWidgets import QComboBox, QDialog, QDoubleSpinBox, QHBoxLayout, QLabel, QLineEdit, QScrollArea, QVBoxLayout, QWidget
from ...core.worker import run_async
from ...ui.theme import theme
from ...ui.widgets import (Accordion, Button, DataTable, DropZone, Empty, Field, FormGrid, Note, ProgressBlock, Segmented, StatRow, Tag, ask_save,
                           cell_center, cell_edit, check, label, open_folder)
from ..base import ToolPage
from . import data as D
from . import logic as L

KIND = {"대기": "neutral", "인식중": "pri", "완료": "ok", "확인필요": "warn", "오류": "err"}


def pix(im, w):
    im = im.convert("RGB")
    q = QImage(im.tobytes("raw", "RGB"), im.width, im.height, im.width * 3, QImage.Format_RGB888).copy()
    return QPixmap.fromImage(q).scaledToWidth(w, Qt.SmoothTransformation)


class Viewer(QDialog):
    def __init__(self, im, title, parent=None):
        super().__init__(parent)
        self.setWindowTitle(title)
        self.resize(820, 900)
        lay = QVBoxLayout(self)
        sc = QScrollArea()
        sc.setWidgetResizable(True)
        lb = QLabel()
        lb.setPixmap(pix(im, 780))
        lb.setAlignment(Qt.AlignHCenter | Qt.AlignTop)
        sc.setWidget(lb)
        lay.addWidget(sc)


class UtilityPage(ToolPage):
    def build(self):
        self.rows: list = []
        self.seq = 0
        self.tid = "water"
        self.running = False
        self.stop_flag = threading.Event()

        c1 = self.card("1", "고지서 종류 고르기", "종류를 바꾸면 표에 담긴 인식 결과가 초기화됩니다. 먼저 필요한 종류를 고른 뒤 고지서를 올려 주세요.")
        self.seg = Segmented([(k, f"{L.TYPES[k].label}  ·  {L.TYPES[k].tag}") for k in L.ORDER], "water")
        self.seg.changed.connect(self.set_type)
        c1.add(self.seg)
        self.sub = Note("", "plain")
        c1.add(self.sub)

        c2 = self.card("2", "AI 인식 설정", "고지서의 글자를 읽을 AI를 정합니다. 키는 이 PC 밖으로 보내지 않으며(해당 AI 서버 제외), 설정 화면에 저장해 두면 자동으로 채워집니다.")
        g = FormGrid(3)
        self.prov = Segmented([("gemini", "Google Gemini"), ("claude", "Claude")], "gemini")
        self.prov.changed.connect(self.on_prov)
        self.model = QComboBox()
        for v, t in L.GEMINI_MODELS:
            self.model.addItem(t, v)
        self.key = QLineEdit()
        self.key.setEchoMode(QLineEdit.Password)
        self.key.setPlaceholderText("API 키를 붙여넣으세요")
        self.fprov = Field("AI 공급자", self.prov)
        self.fmodel = Field("Gemini 모델", self.model)
        g.add(self.fprov)
        g.add(self.fmodel)
        g.add(Field("API 키", self.key, "Gemini: aistudio.google.com/apikey · Claude: platform.claude.com"))
        c2.add(g)
        acc = Accordion("고급 설정", "요청 간격 · 토큰 절약 · 정밀 확대")
        self.pacing = QDoubleSpinBox()
        self.pacing.setRange(0.5, 60)
        self.pacing.setSingleStep(0.5)
        self.pacing.setValue(7)
        self.pacing.setSuffix(" 초")
        acc.body.addWidget(Field("요청 간격", self.pacing, "Gemini 무료 등급은 분당 요청 수 제한이 있어요. 429 오류가 나면 간격을 늘리세요."))
        self.saver = check("토큰 절약 모드", "전체 페이지 이미지 해상도를 낮추고 확대 크롭도 줄여서 무료 등급 사용량을 아낍니다(Gemini).")
        self.crops = check("정밀 확대 인식 사용", "계좌·금액 부분을 잘라 확대한 이미지를 함께 보내 숫자 인식 정확도를 높입니다.", True)
        self.verify = check("계좌번호 2단계 재확인", "전기요금 · 장당 API 호출 +1회. 계좌 부분만 고해상도로 한 번 더 읽어 비교합니다.")
        for w in (self.saver, self.crops, self.verify):
            acc.body.addWidget(w)
        c2.add(acc)
        self.notice = Note("", "info")
        c2.add(self.notice)

        c3 = self.card("3", "고지서 올리기", "이미지(JPG·PNG) 또는 PDF를 끌어다 놓거나 클릭해서 고르세요. 여러 장을 모은 PDF는 페이지별로 자동 분리됩니다.")
        self.drop = DropZone("고지서 이미지 또는 PDF를 끌어다 놓으세요", "여러 파일을 한꺼번에 올릴 수 있어요 · JPG, PNG, PDF",
                             [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".pdf"], True, kind="고지서")
        self.drop.picked.connect(self.add_files)
        c3.add(self.drop)
        self.load_prog = ProgressBlock()
        self.load_prog.hide()
        c3.add(self.load_prog)

        c4 = self.card("4", "인식 결과 확인 · 내보내기", "표의 값은 언제든 직접 고칠 수 있습니다. 확인이 끝나면 거래처입력 양식 엑셀로 내보내세요.")
        bar = QHBoxLayout()
        self.stats = StatRow()
        bar.addWidget(self.stats, 1)
        c4.add_layout(bar)
        btns = QHBoxLayout()
        self.b_clear = Button("전체 지우기", "ghost", "trash")
        self.b_run = Button("일괄 인식 시작", "primary", "bolt")
        self.b_export = Button("엑셀 내보내기 (거래처입력 양식)", "success", "down")
        self.b_clear.clicked.connect(self.clear)
        self.b_run.clicked.connect(self.run_all)
        self.b_export.clicked.connect(self.export)
        btns.addWidget(self.b_clear)
        btns.addStretch(1)
        btns.addWidget(self.b_run)
        btns.addWidget(self.b_export)
        c4.add_layout(btns)
        self.run_prog = ProgressBlock()
        self.run_prog.hide()
        c4.add(self.run_prog)
        cols = [("원본", 64, "c"), ("상태", 92, "c")] + [(h.split("\n")[0], L.FIELD_WIDTH[k] + 44, "l") for h, k in zip(D.HEADERS, L.FIELD_KEYS)] + \
               [("AI 메모", 240, "l"), ("", 86, "c")]
        self.table = DataTable(cols, row_height=54, max_rows=7)
        self.empty = Empty("아직 올라온 고지서가 없어요. 위 영역에 이미지나 PDF를 올려 보세요.")
        c4.add(self.table)
        c4.add(self.empty)
        self.msg = Note("", "warn")
        self.msg.hide()
        c4.add(self.msg)
        self.restore_keys()
        self.set_type("water")

    # ── 설정 ──
    def restore_keys(self):
        name = "gemini" if self.prov.value() == "gemini" else "claude"
        self.key.setText(self.ctx.key(name) if hasattr(self.ctx, "key") else "")

    def on_prov(self, v):
        self.fmodel.setVisible(v == "gemini")
        self.restore_keys()

    def cfg(self) -> L.AiConfig:
        return L.AiConfig(self.prov.value(), self.key.text().strip(), self.model.currentData() or "gemini-3.6-flash", self.pacing.value(),
                          self.saver.cb.isChecked(), self.crops.cb.isChecked(), self.verify.cb.isChecked())

    @property
    def t(self) -> L.BillType:
        return L.TYPES[self.tid]

    def set_type(self, tid):
        if self.running:
            return
        self.tid = tid
        self.rows = []
        self.sub.set_text(self.t.sub)
        self.notice.set_text(self.t.notice)
        self.verify.setVisible(self.t.verify)
        self.drop_title(f"{self.t.label} 고지서 이미지 또는 PDF를 끌어다 놓으세요")
        self.render()

    def drop_title(self, text):
        for lb in self.drop.findChildren(QLabel):
            if "끌어다 놓으세요" in lb.text():
                lb.setText(text)
                break

    # ── 파일 ──
    def add_files(self, paths):
        self.load_prog.show()
        self.load_prog.set(0, "불러오는 중…")

        def job(ps, progress=lambda *a: None):
            out = []
            for p in ps:
                try:
                    out += L.load_images(p, progress)
                except Exception as e:  # noqa
                    out.append((os.path.basename(p), str(e)))
            return out
        run_async(job, paths, on_done=self.loaded, on_error=self.load_fail, on_progress=lambda p, t: self.load_prog.set(p, t))

    def load_fail(self, e):
        self.load_prog.hide()
        self.msg.set_text("파일을 읽지 못했습니다: " + e, "err")
        self.msg.show()

    def loaded(self, items):
        self.load_prog.hide()
        bad = []
        for name, im in items:
            if isinstance(im, str):
                bad.append(f"{name}: {im}")
                continue
            self.seq += 1
            self.rows.append(L.Row(self.seq, name, im, L.default_data(self.t)))
        if bad:
            self.msg.set_text("읽지 못한 파일 — " + " / ".join(bad), "warn")
            self.msg.show()
        else:
            self.msg.hide()
        self.render()

    def clear(self):
        if not self.running:
            self.rows = []
            self.render()

    # ── 표 ──
    def render(self):
        has = bool(self.rows)
        self.table.setVisible(has)
        self.empty.setVisible(not has)
        self.b_run.setEnabled(has and not self.running)
        self.b_export.setEnabled(has)
        self.b_clear.setEnabled(has and not self.running)
        self.table.clear()
        for r in self.rows:
            self.table.add_row(self.row_widgets(r))
        self.update_stats()

    def row_widgets(self, r):
        th = QLabel()
        th.setPixmap(pix(r.img, 40))
        th.setCursor(Qt.PointingHandCursor)
        th.mousePressEvent = lambda e, r=r: Viewer(r.img, r.name, self).exec()
        cells = [cell_center(th), cell_center(self.status_tag(r))]
        for k in L.FIELD_KEYS:
            if k == "bank":
                cb = QComboBox()
                cb.setProperty("cell", True)
                cb.addItem("선택", "")
                for b in D.BANK_CODES:
                    cb.addItem(b, b)
                i = cb.findData(r.data.get("bank"))
                if i >= 0:
                    cb.setCurrentIndex(i)
                elif r.data.get("bank"):
                    cb.setItemText(0, r.data["bank"])
                cb.currentIndexChanged.connect(lambda _=0, cb=cb, r=r: r.data.__setitem__("bank", cb.currentData() or ""))
                cells.append(cb)
                continue
            e = cell_edit(r.data.get(k, ""))
            e.setCursorPosition(0)
            if k == "holder":
                e.setMaxLength(L.HOLDER_MAX)
                e.setToolTip(f"예금주명은 e호조 등 회계 시스템 호환을 위해 기호 포함 최대 {L.HOLDER_MAX}자입니다.")
            e.textChanged.connect(lambda v, k=k, r=r: r.data.__setitem__(k, v))
            cells.append(e)
        memo = QLabel(r.ai_memo)
        memo.setToolTip(r.ai_memo)
        memo.setStyleSheet(f"background:transparent; color:{theme.c('text2')}; font-size:12px; padding:0 8px;")
        cells.append(memo)
        act = QWidget()
        h = QHBoxLayout(act)
        h.setContentsMargins(0, 0, 0, 0)
        h.setSpacing(4)
        rb = Button("", "ghost", "refresh", "sm")
        rb.setToolTip("재인식")
        rb.clicked.connect(lambda _=False, r=r: self.run_one(r))
        db = Button("", "ghost", "x", "sm")
        db.setToolTip("삭제")
        db.clicked.connect(lambda _=False, r=r: self.delete(r))
        h.addWidget(rb)
        h.addWidget(db)
        cells.append(act)
        return cells

    def status_tag(self, r):
        t = Tag(r.status, KIND[r.status])
        t.setToolTip(r.error)
        return t

    def refresh_row(self, r):
        i = next((n for n, x in enumerate(self.rows) if x is r), -1)
        if i < 0:
            return
        tb = self.table.table
        tb.setCellWidget(i, 1, cell_center(self.status_tag(r)))
        for n, k in enumerate(L.FIELD_KEYS):
            w = tb.cellWidget(i, 2 + n)
            if isinstance(w, QLineEdit):
                w.blockSignals(True)
                w.setText(r.data.get(k, ""))
                w.blockSignals(False)
            elif isinstance(w, QComboBox):
                j = w.findData(r.data.get("bank"))
                w.blockSignals(True)
                w.setCurrentIndex(max(j, 0))
                w.blockSignals(False)
        m = tb.cellWidget(i, 2 + len(L.FIELD_KEYS))
        if isinstance(m, QLabel):
            m.setText(r.ai_memo)
            m.setToolTip(r.ai_memo)
        self.update_stats()

    def update_stats(self):
        c = lambda s: sum(1 for r in self.rows if r.status == s)
        items = [("전체", f"{len(self.rows)}장"), ("완료", f"{c('완료')}장", True), ("확인필요", f"{c('확인필요')}장"), ("오류", f"{c('오류')}장")]
        self.stats.set_items(items)

    def delete(self, r):
        if not self.running:
            self.rows = [x for x in self.rows if x is not r]
            self.render()

    # ── 인식 ──
    def run_one(self, r):
        self.run_rows([r])

    def run_all(self):
        self.run_rows([r for r in self.rows if r.status in ("대기", "오류", "확인필요")])

    def run_rows(self, targets):
        if self.running or not targets:
            return
        cfg = self.cfg()
        t = self.t
        self.running = True
        self.b_run.setEnabled(False)
        self.run_prog.show()
        self.run_prog.set(0, "시작…")
        ids = {r.id: r for r in targets}

        def job(progress=lambda *a: None):
            n = len(targets)
            for i, r in enumerate(targets):
                progress(int(i / n * 100), f"{r.id}|{r.name}")
                r.status = "인식중"
                progress(int(i / n * 100), f"{r.id}|{r.name}")
                L.process_row(cfg, t, r, on_wait=lambda s, a, m, r=r: setattr(r, "error", f"요청 한도(429) 초과 — {int(s)}초 후 자동 재시도 ({a}/{m})"))
                progress(int((i + 1) / n * 100), f"{r.id}|{r.name}")
                if i < n - 1:
                    time.sleep(max(.3, cfg.pacing))
            return True

        def prog(p, text):
            rid, name = text.split("|", 1)
            self.run_prog.set(p, name)
            r = ids.get(int(rid))
            if r:
                self.refresh_row(r)

        def done(_):
            self.running = False
            self.run_prog.hide()
            for r in targets:
                self.refresh_row(r)
            self.b_run.setEnabled(True)
            self.toast("인식이 끝났습니다. 표의 값을 확인하세요")

        def fail(e):
            self.running = False
            self.run_prog.hide()
            self.b_run.setEnabled(True)
            self.msg.set_text("오류: " + e, "err")
            self.msg.show()
        run_async(job, on_done=done, on_error=fail, on_progress=prog)

    # ── 내보내기 ──
    def export(self):
        if not self.rows:
            return
        stamp = datetime.date.today().strftime("%Y%m%d")
        fn = ask_save(self, f"{self.t.prefix}_{stamp}.xlsx", "엑셀 (*.xlsx)")
        if not fn:
            return
        with open(fn, "wb") as f:
            f.write(L.build_xlsx(self.rows))
        self.toast("거래처입력 양식으로 저장했습니다")
        open_folder(fn)
