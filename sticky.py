"""바탕화면 스티커: 날짜 하나의 일정과 메모를 한 장에 보여 주는 작은 창.
끌어서 옮기고, 접고 펴고, 크기·색을 바꿀 수 있다. 메모는 달력의 그날 메모와 같은 것이라 어디서 고쳐도 같이 바뀐다.
스티커 위치·크기·색은 데이터(폰과 동기화되는 일정)가 아니라 이 PC의 설정(conf["stickies"])에만 저장한다."""
import ctypes
import datetime as dt
import re
import tkinter as tk
import tkinter.font as tkfont

import core

COLORS = [("노랑", "#fff1a6"), ("분홍", "#ffd3df"), ("민트", "#c8f1dc"),
          ("하늘", "#cde5ff"), ("보라", "#ded5ff"), ("회색", "#e6e6ec")]
INK, INK_SUB, INK_DIM = "#2a2b36", "#6a6c7a", "#9a9caa"
PRI_INK = ["#c8374a", "#c8374a", "#2a2b36", "#1f8a5b"]
WD = "월화수목금토일"


def _mix(a, b, t):
    """a 색에 b 색을 t(0~1)만큼 섞는다"""
    pa = [int(a[i:i + 2], 16) for i in (1, 3, 5)]
    pb = [int(b[i:i + 2], 16) for i in (1, 3, 5)]
    return "#%02x%02x%02x" % tuple(round(x + (y - x) * t) for x, y in zip(pa, pb))


# ── 메모 서식 ────────────────────────────────────────────
# 메모는 폰·검색·동기화와 같은 '그냥 글자'로 저장하고(**굵게** ~~취소선~~ {빨강|색 글자}, 1. 번호, - 글머리, - [ ] 체크리스트),
# 화면에서는 표시를 감추고 진짜 서식으로 보여 준다: 굵게는 그냥 굵게, 체크는 ☐/☑ (누르면 바뀜), 글머리는 •.
MEMO_COLORS = [("빨강", "#d1344a"), ("주황", "#d9730d"), ("초록", "#1f8a5b"), ("파랑", "#2f6bd1"), ("보라", "#7048c9")]
_RX_PAT = re.compile(r"\*\*(.+?)\*\*|~~(.+?)~~|\{(" + "|".join(n for n, _ in MEMO_COLORS) + r")\|(.+?)\}")
_RX_CHECK = re.compile(r"^(\s*)- \[( |x|X)\] ?(.*)$")
_RX_NUM = re.compile(r"^(\s*)(\d+)\. (.*)$")
_RX_BUL = re.compile(r"^(\s*)- (.*)$")
_D_PRE = re.compile(r"^(\s*)([☐☑] |• |\d+\. )")                # 화면에 보이는 줄 머리
_RX_AUTO = re.compile(r"^(\s*)(- \[ \] |- \[[xX]\] |\[\] |\[ \] |\[[xX]\] |- |\* )$")   # 줄 머리를 손으로 치면 바꿔 준다
_COLOR_OF = dict(MEMO_COLORS)


def strip_markup(text):
    """달력 칸·검색에서 보여 줄 때: 서식 표시를 빼고 글자만 (체크는 ☐/☑, 글머리는 •)"""
    out = []
    for line in (text or "").split("\n"):
        m = _RX_CHECK.match(line)
        if m:
            line = m.group(1) + ("☑ " if m.group(2) in "xX" else "☐ ") + m.group(3)
        else:
            m = _RX_BUL.match(line)
            if m:
                line = m.group(1) + "• " + m.group(2)
        prev = None
        while prev != line:
            prev = line
            line = _RX_PAT.sub(lambda k: k.group(1) or k.group(2) or k.group(4), line)
        out.append(line)
    return "\n".join(out)


