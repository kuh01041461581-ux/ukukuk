# -*- coding: utf-8 -*-
"""공문(PDF) 텍스트에서 일정 후보를 뽑는 모듈. (글자가 선택되는 PDF 전용, 스캔본 이미지는 불가)"""
import calendar
import datetime as dt
import re
from collections import Counter

_MARKERS = "○ㅇ□■※•▪◦·ㆍ❍◾◇▶►➢●∙⇒▷◆\\-–—"
# 새 문단이 시작되는 표지 (1. / 가. / ❍ / - / (1) / 제목 / 붙임 …)
_PARA_START = re.compile(
    r"^\s*(\d+[.)]\s|[가-하][.)]\s|[" + _MARKERS + r"]\s?|\(\d+\)|[①-⑩]|<|"
    r"제목|수신|참조|붙임|경유|발신|시행|접수|문서번호|결재|협조자|기안자|관련|등록|공개)")
# 일정이 아닌 문단 (시행일·접수일·관련 문서 번호·결재란 등)
_SKIP = re.compile(r"^\s*(시행|접수|문서번호|결재|협조자|기안자|붙임|관련|우\s?\d|전화|주소|수신|참조|발신|"
                   r"경유|등록|공개|전결|주무관|계\s?장|과\s?장|국\s?장)")
_MARK = re.compile(r"^\s*(\d+[.)]|[가-하][.)]|[" + _MARKERS + r"]|\(\d+\)|[①-⑩])\s*")
_PAGE_NO = re.compile(r"^\s*[-–—]\s*\d+\s*[-–—]\s*$")

_QUOTE = "'‘’´`"
_DATE = re.compile(
    r"(?P<y>20\d{2})\s*[.\-/년]\s*(?P<m>\d{1,2})\s*[.\-/월]\s*(?P<d>\d{1,2})(?!\d)\s*(?:일|\.)?"
    r"|(?:[" + _QUOTE + r"]\s*|(?<![\d.\-/]))(?P<y2>\d{2})\s*\.\s*(?P<m2>\d{1,2})\s*\.\s*(?P<d2>\d{1,2})(?!\d)\s*(?:일|\.)?"
    r"|(?<![\d.\-/])(?P<m3>\d{1,2})\s*[.월]\s*(?P<d3>\d{1,2})(?!\d)"
    r"(?:\s*일|\s*\.(?!\d)|(?=\s*\([월화수목금토일]\)))")
_RANGE_END = re.compile(r"(\d{1,2})\s*[.월]\s*(\d{1,2})(?!\d)\s*(?:일|\.)?")
_MRANGE = re.compile(r"(?<![\d.])(\d{1,2})\s*월\s*[~∼～]\s*(\d{1,2})\s*월(?!\s*\d)")
_TIME_COLON = re.compile(r"(?<!\d)(\d{1,2})\s*:\s*(\d{2})(?!\d)")
_TIME_KO = re.compile(r"(오전|오후)?\s*(\d{1,2})\s*시\s*(?:(\d{1,2})\s*분)?")
_RANGE = re.compile(r"^\s*(?:\([^)]*\))?\s*(?:\d{1,2}\s*:\s*\d{2})?\s*[~∼～\-–]\s*")
# 마감이 아니라 '그 기간 내내' 일정인 행사 (감사·의회·훈련 등). 그 밖의 "~까지" 기간은 마감일에만 표시한다.
_PERIOD = re.compile(r"감사|의회|회기|훈련|연수|교육|워크숍|워크샵|파견|수련|캠프")
_URGENT = re.compile(r"기한|마감|제출|까지|회신|신청|접수")
_ASOF = re.compile(r"^\s*\)?\s*(기준|현재)")   # "(2026. 4. 8. 기준)" 같은 기준일은 일정이 아님


def extract_text(path):
    """PDF → 텍스트. pdfminer.six 우선, 없으면 pypdf."""
    try:
        from pdfminer.high_level import extract_text as pm_extract
        return pm_extract(path)
    except ImportError:
        from pypdf import PdfReader
        return "\n".join((p.extract_text() or "") for p in PdfReader(path).pages)


def _clean(raw):
    return re.sub(r"\s+", " ", raw).strip()


