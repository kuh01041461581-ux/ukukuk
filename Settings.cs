﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal class SidePanel : Panel, IBackHost
{
	protected override void WndProc(ref Message m) { if (PaintProf.On && m.Msg == 0x0F) { long t0 = Stopwatch.GetTimestamp(); base.WndProc(ref m); PaintProf.Add(GetType().Name, t0); } else base.WndProc(ref m); }
	public RectangleF Pill = new RectangleF(0, -100, 10, 10);
	public SidePanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Theme.Side; }
	public Color BaseColor { get { return BackColor; } }
	private void DrawBack(Graphics g)
	{
		g.Clear(BackColor);
		int a = Theme.Dark ? 30 : 22;
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, -1, Math.Max(1, Width), Height + 2), Color.FromArgb(a, Theme.Accent), Color.FromArgb(0, Theme.Accent), 90f)) g.FillRectangle(lb, 0, 0, Width, Height);
		using (LinearGradientBrush lb2 = new LinearGradientBrush(new RectangleF(0, -1, Math.Max(1, Width), Height + 2), Color.FromArgb(0, Theme.Blue), Color.FromArgb(Theme.Dark ? 16 : 12, Theme.Blue), 90f)) g.FillRectangle(lb2, 0, Height / 2, Width, Height - Height / 2);
	}
	public void PaintBack(Graphics g, Control child) { GraphicsState s = g.Save(); g.TranslateTransform(-child.Left, -child.Top); DrawBack(g); g.Restore(s); }
	protected override void OnPaint(PaintEventArgs e) { DrawBack(e.Graphics); }
}

internal class FootPanel : WashPanel
{
	protected override void DrawBack(Graphics g)
	{
		base.DrawBack(g);
	}
}

internal class SettingsForm : Form
{
	[DllImport("user32.dll")] private static extern bool ReleaseCapture();
	[DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
	[DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams cp = base.CreateParams;
			cp.Style |= 0x20000 | 0x80000;     // 작업표시줄에서 최소화
			return cp;
		}
	}
	[DllImport("user32.dll", EntryPoint = "SetClassLongW")] private static extern uint SetClassLong32(IntPtr h, int i, IntPtr v);
	[DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")] private static extern IntPtr SetClassLongPtr64(IntPtr h, int i, IntPtr v);
	[DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(int rgb);
	private void NoEdge()
	{
		try
		{
			int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4);          // 윈도우 11 둥근 모서리
			int none = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(Handle, 34, ref none, 4);   // 시스템이 그리는 테두리선 없애기
		}
		catch { }
	}
	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		NoEdge();
		// 창이 처음 뜰 때 잠깐 보이는 기본 바탕(흰색)을 앱 바탕색으로
		try { IntPtr br = CreateSolidBrush(Theme.Page.R | (Theme.Page.G << 8) | (Theme.Page.B << 16)); if (IntPtr.Size == 8) SetClassLongPtr64(Handle, -10, br); else SetClassLong32(Handle, -10, br); } catch { }
	}
	protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) NoEdge(); }
	// 닫기(X)를 누르면 창을 없애지 않고 숨겨요 → 다시 열 때 바로 떠요 (앱이 끝나거나 색을 바꿀 때만 진짜로 닫아요)
	public static bool HideOnClose = true;
	// 숨은 명령: 위 위 아래 아래 좌 우 좌 우 → 폭죽이 터지고 마룡이 오락실이 열려요 (설정창을 보고 있을 때만 / 방향키 동작은 그대로 둬요)
	private static readonly Keys[] Secret = { Keys.Up, Keys.Up, Keys.Down, Keys.Down, Keys.Left, Keys.Right, Keys.Left, Keys.Right };
	private int secretPos; private long secretAt;
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		try
		{
			Keys k = keyData & Keys.KeyCode;
			if (k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right)
			{
				long now = System.Diagnostics.Stopwatch.GetTimestamp(); if (now - secretAt > System.Diagnostics.Stopwatch.Frequency * 25 / 10) secretPos = 0; secretAt = now;
				if (k == Secret[secretPos]) { secretPos++; if (secretPos >= Secret.Length) { secretPos = 0; Program.OpenArcadeWithFireworks(this); } }
				else secretPos = (k == Secret[0]) ? 1 : 0;
			}
			else if ((keyData & Keys.Modifiers) == Keys.None) secretPos = 0;
		}
		catch { }
		return base.ProcessCmdKey(ref msg, keyData);
	}
	protected override void OnFormClosing(FormClosingEventArgs e) { if (HideOnClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; } base.OnFormClosing(e); }
	// 창 크기 조절: 가장자리에 투명 컨트롤을 깔지 않고(그 자리가 흰 띠로 보였어요), 창 자체가 가장자리를 알아보게 해요
	private int EdgeHit(Point screen)
	{
		if (WindowState != FormWindowState.Normal) return 0;
		Point p = PointToClient(screen); int e = Dpi.Z(6), c = Dpi.Z(14), w = ClientSize.Width, h = ClientSize.Height;
		bool l = p.X < e, r = p.X >= w - e, t = p.Y < e, b = p.Y >= h - e;
		bool cl = p.X < c, cr = p.X >= w - c, ct = p.Y < c, cb = p.Y >= h - c;
		if ((l && ct) || (t && cl)) return 13; if ((r && ct) || (t && cr)) return 14;
		if ((l && cb) || (b && cl)) return 16; if ((r && cb) || (b && cr)) return 17;
		if (l) return 10; if (r) return 11; if (t) return 12; if (b) return 15;
		return 0;
	}
	internal bool InEdge(int lp) { return EdgeHit(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF))) != 0; }
	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 0x84)
		{
			int hit = EdgeHit(new Point((short)((long)m.LParam & 0xFFFF), (short)(((long)m.LParam >> 16) & 0xFFFF)));
			if (hit != 0) { m.Result = (IntPtr)hit; return; }
		}
		base.WndProc(ref m);
	}
	private readonly List<EdgeFilter> edgeF = new List<EdgeFilter>();
	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		try { foreach (Control c in Controls) { IntPtr h = c.Handle; EdgeFilter ef = new EdgeFilter(this); ef.AssignHandle(h); edgeF.Add(ef); } } catch { }
	}
	public void SetMaxBounds(Rectangle r) { MaximizedBounds = r; }
	public void Drag()
	{
		try { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } catch { }
	}
}

internal class ThemeDot : Ctl
{
	public ThemeDot() { Cursor = Cursors.Hand; Size = new Size(Dpi.Z(34), Dpi.Z(34)); }
	protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); Program.SetTheme((Theme.Mode + 1) % 3); Invalidate(); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(BackColor);
		float cx = Width / 2f, cy = Height / 2f, r = Dpi.Zf(7f);
		using (Pen p = new Pen(Theme.Sub, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		using (SolidBrush b = new SolidBrush(Theme.Sub))
		{
			int m = Theme.Mode;
			if (m == 0) { g.FillEllipse(b, cx - r * 0.62f, cy - r * 0.62f, r * 1.24f, r * 1.24f); for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(p, cx + (float)Math.Cos(a) * r * 0.95f, cy + (float)Math.Sin(a) * r * 0.95f, cx + (float)Math.Cos(a) * r * 1.35f, cy + (float)Math.Sin(a) * r * 1.35f); } }
			else if (m == 1) { using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(cx - r, cy - r, r * 2, r * 2); using (GraphicsPath cut = new GraphicsPath()) { cut.AddEllipse(cx - r * 0.2f, cy - r * 1.25f, r * 2, r * 2); using (Region rg = new Region(gp)) { rg.Exclude(cut); g.FillRegion(b, rg); } } } }
			else { g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2); g.FillPie(b, cx - r, cy - r, r * 2, r * 2, 90, 180); }
		}
	}
}

internal class MascotPic : Ctl
{
	public Image Img;
	private readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
	private readonly FrameTimer ft = new FrameTimer { Throttle = true }; private readonly Timer idleTm = new Timer();
	private double clickT = -9, nextIdle = 3.0, idleT = -9, lastT; private bool hover; private float hv;
	private Bitmap sc; private int scH; private Image scSrc;
	private static readonly Random rnd = new Random(); public static bool CalmIdle = Environment.GetEnvironmentVariable("OT_CALM") == "1";   // 가만히 있을 땐 움직이지 않고 마우스를 대거나 누를 때만 반응
	public MascotPic(Image i, Color back)
	{
		Img = i; BackColor = back; Cursor = Cursors.Hand;
		ft.Tick += delegate
		{
			double t = sw.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.05, t - lastT); lastT = t;
			hv += ((hover ? 1f : 0f) - hv) * (float)(1 - Math.Exp(-14 * dt));
			if (!hover && hv < 0.004f) hv = 0f;
			Invalidate();
			if (!hover && hv <= 0f && t - clickT >= 0.9 && t - idleT >= 0.8) ft.Stop();
		};
		idleTm.Interval = 400;
		idleTm.Tick += delegate { double t = sw.Elapsed.TotalSeconds; if (!CalmIdle && t >= nextIdle && Visible) { idleT = t; nextIdle = t + 6 + rnd.NextDouble() * 6; Go(); } };
		idleTm.Start();
	}
	private void Go() { lastT = sw.Elapsed.TotalSeconds; ft.Start(); }
	protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Go(); }
	protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Go(); }
	protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); clickT = sw.Elapsed.TotalSeconds; Go(); }
	protected override void Dispose(bool d) { if (d) { ft.Stop(); idleTm.Stop(); idleTm.Dispose(); if (sc != null) sc.Dispose(); } base.Dispose(d); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		if (Img == null || Width < 2 || Height < 2) return;
		double t = sw.Elapsed.TotalSeconds;
		float h = Height * 0.92f, w = h * Img.Width / Img.Height;
		int ch = (int)Math.Ceiling(h * 2f);
		if (sc == null || scH != ch || !object.ReferenceEquals(scSrc, Img))
		{
			if (sc != null) sc.Dispose();
			sc = new Bitmap((int)Math.Ceiling(ch * (float)Img.Width / Img.Height), ch, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics cg = Graphics.FromImage(sc)) { cg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; cg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality; cg.DrawImage(Img, 0, 0, sc.Width, sc.Height); }
			scH = ch; scSrc = Img;
		}
		float ang = 0, lift = 0, sx = 1, sy = 1;
		double u = (t - clickT) / 0.9;
		// 클릭: 가볍게 통통 튀면서 한 번 갸웃 (부드럽게 잦아들도록 이징)
		if (u >= 0 && u < 1) { double d = 1 - u; ang += (float)(Math.Sin(u * Math.PI * 4) * 12 * d * d); lift += (float)(Math.Abs(Math.Sin(u * Math.PI * 2)) * Height * 0.16 * d); }
		double v = (t - idleT) / 0.8;
		if (v >= 0 && v < 1) { float s = (float)Math.Sin(v * Math.PI); sy *= 1 + 0.07f * s; sx *= 1 - 0.04f * s; }
		lift += hv * Height * 0.05f; sx *= 1 + 0.06f * hv; sy *= 1 + 0.06f * hv;
		g.TranslateTransform(Width / 2f, Height - 1 - lift); g.RotateTransform(ang); g.ScaleTransform(sx, sy);
		g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
		g.DrawImage(sc, -w / 2, -h, w, h);
	}
}

// 홈 화면에서 ↑↑↓↓←→←→ 를 누르면 숨은 폭죽
internal sealed class KonamiWatch : IMessageFilter
{
	private static readonly Keys[] seq = { Keys.Up, Keys.Up, Keys.Down, Keys.Down, Keys.Left, Keys.Right, Keys.Left, Keys.Right };
	private int n; private readonly Func<bool> active; private readonly Action hit;
	public KonamiWatch(Func<bool> active, Action hit) { this.active = active; this.hit = hit; }
	public bool PreFilterMessage(ref Message m)
	{
		if (m.Msg != 0x100 && m.Msg != 0x104) return false;      // WM_KEYDOWN
		if (((long)m.LParam & 0x40000000L) != 0) return false;    // 누르고 있어서 반복되는 입력은 무시
		try
		{
			if (!active()) { n = 0; return false; }
			Keys k = (Keys)(int)m.WParam;
			if (k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu) return false;
			if (k == seq[n]) { n++; if (n >= seq.Length) { n = 0; hit(); } }
			else n = k == Keys.Up ? 1 : 0;
		}
		catch { n = 0; }
		return false;
	}
}

internal partial class Program
{
	private static int Zs(int v) { return Dpi.Z(v); }

	private static TLabel Lbl(Control parent, string text, float pt, int w, Color c, int x, int y, int cw, int ch)
	{
		TLabel l = new TLabel(text, pt, w, c, parent.BackColor);
		l.Location = new Point(Zs(x), Zs(y)); l.Size = new Size(Zs(cw), Zs(ch));
		l.V = StringAlignment.Center;
		parent.Controls.Add(l);
		return l;
	}

