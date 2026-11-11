#!/usr/bin/env python3
"""엑셀(공공요금·일상경비)에서 초기 데이터를 읽는 함수 모음. exe 안에서도 쓰입니다.
"""
import argparse, json, re, sys
from pathlib import Path
import openpyxl


# 일상경비 행 정의: (그룹, 엑셀 사업명) -> (예산서 세부사업명, 편성목코드(5자리))
DAILY_MAP = {
    ("사무관리비", "문화유적지 보존관리"): ("문화유적지 보존관리", "20101"),
    ("사무관리비", "문화유적지 정비"): ("문화유적지 정비", "20101"),
    ("사무관리비", "기본경비"): ("기본경비", "20101"),
    ("사무관리비", "익산고도육성사업"): ("익산고도육성사업", "20101"),
    ("사무관리비", "백제왕궁 편의시설 운영"): ("백제왕궁 편의시설 운영", "20101"),
    ("사무관리비", "세계유산 보존관리 사업"): ("세계유산 보존관리 사업", "20101"),
    ("사무관리비", "세계유산 활용 및 홍보"): ("세계유산 활용 및 홍보 사업", "20101"),
    ("사무관리비", "통합전수교육관운영"): ("국가무형유산 통합전수교육관 운영", "20101"),
    ("공공운영비", "문화유적지 보존관리"): ("문화유적지 보존관리", "20102"),
    ("공공운영비", "문화유적지 정비"): ("문화유적지 정비", "20102"),
    ("공공운영비", "익산고도육성사업"): ("익산고도육성사업", "20102"),
    ("공공운영비", "기본경비"): ("기본경비", "20102"),
    ("업무추진비", "시책추진업무추진비"): ("국가유산 운영", "20303"),
    ("업무추진비", "부서운영업무추진비"): ("기본경비", "20304"),
    ("여비", "국내여비"): ("국가유산 운영", "20201"),
}
GROUPS = ["사무관리비", "공공운영비", "업무추진비", "여비"]
# 엑셀 비고에 '예정'이라고 적힌 금액만 지급예정으로 옮김(나머지는 비고 글로 보존)
SEED_SCHEDULED = {
    ("사무관리비", "문화유적지 보존관리"): [("남중마트 청소용품(미납분 처리 예정)", 600000)],
    ("사무관리비", "통합전수교육관운영"): [("일직수당", 1980000)],
}


def num(v):
    if isinstance(v, bool):
        return None
    if isinstance(v, (int, float)):
        return int(round(v))
    return None


def txt(v):
    return "" if v is None else str(v).replace("\r", "").strip()


def find_daily_sheet(wb):
    if "사무관리비" in wb.sheetnames:
        return wb["사무관리비"]
    for ws in wb.worksheets:
        a1 = ws["A1"].value
        if isinstance(a1, str) and "집행현황" in a1:
            return ws
    return None


def parse_daily(path):
    wb = openpyxl.load_workbook(path, data_only=False)
    ws = find_daily_sheet(wb)
    if ws is None:
        raise ValueError("일상경비 표('사무관리비' 탭)를 찾지 못했습니다.")
    lines, group, seq = [], None, 0
    for r in range(4, ws.max_row + 1):
        a, b = ws.cell(r, 1).value, ws.cell(r, 2).value
        if isinstance(a, str) and a.strip() in GROUPS:
            group = a.strip()
            continue
        if group is None or not isinstance(a, (int, float)) or not b:
            continue
        label = txt(b)
        key = (group, label)
        if key not in DAILY_MAP:
            print("  [경고] 매핑 없는 행:", key, file=sys.stderr)
            biz, code = label, ""
        else:
            biz, code = DAILY_MAP[key]
        note = txt(ws.cell(r, 8).value)
        extra = ws.cell(r, 9).value
        if extra not in (None, ""):
            note += (" / " if note else "") + ("엑셀 I열 메모: " + (f"{extra:,}" if isinstance(extra, (int, float)) else txt(extra)))
        seq += 1
        lines.append({
            "id": f"L{seq}", "group": group, "label": label, "biz": biz, "code": code,
            "budget": num(ws.cell(r, 3).value) or 0, "spent": num(ws.cell(r, 5).value) or 0,
            "note": note,
            "scheduled": [{"id": f"S{seq}_{i}", "text": t, "amount": amt}
                          for i, (t, amt) in enumerate(SEED_SCHEDULED.get(key, []))],
            "ehojo": None,
        })
    asof_raw = txt(ws["A2"].value)
    m = re.match(r"(\d{4})\.(\d{1,2})\.(\d{1,2})", asof_raw)
    asof = f"{m.group(1)}-{int(m.group(2)):02d}-{int(m.group(3)):02d}" if m else "2026-11-06"
    return lines, txt(ws["H2"].value), asof


