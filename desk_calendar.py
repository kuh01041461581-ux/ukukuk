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
from tkinter import colorchooser, filedialog, messagebox

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
PRIORITIES_DEFAULT = list(PRIORITIES)
CHIP_MIX = "#ffffff"    # 칩 글자색을 섞을 색 (밝은 테마에서는 검정)

THEMES = {
    "그래파이트": dict(BG="#14151a", SURFACE="#1a1b22", CARD="#212230", CARD_DIM="#191a22",
                    CARD_HOVER="#2a2c3d", BORDER="#2b2d3b", TEXT="#ececf3", SUB="#a1a4b8",
                    DIM="#5e6178", ACCENT="#7c6cff", ACCENT_SOFT="#2c2850", DONE="#5e6178",
                    SUN="#ff6b81", SAT="#5db8ff", CHIP_MIX="#ffffff"),
    "미드나잇": dict(BG="#0d1424", SURFACE="#121b30", CARD="#192540", CARD_DIM="#111a2e",
                  CARD_HOVER="#223256", BORDER="#233153", TEXT="#e8eefc", SUB="#9fb0d4",
                  DIM="#586a90", ACCENT="#4cc9f0", ACCENT_SOFT="#16384d", DONE="#586a90",
                  SUN="#ff6b81", SAT="#7ab8ff", CHIP_MIX="#ffffff"),
    "포레스트": dict(BG="#0f1713", SURFACE="#14201a", CARD="#1a2b22", CARD_DIM="#121e18",
                  CARD_HOVER="#223a2d", BORDER="#223629", TEXT="#e8f3ec", SUB="#9bb8a6",
                  DIM="#587064", ACCENT="#3ddc97", ACCENT_SOFT="#16402f", DONE="#587064",
                  SUN="#ff7a8a", SAT="#5db8ff", CHIP_MIX="#ffffff"),
    "로즈": dict(BG="#191216", SURFACE="#20171c", CARD="#2a1e25", CARD_DIM="#1d151a",
              CARD_HOVER="#38272f", BORDER="#37262f", TEXT="#f6eaf0", SUB="#c3a6b4",
              DIM="#76596a", ACCENT="#ff6b9d", ACCENT_SOFT="#45202f", DONE="#76596a",
              SUN="#ff6b81", SAT="#7fb4ff", CHIP_MIX="#ffffff"),
    "라이트": dict(BG="#f4f4f8", SURFACE="#ffffff", CARD="#ffffff", CARD_DIM="#ececf2",
                CARD_HOVER="#eae7fb", BORDER="#dcdce6", TEXT="#1f2030", SUB="#5d6075",
                DIM="#9a9db0", ACCENT="#6c5ce7", ACCENT_SOFT="#e6e3fb", DONE="#9a9db0",
                SUN="#e5484d", SAT="#2f7de1", CHIP_MIX="#000000"),
}
ACCENT_PRESETS = ["#7c6cff", "#4cc9f0", "#3ddc97", "#ff6b9d", "#ffb547", "#ff5c7a"]

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


def apply_conf_theme(conf):
    """설정(테마·강조색·글자색·중요도 색)을 모듈 전역 색상에 반영"""
    g = globals()
    g.update(THEMES.get(conf.get("theme"), THEMES["그래파이트"]))
    if conf.get("accent"):
        g["ACCENT"] = conf["accent"]
        g["ACCENT_SOFT"] = blend(conf["accent"], g["BG"], 0.22)
    if conf.get("text_color"):
        g["TEXT"] = conf["text_color"]
    pc = conf.get("pri_colors") or []
    g["PRIORITIES"] = [(n, pc[i] if i < len(pc) else c) for i, (n, c) in enumerate(PRIORITIES_DEFAULT)]


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


def item_end(t, start):
    """기간 일정의 종료일 (하루짜리면 None)"""
    e = t.get("end")
    if not e:
        return None
    try:
        d = dt.date.fromisoformat(e)
    except ValueError:
        return None
    return d if d > start else None


def sort_key(t):
    return (bool(t.get("done")), pri_of(t), t.get("time") or "99:99")


