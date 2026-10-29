using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// 마룡이 키우기 (다마고치·디지몬 방식): 배부름·기분·청결·체력이 시간에 따라 줄고, 돌본 방식에 따라 자라면서 모습(칭호)이 달라져요
internal static class Raise
{
	private static string P { get { return Path.Combine(Program.DataDir, "raise.txt"); } }
	public static float Hunger = 80f, Fun = 80f, Clean = 90f, Energy = 90f; public static int Poop, Stage, Form = -1, CareToday; public static bool Sick;
	private static DateTime born = DateTime.Now, last = DateTime.Now; private static float cF, cP, cC, cR, neglect, sickAcc; private static bool loaded; private static string careDay = "";
	public static string Pending;   // 펫이 대신 말해 줄 소식 (진화 등)
	public static readonly string[] StageNames = { "아기 마룡", "꼬마 마룡", "청소년 마룡", "어른 마룡", "전설의 마룡" };
	public static readonly string[] FormNames = { "모범 공무원 마룡", "먹보 마룡", "장난꾸러기 마룡", "깔끔쟁이 마룡", "낮잠왕 마룡", "야근에 지친 마룡" };
	public static readonly string[] FormDesc = { "골고루 잘 돌봐서 균형 잡힌 모범생이 됐어요", "밥을 많이 챙겨 줘서 든든하게 자랐어요", "많이 놀아 줘서 장난기가 가득해요", "자주 씻겨 줘서 반짝반짝해요", "푹 재워 줘서 여유가 넘쳐요", "돌봄이 부족했어요… 지금부터 잘 챙겨 주세요" };
	public static double AgeDays { get { Load(); return Math.Max(0, (DateTime.Now - born).TotalDays); } }
	public static string Title { get { Load(); string s = StageNames[Stage]; return Stage >= 3 && Form >= 0 ? (Stage == 4 ? "전설의 " : "") + FormNames[Form] : s; } }
	private static void Load()
	{
		if (loaded) return; loaded = true;
		try
		{
			if (!File.Exists(P)) { born = DateTime.Now; last = born; Save(); return; }
			string[] a = File.ReadAllText(P).Trim().Split('|'); int i = 0;
			born = new DateTime(long.Parse(a[i++])); Hunger = F(a[i++]); Fun = F(a[i++]); Clean = F(a[i++]); Energy = F(a[i++]); Poop = int.Parse(a[i++]); Sick = a[i++] == "1";
			cF = F(a[i++]); cP = F(a[i++]); cC = F(a[i++]); cR = F(a[i++]); neglect = F(a[i++]); last = new DateTime(long.Parse(a[i++])); Stage = int.Parse(a[i++]); Form = int.Parse(a[i++]);
			if (a.Length > i + 1) { careDay = a[i++]; CareToday = int.Parse(a[i++]); }
		}
		catch { born = DateTime.Now; last = born; }
		// 앱이 꺼져 있던 동안: 잠들어 있었던 걸로 보고 절반 속도로, 최대 10시간만 반영해요
		double off = Math.Min(10.0, Math.Max(0.0, (DateTime.Now - last).TotalHours)); if (off > 0.02) Decay((float)off, 0.5f); last = DateTime.Now;
	}
	private static float F(string s) { float v; return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f; }
	private static string N(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }
	public static void Save()
	{
		try
		{
			Directory.CreateDirectory(Program.DataDir);
			File.WriteAllText(P, string.Join("|", new string[] { born.Ticks.ToString(), N(Hunger), N(Fun), N(Clean), N(Energy), Poop.ToString(), Sick ? "1" : "0", N(cF), N(cP), N(cC), N(cR), N(neglect), last.Ticks.ToString(), Stage.ToString(), Form.ToString(), careDay, CareToday.ToString() }));
		}
		catch { }
	}
	private static float C(float v) { return v < 0f ? 0f : (v > 100f ? 100f : v); }
	private static void Decay(float h, float k)
	{
		Hunger = C(Hunger - 7f * h * k); Fun = C(Fun - (Sick ? 10f : 5.5f) * h * k); Clean = C(Clean - (3f + 4f * Poop) * h * k); Energy = C(Energy - 4f * h * k);
		if (Hunger < 15f || Fun < 15f || Clean < 15f || Energy < 15f) neglect += h;
		if (Clean < 15f || Hunger < 8f) { sickAcc += h; if (sickAcc > 1f && !Sick) { Sick = true; Pending = "몸이 안 좋아요… 약을 주세요 (오른쪽 클릭 → 마룡이 돌보기)"; } } else sickAcc = Math.Max(0f, sickAcc - h);
	}
	private static double nextTick; private static readonly System.Diagnostics.Stopwatch clk = System.Diagnostics.Stopwatch.StartNew();
	// 펫이나 돌보기 창이 켜져 있는 동안 자주 불러도 30초에 한 번만 계산해요
	public static void Tick(double unused)
	{
		Load(); double now = clk.Elapsed.TotalSeconds; if (now < nextTick) return; nextTick = now + 30.0;
		DateTime n = DateTime.Now; float h = (float)Math.Min(1.0, (n - last).TotalHours); last = n; Decay(h, 1f);
		if (Hunger > 40f && Poop < 3 && new Random().NextDouble() < 30.0 / (3.2 * 3600.0)) { Poop++; if (Pending == null) Pending = "앗… 실수했어요. 치워 주세요! (마룡이 돌보기 → 청소)"; }
		int st = AgeDays < 1 ? 0 : (AgeDays < 3 ? 1 : (AgeDays < 7 ? 2 : (AgeDays < 14 ? 3 : 4)));
		if (st > Stage)
		{
			Stage = st; if (Stage >= 3) Form = PickForm();
			Pending = "진화했어요! 이제 '" + Title + "'예요!" + (Stage >= 3 ? " " + FormDesc[Form] : "");
		}
		Save();
	}
	private static int PickForm()
	{
		if (neglect > 30f) return 5;
		float mx = Math.Max(Math.Max(cF, cP), Math.Max(cC, cR)), mn = Math.Min(Math.Min(cF, cP), Math.Min(cC, cR));
		if (mx < 3f || mx <= (mn + 1f) * 1.7f) return 0;
		return mx == cF ? 1 : (mx == cP ? 2 : (mx == cC ? 3 : 4));
	}
	// 지금 가장 급한 부탁 (없으면 null)
	public static string Need()
	{
		Load();
		if (Sick) return "몸이 안 좋아요… 약 주세요";
		if (Poop > 0 && Clean < 50f) return "주변이 지저분해요, 치워 줄래요?";
		if (Hunger < 25f) return "배고파요… 밥 주세요";
		if (Energy < 20f) return "졸려요… 방석 깔아 줄래요?";
		if (Fun < 25f) return "심심해요! 놀아 주세요";
		if (Clean < 25f) return "꼬질꼬질해요, 씻겨 주세요";
		return null;
	}
	// 0 밥 · 1 놀기 · 2 씻기 · 3 재우기 · 4 청소 · 5 약
	public static string Do(int kind)
	{
		Load(); string today = DateTime.Now.ToString("yyyyMMdd"); if (careDay != today) { careDay = today; CareToday = 0; }
		string r;
		switch (kind)
		{
			case 0: if (Hunger > 92f) { r = "배불러요, 이따 먹을게요"; break; } Hunger = C(Hunger + 35f); Energy = C(Energy + 4f); cF += 1f; r = "냠냠, 맛있어요!"; break;
			case 1: if (Energy < 10f) { r = "너무 졸려서 못 놀겠어요…"; break; } Fun = C(Fun + 30f); Energy = C(Energy - 8f); Hunger = C(Hunger - 5f); cP += 1f; r = "신난다! 또 놀아요!"; break;
			case 2: Clean = C(Clean + 45f); Fun = C(Fun - 3f); cC += 1f; r = "뽀득뽀득~ 깨끗해졌어요"; break;
			case 3: Energy = C(Energy + 45f); cR += 1f; r = "쿨쿨… 푹 잤어요"; break;
			case 4: if (Poop == 0) { r = "치울 게 없어요!"; break; } Poop = 0; Clean = C(Clean + 15f); cC += 0.5f; r = "치워 줘서 고마워요!"; break;
			default: if (!Sick) { r = "아픈 데 없어요, 건강해요!"; break; } Sick = false; sickAcc = 0f; Fun = C(Fun - 5f); r = "약 먹었더니 다 나았어요!"; break;
		}
		CareToday++; if (CareToday <= 12) Bond.Add(1); Save(); return r;
	}
	// 놀이 도구를 주면 같은 효과가 조금 생겨요
	public static void Toy(int kind) { Load(); if (kind == 0) { Hunger = C(Hunger + 20f); cF += 0.5f; } else if (kind == 1) { Fun = C(Fun + 15f); Energy = C(Energy - 3f); cP += 0.5f; } else if (kind == 3) { Energy = C(Energy + 25f); cR += 0.5f; } else { Fun = C(Fun + 6f); } Save(); }
	public static void Rub() { Load(); Fun = C(Fun + 0.6f); }
}

