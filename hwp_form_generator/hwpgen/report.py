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
    if re.match(r"^[-－–ㅡ]\s", s):
        return "item2"
    return "other"


def _kv_roles(p):
    """◦ 이름 : 값 줄의 글자 모양 역할(글머리·이름·값)을 추정한다."""
    runs = [(r.get("charPrIDRef"), "".join(t.text or "" for t in r.findall(HP + "t"))) for r in p.findall(HP + "run")]
    runs = [(c, t) for c, t in runs if t != ""]
    if not runs:
        return None
    full = "".join(t for _, t in runs)
    paren = bool(re.match(r"^\s*[%s]\s*\(" % re.escape(T.BULLETS), full))
    bullet_cp = runs[0][0]
    label_cp = value_cp = gap_cp = None
    if len(runs) == 1:
        return {"fmt": "paren" if paren else "colon", "bullet": bullet_cp, "label": bullet_cp, "gap": bullet_cp,
                "value": bullet_cp}
    if paren:
        for c, t in runs[1:]:
            if label_cp is None and "(" in t:
                label_cp = c
            elif label_cp is not None and value_cp is None and t.strip():
                value_cp = c
            elif label_cp is None and not t.strip():
                gap_cp = c
        # 값이 여러 조각이면 첫 조각을 값 모양으로
    else:
        for c, t in runs[1:]:
            if label_cp is None and t.strip() and ":" not in t and "：" not in t:
                label_cp = c
            elif ":" in t or "：" in t:
                value_cp = c
                break
    label_cp = label_cp or bullet_cp
    value_cp = value_cp or runs[-1][0]
    return {"fmt": "paren" if paren else "colon", "bullet": bullet_cp, "label": label_cp,
            "gap": gap_cp or label_cp, "value": value_cp}


def _prefix_of(p):
    s = own_text(p)
    m = re.match(r"^(\s*[%s])" % re.escape(T.BULLETS + "-－–"), s)
    return m.group(1) if m else ""


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


def extract_table_protos(tbl):
    """표 하나에서 '행 견본'을 뽑는다. 병합 모양(칸 수·합친 칸)별로 처음/중간/끝 줄을 따로 둔다."""
    col_cnt = int(tbl.get("colCnt"))
    rows = tbl.findall(HP + "tr")
    res = {"cols": col_cnt, "rows": []}
    n = len(rows)
    for i, tr in enumerate(rows):
        cells, spans, simple = _row_info(tr)
        if not simple or sum(spans) != col_cnt:
            continue
        kind = "first" if i == 0 else "last" if i == n - 1 else "mid"
        res["rows"].append({"kind": kind, "spans": spans, "tr": tr})
    return res


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


def sanitize_package(pkg, keep_sections=("Contents/section0.xml",)):
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
    # 그림: 본문에서 쓰는 것만 남기고 나머지는 1×1 빈 그림
    used = set()
    for n in pkg.section_names():
        for el in pkg.xml(n).iter():
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


