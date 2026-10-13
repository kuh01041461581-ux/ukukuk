# -*- coding: utf-8 -*-
"""
DeskCalendar - 바탕화면 위젯 달력 + 날짜별 메모 / 할 일
 - 바탕화면에 붙어 있는 위젯 (작업표시줄·Alt+Tab에 안 뜸, Win+D 눌러도 유지)
 - Ctrl+Alt+Space : 어디서든 위젯을 앞으로 불러와 바로 입력
 - 할 일 입력창에서 "14:30 팀 회의" 처럼 시간을 앞에 쓰면 시간 태그로 저장
 - Windows 시작 시 자동 실행 (exe 첫 실행 때 자동 등록, 우클릭 메뉴로 끄기 가능)
 - 데이터: %APPDATA%\\DeskCalendar\\data.json
"""
import calendar
import datetime as dt
import json
import os
import queue
import re
import shutil
import sys
import threading
import tkinter as tk
import tkinter.font as tkfont
from tkinter import filedialog, messagebox

import gongmun

APP_NAME = "DeskCalendar"
IS_WIN = sys.platform == "win32"
IS_FROZEN = getattr(sys, "frozen", False)

DATA_DIR = os.path.join(os.environ.get("APPDATA") or os.path.expanduser("~"), APP_NAME)
DATA_FILE = os.path.join(DATA_DIR, "data.json")
CONF_FILE = os.path.join(DATA_DIR, "config.json")

if IS_WIN:
    import ctypes
    from ctypes import wintypes

DEFAULT_FONT = "Malgun Gothic" if IS_WIN else "NanumGothic"
# 글꼴 메뉴에 바로 보여줄 후보 (설치된 것만 표시)
FONT_CHOICES = ["Malgun Gothic", "Segoe UI Variable Text", "Segoe UI", "Pretendard", "Pretendard Variable",
                "Noto Sans KR", "Noto Sans CJK KR", "NanumGothic", "NanumSquare", "NanumSquare Round",
                "NanumBarunGothic", "NanumMyeongjo", "Gulim", "Dotum", "Batang", "Gungsuh"]

# ── 색상 테마 (Linear 풍 딥 그래파이트 + Notion 풍 태그 컬러) ─────────
BG = "#14151a"          # 창 바탕
SURFACE = "#1a1b22"     # 오른쪽 패널
CARD = "#212230"        # 날짜 셀
CARD_DIM = "#191a22"    # 이번 달이 아닌 날짜
CARD_HOVER = "#2a2c3d"
BORDER = "#2b2d3b"
BORDER_HI = "#44475f"
TEXT = "#ececf3"
SUB = "#a1a4b8"
DIM = "#5e6178"
ACCENT = "#7c6cff"      # 바이올렛
ACCENT_SOFT = "#2c2850"
DONE = "#5e6178"
SUN = "#ff6b81"         # 코랄
SAT = "#5db8ff"         # 스카이
# 중요도 (인덱스 = 저장값 "pri", 기본 2 = 보통)
PRIORITIES = [("매우 중요", "#ff5c7a"), ("중요", "#ffb547"), ("보통", "#8b7bff"), ("낮음", "#3ddc97")]
DEFAULT_PRI = 2

# 양력 고정 공휴일
FIXED_HOLIDAYS = {
    (1, 1): "신정", (3, 1): "삼일절", (5, 5): "어린이날", (6, 6): "현충일",
    (8, 15): "광복절", (10, 3): "개천절", (10, 9): "한글날", (12, 25): "성탄절",
}
# 음력·대체·임시 공휴일은 해마다 달라서 연도별로 직접 적는다 (2026년분)
EXTRA_HOLIDAYS = {
    (2026, 2, 16): "설 연휴", (2026, 2, 17): "설날", (2026, 2, 18): "설 연휴",
    (2026, 3, 2): "대체공휴일", (2026, 5, 24): "부처님오신날", (2026, 5, 25): "대체공휴일",
    (2026, 6, 3): "지방선거", (2026, 8, 17): "대체공휴일",
    (2026, 9, 24): "추석 연휴", (2026, 9, 25): "추석", (2026, 9, 26): "추석 연휴",
    (2026, 10, 5): "대체공휴일",
}
WEEKDAY_KO = "월화수목금토일"
TIME_RE = re.compile(r"^\s*(\d{1,2}):(\d{2})\s+(.+)$")


def holiday_name(d):
    return EXTRA_HOLIDAYS.get((d.year, d.month, d.day)) or FIXED_HOLIDAYS.get((d.month, d.day))


# ── 저장 ────────────────────────────────────────────────
def load_json(path, default):
    if not os.path.exists(path):
        return default
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        # 손상된 파일은 덮어쓰지 않도록 백업해 둔다
        stamp = dt.datetime.now().strftime("%Y%m%d%H%M%S")
        try:
            shutil.copy(path, f"{path}.broken-{stamp}")
        except Exception:
            pass
        return default


def save_json(path, obj):
    os.makedirs(DATA_DIR, exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=2)
    os.replace(tmp, path)


# ── 자동 실행 (레지스트리 HKCU\...\Run) ─────────────────
RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"


def launch_command():
    if IS_FROZEN:
        return f'"{sys.executable}"'
    exe = sys.executable
    pyw = os.path.join(os.path.dirname(exe), "pythonw.exe")
    if os.path.exists(pyw):
        exe = pyw
    return f'"{exe}" "{os.path.abspath(__file__)}"'


def get_autostart():
    if not IS_WIN:
        return False
    import winreg
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY) as k:
            winreg.QueryValueEx(k, APP_NAME)
            return True
    except OSError:
        return False


def set_autostart(on):
    if not IS_WIN:
        return
    import winreg
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY, 0, winreg.KEY_SET_VALUE) as k:
        if on:
            winreg.SetValueEx(k, APP_NAME, 0, winreg.REG_SZ, launch_command())
        else:
            try:
                winreg.DeleteValue(k, APP_NAME)
            except FileNotFoundError:
                pass


# ── Windows 유틸 ────────────────────────────────────────
_MUTEX = None


def ensure_single_instance():
    """이미 실행 중이면 False"""
    global _MUTEX
    k32 = ctypes.WinDLL("kernel32", use_last_error=True)
    k32.CreateMutexW.restype = wintypes.HANDLE
    _MUTEX = k32.CreateMutexW(None, False, "Local\\DeskCalendar_SingleInstance")
    return ctypes.get_last_error() != 183  # ERROR_ALREADY_EXISTS


def enable_dpi_awareness():
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


def virtual_screen(root):
    """멀티 모니터 전체 영역 (x, y, w, h)"""
    if IS_WIN:
        m = ctypes.windll.user32.GetSystemMetrics
        return m(76), m(77), m(78), m(79)
    return 0, 0, root.winfo_screenwidth(), root.winfo_screenheight()


def start_hotkey_listener(q, mods, vk):
    """전역 단축키 → q 에 신호. 별도 스레드의 메시지 루프에서 처리한다."""
    def run():
        u = ctypes.windll.user32
        if not u.RegisterHotKey(None, 1, mods | 0x4000, vk):  # MOD_NOREPEAT
            return
        msg = wintypes.MSG()
        while u.GetMessageW(ctypes.byref(msg), None, 0, 0) > 0:
            if msg.message == 0x0312:  # WM_HOTKEY
                q.put(1)

    threading.Thread(target=run, daemon=True).start()