// "마룡이 돌보기" 창: 상태 막대 + 돌보기 버튼
internal sealed class RaiseForm : Form
{
	private static RaiseForm cur;
	public static void Open() { try { if (cur != null && !cur.IsDisposed) { cur.Activate(); return; } cur = new RaiseForm(); cur.Show(); } catch (Exception ex) { cur = null; Program.LogErr(ex); } }
	private readonly float S; private readonly Timer tm = new Timer(); private string msg = ""; private double msgT; private int hover = -1, down = -1; private readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
	private static readonly string[] Btn = { "밥 주기", "놀아 주기", "씻기기", "재우기", "청소하기", "약 주기" };
	private const int LW = 340, LH = 512;
	private RaiseForm()
	{
		S = Dpi.Scale; Text = "마룡이 돌보기"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = true; StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = true; TopMost = true;
		ClientSize = new Size((int)(LW * S), (int)(LH * S)); BackColor = Theme.Page;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
		tm.Interval = 250; tm.Tick += delegate { Raise.Tick(0); Invalidate(); }; tm.Start();
		MouseMove += delegate(object o, MouseEventArgs e) { int h = Hit(e.X / S, e.Y / S); if (h != hover) { hover = h; Invalidate(); } Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; };
		MouseLeave += delegate { hover = -1; Invalidate(); };
		MouseDown += delegate(object o, MouseEventArgs e) { down = Hit(e.X / S, e.Y / S); Invalidate(); };
		MouseUp += delegate(object o, MouseEventArgs e)
		{
			int h = Hit(e.X / S, e.Y / S); if (h >= 0 && h == down) { msg = Raise.Do(h); msgT = sw.Elapsed.TotalSeconds + 3.0; PetWin.Care(h); Sfx.Play("ok"); }
			down = -1; Invalidate();
		};
		FormClosed += delegate { tm.Stop(); cur = null; };
		try { Icon = Program.AppIcon; } catch { }
	}
	private RectangleF BtnR(int i) { float bw = (LW - 40f - 10f) / 2f; return new RectangleF(20f + (i % 2) * (bw + 10f), 308f + (i / 2) * 46f, bw, 38f); }
	private int Hit(float x, float y) { for (int i = 0; i < 6; i++) if (BtnR(i).Contains(x, y)) return i; return -1; }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.ScaleTransform(S, S);
		using (SolidBrush b = new SolidBrush(Theme.Page)) g.FillRectangle(b, 0, 0, LW, LH);
		// 머리: 마룡이 얼굴 + 이름·단계
		using (GraphicsPath gp = Gfx.Round(new RectangleF(16, 14, LW - 32, 104), 18)) { using (SolidBrush b = new SolidBrush(Theme.Raised)) g.FillPath(b, gp); using (Pen p = new Pen(Theme.Line, 1.2f)) g.DrawPath(p, gp); }
		Image m = Program.MascotImage(); float bob = (float)Math.Sin(sw.Elapsed.TotalSeconds * 2.2) * 2.5f;
		if (m != null) { float h = 80f * (0.82f + 0.06f * Raise.Stage), w = h * m.Width / m.Height; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(m, 64f - w / 2f, 66f - h / 2f + bob, w, h); }
		if (Raise.Sick) Gfx.Text(g, "아파요", Theme.F(8.5f, 2), Theme.Red, new RectangleF(30, 96, 70, 18), StringAlignment.Center, StringAlignment.Center, false);
		Gfx.Text(g, Raise.Title, Theme.F(13f, 2), Theme.Ink, new RectangleF(116, 24, LW - 136, 28), StringAlignment.Near, StringAlignment.Center, false);
		int days = (int)Raise.AgeDays; Gfx.Text(g, "함께한 지 " + (days + 1) + "일째 · " + Raise.StageNames[Raise.Stage], Theme.F(9f, 0), Theme.Sub, new RectangleF(116, 52, LW - 136, 20), StringAlignment.Near, StringAlignment.Center, false);
		string nx = Raise.Stage == 0 ? "1일째에 꼬마로 자라요" : (Raise.Stage == 1 ? "3일째에 청소년이 돼요" : (Raise.Stage == 2 ? "7일째 진화! 돌본 방식에 따라 모습이 달라져요" : (Raise.Stage == 3 ? "14일째에 전설이 돼요" : "최고 단계예요!")));
		Gfx.Text(g, nx, Theme.F(8.5f, 0), Theme.Muted, new RectangleF(116, 72, LW - 136, 40), StringAlignment.Near, StringAlignment.Near, true);
		// 상태 막대
		string[] nm = { "배부름", "기분", "청결", "체력" }; float[] v = { Raise.Hunger, Raise.Fun, Raise.Clean, Raise.Energy };
		Color[] col = { Color.FromArgb(240, 150, 60), Color.FromArgb(236, 96, 140), Color.FromArgb(60, 160, 230), Color.FromArgb(70, 180, 110) };
		for (int i = 0; i < 4; i++)
		{
			float y = 134f + i * 34f;
			Gfx.Text(g, nm[i], Theme.F(9.5f, 1), Theme.Ink, new RectangleF(22, y, 60, 24), StringAlignment.Near, StringAlignment.Center, false);
			RectangleF bar = new RectangleF(84, y + 6, LW - 84 - 66, 12);
			using (GraphicsPath gp = Gfx.Round(bar, 6)) using (SolidBrush b = new SolidBrush(Theme.Line)) g.FillPath(b, gp);
			float fr = Math.Max(0.04f, v[i] / 100f); Color c = v[i] < 25f ? Theme.Red : col[i];
			using (GraphicsPath gp = Gfx.Round(new RectangleF(bar.X, bar.Y, bar.Width * fr, bar.Height), 6)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, gp);
			Gfx.Text(g, ((int)v[i]) + "%", Theme.F(9f, 1), v[i] < 25f ? Theme.Red : Theme.Sub, new RectangleF(LW - 64, y, 44, 24), StringAlignment.Far, StringAlignment.Center, false);
		}
		// 똥 (다마고치처럼)
		float py = 282f; Gfx.Text(g, Raise.Poop > 0 ? "치울 것 " + Raise.Poop + "개" : "주변이 깨끗해요", Theme.F(9f, 1), Raise.Poop > 0 ? Theme.Amber : Theme.Muted, new RectangleF(22, py - 9, 120, 20), StringAlignment.Near, StringAlignment.Center, false);
		for (int i = 0; i < Raise.Poop; i++) Poo(g, 150f + i * 26f, py + 2f);
		// 버튼
		for (int i = 0; i < 6; i++)
		{
			RectangleF r = BtnR(i); bool dim = (i == 5 && !Raise.Sick) || (i == 4 && Raise.Poop == 0);
			Color bc = down == i ? Theme.AccentDown : (hover == i ? Theme.AccentHover : Theme.AccentBtn); if (dim) bc = Theme.Mix(Theme.Line, Theme.Page, 0.2);
			using (GraphicsPath gp = Gfx.Round(r, 12)) using (SolidBrush b = new SolidBrush(bc)) g.FillPath(b, gp);
			Gfx.Text(g, Btn[i], Theme.F(10.5f, 2), dim ? Theme.Muted : Color.White, r, StringAlignment.Center, StringAlignment.Center, false);
		}
		// 말풍선 · 도움말
		string say = sw.Elapsed.TotalSeconds < msgT ? msg : (Raise.Need() ?? "기분 좋아요! 오늘도 같이 힘내요");
		using (GraphicsPath gp = Gfx.Round(new RectangleF(20, 462, LW - 40, 32), 16)) using (SolidBrush b = new SolidBrush(Theme.AccentSoft)) g.FillPath(b, gp);
		Gfx.Text(g, say, Theme.F(9.5f, 1), Theme.Accent, new RectangleF(24, 462, LW - 48, 32), StringAlignment.Center, StringAlignment.Center, false);
	}
	private static void Poo(Graphics g, float x, float y)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 100, 60))) { g.FillEllipse(b, x - 10, y - 2, 20, 9); g.FillEllipse(b, x - 7, y - 7, 14, 8); g.FillEllipse(b, x - 4, y - 11, 8, 7); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, x - 5, y - 6, 4, 3);
	}
}
