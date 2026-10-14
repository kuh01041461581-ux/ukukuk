# -*- coding: utf-8 -*-
"""우편 라벨지 — 화면 없이 시험할 수 있는 계산 부분 (mm 기하 · 쪽 나누기 · 열 인식 · 텍스트 해석)

좌표는 모두 mm, 원점은 용지(A4 210×297) 왼쪽 위.  글자 크기 px 는 CSS px(1px = 25.4/96 mm) 기준이라
원본 웹 도구와 같은 값을 그대로 쓴다."""
from __future__ import annotations

import json
import math
import re
import urllib.parse
import urllib.request
from dataclasses import dataclass, field

from . import specs_data

PAGE_W, PAGE_H = 210.0, 297.0
PX = 25.4 / 96.0                 # CSS px → mm
KAKAO_KEY = "KakaoAK a1e32e809202e84389f8a7913da38fe1"   # 원본 도구가 쓰던 주소 검색 키


# ── 규격 ───────────────────────────────────────────────────────────
@dataclass(frozen=True)
class Geometry:
    """같은 크기의 칸이 규칙적으로 늘어선 라벨지 한 장"""
    cols: int
    rows: int
    w: float
    h: float
    col_gap: float
    row_gap: float
    pt: float
    pl: float

    @property
    def per_page(self) -> int:
        return self.cols * self.rows

    def cell_rect(self, i: int) -> tuple[float, float, float, float]:
        """칸 번호(0부터, 가로로 먼저) → (x, y, w, h) mm"""
        r, c = divmod(i, self.cols)
        return (self.pl + c * (self.w + self.col_gap), self.pt + r * (self.h + self.row_gap), self.w, self.h)

    def cell_at(self, x: float, y: float) -> int | None:
        """용지 위 좌표(mm) → 칸 번호. 칸 밖(여백·간격)이면 None"""
        for i in range(self.per_page):
            cx, cy, cw, ch = self.cell_rect(i)
            if cx <= x <= cx + cw and cy <= y <= cy + ch:
                return i
        return None

    @property
    def bottom_margin(self) -> float:
        return PAGE_H - self.pt - self.rows * self.h - max(0, self.rows - 1) * self.row_gap

    @property
    def right_margin(self) -> float:
        return PAGE_W - self.pl - self.cols * self.w - max(0, self.cols - 1) * self.col_gap


def safe_col_gap(cols: int, w: float, pl: float, declared: float) -> float:
    """선언한 열 간격과 '실제로 남는 폭' 중 작은 값 — 210mm 를 넘지 않게 (원본 getSafeColumnGapMM)"""
    if cols <= 1:
        return 0.0
    leftover = max(0.0, (PAGE_W - 2 * pl - cols * w) / (cols - 1))
    return min(declared, leftover)


def safe_row_gap(rows: int, h: float, pt: float, declared: float) -> float:
    if rows <= 1 or declared <= 0:
        return 0.0
    leftover = max(0.0, (PAGE_H - pt - rows * h) / (rows - 1))
    return min(declared, leftover)


@dataclass(frozen=True)
class AddressSpec:
    key: str
    maker: str
    model: str
    cols: int
    rows: int
    w: float
    h: float
    col_gap: float
    pt: float
    pl: float
    pad: tuple          # 안쪽 여백 위·오른쪽·아래·왼쪽 (mm)
    addr_px: float
    name_px: float
    digit_w_px: float
    digit_h_px: float
    digit_fs_px: float

    @property
    def per_page(self) -> int:
        return self.cols * self.rows

    @property
    def title(self) -> str:
        return f"{self.maker} {self.model}"

    def geometry(self, dy: float = 0.0, dx: float = 0.0) -> Geometry:
        """미세조정(dy: 아래로 +, dx: 오른쪽으로 +, mm)을 더한 기하"""
        pt, pl = self.pt + dy, self.pl + dx
        return Geometry(self.cols, self.rows, self.w, self.h, safe_col_gap(self.cols, self.w, pl, self.col_gap), 0.0, pt, pl)

    def info(self) -> str:
        return f"{self.w:g}×{self.h:g}mm · {self.cols}열×{self.rows}행 = {self.per_page}칸"


@dataclass(frozen=True)
class GeneralSpec:
    key: str
    maker: str
    model: str
    category: str
    cols: int
    rows: int
    w: float
    h: float
    col_gap: float
    row_gap: float
    pt: float
    pl: float
    vertical: bool = False

    @property
    def per_page(self) -> int:
        return self.cols * self.rows

    @property
    def title(self) -> str:
        return f"{self.model} · {self.w:g}×{self.h:g}mm · {self.per_page}칸"

    def geometry(self, dy: float = 0.0, dx: float = 0.0) -> Geometry:
        pt, pl = self.pt + dy, self.pl + dx
        return Geometry(self.cols, self.rows, self.w, self.h, safe_col_gap(self.cols, self.w, pl, self.col_gap),
                        safe_row_gap(self.rows, self.h, pt, self.row_gap), pt, pl)

    def info(self) -> str:
        return f"{self.category} · {self.w:g}×{self.h:g}mm · {self.cols}열×{self.rows}행 = {self.per_page}칸"


