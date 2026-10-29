using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 트레이 위로 잠깐 올라오는 요약 카드 (현재 시각 · 이번 달 초과근무)
internal class TrayToast : AlertForm
{
	private SkyBanner sky;
	private FrameTimer tm;
	private DateTime born = DateTime.Now, closeAt;
	private bool closing; private DateTime closeStart;
	private int baseTop;
	private bool quit, hourMode; private double span = 30.0; private DateTime quitAt; private TLabel lA, lB, lC, lD; private ProgBar pBar; private string lastB = "";
	private static int Z(int v) { return Dpi.Z(v); }

	public TrayToast(string today, string month, int mon, int lim, string sum, string next, DateTime? quitTime = null, bool hourMode = false)
	{
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
		StartPosition = FormStartPosition.Manual; BackColor = Theme.Bg;
		ClientSize = new Size(Z(330), Z(226));
		Font = Fonts.Native(9f);

		WashPanel wp = new WashPanel(); wp.BackColor = Theme.Bg; wp.Dock = DockStyle.Fill; Controls.Add(wp);
		DateTime n = DateTime.Now;
		sky = new SkyBanner(); sky.Mascot = Program.MascotImage(); sky.Caption = Program.WarmCaption(n); sky.TimeText = n.ToString("HH:mm:ss"); sky.TimePt = 20f;
		sky.DateText = n.Month + "월 " + n.Day + "일 " + Records.Dow[(int)n.DayOfWeek] + "요일";
		sky.Location = new Point(0, 0); sky.Size = new Size(Z(330), Z(106));
		wp.Controls.Add(sky);

		TLabel a = new TLabel("이번 달 초과근무", 10f, 1, Theme.Sub, Theme.Bg); a.Location = new Point(Z(18), Z(116)); a.Size = new Size(Z(150), Z(22)); a.V = StringAlignment.Center;
		double fr = lim > 0 ? (double)mon / lim : 0;
		Color gc = fr >= 1.0 ? Theme.Red : (fr >= 0.75 ? Theme.Amber : Theme.Accent);
		TLabel b = new TLabel(mon + " / " + lim + "시간", 12f, 2, gc, Theme.Bg); b.Location = new Point(Z(170), Z(114)); b.Size = new Size(Z(142), Z(26)); b.H = StringAlignment.Far; b.V = StringAlignment.Center;
		ProgBar bar = new ProgBar(); bar.BackColor = Theme.Bg; bar.Frac = fr; bar.C = gc; bar.Location = new Point(Z(18), Z(144)); bar.Size = new Size(Z(294), Z(8));
		TLabel c = new TLabel(sum, 10f, 0, Theme.Sub, Theme.Bg); c.Location = new Point(Z(18), Z(160)); c.Size = new Size(Z(294), Z(22)); c.V = StringAlignment.Center;
		TLabel d = new TLabel("다음 알림  " + next, 11.5f, 2, Theme.Accent, Theme.Bg); d.Location = new Point(Z(18), Z(182)); d.Size = new Size(Z(294), Z(24)); d.V = StringAlignment.Center;
		wp.Controls.AddRange(new Control[] { b, a, bar, c, d });
		lA = a; lB = b; lC = c; lD = d; pBar = bar;
		if (quitTime.HasValue)
		{
			quit = true; quitAt = quitTime.Value; this.hourMode = hourMode;
			if (hourMode)
			{
				span = 10.0; sky.Caption = "정각 카운트다운";
				a.Text = quitAt.Hour + "시 초과근무 확인"; b.Fore = Theme.Accent; b.SetFont(15f, 2);
				c.Text = "곧 확인 시간이에요. 인사랑을 준비해 주세요"; d.Text = "누르면 닫혀요"; d.Fore = Theme.Muted; d.SetFont(10f, 0);
			}
			else
			{
			sky.Caption = "퇴근 카운트다운";
			a.Text = "퇴근 시간 " + quitAt.ToString("HH:mm"); b.Fore = Theme.Accent; b.SetFont(15f, 2);
			c.Text = "오늘도 수고 많으셨어요"; d.Text = "누르면 닫혀요"; d.Fore = Theme.Muted; d.SetFont(10f, 0);
			}
			bar.C = Theme.Accent;
		}
		wp.Click += delegate { Hide2(); };
		foreach (Control k in wp.Controls) k.Click += delegate { Hide2(); };

		Rectangle wa = Screen.PrimaryScreen.WorkingArea;
		baseTop = wa.Bottom - Height - Z(14);
		Location = new Point(wa.Right - Width - Z(14), baseTop + Z(30));
		Opacity = 0;
		closeAt = quitTime.HasValue ? quitTime.Value.AddSeconds(hourMode ? 4 : 8) : DateTime.Now.AddSeconds(30);
		tm = new FrameTimer(); tm.Interval = 16;
		tm.Tick += Tick;
		FormClosed += delegate { if (backup != null) { backup.Stop(); backup.Dispose(); backup = null; } if (tm != null) { tm.Stop(); tm.Dispose(); tm = null; } if (hi) { hi = false; Anim.HiRes(false); } };
	}

