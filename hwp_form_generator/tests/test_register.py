# -*- coding: utf-8 -*-
"""'내가 준 한글 파일로 양식 추가' 흐름: 만든 문서를 샘플로 다시 등록해 같은 모양이 나오는지."""
import os
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, HERE)

from hwpgen import registry, report  # noqa: E402
from hwpgen.package import HP, Package, ptext  # noqa: E402
import test_render as TR  # noqa: E402


class RegisterTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        os.environ["APPDATA"] = self.tmp

    def test_register_from_sample(self):
        f = registry.load_form("report_dept", TR.ROOTS)
        sample = os.path.join(self.tmp, "sample.hwpx")
        f.render({"title": "샘플 제목 ABC", "dept": "○○과", "team": "△△계", "date": "2026-11-04",
                  "head": "가(1)", "lead": "나(2)", "staff": "다(3)"}, TR.OUTLINE_DEPT).save(sample)
        pkg = Package.load(sample)
        cands = report.frame_texts(pkg.section(0), report.detect_body(pkg.section(0)))
        texts = [t for _, t in cands]
        self.assertIn("샘플 제목 ABC", texts)
        res = registry.add_report_form(sample, "u_test", "내 양식", "내가 추가한 양식",
                                       [{"key": "title", "label": "제목", "sample": "샘플 제목 ABC"}])
        self.assertEqual(res["tables"], [3])
        self.assertIn("h1", res["protos"])
        forms = {fid: folder for fid, folder, meta in registry.list_forms([os.path.join(self.tmp, "HwpFormGenerator", "forms")])}
        self.assertIn("u_test", forms)
        g = registry.load_form("u_test", [os.path.join(self.tmp, "HwpFormGenerator", "forms")])
        out = g.render({"title": "새 제목"}, TR.OUTLINE_DEPT)
        TR.check_package(self, out)
        txt = ptext(out.section(0))
        self.assertIn("새 제목", txt)
        self.assertIn("◦ 일    시 : 2026. 11. 4.(수) 13:00 ~ 17:00", txt)     # 항목 모양 그대로
        self.assertTrue(registry.remove_user_form("u_test"))

    def test_missing_sample_text(self):
        f = registry.load_form("report_dept", TR.ROOTS)
        sample = os.path.join(self.tmp, "s2.hwpx")
        f.render({"title": "T"}, TR.OUTLINE_DEPT).save(sample)
        with self.assertRaises(ValueError):
            registry.add_report_form(sample, "u_bad", "x", "y", [{"key": "k", "label": "k", "sample": "없는글자"}])

    def test_no_body_heading(self):
        f = registry.load_form("weekly_schedule", TR.ROOTS)
        sample = os.path.join(self.tmp, "s3.hwpx")
        f.render({"org": "국", "start": "2026-11-09", "events": []}).save(sample)
        with self.assertRaises(ValueError):
            registry.add_report_form(sample, "u_bad2", "x", "y", [])


if __name__ == "__main__":
    unittest.main()
