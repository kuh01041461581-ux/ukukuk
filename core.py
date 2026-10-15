# -*- coding: utf-8 -*-
"""DeskCalendar 데이터 규칙: 일정 항목, 반복, 동기화 병합, .ics 내보내기 (화면과 무관한 순수 로직)"""
import calendar
import datetime as dt
import time
import uuid

NON_DATE_KEYS = ("_inbox", "_deleted", "_work")   # _work: 업무(공문·제출 현황) 묶음


# ── 항목 ──
def now_ms():
    return int(time.time() * 1000)


def new_id():
    return uuid.uuid4().hex[:12]


def touch(t):
    t["updated"] = now_ms()
    return t


def new_item(text, **kw):
    t = {"id": new_id(), "text": text, "done": False, "pri": 2, "updated": now_ms()}
    t.update(kw)
    return t


def ensure_ids(data):
    """예전 data.json 의 항목에 id / updated 를 채운다. 바뀐 게 있으면 True"""
    changed = False
    for key, v in data.items():
        if key in ("_deleted", "_work"):
            continue
        for t in v.get("todos", []):
            if not t.get("id"):
                t["id"] = new_id()
                changed = True
            if "updated" not in t:
                t["updated"] = now_ms()
                changed = True
    data.setdefault("_deleted", {})
    return changed


def is_date_key(key):
    return key not in NON_DATE_KEYS


def parse_date(s):
    try:
        return dt.date.fromisoformat(s)
    except (TypeError, ValueError):
        return None


def item_end(t, start):
    """기간 일정의 종료일 (하루짜리면 None)"""
    e = parse_date(t.get("end"))
    return e if e and e > start else None


# ── 반복 ──
# 고르는 이름(code) → 저장 모양. 분기·반기는 '매월'의 간격(n)으로 저장해서 예전 버전과도 맞는다.
REPEAT_LABELS = [("", "없음"), ("d", "매일"), ("wd", "평일"), ("w", "매주"), ("m", "매월"), ("ml", "매월 말일"),
                 ("mw", "매월 n째 요일"), ("q", "분기마다"), ("h", "반기마다"), ("y", "매년")]
_NTH = {1: "첫째", 2: "둘째", 3: "셋째", 4: "넷째", -1: "마지막"}


def nth_of(d):
    """d 가 그 달의 몇째 요일인가 (다섯째는 '마지막'으로)"""
    k = (d.day - 1) // 7 + 1
    return -1 if k >= 5 else k


def repeat_from_code(code, start=None, until=None):
    if not code:
        return None
    if code == "q":
        rep = {"f": "m", "n": 3}
    elif code == "h":
        rep = {"f": "m", "n": 6}
    elif code == "mw":
        rep = {"f": "mw", "n": 1, "k": nth_of(start) if start else 1}
    else:
        rep = {"f": code, "n": 1}
    if until:
        rep["until"] = until
    return rep


def repeat_code(rep):
    if not rep:
        return ""
    f, n = rep.get("f", ""), int(rep.get("n", 1) or 1)
    if f == "m" and n == 3:
        return "q"
    if f == "m" and n == 6:
        return "h"
    return f


def repeat_label(rep, start=None):
    """'매월 둘째 화요일' 처럼 사람이 읽는 반복 이름"""
    code = repeat_code(rep)
    if code == "mw":
        wd = "월화수목금토일"[start.weekday()] if start else ""
        return f"매월 {_NTH.get(int(rep.get('k', 1)), '')} {wd}요일".replace("  ", " ")
    if code == "m" and int(rep.get("n", 1) or 1) > 1:
        return f"{rep['n']}개월마다"
    return dict(REPEAT_LABELS).get(code, "")


def repeat_hit(rep, start, d):
    """반복 규칙 rep 가 start 날짜에서 시작할 때 d 날짜에 일정이 있는가"""
    if not rep or d < start:
        return False
    until = parse_date(rep.get("until"))
    if until and d > until:
        return False
    n = max(1, int(rep.get("n", 1) or 1))
    f = rep.get("f")
    if f == "d":
        return (d - start).days % n == 0
    if f == "wd":
        return d.weekday() < 5
    if f == "w":
        return (d - start).days % (7 * n) == 0
    if f in ("m", "ml", "mw"):
        months = (d.year - start.year) * 12 + d.month - start.month
        if months % n:
            return False
        last = calendar.monthrange(d.year, d.month)[1]
        if f == "ml":
            return d.day == last
        if f == "mw":
            if d.weekday() != start.weekday():
                return False
            k = int(rep.get("k", 1) or 1)
            return d.day + 7 > last if k < 0 else (d.day - 1) // 7 + 1 == k
        return d.day == min(start.day, last)
    if f == "y":
        if (d.year - start.year) % n:
            return False
        if start.month == 2 and start.day == 29:
            return d.month == 2 and d.day == min(29, calendar.monthrange(d.year, 2)[1])
        return d.month == start.month and d.day == start.day
    return False


