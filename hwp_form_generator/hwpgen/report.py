# -*- coding: utf-8 -*-
"""보고서류 양식(국장·시장님 보고, 행사 계획 등)의 등록과 생성.

양식 등록(register_report): 사용자가 준 샘플 .hwpx 한 개에서
  1) 바뀌는 칸(제목·보고일·담당자 …)을 {{키}} 자리표시로 바꾸고
  2) 본문(□ 제목, ◦ 항목, 표)을 지워 {{BODY}} 한 줄로 두며
  3) 본문의 모양(□ 줄, ◦ 줄, 표 줄 …)을 '견본 문단'(protos.xml)으로 따로 보관한다.
생성(ReportForm.render): 자리표시를 값으로 채우고, {{BODY}} 자리에 입력한 개요를 견본 문단 서식 그대로 찍어 넣는다.
→ 글꼴·글자 크기·자간·들여쓰기·표 테두리는 모두 사용자 샘플의 서식 번호를 재사용하므로 원본과 똑같다.
"""
import collections
import json
import os
import re

from lxml import etree

from . import package as P
from .package import HP, Package, clone, local, own_text, ptext
from . import textfmt as T

BODY_MARK = "{{BODY}}"
_PH = re.compile(r"\{\{(\w+)\}\}")


# ------------------------------------------------------------------ 분석
def _has_obj(p):
    for run in p.findall(HP + "run"):
        for c in run:
            if local(c) in ("container", "pic", "rect"):
                return True
    return False


def _has_tbl(p):
    return any(local(c) == "tbl" for run in p.findall(HP + "run") for c in run)


def is_heading_like(p):
    t = own_text(p).strip()
    if not t:
        return False
    if t[0] in T.HEAD_MARKS:
        return True
    return _has_obj(p) and len(t) <= 30 and not _has_tbl(p)


def candidate_lists(section):
    """본문이 들어 있을 수 있는 문단 목록: 맨 바깥 + 맨 바깥 표 칸 안."""
    out = [section]
    for p in section.findall(HP + "p"):
        for run in p.findall(HP + "run"):
            for tbl in run.findall(HP + "tbl"):
                for tc in tbl.iter(HP + "tc"):
                    sub = tc.find(HP + "subList")
                    if sub is not None:
                        out.append(sub)
    return out


def detect_body(section):
    """본문 문단 범위를 찾는다 → (문단 목록 element, 시작, 끝(미포함)) / 못 찾으면 None."""
    best = None
    for lst in candidate_lists(section):
        ps = lst.findall(HP + "p")
        marks = [i for i, p in enumerate(ps) if is_heading_like(p)]
        if len(marks) < 1:
            continue
        score = len(marks)
        if best is None or score > best[0]:
            best = (score, lst, marks[0], len(ps))
    if not best:
        return None
    return best[1], best[2], best[3]


def classify(p):
    t = own_text(p)
    s = t.strip()
    if _has_tbl(p):
        return "table"
    if not s:
        return "blank"
    if is_heading_like(p):
        return "h1"
    if s[0] in T.BULLETS:
        if re.match(r"^[%s]\s*(\([^()]{1,12}\)|[^\s:：()][^:：]{0,11}?\s*[:：])" % re.escape(T.BULLETS), s):
            return "kv"
        return "item1"
    if s[0] in T.SUB_MARKS:
        return "item2"
    if s[0] in T.DASH_MARKS:
        return "item3"
    return "other"


def cp_info(pkg):
    """charPr id → (글꼴 묶음, 크기, 굵게, 자간)"""
    from .package import HH
    out = {}
    for c in pkg.header().iter(HH + "charPr"):
        fr = c.find(HH + "fontRef")
        sp = c.find(HH + "spacing")
        out[c.get("id")] = (tuple(sorted(fr.attrib.items())), c.get("height"), c.find(HH + "bold") is not None,
                            int(sp.get("hangul", "0")) if sp is not None else 0)
    return out


def typical_cp(info, cps):
    """본문 글자 모양으로 쓸 '대표' 글자 모양.
    같은 글꼴·크기끼리 묶어 가장 많은 묶음에서 고르고(굵게는 강조라 제외), 자간은 여러 줄에서 같은 값이면 그 값,
    줄마다 제각각이면 가장 덜 줄인 값을 쓴다 (샘플은 줄이 넘치지 않게 문단마다 자간을 손으로 줄여 둔 것이라
    그중 하나를 모든 줄에 쓰면 글자가 옹색해진다)."""
    cps = [c for c in cps if c and c in info]
    if not cps:
        return None
    plain = [c for c in cps if not info[c][2]] or cps
    key = lambda c: info[c][:2]                                    # noqa: E731
    most = collections.Counter(key(c) for c in plain).most_common(1)[0][0]
    group = [c for c in plain if key(c) == most]
    counts = collections.Counter(info[c][3] for c in group).most_common()
    sp = counts[0][0] if counts[0][1] >= 2 else max(info[c][3] for c in group)
    return next(c for c in group if info[c][3] == sp)


