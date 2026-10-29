using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 5초 챌린지에 들어가는 추가 미니 미션 10종 ──
internal abstract class MTask
{
	public string Word = "", Hint = ""; public Color C1 = Color.FromArgb(120, 170, 230), C2 = Color.FromArgb(60, 90, 160); public float Dur = 4.6f; public bool TimeoutWins;
	protected Random R = new Random();
	public abstract void Init(int round);
	public virtual int Update(float dt, GameInput inp) { return 0; }
	public virtual int Click(float x, float y) { return 0; }
	public abstract void Draw(Graphics g, double t, float tl);
	protected static void T(Graphics g, string s, float pt, Color c, float x, float y, float w, float h) { MiniGame.Txt(g, s, pt, 2, c, x, y, w, h, StringAlignment.Center); }
	protected static void Pn(Graphics g, RectangleF r, Color c) { using (GraphicsPath gp = Gfx.Round(r, 16)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, gp); }
	public static MTask[] All() { return new MTask[] { new BigNumTask(), new OddColorTask(), new StampTask(), new LightTask(), new OrderTask(), new PhoneTask(), new RouletteTask(), new TypoTask(), new MathTask(), new ArrowTask(), new FindTask(), new CountTask(), new CatchTask(), new SizeTask(), new ClockTask(), new MemTask() }; }
}

internal sealed class BigNumTask : MTask
{
	private int a, b;
	public BigNumTask() { Word = "더 큰 수!"; Hint = "더 큰 숫자를 클릭!"; C1 = Color.FromArgb(255, 200, 120); C2 = Color.FromArgb(230, 120, 90); Dur = 3.2f; }
	public override void Init(int round) { int hi = round >= 6 ? 999 : 99; a = 10 + R.Next(hi - 10); do { b = 10 + R.Next(hi - 10); if (round >= 4) b = Math.Max(10, a + R.Next(-12, 13)); } while (b == a); }
	public override int Click(float x, float y) { bool left = x < 200f; return ((left && a > b) || (!left && b > a)) ? 1 : -1; }
	public override void Draw(Graphics g, double t, float tl)
	{
		Pn(g, new RectangleF(24, 220, 168, 180), Color.FromArgb(235, 255, 255, 255)); Pn(g, new RectangleF(208, 220, 168, 180), Color.FromArgb(235, 255, 255, 255));
		T(g, a.ToString(), 40f, Color.FromArgb(60, 50, 70), 24, 250, 168, 120); T(g, b.ToString(), 40f, Color.FromArgb(60, 50, 70), 208, 250, 168, 120);
	}
}

internal sealed class OddColorTask : MTask
{
	private int odd; private int hue; private int delta;
	public OddColorTask() { Word = "다른 색!"; Hint = "혼자 색이 다른 동그라미를 클릭!"; C1 = Color.FromArgb(150, 230, 190); C2 = Color.FromArgb(60, 140, 130); Dur = 4.8f; }
	public override void Init(int round) { odd = R.Next(16); hue = R.Next(360); delta = Math.Max(9, 44 - round * 2); }
	private static Color Hsv(int h, double s, double v) { h = ((h % 360) + 360) % 360; double c = v * s, x = c * (1 - Math.Abs((h / 60.0) % 2 - 1)), m = v - c; double r, g, b; if (h < 60) { r = c; g = x; b = 0; } else if (h < 120) { r = x; g = c; b = 0; } else if (h < 180) { r = 0; g = c; b = x; } else if (h < 240) { r = 0; g = x; b = c; } else if (h < 300) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; } return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255)); }
	private static PointF Cell(int i) { return new PointF(40f + (i % 4) * 80f + 40f, 170f + (i / 4) * 80f + 40f); }
	public override int Click(float x, float y) { for (int i = 0; i < 16; i++) { PointF c = Cell(i); if ((x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) < 34f * 34f) return i == odd ? 1 : -1; } return 0; }
	public override void Draw(Graphics g, double t, float tl)
	{
		for (int i = 0; i < 16; i++) { PointF c = Cell(i); using (SolidBrush b = new SolidBrush(Hsv(hue + (i == odd ? delta : 0), 0.65, 0.95))) g.FillEllipse(b, c.X - 32, c.Y - 32, 64, 64); }
	}
}

