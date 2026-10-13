# -*- coding: utf-8 -*-
"""업무(공문 한 건 = 업무 하나) 관리: 기한, 업무 폴더(공문·작성 자료·제출본), 반복 업무 모음.
화면과 무관한 순수 로직이라 시험(tests/test_work.py)으로 확인한다. 업무 정보는 data["_work"] 에 들어가 폰과 동기화된다.

업무 모양:
  {"id", "title", "due": "YYYY-MM-DD"|None, "received": "YYYY-MM-DD", "doc_no": "",
   "done": False, "note": "", "folder": "PC 의 업무 폴더 경로", "created": ms, "updated": ms}"""
import datetime as dt
import os
import re
import shutil

import core

SUBDIRS = ("1. 공문", "2. 작성 자료", "3. 제출본")
WD = "월화수목금토일"


# ── 업무 만들기·찾기 ──
def new_case(title, due=None, **kw):
    c = {"id": core.new_id(), "title": title.strip() or "새 업무", "due": due.isoformat() if isinstance(due, dt.date) else due,
         "received": dt.date.today().isoformat(), "done": False, "created": core.now_ms(),
         "updated": core.now_ms()}
    c.update(kw)
    return c


def cases(data):
    return data.setdefault("_work", {})


def add_case(data, c):
    cases(data)[c["id"]] = c
    return c


def delete_case(data, cid):
    if cases(data).pop(cid, None) is not None:
        data.setdefault("_deleted", {})[cid] = core.now_ms()
    for _, v in list(data.items()):
        if isinstance(v, dict):
            for t in v.get("todos", []) if "todos" in v else []:
                if t.get("case") == cid:
                    t.pop("case", None)
                    core.touch(t)


def touch(c):
    c["updated"] = core.now_ms()
    return c


def sorted_cases(data, include_done=False):
    out = [c for c in (data.get("_work") or {}).values() if include_done or not c.get("done")]
    out.sort(key=lambda c: (bool(c.get("done")), c.get("due") or "9999", c.get("created", 0)))
    return out


def case_items(data, cid):
    """업무에 묶인 일정들 [(날짜키, 항목)]"""
    out = []
    for key, v in data.items():
        if key in ("_deleted", "_work") or not isinstance(v, dict):
            continue
        for t in v.get("todos", []):
            if t.get("case") == cid:
                out.append((key, t))
    return out


def dday(c, today=None):
    d = core.parse_date(c.get("due"))
    if not d:
        return None
    return (d - (today or dt.date.today())).days


def dday_label(n):
    if n is None:
        return ""
    return "D-DAY" if n == 0 else (f"D-{n}" if n > 0 else f"D+{-n}")


def fmt_date(d, wd=True):
    """공문식 날짜 10.20.(화)"""
    if isinstance(d, str):
        d = core.parse_date(d)
    if not d:
        return ""
    return f"{d.month}.{d.day}." + (f"({WD[d.weekday()]})" if wd else "")


# ── 업무 폴더 ──
def safe_name(s, limit=60):
    s = re.sub(r'[\\/:*?"<>|\r\n\t]+', " ", s or "").strip(" .")
    s = re.sub(r"\s+", " ", s)
    return (s[:limit].rstrip() or "업무")


def default_folder(root, c):
    d = core.parse_date(c.get("due")) or core.parse_date(c.get("received")) or dt.date.today()
    return os.path.join(root, str(d.year), f"{d:%m%d} {safe_name(c['title'])}")


def ensure_folder(root, c):
    path = c.get("folder")
    if not path or not os.path.isdir(path):
        path = default_folder(root, c)
        c["folder"] = path
        touch(c)
    for s in SUBDIRS:
        os.makedirs(os.path.join(path, s), exist_ok=True)
    return path


def sub(c, k):
    return os.path.join(c["folder"], SUBDIRS[k])


def _unique(path):
    if not os.path.exists(path):
        return path
    stem, ext = os.path.splitext(path)
    k = 2
    while os.path.exists(f"{stem} ({k}){ext}"):
        k += 1
    return f"{stem} ({k}){ext}"


def copy_in(src, folder, name=None):
    dst = os.path.join(folder, name or os.path.basename(src))
    if os.path.abspath(src) == os.path.abspath(dst):
        return dst
    dst = _unique(dst)
    shutil.copy2(src, dst)
    return dst


def add_doc(c, path):
    return copy_in(path, sub(c, 0))


def add_file(c, path, k=1):
    """작성 자료(1)·제출본(2) 폴더에 파일 넣기"""
    return copy_in(path, sub(c, k))


def files(c, k):
    if not c.get("folder") or not os.path.isdir(sub(c, k)):
        return []
    d = sub(c, k)
    return [os.path.join(d, f) for f in sorted(os.listdir(d)) if os.path.isfile(os.path.join(d, f)) and not f.startswith("~$")]



