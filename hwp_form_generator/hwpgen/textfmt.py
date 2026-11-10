# -*- coding: utf-8 -*-
"""공문서 글 모양 규칙 — 항목 이름 맞춤, 날짜·요일, 개요 입력 해석."""
import datetime
import re

WEEKDAYS = "월화수목금토일"
BULLETS = "◦○ㅇ∘•ㆍ·"
HEAD_MARKS = "□■▣◆▶"
SUB_MARKS = "▸►▹‣"          # 둘째 단계 글머리
DASH_MARKS = "-－–ㅡ"        # 셋째 단계 글머리


def weekday_ko(d):
    return WEEKDAYS[d.weekday()]


def fmt_date_long(d):
    """2026. 11. 9.(월)"""
    return "%d. %d. %d.(%s)" % (d.year, d.month, d.day, weekday_ko(d))


def fmt_date_short(d):
    """11. 9.(월)"""
    return "%d. %d.(%s)" % (d.month, d.day, weekday_ko(d))


def fmt_date_dot(d):
    """2026. 11. 9."""
    return "%d. %d. %d." % (d.year, d.month, d.day)


def fmt_range_short(a, b):
    """26. 11. 9. ~ 11. 15."""
    return "%02d. %d. %d. ~ %d. %d." % (a.year % 100, a.month, a.day, b.month, b.day)


def week_monday(d):
    return d - datetime.timedelta(days=d.weekday())


def parse_date(s, today=None):
    """'2026-11-09', '2026.11.9', '11.9', '261109' 같은 입력을 날짜로."""
    s = (s or "").strip()
    today = today or datetime.date.today()
    m = re.fullmatch(r"(\d{4})[-./\s]+(\d{1,2})[-./\s]+(\d{1,2})\.?(?:\(.\))?", s)
    if m:
        return datetime.date(int(m[1]), int(m[2]), int(m[3]))
    m = re.fullmatch(r"(\d{2})(\d{2})(\d{2})", s)
    if m:
        return datetime.date(2000 + int(m[1]), int(m[2]), int(m[3]))
    m = re.fullmatch(r"(\d{1,2})[-./\s]+(\d{1,2})\.?(?:\(.\))?", s)
    if m:
        return datetime.date(today.year, int(m[1]), int(m[2]))
    raise ValueError("날짜를 읽을 수 없어요: %r" % s)


def pad_label(label):
    """항목 이름 맞춤. 2글자 '일시' → '일    시', 3글자 '사업명' → '사 업 명', 4글자 이상은 그대로."""
    lab = re.sub(r"\s+", "", label or "")
    if len(lab) == 2:
        return lab[0] + "    " + lab[1]
    if len(lab) == 3:
        return " ".join(lab)
    return lab


KV_RE = re.compile(r"^\s*[%s]?\s*(?:\(([^()]{1,12})\)|([^\s:：()][^:：()]{0,11}?))\s*[:：]?\s*(.*)$" % re.escape(BULLETS))


