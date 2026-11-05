# -*- coding: utf-8 -*-
"""한글(.hwpx) 묶음 읽기·쓰기와 XML 도우미.

.hwpx 는 zip 안에 header.xml(글자·문단 모양 정의), section*.xml(본문), BinData(그림) 이 들어 있는 형식이다.
이 프로그램은 "양식 파일의 서식 번호(charPrIDRef·paraPrIDRef·borderFillIDRef)를 그대로 가져다 쓰는" 방식이라
글꼴·글자 크기·자간·들여쓰기가 사용자의 원본 양식과 똑같이 나온다.
"""
import copy
import io
import re
import zipfile

from lxml import etree

NS = {
    "hp": "http://www.hancom.co.kr/hwpml/2011/paragraph",
    "hh": "http://www.hancom.co.kr/hwpml/2011/head",
    "hc": "http://www.hancom.co.kr/hwpml/2011/core",
    "hs": "http://www.hancom.co.kr/hwpml/2011/section",
    "opf": "http://www.idpf.org/2007/opf/",
}
HP = "{%s}" % NS["hp"]
HH = "{%s}" % NS["hh"]
HC = "{%s}" % NS["hc"]
OPF = "{%s}" % NS["opf"]

XML_DECL = b'<?xml version="1.0" encoding="UTF-8" standalone="yes" ?>'


def local(el):
    return etree.QName(el).localname


def parse_xml(data):
    parser = etree.XMLParser(remove_blank_text=False, resolve_entities=False, huge_tree=True)
    return etree.fromstring(data, parser)


def dump_xml(root):
    return XML_DECL + etree.tostring(root, encoding="UTF-8", xml_declaration=False)


