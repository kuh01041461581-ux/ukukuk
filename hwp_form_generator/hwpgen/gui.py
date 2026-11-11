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


def write_error_log(what, exc):
    """오류가 나면 자세한 내용을 %APPDATA%\\HwpFormGenerator\\error.log 에 남긴다(어느 양식·몇 번째 조각에서 어떤 오류인지)"""
    import traceback
    try:
        d = os.path.join(os.environ.get("APPDATA") or os.path.expanduser("~"), "HwpFormGenerator")
        os.makedirs(d, exist_ok=True)
        p = os.path.join(d, "error.log")
        with open(p, "a", encoding="utf-8") as f:
            f.write("[%s] %s\n%s\n" % (datetime.datetime.now().isoformat(timespec="seconds"), what,
                                      "".join(traceback.format_exception(type(exc), exc, exc.__traceback__))))
        return p
    except Exception:                                    # noqa: BLE001
        return ""


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
        i = 0
        try:
            for i, ch in enumerate(chunks, 1):
                if step:
                    br.move_doc_end()                # 매번 문서 끝에 붙인다(앞 조각이 남긴 받침 문단에 합쳐짐)
                self._insert_chunk(ch, prefix, br, keep_section and i == 1)
                if n > 1:
                    app.say("입력 중… %d/%d" % (i, n))
                    app.update()
                    time.sleep(app.ui.get("insert_delay", min(0.45, max(0.12, 3.0 / n))))
            app.say("열려 있는 문서의 커서 위치에 넣었어요")
        except Exception as e:                       # noqa: BLE001
            log = write_error_log("insert %s chunk %d/%d step=%s" % (prefix, i, n, step), e)
            if messagebox.askyesno("한글에 넣기", "%s\n\n(%s)\n\n이 양식을 새 문서로 열어 드릴까요?\n오류 기록: %s" % (
                    e, prefix, log)):
                try:
                    self.open_new(pkg, prefix)
                except Exception as e2:              # noqa: BLE001
                    write_error_log("open_new %s" % prefix, e2)
                    messagebox.showerror("한글에서 열기", str(e2))

    def _insert_chunk(self, ch, prefix, br, keep_section):
        path = ch.save(tmp_hwpx(prefix))
        try:
            br.insert_file(path, keep_section=keep_section)
        finally:
            try:
                os.remove(path)
            except OSError:
                pass

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
# 팔레트: Linear(절제된 인디고) + Notion(차분한 회색 글자) + 윈도우 11 유리(옅은 반투명 흰색)
C_BG = "#f2f3fb"
C_G1 = "#4a5bd8"          # 포인트 색(인디고) 그라데이션: 진한 쪽
C_G2 = "#8b9cf5"          # 같은 색 계열의 밝은 쪽
C_CARD = "#ffffff"
C_LINE = "#e3e5ea"
C_HOVER = "#eef0f5"
C_PRESS = "#e1e4ec"
C_TEXT = "#1f2329"
C_SUB = "#7a808c"
C_ACC = "#4a5bd8"
C_OK = "#2a9461"
C_BAD = "#c94848"
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


_AA = {}


def aa_shape(w, h, r, fill, outline=None, bg=None, grad=None, vfill=None):
    """안티앨리어싱된 둥근 도형 이미지(Pillow 로 3배 크기로 그려 줄임). Pillow 가 없으면 None → 호출한 쪽이 기본 도형으로 그림.
    grad=(색1, 색2): 왼쪽 위→오른쪽 아래 그라데이션으로 채움."""
    key = (w, h, r, fill, outline, bg, grad, vfill)
    if key in _AA:
        return _AA[key]
    img = None
    try:
        import base64
        import io
        from PIL import Image, ImageDraw
        k = 3
        W, H = max(2, int(w)) * k, max(2, int(h)) * k
        bgc = bg or C_BG
        base = Image.new("RGB", (W, H), bgc)
        mask = Image.new("L", (W, H), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, W - 1, H - 1), radius=r * k, fill=255)
        if grad:
            c1 = tuple(int(grad[0][i:i + 2], 16) for i in (1, 3, 5))
            c2 = tuple(int(grad[1][i:i + 2], 16) for i in (1, 3, 5))
            gi = Image.new("RGB", (W, H))
            px = gi.load()
            den = float(W + H)
            for y in range(H):
                for x in range(W):
                    t = (x + y) / den
                    px[x, y] = (int(c1[0] + (c2[0] - c1[0]) * t), int(c1[1] + (c2[1] - c1[1]) * t), int(c1[2] + (c2[2] - c1[2]) * t))
            base.paste(gi, (0, 0), mask)
        else:
            if vfill:                                        # 위→아래로 아주 살짝 어두워지는 유리 카드 느낌
                v1 = tuple(int(vfill[0][i:i + 2], 16) for i in (1, 3, 5))
                v2 = tuple(int(vfill[1][i:i + 2], 16) for i in (1, 3, 5))
                vi = Image.new("RGB", (W, H))
                vd = ImageDraw.Draw(vi)
                for y in range(H):
                    tt = y / max(1, H - 1)
                    vd.line((0, y, W, y), fill=tuple(int(v1[i] + (v2[i] - v1[i]) * tt) for i in range(3)))
                base.paste(vi, (0, 0), mask)
            else:
                base.paste(Image.new("RGB", (W, H), fill), (0, 0), mask)
            if outline:
                inner = Image.new("L", (W, H), 0)
                ImageDraw.Draw(inner).rounded_rectangle((k, k, W - 1 - k, H - 1 - k), radius=max(1, r * k - k), fill=255)
                ring = Image.new("L", (W, H), 0)
                ring.paste(mask, (0, 0))
                ring.paste(0, (0, 0), inner)
                base.paste(Image.new("RGB", (W, H), outline), (0, 0), ring)
        base = base.resize((int(w), int(h)), Image.LANCZOS)
        bio = io.BytesIO()
        base.save(bio, "PNG")
        img = tk.PhotoImage(data=base64.b64encode(bio.getvalue()))
    except Exception:                                    # noqa: BLE001
        img = None
    if len(_AA) > 400:
        _AA.clear()
    _AA[key] = img
    return img


