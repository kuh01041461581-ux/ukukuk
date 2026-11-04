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

    def insert(self, pkg, prefix):
        path = pkg.save(tmp_hwpx(prefix))
        br = self.app.bridge
        if br.connected:
            try:
                br.insert_file(path)
                self.app.say("한글 커서 위치에 넣었어요")
                return
            except B.BridgeError as e:
                if not messagebox.askyesno("한글에 넣기", "%s\n\n새 문서로 열까요?" % e):
                    return
        self.open_new(pkg, prefix, path)

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
            if f.get("default") == "today":
                val = datetime.date.today().isoformat()
            if f.get("profile"):
                val = PR.profile_value(prof, f["profile"]) or val
            v.set(val)
            self.vars[f["key"]] = v
            ttk.Entry(self.fields, textvariable=v, width=34).grid(row=i, column=1, sticky="ew", padx=4, pady=1)
        self.fields.columnconfigure(1, weight=1)
        self.body.delete("1.0", "end")
        self.body.insert("1.0", self.form.meta.get("starter", "□ 개요\n◦ 일시: \n◦ 장소: \n"))

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
            pkg = self.form.render(self.values(), self.body.get("1.0", "end"))
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
        self.monday = T.week_monday(datetime.date.today())
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
        path = filedialog.askopenfilename(title="양식으로 쓸 한글 파일", filetypes=[("한글 문서", "*.hwpx")])
        if not path:
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
        ttk.Button(self, text="저장", command=self.ok).grid(row=len(PR.PROFILE_FIELDS), column=0, columnspan=2, pady=8)

    def ok(self):
        PR.save_profile({k: v.get().strip() for k, v in self.v.items()})
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
WIDGET_W = 230


class Widget(tk.Tk):
    """한글 옆에 떠 있는 양식 단추 모음. 단추를 누르면 한글에 바로 만들어 넣는다."""

    def __init__(self, bridge=None):
        super().__init__()
        self.title("양식")
        self.bridge = bridge or B.HwpBridge()
        self.out = Output(self)
        self.detail = None
        self.attributes("-topmost", True)
        self.resizable(False, True)
        self.geometry("%dx%d+%d+%d" % (WIDGET_W, 640, max(0, self.winfo_screenwidth() - WIDGET_W - 10), 60))
        try:
            ttk.Style().theme_use("vista" if sys.platform == "win32" else "clam")
        except tk.TclError:
            pass
        st = ttk.Style()
        st.configure("Form.TButton", font=FONT, padding=(8, 7), anchor="w")
        st.configure("Wk.TButton", font=FONT_B, padding=(8, 7), anchor="w")
        top = ttk.Frame(self, padding=(6, 4))
        top.pack(fill="x")
        self.conn = ttk.Label(top, text="●", foreground="#a33")
        self.conn.pack(side="left")
        self.conn_txt = ttk.Label(top, text="한글 연결 안 됨", font=FONT_S)
        self.conn_txt.pack(side="left", padx=3)
        ttk.Button(top, text="붙이기", width=6, command=self.dock).pack(side="right")
        self.body = ttk.Frame(self, padding=(6, 0))
        self.body.pack(fill="both", expand=True)
        self.status = ttk.Label(self, text="양식을 클릭하면 한글에 만들어져요", font=FONT_S, wraplength=WIDGET_W - 12,
                                relief="sunken", anchor="w")
        self.status.pack(fill="x", side="bottom")
        self.rebuild()
        self.after(300, self.auto_connect)
        self.after(1500, self.tick)

    def say(self, msg):
        self.status.config(text=msg)

    def rebuild(self):
        for w in self.body.winfo_children():
            w.destroy()
        groups = {}
        for fid, folder, meta in registry.list_forms():
            if meta["kind"] == "report":
                groups.setdefault(meta.get("audience", "기타"), []).append((fid, meta))
        for g, lst in groups.items():
            ttk.Label(self.body, text=g, font=FONT_B, foreground=ACCENT).pack(anchor="w", pady=(8, 2))
            for fid, meta in lst:
                ttk.Button(self.body, text=meta["name"].split(" (")[0], style="Form.TButton",
                           command=lambda f=fid: self.make_report(f)).pack(fill="x", pady=1)
        ttk.Label(self.body, text="주간 업무 (이번 주)", font=FONT_B, foreground=ACCENT).pack(anchor="w", pady=(10, 2))
        ttk.Button(self.body, text="주간업무계획", style="Wk.TButton", command=lambda: self.make_week("plan")).pack(fill="x", pady=1)
        ttk.Button(self.body, text="주간 주요일정", style="Wk.TButton", command=lambda: self.make_week("schedule")).pack(fill="x", pady=1)
        ttk.Separator(self.body).pack(fill="x", pady=10)
        ttk.Button(self.body, text="상세 입력 · 양식 추가…", command=self.open_detail).pack(fill="x")

    # ---- 한 번 클릭으로 만들기
    def make_report(self, fid):
        try:
            f = registry.load_form(fid)
            prof = PR.load_profile()
            vals = {}
            for fl in f.fields:
                v = ""
                if fl.get("default") == "today":
                    v = datetime.date.today().isoformat()
                if fl.get("profile"):
                    v = PR.profile_value(prof, fl["profile"]) or v
                vals[fl["key"]] = v
            pkg = f.render(vals, f.meta.get("starter", "□ 개요\n◦ 일시: \n◦ 장소: \n"))
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        self.out.insert(pkg, f.id)

    def make_week(self, which):
        monday = T.week_monday(datetime.date.today())
        data = PR.load_week(monday)
        org = PR.load_profile().get("org") or "문화교육국"
        try:
            if which == "plan":
                pkg = registry.load_form("weekly_plan").render({"org": org, "items": data["items"]})
            else:
                end = monday + datetime.timedelta(days=6)
                evs = [e for e in weekly.extract_events(data["items"]) if monday <= T.parse_date(e["date"]) <= end]
                pkg = registry.load_form("weekly_schedule").render({"org": org, "start": monday.isoformat(), "events": evs})
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        self.out.insert(pkg, "주간업무계획" if which == "plan" else "주간주요일정")

    def open_detail(self):
        if self.detail is not None:
            self.detail.lift()
        else:
            DetailWindow(self)

    # ---- 한글 연결·배치
    def auto_connect(self):
        if B.IS_WIN:
            try:
                self.bridge.connect(launch=False)
            except Exception:                            # noqa: BLE001
                pass
        self.update_conn()
        if B.IS_WIN:
            self.dock()

    def update_conn(self):
        ok = self.bridge.connected
        self.conn.config(foreground="#1a7f37" if ok else "#a33")
        self.conn_txt.config(text="한글 연결됨" if ok else "한글 연결 안 됨 (누르면 연결)")
        self.conn_txt.bind("<Button-1>", lambda e: self.connect() if not self.bridge.connected else None)

    def connect(self):
        try:
            self.bridge.connect()
        except B.BridgeError as e:
            messagebox.showwarning("한글 연결", str(e))
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
            return
        x, y, w, h = B.dock_beside(WIDGET_W + 6)
        self.geometry("%dx%d+%d+%d" % (WIDGET_W, h, x, y))


App = Widget


def run():
    Widget().mainloop()
