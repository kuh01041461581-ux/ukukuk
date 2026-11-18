# -*- coding: utf-8 -*-
"""틀만 있는 양식(이월 조서·국가예산 신규사업 등)에 이미 쓴 글을 옮겨 넣는다.

이미 쓴 글(다른 양식으로 쓴 것도 됨)에서 '항목 이름 : 값' 줄과 '□ 제목' 아래의 내용 줄을 읽어,
양식의 같은 항목 칸('◦ 사업기간 :', 'ㅇ (사업내용)')과 같은 제목 아래 빈 줄에 채운다. 항목 이름이 조금 달라도
(사업장소↔사업위치, 사업비↔총사업비, 그간 추진사항↔추진상황 …) 같은 뜻이면 맞춘다."""
import copy
import re

from . import package as P
from .package import HP

_SYN = [
    ("사업명", "사업명", "행사명", "과제명", "제목"),
    ("사업량", "사업량", "사업규모", "규모"),
    ("사업기간", "사업기간", "기간", "추진기간"),
    ("사업내용", "사업내용", "주요내용", "내용"),
    ("사업위치", "사업위치", "위치", "사업장소", "장소"),
    ("총사업비", "총사업비", "사업비", "소요예산", "예산"),
    ("주관시행", "주관시행", "주관", "시행", "시행기관", "주관기관", "주관부서"),
    ("필요성", "필요성", "추진배경", "배경", "추진목적", "목적"),
    ("추진상황", "추진상황", "추진상황및계획", "그간추진사항", "그간추진경과", "추진경과", "추진현황", "그간추진현황"),
    ("향후계획", "향후계획", "금후계획", "추진계획"),
    ("이월사유", "이월사유"),
    ("이월액", "이월액"),
    ("예산편성시점", "예산편성시점"),
    ("일시", "일시", "일자", "날짜"),
]
_BUL = "◦○ㅇ▸‣-·•*※ㆍ□■"


def norm(label):
    s = re.sub(r"\(.*?\)", "", label or "")
    s = re.sub(r"[\s·ㆍ/,.:：\[\]]", "", s)
    for group in _SYN:
        if s in group[1:]:
            return group[0]
    return s


def parse_source(text):
    """→ (항목 사전 {표준이름: 값}, 제목별 내용 {표준이름: [줄…]})"""
    labels, sections, cur = {}, {}, None
    for raw in (text or "").replace("\r", "").split("\n"):
        s = raw.strip()
        if not s:
            continue
        if s[0] in "□■" or re.match(r"^\d+\.\s", s):
            cur = norm(re.sub(r"^(□|■|\d+\.)\s*", "", s))
            sections.setdefault(cur, [])
            m = re.match(r"^(?:□|■)\s*([^:：]{1,14}?)\s*[:：]\s*(.+)$", s)      # □ 이 월 액 : 1,000원 처럼 제목 줄에 값
            if m:
                labels.setdefault(norm(m.group(1)), m.group(2).strip())
            continue
        body = re.sub(r"^[%s\s]+" % re.escape(_BUL), "", s)
        m = re.match(r"^\(?([가-힣][가-힣\s/·]{0,12}?)\)?\s*[:：]\s*(.+)$", body)
        if m:
            labels.setdefault(norm(m.group(1)), m.group(2).strip())
        if cur is not None:
            sections[cur].append(body)
    return labels, sections


def _own(p):
    return "".join(t.text or "" for r in p.findall(HP + "run") for t in r.findall(HP + "t"))


def _append(p, text):
    runs = [r for r in p.findall(HP + "run") if r.findall(HP + "t")]
    cp = runs[-1].get("charPrIDRef") if runs else (p.find(HP + "run").get("charPrIDRef") if p.find(HP + "run") is not None else "0")
    P.set_par_runs(p, [(r.get("charPrIDRef"), "".join(t.text or "" for t in r.findall(HP + "t"))) for r in runs] + [(cp, text)])


_LABEL_LINE = re.compile(r"^(\s*[%s]?\s*)\(?([가-힣][가-힣\s/·]{0,12}?)\)?\s*[:：]?\s*$" % re.escape(_BUL))


def fill_frame(pkg, text):
    """틀 양식 pkg 에 text 의 값을 채운다 → 채운 칸 수"""
    labels, sections = parse_source(text)
    used_sec = set()
    n = 0
    sec = pkg.section(0)
    in_cell = set()
    for tc in sec.iter(HP + "tc"):
        for q in tc.iter(HP + "p"):
            in_cell.add(q)
    paras = [p for p in sec.iter(HP + "p") if not list(p.iter(HP + "tbl")) and p not in in_cell]
    # 표 안 '항목 칸 | 값 칸' 상자: 항목 칸 글이 아는 항목이고 바로 오른쪽 칸이 비었으면 거기에 값
    for tr in sec.iter(HP + "tr"):
        tcs = tr.findall(HP + "tc")
        for a, b in zip(tcs, tcs[1:]):
            key = norm(P.ptext(a))
            if key in labels and not P.ptext(b).strip():
                bp = b.find(".//" + HP + "p")
                if bp is not None:
                    _append(bp, labels[key])
                    n += 1
    for i, p in enumerate(paras):
        own = _own(p)
        s = own.strip()
        if not s:
            continue
        if s[0] in "□■":                                    # 제목: 같은 제목의 내용 줄을 아래 빈 글머리 줄에 채운다
            key = norm(re.sub(r"^(□|■)\s*", "", s).split(":")[0].split("：")[0])
            m = re.match(r"^(?:□|■)\s*([^:：]{1,14}?)\s*[:：]\s*$", s)
            if m and norm(m.group(1)) in labels:              # '□ 이 월 액 :' 처럼 제목 줄에 값 자리
                _append(p, ("" if own.endswith(" ") else " ") + labels[norm(m.group(1))])
                n += 1
                continue
            items = [x for x in sections.get(key, []) if not re.match(r"^\(?[가-힣][가-힣\s/·]{0,12}?\)?\s*[:：]", x)] if key not in used_sec else []
            nxt = paras[i + 1] if i + 1 < len(paras) else None
            if items and nxt is not None and re.fullmatch(r"\s*[%s]?\s*" % re.escape(_BUL), _own(nxt) or ""):
                used_sec.add(key)
                base = nxt
                _append(base, (" " if _own(base).strip() else "") + items[0])
                last = base
                for extra in items[1:]:
                    q = copy.deepcopy(base)
                    P.set_par_runs(q, [])
                    q2 = copy.deepcopy(nxt)
                    last.addnext(q)
                    runs = [r for r in base.findall(HP + "run") if r.findall(HP + "t")]
                    cp = runs[0].get("charPrIDRef") if runs else "0"
                    lead = re.match(r"^\s*[%s]?\s*" % re.escape(_BUL), _own(q2)).group(0)
                    P.set_par_runs(q, [(cp, lead + extra)])
                    last = q
                n += 1
            continue
        m = _LABEL_LINE.match(own)                            # '◦ 사업기간 :' / 'ㅇ (사업내용)' — 값이 빈 항목 칸
        if m:
            key = norm(m.group(2))
            if key in labels:
                _append(p, (" " if not own.endswith(" ") else "") + labels[key])
                n += 1
    return n