internal sealed class StampTask : MTask
{
	private float tx, ty, sp, ph; private float sx, sy;
	public StampTask() { Word = "쾅 찍어라!"; Hint = "도장이 동그라미 위에 올 때 클릭!"; C1 = Color.FromArgb(250, 160, 140); C2 = Color.FromArgb(190, 70, 90); Dur = 5f; }
	public override void Init(int round) { tx = 130 + (float)R.NextDouble() * 140; ty = 230 + (float)R.NextDouble() * 150; sp = 2.2f + round * 0.08f; ph = (float)R.NextDouble() * 6f; }
	private void Pos(float t) { sx = 200f + (float)Math.Sin(t * sp + ph) * 150f; sy = 330f + (float)Math.Sin(t * sp * 1.37f + ph * 0.5f) * 170f; }
	private float tm;
	public override int Update(float dt, GameInput inp) { tm += dt; Pos(tm); return 0; }
	public override int Click(float x, float y) { float dx = sx - tx, dy = sy - ty; return dx * dx + dy * dy < 30f * 30f ? 1 : -1; }
	public override void Draw(Graphics g, double t, float tl)
	{
		using (Pen p = new Pen(Color.FromArgb(240, 255, 255, 255), 4f)) g.DrawEllipse(p, tx - 34, ty - 34, 68, 68);
		using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawEllipse(p, tx - 16, ty - 16, 32, 32);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 200, 40, 60))) g.FillEllipse(b, sx - 26, sy - 26, 52, 52);
		T(g, "결재", 12f, Color.White, sx - 26, sy - 26, 52, 52);
	}
}

internal sealed class LightTask : MTask
{
	private float delay, tm; private bool green; private float gt;
	public LightTask() { Word = "초록불에!"; Hint = "초록불이 켜지면 바로 클릭! (빨간불에 누르면 실패)"; C1 = Color.FromArgb(120, 130, 160); C2 = Color.FromArgb(40, 44, 70); }
	public override void Init(int round) { delay = 1.0f + (float)R.NextDouble() * 2.0f; tm = 0; green = false; gt = 0; Dur = delay + Math.Max(0.6f, 1.2f - round * 0.03f); }
	public override int Update(float dt, GameInput inp) { tm += dt; if (!green && tm >= delay) green = true; return 0; }
	public override int Click(float x, float y) { return green ? 1 : -1; }
	public override void Draw(Graphics g, double t, float tl)
	{
		Pn(g, new RectangleF(140, 170, 120, 300), Color.FromArgb(230, 30, 32, 44));
		using (SolidBrush b = new SolidBrush(green ? Color.FromArgb(60, 70, 60) : Color.FromArgb(240, 60, 60))) g.FillEllipse(b, 160, 190, 80, 80);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 60, 50))) g.FillEllipse(b, 160, 280, 80, 80);
		using (SolidBrush b = new SolidBrush(green ? Color.FromArgb(80, 240, 120) : Color.FromArgb(40, 70, 50))) g.FillEllipse(b, 160, 370, 80, 80);
		if (green) T(g, "지금!", 26f, Color.FromArgb(255, 255, 160), 0, 490, 400, 40);
	}
}

internal sealed class OrderTask : MTask
{
	private readonly List<PointF> pos = new List<PointF>(); private int n, next;
	public OrderTask() { Word = "차례대로!"; Hint = "1부터 순서대로 눌러요!"; C1 = Color.FromArgb(170, 150, 240); C2 = Color.FromArgb(80, 60, 160); Dur = 5.2f; }
	public override void Init(int round)
	{
		n = 4 + Math.Min(2, round / 3); next = 1; pos.Clear();
		int guard = 0; while (pos.Count < n && guard++ < 400) { PointF p = new PointF(50 + (float)R.NextDouble() * 300, 180 + (float)R.NextDouble() * 300); bool ok = true; foreach (PointF q in pos) if ((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y) < 84f * 84f) ok = false; if (ok) pos.Add(p); }
		n = pos.Count;
	}
	public override int Click(float x, float y)
	{
		for (int i = 0; i < n; i++) { if (i + 1 < next) continue; PointF p = pos[i]; if ((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y) < 32f * 32f) { if (i + 1 == next) { next++; return next > n ? 1 : 0; } return -1; } }
		return 0;
	}
	public override void Draw(Graphics g, double t, float tl)
	{
		for (int i = 0; i < n; i++) { if (i + 1 < next) continue; PointF p = pos[i]; using (SolidBrush b = new SolidBrush(i + 1 == next ? Color.FromArgb(255, 226, 120) : Color.FromArgb(235, 255, 255, 255))) g.FillEllipse(b, p.X - 30, p.Y - 30, 60, 60); T(g, (i + 1).ToString(), 22f, Color.FromArgb(70, 50, 100), p.X - 30, p.Y - 30, 60, 60); }
	}
}

