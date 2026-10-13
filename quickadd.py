# -*- coding: utf-8 -*-
"""한 줄 빠른 입력: '내일 오후 3시 도시재생 회의 !중요' → 날짜·시간·중요도·반복을 알아서 뽑는다.
폰 앱(mobile/lib/quickadd.dart)과 같은 규칙이며, 같은 시험 문장(tests/quickadd_cases.json)으로 맞춰 본다."""
import calendar
import datetime as dt
import re

WD = {"월": 0, "화": 1, "수": 2, "목": 3, "금": 4, "토": 5, "일": 6}
PRI_WORDS = {"중요": 1, "긴급": 1, "보통": 2, "낮음": 3}
REPEAT_WORDS = [("평일마다", "wd"), ("매 평일", "wd"), ("매일", "d"), ("매주", "w"), ("매월", "m"), ("매년", "y")]


def _mk(y, m, d):
    try:
        return dt.date(y, m, d)
    except ValueError:
        return None


def _next_weekday(today, wd, week_shift=None):
    """week_shift None: 오늘 포함 가장 가까운 그 요일 / 0: 이번 주 / 1: 다음 주 / 2: 다다음 주 (월요일 시작 주)"""
    if week_shift is None:
        return today + dt.timedelta(days=(wd - today.weekday()) % 7)
    monday = today - dt.timedelta(days=today.weekday())
    return monday + dt.timedelta(days=7 * week_shift + wd)


def _day_of_month(today, day, month_shift=None):
    if month_shift is None:      # 오늘 이후 가장 가까운 그 날짜
        for add in (0, 1, 2):
            y, m = today.year, today.month + add
            y += (m - 1) // 12
            m = (m - 1) % 12 + 1
            d = _mk(y, m, day)
            if d and d >= today:
                return d
        return None
    y, m = today.year, today.month + month_shift
    y += (m - 1) // 12
    m = (m - 1) % 12 + 1
    return _mk(y, m, day)