def first_hit(rep, start, limit=400):
    """반복 일정이 실제로 처음 나오는 날 (매월 말일처럼 시작일과 다를 수 있다)"""
    for k in range(limit):
        d = start + dt.timedelta(days=k)
        if repeat_hit(rep, start, d):
            return d
    return None


# ── 근무일 (주말·공휴일 빼고 세기) ──
def is_workday(d, holiday=None):
    return d.weekday() < 5 and not (holiday and holiday(d))


def add_workdays(start, n, holiday=None):
    """start 다음 날부터 근무일로 n 일째 되는 날 (n=0 이면 start 가 쉬는 날일 때 다음 근무일)"""
    d = start
    if n <= 0:
        while not is_workday(d, holiday):
            d += dt.timedelta(days=1)
        return d
    while n:
        d += dt.timedelta(days=1)
        if is_workday(d, holiday):
            n -= 1
    return d


def workdays_between(a, b, holiday=None):
    """a 다음 날부터 b 까지 근무일 수 (b 가 앞이면 음수)"""
    if b < a:
        return -workdays_between(b, a, holiday)
    return sum(1 for k in range(1, (b - a).days + 1) if is_workday(a + dt.timedelta(days=k), holiday))


def occurrence(t, d):
    """반복 항목의 d 날짜 표시용 사본 (완료 여부는 날짜별로 따로 기억)"""
    v = dict(t)
    v["_src"] = t
    v["_occ"] = d
    v["done"] = d.isoformat() in (t.get("done_dates") or [])
    return v


def base_of(t):
    return t.get("_src", t)


def day_items(data, d):
    """d 날짜에 보이는 항목: [(소유 리스트, 항목(또는 반복 사본), 시작일)]"""
    out = []
    for key, v in data.items():
        if key in NON_DATE_KEYS:
            continue
        s = parse_date(key)
        if not s or s > d:
            continue
        for t in v.get("todos", []):
            rep = t.get("repeat")
            if rep:
                if repeat_hit(rep, s, d):
                    out.append((v["todos"], occurrence(t, d), s))
                continue
            e = item_end(t, s)
            if s == d or (e and e >= d):
                out.append((v["todos"], t, s))
    return out


def find_owner(data, t):
    """항목이 들어 있는 (키, 리스트)"""
    t = base_of(t)
    for key, v in data.items():
        if key in ("_deleted", "_work"):
            continue
        for x in v.get("todos", []):
            if x is t:
                return key, v["todos"]
    return None, None


def remove_item(data, t):
    key, lst = find_owner(data, t)
    if lst is None:
        return False
    t = base_of(t)
    for k, x in enumerate(lst):
        if x is t:
            del lst[k]
            break
    return True


def delete_item(data, t):
    """삭제 + 동기화용 삭제 기록 남기기"""
    t = base_of(t)
    if remove_item(data, t) and t.get("id"):
        data.setdefault("_deleted", {})[t["id"]] = now_ms()


def add_item(data, key, t):
    data.setdefault(key, {"memo": "", "todos": []}).setdefault("todos", []).append(t)


# ── 동기화 ──
def export_state(data):
    items, memos = [], {}
    for key, v in data.items():
        if key in ("_deleted", "_work"):
            continue
        for t in v.get("todos", []):
            if not t.get("id"):
                continue
            r = {k: val for k, val in t.items() if not k.startswith("_")}
            r["date"] = None if key == "_inbox" else key
            items.append(r)
        if key != "_inbox" and (v.get("memo", "").strip() or v.get("memo_ts")):    # 지운 메모도 '비웠다'는 기록(ts)으로 보내야 다른 기기의 옛 메모가 되살아나지 않는다
            memos[key] = {"memo": v.get("memo", ""), "ts": v.get("memo_ts", 0)}
    work = {i: {k: val for k, val in c.items() if not k.startswith("_")} for i, c in (data.get("_work") or {}).items()}
    return {"items": items, "memos": memos, "deleted": dict(data.get("_deleted", {})), "work": work}


