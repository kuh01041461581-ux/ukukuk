# -*- coding: utf-8 -*-
import os
from PySide6.QtWidgets import QHBoxLayout, QMessageBox
from ...core.worker import run_async
from ...ui.widgets import (Button, DataTable, DownloadBar, Field, FileInput, FormGrid, Note, ProgressBlock, RangeField, StatRow, Tag, ask_save,
                           cell_text, fmt_size, open_folder)
from ..base import ToolPage
from . import logic as C


class CompressPage(ToolPage):
    def build(self):
        self.report = None
        c1 = self.card("1", "HWPX 파일 올리기", ".hwpx 파일 안의 사진(BinData)과 미리보기 썸네일을 줄입니다. 구형 .hwp(바이너리)는 지원하지 않으니, 한글에서 \"다른 이름으로 저장 → HWPX\"로 바꾼 뒤 올려 주세요.")
        self.fin = FileInput("HWPX 파일을 여기에 놓으세요", "또는 클릭해서 파일 선택 · 서버로 업로드되지 않음", [".hwpx"], kind="HWPX 파일")
        self.fin.file_changed.connect(self.on_file)
        c1.add(self.fin)
        c2 = self.card("2", "압축 옵션", "JPEG는 다시 압축하고, BMP·GIF·TIFF는 PNG로 무손실 변환합니다(내부 매니페스트는 자동 갱신). 전체 압축률도 최대로 적용합니다.")
        g = FormGrid(2)
        self.dim = RangeField(600, 3000, 100, 1600, "px")
        self.q = RangeField(30, 95, 5, 70, "%")
        g.add(Field("이미지 최대 가로/세로 크기", self.dim, "값이 작을수록 용량이 많이 줄지만 화질이 낮아집니다. 보고서용은 1600px 안팎을 권장합니다."))
        g.add(Field("JPEG 압축 품질", self.q, "70% 안팎이면 육안으로 차이가 거의 없습니다."))
        c2.add(g)
        self.run = Button("압축 시작", "primary", "zip", "lg")
        self.run.setEnabled(False)
        self.run.clicked.connect(self.start)
        c2.add(self.run)
        self.prog = ProgressBlock()
        self.prog.hide()
        c2.add(self.prog)
        self.msg = Note("", "ok")
        self.msg.hide()
        c2.add(self.msg)
        self.c3 = self.card("✓", "압축 결과", "이미지별 절감량과 최종 파일 크기입니다.")
        self.c3.badge.set_done(True)
        self.stats = StatRow()
        self.c3.add(self.stats)
        self.notes = Note("", "plain")
        self.notes.hide()
        self.c3.add(self.notes)
        self.table = DataTable([("이미지", None, "l"), ("원본", 120, "r"), ("압축 후", 120, "r"), ("절감률", 90, "c")], max_rows=8)
        self.c3.add(self.table)
        self.dl = DownloadBar("압축된 HWPX 저장")
        self.dl.clicked.connect(self.save)
        self.c3.add(self.dl)
        self.c3.hide()

    def on_file(self, path):
        self.run.setEnabled(bool(path))
        self.c3.hide()
        self.msg.hide()
        self.prog.hide()

    def start(self):
        path = self.fin.path
        if not path:
            return
        self.run.setEnabled(False)
        self.msg.hide()
        self.c3.hide()
        self.prog.show()
        self.prog.set(0, "읽는 중…")
        run_async(C.compress_hwpx, path, self.dim.value(), self.q.value(), on_done=self.done, on_error=self.fail,
                  on_progress=lambda p, t: self.prog.set(p, os.path.basename(t)))

    def fail(self, err):
        self.run.setEnabled(True)
        self.prog.hide()
        self.msg.set_text("오류: " + err, "err")
        self.msg.show()

    def done(self, rep: C.Report):
        self.run.setEnabled(True)
        self.prog.hide()
        self.report = rep
        self.stats.set_items([("원본 크기", fmt_size(rep.original_size)), ("압축 후 크기", fmt_size(rep.new_size)),
                              ("재인코딩한 이미지", f"{len(rep.rows)}개"), ("전체 절감률", f"{rep.pct}%", True)])
        notes = []
        if rep.converted:
            notes.append(f"BMP/GIF/TIFF {rep.converted}개를 PNG로 무손실 변환하고, 내부 매니페스트(content.hpf)의 형식 정보도 함께 갱신했습니다.")
        if rep.convert_failed:
            notes.append(f"{rep.convert_failed}개(원본 {fmt_size(rep.convert_failed_bytes)})는 디코딩하지 못해 원본 그대로 두었습니다(매니페스트도 변경하지 않음).")
        if rep.other_bin:
            notes.append(f"그 외 첨부 데이터(폰트·OLE 객체 등) {rep.other_bin}개(원본 {fmt_size(rep.other_bin_bytes)})는 포맷을 바꾸지 않고 무손실 압축(레벨 9)만 적용했습니다.")
        if notes:
            self.notes.set_text("<br>".join(notes))
            self.notes.show()
        else:
            self.notes.hide()
        self.table.clear()
        for r in rep.rows:
            self.table.add_row([cell_text(r.name), cell_text(fmt_size(r.before), "r"), cell_text(fmt_size(r.after), "r"),
                                self._pct_tag(r.pct)])
        base = os.path.splitext(os.path.basename(self.fin.path))[0]
        self.dl.set_info(base + "_압축.hwpx", f"{fmt_size(rep.new_size)} · 원본 대비 {rep.pct}% 감소")
        self.c3.show()
        self.msg.set_text("압축이 끝났습니다. 아래에서 결과를 확인하고 저장하세요.", "ok")
        self.msg.show()

    def _pct_tag(self, pct):
        from ...ui.widgets import cell_center
        return cell_center(Tag(f"{pct}%", "ok" if pct > 0 else "neutral"))

    def save(self):
        base = os.path.splitext(os.path.basename(self.fin.path))[0]
        fn = ask_save(self, base + "_압축.hwpx", "한글 문서 (*.hwpx)")
        if not fn:
            return
        with open(fn, "wb") as f:
            f.write(self.report.data)
        self.toast("압축된 HWPX를 저장했습니다")
        open_folder(fn)
