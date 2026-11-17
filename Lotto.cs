using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

// ── 재미로 보는 로또 추첨기 ─────────────────────────────────────────────────────────────
// 유리 구체 안에서 1~45번 공이 3D로 섞이고, 한 개씩 위로 빨려 올라가 관을 타고 내려와 아래 트레이에 채워져요.
// 통·관·유리 반사는 한 번만 그려 두고(레이어), 공만 매 프레임 움직여서 가볍게 돌아가요. (재미용)
internal sealed class LottoForm : Form
{
	private static LottoForm inst; private readonly float sc = FUi.Scale(420, 620);
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new LottoForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }

	private sealed class Ball { public int N; public float X, Y, Z, Vx, Vy, Vz; }
	private sealed class PBall { public int N, Slot; public float S; }
	private sealed class Path
	{
		public List<PointF> P = new List<PointF>(); public float[] C; public float Len;
		public void Fin() { C = new float[P.Count]; for (int i = 1; i < P.Count; i++) C[i] = C[i - 1] + (float)Math.Sqrt((P[i].X - P[i - 1].X) * (P[i].X - P[i - 1].X) + (P[i].Y - P[i - 1].Y) * (P[i].Y - P[i - 1].Y)); Len = C[C.Length - 1]; }
		public PointF At(float s)
		{
			if (s <= 0) return P[0]; if (s >= Len) return P[P.Count - 1]; int lo = 0, hi = C.Length - 1; while (hi - lo > 1) { int m = (lo + hi) / 2; if (C[m] <= s) lo = m; else hi = m; }
			float u = (s - C[lo]) / Math.Max(0.001f, C[hi] - C[lo]); return new PointF(P[lo].X + (P[hi].X - P[lo].X) * u, P[lo].Y + (P[hi].Y - P[lo].Y) * u);
		}
	}

	private const float MX = 180f, MY = 250f, MR = 118f, BR = 16f, TUBE = 38f, RAIL = 452f, SLOTY = 508f, PIPEX = 396f, TOPY = 76f;
	private readonly List<Ball> pool = new List<Ball>(); private readonly List<PBall> pipe = new List<PBall>(); private readonly List<int> drawn = new List<int>();
	private readonly Random rnd = new Random(); private readonly Timer tm = new Timer(); private readonly Path[] slotPath = new Path[7]; private Path railPath;
	private Ball pull; private float pullT, pullCd; private bool running, finished; private float tAir;
	private RectangleF btn = new RectangleF(120, 568, 180, 38);
	private Bitmap layBack, layGlass, layPipe; private bool layDark; private readonly Bitmap[] spr = new Bitmap[46];
	private readonly float[][] jets = new float[16][];

	public LottoForm()
	{
		Text = "로또 추첨기 (재미용)"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(420 * sc), (int)(620 * sc)); DoubleBuffered = true; BackColor = Theme.Bg;
		BuildPaths(); Reset(); tm.Interval = 16; tm.Tick += delegate { try { Step(0.016f); Invalidate(); } catch (Exception ex) { Program.LogErr(ex); tm.Stop(); } }; tm.Start();
		FormClosed += delegate { tm.Stop(); tm.Dispose(); if (layBack != null) layBack.Dispose(); if (layGlass != null) layGlass.Dispose(); if (layPipe != null) layPipe.Dispose(); for (int i = 0; i < spr.Length; i++) if (spr[i] != null) spr[i].Dispose(); };
		MouseDown += delegate(object s, MouseEventArgs e) { if (btn.Contains(e.X / sc, e.Y / sc)) { if (finished || (!running && drawn.Count == 0)) { if (finished) Reset(); running = true; pullCd = 1.0f; try { Sfx.Play("whoosh"); } catch { } } } };
	}

	// 둥근 모서리 꺾은선 (관 중심선)
	private static Path Rounded(PointF[] c, float r)
	{
		Path p = new Path(); p.P.Add(c[0]);
		for (int i = 1; i < c.Length - 1; i++)
		{
			PointF a = c[i - 1], b = c[i], d = c[i + 1]; float l1 = Dist(a, b), l2 = Dist(b, d), rr = Math.Min(r, Math.Min(l1, l2) * 0.5f);
			PointF u1 = new PointF((b.X - a.X) / l1, (b.Y - a.Y) / l1), u2 = new PointF((d.X - b.X) / l2, (d.Y - b.Y) / l2);
			PointF s = new PointF(b.X - u1.X * rr, b.Y - u1.Y * rr), e = new PointF(b.X + u2.X * rr, b.Y + u2.Y * rr); p.P.Add(s);
			for (int k = 1; k <= 8; k++) { float t = k / 8f, w0 = (1 - t) * (1 - t), w1 = 2 * (1 - t) * t, w2 = t * t; p.P.Add(new PointF(w0 * s.X + w1 * b.X + w2 * e.X, w0 * s.Y + w1 * b.Y + w2 * e.Y)); }
		}
		p.P.Add(c[c.Length - 1]); p.Fin(); return p;
	}
	private static float Dist(PointF a, PointF b) { return (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)); }
	private static float SlotX(int i) { return i < 6 ? 46f + i * 52f : 368f; }
	private void BuildPaths()
	{
		PointF p0 = new PointF(MX, MY - MR + BR + 4f);
		for (int i = 0; i < 7; i++) { float sx = SlotX(i); slotPath[i] = Rounded(new[] { p0, new PointF(MX, TOPY), new PointF(PIPEX, TOPY), new PointF(PIPEX, RAIL), new PointF(sx, RAIL), new PointF(sx, SLOTY) }, 26f); }
		railPath = Rounded(new[] { new PointF(MX, MY - MR + 6f), new PointF(MX, TOPY), new PointF(PIPEX, TOPY), new PointF(PIPEX, RAIL), new PointF(26f, RAIL) }, 26f);
	}
	private void Reset()
	{
		pool.Clear(); pipe.Clear(); drawn.Clear(); pull = null; running = false; finished = false;
		for (int i = 1; i <= 45; i++)
		{
			double a = rnd.NextDouble() * 6.283, u = rnd.NextDouble() * 2 - 1; float rr = (float)Math.Pow(rnd.NextDouble(), 0.33) * (MR - BR - 4f), s = (float)Math.Sqrt(1 - u * u);
			pool.Add(new Ball { N = i, X = (float)(Math.Cos(a) * s) * rr, Y = (float)u * rr * 0.6f + 40f, Z = (float)(Math.Sin(a) * s) * rr, Vx = (float)(rnd.NextDouble() - 0.5) * 200f, Vy = (float)(rnd.NextDouble() - 0.5) * 200f, Vz = (float)(rnd.NextDouble() - 0.5) * 200f });
		}
		for (int i = 0; i < jets.Length; i++) jets[i] = new float[] { (float)(rnd.NextDouble() - 0.5) * 120f, (float)rnd.NextDouble(), 0.6f + (float)rnd.NextDouble() * 0.8f, (float)(rnd.NextDouble() - 0.5) * 80f };
	}

	private void Step(float dt)
	{
		tAir += dt; float lim = MR - BR - 1f; float stir = running ? 1f : 0.32f;
		for (int i = 0; i < pool.Count; i++)
		{
			Ball b = pool[i]; if (b == pull) continue;
			b.Vy += 330f * dt;
			float fy = (b.Y + lim * 0.1f) / (lim * 1.1f); if (fy < 0) fy = 0; if (fy > 1) fy = 1;   // 아래쪽일수록 바람이 세요
			b.Vy -= (900f + (float)rnd.NextDouble() * 1300f) * dt * stir * fy;
			b.Vx += ((float)(rnd.NextDouble() - 0.5) * 900f - b.Z * 5f * stir) * dt * stir; b.Vz += ((float)(rnd.NextDouble() - 0.5) * 900f + b.X * 5f * stir) * dt * stir;
			float sp = (float)Math.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy + b.Vz * b.Vz), mx = running ? 520f : 250f; if (sp > mx) { float k = mx / sp; b.Vx *= k; b.Vy *= k; b.Vz *= k; }
			b.X += b.Vx * dt; b.Y += b.Vy * dt; b.Z += b.Vz * dt;
			float d = (float)Math.Sqrt(b.X * b.X + b.Y * b.Y + b.Z * b.Z);
			if (d > lim) { float nx = b.X / d, ny = b.Y / d, nz = b.Z / d; b.X = nx * lim; b.Y = ny * lim; b.Z = nz * lim; float vn = b.Vx * nx + b.Vy * ny + b.Vz * nz; if (vn > 0) { b.Vx -= 1.8f * vn * nx; b.Vy -= 1.8f * vn * ny; b.Vz -= 1.8f * vn * nz; } }
		}
		float r2 = BR * 2f;
		for (int i = 0; i < pool.Count; i++)
		{
			Ball a = pool[i]; if (a == pull) continue;
			for (int j = i + 1; j < pool.Count; j++)
			{
				Ball c = pool[j]; if (c == pull) continue; float dx = c.X - a.X; if (dx > r2 || dx < -r2) continue; float dy = c.Y - a.Y; if (dy > r2 || dy < -r2) continue; float dz = c.Z - a.Z; if (dz > r2 || dz < -r2) continue;
				float d2 = dx * dx + dy * dy + dz * dz; if (d2 >= r2 * r2 || d2 < 0.0001f) continue; float d = (float)Math.Sqrt(d2), nx = dx / d, ny = dy / d, nz = dz / d, push = (r2 - d) * 0.5f;
				a.X -= nx * push; a.Y -= ny * push; a.Z -= nz * push; c.X += nx * push; c.Y += ny * push; c.Z += nz * push;
				float rv = (c.Vx - a.Vx) * nx + (c.Vy - a.Vy) * ny + (c.Vz - a.Vz) * nz; if (rv < 0) { a.Vx += rv * nx; a.Vy += rv * ny; a.Vz += rv * nz; c.Vx -= rv * nx; c.Vy -= rv * ny; c.Vz -= rv * nz; }
			}
		}
		if (running)
		{
			if (pull != null)
			{
				pullT += dt; float sp = 90f + pullT * 620f, tx = 0f, ty = -lim, tz = 0f, dx = tx - pull.X, dy = ty - pull.Y, dz = tz - pull.Z, d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz), st = Math.Min(d, sp * dt);
				if (d > 0.01f) { pull.X += dx / d * st; pull.Y += dy / d * st; pull.Z += dz / d * st; }
				if (d - st < 2.5f) { pool.Remove(pull); pipe.Add(new PBall { N = pull.N, Slot = drawn.Count + pipe.Count, S = 0f }); pull = null; pullCd = 0.25f; }
			}
			else if (drawn.Count + pipe.Count < 7)
			{
				pullCd -= dt; bool room = pipe.Count == 0 || pipe[pipe.Count - 1].S > 150f;
				if (pullCd <= 0f && room && pool.Count > 0) { Ball best = null; foreach (Ball b in pool) if (best == null || b.Y < best.Y) best = b; pull = best; pullT = 0f; try { Sfx.Play("tick"); } catch { } }
			}
			for (int i = pipe.Count - 1; i >= 0; i--)
			{
				PBall pb = pipe[i]; Path pt = slotPath[pb.Slot]; pb.S += (pb.S < 60f ? 300f : 400f) * dt;
				if (pb.S >= pt.Len) { drawn.Add(pb.N); pipe.RemoveAt(i); try { Sfx.Play(drawn.Count == 7 ? "fanfare" : "ok"); } catch { } if (drawn.Count >= 7) { running = false; finished = true; } }
			}
		}
		for (int i = 0; i < jets.Length; i++) { float[] j = jets[i]; j[1] -= dt * j[2] * (running ? 1.3f : 0.4f); if (j[1] < 0f) { j[1] = 1f; j[0] = (float)(rnd.NextDouble() - 0.5) * 120f; j[2] = 0.6f + (float)rnd.NextDouble() * 0.8f; j[3] = (float)(rnd.NextDouble() - 0.5) * 80f; } }
	}

	// ───────── 정적 레이어 ─────────
	private void BuildLayers()
	{
		layDark = Theme.Dark; if (layBack != null) layBack.Dispose(); if (layGlass != null) layGlass.Dispose(); if (layPipe != null) layPipe.Dispose();
		int W = (int)(420 * sc), H = (int)(620 * sc);
		layBack = new Bitmap(W, H, PixelFormat.Format32bppPArgb); layGlass = new Bitmap(W, H, PixelFormat.Format32bppPArgb); layPipe = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
		Color glassC = Color.FromArgb(120, 170, 225), tubeIn = Theme.Mix(Theme.Bg, glassC, Theme.Dark ? 0.22 : 0.2), tubeOut = Theme.Mix(Theme.Bg, Color.FromArgb(70, 110, 170), Theme.Dark ? 0.8 : 0.55);
		using (Graphics g = Graphics.FromImage(layBack))
		{
			Gfx.HQ(g); g.Clear(Theme.Bg); g.ScaleTransform(sc, sc);
			// 바닥 그림자 + 받침대(원통)
			using (GraphicsPath sp = new GraphicsPath()) { sp.AddEllipse(MX - 118, 392, 236, 30); using (PathGradientBrush pb = new PathGradientBrush(sp)) { pb.CenterColor = Color.FromArgb(Theme.Dark ? 120 : 70, 0, 0, 0); pb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) }; g.FillPath(pb, sp); } }
			Color red = Color.FromArgb(206, 66, 76), redD = Theme.Mix(red, Color.Black, 0.45), redL = Theme.Mix(red, Color.White, 0.5);
			RectangleF body = new RectangleF(MX - 98, 360, 196, 40);
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(body.X, body.Y, body.Width, 1), redD, redD, 0f)) { ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.22f, 0.5f, 1f }; cb.Colors = new[] { redD, red, redL, redD }; lb.InterpolationColors = cb; g.FillRectangle(lb, body); g.FillEllipse(lb, body.X, body.Bottom - 9, body.Width, 18); }
			using (SolidBrush b = new SolidBrush(redL)) g.FillEllipse(b, body.X, body.Y - 9, body.Width, 18);
			using (SolidBrush b = new SolidBrush(Theme.Mix(red, Color.Black, 0.25))) g.FillEllipse(b, body.X + 14, body.Y - 6, body.Width - 28, 12);
			using (Pen p = new Pen(Color.FromArgb(200, 238, 196, 80), 3f)) g.DrawArc(p, body.X + 3, body.Bottom - 8, body.Width - 6, 14, 10, 160);
			// 관 몸통 (불투명)
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddLines(railPath.P.ToArray()); using (Pen p = new Pen(tubeOut, TUBE + 4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(p, gp); using (Pen p = new Pen(tubeIn, TUBE) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(p, gp); }
			// 결과 트레이
			using (GraphicsPath gp = Gfx.Round(new RectangleF(16, 408, 388, 150), 16)) { using (SolidBrush b = new SolidBrush(Theme.Page)) g.FillPath(b, gp); using (Pen p = new Pen(Theme.Line, 1f)) g.DrawPath(p, gp); }
			// 관을 다시 위에 (트레이 위로 지나가도록): 레일 부분만 한 번 더
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddLines(railPath.P.ToArray()); using (Pen p = new Pen(tubeOut, TUBE + 4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(p, gp); using (Pen p = new Pen(tubeIn, TUBE) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(p, gp); }
			for (int i = 0; i < 7; i++) { float x = SlotX(i); using (SolidBrush b = new SolidBrush(Color.FromArgb(Theme.Dark ? 60 : 24, 0, 0, 0))) g.FillEllipse(b, x - 24, SLOTY - 21, 48, 48); using (Pen p = new Pen(Theme.Line, 2f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(p, x - 21, SLOTY - 21, 42, 42); }
			// 유리 구체 뒷면
			using (GraphicsPath gp = new GraphicsPath())
			{
				gp.AddEllipse(MX - MR, MY - MR, MR * 2, MR * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(MX - MR * 0.25f, MY - MR * 0.3f); pb.CenterColor = Theme.Mix(Theme.Bg, glassC, Theme.Dark ? 0.16 : 0.1); pb.SurroundColors = new[] { Theme.Mix(Theme.Bg, glassC, Theme.Dark ? 0.5 : 0.4) }; g.FillPath(pb, gp); }
				using (Pen p = new Pen(Color.FromArgb(70, 40, 80, 140), 1.5f)) g.DrawEllipse(p, MX - MR + 6, MY - MR + 6, MR * 2 - 12, MR * 2 - 12);
			}
			// 바닥 송풍구
			using (SolidBrush b = new SolidBrush(Color.FromArgb(Theme.Dark ? 130 : 60, 20, 40, 90))) g.FillEllipse(b, MX - 42, MY + MR - 20, 84, 22);
			for (int k = -2; k <= 2; k++) using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 150, 190, 235))) g.FillEllipse(b, MX + k * 14 - 3, MY + MR - 14, 6, 6);
		}
		using (Graphics g = Graphics.FromImage(layGlass))
		{
			Gfx.HQ(g); g.ScaleTransform(sc, sc);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(MX - MR, MY - MR, MR * 2, MR * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(MX, MY); pb.CenterColor = Color.FromArgb(0, 200, 225, 255); pb.SurroundColors = new[] { Color.FromArgb(Theme.Dark ? 80 : 70, 150, 190, 240) }; ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.55f, 1f }; cb.Colors = new[] { Color.FromArgb(Theme.Dark ? 80 : 70, 150, 190, 240), Color.FromArgb(0, 200, 225, 255), Color.FromArgb(0, 200, 225, 255) }; pb.InterpolationColors = cb; g.FillPath(pb, gp); } }
			// 금색 띠(적도·자오선) — 앞쪽 반만 보이게
			using (Pen p = new Pen(Color.FromArgb(215, 226, 178, 60), 4f)) { g.DrawArc(p, MX - MR, MY - MR * 0.3f, MR * 2, MR * 0.6f, 0, 180); g.DrawArc(p, MX - MR * 0.3f, MY - MR, MR * 0.6f, MR * 2, 90, 180); }
			using (Pen p = new Pen(Color.FromArgb(120, 255, 240, 190), 1.5f)) { g.DrawArc(p, MX - MR, MY - MR * 0.3f - 2f, MR * 2, MR * 0.6f, 10, 160); }
			// 테두리 + 반사
			using (Pen p = new Pen(Color.FromArgb(200, 120, 160, 215), 5f)) g.DrawEllipse(p, MX - MR, MY - MR, MR * 2, MR * 2);
			using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, MX - MR + 14, MY - MR + 14, MR * 2 - 28, MR * 2 - 28, 205, 62);
			using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, MX - MR + 14, MY - MR + 14, MR * 2 - 28, MR * 2 - 28, 280, 22);
			using (Pen p = new Pen(Color.FromArgb(70, 255, 255, 255), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, MX - MR + 12, MY - MR + 12, MR * 2 - 24, MR * 2 - 24, 20, 40);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(MX - MR * 0.62f, MY - MR * 0.82f, MR * 0.5f, MR * 0.22f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(110, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }; g.FillPath(pb, gp); } }
			// 목(구체 위 이음부)
			using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 200, 156, 52))) g.FillRectangle(b, MX - TUBE / 2f - 3f, MY - MR - 7f, TUBE + 6f, 10f);
			using (Pen p = new Pen(Color.FromArgb(180, 120, 90, 20), 1.2f)) g.DrawRectangle(p, MX - TUBE / 2f - 3f, MY - MR - 7f, TUBE + 6f, 10f);
		}
		using (Graphics g = Graphics.FromImage(layPipe))
		{
			Gfx.HQ(g); g.ScaleTransform(sc, sc);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddLines(railPath.P.ToArray()); using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 3f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { Matrix m = new Matrix(); m.Translate(-8f, -7f); gp.Transform(m); g.DrawPath(p, gp); } }
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddLines(railPath.P.ToArray()); using (Pen p = new Pen(Color.FromArgb(50, 255, 255, 255), 2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { Matrix m = new Matrix(); m.Translate(9f, 8f); gp.Transform(m); g.DrawPath(p, gp); } }
		}
	}

	// ───────── 공 ─────────
	private static Color BallColor(int n) { return n <= 10 ? Color.FromArgb(244, 190, 40) : (n <= 20 ? Color.FromArgb(70, 140, 230) : (n <= 30 ? Color.FromArgb(226, 80, 76) : (n <= 40 ? Color.FromArgb(130, 134, 146) : Color.FromArgb(80, 176, 100)))); }
	private Bitmap Sprite(int n)
	{
		if (spr[n] != null) return spr[n];
		const int S = 72; Bitmap bm = new Bitmap(S, S, PixelFormat.Format32bppPArgb); float r = S / 2f - 2f, x = S / 2f, y = S / 2f; Color c = BallColor(n);
		using (Graphics g = Graphics.FromImage(bm))
		{
			Gfx.HQ(g);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r, y - r, r * 2, r * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(x - r * 0.38f, y - r * 0.42f); pb.CenterColor = Theme.Mix(c, Color.White, 0.6); pb.SurroundColors = new[] { Theme.Mix(c, Color.Black, 0.3) }; g.FillPath(pb, gp); } }
			using (SolidBrush w = new SolidBrush(Color.FromArgb(238, 255, 255, 255))) g.FillEllipse(w, x - r * 0.6f, y - r * 0.6f, r * 1.2f, r * 1.2f);
			using (Font f = new Font("Arial", 21f, FontStyle.Bold, GraphicsUnit.Pixel)) using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (SolidBrush tb = new SolidBrush(Color.FromArgb(40, 40, 52))) { g.TextRenderingHint = TextRenderingHint.AntiAlias; g.DrawString(n.ToString(), f, tb, new RectangleF(x - r, y - r + 1f, r * 2, r * 2), sf); }
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r * 0.62f, y - r * 0.86f, r * 0.7f, r * 0.34f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(150, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }; g.FillPath(pb, gp); } }
			using (Pen p = new Pen(Color.FromArgb(60, 0, 0, 0), 1.2f)) g.DrawEllipse(p, x - r, y - r, r * 2, r * 2);
		}
		spr[n] = bm; return bm;
	}
	private void DrawBall(Graphics g, float x, float y, float r, int n, float shadow)
	{
		if (shadow > 0f) using (SolidBrush sh = new SolidBrush(Color.FromArgb((int)(shadow * 60), 0, 0, 0))) g.FillEllipse(sh, x - r + 1.5f, y - r + r * 0.18f + 2f, r * 2, r * 2);
		g.DrawImage(Sprite(n), new RectangleF(x - r - r * 0.06f, y - r - r * 0.06f, r * 2.12f, r * 2.12f));
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; if (layBack == null || layDark != Theme.Dark) BuildLayers();
		Gfx.HQ(g); g.DrawImageUnscaled(layBack, 0, 0); g.ScaleTransform(sc, sc);
		FUi.Text(g, "로또 추첨기", FUi.F(15f, 1), Theme.Ink, new RectangleF(16, 10, 300, 28), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "1~45번 공 중에서 6개 + 보너스 1개 · 한 번 나온 공은 다시 안 나와요 (재미용)", FUi.F(8.5f, 0), Theme.Muted, new RectangleF(16, 36, 400, 18), StringAlignment.Near, StringAlignment.Center, false);
		// 구체 안 공: 뒤→앞 순서
		pool.Sort(delegate(Ball a, Ball b) { return a.Z.CompareTo(b.Z); }); float lim = MR - BR;
		using (SolidBrush air = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) foreach (float[] j in jets) { float y = MY + MR - 14f - j[1] * (MR * 1.5f); float x = MX + j[0] * (0.5f + j[1]) + j[3] * j[1]; if ((x - MX) * (x - MX) + (y - MY) * (y - MY) < (MR - 6) * (MR - 6)) g.FillEllipse(air, x - 2f, y - 2f, 4f, 4f); }
		using (SolidBrush glass = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
			foreach (Ball b in pool)
			{
				float s = 1f + b.Z / (MR * 2.6f), r = BR * s, px = MX + b.X * s, py = MY + b.Y * s; DrawBall(g, px, py, r, b.N, 0.4f);
				if (b.Z < 0f) { int al = (int)(Math.Min(1f, -b.Z / lim) * 70); using (SolidBrush ov = new SolidBrush(Color.FromArgb(al, 120, 170, 225))) g.FillEllipse(ov, px - r, py - r, r * 2, r * 2); }
			}
		g.ResetTransform(); g.DrawImageUnscaled(layGlass, 0, 0); g.ScaleTransform(sc, sc);
		// 관 안 공
		foreach (PBall pb in pipe)
		{
			Path pt = slotPath[pb.Slot]; PointF p = pt.At(pb.S); float tail = pt.Len - 56f, r = BR; if (pb.S > tail) r = BR + (21f - BR) * Math.Min(1f, (pb.S - tail) / 56f);
			DrawBall(g, p.X, p.Y, r, pb.N, 0f);
		}
		g.ResetTransform(); g.DrawImageUnscaled(layPipe, 0, 0); g.ScaleTransform(sc, sc);
		// 결과 트레이
		FUi.Text(g, finished ? "이번 번호" : "뽑힌 번호", FUi.F(10f, 1), Theme.Sub, new RectangleF(30, 414, 200, 20), StringAlignment.Near, StringAlignment.Center, false);
		List<int> main = new List<int>(); for (int i = 0; i < Math.Min(6, drawn.Count); i++) main.Add(drawn[i]); if (finished) main.Sort();
		for (int i = 0; i < main.Count; i++) DrawBall(g, SlotX(i), SLOTY, 21f, main[i], 0.6f);
		FUi.Text(g, "+", FUi.F(16f, 1), Theme.Muted, new RectangleF(325, SLOTY - 16, 24, 32), StringAlignment.Center, StringAlignment.Center, false);
		if (drawn.Count >= 7) DrawBall(g, SlotX(6), SLOTY, 21f, drawn[6], 0.6f);
		FUi.Text(g, "보너스", FUi.F(8.5f, 0), Theme.Muted, new RectangleF(338, SLOTY + 24, 60, 18), StringAlignment.Center, StringAlignment.Center, false);
		bool can = finished || (!running && drawn.Count == 0);
		using (GraphicsPath gp = Gfx.Round(btn, 19)) using (SolidBrush b = new SolidBrush(can ? Theme.Accent : Theme.Line)) g.FillPath(b, gp);
		FUi.Text(g, finished ? "다시 뽑기" : (running ? "추첨 중…" : "추첨 시작!"), FUi.F(11f, 1), can ? Color.White : Theme.Muted, btn, StringAlignment.Center, StringAlignment.Center, false);
	}
}
