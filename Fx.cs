using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ─────────────────────────────────────────────────────────────
//  알림이 왔다는 걸 한눈에 알려주는 효과
//   · AlertBanner : 화면 위쪽에서 통통 튀며 내려오는 큰 띠 (흔들리는 종 + 퍼지는 물결)
//   · Confetti    : '확인했어요'를 누르면 팝업 양쪽 아래에서 터지는 폭죽
// ─────────────────────────────────────────────────────────────
internal sealed class AlertBanner : Form
{
	private static AlertBanner cur;
	private readonly Stopwatch sw = Stopwatch.StartNew();
	private double tt, lastNow;      // 느린 PC에서 한 번 멈칫해도 애니메이션이 건너뛰지 않게, 한 틱에 흐르는 시간에 상한을 둔다
	private readonly FrameTimer tm = new FrameTimer();
	private int topY, hiddenY; private bool quiet; private Bitmap layer; private double lastPaintAt;
	private double outAt = -1;
	private readonly Image mascot;
	private bool canOpen;

	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80; return cp; } }

	public static void Pop()
	{
		try
		{
			if (cur != null && !cur.IsDisposed) { cur.Restart(); return; }
			cur = new AlertBanner(); cur.Show();
		}
		catch (Exception ex) { Program.LogErr(ex); }
	}
	// 미리 만들어 숨겨 둬요 (알림이 뜰 때 창을 새로 만들면 느린 PC에서 몇 백 ms 걸려요)
	public static void Prebuild()
	{
		try
		{
			if (cur != null && !cur.IsDisposed) return;
			AlertBanner b = new AlertBanner(); b.quiet = true; cur = b; b.Show(); b.tm.Stop(); b.Visible = false;
		}
		catch (Exception ex) { Program.LogErr(ex); cur = null; }
	}
	public static void PrebuildLater(int ms)
	{
		Timer t = new Timer(); t.Interval = ms; t.Tick += delegate { t.Stop(); t.Dispose(); Prebuild(); }; t.Start();
	}
	public static void Hide() { try { if (cur != null && !cur.IsDisposed) cur.SlideOut(); } catch { } }

	[System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		// Windows 11: 시스템이 부드러운 둥근 모서리·그림자를 그려요 (테두리선 없음). 그 외에는 깔끔한 사각형
		try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); int none = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(Handle, 34, ref none, 4); } catch { }
	}
	private AlertBanner()
	{
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
		BackColor = Theme.AccentBtn;
		mascot = Program.MascotImage();
		Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
		Size = new Size(Math.Min(Dpi.Z(560), wa.Width - Dpi.Z(24)), Dpi.Z(84));
		topY = wa.Top + Dpi.Z(16); hiddenY = wa.Top - Height - Dpi.Z(8);
		Location = new Point(wa.Left + (wa.Width - Width) / 2, hiddenY);
		canOpen = Program.HasInsarang;
		if (canOpen) Cursor = Cursors.Hand;
		Click += delegate { if (canOpen) { Program.OpenInsarang(); } SlideOut(); };
		tm.Tick += delegate { Step(); };
		Shown += delegate { if (quiet) return; sw.Restart(); tt = 0; lastNow = 0; tm.Start(); };
	}

	private void Restart() { quiet = false; Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea; Size = new Size(Math.Min(Dpi.Z(560), wa.Width - Dpi.Z(24)), Dpi.Z(84)); topY = wa.Top + Dpi.Z(16); hiddenY = wa.Top - Height - Dpi.Z(8); Location = new Point(wa.Left + (wa.Width - Width) / 2, hiddenY); sw.Restart(); tt = 0; lastNow = 0; outAt = -1; lastPaintAt = 0; if (!Visible) Show(); canOpen = Program.HasInsarang; Cursor = canOpen ? Cursors.Hand : Cursors.Default; if (!tm.Enabled) tm.Start(); }
	private void SlideOut() { if (outAt < 0) outAt = tt; }

	private void Step()
	{
		if (IsDisposed) { tm.Stop(); return; }
		double nowS = sw.Elapsed.TotalSeconds; tt += Math.Min(0.1, nowS - lastNow); lastNow = nowS;
		double t = tt;
		double p;                                   // 0 = 숨김, 1 = 자리
		if (outAt >= 0)
		{
			double k = (t - outAt) / 0.32;
			if (k >= 1) { tm.Stop(); Visible = false; return; }
			p = (1 - k * k * k) * Math.Min(1.0, SpringIn(outAt));
		}
		else
		{
			p = SpringIn(t);
			if (t > 22) { SlideOut(); }
		}
		int y = hiddenY + (int)Math.Round((topY - hiddenY) * p);
		if (y != Top) Top = y;
		if (outAt < 0 && t > 1.7 && nowS - lastPaintAt < 0.033) return;   // 자리를 잡은 뒤엔 물결·종만 움직이니 초당 30번이면 충분해요
		lastPaintAt = nowS; Invalidate();
	}
	// 살짝 넘쳤다가 제자리로 (스프링)
	private static double SpringIn(double t) { if (t >= 1.6) return 1.0; return 1.0 - Math.Exp(-7.5 * t) * Math.Cos(12.5 * t); }

	// 움직이지 않는 부분(바탕·마룡이·글자)은 한 번만 그려 두고, 매 프레임엔 사진 한 장만 붙여요
	private void BuildLayer()
	{
		if (layer != null) layer.Dispose();
		layer = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(layer))
		{
			Gfx.HQ(g);
			RectangleF full = new RectangleF(0, 0, Width, Height);
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, -1, Width, Height + 2), Color.FromArgb(18, 156, 142), Color.FromArgb(24, 118, 140), 0f)) g.FillRectangle(lb, full);
			using (LinearGradientBrush hl = new LinearGradientBrush(new RectangleF(0, -1, Width, Height * 0.6f), Color.FromArgb(20, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(hl, 0, 0, Width, Height * 0.6f);
			float m = Height - Dpi.Zf(18);
			RectangleF mr = new RectangleF(Dpi.Zf(14), Dpi.Zf(9), m, m);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillEllipse(b, mr);
			if (mascot != null)
			{
				float ar = (float)mascot.Width / mascot.Height; float mh = m * 0.92f, mw = mh * ar;
				g.DrawImage(mascot, mr.X + (m - mw) / 2f, mr.Y + (m - mh) / 2f, mw, mh);
			}
			float tx = mr.Right + Dpi.Zf(14), tw = Width - tx - Dpi.Zf(96);
			Gfx.Text(g, "초과근무 확인 시간이에요", Theme.F(15f, 2), Color.White, new RectangleF(tx, Dpi.Zf(14), tw, Dpi.Zf(30)), StringAlignment.Near, StringAlignment.Center, false);
			Gfx.Text(g, "인사랑을 열고 직접 확인을 눌러 주세요", Theme.F(10.5f, 0), Color.FromArgb(230, 255, 255, 255), new RectangleF(tx, Dpi.Zf(45), tw, Dpi.Zf(24)), StringAlignment.Near, StringAlignment.Center, false);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g);
		double t = tt;
		if (layer == null || layer.Width != Width || layer.Height != Height) BuildLayer();
		g.DrawImageUnscaled(layer, 0, 0);
		float m = Height - Dpi.Zf(18);
		RectangleF mr = new RectangleF(Dpi.Zf(14), Dpi.Zf(9), m, m);

		// 종 + 퍼지는 물결
		float bx = Width - Dpi.Zf(50), by = Height / 2f;
		double ph = (t * 0.85) % 1.0;
		for (int i = 0; i < 2; i++)
		{
			double q = (ph + i * 0.5) % 1.0;
			float rr = Dpi.Zf(18) + (float)q * Dpi.Zf(26);
			using (Pen pn = new Pen(Color.FromArgb((int)(90 * (1 - q)), 255, 255, 255), Dpi.Zf(1.6f))) g.DrawEllipse(pn, bx - rr, by - rr, rr * 2, rr * 2);
		}
		double cyc = (t - 0.5) % 2.2; if (t < 0.5) cyc = 9;
		float ang = cyc < 1.4 ? (float)(Math.Sin(cyc * 30.0) * Math.Exp(-cyc * 2.6) * 24.0) : 0f;
		float S = Dpi.Zf(25f);
		GraphicsState gs = g.Save();
		g.TranslateTransform(bx, by - S * 0.5f); g.RotateTransform(ang); g.ScaleTransform(S, S);
		using (GraphicsPath bell = new GraphicsPath())
		{
			bell.AddBezier(-0.42f, 0.30f, -0.42f, -0.50f, 0.42f, -0.50f, 0.42f, 0.30f);
			bell.AddLine(0.42f, 0.30f, 0.58f, 0.46f); bell.AddLine(0.58f, 0.46f, -0.58f, 0.46f); bell.AddLine(-0.58f, 0.46f, -0.42f, 0.30f); bell.CloseFigure();
			using (SolidBrush b = new SolidBrush(Color.FromArgb(244, 255, 252, 244))) { g.FillPath(b, bell); g.FillEllipse(b, -0.1f, -0.62f, 0.2f, 0.2f); g.FillEllipse(b, -0.13f, 0.52f, 0.26f, 0.26f); }
		}
		g.Restore(gs);
		if (t >= 0.25 && t < 1.05) { Bitmap fx = Spr.Get("ms_wexp" + Math.Min(7, (int)((t - 0.25) / 0.1))); if (fx != null) { float fs = Dpi.Zf(74f); g.DrawImage(fx, bx - fs / 2f, by - fs / 2f - Dpi.Zf(4), fs, fs * fx.Height / fx.Width); } }
	}

	protected override void Dispose(bool d) { if (d) { tm.Stop(); if (layer != null) layer.Dispose(); } base.Dispose(d); }
}

