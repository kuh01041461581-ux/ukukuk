# -*- coding: utf-8 -*-
"""주간업무계획 · 주간 주요일정.

 - 주간업무계획(국 한 쪽): 항목마다 '❶ 행사명 (부서 담당자 내선)' 머리줄 + '◦ 일시/장소/…' 줄
 - 주간 주요일정(표): 월~일 날짜별로 시간·일정명·장소·주관부서·국장님·비고
 - 주간업무계획의 글에서 일정을 뽑아(extract_events) 주요일정 표를 채운다.
"""
import collections
import datetime
import json
import os
import re

from lxml import etree

from . import package as P
from . import textfmt as T
from .package import HP, Package, clone, local, own_text, ptext
from .report import sanitize_package, _kv_roles, _empty_like

ITEMS_MARK = "{{ITEMS}}"
DEFAULT_ORGS = ["기획안전국", "청년경제국", "문화교육국", "복지국", "바이오농정국", "녹색도시환경국", "건설국", "보건소",
                "농업기술센터", "상하수도사업단", "홍보담당관", "국가식품클러스터담당관", "감사위원회", "정책개발담당관"]
_PH = re.compile(r"\{\{(\w+)\}\}")
SYMBOL_BASE = 0xF02CD          # 한글 글꼴의 동그라미 숫자 (1 → U+F02CE)
KV_LABELS_FIRST = ("일시", "장소")


def item_symbol(k):
    if 1 <= k <= 9:
        return chr(SYMBOL_BASE + k)
    return "(%d)" % k


