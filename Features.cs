using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// ─────────────────────────────────────────────────────────────
//  시간대(슬롯) · 확인 기록 저장소
// ─────────────────────────────────────────────────────────────
internal class Slot
{
	public DateTime At;      // 알림이 울린 시각
	public DateTime Start;   // 해당 시간대 시작 (HH:00)
	public bool Test;
	public DateTime Deadline { get { return Start.AddHours(1); } }  // 해당 시간대 마감 = 다음 정각
	public int Hour { get { return Start.Hour; } }
}

internal class Rec
{
	public DateTime Date;
	public int Hour;
	public DateTime At;
}

internal static class Records
{
	private static List<Rec> list;
	private static string PathFile { get { return Path.Combine(Program.DataDir, "records.txt"); } }
	public static readonly string[] Dow = { "일", "월", "화", "수", "목", "금", "토" };

	public static List<Rec> All()
	{
		if (list != null) return list;
		list = new List<Rec>();
		try
		{
			if (File.Exists(PathFile))
			{
				foreach (string line in File.ReadAllLines(PathFile, Encoding.UTF8))
				{
					string[] p = line.Split(new char[] { '|' });
					if (p.Length < 3) continue;
					DateTime d, at; int h;
					if (!DateTime.TryParseExact(p[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
					if (!int.TryParse(p[1], out h) || h < 0 || h > 23) continue;
					if (!DateTime.TryParseExact(p[2], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) at = d.AddHours(h);
					list.Add(new Rec { Date = d, Hour = h, At = at });
				}
			}
		}
		catch { }
		return list;
	}

	private static string Fmt(Rec r)
	{
		return r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + r.Hour + "|" + r.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
	}

	private static void SaveAll()
	{
		try
		{
			Directory.CreateDirectory(Program.DataDir);
			string tmp = PathFile + ".tmp";
			File.WriteAllLines(tmp, All().OrderBy(r => r.Date).ThenBy(r => r.Hour).Select(r => Fmt(r)).ToArray(), Encoding.UTF8);
			if (File.Exists(PathFile)) File.Delete(PathFile);
			File.Move(tmp, PathFile);
		}
		catch (Exception ex) { Program.LogErr(ex); }
	}

	// ── 실행 취소 (가장 최근 사용자 동작 1건) ──
	private static List<Rec> opAdded = new List<Rec>(), opRemoved = new List<Rec>();
	private static bool recording;
	public static void BeginOp() { opAdded.Clear(); opRemoved.Clear(); recording = true; }
	public static bool CanUndo { get { return opAdded.Count + opRemoved.Count > 0; } }
	public static int Undo()
	{
		bool was = recording; recording = false;
		List<Rec> a = new List<Rec>(opAdded), r = new List<Rec>(opRemoved);
		opAdded.Clear(); opRemoved.Clear();
		foreach (Rec x in a) Remove(x.Date, x.Hour);
		foreach (Rec x in r) Add(x.Date, x.Hour, x.At);
		recording = was;
		return a.Count + r.Count;
	}

	// ── 놓친 알림 (마감까지 확인하지 못한 시간대) ──
	private static List<Rec> missed;
	private static string MissPath { get { return Path.Combine(Program.DataDir, "missed.txt"); } }
	private static List<Rec> Missed()
	{
		if (missed != null) return missed;
		missed = new List<Rec>();
		try
		{
			if (File.Exists(MissPath))
				foreach (string line in File.ReadAllLines(MissPath, Encoding.UTF8))
				{
					string[] p = line.Split(new char[] { '|' });
					DateTime d; int h;
					if (p.Length >= 2 && DateTime.TryParseExact(p[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && int.TryParse(p[1], out h) && h >= 0 && h < 24)
						missed.Add(new Rec { Date = d, Hour = h });
				}
		}
		catch { }
		return missed;
	}
	private static void SaveMissed()
	{
		try
		{
			Directory.CreateDirectory(Program.DataDir);
			AtomicIO.WriteLines(MissPath, Missed().OrderBy(r => r.Date).ThenBy(r => r.Hour).Select(r => r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + r.Hour).ToArray(), Encoding.UTF8);
		}
		catch (Exception ex) { Program.LogErr(ex); }
	}
	public static void AddMissed(DateTime slotStart)
	{
		if (IsConfirmed(slotStart)) return;
		if (Missed().Any(r => r.Date == slotStart.Date && r.Hour == slotStart.Hour)) return;
		Missed().Add(new Rec { Date = slotStart.Date, Hour = slotStart.Hour });
		SaveMissed();
	}
	private static void ClearMissed(DateTime date, int hour)
	{
		if (Missed().RemoveAll(r => r.Date == date.Date && r.Hour == hour) > 0) SaveMissed();
	}
	public static List<int> MissedOn(DateTime d) { return Missed().Where(r => r.Date == d.Date).Select(r => r.Hour).OrderBy(h => h).ToList(); }
	private static HashSet<string> pre8;
	private static string Pre8Path { get { return Path.Combine(Program.DataDir, "pre8.txt"); } }
	private static HashSet<string> Pre8()
	{
		if (pre8 != null) return pre8;
		pre8 = new HashSet<string>();
		try { if (File.Exists(Pre8Path)) foreach (string l in File.ReadAllLines(Pre8Path, Encoding.UTF8)) if (l.Trim().Length == 10) pre8.Add(l.Trim()); } catch { }
		return pre8;
	}
	public static bool HasPre8(DateTime d) { return Pre8().Contains(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }
	public static void SetPre8(DateTime d, bool on)
	{
		string k = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		if (on) Pre8().Add(k); else Pre8().Remove(k);
		try { Directory.CreateDirectory(Program.DataDir); AtomicIO.WriteLines(Pre8Path, Pre8().OrderBy(x => x).ToArray(), Encoding.UTF8); }
		catch (Exception ex) { Program.LogErr(ex); }
	}
	public static int MissedCount(DateTime d) { return Missed().Count(r => r.Date == d.Date); }

	public static bool IsConfirmed(DateTime slotStart)
	{
		return All().Any(r => r.Date == slotStart.Date && r.Hour == slotStart.Hour);
	}

	public static bool Add(DateTime date, int hour, DateTime at)
	{
		if (All().Any(r => r.Date == date.Date && r.Hour == hour)) return false;
		Rec r2 = new Rec { Date = date.Date, Hour = hour, At = at };
		All().Add(r2);
		if (recording) opAdded.Add(r2);
		ClearMissed(date, hour);
		try
		{
			Directory.CreateDirectory(Program.DataDir);
			File.AppendAllText(PathFile, Fmt(r2) + "\r\n", Encoding.UTF8);
		}
		catch (Exception ex) { Program.LogErr(ex); }
		return true;
	}

	public static bool Add(Slot s, DateTime at) { return Add(s.Start.Date, s.Start.Hour, at); }

	public static bool Remove(DateTime date, int hour)
	{
		if (recording) foreach (Rec x in All().Where(r => r.Date == date.Date && r.Hour == hour)) opRemoved.Add(x);
		int n = All().RemoveAll(r => r.Date == date.Date && r.Hour == hour);
		if (n > 0) SaveAll();
		return n > 0;
	}

	public static int CountOn(DateTime d) { return All().Count(r => r.Date == d.Date); }
	// 월·연 한도에 쓰는 시간: 평일은 하루 1시간 공제, 공휴일·주말은 전부 인정 (수당 계산과 같은 규칙)

	public static int CountYear(int y) { return All().Count(r => r.Date.Year == y); }
	public static int CountMonth(int y, int m) { return All().Count(r => r.Date.Year == y && r.Date.Month == m); }

	public static int CountAll() { return All().Count; }


	// 확인 1회 = 1시간 (인사랑 매시간 근무기록 1건)
	public static string Hours(int n) { return n + "시간"; }
	public static string DayLabel(DateTime d) { return d.Month + "/" + d.Day + "(" + Dow[(int)d.DayOfWeek] + ")"; }
}

internal class GlowCore
{
	public readonly int W, H;
	private int[] idx; private byte[] al; private byte[] ph;
	private static readonly int[] Pal = BuildPal();

	private static int[] BuildPal()
	{
		int[][] key = { new[] { 40, 196, 180 }, new[] { 86, 150, 255 }, new[] { 160, 118, 255 }, new[] { 238, 128, 196 } };   // 청록 → 파랑 → 보라 → 분홍 (은은한 오로라 톤)
		int[] p = new int[256];
		for (int i = 0; i < 256; i++)
		{
			double pos = i / 256.0 * key.Length;
			int sg = (int)pos; double f = pos - sg; f = f * f * (3 - 2 * f);
			int[] c1 = key[sg % key.Length], c2 = key[(sg + 1) % key.Length];
			int r = (int)(c1[0] + (c2[0] - c1[0]) * f), g = (int)(c1[1] + (c2[1] - c1[1]) * f), b = (int)(c1[2] + (c2[2] - c1[2]) * f);
			p[i] = (r << 16) | (g << 8) | b;
		}
		return p;
	}

	public static int Band(int w, int h)
	{
		int band = Math.Max(84, (int)(Math.Min(w, h) * 0.095));
		if (band * 2 > Math.Min(w, h)) band = Math.Min(w, h) / 2;
		return band;
	}

	// 화면 전체(fw x fh) 가장자리 빛 중에서 r 영역(가장자리 띠 하나)에 해당하는 픽셀만 계산해요
	public GlowCore(int fw, int fh, Rectangle r)
	{
		W = r.Width; H = r.Height;
		int band = Band(fw, fh);
		var li = new List<int>(W * H / 3); var la = new List<byte>(W * H / 3); var lp = new List<byte>(W * H / 3);
		for (int y = r.Top; y < r.Bottom; y++)
		{
			int dy = Math.Min(y, fh - 1 - y);
			for (int x = r.Left; x < r.Right; x++)
			{
				int dx = Math.Min(x, fw - 1 - x);
				double d = Math.Min(dx, dy);
				if (dx < band && dy < band) d = Math.Max(0.0, band - Math.Sqrt((double)(band - dx) * (band - dx) + (double)(band - dy) * (band - dy)));   // 모서리는 둥글게 번져요
				if (d >= band) continue;
				double k = 1.0 - d / band;
				double a = Math.Pow(k, 1.9) * 0.74;
				if (a < 0.004) continue;
				double t = (double)x / fw * 0.9 + (double)y / fh * 0.7;
				li.Add((y - r.Top) * W + (x - r.Left)); la.Add((byte)(255 * a)); lp.Add((byte)((int)(t * 256) & 255));
			}
		}
		idx = li.ToArray(); al = la.ToArray(); ph = lp.ToArray();
	}

	public unsafe void Fill(int* dst, int shift)
	{
		int n = idx.Length;
		fixed (int* pal = Pal) fixed (int* ix = idx) fixed (byte* aa = al) fixed (byte* pp = ph)
		{
			for (int i = 0; i < n; i++)
			{
				int c = pal[(pp[i] + shift) & 255];
				int a = aa[i], m = a + 1;
				int r = (((c >> 16) & 255) * m) >> 8, g = (((c >> 8) & 255) * m) >> 8, b = ((c & 255) * m) >> 8;
				dst[ix[i]] = (a << 24) | (r << 16) | (g << 8) | b;
			}
		}
	}
}

internal class GlowWin : Form
{
	[StructLayout(LayoutKind.Sequential)] private struct PT { public int x, y; }
	[StructLayout(LayoutKind.Sequential)] private struct SZ { public int cx, cy; }
	[StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BF { public byte Op, Flags, Alpha, Fmt; }
	[StructLayout(LayoutKind.Sequential)] private struct BMI { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biX, biY, biClrUsed, biClrImportant; }
	[DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PT pptDst, ref SZ psize, IntPtr hdcSrc, ref PT pprSrc, int crKey, ref BF pblend, int dwFlags);
	[DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)] private static extern bool UpdateLayeredWindowA(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, IntPtr psize, IntPtr hdcSrc, IntPtr pprSrc, int crKey, ref BF pblend, int dwFlags);
	[DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
	[DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
	[DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
	[DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BMI pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
	[DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
	[DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr o);
	[DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

	private IntPtr memDC = IntPtr.Zero, hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero, bits = IntPtr.Zero;
	private int W, H;
	private GlowCore core; private int lastShift = -1; private int lastAlpha = -1;

	public GlowWin(Rectangle b, GlowCore c)
	{
		core = c;
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.Manual;
		TopMost = true;
		Bounds = b;
		W = b.Width; H = b.Height;
	}

	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams cp = base.CreateParams;
			cp.ExStyle |= 0x00080000 | 0x00000020 | 0x00000080 | 0x08000000; // LAYERED | TRANSPARENT(클릭 통과) | TOOLWINDOW | NOACTIVATE
			return cp;
		}
	}
	protected override void OnPaint(PaintEventArgs e) { }
	protected override void OnPaintBackground(PaintEventArgs e) { }

	public void Init()
	{
		BMI bi = new BMI { biSize = 40, biWidth = W, biHeight = -H, biPlanes = 1, biBitCount = 32, biCompression = 0 };
		IntPtr sdc = GetDC(IntPtr.Zero);
		memDC = CreateCompatibleDC(sdc);
		hBmp = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0);
		ReleaseDC(IntPtr.Zero, sdc);
		if (hBmp == IntPtr.Zero || bits == IntPtr.Zero) throw new InvalidOperationException("글로우 비트맵을 만들지 못했어요.");
		oldBmp = SelectObject(memDC, hBmp);
	}

	public void ResetFrame() { lastShift = -1; lastAlpha = -1; }
	// 색이 3칸 이상 흘렀을 때만 그림을 다시 채워요. 밝기만 바뀔 땐 '투명도'만 바꿔서 거의 공짜예요
	public unsafe void Frame(int shift, byte alpha)
	{
		if (memDC == IntPtr.Zero || !IsHandleCreated) return;
		int d = lastShift < 0 ? 999 : ((shift - lastShift + 256) & 255); if (d < 999) d = Math.Min(d, 256 - d);
		BF bf = new BF { Op = 0, Flags = 0, Alpha = alpha, Fmt = 1 };
		if (d >= 3)
		{
			core.Fill((int*)bits.ToPointer(), shift); lastShift = shift;
			PT dst = new PT { x = Left, y = Top };
			SZ sz = new SZ { cx = W, cy = H };
			PT src = new PT();
			IntPtr sdc = GetDC(IntPtr.Zero);
			UpdateLayeredWindow(Handle, sdc, ref dst, ref sz, memDC, ref src, 0, ref bf, 2);
			ReleaseDC(IntPtr.Zero, sdc);
		}
		else if (alpha != lastAlpha) UpdateLayeredWindowA(Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref bf, 2);
		lastAlpha = alpha;
	}

	private void FreeGdi()
	{
		try
		{
			if (memDC != IntPtr.Zero)
			{
				if (oldBmp != IntPtr.Zero) SelectObject(memDC, oldBmp);
				DeleteDC(memDC);
			}
			if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
		}
		catch { }
		memDC = IntPtr.Zero; hBmp = IntPtr.Zero; bits = IntPtr.Zero;
	}

	protected override void Dispose(bool disposing)
	{
		FreeGdi();
		base.Dispose(disposing);
	}
}

internal static class Glow
{
	private static List<GlowWin> wins;     // 지금 보이는 창들
	private static List<GlowWin> pool; private static string poolSig = "";   // 숨겨 둔 채 다시 쓰는 창들 (알림이 뜰 때마다 창을 새로 만들면 느려서요)
	private static FrameTimer timer;
	private static Stopwatch sw = new Stopwatch();
	private static Stopwatch fadeOut = new Stopwatch();
	private static int state = -1;
	private static double phase = 0;     // 색 흐름 위치 (0~256, 속도가 바뀌어도 끊기지 않게 누적)
	private static double lastT = 0, lastDraw = -1;
	private static readonly Dictionary<string, GlowCore> cache = new Dictionary<string, GlowCore>();

	// 화면 가장자리 띠 4개 (위·아래·왼·오른쪽). 화면 전체 크기의 투명 창 하나를 매 프레임 갱신하던 것보다 훨씬 가벼워요
	private static List<Rectangle> Bands(Rectangle sb)
	{
		int band = GlowCore.Band(sb.Width, sb.Height); List<Rectangle> l = new List<Rectangle>();
		l.Add(new Rectangle(0, 0, sb.Width, band)); l.Add(new Rectangle(0, sb.Height - band, sb.Width, band));
		if (sb.Height - 2 * band > 0) { l.Add(new Rectangle(0, band, band, sb.Height - 2 * band)); l.Add(new Rectangle(sb.Width - band, band, band, sb.Height - 2 * band)); }
		return l;
	}
	private static GlowCore CoreFor(Rectangle sb, Rectangle r)
	{
		string key = sb.Width + "x" + sb.Height + ":" + r.X + "," + r.Y + "," + r.Width + "," + r.Height; GlowCore c;
		lock (cache) { if (cache.TryGetValue(key, out c)) return c; }
		c = new GlowCore(sb.Width, sb.Height, r);
		lock (cache) { cache[key] = c; }
		return c;
	}
	private static bool hooked;
	// 모니터를 꽂거나 빼거나 해상도를 바꾸면, 숨겨 둔 창을 새 크기에 맞게 다시 만들어 둬요
	private static void HookDisplay()
	{
		if (hooked) return; hooked = true;
		Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate
		{
			try
			{
				Timer t = new Timer(); t.Interval = 2500;
				t.Tick += delegate { t.Stop(); t.Dispose(); if (wins == null) { DropPool(); Prepare(); } };
				// 이벤트는 다른 스레드에서 올 수 있어요 → 화면 스레드로 넘겨서 시작
				if (Application.OpenForms.Count > 0) Application.OpenForms[0].BeginInvoke((MethodInvoker)delegate { t.Start(); }); else t.Start();
			}
			catch { }
		};
	}
	private static string Sig() { string s = ""; foreach (Screen sc in Screen.AllScreens) s += sc.Bounds.ToString() + ";"; return s; }
	// 알림이 오기 전, 한가할 때 미리 계산해 둬요 → 알림이 뜰 때 멈칫하지 않아요
	public static void Prewarm()
	{
		System.Threading.ThreadPool.QueueUserWorkItem(delegate
		{
			try { System.Threading.Thread.Sleep(2500); foreach (Screen s in Screen.AllScreens) foreach (Rectangle r in Bands(s.Bounds)) CoreFor(s.Bounds, r); } catch { }
		});
	}
	// 창을 미리 만들어 숨겨 둬요 (창 만들기가 느린 PC에서도 알림 순간엔 '보이기'만 하면 돼요)
	public static void Prepare()
	{
		try
		{
			HookDisplay();
			if (wins != null) return;
			string sig = Sig(); if (pool != null && sig == poolSig) return;
			DropPool();
			List<GlowWin> l = new List<GlowWin>();
			foreach (Screen s in Screen.AllScreens)
			{
				Rectangle sb = s.Bounds;
				foreach (Rectangle r in Bands(sb))
				{
					GlowWin w = new GlowWin(new Rectangle(sb.X + r.X, sb.Y + r.Y, r.Width, r.Height), CoreFor(sb, r));
					w.Init(); w.Show(); w.Frame(0, 0); w.Hide(); l.Add(w);
				}
			}
			pool = l; poolSig = sig;
		}
		catch (Exception ex) { Program.LogErr(ex); DropPool(); }
	}
	private static void DropPool() { if (pool != null) { foreach (GlowWin w in pool) { try { w.Close(); w.Dispose(); } catch { } } } pool = null; poolSig = ""; }

	public static void Show(int st)
	{
		try
		{
			Hide();
			Prepare();
			if (pool == null) return;
			wins = pool;
			state = st;
			phase = 0; lastT = 0; lastDraw = -1;
			foreach (GlowWin w in wins) { w.ResetFrame(); w.Frame(0, 0); w.Show(); }
			sw.Restart();
			fadeOut.Reset();
			timer = new FrameTimer();
			timer.Tick += Tick;
			timer.Start();
		}
		catch (Exception ex) { Program.LogErr(ex); Hide(); }
	}

	// 마감이 임박하면(2) 색이 더 빠르게 흐른다. 색 자체는 항상 청록 → 초록 → 주황 → 보라로 순환.
	public static void SetState(int st) { if (wins != null) state = st; }

	public static void FadeOut()
	{
		if (wins != null && !fadeOut.IsRunning) fadeOut.Restart();
	}

	private static void Tick(object s, EventArgs e)
	{
		if (wins == null) return;
		try
		{
			double t = sw.Elapsed.TotalSeconds;
			bool fading = t < 0.55 || fadeOut.IsRunning;
			if (lastDraw >= 0 && t - lastDraw < (fading ? 0.016 : 0.045)) return;   // 나타나고 사라질 땐 부드럽게, 평소엔 천천히 숨 쉬는 정도면 충분해요
			lastDraw = t;
			double dt = Math.Min(0.1, t - lastT); lastT = t;
			double period = state == 2 ? 4.0 : 9.0;   // 팔레트 한 바퀴 시간(초)
			phase = (phase + dt / period * 256.0) % 256.0;
			double pulse = 0.5 + 0.5 * Math.Sin(2 * Math.PI * t / (state == 2 ? 1.6 : 3.8) - Math.PI / 2);
			double fadeIn = Math.Min(1.0, t / 0.5); fadeIn = fadeIn * fadeIn * (3 - 2 * fadeIn);
			double fo = 1.0;
			if (fadeOut.IsRunning)
			{
				fo = 1.0 - fadeOut.Elapsed.TotalMilliseconds / 450.0;
				if (fo <= 0) { Hide(); return; }
				fo = fo * fo * (3 - 2 * fo);
			}
			byte a = (byte)(255 * (0.6 + 0.4 * pulse) * fadeIn * fo);
			int shift = (int)phase & 255;
			foreach (GlowWin w in wins) w.Frame(shift, a);
		}
		catch (Exception ex) { Program.LogErr(ex); Hide(); }
	}

	public static void Hide()
	{
		if (timer != null) { timer.Stop(); timer = null; }
		fadeOut.Reset();
		if (wins != null)
		{
			foreach (GlowWin w in wins) { try { w.Hide(); } catch { } }
			wins = null;   // 창은 pool 에 남겨 둬서 다음 알림에 다시 써요
		}
		state = -1;
	}
}
