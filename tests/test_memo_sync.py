"""스티커 메모 ↔ 오른쪽 패널 메모 연동 (Tk 필요). 실행: python tests/test_memo_sync.py"""
import json
import os
import sys
import tempfile
import time

_tmp = tempfile.mkdtemp(prefix="dc_memo_")
os.environ["APPDATA"] = os.environ["HOME"] = _tmp
os.makedirs(os.path.join(_tmp, "DeskCalendar"), exist_ok=True)
json.dump({"seen_help": True}, open(os.path.join(_tmp, "DeskCalendar", "config.json"), "w"))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import tkinter as tk
import desk_calendar as dc

root = tk.Tk(); a = dc.DeskCalendar(root); root.update(); time.sleep(0.3); root.update()
a.stickers.close_all()
a.show_day(a.today)
a.stickers.add(); root.update()
n = list(a.stickers.notes.values())[0]
# 1) 스티커에서 쓴 메모 → 패널 메모 칸에 바로 보인다
n.memo.insert("1.0", "스티커에서 씀"); n._save_memo(); root.update()
assert a.memo.get("1.0", "end-1c") == "스티커에서 씀", a.memo.get("1.0", "end-1c")
# 2) 패널에서 쓴 메모 → 스티커에 바로 보인다
a.memo.delete("1.0", "end"); a.memo.insert("1.0", "패널에서 씀"); a._flush_memo(); root.update()
assert n.memo.get("1.0", "end-1c") == "패널에서 씀", n.memo.get("1.0", "end-1c")
# 3) 패널은 고친 적이 없으면 옛 글로 스티커 내용을 덮어쓰지 않는다
a.memo.delete("1.0", "end"); a.memo.insert("1.0", "옛 글"); a.memo.edit_modified(False)
a._entry(a.today, create=True)["memo"] = "스티커가 고친 새 글"
a._flush_memo(); a.show_day(a.today)
assert a._entry(a.today)["memo"] == "스티커가 고친 새 글", a._entry(a.today)["memo"]
print("MEMO SYNC OK")
