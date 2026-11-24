#!/usr/bin/env python3
"""지급명세서 양식(사용자가 준 엑셀)에서 이름·계좌·금액을 지우고 서식(글꼴·테두리·병합·열 너비)만 남긴 템플릿을 만든다.
   python3 seomu/tests/make_template.py <원본.xlsx>   -> seomu/shared/wage_template.xlsx
템플릿에는 개인정보가 없다(근로자·계좌·금액 전부 삭제). 생성기는 이 서식을 복제해서 값만 채운다."""
import sys, openpyxl, pathlib
src = sys.argv[1]
wb = openpyxl.load_workbook(src)
for n in list(wb.sheetnames):
    if n.strip() in ("Sheet1", "김향은(주말)"):
        del wb[n]
ws1, ws2, ws3 = wb.worksheets
ws1.title, ws2.title, ws3.title = "임금지급 명세서", "은행이체(개인별)", "급여지급 명세서"

def clear(ws, min_row=1, keep=lambda c: False):
    for row in ws.iter_rows(min_row=min_row):
        for c in row:
            if type(c).__name__ == "MergedCell":
                continue
            if c.value is not None and not keep(c):
                c.value = None

# 임금지급 명세서: 머리글(1~7행)의 글자는 그대로, 숫자·수식·이름은 지운다
def keep1(c):
    return c.row <= 4 or c.coordinate in ("C5", "C6", "C7", "S5", "S6", "S7", "T5", "U5", "V5", "W5", "W2") or (c.column_letter == "C" and c.row >= 8)
clear(ws1, 1, keep1)
for coord in ("B1", "X2", "A16", "A18", "B14"):
    ws1[coord].value = None
# 은행이체: 머리글 6~7행 글자만 남기고 전부 삭제
clear(ws2, 1, lambda c: c.row in (6, 7) and c.coordinate not in ("B7", "D7"))
# 개인별: 항목 이름(A/E열, 머리글)만 남기고 값은 삭제
clear(ws3, 1, lambda c: c.column_letter in ("A", "E") or c.coordinate in ("B8", "B9", "C9", "D8"))
for c in ("A3", "A4", "A5", "A6"):
    pass
out = pathlib.Path(__file__).resolve().parent.parent / "shared" / "wage_template.xlsx"
wb.save(out)
print("saved", out, out.stat().st_size)
