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

# 4) 일정 없는 스티커는 포스트잇처럼 메모만, 일정이 생기면 목록+입력줄, + 버튼으로 새 스티커
p = u
p.set_date("2031-06-10"); root.update()
assert p._mode == "postit", p._mode
assert not p.foot.winfo_ismapped() and not p.wrap.winfo_ismapped() and p.mf.winfo_ismapped()
p.open_entry(); root.update()
assert p._mode == "full" and p.foot.winfo_ismapped()
p.show_entry = False; p.refresh(force=True); assert p._mode == "postit"
import core
core.add_item(a.data, "2031-06-10", core.new_item("일정 하나")); p.refresh(force=True); root.update()
assert p._mode == "full" and p.wrap.winfo_ismapped()
cnt = len(a.stickers.notes)
p.hd.winfo_children()[0].event_generate  # header exists
plus = [w for w in p.hd.winfo_children() if getattr(w, "_label", None) and w.itemcget(w._label, "text") == "＋"][0]
plus._on_press(None); plus._on_release(type("E", (), {"x": 2, "y": 2})())
root.update(); assert len(a.stickers.notes) == cnt + 1
print("POSTIT OK")

# 5) 닫은 스티커는 '스티커' 버튼 한 번에 모두 다시 뜬다 (자리·설정 그대로)
a.stickers.close_all()
a.conf["stickies_closed"] = []
a.stickers.add(); a.stickers.add(inbox=True); a.stickers.add(); root.update()
ids = sorted(a.stickers.notes)
pos = {k: (v.conf["x"], v.conf["y"], v.conf["color"]) for k, v in a.stickers.notes.items()}
for n in list(a.stickers.notes.values()):
    n.close()
assert not a.stickers.notes and len(a.conf["stickies_closed"]) == 3
a.stickers.open_all(None); root.update()                          # '스티커' 버튼: 닫은 3개가 돌아오고 새 것도 하나 더
assert set(ids) <= set(a.stickers.notes) and len(a.stickers.notes) == 4, sorted(a.stickers.notes)
assert {k: (v.conf["x"], v.conf["y"], v.conf["color"]) for k, v in a.stickers.notes.items() if k in pos} == pos
assert a.conf["stickies_closed"] == []
a.stickers.open_all(None); assert len(a.stickers.notes) == 5      # 닫은 게 없어도 버튼은 새 스티커를 붙인다
a.stickers.open_all(); assert len(a.stickers.notes) == 5          # 트레이·메뉴의 '모두 열기'는 새로 만들지 않는다
a.stickers.close_all(); a.conf["stickies_closed"] = []; a.stickers.open_all(); assert len(a.stickers.notes) == 1   # 하나도 없을 때만 새로
print("REOPEN OK")

# 6) 글자 크기 조절: 스티커마다 따로, 저장되고, 메모·일정·입력줄이 같이 커진다
q = list(a.stickers.notes.values())[0]
q.set_date("2031-07-01"); root.update()
q.memo.insert("1.0", "크기 시험"); q._save_memo()
base = abs(int(q.f_txt.cget("size")))
q.set_fs(0.3); root.update()
assert q.conf["fs"] == 1.3 and abs(int(q.f_txt.cget("size"))) > base
assert q.memo.cget("font") and q.fmt.get_markup() == "크기 시험"          # 크기를 바꿔도 내용은 그대로
q.set_fs(1.0, rel=False); assert q.conf["fs"] == 1.0 and abs(int(q.f_txt.cget("size"))) == base
for _ in range(30):
    q.set_fs(-0.1)
assert q.conf["fs"] == 0.7                                                   # 너무 작아지지 않는다
print("FONTSIZE OK")