# =========================================================== 주간업무계획 등록
def register_plan(sample_path, out_dir, org_sample="문화교육국"):
    pkg = Package.load(sample_path)
    sec = pkg.section(0)
    ps = sec.findall(HP + "p")
    # 국 이름 쪽(탭) 문단: 표를 품고 글자가 org_sample 인 것
    tab_idx = None
    for i, p in enumerate(ps):
        if any(local(c) == "tbl" for r in p.findall(HP + "run") for c in r) and ptext(p).strip() == org_sample:
            tab_idx = i
    if tab_idx is None:
        raise ValueError("'%s' 이름표 문단을 찾지 못했어요" % org_sample)
    # 항목 문단들: 이름표 다음부터 다음 이름표(표 품은 문단) 전까지
    end = len(ps)
    for j in range(tab_idx + 1, len(ps)):
        if any(local(c) == "tbl" for r in ps[j].findall(HP + "run") for c in r):
            end = j
            break
    block = ps[tab_idx + 1:end]
    protos = {}
    headers, kvs, blanks = [], [], []
    for p in block:
        t = own_text(p)
        s = t.strip()
        if not s:
            blanks.append(p)
        elif s[0] in T.BULLETS:
            kvs.append(p)
        elif re.search(r"\([^()]*\)\s*$", s):
            headers.append(p)
    if not headers or not kvs:
        raise ValueError("주간업무계획 항목(제목줄·◦ 줄)을 찾지 못했어요")
    # 머리줄: 기호 run 이 따로 있는 것(두 번째 항목 모양)을 우선
    hp_ = next((p for p in headers if len(own_text(p).split()) and
                len(p.findall(HP + "run")) >= 3 and len("".join(t.text or "" for t in p.findall(HP + "run")[0].findall(HP + "t")).strip()) <= 2), headers[0])
    runs = [(r.get("charPrIDRef"), "".join(t.text or "" for t in r.findall(HP + "t"))) for r in hp_.findall(HP + "run")]
    runs = [x for x in runs if x[1] != ""]
    head_roles = {"sym": runs[0][0], "title": runs[1][0] if len(runs) > 1 else runs[0][0],
                  "paren": runs[-1][0]}
    protos["header"] = _empty_like(hp_)
    protos["kv_mid"] = _empty_like(kvs[min(1, len(kvs) - 1)] if len(kvs) > 1 else kvs[0])
    protos["kv_last"] = _empty_like(kvs[-1])
    protos["blank"] = _empty_like(blanks[0]) if blanks else _empty_like(kvs[-1])
    roles_list = [r for r in (_kv_roles(p) for p in kvs) if r]
    kv_roles = None
    if roles_list:
        from .report import cp_info, typical_cp
        info = cp_info(pkg)
        kv_roles = dict(roles_list[0])
        for key in ("label", "value"):                       # 마지막 줄 하나가 아니라 여러 줄의 대표 글자 모양
            kv_roles[key] = typical_cp(info, [r[key] for r in roles_list]) or kv_roles[key]
    prefix = "◦"
    # 프레임: 구역 설정 문단 + 이름표 + 항목 표시
    secpar = clone(ps[0])
    runs0 = secpar.findall(HP + "run")
    for r in runs0[1:]:
        secpar.remove(r)
    P.strip_lineseg(secpar)
    tab = clone(ps[tab_idx])
    for t in tab.iter(HP + "t"):
        if (t.text or "").strip() == org_sample:
            t.text = "{{org}}"
    # 이름표 뒤 빈 문단은 한 줄만
    after = clone(ps[tab_idx + 1]) if tab_idx + 1 < len(ps) and not own_text(ps[tab_idx + 1]).strip() else None
    marker = _empty_like(protos["blank"])
    P.set_par_runs(marker, [(protos["blank"].find(HP + "run").get("charPrIDRef"), ITEMS_MARK)])
    for p in list(sec.findall(HP + "p")):
        sec.remove(p)
    for p in (secpar, tab, after, marker):
        if p is not None:
            sec.append(p)
    P.strip_lineseg(sec)
    sanitize_package(pkg)
    os.makedirs(out_dir, exist_ok=True)
    pkg.save(os.path.join(out_dir, "template.hwpx"))
    root = etree.Element("protos")
    for k, p in protos.items():
        e = etree.SubElement(root, "proto", name=k)
        e.append(clone(p))
    with open(os.path.join(out_dir, "protos.xml"), "w", encoding="utf-8") as f:
        f.write(etree.tostring(root, encoding="unicode"))
    form = {
        "id": "weekly_plan", "name": "주간업무계획 (국 쪽)", "audience": "주간 업무", "kind": "weekly_plan",
        "note": "국 이름표 + ❶ 행사명 (부서 담당자 내선) + ◦ 일시/장소/… 줄",
        "style": {"head": head_roles, "kv": kv_roles, "prefix": prefix},
    }
    with open(os.path.join(out_dir, "form.json"), "w", encoding="utf-8") as f:
        json.dump(form, f, ensure_ascii=False, indent=2)
    return {"id": "weekly_plan", "leftover": [t for t in (x.text for x in sec.iter(HP + "t")) if t and t.strip() and "{{" not in t],
            "protos": sorted(protos), "style": form["style"]}


# =========================================================== 주간업무계획 생성
def item_lines(item):
    """항목 → [(이름, 값)] : 일시·장소를 앞에 두고 사용자가 적은 줄을 잇는다."""
    extra = list(item.get("lines") or [])
    if isinstance(item.get("extra"), str):
        extra += [(b[1], b[2]) for b in T.parse_outline(item["extra"]) if b[0] == "kv"]
    labels = {re.sub(r"\s", "", l) for l, _ in extra}
    out = []
    if "일시" not in labels:
        when = when_text(item)
        if when:
            if item.get("when_note"):
                when += "         " + item["when_note"]
            out.append(("일시", when))
    if "장소" not in labels and item.get("place"):
        out.append(("장소", item["place"]))
    out += extra
    return out


def when_text(item):
    if not item.get("date"):
        return ""
    d = T.parse_date(item["date"])
    s = T.fmt_date_long(d)
    tm = (item.get("time") or "").strip()
    et = (item.get("end_time") or "").strip()
    if tm:
        s += " " + tm
        if et:
            s += " ~ " + et
    return s


