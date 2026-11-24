#!/usr/bin/env python3
"""서무 종합프로그램 빌드: shell.html 의 {{B64:이름}} 자리에 tools/이름.html 을 base64 로 넣어 한 파일로 만든다.
사용:  python3 seomu/build.py        -> seomu/dist/서무종합프로그램.html
"""
import base64, pathlib, re, sys
root = pathlib.Path(__file__).resolve().parent
shell = (root / "shell.html").read_text(encoding="utf-8")

def include(text):
    """도구 안의 /*@include 파일.js*/ 를 shared/파일.js 내용으로 바꾼다."""
    return re.sub(r"/\*@include ([\w.]+)\*/", lambda m: (root / "shared" / m.group(1)).read_text(encoding="utf-8"), text)

def b64inc(text):
    """/*@b64 파일*/ 를 shared/파일 의 base64 로 바꾼다(양식 엑셀을 도구 안에 넣을 때)."""
    return re.sub(r"/\*@b64 ([\w.]+)\*/", lambda m: base64.b64encode((root / "shared" / m.group(1)).read_bytes()).decode("ascii"), text)

def put(m):
    name = m.group(1)
    text = b64inc(include((root / "tools" / f"{name}.html").read_text(encoding="utf-8")))
    return base64.b64encode(text.encode("utf-8")).decode("ascii")

out = re.sub(r"\{\{B64:(\w+)\}\}", put, shell)
dest = root / "dist" / "서무종합프로그램.html"
dest.parent.mkdir(exist_ok=True)
dest.write_text(out, encoding="utf-8")
print(f"built {dest} ({len(out):,} chars)")
