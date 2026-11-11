# -*- coding: utf-8 -*-
"""보도자료 초안: 대략 적은 글(제목·일시·장소·내용 …)을 보도자료에서 흔히 쓰는 문단 구성으로 정리한다.
 - 규칙 기반이라 적은 사실만 재배치한다(없는 사실·인용문은 만들어 내지 않음). 문장은 한글에서 다듬어 쓰도록 초안으로 넣는다."""
import re

_DATE = re.compile(r"(\d{4})\.\s*(\d{1,2})\.\s*(\d{1,2})\.?\s*(?:\(([월화수목금토일])\))?")
_TIME = re.compile(r"(\d{1,2}):(\d{2})")
_KEYS = {"일시": "when", "일자": "when", "날짜": "when", "기간": "when", "장소": "place", "위치": "place",
         "주최": "host", "주관": "host", "대상": "target", "참가대상": "target", "참여대상": "target", "대상자": "target",
         "내용": "content", "주요내용": "content", "문의": "contact", "담당": "contact", "예산": "budget", "사업비": "budget"}
_EVENT = ("행사", "축제", "대회", "공연", "회의", "포럼", "설명회", "박람회", "전시", "세미나", "발표회", "기념식", "개막", "페스티벌", "교육", "음악회", "콘서트", "문화제", "투어", "탐방", "워크숍", "강연", "강좌", "체험", "시상식", "개최", "한마당")


def _josa(word, a, b):
    """받침 있으면 a, 없으면 b (을/를 등)"""
    for ch in reversed(word.strip()):
        if "가" <= ch <= "힣":
            return a if (ord(ch) - 0xAC00) % 28 else b
        if ch.isalnum():
            return b
    return b


def parse_rough(text):
    d = {"title": "", "when": "", "place": "", "host": "", "target": "", "contact": "", "budget": "", "content": []}
    for raw in (text or "").replace("\r", "").split("\n"):
        s = raw.strip()
        if not s:
            continue
        body = re.sub(r"^[\s○◦ㅇ▸\-·ㆍ□■※]+", "", s)
        m = re.match(r"^([^:：]{1,8}?)\s*[:：]\s*(.+)$", body)
        key = _KEYS.get(re.sub(r"\s", "", m.group(1))) if m else None
        if key and key != "content":
            if not d[key]:
                d[key] = m.group(2).strip()
        elif key == "content":
            d["content"].append(m.group(2).strip())
        elif not d["title"] and not re.match(r"^[○◦ㅇ▸\-·ㆍ]", s) and len(body) <= 60:
            d["title"] = body
        else:
            d["content"].append(body)
    return d


def _when(w):
    m = _DATE.search(w or "")
    if not m:
        return (w or "").strip()
    out = "%d월 %d일" % (int(m[2]), int(m[3]))
    if m[4]:
        out += "(%s)" % m[4]
    t = _TIME.search(w[m.end():])
    if t:
        h = int(t[1])
        out += " %s %d시%s" % ("오전" if h < 12 else "오후", h if h <= 12 else h - 12, "" if t[2] == "00" else " %s분" % t[2])
    return out


def draft(text, org="익산시", dept="", tel=""):
    """→ (제목, 본문 문단 목록). 본문은 보도자료 흐름: 리드문 → 개요 → 세부 내용 → 참여·문의 안내"""
    d = parse_rough(text)
    title = d["title"] or "(제목)"
    event = any(k in title for k in _EVENT)
    kind = "행사" if event else "사업"
    verb = "개최한다고 밝혔다" if event else "추진한다고 밝혔다"
    when, place = _when(d["when"]), d["place"]
    where = ("%s%s " % (place, "에서")) if place else ""
    quoted = "‘%s’%s" % (title, _josa(title, "을", "를"))
    lead = "%s는 %s%s%s %s." % (org, ("%s " % when) if when else "", where, quoted, verb)
    lead = re.sub(r"\s+", " ", lead).replace(" .", ".")
    paras = [lead]
    content = d["content"]
    if content:
        if len(content) == 1:
            c = content[0].rstrip(".")
            paras.append(c + "." if (len(c) > 18 or c.endswith("다")) else "주요 내용은 %s%s다." % (c, _josa(c, "이", "")))
        else:
            paras.append("주요 내용은 %s 등이다." % ", ".join(c.rstrip(".") for c in content))
    if d["host"]:
        paras.append("이번 %s%s %s%s 주관·추진한다." % (kind, _josa(kind, "은", "는"), d["host"], _josa(d["host"], "이", "가")))
    if d["budget"]:
        paras.append("사업비는 %s이다." % d["budget"].rstrip("."))
    if d["target"]:
        tg = d["target"].rstrip(".")
        paras.append(("%s 참여할 수 있다." % tg) if tg.endswith("누구나") else "참여 대상은 %s%s다." % (tg, _josa(tg, "이", "")) )
    tail = d["contact"] or ((dept + (" (%s)" % tel if tel else "")) if dept else "")
    paras.append("한편, 자세한 사항은 %s로 문의하면 된다." % (tail or "담당 부서"))
    return title, paras