def paragraphs(text):
    """줄바꿈으로 끊긴 문장을 문단 단위로 다시 이어 붙인다."""
    out = []
    for raw in text.replace("\r", "").split("\n"):
        line = _clean(raw)
        if not line or _PAGE_NO.match(line):
            continue
        if not out or _PARA_START.match(line):
            out.append(line)
        else:
            out[-1] += " " + line
    return out


def find_title(text, paras):
    """제목 줄 → (결재문서처럼 제목 줄이 없으면) 쪽마다 반복되는 문서 제목 → 첫 긴 줄"""
    for p in paras:
        if p.startswith("제목"):
            return re.sub(r"^제목\s*", "", p).strip()
    lines = [_clean(x) for x in text.split("\n")]
    lines = [x for x in lines if len(x) >= 8 and re.search(r"[가-힣]", x) and not _PAGE_NO.match(x)
             and not _PARA_START.match(x)]
    cnt = Counter(lines)
    for x in lines:  # 처음 나온 순서대로
        if cnt[x] >= 2:
            return x
    return lines[0] if lines else (paras[0] if paras else "")


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


def _date_of(m, year):
    """_DATE 매치 → date (연도가 없으면 year)"""
    if m.group("y"):
        return _mk_date(int(m.group("y")), int(m.group("m")), int(m.group("d")))
    if m.group("y2"):
        quoted = m.group(0).lstrip()[:1] in _QUOTE
        if not quoted and not 24 <= int(m.group("y2")) <= 39:      # 따옴표가 없으면 24~39 만 연도로 본다
            return None
        return _mk_date(2000 + int(m.group("y2")), int(m.group("m2")), int(m.group("d2")))
    return _mk_date(year, int(m.group("m3")), int(m.group("d3")))


def _label(body, start, end):
    """날짜 주변만 잘라 일정 이름으로 쓴다"""
    a = max(0, start - 36)
    if a > 0:
        sp = body.find(" ", a)
        a = sp + 1 if 0 <= sp < start else a
    b = min(len(body), end + 14)
    text = body[a:b].strip()
    return ("…" if a > 0 else "") + text + ("…" if b < len(body) else "")


_STRIP_TIME = [_TIME_COLON, re.compile(r"(오전|오후)\s*\d{1,2}\s*시(?:\s*\d{1,2}\s*분)?"), re.compile(r"\d{1,2}\s*시\s*\d{0,2}\s*분?")]
_WEEKDAY = re.compile(r"\(\s*[월화수목금토일]\s*\)")
_TAIL = re.compile(r"(?:하여|해|하시기)?\s*(?:주시기\s*)?(?:바랍니다|바람|부탁드립니다|요망|협조\s*바랍니다)\s*\.?\s*$")
_LEADWORD = re.compile(r"^\s*(?:및|또는|그리고|이후|까지|부터|에서|에는|에|행정사항|추진일정|향후일정|주요일정)\s+")


def _tidy(t):
    t = _WEEKDAY.sub(" ", t)
    for rx in _STRIP_TIME:
        t = rx.sub(" ", t)
    t = re.sub(r"(?:까지|부터|이내|전까지)(?=\s|$|[,.;:)])", " ", t)
    t = re.sub(r"[‘’'`´\"“”]", "", t)
    t = re.sub(r"\(\s*[,.~∼～\-:·\s]*\)", " ", t)                 # 비어 버린 괄호
    t = _TAIL.sub("", t.strip())
    t = re.sub(r"(?:기한|마감일?)\s*[:：]\s*(?=\S)", " ", t)           # "제출 기한: 제출" 처럼 겹치는 말
    t = re.sub(r"(?<!\S)(\S{2,})(\s+\1)+(?!\S)", r"\1", t)
    t = re.sub(r"\s+", " ", t).strip(" \t:;,.~∼～-–—·ㆍ")
    m = re.fullmatch(r"\(([^()]*)\)", t)                           # 전체가 괄호 하나면 벗긴다
    if m:
        t = m.group(1).strip()
    t = _LEADWORD.sub("", t).strip(" :;,.~-")
    if len(t) > 70:
        cut = t.rfind(" ", 0, 68)
        t = t[:cut if cut > 30 else 68].rstrip(" ,.;:") + "…"
    return t


def _meaningful(t):
    return len(re.sub(r"[^0-9A-Za-z가-힣]", "", t)) >= 2