# ── 반복 업무 모음 (기관마다 날짜가 다르니 넣기 전에 고칠 수 있게 보여 준다) ──
# rule: ("day", 일) 매월 그날 / ("last",) 매월 말일 / ("nth", k, 요일0~6) 매월 k째 요일 /
#       ("q", 일) 분기 첫 달(1·4·7·10월) 그날 / ("h", 월, 일) 반기 / ("y", 월, 일) 매년
TEMPLATES = [
    ("매월", "월간 업무계획 제출", ("day", 25)),
    ("매월", "업무추진비·법인카드 사용내역 정리", ("last",)),
    ("매월", "월례회의(정례조회)", ("nth", 1, 0)),
    ("매월", "사이버 보안진단의 날 (PC 점검)", ("nth", 3, 2)),
    ("매월", "주요 업무 추진실적 입력", ("day", 5)),
    ("분기", "분기별 추진실적 보고", ("q", 5)),
    ("분기", "개인정보 보호 자율점검", ("q", 20)),
    ("반기", "상·하반기 자체평가 자료 작성", ("h", 6, 20)),
    ("매년", "연말정산 서류 제출", ("y", 1, 15)),
    ("매년", "결산 자료 준비", ("y", 2, 10)),
    ("매년", "다음 연도 본예산 요구 자료 준비", ("y", 8, 20)),
    ("매년", "행정사무감사 자료 준비", ("y", 10, 25)),
    ("매년", "업무 인수인계서 정리 (정기 인사 전)", ("y", 1, 2)),
    ("매년", "청렴·폭력예방 등 법정 의무교육 이수 확인", ("y", 11, 30)),
]


def template_first(rule, today=None):
    """반복 업무가 오늘 이후 처음 돌아오는 날과 반복 코드 (date, code)"""
    import calendar
    today = today or dt.date.today()
    kind = rule[0]

    def months(step, first):
        y, m = today.year, today.month
        for _ in range(30):
            if (m - first) % step == 0:
                yield y, m
            m += 1
            if m > 12:
                y, m = y + 1, 1

    if kind == "day":
        for y, m in months(1, 1):
            d = dt.date(y, m, min(rule[1], calendar.monthrange(y, m)[1]))
            if d >= today:
                return d, "m"
    if kind == "last":
        for y, m in months(1, 1):
            d = dt.date(y, m, calendar.monthrange(y, m)[1])
            if d >= today:
                return d, "ml"
    if kind == "nth":
        k, wd = rule[1], rule[2]
        for y, m in months(1, 1):
            first = dt.date(y, m, 1)
            d = first + dt.timedelta(days=(wd - first.weekday()) % 7 + 7 * (k - 1))
            if d.month == m and d >= today:
                return d, "mw"
    if kind in ("q", "h"):
        step = 3 if kind == "q" else 6
        mon = 1 if kind == "q" else rule[1]
        day = rule[1] if kind == "q" else rule[2]
        for y, m in months(step, mon):
            d = dt.date(y, m, min(day, calendar.monthrange(y, m)[1]))
            if d >= today:
                return d, kind
    if kind == "y":
        for y in (today.year, today.year + 1):
            d = dt.date(y, rule[1], min(rule[2], calendar.monthrange(y, rule[1])[1]))
            if d >= today:
                return d, "y"
    return today, ""


def rule_label(rule):
    k = rule[0]
    if k == "day":
        return f"매월 {rule[1]}일"
    if k == "last":
        return "매월 말일"
    if k == "nth":
        return f"매월 {['', '첫째', '둘째', '셋째', '넷째'][rule[1]]} {WD[rule[2]]}요일"
    if k == "q":
        return f"1·4·7·10월 {rule[1]}일"
    if k == "h":
        return f"{rule[1]}·{(rule[1] + 5) % 12 + 1}월 {rule[2]}일"
    if k == "y":
        return f"매년 {rule[1]}월 {rule[2]}일"
    return ""


# ── 공문 → 업무 ──
_DUEWORD = re.compile(r"제출|회신|보고|기한|마감|신청|입력|등록")


def due_from_items(items):
    """공문에서 뽑은 일정 중 '제출·회신' 같은 마감 날짜 (없으면 가장 늦은 날짜)"""
    cand = [i for i in items if _DUEWORD.search(i.get("text", ""))]
    pool = cand or items
    if not pool:
        return None
    return max((i.get("end") or i["date"]) if not i.get("period") else i["date"] for i in pool)


def doc_no(text):
    m = re.search(r"([가-힣A-Za-z]{2,20}-\d{2,7})\s*\(\s*20\d{2}\s*[.\-]\s*\d{1,2}\s*[.\-]\s*\d{1,2}", text or "")
    return m.group(1) if m else ""
