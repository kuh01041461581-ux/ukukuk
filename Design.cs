using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ─────────────────────────────────────────────────────────────
//  화면 배율 (DPI)
// ─────────────────────────────────────────────────────────────
internal static class Dpi
{
	// 화면이 작은데 배율이 크면(예: 1080p 에서 200%) 설정창(논리 높이 640)이 화면보다 커져 아래가 잘려요 → 전체(글자 포함)를 화면에 맞춰 줄여요
	public static float Fit = 1f;
	public static readonly float Scale = Calc();
	// 사용자가 '글자 크게'를 켜면 화면 전체 배율을 키운다 (글자 + 간격이 같이 커져서 레이아웃이 안 깨진다)
	public static float UserScale = 1f;
	private static float Calc()
	{
		float baseS = 1f;
		try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) baseS = Math.Max(1f, g.DpiX / 96f); }
		catch { }
			try { float capFit = (Screen.PrimaryScreen.WorkingArea.Height - 24f) / 680f; float want = Math.Max(1f, capFit); if (want < baseS) { Fit = want / baseS; baseS = want; } } catch { }
		try
		{
			string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OvertimeReminder", "settings.ini");
			if (File.Exists(p))
				foreach (string line in File.ReadAllLines(p))
					if (line.StartsWith("uiScale="))
					{
						float u; if (float.TryParse(line.Substring(8), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out u) && u > 1f && u <= 1.4f) UserScale = u;
					}
			if (UserScale > 1f)
			{
				// 작은 화면에서는 창이 화면보다 커지지 않게 상한을 둔다 (설정창 높이 640 기준)
				float cap = (Screen.PrimaryScreen.WorkingArea.Height - 24f) / 680f;
				float want = baseS * UserScale;
				return Math.Max(baseS, Math.Min(want, cap));
			}
		}
		catch { }
		return baseS;
	}
	public static int Z(int v) { return (int)Math.Round(v * Scale); }
	public static float Zf(float v) { return v * Scale; }
}

// ─────────────────────────────────────────────────────────────
//  폰트: 윈도우 기본 '맑은 고딕' — 윈도우 화면용으로 손질(힌팅)되어 있어 작은 글씨도 또렷하다.
//  (내장 글꼴은 글자 획이 화면 격자에 안 맞아 깨져 보여서 쓰지 않는다)
// ─────────────────────────────────────────────────────────────
internal static class Fonts
{
	// 노토 산스 KR (구글 오픈소스, SIL OFL): 보통은 Medium, 강조는 Bold. 앱 안에 압축해서 넣어 둬요.
	private static Dictionary<string, Font> cache = new Dictionary<string, Font>();
	private static System.Drawing.Text.PrivateFontCollection pfc;
	private static FontFamily famM, famB;
	public static bool Loaded;
	public static bool Gdi { get { return !Loaded; } }
	static Fonts()
	{
		try
		{
			if (Environment.GetEnvironmentVariable("OT_NOFONT") == "1") return;
			pfc = new System.Drawing.Text.PrivateFontCollection();
			Add("noto.m.gz"); Add("noto.b.gz");
			foreach (FontFamily ff in pfc.Families) { if (ff.Name == "NotoKR") famB = ff; else if (ff.Name.StartsWith("NotoKR")) famM = ff; }
			Loaded = famM != null && famB != null && Environment.GetEnvironmentVariable("MG_SYSFONT") == null;
		}
		catch { Loaded = false; }
	}
	private static bool[] cov;      // 앱에 넣은 노토 글꼴이 가진 글자 (BMP)
	private static void Add(string res)
	{
		using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(res))
		using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(s, System.IO.Compression.CompressionMode.Decompress))
		using (MemoryStream ms = new MemoryStream())
		{
			gz.CopyTo(ms); byte[] b = ms.ToArray();
			if (cov == null) { try { cov = ReadCmap(b); } catch { cov = null; } }
			IntPtr p = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(b.Length);
			System.Runtime.InteropServices.Marshal.Copy(b, 0, p, b.Length);
			pfc.AddMemoryFont(p, b.Length);
		}
	}
	private static int U16(byte[] b, int o) { return (b[o] << 8) | b[o + 1]; }
	private static long U32(byte[] b, int o) { return ((long)b[o] << 24) | ((long)b[o + 1] << 16) | ((long)b[o + 2] << 8) | b[o + 3]; }
	// 글꼴 파일의 글자표(cmap)를 읽어서, 이 글꼴이 실제로 가진 글자만 표시해 둬요
	internal static bool[] ReadCmap(byte[] b)
	{
		int nt = U16(b, 4), cm = -1;
		for (int i = 0; i < nt; i++) { int o = 12 + i * 16; if (b[o] == 'c' && b[o + 1] == 'm' && b[o + 2] == 'a' && b[o + 3] == 'p') cm = (int)U32(b, o + 8); }
		if (cm < 0) return null;
		int n = U16(b, cm + 2), sub = -1;
		for (int i = 0; i < n; i++)
		{
			int o = cm + 4 + i * 8, pl = U16(b, o), en = U16(b, o + 2); int off = cm + (int)U32(b, o + 4);
			int fmt = U16(b, off);
			if (fmt == 12 && ((pl == 3 && en == 10) || pl == 0)) { sub = off; break; }
			if (fmt == 4 && sub < 0 && ((pl == 3 && en == 1) || pl == 0)) sub = off;
		}
		if (sub < 0) return null;
		bool[] r = new bool[65536];
		if (U16(b, sub) == 12)
		{
			long ng = U32(b, sub + 12);
			for (long i = 0; i < ng; i++) { int o = sub + 16 + (int)i * 12; long st = U32(b, o), en = U32(b, o + 4); for (long c = st; c <= en && c < 65536; c++) r[c] = true; }
		}
		else
		{
			int sc = U16(b, sub + 6) / 2, endO = sub + 14, stO = endO + sc * 2 + 2, dO = stO + sc * 2, roO = dO + sc * 2;
			for (int i = 0; i < sc; i++)
			{
				int en = U16(b, endO + i * 2), st = U16(b, stO + i * 2), ro = U16(b, roO + i * 2);
				for (int c = st; c <= en && c < 0xFFFF; c++)
				{
					if (ro == 0) { r[c] = true; continue; }
					int g = U16(b, roO + i * 2 + ro + (c - st) * 2); if (g != 0) r[c] = true;
				}
			}
		}
		return r;
	}
	// 글꼴에 없는 글자(드문 한글·특수문자·기호)가 하나라도 있으면 false
	public static bool Covers(string s)
	{
		if (cov == null) return true;
		foreach (char c in s) { if (c < 0x20 || c == '\u00a0' || c == '\u200b') continue; if (!cov[c]) return false; }
		return true;
	}
	private static readonly Dictionary<string, Font> fbCache = new Dictionary<string, Font>();
	// 빠진 글자를 위해 윈도우 기본 '맑은 고딕'으로 대신 그려요 (네모로 깨지는 것 방지)
	public static Font Fallback(Font f)
	{
		string key = f.Size + "/" + (int)f.Style; Font x;
		if (fbCache.TryGetValue(key, out x)) return x;
		x = new Font("맑은 고딕", f.Size, f.Style, GraphicsUnit.Point); fbCache[key] = x; return x;
	}
	public static bool IsPrivate(Font f) { return Loaded && f != null && f.FontFamily.Name.StartsWith("NotoKR"); }

	// w: 0 보통 · 1 세미볼드 · 2 볼드
	public static Font F(float pt, int w)
	{
		string key = pt.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + w;
		Font f;
		if (cache.TryGetValue(key, out f)) return f;
		f = Make(pt, w);
		cache[key] = f;
		return f;
	}

	private static Font Make(float pt, int w)
	{
		if (pt < 10f) pt += 0.5f;     // 작은 글씨는 살짝 키워서 또렷하게
		pt *= Dpi.Fit;
		if (Loaded) return w >= 1 ? new Font(famB, pt, FontStyle.Bold, GraphicsUnit.Point) : new Font(famM, pt, FontStyle.Regular, GraphicsUnit.Point);
		return new Font("맑은 고딕", pt, w >= 1 ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
	}

	// 숫자·시계 전용도 같은 노토 산스
	public static Font Num(float pt, int w) { return F(pt, w >= 1 ? 1 : 0); }

	// 입력칸처럼 윈도우 기본 컨트롤이 쓰는 글꼴 (이런 칸은 앱에 넣은 글꼴을 못 써요)
	public static Font Native(float pt) { return new Font("맑은 고딕", pt * Dpi.Fit, FontStyle.Regular, GraphicsUnit.Point); }
}

// ─────────────────────────────────────────────────────────────
//  색 토큰 — Notion 의 차분한 회색 · Linear 의 인디고 포인트 · Windows 11 의 둥근 모서리
// ─────────────────────────────────────────────────────────────
internal static class Theme
{
	// 0 밝게 · 1 어둡게 · 2 시스템 설정 따라가기 (settings.ini 의 theme=)
	public static int Mode = 0;
	public static bool Dark = false;
	// 포인트 색 (0 마룡이 청록 · 1 블루 · 2 보라 · 3 코랄 · 4 오렌지 · 5 그래파이트)
	public static int AccentIdx = 0;
	// 분위기: 0 기본 · 1 종이·문구 · 2 말랑 젤리 · 3 유리·파스텔
	public static int Style = 0;
	public static readonly string[] StyleNames = { "기본", "종이", "젤리", "유리" };
	public static readonly string[] AccentNames = { "마룡이 청록", "맑은 블루", "라벤더", "코랄", "선셋", "그래파이트" };
	public static readonly Color[] AccentBase = { Color.FromArgb(14, 138, 124), Color.FromArgb(52, 104, 232), Color.FromArgb(118, 88, 218), Color.FromArgb(226, 84, 108), Color.FromArgb(214, 106, 28), Color.FromArgb(66, 80, 102) };

	public static Color Bg, Page, Side, Raised, Line, LineStrong, Ink, Sub, Muted, Hover;
	public static Color Accent, AccentBtn, AccentHover, AccentDown, AccentSoft;
	public static Color Gold, Green, GreenSoft, Red, Amber, Blue, ToggleOff, DangerLine, DangerSoft;

	static Theme()
	{
		try
		{
			string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OvertimeReminder", "settings.ini");
			if (File.Exists(p))
				foreach (string line in File.ReadAllLines(p))
					{
						if (line.StartsWith("theme=")) { int m; if (int.TryParse(line.Substring(6).Trim(), out m) && m >= 0 && m <= 2) Mode = m; }
						if (line.StartsWith("style=")) { int m; if (int.TryParse(line.Substring(6).Trim(), out m) && m >= 0 && m < 4) Style = m; }
						if (line.StartsWith("accent=")) { int m; if (int.TryParse(line.Substring(7).Trim(), out m) && m >= 0 && m < 6) AccentIdx = m; }
					}
		}
		catch { }
		try { string es = Environment.GetEnvironmentVariable("OT_STYLE"); int si; if (es != null && int.TryParse(es, out si) && si >= 0 && si < 4) Style = si; } catch { }
		try { string ea = Environment.GetEnvironmentVariable("OT_ACCENT"); int ai; if (ea != null && int.TryParse(ea, out ai) && ai >= 0 && ai < 6) AccentIdx = ai; } catch { }
		try { string ev = Environment.GetEnvironmentVariable("OT_THEME"); if (ev == "0" || ev == "1" || ev == "2") Mode = int.Parse(ev); } catch { }
		Apply(Mode);
	}

	static bool SystemDark()
	{
		try
		{
			using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
			{
				object v = k == null ? null : k.GetValue("AppsUseLightTheme");
				return v is int && (int)v == 0;
			}
		}
		catch { return false; }
	}

	public static void Apply(int mode)
	{
		Mode = mode;
		Dark = mode == 1 || (mode == 2 && SystemDark());
		Gold = Color.FromArgb(245, 183, 59);
		Amber = Color.FromArgb(217, 130, 43);
		if (!Dark)
		{
			// 따뜻한 크림 바탕 + 마룡이의 부드러운 청록 포인트
			Bg = Color.FromArgb(255, 254, 252); Page = Color.FromArgb(251, 248, 243); Side = Color.FromArgb(244, 240, 233); Raised = Color.FromArgb(255, 255, 253);
			Line = Color.FromArgb(238, 232, 223); LineStrong = Color.FromArgb(224, 216, 204);
			Ink = Color.FromArgb(40, 37, 34); Sub = Color.FromArgb(98, 92, 84); Muted = Color.FromArgb(140, 133, 124);
			Hover = Color.FromArgb(238, 232, 222);
			Accent = Color.FromArgb(14, 138, 124); AccentBtn = Color.FromArgb(20, 148, 133);
			AccentHover = Color.FromArgb(10, 128, 115); AccentDown = Color.FromArgb(6, 112, 100);
			AccentSoft = Color.FromArgb(224, 245, 239);
			Green = Color.FromArgb(52, 160, 100); GreenSoft = Color.FromArgb(230, 246, 236);
			Red = Color.FromArgb(226, 84, 84); Blue = Color.FromArgb(70, 120, 230);
			ToggleOff = Color.FromArgb(208, 201, 191); DangerLine = Color.FromArgb(246, 200, 198); DangerSoft = Color.FromArgb(254, 240, 237);
		}
		else
		{
			// 포근한 밤색: 차가운 회색 대신 아주 살짝 따뜻한 먹색
			Bg = Color.FromArgb(46, 44, 46); Page = Color.FromArgb(35, 34, 37); Side = Color.FromArgb(28, 27, 30); Raised = Color.FromArgb(66, 63, 68);
			Line = Color.FromArgb(58, 56, 60); LineStrong = Color.FromArgb(80, 77, 83);
			Ink = Color.FromArgb(246, 243, 238); Sub = Color.FromArgb(200, 195, 188); Muted = Color.FromArgb(152, 147, 141);
			Hover = Color.FromArgb(56, 54, 58);
			Accent = Color.FromArgb(94, 212, 192); AccentBtn = Color.FromArgb(18, 142, 128);
			AccentHover = Color.FromArgb(24, 156, 141); AccentDown = Color.FromArgb(10, 120, 108);
			AccentSoft = Color.FromArgb(30, 66, 64);
			Green = Color.FromArgb(104, 208, 148); GreenSoft = Color.FromArgb(32, 60, 48);
			Red = Color.FromArgb(255, 125, 120); Blue = Color.FromArgb(132, 170, 255);
			ToggleOff = Color.FromArgb(90, 87, 93); DangerLine = Color.FromArgb(124, 62, 62); DangerSoft = Color.FromArgb(76, 42, 44);
		}
		if (AccentIdx > 0 && AccentIdx < AccentBase.Length)
		{
			Color b = AccentBase[AccentIdx];
			if (!Dark)
			{
				Accent = b; AccentBtn = Mix(b, Color.White, 0.06); AccentHover = Mix(b, Color.Black, 0.10); AccentDown = Mix(b, Color.Black, 0.24);
				AccentSoft = Mix(Color.White, b, 0.13);
			}
			else
			{
				Accent = Mix(b, Color.White, 0.46); AccentBtn = Mix(b, Color.Black, 0.04); AccentHover = Mix(b, Color.White, 0.10); AccentDown = Mix(b, Color.Black, 0.18);
				AccentSoft = Mix(Color.FromArgb(40, 38, 42), b, 0.34);
			}
		}
		Tint();
	}

	// 분위기별로 바탕·글자 색을 살짝 바꿔요 (포인트 색은 그대로)
	private static void Tint()
	{
		if (Style == 0) return;
		if (!Dark)
		{
			if (Style == 1)   // 종이·문구: 누런 종이 + 먹색에 가까운 갈색 글씨
			{
				Bg = Color.FromArgb(255, 251, 241); Page = Color.FromArgb(248, 240, 224); Side = Color.FromArgb(240, 230, 210); Raised = Color.FromArgb(255, 253, 246);
				Line = Color.FromArgb(230, 218, 196); LineStrong = Color.FromArgb(212, 196, 168); Hover = Color.FromArgb(236, 224, 202);
				Ink = Color.FromArgb(54, 42, 32); Sub = Color.FromArgb(110, 94, 76); Muted = Color.FromArgb(150, 134, 114); ToggleOff = Color.FromArgb(212, 200, 178);
			}
			else if (Style == 2)   // 말랑 젤리: 사탕 같은 연분홍·크림
			{
				Bg = Color.FromArgb(255, 252, 253); Page = Color.FromArgb(253, 246, 249); Side = Color.FromArgb(249, 238, 244); Raised = Color.White;
				Line = Color.FromArgb(244, 228, 236); LineStrong = Color.FromArgb(234, 212, 224); Hover = Color.FromArgb(246, 232, 240);
				Ink = Color.FromArgb(52, 38, 50); Sub = Color.FromArgb(112, 94, 108); Muted = Color.FromArgb(158, 140, 154); ToggleOff = Color.FromArgb(226, 208, 220);
			}
			else   // 유리·파스텔: 맑은 하늘빛 바탕
			{
				Bg = Color.FromArgb(251, 253, 255); Page = Color.FromArgb(240, 246, 253); Side = Color.FromArgb(230, 240, 250); Raised = Color.White;
				Line = Color.FromArgb(214, 227, 242); LineStrong = Color.FromArgb(190, 208, 230); Hover = Color.FromArgb(222, 234, 248);
				Ink = Color.FromArgb(34, 42, 58); Sub = Color.FromArgb(84, 98, 120); Muted = Color.FromArgb(132, 146, 168); ToggleOff = Color.FromArgb(198, 210, 228);
			}
		}
		else
		{
			if (Style == 1) { Bg = Color.FromArgb(52, 46, 40); Page = Color.FromArgb(40, 35, 30); Side = Color.FromArgb(32, 28, 24); Raised = Color.FromArgb(72, 64, 56); Line = Color.FromArgb(66, 58, 50); LineStrong = Color.FromArgb(90, 80, 68); Hover = Color.FromArgb(62, 55, 48); }
			else if (Style == 2) { Bg = Color.FromArgb(52, 44, 50); Page = Color.FromArgb(40, 33, 40); Side = Color.FromArgb(32, 26, 32); Raised = Color.FromArgb(74, 62, 72); Line = Color.FromArgb(68, 56, 66); LineStrong = Color.FromArgb(92, 78, 90); Hover = Color.FromArgb(62, 52, 60); }
			else { Bg = Color.FromArgb(40, 46, 58); Page = Color.FromArgb(30, 35, 46); Side = Color.FromArgb(24, 28, 38); Raised = Color.FromArgb(58, 66, 82); Line = Color.FromArgb(52, 60, 76); LineStrong = Color.FromArgb(76, 86, 106); Hover = Color.FromArgb(50, 58, 74); }
		}
	}

