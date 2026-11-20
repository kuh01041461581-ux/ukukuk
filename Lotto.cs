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

	private sealed class Ball { public int N; public float X, Y, Z, Vx, Vy, Vz; public float[] R = Ident(); public float Kx, Ky, Kz; public int Cnt; }
	private sealed class PBall { public int N, Slot; public float S; }
	private static float[] Ident() { return new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }; }
	// 공의 방향(회전 행렬): w 방향으로 |w|*dt 만큼 돌려요 (로드리게스 공식)
	private static void Spin(float[] R, float wx, float wy, float wz, float dt)
	{
		float a = (float)Math.Sqrt(wx * wx + wy * wy + wz * wz) * dt; if (Math.Abs(a) < 1e-5f) return; float l = a / dt, kx = wx / l, ky = wy / l, kz = wz / l, c = (float)Math.Cos(a), s = (float)Math.Sin(a), v = 1f - c;
		float[] M = { c + kx * kx * v, kx * ky * v - kz * s, kx * kz * v + ky * s, ky * kx * v + kz * s, c + ky * ky * v, ky * kz * v - kx * s, kz * kx * v - ky * s, kz * ky * v + kx * s, c + kz * kz * v };
		float[] o = new float[9]; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) o[i * 3 + j] = M[i * 3] * R[j] + M[i * 3 + 1] * R[3 + j] + M[i * 3 + 2] * R[6 + j]; Array.Copy(o, R, 9);
	}
	private static void Ortho(float[] R)
	{
		float l = (float)Math.Sqrt(R[0] * R[0] + R[3] * R[3] + R[6] * R[6]); R[0] /= l; R[3] /= l; R[6] /= l;
		float d = R[0] * R[1] + R[3] * R[4] + R[6] * R[7]; R[1] -= d * R[0]; R[4] -= d * R[3]; R[7] -= d * R[6]; l = (float)Math.Sqrt(R[1] * R[1] + R[4] * R[4] + R[7] * R[7]); R[1] /= l; R[4] /= l; R[7] /= l;
		R[2] = R[3] * R[7] - R[6] * R[4]; R[5] = R[6] * R[1] - R[0] * R[7]; R[8] = R[0] * R[4] - R[3] * R[1];
	}
	// 카메라가 살짝 내려다보는 각도 (가까운 쪽이 아래로 보여요)
	private const float PITCH = 0.30f;
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
		FormClosed += delegate { tm.Stop(); tm.Dispose(); if (layBack != null) layBack.Dispose(); if (layGlass != null) layGlass.Dispose(); if (layPipe != null) layPipe.Dispose(); for (int i = 0; i < spr.Length; i++) if (spr[i] != null) spr[i].Dispose(); for (int i = 0; i < baseSpr.Length; i++) if (baseSpr[i] != null) baseSpr[i].Dispose(); if (shadeSpr != null) shadeSpr.Dispose(); };
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
			Ball nb = new Ball { N = i, X = (float)(Math.Cos(a) * s) * rr, Y = (float)u * rr * 0.6f + 40f, Z = (float)(Math.Sin(a) * s) * rr, Vx = (float)(rnd.NextDouble() - 0.5) * 200f, Vy = (float)(rnd.NextDouble() - 0.5) * 200f, Vz = (float)(rnd.NextDouble() - 0.5) * 200f };
			nb.Kx = (float)(rnd.NextDouble() - 0.5); nb.Ky = (float)(rnd.NextDouble() - 0.5); nb.Kz = (float)(rnd.NextDouble() - 0.5); Spin(nb.R, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, 9f); pool.Add(nb);
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
			{ float wx = (b.Vy * b.Kz - b.Vz * b.Ky) * 0.012f, wy = (b.Vz * b.Kx - b.Vx * b.Kz) * 0.012f, wz = (b.Vx * b.Ky - b.Vy * b.Kx) * 0.012f; Spin(b.R, wx, wy, wz, dt); if (++b.Cnt % 40 == 0) Ortho(b.R); }
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

	// 관: 가운데는 밝고 가장자리는 어두운 원통 단면 + 빛이 오는 왼쪽 위로 치우친 하이라이트
	private void DrawTube(Graphics g, Color tubeIn, Color tubeOut)
	{
		using (GraphicsPath gp = new GraphicsPath())
		{
			gp.AddLines(railPath.P.ToArray());
			using (Pen p = new Pen(Color.FromArgb(Theme.Dark ? 90 : 50, 0, 0, 0), TUBE + 8f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { GraphicsState s = g.Save(); g.TranslateTransform(5f, 7f); g.DrawPath(p, gp); g.Restore(s); }
			using (Pen p = new Pen(tubeOut, TUBE + 4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(p, gp);
			Color lit = Theme.Mix(tubeIn, Color.White, Theme.Dark ? 0.25 : 0.55);
			for (int k = 0; k < 6; k++)
			{
				float u = k / 5f; using (Pen p = new Pen(Theme.Mix(Theme.Mix(tubeOut, tubeIn, 0.55), lit, u), TUBE - k * (TUBE - 8f) / 6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { GraphicsState s = g.Save(); g.TranslateTransform(-1.2f * u * 3f, -1.2f * u * 3f); g.DrawPath(p, gp); g.Restore(s); }
			}
		}
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
			DrawTube(g, tubeIn, tubeOut);
			// 결과 트레이
			using (GraphicsPath gp = Gfx.Round(new RectangleF(16, 408, 388, 150), 16)) { using (SolidBrush b = new SolidBrush(Theme.Page)) g.FillPath(b, gp); using (Pen p = new Pen(Theme.Line, 1f)) g.DrawPath(p, gp); }
			// 관을 다시 위에 (트레이 위로 지나가도록): 레일 부분만 한 번 더
			DrawTube(g, tubeIn, tubeOut);
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
	// 공은 세 겹으로 그려요: ① 색깔 구 ② 번호 원판(공이 돌면 같이 돌고 납작해져요) ③ 빛과 그림자
	private readonly Bitmap[] baseSpr = new Bitmap[5]; private Bitmap shadeSpr;
	private Bitmap BaseSprite(int n)
	{
		int k = n <= 10 ? 0 : (n <= 20 ? 1 : (n <= 30 ? 2 : (n <= 40 ? 3 : 4))); if (baseSpr[k] != null) return baseSpr[k];
		const int S = 96; Bitmap bm = new Bitmap(S, S, PixelFormat.Format32bppPArgb); float r = S / 2f - 2f, x = S / 2f, y = S / 2f; Color c = BallColor(n);
		using (Graphics g = Graphics.FromImage(bm)) { Gfx.HQ(g); using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, r * 2, r * 2); }
		baseSpr[k] = bm; return bm;
	}
	private Bitmap Decal(int n)
	{
		if (spr[n] != null) return spr[n];
		const int S = 80; Bitmap bm = new Bitmap(S, S, PixelFormat.Format32bppPArgb); float x = S / 2f;
		using (Graphics g = Graphics.FromImage(bm))
		{
			Gfx.HQ(g); using (SolidBrush w = new SolidBrush(Color.FromArgb(250, 250, 250, 246))) g.FillEllipse(w, 1, 1, S - 2, S - 2);
			using (Font f = new Font("Arial", n >= 10 ? 36f : 42f, FontStyle.Bold, GraphicsUnit.Pixel)) using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (SolidBrush tb = new SolidBrush(Color.FromArgb(36, 36, 48))) { g.TextRenderingHint = TextRenderingHint.AntiAlias; g.DrawString(n.ToString(), f, tb, new RectangleF(0, 3, S, S), sf); }
			if (n == 6 || n == 9 || n == 66) using (Pen p = new Pen(Color.FromArgb(36, 36, 48), 4f)) g.DrawLine(p, x - 14, S - 12, x + 14, S - 12);   // 6·9 구분 밑줄
		}
		spr[n] = bm; return bm;
	}
	private Bitmap Shade()
	{
		if (shadeSpr != null) return shadeSpr;
		const int S = 96; Bitmap bm = new Bitmap(S, S, PixelFormat.Format32bppPArgb); float r = S / 2f - 2f, x = S / 2f, y = S / 2f;
		using (Graphics g = Graphics.FromImage(bm))
		{
			Gfx.HQ(g);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r, y - r, r * 2, r * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(x - r * 0.36f, y - r * 0.4f); pb.CenterColor = Color.FromArgb(70, 255, 255, 255); ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.45f, 0.8f, 1f }; cb.Colors = new[] { Color.FromArgb(150, 20, 20, 40), Color.FromArgb(70, 20, 20, 40), Color.FromArgb(0, 20, 20, 40), Color.FromArgb(70, 255, 255, 255) }; pb.InterpolationColors = cb; g.FillPath(pb, gp); } }
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r * 0.62f, y - r * 0.82f, r * 0.62f, r * 0.34f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(230, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }; g.FillPath(pb, gp); } }
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x + r * 0.2f, y + r * 0.38f, r * 0.62f, r * 0.34f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(50, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }; g.FillPath(pb, gp); } }   // 바닥에서 튀어 오른 빛
			using (Pen p = new Pen(Color.FromArgb(70, 0, 0, 0), 1.4f)) g.DrawEllipse(p, x - r, y - r, r * 2, r * 2);
		}
		shadeSpr = bm; return bm;
	}
	// R: 공의 방향. 번호 원판은 앞면·뒷면에 하나씩 있어서, 공이 돌아도 항상 어딘가엔 번호가 보여요
	private void DrawBall3(Graphics g, float x, float y, float r, int n, float[] R, float shadow)
	{
		if (shadow > 0f) using (SolidBrush sh = new SolidBrush(Color.FromArgb((int)(shadow * 60), 0, 0, 0))) g.FillEllipse(sh, x - r + 1.5f, y - r + r * 0.18f + 2f, r * 2, r * 2);
		InterpolationMode im0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear;   // 공이 많아서 빠른 보간을 써요
		g.DrawImage(BaseSprite(n), new RectangleF(x - r - r * 0.04f, y - r - r * 0.04f, r * 2.08f, r * 2.08f));
		for (int side = 0; side < 2; side++)
		{
			float sg = side == 0 ? 1f : -1f, nx = sg * R[2], ny = sg * R[5], nz = sg * R[8]; if (nz <= 0.06f) continue;
			float ux = -R[1], uy = -R[4], uz = -R[7];                       // 글자의 위쪽
			float e1x = ny * uz - nz * uy, e1y = nz * ux - nx * uz;         // n × u : 글자의 오른쪽
			float dx = -ux, dy = -uy, rd = 0.5f * r, cx = x + nx * r * 0.86f, cy = y + ny * r * 0.86f;
			Bitmap d = Decal(n); float k = rd * 2f / d.Width; GraphicsState gs = g.Save();
			using (Matrix m = new Matrix(e1x * k, e1y * k, dx * k, dy * k, cx, cy)) g.MultiplyTransform(m, MatrixOrder.Prepend);
			g.DrawImage(d, -d.Width / 2f, -d.Height / 2f, d.Width, d.Height); g.Restore(gs);
		}
		g.DrawImage(Shade(), new RectangleF(x - r - r * 0.04f, y - r - r * 0.04f, r * 2.08f, r * 2.08f)); g.InterpolationMode = im0;
	}
	private static readonly float[] Front = { 0.985f, 0.0f, 0.17f, 0.03f, 0.985f, -0.12f, -0.17f, 0.12f, 0.975f };   // 쟁반 위 공: 번호가 정면을 보고 빛 쪽으로 살짝 기울어요
	private void DrawBall(Graphics g, float x, float y, float r, int n, float shadow) { DrawBall3(g, x, y, r, n, Front, shadow); }

	// 유리 구체를 감싼 금색 띠: 구체 둘레를 따라 천천히 돌아요 (뒤쪽 반은 공 뒤로, 앞쪽 반은 공 앞으로)
	private void Rings(Graphics g, bool front)
	{
		float ca = (float)Math.Cos(PITCH), sa = (float)Math.Sin(PITCH); float ph = tAir * 0.55f; const int NP = 48;
		PointF[] pt = new PointF[NP + 1]; float[] zz = new float[NP + 1];
		using (Pen p = new Pen(front ? Color.FromArgb(200, 232, 184, 64) : Color.FromArgb(90, 150, 110, 40), front ? 2.6f : 2f))
			for (int ring = 0; ring < 3; ring++)
			{
				for (int k = 0; k <= NP; k++)
				{
					float th = k * 6.2832f / NP, x, y, z;
					if (ring == 0) { x = (float)Math.Cos(th); y = 0f; z = (float)Math.Sin(th); }
					else { float q = ph + (ring - 1) * 1.5708f, c = (float)Math.Cos(th), s = (float)Math.Sin(th); x = c * (float)Math.Cos(q); y = s; z = c * (float)Math.Sin(q); }
					x *= MR; y *= MR; z *= MR; float y2 = y * ca + z * sa, z2 = z * ca - y * sa, sc2 = 1f + z2 / (MR * 2.6f); pt[k] = new PointF(MX + x * sc2, MY + y2 * sc2); zz[k] = z2;
				}
				int st = -1;   // 앞(또는 뒤)쪽에 있는 연속 구간만 이어서 그려요
				for (int k = 0; k <= NP; k++)
				{
					bool on = k < NP && ((zz[k] + zz[k + 1]) * 0.5f >= 0f) == front;
					if (on && st < 0) st = k;
					if (!on && st >= 0) { PointF[] seg = new PointF[k - st + 1]; Array.Copy(pt, st, seg, 0, seg.Length); if (seg.Length >= 2) g.DrawLines(p, seg); st = -1; }
				}
			}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; if (layBack == null || layDark != Theme.Dark) BuildLayers();
		Gfx.HQ(g); g.DrawImageUnscaled(layBack, 0, 0); g.ScaleTransform(sc, sc);
		FUi.Text(g, "로또 추첨기", FUi.F(15f, 1), Theme.Ink, new RectangleF(16, 10, 300, 28), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "1~45번 공 중에서 6개 + 보너스 1개 · 한 번 나온 공은 다시 안 나와요 (재미용)", FUi.F(8.5f, 0), Theme.Muted, new RectangleF(16, 36, 400, 18), StringAlignment.Near, StringAlignment.Center, false);
		// 구체 안 공: 뒤→앞 순서
		float ca = (float)Math.Cos(PITCH), sa = (float)Math.Sin(PITCH); float lim = MR - BR;
		pool.Sort(delegate(Ball a, Ball b) { return (a.Z * ca - a.Y * sa).CompareTo(b.Z * ca - b.Y * sa); });
		Rings(g, false);
		using (SolidBrush air = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) foreach (float[] j in jets) { float y = MY + MR - 14f - j[1] * (MR * 1.5f); float x = MX + j[0] * (0.5f + j[1]) + j[3] * j[1]; if ((x - MX) * (x - MX) + (y - MY) * (y - MY) < (MR - 6) * (MR - 6)) g.FillEllipse(air, x - 2f, y - 2f, 4f, 4f); }
		foreach (Ball b in pool)
		{
			float y2 = b.Y * ca + b.Z * sa, z2 = b.Z * ca - b.Y * sa; float s = 1f + z2 / (MR * 2.6f), r = BR * s, px = MX + b.X * s, py = MY + y2 * s;
			DrawBall3(g, px, py, r, b.N, b.R, 0f);
			if (z2 < 0f) { int al = (int)(Math.Min(1f, -z2 / lim) * 80); using (SolidBrush ov = new SolidBrush(Color.FromArgb(al, 120, 170, 225))) g.FillEllipse(ov, px - r, py - r, r * 2, r * 2); }   // 뒤쪽 공은 유리 너머로 흐릿하게
		}
		g.ResetTransform(); g.DrawImageUnscaled(layGlass, 0, 0); g.ScaleTransform(sc, sc); Rings(g, true);
		// 관 안 공
		foreach (PBall pb in pipe)
		{
			Path pt = slotPath[pb.Slot]; PointF p = pt.At(pb.S); float tail = pt.Len - 56f, r = BR; if (pb.S > tail) r = BR + (21f - BR) * Math.Min(1f, (pb.S - tail) / 56f);
			{ float[] Rp = Ident(); Spin(Rp, 0f, 0f, 1f, (pb.S - pt.Len) / BR * 0.9f); Spin(Rp, 1f, 0f, 0f, 0.35f); DrawBall3(g, p.X, p.Y, r, pb.N, Rp, 0f); }
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