	private static void ShowSettings()
	{
		CloseToast();
		if (settingsForm != null && !settingsForm.IsDisposed)
		{
			if (!settingsForm.Visible && settingsReopen != null) { settingsReopen(); return; }
			if (!settingsForm.Visible) settingsForm.Show();
			settingsForm.Opacity = 0.996;
			settingsForm.WindowState = FormWindowState.Normal;
			settingsForm.Activate(); settingsForm.BringToFront();
			return;
		}
		const int WW = 800, HH = 680, SIDE = 200, TOP = 40, FOOT = 60, PX = 28, CW = 544, PH = HH - TOP - FOOT, PW = WW - SIDE;
		SettingsForm f = new SettingsForm();
		settingsForm = f;
		f.Text = "초과근무 알리미";
		f.Icon = appIcon;
		f.Font = Fonts.Native(9f);
		f.StartPosition = FormStartPosition.CenterScreen;
		f.FormBorderStyle = FormBorderStyle.None;
		f.MaximizeBox = true;
		f.BackColor = Theme.Page;
		f.ClientSize = new Size(Zs(880), Zs(HH));
		f.MinimumSize = new Size(Zs(820), Zs(HH));
		RestoreWinRect(f);

		BM("시작");
		// ── 사이드바 ──
		SidePanel side = new SidePanel(); side.Location = new Point(0, 0); side.Size = new Size(Zs(SIDE), Zs(HH));
		MascotPic mp = new MascotPic(MascotImage(), Theme.Side); mp.Location = new Point(Zs(16), Zs(20)); mp.Size = new Size(Zs(38), Zs(38)); mp.Click += delegate { MaryongGame.Tap(); };
		side.Controls.Add(mp);
		Lbl(side, "초과근무 알리미", 11.5f, 2, Theme.Ink, 62, 19, 134, 22);
		Lbl(side, "마룡이와 함께", 9.5f, 0, Theme.Sub, 62, 40, 134, 18);
		string[] navNames = { "홈", "기록", "알림", "휴대폰", "업무", "설정", "사용법" };
		int[][] navGroup = { new int[] { 0 }, new int[] { 2 }, new int[] { 1, 3 }, new int[] { 5 }, new int[] { 7, 10, 6, 8 }, new int[] { 4 }, new int[] { 9 } };   // 메뉴 하나에 모이는 페이지들
		string[][] subNames = { null, null, new string[] { "알림 시간", "휴일" }, null, new string[] { "공문 도우미", "금액·합계", "기한", "수당" }, null, null };
		Func<int, int> grpOf = delegate(int pg) { for (int q = 0; q < navGroup.Length; q++) if (Array.IndexOf(navGroup[q], pg) >= 0) return q; return 0; };
		int[] lastSub = { 0, 2, 1, 5, 7, 4, 9 };
		NavIcon[] navIcons = { NavIcon.Home, NavIcon.Clock, NavIcon.Bell, NavIcon.Phone, NavIcon.Doc, NavIcon.Gear, NavIcon.Help };
		NavItem[] nav = new NavItem[7];
		for (int i = 0; i < 7; i++)
		{
			nav[i] = new NavItem(navNames[i], navIcons[i]);
			nav[i].Location = new Point(Zs(10), Zs(84 + i * 40)); nav[i].Size = new Size(Zs(SIDE - 20), Zs(38));
			side.Controls.Add(nav[i]);
		}
		TLabel verLbl = Lbl(side, "v" + AppVersion, 9.5f, 0, Theme.Muted, 16, HH - 44, 100, 20);
		{
			int vTaps = 0; DateTime vT = DateTime.MinValue;
			verLbl.Click += delegate { DateTime vn = DateTime.Now; if ((vn - vT).TotalSeconds > 1.5) vTaps = 0; vT = vn; vTaps++; if (vTaps >= 5) { vTaps = 0; Bubble.Say("개발자 모드... 가 아니라 오락실 모드!", 2.5); MaryongGame.Open(); } };
		}
		TLabel copyLbl = Lbl(side, "© 2026 uk 제작", 8f, 0, Theme.Muted, 16, HH - 24, 150, 18);
		TLabel acCap = Lbl(side, "포인트 색 · " + Theme.AccentNames[Theme.AccentIdx], 9.5f, 1, Theme.Muted, 16, HH - 226, 170, 18);
		AccentPicker acPick = new AccentPicker(); acPick.BackColor = Theme.Side; acPick.Location = new Point(Zs(12), Zs(HH - 206)); side.Controls.Add(acPick);
		TLabel stCap = Lbl(side, "분위기 · " + Theme.StyleNames[Theme.Style], 9.5f, 1, Theme.Muted, 16, HH - 168, 170, 18);
		TSeg stSeg = new TSeg(Theme.StyleNames, 12); stSeg.BackColor = Theme.Side; stSeg.SnapIndex(Theme.Style); stSeg.Location = new Point(Zs(12), Zs(HH - 148)); side.Controls.Add(stSeg);
		stSeg.Changed += delegate { Program.SetStyle(stSeg.Index); };
		TLabel thCap = Lbl(side, "화면 색", 9.5f, 1, Theme.Muted, 16, HH - 98, 100, 18);
		TSeg thSeg = new TSeg(new string[] { "밝게", "어둡게", "자동" }); thSeg.BackColor = Theme.Side; thSeg.SnapIndex(Theme.Mode);
		thSeg.Location = new Point(Zs(12), Zs(HH - 78));
		side.Controls.Add(thSeg);
		thSeg.Changed += delegate { Program.SetTheme(thSeg.Index); };
		ThemeDot thDot = new ThemeDot(); thDot.BackColor = Theme.Side; thDot.Visible = false; side.Controls.Add(thDot);
		side.MouseDown += delegate(object o, MouseEventArgs me) { if (me.Button == MouseButtons.Left) f.Drag(); };

		// ── 상단 바 (끌어서 이동 · 최소화 · 닫기) ──
		Panel top = new WashPanel(); top.BackColor = Theme.Page; top.Location = new Point(Zs(SIDE), 0); top.Size = new Size(Zs(PW), Zs(TOP));
		top.MouseDown += delegate(object o, MouseEventArgs me) { if (me.Button == MouseButtons.Left) f.Drag(); };
		WinBtn bMin = new WinBtn(false); bMin.BackColor = Theme.Page; bMin.Location = new Point(Zs(PW) - Zs(36 * 2 + 14), Zs(8));
		WinBtn bClose = new WinBtn(true); bClose.BackColor = Theme.Page; bClose.Location = new Point(Zs(PW) - Zs(36 + 10), Zs(8));
		WinBtn bMax = new WinBtn(false); bMax.IsMax = true; bMax.BackColor = Theme.Page;
		top.Controls.Add(bMax);
		bool fading = false;
		Action closeAnim = delegate
		{
			if (fading) return;
			if (!Anim.Effects || f.WindowState != FormWindowState.Normal) { f.Close(); return; }
			fading = true;
			Anim.Run(150, Ease.OutCubic, delegate(float t) { if (!f.IsDisposed) f.Opacity = 0.996 - t * 0.995; }, delegate { if (!f.IsDisposed) f.Close(); });
		};
		bMin.Click += delegate
		{
			if (!Anim.Effects || fading) { f.WindowState = FormWindowState.Minimized; return; }
			fading = true;
			Anim.Run(130, Ease.OutCubic, delegate(float t) { if (!f.IsDisposed) f.Opacity = 0.996 - t * 0.995; }, delegate { if (!f.IsDisposed) { f.WindowState = FormWindowState.Minimized; f.Opacity = 0.996; } fading = false; });
		};
		Action toggleMax = delegate
		{
			if (f.WindowState == FormWindowState.Maximized) f.WindowState = FormWindowState.Normal;
			else { f.SetMaxBounds(Screen.FromControl(f).WorkingArea); f.WindowState = FormWindowState.Maximized; }
		};
		bMax.Click += delegate { toggleMax(); };
		top.DoubleClick += delegate { toggleMax(); };
		bClose.Click += delegate { closeAnim(); };
		top.Controls.AddRange(new Control[] { bMin, bClose });

		// ── 페이지 ──
		BM("사이드바·상단바");
		Panel[] pages = new Panel[11];
		for (int i = 0; i < 11; i++)
		{
			pages[i] = new WashPanel(); pages[i].BackColor = Theme.Page;
			pages[i].Location = new Point(Zs(SIDE), Zs(TOP)); pages[i].Size = new Size(Zs(PW), Zs(PH));
		}
		Action<Panel, string, string> head = delegate(Panel p, string t, string sub)
		{
			Lbl(p, t, 20f, 2, Theme.Ink, PX, 4, 480, 38);
			Lbl(p, sub, 10.5f, 0, Theme.Sub, PX, 42, 480, 22);
		};
		Func<Panel, int, int, Card> card = delegate(Panel p, int y, int h)
		{
			Card c = new Card(); c.Location = new Point(Zs(PX), Zs(y)); c.Size = new Size(Zs(CW), Zs(h)); p.Controls.Add(c); return c;
		};

		// ═════ 홈 ═════ (다음 알림이 가장 눈에 띄게)
		Panel pHome = pages[0];
		head(pHome, "홈", "다음 알림과 이번 달 초과근무를 한눈에 봐요");
		SkyBanner sky = new SkyBanner();
		bool lpDone = false; System.Windows.Forms.Timer lpTm = new System.Windows.Forms.Timer(); lpTm.Interval = 750;
		lpTm.Tick += delegate { lpTm.Stop(); lpDone = true; Bubble.Say(Care.Quitting(DateTime.Now), 4.5); };
		sky.MouseDown += delegate { lpDone = false; lpTm.Stop(); lpTm.Start(); };
		sky.MouseUp += delegate { lpTm.Stop(); };
		sky.Alive = true;
		System.Windows.Forms.Timer invTm = new System.Windows.Forms.Timer(); invTm.Interval = 90000;
		invTm.Tick += delegate { if (sky.Visible && pHome.Visible && sky.FindForm() != null && sky.FindForm().ContainsFocus) { invTm.Stop(); MaryongGame.InviteOnce(); } };
		invTm.Start();
		int fullN = 0; DateTime fullT = DateTime.MinValue;
		sky.Click += delegate
		{
			if (lpDone) { lpDone = false; return; }
			DateTime cn = DateTime.Now; int hm = cn.Hour * 100 + cn.Minute;
			if (hm == 1111 || hm == 1234 || hm == 2222 || hm == 700) { sky.Poke(); Bubble.Say(hm == 700 ? "럭키 세븐! 오락실 문이 열렸어요" : "행운의 시각! 오락실 문이 열렸어요", 2.5); MaryongGame.Open(); return; }
			try { if (monthlyLimit > 0 && Records.CountMonth(cn.Year, cn.Month) >= monthlyLimit) { if ((cn - fullT).TotalSeconds > 1.5) fullN = 0; fullT = cn; fullN++; sky.Poke(); if (fullN >= 3) { fullN = 0; Bubble.Say("이번 달 한도 달성! 수고 보너스로 한 판 해요", 3); MaryongGame.Open(); } return; } } catch { }
			sky.Poke(); MaryongGame.Tap();
		};
		sky.Mascot = MascotImage(); sky.Radius = 24; sky.CalmIdle = Environment.GetEnvironmentVariable("OT_CALM") == "1";   // 기본: 마룡이가 다시 알아서도 움직여요 (측정상 시계 매끄러움에 영향 없음). 가만히 두려면 OT_CALM=1 sky.Radius = 24; sky.Hop = true; sky.Caption = WarmCaption(DateTime.Now); sky.TimePt = 26f;
		sky.Location = new Point(Zs(PX), Zs(70)); sky.Size = new Size(Zs(CW), Zs(140));
		pHome.Controls.Add(sky);

		Card hero = card(pHome, 224, 96); hero.BackColor = Theme.AccentSoft;
		TLabel hCap = new TLabel("다음 알림", 10.5f, 2, Theme.Accent, Theme.AccentSoft); hCap.Location = new Point(Zs(18), Zs(10)); hCap.Size = new Size(Zs(120), Zs(22)); hCap.V = StringAlignment.Center;
		TLabel hTime = new TLabel("", 34f, 2, Theme.Accent, Theme.AccentSoft); hTime.Location = new Point(Zs(18), Zs(32)); hTime.Size = new Size(Zs(190), Zs(54)); hTime.V = StringAlignment.Center; hTime.SetFontObj(Fonts.Num(34f, 2));
		TLabel hRel = new TLabel("", 15f, 2, Theme.Ink, Theme.AccentSoft); hRel.Location = new Point(Zs(212), Zs(38)); hRel.Size = new Size(Zs(170), Zs(28)); hRel.V = StringAlignment.Center;
		TLabel hDate = new TLabel("", 10.5f, 0, Theme.Sub, Theme.AccentSoft); hDate.Location = new Point(Zs(212), Zs(64)); hDate.Size = new Size(Zs(170), Zs(22)); hDate.V = StringAlignment.Center;
		TButton testBtn = new TButton("알림 테스트", BK.Outline); testBtn.BackColor = Theme.AccentSoft; testBtn.Pt = 10f; testBtn.Location = new Point(Zs(CW - 18 - 116), Zs(30)); testBtn.Size = new Size(Zs(116), Zs(36));
		testBtn.Click += delegate { try { Notify(DateTime.Now, true); } catch (Exception ex) { LogError(ex); } };
		hero.Controls.AddRange(new Control[] { hCap, hTime, hRel, hDate, testBtn });

		Card mCard = card(pHome, 334, 146); mCard.Cursor = Cursors.Hand;
		TLabel mCap = new TLabel("이번 달 초과근무", 10.5f, 1, Theme.Sub, Theme.Bg); mCap.Location = new Point(Zs(18), Zs(10)); mCap.Size = new Size(Zs(340), Zs(22)); mCap.V = StringAlignment.Center;
		TLabel mVal = new TLabel("", 22f, 2, Theme.Ink, Theme.Bg); mVal.Location = new Point(Zs(18), Zs(30)); mVal.Size = new Size(Zs(120), Zs(36)); mVal.V = StringAlignment.Center;
		TLabel mLim = new TLabel("", 12f, 1, Theme.Sub, Theme.Bg); mLim.Location = new Point(Zs(140), Zs(34)); mLim.Size = new Size(Zs(120), Zs(30)); mLim.V = StringAlignment.Center;
		TLabel mPct = new TLabel("", 17f, 2, Theme.Accent, Theme.Bg); mPct.Location = new Point(Zs(CW - 18 - 120), Zs(34)); mPct.Size = new Size(Zs(120), Zs(32)); mPct.H = StringAlignment.Far; mPct.V = StringAlignment.Center;
		ProgBar mBar = new ProgBar(); mBar.BackColor = Theme.Bg; mBar.Location = new Point(Zs(18), Zs(68)); mBar.Size = new Size(Zs(CW - 36), Zs(9));
		TLabel mHint = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); mHint.Location = new Point(Zs(18), Zs(78)); mHint.Size = new Size(Zs(CW - 36), Zs(18)); mHint.V = StringAlignment.Center;
		TLabel yCap = new TLabel("", 10f, 1, Theme.Sub, Theme.Bg); yCap.Location = new Point(Zs(18), Zs(106)); yCap.Size = new Size(Zs(CW - 36 - 90), Zs(20)); yCap.V = StringAlignment.Center;
		TLabel yPct = new TLabel("", 10.5f, 2, Theme.Accent, Theme.Bg); yPct.Location = new Point(Zs(CW - 18 - 90), Zs(106)); yPct.Size = new Size(Zs(90), Zs(20)); yPct.H = StringAlignment.Far; yPct.V = StringAlignment.Center;
		ProgBar yBar = new ProgBar(); yBar.BackColor = Theme.Bg; yBar.Location = new Point(Zs(18), Zs(130)); yBar.Size = new Size(Zs(CW - 36), Zs(6));
		mCard.Controls.AddRange(new Control[] { mCap, mVal, mLim, mPct, mBar, mHint, yCap, yPct, yBar });
		DateTime menuShownAt = DateTime.MinValue;
		int hw2 = (CW - 12) / 2;
		Card tCard = new Card(); tCard.Location = new Point(Zs(PX), Zs(488)); tCard.Size = new Size(Zs(hw2), Zs(74)); pHome.Controls.Add(tCard);
		TLabel tCap = new TLabel("오늘 기록", 10f, 1, Theme.Sub, Theme.Bg); tCap.Location = new Point(Zs(16), Zs(10)); tCap.Size = new Size(Zs(hw2 - 32), Zs(20));
		TLabel tVal = new TLabel("", 15f, 2, Theme.Ink, Theme.Bg); tVal.Location = new Point(Zs(16), Zs(32)); tVal.Size = new Size(Zs(hw2 - 32), Zs(30)); tVal.V = StringAlignment.Center;
		tCard.Controls.AddRange(new Control[] { tCap, tVal });
		Card rCard = new Card(); rCard.Location = new Point(Zs(PX + hw2 + 12), Zs(488)); rCard.Size = new Size(Zs(hw2), Zs(74)); pHome.Controls.Add(rCard);
		TLabel rCap = new TLabel("점심 · 퇴근", 10f, 1, Theme.Sub, Theme.Bg); rCap.Location = new Point(Zs(16), Zs(10)); rCap.Size = new Size(Zs(hw2 - 32), Zs(20));
		TLabel rVal = new TLabel("", 15f, 2, Theme.Ink, Theme.Bg); rVal.V = StringAlignment.Center; rVal.Location = new Point(Zs(16), Zs(32)); rVal.Size = new Size(Zs(hw2 - 32 - 108), Zs(30)); rVal.SetFontObj(Fonts.Num(15f, 2));
		TButton menuBtn = new TButton("점심 메뉴", BK.Outline); menuBtn.Pt = 9f; menuBtn.BackColor = Theme.Bg; menuBtn.Location = new Point(Zs(hw2 - 12 - 104), Zs(30)); menuBtn.Size = new Size(Zs(104), Zs(38));
		menuBtn.Click += delegate { try { string pick = PickMenuAndSearch(); rCap.Text = "오늘 점심은 " + pick + "! (익산시 검색)"; menuShownAt = DateTime.Now; } catch (Exception ex) { LogError(ex); } };
		rCard.Controls.AddRange(new Control[] { rCap, rVal, menuBtn });

		// ═════ 기록 ═════
		Panel pRe = pages[2];
		head(pRe, "기록", "날짜별 초과근무와 월 한도를 확인해요");
		foreach (Control hc0 in pRe.Controls) hc0.Width = Zs(270);
		TButton rPrev = new TButton("‹", BK.Outline); rPrev.Pt = 13f; rPrev.BackColor = Theme.Bg; rPrev.Location = new Point(Zs(314), Zs(8)); rPrev.Size = new Size(Zs(34), Zs(34));
		TLabel rMonth = new TLabel("", 12f, 1, Theme.Ink, Theme.Bg); rMonth.Location = new Point(Zs(350), Zs(8)); rMonth.Size = new Size(Zs(110), Zs(34)); rMonth.H = StringAlignment.Center; rMonth.V = StringAlignment.Center;
		TButton rNext = new TButton("›", BK.Outline); rNext.Pt = 13f; rNext.BackColor = Theme.Bg; rNext.Location = new Point(Zs(462), Zs(8)); rNext.Size = new Size(Zs(34), Zs(34));
		TButton rNow = new TButton("오늘", BK.Ghost); rNow.Pt = 10f; rNow.BackColor = Theme.Bg; rNow.Location = new Point(Zs(PX + CW - 66), Zs(8)); rNow.Size = new Size(Zs(66), Zs(34));
		pRe.Controls.AddRange(new Control[] { rPrev, rMonth, rNext, rNow });