	public static Font F(float pt, int w) { return Fonts.F(pt, w); }

	public static Color Mix(Color a, Color b, double t)
	{
		if (t < 0) t = 0; if (t > 1) t = 1;
		return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
	}

	// 시간대 상태: 0 여유 · 1 서두르기 · 2 마감 임박 · 3 지남
	public static int StateFor(TimeSpan left)
	{
		if (left.TotalSeconds <= 0) return 3;
		if (left.TotalMinutes < 10) return 2;
		if (left.TotalMinutes < 20) return 1;
		return 0;
	}

	public static Color StateColor(int st)
	{
		switch (st)
		{
			case 1: return Amber;
			case 2: return Red;
			case 3: return Muted;
			default: return Accent;
		}
	}
}

// ─────────────────────────────────────────────────────────────
//  그리기 도우미
// ─────────────────────────────────────────────────────────────
internal static class Gfx
{
	public static void HQ(Graphics g)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
	}

	public static GraphicsPath Round(RectangleF r, float rad)
	{
		GraphicsPath p = new GraphicsPath();
		float d = Math.Max(0.01f, Math.Min(rad * 2f, Math.Min(r.Width, r.Height)));
		p.AddArc(r.X, r.Y, d, d, 180f, 90f);
		p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
		p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
		p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
		p.CloseFigure();
		return p;
	}

	// 종이 결: 작은 점무늬 타일을 한 번 만들어 두고 깔아요 (페이지 배경을 만들 때 딱 한 번)
	private static Bitmap grainTile;
	public static void Grain(Bitmap b)
	{
		try
		{
			if (grainTile == null)
			{
				Bitmap t = new Bitmap(96, 96, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); Random r = new Random(11);
				for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++) { int v = r.Next(100); if (v < 34) t.SetPixel(x, y, Color.FromArgb(10 + r.Next(10), 120, 90, 50)); else if (v > 94) t.SetPixel(x, y, Color.FromArgb(34, 255, 255, 255)); }
				for (int i = 0; i < 14; i++) { int x = r.Next(80), y = r.Next(96), l = 4 + r.Next(10); for (int k = 0; k < l && x + k < 96; k++) t.SetPixel(x + k, Math.Min(95, y + (k / 4)), Color.FromArgb(12, 110, 80, 40)); }
				grainTile = t;
			}
			using (Graphics g = Graphics.FromImage(b)) using (TextureBrush tb = new TextureBrush(grainTile)) { g.FillRectangle(tb, 0, 0, b.Width, b.Height); }
		}
		catch { }
	}
	// 유리: 비스듬히 스치는 빛결
	public static void Sheen(Bitmap b)
	{
		try
		{
			using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; float w = b.Width, h = b.Height; int a = Theme.Dark ? 9 : 26;
				for (int i = 0; i < 3; i++) { float x = w * (0.18f + i * 0.34f), bw = w * (0.05f + 0.025f * i); using (SolidBrush sb = new SolidBrush(Color.FromArgb(a, 255, 255, 255))) g.FillPolygon(sb, new PointF[] { new PointF(x, 0), new PointF(x + bw, 0), new PointF(x - h * 0.42f + bw, h), new PointF(x - h * 0.42f, h) }); } }
		}
		catch { }
	}

	public static void Fill(Graphics g, RectangleF r, float rad, Color c)
	{
		using (GraphicsPath p = Round(r, rad)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
	}

	public static void Border(Graphics g, RectangleF r, float rad, Color c, float w)
	{
		using (GraphicsPath p = Round(r, rad)) using (Pen pen = new Pen(c, w)) g.DrawPath(pen, p);
	}

	// 글자가 칸보다 길면 글자 크기를 알아서 줄여서 절대 잘리지 않게 해요
	private static readonly Dictionary<string, float> fitK = new Dictionary<string, float>();
	private static readonly Dictionary<string, Font> fitF = new Dictionary<string, Font>();
	private static Font Scaled(Font f, float k)
	{
		string key = f.Name + "|" + (int)f.Style + "|" + Math.Round(f.Size * k, 1);
		Font o; if (fitF.TryGetValue(key, out o)) return o;
		try { o = new Font(f.FontFamily, (float)Math.Round(f.Size * k, 1), f.Style, GraphicsUnit.Point); } catch { o = f; }
		fitF[key] = o; return o;
	}
	// 먼저 칸 높이가 허락하면 줄바꿈으로 해결하고, 그래도 안 되면 그때 글자를 줄여요
	private static Font Fit(Graphics g, string s, Font f, RectangleF r, ref bool wrap)
	{
		if (r.Width < 10 || r.Height < 6) return f;
		string key = f.Name + "|" + f.Size + "|" + (int)f.Style + "|" + (int)r.Width + "|" + (int)r.Height + "|" + (wrap ? 1 : 0) + "|" + s;
		float k;
		if (!fitK.TryGetValue(key, out k))
		{
			float avail = r.Width - 3f; bool w2 = wrap; k = 1f;
			try
			{
				if (!w2 && Measure(g, s, f) > avail && r.Height >= f.GetHeight(g) * 1.9f) w2 = true;
				if (!w2) { float w = Measure(g, s, f); if (w > avail) k = Math.Max(0.72f, avail / w * 0.985f); }
				else
				{
					float[] ks = { 1f, 0.94f, 0.88f, 0.82f, 0.76f, 0.7f, 0.64f }; k = 0.64f;
					foreach (float q in ks) { if (MeasureH(g, s, q == 1f ? f : Scaled(f, q), avail) <= r.Height + 1f) { k = q; break; } }
				}
			}
			catch { k = 1f; w2 = wrap; }
			k = w2 && !wrap ? -k : k;      // 음수 = 줄바꿈으로 바꿨음
			if (fitK.Count > 4000) fitK.Clear();
			fitK[key] = k;
		}
		if (k < 0) { wrap = true; k = -k; }
		return k >= 0.999f ? f : Scaled(f, k);
	}

	public static void Text(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h, StringAlignment v, bool wrap)
	{
		if (string.IsNullOrEmpty(s)) return;
		if (Fonts.IsPrivate(f) && !Fonts.Covers(s)) f = Fonts.Fallback(f);
		f = Fit(g, s, f, r, ref wrap);
		if (c.A == 255 && !Fonts.IsPrivate(f))
		{
			TextFormatFlags fl = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
			fl |= h == StringAlignment.Center ? TextFormatFlags.HorizontalCenter : h == StringAlignment.Far ? TextFormatFlags.Right : TextFormatFlags.Left;
			fl |= v == StringAlignment.Center ? TextFormatFlags.VerticalCenter : v == StringAlignment.Far ? TextFormatFlags.Bottom : TextFormatFlags.Top;
			fl |= wrap ? TextFormatFlags.WordBreak : (TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			Rectangle ri = new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Ceiling(r.Width), (int)Math.Ceiling(r.Height));
			TextRenderer.DrawText(g, s, f, ri, c, fl);
			return;
		}
		using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip))
		{
			sf.Alignment = h; sf.LineAlignment = v;
			sf.HotkeyPrefix = HotkeyPrefix.None;
			sf.Trimming = wrap ? StringTrimming.Word : StringTrimming.EllipsisCharacter;
			if (!wrap) sf.FormatFlags |= StringFormatFlags.NoWrap;
			TextRenderingHint oh = g.TextRenderingHint;
			if (Fonts.IsPrivate(f)) g.TextRenderingHint = TextRenderingHint.AntiAlias;
			using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
			g.TextRenderingHint = oh;
		}
	}

	public static float Measure(Graphics g, string s, Font f)
	{
		if (Fonts.IsPrivate(f) && !Fonts.Covers(s)) f = Fonts.Fallback(f);
		if (!Fonts.IsPrivate(f))
			return TextRenderer.MeasureText(g, s, f, new Size(4000, 1000), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
		using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic)) return g.MeasureString(s, f, 4000, sf).Width;
	}

	// 게임·설명 카드처럼 "96dpi 기준 논리 좌표"로 그리는 곳 전용 측정: 화면 배율(125~200%)이나 글꼴 대체(맑은 고딕)와 상관없이 그려지는 크기와 같은 값을 줘요
	private static Graphics logicalG;
	private static Graphics LogicalG { get { if (logicalG == null) { Bitmap b = new Bitmap(8, 8, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); b.SetResolution(96f, 96f); logicalG = Graphics.FromImage(b); logicalG.PageUnit = GraphicsUnit.Pixel; } return logicalG; } }
	public static float MeasureL(string s, Font f)
	{
		if (Fonts.IsPrivate(f) && !Fonts.Covers(s)) f = Fonts.Fallback(f);
		using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic)) return LogicalG.MeasureString(s, f, 4000, sf).Width;
	}
	public static float MeasureHL(string s, Font f, float width)
	{
		if (Fonts.IsPrivate(f) && !Fonts.Covers(s)) f = Fonts.Fallback(f);
		return LogicalG.MeasureString(s, f, (int)width).Height;
	}

	public static float MeasureH(Graphics g, string s, Font f, float width)
	{
		if (Fonts.IsPrivate(f) && !Fonts.Covers(s)) f = Fonts.Fallback(f);
		if (!Fonts.IsPrivate(f))
			return TextRenderer.MeasureText(g, s, f, new Size((int)width, 4000), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak).Height;
		return g.MeasureString(s, f, (int)width).Height;
	}

	public static GraphicsPath Star(float cx, float cy, float ro, float ri)
	{
		PointF[] pts = new PointF[10];
		for (int i = 0; i < 10; i++)
		{
			double a = -Math.PI / 2 + i * Math.PI / 5;
			float r = (i % 2 == 0) ? ro : ri;
			pts[i] = new PointF(cx + (float)(Math.Cos(a) * r), cy + (float)(Math.Sin(a) * r));
		}
		GraphicsPath p = new GraphicsPath();
		p.AddPolygon(pts);
		return p;
	}
}

// ─────────────────────────────────────────────────────────────
//  서울의 해 뜨고 지는 시각에 맞춰 바뀌는 하늘색 (벤치마킹 앱의 '퇴근길 하늘' 아이디어)
// ─────────────────────────────────────────────────────────────
internal static class Sky
{
	public static DateTime Now()
	{
		string o = Environment.GetEnvironmentVariable("OT_HOUR");
		DateTime n = DateTime.Now; double h;
		if (o != null && double.TryParse(o, out h)) return n.Date.AddHours(h);
		return n;
	}

	private const double LAT = 37.5665, LON = 126.978;
	private const int TZ = 9;

	private static readonly double[] EvM = { -200, -70, -20, 0, 14, 32, 55, 95, 170, 330 };
	private static readonly int[] EvT = { 0x3F7BD6, 0x4C78C9, 0x4E63B2, 0x43489A, 0x383886, 0x29296C, 0x1C1D53, 0x12143E, 0x0B0E2C, 0x070920 };
	private static readonly int[] EvH = { 0xA8CCF0, 0xF2D3A6, 0xFFB06E, 0xFF8A57, 0xFF667A, 0xB65CC9, 0x69419F, 0x33296F, 0x1B1C4C, 0x11143A };
	private static readonly double[] MoM = { -150, -70, -32, -10, 5, 60, 200 };
	private static readonly int[] MoT = { 0x070920, 0x0E1340, 0x24317A, 0x3A55A6, 0x4E70C0, 0x5A8ED8, 0x3F7BD6 };
	private static readonly int[] MoH = { 0x11143A, 0x2B2C69, 0x7B58AE, 0xFF9A78, 0xFFC48C, 0xCFE3F5, 0xA8CCF0 };

	public static void SunTimes(DateTime d, out DateTime rise, out DateTime set)
	{
		double g = (2 * Math.PI / 365) * (d.DayOfYear - 1);
		double eq = 229.18 * (7.5e-05 + 0.001868 * Math.Cos(g) - 0.032077 * Math.Sin(g) - 0.014615 * Math.Cos(2 * g) - 0.040849 * Math.Sin(2 * g));
		double decl = 0.006918 - 0.399912 * Math.Cos(g) + 0.070257 * Math.Sin(g) - 0.006758 * Math.Cos(2 * g) + 0.000907 * Math.Sin(2 * g) - 0.002697 * Math.Cos(3 * g) + 0.00148 * Math.Sin(3 * g);
		double lat = LAT * Math.PI / 180;
		double c = Math.Cos(90.833 * Math.PI / 180) / (Math.Cos(lat) * Math.Cos(decl)) - Math.Tan(lat) * Math.Tan(decl);
		c = Math.Max(-1, Math.Min(1, c));
		double ha = Math.Acos(c) * 180 / Math.PI;
		DateTime b = d.Date;
		rise = b.AddMinutes(720 - 4 * (LON + ha) - eq + TZ * 60);
		set = b.AddMinutes(720 - 4 * (LON - ha) - eq + TZ * 60);
	}

	private static Color Hex(int v) { return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255); }

	private static void Interp(double[] m, int[] top, int[] hor, double x, out Color t, out Color h)
	{
		if (x <= m[0]) { t = Hex(top[0]); h = Hex(hor[0]); return; }
		for (int i = 1; i < m.Length; i++)
		{
			if (x <= m[i])
			{
				double k = (x - m[i - 1]) / (m[i] - m[i - 1]);
				k = k * k * (3 - 2 * k);
				t = Theme.Mix(Hex(top[i - 1]), Hex(top[i]), k);
				h = Theme.Mix(Hex(hor[i - 1]), Hex(hor[i]), k);
				return;
			}
		}
		t = Hex(top[top.Length - 1]); h = Hex(hor[hor.Length - 1]);
	}

	public static void Colors(DateTime t, out Color top, out Color hor)
	{
		DateTime rise, set;
		SunTimes(t, out rise, out set);
		if (t.Hour < 12) Interp(MoM, MoT, MoH, (t - rise).TotalMinutes, out top, out hor);
		else Interp(EvM, EvT, EvH, (t - set).TotalMinutes, out top, out hor);
	}

	// 0(낮) ~ 1(깊은 밤)
	public static double Darkness(DateTime t)
	{
		Color top, hor;
		Colors(t, out top, out hor);
		double lum = (0.2126 * top.R + 0.7152 * top.G + 0.0722 * top.B) / 255;
		return Math.Max(0, Math.Min(1, (0.33 - lum) / 0.27));
	}
}

// ─────────────────────────────────────────────────────────────
//  컨트롤 공통 바탕: 깜빡임 없는 직접 그리기
// ─────────────────────────────────────────────────────────────
// 아주 은은한 색 번짐(그라데이션)을 깔아 주는 배경 — 자식 컨트롤도 같은 그림을 이어서 그려 네모 자국이 안 생겨요
internal interface IBackHost { Color BaseColor { get; } void PaintBack(Graphics g, Control child); }
internal static class Wash
{
	public static Point Off(Control c) { int x = 0, y = 0; for (Control p = c; p != null && !(p is Form); p = p.Parent) { x += p.Left; y += p.Top; } return new Point(x, y); }
	// 부모가 배경 호스트면 그 그림을, 아니면 부모 색을 깐다
	public static void Under(Control c, Graphics g)
	{
		IBackHost h = c.Parent as IBackHost;
		if (h != null) h.PaintBack(g, c); else g.Clear(c.Parent != null ? c.Parent.BackColor : Theme.Bg);
	}
	// 창 전체 좌표 기준으로: 오른쪽 위 청록 번짐 + 왼쪽 아래 푸른 번짐
	public static void Glow(Graphics g, Control host)
	{
		Form f = host.FindForm(); GlowAt(g, f != null ? f.ClientSize : host.Size, Off(host));
	}
	public static void GlowAt(Graphics g, Size fs, Point o)
	{
		GraphicsState s = g.Save();
		try
		{
			g.TranslateTransform(-o.X, -o.Y);
			int a1 = Theme.Dark ? 42 : 36, a2 = Theme.Dark ? 24 : 18; float dk = Theme.Dark ? 0.7f : 1f; float W = fs.Width, H = fs.Height;
			if (Theme.Style == 1)   // 종이: 스탠드 불빛처럼 따뜻한 번짐
			{
				Glow1(g, W * 0.88f, -H * 0.06f, W * 0.66f, H * 0.72f, Color.FromArgb((int)(34 * dk), 255, 196, 110));
				Glow1(g, W * 0.10f, H * 1.05f, W * 0.55f, H * 0.55f, Color.FromArgb((int)(22 * dk), 226, 150, 120));
				Glow1(g, W * 0.50f, H * 0.50f, W * 0.60f, H * 0.60f, Color.FromArgb((int)(10 * dk), Theme.Accent));
			}
			else if (Theme.Style == 2)   // 젤리: 사탕빛 구름 세 덩이
			{
				Glow1(g, W * 0.08f, H * 0.02f, W * 0.50f, H * 0.52f, Color.FromArgb((int)(50 * dk), 255, 168, 204));
				Glow1(g, W * 0.95f, H * 0.45f, W * 0.46f, H * 0.50f, Color.FromArgb((int)(46 * dk), 150, 228, 208));
				Glow1(g, W * 0.40f, H * 1.04f, W * 0.55f, H * 0.46f, Color.FromArgb((int)(42 * dk), 255, 226, 140));
			}
			else if (Theme.Style == 3)   // 유리: 오로라처럼 번지는 파스텔
			{
				Glow1(g, W * 0.92f, -H * 0.02f, W * 0.66f, H * 0.62f, Color.FromArgb((int)(70 * dk), 186, 168, 255));
				Glow1(g, W * 0.02f, H * 0.40f, W * 0.52f, H * 0.52f, Color.FromArgb((int)(60 * dk), 136, 200, 255));
				Glow1(g, W * 0.84f, H * 1.00f, W * 0.62f, H * 0.52f, Color.FromArgb((int)(54 * dk), 148, 236, 214));
				Glow1(g, W * 0.30f, H * 1.02f, W * 0.40f, H * 0.34f, Color.FromArgb((int)(36 * dk), 255, 190, 220));
			}
			else
			{
				Glow1(g, fs.Width * 0.86f, -fs.Height * 0.04f, fs.Width * 0.62f, fs.Height * 0.70f, Color.FromArgb(a1, Theme.Accent));
				Glow1(g, fs.Width * 0.12f, fs.Height * 1.04f, fs.Width * 0.50f, fs.Height * 0.52f, Color.FromArgb(a2, Theme.Dark ? Color.FromArgb(214, 130, 96) : Color.FromArgb(255, 178, 130)));
			}
		}
		catch { }
		g.Restore(s);
	}
	private static void Glow1(Graphics g, float cx, float cy, float rx, float ry, Color c)
	{
		using (GraphicsPath gp = new GraphicsPath())
		{
			gp.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
			using (PathGradientBrush pb = new PathGradientBrush(gp))
			{
				pb.CenterColor = c; pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
				g.FillPath(pb, gp);
			}
		}
	}
}