internal sealed class PhoneTask : MTask
{
	private float tm, bx, by; private float appear;
	public PhoneTask() { Word = "전화 받기!"; Hint = "초록 '받기'를 클릭! (빨간 '끊기'는 안 돼요)"; C1 = Color.FromArgb(120, 210, 230); C2 = Color.FromArgb(50, 110, 160); Dur = 3.8f; }
	public override void Init(int round) { tm = 0; appear = 0.5f + (float)R.NextDouble() * 0.5f; bx = 60 + (float)R.NextDouble() * 150; by = 380 + (float)R.NextDouble() * 80; }
	public override int Update(float dt, GameInput inp) { tm += dt; return 0; }
	public override int Click(float x, float y)
	{
		if (tm < appear) return 0;
		if (x > bx && x < bx + 110 && y > by && y < by + 60) return 1;
		float ex = 400f - bx - 110f; if (x > ex && x < ex + 110 && y > by && y < by + 60) return -1; return 0;
	}
	public override void Draw(Graphics g, double t, float tl)
	{
		float sh = (float)Math.Sin(t * 40) * 6f; GraphicsState st = g.Save(); g.TranslateTransform(200 + sh, 270); g.RotateTransform(sh * 1.5f);
		Pn(g, new RectangleF(-50, -80, 100, 160), Color.FromArgb(240, 40, 44, 60)); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 120, 200, 255))) g.FillRectangle(b, -40, -66, 80, 110); g.Restore(st);
		T(g, "띠리링~", 18f, Color.White, 0, 150, 400, 30);
		if (tm >= appear)
		{
			Pn(g, new RectangleF(bx, by, 110, 60), Color.FromArgb(240, 90, 210, 120)); T(g, "받기", 16f, Color.White, bx, by, 110, 60);
			float ex = 400f - bx - 110f; Pn(g, new RectangleF(ex, by, 110, 60), Color.FromArgb(240, 230, 80, 80)); T(g, "끊기", 16f, Color.White, ex, by, 110, 60);
		}
	}
}

internal sealed class RouletteTask : MTask
{
	private static readonly string[] Foods = { "김밥", "라면", "비빔밥", "국밥", "돈까스", "냉면" };
	private int target, cur; private float tm, step;
	public RouletteTask() { Word = "메뉴 룰렛!"; Hint = "오늘의 메뉴와 같을 때 클릭!"; C1 = Color.FromArgb(255, 200, 130); C2 = Color.FromArgb(220, 120, 70); Dur = 5.2f; }
	public override void Init(int round) { target = R.Next(Foods.Length); cur = R.Next(Foods.Length); tm = 0; step = Math.Max(0.07f, 0.17f - round * 0.005f); }
	public override int Update(float dt, GameInput inp) { tm += dt; if (tm >= step) { tm -= step; cur = (cur + 1) % Foods.Length; } return 0; }
	public override int Click(float x, float y) { return cur == target ? 1 : -1; }
	public override void Draw(Graphics g, double t, float tl)
	{
		T(g, "오늘의 메뉴", 13f, Color.FromArgb(230, 255, 255, 255), 0, 160, 400, 24); T(g, Foods[target], 30f, Color.FromArgb(255, 240, 140), 0, 188, 400, 50);
		Pn(g, new RectangleF(70, 290, 260, 130), Color.FromArgb(240, 255, 255, 255)); T(g, Foods[cur], 36f, Color.FromArgb(70, 50, 50), 70, 300, 260, 110);
	}
}