class WeeklyPlanForm:
    def __init__(self, folder):
        self.folder = folder
        with open(os.path.join(folder, "form.json"), encoding="utf-8") as f:
            self.meta = json.load(f)
        with open(os.path.join(folder, "protos.xml"), encoding="utf-8") as f:
            root = P.parse_xml(f.read().encode("utf-8"))
        self.protos = {e.get("name"): e[0] for e in root.findall("proto")}

    id = property(lambda s: s.meta["id"])
    name = property(lambda s: s.meta["name"])

    def _item_pars(self, k, item):
        st = self.meta["style"]
        hd = st["head"]
        h = clone(self.protos["header"])
        paren = "(%s)" % " ".join(x for x in (item.get("dept", ""), item.get("staff", ""), item.get("phone", "")) if x)
        specs = [(hd["sym"], item_symbol(k) + " "), (hd["title"], item.get("title", "") + " ")]
        if paren != "()":
            specs.append((hd["paren"], paren))
        P.set_par_runs(h, specs)
        out = [h]
        lines = item_lines(item)
        kv = st.get("kv")
        for i, (label, value) in enumerate(lines):
            p = clone(self.protos["kv_mid"])               # 모든 줄 같은 문단 모양(샘플의 마지막 줄 모양은 줄바꿈용 특수 모양이라 쓰지 않는다)
            lab = T.pad_label(label)
            if kv:
                specs = [(kv["bullet"], st["prefix"]), (kv["label"], " %s " % lab), (kv["value"], ": %s" % value)]
            else:
                cp = p.find(HP + "run").get("charPrIDRef")
                specs = [(cp, "%s %s : %s" % (st["prefix"], lab, value))]
            P.set_par_runs(p, specs)
            out.append(p)
        return out

    def render(self, data):
        """data: {"org": "문화교육국", "items": [ {title, dept, staff, phone, date, time, end_time, place, lines|extra} ]}"""
        pkg = Package.load(os.path.join(self.folder, "template.hwpx"))
        sec = pkg.section(0)
        for t in sec.iter(HP + "t"):
            if t.text and "{{org}}" in t.text:
                t.text = t.text.replace("{{org}}", data.get("org", ""))
        marker = next(p for p in sec.iter(HP + "p") if own_text(p) == ITEMS_MARK)
        parent = marker.getparent()
        idx = parent.index(marker)
        parent.remove(marker)
        items = sorted(data.get("items", []), key=lambda it: (it.get("date") or "9999", it.get("time") or "99:99"))
        pos = idx
        for k, item in enumerate(items, 1):
            for p in self._item_pars(k, item):
                parent.insert(pos, p)
                pos += 1
            if k < len(items):
                parent.insert(pos, clone(self.protos["blank"]))
                pos += 1
        P.strip_lineseg(sec)
        return pkg


# =========================================================== 주요일정 표 등록
_DAYS = "월화수목금토일"


def _tc_info(tc):
    a = tc.find(HP + "cellAddr")
    s = tc.find(HP + "cellSpan")
    return int(a.get("colAddr")), int(a.get("rowAddr")), int(s.get("colSpan")), int(s.get("rowSpan"))


def _plain_charpr(pkg, cid):
    """글자 모양 cid 에서 노랑 바탕(shade)과 굵게만 뺀 새 글자 모양을 header.xml 에 더하고 번호를 돌려준다."""
    from .package import HH
    hdr = pkg.header()
    props = next(hdr.iter(HH + "charProperties"))
    src = next(c for c in props.findall(HH + "charPr") if c.get("id") == cid)
    if src.get("shadeColor") in (None, "none") and src.find(HH + "bold") is None:
        return cid
    new = clone(src)
    nid = str(max(int(c.get("id")) for c in props.findall(HH + "charPr")) + 1)
    new.set("id", nid)
    new.set("shadeColor", "none")
    b = new.find(HH + "bold")
    if b is not None:
        new.remove(b)
    props.append(new)
    props.set("itemCnt", str(len(props.findall(HH + "charPr"))))
    return nid


