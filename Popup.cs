using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class CheckBadge : Ctl
{
	public Color C = Theme.Green;
	public CheckBadge() { BackColor = Theme.Bg; }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(BackColor);
		float d = Math.Min(Width, Height) - 2;
		using (SolidBrush b = new SolidBrush(Theme.Mix(Theme.Bg, C, 0.14))) g.FillEllipse(b, 1, 1, d, d);
		using (Pen p = new Pen(C, Dpi.Zf(2.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			g.DrawLines(p, new PointF[] { new PointF(d * 0.29f + 1, d * 0.53f + 1), new PointF(d * 0.45f + 1, d * 0.68f + 1), new PointF(d * 0.73f + 1, d * 0.35f + 1) });
	}
}

// ─────────────────────────────────────────────────────────────
//  알림 팝업 — 시간대 하늘 배너 · 마감 카운트다운 · 별 타임라인 · 다시 알림
// ─────────────────────────────────────────────────────────────
internal class AlertPopup : AlertForm
{
	public Slot Slot;
	public bool Closing;

	private SkyBanner sky;
	private CloseBtn xb;
	private TLabel title, instr, starCap, todayLbl, snoozeCap, soundLbl, resTitle, resNote, autoLbl;
	private CountdownBar bar;
	private StarRow stars;
	private TButton ok, s5, s10, s15, skip, dndBtn, viewBtn, closeBtn;
	private TToggle snd; private TButton sizeBtn; private bool compact, forcedSmall;
	private CheckBadge badge;
	private Card[] cards = new Card[3];
	private TLabel[] cCap = new TLabel[3], cVal = new TLabel[3];
	private FrameTimer anim;
	private Stopwatch inSw = Stopwatch.StartNew();
	private Stopwatch outSw = new Stopwatch();
	private int ty, tick;
	private Stopwatch nudgeSw = new Stopwatch(); private int nudgeX; private DateTime nextNudge = DateTime.Now.AddSeconds(14), nextHop = DateTime.Now.AddSeconds(7);
	private bool dragged, dragging; private Point dragCur, dragLoc;
	private static Point? lastLoc;   // 마지막으로 옮긴 위치 기억
	private int lastSec = -1, lastState = -1;
	private bool resultView, again;
	private DateTime closeAt;
	private Control[] alertGroup, resultGroup;

	private static int Z(int v) { return Dpi.Z(v); }
	private const int W = 440, H1 = 554, H2 = 388, M = 0;   // M: 테두리 선 여백
	private Panel inner;

	private void Place(Control c, int x, int y, int w, int h) { c.Location = new Point(Z(x), Z(y)); c.Size = new Size(Z(w), Z(h)); }

	public AlertPopup(Slot s, bool snoozedAgain)
	{
		Slot = s; again = snoozedAgain;
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		TopMost = true;
		StartPosition = FormStartPosition.Manual;
		BackColor = Theme.Bg;
		ClientSize = new Size(Z(W + M * 2), Z(H1 + M * 2));
		Font = Fonts.Native(9f);
		inner = new WashPanel(); inner.BackColor = Theme.Bg; inner.Location = new Point(Z(M), Z(M)); inner.Size = new Size(Z(W), Z(H1));

		sky = new SkyBanner(); sky.Mascot = Program.MascotImage(); Place(sky, 0, 0, W, 112); sky.ShowPagoda = false;
		xb = new CloseBtn(); xb.Location = new Point(Z(W) - Z(40), Z(10)); sky.Controls.Add(xb);

		title = new TLabel("", 17f, 2, Theme.Ink, Theme.Bg); Place(title, 28, 130, 384, 32); title.V = StringAlignment.Center;
		instr = new TLabel("확인을 누르면 새올에 로그인이 되어있는 경우\n인사랑으로 이동해요", 10.5f, 0, Theme.Sub, Theme.Bg); Place(instr, 28, 160, 384, 42); instr.V = StringAlignment.Center;
		bar = new CountdownBar(); bar.BackColor = Theme.Bg; Place(bar, 28, 204, 384, 54);

		starCap = new TLabel("오늘의 기록", 10.5f, 0, Theme.Muted, Theme.Bg); Place(starCap, 28, 282, 140, 20);
		todayLbl = new TLabel("", 10.5f, 0, Theme.Sub, Theme.Bg); Place(todayLbl, 192, 282, 220, 20); todayLbl.H = StringAlignment.Far;
		stars = new StarRow(); stars.BackColor = Theme.Bg; Place(stars, 28, 304, 384, 48);

		ok = new TButton("확인했어요", BK.Primary); ok.Pt = 13f; ok.Weight = 2; Place(ok, 28, 366, 384, 50);
		snoozeCap = new TLabel("나중에 다시 알림", 10.5f, 0, Theme.Muted, Theme.Bg); Place(snoozeCap, 28, 436, 200, 20); snoozeCap.V = StringAlignment.Center;
		s5 = new TButton("5분 뒤", BK.Soft); Place(s5, 28, 462, 72, 36);
		s10 = new TButton("10분 뒤", BK.Soft); Place(s10, 108, 462, 78, 36);
		s15 = new TButton("15분 뒤", BK.Soft); Place(s15, 194, 462, 78, 36);
		skip = new TButton("이번 시간 무시", BK.Outline); Place(skip, 280, 462, 132, 36);
		foreach (TButton b in new TButton[] { ok, s5, s10, s15, skip }) { b.BackColor = Theme.Bg; }
		foreach (TButton b in new TButton[] { s5, s10, s15 }) b.Pt = 10.5f; skip.Pt = 10f;
		ok.Shimmer = Anim.Effects;

		snd = new TToggle(); snd.SetImmediate(Program.SoundOn); snd.BackColor = Theme.Bg; snd.Location = new Point(Z(332), Z(437)); snd.Size = new Size(Z(34), Z(20));
		snd.CheckedChanged += delegate { Program.SoundOn = snd.Checked; };
		soundLbl = new TLabel("알림음", 10.5f, 0, Theme.Sub, Theme.Bg); Place(soundLbl, 372, 436, 50, 22); soundLbl.V = StringAlignment.Center;
		sizeBtn = new TButton("작게 보기", BK.Outline); sizeBtn.BackColor = Theme.Bg; sizeBtn.Pt = 10f; Place(sizeBtn, 226, 510, 186, 32);
		dndBtn = new TButton("방해 금지 켜기", BK.Outline); dndBtn.BackColor = Theme.Bg; dndBtn.Pt = 10f; Place(dndBtn, 28, 510, 186, 32);
		dndBtn.Click += delegate { if (Closing || resultView) return; Program.DndFromPopup(Slot); Dismiss(); };
		sizeBtn.Click += delegate { Program.popupSmall = !Program.popupSmall; try { Program.SaveSettingsPublic(); } catch { } RefreshCompact(); SetGroups(!resultView); };

		// 결과 화면
		badge = new CheckBadge(); Place(badge, 28, 132, 44, 44);
		resTitle = new TLabel("", 14.5f, 2, Theme.Green, Theme.Bg); Place(resTitle, 84, 130, 328, 26); resTitle.V = StringAlignment.Center;
		resNote = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); Place(resNote, 84, 156, 328, 22); resNote.V = StringAlignment.Center;
		for (int i = 0; i < 3; i++)
		{
			Card c = new Card(); Place(c, 28 + i * 132, 196, 120, 76); cards[i] = c; c.Ico = i == 0 ? "ms_of_cal" : (i == 1 ? "ms_of_clock" : "ms_of_bind");
			cCap[i] = new TLabel("", 9f, 0, Theme.Muted, Theme.Bg); cCap[i].Location = new Point(Z(14), Z(33)); cCap[i].Size = new Size(Z(104), Z(16));
			cVal[i] = new TLabel("", 13.5f, 2, Theme.Ink, Theme.Bg); cVal[i].Location = new Point(Z(14), Z(49)); cVal[i].Size = new Size(Z(104), Z(24)); cVal[i].V = StringAlignment.Center;
			c.Controls.Add(cCap[i]); c.Controls.Add(cVal[i]);
		}
		viewBtn = new TButton("기록 보기", BK.Primary); viewBtn.Pt = 11.5f; viewBtn.Weight = 2; Place(viewBtn, 28, 292, 200, 48); viewBtn.BackColor = Theme.Bg;
		closeBtn = new TButton("닫기", BK.Outline); closeBtn.Pt = 11f; Place(closeBtn, 238, 292, 100, 48); closeBtn.BackColor = Theme.Bg;
		autoLbl = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); Place(autoLbl, 28, 352, 384, 20);

		alertGroup = new Control[] { title, instr, bar, starCap, todayLbl, stars, snoozeCap, ok, s5, s10, s15, skip, snd, soundLbl, sizeBtn, dndBtn };
		resultGroup = new Control[] { badge, resTitle, resNote, cards[0], cards[1], cards[2], viewBtn, closeBtn, autoLbl };
		inner.Controls.Add(sky);
		inner.Controls.AddRange(alertGroup); sizeBtn.BringToFront(); dndBtn.BringToFront();
		inner.Controls.AddRange(resultGroup);
		Controls.Add(inner);

		ok.Click += delegate { Confirm(); };
		s5.Click += delegate { Snooze(5); };
		s10.Click += delegate { Snooze(10); };
		s15.Click += delegate { Snooze(15); };
		skip.Click += delegate { Skip(); };
		xb.Click += delegate { if (!resultView && !Closing) Program.OnPopupIgnored(Slot); Dismiss(); };
		closeBtn.Click += delegate { Dismiss(); };
		viewBtn.Click += delegate { Program.ShowHistory(); Dismiss(); };

		ApplyRegion();
		Paint += GlowPaint;
		KeyPreview = true;
		KeyDown += delegate(object ko, KeyEventArgs ke)
		{
			if (resultView || Closing) { if (ke.KeyCode == Keys.Escape || ke.KeyCode == Keys.Enter) Dismiss(); return; }
			if (ke.KeyCode == Keys.Enter || ke.KeyCode == Keys.Space) { Confirm(); ke.Handled = true; }
			else if ((ke.KeyCode == Keys.D1 || ke.KeyCode == Keys.NumPad1) && s5.Enabled) Snooze(5);
			else if ((ke.KeyCode == Keys.D2 || ke.KeyCode == Keys.NumPad2) && s10.Enabled) Snooze(10);
			else if ((ke.KeyCode == Keys.D3 || ke.KeyCode == Keys.NumPad3) && s15.Enabled) Snooze(15);
		};

		Relocate();
		Opacity = 0.0;
		HookDrag(this);

		ApplySlot();

		anim = new FrameTimer();
		anim.Interval = 16;
		anim.Tick += AnimTick;
		FormClosed += delegate
		{
			if (anim != null) { anim.Stop(); anim.Dispose(); anim = null; }
			if (hi) { hi = false; Anim.HiRes(false); }
			Glow.Hide();
		};
	}

	[System.Runtime.InteropServices.DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
	private bool nativeRound;
	private const float R = 12f;   // 모서리 반경 (윈도우 11이 아닐 때만 쓰고, 11에서는 시스템이 안티앨리어싱된 둥근 모서리를 그려요)

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		// Windows 11: 시스템이 직접 안티앨리어싱된 둥근 모서리 + 그림자를 그려줌 (실패하면 Region 방식)
		try
		{
			int pref = 2;   // DWMWCP_ROUND
			nativeRound = DwmSetWindowAttribute(Handle, 33, ref pref, 4) == 0;
			if (nativeRound)
			{
				int none = unchecked((int)0xFFFFFFFE);
				DwmSetWindowAttribute(Handle, 34, ref none, 4);   // 시스템 테두리선 없음 (안쪽 얇은 선만 직접 그림)
			}
		}
		catch { nativeRound = false; }
		ApplyRegion();
	}

	// 빈 곳(배경·글자·배너)을 잡고 끌면 창 이동. 버튼·토글은 제외
	private void HookDrag(Control c)
	{
		if (c is TButton || c is TToggle || c is CloseBtn) return;
		c.MouseDown += DragDown; c.MouseMove += DragMove; c.MouseUp += DragUp;
		foreach (Control k in c.Controls) HookDrag(k);
	}
	private void DragDown(object o, MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Left || Closing) return;
		dragging = true; dragCur = Cursor.Position; dragLoc = Location;
		((Control)o).Capture = true;
	}
	private void DragMove(object o, MouseEventArgs e)
	{
		if (!dragging) return;
		Point c = Cursor.Position;
		Location = new Point(dragLoc.X + c.X - dragCur.X, dragLoc.Y + c.Y - dragCur.Y);
	}
	private void DragUp(object o, MouseEventArgs e)
	{
		if (!dragging) return;
		dragging = false; ((Control)o).Capture = false;
		dragged = true; ty = Top; lastLoc = new Point(Left, Top);
		if (Opacity < 1.0) Opacity = 1.0;
	}

	private void ApplyRegion()
	{
		try
		{
			if (nativeRound) { Region = null; }
			else { Region = null; }   // 윈도우 11이 아니면 깨진 모서리 대신 깔끔한 사각형
			inner.Region = null;
		}
		catch { }
	}

	protected override void OnResize(EventArgs e) { base.OnResize(e); if (Created || IsHandleCreated) ApplyRegion(); }


	private void GlowPaint(object o, PaintEventArgs pe)
	{
		Graphics g = pe.Graphics; Gfx.HQ(g);
		g.Clear(Theme.Bg);
	}

	private void Relocate()
	{
		dragged = false;
		Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
		// 작은 화면(노트북 1366x768 · 배율 125~150%)에서도 아래 버튼이 작업표시줄에 가리지 않게, 화면 안으로 맞춰요
		int x = Math.Max(wa.Left, Math.Min(wa.Right - Width, wa.Left + (wa.Width - Width) / 2));
		ty = Math.Max(wa.Top, Math.Min(wa.Bottom - Height - Z(6), wa.Top + (wa.Height - Height) / 2));
		Location = new Point(x, ty + Z(40));
		if (lastLoc.HasValue)
		{
			Point lp = lastLoc.Value;
			Rectangle vis = Rectangle.Empty;
			foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.Contains(new Point(lp.X + Width / 2, lp.Y + Z(40)))) vis = sc.WorkingArea;
			if (!vis.IsEmpty) { dragged = true; int lx = Math.Max(vis.Left, Math.Min(vis.Right - Width, lp.X)); ty = Math.Max(vis.Top, Math.Min(vis.Bottom - Height - Z(6), lp.Y)); Location = new Point(lx, ty + Z(40)); }
		}
	}

	// ── 미리 만들어 두기: 알림이 뜰 때 컨트롤 40여 개를 만드느라 멈칫하지 않도록, 한가할 때 숨겨서 만들어 둬요 ──
	private static AlertPopup spare; private bool quiet;
	protected override bool ShowWithoutActivation { get { return quiet; } }
	public static void Prebuild()
	{
		try
		{
			if (spare != null && !spare.IsDisposed) return;
			DateTime n = DateTime.Now;
			AlertPopup p = new AlertPopup(new Slot { At = n, Start = new DateTime(n.Year, n.Month, n.Day, n.Hour, 0, 0), Test = true }, false);
			p.quiet = true; p.Opacity = 0.0; p.Show(); p.Invalidate(true); p.Update(); p.Hide(); p.quiet = false;
			spare = p;
		}
		catch (Exception ex) { Program.LogErr(ex); spare = null; }
	}
	public static void PrebuildLater(int ms)
	{
		System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = ms;
		t.Tick += delegate { t.Stop(); t.Dispose(); Prebuild(); };
		t.Start();
	}
	public static void DropSpare() { try { if (spare != null && !spare.IsDisposed) { spare.Dispose(); } } catch { } spare = null; }
	public static AlertPopup TakeSpare(Slot s, bool snoozedAgain)
	{
		AlertPopup p = spare; spare = null;
		if (p == null || p.IsDisposed) return null;
		try
		{
			p.inSw.Restart(); p.tick = 0; p.Closing = false; p.outSw.Reset(); p.nudgeSw.Reset(); p.dragging = false;
			p.nextNudge = DateTime.Now.AddSeconds(14); p.nextHop = DateTime.Now.AddSeconds(7);
			p.Opacity = 0.0; p.Relocate(); p.SetSlot(s, snoozedAgain);
			return p;
		}
		catch { try { p.Dispose(); } catch { } return null; }
	}

	public void Begin() { Anim.HiRes(true); hi = true; Show(); anim.Start(); }
	private bool hi;

	public void SetSlot(Slot s, bool snoozedAgain)
	{
		Slot = s; again = snoozedAgain; lastSec = -1; lastState = -1;
		ApplySlot();
		sky.Restart();
		TopMost = false; TopMost = true;
	}

	// 작게 보기: '오늘의 기록' 줄을 접어서 높이를 줄여요. 화면이 낮은 PC에서는 자동으로 작게 나와요
	private const int CompactCut = 82;
	private void RefreshCompact()
	{
		Rectangle wa = Screen.FromPoint(IsHandleCreated ? new Point(Left + Width / 2, Top + Height / 2) : Cursor.Position).WorkingArea;
		forcedSmall = wa.Height < Z(H1 + M * 2) + Z(24);
		compact = Program.popupSmall || forcedSmall;
		sizeBtn.Text = compact ? "크게 보기" : "작게 보기";
	}
	private void SetGroups(bool alert)
	{
		if (!resultView && alert) RefreshCompactIfNeeded();
		foreach (Control c in alertGroup) c.Visible = alert;
		foreach (Control c in resultGroup) c.Visible = !alert;
		if (alert)
		{
			int d = compact ? CompactCut : 0;
			starCap.Visible = todayLbl.Visible = stars.Visible = !compact;
			sizeBtn.Visible = !forcedSmall;
			ok.Top = Z(366 - d); snoozeCap.Top = Z(436 - d); soundLbl.Top = Z(436 - d); snd.Top = Z(437 - d); sizeBtn.Top = Z(510 - d); dndBtn.Top = Z(510 - d); dndBtn.Width = Z(forcedSmall ? 384 : 186);
			s5.Top = Z(462 - d); s10.Top = Z(462 - d); s15.Top = Z(462 - d); skip.Top = Z(462 - d);
		}
		int hh = alert ? (compact ? H1 - CompactCut : H1) : H2;
		inner.Size = new Size(Z(W), Z(hh));
		ClientSize = new Size(Z(W + M * 2), Z(hh + M * 2));
		ApplyRegion();
		sky.Height = Z(112);
		// 높이가 바뀌어도 작업표시줄에 가리지 않게 화면 안으로
		try
		{
			if (!dragging)
			{
				Rectangle wa = Screen.FromPoint(new Point(Left + Width / 2, ty + Height / 2)).WorkingArea;
				ty = Math.Max(wa.Top, Math.Min(wa.Bottom - Height - Z(6), ty));
				if (Opacity >= 1.0) Top = ty;
			}
		}
		catch { }
		Invalidate();
	}
	private void RefreshCompactIfNeeded() { RefreshCompact(); }

	private void ApplySlot()
	{
		resultView = false; sky.StampClear();
		SetGroups(true);
		DateTime n = Slot.At;
		sky.Caption = again ? "마룡이가 다시 알려드려요" : (Slot.Test ? "마룡이가 알려드려요" : Program.WarmCaption(DateTime.Now));
		sky.TimeText = n.ToString("HH:mm");
		sky.DateText = n.Month + "월 " + n.Day + "일 " + Records.Dow[(int)n.DayOfWeek] + "요일";
		sky.TimePt = 32f;
		title.Text = Slot.Test ? "초과근무 알림 테스트" : Slot.Hour + "시 초과근무 확인 시간이에요";
		bar.SlotHour = Slot.Hour;
		ok.Text = Slot.Test ? "확인 (테스트)" : "확인했어요";
		RefreshToday();
		BuildStars();
		TickCountdown(true);
		sky.Invalidate();
	}

	private void BuildStars()
	{
		stars.Items.Clear();
		for (int k = -4; k <= 1; k++)
		{
			DateTime st = Slot.Start.AddHours(k);
			StarItem it = new StarItem();
			it.Label = st.Hour + "시";
			bool rec = Records.IsConfirmed(st);
			if (rec) it.State = 1;
			else if (k == 0) it.State = 3;
			else if (k < 0) it.State = 2;
			else it.State = 0;
			stars.Items.Add(it);
		}
		stars.Invalidate();
	}

	private void RefreshToday()
	{
		int n = Records.CountOn(Slot.Start.Date);
		todayLbl.Text = n > 0 ? n + "회 · " + Records.Hours(n) + " 기록" : "아직 기록 없음";
	}

	private void TickCountdown(bool force)
	{
		TimeSpan left = Slot.Deadline - DateTime.Now;
		int sec = (int)Math.Ceiling(left.TotalSeconds);
		int st = Theme.StateFor(left);
		if (force || sec != lastSec)
		{
			lastSec = sec;
			bar.Left = left; bar.State = st;
			bar.Invalidate();
			s5.Enabled = left.TotalMinutes > 5 + 1;
			s10.Enabled = left.TotalMinutes > 10 + 1;
			s15.Enabled = left.TotalMinutes > 15 + 1;
		}
		if (force || st != lastState)
		{
			lastState = st;
			if (!resultView) { if (st == 3) Glow.FadeOut(); else Glow.SetState(st); }
		}
	}

	private void AnimTick(object sender, EventArgs e)
	{
		tick++;
		bool fx = Anim.Effects;
		if (!Closing)
		{
			double a = fx ? Math.Min(1.0, inSw.Elapsed.TotalMilliseconds / 520.0) : 1.0;
			double ease = Ease.OutBack((float)a);
			double fade = Math.Min(1.0, a * 2.2);
			if (a < 1.0 || Opacity < 1.0)
			{
				if (!dragging) Top = ty + (int)(Z(40) * (1.0 - ease));
				Opacity = fade;
			}
			if (!resultView)
			{
				TickCountdown(false);
				DateTime nw = DateTime.Now;
				if (nw >= nextHop) { sky.Restart(); nextHop = nw.AddSeconds(7); }
				if (fx && nw >= nextNudge && !dragging && a >= 1.0 && Opacity >= 1.0)
				{
					int stt = Theme.StateFor(Slot.Deadline - nw);
					nextNudge = nw.AddSeconds(stt >= 2 ? 12 : (stt == 1 ? 18 : 28));
					nudgeX = Left; nudgeSw.Restart();
				}
			}
			else
			{
				if (Bounds.Contains(Cursor.Position)) closeAt = DateTime.Now.AddSeconds(8);
				int rem = (int)Math.Ceiling((closeAt - DateTime.Now).TotalSeconds);
				autoLbl.Text = rem > 0 ? rem + "초 뒤 자동으로 닫혀요" : "";
				if (rem <= 0) Dismiss();
			}
		}
		if (nudgeSw.IsRunning)
		{
			if (dragging) nudgeSw.Reset();
			else
			{
				double nt = nudgeSw.Elapsed.TotalSeconds;
				if (nt > 0.8) { Left = nudgeX; nudgeSw.Reset(); }
				else Left = nudgeX + (int)(Math.Sin(nt * 44) * Z(9) * Math.Exp(-nt * 4.2));
			}
		}
		if (Closing)
		{
			double b = fx ? Math.Min(1.0, outSw.Elapsed.TotalMilliseconds / 260.0) : 1.0;
			Top = ty + (int)(Z(26) * b * b * b);
			Opacity = 1.0 - b * b;
			if (b >= 1.0) { anim.Stop(); Close(); return; }
		}
		sky.Pulse();
	}

	public void Dismiss()
	{
		// 이미 닫히는 중인데 또 누르면 바로 닫아요 (닫힘 애니메이션이 멈춰서 창이 남아 있는 경우에도 지울 수 있게)
		if (Closing) { try { anim.Stop(); Close(); } catch { } return; }
		Closing = true;
		outSw.Start();
		Glow.FadeOut();
		// 혹시 애니메이션이 돌지 않아도 0.7초 뒤에는 꼭 닫혀요
		System.Windows.Forms.Timer hard = new System.Windows.Forms.Timer(); hard.Interval = 700;
		hard.Tick += delegate { hard.Stop(); hard.Dispose(); try { if (!IsDisposed && Closing) { anim.Stop(); Close(); } } catch { } };
		hard.Start();
	}
	// 닫히는 중인 창도 다시 누르면 바로 사라져요
	protected override void OnMouseDown(MouseEventArgs e) { if (Closing) { Dismiss(); return; } base.OnMouseDown(e); }

	private void Snooze(int min)
	{
		if (Closing) { Dismiss(); return; }
		Program.DoSnooze(Slot, min);
		Dismiss();
	}

	private void Skip()
	{
		if (Closing) { Dismiss(); return; }
		if (resultView) return;
		Program.SkipSlot(Slot);
		Dismiss();
	}

	private void Confirm()
	{
		if (Closing) { Dismiss(); return; }
		if (resultView) { Dismiss(); return; }
		bool added = false, dup = false;
		if (!Slot.Test)
		{
			if (Records.IsConfirmed(Slot.Start)) dup = true;
			else { Records.BeginOp(); added = Records.Add(Slot, DateTime.Now); }
		}
		Program.CancelSnooze();
		if (Program.InsarangOnConfirm) Program.OpenInsarangAuto();   // '확인했어요'를 누르면 (테스트 알림에서도) 인사랑으로 바로 이동
		ShowResult(added, dup);
		AlertBanner.Hide();
		if (added || Slot.Test) { sky.StampStart(); sky.Burst(); Confetti.Fire(Bounds); }
		Glow.FadeOut();
	}

	private void ShowResult(bool added, bool dup)
	{
		resultView = true;
		SetGroups(false);
		string h = Slot.Hour + "시";
		badge.C = Theme.Green; resTitle.Fore = Theme.Green;
		if (Slot.Test) { resTitle.Text = "테스트 알림이었어요"; resNote.Text = "테스트라서 기록은 저장하지 않았어요."; }
		else if (dup) { resTitle.Text = "이미 기록된 시간대예요"; resNote.Text = h + " 확인은 한 번만 기록돼요."; }
		else if (added)
		{
			resTitle.Text = h + " 확인, 기록했어요";
			int lv; DateTime dd0 = Slot.Start.Date;
			resNote.Text = Program.CareText(dd0, out lv);
			resNote.Fore = lv >= 3 ? Theme.Red : (lv >= 1 ? Theme.Amber : Theme.Sub);
		}
		else { resTitle.Text = "기록을 저장하지 못했어요"; resNote.Text = "기록 파일을 쓸 수 없었어요. error.log를 확인해 주세요."; resTitle.Fore = Theme.Red; badge.C = Theme.Red; }
		badge.Invalidate();
		sky.Caption = "마룡이가 기록했어요";
		sky.TimeText = "기록 완료";
		sky.TimePt = 26f;
		DateTime d = Slot.Start.Date;
		int day = Records.CountOn(d);
		int mon = Records.CountMonth(d.Year, d.Month);
		int all = Records.CountAll();
		cCap[0].Text = d == DateTime.Today ? "오늘 " + Records.DayLabel(d) : Records.DayLabel(d); cVal[0].Text = Records.Hours(day);
		cCap[1].Text = d.Month + "월 누적"; cVal[1].Text = Records.Hours(mon);
		cCap[2].Text = "총 초과근무"; cVal[2].Text = Records.Hours(all);
		closeAt = DateTime.Now.AddSeconds(12);
		sky.Invalidate();
	}
}


