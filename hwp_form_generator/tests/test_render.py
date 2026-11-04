# -*- coding: utf-8 -*-
"""기본 양식 4종이 문서를 제대로 만드는지: 구조·서식 번호·자리표시·id 중복·표 주소."""
import os
import re
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, ROOT)

from hwpgen import registry, weekly, textfmt as T  # noqa: E402
from hwpgen.package import HP, HH, Package, ptext, own_text  # noqa: E402

FORMS = os.path.join(ROOT, "forms")
ROOTS = [FORMS]

OUTLINE_DEPT = """□ 행사개요
◦ 사업명: 미륵사지 디지털 건축 학술포럼
◦ 일시: 2026. 11. 4.(수) 13:00 ~ 17:00
◦ 장소: 익산 웨스턴 라이프호텔 그랜드볼룸(B2)

□ 주요내용
| 등록 및 개회 | 사회: 홍길동(○○연구원) |
| 13:00~13:30(30') | **참석자 등록** |
| 13:30~13:50(20') | **개 회 사** | ○○연구원장 |
| 학술발표 |
| 14:00~14:30(30') | [기조강연] 가나다 | 발표: 갑을 |
"""

OUTLINE_MAYOR = """□ 개요
◦ 일시: 2026. 9. 16.(수) 14:00
◦ 장소: 익산시청 지하 1층 로비
◦ 참석인원: 50명 내외
| 구 분 | 참석대상 |
| 익 산 시 | 시장, 부시장 |
| 시 민 | 시민 20~30명 |
◦ 진행방식 분과별 발표 후 토론
□ 시간계획
| 시 간 | 소요 | 주 요 내 용 | 비 고 |
| 14:00 ~ 14:05 | 5′ | ◦ 개회 및 안내 | 사회자 |
| 14:05 ~ 14:10 | 5′ | ◦ 인사말씀 | 시장님 |
"""


def all_ids(sec):
    ids = []
    for el in sec.iter():
        if el.tag.split("}")[-1] in ("tbl", "container", "pic") and el.get("id"):
            ids.append(el.get("id"))
    return ids


def check_package(tc, pkg):
    data = pkg.to_bytes()
    pkg2 = Package.load(data)                       # 다시 읽힘
    sec = pkg2.section(0)
    txt = ptext(sec)
    tc.assertNotIn("{{", txt, "자리표시가 남았어요")
    ids = all_ids(sec)
    tc.assertEqual(len(ids), len(set(ids)), "개체 id 중복")
    # 쓰인 서식 번호는 header.xml 에 있어야 한다
    hdr = pkg2.header()
    cps = {c.get("id") for c in hdr.iter(HH + "charPr")}
    pps = {c.get("id") for c in hdr.iter(HH + "paraPr")}
    bfs = {c.get("id") for c in hdr.iter(HH + "borderFill")}
    for el in sec.iter(HP + "run"):
        tc.assertIn(el.get("charPrIDRef"), cps)
    for el in sec.iter(HP + "p"):
        tc.assertIn(el.get("paraPrIDRef"), pps)
    for el in sec.iter(HP + "tc"):
        tc.assertIn(el.get("borderFillIDRef"), bfs)
    # 표: 행·열 주소가 겹치지 않고 rowCnt 가 맞다
    for tbl in sec.iter(HP + "tbl"):
        trs = tbl.findall(HP + "tr")
        tc.assertEqual(int(tbl.get("rowCnt")), len(trs))
        occ = set()
        for tr in trs:
            for c in tr.findall(HP + "tc"):
                a = c.find(HP + "cellAddr")
                s = c.find(HP + "cellSpan")
                r0, c0 = int(a.get("rowAddr")), int(a.get("colAddr"))
                for r in range(r0, r0 + int(s.get("rowSpan"))):
                    for cc in range(c0, c0 + int(s.get("colSpan"))):
                        tc.assertNotIn((r, cc), occ, "칸 주소 겹침")
                        occ.add((r, cc))
                tc.assertLess(c0 + int(s.get("colSpan")) - 1, int(tbl.get("colCnt")))
    tc.assertFalse(list(sec.iter(HP + "linesegarray")))
    return pkg2


