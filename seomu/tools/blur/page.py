# -*- coding: utf-8 -*-
import os
from PIL import Image
from PySide6.QtCore import QPointF, QRectF, Qt, Signal
from PySide6.QtGui import QColor, QImage, QPainter, QPen, QPixmap
from PySide6.QtWidgets import QHBoxLayout, QLabel, QVBoxLayout, QWidget
from ...core.worker import run_async
from ...ui.theme import theme
from ...ui.widgets import (Button, DownloadBar, Field, FileInput, FormGrid, Note, ProgressBlock, Segmented, StatRow, Tag, ask_save,
                           fmt_size, open_folder)
from ..base import ToolPage
from . import logic as B


def to_pixmap(im: Image.Image) -> QPixmap:
    im = im.convert("RGBA")
    q = QImage(im.tobytes("raw", "RGBA"), im.width, im.height, im.width * 4, QImage.Format_RGBA8888)
    return QPixmap.fromImage(q.copy())


class FaceEditor(QWidget):
    """사진 위에서 얼굴 상자를 확인·수정한다. 상자 클릭 = 삭제, 빈 곳 드래그 = 추가."""
    changed = Signal()

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setMinimumHeight(420)
        self.setCursor(Qt.CrossCursor)
        self.pix: QPixmap | None = None
        self.boxes: list = []
        self._drag = None
        self._cur = None
        self.preview = False

    def set_image(self, im: Image.Image, boxes: list):
        self.pix = to_pixmap(im)
        self.boxes = boxes
        self._drag = self._cur = None
        self.update()

    def set_pixmap_only(self, im: Image.Image):
        self.pix = to_pixmap(im)
        self.update()

    def _fit(self):
        if not self.pix:
            return QRectF(), 1.0
        s = min((self.width() - 16) / self.pix.width(), (self.height() - 16) / self.pix.height())
        w, h = self.pix.width() * s, self.pix.height() * s
        return QRectF((self.width() - w) / 2, (self.height() - h) / 2, w, h), s

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        p.setRenderHint(QPainter.SmoothPixmapTransform)
        c = theme.tok
        p.setPen(QPen(QColor(c["border"]), 1))
        p.setBrush(QColor(c["surface2"]))
        p.drawRoundedRect(QRectF(self.rect()).adjusted(.5, .5, -.5, -.5), 10, 10)
        if not self.pix:
            p.setPen(QColor(c["text3"]))
            p.drawText(self.rect(), Qt.AlignCenter, "이미지를 불러오면 여기에 표시됩니다")
            return
        r, s = self._fit()
        p.drawPixmap(r, self.pix, QRectF(self.pix.rect()))
        for i, (x, y, w, h) in enumerate(self.boxes):
            br = QRectF(r.x() + x * s, r.y() + y * s, w * s, h * s)
            p.setPen(QPen(QColor(c["primary"]), 2))
            p.setBrush(QColor(99, 102, 241, 40))
            p.drawRoundedRect(br, 4, 4)
            p.setBrush(QColor(c["primary"]))
            p.setPen(Qt.NoPen)
            p.drawEllipse(QPointF(br.right(), br.top()), 9, 9)
            p.setPen(QPen(QColor("#fff"), 1.8))
            cx, cy = br.right(), br.top()
            p.drawLine(QPointF(cx - 3.5, cy - 3.5), QPointF(cx + 3.5, cy + 3.5))
            p.drawLine(QPointF(cx - 3.5, cy + 3.5), QPointF(cx + 3.5, cy - 3.5))
        if self._cur:
            p.setPen(QPen(QColor(c["success"]), 2, Qt.DashLine))
            p.setBrush(QColor(16, 185, 129, 40))
            p.drawRect(self._cur)

    def _to_img(self, pt):
        r, s = self._fit()
        return (pt.x() - r.x()) / s, (pt.y() - r.y()) / s

    def mousePressEvent(self, e):
        if not self.pix or e.button() != Qt.LeftButton:
            return
        r, s = self._fit()
        # 삭제 단추(오른쪽 위 원) 우선
        for i, (x, y, w, h) in enumerate(self.boxes):
            cx, cy = r.x() + (x + w) * s, r.y() + y * s
            if (e.position().x() - cx) ** 2 + (e.position().y() - cy) ** 2 <= 11 ** 2:
                del self.boxes[i]
                self.update()
                self.changed.emit()
                return
        self._drag = e.position()

    def mouseMoveEvent(self, e):
        if self._drag is not None:
            self._cur = QRectF(self._drag, e.position()).normalized()
            self.update()

    def mouseReleaseEvent(self, e):
        if self._drag is None:
            return
        rc = QRectF(self._drag, e.position()).normalized()
        self._drag = self._cur = None
        if rc.width() > 8 and rc.height() > 8:
            r, s = self._fit()
            x, y = (rc.x() - r.x()) / s, (rc.y() - r.y()) / s
            w, h = rc.width() / s, rc.height() / s
            x0, y0 = max(0, x), max(0, y)
            w, h = min(self.pix.width() - x0, w - (x0 - x)), min(self.pix.height() - y0, h - (y0 - y))
            if w > 2 and h > 2:
                self.boxes.append((x0, y0, w, h))
                self.changed.emit()
        self.update()


