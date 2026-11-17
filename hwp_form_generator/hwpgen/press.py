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

_TYPES = (
    ("open", ("개관", "개장", "준공", "개원", "오픈", "개소", "준공식")),
    ("recruit", ("모집", "공모", "접수", "신청")),
    ("support", ("지원", "보조", "장려", "바우처", "지급", "감면")),
    ("meeting", ("회의", "간담회", "설명회", "포럼", "토론", "세미나", "보고회", "협약")),
    ("edu", ("교육", "강좌", "강연", "워크숍", "체험", "학교", "아카데미", "프로그램", "캠프")),
    ("event", _EVENT),
)

_PHR = {          # 유형별 문구: (동사, 취지 문장, 기대 문장, 인용문 앞부분, 인용문 끝부분)
    "event": ("개최한다고 밝혔다", "시민들에게 다채로운 볼거리와 즐길 거리를 제공하고 지역에 활력을 불어넣기 위해 마련됐다",
              "시민들이 함께 즐기며 지역의 매력을 새롭게 느끼는 계기가 될 것으로 기대하고 있다",
              "많은 시민이 찾아 즐겁고 뜻깊은 시간을 보내시길 바란다", "시민 여러분의 많은 관심과 참여를 당부드린다"),
    "recruit": ("실시한다고 밝혔다", "더 많은 시민에게 참여 기회를 넓히고 시민이 체감할 수 있는 성과를 거두기 위해 마련됐다",
                "더 많은 시민이 혜택을 누릴 수 있는 기회가 될 것으로 기대하고 있다",
                "관심 있는 시민들이 놓치지 않고 신청하시길 바란다", "빠짐없이 안내해 참여 기회를 넓혀 가겠다"),
    "support": ("추진한다고 밝혔다", "시민의 부담을 덜고 실질적인 도움이 닿도록 하기 위해 추진된다",
                "지원이 필요한 시민들에게 실질적인 도움이 될 것으로 기대하고 있다",
                "꼭 필요한 분들께 도움이 닿도록 세심하게 살피겠다", "지원 대상이 한 분도 빠지지 않도록 꼼꼼히 안내하겠다"),
    "meeting": ("개최한다고 밝혔다", "현안을 공유하고 다양한 의견을 듣고 정책에 반영하기 위해 마련됐다",
                "충분한 의견 수렴을 바탕으로 정책의 완성도를 높일 것으로 기대하고 있다",
                "현장의 목소리를 정책에 담아내겠다", "논의된 의견은 꼼꼼히 살펴 반영하겠다"),
    "edu": ("운영한다고 밝혔다", "시민들이 일상에서 배움과 체험의 즐거움을 누릴 수 있도록 하기 위해 마련됐다",
            "참여자들이 새로운 지식과 경험을 쌓는 기회가 될 것으로 기대하고 있다",
            "시민들이 배우고 체험하는 즐거움을 느끼시길 바란다", "다양한 분야의 프로그램을 꾸준히 확대해 가겠다"),
    "open": ("개관한다고 밝혔다", "시민의 생활 편의를 높이고 지역 균형 발전에 이바지하기 위해 추진됐다",
             "시민들의 일상에 편리함과 즐거움을 더하는 공간이 될 것으로 기대하고 있다",
             "시민들께서 편하게 이용하시는 공간이 되길 바란다", "이용에 불편이 없도록 운영에 만전을 기하겠다"),
    "etc": ("추진한다고 밝혔다", "시민이 체감할 수 있는 변화를 만들기 위해 추진된다",
            "시민의 삶의 질을 높이는 데 도움이 될 것으로 기대하고 있다",
            "시민이 체감할 수 있는 성과로 이어지도록 최선을 다하겠다", "앞으로도 시민의 목소리에 귀 기울이겠다"),
}


def _kind(title, rest=""):
    for src in (title, rest):                    # 제목에서 먼저 찾고, 없으면 내용에서 찾는다
        for k, words in _TYPES:
            if any(w in src for w in words):
                return k
    return "etc"


def _list(items):
    items = [i.rstrip(".").strip() for i in items if i.strip()]
    return " ".join("△%s" % i for i in items)


