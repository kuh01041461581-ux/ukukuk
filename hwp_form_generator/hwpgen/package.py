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
