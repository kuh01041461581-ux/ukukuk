# -*- coding: utf-8 -*-
"""명령줄 사용 (화면 없이 파일 만들기)

  python app.py list
  python app.py render --form report_dept --data 입력.json --out 결과.hwpx
     입력.json:  {"values": {"title": "...", ...}, "outline": "□ 개요\n◦ 일시: ..."}
  python app.py render --form weekly_plan --data 계획.json --out 계획.hwpx
     계획.json:  {"org": "문화교육국", "items": [{"title","dept","staff","phone","date","time","place","extra"}]}
  python app.py render --form weekly_schedule --data 일정.json --out 일정.hwpx
     일정.json:  {"org": "문화교육국", "start": "2026-11-09", "events": [{"date","time","title","place","dept"}]}
  python app.py extract --plan 주간업무계획.hwpx --org 문화교육국 --out 계획.json
     (시 전체 주간업무계획 hwpx 에서 우리 국 항목만 뽑아 JSON 으로)
  python app.py convert --src 옛양식.hwp --out 새양식.hwpx    (한글이 켜져 있어야 함: .hwp → .hwpx)
  python app.py add-form --sample 새양식.hwpx --id my_form --name "이름" --audience "구분" --title "샘플 제목 글자"
"""
import argparse
import json
import sys

from . import registry, weekly
from .package import Package


def main(argv=None):
    ap = argparse.ArgumentParser(prog="app.py", description="한글 보고서 양식 생성기")
    sub = ap.add_subparsers(dest="cmd")
    sub.add_parser("list")
    r = sub.add_parser("render")
    r.add_argument("--form", required=True)
    r.add_argument("--data", required=True)
    r.add_argument("--out", required=True)
    e = sub.add_parser("extract")
    e.add_argument("--plan", required=True)
    e.add_argument("--org", required=True)
    e.add_argument("--out", required=True)
    cv = sub.add_parser("convert")
    cv.add_argument("--src", required=True)
    cv.add_argument("--out", required=True)
    a = sub.add_parser("add-form")
    a.add_argument("--sample", required=True)
    a.add_argument("--id", required=True)
    a.add_argument("--name", required=True)
    a.add_argument("--audience", default="내가 추가한 양식")
    a.add_argument("--title", default="", help="샘플 안의 제목 글자(입력 칸 '제목' 으로 바뀜)")
    args = ap.parse_args(argv)
    if args.cmd == "list":
        for fid, folder, meta in registry.list_forms():
            print("%-18s %-14s %s" % (fid, meta["kind"], meta["name"]))
    elif args.cmd == "render":
        form = registry.load_form(args.form)
        with open(args.data, encoding="utf-8") as f:
            data = json.load(f)
        if form.meta["kind"] == "report":
            pkg = form.render(data.get("values", {}), data.get("outline", ""))
        else:
            pkg = form.render(data)
        pkg.save(args.out)
        print("만들었어요:", args.out)
    elif args.cmd == "extract":
        pkg = Package.load(args.plan)
        orgs = [args.org] + [o for o in weekly.DEFAULT_ORGS if o != args.org]
        secs = weekly.split_org_sections(weekly.plan_text_lines(pkg), orgs)
        key = args.org.replace(" ", "")
        if key not in secs:
            print("'%s' 구역을 찾지 못했어요" % args.org)
            return 1
        items = weekly.parse_plan_lines(secs[key])
        with open(args.out, "w", encoding="utf-8") as f:
            json.dump({"org": args.org, "items": items}, f, ensure_ascii=False, indent=2)
        print("항목 %d개 → %s" % (len(items), args.out))
    elif args.cmd == "convert":
        from .bridge import HwpBridge
        br = HwpBridge()
        br.connect()
        print("변환했어요:", br.convert_to_hwpx(args.src, args.out))
    elif args.cmd == "add-form":
        fields = []
        if args.title:
            fields.append({"key": "title", "label": "제목", "sample": args.title})
        res = registry.add_report_form(args.sample, args.id, args.name, args.audience, fields)
        print("등록했어요:", res["id"], "· 견본 문단:", res["protos"], "· 표:", res["tables"])
        if res["leftover"]:
            print("고정 글자로 남은 것(입력 칸으로 바꾸려면 GUI 의 '양식 추가'를 쓰세요):", res["leftover"][:20])
    else:
        ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