class Pill(tk.Canvas):
    """둥근 알약형 버튼"""

    def __init__(self, parent, text, cmd, font, fg=None, bg=None, hover=None,
                 parent_bg=None, padx=12, pady=5, r=8):
        fg, bg = fg or TEXT, bg or CARD
        hover, parent_bg = hover or CARD_HOVER, parent_bg or BG
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
        msg = ("날짜가 들어 있는 항목을 찾았어요. 추가할 항목만 체크하고, 날짜(시작 ~ 종료)·내용을 고친 뒤 추가하세요."
               if items else "날짜가 있는 항목을 찾지 못했어요. 아래 '＋ 행 추가'로 직접 넣을 수 있어요.")
        tk.Label(top, text=msg, font=app.f_tiny, bg=SURFACE, fg=SUB, anchor="w").pack(
            fill="x", padx=S(20), pady=(S(4), S(8)))

        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=S(20), pady=S(14))
        self.count = tk.Label(foot, font=app.f_small, bg=SURFACE, fg=SUB)
        self.count.pack(side="left")
        Pill(foot, "추가", self.confirm, app.f_body, fg="#ffffff", bg=ACCENT, hover=blend(ACCENT, "#ffffff", 0.25),
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
        it = it or {"date": app.selected, "end": None, "time": "", "text": "", "pri": DEFAULT_PRI}
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
        tk.Label(row, text="~", font=app.f_small, bg=SURFACE, fg=DIM).pack(side="left", padx=(0, S(6)))
        en = entry(11, it["end"].isoformat() if it.get("end") else "")
        t = entry(6, it.get("time", ""))
        x = entry(10, it["text"], expand=True)
        r = {"var": var, "date": d, "end": en, "time": t, "text": x, "pri": it.get("pri", DEFAULT_PRI)}
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
            end = None
            if r["end"].get().strip():
                try:
                    end = dt.date.fromisoformat(r["end"].get().strip())
                except ValueError:
                    messagebox.showwarning("날짜 확인", "종료일은 2026-10-24 처럼 적어 주세요. (하루 일정이면 비워 두세요)",
                                           parent=self.top)
                    return
            tm = r["time"].get().strip()
            m = re.fullmatch(r"(\d{1,2}):(\d{2})", tm)
            tm = f"{int(m.group(1)):02d}:{m.group(2)}" if m else ""
            picked.append((d, {"text": text, "done": False, "pri": r["pri"],
                               **({"time": tm} if tm else {}),
                               **({"end": end.isoformat()} if end and end > d else {})}))
        for d, item in picked:
            app._entry(d, create=True)["todos"].append(item)
        if picked:
            app._after_todo_change()
        self.top.destroy()
        if picked:
            messagebox.showinfo("가져오기 완료", f"{len(picked)}개 일정을 추가했어요.", parent=app.root)


class FontDialog:
    """모든 설치된 글꼴을 검색해서 고르는 창"""

    def __init__(self, app, on_done=None):
        self.app, S = app, app.S
        self.on_done = on_done
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
        Pill(top, "적용", self.apply, app.f_body, fg="#ffffff", bg=ACCENT, hover=blend(ACCENT, "#ffffff", 0.25),
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
            if self.on_done:
                self.on_done()


class SettingsDialog:
    """글자 크기·글꼴·색상 테마·강조색·중요도 색·투명도를 바꾸는 창 (바꾸는 즉시 적용)"""

    def __init__(self, app):
        self.app = app
        S = app.S
        self.top = _dialog(app, "화면 설정", S(500), S(720))
        self.build()

    def build(self):
        app, top = self.app, self.top
        S = app.S
        for w in top.winfo_children():
            w.destroy()
        top.configure(bg=SURFACE)
        pad = S(22)

        def section(title):
            tk.Label(top, text=title, font=app.f_head, bg=SURFACE, fg=SUB, anchor="w").pack(
                fill="x", padx=pad, pady=(S(14), S(6)))

        def row():
            f = tk.Frame(top, bg=SURFACE)
            f.pack(fill="x", padx=pad)
            return f

        def pill(parent, text, cmd, **kw):
            p = Pill(parent, text, cmd, app.f_small, parent_bg=SURFACE, padx=S(12), pady=S(4), **kw)
            p.pack(side="left", padx=(0, S(6)))
            return p

        tk.Label(top, text="화면 설정", font=app.f_h2, bg=SURFACE, fg=TEXT, anchor="w").pack(
            fill="x", padx=pad, pady=(S(16), 0))

        section("글자")
        r = row()
        tk.Label(r, text="크기", font=app.f_small, bg=SURFACE, fg=TEXT, width=8, anchor="w").pack(side="left")
        pill(r, "－", lambda: self.act(app.set_ui_scale, app.ui - 0.1))
        tk.Label(r, text=f"{int(round(app.ui * 100))}%", font=app.f_body, bg=SURFACE, fg=TEXT,
                 width=5).pack(side="left")
        pill(r, "＋", lambda: self.act(app.set_ui_scale, app.ui + 0.1))
        pill(r, "기본", lambda: self.act(app.set_ui_scale, 1.0))
        r = row()
        r.pack_configure(pady=(S(8), 0))
        tk.Label(r, text="글꼴", font=app.f_small, bg=SURFACE, fg=TEXT, width=8, anchor="w").pack(side="left")
        tk.Label(r, text=app.family, font=app.f_body, bg=SURFACE, fg=ACCENT).pack(side="left", padx=(0, S(10)))
        pill(r, "변경…", lambda: FontDialog(app, on_done=self.build))
        r = row()
        r.pack_configure(pady=(S(8), 0))
        tk.Label(r, text="글자 색", font=app.f_small, bg=SURFACE, fg=TEXT, width=8, anchor="w").pack(side="left")
        self.swatch(r, TEXT, S(26))
        pill(r, "선택…", lambda: self.pick("text_color", TEXT))
        pill(r, "기본", lambda: self.act(app.update_appearance, text_color=None))

        section("색상 테마")
        r = row()
        for name, th in THEMES.items():
            self.theme_card(r, name, th)

        section("강조색")
        r = row()
        for col in ACCENT_PRESETS:
            c = tk.Canvas(r, width=S(28), height=S(28), bg=SURFACE, highlightthickness=0, cursor="hand2")
            c.create_oval(S(3), S(3), S(25), S(25), fill=col, outline=TEXT if col == ACCENT else "",
                          width=S(2))
            c.pack(side="left", padx=(0, S(6)))
            c.bind("<Button-1>", lambda e, col=col: self.act(app.update_appearance, accent=col))
        pill(r, "직접 선택…", lambda: self.pick("accent", ACCENT))

        section("중요도 색")
        for i, (name, col) in enumerate(PRIORITIES):
            r = row()
            r.pack_configure(pady=(0, S(4)))
            self.swatch(r, col, S(22))
            tk.Label(r, text=name, font=app.f_small, bg=SURFACE, fg=TEXT, width=9, anchor="w").pack(side="left")
            pill(r, "색 변경…", lambda i=i, col=col: self.pick_pri(i, col))

        section("투명도")
        r = row()
        sc = tk.Scale(r, from_=40, to=100, orient="horizontal", length=S(300), showvalue=True,
                      bg=SURFACE, fg=TEXT, troughcolor=CARD, highlightthickness=0, bd=0,
                      activebackground=ACCENT, command=lambda v: app.set_alpha(int(v) / 100))
        sc.set(int(round(app.alpha.get() * 100)))
        sc.pack(side="left")

        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=pad, pady=S(16))
        Pill(foot, "닫기", top.destroy, app.f_body, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(22), pady=S(6)
             ).pack(side="right")
        Pill(foot, "모두 기본값으로", lambda: self.act(app.reset_appearance), app.f_small,
             parent_bg=SURFACE, padx=S(14), pady=S(6)).pack(side="left")

    def act(self, fn, *a, **kw):
        fn(*a, **kw)
        self.build()

    def swatch(self, parent, col, size):
        c = tk.Canvas(parent, width=size, height=size, bg=SURFACE, highlightthickness=0)
        rrect(c, 1, 1, size - 1, size - 1, self.app.S(6), fill=col, outline=BORDER)
        c.pack(side="left", padx=(0, self.app.S(10)))

    def theme_card(self, parent, name, th):
        S = self.app.S
        w, h = S(84), S(58)
        c = tk.Canvas(parent, width=w, height=h, bg=SURFACE, highlightthickness=0, cursor="hand2")
        cur = self.app.conf.get("theme", "그래파이트") == name
        rrect(c, 1, 1, w - 1, h - 1, S(8), fill=th["BG"], outline=ACCENT if cur else th["BORDER"],
              width=S(2) if cur else 1)
        rrect(c, S(8), S(8), w - S(8), S(30), S(5), fill=th["CARD"], outline="")
        c.create_oval(S(12), S(14), S(20), S(22), fill=th["ACCENT"], outline="")
        c.create_text(w / 2, h - S(14), text=name, font=self.app.f_tiny, fill=th["TEXT"])
        c.pack(side="left", padx=(0, S(6)))
        c.bind("<Button-1>", lambda e: self.act(self.app.update_appearance, theme=name,
                                                accent=None, text_color=None))

    def pick(self, key, cur):
        _, hexv = colorchooser.askcolor(color=cur, parent=self.top, title="색 선택")
        if hexv:
            self.act(self.app.update_appearance, **{key: hexv})

    def pick_pri(self, i, cur):
        _, hexv = colorchooser.askcolor(color=cur, parent=self.top, title="중요도 색 선택")
        if hexv:
            cols = [c for _, c in PRIORITIES]
            cols[i] = hexv
            self.act(self.app.update_appearance, pri_colors=cols)


class HelpDialog:
    TEXT_LINES = [
        ("달력 보기", "평소에는 달력만 바탕화면에 떠 있어요. 달력 가운데에서 원하는 곳으로 끌어 옮길 수 있어요."),
        ("일정 입력", "날짜를 클릭하면 오른쪽에 입력창이 열려요. 쓰고 Enter. 같은 날짜를 다시 누르면 닫혀요."),
        ("시간 넣기", "'14:30 팀 회의' 처럼 앞에 시간을 쓰면 시간이 함께 저장돼요."),
        ("기간 일정", "시작 날짜에서 끝 날짜까지 끌어서 선택한 뒤 입력하면 여러 날에 걸친 일정이 돼요."),
        ("완료 / 삭제", "동그라미를 누르면 완료(취소선). ✕ 는 삭제, 목록에서 우클릭하면 더 많은 메뉴가 나와요."),
        ("중요도 색", "입력창 아래 색 점으로 중요도를 고르면 달력에도 그 색으로 보여요."),
        ("보관함", "날짜를 정하기 전의 할 일을 적어 뒀다가 '꺼내기'로 원하는 날짜에 옮겨요."),
        ("공문 PDF", "'공문 PDF' 버튼으로 PDF를 고르면 날짜가 있는 항목을 찾아 일정으로 넣어 줘요."),
        ("창 크기", "창 가장자리나 모서리에 마우스를 가져가면 양방향 화살표가 나와요. 끌면 창 크기가 바뀌어요."),
        ("글자·색", "위쪽 'Aa' 에서 글자 크기, 글꼴, 색상 테마, 강조색, 중요도 색, 투명도를 바꿔요."),
        ("빠른 입력", "Ctrl+Alt+Space 를 누르면 어디서든 오늘 날짜 입력창이 바로 열려요."),
    ]

    def __init__(self, app):
        S = app.S
        top = _dialog(app, "사용법", S(520), S(640))
        tk.Label(top, text="사용법", font=app.f_h2, bg=SURFACE, fg=TEXT, anchor="w").pack(
            fill="x", padx=S(24), pady=(S(18), S(8)))
        for head, body in self.TEXT_LINES:
            f = tk.Frame(top, bg=SURFACE)
            f.pack(fill="x", padx=S(24), pady=S(4))
            tk.Label(f, text=head, font=app.f_head, bg=SURFACE, fg=ACCENT, width=9, anchor="nw"
                     ).pack(side="left", anchor="n")
            tk.Label(f, text=body, font=app.f_small, bg=SURFACE, fg=TEXT, anchor="w",
                     justify="left", wraplength=S(380)).pack(side="left", fill="x")
        Pill(top, "확인", top.destroy, app.f_body, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(26), pady=S(6)
             ).pack(side="bottom", pady=S(18))


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
        apply_conf_theme(self.conf)
        self.dpi_scale = max(1.0, root.winfo_fpixels("1i") / 96.0)
        self.ui = min(2.0, max(0.7, float(self.conf.get("scale", 1.0))))
        self.scale = self.dpi_scale * self.ui
        self.panel_w = self.S(290) + 1
        self.win_w = int(self.conf.get("w", self.S(660)))   # 입력 패널을 뺀 창 너비
        self.win_h = int(self.conf.get("h", self.S(720)))
        self.nslot = 3
        self.menu = None
        installed = set(tkfont.families())
        fam = self.conf.get("font", DEFAULT_FONT)
        self.family = fam if fam in installed else DEFAULT_FONT

        self.today = dt.date.today()
        self.view_y, self.view_m = self.today.year, self.today.month
        self.selected = self.today
        self.mode = "day"          # "day" | "inbox"
        self.panel_open = False    # 오른쪽 입력 패널 (날짜를 누르면 열림)
        self.sel_end = None        # 기간 선택의 종료일 (시작일은 self.selected)
        self._shift = 0
        self._press_idx = None
        self._drag_range = None
        self.lay = {"bars": [], "singles": {}, "occ": {}, "hidden": {}}
        self.new_pri = DEFAULT_PRI
        self.dates = []
        self.hover_idx = None
        self._memo_date = None
        self._save_job = None
        self._scale_job = None
        self._drag = None
        self._rs = None            # 창 크기 조절 상태
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
        if not self.conf.get("seen_help"):
            root.after(900, self._first_help)
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
        self.panel_w = self.S(290) + 1
        mw, mh = self._min_size()
        self.win_w, self.win_h = max(mw, self.win_w), max(mh, self.win_h)
        self._build_ui()
        self._geom()
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

    def _min_size(self):
        return self.S(520), self.S(560)

    def _geom(self, x=None, y=None):
        r = self.root
        w = self.win_w + (self.panel_w if self.panel_open else 0)
        x = r.winfo_x() if x is None else x
        y = r.winfo_y() if y is None else y
        r.geometry(f"{w}x{self.win_h}+{x}+{y}")

    def _setup_window(self):
        r = self.root
        r.title(APP_NAME)
        r.overrideredirect(True)
        r.configure(bg=BG)
        r.attributes("-alpha", self.alpha.get())
        vx, vy, vw, vh = virtual_screen(r)
        mw, mh = self._min_size()
        self.win_w, self.win_h = max(mw, self.win_w), max(mh, self.win_h)
        default_x = r.winfo_screenwidth() - self.win_w - self.S(40)
        x = self.conf.get("x", default_x)
        y = self.conf.get("y", self.S(40))
        x = min(max(vx, x), vx + vw - self.S(120))
        y = min(max(vy, y), vy + vh - self.S(120))
        self._geom(x, y)
        r.bind("<FocusOut>", lambda e: r.after(150, self._bottom_if_inactive), add="+")
        # 창 가장자리(바깥 여백)에서 크기 조절
        r.bind("<Motion>", self._on_root_motion, add="+")
        r.bind("<ButtonPress-1>", self._resize_start, add="+")
        r.bind("<B1-Motion>", self._resize_move, add="+")
        r.bind("<ButtonRelease-1>", self._resize_end, add="+")

    # ── 창 크기 조절 (윈도우 창처럼 가장자리·모서리를 끌기) ──
    _CURSORS_WIN = {"n": "size_ns", "s": "size_ns", "e": "size_we", "w": "size_we",
                    "ne": "size_ne_sw", "sw": "size_ne_sw", "nw": "size_nw_se", "se": "size_nw_se"}
    _CURSORS_X11 = {"n": "sb_v_double_arrow", "s": "sb_v_double_arrow",
                    "e": "sb_h_double_arrow", "w": "sb_h_double_arrow",
                    "ne": "top_right_corner", "sw": "bottom_left_corner",
                    "nw": "top_left_corner", "se": "bottom_right_corner"}

    def _zone(self, e):
        r = self.root
        if e.widget is not r:
            return ""
        w, h = r.winfo_width(), r.winfo_height()
        m, c = self.S(8), self.S(22)
        nx = "w" if e.x < m else "e" if e.x >= w - m else ""
        ny = "n" if e.y < m else "s" if e.y >= h - m else ""
        if nx and e.y < c:
            ny = "n"
        if nx and e.y >= h - c:
            ny = "s"
        if ny and e.x < c:
            nx = "w"
        if ny and e.x >= w - c:
            nx = "e"
        return ny + nx

    def _on_root_motion(self, e):
        if self._rs:
            return
        z = self._zone(e)
        cur = (self._CURSORS_WIN if IS_WIN else self._CURSORS_X11).get(z, "")
        if self.root.cget("cursor") != cur:
            self.root.config(cursor=cur)

    def _resize_start(self, e):
        z = self._zone(e)
        if not z or self.locked.get():
            return
        r = self.root
        self._rs = (z, r.winfo_x(), r.winfo_y(), e.x_root, e.y_root, r.winfo_width(), r.winfo_height())

    def _resize_move(self, e):
        if not self._rs:
            return
        z, x0, y0, rx, ry, w0, h0 = self._rs
        dx, dy = e.x_root - rx, e.y_root - ry
        pw = self.panel_w if self.panel_open else 0
        mw, mh = self._min_size()
        minw = mw + pw
        x, y, w, h = x0, y0, w0, h0
        if "e" in z:
            w = max(minw, w0 + dx)
        if "s" in z:
            h = max(mh, h0 + dy)
        if "w" in z:
            w = max(minw, w0 - dx)
            x = x0 + (w0 - w)
        if "n" in z:
            h = max(mh, h0 - dy)
            y = y0 + (h0 - h)
        self.win_w, self.win_h = w - pw, h
        self.root.geometry(f"{w}x{h}+{x}+{y}")

    def _resize_end(self, e):
        if not self._rs:
            return
        self._rs = None
        self._shift = 0
        self.conf.update(w=self.win_w, h=self.win_h, x=self.root.winfo_x(), y=self.root.winfo_y())
        self._save_conf()

    def _on_cal_resize(self, e):
        """창 크기에 맞춰 날짜 칸 크기를 다시 계산"""
        S, gap = self.S, self.gap
        self.cw = max(S(44), (e.width - 6 * gap - 2) // 7)
        self.ch = max(S(46), (e.height - self.wd_h - 5 * gap) // 6)
        self.nslot = max(1, min(6, (self.ch - S(32)) // S(16)))
        self.render_month()

    # ── UI 구성 ──
    def _build_ui(self):
        if self.outer is not None:
            self.outer.destroy()
        S = self.S
        self.cw, self.ch, self.gap = S(78), S(80), S(5)
        self.wd_h = S(26)

        outer = self.outer = tk.Frame(self.root, bg=BG)
        outer.pack(fill="both", expand=True, padx=S(8), pady=S(8))

        # 맨 위 이동 막대 (잡고 끌면 창 이동) + 설정 버튼
        bar = tk.Frame(outer, bg=BG, height=S(24))
        bar.pack(side="top", fill="x")
        bar.pack_propagate(False)
        grab = tk.Label(bar, text="⋯⋯⋯", font=self.f_tiny, bg=BG, fg=DIM, cursor="fleur")
        grab.pack(expand=True)
        tools = tk.Frame(bar, bg=BG)
        tools.place(relx=1.0, x=-S(2), rely=0.5, anchor="e")

        def tool(text, cmd):
            lb = tk.Label(tools, text=text, font=self.f_tiny, bg=BG, fg=SUB, cursor="hand2",
                          padx=S(7))
            lb.pack(side="left")
            lb.bind("<Button-1>", cmd)
            lb.bind("<Enter>", lambda e: lb.config(fg=ACCENT))
            lb.bind("<Leave>", lambda e: lb.config(fg=SUB))

        tool("Aa 화면 설정", lambda e: self.open_settings())
        tool("? 사용법", lambda e: self.open_help())
        tool("⚙", lambda e: self.menu.tk_popup(e.x_root, e.y_root))
        for w in (bar, grab):
            self._bind_drag(w)

        self.right = tk.Frame(outer, bg=SURFACE, width=S(290))
        self.right.pack_propagate(False)
        self.sep = tk.Frame(outer, bg=BORDER, width=1)
        left = self.left = tk.Frame(outer, bg=BG, padx=S(20), pady=S(4))
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
        Pill(nav, "보관함", self.open_inbox, self.f_small, padx=S(11), pady=S(4)
             ).pack(side="left", padx=(0, S(6)))
        Pill(nav, "공문 PDF", self.import_pdf, self.f_small, padx=S(11), pady=S(4)
             ).pack(side="left", padx=(0, S(10)))
        Pill(nav, "‹", lambda: self.shift_month(-1), self.f_body, padx=S(11), pady=S(3)
             ).pack(side="left")
        Pill(nav, "오늘", self.go_today, self.f_small, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), padx=S(13), pady=S(4)).pack(side="left", padx=S(5))
        Pill(nav, "›", lambda: self.shift_month(1), self.f_body, padx=S(11), pady=S(3)
             ).pack(side="left")
        for w in (left, hdr, titles, self.title_lbl, self.month_sub):
            self._bind_drag(w)

        # 아래쪽 안내 (먼저 배치해 두어야 창이 작아져도 달력이 이 영역을 침범하지 않는다)
        tk.Label(left, text="날짜 클릭: 입력창  ·  날짜 끌기: 기간 일정  ·  창 가장자리 끌기: 크기",
                 font=self.f_tiny, bg=BG, fg=DIM).pack(side="bottom", anchor="w", pady=(S(4), S(6)))
        leg = tk.Frame(left, bg=BG)
        leg.pack(side="bottom", fill="x", pady=(S(8), 0))
        for name, col in PRIORITIES:
            tk.Label(leg, text="●", font=self.f_tiny, bg=BG, fg=col).pack(side="left")
            tk.Label(leg, text=name, font=self.f_tiny, bg=BG, fg=SUB).pack(side="left", padx=(S(2), S(10)))

        # 달력 (캔버스 한 장에 직접 그린다 / 창 크기에 맞춰 칸이 늘어남)
        self.cal = tk.Canvas(left, width=1, height=1, bg=BG, highlightthickness=0, bd=0)
        self.cal.pack(fill="both", expand=True)
        self.cal.bind("<Configure>", self._on_cal_resize)
        self.cal.bind("<Motion>", self._cal_motion)
        self.cal.bind("<Leave>", self._cal_leave)
        self.cal.bind("<ButtonPress-1>", self._cal_press)
        self.cal.bind("<B1-Motion>", self._cal_drag)
        self.cal.bind("<ButtonRelease-1>", self._cal_release)
        self.cal.bind("<MouseWheel>", self._on_wheel_month)

        # 오른쪽 상세 패널
        inner = tk.Frame(self.right, bg=SURFACE, padx=S(18), pady=S(10))
        inner.pack(fill="both", expand=True)
        tabs = tk.Frame(inner, bg=SURFACE)
        tabs.pack(fill="x", pady=(0, S(10)))
        self.tab_day = self._tab(tabs, "일정", lambda: self.set_mode("day"))
        self.tab_inbox = self._tab(tabs, "보관함", lambda: self.set_mode("inbox"))
        close = tk.Label(tabs, text="✕", font=self.f_body, bg=SURFACE, fg=DIM, cursor="hand2")
        close.pack(side="right")
        close.bind("<Button-1>", lambda e: self.set_panel(False))
        close.bind("<Enter>", lambda e: close.config(fg=TEXT))
        close.bind("<Leave>", lambda e: close.config(fg=DIM))
        self.d_title = tk.Label(inner, font=self.f_h2, bg=SURFACE, fg=TEXT, anchor="w",
                                cursor="fleur")
        self.d_title.pack(fill="x")
        self.d_sub = tk.Label(inner, font=self.f_small, bg=SURFACE, fg=SUB, anchor="w",
                              wraplength=S(250), justify="left")
        self.d_sub.pack(fill="x", pady=(0, S(8)))
        self.range_lbl = tk.Label(inner, text="✕ 기간 해제 (하루 일정으로)", font=self.f_tiny,
                                  bg=ACCENT_SOFT, fg=ACCENT, cursor="hand2", padx=S(8), pady=S(2))
        self.range_lbl.bind("<Button-1>", lambda e: self.clear_range())
        for w in (self.right, inner, tabs, self.d_title, self.d_sub):
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

        self._apply_mode_layout()
        self._apply_panel_visibility()

    def _tab(self, parent, text, cmd):
        lb = tk.Label(parent, text=text, font=self.f_small, padx=self.S(12), pady=self.S(3),
                      cursor="hand2")
        lb.pack(side="left", padx=(0, self.S(6)))
        lb.bind("<Button-1>", lambda e: cmd())
        return lb

    def _build_menu(self):
        if self.menu is not None:
            self.menu.destroy()
        f = (self.family, 10)
        m = self.menu = tk.Menu(self.root, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                                activeforeground="#ffffff", bd=0, font=f)
        m.add_command(label="화면 설정 (글자·색상)…", command=self.open_settings)
        m.add_command(label="사용법", command=self.open_help)
        m.add_separator()
        m.add_command(label="공문 PDF 가져오기…", command=self.import_pdf)
        m.add_command(label="오늘로 이동", command=self.go_today)
        m.add_command(label=f"빠른 입력  ({self.HOTKEY_LABEL})", command=self.summon)
        m.add_separator()
        m.add_checkbutton(label="위치·크기 고정", variable=self.locked, command=self._save_conf)
        if IS_WIN:
            m.add_checkbutton(label="Windows 시작 시 자동 실행", variable=self.autostart,
                              command=self._toggle_autostart)
            m.add_command(label="데이터 폴더 열기", command=lambda: os.startfile(DATA_DIR))
        m.add_separator()
        m.add_command(label="종료", command=self.quit)
        self.root.bind_all("<Button-3>", lambda e: m.tk_popup(e.x_root, e.y_root))

    def open_settings(self):
        SettingsDialog(self)

    def open_help(self):
        HelpDialog(self)

    def _first_help(self):
        self.conf["seen_help"] = True
        self._save_conf()
        HelpDialog(self)

    def set_alpha(self, v):
        self.alpha.set(v)
        self.root.attributes("-alpha", v)
        self._save_conf()

    def update_appearance(self, **kw):
        for k, v in kw.items():
            if v is None:
                self.conf.pop(k, None)
            else:
                self.conf[k] = v
        self._save_conf()
        self._refresh_theme()

    def reset_appearance(self):
        for k in ("theme", "accent", "text_color", "pri_colors", "font", "scale"):
            self.conf.pop(k, None)
        self.family = DEFAULT_FONT
        self.ui = 1.0
        self.scale = self.dpi_scale
        self.panel_w = self.S(290) + 1
        self._apply_fonts()
        self._save_conf()
        self._refresh_theme()

    def _refresh_theme(self):
        apply_conf_theme(self.conf)
        self.root.configure(bg=BG)
        self._build_menu()
        self.rebuild()

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
        self._build_layout()
        remain = 0
        for d in dates:
            if d.month == self.view_m:
                remain += sum(1 for t in self.lay["singles"].get(d, []) if not t.get("done"))
        for b in self.lay["bars"]:
            if not b["t"].get("done") and b["first"]:
                remain += 1
        self.month_sub.config(text=f"남은 할 일 {remain}개" if remain else "남은 할 일 없음")
        self.draw_calendar()

    def _build_layout(self):
        """보이는 6주 안의 일정을 칸(슬롯)에 배치: 기간 일정은 주마다 이어진 막대, 하루 일정은 남는 칸"""
        dates = self.dates
        first, last = dates[0], dates[-1]
        singles, spans = {}, []
        for key, v in self.data.items():
            if key == "_inbox":
                continue
            try:
                s0 = dt.date.fromisoformat(key)
            except ValueError:
                continue
            for t in v.get("todos", []):
                e0 = item_end(t, s0)
                if e0:
                    if s0 <= last and e0 >= first:
                        spans.append((s0, e0, t))
                elif first <= s0 <= last:
                    singles.setdefault(s0, []).append(t)
        for lst in singles.values():
            lst.sort(key=sort_key)
        spans.sort(key=lambda x: (x[0], -(x[1] - x[0]).days))
        occ, hidden, bars = {}, {}, []
        for s0, e0, t in spans:
            i1, i2 = max(0, (s0 - first).days), min(41, (e0 - first).days)
            i = i1
            while i <= i2:
                row = i // 7
                j = min(i2, row * 7 + 6)
                slot = next((k for k in range(self.nslot)
                             if all(k not in occ.get(dates[x], ()) for x in range(i, j + 1))), None)
                if slot is None:
                    for x in range(i, j + 1):
                        hidden[dates[x]] = hidden.get(dates[x], 0) + 1
                else:
                    for x in range(i, j + 1):
                        occ.setdefault(dates[x], set()).add(slot)
                    bars.append({"row": row, "c1": i % 7, "c2": j % 7, "slot": slot, "t": t,
                                 "left": s0 >= first and i == (s0 - first).days,
                                 "right": e0 <= last and j == (e0 - first).days,
                                 "first": s0 >= first and i == (s0 - first).days})
                i = j + 1
        self.lay = {"bars": bars, "singles": singles, "occ": occ, "hidden": hidden}

    def draw_calendar(self):
        S, c = self.S, self.cal
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
        for b in self.lay["bars"]:
            self._draw_bar(b)

    def _sel_range(self):
        if self._drag_range:
            return self._drag_range
        if self.panel_open and self.mode == "day":
            return self.selected, self.sel_end or self.selected
        return None

    def _draw_chip(self, x1, x2, yy, t, in_month):
        S, c = self.S, self.cal
        done = t.get("done", False)
        col = DONE if done else pri_color(t)
        bgc = CARD if in_month else CARD_DIM
        rrect(c, x1, yy, x2, yy + S(14), S(4),
              fill=blend(col, bgc, 0.10 if done else 0.20), outline="")
        c.create_rectangle(x1, yy + S(3), x1 + S(2), yy + S(11), fill=col, outline="")
        fnt = self.f_tiny_done if done else self.f_tiny
        c.create_text(x1 + S(6), yy + S(7), anchor="w", font=fnt,
                      text=self._fit(t.get("text", ""), fnt, x2 - x1 - S(10)),
                      fill=DIM if done else blend(col, CHIP_MIX, 0.55 if CHIP_MIX == "#000000" else 0.6))

    def _draw_bar(self, b):
        """기간 일정: 여러 날짜 칸을 가로로 이어 붙인 막대"""
        S, c = self.S, self.cal
        cw, ch, gap = self.cw, self.ch, self.gap
        t = b["t"]
        done = t.get("done", False)
        col = DONE if done else pri_color(t)
        fill = blend(col, CARD, 0.30 if not done else 0.14)
        x1 = b["c1"] * (cw + gap) + S(3)
        x2 = b["c2"] * (cw + gap) + cw - S(3)
        y = self.wd_h + b["row"] * (ch + gap) + S(30) + b["slot"] * S(16)
        rrect(c, x1, y, x2, y + S(14), S(4), fill=fill, outline="")
        if not b["left"]:   # 앞 주에서 이어지는 막대는 왼쪽을 평평하게
            c.create_rectangle(x1, y, x1 + S(6), y + S(14), fill=fill, outline="")
        else:
            c.create_rectangle(x1, y + S(3), x1 + S(2), y + S(11), fill=col, outline="")
        if not b["right"]:
            c.create_rectangle(x2 - S(6), y, x2, y + S(14), fill=fill, outline="")
        fnt = self.f_tiny_done if done else self.f_tiny
        c.create_text(x1 + S(6), y + S(7), anchor="w", font=fnt,
                      text=self._fit(t.get("text", ""), fnt, x2 - x1 - S(10)),
                      fill=DIM if done else blend(col, CHIP_MIX, 0.6 if CHIP_MIX == "#000000" else 0.7))

    def _draw_cell(self, i, d, x, y):
        S, c = self.S, self.cal
        cw, ch = self.cw, self.ch
        in_month = d.month == self.view_m
        is_today = d == self.today
        rng = self._sel_range()
        in_sel = bool(rng) and rng[0] <= d <= rng[1]
        hover = i == self.hover_idx
        holiday = holiday_name(d)
        wd = (d.weekday() + 1) % 7  # 0 = 일요일

        fill = CARD_HOVER if hover else CARD if in_month else CARD_DIM
        if in_sel:
            fill = blend(ACCENT, CARD, 0.18)
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

        # 막대가 차지하지 않은 칸에 공휴일 이름 / 하루 일정을 채운다
        occ = self.lay["occ"].get(d, set())
        free = [k for k in range(self.nslot) if k not in occ]
        rows = []
        if holiday:
            rows.append(("h", holiday))
        rows += [("t", t) for t in self.lay["singles"].get(d, [])]
        extra = self.lay["hidden"].get(d, 0)
        if len(rows) + extra > len(free):
            keep = max(0, len(free) - 1)
            rows = rows[:keep] + [("m", len(rows) - keep + extra)]
        x1, x2 = x + S(6), x + cw - S(6)
        for slot, (kind, v) in zip(free, rows):
            yy = y + S(30) + slot * S(16)
            if kind == "h":
                c.create_text(x1 + S(2), yy + S(7), anchor="w", font=self.f_tiny,
                              text=self._fit(v, self.f_tiny, x2 - x1 - S(4)),
                              fill=SUN if in_month else DIM)
            elif kind == "m":
                c.create_text(x1 + S(2), yy + S(7), anchor="w", font=self.f_tiny,
                              text=f"+{v}개 더", fill=SUB if in_month else DIM)
            else:
                self._draw_chip(x1, x2, yy, v, in_month)

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

    def _cal_press(self, e):
        self._press_idx = self._cell_at(e.x, e.y)
        self._drag_range = None

    def _cal_drag(self, e):
        if self._press_idx is None:
            return
        idx = self._cell_at(e.x, e.y)
        if idx is None or idx == self._press_idx and not self._drag_range:
            return
        a, b = sorted((self.dates[self._press_idx], self.dates[idx]))
        if self._drag_range != (a, b):
            self._drag_range = (a, b)
            self.draw_calendar()

    def _cal_leave(self, e):
        if self.hover_idx is not None:
            self.hover_idx = None
            self.draw_calendar()

    def _cal_release(self, e):
        idx = self._cell_at(e.x, e.y)
        press, rng = self._press_idx, self._drag_range
        self._press_idx, self._drag_range = None, None
        if press is None:
            return
        if rng and rng[0] != rng[1]:
            self.select_date(rng[0], rng[1])
        elif idx == press:
            self.select_date(self.dates[idx])
        else:
            self.draw_calendar()

    def shift_month(self, delta):
        m = self.view_m - 1 + delta
        self.view_y += m // 12
        self.view_m = m % 12 + 1
        self.render_month()

    def go_today(self):
        self.today = dt.date.today()
        self.view_y, self.view_m = self.today.year, self.today.month
        self.mode = "day"
        self.sel_end = None
        self._apply_mode_layout()
        self.show_day(self.today)
        self.render_month()

    def _on_wheel_month(self, e):
        if e.state & 0x4:   # Ctrl+휠은 크기 조절
            return
        self.shift_month(-1 if e.delta > 0 else 1)

    # ── 입력 패널 열기 / 닫기 ──
    def _apply_panel_visibility(self):
        if self.panel_open:
            self.right.pack(side="right", fill="y", before=self.left)
            self.sep.pack(side="right", fill="y", before=self.left)
        else:
            self.right.pack_forget()
            self.sep.pack_forget()

    def set_panel(self, open_):
        if open_ == self.panel_open:
            return
        self._flush_memo()
        self.panel_open = open_
        if not open_:
            self.mode = "day"
            self.sel_end = None
            self._apply_mode_layout()
            self._update_sub()
        self._apply_panel_visibility()
        r = self.root
        vx, vy, vw, vh = virtual_screen(r)
        x = r.winfo_x()
        w = self.win_w + (self.panel_w if open_ else 0)
        if open_:   # 화면 오른쪽 밖으로 나가면 그만큼 왼쪽으로 밀었다가 닫을 때 되돌린다
            over = x + w - (vx + vw)
            if over > 0:
                self._shift = over
                x -= over
        elif self._shift:
            x += self._shift
            self._shift = 0
        self._geom(x, r.winfo_y())
        self.render_month()

    def open_inbox(self):
        if self.panel_open and self.mode == "inbox":
            self.set_panel(False)
            return
        self.set_mode("inbox")
        self.set_panel(True)
        self.todo_entry.focus_force()

    def select_date(self, start, end=None):
        """날짜(또는 기간)를 고르고 입력 패널을 연다. 같은 날짜를 다시 누르면 패널을 닫는다."""
        end = end if end and end > start else None
        if (self.panel_open and self.mode == "day" and start == self.selected
                and end is None and self.sel_end is None):
            self.set_panel(False)
            return
        self.sel_end = end
        if (start.year, start.month) != (self.view_y, self.view_m):
            self.view_y, self.view_m = start.year, start.month
        if self.mode == "inbox":      # 보관함에서는 '꺼낼 날짜'만 바뀐다
            self.selected = start
            self._update_sub()
            self.render_todos()
        else:
            self.show_day(start)
        self.set_panel(True)
        self.render_month()
        self.todo_entry.focus_force()

    def clear_range(self):
        self.sel_end = None
        self._update_sub()
        self.render_month()

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

    def _rows(self):
        """(소유 리스트, 항목, 시작일) 목록. 일정 탭: 그날 항목 + 그날을 지나는 기간 일정 / 보관함 탭: 보관함"""
        if self.mode == "inbox":
            lst = self._inbox().get("todos", [])
            rows = [(lst, t, None) for t in lst]
        else:
            lst = self._entry(self.selected).get("todos", [])
            rows = [(lst, t, self.selected) for t in lst]
            for key, v in self.data.items():
                if key == "_inbox":
                    continue
                try:
                    s0 = dt.date.fromisoformat(key)
                except ValueError:
                    continue
                if s0 >= self.selected:
                    continue
                for t in v.get("todos", []):
                    e0 = item_end(t, s0)
                    if e0 and e0 >= self.selected:
                        rows.append((v["todos"], t, s0))
        rows.sort(key=lambda r: sort_key(r[1]))
        return rows

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
        d, e = self.selected, self.sel_end
        span = ""
        if e:
            span = f"{d.month}/{d.day} ~ {e.month}/{e.day}"
        if self.mode == "inbox":
            self.d_title.config(text="보관함")
            self.d_sub.config(text="날짜 없이 적어 두는 곳 · '꺼내기'를 누르면 "
                              + (f"{span} 기간으로 옮겨요" if e else f"{d.month}월 {d.day}일로 옮겨요"))
        elif e:
            tail = f"{e.day}일" if e.month == d.month else f"{e.month}월 {e.day}일"
            self.d_title.config(text=f"{d.month}월 {d.day}일 ~ {tail}")
            self.d_sub.config(text=f"{(e - d).days + 1}일간 · 아래에 입력하면 기간 일정으로 저장돼요")
        else:
            self.d_title.config(text=f"{d.month}월 {d.day}일")
            parts = [f"{d.year}년", f"{WEEKDAY_KO[d.weekday()]}요일"]
            if d == self.today:
                parts.append("오늘")
            h = holiday_name(d)
            if h:
                parts.append(h)
            self.d_sub.config(text=" · ".join(parts))
        if e:
            self.range_lbl.pack(after=self.d_sub, anchor="w", pady=(0, self.S(8)))
        else:
            self.range_lbl.pack_forget()

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
        rows = self._rows()
        rrect(p, 0, 0, w, h, h / 2, fill=BORDER, outline="")
        done = sum(1 for _, t, _ in rows if t.get("done"))
        if rows and done:
            rrect(p, 0, 0, max(h, w * done / len(rows)), h, h / 2, fill=ACCENT, outline="")

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
        rows = self._rows()
        inbox = self.mode == "inbox"
        left_n = sum(1 for t in self._inbox().get("todos", []) if not t.get("done"))
        self.tab_inbox.config(text=f"보관함 {left_n}" if left_n else "보관함")
        done_n = sum(1 for _, t, _ in rows if t.get("done"))
        self.todo_count.config(text=f"{done_n}/{len(rows)}" if rows else "")
        self._draw_progress()
        if not rows:
            msg = ("보관함이 비어 있어요\n나중에 할 일을 적어 두면 여기 쌓여요" if inbox
                   else "등록된 할 일이 없어요\n위 입력창에 적고 Enter")
            lbl = tk.Label(self.todo_inner, text=msg, font=self.f_small, bg=SURFACE, fg=DIM,
                           anchor="w", justify="left")
            lbl.pack(fill="x", pady=S(6))
            lbl.bind("<MouseWheel>", self._on_wheel_todo)
            return
        for lst, t, start in rows:
            done = t.get("done", False)
            col = pri_color(t)
            row = tk.Frame(self.todo_inner, bg=SURFACE)
            row.pack(fill="x", pady=S(3))
            chk = self._check_box(row, done, col, lambda t=t: self.toggle_todo(t))
            chk.pack(side="left", anchor="n", pady=S(1))
            dele = tk.Label(row, text="✕", font=self.f_tiny, bg=SURFACE, fg=DIM, cursor="hand2")
            dele.pack(side="right", anchor="n", padx=(S(4), 0), pady=S(3))
            dot = tk.Canvas(row, width=S(14), height=S(14), bg=SURFACE, highlightthickness=0,
                            cursor="hand2")
            dot.create_oval(S(2), S(2), S(12), S(12), fill=DONE if done else col, outline="")
            dot.pack(side="right", anchor="n", padx=(S(4), 0), pady=S(3))
            dot.bind("<Button-1>", lambda e, t=t: self.cycle_pri(t))
            body = tk.Frame(row, bg=SURFACE)
            body.pack(side="left", fill="x", expand=True, padx=(S(8), 0))
            e0 = item_end(t, start) if start else None
            if e0:
                tk.Label(body, text=f"기간  {start.month}/{start.day} ~ {e0.month}/{e0.day}",
                         font=self.f_tiny, bg=SURFACE, fg=DIM if done else col, anchor="w"
                         ).pack(fill="x")
            elif t.get("time") and not inbox:
                tk.Label(body, text=t["time"], font=self.f_tiny, bg=SURFACE,
                         fg=DIM if done else PRIORITIES[1][1], anchor="w").pack(fill="x")
            txt = tk.Label(body, text=t.get("text", ""), font=self.f_done if done else self.f_body,
                           bg=SURFACE, fg=DONE if done else TEXT, anchor="w", justify="left",
                           wraplength=S(150))
            txt.pack(fill="x")
            if inbox:
                tgt = (f"{self.selected.month}/{self.selected.day}~{self.sel_end.month}/{self.sel_end.day}"
                       if self.sel_end else f"{self.selected.month}/{self.selected.day}")
                pull = tk.Label(body, text=f"꺼내기 → {tgt}", font=self.f_tiny, bg=ACCENT_SOFT,
                                fg=ACCENT, cursor="hand2", padx=S(6), pady=S(1))
                pull.pack(anchor="w", pady=(S(3), 0))
                pull.bind("<Button-1>", lambda e, t=t: self.pull_from_inbox(t))
            dele.bind("<Button-1>", lambda e, l=lst, t=t: self.delete_todo(l, t))
            dele.bind("<Enter>", lambda e, w=dele: w.config(fg=SUN))
            dele.bind("<Leave>", lambda e, w=dele: w.config(fg=DIM))
            txt.bind("<Double-Button-1>", lambda e, t=t, w=txt: self._edit_todo(t, w))
            for w in (row, body, txt, dele, dot, chk):
                w.bind("<MouseWheel>", self._on_wheel_todo)
                w.bind("<Button-3>", lambda e, l=lst, t=t, s0=start: self._row_menu(e, l, t, s0))

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
            if self.sel_end:
                item["end"] = self.sel_end.isoformat()
            self._entry(self.selected, create=True)["todos"].append(item)
        self.todo_entry.delete(0, "end")
        self._pick_pri(DEFAULT_PRI)
        if self.sel_end and self.mode == "day":   # 기간은 한 번 쓰고 풀어서 다음 입력은 하루 일정
            self.sel_end = None
            self._update_sub()
        self._after_todo_change()

    @staticmethod
    def _remove(lst, t):
        for k, x in enumerate(lst):
            if x is t:
                del lst[k]
                return

    def toggle_todo(self, t):
        t["done"] = not t.get("done", False)
        self._after_todo_change()

    def cycle_pri(self, t):
        t["pri"] = (pri_of(t) - 1) % len(PRIORITIES)
        self._after_todo_change()

    def set_pri(self, t, p):
        t["pri"] = p
        self._after_todo_change()

    def delete_todo(self, lst, t):
        self._remove(lst, t)
        self._after_todo_change()

    def to_inbox(self, lst, t):
        self._remove(lst, t)
        t.pop("time", None)
        t.pop("end", None)
        t["done"] = False
        self._inbox(create=True)["todos"].append(t)
        self._after_todo_change()

    def pull_from_inbox(self, t):
        self._remove(self._inbox()["todos"], t)
        if self.sel_end and self.sel_end > self.selected:
            t["end"] = self.sel_end.isoformat()
        self._entry(self.selected, create=True)["todos"].append(t)
        self._after_todo_change()

    def postpone(self, lst, t):
        self._remove(lst, t)
        nxt = self.selected + dt.timedelta(days=1)
        self._entry(nxt, create=True)["todos"].append(t)
        self._after_todo_change()

    def unspan(self, t):
        t.pop("end", None)
        self._after_todo_change()

    def _row_menu(self, e, lst, t, start):
        m = tk.Menu(self.root, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                    activeforeground="#ffffff", bd=0, font=self.f_small)
        if self.mode == "inbox":
            m.add_command(label="선택한 날짜로 꺼내기", command=lambda: self.pull_from_inbox(t))
        else:
            if item_end(t, start):
                m.add_command(label="하루 일정으로 바꾸기", command=lambda: self.unspan(t))
            elif start == self.selected:
                m.add_command(label="내일로 미루기", command=lambda: self.postpone(lst, t))
            m.add_command(label="보관함으로 보내기", command=lambda: self.to_inbox(lst, t))
        m.add_separator()
        for p, (name, _) in enumerate(PRIORITIES):
            m.add_command(label=("✓ " if pri_of(t) == p else "    ") + f"● {name}",
                          command=lambda p=p: self.set_pri(t, p))
        m.add_separator()
        m.add_command(label="삭제", command=lambda: self.delete_todo(lst, t))
        m.tk_popup(e.x_root, e.y_root)
        return "break"

    def _edit_todo(self, t, label):
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
        self.set_panel(False)
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
            self._shift = 0
            self.conf["x"], self.conf["y"] = self.root.winfo_x(), self.root.winfo_y()
            self._save_conf()

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
        self.set_panel(True)
        self.render_month()
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
