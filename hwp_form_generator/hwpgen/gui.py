# -*- coding: utf-8 -*-
"""옆 창(사이드 패널) 화면.

한글 창 옆에 붙여 두고: 양식을 고르고 → 칸을 채우고 → [한글에 넣기] 를 누르면
글꼴·글자 크기·자간·들여쓰기·표가 양식 그대로 한꺼번에 들어간다.
탭: ① 보고서  ② 주간 업무(주간업무계획·주요일정)  ③ 양식 관리(내 양식 추가)
"""
import datetime
import os
import re
import sys
import tempfile
import tkinter as tk
from tkinter import filedialog, messagebox, simpledialog, ttk

from . import bridge as B
from . import profile as PR
from . import registry, weekly
from .symbols import SYMBOL_GROUPS
from . import compose as CMP
from . import textfmt as T
from .package import Package, HP, local, own_text

PANEL_W = 470
FONT = ("맑은 고딕", 10)
FONT_B = ("맑은 고딕", 10, "bold")
FONT_S = ("맑은 고딕", 9)
ACCENT = "#1f4e8c"


def tmp_hwpx(prefix):
    d = os.path.join(tempfile.gettempdir(), "HwpFormGenerator")
    os.makedirs(d, exist_ok=True)
    return os.path.join(d, "%s_%s.hwpx" % (prefix, datetime.datetime.now().strftime("%H%M%S")))


class Output:
    """만든 문서를 어디로 보낼지: 한글에 끼우기 / 새 문서로 열기 / 파일 저장"""

    def __init__(self, app):
        self.app = app

    def insert(self, pkg, prefix, replace=None):
        """열려 있는 한글 문서의 커서 위치에 서식째 넣는다(새 문서는 만들지 않음).
        - 샘플의 쪽 설정(secPr)은 빼서 문서 중간에서 쪽·여백이 바뀌지 않게 한다. 빈 문서일 때만 샘플 용지·여백을 적용.
        - 기본은 문단 단위로 차례대로 넣어 '입력되는' 느낌을 준다(더보기에서 '한 번에'로 바꿀 수 있음)."""
        import time
        app, br = self.app, self.app.bridge
        if not br.connected:
            try:
                br.connect(launch=False)
            except Exception:                            # noqa: BLE001
                pass
        if not br.connected:
            messagebox.showwarning("한글에 넣기", "열려 있는 한글에 연결하지 못했어요.\n"
                                   "한글을 열고 문서를 띄운 뒤 다시 눌러 주세요.")
            return
        from .package import page_setup_of, strip_section_props, split_chunks, with_lead_blank, with_tail_blank
        if replace:                                  # 이미 쓴 글을 이 양식으로 바꿔 넣기: 먼저 그 글(선택 또는 전체)을 지운다
            br.clear_target(replace == "all")
        page = page_setup_of(pkg)
        empty = br.is_empty_doc()
        keep_section = bool(empty and page)          # 빈 문서에 첫 양식을 넣을 때만 샘플의 쪽 설정(용지·여백)을 가져온다
        if keep_section:
            br.apply_page_setup(page)                # 되면 이쪽으로도 한 번 더 맞춰 둔다(안 돼도 괜찮음)
        else:
            strip_section_props(pkg)                 # 이미 내용이 있는 문서의 쪽 설정은 건드리지 않는다
        # 차례대로 입력은 커서가 문서 맨 끝일 때(빈 문서 포함)만: 매번 끝에 붙여 순서가 절대 뒤집히지 않게 한다.
        # 문서 중간에 넣을 때는 한 번에 넣어 순서를 지킨다.
        at_end = empty or br.at_doc_end()
        step = app.ui.get("insert_mode", "step") == "step" and at_end
        if not empty:
            br.prepare_insert(at_end)                # 앞 내용과 한 줄로 붙지 않게 새 줄에서 시작
            with_lead_blank(pkg)                     # 첫 문단이 앞 문단의 모양을 물려받지 않게 빈 받침 문단 하나
        with_tail_blank(pkg)                         # 끝에 빈 줄: 커서가 새 줄에 놓여 다음 ◦가 붙지 않는다
        chunks = split_chunks(pkg) if step else [pkg]
        n = len(chunks)
        try:
            for i, ch in enumerate(chunks, 1):
                if step:
                    br.move_doc_end()                # 매번 문서 끝에 붙인다(앞 조각이 남긴 받침 문단에 합쳐짐)
                path = ch.save(tmp_hwpx(prefix))
                try:
                    br.insert_file(path, keep_section=(keep_section and i == 1))
                finally:
                    try:
                        os.remove(path)
                    except OSError:
                        pass
                if n > 1:
                    app.say("입력 중… %d/%d" % (i, n))
                    app.update()
                    time.sleep(app.ui.get("insert_delay", min(0.45, max(0.12, 3.0 / n))))
            app.say("열려 있는 문서의 커서 위치에 넣었어요")
        except B.BridgeError as e:
            messagebox.showerror("한글에 넣기", str(e))

    def open_new(self, pkg, prefix, path=None):
        path = path or pkg.save(tmp_hwpx(prefix))
        br = self.app.bridge
        try:
            if br.connected:
                br.open_file(path)
                self.app.say("새 문서로 열었어요")
                return
        except B.BridgeError as e:
            messagebox.showwarning("한글에서 열기", str(e))
        if hasattr(os, "startfile"):
            os.startfile(path)            # 윈도우: 연결된 프로그램(한글)으로 열기
            self.app.say("한글로 열었어요")
        else:
            messagebox.showinfo("만들었어요", "한글이 없는 환경이라 파일만 만들었어요:\n%s" % path)

    def save_as(self, pkg, prefix):
        path = filedialog.asksaveasfilename(defaultextension=".hwpx", filetypes=[("한글 문서", "*.hwpx")],
                                            initialfile="%s.hwpx" % prefix)
        if path:
            pkg.save(path)
            self.app.say("저장했어요: %s" % path)


# ===================================================================== 보고서 탭
class ReportTab(ttk.Frame):
    def __init__(self, master, app):
        super().__init__(master, padding=8)
        self.app = app
        self.form = None
        self.vars = {}
        self.form_ids = {}
        ttk.Label(self, text="① 양식을 고르세요", font=FONT_B).pack(anchor="w")
        self.tree = ttk.Treeview(self, show="tree", height=6, selectmode="browse")
        self.tree.pack(fill="x", pady=(2, 6))
        self.tree.bind("<<TreeviewSelect>>", self.on_select)
        self.note = ttk.Label(self, text="", font=FONT_S, foreground="#555", wraplength=PANEL_W - 40, justify="left")
        self.note.pack(anchor="w")
        ttk.Label(self, text="② 칸을 채우세요", font=FONT_B).pack(anchor="w", pady=(6, 0))
        self.fields = ttk.Frame(self)
        self.fields.pack(fill="x")
        bar = ttk.Frame(self)
        bar.pack(fill="x", pady=(8, 0))
        ttk.Label(bar, text="③ 본문 (□ 제목 · ◦ 항목: 값 · - 세부 · | 표 |)", font=FONT_B).pack(side="left")
        ttk.Button(bar, text="입력 방법", width=8, command=self.help).pack(side="right")
        tools = ttk.Frame(self)
        tools.pack(fill="x", pady=2)
        for txt, ins in (("□ 제목", "\n□ "), ("◦ 항목", "\n◦ "), ("- 세부", "\n  - "), ("일시·장소", "\n◦ 일시: \n◦ 장소: "),
                         ("표 2칸", "\n| 구 분 | 내 용 |\n| | |"), ("표 3칸", "\n| 시 간 | 내 용 | 비 고 |\n| | | |")):
            ttk.Button(tools, text=txt, width=7 if len(txt) < 6 else 8, command=lambda s=ins: self.insert(s)).pack(side="left", padx=1)
        self.body = tk.Text(self, height=14, wrap="word", font=FONT, undo=True)
        self.body.pack(fill="both", expand=True, pady=4)
        btns = ttk.Frame(self)
        btns.pack(fill="x")
        ttk.Button(btns, text="한글에 넣기 (커서 위치)", command=lambda: self.go("insert")).pack(side="left", fill="x", expand=True, padx=1)
        ttk.Button(btns, text="새 문서로", command=lambda: self.go("open")).pack(side="left", padx=1)
        ttk.Button(btns, text="파일로 저장", command=lambda: self.go("save")).pack(side="left", padx=1)
        self.reload()

    def reload(self):
        self.tree.delete(*self.tree.get_children())
        self.form_ids.clear()
        groups = {}
        for fid, folder, meta in registry.list_forms():
            if meta["kind"] != "report":
                continue
            g = meta.get("audience", "기타")
            if g not in groups:
                groups[g] = self.tree.insert("", "end", text=g, open=True)
            item = self.tree.insert(groups[g], "end", text=meta["name"])
            self.form_ids[item] = fid
        if self.form_ids:
            first = next(iter(self.form_ids))
            self.tree.selection_set(first)

    def on_select(self, _e=None):
        sel = self.tree.selection()
        if not sel or sel[0] not in self.form_ids:
            return
        self.form = registry.load_form(self.form_ids[sel[0]])
        self.note.config(text=self.form.meta.get("note", ""))
        for w in self.fields.winfo_children():
            w.destroy()
        self.vars = {}
        prof = PR.load_profile()
        for i, f in enumerate(self.form.fields):
            ttk.Label(self.fields, text=f["label"], font=FONT_S).grid(row=i, column=0, sticky="w", pady=1)
            v = tk.StringVar()
            val = ""
            dv = f.get("default")
            if dv in ("today", "target_monday"):
                val = self.app.default_value(dv) if hasattr(self.app, "default_value") else datetime.date.today().isoformat()
            elif dv:
                val = dv
            if f.get("profile") and prof.get("autofill", True):
                val = PR.profile_value(prof, f["profile"]) or val
            v.set(val)
            self.vars[f["key"]] = v
            ttk.Entry(self.fields, textvariable=v, width=34).grid(row=i, column=1, sticky="ew", padx=4, pady=1)
        self.fields.columnconfigure(1, weight=1)
        self.body.delete("1.0", "end")
        plain = self.form.meta.get("style", {}).get("body_mode") == "plain"
        self.body.insert("1.0", "" if plain else (self.form.meta.get("starter") or T.DEFAULT_STARTER))

    def insert(self, s):
        self.body.insert("insert", s)
        self.body.focus_set()

    def help(self):
        messagebox.showinfo("본문 입력 방법", (
            "□ 행사개요          → 제목 줄 (양식의 □ 모양으로)\n"
            "◦ 일시: 2026. 11. 4.(수) 13:00   → 항목 (이름은 자동으로 '일    시' 처럼 맞춰요)\n"
            "◦ 그냥 글            → 이름 없는 항목\n"
            "- 세부 내용          → 하위 항목\n"
            "| 시 간 | 내 용 |    → 표 (첫 줄이 머리, 줄마다 | 로 칸 나눔)\n"
            "| 등록 및 개회 |      → 한 칸만 쓰면 줄 전체 합친 칸\n"
            "표 칸 안에서 줄 바꿈은 <br>\n\n"
            "※ 표 칸 수는 양식에 있는 표 모양만 쓸 수 있어요."))

    def values(self):
        return {k: v.get() for k, v in self.vars.items()}

    def go(self, how):
        if not self.form:
            return
        try:
            pkg = self.form.render(self.values(), self.body.get("1.0", "end") if self.form.meta.get("has_body", True) else "")
        except Exception as e:                      # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        title = self.vars.get("title").get() if "title" in self.vars else self.form.id
        name = re.sub(r'[\\/:*?"<>|]', "", title or self.form.id)[:40] or self.form.id
        out = self.app.out
        {"insert": out.insert, "open": out.open_new, "save": out.save_as}[how](pkg, name)