internal sealed class TypoTask : MTask
{
	private static readonly string[][] Pairs = { new[] { "결재", "결제" }, new[] { "민원", "민언" }, new[] { "업무", "엄무" }, new[] { "예산", "예살" }, new[] { "기안", "기한" }, new[] { "공문", "공문서" }, new[] { "시행", "시향" }, new[] { "협조", "협죠" } };
	private int odd; private string a, b;
	public TypoTask() { Word = "오탈자!"; Hint = "혼자 다른 글자를 클릭!"; C1 = Color.FromArgb(150, 190, 240); C2 = Color.FromArgb(60, 90, 170); Dur = 4.2f; }
	public override void Init(int round) { string[] p = Pairs[R.Next(Pairs.Length)]; if (R.Next(2) == 0) { a = p[0]; b = p[1]; } else { a = p[1]; b = p[0]; } odd = R.Next(6); }
	private static RectangleF Cl(int i) { return new RectangleF(30 + (i % 3) * 118, 230 + (i / 3) * 110, 108, 90); }
	public override int Click(float x, float y) { for (int i = 0; i < 6; i++) if (Cl(i).Contains(x, y)) return i == odd ? 1 : -1; return 0; }
	public override void Draw(Graphics g, double t, float tl) { for (int i = 0; i < 6; i++) { Pn(g, Cl(i), Color.FromArgb(240, 255, 255, 255)); T(g, i == odd ? b : a, 22f, Color.FromArgb(50, 60, 90), Cl(i).X, Cl(i).Y, 108, 90); } }
}

internal sealed class MathTask : MTask
{
	private int x, y, ans; private int[] ch = new int[3]; private int ci; private char op;
	public MathTask() { Word = "계산!"; Hint = "답이 맞는 버튼을 클릭!"; C1 = Color.FromArgb(130, 200, 150); C2 = Color.FromArgb(50, 120, 90); Dur = 4.2f; }
	public override void Init(int round)
	{
		int m = round >= 6 ? 30 : 15; x = 3 + R.Next(m); y = 2 + R.Next(m); op = R.Next(3) == 0 ? '-' : (round >= 3 && R.Next(3) == 0 ? '×' : '+');
		if (op == '-') { if (y > x) { int t = x; x = y; y = t; } ans = x - y; } else if (op == '×') { x = 2 + R.Next(8); y = 2 + R.Next(8); ans = x * y; } else ans = x + y;
		ci = R.Next(3); for (int i = 0; i < 3; i++) { if (i == ci) { ch[i] = ans; continue; } int d; do { d = ans + R.Next(-6, 7); } while (d == ans || d < 0 || Array.IndexOf(ch, d) >= 0 && Array.IndexOf(ch, d) < i); ch[i] = d; }
	}
	private static RectangleF Bt(int i) { return new RectangleF(24 + i * 124, 380, 108, 80); }
	public override int Click(float cx, float cy) { for (int i = 0; i < 3; i++) if (Bt(i).Contains(cx, cy)) return i == ci ? 1 : -1; return 0; }
	public override void Draw(Graphics g, double t, float tl)
	{
		T(g, x + " " + op + " " + y + " = ?", 38f, Color.White, 0, 220, 400, 70);
		for (int i = 0; i < 3; i++) { Pn(g, Bt(i), Color.FromArgb(240, 255, 255, 255)); T(g, ch[i].ToString(), 26f, Color.FromArgb(40, 80, 60), Bt(i).X, Bt(i).Y, 108, 80); }
	}
}

internal sealed class ArrowTask : MTask
{
	private int dir; private bool rev;
	public ArrowTask() { Word = "방향!"; Hint = "화살표가 가리키는 쪽을 클릭!"; C1 = Color.FromArgb(240, 170, 200); C2 = Color.FromArgb(170, 80, 130); Dur = 3.4f; }
	public override void Init(int round) { dir = R.Next(4); rev = round >= 5 && R.Next(2) == 0; Hint = rev ? "반대 방향을 클릭!" : "화살표가 가리키는 쪽을 클릭!"; }
	private static RectangleF Zn(int i) { return i == 0 ? new RectangleF(140, 130, 120, 70) : (i == 1 ? new RectangleF(250, 260, 120, 120) : (i == 2 ? new RectangleF(140, 440, 120, 70) : new RectangleF(30, 260, 120, 120))); }
	public override int Click(float x, float y) { int want = rev ? (dir + 2) % 4 : dir; for (int i = 0; i < 4; i++) if (Zn(i).Contains(x, y)) return i == want ? 1 : -1; return 0; }
	public override void Draw(Graphics g, double t, float tl)
	{
		string[] lb = { "위", "오른쪽", "아래", "왼쪽" };
		for (int i = 0; i < 4; i++) { Pn(g, Zn(i), Color.FromArgb(90, 255, 255, 255)); T(g, lb[i], 14f, Color.White, Zn(i).X, Zn(i).Y, Zn(i).Width, Zn(i).Height); }
		GraphicsState st = g.Save(); g.TranslateTransform(200, 320); g.RotateTransform(dir * 90f);
		using (SolidBrush b = new SolidBrush(rev ? Color.FromArgb(255, 150, 150) : Color.FromArgb(255, 240, 150))) g.FillPolygon(b, new PointF[] { new PointF(0, -60), new PointF(44, -6), new PointF(18, -6), new PointF(18, 50), new PointF(-18, 50), new PointF(-18, -6), new PointF(-44, -6) });
		g.Restore(st);
		if (rev) T(g, "반대!", 22f, Color.FromArgb(255, 200, 200), 0, 120, 400, 30);
	}
}


