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
	private readonly Random rnd = new Random(); private readonly FrameTimer tm = new FrameTimer(); private readonly System.Diagnostics.Stopwatch clk = System.Diagnostics.Stopwatch.StartNew(); private double lastClk; private readonly Path[] slotPath = new Path[7]; private Path railPath;
	private Ball pull; private float pullT, pullCd; private bool running, finished; private float tAir;
	private float resT = -1f; private bool resOpen; private float copiedT;   // 당첨 번호 큰 화면: 추첨이 끝나면 번호만 크게 보여줘요
	private static readonly RectangleF ResCard = new RectangleF(24, 120, 372, 372), ResCopy = new RectangleF(48, 436, 148, 38), ResAgain = new RectangleF(224, 436, 148, 38), ResX = new RectangleF(362, 128, 26, 26);
	private bool ResVisible { get { return finished && resOpen && resT > 0.8f; } }
	private RectangleF btn = new RectangleF(120, 568, 180, 38);
	private Bitmap layBack, layGlass, layPipe; private bool layDark; private readonly Bitmap[] spr = new Bitmap[46];
	private readonly float[][] jets = new float[16][];

	public LottoForm()
	{
		Text = "로또 추첨기 (재미용)"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(420 * sc), (int)(620 * sc)); DoubleBuffered = true; BackColor = Theme.Bg;
		BuildPaths(); Reset(); tm.Tick += delegate
		{
			try
			{
				// 화면 주사율(vsync)에 맞춰 돌고, 실제 흐른 시간만큼 계산해요 (프레임이 흔들려도 움직임은 일정). 계산은 잘게 쪼개서 공이 서로 뚫고 지나가지 않게 해요
				double now = clk.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.05, Math.Max(0.001, now - lastClk)); lastClk = now;
				int sub = Math.Max(1, (int)Math.Ceiling(dt / 0.009f)); for (int q = 0; q < sub; q++) Step(dt / sub);
				Invalidate();
			}
			catch (Exception ex) { Program.LogErr(ex); tm.Stop(); }
		};
		lastClk = clk.Elapsed.TotalSeconds; tm.Start();
		FormClosed += delegate { tm.Stop(); if (layBack != null) layBack.Dispose(); if (layGlass != null) layGlass.Dispose(); if (layPipe != null) layPipe.Dispose(); for (int i = 0; i < spr.Length; i++) if (spr[i] != null) spr[i].Dispose(); for (int i = 0; i < baseSpr.Length; i++) if (baseSpr[i] != null) baseSpr[i].Dispose(); if (shadeSpr != null) shadeSpr.Dispose(); };
		MouseDown += delegate(object s, MouseEventArgs e)
		{
			float mx = e.X / sc, my = e.Y / sc;
			if (ResVisible)
			{
				if (ResAgain.Contains(mx, my)) { Reset(); running = true; pullCd = 1.0f; try { Sfx.Play("whoosh"); } catch { } }
				else if (ResCopy.Contains(mx, my)) { try { Clipboard.SetText(ResultText()); copiedT = 1.8f; Sfx.Play("ok"); } catch { } }
				else if (ResX.Contains(mx, my) || !ResCard.Contains(mx, my)) { resOpen = false; try { Sfx.Play("tick"); } catch { } }
				return;
			}
			if (finished && !resOpen && new RectangleF(16, 408, 388, 150).Contains(mx, my)) { resOpen = true; resT = 0.8f; return; }
			if (btn.Contains(mx, my)) { if (finished || (!running && drawn.Count == 0)) { if (finished) Reset(); running = true; pullCd = 1.0f; try { Sfx.Play("whoosh"); } catch { } } }
		};
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
		pool.Clear(); pipe.Clear(); drawn.Clear(); pull = null; running = false; finished = false; resT = -1f; resOpen = false;
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
		if (resT >= 0f) resT += dt; if (copiedT > 0f) copiedT -= dt; tAir += dt; float lim = MR - BR - 1f; float nfac = (float)Math.Sqrt(0.016f / Math.Max(0.002f, dt)); float stir = running ? 1f : 0.32f;
		for (int i = 0; i < pool.Count; i++)
		{
			Ball b = pool[i]; if (b == pull) continue;
			b.Vy += 330f * dt;
			float fy = (b.Y + lim * 0.1f) / (lim * 1.1f); if (fy < 0) fy = 0; if (fy > 1) fy = 1;   // 아래쪽일수록 바람이 세요
			b.Vy -= (900f + (float)rnd.NextDouble() * 1300f) * dt * stir * fy;
			b.Vx += ((float)(rnd.NextDouble() - 0.5) * 900f * nfac - b.Z * 5f * stir) * dt * stir; b.Vz += ((float)(rnd.NextDouble() - 0.5) * 900f * nfac + b.X * 5f * stir) * dt * stir;
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
				if (pb.S >= pt.Len) { drawn.Add(pb.N); pipe.RemoveAt(i); try { Sfx.Play(drawn.Count == 7 ? "fanfare" : "ok"); } catch { } if (drawn.Count >= 7) { running = false; finished = true; resT = 0f; resOpen = true; } }
			}
		}
		for (int i = 0; i < jets.Length; i++) { float[] j = jets[i]; j[1] -= dt * j[2] * (running ? 1.3f : 0.4f); if (j[1] < 0f) { j[1] = 1f; j[0] = (float)(rnd.NextDouble() - 0.5) * 120f; j[2] = 0.6f + (float)rnd.NextDouble() * 0.8f; j[3] = (float)(rnd.NextDouble() - 0.5) * 80f; } }
	}

	private static Color A(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }
	// 금속 띠(조임쇠): 가운데 선이 축 방향. 원통의 옆면 빛 반사를 가로로 줄 세워서 크롬처럼 보이게 해요
	private void MetalBand(Graphics g, PointF c, float tangentDeg, float w, float h, bool cap)
	{
		GraphicsState gs = g.Save(); g.TranslateTransform(c.X, c.Y); g.RotateTransform(tangentDeg + 90f);
		RectangleF body = new RectangleF(-w / 2f, -h / 2f, w, h); float eh = Math.Max(5f, w * 0.2f);
		Color d = Color.FromArgb(Theme.Dark ? 70 : 96, Theme.Dark ? 78 : 104, Theme.Dark ? 92 : 120), m = Color.FromArgb(168, 176, 190), l = Color.FromArgb(238, 242, 248);
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sh, -w / 2f + 2f, h / 2f - eh / 2f + 3f, w, eh);
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(body.X, body.Y, body.Width, 1), d, d, 0f))
		{
			ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.12f, 0.3f, 0.42f, 0.55f, 0.78f, 1f }; cb.Colors = new[] { d, m, l, Color.White, m, d, Theme.Mix(d, Color.Black, 0.4) }; lb.InterpolationColors = cb;
			g.FillRectangle(lb, body); g.FillEllipse(lb, body.X, body.Bottom - eh / 2f, body.Width, eh);
			if (cap) { using (SolidBrush top = new SolidBrush(Color.FromArgb(235, 214, 220, 230))) g.FillEllipse(top, body.X, body.Y - eh / 2f, body.Width, eh); using (SolidBrush hole = new SolidBrush(Color.FromArgb(150, 40, 50, 70))) g.FillEllipse(hole, body.X + w * 0.14f, body.Y - eh * 0.36f, w * 0.72f, eh * 0.72f); }
			else using (SolidBrush top = new SolidBrush(Color.FromArgb(120, 214, 220, 230))) g.FillEllipse(top, body.X, body.Y - eh / 2f, body.Width, eh);
			using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawArc(p, body.X + 0.5f, body.Y - eh / 2f, body.Width - 1f, eh, 180, 180);
			using (Pen p = new Pen(Color.FromArgb(90, 40, 50, 70), 1f)) g.DrawArc(p, body.X + 0.5f, body.Bottom - eh / 2f, body.Width - 1f, eh, 0, 180);
		}
		g.Restore(gs);
	}
	// 관: 가장자리는 어둡고 가운데는 맑은 유리 원통 + 빛이 오는 쪽의 반사줄
	private void DrawTube(Graphics g, Color tubeIn, Color tubeOut)
	{
		using (GraphicsPath gp = new GraphicsPath())
		{
			gp.AddLines(railPath.P.ToArray());
			for (int k = 3; k >= 0; k--) using (Pen p = new Pen(Color.FromArgb(Theme.Dark ? 12 : 5, 0, 0, 0), TUBE + 4f + k * 3f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { GraphicsState s = g.Save(); g.TranslateTransform(5f, 8f); g.DrawPath(p, gp); g.Restore(s); }   // 부드러운 그림자
			Color edge = Theme.Mix(tubeOut, Theme.Bg, Theme.Dark ? 0.1 : 0.32), clear = Theme.Mix(Theme.Bg, tubeIn, Theme.Dark ? 0.3 : 0.18);
			const int N = 11;
			for (int k = 0; k <= N; k++)
			{
				double u = k / (double)N, e = u * u * (3 - 2 * u); float w = (TUBE + 5f) * (1f - (float)u * 0.93f) ; Color c = Theme.Mix(edge, clear, Math.Pow(e, 0.45));
				float off = (float)u * 2.2f; using (Pen p = new Pen(c, w) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { GraphicsState s = g.Save(); g.TranslateTransform(-off * 0.6f, -off * 0.6f); g.DrawPath(p, gp); g.Restore(s); }
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
			// 스튜디오 배경: 구체 뒤가 은은하게 밝아요
			using (GraphicsPath bgp = new GraphicsPath()) { bgp.AddEllipse(MX - 250, MY - 230, 500, 470); using (PathGradientBrush pb = new PathGradientBrush(bgp)) { pb.CenterPoint = new PointF(MX - 20, MY - 40); pb.CenterColor = Color.FromArgb(Theme.Dark ? 34 : 120, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }; g.FillPath(pb, bgp); } }
			// 바닥 그림자(겹겹) + 받침대
			for (int k = 0; k < 4; k++) using (GraphicsPath sp = new GraphicsPath()) { sp.AddEllipse(MX - 124 - k * 10, 394 - k * 2, 248 + k * 20, 34 + k * 6); using (PathGradientBrush pb = new PathGradientBrush(sp)) { pb.CenterColor = Color.FromArgb(Theme.Dark ? 70 : 40, 0, 0, 0); pb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) }; g.FillPath(pb, sp); } }
			Color red = Color.FromArgb(200, 58, 70), redD = Theme.Mix(red, Color.Black, 0.55), redL = Theme.Mix(red, Color.White, 0.55);
			RectangleF body = new RectangleF(MX - 98, 360, 196, 40);
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(body.X, body.Y, body.Width, 1), redD, redD, 0f)) { ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.1f, 0.26f, 0.38f, 0.55f, 0.82f, 1f }; cb.Colors = new[] { redD, Theme.Mix(red, Color.Black, 0.25), red, redL, red, redD, Theme.Mix(redD, Color.Black, 0.4) }; lb.InterpolationColors = cb; g.FillRectangle(lb, body); g.FillEllipse(lb, body.X, body.Bottom - 9, body.Width, 18); }
			using (LinearGradientBrush vb = new LinearGradientBrush(new RectangleF(body.X, body.Y, 1, body.Height + 9), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(80, 0, 0, 0), 90f)) g.FillRectangle(vb, body.X, body.Y, body.Width, body.Height);
			// 받침대 윗면: 크롬 링 + 구체가 앉는 홈
			using (LinearGradientBrush mb = new LinearGradientBrush(new RectangleF(body.X, 1, body.Width, 1), Color.Gray, Color.Gray, 0f)) { ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.2f, 0.45f, 0.55f, 0.8f, 1f }; cb.Colors = new[] { Color.FromArgb(120, 126, 140), Color.FromArgb(220, 226, 236), Color.White, Color.FromArgb(200, 206, 218), Color.FromArgb(120, 126, 140), Color.FromArgb(80, 86, 100) }; mb.InterpolationColors = cb; g.FillEllipse(mb, body.X, body.Y - 9, body.Width, 18); }
			using (SolidBrush b = new SolidBrush(Theme.Mix(red, Color.Black, 0.5))) g.FillEllipse(b, body.X + 12, body.Y - 6, body.Width - 24, 12);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(b, body.X + 20, body.Y - 4, body.Width - 40, 8);
			using (Pen p = new Pen(Color.FromArgb(220, 238, 196, 80), 2.4f)) g.DrawArc(p, body.X + 2, body.Bottom - 10, body.Width - 4, 16, 8, 164);
			using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawArc(p, body.X + 2, body.Y - 8, body.Width - 4, 16, 195, 150);
			// 관 몸통
			DrawTube(g, tubeIn, tubeOut);
			// 결과 트레이
			using (GraphicsPath gp = Gfx.Round(new RectangleF(16, 408, 388, 150), 16))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(Theme.Dark ? 70 : 28, 0, 0, 0))) { GraphicsState s = g.Save(); g.TranslateTransform(0, 4); g.FillPath(b, gp); g.Restore(s); }
				using (SolidBrush b = new SolidBrush(Theme.Page)) g.FillPath(b, gp); using (Pen p = new Pen(Theme.Line, 1f)) g.DrawPath(p, gp);
			}
			DrawTube(g, tubeIn, tubeOut);   // 트레이 위로 지나가도록 한 번 더
			for (int i = 0; i < 7; i++)
			{
				float x = SlotX(i);
				using (SolidBrush b = new SolidBrush(Color.FromArgb(Theme.Dark ? 60 : 26, 0, 0, 0))) g.FillEllipse(b, x - 23, SLOTY - 22, 46, 46);
				using (Pen p = new Pen(Color.FromArgb(Theme.Dark ? 130 : 80, 0, 0, 0), 3f)) g.DrawArc(p, x - 22, SLOTY - 21, 44, 44, 150, 130);   // 안쪽 그림자(윗면)
				using (Pen p = new Pen(Color.FromArgb(Theme.Dark ? 60 : 200, 255, 255, 255), 2f)) g.DrawArc(p, x - 23, SLOTY - 22, 46, 46, 330, 100);   // 아랫면 반사
			}
			// 유리 구체 뒷면 (두께가 있는 유리: 가장자리가 짙고 가운데는 맑아요)
			using (GraphicsPath gp = new GraphicsPath())
			{
				gp.AddEllipse(MX - MR, MY - MR, MR * 2, MR * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(MX - MR * 0.2f, MY - MR * 0.25f); pb.CenterColor = Theme.Mix(Theme.Bg, Color.White, Theme.Dark ? 0.05 : 0.45); ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.12f, 0.5f, 1f }; cb.Colors = new[] { Theme.Mix(Theme.Bg, glassC, Theme.Dark ? 0.62 : 0.5), Theme.Mix(Theme.Bg, glassC, Theme.Dark ? 0.3 : 0.2), Theme.Mix(Theme.Bg, glassC, Theme.Dark ? 0.12 : 0.07), pb.CenterColor }; pb.InterpolationColors = cb; g.FillPath(pb, gp); }
				using (Pen p = new Pen(Color.FromArgb(60, 30, 70, 130), 1.4f)) g.DrawEllipse(p, MX - MR + 7, MY - MR + 7, MR * 2 - 14, MR * 2 - 14);
				// 바닥에 비치는 빨간 받침 (유리 아래쪽에 반사)
				using (GraphicsPath rp = new GraphicsPath()) { rp.AddEllipse(MX - 70, MY + MR - 34, 140, 40); using (PathGradientBrush pb = new PathGradientBrush(rp)) { pb.CenterColor = Color.FromArgb(70, 200, 58, 70); pb.SurroundColors = new[] { Color.FromArgb(0, 200, 58, 70) }; g.FillPath(pb, rp); } }
			}
			// 바닥 송풍구
			using (SolidBrush b = new SolidBrush(Color.FromArgb(Theme.Dark ? 130 : 60, 20, 40, 90))) g.FillEllipse(b, MX - 42, MY + MR - 20, 84, 22);
			for (int k = -2; k <= 2; k++) using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 150, 190, 235))) g.FillEllipse(b, MX + k * 14 - 3, MY + MR - 14, 6, 6);
		}
		using (Graphics g = Graphics.FromImage(layGlass))
		{
			Gfx.HQ(g); g.ScaleTransform(sc, sc);
			// 프레넬: 가장자리로 갈수록 유리가 진하게 보여요
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(MX - MR, MY - MR, MR * 2, MR * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(MX, MY); pb.CenterColor = Color.FromArgb(0, 200, 225, 255); ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.1f, 0.55f, 1f }; cb.Colors = new[] { Color.FromArgb(Theme.Dark ? 130 : 120, 130, 175, 235), Color.FromArgb(Theme.Dark ? 70 : 60, 150, 190, 240), Color.FromArgb(0, 200, 225, 255), Color.FromArgb(0, 200, 225, 255) }; pb.InterpolationColors = cb; g.FillPath(pb, gp); } }
			// 유리 두께: 바깥 어두운 선 · 안쪽 밝은 선
			using (Pen p = new Pen(Color.FromArgb(150, 70, 110, 170), 2.2f)) g.DrawEllipse(p, MX - MR, MY - MR, MR * 2, MR * 2);
			using (Pen p = new Pen(Color.FromArgb(100, 210, 232, 255), 3.4f)) g.DrawEllipse(p, MX - MR + 3.4f, MY - MR + 3.4f, MR * 2 - 6.8f, MR * 2 - 6.8f);
			using (Pen p = new Pen(Color.FromArgb(70, 60, 100, 160), 1f)) g.DrawEllipse(p, MX - MR + 6.5f, MY - MR + 6.5f, MR * 2 - 13f, MR * 2 - 13f);
			// 창문 모양 반사 (왼쪽 위): 구체 안으로 잘라서 곡면에 맞춰요
			GraphicsState gs = g.Save();
			using (GraphicsPath clip = new GraphicsPath()) { clip.AddEllipse(MX - MR + 8, MY - MR + 8, MR * 2 - 16, MR * 2 - 16); g.SetClip(clip, CombineMode.Intersect); }
			g.TranslateTransform(MX - MR * 0.46f, MY - MR * 0.5f); g.RotateTransform(-38f);
			using (GraphicsPath win = Gfx.Round(new RectangleF(-34, -20, 68, 40), 10f))
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(-34, -20, 68, 40), Color.FromArgb(185, 255, 255, 255), Color.FromArgb(10, 255, 255, 255), 60f)) { g.FillPath(lb, win); }
			using (Pen p = new Pen(Color.FromArgb(120, 140, 170, 210), 1.6f)) { g.DrawLine(p, 0, -20, 0, 20); g.DrawLine(p, -34, 0, 34, 0); }   // 창틀
			g.Restore(gs);
			using (Pen p = new Pen(Color.FromArgb(170, 255, 255, 255), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, MX - MR + 12, MY - MR + 12, MR * 2 - 24, MR * 2 - 24, 200, 40);
			using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, MX - MR + 12, MY - MR + 12, MR * 2 - 24, MR * 2 - 24, 244, 18);
			using (Pen p = new Pen(Color.FromArgb(80, 255, 255, 255), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, MX - MR + 10, MY - MR + 10, MR * 2 - 20, MR * 2 - 20, 15, 48);   // 오른쪽 아래 반사광
			using (Pen p = new Pen(Color.FromArgb(50, 255, 200, 150), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, MX - MR + 6, MY - MR + 6, MR * 2 - 12, MR * 2 - 12, 62, 46);   // 아래쪽 따뜻한 반사
			// 구체가 받침에 앉는 크롬 링 (앞쪽 반)
			{
				float rw = 156f, rh = 22f, rx = MX - rw / 2f, ry = MY + MR - 8f;
				using (Pen p = new Pen(Color.FromArgb(Theme.Dark ? 120 : 150, 70, 78, 96), 7f)) g.DrawArc(p, rx, ry, rw, rh, 0, 180);
				using (Pen p = new Pen(Color.FromArgb(230, 214, 220, 232), 4f)) g.DrawArc(p, rx, ry - 0.5f, rw, rh, 8, 164);
				using (Pen p = new Pen(Color.FromArgb(230, 255, 255, 255), 1.4f)) g.DrawArc(p, rx + 6, ry - 1.6f, rw - 12, rh, 30, 100);
			}
		}
		using (Graphics g = Graphics.FromImage(layPipe))
		{
			Gfx.HQ(g); g.ScaleTransform(sc, sc);
			// 유리관의 빛 반사 (왼쪽 위에서 오는 빛)
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddLines(railPath.P.ToArray()); using (Pen p = new Pen(Color.FromArgb(170, 255, 255, 255), 2.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { Matrix m = new Matrix(); m.Translate(-9f, -8f); gp.Transform(m); g.DrawPath(p, gp); } }
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddLines(railPath.P.ToArray()); using (Pen p = new Pen(Color.FromArgb(60, 255, 255, 255), 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) { Matrix m = new Matrix(); m.Translate(10f, 9f); gp.Transform(m); g.DrawPath(p, gp); } }
			// 금속 이음쇠: 구체 목 · 관 연결 마디 · 끝 마개 (공이 지나갈 때 띠 뒤로 잠깐 가려져요)
			MetalBand(g, new PointF(MX, MY - MR + 2f), -90f, TUBE + 30f, 7f, false);     // 구체 목의 받침 플랜지
			MetalBand(g, new PointF(MX, MY - MR - 8f), -90f, TUBE + 12f, 12f, true);      // 관을 물고 있는 목 조임쇠
			MetalBand(g, new PointF(PIPEX, 230f), 90f, TUBE + 8f, 7f, false);
			MetalBand(g, new PointF(40f, RAIL), 180f, TUBE + 10f, 9f, false);
		}
	}

	// ───────── 공 ─────────
	private static Color BallColor(int n) { return n <= 10 ? Color.FromArgb(244, 190, 40) : (n <= 20 ? Color.FromArgb(70, 140, 230) : (n <= 30 ? Color.FromArgb(226, 80, 76) : (n <= 40 ? Color.FromArgb(130, 134, 146) : Color.FromArgb(80, 176, 100)))); }
	// 공은 세 겹으로 그려요: ① 색깔 구 ② 번호 원판(공이 돌면 같이 돌고 납작해져요) ③ 빛과 그림자
	private readonly Bitmap[] baseSpr = new Bitmap[5]; private Bitmap shadeSpr;
	private Bitmap BaseSprite(int n)
	{
		int k = n <= 10 ? 0 : (n <= 20 ? 1 : (n <= 30 ? 2 : (n <= 40 ? 3 : 4))); if (baseSpr[k] != null) return baseSpr[k];
		const int S = 88; Bitmap bm = new Bitmap(S, S, PixelFormat.Format32bppPArgb); float r = S / 2f - 2f, x = S / 2f, y = S / 2f; Color c = BallColor(n);
		using (Graphics g = Graphics.FromImage(bm)) { Gfx.HQ(g); using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, r * 2, r * 2); using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r, y - r, r * 2, r * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(x - r * 0.3f, y - r * 0.34f); pb.CenterColor = Theme.Mix(c, Color.White, 0.22); pb.SurroundColors = new[] { Theme.Mix(c, Color.Black, 0.18) }; g.FillPath(pb, gp); } } }
		baseSpr[k] = bm; return bm;
	}
	private Bitmap Decal(int n)
	{
		if (spr[n] != null) return spr[n];
		const int S = 64; Bitmap bm = new Bitmap(S, S, PixelFormat.Format32bppPArgb); float x = S / 2f;
		using (Graphics g = Graphics.FromImage(bm))
		{
			Gfx.HQ(g); using (SolidBrush w = new SolidBrush(Color.FromArgb(250, 250, 250, 246))) g.FillEllipse(w, 1, 1, S - 2, S - 2);
			using (Font f = new Font("Arial", n >= 10 ? 29f : 34f, FontStyle.Bold, GraphicsUnit.Pixel)) using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (SolidBrush tb = new SolidBrush(Color.FromArgb(36, 36, 48))) { g.TextRenderingHint = TextRenderingHint.AntiAlias; g.DrawString(n.ToString(), f, tb, new RectangleF(0, 2, S, S), sf); }
			if (n == 6 || n == 9 || n == 66) using (Pen p = new Pen(Color.FromArgb(36, 36, 48), 4f)) g.DrawLine(p, x - 11, S - 10, x + 11, S - 10);   // 6·9 구분 밑줄
		}
		spr[n] = bm; return bm;
	}
	private Bitmap Shade()
	{
		if (shadeSpr != null) return shadeSpr;
		const int S = 88; Bitmap bm = new Bitmap(S, S, PixelFormat.Format32bppPArgb); float r = S / 2f - 2f, x = S / 2f, y = S / 2f;
		using (Graphics g = Graphics.FromImage(bm))
		{
			Gfx.HQ(g);
			// ① 부드러운 명암: 빛 쪽은 환하고 반대쪽은 깊게 어두워요
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r, y - r, r * 2, r * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(x - r * 0.34f, y - r * 0.38f); pb.CenterColor = Color.FromArgb(46, 255, 255, 255); ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.3f, 0.62f, 0.86f, 1f }; cb.Colors = new[] { Color.FromArgb(190, 14, 14, 34), Color.FromArgb(118, 14, 14, 34), Color.FromArgb(34, 14, 14, 34), Color.FromArgb(0, 14, 14, 34), pb.CenterColor }; pb.InterpolationColors = cb; g.FillPath(pb, gp); } }
			// ② 가장자리 프레넬 반사(하늘색 테두리) — 윤이 나는 공의 느낌
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r, y - r, r * 2, r * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(0, 200, 225, 255); pb.SurroundColors = new[] { Color.FromArgb(70, 190, 215, 255) }; ColorBlend cb = new ColorBlend(); cb.Positions = new[] { 0f, 0.8f, 1f }; cb.Colors = new[] { Color.FromArgb(70, 190, 215, 255), Color.FromArgb(0, 200, 225, 255), Color.FromArgb(0, 200, 225, 255) }; pb.InterpolationColors = cb; g.FillPath(pb, gp); } }
			// ③ 바닥에서 튀어 오른 빛(오른쪽 아래)
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x + r * 0.05f, y + r * 0.3f, r * 0.8f, r * 0.5f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(74, 255, 244, 224); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 244, 224) }; g.FillPath(pb, gp); } }
			// ④ 창문 모양 반사: 크고 부드러운 면광 + 또렷한 핵심 반사
			GraphicsState gs = g.Save(); g.TranslateTransform(x - r * 0.36f, y - r * 0.44f); g.RotateTransform(-32f);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(-r * 0.46f, -r * 0.27f, r * 0.92f, r * 0.54f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(214, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }; g.FillPath(pb, gp); } }
			using (GraphicsPath gp = Gfx.Round(new RectangleF(-r * 0.27f, -r * 0.13f, r * 0.54f, r * 0.26f), r * 0.07f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillPath(b, gp);
			g.Restore(gs);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r * 0.5f, y - r * 0.62f, r * 0.17f, r * 0.17f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(255, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }; g.FillPath(pb, gp); } }
			using (Pen p = new Pen(Color.FromArgb(80, 0, 0, 0), 1.2f)) g.DrawEllipse(p, x - r, y - r, r * 2, r * 2);
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
		PointF[] pt = ringPt; float[] zz = ringZ;
		Pen p = front ? ringFront : ringBack;
		{
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
					if (!on && st >= 0) { int cnt = k - st + 1; if (cnt >= 2) { Array.Copy(pt, st, ringSeg, 0, cnt); g.DrawLines(p, cnt == ringSeg.Length ? ringSeg : Sub(ringSeg, cnt)); } st = -1; }
				}
			}
		}
	}
	private readonly PointF[] ringPt = new PointF[49], ringSeg = new PointF[49]; private readonly float[] ringZ = new float[49];
	private readonly Pen ringFront = new Pen(Color.FromArgb(200, 232, 184, 64), 2.6f), ringBack = new Pen(Color.FromArgb(90, 150, 110, 40), 2f);
	private static PointF[] Sub(PointF[] a, int n) { PointF[] r = new PointF[n]; Array.Copy(a, r, n); return r; }

	private string ResultText()
	{
		List<int> m = new List<int>(); for (int i = 0; i < Math.Min(6, drawn.Count); i++) m.Add(drawn[i]); m.Sort();
		return string.Join(", ", m.ConvertAll(x => x.ToString()).ToArray()) + (drawn.Count >= 7 ? " + 보너스 " + drawn[6] : "");
	}
	private static float Pop(float u) { if (u <= 0f) return 0f; if (u >= 1f) return 1f; float c1 = 1.9f, c3 = c1 + 1f, v = u - 1f; return 1f + c3 * v * v * v + c1 * v * v; }   // 튀어 오르듯 커지는 효과
	private void DrawResult(Graphics g)
	{
		float t = resT - 0.8f; float fade = Math.Min(1f, t / 0.25f);
		using (SolidBrush dim = new SolidBrush(Color.FromArgb((int)(fade * (Theme.Dark ? 200 : 170)), 12, 14, 28))) g.FillRectangle(dim, 0, 0, 420, 620);
		float cy = ResCard.Y + (1f - Pop(Math.Min(1f, t / 0.4f))) * 30f;
		RectangleF card = new RectangleF(ResCard.X, cy, ResCard.Width, ResCard.Height);
		using (GraphicsPath gp = Gfx.Round(card, 22))
		{
			using (SolidBrush sh = new SolidBrush(Color.FromArgb((int)(fade * 70), 0, 0, 0))) { GraphicsState s = g.Save(); g.TranslateTransform(0, 8); g.FillPath(sh, gp); g.Restore(s); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(fade * 255), Theme.Page))) g.FillPath(b, gp);
			using (Pen p = new Pen(Color.FromArgb((int)(fade * 255), Theme.Accent), 2.5f)) g.DrawPath(p, gp);
		}
		if (fade < 1f) return;
		float oy = cy - ResCard.Y;
		FUi.Text(g, "🎉 당첨 번호", FUi.F(22f, 1), Theme.Ink, new RectangleF(card.X, card.Y + 18, card.Width, 40), StringAlignment.Center, StringAlignment.Center, false);
		FUi.Text(g, "이번 추첨 결과 (재미용)", FUi.F(9f, 0), Theme.Muted, new RectangleF(card.X, card.Y + 56, card.Width, 20), StringAlignment.Center, StringAlignment.Center, false);
		using (Pen p = new Pen(Color.FromArgb(120, Theme.Line), 1f)) g.DrawLine(p, card.X + 30, card.Y + 86, card.Right - 30, card.Y + 86);
		List<int> main = new List<int>(); for (int i = 0; i < Math.Min(6, drawn.Count); i++) main.Add(drawn[i]); main.Sort();
		float R = 27f, gap = 58f, x0 = card.X + card.Width / 2f - gap * 2.5f, by = card.Y + 142f;
		for (int i = 0; i < main.Count; i++)
		{
			float u = Pop((t - 0.2f - i * 0.16f) / 0.4f); if (u <= 0f) continue; float x = x0 + i * gap, r = R * u;
			using (GraphicsPath gl = new GraphicsPath()) { gl.AddEllipse(x - R * 1.6f, by - R * 1.6f, R * 3.2f, R * 3.2f); using (PathGradientBrush pb = new PathGradientBrush(gl)) { pb.CenterColor = Color.FromArgb((int)(Math.Min(1f, u) * 70), BallColor(main[i])); pb.SurroundColors = new[] { Color.FromArgb(0, BallColor(main[i])) }; g.FillPath(pb, gl); } }
			DrawBall3(g, x, by, r, main[i], Front, 0.7f);
		}
		float bt = t - 0.2f - 6 * 0.16f - 0.15f;
		if (drawn.Count >= 7)
		{
			FUi.Text(g, "+", FUi.F(22f, 1), Theme.Muted, new RectangleF(card.X + card.Width / 2f - 20, by + 40, 40, 30), StringAlignment.Center, StringAlignment.Center, false);
			float u = Pop(bt / 0.45f);
			if (u > 0f)
			{
				float bx = card.X + card.Width / 2f, bcy = by + 112f;
				using (GraphicsPath gl = new GraphicsPath()) { gl.AddEllipse(bx - R * 2f, bcy - R * 2f, R * 4f, R * 4f); using (PathGradientBrush pb = new PathGradientBrush(gl)) { pb.CenterColor = Color.FromArgb((int)(Math.Min(1f, u) * 90), 255, 210, 90); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 210, 90) }; g.FillPath(pb, gl); } }
				DrawBall3(g, bx, bcy, R * 1.1f * u, drawn[6], Front, 0.7f);
				FUi.Text(g, "보너스", FUi.F(9.5f, 1), Theme.Sub, new RectangleF(bx - 40, bcy + R * 1.1f + 4, 80, 20), StringAlignment.Center, StringAlignment.Center, false);
			}
		}
		bool cp = copiedT > 0f;
		foreach (int k in new[] { 0, 1 })
		{
			RectangleF r = k == 0 ? new RectangleF(ResCopy.X, ResCopy.Y + oy, ResCopy.Width, ResCopy.Height) : new RectangleF(ResAgain.X, ResAgain.Y + oy, ResAgain.Width, ResAgain.Height);
			using (GraphicsPath gp = Gfx.Round(r, 19))
			{
				if (k == 1) using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillPath(b, gp);
				else { using (SolidBrush b = new SolidBrush(Theme.Bg)) g.FillPath(b, gp); using (Pen p = new Pen(Theme.Accent, 1.6f)) g.DrawPath(p, gp); }
			}
			FUi.Text(g, k == 1 ? "다시 뽑기" : (cp ? "복사했어요 ✓" : "번호 복사"), FUi.F(11f, 1), k == 1 ? Color.White : Theme.Accent, r, StringAlignment.Center, StringAlignment.Center, false);
		}
		RectangleF xr = new RectangleF(ResX.X, ResX.Y + oy, ResX.Width, ResX.Height);
		FUi.Text(g, "✕", FUi.F(11f, 1), Theme.Muted, xr, StringAlignment.Center, StringAlignment.Center, false);
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
		if (finished && !resOpen) FUi.Text(g, "결과 칸을 누르면 당첨 번호를 크게 볼 수 있어요", FUi.F(8.5f, 0), Theme.Muted, new RectangleF(16, 598, 388, 16), StringAlignment.Center, StringAlignment.Center, false);
		if (ResVisible) DrawResult(g);
	}
}
