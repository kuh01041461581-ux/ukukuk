using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// 바탕화면 펫: 창 없이 마룡이만 화면 위에 나와요. 돌아다니고, 쓰다듬으면 좋아하고, 공·커피·별로 같이 놀아요.
// 동작별 그림이 늘어나면 spr/ms_pet_<동작><방향><번호>.png 로 넣어 두면 이 파일의 Pose()가 먼저 찾아 써요 (없으면 기존 그림 사용).
internal sealed class PetWin : Form
{
	private static PetWin cur;
	internal static bool IsOn { get { return cur != null && !cur.IsDisposed; } }
	internal static void Toggle() { if (IsOn) { cur.Close(); } else { try { cur = new PetWin(); cur.Show(); } catch (Exception ex) { cur = null; Program.LogErr(ex); } } Program.petOn = IsOn; }

	[StructLayout(LayoutKind.Sequential)] private struct PT { public int x, y; }
	[StructLayout(LayoutKind.Sequential)] private struct SZ { public int cx, cy; }
	[StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BF { public byte Op, Flags, Alpha, Fmt; }
	[StructLayout(LayoutKind.Sequential)] private struct BMI { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biX, biY, biClrUsed, biClrImportant; }
	[DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PT pptDst, ref SZ psize, IntPtr hdcSrc, ref PT pprSrc, int crKey, ref BF pblend, int dwFlags);
	[DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
	[DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
	[DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
	[DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BMI pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
	[DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
	[DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr o);
	[DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

	private enum St { Idle, Walk, Sit, Sleep, Jump, Fly, Held, Fall, Happy, Chase, Kick, Sip, Catch, Eat, Tramp, Nap, Fetch, Water, Stretch, Yawn, Wave, Dizzy }
	private enum Toy { None, Ball, Mug, Star, Cookie, Tramp, Cushion, Plane }
	private sealed class Pt { public int K; public float X, Y, Vx, Vy, Age, Life, Sz; public string T; }   // K 0 하트 · 1 반짝 · 2 글자(Zzz) · 3 김
	private readonly List<Pt> ps = new List<Pt>();
	private IntPtr memDC, hBmp, oldBmp, bits; private Bitmap canvas; private int Wd, Hd; private float S, sizeMul = 1f;
	private readonly System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer(); private readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew(); private double last, T;
	private static readonly Random R = new Random();
	private St st = St.Idle; private float stT, px, py, vx, vy, flyH, tx, squish, idleFor, rubAcc, lastRubX, lastRubY, sayT, kickCd, mood; private int face = 1; private string say = "";
	private Rectangle wa;
	// 장난감 (한 번에 하나)
	private Toy toy = Toy.None; private float toyLife, tX, toyAge;
	private float bx, by, bvx, bvy, brot; private bool carry;
	private float mugX, sx, sy, starT, plX, plY, plVx, plVy, plDvx, plDvy, pendT; private int plTick, plFace = 1; private St pendS = St.Idle; private int tries; private bool starCaught, plLand;
	// 입력
	private bool down, dragged; private Point downAt; private Point grab; private DateTime lastClick = DateTime.MinValue; private float lastSfx;
	// 한마디 조언: 물·스트레칭·점심 추천 (일하는 시간에만, 방해 금지·전체화면 중엔 안 해요)
	private double nextWater = 50 * 60, nextStretch = 70 * 60; private bool lunchDone; private int lunchDay = -1; private string lunchMenu; private float lunchT;
	private static readonly string[] Menus = { "김치찌개", "된장찌개", "순두부찌개", "제육볶음", "비빔밥", "칼국수", "냉면", "국밥", "돈까스", "쌀국수", "초밥", "부대찌개", "짜장면", "샐러드", "샌드위치", "백반", "설렁탕", "카레", "라멘", "떡볶이" };
	private static readonly string[] WaterL = { "물 한 잔 마셔요!", "목 마르지 않으세요? 물 한 잔!", "물 마실 시간이에요~" };
	private static readonly string[] StretchL = { "어깨 쭉 펴고 스트레칭!", "목이랑 허리 한번 돌려요~", "잠깐 일어나서 쭉 펴 볼까요?", "눈도 쉬어 주세요, 먼 곳 20초!" };
	private static readonly string[] Lines = { "헤헤, 좋아요", "더 해주세요!", "기분 좋아요~", "오늘도 고생했어요", "힘이 나요!", "포근포근해요", "고마워요!" };

	private ContextMenuStrip menu;
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
	private double nextState = 20 * 60; private int stateDay = -1;
	private PetWin()
	{
		Bond.OnLevel = delegate(int lv) { Say("호감도 Lv." + (lv + 1) + " · " + Bond.Names[lv] + "!", 4.5f); Burst(0, 0, false); Sfx.Play("fanfare"); };
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
		wa = AllWA(); SizeMul(Program.petSize); homeX = Screen.PrimaryScreen.WorkingArea.Right - 130f * S; px = homeX; py = Ground; face = -1; prevFace = -1; faceF = -1f;
		Bounds = new Rectangle(wa.Left, wa.Top, 10, 10);
		menu = BuildMenu();
		MouseDown += OnDown; MouseUp += OnUp; MouseMove += OnMove;
		tm.Interval = 33; tm.Tick += delegate { Tick(); };
		Load += delegate { Init(); lookCd = 4f; last = sw.Elapsed.TotalSeconds; tm.Start(); Say("안녕하세요!", 2f); Burst(0, 0, true); Go(St.Wave, 0f); };
		FormClosed += delegate { tm.Stop(); tm.Dispose(); FreeGdi(); if (cur == this) cur = null; Program.petOn = false; };
	}
	private void SizeMul(int idx) { Program.petSize = idx; sizeMul = idx == 0 ? 0.75f : (idx == 2 ? 1.3f : 1f); S = Dpi.Scale * sizeMul; Rectangle pw = Screen.PrimaryScreen.WorkingArea; int mh = 0; foreach (Screen sc0 in Screen.AllScreens) mh = Math.Max(mh, sc0.WorkingArea.Height); Wd = Math.Max(400, pw.Width + (int)(200 * Dpi.Scale)); Hd = Math.Max(300, mh + (int)(300 * Dpi.Scale)); }
	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x00080000 | 0x00000080; return cp; } }   // LAYERED | TOOLWINDOW
	protected override void OnPaint(PaintEventArgs e) { }
	protected override void OnPaintBackground(PaintEventArgs e) { }

	private void Init()
	{
		BMI bi = new BMI { biSize = 40, biWidth = Wd, biHeight = -Hd, biPlanes = 1, biBitCount = 32, biCompression = 0 };
		IntPtr sdc = GetDC(IntPtr.Zero); memDC = CreateCompatibleDC(sdc); hBmp = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0); ReleaseDC(IntPtr.Zero, sdc);
		if (hBmp == IntPtr.Zero || bits == IntPtr.Zero) throw new InvalidOperationException("펫 비트맵을 만들지 못했어요.");
		oldBmp = SelectObject(memDC, hBmp); canvas = new Bitmap(Wd, Hd, Wd * 4, PixelFormat.Format32bppPArgb, bits);
	}
	private void FreeGdi()
	{
		try { if (canvas != null) canvas.Dispose(); canvas = null; if (memDC != IntPtr.Zero) { SelectObject(memDC, oldBmp); DeleteObject(hBmp); DeleteDC(memDC); memDC = IntPtr.Zero; } } catch { }
	}
	private ContextMenuStrip BuildMenu()
	{
		ContextMenuStrip m = new ContextMenuStrip(); m.Font = Fonts.Native(9.5f);
		ToolStripMenuItem t = new ToolStripMenuItem("놀이 도구 주기");
		t.DropDownItems.Add("공 (차고 물어와요)", null, delegate { GiveToy(Toy.Ball); }); t.DropDownItems.Add("트램펄린", null, delegate { GiveToy(Toy.Tramp); });
		t.DropDownItems.Add("쿠키", null, delegate { GiveToy(Toy.Cookie); });
		t.DropDownItems.Add("방석 (낮잠)", null, delegate { GiveToy(Toy.Cushion); }); t.DropDownItems.Add("종이비행기", null, delegate { GiveToy(Toy.Plane); });
		t.DropDownItems.Add("커피 한 잔", null, delegate { GiveToy(Toy.Mug); }); t.DropDownItems.Add("별 잡기", null, delegate { GiveToy(Toy.Star); });
		m.Items.Add(t);
		m.Items.Add("마룡이 돌보기 (키우기)", null, delegate { RaiseForm.Open(); });
		ToolStripMenuItem bondI = new ToolStripMenuItem(Bond.Info()); m.Items.Insert(0, bondI); m.Opening += delegate { bondI.Text = Bond.Info(); bondI.DropDownItems.Clear(); for (int i = 0; i < Bond.Perks.Length; i++) { ToolStripMenuItem pi = new ToolStripMenuItem((Bond.Level >= i ? "✓ " : "· ") + "Lv." + (i + 1) + " " + Bond.Perks[i]); pi.Enabled = false; bondI.DropDownItems.Add(pi); } }; m.Items.Insert(1, new ToolStripMenuItem("지금 시간·현황 알려줘", null, delegate { StatusNow(); })); m.Items.Insert(1, new ToolStripMenuItem("마룡이 운세 · 타로", null, delegate { FortuneForm.Open(); }));
		m.Items.Add("도구 치우기", null, delegate { if (toy == Toy.None) { Say("치울 게 없어요", 1.2f); return; } EndToy(); if (st == St.Eat || st == St.Sip || st == St.Nap || st == St.Chase || st == St.Fetch) Go(St.Idle, 0f); Say("정리했어요", 1.2f); });
		m.Items.Add("날아다니기", null, delegate { StartFly(); });
		m.Items.Add("창문 위로 올라가기", null, delegate { RefreshPlats(); if (!TryHop()) Say("올라갈 창문이 없어요", 1.6f); });
		ToolStripMenuItem sz = new ToolStripMenuItem("크기"); string[] sn = { "작게", "보통", "크게" };
		for (int i = 0; i < 3; i++) { int k = i; sz.DropDownItems.Add(sn[i], null, delegate { ResizeTo(k); }); }
		m.Items.Add(sz);
		ToolStripMenuItem ro = new ToolStripMenuItem("돌아다니기 (화면 전체)") { CheckOnClick = true, Checked = Program.petRoam }; ro.CheckedChanged += delegate { Program.petRoam = ro.Checked; if (!ro.Checked) homeX = wa.Left + wa.Width * 0.14f; Say(ro.Checked ? "여기저기 돌아다녀 볼게요!" : "이 근처에 있을게요", 2f); };
		m.Items.Add(ro);
		ToolStripMenuItem bz = new ToolStripMenuItem("말풍선 크기"); string[] bn = { "아주 작게", "작게", "보통", "크게", "아주 크게" };
		for (int i = 0; i < 5; i++) { int k = i; bz.DropDownItems.Add(bn[i], null, delegate { Program.bubbleSize = k; Say("이 크기는 어때요?", 2f); }); }
		m.Items.Add(bz); m.Items.Add(new ToolStripSeparator()); m.Items.Add("펫 끄기", null, delegate { Close(); });
		return m;
	}
	private void ResizeTo(int idx) { Program.petSize = idx; Close(); Toggle(); }

	// ───── 상태 ─────
	private float wakeT, trC, trT; private int trN;
	private float tY = -1f, tG, tvy, tOffX; private IntPtr tH; private bool tDrag, tSaid;
	private bool GroundToy { get { return toy == Toy.Mug || toy == Toy.Cookie || toy == Toy.Cushion || toy == Toy.Tramp; } }
	private float TXs { get { return toy == Toy.Mug ? mugX : tX; } set { if (toy == Toy.Mug) mugX = value; else tX = value; } }
	private float TrSurf { get { return tG - 36f * S; } }
	private float TrVmax { get { float g = 1800f * S; return (float)Math.Sqrt(2f * g * Math.Max(220f * S, TrSurf - (wa.Top + 200f * S))); } }
	// 떨어지던 마룡이가 트램펄린 위로 내려오면 튕겨 올라요
	private bool TrCatch(float dt)
	{
		if (toy != Toy.Tramp || vy <= 0f || Math.Abs(px - tX) > 58f * S) return false;
		float surf = TrSurf; if (py < surf || py - vy * dt > surf + 8f * S) return false;
		float sp = vy; py = surf; trN = 1; trC = 1f; trT = 0f; vx *= 0.3f; vy = -Math.Min(TrVmax, sp * 0.95f + 260f * S); Go(St.Tramp, 0f); Sfx.Play("tick"); Say("통통!", 0.9f); return true;
	}
	private float homeX, runDist, lookCd, lookDw; private bool userMoved; private int eatB; private bool dizzyNext; private float gait, lastPx, curV, startT, turnT, turnDur, brakeT, brakeDur, brakeV; private int prevFace = 1, turnFrom = 1, brakeK;
	private void Go(St s, float t) { if (st == St.Chase && pendS == St.Idle && runDist > 260f * S && curV > 130f * S && (s == St.Kick || s == St.Sip || s == St.Eat)) { pendS = s; pendT = t; s = St.Idle; t = 0f; } if (s == St.Idle && (st == St.Walk || st == St.Chase) && turnT <= 0f && ((st == St.Walk && runDist > 70f * S && curV > 55f * S) || (st == St.Chase && runDist > 150f * S && curV > 130f * S))) { brakeK = st == St.Walk ? 0 : 1; brakeDur = brakeK == 0 ? 0.24f : 0.3f; brakeT = brakeDur; brakeV = curV; } else brakeT = 0f; if (s == St.Walk || s == St.Chase) { startT = 0f; runDist = 0f; } if (s == St.Eat) eatB = 0; if (s == St.Walk || s == St.Chase || s == St.Fetch) gait = 0f; if ((st == St.Sleep || st == St.Nap) && s != st) wakeT = 0.9f; st = s; stT = t; idleFor = 0f; }
	// 모니터 작업 영역은 1초에 한 번만 다시 읽어요 (매 프레임 여러 번 묻지 않게)
	private static Rectangle[] waC; private static int waTk;
	private static Rectangle[] WAs() { int tk = Environment.TickCount; if (waC == null || unchecked(tk - waTk) > 1000 || unchecked(tk - waTk) < 0) { waTk = tk; try { Screen[] sc = Screen.AllScreens; Rectangle[] r = new Rectangle[sc.Length]; for (int i = 0; i < sc.Length; i++) r[i] = sc[i].WorkingArea; waC = r.Length > 0 ? r : new[] { Screen.PrimaryScreen.WorkingArea }; } catch { if (waC == null) waC = new[] { Screen.PrimaryScreen.WorkingArea }; } } return waC; }
	private static Rectangle AllWA() { Rectangle r = Rectangle.Empty; foreach (Rectangle sw0 in WAs()) r = r.IsEmpty ? sw0 : Rectangle.Union(r, sw0); return r.IsEmpty ? Screen.PrimaryScreen.WorkingArea : r; }
	// 모니터가 여러 개면 지금 서 있는 화면의 바닥을 써요 (아래로 떨어질 때는 바로 아래 화면 바닥)
	private float Ground { get { float x = px, y = py, best = float.NaN, bestAny = wa.Bottom, nd = 1e9f; bool any = false; foreach (Rectangle w in WAs()) { if (x >= w.Left && x < w.Right) { if (!any || w.Bottom > bestAny) bestAny = w.Bottom; any = true; if (w.Bottom >= y - 4f && (float.IsNaN(best) || w.Bottom < best)) best = w.Bottom; } else { float d = x < w.Left ? w.Left - x : x - w.Right; if (!any && d < nd) { nd = d; bestAny = w.Bottom; } } } return float.IsNaN(best) ? bestAny : best; } }
	// ───── 바탕화면 물리: 열린 창의 윗변을 발판으로 써요 ─────
	private delegate bool EnumProc(IntPtr h, IntPtr l);
	[DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc p, IntPtr l);
	[DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
	[DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
	[DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
	[DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
	[DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
	[DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int n);
	[DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int sz);
	[DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int sz);
	[StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }
	private struct Plat { public IntPtr H; public Rectangle R; }
	private List<Plat> plats = new List<Plat>(); private double platT; private IntPtr supH; private int supTop, supLeft; private bool platOk = true;
	private float hvx, hvy, slide, lastHx, lastHy; private readonly float[] hsT = new float[32], hsX = new float[32], hsY = new float[32]; private int hsN; private float hsClock; private readonly System.Diagnostics.Stopwatch hsSw = System.Diagnostics.Stopwatch.StartNew();
	private void HoldSample(float nx, float ny, float dt) { hsClock = (float)hsSw.Elapsed.TotalSeconds; if (hsN == 32) { Array.Copy(hsT, 1, hsT, 0, 31); Array.Copy(hsX, 1, hsX, 0, 31); Array.Copy(hsY, 1, hsY, 0, 31); hsN = 31; } hsT[hsN] = hsClock; hsX[hsN] = nx; hsY[hsN] = ny; hsN++; }
	// 놓는 순간의 속도: 마지막 0.12초 동안 실제로 움직인 거리로 계산 (손떨림은 무시, 위로만 올리면 거의 수직으로)
	private void ThrowVel(out float ox, out float oy)
	{
		ox = oy = 0f; if (hsN < 2) return; float tn = hsT[hsN - 1]; hsClock = (float)hsSw.Elapsed.TotalSeconds; if (hsClock - tn > 0.08f) return;
		int i0 = hsN - 1; while (i0 > 0 && tn - hsT[i0 - 1] <= 0.12f) i0--;
		float dtw = tn - hsT[i0]; if (dtw < 0.03f) return;
		float dx = hsX[hsN - 1] - hsX[i0], dy = hsY[hsN - 1] - hsY[i0]; float sp = (float)Math.Sqrt(dx * dx + dy * dy) / dtw;
		float dead = 320f * S; if (sp < dead) return;
		float k = (sp - dead) / sp; float vxx = dx / dtw * k, vyy = dy / dtw * k;
		if (Math.Abs(vxx) < Math.Abs(vyy) * 0.45f) vxx *= 0.25f;   // 거의 위·아래로만 움직였으면 옆으로 튀지 않게
		float m = (float)Math.Sqrt(vxx * vxx + vyy * vyy), soft = 600f * S; if (m > 0f) { float mm = soft * (1f - (float)Math.Exp(-m * 0.55f / soft)) / 0.55f; float f = mm / m; vxx *= f; vyy *= f; }
		ox = vxx * 0.4f; oy = vyy * 0.5f;
	}
	private void RefreshPlats()
	{
		if (!platOk) return;
		try
		{
			List<Plat> L = new List<Plat>(); IntPtr me = Handle; Rectangle w = wa; System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
			EnumWindows(delegate(IntPtr h, IntPtr l)
			{
				if (h == me || !IsWindowVisible(h) || IsIconic(h)) return true;
				if ((GetWindowLong(h, -20) & 0x80) != 0) return true;   // 도구 창 제외
				int cloaked; if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
				if (GetWindowTextLength(h) == 0) return true;
				sb.Length = 0; GetClassName(h, sb, 64); string cn = sb.ToString(); if (cn == "Progman" || cn == "WorkerW" || cn == "Shell_TrayWnd" || cn == "Windows.UI.Core.CoreWindow") return true;
				RECT r; if (DwmGetWindowAttribute(h, 9, out r, 16) != 0 && !GetWindowRect(h, out r)) return true;
				Rectangle rc = Rectangle.FromLTRB(r.L, r.T, r.R, r.B);
				if (rc.Width < 220 || rc.Height < 90 || rc.Top <= w.Top + 50 || rc.Top >= w.Bottom - 60) return true;
				if (rc.Width >= w.Width - 4 && rc.Height >= w.Height - 4) return true;   // 전체 화면 창
				L.Add(new Plat { H = h, R = rc }); return true;
			}, IntPtr.Zero);
			// 다른 창에 가려진 윗변은 제외 (앞쪽 창이 먼저 나열되므로 앞의 창들이 덮는지 확인)
			List<Plat> vis = new List<Plat>();
			foreach (Plat p in L) { bool hid = false; foreach (Plat q in vis) { if (q.R.Left <= p.R.Left + 4 && q.R.Right >= p.R.Right - 4 && q.R.Top <= p.R.Top - 2) { hid = true; break; } } if (!hid) vis.Add(p); }
			// 창이 움직이면 그 위에 서 있던 마룡이를 같이 옮겨요
			if (supH != IntPtr.Zero && (st == St.Idle || st == St.Walk || st == St.Sit || st == St.Sleep || st == St.Happy))
				foreach (Plat p in vis) if (p.H == supH) { int dy = p.R.Top - supTop, dx = p.R.Left - supLeft; if (Math.Abs(dy) < 260 && Math.Abs(dx) < 600) { py += dy; px += dx; tx += dx; } break; }
			if (tH != IntPtr.Zero) { bool fo = false; foreach (Plat p in vis) if (p.H == tH) { fo = true; tG = p.R.Top; TXs = p.R.Left + tOffX; break; } if (!fo) { tH = IntPtr.Zero; tG = Ground; } }
			plats = vis;
		}
		catch { platOk = false; plats.Clear(); }
	}
	private bool PlatOn { get { return platOk && (toy == Toy.None || (tH != IntPtr.Zero && GroundToy)); } }
	private float FloorAt(float x, float feet, out IntPtr h)
	{
		float best = Ground; h = IntPtr.Zero;
		if (!PlatOn) return best;
		foreach (Plat p in plats) if (x >= p.R.Left + 10 && x <= p.R.Right - 10 && p.R.Top >= feet - 8f && p.R.Top < best) { best = p.R.Top; h = p.H; }
		return best;
	}
	private float FloorAt(float x, float feet) { IntPtr h; return FloorAt(x, feet, out h); }
	private bool TryHop()
	{
		if (!PlatOn) return false; List<Plat> c = new List<Plat>();
		foreach (Plat p in plats) { float h = py - p.R.Top; if (h > 40f * S && h < 430f * S && px > p.R.Left - 380f * S && px < p.R.Right + 380f * S) c.Add(p); }
		if (c.Count == 0) return false; Plat t = c[R.Next(c.Count)];
		float lx = Clamp(px, t.R.Left + 60, t.R.Right - 60); float hh = py - t.R.Top; float g = 1800f * S, over = 40f * S;
		vy = -(float)Math.Sqrt(2f * g * (hh + over)); float tt = -vy / g + (float)Math.Sqrt(2f * over / g); vx = (lx - px) / tt; if (Math.Abs(vx) > 1f) face = vx > 0 ? 1 : -1;
		Go(St.Jump, 0f); Sfx.Play("whoosh"); return true;
	}
	private void Say(string t, float sec) { say = t; sayT = sec; }
	private void Burst(float x, float y, bool hearts)
	{
		int n = hearts ? 5 : 8; for (int i = 0; i < n; i++) ps.Add(new Pt { K = hearts ? 0 : 1, X = px + x + (float)(R.NextDouble() - 0.5) * 50f * S, Y = py + y - 90f * S, Vx = (float)(R.NextDouble() - 0.5) * 90f * S, Vy = -(50f + (float)R.NextDouble() * 70f) * S, Life = 1.1f, Sz = (hearts ? 11f : 8f) * S * (0.8f + (float)R.NextDouble() * 0.5f) });
	}
	private void StartFly() { flyH = 0f; tx = Program.petRoam ? wa.Left + 80 + (float)R.NextDouble() * (wa.Width - 160) : Clamp(homeX + (float)(R.NextDouble() * 2 - 1) * 300f * S, wa.Left + 80, wa.Right - 80); Go(St.Fly, 0f); }
	private static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
	private bool Busy { get { return st == St.Held || st == St.Fall || st == St.Jump || st == St.Kick || st == St.Sip || st == St.Catch || st == St.Happy || st == St.Eat || st == St.Tramp || st == St.Nap || st == St.Fetch || st == St.Water || st == St.Stretch || st == St.Yawn || st == St.Wave || st == St.Dizzy; } }
	private void EndToy() { toy = Toy.None; carry = false; tH = IntPtr.Zero; tDrag = false; tG = Ground; tY = -1f; }
	private float Side(float dist)
	{
		// 마룡이에게서 멀리(260px 이상) 떨어진 아무 곳에나 놓아요
		float far = Math.Max(dist * S, 260f * S); float lo = wa.Left + 90, hi = wa.Right - 90; float x = lo + (float)R.NextDouble() * (hi - lo);
		for (int i = 0; i < 12 && Math.Abs(x - px) < far; i++) x = lo + (float)R.NextDouble() * (hi - lo);
		if (Math.Abs(x - px) < far) x = px > (lo + hi) / 2f ? lo : hi;
		return x;
	}
	private bool careCall;
	internal static void Care(int k) { if (!IsOn) return; try { cur.CareAnim(k); } catch (Exception ex) { Program.LogErr(ex); } }
	private void CareAnim(int k)
	{
		careCall = true;
		try
		{
			if (k == 0) GiveToy(Toy.Cookie); else if (k == 1) GiveToy(Toy.Ball); else if (k == 3) GiveToy(Toy.Cushion);
			else if (st != St.Held) { if (k == 2) { Go(St.Happy, 0f); Burst(0, 0, false); Say("뽀득뽀득~", 1.8f); } else if (k == 4) { Burst(0, 0, false); Say("깨끗해졌어요!", 1.6f); } else { Go(St.Happy, 0f); Burst(0, 0, true); Say("다 나았어요!", 1.8f); } }
		}
		finally { careCall = false; }
	}
	private void GiveToy(Toy t)
	{
		if (!careCall) { try { Raise.Toy(t == Toy.Cookie ? 0 : (t == Toy.Ball ? 1 : (t == Toy.Cushion ? 3 : 2))); } catch { } }
		if (st == St.Held) return; EndToy(); carry = false; if (st == St.Sleep || st == St.Nap || st == St.Sit) Go(St.Idle, 0f); toy = t; tG = tY = Ground; tvy = 0f; tH = IntPtr.Zero; tSaid = false; toyLife = 18f; toyAge = 0f; kickCd = 0f;
		switch (t)
		{
			case Toy.Ball: bx = Side(300f); by = py - 330f * S; bvx = face * 60f * S; bvy = 0f; toyLife = 20f; Say("공이다!", 1.2f); break;
			case Toy.Mug: mugX = Side(300f); Say("커피!", 1.2f); break;
			case Toy.Star: starCaught = false; tries = 0; sx = Side(300f); sy = py - 150f * S; starT = 0f; Say("별이다!", 1.2f); break;
			case Toy.Cookie: tX = Side(130f); Say("쿠키다!", 1.2f); break;
			case Toy.Tramp: tX = Side(190f); toyLife = 40f; Say("트램펄린!", 1.2f); break;
			case Toy.Cushion: tX = Side(130f); toyLife = 40f; Say("포근해 보여요", 1.4f); break;
			case Toy.Plane: plX = Side(300f); plY = py - 260f * S; plVx = (plX > px ? -1f : 1f) * 260f * S; plVy = 0f; plLand = false; toyLife = 20f; Say("슝~", 1f); break;
		}
		Go(St.Idle, 0f);
	}

	private float dtS = 0.033f;
	private void Tick()
	{
		double now = sw.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.06, now - last); last = now; T += dt;
		// 전체화면 앱·발표 중에는 숨어요
		if ((int)(T * 2) != (int)((T - dt) * 2)) { try { if (Program.AutoBusy()) { if (Visible) Hide(); return; } else if (!Visible) Show(); } catch { } }
		if (!Visible) return;
		wa = AllWA(); dtS += (dt - dtS) * 0.35f; Step(dtS); Render();
		tm.Interval = (st == St.Sleep && ps.Count == 0 && toy == Toy.None) ? 90 : 33;
	}
	// 비서 기능: 정각 시간 알림과 초과근무 현황
	private int lastChime = -1; private int ux, uy, uw = 10, uh = 10; private bool dragToy;
	private string StatusText(DateTime n, bool full)
	{
		try
		{
			var all = Records.All().ToList(); int mon = all.Count(r => r.Date.Year == n.Year && r.Date.Month == n.Month), today = all.Count(r => r.Date.Date == n.Date), lim = Program.MonthlyLimit;
			string t = "이번 달 초과근무 " + mon + "/" + lim + "시간";
			if (today > 0) t += ", 오늘 " + today + "시간";
			if (mon >= lim) t += " · 한도를 넘었어요!"; else if (mon >= lim * 0.9) t += " · 한도까지 " + (lim - mon) + "시간 남았어요";
			return t;
		}
		catch { return full ? "기록을 불러오지 못했어요" : ""; }
	}
	private string TimeText(DateTime n) { int h = n.Hour; return (h < 12 ? "오전 " : "오후 ") + (h % 12 == 0 ? 12 : h % 12) + "시" + (n.Minute == 0 ? " 정각" : " " + n.Minute + "분"); }
	private static string Fort() { string f = FortuneForm.TodayLine(); return f == null ? "" : "\n" + f; }
	private void StatusNow() { DateTime n = DateTime.Now; string fl = Fort(); Say(TimeText(n) + "이에요. " + StatusText(n, true) + fl, fl == "" ? 9f : 12f); Burst(0, 0, false); Sfx.Play("ok"); }
	private void ChimeTick()
	{
		DateTime n = DateTime.Now; if (n.Minute != 0 || lastChime == n.Hour + n.DayOfYear * 100) return;
		if (n.DayOfWeek == DayOfWeek.Saturday || n.DayOfWeek == DayOfWeek.Sunday || n.Hour < 8 || n.Hour > 19 || Busy || st == St.Sleep || Program.DndActive) return;
		lastChime = n.Hour + n.DayOfYear * 100; string t = TimeText(n) + "이에요.";
		if (n.Hour == 9) t = "좋은 아침이에요! " + StatusText(n, false) + Fort();
		else if (n.Hour == 12) t += " 점심시간이에요, 맛있게 드세요!";
		else if (n.Hour == 18) t = "퇴근 시간이에요! " + StatusText(n, false);
		else if (n.Hour == 14 || n.Hour == 16) { string st2 = StatusText(n, false); if (st2.Contains("한도")) t += " " + st2; }
		Say(t.Trim(), 8f); Go(St.Wave, 0f); Sfx.Play("ok");
	}
	// 시간·요일·야근 같은 상황에 맞춰 가끔 한마디 해요 (호감도가 높으면 더 다정해요)
	private string pendFort; private double fortAt, nextNeed = 120;
	private void StateTick()
	{
		if (pendFort != null && T > fortAt && !Busy && st != St.Sleep) { Say("오늘의 운세: " + pendFort, 8f); pendFort = null; return; }
		if (Busy || st == St.Sleep || Program.DndActive || sayT > 0f) return; DateTime n = DateTime.Now;
		if (Raise.Pending != null) { Say(Raise.Pending, 6f); Burst(0, 0, true); Raise.Pending = null; return; }
		if (T > nextNeed) { nextNeed = T + 900 + R.Next(600); string nd = Raise.Need(); if (nd != null) { Say(nd, 5f); return; } }
		if (stateDay != n.DayOfYear) { stateDay = n.DayOfYear; string v = Bond.Visit(); if (v != null) { Say(v, 4f); Burst(0, 0, false); nextState = T + 600; if (Bond.Level >= 4) { string fl = FortuneForm.TodayLine(); if (fl != null) { pendFort = fl.Replace("\n", " "); fortAt = T + 6; } } return; } }
		if (T < nextState) return; nextState = T + (Bond.Level >= 5 ? 900 : 1500) + R.Next(1200); int lv = Bond.Level; bool wk = n.DayOfWeek != DayOfWeek.Saturday && n.DayOfWeek != DayOfWeek.Sunday; string s = null;
		if (!wk) s = lv >= 3 ? "주말에도 같이 있어 주네요. 오늘은 푹 쉬어요" : "주말이에요, 쉬어도 돼요";
		else if (n.Hour >= 23 || n.Hour < 5) s = "너무 늦었어요. 내일의 나에게 맡기고 쉬어요";
		else if (n.Hour >= Program.quitHour + 2) s = lv >= 3 ? "야근이 길어지네요… 마룡이가 옆에서 응원할게요" : "야근 중이네요, 물 한 잔 마셔요";
		else if (n.Hour >= Program.quitHour) s = lv >= 2 ? "퇴근 시간이 지났어요. 곧 끝낼 수 있죠?" : "퇴근 시간이 지났어요";
		else if (n.DayOfWeek == DayOfWeek.Friday && n.Hour >= 14) s = "금요일이에요! 조금만 더 힘내요";
		else if (n.DayOfWeek == DayOfWeek.Monday && n.Hour < 11) s = "월요일이에요, 천천히 시동 걸어요";
		else if (n.Hour < 10) s = "좋은 아침이에요" + (lv >= 3 ? ", 오늘도 같이 힘내요" : "");
		if (s != null) { Say(s, 5f); if (lv >= 4 && R.Next(3) == 0) Burst(0, 0, true); }
	}
	private void AdviceTick(float dt)
	{
		ChimeTick(); try { Raise.Tick(T); } catch { } StateTick();
		DateTime n = DateTime.Now; bool work = n.DayOfWeek != DayOfWeek.Saturday && n.DayOfWeek != DayOfWeek.Sunday && n.Hour >= 9 && n.Hour < 19;
		if (lunchT > 0f) { lunchT -= dt; if (lunchT <= 0f) lunchMenu = null; }
		if (!work || Busy || st == St.Sleep || Program.DndActive) return;
		if (lunchDay != n.DayOfYear) { lunchDay = n.DayOfYear; lunchDone = false; }
		if (!lunchDone && (n.Hour == 11 && n.Minute >= 20 || n.Hour == 12 && n.Minute < 30))
		{
			lunchDone = true; lunchMenu = Menus[new Random(n.DayOfYear * 31 + n.Year).Next(Menus.Length)]; lunchT = 25f; Say("점심은 " + lunchMenu + " 어때요? 눌러 보세요!", 8f); Burst(0, 0, false); Sfx.Play("ok"); return;
		}
		if (T > nextWater) { nextWater = T + 3000; Say(WaterL[R.Next(WaterL.Length)], 6f); Go(St.Water, 0f); }
		else if (T > nextStretch) { nextStretch = T + 4200; Say(StretchL[R.Next(StretchL.Length)], 6.5f); Go(St.Stretch, 0f); }
	}
	private void OpenLunch() { try { string q = Uri.EscapeDataString(lunchMenu + " 맛집"); System.Diagnostics.Process.Start("https://map.naver.com/p/search/" + q); } catch { } lunchMenu = null; lunchT = 0f; Say("맛있게 드세요!", 2f); }

	private float faceF = 1f, walkV, wphase;
	private float turnCd;
	// 목표 바로 근처에서 방향이 매 프레임 뒤집혀 덜덜 떨지 않게: 충분히 멀어졌을 때만 방향을 바꿔요
	private void FaceTo(float d) { if (Math.Abs(d) > 18f * S) { int nf = d > 0 ? 1 : -1; if (nf != face && turnCd > 0f && Math.Abs(d) < 60f * S) return; face = nf; } }
	private void Step(float dt)
	{
		if (turnCd > 0f) turnCd -= dt;
		faceF += (face - faceF) * Math.Min(1f, dt * 13f); if (Math.Abs(face - faceF) < 0.02f) faceF = face;
		if (face != prevFace) { if ((st == St.Idle || st == St.Walk || st == St.Chase) && wakeT <= 0f) { turnFrom = prevFace; turnDur = 0.26f; turnT = turnDur; turnCd = 0.8f; } prevFace = face; }
		if (turnT > 0f) turnT -= dt;
		if (userMoved && (st == St.Idle || st == St.Dizzy || st == St.Happy)) { homeX = Clamp(px, wa.Left + 80, wa.Right - 80); userMoved = false; }
		if (pendS != St.Idle) { if (st != St.Idle) pendS = St.Idle; else if (brakeT <= 0f) { St q = pendS; pendS = St.Idle; Go(q, pendT); } }
		if (brakeT > 0f) { brakeT -= dt; if (st == St.Idle) px += face * brakeV * Math.Max(0f, brakeT / brakeDur) * dt; else brakeT = 0f; }
		AdviceTick(dt); if (wakeT > 0f) wakeT -= dt; if (trC > 0f) trC = Math.Max(0f, trC - dt * 5f); stT += dt; idleFor += dt; if (sayT > 0f) sayT -= dt; if (squish > 0f) squish = Math.Max(0f, squish - dt * 3.2f); if (kickCd > 0f) kickCd -= dt;
		Point cp = Cursor.Position;
		platT += dt; if (platT > 0.25) { platT = 0; RefreshPlats(); }
		// 장난감 물리
		if (toy != Toy.None && st != St.Eat && st != St.Sip && st != St.Tramp && st != St.Nap) { toyAge += dt; if (Math.Abs(px - ToyX()) < 300f * S || toyAge > 90f) toyLife -= dt; if (toyLife <= 0f) { bool wasBall = toy == Toy.Ball; EndToy(); if (!Busy && wasBall) { Go(St.Happy, 0f); Burst(0, 0, true); Say("재밌었어요!", 1.6f); } } }
		if (toy == Toy.Ball && !carry)
		{
			bvy += 1500f * S * dt; bx += bvx * dt; by += bvy * dt; brot += bvx * dt * 0.6f; float gy = Ground - 16f * S;
			if (by > gy) { by = gy; if (bvy > 120f * S) bvy = -bvy * 0.62f; else bvy = 0f; bvx *= 0.82f; }
			if (by < wa.Top + 20) { by = wa.Top + 20; if (bvy < 0f) bvy = -bvy * 0.5f; } bvx *= (float)Math.Pow(0.7, dt);
			if (bx < wa.Left + 20 || bx > wa.Right - 20) { bvx = -bvx * 0.7f; bx = Clamp(bx, wa.Left + 20, wa.Right - 20); }
			if (Math.Abs(bvy) < 1f) bvx *= (float)Math.Pow(0.25, dt);
		}
		if (GroundToy) { if (tY < 0f) tY = tG = Ground; if (!tDrag) { if (tY < tG - 0.5f) { tvy += 1800f * S * dt; tY += tvy * dt; if (tY >= tG) { tY = tG; tvy = 0f; } } else { tY = tG; tvy = 0f; } } }
		if (toy == Toy.Star) { starT += dt; if (!starCaught) sy = py - 150f * S + (float)Math.Sin(starT * 3f) * 10f * S; }
		if (toy == Toy.Plane && !plLand && !(dragToy && down))
		{
			float grav = 420f * S, lift = Math.Min(Math.Abs(plVx) * 0.62f, grav * 0.96f);
			plVy += (grav - lift + (float)Math.Sin(T * 3.1) * 70f * S) * dt; plVy *= (float)Math.Pow(0.45, dt); plVx *= (float)Math.Pow(0.72, dt);
			plX += plVx * dt; plY += plVy * dt;
			if (Math.Abs(plVx) > 12f * S) plFace = plVx > 0 ? 1 : -1;
			if (plX < wa.Left + 30 || plX > wa.Right - 30) { plVx = -plVx * 0.6f; plX = Clamp(plX, wa.Left + 30, wa.Right - 30); }
			if (plY < wa.Top + 40) { plY = wa.Top + 40; if (plVy < 0f) plVy = 0f; }
			if (plY >= Ground - 20f * S) { plY = Ground - 20f * S; plLand = true; plVx = 0f; plVy = 0f; }
		}
		float spd = 95f * S;
		switch (st)
		{
			case St.Idle:
				if (pendS != St.Idle) break;
				if (lookCd > 0f) { lookCd -= dt; lookDw = 0f; } else if (turnT <= 0f && Math.Abs(cp.X - px) < 380f * S && Math.Abs(cp.X - px) > 90f * S && Math.Abs(cp.Y - (py - 60f * S)) < 260f * S) { int nf = cp.X > px ? 1 : -1; if (nf != face) { lookDw += dt; if (lookDw > 0.8f) { face = nf; lookCd = 5f; lookDw = 0f; } } else lookDw = 0f; } else lookDw = 0f;
				if (ChaseCheck()) break;
				if (stT > 2.8f + (float)R.NextDouble() * 4.2f)
				{
					if (idleFor > 40f) { Go(St.Yawn, 0f); break; }
					double r = R.NextDouble();
					if (r < 0.46) { tx = Program.petRoam ? Clamp(px + (float)(R.NextDouble() * 2 - 1) * 300f * S, wa.Left + 50, wa.Right - 50) : Clamp(homeX + (float)(R.NextDouble() * 2 - 1) * 260f * S, wa.Left + 50, wa.Right - 50); if (supH != IntPtr.Zero && R.Next(5) != 0) foreach (Plat p in plats) if (p.H == supH) { tx = Clamp(tx, p.R.Left + 40, p.R.Right - 40); break; } if (Math.Abs(tx - px) < 70f * S) { face = -face; lookCd = 3f; stT = 0f; } else Go(St.Walk, 0f); }
					else if (r < 0.62) Go(St.Sit, 0f);
					else if (r < 0.68) { Go(St.Wave, 0f); }
					else if (r < 0.73) { Go(St.Stretch, 0f); }
					else if (r < 0.77 && TryHop()) { }
					else if (r < 0.82) StartFly();
					else { vy = -620f * S; vx = 0f; Go(St.Jump, 0f); Sfx.Play("whoosh"); }
				}
				break;
			case St.Walk:
				if (ChaseCheck()) break;
				FaceTo(tx - px); if (turnT > 0f) break; startT += dt; { float dist = Math.Abs(tx - px); float want = spd * Math.Min(1f, 0.25f + dist / (90f * S)); walkV += (want - walkV) * Math.Min(1f, dt * 6f); px += face * walkV * dt; wphase += walkV / spd * dt; } if (Math.Abs(tx - px) < 6f) { walkV = 0f; Go(St.Idle, 0f); }
				break;
			case St.Sit: if (ChaseCheck()) break; if (stT > 3f + (float)R.NextDouble() * 4f) { idleFor += 3f; Go(St.Idle, 0f); } break;
			case St.Sleep:
				ZzzTick(dt);
				if (Math.Abs(cp.X - px) < 70f * S && Math.Abs(cp.Y - (py - 60f * S)) < 80f * S && (Control.MouseButtons != MouseButtons.None)) Go(St.Idle, 0f);
				break;
			case St.Fly:
				if (stT < 5f) flyH += (120f * S - flyH) * Math.Min(1f, dt * 3f);
				FaceTo(tx - px); { float fd = Math.Abs(tx - px), ease = Math.Min(1f, stT / 0.7f) * Math.Min(1f, 0.3f + fd / (110f * S)); px += face * 95f * S * ease * dt; }
				if (Math.Abs(tx - px) < 8f || stT > 7f) { Go(St.Fall, 0f); vy = 0f; vx = 0f; }
				break;
			case St.Jump:
				vy += 1800f * S * dt; py += vy * dt; px += vx * dt; if (px < wa.Left + 30 || px > wa.Right - 30) vx = -vx * 0.5f; if (TrCatch(dt)) break; { float fl = FloorAt(px, py - Math.Max(0f, vy) * dt); if (vy > 0f && py >= fl) { py = fl; squish = 0.6f; slide = vx * 0.3f; Go(St.Idle, 0f); } }
				break;
			case St.Fall:
				vy += 1800f * S * dt; py += vy * dt; px += vx * dt; vx *= (float)Math.Pow(0.15, dt); flyH = Math.Max(0f, flyH - 400f * S * dt);
				if (px < wa.Left + 30 || px > wa.Right - 30) { vx = -vx * 0.6f; px = Clamp(px, wa.Left + 30, wa.Right - 30); Sfx.Play("tick"); }
				if (TrCatch(dt)) break;
				{ float fl = FloorAt(px, py - Math.Max(0f, vy) * dt); if (vy > 0f && py >= fl) { py = fl; if (vy > 380f * S) { vy = -vy * 0.32f; vx *= 0.7f; squish = 1f; Sfx.Play("tick"); } else { vy = 0f; squish = 0.7f; slide = vx * 0.5f; vx = 0f; flyH = 0f; if (dizzyNext) { dizzyNext = false; Go(St.Dizzy, 0f); Say("어질어질~", 2.4f); Sfx.Play("thud_soft"); } else Go(St.Idle, 0f); } } }
				break;
			case St.Held: { float nx = cp.X - grab.X, ny = cp.Y - grab.Y; if (dt > 0f) { hvx += ((nx - lastHx) / dt - hvx) * 0.35f; hvy += ((ny - lastHy) / dt - hvy) * 0.35f; } lastHx = nx; lastHy = ny; HoldSample(nx, ny, dt); px = nx; py = ny; vx = 0f; vy = 0f; break; }
			case St.Happy: if (stT > 0.9f) Go(St.Idle, 0f); break;
			case St.Dizzy: if (stT > 2.8f) Go(St.Idle, 0f); break;
			case St.Wave: if (stT > 2.7f) Go(St.Idle, 0f); break;
			case St.Water: if (stT > 3.8f) { Go(St.Idle, 0f); Burst(0, 0, false); } break;
			case St.Stretch: if (stT > 4.4f) Go(St.Idle, 0f); break;
			case St.Yawn: if (stT > 2.6f) { Go(St.Sleep, 0f); Say("zZ…", 1.6f); } break;
			case St.Chase: if (!ChaseTarget(dt)) Go(St.Idle, 0f); break;
			case St.Kick:
				if (stT > 0.14f && stT - dt <= 0.14f && toy == Toy.Ball) { bvx = face * (380f + (float)R.NextDouble() * 160f) * S; bvy = -(520f + (float)R.NextDouble() * 220f) * S; Burst(face * 40f * S, 40f * S, false); Sfx.Play("swing"); }
				if (stT > 0.5f) Go(St.Idle, 0f);
				break;
			case St.Fetch:
			{
				float want = Clamp(cp.X, wa.Left + 80, wa.Right - 80); float d = want - px; FaceTo(d);
				if (Math.Abs(d) > 70f * S && stT < 7f) px += face * 150f * S * dt;
				else { carry = false; bx = px + face * 60f * S; by = py - 40f * S; bvx = face * 80f * S; bvy = -200f * S; Go(St.Happy, 0f); Burst(0, 0, true); Say("가져왔어요!", 1.6f); Sfx.Play("ok"); kickCd = 1.4f; }
				break;
			}
			case St.Sip:
				if (stT > 1.0f && stT < 2.8f && (int)(T * 12) != (int)((T - dt) * 12)) ps.Add(new Pt { K = 3, X = px + 3f * S + (float)(R.NextDouble() - 0.5) * 12f * S, Y = py - 74f * S, Vx = (float)(R.NextDouble() - 0.5) * 6f * S, Vy = -26f * S, Life = 1.0f, Sz = 2.8f * S });
				if (stT > 3.6f) { EndToy(); Go(St.Happy, 0f); Burst(0, 0, true); Say("힘이 나요!", 2f); Sfx.Play("ok"); }
				break;
			case St.Eat:
				{ while (stT >= 0.2f + eatB * 0.17f && eatB < 12) { eatB++; Crumbs(4 + (eatB % 3)); if (eatB % 2 == 1) Sfx.Play("munch"); if (eatB % 4 == 1) squish = 0.35f; } }
				if (stT > 2.35f) { EndToy(); Go(St.Happy, 0f); Burst(0, 0, true); Say("맛있다~", 2f); Sfx.Play("ok"); }
				break;
			case St.Tramp:
			{
				if (toy != Toy.Tramp) { vx *= 0.4f; dizzyNext = true; Go(St.Fall, 0f); flyH = 0f; Say("어어…", 1f); break; }
				if (Math.Abs(tX - px) > 150f * S || (tDrag && Math.Abs(tY - tG) > 30f * S)) { vx *= 0.4f; dizzyNext = true; Go(St.Fall, 0f); flyH = 0f; Say("어어…", 1f); break; }
				float surf = TrSurf, g = 1800f * S, tG0 = tG; trT += dt;
				vy += g * dt; py += vy * dt; px += vx * dt; vx += (tX - px) * 2.5f * dt; vx *= (float)Math.Pow(0.45, dt); if (Math.Abs(vx) > 8f * S) face = vx > 0 ? 1 : -1;
				if (py < wa.Top + 120f * S) { py = wa.Top + 120f * S; vy = Math.Max(vy, 0f); }
				if (vy > 0f && py >= surf)
				{
					float sp = vy; py = surf; trC = 1f; trN++; trT = 0f; Sfx.Play("tick");
					if (stT > 12f) { EndToy(); py = tG0; Go(St.Happy, 0f); Burst(0, 0, false); Say("또 하고 싶어요!", 2f); vy = 0f; break; }
					float nv = Math.Min(TrVmax, sp * 1.06f + 170f * S); if (trN < 3) nv = Math.Max(nv, 880f * S);
					vy = -nv; if (nv > 1250f * S) { vx = ((float)R.NextDouble() * 2f - 1f) * 150f * S; if (R.Next(3) == 0) Say("와아~", 1.0f); Sfx.Play("whoosh"); }
				}
				break;
			}
			case St.Nap:
				px = tX; ZzzTick(dt);
				if (stT > 14f) { EndToy(); Go(St.Happy, 0f); Say("푹 잤어요!", 2f); }
				break;
			case St.Catch:
				py += vy * dt; vy += 1800f * S * dt; if (py >= Ground) { py = Ground; squish = 0.5f; vy = 0f; if (starCaught && stT > 0.9f) { EndToy(); Go(St.Happy, 0f); Burst(0, 0, true); Say("잡았다!", 1.8f); Sfx.Play("ok"); } else if (!starCaught) { tries++; if (tries >= 5) { EndToy(); Go(St.Idle, 0f); Say("아쉽다…", 1.5f); } else Go(St.Chase, 0f); } }
				if (!starCaught && vy > -80f * S && vy < 80f * S && Math.Abs(sx - px) < 40f * S && Math.Abs(sy - (py - 100f * S)) < 54f * S) { starCaught = true; Burst(0, 0, false); Sfx.Play("coin"); }
				if (starCaught) { sx = px; sy = py - 135f * S; }
				break;
		}
		if (st != St.Held && st != St.Fly && st != St.Fall && st != St.Jump && st != St.Catch && st != St.Tramp)
		{
			IntPtr sh; float fl = FloorAt(px, py - 2f, out sh);
			if (py < fl - 3f) { vy = 0f; vx = 0f; Go(St.Fall, 0f); supH = IntPtr.Zero; }
			else { py = fl; supH = sh; foreach (Plat p in plats) if (p.H == sh) { supTop = p.R.Top; supLeft = p.R.Left; break; } }
			if (Math.Abs(slide) > 4f) { px += slide * dt; slide *= (float)Math.Pow(0.03, dt); } else slide = 0f;
		}
		px = Clamp(px, wa.Left + 30, wa.Right - 30);
		{ float ddx = Math.Abs(px - lastPx); lastPx = px; if (st == St.Walk || st == St.Chase) runDist += ddx; if (dt > 0f) curV += (ddx / dt - curV) * Math.Min(1f, dt * 20f); if (st == St.Walk) { gait += ddx / (106f * S); if (startT < 0.27f) gait = 0f; } else if (st == St.Chase) { gait += ddx / (150f * S); if (startT < 0.36f) gait = 0f; } else if (st == St.Fetch) gait += ddx / (92f * S); }
		for (int i = ps.Count - 1; i >= 0; i--) { Pt p = ps[i]; p.Age += dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; if (p.K == 0) p.Vy -= 10f * dt; if (p.K == 6) p.Vy += 1250f * S * dt; if (p.K == 3) p.Vx += (float)Math.Sin(p.Age * 12f) * 22f * S * dt; if (p.Age >= p.Life) ps.RemoveAt(i); }
	}
	private void ZzzTick(float dt)
	{
		if (stT > 0.7f && (int)(T * 1.1f) != (int)((T - dt) * 1.1f)) ps.Add(new Pt { K = 2, T = "Z", X = px + 24f * S, Y = py - 90f * S, Vx = 14f * S, Vy = -26f * S, Life = 2.2f, Sz = 13f * S });
	}
	private bool ChaseCheck() { if (toy != Toy.None && kickCd <= 0f) { Go(St.Chase, 0f); return true; } return false; }
	private float ToyX() { switch (toy) { case Toy.Ball: return bx; case Toy.Mug: return mugX; case Toy.Star: return sx; case Toy.Plane: return plX; default: return tX; } }
	private void HopTo(Plat t, float lx)
	{
		float hh = py - t.R.Top; float g = 1800f * S, over = 40f * S;
		vy = -(float)Math.Sqrt(2f * g * (hh + over)); float tt = -vy / g + (float)Math.Sqrt(2f * over / g); vx = (lx - px) / tt; if (Math.Abs(vx) > 1f) face = vx > 0 ? 1 : -1;
		Go(St.Jump, 0f); Sfx.Play("whoosh");
	}
	private bool ToyLevel(float dt, float spd)
	{
		if (Math.Abs(py - tG) < 6f) return true;
		if (tG < py)
		{
			Plat t = new Plat(); bool tf = false; foreach (Plat p in plats) if (p.H == tH) { t = p; tf = true; break; }
			if (!tf || py - tG > 1100f * S) { if (!tSaid) { tSaid = true; Say("너무 높아서 못 가요!", 1.8f); } Go(St.Idle, 0f); kickCd = 6f; return false; }
			float nx = Clamp(px, t.R.Left + 60, t.R.Right - 60);
			if (Math.Abs(px - nx) > 300f * S) { FaceTo(nx - px); px += face * spd * dt; return false; }
			HopTo(t, Clamp(TXs, t.R.Left + 60, t.R.Right - 60)); return false;
		}
		if (Math.Abs(px - TXs) < 20f * S) { vx = 0f; vy = 0f; Go(St.Fall, 0f); return false; }
		FaceTo(TXs - px); px += face * spd * dt; return false;
	}
	private bool ChaseTarget(float dt)
	{
		float spd = 175f * S; float d = ToyX() - px; if (toy != Toy.None) FaceTo(d);
		if (turnT > 0f && toy != Toy.None) return true; startT += dt;
		if (GroundToy && !ToyLevel(dt, spd)) return true;
		switch (toy)
		{
			case Toy.Ball:
				if (carry) return false;
				if (Math.Abs(d) > 54f * S) { px += face * spd * dt; return true; }
				if (by > py - 70f * S && kickCd <= 0f)
				{
					bool resting = Math.Abs(bvx) < 40f * S && Math.Abs(bvy) < 30f * S;
					if (resting && R.NextDouble() < 0.5) { carry = true; Go(St.Fetch, 0f); Say("내가 가져올게요!", 1.2f); } else { Go(St.Kick, 0f); kickCd = 0.7f; }
					return true;
				}
				if (Math.Abs(d) < 30f * S) px -= face * 60f * S * dt;
				return true;
			case Toy.Mug: if (Math.Abs(d) > 30f * S) { px += face * spd * 0.8f * dt; return true; } Go(St.Sip, 0f); return true;
			case Toy.Cookie: if (Math.Abs(d) > 30f * S) { px += face * spd * 0.8f * dt; return true; } Go(St.Eat, 0f); return true;
			case Toy.Tramp: if (Math.Abs(d) > 8f * S) { px += face * spd * 0.8f * dt; return true; } px = tX; vx = 0f; vy = -673f * S; trN = 0; trT = 0f; Go(St.Tramp, 0f); Sfx.Play("whoosh"); return true;
			case Toy.Cushion: if (Math.Abs(d) > 8f * S) { px += face * spd * 0.7f * dt; return true; } px = tX; Go(St.Nap, 0f); return true;
			case Toy.Star: if (Math.Abs(d) > 22f * S) { px += face * spd * dt; return true; } Go(St.Catch, 0f); vy = -740f * S; Sfx.Play("whoosh"); return true;
			case Toy.Plane:
				if (Math.Abs(d) > 40f * S) { px += face * spd * dt; return true; }
				if (plLand) { EndToy(); Go(St.Happy, 0f); Burst(0, 0, true); Say("잡았다!", 1.4f); Sfx.Play("ok"); }
				return true;
		}
		return false;
	}

	// ───── 마우스 ─────
	// 마룡이 위치가 아니라 화면 좌표(Cursor.Position)로 계산해요: 창이 계속 따라다니기 때문
	private bool ToyAt(Point p)
	{
		float G = tY > 0f ? tY : Ground;
		switch (toy)
		{
			case Toy.Ball: return !carry && Math.Abs(p.X - bx) < 30f * S && Math.Abs(p.Y - by) < 30f * S;
			case Toy.Mug: return st != St.Sip && Math.Abs(p.X - mugX) < 32f * S && p.Y > G - 64f * S && p.Y < G + 8f * S;
			case Toy.Cookie: return st != St.Eat && Math.Abs(p.X - tX) < 40f * S && p.Y > G - 64f * S && p.Y < G + 8f * S;
			case Toy.Cushion: return st != St.Nap && Math.Abs(p.X - tX) < 70f * S && p.Y > G - 50f * S && p.Y < G + 8f * S;
			case Toy.Tramp: return Math.Abs(p.X - tX) < 80f * S && p.Y > G - (st == St.Tramp ? 50f : 90f) * S && p.Y < G + 8f * S;
			case Toy.Star: return Math.Abs(p.X - sx) < 32f * S && Math.Abs(p.Y - sy) < 32f * S;
			case Toy.Plane: return Math.Abs(p.X - plX) < 54f * S && Math.Abs(p.Y - plY) < 42f * S;
		}
		return false;
	}
	private float pathLen; private bool rubMode;
	private void OnDown(object s, MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Left) return;
		Point c = Cursor.Position; down = true; dragged = false; dragToy = ToyAt(c); rubMode = false; pathLen = 0f; downAt = c; lastRubX = c.X; lastRubY = c.Y; grab = new Point((int)(c.X - px), (int)(c.Y - py)); Capture = true; hsN = 0;
		if (dragToy) { toyLife += 10f; if (toy == Toy.Plane) { plDvx = plDvy = 0f; plTick = Environment.TickCount; plVx = plVy = 0f; plLand = false; } return; }
		if (st == St.Sleep || st == St.Nap) { if (st == St.Nap) EndToy(); Go(St.Idle, 0f); Say("으음…?", 1.2f); }
	}
	private void OnMove(object s, MouseEventArgs e)
	{
		if (!down) return;   // 마우스를 올리기만 해서는 반응하지 않아요 (누른 채로만)
		Point c = Cursor.Position;
		if (dragToy)
		{
			switch (toy)
			{
				case Toy.Ball: bx = c.X; by = c.Y; bvx = 0f; bvy = 0f; HoldSample(bx, by, 0f); break;
				case Toy.Star: sx = c.X; sy = c.Y; break;
				case Toy.Plane: { int tk = Environment.TickCount; float dtm = Math.Max(0.008f, (tk - plTick) / 1000f); plTick = tk; plDvx += ((c.X - plX) / dtm - plDvx) * 0.5f; plDvy += ((c.Y - plY) / dtm - plDvy) * 0.5f; plX = c.X; plY = c.Y; plLand = false; plVx = plDvx; plVy = plDvy; if (Math.Abs(plDvx) > 40f * S) plFace = plDvx > 0 ? 1 : -1; break; }
				case Toy.Mug: mugX = Clamp(c.X, wa.Left + 40, wa.Right - 40); tDrag = true; tY = c.Y + 40f * S; tH = IntPtr.Zero; break;
				default: tX = Clamp(c.X, wa.Left + 70, wa.Right - 70); tDrag = true; tY = c.Y + 40f * S; tH = IntPtr.Zero; break;
			}
			return;
		}
		float step = (float)Math.Sqrt((c.X - lastRubX) * (c.X - lastRubX) + (c.Y - lastRubY) * (c.Y - lastRubY)); lastRubX = c.X; lastRubY = c.Y; pathLen += step;
		float net = (float)Math.Sqrt((c.X - downAt.X) * (c.X - downAt.X) + (c.Y - downAt.Y) * (c.Y - downAt.Y));
		if (st == St.Held) return;
		// 한 방향으로 멀리 끌면 들어 올리고, 제자리에서 왔다 갔다 하면 쓰다듬어요
		if (!rubMode && net > 46f * S && net > pathLen * 0.7f) { dragged = true; if (st == St.Fetch) carry = false; Go(St.Held, 0f); flyH = 0f; Say("어어어~", 1f); ps.Add(new Pt { K = 4, X = px + 24f * S, Y = py - 110f * S, Vy = 18f * S, Life = 1.2f, Sz = 1f }); return; }
		if (net > 130f * S && rubMode) { rubMode = false; dragged = true; Go(St.Held, 0f); flyH = 0f; return; }
		if (!(st == St.Idle || st == St.Walk || st == St.Sit || st == St.Happy || st == St.Chase || st == St.Sleep || st == St.Wave || st == St.Yawn)) return;
		rubAcc += step; if (rubAcc >= 70f * S) { rubAcc = 0f; rubMode = true; Rub(); }
	}
	private void Rub()
	{
		if (st != St.Happy) Go(St.Happy, 0f);
		stT = 0f; squish = 0.22f; mood++; Burst(0, 0, true); if (T - lastSfx > 0.35) { lastSfx = (float)T; Sfx.Play("pluck"); }
		Bond.Rub(); Raise.Rub(); if (Bond.Level >= 3 && mood % 10 == 0 && st != St.Wave) { Say("하이파이브!", 2f); Go(St.Wave, 0f); }
		if (mood % 4 == 0) { string[] bl = Bond.Lines(); Say(R.Next(3) == 0 ? bl[R.Next(bl.Length)] : Lines[R.Next(Lines.Length)], 2.0f); }
		if (mood % Math.Max(6, 14 - Bond.Level) == 0) { Burst(0, 0, false); Sfx.Play("ok2"); Say("행복해서 날아갈 것 같아요!", 2.2f); }
	}
	private void OnUp(object s, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Right) { try { SetForegroundWindow(Handle); menu.Show(Cursor.Position); } catch { } return; }
		if (e.Button != MouseButtons.Left) return; Capture = false; if (!down) return; down = false;
		if (dragToy && GroundToy && tDrag)
		{
			tDrag = false; tvy = 0f; tSaid = false; float fx = TXs, fl = Ground; IntPtr fh = IntPtr.Zero; Rectangle fr = Rectangle.Empty;
			foreach (Plat p in plats) if (fx >= p.R.Left + 10 && fx <= p.R.Right - 10 && p.R.Top >= tY - 8f && p.R.Top < fl) { fl = p.R.Top; fh = p.H; fr = p.R; }
			tG = fl; tH = fh; if (fh != IntPtr.Zero) tOffX = fx - fr.Left; kickCd = 0f;
		}
		if (dragToy) { dragToy = false; if (toy == Toy.Ball) { float tbx, tby; ThrowVel(out tbx, out tby); bvx = tbx * 1.5f; bvy = tby * 1.5f; hsN = 0; toyLife = Math.Max(toyLife, 14f); if (Math.Abs(bvx) + Math.Abs(bvy) > 200f * S) Sfx.Play("whoosh"); } if (toy == Toy.Plane) { bool still = Environment.TickCount - plTick > 90; float cx = 1300f * S; plVx = still ? 0f : Clamp(plDvx * 0.9f, -cx, cx); plVy = still ? 0f : Clamp(plDvy * 0.9f, -900f * S, 900f * S); plLand = false; toyLife = Math.Max(toyLife, 14f); if (!still && (Math.Abs(plVx) > 120f * S)) Sfx.Play("whoosh"); } return; }
		if (rubMode) { rubMode = false; return; }
		if (dragged) { dragged = false; userMoved = true; Go(St.Fall, 0f); float tvx, tvy; ThrowVel(out tvx, out tvy); vx = tvx; vy = tvy; hsN = 0; if (Math.Abs(vx) > 60f) face = vx > 0 ? 1 : -1; flyH = 0f; hvx = hvy = 0f; return; }
		DateTime n = DateTime.Now; bool dbl = (n - lastClick).TotalMilliseconds < 380; lastClick = n;
		if (lunchMenu != null) { OpenLunch(); return; }
		if (dbl) { StartFly(); Say("슝~", 1.2f); return; }
		if (st == St.Idle || st == St.Walk || st == St.Sit || st == St.Happy) { vy = -560f * S; vx = 0f; Go(St.Jump, 0f); ps.Add(new Pt { K = 5, X = px + 18f * S, Y = py - 118f * S, Vy = -14f * S, Life = 0.9f, Sz = 1f }); Sfx.Play("whoosh"); Say("앗!", 0.9f); }
	}

	// ───── 그리기 ─────
	// 동작별 그림 고르기: 모두 spr/ms_pet_<이름><번호>.png (오른쪽을 보는 그림은 face에 따라 뒤집어 그려요)
	private static string P(string n, int i) { return "ms_pet_" + n + i; }
	private static int Loop(double t, double fps, int n) { return ((int)(t * fps)) % n; }
	// 그릴 영역(화면 좌표): 마룡이 · 말풍선 · 장난감 · 효과를 모두 감싸는 가장 작은 사각형
	private Rectangle Union()
	{
		float L = px - 130f * S, R2 = px + 130f * S, T0 = py - flyH - 215f * S, B = py + 16f * S;
		if (sayT > 0f && say.Length > 0) { float bmm = BubbleMul[Math.Max(0, Math.Min(4, Program.bubbleSize))]; L = Math.Min(L, px - 260f * S * bmm); R2 = Math.Max(R2, px + 260f * S * bmm); T0 = Math.Min(T0, py - flyH - (215f + 70f * bmm) * S - 60f * S * bmm); }
		float G = (GroundToy && tY > 0f) ? Math.Max(tY, tG) : Ground; Action<float, float, float, float> add = delegate(float x0, float y0, float x1, float y1) { L = Math.Min(L, x0); T0 = Math.Min(T0, y0); R2 = Math.Max(R2, x1); B = Math.Max(B, y1); };
		if (GroundToy && tY > 0f) add(TXs - 120f * S, tY - 280f * S, TXs + 120f * S, tY + 16f * S);
		switch (toy)
		{
			case Toy.Ball: add(bx - 34f * S, by - 34f * S, bx + 34f * S, by + 34f * S); break;
			case Toy.Mug: add(mugX - 60f * S, G - 70f * S, mugX + 60f * S, G + 16f * S); break;
			case Toy.Star: add(sx - 50f * S, sy - 50f * S, sx + 50f * S, sy + 50f * S); break;
			case Toy.Plane: add(plX - 80f * S, plY - 60f * S, plX + 80f * S, plY + 60f * S); break;
			case Toy.None: break;
			default: add(tX - 110f * S, G - 270f * S, tX + 110f * S, G + 16f * S); break;
		}
		foreach (Pt p in ps) add(p.X - 40f * S, p.Y - 40f * S, p.X + 40f * S, p.Y + 40f * S);
		int x = (int)Math.Floor(L), y = (int)Math.Floor(T0), x2 = (int)Math.Ceiling(R2), y2 = (int)Math.Ceiling(B);
		x = Math.Max(x, wa.Left); y = Math.Max(y, wa.Top - 200); x2 = Math.Min(x2, x + Wd); y2 = Math.Min(y2, y + Hd);
		return new Rectangle(x, y, Math.Max(8, x2 - x), Math.Max(8, Math.Min(Hd, y2 - y)));
	}
	private void Render()
	{
		if (canvas == null) return;
		Rectangle u = Union(); ux = u.X; uy = u.Y; uw = Math.Min(u.Width, Wd); uh = Math.Min(u.Height, Hd);
		using (Graphics g = Graphics.FromImage(canvas))
		{
			g.SetClip(new Rectangle(0, 0, uw, uh)); g.CompositingMode = CompositingMode.SourceCopy; g.Clear(Color.Transparent); g.CompositingMode = CompositingMode.SourceOver;
			g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.Bilinear; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
			float ox = ux, oy = uy; float lx = (float)Math.Round(px) - ox, ly = (float)Math.Round(py) - oy;
			float lift = (st == St.Fly || (st == St.Fall && flyH > 0f)) ? flyH : 0f;
			{ float sh = Math.Max(0.35f, 1f - lift / (260f * S)); float gy = st == St.Tramp ? Math.Min(TrSurf - oy, uh - 4f) : ((st == St.Held || st == St.Jump || st == St.Catch || st == St.Fall) ? Math.Min(FloorAt(px, py - 2f) - oy, uh - 4f) : ly); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(60 * sh), 0, 0, 0))) g.FillEllipse(b, lx - 34f * S * sh, gy - 6f * S, 68f * S * sh, 12f * S); }
			DrawToys(g, ox, oy);
			DrawPet(g, lx, ly - lift);
			foreach (Pt p in ps) DrawPart(g, p, ox, oy);
			if (sayT > 0f && say.Length > 0) DrawBubble(g, lx, ly - lift - 138f * S, say, Math.Min(1f, sayT * 3f));
		}
		BF bf = new BF { Op = 0, Flags = 0, Alpha = 255, Fmt = 1 }; PT dst = new PT { x = ux, y = uy }; SZ sz = new SZ { cx = uw, cy = uh }; PT src = new PT(); IntPtr sdc = GetDC(IntPtr.Zero);
		UpdateLayeredWindow(Handle, sdc, ref dst, ref sz, memDC, ref src, 0, ref bf, 2); ReleaseDC(IntPtr.Zero, sdc);
	}
	private void DrawToys(Graphics g, float ox, float oy)
	{
		float gx = 0f, gy = ((GroundToy && tY > 0f) ? tY : Ground) - oy;
		switch (toy)
		{
			case Toy.Ball: if (!carry) Spr.Draw(g, "ms_pet_toy_ball", bx - ox, by - oy, 32f * S, 32f * S * Spr.OH("ms_pet_toy_ball", Spr.Get("ms_pet_toy_ball")) / Spr.OW("ms_pet_toy_ball", Spr.Get("ms_pet_toy_ball")), brot); break;
			case Toy.Mug: if (st != St.Sip) DrawBottom(g, "ms_pet_toy_mug", mugX - ox, gy, S, 0f); break;
			case Toy.Star: if (!(starCaught && st == St.Catch)) { float pul = 1f + 0.08f * (float)Math.Sin(T * 8); DrawFxSpr(g, "ms_pet_toy_star", sx - ox, sy - oy, 40f * S * pul, 1f, (float)Math.Sin(T * 2) * 10f); } break;
			case Toy.Cookie: if (st != St.Eat) DrawBottom(g, "ms_pet_toy_cookie", tX - ox, gy, S, 0f); break;
			case Toy.Cushion: DrawBottom(g, "ms_pet_toy_cushion", tX - ox, gy + 2f * S, S, 0f); break;
			case Toy.Tramp: { GraphicsState gs0 = g.Save(); g.TranslateTransform(tX - ox, gy); g.ScaleTransform(1f + 0.07f * trC, 1f - 0.22f * trC); DrawBottom(g, "ms_pet_toy_tramp", 0f, 0f, S, 0f); g.Restore(gs0); break; }
			case Toy.Plane: DrawBottom(g, "ms_pet_toy_plane", plX - ox, plY - oy + 14f * S, S, plLand ? 0f : plFace * Clamp((float)(Math.Atan2(plVy, Math.Max(40f * S, Math.Abs(plVx))) * 57.3), -45f, 45f) + (float)Math.Sin(T * 5f) * 3f, plFace < 0); break;
		}
	}
	private static void DrawBottom(Graphics g, string n, float cx, float bottom, float sc, float rot) { DrawBottom(g, n, cx, bottom, sc, rot, false); }
	private static void DrawBottom(Graphics g, string n, float cx, float bottom, float sc, float rot, bool flip) { Spr.Frame(g, n, cx, bottom, sc, rot, flip); }
	private void DrawPet(Graphics g, float x, float y)
	{
		float breathe = (st == St.Walk || st == St.Chase || st == St.Fetch || st == St.Fly) ? 1f : 1f + 0.018f * (float)Math.Sin(T * 2.4f); float sq = squish;
		float sy2 = breathe * (1f - 0.22f * sq * (float)Math.Sin(sq * 3.14f * 0.5f + 0.0001f)), sx2 = 1f + 0.16f * sq; if (st == St.Tramp) { sy2 *= 1f - 0.14f * trC; sx2 *= 1f + 0.09f * trC; }
		string n; bool flip = face < 0; float rot = 0f, bob = 0f; double t = T; float yo = 0f;
		switch (st)
		{
			case St.Walk: n = startT < 0.27f ? P("wstart", Math.Min(2, (int)(startT / 0.09f))) : P("walk", ((int)(gait * 8f)) % 8); break;
			case St.Chase: n = startT < 0.36f ? P("rstart", Math.Min(3, (int)(startT / 0.09f))) : P("run", ((int)(gait * 8f)) % 8); break;
			case St.Fetch: n = P("fetch", ((int)(gait * 4f)) % 4); bob = -Math.Abs((float)Math.Sin(gait * 6.28318f)) * 3f * S; rot = face * 3f; break;
			case St.Kick: n = P("kick", stT < 0.14f ? 0 : (stT < 0.3f ? 1 : 2)); break;
			case St.Jump: n = P(vy < 0f ? "jumpup" : "jumpdn", 0); rot = face * (vy < 0f ? -3f : 4f); break;
			case St.Fall: n = P("jumpdn", 0); rot = face * 4f; break;
			case St.Fly: n = P("fly", Loop(t, 10, 6)); bob = (float)Math.Sin(t * 4) * 4f * S; rot = face * 3f; break;
			case St.Held: n = P("held", Loop(t, 2.2, 2)); rot = (float)Math.Sin(t * 4f) * 5f; flip = false; break;
			case St.Happy: { int[] hq = { 0, 1, 2, 1 }; n = stT > 0.7f ? P("stand", 2) : P("happy", hq[Loop(t, 5, 4)]); bob = -(0.5f - 0.5f * (float)Math.Cos(t * 6.0)) * 4.5f * S; flip = false; break; }   // 쓰다듬는 동안엔 프레임이 끊기지 않고 부드럽게 이어져요
			case St.Sleep: n = P("sleep", Loop(t, 1.1, 2)); flip = false; break;
			case St.Nap: n = P("sleep", Loop(t, 1.1, 2)); flip = false; yo = -10f * S; break;
			case St.Sip: n = P("sip", stT < 0.9f ? 0 : (stT < 1.9f ? 1 : (stT < 2.9f ? 2 : 3))); flip = false; break;
			case St.Eat: n = P("eat", stT < 0.2f ? 0 : (stT < 0.95f ? 1 : (stT < 1.6f ? 2 : 3))); flip = false; if (stT > 0.2f) { float w = (float)Math.Sin(stT * 36f); bob = Math.Abs(w) * -3.5f * S; rot = (float)Math.Sin(stT * 18f) * 3.5f; sx2 *= 1f + 0.05f * w; sy2 *= 1f - 0.05f * w; } break;
			case St.Tramp:
			{
				float h = TrSurf - py, hs = h / S; flip = face < 0;
				if (hs > 300f && Math.Abs(vy) < 900f * S) { n = P("fly", Loop(t, 12, 6)); rot = face * 4f; }
				else if (trT < 0.09f && hs < 14f) { n = P("tr", 0); flip = false; }
				else if (Math.Abs(vy) < 260f * S) { n = P("tr", 3); flip = false; }
				else if (vy < 0f) { n = P("tr", trT < 0.35f ? 1 : 2); flip = false; }
				else { n = P("tr", 4); flip = false; }
				if (hs < 14f && vy >= 0f) { n = P("tr", 5); flip = false; }
				break;
			}
			case St.Catch: n = P("catch", starCaught ? 2 : (vy < 0f ? 1 : 0)); break;
			case St.Sit: n = P("sit", (Loop(t + 0.0, 1, 4) == 3 && (t % 1.0) < 0.18) ? 1 : 0); flip = false; break;
			case St.Dizzy: n = P("sit", 0); rot = (float)Math.Sin(stT * 6f) * 6f; flip = false; break;
			case St.Wave: n = P("wave", Loop(stT, 3.5, 3)); flip = false; break;
			case St.Water: n = P("water", stT < 0.7f ? 0 : (stT < 1.5f ? 1 : (stT < 2.6f ? 2 : 3))); flip = false; break;
			case St.Stretch: n = P("stretch", stT < 1.3f ? 0 : (stT < 2.3f ? 1 : (stT < 3.3f ? 2 : 3))); flip = false; break;
			case St.Yawn: n = P("yawn", stT < 0.8f ? 0 : (stT < 1.8f ? 1 : 2)); flip = false; break;
			default: { double ph = t % 4.0; int k = ph < 0.16 ? 2 : (((int)(t % 9.0) == 7 && (t % 1.0) < 0.5) ? 3 : (Loop(t, 0.9, 2))); if (wakeT > 0f) k = wakeT > 0.45f ? 2 : (wakeT > 0.25f ? 2 : 0); n = P("stand", k); flip = st == St.Idle && face < 0; rot = (float)Math.Sin(t * 1.3) * 1.2f; break; }
		}
		bool turning = turnT > 0f && (st == St.Idle || st == St.Walk || st == St.Chase);
		if (turning) { int tnf = 4; int tk = Math.Min(tnf - 1, (int)((1f - turnT / turnDur) * tnf)); n = P(st == St.Chase ? "rturn" : "turn", tk); flip = turnFrom < 0; rot = 0f; bob = 0f; }
		else if (brakeT > 0f && st == St.Idle) { int bk = Math.Min(2, (int)((1f - brakeT / brakeDur) * 3f)); n = P(brakeK == 0 ? "wstop" : "rstop", bk); rot = 0f; flip = face < 0; }
		if (!turning && (flip || face > 0)) { if (st == St.Walk || st == St.Idle || st == St.Chase || st == St.Fetch || st == St.Fly || st == St.Jump || st == St.Fall) { flip = faceF < 0f; sx2 *= Math.Max(0.15f, Math.Abs(faceF)); } }
		GraphicsState gs = g.Save(); g.TranslateTransform((float)Math.Round(x), (float)Math.Round(y + yo + bob)); g.ScaleTransform(sx2, sy2); rot = (float)Math.Round(rot * 2f) / 2f;
		Spr.Frame(g, n, 0f, 0f, S, rot, flip);
		g.Restore(gs);
		if (st == St.Dizzy) { float hy = y - 94f * S; for (int i = 0; i < 3; i++) { double an = stT * 7.5 + i * 2.0944; float sx0 = x + (float)Math.Cos(an) * 36f * S, sy0 = hy + (float)Math.Sin(an) * 9f * S; DrawFxSpr(g, "ms_pet_toy_star", sx0, sy0, (13f + (float)Math.Sin(an) * 2f) * S, 1f, (float)(stT * 160.0)); } }
	}
	private static void DrawFxSpr(Graphics g, string n, float x, float y, float w, float a, float rot)
	{
		Bitmap b = Spr.Get(n); if (b == null) return; float h = w * b.Height / b.Width;
		using (ImageAttributes ia = new ImageAttributes())
		{
			ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = Math.Max(0f, Math.Min(1f, a)); ia.SetColorMatrix(cm);
			GraphicsState gs = g.Save(); g.TranslateTransform(x, y); if (rot != 0f) g.RotateTransform(rot);
			g.DrawImage(b, new Rectangle((int)(-w / 2), (int)(-h / 2), (int)w, (int)h), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia); g.Restore(gs);
		}
	}
	private void Crumbs(int n)
	{
		for (int i = 0; i < n; i++) ps.Add(new Pt { K = 6, X = px + ((float)R.NextDouble() * 24f - 12f) * S, Y = py - 78f * S + ((float)R.NextDouble() * 10f - 5f) * S, Vx = ((float)R.NextDouble() * 2f - 1f) * 130f * S, Vy = -(120f + (float)R.NextDouble() * 220f) * S, Life = 0.75f + (float)R.NextDouble() * 0.35f, Sz = R.Next(3) + (float)R.NextDouble() * 0.5f });
	}
	private static readonly Color[] CrumbC = { Color.FromArgb(196, 132, 66), Color.FromArgb(150, 94, 44), Color.FromArgb(78, 48, 30) };
	private void DrawPart(Graphics g, Pt p, float ox, float oy)
	{
		float u = p.Age / p.Life, a = u < 0.15f ? u / 0.15f : 1f - (u - 0.15f) / 0.85f; a = Math.Max(0f, Math.Min(1f, a)); float x = p.X - ox, y = p.Y - oy;
		switch (p.K)
		{
			case 0: DrawFxSpr(g, "ms_pet_fx0", x, y, p.Sz * 2.3f, a, (float)Math.Sin(p.Age * 6f + p.Sz) * 10f); break;
			case 1: DrawFxSpr(g, "ms_pet_fx1", x, y, p.Sz * 2.4f * (1f - u * 0.4f), a, p.Age * 140f); break;
			case 2: DrawFxSpr(g, "ms_pet_fx2", x, y, p.Sz * 2.0f * (1f + u * 0.6f), a, -8f + (float)Math.Sin(p.Age * 3f) * 8f); break;
			case 3: DrawFxSpr(g, "ms_pet_fx3", x, y, p.Sz * 4f * (1f + u * 0.5f), a * 0.8f, 0f); break;
			case 4: DrawFxSpr(g, "ms_pet_fx4", x, y, 20f * S, a, 0f); break;
			case 6: { float al = u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f; float sz = (3.2f + (p.Sz % 1f) * 2.2f) * S; GraphicsState gs6 = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(p.Age * 420f * (p.Sz > 1.5f ? 1f : -1f)); using (SolidBrush cb = new SolidBrush(Color.FromArgb((int)(255 * Math.Max(0f, al)), CrumbC[(int)p.Sz % 3]))) g.FillRectangle(cb, -sz / 2f, -sz / 2f, sz, sz); g.Restore(gs6); break; }
			default: DrawFxSpr(g, "ms_pet_fx5", x, y, 22f * S, a, 0f); break;
		}
	}
	internal static readonly float[] BubbleMul = { 0.8f, 1f, 1.25f, 1.55f, 1.9f };
	private void DrawBubble(Graphics g, float x, float y, string t, float a)
	{
		// 유리 느낌의 둥근 말풍선
		int A(int v) { return Math.Max(0, Math.Min(255, (int)(v * a))); }
		t = t.Replace("…", "...").Replace("♪", "~").Replace("·", ".");
		float bm = BubbleMul[Math.Max(0, Math.Min(4, Program.bubbleSize))]; float u = S * bm;
		using (Font f = BubbleFont.Make(t, 15f * sizeMul * Dpi.Scale * bm))
		using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Trimming = StringTrimming.None })
		{
			float maxW = 190f * u, padX = 15f * u, padY = 9f * u;
			SizeF m = g.MeasureString(t, f, new SizeF(maxW, 1000f), sf);
			float w = Math.Max(m.Width + padX * 2 + 4f * u, 96f * u), h = Math.Max(m.Height + padY * 2, 34f * u); float rad = Math.Min(20f * u, h / 2f);
			float bx0 = Clamp(x - w / 2f, 4f, Math.Max(5f, uw - w - 4f)); RectangleF r = new RectangleF(bx0, y - h, w, h);
			float tx = Clamp(x, r.X + rad + 8f * u, r.Right - rad - 8f * u), th = 12f * u, tw = 9f * u;
			float d = rad * 2f;
			Func<RectangleF, float, GraphicsPath> body = delegate(RectangleF q, float rr)
			{
				float dd = rr * 2f; GraphicsPath p = new GraphicsPath();
				p.AddArc(q.X, q.Y, dd, dd, 180, 90); p.AddArc(q.Right - dd, q.Y, dd, dd, 270, 90); p.AddArc(q.Right - dd, q.Bottom - dd, dd, dd, 0, 90);
				p.AddBezier(tx + tw, q.Bottom, tx + tw * 0.3f, q.Bottom, x + 1f * u, q.Bottom + th * 0.5f, x, q.Bottom + th);
				p.AddBezier(x, q.Bottom + th, x - 1f * u, q.Bottom + th * 0.5f, tx - tw * 0.3f, q.Bottom, tx - tw, q.Bottom);
				p.AddArc(q.X, q.Bottom - dd, dd, dd, 90, 90); p.CloseFigure(); return p;
			};
			for (int i = 4; i >= 1; i--) using (GraphicsPath sp = body(new RectangleF(r.X - i * 0.6f, r.Y + 3f * u + i * 0.6f, r.Width + i * 1.2f, r.Height + i * 0.6f), rad)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(A(11), 30, 70, 110))) g.FillPath(sb, sp);
			using (GraphicsPath gp = body(r, rad))
			{
				using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1f, r.Width, r.Height + 2f), Color.FromArgb(A(238), 255, 255, 255), Color.FromArgb(A(212), 214, 234, 252), 90f)) g.FillPath(lg, gp);
								using (Pen p = new Pen(Color.FromArgb(A(120), 120, 170, 215), Math.Max(1f, 1f * u))) { p.LineJoin = LineJoin.Round; g.DrawPath(p, gp); }
			}
			// 유리 광택: 위쪽 하이라이트와 작은 반짝임
			RectangleF hi = new RectangleF(r.X + 6f * u, r.Y + 3f * u, r.Width - 12f * u, r.Height * 0.42f);
			using (GraphicsPath hp = Gfx.Round(hi, Math.Min(hi.Height / 2f, rad - 2f * u))) using (LinearGradientBrush hb = new LinearGradientBrush(new RectangleF(hi.X, hi.Y - 1f, hi.Width, hi.Height + 2f), Color.FromArgb(A(170), 255, 255, 255), Color.FromArgb(A(8), 255, 255, 255), 90f)) g.FillPath(hb, hp);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(A(255), 38, 58, 84))) g.DrawString(t, f, b, new RectangleF(r.X + (w - m.Width) / 2f, r.Y + (h - m.Height) / 2f, m.Width + 4f * u, m.Height + 2f), sf);
		}
	}
}