def liquid_img(w, h, r, base=(235, 160), tint=None, t=0.0, pressed=False, pad=5, hover_rim=True):
    """리퀴드 글래스 알약(투명 PNG): 위가 밝은 반투명 몸체 + 모서리를 따라 도는 빛 테두리(왼쪽 위는 밝고 오른쪽 아래는 옅게) +
    가장자리 안쪽 번짐 + 윗면 반사광 + 아래쪽 두께감 + 색 번진 그림자. t=올림 정도, pressed=눌림, tint=(색1,색2) 면 색 입힌 유리."""
    key = ("liq", w, h, r, base, tint, _q(t), pressed, pad, hover_rim)
    if key in _AA:
        return _AA[key]
    im = None
    try:
        from PIL import Image, ImageChops, ImageDraw, ImageFilter
        k = 3
        W3, H3 = (w + 2 * pad) * k, (h + 2 * pad) * k
        x0, y0, x1, y1 = pad * k, pad * k, (pad + w) * k - 1, (pad + h) * k - 1
        rad = min(r * k, (y1 - y0) // 2)
        t = _q(t)
        lift = 0 if pressed else int(2 * t * k)
        sy = (3 if not pressed else 1) * k                            # 그림자 아래로 처지는 정도(누르면 납작)
        # 그림자(색 번진)
        sh = Image.new("RGBA", (W3, H3), (0, 0, 0, 0))
        sa = 14 if pressed else int(40 + 46 * t)
        scol = (70, 84, 190) if not tint else (60, 70, 210)
        ImageDraw.Draw(sh).rounded_rectangle((x0 + 2 * k, y0 + sy - lift, x1 - 2 * k, y1 + sy - lift), radius=rad, fill=scol + (sa,))
        sh = sh.filter(ImageFilter.GaussianBlur(3.2 * k))
        # 몸체 마스크
        M = Image.new("L", (W3, H3), 0)
        ImageDraw.Draw(M).rounded_rectangle((x0, y0 - lift, x1, y1 - lift), radius=rad, fill=255)
        top, bot = base
        if pressed:
            top, bot = min(255, top), min(255, bot + 30)
        top = min(255, int(top + 18 * t))
        bot = min(255, int(bot + 40 * t))
        body = Image.new("RGBA", (W3, H3), (0, 0, 0, 0))
        bd = ImageDraw.Draw(body)
        c1 = tuple(int(tint[0][i:i + 2], 16) for i in (1, 3, 5)) if tint else (255, 255, 255)
        c2 = tuple(int(tint[1][i:i + 2], 16) for i in (1, 3, 5)) if tint else (246, 248, 255)
        span = max(1, y1 - y0)
        for yy in range(y0 - lift, y1 - lift + 1):
            f = (yy - (y0 - lift)) / float(span)
            col = tuple(round(c1[i] + (c2[i] - c1[i]) * f) for i in range(3)) + (round(top + (bot - top) * f),)
            bd.line((x0, yy, x1, yy), fill=col)
        if pressed and not tint:
            body.alpha_composite(Image.new("RGBA", (W3, H3), (196, 204, 246, 70)))
        body.putalpha(ImageChops.multiply(body.getchannel("A"), M))
        # 가장자리 안쪽 번짐(유리 두께에서 빛이 모이는 느낌)
        blur = M.filter(ImageFilter.GaussianBlur(2.6 * k))
        inner = ImageChops.subtract(M, blur).point(lambda v: min(255, v * 2))
        glow = Image.new("RGBA", (W3, H3), (255, 255, 255, 0))
        glow.putalpha(inner.point(lambda v: int(v * (0.55 if not tint else 0.42))))
        # 아래쪽 안쪽 어두운 띠(두께감)
        up = ImageChops.offset(M, 0, -2 * k)
        band = ImageChops.subtract(M, up).filter(ImageFilter.GaussianBlur(0.8 * k))
        dark = Image.new("RGBA", (W3, H3), (60, 70, 160, 0))
        dark.putalpha(band.point(lambda v: int(v * (0.22 if not tint else 0.30))))
        # 윗면 반사광
        sp = Image.new("L", (W3, H3), 0)
        ImageDraw.Draw(sp).rounded_rectangle((x0 + (x1 - x0) * 0.10, y0 - lift + 2 * k, x1 - (x1 - x0) * 0.10, y0 - lift + (y1 - y0) * 0.46),
                                             radius=max(2, rad // 2), fill=int((78 if not tint else 92) * (0.5 if pressed else 1)))
        sp = ImageChops.multiply(sp.filter(ImageFilter.GaussianBlur(1.6 * k)), M)
        spec = Image.new("RGBA", (W3, H3), (255, 255, 255, 0))
        spec.putalpha(sp)
        # 빛 테두리: 왼쪽 위는 밝고 오른쪽 아래는 옅은 그라데이션 (+ 올리면 인디고 기운)
        ring = ImageChops.subtract(M, M.filter(ImageFilter.MinFilter(2 * k + 1)))
        rg = Image.new("RGBA", (W3, H3), (255, 255, 255, 0))
        rgd = ImageDraw.Draw(rg)
        for i in range(W3 + H3):
            f = i / float(W3 + H3)
            a = int(250 - 190 * f) if not pressed else int(150 - 80 * f)
            rgd.line((i, 0, 0, i), fill=(255, 255, 255, max(0, a)))
        rg.putalpha(ImageChops.multiply(rg.getchannel("A"), ring))
        out = Image.alpha_composite(sh, body)
        out = Image.alpha_composite(out, glow)
        out = Image.alpha_composite(out, dark)
        out = Image.alpha_composite(out, spec)
        out = Image.alpha_composite(out, rg)
        if hover_rim and t and not tint:                              # 올리면 테두리에 인디고 기운이 은은히
            tr = Image.new("RGBA", (W3, H3), (100, 114, 235, 0))
            tr.putalpha(ring.point(lambda v: int(v * 0.42 * t)))
            out = Image.alpha_composite(out, tr)
        out = out.resize((w + 2 * pad, h + 2 * pad), Image.LANCZOS)
        im = _pil_photo(out)
    except Exception:                                    # noqa: BLE001
        im = None
    if len(_AA) > 600:
        _AA.clear()
    _AA[key] = im
    return im


def _q(t):
    """애니메이션 값을 7단계로 끊어 이미지 캐시가 늘어나지 않게"""
    return round(t * 6) / 6.0


def vgradient_image(w, h, c1, c2):
    img = tk.PhotoImage(width=w, height=h)
    for y in range(h):
        img.put(_mix(c1, c2, y / max(1, h - 1)), to=(0, y, w, y + 1))
    return img


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
        t, acc = _q(self.t), self.kind == "accent"
        dy = 2 if self.pressed else -int(round(1.5 * t))      # 올리면 살짝 떠오르고 누르면 눌림 — 아이콘·글자가 함께
        pad = 5
        r = (h - 2 * pad) // 2
        if acc:
            im = liquid_img(w - 2 * pad, h - 2 * pad, r, base=(238, 214), tint=(C_G1, C_G2), t=t, pressed=self.pressed, pad=pad)
            fg = "white"
        elif self.kind == "flat" and t == 0 and not self.pressed:
            im = None
            fg = C_TEXT
        else:
            im = liquid_img(w - 2 * pad, h - 2 * pad, r, base=(238 if self.kind == "card" else 120, 168 if self.kind == "card" else 40),
                            t=t, pressed=self.pressed, pad=pad)
            fg = _mix(C_TEXT, "#2c3366", t)
        if im is not None:
            self.create_image(0, 0, image=im, anchor="nw")
        ic = icon(self.icon_name, white=acc) if self.icon_name else None
        x = 10
        if self.anchor == "w":
            if ic is not None:
                d = h - 14
                cy = h // 2 + dy
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
        t = _q(t)
        fill = C_PRESS if self.pressed else _mix(C_CARD, C_HOVER, t)
        im = aa_shape(s - 2, s - 2, 10, fill, outline=_mix(C_LINE, C_ACC, t))
        if im is not None:
            self.create_image(1, 1, image=im, anchor="nw")
        else:
            round_rect(self, 1, 1, s - 2, s - 2, 10, fill=fill, outline=_mix(C_LINE, C_ACC, t))
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


def grad_round(cv, x0, y0, x1, y1, r, c1, c2):
    """둥근 모서리 안을 c1→c2 로 비스듬히(왼쪽 위→오른쪽 아래) 채우는 그라데이션"""
    w, h = x1 - x0, y1 - y0
    for x in range(int(w)):
        edge = max(0.0, r - min(x, w - 1 - x))
        inset = r - (r * r - edge * edge) ** 0.5 if edge > 0 else 0
        col = _mix(c1, c2, x / max(1, w - 1))
        cv.create_line(x0 + x, y0 + inset, x0 + x, y1 - inset, fill=col)


CAP = "#eef0fc"           # 제목줄·머리 맨 위 색(윈도우 제목줄 색과 같게 맞춰 창이 끊김 없이 이어지게)


def make_aurora(w, h):
    """은은한 오로라(인디고·바이올렛·하늘색 번짐) 배경 — 유리 위에 비치는 색 느낌. 맨 위는 CAP, 맨 아래는 C_BG 로 자연스럽게 이어짐"""
    from PIL import Image, ImageDraw, ImageFilter
    top = tuple(int(CAP[i:i + 2], 16) for i in (1, 3, 5))
    bot = tuple(int(C_BG[i:i + 2], 16) for i in (1, 3, 5))
    base = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(base)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line((0, y, w, y), fill=tuple(round(top[i] + (bot[i] - top[i]) * t) for i in range(3)))
    blobs = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bd = ImageDraw.Draw(blobs)
    for cx, cy, r, col in ((40, 92, 74, (112, 128, 255, 150)), (222, 74, 66, (178, 132, 255, 140)),
                           (140, 152, 88, (110, 205, 255, 125)), (250, 165, 54, (255, 160, 214, 100))):
        bd.ellipse((cx - r, cy - r, cx + r, cy + r), fill=col)
    blobs = blobs.filter(ImageFilter.GaussianBlur(26))
    out = base.convert("RGBA")
    out.alpha_composite(blobs)
    out = out.convert("RGB")
    d = ImageDraw.Draw(out)
    fade = 46                                              # 아래쪽은 본문 바탕색으로 서서히
    for i in range(fade):
        y = h - fade + i
        t = i / float(fade - 1)
        row = [out.getpixel((x, y)) for x in range(w)]
        d.line((0, y, w, y), fill=bot)
        for x in range(w):
            p = row[x]
            out.putpixel((x, y), tuple(round(p[j] + (bot[j] - p[j]) * min(1.0, t * 1.15)) for j in range(3)))
    return out


class _Chip:
    """머리 안 글자(연결 상태) — 예전 Label 처럼 config(text=, fg=) 로 바꿀 수 있게"""

    def __init__(self, cv, item, on_click):
        self.cv, self.item = cv, item
        cv.tag_bind(item, "<Button-1>", lambda e: on_click())

    def config(self, **kw):
        if "text" in kw:
            self.cv.itemconfig(self.item, text=kw["text"])
        if "fg" in kw:
            self.cv.itemconfig(self.item, fill=kw["fg"])

    configure = config


class GlassHeader(tk.Canvas):
    """윈도우 11 · 리니어 · 노션 느낌의 머리: 오로라 배경 위에 유리 알약 타일을 얹고, 제목줄과 한 덩어리로 이어진다.
    타일은 마우스를 올리면 부드럽게 밝아지고, 선택된 타일은 포인트 그라데이션으로 채워진다."""

    FULL_H, MIN_H = 184, 56
    PAD = 8

    def __init__(self, master, tabs, icons, labels, on_pick, buttons, on_chip):
        W = WIDGET_W
        super().__init__(master, width=W, height=self.FULL_H, highlightthickness=0, bd=0, bg=C_BG)
        self.W, self.tabs, self.icons, self.labels, self.on_pick = W, tabs, icons, labels, on_pick
        self.active, self.tiles, self._job, self.collapsed = None, {}, None, False
        self._bgpil = None
        try:
            self._bgpil = make_aurora(W, self.FULL_H)
            self._draw_logo(self._bgpil)
            self._bgimg = _pil_photo(self._bgpil)
            self.create_image(0, 0, image=self._bgimg, anchor="nw")
        except Exception:                                  # noqa: BLE001
            self._bgpil = None
            self.configure(bg=CAP)
        self._build_top(buttons, on_chip)
        self._build_tiles()

    @staticmethod
    def _draw_logo(img):
        """로고(그라데이션 둥근 네모)를 오로라 배경에 직접 그려 모서리가 배경과 자연스럽게 섞이게"""
        from PIL import Image, ImageDraw
        k, s = 4, 30
        c1 = tuple(int(C_G1[i:i + 2], 16) for i in (1, 3, 5))
        c2 = tuple(int(C_G2[i:i + 2], 16) for i in (1, 3, 5))
        g = Image.new("RGB", (s * k, s * k))
        d = ImageDraw.Draw(g)
        for i in range(s * k * 2):
            t = i / float(s * k * 2)
            d.line((i, 0, 0, i), fill=tuple(round(c1[j] + (c2[j] - c1[j]) * t) for j in range(3)))
        m = Image.new("L", (s * k, s * k), 0)
        ImageDraw.Draw(m).rounded_rectangle((0, 0, s * k - 1, s * k - 1), radius=9 * k, fill=255)
        img.paste(g.resize((s, s), Image.LANCZOS), (14, 12), m.resize((s, s), Image.LANCZOS))

    # --- 맨 위: 로고·제목·연결 상태·창 단추
    def _build_top(self, buttons, on_chip):
        gl = icon("doc", white=True)
        if gl is not None:
            self.create_image(29, 27, image=gl)
        self.create_text(54, 19, text="양식 도우미", anchor="w", fill=C_TEXT, font=(FONT[0], 11, "bold"))
        st = self.create_text(54, 38, text="", anchor="w", fill=C_SUB, font=(FONT[0], 8), tags=("chip",))
        self.chip = _Chip(self, st, on_chip)
        self.tag_bind("chip", "<Enter>", lambda e: self.config(cursor="hand2"))
        self.tag_bind("chip", "<Leave>", lambda e: self.config(cursor=""))
        for i, (glyph, cmd, tip) in enumerate(buttons):
            cx = self.W - 20 - i * 28
            tag = "hb%d" % i
            ov = self.create_oval(cx - 12, 27 - 12, cx + 12, 27 + 12, fill="", outline="", tags=(tag,))
            tx = self.create_text(cx, 27, text=glyph, fill=C_SUB, font=(FONT[0], 11 if glyph != "▭" else 9, "bold"), tags=(tag,))
            self.tag_bind(tag, "<Button-1>", lambda e, c=cmd: c())
            self.tag_bind(tag, "<Enter>", lambda e, o=ov, t=tx: (self.itemconfig(o, fill="#ffffff", outline="#dfe3f5"), self.itemconfig(t, fill=C_ACC), self.config(cursor="hand2")))
            self.tag_bind(tag, "<Leave>", lambda e, o=ov, t=tx: (self.itemconfig(o, fill="", outline=""), self.itemconfig(t, fill=C_SUB), self.config(cursor="")))

    # --- 유리 타일
    def _build_tiles(self):
        tw, th, gap, x0, y0 = 46, 56, 5, 7, 60
        for idx, (name, key) in enumerate(self.tabs):
            x = x0 + (idx % 5) * (tw + gap)
            y = y0 + (idx // 5) * (th + gap + 1)
            tag = "tile_" + key
            st = {"x": x, "y": y, "w": tw, "h": th, "t": 0.0, "target": 0.0, "tag": tag, "name": name}
            self.tiles[key] = st
            st["img"] = self.create_image(x - self.PAD, y - self.PAD, anchor="nw", tags=(tag,))
            st["ico"] = self.create_image(x + tw // 2, y + 20, tags=(tag,))
            st["txt"] = self.create_text(x + tw // 2, y + th - 13, text=name, tags=(tag,), font=(FONT[0], 9))
            self.tag_bind(tag, "<Enter>", lambda e, k=key: self._hover(k, 1.0))
            self.tag_bind(tag, "<Leave>", lambda e, k=key: self._hover(k, 0.0))
            self.tag_bind(tag, "<ButtonPress-1>", lambda e, k=key: self._press(k, True))
            self.tag_bind(tag, "<ButtonRelease-1>", lambda e, k=key: self._release(k, e))
            self._paint(key)

    def _press(self, key, v):
        self.tiles[key]["p"] = v
        self._paint(key)

    def _release(self, key, e):
        s = self.tiles[key]
        was = s.get("p")
        s["p"] = False
        self._paint(key)
        if was and s["x"] - 4 <= e.x <= s["x"] + s["w"] + 4 and s["y"] - 4 <= e.y <= s["y"] + s["h"] + 4:
            self.on_pick(key)

    def _hover(self, key, v):
        self.tiles[key]["target"] = v
        if not v:
            self.tiles[key]["p"] = False
        self.config(cursor="hand2" if v else "")
        if self._job is None:
            self._step()

    def _step(self):
        busy = False
        for k, s in self.tiles.items():
            d = s["target"] - s["t"]
            if abs(d) > 0.03:
                s["t"] += d * 0.38
                busy = True
            else:
                s["t"] = s["target"]
            self._paint(k)
        self._job = self.after(16, self._step) if busy else None

    def set_active(self, key):
        self.active = key
        for k in self.tiles:
            self._paint(k)

    def _paint(self, key):
        s, on = self.tiles[key], key == self.active
        t, p = _q(s["t"]), bool(s.get("p"))
        im = self._glass(key, t, on, p)
        self.itemconfigure(s["img"], image=im if im is not None else "")
        dy = (2 if p else (-1.5 * t))                      # 올리면 살짝 떠오르고, 누르면 아래로 눌림 — 아이콘과 글자가 함께 움직인다
        x, y, w, h = s["x"], s["y"], s["w"], s["h"]
        self.coords(s["ico"], x + w // 2, y + 20 + dy)
        self.coords(s["txt"], x + w // 2, y + h - 13 + dy)
        ic = icon(self.icons.get(key, "doc"), white=on)
        self.itemconfigure(s["ico"], image=ic if ic is not None else "")
        col = "white" if on else (_mix(C_SUB, C_ACC, t) if not p else C_ACC)
        self.itemconfigure(s["txt"], fill=col, font=(FONT[0], 9, "bold" if (on or t > 0.5) else "normal"))

    def _glass(self, key, t, on, p=False):
        """리퀴드 글래스 타일: 평소엔 투명(테두리 없음), 올리면 유리가 떠오르고, 선택된 타일은 색 입힌 유리"""
        s = self.tiles[key]
        if not on and t == 0 and not p:
            return None
        if on:
            return liquid_img(s["w"], s["h"], 14, base=(232, 190), tint=(C_G1, C_G2), t=t, pressed=p, pad=self.PAD)
        return liquid_img(s["w"], s["h"], 14, base=(int(70 + 165 * t) if not p else 238, int(24 + 150 * t) if not p else 196),
                          t=t, pressed=p, pad=self.PAD)


def _pil_photo(img):
    import base64
    import io
    bio = io.BytesIO()
    img.save(bio, "PNG")
    return tk.PhotoImage(data=base64.b64encode(bio.getvalue()))


class PillScroll(tk.Canvas):
    """가는 알약 모양 스크롤바 (평소엔 옅게, 마우스를 올리면 진하게)"""

    def __init__(self, master, command):
        super().__init__(master, width=10, bg=C_BG, highlightthickness=0, bd=0)
        self.command, self.first, self.last, self.hot, self.drag = command, 0.0, 1.0, False, None
        self.bind("<Configure>", lambda e: self.draw())
        self.bind("<Enter>", lambda e: self._hot(True))
        self.bind("<Leave>", lambda e: self._hot(False))
        self.bind("<ButtonPress-1>", self._down)
        self.bind("<B1-Motion>", self._move)

    def _hot(self, v):
        self.hot = v
        self.draw()

    def set(self, first, last):
        self.first, self.last = float(first), float(last)
        self.draw()

    def draw(self):
        self.delete("all")
        h = self.winfo_height()
        if self.last - self.first >= 0.999 or h < 20:
            return
        y0, y1 = 2 + self.first * (h - 4), 2 + self.last * (h - 4)
        y1 = max(y1, y0 + 24)
        round_rect(self, 2, y0, 8, y1, 3, fill="#b4b9c4" if self.hot else "#d3d6de", outline="")

    def _down(self, e):
        h = self.winfo_height()
        top, bot = self.first * h, self.last * h
        if top <= e.y <= bot:
            self.drag = e.y - top
        else:
            self.drag = (bot - top) / 2
            self._move(e)

    def _move(self, e):
        h = max(1, self.winfo_height())
        if self.drag is not None:
            self.command("moveto", max(0.0, min(1.0, (e.y - self.drag) / h)))


class Widget(tk.Tk):
    """한글 옆에 떠 있는 양식 단추 모음. 단추를 누르면 열려 있는 한글 문서에 바로 만들어 넣는다."""

    TAB_LABEL = {"report": "보고", "week": "주간업무 · 간부회의", "budget": "예산 · 이월 조서", "press": "보도자료",
                 "assembly": "국회단계 질의서", "audit": "행정사무감사", "nat": "국가예산 신규사업", "tools": "도구 (표·날짜·문구)",
                 "sym": "기호 · 글자 겹치기", "more": "설정 · 더보기"}
    TAB_ICON = {"report": "doc", "week": "schedule", "budget": "calc", "press": "text", "assembly": "mayor", "audit": "plan",
                "nat": "nation", "tools": "table", "sym": "swap", "more": "sliders"}
    TABS = [("보고", "report"), ("주간", "week"), ("예산", "budget"), ("보도", "press"),
            ("국회", "assembly"), ("감사", "audit"), ("국비", "nat"), ("도구", "tools"), ("기호", "sym"),
            ("설정", "more")]
    # 양식 묶음(audience) → 탭. 사용자가 추가한 양식은 '보고' 탭에 들어간다
    AUDIENCE_TAB = {"문화교육국 보고": "report", "시장님 보고": "report", "간부회의": "week", "예산 설명자료": "budget", "참고자료(백데이터)": "budget",
                    "보도자료": "press", "국회단계 질의서": "assembly", "국회단계 서면질의서": "assembly",
                    "국회단계 정책질의서": "assembly", "행정사무감사": "audit", "이월 조서": "budget",
                    "국가예산 신규사업": "nat"}
    GROUP_ORDER = ["문화교육국 보고", "시장님 보고", "간부회의", "예산 설명자료", "참고자료(백데이터)", "보도자료", "국회단계 서면질의서",
                   "국회단계 정책질의서", "국회단계 질의서", "행정사무감사", "이월 조서", "국가예산 신규사업"]
    FORM_ORDER = ["report_event", "report_project", "report_mayor", "meeting_cover", "meeting_issue", "meeting_card", "issue_list", "issue_card", "issue_ref",
                  "budget_cover", "budget_project", "budget_ref_cover", "budget_ref_detail", "budget_ref_return", "press_release",
                  "written_cover", "written_list", "written_item", "policy_cover", "policy_list", "policy_item",
                  "policy_ref", "audit_cover", "audit_common", "audit_item", "carry_item", "nbud_main", "nbud_ref1", "nbud_ref2"]
    FORM_ICONS = {"report_event": "event", "report_project": "project", "report_mayor": "mayor",
                  "meeting_cover": "cover", "meeting_issue": "event", "meeting_card": "project", "issue_list": "plan", "issue_card": "event", "issue_ref": "clip",
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
        try:                                             # 창·작업 표시줄 아이콘
            self._app_icon = tk.PhotoImage(file=os.path.join(registry.app_dir(), "assets", "app_256.png"))
            self.iconphoto(True, self._app_icon)
        except Exception:                                # noqa: BLE001
            pass
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
            rgb = int(CAP[1:], 16)
            bgr = ((rgb & 0xff) << 16) | (rgb & 0xff00) | (rgb >> 16)
            dwm(hwnd, 35, ctypes.byref(ctypes.c_int(bgr)), 4)             # 제목줄 색 = 머리 맨 위 색(창이 끊김 없이 이어짐)
            dwm(hwnd, 34, ctypes.byref(ctypes.c_int(bgr)), 4)             # 창 테두리 색도 같게
            dwm(hwnd, 36, ctypes.byref(ctypes.c_int(0x0029231f)), 4)  # 제목줄 글자 진한 회색
        except Exception:                                # noqa: BLE001
            pass

    # ---------------------------------------------------------------- 뼈대
    def _build_frame(self):
        W = WIDGET_W
        buttons = (("⌄", self.to_tray, "트레이로 보내기"), ("▭", self.dock, "한글 옆에 붙이기"), ("–", self.toggle, "접기·펴기"))
        self.head = GlassHeader(self, self.TABS, self.TAB_ICON, self.TAB_LABEL, self.show_tab, buttons,
                                lambda: self.connect() if not self.bridge.connected else None)
        self.head.pack(fill="x")
        self.chip = self.head.chip
        self.tiles = self.head.tiles
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
        self.head.set_active(key)
        self.unbind_all("<MouseWheel>")
        self.unbind_all("<Button-4>")
        self.unbind_all("<Button-5>")
        for w in self.body.winfo_children():
            w.destroy()
        {"week": self.build_week, "sym": self.build_symbols, "more": self.build_more, "tools": self.build_tools}.get(
            key, lambda parent, k=key: self.build_forms_tab(parent, k))(self.body)
        self.ui["tab"] = key

    def section(self, parent, text):
        tk.Label(parent, text=text, bg=C_BG, fg=C_ACC, font=(FONT[0], 9, "bold"), anchor="w").pack(fill="x", pady=(12, 4))

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
                box = tk.Frame(parent, bg="#ffffff", highlightthickness=1, highlightbackground="#e1e5f5")
                box.pack(fill="x", pady=(0, 4))
                tk.Label(box, text="내 정보(과·담당자)를 한 번 적어 두면\n양식에 자동으로 채워져요", bg="#ffffff", fg="#5b6384",
                         font=FONT_S, justify="left", padx=10, pady=6).pack(anchor="w")
                Btn(box, "내 정보 입력", self.open_profile, icon_name="user", h=32).pack(fill="x", padx=8, pady=(0, 8))
        tk.Label(parent, text="글을 선택하고 양식을 누르면 그 글이 양식으로 바뀌어요", bg=C_BG, fg=C_SUB, font=FONT_S, anchor="w",
                 wraplength=WIDGET_W - 24, justify="left").pack(fill="x", pady=(2, 0))
        self._convert_var = tk.IntVar(value=1 if self.ui.get("convert") else 0)
        tk.Checkbutton(parent, text="선택 없으면 문서 전체 변환", variable=self._convert_var, bg=C_BG, fg=C_TEXT,
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
        sb = PillScroll(parent, cv.yview)
        inner = tk.Frame(cv, bg=C_BG)
        win = cv.create_window((0, 0), window=inner, anchor="nw")
        inner.bind("<Configure>", lambda e: cv.configure(scrollregion=cv.bbox("all")))
        cv.bind("<Configure>", lambda e: cv.itemconfigure(win, width=e.width))
        cv.configure(yscrollcommand=sb.set)
        sb.pack(side="right", fill="y", padx=(2, 0))
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
            Chip(row, "−", lambda v=var: v.set(max(1, int(v.get()) - 1)), size=26, font=(FONT[0], 11, "bold")).pack(side="left")
            tk.Label(row, textvariable=var, bg=C_BG, fg=C_TEXT, font=FONT_B, width=3).pack(side="left")
            Chip(row, "+", lambda v=var: v.set(min(30, int(v.get()) + 1)), size=26, font=(FONT[0], 11, "bold")).pack(side="left", padx=(0, 10))
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
        Btn(parent, "오류 기록 폴더 열기", self.open_log_dir, kind="flat", icon_name="doc",
            tip="양식 넣기가 실패하면 자세한 내용이 error.log 에 남아요").pack(fill="x", pady=4)
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
            if plain:                                    # 보도자료: 대략 적은 글(선택한 글 또는 입력창)을 흔한 보도자료 구성의 초안으로
                rough, sel_used = "", False
                if self.bridge.connected:
                    sel = self.bridge.get_selection_text()
                    if sel.strip():
                        rough, sel_used = sel, True
                if not rough:
                    rough = self.ask_rough("보도자료 초안", "제목, 일시·장소·주최·대상·내용을 대략 적으면 보도자료 문단으로 정리해 드려요.\n"
                                           "(비워 두면 틀만 넣어요)", "예)\n익산 푸드 페스티벌\n일시: 2026. 11. 20.(금) 14:00\n장소: 중앙체육관\n"
                                           "주최: 익산시\n대상: 시민 누구나\n내용: 먹거리 부스 운영\n내용: 체험 행사") or ""
                if rough.strip():
                    from . import press
                    prof0 = PR.load_profile()
                    title0, paras = press.draft(rough, dept=prof0.get("dept", ""), tel=prof0.get("staff_ext", ""))
                    if any(x["key"] == "title" for x in f.fields):
                        vals["title"] = title0
                    outline = "\n".join(paras)
                    replace = "selection" if sel_used else None
            if outline and not plain and self.bridge.connected:
                # 글을 선택해 두고 양식을 누르면 그 글이 바로 그 양식으로 바뀐다(서식 무시하고 아무렇게나 쓴 글도 됨).
                # 선택이 없으면 평소처럼 새로 넣고, '문서 전체' 체크를 켠 때만 전체를 바꾼다.
                sel = self.bridge.get_selection_text()
                src = sel if sel.strip() else (self.bridge.get_all_text() if self.ui.get("convert") else "")
                if src.strip():
                    whole = not sel.strip()
                    if whole and not messagebox.askyesno(
                            "서식 바꾸기", "문서 전체를 '%s' 양식으로 바꿔 넣을까요?\n(한글에서 Ctrl+Z 로 되돌릴 수 있어요)" % f.meta["name"].split(" (")[0]):
                        return
                    title, body = T.split_title(src) if any(x["key"] == "title" for x in f.fields) else ("", src)
                    if title:
                        vals["title"] = title
                    outline = T.normalize_outline(body)
                    replace = "all" if whole else "selection"
            pkg = f.render(vals, outline)
        except Exception as e:                           # noqa: BLE001
            messagebox.showerror("만들 수 없어요", str(e))
            return
        PR.push_recent(self.ui, "recent_forms", fid, 5)
        self.busy(lambda: self.out.insert(pkg, f.id, replace))

    def ask_rough(self, title, msg, hint=""):
        """대략 적은 글을 입력받는 작은 창 → 글(비어 있으면 ''), 닫으면 None"""
        dlg = tk.Toplevel(self)
        dlg.title(title)
        dlg.configure(bg=C_BG)
        dlg.attributes("-topmost", True)
        dlg.geometry("+%d+%d" % (max(0, self.winfo_x() - 330), self.winfo_y() + 60))
        tk.Label(dlg, text=msg, bg=C_BG, fg=C_TEXT, font=FONT, justify="left", anchor="w", wraplength=360).pack(fill="x", padx=14, pady=(12, 6))
        box = tk.Text(dlg, width=46, height=12, font=FONT, relief="flat", highlightthickness=1, highlightbackground=C_LINE,
                      highlightcolor=C_ACC, wrap="word", padx=8, pady=6)
        box.pack(padx=14)
        if hint:
            box.insert("1.0", hint)
            box.config(fg=C_SUB)
            def clear(_e=None):
                if box.cget("fg") == C_SUB:
                    box.delete("1.0", "end")
                    box.config(fg=C_TEXT)
            box.bind("<FocusIn>", clear)
        res = {"v": None}
        row = tk.Frame(dlg, bg=C_BG)
        row.pack(fill="x", padx=14, pady=10)
        def ok():
            res["v"] = "" if box.cget("fg") == C_SUB else box.get("1.0", "end").strip()
            dlg.destroy()
        Btn(row, "초안 만들기", ok, kind="accent", h=36, w=130, anchor="c").pack(side="right")
        Btn(row, "틀만 넣기", lambda: (res.__setitem__("v", ""), dlg.destroy()), kind="flat", h=36, w=100, anchor="c").pack(side="right", padx=6)
        box.focus_set()
        dlg.grab_set()
        self.wait_window(dlg)
        return res["v"]

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
                sel = self.bridge.get_selection_text() if self.bridge.connected else ""
                if sel.strip():                          # 아무렇게나 쓴 글을 선택하고 누르면 그 글이 주간업무계획 서식으로
                    rough_items = weekly.parse_rough_plan(sel, prof.get("dept", ""), prof.get("staff_name", ""), prof.get("staff_ext", ""))
                    if rough_items:
                        replace = "selection"
                        pkg = registry.load_form("weekly_plan").render({"org": org, "items": rough_items})
                        self.busy(lambda: self.out.insert(pkg, "주간업무계획", replace))
                        return
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
            self.body.pack_forget()
            self.head.configure(height=GlassHeader.MIN_H)
            self.geometry("%dx%d" % (WIDGET_W, GlassHeader.MIN_H + 44))
        else:
            self.head.configure(height=GlassHeader.FULL_H)
            self.body.pack(fill="both", expand=True, padx=10, pady=6)
            self.geometry("%dx%d" % (WIDGET_W, getattr(self, "_h", 700)))

    # ---------------------------------------------------------------- 트레이
    def _tray_image(self):
        from PIL import Image
        try:
            return Image.open(os.path.join(registry.app_dir(), "assets", "app_256.png")).convert("RGBA").resize((64, 64), Image.LANCZOS)
        except Exception:                                # noqa: BLE001
            return Image.new("RGBA", (64, 64), (0x4a, 0x5b, 0xd8, 255))

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

    def open_log_dir(self):
        d = os.path.join(os.environ.get("APPDATA") or os.path.expanduser("~"), "HwpFormGenerator")
        os.makedirs(d, exist_ok=True)
        try:
            os.startfile(d)
        except Exception:                                # noqa: BLE001
            self.say("기록 폴더: %s" % d)

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
