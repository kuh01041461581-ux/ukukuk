"""스티커: 날짜 없는 칸 스티커, 켤 때 펼쳐서 제자리 복원 (Tk 필요). 실행: python tests/test_sticky_inbox.py"""
import os
import sys
import time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import tkinter as tk
import desk_calendar as dc

root = tk.Tk(); a = dc.DeskCalendar(root); root.update(); time.sleep(0.3); root.update()
a.stickers.close_all()
# 1) 날짜 없는 스티커
a.stickers.add(inbox=True); root.update()
n = list(a.stickers.notes.values())[0]
assert n.is_inbox() and "보관함" in n.title.cget("text"), n.title.cget("text")
n.memo.insert("1.0", "날짜 없는 메모"); n._save_memo()
assert a._inbox().get("memo") == "날짜 없는 메모"
n.entry.focus_force(); n._ph_off(); n.entry.insert(0, "보관함 할 일"); n.add_item(); root.update()
assert any(t["text"] == "보관함 할 일" for t in a._inbox()["todos"])
n.go(1); assert n.is_inbox()                       # 날짜 이동 버튼은 동작 안 함
n.set_date(None); assert not n.is_inbox()
n.set_date("inbox"); assert n.is_inbox()
# 2) 접어 둔 채 저장해도 다시 켜면 펼쳐져서 제자리, 저장값은 안 바뀜
c = n.conf; c.update(x=137, y=211, w=300, h=400, folded=True); a.stickers.save()
saved = [dict(x) for x in a.conf["stickies"]]
a.stickers.close_all(); a.stickers.restore(); root.update()
n2 = list(a.stickers.notes.values())[0]
assert not n2.conf["folded"], "펼쳐져야 함"
assert (n2.conf["x"], n2.conf["y"], n2.conf["w"], n2.conf["h"]) == (137, 211, 300, 400), n2.conf
root.update(); g = n2.top.geometry(); print(g)
assert g.startswith("300x400+137+211"), g
# 3) 화면 밖 값은 보이는 위치만 보정하고 저장값은 유지
n2.conf["x"] = 99999; n2._place(); assert n2.conf["x"] == 99999
print("STICKY OK")
