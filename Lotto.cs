using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

// ── 재미로 보는 로또 추첨기: 통 안의 공 1~45 중에서 6개 + 보너스 1개를 중복 없이 뽑아요 ──────────
internal sealed class LottoForm : Form
{
	private static LottoForm inst; private readonly float sc = FUi.Scale(420, 620);
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new LottoForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }
	private static readonly Font numBig = new Font("Arial", 15f, FontStyle.Bold, GraphicsUnit.Pixel), numSmall = new Font("Arial", 11.5f, FontStyle.Bold, GraphicsUnit.Pixel);
	private class Ball { public int N; public float X, Y, Vx, Vy; }
	private readonly List<Ball> pool = new List<Ball>(); private readonly List<int> drawn = new List<int>(); private readonly Random rnd = new Random(); private readonly Timer tm = new Timer();
	private const float CX = 210f, CY = 200f, CR = 150f, BR = 14f;
	private Ball rising; private float riseT, nextT; private bool running, finished; private RectangleF btn = new RectangleF(120, 560, 180, 38);
	public LottoForm()
	{
		Text = "로또 추첨기 (재미용)"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(420 * sc), (int)(620 * sc)); DoubleBuffered = true; BackColor = Theme.Bg;
		Reset(); tm.Interval = 16; tm.Tick += delegate { Step(0.016f); Invalidate(); }; tm.Start(); FormClosed += delegate { tm.Stop(); tm.Dispose(); };
		MouseDown += delegate(object s, MouseEventArgs e) { if (btn.Contains(e.X / sc, e.Y / sc)) { if (finished || (!running && drawn.Count == 0)) { if (finished) Reset(); running = true; nextT = 0.5f; try { Sfx.Play("whoosh"); } catch { } } } };
	}
	private void Reset()
	{
		pool.Clear(); drawn.Clear(); rising = null; running = false; finished = false;
		for (int i = 1; i <= 45; i++) { double a = rnd.NextDouble() * 6.283, r = Math.Sqrt(rnd.NextDouble()) * (CR - BR - 4); pool.Add(new Ball { N = i, X = CX + (float)Math.Cos(a) * (float)r, Y = CY + (float)Math.Sin(a) * (float)r, Vx = (float)(rnd.NextDouble() - 0.5) * 300f, Vy = (float)(rnd.NextDouble() - 0.5) * 300f }); }
	}
	private void Step(float dt)
	{
		float stir = running && rising == null ? 1f : (running ? 0.7f : 0.35f);
		foreach (Ball b in pool)
		{
			b.Vy += 260f * dt * (running ? 0.4f : 1f); b.Vx += (float)(rnd.NextDouble() - 0.5) * 900f * dt * stir; b.Vy += (float)(rnd.NextDouble() - 0.6) * 1300f * dt * stir;
			float sp = (float)Math.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy), mx = running ? 420f : 220f; if (sp > mx) { b.Vx *= mx / sp; b.Vy *= mx / sp; }
			b.X += b.Vx * dt; b.Y += b.Vy * dt;
			float dx = b.X - CX, dy = b.Y - CY, d = (float)Math.Sqrt(dx * dx + dy * dy), lim = CR - BR;
			if (d > lim) { float nx = dx / d, ny = dy / d; b.X = CX + nx * lim; b.Y = CY + ny * lim; float vn = b.Vx * nx + b.Vy * ny; if (vn > 0) { b.Vx -= 1.7f * vn * nx; b.Vy -= 1.7f * vn * ny; } }
		}
		for (int i = 0; i < pool.Count; i++) for (int j = i + 1; j < pool.Count; j++)
		{
			Ball a = pool[i], c = pool[j]; float dx = c.X - a.X, dy = c.Y - a.Y, d2 = dx * dx + dy * dy, r = BR * 2f; if (d2 >= r * r || d2 < 0.0001f) continue; float d = (float)Math.Sqrt(d2), nx = dx / d, ny = dy / d, push = (r - d) / 2f;
			a.X -= nx * push; a.Y -= ny * push; c.X += nx * push; c.Y += ny * push; float rv = (c.Vx - a.Vx) * nx + (c.Vy - a.Vy) * ny; if (rv < 0) { a.Vx += rv * nx; a.Vy += rv * ny; c.Vx -= rv * nx; c.Vy -= rv * ny; }
		}
		if (running)
		{
			if (rising != null)
			{
				riseT += dt; float u = Math.Min(1f, riseT / 0.9f); float e = u * u * (3f - 2f * u); rising.X = rising.Vx + (CX - rising.Vx) * e * 0.0f; rising.Y = rising.Vy + (-14f - rising.Vy) * e;
				if (u >= 1f) { drawn.Add(rising.N); rising = null; try { Sfx.Play(drawn.Count == 7 ? "fanfare" : "ok"); } catch { } nextT = 0.9f; if (drawn.Count >= 7) { running = false; finished = true; } }
			}
			else if (pool.Count > 0) { nextT -= dt; if (nextT <= 0f) { Ball b = pool[rnd.Next(pool.Count)]; pool.Remove(b); rising = b; riseT = 0f; b.Vx = b.X; b.Vy = b.Y; try { Sfx.Play("tick"); } catch { } } }
		}
	}
	private static Color BallColor(int n) { return n <= 10 ? Color.FromArgb(244, 190, 40) : (n <= 20 ? Color.FromArgb(70, 140, 230) : (n <= 30 ? Color.FromArgb(226, 80, 76) : (n <= 40 ? Color.FromArgb(130, 134, 146) : Color.FromArgb(80, 176, 100)))); }
	private void DrawBall(Graphics g, float x, float y, float r, int n)
	{
		Color c = BallColor(n); using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, x - r + 1.5f, y - r + 3f, r * 2, r * 2);
		using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x - r, y - r, r * 2, r * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(x - r * 0.35f, y - r * 0.4f); pb.CenterColor = Theme.Mix(c, Color.White, 0.55); pb.SurroundColors = new[] { Theme.Mix(c, Color.Black, 0.18) }; g.FillPath(pb, gp); } }
		using (SolidBrush w = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillEllipse(w, x - r * 0.62f, y - r * 0.62f, r * 1.24f, r * 1.24f);
		Font nf = r >= 18f ? numBig : numSmall; using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (SolidBrush tb = new SolidBrush(Color.FromArgb(40, 40, 50))) { TextRenderingHint oh = g.TextRenderingHint; g.TextRenderingHint = TextRenderingHint.AntiAlias; g.DrawString(n.ToString(), nf, tb, new RectangleF(x - r, y - r + 0.5f, r * 2, r * 2), sf); g.TextRenderingHint = oh; }
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(Theme.Bg); g.ScaleTransform(sc, sc);
		FUi.Text(g, "로또 추첨기", FUi.F(15f, 1), Theme.Ink, new RectangleF(16, 10, 300, 28), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "1~45번 공 중에서 6개 + 보너스 1개 · 한 번 나온 공은 다시 안 나와요 (재미용)", FUi.F(8.5f, 0), Theme.Muted, new RectangleF(16, 36, 400, 18), StringAlignment.Near, StringAlignment.Center, false);
		// 통
		using (SolidBrush b = new SolidBrush(Color.FromArgb(Theme.Dark ? 50 : 36, 120, 160, 220))) g.FillEllipse(b, CX - CR, CY - CR, CR * 2, CR * 2);
		foreach (Ball bl in pool) DrawBall(g, bl.X, bl.Y, BR, bl.N);
		using (Pen p = new Pen(Color.FromArgb(190, 150, 190, 235), 5f)) g.DrawEllipse(p, CX - CR, CY - CR, CR * 2, CR * 2); using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawArc(p, CX - CR + 10, CY - CR + 10, CR * 2 - 20, CR * 2 - 20, 200, 60);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 150, 160, 190))) g.FillRectangle(b, CX - 26, CY - CR - 26, 52, 18);   // 배출구
		if (rising != null) DrawBall(g, rising.X, rising.Y, BR + 5f * Math.Min(1f, riseT / 0.9f), rising.N);
		// 결과 트레이
		using (GraphicsPath gp = Gfx.Round(new RectangleF(16, 372, 388, 170), 16)) { using (SolidBrush b = new SolidBrush(Theme.Page)) g.FillPath(b, gp); using (Pen p = new Pen(Theme.Line, 1f)) g.DrawPath(p, gp); }
		FUi.Text(g, finished ? "이번 번호" : "뽑힌 번호", FUi.F(10f, 1), Theme.Sub, new RectangleF(30, 380, 200, 22), StringAlignment.Near, StringAlignment.Center, false);
		List<int> main = new List<int>(); for (int i = 0; i < Math.Min(6, drawn.Count); i++) main.Add(drawn[i]); if (finished) main.Sort();
		for (int i = 0; i < 6; i++) { float x = 50f + i * 54f, y = 440f; if (i < main.Count) DrawBall(g, x, y, 21f, main[i]); else using (Pen p = new Pen(Theme.Line, 2f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(p, x - 21, y - 21, 42, 42); }
		FUi.Text(g, "+", FUi.F(16f, 1), Theme.Muted, new RectangleF(352, 424, 24, 32), StringAlignment.Center, StringAlignment.Center, false);
		if (drawn.Count >= 7) DrawBall(g, 388f - 6f, 500f, 21f, drawn[6]); else using (Pen p = new Pen(Theme.Line, 2f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(p, 388f - 27f, 479f, 42, 42);
		FUi.Text(g, "보너스", FUi.F(8.5f, 0), Theme.Muted, new RectangleF(300, 500, 60, 20), StringAlignment.Far, StringAlignment.Center, false);
		bool can = finished || (!running && drawn.Count == 0);
		using (GraphicsPath gp = Gfx.Round(btn, 19)) using (SolidBrush b = new SolidBrush(can ? Theme.Accent : Theme.Line)) g.FillPath(b, gp);
		FUi.Text(g, finished ? "다시 뽑기" : (running ? "추첨 중…" : "추첨 시작!"), FUi.F(11f, 1), can ? Color.White : Theme.Muted, btn, StringAlignment.Center, StringAlignment.Center, false);
	}
}