internal static class OfI
{
	public static readonly string[] N = { "ms_of_bind", "ms_of_cal", "ms_of_clip", "ms_of_clock", "ms_of_mug", "ms_of_note", "ms_of_paper", "ms_of_pc", "ms_of_stap", "ms_of_tel" };
	public static readonly string[] K = { "바인더", "달력", "클립", "시계", "머그컵", "포스트잇", "서류", "컴퓨터", "스테이플러", "전화기" };
	public static void Ico(Graphics g, int i, float cx, float cy, float box)
	{
		Bitmap b = Spr.Get(N[i]); if (b == null) { using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 120 + i * 12, 150, 220 - i * 10))) g.FillEllipse(br, cx - box / 2, cy - box / 2, box, box); return; }
		float w = box, h = box * b.Height / b.Width; if (h > box) { h = box; w = box * b.Width / b.Height; } Spr.Draw(g, N[i], cx, cy, w, h, 0f);
	}
	public static int[] Shuffle(Random r, int n) { int[] a = new int[n]; for (int i = 0; i < n; i++) a[i] = i; for (int i = n - 1; i > 0; i--) { int j = r.Next(i + 1); int t = a[i]; a[i] = a[j]; a[j] = t; } return a; }
}

internal sealed class FindTask : MTask
{
	private int tgt; private int[] cell = new int[12];
	public FindTask() { Word = "찾아라!"; Hint = "위에 보이는 물건을 찾아 클릭!"; C1 = Color.FromArgb(160, 210, 250); C2 = Color.FromArgb(70, 120, 190); Dur = 4.6f; }
	public override void Init(int round)
	{
		int[] p = OfI.Shuffle(R, 10); tgt = p[0]; int cnt = Math.Min(12, 8 + round / 2);
		for (int i = 0; i < 12; i++) cell[i] = i < cnt ? p[(i % 9) + 1] : -1; int[] o = OfI.Shuffle(R, 12); int[] c2 = new int[12]; for (int i = 0; i < 12; i++) c2[o[i]] = cell[i]; cell = c2;
		int place = R.Next(12); while (cell[place] < 0 && false) place = R.Next(12); cell[place] = tgt;
	}
	private static PointF Pos(int i) { return new PointF(70f + (i % 3) * 130f, 230f + (i / 3) * 84f); }
	public override int Click(float x, float y) { for (int i = 0; i < 12; i++) { PointF c = Pos(i); if (Math.Abs(x - c.X) < 46 && Math.Abs(y - c.Y) < 38) return cell[i] == tgt && cell[i] >= 0 ? 1 : (cell[i] < 0 ? 0 : -1); } return 0; }
	public override void Draw(Graphics g, double t, float tl)
	{
		Pn(g, new RectangleF(110, 150, 180, 52), Color.FromArgb(230, 255, 255, 255)); OfI.Ico(g, tgt, 150, 176, 38); T(g, OfI.K[tgt], 15f, Color.FromArgb(60, 50, 70), 170, 158, 120, 36);
		for (int i = 0; i < 12; i++) { PointF c = Pos(i); if (cell[i] < 0) continue; Pn(g, new RectangleF(c.X - 46, c.Y - 36, 92, 72), Color.FromArgb(150, 255, 255, 255)); OfI.Ico(g, cell[i], c.X, c.Y, 52); }
	}
}

