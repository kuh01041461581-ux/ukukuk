"""바탕화면 스티커: 날짜 하나의 일정과 메모를 한 장에 보여 주는 작은 창.
끌어서 옮기고, 접고 펴고, 크기·색을 바꿀 수 있다. 메모는 달력의 그날 메모와 같은 것이라 어디서 고쳐도 같이 바뀐다.
스티커 위치·크기·색은 데이터(폰과 동기화되는 일정)가 아니라 이 PC의 설정(conf["stickies"])에만 저장한다."""
import datetime as dt
import tkinter as tk

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


class StickyManager:
    def __init__(self, app, ui):
        self.app, self.ui = app, ui
        self.notes = {}
        self.hidden = bool(app.conf.get("stickies_hidden"))
        self._tick()

    def restore(self):
        for c in list(self.app.conf.get("stickies", [])):
            self._open(c)

    def add(self, d=None):
        """새 스티커. d 가 없으면 '오늘'을 따라가는 스티커"""
        app = self.app
        S = app.S
        n = len(self.notes)
        x0, y0, w0, _ = self.ui.work_area(app.root)
        conf = {"id": max([int(k) for k in self.notes] + [0]) + 1,
                "date": d.isoformat() if d else None,
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
        self.notes.pop(str(note.conf["id"]), None)
        self.save()

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
            if not self.mgr.hidden:
                self.top.withdraw()
                self.top.deiconify()
        except Exception:
            pass

    # ── 위치·크기 ──
    def _place(self):
        c, S = self.conf, self.app.S
        vx, vy, vw, vh = self.ui.virtual_screen(self.app.root)
        w = max(S(230), int(c.get("w", S(280))))
        h = int(c.get("h", S(340)))
        x = min(max(vx, int(c.get("x", vx + 40))), vx + vw - S(120))
        y = min(max(vy, int(c.get("y", vy + 60))), vy + vh - S(60))
        c["x"], c["y"], c["w"], c["h"] = x, y, w, max(S(180), h)
        self.top.geometry(f"{w}x{self.bar_h() if c.get('folded') else c['h']}+{x}+{y}")

    def bar_h(self):
        return self.app.S(self.BAR)

    def date(self):
        s = self.conf.get("date")
        return dt.date.fromisoformat(s) if s else dt.date.today()

    # ── 화면 ──
    def build(self):
        app, ui, c = self.app, self.ui, self.conf
        S = app.S
        top = self.top
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
        pill(hd, "‹", lambda: self.go(-1), "left")
        self.title = tk.Label(hd, font=app.f_body, bg=bar, fg=INK, anchor="w", cursor="fleur")
        self.title.pack(side="left", padx=(S(8), S(2)))
        pill(hd, "›", lambda: self.go(1), "left")
        # 제목 줄을 끌면 옮기고, 두 번 누르면 접는다
        for w in (hd, self.title):
            w.bind("<ButtonPress-1>", self._drag_start)
            w.bind("<B1-Motion>", self._drag_move)
            w.bind("<ButtonRelease-1>", self._drag_end)
            w.bind("<Double-Button-1>", lambda e: self.toggle_fold())
            w.bind("<Button-3>", lambda e: self.menu(e))

        self.body = tk.Frame(self.frame, bg=base)
        # 아래: 일정 추가 줄 → 메모 → 일정 목록(남는 자리)
        foot = tk.Frame(self.body, bg=base)
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
        mf = tk.Frame(self.body, bg=base)
        mf.pack(side="bottom", fill="x", padx=S(10), pady=(S(4), S(8)))
        tk.Label(mf, text="메모", font=app.f_tiny, bg=base, fg=INK_SUB, anchor="w").pack(fill="x")
        self.memo = tk.Text(mf, height=4, font=app.f_small, bg=field, fg=INK, insertbackground=INK, relief="flat",
                            wrap="word", padx=S(6), pady=S(4), undo=True, highlightthickness=1,
                            highlightbackground=_mix(base, "#000000", 0.1),
                            highlightcolor=_mix(base, "#000000", 0.35))
        self.memo.pack(fill="x")
        self.memo.bind("<KeyRelease>", lambda e: self._memo_changed())
        self.memo.bind("<Button-1>", lambda e: self.memo.focus_force())
        self.memo.bind("<FocusOut>", lambda e: self._save_memo())
        wrap = tk.Frame(self.body, bg=base)
        wrap.pack(side="top", fill="both", expand=True, padx=S(10), pady=(S(8), 0))
        self.cv = tk.Canvas(wrap, bg=base, highlightthickness=0, bd=0)
        self.cv.pack(fill="both", expand=True)
        self.list = tk.Frame(self.cv, bg=base)
        win = self.cv.create_window((0, 0), window=self.list, anchor="nw")
        self.list.bind("<Configure>", lambda e: self.cv.configure(scrollregion=self.cv.bbox("all")))
        self.cv.bind("<Configure>", lambda e: self.cv.itemconfigure(win, width=e.width))
        self.cv.bind("<MouseWheel>", lambda e: self.cv.yview_scroll(int(-e.delta / 120), "units"))
        # 오른쪽 아래 손잡이: 크기 조절
        self.grip = tk.Canvas(self.body, width=S(16), height=S(16), bg=base, highlightthickness=0, cursor="size_nw_se")
        for k in (4, 8, 12):
            self.grip.create_line(S(k) + S(3), S(15), S(15), S(k) + S(3), fill=INK_DIM)
        self.grip.place(relx=1.0, rely=1.0, anchor="se")
        self.grip.bind("<ButtonPress-1>", self._rs_start)
        self.grip.bind("<B1-Motion>", self._rs_move)
        self.grip.bind("<ButtonRelease-1>", self._rs_end)
        if not self.conf.get("folded"):
            self.body.pack(side="top", fill="both", expand=True)
        self.fold_btn_set()

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
        rows = core.day_items(app.data, d)
        rows.sort(key=lambda r: self.ui.sort_key(r[1]))
        memo = app._entry(d).get("memo", "")
        sig = (d, memo, tuple((t.get("text"), t.get("time"), bool(t.get("done")), t.get("note"), self.ui.pri_of(t))
                              for _, t, _ in rows))
        if sig == self.sig and not force:
            return
        self.sig = sig
        hol = self.ui.holiday_name(d)
        left = f"{d.month}/{d.day} ({WD[d.weekday()]})"
        if d == dt.date.today() and not self.conf.get("date"):
            left = "오늘 " + left
        n_open = sum(1 for _, t, _ in rows if not t.get("done"))
        if self.conf.get("folded"):
            left += f" · 일정 {n_open}" + (" · 메모" if memo.strip() else "")
        self.title.config(text=left, fg="#c8374a" if (hol or d.weekday() == 6) else INK)
        # 메모: 지금 입력 중이면 건드리지 않는다
        if self.top.focus_get() is not self.memo and self.memo.get("1.0", "end-1c") != memo:
            self.memo.delete("1.0", "end")
            self.memo.insert("1.0", memo)
            self.memo.edit_reset()
        for w in self.list.winfo_children():
            w.destroy()
        S = app.S
        base = self.base
        if hol:
            tk.Label(self.list, text=hol, font=app.f_tiny, bg=base, fg="#c8374a", anchor="w").pack(fill="x")
        if not rows:
            tk.Label(self.list, text="이 날은 일정이 없어요", font=app.f_small, bg=base, fg=INK_DIM,
                     anchor="w").pack(fill="x", pady=S(6))
        scroll = lambda e: self.cv.yview_scroll(int(-e.delta / 120), "units")
        for lst, t, s0 in rows:
            self._row(t, scroll)
        self.list.bind("<MouseWheel>", scroll)

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
        lb = tk.Label(r, text=txt, font=app.f_done if done else app.f_small, bg=base, fg=INK_DIM if done else INK,
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
            text = self.memo.get("1.0", "end-1c")
        except tk.TclError:
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
        core.add_item(self.app.data, self.date().isoformat(), item)
        self.entry.delete(0, "end")
        self.app._after_todo_change()

    def go(self, delta):
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
        self.conf["x"], self.conf["y"] = self.top.winfo_x(), self.top.winfo_y()
        self.mgr.save()

    def _rs_start(self, e):
        self._r0 = (e.x_root, e.y_root, self.top.winfo_width(), self.top.winfo_height())

    def _rs_move(self, e):
        S = self.app.S
        x0, y0, w0, h0 = self._r0
        w = max(S(230), w0 + e.x_root - x0)
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
        m.add_command(label="오늘로", command=lambda: (self.conf.update(date=None), self.refresh(force=True), self.mgr.save()))
        m.add_command(label="새 스티커 (오늘)", command=self.mgr.add)
        m.add_separator()
        m.add_command(label="이 스티커 닫기", command=self.close)
        x, y = (e.x_root, e.y_root) if e is not None else (self.top.winfo_pointerx(), self.top.winfo_pointery())
        m.tk_popup(x, y)

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
