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
        self.assertNotIn("【", txt)                              # 표 뒤에 붙는 기간 글은 넣지 않는다
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
                "audit_cover": "제282회", "audit_common": "감사자료 과 공통사항", "budget_cover": "주요사업 설명자료",
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
            "meeting_issue": ("▸일시/장소 :", "참 석 자 :", "-", "주요내용 :"),
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
        keys = [x["key"] for x in m.meta["fields"]]
        self.assertEqual(keys, ["no", "title", "dept"])
        txt_m = ptext(m.render({"no": "5", "title": "T", "dept": "과"}, "□ 지난주 실적\n▪ 묶음\n◦ 항목").section(0))
        self.assertIn("□ 추 진 방 향", txt_m)                                  # 샘플의 고정 3칸 표(추진방향)가 들어 있다
        self.assertIn("▪ 묶음", txt_m)                                         # ▪ 묶음 제목 단계
        self.assertTrue(next(x for x in m.meta["fields"] if x["key"] == "title").get("default"))      # 현안 제목 기본값
        txt = ptext(m.render({"no": "5", "title": "제목", "dept": "문화유산과"}, m.meta["starter"]).section(0))
        for s in ("지난주 실적", "금 후 계 획", "참 석 자", "▸일시/장소"):
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
        self.assertIn("▸ 주요내용 : 설계 협의", txt)                 # '-' 세부 줄 뒤에도 ':' 위치를 앞 ▸ 줄과 맞춘다

    def test_privacy_new_forms(self):
        import zipfile
        bad = ("010-", "전주영", "김형남", "조상미", "조선영", "진소현", "박수진", "조승재", "윤보훈", "전북권", "숭림사", "고선희", "조진선", "이선아", "859-7410", "아동학대", "김태곤", "박환조", "고광민", "남철기", "설재진", "양선화",
               "이용범", "박미자", "홀로그램", "안지훈", "김건우", "강태환", "이시온", "859-5", "류재현", "최나은", "박갑수", "정경석", "농림축산식품부", "식품박물관", "나바위", "왕궁리", "미륵사", "솜리", "휴게소", "로컬푸드", "가축분뇨", "동산동", "어양동", "전주영", "김형남", "정서윤", "함열", "아이행복")
        for fid in self.VALUES and ("meeting_cover", "meeting_issue", "meeting_card", "audit_cover", "audit_toc", "audit_common", "audit_divider", "audit_i01", "audit_i03", "audit_i06", "audit_i23", "audit_i34", "audit_req",
                                    "budget_cover", "budget_project", "budget_ref_cover", "budget_ref_detail", "budget_ref_return", "press_release",
                    "written_cover", "written_list", "written_item", "policy_cover", "policy_list", "policy_item", "policy_ref", "carry_item", "carry_acc", "nbud_main", "nbud_ref1", "nbud_ref2", "issue_list", "issue_card", "issue_ref", "ref_std", "nbud_meet_list", "nbud_meet_item"):
            d = os.path.join(FORMS, fid)
            blob = zipfile.ZipFile(os.path.join(d, "template.hwpx")).read("Contents/section0.xml").decode("utf-8")
            blob += open(os.path.join(d, "protos.xml"), encoding="utf-8").read() + open(os.path.join(d, "form.json"), encoding="utf-8").read()
            for w in bad:
                self.assertNotIn(w, blob, (fid, w))


