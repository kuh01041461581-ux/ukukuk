"""부서 공유: 작성자 표시, 부서원 명단 동기화, 내가 쓴 일정만 수정 (Tk 필요). 실행: python tests/test_team.py"""
import datetime as dt
import json
import os
import sys
import tempfile
import time

_tmp = tempfile.mkdtemp(prefix="dc_team_")
os.environ["APPDATA"] = os.environ["HOME"] = _tmp
os.makedirs(os.path.join(_tmp, "DeskCalendar"), exist_ok=True)
json.dump({"seen_help": True}, open(os.path.join(_tmp, "DeskCalendar", "config.json"), "w"))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import core
import team

# ── 순수 규칙: 두 사람의 데이터가 합쳐진다 ──
kim, lee = {"_deleted": {}}, {"_deleted": {}}
core.set_author("u_kim")
a = core.new_item("행사 준비 회의", pri=1)
core.add_item(kim, "2026-11-10", a)
team.register(kim, "u_kim", "김주무관", "#ff6b81")
core.set_author("u_lee")
b = core.new_item("주간업무 취합")
core.add_item(lee, "2026-11-10", b)
team.register(lee, "u_lee", "이주무관", "#54a0ff")
core.set_author(None)
assert a["au"] == "u_kim" and b["au"] == "u_lee"
core.merge_state(kim, core.export_state(lee))
core.merge_state(lee, core.export_state(kim))
for d in (kim, lee):
    assert {t["text"] for t in d["2026-11-10"]["todos"]} == {"행사 준비 회의", "주간업무 취합"}
    assert team.name_of(d, "u_kim") == "김주무관" and team.color_of(d, "u_lee") == "#54a0ff"
# 남의 일정으로 작성자를 바꾼 수정은 받지 않는다
forged = core.export_state(lee)
for r in forged["items"]:
    if r["text"] == "행사 준비 회의":
        r["au"], r["text"], r["updated"] = "u_lee", "바꿔치기", r["updated"] + 5000
assert not core.merge_state(kim, forged) or all(t["text"] != "바꿔치기" for t in kim["2026-11-10"]["todos"])
# 권한
assert team.can_edit(a, "u_kim", True) and not team.can_edit(a, "u_lee", True) and team.can_edit(a, "u_lee", False)
assert team.can_edit({"text": "예전 일정"}, "u_lee", True)                    # 작성자 없는 예전 일정은 누구나
assert team.pick_color(kim) not in ("#ff6b81",)                              # 이미 쓴 색은 피한다
# 예전 일정을 내 것으로
old = {"2026-11-11": {"memo": "", "todos": [{"id": "x", "text": "예전", "updated": 1}]}}
assert team.claim_legacy(old, "u_kim") == 1 and old["2026-11-11"]["todos"][0]["au"] == "u_kim"

# ── 화면 쪽: 설정, 색, 수정 잠금 ──
import tkinter as tk
import desk_calendar as dc
root = tk.Tk(); app = dc.DeskCalendar(root); root.update(); time.sleep(0.3); root.update()
app.data.update({"2026-11-10": {"memo": "", "todos": [dict(a), dict(b)]}})
app.data["_team"] = {"u_kim": {"name": "김주무관", "color": "#ff6b81", "updated": 1},
                     "u_lee": {"name": "이주무관", "color": "#54a0ff", "updated": 1}}
app.conf["team"] = {"on": True, "uid": "u_lee", "name": "이주무관", "color": "#54a0ff"}
app._team_apply()
mine = [t for t in app.data["2026-11-10"]["todos"] if t["au"] == "u_lee"][0]
theirs = [t for t in app.data["2026-11-10"]["todos"] if t["au"] == "u_kim"][0]
assert app.team_on() and app.author_color(theirs) == "#ff6b81" and dc.item_color(mine) == "#54a0ff"
assert app.author_name(theirs) == "김주무관"
assert app.can_edit(mine, warn=False) and not app.can_edit(theirs, warn=False)
before = dict(theirs)
app.toggle_todo(theirs); app.set_pri(theirs, 3); app.delete_todo(None, theirs)
app.move_item(theirs, dt.date(2026, 11, 10), dt.date(2026, 11, 12))
assert theirs in app.data["2026-11-10"]["todos"] and theirs == before, "남의 일정이 바뀌었다"
app.toggle_todo(mine); assert mine["done"]                                   # 내 일정은 된다
app.delete_todo(None, mine); assert mine not in app.data["2026-11-10"]["todos"]
# 새로 쓰면 내 이름이 붙는다 (빠른 입력)
app.quick_add_anywhere(); root.update()
app.cmdbar.entry.insert(0, "회의록 정리 261112 중요")
app.cmdbar.refresh(); app.cmdbar.run(); root.update()
n = [t for t in app.data["2026-11-12"]["todos"] if t["text"] == "회의록 정리"][0]
assert n["au"] == "u_lee" and n["pri"] == 1
# 달력·상세 창·말풍선이 오류 없이 그려진다
app.render_month(); app.open_detail(dt.date(2026, 11, 10)); root.update()
app.detail.top.destroy()
# 부서 공유 설정 창
dc.TeamDialog(app); root.update()
# 공유를 끄면 예전처럼 누구나
app.conf["team"]["on"] = False; app._team_apply()
assert app.can_edit(theirs, warn=False) and not app.author_color(theirs)
print("TEAM OK")
