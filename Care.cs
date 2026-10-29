using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ─────────────────────────────────────────────────────────────
//  마룡이의 말풍선 · 건강 챙김 멘트 · 숨은 재미
// ─────────────────────────────────────────────────────────────
internal static class Care
{
	// 0 보통 · 1 한도 80% · 2 한도 90% · 3 쉬어야 해요(한도 초과 또는 하루 8시간 이상)
	public static string Message(DateTime d, int monthHours, int dayHours, int limit, out int level, int yearHours = 0)
	{
		level = 0;
		if (yearHours >= Program.YearLimit) { level = 3; return "올해 연 한도 " + Program.YearLimit + "시간을 넘었어요. 좀 쉬세요"; }
		double pct = limit > 0 ? monthHours * 100.0 / limit : 0;
		if (dayHours >= 8) { level = 3; return "오늘 " + dayHours + "시간째예요. 좀 쉬세요"; }
		if (pct >= 100) { level = 3; return "이번 달 한도를 넘었어요. 좀 쉬세요"; }
		if (pct >= 90) { level = 2; return "한도가 거의 찼어요. 무리하지 마세요"; }
		if (pct >= 80) { level = 1; return "한도가 가까워요. 몸 챙기세요"; }
		if (yearHours >= Program.YearLimit * 9 / 10) { level = 2; return "올해 연 한도가 거의 찼어요. 무리하지 마세요"; }
		return "수고하셨어요. 오늘도 고생 많았어요.";
	}

	public static string Holiday(DateTime n)
	{
		string fe = Hanbok.Festival(n); if (fe == "설") return "새해 복 많이 받으세요!"; if (fe == "추석") return "풍성한 한가위 보내세요!";
		int m = n.Month, d = n.Day;
		if (m == 1 && d == 1) return "새해 복 많이 받으세요!";
		if (m == 3 && d == 1) return "오늘은 삼일절이에요";
		if (m == 5 && d == 5) return "어린이날, 마음만은 어린이처럼!";
		if (m == 5 && d == 8) return "어버이날, 부모님께 안부 전해요";
		if (m == 6 && d == 6) return "오늘은 현충일이에요";
		if (m == 8 && d == 15) return "광복절이에요";
		if (m == 10 && d == 3) return "개천절이에요";
		if (m == 10 && d == 9) return "한글날! 오늘도 바른 말 고운 말";
		if (m == 12 && d == 24) return "메리 크리스마스 이브!";
		if (m == 12 && d == 25) return "메리 크리스마스!";
		if (m == 12 && d == 31) return "올 한 해도 고생 많았어요";
		return null;
	}

	public static string Quitting(DateTime now)
	{
		if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday) return "오늘은 쉬는 날이에요. 푹 쉬어요!";
		DateTime q = now.Date.AddHours(Program.quitHour);
		if (now >= q) return "퇴근 시간이 지났어요. 얼른 들어가요!";
		TimeSpan t = q - now; int h = (int)t.TotalHours, mi = t.Minutes;
		return "퇴근까지 " + (h > 0 ? h + "시간 " : "") + mi + "분 남았어요";
	}
}

internal sealed class Bubble : Form
{
	private static Bubble cur;
	private string text; private readonly Image mascot;
	private readonly FrameTimer tm = new FrameTimer(); private readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
	private double tt, last, outAt = -1; private double hold; private readonly int baseY;
	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80; return cp; } }

	private Action onClick;
	public static void Say(string s, double seconds) { Say(s, seconds, null); }
	public static void Say(string s, double seconds, Action click)
	{
		try
		{
			if (cur == null || cur.IsDisposed) cur = new Bubble(s, seconds);
			cur.text = s; cur.hold = seconds; cur.onClick = click; cur.tt = 0; cur.outAt = -1; cur.last = cur.sw.Elapsed.TotalSeconds;
			cur.Opacity = 0; cur.Top = cur.baseY + Dpi.Z(24);
			if (!cur.Visible) cur.Show();
			cur.tm.Start(); cur.Invalidate();
		}
		catch (Exception ex) { Program.LogErr(ex); cur = null; }
	}
	// 미리 만들어 두면 처음 눌렀을 때 끊기지 않아요
	public static void Prepare() { try { if (cur == null || cur.IsDisposed) { cur = new Bubble("", 1); IntPtr h = cur.Handle; } } catch (Exception ex) { Program.LogErr(ex); } }

	[System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); int none = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(Handle, 34, ref none, 4); } catch { }
	}
	private Bubble(string s, double seconds)
	{
		text = s; hold = seconds; mascot = Program.MascotImage();
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true; Opacity = 0;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
		Size = new Size(Dpi.Z(340), Dpi.Z(76));
		Rectangle wa = Screen.PrimaryScreen.WorkingArea;
		baseY = wa.Bottom - Height - Dpi.Z(16);
		Location = new Point(wa.Right - Width - Dpi.Z(16), baseY + Dpi.Z(24));
		Cursor = Cursors.Hand; Click += delegate { Action a = onClick; onClick = null; if (outAt < 0) outAt = tt; if (a != null) { try { a(); } catch (Exception ex) { Program.LogErr(ex); } } };
		tm.Tick += delegate { Step(); };
		Shown += delegate { sw.Restart(); last = 0; tt = 0; tm.Start(); };
	}
	private void Step()
	{
		if (IsDisposed) { tm.Stop(); return; }
		double now = sw.Elapsed.TotalSeconds; tt += Math.Min(0.1, now - last); last = now;
		double inK = Math.Min(1.0, tt / 0.35); double e = 1 - Math.Pow(1 - inK, 3);
		if (outAt < 0 && tt > hold) outAt = tt;
		double a = e;
		if (outAt >= 0) { double k = (tt - outAt) / 0.3; if (k >= 1) { tm.Stop(); Opacity = 0; Hide(); return; } a = e * (1 - k); }
		Opacity = Math.Max(0.0, Math.Min(1.0, a));
		int y = baseY + (int)Math.Round(Dpi.Z(24) * (1 - e)); if (y != Top) Top = y;
		Invalidate();
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(Theme.Bg); Wash.GlowAt(g, Size, Point.Empty);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		using (SolidBrush b = new SolidBrush(Theme.AccentSoft)) g.FillEllipse(b, Dpi.Zf(12), Dpi.Zf(10), Dpi.Zf(56), Dpi.Zf(56));
		if (mascot != null) { float h = Dpi.Zf(52), w = h * mascot.Width / mascot.Height; g.DrawImage(mascot, Dpi.Zf(40) - w / 2, Dpi.Zf(12) + (float)Math.Sin(tt * 6) * Dpi.Zf(1.2f), w, h); }
		Gfx.Text(g, text, Theme.F(11.5f, 1), Theme.Ink, new RectangleF(Dpi.Zf(80), Dpi.Zf(8), Width - Dpi.Zf(92), Height - Dpi.Zf(16)), StringAlignment.Near, StringAlignment.Center, true);
	}
	protected override void Dispose(bool d) { if (d) tm.Stop(); base.Dispose(d); }
}
