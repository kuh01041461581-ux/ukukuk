# -*- coding: utf-8 -*-
"""선택한 글을 다듬는 도구(콤마·금액 한글·줄 정리·번호·글머리·날짜). 글자만 바꾸는 순수 함수라 한글 없이도 시험할 수 있다."""
import datetime
import re

_NUM = re.compile(r"\d[\d,]*(?:\.\d+)?")


def comma_add(text):
    def fix(m):
        s = m.group(0).replace(",", "")
        head, dot, tail = s.partition(".")
        if len(head) <= 3 or (len(head) > 1 and head[0] == "0"):
            return m.group(0)
        prev = text[m.start() - 1] if m.start() else ""
        nxt = text[m.end()] if m.end() < len(text) else ""
        if (prev and prev in "-/:") or (nxt and nxt in "-/:") or nxt == "년":          # 날짜·전화번호·연도는 그대로
            return m.group(0)
        if len(head) == 4 and 1900 <= int(head) <= 2100 and nxt != "원" and not dot:
            return m.group(0)
        return "{:,}".format(int(head)) + dot + tail
    return _NUM.sub(fix, text)


def comma_del(text):
    return re.sub(r"(?<=\d),(?=\d{3})", "", text)


_D = "영일이삼사오육칠팔구"
_U = ("", "십", "백", "천")
_B = ("", "만", "억", "조")


def _four(n):
    out = ""
    for i in range(3, -1, -1):
        d = (n // 10 ** i) % 10
        if d:
            out += ("" if (d == 1 and i > 0) else _D[d]) + _U[i]
    return out


def num_to_korean(n):
    """12345 → 일만이천삼백사십오 (십·백·천 앞의 '일'은 생략)"""
    if n == 0:
        return "영"
    parts, i = [], 0
    while n:
        n, r = divmod(n, 10000)
        if r:
            parts.append(("일" if (r == 1 and i > 0) else _four(r)) + _B[i])
        i += 1
    return "".join(reversed(parts))


def money_korean(text):
    """숫자(콤마 있어도 됨)를 한글 금액으로: 304,441,020원 → 삼억사백사십사만천이십원"""
    def fix(m):
        s = m.group(0).replace(",", "")
        if "." in s or len(s) > 16:
            return m.group(0)
        return num_to_korean(int(s))
    return _NUM.sub(fix, text)


def clean_spaces(text):
    out = []
    for ln in text.split("\n"):
        ln = re.sub(r"[ \t　]{2,}", " ", ln).strip()
        out.append(ln)
    return "\n".join(out)


def join_lines(text):
    """엔터 정리: 빈 줄로 나뉜 문단은 두고, 그 안의 줄바꿈은 없애 한 줄로 잇는다"""
    paras = re.split(r"\n\s*\n", text.replace("\r", ""))
    return "\n\n".join(re.sub(r"\s*\n\s*", " ", p).strip() for p in paras if p.strip())


def drop_blank_lines(text):
    return "\n".join(ln for ln in text.split("\n") if ln.strip())


def sort_lines(text, reverse=False):
    lines = [ln for ln in text.split("\n") if ln.strip()]
    return "\n".join(sorted(lines, reverse=reverse))


_CIRC = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳"
_GANA = "가나다라마바사아자차카타파하"
_BULLET = re.compile(r"^\s*(?:[□■◦○●ㅇ▸▶▷►‣\-·•*※ㆍ]|\d+[.)]|[①-⑳]|[가-하][.)])\s*")


def number_lines(text, style="1."):
    lines = [ln for ln in text.split("\n") if ln.strip()]
    out = []
    for i, ln in enumerate(lines):
        body = _BULLET.sub("", ln)
        if style == "①":
            mark = _CIRC[i] if i < len(_CIRC) else "(%d)" % (i + 1)
        elif style == "가.":
            mark = (_GANA[i] if i < len(_GANA) else str(i + 1)) + "."
        else:
            mark = "%d." % (i + 1)
        out.append("%s %s" % (mark, body))
    return "\n".join(out)


def strip_bullets(text):
    return "\n".join(_BULLET.sub("", ln) for ln in text.split("\n"))


_DATE = re.compile(r"(?<!\d)(\d{4})\s*[.\-/년]\s*(\d{1,2})\s*[.\-/월]\s*(\d{1,2})(?:\s*일)?\.?(?:\s*\([월화수목금토일]\))?")


def date_fmt(text):
    """2026-11-20 / 2026.11.20 / 2026년 11월 20일 → 2026. 11. 20.(금)"""
    def fix(m):
        try:
            d = datetime.date(int(m[1]), int(m[2]), int(m[3]))
        except ValueError:
            return m.group(0)
        return "%d. %d. %d.(%s)" % (d.year, d.month, d.day, "월화수목금토일"[d.weekday()])
    return _DATE.sub(fix, text)


def add_around(text, before="", after=""):
    return "\n".join((before + ln + after) if ln.strip() else ln for ln in text.split("\n"))


def replace_all(text, old, new):
    return text.replace(old, new) if old else text


# (이름, 설명, 함수) — 화면 단추 순서
TOOLS = (
    ("콤마 넣기", "숫자에 천 단위 콤마를 넣어요", comma_add),
    ("콤마 빼기", "숫자의 콤마를 없애요", comma_del),
    ("금액 한글", "숫자를 한글 금액(삼억…)으로 바꿔요", money_korean),
    ("날짜 맞추기", "날짜를 2026. 11. 20.(금) 꼴로 맞춰요", date_fmt),
    ("엔터 정리", "문단 안의 줄바꿈을 없애 한 줄로 이어요", join_lines),
    ("빈 줄 삭제", "빈 줄을 모두 없애요", drop_blank_lines),
    ("공백 정리", "겹친 띄어쓰기·앞뒤 공백을 정리해요", clean_spaces),
    ("글머리 삭제", "줄 앞 기호·번호를 지워요", strip_bullets),
    ("번호 1. 2.", "줄마다 번호를 붙여요", lambda t: number_lines(t, "1.")),
    ("번호 ① ②", "줄마다 원 번호를 붙여요", lambda t: number_lines(t, "①")),
    ("번호 가. 나.", "줄마다 가나다 번호를 붙여요", lambda t: number_lines(t, "가.")),
    ("가나다순", "줄을 가나다순으로 늘어놓아요", lambda t: sort_lines(t, False)),
    ("역순 정렬", "줄을 거꾸로 늘어놓아요", lambda t: sort_lines(t, True)),
)