# ===================================================================== 주간 업무 탭
class ItemDialog(tk.Toplevel):
    def __init__(self, master, item=None, week_start=None, org_default=""):
        super().__init__(master)
        self.title("업무 항목")
        self.transient(master)
        self.grab_set()
        self.result = None
        it = dict(item or {})
        prof = PR.load_profile()
        self.v = {}
        rows = [("title", "행사·업무명"), ("date", "날짜 (2026-11-09)"), ("time", "시작 시각 (10:00)"),
                ("end_time", "끝나는 시각 (선택)"), ("place", "장소"), ("dept", "부서"), ("staff", "담당자"),
                ("phone", "내선"), ("attend", "국장님 (참석 등)"), ("note", "주요일정 비고")]
        defaults = {"dept": prof.get("dept", ""), "staff": prof.get("staff_name", ""), "phone": prof.get("staff_ext", ""),
                    "date": week_start.isoformat() if week_start else ""}
        for i, (k, lab) in enumerate(rows):
            ttk.Label(self, text=lab).grid(row=i, column=0, sticky="w", padx=8, pady=2)
            var = tk.StringVar(value=it.get(k) or (defaults.get(k, "") if not item else ""))
            self.v[k] = var
            ttk.Entry(self, textvariable=var, width=36).grid(row=i, column=1, padx=8, pady=2)
        n = len(rows)
        ttk.Label(self, text="그 밖의 줄\n(이름: 값, 한 줄씩)").grid(row=n, column=0, sticky="nw", padx=8)
        self.extra = tk.Text(self, width=36, height=6, font=FONT)
        extra = it.get("extra") or "\n".join("%s: %s" % (l, v) for l, v in it.get("lines", []))
        self.extra.insert("1.0", extra)
        self.extra.grid(row=n, column=1, padx=8, pady=2)
        self.imp = tk.BooleanVar(value=bool(it.get("important")))
        ttk.Checkbutton(self, text="주요일정에서 노랗게 강조 (시장·부시장 참석 등)", variable=self.imp).grid(
            row=n + 1, column=1, sticky="w", padx=8)
        bar = ttk.Frame(self)
        bar.grid(row=n + 2, column=0, columnspan=2, pady=8)
        ttk.Button(bar, text="확인", command=self.ok).pack(side="left", padx=4)
        ttk.Button(bar, text="취소", command=self.destroy).pack(side="left")

    def ok(self):
        d = {k: v.get().strip() for k, v in self.v.items()}
        if not d["title"]:
            messagebox.showwarning("항목", "행사·업무명을 적어 주세요", parent=self)
            return
        if d["date"]:
            try:
                d["date"] = T.parse_date(d["date"]).isoformat()
            except ValueError as e:
                messagebox.showwarning("날짜", str(e), parent=self)
                return
        d["extra"] = self.extra.get("1.0", "end").strip()
        d["important"] = self.imp.get()
        self.result = d
        self.destroy()


class WeeklyTab(ttk.Frame):
    def __init__(self, master, app):
        super().__init__(master, padding=8)
        self.app = app
        tm = getattr(app, "target_monday", None)
        self.monday = tm() if tm else T.week_monday(datetime.date.today())
        self.data = PR.load_week(self.monday)
        top = ttk.Frame(self)
        top.pack(fill="x")
        ttk.Button(top, text="◀", width=3, command=lambda: self.shift(-7)).pack(side="left")
        self.week_lbl = ttk.Label(top, font=FONT_B, anchor="center")
        self.week_lbl.pack(side="left", fill="x", expand=True)
        ttk.Button(top, text="▶", width=3, command=lambda: self.shift(7)).pack(side="left")
        ttk.Label(self, text="이번 주 업무·행사 (여기에 한 번만 적으면 두 문서가 모두 나와요)", font=FONT_S,
                  foreground="#555").pack(anchor="w", pady=(6, 0))
        cols = ("date", "time", "title", "place")
        self.tv = ttk.Treeview(self, columns=cols, show="headings", height=11)
        for c, t, w in (("date", "날짜", 70), ("time", "시간", 48), ("title", "행사·업무명", 200), ("place", "장소", 100)):
            self.tv.heading(c, text=t)
            self.tv.column(c, width=w, anchor="w")
        self.tv.pack(fill="both", expand=True, pady=4)
        self.tv.bind("<Double-1>", lambda e: self.edit())
        bar = ttk.Frame(self)
        bar.pack(fill="x")
        for t, c in (("추가", self.add), ("고치기", self.edit), ("복제", self.dup), ("지우기", self.delete)):
            ttk.Button(bar, text=t, width=7, command=c).pack(side="left", padx=1)
        ttk.Separator(self).pack(fill="x", pady=6)
        ttk.Label(self, text="주간업무계획에서 가져오기 (시 전체 계획에서 우리 국만)", font=FONT_S).pack(anchor="w")
        imp = ttk.Frame(self)
        imp.pack(fill="x", pady=2)
        ttk.Button(imp, text="열린 한글 문서에서", command=self.import_open).pack(side="left", fill="x", expand=True, padx=1)
        ttk.Button(imp, text="파일에서", command=self.import_file).pack(side="left", fill="x", expand=True, padx=1)
        ttk.Separator(self).pack(fill="x", pady=6)
        ttk.Label(self, text="문서 만들기", font=FONT_B).pack(anchor="w")
        mk = ttk.Frame(self)
        mk.pack(fill="x", pady=2)
        ttk.Button(mk, text="주간업무계획 → 한글에", command=lambda: self.make("plan", "insert")).pack(side="left", fill="x", expand=True, padx=1)
        ttk.Button(mk, text="주간 주요일정 → 한글에", command=lambda: self.make("schedule", "insert")).pack(side="left", fill="x", expand=True, padx=1)
        mk2 = ttk.Frame(self)
        mk2.pack(fill="x")
        ttk.Button(mk2, text="계획 새 문서", command=lambda: self.make("plan", "open")).pack(side="left", fill="x", expand=True, padx=1)
        ttk.Button(mk2, text="일정 새 문서", command=lambda: self.make("schedule", "open")).pack(side="left", fill="x", expand=True, padx=1)
        ttk.Button(mk2, text="파일 저장", command=lambda: self.make("both", "save")).pack(side="left", padx=1)
        self.refresh()

    # ---- 자료
    def org(self):
        return PR.load_profile().get("org") or "문화교육국"

    def shift(self, days):
        self.save()
        self.monday += datetime.timedelta(days=days)
        self.data = PR.load_week(self.monday)
        self.refresh()

    def save(self):
        PR.save_week(self.monday, self.data)

    def refresh(self):
        end = self.monday + datetime.timedelta(days=6)
        self.week_lbl.config(text="%s ~ %s" % (T.fmt_date_long(self.monday), T.fmt_date_short(end)))
        self.tv.delete(*self.tv.get_children())
        items = sorted(self.data["items"], key=lambda i: (i.get("date") or "9999", i.get("time") or ""))
        self.data["items"] = items
        for idx, it in enumerate(items):
            d = it.get("date") or ""
            shown = ""
            if d:
                dd = T.parse_date(d)
                shown = "%d.%d.(%s)" % (dd.month, dd.day, T.weekday_ko(dd))
            self.tv.insert("", "end", iid=str(idx), values=(shown, it.get("time", ""), it.get("title", ""), it.get("place", "")))

    def sel(self):
        s = self.tv.selection()
        return int(s[0]) if s else None

    def add(self):
        dlg = ItemDialog(self, None, self.monday)
        self.wait_window(dlg)
        if dlg.result:
            self.data["items"].append(dlg.result)
            self.save()
            self.refresh()

    def edit(self):
        i = self.sel()
        if i is None:
            return
        dlg = ItemDialog(self, self.data["items"][i], self.monday)
        self.wait_window(dlg)
        if dlg.result:
            self.data["items"][i] = dlg.result
            self.save()
            self.refresh()

    def dup(self):
        i = self.sel()
        if i is not None:
            self.data["items"].append(dict(self.data["items"][i]))
            self.save()
            self.refresh()

    def delete(self):
        i = self.sel()
        if i is not None and messagebox.askyesno("지우기", "이 항목을 지울까요?"):
            del self.data["items"][i]
            self.save()
            self.refresh()

    # ---- 가져오기
    def _import_lines(self, lines):
        orgs = [self.org()] + [o for o in weekly.DEFAULT_ORGS if o != self.org()]
        secs = weekly.split_org_sections(lines, orgs)
        key = self.org().replace(" ", "")
        if key not in secs:                              # 국 이름표가 없으면 전체를 우리 국 글로 본다
            items = weekly.parse_plan_lines(lines)
        else:
            items = weekly.parse_plan_lines(secs[key])
        if not items:
            messagebox.showinfo("가져오기", "일정 항목(❶ 제목 (부서 담당자 내선) + ◦ 일시 줄)을 찾지 못했어요")
            return
        have = {(i.get("date"), i.get("title")) for i in self.data["items"]}
        added = 0
        for it in items:
            if (it.get("date"), it.get("title")) in have:
                continue
            it["extra"] = "\n".join("%s: %s" % (l, v) for l, v in it.pop("lines", []))
            self.data["items"].append(it)
            added += 1
        self.save()
        self.refresh()
        self.app.say("%d개 가져왔어요 (이미 있던 %d개는 건너뜀)" % (added, len(items) - added))

    def import_open(self):
        try:
            text = self.app.bridge.get_text()
        except B.BridgeError as e:
            messagebox.showwarning("한글 문서", str(e))
            return
        self._import_lines(text.splitlines())

    def import_file(self):
        path = filedialog.askopenfilename(filetypes=[("한글 문서", "*.hwpx"), ("글 파일", "*.txt")])
        if not path:
            return
        try:
            if path.lower().endswith(".txt"):
                with open(path, encoding="utf-8", errors="replace") as f:
                    lines = f.read().splitlines()
            else:
                lines = weekly.plan_text_lines(Package.load(path))
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("가져오기", str(e))
            return
        self._import_lines(lines)

    # ---- 만들기
    def make(self, which, how):
        self.save()
        org = self.org()
        items = self.data["items"]
        try:
            docs = []
            if which in ("plan", "both"):
                f = registry.load_form("weekly_plan")
                docs.append((f.render({"org": org, "items": [i for i in items]}), "주간업무계획_%s" % self.monday.strftime("%m%d")))
            if which in ("schedule", "both"):
                f = registry.load_form("weekly_schedule")
                evs = weekly.extract_events(items)
                evs = [e for e in evs if self.monday <= T.parse_date(e["date"]) <= self.monday + datetime.timedelta(days=6)]
                docs.append((f.render({"org": org, "start": self.monday.isoformat(), "events": evs}),
                             "주간주요일정_%s" % self.monday.strftime("%m%d")))
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        out = self.app.out
        for pkg, name in docs:
            {"insert": out.insert, "open": out.open_new, "save": out.save_as}[how](pkg, name)


