# -*- coding: utf-8 -*-
"""업무(기한·폴더·한글 공문·반복 업무) 시험. 실행: python tests/test_work.py  (olefile 필요)
모든 문서는 지어낸 내용으로 시험 때마다 새로 만든다."""
import datetime as dt
import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path[:0] = [os.path.dirname(HERE), HERE]

import core  # noqa: E402
import fixtures as F  # noqa: E402
import gongmun  # noqa: E402
import hwpdoc  # noqa: E402
import work  # noqa: E402

D = dt.date
TODAY = D(2026, 10, 13)
TMP = tempfile.mkdtemp(prefix="dc_work_")
P = lambda *a: os.path.join(TMP, *a)  # noqa: E731

GONGMUN = [
    "도시재생과",
    "수신자 수신자 참조",
    "제목 2027년 도시재생 뉴딜 수요조사 제출 요청",
    "1. 관련: 도시재생과-1234(2026. 10. 1.)",
    "2. 2027년 사업 수요를 붙임 양식에 따라 작성하여 2026. 10. 20.(화)까지 제출하여 주시기 바랍니다.",
    "가. 설명회: 2026. 10. 15.(목) 14:00, 시청 3층 회의실",
    "붙임 수요조사 양식 1부. 끝.",
    "수신자 건축과장, 도로과장, 행정지원과장, 중앙동장",
]


def test_hwp_gongmun():
    F.make_hwp(P("공문.hwp"), GONGMUN)
    text = hwpdoc.read_text(P("공문.hwp"))
    title, items = gongmun.parse(text, TODAY)
    assert title.startswith("2027년 도시재생 뉴딜 수요조사"), title
    dates = {i["date"] for i in items}
    assert D(2026, 10, 20) in dates and D(2026, 10, 15) in dates, items
    assert work.due_from_items(items) == D(2026, 10, 20)
    assert work.doc_no(text) == "도시재생과-1234"
    F.make_hwpx(P("공문.hwpx"), GONGMUN)
    assert gongmun.parse(hwpdoc.read_text(P("공문.hwpx")), TODAY)[0] == title


def test_case_folder():
    data = {"_deleted": {}}
    root = P("업무")
    c = work.add_case(data, work.new_case("2027년 도시재생 수요조사", D(2026, 10, 20)))
    path = work.ensure_folder(root, c)
    assert path.endswith(os.path.join("2026", "1020 2027년 도시재생 수요조사")), path
    for s in work.SUBDIRS:
        assert os.path.isdir(os.path.join(path, s))
    F.make_hwp(P("공문2.hwp"), GONGMUN)
    work.add_doc(c, P("공문2.hwp"))
    work.add_doc(c, P("공문2.hwp"))                  # 같은 이름은 (2) 를 붙여 덮어쓰지 않는다
    assert [os.path.basename(f) for f in work.files(c, 0)] == ["공문2 (2).hwp", "공문2.hwp"]
    F.make_docx(P("작성.docx"), ["우리 과 자료"])
    work.add_file(c, P("작성.docx"))
    assert len(work.files(c, 1)) == 1 and work.files(c, 2) == []
    assert work.dday(c, TODAY) == 7 and work.dday_label(7) == "D-7" and work.fmt_date(c["due"]) == "10.20.(화)"
    assert core.export_state(data)["work"][c["id"]]["title"] == "2027년 도시재생 수요조사"
    assert work.safe_name('a/b:c*?') == "a b c"


def test_templates():
    data = {"_deleted": {}}
    first = {t[1]: work.template_first(t[2], TODAY) for t in work.TEMPLATES}
    assert first["월간 업무계획 제출"] == (D(2026, 10, 25), "m")
    assert first["업무추진비·법인카드 사용내역 정리"] == (D(2026, 10, 31), "ml")
    assert first["사이버 보안진단의 날 (PC 점검)"] == (D(2026, 10, 21), "mw")
    assert first["분기별 추진실적 보고"] == (D(2027, 1, 5), "q")
    assert first["행정사무감사 자료 준비"] == (D(2026, 10, 25), "y")
    assert first["상·하반기 자체평가 자료 작성"] == (D(2026, 12, 20), "h")
    for _, _, rule in work.TEMPLATES:
        d, code = work.template_first(rule, TODAY)
        rep = core.repeat_from_code(code, d)
        assert core.repeat_hit(rep, d, d), rule
    assert work.rule_label(("nth", 3, 2)) == "매월 셋째 수요일"
    # 업무 지우기: 묶인 일정의 연결도 끊고 삭제 기록
    case = work.add_case(data, work.new_case("수요조사", D(2026, 10, 20)))
    a = core.new_item("설명회", case=case["id"])
    core.add_item(data, "2026-10-14", a)
    work.delete_case(data, case["id"])
    assert case["id"] in data["_deleted"] and "case" not in a and not data["_work"]


def test_hwp_distribution_preview():
    F.make_hwp(P("배포.hwp"), ["무시"], distribution=True, preview="제목 배포용 공문\r\n2026. 11. 2.까지 회신")
    t = hwpdoc.read_text(P("배포.hwp"))
    assert t.startswith("제목 배포용 공문") and "\x00" not in t


if __name__ == "__main__":
    n = 0
    for k, fn in list(globals().items()):
        if k.startswith("test_") and callable(fn):
            fn()
            n += 1
            print("ok", k)
    print(f"{n} tests passed ({TMP})")
