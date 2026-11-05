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

OUTLINE_MAYOR = """□ 직무연수 계획
◦ 일자: 2026. 9. 16.(수) 14:00
▸ 1일차 연수
◦ 장소: 익산시청 지하 1층 로비
◦ 참석자: 50명 내외
◦ 주요내용: 분과별 발표 후 토론
▸ 토론
"""


def all_ids(sec):
    ids = []
    for el in sec.iter():
        if el.tag.split("}")[-1] in ("tbl", "container", "pic") and el.get("id"):
            ids.append(el.get("id"))
    return ids


_ALLOWED = {}


def allowed_attrs():
    """원본에서 온 틀(template.hwpx·protos.xml)에 실제로 있던 요소별 속성 이름 — 이 밖의 속성은 한글이 '손상'으로 본다"""
    if not _ALLOWED:
        import zipfile
        from lxml import etree
        for fid, folder, meta in registry.list_forms(ROOTS):
            z = zipfile.ZipFile(os.path.join(folder, "template.hwpx"))
            roots = [etree.fromstring(z.read("Contents/section0.xml")), etree.fromstring(z.read("Contents/header.xml"))]
            for root in roots:
                for el in root.iter():
                    _ALLOWED.setdefault(etree.QName(el).localname, set()).update(etree.QName(k).localname for k in el.attrib)
    return _ALLOWED


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
    # 내부 보관용 속성(dcp·bcp·mark …)이나 원본에 없던 속성이 만든 파일에 남아 있으면 안 된다
    from lxml import etree as _et
    allowed = allowed_attrs()
    for el in sec.iter():
        name = _et.QName(el).localname
        for k in el.attrib:
            kn = _et.QName(k).localname
            tc.assertIn(kn, allowed.get(name, set()) | {"id"}, "원본에 없던 속성: <%s %s>" % (name, kn))
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
        self.assertIn("직무연수 계획", txt)
        self.assertIn("타운홀미팅 시험", txt)

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
        for s in ("행사개요", "회의주제", "식순 및 시간표", "국장님 하실 일"):
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


