#!/usr/bin/env python3
"""소득세법 시행령 [별표 2] 근로소득 간이세액표(law.go.kr 에서 받은 PDF)를 읽어 seomu/shared/taxtable.js 를 만든다.
   python3 seomu/tests/make_taxtable.py <별표2.pdf 에서 pdftotext -layout 한 txt>"""
import re, sys, json, pathlib
rows = []
for line in open(sys.argv[1], encoding="utf-8"):
    t = line.split()
    if len(t) != 13: continue
    if not re.fullmatch(r"[\d,]+", t[0]) or not re.fullmatch(r"[\d,]+", t[1]): continue
    if not all(re.fullmatch(r"[\d,]+|-", x) for x in t[2:]): continue
    lo, hi = int(t[0].replace(",", "")), int(t[1].replace(",", ""))
    rows.append([lo, hi] + [0 if x == "-" else int(x.replace(",", "")) for x in t[2:]])
rows.sort()
# 구간이 빠짐없이 이어지는지
for a, b in zip(rows, rows[1:]):
    assert a[1] == b[0], ("구간 끊김", a[:2], b[:2])
print(len(rows), "구간", rows[0][:2], "~", rows[-1][:2])
js = "/* 근로소득 간이세액표 — 소득세법 시행령 [별표 2] (2023.2.28. 개정), 단위 원, 월급여액 천원 구간 [이상, 미만, 가족1명..11명]\n   자동 생성: seomu/tests/make_taxtable.py (원문 PDF: law.go.kr) */\nvar SEOMU_TAXTABLE = {ver:'소득세법 시행령 별표 2 (2023.2.28. 개정)', rows:" + json.dumps(rows, separators=(",", ":")) + "};\n"
out = pathlib.Path(__file__).resolve().parent.parent / "shared" / "taxtable.js"
out.write_text(js, encoding="utf-8"); print(out, out.stat().st_size)
