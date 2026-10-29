using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 바탕화면 시계 위젯 (공항 출발 안내판 스타일): 글자가 착착 넘어가며 바뀌어요.
// 끌어서 옮기고, 우클릭으로 '항상 맨 위' / '바탕화면에 붙이기(다른 창 뒤)'를 고를 수 있어요.
internal sealed class ClockWidget : Form
{
	private static ClockWidget cur;
	private readonly System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
	private readonly FrameTimer ft = new FrameTimer();
	private Point downAt; private bool dragging;

	internal static bool IsOn { get { return cur != null && !cur.IsDisposed; } }
	internal static void Toggle(bool on)
	{
		try
		{
			if (on && !IsOn) { cur = new ClockWidget(); cur.Show(); cur.ApplyZ(); }
			else if (!on && IsOn) { cur.Close(); cur = null; }
		}
		catch (Exception ex) { Program.LogErr(ex); cur = null; }
	}

	// ── 넘어가는 글자 한 칸 ──
	private sealed class Cell
	{
		public RectangleF R; public string Cur = " ", Old = " "; public float T = 1f; public bool Small, Narrow;
		public bool Flipping { get { return T < 1f; } }
	}
	private readonly List<Cell> cells = new List<Cell>();
	private Cell[] clock = new Cell[8], date = new Cell[11], val = new Cell[8];
	private string cap = "";
	private static readonly Color Board = Color.FromArgb(18, 20, 24), TileTop = Color.FromArgb(44, 47, 54), TileBot = Color.FromArgb(30, 32, 38), Amber = Color.FromArgb(255, 205, 80), Dim = Color.FromArgb(150, 255, 205, 80);

	private Cell Make(float x, float y, float w, float h, bool small)
	{
		Cell c = new Cell { R = new RectangleF(Dpi.Zf(x), Dpi.Zf(y), Dpi.Zf(w), Dpi.Zf(h)), Small = small }; cells.Add(c); return c;
	}

