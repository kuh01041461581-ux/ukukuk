using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// ── 6. 10초 맞추기 ──────────────────────────────────────────
internal sealed class TimeGame : MiniGame
{
	private static readonly int[] Targets = { 5, 8, 10 };
	private int round, phase, streak; private double run, resT; private long t0; private float err; private int got; private string last = "", grade = "";
	public override string Costume { get { return "정장"; } }
	public override string Name { get { return "시간 맞추기"; } }
	public override string Key { get { return "time"; } }
	public override string Desc { get { return "속으로 세고 딱 맞춰 멈추기"; } }
	public override string Help { get { return "목표 시간이 되면 클릭(스페이스)해서 멈춰요\n5초·8초는 처음 2초만 숫자가 보이고, 10초는 시작하자마자 숫자가 사라져요!\n±0.05초 안이면 PERFECT · 연속이면 보너스가 커져요"; } }
	public override string RightText { get { return Math.Min(round + 1, 3) + " / 3 판"; } }
	public override void Reset() { round = 0; phase = 0; Score = 0; Over = false; run = 0; last = ""; grade = ""; streak = 0; }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (Pen p = new Pen(Color.White, 3f)) { g.DrawEllipse(p, cx - 17, cy - 15, 34, 34); g.DrawLine(p, cx, cy + 2, cx, cy - 9); g.DrawLine(p, cx, cy + 2, cx + 8, cy + 6); }
	}
	private double ShowT { get { return Targets[Math.Min(round, 2)] >= 10 ? 0.0 : 2.0; } }   // 10초 판은 시작하자마자 숫자를 숨겨요
	private void Act()
	{
		if (Over) return;
		if (phase == 0) { phase = 1; run = 0; t0 = System.Diagnostics.Stopwatch.GetTimestamp(); Sfx.Play("ui_click"); }
		else if (phase == 1)
		{
			run = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) / (double)System.Diagnostics.Stopwatch.Frequency; err = (float)(run - Targets[round]); got = Math.Max(0, 100 - (int)Math.Round(Math.Abs(err) * 100)); float ae = Math.Abs(err); if (ae < 0.05f) { grade = "PERFECT!"; streak++; got += 50 + 25 * Math.Min(streak - 1, 3); Shake = 0.6f; Fx.Burst(W / 2f, 330f, Color.FromArgb(255, 214, 80), 16); } else if (ae < 0.2f) { grade = "GREAT"; streak = 0; Fx.Burst(W / 2f, 330f, Color.FromArgb(120, 230, 200), 8); } else if (ae < 0.5f) { grade = "GOOD"; streak = 0; } else { grade = "아쉬워요"; streak = 0; } Score += got; phase = 2; resT = 0;
			last = run.ToString("0.00") + "초"; Fx.Burst(W / 2f, 330f, grade == "PERFECT!" ? Color.FromArgb(255, 214, 90) : (got >= 80 ? Color.FromArgb(150, 230, 200) : Color.FromArgb(180, 180, 220)), grade == "PERFECT!" ? 24 : 8); if (grade == "PERFECT!") Shake = 0.3f; Sfx.Play(grade == "PERFECT!" ? "fanfare" : (got >= 80 ? "ok" : "tick"));
		}
		else if (phase == 2 && resT > 0.5) { NextRound(); }
	}
	private void NextRound() { round++; if (round >= 3) Over = true; else phase = 0; }
	public override void Click(float x, float y) { Act(); }
	public override void Press(Keys k) { if (k == Keys.Space || k == Keys.Enter) Act(); }
	public override void Update(float dt, GameInput inp, double t)
	{
		if (phase == 1) run = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) / (double)System.Diagnostics.Stopwatch.Frequency;
		else if (phase == 2) { resT += dt; if (resT > 2.6) NextRound(); }
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(16, 20, 56), Color.FromArgb(48, 36, 100)); Twinkle(g, t);
		int target = Targets[Math.Min(round, 2)];
		Txt(g, "목표", 12f, 1, Color.FromArgb(180, 255, 255, 255), 0, 130, W, 24, StringAlignment.Center);
		Txt(g, target + "초", 40f, 2, Color.FromArgb(255, 214, 90), 0, 154, W, 64, StringAlignment.Center);
		{ string[] tn = { "ms_tm3", "ms_tm1", "ms_tm2" }; int ri = Math.Min(round, 2); if (Spr.Get(tn[ri]) != null) { float ih = ri == 2 ? 70f : 62f; Bitmap tb = Spr.Get(tn[ri]); Spr.Draw(g, tn[ri], W / 2f - 100f, 168f, ih * tb.Width / tb.Height, ih, (float)Math.Sin(t * 2.2 + ri) * 5f); Spr.Draw(g, tn[ri], W / 2f + 100f, 168f, -ih * tb.Width / tb.Height, ih, (float)Math.Sin(t * 2.2 + ri + 1) * 5f); } }
		float cx = W / 2f, cy = 330, R = 100;
		if (phase == 2 && grade == "PERFECT!") using (Pen rp = new Pen(Color.FromArgb(46, 255, 214, 90), 5f)) for (int rk = 0; rk < 16; rk++) { double an = rk * Math.PI / 8 + t * 0.4; g.DrawLine(rp, cx + (float)Math.Cos(an) * (R + 22), cy + (float)Math.Sin(an) * (R + 22), cx + (float)Math.Cos(an) * (R + 90), cy + (float)Math.Sin(an) * (R + 90)); }
		if (phase == 2 && grade == "PERFECT!") { float pr = 1f + 0.04f * (float)Math.Sin(t * 6); DrawFx(g, "tm4", cx, cy, 300f * pr, 0.95f); }
		if (phase == 2 && got < 50) { DrawFx(g, "tm5", cx + 82f, cy - 96f, 70f, 1f); }
		using (SolidBrush fb = new SolidBrush(Color.FromArgb(70, 10, 10, 40))) g.FillEllipse(fb, cx - R - 14, cy - R - 14, (R + 14) * 2, (R + 14) * 2);
		using (Pen bz = new Pen(Color.FromArgb(120, 190, 180, 255), 3f)) g.DrawEllipse(bz, cx - R - 14, cy - R - 14, (R + 14) * 2, (R + 14) * 2);
		using (Pen tk = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f)) for (int k = 0; k < 60; k++) { double an = k * Math.PI / 30 - Math.PI / 2; float r1 = R + 16 - (k % 5 == 0 ? 8 : 0); float r0 = R + 4; g.DrawLine(tk, cx + (float)Math.Cos(an) * (R + 11), cy + (float)Math.Sin(an) * (R + 11), cx + (float)Math.Cos(an) * (R + (k % 5 == 0 ? 1 : 6)), cy + (float)Math.Sin(an) * (R + (k % 5 == 0 ? 1 : 6))); }
		{ Color ringC = phase == 2 ? (grade == "PERFECT!" ? Color.FromArgb(255, 214, 90) : (got >= 80 ? Color.FromArgb(130, 230, 190) : Color.FromArgb(160, 160, 210))) : Color.White; using (Pen p = new Pen(Color.FromArgb(phase == 2 ? 120 : 50, ringC), 10f)) g.DrawEllipse(p, cx - R, cy - R, R * 2, R * 2); }
		if (phase == 1 && run < ShowT) { double an = run / target * Math.PI * 2 - Math.PI / 2; using (SolidBrush hb = new SolidBrush(Color.FromArgb(80, 140, 240, 220))) g.FillEllipse(hb, cx + (float)Math.Cos(an) * R - 12, cy + (float)Math.Sin(an) * R - 12, 24, 24); using (SolidBrush hb = new SolidBrush(Color.White)) g.FillEllipse(hb, cx + (float)Math.Cos(an) * R - 6, cy + (float)Math.Sin(an) * R - 6, 12, 12); }
		if (phase == 1 && run < ShowT) using (Pen p = new Pen(Color.FromArgb(220, 120, 220, 200), 10f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, cx - R, cy - R, R * 2, R * 2, -90, (float)(run / target * 360));
		if (phase == 1)
		{
			if (run < ShowT) Txt(g, run.ToString("0.00"), 34f, 2, Color.White, cx - 90, cy - 30, 180, 60, StringAlignment.Center);
			else { float pulse = 0.5f + 0.5f * (float)Math.Sin(t * 5); using (Pen p = new Pen(Color.FromArgb((int)(40 + 60 * pulse), 255, 255, 255), 4f)) g.DrawEllipse(p, cx - R - 8, cy - R - 8, R * 2 + 16, R * 2 + 16); Txt(g, "?", 44f, 2, Color.White, cx - 60, cy - 36, 120, 72, StringAlignment.Center); }
		}
		else if (phase == 2)
		{
			Txt(g, last, 30f, 2, Color.White, cx - 100, cy - 44, 200, 50, StringAlignment.Center);
			Txt(g, (err >= 0 ? "+" : "") + err.ToString("0.00") + "초 차이", 12f, 1, Color.FromArgb(220, 255, 255, 255), cx - 100, cy + 4, 200, 24, StringAlignment.Center);
			Txt(g, "+" + got + "점", 20f, 2, got >= 90 ? Color.FromArgb(255, 214, 90) : Color.White, cx - 100, cy + 34, 200, 36, StringAlignment.Center); Txt(g, grade + (streak >= 2 ? "  " + streak + "연속!" : ""), 22f, 2, grade == "PERFECT!" ? Color.FromArgb(255, 214, 90) : Color.FromArgb(200, 230, 255), 0, 236, W, 36, StringAlignment.Center);
		}
		else Txt(g, "준비되면 클릭!", 16f, 2, Color.FromArgb(230, 255, 255, 255), cx - 110, cy - 20, 220, 40, StringAlignment.Center);
		Txt(g, phase == 0 ? "클릭하면 시작" : (phase == 1 ? "목표 시간에 클릭!" : "클릭하면 다음 판"), 11f, 0, Color.FromArgb(170, 255, 255, 255), 0, 470, W, 24, StringAlignment.Center);
		DrawMascot(g, 70, H - 24, 70, (float)Math.Sin(t * 3) * 3);
	}
}