# ===================================================================== 양식 관리 탭
class AddFormDialog(tk.Toplevel):
    """샘플 한글 파일에서 바뀌는 칸을 골라 새 보고서 양식으로 등록"""

    def __init__(self, master, sample):
        super().__init__(master)
        self.title("양식 추가")
        self.geometry("560x560")
        self.transient(master)
        self.grab_set()
        self.sample = sample
        self.done = False
        pkg = Package.load(sample)
        from . import report
        self.sec = pkg.section(0)
        self.body = report.detect_body(self.sec)
        if self.body is None:
            messagebox.showerror("양식 추가", "□ 제목 줄이 있는 본문을 찾지 못했어요.\n본문에 □ 로 시작하는 제목 줄이 있는 샘플이어야 해요.", parent=self)
            self.after(10, self.destroy)
            return
        ttk.Label(self, text="이름").grid(row=0, column=0, sticky="w", padx=8, pady=4)
        self.name = tk.StringVar(value=os.path.splitext(os.path.basename(sample))[0][:30])
        ttk.Entry(self, textvariable=self.name, width=40).grid(row=0, column=1, sticky="ew", padx=8)
        ttk.Label(self, text="묶음 (예: ○○국 보고)").grid(row=1, column=0, sticky="w", padx=8, pady=4)
        self.aud = tk.StringVar(value="내가 추가한 양식")
        ttk.Entry(self, textvariable=self.aud, width=40).grid(row=1, column=1, sticky="ew", padx=8)
        ttk.Label(self, wraplength=520, justify="left", font=FONT_S, text=(
            "아래는 샘플 본문 밖에 있는 글자예요. 매번 바뀌는 것(제목·날짜·담당자 …)을 골라 두 번 클릭해\n"
            "'입력 칸 이름'을 적으세요. 고르지 않은 글자는 양식의 고정 글자로 남아요.\n"
            "(본문 □ 제목·◦ 항목·표 모양은 자동으로 견본이 만들어져요)")).grid(
            row=2, column=0, columnspan=2, sticky="w", padx=8, pady=4)
        self.tv = ttk.Treeview(self, columns=("text", "label"), show="headings", height=14)
        self.tv.heading("text", text="샘플의 글자")
        self.tv.heading("label", text="입력 칸 이름")
        self.tv.column("text", width=330)
        self.tv.column("label", width=150)
        self.tv.grid(row=3, column=0, columnspan=2, sticky="nsew", padx=8)
        self.rows = {}
        for i, text in report.frame_texts(self.sec, self.body):
            iid = self.tv.insert("", "end", values=(text.strip(), ""))
            self.rows[iid] = text
        self.tv.bind("<Double-1>", self.label_row)
        self.columnconfigure(1, weight=1)
        self.rowconfigure(3, weight=1)
        bar = ttk.Frame(self)
        bar.grid(row=4, column=0, columnspan=2, pady=8)
        ttk.Button(bar, text="양식 등록", command=self.ok).pack(side="left", padx=4)
        ttk.Button(bar, text="취소", command=self.destroy).pack(side="left")

    def label_row(self, _e):
        s = self.tv.selection()
        if not s:
            return
        cur = self.tv.item(s[0], "values")
        lab = simpledialog.askstring("입력 칸 이름", "이 글자가 바뀔 때 부를 이름 (비우면 고정 글자)\n예: 제목, 보고일, 담당자",
                                     initialvalue=cur[1], parent=self)
        if lab is not None:
            self.tv.item(s[0], values=(cur[0], lab.strip()))

    def ok(self):
        fields = []
        for n, iid in enumerate(self.tv.get_children(), 1):
            text, lab = self.tv.item(iid, "values")
            if not lab:
                continue
            f = {"key": "f%d" % n, "label": lab, "sample": text}
            if re.search(r"\d{4}\.\s*\d{1,2}\.\s*\d{1,2}", text):
                f["type"] = "date"
                f["fmt"] = "long" if re.search(r"\([월화수목금토일]\)", text) else ("dot" if text.rstrip().endswith(".") else "ymd")
                f["default"] = "today"
            if lab.strip() in ("제목", "사업명") and not any(x["key"] == "title" for x in fields):
                f["key"] = "title"
            fields.append(f)
        name = self.name.get().strip()
        if not name:
            messagebox.showwarning("양식 추가", "이름을 적어 주세요", parent=self)
            return
        fid = "u_" + re.sub(r"\W+", "_", name).strip("_")[:24] + datetime.datetime.now().strftime("%H%M%S")
        try:
            res = registry.add_report_form(self.sample, fid, name, self.aud.get().strip() or "내가 추가한 양식", fields)
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("양식 추가", str(e), parent=self)
            return
        messagebox.showinfo("양식 추가", "등록했어요.\n본문 견본: %s\n표 칸 수: %s" % (", ".join(res["protos"]), res["tables"] or "없음"), parent=self)
        self.done = True
        self.destroy()


class FormsTab(ttk.Frame):
    def __init__(self, master, app):
        super().__init__(master, padding=8)
        self.app = app
        ttk.Label(self, text="설치된 양식", font=FONT_B).pack(anchor="w")
        self.tv = ttk.Treeview(self, columns=("kind", "src"), show="tree headings", height=10)
        self.tv.heading("#0", text="이름")
        self.tv.heading("kind", text="종류")
        self.tv.heading("src", text="출처")
        self.tv.column("#0", width=250)
        self.tv.column("kind", width=80)
        self.tv.column("src", width=70)
        self.tv.pack(fill="both", expand=True, pady=4)
        bar = ttk.Frame(self)
        bar.pack(fill="x")
        ttk.Button(bar, text="양식 추가 (한글 파일 선택)", command=self.add).pack(side="left", fill="x", expand=True, padx=1)
        ttk.Button(bar, text="내 양식 지우기", command=self.remove).pack(side="left", padx=1)
        ttk.Separator(self).pack(fill="x", pady=8)
        ttk.Button(self, text="내 정보 (국·과·담당자) 설정", command=self.profile).pack(fill="x")
        ttk.Button(self, text="내 양식 폴더 열기", command=self.open_dir).pack(fill="x", pady=2)
        ttk.Label(self, wraplength=PANEL_W - 40, justify="left", font=FONT_S, foreground="#555", text=(
            "새 보고 양식은 그 양식으로 쓴 한글 파일(.hwpx) 하나만 주면 돼요.\n"
            "한글에서 '다른 이름으로 저장 → hwpx' 로 저장한 파일을 고르면, 글꼴·글자 크기·자간·들여쓰기·표 모양을 "
            "그대로 읽어 양식으로 만들어요. 내 정보는 이 PC 에만 저장돼요.")).pack(anchor="w", pady=6)
        self.refresh()

    def refresh(self):
        self.tv.delete(*self.tv.get_children())
        user_root = os.path.join(registry.user_dir(), "forms")
        names = {"report": "보고서", "weekly_plan": "주간계획", "weekly_schedule": "주요일정"}
        for fid, folder, meta in registry.list_forms():
            mine = os.path.abspath(folder).startswith(os.path.abspath(user_root))
            self.tv.insert("", "end", iid=fid, text=meta["name"], values=(names.get(meta["kind"], meta["kind"]), "내 양식" if mine else "기본"))

    def add(self):
        path = filedialog.askopenfilename(title="양식으로 쓸 한글 파일", filetypes=[("한글 문서", "*.hwpx *.hwp")])
        if not path:
            return
        if path.lower().endswith(".hwp"):                      # 옛 형식은 열려 있는 한글이 hwpx 로 바꿔 준다
            try:
                if not self.app.bridge.connected:
                    self.app.bridge.connect(launch=False)
                out = tmp_hwpx("convert")
                path = self.app.bridge.convert_to_hwpx(path, out)
            except Exception as e:                           # noqa: BLE001
                messagebox.showerror("양식 추가", "%s\n\n한글을 열어 둔 상태에서 다시 해 보시거나, 한글에서 '다른 이름으로 저장 → HWPX' 로 저장한 파일을 고르세요." % e)
                return
        try:
            Package.load(path)
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("양식 추가", str(e))
            return
        dlg = AddFormDialog(self, path)
        self.wait_window(dlg)
        if dlg.done:
            self.refresh()
            self.app.report_tab.reload()

    def remove(self):
        s = self.tv.selection()
        if not s:
            return
        if self.tv.item(s[0], "values")[1] != "내 양식":
            messagebox.showinfo("지우기", "기본 양식은 지울 수 없어요 (내가 추가한 양식만)")
            return
        if messagebox.askyesno("지우기", "이 양식을 지울까요?"):
            registry.remove_user_form(s[0])
            self.refresh()
            self.app.report_tab.reload()

    def open_dir(self):
        d = os.path.join(registry.user_dir(), "forms")
        os.makedirs(d, exist_ok=True)
        if hasattr(os, "startfile"):
            os.startfile(d)
        else:
            messagebox.showinfo("폴더", d)

    def profile(self):
        ProfileDialog(self)