def _core_text(body, spans, k):
    """k번째 날짜 주변에서 날짜·시간을 빼고 남는 핵심 내용"""
    s0, e0 = spans[k][0], spans[k][1]
    prev_end = spans[k - 1][1] if k > 0 else 0
    next_start = spans[k + 1][0] if k + 1 < len(spans) else len(body)
    before, after = _tidy(body[prev_end:s0]), _tidy(body[e0:next_start])
    if len(spans) == 1 or (k == 0 and not _meaningful(after)):
        core = _tidy(body[:s0] + " " + body[e0:]) if len(spans) == 1 else before
    elif _meaningful(before):
        core = before
    else:
        core = after
    if not _meaningful(core):
        core = after if _meaningful(after) else before
    return core


def parse(text, today=None):
    """반환: (제목, [{"date": date, "end": date|None, "time": "HH:MM"|"", "text": str, "pri": 0~3}, ...])"""
    today = today or dt.date.today()
    paras = paragraphs(text)
    title = find_title(text, paras)
    short = re.sub(r"^20\d{2}\s*(년도|년|학년도)?\s*", "", title).strip() or title
    short = short if len(short) <= 16 else short[:15] + "…"

    # 문서 연도: 본문에서 처음 나오는 '연도 포함 날짜'의 연도, 없으면 올해
    year = today.year
    for p in paras:
        m = _DATE.search(p)
        if m and (m.group("y") or m.group("y2")):
            d0 = _date_of(m, year)
            year = d0.year if d0 else year
            break

    found, seen = [], set()
    for p in paras:
        if _SKIP.match(p) or p.startswith("제목"):
            continue
        body = _MARK.sub("", p, count=1)
        if _SKIP.match(body):
            continue
        # 1) 문단 안의 날짜/기간 위치를 모두 찾는다
        spans, pos = [], 0
        for m in _DATE.finditer(body):
            if m.start() < pos:
                continue
            d = _date_of(m, year)
            if not d or _ASOF.match(body[m.end():m.end() + 8]):
                continue
            rest = body[m.end():]
            when = _time_after(rest)
            end, stop = None, m.end()
            r = _RANGE.match(rest)  # "10. 20. ~ 10. 24." 는 기간 일정 하나로
            if r:
                m2 = _DATE.match(body, m.end() + r.end())
                e2 = _date_of(m2, d.year) if m2 else None
                if m2 is None:                                   # "~ 10. 16" 처럼 끝에 점이 없는 종료일
                    m3 = _RANGE_END.match(body, m.end() + r.end())
                    if m3:
                        e2 = _mk_date(d.year, int(m3.group(1)), int(m3.group(2)))
                        m2 = m3
                if m2 and e2 and e2 > d:
                    end, stop = e2, m2.end()
            pos = stop
            spans.append((m.start(), stop, d, end, when))
        for m in _MRANGE.finditer(body):  # "4월~9월" 같은 월 단위 기간
            if any(a0 < m.end() and m.start() < b0 for a0, b0, *_ in spans):
                continue
            m1, m2 = int(m.group(1)), int(m.group(2))
            if 1 <= m1 <= 12 and 1 <= m2 <= 12 and m1 <= m2:
                spans.append((m.start(), m.end(), dt.date(year, m1, 1),
                              dt.date(year, m2, calendar.monthrange(year, m2)[1]), ""))
        spans.sort(key=lambda x: x[0])
        # 2) 날짜마다 '핵심 내용'만 뽑는다 (날짜·시간은 달력에 기록되니 글에서 뺀다)
        for k, (s0, e0, d, end, when) in enumerate(spans):
            core = _core_text(body, spans, k)
            if len(re.sub(r"[^0-9A-Za-z가-힣]", "", core)) < 2:
                continue
            key = (d, end, core)
            if key in seen:
                continue
            seen.add(key)
            ctx = body[max(0, s0 - 30):e0 + 14]
            found.append({"date": d, "end": end, "time": when, "text": core, "doc": title,
                          "period": bool(end) and bool(_PERIOD.search(body[max(0, s0 - 40):e0 + 20])),
                          "pri": 1 if _URGENT.search(ctx) else 2})
    found.sort(key=lambda x: (x["date"], x["time"] or "99:99"))
    return title, found
