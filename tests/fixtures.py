# -*- coding: utf-8 -*-
"""시험용 가짜 문서 만들기: 실제 공문·양식은 저장소에 넣지 않고, 시험할 때마다 지어낸 내용으로 만든다."""
import struct
import zipfile
import zlib

SECT = 512
END, FREE, FATSECT = 0xFFFFFFFE, 0xFFFFFFFF, 0xFFFFFFFD


def _dir_entry(name, typ, left=FREE, right=FREE, child=FREE, start=END, size=0):
    n = (name + "\0").encode("utf-16-le")
    e = n.ljust(64, b"\0") + struct.pack("<HBB", len(n), typ, 1) + struct.pack("<III", left, right, child)
    e += b"\0" * 16 + b"\0" * 4 + b"\0" * 16 + struct.pack("<III", start, size, 0)
    assert len(e) == 128
    return e


def write_cfb(path, streams):
    """아주 단순한 OLE(복합 문서) 파일 쓰기. streams: [(경로 튜플, bytes)] — 모두 4096바이트 이상으로 채운다"""
    blobs = []
    for p, data in streams:
        if len(data) < 4096:
            data = data + b"\0" * (4096 - len(data))
        blobs.append((p, data))
    # 디렉터리: 0 Root, 이후 항목들 (저장소는 BodyText 하나만 지원)
    entries = [["Root Entry", 5, FREE, FREE, FREE, END, 0]]
    storages = {}
    for p, data in blobs:
        if len(p) == 2 and p[0] not in storages:
            storages[p[0]] = len(entries)
            entries.append([p[0], 1, FREE, FREE, FREE, 0, 0])
    sid_of = {}
    for p, data in blobs:
        sid_of[p] = len(entries)
        entries.append([p[-1], 2, FREE, FREE, FREE, 0, len(data)])
    # 형제 사슬: 루트 아래 / 저장소 아래 각각 오른쪽으로 이어 붙인다
    groups = {None: []}
    for p, _ in blobs:
        groups.setdefault(p[0] if len(p) == 2 else None, []).append(sid_of[p])
    for name, sid in storages.items():
        groups[None].append(sid)
    for key, sids in groups.items():
        parent = 0 if key is None else storages[key]
        entries[parent][4] = sids[0]
        for a, b in zip(sids, sids[1:]):
            entries[a][3] = b
    n_dir = (len(entries) * 128 + SECT - 1) // SECT
    data_secs = [(len(d) + SECT - 1) // SECT for _, d in blobs]
    total = n_dir + sum(data_secs)
    n_fat = 1
    while (total + n_fat) > n_fat * (SECT // 4):
        n_fat += 1
    fat = [FATSECT] * n_fat
    first_dir = len(fat)
    fat += [first_dir + i + 1 for i in range(n_dir - 1)] + [END]
    starts = []
    for cnt in data_secs:
        s0 = len(fat)
        starts.append(s0)
        fat += [s0 + i + 1 for i in range(cnt - 1)] + [END]
    fat += [FREE] * (n_fat * (SECT // 4) - len(fat))
    for (p, d), st in zip(blobs, starts):
        entries[sid_of[p]][5] = st
    head = bytes.fromhex("D0CF11E0A1B11AE1") + b"\0" * 16 + struct.pack("<HHHHH", 0x3E, 3, 0xFFFE, 9, 6) + b"\0" * 6
    head += struct.pack("<IIIIIIIII", 0, n_fat, first_dir, 0, 4096, END, 0, END, 0)
    difat = list(range(n_fat)) + [FREE] * (109 - n_fat)
    head += struct.pack("<109I", *difat)
    assert len(head) == 512
    out = bytearray(head)
    out += struct.pack(f"<{len(fat)}I", *fat)
    dirs = b"".join(_dir_entry(*e) for e in entries)
    out += dirs.ljust(n_dir * SECT, b"\0")
    for (_, d), cnt in zip(blobs, data_secs):
        out += d.ljust(cnt * SECT, b"\0")
    open(path, "wb").write(bytes(out))


def _rec(tag, body, level=0):
    size = len(body)
    if size >= 0xFFF:
        return struct.pack("<II", tag | (level << 10) | (0xFFF << 20), size) + body
    return struct.pack("<I", tag | (level << 10) | (size << 20)) + body


def _ptext(s, table=False):
    b = b""
    if table:          # 표 조판 부호(8칸) 를 문단 앞에 끼워 넣어 건너뛰기 시험
        b += struct.pack("<H", 11) + b"tbl " + b"\0" * 8 + struct.pack("<H", 11)
    b += s.replace("\t", "\x09").encode("utf-16-le") if "\t" not in s else b""
    if "\t" in s:
        a, c = s.split("\t", 1)
        b += a.encode("utf-16-le") + struct.pack("<H", 9) + b"\0" * 12 + struct.pack("<H", 9) + c.encode("utf-16-le")
    return b + struct.pack("<H", 13)


def make_hwp(path, paragraphs, compressed=True, distribution=False, preview=None):
    body = b"".join(_rec(66, b"\0" * 22) + _rec(67, _ptext(p, table=(i == 2))) for i, p in enumerate(paragraphs))
    if compressed:
        c = zlib.compressobj(9, zlib.DEFLATED, -15)
        body = c.compress(body) + c.flush()
    prop = (1 if compressed else 0) | (4 if distribution else 0)
    head = b"HWP Document File".ljust(32, b"\0") + struct.pack("<II", 0x05000300, prop)
    head = head.ljust(256, b"\0")
    streams = [(("FileHeader",), head), (("BodyText", "Section0"), body if not distribution else b"\x01" * 600)]
    if preview is not None:
        streams.append((("PrvText",), preview.encode("utf-16-le")))
    write_cfb(path, streams)


HWPX_NS = 'xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph" xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section"'


def make_hwpx(path, paragraphs, table_rows=None):
    def p(t):
        t = t.replace("&", "&amp;").replace("<", "&lt;")
        if "\t" in t:
            a, b = t.split("\t", 1)
            return f'<hp:p><hp:run><hp:t>{a}<hp:tab width="100"/>{b}</hp:t></hp:run></hp:p>'
        return f"<hp:p><hp:run><hp:t>{t}</hp:t></hp:run></hp:p>"
    body = "".join(p(t) for t in paragraphs)
    if table_rows:
        cells = "".join("<hp:tr>" + "".join(f"<hp:tc><hp:subList>{p(c)}</hp:subList></hp:tc>" for c in r) + "</hp:tr>"
                        for r in table_rows)
        body += f'<hp:p><hp:run><hp:tbl>{cells}</hp:tbl><hp:t>표 끝</hp:t></hp:run></hp:p>'
    xml = f'<?xml version="1.0" encoding="UTF-8"?><hs:sec {HWPX_NS}>{body}</hs:sec>'
    with zipfile.ZipFile(path, "w") as z:
        z.writestr("mimetype", "application/hwp+zip", compress_type=zipfile.ZIP_STORED)
        z.writestr("Contents/section0.xml", xml)
        z.writestr("Contents/content.hpf", "<opf/>")


def make_docx(path, paragraphs):
    ns = 'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"'
    body = "".join(f"<w:p><w:r><w:t>{t}</w:t></w:r></w:p>" for t in paragraphs)
    with zipfile.ZipFile(path, "w") as z:
        z.writestr("word/document.xml", f'<?xml version="1.0"?><w:document {ns}><w:body>{body}</w:body></w:document>')
