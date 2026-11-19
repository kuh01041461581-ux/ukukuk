# -*- coding: utf-8 -*-
"""'한글 명령으로 직접 만들기'가 원본 양식과 같은 결과를 내는지 양식마다 검증한다.

양식 하나마다 ① 원본 양식에서 레시피를 뽑고 ② 한글의 새 문서에서 그 레시피를 명령으로 만든 뒤 hwpx 로 저장하고
③ 저장된 파일에서 다시 레시피를 뽑아 원본과 비교한다(글·글꼴·크기·굵게·색·정렬·간격, 표의 칸 수·너비·합침·색·테두리).
같으면 '통과'. 통과한 양식만 '자동' 방식에서 명령으로 만들고, 나머지는 기존처럼 양식 파일을 끼워 넣는다.
결과는 %APPDATA%\\HwpFormGenerator\\engine_status.json(통과 여부)과 engine_check.txt(어긋난 곳 목록)에 남는다."""
import datetime
import json
import os
import re

from . import recipe as R


def _data_dir():
    d = os.path.join(os.environ.get("APPDATA") or os.path.expanduser("~"), "HwpFormGenerator")
    os.makedirs(d, exist_ok=True)
    return d


def status_path():
    return os.path.join(_data_dir(), "engine_status.json")


def load_status():
    try:
        with open(status_path(), encoding="utf-8") as f:
            return json.load(f)
    except Exception:                                      # noqa: BLE001
        return {}


def save_status(st):
    with open(status_path(), "w", encoding="utf-8") as f:
        json.dump(st, f, ensure_ascii=False, indent=1)


def is_ok(form_id):
    """검증을 통과한 양식인가(아직 검증하지 않았으면 False → 기존 방식)"""
    return bool(load_status().get(form_id, {}).get("ok"))


# ------------------------------------------------------------------ 비교
def _t(s):
    return re.sub(r"[\s　]+", "", s or "")


def _near(a, b, tol):
    return abs((a or 0) - (b or 0)) <= tol


def _pstyle(p):
    runs = [r for r in p.get("runs", []) if _t(r.get("text"))]
    cs = (runs[0].get("cs") if runs else None) or {}
    return cs


def _cmp_para(a, b, where, out):
    ta = _t("".join(r.get("text", "") for r in a.get("runs", [])))
    tb = _t("".join(r.get("text", "") for r in b.get("runs", [])))
    if ta != tb:
        out.append("%s 글이 달라요: '%s' → '%s'" % (where, ta[:20], tb[:20]))
        return
    if not ta:
        return
    ca, cb = _pstyle(a), _pstyle(b)
    for k, lab in (("font", "글꼴"), ("bold", "굵게"), ("italic", "기울임"), ("color", "글자색")):
        va, vb = ca.get(k), cb.get(k)
        if k == "color":
            va, vb = (va or "#000000").lower(), (vb or "#000000").lower()
        if bool(va) != bool(vb) if k in ("bold", "italic") else va != vb:
            out.append("%s '%s' %s: %s → %s" % (where, ta[:12], lab, va, vb))
    if re.search(r"[\U000F0000-\U000FFFFF]", "".join(r.get("text", "") for r in a.get("runs", []))):          # 한컴 기호 글자는 기호용 글꼴도 같아야 한다
        sa, sb = (ca.get("fonts") or {}).get("symbol"), (cb.get("fonts") or {}).get("symbol")
        if sa != sb:
            out.append("%s '%s' 기호 글꼴: %s → %s" % (where, ta[:12], sa, sb))
    if not _near(ca.get("h"), cb.get("h"), 10):
        out.append("%s '%s' 글자 크기: %s → %s" % (where, ta[:12], ca.get("h"), cb.get("h")))
    pa, pb = a.get("para") or {}, b.get("para") or {}
    if (pa.get("align") or "JUSTIFY") != (pb.get("align") or "JUSTIFY"):
        out.append("%s '%s' 정렬: %s → %s" % (where, ta[:12], pa.get("align"), pb.get("align")))
    for k, tol, lab in (("ls", 6, "줄간격"), ("left", 300, "왼쪽 여백"), ("indent", 300, "들여쓰기"), ("prev", 300, "문단 위"), ("next", 300, "문단 아래")):
        if not _near(pa.get(k), pb.get(k), tol):
            out.append("%s '%s' %s: %s → %s" % (where, ta[:12], lab, pa.get(k), pb.get(k)))


