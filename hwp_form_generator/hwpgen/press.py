# -*- coding: utf-8 -*-
"""보도자료 초안: 대략 적은 글(제목·일시·장소·내용 …)을 보도자료에서 흔히 쓰는 문단 구성으로 정리한다.
 - 규칙 기반이라 적은 사실만 재배치한다(없는 사실·인용문은 만들어 내지 않음). 문장은 한글에서 다듬어 쓰도록 초안으로 넣는다."""
import re

_DATE = re.compile(r"(\d{4})\.\s*(\d{1,2})\.\s*(\d{1,2})\.?\s*(?:\(([월화수목금토일])\))?")
_TIME = re.compile(r"(\d{1,2}):(\d{2})")
_KEYS = {"일시": "when", "일자": "when", "날짜": "when", "기간": "when", "장소": "place", "위치": "place",
         "주최": "host", "주관": "host", "대상": "target", "참가대상": "target", "참여대상": "target", "대상자": "target",
         "내용": "content", "주요내용": "content", "문의": "contact", "담당": "contact", "예산": "budget", "사업비": "budget",
         "목적": "purpose", "취지": "purpose", "배경": "purpose", "기대효과": "effect", "효과": "effect",
         "신청": "apply", "접수": "apply", "신청방법": "apply", "참가신청": "apply", "방법": "apply"}
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
    d = {"title": "", "when": "", "place": "", "host": "", "target": "", "contact": "", "budget": "", "purpose": "", "effect": "",
         "apply": "", "content": []}
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


ORD = ("첫째", "둘째", "셋째", "넷째", "다섯째")


def draft(text, org="익산시", dept="", tel=""):
    """→ (제목, 본문 문단 목록). 보도자료의 흔한 흐름: 리드문 → 일시·장소·주관 → 추진 취지 → 주요 내용(첫째·둘째…) →
    사업비·참여·신청 → 기대효과 → 관계자 인용(빈칸 안내) → 문의. 적은 사실만 쓰고, 없는 정보는 [대괄호]로 비워 둔다."""
    d = parse_rough(text)
    title = d["title"] or "(제목)"
    event = any(k in title for k in _EVENT)
    kind = "행사" if event else "사업"
    topic = "이번 %s%s" % (kind, _josa(kind, "은", "는"))
    verb = "개최한다고 밝혔다" if event else "추진한다고 밝혔다"
    when, place = _when(d["when"]), d["place"]
    quoted = "‘%s’%s" % (title, _josa(title, "을", "를"))
    lead = "%s는 %s%s%s %s." % (org, ("%s " % when) if when else "", ("%s에서 " % place) if place else "", quoted, verb)
    paras = [re.sub(r"\s+", " ", lead).replace(" .", ".")]
    # 취지
    if d["purpose"]:
        pu = d["purpose"].rstrip(".")
        paras.append("%s %s" % (topic, pu + "." if (pu.endswith("다") or len(pu) > 28) else "%s%s 위해 마련됐다." % (pu, _josa(pu, "을", "를"))))
    else:
        paras.append("%s [추진 배경·취지를 한두 문장으로 적어 주세요]." % topic)
    # 일시·장소·주관
    where = ("%s " % when if when else "") + ("%s에서" % place if place else "")
    if where or d["host"]:
        s1 = "%s %s" % (topic, where.strip() + (" 열리며" if where else "")) if where else topic
        if d["host"]:
            s1 += ("%s%s 주관한다." % (", " if where else " ", d["host"] + _josa(d["host"], "이", "가"))) if where else " %s%s 주관한다." % (d["host"], _josa(d["host"], "이", "가"))
        else:
            s1 = (s1[:-2] + "린다.") if s1.endswith("열리며") else s1 + "."
        paras.append(s1)
    # 주요 내용
    content = [c.rstrip(".") for c in d["content"]]
    if len(content) == 1:
        c = content[0]
        paras.append(c + "." if (len(c) > 18 or c.endswith("다")) else "주요 내용은 %s%s다." % (c, _josa(c, "이", "")))
    elif content:
        parts = ["%s, %s" % (ORD[i], c) for i, c in enumerate(content[:5])]
        paras.append("%s %s 등으로 구성된다." % (topic, ", ".join(parts)))
        if len(content) > 5:
            paras.append("이 밖에 %s 등도 마련된다." % ", ".join(content[5:]))
    if d["budget"]:
        paras.append("사업비는 %s이다." % d["budget"].rstrip("."))
    if d["target"]:
        tg = d["target"].rstrip(".")
        paras.append(("%s 참여할 수 있다." % tg) if tg.endswith("누구나") else "참여 대상은 %s%s다." % (tg, _josa(tg, "이", "")))
    if d["apply"]:
        ap = d["apply"].rstrip(".")
        paras.append("참여 신청은 %s." % (ap + "으로 하면 된다" if not ap.endswith("다") else ap))
    # 기대효과
    if d["effect"]:
        ef = d["effect"].rstrip(".")
        paras.append("시는 %s" % (ef + "." if ef.endswith("다") else "%s로 이어질 것으로 기대하고 있다." % ef))
    else:
        who = "‘%s’%s" % (title, _josa(title, "을", "를")) if len(title) <= 20 else "이번 %s%s" % (kind, _josa(kind, "을", "를"))
        tg2 = d["target"].rstrip(".") if d["target"] and not d["target"].endswith("누구나") else "시민"
        paras.append("시는 %s 통해 %s의 참여와 소통을 넓히고 지역에 활력을 더하는 계기가 될 것으로 기대하고 있다." % (who, tg2))
    paras.append("%s 관계자는 “[관계자 인용문을 적어 주세요]”라고 말했다." % org)
    tail = d["contact"] or ((dept + (" (%s)" % tel if tel else "")) if dept else "")
    paras.append("한편, 자세한 사항은 %s로 문의하면 된다." % (tail or "담당 부서"))
    return title, paras
