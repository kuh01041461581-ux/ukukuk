# -*- coding: utf-8 -*-
"""폰 앱(Dart)과 함께 쓰는 시험 문장(JSON)을 PC(Python) 쪽에서도 확인한다. 실행: python tests/test_fixtures.py"""
import datetime as dt
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

import analyze  # noqa: E402
import core  # noqa: E402
import gongmun  # noqa: E402
import quickadd  # noqa: E402


def load(name):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        return json.load(f)


def iso(d):
    return d.isoformat() if d else None


def test_quickadd():
    j = load("quickadd_cases.json")
    today = dt.date.fromisoformat(j["today"])
    for c in j["cases"]:
        r = quickadd.parse(c["in"], today)
        got = {"text": r["text"], "date": iso(r["date"]), "time": r["time"], "pri": r["pri"], "repeat": r["repeat"]}
        assert got == c["out"], (c["in"], got)


def test_gongmun():
    for c in load("gongmun_cases.json"):
        title, items = gongmun.parse(c["text"], dt.date.fromisoformat(c["today"]))
        got = [{"date": iso(i["date"]), "end": iso(i["end"]), "time": i["time"], "text": i["text"], "pri": i["pri"]}
               for i in items]
        exp = [{k: i[k] for k in ("date", "end", "time", "text", "pri")} for i in c["items"]]
        assert title == c["title"], title
        assert got == exp, (c["title"], got)


def test_analyze():
    for c in load("analyze_cases.json"):
        r = analyze.analyze(c["text"], dt.date.fromisoformat(c["today"]))
        got = [{"date": iso(i["date"]), "end": iso(i["end"]), "time": i["time"], "text": i["text"], "pri": i["pri"],
                "kind": i["kind"]} for i in r]
        assert got == c["items"], (c["text"][:30], got)


def test_core():
    j = load("core_cases.json")
    for c in j["repeat"]:
        start = dt.date.fromisoformat(c["start"])
        rep = core.repeat_from_code(c["code"], start)
        assert rep == c["rep"] and core.repeat_label(rep, start) == c["label"]
        hits = [iso(start + dt.timedelta(days=k)) for k in range(800) if core.repeat_hit(rep, start, start + dt.timedelta(days=k))]
        assert hits[:14] == c["hits"], c["code"]
    hol = set(j["holidays"])
    h = lambda d: d.isoformat() in hol  # noqa: E731
    for c in j["workdays"]:
        s = dt.date.fromisoformat(c["start"])
        assert iso(core.add_workdays(s, c["n"], h)) == c["out"], c


if __name__ == "__main__":
    n = 0
    for k, fn in list(globals().items()):
        if k.startswith("test_") and callable(fn):
            fn()
            n += 1
            print("ok", k)
    print(f"{n} fixture tests passed")