// 페이지·상단·하단: 바탕색 + 은은한 번짐
internal class WashPanel : Panel, IBackHost
{
	protected override void WndProc(ref Message m) { if (PaintProf.On && m.Msg == 0x0F) { long t0 = Stopwatch.GetTimestamp(); base.WndProc(ref m); PaintProf.Add(GetType().Name, t0); } else base.WndProc(ref m); }
	public WashPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); BackColor = Theme.Page; }
	public Color BaseColor { get { return BackColor; } }
	protected virtual void DrawBack(Graphics g) { g.Clear(BackColor); Wash.Glow(g, this); }
	// 번짐 배경은 크기가 창 전체라 그리는 데 오래 걸려요. 한 번만 그려 두고, 글자·버튼은 필요한 조각만 복사해요
	// 같은 크기·같은 자리·같은 테마의 쪽들은 배경 그림이 똑같으므로 하나를 같이 써요 (쪽을 옮길 때마다 다시 그리지 않아요)
	private static readonly Dictionary<string, Bitmap> washShared = new Dictionary<string, Bitmap>();
	private Bitmap WashCache()
	{
		Form f = FindForm(); Size fs = f != null ? f.ClientSize : Size.Empty; Point o = Wash.Off(this);
		string k = Width + "x" + Height + "|" + fs.Width + "x" + fs.Height + "|" + o.X + "," + o.Y + "|" + Theme.Dark + "|" + Theme.Accent.ToArgb() + "|" + BackColor.ToArgb() + "|" + Theme.Bg.ToArgb() + "|" + Theme.Page.ToArgb() + "|" + Theme.Style + "|" + GetType().Name;
		Bitmap hit;
		if (washShared.TryGetValue(k, out hit)) return hit;
		if (Width <= 0 || Height <= 0) return null;
		if (washShared.Count >= 8) { foreach (Bitmap old in washShared.Values) old.Dispose(); washShared.Clear(); }   // 테마·크기가 바뀌어 안 쓰게 된 그림 정리
		Bitmap b = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		// 번짐은 부드러운 그라데이션이라 1/4 크기로 그려서 늘려도 똑같아 보여요 (그리는 시간이 훨씬 줄어요)
		const float Sc = 0.25f;
		int sw = Math.Max(1, (int)Math.Ceiling(Width * Sc)), sh = Math.Max(1, (int)Math.Ceiling(Height * Sc));
		using (Bitmap small = new Bitmap(sw, sh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
		{
			using (Graphics sg = Graphics.FromImage(small)) { Gfx.HQ(sg); sg.ScaleTransform(sw / (float)Width, sh / (float)Height); DrawBack(sg); }
			using (Graphics g = Graphics.FromImage(b))
			using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
			{
				ia.SetWrapMode(WrapMode.TileFlipXY);
				g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
				g.DrawImage(small, new Rectangle(0, 0, Width, Height), 0, 0, sw, sh, GraphicsUnit.Pixel, ia);
			}
		}
		if (Theme.Style == 1) Gfx.Grain(b);
		else if (Theme.Style == 3) Gfx.Sheen(b);
		washShared[k] = b; return b;
	}
	private static void Blit(Graphics g, Bitmap src, Rectangle dst, int sx, int sy)
	{
		InterpolationMode im = g.InterpolationMode; PixelOffsetMode po = g.PixelOffsetMode;
		g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.None;
		g.DrawImage(src, dst, sx, sy, dst.Width, dst.Height, GraphicsUnit.Pixel);
		g.InterpolationMode = im; g.PixelOffsetMode = po;
	}
	public void PaintBack(Graphics g, Control child)
	{
		Bitmap c = null; try { c = WashCache(); } catch { }
		if (c == null || child.Width <= 0 || child.Height <= 0) { GraphicsState s = g.Save(); g.TranslateTransform(-child.Left, -child.Top); DrawBack(g); g.Restore(s); return; }
		Blit(g, c, new Rectangle(0, 0, child.Width, child.Height), child.Left, child.Top);
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Bitmap c = null; try { c = WashCache(); } catch { }
		if (c == null) DrawBack(e.Graphics); else Blit(e.Graphics, c, e.ClipRectangle, e.ClipRectangle.X, e.ClipRectangle.Y);
	}
}

internal class Ctl : Control
{
	protected override void WndProc(ref Message m) { if (PaintProf.On && m.Msg == 0x0F) { long t0 = Stopwatch.GetTimestamp(); base.WndProc(ref m); PaintProf.Add(GetType().Name, t0); } else base.WndProc(ref m); }
	protected void Back(Graphics g)
	{
		IBackHost h = Parent as IBackHost;
		if (h != null && BackColor.ToArgb() == h.BaseColor.ToArgb()) h.PaintBack(g, this); else g.Clear(BackColor);
	}
	public Ctl()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, false);
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
		BackColor = Theme.Bg;
	}
}

internal class TLabel : Ctl
{
	public StringAlignment H = StringAlignment.Near;
	public StringAlignment V = StringAlignment.Near;
	public bool Wrap;
	private Color fore = Theme.Ink;
	private Font tf;

	public TLabel(string text, float pt, int w, Color fore, Color back)
	{
		Text = text; this.fore = fore; BackColor = back; tf = Theme.F(pt, w);
	}
	public Color Fore { get { return fore; } set { fore = value; Invalidate(); } }
	public void SetFont(float pt, int w) { tf = Theme.F(pt, w); Invalidate(); }
	public void SetFontObj(Font f) { tf = f; Invalidate(); }
	protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		Gfx.Text(g, Text, tf, fore, new RectangleF(0, 0, Width, Height), H, V, Wrap);
	}
}

internal enum BK { Primary, Outline, Ghost, Danger, Soft }

internal class TButton : Ctl
{
	public BK Kind = BK.Outline;
	public float Pt = 10f;
	public int Weight = 1;
	public float Rad = 12;
	private AV hv, pr;
	private float shim = -1f; private bool shimOn; private Tw shimTw;
	// 눈길 끌기: 버튼 위로 빛이 주기적으로 스윽 지나감
	public bool Shimmer
	{
		get { return shimOn; }
		set { if (shimOn == value) return; shimOn = value; if (value) ShimLoop(); else { if (shimTw != null) shimTw.Cancel(); shim = -1f; Invalidate(); } }
	}
	private void ShimLoop()
	{
		if (!shimOn || IsDisposed) return;
		shimTw = Anim.Run(1500, Ease.InOutCubic, delegate(float t) { shim = t; Invalidate(); }, delegate
		{
			shim = -1f; Invalidate();
			if (shimOn && !IsDisposed) shimTw = Anim.Run(1300, Ease.OutCubic, delegate(float t) { }, ShimLoop);
		});
	}

	public TButton(string text, BK kind) { Text = text; Kind = kind; Cursor = Cursors.Hand; hv = new AV(this, 0) { Rate = 18f }; pr = new AV(this, 0) { Rate = 34f }; }

	protected override void OnMouseEnter(EventArgs e) { hv.Set(1); base.OnMouseEnter(e); }
	protected override void OnMouseLeave(EventArgs e) { hv.Set(0); pr.Set(0); base.OnMouseLeave(e); }
	protected override void OnMouseDown(MouseEventArgs e) { pr.Set(1); base.OnMouseDown(e); }
	protected override void OnMouseUp(MouseEventArgs e) { pr.Set(0); base.OnMouseUp(e); }
	protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
	protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		float rad = Math.Min(Height / 2f - 1f, Dpi.Zf(Rad) * 1.8f);
		if (Theme.Style == 1) rad = Math.Min(rad, Dpi.Zf(10));   // 종이: 모서리가 조금 각진 스티커
		if (Theme.Style == 2 && Enabled) r = new RectangleF(r.X + pr.V * 1.6f, r.Y + pr.V * 2.2f, r.Width - pr.V * 3.2f, r.Height - pr.V * 3.4f);   // 젤리: 누르면 말랑하게 눌려요
		if (Theme.Style == 3 && Enabled && Kind == BK.Primary) { Gfx.Fill(g, new RectangleF(r.X - 1.5f, r.Y + 1f, r.Width + 3f, r.Height + 3f), rad + 2f, Color.FromArgb(46, Theme.Accent)); Gfx.Fill(g, new RectangleF(r.X - 0.5f, r.Y + 0.5f, r.Width + 1f, r.Height + 1.5f), rad + 1f, Color.FromArgb(40, Theme.Accent)); }
		Color txt = Theme.Ink;
		if (!Enabled)
		{
			Gfx.Fill(g, r, rad, Theme.Side); Gfx.Border(g, r, rad, Theme.Line, 1f); txt = Theme.Muted;
		}
		else if (Kind == BK.Primary)
		{
			Color pc = Theme.Mix(Theme.Mix(Theme.AccentBtn, Theme.AccentHover, hv.V), Theme.AccentDown, pr.V);
			Gfx.Fill(g, r, rad, pc);
			txt = Color.White;
		}
		else if (Kind == BK.Ghost)
		{
			if (hv.V > 0.01f) Gfx.Fill(g, r, rad, Theme.Mix(Theme.Mix(BackColor, Theme.Hover, hv.V), Theme.LineStrong, pr.V));
			txt = Theme.Sub;
		}
		else if (Kind == BK.Soft)
		{
			Gfx.Fill(g, r, rad, Theme.Mix(Theme.Mix(Theme.Side, Theme.Hover, hv.V), Theme.LineStrong, pr.V * 0.6));
			txt = Theme.Mix(Theme.Sub, Theme.Ink, hv.V);
		}
		else
		{
			Color oc = Theme.Mix(Theme.Mix(Theme.Bg, Theme.Side, hv.V), Theme.Hover, pr.V);
			Gfx.Fill(g, r, rad, oc);
			Gfx.Border(g, r, rad, Kind == BK.Danger ? Theme.DangerLine : Theme.Mix(Theme.Line, Theme.LineStrong, 0.55), 1f);
			txt = Kind == BK.Danger ? Theme.Red : Theme.Ink;
		}
		if (Enabled && Theme.Style != 0)
		{
			bool pri = Kind == BK.Primary;
			if (Theme.Style == 1)   // 종이: 도장처럼 한 겹 더 진한 테두리 + 아래쪽 눌림 선
			{
				Gfx.Border(g, r, rad, pri ? Theme.Mix(Theme.AccentDown, Color.Black, 0.12) : Theme.LineStrong, 1.4f);
				if (pri) using (Pen lp = new Pen(Color.FromArgb(60, 0, 0, 0), 2f)) g.DrawLine(lp, r.X + rad * 0.6f, r.Bottom - 2f, r.Right - rad * 0.6f, r.Bottom - 2f);
			}
			else if (Theme.Style == 2)   // 젤리: 윗면 반짝임
			{
				GraphicsState gs2 = g.Save();
				using (GraphicsPath cp = Gfx.Round(r, rad)) g.SetClip(cp);
				using (LinearGradientBrush gl = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, Math.Max(4f, r.Height * 0.55f)), Color.FromArgb(pri ? 96 : 70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(gl, r.X, r.Y, r.Width, Math.Max(4f, r.Height * 0.55f));
				using (LinearGradientBrush gb = new LinearGradientBrush(new RectangleF(r.X, r.Bottom - r.Height * 0.4f, r.Width, r.Height * 0.4f + 1f), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(pri ? 36 : 12, 0, 0, 0), 90f)) g.FillRectangle(gb, r.X, r.Bottom - r.Height * 0.4f, r.Width, r.Height * 0.4f + 1f);
				g.Restore(gs2);
				using (GraphicsPath hp = Gfx.Round(new RectangleF(r.X + rad * 0.8f, r.Y + 2.5f, r.Width - rad * 1.6f, 3f), 1.5f)) using (SolidBrush hb = new SolidBrush(Color.FromArgb(pri ? 150 : 110, 255, 255, 255))) g.FillPath(hb, hp);
			}
			else   // 유리: 맑은 흰 테두리와 윗면 빛
			{
				using (GraphicsPath rp = Gfx.Round(r, rad)) using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(pri ? 230 : 200, 255, 255, 255), Color.FromArgb(40, 255, 255, 255), 70f)) using (Pen bp = new Pen(lb, 1.4f)) g.DrawPath(bp, rp);
				GraphicsState gs3 = g.Save();
				using (GraphicsPath cp = Gfx.Round(r, rad)) g.SetClip(cp);
				using (LinearGradientBrush gl = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, Math.Max(4f, r.Height * 0.5f)), Color.FromArgb(pri ? 64 : 80, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(gl, r.X, r.Y, r.Width, Math.Max(4f, r.Height * 0.5f));
				g.Restore(gs3);
			}
		}
		Gfx.Text(g, Text, Theme.F(Pt, Weight), txt, new RectangleF(Dpi.Zf(8), 0, Width - Dpi.Zf(16), Height - 1), StringAlignment.Center, StringAlignment.Center, false);
		if (shim >= 0f && Enabled)
		{
			GraphicsState gs = g.Save();
			using (GraphicsPath cp = Gfx.Round(r, rad)) g.SetClip(cp);
			float bw = Width * 0.42f, bx = -bw + (Width + bw * 2) * shim;
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(bx, 0, bw, Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
			{
				ColorBlend cb = new ColorBlend(); cb.Colors = new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(95, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) }; cb.Positions = new float[] { 0f, 0.5f, 1f }; lb.InterpolationColors = cb;
				g.FillRectangle(lb, bx, 0, bw, Height);
			}
			g.Restore(gs);
		}
	}
}

// Windows 11 스타일 토글 스위치 (눌렀을 때 동그라미가 늘어나고, 살짝 튕기며 이동)
internal class TToggle : Ctl
{
	private bool on;
	private float pos;
	private AV press;
	private Tw tw;
	public event EventHandler CheckedChanged;

	public TToggle()
	{
		Cursor = Cursors.Hand;
		Size = new Size(Dpi.Z(42), Dpi.Z(24));
		press = new AV(this, 0) { Rate = 30f };
	}
	public bool Checked
	{
		get { return on; }
		set
		{
			if (on == value) return; on = value;
			float from = pos, to = on ? 1f : 0f;
			if (tw != null) tw.Cancel();
			tw = Anim.Run(280, Ease.OutBack, delegate(float t) { pos = from + (to - from) * t; Invalidate(); }, null);
			if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
		}
	}
	public void SetImmediate(bool v) { on = v; pos = v ? 1f : 0f; if (tw != null) tw.Cancel(); Invalidate(); }
	protected override void OnMouseDown(MouseEventArgs e) { press.Set(1); base.OnMouseDown(e); }
	protected override void OnMouseUp(MouseEventArgs e) { press.Set(0); base.OnMouseUp(e); }
	protected override void OnMouseLeave(EventArgs e) { press.Set(0); base.OnMouseLeave(e); }
	protected override void OnClick(EventArgs e) { if (Enabled) Checked = !Checked; base.OnClick(e); }
	protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
		float pc = Math.Max(0f, Math.Min(1f, pos));
		Color off = Theme.ToggleOff;
		Color track = Theme.Mix(off, Theme.Accent, pc);
		if (!Enabled) track = Theme.Mix(track, Theme.Bg, 0.55);
		using (GraphicsPath p = Gfx.Round(r, r.Height / 2)) using (SolidBrush b = new SolidBrush(track)) g.FillPath(b, p);
		float d = r.Height - Dpi.Zf(7), w = d + press.V * Dpi.Zf(3.5f);
		float x = r.X + Dpi.Zf(3.5f) + (r.Width - d - Dpi.Zf(7)) * pos - (pos > 0.5f ? press.V * Dpi.Zf(3.5f) : 0);
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(30, 0, 0, 0))) g.FillEllipse(sh, x, r.Y + Dpi.Zf(3.5f) + 0.8f, w, d);
		using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, x, r.Y + Dpi.Zf(3.5f), w, d);
	}
}

