"""Ctrl+Alt+Space 빠른 입력 창: '주간업무취합 261024 중요' → 2026-10-24 에 중요 일정 (Tk 필요). 실행: python tests/test_quickbar.py"""
import datetime as dt
import json
import os
import sys
import tempfile
import time

_tmp = tempfile.mkdtemp(prefix="dc_qbar_")
os.environ["APPDATA"] = os.environ["HOME"] = _tmp
os.makedirs(os.path.join(_tmp, "DeskCalendar"), exist_ok=True)
json.dump({"seen_help": True}, open(os.path.join(_tmp, "DeskCalendar", "config.json"), "w"))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import tkinter as tk
import core
import desk_calendar as dc

root = tk.Tk(); a = dc.DeskCalendar(root); root.update(); time.sleep(0.3); root.update()
a.quick_add_anywhere(); root.update()
assert a.cmdbar is not None and a.cmdbar.anywhere
a.quick_add_anywhere(); root.update()                              # 다시 누르면 닫힌다
assert a.cmdbar is None
a.quick_add_anywhere(); root.update()
bar = a.cmdbar
bar.entry.insert(0, "주간업무취합 261024 중요")
bar.refresh(); root.update()
assert bar.parsed["date"] == dt.date(2026, 10, 24) and bar.parsed["pri"] == 1 and bar.parsed["text"] == "주간업무취합", bar.parsed
bar.run(); root.update()
items = a.data["2026-10-24"]["todos"]
assert len(items) == 1 and items[0]["text"] == "주간업무취합" and items[0]["pri"] == 1, items
assert a.cmdbar is None
# 날짜·시간·중요를 한 줄에
a.quick_add_anywhere(); root.update()
a.cmdbar.entry.insert(0, "20261030 감사 자료 협의 14:30 긴급")
a.cmdbar.refresh(); a.cmdbar.run(); root.update()
t = [x for x in a.data["2026-10-30"]["todos"] if x["text"] == "감사 자료 협의"][0]
assert t["time"] == "14:30" and t["pri"] == 1, t
# 일정이 많은 날짜의 상세 창: 어느 줄 위에서든 마우스 휠이 걸려 있다
for i in range(30):
    core.add_item(a.data, "2026-10-28", core.new_item(f"일정 {i}"))
a._after_todo_change()
a.open_detail(dt.date(2026, 10, 28)); root.update()
def walk(w):
    yield w
    for c in w.winfo_children():
        yield from walk(c)
missing = [w for w in walk(a.detail.top) if not isinstance(w, (tk.Text, tk.Entry, tk.Toplevel)) and "<MouseWheel>" not in w.bind()
           and str(w).count(".") > 6]                                  # 목록 안쪽 위젯만
assert not missing, [(w.winfo_class(), str(w)[-30:]) for w in missing[:5]]
print("QUICKBAR OK")
