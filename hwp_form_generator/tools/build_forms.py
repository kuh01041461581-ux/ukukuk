# -*- coding: utf-8 -*-
"""사용자가 준 샘플 한글 파일에서 기본 양식 4종을 만든다.

사용: python tools/build_forms.py <미륵사_학술대회.hwpx> <타운홀미팅.hwpx> <주간업무계획.hwpx> <주간주요일정.hwpx>
      [meeting=간부회의자료.hwpx meeting_ref=간부회의참고자료.hwpx audit=행정사무감사.hwpx budget=예산설명자료.hwpx budget_ref=예산참고자료.hwpx]
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
        {"key": "dept", "label": "부서(과)", "sample": s["dept"], "profile": "dept", "blank": "      "},
        {"key": "team", "label": "계(팀)", "sample": s["team"], "profile": "team", "blank": "      "},
        {"key": "date", "label": "보고일", "sample": s["date"], "type": "date", "fmt": "ymd", "default": "today"},
        {"key": "head", "label": "과장 (이름(내선))", "sample": s["head"], "profile": "head", "blank": "(      )"},
        {"key": "lead", "label": "계장 (이름(내선))", "sample": s["lead"], "profile": "lead", "blank": "(      )"},
        {"key": "staff", "label": "담당자 (이름(내선))", "sample": s["staff"], "profile": "staff", "blank": "(      )"},
    ]
    out["report_event"] = report.register_report(
        dept_report, os.path.join(FORMS, "report_event"), "report_event", "행사 보고 (식순·시간표)", "문화교육국 보고",
        [dict(f, default="(행사명)") if f["key"] == "title" else f for f in fields],
        note="제목 상자 + 담당자 줄 + 행사개요 + 식순·시간표(표) + 국장님 하실 일",
        starter="□ 행사개요\n◦ 사 업 명: \n◦ 회의주제: \n◦ 일시: \n◦ 장소: \n◦ 사업내용: \n◦ 대행기관: \n\n"
                "□ 식순 및 시간표\n| **등록 및 개회** |\n| | **참석자 등록** |\n| | **개 회 사** | |\n| | **축    사** | |\n"
                "| | **기념촬영** | |\n| | **장내 정리** |\n| **본 행 사** |\n| | | |\n| | | |\n| | **폐회사** |\n\n"
                "□ 국장님 하실 일\n◦ \n")
    out["report_project"] = report.register_report(
        dept_report, os.path.join(FORMS, "report_project"), "report_project", "사업 보고 (개요·실적·계획)", "문화교육국 보고",
        [dict(f, default="(사업명)") if f["key"] == "title" else f for f in fields],
        note="제목 상자 + 담당자 줄 + 사업개요 + 지난 실적 + 향후 계획 + 참고자료",
        starter="□ 사업개요\n◦ 사 업 명: \n◦ 사업기간: \n◦ 사업장소: \n◦ 사 업 비: \n◦ 사업내용: \n\n"
                "□ 지난 실적\n◦ \n▸\n\n□ 향후 계획\n◦ \n▸\n\n□ 참고자료\n◦ ")
    s = detect_mayor_fields(mayor_report)
    out["report_mayor"] = report.register_report(
        mayor_report, os.path.join(FORMS, "report_mayor"), "report_mayor",
        "시장님 보고 (보고 표지 + 세부 추진계획)", "시장님 보고",
        [
            {"key": "title", "label": "제목", "sample": s["title"], "default": "(사업명)"},
            {"key": "date", "label": "보고일시", "sample": s["date"], "type": "date", "fmt": "long", "default": "today"},
            {"key": "head", "label": "담당과장 (이름 내선 / 휴대폰)", "sample": s["head"], "profile": "head_full"},
            {"key": "lead", "label": "담당계장 (이름 내선 / 휴대폰)", "sample": s["lead"], "profile": "lead_full"},
            {"key": "dept", "label": "담당부서", "sample": s["dept"], "profile": "dept_team"},
            {"key": "staff", "label": "담 당 자 (이름 내선 / 휴대폰)", "sample": s["staff"], "profile": "staff_full"},
        ],
        note="'보 고' 머리표 + 제목 상자 + □ 본문. 시장님 보고용. 보고 내용은 1장으로, 참고자료는 뒤에 쪽을 더 붙여 넣습니다",
        starter="□ (사업명)\n◦ 일자: \n▸\n◦ 장소: \n◦ 참석자: \n◦ 주요내용: \n▸")
    out["weekly_plan"] = weekly.register_plan(plan, os.path.join(FORMS, "weekly_plan"))
    out["weekly_cover"] = weekly.register_cover(plan, os.path.join(FORMS, "weekly_cover"))
    out["weekly_schedule"] = weekly.register_schedule(schedule, os.path.join(FORMS, "weekly_schedule"))
    return out


def _flat_texts(path, section_index=0):
    """flatten 한 뒤의 글자 목록 (등록 때와 같은 기준)"""
    from hwpgen.report import flatten_mixed
    pkg = Package.load(path)
    sec = pkg.xml(pkg.section_names()[section_index])
    flatten_mixed(sec)
    return [t.text for t in sec.iter(HP + "t") if t.text and t.text.strip()]


ISSUE_STARTER = ("□ 지난주 실적\n◦ \n▸일시/장소: \n▸참 석 자: \n▸주요내용: \n\n"
                 "□ 금 후 계 획\n◦ \n▸일시/장소: \n▸참 석 자: \n- \n▸주요내용: \n")


def build_extra(paths):
    """간부회의·행정사무감사·예산 설명자료 양식. paths: {meeting, meeting_ref, audit, budget, budget_ref}"""
    import re as _re
    out = {}
    F = FORMS
    if "meeting" in paths:
        ts = _flat_texts(paths["meeting"])
        m = next(_re.search(r"(\d{4}\. \d+\. \d+\.\([월화수목금토일]\))\s*(\d{1,2}:\d{2})", t) for t in ts
                 if _re.search(r"\d{4}\. \d+\. \d+\.\([월화수목금토일]\)\s*\d{1,2}:\d{2}", t))
        i = ts.index("현안·당면 업무보고") if "현안·당면 업무보고" in ts else 0
        cover_dept = ts[i + 1]
        out["meeting_cover"] = report.register_report(
            paths["meeting"], os.path.join(F, "meeting_cover"), "meeting_cover", "간부회의 표지", "간부회의", [
                {"key": "mdate", "label": "회의 날짜", "sample": m.group(1), "type": "date", "fmt": "long", "default": "target_monday"},
                {"key": "mtime", "label": "회의 시각", "sample": m.group(2), "default": "08:30"},
                {"key": "dept", "label": "부서(과)", "sample": cover_dept, "profile": "dept"},
            ], note="간부회의 표지: 날짜·시각 상자 + 현안·당면 업무보고 + 부서", slice_=(0, 8), allow_no_body=True)
        hdr = _flat_texts(paths["meeting"])
        no_t = next(t for t in hdr if _re.fullmatch(r"현안업무\(\d+\)", t.strip()))
        k = hdr.index(no_t)
        out["meeting_issue"] = report.register_report(
            paths["meeting"], os.path.join(F, "meeting_issue"), "meeting_issue", "간부회의 현안업무 보고", "간부회의", [
                {"key": "no", "label": "현안업무 번호", "sample": no_t, "repl": "현안업무({{no}})",
                 "default": _re.search(r"\d+", no_t).group(0)},
                {"key": "title", "label": "현안 제목", "sample": hdr[k + 1], "default": hdr[k + 1]},
                {"key": "dept", "label": "부서(과)", "sample": hdr[k + 2], "repl": "({{dept}})", "profile": "dept"},
            ], note="현안업무(번호) 제목 상자 + □ 금 후 계 획 + ◦ ▸ - 단계",
            starter=ISSUE_STARTER,
            slice_=(12, 22))
    if "meeting_ref" in paths:
        pr = paths["meeting_ref"]
        ts0 = _flat_texts_para(pr, 0, 6)
        k0 = next(i for i, t in enumerate(ts0) if _re.fullmatch(r"현안업무\(\d+\)", t.strip()))
        no_t0 = ts0[k0]
        info0 = next(t for t in ts0 if _re.match(r"\S*계장\s+\S+\s+\d+\s*/", t))
        team0, lead0 = info0.split(None, 1)
        staff0 = ts0[ts0.index(info0) + 1].replace("담당자", "", 1).strip()
    if "meeting_ref" in paths:
        pr = paths["meeting_ref"]
        ts0 = _flat_texts_para(pr, 0, 6)
        k0 = next(i for i, t in enumerate(ts0) if _re.fullmatch(r"현안업무\(\d+\)", t.strip()))
        no_t0 = ts0[k0]
        info0 = next(t for t in ts0 if _re.match(r"\S*계장\s+\S+\s+\d+\s*/", t))
        team0, lead0 = info0.split(None, 1)
        staff0 = ts0[ts0.index(info0) + 1].replace("담당자", "", 1).strip()
        out["meeting_card"] = report.register_report(
            pr, os.path.join(F, "meeting_card"), "meeting_card", "간부회의 참고자료 (사업 카드)", "간부회의", [
                {"key": "org", "label": "국 이름", "sample": ts0[0], "profile": "org"},
                {"key": "no", "label": "현안업무 번호", "sample": no_t0, "repl": "현안업무({{no}})",
                 "default": _re.search(r"\d+", no_t0).group(0)},
                {"key": "issue", "label": "현안 제목", "sample": ts0[k0 + 1], "default": ts0[k0 + 1]},
                {"key": "dept", "label": "부서(과)", "sample": ts0[k0 + 2], "repl": "({{dept}})", "profile": "dept"},
                {"key": "title", "label": "사업명", "sample": ts0[k0 + 3], "default": "(사업명)"},
                {"key": "team", "label": "계 이름", "sample": team0[:-1], "profile": "team", "blank": "      "},
                {"key": "lead", "label": "계장 (이름 내선 / 휴대폰)", "sample": lead0, "profile": "lead_full",
                 "blank": "이름 내선 / 휴대폰"},
                {"key": "staff", "label": "담당자 (이름 내선 / 휴대폰)", "sample": staff0, "profile": "staff_full",
                 "blank": "이름 내선 / 휴대폰"},
            ], note="현안 제목 + 사업명 + 계장·담당자 줄 + 개요 상자(사업기간·장소·사업비·운영횟수·사업내용·수행단체) + 프로그램 표 + □ 지난주 실적 + □ 금 후 계 획",
            starter=ISSUE_STARTER, slice_=(0, 48), drop=list(range(6, 37)), fix=_card_box_fix)
    if "audit_item" in paths:
        ai = paths["audit_item"]
        a0 = _flat_texts_para(ai, 0, 6)
        out["audit_item"] = report.register_report(
            ai, os.path.join(F, "audit_item"), "audit_item", "행정사무감사 과 개별 서식", "행정사무감사", [
                {"key": "item", "label": "항목 (예: 3. 예산편성 후 전액 미집행 내역)", "sample": a0[0], "default": "(번호. 항목명)"},
                {"key": "biz", "label": "사업 제목 (예: [2026] 3-1. 사업명)", "sample": a0[-1], "default": "(사업 제목)"},
            ], note="항목 제목 + 확인자·작성자 상자 + 사업 제목 상자 + □ 사업개요 · □ 추진현황 · □ 금후계획",
            starter="□ 사업개요\n◦ 사업기간 : \n◦ 사업내용 : \n◦ 소요예산 : \n\n□ 추진현황\n◦ \n\n□ 금후계획\n◦ ",
            slice_=(0, 26), body_from_text="□ 사업개요", fix=_audit_sign_fix)
    if "audit" in paths:
        ts = _flat_texts(paths["audit"])
        out["audit_cover"] = report.register_report(
            paths["audit"], os.path.join(F, "audit_cover"), "audit_cover", "행정사무감사 표지", "행정사무감사", [
                {"key": "adate", "label": "표지 날짜 (예: 6. 11.)", "sample": ts[0], "exact": True, "once": True},
                {"key": "session", "label": "회기 (예: 제282회)", "sample": next(t.strip() for t in ts if _re.fullmatch(r"제\d+회", t.strip()))},
                {"key": "org", "label": "국 이름", "sample": ts[-1], "profile": "org"},
            ], note="2026 행정사무감사자료 표지(회기·국 이름)", allow_no_body=True, slice_=(0, 14))
        s1 = _flat_texts_para(paths["audit"], 37, 48, 1)
        dept = next(t for t in s1 if t.strip().endswith("과") and len(t.strip()) <= 8)
        out["audit_common"] = report.register_report(
            paths["audit"], os.path.join(F, "audit_common"), "audit_common", "행정사무감사 과 공통사항", "행정사무감사", [
                {"key": "dept", "label": "과 이름", "sample": dept, "profile": "dept"},
            ], note="과 이름표 + 감사자료 과 공통사항 목록(1~)", allow_no_body=True, frame_only=True, section_index=1, slice_=(37, 48), fix=_audit_common_fix)
    if "press" in paths:
        hd = _flat_texts_para(paths["press"], 0, 1)
        tl = _flat_texts_para(paths["press"], 1, 4)
        dline = next(t for t in hd if _re.search(r"\d{4}\. \d+\. \d+\.\(", t))
        dm = _re.search(r"(\d{4}\. \d+\. \d+\.\([월화수목금토일]\))\s*(\S+)?", dline)
        i_dept = hd.index("부 서") + 1
        i_head = next(i for i, t in enumerate(hd) if t.startswith("과"))
        i_lead = next(i for i, t in enumerate(hd) if t.startswith("계") and "장" in t)
        i_staff = hd.index("주무관")
        out["press_release"] = report.register_report(
            paths["press"], os.path.join(F, "press_release"), "press_release", "보도자료", "보도자료", [
                {"key": "pdate", "label": "보도 날짜", "sample": dm.group(1), "type": "date", "fmt": "long", "default": "today"},
                {"key": "ptime", "label": "조간용/석간용", "sample": dm.group(2) or "조간용", "default": "조간용"},
                {"key": "embargo", "label": "보도 시점 안내 문구", "sample": next(t for t in hd if t.startswith("이 보도자료는")),
                 "default": ""},
                {"key": "dept", "label": "담당 부서(과)", "sample": hd[i_dept], "profile": "dept", "blank": "      "},
                {"key": "team", "label": "담당 계", "sample": hd[i_dept + 3] if False else next(t for t in hd if t.endswith("계") and t != hd[i_dept]),
                 "profile": "team", "blank": "      "},
                {"key": "head_name", "label": "과장 이름", "sample": hd[i_head + 1], "profile": "head_name", "blank": "      "},
                {"key": "head_tel", "label": "과장 전화", "sample": hd[i_head + 2], "profile": "head_tel", "blank": "859-    "},
                {"key": "lead_name", "label": "계장 이름", "sample": hd[i_lead + 1], "profile": "lead_name", "blank": "      "},
                {"key": "lead_tel", "label": "계장 전화", "sample": hd[i_lead + 2], "profile": "lead_tel", "blank": "859-    "},
                {"key": "staff_name", "label": "주무관 이름", "sample": hd[i_staff + 1], "profile": "staff_name", "blank": "      "},
                {"key": "staff_tel", "label": "주무관 전화", "sample": hd[i_staff + 2], "profile": "staff_tel", "blank": "859-    "},
                {"key": "photo_time", "label": "사진 제공 시각", "sample": "15:00이후", "default": "", "blank": "      "},
                {"key": "pages", "label": "쪽 수", "sample": "2쪽", "repl": "{{pages}}쪽", "default": "1", "blank": "1"},
                {"key": "title", "label": "보도 제목", "sample": tl[0]},
                {"key": "sub1", "label": "부제 1", "sample": tl[1], "blank": " "},
                {"key": "sub2", "label": "부제 2", "sample": tl[2], "blank": " "},
            ], note="보도자료 머리표(담당·과장·계장·주무관·사진·영상) + 제목·부제 + 본문(문단마다 한 줄)",
            starter="",
            body_from=7, plain_body=True)
    for key, fid, label in (("budget", "budget", "설명자료"), ("budget_ref", "budget_ref", "참고자료")):
        if key not in paths:
            continue
        ts = _flat_texts(paths[key])
        flat = " ".join(ts)
        m = _re.search(r"(\d{1,2}\. ?\.?\(?[월화수목금토일]\)? ?\d{1,2}:\d{2})", flat)
        dt = next((t for t in ts if _re.search(r"\d{1,2}:\d{2}", t)), None)
        first = ts[0]
        sess_full = next(t for t in ts if "익산시의회" in t)
        sess, _, rnd = sess_full.partition("\u3000")
        btitle = next(t for t in ts if t.startswith("―"))
        org = next(t for t in ts if t.strip() in ("문화교육국", "문화유산과"))
        # 날짜·시각 글자: '2026.\u3000 12. .(화) 10:00'
        dt_sample = _re.sub(r"^2026\.\u3000\s*", "", first)
        cover_fields = [
            {"key": "mdt", "label": "회의 날짜·시각 (예: 12. 9.(화) 10:00)", "sample": dt_sample},
            {"key": "session", "label": "회기 (제284회 익산시의회)", "sample": sess},
            {"key": "round", "label": "정례회 구분", "sample": rnd} if rnd else None,
            {"key": "btitle", "label": "예산 제목", "sample": btitle},
            {"key": "org", "label": "국·과 이름", "sample": org, "profile": "org" if key == "budget" else "dept"},
        ]
        cover_fields = [f for f in cover_fields if f]
        out[fid + "_cover"] = report.register_report(
            paths[key], os.path.join(F, fid + "_cover"), fid + "_cover",
            "예산 설명자료 표지" if key == "budget" else "참고자료(백데이터) 표지",
            "예산 설명자료" if key == "budget" else "참고자료(백데이터)",
            cover_fields, note="의회 회기·예산 제목·국(과) 표지", allow_no_body=True, slice_=(0, 12))
        if key == "budget_ref":
            out.update(_build_ref_pages(paths[key], F))
            continue
        # 사업 쪽: 두 번째 사업 쪽(정책명 줄 + 경비내역 표 + □ 본문) = 쪽 나눔 문단부터 다음 사업 제목 표 전까지
        doc = Package.load(paths[key]).section(0)
        pss = doc.findall(HP + "p")
        s = next(i for i, p in enumerate(pss) if p.get("pageBreak") == "1" and any(c.tag == HP + "tbl" for r_ in p.findall(HP + "run") for c in r_))
        e = next(i for i in range(s + 1, len(pss)) if any(c.tag == HP + "tbl" for r_ in pss[i].findall(HP + "run") for c in r_)
                 and _re.match(r"^\d+\s*\D", "".join(t.text or "" for t in pss[i].iter(HP + "t"))))
        ptxt = _flat_texts_para(paths[key], s, e)
        pol = next(t for t in ptxt if "정" in t and "책" in t and "단위사업" in t)
        mm = _re.search(r"정\s*책\s*명\s*:\s*(.+?)\s{2,}단위사업\s*:\s*(.+)$", pol)
        detail_line = next(t for t in ptxt if t.startswith("세부사업"))
        detail = _re.sub(r"^세부사업명?\s*:\s*", "", detail_line).strip()
        pageno = next(t for t in ptxt if t.strip().isdigit())
        out[fid + "_project"] = report.register_report(
            paths[key], os.path.join(F, fid + "_project"), fid + "_project", "사업별 %s (1사업 1쪽)" % label, "예산 설명자료",
            [
                {"key": "page", "label": "쪽 번호"},
                {"key": "policy", "label": "정책명", "sample": mm.group(1).strip()},
                {"key": "unit", "label": "단위사업", "sample": mm.group(2).strip()},
                {"key": "detail", "label": "세부사업명", "sample": detail},
            ], note="정책명·단위사업·세부사업명 + 사업개요 및 경비내역 표(머리만) + □ 사업개요/증감사유/관계법령/기대효과",
            starter="□ 사 업 개 요\n◦ 사업기간: \n◦ 사 업 비: \n◦ 사업내용: \n\n□ 증 감 사 유\n◦ \n▸\n\n□ 관 계 법 령\n◦ \n\n□ 기 대 효 과\n◦ ",
            fix=_title_cell_fix, slice_=(s, e), blank_tables={str(i): 1 for i in range(s, e) if i - s in (4,)} or {str(s + 4): 1})
    return out


def _photo_fix(section):
    """현황 사진 상자: 샘플의 사진은 빼고 칸 모양(사진 자리 + 설명 줄)만 남긴다"""
    for tbl in section.iter(HP + "tbl"):
        if len(list(tbl.iter(HP + "pic"))):
            for pic in list(tbl.iter(HP + "pic")):
                pic.getparent().remove(pic)
            for t in tbl.iter(HP + "t"):
                t.text = ""


def _build_ref_pages(path, F):
    """참고자료(백데이터): 설명자료 사업 쪽 뒤에 붙는 자료 — 세부사업 참고 쪽(그간 추진현황·향후 계획·현황 사진), 반환금 쪽"""
    import re as _re
    out = {}
    doc = Package.load(path).section(0)
    from hwpgen.report import flatten_mixed
    flatten_mixed(doc)
    pss = doc.findall(HP + "p")
    txt = lambda p: "".join(t.text or "" for t in p.iter(HP + "t")).strip()          # noqa: E731
    has_tbl = lambda p: any(c.tag == HP + "tbl" for r_ in p.findall(HP + "run") for c in r_)    # noqa: E731
    s = next(i for i in range(len(pss) - 1) if pss[i].get("pageBreak") == "1" and has_tbl(pss[i])
             and txt(pss[i + 1]).startswith("□ 사 업 개 요") and not txt(pss[i])[:1].isdigit())
    e = next(i for i in range(s + 1, len(pss)) if has_tbl(pss[i]) and _re.match(r"^\d+\s*\D", txt(pss[i])))
    ph = next((i for i in range(s + 1, e) if has_tbl(pss[i]) and any(True for _ in pss[i].iter(HP + "pic"))), e)
    ts = _flat_texts_para(path, s, s + 1)
    staff = _re.sub(r"[\[\]]", "", ts[1]).strip()
    out["budget_ref_detail"] = report.register_report(
        path, os.path.join(F, "budget_ref_detail"), "budget_ref_detail", "세부사업 참고 쪽 (설명자료 뒤)", "참고자료(백데이터)", [
            {"key": "title", "label": "내역사업명", "sample": ts[0].strip(), "default": "(내역사업명)"},
            {"key": "staff", "label": "담당자 (이름 내선)", "sample": staff, "profile": "staff_name_ext", "blank": "이름 내선"},
        ], note="내역사업명 [담당자] + □ 사 업 개 요 + □ 그 간 추 진 현 황 + □ 향 후 계 획 + □ 현 황 사 진(제목만)",
        starter="□ 사 업 개 요\n◦ 사업기간: \n◦ 사 업 비: \n◦ 사업위치: \n◦ 주요내용: \n▸\n\n"
                "□ 그 간  추 진 현 황\n◦ \n\n□ 향 후 계 획\n◦ \n◦ \n\n□ 현 황 사 진",
        slice_=(s, ph), body_from_text="□ 사 업 개 요", body_tail=0)
    u = next(i for i, p in enumerate(pss) if txt(p) == "(단위 : 원)")
    t0 = max(i for i in range(u) if has_tbl(pss[i]))
    out["budget_ref_return"] = report.register_report(
        path, os.path.join(F, "budget_ref_return"), "budget_ref_return", "보조금 반환금 쪽 (설명자료 뒤)", "참고자료(백데이터)", [
            {"key": "title", "label": "사업명 (예: 2025년 ○○사업)", "sample": txt(pss[t0]), "default": "(사업명)"},
        ], note="사업명 상자 + □ 사 업 개 요(사업기간·사업비·사업내용·반 납 액·반납사유) + (단위 : 원) 구분 표(머리만)",
        starter="□ 사 업 개 요\n◦ 사업기간: \n◦ 사 업 비: \n◦ 사업내용: \n◦ 반 납 액: \n◦ 반납사유: ",
        slice_=(t0, u + 2), body_from_text="□ 사 업 개 요", body_tail=2, blank_tables={str(u + 1): 2})
    return out


def _title_cell_fix(section):
    """사업 쪽 맨 위 제목 표: [쪽번호][빈칸][사업명] → {{page}} / {{detail}}"""
    tbl = next(section.iter(HP + "tbl"))
    tcs = tbl.findall(HP + "tr")[0].findall(HP + "tc")
    for i, tc in enumerate(tcs):
        ts_ = [t for t in tc.iter(HP + "t")]
        for t in ts_:
            t.text = ""
        if i == 0 and ts_:
            ts_[0].text = "{{page}}"
        if i == len(tcs) - 1 and ts_:
            ts_[0].text = " {{detail}}"


def _audit_sign_fix(section):
    """확인자·작성자 상자: 라벨(확인자 :, 작성자 :)만 남기고 직책·이름은 비운다"""
    from hwpgen.package import ptext
    tbl = next(t for t in section.iter(HP + "tbl") if "확인자" in ptext(t))
    for pic in list(section.iter(HP + "pic")):             # 서명 그림(이름이 들어 있음)은 뺀다
        pic.getparent().remove(pic)
    for t in tbl.iter(HP + "t"):
        x = (t.text or "").strip()
        if x not in ("확인자", "작성자", ":") and not (t.text or "").startswith("\U000f012b"):
            t.text = ""


def _audit_common_fix(section):
    """'감사자료 과 요구사항' 아래 과별 항목 이름(그 과 고유 내용)은 비우고 번호·점선 칸만 남긴다"""
    from hwpgen.package import ptext
    tbl = next(t for t in section.iter(HP + "tbl") if "과 요구사항" in ptext(t))
    trs = tbl.findall(HP + "tr")
    start = next(i for i, tr in enumerate(trs) if "과 요구사항" in ptext(tr))
    for tr in trs[start + 1:]:
        tcs = tr.findall(HP + "tc")
        if len(tcs) > 4:
            for t in tcs[4].iter(HP + "t"):
                t.text = ""


def _trim_rows(section, keep_data=9):
    """긴 표를 한 쪽에 들어가게: 머리 줄 + 본문 keep_data 줄 + 맨 끝 안내 줄만 남기고 본문 줄 높이를 같게 맞춘다"""
    tbl = next(section.iter(HP + "tbl"))
    trs = tbl.findall(HP + "tr")
    head, body, foot = trs[0], trs[1:-1], trs[-1]
    for tr in body[keep_data:]:
        tbl.remove(tr)
    body = body[:keep_data]
    h0 = int(body[0].find(HP + "tc").find(HP + "cellSz").get("height"))
    for tr in body:
        for tc in tr.findall(HP + "tc"):
            tc.find(HP + "cellSz").set("height", str(h0))
    kept = [head] + body + [foot]
    total = 0
    for i, tr in enumerate(kept):
        for tc in tr.findall(HP + "tc"):
            tc.find(HP + "cellAddr").set("rowAddr", str(i))
        total += int(tr.find(HP + "tc").find(HP + "cellSz").get("height"))
    tbl.set("rowCnt", str(len(kept)))
    tbl.find(HP + "sz").set("height", str(total))


def _written_fix(section):
    """서면질의서 한 건: 총괄표의 값 칸은 비우고 세부사업명 칸만 {{title}} 자리표시로"""
    from hwpgen.package import ptext
    tbl = next(t for t in section.iter(HP + "tbl") if "(단위: 백만원)" in ptext(t))
    trs = tbl.findall(HP + "tr")
    for tr in trs[2:]:
        for ci, tc in enumerate(tr.findall(HP + "tc")):
            ts_ = list(tc.iter(HP + "t"))
            for t in ts_:
                t.text = ""
            if ci == 0 and ts_:
                ts_[0].text = "{{title}}"


def _people_table_fix(section):
    """'담당' 표(기획처·부처·도·시 담당자 이름·전화): 구조만 남기고 이름·전화는 모두 비운다"""
    from hwpgen.package import ptext
    for tbl in section.iter(HP + "tbl"):
        first = ptext(tbl.find(HP + "tr").find(HP + "tc")).strip()
        if first == "담당" and int(tbl.get("rowCnt")) >= 4:
            for t in tbl.iter(HP + "t"):
                if (t.text or "").strip() != "담당":
                    t.text = ""


def build_queries(paths):
    """국회단계 사업 서면질의서·정책질의서. paths: {written, policy}"""
    out = {}
    F = FORMS
    ITEM_STARTER = ("□ 사업개요\nㅇ (사업내용) \nㅇ (사업위치) \nㅇ (사업기간) \nㅇ (총사업비) \nㅇ (주관/시행) \n\n"
                    "□ 필요성 및 기대효과\nㅇ \n- \nㅇ \n- \n\n□ 요구사항 : ")
    if "written" in paths:
        w = paths["written"]
        ts = _flat_texts_para(w, 13, 14)
        title, head = ts[1], ts[2]
        out["written_cover"] = report.register_report(
            w, os.path.join(F, "written_cover"), "written_cover", "서면질의서 표지", "국회단계 서면질의서", [
                {"key": "dept", "label": "부서(과)", "sample": _flat_texts_para(w, 5, 6)[0], "profile": "dept"},
            ], note="총괄본 · 국회단계 사업 서면질의서 · 부서", slice_=(0, 7), allow_no_body=True, frame_only=True)
        out["written_list"] = report.register_report(
            w, os.path.join(F, "written_list"), "written_list", "서면질의 목록", "국회단계 서면질의서", [],
            note="서면질의 목록(의원명·제출일자 + 회계/기금·세부사업명·예산 표 머리)", slice_=(7, 13), allow_no_body=True, frame_only=True,
            blank_tables={"12": 1}, fix=_trim_rows)
        out["written_item"] = report.register_report(
            w, os.path.join(F, "written_item"), "written_item", "서면질의서 (사업 1건)", "국회단계 서면질의서", [
                {"key": "title", "label": "세부사업명 (예: ○○○ 지원(신규))", "sample": title, "default": "(세부사업명)(신규)"},
                {"key": "committee", "label": "상임위원회", "sample": head, "default": "[상임위원회]",
                 "repl": "< {{committee}} / {{ministry}} 소관 / {{account}} >"},
                {"key": "ministry", "label": "소관 부처", "default": "[소관 부처]"},
                {"key": "account", "label": "회계 (예: 일반회계)", "default": "[회계]"},
            ], note="2027년도 예산안 서면질의 머리 + □ 총괄표(표) + □ 사업개요/필요성 및 기대효과/요구사항",
            starter=ITEM_STARTER, slice_=(13, 39), body_from_text="□ 사업개요", fix=_written_fix)
    if "policy" in paths:
        p = paths["policy"]
        out["policy_cover"] = report.register_report(
            p, os.path.join(F, "policy_cover"), "policy_cover", "정책질의서 표지", "국회단계 정책질의서", [],
            note="총괄본 · 국회단계 사업 정책질의서 · 익산시", slice_=(0, 9), allow_no_body=True, frame_only=True)
        out["policy_list"] = report.register_report(
            p, os.path.join(F, "policy_list"), "policy_list", "국회단계 활동사업 목록", "국회단계 정책질의서", [],
            note="익산시 국회단계 활동사업 목록(표 머리만, 연번~비고)", slice_=(9, 13), allow_no_body=True, frame_only=True,
            blank_tables={"9": 4})
        t13 = _flat_texts_para(p, 13, 14)
        out["policy_item"] = report.register_report(
            p, os.path.join(F, "policy_item"), "policy_item", "정책질의서 (사업 1건)", "국회단계 정책질의서", [
                {"key": "no", "label": "연번", "sample": "1.", "once": True, "repl": "{{no}}.", "default": "1"},
                {"key": "title", "label": "사업명", "sample": t13[1].split(".", 1)[1].strip(), "once": True,
                 "default": "(사업명)"},
                {"key": "kind", "label": "신규/계속", "sample": t13[2], "exact": True, "once": True, "default": "신규"},
            ], note="정책질의서 머리 + □ 질의 취지·◦ 설명 + <Q> 질의 상자",
            starter="□ [질의 취지: 사업의 목적을 한 줄로]\n◦ [정부 정책·추진 배경]\n\n◦ [익산의 현황·기반]\n\n◦ [문제점·추진 과제]\n\n"
                    "◦ [이에 따라 필요한 사항]\n\n| **<Q>** [질의 목적]을 위해 [사업명 · 요구 사업비 국비 ○○억원 반영]이 필요하다고 생각하는데, **이에 대한 견해**를 말씀해 주시기 바람 |",
            slice_=(13, 35), body_from_text="□")
        t35 = _flat_texts_para(p, 35, 36)
        out["policy_ref"] = report.register_report(
            p, os.path.join(F, "policy_ref"), "policy_ref", "참고1 조서 (사업 1건)", "국회단계 정책질의서", [
                {"key": "ref", "label": "참고 번호 (예: 참고1)", "sample": t35[0], "exact": True, "once": True, "default": "참고1"},
                {"key": "title", "label": "사업명", "sample": t35[1].replace("(조서)", "").strip(), "default": "(사업명)"},
                {"key": "request", "label": "건의 문구 (파란 상자)", "sample": _flat_texts_para(p, 66, 67)[0],
                 "default": "(사업명) ○○억원 반영 건의드림"},
            ], note="참고1 (조서) 머리 + 회계/기금명 표 + □ 사업개요/필요성/추진상황 및 계획 + 건의 상자 + 담당 표(이름 비움)",
            starter="□ 사업개요\nㅇ (사업내용) \nㅇ (사업위치) \nㅇ (사업기간) \nㅇ (총사업비) \nㅇ (주관/시행) \n\n"
                    "□ 필 요 성 \nㅇ \n\n□ 추진상황 및 계획\nㅇ ",
            slice_=(35, 68), body_from_text="□ 사업개요", body_tail=2, blank_tables={"37": 1}, fix=_people_table_fix)
    return out


def _card_box_fix(section):
    """사업 카드 개요 상자: ◦ 항목 이름과 프로그램 표의 틀(3갈래 一樂/二樂/三樂 · ①② · 연인원 줄)만 남기고 내용은 비운다"""
    from hwpgen import package as P2
    from hwpgen.package import ptext
    from hwpgen.report import dominant_cp
    box = next(t for t in section.iter(HP + "tbl") if "프로그램 내용" in ptext(t))
    cell = next(tc for tc in box.iter(HP + "tc") if "프로그램 내용" in ptext(tc) and len(list(tc.iter(HP + "tbl"))) > 0)
    sub = cell.find(HP + "subList")
    for tc in box.findall(HP + "tr")[0].findall(HP + "tc"):
        ts_ = [t for t in tc.iter(HP + "t")]
        if any("【" in (t.text or "") for t in ts_):          # 상자 제목 【…】 은 글자 조각이 여럿이라 첫 조각에 합쳐 넣는다
            for t in ts_:
                t.text = ""
            ts_[0].text = "【{{title}}】"
    for p in sub.findall(HP + "p"):
        txt = "".join(t.text or "" for r in p.findall(HP + "run") for t in r.findall(HP + "t")).strip()
        m = _re_mod.match(r"^(◦\s*[^:：]{1,10}?)\s*[:：]", txt)
        if m:
            cp = dominant_cp(p) or "0"
            P2.set_par_runs(p, [(cp, m.group(1) + " : ")])
    prog = next(t for t in sub.iter(HP + "tbl"))
    marks = {1: {0: "一樂.", 1: "①"}, 2: {0: "②"}, 3: {0: "二樂.", 1: "①"}, 4: {0: "②"}, 5: {0: "三樂.", 1: "①"}, 6: {0: "②"},
             7: {0: "연인원 : "}}
    for ri, tr in enumerate(prog.findall(HP + "tr")):
        if ri == 0:
            continue
        for ci, tc in enumerate(tr.findall(HP + "tc")):
            ts_ = list(tc.iter(HP + "t"))
            for t in ts_:
                t.text = ""
            mk = marks.get(ri, {}).get(ci)
            if mk and ts_:
                ts_[0].text = mk


import re as _re_mod


def _flat_texts_para(path, a, b, section_index=0):
    from hwpgen.report import flatten_mixed
    pkg = Package.load(path)
    sec = pkg.xml(pkg.section_names()[section_index])
    flatten_mixed(sec)
    ps = sec.findall(HP + "p")[a:b]
    return [t.text for p in ps for t in p.iter(HP + "t") if t.text and t.text.strip()]


# ------------------------------------------------------------------ 이월 조서 · 국가예산 신규사업 조서
def _set_ts(p, texts):
    """문단 안 글자 조각들을 순서대로 texts 로 바꾸고 남는 조각은 비운다"""
    ts = [t for r in p.findall(HP + "run") for t in r.findall(HP + "t")]       # 문단 바로 아래 글자(안내 상자 속 글자는 제외)
    for i, t in enumerate(ts):
        t.text = texts[i] if i < len(texts) else ""


def _carry_fix(section):
    """명시이월 사업현황 한 건: 틀과 항목 이름만 남기고 값은 비운다"""
    import re
    from hwpgen.package import ptext
    for p in section.findall(HP + "p"):
        tbls = list(p.iter(HP + "tbl"))
        ts = list(p.iter(HP + "t"))
        txt = "".join(t.text or "" for t in ts)
        if tbls:
            tbl = tbls[0]
            if "담당부서" in txt:                                     # 담당부서·전화·담당자·이월액 상자
                labs = ("담당부서", "행정전화", "담당자", "이월액")
                for t in tbl.iter(HP + "t"):
                    if (t.text or "").strip() not in labs:
                        t.text = ""
                    elif (t.text or "").strip() == "담당부서":
                        pass
            elif "사업명" in txt:                                     # 1 사업명 : ...
                tt = list(tbl.iter(HP + "t"))
                for i, t in enumerate(tt):
                    t.text = {0: "1", 1: " 사업명 : {{title}}"}.get(i, "")
            elif "구  분" in txt or "총사업비" in txt:               # 금액 표: 머리 글자만
                hdr = {"구  분", "총사업비", "예  산", "확보액", "지출", "원인행위액", "지출액", "잔  액", "이  월  액", "계",
                       "국비", "도비", "시비", "보조", "자체"}
                for t in tbl.iter(HP + "t"):
                    if (t.text or "").strip() not in hdr:
                        t.text = ""
            continue
        s = txt.strip()
        if re.match(r"^◦\s*(사 업 량|사업기간|사업내용)", s):
            lab = re.match(r"^◦\s*(사 업 량|사업기간|사업내용)", s).group(1)
            _set_ts(p, [" ◦ %s : " % lab])
        elif s.startswith("□ 이 월 액"):
            _set_ts(p, ["□ 이 월 액 :", " "])
        elif s.startswith("□ 예산편성시점"):
            _set_ts(p, ["□ 예산편성시점 : "])
        elif s.startswith("※"):
            pass                                                         # 안내 문구(※ 예산확보액 : …)는 양식 글자라 그대로
        elif s.startswith("◦"):
            _set_ts(p, ["  ◦"])
        elif s.startswith("-") or s.startswith("‣") or s.startswith("▸"):
            _set_ts(p, [])


def _nbud_clean(section):
    """국가예산 신규사업 조서 틀: 안내(빨간 글씨 지침)와 샘플 내용은 지우고 제목·항목 이름·표 머리만 남긴다"""
    import re
    HEAD = [("사업개요", "□ <사업개요>"), ("필요성", "□ <필요성>"), ("추진상황", "□ <추진상황 및 계획>"),
            ("연도별 투자계획", "□ 연도별 투자계획"), ("예산 요구액", "□ 예산 요구액 및 반영액(국비)"),
            ("행정절차", "□ 주요 행정절차 이행 상황"), ("사업추진 타당성", "□ 사업추진 타당성"),
            ("건립 필요성", "□ 익산시 건립 필요성"), ("사업 세부내역", "□ 사업 세부내역"), ("위치도", "□ 위치도"),
            ("기본구상", "□ 기본구상(안)")]
    labels = {"사업개요", "요구", "(억원)", "부처안", "담당부처", "담당", "기획처", "전북도", "익산시", "구분", "총사업비",
              "기투자액", "연도별 투자계획", "비고", "계", "국비", "도비", "시비", "투자 계획", "(단위: 억원)", "기확보액",
              "건의액", "반 영 내 역", "기 획 처", "정부안", "국회", "1차", "2차", "3차", "이후", "예비", "타당성조사", "사전",
              "보조사업", "적격성", "국립시설", "사전타당성", "국가과학기술심의", "지방재정", "투자", "국가기본", "계획",
              "부지확보", "실 명", "이용계획", "1층", "2층", "지하", "참고 1", "참고 2", "사업 세부 내용", "위치도 및 기본구상안"}
    for ctrl in list(section.iter(HP + "ctrl")):                 # 누름틀(빨간 안내 글자가 든 필드)은 안내째 뺀다
        if any(c.tag in (HP + "fieldBegin", HP + "fieldEnd") for c in ctrl) or any(True for _ in ctrl.iter(HP + "subList")):
            if not any(True for _ in ctrl.iter(HP + "tbl")):
                ctrl.getparent().remove(ctrl)
    for p in list(section.findall(HP + "p")):
        ts = [t for r in p.findall(HP + "run") for t in r.findall(HP + "t")]
        txt = "".join(t.text or "" for t in ts)
        s = txt.strip()
        tbls = list(p.iter(HP + "tbl"))
        if tbls:
            s = "".join(t.text or "" for t in p.iter(HP + "t")).strip()          # 표 문단은 표 안 글자까지 본다
            for pic in list(p.iter(HP + "pic")):
                pic.getparent().remove(pic)
            tb = tbls[0]
            if "사업개요" in s and "요구" in s:                        # 요구(억원)/부처안/담당부처 칸
                keep = {"요구", "(억원)", "부처안", "담당부처", "사업개요"}
            elif "<건의란>" in s or "건의드림" in s:
                keep = {"건의드림"}
            else:
                keep = labels
            for tb_ in tbls:
                for t in tb_.iter(HP + "t"):
                    x = (t.text or "").strip()
                    if x not in keep and not re.match(r"^’\d\d$|^~’\d\d$|^\d+%$", x):
                        t.text = ""
            if re.match(r"^\d\.\s", s):                                 # 맨 위 제목 상자: 1. 사업명(신규)  <부처명, 세부사업명>
                tt = list(tb.iter(HP + "t"))
                for i, t in enumerate(tt):
                    t.text = {0: " {{title}}", 2: "  <부처명, 세부사업명>"}.get(i, "")
            continue
        if s.startswith("□"):
            real = [(t.text or "") for t in ts]
            _set_ts(p, [real[0], (real[1] if len(real) > 1 else "")])          # '□ ' + 제목(필 요 성 …)만, 뒤 요약 글은 비움
        elif s.startswith("ㅇ") or s.startswith("O"):
            m = re.search(r"\((사업내용|사업위치|사업기간|총사업비|시행기관)\)", s)
            if m:
                _set_ts(p, [" ㅇ ", "(%s) " % m.group(1)])
            else:
                lab = {"정부 정책과의 부합성": 1, "익산시 필요성": 1}
                keep_lab = next((k for k in lab if k in s), None)
                _set_ts(p, [" ㅇ ", keep_lab or ""])
        elif s.startswith("▸"):
            _set_ts(p, ["  ▸"])
        elif s.startswith("※") or s.startswith("<") or (len(s) > 30 and not s.startswith("□")):
            _set_ts(p, [])


def build_more(paths):
    out = {}
    F = FORMS
    if "carry" in paths:
        out["carry_item"] = report.register_report(
            paths["carry"], os.path.join(F, "carry_item"), "carry_item", "명시이월 사업현황", "이월 조서", [
                {"key": "title", "label": "사업명", "sample": "{{title}}", "default": "(사업명)"},
            ], note="명시이월 사업현황 한 건: 담당부서 상자 + 사업명 + □ 사업개요 + □ 이 월 액(표) + □ 예산편성시점 + □ 그간 추진경과 + □ 이월사유",
            allow_no_body=True, frame_only=True, slice_=(0, 22), fix=_carry_fix)
    if "nbud" in paths:
        pth = paths["nbud"]
        out["nbud_main"] = report.register_report(
            pth, os.path.join(F, "nbud_main"), "nbud_main", "신규사업 첫장 (사업개요·필요성·추진상황)", "국가예산 신규사업", [
                {"key": "title", "label": "사업명 (예: 1. ○○○ 건립(신규))", "sample": "{{title}}", "default": "1. (사업명)(신규)"},
            ], note="사업명·부처 상자 + 사업개요(사업내용·위치·기간·총사업비·시행기관) + 필요성 + 추진상황 및 계획 + 건의란 + 담당란",
            allow_no_body=True, frame_only=True, slice_=(22, 58), drop=[23, 25, 27, 29, 31, 33, 35, 37, 40, 42, 44, 46, 48, 50, 52, 54, 56],
            fix=_nbud_clean)
        out["nbud_ref1"] = report.register_report(
            pth, os.path.join(F, "nbud_ref1"), "nbud_ref1", "신규사업 참고1 (연도별 투자계획·행정절차)", "국가예산 신규사업", [], note="연도별 투자계획 표 + 예산 요구액 및 반영액 표 + 행정절차 이행 표 + 사업추진 타당성",
            allow_no_body=True, frame_only=True, slice_=(58, 85), fix=_nbud_clean)
        out["nbud_ref2"] = report.register_report(
            pth, os.path.join(F, "nbud_ref2"), "nbud_ref2", "신규사업 참고2 (위치도·기본구상)", "국가예산 신규사업", [], note="위치도 칸 + 기본구상(안) 표",
            allow_no_body=True, frame_only=True, slice_=(86, 94), fix=_nbud_clean)
    return out


if __name__ == "__main__":
    pos = [a for a in sys.argv[1:] if "=" not in a]
    extra = dict(a.split("=", 1) for a in sys.argv[1:] if "=" in a)
    if len(pos) not in (0, 4) or (not pos and not extra):
        print(__doc__)
        sys.exit(1)
    res = build(*pos) if pos else {}
    if extra:
        res.update(build_extra({k: v for k, v in extra.items() if k not in ("written", "policy", "carry", "nbud")}))
        res.update(build_more({k: v for k, v in extra.items() if k in ("carry", "nbud")}))
        res.update(build_queries({k: v for k, v in extra.items() if k in ("written", "policy")}))
    for k, v in res.items():
        print(k, "→", {a: b for a, b in v.items() if a != "style"})