class ProfileDialog(tk.Toplevel):
    def __init__(self, master):
        super().__init__(master)
        self.title("내 정보")
        self.transient(master)
        self.grab_set()
        cur = PR.load_profile()
        self.v = {}
        for i, (k, lab, _d) in enumerate(PR.PROFILE_FIELDS):
            ttk.Label(self, text=lab).grid(row=i, column=0, sticky="w", padx=8, pady=2)
            self.v[k] = tk.StringVar(value=cur.get(k, ""))
            ttk.Entry(self, textvariable=self.v[k], width=26).grid(row=i, column=1, padx=8, pady=2)
        self.auto = tk.BooleanVar(value=cur.get("autofill", True))
        n = len(PR.PROFILE_FIELDS)
        ttk.Checkbutton(self, text="양식에 이름·전화를 자동으로 채우기\n(끄면 담당자 칸을 빈칸으로 만들어요)", variable=self.auto).grid(
            row=n, column=0, columnspan=2, sticky="w", padx=8, pady=(8, 0))
        ttk.Button(self, text="저장", command=self.ok).grid(row=n + 1, column=0, columnspan=2, pady=8)

    def ok(self):
        data = {k: v.get().strip() for k, v in self.v.items()}
        data["autofill"] = bool(self.auto.get())
        PR.save_profile(data)
        self.destroy()


# ===================================================================== 본체 (상세 입력 창)
class DetailWindow(tk.Toplevel):
    """칸을 직접 채워서 만들 때 쓰는 상세 창 (위젯의 [상세 입력])"""

    def __init__(self, widget):
        super().__init__(widget)
        self.title("한글 양식 생성기 - 상세 입력")
        self.bridge = widget.bridge
        self.out = widget.out
        self.say = widget.say
        self.target_monday = widget.target_monday
        self.default_value = widget.default_value
        self.geometry("%dx%d+%d+%d" % (PANEL_W, 720, 60, 40))
        self.nb = ttk.Notebook(self)
        self.nb.pack(fill="both", expand=True)
        self.report_tab = ReportTab(self.nb, self)
        self.weekly_tab = WeeklyTab(self.nb, self)
        self.forms_tab = FormsTab(self.nb, self)
        self.nb.add(self.report_tab, text=" 보고서 ")
        self.nb.add(self.weekly_tab, text=" 주간 업무 ")
        self.nb.add(self.forms_tab, text=" 양식 관리 ")
        widget.detail = self
        self.protocol("WM_DELETE_WINDOW", self.close)
        self._wd = widget

    def close(self):
        self._wd.detail = None
        self._wd.rebuild()
        self.destroy()


# ===================================================================== 옆에 뜨는 위젯
WIDGET_W = 264
# 팔레트: Linear(인디고) + Notion(차분한 회색 글자) + 윈도우 11 유리(옅은 반투명 흰색)
C_BG = "#f3f4fb"
C_G1 = "#5e6ad2"          # 그라데이션 시작(인디고)
C_G2 = "#9a6bd6"          # 그라데이션 끝(보라)
C_CARD = "#ffffff"
C_LINE = "#e3e5f0"
C_HOVER = "#eceefe"
C_PRESS = "#dadefb"
C_TEXT = "#23283b"
C_SUB = "#7b8199"
C_ACC = "#5e6ad2"
C_OK = "#1f9d62"
C_BAD = "#d64545"
ICON_DIR = os.path.join(registry.app_dir(), "assets", "icons")
FONT_FILES = ("NotoSansKR-Regular.otf", "NotoSansKR-Bold.otf")


FONT_BTN = ("맑은 고딕", 11)


def init_fonts(root):
    """노토 산스(SIL OFL)를 프로그램 안에서만 등록해 쓰고, 못 쓰면 맑은 고딕"""
    global FONT, FONT_B, FONT_S
    fam = "맑은 고딕"
    try:
        if sys.platform == "win32":
            import ctypes
            d = os.path.join(registry.app_dir(), "assets", "fonts")
            for fn in FONT_FILES:
                p = os.path.join(d, fn)
                if os.path.exists(p):
                    ctypes.windll.gdi32.AddFontResourceExW(p, 0x10, 0)      # FR_PRIVATE
        from tkinter import font as tkfont
        fams = set(tkfont.families(root))
        for cand in ("Noto Sans CJK KR", "Noto Sans KR", "맑은 고딕", "Malgun Gothic"):
            if cand in fams:
                fam = cand
                break
    except Exception:                                    # noqa: BLE001
        pass
    FONT, FONT_B, FONT_S = (fam, 10), (fam, 10, "bold"), (fam, 9)
    global FONT_BTN
    FONT_BTN = (fam, 11)
    return fam


_ICONS = {}


def icon(name, white=False):
    key = (name, white)
    if key not in _ICONS:
        p = os.path.join(ICON_DIR, "%s_%s.png" % (name, "w" if white else "b"))
        try:
            _ICONS[key] = tk.PhotoImage(file=p)
        except tk.TclError:
            _ICONS[key] = None
    return _ICONS[key]


