# -*- coding: utf-8 -*-
"""한글(.hwp / .hwpx)·워드(.docx) 문서에서 글자만 뽑는다. 공문 읽기와 제출 파일 취합에 쓴다.
 - .hwpx / .docx : 압축(zip) 안의 XML 에서 문단 글자를 읽는다
 - .hwp (5.0)    : OLE 파일의 BodyText 를 풀어 문단 글자 레코드를 읽는다.
                   배포용(암호화) 문서는 본문 대신 '미리보기 글자'(PrvText, 앞부분만)를 읽는다
그 밖에 .hwpx 양식의 {부서} 같은 자리표시를 채워 새 파일로 저장하는 기능도 있다."""
import os
import re
import struct
import zipfile
import zlib
import xml.etree.ElementTree as ET

TEXT_EXT = (".hwp", ".hwpx", ".docx", ".txt")

_PARA_TEXT = 67           # HWPTAG_BEGIN(16) + 51
# 글자 자리 8칸(16바이트)을 차지하는 조판 부호 (표·그림·필드 등)
_WIDE = {1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23}


def _para_text(buf):
    out, i, n = [], 0, len(buf) - 1
    while i < n:
        c = buf[i] | (buf[i + 1] << 8)
        if c < 32:
            if c == 9:
                out.append("\t")
            elif c == 10:
                out.append("\n")
            elif c in (24, 30, 31):
                out.append(" " if c != 24 else "-")
            i += 16 if c in _WIDE else 2
            continue
        if 0xD800 <= c < 0xDC00 and i + 3 < len(buf):          # 두 칸짜리 글자(이모지 등)
            c2 = buf[i + 2] | (buf[i + 3] << 8)
            out.append(chr(0x10000 + ((c - 0xD800) << 10) + (c2 - 0xDC00)))
            i += 4
            continue
        out.append(chr(c))
        i += 2
    return "".join(out)


def _records(data):
    i, n = 0, len(data)
    while i + 4 <= n:
        h = struct.unpack_from("<I", data, i)[0]
        tag, size = h & 0x3FF, (h >> 20) & 0xFFF
        i += 4
        if size == 0xFFF:
            if i + 4 > n:
                break
            size = struct.unpack_from("<I", data, i)[0]
            i += 4
        yield tag, data[i:i + size]
        i += size


def _inflate(raw):
    d = zlib.decompressobj(-15)
    return d.decompress(raw) + d.flush()


def hwp_text(path):
    import olefile
    with olefile.OleFileIO(path) as ole:
        head = ole.openstream("FileHeader").read()
        if not head.startswith(b"HWP Document File"):
            raise ValueError("한글 문서가 아닙니다")
        prop = struct.unpack_from("<I", head, 36)[0]
        compressed, locked, dist = prop & 1, prop & 2, prop & 4
        if locked:
            raise ValueError("암호가 걸린 한글 문서는 읽을 수 없어요")
        secs = sorted((e for e in ole.listdir() if len(e) == 2 and e[0] == "BodyText" and e[1].startswith("Section")),
                      key=lambda e: int(re.sub(r"\D", "", e[1]) or 0))
        lines = []
        if not dist:
            for e in secs:
                raw = ole.openstream(e).read()
                data = _inflate(raw) if compressed else raw
                for tag, body in _records(data):
                    if tag == _PARA_TEXT:
                        lines.append(_para_text(body))
        if not any(l.strip() for l in lines) and ole.exists("PrvText"):      # 배포용 문서 등
            lines = ole.openstream("PrvText").read().decode("utf-16-le", "ignore").split("\x00")[0].replace("\r", "").split("\n")
            lines = [re.sub(r"[<>]", " ", l) for l in lines]
    return "\n".join(l.rstrip() for l in lines)


def _local(tag):
    return tag.rsplit("}", 1)[-1]


def _xml_paras(xml_bytes, para="p", text="t"):
    """XML 의 문단(p) 마다 글자(t)를 모아 한 줄로. 표 안의 문단은 따로 한 줄씩"""
    root = ET.fromstring(xml_bytes)
    lines = []

    def para_line(p):
        buf = []

        def gather(x, in_t):
            name = _local(x.tag)
            if name == para and x is not p:
                walk(x)
                return
            if name == text:
                buf.append(x.text or "")
                in_t = True
            elif name == "tab":
                buf.append("\t")
            elif name in ("lineBreak", "br", "cr"):
                buf.append("\n")
            for ch in x:
                gather(ch, in_t)
                if in_t and ch.tail:
                    buf.append(ch.tail)

        pos = len(lines)
        gather(p, False)
        lines.insert(pos, "".join(buf))

    def walk(el):
        if _local(el.tag) == para:
            para_line(el)
            return
        for ch in el:
            walk(ch)

    walk(root)
    return lines


def hwpx_text(path):
    with zipfile.ZipFile(path) as z:
        names = sorted((n for n in z.namelist() if re.match(r"Contents/section\d+\.xml$", n, re.I)),
                       key=lambda n: int(re.sub(r"\D", "", n.rsplit("/", 1)[-1]) or 0))
        lines = []
        for n in names:
            lines += _xml_paras(z.read(n))
        if not lines and "Preview/PrvText.txt" in z.namelist():
            lines = z.read("Preview/PrvText.txt").decode("utf-8", "ignore").splitlines()
    return "\n".join(l.rstrip() for l in lines)


def docx_text(path):
    with zipfile.ZipFile(path) as z:
        return "\n".join(_xml_paras(z.read("word/document.xml")))


def read_text(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".hwp":
        return hwp_text(path)
    if ext == ".hwpx":
        return hwpx_text(path)
    if ext == ".docx":
        return docx_text(path)
    if ext == ".txt":
        raw = open(path, "rb").read()
        for enc in ("utf-8-sig", "cp949", "utf-16"):
            try:
                return raw.decode(enc)
            except UnicodeDecodeError:
                pass
        return raw.decode("utf-8", "ignore")
    raise ValueError("읽을 수 없는 파일 형식: " + ext)


def fill_hwpx(src, dst, values):
    """hwpx 양식 안의 {부서} 같은 자리표시를 바꿔 새 파일로 저장. 바꾼 개수를 돌려준다"""
    count = 0
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w") as zout:
        for info in zin.infolist():
            data = zin.read(info.filename)
            if re.match(r"Contents/section\d+\.xml$", info.filename, re.I):
                s = data.decode("utf-8")
                for k, v in values.items():
                    key = "{" + k + "}"
                    count += s.count(key)
                    s = s.replace(key, _xml_escape(v))
                data = s.encode("utf-8")
            # mimetype 은 압축하지 않고 맨 앞에 두어야 한글이 연다
            ct = zipfile.ZIP_STORED if info.filename == "mimetype" else zipfile.ZIP_DEFLATED
            zout.writestr(info, data, compress_type=ct)
    return count


def _xml_escape(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
