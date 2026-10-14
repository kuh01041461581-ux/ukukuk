# -*- coding: utf-8 -*-
"""공과금 고지서 AI 인식 — 종류별 설정 · 이미지 준비 · Gemini/Claude 호출 · 결과 매핑 · 거래처입력 엑셀"""
import base64
import io
import json
import re
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field

from PIL import Image, ImageOps

from . import data as D
from ...core import excel as X

HOLDER_MAX = 10
FIELD_KEYS = ["name", "type", "bizno", "birth", "paytype", "bank", "holder", "account", "supply", "vat", "deduct", "payout",
              "giro", "cms", "repFlag", "repName", "zip", "addr1", "addr2", "tel", "mobile", "memo"]
NUMERIC_KEYS = ["supply", "vat", "deduct", "payout"]
FIELD_WIDTH = dict(name=130, type=90, bizno=100, birth=80, paytype=90, bank=130, holder=110, account=150, supply=90, vat=80, deduct=70,
                   payout=90, giro=90, cms=90, repFlag=60, repName=90, zip=70, addr1=130, addr2=130, tel=100, mobile=100, memo=150)
GEMINI_MODELS = [("gemini-3.6-flash", "gemini-3.6-flash (권장, 최신)"),
                 ("gemini-3.1-flash-lite", "gemini-3.1-flash-lite (무료 한도 가장 넉넉함)"),
                 ("gemini-2.5-flash", "gemini-2.5-flash (2026-10 종료 예정)")]
CLAUDE_MODEL = "claude-sonnet-5"


def clamp_holder(name) -> str:
    s = "" if name is None else str(name)
    return s[:HOLDER_MAX]


def kepco_holder(agency) -> str:
    """'한전(기관명)' — 괄호 앞까지 항상 10자(짧으면 공백으로 채움)"""
    prefix, suffix = "한전(", ")"
    budget = HOLDER_MAX - len(prefix) - len(suffix)
    if not agency:
        return clamp_holder("한국전력공사")
    a = str(agency)
    a = a[:budget] if len(a) > budget else a.ljust(budget)
    return f"{prefix}{a}{suffix}"


def _digits(v) -> str:
    return re.sub(r"\D", "", str(v or ""))


def _num(v) -> str:
    return re.sub(r"[^0-9.-]", "", str(v or ""))


TELECOM_ISSUER = {"SK": dict(name="SK", bizno="104-81-37225", holder="SKT 익산시청"),
                  "KT": dict(name="KT", bizno="102-81-42945", holder="익산시청(KT)")}


def _map_water(row, r):
    holder_name = str(r.get("수용가명") or "").strip()
    row.data["holder"] = clamp_holder(holder_name or row.data.get("holder"))
    row.data["account"] = str(r.get("농협계좌번호") or "").strip()
    row.data["supply"] = _num(r.get("총납부금액"))
    row.data["memo"] = f"상하수도요금({r['사용기간']})" if r.get("사용기간") else "상하수도요금"
    bits = []
    if r.get("수전번호"):
        bits.append(f"수전번호 {r['수전번호']}")
    if r.get("수용가명"):
        bits.append(r["수용가명"])
    if r.get("비고"):
        bits.append(r["비고"])
    row.ai_memo = " · ".join(bits)
    return [(bool(row.data["account"]), "농협 가상계좌번호를 찾지 못했습니다. 표를 직접 확인해 입력하거나 재인식하세요."),
            (bool(row.data["supply"]), "총납부금액을 찾지 못했습니다. 직접 입력하거나 재인식하세요."),
            (bool(holder_name), "수용가명(예금주명)을 찾지 못해 기본값을 사용했습니다. 표를 직접 확인해 입력하세요.")]


