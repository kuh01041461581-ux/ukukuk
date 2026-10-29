using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 24. 얼음 동굴 (포켓몬 금은 얼음길 방식: 한번 미끄러지면 벽·바위에 부딪힐 때까지 이동) ──
internal sealed class IceGame : MiniGame
{
	private const int N = 8; private const float T = 46f, X0 = 16f, Y0 = 132f;
	private readonly bool[,] rock = new bool[N, N];
	private int px, py, ex, ey, sx, sy, level, moves, par, dirX, dirY; private double left; private float msgT, animT, winT; private string msg = "";
	private readonly List<Point> path = new List<Point>(); private readonly List<Point> undo = new List<Point>(); private readonly List<Point> trail = new List<Point>();
	private bool sliding; private Point dest; private int bdx, bdy;
	public override string Costume { get { return "겨울"; } }
	public override string Name { get { return "얼음 동굴"; } }
	public override string Key { get { return "ice"; } }
	public override string Desc { get { return "미끄러져서 출구를 찾아요"; } }
	public override string Help { get { return "방향키(또는 화면을 쓸어 넘기기·마룡이 주변 클릭)로 이동 방향을 정해요\n얼음 위에서는 바위나 벽에 부딪힐 때까지 쭉 미끄러져요\n계단(출구)에 멈추면 성공! 판은 매번 새로 만들어져요\n막히면 '한 수 취소'나 '처음부터'를 누르세요 · 60초"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 220, 250))) g.FillEllipse(b, cx - 20, cy - 14, 40, 30);
		using (SolidBrush b = new SolidBrush(Color.White)) { g.FillEllipse(b, cx - 12, cy - 8, 14, 5); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 110, 150))) g.FillEllipse(b, cx + 2, cy - 4, 14, 14);
	}
	public override void Reset() { left = 60; Score = 0; Over = false; level = 0; msg = ""; msgT = 0; winT = 0; NewMap(); }
	private Point Slide(int x, int y, int dx, int dy, out int cells)
	{
		cells = 0;
		while (true)
		{
			int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= N || ny >= N || rock[nx, ny]) break;
			x = nx; y = ny; cells++; if (x == ex && y == ey) break;
		}
		return new Point(x, y);
	}
	private int Solve(int fx, int fy)
	{
		int[,] d = new int[N, N]; for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) d[i, j] = -1;
		Queue<Point> q = new Queue<Point>(); q.Enqueue(new Point(fx, fy)); d[fx, fy] = 0; int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };
		while (q.Count > 0)
		{
			Point p = q.Dequeue(); if (p.X == ex && p.Y == ey) return d[p.X, p.Y];
			for (int k = 0; k < 4; k++) { int c; Point r = Slide(p.X, p.Y, DX[k], DY[k], out c); if (c > 0 && d[r.X, r.Y] < 0) { d[r.X, r.Y] = d[p.X, p.Y] + 1; q.Enqueue(r); } }
		}
		return -1;
	}
	private void NewMap()
	{
		int want = Math.Min(12, 3 + level); int bestD = -1; bool[,] bestRock = new bool[N, N]; int bsx = 0, bsy = 0, bex = 0, bey = 0;
		for (int tries = 0; tries < 400; tries++)
		{
			for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) rock[i, j] = false;
			int nr = 12 + Rnd.Next(9); for (int k = 0; k < nr; k++) rock[Rnd.Next(N), Rnd.Next(N)] = true;
			do { sx = Rnd.Next(N); sy = Rnd.Next(N); } while (rock[sx, sy]);
			do { ex = Rnd.Next(N); ey = Rnd.Next(N); } while (rock[ex, ey] || (ex == sx && ey == sy));
			int d = Solve(sx, sy); if (d < 0) continue;
			if (d > bestD) { bestD = d; Array.Copy(rock, bestRock, rock.Length); bsx = sx; bsy = sy; bex = ex; bey = ey; }
			if (d >= want && d <= want + 2) { bestD = d; Array.Copy(rock, bestRock, rock.Length); bsx = sx; bsy = sy; bex = ex; bey = ey; break; }
		}
		Array.Copy(bestRock, rock, rock.Length); sx = bsx; sy = bsy; ex = bex; ey = bey; par = bestD < 1 ? 1 : bestD;
		if (bestD < 0) { for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) rock[i, j] = false; sx = 0; sy = 0; ex = 7; ey = 0; par = 1; }
		px = sx; py = sy; moves = 0; sliding = false; bdx = bdy = 0; undo.Clear(); trail.Clear(); winT = 0f;
	}
	private float shT, shX, shY;
	private void Go(int dx, int dy)
	{
		if (Over || winT > 0f) return; if (sliding) { bdx = dx; bdy = dy; return; }   // 미끄러지는 중에 누른 방향은 기억했다가 멈추면 바로 이동해요
		int c; Point r = Slide(px, py, dx, dy, out c);
		if (c == 0) { Sfx.Play("tick"); return; }
		undo.Add(new Point(px, py)); path.Clear(); int x = px, y = py; for (int i = 0; i < c; i++) { x += dx; y += dy; path.Add(new Point(x, y)); }
		dest = r; dirX = dx; dirY = dy; sliding = true; animT = 0f; moves++; Sfx.Play("whoosh");
	}
	public override void Press(Keys k)
	{
		if (k == Keys.Left || k == Keys.A) Go(-1, 0); else if (k == Keys.Right || k == Keys.D) Go(1, 0); else if (k == Keys.Up || k == Keys.W) Go(0, -1); else if (k == Keys.Down || k == Keys.S) Go(0, 1);
		else if (k == Keys.Z || k == Keys.Back) Undo(); else if (k == Keys.R) Restart();
	}
	public override void Swipe(float dx, float dy) { if (Math.Abs(dx) > Math.Abs(dy)) Go(dx > 0 ? 1 : -1, 0); else Go(0, dy > 0 ? 1 : -1); }
	private void Undo() { if (sliding || winT > 0f || undo.Count == 0) return; Point p = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); px = p.X; py = p.Y; Sfx.Play("ui_back"); }
	private void Restart() { if (sliding || winT > 0f) return; px = sx; py = sy; undo.Clear(); Sfx.Play("ui_back"); }
	public override void Click(float x, float y)
	{
		if (y >= 516f && y <= 568f) { if (x >= 20f && x <= 190f) Undo(); else if (x >= 210f && x <= 380f) Restart(); return; }
		if (sliding) return; float cx = X0 + px * T + T / 2f, cy = Y0 + py * T + T / 2f, dx = x - cx, dy = y - cy;
		if (Math.Abs(dx) < 12f && Math.Abs(dy) < 12f) return;
		if (Math.Abs(dx) > Math.Abs(dy)) Go(dx > 0 ? 1 : -1, 0); else Go(0, dy > 0 ? 1 : -1);
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (shT > 0f) shT -= dt;
		left -= dt; if (left > 60 - PlayT) left = 60 - PlayT; if (left <= 0) { left = 0; Over = true; return; } if (msgT > 0) msgT -= dt;
		if (sliding)
		{
			animT += dt * 15f;
			if (animT >= path.Count)
			{
				sliding = false; px = dest.X; py = dest.Y; { int qx = bdx, qy = bdy; bdx = bdy = 0; if ((qx != 0 || qy != 0) && !(px == ex && py == ey)) Go(qx, qy); } trail.Add(new Point(px, py)); if (trail.Count > 6) trail.RemoveAt(0);
				if (px == ex && py == ey)
				{
					int pts = 30 + Math.Max(0, par + 3 - moves) * 6 + level * 3; Score += pts; left += level >= 6 ? 6 : 8; level++; winT = 1.1f;
					msg = moves <= par ? "최단 경로! +" + pts : "탈출 성공! +" + pts; msgT = 1.3f; Sfx.Play("fanfare"); Fx.Burst(X0 + ex * T + T / 2f, Y0 + ey * T + T / 2f, Color.FromArgb(255, 226, 120), 14);
				}
				else { Sfx.Play("thud_soft"); Fx.Burst(X0 + px * T + T / 2f + dirX * 18f, Y0 + py * T + T / 2f + dirY * 18f, Color.FromArgb(220, 240, 255), 5); Shake = 0.08f; shT = 0.45f; shX = X0 + px * T + T / 2f + dirX * 22f; shY = Y0 + py * T + T / 2f + dirY * 22f; }
			}
		}
		if (winT > 0f) { winT -= dt; if (winT <= 0f) NewMap(); }
	}
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_ice", Color.FromArgb(34, 52, 90), Color.FromArgb(18, 28, 56)); Snow(g, Environment.TickCount / 1000.0, 14);
		Txt(g, (level + 1) + "번째 동굴 · 미끄러진 횟수 " + moves + " (최소 " + par + ")", 12f, 2, Color.FromArgb(255, 24, 64, 118), 0, 58, W, 26, StringAlignment.Center);
		Hint(g, "얼음 위는 멈추지 않아요! 바위에 부딪혀 멈추세요", 86 + 11.0, 10.5f, false, 1f);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(X0 - 6, Y0 - 6, N * T + 12, N * T + 12), 18)) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 190, 225))) g.FillPath(b, gp);
		for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
		{
			float x = X0 + i * T, y = Y0 + j * T; bool alt = (i + j) % 2 == 0;
			using (SolidBrush b = new SolidBrush(alt ? Color.FromArgb(176, 226, 248) : Color.FromArgb(160, 214, 242))) g.FillRectangle(b, x, y, T, T);
			if ((i * 7 + j * 3) % 5 == 0) using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x + 8, y + 12, x + 18, y + 8);
		}
		foreach (Point p in trail) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillEllipse(b, X0 + p.X * T + 14, Y0 + p.Y * T + 14, T - 28, T - 28);
		// 출구
		{
			float x = X0 + ex * T, y = Y0 + ey * T; using (GraphicsPath gp = Gfx.Round(new RectangleF(x + 3, y + 3, T - 6, T - 6), 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 28, 40))) g.FillPath(b, gp);
			for (int k = 0; k < 3; k++) using (SolidBrush b = new SolidBrush(Color.FromArgb(150 + k * 30, 180 + k * 20, 130 + k * 15))) g.FillRectangle(b, x + 9 + k * 3, y + 10 + k * 9, T - 18 - k * 6, 7);
			using (Pen p = new Pen(Color.FromArgb(120 + (int)(100 * Math.Sin(t * 5)), 255, 226, 120), 2.5f)) g.DrawRectangle(p, x + 2, y + 2, T - 4, T - 4);
			if (Spr.Get("ms_cu_peng") != null) { float pw = T * 0.62f; Spr.Draw(g, "ms_cu_peng", x + T * 0.5f, y + T * 0.46f + (float)Math.Sin(t * 4) * 1.5f, pw, pw * 120f / 90f, (float)Math.Sin(t * 3) * 4f); }
		}
		for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) if (rock[i, j])
		{
			float x = X0 + i * T, y = Y0 + j * T;
			if (Spr.Get("ms_ic1") != null) { using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 20, 40, 90))) g.FillEllipse(b, x + 4, y + T - 14, T - 8, 12); bool tall = ((i * 5 + j * 3) & 1) == 0; float hh = T * (tall ? 1.15f : 0.95f), ww = hh * (tall ? 186f / 240f : 188f / 200f); Spr.Draw(g, tall ? "ms_ic2" : "ms_ic1", x + T / 2f, y + T - 6 - hh / 2f, ww, hh, 0f); continue; }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 20, 30, 60))) g.FillEllipse(b, x + 5, y + 26, T - 10, 14);
			using (GraphicsPath gp = Gfx.Round(new RectangleF(x + 5, y + 6, T - 10, T - 14), 12)) { using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(x, y, T, T), Color.FromArgb(150, 160, 190), Color.FromArgb(84, 92, 124), 90f)) g.FillPath(lb, gp); using (Pen p = new Pen(Color.FromArgb(120, 40, 50, 80), 1.4f)) g.DrawPath(p, gp); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(b, x + 11, y + 10, 12, 7);
		}
		// 마룡이
		float mx = X0 + px * T + T / 2f, my = Y0 + py * T + T / 2f;
		if (sliding) { int a = Math.Min(path.Count - 1, (int)animT); float f = animT - a; Point from = a == 0 ? new Point(px, py) : path[a - 1]; Point to = path[Math.Min(a, path.Count - 1)]; float ff = Math.Min(1f, f); mx = X0 + (from.X + (to.X - from.X) * ff) * T + T / 2f; my = Y0 + (from.Y + (to.Y - from.Y) * ff) * T + T / 2f; }
		float tilt = sliding ? dirX * 8f : (float)Math.Sin(t * 3) * 2f;
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 20, 40, 80))) g.FillEllipse(b, mx - 15, my + 12, 30, 9);
		if (sliding && Spr.Get("ms_ic_sl") != null) { bool hz = Math.Abs(dirX) >= Math.Abs(dirY); float sw = 62f, sh = sw * 86f / 108f; Spr.Draw(g, "ms_ic_sl", mx, my + 4f, hz ? (dirX < 0 ? -sw : sw) : sw, sh, hz ? 0f : (dirY > 0 ? 90f : -90f)); }
		else DrawMascot(g, mx, my + 20f, 54f, tilt);
		if (shT > 0f) { float k = 1f - shT / 0.45f; DrawFx(g, "ice_sh", shX, shY, 30f + 34f * k, Math.Min(1f, shT / 0.2f)); }
		// 버튼
		using (GraphicsPath gp = Gfx.Round(new RectangleF(20, 516, 170, 52), 16)) using (SolidBrush b = new SolidBrush(Color.FromArgb(undo.Count > 0 ? 170 : 80, 90, 130, 200))) g.FillPath(b, gp);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(210, 516, 170, 52), 16)) using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 200, 110, 110))) g.FillPath(b, gp);
		Txt(g, "↩ 한 수 취소", 14f, 2, Color.White, 20, 516, 170, 52, StringAlignment.Center); Txt(g, "처음부터", 14f, 2, Color.White, 210, 516, 170, 52, StringAlignment.Center);
		if (msgT > 0) Txt(g, msg, 22f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 236, 130), 0, 50, W, 34, StringAlignment.Center);
	}
}