def parse(text, today=None):
    """반환: {"text": 남은 내용, "date": date|None, "time": "HH:MM"|None, "pri": 1~3|None, "repeat": 코드|None}"""
    today = today or dt.date.today()
    s = " " + text.strip() + " "
    out = {"date": None, "time": None, "pri": None, "repeat": None}

    def cut(rx, flags=0):
        nonlocal s
        m = re.search(rx, s, flags)
        if m:
            s = s[:m.start()] + " " + s[m.end():]
        return m

    # 중요도: !중요 !긴급 !낮음 !보통 / !!
    m = cut(r"(?<=\s)!(중요|긴급|낮음|보통)(?=\s)")
    if m:
        out["pri"] = PRI_WORDS[m.group(1)]
    elif cut(r"(?<=\s)!{2,3}(?=\s)"):
        out["pri"] = 1

    # 반복
    for word, code in REPEAT_WORDS:
        if cut(re.escape(word) + r"(?=\s|[가-힣]|$)"):
            out["repeat"] = code
            break

    d = None
    # 날짜: 연-월-일
    m = cut(r"(20\d{2})\s*[.\-/년]\s*(\d{1,2})\s*[.\-/월]\s*(\d{1,2})\s*일?(?!\d)")
    if m:
        d = _mk(int(m.group(1)), int(m.group(2)), int(m.group(3)))
    if d is None:
        m = cut(r"(?<!\d)(\d{1,2})\s*월\s*(\d{1,2})\s*일?(?![가-힣\d])")
        if m:
            d = _mk(today.year, int(m.group(1)), int(m.group(2)))
            if d and d < today and (today - d).days > 180:
                d = _mk(today.year + 1, int(m.group(1)), int(m.group(2)))
    if d is None:
        m = cut(r"(?<![\d:.])(\d{1,2})\s*[/.]\s*(\d{1,2})(?![\d:.])")
        if m and 1 <= int(m.group(1)) <= 12 and 1 <= int(m.group(2)) <= 31:
            d = _mk(today.year, int(m.group(1)), int(m.group(2)))
            if d and d < today and (today - d).days > 180:
                d = _mk(today.year + 1, int(m.group(1)), int(m.group(2)))
    if d is None:
        m = cut(r"(?<![가-힣])(내일모레|모레|글피|내일|오늘)")
        if m:
            w = m.group(1)
            d = today + dt.timedelta(days={"오늘": 0, "내일": 1, "모레": 2, "내일모레": 2, "글피": 3}[w])
    if d is None:
        m = cut(r"(\d{1,3})\s*(일|주)\s*(뒤|후)")
        if m:
            d = today + dt.timedelta(days=int(m.group(1)) * (7 if m.group(2) == "주" else 1))
    if d is None:
        m = cut(r"(다다음\s*주|다음\s*주|담주|이번\s*주)\s*(월|화|수|목|금|토|일)\s*(?:요일|(?![가-힣]))")
        if m:
            shift = 2 if m.group(1).startswith("다다음") else 1 if (m.group(1).startswith("다음") or m.group(1) == "담주") else 0
            d = _next_weekday(today, WD[m.group(2)], shift)
    if d is None:
        m = cut(r"(?<![가-힣])(월|화|수|목|금|토|일)요일")
        if m:
            d = _next_weekday(today, WD[m.group(1)])
    if d is None:
        m = cut(r"(다음\s*주|담주)(?![가-힣])")
        if m:
            d = today + dt.timedelta(days=7)
    if d is None:
        m = cut(r"(다음\s*달|이번\s*달)\s*(\d{1,2})\s*일(?![가-힣])")
        if m:
            d = _day_of_month(today, int(m.group(2)), 1 if m.group(1).startswith("다음") else 0)
    if d is None:
        m = cut(r"(월말|이번\s*달\s*말|말일)(?![가-힣])")
        if m:
            d = dt.date(today.year, today.month, calendar.monthrange(today.year, today.month)[1])
    if d is None:
        m = cut(r"(?<![\d가-힣])(\d{1,2})\s*일(?![가-힣\d])")
        if m:
            d = _day_of_month(today, int(m.group(1)))
    out["date"] = d

    # 시간
    m = cut(r"(?<!\d)(\d{1,2}):(\d{2})(?!\d)")
    if m and int(m.group(1)) < 24 and int(m.group(2)) < 60:
        out["time"] = f"{int(m.group(1)):02d}:{m.group(2)}"
    elif cut(r"정오(?![가-힣])"):
        out["time"] = "12:00"
    else:
        m = cut(r"(오전|오후|아침|점심|저녁|밤|새벽)?\s*(\d{1,2})\s*시\s*(?:(\d{1,2})\s*분|(반))?")
        if m:
            h = int(m.group(2))
            pre = m.group(1) or ""
            if pre in ("오후", "저녁", "밤") and h < 12:
                h += 12
            elif pre in ("오전", "아침", "새벽") and h == 12:
                h = 0
            elif not pre and 1 <= h <= 6:      # '3시 회의' 는 보통 오후
                h += 12
            if h < 24:
                mi = 30 if m.group(4) else int(m.group(3) or 0)
                out["time"] = f"{h:02d}:{mi:02d}"

    s = re.sub(r"\s+", " ", s).strip()
    s = re.sub(r"^(에|까지|부터|은|는|에는|에서)\s+", "", s).strip()
    out["text"] = s or text.strip()
    if out["repeat"] and out["date"] is None:
        out["date"] = today
    return out


def describe(r, today=None):
    """미리보기 문구: '10/14(수) 15:00 · 중요 · 매주'"""
    today = today or dt.date.today()
    parts = []
    d = r["date"]
    if d:
        wd = "월화수목금토일"[d.weekday()]
        tag = {0: "오늘", 1: "내일", 2: "모레"}.get((d - today).days)
        parts.append(f"{d.month}/{d.day}({wd})" + (f" {tag}" if tag else ""))
    if r["time"]:
        parts.append(r["time"])
    if r["pri"]:
        parts.append({1: "중요", 2: "보통", 3: "낮음"}[r["pri"]])
    if r["repeat"]:
        parts.append({"d": "매일", "wd": "평일마다", "w": "매주", "m": "매월", "y": "매년"}[r["repeat"]])
    return " · ".join(parts)