def _mix(c1, c2, t):
    a = [int(c1[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(c2[i:i + 2], 16) for i in (1, 3, 5)]
    return "#%02x%02x%02x" % tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def gradient_image(w, h, c1, c2):
    img = tk.PhotoImage(width=w, height=h)
    for x in range(w):
        img.put(_mix(c1, c2, x / max(1, w - 1)), to=(x, 0, x + 1, h))
    return img


class Tip:
    """마우스를 올리면 잠깐 뜨는 설명"""

    def __init__(self, widget, text):
        self.w, self.text, self.tw, self.job = widget, text, None, None
        widget.bind("<Enter>", self.enter, add="+")
        widget.bind("<Leave>", self.leave, add="+")
        widget.bind("<ButtonPress>", self.leave, add="+")

    def enter(self, _e=None):
        self.job = self.w.after(450, self.show)

    def show(self):
        if self.tw or not self.text:
            return
        x, y = self.w.winfo_rootx() + 10, self.w.winfo_rooty() + self.w.winfo_height() + 4
        self.tw = tk.Toplevel(self.w)
        self.tw.wm_overrideredirect(True)
        self.tw.attributes("-topmost", True)
        self.tw.geometry("+%d+%d" % (x, y))
        tk.Label(self.tw, text=self.text, bg="#2b3040", fg="white", font=FONT_S, padx=8, pady=4).pack()

    def leave(self, _e=None):
        if self.job:
            self.w.after_cancel(self.job)
            self.job = None
        if self.tw:
            self.tw.destroy()
            self.tw = None


def round_rect(cv, x0, y0, x1, y1, r, **kw):
    pts = [x0 + r, y0, x1 - r, y0, x1, y0, x1, y0 + r, x1, y1 - r, x1, y1, x1 - r, y1, x0 + r, y1, x0, y1, x0, y1 - r,
           x0, y0 + r, x0, y0]
    return cv.create_polygon(pts, smooth=True, **kw)


class _Anim:
    """호버·눌림을 부드럽게 오가게 하는 보간기: t(0=평소 → 1=올림)가 목표값으로 천천히 다가간다."""

    def _anim_init(self):
        self.t, self.target, self._job, self.pressed = 0.0, 0.0, None, False

    def _go(self, target):
        self.target = target
        if self._job is None:
            self._step()

    def _step(self):
        d = self.target - self.t
        if abs(d) < 0.03:
            self.t, self._job = self.target, None
            self.draw()
            return
        self.t += d * 0.32
        self.draw()
        self._job = self.after(16, self._step)

    def _bind_anim(self):
        self.bind("<Enter>", lambda e: self._go(1.0))
        self.bind("<Leave>", lambda e: (setattr(self, "pressed", False), self._go(0.0)))
        self.bind("<ButtonPress-1>", self._press)
        self.bind("<ButtonRelease-1>", self._release)

    def _press(self, _e):
        self.pressed = True
        self.draw()

    def _release(self, e):
        was = self.pressed
        self.pressed = False
        self.draw()
        if was and 0 <= e.x <= self.winfo_width() and 0 <= e.y <= self.winfo_height():
            self.cmd()


def pill(cv, x0, y0, x1, y1, **kw):
    return round_rect(cv, x0, y0, x1, y1, (y1 - y0) / 2, **kw)


class Btn(_Anim, tk.Canvas):
    """알약(pill) 모양 단추. 마우스를 올리면 테두리·배경·아이콘 원·화살표가 부드럽게 바뀌고 눌리면 살짝 눌림.
    kind: card(흰 알약) / accent(그라데이션 알약) / flat(투명)"""

    def __init__(self, master, text, command, kind="card", font=None, tip=None, icon_name=None, w=None, h=42,
                 anchor="w", radius=None):
        super().__init__(master, height=h, width=w or 10, bg=C_BG, highlightthickness=0, bd=0, cursor="hand2")
        self.kind, self.cmd, self.text, self.fnt, self.anchor = kind, command, text, font, anchor
        self.icon_name = icon_name
        self._anim_init()
        if w is None:
            self.pack_propagate(False)
        self.bind("<Configure>", lambda e: self.draw())
        self._bind_anim()
        if tip:
            Tip(self, tip)

    def draw(self):
        self.delete("all")
        w, h = self.winfo_width(), self.winfo_height()
        if w < 4:
            return
        t, acc = self.t, self.kind == "accent"
        dy = 1 if self.pressed else 0
        if acc:
            lift = 0.10 * t - (0.12 if self.pressed else 0)
            r = (h - 2) / 2
            for x in range(1, w - 1):
                edge = max(0.0, r - min(x - 1, w - 2 - x))
                inset = r - (r * r - edge * edge) ** 0.5 if edge > 0 else 0
                col = _mix(C_G1, C_G2, x / max(1, w - 1))
                col = _mix(col, "#ffffff" if lift >= 0 else "#000000", abs(lift))
                self.create_line(x, 1 + inset, x, h - 1 - inset, fill=col)
            fg = "white"
        else:
            idle = C_CARD if self.kind == "card" else C_BG
            fill = _mix(idle, C_HOVER, t)
            if self.pressed:
                fill = C_PRESS
            line = _mix(C_LINE if self.kind == "card" else C_BG, "#aeb6f2", t)
            pill(self, 1, 1 + dy * 0, w - 2, h - 2, fill=fill, outline=line)
            fg = _mix(C_TEXT, "#2b3470", t)
        ic = icon(self.icon_name, white=acc or t > 0.5) if self.icon_name else None
        x = 10
        if self.anchor == "w":
            if ic is not None:
                d = h - 14
                cy = h // 2 + dy
                if not acc:               # 아이콘 둥근 바탕: 평소 옅은 보라 → 올리면 진한 인디고
                    self.create_oval(x, cy - d // 2, x + d, cy + d // 2, fill=_mix("#eef0fd", C_ACC, t), outline="")
                self.create_image(x + d // 2, cy, image=ic)
                x += d + 10
            else:
                x = 18
            self.create_text(x, h // 2 + dy, text=self.text, anchor="w", fill=fg, font=self.fnt or FONT_BTN)
            if not acc and self.kind != "flat" or self.kind == "flat":
                # 올리면 오른쪽에서 슬며시 나타나는 화살표
                self.create_text(w - 18 - (1 - t) * 8, h // 2 + dy, text="›", anchor="e",
                                 fill=_mix(C_CARD if self.kind == "card" else C_BG, C_ACC, t), font=(FONT[0], 14, "bold"))
        else:
            if ic is not None:
                self.create_image(w // 2, h // 2 + dy, image=ic)
            else:
                self.create_text(w // 2, h // 2 + dy, text=self.text, fill=fg, font=self.fnt or FONT_BTN)


class Chip(_Anim, tk.Canvas):
    """작은 둥근 단추(기호·숫자): 올리면 테두리가 인디고로, 살짝 커지는 느낌"""

    def __init__(self, master, text, command, tip=None, size=34, font=None, badge=None):
        super().__init__(master, width=size, height=size, bg=C_BG, highlightthickness=0, bd=0, cursor="hand2")
        self.text, self.cmd, self.size, self.fnt = text, command, size, font
        self.badge = badge            # (숫자, 속이 찼는지) — 원 숫자는 글꼴마다 크기가 달라 직접 그린다
        self._anim_init()
        self._bind_anim()
        self.draw()
        if tip:
            Tip(self, tip)

    def draw(self):
        self.delete("all")
        s, t = self.size, self.t
        grow = 1.5 * t - (1.5 if self.pressed else 0)
        fill = C_PRESS if self.pressed else _mix(C_CARD, C_HOVER, t)
        round_rect(self, 1 - grow, 1 - grow, s - 2 + grow, s - 2 + grow, 10, fill=fill, outline=_mix(C_LINE, C_ACC, t))
        c = s // 2
        if self.badge:
            n, solid = self.badge
            r = 11
            col = _mix(C_TEXT, C_ACC, t)
            self.create_oval(c - r, c - r, c + r, c + r, outline=col, width=1.6, fill=col if solid else "")
            self.create_text(c, c, text=str(n), fill="white" if solid else col,
                             font=(FONT[0], 9 if n < 10 else 8, "bold"))
            return
        self.create_text(c, c, text=self.text, fill=_mix(C_TEXT, C_ACC, t), font=self.fnt or FONT)


class Widget(tk.Tk):
    """한글 옆에 떠 있는 양식 단추 모음. 단추를 누르면 열려 있는 한글 문서에 바로 만들어 넣는다."""

    TABS = [("보고", "report"), ("주간", "week"), ("예산", "budget"), ("보도", "press"),
            ("국회", "assembly"), ("감사", "audit"), ("국비", "nat"), ("도구", "tools"), ("기호", "sym"),
            ("더보기", "more")]
    # 양식 묶음(audience) → 탭. 사용자가 추가한 양식은 '보고' 탭에 들어간다
    AUDIENCE_TAB = {"문화교육국 보고": "report", "시장님 보고": "report", "간부회의": "week", "예산 설명자료": "budget", "참고자료(백데이터)": "budget",
                    "보도자료": "press", "국회단계 질의서": "assembly", "국회단계 서면질의서": "assembly",
                    "국회단계 정책질의서": "assembly", "행정사무감사": "audit", "이월 조서": "budget",
                    "국가예산 신규사업": "nat"}
    GROUP_ORDER = ["문화교육국 보고", "시장님 보고", "간부회의", "예산 설명자료", "참고자료(백데이터)", "보도자료", "국회단계 서면질의서",
                   "국회단계 정책질의서", "국회단계 질의서", "행정사무감사", "이월 조서", "국가예산 신규사업"]
    FORM_ORDER = ["report_event", "report_project", "report_mayor", "meeting_cover", "meeting_issue", "meeting_card",
                  "budget_cover", "budget_project", "budget_ref_cover", "budget_ref_detail", "budget_ref_return", "press_release",
                  "written_cover", "written_list", "written_item", "policy_cover", "policy_list", "policy_item",
                  "policy_ref", "audit_cover", "audit_common", "audit_item", "carry_item", "nbud_main", "nbud_ref1", "nbud_ref2"]
    FORM_ICONS = {"report_event": "event", "report_project": "project", "report_mayor": "mayor",
                  "meeting_cover": "cover", "meeting_issue": "event", "meeting_card": "project",
                  "audit_cover": "cover", "audit_common": "plan", "audit_item": "project", "carry_item": "project",
                  "nbud_main": "nation", "nbud_ref1": "table", "nbud_ref2": "doc",
                  "budget_cover": "cover", "budget_project": "project", "budget_ref_cover": "cover",
                  "budget_ref_detail": "project", "budget_ref_return": "plan"}

    def __init__(self, bridge=None):
        super().__init__()
        init_fonts(self)
        self.title("양식 도우미")
        self.bridge = bridge or B.HwpBridge()
        self.out = Output(self)
        self.detail = None
        self.ui = PR.load_ui()
        self.tab = self.ui.get("tab", "report")
        self.collapsed = False
        self.docked = False
        self._msg_job = None
        self.configure(bg=C_BG)
        self.attributes("-topmost", True)
        self.alpha = float(self.ui.get("alpha", 0.9))
        self.sharp_hover = bool(self.ui.get("sharp_hover", True))
        self.apply_alpha(self.alpha)
        self.bind("<Enter>", lambda e: self._hover_alpha(True))
        self.bind("<Leave>", lambda e: self._hover_alpha(False))
        self.resizable(False, True)
        self.minsize(WIDGET_W, 300)
        self.geometry("%dx%d+%d+%d" % (WIDGET_W, 700, self.ui.get("x", max(0, self.winfo_screenwidth() - WIDGET_W - 10)),
                                       self.ui.get("y", 60)))
        try:
            ttk.Style().theme_use("vista" if sys.platform == "win32" else "clam")
        except tk.TclError:
            pass
        self.shape = tk.IntVar(value=self.ui.get("shape", 0))
        self.cmp_text = tk.StringVar()
        self.q = tk.StringVar()
        self._build_frame()
        self.show_tab(self.tab)
        self.protocol("WM_DELETE_WINDOW", self.on_close)
        self.after(100, self.win11_style)
        self.after(300, self.auto_connect)
        self.after(1500, self.tick)

    def apply_alpha(self, a):
        try:
            self.attributes("-alpha", max(0.4, min(1.0, a)))
        except tk.TclError:
            pass

    def _hover_alpha(self, inside):
        """창 위에 마우스가 있으면 선명하게, 벗어나면 설정한 투명도로"""
        if not self.sharp_hover:
            return
        if inside:
            self.apply_alpha(1.0)
        else:
            x, y = self.winfo_pointerxy()
            w = self.winfo_containing(x, y)
            if w is None or w.winfo_toplevel() is not self:
                self.apply_alpha(self.alpha)

    def set_alpha(self, pct):
        self.alpha = float(pct) / 100.0
        self.ui["alpha"] = self.alpha
        self.apply_alpha(self.alpha)

    def set_sharp(self):
        self.sharp_hover = bool(self._sharp_var.get())
        self.ui["sharp_hover"] = self.sharp_hover
        self.apply_alpha(self.alpha)

    def win11_style(self):
        """윈도우 11: 둥근 창 모서리 + 어두운 제목줄 (되는 경우에만)"""
        if sys.platform != "win32":
            return
        try:
            import ctypes
            hwnd = ctypes.windll.user32.GetParent(self.winfo_id())
            dwm = ctypes.windll.dwmapi.DwmSetWindowAttribute
            dwm(hwnd, 33, ctypes.byref(ctypes.c_int(2)), 4)         # DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
            dwm(hwnd, 35, ctypes.byref(ctypes.c_int(0x00d26a5e)), 4)  # 제목줄 색(BGR) = 인디고
            dwm(hwnd, 36, ctypes.byref(ctypes.c_int(0x00ffffff)), 4)  # 제목줄 글자 흰색
        except Exception:                                # noqa: BLE001
            pass

    # ---------------------------------------------------------------- 뼈대
    def _build_frame(self):
        W = WIDGET_W
        self.head = tk.Canvas(self, width=W, height=54, highlightthickness=0, bd=0)
        self.head.pack(fill="x")
        self._hg = gradient_image(W + 200, 54, C_G1, C_G2)
        self.head.create_image(0, 0, image=self._hg, anchor="nw")
        self.head.create_text(14, 20, text="양식 도우미", anchor="w", fill="white", font=(FONT[0], 12, "bold"))
        self.head.create_text(14, 39, text="한글 옆에서 한 번에", anchor="w", fill="#dfe3ff", font=FONT_S)
        for i, (nm, cmd, tip) in enumerate((("minus", self.toggle, "접기·펴기"), ("dock", self.dock, "한글 옆에 붙이기"),
                                            ("tray", self.to_tray, "트레이로 보내기"))):
            ic = icon(nm, white=True)
            x = W - 22 - i * 30
            self.head.create_oval(x - 13, 27 - 13, x + 13, 27 + 13, fill="", outline="", tags=("hb%d" % i,))
            self.head.create_image(x, 27, image=ic, tags=("hb%d" % i,))
            self.head.tag_bind("hb%d" % i, "<Button-1>", lambda e, c=cmd: c())
            self.head.tag_bind("hb%d" % i, "<Enter>", lambda e: self.head.config(cursor="hand2"))
            self.head.tag_bind("hb%d" % i, "<Leave>", lambda e: self.head.config(cursor=""))
        self.chip = tk.Label(self, text="", bg=C_BG, font=FONT_S, anchor="w", padx=14, pady=5, cursor="hand2")
        self.chip.pack(fill="x")
        self.chip.bind("<Button-1>", lambda e: self.connect() if not self.bridge.connected else None)
        self.tabbar = tk.Frame(self, bg=C_BG)
        self.tabbar.pack(fill="x", padx=8)
        self.tab_lbl, self.tab_bar = {}, {}
        per_row = 5
        for c in range(per_row):
            self.tabbar.columnconfigure(c, weight=1, uniform="tab")
        for idx, (name, key) in enumerate(self.TABS):
            col = tk.Frame(self.tabbar, bg=C_BG)
            col.grid(row=idx // per_row, column=idx % per_row, sticky="ew", padx=1)
            l = tk.Label(col, text=name, bg=C_BG, fg=C_SUB, font=FONT, padx=4, pady=4, cursor="hand2")
            l.pack()
            bar = tk.Frame(col, bg=C_BG, height=2)
            bar.pack(fill="x", padx=6)
            l.bind("<Button-1>", lambda e, k=key: self.show_tab(k))
            l.bind("<Enter>", lambda e, w=l, k=key: (w.config(fg=C_ACC), self.tab_bar[k].config(bg="#c9cdf6")) if k != self.tab else None)
            l.bind("<Leave>", lambda e, w=l, k=key: (w.config(fg=C_SUB), self.tab_bar[k].config(bg=C_BG)) if k != self.tab else None)
            self.tab_lbl[key], self.tab_bar[key] = l, bar
        tk.Frame(self, bg=C_LINE, height=1).pack(fill="x", padx=8)
        self.body = tk.Frame(self, bg=C_BG)
        self.body.pack(fill="both", expand=True, padx=10, pady=6)
        self.status = tk.Label(self, text="", bg=C_BG, fg=C_SUB, font=FONT_S, anchor="w", padx=14, pady=7,
                               wraplength=WIDGET_W - 28, justify="left")
        self.status.pack(fill="x", side="bottom")
        tk.Frame(self, bg=C_LINE, height=1).pack(fill="x", side="bottom")
        self.hint()
        self.update_conn()

    def show_tab(self, key):
        self.tab = key
        for k, l in self.tab_lbl.items():
            on = k == key
            l.config(fg=C_ACC if on else C_SUB, font=FONT_B if on else FONT)
            self.tab_bar[k].config(bg=C_ACC if on else C_BG)
        self.unbind_all("<MouseWheel>")
        self.unbind_all("<Button-4>")
        self.unbind_all("<Button-5>")
        for w in self.body.winfo_children():
            w.destroy()
        {"week": self.build_week, "sym": self.build_symbols, "more": self.build_more, "tools": self.build_tools}.get(
            key, lambda parent, k=key: self.build_forms_tab(parent, k))(self.body)
        self.ui["tab"] = key

    def section(self, parent, text):
        tk.Label(parent, text=text, bg=C_BG, fg=C_SUB, font=(FONT[0], 9, "bold"), anchor="w").pack(fill="x", pady=(12, 4))

    def rebuild(self):
        self.show_tab(self.tab)

    # ---------------------------------------------------------------- 알림
    def hint(self):
        self.status.config(text="양식을 누르면 열려 있는 한글에 만들어져요", fg=C_SUB)

    def say(self, msg, ok=True):
        self.status.config(text=("✔  " if ok else "⚠  ") + msg, fg=C_OK if ok else C_BAD)
        if self._msg_job:
            self.after_cancel(self._msg_job)
        self._msg_job = self.after(3500, self.hint)

    # ---------------------------------------------------------------- 양식 탭(보고·예산설명·보도자료·국회질의·행정감사)
    def tab_forms(self, tab):
        """이 탭에 들어갈 보고서류 양식 {묶음: [(id, meta)]} (순서 고정)"""
        groups = {}
        for fid, folder, meta in registry.list_forms():
            if meta["kind"] != "report":
                continue
            aud = meta.get("audience", "기타")
            if self.AUDIENCE_TAB.get(aud, "report") == tab:
                groups.setdefault(aud, []).append((fid, meta))
        key = lambda kv: self.GROUP_ORDER.index(kv[0]) if kv[0] in self.GROUP_ORDER else 99      # noqa: E731
        fkey = lambda it: self.FORM_ORDER.index(it[0]) if it[0] in self.FORM_ORDER else 99       # noqa: E731
        return {k: sorted(v, key=fkey) for k, v in sorted(groups.items(), key=key)}

    def build_forms_tab(self, parent, tab):
        if tab == "report":
            prof = PR.load_profile()
            if not (prof.get("dept") or prof.get("staff_name")):
                box = tk.Frame(parent, bg="#fff7e0", highlightthickness=1, highlightbackground="#f1dfa2")
                box.pack(fill="x", pady=(0, 4))
                tk.Label(box, text="내 정보(과·담당자)를 한 번 적어 두면\n양식에 자동으로 채워져요", bg="#fff7e0", fg="#6b5600",
                         font=FONT_S, justify="left", padx=10, pady=6).pack(anchor="w")
                Btn(box, "내 정보 입력", self.open_profile, icon_name="user", h=32).pack(fill="x", padx=8, pady=(0, 8))
        self._convert_var = tk.IntVar(value=1 if self.ui.get("convert") else 0)
        tk.Checkbutton(parent, text="이미 쓴 글을 이 양식으로 바꾸기", variable=self._convert_var, bg=C_BG, fg=C_TEXT,
                       font=FONT_S, activebackground=C_BG, selectcolor=C_CARD, highlightthickness=0, anchor="w",
                       command=lambda: self.ui.__setitem__("convert", bool(self._convert_var.get()))).pack(fill="x", pady=(2, 0))
        box = self.scroll_area(parent)
        self.forms_box = box
        groups = self.tab_forms(tab)
        if not groups:
            tk.Label(box, text="이 탭에 양식이 없어요", bg=C_BG, fg=C_SUB, font=FONT_S).pack(pady=10)
        for g, lst in groups.items():
            self.section(box, g)
            for fid, meta in lst:
                self._form_btn(meta, fid)
            if any(f == "report_mayor" for f, _ in lst):
                Btn(box, "참고자료 쪽 추가", self.make_reference, kind="flat", icon_name="clip", h=32,
                    tip="보고 1장 뒤에 새 쪽과 '참고자료' 제목을 붙여요").pack(fill="x", pady=2)

    def _form_btn(self, meta, fid, parent=None):
        label = meta["name"].split(" (")[0]
        for pre in ("행정사무감사 ", "간부회의 "):          # 묶음 이름과 겹치는 앞말은 뺀다(좁은 창에서 안 잘리게)
            if label.startswith(pre) and len(label) > len(pre):
                label = label[len(pre):]
        Btn(parent or self.forms_box, label, lambda f=fid: self.make_report(f),
            tip=meta.get("note", ""), icon_name=self.FORM_ICONS.get(fid, "doc")).pack(fill="x", pady=4)

    # ---------------------------------------------------------------- 주간 탭
    def target_monday(self):
        """작성할 주의 월요일. 기본은 '다음 주'(보통 다음 주 계획을 이번 주에 쓰므로) — ◀ ▶ 로 바꿀 수 있다"""
        off = int(self.ui.get("week_offset", 1))
        return T.week_monday(datetime.date.today()) + datetime.timedelta(days=7 * off)

    def shift_week(self, d):
        self.ui["week_offset"] = int(self.ui.get("week_offset", 1)) + d
        self.show_tab("week")

    def default_value(self, token):
        """양식 칸의 기본값 약속어: today(오늘) / target_monday(작성 대상 주의 월요일)"""
        if token == "today":
            return datetime.date.today().isoformat()
        if token == "target_monday":
            return self.target_monday().isoformat()
        return ""

    def build_week(self, parent):
        monday = self.target_monday()
        end = monday + datetime.timedelta(days=6)
        off = int(self.ui.get("week_offset", 1))
        name = {-1: "지난 주", 0: "이번 주", 1: "다음 주", 2: "다다음 주"}.get(off, "%+d주" % off)
        bar = tk.Frame(parent, bg=C_BG)
        bar.pack(fill="x", pady=(6, 0))
        Chip(bar, "◀", lambda: self.shift_week(-1), size=30, tip="앞 주").pack(side="left")
        mid = tk.Frame(bar, bg=C_BG)
        mid.pack(side="left", fill="x", expand=True)
        tk.Label(mid, text="%s 작성" % name, bg=C_BG, fg=C_ACC, font=FONT_B).pack()
        tk.Label(mid, text="%s ~ %s" % (T.fmt_date_short(monday), T.fmt_date_short(end)), bg=C_BG, fg=C_SUB,
                 font=FONT_S).pack()
        Chip(bar, "▶", lambda: self.shift_week(1), size=30, tip="다음 주").pack(side="right")
        n = len(PR.load_week(monday).get("items", []))
        tk.Label(parent, text="입력한 일정 %d건" % n, bg=C_BG, fg=C_SUB, font=FONT_S, anchor="w").pack(fill="x", pady=(4, 0))
        box = self.scroll_area(parent)
        self.section(box, "주간업무")
        for txt, which, ic, tip in (("주간업무계획 표지", "cover", "cover", "날짜 상자·제목 배너·순서 목차 (대상 주 날짜 자동)"),
                                    ("주간업무계획", "plan", "plan", "대상 주 일정을 ❶ 행사명 + ◦ 일시/장소로"),
                                    ("주간 주요일정", "schedule", "schedule", "월~일 표에 시간·일정명·장소")):
            Btn(box, txt, lambda w=which: self.make_week(w), tip=tip, icon_name=ic).pack(fill="x", pady=4)
        Btn(box, "일정 입력·가져오기", lambda: self.open_detail("weekly"), kind="flat", icon_name="plus", h=32,
            tip="한 번 적으면 계획과 주요일정이 같이 나와요").pack(fill="x", pady=2)
        for g, lst in self.tab_forms("week").items():
            self.section(box, g)
            for fid, meta in lst:
                self._form_btn(meta, fid, box)

    # ---------------------------------------------------------------- 기호 탭
    def scroll_area(self, parent):
        """길어지는 탭용 스크롤 영역 (마우스 휠 지원) → 안쪽 프레임"""
        cv = tk.Canvas(parent, bg=C_BG, highlightthickness=0)
        sb = ttk.Scrollbar(parent, orient="vertical", command=cv.yview)
        inner = tk.Frame(cv, bg=C_BG)
        win = cv.create_window((0, 0), window=inner, anchor="nw")
        inner.bind("<Configure>", lambda e: cv.configure(scrollregion=cv.bbox("all")))
        cv.bind("<Configure>", lambda e: cv.itemconfigure(win, width=e.width))
        cv.configure(yscrollcommand=sb.set)
        sb.pack(side="right", fill="y")
        cv.pack(side="left", fill="both", expand=True)

        def wheel(e):
            cv.yview_scroll(-1 if (e.delta > 0 or e.num == 4) else 1, "units")
        for ev in ("<MouseWheel>", "<Button-4>", "<Button-5>"):
            cv.bind_all(ev, wheel)
        return inner

    def build_symbols(self, parent):
        parent = self.scroll_area(parent)
        self.build_compose(parent)                        # 글자 겹치기(네모·원 안 숫자)를 맨 위에
        recent = self.ui.get("recent_syms", [])
        if recent:
            self.section(parent, "최근")
            self._sym_grid(parent, [(c, "") for c in recent[:12]])
        for name, items in SYMBOL_GROUPS:
            self.section(parent, name)
            self._sym_grid(parent, items)

    def _sym_grid(self, parent, items):
        import unicodedata
        grid = tk.Frame(parent, bg=C_BG)
        grid.pack(anchor="w")
        for i, (ch, tip) in enumerate(items):
            badge = None
            name = unicodedata.name(ch, "")
            if "CIRCLED DIGIT" in name or "CIRCLED NUMBER" in name or "NEGATIVE CIRCLED" in name or "DINGBAT" in name and "CIRCLED" in name:
                num = unicodedata.numeric(ch, None)
                if num is not None and 1 <= num <= 20:
                    badge = (int(num), "NEGATIVE" in name)
            Chip(grid, ch, lambda c=ch, t=tip: self.put_symbol(c, t), tip=tip or None, size=34,
                 font=(FONT[0], 12), badge=badge).grid(row=i // 6, column=i % 6, padx=2, pady=2)

    # ---------------------------------------------------------------- 도구 탭 (행정에서 자주 쓰는 것)
    def build_tools(self, parent):
        parent = self.scroll_area(parent)
        self.section(parent, "표 만들기")
        row = tk.Frame(parent, bg=C_BG)
        row.pack(fill="x")
        self._tb_rows = tk.IntVar(value=int(self.ui.get("tb_rows", 3)))
        self._tb_cols = tk.IntVar(value=int(self.ui.get("tb_cols", 3)))
        for lab, var in (("줄", self._tb_rows), ("칸", self._tb_cols)):
            tk.Label(row, text=lab, bg=C_BG, fg=C_SUB, font=FONT_S).pack(side="left", padx=(0, 3))
            tk.Spinbox(row, from_=1, to=30, width=3, textvariable=var, font=FONT, justify="center", relief="flat",
                       highlightthickness=1, highlightbackground=C_LINE).pack(side="left", padx=(0, 10))
        Btn(parent, "표 넣기", self.put_table, icon_name="table", tip="커서 위치에 줄×칸 표를 만들어요").pack(fill="x", pady=4)
        self.section(parent, "오늘 날짜")
        today = datetime.date.today()
        wd = "월화수목금토일"[today.weekday()]
        for txt in ("%d. %d. %d." % (today.year, today.month, today.day),
                    "%d. %d. %d.(%s)" % (today.year, today.month, today.day, wd),
                    "%d년 %d월 %d일" % (today.year, today.month, today.day),
                    "’%02d. %d. %d." % (today.year % 100, today.month, today.day)):
            Btn(parent, txt, lambda t=txt: self.put_text(t), kind="flat", icon_name="date", h=34).pack(fill="x", pady=2)
        self.section(parent, "쪽")
        Btn(parent, "쪽 나누기", self.put_pagebreak, icon_name="pagebreak", tip="커서 위치에서 다음 쪽으로").pack(fill="x", pady=4)
        self.section(parent, "자주 쓰는 문구")
        grid = tk.Frame(parent, bg=C_BG)
        grid.pack(fill="x")
        phrases = ["붙임 ", "끝.", "※ ", "(단위 : 천원)", "(단위 : 원)", "<참고>", "(참고)", "해당없음", "- 이하 여백 -", "추진실적", "향후계획", "기대효과"]
        for i, ph in enumerate(phrases):
            Btn(grid, ph.strip() or ph, lambda p=ph: self.put_text(p), kind="flat", h=32, icon_name="text").grid(
                row=i // 2, column=i % 2, sticky="ew", padx=2, pady=2)
        grid.columnconfigure(0, weight=1)
        grid.columnconfigure(1, weight=1)
        self.section(parent, "금액 바꿔 쓰기")
        self._money = tk.StringVar()
        ent = tk.Entry(parent, textvariable=self._money, font=FONT, relief="flat", highlightthickness=1,
                       highlightbackground=C_LINE, highlightcolor=C_ACC)
        ent.pack(fill="x", ipady=5)
        ent.bind("<KeyRelease>", lambda e: self.refresh_money())
        tk.Label(parent, text="숫자(원 단위)를 쓰면 쓰는 방식별로 바꿔줘요. 누르면 한글에 넣어요", bg=C_BG, fg=C_SUB, font=FONT_S,
                 anchor="w", wraplength=WIDGET_W - 40, justify="left").pack(fill="x")
        self._money_box = tk.Frame(parent, bg=C_BG)
        self._money_box.pack(fill="x")

    @staticmethod
    def money_forms(won):
        """원 단위 정수 → 여러 표기"""
        out = ["{:,}원".format(won)]
        if won >= 1000:
            out.append("{:,}천원".format(round(won / 1000)))
        if won >= 1000000:
            out.append("{:,}백만원".format(round(won / 1000000)))
        if won >= 10000:
            eok, rest = divmod(won, 100000000)
            man, wn = divmod(rest, 10000)
            parts = []
            if eok:
                parts.append("{:,}억".format(eok))
            if man:
                parts.append("{:,}만".format(man))
            if wn:
                parts.append("{:,}".format(wn))
            out.append(" ".join(parts) + "원")
        if won >= 100000000:
            out.append("{:.2f}억원".format(won / 100000000))
        return out

    def refresh_money(self):
        for w in self._money_box.winfo_children():
            w.destroy()
        raw = "".join(ch for ch in self._money.get() if ch.isdigit())
        if not raw:
            return
        for txt in self.money_forms(int(raw)):
            Btn(self._money_box, txt, lambda t=txt: self.put_text(t), kind="flat", h=32, icon_name="calc").pack(fill="x", pady=2)

    def put_text(self, text):
        if not self._need_hwp("문구"):
            return
        try:
            self.bridge.insert_text(text)
            self.say("넣었어요: %s" % text.strip())
        except B.BridgeError as e:
            self.say(str(e), ok=False)

    def put_table(self):
        if not self._need_hwp("표"):
            return
        try:
            r, c = int(self._tb_rows.get()), int(self._tb_cols.get())
        except (ValueError, tk.TclError):
            self.say("줄·칸은 숫자로 넣어 주세요", ok=False)
            return
        self.ui["tb_rows"], self.ui["tb_cols"] = r, c
        try:
            self.bridge.insert_table(r, c)
            self.say("%d줄 × %d칸 표를 넣었어요" % (r, c))
        except B.BridgeError as e:
            self.say(str(e), ok=False)

    def put_pagebreak(self):
        if not self._need_hwp("쪽 나누기"):
            return
        try:
            self.bridge.break_page()
            self.say("쪽을 나눴어요")
        except B.BridgeError as e:
            self.say(str(e), ok=False)

    # ---------------------------------------------------------------- 겹치기 탭
    def build_compose(self, parent):
        self.section(parent, "글자 겹치기 모양")
        for i, (name, _t, _n) in enumerate(CMP.SHAPES):
            tk.Radiobutton(parent, text=name, variable=self.shape, value=i, bg=C_BG, font=FONT, anchor="w", fg=C_TEXT,
                           activebackground=C_BG, selectcolor=C_CARD, highlightthickness=0,
                           command=lambda: self.ui.__setitem__("shape", self.shape.get())).pack(fill="x", pady=1)
        self.section(parent, "숫자")
        grid = tk.Frame(parent, bg=C_BG)
        grid.pack(anchor="w")
        for n in range(1, 21):
            Chip(grid, str(n), lambda t=str(n): self.put_compose(t), size=40, font=FONT_B).grid(
                row=(n - 1) // 5, column=(n - 1) % 5, padx=2, pady=2)
        self.section(parent, "다른 글자")
        row = tk.Frame(parent, bg=C_BG)
        row.pack(fill="x")
        e = tk.Entry(row, textvariable=self.cmp_text, width=7, font=FONT, relief="flat", highlightthickness=1,
                     highlightbackground=C_LINE, highlightcolor=C_ACC, bg="white")
        e.pack(side="left", ipady=6)
        e.bind("<Return>", lambda ev: self.put_compose(self.cmp_text.get()))
        Btn(row, "넣기", lambda: self.put_compose(self.cmp_text.get()), kind="accent", anchor="c", w=84, h=34).pack(
            side="left", padx=6)

    # ---------------------------------------------------------------- 더보기 탭
    def build_more(self, parent):
        self.section(parent, "상세 입력")
        Btn(parent, "상세 입력 창", self.open_detail, icon_name="sliders", tip="칸을 직접 채워 만들기·본문 입력").pack(fill="x", pady=4)
        Btn(parent, "새 양식 추가 (한글 파일로)", lambda: self.open_detail("forms"), icon_name="plus").pack(fill="x", pady=4)
        self.section(parent, "설정")
        Btn(parent, "내 정보 (과·담당자)", self.open_profile, icon_name="user").pack(fill="x", pady=4)
        Btn(parent, "한글에 다시 연결", self.connect, kind="flat", icon_name="link").pack(fill="x", pady=4)
        Btn(parent, "프로그램 완전 종료", self.quit_app, kind="flat", icon_name="tray",
            tip="창을 닫으면 트레이로 가요. 아예 끄려면 여기 또는 트레이 아이콘 메뉴의 '종료'").pack(fill="x", pady=4)
        self.section(parent, "넣는 방식")
        self._mode_var = tk.StringVar(value=self.ui.get("insert_mode", "step"))
        for val, txt in (("step", "차례대로 입력 (문서 끝에서, 기본)"), ("whole", "한 번에 넣기")):
            tk.Radiobutton(parent, text=txt, variable=self._mode_var, value=val, bg=C_BG, font=FONT, anchor="w",
                           fg=C_TEXT, activebackground=C_BG, selectcolor=C_CARD, highlightthickness=0,
                           command=lambda: self.ui.__setitem__("insert_mode", self._mode_var.get())).pack(fill="x")
        self.section(parent, "창 투명도")
        row = tk.Frame(parent, bg=C_BG)
        row.pack(fill="x")
        sc = tk.Scale(row, from_=50, to=100, orient="horizontal", showvalue=True, command=self.set_alpha, bg=C_BG,
                      troughcolor=C_LINE, highlightthickness=0, bd=0, fg=C_SUB, font=FONT_S, length=WIDGET_W - 40,
                      activebackground=C_ACC)
        sc.set(int(self.alpha * 100))
        sc.pack(fill="x")
        self._sharp_var = tk.IntVar(value=1 if self.sharp_hover else 0)
        tk.Checkbutton(parent, text="마우스를 올리면 선명하게", variable=self._sharp_var, command=self.set_sharp, bg=C_BG,
                       font=FONT_S, fg=C_TEXT, activebackground=C_BG, selectcolor=C_CARD, highlightthickness=0,
                       anchor="w").pack(fill="x")
        tk.Label(parent, justify="left", anchor="w", bg=C_BG, fg=C_SUB, font=FONT_S, wraplength=WIDGET_W - 36, text=(
            "\n· 양식을 누르면 한글에서 커서가 있는 곳에 들어가요\n· 한글을 먼저 열어 두세요\n"
            "· 모양이 어긋나면 한글에서 Ctrl+Z 로 되돌릴 수 있어요")).pack(fill="x", pady=8)

    # ---------------------------------------------------------------- 동작
    def make_report(self, fid):
        try:
            f = registry.load_form(fid)
            prof = PR.load_profile()
            vals = {}
            for fl in f.fields:
                v = ""
                d = fl.get("default")
                if d in ("today", "target_monday"):
                    v = self.default_value(d)
                elif d:
                    v = d
                if fl.get("profile") and prof.get("autofill", True):
                    v = PR.profile_value(prof, fl["profile"]) or v
                vals[fl["key"]] = v
            plain = f.meta.get("style", {}).get("body_mode") == "plain"      # 보도자료처럼 본문을 직접 쓰는 양식은 틀만
            outline = "" if (plain or not f.meta.get("has_body", True)) else (f.meta.get("starter") or T.DEFAULT_STARTER)
            replace = None
            if self.ui.get("convert") and outline and self._need_hwp("서식 바꾸기"):
                sel = self.bridge.get_selection_text()
                src = sel if sel.strip() else self.bridge.get_all_text()
                if src.strip():
                    what = "선택한 글" if sel.strip() else "문서 전체"
                    if not messagebox.askyesno("서식 바꾸기", "%s을(를) '%s' 양식으로 바꿔 넣을까요?\n"
                                               "(글이 □ ◦ ▸ - 단계로 정리돼요. 한글에서 Ctrl+Z 로 되돌릴 수 있어요)" % (
                                                   what, f.meta["name"].split(" (")[0])):
                        return
                    outline = T.normalize_outline(src)
                    replace = "selection" if sel.strip() else "all"
            pkg = f.render(vals, outline)
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        PR.push_recent(self.ui, "recent_forms", fid, 5)
        self.busy(lambda: self.out.insert(pkg, f.id, replace))

    def make_reference(self):
        """시장님 보고(1장) 뒤에 새 쪽 + 참고자료 제목을 붙인다"""
        try:
            pkg = registry.load_form("report_mayor").render_fragment("---\n□ 참고자료\n◦ ")
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        self.busy(lambda: self.out.insert(pkg, "참고자료"))

    def make_week(self, which):
        monday = self.target_monday()
        data = PR.load_week(monday)
        org = PR.load_profile().get("org") or "문화교육국"
        try:
            if which == "cover":
                pkg = registry.load_form("weekly_cover").render({"start": monday.isoformat()})
            elif which == "plan":
                prof = PR.load_profile()
                items = data["items"] or [{"title": "(행사·업무명)", "dept": prof.get("dept", ""), "staff": prof.get("staff_name", ""),
                                           "phone": prof.get("staff_ext", ""),
                                           "lines": [("일시", ""), ("장소", ""), ("주최", ""), ("내용", "")]}]
                pkg = registry.load_form("weekly_plan").render({"org": org, "items": items})
            else:
                end = monday + datetime.timedelta(days=6)
                evs = [e for e in weekly.extract_events(data["items"]) if monday <= T.parse_date(e["date"]) <= end]
                pkg = registry.load_form("weekly_schedule").render({"org": org, "start": monday.isoformat(), "events": evs})
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        self.busy(lambda: self.out.insert(pkg, {"cover": "주간업무계획표지", "plan": "주간업무계획"}.get(which, "주간주요일정")))

    def busy(self, fn):
        self.config(cursor="watch")
        self.update_idletasks()
        try:
            fn()
        finally:
            self.config(cursor="")

    def _need_hwp(self, title):
        br = self.bridge
        if not br.connected:
            try:
                br.connect(launch=False)
            except Exception:                            # noqa: BLE001
                pass
        if not br.connected:
            self.say("한글에 연결되지 않았어요. 한글을 먼저 열어 주세요", ok=False)
            self.update_conn()
            return False
        return True

    def put_symbol(self, ch, tip=""):
        if not self._need_hwp("기호"):
            return
        try:
            self.bridge.insert_text(ch)
            PR.push_recent(self.ui, "recent_syms", ch, 12)
            self.say("%s 넣음%s" % (ch, " (%s)" % tip if tip else ""))
        except B.BridgeError as e:
            messagebox.showerror("기호", str(e))

    def put_compose(self, text):
        text = (text or "").strip()
        if not text or not self._need_hwp("글자 겹치기"):
            return
        name, ctype, shape_no = CMP.SHAPES[self.shape.get()]
        try:                                             # 1) 한글의 '글자 겹치기' 명령을 직접 실행
            self.bridge.compose_chars(text, shape_no)
            self.say("%s: %s" % (name, text))
            return
        except B.BridgeError as e1:
            err = str(e1)
        path = None
        try:                                             # 2) 명령이 안 되면 겹침 글자 조각을 끼워 넣기
            pkg = CMP.compose_package(text, ctype, CMP.base_template())
            path = pkg.save(tmp_hwpx("compose"))
            self.bridge.insert_file(path)
            self.say("%s: %s (조각)" % (name, text))
        except Exception as e2:                          # noqa: BLE001
            messagebox.showerror("글자 겹치기", "%s\n%s" % (err, e2))
        finally:
            if path and os.path.exists(path):
                os.remove(path)

    def open_detail(self, tab=None):
        if self.detail is None:
            DetailWindow(self)
        self.detail.lift()
        if tab:
            self.detail.nb.select({"weekly": 1, "forms": 2}.get(tab, 0))

    def open_profile(self):
        d = ProfileDialog(self)
        self.wait_window(d)
        self.rebuild()

    # ---------------------------------------------------------------- 창·연결
    def toggle(self):
        self.collapsed = not self.collapsed
        if self.collapsed:
            self._h = self.winfo_height()
            for w in (self.tabbar, self.body, self.chip):
                w.pack_forget()
            self.geometry("%dx%d" % (WIDGET_W, 100))
        else:
            self.chip.pack(fill="x", after=self.head)
            self.tabbar.pack(fill="x", padx=8, after=self.chip)
            self.body.pack(fill="both", expand=True, padx=10, pady=6)
            self.geometry("%dx%d" % (WIDGET_W, getattr(self, "_h", 700)))

    # ---------------------------------------------------------------- 트레이
    def _tray_image(self):
        from PIL import Image
        im = Image.new("RGBA", (64, 64))
        a, b = (0x5e, 0x6a, 0xd2), (0x9a, 0x6b, 0xd6)
        px = im.load()
        for x in range(64):
            for y in range(64):
                t = (x + y) / 126
                px[x, y] = tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)
        mask = Image.new("L", (64, 64), 0)
        from PIL import ImageDraw
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, 63, 63), 14, fill=255)
        im.putalpha(mask)
        try:
            ic = Image.open(os.path.join(ICON_DIR, "doc_w.png")).convert("RGBA").resize((40, 40), Image.LANCZOS)
            im.alpha_composite(ic, (12, 12))
        except Exception:                                # noqa: BLE001
            pass
        return im

    def to_tray(self):
        """창을 숨기고 알림 영역(트레이)에 아이콘만 남긴다. 아이콘을 누르면 다시 나타남."""
        if getattr(self, "_tray", None) is not None:
            return True
        try:
            import pystray
            import queue
            self._tray_q = queue.Queue()
            q = self._tray_q
            menu = pystray.Menu(
                pystray.MenuItem("열기", lambda icon, item: q.put("show"), default=True),
                pystray.MenuItem("종료", lambda icon, item: q.put("quit")))
            self._tray = pystray.Icon("hwpforms", self._tray_image(), "양식 도우미", menu)
            self._tray.run_detached()
        except Exception:                                # noqa: BLE001
            self._tray = None
            return False
        self.withdraw()
        self.after(250, self._poll_tray)
        try:                                             # 처음 숨길 때 한 번 알려 준다(트레이 아이콘 말풍선)
            if not self.ui.get("tray_told"):
                self._tray.notify("창을 닫아도 계속 실행돼요. 이 아이콘을 누르면 다시 열려요.", "양식 도우미")
                self.ui["tray_told"] = True
        except Exception:                                # noqa: BLE001
            pass
        return True

    def _poll_tray(self):
        q = getattr(self, "_tray_q", None)
        if q is None:
            return
        try:
            while True:
                cmd = q.get_nowait()
                if cmd == "show":
                    self.from_tray()
                    return
                if cmd == "quit":
                    self.from_tray()
                    self.quit_app()
                    return
        except Exception:                                # noqa: BLE001
            pass
        self.after(250, self._poll_tray)

    def from_tray(self):
        t, self._tray = getattr(self, "_tray", None), None
        self._tray_q = None
        if t is not None:
            try:
                t.stop()
            except Exception:                            # noqa: BLE001
                pass
        self.deiconify()
        self.attributes("-topmost", True)
        self.lift()

    def auto_connect(self):
        if B.IS_WIN:
            try:
                self.bridge.connect(launch=False)
            except Exception:                            # noqa: BLE001
                pass
            self.dock()
        self.update_conn()

    def update_conn(self):
        ok = self.bridge.connected
        self.chip.config(text=("●  한글에 연결됨" if ok else "●  한글 연결 안 됨 — 눌러서 연결"),
                         fg=C_OK if ok else C_BAD)

    def connect(self):
        try:
            self.bridge.connect()
            self.say("한글에 연결했어요")
        except B.BridgeError as e:
            self.say(str(e), ok=False)
        self.update_conn()

    def tick(self):
        if B.IS_WIN and not self.bridge.connected:
            try:
                self.bridge.connect(launch=False)
            except Exception:                            # noqa: BLE001
                pass
        self.update_conn()
        self.after(3000, self.tick)

    def dock(self):
        if not B.IS_WIN:
            self.say("한글 옆 붙이기는 Windows 에서만 돼요", ok=False)
            return
        x, y, w, h = B.dock_beside(WIDGET_W + 6)
        self.geometry("%dx%d+%d+%d" % (WIDGET_W, h, x, y))
        self.docked = True

    def attach_single(self, single):
        """다른 실행이 '열어 줘' 신호를 보내면 (트레이에 있어도) 창을 앞으로 가져온다"""
        self._single = single

        def poll():
            try:
                while True:
                    if single.q.get_nowait() == "show":
                        self.bring_front()
            except Exception:                              # noqa: BLE001
                pass
            self.after(300, poll)
        poll()

    def bring_front(self):
        if getattr(self, "_tray", None) is not None:
            self.from_tray()
        self.deiconify()
        self.attributes("-topmost", True)
        self.lift()

    def on_close(self):
        """창 닫기(X) = 트레이로 보내기. 트레이를 쓸 수 없으면 그냥 종료한다. 완전 종료는 트레이 메뉴 '종료' 또는 더보기."""
        if not self.to_tray():
            self.quit_app()

    def quit_app(self):
        if getattr(self, "_single", None) is not None:
            self._single.close()
        if getattr(self, "_tray", None) is not None:
            try:
                self._tray.stop()
            except Exception:                            # noqa: BLE001
                pass
        self.ui["x"], self.ui["y"] = self.winfo_x(), self.winfo_y()
        self.ui["shape"] = self.shape.get()
        PR.save_ui(self.ui)
        self.destroy()


App = Widget


def run():
    from .single import SingleInstance
    single = SingleInstance()
    if not single.acquire():               # 이미 실행 중이면 그 창을 불러오고 이 실행은 끝낸다
        return
    w = Widget()
    w.attach_single(single)
    w.mainloop()