class MemoFormat:
    """메모 칸(tk.Text)을 진짜 서식 편집기로 만든다: 굵게·취소선·글자색은 선택한 글자(또는 앞으로 칠 글자)에,
    번호·글머리·체크리스트는 줄 머리로. 저장할 땐 get_markup(), 보여 줄 땐 set_markup() 으로 글자 표시와 오간다."""

    def __init__(self, text, app, on_change):
        self.t, self.on_change = text, on_change
        base = tkfont.Font(font=text.cget("font"))
        bold = base.copy()
        bold.configure(weight="bold")
        strike = base.copy()
        strike.configure(overstrike=True)
        self._fonts = (base, bold, strike)               # 글꼴 객체를 붙들어 둔다 (없어지면 서식이 사라짐)
        t = text
        t.tag_configure("bold", font=bold)
        t.tag_configure("strike", overstrike=True)
        t.tag_configure("done", overstrike=True, foreground=INK_DIM)
        for name, col in MEMO_COLORS:
            t.tag_configure("c_" + name, foreground=col)
        t.tag_raise("done")
        t.tag_raise("sel")
        self._pend = {}                                  # 글자를 고르지 않고 누른 서식: 이어서 칠 글자에 적용
        t.mark_set("pend", "insert")
        t.mark_gravity("pend", "left")
        t.bind("<Return>", self._return)
        t.bind("<BackSpace>", self._back)
        t.bind("<Button-1>", self._click, add="+")
        t.bind("<Control-b>", lambda e: (self.toggle("bold"), "break")[1])
        t.bind("<Control-B>", lambda e: (self.toggle("bold"), "break")[1])

    # ── 저장 글자 ↔ 화면 ──
    def set_markup(self, s):
        t = self.t
        t.delete("1.0", "end")
        for i, line in enumerate((s or "").split("\n")):
            if i:
                t.insert("end-1c", "\n")
            m = _RX_CHECK.match(line)
            if m:
                t.insert("end-1c", m.group(1) + ("☑ " if m.group(2) in "xX" else "☐ "))
                line = m.group(3)
            else:
                m = _RX_BUL.match(line)
                if m:
                    t.insert("end-1c", m.group(1) + "• ")
                    line = m.group(2)
            self._emit(line, ())
        self._pend.clear()
        self._lists()

    def _emit(self, text, tags):
        t, pos = self.t, 0
        for m in _RX_PAT.finditer(text):
            if m.start() > pos:
                t.insert("end-1c", text[pos:m.start()], tags)
            if m.group(1) is not None:
                self._emit(m.group(1), tags + ("bold",))
            elif m.group(2) is not None:
                self._emit(m.group(2), tags + ("strike",))
            else:
                self._emit(m.group(4), tags + ("c_" + m.group(3),))
            pos = m.end()
        if pos < len(text):
            t.insert("end-1c", text[pos:], tags)

    def get_markup(self):
        t, active, lines = self.t, set(), [[]]
        for key, val, _ in t.dump("1.0", "end-1c", text=True, tag=True):
            if key == "tagon":
                active.add(val)
            elif key == "tagoff":
                active.discard(val)
            else:
                fm = frozenset(a for a in active if a in ("bold", "strike") or a.startswith("c_"))
                for j, part in enumerate(val.split("\n")):
                    if j:
                        lines.append([])
                    if part:
                        lines[-1].append([part, fm])
        out = []
        for runs in lines:
            plain = "".join(r[0] for r in runs)
            m = _D_PRE.match(plain)
            head = ""
            if m:                                           # 줄 머리는 저장 글자(- [ ] / - )로, 번호는 그대로
                n = m.end()
                g = m.group(2)
                head = m.group(1) + {"☐ ": "- [ ] ", "☑ ": "- [x] ", "• ": "- "}.get(g, g)
                while n and runs:                            # 줄 머리 글자는 서식 없이 따로 빼 둔다
                    take = min(n, len(runs[0][0]))
                    runs[0][0] = runs[0][0][take:]
                    n -= take
                    if not runs[0][0]:
                        runs.pop(0)
            merged = []
            for txt, fm in runs:
                if merged and merged[-1][1] == fm:
                    merged[-1][0] += txt
                else:
                    merged.append([txt, fm])
            out.append(head + "".join(self._wrap(txt, fm) for txt, fm in merged))
        return "\n".join(out)

    @staticmethod
    def _wrap(txt, fm):
        s = txt
        if "strike" in fm:
            s = f"~~{s}~~"
        if "bold" in fm:
            s = f"**{s}**"
        for c in fm:
            if c.startswith("c_"):
                s = "{" + c[2:] + "|" + s + "}"
        return s

    # ── 글자 꾸미기 ──
    def _sel(self):
        try:
            return self.t.index("sel.first"), self.t.index("sel.last")
        except tk.TclError:
            return None

    def _all_tagged(self, tag, a, b):
        t = self.t
        n = len(t.get(a, b))
        return n > 0 and all(tag in t.tag_names(f"{a}+{k}c") for k in range(n) if t.get(f"{a}+{k}c") != "\n")

    def toggle(self, tag):
        t, sel = self.t, self._sel()
        if sel:
            a, b = sel
            (t.tag_remove if self._all_tagged(tag, a, b) else t.tag_add)(tag, a, b)
            t.focus_force()
            self._changed()
            return
        t.mark_set("pend", "insert")                      # 고른 글자가 없으면 이어서 칠 글자에 켜고 끈다
        self._pend[tag] = not self._pend.get(tag, tag in t.tag_names("insert-1c"))
        t.focus_force()

    def color(self, name):
        t, sel = self.t, self._sel()
        cols = ["c_" + n for n, _ in MEMO_COLORS]
        if sel:
            a, b = sel
            for c in cols:
                t.tag_remove(c, a, b)
            if name:
                t.tag_add("c_" + name, a, b)
            self._changed()
            return
        t.mark_set("pend", "insert")
        for c in cols:
            self._pend[c] = False
        if name:
            self._pend["c_" + name] = True
        t.focus_force()

    def on_key(self, e=None):
        """키를 뗄 때: 미리 눌러 둔 서식을 방금 친 글자에 입히고, 손으로 친 줄 머리('- ', '[] ')를 바꿔 주고, 저장 예약"""
        t = self.t
        if e is not None and e.keysym in ("Left", "Right", "Up", "Down", "Home", "End", "Prior", "Next"):
            self._pend.clear()
            t.mark_set("pend", "insert")
            return
        if self._pend and t.compare("insert", ">", "pend"):
            for tag, on in self._pend.items():
                (t.tag_add if on else t.tag_remove)(tag, "pend", "insert")
            t.mark_set("pend", "insert")
        ln = t.index("insert").split(".")[0]
        head = t.get(f"{ln}.0", "insert")
        m = _RX_AUTO.match(head)
        if m and t.index("insert") == t.index(f"{ln}.{len(head)}"):
            g = m.group(2)
            glyph = "☑ " if "x" in g.lower() and "[" in g else "☐ " if "[" in g else "• "
            t.delete(f"{ln}.{len(m.group(1))}", "insert")
            t.insert("insert", glyph)
        self._changed()

    # ── 줄 머리: 번호 / 글머리 / 체크 (선택한 줄 전체, 이미 있으면 뺀다) ──
    def prefix(self, kind):
        t = self.t
        sel = self._sel()
        if sel:
            l1, l2 = int(sel[0].split(".")[0]), int(sel[1].split(".")[0])
        else:
            l1 = l2 = int(t.index("insert").split(".")[0])
        want = {"check": "☐ ", "num": "1. ", "bul": "• "}[kind]

        def kind_of(line):
            m = _D_PRE.match(line)
            if not m:
                return None
            g = m.group(2)
            return "check" if g in ("☐ ", "☑ ") else "bul" if g == "• " else "num"
        lines = [t.get(f"{i}.0", f"{i}.end") for i in range(l1, l2 + 1)]
        all_same = all(kind_of(x) == kind for x in lines if x.strip())
        n = 0
        for i, x in zip(range(l1, l2 + 1), lines):
            m = _D_PRE.match(x)
            ind = m.group(1) if m else re.match(r"^(\s*)", x).group(1)
            a = f"{i}.{len(ind)}"
            if m:
                t.delete(a, f"{i}.{m.end()}")
            if all_same and x.strip():
                continue
            n += 1
            t.insert(a, f"{n}. " if kind == "num" else want)
        t.focus_force()
        self._changed()

    # ── Enter / 지우기 / 누르기 ──
    def _return(self, e):
        t = self.t
        ln = t.index("insert").split(".")[0]
        line = t.get(f"{ln}.0", f"{ln}.end")
        m = _D_PRE.match(line)
        if not m or int(t.index("insert").split(".")[1]) < m.end():
            return None
        if not line[m.end():].strip():                    # 비어 있는 목록 줄에서 Enter: 목록 끝내기
            t.delete(f"{ln}.{len(m.group(1))}", f"{ln}.end")
            self._changed()
            return "break"
        g = m.group(2)
        nxt = "☐ " if g in ("☐ ", "☑ ") else g if g == "• " else f"{int(g.split('.')[0]) + 1}. "
        t.insert("insert", "\n" + m.group(1) + nxt)
        t.see("insert")
        self._changed()
        return "break"

    def _back(self, e):
        t = self.t
        if self._sel():
            return None
        ln, col = t.index("insert").split(".")
        m = _D_PRE.match(t.get(f"{ln}.0", f"{ln}.end"))
        if m and int(col) == m.end():                     # 줄 머리 바로 뒤에서 지우기: 줄 머리를 통째로
            t.delete(f"{ln}.{len(m.group(1))}", f"{ln}.{m.end()}")
            self._changed()
            return "break"
        return None

    def _click(self, e):
        t = self.t
        self._pend.clear()
        ln, col = t.index(f"@{e.x},{e.y}").split(".")
        m = _D_PRE.match(t.get(f"{ln}.0", f"{ln}.end"))
        if m and m.group(2) in ("☐ ", "☑ ") and len(m.group(1)) <= int(col) <= len(m.group(1)) + 1:
            p = f"{ln}.{len(m.group(1))}"                  # 체크 칸을 누르면 ☐ ↔ ☑
            t.delete(p, f"{p}+1c")
            t.insert(p, "☐" if m.group(2) == "☑ " else "☑")
            t.focus_force()
            self._changed()
            return "break"
        return None

    def _lists(self):
        """체크한 줄(☑)은 취소선·흐리게"""
        t = self.t
        t.tag_remove("done", "1.0", "end")
        for i, line in enumerate(t.get("1.0", "end-1c").split("\n"), 1):
            m = _D_PRE.match(line)
            if m and m.group(2) == "☑ ":
                t.tag_add("done", f"{i}.{m.end()}", f"{i}.end")

    def _changed(self):
        self._lists()
        self.t.edit_modified(True)                        # 서식만 바꿨어도 '고쳤다'고 표시해 저장되게
        self.on_change()