	private ClockWidget()
	{
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = Program.widgetTop; StartPosition = FormStartPosition.Manual;
		BackColor = Board; ClientSize = new Size(Dpi.Z(252), Dpi.Z(150)); DoubleBuffered = true;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
		// 시계 8칸: 시 시 : 분 분 : 초 초
		float x = 16;
		for (int i = 0; i < 8; i++)
		{
			bool colon = i == 2 || i == 5; float w = colon ? 14 : 27;
			clock[i] = Make(x, 34, w, 44, false); clock[i].Narrow = colon; x += w + 3;
		}
		for (int i = 0; i < 11; i++) date[i] = Make(16 + i * 16.5f, 86, 15, 22, true);
		for (int i = 0; i < 8; i++) val[i] = Make(252 - 16 - (8 - i) * 16.5f + 1.5f, 116, 15, 22, true);
		Rectangle wa = Screen.PrimaryScreen.WorkingArea; int px = Program.widgetX, py = Program.widgetY;
		if (px < -9000 || py < -9000) { px = wa.Right - Width - Dpi.Z(30); py = wa.Top + Dpi.Z(60); }
		// 저장된 자리가 지금 모니터 안에 없으면(모니터를 뺐거나 해상도가 바뀐 경우) 기본 자리로, 있으면 그 모니터 안으로 맞춰요
		Rectangle area = Rectangle.Empty;
		foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.Contains(new Point(px + Width / 2, py + Height / 2))) area = sc.WorkingArea;
		if (area.IsEmpty) { area = wa; px = wa.Right - Width - Dpi.Z(30); py = wa.Top + Dpi.Z(60); }
		px = Math.Max(area.Left, Math.Min(area.Right - Width, px)); py = Math.Max(area.Top, Math.Min(area.Bottom - Height, py)); Location = new Point(px, py);
		Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
		Cursor = Cursors.SizeAll;
		ContextMenu = BuildMenu();
		MouseDown += delegate(object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; downAt = e.Location; } };
		MouseMove += delegate(object o, MouseEventArgs e) { if (dragging) Location = new Point(Left + e.X - downAt.X, Top + e.Y - downAt.Y); };
		MouseUp += delegate(object o, MouseEventArgs e) { if (dragging) { dragging = false; Program.widgetX = Left; Program.widgetY = Top; Program.SaveSettingsPublic(); ApplyZ(); } };
		MouseDoubleClick += delegate { try { Program.OpenSettingsPublic(); } catch { } };
		Refresh2(true);
		tm.Interval = 100; tm.Tick += delegate { Refresh2(false); }; tm.Start();
		ft.Tick += delegate { StepFlip(); };
		FormClosed += delegate { tm.Stop(); tm.Dispose(); ft.Stop(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged; if (cur == this) cur = null; };
	}

	private void OnDisplayChanged(object o, EventArgs e)
	{
		try
		{
			BeginInvoke((MethodInvoker)delegate
			{
				Rectangle area = Rectangle.Empty;
				foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(Bounds)) area = sc.WorkingArea;
				if (area.IsEmpty) area = Screen.PrimaryScreen.WorkingArea;
				Location = new Point(Math.Max(area.Left, Math.Min(area.Right - Width, Left)), Math.Max(area.Top, Math.Min(area.Bottom - Height, Top)));
				ApplyZ();
			});
		}
		catch { }
	}

	private ContextMenu BuildMenu()
	{
		ContextMenu m = new ContextMenu();
		MenuItem top = new MenuItem("항상 맨 위에 두기"), bot = new MenuItem("바탕화면에 붙이기 (다른 창 뒤)"), close = new MenuItem("시계 끄기");
		top.Checked = Program.widgetTop; bot.Checked = !Program.widgetTop;
		top.Click += delegate { Program.widgetTop = true; TopMost = true; Program.SaveSettingsPublic(); top.Checked = true; bot.Checked = false; ApplyZ(); };
		bot.Click += delegate { Program.widgetTop = false; TopMost = false; Program.SaveSettingsPublic(); top.Checked = false; bot.Checked = true; ApplyZ(); };
		close.Click += delegate { Program.widgetOn = false; Program.SaveSettingsPublic(); Toggle(false); };
		m.MenuItems.AddRange(new MenuItem[] { top, bot, new MenuItem("-"), close });
		return m;
	}

	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
	private void ApplyZ()
	{
		try { if (IsHandleCreated) SetWindowPos(Handle, Program.widgetTop ? new IntPtr(-1) : new IntPtr(1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); } catch { }   // NOSIZE | NOMOVE | NOACTIVATE
	}
	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000; return cp; } }   // Alt+Tab 목록에 안 나오고, 눌러도 다른 창의 포커스를 뺏지 않아요
	protected override void WndProc(ref Message m)
	{
		// 바탕화면 모드: 어떤 이유로든 앞으로 올라오려 하면 다시 맨 뒤로 보내요
		if (m.Msg == 0x46 && !Program.widgetTop)
		{
			try { int flags = System.Runtime.InteropServices.Marshal.ReadInt32(m.LParam, IntPtr.Size * 2 + 16); if ((flags & 0x0004) == 0) { System.Runtime.InteropServices.Marshal.WriteIntPtr(m.LParam, IntPtr.Size, new IntPtr(1)); } } catch { }
		}
		base.WndProc(ref m);
	}
	[System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
	}

	// ── 글자 바꾸기 ──
	private void Set(Cell c, string s, bool animate)
	{
		if (c.Cur == s) return;
		if (animate) { c.Old = c.Cur; c.T = 0f; if (!ft.Enabled) { last = Environment.TickCount; ft.Start(); } } else { c.Old = s; c.T = 1f; }
		c.Cur = s;
	}
	private static string Fit(string s, int n) { if (s.Length > n) s = s.Substring(0, n); return s; }
	private void SetRow(Cell[] row, string s, bool right, bool animate)
	{
		s = Fit(s, row.Length); int pad = row.Length - s.Length; if (!right) pad = 0;
		for (int i = 0; i < row.Length; i++) { int k = i - pad; Set(row[i], k >= 0 && k < s.Length ? s.Substring(k, 1) : " ", animate); }
	}
	private void Refresh2(bool first)
	{
		DateTime n = DateTime.Now; bool an = !first;
		string t = n.ToString("HH:mm:ss"); for (int i = 0; i < 8; i++) Set(clock[i], t.Substring(i, 1), an);
		SetRow(date, n.Month + "월 " + n.Day + "일 " + Records.Dow[(int)n.DayOfWeek] + "요일", false, an);
		string c; string v = Program.CountdownText(n, out c);
		if (v.IndexOf(':') < 0) v = v.IndexOf("쉬는") >= 0 ? "쉬는 날" : v;
		if (c.Length > 8) c = "점심 중";
		if (c != cap) { cap = c; Invalidate(new Rectangle(0, Dpi.Z(112), Dpi.Z(110), Dpi.Z(30))); }
		SetRow(val, v, true, an);
	}
	private int last;
	private void StepFlip()
	{
		int now = Environment.TickCount;   // 오락실이 열려 있으면 초당 30번만 (게임과 CPU 를 나눠 써요)
		float dt = Math.Min(0.05f, (now - last) / 1000f); last = now; bool any = false;
		foreach (Cell c in cells)
		{
			if (!c.Flipping) continue;
			c.T = Math.Min(1f, c.T + dt / 0.2f); any = true;
			Invalidate(Rectangle.Ceiling(new RectangleF(c.R.X - 1, c.R.Y - 1, c.R.Width + 2, c.R.Height + 2)));
		}
		if (!any) ft.Stop();
	}

	// ── 그리기 ──
	private static readonly Dictionary<string, Bitmap> halfCache = new Dictionary<string, Bitmap>();
	private Bitmap HalfBmp(Cell c, string s, bool top)
	{
		string key = s + "|" + (top ? 1 : 0) + "|" + (c.Small ? 1 : 0) + "|" + (int)c.R.Width + "x" + (int)c.R.Height;
		Bitmap b;
		if (halfCache.TryGetValue(key, out b)) return b;
		if (halfCache.Count > 400) { foreach (Bitmap o in halfCache.Values) o.Dispose(); halfCache.Clear(); }
		b = new Bitmap((int)Math.Ceiling(c.R.Width) + 2, (int)Math.Ceiling(c.R.Height) + 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(b))
		{
			g.Clear(Color.Transparent); Gfx.HQ(g);
			Cell t = new Cell { R = new RectangleF(1f, 1f, c.R.Width, c.R.Height), Small = c.Small };
			DrawHalfRaw(g, t, s, top, 1f);
		}
		halfCache[key] = b; return b;
	}
	private void DrawHalf(Graphics g, Cell c, string s, bool top, float scaleY)
	{
		try
		{
			Bitmap b = HalfBmp(c, s, top); RectangleF r = c.R; float mid = r.Y + r.Height / 2f;
			GraphicsState gs = g.Save();
			g.TranslateTransform(0, mid); g.ScaleTransform(1f, Math.Max(0.001f, scaleY)); g.TranslateTransform(0, -mid);
			g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
			g.DrawImage(b, (float)Math.Round(r.X) - 1f, (float)Math.Round(r.Y) - 1f, b.Width, b.Height);
			g.Restore(gs);
		}
		catch { DrawHalfRaw(g, c, s, top, scaleY); }
	}
	private void DrawHalfRaw(Graphics g, Cell c, string s, bool top, float scaleY)
	{
		RectangleF r = c.R; float mid = r.Y + r.Height / 2f;
		RectangleF half = top ? new RectangleF(r.X, r.Y, r.Width, r.Height / 2f) : new RectangleF(r.X, mid, r.Width, r.Height / 2f);
		GraphicsState gs = g.Save();
		g.TranslateTransform(0, mid); g.ScaleTransform(1f, Math.Max(0.001f, scaleY)); g.TranslateTransform(0, -mid);
		float rad = Dpi.Zf(c.Small ? 3.5f : 5f);
		using (GraphicsPath gp = Gfx.Round(r, rad))
		{
			g.SetClip(half, CombineMode.Intersect); g.SetClip(gp, CombineMode.Intersect);
			using (LinearGradientBrush lb = new LinearGradientBrush(r, top ? Color.FromArgb(52, 55, 63) : Color.FromArgb(34, 36, 42), top ? Color.FromArgb(40, 43, 49) : Color.FromArgb(26, 28, 33), 90f)) g.FillRectangle(lb, r);
			if (s != " ") Gfx.Text(g, s, Fonts.Num(c.Small ? 11.5f : 24f, 2), Amber, r, StringAlignment.Center, StringAlignment.Center, false);
		}
		g.Restore(gs);
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Rectangle clip = e.ClipRectangle;
		RectangleF full = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		g.Clear(Board);
		using (GraphicsPath gp = Gfx.Round(full, Dpi.Zf(16))) using (Pen p = new Pen(Color.FromArgb(70, 255, 255, 255), 1f)) g.DrawPath(p, gp);
		// 머리글
		Gfx.Text(g, "마룡 AIR  ·  초과근무", Theme.F(8.5f, 2), Amber, new RectangleF(Dpi.Zf(16), Dpi.Zf(10), Dpi.Zf(150), Dpi.Zf(18)), StringAlignment.Near, StringAlignment.Center, false);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(86, 220, 120))) g.FillEllipse(b, Width - Dpi.Zf(76), Dpi.Zf(15), Dpi.Zf(7), Dpi.Zf(7));
		Gfx.Text(g, "ON TIME", Theme.F(8f, 2), Color.FromArgb(200, 255, 255, 255), new RectangleF(Width - Dpi.Zf(66), Dpi.Zf(10), Dpi.Zf(56), Dpi.Zf(18)), StringAlignment.Near, StringAlignment.Center, false);
		Gfx.Text(g, cap, Theme.F(9f, 1), Dim, new RectangleF(Dpi.Zf(16), Dpi.Zf(116), Dpi.Zf(110), Dpi.Zf(22)), StringAlignment.Near, StringAlignment.Center, false);
		foreach (Cell c in cells)
		{
			if (!clip.IntersectsWith(Rectangle.Ceiling(new RectangleF(c.R.X - 1, c.R.Y - 1, c.R.Width + 2, c.R.Height + 2)))) continue;
			if (c.Narrow && c.Cur == ":" && !c.Flipping)
			{
				Gfx.Text(g, ":", Fonts.Num(24f, 2), Amber, c.R, StringAlignment.Center, StringAlignment.Center, false); continue;
			}
			if (c.Cur == " " && !c.Flipping) continue;   // 빈 칸은 그리지 않아요
			if (!c.Flipping) { DrawHalf(g, c, c.Cur, true, 1f); DrawHalf(g, c, c.Cur, false, 1f); }
			else
			{
				DrawHalf(g, c, c.Cur, true, 1f);      // 새 글자 위쪽은 뒤에서 드러나고
				DrawHalf(g, c, c.Old, false, 1f);     // 옛 글자 아래쪽은 그대로
				if (c.T < 0.5f) DrawHalf(g, c, c.Old, true, 1f - c.T * 2f);   // 옛 글자 위쪽이 아래로 접히고
				else DrawHalf(g, c, c.Cur, false, (c.T - 0.5f) * 2f);          // 새 글자 아래쪽이 펼쳐져요
			}
			float my = c.R.Y + c.R.Height / 2f;
			using (Pen sp = new Pen(Color.FromArgb(200, 8, 9, 11), Math.Max(1f, Dpi.Zf(1.2f)))) g.DrawLine(sp, c.R.X, my, c.R.Right, my);   // 가운데 틈
		}
	}
}