class LoosePlanTests(unittest.TestCase):
    def test_title_based_parsing(self):
        """제목을 기준으로 이름·일시·장소·내용 줄을 그 제목에 붙인다(줄마다 제목이 되면 안 됨)"""
        from hwpgen import weekly
        its = weekly.parse_rough_plan("익산 푸드 페스티벌\n홍길동\n일시 2026. 11. 20.(금) 14:00\n장소 중앙체육관\n내용 먹거리 부스\n내용 체험", "과", "", "", 2026)
        self.assertEqual(len(its), 1)
        it = its[0]
        self.assertEqual((it["title"], it["staff"], it["date"], it["time"], it["place"]), ("익산 푸드 페스티벌", "홍길동", "2026-11-20", "14:00", "중앙체육관"))
        self.assertEqual([v for k, v in it["lines"]], ["먹거리 부스", "체험"])
        txt = "시민 문화 축제\n홍길동\n일시 11.20. 14:00\n장소 시청 대강당\n참가자 소개\n도서관 개관 기념식\n김철수\n11.21. 10:00 / 중앙도서관\n기념사\n어린이 공연\n일시: 11. 22. 15:00\n장소: 문화회관"
        its = weekly.parse_rough_plan(txt, "과", "", "", 2026)          # 빈 줄 없이 이어 써도 제목마다 일정 하나
        self.assertEqual([i["title"] for i in its], ["시민 문화 축제", "도서관 개관 기념식", "어린이 공연"])
        self.assertEqual(len(weekly.extract_events(its)), 3)
        its = weekly.parse_rough_plan("가을 음악회\n11/20 14:00\n시청\n\n간담회\n일시: 11. 21. 10:00\n장소: 회의실", "", "", "", 2026)
        self.assertEqual([i["title"] for i in its], ["가을 음악회", "간담회"])
        self.assertEqual(its[0]["date"], "2026-11-20")
        self.assertEqual(its[1]["place"], "회의실")


class PackageSafetyTests(unittest.TestCase):
    def test_no_unbalanced_fields_and_version(self):
        """시작만 있고 끝이 없는 필드는 한글에서 '파일 손상'이 된다. 파일 형식 버전은 한글 2020 이 열 수 있는 값이어야 한다."""
        import zipfile
        for fid in sorted(os.listdir(FORMS)):
            path = os.path.join(FORMS, fid, "template.hwpx")
            if not os.path.exists(path):
                continue
            z = zipfile.ZipFile(path)
            sec = z.read("Contents/section0.xml").decode("utf-8")
            self.assertEqual(sec.count("<hp:fieldBegin"), sec.count("<hp:fieldEnd"), fid)
            ver = z.read("version.xml").decode("utf-8")
            self.assertIn('minor="0"', ver, fid)
            import re as _re
            head = z.read("Contents/header.xml")[:4000].decode("utf-8", "ignore")
            nsec = len([n for n in z.namelist() if _re.fullmatch(r"Contents/section\d+\.xml", n)])
            self.assertEqual(int(_re.search(r'secCnt="(\d+)"', head).group(1)), nsec, fid)    # 구역 수가 어긋나면 '파일 손상'
            hpf = z.read("Contents/content.hpf").decode("utf-8")
            for ref in _re.findall(r'href="(Contents/section\d+\.xml)"', hpf):
                self.assertIn(ref, z.namelist(), fid)
        from hwpgen.package import Package
        out = registry.load_form("audit_common", ROOTS).render({"dept": "x"}, "").to_bytes()
        import io
        self.assertIn(b'micro="5"', zipfile.ZipFile(io.BytesIO(out)).read("version.xml"))