def register_report(sample_path, out_dir, form_id, name, audience, fields, kind="report", note="", starter=""):
    """샘플 hwpx → 양식 폴더(form.json, template.hwpx, protos.xml).

    fields: [{"key","label","sample"(샘플에 들어 있던 글자),"type": text|date|multiline, "fmt":..., "profile": 키}]
    반환: dict(결과 요약, 'leftover': 입력 칸으로 지정되지 않고 고정 글자로 남은 것들)
    """
    pkg = Package.load(sample_path)
    section = pkg.section(0)
    body = detect_body(section)
    if body is None:
        raise ValueError("본문(□ 제목 줄)을 찾지 못했어요. □ 로 시작하는 제목 줄이 있는 양식이어야 해요.")
    lst, a, b = body
    ps = lst.findall(HP + "p")
    region = ps[a:b]

    # 1) 견본 문단 뽑기
    kinds = collections.defaultdict(list)
    for p in region:
        kinds[classify(p)].append(p)
    protos = {}
    for k in ("h1", "item1", "item2", "blank"):
        if kinds.get(k):
            protos[k] = _empty_like(kinds[k][0])
    style = {"heading_prefix": "", "heading_pad2": False, "item1_prefix": " ◦", "item2_prefix": "  -", "kv": None}
    if kinds.get("h1"):
        h = own_text(kinds["h1"][0])
        m = re.match(r"^([\s%s]*)(.*)$" % re.escape(T.HEAD_MARKS), h)
        style["heading_prefix"] = m.group(1) if m else ""
        style["heading_pad2"] = bool(re.fullmatch(r"\S\s{2,}\S", (m.group(2) if m else "").strip()))
    if kinds.get("kv"):
        protos["kv"] = _empty_like(kinds["kv"][0])
        roles_list = [r for r in (_kv_roles(p) for p in kinds["kv"]) if r]
        style["kv"] = roles_list[0]
        for key in ("label", "bullet", "gap", "value"):
            style["kv"][key] = collections.Counter(r[key] for r in roles_list).most_common(1)[0][0]
        style["item1_prefix"] = _prefix_of(kinds["kv"][0]) or style["item1_prefix"]
    if kinds.get("item1"):
        style["item1_prefix"] = _prefix_of(kinds["item1"][0]) or style["item1_prefix"]
    if kinds.get("item2"):
        style["item2_prefix"] = _prefix_of(kinds["item2"][0]) or style["item2_prefix"]
    if "kv" not in protos and "item1" in protos:
        protos["kv"] = clone(protos["item1"])
    if "item1" not in protos and "kv" in protos:
        protos["item1"] = clone(protos["kv"])
    if "blank" not in protos:
        protos["blank"] = _empty_like(region[0])
    if "h1" not in protos:
        raise ValueError("□ 제목 줄 모양을 찾지 못했어요")

    # 2) 표 견본
    tables = []
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
    P.set_par_runs(marker, [(marker.find(HP + "run").get("charPrIDRef") if marker.find(HP + "run") is not None else "0",
                             BODY_MARK)])
    first = region[0]
    lst.replace(first, marker)
    for p in region[1:]:
        lst.remove(p)

    # 4) 입력 칸 자리표시
    used_keys = []
    for f in fields:
        sample = f.get("sample")
        if not sample:
            continue
        hit = 0
        for t in section.iter(HP + "t"):
            if t.text and sample in t.text and BODY_MARK not in t.text:
                t.text = t.text.replace(sample, "{{%s}}" % f["key"])
                hit += 1
        if not hit:
            raise ValueError("샘플에서 '%s' 글자를 찾지 못했어요 (%s)" % (sample, f["key"]))
        used_keys.append(f["key"])

    # 5) 쓰지 않는 메모·구역 정리, 줄 배치 캐시 제거
    for el in list(section.iter(HP + "fieldBegin")):
        if el.get("type") == "MEMO":
            ctrl = el.getparent()
            ctrl.getparent().remove(ctrl)
    for el in list(section.iter(HP + "fieldEnd")):
        ctrl = el.getparent()
        ctrl.getparent().remove(ctrl)
    P.strip_lineseg(section)
    sanitize_package(pkg)

    leftover = [t for _, t in frame_texts(section, None) if BODY_MARK not in t and "{{" not in t]

    os.makedirs(out_dir, exist_ok=True)
    pkg.save(os.path.join(out_dir, "template.hwpx"))
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
            re_.append(tr)
    xml = etree.tostring(proto_root, encoding="unicode")
    with open(os.path.join(out_dir, "protos.xml"), "w", encoding="utf-8") as f:
        f.write(xml)

    form = {
        "id": form_id, "name": name, "audience": audience, "kind": kind, "note": note,
        "fields": [{k: v for k, v in f.items() if k != "sample"} for f in fields],
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
    def _par(self, kind, specs):
        protos, _ = self._load_protos()
        p = clone(protos[kind])
        P.set_par_runs(p, specs)
        return self._renew(p)

    def _renew(self, p):
        if getattr(self, "_idgen", None) is not None:
            self._idgen.renew(p)
        return p

    def _first_cp(self, kind):
        protos, _ = self._load_protos()
        r = protos[kind].find(HP + "run")
        return r.get("charPrIDRef") if r is not None else "0"

    def _heading(self, text):
        protos, _ = self._load_protos()
        st = self.meta["style"]
        text = text.strip()
        if st.get("heading_pad2") and len(re.sub(r"\s", "", text)) == 2:
            text = re.sub(r"\s", "", text)
            text = text[0] + "    " + text[1]
        p = clone(protos["h1"])
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
        if kv["fmt"] == "paren":
            specs = [(kv["bullet"], bullet), (kv["gap"], " "), (kv["label"], "(%s)" % lab), (kv["gap"], " "),
                     (kv["value"], value)]
        else:
            specs = [(kv["bullet"], bullet), (kv["label"], " %s " % lab), (kv["value"], ": %s" % value)]
        return self._par("kv", specs)

    def _item(self, kind, text):
        st = self.meta["style"]
        prefix = st["item1_prefix"] if kind == "item1" else st["item2_prefix"]
        proto = "item1"
        if kind == "item2" and "item2" in self._load_protos()[0]:
            proto = "item2"
        cp = self._first_cp(proto)
        sep = " " if not prefix.endswith(" ") else ""
        return self._par(proto, [(cp, "%s%s%s" % (prefix, sep, text))])

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
        cp = run.get("charPrIDRef") if run is not None else "0"
        for r in first.findall(HP + "run"):
            for t in r.findall(HP + "t"):
                if (t.text or "").strip():
                    cp = r.get("charPrIDRef")
                    break
            else:
                continue
            break
        lines = re.split(r"<br\s*/?>|\\n", text) if text else [""]
        P.set_par_runs(first, [(cp, lines[0])])
        prev = first
        for ln in lines[1:]:
            q = clone(first)
            P.set_par_runs(q, [(cp, ln)])
            prev.addnext(q)
            prev = q

    def build_body(self, outline, idgen):
        out = []
        for b in T.parse_outline(outline):
            k = b[0]
            if k == "h1":
                out.append(self._heading(b[1]))
            elif k == "kv":
                out.append(self._kv(b[1], b[2]))
            elif k in ("item1", "item2"):
                out.append(self._item(k, b[1]))
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
            raise ValueError("양식에 {{BODY}} 자리가 없어요")
        parent = marker.getparent()
        idx = parent.index(marker)
        parent.remove(marker)
        for k, el in enumerate(self.build_body(outline, idgen)):
            parent.insert(idx + k, el)
        P.strip_lineseg(section)
        return pkg