internal sealed class Confetti : Form
{
	private class P { public float X, Y, Vx, Vy, R, Vr, S, Ph, Vp; public int Shape; public int Ci; }
	private readonly List<P> ps = new List<P>();
	private readonly Stopwatch sw = new Stopwatch();
	private readonly FrameTimer tm = new FrameTimer();
	private double last;
	private const double Life = 2.7;
	private static Confetti spare;   // 한 번 만든 창은 숨겨 두고 다시 써요 (창을 새로 만드는 게 느린 PC가 있어서요)
	private static readonly Color[] Pal = { Color.FromArgb(0, 166, 148), Color.FromArgb(255, 193, 59), Color.FromArgb(255, 99, 125), Color.FromArgb(86, 156, 255), Color.FromArgb(150, 110, 255), Color.FromArgb(56, 200, 120), Color.FromArgb(255, 140, 60) };
	private SolidBrush[] brushes;
	private readonly Random rnd = new Random();

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct PT { public int x, y; }
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct SZ { public int cx, cy; }
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)] private struct BF { public byte Op, Flags, Alpha, Fmt; }
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct BMI { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biX, biY, biClrUsed, biClrImportant; }
	[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PT pptDst, ref SZ psize, IntPtr hdcSrc, ref PT pprSrc, int crKey, ref BF pblend, int dwFlags);
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BMI pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr o);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
	private IntPtr memDC = IntPtr.Zero, hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero, bits = IntPtr.Zero; private int bw, bh; private Bitmap surf; private Graphics sg;

	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x20 | 0x80 | 0x00080000; return cp; } }   // 클릭 통과 · 포커스 안 받음 · 투명 레이어
	protected override void OnPaint(PaintEventArgs e) { }
	protected override void OnPaintBackground(PaintEventArgs e) { }

	public static void Fire(Rectangle popup)
	{
		try
		{
			if (!Anim.Effects) return;
			if (spare == null || spare.IsDisposed) { spare = new Confetti(); }
			spare.Launch(popup);
		}
		catch (Exception ex) { Program.LogErr(ex); spare = null; }
	}
	public static void Prebuild()
	{
		try { if (spare == null || spare.IsDisposed) { Confetti c = new Confetti(); c.Bounds = new Rectangle(-3000, -3000, 64, 64); c.Show(); c.EnsureSurface(); c.Visible = false; spare = c; } }
		catch (Exception ex) { Program.LogErr(ex); spare = null; }
	}
	public static void PrebuildLater(int ms) { Timer t = new Timer(); t.Interval = ms; t.Tick += delegate { t.Stop(); t.Dispose(); Prebuild(); }; t.Start(); }

	private Confetti()
	{
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
		brushes = new SolidBrush[Pal.Length]; for (int i = 0; i < Pal.Length; i++) brushes[i] = new SolidBrush(Pal[i]);
		tm.Tick += delegate { Step(); };
		FormClosed += delegate { tm.Stop(); FreeSurface(); };
	}

	private void Launch(Rectangle pop)
	{
		tm.Stop(); ps.Clear();
		Rectangle wa = Screen.FromRectangle(pop).WorkingArea;
		int padX = Dpi.Z(190), padT = Dpi.Z(230), padB = Dpi.Z(60);
		Rectangle r = Rectangle.FromLTRB(Math.Max(wa.Left, pop.Left - padX), Math.Max(wa.Top, pop.Top - padT), Math.Min(wa.Right, pop.Right + padX), Math.Min(wa.Bottom, pop.Bottom + padB));
		float sc = Dpi.Scale;
		for (int side = 0; side < 2; side++)
		{
			float ox = (side == 0 ? pop.Left + Dpi.Z(30) : pop.Right - Dpi.Z(30)) - r.Left, oy = pop.Bottom - Dpi.Z(40) - r.Top;
			for (int i = 0; i < 70; i++)
			{
				double an = (side == 0 ? -65 : -115) * Math.PI / 180.0 + (rnd.NextDouble() - 0.5) * 0.9;
				double sp = (620 + rnd.NextDouble() * 760) * sc;
				ps.Add(new P { X = ox, Y = oy, Vx = (float)(Math.Cos(an) * sp), Vy = (float)(Math.Sin(an) * sp), R = (float)(rnd.NextDouble() * 360), Vr = (float)((rnd.NextDouble() - 0.5) * 900), S = (float)(7 + rnd.NextDouble() * 7) * sc, Ph = (float)(rnd.NextDouble() * 6.28), Vp = (float)(6 + rnd.NextDouble() * 8), Shape = rnd.Next(3), Ci = rnd.Next(Pal.Length) });
			}
		}
		Bounds = r;
		EnsureSurface();
		sw.Restart(); last = 0; Draw();
		if (!Visible) Show();
		tm.Start();
	}

	private void EnsureSurface()
	{
		if (!IsHandleCreated) { var h = Handle; }
		int w = Math.Max(1, Width), h2 = Math.Max(1, Height);
		if (memDC != IntPtr.Zero && w == bw && h2 == bh) return;
		FreeSurface();
		BMI bi = new BMI { biSize = 40, biWidth = w, biHeight = -h2, biPlanes = 1, biBitCount = 32, biCompression = 0 };
		IntPtr sdc = GetDC(IntPtr.Zero); memDC = CreateCompatibleDC(sdc);
		hBmp = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0); ReleaseDC(IntPtr.Zero, sdc);
		if (hBmp == IntPtr.Zero || bits == IntPtr.Zero) throw new InvalidOperationException("폭죽 비트맵을 만들지 못했어요.");
		oldBmp = SelectObject(memDC, hBmp); bw = w; bh = h2;
		surf = new Bitmap(w, h2, w * 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, bits); sg = Graphics.FromImage(surf); sg.SmoothingMode = SmoothingMode.AntiAlias;
	}
	private void FreeSurface()
	{
		try { if (sg != null) sg.Dispose(); if (surf != null) surf.Dispose(); } catch { }
		sg = null; surf = null;
		try { if (memDC != IntPtr.Zero) { if (oldBmp != IntPtr.Zero) SelectObject(memDC, oldBmp); DeleteDC(memDC); } if (hBmp != IntPtr.Zero) DeleteObject(hBmp); } catch { }
		memDC = IntPtr.Zero; hBmp = IntPtr.Zero; bits = IntPtr.Zero; bw = bh = 0;
	}

	private void Step()
	{
		if (IsDisposed) { tm.Stop(); return; }
		double t = sw.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.05, t - last); last = t;
		if (t > Life) { tm.Stop(); Visible = false; return; }
		float sc = Dpi.Scale, g = 1500f * sc;
		foreach (P p in ps)
		{
			p.Vy += g * dt; float drag = (float)Math.Exp(-1.7 * dt); p.Vx *= drag; p.Vy *= (float)Math.Exp(-0.9 * dt);
			p.X += p.Vx * dt; p.Y += p.Vy * dt; p.R += p.Vr * dt; p.Ph += p.Vp * dt;
		}
		Draw();
	}

	private void Draw()
	{
		if (sg == null || memDC == IntPtr.Zero || !IsHandleCreated) return;
		Graphics g = sg; g.Clear(Color.Transparent);
		double t = sw.Elapsed.TotalSeconds;
		float shrink = t < Life - 0.7 ? 1f : (float)Math.Max(0.0, (Life - t) / 0.7);
		foreach (P p in ps)
		{
			float flip = (float)Math.Abs(Math.Cos(p.Ph));            // 팔랑이는 느낌
			float w = p.S * shrink, h = p.S * 0.55f * Math.Max(0.15f, flip) * shrink;
			if (w < 1f) continue;
			GraphicsState gs = g.Save(); g.TranslateTransform(p.X, p.Y); g.RotateTransform(p.R);
			SolidBrush b = brushes[p.Ci];
			if (p.Shape == 0) g.FillRectangle(b, -w / 2, -h / 2, w, h);
			else if (p.Shape == 1) g.FillEllipse(b, -w / 3, -w / 3, w * 0.66f, w * 0.66f);
			else g.FillRectangle(b, -w, -h / 4, w * 2f, Math.Max(1.5f, h / 2f));
			g.Restore(gs);
		}
		PT dst = new PT { x = Left, y = Top }; SZ sz = new SZ { cx = bw, cy = bh }; PT src = new PT(); BF bf = new BF { Op = 0, Flags = 0, Alpha = 255, Fmt = 1 };
		IntPtr sdc = GetDC(IntPtr.Zero);
		UpdateLayeredWindow(Handle, sdc, ref dst, ref sz, memDC, ref src, 0, ref bf, 2);
		ReleaseDC(IntPtr.Zero, sdc);
	}
	protected override void Dispose(bool d) { if (d) { tm.Stop(); FreeSurface(); } base.Dispose(d); }
}
