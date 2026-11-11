"""저장·백업·엑셀 읽기 기본 동작 시험 (창 없이). 실행: python expense-tracker/tests/test_api.py"""
import json, sys, tempfile
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import openpyxl
import app

d = Path(tempfile.mkdtemp())
a = app.Api(d)
assert a.load_state() == ""
assert a.save_state('{"a":1}')["ok"] and a.save_state('{"a":2}')["ok"]
assert not a.save_state("깨진 내용")["ok"]
assert json.loads(a.load_state()) == {"a": 2}
assert list((d / "backups").glob("data-*.json"))

wb = openpyxl.Workbook(); ws = wb.active; ws.title = "사무관리비"
ws["A2"] = "2026.11.06.기준"
ws["A4"] = "사무관리비"
ws["A5"], ws["B5"], ws["C5"], ws["E5"], ws["H5"] = 1, "문화유적지 보존관리", 18800000, 17404350, "비고"
x = d / "t.xlsx"; wb.save(x)
import base64
r = app.Api(d).import_xlsx_data("t.xlsx", base64.b64encode(x.read_bytes()).decode(), 2026)
assert "util" not in r and r["lines"][0]["budget"] == 18800000 and r["lines"][0]["spent"] == 17404350 and r["asOf"] == "2026-11-06"
bad = app.Api(d).import_xlsx_data("t.xlsx", base64.b64encode(b"not excel").decode(), 2026)
assert "error" in bad
print("OK")
