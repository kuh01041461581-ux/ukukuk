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
import zlib

APP_NAME = "DeskCalendar"
IS_WIN = sys.platform == "win32"
IS_FROZEN = getattr(sys, "frozen", False)

DATA_DIR = os.path.join(os.environ.get("APPDATA") or os.path.expanduser("~"), APP_NAME)
DATA_FILE = os.path.join(DATA_DIR, "data.json")
CONF_FILE = os.path.join(DATA_DIR, "config.json")

if IS_WIN:
    import ctypes
    from ctypes import wintypes

FONT = "Malgun Gothic" if IS_WIN else "NanumGothic"
FONT_NUM = "Segoe UI" if IS_WIN else "NanumGothic"

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
TAG_COLORS = ["#8b7bff", "#3ddc97", "#ffb547", "#ff7a90", "#4cc9f0"]  # 바이올렛·민트·앰버·코랄·시안

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


def tag_color(text):
    return TAG_COLORS[zlib.crc32(text.encode("utf-8")) % len(TAG_COLORS)]


def todo_order(todos):
    """표시 순서: 미완료(시간 빠른 순) → 완료. 반환값은 todos 의 인덱스 목록"""
    return sorted(range(len(todos)),
                  key=lambda i: (bool(todos[i].get("done")), todos[i].get("time") or "99:99", i))


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