class RenderTests(unittest.TestCase):
    def test_dept_report(self):
        f = registry.load_form("report_event", ROOTS)
        pkg = f.render({"title": "시험 행사 계획", "dept": "○○과", "team": "△△계", "date": "2026-11-04",
                        "head": "가나다(1111)", "lead": "라마바(2222)", "staff": "사아자(3333)"}, OUTLINE_DEPT)
        pkg2 = check_package(self, pkg)
        txt = ptext(pkg2.section(0))
        self.assertIn("시험 행사 계획", txt)
        self.assertIn("보고 : 2026. 11. 4)", txt)
        self.assertIn("◦ 사 업 명 : 미륵사지 디지털 건축 학술포럼", txt)
        self.assertIn("◦ 일    시 : 2026. 11. 4.(수) 13:00 ~ 17:00", txt)
        self.assertIn("주요내용", txt)
        # 3칸 표: 1칸 병합 줄 / 2칸(1+2) / 3칸 줄
        tbls = [t for t in pkg2.section(0).iter(HP + "tbl") if t.get("colCnt") == "3"]
        self.assertEqual(len(tbls), 1)
        self.assertEqual(int(tbls[0].get("rowCnt")), 5)

    def test_blank_staff(self):
        """이름·전화를 비워 두면 담당자 줄이 '(      )' 빈칸으로 나온다 (라벨 과장/계장/담당자는 양식이 만든다)"""
        f = registry.load_form("report_event", ROOTS)
        txt = ptext(check_package(self, f.render({"title": "t", "date": "2026-11-04"}, "")).section(0))
        self.assertIn("과장 (      ), 계장 (      ), 담당자 (      )", txt)

    def test_mayor_report(self):
        f = registry.load_form("report_mayor", ROOTS)
        pkg = f.render({"title": "타운홀미팅 시험", "date": "2026-09-14", "head": "과장 1111", "lead": "계장 2222",
                        "dept": "○○과 △△계", "staff": "담당 3333"}, OUTLINE_MAYOR)
        pkg2 = check_package(self, pkg)
        txt = ptext(pkg2.section(0))
        self.assertIn("2026. 9. 14.(월)", txt)
        self.assertIn("(일    시)", txt)
        self.assertIn("(참석인원)", txt)
        self.assertIn("타운홀미팅 시험", txt)
        # 개요가 헤더표 안 본문 칸에 들어갔는지
        self.assertEqual(sorted(t.get("colCnt") for t in pkg2.section(0).iter(HP + "tbl")).count("4"), 1)

    def test_weekly_plan(self):
        f = registry.load_form("weekly_plan", ROOTS)
        data = {"org": "문화교육국", "items": [
            {"title": "개막식", "dept": "문화유산과", "staff": "홍길동", "phone": "1234", "date": "2026-11-09",
             "time": "10:00", "place": "국립박물관", "lines": [("주최", "익산시"), ("내용", "개막식 및 기념식")]},
            {"title": "패밀리마켓", "dept": "문화유산과", "staff": "김철수", "phone": "5678", "date": "2026-11-14",
             "time": "13:00", "end_time": "17:00", "place": "모현공원", "extra": "참석: 시민 누구나\n주요내용: 장터"},
        ]}
        pkg = check_package(self, f.render(data))
        txt = ptext(pkg.section(0))
        self.assertIn("문화교육국", txt)
        self.assertIn(weekly.item_symbol(1) + " 개막식", txt)
        self.assertIn(weekly.item_symbol(2) + " 패밀리마켓", txt)
        self.assertIn("(문화유산과 홍길동 1234)", txt)
        self.assertIn("◦ 일    시 : 2026. 11. 14.(토) 13:00 ~ 17:00", txt)
        self.assertIn("◦ 주요내용 : 장터", txt)

    def test_weekly_cover(self):
        f = registry.load_form("weekly_cover", ROOTS)
        pkg = check_package(self, f.render({"start": "2026-11-11"}))
        txt = ptext(pkg.section(0))
        self.assertIn("26. 11. 9. ~ 11. 15.", txt)
        self.assertIn("주간업무계획", txt)
        self.assertIn("문화교육국", txt)          # 순서 목차

    def test_default_body(self):
        """사업개요(사업비·사업내용) → 지난 실적 → 향후 계획 기본 틀이 두 보고 양식에서 나온다"""
        for fid in ("report_event", "report_mayor"):
            f = registry.load_form(fid, ROOTS)
            pkg = check_package(self, f.render({"title": "t"}, T.DEFAULT_STARTER))
            txt = ptext(pkg.section(0))
            for s in ("사업개요", "사 업 비", "사업내용", "지난 실적", "향후 계획", "참고자료"):
                self.assertIn(s, txt)

    def test_event_and_project_forms(self):
        ev = registry.load_form("report_event", ROOTS)
        pk = registry.load_form("report_project", ROOTS)
        t1 = ptext(check_package(self, ev.render({"title": "t"}, ev.meta["starter"])).section(0))
        for s in ("행사개요", "회의주제", "식순 및 시간표", "등록 및 개회", "국장님 하실 일"):
            self.assertIn(s, t1)
        t2 = ptext(check_package(self, pk.render({"title": "t"}, pk.meta["starter"])).section(0))
        for s in ("사업개요", "지난 실적", "향후 계획", "참고자료"):
            self.assertIn(s, t2)

    def test_mayor_reference_fragment(self):
        f = registry.load_form("report_mayor", ROOTS)
        pkg = check_package(self, f.render_fragment("---\n□ 참고자료\n◦ 가나다"))
        sec = pkg.section(0)
        self.assertIn("참고자료", ptext(sec))
        self.assertFalse(list(sec.iter(HP + "tbl")))                      # 보고 표지 틀은 안 들어감
        self.assertTrue(any(p.get("pageBreak") == "1" for p in sec.iter(HP + "p")))   # 새 쪽

    def test_weekly_schedule(self):
        f = registry.load_form("weekly_schedule", ROOTS)
        evs = [{"date": "2026-11-09", "time": "10:00", "title": "개막식", "place": "박물관", "dept": "문화유산과",
                "attend": "참석", "note": "시장, 부시장 참석", "important": True}]
        evs += [{"date": "2026-11-11", "time": "%02d:00" % (9 + i), "title": "행사%d" % i, "place": "장소", "dept": "과"}
                for i in range(6)]
        evs.append({"date": "2026-11-14", "time": "13:00", "title": "패밀리마켓", "place": "모현공원", "dept": "문화유산과"})
        pkg = check_package(self, f.render({"org": "문화교육국", "start": "2026-11-10", "events": evs}))
        sec = pkg.section(0)
        txt = ptext(sec)
        self.assertIn("문화교육국 주간 주요일정", txt)
        self.assertIn("2026. 11. 9.(월) ~ 11. 15.(일)", txt)
        self.assertIn("• 개막식", txt)
        big = [t for t in sec.iter(HP + "tbl") if t.get("colCnt") == "7"][0]
        # 헤더 1 + 월3 + 화3 + 수6(일정 6개) + 목3 + 금3 + 토3 + 일3 + 푸터 1
        self.assertEqual(int(big.get("rowCnt")), 1 + 3 + 3 + 6 + 3 + 3 + 3 + 3 + 1)
        # 요일 글자색(토요일 빨강)이 헤더에 있는 서식으로 연결됐는지: 날짜 칸 글자
        self.assertIn("11.14.", txt)
        self.assertIn("(일)", txt)