		Card gCard = card(pRe, 78, 112);
		TLabel gCap = Lbl(gCard, "", 10.5f, 1, Theme.Sub, 18, 10, 322, 24);
		{ TLabel lml = Lbl(gCard, "월 한도", 10f, 0, Theme.Sub, CW - 18 - 130 - 52, 10, 50, 26); lml.H = StringAlignment.Far; lml.Tag = "right"; }
		TStepper limStep = new TStepper(); limStep.Min = 1; limStep.Max = 300; limStep.Suffix = "시간"; limStep.Value = monthlyLimit; limStep.BackColor = Theme.Bg;
		limStep.Size = new Size(Zs(120), Zs(30)); limStep.Location = new Point(Zs(CW - 18 - 120), Zs(8)); gCard.Controls.Add(limStep);
		TLabel gVal = new TLabel("", 22f, 2, Theme.Ink, Theme.Bg); gVal.Location = new Point(Zs(18), Zs(36)); gVal.Size = new Size(Zs(130), Zs(36)); gVal.V = StringAlignment.Center; gCard.Controls.Add(gVal);
		TLabel gLim = new TLabel("", 12f, 1, Theme.Sub, Theme.Bg); gLim.Location = new Point(Zs(150), Zs(40)); gLim.Size = new Size(Zs(120), Zs(30)); gLim.V = StringAlignment.Center; gCard.Controls.Add(gLim);
		TLabel gPct = new TLabel("", 16f, 2, Theme.Accent, Theme.Bg); gPct.Location = new Point(Zs(CW - 18 - 120), Zs(38)); gPct.Size = new Size(Zs(120), Zs(32)); gPct.H = StringAlignment.Far; gPct.V = StringAlignment.Center; gCard.Controls.Add(gPct);
		ProgBar gBar = new ProgBar(); gBar.BackColor = Theme.Bg; gBar.Location = new Point(Zs(18), Zs(78)); gBar.Size = new Size(Zs(CW - 36), Zs(9)); gCard.Controls.Add(gBar);
		TLabel gHint = Lbl(gCard, "", 10f, 0, Theme.Sub, 18, 88, 260, 20);
		TLabel gHint2 = Lbl(gCard, "", 10f, 0, Theme.Sub, CW - 18 - 250, 88, 250, 20); gHint2.H = StringAlignment.Far;