def _map_kepco(row, r):
    row.data["holder"] = kepco_holder(r.get("수신기관"))
    row.data["account"] = str(r.get("농협계좌번호") or "").strip()
    ibk = _digits(r.get("기업은행계좌번호"))
    lbl = str(r.get("농협_은행표기") or "")
    mixed = bool(row.data["account"]) and ((ibk and _digits(row.data["account"]) == ibk) or (lbl and not re.search("농|NH", lbl, re.I)))
    if mixed:
        row.data["account"] = ""
    row.data["supply"] = _num(r.get("청구금액"))
    row.data["memo"] = f"전기요금({r['사용기간']})" if r.get("사용기간") else "전기요금"
    bits = [b for b in (r.get("비고"), f"고객번호 {r['고객번호']}" if r.get("고객번호") else "") if b]
    row.ai_memo = " · ".join(bits)
    return [(not mixed, "기업은행 줄을 농협으로 잘못 읽은 것 같아 계좌번호를 비웠습니다. 고지서의 농협 줄을 직접 확인해 입력하거나 재인식하세요."),
            (bool(row.data["account"]), "농협 가상계좌번호를 찾지 못했습니다. 표를 직접 확인해 입력하거나 재인식하세요."),
            (bool(row.data["supply"]), "청구금액을 찾지 못했습니다. 직접 입력하거나 재인식하세요.")]


def _map_telecom(row, r):
    raw = str(r.get("발행사") or "").strip().upper()
    key = raw if raw in TELECOM_ISSUER else ("SK" if "SK" in raw else ("KT" if "KT" in raw else ""))
    info = TELECOM_ISSUER.get(key)
    if info:
        row.data.update(name=info["name"], bizno=info["bizno"], holder=info["holder"])
    row.data["account"] = str(r.get("농협계좌번호") or "").strip()
    row.data["supply"] = _num(r.get("납부금액"))
    parts = [p for p in (r.get("대표상품명"), r.get("이용기간")) if p]
    row.data["memo"] = f"통신요금({' · '.join(parts)})" if parts else "통신요금"
    bits = [b for b in (f"명세서번호 {r['명세서번호']}" if r.get("명세서번호") else "", r.get("비고")) if b]
    row.ai_memo = " · ".join(bits)
    return [(bool(info), "발행사(SK/KT)를 판별하지 못했습니다. 거래처명·사업자번호·예금주명을 직접 확인해 입력하세요."),
            (bool(row.data["account"]), "농협 계좌번호를 찾지 못했습니다. 표를 직접 확인해 입력하거나 재인식하세요."),
            (bool(row.data["supply"]), "납부금액을 찾지 못했습니다. 직접 입력하거나 재인식하세요.")]


@dataclass
class BillType:
    id: str
    label: str
    tag: str
    sub: str
    notice: str
    prompt: str
    defaults: dict
    mapper: object
    prefix: str
    focus: list = field(default_factory=list)
    verify: bool = False


_BASE = dict(type="10:법인사업자", paytype="10:계좌이체", bank="011:농협은행", deduct="0", repFlag="N")
TYPES = {
    "water": BillType("water", "상하수도요금", "익산시",
                      "익산시 상·하수도 요금 고지서의 <b>농협 가상계좌번호</b>와 <b>총납부금액(합계)</b>을 읽습니다.",
                      "고지서마다 <b>전북은행</b>과 <b>농협</b> 가상계좌가 나란히 표시되는데, 이 도구는 그중 <b>농협 계좌번호만</b> 자동으로 골라 추출합니다.",
                      D.WATER_PROMPT,
                      dict(_BASE, name="상수도문화유산과", bizno="403-83-00013", holder="상수도문화유산과"), _map_water, "거래처입력_상하수도요금",
                      [dict(x=0, y=.63, w=.68, h=.05, label="지로번호·가상계좌 표 (농협 계좌번호가 오른쪽 칸에 있음)"),
                       dict(x=.62, y=.78, w=.38, h=.07, label="수납은행보관용 표의 총납부금액")]),
    "kepco": BillType("kepco", "전기요금", "한전",
                      "한전 전기요금 청구서의 <b>농협 가상계좌번호</b>와 <b>청구금액</b>을 읽습니다.",
                      "우리·신한·국민·농협·하나·기업·우체국·전북·광주은행 가상계좌 중 <b>농협 계좌번호만</b> 자동으로 골라 추출합니다.",
                      D.KEPCO_PROMPT, dict(_BASE, name="한국전력공사", bizno="4038202334"), _map_kepco, "거래처입력_전기요금", [], True),
    "telecom": BillType("telecom", "통신요금", "SK·KT",
                        "SK Telecom 요금안내서와 KT 명세서를 모두 올릴 수 있습니다. 발행사(SK/KT)는 장마다 자동으로 구분합니다.",
                        "케이뱅크·농협·신한·국민·카카오뱅크 등 여러 은행 계좌 중 <b>농협(농협은행) 계좌번호만</b> 추출합니다.",
                        D.TELECOM_PROMPT, dict(_BASE), _map_telecom, "거래처입력_통신요금",
                        [dict(x=0, y=.72, w=.30, h=.16, label="SK 요금안내서인 경우: 입금전용계좌 박스 (농협 줄 포함)"),
                         dict(x=0, y=.44, w=.60, h=.10, label="SK 요금안내서인 경우: 이번 달 납부하실 금액 박스"),
                         dict(x=0, y=.34, w=.30, h=.16, label="KT 명세서인 경우: 전용 입금계좌 박스 (농협은행 줄 포함)"),
                         dict(x=.25, y=.30, w=.75, h=.16, label="KT 명세서인 경우: 이번 달 납부하실 금액 박스")]),
}
ORDER = ["water", "kepco", "telecom"]