// 시간 선택 칩 (켜짐/꺼짐)
internal class TChip : Ctl
{
	private bool on;
	private AV hv, onv;
	public event EventHandler CheckedChanged;
	public TChip(string text) { Text = text; Cursor = Cursors.Hand; hv = new AV(this, 0) { Rate = 18f }; onv = new AV(this, 0) { Rate = 20f }; }
	public bool Checked
	{
		get { return on; }
		set { if (on == value) return; on = value; onv.Set(on ? 1 : 0); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
	}
	public void SetImmediate(bool v) { on = v; onv.Snap(v ? 1 : 0); }
	protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
	protected override void OnMouseEnter(EventArgs e) { hv.Set(1); base.OnMouseEnter(e); }
	protected override void OnMouseLeave(EventArgs e) { hv.Set(0); base.OnMouseLeave(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		float rad = Math.Min(Height / 2f - 1f, Dpi.Zf(12)), k = onv.V;
		Color fill = Theme.Mix(Theme.Mix(Theme.Bg, Theme.Side, hv.V), Theme.AccentSoft, k);
		Color bord = Theme.Mix(Theme.Line, Theme.Mix(Theme.AccentSoft, Theme.Accent, 0.55), k);
		Gfx.Fill(g, r, rad, fill); Gfx.Border(g, r, rad, bord, 1f);
		Gfx.Text(g, Text, Theme.F(10f, k > 0.5f ? 1 : 0), Theme.Mix(Theme.Sub, Theme.Accent, k), new RectangleF(0, 0, Width, Height - 1), StringAlignment.Center, StringAlignment.Center, false);
	}
}

// 위쪽 탭 (조각 버튼): 선택 표시가 미끄러지듯 움직임
internal class TSeg : Ctl
{
	private string[] items; private int idx; private AV pos, hv; private int hot = -1; private float[] wid;
	public event EventHandler Changed;
	public TSeg(string[] names) : this(names, 26) { }
	public TSeg(string[] names, int pad)
	{
		items = names; Cursor = Cursors.Hand; wid = new float[names.Length];
		using (Graphics g = CreateGraphics()) { Gfx.HQ(g); float tot = 0; for (int i = 0; i < names.Length; i++) { wid[i] = Gfx.Measure(g, names[i], Theme.F(10f, 1)) + Dpi.Zf(pad); tot += wid[i]; } Size = new Size((int)Math.Ceiling(tot) + Dpi.Z(8), Dpi.Z(34)); }
		pos = new AV(this, 0) { Rate = 16f }; hv = new AV(this, 0) { Rate = 18f };
	}
	public int Index { get { return idx; } }
	public void SnapIndex(int i) { idx = i; pos.Snap(i); }
	public void Slide(int i) { idx = i; pos.Set(i); }
	private float X(int i) { float x = Dpi.Zf(4); for (int k = 0; k < i; k++) x += wid[k]; return x; }
	private int At(int px) { for (int i = 0; i < items.Length; i++) if (px >= X(i) && px < X(i) + wid[i]) return i; return -1; }
	protected override void OnMouseMove(MouseEventArgs e) { int h = At(e.X); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
	protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseClick(MouseEventArgs e) { int i = At(e.X); if (i >= 0 && i != idx) { idx = i; pos.Set(i); if (Changed != null) Changed(this, EventArgs.Empty); } base.OnMouseClick(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		float sr = r.Height / 2f; Gfx.Fill(g, r, sr, Theme.Side); Gfx.Border(g, r, sr, Theme.Line, 1f);
		// 움직이는 흰 판 (두 칸 사이는 폭도 같이 바뀜)
		float p = Math.Max(0, Math.Min(items.Length - 1, pos.V)); int a = (int)Math.Floor(p); int b = Math.Min(items.Length - 1, a + 1); float f = p - a;
		float tx = X(a) + (X(b) - X(a)) * f, tw = wid[a] + (wid[b] - wid[a]) * f;
		RectangleF th = new RectangleF(tx, Dpi.Zf(4), tw, Height - Dpi.Zf(8));
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(22, 0, 0, 0))) Gfx.Fill(g, new RectangleF(th.X, th.Y + 1, th.Width, th.Height), th.Height / 2f, Color.FromArgb(22, 0, 0, 0));
		Gfx.Fill(g, th, th.Height / 2f, Theme.Raised); Gfx.Border(g, th, th.Height / 2f, Theme.Line, 1f);
		for (int i = 0; i < items.Length; i++)
		{
			float near = 1 - Math.Min(1, Math.Abs(pos.V - i));
			Color c = Theme.Mix(i == hot ? Theme.Ink : Theme.Sub, Theme.Ink, near);
			Gfx.Text(g, items[i], Theme.F(10f, near > 0.5f ? 1 : 0), c, new RectangleF(X(i), 0, wid[i], Height - 1), StringAlignment.Center, StringAlignment.Center, false);
		}
	}
}

// 지울 수 있는 태그 (예: "매시 30분  ×")
internal class TTag : Ctl
{
	private bool hov, hovX;
	private AV xv;
	public event EventHandler Remove;
	public TTag(string text)
	{
		Text = text; Cursor = Cursors.Hand; xv = new AV(this, 0) { Rate = 22f };
		using (Graphics g = CreateGraphics()) { Gfx.HQ(g); Size = new Size((int)Gfx.Measure(g, text, Theme.F(10f, 1)) + Dpi.Z(46), Dpi.Z(32)); }
	}
	private RectangleF XRect { get { return new RectangleF(Width - Dpi.Zf(28), 0, Dpi.Zf(26), Height); } }
	protected override void OnMouseMove(MouseEventArgs e) { bool x = XRect.Contains(e.Location); if (x != hovX || !hov) { hovX = x; hov = true; xv.Set(x ? 1 : 0); } base.OnMouseMove(e); }
	protected override void OnMouseLeave(EventArgs e) { hov = false; hovX = false; xv.Set(0); base.OnMouseLeave(e); }
	protected override void OnMouseClick(MouseEventArgs e) { if (XRect.Contains(e.Location) && Remove != null) Remove(this, EventArgs.Empty); base.OnMouseClick(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		Gfx.Fill(g, r, Dpi.Zf(12), Theme.AccentSoft);
		Gfx.Border(g, r, Dpi.Zf(12), Theme.Mix(Theme.AccentSoft, Theme.Accent, 0.4), 1f);
		Gfx.Text(g, Text, Theme.F(10f, 1), Theme.Accent, new RectangleF(Dpi.Zf(12), 0, Width - Dpi.Zf(36), Height - 1), StringAlignment.Near, StringAlignment.Center, false);
		RectangleF x = XRect; float cx = x.X + x.Width / 2, cy = Height / 2f - 0.5f, s = Dpi.Zf(3.2f);
		if (xv.V > 0.01f) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(44 * xv.V), Theme.Accent))) g.FillEllipse(b, cx - Dpi.Zf(9), cy - Dpi.Zf(9), Dpi.Zf(18), Dpi.Zf(18));
		using (Pen p = new Pen(Theme.Accent, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		{
			g.DrawLine(p, cx - s, cy - s, cx + s, cy + s); g.DrawLine(p, cx - s, cy + s, cx + s, cy - s);
		}
	}
}

// 숫자 올리고 내리기 [ − 30 + ]
internal class TStepper : Ctl
{
	public int Min = 0, Max = 59;
	private int val = 30;
	private int hot = 0;   // 1 minus · 2 plus
	private AV h1, h2;
	public string Suffix = "분";
	public event EventHandler ValueChanged;
	public TStepper() { Size = new Size(Dpi.Z(132), Dpi.Z(34)); Cursor = Cursors.Hand; h1 = new AV(this, 0) { Rate = 22f }; h2 = new AV(this, 0) { Rate = 22f }; }
	public int Value
	{
		get { return val; }
		set { int v = Math.Max(Min, Math.Min(Max, value)); if (v == val) return; val = v; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
	}
	private int Part(Point p) { int b = Dpi.Z(34); return p.X < b ? 1 : (p.X > Width - b ? 2 : 0); }
	protected override void OnMouseMove(MouseEventArgs e) { int h = Part(e.Location); if (h != hot) { hot = h; h1.Set(h == 1 ? 1 : 0); h2.Set(h == 2 ? 1 : 0); } base.OnMouseMove(e); }
	protected override void OnMouseLeave(EventArgs e) { hot = 0; h1.Set(0); h2.Set(0); base.OnMouseLeave(e); }
	private TextBox edit;
	public void Commit() { if (edit != null) { int v; if (int.TryParse(edit.Text.Trim(), out v)) Value = v; try { Controls.Remove(edit); edit.Dispose(); } catch { } edit = null; Invalidate(); } }
	protected override void OnMouseClick(MouseEventArgs e) { int h = Part(e.Location); if (h == 1) Value = val - 1; else if (h == 2) Value = val + 1; else BeginEdit(); base.OnMouseClick(e); }
	protected override void OnMouseWheel(MouseEventArgs e) { Value = val + (e.Delta > 0 ? 1 : -1); base.OnMouseWheel(e); }
	// 가운데 숫자를 누르면 직접 입력할 수 있어요 (Enter 또는 다른 곳을 누르면 적용)
	private void BeginEdit()
	{
		if (edit != null) return;
		int b = Dpi.Z(34);
		TextBox t = new TextBox(); edit = t;
		t.BorderStyle = BorderStyle.None; t.Font = Fonts.Native(10.5f); t.ForeColor = Theme.Ink; t.BackColor = Theme.Bg; t.TextAlign = HorizontalAlignment.Center; t.MaxLength = 4;
		t.Text = val.ToString(); t.Width = Width - b * 2 - Dpi.Z(6); t.Left = b + Dpi.Z(3); t.Top = Math.Max(0, (Height - t.Height) / 2);
		bool done = false;
		Action<bool> finish = delegate(bool ok)
		{
			if (done) return; done = true;
			int v; if (ok && int.TryParse(t.Text.Trim(), out v)) Value = v;
			edit = null; Controls.Remove(t); t.Dispose(); Invalidate();
		};
		t.KeyPress += delegate(object s, KeyPressEventArgs ke) { if (!char.IsControl(ke.KeyChar) && !char.IsDigit(ke.KeyChar)) ke.Handled = true; };
		t.KeyDown += delegate(object s, KeyEventArgs ke) { if (ke.KeyCode == Keys.Enter) { ke.SuppressKeyPress = true; finish(true); } else if (ke.KeyCode == Keys.Escape) { ke.SuppressKeyPress = true; finish(false); } };
		t.LostFocus += delegate { finish(true); };
		Controls.Add(t); t.Focus(); t.SelectAll(); Invalidate();
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Wash.Under(this, g);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		Gfx.Fill(g, r, Dpi.Zf(15), Theme.Bg); Gfx.Border(g, r, Dpi.Zf(15), Theme.Mix(Theme.LineStrong, Theme.Bg, 0.3), 1f);
		float b = Dpi.Zf(34);
		if (h1.V > 0.01f) Gfx.Fill(g, new RectangleF(2, 2, b - 2, Height - 5), Dpi.Zf(6), Theme.Mix(Theme.Bg, Theme.Hover, h1.V));
		if (h2.V > 0.01f) Gfx.Fill(g, new RectangleF(Width - b, 2, b - 2, Height - 5), Dpi.Zf(6), Theme.Mix(Theme.Bg, Theme.Hover, h2.V));
		using (Pen p = new Pen(Theme.Sub, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		{
			float cy = Height / 2f - 0.5f, s = Dpi.Zf(4);
			g.DrawLine(p, b / 2 - s, cy, b / 2 + s, cy);
			float cx = Width - b / 2;
			g.DrawLine(p, cx - s, cy, cx + s, cy); g.DrawLine(p, cx, cy - s, cx, cy + s);
		}
		if (edit == null) Gfx.Text(g, val + Suffix, Theme.F(10.5f, 1), Theme.Ink, new RectangleF(b, 0, Width - b * 2, Height - 1), StringAlignment.Center, StringAlignment.Center, false);
	}
}

// 둥근 테두리 입력칸 (안에 TextBox)
internal class TInput : Panel
{
	public TextBox Box;
	private bool focus;
	public TInput()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
		BackColor = Theme.Bg;
		Box = new TextBox();
		Box.BorderStyle = BorderStyle.None; Box.Font = Fonts.Native(10f); Box.ForeColor = Theme.Ink; Box.BackColor = Theme.Bg;
		Box.Enter += delegate { focus = true; Invalidate(); };
		Box.Leave += delegate { focus = false; Invalidate(); };
		Controls.Add(Box);
	}
	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		if (Box == null) return;
		Box.Width = Width - Dpi.Z(24);
		Box.Left = Dpi.Z(12); Box.Top = Math.Max(0, (Height - Box.Height) / 2);
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Wash.Under(this, g);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		Gfx.Fill(g, r, Dpi.Zf(12), Theme.Bg);
		Gfx.Border(g, r, Dpi.Zf(15), focus ? Theme.Accent : Theme.Mix(Theme.LineStrong, Theme.Bg, 0.3), focus ? 1.6f : 1f);
	}
}

internal class Card : Panel
{
	public string Ico;
	private void DrawIco(Graphics g)
	{
		Bitmap ib = Ico == null ? null : Spr.Get(Ico); if (ib == null) return;
		float h = Dpi.Zf(23), w = h * ib.Width / ib.Height; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
		g.DrawImage(ib, new RectangleF(Dpi.Zf(14), Dpi.Zf(8), w, h));
	}
	protected override void OnPaint(PaintEventArgs e) { PaintCard(e); DrawIco(e.Graphics); }
	public Card()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
		BackColor = Theme.Bg;
	}
	private void PaintCard(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Wash.Under(this, g);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		// 살짝 떠 있는 느낌: 아래쪽에 1px 그림자, 위쪽엔 빛이 닿은 테두리
		if (Theme.Style == 1)   // 종이: 살짝 어긋난 그림자 + 점선 스티치
		{
			Gfx.Fill(g, new RectangleF(r.X + 2f, r.Y + 3f, r.Width - 2f, r.Height - 2f), Dpi.Zf(14), Color.FromArgb(Theme.Dark ? 70 : 26, 90, 60, 20));
			Gfx.Fill(g, r, Dpi.Zf(14), BackColor); Gfx.Border(g, r, Dpi.Zf(14), Theme.LineStrong, 1.2f);
			using (GraphicsPath sp = Gfx.Round(new RectangleF(r.X + Dpi.Zf(5), r.Y + Dpi.Zf(5), r.Width - Dpi.Zf(10), r.Height - Dpi.Zf(10)), Dpi.Zf(10))) using (Pen dp = new Pen(Color.FromArgb(Theme.Dark ? 70 : 90, Theme.LineStrong), 1f)) { dp.DashStyle = DashStyle.Dash; dp.DashPattern = new float[] { 3f, 3f }; g.DrawPath(dp, sp); }
			return;
		}
		if (Theme.Style == 2)   // 젤리: 아주 둥글고 몽글한 그림자 + 윗면 반짝임
		{
			Gfx.Fill(g, new RectangleF(r.X + 1f, r.Y + 4f, r.Width - 2f, r.Height - 3f), Dpi.Zf(30), Color.FromArgb(Theme.Dark ? 60 : 16, 200, 100, 150));
			Gfx.Fill(g, new RectangleF(r.X, r.Y + 2f, r.Width, r.Height - 1f), Dpi.Zf(30), Color.FromArgb(Theme.Dark ? 50 : 14, 200, 100, 150));
			Gfx.Fill(g, r, Dpi.Zf(28), BackColor); Gfx.Border(g, r, Dpi.Zf(28), Theme.Mix(Theme.Line, Color.White, Theme.Dark ? 0.0 : 0.4), 1.4f);
			using (GraphicsPath hp = Gfx.Round(new RectangleF(r.X + Dpi.Zf(14), r.Y + 2.5f, r.Width - Dpi.Zf(28), 3f), 1.5f)) using (SolidBrush hb = new SolidBrush(Color.FromArgb(Theme.Dark ? 40 : 200, 255, 255, 255))) g.FillPath(hb, hp);
			return;
		}
		if (Theme.Style == 3)   // 유리: 맑은 흰 테두리 + 번지는 은은한 빛
		{
			Gfx.Fill(g, new RectangleF(r.X - 1f, r.Y + 2f, r.Width + 2f, r.Height + 1f), Dpi.Zf(24), Color.FromArgb(Theme.Dark ? 40 : 18, Theme.Accent));
			Gfx.Fill(g, r, Dpi.Zf(22), BackColor);
			using (GraphicsPath rp = Gfx.Round(r, Dpi.Zf(22))) using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), Color.FromArgb(Theme.Dark ? 70 : 255, 255, 255, 255), Color.FromArgb(Theme.Dark ? 20 : 120, Theme.Accent), 60f)) using (Pen bp = new Pen(lb, 1.6f)) g.DrawPath(bp, rp);
			return;
		}
		Gfx.Fill(g, new RectangleF(r.X, r.Y + 1f, r.Width, r.Height), Dpi.Zf(16), Color.FromArgb(Theme.Dark ? 50 : 9, 10, 30, 30));
		Gfx.Fill(g, r, Dpi.Zf(20), BackColor); Gfx.Border(g, r, Dpi.Zf(20), Theme.Mix(Theme.Line, BackColor, 0.35), 1f);
		using (GraphicsPath rp = Gfx.Round(new RectangleF(r.X + 1f, r.Y + 1f, r.Width - 2f, r.Height - 2f), Dpi.Zf(19)))
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, 0, Width, Math.Max(8f, Height * 0.5f)), Color.FromArgb(Theme.Dark ? 22 : 150, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
		using (Pen hp = new Pen(lb, 1f)) g.DrawPath(hp, rp);
	}
}

// 닫기 버튼 (✕ 를 직접 그림)
internal class CloseBtn : Ctl
{
	private AV hv;
	public Color Ink = Color.White;
	public CloseBtn() { hv = new AV(this, 0) { Rate = 20f }; Cursor = Cursors.Hand; Size = new Size(Dpi.Z(30), Dpi.Z(30)); SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
	protected override void OnMouseEnter(EventArgs e) { hv.Set(1); base.OnMouseEnter(e); }
	protected override void OnMouseLeave(EventArgs e) { hv.Set(0); base.OnMouseLeave(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g);
		float cx = Width / 2f, cy = Height / 2f, s = Dpi.Zf(4.5f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(70 * hv.V), 255, 255, 255))) g.FillEllipse(b, cx - Dpi.Zf(13), cy - Dpi.Zf(13), Dpi.Zf(26), Dpi.Zf(26));
		using (Pen p = new Pen(Ink, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		{
			g.DrawLine(p, cx - s, cy - s, cx + s, cy + s); g.DrawLine(p, cx - s, cy + s, cx + s, cy - s);
		}
	}
}

// ─────────────────────────────────────────────────────────────
//  하늘 배너 — 시간대별 하늘색 + 별 + 달 + 마룡이
// ─────────────────────────────────────────────────────────────
internal class SkyBanner : Ctl
{
	public Image Mascot;
	public string Caption = "", TimeText = "", DateText = "";
	public float TimePt = 30f;
	public float Radius = 0f;
	public bool MascotRight = false;
	public bool Hop = true;
	public bool ShowPagoda = true;                 // 처음 등장할 때 통통 튀기
	public float MoonX = 0.7f;
	private Stopwatch clock = Stopwatch.StartNew();
	private Bitmap mscCache; private int mscH; private Image mscSrc;
	private static PointF[] stars;

	public SkyBanner()
	{
		if (stars == null)
		{
			Random rnd = new Random(11);
			stars = new PointF[46];
			for (int i = 0; i < stars.Length; i++) stars[i] = new PointF((float)rnd.NextDouble(), (float)rnd.NextDouble());
		}
	}
	public void Restart() { bgDirty = true; clock.Reset(); clock.Start(); lastPulse = -1; tickPending = false; liveStamp = 0; nextAct = 4.5; actKind = -1; }