class ComposeTests(unittest.TestCase):
    def test_compose_package(self):
        from hwpgen import compose
        for name, ctype, _n in compose.SHAPES:
            pkg = compose.compose_package("12", ctype, os.path.join(FORMS, "report_event", "template.hwpx"))
            pkg2 = Package.load(pkg.to_bytes())
            comps = list(pkg2.section(0).iter(HP + "compose"))
            self.assertEqual(len(comps), 1)
            self.assertEqual(comps[0].get("circleType"), ctype)
            self.assertEqual(comps[0].get("composeText"), "12")
            self.assertEqual(len(comps[0]), 2)


class OutlineTests(unittest.TestCase):
    def test_pad_label(self):
        self.assertEqual(T.pad_label("일시"), "일    시")
        self.assertEqual(T.pad_label("사업명"), "사 업 명")
        self.assertEqual(T.pad_label("회의주제"), "회의주제")
        self.assertEqual(T.pad_label("주요 내용"), "주요내용")

    def test_parse_outline(self):
        b = T.parse_outline("□ 개요\n◦ 일시: 2026\n◦ (장소) 시청\n- 하위\n그냥 글\n| a | b |\n|---|---|\n| c | d |\n")
        self.assertEqual(b[0], ("h1", "개요"))
        self.assertEqual(b[1], ("kv", "일시", "2026"))
        self.assertEqual(b[2], ("kv", "장소", "시청"))
        self.assertEqual(b[3], ("item2", "하위"))
        self.assertEqual(b[4], ("item1", "그냥 글"))
        self.assertEqual(b[5], ("table", [["a", "b"], ["c", "d"]]))