def _addr_specs() -> dict[str, AddressSpec]:
    out = {}
    for k, v in specs_data.ADDRESS_SPECS.items():
        out[k] = AddressSpec(k, *v)
    return out


def _gen_specs() -> dict[str, GeneralSpec]:
    out = {}
    for k, v in specs_data.GENERAL_SPECS.items():
        out[k] = GeneralSpec(k, *v)
    return out


ADDRESS_SPECS: dict[str, AddressSpec] = _addr_specs()
GENERAL_SPECS: dict[str, GeneralSpec] = _gen_specs()
DEFAULT_ADDRESS = "3107"
DEFAULT_GENERAL = "G_F_3107"


def by_maker(specs: dict) -> dict[str, list]:
    """제조사별(처음 나온 순서) 규격 묶음. 일반라벨은 칸 수가 적은 순"""
    g: dict[str, list] = {}
    for s in specs.values():
        g.setdefault(s.maker, []).append(s)
    return g


# ── 쪽 나누기 ──────────────────────────────────────────────────────
def paginate(n_items: int, per_page: int, start: int = 0) -> list[list]:
    """항목 n개를 칸에 채운다. 쪽마다 길이 per_page 의 목록 — 값은 항목 번호, 빈 칸/건너뛴 칸은 None.
       start = 첫 쪽에서 건너뛸(이미 쓴) 칸 수. 항목이 없어도 빈 1쪽은 돌려준다."""
    start = max(0, min(start, per_page - 1))
    total = start + n_items
    pages = max(1, math.ceil(total / per_page))
    cells: list = [None] * (pages * per_page)
    for k in range(n_items):
        cells[start + k] = k
    return [cells[i * per_page:(i + 1) * per_page] for i in range(pages)]


# ── 우편 라벨 한 장의 안쪽 배치(세로) ──────────────────────────────────
def space_between(heights: list[float], total: float) -> list[float]:
    """CSS flex 'space-between' — 각 요소의 위쪽 y. 넘치면 위에서부터 차례로 쌓는다."""
    n = len(heights)
    used = sum(heights)
    gap = (total - used) / (n - 1) if n > 1 and total > used else 0.0
    ys, y = [], 0.0
    for hgt in heights:
        ys.append(y)
        y += hgt + gap
    return ys


# ── 사람(받는 분) ───────────────────────────────────────────────────
@dataclass
class Recipient:
    name: str = ""
    addr: str = ""
    zip: str = ""

    def blank(self) -> bool:
        return not (self.name.strip() or self.addr.strip())


def norm_zip(v) -> str:
    """엑셀에서 온 우편번호 → 숫자 5자리 (앞자리 0 이 빠진 4자리는 복구). 알아볼 수 없으면 빈 문자열"""
    if v is None:
        return ""
    if isinstance(v, float) and v.is_integer():
        v = int(v)
    s = re.sub(r"\.0+$", "", str(v).strip())
    s = re.sub(r"\D", "", s)
    if not s:
        return ""
    if len(s) == 4:
        s = "0" + s
    return s if len(s) == 5 else ""


def honorific(name: str) -> str:
    name = (name or "").strip()
    if not name or name.endswith("귀하"):
        return name
    return name + " 귀하"


# ── 표 읽기 (엑셀/CSV) ────────────────────────────────────────────────
NAME_KW = ["이름", "성명", "민원인", "세대주", "수취인", "고객명", "name", "받는분", "수령인", "받는사람", "수신인", "주문자", "구매자", "발송대상", "고객", "member"]
ADDR_KW = ["주소", "도로명", "지번", "배송지", "address", "addr", "받는주소", "배송주소", "수취주소", "받는곳", "거주지", "납품지", "목적지", "delivery", "수령"]
ZIP_KW = ["우편번호", "우편", "zipcode", "zip", "postcode", "postal"]
_HDR_NAME = ["이름", "성명", "민원인", "세대주", "수취인", "고객명", "받는분"]
_HDR_ADDR = ["주소", "도로명", "지번", "수령", "배송지", "받는주소"]


def _clean(s) -> str:
    return re.sub(r"\s", "", "" if s is None else str(s)).lower()


def cell_text(v) -> str:
    if v is None:
        return ""
    if isinstance(v, float) and v.is_integer():
        v = int(v)
    return str(v).strip().replace("\r", "").replace("\n", " ")


