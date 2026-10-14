#!/usr/bin/env python3
"""서무 업무 자동화 — 단일 HTML 파일로 묶기

    python3 suite/build.py          →  suite/서무업무자동화.html

· shell.html (껍데기) + tools/*.html (도구) + theme.css (공통 디자인) + bridge.js (셸 연결)
· 각 도구는 iframe(srcdoc) 안에서 돌아가므로 서로 id/전역 변수가 부딪히지 않는다.
· 결과 파일 하나만 있으면 인터넷 연결된 PC의 브라우저에서 바로 열린다(라이브러리·글꼴은 CDN).
"""
import json
import pathlib
import re
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE / "서무업무자동화.html"

# 셸 목록(shell.html 의 TOOLS)과 같은 순서/이름
TOOL_IDS = ["travel", "outstation", "meal", "utility", "blur"]


def read(p):
    return pathlib.Path(p).read_text(encoding="utf-8")


def version():
    try:
        sha = subprocess.check_output(["git", "-C", str(HERE), "rev-parse", "--short", "HEAD"],
                                      stderr=subprocess.DEVNULL, text=True).strip()
    except Exception:
        sha = "dev"
    return "build " + sha


def js_string(s):
    """<script type=application/json> 안에 안전하게 넣을 수 있는 JSON 문자열"""
    return json.dumps(s, ensure_ascii=False).replace("<", "\\u003c").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")


def main():
    theme = read(HERE / "theme.css")
    bridge = read(HERE / "bridge.js")
    shell = read(HERE / "shell.html")

    blocks = []
    for tid in TOOL_IDS:
        h = read(HERE / "tools" / f"{tid}.html")
        for marker in ("<!--SUITE:THEME-->", "<!--SUITE:BRIDGE-->"):
            if marker not in h:
                sys.exit(f"tools/{tid}.html 에 {marker} 가 없습니다")
        h = h.replace("<!--SUITE:THEME-->", "<style id=\"suite-theme\">\n" + theme + "\n</style>")
        h = h.replace("<!--SUITE:BRIDGE-->", "<script>\n" + bridge + "\n</script>")
        blocks.append(f'<script type="application/json" id="tool-data-{tid}">{js_string(h)}</script>')

    out = shell.replace("/*SUITE:THEME_CSS*/", theme)
    out = out.replace("<!--SUITE:TOOLS-->", "\n".join(blocks))
    out = out.replace("__VERSION__", version())
    OUT.write_text(out, encoding="utf-8")
    print(f"{OUT.name}: {len(out.encode('utf-8')) / 1024:.0f} KB")


if __name__ == "__main__":
    main()
