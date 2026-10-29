using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

// 부드러운 값 하나 (목표값으로 미끄러지듯 따라감)
internal sealed class AV
{
	public float V, T;
	public float Rate = 16f;
	public Control Owner;
	public Action Step;
	internal bool On;
	public AV(Control owner, float v) { Owner = owner; V = T = v; }
	public void Set(float t)
	{
		T = t;
		if (!On && Math.Abs(T - V) > 0.0005f) { On = true; Anim.Add(this); }
	}
	public void Snap(float t) { V = T = t; if (Owner != null && !Owner.IsDisposed) Owner.Invalidate(); if (Step != null) Step(); }
}

internal static class Ease
{
	public static float OutCubic(float t) { t = 1 - t; return 1 - t * t * t; }
	public static float OutQuint(float t) { t = 1 - t; return 1 - t * t * t * t * t; }
	public static float InOutSine(float t) { return 0.5f - 0.5f * (float)Math.Cos(Math.PI * t); }
	public static float InOutCubic(float t) { return t < 0.5f ? 4 * t * t * t : 1 - (float)Math.Pow(-2 * t + 2, 3) / 2; }
	public static float OutBack(float t) { const float c1 = 1.35f, c3 = c1 + 1; t -= 1; return 1 + c3 * t * t * t + c1 * t * t; }
}

internal sealed class Tw
{
	internal double Dur, T0; internal Func<float, float> Ease; internal Action<float> Step; internal Action Done; internal bool Dead;
	public void Cancel() { Dead = true; }
}

// 화면 주사율(vsync)에 맞춰 도는 프레임 타이머 (팝업·토스트·홈 배너용)
internal sealed class FrameTimer
{
	public int Interval;   // 호환용 (무시됨: 화면 주사율에 맞춰 호출)
	public bool Throttle; internal double LastFire;   // Throttle: 게임 창이 열려 있는 동안엔 초당 4번만 (뒤쪽 화면이 게임과 CPU 를 나눠 쓰지 않게)
	public event EventHandler Tick;
	private bool on;
	public bool Enabled { get { return on; } }
	public void Start() { if (!on) { on = true; Anim.AddFrame(this); } }
	public void Stop() { if (on) { on = false; Anim.RemoveFrame(this); } }
	public void Dispose() { Stop(); }
	public static readonly List<long> FireStamps = new List<long>();
	internal void Fire() { if (PaintProf.On) lock (FireStamps) FireStamps.Add(Stopwatch.GetTimestamp()); EventHandler h = Tick; if (h != null) h(this, EventArgs.Empty); }
}