def _kv_roles(p):
    """◦ 이름 : 값 줄의 글자 모양 역할(글머리·이름·값)을 추정한다.
    값 = 이름 뒤 조각 중 글자가 가장 많은 조각(끝에 붙은 짧은 조각의 모양에 끌려가지 않게)"""
    runs = [(r.get("charPrIDRef"), "".join(t.text or "" for t in r.findall(HP + "t"))) for r in p.findall(HP + "run")]
    runs = [(c, t) for c, t in runs if t != ""]
    if not runs:
        return None
    full = "".join(t for _, t in runs)
    paren = bool(re.match(r"^\s*[%s]\s*\(" % re.escape(T.BULLETS), full))
    bullet_cp = runs[0][0]
    if len(runs) == 1:
        return {"fmt": "paren" if paren else "colon", "bullet": bullet_cp, "label": bullet_cp, "gap": bullet_cp,
                "value": bullet_cp}
    label_cp = gap_cp = None
    label_i = 0                               # 이름이 들어 있는 조각 번호(글머리와 한 조각이면 0)
    if paren:
        for i, (c, t) in enumerate(runs):
            if "(" in t and label_cp is None and not (i == 0 and not re.search(r"\(", t)):
                label_cp, label_i = c, i
            elif label_cp is None and not t.strip():
                gap_cp = c
    else:
        for i, (c, t) in enumerate(runs[1:], 1):
            if label_cp is None and t.strip() and ":" not in t and "：" not in t:
                label_cp, label_i = c, i
            elif ":" in t or "：" in t:
                break
    label_cp = label_cp or bullet_cp
    rest = [(c, t) for c, t in runs[label_i + 1:] if t.strip() and t.strip() not in (":", "：")]
    if not rest:
        rest = [(c, t) for c, t in runs[1:] if t.strip()] or runs[-1:]
    value_cp = max(rest, key=lambda x: len(x[1].strip()))[0]
    return {"fmt": "paren" if paren else "colon", "bullet": bullet_cp, "label": label_cp,
            "gap": gap_cp or label_cp, "value": value_cp}


def _kv_label(p):
    s = own_text(p)
    m = re.match(r"^\s*[%s]\s*\(([^()]+)\)" % re.escape(T.BULLETS), s) or \
        re.match(r"^\s*[%s]\s*([^:：]+?)\s*[:：]" % re.escape(T.BULLETS), s)
    return re.sub(r"\s", "", m.group(1)) if m else None


def _prefix_of(p):
    """글머리 앞부분(들여쓰기+기호)과 기호 뒤 공백 유무"""
    s = own_text(p)
    m = re.match(r"^(\s*[%s])(\s?)" % re.escape(T.BULLETS + T.SUB_MARKS + T.DASH_MARKS), s)
    return (m.group(1), m.group(2)) if m else ("", " ")


def _row_info(tr):
    cells = tr.findall(HP + "tc")
    spans = []
    simple = True
    for tc in cells:
        sp = tc.find(HP + "cellSpan")
        spans.append(int(sp.get("colSpan", "1")))
        if int(sp.get("rowSpan", "1")) > 1:
            simple = False
    return cells, spans, simple


def dominant_cp(tc):
    """칸 안에서 글자가 가장 많은 조각의 글자 모양 번호 ('[발표 1]' 같은 짧은 굵은 태그보다 본 글자를 따른다)"""
    best, best_len = None, 0
    for run in tc.iter(HP + "run"):
        n = len("".join(t.text or "" for t in run.findall(HP + "t")).strip())
        if n > best_len:
            best, best_len = run.get("charPrIDRef"), n
    return best


def _row_cps(tr):
    return [dominant_cp(tc) for tc in tr.findall(HP + "tc")]


def extract_table_protos(tbl):
    """표 하나에서 '행 견본'을 뽑는다. 병합 모양(칸 수·합친 칸)별로 처음/중간/끝 줄을 따로 둔다.
    중간 줄은 같은 모양끼리 '가장 흔한 글자 모양'을 가진 줄을 골라, 줄마다 손으로 자간을 조정한 줄이 견본이 되지 않게 한다."""
    col_cnt = int(tbl.get("colCnt"))
    trs = tbl.findall(HP + "tr")
    n = len(trs)
    cand = []
    for i, tr in enumerate(trs):
        cells, spans, simple = _row_info(tr)
        if not simple or sum(spans) != col_cnt:
            continue
        kind = "first" if i == 0 else "last" if i == n - 1 else "mid"
        cand.append({"i": i, "kind": kind, "spans": spans, "tr": tr, "cps": tuple(_row_cps(tr))})
    mids = collections.defaultdict(list)
    keep = []
    for row in cand:
        (mids[tuple(row["spans"])] if row["kind"] == "mid" else keep).append(row)
    for group in mids.values():
        common = collections.Counter(x["cps"] for x in group).most_common(1)[0][0]
        keep.append(next(x for x in group if x["cps"] == common))
    keep.sort(key=lambda x: x["i"])
    return {"cols": col_cnt, "rows": keep}


# ------------------------------------------------------------------ 등록
def frame_texts(section, body):
    """본문 밖에 남은 글자 목록 (번호, 글자) — 입력 칸으로 지정할 후보."""
    lst, a, b = body if body else (None, 0, 0)
    skip = set()
    if lst is not None:
        for p in lst.findall(HP + "p")[a:b]:
            for t in p.iter(HP + "t"):
                skip.add(t)
    out = []
    for i, t in enumerate(section.iter(HP + "t")):
        if t in skip:
            continue
        if (t.text or "").strip():
            out.append((i, t.text))
    return out