UTIL_DEF = [
    # key, title, 제목행 키워드, no1 라벨, no2 라벨
    ("elec", "전기요금", "전기요금", "계약번호", "납부번호(지로·가상계좌)"),
    ("water", "상하수도요금", "상하수도요금", "수전번호", "납부번호(지로·가상계좌)"),
    ("tel", "통신요금(U-안전관리시스템)", "통신요금", "고객번호", "납부계좌(농협)"),
]


def find_blocks(ws):
    blocks = {}
    for r in range(1, ws.max_row + 1):
        a = ws.cell(r, 1).value
        if isinstance(a, str) and "년" in a and "요금" in a:
            for key, _, kw, *_ in UTIL_DEF:
                if kw in a:
                    blocks[key] = r
    return blocks


def norm_key(s):
    return re.sub(r"[\s\n]", "", txt(s))


def months_of(ws, r):
    out = []
    for c in range(5, 17):  # E..P
        out.append(num(ws.cell(r, c).value))
    return out


def parse_util(path, year=2026):
    wb = openpyxl.load_workbook(path)
    ys = str(year)
    if ys not in wb.sheetnames:
        raise ValueError(f"엑셀에 '{ys}' 탭이 없습니다. (탭 이름: {', '.join(wb.sheetnames[:8])})")
    ws = wb[ys]
    w25 = wb[str(year - 1)] if str(year - 1) in wb.sheetnames else None
    b26 = find_blocks(ws)
    b25 = find_blocks(w25) if w25 is not None else {}
    if not b26:
        raise ValueError(f"'{ys}' 탭에서 전기·상하수도·통신 표를 찾지 못했습니다.")
    # 전달처 안내표(T:V 열, 제목행 '관리항목')
    sections, infos = {}, {}
    ends = sorted(b26.values()) + [ws.max_row + 1]
    for key, title, kw, l1, l2 in UTIL_DEF:
        start = b26[key]
        end = min(x for x in ends if x > start)
        # 항목 영역: 제목+5 (제목, 단위, 헤더, 합계 다음) ~ end
        first = start + 4
        items, forwards, tips = [], [], []
        # 안내표 파싱
        for r in range(start, end):
            t = ws.cell(r, 20).value
            if isinstance(t, str) and t.strip() == "관리항목":
                continue
            if t not in (None, "") and r >= first - 1:
                forwards.append({
                    "name": txt(t).replace("\n", " "),
                    "numbers": txt(ws.cell(r, 21).value).replace("\n", " / "),
                    "to": txt(ws.cell(r, 22).value).replace("\n", " "),
                    "memo": txt(ws.cell(r, 23).value) or ("" if ws.cell(r, 19).value in (None, "") else ""),
                })
        # 25 시트 매칭용 맵
        py_map = {}
        if key in b25 and w25 is not None:
            s25 = b25[key]
            e25 = min([x for x in sorted(b25.values()) if x > s25] + [w25.max_row + 1])
            for r in range(s25 + 4, e25):
                if key == "tel":
                    k = norm_key(w25.cell(r, 3).value)
                else:
                    k = norm_key(w25.cell(r, 2).value)
                if k and any(isinstance(w25.cell(r, c).value, (int, float)) for c in range(5, 17)):
                    py_map[k] = months_of(w25, r)
        for r in range(first, end):
            a = ws.cell(r, 1).value
            b = ws.cell(r, 2).value
            c = ws.cell(r, 3).value
            if a is None and b is None:
                continue
            nm = txt(a)
            if key != "tel" and (nm.startswith("한국전력") or (b and "안내" in txt(b))):
                tips.append((nm + " " + txt(b)).strip())
                continue
            if key == "elec" and a is None and "2000원" in txt(ws.cell(r, 14).value):
                continue
            if key != "tel" and not nm and not txt(b):
                continue
            if key == "tel":
                blines = [x.strip() for x in txt(b).split("\n") if x.strip()]
                carrier = blines[0] if blines else ""
                no1 = next((x for x in reversed(blines[1:]) if re.fullmatch(r"[\d-]+", x)), "")
                bmemo = " ".join(x for x in blines[1:] if x != no1)
                nlines = [x.strip() for x in nm.split("\n")
                          if x.strip() and not x.strip().startswith("농협") and not re.fullmatch(r"[\d-]+", x.strip())]
                name = "\n".join(nlines)
                no2 = txt(c)
            else:
                name, no1, no2, bmemo, carrier = nm, txt(b), txt(c), "", ""
            amts = months_of(ws, r)
            dec = num(ws.cell(r, 4).value)
            memo_parts = [bmemo] if bmemo else []
            if txt(ws.cell(r, 18).value):
                memo_parts.append(txt(ws.cell(r, 18).value).replace("\n", " "))
            for cc in range(5, 17):
                v = ws.cell(r, cc).value
                if isinstance(v, str) and v.strip():
                    memo_parts.append(v.strip().replace("\n", " "))
            memo = " · ".join(memo_parts)
            k = norm_key(c) if key == "tel" else norm_key(b)
            py = py_map.get(k)
            items.append({
                "id": f"{key}{r}", "name": name, "no1": no1, "no2": no2, "carrier": carrier,
                "memo": memo, "inactive": False, "dec": dec, "amt": amts, "py": py,
                "forward": "",
            })
        # 안내표에서 번호 일치 → 전달처 표시 / 이름 없는 통신 항목 보강
        for it in items:
            for f in forwards:
                nums = [x.strip() for x in f["numbers"].split("/")]
                cand = [it["no1"], it["no2"]]
                cand = [re.sub(r"^농협\s*", "", x) for x in cand if x]
                if any(any(cn and cn in n for n in nums) for cn in cand):
                    it["forward"] = f["to"]
                    if key == "tel" and not it["name"]:
                        it["name"] = f["name"]
        for it in items:
            if not it["name"]:
                it["name"] = "(명칭 미입력) " + (it["no2"] or it["no1"])
        # 수령 상태 결정
        last = 0
        for it in items:
            for i, v in enumerate(it["amt"]):
                if v:
                    last = max(last, i + 1)
        for it in items:
            has_any = any(v for v in it["amt"])
            if not has_any:
                it["inactive"] = True
                it["memo"] = (it["memo"] + " · " if it["memo"] else "") + "2026년 청구 내역 없음(미사용 처리)"
            if "사용자 납부" in it["memo"]:
                it["inactive"] = True
            it["rcv"] = []
            for i in range(12):
                v = it["amt"][i]
                if v:
                    it["rcv"].append(1)
                elif i < last:
                    it["rcv"].append(2)  # 지난 달 빈칸 = 청구 없음/합산으로 처리
                else:
                    it["rcv"].append(0)
            if it["inactive"]:
                it["rcv"] = [2 if x == 0 else x for x in it["rcv"]]
        sections[key] = {
            "key": key, "title": title, "no1Label": l1, "no2Label": l2,
            "items": items, "forwards": forwards, "tips": tips, "budget": None,
        }
    return sections