	[System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		// Windows 11: 시스템이 부드러운 둥근 모서리와 그림자를 직접 그려요 (테두리선 없음). 그 외에는 깔끔한 사각형
		try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); int none = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(Handle, 34, ref none, 4); } catch { }
	}
	private bool hi;
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int cx, uint f);
	private void KeepTop() { try { if (IsHandleCreated) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0053); } catch { } }   // 작업표시줄·다른 맨위 창 뒤로 묻히지 않게 (활성화 없이)
	private System.Windows.Forms.Timer backup; private DateTime lastTick = DateTime.Now; private int kt;
	public void Begin()
	{
		Anim.HiRes(true); hi = true; Show(); KeepTop(); tm.Start();
		// 화면 갱신 타이머가 늦거나 멈춰도 카드가 뜨고 닫히도록 보조 타이머를 둬요
		backup = new System.Windows.Forms.Timer(); backup.Interval = 120; backup.Tick += delegate { if ((DateTime.Now - lastTick).TotalMilliseconds > 250 && !IsDisposed) Tick(null, EventArgs.Empty); }; backup.Start();
	}

	private void Hide2() { if (!closing) { closing = true; closeStart = DateTime.Now; } }

	private void Tick(object s, EventArgs e)
	{
		lastTick = DateTime.Now; if ((++kt & 31) == 0) KeepTop();
		double t = (DateTime.Now - born).TotalMilliseconds;
		if (!closing)
		{
			double a = Anim.Effects ? Math.Min(1.0, t / 480.0) : 1.0, ease = 1.0 - Math.Pow(1.0 - a, 5.0);
			Opacity = Math.Min(1.0, a * 2.2); Top = baseTop + (int)(Z(30) * (1.0 - ease));
			DateTime nw = DateTime.Now;
			string ts = nw.ToString("HH:mm:ss"); if (ts != sky.TimeText) sky.TimeText = ts;
			if (quit)
			{
				double left = (quitAt - nw).TotalSeconds;
				string bt = left > 0 ? "0:" + Math.Min(59, (int)Math.Ceiling(left)).ToString("00") : (hourMode ? "지금!" : "퇴근!");
				if (bt != lastB)
				{
					lastB = bt; lB.Text = bt;
					if (left <= 0) { if (hourMode) { lA.Text = quitAt.Hour + "시예요"; lB.Text = "지금!"; lC.Text = "인사랑에서 확인을 눌러 주세요"; } else { lA.Text = "퇴근 시간이에요"; lC.Text = "수고하셨어요. 조심히 들어가세요"; } lB.Fore = Theme.Green; pBar.C = Theme.Green; }
				}
				pBar.Frac = Math.Max(0, Math.Min(1, left / span));
			}
			else if (Bounds.Contains(Cursor.Position)) closeAt = DateTime.Now.AddSeconds(10);
			if (DateTime.Now >= closeAt) Hide2();
			sky.Pulse();
		}
		else
		{
			double b = Math.Min(1.0, (DateTime.Now - closeStart).TotalMilliseconds / 250.0);
			Opacity = 1.0 - b; Top = baseTop + (int)(Z(20) * b);
			if (b >= 1.0) { tm.Stop(); Close(); }
		}
	}
}

internal partial class Program
{
	// 시간대·요일에 맞춰 마룡이가 건네는 한마디 (배너 캡션)
	internal static string WarmCaption(DateTime n)
	{
		int h = n.Hour; bool we = n.DayOfWeek == DayOfWeek.Saturday || n.DayOfWeek == DayOfWeek.Sunday;
		if (we) return h < 12 ? "주말에도 고생이 많아요" : "주말 근무, 고마워요";
		if (h < 6) return "새벽까지 고생이 많아요";
		if (h < 9) return "좋은 아침이에요";
		if (h < 12) return "오전도 힘내봐요";
		if (h < 14) return "점심은 드셨나요?";
		if (h < 18) return "오후도 마룡이가 함께";
		if (h < 20) return "오늘도 수고했어요";
		return "늦게까지 고마워요";
	}
	private static TrayToast toast;
	// 요약 카드가 떠 있는 동안에는 설정 창 홈 배너를 쉬게 해요 (두 창이 동시에 계속 다시 그리면 4K 에서 부담이 커져요)
	internal static bool ToastActive { get { try { return toast != null && !toast.IsDisposed && toast.Visible; } catch { return false; } } }

	internal static void CloseToast()
	{
		try { if (toast != null && !toast.IsDisposed) toast.Close(); } catch { }
		toast = null;
	}

	internal static void ShowQuitToast(DateTime q, bool hourMode = false)
	{
		try
		{
			CloseToast();
			DateTime n = DateTime.Now;
			TrayToast t = new TrayToast("", "", Records.CountMonth(n.Year, n.Month), monthlyLimit, "", "", q, hourMode);
			t.FormClosed += delegate { if (toast == t) toast = null; };
			toast = t;
			t.Begin();
		}
		catch (Exception ex) { LogError(ex); }
	}

	internal static void ShowStatusToast()
	{
		try
		{
			CloseToast();
			DateTime n = DateTime.Now;
			int mon = Records.CountMonth(n.Year, n.Month), day = Records.CountOn(n);
			DateTime na = NextAlertAt();
			string next = na == DateTime.MinValue ? "없음" : na.ToString("HH:mm") + " (" + RelText(na - n) + ")";
			TrayToast t = new TrayToast("", "", mon, monthlyLimit, "오늘 " + day + "시간 · 올해 " + Records.CountYear(n.Year) + " / " + YearLimit + "시간", next);
			t.FormClosed += delegate { if (toast == t) toast = null; };
			toast = t;
			t.Begin();
		}
		catch (Exception ex) { LogError(ex); }
	}
}
