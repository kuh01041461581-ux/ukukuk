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
        fields, note="제목 상자 + 담당자 줄 + 행사개요 + 식순·시간표(표) + 국장님 하실 일",
        starter="□ 행사개요\n◦ 사 업 명: \n◦ 회의주제: \n◦ 일시: \n◦ 장소: \n◦ 사업내용: \n◦ 대행기관: \n\n"
                "□ 식순 및 시간표\n| 등록 및 개회 |\n| 00:00~00:00(00') | **참석자 등록** |\n"
                "| 00:00~00:00(00') | **개 회 사** | |\n| 00:00~00:00(00') | **축    사** | |\n"
                "| 00:00~00:00(00') | **기념촬영** | |\n| 00:00~00:00(00') | **폐회사** |\n\n"
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


def _flat_texts(path, section_index=0):
    """flatten 한 뒤의 글자 목록 (등록 때와 같은 기준)"""
    from hwpgen.report import flatten_mixed
    pkg = Package.load(path)
    sec = pkg.xml(pkg.section_names()[section_index])
    flatten_mixed(sec)
    return [t.text for t in sec.iter(HP + "t") if t.text and t.text.strip()]


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
                {"key": "mdate", "label": "회의 날짜", "sample": m.group(1), "type": "date", "fmt": "long", "default": "today"},
                {"key": "mtime", "label": "회의 시각", "sample": m.group(2), "default": "08:30"},
                {"key": "dept", "label": "부서(과)", "sample": cover_dept, "profile": "dept"},
            ], note="간부회의 표지: 날짜·시각 상자 + 현안·당면 업무보고 + 부서", slice_=(0, 8), allow_no_body=True)
        hdr = _flat_texts(paths["meeting"])
        no_t = next(t for t in hdr if _re.fullmatch(r"현안업무\(\d+\)", t.strip()))
        k = hdr.index(no_t)
        out["meeting_issue"] = report.register_report(
            paths["meeting"], os.path.join(F, "meeting_issue"), "meeting_issue", "현안업무 보고 (1쪽)", "간부회의", [
                {"key": "no", "label": "현안업무 번호", "sample": no_t, "repl": "현안업무({{no}})", "default": "1"},
                {"key": "title", "label": "현안 제목", "sample": hdr[k + 1]},
                {"key": "dept", "label": "부서(과)", "sample": hdr[k + 2], "repl": "({{dept}})", "profile": "dept"},
            ], note="현안업무(번호) 제목 상자 + □ 금 후 계 획 + ◦ ▸ - 단계",
            starter="□ 금 후 계 획\n◦ \n▸일시/장소: \n▸참 석 자: \n- \n▸주요내용: ", slice_=(12, 22))
    if "meeting_ref" in paths:
        ts = _flat_texts_para(paths["meeting_ref"], 34, 36)         # 제목 상자(34) + 계장·담당자 줄(35)
        info = next(t for t in ts if _re.match(r"\S*계장\s+\S+\s+\d+\s*/", t))
        card_title = ts[ts.index(info) - 1]
        team_title, lead = info.split(None, 1)
        staff_t = ts[ts.index(info) + 1]
        staff = staff_t.replace("담당자", "", 1).strip()
        out["meeting_card"] = report.register_report(
            paths["meeting_ref"], os.path.join(F, "meeting_card"), "meeting_card", "참고자료 사업 카드", "간부회의", [
                {"key": "title", "label": "사업명", "sample": card_title},
                {"key": "team_title", "label": "계 (예: 문화도시계장)", "sample": team_title, "profile": "team_title"},
                {"key": "lead", "label": "계장 (이름 내선 / 휴대폰)", "sample": lead, "profile": "lead_full"},
                {"key": "staff", "label": "담당자 (이름 내선 / 휴대폰)", "sample": staff, "profile": "staff_full"},
            ], note="사업명 상자 + 계장·담당자 줄 + □ 지난주 실적 / □ 금 후 계 획",
            starter="□ 지난주 실적\n◦ \n▸기간/장소: \n▸주요내용: \n\n□ 금 후 계 획\n◦ \n▸일시/장소: \n▸주요내용: ",
            slice_=(34, 48), drop=[36])
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
            paths["audit"], os.path.join(F, "audit_common"), "audit_common", "과 공통사항 목록", "행정사무감사", [
                {"key": "dept", "label": "과 이름", "sample": dept, "profile": "dept"},
            ], note="과 이름표 + 감사자료 과 공통사항 목록(1~)", allow_no_body=True, section_index=1, slice_=(37, 48), fix=_audit_common_fix)
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
            paths[key], os.path.join(F, fid + "_cover"), fid + "_cover", "예산 %s 표지" % label, "예산 설명자료",
            cover_fields, note="의회 회기·예산 제목·국(과) 표지", allow_no_body=True, slice_=(0, 12))
        # 사업 쪽: 두 번째 사업 쪽(정책명 줄 + 경비내역 표 + □ 본문) = 쪽 나눔 문단부터 다음 사업 제목 표 전까지
        doc = Package.load(paths[key]).section(0)
        pss = doc.findall(HP + "p")
        s = next(i for i, p in enumerate(pss) if p.get("pageBreak") == "1" and any(c.tag == HP + "tbl" for r_ in p.findall(HP + "run") for c in r_))
        e = next(i for i in range(s + 1, len(pss)) if any(c.tag == HP + "tbl" for r_ in pss[i].findall(HP + "run") for c in r_)
                 and _re.match(r"^\d+\S", "".join(t.text or "" for t in pss[i].iter(HP + "t"))))
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
            w, os.path.join(F, "written_cover"), "written_cover", "서면질의서 표지", "국회단계 질의서", [
                {"key": "dept", "label": "부서(과)", "sample": _flat_texts_para(w, 5, 6)[0], "profile": "dept"},
            ], note="총괄본 · 국회단계 사업 서면질의서 · 부서", slice_=(0, 7), allow_no_body=True, frame_only=True)
        out["written_list"] = report.register_report(
            w, os.path.join(F, "written_list"), "written_list", "서면질의 목록", "국회단계 질의서", [],
            note="서면질의 목록(의원명·제출일자 + 회계/기금·세부사업명·예산 표 머리)", slice_=(7, 13), allow_no_body=True, frame_only=True,
            blank_tables={"12": 1})
        out["written_item"] = report.register_report(
            w, os.path.join(F, "written_item"), "written_item", "서면질의서 (사업 1건)", "국회단계 질의서", [
                {"key": "title", "label": "세부사업명 (예: ○○○ 지원(신규))", "sample": title},
                {"key": "committee", "label": "상임위원회", "sample": head,
                 "repl": "< {{committee}} / {{ministry}} 소관 / {{account}} >"},
                {"key": "ministry", "label": "소관 부처"},
                {"key": "account", "label": "회계 (예: 일반회계)"},
            ], note="2027년도 예산안 서면질의 머리 + □ 총괄표(표) + □ 사업개요/필요성 및 기대효과/요구사항",
            starter=ITEM_STARTER, slice_=(13, 39), body_from_text="□ 사업개요", fix=_written_fix)
    if "policy" in paths:
        p = paths["policy"]
        out["policy_cover"] = report.register_report(
            p, os.path.join(F, "policy_cover"), "policy_cover", "정책질의서 표지", "국회단계 질의서", [],
            note="총괄본 · 국회단계 사업 정책질의서 · 익산시", slice_=(0, 9), allow_no_body=True, frame_only=True)
        out["policy_list"] = report.register_report(
            p, os.path.join(F, "policy_list"), "policy_list", "국회단계 활동사업 목록", "국회단계 질의서", [],
            note="익산시 국회단계 활동사업 목록(표 머리만, 연번~비고)", slice_=(9, 13), allow_no_body=True, frame_only=True,
            blank_tables={"9": 4})
        t13 = _flat_texts_para(p, 13, 14)
        out["policy_item"] = report.register_report(
            p, os.path.join(F, "policy_item"), "policy_item", "정책질의서 (사업 1건)", "국회단계 질의서", [
                {"key": "no", "label": "연번", "sample": "1.", "once": True, "repl": "{{no}}.", "default": "1"},
                {"key": "title", "label": "사업명", "sample": t13[1].split(".", 1)[1].strip(), "once": True},
                {"key": "kind", "label": "신규/계속", "sample": t13[2], "exact": True, "once": True, "default": "신규"},
            ], note="정책질의서 머리 + □ 질의 취지·◦ 설명 + <Q> 질의 상자",
            starter="□ \n◦ \n\n◦ \n\n| **<Q>** (질의 내용) |", slice_=(13, 35), body_from_text="□")
        t35 = _flat_texts_para(p, 35, 36)
        out["policy_ref"] = report.register_report(
            p, os.path.join(F, "policy_ref"), "policy_ref", "참고1 조서 (사업 1건)", "국회단계 질의서", [
                {"key": "ref", "label": "참고 번호 (예: 참고1)", "sample": t35[0], "exact": True, "once": True, "default": "참고1"},
                {"key": "title", "label": "사업명", "sample": t35[1].replace("(조서)", "").strip()},
                {"key": "request", "label": "건의 문구 (파란 상자)", "sample": _flat_texts_para(p, 66, 67)[0]},
            ], note="참고1 (조서) 머리 + 회계/기금명 표 + □ 사업개요/필요성/추진상황 및 계획 + 건의 상자 + 담당 표(이름 비움)",
            starter="□ 사업개요\nㅇ (사업내용) \nㅇ (사업위치) \nㅇ (사업기간) \nㅇ (총사업비) \nㅇ (주관/시행) \n\n"
                    "□ 필 요 성 \nㅇ \n\n□ 추진상황 및 계획\nㅇ ",
            slice_=(35, 68), body_from_text="□ 사업개요", body_tail=2, blank_tables={"37": 1}, fix=_people_table_fix)
    return out


def _flat_texts_para(path, a, b, section_index=0):
    from hwpgen.report import flatten_mixed
    pkg = Package.load(path)
    sec = pkg.xml(pkg.section_names()[section_index])
    flatten_mixed(sec)
    ps = sec.findall(HP + "p")[a:b]
    return [t.text for p in ps for t in p.iter(HP + "t") if t.text and t.text.strip()]


if __name__ == "__main__":
    pos = [a for a in sys.argv[1:] if "=" not in a]
    extra = dict(a.split("=", 1) for a in sys.argv[1:] if "=" in a)
    if len(pos) not in (0, 4) or (not pos and not extra):
        print(__doc__)
        sys.exit(1)
    res = build(*pos) if pos else {}
    if extra:
        res.update(build_extra({k: v for k, v in extra.items() if k not in ("written", "policy")}))
        res.update(build_queries({k: v for k, v in extra.items() if k in ("written", "policy")}))
    for k, v in res.items():
        print(k, "→", {a: b for a, b in v.items() if a != "style"})
