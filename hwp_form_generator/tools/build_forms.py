# -*- coding: utf-8 -*-
"""사용자가 준 샘플 한글 파일에서 기본 양식 4종을 만든다.

사용: python tools/build_forms.py <미륵사_학술대회.hwpx> <타운홀미팅.hwpx> <주간업무계획.hwpx> <주간주요일정.hwpx>
(샘플에 들어 있던 실제 이름·전화번호는 양식에 남지 않는다: 입력 칸 자리표시로 바뀌고, 쓰지 않는 그림·미리보기는 지워진다)
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

from hwpgen import report, weekly  # noqa: E402
from hwpgen.package import HP, Package, char_heights  # noqa: E402

FORMS = os.path.join(os.path.dirname(HERE), "forms")


def _texts(path):
    pkg = Package.load(path)
    sec = pkg.section(0)
    hts = char_heights(pkg.header())
    out = []
    for t in sec.iter(HP + "t"):
        if t.text and t.text.strip():
            run = t.getparent()
            out.append((t.text, hts.get(run.get("charPrIDRef"), 0)))
    return out


def _title(path):
    """제목 = 글자 크기가 가장 큰 글 (2글자 이하 '보 고' 같은 고정 문구 제외)"""
    best = None
    for text, h in _texts(path):
        if len(re.sub(r"\s", "", text)) < 4:
            continue
        if best is None or h > best[1]:
            best = (text, h)
    return best[0]


def _after(path, label):
    ts = [t for t, _ in _texts(path)]
    for i, t in enumerate(ts):
        if t.strip() == label:
            return ts[i + 1].strip()
    raise ValueError("%s 칸을 찾지 못했어요" % label)


def detect_dept_fields(path):
    """국장 보고 양식: 제목 상자, '과 ( 계 / 보고 : 날짜) / 과장 이름(내선), 계장 …, 담당자 …' 한 줄"""
    ts = [t for t, _ in _texts(path)]
    line = next(t for t in ts if "보고 :" in t and "과장" in t)
    m = re.search(r"\(\s*(?P<team>[^/]+?)\s*/\s*보고\s*:\s*(?P<date>[\d. ]+?)\)\s*/\s*과장\s*(?P<head>[^,]+),\s*계장\s*(?P<lead>[^,]+),"
                  r"\s*담당자\s*(?P<staff>.+?)\s*$", line)
    dept = ts[ts.index(line) - 1].strip()
    return {"title": _title(path), "dept": dept, **{k: v.strip() for k, v in m.groupdict().items()}}


def detect_mayor_fields(path):
    return {"title": _title(path), "date": _after(path, "보고일시"), "head": _after(path, "담당과장"),
            "lead": _after(path, "담당계장"), "dept": _after(path, "담당부서"), "staff": _after(path, "담 당 자")}


def build(dept_report, mayor_report, plan, schedule):
    out = {}
    s = detect_dept_fields(dept_report)
    fields = [
        {"key": "title", "label": "제목", "sample": s["title"]},
        {"key": "dept", "label": "부서(과)", "sample": s["dept"], "profile": "dept"},
        {"key": "team", "label": "계(팀)", "sample": s["team"], "profile": "team"},
        {"key": "date", "label": "보고일", "sample": s["date"], "type": "date", "fmt": "ymd", "default": "today"},
        {"key": "head", "label": "과장 (이름(내선))", "sample": s["head"], "profile": "head"},
        {"key": "lead", "label": "계장 (이름(내선))", "sample": s["lead"], "profile": "lead"},
        {"key": "staff", "label": "담당자 (이름(내선))", "sample": s["staff"], "profile": "staff"},
    ]
    out["report_event"] = report.register_report(
        dept_report, os.path.join(FORMS, "report_event"), "report_event", "행사 보고 (식순·시간표)", "문화교육국 보고",
        fields, note="제목 상자 + 담당자 줄 + 행사개요 + 식순·시간표(표) + 국장님 하실 일",
        starter="□ 행사개요\n◦ 사 업 명: \n◦ 회의주제: \n◦ 일시: \n◦ 장소: \n◦ 사업내용: \n◦ 대행기관: \n\n"
                "□ 식순 및 시간표\n| 등록 및 개회 |\n| 00:00~00:00(00') | 참석자 등록 |\n"
                "| 00:00~00:00(00') | 개 회 사 | |\n| 00:00~00:00(00') | 축    사 | |\n"
                "| 00:00~00:00(00') | 기념촬영 | |\n| 00:00~00:00(00') | 폐회사 |\n\n"
                "□ 국장님 하실 일\n◦ ")
    out["report_project"] = report.register_report(
        dept_report, os.path.join(FORMS, "report_project"), "report_project", "사업 보고 (개요·실적·계획)", "문화교육국 보고",
        fields, note="제목 상자 + 담당자 줄 + 사업개요 + 지난 실적 + 향후 계획 + 참고자료",
        starter="□ 사업개요\n◦ 사 업 명: \n◦ 사업기간: \n◦ 사업장소: \n◦ 사 업 비: \n◦ 사업내용: \n\n"
                "□ 지난 실적\n◦ \n\n□ 향후 계획\n◦ \n\n□ 참고자료\n◦ ")
    s = detect_mayor_fields(mayor_report)
    out["report_mayor"] = report.register_report(
        mayor_report, os.path.join(FORMS, "report_mayor"), "report_mayor",
        "시장님 보고 (보고 표지 + 세부 추진계획)", "시장님 보고",
        [
            {"key": "title", "label": "제목", "sample": s["title"]},
            {"key": "date", "label": "보고일시", "sample": s["date"], "type": "date", "fmt": "long", "default": "today"},
            {"key": "head", "label": "담당과장 (이름 내선 / 휴대폰)", "sample": s["head"], "profile": "head_full"},
            {"key": "lead", "label": "담당계장 (이름 내선 / 휴대폰)", "sample": s["lead"], "profile": "lead_full"},
            {"key": "dept", "label": "담당부서", "sample": s["dept"], "profile": "dept_team"},
            {"key": "staff", "label": "담 당 자 (이름 내선 / 휴대폰)", "sample": s["staff"], "profile": "staff_full"},
        ],
        note="'보 고' 머리표 + 제목 상자 + □ 본문. 시장님 보고용. 보고 내용은 1장으로, 참고자료는 뒤에 쪽을 더 붙여 넣습니다",
        starter="□ 개요\n◦ 일시: \n◦ 장소: \n◦ 참석인원: \n\n□ 주요내용\n◦ \n\n□ 향후 계획\n◦ ")
    out["weekly_plan"] = weekly.register_plan(plan, os.path.join(FORMS, "weekly_plan"))
    out["weekly_cover"] = weekly.register_cover(plan, os.path.join(FORMS, "weekly_cover"))
    out["weekly_schedule"] = weekly.register_schedule(schedule, os.path.join(FORMS, "weekly_schedule"))
    return out


if __name__ == "__main__":
    if len(sys.argv) != 5:
        print(__doc__)
        sys.exit(1)
    res = build(*sys.argv[1:5])
    for k, v in res.items():
        print(k, "→", {a: b for a, b in v.items() if a != "style"})