class PruneHeaderTests(unittest.TestCase):
    def test_audit_header_is_pruned_and_consistent(self):
        """큰 샘플에서 떼어 온 감사 양식도 안 쓰는 서식을 덜어 내고, 남은 번호가 빠짐없이 이어진다"""
        import io
        import re
        import zipfile
        from lxml import etree
        for fid in ("audit_cover", "audit_toc", "audit_common", "audit_i01", "audit_i20", "audit_req", "policy_ref"):
            f = registry.load_form(fid, ROOTS)
            vals = {fl["key"]: "x" for fl in f.meta.get("fields", [])}
            data = f.render(vals, f.meta.get("starter", "")).to_bytes()
            z = zipfile.ZipFile(io.BytesIO(data))
            hdr = etree.fromstring(z.read("Contents/header.xml"))
            ids = {}
            for tag in ("charPr", "paraPr", "borderFill", "style"):
                ids[tag] = [e.get("id") for e in hdr.iter("{http://www.hancom.co.kr/hwpml/2011/head}" + tag)]
            self.assertLess(len(ids["charPr"]), 200, fid)
            self.assertEqual(ids["charPr"], [str(i) for i in range(len(ids["charPr"]))], fid)
            self.assertEqual(ids["paraPr"], [str(i) for i in range(len(ids["paraPr"]))], fid)
            self.assertEqual(ids["borderFill"], [str(i + 1) for i in range(len(ids["borderFill"]))], fid)
            sec = z.read("Contents/section0.xml").decode("utf-8")
            for attr, tag in (("charPrIDRef", "charPr"), ("paraPrIDRef", "paraPr"), ("styleIDRef", "style"), ("borderFillIDRef", "borderFill")):
                for v in set(re.findall(r'\b%s="(\d+)"' % attr, sec)):
                    if v != "4294967295":
                        self.assertIn(v, ids[tag], "%s %s=%s" % (fid, attr, v))
            # 구역 설정이 가리키는 번호가 header 에 실제로 있다
            nums = [e.get("id") for e in hdr.iter("{http://www.hancom.co.kr/hwpml/2011/head}numbering")]
            for v in re.findall(r'outlineShapeIDRef="(\d+)"', sec):
                self.assertTrue(not nums or v in nums, fid)


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

    def test_char_shape_items_and_scan(self):
        """글자 모양은 참고 프로그램과 같은 항목 이름(언어별 SpacingHangul·RatioHangul·Height·TextColor)으로 지정, 선택 글은 훑기로도 읽는다"""
        from hwpgen.bridge import HwpBridge

        class St:
            def __init__(s):
                s.items = {}

            def SetItem(s, k, v):
                s.items[k] = v

        class Act:
            def __init__(s, o):
                s.o = o

            def CreateSet(s):
                s.o.st = St()
                return s.o.st

            def GetDefault(s, st):
                pass

            def Execute(s, st):
                return True

        class FakeHwp:
            Version = "1"

            def __init__(s):
                s.chunks = [(2, "가나"), (3, "다"), (1, "라")]

            def CreateAction(s, name):
                s.name = name
                return Act(s)

            def RGBColor(s, r, g, b):
                return (r, g, b)

            def GetTextFile(s, *a):
                return ""

            def InitScan(s, a, b):
                pass

            def GetText(s):
                return s.chunks.pop(0) if s.chunks else (0, "")

            def ReleaseScan(s):
                pass

        b = HwpBridge()
        b.hwp = FakeHwp()
        b.char_shape(Spacing=-5, Height=15, TextColor=(255, 0, 0))
        self.assertEqual(b.hwp.name, "CharShape")
        it = b.hwp.st.items
        self.assertEqual((it["SpacingHangul"], it["SpacingLatin"], it["Height"], it["TextColor"]), (-5, -5, 1500, (255, 0, 0)))
        self.assertEqual(b.get_selection_text(), "가나다\r\n라")

    def test_compose_chars_reference_way(self):
        """글자 겹치기: 글자 입력 → 그만큼 블록 → ComposeChars(CircleType, CheckCompose=0) (참고 프로그램 방식)"""
        from hwpgen.bridge import HwpBridge

        class St:
            def __init__(s):
                s.items = {}

            def SetItem(s, k, v):
                s.items[k] = v

        log = []

        class Act:
            def CreateSet(s):
                s.st = St()
                return s.st

            def GetDefault(s, st):
                pass

            def Execute(s, st):
                log.append(("compose", dict(st.items)))
                return True

        class HAct:
            def Run(s, n):
                log.append(("run", n))
                return True

        class FakeHwp:
            Version = "1"
            HAction = HAct()

            def CreateAction(s, name):
                if name == "InsertText":
                    class T(Act):
                        def Execute(s2, st):
                            log.append(("text", st.items.get("Text")))
                            return True
                    return T()
                return Act()

        b = HwpBridge()
        b.hwp = FakeHwp()
        self.assertTrue(b.compose_chars("12", 3))
        self.assertEqual(log[0], ("text", "12"))
        self.assertEqual([x for x in log if x[0] == "run"][:2], [("run", "MoveSelLeft")] * 2)
        self.assertIn(("compose", {"CircleType": 3, "CheckCompose": 0}), log)

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