// ── 7. 카드 짝맞추기 ────────────────────────────────────────
internal sealed class MemoryGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private static KeyValuePair<string, Image>[] pool; private Image[] face = new Image[8]; private int[] sym = new int[16]; private int[] st = new int[16]; private float[] flip = new float[16];
	private int first = -1, second = -1, moves, matched, combo; private double time, wrong, comboT; private bool started, shuf;
	public override string Name { get { return "카드 짝맞추기"; } }
	public override string Key { get { return "memory"; } }
	public override string Desc { get { return "여러 마룡이 8쌍 찾기"; } }
	public override string Help { get { return "카드를 두 장씩 뒤집어 같은 옷의 마룡이를 찾아요\n연속으로 맞히면 콤보 점수가 커져요 (틀리면 콤보 끊김)\n카드 위치는 끝까지 그대로예요 · 기억력이 승부!\n적게 뒤집고 빨리 끝낼수록 마지막 보너스!"; } }
	public override string RightText { get { return moves + "번 · " + (int)time + "초"; } }
	public override void Reset()
	{
		Score = 0; Over = false; first = second = -1; moves = 0; matched = 0; time = 0; wrong = 0; started = false; shuf = false; combo = 0; comboT = 0;
		if (pool == null) pool = Program.GameFaces(16);
		{ List<int> ix = new List<int>(); for (int i = 0; i < pool.Length; i++) ix.Add(i); for (int i = ix.Count - 1; i > 0; i--) { int j2 = Rnd.Next(i + 1); int t2 = ix[i]; ix[i] = ix[j2]; ix[j2] = t2; } for (int i = 0; i < 8; i++) face[i] = pool[ix[i]].Value; }
		List<int> l = new List<int>(); for (int i = 0; i < 8; i++) { l.Add(i); l.Add(i); }
		for (int i = l.Count - 1; i > 0; i--) { int j = Rnd.Next(i + 1); int tmp = l[i]; l[i] = l[j]; l[j] = tmp; }
		for (int i = 0; i < 16; i++) { sym[i] = l[i]; st[i] = 0; flip[i] = 0; }
	}
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 255))) { g.FillRectangle(b, cx - 20, cy - 14, 18, 26); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 190, 255))) { g.FillRectangle(b, cx + 2, cy - 14, 18, 26); }
		StarShape(g, cx - 11, cy - 1, 6, Color.FromArgb(255, 190, 40));
	}
	private RectangleF Cell(int i) { return new RectangleF(25 + (i % 4) * 90, 120 + (i / 4) * 100, 80, 90); }
	public override void Click(float x, float y)
	{
		if (Over) return;
		if (wrong > 0) { st[first] = 0; st[second] = 0; first = second = -1; wrong = 0; }   // 틀린 카드가 보이는 동안 눌러도 바로 이어서 뒤집혀요
		for (int i = 0; i < 16; i++) if (st[i] == 0 && Cell(i).Contains(x, y))
		{
			started = true; st[i] = 1; Sfx.Play("flip");
			if (first < 0) first = i;
			else { second = i; moves++; if (sym[first] == sym[second]) { st[first] = st[second] = 2; matched++; combo++; comboT = 1.4; Score += 40 * Math.Min(combo, 5); Sfx.Play(combo >= 3 ? "ok2" : "ok"); { RectangleF c1 = Cell(first), c2 = Cell(second); Color fc = combo >= 3 ? Color.FromArgb(255, 214, 80) : Color.FromArgb(120, 230, 200); Fx.Burst(c1.X + c1.Width / 2, c1.Y + c1.Height / 2, fc, 7); Fx.Burst(c2.X + c2.Width / 2, c2.Y + c2.Height / 2, fc, 7); if (combo >= 3) Shake = 0.25f; } first = second = -1; if (matched == 8) { Over = true; Score += Math.Max(10, 500 - moves * 12 - (int)time * 4); Shake = 0.5f; } } else { wrong = 0.8; combo = 0; Sfx.Play("bad2"); } }
			return;
		}
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (started && !Over) time += dt; if (comboT > 0) comboT -= dt;
		for (int i = 0; i < 16; i++) { float tgt = st[i] == 0 ? 0f : 1f; flip[i] += Math.Sign(tgt - flip[i]) * Math.Min(Math.Abs(tgt - flip[i]), dt * 6f); }
		// (카드 위치는 한 번 정해지면 끝까지 그대로예요)
		if (wrong > 0) { wrong -= dt; if (wrong <= 0) { st[first] = 0; st[second] = 0; first = second = -1; wrong = 0; } }
	}
	private static void Sym(Graphics g, int s, float cx, float cy, float r, Image mas)
	{
		Color[] c = { Color.FromArgb(255, 196, 40), Color.FromArgb(240, 90, 110), Color.FromArgb(170, 180, 255), Color.White, Color.FromArgb(90, 210, 190), Color.FromArgb(255, 150, 70), Color.FromArgb(180, 120, 240), Color.FromArgb(110, 190, 255) };
		using (SolidBrush b = new SolidBrush(c[s % 8]))
		{
			if (s == 0) { using (GraphicsPath p = Gfx.Star(cx, cy, r, r * 0.48f)) g.FillPath(b, p); }
			else if (s == 1) { using (GraphicsPath p = new GraphicsPath()) { p.AddBezier(cx, cy + r, cx - r * 1.5f, cy - r * 0.2f, cx - r * 0.6f, cy - r * 1.2f, cx, cy - r * 0.4f); p.AddBezier(cx, cy - r * 0.4f, cx + r * 0.6f, cy - r * 1.2f, cx + r * 1.5f, cy - r * 0.2f, cx, cy + r); g.FillPath(b, p); } }
			else if (s == 2) { g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2); using (SolidBrush k = new SolidBrush(Color.FromArgb(40, 44, 90))) g.FillEllipse(k, cx - r * 0.4f, cy - r * 1.1f, r * 2, r * 2); }
			else if (s == 3 && mas != null) { float h = r * 2.3f, w = h * mas.Width / mas.Height; g.DrawImage(mas, cx - w / 2, cy - h / 2, w, h); }
			else if (s == 4) { g.FillPolygon(b, new PointF[] { new PointF(cx, cy - r), new PointF(cx + r * 0.8f, cy), new PointF(cx, cy + r), new PointF(cx - r * 0.8f, cy) }); }
			else if (s == 5) { using (Pen p = new Pen(c[5], r * 0.4f)) g.DrawEllipse(p, cx - r * 0.8f, cy - r * 0.8f, r * 1.6f, r * 1.6f); }
			else if (s == 6) { g.FillPolygon(b, new PointF[] { new PointF(cx, cy - r), new PointF(cx + r, cy + r * 0.8f), new PointF(cx - r, cy + r * 0.8f) }); }
			else { using (GraphicsPath p = new GraphicsPath()) { p.AddBezier(cx, cy - r, cx + r * 1.2f, cy + r * 0.2f, cx + r * 0.8f, cy + r, cx, cy + r); p.AddBezier(cx, cy + r, cx - r * 0.8f, cy + r, cx - r * 1.2f, cy + r * 0.2f, cx, cy - r); g.FillPath(b, p); } }
		}
	}
	private Bitmap backBmp; private float backK, backW;
	private Bitmap BackBmp(float w, float h, float k)
	{
		if (backBmp != null && Math.Abs(backK - k) < 0.01f && Math.Abs(backW - w) < 0.5f) return backBmp;
		if (backBmp != null) backBmp.Dispose();
		int pw = Math.Max(8, (int)Math.Ceiling(w * k)), ph = Math.Max(8, (int)Math.Ceiling(h * k)); backBmp = new Bitmap(pw, ph, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); backK = k; backW = w;
		using (Graphics g = Graphics.FromImage(backBmp))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(pw / w, ph / h); RectangleF rr = new RectangleF(0, 0, w, h);
			Bitmap cb = Spr.Get("ms_mem"); if (cb != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(cb, 0, 0, w, h); return backBmp; }
			using (GraphicsPath gp = Gfx.Round(new RectangleF(1f, 1f, w - 2f, h - 2f), 12))
			{
				using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, -0.5f, w, h + 1f), Color.FromArgb(20, 160, 146), Color.FromArgb(0, 104, 98), 90f)) g.FillPath(b, gp);
				GraphicsState gc = g.Save(); g.SetClip(gp); using (Pen lp = new Pen(Color.FromArgb(30, 255, 255, 255), 1.2f)) for (int q = -6; q <= 6; q++) { g.DrawLine(lp, q * 14 + w / 2, 0, q * 14 + w / 2 + h, h); g.DrawLine(lp, q * 14 + w / 2 + h, 0, q * 14 + w / 2, h); } g.Restore(gc);
				g.TranslateTransform(w / 2, h / 2); using (GraphicsPath sg = Gfx.Star(0, 0, 14, 6.5f)) using (SolidBrush sb2 = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillPath(sb2, sg); g.TranslateTransform(-w / 2, -h / 2);
				using (GraphicsPath hg = Gfx.Round(new RectangleF(5, 4, w * 0.55f, 9), 5)) using (SolidBrush hb2 = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillPath(hb2, hg);
				using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawPath(p, gp);
			}
		}
		return backBmp;
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(20, 28, 70), Color.FromArgb(52, 78, 140)); Twinkle(g, t);
		for (int i = 0; i < 16; i++)
		{
			RectangleF r = Cell(i); float f = flip[i]; float sx = Math.Abs((float)Math.Cos(f * Math.PI)); bool face = f > 0.5f;
			bool bad = wrong > 0 && (i == first || i == second); float shk = bad ? (float)Math.Sin(t * 55) * 3f : 0f, lift = (float)Math.Sin(f * Math.PI);
			using (GraphicsPath sp = Gfx.Round(new RectangleF(r.X + 3, r.Y + 6 + lift * 4, r.Width - 4, r.Height - 2), 12)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(50 - (int)(lift * 20), 0, 0, 20))) g.FillPath(sb, sp);
			GraphicsState gs = g.Save(); g.TranslateTransform(r.X + r.Width / 2 + shk, r.Y + r.Height / 2 - lift * 5f); g.ScaleTransform(Math.Max(0.04f, sx) * (1f + 0.05f * lift), 1f + 0.05f * lift);
			RectangleF rr = new RectangleF(-r.Width / 2, -r.Height / 2, r.Width, r.Height);
			using (GraphicsPath gp = Gfx.Round(rr, 12))
			{
				if (face) { using (SolidBrush b = new SolidBrush(st[i] == 2 ? Color.FromArgb(120, 70, 190, 150) : (bad ? Color.FromArgb(255, 232, 232) : Color.FromArgb(244, 246, 255)))) g.FillPath(b, gp); }
				else { Bitmap bk = BackBmp(rr.Width, rr.Height, ImgCache.Scale(g)); InterpolationMode im0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(bk, rr, new RectangleF(0, 0, bk.Width, bk.Height), GraphicsUnit.Pixel); g.InterpolationMode = im0; }   // 카드 뒷면은 한 번만 그려 두고 재사용
				if (st[i] == 2) using (Pen mp = new Pen(Color.FromArgb(90 + (int)(60 * Math.Sin(t * 4 + i)), 255, 220, 100), 3f)) g.DrawPath(mp, gp);
				if (face) using (Pen p = new Pen(Color.FromArgb(80, 255, 255, 255), 2f)) g.DrawPath(p, gp);
			}
			if (face) { Image fi = this.face[sym[i] % 8]; if (fi != null) { float hh = r.Height - 14, ww = hh * fi.Width / fi.Height; if (ww > r.Width - 8) { ww = r.Width - 8; hh = ww * fi.Height / fi.Width; } g.DrawImage(fi, -ww / 2, -hh / 2, ww, hh); } else Sym(g, sym[i], 0, 0, 20, Mascot); }
			g.Restore(gs);
		}
		if (combo >= 2 && comboT > 0) Txt(g, combo + " 콤보!  +" + 40 * Math.Min(combo, 5), 17f, 2, Color.FromArgb((int)(255 * Math.Min(1.0, comboT)), 255, 214, 90), 0, 76, W, 32, StringAlignment.Center); Txt(g, "카드를 눌러 뒤집어요", 11f, 0, Color.FromArgb(160, 255, 255, 255), 0, 532, W, 22, StringAlignment.Center);
	}
}