class Package:
    """.hwpx 파일 하나. 수정한 XML 은 save() 때 다시 쓴다."""

    def __init__(self, files=None):
        self.files = dict(files or {})          # 이름 -> bytes (원래 순서 유지)
        self._xml = {}

    # ---- 읽기/쓰기 ----
    @classmethod
    def load(cls, path_or_bytes):
        src = io.BytesIO(path_or_bytes) if isinstance(path_or_bytes, (bytes, bytearray)) else path_or_bytes
        with zipfile.ZipFile(src) as z:
            files = {n: z.read(n) for n in z.namelist() if not n.endswith("/")}
        if "Contents/header.xml" not in files:
            raise ValueError("한글(hwpx) 문서가 아닙니다 — .hwp 는 한글에서 'hwpx 로 저장'한 파일을 써 주세요")
        return cls(files)

    def xml(self, name):
        if name not in self._xml:
            self._xml[name] = parse_xml(self.files[name])
        return self._xml[name]

    def header(self):
        return self.xml("Contents/header.xml")

    def section_names(self):
        names = [n for n in self.files if re.fullmatch(r"Contents/section\d+\.xml", n)]
        return sorted(names, key=lambda n: int(re.findall(r"\d+", n)[0]))

    def section(self, i=0):
        return self.xml(self.section_names()[i])

    def to_bytes(self):
        for name, root in self._xml.items():
            self.files[name] = dump_xml(root)
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as z:
            order = ["mimetype"] + [n for n in self.files if n != "mimetype"]
            for n in order:
                if n not in self.files:
                    continue
                info = zipfile.ZipInfo(n, date_time=(2026, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_STORED if n == "mimetype" else zipfile.ZIP_DEFLATED
                z.writestr(info, self.files[n])
        return buf.getvalue()

    def save(self, path):
        data = self.to_bytes()
        with open(path, "wb") as f:
            f.write(data)
        return path

    def copy(self):
        self.files.update({n: dump_xml(r) for n, r in self._xml.items()})
        return Package(self.files)


# ---------------------------------------------------------------- 문단/글자 도우미
def ptext(el):
    """문단(또는 칸) 안의 글자 전부 (표 안 글자 포함)."""
    return "".join(t.text or "" for t in el.iter(HP + "t"))


def own_text(p):
    """문단 자기 글자만 (안에 든 표·글상자 글자는 빼고)."""
    out = []
    for run in p.findall(HP + "run"):
        for t in run.findall(HP + "t"):
            out.append(t.text or "")
    return "".join(out)


def direct_runs(p):
    return p.findall(HP + "run")


def strip_lineseg(root):
    """줄 배치 캐시(linesegarray)를 지운다. 한글이 열 때 다시 계산한다."""
    for el in list(root.iter(HP + "linesegarray")):
        el.getparent().remove(el)


def remove_children(el, names=()):
    for c in list(el):
        if local(c) in names:
            el.remove(c)


def set_run_text(run, text):
    """run 안의 첫 t 에 글자를 넣고 나머지 t 는 지운다 (표·그림 같은 다른 자식은 그대로)."""
    ts = run.findall(HP + "t")
    if not ts:
        t = etree.SubElement(run, HP + "t")
        ts = [t]
    ts[0].text = text
    for extra in ts[1:]:
        run.remove(extra)
    # t 안에 낀 부호(탭 등) 제거
    for c in list(ts[0]):
        ts[0].remove(c)


def make_run(charpr, text):
    run = etree.Element(HP + "run")
    run.set("charPrIDRef", str(charpr))
    t = etree.SubElement(run, HP + "t")
    t.text = text
    return run


def set_par_runs(p, specs):
    """문단의 글자 run 들을 specs=[(charPr, text), ...] 로 갈아 끼운다.
    표·그림·도형처럼 글자가 아닌 것을 품은 run 은 남기고 그 앞에 글자 run 을 둔다."""
    keep = []
    for run in p.findall(HP + "run"):
        if any(local(c) not in ("t",) for c in run):
            for t in run.findall(HP + "t"):
                run.remove(t)
            keep.append(run)
        else:
            p.remove(run)
    insert_at = 0
    for i, c in enumerate(list(p)):
        if local(c) == "linesegarray":
            insert_at = i
            break
    else:
        insert_at = len(p)
    for k, (cp, text) in enumerate(specs):
        p.insert(insert_at + k, make_run(cp, text))


def clone(el):
    return copy.deepcopy(el)


_ID_TAGS = {"tbl", "container", "pic", "rect", "ellipse", "line", "arc", "polygon", "curve", "connectLine",
            "textart", "ole", "equation", "video", "chart"}


class IdGen:
    """표·그림 같은 개체의 id 가 겹치지 않게 새로 나눠 준다."""

    def __init__(self, root=None, start=1_000_000_000):
        self.n = start
        if root is not None:
            for el in root.iter():
                v = el.get("id")
                if v and v.isdigit() and local(el) in _ID_TAGS:
                    self.n = max(self.n, int(v) + 1)

    def next(self):
        self.n += 1
        return str(self.n)

    def renew(self, el):
        for e in el.iter():
            if local(e) in _ID_TAGS and e.get("id") is not None:
                e.set("id", self.next())
            if e.get("instid") is not None:
                e.set("instid", self.next())
        return el


def char_heights(header):
    """charPr id -> 글자 크기(1/100 pt)"""
    return {c.get("id"): int(c.get("height", "0")) for c in header.iter(HH + "charPr")}


def parse_fragment(xml_text):
    """네임스페이스 선언 없이 저장해 둔 조각 XML 을 다시 읽는다."""
    decl = " ".join('xmlns:%s="%s"' % (k, v) for k, v in NS.items())
    wrap = "<root %s>%s</root>" % (decl, xml_text)
    root = parse_xml(wrap.encode("utf-8"))
    return list(root)


def serialize_fragment(el):
    s = etree.tostring(el, encoding="unicode")
    # 루트에 붙은 네임스페이스 선언은 지운다 (parse_fragment 가 다시 붙여 줌)
    s = re.sub(r'\sxmlns:\w+="[^"]+"', "", s)
    return s


def derive_charpr(pkg, cid, bold=None, shade_none=False):
    """글자 모양 cid 를 바탕으로 굵게 켜기/끄기(·바탕색 제거)만 다른 새 글자 모양을 header.xml 에 더하고 번호를 돌려준다."""
    hdr = pkg.header()
    props = next(hdr.iter(HH + "charProperties"))
    items = props.findall(HH + "charPr")
    src = next(c for c in items if c.get("id") == cid)
    has_bold = src.find(HH + "bold") is not None
    need_shade = shade_none and src.get("shadeColor") not in (None, "none")
    if (bold is None or bold == has_bold) and not need_shade:
        return cid
    for c in items:                                   # 이미 같은 파생이 있으면 재사용
        if c.get("_src") == cid and (c.find(HH + "bold") is not None) == (has_bold if bold is None else bold):
            return c.get("id")
    new = clone(src)
    nid = str(max(int(c.get("id")) for c in items) + 1)
    new.set("id", nid)
    if shade_none:
        new.set("shadeColor", "none")
    b = new.find(HH + "bold")
    if bold is False and b is not None:
        new.remove(b)
    if bold is True and b is None:
        new.insert(0, etree.Element(HH + "bold"))
        # bold 는 fontRef/ratio/spacing/relSz/offset 뒤, underline 앞이 원칙이지만 한글은 자식 순서를 관대하게 읽는다
        new.remove(new[0])
        ul = new.find(HH + "underline")
        el = etree.Element(HH + "bold")
        if ul is not None:
            ul.addprevious(el)
        else:
            new.append(el)
    props.append(new)
    props.set("itemCnt", str(len(props.findall(HH + "charPr"))))
    return nid


# ---------------------------------------------------------------- 열린 문서에 넣을 때 쓰는 가공
def page_setup_of(pkg):
    """구역 설정(secPr)의 용지·여백 → dict (없으면 None)"""
    sec = pkg.section(0)
    pp = next(sec.iter(HP + "pagePr"), None)
    if pp is None:
        return None
    mg = pp.find(HP + "margin")
    d = {"width": int(pp.get("width")), "height": int(pp.get("height")), "landscape": pp.get("landscape") != "WIDELY"
         and pp.get("landscape") == "NARROWLY"}
    for k in ("left", "right", "top", "bottom", "header", "footer", "gutter"):
        d[k] = int(mg.get(k, "0")) if mg is not None else 0
    return d


def _has_content(p):
    if any((t.text or "").strip() for t in p.iter(HP + "t")):
        return True
    return any(local(e) in ("tbl", "pic", "container", "ole", "equation", "video", "chart") for e in p.iter())


def strip_section_props(pkg):
    """열려 있는 문서 중간에 끼워 넣을 때 쪽 설정(secPr)이 같이 들어가 쪽·여백이 바뀌지 않게 빼 준다.
    비어 버린 문단(구역 설정만 싣고 있던 문단)은 지운다."""
    sec = pkg.section(0)
    for run in sec.iter(HP + "run"):
        if run.find(HP + "secPr") is not None:
            for c in list(run):
                if local(c) == "secPr" or (local(c) == "ctrl" and c.find(HP + "colPr") is not None):
                    run.remove(c)
    for i, p in enumerate(list(sec.findall(HP + "p"))):
        if i == 0 and not _has_content(p) and len(sec.findall(HP + "p")) > 1:
            sec.remove(p)
    return pkg


def _empty_like_para(src):
    """src 와 같은 문단 모양(정렬·들여쓰기·간격·쪽 나눔)을 가진 빈 문단 — 이어 넣을 때 '받침 문단'으로 쓴다"""
    d = copy.deepcopy(src)
    cp = None
    for run in d.findall(HP + "run"):
        if cp is None:
            cp = run.get("charPrIDRef")
        d.remove(run)
    for ls in list(d.iter(HP + "linesegarray")):
        ls.getparent().remove(ls)
    run = etree.Element(HP + "run")
    run.set("charPrIDRef", cp or "0")
    etree.SubElement(run, HP + "t")
    d.insert(0, run)
    return d


def split_chunks(pkg):
    """본문 문단을 '차례대로 입력되는 느낌'으로 넣기 위해 문단 단위 조각으로 나눈다.
    - 빈 문단은 앞 조각에 붙인다.
    - 조각 끝마다 '다음 조각 첫 문단과 같은 모양의 빈 문단'을 붙인다. 한글은 끼워 넣는 파일의 첫 문단을 커서가 있는
      문단에 합치므로, 이 받침 문단에 다음 조각이 합쳐지면 정렬·들여쓰기가 앞 문단에서 새지 않고 줄도 붙지 않는다.
    각 조각은 같은 서식 정의(header)를 가진 독립 문서."""
    pkg.files.update({n: dump_xml(r) for n, r in pkg._xml.items()})
    sec = pkg.section(0)
    ps = sec.findall(HP + "p")
    groups = []
    for p in ps:
        if groups and not _has_content(p):
            groups[-1].append(p)
        else:
            groups.append([p])
    if len(groups) > 1 and not any(_has_content(q) for q in groups[0]):
        groups[1] = groups[0] + groups[1]          # 쪽 설정만 든 빈 첫 문단은 다음 조각에 붙인다(맨 위에 빈 줄이 생기지 않게)
        groups.pop(0)
    out = []
    for gi, g in enumerate(groups):
        clone_pkg = Package(dict(pkg.files))
        s2 = clone_pkg.section(0)
        for q in list(s2.findall(HP + "p")):
            s2.remove(q)
        for q in g:
            s2.append(copy.deepcopy(q))
        if gi + 1 < len(groups):
            s2.append(_empty_like_para(groups[gi + 1][0]))
        out.append(clone_pkg)
    return out


def with_lead_blank(pkg):
    """내용이 있는 문서에 이어 넣을 때: 앞 문단과 한 줄로 붙지 않게 맨 앞에 빈 문단을 하나 둔다(받침)"""
    sec = pkg.section(0)
    ps = sec.findall(HP + "p")
    if not ps:
        return pkg
    blank = _empty_like_para(ps[0])
    blank.set("pageBreak", "0")
    ps[0].addprevious(blank)
    return pkg