internal static class Anim
{
	[System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
	[System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);
	[System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmFlush();

	private static readonly List<AV> avs = new List<AV>();
	private static readonly List<Tw> tws = new List<Tw>();
	private static readonly List<FrameTimer> frames = new List<FrameTimer>();
	private static Timer backup;
	private static System.Threading.Thread vthread;
	private static readonly System.Threading.ManualResetEvent demand = new System.Threading.ManualResetEvent(false);
	private static System.Threading.SynchronizationContext ctx;
	private static int posted;
	private static Stopwatch sw = Stopwatch.StartNew();
	private static double last;
	private static int hiCount; private static bool hiOn;
	private static bool errLogged; internal static volatile bool GameBusy;
	// 끊김 기록 (문제를 찾을 때만): 설정 폴더에 perf.on 파일을 만들어 두면 50ms 넘게 끊긴 순간을 perf.log 에 적어요
	public static readonly bool PerfLog = CheckPerf();
	public static string PerfMark = "-";
	private static bool CheckPerf()
	{
		try
		{
			bool on = File.Exists(Path.Combine(Program.DataDir, "perf.on"));
			if (on) { string exe = System.Reflection.Assembly.GetExecutingAssembly().Location; PerfWrite("===== 시작 · 이 exe를 만든 시각 " + File.GetLastWriteTime(exe).ToString("MM-dd HH:mm") + " ====="); }
			return on;
		}
		catch { return false; }
	}
	public static void Mark(string s) { PerfMark = s; if (PerfLog) PerfWrite("동작: " + s); }
	public static void PerfNote(string s) { if (PerfLog) PerfWrite(s); }
	private static void PerfWrite(string s) { try { File.AppendAllText(Path.Combine(Program.DataDir, "perf.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); } catch { } }

	// 시스템 설정에서 '애니메이션 효과'를 끄면 움직임 없이 바로 바뀜 (접근성)
	public static bool Effects { get { try { if (Environment.GetEnvironmentVariable("OT_FX") == "1") return true; return SystemInformation.UIEffectsEnabled; } catch { return true; } } }

	// 타이머 정밀도를 1ms로 (움직임이 있는 동안만)
	public static void HiRes(bool on)
	{
		try { if (on) { if (hiCount++ == 0) timeBeginPeriod(1); } else if (hiCount > 0 && --hiCount == 0) timeEndPeriod(1); } catch { }
	}

	private static bool Busy { get { return avs.Count > 0 || tws.Count > 0 || frames.Count > 0; } }

	private static void Ensure()
	{
		if (ctx == null) { ctx = System.Threading.SynchronizationContext.Current; if (PerfLog) UiProf.Start(ctx); }
		if (backup == null) { backup = new Timer(); backup.Interval = 30; backup.Tick += delegate { if (sw.Elapsed.TotalSeconds - last > 0.045) Tick(); }; }
		if (!backup.Enabled) { last = sw.Elapsed.TotalSeconds; if (!hiOn) { hiOn = true; HiRes(true); } backup.Start(); }
		demand.Set();
		if (vthread == null && ctx != null)
		{
			vthread = new System.Threading.Thread(VLoop); vthread.IsBackground = true; vthread.Name = "vsync"; vthread.Start();
		}
	}

	// 화면 갱신(DWM)이 끝날 때마다 UI 스레드에 한 프레임을 알림 → 모니터가 120/144Hz면 그만큼 부드럽게
	private static void VLoop()
	{
		bool dwmOk = true;
		while (true)
		{
			demand.WaitOne();
			if (dwmOk) { try { if (DwmFlush() != 0) System.Threading.Thread.Sleep(8); } catch { dwmOk = false; } }
			else System.Threading.Thread.Sleep(8);
			if (System.Threading.Interlocked.Exchange(ref posted, 1) == 0)
				ctx.Post(delegate { System.Threading.Interlocked.Exchange(ref posted, 0); Tick(); }, null);
		}
	}

	internal static void Add(AV a) { avs.Add(a); Ensure(); }
	internal static void AddFrame(FrameTimer f) { frames.Add(f); Ensure(); }
	internal static void RemoveFrame(FrameTimer f) { frames.Remove(f); }

	// 정해진 시간 동안 0→1 진행 (easing 적용)
	public static Tw Run(int ms, Func<float, float> ease, Action<float> step, Action done)
	{
		Tw t = new Tw(); t.Dur = ms / 1000.0; t.T0 = sw.Elapsed.TotalSeconds; t.Ease = ease ?? Ease.OutCubic; t.Step = step; t.Done = done;
		if (!Effects) { t.Dead = true; step(1f); if (done != null) done(); return t; }
		tws.Add(t); Ensure();
		step(0f);
		return t;
	}

	private static void Safe(Action a)
	{
		try { a(); } catch (Exception ex) { if (!errLogged) { errLogged = true; try { Program.LogErr(ex); } catch { } } }
	}

	private static void Tick()
	{
		double now = sw.Elapsed.TotalSeconds, gapRaw = now - last, dt = Math.Min(0.05, Math.Max(0.0005, gapRaw)); last = now;
		if (PerfLog && gapRaw > 0.05 && gapRaw < 5) PerfWrite("끊김 " + (int)(gapRaw * 1000) + "ms (직전 동작: " + PerfMark + ")");
		double tickStart = now;
		for (int i = avs.Count - 1; i >= 0; i--)
		{
			if (i >= avs.Count) continue;
			AV a = avs[i];
			if (a.Owner != null && a.Owner.IsDisposed) { a.On = false; avs.RemoveAt(i); continue; }
			bool fx = Effects;
			a.V = fx ? a.V + (a.T - a.V) * (float)(1 - Math.Exp(-a.Rate * dt)) : a.T;
			if (Math.Abs(a.T - a.V) < 0.003f) { a.V = a.T; a.On = false; avs.RemoveAt(i); }
			AV av = a;
			Safe(delegate { if (av.Owner != null) av.Owner.Invalidate(); if (av.Step != null) av.Step(); });
		}
		for (int i = tws.Count - 1; i >= 0; i--)
		{
			if (i >= tws.Count) continue;
			Tw t = tws[i];
			if (t.Dead) { tws.RemoveAt(i); continue; }
			double p = (now - t.T0) / t.Dur;
			if (p >= 1) { tws.RemoveAt(i); Tw tt = t; Safe(delegate { tt.Step(1f); if (tt.Done != null) tt.Done(); }); }
			else { Tw tt = t; float e = t.Ease((float)p); Safe(delegate { tt.Step(e); }); }
		}
		if (frames.Count > 0) { FrameTimer[] fs = frames.ToArray(); foreach (FrameTimer f in fs) { FrameTimer ff = f; if (!ff.Enabled) continue; if (ff.Throttle && GameBusy && now - ff.LastFire < 0.25) continue; ff.LastFire = now; Safe(ff.Fire); } }
		if (PerfLog) { double cost = sw.Elapsed.TotalSeconds - tickStart; if (cost > 0.03) PerfWrite("한 프레임 처리 " + (int)(cost * 1000) + "ms (직전 동작: " + PerfMark + ")"); }
		if (!Busy)
		{
			demand.Reset(); backup.Stop();
			if (hiOn) { hiOn = false; HiRes(false); }
		}
	}
}

// 창 크기에 따라 자식 컨트롤을 늘이거나 붙이는 간단한 반응형 배치
internal static class Fluid
{
	public static void Attach(Control host)
	{
		if (host.Controls.Count == 0) return;
		int w0 = host.ClientSize.Width;
		if (w0 <= 0) return;
		List<Control> cs = new List<Control>(); List<int[]> info = new List<int[]>();   // mode, left, width, rightMargin
		int near = Dpi.Z(44);
		int m = int.MaxValue;
		foreach (Control c in host.Controls) if (c.Left >= 0 && c.Left < m) m = c.Left;
		if (m == int.MaxValue || m * 2 >= w0) m = 0;
		foreach (Control c in host.Controls)
		{
			int l = c.Left, w = c.Width, rm = w0 - c.Right, mode;
			if (c.Tag as string == "frac") mode = 3;
			else if (c.Tag as string == "right") mode = 1;
			else if (rm <= near && w > w0 * 0.25) mode = 2;
			else if (rm <= near && l > w0 * 0.4) mode = 1;
			else mode = 0;
			cs.Add(c); info.Add(new int[] { mode, l, w, rm, m });
			if (c is Panel && !(c is TInput)) Attach(c);
		}
		host.Resize += delegate
		{
			int W = host.ClientSize.Width;
			for (int i = 0; i < cs.Count; i++)
			{
				int[] n = info[i]; Control c = cs[i];
				switch (n[0])
				{
					case 1: c.Left = W - n[3] - n[2]; break;
					case 2: c.SetBounds(n[1], c.Top, Math.Max(Dpi.Z(40), W - n[3] - n[1]), c.Height); break;
					case 3: { double k = (double)(W - 2 * n[4]) / (w0 - 2 * n[4]); c.SetBounds((int)Math.Round(n[4] + (n[1] - n[4]) * k), c.Top, (int)Math.Round(n[2] * k), c.Height); break; }
				}
			}
		};
	}
}

// 창 가장자리 크기 조절 손잡이 (눈에 안 보임)
// 가장자리에서는 마우스 위치 판정을 아래(창)로 넘겨서, 자식 컨트롤이 있어도 가장자리에서 크기를 조절할 수 있게 해요
internal sealed class EdgeFilter : NativeWindow
{
	private readonly SettingsForm f;
	public EdgeFilter(SettingsForm form) { f = form; }
	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 0x84 && !f.IsDisposed && f.InEdge((int)((long)m.LParam & 0xFFFFFFFF))) { m.Result = (IntPtr)(-1); return; }   // HTTRANSPARENT
		base.WndProc(ref m);
	}
}

internal sealed class Grip : Control
{
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ReleaseCapture();
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
	private readonly int ht;
	public Grip(int hit, Cursor cur) { ht = hit; Cursor = cur; SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
	protected override void OnPaintBackground(PaintEventArgs e) { }
	protected override void OnPaint(PaintEventArgs e) { }
	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && FindForm() != null && FindForm().WindowState == FormWindowState.Normal)
		{ try { ReleaseCapture(); SendMessage(FindForm().Handle, 0xA1, ht, 0); } catch { } }
		base.OnMouseDown(e);
	}
}

// 마우스·키보드 입력이 있었는지 알려 줘요 (입력이 멈춘 뒤에만 쪽 사진을 미리 찍으려고)
internal sealed class PageShotInput : IMessageFilter
{
	private static PageShotInput inst; private static Action<int> cb;
	public static void Hook(Action<int> a) { cb = a; if (inst == null) { inst = new PageShotInput(); Application.AddMessageFilter(inst); } }
	public bool PreFilterMessage(ref Message m)
	{
		int w = m.Msg;
		if (cb != null && (w == 0x100 || w == 0x202 || w == 0x203 || w == 0x205 || w == 0x20A || w == 0xA2 || w == 0xA3)) cb(w);   // 키 누름 · 마우스 뗌 · 휠 (움직이기만 한 건 빼요)
		return false;
	}
}

// 화면 사진 찍기: Graphics.CopyFromScreen 은 CAPTUREBLT 를 붙여 느려요(15~30ms). 그냥 BitBlt 로 복사하면 훨씬 빨라요
internal static class Grab
{
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int rop);
	public static Bitmap Screen(Rectangle r)
	{
		Bitmap b = new Bitmap(r.Width, r.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		IntPtr sdc = GetDC(IntPtr.Zero);
		try
		{
			using (Graphics g = Graphics.FromImage(b))
			{
				IntPtr ddc = g.GetHdc();
				try { BitBlt(ddc, 0, 0, r.Width, r.Height, sdc, r.X, r.Y, 0x00CC0020); } finally { g.ReleaseHdc(ddc); }
			}
		}
		finally { ReleaseDC(IntPtr.Zero, sdc); }
		return b;
	}
}

// 쪽·테마 전환용: 앱 창 '안'에서 옛 화면 사진 위로 새 화면을 부드럽게 겹쳐 넘겨요 (덮개 창을 만들지 않아서 번쩍이지 않아요)
internal static class FastPrint
{
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern int SaveDC(IntPtr hdc);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool RestoreDC(IntPtr hdc, int n);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool OffsetViewportOrgEx(IntPtr hdc, int x, int y, IntPtr old);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern int IntersectClipRect(IntPtr hdc, int l, int t, int r, int b);
	// DrawToBitmap 은 중간 그림을 한 번 더 복사해서 느려요 → 비트맵 위에 곧바로 그려요 (알파는 쓰지 않아요).
	// 자식 컨트롤은 겹침 순서(아래 → 위)대로 하나씩 직접 그려요. WM_PRINT 의 PRF_CHILDREN 은 겹친 형제의 순서를 지키지 못해서 위에 있어야 할 것이 잘려 보였어요.
	public static void Print(Control c, Bitmap bm)
	{
		using (Graphics g = Graphics.FromImage(bm)) { g.Clear(c.BackColor); IntPtr hdc = g.GetHdc(); try { Tree(c, hdc); } finally { g.ReleaseHdc(hdc); } }
	}
	private static void Tree(Control c, IntPtr hdc)
	{
		if (!c.IsHandleCreated) c.CreateControl();
		SendMessage(c.Handle, 0x317, hdc, (IntPtr)(0x2 | 0x4 | 0x8));        // 자기 자신만 (PRF_NONCLIENT | CLIENT | ERASEBKGND)
		for (int i = c.Controls.Count - 1; i >= 0; i--)
		{
			Control k = c.Controls[i]; if (!k.Visible || k.Width <= 0 || k.Height <= 0) continue;
			int st = SaveDC(hdc);
			try { OffsetViewportOrgEx(hdc, k.Left, k.Top, IntPtr.Zero); IntersectClipRect(hdc, 0, 0, k.Width, k.Height); Tree(k, hdc); }
			finally { RestoreDC(hdc, st); }
		}
	}
}
internal sealed class TransHost : Control
{
	public Bitmap A, B; public float T; public int Slide;
	private readonly System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes();
	private readonly System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix();
	public TransHost() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true); TabStop = false; Visible = false; BackColor = Theme.Page; }
	protected override void OnPaintBackground(PaintEventArgs e) { }
	public void Cover(Rectangle bounds, Bitmap a)
	{
		Release(); A = a; B = null; T = 0; SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
		Visible = true; BringToFront(); Refresh();   // 옛 화면과 똑같은 그림이 바로 덮여요
	}
	private int[] pa, pb, po, ba, bb; private int pw, ph; private Bitmap frame;
	private static int[] Pix(Bitmap bm, int[] r)
	{
		if (r == null || r.Length != bm.Width * bm.Height) r = new int[bm.Width * bm.Height]; System.Drawing.Imaging.BitmapData d = bm.LockBits(new Rectangle(0, 0, bm.Width, bm.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		try { if (d.Stride == bm.Width * 4) System.Runtime.InteropServices.Marshal.Copy(d.Scan0, r, 0, r.Length); else for (int y = 0; y < bm.Height; y++) System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), r, y * bm.Width, bm.Width); } finally { bm.UnlockBits(d); }
		return r;
	}
	public void Begin(Bitmap b, int slide)
	{
		if (B != null && B != b) B.Dispose(); B = b; Slide = slide; T = 0;
		try
		{
			if (A != null && b != null && A.Width == b.Width && A.Height == b.Height)
			{
				pw = b.Width; ph = b.Height; ba = Pix(A, ba); bb = Pix(b, bb); pa = ba; pb = bb; if (po == null || po.Length != pa.Length) po = new int[pa.Length];
				if (frame != null && (frame.Width != pw || frame.Height != ph)) { frame.Dispose(); frame = null; }
				if (frame == null) frame = new Bitmap(pw, ph, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			}
			else pa = pb = null;
		}
		catch { pa = pb = null; }
	}
	// 알파 섞기를 GDI+ 색행렬(느림) 대신 여러 코어로 직접 계산해요
	private void Mix()
	{
		int t = (int)Math.Round(Math.Max(0f, Math.Min(1f, T)) * 256), it = 256 - t, dx = (int)Math.Round((1f - T) * Slide), w = pw; int[] a = pa, b = pb, o = po;
		System.Threading.Tasks.Parallel.For(0, 8, delegate(int part)
		{
			int y0 = ph * part / 8, y1 = ph * (part + 1) / 8;
			for (int y = y0; y < y1; y++)
			{
				int row = y * w;
				for (int x = 0; x < w; x++)
				{
					int ai = a[row + x], sx = x - dx;
					if (sx < 0 || sx >= w) { o[row + x] = ai; continue; }
					int bi = b[row + sx];
					int rb = (int)((((uint)(ai & 0xFF00FF) * (uint)it) + ((uint)(bi & 0xFF00FF) * (uint)t)) >> 8) & 0xFF00FF;
					int gg = (int)((((uint)(ai & 0x00FF00) * (uint)it) + ((uint)(bi & 0x00FF00) * (uint)t)) >> 8) & 0x00FF00;
					o[row + x] = rb | gg | unchecked((int)0xFF000000);
				}
			}
		});
		System.Drawing.Imaging.BitmapData d = frame.LockBits(new Rectangle(0, 0, pw, ph), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		try { if (d.Stride == pw * 4) System.Runtime.InteropServices.Marshal.Copy(o, 0, d.Scan0, o.Length); else for (int y = 0; y < ph; y++) System.Runtime.InteropServices.Marshal.Copy(o, y * pw, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), pw); } finally { frame.UnlockBits(d); }
	}
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, uint flags);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern int SetDIBitsToDevice(IntPtr hdc, int xd, int yd, uint w, uint h, int xs, int ys, uint start, uint lines, IntPtr bits, byte[] bmi, uint usage);
	public static bool FastRelease = true;   // 되돌리려면 false (덮개를 걷은 뒤 쪽 전체를 다시 그려서 마지막에 약 30ms 멈춰요)
	// 덮개를 걷으면서 마지막 그림을 쪽 위에 바로 얹고 '다시 그리기' 요청을 지워요 → 전환이 끝나는 순간의 멈춤이 없어져요
	public void ReleaseTo(Control page)
	{
		Bitmap b = B;
		Visible = false;
		try
		{
			if (FastRelease && b != null && page != null && page.IsHandleCreated && page.Visible && b.Width == page.Width && b.Height == page.Height)
			{
				IntPtr dc = GetDC(page.Handle);
				try
				{
					if (pb != null && pw == page.Width && ph == page.Height)
					{
						byte[] bmi = new byte[40]; BitConverter.GetBytes(40).CopyTo(bmi, 0); BitConverter.GetBytes(pw).CopyTo(bmi, 4); BitConverter.GetBytes(-ph).CopyTo(bmi, 8); BitConverter.GetBytes((short)1).CopyTo(bmi, 12); BitConverter.GetBytes((short)32).CopyTo(bmi, 14);
						System.Runtime.InteropServices.GCHandle gh = System.Runtime.InteropServices.GCHandle.Alloc(pb, System.Runtime.InteropServices.GCHandleType.Pinned);
						try { SetDIBitsToDevice(dc, 0, 0, (uint)pw, (uint)ph, 0, 0, 0, (uint)ph, gh.AddrOfPinnedObject(), bmi, 0); } finally { gh.Free(); }   // GDI+ 를 거치지 않고 곧바로 복사(빠름)
					}
					else using (Graphics g = Graphics.FromHdc(dc)) { g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy; g.DrawImage(b, new Rectangle(0, 0, b.Width, b.Height), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel); }
				}
				finally { ReleaseDC(page.Handle, dc); }
				RedrawWindow(page.Handle, IntPtr.Zero, IntPtr.Zero, 0x0008 | 0x0010 | 0x0020 | 0x0080);   // VALIDATE | NOINTERNALPAINT | NOERASE | ALLCHILDREN
			}
		}
		catch { }
		Release();
	}
	public void Release()
	{
		Visible = false;
		if (A != null) { A.Dispose(); A = null; } if (B != null) { B.Dispose(); B = null; }
		pa = pb = null; if (frame != null) { frame.Dispose(); frame = null; }
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		long pt0 = System.Diagnostics.Stopwatch.GetTimestamp(); Graphics g = e.Graphics; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
		Bitmap a = A, b = B;
		if (pa != null && frame != null && T > 0f) { Mix(); g.DrawImage(frame, new Rectangle(0, 0, pw, ph), 0, 0, pw, ph, GraphicsUnit.Pixel); PaintProf.Add("TransHost", pt0); return; }
		if (a != null) g.DrawImage(a, new Rectangle(0, 0, a.Width, a.Height), 0, 0, a.Width, a.Height, GraphicsUnit.Pixel); else g.Clear(BackColor);
		if (b != null && T > 0f)
		{
			cm.Matrix33 = Math.Min(1f, T); ia.SetColorMatrix(cm);
			int dx = (int)Math.Round((1f - T) * Slide);
			g.DrawImage(b, new Rectangle(dx, 0, b.Width, b.Height), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
		}
		PaintProf.Add("TransHost", pt0);
	}
	protected override void Dispose(bool d) { if (d) { if (A != null) A.Dispose(); if (B != null) B.Dispose(); ia.Dispose(); } base.Dispose(d); }
}
// 쪽 전환용 덮개 창: 하얀 반투명 창을 쪽 위에 얹었다가 걷어냄 (창 합성은 그래픽 카드가 처리해서 끊기지 않음)
internal sealed class FadeVeil : Form
{
	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams
	{
		get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x20 | 0x80; return cp; }   // 포커스 안 받음 · 클릭 통과 · 작업표시줄 제외
	}
	public Bitmap Shot;
	public FadeVeil(Form owner)
	{
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
		BackColor = Theme.Page; Opacity = 0; Owner = owner;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
	}
	protected override void OnPaint(PaintEventArgs e) { if (Shot != null) e.Graphics.DrawImageUnscaled(Shot, 0, 0); else e.Graphics.Clear(BackColor); }

	protected override void Dispose(bool d) { if (d && Shot != null) { Shot.Dispose(); Shot = null; } base.Dispose(d); }
	public void Place(Rectangle screenRect)
	{
		Bounds = screenRect;
		if (!Visible) { Opacity = 0; Show(Owner); }
	}
}

// 파일을 쓰다가 꺼져도 망가지지 않게: 임시 파일에 먼저 쓰고 교체
internal static class AtomicIO
{
	public static void WriteLines(string path, string[] lines, Encoding enc)
	{
		string tmp = path + ".tmp";
		File.WriteAllLines(tmp, lines, enc);
		if (File.Exists(path)) { try { File.Replace(tmp, path, null); } catch { File.Copy(tmp, path, true); try { File.Delete(tmp); } catch { } } }
		else File.Move(tmp, path);
	}
}

// 트레이에 상주하는 앱이라, 창을 닫거나 한동안 조용할 때 메모리를 돌려줘요
// 렉 원인 추적(perf.on 이 있을 때만): 메시지 하나가 20ms 넘게 걸리면 어떤 메시지·어느 컨트롤인지 기록하고,
// 별도 스레드가 UI 에 메시지를 던져 45ms 넘게 응답이 늦으면 그 사이 오래 걸린 메시지를 perf.log 에 적어요.
internal sealed class UiProf : IMessageFilter
{
	private static UiProf inst; private static System.Threading.Thread th;
	private readonly object lk = new object(); private long lastTs; private string lastDesc = ""; private readonly List<KeyValuePair<long, string>> slow = new List<KeyValuePair<long, string>>();
	private static string Name(int m)
	{
		switch (m) { case 0x0F: return "WM_PAINT"; case 0x113: return "WM_TIMER"; case 0x200: return "마우스이동"; case 0x201: return "마우스누름"; case 0x202: return "마우스뗌"; case 0x14: return "배경지우기"; case 0x100: return "키"; case 0x111: return "WM_COMMAND"; case 0x7: return "포커스"; case 0x2A3: return "마우스나감"; default: return "0x" + m.ToString("X"); }
	}
	public bool PreFilterMessage(ref Message m)
	{
		try
		{
			long now = Stopwatch.GetTimestamp();
			lock (lk)
			{
				if (lastTs != 0) { double ms = (now - lastTs) * 1000.0 / Stopwatch.Frequency; if (ms > 20 && ms < 5000 && lastDesc != "") { slow.Add(new KeyValuePair<long, string>(now, ms.ToString("F0") + "ms " + lastDesc)); if (slow.Count > 40) slow.RemoveAt(0); } }
				lastTs = now;
				Control c = Control.FromHandle(m.HWnd); lastDesc = Name(m.Msg) + (c != null ? " " + c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : "(" + c.Name + ")") : " (창 " + m.HWnd.ToInt64().ToString("X") + ")");
			}
		}
		catch { }
		return false;
	}
	public static void Start(System.Threading.SynchronizationContext ctx)
	{
		if (inst != null || ctx == null) return; inst = new UiProf(); Application.AddMessageFilter(inst);
		th = new System.Threading.Thread(delegate()
		{
			while (true)
			{
				System.Threading.Thread.Sleep(25); long s0 = Stopwatch.GetTimestamp();
				ctx.Post(delegate
				{
					double lat = (Stopwatch.GetTimestamp() - s0) * 1000.0 / Stopwatch.Frequency;
					if (lat > 45) { StringBuilder sb = new StringBuilder(); lock (inst.lk) { foreach (KeyValuePair<long, string> kv in inst.slow) if (kv.Key >= s0) sb.Append("[" + kv.Value + "] "); } Anim.PerfNote("UI 막힘 " + lat.ToString("F0") + "ms · 오래 걸린 메시지: " + (sb.Length > 0 ? sb.ToString() : "(없음: 메시지 밖의 작업)") + " · 직전 동작 " + Anim.PerfMark); }
				}, null);
			}
		}); th.IsBackground = true; th.Priority = System.Threading.ThreadPriority.AboveNormal; th.Start();
	}
}
internal static class Mem
{
	[System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr h, IntPtr min, IntPtr max);
	[System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
	private static Timer t;
	public static bool NoVisibleForms() { foreach (Form fm in Application.OpenForms) if (fm.Visible) return false; return true; }
	public static void Trim()
	{
		try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1)); } catch { }
	}
	public static void Later(int ms)
	{
		try
		{
			if (t == null) { t = new Timer(); t.Tick += delegate { t.Stop(); if (NoVisibleForms()) Trim(); }; }
			t.Stop(); t.Interval = ms; t.Start();
		}
		catch { }
	}
}