def col_letter(i: int) -> str:
    s, n = "", i + 1
    while n > 0:
        n, m = divmod(n - 1, 26)
        s = chr(65 + m) + s
    return s


def find_header_row(rows: list[list]) -> int:
    """앞 15줄 중 '이름 계열'과 '주소 계열' 낱말이 함께 있는 줄. 못 찾으면 첫 번째 비어 있지 않은 줄"""
    for i in range(min(15, len(rows))):
        rs = _clean("".join(cell_text(c) for c in rows[i]))
        if any(k in rs for k in _HDR_NAME) and any(k in rs for k in _HDR_ADDR):
            return i
    for i, r in enumerate(rows):
        if any(cell_text(c) for c in r):
            return i
    return 0


def _pick_col(headers: list[str], kws: list[str], exclude: set) -> int | None:
    # 낱말 우선순위대로 (예: '주소' 가 '수령' 보다 먼저)
    for kw in kws:
        k = _clean(kw)
        for i, h in enumerate(headers):
            if i not in exclude and k in _clean(h):
                return i
    return None


@dataclass
class Table:
    headers: list[str]
    rows: list[list[str]]
    name_col: int | None = None
    addr_col: int | None = None
    zip_col: int | None = None
    header_row: int = 0

    def labels(self) -> list[str]:
        out = []
        for i, h in enumerate(self.headers):
            out.append(f"{h}  ({col_letter(i)}열)" if h else f"{col_letter(i)}열")
        return out

    def recipients(self, name_col=None, addr_col=None, zip_col=None) -> list[Recipient]:
        nc = self.name_col if name_col is None else name_col
        ac = self.addr_col if addr_col is None else addr_col
        zc = self.zip_col if zip_col is None else zip_col
        out = []
        for r in self.rows:
            def g(c):
                return r[c] if (c is not None and 0 <= c < len(r)) else ""
            rec = Recipient(cell_text(g(nc)), cell_text(g(ac)), norm_zip(g(zc)) if zc is not None else "")
            if not rec.blank():
                out.append(rec)
        return out


def parse_table(raw: list[list]) -> Table:
    """시트(2차원 목록) → 머리글·데이터 줄·열 자동 인식"""
    raw = [list(r) for r in raw]
    if not raw:
        return Table([], [])
    hr = find_header_row(raw)
    ncol = max((len(r) for r in raw[hr:]), default=0)
    hdr = [cell_text(c) for c in raw[hr]] + [""] * (ncol - len(raw[hr]))
    rows = []
    for r in raw[hr + 1:]:
        row = [cell_text(c) for c in r] + [""] * (ncol - len(r))
        if any(row):
            rows.append(row)
    t = Table(hdr, rows, header_row=hr)
    t.name_col = _pick_col(hdr, NAME_KW, set())
    ex = {t.name_col} if t.name_col is not None else set()
    t.addr_col = _pick_col(hdr, ADDR_KW, ex)
    ex2 = ex | ({t.addr_col} if t.addr_col is not None else set())
    t.zip_col = _pick_col(hdr, ZIP_KW, ex2)
    # 못 찾았으면 앞쪽 열을 추측으로 채운다 (사용자가 바꿀 수 있다)
    if t.name_col is None and ncol:
        t.name_col = 0
    if t.addr_col is None and ncol:
        t.addr_col = 1 if ncol > 1 and t.name_col == 0 else 0
    return t


def read_csv_rows(path: str) -> list[list]:
    """CSV/TSV — UTF-8(BOM) → CP949 순으로 시도"""
    import csv
    data = open(path, "rb").read()
    text = None
    for enc in ("utf-8-sig", "cp949", "euc-kr"):
        try:
            text = data.decode(enc)
            break
        except UnicodeDecodeError:
            continue
    if text is None:
        text = data.decode("utf-8", "replace")
    try:
        dialect = csv.Sniffer().sniff(text[:4096], delimiters=",\t;")
    except csv.Error:
        dialect = csv.excel
    return [list(r) for r in csv.reader(text.splitlines(), dialect)]


# ── 글자 붙여넣기에서 이름/주소 추측 (원본 parseTextForNameAddress) ──────────────
def _looks_name(s: str) -> bool:
    t = (s or "").strip()
    return 2 <= len(t) <= 12 and not re.search(r"\s", t) and bool(re.search(r"[가-힣a-zA-Z]", t))


def _looks_addr(s: str) -> bool:
    t = (s or "").strip()
    return len(t) >= 4 and bool(re.search(r"(시|도|군|구|읍|면|동|로|길|번지|아파트|빌딩|층|호)", t))


def _score(rows: list[Recipient]) -> float:
    if not rows:
        return -1
    ok = sum(1 for r in rows if _looks_name(r.name) and _looks_addr(r.addr))
    return ok / len(rows)