// ── 8. 2048 ────────────────────────────────────────────────
// 규칙: 방향을 누르면 모든 타일이 그쪽 끝까지 미끄러져요. 부딪힌 같은 숫자 두 개는 하나로 합쳐져요(2+2=4). 한 번 밀 때 한 번만 합쳐져요.
internal sealed class Game2048 : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class Tl { public int V; public float X, Y, Appear = 0f, Bump = 0f; public int R, C; public Tl Into; }   // X,Y: 칸 단위로 보이는 위치
	private readonly List<Tl> tiles = new List<Tl>(); private Tl[,] grid = new Tl[4, 4]; private int moves, best, undos, snapScore; private int[] snap; private float shake;
	public override string Costume { get { return "g_wizard"; } }
	public override string Name { get { return "2048"; } }
	public override string Key { get { return "g2048"; } }
	public override string Desc { get { return "같은 숫자를 합쳐 2048!"; } }
	public override string Help { get { return "방향키(또는 마우스로 밀고 싶은 쪽으로 끌기)로\n모든 숫자 타일을 한쪽으로 밀어요\n같은 숫자 두 개가 부딪히면 하나로 합쳐져요 (2+2=4)\n계속 합쳐서 2048을 만들어요!"; } }
	public override string RightText { get { return "최고 " + best; } }
	public override void Swipe(float dx, float dy) { if (Math.Abs(dx) > Math.Abs(dy)) Press(dx < 0 ? Keys.Left : Keys.Right); else Press(dy < 0 ? Keys.Up : Keys.Down); }
	public override void Reset() { tiles.Clear(); grid = new Tl[4, 4]; Score = 0; Over = false; moves = 0; best = 0; shake = 0; undos = 3; snap = null; Spawn(); Spawn(); }
	// 되돌리기: 방금 민 것을 취소해요 (점수도 같이 돌아가요 · 3번)
	private void Undo()
	{
		if (undos <= 0 || snap == null || Over) return;
		tiles.Clear(); grid = new Tl[4, 4];
		for (int i = 0; i < 16; i++) if (snap[i] > 0) { Tl t = new Tl { V = snap[i], R = i / 4, C = i % 4, Appear = 1f }; t.X = t.C; t.Y = t.R; grid[t.R, t.C] = t; tiles.Add(t); }
		Score = snapScore; undos--; snap = null; Sfx.Play("flip");
	}
	public override void Click(float x, float y) { if (x > 292 && x < 392 && y > 96 && y < 126) Undo(); }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (GraphicsPath p = Gfx.Round(new RectangleF(cx - 20, cy - 18, 40, 36), 8)) using (SolidBrush br = new SolidBrush(Color.FromArgb(244, 190, 90))) g.FillPath(br, p);
		Txt(g, "2048", 9f, 2, Color.White, cx - 20, cy - 12, 40, 24, StringAlignment.Center);
	}
	private void Spawn()
	{
		List<int> e = new List<int>(); for (int i = 0; i < 16; i++) if (grid[i / 4, i % 4] == null) e.Add(i); if (e.Count == 0) return;
		int c = e[Rnd.Next(e.Count)]; Tl t = new Tl { V = Rnd.NextDouble() < (best >= 512 ? 0.74 : best >= 128 ? 0.84 : 0.9) ? 2 : 4, R = c / 4, C = c % 4 }; t.X = t.C; t.Y = t.R; grid[t.R, t.C] = t; tiles.Add(t);
	}
	private bool mergedNow;
	private bool Move(int dr, int dc)
	{
		mergedNow = false; bool moved = false; HashSet<Tl> merged = new HashSet<Tl>();
		for (int k = 0; k < 4; k++)
		{
			List<Tl> line = new List<Tl>();
			for (int i = 0; i < 4; i++)
			{
				int r = dc != 0 ? k : (dr > 0 ? 3 - i : i), c = dc != 0 ? (dc > 0 ? 3 - i : i) : k;   // 미는 쪽 끝에 가까운 칸부터
				Tl t = grid[r, c]; if (t != null) { line.Add(t); grid[r, c] = null; }
			}
			int slot = 0; Tl last = null;
			foreach (Tl t in line)
			{
				int tr, tc;
				if (last != null && last.V == t.V && !merged.Contains(last))
				{
					// 부딪힌 같은 숫자: 앞 타일로 합쳐져요 (한 번 밀 때 한 번만)
					last.V *= 2; mergedNow = true; last.Bump = 1f; Fx.Burst(20 + last.C * 92 + 42, 138 + last.R * 92 + 42, TileColor(last.V), last.V >= 128 ? 10 : 5); if (last.V >= 128) Shake = Math.Max(Shake, last.V >= 512 ? 0.6f : 0.3f); if (last.V == 2048) Sfx.Play("fanfare"); merged.Add(last); Score += last.V; if (last.V > best) best = last.V; t.Into = last; tr = last.R; tc = last.C; moved = true;
					t.R = tr; t.C = tc;
				}
				else
				{
					tr = dc != 0 ? k : (dr > 0 ? 3 - slot : slot); tc = dc != 0 ? (dc > 0 ? 3 - slot : slot) : k; slot++;
					if (tr != t.R || tc != t.C) moved = true; t.R = tr; t.C = tc; grid[tr, tc] = t; last = t;
				}
			}
		}
		return moved;
	}
	private bool CanMove()
	{
		for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
		{
			if (grid[r, c] == null) return true;
			if (r < 3 && grid[r + 1, c] != null && grid[r + 1, c].V == grid[r, c].V) return true;
			if (c < 3 && grid[r, c + 1] != null && grid[r, c + 1].V == grid[r, c].V) return true;
		}
		return false;
	}
	public override void Press(Keys k)
	{
		int dr = 0, dc = 0;
		if (k == Keys.U || k == Keys.Z || k == Keys.Back) { Undo(); return; }
			if (k == Keys.Left || k == Keys.A) dc = -1; else if (k == Keys.Right || k == Keys.D) dc = 1; else if (k == Keys.Up || k == Keys.W) dr = -1; else if (k == Keys.Down || k == Keys.S) dr = 1; else return;
		if (Over) return;
		int[] pre = new int[16]; for (int i = 0; i < 16; i++) pre[i] = grid[i / 4, i % 4] != null ? grid[i / 4, i % 4].V : 0; int preScore = Score;
		if (Move(dr, dc)) { snap = pre; snapScore = preScore; Sfx.Play(mergedNow ? "pluck" : "tick"); Spawn(); moves++; if (!CanMove()) Over = true; } else { shake = 0.25f; Sfx.Play("bad"); }   // 안 움직이면 판이 살짝 흔들려요
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (shake > 0) shake -= dt;
		for (int i = tiles.Count - 1; i >= 0; i--)
		{
			Tl a = tiles[i]; float k = Math.Min(1f, dt * 20f);
			a.X += (a.C - a.X) * k; a.Y += (a.R - a.Y) * k;
			if (a.Appear < 1f) a.Appear = Math.Min(1f, a.Appear + dt * 7f);
			if (a.Bump > 0) a.Bump = Math.Max(0f, a.Bump - dt * 5f);
			if (a.Into != null && Math.Abs(a.X - a.C) + Math.Abs(a.Y - a.R) < 0.06f) tiles.RemoveAt(i);   // 합쳐지는 타일은 도착하면 사라져요
		}
	}
	private static Color TileColor(int v)
	{
		switch (v)
		{
			case 2: return Color.FromArgb(120, 190, 250); case 4: return Color.FromArgb(160, 205, 100); case 8: return Color.FromArgb(251, 138, 87); case 16: return Color.FromArgb(250, 89, 134);
			case 32: return Color.FromArgb(40, 140, 250); case 64: return Color.FromArgb(24, 120, 240); case 128: return Color.FromArgb(30, 150, 235); case 256: return Color.FromArgb(10, 185, 205);
			case 512: return Color.FromArgb(110, 83, 248); case 1024: return Color.FromArgb(200, 70, 205); default: return Color.FromArgb(214, 62, 213);
		}
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(250, 244, 232), Color.FromArgb(240, 228, 206));
		float sh = shake > 0 ? (float)Math.Sin(t * 70) * shake * 14f : 0f; GraphicsState gs0 = g.Save(); g.TranslateTransform(sh, 0);
		RectangleF board = new RectangleF(12, 130, 376, 376);
		using (GraphicsPath sgp = Gfx.Round(new RectangleF(board.X + 2, board.Y + 6, board.Width, board.Height), 14)) using (SolidBrush sbr = new SolidBrush(Color.FromArgb(50, 90, 60, 30))) g.FillPath(sbr, sgp);
		using (GraphicsPath gp = Gfx.Round(board, 14)) using (SolidBrush br = new SolidBrush(Color.FromArgb(187, 173, 160))) g.FillPath(br, gp);
		for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) { RectangleF cell = new RectangleF(20 + c * 92, 138 + r * 92, 84, 84); using (GraphicsPath gp = Gfx.Round(cell, 8)) using (SolidBrush br = new SolidBrush(Color.FromArgb(205, 193, 180))) { g.FillPath(br, gp); using (Pen ip = new Pen(Color.FromArgb(46, 80, 60, 40), 2f)) g.DrawArc(ip, cell.X + 3, cell.Y + 2, cell.Width - 6, 20, 200, 140); }; }
		// 합쳐져 사라지는 타일을 먼저(아래), 남는 타일을 위에
		for (int pass = 0; pass < 2; pass++)
			foreach (Tl a in tiles)
			{
				if ((a.Into != null) != (pass == 0)) continue;
				float sc = (0.25f + 0.75f * a.Appear) * (1f + 0.16f * (float)Math.Sin(a.Bump * Math.PI)); float cw = 84 * sc;
				float cx = 20 + a.X * 92 + 42, cy = 138 + a.Y * 92 + 42; RectangleF tr = new RectangleF(cx - cw / 2, cy - cw / 2, cw, cw);
				if (a.V >= 128 && a.Into == null) { float gp0 = a.V >= 1024 ? 0.6f + 0.4f * (float)Math.Sin(t * 5) : 0.5f; using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(60 * gp0), TileColor(a.V)))) g.FillEllipse(hb, cx - cw * 0.78f, cy - cw * 0.78f, cw * 1.56f, cw * 1.56f); }
				bool spr48 = false;
				if (spr48) Spr.Draw(g, "ms_t" + (a.V <= 2048 ? a.V : 2048), cx, cy, cw, cw * 0.92f, 0f); else
				Bevel(g, tr, 8, TileColor(a.V));
				if (a.Bump > 0f && a.Bump < 1f && a.Into == null) using (Pen rp = new Pen(Color.FromArgb((int)(200 * a.Bump), 255, 255, 255), 3f * a.Bump + 0.6f)) { float rr = cw * (0.55f + 0.5f * (1f - a.Bump)); g.DrawEllipse(rp, cx - rr, cy - rr, rr * 2, rr * 2); }
				if (a.V >= 128 && a.Into == null) using (SolidBrush sh2 = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) { float u2 = (float)((t * 0.6 + a.X * 0.3 + a.Y * 0.2) % 2.0); if (u2 < 1f) g.FillRectangle(sh2, tr.X + tr.Width * u2 - 6, tr.Y + 4, 8, tr.Height - 8); }
				if (!spr48) Txt(g, a.V.ToString(), (a.V < 100 ? 20f : (a.V < 1000 ? 17f : 14f)) * sc, 2, Color.White, cx - 42, cy - 42, 84, 84, StringAlignment.Center);
			}
		g.Restore(gs0);
		Hint(g, "같은 숫자 두 개가 부딪히면 합쳐져요  (2 + 2 = 4)", 524 + 11.0, 11f, false, 1f);
		Hint(g, "방향키 · 마우스로 끌기로 밀어요", 548 + 10.0, 10f, false, 1f);
			using (GraphicsPath ub = Gfx.Round(new RectangleF(292, 96, 96, 28), 12)) using (SolidBrush ubb = new SolidBrush(Color.FromArgb(undos > 0 && snap != null ? 200 : 70, 143, 122, 102))) g.FillPath(ubb, ub);
			Txt(g, "↶ 되돌리기 " + undos, 10.5f, 2, Color.White, 292, 96, 96, 28, StringAlignment.Center);
	}
}
// ── 9. 타자 연습 (공문 문구) ────────────────────────────────
internal sealed class TypeGame : MiniGame
{
	private static readonly string[] Ph = { "아래와 같이 알려드립니다", "검토 후 회신 바랍니다", "붙임  계획서 1부.  끝.", "협조 부탁드립니다", "초과근무 확인 요망", "결재 올립니다", "참고하시기 바랍니다", "기한 내 제출 바랍니다", "문의는 담당자에게", "다음과 같이 보고합니다", "회의 자료를 첨부합니다", "수고 많으셨습니다", "확인 후 조치 바랍니다", "관련 부서에 통보합니다" };
	private string target = "", bonusMsg = ""; private double left, phraseT, bonusT; private int done, streak, keys; private readonly List<string> bag = new List<string>();
	public override bool NeedsInput { get { return true; } }
	public override float InputY { get { return 346f; } }   // 문구 바로 아래에 진짜 입력창을 놓아요 (입력창이 화면에 하나만 보이게)
	public override string Costume { get { return "정장"; } }
	public override string Name { get { return "공문 타자"; } }
	public override string Key { get { return "type"; } }
	public override string Desc { get { return "공문 문구를 빨리 입력"; } }
	public override string Help { get { return "문구 바로 아래 칸에 그대로 입력해요\n빨리 칠수록 시간이 늘고(+1~2초), 연속으로 치면 보너스!\n오타가 나면 빨간 안내가 나와요"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override void Reset() { Score = 0; Over = false; left = 40; done = 0; keys = 0; streak = 0; phraseT = 0; bonusT = 0; bonusMsg = ""; bag.Clear(); Typed = ""; ClearTyped = true; NextPhrase(); }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (GraphicsPath p = Gfx.Round(new RectangleF(cx - 22, cy - 10, 44, 24), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 210, 230))) g.FillPath(b, p);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 80, 110))) for (int i = 0; i < 4; i++) g.FillRectangle(b, cx - 17 + i * 9, cy - 5, 6, 5);
	}
	private static readonly string[] PhMid = { "위 제목과 관련하여", "아래와 같이 협조를 요청합니다", "기한 내 회신 바랍니다", "관련 서류를 제출해 주세요", "다음과 같이 시행합니다", "해당 사항 없음", "검토 결과를 통보합니다", "내부 결재를 받았습니다", "회의 개최를 알려드립니다", "일정 변경을 안내드립니다", "자료를 취합하여 송부", "업무 추진에 참고 바랍니다", "민원 처리 결과를 알려드립니다", "예산 집행 내역을 확인하세요", "점검 결과 이상 없음" };
	private static readonly string[] PhLong = { "붙임  1. 추진계획 1부.  끝.", "시행 총무과-1234 (2026. 10. 21.)", "문의 총무과 담당자 063-859-0000", "수신 각 부서장 (경유)", "접수 민원과-5678 (2026. 10. 21.)", "회신 기한 2026. 10. 31.(금) 18:00까지", "제목 2026년 하반기 업무 협조 요청", "관련 행정안전부 공문 제2026-77호", "발신 익산시장 직인 생략", "붙임  2. 점검 결과표 1부.  끝.", "시간외근무 확인 후 결재 바랍니다", "2026. 10. 21.(수) 14:00 회의실", "주요 내용은 아래와 같습니다", "본 문서는 대외비로 취급 바랍니다", "첨부 파일을 반드시 확인하세요", "공문 번호를 정확히 기재해 주세요" };
	private static string NoSp(string s) { return (s ?? "").Normalize(System.Text.NormalizationForm.FormC).Replace(" ", "").Replace("\u00A0", "").Replace("\t", "").Trim(); }   // 띄어쓰기 차이는 봐줘요
	// 한글 조합 중인 마지막 글자는 아직 틀린 게 아니에요 (예: '한'을 치는 중에 '하'나 'ㅎ'이 보이는 순간)
	private static bool PartOk(char typed, char want)
	{
		if (typed == want) return true;
		int w = want - 0xAC00, c = typed - 0xAC00;
		if (c >= 0 && c < 11172 && w >= 0 && w < 11172) return c / 28 == w / 28 && (c % 28 == 0 || c % 28 == w % 28);
		if (typed >= 0x3131 && typed <= 0x314E && w >= 0 && w < 11172) { string cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ"; int i = cho.IndexOf(typed); return i >= 0 && i == w / 588; }
		return false;
	}
	// 한글은 자판을 누른 횟수(자모)로 세요: 쌍모음·겹받침은 2타
	private static int Strokes(string s)
	{
		int n = 0; const string dv = "ㅘㅙㅚㅝㅞㅟㅢ"; const string jong = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ"; const string vow = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
		foreach (char c in s) { int w = c - 0xAC00; if (w >= 0 && w < 11172) { n++; int v = (w % 588) / 28, j = w % 28; n += dv.IndexOf(vow[v]) >= 0 ? 2 : 1; if (j > 0) n += "ㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄ".IndexOf(jong[j]) >= 0 ? 2 : 1; } else n++; }
		return n;
	}
	private double FastT { get { return 2.2 + target.Length * 0.13; } }
	private static string[] SplitLines(string s) { if (s.Length <= 17) return new string[] { s }; int mid = s.Length / 2, best = -1; for (int i = 0; i < s.Length; i++) if (s[i] == ' ' && (best < 0 || Math.Abs(i - mid) < Math.Abs(best - mid))) best = i; if (best < 0) best = mid; return new string[] { s.Substring(0, best), s.Substring(best).TrimStart() }; }
	private void NextPhrase()
	{
		if (bag.Count == 0)
		{
			bag.AddRange(Ph); if (done >= 4) bag.AddRange(PhMid); if (done >= 9) bag.AddRange(PhLong);
			for (int i = bag.Count - 1; i > 0; i--) { int j = Rnd.Next(i + 1); string t = bag[i]; bag[i] = bag[j]; bag[j] = t; }
		}
		target = bag[0]; bag.RemoveAt(0);
	}

	public override void Update(float dt, GameInput inp, double t)
	{
		left -= dt; if (left > 60 - PlayT) left = 60 - PlayT; if (left <= 0) { left = 0; Over = true; return; }
		phraseT += dt; if (bonusT > 0) bonusT -= dt;
		if (!string.IsNullOrEmpty(Typed) && NoSp(Typed) == NoSp(target))
		{
			int len = target.Replace(" ", "").Length; streak++; double add = phraseT < FastT ? 2 : (phraseT < FastT + 2.0 ? 1 : 0); int pts = len + (streak >= 3 ? 3 * Math.Min(streak, 8) : 0) + (phraseT < FastT ? 5 : 0);
			Score += pts; left += add; done++; keys += Strokes(target); bonusMsg = (phraseT < FastT ? "빠르다! " : "") + "+" + pts + "점" + (add > 0 ? " · +" + add + "초" : "") + (streak >= 3 ? " · " + streak + "연속" : ""); bonusT = 1.4;
			Sfx.Play(add >= 2 ? "ok2" : "ok"); Fx.Burst(W / 2f, 240f, add >= 2 ? Color.FromArgb(255, 214, 80) : Color.FromArgb(120, 230, 200), add >= 2 ? 10 : 5); if (streak >= 5) Shake = 0.2f;
			phraseT = 0; ClearTyped = true; Typed = ""; NextPhrase();
		}
	}
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_desk", Color.FromArgb(18, 24, 60), Color.FromArgb(40, 60, 110)); if (Spr.Get("bg_desk") == null) Twinkle(g, t);
		string ty = Typed ?? ""; string tyNs = NoSp(ty); int okNs = 0; while (okNs < tyNs.Length && okNs < NoSp(target).Length && tyNs[okNs] == NoSp(target)[okNs]) okNs++;
		int ok = 0; { int cnt = 0; while (ok < target.Length && cnt < okNs) { if (target[ok] != ' ') cnt++; ok++; } }   // 띄어쓰기는 건너뛰고 맞은 글자 수만큼 색칠
		using (GraphicsPath tp0 = Gfx.Round(new RectangleF(W / 2f - 78f, 156f, 156f, 22f), 11)) using (SolidBrush tb0 = new SolidBrush(Color.FromArgb(150, 12, 20, 44))) g.FillPath(tb0, tp0);
		Txt(g, "이 문구를 입력하세요", 11f, 1, Color.FromArgb(240, 255, 255, 255), 0, 157, W, 22, StringAlignment.Center);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(20, 184, W - 40, 110), 18)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 16, 24, 52))) g.FillPath(b, gp); using (Pen bp = new Pen(Color.FromArgb(120, 170, 190, 240), 1.5f)) g.DrawPath(bp, gp); }
		string[] tl = SplitLines(target); int off = 0;
		for (int li = 0; li < tl.Length; li++)
		{
			float ly = tl.Length == 1 ? 222f : 204f + li * 40f; int cut = Math.Max(0, Math.Min(tl[li].Length, ok - off));
			Txt(g, tl[li], 17f, 2, Color.FromArgb(235, 215, 225, 250), 23, ly, W - 46, 34, StringAlignment.Near);
			if (cut > 0) Txt(g, tl[li].Substring(0, cut), 17f, 2, Color.FromArgb(255, 214, 90), 23, ly, W - 46, 34, StringAlignment.Near);
			off += tl[li].Length + (target.Length > off + tl[li].Length && target[off + tl[li].Length] == ' ' ? 1 : 0);
		}
		{ int kk = keys + Strokes(target.Substring(0, Math.Min(target.Length, ok))); double el2 = Math.Max(1.0, PlayT); int kpm = PlayT > 3 ? (int)(kk * 60.0 / el2) : 0; Txt(g, done + "개 완료 · " + kk + "타 · 분당 " + kpm + "타", 12f, 1, Color.FromArgb(255, 255, 255, 255), 0, 320, W, 24, StringAlignment.Center); }
		{ // 빠르게 치면 보너스: 남은 시간 막대
			float fw = (float)Math.Max(0.0, 1.0 - phraseT / FastT); using (GraphicsPath tg = Gfx.Round(new RectangleF(40, 298, W - 80, 6), 3)) using (SolidBrush tb = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillPath(tb, tg);
			if (fw > 0.01f) using (GraphicsPath tg = Gfx.Round(new RectangleF(40, 298, Math.Max(6f, (W - 80) * fw), 6), 3)) using (SolidBrush tb = new SolidBrush(fw > 0.35f ? Color.FromArgb(255, 214, 90) : Color.FromArgb(255, 140, 110))) g.FillPath(tb, tg);
			Txt(g, fw > 0 ? "빠른 입력 보너스" : "", 8.5f, 1, Color.FromArgb(150, 255, 235, 170), 40, 306, 160, 14, StringAlignment.Near);
			bool wrongNow = tyNs.Length > okNs && tyNs.Length <= NoSp(target).Length && !(tyNs.Length == okNs + 1 && PartOk(tyNs[okNs], NoSp(target)[okNs])); using (GraphicsPath eg = Gfx.Round(new RectangleF(14, 339, W - 28, 44), 12)) { using (SolidBrush eb = new SolidBrush(Color.FromArgb(wrongNow ? 80 : 40, wrongNow ? Color.FromArgb(255, 110, 120) : Color.White))) g.FillPath(eb, eg); using (Pen ep = new Pen(Color.FromArgb(wrongNow ? 220 : 120, wrongNow ? Color.FromArgb(255, 120, 130) : Color.FromArgb(160, 220, 200)), 2f)) g.DrawPath(ep, eg); }
		}
			if (tyNs.Length > okNs && tyNs.Length <= NoSp(target).Length && !(tyNs.Length == okNs + 1 && PartOk(tyNs[okNs], NoSp(target)[okNs]))) Txt(g, "오타! 지우고 다시 (" + (okNs + 1) + "번째 글자)", 10.5f, 1, Color.FromArgb(255, 130, 140), 0, 388, W, 20, StringAlignment.Center);
			if (bonusT > 0 && bonusMsg.Length > 0) Txt(g, bonusMsg, 14f, 2, Color.FromArgb((int)(255 * Math.Min(1.0, bonusT)), 255, 226, 120), 0, 420, W, 28, StringAlignment.Center);
		if (Spr.Get("ms_ty1") != null)
		{
			Spr.Draw(g, "ms_ty3", 54f, 548f, 52f, 70f, 0f); Spr.Draw(g, "ms_ty1", 196f, 556f, 170f, 170f * 76f / 150f, 0f); Spr.Draw(g, "ms_ty2", 296f, 552f, 38f, 38f * 150f / 116f, -8f);
			Spr.Draw(g, "ms_ty4", 350f, 112f, 54f, 54f * 137f / 150f, 6f); Spr.Draw(g, "ms_ty5", 46f, 114f, 48f, 48f * 130f / 150f, -8f);
			Spr.Draw(g, "ms_ty0", W / 2f - 68f, 332f, 22f, 22f * 150f / 117f, -6f);
		}
		using (GraphicsPath hp1 = Gfx.Round(new RectangleF(24f, 490f, W - 48f, 20f), 10)) using (SolidBrush hb1 = new SolidBrush(Color.FromArgb(150, 12, 20, 44))) g.FillPath(hb1, hp1);
		Txt(g, "문구 바로 아래 칸에 입력하세요 (노란 글자가 맞게 친 부분이에요)", 9.5f, 0, Color.FromArgb(235, 255, 255, 255), 0, 492, W, 18, StringAlignment.Center);
		DrawMascot(g, W - 60, 470, 70, (float)Math.Sin(t * 4) * 3);
	}
}