		RecCal rcal = new RecCal(); rcal.BackColor = Theme.Bg; rcal.Location = new Point(Zs(PX - 3), Zs(204));
		rcal.IsHoliday = delegate(DateTime d) { return IsHoliday(d); };
		rcal.Hours = delegate(DateTime d) { return Records.CountOn(d); };
		pRe.Controls.Add(rcal);
		int rx = PX - 3 + rcal.Width + Zs(22);
		int rw = Zs(PW) - rx - Zs(PX);
		TLabel dTitle = new TLabel("", 15f, 2, Theme.Ink, Theme.Bg); dTitle.Location = new Point(rx, Zs(208)); dTitle.Size = new Size(rw, Zs(30)); dTitle.V = StringAlignment.Center; pRe.Controls.Add(dTitle);
		TLabel dSub = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); dSub.Wrap = true; dSub.Location = new Point(rx, Zs(238)); dSub.Size = new Size(rw, Zs(40)); pRe.Controls.Add(dSub);
		WashPanel dTags = new WashPanel(); dTags.BackColor = Theme.Page; dTags.Location = new Point(rx, Zs(282)); dTags.Size = new Size(rw, Zs(76)); pRe.Controls.Add(dTags);
		Card aCard = new Card(); aCard.Location = new Point(rx, Zs(362)); aCard.Size = new Size(rw, Zs(150)); pRe.Controls.Add(aCard);
		int iw = rw - Zs(32);
		TLabel aCap = new TLabel("시간대 추가", 10f, 1, Theme.Sub, Theme.Bg); aCap.Location = new Point(Zs(16), Zs(8)); aCap.Size = new Size(Zs(80), Zs(26)); aCap.V = StringAlignment.Center; aCard.Controls.Add(aCap);
		TButton undoBtn = new TButton("되돌리기", BK.Ghost); undoBtn.BackColor = Theme.Bg; undoBtn.Pt = 9.5f; undoBtn.Location = new Point(rw - Zs(12 + 76), Zs(8)); undoBtn.Size = new Size(Zs(76), Zs(26)); undoBtn.Visible = false; aCard.Controls.Add(undoBtn);
		TStepper hStep = new TStepper(); hStep.Min = 0; hStep.Max = 23; hStep.Suffix = "시부터"; hStep.Value = Math.Min(23, DateTime.Now.Hour); hStep.BackColor = Theme.Bg;
		hStep.Location = new Point(Zs(16), Zs(38)); hStep.Size = new Size(iw, Zs(30)); aCard.Controls.Add(hStep);
		TStepper hStep2 = new TStepper(); hStep2.Min = 0; hStep2.Max = 23; hStep2.Suffix = "시까지"; hStep2.Value = hStep.Value; hStep2.BackColor = Theme.Bg;
		hStep2.Location = new Point(Zs(16), Zs(72)); hStep2.Size = new Size(iw, Zs(30)); aCard.Controls.Add(hStep2);
		hStep.ValueChanged += delegate { if (hStep2.Value < hStep.Value) hStep2.Value = hStep.Value; };
		TButton hAdd = new TButton("이 날에 추가", BK.Primary); hAdd.BackColor = Theme.Bg; hAdd.Pt = 10f; hAdd.Location = new Point(Zs(16), Zs(108)); hAdd.Size = new Size(iw, Zs(32)); aCard.Controls.Add(hAdd);
		rcal.Missed = delegate(DateTime d) { return Records.MissedCount(d); };
		rcal.Pre8 = delegate(DateTime d) { return Records.HasPre8(d); };
		TButton expBtn = new TButton("이 달 기록 표로 저장", BK.Outline); expBtn.BackColor = Theme.Bg; expBtn.Pt = 10f; expBtn.Size = new Size(rcal.Width, Zs(34)); expBtn.Location = new Point(rcal.Left, Math.Min(Zs(526), rcal.Bottom + Zs(10))); pRe.Controls.Add(expBtn);
		expBtn.Click += delegate
		{
			try
			{
				DateTime m0 = rcal.Month; string path = ExportMonthCsv(m0.Year, m0.Month);
				Bubble.Say(path == null ? "저장할 기록이 없어요" : "바탕화면에 저장했어요\n" + System.IO.Path.GetFileName(path), 5);
			}
			catch (Exception ex) { LogError(ex); Bubble.Say("저장하지 못했어요", 4); }
		};
		TLabel p8Lbl = new TLabel("8시 이전 근무 기록함", 10f, 1, Theme.Ink, Theme.Bg); p8Lbl.Location = new Point(rx + Zs(4), Zs(522)); p8Lbl.Size = new Size(rw - Zs(64), Zs(28)); p8Lbl.V = StringAlignment.Center; pRe.Controls.Add(p8Lbl);
		TToggle p8Tg = new TToggle(); p8Tg.BackColor = Theme.Bg; p8Tg.Location = new Point(rx + rw - Zs(46), Zs(524)); pRe.Controls.Add(p8Tg);
		bool p8Busy = false;
		p8Tg.CheckedChanged += delegate { if (p8Busy) return; Records.SetPre8(rcal.Selected.Date, p8Tg.Checked); rcal.Invalidate(); };

		int lastRecCount = -1;
		DateTime lastPickDay = DateTime.MinValue;
		Action refreshRec = null;
		refreshRec = delegate
		{
			lastRecCount = Records.CountAll();
			DateTime m = rcal.Month;
			rMonth.Text = m.Year + "년 " + m.Month + "월";
			int mon = Records.CountMonth(m.Year, m.Month);
			int lim = monthlyLimit;
			double fr = lim > 0 ? (double)mon / lim : 0;
			Color gc = fr >= 1.0 ? Theme.Red : (fr >= 0.75 ? Theme.Amber : Theme.Accent);
			gCap.Text = m.Month + "월 초과근무";
			gVal.Text = mon + "시간"; gLim.Text = "/ " + lim + "시간";
			gPct.Text = (int)Math.Round(fr * 100) + "%"; gPct.Fore = gc;
			gBar.Frac = fr; gBar.C = gc; gBar.Invalidate();
			gHint.Text = mon >= lim ? (mon == lim ? "한도를 모두 채웠어요" : "한도를 " + (mon - lim) + "시간 넘었어요") : "한도까지 " + (lim - mon) + "시간 남았어요";
			gHint.Fore = fr >= 1.0 ? Theme.Red : Theme.Sub;
			int days = Records.All().Where(r => r.Date.Year == m.Year && r.Date.Month == m.Month).Select(r => r.Date).Distinct().Count();
			gHint2.Text = "기록한 날 " + days + "일 · 올해 " + Records.CountYear(m.Year) + " / " + YearLimit + "시간";
			rcal.Invalidate();
			DateTime d = rcal.Selected.Date;
			p8Busy = true; p8Tg.SetImmediate(Records.HasPre8(d)); p8Busy = false;
			dTitle.Text = d.Month + "월 " + d.Day + "일 " + Records.Dow[(int)d.DayOfWeek] + "요일";
			List<int> hrs = Records.All().Where(r => r.Date == d).Select(r => r.Hour).OrderBy(h => h).ToList();
			List<int> miss = Records.MissedOn(d);
			dSub.Text = (hrs.Count > 0 ? hrs.Count + "시간 기록했어요" : "이 날은 기록이 없어요") + (miss.Count > 0 ? "\r\n놓친 알림 " + string.Join(", ", miss.Select(x => x + "시").ToArray()) + " (빨간 점)" : "");
			dSub.Fore = miss.Count > 0 ? Theme.Red : Theme.Sub;
			if (miss.Count > 0 && lastPickDay != d) { hStep.Value = miss[0]; hStep2.Value = miss[miss.Count - 1]; }
			lastPickDay = d;
			undoBtn.Visible = Records.CanUndo;
			for (int i = dTags.Controls.Count - 1; i >= 0; i--) { Control o = dTags.Controls[i]; dTags.Controls.RemoveAt(i); o.Dispose(); }
			int x = 0, y = 0;
			foreach (int hh in hrs)
			{
				int h2 = hh;
				TTag t = new TTag(h2 + "시"); t.BackColor = Theme.Page;
				if (x + t.Width > dTags.Width) { x = 0; y += Zs(38); }
				t.Location = new Point(x, y);
				t.Remove += delegate { Records.BeginOp(); Records.Remove(d, h2); refreshRec(); lastNextReset(); };
				dTags.Controls.Add(t);
				x += t.Width + Zs(6);
			}
		};
		limStep.ValueChanged += delegate { MonthlyLimit = limStep.Value; refreshRec(); lastNextReset(); };
		rPrev.Click += delegate { rcal.Month = rcal.Month.AddMonths(-1); refreshRec(); };
		rNext.Click += delegate { rcal.Month = rcal.Month.AddMonths(1); refreshRec(); };
		rNow.Click += delegate { rcal.Month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1); rcal.Selected = DateTime.Today; refreshRec(); };
		rcal.DateChanged += delegate { refreshRec(); };
		hAdd.Click += delegate
		{
			int added = 0, dup = 0;
			Records.BeginOp();
			for (int hh = hStep.Value; hh <= hStep2.Value; hh++) { if (Records.Add(rcal.Selected, hh, DateTime.Now)) added++; else dup++; }
			refreshRec(); lastNextReset();
			dSub.Fore = Theme.Sub;
			dSub.Text = added + "개 추가했어요" + (dup > 0 ? " (이미 있는 " + dup + "개 제외)" : "");
		};
		undoBtn.Click += delegate
		{
			int n0 = Records.Undo();
			refreshRec(); lastNextReset();
			dSub.Fore = Theme.Sub; dSub.Text = n0 + "건을 되돌렸어요";
		};

		// ═════ 알림 ═════
		Panel pAl = pages[1];
		head(pAl, "알림", "알림 시각과 평일에 알림 받을 시간을 정해요");
		Card cA = card(pAl, 78, 136);
		Lbl(cA, "매시 알림 시각", 11f, 1, Theme.Ink, 18, 12, 300, 24);
		Panel tagHost = new Panel(); tagHost.BackColor = Theme.Bg; tagHost.Location = new Point(Zs(18), Zs(46)); tagHost.Size = new Size(Zs(CW - 36), Zs(36)); cA.Controls.Add(tagHost);
		TStepper nud = new TStepper(); nud.Min = 0; nud.Max = 59; nud.Value = 30; nud.BackColor = Theme.Bg; nud.Location = new Point(Zs(18), Zs(94)); cA.Controls.Add(nud);
		TButton addMin = new TButton("추가", BK.Primary); addMin.BackColor = Theme.Bg; addMin.Location = new Point(Zs(160), Zs(94)); addMin.Size = new Size(Zs(72), Zs(34)); cA.Controls.Add(addMin);
		Lbl(cA, "분을 고르고 추가하면 알림이 늘어나요", 10f, 0, Theme.Sub, 244, 94, 290, 34);

		// 인사랑 바로가기 (주소는 프로그램 안에 들어 있어요)
		Card cI = card(pAl, 444, 94);
		Lbl(cI, "인사랑 바로가기", 11f, 1, Theme.Ink, 18, 8, 300, 24);
		Lbl(cI, "알림창에서 '확인했어요'를 눌렀을 때만 인사랑을 앞으로 가져와요", 10f, 0, Theme.Sub, 18, 31, CW - 36 - 56, 20);
		TToggle frontTg = new TToggle(); frontTg.BackColor = Theme.Bg; frontTg.Location = new Point(Zs(CW - 42 - 16), Zs(12)); frontTg.SetImmediate(insarangFront); cI.Controls.Add(frontTg);
		TButton insOpen = new TButton("인사랑 열기", BK.Outline); insOpen.BackColor = Theme.Bg; insOpen.Pt = 10.5f; insOpen.Location = new Point(Zs(18), Zs(56)); insOpen.Size = new Size(Zs(140), Zs(30)); cI.Controls.Add(insOpen);
		insOpen.Click += delegate { OpenInsarang(); };

		List<int> mins = new List<int>(minutes);
		Action fillMins = null;
		fillMins = delegate
		{
			mins.Sort();
			for (int i = tagHost.Controls.Count - 1; i >= 0; i--) { Control o = tagHost.Controls[i]; tagHost.Controls.RemoveAt(i); o.Dispose(); }
			int x = 0;
			foreach (int m in mins)
			{
				int mm = m;
				TTag t = new TTag("매시 " + m + "분");
				t.BackColor = Theme.Bg;
				t.Location = new Point(x, Zs(2));
				t.Remove += delegate
				{
					if (mins.Count <= 1) { MessageBox.Show(f, "알림 시각은 최소 1개가 필요합니다.", "알림"); return; }
					mins.Remove(mm); fillMins();
				};
				tagHost.Controls.Add(t);
				x += t.Width + Zs(8);
			}
		};
		addMin.Click += delegate { nud.Commit(); if (!mins.Contains(nud.Value)) mins.Add(nud.Value); fillMins(); };
		fillMins();

		Card cB = card(pAl, 224, 216);
		Lbl(cB, "평일에 알림 받을 시간", 11f, 1, Theme.Ink, 18, 12, 300, 24);
		Lbl(cB, "보라색 칸이 알림을 받는 시간이에요. 주말·공휴일은 24시간 매시간 알려요.", 10f, 0, Theme.Sub, 18, 38, CW - 36, 20);
		TChip[] hb = new TChip[24];
		int chipW = (CW - 36 - 5 * 11) / 12;
		for (int i = 0; i < 24; i++)
		{
			TChip ch = new TChip(i.ToString()); ch.BackColor = Theme.Bg;
			ch.Location = new Point(Zs(18) + i % 12 * (chipW + Zs(5)), Zs(68) + i / 12 * Zs(40));
			ch.Size = new Size(chipW, Zs(34));
			ch.SetImmediate(weekdayHours[i]);
			hb[i] = ch; cB.Controls.Add(ch);
		}
		Lbl(cB, "윗줄 0~11시 · 아랫줄 12~23시", 10f, 0, Theme.Sub, 18, 150, 300, 18);
		TButton pNight = new TButton("야간 18~08시", BK.Outline), pAll = new TButton("하루 종일", BK.Outline), pNone = new TButton("모두 해제", BK.Ghost);
		TButton[] pb = { pNight, pAll, pNone };
		int[] pw = { 124, 100, 96 }; int px = 18;
		for (int i = 0; i < 3; i++) { pb[i].BackColor = Theme.Bg; pb[i].Pt = 10f; pb[i].Location = new Point(Zs(px), Zs(172)); pb[i].Size = new Size(Zs(pw[i]), Zs(34)); px += pw[i] + 8; cB.Controls.Add(pb[i]); }
		pNight.Click += delegate { for (int n = 0; n < 24; n++) hb[n].Checked = n >= 18 || n <= 8; };
		pAll.Click += delegate { for (int n = 0; n < 24; n++) hb[n].Checked = true; };
		pNone.Click += delegate { for (int n = 0; n < 24; n++) hb[n].Checked = false; };

		// ═════ 휴일 ═════
		Panel pHo = pages[3];
		head(pHo, "휴일", "날짜를 골라 휴일로 지정하면 그날은 매시간 알려요");
		MiniCal cal = new MiniCal(); cal.BackColor = Theme.Bg; cal.IsHoliday = delegate(DateTime d) { return IsHoliday(d); };
		cal.Location = new Point(Zs(PX - 4), Zs(84)); pHo.Controls.Add(cal);
		int hx = PX - 4 + cal.Width + Zs(20);
		TLabel selTitle = new TLabel("", 17f, 2, Theme.Ink, Theme.Bg); selTitle.Location = new Point(hx, Zs(88)); selTitle.Size = new Size(Zs(PW) - hx - Zs(PX), Zs(32)); selTitle.V = StringAlignment.Center; pHo.Controls.Add(selTitle);
		TLabel selSub = new TLabel("", 11f, 0, Theme.Sub, Theme.Bg); selSub.Location = new Point(hx, Zs(122)); selSub.Size = new Size(selTitle.Width, Zs(22)); selSub.V = StringAlignment.Center; pHo.Controls.Add(selSub);
		Card hCard = new Card(); hCard.Location = new Point(hx, Zs(160)); hCard.Size = new Size(selTitle.Width, Zs(70)); pHo.Controls.Add(hCard);
		Lbl(hCard, "휴일로 처리", 11f, 1, Theme.Ink, 16, 12, 150, 22);
		Lbl(hCard, "매시간 알림", 10f, 0, Theme.Sub, 16, 34, 150, 20);
		TToggle holTg = new TToggle(); holTg.BackColor = Theme.Bg; holTg.Location = new Point(hCard.Width - Zs(42 + 16), Zs(23)); hCard.Controls.Add(holTg);
		TLabel legend = new TLabel("빨간색은 공휴일·일요일, 파란색은 토요일이에요. 놓친 공휴일은 날짜를 고르고 켜 주세요. 끄면 휴일에서 빠져요. 바로 저장돼요.", 10f, 0, Theme.Sub, Theme.Bg);
		legend.Wrap = true; legend.Location = new Point(hx, Zs(244)); legend.Size = new Size(selTitle.Width, Zs(100)); pHo.Controls.Add(legend);

		// ═════ 일반 ═════
		Panel pGe = pages[4];
		head(pGe, "설정", "시작 · 소리 · 방해 금지 · 공휴일");
		Card cG = card(pGe, 76, 208);
		Lbl(cG, "윈도우 시작 시 자동 실행", 11f, 1, Theme.Ink, 18, 8, 360, 22);
		Lbl(cG, "켜면 컴퓨터를 켤 때 알아서 실행돼요", 10f, 0, Theme.Sub, 18, 30, 380, 20);
		TToggle autoTg = new TToggle(); autoTg.BackColor = Theme.Bg; autoTg.Location = new Point(Zs(CW - 42 - 18), Zs(16)); autoTg.SetImmediate(autoStart); cG.Controls.Add(autoTg);
		Lbl(cG, "알림음 사용", 11f, 1, Theme.Ink, 18, 62, 200, 22);
		Lbl(cG, "알림이 올 때 소리를 내요", 10f, 0, Theme.Sub, 18, 84, 260, 20);
		TToggle sndTg = new TToggle(); sndTg.BackColor = Theme.Bg; sndTg.Location = new Point(Zs(CW - 42 - 18), Zs(70)); sndTg.SetImmediate(soundOn); cG.Controls.Add(sndTg);
		TButton hear = new TButton("들어보기", BK.Outline); hear.BackColor = Theme.Bg; hear.Pt = 10f; hear.Location = new Point(Zs(CW - 42 - 18 - 92), Zs(68)); hear.Size = new Size(Zs(80), Zs(30)); cG.Controls.Add(hear);
		hear.Click += delegate { Sfx.PlayAlert(); };
		Lbl(cG, "퇴근 카운트다운", 11f, 1, Theme.Ink, 18, 116, 200, 22);
		Lbl(cG, "평일 18시 정각 30초 전에 카드가 떠서 초 단위로 세어 줘요", 10f, 0, Theme.Sub, 18, 138, 330, 20);
		TToggle quitTg = new TToggle(); quitTg.BackColor = Theme.Bg; quitTg.Location = new Point(Zs(CW - 42 - 18), Zs(124)); quitTg.SetImmediate(quitOn); cG.Controls.Add(quitTg);
		Lbl(cG, "정각 10초 전 카운트다운", 11f, 1, Theme.Ink, 18, 166, 240, 22);
		Lbl(cG, "알림 시각 10초 전에 오른쪽 아래에 카드가 떠서 세어 줘요", 10f, 0, Theme.Sub, 18, 186, 340, 18);
		TToggle preTg = new TToggle(); preTg.BackColor = Theme.Bg; preTg.Location = new Point(Zs(CW - 42 - 18), Zs(172)); preTg.SetImmediate(preHourOn); cG.Controls.Add(preTg);
		Panel line1c = new Panel(); line1c.BackColor = Theme.Line; line1c.Location = new Point(Zs(18), Zs(160)); line1c.Size = new Size(Zs(CW - 36), 1); cG.Controls.Add(line1c);
		Panel line1 = new Panel(); line1.BackColor = Theme.Line; line1.Location = new Point(Zs(18), Zs(54)); line1.Size = new Size(Zs(CW - 36), 1); cG.Controls.Add(line1);
		Panel line1b = new Panel(); line1b.BackColor = Theme.Line; line1b.Location = new Point(Zs(18), Zs(108)); line1b.Size = new Size(Zs(CW - 36), 1); cG.Controls.Add(line1b);

		Card cPet = card(pGe, 458, 70);
		Lbl(cPet, "마룡이 쓰다듬기", 11f, 1, Theme.Ink, 18, 8, 300, 22);
		Lbl(cPet, "쓰다듬고 놀아 주세요 · " + Bond.Info(), 10f, 0, Theme.Sub, 18, 32, 400, 20);
		TButton petBtn = new TButton("쓰다듬기", BK.Primary); petBtn.BackColor = Theme.Bg; petBtn.Pt = 10.5f; petBtn.Location = new Point(Zs(CW - 18 - 110), Zs(15)); petBtn.Size = new Size(Zs(110), Zs(34)); cPet.Controls.Add(petBtn);
		petBtn.Click += delegate { PetWin.Toggle(); petBtn.Text = PetWin.IsOn ? "집에 보내기" : "쓰다듬기"; };
		Card cFo = card(pGe, 534, 44);
		Lbl(cFo, "마룡이 운세 · 타로", 11f, 1, Theme.Ink, 18, 2, 300, 22);
		Lbl(cFo, "스위치: PC 켠 뒤 첫 실행 때 오늘 운세를 알림으로", 8.5f, 0, Theme.Sub, 18, 23, CW - 18 - 198 - 22, 18);
		TToggle foTg = new TToggle(); foTg.BackColor = Theme.Bg; foTg.Location = new Point(Zs(CW - 18 - 110 - 12 - 42), Zs(11)); foTg.SetImmediate(foStart); cFo.Controls.Add(foTg);
		TButton foBtn = new TButton("열기", BK.Primary); foBtn.BackColor = Theme.Bg; foBtn.Pt = 10.5f; foBtn.Location = new Point(Zs(CW - 18 - 110), Zs(5)); foBtn.Size = new Size(Zs(110), Zs(34)); cFo.Controls.Add(foBtn);
		foBtn.Click += delegate { FortuneForm.Open(); };
		Card cD = card(pGe, 290, 162);
		Lbl(cD, "방해 금지 모드", 11f, 1, Theme.Ink, 18, 8, 360, 22);
		Lbl(cD, "켜면 팝업·소리 없이, 오른쪽 아래 작은 알림만 보내요", 10f, 0, Theme.Sub, 18, 30, 400, 20);
		TToggle dndTg = new TToggle(); dndTg.BackColor = Theme.Bg; dndTg.Location = new Point(Zs(CW - 42 - 18), Zs(16)); dndTg.SetImmediate(dndManual); cD.Controls.Add(dndTg);
		Panel line2 = new Panel(); line2.BackColor = Theme.Line; line2.Location = new Point(Zs(18), Zs(54)); line2.Size = new Size(Zs(CW - 36), 1); cD.Controls.Add(line2);
		Lbl(cD, "전체화면·발표 중 자동 방해 금지", 11f, 1, Theme.Ink, 18, 62, 400, 22);
		Lbl(cD, "영상·게임·PPT 발표 중에는 알림을 미뤘다가 끝나면 알려줘요", 10f, 0, Theme.Sub, 18, 84, 420, 20);
		TToggle dndAutoTg = new TToggle(); dndAutoTg.BackColor = Theme.Bg; dndAutoTg.Location = new Point(Zs(CW - 42 - 18), Zs(70)); dndAutoTg.SetImmediate(dndAuto); cD.Controls.Add(dndAutoTg);
		Panel line3 = new Panel(); line3.BackColor = Theme.Line; line3.Location = new Point(Zs(18), Zs(108)); line3.Size = new Size(Zs(CW - 36), 1); cD.Controls.Add(line3);
		Lbl(cD, "간단 알림만 받기", 11f, 1, Theme.Ink, 18, 116, 400, 22);
		Lbl(cD, "큰 알림창 없이 위쪽 리본과 오른쪽 아래 작은 카드로만 알려요", 10f, 0, Theme.Sub, 18, 138, 420, 20);
		TToggle lightTg = new TToggle(); lightTg.BackColor = Theme.Bg; lightTg.Location = new Point(Zs(CW - 42 - 18), Zs(124)); lightTg.SetImmediate(lightAlert); cD.Controls.Add(lightTg);

		Card cS = card(pHo, 404, 172);
		Lbl(cS, "공휴일 자동 업데이트", 11f, 1, Theme.Ink, 18, 8, 300, 24);
		Lbl(cS, "공휴일을 자동으로 최신화해요 (선택) · 공공데이터포털 인증키 입력", 10f, 0, Theme.Sub, 18, 32, CW - 36, 20);
		TInput kin = new TInput(); kin.Location = new Point(Zs(18), Zs(56)); kin.Size = new Size(Zs(CW - 36), Zs(34)); kin.Box.Text = kasiKey; cS.Controls.Add(kin);
		TButton syncBtn = new TButton("지금 업데이트", BK.Primary); syncBtn.BackColor = Theme.Bg; syncBtn.Pt = 10f; syncBtn.Location = new Point(Zs(18), Zs(98)); syncBtn.Size = new Size(Zs(110), Zs(32)); cS.Controls.Add(syncBtn);
		TButton keyLink = new TButton("키 발급 data.go.kr", BK.Ghost); keyLink.BackColor = Theme.Bg; keyLink.Pt = 10f; keyLink.Location = new Point(Zs(138), Zs(98)); keyLink.Size = new Size(Zs(160), Zs(32)); cS.Controls.Add(keyLink);
		keyLink.Click += delegate { try { Process.Start("https://www.data.go.kr"); } catch { } };
		TLabel syncSt = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); syncSt.Wrap = true; syncSt.Location = new Point(Zs(18), Zs(134)); syncSt.Size = new Size(Zs(CW - 36), Zs(36)); cS.Controls.Add(syncSt);

		// ═════ 휴대폰 ═════ (ntfy 앱으로 같은 알림을 폰에서도 받기)
		Panel pPh = pages[5];
		head(pPh, "휴대폰", "PC에서 알림이 울릴 때 휴대폰에도 똑같이 알려줘요");
		Card cPh = card(pPh, 78, 78);
		Lbl(cPh, "휴대폰으로 알림 받기", 11f, 1, Theme.Ink, 18, 12, 360, 22);
		Lbl(cPh, "알림 · 마감 5분 전 · 다시 알림이 폰에도 가요 (PC가 켜져 있어야 해요)", 10f, 0, Theme.Sub, 18, 36, CW - 100, 34);
		TToggle phTg = new TToggle(); phTg.BackColor = Theme.Bg; phTg.Location = new Point(Zs(CW - 42 - 18), Zs(20)); phTg.SetImmediate(phoneOn); cPh.Controls.Add(phTg);
		string phTopic = string.IsNullOrEmpty(phoneTopic) ? Push.NewTopic() : phoneTopic;
		Card cQr = card(pPh, 168, 336);
		QrBox qr = new QrBox(); qr.Location = new Point(Zs(22), Zs(22)); qr.Size = new Size(Zs(214), Zs(214)); cQr.Controls.Add(qr);
		Lbl(cQr, "ntfy 앱에서 QR 스캔", 10f, 0, Theme.Sub, 22, 242, 214, 20).H = StringAlignment.Center;
		Lbl(cQr, "연결 방법", 11f, 2, Theme.Ink, 264, 20, 240, 22);
		TLabel stepLb = new TLabel("1. 폰에 'ntfy' 앱을 설치해요\n    (Play 스토어 · 앱스토어에서 검색)\n\n2. 앱에서 + 를 누르고 왼쪽 QR을 스캔해요\n\n3. 아래 '테스트 알림 보내기'를 눌러요", 11f, 0, Theme.Ink, Theme.Bg);
		stepLb.Wrap = true; stepLb.Location = new Point(Zs(264), Zs(48)); stepLb.Size = new Size(Zs(CW - 264 - 18), Zs(150)); cQr.Controls.Add(stepLb);
		Lbl(cQr, "내 알림 주소 (직접 입력할 때만)", 10f, 1, Theme.Ink, 264, 204, 240, 20);
		TInput topicIn = new TInput(); topicIn.Location = new Point(Zs(264), Zs(228)); topicIn.Size = new Size(Zs(CW - 264 - 18), Zs(36)); topicIn.Box.Text = phTopic; topicIn.Box.ReadOnly = true; cQr.Controls.Add(topicIn);
		TButton phTest = new TButton("테스트 알림 보내기", BK.Primary); phTest.BackColor = Theme.Bg; phTest.Pt = 10f; phTest.Location = new Point(Zs(22), Zs(278)); phTest.Size = new Size(Zs(160), Zs(36)); cQr.Controls.Add(phTest);
		TButton phNew = new TButton("주소 새로 만들기", BK.Outline); phNew.BackColor = Theme.Bg; phNew.Pt = 10f; phNew.Location = new Point(Zs(192), Zs(278)); phNew.Size = new Size(Zs(140), Zs(36)); cQr.Controls.Add(phNew);
		TLabel phSt = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); phSt.Location = new Point(Zs(344), Zs(278)); phSt.Size = new Size(Zs(CW - 344 - 14), Zs(36)); phSt.V = StringAlignment.Center; cQr.Controls.Add(phSt);
		Action refreshQr = delegate { qr.SetText("ntfy://ntfy.sh/" + phTopic); topicIn.Box.Text = phTopic; };
		refreshQr();
		phNew.Click += delegate { phTopic = Push.NewTopic(); refreshQr(); phSt.Text = "새 주소예요. 폰 앱에서 다시 QR을 스캔해 주세요"; };
		phTest.Click += delegate
		{
			phSt.Text = "보내는 중...";
			Push.Send(phTopic, "\U0001F9EA 초과근무 알림 테스트", "\U0001F409 마룡이가 알려드려요\n휴대폰 알림이 잘 연결됐어요! \u2728", 4, delegate(bool ok, string m)
			{
				try { f.BeginInvoke((MethodInvoker)delegate { phSt.Text = m; }); } catch { }
			});
		};

		// ═════ 기한 ═════ (공문 처리기한 D-day)
		Panel pDe = pages[6];
		head(pDe, "기한", "등록하면 3일 전 · 1일 전 · 당일에 알려주고, 시간을 정하면 1시간 전·30분 전에도 알려줘요");
		Card cDa = card(pDe, 78, 220);
		Lbl(cDa, "새 기한 등록", 11f, 1, Theme.Ink, 18, 12, 300, 22);
		TInput dlTitle = new TInput(); dlTitle.Location = new Point(Zs(18), Zs(34)); dlTitle.Size = new Size(Zs(CW - 36), Zs(36)); cDa.Controls.Add(dlTitle);
		TStepper dlStep = new TStepper(); dlStep.BackColor = Theme.Bg; dlStep.Suffix = "일 뒤"; dlStep.Min = 0; dlStep.Max = 365; dlStep.Value = 3; dlStep.Location = new Point(Zs(18), Zs(80)); dlStep.Size = new Size(Zs(132), Zs(34)); cDa.Controls.Add(dlStep);
		TLabel dlDate = new TLabel("", 11f, 1, Theme.Ink, Theme.Bg); dlDate.Location = new Point(Zs(160), Zs(80)); dlDate.Size = new Size(Zs(190), Zs(34)); dlDate.V = StringAlignment.Center; cDa.Controls.Add(dlDate);
		TButton dlAdd = new TButton("등록", BK.Primary); dlAdd.BackColor = Theme.Bg; dlAdd.Pt = 10f; dlAdd.Location = new Point(Zs(CW - 18 - 100), Zs(80)); dlAdd.Size = new Size(Zs(100), Zs(34)); cDa.Controls.Add(dlAdd);
		TToggle dlTimeOn = new TToggle(); dlTimeOn.BackColor = Theme.Bg; dlTimeOn.Location = new Point(Zs(18), Zs(172)); dlTimeOn.Size = new Size(Zs(34), Zs(20)); cDa.Controls.Add(dlTimeOn);
		TLabel dlTimeLbl = new TLabel("시간도 정하기", 10f, 0, Theme.Sub, Theme.Bg); dlTimeLbl.Location = new Point(Zs(60), Zs(165)); dlTimeLbl.Size = new Size(Zs(120), Zs(34)); dlTimeLbl.V = StringAlignment.Center; cDa.Controls.Add(dlTimeLbl);
		TStepper dlHour = new TStepper(); dlHour.BackColor = Theme.Bg; dlHour.Suffix = "시"; dlHour.Min = 0; dlHour.Max = 23; dlHour.Value = 18; dlHour.Location = new Point(Zs(186), Zs(165)); dlHour.Size = new Size(Zs(112), Zs(34)); dlHour.Visible = false; cDa.Controls.Add(dlHour);
		TStepper dlMin = new TStepper(); dlMin.BackColor = Theme.Bg; dlMin.Suffix = "분"; dlMin.Min = 0; dlMin.Max = 59; dlMin.Value = 0; dlMin.Location = new Point(Zs(306), Zs(165)); dlMin.Size = new Size(Zs(112), Zs(34)); dlMin.Visible = false; cDa.Controls.Add(dlMin);
		string[] qn = { "오늘", "내일", "3일 뒤", "1주 뒤", "2주 뒤", "한 달 뒤" }; int[] qd = { 0, 1, 3, 7, 14, 30 };
		for (int qi = 0; qi < qn.Length; qi++)
		{
			TButton qb = new TButton(qn[qi], BK.Outline); qb.BackColor = Theme.Bg; qb.Pt = 9.5f; qb.Location = new Point(Zs(18 + qi * 82), Zs(124 + 0)); qb.Size = new Size(Zs(76), Zs(28));
			qb.Tag = "frac"; int dv = qd[qi]; qb.Click += delegate { dlStep.Value = dv; }; cDa.Controls.Add(qb);
		}
		TLabel dlMsg = new TLabel("", 9.5f, 0, Theme.Red, Theme.Bg); dlMsg.Location = new Point(Zs(18), Zs(200)); dlMsg.Size = new Size(Zs(CW - 36), Zs(18)); cDa.Controls.Add(dlMsg);
		Card cDl = card(pDe, 310, 220);
		DeadList dlList = new DeadList(); dlList.BackColor = Theme.Bg; dlList.Location = new Point(Zs(18), Zs(10)); dlList.Size = new Size(Zs(CW - 36), Zs(204)); cDl.Controls.Add(dlList);
		Action refreshDead = null;
		refreshDead = delegate { dlList.SetItems(Deadlines.Sorted()); };
		Action refreshDate = delegate { DateTime dd = DateTime.Today.AddDays(dlStep.Value); dlDate.Text = dd.Month + "월 " + dd.Day + "일(" + Records.Dow[(int)dd.DayOfWeek] + ")" + (dlTimeOn.Checked ? " " + dlHour.Value.ToString("00") + ":" + dlMin.Value.ToString("00") : ""); };
		dlTimeOn.CheckedChanged += delegate { dlHour.Visible = dlMin.Visible = dlTimeOn.Checked; refreshDate(); };
		dlHour.ValueChanged += delegate { refreshDate(); }; dlMin.ValueChanged += delegate { refreshDate(); };
		dlStep.ValueChanged += delegate { refreshDate(); };
		refreshDate();
		dlAdd.Click += delegate
		{
			string tt = dlTitle.Box.Text.Trim();
			if (tt.Length == 0) { dlMsg.Text = "공문 제목을 입력해 주세요"; return; }
			DateTime dueAt = DateTime.Today.AddDays(dlStep.Value);
			if (dlTimeOn.Checked) { dueAt = dueAt.AddHours(dlHour.Value).AddMinutes(dlMin.Value); if (dueAt <= DateTime.Now) { dlMsg.Text = "이미 지난 시각이에요. 날짜나 시간을 다시 골라 주세요"; return; } }
			dlMsg.Text = ""; Deadlines.Add(tt, dueAt, dlTimeOn.Checked); dlTitle.Box.Text = ""; refreshDead();
		};
		dlTitle.Box.TextChanged += delegate { if (dlMsg.Text.Length > 0) dlMsg.Text = ""; };
		dlList.Completed += delegate(string id) { Deadline dd = Deadlines.All().Find(x => x.Id == id); if (dd != null) { Deadlines.SetDone(id, !dd.Done); refreshDead(); } };
		dlList.Removed += delegate(string id) { Deadlines.Remove(id); refreshDead(); };
		refreshDead();

		// ═════ 도구 ═════ (공문 도우미 · 예상 수당)
		Panel pTo = pages[7];
		head(pTo, "공문 도우미", "드래그한 글을 단축키로 바로 바꿔 줘요");
		Card cTa = card(pTo, 78, 206);
		Lbl(cTa, "공문 도우미 사용", 11f, 1, Theme.Ink, 18, 10, 300, 22);
		Lbl(cTa, "금액 한글 변환 · 날짜 공문서식 · 특수문자 입력", 10f, 0, Theme.Sub, 18, 32, 380, 20);
		TToggle toTg = new TToggle(); toTg.BackColor = Theme.Bg; toTg.Location = new Point(Zs(CW - 42 - 18), Zs(18)); toTg.SetImmediate(toolsOn); cTa.Controls.Add(toTg);
		Panel tl1 = new Panel(); tl1.BackColor = Theme.Line; tl1.Location = new Point(Zs(18), Zs(58)); tl1.Size = new Size(Zs(CW - 36), 1); cTa.Controls.Add(tl1);
		Lbl(cTa, "자동 붙여넣기", 11f, 1, Theme.Ink, 18, 64, 300, 22);
		Lbl(cTa, "끄면 바꾼 결과를 복사만 해요 (직접 Ctrl+V)", 10f, 0, Theme.Sub, 18, 86, 380, 20);
		TToggle toPaste = new TToggle(); toPaste.BackColor = Theme.Bg; toPaste.Location = new Point(Zs(CW - 42 - 18), Zs(72)); toPaste.SetImmediate(toolsPaste); cTa.Controls.Add(toPaste);
		Panel tl2 = new Panel(); tl2.BackColor = Theme.Line; tl2.Location = new Point(Zs(18), Zs(112)); tl2.Size = new Size(Zs(CW - 36), 1); cTa.Controls.Add(tl2);
		Lbl(cTa, "금액 형식", 11f, 1, Theme.Ink, 18, 120, 140, 34);
		TChooser moneyCh = new TChooser(); moneyCh.BackColor = Theme.Bg; moneyCh.Items = KoText.MoneyFmts; moneyCh.SetImmediate(moneyFmt); moneyCh.Location = new Point(Zs(CW - 18 - 300), Zs(120)); moneyCh.Size = new Size(Zs(300), Zs(34)); cTa.Controls.Add(moneyCh);
		Lbl(cTa, "날짜 형식", 11f, 1, Theme.Ink, 18, 160, 140, 34);
		TChooser dateCh = new TChooser(); dateCh.BackColor = Theme.Bg; dateCh.Items = KoText.DateFmts; dateCh.SetImmediate(dateFmt); dateCh.Location = new Point(Zs(CW - 18 - 300), Zs(160)); dateCh.Size = new Size(Zs(300), Zs(34)); cTa.Controls.Add(dateCh);

		Card cHk = card(pTo, 296, 282);
		Lbl(cHk, "단축키", 11f, 1, Theme.Ink, 18, 8, 64, 26);
		Lbl(cHk, "칸을 누르고 원하는 키 조합을 눌러 주세요 (예: Alt+F1)", 9.5f, 0, Theme.Sub, 86, 8, CW - 86 - 100, 26);
		TButton hkReset = new TButton("기본값", BK.Ghost); hkReset.BackColor = Theme.Bg; hkReset.Pt = 9.5f; hkReset.Location = new Point(Zs(CW - 18 - 72), Zs(8)); hkReset.Size = new Size(Zs(72), Zs(26)); cHk.Controls.Add(hkReset);
		string[] hkNames = { "금액 한글 변환", "날짜 공문서식", "특수문자 팔레트", "법령 바로 찾기", "면적 변환 (㎡↔평)", "개인정보 가리기", "항목부호 자동 정리", "금액 단위 변환", "셀 합계", "화면 캡처 · 플로팅", "기능 찾기 (검색창)" };
		// 새 '금액·합계' 쪽: 금액 단위 변환 · 셀 합계 설정과 단축키
		Panel pTn = pages[10];
		head(pTn, "금액 · 합계", "금액 단위를 바꾸고, 선택한 칸의 숫자를 더해 줘요");
		Card cUn = card(pTn, 78, 212);
		Lbl(cUn, "금액 단위 변환", 11f, 1, Theme.Ink, 18, 8, 300, 22);
		Lbl(cUn, "드래그한 금액을 고른 단위로 바꿔요 (예: 1,000,000원 → 1,000천원)", 10f, 0, Theme.Sub, 18, 30, CW - 36, 20);
		Lbl(cUn, "바꿀 단위", 11f, 1, Theme.Ink, 18, 62, 160, 34);
		TChooser unTo = new TChooser(); unTo.BackColor = Theme.Bg; unTo.Items = AdminText.Units; unTo.SetImmediate(moneyUnitTo); unTo.Location = new Point(Zs(CW - 18 - 200), Zs(62)); unTo.Size = new Size(Zs(200), Zs(34)); cUn.Controls.Add(unTo);
		Lbl(cUn, "단위 없는 숫자", 11f, 1, Theme.Ink, 18, 102, 160, 34);
		TChooser unBare = new TChooser(); unBare.BackColor = Theme.Bg; unBare.Items = AdminText.Units; unBare.SetImmediate(moneyUnitBare); unBare.Location = new Point(Zs(CW - 18 - 200), Zs(102)); unBare.Size = new Size(Zs(200), Zs(34)); cUn.Controls.Add(unBare);
		Lbl(cUn, "표의 숫자만 골랐을 때 이 단위로 보고 바꿔요", 9.5f, 0, Theme.Muted, 18, 140, CW - 36, 18);
		Card cSu = card(pTn, 300, 180);
		Lbl(cSu, "셀 합계", 11f, 1, Theme.Ink, 18, 8, 300, 22);
		Lbl(cSu, "한글·엑셀 표에서 칸을 드래그로 고르고 누르면 합계를 알려 줘요", 10f, 0, Theme.Sub, 18, 30, CW - 36, 20);
		Lbl(cSu, "누적해서 더하기", 11f, 1, Theme.Ink, 18, 62, 300, 22);
		Lbl(cSu, "떨어진 칸도 하나씩 골라 누르면 2분 안에는 계속 더해 가요", 10f, 0, Theme.Sub, 18, 84, CW - 100, 20);
		TToggle sumTg = new TToggle(); sumTg.BackColor = Theme.Bg; sumTg.Location = new Point(Zs(CW - 42 - 18), Zs(70)); sumTg.SetImmediate(sumAcc); cSu.Controls.Add(sumTg);
		Lbl(cSu, "합계는 클립보드에 복사돼요 (원하는 곳에 Ctrl+V) · 음수는 △ ▲ − ( ) 표시를 알아봐요", 9.5f, 0, Theme.Muted, 18, 116, CW - 36, 18);
		TKeyBox[] hkBox = new TKeyBox[NHK]; TLabel[] hkSt = new TLabel[NHK];
		for (int hi = 0; hi < NHK; hi++)
		{
			bool inNew = hi == 7 || hi == 8; Card host = inNew ? (hi == 7 ? cUn : cSu) : cHk; int yy = inNew ? (hi == 7 ? 164 : 140) : 37 + (hi == 9 ? 7 : hi == 10 ? 8 : hi) * 27;
			Lbl(host, hkNames[hi], 10.5f, 1, Theme.Ink, 18, yy, 170, inNew ? 30 : 26);
			hkBox[hi] = new TKeyBox(); hkBox[hi].BackColor = Theme.Bg; hkBox[hi].Location = new Point(Zs(190), Zs(yy)); hkBox[hi].Size = new Size(Zs(170), Zs(inNew ? 30 : 25)); hkBox[hi].Set(hkMod[hi], hkVk[hi]); host.Controls.Add(hkBox[hi]);
			hkSt[hi] = Lbl(host, hotStatus[hi], 9.5f, 0, hotStatus[hi] == "사용 중" || hotStatus[hi] == "" ? Theme.Muted : Theme.Red, 372, yy, CW - 372 - 12, inNew ? 30 : 26);
		}
		Action<int> hkChanged = delegate(int which)
		{
			for (int o = 0; o < NHK; o++)
				if (o != which && hkBox[o].Mod == hkBox[which].Mod && hkBox[o].Vk == hkBox[which].Vk) { hkSt[which].Text = "다른 기능과 겹쳐요"; hkSt[which].Fore = Theme.Red; return; }
			hkSt[which].Text = "저장하면 적용돼요"; hkSt[which].Fore = Theme.Muted;
		};
		for (int hi = 0; hi < NHK; hi++) { int w = hi; hkBox[hi].Changed += delegate { hkChanged(w); }; }
		hkReset.Click += delegate { for (int hi = 0; hi < NHK; hi++) { hkBox[hi].Set(1, HkDefault[hi]); hkSt[hi].Text = "바로 적용돼요"; hkSt[hi].Fore = Theme.Muted; } };

		Panel pPa = pages[8];
		head(pPa, "수당", "이번 달 예상 시간외근무수당을 계산해요 (공무원수당 등에 관한 규정 기준)");
		Card cPy = card(pPa, 78, 196);
		Lbl(cPy, "예상 시간외근무수당", 11f, 1, Theme.Ink, 18, 8, 180, 34);
		TChooser gradeCh = new TChooser(); gradeCh.BackColor = Theme.Bg; gradeCh.Location = new Point(Zs(CW - 18 - 320), Zs(8)); gradeCh.Size = new Size(Zs(320), Zs(34));
		string[] gl = new string[Pay.Grades.Length]; for (int gi = 0; gi < gl.Length; gi++) gl[gi] = Pay.Grades[gi].Label;
		gradeCh.Items = gl; int gStart = 2; for (int gi = 0; gi < gl.Length; gi++) if (Pay.Grades[gi].Id == payGradeId) gStart = gi; gradeCh.SetImmediate(gStart); cPy.Controls.Add(gradeCh);
		Lbl(cPy, "기준 월급(호봉) · 비율", 11f, 1, Theme.Ink, 18, 50, 180, 34);
		TInput baseIn = new TInput(); baseIn.Location = new Point(Zs(CW - 18 - 320), Zs(50)); baseIn.Size = new Size(Zs(150), Zs(34)); cPy.Controls.Add(baseIn);
		TChooser rateCh = new TChooser(); rateCh.BackColor = Theme.Bg; rateCh.Items = new string[] { "55%", "60%" }; rateCh.Location = new Point(Zs(CW - 18 - 150), Zs(50)); rateCh.Size = new Size(Zs(150), Zs(34)); cPy.Controls.Add(rateCh);
		Panel tl3 = new Panel(); tl3.BackColor = Theme.Line; tl3.Location = new Point(Zs(18), Zs(98)); tl3.Size = new Size(Zs(CW - 36), 1); cPy.Controls.Add(tl3);
		TLabel payRes = new TLabel("", 10.5f, 1, Theme.Ink, Theme.Bg); payRes.Wrap = true; payRes.Location = new Point(Zs(18), Zs(104)); payRes.Size = new Size(Zs(CW - 36), Zs(46)); cPy.Controls.Add(payRes);
		TLabel payNote = new TLabel("", 9f, 0, Theme.Muted, Theme.Bg); payNote.Wrap = true; payNote.Location = new Point(Zs(18), Zs(152)); payNote.Size = new Size(Zs(CW - 36), Zs(40)); cPy.Controls.Add(payNote);
		Action refreshPay = null;
		baseIn.Box.TextChanged += delegate { if (refreshPay != null && baseIn.Box.Focused) refreshPay(); };
		Action syncPayFields = delegate
		{
			PayGrade pg = Pay.Grades[gradeCh.Index]; bool custom = pg.Id == "xx";
			baseIn.Box.ReadOnly = !custom; rateCh.Enabled = custom;
			if (!custom) { baseIn.Box.Text = pg.Fixed > 0 ? "시간당 " + pg.Fixed.ToString("N0") + "원 고정" : pg.Base.ToString("N0"); rateCh.SetImmediate(pg.Rate == 60 ? 1 : 0); }
			else { baseIn.Box.Text = payBase > 0 ? payBase.ToString("N0") : ""; rateCh.SetImmediate(payRate == 60 ? 1 : 0); }
		};
		refreshPay = delegate
		{
			PayGrade pg = Pay.Grades[gradeCh.Index];
			int b = pg.Base, rt = pg.Rate;
			if (pg.Id == "xx") { int v; string digits = baseIn.Box.Text.Replace(",", "").Trim(); b = int.TryParse(digits, out v) ? v : 0; rt = rateCh.Index == 1 ? 60 : 55; }
			DateTime n = DateTime.Now; Pay.Result pr = Pay.Month(n.Year, n.Month, b, rt, false, pg.Fixed);
			if (b <= 0 && pg.Fixed <= 0) { payRes.Text = "기준호봉 봉급액을 입력하면 이번 달 예상 수당을 계산해요"; payRes.Fore = Theme.Sub; }
			else
			{
				payRes.Fore = Theme.Ink;
				payRes.Text = n.Month + "월 수당 계산 시간 " + pr.Counted + "시간 (기록 " + pr.RawHours + "시간) + 기본 10시간 = " + pr.Paid + "시간  ·  시간당 " + pr.Unit.ToString("N0") + "원\n예상 수당 약 " + pr.Amount.ToString("N0") + "원" + (pr.Capped ? "  (월 최대 57시간까지만 계산)" : "");
			}
			payNote.Text = pg.Verified || pg.Id == "xx" ? "평일은 하루 1시간을 빼고, 토요일·공휴일은 전부 계산해요 · 세금 떼기 전 금액 · 참고용 예상치예요" : "※ 이 직급의 기준호봉은 확인되지 않아 상당 계급 값으로 근사했어요. 정확한 금액은 \"직접 입력\"에 기준호봉 봉급액을 넣어 보세요";
		};
		gradeCh.IndexChanged += delegate { syncPayFields(); refreshPay(); };
		rateCh.IndexChanged += delegate { refreshPay(); };
		syncPayFields(); refreshPay();

		// ── 하단 바 ──
		// ═════ 사용법 ═════ (처음 쓰는 분도 순서대로 따라 하면 되게)
		Panel pGu = pages[9];
		head(pGu, "사용법", "처음이라면 위에서부터 차례로 읽어 보세요");
		string[][] gs = {
			new string[] { "프로그램은 화면 오른쪽 아래에 있어요", "작업표시줄의 ^ 를 누르면 시계 모양 아이콘이 보여요. 창을 닫아도 꺼지지 않아요." },
			new string[] { "정해 둔 시간이 되면 팝업이 떠요", "초과근무를 했다면 큰 '확인했어요' 버튼을 누르세요. (Enter 키도 돼요)" },
			new string[] { "지금 못 누르겠다면 '나중에 다시 알림'", "'5분 뒤·10분 뒤·15분 뒤' 중 하나를 누르면 그때 다시 알려 줘요." },
			new string[] { "기록은 저절로 쌓여요", "누른 시간이 기록돼요. 왼쪽 '기록'에서 달력으로 보고, '업무 → 수당'에서 예상 수당을 봐요." },
			new string[] { "완전히 끄고 싶을 때", "아이콘을 우클릭(마우스 오른쪽 버튼) → '종료'. 창의 X는 창만 닫아요." }
		};
		for (int gi = 0; gi < gs.Length; gi++)
		{
			GuideCard gc = new GuideCard(); gc.Num = (gi + 1).ToString(); gc.Title = gs[gi][0]; gc.Body = gs[gi][1];
			gc.Location = new Point(Zs(PX), Zs(78 + gi * 84)); gc.Size = new Size(Zs(CW), Zs(80)); pGu.Controls.Add(gc);
		}
		TButton gTest = new TButton("알림 미리 보기", BK.Outline); gTest.BackColor = Theme.Bg; gTest.Pt = 10.5f; gTest.Location = new Point(Zs(PX), Zs(500)); gTest.Size = new Size(Zs(150), Zs(36));
		gTest.Click += delegate { try { Notify(DateTime.Now, true); } catch (Exception ex) { LogError(ex); } };
		TButton gBig = new TButton(Program.BigUi ? "글자 보통 크기로" : "글자·화면 크게", BK.Outline); gBig.BackColor = Theme.Bg; gBig.Pt = 10.5f; gBig.Location = new Point(Zs(PX + 160), Zs(500)); gBig.Size = new Size(Zs(170), Zs(36));
		gBig.Click += delegate
		{
			if (MessageBox.Show(f, (Program.BigUi ? "글자를 보통 크기로 되돌릴까요?" : "글자와 화면을 크게 바꿀까요?") + "\n프로그램이 잠깐 꺼졌다가 다시 켜져요.", "초과근무 알리미", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) Program.ToggleBigUi();
		};
		TButton gMan = new TButton("그림으로 보기", BK.Primary); gMan.BackColor = Theme.Bg; gMan.Pt = 10.5f; gMan.Location = new Point(Zs(PX), Zs(500)); gMan.Size = new Size(Zs(150), Zs(36));
		gMan.Click += delegate { ShowManual(); };
		gTest.Location = new Point(Zs(PX + 160), Zs(500)); gTest.Size = new Size(Zs(140), Zs(36));
		gBig.Location = new Point(Zs(PX + 310), Zs(500)); gBig.Size = new Size(Zs(CW - 310), Zs(36));
		pGu.Controls.Add(gMan);
		pGu.Controls.Add(gBig);
		pGu.Controls.Add(gTest);

		FootPanel foot = new FootPanel(); foot.Location = new Point(Zs(SIDE), Zs(HH - FOOT)); foot.Size = new Size(Zs(PW), Zs(FOOT));
		TButton saveBtn = new TButton("저장", BK.Primary); saveBtn.BackColor = Theme.Bg; saveBtn.Size = new Size(Zs(96), Zs(38)); saveBtn.Location = new Point(Zs(PW - PX - 96 - 8 - 84), Zs(11));
		TButton closeB = new TButton("닫기", BK.Outline); closeB.BackColor = Theme.Bg; closeB.Size = new Size(Zs(84), Zs(38)); closeB.Location = new Point(Zs(PW - PX - 84), Zs(11));
		TLabel foot1 = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); foot1.Location = new Point(Zs(PX), Zs(11)); foot1.Size = new Size(Zs(320), Zs(38)); foot1.V = StringAlignment.Center;
		foot.Controls.AddRange(new Control[] { foot1, saveBtn, closeB });

		for (int gq = 0; gq < navGroup.Length; gq++)
		{
			if (navGroup[gq].Length < 2) continue;
			for (int sq = 0; sq < navGroup[gq].Length; sq++)
			{
				TSeg seg = new TSeg(subNames[gq]); seg.BackColor = Theme.Bg; seg.SnapIndex(sq);
				seg.Location = new Point(Zs(PW - PX) - seg.Width, Zs(8)); seg.Tag = "right";
				int[] grp = navGroup[gq];
				seg.Changed += delegate(object so, EventArgs se) { int target = grp[((TSeg)so).Index]; if (settingsSub != null) { int from = 0; for (int gk2 = 0; gk2 < grp.Length; gk2++) if (pages[grp[gk2]].Visible) from = grp[gk2]; settingsSub(from, target); } else if (settingsGo != null) settingsGo(target); };
				pages[navGroup[gq][sq]].Controls.Add(seg); seg.BringToFront();
			}
		}
		tCard.Tag = "frac"; rCard.Tag = "frac"; rPrev.Tag = "right"; rMonth.Tag = "right"; rNext.Tag = "right";
		for (int q = 0; q < hb.Length; q++) hb[q].Tag = "frac";
		BM("쪽 10개 만들기");
		for (int q = 0; q < 11; q++) Fluid.Attach(pages[q]);
		// 카드 밖에 직접 놓인 것들은 '바탕색'으로 (카드는 흰색으로 떠 보이게)
		Action<Control> toPage = null;
		toPage = delegate(Control pc)
		{
			foreach (Control c in pc.Controls)
			{
				if (c is Card) continue;
				if (c.BackColor == Theme.Bg) c.BackColor = Theme.Page;
				if (c is Panel) toPage(c);
			}
		};
		for (int q = 0; q < 11; q++) toPage(pages[q]);
		toPage(foot);
		BM("색 정리");
		f.Controls.AddRange(new Control[] { side, top, pages[0], pages[1], pages[2], pages[3], pages[4], pages[5], pages[6], pages[7], pages[8], pages[9], pages[10], foot });

		BM("폼에 붙이기");
		// ── 반응형 배치 ──
		Action pillNow = null;
		ToolTip navTip = new ToolTip(); navTip.Active = false;
		for (int i = 0; i < 7; i++) navTip.SetToolTip(nav[i], navNames[i]);
		Action relayout = delegate
		{
			int W = f.ClientSize.Width, H = f.ClientSize.Height;
			if (W < 100 || H < 100) return;
			bool compact = W < Zs(780);   // 좁아져도 글자는 그대로 (아이콘만 남기지 않아요)
			int sw = compact ? Zs(168) : Zs(SIDE);
			side.SetBounds(0, 0, sw, H);
			mp.Left = Zs(16);
			foreach (Control sc in side.Controls) if (sc is TLabel && sc.Left == Zs(62)) sc.Width = sw - Zs(62) - Zs(4);
			thSeg.Width = sw - Zs(24); thSeg.Visible = true; thDot.Visible = false;
			for (int i = 0; i < 7; i++)
			{
				nav[i].Compact = false; navTip.Active = false;
				nav[i].SetBounds(Zs(10), Zs(84 + i * 40), sw - Zs(20), Zs(38));
			}
			int avail = W - sw, pw = Math.Min(avail, Zs(780)), px0 = (avail - pw) / 2;
			top.SetBounds(sw, 0, avail, Zs(TOP));
			{ int wb = Zs(36), gp = Zs(4), ty = (Zs(TOP) - bClose.Height) / 2; bClose.SetBounds(avail - Zs(10) - wb, ty, wb, bClose.Height); bMax.SetBounds(bClose.Left - gp - wb, ty, wb, bClose.Height); bMin.SetBounds(bMax.Left - gp - wb, ty, wb, bClose.Height); }
			bMax.Restore = f.WindowState == FormWindowState.Maximized;
			for (int i = 0; i < 11; i++) pages[i].SetBounds(sw + px0, Zs(TOP), pw, H - Zs(TOP) - Zs(FOOT));
			foot.SetBounds(sw, H - Zs(FOOT), avail, Zs(FOOT));
			int cr = px0 + pw - Zs(PX);
			closeB.Left = cr - closeB.Width; saveBtn.Left = closeB.Left - Zs(8) - saveBtn.Width;
			foot1.Left = px0 + Zs(PX);
			bool max = f.WindowState == FormWindowState.Maximized;
			verLbl.Top = H - Zs(44); copyLbl.Top = H - Zs(24); thCap.Top = H - Zs(98); thSeg.Top = H - Zs(78);
			side.Invalidate(); f.Invalidate();
		};
		f.Resize += delegate { relayout(); if (pillNow != null) pillNow(); };
		relayout();

		BM("배치");
		// ── 동작 ──
		string footDefault = "바꾼 내용은 자동으로 저장돼요";
		bool dirty = false, invalid = false;
		Action<int> go = null;
		int curPage = -1;
		AV pillY = new AV(null, 0) { Rate = 20f };   // 주인을 두지 않아요: 알약이 움직일 때 사이드바 전체를 다시 그리지 않으려고
		Action syncPill = delegate
		{
			{ int i = grpOf(curPage); RectangleF oldP = side.Pill; side.Pill = new RectangleF(nav[i].Left, pillY.V + 1, nav[i].Width - 1, nav[i].Height - 2); RectangleF both = RectangleF.Union(oldP, side.Pill); foreach (NavItem ni in nav) if (both.IntersectsWith(new RectangleF(ni.Left, ni.Top, ni.Width, ni.Height))) ni.Invalidate(); }   // 알약이 걸친 메뉴만 다시 그려요 (전부 다시 그리면 프레임마다 40ms 넘게 걸렸어요)
		};
		pillY.Step = syncPill;
		pillNow = delegate { pillY.Snap(nav[grpOf(curPage)].Top); };
		TransHost th = null; Tw vsTw = null; int goSeq = 0;
		// 쪽 전환·테마 변경에 쓸 창 사진을 가만히 있을 때 미리 찍어 둬요 (클릭 순간에 15~25ms 걸리던 사진 찍기를 없애려고, 다른 스레드에서 찍어요)
		Bitmap cachedShot = null; Rectangle cachedRect = Rectangle.Empty; long cachedShotAt = 0, lastActionAt = 0, prevActionAt = 0; bool shotBusy = false; int contentTick = 0, cachedTick = -1;   // contentTick: 시계·남은 시간처럼 저절로 바뀌는 글자가 갱신될 때마다 올라가요 (미리 찍어 둔 사진이 옛 숫자를 보여주지 않게)
		PageShotInput.Hook(delegate(int wm) { prevActionAt = lastActionAt; lastActionAt = Stopwatch.GetTimestamp(); });
		Func<Rectangle, Bitmap> takeShot = delegate(Rectangle r)
		{
			// 클릭 '전'에 찍은 사진만 써요 (그 사이에 화면 내용이 바뀌었으면 쓰지 않아요)
			if (cachedShot == null || cachedShotAt <= prevActionAt || cachedTick != contentTick || cachedRect != f.Bounds || !cachedRect.Contains(r)) return null;
			Bitmap b = cachedShot.Clone(new Rectangle(r.X - cachedRect.X, r.Y - cachedRect.Y, r.Width, r.Height), cachedShot.PixelFormat);
			cachedShot.Dispose(); cachedShot = null;   // 이제 화면이 바뀌므로 다시 찍어야 해요
			return b;
		};
		shotProvider = takeShot;
		System.Windows.Forms.Timer shotTm = new System.Windows.Forms.Timer(); shotTm.Interval = 150;
		shotTm.Tick += delegate
		{
			try
			{
				if (f.IsDisposed) { shotTm.Stop(); return; }
				if (shotBusy || !f.Visible || f.WindowState != FormWindowState.Normal || (Form.ActiveForm != f && Environment.GetEnvironmentVariable("OT_BENCH") != "1") || (th != null && th.Visible)) return;
				long now = Stopwatch.GetTimestamp();
				if (now - lastActionAt < Stopwatch.Frequency * 45 / 100) return;
				Rectangle fb = f.Bounds;
				if (cachedShot != null && cachedRect == fb && cachedShotAt > lastActionAt && cachedTick == contentTick) return;   // 마지막 조작 뒤에 이미 찍어 뒀어요
				shotBusy = true; long startedAt = now; int startedTick = contentTick;
				System.Threading.ThreadPool.QueueUserWorkItem(delegate
				{
					Bitmap nb = null;
					try { nb = Grab.Screen(fb); } catch { }
					try
					{
						f.BeginInvoke((MethodInvoker)delegate
						{
							shotBusy = false;
							if (nb == null) return;
							if (f.IsDisposed || lastActionAt > startedAt || startedTick != contentTick || f.Bounds != fb || (th != null && th.Visible)) { nb.Dispose(); return; }
							if (cachedShot != null) cachedShot.Dispose();
							cachedShot = nb; cachedRect = fb; cachedShotAt = startedAt; cachedTick = startedTick;
						});
					}
					catch { if (nb != null) nb.Dispose(); }
				});
			}
			catch { shotBusy = false; }
		};
		shotTm.Start();
		f.FormClosed += delegate { shotTm.Stop(); shotProvider = null; if (cachedShot != null) { cachedShot.Dispose(); cachedShot = null; } };
		bool skipUpd = false;
		Action<int> swap = delegate(int idx)
		{
			for (int i = 0; i < 11; i++) pages[i].Visible = i == idx;
			saveBtn.Visible = false;
			if (!dirty && !invalid) { foot1.Fore = Theme.Sub; foot1.Text = footDefault; }
			if (idx == 0 && !skipUpd) sky.Restart();   // 전환 중에는 덮개 아래라 안 보이니, 끝난 뒤에 시작해요
			if (idx == 2) refreshRec();
			if (idx == 6 && refreshDead != null) refreshDead();
			if (idx == 8 && refreshPay != null) refreshPay();
			if (!skipUpd) pages[idx].Update();
		};
		go = delegate(int idx)
		{
			Anim.Mark("쪽 이동 " + idx);
			Stopwatch goSw = Stopwatch.StartNew();
			bool animate = !LowPower && curPage >= 0 && curPage != idx && f.Visible && f.WindowState != FormWindowState.Minimized;
			curPage = idx;
			lastSub[grpOf(idx)] = idx;
			if (!animate)
			{
				{ int gi = grpOf(idx); pillY.Snap(nav[gi].Top); }
				for (int i = 0; i < 7; i++) nav[i].SnapSelected(i == grpOf(idx));
				swap(idx); return;
			}
			// 부드러운 전환: 앱 창 안에서 옛 화면 사진을 먼저 덮고 → 뒤에서 쪽을 바꿔 새 화면을 그려 둔 뒤 → 새 화면을 미끄러지듯 겹쳐 보여요 (별도 창이 없어서 번쩍이지 않아요)
			try
			{
				if (th == null) { th = new TransHost(); f.Controls.Add(th); }
				if (vsTw != null) vsTw.Cancel();
				BM("go시작"); Rectangle pr = pages[idx].Bounds, sr = f.RectangleToScreen(pr);
				Bitmap oldB = takeShot(sr); BM(oldB != null ? "캐시사진" : "사진없음"); if (oldB == null) oldB = Grab.Screen(sr); BM("옛사진준비");
				th.Cover(pr, oldB); BM("덮개표시");
					{ int gi = grpOf(idx); pillY.Set(nav[gi].Top); }   // 메뉴 알약·선택 표시는 무거운 일(새 화면 그리기) 전에 먼저 움직여서 누르자마자 반응이 보여요
					for (int i = 0; i < 7; i++) nav[i].SetSelected(i == grpOf(idx)); BM("알약·메뉴시작");
				int seq = ++goSeq;
				f.BeginInvoke((MethodInvoker)delegate
				{
					try
					{
						if (seq != goSeq || f.IsDisposed) return;
						BM("BeginInvoke시작"); skipUpd = true; try { swap(idx); } finally { skipUpd = false; } BM("쪽바꿈");
						Bitmap nb = new Bitmap(pr.Width, pr.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
						BM("비트맵생성"); FastPrint.Print(pages[idx], nb); BM("새화면그림");
						Anim.Mark("쪽 이동: 새 화면 준비 완료");
						th.Begin(nb, Zs(18)); BM("Begin(변환)");
						NoteSlow((int)goSw.ElapsedMilliseconds, 90);
						vsTw = Anim.Run(420, Ease.InOutSine, delegate(float t) { if (th.IsDisposed) return; if (t > 0.8f) BM("f" + t.ToString("F2") + "시작"); th.T = t; th.Invalidate(); th.Update(); if (t > 0.8f) BM("끝"); }, delegate { if (seq == goSeq && !th.IsDisposed) { BM("끝처리시작"); pages[idx].Update(); BM("끝-페이지그림"); th.ReleaseTo(pages[idx]); if (idx == 0) sky.Restart(); BM("끝-덮개제거"); } });
					}
					catch (Exception ex2) { LogError(ex2); if (th != null) th.Release(); }
				});
			}
			catch (Exception ex) { LogError(ex); swap(idx); }
		};
			// 메뉴는 마우스를 누르는 순간 바로 바뀌어요 (손을 뗄 때까지 기다리지 않아서 반응이 빨라요). 키보드로 누르면 Click 이 처리해요
			bool navDown = false;
			for (int i = 0; i < 7; i++) { int gk = i; nav[i].MouseDown += delegate(object o, MouseEventArgs me) { if (me.Button == MouseButtons.Left) { navDown = true; go(lastSub[gk]); } }; nav[i].Click += delegate { if (navDown) { navDown = false; return; } go(lastSub[gk]); }; }
		settingsGo = go;
		settingsSub = delegate(int from, int to)
		{
			if (from == to) return;
			// 같은 메뉴 안이라 쪽 사진을 덮는 전환 없이 곧바로 바꾸고, 새 쪽의 알약은 옛 자리에서 출발해 미끄러져요
			if (vsTw != null) vsTw.Cancel(); goSeq++; if (th != null) th.Release();
			curPage = to; lastSub[grpOf(to)] = to;
			foreach (Control c in pages[to].Controls) { TSeg sg = c as TSeg; if (sg != null && "right".Equals(sg.Tag)) { int ni = sg.Index; int oi = 0; int[] gp2 = navGroup[grpOf(to)]; oi = Array.IndexOf(gp2, from); sg.SnapIndex(oi < 0 ? ni : oi); swap(to); sg.Slide(ni); } }
			foreach (Control c in pages[from].Controls) { TSeg sg = c as TSeg; if (sg != null && "right".Equals(sg.Tag)) sg.SnapIndex(Array.IndexOf(navGroup[grpOf(from)], from)); }   // 떠나는 쪽의 알약은 제자리로 (나중에 돌아와도 어긋나지 않게)
		};
		Action<int> goQuick = delegate(int idx) { if (th != null) th.Release(); curPage = idx; lastSub[grpOf(idx)] = idx; { int gi = grpOf(idx); pillY.Snap(nav[gi].Top); } for (int i = 0; i < 7; i++) nav[i].SnapSelected(i == grpOf(idx)); swap(idx); };
			// 화면 색 전환도 같은 방식: 옛 화면 사진을 덮고 → 색을 갈아 끼운 새 화면을 그려 두고 → 서서히 겹쳐요
			Func<Bitmap> renderForm = delegate
			{
				Bitmap b = new Bitmap(f.ClientSize.Width, f.ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
				using (Graphics g2 = Graphics.FromImage(b))
				{
					g2.Clear(f.BackColor);
					for (int i = f.Controls.Count - 1; i >= 0; i--)
					{
						Control c = f.Controls[i]; if (c == th || !c.Visible || c.Width <= 0 || c.Height <= 0) continue;
						using (Bitmap tmp = new Bitmap(c.Width, c.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) { c.DrawToBitmap(tmp, new Rectangle(0, 0, c.Width, c.Height)); g2.DrawImage(tmp, new Rectangle(c.Left, c.Top, c.Width, c.Height), 0, 0, c.Width, c.Height, GraphicsUnit.Pixel); }
					}
				}
				return b;
			};
			themeTrans = delegate(int mode)
			{
				if (th == null) { th = new TransHost(); f.Controls.Add(th); }
				if (vsTw != null) vsTw.Cancel();
				Rectangle cr = f.ClientRectangle, sr = f.RectangleToScreen(cr);
				Bitmap oldB = takeShot(sr); if (oldB == null) oldB = Grab.Screen(sr);
				th.Cover(cr, oldB);
				int seq = ++goSeq;
				f.BeginInvoke((MethodInvoker)delegate
				{
					try
					{
						if (seq != goSeq || f.IsDisposed) return;
						Stopwatch thSw = Stopwatch.StartNew();
						ThemeFx.Apply(f, mode); SaveSettings();
						f.Invalidate(true); f.Update();
						th.BackColor = Theme.Page;
						Bitmap nb = renderForm();
						NoteSlow((int)thSw.ElapsedMilliseconds + 20, 140);
						th.Begin(nb, 0);
						vsTw = Anim.Run(520, Ease.InOutSine, delegate(float t) { if (th.IsDisposed) return; if (t > 0.8f) BM("f" + t.ToString("F2") + "시작"); th.T = t; th.Invalidate(); th.Update(); if (t > 0.8f) BM("끝"); }, delegate { if (seq == goSeq && !th.IsDisposed) { f.Update(); th.Release(); Mem.Later(3000); } });
					}
					catch (Exception ex3) { LogError(ex3); if (th != null) th.Release(); }
				});
			};
		// 창이 뜬 뒤 한가할 때 다른 쪽의 화면 부품(창 핸들)을 미리 만들어 둬요 → 처음 그 쪽으로 갈 때 멈칫하지 않아요
		f.Shown += delegate
		{
			Queue<int> pre = new Queue<int>(); for (int i = 0; i < 11; i++) if (i != curPage) pre.Enqueue(i);
			System.Windows.Forms.Timer pt = new System.Windows.Forms.Timer(); pt.Interval = 90;
			pt.Tick += delegate
			{
				try
				{
					if (f.IsDisposed || pre.Count == 0) { pt.Stop(); pt.Dispose(); return; }
					int pi = pre.Dequeue();
					if (!pages[pi].Visible) { Action<Control> mk = null; mk = delegate(Control c) { IntPtr hh = c.Handle; foreach (Control k in c.Controls) mk(k); }; mk(pages[pi]); }
				}
				catch { pt.Stop(); pt.Dispose(); }
			};
			System.Windows.Forms.Timer st = new System.Windows.Forms.Timer(); st.Interval = 700; st.Tick += delegate { st.Stop(); st.Dispose(); pt.Start(); }; st.Start();
		};
		pRe.Resize += delegate { if (pRe.Visible && refreshRec != null) refreshRec(); };
		EventHandler toRec = delegate { go(2); };
		mCard.Click += toRec; foreach (Control mc in mCard.Controls) mc.Click += toRec;

		bool busy = false;
		string lastNext = null;
		Action refreshHome = null;
		refreshHome = delegate
		{
			contentTick++; DateTime n = DateTime.Now;
			sky.TimeText = n.ToString("HH:mm:ss");
			sky.DateText = n.Month + "월 " + n.Day + "일 " + Records.Dow[(int)n.DayOfWeek] + "요일";
			DateTime na = NextAlertAt();
			if (na == DateTime.MinValue) { hTime.Text = "--:--"; hRel.Text = "예정 없음"; hDate.Text = ""; }
			else
			{
				hTime.Text = na.ToString("HH:mm");
				hRel.Text = RelText(na - n);
				string dn = na.Date == n.Date ? "오늘" : (na.Date == n.Date.AddDays(1) ? "내일" : na.Month + "월 " + na.Day + "일");
				hDate.Text = dn + " " + Records.Dow[(int)na.DayOfWeek] + "요일" + (snoozeSlot != null && na == snoozeAt ? " · 다시 알림" : "") + (DndActive ? " · 방해 금지 중" : "");
			}
			string nx = n.ToString("yyyyMMddHHmm");
			if (nx != lastNext)
			{
				lastNext = nx;
				int mon = Records.CountMonth(n.Year, n.Month), lim = monthlyLimit;
				double fr = lim > 0 ? (double)mon / lim : 0;
				Color gc = fr >= 1.0 ? Theme.Red : (fr >= 0.75 ? Theme.Amber : Theme.Accent);
				mVal.Text = mon + "시간"; mLim.Text = "/ " + lim + "시간";
				mPct.Text = (int)Math.Round(fr * 100) + "%"; mPct.Fore = gc;
				mBar.Frac = fr; mBar.C = gc; mBar.Invalidate();
				mHint.Top = Zs(78);
				int yh0 = Records.CountYear(n.Year);
				int monLeft = lim - mon, yrLeft = YearLimit - yh0;
				string hintTxt = yrLeft < monLeft || yrLeft <= 0
					? (yrLeft <= 0 ? (yrLeft == 0 ? "연 한도를 모두 채웠어요" : "연 한도를 " + (-yrLeft) + "시간 넘었어요") : "연 한도까지 " + yrLeft + "시간 남았어요")
					: (mon >= lim ? (mon == lim ? "한도를 모두 채웠어요" : "한도를 " + (mon - lim) + "시간 넘었어요") : "한도까지 " + (lim - mon) + "시간 남았어요");
				mHint.Text = hintTxt + PayHint();
				mHint.Fore = (fr >= 1.0 || yrLeft <= 0) ? Theme.Red : Theme.Sub;
				{
					int yh = Records.CountYear(n.Year); double yf = (double)yh / YearLimit;
					Color yc = yf >= 1.0 ? Theme.Red : (yf >= 0.9 ? Theme.Amber : Theme.Accent);
					yCap.Text = "올해 " + yh + " / " + YearLimit + "시간 (연 한도)"; yPct.Text = (int)Math.Round(yf * 100) + "%"; yPct.Fore = yc;
					yBar.Frac = Math.Min(1.0, yf); yBar.C = yc; yBar.Invalidate();
				}
				tVal.Text = Records.CountOn(n) + "시간";
			}
			{
				string cc, vv = CountdownText(n, out cc);
				if (menuShownAt == DateTime.MinValue || (n - menuShownAt).TotalSeconds > 20) { menuShownAt = DateTime.MinValue; if (rCap.Text != cc) rCap.Text = cc; }
				if (rVal.Text != vv) rVal.Text = vv;
			}
			if (!sky.LiveClock || !sky.Fixed60) sky.Invalidate();   // 시계는 매 프레임 직접 그리니 따로 다시 그리기를 걸지 않아요 (같은 화면 갱신에 두 번 그리면 한 장이 사라져요)
		};
		lastNextReset = delegate { lastNext = null; };
		Action showSel = delegate
		{
			busy = true;
			DateTime d = cal.Selected.Date;
			string nm;
			bool h = HolidayName(d, out nm);
			selTitle.Text = d.Month + "월 " + d.Day + "일 " + Records.Dow[(int)d.DayOfWeek] + "요일";
			if (IsWeekend(d)) { selSub.Text = "주말 · 항상 매시간 알려요"; holTg.Enabled = false; holTg.SetImmediate(true); }
			else { selSub.Text = h ? nm : "평일"; holTg.Enabled = true; holTg.SetImmediate(h); }
			busy = false;
		};
		Action showSync = delegate
		{
			syncSt.Text = syncStatus + ((lastSync.Length <= 0) ? "\r\n인증키가 없으면 내장 계산값(양력·음력·대체공휴일)만 사용해요." : ("\r\n마지막 성공: " + lastSync));
		};
		holTg.CheckedChanged += delegate
		{
			if (busy) return;
			DateTime date = cal.Selected.Date;
			string name;
			bool isBase = BaseHoliday(date, out name);
			if (holTg.Checked) { if (isBase) userRemove.Remove(date); else userAdd[date] = "사용자 지정"; }
			else { userAdd.Remove(date); if (isBase) userRemove[date] = true; }
			SaveUserHolidays();
			cal.Invalidate(); rcal.Invalidate();
			showSel(); lastNext = null; refreshHome(); UpdateTrayText();
		};
		cal.DateChanged += delegate { showSel(); };
		syncBtn.Click += delegate
		{
			kasiKey = kin.Box.Text.Trim();
			SaveSettings();
			lastSync = "";
			StartSync(true);
			showSync();
		};
		SyncDone = delegate { showSync(); cal.Invalidate(); rcal.Invalidate(); showSel(); lastNext = null; refreshHome(); };

		closeB.Click += delegate { closeAnim(); };
		// ── 자동 저장: 바꾸면 잠깐 뒤 알아서 저장 (저장 버튼 없음) ──
		string lastApplied = null;
		Func<string> applyAll = delegate
		{
			if (mins.Count == 0) return "알림 시각을 1개 이상 추가해야 저장돼요";
			bool[] hrs = new bool[24];
			for (int j = 0; j < 24; j++) hrs[j] = hb[j].Checked;
			string text = HoursToText(hrs);
			if (text == null) return "평일 알림 시간을 한 곳 이상 골라야 저장돼요";
			for (int a1 = 0; a1 < NHK; a1++) for (int b1 = a1 + 1; b1 < NHK; b1++) if (hkBox[a1].Mod == hkBox[b1].Mod && hkBox[a1].Vk == hkBox[b1].Vk) return "단축키가 서로 겹쳐서 저장되지 않았어요";
			minutes = mins.ToArray();
			weekdayHoursText = text;
			weekdayHours = hrs;
			autoStart = autoTg.Checked; foStart = foTg.Checked;
			soundOn = sndTg.Checked;
			dndManual = dndTg.Checked; dndAuto = dndAutoTg.Checked; lightAlert = lightTg.Checked; moneyUnitTo = unTo.Index; moneyUnitBare = unBare.Index; sumAcc = sumTg.Checked;
			quitOn = quitTg.Checked; quitHour = 18; preHourOn = preTg.Checked;
			phoneOn = phTg.Checked; phoneTopic = phTopic;
			toolsOn = toTg.Checked; toolsPaste = toPaste.Checked; moneyFmt = moneyCh.Index; dateFmt = dateCh.Index;
			for (int a1 = 0; a1 < NHK; a1++) { hkMod[a1] = hkBox[a1].Mod; hkVk[a1] = hkBox[a1].Vk; }
			payGradeId = Pay.Grades[gradeCh.Index].Id; payCap4 = false;
			if (payGradeId == "xx") { int pv; if (int.TryParse(baseIn.Box.Text.Replace(",", "").Trim(), out pv) && pv > 0) payBase = pv; payRate = rateCh.Index == 1 ? 60 : 55; }
			if (dndItem != null) dndItem.Checked = dndManual;
			if (soundItem != null) soundItem.Checked = soundOn;
			kasiKey = kin.Box.Text.Trim();
			insarangFront = frontTg.Checked;
			string snap = autoStart + "|" + string.Join(",", hkMod) + "|" + string.Join(",", hkVk) + "|" + insarangFront;
			SaveSettings();
			if (snap != lastApplied) { ApplyHotkeys(); ApplyAutoStart(); lastApplied = snap; }   // 단축키·자동 실행이 그대로면 다시 등록하지 않아요
			UpdateTrayText();
			lastNextReset();
			return null;
		};
		System.Windows.Forms.Timer saveTm = new System.Windows.Forms.Timer(); saveTm.Interval = 3000;   // 값을 다 고르고 3초 쉬었을 때(또는 창을 닫을 때) 한 번만 저장해요
		Action flushSave = delegate
		{
			saveTm.Stop();
			if (!dirty) return;
			string err = null;
			try { err = applyAll(); } catch (Exception ex) { LogError(ex); err = "저장하지 못했어요"; }
			invalid = err != null;
			if (err == null) { dirty = false; foot1.Fore = Theme.Green; foot1.Text = "저장됐어요"; }
			else { foot1.Fore = Theme.Red; foot1.Text = err; }
		};
		saveTm.Tick += delegate { flushSave(); };
		settingsPage = delegate { return curPage; };
		Action queueSave = delegate { dirty = true; foot1.Fore = Theme.Sub; foot1.Text = "저장 중..."; saveTm.Stop(); saveTm.Start(); };
		// 저장이 필요한 쪽(알림·기한 설정·휴대폰·도구·수당·일반)의 컨트롤 변화를 모두 감시
		Action<Control> hook = null;
		hook = delegate(Control root)
		{
			foreach (Control c in root.Controls)
			{
				if (c is TToggle) ((TToggle)c).CheckedChanged += delegate { queueSave(); };
				else if (c is TChip) ((TChip)c).CheckedChanged += delegate { queueSave(); };
				else if (c is TStepper) { if (c != nud) ((TStepper)c).ValueChanged += delegate { queueSave(); }; }   // 알림 분 입력칸은 '추가'를 눌러야 저장돼요
				else if (c is TChooser) ((TChooser)c).IndexChanged += delegate { queueSave(); };
				else if (c is TKeyBox) ((TKeyBox)c).Changed += delegate { queueSave(); };
				else if (c is TInput) { TInput ti = (TInput)c; ti.Box.TextChanged += delegate { queueSave(); }; }
				if (c is Panel && !(c is TInput)) hook(c);
			}
		};
		foreach (int pi in new int[] { 1, 4, 5, 7, 8 }) hook(pages[pi]);
		addMin.Click += delegate { dirty = true; flushSave(); };   // 추가를 누르면 바로 저장
		hkReset.Click += delegate { queueSave(); };
		// ═════ 처음 켰을 때의 '시작 도우미' (홈 위에 한 번만) ═════
		if (!File.Exists(SetupDonePath))
		{
			Card sc = new Card(); sc.Location = new Point(Zs(PX), Zs(70)); sc.Size = new Size(Zs(CW), Zs(AutoAllowed ? 418 : 330)); sc.BackColor = Theme.Bg;
			Lbl(sc, "설치가 끝났어요!", 18f, 2, Theme.Accent, 22, 16, CW - 44, 34);
			Lbl(sc, "이제 따로 할 일은 없어요. 알아서 알려 드려요", 12f, 1, Theme.Ink, 22, 52, CW - 44, 26);
			string[] sl = {
				"평일 퇴근 후(18시~다음 날 8시)와 주말·공휴일에\n매시 30분마다 알림이 떠요",
				"알림이 뜨면 큰 '확인했어요' 버튼만 누르면\n초과근무가 기록돼요",
				"이 창을 닫아도 오른쪽 아래 작업표시줄에서\n계속 실행되고 있어요" };
			for (int si = 0; si < sl.Length; si++)
			{
				Lbl(sc, "✓", 13f, 2, Theme.Accent, 22, 96 + si * 56, 24, 26);
				TLabel st = new TLabel(sl[si], 11.5f, 0, Theme.Ink, Theme.Bg); st.Wrap = true; st.Location = new Point(Zs(50), Zs(96 + si * 56)); st.Size = new Size(Zs(CW - 72), Zs(48)); sc.Controls.Add(st);
			}
			Panel sd = new Panel(); sd.BackColor = Theme.Line; sd.Location = new Point(Zs(22), Zs(272)); sd.Size = new Size(Zs(CW - 44), 1); sc.Controls.Add(sd);
			Action finishSetup = delegate
			{
				try { Directory.CreateDirectory(Dir); File.WriteAllText(SetupDonePath, DateTime.Now.ToString("s")); } catch { }
				sc.Visible = false; sky.Restart();
			};
			if (AutoAllowed)
			{
				Lbl(sc, "컴퓨터를 켤 때 자동으로 시작할까요?", 12.5f, 2, Theme.Ink, 22, 286, CW - 44, 28);
				TLabel sq = new TLabel("끄면 컴퓨터를 켤 때마다 직접 실행해야 알림이 와요", 10.5f, 0, Theme.Sub, Theme.Bg); sq.Wrap = true; sq.Location = new Point(Zs(22), Zs(316)); sq.Size = new Size(Zs(CW - 44), Zs(40)); sc.Controls.Add(sq);
				TButton yes = new TButton("네, 자동으로 시작할게요 (추천)", BK.Primary); yes.Pt = 12f; yes.Weight = 2; yes.BackColor = Theme.Bg; yes.Location = new Point(Zs(22), Zs(362)); yes.Size = new Size(Zs(320), Zs(50)); sc.Controls.Add(yes);
				TButton no = new TButton("아니요", BK.Outline); no.Pt = 12f; no.BackColor = Theme.Bg; no.Location = new Point(Zs(354), Zs(362)); no.Size = new Size(Zs(CW - 44 - 332), Zs(50)); sc.Controls.Add(no);
				yes.Click += delegate { autoTg.SetImmediate(true); queueSave(); finishSetup(); };
				no.Click += delegate { finishSetup(); };
			}
			else
			{
				Lbl(sc, "컴퓨터를 켠 뒤에는 이 프로그램을 직접 실행해 주세요", 11f, 1, Theme.Sub, 22, 286, CW - 44, 26);
				TButton okS = new TButton("알겠어요, 시작할게요", BK.Primary); okS.Pt = 12f; okS.Weight = 2; okS.BackColor = Theme.Bg; okS.Location = new Point(Zs(22), Zs(322)); okS.Size = new Size(Zs(CW - 44), Zs(50)); sc.Controls.Add(okS);
				okS.Click += delegate { finishSetup(); };
			}
			pHome.Controls.Add(sc); sc.BringToFront();
			EventHandler fitSc = delegate { sc.Width = Math.Max(Zs(300), pHome.Width - 2 * Zs(PX)); sc.Height = Math.Max(Zs(AutoAllowed ? 418 : 330), pHome.Height - Zs(70) - Zs(6)); }; pHome.Resize += fitSc; fitSc(null, EventArgs.Empty);
		}
		closeB.Text = "완료";
		f.FormClosing += delegate(object o, FormClosingEventArgs fe)
		{
			flushSave();
			if (invalid && MessageBox.Show(f, foot1.Text + "\n그래도 닫을까요? (바뀐 내용은 저장되지 않아요)", "초과근무 알리미", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { fe.Cancel = true; fading = false; f.Opacity = 1; }
			if (!fe.Cancel && f.WindowState == FormWindowState.Normal) SaveWinRect(f);
		};
		f.AcceptButton = null;
		f.KeyPreview = true;
		f.KeyDown += delegate(object o, KeyEventArgs ke) { if (ke.KeyCode == Keys.Escape) { f.Close(); } };
		// 방향키는 윈도우가 먼저 가로채서 키 이벤트로 안 오기 때문에, 메시지 단계에서 직접 지켜봐요 (홈 화면에서만)
		KonamiWatch kw = new KonamiWatch(delegate { return f.ContainsFocus && pHome.Visible; }, delegate { Confetti.Fire(f.Bounds); Bubble.Say("숨은 코드를 찾았어요! 오늘도 힘내요", 4); });
		Application.AddMessageFilter(kw);
		f.FormClosed += delegate { Application.RemoveMessageFilter(kw); };

		System.Windows.Forms.Timer clk = new System.Windows.Forms.Timer(); clk.Interval = 250;
		FrameTimer skyTm = new FrameTimer(); skyTm.Throttle = true; int skyN = 0;
		skyTm.Tick += delegate { if (pHome.Visible && f.Visible && f.WindowState != FormWindowState.Minimized) sky.Pulse(); };   // 포커스가 없어도(다른 창을 쓰는 중에도) 시계는 정확히 넘어가야 해요
		clk.Tick += delegate
		{
			try
			{
				if (pHome.Visible) { long rt0 = System.Diagnostics.Stopwatch.GetTimestamp(); refreshHome(); PaintProf.Add("refreshHome", rt0); }
				else if (pRe.Visible && Records.CountAll() != lastRecCount) refreshRec();   // 팝업에서 방금 기록한 것 바로 반영
			}
			catch (Exception ex) { LogError(ex); }
		};
		f.FormClosed += delegate
		{
			clk.Stop(); clk.Dispose(); skyTm.Stop(); skyTm.Dispose();
			SyncDone = null;
			themeTrans = null; settingsReopen = null;
			settingsGo = null; settingsSub = null; settingsPage = null;
			settingsForm = null; Mem.Later(2500);
		};

		BM("동작 연결");
		showSel(); showSync(); refreshHome(); go(themePage >= 0 ? themePage : 0); themePage = -1;
		if (themeSnap == null && holidayDay != DateTime.Today) { holidayDay = DateTime.Today; string hg = Care.Holiday(DateTime.Today); if (hg != null) Bubble.Say(hg, 5); }
		BM("홈 채우기·시작");
		clk.Start(); skyTm.Start();
		f.VisibleChanged += delegate { if (f.IsDisposed) return; if (f.Visible) { clk.Start(); skyTm.Start(); } else { clk.Stop(); skyTm.Stop(); if (th != null) th.Release(); if (vsTw != null) vsTw.Cancel(); } };
		if (themeSnap != null)
		{
			// 화면 색 바꾸는 중: 새 창을 바로 보이게 하고, 위에 얹은 옛 화면 사진을 서서히 걷어낸다
			themeSnap = null;
			ThemeVeil tv = themeVeil; themeVeil = null;
			f.Opacity = 0.996; f.Show(); f.Invalidate(true); f.Update();
			if (tv != null)
			{
				Anim.Run(480, Ease.InOutCubic, delegate(float k) { if (!tv.IsDisposed) tv.Opacity = 1.0 - k * 0.999; }, delegate { if (!tv.IsDisposed) tv.Dispose(); });
				System.Windows.Forms.Timer tg = new System.Windows.Forms.Timer(); tg.Interval = 1200;
				tg.Tick += delegate { tg.Stop(); tg.Dispose(); if (!tv.IsDisposed) tv.Dispose(); };
				tg.Start();
			}
			return;
		}
		Action fadeIn = delegate
		{
			fading = false;
			f.TopMost = true;
			f.Opacity = 0;
			f.Show();
			f.Activate();
			f.TopMost = false;
			f.Invalidate(true); f.Update();
			int top0 = f.Top;
			Anim.Run(300, Ease.OutQuint, delegate(float t) { if (!f.IsDisposed) { f.Opacity = Math.Min(0.996, t * 1.6); f.Top = top0 + (int)(Zs(16) * (1 - t)); } }, delegate { if (!f.IsDisposed) { f.Opacity = 0.996; f.Top = top0; f.Invalidate(true); } });
			// 안전장치: 애니메이션이 어떤 이유로든 멈춰도 창은 반드시 완전히 보이게 한다
			System.Windows.Forms.Timer guard = new System.Windows.Forms.Timer(); guard.Interval = 700;
			guard.Tick += delegate { guard.Stop(); guard.Dispose(); if (!f.IsDisposed && !fading) { f.Opacity = 0.996; f.Top = top0; f.Invalidate(true); } };
			guard.Start();
		};
		settingsReopen = delegate { showSel(); showSync(); refreshHome(); goQuick(0); fadeIn(); };
		if (prebuilding)
		{
			// 앱이 한가할 때 미리 만들어 숨겨 둬요 → 처음 열 때도 바로 떠요
			f.Opacity = 0; f.Show(); f.Invalidate(true); f.Update(); f.Hide();
			return;
		}
		fadeIn();
	}

	private static DateTime holidayDay = DateTime.MinValue;
	private static string nacKey = null; private static DateTime nacVal;
	private static string WinPath { get { return Path.Combine(DataDir, "window.txt"); } }
	private static void SaveWinRect(Form f)
	{
		try { Directory.CreateDirectory(DataDir); AtomicIO.WriteLines(WinPath, new string[] { f.Left + "," + f.Top + "," + f.ClientSize.Width + "," + f.ClientSize.Height }, System.Text.Encoding.UTF8); } catch { }
	}
	// 지난번 창 위치·크기를 기억 (모니터가 바뀌어 화면 밖이면 무시)
	private static void RestoreWinRect(Form f)
	{
		try
		{
			if (!File.Exists(WinPath)) return;
			string[] p = File.ReadAllText(WinPath).Trim().Split(new char[] { ',' });
			if (p.Length < 4) return;
			int x = int.Parse(p[0]), y = int.Parse(p[1]), w = int.Parse(p[2]), h = int.Parse(p[3]);
			w = Math.Max(f.MinimumSize.Width, w); h = Math.Max(f.MinimumSize.Height - Zs(0), h);
			Rectangle probe = new Rectangle(x, y, Math.Min(w, Zs(300)), Zs(60));
			bool vis = false;
			foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(probe)) { vis = true; w = Math.Min(w, sc.WorkingArea.Width); h = Math.Min(h, sc.WorkingArea.Height); }
			if (!vis) return;
			f.StartPosition = FormStartPosition.Manual; f.ClientSize = new Size(w, h); f.Location = new Point(x, y);
		}
		catch { }
	}

	private static DateTime NextAlertAt()
	{
		DateTime now0 = DateTime.Now;
		string key = now0.ToString("yyyyMMddHHmm") + "|" + string.Join(",", minutes.Select(m => m.ToString()).ToArray()) + "|" + weekdayHoursText + "|" + snoozeAt.Ticks + "|" + (snoozeSlot != null) + "|" + userAdd.Count + "|" + userRemove.Count;
		if (key == nacKey) return nacVal;
		nacVal = NextAlertAt0(); nacKey = key;
		return nacVal;
	}
	private static DateTime NextAlertAt0()
	{
		DateTime now = DateTime.Now, res = DateTime.MinValue;
		DateTime n = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);
		for (int i = 0; i < 14400; i++)
		{
			if (IsAlertMinute(n.Minute) && ShouldAlert(n)) { res = n; break; }
			n = n.AddMinutes(1);
		}
		if (snoozeSlot != null && snoozeAt > now && (res == DateTime.MinValue || snoozeAt < res)) res = snoozeAt;
		return res;
	}

	private static string RelText(TimeSpan t)
	{
		int m = (int)Math.Ceiling(t.TotalMinutes);
		if (m <= 0) return "곧 알려드려요";
		if (m < 60) return m + "분 뒤";
		if (m < 24 * 60) return (m / 60) + "시간 " + (m % 60) + "분 뒤";
		return (m / (24 * 60)) + "일 뒤";
	}

	private static Action lastNextReset = delegate { };
}