def register_schedule(sample_path, out_dir, org_sample="문화교육국"):
    pkg = Package.load(sample_path)
    sec = pkg.section(0)
    tbls = [t for t in sec.iter(HP + "tbl") if t.get("colCnt") == "7" and int(t.get("rowCnt")) >= 9]
    if not tbls:
        raise ValueError("7칸(날짜·시간·일정명·장소·주관부서·국장님·비고) 주요일정 표를 찾지 못했어요")
    big = tbls[0]
    trs = big.findall(HP + "tr")
    header_tr, footer_tr = trs[0], trs[-1]
    # 요일별 묶음
    groups = []
    for tr in trs[1:-1]:
        tcs = tr.findall(HP + "tc")
        if _tc_info(tcs[0])[0] == 0:
            groups.append([tr])
        elif groups:
            groups[-1].append(tr)
    by_day = {}
    for g in groups:
        txt = ptext(g[0].findall(HP + "tc")[0])
        m = re.search(r"\(([%s])\)" % _DAYS, txt)
        if m:
            by_day[m.group(1)] = g
    if len(by_day) < 5:
        raise ValueError("요일별(월~일) 묶음을 찾지 못했어요")

    def cps(tc):
        return [clone(p) for p in tc.find(HP + "subList").findall(HP + "p")]

    # 모양 견본
    ref = by_day.get("화") or next(iter(by_day.values()))
    protos = etree.Element("protos")

    def add(name, el, **attrs):
        e = etree.SubElement(protos, name, **attrs)
        e.append(clone(el))
        return e

    # 칸 틀: 화요일 묶음의 각 위치(처음/중간/끝) 줄의 칸 테두리 번호
    bfs = {}
    n = len(ref)
    for key, tr in (("first", ref[0]), ("mid", ref[1] if n > 2 else ref[0]), ("last", ref[-1])):
        tcs = tr.findall(HP + "tc")
        if key == "first":
            tcs = tcs[1:]
        bfs[key] = [tc.get("borderFillIDRef") for tc in tcs]
    # 칸 틀 하나(내용 칸)
    cell_tc = ref[1].findall(HP + "tc")[0] if n > 1 else ref[0].findall(HP + "tc")[1]
    cell_proto = clone(cell_tc)
    for p in cell_proto.find(HP + "subList").findall(HP + "p"):
        cell_proto.find(HP + "subList").remove(p)
    # 글 모양 견본: normal(검정) / important(노랑 강조) / weekend(빨강)
    first_tcs = by_day["월"][0].findall(HP + "tc")[1:] if "월" in by_day else None
    sat = by_day.get("토")
    if not sat:
        raise ValueError("토요일 묶음이 있어야 주말 글자색 견본을 만들 수 있어요")
    sat_tcs = sat[0].findall(HP + "tc")[1:]
    # 글 모양 견본: important(노랑 강조) / weekend(빨강) / normal(강조에서 노랑 바탕만 뺀 것: 샘플에 강조 없는 평일 줄이 없어서)
    styles = {"important": first_tcs, "weekend": sat_tcs}
    for name_, tcs in styles.items():
        e = etree.SubElement(protos, "style", name=name_)
        for tc in tcs:
            c = etree.SubElement(e, "cell")
            for p in cps(tc):
                P.strip_lineseg(p)
                for t in p.iter(HP + "t"):
                    t.text = ""
                c.append(p)
    normal = etree.SubElement(protos, "style", name="normal")
    cache = {}
    for c in protos.find("style[@name='important']").findall("cell"):
        nc = etree.SubElement(normal, "cell")
        for p in c:
            q = clone(p)
            for run in q.iter(HP + "run"):
                cid = run.get("charPrIDRef")
                if cid not in cache:
                    cache[cid] = _plain_charpr(pkg, cid)
                run.set("charPrIDRef", cache[cid])
            nc.append(q)
    # 날짜 칸(요일별)
    for d, g in by_day.items():
        tc = clone(g[0].findall(HP + "tc")[0])
        P.strip_lineseg(tc)
        add("daycell", tc, day=d)
    add("cell", cell_proto)
    add("header", header_tr)
    add("footer", footer_tr)
    for k, v in bfs.items():
        etree.SubElement(protos, "bf", pos=k, ids=",".join(v))
    row_h = int(ref[1].findall(HP + "tc")[0].find(HP + "cellSz").get("height", "2400")) if n > 1 else 2400
    # 틀(표 바깥) 만들기: 큰 표의 줄을 지우고 자리표시를 둔다
    for tr in list(big.findall(HP + "tr")):
        big.remove(tr)
    mark = etree.SubElement(big, HP + "tr")
    mark.set("mark", "ROWS")
    # 제목 칸: 메모 필드 제거 후 한 줄로
    for el in list(sec.iter(HP + "fieldBegin")):
        if el.get("type") == "MEMO":
            ctrl = el.getparent()
            ctrl.getparent().remove(ctrl)
    for el in list(sec.iter(HP + "fieldEnd")):
        ctrl = el.getparent()
        ctrl.getparent().remove(ctrl)
    for p in sec.iter(HP + "p"):
        s = ptext(p)
        if s.replace(" ", "").startswith(org_sample + "주간주요일정") and not any(local(c) == "tbl" for r in p.findall(HP + "run") for c in r):
            runs = [r for r in p.findall(HP + "run") if r.find(HP + "t") is not None]
            cp = runs[0].get("charPrIDRef")
            P.set_par_runs(p, [(cp, "{{org}} 주간 주요일정")])
    _mark_period(sec)
    P.strip_lineseg(sec)
    sanitize_package(pkg, extra_roots=[protos])
    os.makedirs(out_dir, exist_ok=True)
    pkg.save(os.path.join(out_dir, "template.hwpx"))
    with open(os.path.join(out_dir, "protos.xml"), "w", encoding="utf-8") as f:
        f.write(etree.tostring(protos, encoding="unicode"))
    form = {"id": "weekly_schedule", "name": "주간 주요일정 (표)", "audience": "주간 업무", "kind": "weekly_schedule",
            "note": "월~일 날짜별 표. 일정이 많으면 그날 줄이 늘어납니다", "row_height": row_h}
    with open(os.path.join(out_dir, "form.json"), "w", encoding="utf-8") as f:
        json.dump(form, f, ensure_ascii=False, indent=2)
    left = [t.text for t in sec.iter(HP + "t") if t.text and t.text.strip() and "{{" not in t.text]
    return {"id": "weekly_schedule", "leftover": left, "protos": ["daycell×%d" % len(by_day)], "style": {}}