class NewFormsTests(unittest.TestCase):
    VALUES = {"title": "시험 제목", "dept": "○○과", "date": "2026-11-09", "mdate": "2026-11-09", "mtime": "08:30", "no": "3",
              "team_title": "△△계장", "lead": "홍길동 1111 / 010-0000-0000", "staff": "이순신 2222 / 010-1111-1111",
              "adate": "6. 11.", "session": "제282회", "org": "문화교육국", "mdt": "12. 9.(화) 10:00", "round": "(제2차 정례회)",
              "btitle": "― 2026년도 제2회 추가경정 예산(안) ―", "page": "2", "policy": "정책 A", "unit": "단위 B",
              "detail": "세부 C", "pdate": "2026-11-10", "ptime": "조간용", "embargo": "", "team": "△△계",
              "head_name": "가", "head_tel": "859-1", "lead_name": "나", "lead_tel": "859-2", "staff_name": "다",
              "staff_tel": "859-3", "pages": "2", "sub1": "- 부제 -", "sub2": "", "photo_time": "",
              "committee": "○○위원회", "ministry": "○○부", "account": "일반회계", "kind": "신규", "no": "3", "ref": "참고1",
              "request": "건의 문구", "issue": "현안 제목"}

    def test_all_new_forms_render(self):
        want = {"meeting_cover": "현안·당면 업무보고", "meeting_issue": "현안업무(3)", "meeting_card": "프로그램 내용",
                "audit_cover": "제282회", "audit_common": "감사자료 과 공통사항", "audit_item": "확인자", "budget_cover": "주요사업 설명자료",
                "budget_project": "세부사업명 : 세부 C", "press_release": "시험 제목", "written_cover": "서면질의서", "written_list": "서면질의 목록",
                "written_item": "< ○○위원회 / ○○부 소관 / 일반회계 >", "policy_cover": "정책질의서", "policy_list": "활동사업 목록",
                "policy_item": "정책질의서", "policy_ref": "건의 문구", "budget_ref_cover": "(참고자료)", "budget_ref_detail": "시험 제목", "budget_ref_return": "시험 제목"}
        for fid, needle in want.items():
            f = registry.load_form(fid, ROOTS)
            outline = (f.meta.get("starter") or "") if f.meta.get("has_body", True) else ""
            pkg = check_package(self, f.render(self.VALUES, outline))
            self.assertIn(needle, ptext(pkg.section(0)), fid)

    def test_press_release_body(self):
        f = registry.load_form("press_release", ROOTS)
        pkg = check_package(self, f.render(self.VALUES, "첫 문단\n둘째 문단\n\n셋째 문단"))
        paras = ["".join(t.text or "" for r in p.findall(HP + "run") for t in r.findall(HP + "t"))
                 for p in pkg.section(0).findall(HP + "p")]
        body = [x for x in paras[7:]]
        self.assertEqual([b for b in body if b.strip()], [" 첫 문단", " 둘째 문단", " 셋째 문단"])   # 앞 한 칸 들여쓰기
        self.assertEqual(body[1], "")                                                                 # 문단 사이 빈 줄

    def test_press_release_frame_only(self):
        """보도자료 단추: 상단 틀·제목 양식만, 본문은 샘플 모양의 빈 문단 하나(안내 문구 없음)"""
        f = registry.load_form("press_release", ROOTS)
        self.assertEqual(f.meta.get("starter", ""), "")
        pkg = check_package(self, f.render(self.VALUES, ""))
        paras = ["".join(t.text or "" for r in p.findall(HP + "run") for t in r.findall(HP + "t"))
                 for p in pkg.section(0).findall(HP + "p")]
        self.assertEqual(paras[7:], [" "])
        self.assertNotIn("첫 문단", ptext(pkg.section(0)))

    def test_starters_keep_labels_and_markers(self):
        """단추를 누르면 항목 이름(사업개요·일시·장소 …)과 ◦ ▸ - 표시, 표 머리·구분 이름이 서식대로 들어간다"""
        want = {
            "report_event": ("행사개요", "일    시 :", "장    소 :", "사업내용 :", "식순 및 시간표", "등록 및 개회", "개 회 사", "국장님 하실 일"),
            "report_project": ("사업개요", "사업기간 :", "사업장소 :", "사 업 비 :", "사업내용 :", "지난 실적", "향후 계획", "참고자료", "▸"),
            "report_mayor": ("◦ 일    자", "◦ 장    소", "◦ 참 석 자", "◦ 주요내용"),
            "meeting_issue": ("▸일시/장소 :", "▸참 석 자 :", "-", "▸주요내용 :"),
            "written_item": ("ㅇ (사업내용)", "ㅇ (총사업비)", "-", "□ 요구사항 :"),
        }
        for fid, needles in want.items():
            f = registry.load_form(fid, ROOTS)
            pkg = check_package(self, f.render(self.VALUES, f.meta["starter"]))
            sec = pkg.section(0)
            txt = " ".join("".join(t.text or "" for t in p.iter(HP + "t")) for p in sec.iter(HP + "p"))
            for n in needles:
                self.assertIn(n, txt, (fid, n))

    def test_guides_are_not_mangled(self):
        """안내문 [..] 은 글자 그대로 들어가고 항목 이름(…)처럼 바뀌지 않는다. 현안 보고는 지난주 실적+금후 계획 두 구역"""
        f = registry.load_form("policy_item", ROOTS)
        txt = ptext(f.render(self.VALUES, f.meta["starter"]).section(0))
        for s in ("[정부 정책·추진 배경]", "[익산의 현황·기반]", "[문제점·추진 과제]", "이에 대한 견해를 말씀해 주시기 바람", "<Q>"):
            self.assertIn(s, txt)
        m = registry.load_form("meeting_issue", ROOTS)
        self.assertEqual(m.meta["fields"][1].get("default"), "고도 익산 역사·문화자원 브랜드화 및 지역 명소화")   # 현안 제목 기본값
        txt = ptext(m.render({"no": "5", "title": "제목", "dept": "문화유산과"}, m.meta["starter"]).section(0))
        for s in ("지난주 실적", "금 후 계 획", "▸참 석 자", "▸일시/장소"):
            self.assertIn(s, txt)

    def test_query_items(self):
        """질의서: ㅇ (사업내용) 줄·- 줄·□ 요구사항, 정책질의서 <Q> 상자, 총괄표 사업명 자리"""
        f = registry.load_form("written_item", ROOTS)
        pkg = check_package(self, f.render(self.VALUES, "□ 사업개요\nㅇ (사업내용) 가나다\n\n□ 필요성 및 기대효과\nㅇ 첫째\n- 하위\n\n□ 요구사항 : 10억원 반영"))
        txt = ptext(pkg.section(0))
        self.assertEqual(txt.count("시험 제목"), 2)                   # 머리 + 총괄표 첫 칸
        for s in ("(사업내용)", "가나다", "하위", "□ 요구사항 : 10억원 반영"):
            self.assertIn(s, txt)
        g = registry.load_form("policy_item", ROOTS)
        pkg = check_package(self, g.render(self.VALUES, "□ 취지\nㅇ 설명\n\n| **<Q>** 질의 내용 |"))
        sec = pkg.section(0)
        self.assertIn("질의 내용", ptext(sec))
        base = g.render(self.VALUES, "□ 취지\nㅇ 설명").section(0)
        self.assertEqual(len(list(sec.iter(HP + "tbl"))) - len(list(base.iter(HP + "tbl"))), 1)   # <Q> 상자 한 개가 더 생김
        h = registry.load_form("policy_ref", ROOTS)
        pkg = check_package(self, h.render(self.VALUES, h.meta["starter"]))
        tails = [p for p in pkg.section(0).findall(HP + "p")][-2:]
        self.assertTrue(all(any(c.tag == HP + "tbl" for r in p.findall(HP + "run") for c in r) for p in tails))  # 건의 상자·담당 표는 끝에 남음

    def test_sub_levels(self):
        f = registry.load_form("meeting_issue", ROOTS)
        pkg = check_package(self, f.render(self.VALUES, "□ 금 후 계 획\n◦ 협의\n▸일시/장소: 2026. 11. 11.(수) / 센터\n- (익산시) 담당\n▸주요내용: 설계 협의"))
        txt = ptext(pkg.section(0))
        self.assertIn("▸일시/장소 : 2026. 11. 11.(수) / 센터", txt.replace("일시/장소", "일시/장소"))
        self.assertIn("-(익산시) 담당", txt.replace(" ", "", 0).replace("- (익산시)", "-(익산시)"))
        self.assertIn("▸주요내용", txt)

    def test_privacy_new_forms(self):
        import zipfile
        bad = ("010-", "전주영", "김형남", "조상미", "조선영", "진소현", "박수진", "조승재", "윤보훈", "전북권", "숭림사", "고선희", "조진선", "이선아", "859-7410", "아동학대", "김태곤", "박환조", "고광민", "남철기", "설재진", "양선화",
               "이용범", "박미자", "홀로그램", "전주영", "김형남", "정서윤", "함열", "아이행복")
        for fid in self.VALUES and ("meeting_cover", "meeting_issue", "meeting_card", "audit_cover", "audit_common", "audit_item",
                                    "budget_cover", "budget_project", "budget_ref_cover", "budget_ref_detail", "budget_ref_return", "press_release",
                    "written_cover", "written_list", "written_item", "policy_cover", "policy_list", "policy_item", "policy_ref"):
            d = os.path.join(FORMS, fid)
            blob = zipfile.ZipFile(os.path.join(d, "template.hwpx")).read("Contents/section0.xml").decode("utf-8")
            blob += open(os.path.join(d, "protos.xml"), encoding="utf-8").read() + open(os.path.join(d, "form.json"), encoding="utf-8").read()
            for w in bad:
                self.assertNotIn(w, blob, (fid, w))