internal sealed class CountTask : MTask
{
	private int tgt, n; private float[] px = new float[14], py = new float[14]; private int[] it = new int[14]; private int m; private int[] ch = new int[3]; private int ans;
	public CountTask() { Word = "몇 개?"; Hint = "위 물건이 몇 개인지 아래에서 고르기!"; C1 = Color.FromArgb(250, 220, 140); C2 = Color.FromArgb(210, 140, 70); Dur = 4.8f; }
	public override void Init(int round)
	{
		tgt = R.Next(10); n = 2 + R.Next(Math.Min(6, 3 + round / 2)); int dec = 3 + R.Next(4); m = n + dec;
		for (int i = 0; i < m; i++) { it[i] = i < n ? tgt : (tgt + 1 + R.Next(9)) % 10; for (int tries = 0; tries < 30; tries++) { px[i] = 50f + (float)R.NextDouble() * 300f; py[i] = 200f + (float)R.NextDouble() * 200f; bool ok = true; for (int j = 0; j < i; j++) if ((px[i] - px[j]) * (px[i] - px[j]) + (py[i] - py[j]) * (py[i] - py[j]) < 52f * 52f) ok = false; if (ok) break; }}
		int[] o = OfI.Shuffle(R, m); int[] i2 = new int[14]; for (int i = 0; i < m; i++) i2[o[i]] = it[i]; for (int i = 0; i < m; i++) it[i] = i2[i];
		ans = R.Next(3); for (int i = 0; i < 3; i++) ch[i] = Math.Max(1, n + (i - ans)); if (n == 1 || n + (0 - ans) < 1) { for (int i = 0; i < 3; i++) ch[i] = n - ans + i < 1 ? n + 3 - ans + i : n - ans + i; }
		int pos = -1; for (int i = 0; i < 3; i++) if (ch[i] == n) pos = i; if (pos < 0) { ch[0] = n; }
	}
	private RectangleF Btn(int i) { return new RectangleF(40f + i * 112f, 470f, 96f, 60f); }
	public override int Click(float x, float y) { for (int i = 0; i < 3; i++) if (Btn(i).Contains(x, y)) return ch[i] == n ? 1 : -1; return 0; }
	public override void Draw(Graphics g, double t, float tl)
	{
		Pn(g, new RectangleF(110, 140, 180, 46), Color.FromArgb(230, 255, 255, 255)); OfI.Ico(g, tgt, 150, 163, 34); T(g, OfI.K[tgt] + " 몇 개?", 13f, Color.FromArgb(60, 50, 70), 168, 146, 126, 34);
		for (int i = 0; i < m; i++) OfI.Ico(g, it[i], px[i], py[i], 44);
		for (int i = 0; i < 3; i++) { Pn(g, Btn(i), Color.FromArgb(235, 255, 255, 255)); T(g, ch[i].ToString(), 26f, Color.FromArgb(60, 50, 70), Btn(i).X, Btn(i).Y, Btn(i).Width, Btn(i).Height); }
	}
}

internal sealed class CatchTask : MTask
{
	private float[] x = new float[8], y = new float[8], v = new float[8]; private bool[] good = new bool[8], gone = new bool[8]; private int got; private float tm, spT;
	public CatchTask() { Word = "코인만!"; Hint = "떨어지는 동전 4개 클릭! (상자는 누르면 안 돼요)"; C1 = Color.FromArgb(255, 226, 130); C2 = Color.FromArgb(220, 150, 60); Dur = 5.2f; }
	public override void Init(int round) { got = 0; tm = 0f; spT = 0f; for (int i = 0; i < 8; i++) gone[i] = true; }
	public override int Update(float dt, GameInput inp)
	{
		tm += dt; spT -= dt;
		if (spT <= 0f) { for (int i = 0; i < 8; i++) if (gone[i]) { gone[i] = false; x[i] = 50f + (float)R.NextDouble() * 300f; y[i] = 110f; v[i] = 190f + (float)R.NextDouble() * 80f + tm * 18f; good[i] = R.NextDouble() < 0.62; break; } spT = 0.42f; }
		for (int i = 0; i < 8; i++) if (!gone[i]) { y[i] += v[i] * dt; if (y[i] > 600f) gone[i] = true; }
		return 0;
	}
	public override int Click(float cx, float cy)
	{
		int bi = -1; float bd = 1e9f; for (int i = 0; i < 8; i++) if (!gone[i]) { float d = (cx - x[i]) * (cx - x[i]) + (cy - y[i]) * (cy - y[i]); if (d < 40f * 40f && d < bd) { bd = d; bi = i; } }
		if (bi < 0) return 0; gone[bi] = true; if (!good[bi]) return -1; got++; return got >= 4 ? 1 : 0;
	}
	public override void Draw(Graphics g, double t, float tl)
	{
		T(g, got + " / 4", 20f, Color.White, 0, 120, 400, 36);
		for (int i = 0; i < 8; i++) if (!gone[i]) { if (Spr.Get(good[i] ? "ms_coins" : "ms_crate") != null) Spr.Draw(g, good[i] ? "ms_coins" : "ms_crate", x[i], y[i], 62, 52, (float)Math.Sin(tm * 5 + i) * 8f); else using (SolidBrush b = new SolidBrush(good[i] ? Color.Gold : Color.SaddleBrown)) g.FillEllipse(b, x[i] - 24, y[i] - 24, 48, 48); }
	}
}

