# -*- coding: utf-8 -*-
"""설치된 양식 목록과 생성 진입점.

양식 폴더는 두 곳에서 찾는다: 프로그램에 들어 있는 forms/ (기본 양식) 와
사용자 폴더 %APPDATA%/HwpFormGenerator/forms/ (사용자가 추가한 양식, 같은 id 면 사용자 것이 우선).
양식 폴더 하나 = form.json(이름·입력 칸·서식 규칙) + template.hwpx(틀) + protos.xml(문단·표 견본).
"""
import json
import os
import shutil
import sys

from . import report, weekly

KINDS = {
    "report": report.ReportForm,
    "weekly_plan": weekly.WeeklyPlanForm,
    "weekly_schedule": weekly.WeeklyScheduleForm,
}


def app_dir():
    if getattr(sys, "frozen", False):
        return getattr(sys, "_MEIPASS", os.path.dirname(sys.executable))
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def user_dir():
    base = os.environ.get("APPDATA") or os.path.join(os.path.expanduser("~"), ".config")
    d = os.path.join(base, "HwpFormGenerator")
    os.makedirs(d, exist_ok=True)
    return d


def form_roots(extra=None):
    roots = [os.path.join(app_dir(), "forms"), os.path.join(user_dir(), "forms")]
    if extra:
        roots.append(extra)
    return roots


def list_forms(roots=None):
    """[(id, 폴더, form.json 내용)] — 사용자 폴더가 기본 양식을 덮어쓴다."""
    found = {}
    for root in (roots or form_roots()):
        if not os.path.isdir(root):
            continue
        for name in sorted(os.listdir(root)):
            fj = os.path.join(root, name, "form.json")
            if os.path.isfile(fj):
                with open(fj, encoding="utf-8") as f:
                    meta = json.load(f)
                found[meta["id"]] = (meta["id"], os.path.join(root, name), meta)
    return list(found.values())


def load_form(form_id, roots=None):
    for fid, folder, meta in list_forms(roots):
        if fid == form_id:
            return KINDS[meta["kind"]](folder)
    raise KeyError("양식을 찾을 수 없어요: %s" % form_id)


def add_report_form(sample_path, form_id, name, audience, fields, note=""):
    """사용자가 준 hwpx 샘플로 보고서 양식을 사용자 폴더에 등록한다."""
    out = os.path.join(user_dir(), "forms", form_id)
    if os.path.isdir(out):
        shutil.rmtree(out)
    return report.register_report(sample_path, out, form_id, name, audience, fields, note=note)


def remove_user_form(form_id):
    out = os.path.join(user_dir(), "forms", form_id)
    if os.path.isdir(out):
        shutil.rmtree(out)
        return True
    return False
