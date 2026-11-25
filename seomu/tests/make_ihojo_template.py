#!/usr/bin/env python3
"""이호조 거래처입력 양식에서 입력된 예시 줄(2~9행)의 값만 지우고 서식·입력 검증·은행코드 시트는 그대로 둔 템플릿을 만든다.
   python3 -I seomu/tests/make_ihojo_template.py <원본.xlsx>   -> seomu/shared/ihojo_template.xlsx"""
import sys, openpyxl, pathlib
wb = openpyxl.load_workbook(sys.argv[1])
ws = wb["거래처입력"]
for row in ws.iter_rows(min_row=2, max_row=ws.max_row):
    for c in row:
        c.value = None
out = pathlib.Path(__file__).resolve().parent.parent / "shared" / "ihojo_template.xlsx"
wb.save(out); print("saved", out, out.stat().st_size)