class BridgeTests(unittest.TestCase):
    def test_insert_once_even_if_falsy(self):
        from hwpgen.bridge import HwpBridge

        class PS:
            HSet = object()
            filename = KeepSection = KeepCharshape = KeepParashape = KeepStyle = None

        class HAction:
            def __init__(s, owner):
                s.o = owner

            def GetDefault(s, name, hset):
                pass

            def Execute(s, name, hset):
                s.o.calls.append(name)
                return False                      # 성공해도 거짓값을 돌려주는 한글

        class FakeHwp:
            Version = "1"

            def __init__(s):
                s.calls = []
                s.HAction = HAction(s)
                s.HParameterSet = type("X", (), {"HInsertFile": PS()})()

            def Insert(s, path, fmt, arg):
                s.calls.append("Insert:" + fmt)

        b = HwpBridge()
        b.hwp = FakeHwp()
        b.insert_file(os.path.join(FORMS, "report_event", "template.hwpx"))
        # InsertFile 이 거짓값이면 옛 Insert 로 '한 번만' 더 시도 — 각각 한 번씩, 그 이상은 없다
        self.assertEqual(b.hwp.calls, ["InsertFile", "Insert:HWPX"])
        ps = b.hwp.HParameterSet.HInsertFile
        self.assertEqual((ps.KeepSection, ps.KeepCharshape, ps.KeepParashape, ps.KeepStyle), (0, 1, 1, 1))

    def test_strip_and_chunks(self):
        from hwpgen.package import strip_section_props, split_chunks, page_setup_of
        f = registry.load_form("report_event", ROOTS)
        pkg = f.render({"title": "t"}, f.meta["starter"])
        page = page_setup_of(pkg)
        self.assertTrue(page and page["width"] > 50000)
        strip_section_props(pkg)
        self.assertFalse(list(pkg.section(0).iter(HP + "secPr")))
        chunks = split_chunks(pkg)
        self.assertGreater(len(chunks), 5)
        total = sum(len(c.section(0).findall(HP + "p")) for c in chunks)
        self.assertEqual(total, len(pkg.section(0).findall(HP + "p")) + len(chunks) - 1)   # 조각마다(마지막 빼고) 받침 문단 하나
        # 받침 문단: 다음 조각 첫 문단과 문단 모양이 같고 내용이 없다
        for a, b in zip(chunks, chunks[1:]):
            donor = a.section(0).findall(HP + "p")[-1]
            first = b.section(0).findall(HP + "p")[0]
            self.assertEqual(donor.get("paraPrIDRef"), first.get("paraPrIDRef"))
            self.assertEqual(donor.get("pageBreak"), first.get("pageBreak"))
            self.assertEqual("".join(t.text or "" for t in donor.iter(HP + "t")), "")
        for c in chunks:
            check_package(self, c) if False else Package.load(c.to_bytes())


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