# ── 그리기 헬퍼 ─────────────────────────────────────────
def blend(fg, bg, a):
    """fg 를 a(0~1) 만큼 bg 위에 얹은 색"""
    f = [int(fg[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(bg[i:i + 2], 16) for i in (1, 3, 5)]
    return "#" + "".join(f"{round(x * a + y * (1 - a)):02x}" for x, y in zip(f, b))


def rrect(c, x1, y1, x2, y2, r, **kw):
    """둥근 사각형"""
    r = max(0, min(r, (x2 - x1) / 2, (y2 - y1) / 2))
    pts = [x1 + r, y1, x2 - r, y1, x2, y1, x2, y1 + r, x2, y2 - r, x2, y2,
           x2 - r, y2, x1 + r, y2, x1, y2, x1, y2 - r, x1, y1 + r, x1, y1]
    return c.create_polygon(pts, smooth=True, **kw)


def pri_of(t):
    p = t.get("pri", DEFAULT_PRI)
    return p if isinstance(p, int) and 0 <= p < len(PRIORITIES) else DEFAULT_PRI


def pri_color(t):
    return PRIORITIES[pri_of(t)][1]


def todo_order(todos):
    """표시 순서: 미완료(중요도 높은 순 → 시간 순) → 완료. 반환값은 todos 의 인덱스 목록"""
    return sorted(range(len(todos)),
                  key=lambda i: (bool(todos[i].get("done")), pri_of(todos[i]),
                                 todos[i].get("time") or "99:99", i))


class Pill(tk.Canvas):
    """둥근 알약형 버튼"""

    def __init__(self, parent, text, cmd, font, fg=TEXT, bg=CARD, hover=CARD_HOVER,
                 parent_bg=BG, padx=12, pady=5, r=8):
        w = font.measure(text) + 2 * padx
        h = font.metrics("linespace") + 2 * pady
        super().__init__(parent, width=w, height=h, bg=parent_bg, highlightthickness=0,
                         bd=0, cursor="hand2")
        shape = rrect(self, 1, 1, w - 1, h - 1, r, fill=bg)
        self.create_text(w / 2, h / 2, text=text, font=font, fill=fg)
        self.bind("<Enter>", lambda e: self.itemconfig(shape, fill=hover))
        self.bind("<Leave>", lambda e: self.itemconfig(shape, fill=bg))
        self.bind("<Button-1>", lambda e: cmd())


# ── 대화상자 ────────────────────────────────────────────
def _dialog(app, title, w, h):
    top = tk.Toplevel(app.root)
    top.title(title)
    top.configure(bg=SURFACE)
    top.attributes("-topmost", True)
    top.update_idletasks()
    x = app.root.winfo_x() + max(0, (app.root.winfo_width() - w) // 2)
    y = app.root.winfo_y() + max(0, (app.root.winfo_height() - h) // 2)
    top.geometry(f"{w}x{h}+{x}+{y}")
    return top


class ImportDialog:
    """공문 PDF 에서 찾은 일정 후보를 확인하고 추가하는 창"""

    def __init__(self, app, doc_title, items):
        self.app, S = app, app.S
        self.rows = []
        top = self.top = _dialog(app, "공문에서 일정 가져오기", S(760), S(520))
        tk.Label(top, text="공문에서 일정 가져오기", font=app.f_h2, bg=SURFACE, fg=TEXT,
                 anchor="w").pack(fill="x", padx=S(20), pady=(S(16), 0))
        tk.Label(top, text=doc_title or "(제목 없음)", font=app.f_small, bg=SURFACE, fg=ACCENT,
                 anchor="w", wraplength=S(700), justify="left").pack(fill="x", padx=S(20))
        msg = ("날짜가 들어 있는 항목을 찾았어요. 추가할 항목만 체크하고, 날짜·내용을 고친 뒤 추가하세요."
               if items else "날짜가 있는 항목을 찾지 못했어요. 아래 '＋ 행 추가'로 직접 넣을 수 있어요.")
        tk.Label(top, text=msg, font=app.f_tiny, bg=SURFACE, fg=SUB, anchor="w").pack(
            fill="x", padx=S(20), pady=(S(4), S(8)))

        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=S(20), pady=S(14))
        self.count = tk.Label(foot, font=app.f_small, bg=SURFACE, fg=SUB)
        self.count.pack(side="left")
        Pill(foot, "추가", self.confirm, app.f_body, fg="#ffffff", bg=ACCENT, hover="#9185ff",
             parent_bg=SURFACE, padx=S(20), pady=S(6)).pack(side="right")
        Pill(foot, "취소", top.destroy, app.f_body, parent_bg=SURFACE, padx=S(16),
             pady=S(6)).pack(side="right", padx=S(8))
        Pill(foot, "＋ 행 추가", lambda: self.add_row(None), app.f_small, parent_bg=SURFACE,
             padx=S(12), pady=S(6)).pack(side="right")

        wrap = tk.Frame(top, bg=SURFACE)
        wrap.pack(fill="both", expand=True, padx=S(20))
        self.cv = tk.Canvas(wrap, bg=SURFACE, highlightthickness=0, bd=0)
        self.cv.pack(fill="both", expand=True)
        self.inner = tk.Frame(self.cv, bg=SURFACE)
        win = self.cv.create_window((0, 0), window=self.inner, anchor="nw")
        self.inner.bind("<Configure>", lambda e: self.cv.configure(scrollregion=self.cv.bbox("all")))
        self.cv.bind("<Configure>", lambda e: self.cv.itemconfigure(win, width=e.width))
        self.cv.bind("<MouseWheel>", lambda e: self.cv.yview_scroll(int(-e.delta / 120), "units"))
        for it in items:
            self.add_row(it)
        self.refresh_count()

    def add_row(self, it):
        app, S = self.app, self.app.S
        it = it or {"date": app.selected, "time": "", "text": "", "pri": DEFAULT_PRI}
        row = tk.Frame(self.inner, bg=SURFACE)
        row.pack(fill="x", pady=S(3))
        var = tk.BooleanVar(value=True)
        tk.Checkbutton(row, variable=var, bg=SURFACE, activebackground=SURFACE, selectcolor=CARD,
                       fg=TEXT, command=self.refresh_count, bd=0, highlightthickness=0
                       ).pack(side="left")

        def entry(width, text, expand=False):
            e = tk.Entry(row, font=app.f_small, bg=CARD, fg=TEXT, insertbackground=TEXT,
                         relief="flat", width=width, highlightthickness=1,
                         highlightbackground=BORDER, highlightcolor=ACCENT)
            e.insert(0, text)
            e.pack(side="left", fill="x" if expand else "none", expand=expand, padx=(0, S(6)),
                   ipady=S(4))
            return e

        d = entry(11, it["date"].isoformat())
        t = entry(6, it.get("time", ""))
        x = entry(10, it["text"], expand=True)
        r = {"var": var, "date": d, "time": t, "text": x, "pri": it.get("pri", DEFAULT_PRI)}
        dot = tk.Canvas(row, width=S(16), height=S(16), bg=SURFACE, highlightthickness=0,
                        cursor="hand2")
        oval = dot.create_oval(2, 2, S(14), S(14), fill=PRIORITIES[r["pri"]][1], outline="")

        def cycle(_=None):
            r["pri"] = (r["pri"] - 1) % len(PRIORITIES)
            dot.itemconfig(oval, fill=PRIORITIES[r["pri"]][1])

        dot.bind("<Button-1>", cycle)
        dot.pack(side="left", padx=(0, S(4)))
        for w in (row, dot):
            w.bind("<MouseWheel>", lambda e: self.cv.yview_scroll(int(-e.delta / 120), "units"))
        self.rows.append(r)
        self.refresh_count()

    def refresh_count(self):
        n = sum(1 for r in self.rows if r["var"].get())
        self.count.config(text=f"{n}개 선택됨")

    def confirm(self):
        app = self.app
        picked = []
        for r in self.rows:
            if not r["var"].get():
                continue
            text = r["text"].get().strip()
            if not text:
                continue
            try:
                d = dt.date.fromisoformat(r["date"].get().strip())
            except ValueError:
                messagebox.showwarning("날짜 확인", "날짜는 2026-10-20 처럼 적어 주세요.",
                                       parent=self.top)
                return
            tm = r["time"].get().strip()
            m = re.fullmatch(r"(\d{1,2}):(\d{2})", tm)
            tm = f"{int(m.group(1)):02d}:{m.group(2)}" if m else ""
            picked.append((d, {"text": text, "done": False, "pri": r["pri"],
                               **({"time": tm} if tm else {})}))
        for d, item in picked:
            app._entry(d, create=True)["todos"].append(item)
        if picked:
            app._after_todo_change()
        self.top.destroy()
        if picked:
            messagebox.showinfo("가져오기 완료", f"{len(picked)}개 일정을 추가했어요.", parent=app.root)


class FontDialog:
    """모든 설치된 글꼴을 검색해서 고르는 창"""

    def __init__(self, app):
        self.app, S = app, app.S
        top = self.top = _dialog(app, "글꼴 선택", S(420), S(500))
        tk.Label(top, text="글꼴 선택", font=app.f_h2, bg=SURFACE, fg=TEXT, anchor="w").pack(
            fill="x", padx=S(20), pady=(S(16), S(8)))
        self.q = tk.StringVar()
        e = tk.Entry(top, textvariable=self.q, font=app.f_body, bg=CARD, fg=TEXT,
                     insertbackground=TEXT, relief="flat", highlightthickness=1,
                     highlightbackground=BORDER, highlightcolor=ACCENT)
        e.pack(fill="x", padx=S(20), ipady=S(5))
        self.q.trace_add("write", lambda *a: self.fill())
        self.preview = tk.Label(top, text="가나다라마바사 오늘의 일정 ABC abc 123", bg=CARD, fg=TEXT,
                                height=3, wraplength=S(360))
        self.preview.pack(side="bottom", fill="x", padx=S(20), pady=(S(8), S(14)))
        Pill(top, "적용", self.apply, app.f_body, fg="#ffffff", bg=ACCENT, hover="#9185ff",
             parent_bg=SURFACE, padx=S(24), pady=S(6)).pack(side="bottom", pady=(S(8), 0))
        self.lb = tk.Listbox(top, bg=CARD, fg=TEXT, selectbackground=ACCENT, relief="flat",
                             highlightthickness=0, font=app.f_body, activestyle="none")
        self.lb.pack(fill="both", expand=True, padx=S(20), pady=(S(8), 0))
        self.lb.bind("<<ListboxSelect>>", self.on_select)
        self.lb.bind("<Double-Button-1>", lambda e: self.apply())
        self.names = sorted({f for f in tkfont.families() if not f.startswith("@")})
        self.fill()

    def fill(self):
        q = self.q.get().strip().lower()
        self.lb.delete(0, "end")
        for n in self.names:
            if q in n.lower():
                self.lb.insert("end", n)

    def current(self):
        sel = self.lb.curselection()
        return self.lb.get(sel[0]) if sel else None

    def on_select(self, _=None):
        n = self.current()
        if n:
            self.preview.config(font=(n, 12))

    def apply(self):
        n = self.current()
        if n:
            self.app.set_font(n)
            self.top.destroy()


# ── 앱 ──────────────────────────────────────────────────
class DeskCalendar:
    HOTKEY_LABEL = "Ctrl+Alt+Space"
    # (이름, 기본 pt, 굵게)
    FONT_SPEC = {"title": (17, True), "h2": (14, True), "head": (9, True), "day": (10, True),
                 "tiny": (8, False), "small": (9, False), "body": (10, False)}

    def __init__(self, root):
        self.root = root
        self.data = load_json(DATA_FILE, {})
        self.conf = load_json(CONF_FILE, {})
        self.dpi_scale = max(1.0, root.winfo_fpixels("1i") / 96.0)
        self.ui = min(2.0, max(0.7, float(self.conf.get("scale", 1.0))))
        self.scale = self.dpi_scale * self.ui
        installed = set(tkfont.families())
        fam = self.conf.get("font", DEFAULT_FONT)
        self.family = fam if fam in installed else DEFAULT_FONT

        self.today = dt.date.today()
        self.view_y, self.view_m = self.today.year, self.today.month
        self.selected = self.today
        self.mode = "day"          # "day" | "inbox"
        self.new_pri = DEFAULT_PRI
        self.dates = []
        self.hover_idx = None
        self._memo_date = None
        self._save_job = None
        self._scale_job = None
        self._drag = None
        self._grip = None
        self.outer = None
        self.hwnd = None
        self.hotkey_q = queue.Queue()

        self.locked = tk.BooleanVar(value=self.conf.get("locked", False))
        self.alpha = tk.DoubleVar(value=self.conf.get("alpha", 0.97))
        self._init_autostart()
        self.autostart = tk.BooleanVar(value=get_autostart())

        self.fonts = {}
        for k, (pt, bold) in self.FONT_SPEC.items():
            self.fonts[k] = tkfont.Font(family=self.family, size=-1,
                                        weight="bold" if bold else "normal")
        self.f_done = tkfont.Font(family=self.family, size=-1, overstrike=True)
        self.f_tiny_done = tkfont.Font(family=self.family, size=-1, overstrike=True)
        self._apply_fonts()
        self.f_title, self.f_h2, self.f_head = self.fonts["title"], self.fonts["h2"], self.fonts["head"]
        self.f_day, self.f_tiny = self.fonts["day"], self.fonts["tiny"]
        self.f_small, self.f_body = self.fonts["small"], self.fonts["body"]

        self._setup_window()
        self._build_menu()
        self._build_ui()
        self.show_day(self.selected)
        self.render_month()

        root.bind_all("<Control-MouseWheel>", self._on_ctrl_wheel)
        root.after(300, self._apply_desktop_mode)
        root.after(30_000, self._tick)
        if IS_WIN:
            start_hotkey_listener(self.hotkey_q, 0x0002 | 0x0001, 0x20)  # Ctrl+Alt+Space
            root.after(150, self._poll_hotkey)

    def S(self, v):
        return int(round(v * self.scale))

    # ── 글꼴 / 크기 ──
    def _apply_fonts(self):
        for k, (pt, _) in self.FONT_SPEC.items():
            self.fonts[k].configure(family=self.family, size=-max(8, round(pt * 4 / 3 * self.scale)))
        self.f_done.configure(family=self.family, size=-max(8, round(10 * 4 / 3 * self.scale)))
        self.f_tiny_done.configure(family=self.family, size=-max(8, round(8 * 4 / 3 * self.scale)))

    def set_font(self, family):
        self.family = family
        self.conf["font"] = family
        self._save_conf()
        self._apply_fonts()
        self.rebuild()

    def set_ui_scale(self, v):
        v = round(min(2.0, max(0.7, v)), 2)
        if v == self.ui:
            return
        self.ui = v
        self.conf["scale"] = v
        self.scale = self.dpi_scale * v
        self._save_conf()
        self._apply_fonts()
        self.rebuild()

    def rebuild(self):
        self._flush_memo()
        self.hover_idx = None
        self._build_ui()
        self.show_day(self.selected)
        self.render_month()

    def _on_ctrl_wheel(self, e):
        step = 0.05 if e.delta > 0 else -0.05
        target = getattr(self, "_scale_target", self.ui) + step
        self._scale_target = min(2.0, max(0.7, target))
        if self._scale_job:
            self.root.after_cancel(self._scale_job)
        self._scale_job = self.root.after(60, self._apply_scale_target)

    def _apply_scale_target(self):
        self._scale_job = None
        self.set_ui_scale(self._scale_target)

    # ── 초기 설정 ──
    def _init_autostart(self):
        if not (IS_WIN and IS_FROZEN):
            return
        try:
            if not self.conf.get("autostart_initialized"):
                set_autostart(True)  # exe 첫 실행 시 자동 등록
                self.conf["autostart_initialized"] = True
                self._save_conf()
            elif get_autostart():
                set_autostart(True)  # exe 위치가 바뀌었을 수 있으니 경로 갱신
        except Exception:
            pass

    def _setup_window(self):
        r = self.root
        r.title(APP_NAME)
        r.overrideredirect(True)
        r.configure(bg=BORDER)
        r.attributes("-alpha", self.alpha.get())
        vx, vy, vw, vh = virtual_screen(r)
        default_x = r.winfo_screenwidth() - self.S(940)
        x = self.conf.get("x", default_x)
        y = self.conf.get("y", self.S(40))
        x = min(max(vx, x), vx + vw - self.S(120))
        y = min(max(vy, y), vy + vh - self.S(120))
        r.geometry(f"+{x}+{y}")
        r.bind("<FocusOut>", lambda e: r.after(150, self._bottom_if_inactive), add="+")

    # ── UI 구성 ──
    def _build_ui(self):
        if self.outer is not None:
            self.outer.destroy()
        S = self.S
        self.cw, self.ch, self.gap = S(78), S(68), S(5)
        self.wd_h = S(26)

        outer = self.outer = tk.Frame(self.root, bg=BG)
        outer.pack(fill="both", expand=True, padx=1, pady=1)

        # 맨 위 이동 막대 (잡고 끌면 창 이동) + 설정 버튼
        bar = tk.Frame(outer, bg=BG, height=S(18))
        bar.pack(side="top", fill="x")
        bar.pack_propagate(False)
        grab = tk.Label(bar, text="⋯⋯⋯", font=self.f_tiny, bg=BG, fg=DIM, cursor="fleur")
        grab.pack(expand=True)
        gear = tk.Label(bar, text="⚙", font=self.f_small, bg=BG, fg=DIM, cursor="hand2")
        gear.place(relx=1.0, x=-S(10), rely=0.5, anchor="e")
        gear.bind("<Button-1>", lambda e: self.menu.tk_popup(e.x_root, e.y_root))
        gear.bind("<Enter>", lambda e: gear.config(fg=TEXT))
        gear.bind("<Leave>", lambda e: gear.config(fg=DIM))
        for w in (bar, grab):
            self._bind_drag(w)

        right = tk.Frame(outer, bg=SURFACE, width=S(290))
        right.pack(side="right", fill="y")
        right.pack_propagate(False)
        tk.Frame(outer, bg=BORDER, width=1).pack(side="right", fill="y")
        left = tk.Frame(outer, bg=BG, padx=S(20), pady=S(4))
        left.pack(side="left", fill="both", expand=True)

        # 헤더
        hdr = tk.Frame(left, bg=BG)
        hdr.pack(fill="x", pady=(0, S(10)))
        titles = tk.Frame(hdr, bg=BG)
        titles.pack(side="left")
        self.title_lbl = tk.Label(titles, font=self.f_title, bg=BG, fg=TEXT, cursor="fleur")
        self.title_lbl.pack(anchor="w")
        self.month_sub = tk.Label(titles, font=self.f_small, bg=BG, fg=SUB)
        self.month_sub.pack(anchor="w")
        nav = tk.Frame(hdr, bg=BG)
        nav.pack(side="right", anchor="n", pady=(S(4), 0))
        Pill(nav, "공문 PDF", self.import_pdf, self.f_small, padx=S(11), pady=S(4)
             ).pack(side="left", padx=(0, S(10)))
        Pill(nav, "‹", lambda: self.shift_month(-1), self.f_body, padx=S(11), pady=S(3)
             ).pack(side="left")
        Pill(nav, "오늘", self.go_today, self.f_small, fg="#ffffff", bg=ACCENT,
             hover="#9185ff", padx=S(13), pady=S(4)).pack(side="left", padx=S(5))
        Pill(nav, "›", lambda: self.shift_month(1), self.f_body, padx=S(11), pady=S(3)
             ).pack(side="left")
        for w in (outer, left, hdr, titles, self.title_lbl, self.month_sub):
            if w is not outer:
                self._bind_drag(w)

        # 달력 (캔버스 한 장에 직접 그린다)
        cw, ch, gap = self.cw, self.ch, self.gap
        self.cal_w = 7 * cw + 6 * gap + 2
        self.cal_h = self.wd_h + 6 * ch + 5 * gap
        self.cal = tk.Canvas(left, width=self.cal_w, height=self.cal_h, bg=BG,
                             highlightthickness=0, bd=0)
        self.cal.pack()
        self.cal.bind("<Motion>", self._cal_motion)
        self.cal.bind("<Leave>", self._cal_leave)
        self.cal.bind("<Button-1>", self._cal_click)
        self.cal.bind("<MouseWheel>", self._on_wheel_month)

        # 중요도 범례
        leg = tk.Frame(left, bg=BG)
        leg.pack(fill="x", pady=(S(8), 0))
        for name, col in PRIORITIES:
            tk.Label(leg, text="●", font=self.f_tiny, bg=BG, fg=col).pack(side="left")
            tk.Label(leg, text=name, font=self.f_tiny, bg=BG, fg=SUB).pack(side="left", padx=(S(2), S(10)))
        tk.Label(left, text=f"{self.HOTKEY_LABEL}: 어디서든 빠른 입력  ·  Ctrl+휠 또는 모서리 드래그: 크기 조절",
                 font=self.f_tiny, bg=BG, fg=DIM).pack(anchor="w", pady=(S(4), S(10)))

        # 오른쪽 상세 패널
        inner = tk.Frame(right, bg=SURFACE, padx=S(18), pady=S(10))
        inner.pack(fill="both", expand=True)
        tabs = tk.Frame(inner, bg=SURFACE)
        tabs.pack(fill="x", pady=(0, S(10)))
        self.tab_day = self._tab(tabs, "일정", lambda: self.set_mode("day"))
        self.tab_inbox = self._tab(tabs, "보관함", lambda: self.set_mode("inbox"))
        self.d_title = tk.Label(inner, font=self.f_h2, bg=SURFACE, fg=TEXT, anchor="w",
                                cursor="fleur")
        self.d_title.pack(fill="x")
        self.d_sub = tk.Label(inner, font=self.f_small, bg=SURFACE, fg=SUB, anchor="w",
                              wraplength=S(250), justify="left")
        self.d_sub.pack(fill="x", pady=(0, S(8)))
        for w in (right, inner, tabs, self.d_title, self.d_sub):
            self._bind_drag(w)

        # 진행률
        self.pr = tk.Frame(inner, bg=SURFACE)
        self.pr.pack(fill="x", pady=(0, S(8)))
        self.todo_count = tk.Label(self.pr, font=self.f_tiny, bg=SURFACE, fg=SUB)
        self.todo_count.pack(side="right")
        self.prog = tk.Canvas(self.pr, height=S(6), bg=SURFACE, highlightthickness=0, bd=0)
        self.prog.pack(side="left", fill="x", expand=True, padx=(0, S(8)), pady=S(4))
        self.prog.bind("<Configure>", lambda e: self._draw_progress())

        # 빠른 입력
        self.box = tk.Frame(inner, bg=BORDER, padx=1, pady=1)
        self.box.pack(fill="x")
        box_in = tk.Frame(self.box, bg=CARD)
        box_in.pack(fill="x")
        tk.Label(box_in, text="＋", font=self.f_body, bg=CARD, fg=ACCENT).pack(
            side="left", padx=(S(8), 0))
        self.todo_entry = tk.Entry(box_in, font=self.f_body, bg=CARD, fg=TEXT,
                                   insertbackground=TEXT, relief="flat", highlightthickness=0)
        self.todo_entry.pack(side="left", fill="x", expand=True, ipady=S(6), padx=S(6))
        self.todo_entry.bind("<Return>", lambda e: self.add_todo())
        self.todo_entry.bind("<Escape>", lambda e: self._escape_entry())
        self.todo_entry.bind("<Button-1>", lambda e: self.todo_entry.focus_force(), add="+")
        self.todo_entry.bind("<FocusIn>", lambda e: self.box.config(bg=ACCENT), add="+")
        self.todo_entry.bind("<FocusOut>", lambda e: self.box.config(bg=BORDER), add="+")
        for n in range(4):
            self.todo_entry.bind(f"<Control-Key-{n + 1}>", lambda e, n=n: self._pick_pri(n))
        self._placeholder(self.todo_entry, "")

        # 중요도 선택
        prow = tk.Frame(inner, bg=SURFACE)
        prow.pack(fill="x", pady=(S(8), S(6)))
        tk.Label(prow, text="중요도", font=self.f_tiny, bg=SURFACE, fg=SUB).pack(side="left")
        self.pri_dots = []
        for n, (name, col) in enumerate(PRIORITIES):
            cv = tk.Canvas(prow, width=S(20), height=S(20), bg=SURFACE, highlightthickness=0,
                           cursor="hand2")
            cv.pack(side="left", padx=(S(6) if n == 0 else 0, 0))
            cv.bind("<Button-1>", lambda e, n=n: self._pick_pri(n))
            self.pri_dots.append(cv)
        self.pri_name = tk.Label(prow, font=self.f_tiny, bg=SURFACE, fg=SUB)
        self.pri_name.pack(side="left", padx=S(6))
        self._draw_pri_picker()

        # 메모 (아래쪽 고정)
        self.memo_box = tk.Frame(inner, bg=SURFACE)
        tk.Label(self.memo_box, text="메모", font=self.f_head, bg=SURFACE, fg=SUB, anchor="w"
                 ).pack(fill="x", pady=(S(8), S(3)))
        self.memo = tk.Text(self.memo_box, height=4, font=self.f_body, bg=CARD, fg=TEXT,
                            insertbackground=TEXT, relief="flat", wrap="word",
                            padx=S(10), pady=S(8), undo=True, highlightthickness=1,
                            highlightbackground=BORDER, highlightcolor=ACCENT)
        self.memo.pack(fill="x")
        self.memo.bind("<KeyRelease>", lambda e: self._schedule_memo_save())
        self.memo.bind("<Button-1>", lambda e: self.memo.focus_force())
        self.memo_box.pack(side="bottom", fill="x")

        # 할 일 목록
        self.list_wrap = tk.Frame(inner, bg=SURFACE)
        self.list_wrap.pack(fill="both", expand=True)
        self.todo_canvas = tk.Canvas(self.list_wrap, bg=SURFACE, highlightthickness=0, bd=0)
        self.todo_canvas.pack(fill="both", expand=True)
        self.todo_inner = tk.Frame(self.todo_canvas, bg=SURFACE)
        win = self.todo_canvas.create_window((0, 0), window=self.todo_inner, anchor="nw")
        self.todo_inner.bind("<Configure>", lambda e: self.todo_canvas.configure(
            scrollregion=self.todo_canvas.bbox("all")))
        self.todo_canvas.bind("<Configure>", lambda e: self.todo_canvas.itemconfigure(
            win, width=e.width))
        self.todo_canvas.bind("<MouseWheel>", self._on_wheel_todo)

        # 크기 조절 모서리 (끌어서 크기 변경, 놓으면 적용)
        grip = tk.Canvas(outer, width=S(18), height=S(18), bg=SURFACE, highlightthickness=0, bd=0,
                         cursor="size_nw_se" if IS_WIN else "bottom_right_corner")
        for k in (5, 9, 13):
            grip.create_line(S(17), S(k), S(k), S(17), fill=DIM, width=1)
        grip.place(relx=1.0, rely=1.0, anchor="se")
        grip.bind("<ButtonPress-1>", self._grip_start)
        grip.bind("<B1-Motion>", self._grip_move)
        grip.bind("<ButtonRelease-1>", self._grip_end)
        self.size_hint = tk.Label(outer, font=self.f_body, bg=ACCENT, fg="#ffffff",
                                  padx=S(14), pady=S(6))
        self._apply_mode_layout()

    def _tab(self, parent, text, cmd):
        lb = tk.Label(parent, text=text, font=self.f_small, padx=self.S(12), pady=self.S(3),
                      cursor="hand2")
        lb.pack(side="left", padx=(0, self.S(6)))
        lb.bind("<Button-1>", lambda e: cmd())
        return lb

    def _build_menu(self):
        m = self.menu = tk.Menu(self.root, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                                activeforeground="#ffffff", bd=0, font=("Malgun Gothic" if IS_WIN else "NanumGothic", 10))
        mk = lambda: tk.Menu(m, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                             activeforeground="#ffffff", bd=0, font=m.cget("font"))
        m.add_command(label="공문 PDF 가져오기…", command=self.import_pdf)
        m.add_command(label="오늘로 이동", command=self.go_today)
        m.add_command(label=f"빠른 입력  ({self.HOTKEY_LABEL})", command=self.summon)
        m.add_separator()
        sz = mk()
        self.size_var = tk.DoubleVar(value=self.ui)
        for label, v in (("작게 (80%)", 0.8), ("조금 작게 (90%)", 0.9), ("보통 (100%)", 1.0),
                         ("조금 크게 (125%)", 1.25), ("크게 (150%)", 1.5), ("아주 크게 (180%)", 1.8)):
            sz.add_command(label=label, command=lambda v=v: self.set_ui_scale(v))
        m.add_cascade(label="크기", menu=sz)
        fm = mk()
        installed = set(tkfont.families())
        self.font_var = tk.StringVar(value=self.family)
        for f in FONT_CHOICES:
            if f in installed:
                fm.add_radiobutton(label=f, variable=self.font_var, value=f, font=(f, 10),
                                   command=lambda f=f: self.set_font(f))
        fm.add_separator()
        fm.add_command(label="다른 글꼴 찾기…", command=lambda: FontDialog(self))
        m.add_cascade(label="글꼴", menu=fm)
        al = mk()
        for v in (1.0, 0.97, 0.9, 0.8, 0.7, 0.6):
            al.add_radiobutton(label=f"{int(v * 100)}%", variable=self.alpha, value=v,
                               command=self._apply_alpha)
        m.add_cascade(label="투명도", menu=al)
        m.add_checkbutton(label="위치 고정", variable=self.locked, command=self._save_conf)
        if IS_WIN:
            m.add_checkbutton(label="Windows 시작 시 자동 실행", variable=self.autostart,
                              command=self._toggle_autostart)
            m.add_command(label="데이터 폴더 열기", command=lambda: os.startfile(DATA_DIR))
        m.add_separator()
        m.add_command(label="종료", command=self.quit)
        self.root.bind_all("<Button-3>", lambda e: m.tk_popup(e.x_root, e.y_root))

    # ── 작은 위젯 헬퍼 ──
    def _placeholder(self, entry, text):
        entry.ph_text = text

        def show(_=None):
            if not entry.get():
                entry.insert(0, entry.ph_text)
                entry.config(fg=DIM)
                entry.ph = True

        def hide(_=None):
            if getattr(entry, "ph", False):
                entry.delete(0, "end")
                entry.config(fg=TEXT)
                entry.ph = False

        entry.bind("<FocusIn>", hide, add="+")
        entry.bind("<FocusOut>", show, add="+")
        entry.show_ph = show

    def _set_placeholder(self, text):
        e = self.todo_entry
        if getattr(e, "ph", False):
            e.delete(0, "end")
            e.ph = False
        e.ph_text = text
        if self.root.focus_get() is not e:
            e.show_ph()

    def _bind_drag(self, w):
        w.bind("<ButtonPress-1>", self._drag_start, add="+")
        w.bind("<B1-Motion>", self._drag_move, add="+")
        w.bind("<ButtonRelease-1>", self._drag_end, add="+")

    def _fit(self, text, font, maxw):
        if font.measure(text) <= maxw:
            return text
        while text and font.measure(text + "…") > maxw:
            text = text[:-1]
        return text + "…"

    # ── 중요도 선택기 ──
    def _pick_pri(self, n):
        self.new_pri = n
        self._draw_pri_picker()
        return "break"

    def _draw_pri_picker(self):
        S = self.S
        for n, cv in enumerate(self.pri_dots):
            cv.delete("all")
            col = PRIORITIES[n][1]
            if n == self.new_pri:
                cv.create_oval(1, 1, S(19), S(19), outline=col, width=S(2))
            cv.create_oval(S(5), S(5), S(15), S(15), fill=col, outline="")
        self.pri_name.config(text=PRIORITIES[self.new_pri][0], fg=PRIORITIES[self.new_pri][1])

    # ── 달력 ──
    def render_month(self):
        self.title_lbl.config(text=f"{self.view_y}년 {self.view_m}월")
        dates = list(calendar.Calendar(firstweekday=6).itermonthdates(self.view_y, self.view_m))
        while len(dates) < 42:
            dates.append(dates[-1] + dt.timedelta(days=1))
        self.dates = dates
        remain = 0
        for d in dates:
            if d.month == self.view_m:
                remain += sum(1 for t in self.data.get(d.isoformat(), {}).get("todos", [])
                              if not t.get("done"))
        self.month_sub.config(text=f"남은 할 일 {remain}개" if remain else "남은 할 일 없음")
        self.draw_calendar()

    def draw_calendar(self):
        c = self.cal
        cw, ch, gap = self.cw, self.ch, self.gap
        c.delete("all")
        for col, name in enumerate("일월화수목금토"):
            fg = SUN if col == 0 else SAT if col == 6 else SUB
            c.create_text(col * (cw + gap) + cw / 2, self.wd_h / 2, text=name,
                          font=self.f_head, fill=fg)
        for i, d in enumerate(self.dates):
            x = (i % 7) * (cw + gap)
            y = self.wd_h + (i // 7) * (ch + gap)
            self._draw_cell(i, d, x, y)

    def _draw_cell(self, i, d, x, y):
        S, c = self.S, self.cal
        cw, ch = self.cw, self.ch
        in_month = d.month == self.view_m
        is_today, is_sel = d == self.today, d == self.selected and self.mode == "day"
        hover = i == self.hover_idx
        holiday = holiday_name(d)
        wd = (d.weekday() + 1) % 7  # 0 = 일요일

        fill = CARD_HOVER if hover else CARD if in_month else CARD_DIM
        if is_sel:
            rrect(c, x, y, x + cw, y + ch, S(10), fill=fill, outline=ACCENT, width=S(2))
        else:
            rrect(c, x, y, x + cw, y + ch, S(10), fill=fill, outline=BORDER if in_month else "")

        cx, cy = x + S(17), y + S(16)
        if not in_month:
            num_fg = DIM
        elif holiday or wd == 0:
            num_fg = SUN
        elif wd == 6:
            num_fg = SAT
        else:
            num_fg = TEXT
        if is_today:
            r = S(11)
            c.create_oval(cx - r, cy - r, cx + r, cy + r, fill=ACCENT, outline="")
            num_fg = "#ffffff"
        c.create_text(cx, cy, text=str(d.day), font=self.f_day, fill=num_fg)

        entry = self.data.get(d.isoformat(), {})
        if entry.get("memo", "").strip():
            c.create_oval(x + cw - S(11), y + S(11), x + cw - S(6), y + S(16),
                          fill=SUB if in_month else DIM, outline="")

        todos = entry.get("todos", [])
        rows = []
        if holiday:
            rows.append(("h", holiday))
        rows += [("t", todos[k]) for k in todo_order(todos)]
        if len(rows) > 2:
            rows = rows[:1] + [("m", len(rows) - 1)]
        yy = y + S(33)
        for kind, v in rows:
            x1, x2 = x + S(6), x + cw - S(6)
            if kind == "h":
                c.create_text(x1 + S(2), yy + S(7), anchor="w", font=self.f_tiny,
                              text=self._fit(v, self.f_tiny, x2 - x1 - S(4)),
                              fill=SUN if in_month else DIM)
            elif kind == "m":
                c.create_text(x1 + S(2), yy + S(7), anchor="w", font=self.f_tiny,
                              text=f"+{v}개 더", fill=SUB if in_month else DIM)
            else:
                done = v.get("done", False)
                col = DONE if done else pri_color(v)
                bgc = CARD if in_month else CARD_DIM
                rrect(c, x1, yy, x2, yy + S(15), S(4),
                      fill=blend(col, bgc, 0.10 if done else 0.20), outline="")
                c.create_rectangle(x1, yy + S(3), x1 + S(2), yy + S(12), fill=col, outline="")
                fnt = self.f_tiny_done if done else self.f_tiny
                c.create_text(x1 + S(6), yy + S(7.5), anchor="w", font=fnt,
                              text=self._fit(v.get("text", ""), fnt, x2 - x1 - S(10)),
                              fill=DIM if done else blend(col, "#ffffff", 0.6))
            yy += S(17)

    def _cell_at(self, x, y):
        cw, ch, gap = self.cw, self.ch, self.gap
        if y < self.wd_h:
            return None
        col, row = int(x // (cw + gap)), int((y - self.wd_h) // (ch + gap))
        if not (0 <= col < 7 and 0 <= row < 6):
            return None
        if x - col * (cw + gap) > cw or (y - self.wd_h) - row * (ch + gap) > ch:
            return None
        return row * 7 + col

    def _cal_motion(self, e):
        idx = self._cell_at(e.x, e.y)
        if idx != self.hover_idx:
            self.hover_idx = idx
            self.cal.config(cursor="hand2" if idx is not None else "")
            self.draw_calendar()

    def _cal_leave(self, e):
        if self.hover_idx is not None:
            self.hover_idx = None
            self.draw_calendar()

    def _cal_click(self, e):
        idx = self._cell_at(e.x, e.y)
        if idx is None:
            return
        d = self.dates[idx]
        if self.mode == "inbox":   # 보관함에서 날짜를 누르면 그 날짜가 '꺼낼 날짜'가 된다
            self.selected = d
            self._update_sub()
        else:
            self.show_day(d)
        if (d.year, d.month) != (self.view_y, self.view_m):
            self.view_y, self.view_m = d.year, d.month
        self.render_month()
        if self.mode == "inbox":
            self.render_todos()

    def shift_month(self, delta):
        m = self.view_m - 1 + delta
        self.view_y += m // 12
        self.view_m = m % 12 + 1
        self.render_month()

    def go_today(self):
        self.today = dt.date.today()
        self.view_y, self.view_m = self.today.year, self.today.month
        self.mode = "day"
        self._apply_mode_layout()
        self.show_day(self.today)
        self.render_month()

    def _on_wheel_month(self, e):
        if e.state & 0x4:   # Ctrl+휠은 크기 조절
            return
        self.shift_month(-1 if e.delta > 0 else 1)

    # ── 일정 / 보관함 전환 ──
    def set_mode(self, mode):
        if mode == self.mode:
            return
        self._flush_memo()
        self.mode = mode
        self._apply_mode_layout()
        self.show_day(self.selected)
        self.render_month()

    def _apply_mode_layout(self):
        day = self.mode == "day"
        for lb, on in ((self.tab_day, day), (self.tab_inbox, not day)):
            lb.config(bg=ACCENT_SOFT if on else SURFACE, fg=ACCENT if on else SUB)
        if day:
            self.pr.pack(before=self.box, fill="x", pady=(0, self.S(8)))
            self.memo_box.pack(side="bottom", fill="x", before=self.list_wrap)
        else:
            self.pr.pack_forget()
            self.memo_box.pack_forget()
        self._set_placeholder("할 일 추가   예) 14:30 팀 회의" if day
                              else "나중에 할 일을 적어 두세요")

    def _inbox(self, create=False):
        if create:
            return self.data.setdefault("_inbox", {"memo": "", "todos": []})
        return self.data.get("_inbox", {})

    # ── 상세 패널 ──
    def _entry(self, d, create=False):
        key = d.isoformat()
        if create:
            return self.data.setdefault(key, {"memo": "", "todos": []})
        return self.data.get(key, {})

    def _todos(self, create=False):
        src = self._inbox(create) if self.mode == "inbox" else self._entry(self.selected, create)
        return src.get("todos", [])

    def show_day(self, d):
        self._flush_memo()
        self.selected = d
        self._memo_date = d
        self._update_sub()
        self.memo.delete("1.0", "end")
        self.memo.insert("1.0", self._entry(d).get("memo", ""))
        self.memo.edit_reset()
        self.render_todos()
        self.todo_canvas.yview_moveto(0)

    def _update_sub(self):
        d = self.selected
        if self.mode == "inbox":
            self.d_title.config(text="보관함")
            self.d_sub.config(text=f"날짜 없이 적어 두는 곳 · '꺼내기'를 누르면 {d.month}월 {d.day}일로 옮겨요")
            return
        self.d_title.config(text=f"{d.month}월 {d.day}일")
        parts = [f"{d.year}년", f"{WEEKDAY_KO[d.weekday()]}요일"]
        if d == self.today:
            parts.append("오늘")
        h = holiday_name(d)
        if h:
            parts.append(h)
        self.d_sub.config(text=" · ".join(parts))

    def _schedule_memo_save(self):
        if self._save_job:
            self.root.after_cancel(self._save_job)
        self._save_job = self.root.after(600, self._flush_memo)

    def _flush_memo(self):
        if self._save_job:
            self.root.after_cancel(self._save_job)
            self._save_job = None
        if self._memo_date is None:
            return
        text = self.memo.get("1.0", "end-1c")
        if self._entry(self._memo_date).get("memo", "") != text:
            self._entry(self._memo_date, create=True)["memo"] = text
            self._commit()
            self.draw_calendar()

    def _draw_progress(self):
        p = self.prog
        p.delete("all")
        w, h = p.winfo_width(), p.winfo_height()
        if w < 4:
            return
        todos = self._entry(self.selected).get("todos", [])
        rrect(p, 0, 0, w, h, h / 2, fill=BORDER, outline="")
        done = sum(1 for t in todos if t.get("done"))
        if todos and done:
            rrect(p, 0, 0, max(h, w * done / len(todos)), h, h / 2, fill=ACCENT, outline="")

    def _check_box(self, parent, done, color, cmd):
        S = self.S
        n = S(20)
        cv = tk.Canvas(parent, width=n, height=n, bg=SURFACE, highlightthickness=0, bd=0,
                       cursor="hand2")
        if done:
            cv.create_oval(2, 2, n - 2, n - 2, fill=DONE, outline="")
            cv.create_line(n * .30, n * .52, n * .45, n * .67, n * .72, n * .36,
                           fill="#ffffff", width=S(2), capstyle="round", joinstyle="round")
        else:
            ring = cv.create_oval(2, 2, n - 2, n - 2, outline=color, width=S(2))
            cv.bind("<Enter>", lambda e: cv.itemconfig(ring, fill=blend(color, SURFACE, 0.25)))
            cv.bind("<Leave>", lambda e: cv.itemconfig(ring, fill=""))
        cv.bind("<Button-1>", lambda e: cmd())
        cv.bind("<MouseWheel>", self._on_wheel_todo)
        return cv

    def render_todos(self):
        S = self.S
        for w in self.todo_inner.winfo_children():
            w.destroy()
        todos = self._todos()
        inbox = self.mode == "inbox"
        left_n = sum(1 for t in self._inbox().get("todos", []) if not t.get("done"))
        self.tab_inbox.config(text=f"보관함 {left_n}" if left_n else "보관함")
        done_n = sum(1 for t in todos if t.get("done"))
        self.todo_count.config(text=f"{done_n}/{len(todos)}" if todos else "")
        self._draw_progress()
        if not todos:
            msg = ("보관함이 비어 있어요\n나중에 할 일을 적어 두면 여기 쌓여요" if inbox
                   else "등록된 할 일이 없어요\n위 입력창에 적고 Enter")
            lbl = tk.Label(self.todo_inner, text=msg, font=self.f_small, bg=SURFACE, fg=DIM,
                           anchor="w", justify="left")
            lbl.pack(fill="x", pady=S(6))
            lbl.bind("<MouseWheel>", self._on_wheel_todo)
            return
        for idx in todo_order(todos):
            t = todos[idx]
            done = t.get("done", False)
            col = pri_color(t)
            row = tk.Frame(self.todo_inner, bg=SURFACE)
            row.pack(fill="x", pady=S(3))
            chk = self._check_box(row, done, col, lambda i=idx: self.toggle_todo(i))
            chk.pack(side="left", anchor="n", pady=S(1))
            dele = tk.Label(row, text="✕", font=self.f_tiny, bg=SURFACE, fg=DIM, cursor="hand2")
            dele.pack(side="right", anchor="n", padx=(S(4), 0), pady=S(3))
            dot = tk.Canvas(row, width=S(14), height=S(14), bg=SURFACE, highlightthickness=0,
                            cursor="hand2")
            dot.create_oval(S(2), S(2), S(12), S(12), fill=DONE if done else col, outline="")
            dot.pack(side="right", anchor="n", padx=(S(4), 0), pady=S(3))
            dot.bind("<Button-1>", lambda e, i=idx: self.cycle_pri(i))
            body = tk.Frame(row, bg=SURFACE)
            body.pack(side="left", fill="x", expand=True, padx=(S(8), 0))
            if t.get("time") and not inbox:
                tk.Label(body, text=t["time"], font=self.f_tiny, bg=SURFACE,
                         fg=DIM if done else "#ffb547", anchor="w").pack(fill="x")
            txt = tk.Label(body, text=t.get("text", ""), font=self.f_done if done else self.f_body,
                           bg=SURFACE, fg=DONE if done else TEXT, anchor="w", justify="left",
                           wraplength=S(150))
            txt.pack(fill="x")
            if inbox:
                pull = tk.Label(body, text=f"꺼내기 → {self.selected.month}/{self.selected.day}",
                                font=self.f_tiny, bg=ACCENT_SOFT, fg=ACCENT, cursor="hand2",
                                padx=S(6), pady=S(1))
                pull.pack(anchor="w", pady=(S(3), 0))
                pull.bind("<Button-1>", lambda e, i=idx: self.pull_from_inbox(i))
            dele.bind("<Button-1>", lambda e, i=idx: self.delete_todo(i))
            dele.bind("<Enter>", lambda e, w=dele: w.config(fg=SUN))
            dele.bind("<Leave>", lambda e, w=dele: w.config(fg=DIM))
            txt.bind("<Double-Button-1>", lambda e, i=idx, w=txt: self._edit_todo(i, w))
            for w in (row, body, txt, dele, dot, chk):
                w.bind("<MouseWheel>", self._on_wheel_todo)
                w.bind("<Button-3>", lambda e, i=idx: self._row_menu(e, i))

    # ── 할 일 조작 ──
    def add_todo(self):
        if getattr(self.todo_entry, "ph", False):
            return
        text = self.todo_entry.get().strip()
        if not text:
            return
        item = {"text": text, "done": False, "pri": self.new_pri}
        m = TIME_RE.match(text)
        if self.mode == "day" and m and int(m.group(1)) < 24 and int(m.group(2)) < 60:
            item.update(text=m.group(3).strip(), time=f"{int(m.group(1)):02d}:{m.group(2)}")
        if self.mode == "inbox":
            self._inbox(create=True)["todos"].append(item)
        else:
            self._entry(self.selected, create=True)["todos"].append(item)
        self.todo_entry.delete(0, "end")
        self._pick_pri(DEFAULT_PRI)
        self._after_todo_change()

    def toggle_todo(self, i):
        t = self._todos()[i]
        t["done"] = not t.get("done", False)
        self._after_todo_change()

    def cycle_pri(self, i):
        t = self._todos()[i]
        t["pri"] = (pri_of(t) - 1) % len(PRIORITIES)
        self._after_todo_change()

    def set_pri(self, i, p):
        self._todos()[i]["pri"] = p
        self._after_todo_change()

    def delete_todo(self, i):
        del self._todos()[i]
        self._after_todo_change()

    def to_inbox(self, i):
        t = self._todos().pop(i)
        t.pop("time", None)
        t["done"] = False
        self._inbox(create=True)["todos"].append(t)
        self._after_todo_change()

    def pull_from_inbox(self, i):
        t = self._inbox()["todos"].pop(i)
        self._entry(self.selected, create=True)["todos"].append(t)
        self._after_todo_change()

    def postpone(self, i):
        t = self._todos().pop(i)
        nxt = self.selected + dt.timedelta(days=1)
        self._entry(nxt, create=True)["todos"].append(t)
        self._after_todo_change()

    def _row_menu(self, e, i):
        t = self._todos()[i]
        m = tk.Menu(self.root, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                    activeforeground="#ffffff", bd=0, font=self.f_small)
        if self.mode == "inbox":
            m.add_command(label=f"{self.selected.month}월 {self.selected.day}일로 꺼내기",
                          command=lambda: self.pull_from_inbox(i))
        else:
            m.add_command(label="내일로 미루기", command=lambda: self.postpone(i))
            m.add_command(label="보관함으로 보내기", command=lambda: self.to_inbox(i))
        m.add_separator()
        for p, (name, _) in enumerate(PRIORITIES):
            m.add_command(label=("✓ " if pri_of(t) == p else "    ") + f"● {name}",
                          command=lambda p=p: self.set_pri(i, p))
        m.add_separator()
        m.add_command(label="삭제", command=lambda: self.delete_todo(i))
        m.tk_popup(e.x_root, e.y_root)
        return "break"

    def _edit_todo(self, i, label):
        t = self._todos()[i]
        e = tk.Entry(label.master, font=self.f_body, bg=CARD, fg=TEXT,
                     insertbackground=TEXT, relief="flat")
        e.insert(0, t.get("text", ""))
        label.pack_forget()
        e.pack(fill="x", ipady=self.S(3))
        e.focus_force()
        e.select_range(0, "end")
        state = {"closed": False}

        def finish(save=True):
            if state["closed"]:
                return
            state["closed"] = True
            v = e.get().strip()
            if save and v and v != t.get("text"):
                t["text"] = v
                self._commit()
            self.root.after_idle(self._after_todo_change)

        e.bind("<Return>", lambda ev: finish())
        e.bind("<Escape>", lambda ev: finish(False))
        e.bind("<FocusOut>", lambda ev: finish())

    def _after_todo_change(self):
        self._commit()
        self.render_todos()
        self.render_month()

    def _on_wheel_todo(self, e):
        if e.state & 0x4:
            return
        c = self.todo_canvas
        if self.todo_inner.winfo_height() > c.winfo_height():
            c.yview_scroll(int(-e.delta / 120), "units")

    def _escape_entry(self):
        self.todo_entry.delete(0, "end")
        self.root.focus_set()
        self.send_to_bottom()

    # ── 공문 PDF ──
    def import_pdf(self):
        path = filedialog.askopenfilename(parent=self.root, title="공문 PDF 선택",
                                          filetypes=[("PDF 파일", "*.pdf"), ("모든 파일", "*.*")])
        if not path:
            return
        try:
            text = gongmun.extract_text(path)
        except Exception as ex:
            messagebox.showerror("PDF 읽기 실패", f"파일을 읽지 못했어요.\n{ex}", parent=self.root)
            return
        if len(text.strip()) < 20:
            messagebox.showinfo("글자를 읽을 수 없어요",
                                "이 PDF에는 읽을 수 있는 글자가 거의 없어요.\n"
                                "스캔(이미지) 파일이면 지원하지 않아요. 글자가 선택되는 PDF를 사용해 주세요.",
                                parent=self.root)
            return
        title, items = gongmun.parse(text, self.today)
        ImportDialog(self, title, items)

    # ── 저장 ──
    def _commit(self):
        for k in list(self.data):
            v = self.data[k]
            if not v.get("memo", "").strip() and not v.get("todos"):
                del self.data[k]
        try:
            save_json(DATA_FILE, self.data)
        except Exception:
            pass

    def _save_conf(self):
        self.conf["locked"] = self.locked.get()
        self.conf["alpha"] = self.alpha.get()
        try:
            save_json(CONF_FILE, self.conf)
        except Exception:
            pass

    # ── 창 이동 / 크기 / 메뉴 동작 ──
    def _drag_start(self, e):
        if self.locked.get():
            return
        self._drag = (e.x_root - self.root.winfo_x(), e.y_root - self.root.winfo_y())

    def _drag_move(self, e):
        if self._drag:
            self.root.geometry(f"+{e.x_root - self._drag[0]}+{e.y_root - self._drag[1]}")

    def _drag_end(self, e):
        if self._drag:
            self._drag = None
            self.conf["x"], self.conf["y"] = self.root.winfo_x(), self.root.winfo_y()
            self._save_conf()

    def _grip_start(self, e):
        self._grip = (e.x_root, e.y_root, self.outer.winfo_width(), self.outer.winfo_height(),
                      self.ui)

    def _grip_target(self, e):
        x0, y0, w0, h0, ui0 = self._grip
        f = 1 + ((e.x_root - x0) / w0 + (e.y_root - y0) / h0) / 2
        return round(min(2.0, max(0.7, ui0 * f)), 2)

    def _grip_move(self, e):
        if self._grip:
            self.size_hint.config(text=f"크기 {int(self._grip_target(e) * 100)}%")
            self.size_hint.place(relx=0.5, rely=0.5, anchor="center")

    def _grip_end(self, e):
        if not self._grip:
            return
        target = self._grip_target(e)
        self._grip = None
        self.size_hint.place_forget()
        self.root.after_idle(lambda: self.set_ui_scale(target))

    def _apply_alpha(self):
        self.root.attributes("-alpha", self.alpha.get())
        self._save_conf()

    def _toggle_autostart(self):
        try:
            set_autostart(self.autostart.get())
        except Exception:
            pass
        self.autostart.set(get_autostart())

    def quit(self):
        self._flush_memo()
        self._save_conf()
        self.root.destroy()

    # ── 바탕화면 고정 / 빠른 입력 ──
    def _apply_desktop_mode(self):
        if not IS_WIN:
            return
        try:
            u = ctypes.windll.user32
            self.root.update_idletasks()
            hwnd = u.GetParent(self.root.winfo_id()) or self.root.winfo_id()
            self.hwnd = hwnd
            # 작업표시줄 / Alt+Tab 에서 숨김
            ex = u.GetWindowLongW(hwnd, -20)
            u.SetWindowLongW(hwnd, -20, (ex | 0x00000080) & ~0x00040000)
            # 바탕화면(Progman)을 소유자로 지정 → Win+D 눌러도 남아 있음
            progman = u.FindWindowW("Progman", None)
            if progman:
                setp = getattr(u, "SetWindowLongPtrW", None) or u.SetWindowLongW
                setp.argtypes = [wintypes.HWND, ctypes.c_int, ctypes.c_void_p]
                setp(hwnd, -8, progman)
            # Windows 11 둥근 모서리 (DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND)
            try:
                pref = ctypes.c_int(2)
                ctypes.windll.dwmapi.DwmSetWindowAttribute(
                    hwnd, 33, ctypes.byref(pref), ctypes.sizeof(pref))
            except Exception:
                pass
            self.send_to_bottom()
        except Exception:
            pass

    def send_to_bottom(self):
        if IS_WIN and self.hwnd:
            # HWND_BOTTOM, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE
            ctypes.windll.user32.SetWindowPos(self.hwnd, 1, 0, 0, 0, 0, 0x0013)

    def _bottom_if_inactive(self):
        try:
            focused = self.root.focus_get()
        except Exception:
            focused = None
        if focused is None:
            self.send_to_bottom()

    def _poll_hotkey(self):
        try:
            while True:
                self.hotkey_q.get_nowait()
                self.summon()
        except queue.Empty:
            pass
        self.root.after(150, self._poll_hotkey)

    def summon(self):
        """위젯을 맨 앞으로 불러와 오늘 날짜의 입력창에 포커스"""
        self.go_today()
        if IS_WIN and self.hwnd:
            u = ctypes.windll.user32
            u.SetWindowPos(self.hwnd, 0, 0, 0, 0, 0, 0x0043)  # HWND_TOP, NOSIZE|NOMOVE|SHOWWINDOW
            u.keybd_event(0x12, 0, 0, 0)  # Alt 키 한 번 눌러 포그라운드 잠금 해제
            u.keybd_event(0x12, 0, 2, 0)
            u.SetForegroundWindow(self.hwnd)
        self.root.focus_force()
        self.todo_entry.focus_force()

    def _tick(self):
        now = dt.date.today()
        if now != self.today:  # 자정이 지나면 오늘 표시 갱신
            old, self.today = self.today, now
            if self.selected == old:
                self.go_today()
            else:
                self._update_sub()
                self.render_month()
        self.root.after(30_000, self._tick)


def main():
    if IS_WIN:
        if not ensure_single_instance():
            return
        enable_dpi_awareness()
    os.makedirs(DATA_DIR, exist_ok=True)
    root = tk.Tk()
    DeskCalendar(root)
    root.mainloop()


if __name__ == "__main__":
    main()