def _mark_period(sec):
    """'【2026. 11. 9.(월) ~ 11. 15.(일)】' 가 여러 t 에 나뉘어 있어도 하나의 {{period}} 로 바꾼다."""
    ts = list(sec.iter(HP + "t"))
    for i, t in enumerate(ts):
        if t.text and "【" in t.text:
            pre = t.text.split("【")[0]
            acc = t.text
            j = i
            while "】" not in acc and j + 1 < len(ts):
                j += 1
                acc += ts[j].text or ""
            post = acc.split("】", 1)[1] if "】" in acc else ""
            t.text = pre + "【{{period}}】" + post
            for k in range(i + 1, j + 1):
                ts[k].text = ""
            return True
    return False


# =========================================================== 주요일정 표 생성
class WeeklyScheduleForm:
    def __init__(self, folder):
        self.folder = folder
        with open(os.path.join(folder, "form.json"), encoding="utf-8") as f:
            self.meta = json.load(f)
        with open(os.path.join(folder, "protos.xml"), encoding="utf-8") as f:
            root = P.parse_xml(f.read().encode("utf-8"))
        self.daycells = {e.get("day"): e[0] for e in root.findall("daycell")}
        self.cell = root.find("cell")[0]
        self.header = root.find("header")[0]
        self.footer = root.find("footer")[0]
        self.styles = {s.get("name"): [list(c) for c in s.findall("cell")] for s in root.findall("style")}
        self.bf = {b.get("pos"): b.get("ids").split(",") for b in root.findall("bf")}

    id = property(lambda s: s.meta["id"])
    name = property(lambda s: s.meta["name"])

    def _cell(self, pos, col, paras, texts, width_from):
        tc = clone(self.cell)
        tc.set("borderFillIDRef", self.bf[pos][col])
        sub = tc.find(HP + "subList")
        lines = texts if isinstance(texts, list) else [texts]
        for ln_i, ln in enumerate(lines):
            p = clone(paras[min(ln_i, len(paras) - 1)])
            runs = p.findall(HP + "run")
            if col == 1 and len(runs) >= 2 and ln:            # 일정명: '• ' + 이름
                specs = [(runs[0].get("charPrIDRef"), "• "), (runs[-1].get("charPrIDRef"), ln)]
            else:
                cp = runs[0].get("charPrIDRef") if runs else "0"
                for r in runs:
                    if "".join(t.text or "" for t in r.findall(HP + "t")).strip():
                        cp = r.get("charPrIDRef")
                specs = [(cp, ln)]
                if col == 1 and ln and not ln.startswith("•"):
                    specs = [(cp, "• " + ln)]
            P.set_par_runs(p, specs)
            sub.append(p)
        return tc

    def render(self, data):
        """data: {"org","start":"2026-11-09","events":[{date,time,title,place,dept,attend,note,important}], "min_rows":3}"""
        pkg = Package.load(os.path.join(self.folder, "template.hwpx"))
        sec = pkg.section(0)
        start = T.week_monday(T.parse_date(data["start"]))
        end = start + datetime.timedelta(days=6)
        period = "%s ~ %s" % (T.fmt_date_long(start), T.fmt_date_short(end))
        for t in sec.iter(HP + "t"):
            if t.text and "{{" in t.text:
                t.text = t.text.replace("{{org}}", data.get("org", "")).replace("{{period}}", period)
        big = next(t for t in sec.iter(HP + "tbl") if t.find(HP + "tr") is not None and t.find(HP + "tr").get("mark") == "ROWS")
        big.remove(big.find(HP + "tr"))
        by_date = collections.defaultdict(list)
        for ev in data.get("events", []):
            by_date[T.parse_date(ev["date"])].append(ev)
        min_rows = int(data.get("min_rows", 3))
        row_h = int(self.meta.get("row_height", 2400))
        rows = [clone(self.header)]
        total_h = int(self.header.find(HP + "tc").find(HP + "cellSz").get("height", "1700"))
        for di in range(7):
            day = start + datetime.timedelta(days=di)
            wd = T.WEEKDAYS[di]
            evs = sorted(by_date.get(day, []), key=lambda e: e.get("time") or "99:99")
            n = max(min_rows, len(evs))
            for i in range(n):
                pos = "first" if i == 0 else "last" if i == n - 1 else "mid"
                if n == 1:
                    pos = "last"
                ev = evs[i] if i < len(evs) else None
                tr = etree.Element(HP + "tr")
                if i == 0:
                    dc = clone(self.daycells[wd] if wd in self.daycells else next(iter(self.daycells.values())))
                    ps = dc.find(HP + "subList").findall(HP + "p")
                    texts = ["%d.%d." % (day.month, day.day), "(%s)" % wd]
                    for p, tx in zip(ps, texts):
                        r = p.find(HP + "run")
                        P.set_par_runs(p, [(r.get("charPrIDRef") if r is not None else "0", tx)])
                    for extra in ps[len(texts):]:
                        dc.find(HP + "subList").remove(extra)
                    dc.find(HP + "cellSpan").set("rowSpan", str(n))
                    dc.find(HP + "cellSz").set("height", str(row_h * n))
                    tr.append(dc)
                style = "weekend" if di >= 5 else ("important" if ev and ev.get("important") else "normal")
                cells = self.styles[style]
                if style == "normal" and not ev:
                    cells = self.styles["normal"]
                vals = ([ev.get("time", ""), ev.get("title", ""), ev.get("place", ""), ev.get("dept", ""),
                         ev.get("attend", ""), ev.get("note", "")] if ev else [""] * 6)
                if ev and ev.get("note"):
                    vals[5] = [x for x in re.split(r"\s*\n\s*", ev["note"]) if x] or [""]
                for col in range(6):
                    paras = cells[col] if col < len(cells) else cells[-1]
                    tc = self._cell(pos, col, paras, vals[col], None)
                    tc.find(HP + "cellSz").set("height", str(row_h))
                    tr.append(tc)
                rows.append(tr)
                total_h += row_h
        foot = clone(self.footer)
        rows.append(foot)
        total_h += int(foot.find(HP + "tc").find(HP + "cellSz").get("height", "0"))
        for tr in rows:
            big.append(tr)
        # rowSpan 으로 비는 열 보정 (날짜 칸이 있는 줄 다음 줄들은 colAddr 1부터)
        _fix_addresses(big)
        big.set("rowCnt", str(len(rows)))
        big.find(HP + "sz").set("height", str(total_h))
        P.strip_lineseg(sec)
        return pkg