def merge_state(data, remote):
    """상대 기기의 상태를 합친다 (항목마다 더 늦게 수정된 쪽이 이김). 바뀐 게 있으면 True"""
    changed = False
    dele = data.setdefault("_deleted", {})
    for i, ts in (remote.get("deleted") or {}).items():
        if ts > dele.get(i, 0):
            dele[i] = ts
            changed = True
    idx = {}
    for key, v in data.items():
        if key in ("_deleted", "_work"):
            continue
        for t in v.get("todos", []):
            if t.get("id"):
                idx[t["id"]] = (key, t)
    for i, ts in list(dele.items()):                      # 삭제 기록 적용
        if i in idx and idx[i][1].get("updated", 0) <= ts:
            key, t = idx.pop(i)
            data[key]["todos"] = [x for x in data[key]["todos"] if x is not t]
            changed = True
    for r in remote.get("items") or []:
        i = r.get("id")
        if not i or dele.get(i, 0) >= r.get("updated", 0):
            continue
        key = r.get("date") or "_inbox"
        t = {k: val for k, val in r.items() if k != "date"}
        if i in idx:
            okey, ot = idx[i]
            if r.get("updated", 0) > ot.get("updated", 0):
                data[okey]["todos"] = [x for x in data[okey]["todos"] if x is not ot]
                add_item(data, key, t)
                changed = True
        else:
            add_item(data, key, t)
            changed = True
    for d, m in (remote.get("memos") or {}).items():
        e = data.get(d)
        if m.get("ts", 0) > (e.get("memo_ts", 0) if e else 0):
            e = data.setdefault(d, {"memo": "", "todos": []})
            e["memo"], e["memo_ts"] = m.get("memo", ""), m["ts"]
            changed = True
    work = data.get("_work") or {}                        # 업무 묶음: 통째로, 더 늦게 고친 쪽이 이김
    for i in [i for i, c in work.items() if dele.get(i, 0) >= c.get("updated", 0)]:
        del work[i]
        changed = True
    for i, c in (remote.get("work") or {}).items():
        if dele.get(i, 0) >= c.get("updated", 0):
            continue
        if c.get("updated", 0) > work.get(i, {}).get("updated", -1):
            work[i] = dict(c)
            changed = True
    if work:
        data["_work"] = work
    cutoff = now_ms() - 90 * 86400 * 1000                 # 오래된 삭제 기록 정리
    for i in [i for i, ts in dele.items() if ts < cutoff]:
        del dele[i]
    return changed


# ── .ics 내보내기 ──
def _esc(s):
    return (s or "").replace("\\", "\\\\").replace(";", "\\;").replace(",", "\\,").replace("\n", "\\n")


def build_ics(data):
    lines = ["BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//DeskCalendar//KO", "CALSCALE:GREGORIAN"]
    stamp = dt.datetime.utcnow().strftime("%Y%m%dT%H%M%SZ")
    prio = {0: 1, 1: 3, 2: 5, 3: 9}
    for key, v in sorted(data.items()):
        s = parse_date(key)
        if not s:
            continue
        for t in v.get("todos", []):
            lines += ["BEGIN:VEVENT", f"UID:{t.get('id', new_id())}@deskcalendar", f"DTSTAMP:{stamp}",
                      f"SUMMARY:{_esc(t.get('text', ''))}", f"PRIORITY:{prio.get(t.get('pri', 2), 5)}"]
            if t.get("note"):
                lines.append(f"DESCRIPTION:{_esc(t['note'])}")
            e = item_end(t, s)
            hhmm = (t.get("time") or "").replace(":", "")
            if hhmm and not e:
                st = dt.datetime.combine(s, dt.time(int(hhmm[:2]), int(hhmm[2:])))
                lines += [f"DTSTART:{st:%Y%m%dT%H%M%S}", f"DTEND:{st + dt.timedelta(hours=1):%Y%m%dT%H%M%S}"]
            else:
                end = (e or s) + dt.timedelta(days=1)
                lines += [f"DTSTART;VALUE=DATE:{s:%Y%m%d}", f"DTEND;VALUE=DATE:{end:%Y%m%d}"]
            rep = t.get("repeat")
            if rep:
                f = rep.get("f")
                rule = {"d": "FREQ=DAILY", "w": "FREQ=WEEKLY", "m": "FREQ=MONTHLY", "y": "FREQ=YEARLY",
                        "wd": "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR", "ml": "FREQ=MONTHLY;BYMONTHDAY=-1"}.get(f)
                if f == "mw":
                    rule = f"FREQ=MONTHLY;BYDAY={int(rep.get('k', 1))}{['MO','TU','WE','TH','FR','SA','SU'][s.weekday()]}"
                if rule:
                    if rep.get("n", 1) and rep.get("n", 1) > 1 and f != "wd":
                        rule += f";INTERVAL={rep['n']}"
                    u = parse_date(rep.get("until"))
                    if u:
                        rule += f";UNTIL={u:%Y%m%d}"
                    lines.append("RRULE:" + rule)
            lines.append("END:VEVENT")
    lines.append("END:VCALENDAR")
    return "\r\n".join(lines) + "\r\n"
