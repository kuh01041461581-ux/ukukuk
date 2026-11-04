# -*- coding: utf-8 -*-
"""내 정보(국·과·계·담당자)와 주 단위 데이터를 이 PC 에만 저장한다 (저장소·양식 파일에는 들어가지 않는다)."""
import json
import os

from . import registry

PROFILE_FIELDS = [
    ("org", "국 이름", "문화교육국"),
    ("dept", "과 이름", ""),
    ("team", "계(팀) 이름", ""),
    ("head_name", "과장 이름", ""), ("head_ext", "과장 내선", ""), ("head_cell", "과장 휴대폰", ""),
    ("lead_name", "계장 이름", ""), ("lead_ext", "계장 내선", ""), ("lead_cell", "계장 휴대폰", ""),
    ("staff_name", "담당자 이름", ""), ("staff_ext", "담당자 내선", ""), ("staff_cell", "담당자 휴대폰", ""),
]


def _path(name):
    return os.path.join(registry.user_dir(), name)


def load_profile():
    try:
        with open(_path("profile.json"), encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError):
        data = {}
    return {k: data.get(k, d) for k, _, d in PROFILE_FIELDS}


def save_profile(data):
    with open(_path("profile.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


def _who(p, who, full):
    name, ext, cell = p.get(who + "_name", ""), p.get(who + "_ext", ""), p.get(who + "_cell", "")
    if not name:
        return ""
    if full:
        return " ".join(x for x in (name, ext) if x) + (" / " + cell if cell else "")
    return name + ("(%s)" % ext if ext else "")


def profile_value(p, key):
    """양식 입력 칸의 profile 키 → 값"""
    if key in p:
        return p[key]
    if key == "head":
        return _who(p, "head", False)
    if key == "lead":
        return _who(p, "lead", False)
    if key == "staff":
        return _who(p, "staff", False)
    if key == "head_full":
        return _who(p, "head", True)
    if key == "lead_full":
        return _who(p, "lead", True)
    if key == "staff_full":
        return _who(p, "staff", True)
    if key == "dept_team":
        return " ".join(x for x in (p.get("dept", ""), p.get("team", "")) if x)
    return ""


# ---- 주 단위 자료 (주간업무계획 항목)
def week_file(monday):
    d = os.path.join(registry.user_dir(), "weeks")
    os.makedirs(d, exist_ok=True)
    return os.path.join(d, "%s.json" % monday.isoformat())


def load_week(monday):
    try:
        with open(week_file(monday), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {"items": []}


def save_week(monday, data):
    with open(week_file(monday), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
