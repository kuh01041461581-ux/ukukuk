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
        self.inserted.append(p)

    def open_file(self, p):
        self.opened.append(p)

    def get_text(self):
        return self.text


@unittest.skipUnless(HAVE_TK, "tk/DISPLAY 없음")
class GuiTests(unittest.TestCase):
    def setUp(self):
        os.environ["APPDATA"] = tempfile.mkdtemp()
        from hwpgen import gui
        self.gui = gui
        self.br = FakeBridge()
        self.app = gui.App(self.br)
        self.app.update()

    def tearDown(self):
        self.app.destroy()

    def test_report_flow(self):
        rt = self.app.report_tab
        self.assertGreaterEqual(len(rt.form_ids), 2)
        for iid, fid in rt.form_ids.items():
            rt.tree.selection_set(iid)
            rt.on_select()
            self.app.update()
            rt.vars["title"].set("시험 제목")
            rt.go("insert")
        self.assertEqual(len(self.br.inserted), len(rt.form_ids))
        for p in self.br.inserted:
            self.assertTrue(os.path.getsize(p) > 1000)

    def test_weekly_flow(self):
        wt = self.app.weekly_tab
        wt.data["items"] = [{"title": "개막식", "date": wt.monday.isoformat(), "time": "10:00", "place": "박물관",
                             "dept": "과", "staff": "홍", "phone": "1", "extra": "주최: 시", "important": True}]
        wt.refresh()
        wt.make("plan", "insert")
        wt.make("schedule", "insert")
        self.assertEqual(len(self.br.inserted), 2)
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