def S_(note, v):
    return note.app.S(v)


class StickyManager:
    def __init__(self, app, ui):
        self.app, self.ui = app, ui
        self.notes = {}
        self._owner = None
        self.hidden = bool(app.conf.get("stickies_hidden"))
        self._tick()

    def owner_hwnd(self, u):
        """스티커들의 보이지 않는 주인 창 (1픽셀, 투명, 화면 밖). 작업표시줄·Alt+Tab 에 나오지 않는다"""
        try:
            if self._owner is None or not self._owner.winfo_exists():
                o = self._owner = tk.Toplevel(self.app.root)
                o.overrideredirect(True)
                o.geometry("1x1+-3000+-3000")
                o.attributes("-alpha", 0.0)
                o.update_idletasks()
                wid = o.winfo_id()
                for h in {wid, u.GetParent(wid)}:
                    if h:
                        ex = u.GetWindowLongPtrW(h, -20)
                        u.SetWindowLongPtrW(h, -20, (ex | 0x80) & ~0x40000)
            u.GetAncestor.restype = ctypes.c_void_p
            u.GetAncestor.argtypes = [ctypes.c_void_p, ctypes.c_uint]
            return u.GetAncestor(self._owner.winfo_id(), 2)
        except Exception:
            return None

    def restore(self):
        """윈도우를 켤 때: 마지막으로 두었던 자리·크기·접힘 상태 그대로 나타난다
        (접어 둔 스티커는 한 줄로 접힌 채, 펼쳐 둔 스티커는 펼쳐진 채).
        부팅 직후엔 화면 크기·배율이 아직 자리 잡지 않았을 수 있어 몇 초 뒤 한 번 더 제자리에 놓는다."""
        for c in list(self.app.conf.get("stickies", [])):
            self._open(c)
        for ms in (1500, 6000, 15000):
            try:
                self.app.root.after(ms, self.replace_all)
            except tk.TclError:
                pass

    def replace_all(self):
        for n in list(self.notes.values()):
            if not n._dragging:
                n._place()

    def add(self, d=None, inbox=False):
        """새 스티커. d 가 없으면 '오늘'을 따라가는 스티커, inbox=True 면 날짜 없는 칸(보관함)용 스티커"""
        app = self.app
        S = app.S
        n = len(self.notes)
        x0, y0, w0, _ = self.ui.work_area(app.root)
        used = [int(k) for k in self.notes] + [int(c.get("id", 0)) for c in app.conf.get("stickies_closed", [])]
        conf = {"id": max(used + [0]) + 1,
                "date": "inbox" if inbox else (d.isoformat() if d else None),
                "x": x0 + w0 - S(300) - S(28) * (n % 6), "y": y0 + S(60) + S(34) * (n % 6),
                "w": S(280), "h": S(340), "folded": False, "color": COLORS[n % len(COLORS)][1], "top": False}
        self._open(conf)
        self.save()

    def _open(self, conf):
        note = Sticky(self, conf)
        self.notes[str(conf["id"])] = note
        if self.hidden:
            note.top.withdraw()

    def remove(self, note):
        """닫은 스티커는 지우지 않고 '닫아 둔 목록'에 자리·크기·내용 설정째 남긴다 (스티커 버튼으로 한꺼번에 다시 연다)"""
        self.notes.pop(str(note.conf["id"]), None)
        closed = [c for c in self.app.conf.get("stickies_closed", []) if c.get("id") != note.conf.get("id")]
        closed.append(note.conf)
        self.app.conf["stickies_closed"] = closed[-30:]
        self.save()

    def open_all(self, new_for=False):
        """닫아 둔 스티커가 있으면 모두 한꺼번에 다시 띄우고, 떠 있는 스티커는 앞으로 가져온다.
        new_for 가 False 가 아니면('스티커' 버튼) 새 스티커도 하나 더 띄운다 (None = 오늘을 따라가는 스티커, 날짜 = 그날 스티커)"""
        closed = list(self.app.conf.get("stickies_closed", []))
        self.app.conf["stickies_closed"] = []
        if self.hidden:
            self.hidden = False
            self.app.conf["stickies_hidden"] = False
            for n in self.notes.values():
                n.top.deiconify()
        for c in closed:
            if str(c.get("id")) not in self.notes:
                self._open(c)
        if closed:
            self.save()
        for n in self.notes.values():
            try:
                n.top.deiconify()
                n.top.lift()
            except tk.TclError:
                pass
        if new_for is not False or not self.notes:
            self.add(new_for or None)

    def save(self):
        self.app.conf["stickies"] = [n.conf for n in self.notes.values()]
        self.app._save_conf()

    def refresh(self):
        for n in list(self.notes.values()):
            n.refresh()

    def toggle_hidden(self):
        """모든 스티커를 한꺼번에 숨기거나 다시 보인다 (윈도우 스티커 메모·Simple Sticky Notes 방식)"""
        self.hidden = not self.hidden
        for n in self.notes.values():
            n.top.withdraw() if self.hidden else n.top.deiconify()
        self.app.conf["stickies_hidden"] = self.hidden

    def reopen(self):
        """글자 크기·화면 배율이 바뀌면 같은 자리에 다시 만든다"""
        confs = [n.conf for n in self.notes.values()]
        self.close_all()
        for c in confs:
            self._open(c)

    def close_all(self):
        for n in list(self.notes.values()):
            n.top.destroy()
        self.notes.clear()

    def _tick(self):
        """자정이 지나면 '오늘' 스티커가 새 날짜로 바뀐다"""
        self.refresh()
        try:
            self.app.root.after(30000, self._tick)
        except tk.TclError:
            pass