internal sealed class SizeTask : MTask
{
	private int[] it = new int[3]; private float[] sz = new float[3]; private bool[] done = new bool[3]; private int next; private float[] px = { 80f, 200f, 320f };
	public SizeTask() { Word = "작은 것부터!"; Hint = "작은 것 → 큰 것 순서로 클릭!"; C1 = Color.FromArgb(200, 180, 250); C2 = Color.FromArgb(110, 90, 190); Dur = 4.4f; }
	public override void Init(int round)
	{
		int[] p = OfI.Shuffle(R, 10); float[] s = { 56f, 84f, 118f }; if (round >= 5) s = new float[] { 70f, 82f, 98f }; int[] o = OfI.Shuffle(R, 3); next = 0;
		for (int i = 0; i < 3; i++) { it[i] = p[i]; sz[i] = s[o[i]]; done[i] = false; }
	}
	public override int Click(float x, float y)
	{
		for (int i = 0; i < 3; i++) if (!done[i] && Math.Abs(x - px[i]) < 60 && Math.Abs(y - 330f) < 70) { int rank = 0; for (int j = 0; j < 3; j++) if (sz[j] < sz[i]) rank++; if (rank != next) return -1; done[i] = true; next++; return next >= 3 ? 1 : 0; }
		return 0;
	}
	public override void Draw(Graphics g, double t, float tl)
	{
		for (int i = 0; i < 3; i++) { if (done[i]) { T(g, "✔", 28f, Color.FromArgb(120, 255, 170), px[i] - 40, 300, 80, 60); continue; } Pn(g, new RectangleF(px[i] - 56, 270, 112, 120), Color.FromArgb(140, 255, 255, 255)); OfI.Ico(g, it[i], px[i], 330, sz[i] * 0.85f); }
	}
}