// ── 스트레스 사격 ─────────────────────────────────────────────
internal sealed class StressGame : MiniGame
{
	public override string Costume { get { return "g_clay"; } }
	private class Bl { public float X, Y, V, Ph, R, Vx, Vy, Vr, Rot; public int Hp, MaxHp, Kind; public Color C; public string[] Lines; public float Flash; }
	private class Tr { public float X0, Y0, X1, Y1, Age; }
	private class Pc { public float X, Y, Vx, Vy, Rot, Vr, Age; public Color C; }
	private class Sm { public float X, Y, Vx, Vy, R, Age, Life; public bool Ring; public Color C; }
	private readonly List<Sm> sms = new List<Sm>(); private float hitFlashT;
	private class Tx { public string S; public float X, Y, Age; public Color C; }
	private readonly List<Bl> bls = new List<Bl>(); private readonly List<Tr> trs = new List<Tr>(); private readonly List<Pc> pcs = new List<Pc>(); private readonly List<Tx> txs = new List<Tx>();
	private double left, spawn, cd, flash; private int combo, goodSpawn, shells = 2; private float reloadT; private float kick, aimA = -1.57f;
	private static Image soldier;
	private const float PX = 208f, PY = 502f, SgH = 128f;   // 군인 마룡이(샷건 그림)의 손 위치가 조준의 기준이에요
	private bool UseSg { get { return Spr.Get("ms_k_shotgun") != null; } }
	// 샷건 그림에서 총구 위치: 손 기준(0.55, 0.50)에서 오른쪽 위로 (0.45, -0.08) · 위를 겨눌수록 몸이 살짝 뒤로 기울어요
	private float SgTilt(float a) { float e = (float)Math.Atan2(Math.Sin(a), Math.Abs(Math.Cos(a))); return e * 0.5f; }
	private PointF Muzzle(float a)
	{
		Bitmap sg = Spr.Get("ms_k_shotgun"); if (sg == null) return new PointF(PX + (float)Math.Cos(a) * 76f, PY + (float)Math.Sin(a) * 76f);
		float w = SgH * sg.Width / (float)sg.Height, dir = Math.Cos(a) >= 0 ? 1f : -1f, th = SgTilt(a), lx = 0.45f * w, ly = -0.08f * SgH;
		float rx = lx * (float)Math.Cos(th) - ly * (float)Math.Sin(th), ry = lx * (float)Math.Sin(th) + ly * (float)Math.Cos(th); return new PointF(PX + dir * rx, PY + ry);
	}
	// 라벨(줄바꿈은 |), 체력
	private static readonly object[][] Bad = {
		new object[] { "악성|민원", 2 }, new object[] { "야근", 2 }, new object[] { "결재|대기", 2 }, new object[] { "갑질|상사", 2 }, new object[] { "또|회의", 1 },
		new object[] { "보고서|수정", 2 }, new object[] { "휴일|호출", 2 }, new object[] { "감사|점검", 2 }, new object[] { "퇴근 직전|업무", 2 }, new object[] { "메신저|폭주", 2 },
		new object[] { "엑셀|오류", 1 }, new object[] { "국감|자료", 2 }, new object[] { "전화|폭탄", 2 }, new object[] { "오타|지적", 1 }, new object[] { "민원|재방문", 2 }, new object[] { "자료|요청", 1 }
	};
	private static readonly string[] Good = { "월급", "칭찬", "연차", "점심|시간", "칼퇴" };
	private static readonly Color[] Pal = { Color.FromArgb(232, 84, 92), Color.FromArgb(238, 128, 60), Color.FromArgb(150, 96, 214), Color.FromArgb(70, 130, 232), Color.FromArgb(216, 80, 150), Color.FromArgb(90, 120, 150) };
	public override string Name { get { return "클레이 사격"; } }
	public override string Key { get { return "shoot"; } }
	public override string Desc { get { return "두 발씩 쏘고 장전! 접시를 깨요"; } }
	public override string Help { get { return "마우스로 조준하고 클릭해서 쏴요 (스페이스도 돼요)\n두 발까지 연속으로 쏠 수 있고, 두 발을 다 쏘면 장전해야 해요\n날아가는 접시(악성 민원 · 야근 · 결재 대기…)를\n샷건으로 한 번에 깨뜨려요! (부채꼴로 퍼져서 두 개도 한 번에) 월급 · 칭찬 · 연차 접시는 감점,\n금색 '퇴근!' 접시는 시간 +3초"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override bool Crosshair { get { return true; } }
	public override void Reset() { sms.Clear(); hitFlashT = 0; bls.Clear(); trs.Clear(); pcs.Clear(); txs.Clear(); Score = 0; combo = 0; left = 45; spawn = 0.05; cd = 0; flash = 0; kick = 0; goodSpawn = 0; shells = 2; reloadT = 0f; Over = false; EnsureSoldier(); }
	private void EnsureSoldier()
	{
		if (soldier != null) return;
		try { using (Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("outfit.군복.png")) if (st != null) using (Image im = Image.FromStream(st)) soldier = new Bitmap(im); }
		catch { }
	}
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 80, 40))) g.FillEllipse(b, cx - 22, cy - 6, 44, 18); using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 130, 60))) g.FillEllipse(b, cx - 22, cy - 11, 44, 20);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, cx - 9, cy - 14, 8, 11);
		using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 1.6f)) g.DrawLine(p, cx, cy + 15, cx - 3, cy + 26);
	}
	private void NewBalloon(float lvl)
	{
		// 클레이 사격: 양옆에서 포물선을 그리며 날아가는 접시 (한 발에 깨져요)
		Bl b = new Bl(); b.Ph = (float)(Rnd.NextDouble() * 6.28); b.Hp = b.MaxHp = 1;
		bool left = Rnd.Next(2) == 0; float sp = (float)((185 + Rnd.NextDouble() * 120) * Math.Min(1.35, 0.9 + lvl * 0.2));
		b.X = left ? -26f : W + 26f; b.Y = 380f + (float)Rnd.NextDouble() * 120f; b.Vx = left ? sp : -sp; b.Vy = -(float)(450 + Rnd.NextDouble() * 170); b.Vr = (float)((Rnd.NextDouble() - 0.5) * 520);
		double r = Rnd.NextDouble();
		if (r < 0.07) { b.Kind = 2; b.Lines = new string[] { "퇴근!" }; b.R = 25; b.C = Color.FromArgb(240, 178, 30); b.Vx *= 1.18f; }
		else if (r < 0.22 && goodSpawn >= 3) { goodSpawn = 0; b.Kind = 1; b.Lines = Good[Rnd.Next(Good.Length)].Split('|'); b.R = 28; b.C = Color.FromArgb(70, 176, 120); }
		else
		{
			goodSpawn++; object[] d = Bad[Rnd.Next(Bad.Length)]; b.Lines = ((string)d[0]).Split('|'); b.R = 28;
			Color[] clay = { Color.FromArgb(236, 120, 56), Color.FromArgb(226, 96, 70), Color.FromArgb(240, 150, 64), Color.FromArgb(214, 84, 96) }; b.C = clay[Rnd.Next(clay.Length)];
		}
		bls.Add(b);
	}
	
	public override void Click(float x, float y) { Fire(x, y); }
	public override void Press(Keys k) { if (k == Keys.Space) Fire(mx, my); }
	private float mx = W / 2f, my = 200;
	private void Fire(float x, float y)
	{
		if (cd > 0 || Over || reloadT > 0f || shells <= 0) return;
		shells--; if (shells <= 0) reloadT = 1.2f;
		cd = 0.26; kick = 1.0f; flash = 0.07; Sfx.Play("thud2"); Shake = Math.Max(Shake, 0.08f);
		float a0 = (float)Math.Atan2(y - PY, x - PX); a0 = Clamp(a0, -2.88f, -0.26f); aimA = a0;
		PointF mz = Muzzle(a0); float ox = mz.X, oy = mz.Y; bool hit = false;
		for (int k = 0; k < 4; k++) sms.Add(new Sm { X = ox + (float)Math.Cos(a0) * k * 4f, Y = oy + (float)Math.Sin(a0) * k * 4f, Vx = (float)Math.Cos(a0) * (30 + k * 12) + (float)(Rnd.NextDouble() - 0.5) * 20, Vy = (float)Math.Sin(a0) * (30 + k * 12) - 24, R = 5 + k * 2.2f, Life = 0.55f + k * 0.08f, C = Color.FromArgb(235, 235, 240) });
		pcs.Add(new Pc { X = PX + 6, Y = PY - 12, Vx = -(70f + (float)Rnd.NextDouble() * 50f) * (Math.Cos(a0) < 0 ? -1f : 1f), Vy = -(120f + (float)Rnd.NextDouble() * 60f), Rot = 0, Vr = 700f, C = Color.FromArgb(236, 188, 70) }); int gotPts = 0; bool bad = false;
		List<Bl> dead = new List<Bl>();
		// 샷건: 조준선 앞쪽 부채꼴 안에 들어온 접시는 한 번에 모두 맞아요 (가까울수록 강해서 2칸짜리도 깨져요)
		float Rng = 470f, spread = 0.13f;
		for (int p = 0; p < 9; p++) { float a = a0 + (float)((Rnd.NextDouble() - 0.5) * 2.0 * spread * (p == 0 ? 0 : 0.9)); float len = Rng * (0.55f + (float)Rnd.NextDouble() * 0.45f); trs.Add(new Tr { X0 = ox, Y0 = oy, X1 = ox + (float)Math.Cos(a) * len, Y1 = oy + (float)Math.Sin(a) * len }); }
		{
			float dx = (float)Math.Cos(a0), dy = (float)Math.Sin(a0);
			foreach (Bl b in bls)
			{
				if (b.Hp <= 0) continue;
				float cxv = b.X - ox, cyv = b.Y - oy; float t = cxv * dx + cyv * dy; if (t < 0 || t > Rng) continue;
				float dist = Math.Abs(cxv * dy - cyv * dx); if (dist > b.R * 1.15f + t * Math.Tan(spread) * 0.95f) continue;
				b.Hp -= t < 300f ? 2 : 1; b.Flash = 0.12f; hit = true; if (b.Hp <= 0 && !dead.Contains(b)) dead.Add(b);
			}
		}
		foreach (Bl b in dead)
		{
			float bx = b.X; sms.Add(new Sm { X = bx, Y = b.Y, R = 10, Life = 0.35f, Ring = true, C = b.Kind == 1 ? Color.FromArgb(255, 120, 130) : (b.Kind == 2 ? Color.FromArgb(255, 220, 90) : Color.FromArgb(255, 200, 120)) }); for (int k = 0; k < 4; k++) sms.Add(new Sm { X = bx, Y = b.Y, Vx = (float)(Rnd.NextDouble() - 0.5) * 70, Vy = (float)(Rnd.NextDouble() - 0.7) * 50, R = 8 + k * 2, Life = 0.5f, C = Color.FromArgb(200, 170, 140) }); hitFlashT = 0.1f;
			for (int i = 0; i < 16; i++) { double an = Rnd.NextDouble() * 6.28, sp = 90 + Rnd.NextDouble() * 220; pcs.Add(new Pc { X = bx, Y = b.Y, Vx = (float)(Math.Cos(an) * sp), Vy = (float)(Math.Sin(an) * sp - 60), Rot = (float)(Rnd.NextDouble() * 360), Vr = (float)((Rnd.NextDouble() - 0.5) * 720), C = i % 3 == 0 ? Color.White : b.C }); }
			if (b.Kind == 1) { Sfx.Play("bad2"); Score = Math.Max(0, Score - 5); combo = 0; bad = true; txs.Add(new Tx { S = "-5", X = bx, Y = b.Y, C = Color.FromArgb(255, 120, 130) }); }
			else if (b.Kind == 2) { Sfx.Play("fanfare"); Score += 5; left += 3; gotPts += 5; txs.Add(new Tx { S = "+5  시간 +3초", X = bx, Y = b.Y, C = Color.FromArgb(255, 220, 90) }); }
			else { Sfx.Play("glass"); Fx.Burst(bx, b.Y, Color.FromArgb(255, 150, 70), 8); Shake = 0.2f; int mul = 1 + Math.Min(2, combo / 4); int pt = b.MaxHp * mul; Score += pt; gotPts += pt; txs.Add(new Tx { S = "+" + pt + (mul > 1 ? " x" + mul : ""), X = bx, Y = b.Y, C = Color.FromArgb(255, 235, 140) }); }
			bls.Remove(b);
		}
		if (!bad) { if (hit) combo++; else combo = 0; }
	}
	private double now; private bool logged;
	public override void Update(float dt, GameInput inp, double t)
	{
		now = t; mx = inp.Mx; my = inp.My;
		left -= dt; if (left > 60 - PlayT) left = 60 - PlayT; double lvl = 1 + (45 - left) / 40.0; spawn -= dt;
		if (spawn <= 0) { spawn = 0.85 / lvl + Rnd.NextDouble() * 0.4; NewBalloon((float)lvl); if (left < 17 && Rnd.NextDouble() < 0.32) NewBalloon((float)lvl); }   // 후반엔 접시가 쌍으로 날아와요
		for (int i = bls.Count - 1; i >= 0; i--) { Bl b = bls[i]; b.Vy += 520f * dt; b.X += b.Vx * dt; b.Y += b.Vy * dt; b.Rot += b.Vr * dt; if (b.Flash > 0) b.Flash -= dt; if ((b.Y > H - 36 && b.Vy > 0) || b.X < -70 || b.X > W + 70) { if (b.Kind == 0) combo = 0; bls.RemoveAt(i); } }
		for (int i = trs.Count - 1; i >= 0; i--) { trs[i].Age += dt; if (trs[i].Age > 0.12f) trs.RemoveAt(i); }
		for (int i = pcs.Count - 1; i >= 0; i--) { Pc p = pcs[i]; p.Age += dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vy += 520 * dt; p.Rot += p.Vr * dt; if (p.Age > 0.9f) pcs.RemoveAt(i); }
		for (int i = sms.Count - 1; i >= 0; i--) { Sm m = sms[i]; m.Age += dt; m.X += m.Vx * dt; m.Y += m.Vy * dt; if (m.Age > m.Life) sms.RemoveAt(i); }
		if (hitFlashT > 0) hitFlashT -= dt;
		for (int i = txs.Count - 1; i >= 0; i--) { txs[i].Age += dt; txs[i].Y -= 40 * dt; if (txs[i].Age > 0.9f) txs.RemoveAt(i); }
		if (reloadT > 0f) { float pr = reloadT; reloadT -= dt; if ((pr > 0.9f && reloadT <= 0.9f) || (pr > 0.5f && reloadT <= 0.5f)) Sfx.Play("tick"); if (reloadT <= 0f) { reloadT = 0f; shells = 2; Sfx.Play("ok"); } }
		if (cd > 0) cd -= dt; if (flash > 0) flash -= dt; if (kick > 0) kick = Math.Max(0, kick - dt * 5f);
		float a = (float)Math.Atan2(inp.My - PY, inp.Mx - PX); a = Clamp(a, -2.88f, -0.26f); aimA += (a - aimA) * Math.Min(1f, dt * 18f);
		if (left <= 0) { left = 0; Over = true; }
	}
	public override void Draw(Graphics g, double t)
	{
		bool clayBg = Spr.Get("bg_clay") != null; if (clayBg) BgImg(g, "bg_clay", Color.FromArgb(34, 46, 104), Color.FromArgb(104, 132, 200)); else { Bg(g, Color.FromArgb(34, 46, 104), Color.FromArgb(104, 132, 200)); Twinkle(g, t); }
		if (!clayBg) using (LinearGradientBrush sg = new LinearGradientBrush(new RectangleF(0, H - 260, W, 230), Color.FromArgb(0, 255, 170, 120), Color.FromArgb(56, 255, 170, 120), 90f)) g.FillRectangle(sg, 0, H - 260, W, 230);
		if (!clayBg) { List<PointF> tr1 = new List<PointF> { new PointF(0, H - 36) }; for (float x = 0; x <= W; x += 6) tr1.Add(new PointF(x, H - 70 - 14 * (float)Math.Abs(Math.Sin(x * 0.07 + 1)) - 8 * (float)Math.Sin(x * 0.21))); tr1.Add(new PointF(W, H - 36)); using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 52, 60))) g.FillPolygon(b, tr1.ToArray()); }
		if (!clayBg) using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 70, 52))) g.FillRectangle(b, 0, H - 40, W, 40);
		if (!clayBg) using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 96, 66))) g.FillRectangle(b, 0, H - 40, W, 3);
		if (!clayBg) for (int gx = 0; gx < W; gx += 23) using (Pen gp = new Pen(Color.FromArgb(70, 30, 60, 36), 1.6f)) { float hx = gx + (gx * 7 % 11); g.DrawLine(gp, hx, H - 36, hx - 2, H - 43); g.DrawLine(gp, hx, H - 36, hx + 3, H - 42); }
		foreach (Bl b in bls) { try { DrawBalloon(g, b, t); } catch (Exception ex) { if (!logged) { logged = true; Program.LogErr(ex); } } }
		foreach (Pc p in pcs) { GraphicsState s = g.Save(); g.TranslateTransform(p.X, p.Y); g.RotateTransform(p.Rot); using (SolidBrush br = new SolidBrush(Color.FromArgb((int)(255 * Math.Max(0, 1 - p.Age / 0.9)), p.C))) g.FillRectangle(br, -3, -2, 6, 4); g.Restore(s); }
		foreach (Tr r in trs) using (Pen pn = new Pen(Color.FromArgb((int)(200 * (1 - r.Age / 0.12f)), 255, 236, 170), 1.6f)) g.DrawLine(pn, r.X0, r.Y0, r.X1, r.Y1);
		// 군인 마룡이
		if (UseSg) DrawShooter(g);
		else {
		Image im = soldier ?? Mascot; float h = 124f, sh = kick * 3f;
		if (im != null)
		{
			float w = h * im.Width / im.Height; g.DrawImage(im, PX - w / 2 - 6 + sh, H - 34 - h + 8, w, h);
		}
		DrawGun(g, t); }
		foreach (Sm m in sms) { float u = m.Age / m.Life; if (m.Ring) { float rr = m.R + 36f * u; using (Pen rp = new Pen(Color.FromArgb((int)(220 * (1 - u)), m.C), 3f * (1 - u) + 0.6f)) g.DrawEllipse(rp, m.X - rr, m.Y - rr, rr * 2, rr * 2); } else { float rr = m.R * (0.8f + u * 1.4f); using (SolidBrush sb = new SolidBrush(Color.FromArgb((int)(120 * (1 - u)), m.C))) g.FillEllipse(sb, m.X - rr, m.Y - rr, rr * 2, rr * 2); } }
		if (hitFlashT > 0) using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)(hitFlashT * 400), 255, 236, 190))) g.FillRectangle(fb, 0, 0, W, H);
		foreach (Tx x in txs) Txt(g, x.S, 13f, 2, Color.FromArgb((int)(255 * Math.Max(0, 1 - x.Age / 0.9)), x.C), x.X - 60, x.Y - 12, 120, 24, StringAlignment.Center);
		// 조준점
		using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255), 1.8f)) { g.DrawEllipse(p, mx - 13, my - 13, 26, 26); g.DrawLine(p, mx - 19, my, mx - 7, my); g.DrawLine(p, mx + 7, my, mx + 19, my); g.DrawLine(p, mx, my - 19, mx, my - 7); }
		// 장전 표시
		{ for (int k = 0; k < 2; k++) using (SolidBrush b = new SolidBrush(k < shells ? Color.FromArgb(240, 236, 90, 70) : Color.FromArgb(90, 255, 255, 255))) g.FillRectangle(b, mx - 11 + k * 14, my + 20, 8, 14); if (reloadT > 0f) { using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillRectangle(b, mx - 16, my + 38, 32, 4); using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 255, 214, 90))) g.FillRectangle(b, mx - 16, my + 38, (float)(32 * (1 - reloadT / 1.2f)), 4); Txt(g, "장전 중", 10f, 2, Color.FromArgb(240, 255, 232, 150), mx - 30, my + 42, 60, 16, StringAlignment.Center); } }
		if (combo >= 2) Txt(g, combo + " 연속 명중!", 11f, 1, Color.FromArgb(255, 214, 90), 14, 44, 180, 22, StringAlignment.Near);
	}
	// 샷건 든 군인 마룡이 (기존 그림): 조준하는 쪽을 바라보고, 위를 겨눌수록 손을 중심으로 기울어요. 쏠 때 반동으로 뒤로 밀려요
	private void DrawShooter(Graphics g)
	{
		Bitmap sg = Spr.Get("ms_k_shotgun"); float w = SgH * sg.Width / (float)sg.Height, dir = Math.Cos(aimA) >= 0 ? 1f : -1f, th = SgTilt(aimA), rec = kick * 5f;
		GraphicsState st = g.Save(); g.TranslateTransform(PX - dir * rec, PY); g.ScaleTransform(dir, 1f); g.RotateTransform(th * 180f / (float)Math.PI);
		if (reloadT > 0f) { float ru = 1f - reloadT / 1.2f; g.RotateTransform(32f * (float)Math.Sin(Math.PI * Math.Min(1f, ru * 1.15f))); g.TranslateTransform(-(float)Math.Abs(Math.Sin(ru * Math.PI * 3f)) * 7f * (ru > 0.35f ? 1f : 0f), 0f); }
		Spr.Draw(g, "ms_k_shotgun", (0.5f - 0.55f) * w, (0.5f - 0.50f) * SgH, w, SgH, 0f);
		g.Restore(st);
		if (flash > 0)
		{
			PointF mz = Muzzle(aimA); float k = (float)(flash / 0.07);
			GraphicsState fs = g.Save(); g.TranslateTransform(mz.X, mz.Y); g.RotateTransform(aimA * 180f / (float)Math.PI);
			using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(220 * k), 255, 236, 150))) g.FillEllipse(b, -4, -8, 18, 16);
			using (SolidBrush b2 = new SolidBrush(Color.FromArgb((int)(200 * k), 255, 250, 210))) g.FillPolygon(b2, new PointF[] { new PointF(2, 0), new PointF(12, -4), new PointF(36 * k + 8, 0), new PointF(12, 4) });
			using (SolidBrush b3 = new SolidBrush(Color.FromArgb((int)(70 * k), 255, 200, 90))) g.FillEllipse(b3, -6, -18, 42, 36);
			g.Restore(fs);
		}
	}
	private void DrawGun(Graphics g, double t)
	{
		float px = PX + 8 - kick * 3f * (float)Math.Cos(aimA), py = PY - kick * 3f * (float)Math.Sin(aimA);
		GraphicsState s = g.Save(); g.TranslateTransform(px, py); g.RotateTransform(aimA * 180f / (float)Math.PI);
		if (Math.Cos(aimA) < 0) g.ScaleTransform(1, -1);
		if (true)
		using (SolidBrush wood = new SolidBrush(Color.FromArgb(150, 96, 52))) using (SolidBrush steel = new SolidBrush(Color.FromArgb(58, 64, 74))) using (SolidBrush dark = new SolidBrush(Color.FromArgb(34, 38, 46)))
		{
			g.FillRectangle(wood, -34, -5, 30, 11);          // 개머리판
			g.FillRectangle(steel, -6, -5f, 26, 9);         // 몸통
			g.FillRectangle(dark, 20, -3.4f, 56, 7f);       // 굵은 총열
			g.FillRectangle(dark, 20, 3.6f, 44, 3.4f);      // 탄창관
			g.FillRectangle(wood, 28, 2.2f, 24, 7f);        // 펌프 손잡이
			g.FillRectangle(steel, 74, -4.2f, 5, 8.4f);     // 총구
			}

		if (flash > 0)
		{
			float k = (float)(flash / 0.07);
			using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(220 * k), 255, 236, 150))) { g.FillEllipse(b, 80, -7, 16, 14); }
			using (SolidBrush b2 = new SolidBrush(Color.FromArgb((int)(200 * k), 255, 250, 210))) g.FillPolygon(b2, new PointF[] { new PointF(82, 0), new PointF(92, -4), new PointF(110 * k + 8, 0), new PointF(92, 4) });
			using (SolidBrush b3 = new SolidBrush(Color.FromArgb((int)(70 * k), 255, 200, 90))) g.FillEllipse(b3, 74, -16, 36, 32);
		}
		g.Restore(s);
	}
	private void DrawBalloon(Graphics g, Bl b, double t)
	{
		// 점토(클레이) 접시: 납작한 타원 + 테두리 + 윗면 돔, 날아가며 살짝 기울어요
		float x = b.X, y = b.Y, rx = b.R * 1.32f, ry = b.R * 0.56f; float tilt = (float)Math.Sin(b.Rot * 0.0175f) * 16f;
		Color c = b.Flash > 0 ? Color.White : b.C; Color dk = Theme.Mix(c, Color.Black, 0.35); Color lt = Theme.Mix(c, Color.White, 0.45);
		bool cs = b.Kind == 0 && Spr.Get("ms_cy1") != null;
		if (cs) { bool edge = Math.Abs(Math.Sin(b.Rot * 0.0175f * 0.5f)) > 0.82f; string cn = edge ? "ms_cy2" : "ms_cy1"; float cw = rx * 2.25f, chh = cw * (edge ? 49f / 62f : 57f / 74f); using (SolidBrush sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(sh, x - rx * 0.9f, y + ry * 0.9f, rx * 1.8f, ry * 0.5f); Spr.Draw(g, cn, x, y, cw, chh, tilt); if (b.Flash > 0) using (SolidBrush fb = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(fb, x - rx, y - ry, rx * 2, ry * 1.6f); }
		else {
		GraphicsState gs = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(tilt);
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(sh, -rx * 0.9f, ry * 0.9f, rx * 1.8f, ry * 0.5f);
		using (SolidBrush br = new SolidBrush(dk)) g.FillEllipse(br, -rx, -ry * 0.55f, rx * 2, ry * 1.55f);              // 아랫면 두께
		using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, -rx, -ry, rx * 2, ry * 1.7f);                       // 윗면
		using (SolidBrush br = new SolidBrush(lt)) g.FillEllipse(br, -rx * 0.55f, -ry * 0.78f, rx * 1.1f, ry * 0.8f);   // 돔
		using (SolidBrush br = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(br, -rx * 0.4f, -ry * 0.7f, rx * 0.5f, ry * 0.3f);
		g.Restore(gs); }
		int n = b.Lines.Length; float lh = 11f, y0 = y - n * lh / 2f - ry * 0.55f - 3f;
		for (int i = 0; i < n; i++)
		{
			Txt(g, b.Lines[i], 10f, 2, Color.FromArgb(150, 20, 20, 40), x - rx + 1, y0 + i * lh + 1, rx * 2, lh, StringAlignment.Center);
			Txt(g, b.Lines[i], 10f, 2, Color.White, x - rx, y0 + i * lh, rx * 2, lh, StringAlignment.Center);
		}
	}
}