	// ── 공항 안내판처럼 철컥 넘어가는 시계 ──
	public bool Flap = true;
	private int liveStamp; private string flapPrev = ""; private readonly double[] flapT0 = new double[8]; private readonly char[] flapOld = new char[8];
	private static bool IsClock(string s) { if (string.IsNullOrEmpty(s) || s.Length < 5 || s.Length > 8) return false; foreach (char c in s) if (!(char.IsDigit(c) || c == ':')) return false; return true; }
	// 타일 그림은 글자·크기별로 한 번만 그려 두고, 매 프레임에는 붙여 넣기만 해요 (그래야 매끄럽게 넘어가요)
	private static readonly Dictionary<string, Bitmap> tiles = new Dictionary<string, Bitmap>();
	private static Bitmap TileBmp(char ch, int w, int h)
	{
		string key = ch + "|" + w + "|" + h; Bitmap b;
		if (tiles.TryGetValue(key, out b)) return b;
		if (tiles.Count > 60) { foreach (Bitmap x in tiles.Values) x.Dispose(); tiles.Clear(); }
		b = new Bitmap(Math.Max(2, w), Math.Max(2, h), PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(b))
		{
			Gfx.HQ(g); g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			RectangleF rc = new RectangleF(0.5f, 0.5f, b.Width - 1f, b.Height - 1f);
			float rad = Math.Max(3f, rc.Width * 0.24f);
			using (GraphicsPath p = Gfx.Round(rc, rad))
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(rc.X, rc.Y - 1, rc.Width, rc.Height + 2), Color.FromArgb(30, 78, 100), Color.FromArgb(12, 40, 62), 90f)) g.FillPath(lb, p);
				using (Pen bp = new Pen(Color.FromArgb(60, 255, 255, 255), 1f)) g.DrawPath(bp, p);
			}
			float pt = (float)Math.Round(rc.Height * 0.70f * 72f / g.DpiY, 1);
			Gfx.Text(g, ch.ToString(), Fonts.Num(pt, 2), Color.FromArgb(254, 255, 255, 255), new RectangleF(rc.X - rc.Width * 0.04f, rc.Y - rc.Height * 0.03f, rc.Width * 1.08f, rc.Height), StringAlignment.Center, StringAlignment.Center, false);
			float cy = b.Height / 2f;
			using (Pen lp = new Pen(Color.FromArgb(150, 4, 14, 24), Math.Max(1f, rc.Height * 0.028f))) g.DrawLine(lp, 0, cy, b.Width, cy);
		}
		tiles[key] = b; return b;
	}
	// half: 0 윗반쪽 · 1 아랫반쪽 · 2 전체, sy: 가운데 선을 기준으로 위아래로 납작해지는 정도
	private void FlapTile(Graphics g, RectangleF rc, char ch, int half, float sy)
	{
		int x = (int)Math.Round(rc.X), y = (int)Math.Round(rc.Y), w = (int)Math.Round(rc.Width), h = (int)Math.Round(rc.Height);
		Bitmap b = TileBmp(ch, w, h);
		if (half == 2) { g.DrawImageUnscaled(b, x, y); return; }
		float hh = b.Height / 2f; sy = Math.Max(0.02f, sy); float cy = y + hh;
		RectangleF src = half == 0 ? new RectangleF(0, 0, b.Width, hh) : new RectangleF(0, hh, b.Width, b.Height - hh);
		RectangleF dst = half == 0 ? new RectangleF(x, cy - hh * sy, b.Width, hh * sy) : new RectangleF(x, cy, b.Width, (b.Height - hh) * sy);
		InterpolationMode om = g.InterpolationMode; PixelOffsetMode op = g.PixelOffsetMode;
		g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
		g.DrawImage(b, dst, src, GraphicsUnit.Pixel);
		g.InterpolationMode = om; g.PixelOffsetMode = op;
	}
	private void DrawFlap(Graphics g, RectangleF r, double t)
	{
		string s = TimeText; int n = s.Length; bool anyFlip = false;
		// 시·분은 크게, 초는 작게 (시선이 시·분에 먼저 가도록)
		float[] sc = new float[n]; float h = r.Height * 0.98f, gap = Dpi.Zf(3f);
		float total = 0;
		for (int i = 0; i < n; i++) { sc[i] = (n == 8 && i >= 6) ? 0.68f : 1f; total += (s[i] == ':' ? h * 0.30f : h * 0.70f) * sc[i] + (i > 0 ? gap : 0); }
		float k0 = 1f; if (total > r.Width && total > 0) k0 = r.Width / total;
		h *= k0; gap *= k0;
		float x = r.X;
		for (int i = 0; i < n; i++)
		{
			char c = s[i]; float th = h * sc[i], y = r.Y + (r.Height - th) / 2f + (sc[i] < 1f ? h * 0.08f : 0f);
			if (c == ':')
			{
				float cw = th * 0.30f;
				using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 255, 255))) { float dr = Math.Max(1.6f, th * 0.055f); g.FillEllipse(b, x + cw / 2 - dr, y + th * 0.34f - dr, dr * 2, dr * 2); g.FillEllipse(b, x + cw / 2 - dr, y + th * 0.66f - dr, dr * 2, dr * 2); }
				x += cw + gap; continue;
			}
			float w = th * 0.70f;
			if (i < 8 && flapPrev.Length == n && flapPrev[i] != c) { flapOld[i] = flapPrev[i]; flapT0[i] = t; if (PaintProf.On && i == n - 1) lock (FlipMs) FlipMs.Add(DateTime.Now.Millisecond); }
			float u = i < 8 && flapOld[i] != 0 ? (float)((t - flapT0[i]) / 0.42) : 2f;
			RectangleF tile = new RectangleF(x, y, w, th);
			if (u >= 0f && u < 1f)
			{
				anyFlip = true;
				FlapTile(g, tile, c, 0, 1f); FlapTile(g, tile, flapOld[i], 1, 1f);
				if (u < 0.5f) FlapTile(g, tile, flapOld[i], 0, 1f - u * 2f); else FlapTile(g, tile, c, 1, (u - 0.5f) * 2f);
			}
			else FlapTile(g, tile, c, 2, 1f);
			x += w + gap;
		}
		flapPrev = s; flipping = anyFlip;
	}

	// ── 마룡이 반응 (홈 화면에서만) ──
	public static readonly List<int> FlipMs = new List<int>(); public static int PulsePaints, TickPaints; public bool LiveClock = Environment.GetEnvironmentVariable("OT_LIVECLOCK") != "0";   // LiveClock: 시계 글자를 250ms 타이머가 아니라 매 프레임 실제 시각으로 맞춰요 (초가 바뀌는 순간에 바로 넘어가요)
	public bool Alive; public bool Fixed60 = Environment.GetEnvironmentVariable("OT_FIXED60") != "0"; public bool CalmIdle; private bool calmNow;   // CalmIdle: 가만히 있을 땐 둥실·알아서 하는 동작 없이 거의 그리지 않고, 마우스를 대거나 누를 때만 반응해요
	private int actKind = -1; private double actT0, actDur, nextAct = 4.5; private static readonly Random arnd = new Random();
	private RectangleF mRect; private float hov, hovTarget, mouseX; private DateTime hovLast = DateTime.Now;
	private struct Heart { public float X, Dx, Ph; public double T0; }
	private readonly List<Heart> hearts = new List<Heart>(); private readonly SolidBrush heartBr = new SolidBrush(Color.White); private static GraphicsPath heartP;
	private static GraphicsPath HeartPath() { if (heartP == null) { GraphicsPath p = new GraphicsPath(); p.AddBezier(0, 1, -1.5f, -0.2f, -0.7f, -1.3f, 0, -0.45f); p.AddBezier(0, -0.45f, 0.7f, -1.3f, 1.5f, -0.2f, 0, 1); heartP = p; } return heartP; }
	private void StartAct(int k, double t) { actKind = k; actT0 = t; actDur = k == 0 ? 1.0 : k == 1 ? 1.2 : k == 2 ? 0.9 : k == 3 ? 0.8 : 1.3; }
	public void Poke()
	{
		if (!Alive) return;
		double t = clock.Elapsed.TotalSeconds;
		bool busy = actKind >= 0 && (t - actT0) < actDur * 0.55;   // 연타하면 동작을 끊지 않고 하트만 더해요
		if (!busy) StartAct(arnd.Next(3) == 0 ? 2 : 0, t);
		int add = busy ? 2 : 4;
		for (int i = 0; i < add && hearts.Count < 12; i++) hearts.Add(new Heart { X = (float)(arnd.NextDouble() - 0.5) * mRect.Width * 0.5f, Dx = (float)(arnd.NextDouble() - 0.5) * 30f, Ph = (float)arnd.NextDouble() * 6f, T0 = t + i * 0.07 });
	}
	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (!Alive) return;
		bool on = mRect.Contains(e.X, e.Y); hovTarget = on ? 1f : 0f; mouseX = e.X; Cursor = on ? Cursors.Hand : Cursors.Default;
	}
	protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovTarget = 0f; Cursor = Cursors.Default; }
	private void Act(double t, ref float lift, ref float ang, ref float sx, ref float sy)
	{
		if (actKind < 0 && t >= nextAct && !CalmIdle) StartAct(arnd.Next(5), t);
		if (actKind < 0) return;
		double u = (t - actT0) / actDur;
		if (u >= 1) { actKind = -1; nextAct = t + 5 + arnd.NextDouble() * 6; return; }
		float z = Dpi.Scale;
		switch (actKind)
		{
			case 0: { double a = Math.Abs(Math.Sin(u * Math.PI * 2)); lift += (float)(a * 16 * z); float sq = (float)Math.Pow(1 - a, 6) * 0.12f; sx *= 1 + sq * 0.8f; sy *= 1 - sq; break; }
			case 1: ang += (float)(Math.Sin(u * Math.PI * 2) * 9 * Math.Sin(u * Math.PI)); break;
			case 2: { float c = (float)Math.Cos(u * Math.PI * 2); if (Math.Abs(c) < 0.05f) c = c < 0 ? -0.05f : 0.05f; sx *= c; lift += (float)(Math.Sin(u * Math.PI) * 14 * z); break; }
			case 3: ang += (float)(Math.Sin(u * Math.PI * 8) * 7 * (1 - u)); break;
			default: { float s = (float)Math.Sin(u * Math.PI); sy *= 1 + 0.10f * s; sx *= 1 - 0.05f * s; break; }
		}
	}
	public double PaintMs; public static readonly List<long> Stamps = new List<long>(); public static readonly List<int> StampIds = new List<int>();
	protected override void Dispose(bool disposing) { if (disposing) { if (mscCache != null) { mscCache.Dispose(); mscCache = null; } if (bgCache != null) { bgCache.Dispose(); bgCache = null; } if (clipP != null) { clipP.Dispose(); clipP = null; } } base.Dispose(disposing); }
	public bool Lively { get { return clock.Elapsed.TotalSeconds < 2.8; } }

	// 축하 폭죽
	private class Bit { public float X, Y, Vx, Vy, Rot, Vr, S; public Color C; }
	private List<Bit> bits; private double burstT0;
	public bool Bursting { get { return bits != null || Stamping; } }
	private double stampT0 = -1;
	public void StampStart() { stampT0 = clock.Elapsed.TotalSeconds; Invalidate(); }
	public void StampClear() { stampT0 = -1; }
	public bool Stamping { get { return stampT0 >= 0 && clock.Elapsed.TotalSeconds - stampT0 < 1.5; } }
	private void DrawStamp(Graphics g)
	{
		if (stampT0 < 0) return;
		float t = (float)(clock.Elapsed.TotalSeconds - stampT0), z = Dpi.Scale;
		float ix = Width * 0.80f, iy = Height * 0.66f;   // 도장이 찍히는 자리
		float hit = 0.30f;
		if (t >= hit)
		{
			float ma = Math.Min(1f, (t - hit) / 0.12f) * 0.88f; float sc = t < hit + 0.16f ? 1.12f - 0.12f * ((t - hit) / 0.16f) : 1f;
			GraphicsState ms = g.Save(); g.TranslateTransform(ix, iy); g.RotateTransform(-12f); g.ScaleTransform(sc, sc);
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
			Color ink = Color.FromArgb((int)(255 * ma), 214, 48, 56);
			using (Pen p1 = new Pen(ink, 2.6f * z)) using (Pen p2 = new Pen(Color.FromArgb((int)(150 * ma), 214, 48, 56), 1.2f * z)) { float r = 25f * z; g.DrawEllipse(p1, -r, -r, r * 2, r * 2); g.DrawEllipse(p2, -r + 4f * z, -r + 4f * z, (r - 4f * z) * 2, (r - 4f * z) * 2); }
			using (Font f = new Font("Malgun Gothic", 16f * z, FontStyle.Bold, GraphicsUnit.Pixel)) using (SolidBrush b = new SolidBrush(ink)) { StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }; g.DrawString("확인", f, b, new RectangleF(-24f * z, -13f * z, 48f * z, 26f * z), sf); }
			g.Restore(ms);
		}
		Bitmap sp = Spr.Get("ms_so_ok"); if (sp == null || t > 1.3f) return;
		float h = 66f * z, w = h * sp.Width / sp.Height, y;
		if (t < hit) { float u = t / hit; y = iy - 80f * z * (1f - u * u); } else if (t < 0.62f) y = iy; else { float u = (t - 0.62f) / 0.5f; y = iy - 80f * z * u * u; }
		float al = t < 0.62f ? 1f : Math.Max(0f, 1f - (t - 0.62f) / 0.5f);
		float sq = (t >= hit && t < hit + 0.12f) ? 1f - 0.10f * (float)Math.Sin((t - hit) / 0.12f * Math.PI) : 1f;
		using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
		{
			ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = al; ia.SetColorMatrix(cm);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
			g.DrawImage(sp, new Rectangle((int)(ix - w / 2f), (int)(y - h * sq), (int)w, (int)(h * sq)), 0, 0, sp.Width, sp.Height, GraphicsUnit.Pixel, ia);
		}
	}
	public void Burst()
	{
		Random rnd = new Random();
		Color[] pal = { Color.FromArgb(255, 205, 80), Color.FromArgb(255, 120, 140), Color.FromArgb(255, 255, 255), Color.FromArgb(120, 220, 190), Color.FromArgb(150, 160, 255) };
		bits = new List<Bit>();
		for (int i = 0; i < 46; i++)
		{
			double an = -Math.PI / 2 + (rnd.NextDouble() - 0.5) * 2.2, sp = 160 + rnd.NextDouble() * 260;
			bits.Add(new Bit { X = Width * 0.2f, Y = Height * 0.75f, Vx = (float)(Math.Cos(an) * sp) * 0.9f + (float)rnd.NextDouble() * 140f, Vy = (float)(Math.Sin(an) * sp), Rot = (float)rnd.NextDouble() * 360f, Vr = (float)(rnd.NextDouble() - 0.5) * 720f, S = Dpi.Zf(3.5f + (float)rnd.NextDouble() * 3f), C = pal[rnd.Next(pal.Length)] });
		}
		burstT0 = clock.Elapsed.TotalSeconds;
	}
	private void DrawBits(Graphics g)
	{
		if (bits == null) return;
		float t = (float)(clock.Elapsed.TotalSeconds - burstT0);
		if (t > 1.9f) { bits = null; return; }
		float fade = t < 1.3f ? 1f : Math.Max(0f, 1f - (t - 1.3f) / 0.6f);
		foreach (Bit b in bits)
		{
			float x = b.X + b.Vx * t * Dpi.Scale, y = b.Y + (b.Vy * t + 520f * t * t) * Dpi.Scale;
			GraphicsState gs = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(b.Rot + b.Vr * t);
			using (SolidBrush br = new SolidBrush(Color.FromArgb((int)(235 * fade), b.C))) g.FillRectangle(br, -b.S / 2, -b.S / 4, b.S, b.S / 2);
			g.Restore(gs);
		}
	}

	// ── 변하지 않는 바탕(하늘·해·달·탑·글자)은 한 장으로 그려 두고 재사용 ──
	private Bitmap bgCache; private string bgKey = ""; private GraphicsPath clipP; private double dkC; private Color topC, horC;
	private float L_tx, L_tw, L_yCap, L_yTime, L_hTime, L_yDate, L_hCap, L_hDate, L_mw;
	private bool tickPending; private bool flipping; private double lastPulse, lastBob; private int bgChk; private bool bgDirty;
	private bool ClockMode { get { return Flap && IsClock(TimeText); } }

	private void LayoutText()
	{
		float mw = 0, textLeft = Dpi.Zf(22);
		if (Mascot != null) { float mh = Height * 0.86f; mw = mh * Mascot.Width / Mascot.Height; if (!MascotRight) textLeft = Dpi.Zf(16) + mw + Dpi.Zf(10); }
		L_mw = mw;
		L_tx = MascotRight ? Dpi.Zf(26) : textLeft; L_tw = Width - L_tx - Dpi.Zf(44);
		L_hCap = Dpi.Zf(20); L_hTime = TimePt * 1.55f * Dpi.Scale; L_hDate = Dpi.Zf(20);
		if (ClockMode) L_hTime = Math.Max(L_hTime, Math.Min(Height - L_hCap - L_hDate - Dpi.Zf(16), Dpi.Zf(TimePt * 2.5f)));
		float total = (string.IsNullOrEmpty(Caption) ? 0 : L_hCap) + L_hTime + (string.IsNullOrEmpty(DateText) ? 0 : L_hDate);
		float y = (Height - total) / 2f - Dpi.Zf(2);
		L_yCap = y; if (!string.IsNullOrEmpty(Caption)) y += L_hCap;
		L_yTime = y; y += L_hTime; L_yDate = y;
	}

	private void EnsureBg(DateTime now)
	{
		if (bgCache != null && bgCache.Width == Width && bgCache.Height == Height && Environment.TickCount - bgChk >= 0 && Environment.TickCount - bgChk < 120 && !bgDirty) return;
		bgChk = Environment.TickCount; bgDirty = false;
		LayoutText();
		Form f = FindForm(); Size fs = f != null ? f.ClientSize : Size.Empty; Point off = Wash.Off(this);
		string key = Width + "x" + Height + "|" + Radius + "|" + Caption + "|" + DateText + "|" + ShowPagoda + "|" + MoonX + "|" + now.ToString("HHmm") + "|" + Theme.Dark + "|" + Theme.Page.ToArgb() + "|" + Theme.Accent.ToArgb() + "|" + Theme.Blue.ToArgb()
			+ "|" + (int)L_mw + "|" + MascotRight + "|" + (int)L_yTime + "|" + (int)L_tx + "|" + fs.Width + "x" + fs.Height + "|" + off.X + "," + off.Y;
		if (bgCache != null && key == bgKey) return;
		bgKey = key;
		if (bgCache == null || bgCache.Width != Width || bgCache.Height != Height)
		{
			if (bgCache != null) bgCache.Dispose();
			bgCache = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
		}
		if (clipP != null) clipP.Dispose();
		clipP = Radius > 0 ? Gfx.Round(new RectangleF(0, 0, Width - 1, Height - 1), Dpi.Zf(Radius)) : null;
		using (Graphics g = Graphics.FromImage(bgCache))
		{
			Gfx.HQ(g); g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			if (clipP != null) g.Clear(Color.Transparent); else Wash.Under(this, g);   // 둥근 모서리 바깥은 저장해 두지 않고, 그릴 때마다 지금 배경색으로 채워요 (테마가 바뀌는 중에도 네모가 남지 않게)
			Color top, hor; Sky.Colors(now, out top, out hor); topC = top; horC = hor;
			double dk = Sky.Darkness(now); dkC = dk;
			RectangleF full = new RectangleF(0, 0, Width, Height);
			GraphicsState gs = null;
			if (clipP != null) { gs = g.Save(); g.SetClip(clipP); }
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, -1, Width, Height + 2), top, hor, 90f)) g.FillRectangle(lb, full);
			if (dk > 0.4)
			{
				float mr = Dpi.Zf(13), mx = Width * MoonX - mr, my = Dpi.Zf(26);
				int ma = (int)(255 * Math.Min(1.0, (dk - 0.4) / 0.4));
				using (GraphicsPath gp = new GraphicsPath())
				{
					gp.AddEllipse(mx - mr * 1.8f, my - mr * 1.8f, mr * 3.6f, mr * 3.6f);
					using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(ma / 4, 214, 204, 255); pb.SurroundColors = new Color[] { Color.FromArgb(0, 214, 204, 255) }; g.FillPath(pb, gp); }
				}
				using (SolidBrush b = new SolidBrush(Color.FromArgb(ma, 244, 240, 255))) g.FillEllipse(b, mx - mr, my - mr, mr * 2, mr * 2);
				using (SolidBrush b = new SolidBrush(Theme.Mix(top, hor, 0.15))) g.FillEllipse(b, mx - mr * 0.45f, my - mr * 1.15f, mr * 2, mr * 2);
			}
			// 해: 일출~일몰 사이에 하늘을 가로지르고, 달은 어두울 때만 나와요
			{
				DateTime rise, set;
				Sky.SunTimes(now, out rise, out set);
				double fr = (now - rise).TotalMinutes / Math.Max(1.0, (set - rise).TotalMinutes);
				if (fr > -0.04 && fr < 1.04)
				{
					double el = Math.Sin(Math.PI * Math.Max(0, Math.Min(1, fr)));
					float sr = Dpi.Zf(12);
					float sxp = Width * (float)(0.655 + 0.05 * Math.Max(0, Math.Min(1, fr)));
					float syp = Height * 0.80f - (float)el * Height * 0.58f;
					double vis = Math.Max(0, Math.Min(1, 1.0 - dk * 1.6));
					if (vis > 0.02)
					{
						using (GraphicsPath gp = new GraphicsPath())
						{
							gp.AddEllipse(sxp - sr * 3f, syp - sr * 3f, sr * 6f, sr * 6f);
							using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb((int)(120 * vis), 255, 214, 130); pb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 214, 130) }; g.FillPath(pb, gp); }
						}
						using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * vis), 255, 232, 160))) g.FillEllipse(b, sxp - sr, syp - sr, sr * 2, sr * 2);
					}
				}
			}
			if (ShowPagoda && Width >= Dpi.Z(300))
			{
				float ph = Height * 0.80f;
				Pagoda.DrawImg(g, Width - Dpi.Zf(12), Height - Dpi.Zf(6), ph, dk, hor);
			}
			Shadowed(g, Caption, Theme.F(10f, 1), Color.FromArgb(215, 255, 255, 255), new RectangleF(L_tx, L_yCap, L_tw, L_hCap), StringAlignment.Near);
			Shadowed(g, DateText, Theme.F(10.5f, 0), Color.FromArgb(225, 255, 255, 255), new RectangleF(L_tx, L_yDate, L_tw, L_hDate), StringAlignment.Near);
			if (gs != null) g.Restore(gs);
		}
	}

	// 호스트가 매 화면 갱신마다 부르면, 움직임이 있을 때는 60프레임 안팎으로, 가만히 있을 때는 절전하듯 30프레임으로 다시 그려요
	public void Pulse()
	{
		double t = clock.Elapsed.TotalSeconds; if (t < lastPulse) lastPulse = t - 1; liveStamp = Environment.TickCount;   // 안전장치: 내부 시계가 리셋돼도 시계가 멈추지 않게
		bool tickNow = tickPending; tickPending = false;
		if (LiveClock && ClockMode) { string ns = DateTime.Now.ToString("HH:mm:ss"); if (ns != TimeText) { TimeText = ns; tickNow = true; } }
		if (Fixed60)   // 화면 주사율(60)에 맞춰 일정한 간격으로 그려요 (그리는 시간에 따라 간격이 달라지지 않게)
		{
			if (!tickNow && t - lastPulse < 0.0145) return; if (tickNow && t - lastPulse < 0.0085) { tickPending = true; return; } PulsePaints++; if (tickNow) TickPaints++; lastPulse = t;
			bool oth = Lively || Bursting || actKind >= 0 || hovTarget > 0f || hov > 0.01f || hearts.Count > 0; calmNow = CalmIdle && !oth;
			// 가만히 있을 때: 배너 전체(하늘·구름·마룡이)를 1초에 60번 다시 그리면 앱 전체가 같이 버벅여요.
			// → 시계 글자는 바뀔 때(넘김 0.4초 동안만 60번), 마룡이 둥실은 그 주변만 1초에 20번 그려요
			bool soon = !CalmIdle && t >= nextAct - 0.05;
			if (!oth && !soon && ClockMode && L_tw > 10f && L_hTime > 4f)
			{
				bool doClock = tickNow || flipping, doM = !CalmIdle && mRect.Width > 4f && t - lastBob >= 0.05;
				if (!doClock && !doM) { lastPulse = t; return; }
				if (doClock) Invalidate(new Rectangle((int)L_tx - 6, (int)L_yTime - 6, (int)L_tw + 12, (int)L_hTime + 12));
				if (doM) { lastBob = t; Rectangle mr = Rectangle.Round(mRect); mr.Inflate((int)Dpi.Zf(8f), (int)Dpi.Zf(8f)); Invalidate(mr); }
				lastPulse = t; Update(); return;
			}
			Invalidate(); Update(); return;
		}
		bool busy = Lively || Bursting || actKind >= 0 || hovTarget > 0f || hov > 0.01f || hearts.Count > 0 || flipping || (!CalmIdle && t >= nextAct - 0.05);
		// 그리는 데 오래 걸리는 PC에서는 그리는 시간의 2배 이상 쉬어서, 설정 변경·클릭 같은 다른 동작이 밀리지 않게 해요
		// 가만히 있을 때는 1초에 20번만 그려서(살짝 둥실거리는 정도) 화면 그리기가 컴퓨터를 계속 붙잡지 않게 해요. 움직일 때(마룡이 반응·시계 넘김)는 60번까지 그려요
		calmNow = CalmIdle && !busy;
		double gap = calmNow ? (ClockMode ? 0.0155 : 0.25) : busy ? Math.Max(0.0155, Math.Min(0.07, PaintMs * 0.0022)) : (PaintMs < 7.0 ? Math.Max(0.0155, PaintMs * 0.0022) : Math.Max(0.033, PaintMs * 0.004));   // 움직임이 늘 있으니(둥실·반짝) 가벼우면 항상 주사율대로
		if (!tickNow && t - lastPulse < gap) return;
		// 시계 숫자만 바뀌는 중이면(마룡이가 가만히 있을 때) 시계 부분만 다시 그려요 → 배너 전체를 매 프레임 그리지 않아 가벼워요
		bool others = Lively || Bursting || actKind >= 0 || hovTarget > 0f || hov > 0.01f || hearts.Count > 0;
		if (CalmIdle && ClockMode && !others && L_tw > 10f && L_hTime > 4f)
		{
			double cgap = flipping ? 0.0155 : 0.25; if (!tickNow && t - lastPulse < cgap) return;
			lastPulse = t; Invalidate(new Rectangle((int)L_tx - 6, (int)L_yTime - 6, (int)L_tw + 12, (int)L_hTime + 12)); Update(); return;
		}
		lastPulse = t; Invalidate(); Update();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Stopwatch pw = Stopwatch.StartNew(); long pt0 = Stopwatch.GetTimestamp();
		if (LiveClock && ClockMode && unchecked(Environment.TickCount - liveStamp) > 1500)   // 다른 화면에 있다 돌아온 첫 그림: 예전 시각이 잠깐 보이지 않게 지금 시각으로 바로 맞춰요 (넘김 없이)
		{ string nn = DateTime.Now.ToString("HH:mm:ss"); TimeText = nn; flapPrev = nn; for (int q = 0; q < 8; q++) flapOld[q] = '\0'; liveStamp = Environment.TickCount; }
		Graphics g = e.Graphics; Gfx.HQ(g);
		if (Width <= 0 || Height <= 0) return;
		DateTime now = Sky.Now();
		EnsureBg(now);
		{
			// 바탕은 보간 없이 그대로 복사 (가장 빠른 경로)
			if (clipP != null) { GraphicsState cs = g.Save(); g.SetClip(clipP, CombineMode.Exclude); Wash.Under(this, g); g.Restore(cs); }
			InterpolationMode im0 = g.InterpolationMode; PixelOffsetMode po0 = g.PixelOffsetMode;
			g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.None;
			Rectangle cr = Rectangle.Intersect(e.ClipRectangle, new Rectangle(0, 0, bgCache.Width, bgCache.Height));   // 다시 그릴 부분만 복사 (작은 부분 갱신이 훨씬 가벼워져요)
			if (cr.Width > 0 && cr.Height > 0) g.DrawImage(bgCache, cr, cr, GraphicsUnit.Pixel);
			g.InterpolationMode = im0; g.PixelOffsetMode = po0;
		}
		RectangleF clipF = e.ClipRectangle; bool partial = e.ClipRectangle.Width < Width || e.ClipRectangle.Height < Height;
		double dk = dkC;
		double t = clock.Elapsed.TotalSeconds;
		GraphicsState gs = null;
		if (clipP != null) { gs = g.Save(); g.SetClip(clipP); }
		if (dk > 0.1)
		{
			float edge = Dpi.Zf(Radius) * 1.2f;
			float pgX = ShowPagoda && Width >= Dpi.Z(300) ? Width - Dpi.Zf(12) - Height * 0.80f * 0.80f : Width + 10;
			using (SolidBrush sb = new SolidBrush(Color.White))
			for (int i = 0; i < stars.Length; i++)
			{
				float sx = stars[i].X * Width, sy = stars[i].Y * Height * 0.72f;
				if (sx > pgX || sx < edge || sx > Width - edge) continue; if (partial && !clipF.Contains(sx, sy)) continue;
				double tw = 0.5 + 0.5 * Math.Sin(t * 1.6 + i * 1.9);
				int a = (int)(255 * dk * (0.25 + 0.6 * tw));
				float sz = Dpi.Zf(i % 5 == 0 ? 2.2f : 1.4f);
				sb.Color = Color.FromArgb(Math.Max(0, Math.Min(255, a)), 255, 244, 214);
				g.FillEllipse(sb, sx, sy, sz, sz);
			}
		}
		// 마룡이
		RectangleF mGuard = mRect; mGuard.Inflate(Dpi.Zf(30f), Dpi.Zf(40f));
		if (Mascot != null && (!partial || mRect.Width < 1f || mGuard.IntersectsWith(clipF)))
		{
			float mh = Height * 0.86f, mw = mh * Mascot.Width / Mascot.Height;
			float ground = Height - Dpi.Zf(9);
			double decay = Hop ? Math.Exp(-t * 1.5) : 0;
			double hop = Math.Abs(Math.Sin(t * 7.0)) * Dpi.Zf(14f) * decay;
			double bob = calmNow ? 0 : Math.Sin(t * 2.6) * Dpi.Zf(2.4f);
			float ang = (float)((calmNow ? 0.0 : Math.Sin(t * 1.7) * 3.0) + Math.Sin(t * 7.0) * 3.0 * decay);
			float aSx = 1f, aSy = 1f, aAng = 0f, aLift = 0f;
			if (Alive)
			{
				DateTime hn = DateTime.Now; float hdt = (float)Math.Min(0.1, (hn - hovLast).TotalSeconds); hovLast = hn;
				hov += (hovTarget - hov) * Math.Min(1f, hdt * 12f);
				Act(t, ref aLift, ref aAng, ref aSx, ref aSy);
				aSx *= 1f + 0.05f * hov; aSy *= 1f + 0.05f * hov; aLift += 3f * hov * Dpi.Scale;
			}
			float lift = (float)(hop + bob + Dpi.Zf(3)) + aLift;
			ang += aAng;
			float cx = MascotRight ? Width - Dpi.Zf(16) - mw / 2 : Dpi.Zf(16) + mw / 2;
			float sw = mw * 0.62f * (1f - Math.Min(0.4f, lift / Dpi.Zf(60)));
			using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 8, 12, 36))) g.FillEllipse(b, cx - sw / 2, ground - Dpi.Zf(4), sw, Dpi.Zf(8));
			GraphicsState s2 = g.Save();
			g.TranslateTransform(cx, ground - lift);
			g.RotateTransform(ang);
			{ double ph = Math.Abs(Math.Sin(t * 7.0)); float sq = Hop ? (float)(Math.Pow(1 - ph, 4) * decay * 0.17) : 0f; g.ScaleTransform(1f + sq * 0.8f, 1f - sq); }
			if (Alive) { if (hov > 0.01f) g.RotateTransform(Math.Max(-8f, Math.Min(8f, (mouseX - cx) / Width * 24f)) * hov); g.ScaleTransform(aSx, aSy); }
			int ch = (int)Math.Ceiling(mh * 1.15f);
			if (mscCache == null || mscH != ch || !object.ReferenceEquals(mscSrc, Mascot))
			{
				if (mscCache != null) mscCache.Dispose();
				mscCache = new Bitmap((int)Math.Ceiling(ch * (float)Mascot.Width / Mascot.Height), ch, PixelFormat.Format32bppPArgb);
				using (Graphics cg = Graphics.FromImage(mscCache)) { cg.InterpolationMode = InterpolationMode.HighQualityBicubic; cg.PixelOffsetMode = PixelOffsetMode.HighQuality; cg.DrawImage(Mascot, 0, 0, mscCache.Width, mscCache.Height); }
				mscH = ch; mscSrc = Mascot;
			}
			InterpolationMode oldIm = g.InterpolationMode; PixelOffsetMode oldPo = g.PixelOffsetMode; g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
			g.DrawImage(mscCache, -mw / 2, -mh, mw, mh);
			g.InterpolationMode = oldIm; g.PixelOffsetMode = oldPo;
			g.Restore(s2);
			mRect = new RectangleF(cx - mw / 2, ground - mh, mw, mh);
			if (Alive)
			{
				// 하트
				for (int i = hearts.Count - 1; i >= 0; i--)
				{
					Heart h = hearts[i]; double u = (t - h.T0) / 1.5; if (u > 1) { hearts.RemoveAt(i); continue; } if (u < 0) continue;
					float hx = cx + h.X + h.Dx * (float)u + (float)Math.Sin(u * 6 + h.Ph) * 4f * Dpi.Scale, hy = ground - mh * 0.85f - (float)(u * 46 * Dpi.Scale);
					int ha = (int)(235 * Math.Min(1.0, (1 - u) * 2.2)); float hs = Dpi.Zf(7f) * (float)(0.6 + 0.5 * Math.Min(1.0, u * 4));
					GraphicsState hgs = g.Save(); g.TranslateTransform(hx, hy); g.ScaleTransform(hs, hs);
					heartBr.Color = Color.FromArgb(Math.Max(0, ha), 255, 120, 150); g.FillPath(heartBr, HeartPath());
					g.Restore(hgs);
				}
				// 밤에는 꾸벅꾸벅 졸아요
				int hr = now.Hour;
				if (hr >= 22 || hr < 5)
				{
					Font zf = Theme.F(11f, 2);
					for (int i = 0; i < 3; i++)
					{
						double u = ((t * 0.5) + i / 3.0) % 1.0; float zx = cx + mw * 0.30f + (float)(u * 18 * Dpi.Scale), zy = ground - mh * 0.92f - (float)(u * 34 * Dpi.Scale);
						Gfx.Text(g, "z", zf, Color.FromArgb((int)(220 * Math.Sin(u * Math.PI)), 255, 255, 255), new RectangleF(zx, zy, Dpi.Zf(20), Dpi.Zf(20)), StringAlignment.Near, StringAlignment.Near, false);
					}
				}
			}
		}
		if (ClockMode) { RectangleF ckR = new RectangleF(L_tx - 6, L_yTime - 6, L_tw + 12, L_hTime + 12); if (!partial || ckR.IntersectsWith(clipF)) DrawFlap(g, new RectangleF(L_tx, L_yTime, L_tw, L_hTime), t); } else Shadowed(g, TimeText, Theme.F(TimePt, 2), Color.White, new RectangleF(L_tx, L_yTime, L_tw, L_hTime), StringAlignment.Near);
		DrawBits(g); DrawStamp(g);
		if (gs != null) g.Restore(gs);
		PaintMs = PaintMs * 0.85 + pw.Elapsed.TotalMilliseconds * 0.15; if (pw.Elapsed.TotalMilliseconds > 8.0) PaintProf.Add("Sky긴그림(8ms+)", pt0); if (PaintProf.On) lock (Stamps) { Stamps.Add(Stopwatch.GetTimestamp()); StampIds.Add(this.GetHashCode()); }
	}

	private static void Shadowed(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h)
	{
		if (string.IsNullOrEmpty(s)) return;
		Gfx.Text(g, s, f, Color.FromArgb(70, 6, 10, 40), new RectangleF(r.X + 1, r.Y + 1, r.Width, r.Height), h, StringAlignment.Center, false);
		Gfx.Text(g, s, f, c, r, h, StringAlignment.Center, false);
	}
}