def parse_outline(text):
    """입력창의 개요 글을 줄 단위 블록으로 바꾼다.

    □ 제목              → ('h1', '제목')
    ◦ 일시: 2026...     → ('kv', '일시', '2026...')     (◦ 는 생략 가능, '이름: 값' 모양이면 항목)
    ◦ 그냥 글           → ('item1', '그냥 글')
    - 하위 글           → ('item2', '하위 글')
    | 칸 | 칸 |         → ('table', [[칸,...], ...])  (연속한 | 줄이 표 하나)
    빈 줄               → ('blank',)
    """
    blocks = []
    table = []
    for raw in (text or "").splitlines():
        line = raw.rstrip()
        s = line.strip()
        if s.startswith("|"):
            cells = [c.strip() for c in s.strip("|").split("|")]
            if all(re.fullmatch(r":?-{2,}:?", c) for c in cells if c):   # 마크다운 구분선은 무시
                continue
            table.append(cells)
            continue
        if table:
            blocks.append(("table", table))
            table = []
        if not s:
            blocks.append(("blank",))
            continue
        if re.fullmatch(r"-{3,}|\[쪽\s*나눔\]", s):
            blocks.append(("pagebreak",))
            continue
        if s[0] in HEAD_MARKS:
            blocks.append(("h1", s[1:].strip()))
            continue
        if s.startswith("#"):
            blocks.append(("h1", s.lstrip("#").strip()))
            continue
        if s[0] in SUB_MARKS:                                   # ▸ 둘째 단계 ("▸ 일시: 값" 이면 이름 맞춤)
            body = s[1:].strip()
            m = re.match(r"^([^\s:：()][^:：]{0,11}?)\s*[:：]\s*(.*)$", body)
            if m and len(re.sub(r"\s", "", m.group(1))) <= 8:
                blocks.append(("kv2", m.group(1), m.group(2)))
            else:
                blocks.append(("item2", body))
            continue
        if re.match(r"^[-－–ㅡ*](\s+|$)", s) or (raw[:2].isspace() and re.match(r"^[-－–]\s*", s)):
            blocks.append(("item3", re.sub(r"^[-－–ㅡ*]\s*", "", s)))
            continue
        body = s
        if s[0] in BULLETS:
            body = s[1:].strip()
        m = re.match(r"^\(([^()]{1,12})\)\s*(.*)$", body)           # (일시) 값
        if m:
            blocks.append(("kv", m.group(1), m.group(2)))
            continue
        m = re.match(r"^([^\s:：()][^:：]{0,11}?)\s*[:：]\s*(.*)$", body)  # 일시: 값
        if m and len(re.sub(r"\s", "", m.group(1))) <= 8:
            blocks.append(("kv", m.group(1), m.group(2)))
            continue
        blocks.append(("item1", body))
    if table:
        blocks.append(("table", table))
    return blocks


# 보고서 본문 기본 틀: 사업개요(사업비·사업내용 …) → 지난 실적 → 향후 계획 → 참고자료
DEFAULT_STARTER = ("□ 사업개요\n◦ 사 업 명: \n◦ 사업기간: \n◦ 사업장소: \n◦ 사 업 비: \n◦ 사업내용: \n\n"
                   "□ 지난 실적\n◦ \n\n□ 향후 계획\n◦ \n\n□ 참고자료\n◦ ")


# ---------------------------------------------------------------- 다른 서식으로 쓴 글 → 개요 글
_H1 = "□■ㅁ▣"
_B1 = "○◦ㅇ❍●∘o"
_B2 = "▸‣▪·ㆍ▷►▶◆"
_B3 = "-–—"


def normalize_outline(text):
    """이미 다른 서식으로 쓴 글(한글에서 읽어 온 글자)을 □ ◦ ▸ - 개요 글로 바꾼다.
    기호가 있으면 그 단계로, 없으면 들여쓴 줄은 ▸ 아니면 ◦. '1. 제목' 은 □. 탭으로 나뉜 줄은 표 줄(| a | b |)."""
    out = []
    for raw in (text or "").replace("\r", "").split("\n"):
        line = raw.rstrip()
        s = line.strip()
        if not s:
            if out and out[-1] != "":
                out.append("")
            continue
        if "\t" in s and len([c for c in s.split("\t") if c.strip()]) >= 2:
            out.append("| " + " | ".join(c.strip() for c in s.split("\t")) + " |")
            continue
        indent = len(line) - len(line.lstrip(" \u3000"))
        c = s[0]
        rest = s[1:].strip()
        if c in _H1:
            out.append("□ " + rest)
        elif c in _B2 and c != "o":
            out.append("▸ " + rest)
        elif c in _B1 and not (c == "o" and rest[:1].isalpha() and not s[1:2].isspace()):
            out.append("◦ " + rest)
        elif c in _B3 and s[1:2] in (" ", "") or (c in _B3 and len(s) > 1):
            out.append("- " + rest)
        elif c == "※":
            out.append("▸ " + s)
        elif re.match(r"^\d+\.\s", s) and indent == 0:
            out.append("□ " + re.sub(r"^\d+\.\s*", "", s))
        else:
            out.append(("▸ " if indent >= 4 else "◦ ") + s)
    while out and out[-1] == "":
        out.pop()
    return "\n".join(out)