def _fix_addresses(tbl):
    """rowSpan 으로 가려진 칸을 감안해 모든 칸의 (행, 열) 주소를 다시 계산."""
    occupied = set()
    for ri, tr in enumerate(tbl.findall(HP + "tr")):
        col = 0
        for tc in tr.findall(HP + "tc"):
            while (ri, col) in occupied:
                col += 1
            a = tc.find(HP + "cellAddr")
            a.set("rowAddr", str(ri))
            a.set("colAddr", str(col))
            sp = tc.find(HP + "cellSpan")
            cs, rs = int(sp.get("colSpan", "1")), int(sp.get("rowSpan", "1"))
            for r in range(ri, ri + rs):
                for c in range(col, col + cs):
                    occupied.add((r, c))
            col += cs


# =========================================================== 주간업무계획 글에서 일정 뽑기
_SYM = r"[\U000F0000-\U000FFFFF①-⑳❶-❿➊-➓㈀-㋿□■▣◆▶●○]"
_HEAD_RE = re.compile(r"^\s*(?:%s|\(?\d{1,2}[.)])\s*(.+?)\s*\(([^()]*)\)\s*$" % _SYM)
_KV_RE = re.compile(r"^\s*[%s]\s*([^:：]{1,12}?)\s*[:：]\s*(.*)$" % re.escape(T.BULLETS))
_DATE_RE = re.compile(r"(\d{4})\.\s*(\d{1,2})\.\s*(\d{1,2})\.?\s*(?:\(([월화수목금토일])\))?")
_TIME_RE = re.compile(r"(\d{1,2}):(\d{2})")