def _empty_like(p):
    """글자를 비운 같은 모양의 문단. 제목 앞 네모 같은 꾸밈 도형(container)은 남기고, 표만 뺀다."""
    q = clone(p)
    for t in q.iter(HP + "t"):
        t.text = ""
    for el in list(q.iter(HP + "tbl")):
        if el.getparent() is not None:
            el.getparent().remove(el)
    P.strip_lineseg(q)
    return q


def sanitize_package(pkg, keep_sections=("Contents/section0.xml",), extra_roots=()):
    """개인 정보가 새지 않게: 미리보기 글·그림 제거, 쓰지 않는 그림은 작은 빈 그림으로 바꾼다, 문서 정보 비움."""
    for n in list(pkg.files):
        if n.startswith("Preview/"):
            del pkg.files[n]
    # 쓰지 않는 구역 제거
    for n in pkg.section_names():
        if n not in keep_sections:
            pkg.files.pop(n, None)
            pkg._xml.pop(n, None)
    hpf = pkg.files.get("Contents/content.hpf")
    if hpf is not None:
        root = P.parse_xml(hpf)
        for item in list(root.iter(P.OPF + "item")):
            href = item.get("href", "")
            if href.startswith("Contents/section") and href not in keep_sections:
                item.getparent().remove(item)
        for ref in list(root.iter(P.OPF + "itemref")):
            rid = ref.get("idref", "")
            if re.fullmatch(r"section\d+", rid) and "Contents/%s.xml" % rid not in keep_sections:
                ref.getparent().remove(ref)
        for m in root.iter(P.OPF + "meta"):
            if m.get("name") in ("creator", "lastsaveby", "subject", "description", "keyword"):
                m.text = ""
            if m.get("name") in ("CreatedDate", "ModifiedDate", "date"):
                m.text = ""
        pkg.files["Contents/content.hpf"] = P.dump_xml(root)
    # 그림: 양식이 실제로 쓰는 것만 남기고 나머지는 1×1 빈 그림.
    # '쓴다' = 본문 + 서식 정의(header.xml 의 배경 그림 등) + 마스터쪽 + 따로 보관하는 견본(표 줄 등) 어디서든 가리키는 것
    used = set()
    roots = [pkg.xml(n) for n in pkg.section_names()] + list(extra_roots)
    roots.append(pkg.header())
    for n in list(pkg.files):
        if re.fullmatch(r"Contents/masterpage\d+\.xml", n):
            roots.append(pkg.xml(n))
    for root in roots:
        for el in root.iter():
            v = el.get("binaryItemIDRef")
            if v:
                used.add(v)
    from . import tinyimg
    for n in list(pkg.files):
        if n.startswith("BinData/"):
            stem = os.path.splitext(os.path.basename(n))[0]
            if stem not in used:
                pkg.files[n] = tinyimg.blank_like(n)
    return pkg


def flatten_mixed(section):
    """<t>2026.<fwSpace/> 12. 9.</t> 처럼 고정폭 공백이 끼어 글자가 갈라진 칸을 한 덩어리 글자로 합친다."""
    for t in section.iter(HP + "t"):
        if len(t) and all(local(c) in ("fwSpace", "nbSpace") for c in t):
            s = t.text or ""
            for c in list(t):
                s += ("\u3000" if local(c) == "fwSpace" else "\u00a0") + (c.tail or "")
                t.remove(c)
            t.text = s


def _blank_tables(section, blank_tables):
    """{문단번호: 남길 줄 수} — 그 문단의 표에서 앞의 N줄(표 머리)만 남기고 나머지 칸의 글자를 비운다(칸 모양은 그대로)."""
    ps = section.findall(HP + "p")
    for idx, keep in (blank_tables or {}).items():
        tbl = next(ps[int(idx)].iter(HP + "tbl"))
        for tr in tbl.findall(HP + "tr")[keep:]:
            for t in tr.iter(HP + "t"):
                t.text = ""
            for ctrl in list(tr.iter(HP + "ctrl")):          # 합계 칸의 숨은 계산식(=sum(…) 과 계산 결과값)도 지운다
                if ctrl.find(HP + "fieldBegin") is not None or ctrl.find(HP + "fieldEnd") is not None:
                    ctrl.getparent().remove(ctrl)


def _cut_section(pkg, section_index, slice_, drop, blank_tables=None):
    """문서의 한 구역에서 문단 [a, b) 만 남긴다(구역 설정 문단은 앞에 붙여 둠). 남긴 구역은 section0 자리로 옮긴다."""
    names = pkg.section_names()
    section = pkg.xml(names[section_index])
    if section_index != 0:
        pkg._xml["Contents/section0.xml"] = section
        pkg.files["Contents/section0.xml"] = b""
    _blank_tables(section, blank_tables)
    if slice_ is None and not drop:
        return section
    ps = section.findall(HP + "p")
    a, b = slice_ if slice_ else (0, len(ps))
    keep = [ps[i] for i in range(a, min(b, len(ps))) if i not in set(drop or ())]
    carrier = None
    if a > 0 or section_index != 0 and a > 0:
        carrier = clone(ps[0])
        for run in carrier.findall(HP + "run"):
            if any(local(c) == "secPr" for c in run):
                for c in list(run):
                    if local(c) in ("tbl", "pic", "container", "t"):
                        run.remove(c)
            else:
                carrier.remove(run)
        P.strip_lineseg(carrier)
    for p in ps:
        section.remove(p)
    if carrier is not None:
        section.append(carrier)
    for p in keep:
        section.append(p)
    return section