@dataclass
class Row:
    id: int
    name: str
    img: Image.Image                 # 최대 3200px 원본(화면 표시·고해상도 크롭용)
    data: dict
    status: str = "대기"
    error: str = ""
    ai_memo: str = ""


def default_data(t: BillType) -> dict:
    d = {k: "" for k in FIELD_KEYS}
    d.update(t.defaults)
    d["holder"] = clamp_holder(d.get("holder"))
    return d


# ───────────── 파일 → 행 ─────────────
def _flat(im: Image.Image) -> Image.Image:
    im = ImageOps.exif_transpose(im)
    if im.mode in ("RGBA", "LA", "P"):
        im = im.convert("RGBA")
        bg = Image.new("RGB", im.size, "white")
        bg.paste(im, mask=im.split()[-1])
        return bg
    return im.convert("RGB")


def _limit(im: Image.Image, max_dim: int) -> Image.Image:
    if max(im.size) <= max_dim:
        return im
    s = max_dim / max(im.size)
    return im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)


def load_images(path: str, progress=lambda *a: None) -> list:
    """이미지 1장 또는 PDF 의 모든 쪽 → [(표시이름, PIL.Image)]"""
    import os
    base = os.path.basename(path)
    if path.lower().endswith(".pdf"):
        import pypdfium2 as pdfium
        pdf = pdfium.PdfDocument(path)
        n = len(pdf)
        out = []
        for i in range(n):
            progress(int(i / max(n, 1) * 100), f"{base} — {i + 1}/{n}쪽 변환 중…")
            im = pdf[i].render(scale=2.5).to_pil()
            out.append((f"{base} ({i + 1}/{n}쪽)", _limit(_flat(im), 3200)))
        return out
    return [(base, _limit(_flat(Image.open(path)), 3200))]


def jpeg_b64(im: Image.Image, max_dim: int, quality: float) -> str:
    im = _limit(im, max_dim)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=int(quality * 100))
    return base64.b64encode(buf.getvalue()).decode()


def crop_regions(im: Image.Image, regions: list, max_dim=900, quality=.94) -> list:
    out = []
    W, H = im.size
    for r in regions:
        sx, sy = round(r["x"] * W), round(r["y"] * H)
        sw, sh = max(1, round(r["w"] * W)), max(1, round(r["h"] * H))
        c = im.crop((sx, sy, sx + sw, sy + sh))
        s = min(3, max_dim / max(sw, sh))
        c = c.resize((max(1, round(sw * s)), max(1, round(sh * s))), Image.LANCZOS)
        out.append(dict(label=r.get("label") or "확대 영역", b64=jpeg_b64(c, 99999, quality)))
    return out