def _none_sides(bd):
    return {k: ((bd or {}).get(k, ["SOLID"])[0] == "NONE") for k in "lrtb"}


def _cmp_tbl(a, b, where, out):
    if (a["rows"], a["cols"]) != (b["rows"], b["cols"]):
        out.append("%s 표 크기: %d×%d → %d×%d" % (where, a["rows"], a["cols"], b["rows"], b["cols"]))
        return
    for i, (x, y) in enumerate(zip(a["colw"], b["colw"])):
        if not _near(x, y, max(150, x * 0.04)):
            out.append("%s %d번째 칸 너비: %s → %s" % (where, i + 1, x, y))
    ca = {(c["r"], c["c"]): c for c in a["cells"]}
    cb = {(c["r"], c["c"]): c for c in b["cells"]}
    if set(ca) != set(cb):
        out.append("%s 칸 합치기가 달라요(원본 %d칸 → %d칸)" % (where, len(ca), len(cb)))
        return
    for key, x in ca.items():
        y = cb[key]
        w = "%s (%d,%d)칸" % (where, key[0] + 1, key[1] + 1)
        if (x["rs"], x["cs"]) != (y["rs"], y["cs"]):
            out.append("%s 합친 범위: %d×%d → %d×%d" % (w, x["rs"], x["cs"], y["rs"], y["cs"]))
        fa, fb = (x.get("fill") or "#ffffff").lower(), (y.get("fill") or "#ffffff").lower()
        if fa != fb:
            out.append("%s 배경색: %s → %s" % (w, fa, fb))
        if _none_sides(x.get("bd")) != _none_sides(y.get("bd")):
            out.append("%s 테두리 있음/없음이 달라요" % w)
        if (x.get("va") or "TOP") != (y.get("va") or "TOP"):
            out.append("%s 세로 맞춤: %s → %s" % (w, x.get("va"), y.get("va")))
        npa, npb = sum(1 for p in x["paras"] if p.get("k") == "pic"), sum(1 for p in y["paras"] if p.get("k") == "pic")
        if npa != npb:
            out.append("%s 그림 수: %d → %d" % (w, npa, npb))
        pa = [p for p in x["paras"] if _t("".join(r.get("text", "") for r in p.get("runs", [])))]
        pb = [p for p in y["paras"] if _t("".join(r.get("text", "") for r in p.get("runs", [])))]
        if len(pa) != len(pb):
            out.append("%s 문단 수: %d → %d" % (w, len(pa), len(pb)))
        for p, q in zip(pa, pb):
            _cmp_para(p, q, w, out)


def compare(src, got, limit=40):
    """원본 레시피 src 와 한글이 만든 got 을 비교 → 다른 곳 목록(비어 있으면 같음)"""
    out = ["명령으로 똑같이 만들 수 없는 부분이 있어 양식 파일로 만들어요: %s" % ", ".join(src["unsupported"])] if src.get("unsupported") else []

    def seq(rc):
        items = []
        for s in rc.get("steps", []):
            if s["k"] == "tbl":
                items.append(("tbl", s))
            elif s["k"] == "pic":
                items.append(("pic", s))
            elif s["k"] == "p" and _t("".join(r.get("text", "") for r in s.get("runs", []))):
                items.append(("p", s))
        return items
    a, b = seq(src), seq(got)
    if len(a) != len(b):
        out.append("글 줄·표 개수가 달라요: 원본 %d → 결과 %d" % (len(a), len(b)))
    for i, ((ka, x), (kb, y)) in enumerate(zip(a, b), 1):
        where = "%d번째" % i
        if ka != kb:
            out.append("%s 종류가 달라요: %s → %s" % (where, ka, kb))
            continue
        if ka == "pic":
            if not (_near(x.get("w"), y.get("w"), max(200, (x.get("w") or 0) * 0.05)) and _near(x.get("h"), y.get("h"), max(200, (x.get("h") or 0) * 0.05))):
                out.append("%s 그림 크기: %sx%s → %sx%s" % (where, x.get("w"), x.get("h"), y.get("w"), y.get("h")))
            continue
        (_cmp_tbl if ka == "tbl" else _cmp_para)(x, y, where, out)
        if len(out) >= limit:
            break
    blanks = lambda rc: sum(1 for s in rc.get("steps", []) if s["k"] == "p" and not _t("".join(r.get("text", "") for r in s.get("runs", []))))  # noqa: E731
    if abs(blanks(src) - blanks(got)) > 0:
        out.append("빈 줄 수: %d → %d" % (blanks(src), blanks(got)))
    return out[:limit]