// 간단 알림 카드: 오른쪽 아래에 작게. 확인했어요 · 5분 뒤 · 이번 시간 무시
internal sealed class AlertToast : AlertForm
{
	private static AlertToast cur;
	internal static bool IsOpen { get { try { return cur != null && !cur.IsDisposed && cur.Visible && !cur.done; } catch { return false; } } }
	private readonly Slot slot; private readonly TLabel head, sub; private readonly TButton ok, snz, skip;
	private readonly System.Windows.Forms.Timer tk = new System.Windows.Forms.Timer(); private bool done; private int fade; private DateTime closeAt = DateTime.MaxValue;
	private static int Z(int v) { return Dpi.Z(v); }
	internal static void ShowFor(Slot s, bool again)
	{
		try { if (cur != null && !cur.IsDisposed) { cur.Close(); } } catch { }
		cur = new AlertToast(s, again); cur.Show(); cur.KeepTop();
	}
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int cx, uint f);
	private void KeepTop() { try { if (IsHandleCreated) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0053); } catch { } }
	private AlertToast(Slot s, bool again)
	{
		slot = s; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; BackColor = Theme.Bg;
		ClientSize = new Size(Z(356), Z(128)); Font = Fonts.Native(9f);
		WashPanel wp = new WashPanel(); wp.BackColor = Theme.Bg; wp.Dock = DockStyle.Fill; Controls.Add(wp);
		head = new TLabel(s.Hour + "시 초과근무 확인할 시간이에요", 12f, 2, Theme.Ink, Theme.Bg); head.Location = new Point(Z(18), Z(14)); head.Size = new Size(Z(320), Z(26)); head.V = StringAlignment.Center;
		sub = new TLabel("", 10f, 0, Theme.Sub, Theme.Bg); sub.Location = new Point(Z(18), Z(42)); sub.Size = new Size(Z(320), Z(20)); sub.V = StringAlignment.Center;
		ok = new TButton("확인했어요", BK.Primary); ok.Pt = 11f; ok.Weight = 2; ok.Location = new Point(Z(18), Z(76)); ok.Size = new Size(Z(124), Z(38));
		snz = new TButton("5분 뒤", BK.Soft); snz.Pt = 10.5f; snz.Location = new Point(Z(150), Z(76)); snz.Size = new Size(Z(80), Z(38));
		skip = new TButton("이번 시간 무시", BK.Outline); skip.Pt = 10f; skip.Location = new Point(Z(238), Z(76)); skip.Size = new Size(Z(100), Z(38));
		foreach (TButton b in new TButton[] { ok, snz, skip }) { b.BackColor = Theme.Bg; wp.Controls.Add(b); }
		wp.Controls.Add(head); wp.Controls.Add(sub);
		ok.Click += delegate { Confirm(); };
		snz.Click += delegate { if (done) return; Program.DoSnooze(slot, 5); Finish(0); };
		skip.Click += delegate { if (done) return; Program.SkipSlot(slot); Finish(0); };
		snz.Enabled = (s.Deadline - DateTime.Now).TotalMinutes > 6;
		Rectangle wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(wa.Right - Width - Z(14), wa.Bottom - Height - Z(14));
		Opacity = 0; UpdateSub(again);
		tk.Interval = 40; tk.Tick += delegate { Tick(); }; tk.Start();
		FormClosed += delegate { tk.Stop(); tk.Dispose(); if (cur == this) cur = null; };
	}
	private void UpdateSub(bool again)
	{
		TimeSpan l = slot.Deadline - DateTime.Now; if (l < TimeSpan.Zero) l = TimeSpan.Zero;
		sub.Text = (again ? "다시 알림 · " : "") + "마감 " + slot.Deadline.ToString("HH:mm") + " (" + (int)Math.Ceiling(l.TotalMinutes) + "분 남음)";
	}
	private int sec = -1;
	private void Tick()
	{
		if (done) { if (DateTime.Now >= closeAt) { Opacity = Math.Max(0, Opacity - 0.12); if (Opacity <= 0.02) Close(); } return; }
		if (Opacity < 1.0) Opacity = Math.Min(1.0, Opacity + 0.12);
		int c = (int)((slot.Deadline - DateTime.Now).TotalSeconds / 20); if (c != sec) { sec = c; UpdateSub(false); }
		if (DateTime.Now >= slot.Deadline || Records.IsConfirmed(slot.Start)) Finish(0);
	}
	private void Finish(int ms) { done = true; closeAt = DateTime.Now.AddMilliseconds(ms); AlertBanner.Hide(); }
	private void Confirm()
	{
		if (done) return; bool added = false, dup = false;
		if (!slot.Test) { if (Records.IsConfirmed(slot.Start)) dup = true; else { Records.BeginOp(); added = Records.Add(slot, DateTime.Now); } }
		Program.CancelSnooze(); if (Program.InsarangOnConfirm) Program.OpenInsarangAuto();
		head.Text = dup ? "이미 기록된 시간대예요" : (added || slot.Test ? slot.Hour + "시 확인, 기록했어요" : "기록을 저장하지 못했어요"); head.Fore = added || slot.Test ? Theme.Green : Theme.Red;
		sub.Text = "수고하셨어요"; ok.Visible = snz.Visible = skip.Visible = false; Finish(1400);
	}
}