def register_report(sample_path, out_dir, form_id, name, audience, fields, kind="report", note="", starter="",
                    slice_=None, drop=None, section_index=0, allow_no_body=False, blank_tables=None, fix=None,
                    body_from=None, plain_body=False, body_from_text=None, body_tail=0, frame_only=False):
    """샘플 hwpx → 양식 폴더(form.json, template.hwpx, protos.xml).

    fields: [{"key","label","sample"(샘플에 들어 있던 글자),"repl"(선택: 바꿀 글자, {{키}} 포함),
              "type": text|date|multiline, "fmt":..., "profile": 키}]
    slice_=(a,b): 문서가 여러 쪽일 때 그 중 문단 a~b 만 양식으로 / drop: 뺄 문단 번호 / section_index: 몇 번째 구역
    allow_no_body: 본문(□ 제목) 없이 틀만 있는 양식(표지 등)도 허용
    반환: dict(결과 요약, 'leftover': 입력 칸으로 지정되지 않고 고정 글자로 남은 것들)
    """
    pkg = Package.load(sample_path)
    section = _cut_section(pkg, section_index, slice_, drop, blank_tables)
    flatten_mixed(section)
    if fix:
        fix(section)                      # 양식마다 손봐야 하는 틀 (예: 제목 칸 글자를 자리표시로)
    if frame_only:                        # 틀만 있는 양식(표지·목록 등): 본문 찾기를 하지 않는다
        body_from_text = body_from = None
    if body_from_text is not None:        # 본문이 시작하는 문단을 글자로 지정 (쪽을 잘라 번호가 바뀌어도 안전)
        body_from = next((i for i, p in enumerate(section.findall(HP + "p"))
                          if own_text(p).strip().startswith(body_from_text)), None)
        if body_from is None:
            raise ValueError("본문 시작 글자 '%s' 를 찾지 못했어요" % body_from_text)
    if frame_only:
        body = None
    elif body_from is not None:             # 본문 범위를 직접 지정: 이 문단부터 (끝에서 body_tail 개 문단은 틀로 남김)
        body = (section, body_from, len(section.findall(HP + "p")) - body_tail)
    else:
        body = detect_body(section)
    has_body = body is not None
    if not has_body and not allow_no_body:
        raise ValueError("본문(□ 제목 줄)을 찾지 못했어요. □ 로 시작하는 제목 줄이 있는 양식이어야 해요.")
    protos, tables = {}, []
    style = {"heading_prefix": "", "heading_pad2": False, "item1_prefix": " ◦", "kv": None}
    if has_body:
        lst, a, b = body
        ps = lst.findall(HP + "p")
        region = ps[a:b]

        # 1) 견본 문단 뽑기
        kinds = collections.defaultdict(list)
        for p in region:
            kinds[classify(p)].append(p)
        for k in ("h1", "item1", "item2", "item3", "blank"):
            if kinds.get(k):
                protos[k] = _empty_like(kinds[k][0])
        if kinds.get("h1"):
            h = own_text(kinds["h1"][0])
            m = re.match(r"^([\s%s]*)(.*)$" % re.escape(T.HEAD_MARKS), h)
            style["heading_prefix"] = m.group(1) if m else ""
            style["heading_pad2"] = bool(re.fullmatch(r"\S\s{2,}\S", (m.group(2) if m else "").strip()))
        if kinds.get("kv"):
            protos["kv"] = _empty_like(kinds["kv"][0])
            roles_list = [r for r in (_kv_roles(p) for p in kinds["kv"]) if r]
            style["kv"] = roles_list[0]
            info = cp_info(pkg)
            for key in ("label", "bullet", "gap", "value"):
                style["kv"][key] = (typical_cp(info, [r[key] for r in roles_list]) if key in ("label", "value")
                                    else collections.Counter(r[key] for r in roles_list).most_common(1)[0][0])
            style["item1_prefix"] = _prefix_of(kinds["kv"][0])[0] or style["item1_prefix"]
            by = collections.defaultdict(list)       # 항목 이름별 값 글자 모양 (예: 일시·진행방식만 굵게 밑줄)
            for p in kinds["kv"]:
                ro, lab = _kv_roles(p), _kv_label(p)
                if ro and lab:
                    by[lab].append(ro["value"])
            style["kv"]["by_label"] = {k: typical_cp(info, v) for k, v in by.items()}
        if kinds.get("item1"):
            style["item1_prefix"] = _prefix_of(kinds["item1"][0])[0] or style["item1_prefix"]
        for lvl in ("item1", "item2", "item3"):       # 글머리(앞 조각)와 본문(가장 긴 조각)의 글자 모양을 따로
            if kinds.get(lvl):
                first = [next((x.get("charPrIDRef") for x in p.findall(HP + "run")
                               if "".join(t.text or "" for t in x.findall(HP + "t")) != ""), None) for p in kinds[lvl]]
                dom = [dominant_cp(p) for p in kinds[lvl]]
                mode = lambda xs: collections.Counter(x for x in xs if x).most_common(1)[0][0] if any(xs) else None  # noqa: E731
                style[lvl + "_cps"] = [mode(first), typical_cp(cp_info(pkg), dom)]
        for lvl in ("item2", "item3"):                # ▸ 둘째 단계, - 셋째 단계: 들여쓰기·기호·기호 뒤 공백을 샘플 그대로
            if kinds.get(lvl):
                pre, sep = _prefix_of(kinds[lvl][0])
                style[lvl + "_prefix"], style[lvl + "_sep"] = pre, sep
        if style.get("kv") and kinds.get("item1"):
            # '◦ 이름 : 값' 줄이 있는 양식은 이름 없는 '◦ 글' 줄도 같은 글자 크기·모양으로 통일한다
            # (샘플 마지막 구역의 크게 강조된 줄 하나가 대표 모양이 되지 않게)
            protos["item1"] = clone(protos["kv"])
            style["item1_cps"] = [style["kv"]["bullet"], style["kv"]["value"]]
        if "kv" not in protos and "item1" in protos:
            protos["kv"] = clone(protos["item1"])
        if "item1" not in protos and "kv" in protos:
            protos["item1"] = clone(protos["kv"])
        if "blank" not in protos:
            protos["blank"] = _empty_like(region[0])
        if plain_body:                        # 본문 = 줄마다 한 문단(앞 한 칸 들여쓰기) + 사이 빈 줄
            plain = [p for p in kinds.get("other", []) + kinds.get("item1", []) if own_text(p).strip()]
            if not plain:
                raise ValueError("본문 문단을 찾지 못했어요")
            protos["para"] = _empty_like(plain[0])
            lead = re.match(r"^\s*", own_text(plain[0])).group(0)
            style["body_mode"], style["para_prefix"] = "plain", lead
        elif "h1" not in protos:
            raise ValueError("□ 제목 줄 모양을 찾지 못했어요")

        # 2) 표 견본
        from .package import HH
        bold_ids = {c.get("id") for c in pkg.header().iter(HH + "charPr") if c.find(HH + "bold") is not None}
        for p in kinds.get("table", []):
            for run in p.findall(HP + "run"):
                for tbl in run.findall(HP + "tbl"):
                    info = extract_table_protos(tbl)
                    if not info["rows"]:
                        continue
                    wrapper = clone(p)
                    for t in wrapper.findall(HP + "run"):
                        for tt in t.findall(HP + "t"):
                            tt.text = ""
                    for w in wrapper.iter(HP + "tbl"):
                        for tr in w.findall(HP + "tr"):
                            w.remove(tr)
                    info["wrapper"] = wrapper
                    tables.append(info)

        # 3) 본문을 {{BODY}} 한 줄로
        marker = clone(protos["blank"])
        P.set_par_runs(marker, [(marker.find(HP + "run").get("charPrIDRef") if marker.find(HP + "run") is not None
                                 else "0", BODY_MARK)])
        first = region[0]
        lst.replace(first, marker)
        for p in region[1:]:
            lst.remove(p)

    # 4) 입력 칸 자리표시
    flatten_mixed(section)
    used_keys = []
    for f in fields:
        sample = f.get("sample")
        if not sample:
            continue
        hit = 0
        for t in section.iter(HP + "t"):
            txt = t.text or ""
            if not txt or BODY_MARK in txt:
                continue
            ok = (txt.strip() == sample.strip()) if f.get("exact") else (sample in txt)
            if ok:
                t.text = txt.replace(sample.strip() if f.get("exact") else sample, f.get("repl") or "{{%s}}" % f["key"])
                hit += 1
                if f.get("once"):
                    break
        if not hit:
            raise ValueError("샘플에서 '%s' 글자를 찾지 못했어요 (%s)" % (sample, f["key"]))
        used_keys.append(f["key"])

    for t in section.iter(HP + "t"):          # 샘플에 남은 점 하나짜리 숨은 글자(목차 표식 등)는 지운다
        if (t.text or "").strip() == ".":
            t.text = ""

    # 5) 쓰지 않는 메모·구역 정리, 줄 배치 캐시 제거
    for el in list(section.iter(HP + "fieldBegin")):      # 시작만 남고 끝이 없는 필드는 '파일 손상'이 되므로 필드는 모두 뺀다(글자는 그대로)
        ctrl = el.getparent()
        if ctrl.getparent() is not None:
            ctrl.getparent().remove(ctrl)
    for el in list(section.iter(HP + "fieldEnd")):
        ctrl = el.getparent()
        ctrl.getparent().remove(ctrl)
    P.strip_lineseg(section)
    sanitize_package(pkg, extra_roots=list(protos.values()) + [w for info in tables for w in [info["wrapper"]] +
                                                                [x["tr"] for x in info["rows"]]])

    leftover = [t for _, t in frame_texts(section, None) if BODY_MARK not in t and "{{" not in t]

    os.makedirs(out_dir, exist_ok=True)
    proto_root = etree.Element("protos")
    for k, p in protos.items():
        e = etree.SubElement(proto_root, "proto", name=k)
        e.append(clone(p))
    for info in tables:
        te = etree.SubElement(proto_root, "table", cols=str(info["cols"]))
        w = etree.SubElement(te, "wrapper")
        w.append(clone(info["wrapper"]))
        for r in info["rows"]:
            re_ = etree.SubElement(te, "row", kind=r["kind"], spans=",".join(map(str, r["spans"])))
            tr = clone(r["tr"])
            P.strip_lineseg(tr)
            tcs_ = tr.findall(HP + "tc")
            for tc in tcs_:                                   # 샘플의 글자(이름 등)는 남기지 않고 글자 모양만 보관
                first_cp = next((x.get("charPrIDRef") for x in tc.iter(HP + "run")
                                 if "".join(t.text or "" for t in x.findall(HP + "t")).strip()), None)
                # 줄 전체를 합친 칸(등록 및 개회 같은 구분 줄)은 맨 앞 글자 모양, 그 밖엔 글자가 가장 많은 조각의 모양
                dcp = first_cp if len(tcs_) == 1 else dominant_cp(tc)
                tc.set("dcp", dcp or "")
                tc.set("bcp", P.derive_charpr(pkg, dcp, bold=True) if dcp else "")    # 굵게 = 본 글자 모양에서 굵기만 켠 것
                for t in tc.iter(HP + "t"):
                    t.text = ""
            re_.append(tr)
    xml = etree.tostring(proto_root, encoding="unicode")
    with open(os.path.join(out_dir, "protos.xml"), "w", encoding="utf-8") as f:
        f.write(xml)
    pkg.save(os.path.join(out_dir, "template.hwpx"))          # 표 견본이 만든 파생 글자 모양까지 담아 저장

    form = {
        "id": form_id, "name": name, "audience": audience, "kind": kind, "note": note,
        "fields": [{k: v for k, v in f.items() if k not in ("sample", "repl", "exact", "once")} for f in fields],
        "has_body": has_body,
        "style": style,
        "starter": starter,
        "body_hint": "□ 제목 / ◦ 일시: … / - 세부 / | 표 | 칸 |",
    }
    with open(os.path.join(out_dir, "form.json"), "w", encoding="utf-8") as f:
        json.dump(form, f, ensure_ascii=False, indent=2)
    return {"id": form_id, "leftover": leftover, "protos": sorted(protos), "tables": [t["cols"] for t in tables],
            "style": style}


