# -*- coding: utf-8 -*-
"""붙여넣은 글(메신저 대화·메일·캡처에서 읽은 글자)에서 일정과 할 일 후보를 뽑는다.
폰 앱(mobile/lib/analyze.dart)과 같은 규칙이며 같은 시험 문장(tests/analyze_cases.json)으로 맞춰 본다."""
import datetime as dt
import re

import gongmun
import quickadd

# 카카오톡 PC 복사: "[홍길동] [오후 2:30] 내용" / 날짜 구분줄: "2026년 10월 13일 화요일"
_CHAT_PREFIX = re.compile(r"^\s*\[[^\]]{1,20}\]\s*\[(?:오전|오후)\s*\d{1,2}:\d{2}\]\s*")
_SPEAKER = re.compile(r"^\s*[가-힣A-Za-z0-9_·\s]{1,12}\s*[:：]\s+(?=\S)")
_DATE_SEP = re.compile(r"^\s*20\d{2}\s*년\s*\d{1,2}\s*월\s*\d{1,2}\s*일\s*[월화수목금토일]요일\s*$")
_TASKY = re.compile(r"해\s*주|바랍니다|바람|부탁|제출|회신|확인|보고|준비|작성|검토|요청|신청|참석|송부|전달|공유|취합|점검|완료")
_MARKED = re.compile(r"^\s*(?:\d+[.)]|[가-하][.)]|[○ㅇ□■※•▪◦·❍◾◇▶►➢●∙⇒▷◆\-–—])\s")


# 업무 메신저(시 자체 메신저·쪽지) 복사본의 머리줄: "홍길동(도시재생과) 2026-10-13 오후 2:30:15", "보낸 사람: …"
_META = re.compile(r"^\s*(?:보낸\s*사람|받는\s*사람|보낸\s*시간|받은\s*시간|보낸\s*날짜|받은\s*날짜|발신자|수신자|발신\s*일시|"
                   r"수신\s*일시|일시|시간|참조|숨은\s*참조|읽음|안\s*읽음|첨부(?:\s*파일)?|파일)\s*[:：]")
_SUBJECT = re.compile(r"^\s*(?:제목|쪽지\s*제목)\s*[:：]\s*")
_STAMP_DATE = re.compile(r"\(?20\d{2}\s*[.\-/년]\s*\d{1,2}\s*[.\-/월]\s*\d{1,2}\s*일?\.?\)?(?:\s*\(?[월화수목금토일](?:요일)?\)?)?")
_STAMP_TIME = re.compile(r"\(?(?:(오전|오후|AM|PM|am|pm)\s*)?\d{1,2}:\d{2}(:\d{2})?(?:\s*(AM|PM|am|pm))?\)?")
_NAME_ONLY = re.compile(r"^[가-힣]{2,4}(?:\s*(?:주무관|팀장|과장|계장|국장|실장|소장|동장|주임|담당|담당자|대리|사원|님|선생님|부장|차장))?"
                        r"(?:\s*[(\[<][^)\]>]{1,20}[)\]>])?\s*(?:님)?$")
_EVENTY = re.compile(r"회의|설명회|교육|점검|행사|출장|보고|제출|마감|감사|면담|방문|간담회|워크숍|연수|훈련|시험|심사|회식|휴가|당직|발표|정산")
_TIME_LINE = re.compile(r"^\s*[(\[]?\s*(?:(?:오전|오후)\s*)?\d{1,2}:\d{2}(?::\d{2})?\s*[)\]]?\s*$")


def stamp_of(line):
    """메신저 머리줄이면 그 줄에 적힌 날짜(없으면 True), 아니면 None"""
    t = _STAMP_TIME.search(line)
    if not t:
        return None
    dm = _STAMP_DATE.search(line)
    if not (dm or t.group(1) or t.group(2) or t.group(3)):     # 연도 있는 날짜·오전/오후·초 중 하나는 있어야 머리줄
        return None
    rest = _STAMP_TIME.sub(" ", _STAMP_DATE.sub(" ", line))
    rest = re.sub(r"[\[\]|·•\-:：,]+", " ", rest)
    rest = re.sub(r"\s+", " ", rest).strip()
    if rest and (not _NAME_ONLY.match(rest) or _EVENTY.search(rest)):
        return None
    if dm:
        nums = re.findall(r"\d+", dm.group(0))
        try:
            return dt.date(int(nums[0]), int(nums[1]), int(nums[2]))
        except (ValueError, IndexError):
            return True
    return True


_GREET = re.compile(r"^\s*(?:안녕하세요|수고하십니다|수고 많으십니다|감사합니다)[.,!]?\s*")
_PAST = re.compile(r"(?:했습니다|했어요|했어|하였습니다|됐습니다|드렸습니다|보냈습니다|받았습니다|알겠습니다|네|넵)[.!]?\s*$")
_ASK_TAIL = re.compile(r"\s*(?:해\s*주세요|해주세요|부탁드려요|부탁드립니다|부탁해요|부탁합니다|바랍니다|바람|요망)\s*[.!]?\s*$")