// 화면 색 전환: 창을 다시 만들지 않고, 색을 옛 색 → 새 색으로 한 프레임씩 섞어 가며 그대로 바꿔요
internal static class ThemeFx
{
	private static readonly System.Reflection.FieldInfo[] tok = Array.FindAll(typeof(Theme).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), delegate(System.Reflection.FieldInfo f) { return f.FieldType == typeof(Color); });
	private sealed class B { public object O; public System.Reflection.FieldInfo F; public System.Reflection.PropertyInfo P; public int I; }
	private static List<B> binds; private static Form form; private static Tw tw;

	public static Color[] Snap() { Color[] c = new Color[tok.Length]; for (int i = 0; i < tok.Length; i++) c[i] = (Color)tok[i].GetValue(null); return c; }
	private static void Load(Color[] c) { for (int i = 0; i < tok.Length; i++) tok[i].SetValue(null, c[i]); }
	private static Color Lerp(Color a, Color b, float t) { return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }

	private static void Collect(Control c, Dictionary<int, int> map, List<B> o)
	{
		try
		{
			int ix;
			if (map.TryGetValue(c.BackColor.ToArgb(), out ix)) o.Add(new B { O = c, P = typeof(Control).GetProperty("BackColor"), I = ix });
			if (map.TryGetValue(c.ForeColor.ToArgb(), out ix)) o.Add(new B { O = c, P = typeof(Control).GetProperty("ForeColor"), I = ix });
			for (Type t = c.GetType(); t != null && !(t.Namespace ?? "").StartsWith("System."); t = t.BaseType)
				foreach (System.Reflection.FieldInfo f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
					if (f.FieldType == typeof(Color) && map.TryGetValue(((Color)f.GetValue(c)).ToArgb(), out ix)) o.Add(new B { O = c, F = f, I = ix });
		}
		catch { }
		foreach (Control k in c.Controls) Collect(k, map, o);
	}

	// 한 번에 바꾸기: 색만 즉시 갈아 끼움 (창을 다시 만들지 않고, 프레임마다 전체를 다시 그리지도 않아요)
	public static void Apply(Form f, int mode)
	{
		if (tw != null) tw.Cancel();
		Color[] from = Snap(); bool fromDark = Theme.Dark;
		Theme.Apply(mode); Color[] to = Snap(); bool toDark = Theme.Dark;
		Load(from); Theme.Dark = fromDark;
		Dictionary<int, int> map = new Dictionary<int, int>();
		for (int i = 0; i < from.Length; i++) { int q = from[i].ToArgb(); if (!map.ContainsKey(q) && from[i].A == 255) map[q] = i; }
		List<B> l = new List<B>(); Collect(f, map, l);
		Load(to); Theme.Dark = toDark;
		f.SuspendLayout();
		foreach (B b in l) { try { if (b.P != null) b.P.SetValue(b.O, to[b.I], null); else b.F.SetValue(b.O, to[b.I]); } catch { } }
		f.ResumeLayout(false);
		binds = null; form = null; tw = null;
	}

	public static bool Run(Form f, int mode, Action saved)
	{
		if (tw != null) tw.Cancel();
		Color[] from = Snap(); bool fromDark = Theme.Dark;
		Theme.Apply(mode); Color[] to = Snap(); bool toDark = Theme.Dark;
		Load(from); Theme.Dark = fromDark;
		if (binds == null || form != f)
		{
			Dictionary<int, int> map = new Dictionary<int, int>();
			for (int i = 0; i < from.Length; i++) { int k = from[i].ToArgb(); if (!map.ContainsKey(k) && from[i].A == 255) map[k] = i; }
			List<B> l = new List<B>(); Collect(f, map, l); binds = l; form = f;
		}
		Action<Color[]> paint = delegate(Color[] pal)
		{
			Load(pal);
			foreach (B b in binds) { try { if (b.P != null) b.P.SetValue(b.O, pal[b.I], null); else b.F.SetValue(b.O, pal[b.I]); } catch { } }
			f.Invalidate(true);
		};
		Action finish = delegate { paint(to); Theme.Dark = toDark; binds = null; form = null; tw = null; if (saved != null) saved(); };
		tw = Anim.Run(380, Ease.InOutCubic, delegate(float t)
		{
			if (f.IsDisposed) { Load(to); Theme.Dark = toDark; binds = null; form = null; if (tw != null) tw.Cancel(); return; }
			Color[] pal = new Color[from.Length];
			for (int i = 0; i < pal.Length; i++) pal[i] = Lerp(from[i], to[i], t);
			if (t >= 0.5f) Theme.Dark = toDark;
			paint(pal);
		}, finish);
		return true;
	}
}
