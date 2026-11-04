#!/usr/bin/env python3
"""서무 종합프로그램 빌드: src/ 의 셸 + 도구 8개 + 공통 CSS/JS 를 한 개의 HTML 파일로 묶는다.

    python3 seomu/build.py            → seomu/dist/서무종합프로그램.html

- 도구 화면은 base64 로 담겨 있다가 탭을 처음 열 때(또는 마우스를 올렸을 때) iframe 에 풀린다.
- 각 도구 HTML 안의 <!--SEOMU-COMMON--> 자리에 src/common/ui.css, ui.js 가 들어간다.
"""
import base64
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent
SRC = ROOT / "src"
OUT = ROOT / "dist" / "서무종합프로그램.html"
IDS = ["travel", "outtrip", "meal", "imgzip", "blur", "stamp", "cert", "bill"]


def build() -> pathlib.Path:
    css = (SRC / "common" / "ui.css").read_text(encoding="utf8")
    js = (SRC / "common" / "ui.js").read_text(encoding="utf8")
    common = f"<style id=\"seomu-common\">\n{css}\n</style>\n<script>\n{js}\n</script>"
    shell = (SRC / "shell.html").read_text(encoding="utf8")
    # 색·글꼴 토큰은 ui.css 한 곳에서만 정의하고 셸에도 같은 값을 넣는다
    m = re.search(r":root\{.*?\n\}\n:root\.seomu-dark\{.*?\n\}", css, re.S)
    if not m:
        sys.exit("ui.css 에서 토큰 블록을 찾지 못했습니다")
    shell = shell.replace("/*SEOMU-TOKENS*/", m.group(0).replace(":root.seomu-dark", "html.dark"))
    for tid in IDS:
        html = (SRC / "tools" / f"{tid}.html").read_text(encoding="utf8")
        if "<!--SEOMU-COMMON-->" not in html:
            sys.exit(f"{tid}.html 에 <!--SEOMU-COMMON--> 자리가 없습니다")
        html = html.replace("<!--SEOMU-COMMON-->", common)
        b64 = base64.b64encode(html.encode("utf8")).decode("ascii")
        shell, n = re.subn(
            rf'(<script type="text/plain" id="b64-{tid}">)(</script>)',
            lambda m: m.group(1) + b64 + m.group(2),
            shell,
        )
        if n != 1:
            sys.exit(f"shell.html 에 b64-{tid} 자리가 없습니다")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(shell, encoding="utf8")
    return OUT


if __name__ == "__main__":
    p = build()
    print(f"{p}  ({p.stat().st_size/1024:.0f} KB)")
