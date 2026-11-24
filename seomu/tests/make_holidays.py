#!/usr/bin/env python3
"""저장소의 holidays_kr.py 규칙으로 2024~2030 공휴일 표를 만들어 seomu/shared/holidays.js 에 쓴다."""
import json, pathlib, sys
root = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root))
import holidays_kr
out = {}
for y in range(2024, 2031):
    for d, n in sorted(holidays_kr.holidays(y).items()):
        out[d.isoformat()] = n
js = "/* 공휴일(자동 생성: seomu/tests/make_holidays.py). 임시공휴일은 알려진 것만 */\nvar SEOMU_HOLIDAYS = " + json.dumps(out, ensure_ascii=False) + ";\n"
(root / "seomu/shared/holidays.js").write_text(js, encoding="utf-8")
print(len(out), "days")
