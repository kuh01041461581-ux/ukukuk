# -*- coding: utf-8 -*-
"""공문(PDF) 텍스트에서 일정 후보를 뽑는 모듈. (글자가 선택되는 PDF 전용, 스캔본 이미지는 불가)"""
import datetime as dt
import re

# 새 문단이 시작되는 표지 (1. / 가. / ○ / (1) / 제목 / 붙임 …)
_PARA_START = re.compile(
    r"^\s*(\d+[.)]\s|\d+\.\S|[가-하][.)]\s|[○ㅇ□■※•▪◦·\-ㆍ]\s?|\(\d+\)|[①-⑩]|"
    r"제목|수신|참조|붙임|경유|발신|시행|접수|문서번호|결재|협조자|기안자|관련)")
# 일정이 아닌 문단 (시행일·접수일·관련 문서 번호 등)
_SKIP = re.compile(r"^\s*(시행|접수|문서번호|결재|협조자|기안자|붙임|관련|우\s?\d|전화|주소|수신|참조|발신|경유)")
_MARK = re.compile(r"^\s*(\d+[.)]|[가-하][.)]|[○ㅇ□■※•▪◦·\-ㆍ]|\(\d+\)|[①-⑩])\s*")

_DATE = re.compile(
    r"(?<![\d.])(?:(?P<y>20\d{2})\s*[.\-/년]\s*|'(?P<y2>\d{2})\s*\.\s*)?"
    r"(?P<m>\d{1,2})\s*[.\-/월]\s*(?P<d>\d{1,2})\s*(?:일|\.)?")
_TIME_COLON = re.compile(r"(?<!\d)(\d{1,2})\s*:\s*(\d{2})(?!\d)")
_TIME_KO = re.compile(r"(오전|오후)?\s*(\d{1,2})\s*시\s*(?:(\d{1,2})\s*분)?")
_RANGE = re.compile(r"^\s*(?:\([^)]*\))?\s*(?:\d{1,2}\s*:\s*\d{2})?\s*[~∼～\-–]\s*")
_URGENT = re.compile(r"기한|마감|제출|까지|회신|신청|접수")


def extract_text(path):
    """PDF → 텍스트. pdfminer.six 우선, 없으면 pypdf."""
    try:
        from pdfminer.high_level import extract_text as pm_extract
        return pm_extract(path)
    except ImportError:
        from pypdf import PdfReader
        return "\n".join((p.extract_text() or "") for p in PdfReader(path).pages)


def paragraphs(text):
    """줄바꿈으로 끊긴 문장을 문단 단위로 다시 이어 붙인다."""
    out = []
    for raw in text.replace("\r", "").split("\n"):
        line = re.sub(r"\s+", " ", raw).strip()
        if not line:
            continue
        if not out or _PARA_START.match(line):
            out.append(line)
        else:
            out[-1] += " " + line
    return out


def find_title(paras):
    for p in paras:
        if p.startswith("제목"):
            return re.sub(r"^제목\s*", "", p).strip()
    return paras[0] if paras else ""


def _time_after(s):
    m = _TIME_COLON.search(s[:40])
    if m and int(m.group(1)) < 24:
        return f"{int(m.group(1)):02d}:{m.group(2)}"
    m = _TIME_KO.search(s[:40])
    if m:
        h = int(m.group(2))
        if m.group(1) == "오후" and h < 12:
            h += 12
        if m.group(1) == "오전" and h == 12:
            h = 0
        if h < 24:
            return f"{h:02d}:{int(m.group(3) or 0):02d}"
    return ""


def _mk_date(y, m, d):
    try:
        return dt.date(y, m, d)
    except ValueError:
        return None


def parse(text, today=None):
    """반환: (제목, [{"date": date, "time": "HH:MM"|"", "text": str, "pri": 0~3}, ...])"""
    today = today or dt.date.today()
    paras = paragraphs(text)
    title = find_title(paras)
    short = title if len(title) <= 18 else title[:17] + "…"

    # 문서 연도: 본문에서 처음 나오는 '연도 포함 날짜'의 연도, 없으면 올해
    year = today.year
    for p in paras:
        m = _DATE.search(p)
        if m and (m.group("y") or m.group("y2")):
            year = int(m.group("y") or "20" + m.group("y2"))
            break

    found, seen = [], set()
    for p in paras:
        if _SKIP.match(p) or p.startswith("제목"):
            continue
        body = _MARK.sub("", p, count=1)
        if _SKIP.match(body):
            continue
        pos = 0
        for m in _DATE.finditer(body):
            if m.start() < pos:
                continue
            y = int(m.group("y") or ("20" + m.group("y2") if m.group("y2") else year))
            d = _mk_date(y, int(m.group("m")), int(m.group("d")))
            if not d:
                continue
            rest = body[m.end():]
            when = _time_after(rest)
            r = _RANGE.match(rest)  # "10. 20. ~ 10. 24." 는 시작일 하나로
            if r:
                m2 = _DATE.match(rest, r.end())
                if m2:
                    pos = m.end() + m2.end()
            label = body if len(body) <= 44 else body[:43] + "…"
            item_text = f"[{short}] {label}" if short and short not in body else label
            key = (d, label)
            if key in seen:
                continue
            seen.add(key)
            found.append({"date": d, "time": when, "text": item_text,
                          "pri": 1 if _URGENT.search(body) else 2})
    found.sort(key=lambda x: (x["date"], x["time"] or "99:99"))
    return title, found