// ─────────────────────────────────────────────────────────────
//  시간대 별 타임라인 (기록한 시간대는 금빛 별)
// ─────────────────────────────────────────────────────────────
internal class StarItem
{
	public string Label;
	public int State;     // 0 앞으로 · 1 기록함 · 2 놓침 · 3 지금
}

internal class StarRow : Ctl
{
	public List<StarItem> Items = new List<StarItem>();
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		int n = Items.Count; if (n == 0) return;
		float step = Width / (float)n;
		float cy = Height * 0.40f;
		for (int i = 0; i < n; i++)
		{
			StarItem it = Items[i];
			float cx = step * (i + 0.5f);
			float R = Dpi.Zf(11);
			if (it.State == 1)
			{
				using (GraphicsPath gp = new GraphicsPath())
				{
					gp.AddEllipse(cx - R * 1.7f, cy - R * 1.7f, R * 3.4f, R * 3.4f);
					using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(90, Theme.Gold); pb.SurroundColors = new Color[] { Color.FromArgb(0, Theme.Gold) }; g.FillPath(pb, gp); }
				}
				using (GraphicsPath sp = Gfx.Star(cx, cy, R, R * 0.46f)) using (SolidBrush b = new SolidBrush(Theme.Gold)) g.FillPath(b, sp);
			}
			else if (it.State == 2)
			{
				using (GraphicsPath sp = Gfx.Star(cx, cy, R, R * 0.46f)) using (Pen p = new Pen(Color.FromArgb(232, 130, 134), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPath(p, sp);
			}
			else if (it.State == 3)
			{
				using (Pen p = new Pen(Theme.Accent, 2f)) g.DrawEllipse(p, cx - R * 0.8f, cy - R * 0.8f, R * 1.6f, R * 1.6f);
				using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, cx - R * 0.32f, cy - R * 0.32f, R * 0.64f, R * 0.64f);
			}
			else
			{
				using (SolidBrush b = new SolidBrush(Theme.LineStrong)) g.FillEllipse(b, cx - Dpi.Zf(3), cy - Dpi.Zf(3), Dpi.Zf(6), Dpi.Zf(6));
			}
			Color lc = it.State == 3 ? Theme.Accent : (it.State == 1 ? Theme.Ink : Theme.Sub);
			Gfx.Text(g, it.Label, Theme.F(10f, it.State == 3 ? 1 : 0), lc, new RectangleF(cx - step / 2, cy + R + Dpi.Zf(3), step, Dpi.Zf(18)), StringAlignment.Center, StringAlignment.Near, false);
		}
	}
}

