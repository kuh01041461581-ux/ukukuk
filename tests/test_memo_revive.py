"""메모를 지우면 다른 기기의 옛 메모가 되살아나지 않는다. 실행: python tests/test_memo_revive.py"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import core

pc = {"2026-10-20": {"memo": "옛 메모", "memo_ts": 1000, "todos": []}}
phone = {"2026-10-20": {"memo": "옛 메모", "memo_ts": 1000, "todos": []}}
# PC 에서 메모를 지움 → 기록(ts)은 남고 글자만 빈다
pc["2026-10-20"].update(memo="", memo_ts=2000)
out = core.export_state(pc)
assert out["memos"]["2026-10-20"] == {"memo": "", "ts": 2000}, out["memos"]
# 폰이 받으면 폰 메모도 지워진다
assert core.merge_state(phone, out) and phone["2026-10-20"]["memo"] == ""
# 폰이 다시 PC 로 보내도 되살아나지 않는다
back = core.export_state(phone)
core.merge_state(pc, back)
assert pc["2026-10-20"]["memo"] == "", pc
# 한 번도 쓴 적 없는 날은 보내지 않는다
assert core.export_state({"2026-10-21": {"memo": "", "todos": []}})["memos"] == {}
print("MEMO REVIVE OK")