internal sealed class ClockTask : MTask
{
	private int h, m, ans; private int[] ch = new int[3];
	public ClockTask() { Word = "몇 시?"; Hint = "시계가 가리키는 시간을 고르기!"; C1 = Color.FromArgb(170, 230, 210); C2 = Color.FromArgb(60, 140, 130); Dur = 4.6f; }
	public override void Init(int round)
	{
		h = 1 + R.Next(12); m = R.Next(round >= 4 ? 4 : 2) * (round >= 4 ? 15 : 30); int v = h * 60 + m; ans = R.Next(3); int[] d = { 0, 0, 0 };
		for (int i = 0; i < 3; i++) { if (i == ans) { ch[i] = v; continue; } int cand; bool ok; do { cand = ((h - 1 + R.Next(-3, 4) + 12) % 12 + 1) * 60 + (R.NextDouble() < 0.5 ? m : (m + 30) % 60); ok = cand != v; for (int j = 0; j < i; j++) if (ch[j] == cand) ok = false; } while (!ok); ch[i] = cand; }
	}
	private RectangleF Btn(int i) { return new RectangleF(30f + i * 118f, 470f, 104f, 54f); }
	public override int Click(float x, float y) { for (int i = 0; i < 3; i++) if (Btn(i).Contains(x, y)) return ch[i] == h * 60 + m ? 1 : -1; return 0; }
	public override void Draw(Graphics g, double t, float tl)
	{
		float cx = 200f, cy = 320f, r = 100f;
		using (SolidBrush b = new SolidBrush(Color.FromArgb(245, 255, 255, 255))) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2); using (Pen p = new Pen(Color.FromArgb(70, 80, 100), 5f)) g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2);
		for (int k = 0; k < 12; k++) { double a = k * Math.PI / 6; using (Pen p = new Pen(Color.FromArgb(70, 80, 100), k % 3 == 0 ? 4f : 2f)) g.DrawLine(p, cx + (float)Math.Sin(a) * (r - 6), cy - (float)Math.Cos(a) * (r - 6), cx + (float)Math.Sin(a) * (r - (k % 3 == 0 ? 18 : 12)), cy - (float)Math.Cos(a) * (r - (k % 3 == 0 ? 18 : 12))); }
		double ha = ((h % 12) + m / 60.0) * Math.PI / 6, ma = m * Math.PI / 30;
		using (Pen p = new Pen(Color.FromArgb(40, 50, 80), 7f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, cx, cy, cx + (float)Math.Sin(ha) * 52f, cy - (float)Math.Cos(ha) * 52f);
		using (Pen p = new Pen(Color.FromArgb(200, 70, 70), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, cx, cy, cx + (float)Math.Sin(ma) * 78f, cy - (float)Math.Cos(ma) * 78f);
		for (int i = 0; i < 3; i++) { Pn(g, Btn(i), Color.FromArgb(235, 255, 255, 255)); int hh = ch[i] / 60, mm = ch[i] % 60; T(g, hh + ":" + mm.ToString("00"), 20f, Color.FromArgb(60, 50, 70), Btn(i).X, Btn(i).Y, Btn(i).Width, Btn(i).Height); }
	}
}

internal sealed class MemTask : MTask
{
	private int[] show = new int[2]; private int[] cell = new int[6]; private bool[] hit = new bool[6]; private float tm; private int found;
	public MemTask() { Word = "기억해!"; Hint = "잠깐 보인 물건 2개를 기억해서 찾기!"; C1 = Color.FromArgb(250, 190, 220); C2 = Color.FromArgb(180, 90, 140); Dur = 6.2f; }
	public override void Init(int round) { int[] p = OfI.Shuffle(R, 10); show[0] = p[0]; show[1] = p[1]; int[] q = { p[0], p[1], p[2], p[3], p[4], p[5] }; int[] o = OfI.Shuffle(R, 6); for (int i = 0; i < 6; i++) { cell[o[i]] = q[i]; hit[i] = false; } tm = 0f; found = 0; }
	public override int Update(float dt, GameInput inp) { tm += dt; return 0; }
	private static PointF Pos(int i) { return new PointF(70f + (i % 3) * 130f, 360f + (i / 3) * 100f); }
	public override int Click(float x, float y)
	{
		if (tm < 1.5f) return 0;
		for (int i = 0; i < 6; i++) { PointF c = Pos(i); if (!hit[i] && Math.Abs(x - c.X) < 52 && Math.Abs(y - c.Y) < 44) { if (cell[i] != show[0] && cell[i] != show[1]) return -1; hit[i] = true; found++; return found >= 2 ? 1 : 0; } }
		return 0;
	}
	public override void Draw(Graphics g, double t, float tl)
	{
		if (tm < 1.5f) { T(g, "잘 보세요!", 18f, Color.White, 0, 170, 400, 34); Pn(g, new RectangleF(70, 220, 260, 120), Color.FromArgb(235, 255, 255, 255)); OfI.Ico(g, show[0], 140, 280, 80); OfI.Ico(g, show[1], 260, 280, 80); }
		else T(g, "아까 본 2개는?  " + found + " / 2", 15f, Color.White, 0, 190, 400, 34);
		for (int i = 0; i < 6; i++) { PointF c = Pos(i); Pn(g, new RectangleF(c.X - 52, c.Y - 44, 104, 88), Color.FromArgb(tm < 1.5f ? 70 : 150, 255, 255, 255)); if (tm >= 1.5f) { if (hit[i]) { OfI.Ico(g, cell[i], c.X, c.Y, 56); T(g, "✔", 22f, Color.FromArgb(120, 255, 170), c.X - 20, c.Y - 40, 40, 30); } else OfI.Ico(g, cell[i], c.X, c.Y, 56); } }
	}
}