class BlurPage(ToolPage):
    def build(self):
        self.doc = None
        self.cur = 0
        c1 = self.card("1", "HWPX 파일 올리기", "문서 안의 사진에서 얼굴을 자동으로 찾아 가립니다. 사진은 이 PC 밖으로 나가지 않습니다.")
        self.fin = FileInput("HWPX 파일을 여기에 놓으세요", "또는 클릭해서 파일 선택", [".hwpx"], kind="HWPX 파일")
        self.fin.file_changed.connect(self.on_file)
        c1.add(self.fin)
        c2 = self.card("2", "가리기 방식", "탐지 민감도는 높일수록 더 많이 찾지만 오탐이 늘 수 있습니다. 결과는 다음 단계에서 직접 고칠 수 있습니다.")
        g = FormGrid(3)
        self.sens = Segmented([(0.7, "정확하게"), (0.5, "보통"), (0.3, "넓게")], 0.5)
        self.mode = Segmented([("pixelate", "모자이크"), ("blur", "블러")], "pixelate")
        self.pad = Segmented([(0.15, "좁게"), (0.3, "보통"), (0.5, "넓게")], 0.3)
        g.add(Field("탐지 민감도", self.sens))
        g.add(Field("가리기 방식", self.mode))
        g.add(Field("가리는 범위", self.pad, "얼굴 상자 바깥으로 넓히는 정도"))
        c2.add(g)
        self.run = Button("얼굴 찾기", "primary", "eye", "lg")
        self.run.setEnabled(False)
        self.run.clicked.connect(self.start)
        c2.add(self.run)
        self.prog = ProgressBlock()
        self.prog.hide()
        c2.add(self.prog)
        self.msg = Note("", "ok")
        self.msg.hide()
        c2.add(self.msg)

        self.c3 = self.card("3", "확인하고 고치기", "상자 오른쪽 위 ✕ 로 삭제, 빈 곳을 드래그하면 상자를 추가합니다. 놓친 얼굴이나 잘못 잡힌 곳을 고쳐 주세요.")
        nav = QHBoxLayout()
        self.prev = Button("이전", "secondary", None, "sm")
        self.next = Button("다음", "secondary", None, "sm")
        self.pos = QLabel("")
        self.pos.setStyleSheet(f"font-weight:600; color:{theme.c('text2')}; background:transparent;")
        self.prev.clicked.connect(lambda: self.go(-1))
        self.next.clicked.connect(lambda: self.go(1))
        self.tog = Segmented([(False, "원본에 상자 보기"), (True, "가린 결과 보기")], False)
        self.tog.changed.connect(self.refresh)
        nav.addWidget(self.prev)
        nav.addWidget(self.pos)
        nav.addWidget(self.next)
        nav.addStretch(1)
        nav.addWidget(self.tog)
        self.c3.add_layout(nav)
        self.ed = FaceEditor()
        self.ed.changed.connect(self.on_edit)
        self.c3.add(self.ed)
        self.stats = StatRow()
        self.c3.add(self.stats)
        self.dl = DownloadBar("가려진 HWPX 저장")
        self.dl.clicked.connect(self.save)
        self.c3.add(self.dl)
        self.c3.hide()

    # ── 흐름 ──
    def on_file(self, path):
        self.run.setEnabled(bool(path))
        self.c3.hide()
        self.msg.hide()

    def start(self):
        self.run.setEnabled(False)
        self.msg.hide()
        self.prog.show()
        self.prog.set(0, "문서 여는 중…")

        def job(path, sens, progress=lambda *a: None):
            doc = B.load_document(path)
            B.detect_all(doc, sens, progress)
            return doc
        run_async(job, self.fin.path, self.sens.value(), on_done=self.detected, on_error=self.fail,
                  on_progress=lambda p, t: self.prog.set(p, os.path.basename(t)))

    def fail(self, err):
        self.run.setEnabled(True)
        self.prog.hide()
        self.msg.set_text("오류: " + err, "err")
        self.msg.show()

    def detected(self, doc):
        self.run.setEnabled(True)
        self.prog.hide()
        self.doc = doc
        self.cur = 0
        if not doc.images:
            self.msg.set_text("문서에서 이미지를 찾지 못했습니다.", "warn")
            self.msg.show()
            return
        n = sum(len(e.boxes) for e in doc.images)
        self.msg.set_text(f"사진 {len(doc.images)}장에서 얼굴 {n}개를 찾았습니다. 아래에서 확인하세요.", "ok")
        self.msg.show()
        self.c3.show()
        self.refresh()

    def go(self, d):
        self.cur = max(0, min(len(self.doc.images) - 1, self.cur + d))
        self.refresh()

    def on_edit(self):
        self.update_stats()

    def update_stats(self):
        d = self.doc
        n = sum(len(e.boxes) for e in d.images)
        self.stats.set_items([("사진", f"{len(d.images)}장"), ("가릴 얼굴", f"{n}개", True),
                              ("이 사진", f"{len(d.images[self.cur].boxes)}개")])
        base = os.path.splitext(os.path.basename(self.fin.path))[0]
        self.dl.set_info(base + "_블러.hwpx", f"얼굴 {n}개를 {'모자이크' if self.mode.value() == 'pixelate' else '블러'}로 가립니다")

    def refresh(self, *_):
        if not self.doc or not self.doc.images:
            return
        e = self.doc.images[self.cur]
        self.pos.setText(f"{self.cur + 1} / {len(self.doc.images)}  ·  {e.name}")
        self.prev.setEnabled(self.cur > 0)
        self.next.setEnabled(self.cur < len(self.doc.images) - 1)
        self.ed.set_image(e.image, e.boxes)
        if self.tog.value():
            self.ed.set_image(B.apply_blur(e.image, e.boxes, self.mode.value(), self.pad.value()), [])
        self.update_stats()

    def save(self):
        base = os.path.splitext(os.path.basename(self.fin.path))[0]
        fn = ask_save(self, base + "_블러.hwpx", "한글 문서 (*.hwpx)")
        if not fn:
            return
        rendered = B.render(self.doc, self.mode.value(), self.pad.value())
        with open(fn, "wb") as f:
            f.write(B.build_hwpx(self.doc, rendered))
        self.toast("가려진 HWPX를 저장했습니다")
        open_folder(fn)
