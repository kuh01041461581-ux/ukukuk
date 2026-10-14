"""스티커: 날짜 없는 칸 스티커, 켤 때 접힘 상태 그대로 제자리 복원 (Tk 필요). 실행: python tests/test_sticky_inbox.py"""
import json
import os
import sys
import tempfile
import time

# 내 진짜 데이터(%APPDATA%)를 건드리지 않도록 임시 폴더를 쓴다
_tmp = tempfile.mkdtemp(prefix="dc_sticky_")
os.environ["APPDATA"] = os.environ["HOME"] = _tmp
os.makedirs(os.path.join(_tmp, "DeskCalendar"), exist_ok=True)
json.dump({"seen_help": True}, open(os.path.join(_tmp, "DeskCalendar", "config.json"), "w"))
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
# 2) 접어 둔 건 한 줄로 접힌 채, 펼쳐 둔 건 펼쳐진 채 같은 자리·크기로 복원
c = n.conf; c.update(x=137, y=211, w=300, h=400, folded=True)
a.stickers.add(); root.update()
m = [x for x in a.stickers.notes.values() if x is not n][0]
m.conf.update(x=500, y=100, w=320, h=420, folded=False); a.stickers.save()
a.stickers.close_all(); a.stickers.restore(); root.update()
notes = {str(k): v for k, v in a.stickers.notes.items()}
f = [v for v in notes.values() if v.conf["folded"]][0]
u = [v for v in notes.values() if not v.conf["folded"]][0]
root.update()
bar = f.bar_h()
assert f.top.geometry() == f"300x{bar}+137+211", f.top.geometry()          # 한 줄 높이로 고정
assert f.conf["h"] == 400                                                  # 펼칠 때 돌아갈 크기는 그대로
assert u.top.geometry() == "320x420+500+100", u.top.geometry()
f.toggle_fold(); [(root.update(), time.sleep(0.03)) for _ in range(30)]
assert f.top.winfo_height() == 400, f.top.winfo_height()                   # 펼치면 원래 크기
f.toggle_fold(); [(root.update(), time.sleep(0.03)) for _ in range(30)]
assert f.top.winfo_height() == bar
# 3) 화면 밖 값은 보이는 위치만 보정하고 저장값은 유지
u.conf["x"] = 99999; u._place(); assert u.conf["x"] == 99999
print("STICKY OK")
