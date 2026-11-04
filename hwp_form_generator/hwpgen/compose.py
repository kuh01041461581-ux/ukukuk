# -*- coding: utf-8 -*-
"""한글의 '글자 겹치기'(네모·원 안에 숫자/글자)를 열린 문서 커서 위치에 넣는다.

글자 겹치기는 문서 안의 특수 개체(hp:compose)라서 일반 글자로는 못 넣는다. 두 가지 길을 쓴다.
 1) 겹침 글자만 든 아주 작은 hwpx 조각을 만들어 끼워 넣기 (hp:compose 요소)
 2) 안 되면 한글 명령(ComposeChars)으로 직접 넣기
"""
import os

from lxml import etree

from . import package as P
from .package import HP, Package, clone

# 이름, hwpx circleType, 한글 명령용 번호(테두리 종류)
SHAPES = [
    ("네모 안 숫자", "SHAPE_RECTANGLE", 3),
    ("꽉 찬 네모 흰숫자", "SHAPE_REVERSAL_RECTANGLE", 4),
    ("원 안 숫자", "SHAPE_CIRCLE", 1),
    ("꽉 찬 원 흰숫자", "SHAPE_REVERSAL_CIRCLE", 2),
]


def compose_package(text, circle_type, base_template, char_size=-3):
    """겹침 글자 하나만 든 hwpx. base_template: 서식 정의(header)를 빌려 올 기존 양식 template.hwpx"""
    pkg = Package.load(base_template)
    sec = pkg.section(0)
    ps = sec.findall(HP + "p")
    head = ps[0]
    for p in ps[1:]:
        sec.remove(p)
    p = clone(head)
    for run in p.findall(HP + "run"):                       # 구역 설정(secPr)은 첫 문단에만 둔다
        if any(P.local(c) in ("secPr", "ctrl") for c in run):
            p.remove(run)
    P.strip_lineseg(p)
    charpr = head.find(HP + "run").get("charPrIDRef") if head.find(HP + "run") is not None else "0"
    run = etree.SubElement(p, HP + "run")
    run.set("charPrIDRef", str(charpr))
    comp = etree.SubElement(run, HP + "compose")
    comp.set("circleType", circle_type)
    comp.set("charSz", str(char_size))
    comp.set("composeType", "SPREAD")
    comp.set("composeText", text)
    comp.set("charPrCnt", str(len(text)))
    for _ in text:
        cp = etree.SubElement(comp, HP + "charPr")
        cp.set("prIDRef", str(charpr))
    etree.SubElement(run, HP + "t")
    sec.append(head)
    sec.append(p)
    return pkg


def base_template():
    from . import registry
    return os.path.join(registry.app_dir(), "forms", "report_event", "template.hwpx")