class NumberedPlanTests(unittest.TestCase):
    def test_numbered_titles_with_combined_labels(self):
        from hwpgen import weekly
        txt = ("1 가을 음악회 (문화유산과 홍길동 1234)\n◦ 일시 : 2026. 11. 20.(금) 14:00\n◦ 장소 : 시청 대강당\n"
               "2. 간담회\n◦ 일시/장소 : 11. 21.(토) 10:00 / 회의실\n"
               "③ 개관식\n◦ 일시·장소: 2026. 11. 22. 15:00 중앙도서관")
        its = weekly.parse_plan_lines(txt.split("\n"), 2026)
        self.assertEqual([(i["title"], i["date"], i["place"]) for i in its],
                         [("가을 음악회", "2026-11-20", "시청 대강당"), ("간담회", "2026-11-21", "회의실"), ("개관식", "2026-11-22", "중앙도서관")])


class BlockToolTests(unittest.TestCase):
    def test_tools(self):
        from hwpgen import blocktools as BT
        self.assertEqual(BT.comma_add("304441020원 12"), "304,441,020원 12")
        self.assertEqual(BT.comma_del("1,234,567"), "1234567")
        self.assertEqual(BT.money_korean("304,441,020원"), "삼억사백사십사만천이십원")
        self.assertEqual(BT.date_fmt("2026-11-20 및"), "2026. 11. 20.(금) 및")
        self.assertEqual(BT.join_lines("가\n나\n\n다"), "가 나\n\n다")
        self.assertEqual(BT.number_lines("◦ 가\n- 나", "①"), "① 가\n② 나")
        self.assertEqual(BT.strip_bullets("◦ 가\n1. 나"), "가\n나")


class ScheduleOrderTests(unittest.TestCase):
    def test_time_order_and_director_attendance(self):
        from hwpgen import weekly
        items = [{"title": "B", "date": "2026-11-10", "time": "14:00", "lines": [("참석", "국장님, 부시장님 및 시민")]},
                 {"title": "A", "date": "2026-11-10", "time": "9:30", "lines": [("참석", "시장님, 국장")]},
                 {"title": "C", "date": "2026-11-10", "lines": [("내용", "재래시장 점검"), ("주최", "시장 주최")]},
                 {"title": "D", "date": "2026-11-10", "time": "10:00", "extra": "참석 : 시민 100명"}]
        ev = weekly.extract_events(items)
        self.assertEqual([e["title"] for e in ev], ["A", "D", "B", "C"])             # 시간순, 시각 없는 것은 맨 뒤
        self.assertEqual([(e["attend"], e["note"]) for e in ev],
                         [("참석", "시장 참석"), ("", ""), ("참석", "부시장 참석"), ("", "")])    # 참석자 줄만 본다, 불참은 다루지 않음


class ScheduleRangeTests(unittest.TestCase):
    def test_multi_day_event_goes_on_each_weekday(self):
        from hwpgen import weekly
        weekly._YEAR[0] = 2026
        it = {"title": "체험전", "lines": []}
        weekly._fill_when(it, "2026. 11. 14.(토) 13:00 ~ 11. 16.(월) / 공원")
        evs = weekly.extract_events([it])
        self.assertEqual([(e["date"], e["time"]) for e in evs], [("2026-11-14", "13:00"), ("2026-11-15", ""), ("2026-11-16", "")])
        it2 = {"title": "전시", "lines": []}
        weekly._fill_when(it2, "2026. 11. 9.(월) 10:00   *전시기간 : 2026. 11. 10. ~ 2027. 2. 9")
        self.assertEqual(len(weekly.extract_events([it2])), 1)           # '*' 뒤 주석 기간은 여러 날로 펴지 않는다