class Sticky:
    BAR = 34

    def __init__(self, mgr, conf):
        self.mgr, self.conf = mgr, conf
        self.app, self.ui = mgr.app, mgr.ui
        S = self.app.S
        self.sig = None
        self._memo_job = None
        self._anim = None
        self._dragging = False
        self._mode = None            # 'postit'(일정 없음: 메모만) / 'full'
        self.show_entry = False
        self.top = top = tk.Toplevel(self.app.root)
        top._sticky = True
        top.overrideredirect(True)
        top.attributes("-topmost", bool(conf.get("top")))
        self._place()
        self.build()
        self._no_shadow()
        self.refresh(force=True)
        top.attributes("-alpha", float(conf.get("alpha", 1.0)))

    def _no_shadow(self):
        """Tk 의 테두리 없는 창은 창 클래스에 그림자(CS_DROPSHADOW)가 붙어서, 접거나 크기를 바꿀 때
        예전 크기의 그림자가 남는다. 그림자를 끄고 창을 한 번 다시 보인다."""
        if not self.ui.IS_WIN:
            return
        try:
            import ctypes
            u = ctypes.windll.user32
            u.GetClassLongPtrW.restype = ctypes.c_size_t
            u.GetClassLongPtrW.argtypes = [ctypes.c_void_p, ctypes.c_int]
            u.SetClassLongPtrW.restype = ctypes.c_size_t
            u.SetClassLongPtrW.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_size_t]
            self.top.update_idletasks()
            wid = self.top.winfo_id()
            for h in {wid, u.GetParent(wid)}:
                if h:
                    st = u.GetClassLongPtrW(h, -26)            # GCL_STYLE
                    if st & 0x20000:
                        u.SetClassLongPtrW(h, -26, st & ~0x20000)
            self._tool_window(u)
            if not self.mgr.hidden:
                self.top.withdraw()
                self.top.deiconify()
        except Exception:
            pass

    def _tool_window(self, u):
        """작업표시줄에 스티커마다 단추가 생기지 않게 '도구 창'으로 만든다 (트레이 아이콘 하나로 모아서 다룬다)"""
        u.GetWindowLongPtrW.restype = ctypes.c_ssize_t
        u.GetWindowLongPtrW.argtypes = [ctypes.c_void_p, ctypes.c_int]
        u.SetWindowLongPtrW.restype = ctypes.c_ssize_t
        u.SetWindowLongPtrW.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_ssize_t]
        wid = self.top.winfo_id()
        for h in {wid, u.GetParent(wid)}:
            if h:
                ex = u.GetWindowLongPtrW(h, -20)               # GWL_EXSTYLE
                u.SetWindowLongPtrW(h, -20, (ex | 0x80) & ~0x40000)   # TOOLWINDOW 켜고 APPWINDOW 끔
        # 다른 창에 '소유된' 창에는 윈도우가 작업표시줄 단추를 만들지 않는다 → 눈에 안 보이는 주인 창을 하나 두고 모든 스티커를 그 소유로
        u.GetAncestor.restype = ctypes.c_void_p
        u.GetAncestor.argtypes = [ctypes.c_void_p, ctypes.c_uint]
        owner = self.mgr.owner_hwnd(u)
        if owner:
            u.SetWindowLongPtrW(u.GetAncestor(wid, 2), -8, owner)   # GWLP_HWNDPARENT

    # ── 위치·크기 ──
    def _place(self):
        """저장해 둔 자리·크기에 놓는다. 화면 밖으로 나가 보일 때만 보이는 위치를 보정하고,
        저장된 값(conf)은 건드리지 않아서 부팅 직후 화면이 덜 준비돼 있어도 원래 자리를 잃지 않는다."""
        c, S = self.conf, self.app.S
        vx, vy, vw, vh = self.ui.virtual_screen(self.app.root)
        k = self.app.scale / (c.get("sc") or self.app.scale)       # 화면 배율이 달라졌으면 크기도 같은 비율로
        c["sc"] = self.app.scale
        if abs(k - 1) > .01:
            c["w"], c["h"] = round(c.get("w", S(280)) * k), round(c.get("h", S(340)) * k)
        c["w"] = max(S(250), int(c.get("w", S(280))))
        c["h"] = max(S(180), int(c.get("h", S(340))))
        x = min(max(vx, int(c.get("x", vx + 40))), vx + vw - S(120))
        y = min(max(vy, int(c.get("y", vy + 60))), vy + vh - S(60))
        if "x" not in c:
            c["x"], c["y"] = x, y
        self.top.geometry(f"{c['w']}x{self.bar_h() if c.get('folded') else c['h']}+{x}+{y}")

    def bar_h(self):
        return self.app.S(self.BAR)

    def is_inbox(self):
        return self.conf.get("date") == "inbox"

    def date(self):
        s = self.conf.get("date")
        return dt.date.fromisoformat(s) if s and s != "inbox" else dt.date.today()

    # ── 화면 ──
    def build(self):
        app, ui, c = self.app, self.ui, self.conf
        S = app.S
        top = self.top
        self._mode = None
        for w in top.winfo_children():
            w.destroy()
        base = c.get("color", COLORS[0][1])
        bar = self.bar_col = _mix(base, "#000000", 0.07)
        self.base = base
        top.configure(bg=_mix(base, "#000000", 0.16))          # 아주 얇은 가장자리
        self.frame = tk.Frame(top, bg=base)
        self.frame.pack(fill="both", expand=True, padx=1, pady=1)
        hd = self.hd = tk.Frame(self.frame, bg=bar, height=self.bar_h() - 2)
        hd.pack(side="top", fill="x")
        hd.pack_propagate(False)

        def pill(parent, text, cmd, side):
            p = ui.Pill(parent, text, cmd, app.f_small, fg=INK_SUB, bg=bar, hover=_mix(bar, "#000000", 0.14),
                        parent_bg=bar, padx=S(7), pady=S(2))
            p.pack(side=side, padx=(S(2), 0))
            return p
        pill(hd, "✕", self.close, "right")
        self.fold_btn = pill(hd, "▴", self.toggle_fold, "right")
        pill(hd, "●", self.menu, "right")
        self.pin_btn = pill(hd, "고정", self.toggle_top, "right")
        self.pin_set()
        pill(hd, "＋", lambda: self.mgr.add(inbox=self.is_inbox()), "left")
        self.prev_btn = pill(hd, "‹", lambda: self.go(-1), "left")
        self.title = tk.Label(hd, font=app.f_body, bg=bar, fg=INK, anchor="w", cursor="fleur")
        self.title.pack(side="left", padx=(S(8), S(2)))
        self.next_btn = pill(hd, "›", lambda: self.go(1), "left")
        # 제목 줄을 끌면 옮기고, 두 번 누르면 접는다
        for w in (hd, self.title):
            w.bind("<ButtonPress-1>", self._drag_start)
            w.bind("<B1-Motion>", self._drag_move)
            w.bind("<ButtonRelease-1>", self._drag_end)
            w.bind("<Double-Button-1>", lambda e: self.toggle_fold())
            w.bind("<Button-3>", lambda e: self.menu(e))

        self.body = tk.Frame(self.frame, bg=base)
        # 아래: 일정 추가 줄 → 메모 → 일정 목록(남는 자리)
        foot = self.foot = tk.Frame(self.body, bg=base)
        foot.pack(side="bottom", fill="x", padx=S(10), pady=(0, S(10)))
        field = _mix(base, "#ffffff", 0.55)
        self.entry = tk.Entry(foot, font=app.f_small, bg=field, fg=INK, insertbackground=INK, relief="flat",
                              highlightthickness=1, highlightbackground=_mix(base, "#000000", 0.1),
                              highlightcolor=_mix(base, "#000000", 0.35))
        self.entry.pack(fill="x", ipady=S(4))
        self.entry.bind("<Return>", lambda e: self.add_item())
        self.entry.bind("<Button-1>", lambda e: self.entry.focus_force())
        self.entry.bind("<Escape>", lambda e: self.frame.focus_set())
        self.ph = True
        self._placeholder()
        self.entry.bind("<FocusIn>", self._ph_off)
        self.entry.bind("<FocusOut>", lambda e: self._placeholder())
        mf = self.mf = tk.Frame(self.body, bg=base)
        mf.pack(side="bottom", fill="both", expand=True, padx=S(10), pady=(S(4), S(8)))
        bar_f = tk.Frame(mf, bg=base)
        bar_f.pack(fill="x", pady=(0, S(2)))
        self.memo_lab = tk.Label(bar_f, text="메모", font=app.f_tiny, bg=base, fg=INK_SUB, anchor="w")
        self.memo_lab.pack(side="left")
        tool_bg = _mix(base, "#000000", 0.07)

        def tb(text, cmd, fg=INK_SUB):
            p = ui.Pill(bar_f, text, cmd, app.f_tiny, fg=fg, bg=tool_bg, hover=_mix(tool_bg, "#000000", 0.14),
                        parent_bg=base, padx=S(6), pady=S(1))
            p.pack(side="right", padx=(S(2), 0))
            return p
        self.field = field
        self.memo = tk.Text(mf, height=3, font=app.f_small, bg=field, fg=INK, insertbackground=INK, relief="flat",
                            wrap="word", padx=S(6), pady=S(4), undo=True, highlightthickness=1,
                            highlightbackground=_mix(base, "#000000", 0.1),
                            highlightcolor=_mix(base, "#000000", 0.35))
        self.fmt = MemoFormat(self.memo, app, self._memo_changed)
        self.add_btn = tb("＋일정", self.open_entry)
        tb("☑", lambda: self.fmt.prefix("check"))
        tb("1.", lambda: self.fmt.prefix("num"))
        tb("•", lambda: self.fmt.prefix("bul"))
        tb("색", self._color_menu)
        tb("취소", lambda: self.fmt.toggle("strike"))
        tb("B", lambda: self.fmt.toggle("bold"), fg=INK)
        self.memo.pack(fill="both", expand=True)
        self.memo.bind("<KeyRelease>", self.fmt.on_key)
        self.memo.bind("<Button-1>", lambda e: self.memo.focus_force(), add="+")
        self.memo.bind("<FocusOut>", lambda e: self._save_memo())
        wrap = self.wrap = tk.Frame(self.body, bg=base)
        wrap.pack(side="top", fill="x", padx=S(10), pady=(S(8), 0))
        self.cv = tk.Canvas(wrap, bg=base, highlightthickness=0, bd=0, height=S(60))
        self.cv.pack(fill="x")
        self.list = tk.Frame(self.cv, bg=base)
        win = self.cv.create_window((0, 0), window=self.list, anchor="nw")
        self.list.bind("<Configure>", self._list_size)
        self.cv.bind("<Configure>", lambda e: self.cv.itemconfigure(win, width=e.width))
        self.cv.bind("<MouseWheel>", lambda e: self.cv.yview_scroll(int(-e.delta / 120), "units"))
        # 오른쪽 아래 손잡이: 크기 조절
        self.grip = tk.Canvas(self.body, width=S(16), height=S(16), bg=base, highlightthickness=0, cursor="size_nw_se" if ui.IS_WIN else "bottom_right_corner")
        for k in (4, 8, 12):
            self.grip.create_line(S(k) + S(3), S(15), S(15), S(k) + S(3), fill=INK_DIM)
        self.grip.place(relx=1.0, rely=1.0, anchor="se")
        self.grip.bind("<ButtonPress-1>", self._rs_start)
        self.grip.bind("<B1-Motion>", self._rs_move)
        self.grip.bind("<ButtonRelease-1>", self._rs_end)
        if not self.conf.get("folded"):
            self.body.pack(side="top", fill="both", expand=True)
        self.fold_btn_set()

    def _list_size(self, e=None):
        """일정 목록은 내용만큼(최대 약 120px)만 차지하고, 남는 자리는 메모가 쓴다"""
        self.cv.configure(scrollregion=self.cv.bbox("all"))
        want = self.list.winfo_reqheight()
        self.cv.configure(height=max(self.app.S(24), min(want, self.app.S(120))))

    def _color_menu(self):
        ui = self.ui
        m = tk.Menu(self.top, tearoff=0, bg=ui.CARD, fg=ui.TEXT, activebackground=ui.ACCENT,
                    activeforeground="#ffffff", bd=0)
        for name, col in MEMO_COLORS:
            m.add_command(label="● " + name, foreground=col, command=lambda n=name: self.fmt.color(n))
        m.add_separator()
        m.add_command(label="색 지우기", command=lambda: self.fmt.color(None))
        m.tk_popup(self.top.winfo_pointerx(), self.top.winfo_pointery())

    def _placeholder(self):
        if not self.entry.get():
            self.ph = True
            self.entry.config(fg=INK_DIM)
            self.entry.insert(0, "＋ 일정 추가 (Enter)  예) 14:00 회의")

    def _ph_off(self, e=None):
        if self.ph:
            self.entry.delete(0, "end")
            self.entry.config(fg=INK)
            self.ph = False

    # ── 내용 ──
    def refresh(self, force=False):
        app, d = self.app, self.date()
        inbox = self.is_inbox()
        if inbox:
            rows = [(None, t, None) for t in app._inbox().get("todos", [])]
            memo = app._inbox().get("memo", "")
        else:
            rows = core.day_items(app.data, d)
            rows.sort(key=lambda r: self.ui.sort_key(r[1]))
            memo = app._entry(d).get("memo", "")
        sig = (inbox, d, self.show_entry, memo, tuple((t.get("text"), t.get("time"), bool(t.get("done")), t.get("note"), self.ui.pri_of(t))
                                     for _, t, _ in rows))
        if sig == self.sig and not force:
            return
        self.sig = sig
        self.layout(not rows and not (None if inbox else self.ui.holiday_name(d)) and not self.show_entry)
        hol = None if inbox else self.ui.holiday_name(d)
        for b, ch in ((self.prev_btn, ""if inbox else "‹"), (self.next_btn, "" if inbox else "›")):
            b.itemconfig(b._label, text=ch)
        if inbox:
            left = "보관함 · 날짜 없음"
        else:
            left = f"{d.month}/{d.day} ({WD[d.weekday()]})"
            if d == dt.date.today() and not self.conf.get("date"):
                left = "오늘 " + left
        n_open = sum(1 for _, t, _ in rows if not t.get("done"))
        if self.conf.get("folded"):
            left += f" · 일정 {n_open}" + (" · 메모" if memo.strip() else "")
        self.title.config(text=left, fg="#c8374a" if (hol or (not inbox and d.weekday() == 6)) else INK)
        # 메모: 지금 입력 중이면 건드리지 않는다
        if self.top.focus_get() is not self.memo and self.fmt.get_markup() != memo:
            self.fmt.set_markup(memo)
            self.memo.edit_reset()
        for w in self.list.winfo_children():
            w.destroy()
        S = app.S
        base = self.base
        if hol:
            tk.Label(self.list, text=hol, font=app.f_tiny, bg=base, fg="#c8374a", anchor="w").pack(fill="x")
        if not rows:
            tk.Label(self.list, text="보관함이 비었어요" if inbox else "이 날은 일정이 없어요", font=app.f_small, bg=base, fg=INK_DIM,
                     anchor="w").pack(fill="x", pady=S(6))
        scroll = lambda e: self.cv.yview_scroll(int(-e.delta / 120), "units")
        for lst, t, s0 in rows:
            self._row(t, scroll)
        self.list.bind("<MouseWheel>", scroll)

    def layout(self, postit):
        """일정이 없으면 포스트잇처럼 메모만 한 장으로, 일정이 있으면 목록 + 메모 + 입력줄"""
        mode = "postit" if postit else "full"
        if mode == self._mode:
            return
        self._mode = mode
        base = self.base
        for w in (self.foot, self.mf, self.wrap):
            w.pack_forget()
        if postit:
            self.mf.pack(side="top", fill="both", expand=True, padx=S_(self, 10), pady=(S_(self, 2), S_(self, 10)))
            self.memo_lab.pack_forget()
            self.memo.config(bg=base, highlightthickness=0, padx=S_(self, 2))
        else:
            self.foot.pack(side="bottom", fill="x", padx=S_(self, 10), pady=(0, S_(self, 10)))
            self.mf.pack(side="bottom", fill="both", expand=True, padx=S_(self, 10), pady=(S_(self, 4), S_(self, 8)))
            self.wrap.pack(side="top", fill="x", padx=S_(self, 10), pady=(S_(self, 8), 0))
            self.memo_lab.pack(side="left")
            self.memo.config(bg=self.field, highlightthickness=1, padx=S_(self, 6))

    def open_entry(self):
        """포스트잇 모양에서 일정을 넣고 싶을 때: 입력줄을 연다"""
        self.show_entry = True
        self.refresh(force=True)
        self.entry.focus_force()

    def _row(self, t, scroll):
        app, base = self.app, self.base
        S = app.S
        done = bool(t.get("done"))
        col = PRI_INK[self.ui.pri_of(t)]
        r = tk.Frame(self.list, bg=base)
        r.pack(fill="x", pady=S(2))
        n = S(18)
        chk = tk.Canvas(r, width=n, height=n, bg=base, highlightthickness=0, cursor="hand2")
        if done:
            chk.create_oval(2, 2, n - 2, n - 2, fill=INK_DIM, outline="")
            chk.create_line(n * .30, n * .52, n * .45, n * .67, n * .72, n * .36, fill="#ffffff", width=S(2))
        else:
            chk.create_oval(2, 2, n - 2, n - 2, outline=col, width=S(2))
        chk.pack(side="left", anchor="n", pady=S(1))
        chk.bind("<Button-1>", lambda e, t=t: app.toggle_todo(t))
        txt = (t["time"] + "  " if t.get("time") else "") + t.get("text", "")
        lb = tk.Label(r, text=txt, font=self.fmt._fonts[2] if done else app.f_small, bg=base, fg=INK_DIM if done else INK,
                      anchor="w", justify="left", wraplength=max(S(120), self.conf["w"] - S(70)))
        lb.pack(side="left", fill="x", expand=True, padx=(S(8), 0))
        lb.bind("<Double-Button-1>", lambda e, t=t: app.edit_item(t))
        for w in (r, chk, lb):
            w.bind("<MouseWheel>", scroll)

    # ── 입력 ──
    def _memo_changed(self):
        if self._memo_job:
            self.top.after_cancel(self._memo_job)
        self._memo_job = self.top.after(600, self._save_memo)

    def _save_memo(self):
        if self._memo_job:
            try:
                self.top.after_cancel(self._memo_job)
            except tk.TclError:
                pass
            self._memo_job = None
        app, d = self.app, self.date()
        try:
            text = self.fmt.get_markup()
        except tk.TclError:
            return
        if self.is_inbox():
            if app._inbox().get("memo", "") != text:
                app._inbox(create=True)["memo"] = text
                app._commit()
            return
        if app._entry(d).get("memo", "") != text:
            e = app._entry(d, create=True)
            e["memo"], e["memo_ts"] = text, core.now_ms()
            app._commit()
            app.draw_calendar()

    def add_item(self):
        if self.ph:
            return
        text = self.entry.get().strip()
        if not text:
            return
        ui = self.ui
        item = core.new_item(text, pri=ui.DEFAULT_PRI)
        m = ui.TIME_RE.match(text)
        if m and int(m.group(1)) < 24 and int(m.group(2)) < 60:
            item.update(text=m.group(3).strip(), time=f"{int(m.group(1)):02d}:{m.group(2)}")
        core.add_item(self.app.data, "_inbox" if self.is_inbox() else self.date().isoformat(), item)
        self.entry.delete(0, "end")
        self.app._after_todo_change()

    def go(self, delta):
        if self.is_inbox():
            return
        self._save_memo()
        d = self.date() + dt.timedelta(days=delta)
        self.conf["date"] = None if d == dt.date.today() else d.isoformat()
        self.refresh(force=True)
        self.mgr.save()

    # ── 접기·펴기 ──
    def fold_btn_set(self):
        self.fold_btn.itemconfig(self.fold_btn._label, text="▾" if self.conf.get("folded") else "▴")

    def toggle_fold(self):
        c = self.conf
        self._save_memo()
        c["folded"] = not c.get("folded")
        self.fold_btn_set()
        self.refresh(force=True)
        cur = self.top.winfo_height()
        goal = self.bar_h() if c["folded"] else c["h"]
        if c["folded"]:
            self.body.pack_forget()
        self._animate(cur, goal)
        self.mgr.save()

    def _animate(self, cur, goal, i=0, steps=7):
        if self._anim:
            self.top.after_cancel(self._anim)
        e = 1 - (1 - (i + 1) / steps) ** 2             # 끝에서 부드럽게 멈춤
        h = round(cur + (goal - cur) * e)
        self.top.geometry(f"{self.conf['w']}x{h}+{self.conf['x']}+{self.conf['y']}")
        if i + 1 < steps:
            self._anim = self.top.after(14, lambda: self._animate(cur, goal, i + 1, steps))
        else:
            self._anim = None
            if not self.conf["folded"]:
                self.body.pack(side="top", fill="both", expand=True)

    # ── 끌어서 옮기기 / 크기 조절 ──
    def _drag_start(self, e):
        self._d0 = (e.x_root - self.top.winfo_x(), e.y_root - self.top.winfo_y())
        self._dragging = True
        self.top.lift()

    def _drag_move(self, e):
        x, y = self._snap(e.x_root - self._d0[0], e.y_root - self._d0[1])
        self.top.geometry(f"+{x}+{y}")

    def _snap(self, x, y):
        """화면 가장자리·다른 스티커 가장자리에 가까우면 달라붙는다 (Shift 를 누르면 자유롭게)"""
        S = self.app.S
        near = S(10)
        w, h = self.top.winfo_width(), self.top.winfo_height()
        ax, ay, aw, ah = self.ui.work_area(self.app.root, x + w // 2, y + h // 2)
        xs, ys = [ax, ax + aw - w], [ay, ay + ah - h]
        for n in self.mgr.notes.values():
            if n is self or n.top.state() == "withdrawn":
                continue
            ox, oy, ow, oh = n.top.winfo_x(), n.top.winfo_y(), n.top.winfo_width(), n.top.winfo_height()
            if y < oy + oh + near and oy < y + h + near:         # 세로로 겹치는 이웃: 좌우 붙이기
                xs += [ox + ow, ox - w, ox]
            if x < ox + ow + near and ox < x + w + near:         # 가로로 겹치는 이웃: 위아래 붙이기
                ys += [oy + oh, oy - h, oy]
        sx = min(xs, key=lambda v: abs(v - x))
        sy = min(ys, key=lambda v: abs(v - y))
        return (sx if abs(sx - x) <= near else x), (sy if abs(sy - y) <= near else y)

    def _drag_end(self, e):
        self._dragging = False
        self.conf["x"], self.conf["y"] = self.top.winfo_x(), self.top.winfo_y()
        self.mgr.save()

    def _rs_start(self, e):
        self._r0 = (e.x_root, e.y_root, self.top.winfo_width(), self.top.winfo_height())

    def _rs_move(self, e):
        S = self.app.S
        x0, y0, w0, h0 = self._r0
        w = max(S(250), w0 + e.x_root - x0)
        h = max(S(180), h0 + e.y_root - y0)
        self.conf["w"], self.conf["h"] = w, h
        self.top.geometry(f"{w}x{h}")

    def _rs_end(self, e):
        self.refresh(force=True)                 # 글자 줄바꿈 폭을 새 너비에 맞춘다
        self.mgr.save()

    # ── 메뉴 / 닫기 ──
    def menu(self, e=None):
        app, ui = self.app, self.ui
        m = tk.Menu(self.top, tearoff=0, bg=ui.CARD, fg=ui.TEXT, activebackground=ui.ACCENT,
                    activeforeground="#ffffff", bd=0, relief="flat")
        for name, col in COLORS:
            m.add_command(label=("● " if col == self.conf.get("color") else "○ ") + name,
                          command=lambda col=col: self.set_color(col))
        m.add_separator()
        top_var = tk.BooleanVar(value=bool(self.conf.get("top")))
        m.add_checkbutton(label="항상 위에 두기", variable=top_var, command=lambda: self.set_top(top_var.get()))
        am = tk.Menu(m, tearoff=0, bg=ui.CARD, fg=ui.TEXT, activebackground=ui.ACCENT, activeforeground="#ffffff", bd=0)
        for a in (1.0, 0.85, 0.7, 0.5):
            am.add_command(label=("● " if abs(float(self.conf.get("alpha", 1.0)) - a) < .01 else "○ ") + f"투명도 {int(a * 100)}%",
                           command=lambda a=a: self.set_alpha(a))
        m.add_cascade(label="투명도", menu=am)
        m.add_command(label="모든 스티커 숨기기 / 보이기", command=self.mgr.toggle_hidden)
        if self.is_inbox():
            m.add_command(label="날짜 스티커로 바꾸기 (오늘)", command=lambda: self.set_date(None))
        else:
            m.add_command(label="오늘로", command=lambda: self.set_date(None))
            m.add_command(label="날짜 없는 칸(보관함)으로 바꾸기", command=lambda: self.set_date("inbox"))
        m.add_command(label="새 스티커 (오늘)", command=self.mgr.add)
        m.add_command(label="새 스티커 (날짜 없는 칸)", command=lambda: self.mgr.add(inbox=True))
        m.add_separator()
        m.add_command(label="이 스티커 닫기", command=self.close)
        x, y = (e.x_root, e.y_root) if e is not None else (self.top.winfo_pointerx(), self.top.winfo_pointery())
        m.tk_popup(x, y)

    def set_date(self, val):
        self._save_memo()
        self.conf["date"] = val
        self.refresh(force=True)
        self.mgr.save()

    def set_color(self, col):
        self._save_memo()
        self.conf["color"] = col
        self.build()
        self.refresh(force=True)
        self.mgr.save()

    def set_top(self, on):
        self.conf["top"] = bool(on)
        self.top.attributes("-topmost", bool(on))
        self.pin_set()
        self.mgr.save()

    def toggle_top(self):
        self.set_top(not self.conf.get("top"))

    def pin_set(self):
        on = bool(self.conf.get("top"))
        self.pin_btn.itemconfig(self.pin_btn._label, fill="#ffffff" if on else INK_SUB)
        self.pin_btn.itemconfig(self.pin_btn._shape, fill="#4a4b5a" if on else self.bar_col)
        self.pin_btn._bg = "#4a4b5a" if on else self.bar_col

    def set_alpha(self, a):
        self.conf["alpha"] = a
        self.top.attributes("-alpha", a)
        self.mgr.save()

    def close(self):
        self._save_memo()
        self.mgr.remove(self)
        self.top.destroy()