def _finish(t):
    """대화체 문장을 일정 이름으로: 인사·요청 맺음말·'꼭/반드시'·요일 괄호를 정리"""
    t = _GREET.sub("", t)
    t = re.sub(r"\(\s*[월화수목금토일]\s*\)", " ", t)
    t = re.sub(r"(?<![가-힣])(?:꼭|반드시|급히)(?![가-힣])", " ", t)
    t = re.sub(r"\s*(?:있으니까|있으니|있습니다|있어요)\s*", " ", t)
    t = re.sub(r"^(?:까지|부터|에는|에서|에)\s+", "", t.strip())
    t = _ASK_TAIL.sub("", t.strip())
    t = re.sub(r"^([가-힣]{2,})(?:은|는)\s", r"\1 ", t)          # '자료는 제출' → '자료 제출'
    t = gongmun._tidy(t)
    return t or ""


def _lines(text, today=None):
    """[(줄, 기준일)] — 메신저 머리줄은 빼고, 머리줄에 적힌 날짜를 아래 글의 '내일·모레' 기준일로 쓴다"""
    today = today or dt.date.today()
    out, ref = [], today
    for raw in text.replace("\r", "").split("\n"):
        s = _CHAT_PREFIX.sub("", raw)
        if _DATE_SEP.match(s) or _META.match(s) or _TIME_LINE.match(s):
            continue
        st = stamp_of(s)
        if st is not None:
            if isinstance(st, dt.date) and 0 <= (today - st).days <= 30:
                ref = st
            continue
        s = _SUBJECT.sub("", s)
        s = re.sub(r"\s+", " ", s).strip()
        if len(re.sub(r"[^0-9A-Za-z가-힣]", "", s)) >= 4:
            out.append((s, ref))
    return out


def is_document(text):
    """공문처럼 항목 표지(1. 가. ❍ -)와 '제목' 이 있는 글인가 (그러면 공문 규칙으로 읽는다)"""
    ls = [l for l in text.replace("\r", "").split("\n") if l.strip()]
    marked = sum(1 for l in ls if _MARKED.match(l))
    return bool(re.search(r"^\s*제목\s", text, re.M)) or marked >= 3


def analyze(text, today=None):
    """반환: [{"date": date|None, "end": date|None, "time": "HH:MM"|"", "text": str, "pri": 1~3, "kind": "event"|"todo"}]"""
    today = today or dt.date.today()
    if is_document(text):
        _, items = gongmun.parse(text, today)
        out = [{"date": i["date"], "end": i["end"], "time": i["time"], "text": i["text"], "pri": i["pri"],
                "kind": "event", "period": i["period"], "detail": i.get("detail", "")} for i in items]
        return out
    lines = _lines(text, today)
    # 1) 날짜가 글자로 적힌 줄: 공문 규칙(줄마다 한 문단)으로 읽는다
    numbered = "\n".join(f"1. {_SPEAKER.sub('', l)}" for l, _ in lines)
    _, g = gongmun.parse("제목 메모\n" + numbered, today, with_topic=False)
    out, seen, covered = [], set(), []
    for i in g:
        key = (i["date"], i["text"])
        if key in seen:
            continue
        seen.add(key)
        out.append({"date": i["date"], "end": i["end"], "time": i["time"], "text": _finish(i["text"]) or i["text"],
                    "pri": i["pri"], "kind": "event", "period": i["period"]})
        covered.append(i["text"])
    # 2) 그 밖의 줄: '내일/다음주 화요일' 같은 말은 빠른 입력 규칙으로, 날짜가 없어도 부탁·요청이면 할 일로
    for l, ref in lines:
        body = _SPEAKER.sub("", l)
        if any(c and c in body for c in covered) or _PAST.search(body):
            continue
        r = quickadd.parse(body, ref)
        r["text"] = _finish(r["text"]) or r["text"]
        if r["date"] and r["date"] != ref or (r["date"] == ref and re.search(r"오늘", body)):
            key = (r["date"], r["text"])
            if key in seen:
                continue
            seen.add(key)
            out.append({"date": r["date"], "end": None, "time": r["time"] or "", "text": r["text"],
                        "pri": r["pri"] or (1 if re.search(r"긴급|급히|꼭|반드시", body) else 2), "kind": "event",
                        "period": False})
        elif _TASKY.search(body):
            text_only = _finish(quickadd.parse(body, ref)["text"]) or body
            key = (None, text_only)
            if key in seen:
                continue
            seen.add(key)
            out.append({"date": None, "end": None, "time": "", "text": text_only,
                        "pri": 1 if re.search(r"긴급|급히|꼭|반드시", body) else 2, "kind": "todo", "period": False})
    out.sort(key=lambda x: (x["date"] is None, x["date"] or dt.date.max, x["time"] or "99:99"))
    return out
