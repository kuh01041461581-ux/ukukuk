# -*- coding: utf-8 -*-
"""화면 연기 시험 (xvfb-run -a python3.12 -m pytest tests/test_gui.py). 한글 대신 가짜 다리를 쓴다."""
import os
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

try:
    import tkinter as tk
    HAVE_TK = bool(os.environ.get("DISPLAY"))
except ImportError:
    HAVE_TK = False


class FakeBridge:
    def __init__(self):
        self.inserted, self.opened = [], []
        self.connected = True
        self.text = ""

    def connect(self, launch=True):
        return True

    def insert_file(self, p):
        self.inserted.append((p, os.path.getsize(p)))

    def compose_chars(self, text, shape_no, char_size=-3):
        self.composed = getattr(self, 'composed', []) + [(text, shape_no)]

    def insert_text(self, s):
        self.texts = getattr(self, 'texts', []) + [s]

    def open_file(self, p):
        self.opened.append(p)

    def get_text(self):
        return self.text


@unittest.skipUnless(HAVE_TK, "tk/DISPLAY 없음")
class GuiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        os.environ["APPDATA"] = tempfile.mkdtemp()
        from hwpgen import gui
        cls.gui = gui
        cls.br = FakeBridge()
        cls.widget = gui.Widget(cls.br)
        cls.widget.update()
        cls.app = gui.DetailWindow(cls.widget)
        cls.app.update()

    @classmethod
    def tearDownClass(cls):
        cls.widget.destroy()

    def test_compose(self):
        n0 = len(self.br.inserted)
        self.widget.shape.set(1)
        self.widget.put_compose("3")
        self.assertEqual(self.br.composed[-1], ("3", 4))     # 명령을 직접 실행
        self.assertEqual(len(self.br.inserted) - n0, 0)

    def test_symbols(self):
        self.widget.put_symbol('○')
        self.widget.put_symbol('❶')
        self.assertEqual(self.br.texts[-2:], ['○', '❶'])

    def test_one_click(self):
        n0 = len(self.br.inserted)
        for fid in ("report_dept", "report_mayor"):
            self.widget.make_report(fid)
        self.widget.make_week("plan")
        self.widget.make_week("schedule")
        self.assertEqual(len(self.br.inserted) - n0, 4)

    def test_report_flow(self):
        rt = self.app.report_tab
        self.assertGreaterEqual(len(rt.form_ids), 2)
        for iid, fid in rt.form_ids.items():
            rt.tree.selection_set(iid)
            rt.on_select()
            self.app.update()
            rt.vars["title"].set("시험 제목")
            rt.go("insert")
        self.assertGreaterEqual(len(self.br.inserted), len(rt.form_ids))
        for p, size in self.br.inserted:
            self.assertTrue(size > 1000)

    def test_weekly_flow(self):
        wt = self.app.weekly_tab
        wt.data["items"] = [{"title": "개막식", "date": wt.monday.isoformat(), "time": "10:00", "place": "박물관",
                             "dept": "과", "staff": "홍", "phone": "1", "extra": "주최: 시", "important": True}]
        wt.refresh()
        n0 = len(self.br.inserted)
        wt.make("plan", "insert")
        wt.make("schedule", "insert")
        self.assertEqual(len(self.br.inserted) - n0, 2)
        # 계획 글을 한글에서 읽어 다시 가져오기
        self.br.text = ("문화교육국\n\U000f02ce 가져온 행사 (과 홍 1)\n◦ 일    시 : %s 09:30\n◦ 장    소 : 시청\n"
                        % __import__("hwpgen.textfmt", fromlist=["x"]).fmt_date_long(wt.monday))
        wt.import_open()
        self.assertEqual(len(wt.data["items"]), 2)
        self.assertEqual(wt.data["items"][1]["place"], "시청") if wt.data["items"][1]["title"] == "가져온 행사" else None

    def test_add_form_dialog(self):
        from tkinter import messagebox
        from hwpgen import registry
        import test_render as TR
        messagebox.showinfo = messagebox.showerror = lambda *a, **k: None      # 대화상자 막힘 방지
        sample = os.path.join(os.environ["APPDATA"], "s.hwpx")
        registry.load_form("report_dept", TR.ROOTS).render({"title": "내 제목 XYZ"}, TR.OUTLINE_DEPT).save(sample)
        dlg = self.gui.AddFormDialog(self.app, sample)
        self.app.update()
        for iid in dlg.tv.get_children():
            if dlg.tv.item(iid, "values")[0] == "내 제목 XYZ":
                dlg.tv.item(iid, values=("내 제목 XYZ", "제목"))
        dlg.name.set("시험양식")
        dlg.ok()
        self.assertTrue(dlg.done)
        self.app.forms_tab.refresh()
        self.app.report_tab.reload()
        names = [self.app.report_tab.tree.item(i, "text") for i in self.app.report_tab.form_ids]
        self.assertIn("시험양식", names)


if __name__ == "__main__":
    unittest.main()
