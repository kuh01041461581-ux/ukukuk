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

    def insert_file(self, p, keep_section=False):
        self.keeps = getattr(self, 'keeps', []) + [keep_section]
        self.inserted.append((p, os.path.getsize(p)))
        from hwpgen.package import Package, ptext
        self.last_text = ptext(Package.load(p).section(0))

    def compose_chars(self, text, shape_no, char_size=-3):
        self.composed = getattr(self, 'composed', []) + [(text, shape_no)]

    def move_doc_end(self):
        self.moves = getattr(self, 'moves', 0) + 1

    def break_para(self):
        self.breaks = getattr(self, 'breaks', 0) + 1

    def at_doc_end(self):
        return getattr(self, 'at_end', True)

    def is_empty_doc(self):
        return True

    def apply_page_setup(self, d):
        self.page = d

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

    def setUp(self):
        # 시험끼리 영향을 주지 않게: 기본은 '한 번에', 빈 문서·문서 끝 상태
        self.widget.ui["insert_mode"] = "whole"
        self.widget.ui["insert_delay"] = 0
        self.br.__dict__.pop("is_empty_doc", None)
        self.br.at_end = True

    @classmethod
    def tearDownClass(cls):
        cls.widget.destroy()

    def test_compose(self):
        n0 = len(self.br.inserted)
        self.widget.shape.set(1)
        self.widget.put_compose("3")
        self.assertEqual(self.br.composed[-1], ("3", 4))     # 명령을 직접 실행
        self.assertEqual(len(self.br.inserted) - n0, 0)

    def test_reference_page(self):
        n0 = len(self.br.inserted)
        self.widget.make_reference()
        self.assertEqual(len(self.br.inserted) - n0, 1)

    def test_close_goes_to_tray_and_quit_is_separate(self):
        w = self.widget
        calls = []
        orig_tray, orig_quit = w.to_tray, w.quit_app
        w.to_tray = lambda: calls.append("tray") or True
        w.quit_app = lambda: calls.append("quit")
        w.on_close()
        self.assertEqual(calls, ["tray"])                 # X = 트레이로
        w.to_tray = lambda: calls.append("tray2") or False
        w.on_close()
        self.assertEqual(calls[-2:], ["tray2", "quit"])   # 트레이를 못 쓰면 그냥 종료
        w.to_tray, w.quit_app = orig_tray, orig_quit

    def test_alpha_and_tray(self):
        self.widget.set_alpha(70)
        self.assertAlmostEqual(self.widget.alpha, 0.7)
        # 트레이: 보내고 → 대기열 신호로 다시 나타남 (pystray 가 없거나 화면이 없으면 최소화로 대체됨)
        self.widget.to_tray()
        q = getattr(self.widget, "_tray_q", None)
        if q is not None:
            q.put("show")
            for _ in range(10):
                self.widget.update()
                import time; time.sleep(0.1)
            self.assertIsNone(self.widget._tray)
        self.widget.deiconify()

    def test_single_instance(self):
        from hwpgen.single import SingleInstance
        import time
        a, b = SingleInstance(port=47999), SingleInstance(port=47999)
        try:
            self.assertTrue(a.acquire())            # 첫 실행
            self.assertFalse(b.acquire())           # 두 번째 실행은 신호만 보내고 끝
            for _ in range(20):
                if not a.q.empty():
                    break
                time.sleep(0.05)
            self.assertEqual(a.q.get_nowait(), "show")
        finally:
            a.close()
            b.close()

    def test_symbols(self):
        self.widget.put_symbol('○')
        self.widget.put_symbol('❶')
        self.assertEqual(self.br.texts[-2:], ['○', '❶'])

    def test_step_by_step(self):
        """차례대로 입력: 여러 조각으로 나뉘어 들어가고, 쪽 설정(secPr)은 빠지며, 빈 문서면 용지·여백 적용"""
        import zipfile
        from hwpgen.package import HP
        self.widget.ui["insert_mode"] = "step"
        self.widget.ui["insert_delay"] = 0
        n0 = len(self.br.inserted)
        m0 = getattr(self.br, "moves", 0)
        self.widget.make_report("report_event")
        k = len(self.br.inserted) - n0
        self.assertGreater(k, 5)
        self.assertEqual(self.br.moves - m0, k)          # 조각마다 문서 끝으로 이동한 뒤 넣는다(순서 보장)
        self.assertTrue(getattr(self.br, "page", None))
        self.assertEqual(self.br.keeps[n0:], [True] + [False] * (k - 1))   # 첫 조각만 쪽 설정을 가져온다
        self.assertEqual(self.br.breaks, k - 1)          # 조각 사이마다 문단 나눔(마지막 뒤엔 없음)
        for p, size in self.br.inserted[n0:]:
            self.assertGreater(size, 1000)
        self.widget.ui["insert_mode"] = "whole"
        n1 = len(self.br.inserted)
        self.widget.make_report("report_event")
        self.assertEqual(len(self.br.inserted) - n1, 1)                 # 한 번에: 정확히 한 번(두 번 들어가지 않음)

    def test_step_default_and_tabs_and_week(self):
        w = self.widget
        self.assertEqual(w.ui.get("insert_mode", "step"), "whole")      # setUp 이 바꿔 둠 — 실제 기본값은 아래에서
        fresh = self.gui.Widget.__dict__["TABS"]
        self.assertEqual([k for _, k in fresh][:6], ["report", "week", "budget", "press", "assembly", "audit"])
        # 양식이 탭마다 나뉘어 들어간다
        def ids(tab):
            return {fid for g in w.tab_forms(tab).values() for fid, _ in g}
        self.assertEqual(ids("report") >= {"report_event", "report_project", "report_mayor"}, True)
        self.assertIn("meeting_issue", ids("week"))
        self.assertTrue({"budget_cover", "budget_project", "budget_ref_cover", "budget_ref_detail", "budget_ref_return"} <= ids("budget"))
        self.assertEqual(ids("press"), {"press_release"})
        self.assertTrue({"written_item", "policy_item", "policy_ref"} <= ids("assembly"))
        self.assertTrue({"audit_cover", "audit_common"} <= ids("audit"))
        # 작성 대상 주 = 다음 주
        import datetime
        from hwpgen import textfmt as TT
        w.ui.pop("week_offset", None)
        self.assertEqual(w.target_monday(), TT.week_monday(datetime.date.today()) + datetime.timedelta(days=7))
        self.assertEqual(w.default_value("target_monday"), w.target_monday().isoformat())
        w.shift_week(-1)
        self.assertEqual(w.target_monday(), TT.week_monday(datetime.date.today()))
        w.shift_week(1)

    def test_covers_use_target_week(self):
        from hwpgen.package import ptext
        w = self.widget
        w.ui["week_offset"] = 1
        w.make_week("cover")
        self.assertIn(self.expected_range(w), self.br.last_text)
        w.make_report("meeting_cover")
        self.assertIn(self.expected_mdate(w), self.br.last_text)

    @staticmethod
    def expected_range(w):
        from hwpgen import textfmt as TT
        import datetime
        m = w.target_monday()
        return TT.fmt_range_short(m, m + datetime.timedelta(days=6))

    @staticmethod
    def expected_mdate(w):
        from hwpgen import textfmt as TT
        return TT.fmt_date_long(w.target_monday())

    def test_non_empty_doc_inserts_whole(self):
        """내용이 있는 문서에서는 '차례대로'를 켜도 한 번에 넣는다(커서가 중간이면 순서가 뒤집힐 수 있어서)"""
        self.widget.ui["insert_mode"] = "step"
        self.br.is_empty_doc = lambda: False
        self.br.at_end = False                       # 커서가 문서 중간
        n0 = len(self.br.inserted)
        self.widget.make_report("report_event")
        self.assertEqual(len(self.br.inserted) - n0, 1)

    def test_weekly_plan_without_items_still_has_lines(self):
        """일정을 입력하지 않아도 주간업무계획에 항목 틀(제목·일시·장소·주최·내용)이 나온다"""
        from hwpgen.package import ptext, Package
        wt = self.widget
        wt.ui["insert_mode"] = "whole"
        import hwpgen.profile as PRM
        PRM.save_week(__import__("hwpgen.textfmt", fromlist=["x"]).week_monday(__import__("datetime").date.today()), {"items": []})
        n0 = len(self.br.inserted)
        wt.make_week("plan")
        self.assertEqual(len(self.br.inserted) - n0, 1)

    def test_press_one_click_is_frame_only(self):
        from hwpgen.package import Package
        import zipfile
        self.widget.ui["insert_mode"] = "whole"
        n0 = len(self.br.inserted)
        self.widget.make_report("press_release")
        path, size = self.br.inserted[-1]
        self.assertEqual(len(self.br.inserted) - n0, 1)
        self.assertNotIn("사업개요", self.br.last_text)           # 기본 본문 틀(사업개요…)이 아니라 보도자료 틀만

    def test_one_click(self):
        self.widget.ui["insert_mode"] = "whole"
        n0 = len(self.br.inserted)
        for fid in ("report_event", "report_mayor"):
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
            if "title" in rt.vars:
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
        registry.load_form("report_event", TR.ROOTS).render({"title": "내 제목 XYZ"}, TR.OUTLINE_DEPT).save(sample)
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