class ImageTests(unittest.TestCase):
    def test_referenced_images_are_real(self):
        """양식이 가리키는 그림(표 머리 줄 속 그림·배경 그림 포함)은 빈 그림으로 바뀌어 있으면 안 된다"""
        import re
        import zipfile
        from hwpgen import tinyimg
        checked = 0
        for fid, folder, meta in registry.list_forms(ROOTS):
            z = zipfile.ZipFile(os.path.join(folder, "template.hwpx"))
            blob = z.read("Contents/section0.xml").decode("utf-8") + z.read("Contents/header.xml").decode("utf-8")
            pp = os.path.join(folder, "protos.xml")
            if os.path.exists(pp):
                blob += open(pp, encoding="utf-8").read()
            refs = set(re.findall(r'binaryItemIDRef="([^"]+)"', blob))
            names = {os.path.splitext(os.path.basename(n))[0]: n for n in z.namelist() if n.startswith("BinData/")}
            for ref in refs:
                if ref not in names:
                    continue
                data = z.read(names[ref])
                self.assertNotEqual(data, tinyimg.blank_like(names[ref]), (fid, ref, "빈 그림으로 바뀜"))
                checked += 1
        self.assertGreater(checked, 5)


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
        self.assertEqual(b[3], ("item3", "하위"))
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