# ------------------------------------------------------------------ 생성
def fmt_value(field, raw):
    raw = "" if raw is None else str(raw).strip()
    if not raw:
        return ""
    if field.get("type") == "date":
        try:
            d = T.parse_date(raw)
        except ValueError:
            return raw
        fm = field.get("fmt", "long")
        if fm == "long":
            return T.fmt_date_long(d)
        if fm == "dot":
            return T.fmt_date_dot(d)
        return "%d. %d. %d" % (d.year, d.month, d.day)
    return raw


class ReportForm:
    def __init__(self, folder):
        self.folder = folder
        with open(os.path.join(folder, "form.json"), encoding="utf-8") as f:
            self.meta = json.load(f)
        self._protos = None

    # 메타
    @property
    def id(self):
        return self.meta["id"]

    @property
    def name(self):
        return self.meta["name"]

    @property
    def fields(self):
        return self.meta["fields"]

    def _load_protos(self):
        if self._protos is None:
            with open(os.path.join(self.folder, "protos.xml"), encoding="utf-8") as f:
                root = P.parse_xml(f.read().encode("utf-8"))
            protos, tables = {}, {}
            for e in root.findall("proto"):
                protos[e.get("name")] = e[0]
            for te in root.findall("table"):
                rows = []
                for r in te.findall("row"):
                    rows.append({"kind": r.get("kind"), "spans": [int(x) for x in r.get("spans").split(",")],
                                 "tr": r[0]})
                tables.setdefault(int(te.get("cols")), []).append({"wrapper": te.find("wrapper")[0], "rows": rows})
            self._protos = (protos, tables)
        return self._protos

    # ---- 문단 만들기
    def _kind(self, kind):
        """견본이 없는 종류를 만나면 가장 가까운 견본으로 대신한다(양식에 따라 없을 수 있음)"""
        protos = self._load_protos()[0]
        for k in (kind, "item1", "para", "kv", "h1", "blank"):
            if k in protos:
                return k
        raise ValueError("이 양식에는 본문 견본이 없어요")

    def _par(self, kind, specs):
        protos, _ = self._load_protos()
        kind = self._kind(kind)
        p = clone(protos[kind])
        P.set_par_runs(p, specs)
        return self._renew(p)

    def _renew(self, p):
        if getattr(self, "_idgen", None) is not None:
            self._idgen.renew(p)
        return p

    def _first_cp(self, kind):
        protos, _ = self._load_protos()
        r = protos[self._kind(kind)].find(HP + "run")
        return r.get("charPrIDRef") if r is not None else "0"

    def _heading(self, text):
        protos, _ = self._load_protos()
        st = self.meta["style"]
        text = text.strip()
        if st.get("heading_pad2") and len(re.sub(r"\s", "", text)) == 2:
            text = re.sub(r"\s", "", text)
            text = text[0] + "    " + text[1]
        p = clone(protos[self._kind("h1")])
        ts = [t for run in p.findall(HP + "run") for t in run.findall(HP + "t")]
        full = st.get("heading_prefix", "") + text
        if ts:
            ts[0].text = full
            for t in ts[1:]:
                t.text = ""
        else:
            P.set_par_runs(p, [(self._first_cp("h1"), full)])
        return self._renew(p)

    def _kv(self, label, value):
        st = self.meta["style"]
        kv = st.get("kv")
        bullet = st["item1_prefix"]
        if not kv:
            return self._par("item1", [(self._first_cp("item1"), "%s %s : %s" % (bullet, T.pad_label(label), value))])
        lab = T.pad_label(label)
        vcp = kv.get("by_label", {}).get(re.sub(r"\s", "", label), kv["value"])
        if kv["fmt"] == "paren":
            specs = [(kv["bullet"], bullet), (kv["gap"], " "), (kv["label"], "(%s)" % lab), (kv["gap"], " "),
                     (vcp, value)]
        else:
            specs = [(kv["bullet"], bullet), (kv["label"], " %s " % lab), (vcp, ": %s" % value)]
        return self._par("kv", specs)

    def _item(self, kind, text):
        """◦(item1) ▸(item2) -(item3) 줄. 샘플에 없는 단계는 아래 단계 견본을 빌려 쓴다."""
        st = self.meta["style"]
        protos = self._load_protos()[0]
        defaults = {"item1": (st["item1_prefix"], " "), "item2": ("    ▸", ""), "item3": ("      -", "")}
        pre, sep = defaults[kind]
        if kind != "item1":
            pre, sep = st.get(kind + "_prefix", pre), st.get(kind + "_sep", sep)
        proto = kind if kind in protos else ("item2" if kind == "item3" and "item2" in protos else "item1")
        if proto == "item1" and kind == "item1" and not pre.endswith(" "):
            sep = " "
        cps = st.get(proto + "_cps") or [self._first_cp(proto)] * 2
        bullet_cp, text_cp = cps[0] or self._first_cp(proto), cps[1] or cps[0] or self._first_cp(proto)
        if bullet_cp == text_cp:
            return self._par(proto, [(text_cp, "%s%s%s" % (pre, sep, text))])
        return self._par(proto, [(bullet_cp, pre + sep), (text_cp, text)])      # 글머리 모양 + 본문 모양

    @staticmethod
    def _vw(s):
        """글자 폭(한글·한자 2, 그 밖 1) — 항목 이름을 ':' 앞에서 오른쪽 맞춤할 때 쓴다"""
        return sum(2 if ord(c) >= 0x2E80 else 1 for c in s)

    def _kv2(self, label, value, width=0):
        lab = T.pad_label(label)
        lab = " " * max(0, width - self._vw(lab)) + lab          # 이름 앞에 공백을 채워 ':' 가 한 줄로 서게
        return self._item("item2", "%s : %s" % (lab, value))

    def _table(self, rows, idgen):
        _, tables = self._load_protos()
        ncols = max(len(r) for r in rows)
        need = {len(r) for r in rows}
        entry = None
        # 칸 수가 같은 표를 먼저, 없으면 줄마다 맞는 모양(합친 칸 포함)이 있는 더 큰 표
        for cols in sorted(tables, key=lambda c: (c != ncols, c)):
            for e in tables[cols]:
                have = {len(r["spans"]) for r in e["rows"]}
                if need <= have and cols >= ncols:
                    entry = e
                    break
            if entry:
                break
        if entry is None:
            have = sorted({len(r["spans"]) for e in sum(tables.values(), []) for r in e["rows"]})
            raise ValueError("이 양식의 표는 %s칸 줄만 만들 수 있어요 (입력한 표: %s칸 줄)" % (
                "·".join(map(str, have)) or "없는", "·".join(map(str, sorted(need)))))
        wrapper = clone(entry["wrapper"])
        tbl = next(wrapper.iter(HP + "tbl"))
        idgen.renew(wrapper)
        n = len(rows)
        total_h = 0
        for i, cells in enumerate(rows):
            m = len(cells)
            kind = "first" if i == 0 else "last" if i == n - 1 else "mid"
            cand = [r for r in entry["rows"] if len(r["spans"]) == m]
            if not cand:
                raise ValueError("이 표에는 %d칸짜리 줄 모양이 없어요 (%d째 줄)" % (m, i + 1))
            order = [kind, "mid", "first", "last"]
            cand.sort(key=lambda r: order.index(r["kind"]))
            tr = clone(cand[0]["tr"])
            for tc, text in zip(tr.findall(HP + "tc"), cells):
                self._set_cell(tc, text)
            col = 0
            for tc in tr.findall(HP + "tc"):
                ad = tc.find(HP + "cellAddr")
                ad.set("rowAddr", str(i))
                ad.set("colAddr", str(col))
                col += int(tc.find(HP + "cellSpan").get("colSpan", "1"))
            total_h += int(tr.find(HP + "tc").find(HP + "cellSz").get("height", "0"))
            tbl.append(tr)
        tbl.set("rowCnt", str(n))
        sz = tbl.find(HP + "sz")
        if sz is not None and total_h:
            sz.set("height", str(total_h))
        return wrapper

    @staticmethod
    def _set_cell(tc, text):
        sub = tc.find(HP + "subList")
        ps = sub.findall(HP + "p")
        first = ps[0]
        for extra in ps[1:]:
            sub.remove(extra)
        run = first.find(HP + "run")
        cp = tc.get("dcp") or (run.get("charPrIDRef") if run is not None else "0")
        lines = re.split(r"<br\s*/?>|\\n", text) if text else [""]
        bcp = tc.get("bcp") or cp
        for k in ("dcp", "bcp"):                  # 보관용 내부 표시는 만든 파일에 남기면 한글이 '손상'으로 본다
            tc.attrib.pop(k, None)

        def specs(line):
            out = []
            for k, seg in enumerate(re.split(r"\*\*", line)):
                if seg:
                    out.append((bcp if k % 2 else cp, seg))
            return out or [(cp, "")]
        P.set_par_runs(first, specs(lines[0]))
        prev = first
        for ln in lines[1:]:
            q = clone(first)
            P.set_par_runs(q, specs(ln))
            prev.addnext(q)
            prev = q

    def build_body(self, outline, idgen):
        out = []
        st = self.meta["style"]
        if st.get("body_mode") == "plain":      # 한 줄 = 한 문단, 문단 사이에 빈 줄
            lines = [ln.strip() for ln in (outline or "").splitlines() if ln.strip()]
            cp = self._first_cp("para")
            if not lines:                       # 내용을 안 주면 샘플 모양의 빈 문단 하나만(들여쓰기 한 칸) — 이어서 한글에서 쓰도록
                return [self._par("para", [(cp, st.get("para_prefix", ""))])]
            for i, ln in enumerate(lines):
                out.append(self._par("para", [(cp, st.get("para_prefix", "") + ln)]))
                if i < len(lines) - 1:
                    out.append(self._par("blank", [(self._first_cp("blank"), "")]))
            return out
        blocks = T.parse_outline(outline)
        widths, run = {}, []                    # 이어진 ▸ 항목 줄(kv2)마다 가장 긴 이름의 폭
        for i, b in enumerate(blocks + [("end",)]):
            if b[0] == "kv2":
                run.append(i)
                continue
            if run:
                w = max(self._vw(T.pad_label(blocks[j][1])) for j in run)
                widths.update({j: w for j in run})
                run = []
        for i, b in enumerate(blocks):
            k = b[0]
            if k == "h1":
                out.append(self._heading(b[1]))
            elif k == "kv":
                out.append(self._kv(b[1], b[2]))
            elif k in ("item1", "item2", "item3"):
                out.append(self._item(k, b[1]))
            elif k == "kv2":
                out.append(self._kv2(b[1], b[2], widths.get(i, 0)))
            elif k == "blank":
                out.append(self._par("blank", [(self._first_cp("blank"), "")]))
            elif k == "pagebreak":
                p = self._par("blank", [(self._first_cp("blank"), "")])
                p.set("pageBreak", "1")
                out.append(p)
            elif k == "table":
                out.append(self._table(b[1], idgen))
        return out

    def render_fragment(self, outline):
        """틀(제목 상자·표지 표)은 빼고 본문 문단만 든 조각 — 쪽을 더 붙일 때(참고자료 등) 커서 위치에 끼워 넣는다."""
        pkg = Package.load(os.path.join(self.folder, "template.hwpx"))
        section = pkg.section(0)
        idgen = P.IdGen(section)
        self._idgen = idgen
        first = section.findall(HP + "p")[0]
        carrier = clone(first)
        for run in carrier.findall(HP + "run"):
            for c in list(run):
                if local(c) in ("tbl", "pic", "container", "t"):
                    run.remove(c)
        for p in section.findall(HP + "p"):
            section.remove(p)
        section.append(carrier)
        for el in self.build_body(outline, idgen):
            section.append(el)
        P.strip_lineseg(section)
        return pkg

    def render(self, values, outline):
        """values: {필드키: 값} / outline: 본문 개요 글 → Package"""
        pkg = Package.load(os.path.join(self.folder, "template.hwpx"))
        section = pkg.section(0)
        idgen = P.IdGen(section)
        self._idgen = idgen
        fields = {f["key"]: f for f in self.fields}

        def sub(m):
            key = m.group(1)
            f = fields.get(key, {})
            return fmt_value(f, values.get(key, "")) or f.get("blank", "")      # 비면 '(    )' 처럼 손으로 쓸 빈칸

        for t in section.iter(HP + "t"):
            if t.text and "{{" in t.text and BODY_MARK not in t.text:
                t.text = _PH.sub(sub, t.text)
        marker = None
        for p in section.iter(HP + "p"):
            if own_text(p) == BODY_MARK:
                marker = p
                break
        if marker is None:
            if not self.meta.get("has_body", True):
                P.strip_lineseg(section)
                return pkg
            raise ValueError("양식에 {{BODY}} 자리가 없어요")
        parent = marker.getparent()
        idx = parent.index(marker)
        parent.remove(marker)
        for k, el in enumerate(self.build_body(outline, idgen)):
            parent.insert(idx + k, el)
        P.strip_lineseg(section)
        return pkg
