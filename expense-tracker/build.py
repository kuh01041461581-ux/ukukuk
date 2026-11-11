#!/usr/bin/env python3
"""화면(ui.html) 만들기. exe 빌드 전에 실행합니다.

  python build.py                 -> app/ui.html (개인 데이터 없음, exe에 들어감)
  python build.py --util A.xlsx --daily B.xlsx --out x.html
                                   -> 엑셀 값이 미리 들어간 단독 HTML(개인용, 공개 저장소에 올리지 마세요)
"""
import argparse, json
from pathlib import Path
HERE = Path(__file__).resolve().parent


def assemble(seed, exe):
    tpl = (HERE / "app.template.html").read_text(encoding="utf-8")
    css = (HERE / "src" / "style.css").read_text(encoding="utf-8")
    js = "\n".join(f.read_text(encoding="utf-8") for f in sorted((HERE / "src" / "js").glob("*.js")))
    pdfjs = (HERE / "vendor" / "pdf.min.js").read_text(encoding="utf-8")
    worker = (HERE / "vendor" / "pdf.worker.min.js").read_text(encoding="utf-8")
    esc = lambda t: t.replace("</script", "<\\/script")
    return (tpl.replace("/*__CSS__*/", css)
               .replace("/*__EXE__*/false", "true" if exe else "false")
               .replace("/*__SEED__*/null", json.dumps(seed, ensure_ascii=False).replace("</", "<\\/") if seed else "null")
               .replace("/*__PDFJS__*/", esc(pdfjs))
               .replace("/*__PDFWORKER__*/", esc(worker))
               .replace("/*__APPJS__*/", esc(js)))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--util"); ap.add_argument("--daily"); ap.add_argument("--out")
    a = ap.parse_args()
    if a.util and a.daily:
        import seedlib
        lines, memo, asof = seedlib.parse_daily(a.daily)
        seed = {"version": 1, "year": 2026, "asOf": asof, "memo": memo, "lines": lines,
                "util": seedlib.parse_util(a.util), "utilBudget": {"elec": None, "water": None, "tel": None}}
        out = Path(a.out or HERE / "dist" / "standalone.html"); exe = False
    else:
        seed, exe = None, True
        out = Path(a.out or HERE / "app" / "ui.html")
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(assemble(seed, exe), encoding="utf-8")
    print("완성:", out, f"({out.stat().st_size/1e6:.1f} MB)")


if __name__ == "__main__":
    main()