// 마감까지 남은 시간
internal class CountdownBar : Ctl
{
	public int SlotHour;
	public TimeSpan Left;
	public int State;
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		Color c = Theme.StateColor(State);
		bool expired = State == 3;
		string cap = expired ? (SlotHour + "시 확인 시간이 지났어요") : (SlotHour + "시 확인 마감까지");
		string tm;
		if (expired) tm = "마감";
		else { int s = (int)Math.Ceiling(Left.TotalSeconds); if (s < 0) s = 0; tm = (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }
		float pad = 0, barH = Dpi.Zf(5);
		RectangleF top = new RectangleF(pad, 0, Width - pad * 2, Height - barH - Dpi.Zf(12));
		Gfx.Text(g, cap, Theme.F(11f, 0), Theme.Sub, top, StringAlignment.Near, StringAlignment.Center, false);
		Gfx.Text(g, tm, Theme.F(expired ? 16f : 22f, 2), c, top, StringAlignment.Far, StringAlignment.Center, false);
		RectangleF track = new RectangleF(pad, Height - barH - Dpi.Zf(2), Width - pad * 2, barH);
		Gfx.Fill(g, track, barH / 2, Theme.Mix(Theme.Bg, c, 0.16));
		double frac = Math.Max(0, Math.Min(1, Left.TotalSeconds / 3600.0));
		float fw = (float)(track.Width * frac);
		if (fw >= barH && !expired) Gfx.Fill(g, new RectangleF(track.X, track.Y, fw, track.Height), barH / 2, c);
	}
}

// ─────────────────────────────────────────────────────────────
//  사이드바 메뉴
// ─────────────────────────────────────────────────────────────
internal enum NavIcon { Home, Bell, Calendar, Clock, Gear, Phone, Flag, Doc, Won, Help }