def split_org_sections(lines, orgs):
    """줄 목록에서 국 이름만 적힌 줄을 경계로 나눈다 → {국이름: [줄]}"""
    out = collections.OrderedDict()
    cur = None
    names = {re.sub(r"\s", "", o) for o in orgs}
    for ln in lines:
        key = re.sub(r"\s", "", ln)
        if key in names:
            cur = key
            out.setdefault(cur, [])
            continue
        if cur is not None:
            out[cur].append(ln)
    return out


def parse_plan_lines(lines):
    """주간업무계획 글 줄 → 항목 목록 (title, dept, staff, phone, date, time, end_time, place, lines)"""
    items = []
    cur = None
    for ln in lines:
        if not ln.strip():
            continue
        m = _HEAD_RE.match(ln)
        if m and not ln.lstrip().startswith(tuple(T.BULLETS)):
            who = m.group(2).split()
            cur = {"title": m.group(1).strip(), "dept": who[0] if who else "",
                   "staff": who[1] if len(who) > 1 else "", "phone": who[2] if len(who) > 2 else "", "lines": []}
            items.append(cur)
            continue
        k = _KV_RE.match(ln)
        if k and cur is not None:
            label, value = re.sub(r"\s", "", k.group(1)), k.group(2).strip()
            if label == "일시" and "date" not in cur:
                _fill_when(cur, value)
            elif label == "장소" and not cur.get("place"):
                cur["place"] = value
            else:
                cur["lines"].append((label, value))
    for it in items:
        if not it.get("place"):
            it["place"] = it.pop("place_from_when", "")
        it.pop("place_from_when", None)
    return items


def _fill_when(it, value):
    m = _DATE_RE.search(value)
    if not m:
        it["lines"].append(("일시", value))
        return
    it["date"] = "%04d-%02d-%02d" % (int(m[1]), int(m[2]), int(m[3]))
    rest = value[m.end():]
    head, _, tail = rest.partition("/")
    times = _TIME_RE.findall(head)
    if times:
        it["time"] = "%02d:%s" % (int(times[0][0]), times[0][1])
        if len(times) > 1 and "~" in head:
            it["end_time"] = "%02d:%s" % (int(times[1][0]), times[1][1])
    if tail.strip():
        it["place_from_when"] = tail.strip()
    note = re.search(r"\*.*$", head)
    if note:
        it["when_note"] = note.group(0).strip()