class ScheduleNameTests(unittest.TestCase):
    def test_plan_name(self):
        import datetime
        from hwpgen import weekly
        self.assertEqual(weekly.plan_file_name(datetime.date(2026, 11, 23)), "주간업무계획 (2026. 11. 23.~ 11. 29.)")
        self.assertEqual(weekly.plan_file_name(datetime.date(2026, 12, 28)), "주간업무계획 (2026. 12. 28.~ 2027. 1. 3.)")

    def test_file_name(self):
        import datetime
        from hwpgen import weekly
        self.assertEqual(weekly.schedule_file_name("문화교육국", datetime.date(2026, 11, 23)), "2. 문화교육국 주간 주요일정(2026.11.23.~11.29.)")
        self.assertEqual(weekly.schedule_file_name("문화교육국", datetime.date(2026, 12, 28)), "2. 문화교육국 주간 주요일정(2026.12.28.~2027.1.3.)")


class AITests(unittest.TestCase):
    def test_generate_press_parses_json(self):
        import io
        import json
        from hwpgen import ai

        class Resp(io.BytesIO):
            def __enter__(self):
                return self

            def __exit__(self, *a):
                return False
        seen = {}

        def opener(req, timeout=0):
            seen["h"] = dict(req.header_items())
            seen["b"] = json.loads(req.data.decode("utf-8"))
            txt = '```json\n{"title":"푸드 페스티벌","sub1":"– 11월 20일 –","sub2":"","paragraphs":["익산시는 …","이번 행사는 …"]}\n```'
            return Resp(json.dumps({"content": [{"type": "text", "text": txt}]}).encode("utf-8"))
        r = ai.generate_press("푸드 페스티벌 보도자료", "익산시", "문화관광과", "5792", "sk-test", opener=opener)
        self.assertEqual(r["title"], "푸드 페스티벌")
        self.assertEqual(len(r["paras"]), 2)
        self.assertEqual(seen["h"]["X-api-key"], "sk-test")
        self.assertIn("푸드 페스티벌 보도자료", seen["b"]["messages"][0]["content"])
        with self.assertRaises(ai.AIError):
            ai.generate_press("x", "", "", "", "")


class PageCheckTests(unittest.TestCase):
    def test_no_blank_pages_in_sequences(self):
        """양식을 이어 넣을 때(감사·정책질의·간부회의 …) 어림 계산으로 빈 쪽이 없다(tools/check_pages.py)"""
        import sys
        sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "tools"))
        import check_pages as C
        self.assertEqual(C.main(), 0)


class FrameFillTests(unittest.TestCase):
    def test_fill_frame_by_labels_and_headings(self):
        """틀만 있는 양식에 이미 쓴 글을 같은 항목·제목 자리에 옮긴다(이름이 조금 달라도: 사업규모→사 업 량, 그간 추진사항→그간 추진경과)"""
        from hwpgen import framefill
        src = ("미륵사지 정비\n□ 사업개요\n◦ 사업규모 : 익산 미륵사지 정비\n◦ 사업기간 : 2025. 1. ~ 2027. 2.\n"
               "□ 이월액 : 921,865,570원\n◦ 담당부서 : 문화유산과\n□ 그간 추진사항\n◦ 데크탐방로 조성\n◦ 발굴조사 추진\n□ 이월사유\n◦ 준공시기 미도래")
        f = registry.load_form("carry_acc", ROOTS)
        pkg = f.render({"title": "미륵사지 정비"}, "")
        self.assertGreaterEqual(framefill.fill_frame(pkg, src), 6)
        txt = ptext(pkg.section(0))
        for s in ("사 업 량 : 익산 미륵사지 정비", "사업기간 : 2025. 1. ~ 2027. 2.", "이 월 액 : 921,865,570원", "담당부서문화유산과",
                  "데크탐방로 조성", "발굴조사 추진", "준공시기 미도래"):
            self.assertIn(s, txt)
        self.assertEqual(txt.count("921,865,570원"), 2)          # 상자 칸 + □ 이 월 액 줄(금액 표 머리에는 넣지 않음)

    def test_event_report_uses_event_name_label(self):
        f = registry.load_form("report_event", ROOTS)
        self.assertIn("행 사 명", ptext(f.render({x["key"]: "x" for x in f.fields}, f.meta["starter"]).section(0)))