# ------------------------------------------------------------------ 전체 검증
def _weekly_sample(fid):
    """주간 양식은 입력 사전으로 만든다 — 일정이 여러 줄로 늘어나는 대표 예로 검증"""
    items = [{"title": "행사 %d" % i, "dept": "문화유산과", "staff": "홍길동", "phone": "5708", "date": "2026-11-%02d" % (9 + i), "time": "10:00",
              "place": "시청", "lines": [("주최", "익산시"), ("내용", "내용 %d" % i)]} for i in range(3)]
    if fid == "weekly_cover":
        return {"start": "2026-11-09"}
    if fid == "weekly_plan":
        return {"org": "문화교육국", "items": items}
    evs = [{"date": "2026-11-10", "time": "%02d:00" % (9 + k), "title": "일정%d" % k, "place": "장소", "dept": "과", "attend": "", "note": ""} for k in range(4)]
    return {"org": "문화교육국", "start": "2026-11-09", "events": evs + [{"date": "2026-11-14", "time": "13:00", "title": "토요 일정", "place": "공원", "dept": "과"}]}


def check_form(br, form, tmp_path):
    """양식 하나 검증 → (통과 여부, 다른 곳 목록)"""
    from .package import Package
    fid = getattr(form, "id", "") or form.meta.get("id", "")
    if fid.startswith("weekly"):
        pkg = form.render(_weekly_sample(fid))
    else:
        vals = {x["key"]: ("{{%s}}" % x["key"]) for x in getattr(form, "fields", [])}
        pkg = form.render(vals, form.meta.get("starter", ""))
    src, imgs = R.extract(pkg)
    if src.get("unsupported"):                         # 도형·바탕쪽 등이 있으면 만들어 볼 필요 없이 양식 파일 방식
        return False, compare(src, src)
    br.scratch_build(src, imgs, src.get("page"), tmp_path)
    got, _ = R.extract(Package.load(tmp_path))
    diffs = compare(src, got)
    return (not diffs), diffs


def run_all(br, form_ids, tmp_dir, progress=None):
    """여러 양식 검증 → 결과를 engine_status.json / engine_check.txt 에 저장하고 {양식: (통과, 목록)} 를 돌려준다"""
    from . import registry
    results, status = {}, load_status()
    lines = ["엔진 검증 %s  (한글 %s)" % (datetime.datetime.now().strftime("%Y-%m-%d %H:%M"), getattr(br, "hwp_version", "") or "버전 모름"), ""]
    for i, fid in enumerate(form_ids, 1):
        if progress:
            progress(i, len(form_ids), fid)
        try:
            form = registry.load_form(fid)
            ok, diffs = check_form(br, form, os.path.join(tmp_dir, "enginecheck_%s.hwpx" % fid))
        except Exception as e:                             # noqa: BLE001
            ok, diffs = False, ["검증 중 오류: %s" % e]
        results[fid] = (ok, diffs)
        status[fid] = {"ok": ok, "diffs": len(diffs), "when": datetime.date.today().isoformat()}
        lines.append("[%s] %s" % ("통과" if ok else "다름 %d곳" % len(diffs), fid))
        lines += ["    - " + d for d in diffs[:15]]
    save_status(status)
    with open(os.path.join(_data_dir(), "engine_check.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return results
