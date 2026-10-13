# -*- coding: utf-8 -*-
"""한국 공휴일 (양력 고정 + 설날·추석·부처님오신날 + 대체공휴일 규칙). 음력 날짜는 2024~2030년분을 표로 둔다."""
import datetime as dt

FIXED = {(1, 1): "신정", (3, 1): "삼일절", (5, 5): "어린이날", (6, 6): "현충일",
         (8, 15): "광복절", (10, 3): "개천절", (10, 9): "한글날", (12, 25): "성탄절"}
SUBSTITUTABLE = {"삼일절", "어린이날", "광복절", "개천절", "한글날", "성탄절", "부처님오신날"}

# 해마다 음력으로 정해지는 날의 양력 날짜: (설날, 추석, 부처님오신날)
LUNAR = {
    2024: ((2, 10), (9, 17), (5, 15)), 2025: ((1, 29), (10, 6), (5, 5)),
    2026: ((2, 17), (9, 25), (5, 24)), 2027: ((2, 7), (9, 15), (5, 13)),
    2028: ((1, 27), (10, 3), (5, 2)), 2029: ((2, 13), (9, 22), (5, 20)),
    2030: ((2, 3), (9, 12), (5, 9)),
}
# 선거일·임시공휴일 등 (알려진 것만)
EXTRA = {dt.date(2024, 4, 10): "국회의원선거", dt.date(2024, 10, 1): "국군의 날(임시)",
         dt.date(2025, 1, 27): "임시공휴일", dt.date(2025, 6, 3): "대통령선거",
         dt.date(2026, 6, 3): "지방선거", dt.date(2028, 4, 12): "국회의원선거"}

_cache = {}
_override = {}      # 공공데이터 API 로 받은 연도별 공휴일 {year: {date: 이름}} — 있으면 내장 규칙보다 우선


def set_override(year, mapping):
    """mapping: {'YYYY-MM-DD': 이름}. 비어 있으면 무시(내장 규칙 사용)"""
    out = {}
    for k, v in (mapping or {}).items():
        try:
            out[dt.date.fromisoformat(k)] = v
        except ValueError:
            pass
    if out:
        _override[year] = out


def holidays(year):
    if year in _override:
        return _override[year]
    if year in _cache:
        return _cache[year]
    h, need = {}, []          # need: 대체공휴일이 필요한 묶음의 마지막 날

    def put(d, name):
        if d in h:            # 다른 공휴일과 겹침 → 대체공휴일 필요
            need.append(d)
        else:
            h[d] = name

    for (m, d), name in FIXED.items():
        dd = dt.date(year, m, d)
        put(dd, name)
        if name in SUBSTITUTABLE and dd.weekday() >= 5:
            need.append(dd)
    lunar = LUNAR.get(year)
    if lunar:
        for (m, d), name in zip(lunar[:2], ("설날", "추석")):
            c = dt.date(year, m, d)
            block = [c - dt.timedelta(days=1), c, c + dt.timedelta(days=1)]
            clash = any(b in h for b in block)
            for b in block:
                h.setdefault(b, name if b == c else {"설날": "설 연휴", "추석": "추석 연휴"}[name])
            if clash or any(b.weekday() == 6 for b in block):
                need.append(block[-1])
        b = dt.date(year, *lunar[2])
        put(b, "부처님오신날")
        if b.weekday() >= 5:
            need.append(b)
    for d, name in EXTRA.items():
        if d.year == year:
            h.setdefault(d, name)
    for d in sorted(need):    # 묶음 다음의 첫 평일(공휴일 아닌 날)
        n = d + dt.timedelta(days=1)
        while n.weekday() >= 5 or n in h:
            n += dt.timedelta(days=1)
        h[n] = "대체공휴일"
    _cache[year] = h
    return h


def holiday_name(d):
    return holidays(d.year).get(d)