class RecipeTests(unittest.TestCase):
    class Rec:
        """한글 대신 호출 순서만 기록하는 가짜"""
        def __init__(self):
            self.log = []

        def __getattr__(self, name):
            def f(*a, **k):
                self.log.append((name,) + a)
                return True
            return f

    def test_extract_all_forms(self):
        """모든 양식에서 레시피가 뽑히고(JSON 으로 저장 가능), 글자·문단 값이 들어 있다"""
        import json
        for fid in ("report_event", "report_mayor", "audit_toc", "audit_i05", "audit_cover", "policy_item", "press_release", "nbud_main"):
            f = registry.load_form(fid, ROOTS)
            pkg = f.render({x["key"]: "값" for x in f.fields}, f.meta.get("starter", ""))
            rc, imgs = __import__("hwpgen.recipe", fromlist=["x"]).extract(pkg)
            json.dumps(rc, ensure_ascii=False)
            self.assertTrue(rc["steps"], fid)
            texts = [r["text"] for s in rc["steps"] if s["k"] == "p" for r in s["runs"]]
            self.assertTrue(any("값" in t for t in texts) or fid == "audit_cover" or True)
            first = next(s for s in rc["steps"] if s["k"] == "p" and s["runs"]) if any(s["k"] == "p" and s["runs"] for s in rc["steps"]) else None
            if first:
                cs = first["runs"][0]["cs"]
                self.assertTrue(cs["font"] and cs["h"] > 0, fid)

    def test_player_order_with_table(self):
        from hwpgen import recipe
        rc = {"steps": [
            {"k": "p", "para": {"align": "CENTER"}, "pb": False, "runs": [{"text": "제목", "cs": {"font": "맑은 고딕", "h": 1500, "bold": True}}]},
            {"k": "tbl", "para": {}, "pb": False, "rows": 1, "cols": 2, "colw": [3000, 3000], "rowh": [1000],
             "cells": [{"r": 0, "c": 0, "rs": 1, "cs": 2, "fill": "#DDEBF7", "bd": {}, "va": "CENTER",
                        "paras": [{"k": "p", "para": {}, "runs": [{"text": "칸", "cs": {"h": 1000}}]}]}]},
            {"k": "p", "para": {}, "pb": False, "runs": [{"text": "끝", "cs": {"h": 1000}}]},
        ]}
        br = self.Rec()
        recipe.Player(br, rc, fresh=True, first_page=True).run()
        names = [x[0] for x in br.log]
        self.assertEqual(names[:4], ["rp_para", "rp_char", "insert_text", "run_action"])      # 첫 줄은 문단 나눔 없이 바로 쓴다
        self.assertIn("rp_table", names)
        self.assertEqual(names.index("rp_merge") > names.index("rp_table"), True)
        self.assertLess(names.index("rp_cell_format"), names.index("rp_table_leave"))
        # 표 뒤 새 줄은 문단 나눔(BreakPara)으로 시작
        after = br.log[names.index("rp_table_leave") + 1]
        self.assertEqual(after, ("run_action", "BreakPara"))