# ───────────── AI 호출 ─────────────
class RateLimit(Exception):
    def __init__(self, msg, wait=None):
        super().__init__(msg)
        self.wait = wait


def extract_json(text: str) -> dict:
    cleaned = re.sub(r"```json|```", "", text).strip()
    m = re.search(r"\{[\s\S]*\}", cleaned)
    if not m:
        raise RuntimeError("응답에서 JSON을 찾을 수 없음: " + cleaned[:200])
    return json.loads(m.group(0))


def _post(url, headers, body, label):
    req = urllib.request.Request(url, json.dumps(body).encode(), headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        try:
            j = json.loads(raw)
            msg = (j.get("error") or {}).get("message") if isinstance(j.get("error"), dict) else (j.get("error") or raw[:300])
        except Exception:
            msg = raw[:300]
        if e.code == 429:
            wait = None
            ra = e.headers.get("retry-after")
            if ra and ra.replace(".", "", 1).isdigit():
                wait = float(ra)
            m = re.search(r'retryDelay"?\s*:\s*"?(\d+(?:\.\d+)?)s', raw, re.I)
            if wait is None and m:
                wait = float(m.group(1))
            raise RateLimit(f"{label} API 요청 한도 초과 (429) {msg}", wait)
        raise RuntimeError(f"{label} API 오류 ({e.code}) {msg}")
    except urllib.error.URLError as e:
        raise RuntimeError(f"{label} API 요청 실패(네트워크): {e.reason}")


def call_claude(b64, key, prompt, crops):
    content = [dict(type="image", source=dict(type="base64", media_type="image/jpeg", data=b64))]
    for c in crops:
        content.append(dict(type="text", text=f"[확대 영역: {c['label']}]"))
        content.append(dict(type="image", source=dict(type="base64", media_type="image/jpeg", data=c["b64"])))
    content.append(dict(type="text", text="이 고지서를 인식해서 JSON으로 출력해줘. 위에 제공된 확대 영역 이미지들도 함께 참고해서 계좌번호와 금액 숫자를 정확히 확인해라."
                        if crops else "이 고지서를 인식해서 JSON으로 출력해줘."))
    d = _post("https://api.anthropic.com/v1/messages",
              {"Content-Type": "application/json", "x-api-key": key, "anthropic-version": "2023-06-01"},
              dict(model=CLAUDE_MODEL, max_tokens=1000, system=prompt, messages=[dict(role="user", content=content)]), "Claude")
    tb = next((b for b in d.get("content", []) if b.get("type") == "text"), None)
    if not tb:
        raise RuntimeError("Claude 응답에 텍스트가 없습니다.")
    return extract_json(tb["text"])


def call_gemini(b64, key, prompt, crops, model):
    intro = prompt + ("\n\n아래에 전체 이미지에 이어서 계좌·금액 부분을 확대한 참고 이미지들도 함께 제공된다. 함께 참고해서 계좌번호와 금액 숫자를 정확히 확인한 뒤, 이 고지서를 인식해서 JSON으로 출력해줘."
                      if crops else "\n\n이 고지서를 인식해서 JSON으로 출력해줘.")
    parts = [dict(text=intro), dict(inline_data=dict(mime_type="image/jpeg", data=b64))]
    for c in crops:
        parts.append(dict(text=f"[확대 영역: {c['label']}]"))
        parts.append(dict(inline_data=dict(mime_type="image/jpeg", data=c["b64"])))
    url = f"https://generativelanguage.googleapis.com/v1beta/models/{urllib.request.quote(model)}:generateContent?key={urllib.request.quote(key)}"
    d = _post(url, {"Content-Type": "application/json"},
              dict(contents=[dict(parts=parts)], generationConfig=dict(responseMimeType="application/json", temperature=0.1)), "Gemini")
    cand = (d.get("candidates") or [{}])[0]
    if cand.get("finishReason") and cand["finishReason"] != "STOP":
        raise RuntimeError(f"Gemini 응답이 완료되지 않음 ({cand['finishReason']})")
    text = "".join(p.get("text", "") for p in (cand.get("content") or {}).get("parts", []))
    if not text:
        raise RuntimeError("Gemini 응답에 텍스트가 없습니다. (안전 필터에 걸렸을 수 있음)")
    return extract_json(text)


@dataclass
class AiConfig:
    provider: str = "gemini"
    key: str = ""
    model: str = "gemini-3.6-flash"
    pacing: float = 7.0
    saver: bool = False
    crops: bool = True
    verify: bool = False

    def budget(self):
        if self.provider == "gemini" and self.saver:
            return dict(main=1024, mq=.85, crop=720, cq=.90)
        return dict(main=1600, mq=.90, crop=900, cq=.94)


def recognize(cfg: AiConfig, b64, prompt, crops, on_wait=lambda s, i, n: None, retries=4):
    if not cfg.key:
        raise RuntimeError(f"API 키를 먼저 입력하세요 ({'Google AI Studio' if cfg.provider == 'gemini' else 'Anthropic Console'}) — 설정 화면에서 저장할 수 있습니다.")
    attempt = 0
    while True:
        try:
            if cfg.provider == "gemini":
                return call_gemini(b64, cfg.key, prompt, crops, cfg.model)
            return call_claude(b64, cfg.key, prompt, crops)
        except RateLimit as e:
            if attempt >= retries:
                raise RuntimeError(str(e))
            attempt += 1
            wait = min(60, e.wait or 4 * 2 ** attempt)
            on_wait(wait, attempt, retries)
            time.sleep(wait)


def apply_nonghyup_only(result: dict):
    lst = result.get("은행별계좌")
    if not isinstance(lst, list) or not lst:
        return

    def is_nh(name):
        n = re.sub(r"\s", "", str(name or ""))
        return bool(re.search("농협|NH", n, re.I)) and not re.search("증권|투자|캐피탈|생명|손해|카드", n)
    nh = [it for it in lst if isinstance(it, dict) and is_nh(it.get("은행")) and _digits(it.get("계좌번호"))]
    others = {_digits(it.get("계좌번호")) for it in lst if isinstance(it, dict) and not is_nh(it.get("은행")) and _digits(it.get("계좌번호"))}
    pick = next((it for it in nh if _digits(it["계좌번호"]) not in others), None)
    result["농협계좌번호"] = str(pick["계좌번호"]).strip() if pick else ""
    ibk = next((it for it in lst if isinstance(it, dict) and re.search("기업|IBK", str(it.get("은행") or ""), re.I)), None)
    if ibk:
        result["기업은행계좌번호"] = ibk.get("계좌번호")
    if not pick:
        result["농협_은행표기"] = ""


def verify_account(cfg: AiConfig, row: Row, result: dict, on_wait):
    b = result.get("농협계좌_위치") or {}
    first = str(result.get("농협계좌번호") or "").strip()
    try:
        bw, bh = float(b.get("w", 0)), float(b.get("h", 0))
    except Exception:
        return None
    if not first or bw <= 0 or bh <= 0:
        return None
    bx, by = float(b.get("x", 0)), float(b.get("y", 0))
    cl = lambda v: min(1.0, max(0.0, v))
    x0, x1 = cl(bx - .22), cl(bx + bw + .08)
    y0, y1 = cl(by - bh * .7), cl(by + bh * 1.7)
    W, H = row.img.size
    c = row.img.crop((round(x0 * W), round(y0 * H), round(x0 * W) + max(1, round((x1 - x0) * W)), round(y0 * H) + max(1, round((y1 - y0) * H))))
    s = min(3, 1400 / max(c.size))
    c = c.resize((max(1, round(c.width * s)), max(1, round(c.height * s))), Image.LANCZOS)
    r2 = recognize(cfg, jpeg_b64(c, 99999, .95), D.ACCOUNT_VERIFY_PROMPT, [], on_wait)
    second = str(r2.get("계좌번호") or "").strip()
    label = str(r2.get("은행표기") or "")
    d1, d2 = _digits(first), _digits(second)
    ibk = _digits(r2.get("기업은행계좌번호")) or _digits(result.get("기업은행계좌번호"))
    second_bad = (not second) or (ibk and d2 == ibk) or (label and not re.search("농|NH", label, re.I))
    first_bad = bool(ibk) and d1 == ibk
    if second_bad:
        if first_bad:
            return dict(account="", mismatch=True, note="농협 줄로 읽은 번호가 기업은행 번호와 같아 비웠습니다. 고지서에서 농협 줄을 직접 확인해 입력하세요.")
        return None
    if d1 == d2:
        return dict(account=first, note="", mismatch=False)
    return dict(account=second, mismatch=True, note=f"계좌번호 재확인 불일치: 1차 {first} → 확대 재판독 {second} (5/6/8 등 숫자 직접 확인 권장)")


def process_row(cfg: AiConfig, t: BillType, row: Row, on_wait=lambda s, i, n: None):
    """행 하나 인식. row.status / error / data / ai_memo 를 채운다."""
    row.status, row.error = "인식중", ""
    try:
        bud = cfg.budget()
        crops = crop_regions(row.img, t.focus, bud["crop"], bud["cq"]) if (cfg.crops and t.focus) else []
        b64 = jpeg_b64(row.img, bud["main"], bud["mq"])
        result = recognize(cfg, b64, t.prompt, crops, on_wait)
        apply_nonghyup_only(result)
        vnote, vmis = "", False
        if t.verify and cfg.verify:
            try:
                v = verify_account(cfg, row, result, on_wait)
                if v:
                    result["농협계좌번호"], vnote, vmis = v["account"], v["note"], v["mismatch"]
            except Exception:
                pass
        row.data.update(t.defaults)
        checks = t.mapper(row, result)
        row.data["holder"] = clamp_holder(row.data.get("holder"))
        if vnote:
            row.ai_memo = " · ".join(x for x in (vnote, row.ai_memo) if x)
        failed = next((m for ok, m in checks if not ok), None) or ("계좌번호를 두 번 읽은 결과가 달라요. 5/6/8 등 숫자를 원본과 대조해 확인하세요." if vmis else None)
        row.status, row.error = ("확인필요", failed) if failed else ("완료", "")
    except Exception as e:  # noqa
        row.status, row.error = "오류", str(e)


# ───────────── 엑셀 ─────────────
def build_xlsx(rows: list) -> bytes:
    from openpyxl import Workbook
    from openpyxl.styles import Alignment
    wb = Workbook()
    ws = wb.active
    ws.title = "거래처입력"
    ws.append(D.HEADERS)
    for r in rows:
        line = []
        for k in FIELD_KEYS:
            v = r.data.get(k) or ""
            if k in NUMERIC_KEYS and v != "":
                try:
                    f = float(str(v).replace(",", "").strip())
                    v = int(f) if f == int(f) else f
                except ValueError:
                    pass
            line.append(v)
        ws.append(line)
    for i, k in enumerate(FIELD_KEYS):
        ws.column_dimensions[X.col_letter(i + 1)].width = max(12, round(FIELD_WIDTH[k] / 7))
        if k in NUMERIC_KEYS:
            for n in range(2, len(rows) + 2):
                c = ws.cell(row=n, column=i + 1)
                if isinstance(c.value, (int, float)):
                    c.number_format = "#,##0"
    for c in ws[1]:
        c.alignment = Alignment(wrap_text=True, vertical="center", horizontal="center")
    ws.row_dimensions[1].height = 48
    w2 = wb.create_sheet("거래처입력설명")
    w2.append([None, "거래처입력 엑셀 설명", None, None, None, None, None, "ver2.0"])
    w2.append([None, D.DESC_TEXT])
    w2.append([None] + D.HEADERS)
    w3 = wb.create_sheet("은행코드_참고자료")
    w3.append(["은행코드"])
    for b in D.BANK_CODES:
        w3.append([b])
    out = io.BytesIO()
    wb.save(out)
    return out.getvalue()