def parse_text(text: str) -> list[Recipient]:
    lines = [l.strip() for l in re.split(r"\r\n|\r|\n", text) if l.strip()]
    if not lines:
        return []
    cands: list[list[Recipient]] = []
    if any("\t" in l for l in lines):
        c = []
        for l in lines:
            p = [x.strip() for x in l.split("\t") if x.strip()]
            c.append(Recipient(p[0] if p else "", " ".join(p[1:])))
        cands.append(c)
    if any(re.search(r"\s{2,}", l) for l in lines):
        c = []
        for l in lines:
            p = [x.strip() for x in re.split(r"\s{2,}", l) if x.strip()]
            c.append(Recipient(p[0] if p else "", " ".join(p[1:])))
        cands.append(c)
    if any("," in l for l in lines):
        c = []
        for l in lines:
            idx = l.find(",")
            if idx < 0:
                c.append(Recipient("", l))
                continue
            first, rest = l[:idx].strip(), l[idx + 1:].strip()
            c.append(Recipient(first, rest) if _looks_name(first) else Recipient("", l))
        cands.append(c)
    blocks = [b.strip() for b in re.split(r"\r?\n\s*\r?\n+", text) if b.strip()]
    if len(blocks) >= 2:
        c = []
        for b in blocks:
            bl = [s.strip() for s in b.splitlines() if s.strip()]
            if len(bl) >= 2:
                c.append(Recipient(bl[0], " ".join(bl[1:])))
            elif bl:
                c.append(Recipient("", bl[0]))
        cands.append(c)
    if len(lines) >= 2 and len(lines) % 2 == 0:
        cands.append([Recipient(lines[i], lines[i + 1]) for i in range(0, len(lines), 2)])
    best, best_s = None, -1.0
    for c in cands:
        s = _score(c)
        if s > best_s:
            best, best_s = c, s
    if best is None or best_s <= 0:
        best = [Recipient("", l) for l in lines]
    return [r for r in best if r.name or r.addr]


# ── 우편번호 조회 (카카오 주소 검색) ───────────────────────────────────
def lookup_zip(addr: str, key: str = KAKAO_KEY, timeout: float = 8.0, retries: int = 2) -> str:
    """도로명/지번 주소 → 5자리 우편번호. 실패하면 빈 문자열"""
    addr = (addr or "").strip()
    if not addr:
        return ""
    url = "https://dapi.kakao.com/v2/local/search/address.json?query=" + urllib.parse.quote(addr)
    for attempt in range(retries + 1):
        try:
            req = urllib.request.Request(url, headers={"Authorization": key or KAKAO_KEY})
            with urllib.request.urlopen(req, timeout=timeout) as r:
                data = json.loads(r.read().decode("utf-8"))
            d = (data.get("documents") or [None])[0]
            if not d:
                return ""
            z = ((d.get("road_address") or {}).get("zone_no")) or ((d.get("address") or {}).get("zone_no")) or ""
            return norm_zip(z)
        except Exception:
            if attempt >= retries:
                return ""
    return ""


# ── 일반 라벨 ─────────────────────────────────────────────────────────
@dataclass
class GeneralEntry:
    text: str = ""
    repeat: int = 1


def expand_entries(entries: list[GeneralEntry]) -> list[str]:
    out: list[str] = []
    for e in entries:
        out.extend([e.text] * max(0, int(e.repeat)))
    return out


def vertical_lines(text: str) -> list[str]:
    """세로쓰기 — 글자를 한 줄에 하나씩 위에서 아래로 (줄바꿈은 열 구분 없이 무시)"""
    return [ch for ch in text if ch not in "\r\n" and not ch.isspace()] or [""]


# ── 저장할 설정 기본값 ──────────────────────────────────────────────────
@dataclass
class AddressStyle:
    family: str = "Noto Sans KR"
    mult: float = 1.0
    bold: bool = False
    italic: bool = False
    underline: bool = False
    align: str = "default"          # default | left | center | right
    color: str = ""                 # 빈 값 = 기본(주소 #222, 이름 #000)
    honorific: bool = True
    show_zip: bool = True
    watermark: bool = True
    wm_opacity: float = 0.10
    autoshrink: bool = True


@dataclass
class GeneralStyle:
    family: str = "Noto Sans KR"
    size_pt: float = 12.0
    bold: bool = False
    italic: bool = False
    underline: bool = False
    color: str = "#000000"
    align_h: str = "center"
    align_v: str = "middle"
    line_height: float = 1.25
    letter_spacing: float = 0.0     # pt
    vertical: bool = False
    autofit: bool = True


def style_to_dict(s) -> dict:
    return dict(s.__dict__)


def style_from_dict(cls, d: dict | None):
    s = cls()
    for k, v in (d or {}).items():
        if hasattr(s, k) and isinstance(v, type(getattr(s, k))):
            setattr(s, k, v)
    return s
