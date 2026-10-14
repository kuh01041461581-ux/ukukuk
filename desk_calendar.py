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
import base64
import json
import os
import queue
import re
import shutil
import sys
import threading
import time
import webbrowser
import tkinter as tk
import tkinter.font as tkfont
from tkinter import colorchooser, filedialog, messagebox

import core
import glass
import gongmun
import holidays_api
import holidays_kr
import cloudsync
import analyze
import ocr
import quickadd
import sticky
import work
import hwpdoc
from core import item_end
from holidays_kr import holiday_name

APP_NAME = "DeskCalendar"
IS_WIN = sys.platform == "win32"
IS_FROZEN = getattr(sys, "frozen", False)

DATA_DIR = os.path.join(os.environ.get("APPDATA") or os.path.expanduser("~"), APP_NAME)
DATA_FILE = os.path.join(DATA_DIR, "data.json")
CONF_FILE = os.path.join(DATA_DIR, "config.json")
HOLIDAY_FILE = os.path.join(DATA_DIR, "holidays_cache.json")   # 공공데이터 API 로 받은 공휴일 보관
REQUEST_FILE = os.path.join(DATA_DIR, "open_request.txt")   # 이미 실행 중인 앱으로 PDF 경로를 넘길 때 쓰는 파일

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
# 중요도는 3단계(중요/보통/낮음). 번호 0 은 예전 데이터용(= 중요)
PRIORITIES = [("중요", "#ff5c7a"), ("중요", "#ff5c7a"), ("보통", "#8b7bff"), ("낮음", "#3ddc97")]
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


def open_path(path):
    """파일을 기본 프로그램으로 열기"""
    try:
        if IS_WIN:
            os.startfile(path)
        else:
            import subprocess
            subprocess.Popen(["xdg-open", path])
    except Exception:
        messagebox.showinfo("열 수 없어요", f"파일을 찾을 수 없어요.\n{path}")


def auto_backup(keep=14):
    """하루 한 번 data.json 을 backups 폴더에 복사해 두고 오래된 것은 지운다"""
    try:
        if not os.path.exists(DATA_FILE):
            return
        d = os.path.join(DATA_DIR, "backups")
        os.makedirs(d, exist_ok=True)
        dst = os.path.join(d, f"data-{dt.date.today():%Y%m%d}.json")
        if not os.path.exists(dst):
            shutil.copy(DATA_FILE, dst)
        files = sorted(f for f in os.listdir(d) if f.startswith("data-"))
        for f in files[:-keep]:
            os.remove(os.path.join(d, f))
    except Exception:
        pass


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


def work_area(root, cx=None, cy=None):
    """창이 놓일 수 있는 화면 영역 (x, y, w, h). 주 모니터는 작업표시줄을 뺀 영역"""
    if IS_WIN:
        try:
            rc = wintypes.RECT()
            ctypes.windll.user32.SystemParametersInfoW(48, 0, ctypes.byref(rc), 0)   # SPI_GETWORKAREA
            px, py, pw, ph = rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top
            if cx is None or (px <= cx < px + pw and py <= cy < py + ph):
                return px, py, pw, ph
        except Exception:
            pass
    return virtual_screen(root)