# ── 앱 ──────────────────────────────────────────────────
class DeskCalendar:
    HOTKEY_LABEL = "Ctrl+Alt+Space"

    def __init__(self, root):
        self.root = root
        self.data = load_json(DATA_FILE, {})
        self.conf = load_json(CONF_FILE, {})
        self.scale = max(1.0, root.winfo_fpixels("1i") / 96.0)

        self.today = dt.date.today()
        self.view_y, self.view_m = self.today.year, self.today.month
        self.selected = self.today
        self.dates = []
        self.hover_idx = None
        self._memo_date = None
        self._save_job = None
        self._drag = None
        self.hwnd = None
        self.hotkey_q = queue.Queue()

        self.locked = tk.BooleanVar(value=self.conf.get("locked", False))
        self.alpha = tk.DoubleVar(value=self.conf.get("alpha", 0.97))
        self._init_autostart()
        self.autostart = tk.BooleanVar(value=get_autostart())

        self.f_title = tkfont.Font(family=FONT, size=17, weight="bold")
        self.f_h2 = tkfont.Font(family=FONT, size=14, weight="bold")
        self.f_head = tkfont.Font(family=FONT, size=9, weight="bold")
        self.f_day = tkfont.Font(family=FONT_NUM, size=10, weight="bold")
        self.f_tiny = tkfont.Font(family=FONT, size=8)
        self.f_small = tkfont.Font(family=FONT, size=9)
        self.f_body = tkfont.Font(family=FONT, size=10)
        self.f_done = tkfont.Font(family=FONT, size=10, overstrike=True)

        self._setup_window()
        self._build_ui()
        self._build_menu()
        self.show_day(self.selected)
        self.render_month()

        root.after(300, self._apply_desktop_mode)
        root.after(30_000, self._tick)
        if IS_WIN:
            start_hotkey_listener(self.hotkey_q, 0x0002 | 0x0001, 0x20)  # Ctrl+Alt+Space
            root.after(150, self._poll_hotkey)

    def S(self, v):
        return int(round(v * self.scale))

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
        S = self.S
        # 달력 셀 크기
        self.cw, self.ch, self.gap = S(78), S(68), S(5)
        self.wd_h = S(26)

        outer = tk.Frame(self.root, bg=BG)
        outer.pack(fill="both", expand=True, padx=1, pady=1)

        right = tk.Frame(outer, bg=SURFACE, width=S(290))
        right.pack(side="right", fill="y")
        right.pack_propagate(False)
        tk.Frame(outer, bg=BORDER, width=1).pack(side="right", fill="y")
        left = tk.Frame(outer, bg=BG, padx=S(20), pady=S(16))
        left.pack(side="left", fill="both", expand=True)

        # 헤더 (드래그로 이동)
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
        Pill(nav, "‹", lambda: self.shift_month(-1), self.f_body, padx=S(11), pady=S(3)
             ).pack(side="left")
        Pill(nav, "오늘", self.go_today, self.f_small, fg="#ffffff", bg=ACCENT,
             hover="#9185ff", padx=S(13), pady=S(4)).pack(side="left", padx=S(5))
        Pill(nav, "›", lambda: self.shift_month(1), self.f_body, padx=S(11), pady=S(3)
             ).pack(side="left")
        for w in (outer, left, hdr, titles, self.title_lbl, self.month_sub):
            self._bind_drag(w)

        # 달력 (캔버스 한 장에 직접 그린다)
        cw, ch, gap = self.cw, self.ch, self.gap
        self.cal_w = 7 * cw + 6 * gap
        self.cal_h = self.wd_h + 6 * ch + 5 * gap
        self.cal = tk.Canvas(left, width=self.cal_w, height=self.cal_h, bg=BG,
                             highlightthickness=0, bd=0)
        self.cal.pack()
        self.cal.bind("<Motion>", self._cal_motion)
        self.cal.bind("<Leave>", self._cal_leave)
        self.cal.bind("<Button-1>", self._cal_click)
        self.cal.bind("<MouseWheel>", self._on_wheel_month)

        tk.Label(left, text=f"{self.HOTKEY_LABEL} 로 어디서든 바로 입력", font=self.f_tiny,
                 bg=BG, fg=DIM).pack(anchor="w", pady=(S(8), 0))

        # 오른쪽 상세 패널
        pad = S(18)
        inner = tk.Frame(right, bg=SURFACE, padx=pad, pady=S(16))
        inner.pack(fill="both", expand=True)
        self.d_title = tk.Label(inner, font=self.f_h2, bg=SURFACE, fg=TEXT, anchor="w",
                                cursor="fleur")
        self.d_title.pack(fill="x")
        self.d_sub = tk.Label(inner, font=self.f_small, bg=SURFACE, fg=SUB, anchor="w")
        self.d_sub.pack(fill="x", pady=(0, S(10)))
        for w in (right, inner, self.d_title, self.d_sub):
            self._bind_drag(w)

        # 진행률
        pr = tk.Frame(inner, bg=SURFACE)
        pr.pack(fill="x", pady=(0, S(10)))
        self.todo_count = tk.Label(pr, font=self.f_tiny, bg=SURFACE, fg=SUB)
        self.todo_count.pack(side="right")
        self.prog = tk.Canvas(pr, height=S(6), bg=SURFACE, highlightthickness=0, bd=0)
        self.prog.pack(side="left", fill="x", expand=True, padx=(0, S(8)), pady=S(4))
        self.prog.bind("<Configure>", lambda e: self._draw_progress())

        # 빠른 입력
        box = tk.Frame(inner, bg=BORDER, padx=1, pady=1)
        box.pack(fill="x", pady=(0, S(8)))
        box_in = tk.Frame(box, bg=CARD)
        box_in.pack(fill="x")
        tk.Label(box_in, text="＋", font=self.f_body, bg=CARD, fg=ACCENT).pack(
            side="left", padx=(S(8), 0))
        self.todo_entry = tk.Entry(box_in, font=self.f_body, bg=CARD, fg=TEXT,
                                   insertbackground=TEXT, relief="flat", highlightthickness=0)
        self.todo_entry.pack(side="left", fill="x", expand=True, ipady=S(6), padx=S(6))
        tk.Label(box_in, text="↵", font=self.f_small, bg=CARD, fg=DIM).pack(
            side="right", padx=(0, S(8)))
        self.todo_entry.bind("<Return>", lambda e: self.add_todo())
        self.todo_entry.bind("<Escape>", lambda e: self._escape_entry())
        self.todo_entry.bind("<Button-1>", lambda e: self.todo_entry.focus_force(), add="+")
        self.todo_entry.bind("<FocusIn>", lambda e: box.config(bg=ACCENT), add="+")
        self.todo_entry.bind("<FocusOut>", lambda e: box.config(bg=BORDER), add="+")
        self._placeholder(self.todo_entry, "할 일 추가   예) 14:30 팀 회의", CARD)

        # 메모 (아래쪽 고정)
        self.memo = tk.Text(inner, height=4, font=self.f_body, bg=CARD, fg=TEXT,
                            insertbackground=TEXT, relief="flat", wrap="word",
                            padx=S(10), pady=S(8), undo=True, highlightthickness=1,
                            highlightbackground=BORDER, highlightcolor=ACCENT)
        self.memo.pack(side="bottom", fill="x", pady=(S(4), 0))
        self.memo.bind("<KeyRelease>", lambda e: self._schedule_memo_save())
        self.memo.bind("<Button-1>", lambda e: self.memo.focus_force())
        tk.Label(inner, text="메모", font=self.f_head, bg=SURFACE, fg=SUB, anchor="w").pack(
            side="bottom", fill="x", pady=(S(8), 0))

        # 할 일 목록
        wrap = tk.Frame(inner, bg=SURFACE)
        wrap.pack(fill="both", expand=True)
        self.todo_canvas = tk.Canvas(wrap, bg=SURFACE, highlightthickness=0, bd=0)
        self.todo_canvas.pack(fill="both", expand=True)
        self.todo_inner = tk.Frame(self.todo_canvas, bg=SURFACE)
        win = self.todo_canvas.create_window((0, 0), window=self.todo_inner, anchor="nw")
        self.todo_inner.bind("<Configure>", lambda e: self.todo_canvas.configure(
            scrollregion=self.todo_canvas.bbox("all")))
        self.todo_canvas.bind("<Configure>", lambda e: self.todo_canvas.itemconfigure(
            win, width=e.width))
        self.todo_canvas.bind("<MouseWheel>", self._on_wheel_todo)

    def _build_menu(self):
        m = tk.Menu(self.root, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                    activeforeground="#ffffff", bd=0, font=self.f_small)
        m.add_command(label="오늘로 이동", command=self.go_today)
        m.add_command(label=f"빠른 입력  ({self.HOTKEY_LABEL})", command=self.summon)
        m.add_separator()
        m.add_checkbutton(label="위치 고정", variable=self.locked, command=self._save_conf)
        sub = tk.Menu(m, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                      activeforeground="#ffffff", bd=0, font=self.f_small)
        for v in (1.0, 0.97, 0.9, 0.8, 0.7, 0.6):
            sub.add_radiobutton(label=f"{int(v * 100)}%", variable=self.alpha, value=v,
                                command=self._apply_alpha)
        m.add_cascade(label="투명도", menu=sub)
        if IS_WIN:
            m.add_checkbutton(label="Windows 시작 시 자동 실행", variable=self.autostart,
                              command=self._toggle_autostart)
            m.add_command(label="데이터 폴더 열기", command=lambda: os.startfile(DATA_DIR))
        m.add_separator()
        m.add_command(label="종료", command=self.quit)
        self.root.bind_all("<Button-3>", lambda e: m.tk_popup(e.x_root, e.y_root))

    # ── 작은 위젯 헬퍼 ──
    def _placeholder(self, entry, text, bg):
        def show(_=None):
            if not entry.get():
                entry.insert(0, text)
                entry.config(fg=DIM)
                entry.ph = True

        def hide(_=None):
            if getattr(entry, "ph", False):
                entry.delete(0, "end")
                entry.config(fg=TEXT)
                entry.ph = False

        entry.bind("<FocusIn>", hide, add="+")
        entry.bind("<FocusOut>", show, add="+")
        show()

    def _bind_drag(self, w):
        w.bind("<ButtonPress-1>", self._drag_start)
        w.bind("<B1-Motion>", self._drag_move)
        w.bind("<ButtonRelease-1>", self._drag_end)

    def _fit(self, text, font, maxw):
        if font.measure(text) <= maxw:
            return text
        while text and font.measure(text + "…") > maxw:
            text = text[:-1]
        return text + "…"

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

    def _draw_cell(self, i, d, x, y):
        S, c = self.S, self.cal
        cw, ch = self.cw, self.ch
        in_month = d.month == self.view_m
        is_today, is_sel = d == self.today, d == self.selected
        hover = i == self.hover_idx
        holiday = holiday_name(d)
        wd = (d.weekday() + 1) % 7  # 0 = 일요일

        fill = CARD_HOVER if hover else CARD if in_month else CARD_DIM
        if is_sel:
            rrect(c, x, y, x + cw, y + ch, S(10), fill=fill, outline=ACCENT, width=S(2))
        else:
            rrect(c, x, y, x + cw, y + ch, S(10), fill=fill, outline=BORDER if in_month else "")

        # 날짜 숫자 (오늘은 바이올렛 원)
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

        # 칩 (공휴일 이름 / 할 일)
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
                col = DONE if done else tag_color(v.get("text", ""))
                bgc = CARD if in_month else CARD_DIM
                rrect(c, x1, yy, x2, yy + S(15), S(4),
                      fill=blend(col, bgc, 0.16 if not done else 0.10), outline="")
                c.create_rectangle(x1, yy + S(3), x1 + S(2), yy + S(12), fill=col, outline="")
                c.create_text(x1 + S(6), yy + S(7.5), anchor="w", font=self.f_tiny,
                              text=self._fit(v.get("text", ""), self.f_tiny, x2 - x1 - S(10)),
                              fill=blend(col, "#ffffff", 0.55) if not done else DIM)
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
        self.show_day(d)
        if (d.year, d.month) != (self.view_y, self.view_m):
            self.view_y, self.view_m = d.year, d.month
        self.render_month()

    def shift_month(self, delta):
        m = self.view_m - 1 + delta
        self.view_y += m // 12
        self.view_m = m % 12 + 1
        self.render_month()

    def go_today(self):
        self.today = dt.date.today()
        self.view_y, self.view_m = self.today.year, self.today.month
        self.show_day(self.today)
        self.render_month()

    def _on_wheel_month(self, e):
        self.shift_month(-1 if e.delta > 0 else 1)

    # ── 상세 패널 ──
    def _entry(self, d, create=False):
        key = d.isoformat()
        if create:
            return self.data.setdefault(key, {"memo": "", "todos": []})
        return self.data.get(key, {})

    def show_day(self, d):
        self._flush_memo()
        self.selected = d
        self._memo_date = d
        self.d_title.config(text=f"{d.month}월 {d.day}일")
        self._update_sub()
        self.memo.delete("1.0", "end")
        self.memo.insert("1.0", self._entry(d).get("memo", ""))
        self.memo.edit_reset()
        self.render_todos()
        self.todo_canvas.yview_moveto(0)

    def _update_sub(self):
        d = self.selected
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
            cv.create_oval(2, 2, n - 2, n - 2, fill=ACCENT, outline="")
            cv.create_line(n * .30, n * .52, n * .45, n * .67, n * .72, n * .36,
                           fill="#ffffff", width=S(2), capstyle="round", joinstyle="round")
        else:
            ring = cv.create_oval(2, 2, n - 2, n - 2, outline=color, width=S(2))
            cv.bind("<Enter>", lambda e: cv.itemconfig(ring, fill=blend(color, SURFACE, 0.2)))
            cv.bind("<Leave>", lambda e: cv.itemconfig(ring, fill=""))
        cv.bind("<Button-1>", lambda e: cmd())
        cv.bind("<MouseWheel>", self._on_wheel_todo)
        return cv

    def render_todos(self):
        S = self.S
        for w in self.todo_inner.winfo_children():
            w.destroy()
        todos = self._entry(self.selected).get("todos", [])
        done_n = sum(1 for t in todos if t.get("done"))
        self.todo_count.config(text=f"{done_n}/{len(todos)}" if todos else "")
        self._draw_progress()
        if not todos:
            lbl = tk.Label(self.todo_inner, text="등록된 할 일이 없어요\n위 입력창에 적고 Enter",
                           font=self.f_small, bg=SURFACE, fg=DIM, anchor="w", justify="left")
            lbl.pack(fill="x", pady=S(6))
            lbl.bind("<MouseWheel>", self._on_wheel_todo)
            return
        for idx in todo_order(todos):
            t = todos[idx]
            done = t.get("done", False)
            col = DONE if done else tag_color(t.get("text", ""))
            row = tk.Frame(self.todo_inner, bg=SURFACE)
            row.pack(fill="x", pady=S(3))
            chk = self._check_box(row, done, col, lambda i=idx: self.toggle_todo(i))
            chk.pack(side="left", anchor="n", pady=S(1))
            dele = tk.Label(row, text="✕", font=self.f_tiny, bg=SURFACE, fg=DIM, cursor="hand2")
            dele.pack(side="right", anchor="n", padx=(S(4), 0), pady=S(3))
            body = tk.Frame(row, bg=SURFACE)
            body.pack(side="left", fill="x", expand=True, padx=(S(8), 0))
            if t.get("time"):
                tk.Label(body, text=t["time"], font=self.f_tiny, bg=SURFACE,
                         fg=DIM if done else "#ffb547", anchor="w").pack(fill="x")
            txt = tk.Label(body, text=t.get("text", ""), font=self.f_done if done else self.f_body,
                           bg=SURFACE, fg=DONE if done else TEXT, anchor="w", justify="left",
                           wraplength=S(176))
            txt.pack(fill="x")
            dele.bind("<Button-1>", lambda e, i=idx: self.delete_todo(i))
            dele.bind("<Enter>", lambda e, w=dele: w.config(fg=SUN))
            dele.bind("<Leave>", lambda e, w=dele: w.config(fg=DIM))
            txt.bind("<Double-Button-1>", lambda e, i=idx, w=txt: self._edit_todo(i, w))
            for w in (row, body, txt, dele):
                w.bind("<MouseWheel>", self._on_wheel_todo)

    def add_todo(self):
        if getattr(self.todo_entry, "ph", False):
            return
        text = self.todo_entry.get().strip()
        if not text:
            return
        item = {"text": text, "done": False}
        m = TIME_RE.match(text)
        if m and int(m.group(1)) < 24 and int(m.group(2)) < 60:
            item = {"text": m.group(3).strip(), "done": False,
                    "time": f"{int(m.group(1)):02d}:{m.group(2)}"}
        self._entry(self.selected, create=True)["todos"].append(item)
        self.todo_entry.delete(0, "end")
        self._after_todo_change()

    def toggle_todo(self, i):
        t = self._entry(self.selected)["todos"][i]
        t["done"] = not t.get("done", False)
        self._after_todo_change()

    def delete_todo(self, i):
        del self._entry(self.selected)["todos"][i]
        self._after_todo_change()

    def _edit_todo(self, i, label):
        t = self._entry(self.selected)["todos"][i]
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
        c = self.todo_canvas
        if self.todo_inner.winfo_height() > c.winfo_height():
            c.yview_scroll(int(-e.delta / 120), "units")

    def _escape_entry(self):
        self.todo_entry.delete(0, "end")
        self.root.focus_set()
        self.send_to_bottom()

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

    # ── 창 이동 / 메뉴 동작 ──
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