// 말풍선 전용 둥글둥글한 글꼴 (주아체 · SIL OFL, 앱에서 쓰는 글자만 담았어요)
internal static class BubbleFont
{
	private static System.Drawing.Text.PrivateFontCollection pc; private static FontFamily fam; private static bool[] cov; private static bool tried;
	private static void Load()
	{
		if (tried) return; tried = true;
		try
		{
			using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("noto.j.gz"))
			using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(st, System.IO.Compression.CompressionMode.Decompress))
			using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
			{
				gz.CopyTo(ms); byte[] b = ms.ToArray(); try { cov = Fonts.ReadCmap(b); } catch { cov = null; }
				IntPtr p = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(b.Length); System.Runtime.InteropServices.Marshal.Copy(b, 0, p, b.Length);
				pc = new System.Drawing.Text.PrivateFontCollection(); pc.AddMemoryFont(p, b.Length); if (pc.Families.Length > 0) { fam = pc.Families[0]; if (!Works(fam)) fam = null; }
			}
		}
		catch { fam = null; }
	}
	// 실제로 글자가 그려지는지 한 번 확인해요 (안 그려지면 기본 글꼴 사용)
	private static bool Works(FontFamily f)
	{
		try
		{
			using (Font ft = new Font(f, 24f, FontStyle.Regular, GraphicsUnit.Pixel))
			using (Bitmap b1 = new Bitmap(90, 40, PixelFormat.Format32bppArgb))
			using (Bitmap b2 = new Bitmap(90, 40, PixelFormat.Format32bppArgb))
			{
				using (Graphics g = Graphics.FromImage(b1)) { g.Clear(Color.White); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.DrawString("가나", ft, Brushes.Black, 2f, 2f); }
				using (Graphics g = Graphics.FromImage(b2)) { g.Clear(Color.White); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.DrawString("나가", ft, Brushes.Black, 2f, 2f); }
				int diff = 0, ink = 0;
				for (int y = 0; y < 40; y++) for (int x = 0; x < 90; x++) { Color c1 = b1.GetPixel(x, y); if (c1.R < 128) ink++; if (c1 != b2.GetPixel(x, y)) diff++; }
				return ink > 20 && diff > 20;   // 서로 다른 글자가 서로 다르게 그려져야 진짜 글꼴이에요 (없는 글자는 똑같은 네모로 나와요)
			}
		}
		catch { }
		return false;
	}
	// 글자가 모두 들어 있을 때만 이 글꼴을 쓰고, 아니면 기본 글꼴로 돌아가요
	public static Font Make(string text, float px)
	{
		Load();
		if (fam != null && cov != null) { bool ok = true; foreach (char c in text) { if (c < 0x20 || c == '\n') continue; if (c >= cov.Length || !cov[c]) { ok = false; break; } } if (ok) return new Font(fam, px, FontStyle.Regular, GraphicsUnit.Pixel); }
		return new Font("맑은 고딕", px * 0.9f, FontStyle.Regular, GraphicsUnit.Pixel);
	}
}
