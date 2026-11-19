# -*- coding: utf-8 -*-
"""양식을 이어 넣었을 때 '빈 쪽'이 생길 만한 곳을 글자·표 높이로 어림해서 찾는다(한글 없이 하는 점검).
   사용: python tools/check_pages.py        (어림 계산이라 ±몇 줄 오차가 있으니, 의심 구간을 한글에서 확인)"""
import os
import sys

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
from hwpgen import package as P, registry  # noqa: E402
from hwpgen.package import HP, Package  # noqa: E402

from hwpgen.pagecheck import DEFAULT_BODY, PAGE_H, Styles, body_area, para_height  # noqa: E402,F401


def is_blank(p):
    return P._is_blank_para(p)


def pages_of(paras, st, body_h, width, first_pb_ok=True):
    """문단 목록 → 쪽 목록(각 쪽은 문단 목록). 쪽 나눔과 넘침으로 나눈다"""
    pages, cur, used = [], [], 0
    for i, p in enumerate(paras):
        h = para_height(p, st, width)
        pb = p.get("pageBreak") == "1" and (cur or not first_pb_ok)
        if pb or (cur and used + h > body_h):
            pages.append(cur)
            cur, used = [], 0
        cur.append(p)
        used += h
    if cur:
        pages.append(cur)
    return pages


def blank_pages(pages):
    return [i + 1 for i, pg in enumerate(pages) if all(is_blank(p) for p in pg)]


def prepared(fid, first):
    """insert() 가 하는 손질을 같게 한 문단 목록(첫 양식이 아니면 쪽 설정 제거·앞 받침 문단 포함)"""
    f = registry.load_form(fid)
    if fid.startswith("weekly"):
        pkg = Package.load(os.path.join(HERE, "forms", fid, "template.hwpx"))
    else:
        vals = {x["key"]: "값" for x in f.fields}
        pkg = f.render(vals, f.meta.get("starter", ""))
    st = Styles(pkg)
    area = body_area(pkg) if first else DEFAULT_BODY
    if not first:
        P.strip_section_props(pkg)
    from hwpgen.pagecheck import fit_page_tables
    fit_page_tables(pkg)
    P.big_tables_own_page(pkg)
    P.trim_trailing_blanks(pkg)
    P.trim_leading_pagebreak_blanks(pkg)
    if first:                                          # 빈 문서의 첫 양식은 맨 앞 쪽 나눔을 뺀다
        for p0 in pkg.section(0).findall(HP + "p")[:2]:
            p0.set("pageBreak", "0")
    elif not P.starts_new_page(pkg):
        P.with_lead_blank(pkg)
    from hwpgen.pagecheck import estimate_height
    if estimate_height(pkg) < 0.88 * DEFAULT_BODY[0]:
        P.with_tail_blank(pkg)
    return pkg.section(0).findall(HP + "p"), st, area


SEQUENCES = {
    "감사": ["audit_cover", "audit_toc", "audit_common", "audit_divider", "audit_i01", "audit_i02", "audit_req"],
    "정책질의": ["policy_cover", "policy_list", "policy_item", "policy_ref", "policy_item"],
    "서면질의": ["written_cover", "written_list", "written_item"],
    "간부회의": ["meeting_cover", "meeting_issue", "meeting_card", "issue_list", "issue_card", "issue_ref", "ref_std"],
    "주간": ["weekly_cover", "weekly_plan"],
    "예산": ["budget_cover", "budget_project", "budget_ref_cover", "budget_ref_detail", "budget_ref_return"],
    "이월": ["carry_item", "carry_acc"],
    "국비": ["nbud_main", "nbud_ref1", "nbud_ref2"],
}


FACTOR = 1.0      # 0.92 처럼 줄이면 본문 영역이 작은(여백 큰) 문서에서의 위험을 본다


def main():
    bad = 0
    for name, ids in SEQUENCES.items():
        allp, st, area = [], None, None
        marks = []
        for i, fid in enumerate(ids):
            try:
                ps, st_i, area_i = prepared(fid, i == 0)
            except Exception as e:                       # noqa: BLE001
                print("건너뜀", fid, e)
                continue
            if st is None:
                st, area = st_i, area_i
            marks.append((fid, len(allp)))
            allp.extend(ps)
        pages = pages_of(allp, st, int(area[0] * FACTOR), area[1])
        bp = blank_pages(pages)
        # 어느 양식에서 났는지
        where = []
        for pg in bp:
            first_para = pages[pg - 1][0]
            idx = allp.index(first_para)
            fid = [m for m in marks if m[1] <= idx][-1][0]
            where.append("%d쪽(%s 근처)" % (pg, fid))
        print("%-6s %2d쪽  빈 쪽: %s" % (name, len(pages), ", ".join(where) if where else "없음"))
        bad += len(bp)
    print("빈 쪽으로 보이는 곳 총 %d" % bad)
    return bad


if __name__ == "__main__":
    if len(sys.argv) > 1:
        FACTOR = float(sys.argv[1])
    main()