def extract_events(items, attend_default=""):
    """계획 항목 → 주요일정 표 행 (날짜 있는 것만)"""
    ev = []
    for it in items:
        if not it.get("date"):
            continue
        ev.append({"date": it["date"], "time": it.get("time", ""), "title": it.get("title", ""),
                   "place": it.get("place", ""), "dept": it.get("dept", ""),
                   "attend": it.get("attend", attend_default), "note": it.get("note", ""),
                   "important": bool(it.get("important"))})
    return ev


def plan_text_lines(pkg):
    """hwpx 에서 문단 글을 차례로 (표 칸 안 포함) 뽑아 줄 목록으로."""
    out = []
    for n in pkg.section_names():
        for p in pkg.xml(n).iter(HP + "p"):
            own = own_text(p)
            if own.strip():
                out.append(own)
            elif any(local(c) == "tbl" for r in p.findall(HP + "run") for c in r):
                pass
    return out


# =========================================================== 주간업무계획 표지
_PERIOD_RE = re.compile(r"\d{2}\.\s*\d{1,2}\.\s*\d{1,2}\.\s*~\s*\d{1,2}\.\s*\d{1,2}\.")


def register_cover(sample_path, out_dir, first_org="기획안전국"):
    """시 전체 주간업무계획의 표지(날짜 상자·로고·제목 배너·그림·순서 목차)만 따로 양식으로."""
    pkg = Package.load(sample_path)
    sec = pkg.section(0)
    ps = sec.findall(HP + "p")
    cut = None
    for i, p in enumerate(ps):
        if i > 0 and ptext(p).strip() == first_org and any(local(c) == "tbl" for r in p.findall(HP + "run") for c in r):
            cut = i
            break
    if cut is None:
        raise ValueError("표지 뒤 첫 국 이름표(%s)를 찾지 못했어요" % first_org)
    for p in ps[cut:]:
        sec.remove(p)
    done = False
    for t in sec.iter(HP + "t"):
        if t.text and _PERIOD_RE.search(t.text):
            t.text = _PERIOD_RE.sub("{{period}}", t.text)
            done = True
    if not done:
        raise ValueError("표지에서 날짜 상자를 찾지 못했어요")
    for tbl in sec.iter(HP + "tbl"):                      # 목차의 쪽 번호는 비운다
        for tc in tbl.iter(HP + "tc"):
            if ptext(tc).strip().isdigit():
                for t in tc.iter(HP + "t"):
                    t.text = ""
    P.strip_lineseg(sec)
    sanitize_package(pkg)
    os.makedirs(out_dir, exist_ok=True)
    pkg.save(os.path.join(out_dir, "template.hwpx"))
    form = {"id": "weekly_cover", "name": "주간업무계획 표지", "audience": "주간 업무", "kind": "weekly_cover",
            "note": "날짜 상자 + 제목 배너 + 그림 + 순서 목차"}
    with open(os.path.join(out_dir, "form.json"), "w", encoding="utf-8") as f:
        json.dump(form, f, ensure_ascii=False, indent=2)
    return {"id": "weekly_cover", "leftover": [], "protos": [], "style": {}}


class WeeklyCoverForm:
    def __init__(self, folder):
        self.folder = folder
        with open(os.path.join(folder, "form.json"), encoding="utf-8") as f:
            self.meta = json.load(f)

    id = property(lambda s: s.meta["id"])
    name = property(lambda s: s.meta["name"])

    def render(self, data):
        """data: {"start": "2026-11-09"} → 표지"""
        pkg = Package.load(os.path.join(self.folder, "template.hwpx"))
        start = T.week_monday(T.parse_date(data["start"]))
        period = T.fmt_range_short(start, start + datetime.timedelta(days=6))
        for t in pkg.section(0).iter(HP + "t"):
            if t.text and "{{period}}" in t.text:
                t.text = t.text.replace("{{period}}", period)
        return pkg