def hook_dropfiles(hwnds, q, keep):
    """윈도우 탐색기에서 파일을 끌어다 놓으면 경로 목록을 q 에 넣는다 (WM_DROPFILES 를 직접 받음)"""
    from ctypes import wintypes
    user32, shell32 = ctypes.windll.user32, ctypes.windll.shell32
    LRESULT = ctypes.c_ssize_t
    WNDPROC = ctypes.WINFUNCTYPE(LRESULT, wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM)
    user32.CallWindowProcW.argtypes = [ctypes.c_void_p, wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
    user32.CallWindowProcW.restype = LRESULT
    setl = getattr(user32, "SetWindowLongPtrW", None) or user32.SetWindowLongW
    setl.argtypes = [wintypes.HWND, ctypes.c_int, ctypes.c_void_p]
    setl.restype = ctypes.c_void_p
    shell32.DragQueryFileW.argtypes = [wintypes.HANDLE, wintypes.UINT, wintypes.LPWSTR, wintypes.UINT]
    shell32.DragQueryFileW.restype = wintypes.UINT
    shell32.DragFinish.argtypes = [wintypes.HANDLE]
    shell32.DragAcceptFiles.argtypes = [wintypes.HWND, wintypes.BOOL]
    for msg in (0x233, 0x49):          # 관리자 권한으로 실행돼도 드롭을 받도록 허용
        try:
            user32.ChangeWindowMessageFilter(msg, 1)
        except Exception:
            pass
    olds = keep.setdefault("olds", {})
    for h in list(olds):                # 사라진 창 기록 정리
        if not user32.IsWindow(h):
            del olds[h]
    cbs = keep.setdefault("cbs", [])
    for h in hwnds:
        if not h or h in olds:
            continue

        def make(h):
            def proc(hwnd, msg, wp, lp):
                if msg == 0x233:
                    try:
                        n = shell32.DragQueryFileW(wp, 0xFFFFFFFF, None, 0)
                        files = []
                        for i in range(n):
                            ln = shell32.DragQueryFileW(wp, i, None, 0)
                            buf = ctypes.create_unicode_buffer(ln + 1)
                            shell32.DragQueryFileW(wp, i, buf, ln + 1)
                            files.append(buf.value)
                        shell32.DragFinish(wp)
                        q.put(files)
                    except Exception:
                        pass
                    return 0
                return user32.CallWindowProcW(olds[h], hwnd, msg, wp, lp)
            return WNDPROC(proc)
        cb = make(h)
        cbs.append(cb)
        olds[h] = setl(h, -4, ctypes.cast(cb, ctypes.c_void_p))
        shell32.DragAcceptFiles(h, True)


def _tray_image():
    """트레이 아이콘 그림 (달력 모양, 앱 강조색)"""
    from PIL import Image, ImageDraw
    n = 128
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((8, 14, n - 8, n - 8), radius=22, fill=(124, 108, 255, 255))
    d.rounded_rectangle((8, 14, n - 8, 46), radius=22, fill=(98, 82, 235, 255))
    d.rectangle((8, 34, n - 8, 46), fill=(98, 82, 235, 255))
    for cx in (38, 90):
        d.rounded_rectangle((cx - 5, 4, cx + 5, 28), radius=5, fill=(236, 239, 245, 255))
    for r in range(2):
        for c in range(3):
            x, y = 26 + c * 28, 62 + r * 24
            d.rounded_rectangle((x, y, x + 18, y + 14), radius=4, fill=(255, 255, 255, 235 if (r, c) != (1, 1) else 255))
    return im


def start_tray(q):
    """시스템 트레이(알림 영역) 아이콘: 왼쪽 클릭 = 열기, 오른쪽 클릭 = 메뉴. 명령은 큐로 넘겨 Tk 쪽에서 처리"""
    try:
        import pystray
    except Exception:
        return None
    put = lambda cmd: (lambda icon=None, item=None: q.put(cmd))
    menu = pystray.Menu(
        pystray.MenuItem("열기", put("show"), default=True),
        pystray.MenuItem("오늘 일정 추가", put("add")),
        pystray.MenuItem("클립보드 내용으로 일정 만들기", put("clip")),
        pystray.MenuItem("업무 관리", put("work")),
        pystray.MenuItem("휴대폰 연결", put("phone")),
        pystray.MenuItem("설정", put("settings")),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem("종료", put("quit")))
    icon = pystray.Icon("DeskCalendar", _tray_image(), "DeskCalendar", menu)
    try:
        icon.run_detached()
    except Exception:
        return None
    return icon


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
    if p == 0:
        return 1
    return p if isinstance(p, int) and 0 <= p < len(PRIORITIES) else DEFAULT_PRI


def next_pri(p):
    """중요 → 낮음 → 보통 → 중요 … 로 돌려 가며 바꾸기"""
    return 3 if p <= 1 else p - 1


def pri_color(t):
    return PRIORITIES[pri_of(t)][1]


def todo_order(todos):
    """표시 순서: 미완료(중요도 높은 순 → 시간 순) → 완료. 반환값은 todos 의 인덱스 목록"""
    return sorted(range(len(todos)),
                  key=lambda i: (bool(todos[i].get("done")), pri_of(todos[i]),
                                 todos[i].get("time") or "99:99", i))


def sort_key(t):
    return (bool(t.get("done")), pri_of(t), t.get("time") or "99:99")


class Pill(tk.Canvas):
    """둥근 알약형 버튼: 마우스를 올리면 색이 부드럽게 바뀌고, 누르면 살짝 들어갔다가 손을 떼면 실행된다"""

    STEPS = 5

    def __init__(self, parent, text, cmd, font, fg=None, bg=None, hover=None,
                 parent_bg=None, padx=12, pady=5, r=None):
        fg, bg = fg or TEXT, bg or CARD
        hover, parent_bg = hover or CARD_HOVER, parent_bg or BG
        w = font.measure(text) + 2 * padx
        h = font.metrics("linespace") + 2 * pady
        super().__init__(parent, width=w, height=h, bg=parent_bg, highlightthickness=0,
                         bd=0, cursor="hand2")
        self._pw, self._ph, self._cmd = w, h, cmd
        self._bg, self._hover = bg, hover
        self._press = blend("#000000", bg, 0.16)         # 눌렀을 때: 조금 어둡게
        self._k, self._goal, self._down, self._job = 0, 0, False, None
        self._shape = rrect(self, 1, 1, w - 1, h - 1, h / 2 if r is None else r, fill=bg)   # 완전히 둥글게
        self._label = self.create_text(w / 2, h / 2, text=text, font=font, fill=fg)
        self.bind("<Enter>", lambda e: self._to(self.STEPS))
        self.bind("<Leave>", lambda e: (setattr(self, "_down", False), self._to(0), self._nudge(0)))
        self.bind("<ButtonPress-1>", self._on_press)
        self.bind("<ButtonRelease-1>", self._on_release)

    def _paint(self):
        col = blend(self._hover, self._bg, self._k / self.STEPS) if self._k else self._bg
        if self._down:
            col = self._press if self._hover != self._bg else col
        self.itemconfig(self._shape, fill=col)

    def _to(self, goal):
        self._goal = goal
        if self._job is None:
            self._step()

    def _step(self):
        self._job = None
        if self._k == self._goal:
            self._paint()
            return
        self._k += 1 if self._goal > self._k else -1
        self._paint()
        try:
            self._job = self.after(14, self._step)
        except tk.TclError:
            self._job = None

    def _nudge(self, dy):
        """눌린 느낌: 글자를 1px 아래로"""
        try:
            self.coords(self._label, self._pw / 2, self._ph / 2 + dy)
        except tk.TclError:
            pass

    def _on_press(self, e):
        self._down = True
        self._paint()
        self._nudge(1)

    def _on_release(self, e):
        was, self._down = self._down, False
        self._paint()
        self._nudge(0)
        if was and 0 <= e.x < self._pw and 0 <= e.y < self._ph:
            self._cmd()


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
    _polish_dialog(top)
    return top


def _polish_dialog(top):
    """창이 부드럽게 나타나고(페이드 인), 윈도우 11 둥근 모서리·어두운 제목줄을 쓴다"""
    try:
        top.attributes("-alpha", 0.0)
    except tk.TclError:
        return
    if IS_WIN:
        try:
            top.update_idletasks()
            hwnd = ctypes.windll.user32.GetParent(top.winfo_id()) or top.winfo_id()
            dark = ctypes.c_int(1 if THEME_IS_DARK() else 0)
            ctypes.windll.dwmapi.DwmSetWindowAttribute(hwnd, 20, ctypes.byref(dark), ctypes.sizeof(dark))
            rnd = ctypes.c_int(2)
            ctypes.windll.dwmapi.DwmSetWindowAttribute(hwnd, 33, ctypes.byref(rnd), ctypes.sizeof(rnd))
        except Exception:
            pass

    def step(i=1):
        try:
            top.attributes("-alpha", min(1.0, i / 6))
        except tk.TclError:
            return
        if i < 6:
            top.after(18, lambda: step(i + 1))
    top.after(10, step)


def THEME_IS_DARK():
    try:
        c = BG.lstrip("#")
        return (int(c[0:2], 16) * 299 + int(c[2:4], 16) * 587 + int(c[4:6], 16) * 114) / 1000 < 128
    except Exception:
        return True


class ImportDialog:
    """공문 PDF 에서 찾은 일정 후보를 확인하고 추가하는 창"""

    def __init__(self, app, doc_title, items, src_path=None, heading="공문에서 일정 가져오기", doc_text=None):
        self.app, S = app, app.S
        self.src_path = src_path
        self.doc_text = doc_text
        self.items = items
        self.doc_title = doc_title
        self.rows = []
        top = self.top = _dialog(app, heading, S(760), S(520))
        tk.Label(top, text=heading, font=app.f_h2, bg=SURFACE, fg=TEXT,
                 anchor="w").pack(fill="x", padx=S(20), pady=(S(16), 0))
        if doc_title:
            tk.Label(top, text=doc_title, font=app.f_small, bg=SURFACE, fg=ACCENT,
                     anchor="w", wraplength=S(700), justify="left").pack(fill="x", padx=S(20))
        msg = ("날짜가 있는 항목을 찾았어요. 추가할 것만 체크하세요. 날짜를 비워 두면 보관함(할 일)으로 들어가요. "
               "'기간'을 체크한 것(감사·의회·훈련 등)만 여러 날 막대로, 나머지는 마감일(종료일) 하루에 표시돼요."
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
        dup = bool(it["text"]) and it["date"] is not None and app.has_similar(it["date"], it["text"])
        var = tk.BooleanVar(value=not dup)
        tk.Checkbutton(row, variable=var, bg=SURFACE, activebackground=SURFACE, selectcolor=CARD,
                       fg=TEXT, command=self.refresh_count, bd=0, highlightthickness=0
                       ).pack(side="left")
        if dup:
            tk.Label(row, text="이미 있음", font=app.f_tiny, bg=ACCENT_SOFT, fg=ACCENT, padx=S(4)
                     ).pack(side="left", padx=(0, S(6)))

        def entry(width, text, expand=False):
            e = tk.Entry(row, font=app.f_small, bg=CARD, fg=TEXT, insertbackground=TEXT,
                         relief="flat", width=width, highlightthickness=1,
                         highlightbackground=BORDER, highlightcolor=ACCENT)
            e.insert(0, text)
            e.pack(side="left", fill="x" if expand else "none", expand=expand, padx=(0, S(6)),
                   ipady=S(4))
            return e

        d = entry(11, it["date"].isoformat() if it["date"] else "")
        tk.Label(row, text="~", font=app.f_small, bg=SURFACE, fg=DIM).pack(side="left", padx=(0, S(6)))
        en = entry(11, it["end"].isoformat() if it.get("end") else "")
        t = entry(6, it.get("time", ""))
        x = entry(10, it["text"], expand=True)
        per = tk.BooleanVar(value=bool(it.get("period", False)))
        tk.Checkbutton(row, text="기간", variable=per, font=app.f_tiny, bg=SURFACE, activebackground=SURFACE,
                       selectcolor=CARD, fg=SUB, activeforeground=TEXT, bd=0, highlightthickness=0
                       ).pack(side="left", padx=(0, S(2)))
        r = {"var": var, "date": d, "end": en, "per": per, "time": t, "text": x, "pri": it.get("pri", DEFAULT_PRI),
             "detail": it.get("detail", "")}
        dot = tk.Canvas(row, width=S(16), height=S(16), bg=SURFACE, highlightthickness=0,
                        cursor="hand2")
        oval = dot.create_oval(2, 2, S(14), S(14), fill=PRIORITIES[r["pri"]][1], outline="")

        def cycle(_=None):
            r["pri"] = next_pri(r["pri"])
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
            if not r["date"].get().strip():                       # 날짜가 없으면 보관함(할 일)으로
                picked.append((None, core.new_item(text, pri=r["pri"])))
                continue
            try:
                d = dt.date.fromisoformat(r["date"].get().strip())
            except ValueError:
                messagebox.showwarning("날짜 확인", "날짜는 2026-10-20 처럼 적어 주세요. (비워 두면 보관함으로 들어가요)",
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
            note = "\n".join(x for x in (r.get("detail") if r.get("detail") != text else "", self.doc_title or "") if x)
            if end and end > d and not r["per"].get():   # 기간 행사가 아니면 마감일(종료일) 하루에만 표시
                note = (note + " · " if note else "") + f"기간 {d.month}/{d.day} ~ {end.month}/{end.day}"
                d, end = end, None
            picked.append((d, core.new_item(text, pri=r["pri"],
                                            **({"note": note} if note else {}),
                                            **({"time": tm} if tm else {}),
                                            **({"end": end.isoformat()} if end and end > d else {}),
                                            **({"src_path": self.src_path} if self.src_path else {}))))
        for d, item in picked:
            core.add_item(app.data, d.isoformat() if d else "_inbox", item)
        case = None
        if self.src_path and self.doc_text is not None:          # 공문이면 업무(폴더)도 함께
            case, _ = app.case_from_doc(self.src_path, self.doc_text, self.doc_title, self.items,
                                        [it for _, it in picked])
        if picked or case:
            app._after_todo_change()
        self.top.destroy()
        if picked or case:
            msg = f"{len(picked)}개 일정을 추가했어요."
            if case:
                msg += "\n업무 폴더도 만들었어요. '업무'에서 기한과 폴더를 볼 수 있어요."
            NotifyPopup(app, "가져오기 완료", msg, beep=False,
                        actions=[("업무 열기", lambda: app.open_work(case["id"]))] if case else None)


class EditDialog:
    """일정 하나의 내용·날짜·기간·시간·중요도·반복·메모를 고치는 창"""

    def __init__(self, app, t):
        self.app, self.t = app, core.base_of(t)
        S = app.S
        key, _ = core.find_owner(app.data, self.t)
        self.key = key
        start = core.parse_date(key)
        top = self.top = _dialog(app, "일정 수정", S(440), S(600))
        pad = S(22)
        tk.Label(top, text="일정 수정", font=app.f_h2, bg=SURFACE, fg=TEXT, anchor="w").pack(
            fill="x", padx=pad, pady=(S(16), S(6)))

        def field(label, width=None):
            f = tk.Frame(top, bg=SURFACE)
            f.pack(fill="x", padx=pad, pady=S(4))
            tk.Label(f, text=label, font=app.f_small, bg=SURFACE, fg=SUB, width=8, anchor="w").pack(side="left")
            e = tk.Entry(f, font=app.f_body, bg=CARD, fg=TEXT, insertbackground=TEXT, relief="flat",
                         highlightthickness=1, highlightbackground=BORDER, highlightcolor=ACCENT,
                         **({"width": width} if width else {}))
            e.pack(side="left", fill="x" if not width else "none", expand=not width, ipady=S(5))
            return e

        self.e_text = field("내용")
        self.e_text.insert(0, self.t.get("text", ""))
        self.e_date = field("날짜", 12)
        self.e_date.insert(0, start.isoformat() if start else "")
        tk.Label(top, text="날짜를 비우면 보관함으로 옮겨져요" if key == "_inbox" else "",
                 font=app.f_tiny, bg=SURFACE, fg=DIM, anchor="w").pack(fill="x", padx=pad + S(70))
        self.e_end = field("종료일", 12)
        self.e_end.insert(0, self.t.get("end", ""))
        self.e_time = field("시간", 7)
        self.e_time.insert(0, self.t.get("time", ""))
        tk.Label(top, text="종료일: 기간 일정일 때만 (예 2026-10-24)  ·  시간: 예 14:30", font=app.f_tiny,
                 bg=SURFACE, fg=DIM, anchor="w").pack(fill="x", padx=pad + S(70))

        f = tk.Frame(top, bg=SURFACE)
        f.pack(fill="x", padx=pad, pady=(S(8), S(4)))
        tk.Label(f, text="중요도", font=app.f_small, bg=SURFACE, fg=SUB, width=8, anchor="w").pack(side="left")
        self.pri = pri_of(self.t)
        self.dots = []
        for n, (name, col) in enumerate(PRIORITIES):
            cv = tk.Canvas(f, width=S(24), height=S(24), bg=SURFACE, highlightthickness=0, cursor="hand2")
            if n:
                cv.pack(side="left", padx=(0, S(4)))
            cv.bind("<Button-1>", lambda e, n=n: self.set_pri(n))
            self.dots.append(cv)
        self.pri_lbl = tk.Label(f, font=app.f_tiny, bg=SURFACE)
        self.pri_lbl.pack(side="left", padx=S(6))
        self.set_pri(self.pri)

        f = tk.Frame(top, bg=SURFACE)
        f.pack(fill="x", padx=pad, pady=S(4))
        tk.Label(f, text="반복", font=app.f_small, bg=SURFACE, fg=SUB, width=8, anchor="w").pack(side="left")
        self.rep = core.repeat_code(self.t.get("repeat"))
        self.rep_btns = {}
        rows = tk.Frame(f, bg=SURFACE)
        rows.pack(side="left", fill="x")
        for k, (code, label) in enumerate(core.REPEAT_LABELS):
            if k % 5 == 0:
                line = tk.Frame(rows, bg=SURFACE)
                line.pack(fill="x", pady=(0, S(4)))
            b = tk.Label(line, text=label, font=app.f_tiny, padx=S(8), pady=S(3), cursor="hand2")
            b.pack(side="left", padx=(0, S(4)))
            b.bind("<Button-1>", lambda e, c=code: self.set_rep(c))
            self.rep_btns[code] = b
        self.e_until = field("반복 종료", 12)
        self.e_until.insert(0, (self.t.get("repeat") or {}).get("until", ""))
        self.set_rep(self.rep)

        tk.Label(top, text="메모", font=app.f_small, bg=SURFACE, fg=SUB, anchor="w").pack(
            fill="x", padx=pad, pady=(S(8), S(2)))
        self.note = tk.Text(top, height=4, font=app.f_body, bg=CARD, fg=TEXT, insertbackground=TEXT,
                            relief="flat", wrap="word", padx=S(8), pady=S(6), highlightthickness=1,
                            highlightbackground=BORDER, highlightcolor=ACCENT)
        self.note.insert("1.0", self.t.get("note", ""))
        self.note.pack(fill="x", padx=pad)

        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=pad, pady=S(16))
        Pill(foot, "저장", self.save, app.f_body, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(22), pady=S(6)).pack(side="right")
        Pill(foot, "취소", top.destroy, app.f_body, parent_bg=SURFACE, padx=S(16), pady=S(6)
             ).pack(side="right", padx=S(8))
        if self.t.get("src_path"):
            Pill(foot, "공문 원본 열기", lambda: open_path(self.t["src_path"]), app.f_small, parent_bg=SURFACE,
                 padx=S(12), pady=S(6)).pack(side="left")
        self.e_text.focus_set()

    def set_pri(self, n):
        S = self.app.S
        self.pri = n
        for i, cv in enumerate(self.dots):
            cv.delete("all")
            col = PRIORITIES[i][1]
            if i == n:
                cv.create_oval(1, 1, S(23), S(23), outline=col, width=S(2))
            cv.create_oval(S(6), S(6), S(18), S(18), fill=col, outline="")
        self.pri_lbl.config(text=PRIORITIES[n][0], fg=PRIORITIES[n][1])

    def set_rep(self, code):
        self.rep = code
        for c, b in self.rep_btns.items():
            b.config(bg=ACCENT if c == code else CARD, fg="#ffffff" if c == code else TEXT)
        self.e_until.config(state="normal" if code else "disabled")

    def save(self):
        app, t = self.app, self.t
        text = self.e_text.get().strip()
        if not text:
            messagebox.showwarning("확인", "내용을 입력해 주세요.", parent=self.top)
            return
        ds, es = self.e_date.get().strip(), self.e_end.get().strip()
        d = core.parse_date(ds) if ds else None
        if ds and not d:
            messagebox.showwarning("날짜 확인", "날짜는 2026-10-20 처럼 적어 주세요.", parent=self.top)
            return
        if self.key != "_inbox" and not d:
            messagebox.showwarning("날짜 확인", "날짜를 입력해 주세요. (보관함으로 보내려면 목록에서 우클릭)", parent=self.top)
            return
        e = core.parse_date(es) if es else None
        if es and (not e or (d and e <= d)):
            messagebox.showwarning("종료일 확인", "종료일은 시작 날짜보다 뒤여야 해요. (하루 일정이면 비워 두세요)", parent=self.top)
            return
        tm = self.e_time.get().strip()
        m = re.fullmatch(r"(\d{1,2}):(\d{2})", tm)
        if tm and not (m and int(m.group(1)) < 24 and int(m.group(2)) < 60):
            messagebox.showwarning("시간 확인", "시간은 14:30 처럼 적어 주세요.", parent=self.top)
            return
        until = self.e_until.get().strip()
        if self.rep and until and not core.parse_date(until):
            messagebox.showwarning("날짜 확인", "반복 종료일은 2026-12-31 처럼 적어 주세요.", parent=self.top)
            return
        t["text"], t["pri"] = text, self.pri
        for k, v in (("time", f"{int(m.group(1)):02d}:{m.group(2)}" if tm else ""),
                     ("end", e.isoformat() if e and not self.rep else ""),
                     ("note", self.note.get("1.0", "end-1c").strip())):
            if v:
                t[k] = v
            else:
                t.pop(k, None)
        if self.rep:
            t["repeat"] = core.repeat_from_code(self.rep, d, until or None)
        else:
            t.pop("repeat", None)
            t.pop("done_dates", None)
        core.touch(t)
        newkey = d.isoformat() if d else "_inbox"
        if newkey != self.key:
            core.remove_item(app.data, t)
            core.add_item(app.data, newkey, t)
        self.top.destroy()
        app._after_todo_change()


class DayDetail:
    """날짜 하나의 모든 일정과 메모를 크게 보여 주고 바로 고칠 수 있는 창 (달력 날짜 더블클릭)"""

    def __init__(self, app, d, end=None):
        self.app, self.d, self.end = app, d, end
        self.pri = DEFAULT_PRI
        S = app.S
        self.top = _dialog(app, "메모", S(400), S(520))
        self.top.minsize(S(320), S(360))
        self.top.protocol("WM_DELETE_WINDOW", self.close)
        self.top.bind("<Escape>", lambda e: self.close())
        self.build()
        self.top.after(60, self._focus)

    def _focus(self):
        try:
            self.top.focus_force()
            self.entry.focus_force()
        except tk.TclError:
            pass

    def close(self):
        self.save_memo()
        self.app.detail = None
        self.top.destroy()

    def save_memo(self):
        m = getattr(self, "memo", None)
        if m is None:
            return
        try:
            text = m.get("1.0", "end-1c")
        except tk.TclError:
            return
        app = self.app
        if app._entry(self.d).get("memo", "") != text:
            e = app._entry(self.d, create=True)
            e["memo"], e["memo_ts"] = text, core.now_ms()
            app._commit()
            app.draw_calendar()

    def go(self, delta):
        self.save_memo()
        self.d = self.d + dt.timedelta(days=delta) if delta else dt.date.today()
        self.end = None
        self.build()

    def build(self):
        app, top = self.app, self.top
        S = app.S
        if getattr(self, "memo", None) is not None:
            self.save_memo()
        for w in top.winfo_children():
            w.destroy()
        top.configure(bg=SURFACE)
        d = self.d
        pad = S(22)
        h = holiday_name(d)
        hd = tk.Frame(top, bg=SURFACE)
        hd.pack(fill="x", padx=pad, pady=(S(16), S(4)))
        Pill(hd, "‹", lambda: self.go(-1), app.f_body, parent_bg=SURFACE, padx=S(11), pady=S(3)).pack(side="left")
        tk.Label(hd, text=f"{d.year}년 {d.month}월 {d.day}일 ({WEEKDAY_KO[d.weekday()]})", font=app.f_h2,
                 bg=SURFACE, fg=TEXT).pack(side="left", padx=S(10))
        Pill(hd, "›", lambda: self.go(1), app.f_body, parent_bg=SURFACE, padx=S(11), pady=S(3)).pack(side="left")
        Pill(hd, "오늘", lambda: self.go(0), app.f_small, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(12), pady=S(4)).pack(side="left", padx=S(8))
        sub = []
        if self.end and self.end > d:
            sub.append(f"기간 {d.month}/{d.day} ~ {self.end.month}/{self.end.day} 로 추가돼요")
        if d == app.today:
            sub.append("오늘")
        if h:
            sub.append(h)
        if sub:
            tk.Label(top, text=" · ".join(sub), font=app.f_small, bg=SURFACE, fg=SUN if h else ACCENT, anchor="w"
                     ).pack(fill="x", padx=pad)

        # 아래쪽: 추가 입력
        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=pad, pady=S(10))
        box = tk.Frame(foot, bg=BORDER, padx=1, pady=1)
        box.pack(fill="x")
        inner = tk.Frame(box, bg=CARD)
        inner.pack(fill="x")
        tk.Label(inner, text="＋", font=app.f_body, bg=CARD, fg=ACCENT).pack(side="left", padx=(S(10), 0))
        self.entry = tk.Entry(inner, font=app.f_body, bg=CARD, fg=TEXT, insertbackground=TEXT, relief="flat",
                              highlightthickness=0)
        Pill(inner, "추가 ↵", self.add, app.f_small, fg="#ffffff", bg=ACCENT, hover=blend(ACCENT, "#ffffff", 0.25),
             parent_bg=CARD, padx=S(12), pady=S(5)).pack(side="right", padx=(0, S(6)))
        self.entry.pack(side="left", fill="x", expand=True, ipady=S(8), padx=S(8))
        self.entry.bind("<Return>", lambda e: self.add())
        self.entry.bind("<FocusIn>", lambda e: box.config(bg=ACCENT))
        self.entry.bind("<FocusOut>", lambda e: box.config(bg=BORDER))
        pr = tk.Frame(foot, bg=SURFACE)
        pr.pack(fill="x", pady=(S(6), 0))
        tk.Label(pr, text="새 일정 · 중요도", font=app.f_tiny, bg=SURFACE, fg=SUB).pack(side="left")
        self.dots = []
        for n, (name, col) in enumerate(PRIORITIES):
            cv = tk.Canvas(pr, width=S(22), height=S(22), bg=SURFACE, highlightthickness=0, cursor="hand2")
            if n:
                cv.pack(side="left", padx=(S(8) if n == 1 else 0, 0))
            cv.bind("<Button-1>", lambda e, n=n: self.set_pri(n))
            self.dots.append(cv)
        self.pri_lbl = tk.Label(pr, font=app.f_tiny, bg=SURFACE)
        self.pri_lbl.pack(side="left", padx=S(6))
        self.set_pri(self.pri)
        Pill(pr, "닫기", self.close, app.f_small, parent_bg=SURFACE, padx=S(16), pady=S(4)).pack(side="right")

        # 메모
        mf = tk.Frame(top, bg=SURFACE)
        mf.pack(side="bottom", fill="x", padx=pad, pady=(S(6), 0))
        tk.Label(mf, text="메모", font=app.f_head, bg=SURFACE, fg=SUB, anchor="w").pack(fill="x", pady=(0, S(3)))
        self.memo = tk.Text(mf, height=5, font=app.f_body, bg=CARD, fg=TEXT, insertbackground=TEXT, relief="flat",
                            wrap="word", padx=S(10), pady=S(8), undo=True, highlightthickness=1,
                            highlightbackground=BORDER, highlightcolor=ACCENT)
        self.memo.insert("1.0", app._entry(d).get("memo", ""))
        self.memo.pack(fill="x")
        self.memo.bind("<FocusOut>", lambda e: self.save_memo())

        # 일정 목록 (스크롤)
        wrap = tk.Frame(top, bg=SURFACE)
        wrap.pack(fill="both", expand=True, padx=pad, pady=(S(10), 0))
        cv = tk.Canvas(wrap, bg=SURFACE, highlightthickness=0, bd=0)
        cv.pack(fill="both", expand=True)
        body = tk.Frame(cv, bg=SURFACE)
        win = cv.create_window((0, 0), window=body, anchor="nw")
        body.bind("<Configure>", lambda e: cv.configure(scrollregion=cv.bbox("all")))
        cv.bind("<Configure>", lambda e: cv.itemconfigure(win, width=e.width))
        scroll = lambda e: cv.yview_scroll(int(-e.delta / 120), "units")
        cv.bind("<MouseWheel>", scroll)
        rows = core.day_items(app.data, d)
        rows.sort(key=lambda r: sort_key(r[1]))
        done_n = sum(1 for _, t, _ in rows if t.get("done"))
        tk.Label(body, text=f"일정 {len(rows)}개" + (f"  ·  완료 {done_n}" if rows else ""), font=app.f_head,
                 bg=SURFACE, fg=SUB, anchor="w").pack(fill="x", pady=(0, S(4)))
        if not rows:
            tk.Label(body, text="이 날의 일정이 없어요. 아래에 입력해 추가하세요.", font=app.f_small, bg=SURFACE,
                     fg=DIM, anchor="w").pack(fill="x", pady=S(10))
        for lst, t, s0 in rows:
            self.row(body, lst, t, s0, scroll)
        body.bind("<MouseWheel>", scroll)

    def row(self, parent, lst, t, s0, scroll):
        app = self.app
        S = app.S
        done = t.get("done", False)
        col = pri_color(t)
        card = tk.Frame(parent, bg=CARD, highlightthickness=1, highlightbackground=BORDER)
        card.pack(fill="x", pady=S(4))
        inner = tk.Frame(card, bg=CARD)
        inner.pack(fill="x", padx=S(12), pady=S(10))
        chk = tk.Canvas(inner, width=S(24), height=S(24), bg=CARD, highlightthickness=0, cursor="hand2")
        n = S(24)
        if done:
            chk.create_oval(2, 2, n - 2, n - 2, fill=DONE, outline="")
            chk.create_line(n * .30, n * .52, n * .45, n * .67, n * .72, n * .36, fill="#ffffff", width=S(2))
        else:
            chk.create_oval(2, 2, n - 2, n - 2, outline=col, width=S(2))
        chk.pack(side="left", anchor="n")
        chk.bind("<Button-1>", lambda e, t=t: self.act(app.toggle_todo, t))
        body = tk.Frame(inner, bg=CARD)
        body.pack(side="left", fill="x", expand=True, padx=(S(12), 0))
        e0 = item_end(t, s0)
        tags = []
        if t.get("repeat"):
            tags.append("반복 " + core.repeat_label(t["repeat"], s0))
        elif e0:
            tags.append(f"기간 {s0.month}/{s0.day} ~ {e0.month}/{e0.day}")
        if t.get("time"):
            tags.append(t["time"])
        tags.append(PRIORITIES[pri_of(t)][0])
        cid = core.base_of(t).get("case")
        if cid in (app.data.get("_work") or {}):
            tags.append("업무")
        tl = tk.Label(body, text="  ·  ".join(tags), font=app.f_tiny, bg=CARD, fg=DIM if done else col, anchor="w")
        tl.pack(fill="x")
        if cid in (app.data.get("_work") or {}):
            tl.config(cursor="hand2")
            tl.bind("<Button-1>", lambda e, cid=cid: app.open_work(cid))
        tk.Label(body, text=t.get("text", ""), font=app.f_done if done else app.f_body, bg=CARD,
                 fg=DONE if done else TEXT, anchor="w", justify="left", wraplength=S(260)).pack(fill="x")
        if t.get("note"):
            tk.Label(body, text=t["note"], font=app.f_small, bg=CARD, fg=SUB, anchor="w", justify="left",
                     wraplength=S(260)).pack(fill="x", pady=(S(4), 0))
        btns = tk.Frame(inner, bg=CARD)
        btns.pack(side="right", anchor="n")
        Pill(btns, "수정", lambda t=t: app.edit_item(t), app.f_tiny, parent_bg=CARD, bg=SURFACE, hover=BORDER,
             padx=S(10), pady=S(3)).pack(side="top", pady=(0, S(4)))
        Pill(btns, "삭제", lambda l=lst, t=t: self.act(app.delete_todo, l, t), app.f_tiny, parent_bg=CARD, bg=SURFACE,
             hover=BORDER, fg=SUN, padx=S(10), pady=S(3)).pack(side="top")
        for w in (card, inner, body, chk):
            w.bind("<MouseWheel>", scroll)

    def act(self, fn, *a):
        fn(*a)           # app._after_todo_change 가 이 창도 다시 그린다

    def set_pri(self, n):
        S = self.app.S
        self.pri = n
        for i, cv in enumerate(self.dots):
            cv.delete("all")
            col = PRIORITIES[i][1]
            if i == n:
                cv.create_oval(1, 1, S(21), S(21), outline=col, width=S(2))
            cv.create_oval(S(5), S(5), S(17), S(17), fill=col, outline="")
        self.pri_lbl.config(text=PRIORITIES[n][0], fg=PRIORITIES[n][1])

    def add(self):
        app = self.app
        text = self.entry.get().strip()
        if not text:
            return
        item = core.new_item(text, pri=self.pri)
        if self.end and self.end > self.d:
            item["end"] = self.end.isoformat()
        m = TIME_RE.match(text)
        if m and int(m.group(1)) < 24 and int(m.group(2)) < 60:
            item.update(text=m.group(3).strip(), time=f"{int(m.group(1)):02d}:{m.group(2)}")
        core.add_item(app.data, self.d.isoformat(), item)
        self.pri = DEFAULT_PRI
        app._after_todo_change()
        try:
            self.entry.focus_set()
        except tk.TclError:
            pass


class CommandBar:
    """Ctrl+K / Ctrl+Alt+Space: 한 줄로 일정 추가('내일 오후 3시 회의 !중요') · 검색 · 명령을 한 곳에서"""

    def __init__(self, app):
        self.app = app
        S = app.S
        self.rows, self.sel = [], 0
        top = self.top = tk.Toplevel(app.root)
        top.overrideredirect(True)
        top.attributes("-topmost", True)
        top.configure(bg=ACCENT)
        card = tk.Frame(top, bg=SURFACE)
        card.pack(padx=1, pady=1)
        self.entry = tk.Entry(card, font=app.f_h2, bg=SURFACE, fg=TEXT, insertbackground=TEXT, relief="flat",
                              highlightthickness=0, width=34)
        self.entry.pack(fill="x", padx=S(18), pady=(S(16), S(4)), ipady=S(4))
        self.preview = tk.Label(card, text="일정을 그냥 적어 보세요  ·  예) 내일 오후 3시 회의 !중요", font=app.f_small,
                                bg=SURFACE, fg=DIM, anchor="w")
        self.preview.pack(fill="x", padx=S(20), pady=(0, S(8)))
        tk.Frame(card, bg=BORDER, height=1).pack(fill="x")
        self.list = tk.Frame(card, bg=SURFACE)
        self.list.pack(fill="x", padx=S(8), pady=S(6))
        tk.Label(card, text="Enter 실행  ·  ↑↓ 선택  ·  Esc 닫기", font=app.f_tiny, bg=SURFACE, fg=DIM,
                 anchor="e").pack(fill="x", padx=S(14), pady=(0, S(8)))
        self.entry.bind("<KeyRelease>", self.refresh)
        self.entry.bind("<Return>", lambda e: self.run())
        self.entry.bind("<Escape>", lambda e: self.close())
        self.entry.bind("<Down>", lambda e: self.move(1))
        self.entry.bind("<Up>", lambda e: self.move(-1))
        top.bind("<FocusOut>", lambda e: top.after(150, self._maybe_close))
        top.update_idletasks()
        w = top.winfo_reqwidth()
        vx, vy, vw, vh = virtual_screen(app.root)
        rx, ry = app.root.winfo_x() + app.root.winfo_width() // 2, app.root.winfo_y()
        x = max(vx + S(10), min(rx - w // 2, vx + vw - w - S(10)))
        y = max(vy + S(40), min(ry + S(90), vy + vh - S(420)))
        top.geometry(f"+{x}+{y}")
        top.after(30, self._focus)
        self.refresh()

    def _focus(self):
        try:
            self.top.focus_force()
            self.entry.focus_force()
        except tk.TclError:
            pass

    def _maybe_close(self):
        try:
            f = self.top.focus_get()
        except (tk.TclError, KeyError):
            f = None
        if f is None:
            self.close()

    def close(self):
        try:
            self.top.destroy()
        except tk.TclError:
            pass
        self.app.cmdbar = None

    def move(self, d):
        if self.rows:
            self.sel = (self.sel + d) % len(self.rows)
            self.draw()
        return "break"

    def _commands(self, q):
        app = self.app
        cmds = [("오늘로 이동", "달력", app.go_today), ("설정 열기", "설정", app.open_settings),
                ("휴대폰 연결 (QR)", "폰 연결 동기화", lambda: PhoneDialog(app)),
                ("공문 가져오기 (PDF·한글)", "공문 pdf 한글 hwp 가져오기", app.import_pdf),
                ("업무 관리 · 업무 폴더", "업무 공문 폴더 기한", app.open_work),
                ("반복 업무 추가", "반복 정기 템플릿 매월 분기", app.open_templates),
                ("보관함 열기", "보관함 인박스", app.open_inbox), ("사용법 보기", "도움말 사용법", app.open_help)]
        q = q.lower()
        return [(n, k, f) for n, k, f in cmds if q and (q in n.lower() or q in k.lower())]

    def _hits(self, q):
        app, out = self.app, []
        ql = q.lower()
        if len(ql) < 2:
            return out
        today = dt.date.today()
        for key, v in app.data.items():
            if key == "_deleted" or not isinstance(v, dict):
                continue
            d = core.parse_date(key) if key != "_inbox" else None
            for t in v.get("todos", []):
                if ql in t.get("text", "").lower() or ql in t.get("note", "").lower():
                    out.append((abs((d - today).days) if d else 9999, d, t))
            if d and ql in v.get("memo", "").lower():
                out.append((abs((d - today).days), d, {"text": "✎ " + v["memo"].strip().splitlines()[0][:40]}))
        out.sort(key=lambda x: x[0])
        return out[:6]

    def refresh(self, e=None):
        app = self.app
        q = self.entry.get().strip()
        self.rows = []
        self.parsed = None
        if q:
            r = self.parsed = quickadd.parse(q, dt.date.today())
            self.preview.config(text="→ " + (quickadd.describe(r) or "날짜 없음 (선택한 날짜에 추가)") + "   ·   " + r["text"],
                                fg=ACCENT)
            self.rows.append(("＋  일정으로 추가", quickadd.describe(r), self.add))
        else:
            self.preview.config(text="일정을 그냥 적어 보세요  ·  예) 내일 오후 3시 회의 !중요  /  매주 금요일 9시 주간회의", fg=DIM)
        try:
            clip_ok = len(self.app.root.clipboard_get().strip()) >= 6
        except tk.TclError:
            clip_ok = IS_WIN            # 이미지를 복사한 경우도 있을 수 있어 윈도우에서는 항상 보여 줌
        if clip_ok and (not q or len(q) < 3):
            self.rows.append(("📋  클립보드 내용으로 일정 만들기", "복사한 글·캡처", lambda: (self.close(), self.app.analyze_clipboard())))
        wd = self._workdays(q)
        if wd:
            self.rows.insert(0, wd)
        for name, _, fn in self._commands(q):
            self.rows.append(("▸  " + name, "명령", lambda fn=fn: (self.close(), fn())))
        for _, d, t in self._hits(q):
            when = f"{d.month}/{d.day}" if d else "보관함"
            self.rows.append(("🔎  " + t.get("text", ""), when, lambda d=d: (self.close(), self._open_date(d))))
        self.sel = 0
        self.draw()

    _WD_RE = re.compile(r"^(?:(.*?)\s*(?:부터|로부터|에서|기준)\s*)?(\d{1,3})\s*(?:근무일|영업일|업무일|일\s*근무)\s*(뒤|후|전)?$")

    def _workdays(self, q):
        """'7근무일', '10/20부터 5근무일 전' → 주말·공휴일을 뺀 날짜 계산"""
        m = self._WD_RE.match(q.strip())
        if not m:
            return None
        today = dt.date.today()
        base = quickadd.parse(m.group(1), today)["date"] if m.group(1) else today
        base = base or today
        n = int(m.group(2))
        if m.group(3) == "전":
            d = base
            k = n
            while k:
                d -= dt.timedelta(days=1)
                if core.is_workday(d, _hol):
                    k -= 1
        else:
            d = core.add_workdays(base, n, _hol)
        cal = (d - today).days
        label = f"📅  {work.fmt_date(base)} {'전' if m.group(3) == '전' else '부터'} 근무일 {n}일 → {work.fmt_date(d)}"
        return (label, f"달력으로 {cal:+d}일", lambda d=d: (self.close(), self.app.select_date(d)))

    def _open_date(self, d):
        if d is None:
            self.app.open_inbox()
        else:
            self.app.select_date(d)

    def draw(self):
        app, S = self.app, self.app.S
        for w in self.list.winfo_children():
            w.destroy()
        for i, (label, hint, fn) in enumerate(self.rows[:9]):
            on = i == self.sel
            row = tk.Frame(self.list, bg=CARD if on else SURFACE)
            row.pack(fill="x", pady=1)
            tk.Label(row, text=label, font=app.f_body, bg=row["bg"], fg=TEXT if on else SUB, anchor="w"
                     ).pack(side="left", padx=S(10), pady=S(5))
            tk.Label(row, text=hint, font=app.f_tiny, bg=row["bg"], fg=ACCENT if on else DIM, anchor="e"
                     ).pack(side="right", padx=S(10))
            for w in (row, *row.winfo_children()):
                w.bind("<Button-1>", lambda e, i=i: (setattr(self, "sel", i), self.run()))

    def run(self):
        if self.rows:
            self.rows[self.sel][2]()
        return "break"

    def add(self):
        app, r = self.app, self.parsed
        if not r or not r["text"]:
            return
        d = r["date"] or (app.selected if getattr(app, "_hl", False) else dt.date.today())
        extra = {}
        if r["time"]:
            extra["time"] = r["time"]
        if r["repeat"]:
            extra["repeat"] = {"f": r["repeat"], "n": 1}
        item = core.new_item(r["text"], pri=r["pri"] or DEFAULT_PRI, **extra)
        core.add_item(app.data, d.isoformat(), item)
        app._after_todo_change()
        self.close()

        def undo():
            core.delete_item(app.data, item)
            app._after_todo_change()
        when = f"{d.month}/{d.day}({'월화수목금토일'[d.weekday()]})" + (f" {r['time']}" if r["time"] else "")
        NotifyPopup(app, "일정 추가됨", f"{when}  {r['text']}", action=("실행 취소", undo))


class NotifyPopup:
    """화면 오른쪽 아래에 뜨는 알림 카드"""

    def __init__(self, app, title, body, on_snooze=None, action=None, actions=None, beep=True):
        self.app = app
        S = app.S
        top = self.top = tk.Toplevel(app.root)
        top.overrideredirect(True)
        top.attributes("-topmost", True)
        top.configure(bg=ACCENT)
        card = tk.Frame(top, bg=SURFACE)
        card.pack(padx=1, pady=1)
        tk.Label(card, text=title, font=app.f_head, bg=SURFACE, fg=ACCENT, anchor="w").pack(
            fill="x", padx=S(16), pady=(S(12), 0))
        tk.Label(card, text=body, font=app.f_body, bg=SURFACE, fg=TEXT, anchor="w", justify="left",
                 wraplength=S(300)).pack(fill="x", padx=S(16), pady=(S(4), S(10)))
        row = tk.Frame(card, bg=SURFACE)
        row.pack(fill="x", padx=S(16), pady=(0, S(12)))
        Pill(row, "확인", self.close, app.f_small, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(16), pady=S(4)).pack(side="left")
        if on_snooze:
            Pill(row, "10분 뒤", lambda: (on_snooze(), self.close()), app.f_small, parent_bg=SURFACE,
                 padx=S(14), pady=S(4)).pack(side="left", padx=S(8))
        for lab, fn in ([action] if action else []) + list(actions or []):
            Pill(row, lab, lambda fn=fn: (fn(), self.close()), app.f_small, parent_bg=SURFACE,
                 padx=S(14), pady=S(4)).pack(side="left", padx=(S(8), 0))
        top.update_idletasks()
        w, h = top.winfo_reqwidth(), top.winfo_reqheight()
        vx, vy, vw, vh = virtual_screen(app.root)
        off = sum(p.top.winfo_reqheight() + S(10) for p in app.popups)
        top.geometry(f"+{vx + vw - w - S(20)}+{vy + vh - h - S(70) - off}")
        app.popups.append(self)
        while len(app.popups) > 4:                     # 알림이 너무 쌓이면 오래된 것부터 닫는다
            app.popups[0].close()
        top.after(25000, self.close)
        if IS_WIN and beep:
            try:
                import winsound
                winsound.MessageBeep(0x40)
            except Exception:
                pass

    def close(self):
        if self in self.app.popups:
            self.app.popups.remove(self)
        try:
            self.top.destroy()
        except Exception:
            pass


class PhoneDialog:
    """휴대폰 연결: 연결 코드를 한 번만 붙여 넣으면 QR 이 바로 나온다 (저장소는 자동으로 만든다)"""

    def __init__(self, app):
        self.app = app
        S = app.S
        self.top = _dialog(app, "휴대폰 연결", S(460), S(420))
        self.build()

    def build(self):
        app, top = self.app, self.top
        S = app.S
        for w in top.winfo_children():
            w.destroy()
        top.configure(bg=SURFACE)
        pad = S(24)
        cfg = app.sync_cfg()
        top.geometry(f"{S(460)}x{S(660) if cfg else S(430)}")
        tk.Label(top, text="휴대폰과 연결", font=app.f_h2, bg=SURFACE, fg=TEXT, anchor="w"
                 ).pack(fill="x", padx=pad, pady=(S(16), S(2)))
        tk.Label(top, text="폰 앱에서 QR 만 스캔하면 끝. 집·사무실 밖에서도 일정이 같이 맞춰져요.\n"
                 "일정은 암호화되어 전달되고, 암호 키는 QR 로만 폰에 전해져요.", font=app.f_tiny, bg=SURFACE, fg=SUB,
                 anchor="w", justify="left", wraplength=S(410)).pack(fill="x", padx=pad)
        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=pad, pady=S(14))
        Pill(foot, "닫기", top.destroy, app.f_small, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(20), pady=S(5)).pack(side="right")
        if cfg:
            self.build_connected(cfg, foot)
        else:
            self.build_setup()

    def build_setup(self):
        app, top = self.app, self.top
        S = app.S
        pad = S(24)
        tk.Label(top, text="아래 버튼을 누르면 QR 코드가 만들어져요.\n폰 앱의 [설정] 탭 → 'QR 스캔해서 연결' 로 스캔하면 끝!",
                 font=app.f_body, bg=SURFACE, fg=TEXT, anchor="w", justify="left", wraplength=S(410)
                 ).pack(fill="x", padx=pad, pady=(S(20), S(12)))
        Pill(top, "QR 코드 만들기", self.connect, app.f_body, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(26), pady=S(8)
             ).pack(anchor="w", padx=pad, pady=S(4))
        self.msg = tk.Label(top, text="", font=app.f_small, bg=SURFACE, fg=SUN, anchor="w", justify="left",
                            wraplength=S(410))
        self.msg.pack(fill="x", padx=pad, pady=S(8))
        tk.Label(top, text="서버 주소 (보통 바꾸지 않아요)", font=app.f_tiny, bg=SURFACE, fg=DIM, anchor="w"
                 ).pack(fill="x", padx=pad, pady=(S(18), 0))
        self.e_url = tk.Entry(top, font=app.f_small, bg=CARD, fg=SUB, insertbackground=TEXT, relief="flat",
                              highlightthickness=1, highlightbackground=BORDER, highlightcolor=ACCENT)
        self.e_url.insert(0, cloudsync.DEFAULT_RELAY)
        self.e_url.pack(fill="x", padx=pad, ipady=S(4))

    def connect(self):
        url = self.e_url.get().strip().rstrip("/")
        if not url.startswith("http"):
            self.msg.config(text="서버 주소가 올바르지 않아요.", fg=SUN)
            return
        self.msg.config(text="연결하는 중…", fg=SUB)
        self.top.update_idletasks()
        cfg = {"relay": url, "id": cloudsync.new_id(), "key": cloudsync.new_key()}
        try:
            cloudsync.make_store(cfg).get()                  # 서버가 응답하는지 확인
        except cloudsync.SyncError as ex:
            self.msg.config(text=str(ex), fg=SUN)
            return
        self.app.conf["sync"] = cfg
        self.app._save_conf()
        self.app.sync_now()
        self.build()

    def build_connected(self, cfg, foot):
        app, top = self.app, self.top
        S = app.S
        pad = S(24)
        tk.Label(top, text="연결됨 ✓  폰 앱의 [설정] 탭 → 'QR 스캔해서 연결' 로 아래 QR 을 스캔하세요", font=app.f_small,
                 bg=SURFACE, fg=ACCENT, anchor="w", justify="left", wraplength=S(410)).pack(fill="x", padx=pad, pady=(S(12), 0))
        self.status = tk.Label(top, text=app.sync_status or "대기 중", font=app.f_small, bg=SURFACE, fg=SUB, anchor="w")
        self.status.pack(fill="x", padx=pad)
        tk.Label(top, text="이 QR 에는 연결 정보와 암호 키가 들어 있어요. 다른 사람에게 보여주지 마세요.", font=app.f_tiny, bg=SURFACE, fg=DIM, anchor="w", justify="left",
                 wraplength=S(450)).pack(fill="x", padx=pad, pady=(S(10), S(6)))
        if cfg.get("relay"):
            payload = json.dumps({"v": 2, "relay": cfg["relay"], "id": cfg["id"], "key": cfg["key"]},
                                 separators=(",", ":"))
        else:
            payload = json.dumps({"v": 1, "repo": cfg["repo"], "tok": cfg["token"], "key": cfg["key"],
                                  "p": cfg.get("path", "deskcalendar.enc")}, separators=(",", ":"))
        try:
            import segno
            m = [list(row) for row in segno.make(payload, error="m").matrix]
            n = len(m)
            cell = max(2, S(250) // (n + 2))
            size = cell * (n + 2)
            c = tk.Canvas(top, width=size, height=size, bg="#ffffff", highlightthickness=0)
            for y, row in enumerate(m):
                for x, v in enumerate(row):
                    if v:
                        c.create_rectangle((x + 1) * cell, (y + 1) * cell, (x + 2) * cell, (y + 2) * cell,
                                           fill="#000000", outline="")
            c.pack(pady=S(6))
        except Exception as ex:
            tk.Label(top, text=f"QR 만들기 실패: {ex}", font=app.f_small, bg=SURFACE, fg=SUN).pack()
        def copy_code():
            code = "DC1:" + base64.b64encode(payload.encode("utf-8")).decode()
            self.top.clipboard_clear()
            self.top.clipboard_append(code)
            messagebox.showinfo("연결 코드 복사됨", "연결 코드를 복사했어요.\n폰이나 웹앱의 [설정] → '연결 코드로' 칸에 붙여 넣으세요.\n(이 코드는 비밀번호처럼 다른 사람에게 보내지 마세요)", parent=self.top)
        Pill(foot, "연결 코드 복사", copy_code, app.f_small, parent_bg=SURFACE, padx=S(14), pady=S(6)).pack(side="left")
        Pill(foot, "지금 동기화", lambda: (app.sync_now(), self.top.after(1500, self.build)), app.f_small,
             parent_bg=SURFACE, padx=S(14), pady=S(6)).pack(side="left")
        Pill(foot, "연결 해제", self.disconnect, app.f_small, parent_bg=SURFACE, padx=S(14), pady=S(6)
             ).pack(side="left", padx=S(8))

    def disconnect(self):
        if messagebox.askyesno("연결 해제", "이 PC의 폰 연결을 끕니다. (서버에 암호화된 사본은 남아요)\n계속할까요?",
                               parent=self.top):
            self.app.conf.pop("sync", None)
            self.app._save_conf()
            self.build()


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
    """화면 설정: 작은 창 + 탭 (모양 / 글자 / 색 / 기타). 슬라이더는 움직이는 대로 바로 적용"""

    TABS = ("모양", "글자", "업무", "기타", "고급")

    def __init__(self, app):
        self.app = app
        S = app.S
        self.tab = "모양"
        self.f_val = tkfont.Font(family=app.family, size=-round(11 * 4 / 3 * app.scale))      # 설정 창은 읽기 편하게 따로 키운 글자
        self.f_hint = tkfont.Font(family=app.family, size=-round(9.5 * 4 / 3 * app.scale))
        self.f_lab = tkfont.Font(family=app.family, size=-round(12 * 4 / 3 * app.scale), weight="bold")
        self.top = _dialog(app, "설정", S(580), S(720))
        self.top.minsize(S(440), S(380))
        self.top.resizable(True, True)
        self.build()

    def build(self):
        app, top = self.app, self.top
        S = app.S
        for w in top.winfo_children():
            w.destroy()
        top.configure(bg=SURFACE)
        pad = S(16)
        # 위: 탭 줄
        bar = tk.Frame(top, bg=SURFACE)
        bar.pack(side="top", fill="x", padx=pad, pady=(S(12), S(4)))
        for name in self.TABS:
            on = name == self.tab
            Pill(bar, name, lambda n=name: self.set_tab(n), self.f_val, parent_bg=SURFACE, padx=S(12), pady=S(4),
                 **({"fg": "#ffffff", "bg": ACCENT, "hover": ACCENT} if on else {})).pack(side="left", padx=(0, S(4)))
        tk.Frame(top, bg=BORDER, height=1).pack(side="top", fill="x")
        # 아래: 닫기 / 기본값
        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=pad, pady=S(10))
        Pill(foot, "닫기", top.destroy, self.f_val, fg="#ffffff", bg=ACCENT, hover=blend(ACCENT, "#ffffff", 0.25),
             parent_bg=SURFACE, padx=S(16), pady=S(4)).pack(side="right")
        Pill(foot, "기본값", lambda: self.act(app.reset_appearance), self.f_val, parent_bg=SURFACE,
             padx=S(12), pady=S(4)).pack(side="left")
        cv = tk.Canvas(top, bg=SURFACE, highlightthickness=0, bd=0)
        cv.pack(side="top", fill="both", expand=True)
        body = tk.Frame(cv, bg=SURFACE)
        win = cv.create_window((0, 0), window=body, anchor="nw")
        body.bind("<Configure>", lambda e: cv.configure(scrollregion=cv.bbox("all")))
        cv.bind("<Configure>", lambda e: cv.itemconfigure(win, width=e.width))
        scroll = lambda e: cv.yview_scroll(int(-e.delta / 120), "units")
        top.bind("<MouseWheel>", scroll)
        self.body, self.pad = body, pad
        getattr(self, "tab_" + {"모양": "look", "글자": "text", "고급": "color", "기타": "misc", "업무": "work"}[self.tab])()
        for w in body.winfo_children():
            w.bind("<MouseWheel>", scroll)

    # ── 공통 조각 ──
    def set_tab(self, n):
        self.tab = n
        self.build()

    def row(self):
        f = tk.Frame(self.body, bg=SURFACE)
        f.pack(fill="x", padx=self.pad, pady=(self.app.S(8), 0))
        return f

    def pill(self, parent, text, cmd, on=False):
        S = self.app.S
        p = Pill(parent, text, cmd, self.f_val, parent_bg=SURFACE, padx=S(14), pady=S(5),
                 **({"fg": "#ffffff", "bg": ACCENT, "hover": ACCENT} if on else {}))
        p.pack(side="left", padx=(0, S(6)))
        return p

    def label(self, text, sub=None):
        app, S = self.app, self.app.S
        f = tk.Frame(self.body, bg=SURFACE)
        f.pack(fill="x", padx=self.pad, pady=(S(14), 0))
        tk.Label(f, text=text, font=self.f_lab, bg=SURFACE, fg=TEXT, anchor="w").pack(side="left")
        val = tk.Label(f, text="", font=self.f_val, bg=SURFACE, fg=SUB, anchor="e")
        val.pack(side="right")
        return val

    def slider(self, text, lo, hi, cur, live, fmt=lambda v: f"{int(round(v))}%", release=None, step=1):
        """text: 이름, live(v): 움직이는 동안 호출, release(v): 놓았을 때 호출"""
        app, S = self.app, self.app.S
        val = self.label(text)
        val.config(text=fmt(cur))
        sc = tk.Scale(self.body, from_=lo, to=hi, resolution=step, orient="horizontal", showvalue=False,
                      bg=ACCENT, fg=TEXT, troughcolor=CARD, highlightthickness=0, bd=0, sliderrelief="flat",
                      activebackground=ACCENT, length=S(240), width=S(16), sliderlength=S(26))
        sc.set(cur)
        sc.pack(fill="x", padx=self.pad, pady=(S(2), 0))

        def on_move(v):
            val.config(text=fmt(float(v)))
            if live:
                live(float(v))
        sc.config(command=on_move)
        if release:
            sc.bind("<ButtonRelease-1>", lambda e: release(float(sc.get())))
        return sc

    # ── 탭 ──
    def tab_look(self):
        app, S = self.app, self.app.S
        self.slider("투명도", 30, 100, int(round(app.alpha.get() * 100)), lambda v: app.set_alpha(v / 100))
        gs = float(app.conf.get("glass_strength", 0.6))
        self.slider("유리 진하기", 15, 95, int(round(gs * 100)),
                    None, release=lambda v: app.set_style(glass_strength=round(v / 100, 2)))
        self.slider("달력 칸 글자", 70, 240, int(round(float(app.conf.get("cal_font", 1.15)) * 100)),
                    None, release=lambda v: app.set_cal_font(v / 100))
        self.label("보이는 주")
        r = self.row()
        for n in (4, 5, 6):
            self.pill(r, f"{n}주", lambda n=n: self.act(app.set_style, weeks=n), on=app.weeks == n)
        self.label("유리 색")
        r = self.row()
        gt = app.conf.get("glass_tint", "그래파이트")
        for name in glass.TINT_ORDER:
            cv = tk.Canvas(r, width=S(24), height=S(24), bg=SURFACE, highlightthickness=0, cursor="hand2")
            rgb = glass.TINTS[name]
            cv.create_oval(S(3), S(3), S(21), S(21), fill="#%02x%02x%02x" % rgb, outline=TEXT if gt == name else BORDER,
                           width=S(2) if gt == name else 1)
            cv.pack(side="left", padx=(0, S(4)))
            cv.bind("<Button-1>", lambda e, n=name: self.act(app.set_style, glass_tint=n))
        self.label("음력 표시")
        r = self.row()
        lun = bool(app.conf.get("show_lunar", False))
        self.pill(r, "끔", lambda: self.act(app.set_style, show_lunar=False), on=not lun)
        self.pill(r, "켬", lambda: self.act(app.set_style, show_lunar=True), on=lun)
        tk.Frame(self.body, bg=SURFACE, height=S(10)).pack()

    def tab_text(self):
        app, S = self.app, self.app.S
        self.slider("전체 글자 크기", 70, 200, int(round(app.ui * 100)), None,
                    release=lambda v: (app.set_ui_scale(v / 100), self.build()))
        self.label("칸 안 줄 수 (카드 모양)")
        r = self.row()
        cl = int(app.conf.get("cal_lines", 0))
        for v, lab in ((0, "자동"), (1, "한 줄"), (2, "두 줄")):
            self.pill(r, lab, lambda v=v: self.act(app.update_conf_rebuild, cal_lines=v), on=cl == v)
        self.label("글꼴")
        r = self.row()
        tk.Label(r, text=app.family, font=self.f_val, bg=SURFACE, fg=ACCENT).pack(side="left", padx=(0, S(8)))
        self.pill(r, "변경…", lambda: FontDialog(app, on_done=self.build))
        self.label("글자 색")
        r = self.row()
        self.swatch(r, TEXT, S(22))
        self.pill(r, "선택…", lambda: self.pick("text_color", TEXT))
        self.pill(r, "기본", lambda: self.act(app.update_appearance, text_color=None))

    def tab_color(self):
        app, S = self.app, self.app.S
        tk.Label(self.body, text="자주 바꾸지 않는 꾸미기 설정이에요. 기본값으로 충분해요.", font=self.f_hint, bg=SURFACE, fg=DIM,
                 anchor="w").pack(fill="x", padx=self.pad, pady=(S(10), 0))
        self.label("보기 방식")
        r = self.row()
        st = app.conf.get("style", "glass")
        self.pill(r, "유리", lambda: self.act(app.set_style, style="glass"), on=st == "glass")
        self.pill(r, "카드", lambda: self.act(app.set_style, style="card"), on=st == "card")
        self.label("색상 테마 (카드 모양)")
        names = list(THEMES.items())
        for i in range(0, len(names), 3):                 # 3개씩 줄을 나눠 잘리지 않게
            r = self.row()
            for name, th in names[i:i + 3]:
                self.theme_card(r, name, th)
        self.label("강조색")
        r = self.row()
        for col in ACCENT_PRESETS:
            c = tk.Canvas(r, width=S(26), height=S(26), bg=SURFACE, highlightthickness=0, cursor="hand2")
            c.create_oval(S(3), S(3), S(23), S(23), fill=col, outline=TEXT if col == ACCENT else "", width=S(2))
            c.pack(side="left", padx=(0, S(4)))
            c.bind("<Button-1>", lambda e, col=col: self.act(app.update_appearance, accent=col))
        self.pill(r, "직접…", lambda: self.pick("accent", ACCENT))
        self.label("중요도 색")
        for i, (name, col) in list(enumerate(PRIORITIES))[1:]:
            r = self.row()
            r.pack_configure(pady=(S(4), 0))
            self.swatch(r, col, S(20))
            tk.Label(r, text=name, font=self.f_val, bg=SURFACE, fg=TEXT, width=8, anchor="w").pack(side="left")
            self.pill(r, "변경…", lambda i=i, col=col: self.pick_pri(i, col))

    def tab_misc(self):
        app, S = self.app, self.app.S
        self.label("일정 알림")
        r = self.row()
        notify = app.conf.get("notify", True)
        self.pill(r, "켬", lambda: self.act(app.update_conf, notify=True), on=notify)
        self.pill(r, "끔", lambda: self.act(app.update_conf, notify=False), on=not notify)
        self.label("미리 알림")
        r = self.row()
        lead = int(app.conf.get("lead", 10))
        for v, lab in ((0, "정시"), (5, "5분"), (10, "10분"), (30, "30분"), (60, "1시간")):
            self.pill(r, lab, lambda v=v: self.act(app.update_conf, lead=v), on=lead == v)
        self.label("아침 요약")
        r = self.row()
        brief = app.conf.get("brief", "09:00")
        for v, lab in (("", "끔"), ("08:00", "8시"), ("09:00", "9시"), ("10:00", "10시")):
            self.pill(r, lab, lambda v=v: self.act(app.update_conf, brief=v), on=brief == v)
        self.label("공휴일 API 키 (공공데이터포털)")
        r = self.row()
        self.hol_entry = tk.Entry(r, font=self.f_val, bg=CARD, fg=TEXT, insertbackground=TEXT, relief="flat",
                                  show="•", highlightthickness=1, highlightbackground=BORDER, highlightcolor=ACCENT)
        self.hol_entry.insert(0, app.conf.get("holiday_key", ""))
        self.hol_entry.pack(side="left", fill="x", expand=True, ipady=S(4), padx=(0, S(6)))
        self.pill(r, "저장", self.save_holiday_key)
        tk.Label(self.body, text=app.hol_status or "비워 두면 내장 규칙을 써요. 키는 이 PC에만 저장돼요.",
                 font=self.f_hint, bg=SURFACE, fg=DIM, anchor="w", justify="left", wraplength=S(500)
                 ).pack(fill="x", padx=self.pad, pady=(S(4), S(10)))

    def tab_work(self):
        app, S = self.app, self.app.S
        self.label("업무 폴더 위치")
        r = self.row()
        tk.Label(r, text=app.work_root(), font=self.f_hint, bg=SURFACE, fg=ACCENT, anchor="w", justify="left",
                 wraplength=S(300)).pack(side="left", fill="x", expand=True)
        self.pill(r, "변경…", self.pick_work_root)
        self.pill(r, "열기", lambda: (os.makedirs(app.work_root(), exist_ok=True), open_path(app.work_root())))
        tk.Label(self.body, text="공문 한 건마다 '연도\\월일 제목' 폴더가 생기고 그 안에 1.공문 · 2.작성 자료 · 3.제출본 "
                                 "폴더가 만들어져요.", font=self.f_hint, bg=SURFACE, fg=DIM, anchor="w",
                 justify="left", wraplength=S(500)).pack(fill="x", padx=self.pad, pady=(S(4), 0))
        self.label("공문을 넣으면 업무 폴더 만들기")
        r = self.row()
        on = app.conf.get("auto_case", True)
        self.pill(r, "켬", lambda: self.act(app.update_conf, auto_case=True), on=on)
        self.pill(r, "끔", lambda: self.act(app.update_conf, auto_case=False), on=not on)
        self.label("복사한 글에 날짜가 있으면 알려 주기")
        r = self.row()
        on = app.conf.get("clip_watch", False)
        self.pill(r, "켬", lambda: self.act(app.update_conf, clip_watch=True), on=on)
        self.pill(r, "끔", lambda: self.act(app.update_conf, clip_watch=False), on=not on)
        tk.Label(self.body, text="메신저에서 Ctrl+C 로 복사하면 오른쪽 아래에 '일정 n개를 찾았어요' 알림이 떠요. "
                                 "복사한 글은 이 PC 안에서만 읽고 어디에도 보내지 않아요.",
                 font=self.f_hint, bg=SURFACE, fg=DIM, anchor="w", justify="left", wraplength=S(500)
                 ).pack(fill="x", padx=self.pad, pady=(S(4), 0))
        tk.Frame(self.body, bg=SURFACE, height=S(10)).pack()

    def pick_work_root(self):
        p = filedialog.askdirectory(parent=self.top, title="업무 폴더를 만들 곳", initialdir=self.app.work_root())
        if p:
            self.act(self.app.update_conf, work_root=p)

    def act(self, fn, *a, **kw):
        fn(*a, **kw)
        self.build()

    def save_holiday_key(self):
        app = self.app
        app.update_conf(holiday_key=self.hol_entry.get().strip())
        app.refresh_holidays(force=True)
        self.top.after(3000, self.build)       # 가져온 결과 표시를 갱신

    def swatch(self, parent, col, size):
        c = tk.Canvas(parent, width=size, height=size, bg=SURFACE, highlightthickness=0)
        rrect(c, 1, 1, size - 1, size - 1, self.app.S(6), fill=col, outline=BORDER)
        c.pack(side="left", padx=(0, self.app.S(10)))

    def theme_card(self, parent, name, th):
        S = self.app.S
        w, h = S(110), S(62)
        c = tk.Canvas(parent, width=w, height=h, bg=SURFACE, highlightthickness=0, cursor="hand2")
        cur = self.app.conf.get("theme", "그래파이트") == name
        rrect(c, 1, 1, w - 1, h - 1, S(8), fill=th["BG"], outline=ACCENT if cur else th["BORDER"],
              width=S(2) if cur else 1)
        rrect(c, S(8), S(8), w - S(8), S(30), S(5), fill=th["CARD"], outline="")
        c.create_oval(S(12), S(14), S(20), S(22), fill=th["ACCENT"], outline="")
        c.create_text(w / 2, h - S(14), text=name, font=self.f_hint, fill=th["TEXT"])
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
            cols[0] = cols[1]
            self.act(self.app.update_appearance, pri_colors=cols)


def _scroll_area(parent, bg):
    """세로로 스크롤되는 영역 (canvas, 안쪽 프레임)"""
    cv = tk.Canvas(parent, bg=bg, highlightthickness=0, bd=0)
    inner = tk.Frame(cv, bg=bg)
    win = cv.create_window((0, 0), window=inner, anchor="nw")
    inner.bind("<Configure>", lambda e: cv.configure(scrollregion=cv.bbox("all")))
    cv.bind("<Configure>", lambda e: cv.itemconfigure(win, width=e.width))

    def wheel(e):
        if cv.winfo_height() < inner.winfo_reqheight():
            cv.yview_scroll(int(-e.delta / 120) or (-1 if e.delta > 0 else 1), "units")
    cv._wheel = wheel
    cv.bind("<MouseWheel>", wheel)
    return cv, inner


def _bind_wheel(widget, cv):
    for w in (widget, *widget.winfo_children()):
        w.bind("<MouseWheel>", cv._wheel)
        if w is not widget:
            _bind_wheel(w, cv)


def _flat_entry(app, parent, text="", font=None, width=None, bg=None):
    e = tk.Entry(parent, font=font or app.f_body, bg=bg or CARD, fg=TEXT, insertbackground=TEXT, relief="flat",
                 highlightthickness=1, highlightbackground=BORDER, highlightcolor=ACCENT,
                 **({"width": width} if width else {}))
    e.insert(0, text)
    return e


def _hol(d):
    return bool(holiday_name(d))


def dday_color(n):
    if n is None:
        return DIM
    if n < 0:
        return SUN
    if n <= 2:
        return PRIORITIES[1][1]
    return ACCENT


class WorkWindow:
    """업무 관리: 공문 한 건 = 업무 하나. 왼쪽 목록, 오른쪽에 기한·업무 폴더·연결된 일정·메모"""

    def __init__(self, app, select=None):
        self.app = app
        S = app.S
        self.show_done = False
        self.sel = select
        top = self.top = _dialog(app, "업무 관리", S(920), S(640))
        top.attributes("-topmost", False)
        top.minsize(S(720), S(480))
        top.resizable(True, True)
        top.protocol("WM_DELETE_WINDOW", self.close)
        top.bind("<Escape>", lambda e: self.close())
        self.left = tk.Frame(top, bg=BG, width=S(300))
        self.left.pack(side="left", fill="y")
        self.left.pack_propagate(False)
        tk.Frame(top, bg=BORDER, width=1).pack(side="left", fill="y")
        self.right = tk.Frame(top, bg=SURFACE)
        self.right.pack(side="left", fill="both", expand=True)
        self.drop_q = queue.Queue()
        self._drop_keep = {}
        self._scan_sig = None
        cs = work.sorted_cases(app.data)
        if self.sel is None and cs:
            self.sel = cs[0]["id"]
        self.build_left()
        self.build_right()
        top.after(300, self._hook)
        top.after(400, self._poll)

    # ── 공통 ──
    def case(self):
        return (self.app.data.get("_work") or {}).get(self.sel)

    def close(self):
        self._save_fields()
        try:
            self.top.destroy()
        except tk.TclError:
            pass
        self.app.workwin = None

    def changed(self, c=None, left=True):
        if c is not None:
            work.touch(c)
        self.app._after_todo_change()
        if left:
            self.build_left()

    def _hook(self):
        if not IS_WIN:
            return
        try:
            hs = [ctypes.windll.user32.GetParent(self.top.winfo_id()) or self.top.winfo_id(), self.top.winfo_id()]
            stack = [self.top]
            while stack:
                w = stack.pop()
                stack.extend(w.winfo_children())
                try:
                    hs.append(w.winfo_id())
                except tk.TclError:
                    pass
            hook_dropfiles(hs, self.drop_q, self._drop_keep)
        except Exception:
            pass

    def _poll(self):
        try:
            if not self.top.winfo_exists():
                return
        except tk.TclError:
            return
        try:
            while True:
                self.on_drop(self.drop_q.get_nowait())
        except queue.Empty:
            pass
        c = self.case()
        if c and c.get("folder"):                       # 폴더에 직접 넣은 파일도 목록에 반영
            sig = tuple(len(work.files(c, k)) for k in range(len(work.SUBDIRS)))
            if self._scan_sig is not None and sig != self._scan_sig:
                self.build_right()
            self._scan_sig = sig
        self.top.after(1500, self._poll)

    # ── 왼쪽: 업무 목록 ──
    def build_left(self):
        app, S = self.app, self.app.S
        for w in self.left.winfo_children():
            w.destroy()
        head = tk.Frame(self.left, bg=BG)
        head.pack(fill="x", padx=S(16), pady=(S(16), S(6)))
        tk.Label(head, text="업무", font=app.f_title, bg=BG, fg=TEXT).pack(side="left")
        Pill(head, "＋ 새 업무", self.new_case, app.f_small, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.2), parent_bg=BG, padx=S(12), pady=S(4)).pack(side="right")
        tabs = tk.Frame(self.left, bg=BG)
        tabs.pack(fill="x", padx=S(14), pady=(0, S(6)))
        allc = list((app.data.get("_work") or {}).values())
        n_on = sum(1 for c in allc if not c.get("done"))
        for lab, val in ((f"진행 중 {n_on}", False), (f"완료 {len(allc) - n_on}", True)):
            on = self.show_done == val
            Pill(tabs, lab, lambda v=val: self.set_filter(v), app.f_small, parent_bg=BG, padx=S(10), pady=S(3),
                 **({"fg": TEXT, "bg": CARD_HOVER, "hover": CARD_HOVER} if on else {"fg": SUB, "bg": BG, "hover": CARD})
                 ).pack(side="left", padx=(0, S(4)))
        cv, inner = _scroll_area(self.left, BG)
        cv.pack(fill="both", expand=True, padx=S(8), pady=(0, S(8)))
        items = [c for c in work.sorted_cases(app.data, include_done=True) if bool(c.get("done")) == self.show_done]
        if not items:
            tk.Label(inner, text=("완료한 업무가 없어요" if self.show_done else
                                  "공문(PDF·한글)을 달력이나 이 창에\n끌어다 놓으면 업무가 생겨요.\n\n"
                                  "업무마다 기한과 폴더(공문·작성 자료·\n제출본)가 만들어져요."),
                     font=app.f_small, bg=BG, fg=DIM, justify="left").pack(anchor="w", padx=S(10), pady=S(16))
        for c in items:
            self._case_card(inner, c)
        _bind_wheel(inner, cv)

    def _case_card(self, parent, c):
        app, S = self.app, self.app.S
        on = c["id"] == self.sel
        bg = CARD_HOVER if on else BG
        card = tk.Frame(parent, bg=bg, cursor="hand2")
        card.pack(fill="x", pady=S(2))
        bar = tk.Frame(card, bg=ACCENT if on else bg, width=S(3))
        bar.pack(side="left", fill="y")
        body = tk.Frame(card, bg=bg)
        body.pack(side="left", fill="x", expand=True, padx=(S(10), S(10)), pady=S(9))
        tk.Label(body, text=c.get("title", ""), font=app.f_body, bg=bg, fg=DIM if c.get("done") else TEXT,
                 anchor="w", justify="left", wraplength=S(240)).pack(fill="x")
        meta = tk.Frame(body, bg=bg)
        meta.pack(fill="x", pady=(S(3), 0))
        n = work.dday(c, app.today)
        if n is not None:
            tk.Label(meta, text=work.dday_label(n), font=app.f_tiny, bg=bg,
                     fg=DIM if c.get("done") else dday_color(n)).pack(side="left")
            tk.Label(meta, text="  " + work.fmt_date(c["due"]), font=app.f_tiny, bg=bg, fg=SUB).pack(side="left")
        for w in (card, bar, body, *body.winfo_children(), *meta.winfo_children()):
            w.bind("<Button-1>", lambda e, cid=c["id"]: self.select(cid))
            if not on:
                w.bind("<Enter>", lambda e, cs=(card, body, meta): [x.config(bg=CARD) for x in cs] +
                       [ch.config(bg=CARD) for x in (body, meta) for ch in x.winfo_children() if not isinstance(ch, tk.Canvas)])
                w.bind("<Leave>", lambda e, cs=(card, body, meta): [x.config(bg=bg) for x in cs] +
                       [ch.config(bg=bg) for x in (body, meta) for ch in x.winfo_children() if not isinstance(ch, tk.Canvas)])

    def set_filter(self, done):
        self.show_done = done
        self.build_left()

    def select(self, cid):
        self._save_fields()
        self.sel = cid
        self._scan_sig = None
        self.build_left()
        self.build_right()

    def new_case(self):
        c = work.add_case(self.app.data, work.new_case("새 업무", None))
        self.show_done = False
        self.select(c["id"])
        self.changed()
        try:
            self.e_title.focus_set()
            self.e_title.select_range(0, "end")
        except (AttributeError, tk.TclError):
            pass

    # ── 오른쪽: 업무 한 건 ──
    def _save_fields(self):
        c = self.case()
        if not c or not getattr(self, "e_title", None):
            return
        try:
            title = self.e_title.get().strip()
            due = self.e_due.get().strip()
            note = self.t_note.get("1.0", "end-1c").strip()
        except tk.TclError:
            return
        ch = False
        if title and title != c.get("title"):
            c["title"] = title
            ch = True
        d = core.parse_date(due) if due else None
        if due and not d:
            q = quickadd.parse(due, self.app.today)       # '10/20', '다음주 금요일' 도 받아 준다
            d = q["date"]
        nd = d.isoformat() if d else None
        if nd != c.get("due") and (d or not due):
            c["due"] = nd
            ch = True
        if note != c.get("note", ""):
            c["note"] = note
            ch = True
        if ch:
            self.changed(c)

    def section(self, parent, title, right=None):
        app, S = self.app, self.app.S
        f = tk.Frame(parent, bg=SURFACE)
        f.pack(fill="x", padx=S(24), pady=(S(18), S(6)))
        tk.Label(f, text=title, font=app.f_head, bg=SURFACE, fg=TEXT).pack(side="left")
        if right:
            tk.Label(f, text=right, font=app.f_small, bg=SURFACE, fg=SUB).pack(side="left", padx=S(8))
        return f

    def pill(self, parent, text, cmd, primary=False, side="left"):
        S = self.app.S
        p = Pill(parent, text, cmd, self.app.f_small, parent_bg=SURFACE, padx=S(12), pady=S(4),
                 **({"fg": "#ffffff", "bg": ACCENT, "hover": blend(ACCENT, "#ffffff", 0.2)} if primary else {}))
        p.pack(side=side, padx=(0, S(6)) if side == "left" else (S(6), 0))
        return p

    def build_right(self):
        app, S = self.app, self.app.S
        for w in self.right.winfo_children():
            w.destroy()
        self.e_title = None
        c = self.case()
        if not c:
            box = tk.Frame(self.right, bg=SURFACE)
            box.place(relx=0.5, rely=0.45, anchor="center")
            tk.Label(box, text="업무를 고르거나 새로 만들어 주세요", font=app.f_h2, bg=SURFACE, fg=SUB).pack()
            tk.Label(box, text="공문 PDF·한글 파일을 끌어다 놓으면 일정과 업무 폴더가 함께 만들어져요",
                     font=app.f_small, bg=SURFACE, fg=DIM).pack(pady=(S(6), 0))
            return
        cv, body = _scroll_area(self.right, SURFACE)
        cv.pack(fill="both", expand=True)
        # 제목 + 기한
        self.e_title = tk.Entry(body, font=app.f_title, bg=SURFACE, fg=TEXT, insertbackground=TEXT, relief="flat",
                                highlightthickness=0)
        self.e_title.insert(0, c.get("title", ""))
        self.e_title.pack(fill="x", padx=S(22), pady=(S(20), S(2)))
        self.e_title.bind("<Return>", lambda e: (self._save_fields(), self.top.focus_set()))
        self.e_title.bind("<FocusOut>", lambda e: self._save_fields())
        meta = tk.Frame(body, bg=SURFACE)
        meta.pack(fill="x", padx=S(24), pady=(S(4), 0))
        tk.Label(meta, text="기한", font=app.f_small, bg=SURFACE, fg=SUB).pack(side="left")
        self.e_due = _flat_entry(app, meta, c.get("due") or "", app.f_small, 11)
        self.e_due.pack(side="left", padx=(S(8), S(6)), ipady=S(3))
        self.e_due.bind("<Return>", lambda e: (self._save_fields(), self.build_right()))
        self.e_due.bind("<FocusOut>", lambda e: self._save_fields())
        n = work.dday(c, app.today)
        if n is not None:
            tk.Label(meta, text=f" {work.dday_label(n)} · {work.fmt_date(c['due'])} ", font=app.f_small,
                     bg=blend(dday_color(n), SURFACE, 0.18), fg=dday_color(n)).pack(side="left")
            if n > 0:
                wd = core.workdays_between(app.today, core.parse_date(c["due"]), _hol)
                tk.Label(meta, text=f"근무일 {wd}일 남음", font=app.f_tiny, bg=SURFACE, fg=DIM).pack(side="left", padx=S(8))
        else:
            tk.Label(meta, text="예) 2026-10-20 · 10/20 · 다음주 금요일", font=app.f_tiny, bg=SURFACE, fg=DIM
                     ).pack(side="left")
        info = []
        if c.get("doc_no"):
            info.append(f"문서 {c['doc_no']}")
        if c.get("received"):
            info.append(f"받은 날 {work.fmt_date(c['received'])}")
        if info:
            tk.Label(body, text="  ·  ".join(info), font=app.f_tiny, bg=SURFACE, fg=DIM, anchor="w"
                     ).pack(fill="x", padx=S(24), pady=(S(6), 0))
        acts = tk.Frame(body, bg=SURFACE)
        acts.pack(fill="x", padx=S(24), pady=(S(12), 0))
        self.pill(acts, "폴더 열기", self.open_folder, primary=True)
        self.pill(acts, "다시 진행" if c.get("done") else "완료로 표시", self.toggle_done)
        self.pill(acts, "삭제", self.delete, side="right")

        # 업무 폴더: 공문 · 작성 자료 · 제출본
        self.section(body, "업무 폴더")
        tk.Label(body, text="파일을 이 창에 끌어다 놓으면 원하는 칸에 넣을 수 있어요. 파일을 누르면 열려요.",
                 font=app.f_tiny, bg=SURFACE, fg=DIM, anchor="w").pack(fill="x", padx=S(24), pady=(0, S(6)))
        for k, name in enumerate(work.SUBDIRS):
            box = tk.Frame(body, bg=CARD, highlightthickness=1, highlightbackground=BORDER)
            box.pack(fill="x", padx=S(24), pady=S(3))
            hd = tk.Frame(box, bg=CARD)
            hd.pack(fill="x", padx=S(12), pady=(S(8), S(4)))
            fs = self._files(c, k)
            tk.Label(hd, text=name, font=app.f_small, bg=CARD, fg=TEXT).pack(side="left")
            tk.Label(hd, text=str(len(fs)) if fs else "", font=app.f_tiny, bg=CARD, fg=SUB).pack(side="left", padx=S(6))
            Pill(hd, "파일 넣기…", lambda k=k: self.pick_files(k), app.f_tiny, parent_bg=CARD, bg=SURFACE,
                 hover=BORDER, padx=S(8), pady=S(2)).pack(side="right")
            for f in fs[:8]:
                lb = tk.Label(box, text="  " + os.path.basename(f), font=app.f_small, bg=CARD, fg=ACCENT, anchor="w",
                              cursor="hand2")
                lb.pack(fill="x", padx=S(12))
                lb.bind("<Button-1>", lambda e, f=f: open_path(f))
            if len(fs) > 8:
                tk.Label(box, text=f"  외 {len(fs) - 8}개", font=app.f_tiny, bg=CARD, fg=DIM, anchor="w").pack(fill="x", padx=S(12))
            tk.Frame(box, bg=CARD, height=S(6)).pack()

        # 연결된 일정
        items = sorted(work.case_items(app.data, c["id"]), key=lambda x: x[0])
        h = self.section(body, "연결된 일정", str(len(items)) if items else "")
        self.pill(h, "기한을 일정으로", self.add_due_item, side="right")
        for key, t in items:
            d = core.parse_date(key)
            r = tk.Frame(body, bg=SURFACE, cursor="hand2")
            r.pack(fill="x", padx=S(24), pady=S(1))
            tk.Label(r, text=work.fmt_date(d) if d else "보관함", font=app.f_small, bg=SURFACE, fg=SUB, width=10,
                     anchor="w").pack(side="left")
            tk.Label(r, text=t.get("text", ""), font=app.f_body, bg=SURFACE,
                     fg=DIM if t.get("done") else TEXT, anchor="w").pack(side="left", fill="x", expand=True)
            for w in (r, *r.winfo_children()):
                w.bind("<Button-1>", lambda e, d=d: d and self.app.select_date(d))
        # 메모
        self.section(body, "메모")
        self.t_note = tk.Text(body, height=4, font=app.f_body, bg=CARD, fg=TEXT, insertbackground=TEXT, relief="flat",
                              wrap="word", padx=S(10), pady=S(8), highlightthickness=1, highlightbackground=BORDER,
                              highlightcolor=ACCENT)
        self.t_note.insert("1.0", c.get("note", ""))
        self.t_note.pack(fill="x", padx=S(24), pady=(0, S(24)))
        self.t_note.bind("<FocusOut>", lambda e: self._save_fields())
        _bind_wheel(body, cv)

    # ── 동작 ──
    def _folder(self, c):
        return work.ensure_folder(self.app.work_root(), c)

    def _files(self, c, k):
        return work.files(c, k)

    def open_folder(self):
        c = self.case()
        try:
            path = self._folder(c)
        except OSError as ex:
            messagebox.showerror("폴더", f"업무 폴더를 만들지 못했어요.\n{ex}", parent=self.top)
            return
        self.changed(c, left=False)
        open_path(path)

    def toggle_done(self):
        c = self.case()
        c["done"] = not c.get("done")
        self.changed(c)
        self.build_right()

    def delete(self):
        c = self.case()
        if not messagebox.askyesno("업무 삭제", f"'{c['title']}' 업무를 목록에서 지울까요?\n"
                                   "(업무 폴더와 파일, 달력의 일정은 그대로 남아요)", parent=self.top):
            return
        work.delete_case(self.app.data, c["id"])
        cs = work.sorted_cases(self.app.data)
        self.sel = cs[0]["id"] if cs else None
        self.changed()
        self.build_right()

    def pick_files(self, k):
        ps = filedialog.askopenfilenames(parent=self.top, title=f"'{work.SUBDIRS[k]}' 에 넣을 파일")
        if ps:
            self.put_files(list(ps), k)

    def put_files(self, ps, k):
        c = self.case()
        try:
            self._folder(c)
            for p in ps:
                work.copy_in(p, work.sub(c, k))
        except OSError as ex:
            messagebox.showerror("파일 넣기", str(ex), parent=self.top)
            return
        self._scan_sig = None
        self.changed(c, left=False)
        self.build_right()

    def add_due_item(self):
        c = self.case()
        d = core.parse_date(c.get("due"))
        if not d:
            messagebox.showinfo("기한", "먼저 기한 날짜를 적어 주세요.", parent=self.top)
            return
        text = f"{c['title']} 제출"
        if not any(t.get("text") == text for _, t, _ in core.day_items(self.app.data, d)):
            core.add_item(self.app.data, d.isoformat(), core.new_item(text, pri=1, case=c["id"]))
        self.changed(c)
        self.build_right()

    def on_drop(self, files):
        app = self.app
        docs = [f for f in files if f.lower().endswith((".pdf", ".hwp", ".hwpx", ".docx"))]
        c = self.case()
        if c is None or (len(files) == len(docs) and all(app.looks_like_gongmun(f) for f in docs)
                         and not any(os.path.basename(f) in map(os.path.basename, self._files(c, 0)) for f in docs)):
            app.import_dropped(files, open_case=False)       # 새 공문이면 새 업무로
            cs = work.sorted_cases(app.data)
            if cs:
                self.select(max(cs, key=lambda x: x.get("created", 0))["id"])
            return
        m = tk.Menu(self.top, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT, activeforeground="#ffffff", bd=0,
                    font=(app.family, 10))
        for k, name in enumerate(work.SUBDIRS):
            m.add_command(label=f"'{name}' 에 넣기 ({len(files)}개)", command=lambda k=k: self.put_files(files, k))
        x, y = self.top.winfo_pointerxy()
        m.tk_popup(x, y)


class TemplateDialog:
    """정기·반복 업무 모음에서 골라 한 번에 넣기 (기관마다 날짜가 다르니 이름·첫 날짜를 고칠 수 있다)"""

    def __init__(self, app):
        self.app = app
        S = app.S
        top = self.top = _dialog(app, "반복 업무 추가", S(640), S(620))
        top.resizable(True, True)
        tk.Label(top, text="반복 업무 추가", font=app.f_h2, bg=SURFACE, fg=TEXT, anchor="w").pack(
            fill="x", padx=S(20), pady=(S(16), 0))
        tk.Label(top, text="공무원이 자주 챙기는 정기 업무 예시예요. 필요한 것만 체크하고, 이름과 첫 날짜는 우리 기관에 맞게 고치세요.",
                 font=app.f_tiny, bg=SURFACE, fg=DIM, anchor="w", wraplength=S(590), justify="left").pack(
            fill="x", padx=S(20), pady=(S(2), S(8)))
        foot = tk.Frame(top, bg=SURFACE)
        foot.pack(side="bottom", fill="x", padx=S(20), pady=S(14))
        Pill(foot, "추가", self.add, app.f_body, fg="#ffffff", bg=ACCENT, hover=blend(ACCENT, "#ffffff", 0.2),
             parent_bg=SURFACE, padx=S(22), pady=S(6)).pack(side="right")
        Pill(foot, "취소", top.destroy, app.f_body, parent_bg=SURFACE, padx=S(14), pady=S(6)).pack(side="right", padx=S(8))
        cv, inner = _scroll_area(top, SURFACE)
        cv.pack(fill="both", expand=True, padx=S(20))
        self.rows = []
        existing = {t.get("text") for _, v in app.data.items() if isinstance(v, dict) for t in v.get("todos", [])
                    if t.get("repeat")}
        cat = None
        for c, text, rule in work.TEMPLATES:
            if c != cat:
                cat = c
                tk.Label(inner, text=c, font=app.f_head, bg=SURFACE, fg=SUB, anchor="w").pack(
                    fill="x", pady=(S(10), S(2)))
            d, code = work.template_first(rule, app.today)
            r = tk.Frame(inner, bg=SURFACE)
            r.pack(fill="x", pady=S(2))
            have = text in existing
            var = tk.BooleanVar(value=False)
            tk.Checkbutton(r, variable=var, bg=SURFACE, activebackground=SURFACE, selectcolor=CARD, bd=0,
                           highlightthickness=0, state="disabled" if have else "normal").pack(side="left")
            e = _flat_entry(app, r, text, app.f_small)
            e.pack(side="left", fill="x", expand=True, ipady=S(3), padx=(0, S(6)))
            de = _flat_entry(app, r, d.isoformat(), app.f_small, 11)
            de.pack(side="left", ipady=S(3), padx=(0, S(6)))
            tk.Label(r, text="추가됨" if have else work.rule_label(rule), font=app.f_tiny, bg=SURFACE,
                     fg=ACCENT if have else DIM, width=16, anchor="w").pack(side="left")
            self.rows.append((var, e, de, code))
        _bind_wheel(inner, cv)

    def add(self):
        app = self.app
        n = 0
        for var, e, de, code in self.rows:
            if not var.get() or not e.get().strip():
                continue
            d = core.parse_date(de.get().strip())
            if not d:
                messagebox.showwarning("날짜 확인", "첫 날짜는 2026-10-25 처럼 적어 주세요.", parent=self.top)
                return
            rep = core.repeat_from_code(code, d)
            core.add_item(app.data, d.isoformat(), core.new_item(e.get().strip(), repeat=rep))
            n += 1
        self.top.destroy()
        if n:
            app._after_todo_change()
            NotifyPopup(app, "반복 업무 추가", f"{n}개를 달력에 넣었어요. 각 일정을 눌러 날짜·반복을 바꿀 수 있어요.", beep=False)


class HelpDialog:
    TEXT_LINES = [
        ("달력 보기", "바탕화면이 비치는 유리 달력이에요. 위쪽 날짜 줄을 끌면 창이 옮겨지고, ‹ › 나 마우스 휠로 한 주씩, 왼쪽 아래 월 이름을 누르면 다음 달로 가요."),
        ("일정 입력", "날짜를 클릭하면 오른쪽에 입력창이 열려요. 쓰고 Enter. 같은 날짜를 다시 누르면 닫혀요."),
        ("시간 넣기", "'14:30 팀 회의' 처럼 앞에 시간을 쓰면 시간이 함께 저장돼요."),
        ("기간 일정", "시작 날짜에서 끝 날짜까지 끌어서 선택한 뒤 입력하면 여러 날에 걸친 일정이 돼요."),
        ("완료 / 삭제", "동그라미를 누르면 완료(취소선). ✕ 는 삭제, 목록에서 우클릭하면 더 많은 메뉴가 나와요."),
        ("중요도 색", "입력창 아래 색 점으로 중요도를 고르면 달력에도 그 색으로 보여요."),
        ("보관함", "날짜를 정하기 전의 할 일을 적어 뒀다가 '꺼내기'로 원하는 날짜에 옮겨요."),
        ("공문 → 업무", "공문(PDF·한글)을 달력에 끌어다 놓으면 일정이 추가되고, 업무 폴더(공문·작성 자료·제출본)가 만들어져요."),
        ("업무 폴더", "'업무'에서 공문마다 기한(D-day)을 보고, 공문·작성 자료·제출본 폴더에 파일을 정리해요. 파일을 끌어다 놓으면 넣을 칸을 골라요."),
        ("근무일 계산", "Ctrl+K 에 '7근무일' 이나 '10/20부터 5근무일' 을 쓰면 주말·공휴일을 뺀 날짜를 알려 줘요."),
        ("메신저 글", "메신저 글을 복사하고 Ctrl+Shift+V. 설정 → 업무에서 '복사한 글 알림'을 켜면 복사만 해도 알려 줘요."),
        ("창 크기", "창 가장자리나 모서리에 마우스를 가져가면 양방향 화살표가 나와요. 끌면 창 크기가 바뀌어요."),
        ("달력 글자", "위쪽 '달력 글자 －/＋' 또는 Ctrl+마우스 휠로 달력 칸 안 글자만 키우고 줄여요. (팝업·입력창은 그대로)"),
        ("글자·색", "위쪽 'Aa' 에서 글자 크기, 글꼴, 색상 테마, 강조색, 중요도 색, 투명도를 바꿔요."),
        ("스티커 메모", "'스티커' 버튼(또는 ☰ 메뉴)으로 바탕화면에 스티커를 붙여요. 그날의 일정과 메모가 한 장에 보여요. 제목줄을 끌면 옮겨지고(화면 끝·다른 스티커에 달라붙어요), 두 번 누르거나 ▴ 로 접어요. ● 에서 색·투명도, '고정'은 항상 위, ‹ › 로 날짜를 넘겨요. 메모는 달력의 그날 메모와 같아서 어디서 고쳐도 같이 바뀌어요."),
        ("빠른 입력", "Ctrl+Alt+Space 를 누르면 어디서든 오늘 날짜 입력창이 바로 열려요."),
    ]

    def __init__(self, app):
        S = app.S
        top = _dialog(app, "사용법", S(560), S(680))
        top.minsize(S(420), S(360))
        Pill(top, "확인", top.destroy, app.f_body, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), parent_bg=SURFACE, padx=S(26), pady=S(6)
             ).pack(side="bottom", pady=S(14))
        tk.Label(top, text="사용법", font=app.f_h2, bg=SURFACE, fg=TEXT, anchor="w").pack(
            side="top", fill="x", padx=S(24), pady=(S(18), S(8)))
        cv, inner = _scroll_area(top, SURFACE)       # 내용이 길면 스크롤: 글씨가 잘리지 않는다
        cv.pack(side="top", fill="both", expand=True, padx=(S(24), S(8)))
        labels = []
        for head, body in self.TEXT_LINES:
            f = tk.Frame(inner, bg=SURFACE)
            f.pack(fill="x", pady=S(5))
            tk.Label(f, text=head, font=app.f_head, bg=SURFACE, fg=ACCENT, width=12, anchor="nw"
                     ).pack(side="left", anchor="n", padx=(0, S(6)))
            lb = tk.Label(f, text=body, font=app.f_small, bg=SURFACE, fg=TEXT, anchor="w", justify="left")
            lb.pack(side="left", fill="x", expand=True)
            labels.append(lb)

        def fit(e=None):                             # 창 너비에 맞춰 줄바꿈
            w = max(S(200), cv.winfo_width() - S(170))
            for lb in labels:
                lb.config(wraplength=w)
        cv.bind("<Configure>", lambda e: (cv.itemconfigure(cv.find_all()[0], width=e.width), fit()))
        _bind_wheel(inner, cv)
        top.bind("<MouseWheel>", cv._wheel)