// 포인트 색 고르는 동그라미들 (왼쪽 아래)
internal class AccentPicker : Ctl
{
	private int hov = -1;
	public AccentPicker() { Cursor = Cursors.Hand; Size = new Size(Dpi.Z(176), Dpi.Z(30)); }
	private RectangleF Dot(int i) { float dm = Dpi.Zf(21), gap = Dpi.Zf(7.5f); return new RectangleF(Dpi.Zf(2) + i * (dm + gap), (Height - dm) / 2f, dm, dm); }
	private int At(Point p) { for (int i = 0; i < Theme.AccentBase.Length; i++) { RectangleF r = Dot(i); r.Inflate(Dpi.Zf(4), Dpi.Zf(4)); if (r.Contains(p)) return i; } return -1; }
	protected override void OnMouseMove(MouseEventArgs e) { int i = At(e.Location); if (i != hov) { hov = i; Invalidate(); } base.OnMouseMove(e); }
	protected override void OnMouseLeave(EventArgs e) { hov = -1; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseClick(MouseEventArgs e) { int i = At(e.Location); if (i >= 0 && i != Theme.AccentIdx) Program.SetAccent(i); base.OnMouseClick(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		for (int i = 0; i < Theme.AccentBase.Length; i++)
		{
			RectangleF r = Dot(i); bool sel = i == Theme.AccentIdx; float grow = (i == hov && !sel) ? Dpi.Zf(1.5f) : 0f;
			r.Inflate(grow, grow);
			using (SolidBrush b = new SolidBrush(Theme.AccentBase[i])) g.FillEllipse(b, r);
			if (sel)
			{
				RectangleF ro = r; ro.Inflate(Dpi.Zf(3.4f), Dpi.Zf(3.4f));
				using (Pen p = new Pen(Theme.Mix(Theme.Ink, Theme.AccentBase[i], 0.35), 1.8f)) g.DrawEllipse(p, ro);
				using (Pen p = new Pen(Color.FromArgb(235, 255, 255, 255), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
					g.DrawLines(p, new PointF[] { new PointF(r.X + r.Width * 0.28f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.68f), new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.34f) });
			}
		}
	}
}

internal class NavItem : Ctl
{
	public NavIcon Icon;
	public bool Selected;
	public bool Compact;
	private AV hv, sel;
	public NavItem(string text, NavIcon icon) { Text = text; Icon = icon; Cursor = Cursors.Hand; BackColor = Theme.Side; hv = new AV(this, 0) { Rate = 18f }; sel = new AV(this, 0) { Rate = 16f }; }
	protected override void OnMouseEnter(EventArgs e) { hv.Set(1); base.OnMouseEnter(e); }
	protected override void OnMouseLeave(EventArgs e) { hv.Set(0); base.OnMouseLeave(e); }
	private float popK;
	public void SetSelected(bool v) { if (v && !Selected) Anim.Run(460, delegate(float t) { return t; }, delegate(float t) { popK = (float)Math.Sin(t * Math.PI) * (1f - t * 0.3f); Invalidate(); }, null); Selected = v; sel.Set(v ? 1 : 0); }
	public void SnapSelected(bool v) { Selected = v; sel.Snap(v ? 1 : 0); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF r = new RectangleF(0, 1, Width - 1, Height - 2);
		SidePanel sp = Parent as SidePanel;
		if (sp != null)
		{
			RectangleF pr = sp.Pill; pr.Offset(-Left, -Top);
			Gfx.Fill(g, pr, Dpi.Zf(17), Theme.Dark ? Theme.Hover : Theme.Bg);
		}
		if (hv.V > 0.01f) Gfx.Fill(g, r, Dpi.Zf(17), Color.FromArgb((int)(150 * hv.V * (1 - sel.V)), Theme.Hover));
		Color c = Theme.Mix(Theme.Sub, Theme.Ink, sel.V);
		float ix = Compact ? (Width - Dpi.Zf(16)) / 2f - 0.5f : Dpi.Zf(14), iy = Height / 2f, s = Dpi.Zf(8);
		if (popK > 0.001f) { float pcx = ix + s, pcy = iy; g.TranslateTransform(pcx, pcy); g.RotateTransform(popK * -9f); g.ScaleTransform(1f + 0.28f * popK, 1f + 0.28f * popK); g.TranslateTransform(-pcx, -pcy); }
		using (Pen p = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
		{
			float cx = ix + s, cy = iy;
			switch (Icon)
			{
				case NavIcon.Home:
					g.DrawLines(p, new PointF[] { new PointF(cx - s, cy), new PointF(cx, cy - s), new PointF(cx + s, cy) });
					g.DrawLines(p, new PointF[] { new PointF(cx - s * 0.7f, cy - s * 0.1f), new PointF(cx - s * 0.7f, cy + s * 0.85f), new PointF(cx + s * 0.7f, cy + s * 0.85f), new PointF(cx + s * 0.7f, cy - s * 0.1f) });
					break;
				case NavIcon.Bell:
					g.DrawArc(p, cx - s * 0.75f, cy - s, s * 1.5f, s * 1.6f, 180f, 180f);
					g.DrawLine(p, cx - s * 0.75f, cy - s * 0.2f, cx - s * 0.75f, cy + s * 0.5f); g.DrawLine(p, cx + s * 0.75f, cy - s * 0.2f, cx + s * 0.75f, cy + s * 0.5f);
					g.DrawLine(p, cx - s, cy + s * 0.55f, cx + s, cy + s * 0.55f);
					g.DrawLine(p, cx - s * 0.25f, cy + s * 0.95f, cx + s * 0.25f, cy + s * 0.95f);
					break;
				case NavIcon.Calendar:
					using (GraphicsPath cp = Gfx.Round(new RectangleF(cx - s, cy - s * 0.8f, s * 2, s * 1.8f), Dpi.Zf(2.5f))) g.DrawPath(p, cp);
					g.DrawLine(p, cx - s, cy - s * 0.2f, cx + s, cy - s * 0.2f);
					g.DrawLine(p, cx - s * 0.45f, cy - s * 1.15f, cx - s * 0.45f, cy - s * 0.5f); g.DrawLine(p, cx + s * 0.45f, cy - s * 1.15f, cx + s * 0.45f, cy - s * 0.5f);
					break;
				case NavIcon.Won:
					g.DrawEllipse(p, cx - s, cy - s, s * 2, s * 2);
					g.DrawLines(p, new PointF[] { new PointF(cx - s * 0.55f, cy - s * 0.45f), new PointF(cx - s * 0.2f, cy + s * 0.45f), new PointF(cx, cy - s * 0.1f), new PointF(cx + s * 0.2f, cy + s * 0.45f), new PointF(cx + s * 0.55f, cy - s * 0.45f) });
					g.DrawLine(p, cx - s * 0.6f, cy - s * 0.05f, cx + s * 0.6f, cy - s * 0.05f);
					break;
				case NavIcon.Flag:
					g.DrawLine(p, cx - s * 0.7f, cy - s, cx - s * 0.7f, cy + s);
					g.DrawLines(p, new PointF[] { new PointF(cx - s * 0.7f, cy - s), new PointF(cx + s, cy - s * 0.55f), new PointF(cx - s * 0.7f, cy + s * 0.1f) });
					break;
				case NavIcon.Doc:
					using (GraphicsPath dp = Gfx.Round(new RectangleF(cx - s * 0.8f, cy - s, s * 1.6f, s * 2f), Dpi.Zf(2.5f))) g.DrawPath(p, dp);
					g.DrawLine(p, cx - s * 0.4f, cy - s * 0.35f, cx + s * 0.4f, cy - s * 0.35f); g.DrawLine(p, cx - s * 0.4f, cy + s * 0.15f, cx + s * 0.4f, cy + s * 0.15f); g.DrawLine(p, cx - s * 0.4f, cy + s * 0.6f, cx + s * 0.1f, cy + s * 0.6f);
					break;
				case NavIcon.Phone:
					using (GraphicsPath pp = Gfx.Round(new RectangleF(cx - s * 0.62f, cy - s, s * 1.24f, s * 2f), Dpi.Zf(2.5f))) g.DrawPath(p, pp);
					g.DrawLine(p, cx - s * 0.2f, cy + s * 0.65f, cx + s * 0.2f, cy + s * 0.65f);
					break;
				case NavIcon.Gear:
					g.DrawEllipse(p, cx - s * 0.45f, cy - s * 0.45f, s * 0.9f, s * 0.9f);
					for (int k = 0; k < 8; k++) { double an = k * Math.PI / 4; g.DrawLine(p, cx + (float)Math.Cos(an) * s * 0.78f, cy + (float)Math.Sin(an) * s * 0.78f, cx + (float)Math.Cos(an) * s * 1.05f, cy + (float)Math.Sin(an) * s * 1.05f); }
					g.DrawEllipse(p, cx - s * 0.78f, cy - s * 0.78f, s * 1.56f, s * 1.56f);
					break;
				case NavIcon.Help:
					g.DrawEllipse(p, cx - s, cy - s, s * 2, s * 2);
					g.DrawArc(p, cx - s * 0.38f, cy - s * 0.6f, s * 0.76f, s * 0.7f, 180f, 230f);
					g.DrawLine(p, cx, cy + s * 0.1f, cx, cy + s * 0.25f);
					g.DrawLine(p, cx, cy + s * 0.52f, cx, cy + s * 0.54f);
					break;
				default:
					g.DrawEllipse(p, cx - s, cy - s, s * 2, s * 2);
					g.DrawLines(p, new PointF[] { new PointF(cx, cy - s * 0.55f), new PointF(cx, cy), new PointF(cx + s * 0.5f, cy + s * 0.3f) });
					break;
			}
		}
		g.ResetTransform();
		if (!Compact) Gfx.Text(g, Text, Theme.F(10.5f, sel.V > 0.5f ? 1 : 0), c, new RectangleF(Dpi.Zf(42), 0, Width - Dpi.Zf(46), Height), StringAlignment.Near, StringAlignment.Center, false);
	}
}

// ─────────────────────────────────────────────────────────────
//  달력
// ─────────────────────────────────────────────────────────────
internal class MiniCal : Ctl
{
	public DateTime Month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
	public DateTime Selected = DateTime.Today;
	public Predicate<DateTime> IsHoliday;
	public event EventHandler DateChanged;
	private int cell, head, wk;
	private DateTime hov = DateTime.MinValue;
	private int hovNav;

	public MiniCal()
	{
		cell = Dpi.Z(40); head = Dpi.Z(44); wk = Dpi.Z(26);
		Size = new Size(cell * 7, head + wk + cell * 6 + Dpi.Z(4));
		Cursor = Cursors.Hand;
	}
	private RectangleF Prev { get { return new RectangleF(0, 4, Dpi.Zf(36), head - 8); } }
	private RectangleF Next { get { return new RectangleF(Width - Dpi.Zf(36), 4, Dpi.Zf(36), head - 8); } }
	private DateTime StartDay { get { int off = (int)Month.DayOfWeek; return Month.AddDays(-off); } }

	private DateTime? Hit(Point p)
	{
		if (p.Y < head + wk) return null;
		int col = p.X / cell, row = (p.Y - head - wk) / cell;
		if (col < 0 || col > 6 || row < 0 || row > 5) return null;
		return StartDay.AddDays(row * 7 + col);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		DateTime? h = Hit(e.Location); DateTime hv = h.HasValue ? h.Value : DateTime.MinValue;
		int nav = Prev.Contains(e.Location) ? 1 : (Next.Contains(e.Location) ? 2 : 0);
		if (hv != hov || nav != hovNav) { hov = hv; hovNav = nav; Invalidate(); }
		base.OnMouseMove(e);
	}
	protected override void OnMouseLeave(EventArgs e) { hov = DateTime.MinValue; hovNav = 0; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (Prev.Contains(e.Location)) { Month = Month.AddMonths(-1); Invalidate(); return; }
		if (Next.Contains(e.Location)) { Month = Month.AddMonths(1); Invalidate(); return; }
		DateTime? d = Hit(e.Location);
		if (d.HasValue)
		{
			Selected = d.Value.Date;
			if (d.Value.Month != Month.Month) Month = new DateTime(d.Value.Year, d.Value.Month, 1);
			Invalidate();
			if (DateChanged != null) DateChanged(this, EventArgs.Empty);
		}
		base.OnMouseClick(e);
	}


	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		Gfx.Text(g, Month.Year + "년 " + Month.Month + "월", Theme.F(11.5f, 1), Theme.Ink, new RectangleF(0, 0, Width, head), StringAlignment.Center, StringAlignment.Center, false);
		for (int k = 1; k <= 2; k++)
		{
			RectangleF nr = k == 1 ? Prev : Next;
			if (hovNav == k) Gfx.Fill(g, nr, Dpi.Zf(7), Theme.Hover);
			float cx = nr.X + nr.Width / 2, cy = nr.Y + nr.Height / 2, s = Dpi.Zf(4.5f);
			using (Pen p = new Pen(Theme.Sub, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			{
				if (k == 1) g.DrawLines(p, new PointF[] { new PointF(cx + s * 0.5f, cy - s), new PointF(cx - s * 0.5f, cy), new PointF(cx + s * 0.5f, cy + s) });
				else g.DrawLines(p, new PointF[] { new PointF(cx - s * 0.5f, cy - s), new PointF(cx + s * 0.5f, cy), new PointF(cx - s * 0.5f, cy + s) });
			}
		}
		string[] wn = { "일", "월", "화", "수", "목", "금", "토" };
		for (int i = 0; i < 7; i++)
			Gfx.Text(g, wn[i], Theme.F(9.5f, 1), i == 0 ? Color.FromArgb(232, 130, 134) : (i == 6 ? Color.FromArgb(110, 150, 235) : Theme.Muted), new RectangleF(i * cell, head, cell, wk), StringAlignment.Center, StringAlignment.Center, false);
		DateTime d0 = StartDay;
		for (int i = 0; i < 42; i++)
		{
			DateTime d = d0.AddDays(i);
			int col = i % 7, row = i / 7;
			RectangleF cr = new RectangleF(col * cell + Dpi.Zf(3), head + wk + row * cell + Dpi.Zf(3), cell - Dpi.Zf(6), cell - Dpi.Zf(6));
			bool other = d.Month != Month.Month;
			bool sel = d.Date == Selected.Date, today = d.Date == DateTime.Today;
			bool hol = IsHoliday != null && IsHoliday(d);
			bool sun = d.DayOfWeek == DayOfWeek.Sunday, sat = d.DayOfWeek == DayOfWeek.Saturday;
			Color tc = hol || sun ? Theme.Red : (sat ? Theme.Blue : Theme.Ink);
			if (other) tc = Theme.Mix(tc, Theme.Bg, 0.62);
			if (sel) { Gfx.Fill(g, cr, Dpi.Zf(9), Theme.AccentBtn); tc = Color.White; }
			else
			{
				if (d.Date == hov.Date) Gfx.Fill(g, cr, Dpi.Zf(13), Theme.Hover);
				if (today) Gfx.Border(g, cr, Dpi.Zf(9), Theme.Accent, 1.5f);
			}
			Gfx.Text(g, d.Day.ToString(), Theme.F(10.5f, sel || today ? 1 : 0), tc, new RectangleF(cr.X, cr.Y - Dpi.Zf(1), cr.Width, cr.Height), StringAlignment.Center, StringAlignment.Center, false);
			if (hol && !sun && !sat && !sel)
				using (SolidBrush b = new SolidBrush(other ? Color.FromArgb(240, 190, 192) : Theme.Red)) g.FillEllipse(b, cr.X + cr.Width / 2 - Dpi.Zf(1.8f), cr.Bottom - Dpi.Zf(6.5f), Dpi.Zf(3.6f), Dpi.Zf(3.6f));
		}
	}
}

// ─────────────────────────────────────────────────────────────
//  진행 막대 · 창 버튼 · 기록 달력
// ─────────────────────────────────────────────────────────────
internal class ProgBar : Ctl
{
	private double frac;
	private AV av;
	public Color C = Theme.Accent;
	public double Frac
	{
		get { return frac; }
		set
		{
			if (av == null) { av = new AV(this, 0) { Rate = 7f }; }
			if (Math.Abs(value - frac) < 1e-9 && frac == av.T) return;
			frac = value; av.Set((float)value);
		}
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF tr = new RectangleF(0, 0, Width, Height);
		Gfx.Fill(g, tr, Height / 2f, Theme.Mix(Theme.Bg, C, 0.16));
		double f = Math.Max(0, Math.Min(1, av == null ? 0 : av.V));
		float w = (float)(Width * f);
		RectangleF fr = w >= Height * 0.9f ? new RectangleF(0, 0, w, Height) : (f > 0 ? new RectangleF(0, 0, Height, Height) : RectangleF.Empty);
		if (fr.Width > 0)
		{
			using (GraphicsPath fp = Gfx.Round(fr, Height / 2f))
			{
				using (LinearGradientBrush gb = new LinearGradientBrush(new RectangleF(fr.X - 1, fr.Y, fr.Width + 2, fr.Height), Theme.Mix(C, Color.Black, 0.06), Theme.Mix(C, Color.White, 0.28), 0f)) g.FillPath(gb, fp);
				GraphicsState gs = g.Save(); g.SetClip(fp);
				using (LinearGradientBrush sh = new LinearGradientBrush(new RectangleF(0, 0, fr.Width + 2, Height * 0.55f), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(sh, 0, 0, fr.Width + 2, Height * 0.55f);
				g.Restore(gs);
			}
		}
	}
}

internal class WinBtn : Ctl
{
	public bool IsClose;
	public bool IsMax;
	public bool Restore;
	private AV hv;
	public WinBtn(bool close) { IsClose = close; Cursor = Cursors.Hand; Size = new Size(Dpi.Z(36), Dpi.Z(30)); hv = new AV(this, 0) { Rate = 22f }; }
	protected override void OnMouseEnter(EventArgs e) { hv.Set(1); base.OnMouseEnter(e); }
	protected override void OnMouseLeave(EventArgs e) { hv.Set(0); base.OnMouseLeave(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		RectangleF r = new RectangleF(1, 1, Width - 2, Height - 2);
		Color ink = Theme.Sub;
		if (hv.V > 0.01f) { Gfx.Fill(g, r, Dpi.Zf(7), Theme.Mix(BackColor, IsClose ? Theme.Red : Theme.Hover, hv.V)); if (IsClose) ink = Theme.Mix(Theme.Sub, Color.White, hv.V); }
		float cx = Width / 2f, cy = Height / 2f, s = Dpi.Zf(4.5f);
		using (Pen p = new Pen(ink, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
		{
			if (IsClose) { g.DrawLine(p, cx - s, cy - s, cx + s, cy + s); g.DrawLine(p, cx - s, cy + s, cx + s, cy - s); }
			else if (IsMax)
			{
				if (!Restore) using (GraphicsPath rp = Gfx.Round(new RectangleF(cx - s, cy - s, s * 2, s * 2), Dpi.Zf(1.5f))) g.DrawPath(p, rp);
				else
				{
					using (GraphicsPath rp = Gfx.Round(new RectangleF(cx - s, cy - s * 0.4f, s * 1.6f, s * 1.6f), Dpi.Zf(1.5f))) g.DrawPath(p, rp);
					g.DrawLines(p, new PointF[] { new PointF(cx - s * 0.4f, cy - s * 0.4f), new PointF(cx - s * 0.4f, cy - s), new PointF(cx + s, cy - s), new PointF(cx + s, cy + s * 0.4f), new PointF(cx + s * 0.6f, cy + s * 0.4f) });
				}
			}
			else g.DrawLine(p, cx - s, cy + 1, cx + s, cy + 1);
		}
	}
}

// 날짜마다 초과 시간을 찍어 보여주는 기록 달력
internal class RecCal : Ctl
{
	public DateTime Month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
	public DateTime Selected = DateTime.Today;
	public Func<DateTime, int> Hours;
	public Func<DateTime, int> Missed;
	public Func<DateTime, bool> Pre8;
	public Predicate<DateTime> IsHoliday;
	public event EventHandler DateChanged;
	private int cw, ch, wk;
	private DateTime hov = DateTime.MinValue;

	public RecCal()
	{
		cw = Dpi.Z(48); ch = Dpi.Z(46); wk = Dpi.Z(28);
		Size = new Size(cw * 7, wk + ch * 6);
		Cursor = Cursors.Hand;
	}
	private DateTime StartDay { get { return Month.AddDays(-(int)Month.DayOfWeek); } }
	private DateTime? Hit(Point p)
	{
		if (p.Y < wk) return null;
		int col = p.X / cw, row = (p.Y - wk) / ch;
		if (col < 0 || col > 6 || row < 0 || row > 5) return null;
		return StartDay.AddDays(row * 7 + col);
	}
	protected override void OnMouseMove(MouseEventArgs e) { DateTime? h = Hit(e.Location); DateTime v = h.HasValue ? h.Value : DateTime.MinValue; if (v != hov) { hov = v; Invalidate(); } base.OnMouseMove(e); }
	protected override void OnMouseLeave(EventArgs e) { hov = DateTime.MinValue; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseClick(MouseEventArgs e)
	{
		DateTime? d = Hit(e.Location);
		if (d.HasValue)
		{
			Selected = d.Value.Date;
			if (d.Value.Month != Month.Month) Month = new DateTime(d.Value.Year, d.Value.Month, 1);
			Invalidate();
			if (DateChanged != null) DateChanged(this, EventArgs.Empty);
		}
		base.OnMouseClick(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); Back(g);
		string[] wn = { "일", "월", "화", "수", "목", "금", "토" };
		for (int i = 0; i < 7; i++)
			Gfx.Text(g, wn[i], Theme.F(10f, 1), i == 0 ? Theme.Red : (i == 6 ? Theme.Blue : Theme.Sub), new RectangleF(i * cw, 0, cw, wk), StringAlignment.Center, StringAlignment.Center, false);
		DateTime d0 = StartDay;
		for (int i = 0; i < 42; i++)
		{
			DateTime d = d0.AddDays(i);
			int col = i % 7, row = i / 7;
			RectangleF cr = new RectangleF(col * cw + Dpi.Zf(2.5f), wk + row * ch + Dpi.Zf(2.5f), cw - Dpi.Zf(5), ch - Dpi.Zf(5));
			bool other = d.Month != Month.Month;
			bool sel = d.Date == Selected.Date, today = d.Date == DateTime.Today;
			int h = (!other && Hours != null) ? Hours(d) : 0;
			bool hol = IsHoliday != null && IsHoliday(d);
			bool sun = d.DayOfWeek == DayOfWeek.Sunday, sat = d.DayOfWeek == DayOfWeek.Saturday;
			Color tc = hol || sun ? Theme.Red : (sat ? Theme.Blue : Theme.Ink);
			if (other) tc = Theme.Mix(tc, Theme.Bg, 0.68);
			Color sub = Theme.Accent;
			if (h > 0)
			{
				double k = Math.Min(1.0, h / 6.0);                 // 많이 찍을수록 진해져요
				Color fill = Theme.Mix(Theme.AccentSoft, Theme.Accent, 0.10 + 0.40 * k);   // 은은한 농도 (글자는 항상 또렷하게)
				Gfx.Fill(g, cr, Dpi.Zf(13), fill);
				sub = Theme.Dark ? Theme.Accent : Theme.AccentDown;
			}
			else if (d.Date == hov.Date && !other) Gfx.Fill(g, cr, Dpi.Zf(13), Theme.Hover);
			if (sel) Gfx.Border(g, new RectangleF(cr.X + 0.5f, cr.Y + 0.5f, cr.Width - 1, cr.Height - 1), Dpi.Zf(13), Theme.Ink, 1.8f);
			else if (today) Gfx.Border(g, new RectangleF(cr.X + 0.5f, cr.Y + 0.5f, cr.Width - 1, cr.Height - 1), Dpi.Zf(13), Theme.Accent, 1.4f);
			float ty = h > 0 ? Dpi.Zf(3) : 0;
			Gfx.Text(g, d.Day.ToString(), Theme.F(11f, (h > 0 || today || sel) ? 1 : 0), tc, new RectangleF(cr.X, cr.Y + (h > 0 ? Dpi.Zf(2) : 0), cr.Width, h > 0 ? cr.Height * 0.55f : cr.Height), StringAlignment.Center, StringAlignment.Center, false);
			if (h > 0)
				Gfx.Text(g, h + "h", Theme.F(9.5f, 2), sub, new RectangleF(cr.X, cr.Y + cr.Height * 0.52f, cr.Width, cr.Height * 0.44f), StringAlignment.Center, StringAlignment.Center, false);
			else if (hol && !sun && !sat && !other)
				using (SolidBrush b = new SolidBrush(Theme.Red)) g.FillEllipse(b, cr.X + cr.Width / 2 - Dpi.Zf(1.8f), cr.Bottom - Dpi.Zf(6.5f), Dpi.Zf(3.6f), Dpi.Zf(3.6f));
			if (!other && Pre8 != null && Pre8(d))
			{
				float dr = Dpi.Zf(3f), dx = cr.X + dr + Dpi.Zf(3), dy = cr.Y + dr + Dpi.Zf(2);
				using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, dx - dr - 1, dy - dr - 1, dr * 2 + 2, dr * 2 + 2);
				using (SolidBrush b = new SolidBrush(Theme.Amber)) g.FillEllipse(b, dx - dr, dy - dr, dr * 2, dr * 2);
			}
			if (!other && Missed != null && Missed(d) > 0)
			{
				float dr = Dpi.Zf(3.4f), dx = cr.Right - dr - Dpi.Zf(3), dy = cr.Y + dr + Dpi.Zf(2);
				using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, dx - dr - 1, dy - dr - 1, dr * 2 + 2, dr * 2 + 2);
				using (SolidBrush b = new SolidBrush(Theme.Red)) g.FillEllipse(b, dx - dr, dy - dr, dr * 2, dr * 2);
			}
		}
	}
}

// ─────────────────────────────────────────────────────────────
//  미륵사지 석탑 일러스트 (사진 특징을 살려 단순화)
//   · 넓고 낮은 기단과 앞쪽 계단
//   · 아래 두 층: 기둥이 늘어선 몸돌 + 아주 넓고 납작한 지붕돌
//   · 위쪽 층: 폭이 확 줄고, 왼쪽이 대각선으로 무너져 돌층이 계단처럼 남음
// ─────────────────────────────────────────────────────────────
internal static class Pagoda
{
	private static Bitmap img; private static bool tried;
	internal static void DrawImg(Graphics g, float x, float bottom, float h, double dk, Color hor)
	{
		if (!tried) { tried = true; try { using (System.IO.Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("pagoda.png")) img = new Bitmap(s); } catch { } }
		if (img == null) return;
		float w = h * img.Width / img.Height;
		float k = (float)(1 - 0.62 * dk);                       // 밤에는 어둡게
		float m = 0.10f;                                         // 하늘빛을 살짝 섞어요
		float r = k * (1 - m) + m * hor.R / 255f * k, gg = k * (1 - m) + m * hor.G / 255f * k, b = k * (1 - m) + m * hor.B / 255f * k;
		float nb = (float)(0.05 * dk);
		float[][] cm = { new float[]{ r, 0, 0, 0, 0 }, new float[]{ 0, gg, 0, 0, 0 }, new float[]{ 0, 0, b, 0, 0 }, new float[]{ 0, 0, 0, 0.92f, 0 }, new float[]{ nb, nb, nb * 2, 0, 1 } };
		using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
		{
			ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(cm));
			System.Drawing.Drawing2D.InterpolationMode om = g.InterpolationMode;
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
			g.DrawImage(img, new RectangleF(x - w, bottom - h, w, h).ToRect(), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
			g.InterpolationMode = om;
		}
	}
	private static float cx, by, h;

	private static void R(Graphics g, float u0, float v0, float u1, float v1, Color c)
	{
		float x0 = cx + u0 * h, x1 = cx + u1 * h, y0 = by - v1 * h, y1 = by - v0 * h;
		if (x1 - x0 <= 0 || y1 - y0 <= 0) return;
		using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, x0, y0, x1 - x0, y1 - y0);
	}
	private static void Slab(Graphics g, float u0, float u1, float v0, float v1, Color L, Color D, float lift)
	{
		float t = (v1 - v0);
		PointF[] top = {
			new PointF(cx + u0 * h, by - (v0 + t * 0.35f) * h),
			new PointF(cx + (u0 + 0.012f) * h, by - (v0 + t * 0.35f) * h),
			new PointF(cx + (u0 + 0.03f) * h, by - (v1 + lift) * h),
			new PointF(cx + (u1 - 0.03f) * h, by - (v1 + lift) * h),
			new PointF(cx + (u1 - 0.012f) * h, by - (v0 + t * 0.35f) * h),
			new PointF(cx + u1 * h, by - (v0 + t * 0.35f) * h),
			new PointF(cx + u1 * h, by - v0 * h),
			new PointF(cx + u0 * h, by - v0 * h) };
		using (SolidBrush b = new SolidBrush(L)) g.FillPolygon(b, top);
		R(g, u0, v0, u1, v0 + t * 0.38f, D);         // 처마 밑 그늘
	}


	// 무너진 쪽 사선 (왼쪽 경계): v 높이에서의 u
	private static float DL(float vv) { return -0.37f + (vv - 0.50f) * 0.40f; }

	// 사선으로 잘린 사각형을 안티앨리어싱으로 채움
	private static void RP(Graphics g, float u0, float v0, float u1, float v1, Color c)
	{
		float a0 = Math.Max(u0, DL(v0)), a1 = Math.Max(u0, DL(v1));
		if (a0 >= u1 && a1 >= u1) return;
		a0 = Math.Min(a0, u1); a1 = Math.Min(a1, u1);
		PointF[] p = { new PointF(cx + a0 * h, by - v0 * h), new PointF(cx + u1 * h, by - v0 * h), new PointF(cx + u1 * h, by - v1 * h), new PointF(cx + a1 * h, by - v1 * h) };
		using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, p);
	}

	private static void SlabCut(Graphics g, float u0, float u1, float v0, float v1, Color L, Color D)
	{
		float t = v1 - v0;
		RP(g, u0, v0, u1, v0 + t * 0.38f, D);
		RP(g, u0, v0 + t * 0.38f, u1, v1, L);
	}

	public static void Draw(Graphics g, float cxx, float byy, float hh, Color L, Color D)
	{
		cx = cxx; by = byy; h = hh;
		SmoothingMode sm = g.SmoothingMode;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		Color mid = Theme.Mix(L, D, 0.5);
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(55, 6, 10, 36))) g.FillEllipse(sh, cx - h * 0.56f, by - h * 0.022f, h * 1.12f, h * 0.055f);

		// 기단 + 앞 계단
		R(g, -0.52f, 0f, 0.52f, 0.055f, mid);
		R(g, -0.48f, 0.055f, 0.48f, 0.095f, L);
		R(g, -0.07f, 0f, 0.07f, 0.095f, L);
		R(g, -0.07f, 0.02f, 0.07f, 0.026f, mid); R(g, -0.07f, 0.05f, 0.07f, 0.056f, mid); R(g, -0.07f, 0.078f, 0.07f, 0.084f, mid);

		// 1층 몸돌: 기둥 9개 + 문 3곳
		R(g, -0.43f, 0.09f, 0.43f, 0.30f, mid);
		for (int k = 0; k <= 8; k++) { float u = -0.43f + k * 0.86f / 8f; R(g, u - 0.019f, 0.09f, u + 0.019f, 0.30f, L); }
		foreach (int k in new int[] { 1, 4, 6 }) { float u = -0.43f + k * 0.86f / 8f; R(g, u + 0.022f, 0.09f, u + 0.86f / 8f - 0.022f, 0.22f, D); }
		Slab(g, -0.53f, 0.53f, 0.30f, 0.345f, L, D, 0.012f);

		// 2층 몸돌
		R(g, -0.38f, 0.345f, 0.38f, 0.46f, mid);
		for (int k = 0; k <= 6; k++) { float u = -0.38f + k * 0.76f / 6f; R(g, u - 0.017f, 0.345f, u + 0.017f, 0.46f, L); }
		foreach (int k in new int[] { 1, 3, 4 }) { float u = -0.38f + k * 0.76f / 6f; R(g, u + 0.02f, 0.365f, u + 0.76f / 6f - 0.02f, 0.43f, D); }
		Slab(g, -0.47f, 0.47f, 0.46f, 0.50f, L, D, 0.01f);

		// 위쪽 4개 층: 오른쪽은 탑 원형 그대로(기둥·문), 왼쪽은 사선으로 무너진 벽돌 더미
		float[] bh = { 0.10f, 0.09f, 0.085f, 0.08f };
		float[] rr = { 0.36f, 0.33f, 0.30f, 0.26f };
		float band = 0.19f;
		float v = 0.50f;
		for (int i = 0; i < 4; i++)
		{
			float v1 = v + bh[i], r = rr[i];
			RP(g, -0.6f, v, r, v1, mid);                                            // 몸돌 바탕
			// 오른쪽: 원래 탑의 기둥 + 열린 칸
			float ox = DL(v1) + band;
			for (float u = r; u > ox; u -= 0.075f) RP(g, u - 0.022f, v, u, v1, L);
			for (float u = r - 0.075f; u - 0.05f > ox; u -= 0.15f) RP(g, u - 0.05f, v + bh[i] * 0.18f, u - 0.003f, v + bh[i] * 0.7f, D);
			// 왼쪽: 사선을 따라 쌓인 벽돌
			int nrows = (int)Math.Round(bh[i] / 0.024f);
			float ch = bh[i] / nrows;
			for (int j = 0; j < nrows; j++)
			{
				float rv0 = v + j * ch, rv1 = rv0 + ch;
				float bwd = 0.062f;
				float stag = (j + i) % 2 == 0 ? 0f : bwd * 0.5f;
				float ur = DL(rv1) + band;
				if (ur > r) ur = r;
				int cnt = 0;
				for (float u0 = ur + stag; u0 > DL(rv0) - bwd; u0 -= bwd, cnt++)
				{
					float bl = u0 - bwd, brr = Math.Min(u0, ur);
					if (brr <= bl) continue;
					int hsh = (i * 31 + j * 17 + cnt * 7) % 5;
					Color bc = Theme.Mix(L, mid, hsh * 0.12);
					RP(g, bl + 0.003f, rv0 + 0.003f, brr, rv1, bc);                  // 줄눈이 보이도록 살짝 띄움
				}
			}
			RP(g, ox - 0.004f, v, ox + 0.004f, v1, mid);                              // 벽돌과 몸돌 경계
			// 지붕돌: 오른쪽은 길게, 왼쪽은 사선 쪽에서 살짝 튀어나온 끝
			float rl = DL(v1 + 0.016f) - 0.03f;
			SlabCut(g, rl, r + 0.065f, v1, v1 + 0.034f, L, D);
			v = v1 + 0.034f;
		}
		g.SmoothingMode = sm;
	}
}

internal static class RectExt { internal static Rectangle ToRect(this RectangleF r) { return Rectangle.Round(r); } }