class ExtractTests(unittest.TestCase):
    PLAN = [
        "문화교육국",
        "\U000f02ce 백제왕실사찰 제석사 특별전 개막식 (문화유산과 가나다 5708)",
        "◦ 일    시 : 2026. 11. 9.(월) 10:00         *전시기간 : 2026. 11. 10. ~ 2027. 2. 9",
        "◦ 장     소 : 국립익산박물관 기획전시실",
        "◦ 주     최 : 익산시, 국립익산박물관",
        "",
        "\U000f02cf 온가족이 함께하는 패밀리마켓(중고장터) 시즌5 (문화유산과 라마바 5802)",
        "◦ 일    시 : 2026. 11. 14.(토) 13:00 ~ 17:00 / 모현근린공원(모현도서관 옆)",
        "◦ 참    석 : 판매가족(20팀) 및 시민 누구나",
        "복지국",
        "\U000f02ce 다른 국 행사 (복지과 아무개 1111)",
        "◦ 일    시 : 2026. 11. 10.(화) 09:00",
    ]

    def test_extract(self):
        secs = weekly.split_org_sections(self.PLAN, ["문화교육국", "복지국"])
        items = weekly.parse_plan_lines(secs["문화교육국"])
        self.assertEqual(len(items), 2)
        a, b = items
        self.assertEqual(a["title"], "백제왕실사찰 제석사 특별전 개막식")
        self.assertEqual((a["dept"], a["staff"], a["phone"]), ("문화유산과", "가나다", "5708"))
        self.assertEqual((a["date"], a["time"], a["place"]), ("2026-11-09", "10:00", "국립익산박물관 기획전시실"))
        self.assertIn(("주최", "익산시, 국립익산박물관"), a["lines"])
        self.assertEqual(b["title"], "온가족이 함께하는 패밀리마켓(중고장터) 시즌5")
        self.assertEqual((b["date"], b["time"], b["end_time"]), ("2026-11-14", "13:00", "17:00"))
        self.assertEqual(b["place"], "모현근린공원(모현도서관 옆)")
        ev = weekly.extract_events(items)
        self.assertEqual([e["date"] for e in ev], ["2026-11-09", "2026-11-14"])
        self.assertEqual(ev[1]["dept"], "문화유산과")

    def test_plan_roundtrip(self):
        """계획서를 만든 뒤 다시 읽어 같은 일정이 나오는지"""
        f = registry.load_form("weekly_plan", ROOTS)
        data = {"org": "문화교육국", "items": [
            {"title": "개막식", "dept": "문화유산과", "staff": "홍길동", "phone": "1234", "date": "2026-11-09",
             "time": "10:00", "place": "국립박물관", "lines": [("주최", "익산시")]}]}
        pkg = f.render(data)
        lines = weekly.plan_text_lines(Package.load(pkg.to_bytes()))
        secs = weekly.split_org_sections(lines, ["문화교육국"])
        items = weekly.parse_plan_lines(secs["문화교육국"])
        self.assertEqual(len(items), 1)
        self.assertEqual((items[0]["date"], items[0]["time"], items[0]["place"], items[0]["title"]),
                         ("2026-11-09", "10:00", "국립박물관", "개막식"))


if __name__ == "__main__":
    unittest.main()