# ── 앱 ──────────────────────────────────────────────────
class DeskCalendar:
    HOTKEY_LABEL = "Ctrl+Alt+Space"
    # (이름, 기본 pt, 굵게)
    FONT_SPEC = {"title": (14, True), "h2": (12, True), "head": (8, True), "day": (9, True),
                 "tiny": (7, False), "small": (8, False), "body": (9, False),
                 "chip": (8, False)}

    def __init__(self, root, open_pdfs=None):
        self.root = root
        self.data = load_json(DATA_FILE, {})
        self.conf = load_json(CONF_FILE, {})
        if core.ensure_ids(self.data):
            try:
                save_json(DATA_FILE, self.data)
            except Exception:
                pass
        auto_backup()
        self.hol_status = ""
        self._hol_cache = {}
        if self.conf.get("look_v", 0) < 3:     # 새 디자인: 예전에 저장된 글자 크기·모양 값은 한 번 초기화
            for k in ("scale", "cal_font", "cal_lines", "style", "weeks", "glass_tint", "glass_strength",
                      "w", "h", "x", "y", "alpha"):
                self.conf.pop(k, None)
            self.conf["look_v"] = 3
            save_json(CONF_FILE, self.conf)
        apply_conf_theme(self.conf)
        self.dpi_scale = max(1.0, root.winfo_fpixels("1i") / 96.0)
        self.ui = min(2.0, max(0.7, float(self.conf.get("scale", 0.9))))
        self.scale = self.dpi_scale * self.ui
        self.panel_w = self.S(290) + 1
        if self.conf.get("size_v", 1) < 2:            # 예전 버전에서 저장된 작은 창 크기는 버린다
            for k in ("w", "h", "x", "y"):
                self.conf.pop(k, None)
            self.conf["size_v"] = 2
        sw, sh = root.winfo_screenwidth(), root.winfo_screenheight()
        self.win_w = int(self.conf.get("w", min(self.S(1400), int(sw * 0.78))))   # 입력 패널을 뺀 창 너비
        self.win_h = int(self.conf.get("h", min(self.S(1000), sh - self.S(90))))
        self.detail = None
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
        self.glass = self.conf.get("style", "glass") == "glass"   # 유리(데스크탑) / 카드
        self.weeks = int(self.conf.get("weeks", 5))               # 유리 모양에서 보이는 주 수
        self.view_start = self._default_view_start()
        self._gimg, self._gkey, self._gbtns, self._gcells = None, None, [], []
        self._ghdr, self._ghover, self._gresize_job = None, None, None
        self.gm = {}
        self._summoned = False     # Ctrl+Alt+Space 로 불러낸 동안만 앞에 둔다
        self._tip, self._tip_job = None, None
        self._last_click = None
        self._move = None          # 달력에서 일정 칩을 끌어 옮기는 중: (항목, 눌렀던 날짜)
        self._move_to = None
        self._hits = []
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
        self.tray_q = queue.Queue()
        self.drop_q = queue.Queue()
        self.cmdbar = None
        self._drop_keep = {}
        self.tray = None
        self.sync_q = queue.Queue()
        self._syncing, self._sync_job, self._sync_retry = False, None, 0
        self._in_sync = False
        self.sync_status = ""
        self.popups, self._snooze = [], {}
        self.workwin = None
        self._clip_last = self._own_clip = None

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
        self.f_chip_done = tkfont.Font(family=self.family, size=-1, overstrike=True)
        self._apply_fonts()
        self.f_chip = self.fonts["chip"]
        self.f_title, self.f_h2, self.f_head = self.fonts["title"], self.fonts["h2"], self.fonts["head"]
        self.f_day, self.f_tiny = self.fonts["day"], self.fonts["tiny"]
        self.f_small, self.f_body = self.fonts["small"], self.fonts["body"]

        self._setup_window()
        self._build_menu()
        self._build_ui()
        self.show_day(self.selected)
        self.render_month()

        root.bind_all("<Control-MouseWheel>", self._on_ctrl_wheel)
        root.bind_all("<Control-k>", lambda e: self.open_cmdbar())
        root.bind_all("<Control-Shift-V>", lambda e: self.analyze_clipboard())
        root.bind_all("<Control-Shift-v>", lambda e: self.analyze_clipboard())
        root.bind_all("<Control-K>", lambda e: self.open_cmdbar())
        root.bind_all("<ButtonRelease-1>", lambda e: root.after(40, self._bottom_soon), add="+")
        if not self.conf.get("seen_help"):
            root.after(900, self._first_help)
        self._load_holiday_cache()
        root.after(6000, self.refresh_holidays)
        root.after(300, self._poll_sync)
        root.after(3000, self.sync_now)
        root.after(30 * 1000, self._sync_tick)
        root.after(8000, self._check_reminders)
        root.after(1000, self._poll_requests)
        if open_pdfs:
            root.after(1500, lambda: self.import_path(open_pdfs[0]))
        root.after(300, self._apply_desktop_mode)
        self.stickers = sticky.StickyManager(self, sys.modules[__name__])
        root.after(500, self.stickers.restore)
        root.after(30_000, self._tick)
        root.after(2000, self._clip_watch)
        if IS_WIN:
            start_hotkey_listener(self.hotkey_q, 0x0002 | 0x0001, 0x20)  # Ctrl+Alt+Space
            self.tray = start_tray(self.tray_q)
            root.after(150, self._poll_hotkey)

    def S(self, v):
        return int(round(v * self.scale))

    # ── 글꼴 / 크기 ──
    def _apply_fonts(self):
        cal = float(self.conf.get("cal_font", 1.15))   # 달력 칸 글자 배율
        for k, (pt, _) in self.FONT_SPEC.items():
            f = cal if k == "chip" else 1.0
            self.fonts[k].configure(family=self.family, size=-max(8, round(pt * f * 4 / 3 * self.scale)))
        self.f_done.configure(family=self.family, size=-max(8, round(9 * 4 / 3 * self.scale)))
        self.f_tiny_done.configure(family=self.family, size=-max(8, round(7 * 4 / 3 * self.scale)))
        self.f_chip_done.configure(family=self.family, size=-max(8, round(9 * cal * 4 / 3 * self.scale)))
        self.unit = self.fonts["chip"].metrics("linespace") + self.S(5)     # 칸 안 한 줄이 차지하는 높이
        self.chip_h = self.unit - self.S(2)
        self.pitch = self.unit
        self.chip_lines = 1
        self.head_h = self.S(27)                                            # 날짜 숫자가 차지하는 높이

    def set_cal_font(self, v):
        v = round(min(2.4, max(0.7, v)), 2)
        self._scale_target = v
        self.conf["cal_font"] = v
        self._save_conf()
        self._apply_fonts()
        self._relayout_cal()

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
        if getattr(self, "stickers", None) is not None:
            self.stickers.reopen()
        self._geom()
        self.show_day(self.selected)
        self.render_month()
        self._hook_drop()

    def _on_ctrl_wheel(self, e):
        step = 0.05 if e.delta > 0 else -0.05
        target = getattr(self, "_scale_target", float(self.conf.get("cal_font", 1.15))) + step
        self._scale_target = min(2.4, max(0.7, target))
        if self._scale_job:
            self.root.after_cancel(self._scale_job)
        self._scale_job = self.root.after(60, self._apply_scale_target)

    def _apply_scale_target(self):
        self._scale_job = None
        self.set_cal_font(self._scale_target)

    def cal_font_step(self, d):
        self.set_cal_font(float(self.conf.get("cal_font", 1.15)) + d)

    def _relayout_cal(self):
        """달력 칸 안 글자 크기가 바뀌었을 때 달력만 다시 배치"""
        self._on_cal_resize(type("E", (), {"width": self.cal.winfo_width(), "height": self.cal.winfo_height()})())

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
        return self.S(300), self.S(260)

    def _fit_rect(self, x, y, w, h):
        """창 전체가 화면 안에 들어오도록 크기와 위치를 맞춘다"""
        ax, ay, aw, ah = work_area(self.root, x + w // 2, y + h // 2)
        w, h = min(w, aw), min(h, ah)
        return min(max(ax, x), ax + aw - w), min(max(ay, y), ay + ah - h), w, h

    def _geom(self, x=None, y=None):
        r = self.root
        pw = 0
        x = r.winfo_x() if x is None else x
        y = r.winfo_y() if y is None else y
        x, y, w, h = self._fit_rect(x, y, self.win_w + pw, self.win_h)
        self.win_w, self.win_h = w - pw, h
        r.geometry(f"{w}x{h}+{x}+{y}")

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
        w = e.widget
        S = self.S
        if self.glass and w is self.cal:
            cw_, ch_ = w.winfo_width(), w.winfo_height()
            m, c = S(6), S(22)
            east = True
            nx = "w" if e.x < m else "e" if (east and e.x >= cw_ - m) else ""
            ny = "n" if e.y < m else "s" if e.y >= ch_ - m else ""
            if nx and e.y < c:
                ny = "n"
            if nx and e.y >= ch_ - c:
                ny = "s"
            if ny and e.x < c:
                nx = "w"
            if ny and east and e.x >= cw_ - c:
                nx = "e"
            return ny + nx
        if self.glass and w is getattr(self, "east_strip", None):
            hh = w.winfo_height()
            return ("n" if e.y < S(22) else "s" if e.y >= hh - S(22) else "") + "e"
        if w is not r:
            return ""
        w, h = r.winfo_width(), r.winfo_height()
        m, c = S(8), S(22)
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
        pw = 0
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
        ax, ay, aw, ah = work_area(self.root, x + w // 2, y + h // 2)   # 화면 밖으로는 늘어나지 않게
        if x < ax:
            w, x = w - (ax - x), ax
        if x + w > ax + aw:
            w = ax + aw - x
        if y < ay:
            h, y = h - (ay - y), ay
        if y + h > ay + ah:
            h = ay + ah - y
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
        if self.glass:    # 크기를 끄는 동안에는 잠깐 쉬었다가 한 번만 다시 그린다
            if self._gresize_job:
                self.root.after_cancel(self._gresize_job)
            self._gresize_job = self.root.after(70, self.render_month)
            return
        S, gap = self.S, self.gap
        self.cw = max(S(44), (e.width - 6 * gap - 2) // 7)
        self.ch = max(S(46), (e.height - self.wd_h - 5 * gap) // 6)
        mode = int(self.conf.get("cal_lines", 0))   # 0=자동(칸이 크면 두 줄), 1, 2
        self.chip_lines = mode or (2 if self.ch >= S(105) else 1)
        self.nslot = max(1, min(10, (self.ch - self.head_h - S(3)) // self.unit))   # 칸 안에 들어가는 줄 수
        self.render_month()

    # ── UI 구성 ──
    def _build_ui(self):
        if self.outer is not None:
            self.outer.destroy()
        S = self.S
        self.cw, self.ch, self.gap = S(78), S(80), S(5)
        self.wd_h = S(22)

        outer = self.outer = tk.Frame(self.root, bg=BG)
        outer.pack(fill="both", expand=True, padx=S(8), pady=S(8))

        # 맨 위 이동 막대 (잡고 끌면 창 이동) + 설정 버튼
        bar = tk.Frame(outer, bg=BG, height=S(20))
        bar.pack(side="top", fill="x")
        bar.pack_propagate(False)
        grab = tk.Frame(bar, bg=BG, cursor="fleur")      # 중요도 색 안내를 겸한 이동 막대
        grab.pack(side="left", fill="y", padx=S(8))
        legend = []
        for name, col in PRIORITIES[1:]:
            legend.append(tk.Label(grab, text="●", font=self.f_tiny, bg=BG, fg=col, cursor="fleur"))
            legend.append(tk.Label(grab, text=name, font=self.f_tiny, bg=BG, fg=SUB, cursor="fleur"))
        for i, lb in enumerate(legend):
            lb.pack(side="left", padx=(0, S(2)) if i % 2 == 0 else (0, S(9)))
        tools = tk.Frame(bar, bg=BG)
        tools.place(relx=1.0, x=-S(2), rely=0.5, anchor="e")

        def tool(text, cmd):
            Pill(tools, text, cmd, self.f_tiny, fg=SUB, bg=BG, hover=CARD_HOVER, parent_bg=BG,
                 padx=S(10), pady=S(4)).pack(side="left", padx=S(1))

        tool("업무", self.open_work)
        tool("Aa 화면 설정", self.open_settings)
        tool("? 사용법", self.open_help)
        tool("⚙", lambda: self.menu.tk_popup(*self.root.winfo_pointerxy()))
        for w in (bar, grab, *legend):
            self._bind_drag(w)

        self.right = tk.Frame(outer, bg=SURFACE, width=S(290))
        self.right.pack_propagate(False)
        self.sep = tk.Frame(outer, bg=BORDER, width=1)
        left = self.left = tk.Frame(outer, bg=BG, padx=S(8), pady=S(2))
        left.pack(side="left", fill="both", expand=True)

        # 헤더 (한 줄로 줄여서 달력 칸에 공간을 더 준다)
        hdr = tk.Frame(left, bg=BG)
        hdr.pack(fill="x", pady=(0, S(4)))
        self.title_lbl = tk.Label(hdr, font=self.f_h2, bg=BG, fg=TEXT, cursor="fleur")
        self.title_lbl.pack(side="left")
        self.month_sub = tk.Label(hdr, font=self.f_small, bg=BG, fg=SUB, cursor="fleur")
        self.month_sub.pack(side="left", padx=(S(10), 0))
        nav = tk.Frame(hdr, bg=BG)
        nav.pack(side="right")
        small = dict(padx=S(9), pady=S(2))
        Pill(nav, "보관함", self.open_inbox, self.f_small, **small).pack(side="left", padx=(0, S(4)))
        Pill(nav, "공문 PDF", self.import_pdf, self.f_small, **small).pack(side="left", padx=(0, S(10)))
        Pill(nav, "달력 글자 －", lambda: self.cal_font_step(-0.1), self.f_tiny, **small).pack(side="left", padx=(0, S(3)))
        Pill(nav, "＋", lambda: self.cal_font_step(0.1), self.f_tiny, **small).pack(side="left", padx=(0, S(10)))
        Pill(nav, "‹", lambda: self.shift_month(-1), self.f_body, padx=S(10), pady=S(1)).pack(side="left")
        Pill(nav, "오늘", self.go_today, self.f_small, fg="#ffffff", bg=ACCENT,
             hover=blend(ACCENT, "#ffffff", 0.25), padx=S(11), pady=S(2)).pack(side="left", padx=S(4))
        Pill(nav, "›", lambda: self.shift_month(1), self.f_body, padx=S(10), pady=S(1)).pack(side="left")
        for w in (left, hdr, self.title_lbl, self.month_sub):
            self._bind_drag(w)
        if self.glass:
            bar.pack_forget()
            hdr.pack_forget()
            outer.pack_configure(padx=0, pady=0)
            left.config(padx=0, pady=0)
            self.east_strip = tk.Frame(outer, bg=BG, width=S(5), cursor=self._CURSORS_WIN["e"] if IS_WIN else self._CURSORS_X11["e"])
            for ev, fn in (("<ButtonPress-1>", self._resize_start), ("<B1-Motion>", self._resize_move),
                           ("<ButtonRelease-1>", self._resize_end)):
                self.east_strip.bind(ev, fn)

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
        self.tab_agenda = self._tab(tabs, "예정", lambda: self.set_mode("agenda"))
        self.tab_inbox = self._tab(tabs, "보관함", lambda: self.set_mode("inbox"))
        self.tab_search = self._tab(tabs, "검색", lambda: self.set_mode("search"))
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
        self.todo_entry.bind("<KeyRelease>", lambda e: self.render_todos() if self.mode == "search" else None)
        self.root.bind_all("<Control-f>", lambda e: self.open_search())
        self.todo_entry.bind("<Escape>", lambda e: self._escape_entry())
        self.todo_entry.bind("<Button-1>", lambda e: self.todo_entry.focus_force(), add="+")
        self.todo_entry.bind("<FocusIn>", lambda e: self.box.config(bg=ACCENT), add="+")
        self.todo_entry.bind("<FocusOut>", lambda e: self.box.config(bg=BORDER), add="+")
        for n in range(1, 4):
            self.todo_entry.bind(f"<Control-Key-{n}>", lambda e, n=n: self._pick_pri(n))
        self._placeholder(self.todo_entry, "")

        # 중요도 선택
        self.prow = prow = tk.Frame(inner, bg=SURFACE)
        tk.Label(prow, text="중요도", font=self.f_tiny, bg=SURFACE, fg=SUB).pack(side="left")
        self.pri_dots = []
        for n, (name, col) in enumerate(PRIORITIES):
            cv = tk.Canvas(prow, width=S(20), height=S(20), bg=SURFACE, highlightthickness=0,
                           cursor="hand2")
            if n:
                cv.pack(side="left", padx=(S(6) if n == 1 else 0, 0))
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
        lb = tk.Label(parent, text=text, font=self.f_small, padx=self.S(7), pady=self.S(3),
                      cursor="hand2")
        lb.pack(side="left", padx=(0, self.S(3)))
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
        m.add_command(label="검색  (Ctrl+F)", command=self.open_search)
        m.add_separator()
        m.add_command(label="스티커 메모 만들기 (오늘)", command=lambda: self.stickers.add())
        m.add_command(label="선택한 날짜를 스티커로", command=lambda: self.stickers.add(self.selected))
        m.add_separator()
        m.add_command(label="업무 관리 (기한·업무 폴더)…", command=self.open_work)
        m.add_command(label="반복 업무 추가 (정기 업무 모음)…", command=self.open_templates)
        m.add_separator()
        m.add_command(label="휴대폰 연결…", command=lambda: PhoneDialog(self))
        m.add_command(label="공문 가져오기 (PDF·한글)…", command=self.import_pdf)
        m.add_command(label="클립보드 내용으로 일정 만들기  (Ctrl+Shift+V)", command=self.analyze_clipboard)
        bm = tk.Menu(m, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT, activeforeground="#ffffff",
                     bd=0, font=f)
        bm.add_command(label="백업 파일 저장…", command=self.backup_save)
        bm.add_command(label="백업에서 복원…", command=self.backup_restore)
        bm.add_command(label="캘린더(.ics) 내보내기…", command=self.export_ics)
        m.add_cascade(label="백업 / 복원 / 내보내기", menu=bm)
        m.add_command(label="창을 화면에 크게 맞추기", command=self.fit_screen)
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
        self.root.bind_all("<Button-3>", lambda e: None if getattr(e.widget.winfo_toplevel(), "_sticky", False)
                           else m.tk_popup(e.x_root, e.y_root))

    # ── 인터넷 동기화 (백그라운드 스레드는 네트워크만, 데이터 합치기는 이 스레드에서) ──
    def sync_cfg(self):
        c = self.conf.get("sync")
        if not c or not c.get("key"):
            return None
        return c if (c.get("relay") and c.get("id")) or (c.get("repo") and c.get("token")) else None

    def _schedule_sync(self):
        if not self.sync_cfg():
            return
        if self._sync_job:
            self.root.after_cancel(self._sync_job)
        self._sync_job = self.root.after(4000, self.sync_now)

    def _sync_tick(self):
        self.sync_now()
        self.root.after(30 * 1000, self._sync_tick)

    def sync_now(self):
        self._sync_job = None
        cfg = self.sync_cfg()
        if not cfg or self._syncing:
            return
        self._syncing = True
        self.sync_status = "동기화 중…"
        store = cloudsync.make_store(cfg)

        def job():
            try:
                remote, sha = cloudsync.pull(store, cfg["key"])
                self.sync_q.put(("pulled", remote, sha))
            except Exception as ex:
                self.sync_q.put(("error", str(ex)))

        threading.Thread(target=job, daemon=True).start()

    def _poll_sync(self):
        try:
            while True:
                self._on_sync_msg(self.sync_q.get_nowait())
        except queue.Empty:
            pass
        self.root.after(300, self._poll_sync)

    def _finish_sync(self, text):
        self._syncing = False
        self._sync_retry = 0
        self.sync_status = f"{text} · {dt.datetime.now():%H:%M}"

    # ── 공휴일 (공공데이터포털 API, 설정에 서비스키를 넣었을 때만) ──
    def _load_holiday_cache(self):
        self._hol_cache = holidays_api.load_cache(HOLIDAY_FILE)
        for y, ent in self._hol_cache.items():
            try:
                holidays_kr.set_override(int(y), ent.get("items"))
            except Exception:
                pass

    def refresh_holidays(self, force=False):
        key = (self.conf.get("holiday_key") or "").strip()
        if not key:
            return
        started = False
        for y in (self.today.year, self.today.year + 1):
            if not force and holidays_api.is_fresh(self._hol_cache.get(str(y))):
                continue
            started = True

            def job(y=y):
                try:
                    self.sync_q.put(("holidays", y, holidays_api.fetch_year(key, y), None))
                except Exception as ex:
                    self.sync_q.put(("holidays", y, None, str(ex)))

            threading.Thread(target=job, daemon=True).start()
        if started:
            self.hol_status = "공휴일 가져오는 중…"

    def _on_holidays(self, y, items, err):
        now = dt.datetime.now().strftime("%H:%M")
        if items:
            holidays_kr.set_override(y, items)
            self._hol_cache[str(y)] = {"fetched": time.time(), "items": items}
            holidays_api.save_cache(HOLIDAY_FILE, self._hol_cache)
            self.hol_status = f"{y}년 공휴일 {len(items)}일 반영됨 · {now}"
            self.render_month()
            self.render_todos()
        elif err:
            self.hol_status = f"{y}년: {err} · {now}"
        else:
            self.hol_status = f"{y}년: 받은 공휴일이 없어 내장 규칙을 써요 · {now}"

    def _on_sync_msg(self, msg):
        if msg[0] == "holidays":
            self._on_holidays(msg[1], msg[2], msg[3])
            return
        cfg = self.sync_cfg()
        if not cfg:
            self._syncing = False
            return
        kind = msg[0]
        if kind == "pulled":
            remote, sha = msg[1], msg[2]
            if remote:
                self._in_sync = True
                try:
                    if core.merge_state(self.data, remote):
                        self._commit()
                        self.render_todos()
                        self.render_month()
                finally:
                    self._in_sync = False
            local = core.export_state(self.data)
            if remote is not None and cloudsync.canonical(local) == cloudsync.canonical(remote):
                return self._finish_sync("동기화 완료")
            store = cloudsync.make_store(cfg)

            def job():
                try:
                    cloudsync.push(store, cfg["key"], local, sha)
                    self.sync_q.put(("pushed",))
                except cloudsync.Conflict:
                    self.sync_q.put(("conflict",))
                except Exception as ex:
                    self.sync_q.put(("error", str(ex)))

            threading.Thread(target=job, daemon=True).start()
        elif kind == "pushed":
            self._finish_sync("동기화 완료")
        elif kind == "conflict":      # 그 사이 다른 기기가 올림 → 다시 받아서 합치기
            self._syncing = False
            self._sync_retry += 1
            if self._sync_retry <= 3:
                self.sync_now()
            else:
                self._finish_sync("충돌이 잦아 잠시 후 다시 시도해요")
        else:
            self._syncing = False
            self.sync_status = f"{msg[1]} · {dt.datetime.now():%H:%M}"

    # ── 알림 ──
    def _check_reminders(self):
        try:
            self._reminders()
        except Exception:
            pass
        self.root.after(20_000, self._check_reminders)

    def _reminders(self):
        if not self.conf.get("notify", True):
            return
        now = dt.datetime.now()
        today = now.date()
        n = self.conf.get("notified") or {}
        if n.get("date") != today.isoformat():
            n = {"date": today.isoformat(), "keys": []}
        self.conf["notified"] = n
        lead = int(self.conf.get("lead", 10))
        rows = core.day_items(self.data, today)
        for lst, t, s0 in rows:
            tm = t.get("time")
            if t.get("done") or not tm:
                continue
            at = dt.datetime.combine(today, dt.time(int(tm[:2]), int(tm[3:5])))
            key = f"{core.base_of(t).get('id')}@{tm}"
            due = self._snooze.get(key)
            if due is not None:
                if now < due:
                    continue
                del self._snooze[key]
            elif key in n["keys"] or not (at - dt.timedelta(minutes=lead) <= now < at + dt.timedelta(minutes=30)):
                continue
            n["keys"].append(key)
            when = "지금" if now >= at else f"{int((at - now).total_seconds() // 60) + 1}분 뒤"
            NotifyPopup(self, f"{tm}  ·  {when}", t.get("text", ""),
                        on_snooze=lambda k=key: self._snooze.__setitem__(k, dt.datetime.now() + dt.timedelta(minutes=10)))
            self._save_conf()
        brief = self.conf.get("brief", "09:00")
        if brief and self.conf.get("brief_date") != today.isoformat() and now.strftime("%H:%M") >= brief:
            self.conf["brief_date"] = today.isoformat()
            todo = [t for _, t, _ in rows if not t.get("done")]
            if todo:
                todo.sort(key=sort_key)
                lines = [(f"{t['time']} " if t.get("time") else "") + t.get("text", "") for t in todo[:5]]
                if len(todo) > 5:
                    lines.append(f"…외 {len(todo) - 5}개")
                NotifyPopup(self, f"오늘 할 일 {len(todo)}개", "\n".join(lines))
            self._save_conf()

    def update_conf_rebuild(self, **kw):
        self.conf.update(kw)
        self._save_conf()
        self._relayout_cal()

    def update_conf(self, **kw):
        self.conf.update(kw)
        self._save_conf()

    # ── 백업 / 복원 / 내보내기 ──
    def backup_save(self):
        self._flush_memo()
        self._commit()
        path = filedialog.asksaveasfilename(parent=self.root, defaultextension=".json",
                                            initialfile=f"DeskCalendar-백업-{dt.date.today():%Y%m%d}.json",
                                            filetypes=[("백업 파일", "*.json")])
        if path:
            shutil.copy(DATA_FILE, path)
            messagebox.showinfo("백업 완료", f"저장했어요.\n{path}", parent=self.root)

    def backup_restore(self):
        path = filedialog.askopenfilename(parent=self.root, title="백업 파일 선택",
                                          filetypes=[("백업 파일", "*.json"), ("모든 파일", "*.*")])
        if not path:
            return
        try:
            with open(path, encoding="utf-8") as f:
                loaded = json.load(f)
            assert isinstance(loaded, dict)
        except Exception:
            messagebox.showerror("복원 실패", "올바른 백업 파일이 아니에요.", parent=self.root)
            return
        if not messagebox.askyesno("복원", "현재 일정이 이 백업으로 바뀝니다.\n(지금 데이터는 backups 폴더에 따로 남겨 둬요)\n계속할까요?",
                                   parent=self.root):
            return
        try:
            d = os.path.join(DATA_DIR, "backups")
            os.makedirs(d, exist_ok=True)
            shutil.copy(DATA_FILE, os.path.join(d, f"before-restore-{dt.datetime.now():%Y%m%d%H%M%S}.json"))
        except Exception:
            pass
        self.data = loaded
        core.ensure_ids(self.data)
        self._commit()
        self.rebuild()

    def export_ics(self):
        path = filedialog.asksaveasfilename(parent=self.root, defaultextension=".ics",
                                            initialfile="DeskCalendar.ics", filetypes=[("캘린더 파일", "*.ics")])
        if path:
            with open(path, "w", encoding="utf-8", newline="") as f:
                f.write(core.build_ics(self.data))
            messagebox.showinfo("내보내기 완료", "구글 캘린더 등에서 '가져오기'로 불러올 수 있어요.", parent=self.root)

    def _poll_requests(self):
        """다른 실행(PDF 를 exe 위로 끌어 놓기)이 남긴 요청 확인"""
        try:
            if os.path.exists(REQUEST_FILE):
                with open(REQUEST_FILE, encoding="utf-8") as f:
                    paths = [x.strip() for x in f if x.strip()]
                os.remove(REQUEST_FILE)
                if paths:
                    self.import_path(paths[0])
        except Exception:
            pass
        self.root.after(1000, self._poll_requests)

    def has_similar(self, d, text):
        norm = lambda x: re.sub(r"^\[[^\]]*\]\s*|\s+", "", x)[:30]
        n = norm(text)
        return any(norm(t.get("text", "")) == n for t in self.data.get(d.isoformat(), {}).get("todos", []))

    def open_detail(self, d, end=None):
        self._tip_hide()
        if self.detail is not None:
            try:
                self.detail.top.destroy()
            except Exception:
                pass
        self.detail = DayDetail(self, d, end)

    def _refresh_detail(self):
        dd = self.detail
        if dd is not None:
            try:
                if dd.top.winfo_exists():
                    dd.build()
                    return
            except Exception:
                pass
            self.detail = None

    def fit_screen(self):
        """창을 화면 가득 크게"""
        r = self.root
        sw, sh = r.winfo_screenwidth(), r.winfo_screenheight()
        self.win_w = sw - self.S(40) - (0)
        self.win_h = sh - self.S(80)
        self._geom(self.S(20), self.S(20))
        self.conf.update(w=self.win_w, h=self.win_h, x=self.S(20), y=self.S(20))
        self._save_conf()

    def edit_item(self, t):
        EditDialog(self, t)

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
        for k in ("theme", "accent", "text_color", "pri_colors", "font", "scale", "cal_font", "cal_lines",
                  "glass_tint", "glass_strength", "show_lunar", "alpha"):
            self.conf.pop(k, None)
        self.set_alpha(0.97)
        self._gkey = None
        self.family = DEFAULT_FONT
        self.ui = 0.9
        self.scale = self.dpi_scale * 0.9
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

    def _wrap_lines(self, text, font, maxw, n):
        """글자를 n줄까지 나눠 담고 넘치면 … 로 줄인다 (한글은 글자 단위, 영문은 공백에서 자름)"""
        out, rest = [], text
        for k in range(n):
            if not rest:
                break
            if k == n - 1:
                out.append(self._fit(rest, font, maxw))
                break
            cut = len(rest)
            while cut > 0 and font.measure(rest[:cut]) > maxw:
                cut -= 1
            cut = max(cut, 1)
            if cut < len(rest):
                sp = rest.rfind(" ", 0, cut)
                if sp > cut * 0.5:
                    cut = sp
            out.append(rest[:cut].rstrip())
            rest = rest[cut:].lstrip()
        return out or [""]

    @staticmethod
    def _week_start(d):
        return d - dt.timedelta(days=(d.weekday() + 1) % 7)   # 일요일

    def _default_view_start(self):
        """지난주 · 이번 주 · 앞으로의 주들이 보이도록, 지난주 일요일부터 시작"""
        return self._week_start(dt.date.today()) - dt.timedelta(days=7)

    def shift_weeks(self, n):
        self.view_start += dt.timedelta(weeks=n)
        self.render_month()

    def jump_next_month(self):
        mid = self.view_start + dt.timedelta(days=(7 * self.weeks) // 2)
        nxt = dt.date(mid.year + (mid.month == 12), mid.month % 12 + 1, 1)
        self.view_start = self._week_start(nxt)
        self.render_month()

    def set_style(self, **kw):
        """모양 설정(유리/카드, 주 수, 유리 색·진하기)을 바꾸고 다시 그리기"""
        old = self.conf.get("style", "glass")
        self.conf.update(kw)
        self._save_conf()
        self.glass = self.conf.get("style", "glass") == "glass"
        self.weeks = int(self.conf.get("weeks", 5))
        self._gkey = None
        if self.conf.get("style", "glass") != old:
            self.rebuild()
        else:
            self.render_month()

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
        if self.glass:
            self.dates = [self.view_start + dt.timedelta(days=i) for i in range(7 * self.weeks)]
            self.draw_calendar()
            return
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
        self.month_sub.config(text=f"남은 일정 {remain}" if remain else "")
        self.draw_calendar()

    def _build_layout(self):
        """보이는 6주 안의 일정을 칸(슬롯)에 배치: 기간 일정은 주마다 이어진 막대, 하루 일정은 남는 칸"""
        dates = self.dates
        first, last = dates[0], dates[-1]
        singles, spans = {}, []
        for key, v in self.data.items():
            if key in core.NON_DATE_KEYS:
                continue
            s0 = core.parse_date(key)
            if not s0:
                continue
            for t in v.get("todos", []):
                rep = t.get("repeat")
                if rep:
                    for d in dates:
                        if core.repeat_hit(rep, s0, d):
                            singles.setdefault(d, []).append(core.occurrence(t, d))
                    continue
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
        if self.glass:
            return self.draw_glass()
        self._hits = []
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
        if getattr(self, "_hl", False) and self.mode == "day":
            return self.selected, self.sel_end or self.selected
        return None

    def _draw_chip(self, x1, x2, yy, t, in_month, more=0, units=1):
        """일정 한 줄: 유리형처럼 칠한 상자 없이 왼쪽 색 막대 + 글자 (시간은 흐린 색)"""
        S, c = self.S, self.cal
        h = units * self.unit - S(2)
        done = t.get("done", False)
        col = DONE if done else pri_color(t)
        bgc = CARD if in_month else CARD_DIM
        rrect(c, x1, yy + S(3), x1 + S(3), yy + h - S(3), S(2), fill=col, outline="")
        self._hits.append((x1, yy, x2, yy + h, t))
        fnt = self.f_chip_done if done else self.f_chip
        fg = DIM if done else (TEXT if pri_of(t) >= 2 else blend(col, CHIP_MIX, 0.55 if CHIP_MIX == "#000000" else 0.6))
        tx = x1 + S(9)
        tm = t.get("time")
        label = self._label(t, more)
        if tm and not done and units == 1 and label.startswith(tm):
            c.create_text(tx, yy + h / 2, anchor="w", font=fnt, text=tm, fill=DIM)
            tx += fnt.measure(tm + " ")
            label = label[len(tm):].lstrip()
        c.create_text(tx, yy + h / 2, anchor="w", font=fnt,
                      text="\n".join(self._wrap_lines(label, fnt, x2 - tx - S(4), units)), fill=fg)

    @staticmethod
    def _label(t, more=0):
        return (t["time"] + " " if t.get("time") else "") + t.get("text", "") + (f"   +{more}" if more else "")

    def _draw_bar(self, b):
        """기간 일정: 여러 날짜 칸을 가로로 이어 붙인 막대"""
        S, c, h = self.S, self.cal, self.unit - self.S(2)
        cw, ch, gap = self.cw, self.ch, self.gap
        t = b["t"]
        done = t.get("done", False)
        col = DONE if done else pri_color(t)
        fill = blend(col, CARD, 0.30 if not done else 0.14)
        x1 = b["c1"] * (cw + gap) + S(3)
        x2 = b["c2"] * (cw + gap) + cw - S(3)
        y = self.wd_h + b["row"] * (ch + gap) + self.head_h + b["slot"] * self.unit
        rrect(c, x1, y, x2, y + h, S(4), fill=fill, outline="")
        self._hits.append((x1, y, x2, y + h, t))
        if not b["left"]:   # 앞 주에서 이어지는 막대는 왼쪽을 평평하게
            c.create_rectangle(x1, y, x1 + S(6), y + h, fill=fill, outline="")
        else:
            c.create_rectangle(x1, y + S(3), x1 + S(3), y + h - S(3), fill=col, outline="")
        if not b["right"]:
            c.create_rectangle(x2 - S(6), y, x2, y + h, fill=fill, outline="")
        fnt = self.f_chip_done if done else self.f_chip
        c.create_text(x1 + S(8), y + h / 2, anchor="w", font=fnt,
                      text=self._fit(t.get("text", ""), fnt, x2 - x1 - S(12)),
                      fill=DIM if done else blend(col, CHIP_MIX, 0.6 if CHIP_MIX == "#000000" else 0.7))

    def _draw_cell(self, i, d, x, y):
        S, c = self.S, self.cal
        cw, ch = self.cw, self.ch
        in_month = d.month == self.view_m
        is_today = d == self.today
        rng = self._sel_range()
        in_sel = (bool(rng) and rng[0] <= d <= rng[1]) or (self._move is not None and self._move_to == i)
        hover = i == self.hover_idx
        holiday = holiday_name(d)
        wd = (d.weekday() + 1) % 7  # 0 = 일요일

        fill = CARD_HOVER if hover else CARD if in_month else CARD_DIM
        if in_sel:
            fill = blend(ACCENT, CARD, 0.18)
            rrect(c, x, y, x + cw, y + ch, S(10), fill=fill, outline=ACCENT, width=S(2))
        elif is_today:                                   # 오늘 칸: 강조색이 살짝 비치게
            rrect(c, x, y, x + cw, y + ch, S(10), fill=blend(ACCENT, CARD, 0.13),
                  outline=blend(ACCENT, CARD, 0.45))
        else:                                            # 테두리 없이 칸 바탕만
            rrect(c, x, y, x + cw, y + ch, S(10), fill=fill, outline="")

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
        free = {k for k in range(self.nslot) if k not in occ}
        x1, x2 = x + S(6), x + cw - S(6)
        inner_w = x2 - x1 - S(12)
        items = []   # (종류, 값, 필요한 줄 수)
        if holiday:
            items.append(("h", holiday, 1))
        for t in self.lay["singles"].get(d, []):
            n = len(self._wrap_lines(self._label(t), self.f_chip, inner_w, self.chip_lines))
            items.append(("t", t, n))
        extra = self.lay["hidden"].get(d, 0)

        def find(u):
            for k in sorted(free):
                if all((k + j) in free for j in range(u)):
                    return k
            return None

        placed, i = [], 0
        while i < len(items):
            kind, v, n = items[i]
            k = find(n)
            if k is None and n > 1:
                n, k = 1, find(1)
            if k is None:
                break
            left_after = len(free) - n
            if i < len(items) - 1 and left_after < 1 and (len(items) - i - 1 + extra) > 0:
                break      # 뒤에 남는 게 있으면 "+N" 줄을 위해 한 줄은 남겨 둔다
            for j in range(n):
                free.discard(k + j)
            placed.append((kind, v, k, n))
            i += 1
        left = len(items) - i + extra
        if left > 0:
            k = find(1)
            if k is not None:
                if not placed and items and items[0][0] == "t":
                    placed.append(("tm", (items[0][1], left - 1), k, 1))
                else:
                    placed.append(("m", left, k, 1))
        elif entry.get("memo", "").strip():          # 자리가 남으면 그날 메모
            memo = entry["memo"].strip().splitlines()[0]
            n = len(self._wrap_lines("✎ " + memo, self.f_chip, inner_w, self.chip_lines))
            k = find(n) if free else None
            if k is not None:
                placed.append(("n", memo, k, n))
        for kind, v, k, n in placed:
            yy = y + self.head_h + k * self.unit
            mid = yy + (n * self.unit - S(2)) / 2
            if kind == "h":
                c.create_text(x1 + S(2), mid, anchor="w", font=self.f_chip,
                              text=self._fit(v, self.f_chip, x2 - x1 - S(4)), fill=SUN if in_month else DIM)
            elif kind == "m":
                c.create_text(x1 + S(2), mid, anchor="w", font=self.f_chip,
                              text=f"+{v}개 더 (마우스를 올려 보세요)" if cw > S(170) else f"+{v}개 더",
                              fill=SUB if in_month else DIM)
            elif kind == "n":
                c.create_text(x1 + S(2), mid, anchor="w", font=self.f_chip,
                              text="\n".join(self._wrap_lines("✎ " + v, self.f_chip, inner_w, n)),
                              fill=SUB if in_month else DIM)
            elif kind == "tm":
                self._draw_chip(x1, x2, yy, v[0], in_month, more=v[1], units=1)
            else:
                self._draw_chip(x1, x2, yy, v, in_month, units=n)

    # 유리(데스크탑) 모양: 아크릴 판 하나 + 얇은 선의 둥근 칸 ─────────────
    def _glass_layout(self, W, H):
        S = self.S
        m, pad, gap = S(4), S(10), S(4)
        hdr_h, wd_h = S(44), S(22)
        gx, gw = m + pad, W - 2 * (m + pad)
        rows = self.weeks
        hdr = (gx, m + S(6), gx + gw, m + S(6) + hdr_h)
        y_wd = hdr[3]
        y0 = y_wd + wd_h + S(2)
        panel = (m, m, W - m, y0 - gap)                       # 위쪽 머리 띠만 판이 있다
        cw = (gw - 6 * gap) / 7
        ch = (H - m - y0 - (rows - 1) * gap) / rows
        cells = []
        for i in range(7 * rows):
            c, r = i % 7, i // 7
            x1 = gx + c * (cw + gap)
            y1 = y0 + r * (ch + gap)
            cells.append((int(x1), int(y1), int(x1 + cw), int(y1 + ch)))
        return {"m": m, "gap": gap, "gx": gx, "y0": y0, "cw": cw, "ch": ch, "cells": cells, "panel": panel,
                "hdr": hdr, "wd": [(cells[c][0], y_wd, cells[c][2], y_wd + wd_h) for c in range(7)]}

    def _summary_counts(self):
        """(오늘 남은 일정, 지난 미완료, 앞으로 3일 안 미완료)"""
        today = dt.date.today()
        late = 0
        for key, v in self.data.items():
            if key in core.NON_DATE_KEYS or not isinstance(v, dict):
                continue
            s0 = core.parse_date(key)
            if not s0 or s0 >= today:
                continue
            for t in v.get("todos", []):
                if t.get("done") or t.get("repeat"):
                    continue
                if (core.item_end(t, s0) or s0) < today:
                    late += 1
        undone = lambda d: sum(1 for _, t, _ in core.day_items(self.data, d) if not t.get("done"))
        soon = sum(undone(today + dt.timedelta(days=k)) for k in (1, 2, 3))
        return undone(today), late, soon

    def _glass_colors(self):
        if self.conf.get("glass_tint", "그래파이트") == "밝게":
            return {"txt": "#1c2230", "dim": "#6b7385", "sun": "#c8374a", "sat": "#2f6bd1",
                    "pri": ["#c8374a", "#b7791f", "#2b3345", "#1f8a5b"], "done": "#9aa3b2",
                    "holiday": "#c8374a", "anniv": "#b7791f", "term": "#1f8a5b", "line": "#7d8699", "light": True}
        return {"txt": "#eceff5", "dim": "#8e98ab", "sun": "#f08a94", "sat": "#8fb4f2",
                "pri": ["#ff8f9b", "#f2c374", "#dfe4ee", "#82d9a8"], "done": "#757f92",
                "holiday": "#f08a94", "anniv": "#e5c07b", "term": "#86c9a0", "line": "#8e98ab", "light": False}

    def _gtext(self, x, y, text, font, fill, anchor="nw", shadow=None):
        return self.cal.create_text(x, y, text=text, font=font, fill=fill, anchor=anchor)

    def _rr(self, x1, y1, x2, y2, r, **kw):
        """둥근 사각형 (캔버스에는 없어서 부드러운 다각형으로)"""
        r = max(1, min(r, (x2 - x1) / 2, (y2 - y1) / 2))
        pts = [x1 + r, y1, x2 - r, y1, x2, y1, x2, y1 + r, x2, y2 - r, x2, y2, x2 - r, y2, x1 + r, y2,
               x1, y2, x1, y2 - r, x1, y1 + r, x1, y1]
        return self.cal.create_polygon(pts, smooth=True, **kw)

    def _glass_bg(self, W, H, L):
        """바탕화면을 흐리게 깔고 얇은 선의 칸을 얹은 배경 그림 (창 위치·크기가 같으면 재사용)"""
        try:
            from PIL import ImageTk
        except Exception:
            return None
        sw, sh = self.root.winfo_screenwidth(), self.root.winfo_screenheight()
        ox, oy = self.cal.winfo_rootx(), self.cal.winfo_rooty()
        tint_name = self.conf.get("glass_tint", "그래파이트")
        strength = float(self.conf.get("glass_strength", 0.6))
        today = dt.date.today()
        mid = self.dates[len(self.dates) // 2].month
        key = (W, H, ox, oy, self.weeks, tint_name, strength, self.scale, self.dates[0], today, ACCENT)
        if key == self._gkey and self._gimg is not None:
            return self._gimg
        cx, cy = ox + W // 2, oy + H // 2
        wp = glass.wallpaper(sw, sh) if (0 <= cx < sw and 0 <= cy < sh) else None   # 보조 모니터는 기본 그라데이션
        wp = wp or glass.fallback_wallpaper(sw, sh)
        kinds = ["today" if d == today else "out" if d.month != mid else None for d in self.dates]
        cells = [(*r, (i % 7) in (0, 6), kinds[i] if i < len(kinds) else None) for i, r in enumerate(L["cells"])]
        im = glass.render_bg(wp, ox, oy, W, H, L["panel"], cells,
                             tint=glass.TINTS.get(tint_name, glass.TINTS["그래파이트"]), strength=strength,
                             blur=max(8, self.S(18)), radius=self.S(14), cell_radius=self.S(10),
                             light=tint_name == "밝게", accent=tuple(int(ACCENT[i:i + 2], 16) for i in (1, 3, 5)))
        self._gimg = ImageTk.PhotoImage(im)
        self._gkey = key
        return self._gimg

    def draw_glass(self):
        c, S = self.cal, self.S
        W, H = c.winfo_width(), c.winfo_height()
        if W < 50 or H < 50:
            return
        self._hits = []
        c.delete("all")
        L = self._glass_layout(W, H)
        self.gm = L
        self._gcells = L["cells"]
        col = self._glass_colors()
        img = self._glass_bg(W, H, L)
        if img is not None:
            c.create_image(0, 0, anchor="nw", image=img)
        else:   # PIL 이 없을 때: 단색 판
            self._rr(*L["panel"], S(14), fill="#1b1e29", outline="")
            for r in L["cells"]:
                self._rr(*r, S(8), fill="#232736", outline="")
        today = dt.date.today()
        # 머리줄: 큰 '2026년 10월' + 오른쪽 작은 버튼들
        hx1, hy1, hx2, hy2 = L["hdr"]
        mid_y = (hy1 + hy2) / 2
        mid = self.dates[len(self.dates) // 2]
        self._gtext(hx1 + S(4), mid_y, f"{mid.year}년 {mid.month}월", self.f_h2, col["txt"], "w")
        title_w = self.f_h2.measure(f"{mid.year}년 {mid.month}월")
        self._gbtns = []
        # 요약 줄: 오늘 N · 지연 N · 3일 내 N (눌러서 '예정' 보기)
        n_today, n_late, n_soon = self._summary_counts()
        sx = hx1 + title_w + S(22)
        chips = [(f"오늘 {n_today}", col["txt"])]
        if n_late:
            chips.append((f"지연 {n_late}", col["sun"]))
        chips.append((f"3일 내 {n_soon}", col["dim"] if not n_soon else ACCENT))
        for text, fg in (chips if hx2 - hx1 > S(560) else []):      # 좁으면 요약 칩은 숨긴다
            wch = self.f_small.measure(text) + S(18)
            self._rr(sx, mid_y - S(11), sx + wch, mid_y + S(11), S(11), fill="#e2e6ef" if col.get("light") else "#2c303b", outline="")
            self._gtext(sx + wch / 2, mid_y, text, self.f_small, fg, "center")
            self._gbtns.append((sx, mid_y - S(11), sx + wch, mid_y + S(11),
                                lambda e: (self.set_mode("agenda"), self.set_panel(True)), "sum:" + text))
            sx += wch + S(6)
        sum_end = sx
        n_case = sum(1 for c in (self.data.get("_work") or {}).values() if not c.get("done"))
        labels = [(f"업무 {n_case}" if n_case else "업무", lambda e: self.open_work()),
                  ("스티커", lambda e: self.stickers.add(self.selected if self.selected != self.today else None)),
                  ("보관함", lambda e: self.open_inbox()), ("공문", lambda e: self.import_pdf()),
                  ("검색", lambda e: self.open_search()), ("폰 연결", lambda e: PhoneDialog(self)),
                  ("설정", lambda e: self.open_settings()),
                  ("☰", lambda e: self.menu.tk_popup(e.x_root, e.y_root)),
                  ("‹", lambda e: self.shift_weeks(-1)), ("오늘", lambda e: self.go_today()),
                  ("›", lambda e: self.shift_weeks(1))]
        narrow = hx2 - hx1
        if narrow < S(330):
            labels = [l for l in labels if l[0] in ("☰", "‹", "오늘", "›")]
        elif narrow < S(560):
            labels = [l for l in labels if l[0] in ("☰", "‹", "오늘", "›", "설정") or l[0].startswith("업무")]
        x = hx2
        fb = self.f_small
        for text, fn in reversed(labels):
            wd = fb.measure(text)
            x1 = x - wd - S(14)
            btn = (x1, mid_y - S(13), x, mid_y + S(13), fn, text)
            self._gbtns.append(btn)
            hov = self._ghover == text
            down = bool(self._ghdr) and self._ghdr.get("btn") is not None and self._ghdr["btn"][5] == text
            if hov or down:                                  # 둥근 알약 모양 + 누르면 조금 어둡게
                base = "#dde2ec" if col.get("light") else "#383d4a"
                self._rr(x1, mid_y - S(13), x, mid_y + S(13), S(13),
                         fill=blend("#000000", base, 0.14) if down else base, outline="")
            strong = text in ("오늘", "‹", "›")
            fg = col["txt"] if strong or hov else col["dim"]
            if text.startswith("업무 ") and not hov:          # 진행 중인 업무가 있으면 강조색
                fg = ACCENT if not col.get("light") else blend(ACCENT, "#000000", 0.85)
            self._gtext((x1 + x) / 2, mid_y, text, fb, fg, "center")
            x = x1 - S(2)
        if self.sync_cfg() and self.sync_status and x - sum_end > S(150):
            self._gtext(sum_end + S(8), mid_y + S(2), "☁ " + self.sync_status.split(" · ")[0],
                        self.f_tiny, col["dim"], "w")
        for i, r in enumerate(L["wd"]):
            fg = col["sun"] if i == 0 else col["sat"] if i == 6 else col["dim"]
            self._gtext((r[0] + r[2]) / 2, (r[1] + r[3]) / 2, "일월화수목금토"[i], self.f_small, fg, "center")
        for i, d in enumerate(self.dates):
            self._glass_cell(i, d, L["cells"][i], today, col)

    def _glow_img(self, w, h, level):
        """칸 위에 얹는 반투명 하이라이트(마우스를 올렸을 때). 크기·세기별로 한 번만 만든다"""
        key = (w, h, level, self.conf.get("glass_tint"))
        cache = self.__dict__.setdefault("_glow_cache", {})
        if key not in cache:
            try:
                from PIL import Image, ImageTk
                dark_ink = self.conf.get("glass_tint") == "밝게"
                im = Image.new("RGBA", (w, h), (0, 0, 0, 0) if dark_ink else (255, 255, 255, 0))
                im.putalpha(glass._rmask(w, h, self.S(10)).point(lambda v, a=level * 7: v * a // 255))
                if dark_ink:
                    im = Image.merge("RGBA", (Image.new("L", (w, h), 0),) * 3 + (im.getchannel("A"),))
                cache[key] = ImageTk.PhotoImage(im)
            except Exception:
                cache[key] = None
        return cache[key]

    def _glow_to(self, idx):
        """마우스가 올라간 칸은 서서히 밝아지고, 벗어난 칸은 서서히 원래대로"""
        gl = self.__dict__.setdefault("_glow", {})
        self._glow_goal = idx
        if getattr(self, "_glow_job", None) is None:
            self._glow_tick()

    def _glow_tick(self):
        self._glow_job = None
        gl = self._glow
        changed = False
        for k in list(gl):
            if k != self._glow_goal:
                gl[k] -= 1
                changed = True
                if gl[k] <= 0:
                    del gl[k]
        g = self._glow_goal
        if g is not None and gl.get(g, 0) < 4:
            gl[g] = gl.get(g, 0) + 1
            changed = True
        if changed:
            self.draw_calendar()
            try:
                self._glow_job = self.root.after(18, self._glow_tick)
            except tk.TclError:
                self._glow_job = None

    def _glass_cell(self, i, d, rect, today, col):
        c, S = self.cal, self.S
        x1, y1, x2, y2 = rect
        lvl = getattr(self, "_glow", {}).get(i, 0)
        if lvl:
            im = self._glow_img(x2 - x1, y2 - y1, lvl)
            if im is not None:
                c.create_image(x1, y1, anchor="nw", image=im)
        pad = S(7)
        wd = (d.weekday() + 1) % 7
        label, kind = glass.special_day(d)
        in_cur = d.month == self.dates[len(self.dates) // 2].month
        num_fg = col["holiday"] if kind == "holiday" or wd == 0 else col["sat"] if wd == 6 else col["txt"]
        if not in_cur:
            num_fg = col["dim"]
        ny = y1 + S(5)
        dnum = str(d.day)
        nw = self.f_day.measure(dnum)
        nh = self.f_day.metrics("linespace")
        if d == today:                                   # 오늘: 강조색 알약 + 흰 글자
            self._rr(x1 + pad - S(5), ny - S(1), x1 + pad + nw + S(5), ny + nh + S(1), S(9), fill=ACCENT, outline="")
            num_fg = "#ffffff"
        self._gtext(x1 + pad, ny, dnum, self.f_day, num_fg, "nw")
        off = x1 + pad + nw + S(10)
        if label:
            if off + self.f_tiny.measure(label) <= x2 - S(4):          # 칸이 좁으면 휴일 이름은 생략
                self._gtext(off, ny + S(2), label, self.f_tiny, col[kind], "nw")
        elif self.conf.get("show_lunar", False):
            self._gtext(off, ny + S(2), glass.lunar_text(d), self.f_tiny, col["dim"], "nw")
        font, fdone = self.f_chip, self.f_chip_done
        lh = font.metrics("linespace") + S(2)
        y = ny + nh + S(6)
        bottom = y2 - S(4)
        dot = S(5)
        maxw = x2 - x1 - 2 * pad - dot - S(6)
        rows = [r for r in core.day_items(self.data, d)
                if not (item_end(r[1], r[2]) and r[2] != d and d != self.dates[0])]   # 기간 일정은 시작일에만 (보이는 첫 칸 포함)
        rows.sort(key=lambda r: sort_key(r[1]))
        clipped = False
        for lst, t, s0 in rows:
            done = t.get("done", False)
            e0 = item_end(t, s0)
            text = t.get("text", "")
            if t.get("time"):
                text = t["time"] + " " + text
            if e0:
                text += f" ~{e0.month}.{e0.day}"
            lines = self._wrap_lines(text, fdone if done else font, maxw, 99)
            p = pri_of(t)
            fg = col["done"] if done else (col["pri"][p] if p < 2 else col["txt"])
            dot_c = col["done"] if done else col["pri"][p] if p != 2 else ACCENT
            if not done and d < today and not t.get("repeat") and "_src" not in t and (e0 is None or e0 < today):
                fg = dot_c = col["sun"]                      # 지난 미완료는 붉게
            top = y
            for li, line in enumerate(lines):
                if y + lh > bottom:
                    clipped = True
                    break
                if li == 0:                              # 왼쪽 색 막대 (중요도) — 점보다 차분하고 잘 보인다
                    self._rr(x1 + pad, y + S(3), x1 + pad + S(3), y + lh - S(3), S(2), fill=dot_c, outline="")
                f_ = fdone if done else font
                tx = x1 + pad + dot + S(6)
                tm = t.get("time")
                if li == 0 and tm and line.startswith(tm) and not done:   # 시간은 흐린 색으로 앞에
                    self._gtext(tx, y, tm, f_, col["dim"], "nw")
                    self._gtext(tx + f_.measure(tm + " "), y, line[len(tm):].lstrip(), f_, fg, "nw")
                else:
                    self._gtext(tx, y, line, f_, fg, "nw")
                y += lh
            if y > top:
                self._hits.append((x1 + pad, top, x2 - pad, y, t))
                y += S(2)
            if clipped:
                break
        if clipped:
            self._gtext(x2 - pad, y2 - S(4), "더 보기" if x2 - x1 > S(90) else "…", self.f_tiny, col["dim"], "se")
        elif not rows:
            memo = (self.data.get(d.isoformat(), {}).get("memo", "").strip().splitlines() or [""])[0]
            if memo:
                self._gtext(x1 + pad, y, self._fit("✎ " + memo, font, x2 - x1 - 2 * pad), font, col["dim"], "nw")
        # 선택 / 마우스 올림
        rng = self._sel_range()
        if (rng and rng[0] <= d <= rng[1]) or (self._move is not None and self._move_to == i):
            self._rr(x1 + 1, y1 + 1, x2 - 2, y2 - 2, S(8), fill="", outline=ACCENT, width=2)


    def _cell_at(self, x, y):
        if self.glass:
            for i, (x1, y1, x2, y2) in enumerate(self._gcells):
                if x1 <= x < x2 and y1 <= y < y2:
                    return i
            return None
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
        if self.glass:
            z = self._zone(e)
            cur = (self._CURSORS_WIN if IS_WIN else self._CURSORS_X11).get(z, "")
            btn = self._gbtn_at(e.x, e.y)
            if not cur:
                cur = "hand2" if (btn is not None or self._cell_at(e.x, e.y) is not None) else ""
            if self.cal.cget("cursor") != cur:
                self.cal.config(cursor=cur)
            hov = btn[5] if btn else None
            if hov != self._ghover:
                self._ghover = hov
                self.draw_calendar()
            if z:
                self._tip_hide()
                if self.hover_idx is not None:
                    self.hover_idx = None
                    self._glow_to(None)
                return
        idx = self._cell_at(e.x, e.y)
        if idx != self.hover_idx:
            self.hover_idx = idx
            if not self.glass:
                self.cal.config(cursor="hand2" if idx is not None else "")
                self.draw_calendar()
            else:
                self._glow_to(idx)
            self._tip_hide()
            if idx is not None:
                self._tip_job = self.root.after(450, lambda i=idx: self._tip_show(i))

    def _tip_hide(self):
        if self._tip_job:
            self.root.after_cancel(self._tip_job)
            self._tip_job = None
        if self._tip:
            try:
                self._tip.destroy()
            except Exception:
                pass
            self._tip = None

    def _tip_show(self, idx):
        """칸에 다 안 들어가는 내용까지 보여 주는 말풍선"""
        self._tip_job = None
        if idx != self.hover_idx or self._press_idx is not None or not (0 <= idx < len(self.dates)):
            return
        d = self.dates[idx]
        rows = core.day_items(self.data, d)
        rows.sort(key=lambda r: sort_key(r[1]))
        memo = self.data.get(d.isoformat(), {}).get("memo", "").strip()
        h = holiday_name(d)
        if not rows and not memo and not h:
            return
        S = self.S
        top = self._tip = tk.Toplevel(self.root)
        top.overrideredirect(True)
        top.attributes("-topmost", True)
        top.configure(bg=BORDER)
        card = tk.Frame(top, bg=SURFACE, padx=S(12), pady=S(10))
        card.pack(padx=1, pady=1)
        tk.Label(card, text=f"{d.month}월 {d.day}일 ({WEEKDAY_KO[d.weekday()]})" + (f" · {h}" if h else ""),
                 font=self.f_head, bg=SURFACE, fg=ACCENT if d == self.today else TEXT, anchor="w").pack(fill="x")
        for lst, t, s0 in rows[:14]:
            done = t.get("done", False)
            e0 = item_end(t, s0)
            tag = (f"{t['time']} " if t.get("time") else "") + (f"[{s0.month}/{s0.day}~{e0.month}/{e0.day}] " if e0 else "")
            tk.Label(card, text=("✔ " if done else "● ") + tag + t.get("text", ""),
                     font=self.f_done if done else self.f_body, bg=SURFACE, fg=DONE if done else pri_color(t),
                     anchor="w", justify="left", wraplength=S(320)).pack(fill="x", pady=(S(3), 0))
        if len(rows) > 14:
            tk.Label(card, text=f"…외 {len(rows) - 14}개", font=self.f_small, bg=SURFACE, fg=SUB, anchor="w").pack(fill="x")
        if memo:
            tk.Label(card, text="✎ " + memo[:200], font=self.f_small, bg=SURFACE, fg=SUB, anchor="w",
                     justify="left", wraplength=S(320)).pack(fill="x", pady=(S(6), 0))
        top.update_idletasks()
        px, py = self.root.winfo_pointerxy()
        vx, vy, vw, vh = virtual_screen(self.root)
        w, hh = top.winfo_reqwidth(), top.winfo_reqheight()
        x = min(px + S(16), vx + vw - w - S(8))
        y = min(py + S(16), vy + vh - hh - S(8))
        top.geometry(f"+{x}+{y}")

    def _gbtn_at(self, x, y):
        for b in self._gbtns:
            if b[0] <= x <= b[2] and b[1] <= y <= b[3]:
                return b
        return None

    def _glass_press(self, e):
        z = self._zone(e)
        if z and not self.locked.get():
            self._resize_start(e)
            return True
        btn = self._gbtn_at(e.x, e.y)
        if btn is not None or e.y < self.gm.get("y0", 0):
            self._ghdr = {"btn": btn}
            if btn is not None:
                self.draw_calendar()                          # 누른 모양을 바로 보여 준다
            if btn is None:
                self._drag_start(e)           # 머리줄을 끌면 창 이동
            return True
        return False

    def _cal_press(self, e):
        self._tip_hide()
        if self.glass and self._glass_press(e):
            self._press_idx = None
            return
        self._press_idx = self._cell_at(e.x, e.y)
        self._drag_range = None
        self._move = self._move_to = None
        if self._press_idx is not None:       # 일정 칩 위에서 누르면 '날짜 이동', 빈 곳이면 '기간 선택'
            for x1, y1, x2, y2, t in reversed(self._hits):
                if x1 <= e.x <= x2 and y1 <= e.y <= y2:
                    if not t.get("repeat") and "_src" not in t:
                        self._move = (t, self.dates[self._press_idx])
                    break

    def _cal_drag(self, e):
        if self.glass:
            if self._rs:
                self._resize_move(e)
                return
            if self._ghdr is not None:
                if self._ghdr["btn"] is None:
                    self._drag_move(e)
                return
        if self._press_idx is None:
            return
        idx = self._cell_at(e.x, e.y)
        if self._move:
            to = idx if idx is not None else self._move_to
            if to != self._move_to and (to != self._press_idx or self._move_to is not None):
                self._move_to = to
                self.draw_calendar()
            return
        if idx is None or idx == self._press_idx and not self._drag_range:
            return
        a, b = sorted((self.dates[self._press_idx], self.dates[idx]))
        if self._drag_range != (a, b):
            self._drag_range = (a, b)
            self.draw_calendar()

    def _cal_leave(self, e):
        self._tip_hide()
        if self.hover_idx is not None:
            self.hover_idx = None
            if self.glass:
                self._glow_to(None)
            else:
                self.draw_calendar()

    def _cal_release(self, e):
        if self.glass:
            if self._rs:
                self._resize_end(e)
                self._gkey = None
                self.render_month()
                return
            if self._ghdr is not None:
                h, self._ghdr = self._ghdr, None
                if h["btn"] is None:
                    self._drag_end(e)
                    self._gkey = None
                    self.draw_calendar()
                else:
                    self.draw_calendar()
                    if (self._gbtn_at(e.x, e.y) or (0,) * 6)[5:] == h["btn"][5:]:
                        h["btn"][4](e)
                return
        idx = self._cell_at(e.x, e.y)
        press, rng, mv, to = self._press_idx, self._drag_range, self._move, self._move_to
        self._press_idx, self._drag_range, self._move, self._move_to = None, None, None, None
        if press is None:
            return
        if mv and to is not None and to != press:
            self.move_item(mv[0], mv[1], self.dates[to])
        elif rng and rng[0] != rng[1]:
            self.select_date(rng[0], rng[1])
        elif idx == press:
            now = time.time()
            last = self._last_click
            if last and last[0] == idx and now - last[1] < 0.45:   # 더블클릭: 그날의 상세 보기
                self._last_click = None
                self.open_detail(self.dates[idx])
                return
            self._last_click = (idx, now)
            self.select_date(self.dates[idx], open_=False)
        else:
            self.draw_calendar()

    def shift_month(self, delta):
        if self.glass:
            self.shift_weeks(4 * delta)
            return
        m = self.view_m - 1 + delta
        self.view_y += m // 12
        self.view_m = m % 12 + 1
        self.render_month()

    def go_today(self):
        self.today = dt.date.today()
        self.view_y, self.view_m = self.today.year, self.today.month
        self.view_start = self._default_view_start()
        self.mode = "day"
        self.sel_end = None
        self._apply_mode_layout()
        self.show_day(self.today)
        self.render_month()

    def _on_wheel_month(self, e):
        if e.state & 0x4:   # Ctrl+휠은 크기 조절
            return
        if self.glass:
            self.shift_weeks(-1 if e.delta > 0 else 1)
            return
        self.shift_month(-1 if e.delta > 0 else 1)

    # ── 입력 패널 열기 / 닫기 ──
    def _apply_panel_visibility(self):
        """보관함·일정목록·검색 패널: 달력 위 오른쪽에 겹쳐 띄운다 (창이 커지거나 움직이지 않음)"""
        if self.panel_open:
            pw = self.panel_w
            self.right.place(in_=self.left.master, relx=1.0, x=-self.S(6), y=self.S(8), anchor="ne", width=pw,
                             relheight=1.0, height=-self.S(16))
            self.right.lift()
        else:
            self.right.place_forget()

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
        self.render_month()

    def open_inbox(self):
        if self.panel_open and self.mode == "inbox":
            self.set_panel(False)
            return
        self.set_mode("inbox")
        self.set_panel(True)
        self.todo_entry.focus_force()

    def select_date(self, start, end=None, open_=True):
        """날짜를 한 번 누르면 선택(강조)만, 두 번 누르거나 끌어서 기간을 고르면 메모 앱 같은 입력 창이 뜬다"""
        end = end if end and end > start else None
        if self.mode != "day" and self.panel_open:
            self.set_panel(False)
        self.sel_end = end
        if self.glass and self.dates and not (self.dates[0] <= start <= self.dates[-1]):
            self.view_start = self._week_start(start) - dt.timedelta(days=7)
        self.selected = start
        self._hl = True
        self._update_sub()
        if open_:
            self.open_detail(start, end)
        self.render_month()

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
        if not getattr(self.todo_entry, "ph", False):
            self.todo_entry.delete(0, "end")
        self._apply_mode_layout()
        self.show_day(self.selected)
        self.render_month()
        if mode == "search":
            self.todo_entry.focus_force()

    def open_search(self):
        self.set_panel(True)
        self.set_mode("search")
        self.todo_entry.focus_force()

    def jump_to(self, d):
        """예정·검색 목록에서 날짜를 눌렀을 때: 그 날짜의 일정 탭으로"""
        self._flush_memo()
        self.mode, self.sel_end = "day", None
        self._apply_mode_layout()
        if (d.year, d.month) != (self.view_y, self.view_m):
            self.view_y, self.view_m = d.year, d.month
        self.show_day(d)
        self.render_month()

    def _apply_mode_layout(self):
        m, S = self.mode, self.S
        for lb, key in ((self.tab_day, "day"), (self.tab_agenda, "agenda"),
                        (self.tab_inbox, "inbox"), (self.tab_search, "search")):
            on = m == key
            lb.config(bg=ACCENT_SOFT if on else SURFACE, fg=ACCENT if on else SUB)
        for w in (self.pr, self.box, self.prow, self.memo_box):
            w.pack_forget()
        if m == "day":
            self.pr.pack(before=self.list_wrap, fill="x", pady=(0, S(8)))
        if m in ("day", "inbox", "search"):
            self.box.pack(before=self.list_wrap, fill="x")
        if m in ("day", "inbox"):
            self.prow.pack(before=self.list_wrap, fill="x", pady=(S(8), S(6)))
        if m == "day":
            self.memo_box.pack(side="bottom", fill="x", before=self.list_wrap)
        self._set_placeholder({"day": "할 일 추가   예) 14:30 팀 회의", "inbox": "나중에 할 일을 적어 두세요",
                               "search": "일정·메모 검색", "agenda": ""}[m])

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
        """(소유 리스트, 항목, 시작일) 목록. 일정 탭: 그날 항목 + 그날을 지나는 기간·반복 일정 / 보관함 탭: 보관함"""
        if self.mode == "inbox":
            lst = self._inbox().get("todos", [])
            rows = [(lst, t, None) for t in lst]
        elif self.mode == "day":
            rows = core.day_items(self.data, self.selected)
        else:
            return []
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
        if self.mode in ("agenda", "search"):
            self.d_title.config(text="예정" if self.mode == "agenda" else "검색")
            self.d_sub.config(text="오늘부터 2주 · 지난 미완료 일정 포함" if self.mode == "agenda"
                              else "일정과 메모에서 찾아요 (Ctrl+F)")
            self.range_lbl.pack_forget()
            return
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
            e = self._entry(self._memo_date, create=True)
            e["memo"], e["memo_ts"] = text, core.now_ms()
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

    def _build_row(self, lst, t, start, inbox=False, jump=None, date_text=None):
        S = self.S
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
        if date_text:
            tk.Label(body, text=date_text, font=self.f_tiny, bg=SURFACE, fg=SUB, anchor="w").pack(fill="x")
        e0 = item_end(t, start) if start else None
        if t.get("repeat"):
            lab = core.repeat_label(t["repeat"], start)
            tk.Label(body, text=f"반복  {lab}" + (f"  {t['time']}" if t.get("time") else ""),
                     font=self.f_tiny, bg=SURFACE, fg=DIM if done else col, anchor="w").pack(fill="x")
        elif e0:
            tk.Label(body, text=f"기간  {start.month}/{start.day} ~ {e0.month}/{e0.day}",
                     font=self.f_tiny, bg=SURFACE, fg=DIM if done else col, anchor="w").pack(fill="x")
        elif t.get("time") and not inbox:
            tk.Label(body, text=t["time"], font=self.f_tiny, bg=SURFACE,
                     fg=DIM if done else PRIORITIES[1][1], anchor="w").pack(fill="x")
        txt = tk.Label(body, text=t.get("text", ""), font=self.f_done if done else self.f_body,
                       bg=SURFACE, fg=DONE if done else TEXT, anchor="w", justify="left",
                       wraplength=S(150))
        txt.pack(fill="x")
        if t.get("note") and not date_text:
            tk.Label(body, text=t["note"].splitlines()[0], font=self.f_tiny, bg=SURFACE, fg=DIM,
                     anchor="w", justify="left", wraplength=S(150)).pack(fill="x")
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
        txt.bind("<Double-Button-1>", lambda e, t=t: self.edit_item(t))
        if jump:
            txt.config(cursor="hand2")
            txt.bind("<Button-1>", lambda e, d=jump: self.jump_to(d))
        for w in (row, body, txt, dele, dot, chk):
            w.bind("<MouseWheel>", self._on_wheel_todo)
            w.bind("<Button-3>", lambda e, l=lst, t=t, s0=start: self._row_menu(e, l, t, s0))

    def _note(self, text, fg=None):
        lbl = tk.Label(self.todo_inner, text=text, font=self.f_small, bg=SURFACE, fg=fg or DIM,
                       anchor="w", justify="left")
        lbl.pack(fill="x", pady=self.S(6))
        lbl.bind("<MouseWheel>", self._on_wheel_todo)
        return lbl

    def render_todos(self):
        S = self.S
        for w in self.todo_inner.winfo_children():
            w.destroy()
        left_n = sum(1 for t in self._inbox().get("todos", []) if not t.get("done"))
        self.tab_inbox.config(text=f"보관함 {left_n}" if left_n else "보관함")
        if self.mode == "agenda":
            return self.render_agenda()
        if self.mode == "search":
            return self.render_search()
        rows = self._rows()
        inbox = self.mode == "inbox"
        done_n = sum(1 for _, t, _ in rows if t.get("done"))
        self.todo_count.config(text=f"{done_n}/{len(rows)}" if rows else "")
        self._draw_progress()
        if not rows:
            self._note("보관함이 비어 있어요\n나중에 할 일을 적어 두면 여기 쌓여요" if inbox
                       else "등록된 할 일이 없어요\n위 입력창에 적고 Enter")
            return
        for lst, t, start in rows:
            self._build_row(lst, t, start, inbox=inbox)

    def render_agenda(self):
        """오늘부터 2주 + 지난 미완료 일정"""
        S, today = self.S, self.today
        over = []
        for key, v in self.data.items():
            s0 = core.parse_date(key) if key not in core.NON_DATE_KEYS else None
            if not s0 or s0 >= today:
                continue
            for t in v.get("todos", []):
                if not t.get("done") and not t.get("repeat") and (item_end(t, s0) or s0) < today:
                    over.append((v["todos"], t, s0))
        over.sort(key=lambda r: r[2])
        blocks = [("지난 일정 · 아직 안 끝난 것", over, None, SUN)] if over else []
        for k in range(14):
            d = today + dt.timedelta(days=k)
            rows = core.day_items(self.data, d)
            rows.sort(key=lambda r: sort_key(r[1]))
            name = "오늘" if k == 0 else "내일" if k == 1 else f"{d.month}/{d.day} ({WEEKDAY_KO[d.weekday()]})"
            h = holiday_name(d)
            if rows or k == 0 or h:
                blocks.append((name + (f" · {h}" if h else ""), rows, d, ACCENT if k == 0 else SUB))
        for title, rows, d, col in blocks:
            hd = tk.Label(self.todo_inner, text=title, font=self.f_head, bg=SURFACE, fg=col, anchor="w",
                          cursor="hand2" if d else "")
            hd.pack(fill="x", pady=(S(10), S(2)))
            hd.bind("<MouseWheel>", self._on_wheel_todo)
            if d:
                hd.bind("<Button-1>", lambda e, d=d: self.jump_to(d))
            if not rows:
                self._note("일정 없음")
            for lst, t, start in rows:
                self._build_row(lst, t, start, jump=d or start)
        self.todo_count.config(text="")

    def render_search(self):
        q = "" if getattr(self.todo_entry, "ph", False) else self.todo_entry.get().strip().lower()
        self.todo_count.config(text="")
        if not q:
            return self._note("찾을 말을 입력하세요\n일정 내용·메모·보관함을 모두 찾아요")
        hits = []
        for key, v in self.data.items():
            if key == "_deleted":
                continue
            d = core.parse_date(key)
            for t in v.get("todos", []):
                if q in t.get("text", "").lower() or q in t.get("note", "").lower():
                    hits.append((d, v["todos"], t))
            if d and q in v.get("memo", "").lower():
                hits.append((d, None, v["memo"]))
        hits.sort(key=lambda h: (h[0] is None, h[0] or dt.date.max))
        if not hits:
            return self._note("결과가 없어요")
        S = self.S
        for d, lst, t in hits[:80]:
            label = f"{d.year}.{d.month}.{d.day}" if d else "보관함"
            if lst is None:     # 메모
                f = tk.Label(self.todo_inner, text=f"{label} · 메모\n{t.strip()[:60]}", font=self.f_small,
                             bg=SURFACE, fg=TEXT, anchor="w", justify="left", wraplength=S(220), cursor="hand2")
                f.pack(fill="x", pady=S(4))
                f.bind("<Button-1>", lambda e, d=d: self.jump_to(d))
                f.bind("<MouseWheel>", self._on_wheel_todo)
            else:
                self._build_row(lst, t, d, jump=d, date_text=label)
        if len(hits) > 80:
            self._note(f"…외 {len(hits) - 80}개 (검색어를 더 구체적으로)")

    # ── 할 일 조작 ──
    def add_todo(self):
        if getattr(self.todo_entry, "ph", False) or self.mode in ("agenda", "search"):
            return
        text = self.todo_entry.get().strip()
        if not text:
            return
        item = core.new_item(text, pri=self.new_pri)
        m = TIME_RE.match(text)
        if self.mode == "day" and m and int(m.group(1)) < 24 and int(m.group(2)) < 60:
            item.update(text=m.group(3).strip(), time=f"{int(m.group(1)):02d}:{m.group(2)}")
        if self.mode == "inbox":
            core.add_item(self.data, "_inbox", item)
        else:
            if self.sel_end:
                item["end"] = self.sel_end.isoformat()
            core.add_item(self.data, self.selected.isoformat(), item)
        self.todo_entry.delete(0, "end")
        self._pick_pri(DEFAULT_PRI)
        if self.sel_end and self.mode == "day":   # 기간은 한 번 쓰고 풀어서 다음 입력은 하루 일정
            self.sel_end = None
            self._update_sub()
        self._after_todo_change()

    def _remove(lst, t):
        for k, x in enumerate(lst):
            if x is t:
                del lst[k]
                return

    def toggle_todo(self, t):
        src = t.get("_src")
        if src is not None:                       # 반복 일정: 날짜별로 완료 표시
            dd = src.setdefault("done_dates", [])
            iso = t["_occ"].isoformat()
            if iso in dd:
                dd.remove(iso)
            else:
                dd.append(iso)
            core.touch(src)
        else:
            t["done"] = not t.get("done", False)
            core.touch(t)
        self._after_todo_change()

    def cycle_pri(self, t):
        b = core.base_of(t)
        b["pri"] = next_pri(pri_of(b))
        core.touch(b)
        self._after_todo_change()

    def set_pri(self, t, p):
        b = core.base_of(t)
        b["pri"] = p
        core.touch(b)
        self._after_todo_change()

    def delete_todo(self, lst, t):
        core.delete_item(self.data, t)
        self._after_todo_change()

    def to_inbox(self, lst, t):
        b = core.base_of(t)
        core.remove_item(self.data, b)
        for k in ("time", "end", "repeat", "done_dates"):
            b.pop(k, None)
        b["done"] = False
        core.touch(b)
        core.add_item(self.data, "_inbox", b)
        self._after_todo_change()

    def pull_from_inbox(self, t):
        core.remove_item(self.data, t)
        if self.sel_end and self.sel_end > self.selected:
            t["end"] = self.sel_end.isoformat()
        core.touch(t)
        core.add_item(self.data, self.selected.isoformat(), t)
        self._after_todo_change()

    def postpone(self, lst, t):
        core.remove_item(self.data, t)
        core.touch(t)
        core.add_item(self.data, (self.selected + dt.timedelta(days=1)).isoformat(), t)
        self._after_todo_change()

    def unspan(self, t):
        t.pop("end", None)
        core.touch(t)
        self._after_todo_change()

    def move_item(self, t, d_from, d_to):
        """달력에서 일정을 끌어 다른 날짜로 옮김 (기간 일정은 기간 전체가 같은 만큼 이동)"""
        b = core.base_of(t)
        key, _ = core.find_owner(self.data, b)
        start = core.parse_date(key)
        delta = (d_to - d_from).days
        if start is None or delta == 0:
            return
        e = item_end(b, start)
        core.remove_item(self.data, b)
        if e:
            b["end"] = (e + dt.timedelta(days=delta)).isoformat()
        core.touch(b)
        core.add_item(self.data, (start + dt.timedelta(days=delta)).isoformat(), b)
        self._after_todo_change()

    def unspan(self, t):
        t.pop("end", None)
        self._after_todo_change()

    def _row_menu(self, e, lst, t, start):
        m = tk.Menu(self.root, tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT,
                    activeforeground="#ffffff", bd=0, font=self.f_small)
        m.add_command(label="수정…", command=lambda: self.edit_item(t))
        cid = core.base_of(t).get("case")
        if cid and cid in (self.data.get("_work") or {}):
            m.add_command(label="업무 열기 (기한·폴더)", command=lambda: self.open_work(cid))
        if core.base_of(t).get("src_path"):
            m.add_command(label="공문 원본 열기", command=lambda: open_path(core.base_of(t)["src_path"]))
        m.add_separator()
        if self.mode == "inbox":
            m.add_command(label="선택한 날짜로 꺼내기", command=lambda: self.pull_from_inbox(t))
        elif t.get("repeat"):
            pass
        else:
            if item_end(t, start):
                m.add_command(label="하루 일정으로 바꾸기", command=lambda: self.unspan(t))
            elif start == self.selected:
                m.add_command(label="내일로 미루기", command=lambda: self.postpone(lst, t))
        if self.mode != "inbox":
            m.add_command(label="보관함으로 보내기", command=lambda: self.to_inbox(lst, t))
        m.add_separator()
        for p, (name, _) in list(enumerate(PRIORITIES))[1:]:
            m.add_command(label=("✓ " if pri_of(t) == p else "    ") + f"● {name}",
                          command=lambda p=p: self.set_pri(t, p))
        m.add_separator()
        m.add_command(label="삭제", command=lambda: self.delete_todo(lst, t))
        m.tk_popup(e.x_root, e.y_root)
        return "break"

    def _after_todo_change(self):
        self._commit()
        self.render_todos()
        self.render_month()
        self._refresh_detail()

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
        self._summoned = False
        self.send_to_bottom()

    # ── 공문 PDF ──
    def import_pdf(self):
        path = filedialog.askopenfilename(parent=self.root, title="공문 선택 (PDF·한글)",
                                          filetypes=[("공문 (PDF·한글·워드)", "*.pdf *.hwp *.hwpx *.docx"),
                                                     ("모든 파일", "*.*")])
        if path:
            self.import_path(path)

    # ── 업무 ──
    DOC_EXT = (".pdf", ".hwp", ".hwpx", ".docx", ".txt")

    def work_root(self):
        r = self.conf.get("work_root")
        if r:
            return r
        docs = os.path.join(os.path.expanduser("~"), "Documents")
        if IS_WIN:
            try:
                buf = ctypes.create_unicode_buffer(260)
                if ctypes.windll.shell32.SHGetFolderPathW(None, 5, None, 0, buf) == 0 and buf.value:
                    docs = buf.value                       # 내 문서 (OneDrive 로 옮겨 둔 경우도)
            except Exception:
                pass
        return os.path.join(docs, "DeskCalendar 업무")

    def read_doc_text(self, path):
        if path.lower().endswith(".pdf"):
            return gongmun.extract_text(path)
        return hwpdoc.read_text(path)

    def looks_like_gongmun(self, path, text=None):
        """PDF 는 공문으로 본다. 한글·워드는 '제목' 줄과 수신·시행 같은 공문 표지가 있어야 공문"""
        if path.lower().endswith(".pdf"):
            return True
        try:
            text = text if text is not None else self.read_doc_text(path)
        except Exception:
            return False
        return bool(re.search(r"^\s*제\s*목", text, re.M)) and bool(re.search(r"수\s*신|시\s*행|발\s*신|기안자|협조자", text))

    def copy_text(self, text):
        self._own_clip = text
        self.root.clipboard_clear()
        self.root.clipboard_append(text)
        self.root.update()

    def open_work(self, select=None):
        if self.workwin is not None:
            try:
                self.workwin.top.deiconify()
                self.workwin.top.lift()
                if select:
                    self.workwin.select(select)
                return self.workwin
            except tk.TclError:
                self.workwin = None
        self.workwin = WorkWindow(self, select)
        return self.workwin

    def open_templates(self):
        TemplateDialog(self)

    def case_from_doc(self, path, text, title, items, linked):
        """공문 한 건 → 업무 하나 (폴더를 만들고 공문 원본을 넣고, 추가한 일정을 묶는다). 같은 제목 업무가 있으면 그걸 쓴다"""
        if not self.conf.get("auto_case", True):
            return None, False
        title = (title or os.path.splitext(os.path.basename(path))[0]).strip()
        for c in (self.data.get("_work") or {}).values():
            if c.get("title") == title and not c.get("done"):
                for t in linked:
                    t["case"] = c["id"]
                return c, False
        due = work.due_from_items(items)
        c = work.new_case(title, due, doc_no=work.doc_no(text), received=self.today.isoformat())
        work.add_case(self.data, c)
        try:
            work.ensure_folder(self.work_root(), c)
            work.add_doc(c, path)
        except OSError:
            pass
        for t in linked:
            t["case"] = c["id"]
        return c, True

    def import_path(self, path):
        try:
            text = self.read_doc_text(path)
        except Exception as ex:
            messagebox.showerror("공문 읽기 실패", f"파일을 읽지 못했어요.\n{ex}", parent=self.root)
            return
        if len(text.strip()) < 20:
            messagebox.showinfo("글자를 읽을 수 없어요",
                                "이 PDF에는 읽을 수 있는 글자가 거의 없어요.\n"
                                "스캔(이미지) 파일이면 지원하지 않아요. 글자가 선택되는 PDF를 사용해 주세요.",
                                parent=self.root)
            return
        title, items = gongmun.parse(text, self.today)
        ImportDialog(self, title, items, src_path=os.path.abspath(path), doc_text=text)

    IMG_EXT = (".png", ".jpg", ".jpeg", ".bmp", ".webp")

    def _show_candidates(self, text, heading="붙여넣은 내용에서 일정 만들기"):
        """글 → 일정/할 일 후보 → 확인 창"""
        items = analyze.analyze(text, self.today)
        if not items:
            messagebox.showinfo("분석 결과", "일정이나 할 일로 보이는 내용을 찾지 못했어요.\n"
                                "(날짜나 '제출·회신·확인' 같은 요청이 들어 있어야 해요)", parent=self.root)
            return
        ImportDialog(self, "", items, heading=heading)

    def analyze_clipboard(self):
        """복사해 둔 글·캡처 이미지·PDF 파일을 분석해서 일정 후보로 만든다"""
        self.root.update_idletasks()
        try:
            from PIL import ImageGrab
            clip = ImageGrab.grabclipboard() if IS_WIN else None
        except Exception:
            clip = None
        if isinstance(clip, list):                       # 탐색기에서 파일을 복사한 경우
            self.import_dropped([str(f) for f in clip])
            return
        if clip is not None:                             # 캡처 이미지
            self._ocr_then_show(lambda: ocr.read_pil(clip))
            return
        try:
            text = self.root.clipboard_get()
        except tk.TclError:
            text = ""
        if len(text.strip()) < 6:
            messagebox.showinfo("클립보드가 비어 있어요", "메신저·메일의 내용을 복사(Ctrl+C)하거나\n화면을 캡처(Win+Shift+S)한 뒤 다시 눌러 주세요.",
                                parent=self.root)
            return
        self._show_candidates(text)

    def _ocr_then_show(self, reader):
        self.root.config(cursor="watch")
        self.root.update_idletasks()
        try:
            text = reader()
        except ocr.OcrError as ex:
            self.root.config(cursor="")
            messagebox.showinfo("글자 인식", str(ex), parent=self.root)
            return
        self.root.config(cursor="")
        self._show_candidates(text, "캡처한 이미지에서 일정 만들기")

    def import_dropped(self, files, open_case=True):
        """끌어다 놓은 파일 처리
        - 캡처 이미지 → 글자 인식 → 확인 창
        - 공문(PDF·한글) → 핵심 내용만 일정으로 바로 추가 + 업무 폴더 (실행 취소 가능)
        - 그 밖의 엑셀·한글 파일 → 어느 업무의 양식/제출 파일인지 골라서 넣기"""
        imgs = [f for f in files if f.lower().endswith(self.IMG_EXT)]
        if imgs:
            self._ocr_then_show(lambda: "\n".join(ocr.read_image(p) for p in imgs[:3]))
            return
        docs, others, texts = [], [], {}
        for f in files:
            if f.lower().endswith(self.DOC_EXT):
                try:
                    texts[f] = self.read_doc_text(f)
                except Exception:
                    texts[f] = ""
                if self.looks_like_gongmun(f, texts[f]):
                    docs.append(f)
                    continue
            others.append(f)
        if others:
            self.route_files(others)
        if not docs:
            return
        added, bad, cases_new, cases_all = [], [], [], []
        for path in docs:
            text = texts.get(path, "")
            if len(text.strip()) < 20:
                bad.append(os.path.basename(path))
                continue
            title, items = gongmun.parse(text, self.today)
            mine = []
            for it in items:
                d, end = it["date"], it["end"]
                note = "\n".join(x for x in (it.get("detail") if it.get("detail") != it["text"] else "", title) if x)
                if end and end > d and not it.get("period"):      # 마감형 기간은 종료일 하루에만 표시
                    note += f" · 기간 {d.month}/{d.day} ~ {end.month}/{end.day}"
                    d, end = end, None
                if any(t.get("text") == it["text"] for _, t, _ in core.day_items(self.data, d)):
                    continue                                       # 같은 날 같은 내용은 건너뜀 (중복 방지)
                extra = {}
                if it.get("time"):
                    extra["time"] = it["time"]
                if end and end > d:
                    extra["end"] = end.isoformat()
                item = core.new_item(it["text"], pri=it["pri"], note=note, src_path=os.path.abspath(path), **extra)
                core.add_item(self.data, d.isoformat(), item)
                mine.append(item)
            added += mine
            c, new = self.case_from_doc(os.path.abspath(path), text, title, items, mine)
            if c:
                cases_all.append(c)
                if new:
                    cases_new.append(c)
        if added or cases_new:
            self._after_todo_change()
        msg = f"일정 {len(added)}개를 달력에 추가했어요." if added else (
            "이미 가져온 공문이에요. (일정·업무가 이미 있어요)" if cases_all and not cases_new else "추가할 새 일정을 찾지 못했어요.")
        if cases_new:
            c = cases_new[0]
            msg += f"\n업무 폴더를 만들었어요: {c['title'][:24]}"
            if c.get("due"):
                msg += f" (기한 {work.fmt_date(c['due'])})"
        if bad:
            msg += f"\n읽지 못한 파일: {', '.join(bad[:3])} (스캔 이미지 PDF 는 지원하지 않아요)"

        def undo():
            for item in added:
                core.delete_item(self.data, item)
            for c in cases_new:
                work.delete_case(self.data, c["id"])
            self._after_todo_change()
            if self.workwin is not None:
                self.workwin.sel = None
                self.workwin.build_left()
                self.workwin.build_right()
        acts = []
        if cases_all and open_case:
            acts.append(("업무 열기", lambda: self.open_work(cases_all[0]["id"])))
        if added or cases_new:
            acts.append(("실행 취소", undo))
        NotifyPopup(self, "공문 가져오기", msg, actions=acts)
        if self.workwin is not None:
            self.workwin.build_left()

    def route_files(self, files):
        """공문이 아닌 파일(작성 중인 엑셀·한글 등)을 어느 업무 폴더에 넣을지 고르는 메뉴"""
        cs = work.sorted_cases(self.data)
        menu_kw = dict(tearoff=0, bg=CARD, fg=TEXT, activebackground=ACCENT, activeforeground="#ffffff", bd=0,
                       font=(self.family, 10))
        m = tk.Menu(self.root, **menu_kw)
        label = os.path.basename(files[0]) + (f" 외 {len(files) - 1}개" if len(files) > 1 else "")
        m.add_command(label=f"{label} → 어느 업무 폴더에 넣을까요?", state="disabled")
        for c in cs[:8]:
            sub = tk.Menu(m, **menu_kw)
            for k, name in enumerate(work.SUBDIRS):
                sub.add_command(label=name, command=lambda c=c, k=k: self.open_work(c["id"]).put_files(files, k))
            m.add_cascade(label=f"{c['title'][:26]}  {work.dday_label(work.dday(c, self.today))}", menu=sub)
        m.add_separator()

        def new_case():
            c = work.add_case(self.data, work.new_case(os.path.splitext(os.path.basename(files[0]))[0], None))
            self._after_todo_change()
            self.open_work(c["id"]).put_files(files, 1)
        m.add_command(label="새 업무 만들어 넣기", command=new_case)
        texts = [f for f in files if f.lower().endswith(self.DOC_EXT)]
        if texts:
            m.add_command(label="글에서 일정 찾기", command=lambda: self._show_candidates(
                "\n".join(self.read_doc_text(f) for f in texts[:3]), "파일에서 일정 만들기"))
        x, y = self.root.winfo_pointerxy()
        m.tk_popup(x, y)

    # ── 저장 ──
    def _commit(self):
        for k in list(self.data):
            v = self.data[k]
            if k in ("_deleted", "_work"):
                continue
            if not v.get("memo", "").strip() and not v.get("todos"):
                del self.data[k]
        try:
            save_json(DATA_FILE, self.data)
        except Exception:
            pass
        if not self._in_sync:
            self._schedule_sync()
        st = getattr(self, "stickers", None)
        if st is not None:
            st.refresh()

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
            r = self.root
            x, y, _, _ = self._fit_rect(e.x_root - self._drag[0], e.y_root - self._drag[1],
                                        r.winfo_width(), r.winfo_height())
            r.geometry(f"+{x}+{y}")

    def _drag_end(self, e):
        if self._drag:
            self._drag = None
            self._gkey = None            # 창 위치가 바뀌면 유리 배경도 새 위치의 바탕화면으로 다시 만든다
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
        self.stickers.save()
        try:
            if self.tray is not None:
                self.tray.stop()
        except Exception:
            pass
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
        self._hook_drop()

    def _hook_drop(self):
        """창 전체(달력 포함)에 공문 PDF 를 끌어다 놓을 수 있게 한다"""
        if not IS_WIN:
            return
        try:
            hs = [self.hwnd, self.root.winfo_id()]
            stack = [self.root]
            while stack:
                w = stack.pop()
                stack.extend(w.winfo_children())
                if w is not self.root:
                    try:
                        hs.append(w.winfo_id())
                    except tk.TclError:
                        pass
            hook_dropfiles(hs, self.drop_q, self._drop_keep)
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
            self._summoned = False
            self.send_to_bottom()

    def _bottom_soon(self):
        """위젯을 눌러 쓴 뒤에도 다른 창 위로 올라오지 않고 계속 뒤에 머문다"""
        if not self._summoned:
            self.send_to_bottom()

    def _poll_hotkey(self):
        try:
            while True:
                self.hotkey_q.get_nowait()
                self.summon()
        except queue.Empty:
            pass
        try:
            while True:
                self._tray_cmd(self.tray_q.get_nowait())
        except queue.Empty:
            pass
        try:
            while True:
                self.import_dropped(self.drop_q.get_nowait())
        except queue.Empty:
            pass
        self.root.after(150, self._poll_hotkey)

    def _tray_cmd(self, cmd):
        if cmd == "show":
            self.summon(popup=False)
        elif cmd == "add":
            self.summon()
        elif cmd == "clip":
            self.summon(popup=False)
            self.analyze_clipboard()
        elif cmd == "phone":
            self.summon(popup=False)
            PhoneDialog(self)
        elif cmd == "work":
            self.open_work()

        elif cmd == "settings":
            self.summon(popup=False)
            self.open_settings()
        elif cmd == "quit":
            self.quit()

    def open_cmdbar(self):
        if getattr(self, "cmdbar", None) is not None:
            try:
                self.cmdbar.top.lift()
                self.cmdbar._focus()
                return
            except tk.TclError:
                self.cmdbar = None
        self.cmdbar = CommandBar(self)

    def summon(self, popup=True):
        """위젯을 맨 앞으로 불러온다. popup=True 면 오늘 날짜의 메모/입력 창도 연다"""
        self._summoned = True
        self.go_today()
        self.render_month()
        if IS_WIN and self.hwnd:
            u = ctypes.windll.user32
            u.SetWindowPos(self.hwnd, 0, 0, 0, 0, 0, 0x0043)  # HWND_TOP, NOSIZE|NOMOVE|SHOWWINDOW
            u.keybd_event(0x12, 0, 0, 0)  # Alt 키 한 번 눌러 포그라운드 잠금 해제
            u.keybd_event(0x12, 0, 2, 0)
            u.SetForegroundWindow(self.hwnd)
        self.root.focus_force()
        if popup:
            self.open_cmdbar()

    def _clip_watch(self):
        """(설정에서 켠 경우) 메신저 등에서 복사한 글에 날짜·요청이 있으면 오른쪽 아래에 알려 준다"""
        self.root.after(1500, self._clip_watch)
        on = self.conf.get("clip_watch", False)
        try:
            text = self.root.clipboard_get()
        except tk.TclError:
            text = ""
        if text == self._clip_last:
            return
        first = self._clip_last is None
        self._clip_last = text
        if not on or first or text == self._own_clip or not (8 <= len(text.strip()) <= 4000):
            return
        try:
            items = [i for i in analyze.analyze(text, self.today) if i["date"]]
        except Exception:
            return
        items = [i for i in items if not self.has_similar(i["date"], i["text"])]
        if not items:
            return
        i0 = items[0]
        body = f"{work.fmt_date(i0['date'])} {i0['text'][:28]}" + (f" 외 {len(items) - 1}개" if len(items) > 1 else "")
        for p in list(self.popups):                       # 앞의 같은 알림은 닫고 새로
            if getattr(p, "kind", "") == "clip":
                p.close()
        pop = NotifyPopup(self, "복사한 글에서 일정을 찾았어요", body, beep=False,
                          actions=[("확인하고 추가", lambda: self._show_candidates(text, "복사한 글에서 일정 만들기"))])
        pop.kind = "clip"

    def _tick(self):
        if self.glass:     # 바탕화면 그림이 바뀌었으면 유리 배경도 새로 만든다
            sig = glass.wallpaper_sig()
            if sig != getattr(self, "_wsig", None):
                self._wsig = sig
                self._gkey = None
                self.draw_calendar()
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
    pdfs = [a for a in sys.argv[1:] if a.lower().endswith((".pdf", ".hwp", ".hwpx")) and os.path.exists(a)]
    if IS_WIN:
        if not ensure_single_instance():     # 이미 실행 중이면 PDF 경로만 넘기고 종료
            if pdfs:
                os.makedirs(DATA_DIR, exist_ok=True)
                with open(REQUEST_FILE, "a", encoding="utf-8") as f:
                    f.write(os.path.abspath(pdfs[0]) + "\n")
            return
        enable_dpi_awareness()
    os.makedirs(DATA_DIR, exist_ok=True)
    root = tk.Tk()
    DeskCalendar(root, pdfs)
    root.mainloop()


if __name__ == "__main__":
    main()