def draft_full(text, org="익산시", dept="", tel=""):
    """→ {"title","sub1","sub2","paras"}. 보도자료 흐름: 리드문 → 취지 → 일시·장소·주관 → 주요 내용(△ 나열 또는 첫째·둘째) →
    사업비·대상·신청 → 기대효과 → 관계자 인용 → 문의. 입력한 사실은 그대로 쓰고, 취지·기대·인용은 유형에 맞는 일반 문장으로 채운다(꼭 확인·수정)."""
    d = parse_rough(text)
    title = d["title"] or "(제목)"
    kind = _kind(title, " ".join(d["content"]) + " " + d["apply"])
    verb, purpose_s, expect_s, quote_a, quote_b = _PHR[kind]
    noun = "행사" if kind in ("event", "meeting", "edu") else ("모집" if kind == "recruit" else "사업")
    topic = "이번 %s%s" % (noun, _josa(noun, "은", "는"))
    when, place = _when(d["when"]), d["place"]
    quoted = "‘%s’%s" % (title, _josa(title, "을", "를"))
    lead = "%s는 %s%s%s %s." % (org, ("%s " % when) if when else "", ("%s에서 " % place) if place else "", quoted, verb)
    paras = [re.sub(r"\s+", " ", lead).replace(" .", ".")]
    # 취지
    if d["purpose"]:
        pu = d["purpose"].rstrip(".")
        paras.append("%s %s" % (topic, pu + "." if (pu.endswith("다") or len(pu) > 28) else "%s%s 위해 마련됐다." % (pu, _josa(pu, "을", "를"))))
    else:
        paras.append("%s %s." % (topic, purpose_s))
    # 일시·장소·주관
    where = ("%s " % when if when else "") + ("%s에서" % place if place else "")
    live = kind in ("event", "meeting", "edu", "open")
    if live and where.strip() and d["host"]:
        paras.append("%s %s 열리며, %s%s 주관한다." % (topic, where.strip(), d["host"], _josa(d["host"], "이", "가")))
    elif live and where.strip():
        paras.append("%s %s 열린다." % (topic, where.strip()))
    elif where.strip():
        paras.append("%s기간은 %s이다." % ({"recruit": "접수 ", "support": "지원 "}.get(kind, "추진 "), where.strip().rstrip("에서").strip()) if when else "장소는 %s이다." % place)
        if d["host"]:
            paras.append("%s%s 주관한다." % (d["host"], _josa(d["host"], "이", "가")))
    elif d["host"]:
        paras.append("%s %s%s 주관한다." % (topic, d["host"], _josa(d["host"], "이", "가")))
    # 주요 내용
    content = [c.rstrip(".") for c in d["content"] if c.strip()]
    short = [c for c in content if len(c) <= 22 and not c.endswith("다")]
    if len(content) == len(short) and len(content) >= 2:
        paras.append("주요 프로그램은 %s 등으로 구성된다." % _list(content))
    elif len(content) == 1:
        c = content[0]
        paras.append(c + "." if (len(c) > 18 or c.endswith("다")) else "주요 내용은 %s%s다." % (c, _josa(c, "이", "")))
    elif content:
        for i, c in enumerate(content[:5]):
            if c.endswith("다") or len(c) > 22:
                paras.append("%s, %s." % (ORD[i], c))
            else:
                paras.append("%s, %s%s 마련된다." % (ORD[i], c, _josa(c, "이", "가")))
        if len(content) > 5:
            paras.append("이 밖에 %s 등도 마련된다." % ", ".join(content[5:]))
    if d["budget"]:
        paras.append("사업비는 %s이다." % d["budget"].rstrip("."))
    tg = d["target"].rstrip(".") if d["target"] else ""
    ap = d["apply"].rstrip(".") if d["apply"] else ""
    apply_s = ""
    if ap:
        if ap.endswith("다"):
            apply_s = ap
        elif re.search(r"누리집|홈페이지|사이트|앱|포털|시스템|온라인", ap):
            apply_s = "신청은 %s에서 할 수 있다" % ap
        elif re.search(r"방문|전화|우편|이메일|메일|팩스|현장", ap):
            apply_s = "신청은 %s%s 하면 된다" % (ap, _josa(ap, "으로", "로"))
        else:
            apply_s = "신청은 %s에서 하면 된다" % ap
    if tg and ap:
        paras.append("참여 대상은 %s%s며, %s." % (tg, "이" if _josa(tg, "이", "") else "", apply_s))
    elif tg:
        paras.append(("%s 참여할 수 있다." % tg) if tg.endswith("누구나") else "참여 대상은 %s%s다." % (tg, _josa(tg, "이", "")))
    elif ap:
        paras.append(apply_s + ".")
    # 기대효과
    if d["effect"]:
        ef = d["effect"].rstrip(".")
        paras.append("시는 %s" % (ef + "." if ef.endswith("다") else "%s로 이어질 것으로 기대하고 있다." % ef))
    else:
        who = "‘%s’%s" % (title, _josa(title, "을", "를")) if len(title) <= 20 else "이번 %s%s" % (noun, _josa(noun, "을", "를"))
        paras.append("시는 %s 통해 %s." % (who, expect_s))
    paras.append("%s 관계자는 “%s”며 “%s”고 말했다." % (org, quote_a, quote_b))
    tail = d["contact"] or ((dept + (" (%s)" % tel if tel else "")) if dept else "")
    paras.append("한편, 자세한 사항은 %s로 문의하면 된다." % (tail or "담당 부서"))
    # 부제(소제목)
    sub1 = ""
    if when or place:
        word = {"event": "개최", "meeting": "개최", "edu": "운영", "open": "개관", "recruit": "접수", "support": "시행", "etc": "추진"}[kind]
        sub1 = "– %s %s –" % (" ".join(x for x in (when, place) if x), word)
    sub2 = ""
    bits = []
    if tg:
        bits.append(tg)
    if content:
        bits.append(" · ".join(content[:3]) + (" 마련" if kind in ("event", "edu", "meeting") else ""))
    if bits:
        sub2 = "– %s –" % (", ".join(bits))
    return {"title": title, "sub1": sub1, "sub2": sub2, "paras": paras}


def draft(text, org="익산시", dept="", tel=""):
    """→ (제목, 본문 문단 목록)"""
    r = draft_full(text, org, dept, tel)
    return r["title"], r["paras"]