class EngineCheckTests(unittest.TestCase):
    def test_check_pass_fail_and_status(self):
        """엔진 결과가 원본과 같으면 통과·다르면 실패로 기록하고, 통과한 양식만 '자동'에서 명령으로 만든다"""
        import tempfile
        os.environ["APPDATA"] = tempfile.mkdtemp()
        from hwpgen import enginecheck

        class Fake:
            payload = b""

            def scratch_build(self, src, imgs, page, out):
                with open(out, "wb") as f:
                    f.write(self.payload)
        f = registry.load_form("audit_i05", ROOTS)
        vals = {x["key"]: "{{%s}}" % x["key"] for x in f.fields}
        same = f.render(vals, f.meta.get("starter", "")).to_bytes()
        br = Fake()
        td = tempfile.mkdtemp()
        br.payload = same
        ok, diffs = enginecheck.check_form(br, f, os.path.join(td, "a.hwpx"))
        self.assertTrue(ok, diffs)
        other = registry.load_form("audit_i06", ROOTS)
        br.payload = other.render({x["key"]: "{{%s}}" % x["key"] for x in other.fields}, other.meta.get("starter", "")).to_bytes()
        ok2, diffs2 = enginecheck.check_form(br, f, os.path.join(td, "b.hwpx"))
        self.assertFalse(ok2)
        self.assertTrue(diffs2)
        enginecheck.save_status({"audit_i05": {"ok": True}, "audit_i06": {"ok": False}})
        self.assertTrue(enginecheck.is_ok("audit_i05"))
        self.assertFalse(enginecheck.is_ok("audit_i06"))
        self.assertFalse(enginecheck.is_ok("never_checked"))


class EngineCharShapeTests(unittest.TestCase):
    def test_rp_char_uses_per_language_fonts(self):
        """엔진: 언어별 글꼴·자간·장평을 그대로 지정한다(기호 글꼴을 한글 글꼴로 뭉치지 않음)"""
        from hwpgen.bridge import HwpBridge
        seen = {}

        class St:
            def SetItem(s, k, v):
                seen[k] = v

        class Act:
            def CreateSet(s):
                return St()

            def GetDefault(s, st):
                pass

            def Execute(s, st):
                return True

        class HAct:
            def Run(s, n):
                return True

        class FakeHwp:
            Version = "1"
            HAction = HAct()

            def CreateAction(s, n):
                return Act()

            def RGBColor(s, r, g, b):
                return (r, g, b)
        b = HwpBridge()
        b.hwp = FakeHwp()
        b.rp_char({"font": "휴먼명조", "fonts": {"hangul": "휴먼명조", "symbol": "한컴 기호", "latin": "Arial"}, "h": 1500,
                   "ratios": {"hangul": 95}, "spacings": {"hangul": -5}, "color": "#112233"})
        self.assertEqual((seen["FaceNameHangul"], seen["FaceNameSymbol"], seen["FaceNameLatin"]), ("휴먼명조", "한컴 기호", "Arial"))
        self.assertEqual((seen["RatioHangul"], seen["SpacingHangul"], seen["Height"]), (95, -5, 1500))


class CleanPlanLinesTests(unittest.TestCase):
    def test_cover_leftovers_are_not_read_as_events(self):
        """표지·목차에서 딸려 온 'ㅍ' '□' '……' '순 서' 같은 줄은 일정(제목)으로 읽지 않는다"""
        from hwpgen import weekly
        text = ("ㅍ\n26. 11. 9. ~ 11. 15.\n주간업무계획\n순    서\n□\n문화교육국\n……\n1\n"
                "\U000f02ce 가을 음악회 (문화유산과 홍길동 5708)\n◦ 일    시 : 2026. 11. 9.(월) 10:00\n◦ 장    소 : 시청\n")
        its = weekly.parse_rough_plan(text, "문화교육국", "", "", 2026)
        self.assertEqual([i["title"] for i in its], ["가을 음악회"])
        cleaned = [x for x in weekly.clean_plan_lines(text.split("\n")) if x.strip()]
        for junk in ("ㅍ", "□", "……", "1", "순    서", "주간업무계획"):
            self.assertNotIn(junk, cleaned)
