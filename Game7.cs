using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 올림픽·파티 종목 6종: 컬링, 리듬 도장, 높이뛰기, 역도, 슬라이딩 퍼즐, 마룡이 런 ──

// ── 25. 컬링 (CPU 대결 · 회전) ──────────────────────────────
internal sealed class CurlGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class St { public float X, Y, Vx, Vy, Spin; public bool Moving; public int Owner; }
	private readonly List<St> stones = new List<St>(); private readonly List<St> swp = new List<St>(); private readonly List<float> swl = new List<float>();
	private const float BX = 200f, BY = 150f, R = 16f, HR = 90f, SY = 520f, Hog = 410f;
	private bool coinHeads; private float coinT; private int endNo, thr, state, myPts, cpuPts, first; private float power, chargeT, spin, pressX, settle, msgT, thinkT, resT; private bool sweeping, locked, wasH; private string msg = ""; private St cur; private float mxx = 200f, myy = 300f, adx, ady;
	private float cdx, cdy, cv0, sweepX, sweepY, sweepA; // CPU 계획
	public override string Costume { get { return "g_curl"; } }
	public override string Name { get { return "컬링"; } }
	public override string Key { get { return "curl"; } }
	public override string Desc { get { return "CPU와 대결! 회전을 줘서 휘어 던져요"; } }
	public override string Help { get { return "CPU와 번갈아 돌을 4개씩 밀어요 · 과녁 중심에 가장 가까운 쪽이 점수를 가져가요\n① 마우스로 방향을 잡고 누르면 방향이 고정돼요 ② 누른 채 좌우로 끌면 회전(휘어짐)\n③ 힘 게이지가 알맞을 때 놓기 · 미끄러지는 동안 스톤 앞쪽(노란 영역)을 누르고 있으면 문질러서 더 멀리·곧게 가요\n노란 선(힘 부족) 앞에서 멈춘 돌은 빠져요 · 상대 돌을 쳐내도 돼요\nCPU가 던진 돌도 앞을 누르고 있으면 비질할 수 있어요 → 더 멀리 미끄러져 과녁을 지나치게!"; } }
	public override string RightText { get { return "나 " + myPts + " : " + cpuPts + " CPU"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 120, 140))) g.FillEllipse(b, cx - 16, cy - 6, 32, 24);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 70, 70))) g.FillRectangle(b, cx - 6, cy - 14, 12, 9);
	}
	public override void Reset() { stones.Clear(); endNo = 0; thr = 0; myPts = 0; cpuPts = 0; Score = 0; Over = false; msg = ""; msgT = 0; first = 0; wasH = false; locked = false; cur = null; coinHeads = Rnd.Next(2) == 0; coinT = 0f; state = 7; }
	private void BeginTurn()
	{
		int owner = (thr % 2 == 0) ? first : 1 - first; cur = new St { X = BX, Y = SY, Owner = owner }; stones.Add(cur); chargeT = 0; power = 0; spin = 0; sweeping = false; locked = false; settle = 0;
		if (owner == 0) state = 0; else { state = 1; thinkT = 1.1f; PlanCpu(); }
	}
	private float Dist(St s) { float dx = s.X - BX, dy = s.Y - BY; return (float)Math.Sqrt(dx * dx + dy * dy); }
	private void PlanCpu()
	{
		St tgt = null; float best = 9999f; foreach (St s in stones) { if (s == cur || s.Owner != 0) continue; float d = Dist(s); if (d < HR + R && d < best) { best = d; tgt = s; } }
		float myBest = 9999f; foreach (St s in stones) if (s != cur && s.Owner == 1) myBest = Math.Min(myBest, Dist(s));
		float tx, ty, v0;
		if (tgt != null && best < myBest && Rnd.NextDouble() < 0.65) { tx = tgt.X; ty = tgt.Y; float D = SY - ty; v0 = Math.Min(520f, 210f + 0.9f * D); }
		else { tx = BX + ((float)Rnd.NextDouble() - 0.5f) * 30f; ty = BY + ((float)Rnd.NextDouble() - 0.4f) * 30f; float D = SY - ty; v0 = 0.9f * D; }
		float ax = tx - BX, ay = ty - SY, L = (float)Math.Sqrt(ax * ax + ay * ay); ax /= L; ay /= L; float nf = Math.Max(0.35f, 1f - endNo * 0.22f); float ang = ((float)Rnd.NextDouble() - 0.5f) * 2f * 0.045f * nf; /* 판이 거듭될수록 CPU가 정교해져요 */ float c = (float)Math.Cos(ang), sn = (float)Math.Sin(ang);
		cdx = ax * c - ay * sn; cdy = ax * sn + ay * c; cv0 = v0 * (1f + ((float)Rnd.NextDouble() - 0.5f) * 0.11f * nf);
	}
	private void Launch(St s, float dx, float dy, float v0, float sp) { s.Vx = dx * v0; s.Vy = dy * v0; s.Spin = sp; s.Moving = true; state = 2; settle = 0; Sfx.Play("whoosh"); Fx.Burst(s.X, s.Y, Color.FromArgb(220, 240, 255), 3 + (int)Math.Min(6f, v0 / 120f)); }
	private void Step(St a, float h, bool sweep, float lat = 0f)
	{
		float v = (float)Math.Sqrt(a.Vx * a.Vx + a.Vy * a.Vy); if (v < 1f) return;
		float k = sweep ? 0.55f : 0.9f, f = (float)Math.Exp(-k * h);
		if (a.Spin != 0f) { float acc = a.Spin * 26f / (1f + v / 80f) * (sweep ? 0.35f : 1f); a.Vx += (-a.Vy / v) * acc * h; a.Vy += (a.Vx / v) * acc * h; }
		if (sweep && lat != 0f) { a.Vx += (-a.Vy / v) * lat * h; a.Vy += (a.Vx / v) * lat * h; }   // 옆쪽을 문지르면 그쪽으로 조금 밀려요
		a.Vx *= f; a.Vy *= f; a.X += a.Vx * h; a.Y += a.Vy * h;
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		mxx = inp.Mx; myy = inp.My; if (msgT > 0) msgT -= dt;
		if (state == 7) { float c0 = coinT; coinT += dt; if (c0 < 1.5f && coinT >= 1.5f) Sfx.Play(coinHeads ? "ok" : "tick"); wasH = inp.Hold; if (coinT > 3.0f || (coinT > 1.7f && inp.Hold)) { first = coinHeads ? 0 : 1; BeginTurn(); } return; }
		if (state == 0)
		{
			if (!locked && inp.Hold && !wasH) { float ax = mxx - BX, ay = Math.Min(myy, 470f) - SY, L = (float)Math.Sqrt(ax * ax + ay * ay); if (L < 1f) { ax = 0; ay = -1; L = 1; } adx = ax / L; ady = ay / L; locked = true; pressX = mxx; chargeT = 0; }
			if (locked)
			{
				if (inp.Hold) { chargeT += dt; spin = PF.Cl((mxx - pressX) / 50f, -1f, 1f); float u = (chargeT * 0.9f) % 2f; power = u < 1f ? u : 2f - u; }
				else { locked = false; if (chargeT > 0.12f) Launch(cur, adx, ady, 150f + power * 360f, spin); }
			}
		}
		else if (state == 1)
		{
			thinkT -= dt; if (thinkT <= 0f) Launch(cur, cdx, cdy, cv0, 0f);
		}
		else if (state == 2)
		{
			// 스톤 바로 앞(진행 방향 쪽)을 누르고 있어야 얼음을 문질러서 더 멀리 가요
			sweeping = false; swp.Clear(); swl.Clear();
			if (inp.Hold) foreach (St a0 in stones)   // CPU 차례에도 비질할 수 있어요 (상대 돌을 더 밀어서 과녁 밖으로!)
			{
				if (!a0.Moving) continue; float vx = a0.Vx, vy = a0.Vy, vl = (float)Math.Sqrt(vx * vx + vy * vy) + 0.001f, ux = vx / vl, uy = vy / vl, rx = mxx - a0.X, ry = myy - a0.Y; float along = rx * ux + ry * uy, sd = -rx * uy + ry * ux;
				if (along > -10f && along < 150f && Math.Abs(sd) < 60f) { swp.Add(a0); float k2 = PF.Cl(sd / 30f, -1f, 1f); swl.Add(k2 * 22f); sweeping = true; sweepX = mxx; sweepY = myy; }
			}
			int n = 4; float h = dt / n;
			for (int s = 0; s < n; s++)
			{
				foreach (St a in stones) { if (!a.Moving) continue; { int si = swp.IndexOf(a); Step(a, h, si >= 0, si >= 0 ? swl[si] : 0f); } if (a.Vx * a.Vx + a.Vy * a.Vy < 64f) { a.Vx = a.Vy = 0f; a.Moving = false; } }
				for (int i = 0; i < stones.Count; i++) for (int j = i + 1; j < stones.Count; j++) Collide(stones[i], stones[j]);
			}
			for (int i = stones.Count - 1; i >= 0; i--) { St a = stones[i]; if (a.X < 62f || a.X > 338f || a.Y < 66f) { Sfx.Play("fall"); Fx.Burst(Math.Max(66f, Math.Min(334f, a.X)), Math.Max(70f, a.Y), Color.FromArgb(200, 220, 255), 6); if (a == cur) cur = new St(); stones.RemoveAt(i); } }
			bool any = false; foreach (St a in stones) if (a.Moving) any = true;
			if (!any)
			{
				settle += dt;
				if (settle > 0.5f)
				{
					for (int i = stones.Count - 1; i >= 0; i--) if (stones[i].Y > Hog) { stones.RemoveAt(i); msg = "힘이 부족해서 빠졌어요"; msgT = 1.4f; Sfx.Play("bad2"); }
					thr++; if (thr >= 8) ScoreEnd(); else BeginTurn();
				}
			}
			else settle = 0f;
		}
		else if (state == 3) { resT -= dt; if (resT <= 0f) { state = 4; resT = 0f; } }
		else if (state == 4) { resT += dt; if (resT > 1.6f) Over = true; }
		wasH = inp.Hold;
	}
	private void ScoreEnd()
	{
		St w = null; float bd = 9999f; foreach (St s in stones) { float d = Dist(s); if (d < HR + R && d < bd) { bd = d; w = s; } }
		int pts = 0; if (w != null) { float ob = 9999f; foreach (St s in stones) if (s.Owner != w.Owner) ob = Math.Min(ob, Dist(s)); foreach (St s in stones) if (s.Owner == w.Owner && Dist(s) < HR + R && Dist(s) < ob) pts++; }
		if (w == null) msg = "무득점"; else if (w.Owner == 0) { myPts += pts; msg = "내가 " + pts + "점!"; Sfx.Play("fanfare"); } else { cpuPts += pts; msg = "CPU " + pts + "점"; Sfx.Play("bad2"); }
		msgT = 2.4f; state = 3; resT = 2.4f; Score = myPts * 10 + (myPts > cpuPts ? 30 : (myPts == cpuPts ? 10 : 0));
	}
	private void Collide(St a, St b)
	{
		float dx = b.X - a.X, dy = b.Y - a.Y, d = (float)Math.Sqrt(dx * dx + dy * dy); if (d >= 2 * R || d < 0.001f) return;
		float nx = dx / d, ny = dy / d, push = (2 * R - d) / 2f; a.X -= nx * push; a.Y -= ny * push; b.X += nx * push; b.Y += ny * push;
		float rv = (b.Vx - a.Vx) * nx + (b.Vy - a.Vy) * ny; if (rv < 0) { float j = -rv * 0.95f; a.Vx -= j * nx; a.Vy -= j * ny; b.Vx += j * nx; b.Vy += j * ny; a.Moving = b.Moving = true; a.Spin = b.Spin = 0f; Sfx.Play(j > 90f ? "thud" : "thud_soft"); Fx.Burst((a.X + b.X) / 2f, (a.Y + b.Y) / 2f, Color.FromArgb(230, 240, 255), 2 + (int)Math.Min(10f, j / 18f)); if (j > 120f) Shake = Math.Max(Shake, Math.Min(0.4f, j / 500f)); }   // 세게 부딪힐수록 큰 효과
	}
	private List<PointF> Preview()
	{
		List<PointF> r = new List<PointF>(); St s = new St { X = BX, Y = SY, Spin = spin }; float v0 = 330f; s.Vx = adx * v0; s.Vy = ady * v0;
		for (int i = 0; i < 90; i++) { Step(s, 0.02f, false); if (i % 6 == 0 && r.Count < 12) r.Add(new PointF(s.X, s.Y)); }
		return r;
	}
	// 동전 면 그림 (한 번만 만들어 둬요)
	private static Bitmap coinF, coinB;
	private static Bitmap CoinFace(bool front)
	{
		if (front ? coinF != null : coinB != null) return front ? coinF : coinB;
		const int S = 216; Bitmap b = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		try
		{
			using (Graphics g = Graphics.FromImage(b))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; RectangleF r = new RectangleF(2, 2, S - 4, S - 4);
				Color a = front ? Color.FromArgb(255, 232, 140) : Color.FromArgb(232, 238, 246), c = front ? Color.FromArgb(196, 132, 36) : Color.FromArgb(132, 146, 172), dk = front ? Color.FromArgb(150, 96, 20) : Color.FromArgb(96, 108, 132);
				using (LinearGradientBrush lb = new LinearGradientBrush(r, a, c, 60f)) g.FillEllipse(lb, r);
				RectangleF ri = new RectangleF(16, 16, S - 32, S - 32);
				using (LinearGradientBrush lb = new LinearGradientBrush(ri, c, a, 60f)) using (Pen p = new Pen(lb, 5f)) g.DrawEllipse(p, ri);   // 테두리 홈
				using (SolidBrush db = new SolidBrush(Color.FromArgb(120, dk))) for (int i = 0; i < 48; i++) { double an = i * Math.PI * 2 / 48; g.FillEllipse(db, S / 2f + (float)Math.Cos(an) * (S / 2f - 9) - 2, S / 2f + (float)Math.Sin(an) * (S / 2f - 9) - 2, 4, 4); }
				if (front)
				{
					// 마룡이 그림의 밝기만 남겨 금빛으로 새기고, 아래·오른쪽에 그림자를 줘서 음각처럼
					Image m = Program.MascotImage();
					if (m != null)
					{
						float k = Math.Min((S - 70f) / m.Width, (S - 70f) / m.Height), w = m.Width * k, h = m.Height * k; RectangleF mr = new RectangleF((S - w) / 2f, (S - h) / 2f + 4f, w, h);
						using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
						{
							float[][] sh = { new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0.55f, 0 }, new float[] { dk.R / 255f, dk.G / 255f, dk.B / 255f, 0, 1 } };
							ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(sh)); RectangleF m2 = mr; m2.Offset(3f, 3f);
							g.DrawImage(m, Rectangle.Round(m2), 0, 0, m.Width, m.Height, GraphicsUnit.Pixel, ia);
							// 회색으로 만든 뒤 금색을 입혀요 (R = 0.3+lum*0.7 …)
							float[][] gm = { new float[] { 0.30f * 0.75f, 0.30f * 0.6f, 0.30f * 0.25f, 0, 0 }, new float[] { 0.59f * 0.75f, 0.59f * 0.6f, 0.59f * 0.25f, 0, 0 }, new float[] { 0.11f * 0.75f, 0.11f * 0.6f, 0.11f * 0.25f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0.42f, 0.30f, 0.06f, 0, 1 } };
							ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(gm)); g.DrawImage(m, Rectangle.Round(mr), 0, 0, m.Width, m.Height, GraphicsUnit.Pixel, ia);
						}
					}
				}
				else
				{
					using (Font f = new Font(Theme.F(10f, 2).FontFamily, 92f, FontStyle.Bold, GraphicsUnit.Pixel)) using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
					{
						RectangleF tr = new RectangleF(0, 4, S, S);
						using (SolidBrush b1 = new SolidBrush(Color.FromArgb(160, 255, 255, 255))) { RectangleF t1 = tr; t1.Offset(-2.5f, -2.5f); g.DrawString("1", f, b1, t1, sf); }
						using (SolidBrush b2 = new SolidBrush(Color.FromArgb(200, dk))) { RectangleF t2 = tr; t2.Offset(2.5f, 2.5f); g.DrawString("1", f, b2, t2, sf); }
						using (SolidBrush b3 = new SolidBrush(c)) g.DrawString("1", f, b3, tr, sf);
					}
				}
				using (SolidBrush hb = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(hb, S * 0.18f, S * 0.12f, S * 0.36f, S * 0.18f);
			}
		}
		catch { }
		if (front) coinF = b; else coinB = b; return b;
	}
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_ice", Color.FromArgb(40, 50, 90), Color.FromArgb(24, 30, 60)); Snow(g, Environment.TickCount / 1000.0, 12);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(52, 54, 296, 520), 18)) using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(52, 54, 296, 520), Color.FromArgb(226, 244, 255), Color.FromArgb(190, 222, 244), 90f)) g.FillPath(lb, gp);
		Color[] rc = { Color.FromArgb(70, 120, 214), Color.White, Color.FromArgb(222, 70, 70), Color.White };
		float[] rr = { HR, 68, 46, 24 }; for (int i = 0; i < 4; i++) using (SolidBrush b = new SolidBrush(rc[i])) g.FillEllipse(b, BX - rr[i], BY - rr[i], rr[i] * 2, rr[i] * 2);
		using (Pen p = new Pen(Color.FromArgb(90, 40, 60, 100), 1.5f)) { g.DrawLine(p, 52, BY, 348, BY); g.DrawLine(p, BX, 60, BX, 570); }
		using (Pen p = new Pen(Color.FromArgb(200, 240, 190, 40), 2.5f)) g.DrawLine(p, 52, Hog, 348, Hog);
		Txt(g, "힘 부족선", 8.5f, 1, Color.FromArgb(200, 160, 120, 20), 56, Hog + 2, 70, 14, StringAlignment.Near);
		if (state == 0)
		{
			float ax = locked ? adx : mxx - BX, ay = locked ? ady : Math.Min(myy, 470f) - SY; float L = locked ? 1f : (float)Math.Sqrt(ax * ax + ay * ay); if (L < 1f) { ax = 0; ay = -1; L = 1; }
			if (locked) { List<PointF> pv = Preview(); if (pv.Count > 1) using (Pen p = new Pen(Color.FromArgb(235, 255, 90, 90), 2.5f) { DashStyle = DashStyle.Dot }) g.DrawLines(p, pv.ToArray()); }
			else using (Pen p = new Pen(Color.FromArgb(150, 255, 90, 90), 2.5f) { DashStyle = DashStyle.Dot }) g.DrawLine(p, BX, SY, BX + ax / L * 200f, SY + ay / L * 200f);
		}
		foreach (St s in stones)
		{
			if (s.X == 0f && s.Y == 0f) continue; Color body = s.Owner == 0 ? Color.FromArgb(226, 70, 70) : Color.FromArgb(240, 200, 50);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 40))) g.FillEllipse(b, s.X - R + 2, s.Y - R + 6, R * 2, R * 2);
			if (Spr.Draw(g, s.Owner == 0 ? "ms_m_cl_red" : "ms_m_cl_blue", s.X, s.Y - R * 0.2f, R * 2.5f, R * 2.5f * (s.Owner == 0 ? 200f / 218f : 200f / 216f), 0f)) continue;
			using (SolidBrush b = new SolidBrush(Color.FromArgb(128, 134, 150))) g.FillEllipse(b, s.X - R, s.Y - R, R * 2, R * 2);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 176, 190))) g.FillEllipse(b, s.X - R + 3, s.Y - R + 3, R * 2 - 6, R * 2 - 8);
			using (SolidBrush b = new SolidBrush(body)) g.FillEllipse(b, s.X - 7, s.Y - 8, 14, 14);
		}
		if (state == 2 && cur != null && cur.Moving)
		{
			float vl = (float)Math.Sqrt(cur.Vx * cur.Vx + cur.Vy * cur.Vy) + 0.001f, ux = cur.Vx / vl, uy = cur.Vy / vl;
			PointF c0 = new PointF(cur.X + ux * 62f, cur.Y + uy * 62f); float ang = (float)(Math.Atan2(uy, ux) * 180 / Math.PI);
			GraphicsState gs = g.Save(); g.TranslateTransform(c0.X, c0.Y); g.RotateTransform(ang);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(sweeping ? 80 : 38, sweeping ? 255 : 120, sweeping ? 230 : 200, sweeping ? 120 : 255))) g.FillEllipse(b, -88, -60, 176, 120);
			g.Restore(gs);
			if (sweeping) { for (int k = 0; k < 3; k++) { float ph = (float)(t * 14 + k * 2); float bx = sweepX + (float)Math.Sin(ph) * 12f, by = sweepY + k * 5f - 5f; using (Pen p = new Pen(Color.FromArgb(200, 255, 240, 160), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, bx - 10, by, bx + 10, by - 3); } }
			if (sweeping && Spr.Get("ms_cu_broom") != null) { float bph = (float)(t * 14); Spr.Draw(g, "ms_cu_broom", sweepX + (float)Math.Sin(bph) * 12f + 8f, sweepY - 12f, 30f, 43f, 28f + (float)Math.Sin(bph) * 10f); }
		}
		if (state == 0 && locked)
		{
			using (GraphicsPath gp = Gfx.Round(new RectangleF(322, 340, 16, 160), 8)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 10, 20, 50))) g.FillPath(b, gp);
			Color pc = power > 0.85f ? Color.FromArgb(255, 110, 90) : (power > 0.3f ? Color.FromArgb(120, 230, 160) : Color.FromArgb(255, 214, 90));
			using (GraphicsPath gp = Gfx.Round(new RectangleF(322, 500 - 160 * Math.Max(0.06f, power), 16, 160 * Math.Max(0.06f, power)), 8)) using (SolidBrush b = new SolidBrush(pc)) g.FillPath(b, gp);
			Txt(g, "힘", 10f, 2, Color.White, 312, 504, 36, 18, StringAlignment.Center);
			float sx0 = 130f, sw = 140f, sy0 = 556f; using (GraphicsPath gp = Gfx.Round(new RectangleF(sx0 - 6, sy0 - 8, sw + 12, 22), 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 10, 10, 30))) g.FillPath(b, gp);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 214, 90))) g.FillEllipse(b, sx0 + sw / 2 + spin * sw / 2 - 7, sy0 - 3, 14, 14);
			Txt(g, "← 왼쪽으로 휘어요   오른쪽으로 →", 8.5f, 1, Color.FromArgb(210, 40, 60, 100), sx0 - 40, sy0 + 14, sw + 80, 14, StringAlignment.Center);
		}
		string st2 = state == 7 ? "" : state == 0 ? (locked ? "좌우로 끌어 회전 → 힘이 알맞을 때 놓기" : "방향을 잡고 클릭해서 고정!") : (state == 1 ? "CPU가 생각 중…" : (state == 2 ? (cur != null && cur.Moving ? (sweeping ? (cur.Owner == 0 ? "문질문질! 멀리 가고, 옆을 문지르면 그쪽으로 살짝 밀려요" : "CPU 돌을 문질러 더 멀리 보내요!") : (cur.Owner == 0 ? "스톤 앞쪽(노란 영역)을 꾹 눌러 문질러요" : "CPU 돌도 앞쪽을 꾹 누르면 비질할 수 있어요")) : "") : ""));
		if (!(state == 0 && locked)) Txt(g, st2, 12f, 2, Color.FromArgb(220, 40, 60, 100), 0, 580, W, 20, StringAlignment.Center);
		if (state != 7) Txt(g, Math.Min(8, thr + 1) + "/8번째 · " + (cur != null && cur.Owner == 0 ? "내 차례" : "CPU 차례"), 12f, 2, Color.White, 0, 42, W, 20, StringAlignment.Center);
		if (msgT > 0) Txt(g, msg, 20f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 226, 120), 0, 290, W, 36, StringAlignment.Center);
		if (state == 7)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 20, 50))) g.FillRectangle(b, 0, 0, W, H);
			Txt(g, "동전을 던져 선공을 정해요", 15f, 2, Color.White, 0, 150, W, 28, StringAlignment.Center);
			Txt(g, "앞면이면 내가 선공 · 뒷면이면 CPU 선공", 11f, 1, Color.FromArgb(220, 200, 220, 255), 0, 182, W, 20, StringAlignment.Center);
			float u = Math.Min(1f, coinT / 1.5f), e = 1f - (1f - u) * (1f - u); double ang = e * (Math.PI * 6 + (coinHeads ? 0 : Math.PI)); float cw = (float)Math.Abs(Math.Cos(ang)); bool front = Math.Cos(ang) >= 0;
			float cy0 = 350f - (float)Math.Sin(Math.PI * u) * 80f, rad = 54f, ww = Math.Max(4f, rad * cw);
			using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(sh, 200 - rad * 0.8f, 440, rad * 1.6f, 14);
			Color c1 = front ? Color.FromArgb(255, 226, 120) : Color.FromArgb(214, 224, 240), c2 = front ? Color.FromArgb(222, 150, 40) : Color.FromArgb(140, 156, 190);
			RectangleF cr = new RectangleF(200 - ww, cy0 - rad, ww * 2, rad * 2);
			// 앞면: 마룡이를 금빛으로 새긴 동전 · 뒷면: 은빛 동전에 숫자 1
			Bitmap face = front ? CoinFace(true) : CoinFace(false);
			if (face != null) { InterpolationMode m0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(face, cr); g.InterpolationMode = m0; }
			else using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(cr.X, cr.Y, Math.Max(1f, cr.Width), cr.Height), c1, c2, 70f)) g.FillEllipse(lb, cr);
			using (Pen pe = new Pen(Color.FromArgb(200, 255, 255, 255), 2f)) g.DrawEllipse(pe, cr);
			if (cw < 0.5f) using (SolidBrush eb = new SolidBrush(Color.FromArgb((int)(160 * (1f - cw * 2f)), front ? Color.FromArgb(150, 100, 30) : Color.FromArgb(110, 120, 140)))) g.FillEllipse(eb, cr);   // 옆으로 설 때 동전 두께
			if (coinT >= 1.5f) Txt(g, coinHeads ? "앞면! 내가 먼저 던져요" : "뒷면! CPU가 먼저 던져요", 20f, 2, Color.FromArgb(255, 226, 120), 0, 490, W, 34, StringAlignment.Center);
		}
		DrawMascot(g, 28, 598, 66, (float)Math.Sin(t * 3) * 2);
	}
}

// ── 26. 리듬 도장 ───────────────────────────────────────────
internal sealed class RhythmGame : MiniGame
{
	private class Nt { public float T, Len, HoldT, HitAt = -9f, Tick; public int Lane, Pitch, Kind, Cnt; public bool Done, Held; public Nt Pair; }   // Kind 0 일반 · 1 길게 누르기 · 2 동시(짝) · 3 연타(3번)
	private struct Song { public string Title; public float U; public int[] N; }
	private static int songIdx = -1; private string title = "";
	private static readonly Song[] Songs = {
		new Song { Title = "환희의 송가 (베토벤)", U = 0.5f, N = new int[] { 64,1,64,1,65,1,67,1, 67,1,65,1,64,1,62,1, 60,1,60,1,62,1,64,1, 64,2,62,1,62,2, 64,1,64,1,65,1,67,1, 67,1,65,1,64,1,62,1, 60,1,60,1,62,1,64,1, 62,2,60,1,60,2, 62,1,62,1,64,1,60,1, 62,1,64,1,65,1,64,1,60,1, 62,1,64,1,65,1,64,1,62,1, 60,1,62,1,55,2, 64,1,64,1,65,1,67,1, 67,1,65,1,64,1,62,1, 60,1,60,1,62,1,64,1, 62,2,60,1,60,2 } },
		new Song { Title = "엘리제를 위하여 (베토벤)", U = 0.30f, N = new int[] { 76,1,75,1,76,1,75,1,76,1,71,1,74,1,72,1, 69,3,60,1,64,1,69,1, 71,3,64,1,68,1,71,1, 72,3,64,1, 76,1,75,1,76,1,75,1,76,1,71,1,74,1,72,1, 69,3,60,1,64,1,69,1, 71,3,64,1,72,1,71,1, 69,4, 71,1,72,1,74,1,76,3,67,1,77,1,76,1, 74,3,65,1,76,1,74,1, 72,3,64,1,74,1,72,1, 71,3,64,1,64,1,76,1, 64,1,76,1,64,1 } },
		new Song { Title = "터키 행진곡 (모차르트)", U = 0.30f, N = new int[] { 71,1,69,1,68,1,69,1, 72,3,0,1, 74,1,72,1,71,1,72,1, 76,3,0,1, 77,1,76,1,75,1,76,1, 83,1,81,1,80,1,81,1, 83,1,81,1,80,1,81,1, 84,3,81,3, 83,1,81,1,80,1,81,1, 72,3,0,1, 74,1,72,1,71,1,72,1, 76,3,0,1, 77,1,76,1,75,1,76,1, 83,1,81,1,80,1,81,1, 83,1,81,1,80,1,81,1, 84,3,81,3, 83,2,81,2,84,4 } },
		new Song { Title = "작은 별 (모차르트 변주곡 주제)", U = 0.45f, N = new int[] { 60,1,60,1,67,1,67,1, 69,1,69,1,67,2, 65,1,65,1,64,1,64,1, 62,1,62,1,60,2, 67,1,67,1,65,1,65,1, 64,1,64,1,62,2, 67,1,67,1,65,1,65,1, 64,1,64,1,62,2, 60,1,60,1,67,1,67,1, 69,1,69,1,67,2, 65,1,65,1,64,1,64,1, 62,1,62,1,60,2 } },
		new Song { Title = "미뉴에트 G장조 (바로크)", U = 0.36f, N = new int[] { 74,2,67,1,69,1,71,1,72,1, 74,2,67,2,67,2, 76,2,72,1,74,1,76,1,78,1, 79,2,67,2,67,2, 72,2,74,1,72,1,71,1,69,1, 71,2,72,1,71,1,69,1,67,1, 66,2,67,1,69,1,71,1,67,1, 71,3,0,1, 74,2,67,1,69,1,71,1,72,1, 74,2,67,2,67,2, 76,2,72,1,74,1,76,1,78,1, 79,2,67,2,67,2, 72,2,74,1,72,1,71,1,69,1, 71,2,72,1,71,1,69,1,67,1, 69,2,71,1,69,1,67,1,66,1, 67,4 } } };
	// 반주와 내가 누른 음을 한 소리로 섞어서 내보내요 (윈도우 SoundPlayer는 한 번에 하나만 재생돼서, 지금 울리는 소리를 전부 섞은 짧은 조각을 새로 만들어 재생해요)
	private struct Vc { public double S; public int M; public float V; }
	private static readonly List<Vc> voices = new List<Vc>(); private static double nowS; private static System.Media.SoundPlayer lastSp;
	private static void PlayTone(int midi) { voices.Add(new Vc { S = nowS, M = midi, V = 1f }); Mix(); }
	private static void Mix()
	{
		if (!Sfx.GameOn || !Program.SoundOn) return;
		try
		{
			int sr = 22050, n = (int)(sr * 0.7); float[] mix = new float[n]; bool any = false; const double Dur = 0.6;
			voices.RemoveAll(v => v.S + Dur < nowS - 0.05);
			foreach (Vc v in voices)
			{
				if (v.S > nowS + 0.7 || v.S + Dur < nowS) continue; double f = 440.0 * Math.Pow(2.0, (v.M - 69) / 12.0);
				for (int k = 0; k < n; k++) { double tt = nowS + k / (double)sr - v.S; if (tt < 0 || tt > Dur) continue; double env = Math.Min(1.0, tt / 0.004) * Math.Exp(-tt * (v.M < 50 ? 3.2 : 5.0)); double w = Math.Sin(2 * Math.PI * f * tt) + 0.42 * Math.Sin(4 * Math.PI * f * tt) + 0.18 * Math.Sin(6 * Math.PI * f * tt); mix[k] += (float)(w * env * v.V); any = true; }
			}
			if (!any) return;
			byte[] wav = new byte[44 + n * 2];
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, wav, 0, 4); Buffer.BlockCopy(BitConverter.GetBytes(36 + n * 2), 0, wav, 4, 4); Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "), 0, wav, 8, 8); Buffer.BlockCopy(BitConverter.GetBytes(16), 0, wav, 16, 4); Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, wav, 20, 2); Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, wav, 22, 2); Buffer.BlockCopy(BitConverter.GetBytes(sr), 0, wav, 24, 4); Buffer.BlockCopy(BitConverter.GetBytes(sr * 2), 0, wav, 28, 4); Buffer.BlockCopy(BitConverter.GetBytes((short)2), 0, wav, 32, 2); Buffer.BlockCopy(BitConverter.GetBytes((short)16), 0, wav, 34, 2); Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("data"), 0, wav, 36, 4); Buffer.BlockCopy(BitConverter.GetBytes(n * 2), 0, wav, 40, 4);
			for (int k = 0; k < n; k++) { double x = mix[k] * 6500.0; x = x > 24000 ? 24000 : (x < -24000 ? -24000 : x); short sv = (short)x; wav[44 + k * 2] = (byte)(sv & 0xff); wav[45 + k * 2] = (byte)((sv >> 8) & 0xff); }
			System.Media.SoundPlayer sp = new System.Media.SoundPlayer(new System.IO.MemoryStream(wav)); sp.Load(); sp.Play(); lastSp = sp;
		}
		catch { }
	}
	private readonly List<float> accT = new List<float>(); private int accNext;
	private readonly List<Nt> notes = new List<Nt>();
	private float songT, endT, jT, shk; private int combo, maxCombo, jRes; private string[] lab = { "완벽!", "좋아요", "놓쳤어요", "동시 완벽!", "끝까지!", "손을 뗐어요", "폭탄!", "황금 노트!" };
	private float ft = 1.4f, baseFt = 1.4f; private static int plays; private const float HitY = 470f, TopY = 96f; private static readonly float[] LX = { 110f, 200f, 290f };
	private readonly float[] flash = new float[3], ring = new float[3]; private readonly string[] lastJ = new string[3];
	public override string Costume { get { return "g_rhythm"; } }
	public override string Name { get { return "리듬 도장"; } }
	public override string Key { get { return "rhythm"; } }
	public override string Desc { get { return "반주에 맞춰 도장을 찍어요 (누른 음이 선율)"; } }
	public override string Help { get { return "위에서 떨어지는 도장이 아래 동그라미에 닿을 때 누르세요 (맞히면 그 음이 연주돼요 · 클래식 5곡이 돌아가며 나와요)\n← ↓ → 키(또는 A S D)나 해당 줄을 클릭 · 정확할수록 고득점\n노란 긴 막대는 끝까지 꾹 누르고 · 노란 테두리 ×3은 세 번 연타 · 이어진 두 도장은 동시에(거의 같이) 누르세요\n연속으로 맞히면 보너스! · 약 40초\n검은 폭탄(×)은 누르지 마세요 (누르면 감점) · 금빛 노트는 +8점 · 할수록 점점 빨라져요\n놓치면 위쪽 에너지가 줄어요 · 다 떨어지면 탈락 (맞히면 조금씩 다시 차요)"; } }
	public override string RightText { get { return combo >= 2 ? combo + "연속" : ""; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 70, 70))) { g.FillEllipse(b, cx - 18, cy - 14, 18, 18); g.FillEllipse(b, cx, cy - 2, 18, 18); }
	}
	public override void Reset()
	{
		plays++; baseFt = 1.4f - Math.Min(0.4f, (plays - 1) * 0.05f); ft = baseFt; notes.Clear(); voices.Clear(); accT.Clear(); accNext = 0; nowS = 0; Score = 0; Over = false; combo = 0; maxCombo = 0; songT = 0f; jT = 0f; hp = 1f; hpShow = 1f; failed = false;
		songIdx = (songIdx + 1 + (songIdx < 0 ? Rnd.Next(Songs.Length) : 0)) % Songs.Length; Song sg = Songs[songIdx]; title = sg.Title;
		int mn = 999, mx = 0; for (int i = 0; i < sg.N.Length; i += 2) if (sg.N[i] > 0) { mn = Math.Min(mn, sg.N[i]); mx = Math.Max(mx, sg.N[i]); }
		float t0 = 2.2f; int cnt = 0, lastDbl = -9, uAcc = 0, rootPc = 0;
		for (int i = 0; i < sg.N.Length && t0 < 44f; i += 2)
		{
			int p = sg.N[i], un = sg.N[i + 1]; float dur = un * sg.U;
			if (p > 0) rootPc = p;
			for (int uu = 0; uu < un; uu++) { int ua2 = uAcc + uu; if (ua2 % 4 == 0) { int bm = 36 + rootPc % 12; voices.Add(new Vc { S = t0 + uu * sg.U, M = bm, V = 0.55f }); accT.Add(t0 + uu * sg.U); } else if (ua2 % 4 == 2) { int bm = 43 + rootPc % 12; voices.Add(new Vc { S = t0 + uu * sg.U, M = bm, V = 0.3f }); accT.Add(t0 + uu * sg.U); } }
			uAcc += un;
			if (p > 0)
			{
				int lane = Math.Min(2, (p - mn) * 3 / (mx - mn + 1)); Nt nt = new Nt { T = t0, Lane = lane, Pitch = p }; double r = Rnd.NextDouble();
				if (cnt >= 5)
				{
					if (un >= 2 && dur >= 0.5f && r < 0.62) { nt.Kind = 1; nt.Len = Math.Max(0.4f, dur * 0.88f); }
					else if (un >= 2 && r < 0.78) nt.Kind = 3;
					else if (un >= 2 && r < 0.9 && dur >= 0.45f) { nt.Kind = 0; Nt n2 = new Nt { T = t0 + dur * 0.5f, Lane = (lane + 1 + Rnd.Next(2)) % 3, Pitch = p + (Rnd.Next(2) == 0 ? 2 : -2) }; notes.Add(n2); cnt++; }
					else if (un == 1 && r < 0.3 + 0.2 * Math.Min(1f, t0 / 36f) && cnt - lastDbl >= 3)
					{
						Nt b2 = new Nt { T = t0, Lane = (lane + 1 + Rnd.Next(2)) % 3, Pitch = p - 12, Kind = 2 }; nt.Kind = 2; nt.Pair = b2; b2.Pair = nt; notes.Add(b2); lastDbl = cnt;
					}
				}
				if (nt.Kind == 0 && cnt >= 6 && Rnd.NextDouble() < 0.07) nt.Kind = 5;   // 황금 노트: 맞히면 큰 점수
				if (cnt >= 8 && un >= 2 && Rnd.NextDouble() < 0.16 + 0.12 * Math.Min(1f, t0 / 36f)) notes.Add(new Nt { T = t0 + dur * 0.5f, Lane = (nt.Lane + 1 + Rnd.Next(2)) % 3, Pitch = 0, Kind = 4 });   // 폭탄 노트: 누르면 안 돼요
				notes.Add(nt); cnt++;
			}
			t0 += dur;
		}
		notes.RemoveAll(bn => { if (bn.Kind != 4) return false; foreach (Nt o in notes) { if (o == bn || o.Kind == 4 || o.Lane != bn.Lane) continue; if (Math.Abs(o.T - bn.T) < 0.4f || (o.Kind == 1 && bn.T > o.T - 0.2f && bn.T < o.T + o.Len + 0.2f)) return true; } return false; });
		endT = t0 + 1.2f;
	}
	private float hp = 1f, hpShow = 1f, hpWarn; private bool failed;
	// 에너지: 놓치면 줄고 맞히면 조금 차요. 다 떨어지면 탈락
	private void Judge(int res)
	{
		jRes = res; jT = 0.6f; if (failed) return;
		float d = res == 0 || res == 3 || res == 7 ? 0.025f : (res == 1 ? 0.012f : (res == 4 ? 0.03f : (res == 2 ? -0.13f : (res == 5 ? -0.07f : (res == 6 ? -0.16f : 0f)))));
		hp = Math.Max(0f, Math.Min(1f, hp + d)); if (d < 0f) hpWarn = 0.4f;
		if (hp <= 0f) { failed = true; endT = Math.Min(endT, songT + 1.4f); Sfx.Play("bad2"); Shake = 0.4f; }
	}
	private static Keys[][] LK = { new Keys[] { Keys.Left, Keys.A }, new Keys[] { Keys.Down, Keys.S, Keys.Up }, new Keys[] { Keys.Right, Keys.D } };
	private bool LaneHeld(int lane, GameInput inp)
	{
		if (inp.Down != null) foreach (Keys k in LK[lane]) if (inp.Down.Contains(k)) return true;
		if (inp.Hold) { int ml = inp.Mx < 155f ? 0 : (inp.Mx < 245f ? 1 : 2); if (ml == lane) return true; }
		return false;
	}
	private void Hit(int lane)
	{
		flash[lane] = 1f; ring[lane] = 1f; Nt best = null; float bd = 9f;
		foreach (Nt n in notes) { if (n.Lane != lane) continue; if (n.Held) return; if (n.Done || n.Kind == 4) continue; float d = Math.Abs(n.T - songT); if (n.Kind == 3 && n.Cnt > 0 && songT < n.T + 0.5f) d = 0f; if (d < bd) { bd = d; best = n; } }
		if (best == null || bd > 0.22f) { foreach (Nt n in notes) if (n.Kind == 4 && !n.Done && n.Lane == lane && Math.Abs(n.T - songT) < 0.16f) { n.Done = true; combo = 0; Score = Math.Max(0, Score - 3); Judge(6); Shake = Math.Max(Shake, 0.15f); Sfx.Play("bad2"); Fx.Burst(LX[lane], HitY, Color.FromArgb(255, 120, 70), 14); return; } Sfx.Play("tick"); return; }
		best.HitAt = songT;
		if (best.Kind == 3)
		{
			best.Cnt++; PlayTone(best.Pitch + (best.Cnt - 1) * 2 - 2); Fx.Burst(LX[lane], HitY, Color.FromArgb(255, 200, 120), 5); Score += 1;
			if (best.Cnt < 3) return;
			best.Done = true; combo++; maxCombo = Math.Max(maxCombo, combo); Score += 3; Judge(0); Fx.Burst(LX[lane], HitY, Color.FromArgb(255, 220, 90), 12); return;
		}
		combo++; maxCombo = Math.Max(maxCombo, combo); bool perfect = bd < 0.09f;
		if (best.Kind == 1) { best.Held = true; best.HoldT = 0f; best.Tick = 0f; Score += perfect ? 2 : 1; Judge(perfect ? 0 : 1); PlayTone(best.Pitch); Fx.Burst(LX[lane], HitY, Color.FromArgb(255, 220, 90), 8); return; }
		best.Done = true; if (best.Kind == 5) { Score += 8; Judge(7); Fx.Burst(LX[lane], HitY, Color.FromArgb(255, 226, 90), 22); Sfx.Play("ok"); }
		if (perfect) { Score += 3 + Math.Min(combo / 8, 3); Judge(0); Shake = Math.Max(Shake, 0.06f); PlayTone(best.Pitch); Fx.Burst(LX[lane], HitY, Color.FromArgb(255, 220, 90), 8); }
		else { Score += 1; Judge(1); PlayTone(best.Pitch); Fx.Burst(LX[lane], HitY, Color.FromArgb(160, 230, 255), 4); }
		if (best.Pair != null && best.Pair.HitAt > 0f && songT - best.Pair.HitAt < 0.28f) { Score += 3; Judge(3); Fx.Burst(W / 2f, HitY - 20f, Color.FromArgb(255, 236, 130), 14); Shake = Math.Max(Shake, 0.08f); }
	}
	public override void Click(float x, float y) { Hit(x < 155f ? 0 : (x < 245f ? 1 : 2)); }
	public override void Press(Keys k) { if (k == Keys.Left || k == Keys.A) Hit(0); else if (k == Keys.Down || k == Keys.S || k == Keys.Up) Hit(1); else if (k == Keys.Right || k == Keys.D) Hit(2); }
	public override void Update(float dt, GameInput inp, double t)
	{
		songT += dt; nowS = songT; if (accNext < accT.Count && songT >= accT[accNext]) { while (accNext < accT.Count && songT >= accT[accNext]) accNext++; Mix(); } if (jT > 0) jT -= dt; for (int i = 0; i < 3; i++) { if (flash[i] > 0) flash[i] = Math.Max(0, flash[i] - dt * 5f); if (ring[i] > 0) ring[i] = Math.Max(0, ring[i] - dt * 3.2f); }
		ft = baseFt * (1f - 0.22f * Math.Min(1f, songT / Math.Max(10f, endT)));
		foreach (Nt n in notes)
		{
			if (n.Done) continue;
			if (n.Kind == 4) { if (songT > n.T + 0.2f) { n.Done = true; Score += 1; Fx.Burst(LX[n.Lane], HitY, Color.FromArgb(150, 220, 150), 4); } continue; }
			if (n.Held)
			{
				if (LaneHeld(n.Lane, inp)) n.HoldT = 0f; else n.HoldT += dt;
				n.Tick += dt; while (n.Tick >= 0.25f) { n.Tick -= 0.25f; Score += 1; Fx.Burst(LX[n.Lane], HitY, Color.FromArgb(255, 236, 140), 2); }
				if (songT >= n.T + n.Len) { n.Done = true; n.Held = false; Score += 3; Judge(4); PlayTone(n.Pitch); Fx.Burst(LX[n.Lane], HitY, Color.FromArgb(255, 236, 120), 14); flash[n.Lane] = 1f; }
				else if (n.HoldT > 0.14f) { n.Done = true; n.Held = false; combo = 0; Judge(5); Shake = 0.08f; }
				continue;
			}
			float lim = n.Kind == 3 ? (n.Cnt > 0 ? 0.5f : 0.22f) : 0.22f;
			if (songT > n.T + lim) { n.Done = true; combo = 0; Judge(2); Shake = 0.1f; }
		}
		if (songT > endT) Over = true;
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(40, 24, 70), Color.FromArgb(90, 40, 110)); Twinkle(g, t);
		{ // 에너지 막대
			hpShow += (hp - hpShow) * 0.2f; if (hpWarn > 0f) hpWarn -= 0.016f;
			RectangleF er = new RectangleF(22, 130, 14, 320);   // 왼쪽 세로 막대 (아래에서 위로 차요)
			using (GraphicsPath gp = Gfx.Round(er, 7)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillPath(b, gp);
			Color ec = hpShow > 0.5f ? Color.FromArgb(110, 220, 140) : (hpShow > 0.25f ? Color.FromArgb(250, 200, 70) : Color.FromArgb(240, 80, 80));
			if (hpShow > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(er.X, er.Bottom - er.Height * hpShow, er.Width, er.Height * hpShow), 7)) using (SolidBrush b = new SolidBrush(hpWarn > 0f && ((int)(t * 20) & 1) == 0 ? Color.White : ec)) g.FillPath(b, gp);
			Txt(g, "에너지", 7.5f, 2, Color.FromArgb(220, 255, 255, 255), 4, 108, 50, 18, StringAlignment.Center);
		}
		if (failed) Hint(g, "에너지가 다 떨어졌어요! 탈락", 300, 14f, true, 1f);
		for (int i = 0; i < 3; i++)
		{
			using (GraphicsPath gp = Gfx.Round(new RectangleF(LX[i] - 44, TopY - 20, 88, HitY - TopY + 60), 16)) using (SolidBrush b = new SolidBrush(Color.FromArgb(40 + (int)(80 * flash[i]), 255, 255, 255))) g.FillPath(b, gp);
		}
		foreach (Nt n in notes)   // 동시 노트를 잇는 줄
			if (!n.Done && n.Kind == 2 && n.Pair != null && n.Lane < n.Pair.Lane) { float y2 = HitY - (n.T - songT) / ft * (HitY - TopY); if (y2 > TopY - 30 && y2 < HitY + 50) using (Pen p = new Pen(Color.FromArgb(210, 255, 226, 120), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, LX[n.Lane], y2, LX[n.Pair.Lane], y2); }
		foreach (Nt n in notes)
		{
			if (n.Done) continue; float y = n.Held ? HitY : HitY - (n.T - songT) / ft * (HitY - TopY); float x = LX[n.Lane];
			if (n.Kind == 1)
			{
				float yt = HitY - (n.T + n.Len - songT) / ft * (HitY - TopY); if (yt > HitY + 50) continue; float ytc = Math.Max(yt, TopY - 20f);
				using (GraphicsPath gp = Gfx.Round(new RectangleF(x - 17, ytc, 34, Math.Max(8f, y - ytc)), 14)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(n.Held ? 235 : 170, n.Held ? 255 : 255, n.Held ? 214 : 170, n.Held ? 90 : 120))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255), 2f)) g.DrawPath(p, gp); }
			}
			if (y < TopY - 30 || y > HitY + 50) continue;
			if (n.Kind == 4) { using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, x - 24, y - 20, 48, 48); using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 36, 52))) g.FillEllipse(b, x - 26, y - 26, 52, 52); using (Pen p = new Pen(Color.FromArgb(255, 90, 70), 3f)) g.DrawEllipse(p, x - 26, y - 26, 52, 52); using (Pen p = new Pen(Color.FromArgb(230, 200, 150), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x + 8, y - 24, x + 16, y - 36); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 200, 60))) g.FillEllipse(b, x + 13, y - 41, 8 + (float)Math.Sin(t * 20) * 2f, 8); Txt(g, "×", 22f, 2, Color.FromArgb(255, 120, 100), x - 26, y - 26, 52, 52, StringAlignment.Center); continue; }
			if (Spr.Get("ms_rs" + n.Lane) != null) { using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, x - 24, y + 14, 48, 14); Spr.Draw(g, "ms_rs" + n.Lane, x, y, 62f, 62f * Spr.Get("ms_rs" + n.Lane).Height / Spr.Get("ms_rs" + n.Lane).Width, 0f); }
			else
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, x - 28, y - 24, 56, 56);
				using (SolidBrush b = new SolidBrush(Color.FromArgb(222, 64, 70))) g.FillEllipse(b, x - 28, y - 28, 56, 56);
				using (Pen p = new Pen(Color.FromArgb(255, 235, 235), 2f)) g.DrawEllipse(p, x - 22, y - 22, 44, 44);
				Txt(g, "결재", 11f, 2, Color.White, x - 28, y - 28, 56, 56, StringAlignment.Center);
			}
			if (n.Kind == 5) { using (SolidBrush b = new SolidBrush(Color.FromArgb(90 + (int)(50 * Math.Sin(t * 10)), 255, 214, 60))) g.FillEllipse(b, x - 38, y - 38, 76, 76); using (Pen p = new Pen(Color.FromArgb(255, 255, 214, 60), 4f)) g.DrawEllipse(p, x - 34, y - 34, 68, 68); Txt(g, "+8", 13f, 2, Color.FromArgb(255, 255, 236, 120), x - 22, y - 48, 44, 20, StringAlignment.Center); }
			if (n.Kind == 3) { using (Pen p = new Pen(Color.FromArgb(240, 255, 210, 70), 3.5f)) g.DrawEllipse(p, x - 34, y - 34, 68, 68); Txt(g, "×" + (3 - n.Cnt), 14f, 2, Color.FromArgb(255, 255, 236, 120), x - 22, y - 46, 44, 20, StringAlignment.Center); }
			else if (n.Kind == 2) using (Pen p = new Pen(Color.FromArgb(230, 255, 226, 120), 3f)) g.DrawEllipse(p, x - 34, y - 34, 68, 68);
			if (n.Held) { using (Pen p = new Pen(Color.FromArgb((int)(140 + 100 * Math.Sin(t * 14)), 255, 240, 140), 4f)) g.DrawEllipse(p, x - 38, y - 38, 76, 76); }
		}
		for (int i = 0; i < 3; i++)
		{
			using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255), 3f)) g.DrawEllipse(p, LX[i] - 30, HitY - 30, 60, 60);
			if (ring[i] > 0.02f) { float rr2 = 30f + (1f - ring[i]) * 52f; using (Pen p = new Pen(Color.FromArgb((int)(210 * ring[i]), 255, 236, 140), 3.5f * ring[i] + 1f)) g.DrawEllipse(p, LX[i] - rr2, HitY - rr2, rr2 * 2, rr2 * 2); }
			if (flash[i] > 0.02f) DrawFx(g, "rs_hit", LX[i], HitY, 100f * (1.3f - 0.3f * Math.Min(1f, flash[i])), Math.Min(1f, flash[i] * 1.4f));
			string key = i == 0 ? "←" : (i == 1 ? "↓" : "→"); Txt(g, key, 14f, 2, Color.FromArgb(200, 255, 255, 255), LX[i] - 30, HitY + 34, 60, 22, StringAlignment.Center);
		}
		if (combo >= 5 && Spr.Get("ms_rs3") != null) { Spr.Draw(g, "ms_rs3", W - 44f, 100f, 40f, 40f * 134f / 133f, (float)Math.Sin(t * 5) * 6f); Txt(g, combo + "연속", 11f, 2, Color.FromArgb(255, 236, 130), W - 94f, 118f, 100, 20, StringAlignment.Center); }
		if (jT > 0 && (jRes == 2 || jRes == 5)) DrawFx(g, "rs_puff", W / 2f, 262f, 70f, Math.Min(1f, jT * 2.5f));
		if (jT > 0) Txt(g, lab[jRes], 22f, 2, Color.FromArgb((int)(255 * Math.Min(1f, jT * 2.5f)), (jRes == 0 || jRes == 3 || jRes == 4 || jRes == 7) ? Color.FromArgb(255, 236, 120) : (jRes == 1 ? Color.FromArgb(170, 235, 255) : Color.FromArgb(255, 160, 170))), 0, 526, W, 40, StringAlignment.Center);
		Txt(g, "점수 " + Score, 13f, 2, Color.White, 0, 38, W, 20, StringAlignment.Center);
		Txt(g, "♪ " + title, 10.5f, 1, Color.FromArgb(220, 255, 226, 150), 0, 62, W, 20, StringAlignment.Center);
		DrawMascot(g, 350, 598, 70, (float)Math.Sin(t * 6) * 4);
		if (songT < 1.6f) Txt(g, "준비!", 24f, 2, Color.FromArgb(240, 255, 255, 255), 0, 250, W, 44, StringAlignment.Center);
	}
}
internal sealed class RunGame : MiniGame
{
	private int runMs, runFlag = -99999;
	private class En { public float D, LF, Sw; public int Lane, V, Pu; public bool Coin, Gone, Low; }
	private readonly List<En> en = new List<En>();
	private int lane = 1, lives = 3, coins, stage = 1; private float pl = 1f, spd, spawnD, el, inv, dist, msgT, gT, jumpT, magnetT, flash, bannerT, puT, lastFl; private string msg = "", banner = ""; private bool flying, shield, jumpHint;
	private float bigT;
	public override bool NoLimit { get { return true; } }
	public override string Costume { get { return "g_run"; } }
	public override string Name { get { return "마룡이 런"; } }
	public override string Key { get { return "run"; } }
	public override string Desc { get { return "길을 바꾸고 점프하며 끝없이 달려요"; } }
	public override string Help { get { return "← → (또는 화면 왼쪽·오른쪽 클릭)으로 길을 바꾸고, ↑ · 스페이스 (또는 화면 위쪽 클릭)로 점프해요\n콘·바리케이드는 피하고, 낮은 서류 더미는 점프로 넘어요 · 동전을 모아요\n파란 방패는 한 번 막아 주고, 자석은 동전을 끌어와요\n초록 버섯을 먹으면 6초 동안 커져서 장애물을 부수며 달려요 · 하트 3개"; } }
	public override string RightText { get { return "하트 " + lives + " · 단계 " + stage; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 120, 60))) g.FillPolygon(b, new PointF[] { new PointF(cx - 12, cy + 14), new PointF(cx, cy - 16), new PointF(cx + 12, cy + 14) });
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 214, 70))) g.FillEllipse(b, cx + 6, cy - 14, 12, 12);
	}
	public override void Reset()
	{
		en.Clear(); lane = 1; pl = 1f; lives = 3; coins = 0; Score = 0; Over = false; spd = 0.5f; spawnD = 0f; el = 0; inv = 0; dist = 0; msg = ""; msgT = 0;
		stage = 1; gT = 0f; jumpT = 0f; magnetT = 0f; flash = 0f; bannerT = 0f; puT = 12f; shield = false; flying = false; lastFl = 0f; jumpHint = false;
		bigT = 0f;
	}
	private void Jump() { if (jumpT <= 0f) { jumpT = 0.0001f; Sfx.Play("whoosh"); Fx.Burst(LaneX(lane, 0.9f), 556f, Color.FromArgb(220, 210, 190), 4); } }
	private void Move(int d) { int nl = Math.Max(0, Math.Min(2, lane + d)); if (nl != lane) { Fx.Burst(LaneX(lane, 0.05f), 540f, Color.FromArgb(220, 210, 190), 4); Sfx.Play("whoosh"); mvFx = 1f; mvDir = d; } lane = nl; }
	private float mvFx; private int mvDir;
	public override void Press(Keys k) { if (k == Keys.Left || k == Keys.A) Move(-1); else if (k == Keys.Right || k == Keys.D) Move(1); else if (k == Keys.Up || k == Keys.W || k == Keys.Space) Jump(); }
	public override void Swipe(float dx, float dy) { if (Math.Abs(dx) > Math.Abs(dy)) Move(dx > 0 ? 1 : -1); else if (dy < -20f) Jump(); }
	public override void Click(float x, float y) { if (y < 190f) Jump(); else if (x < W / 2f) Move(-1); else Move(1); }
	private static float Y(float d) { return 165f + 420f * d; }
	private static float Sc(float d) { return 0.28f + 0.72f * d; }
	private static float LaneX(float l, float d) { return 200f + (l - 1f) * TrK * (Y(d) - TrH); }   // 길 그림의 3개 길(레인)이 만나는 지점(TrH)에서 퍼져 나오는 원근에 그대로 맞춰요
	private const float TrK = 0.28f, TrH = 151f;   // 레인 간격 = TrK × (화면 y − TrH)
	private static Bitmap trkBg;
	// 길 그림은 가운데 길만 화면에 다 보여서, 줄 위치(원근)를 가로로 줄여 3개 길이 모두 화면 안에 들어오게 다시 만들어요 (한 번만 만들어 두고 계속 써요)
	private static Bitmap BuildTrack()
	{
		Bitmap src = Spr.Get("bg_track"); if (src == null) return null;
		const int DW = 800, DH = 1200; int sw = src.Width, sh = src.Height;
		int[] sp = new int[sw * sh]; { System.Drawing.Imaging.BitmapData bd = src.LockBits(new Rectangle(0, 0, sw, sh), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, sp, 0, sp.Length); src.UnlockBits(bd); }
		Func<float, float, int> samp = delegate(float fx, float fy)
		{
			if (fx < 0f) fx = 0f; if (fy < 0f) fy = 0f; if (fx > sw - 1.001f) fx = sw - 1.001f; if (fy > sh - 1.001f) fy = sh - 1.001f; int x0 = (int)fx, y0 = (int)fy; float ax = fx - x0, ay = fy - y0; int r = 0, g = 0, b = 0;
			for (int k = 0; k < 4; k++) { int p = sp[(y0 + (k >> 1)) * sw + x0 + (k & 1)]; float w = ((k & 1) == 0 ? 1f - ax : ax) * ((k >> 1) == 0 ? 1f - ay : ay); r += (int)(((p >> 16) & 255) * w); g += (int)(((p >> 8) & 255) * w); b += (int)((p & 255) * w); }
			return (255 << 24) | (Math.Min(255, r) << 16) | (Math.Min(255, g) << 8) | Math.Min(255, b);
		};
		int[] dp = new int[DW * DH]; float k1 = sw / 400f;
		int grass0 = samp(4f * k1, 236f * k1); int grass1 = (255 << 24) | (((grass0 >> 16 & 255) * 88 / 100) << 16) | (((grass0 >> 8 & 255) * 88 / 100) << 8) | ((grass0 & 255) * 88 / 100);
		for (int yd = 0; yd < DH; yd++)
		{
			float y4 = yd / 2f;
			for (int xd = 0; xd < DW; xd++)
			{
				float x4 = xd / 2f; int c;
				if (y4 < 163f) c = samp(x4 * k1, y4 * k1);
				else
				{
					float wd = 0.915f * (y4 - TrH), sn = TrK * (y4 - TrH); float u = (x4 - 200f) / sn, sx4 = 200f + u * wd;
					float au = Math.Abs(u), line = 0.018f;
					if (au <= 1.5f + line && sx4 >= 0f && sx4 <= 399.5f) c = samp(sx4 * k1, y4 * k1);
					else
					{
						if (au <= 1.5f - line) c = samp((200f + (((au * 7f) % 1f) - 0.5f) * 0.5f * wd) * k1, y4 * k1);
						else if (au <= 1.5f + line) c = (255 << 24) | (238 << 16) | (238 << 8) | 232;
						else c = (((int)(500f / (y4 - TrH)) & 1) == 0) ? grass0 : grass1;
					}
				}
				dp[yd * DW + xd] = c;
			}
		}
		Bitmap o = new Bitmap(DW, DH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); { System.Drawing.Imaging.BitmapData bd = o.LockBits(new Rectangle(0, 0, DW, DH), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); System.Runtime.InteropServices.Marshal.Copy(dp, 0, bd.Scan0, dp.Length); o.UnlockBits(bd); }
		return o;
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (msgT > 0) msgT -= dt; if (bannerT > 0) bannerT -= dt; if (flash > 0f) flash -= dt * 1.6f;
		el += dt; gT += dt; if (bigT > 0f) bigT -= dt; { int ns = 1 + (int)(el / 25f); if (ns > stage) { stage = ns; banner = "단계 " + stage; bannerT = 1.4f; Score += 20; Sfx.Play("ok"); } } if (mvFx > 0f) mvFx -= dt * 4f; spd = Math.Min(1.45f, 0.5f + el * 0.010f); dist += spd * dt * 30f; if (inv > 0) inv -= dt;
		if (jumpT > 0f) { jumpT += dt; if (jumpT >= 0.62f) jumpT = 0f; } if (magnetT > 0f) magnetT -= dt;
		pl += (lane - pl) * Math.Min(1f, dt * 14f); spawnD += spd * dt;
		puT -= dt; if (puT <= 0f && spawnD >= 0f) { puT = 6f + (float)Rnd.NextDouble() * 4f; int pk = Rnd.Next(3) + 1; if (pk == 1 && shield) pk = 3; if (pk == 3 && bigT > 0f) pk = 2; int pln = Rnd.Next(3); en.Add(new En { Pu = pk, D = 0f, Lane = pln, LF = pln }); }
		if (spawnD >= 0.5f)
		{
			spawnD = 0f; int freeLane = Rnd.Next(3); bool coinRow = Rnd.NextDouble() < 0.4;
			if (coinRow) for (int i2 = 0; i2 < 3; i2++) en.Add(new En { V = Rnd.Next(5), D = -i2 * 0.08f, Lane = freeLane, LF = freeLane, Coin = true });
			else
			{
				double pr = el < 12 ? 0.5 : (el < 45 ? 0.8 : 0.92); int blocked = 0;
				for (int l = 0; l < 3; l++) if (l != freeLane && Rnd.NextDouble() < pr)
				{
					int v = Rnd.Next(3); float sw = 0f; if (v == 0 && el > 25f && Rnd.NextDouble() < 0.3 + Math.Min(0.3, (el - 25f) * 0.006)) { sw = l == 0 ? 1f : (l == 2 ? -1f : (Rnd.Next(2) == 0 ? 1f : -1f)); }
					en.Add(new En { V = v, D = 0f, Lane = l, LF = l, Sw = sw, Low = v == 2 }); blocked++;
				}
				if (el > 18f && Rnd.NextDouble() < 0.25) { int l2 = Rnd.Next(3); if (l2 != freeLane) en.Add(new En { V = 2, D = -0.14f, Lane = l2, LF = l2, Low = true }); }
				if (Rnd.NextDouble() < 0.5) en.Add(new En { V = Rnd.Next(5), D = -0.1f, Lane = freeLane, LF = freeLane, Coin = true });
			}
		}
		float jh = jumpT > 0f ? (float)Math.Sin(Math.PI * jumpT / 0.62f) : 0f;
		foreach (En e in en)
		{
			e.D += spd * dt;
			if (e.Sw != 0f) { float u = Math.Max(0f, Math.Min(1f, (e.D - 0.28f) / 0.38f)); e.LF = e.Lane + e.Sw * u * u * (3f - 2f * u); }
			else if (e.Coin && magnetT > 0f && e.D > 0.15f) e.LF += (pl - e.LF) * Math.Min(1f, dt * 6f);
			if (e.Gone || e.D < 0.84f || e.D > 0.98f) continue; if (Math.Abs(e.LF - pl) > (bigT > 0f ? 0.85f : 0.55f)) continue;
			if (e.Low && jh > 0.4f && bigT <= 0f) continue;   // 낮은 서류 더미는 점프로 넘어요
			e.Gone = true;
			if (e.Pu == 1) { shield = true; Sfx.Play("ok"); msg = "방패! 한 번 막아 줘요"; msgT = 1.0f; Fx.Burst(LaneX(e.LF, e.D), Y(e.D), Color.FromArgb(120, 190, 255), 8); }
			else if (e.Pu == 2) { magnetT = 6f; Sfx.Play("ok"); msg = "자석! 동전이 끌려와요"; msgT = 1.0f; Fx.Burst(LaneX(e.LF, e.D), Y(e.D), Color.FromArgb(255, 130, 140), 8); }
			else if (e.Pu == 3) { bigT = 6f; Sfx.Play("fanfare"); msg = "커졌다! 장애물을 부숴요"; msgT = 1.2f; flash = 0.5f; Fx.Burst(LaneX(e.LF, e.D), Y(e.D), Color.FromArgb(120, 230, 140), 10); }
			else if (e.Coin) { coins++; Score += 5; Sfx.Play("coin"); Fx.Burst(LaneX(e.LF, e.D), Y(e.D), Color.FromArgb(255, 220, 80), 4); }
			else if (bigT > 0f) { Score += 10; Sfx.Play("tick"); Shake = 0.12f; Fx.Burst(LaneX(e.LF, e.D), Y(e.D), Color.FromArgb(255, 200, 120), 10); }
			else if (inv <= 0f)
			{
				if (shield) { shield = false; inv = 0.9f; Shake = 0.2f; Sfx.Play("ok2"); msg = "방패가 막아줬어요!"; msgT = 0.9f; Fx.Burst(LaneX(e.LF, e.D), Y(e.D), Color.FromArgb(150, 210, 255), 10); continue; }
				lives--; inv = 1.3f; Shake = 0.35f; Fx.Burst(LaneX(e.LF, e.D), Y(e.D), Color.FromArgb(255, 160, 120), 10); Sfx.Play("bad2"); msg = "으악! 긴급 민원!"; msgT = 0.8f; if (lives <= 0) { Over = true; Score += (int)(dist / 10f); return; }
			}
		}
		en.RemoveAll(delegate(En e) { return e.D > 1.1f || (e.Gone && e.D > 0.98f); }); Score = Math.Max(Score, 0);
		if (!jumpHint && el > 6f && en.Exists(delegate(En e) { return e.Low && e.D > 0.1f; })) { jumpHint = true; msg = "서류 더미는 점프로! (↑·스페이스·위쪽 클릭)"; msgT = 2.4f; }
	}
	public override void Draw(Graphics g, double t)
	{
		if (trkBg == null) { try { trkBg = BuildTrack(); } catch { } } if (trkBg != null) { float bsc = ImgCache.Scale(g); System.Drawing.Image bim = ImgCache.Get(trkBg, W * bsc, H * bsc); InterpolationMode m0 = g.InterpolationMode; PixelOffsetMode po0 = g.PixelOffsetMode; CompositingMode cm0 = g.CompositingMode; g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(bim, new Rectangle(0, 0, W, H), 0, 0, bim.Width, bim.Height, GraphicsUnit.Pixel); g.InterpolationMode = m0; g.PixelOffsetMode = po0; g.CompositingMode = cm0; } else BgImg(g, "bg_track", Color.FromArgb(130, 200, 250), Color.FromArgb(190, 230, 190));
		float off = (el * spd * 3f) % 1f;
		List<En> order = new List<En>(en); order.Sort(delegate(En a, En b) { return a.D.CompareTo(b.D); });
		foreach (En e in order)
		{
			if (e.D < 0f || e.Gone) continue; float x = LaneX(e.LF, e.D), y = Y(e.D), sc = Sc(e.D);
			if (e.Pu > 0) { DrawPickup(g, e.Pu, x, y - 18f * sc, 40f * sc, t); continue; }
			if (e.Coin) { DrawSpr(g, "n_co" + (1 + ((int)(t * 9) + e.V) % 5), x, y + 2 * sc, 36f * sc, 0f); }
			else
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, x - 24 * sc, y - 6 * sc, 48 * sc, 12 * sc);
				if (e.V == 0) DrawSpr(g, "n_cone", x, y + 2 * sc, 62f * sc, 0f); else if (e.V == 1) DrawSpr(g, "n_barr", x, y + 2 * sc, 58f * sc, 0f); else DrawSpr(g, "n_paper", x, y + 2 * sc, 52f * sc, 0f);
			}
		}
		float mx = LaneX(pl, 0.9f); float jh = jumpT > 0f ? (float)Math.Sin(Math.PI * jumpT / 0.62f) : 0f; float bobR = Math.Abs((float)Math.Sin(t * (13 + spd * 4)));
		{ int cyc = (int)(t * (7f + spd * 5f)) % 6; string rf = Over ? "ms_rn11" : (inv > 0.6f ? "ms_rn10" : "ms_rn" + cyc); float bobv = (inv > 0.6f || Over) ? 0f : (float)Math.Abs(Math.Sin(t * (3.5f + spd * 2.5f) * Math.PI)) * 3f;   // 길 안쪽으로 달리니까 뒷모습, 6프레임 달리기
			using (SolidBrush shb = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(shb, mx - 30f + jh * 8f, 556f, 60f - jh * 16f, 10f - jh * 3f);
			if (!Spr.Frame(g, rf, mx, 566f - bobv - jh * 85f, 0.42f * (bigT > 0f ? 1f + 0.5f * Math.Min(1f, Math.Min(bigT, 6f - bigT + 0.2f) * 5f) : 1f), (pl - lane) * -9f + (inv > 0.6f ? (float)Math.Sin(t * 30) * 3f : 0f), false)) { if (!Spr.Draw(g, "mascot_back", mx, 508f - bobv, 64f, 104f, 0f)) DrawSpr(g, "n_r" + (1 + ((int)(t * (9 + spd * 4)) % 3)), mx, 562f - bobR * 5f, 100f, (pl - lane) * -10f); } }
		if (mvFx > 0f) { if (Spr.Get("ms_ds0") != null) Spr.Draw(g, "ms_ds" + Math.Min(5, (int)((1f - Math.Min(1f, mvFx)) * 6f)), mx - mvDir * 24f, 548f, 74f, 74f * 178f / 220f, 0f); else DrawFx(g, "e_jump", mx - mvDir * 24f, 548f, 64f, mvFx); }
		if (shield) using (Pen sp = new Pen(Color.FromArgb(170 + (int)(60 * Math.Sin(t * 8)), 120, 190, 255), 3f)) g.DrawEllipse(sp, mx - 34f, 480f - jh * 85f, 68f, 92f);
		if (magnetT > 0f) using (Pen mp = new Pen(Color.FromArgb(110 + (int)(70 * Math.Abs(Math.Sin(t * 8))), 255, 120, 130), 3f)) g.DrawEllipse(mp, mx - 40f, 486f - jh * 85f, 80f, 80f);
		DrawHud(g, t);
		{ int ms = (int)(dist / 10f) / 100; if (ms < runMs) runMs = ms; if (ms > runMs) { runMs = ms; runFlag = Environment.TickCount; }
		  int fa = Environment.TickCount - runFlag; if (fa >= 0 && fa < 1800 && runMs > 0 && Spr.Get("ms_tr_flag") != null) { float fk = Math.Min(1f, fa / 200f), fo = fa > 1300 ? 1f - (fa - 1300) / 500f : 1f; Spr.Draw(g, "ms_tr_flag", W / 2f - 54f, 92f, 30f * fk, 51f * fk, (float)Math.Sin(fa / 90.0) * 6f); Txt(g, runMs * 100 + "m 돌파!", 15f, 2, Color.FromArgb((int)(255 * fo), 255, 226, 100), W / 2f - 30f, 78f, 130, 26, StringAlignment.Near); } }
		if (msgT > 0) Txt(g, msg, msg.Length > 12 ? 16f : 22f, 2, Color.FromArgb(220, 60, 60), 0, 440, W, 36, StringAlignment.Center);
		if (flash > 0f) using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)(200 * Math.Min(1f, flash)), 255, 255, 255))) g.FillRectangle(fb, 0, 0, W, H);
		if (bannerT > 0f) Hint(g, banner, 250 + 20.0, 20f, true, Math.Min(1f, bannerT * 2f));
	}
	private static void DrawPickup(Graphics g, int kind, float x, float y, float sz, double t)
	{
		float bob = (float)Math.Sin(t * 5) * 3f * (sz / 40f); y += bob;
		Color c1 = kind == 1 ? Color.FromArgb(120, 190, 255) : (kind == 3 ? Color.FromArgb(150, 240, 150) : Color.FromArgb(255, 130, 140)), c2 = kind == 1 ? Color.FromArgb(40, 100, 220) : (kind == 3 ? Color.FromArgb(30, 160, 80) : Color.FromArgb(210, 50, 70));
		RectangleF r = new RectangleF(x - sz / 2f, y - sz / 2f, sz, sz);
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(sh, x - sz * 0.42f, y + sz * 0.46f - bob, sz * 0.84f, sz * 0.2f);
		using (LinearGradientBrush lb = new LinearGradientBrush(r, c1, c2, 90f)) g.FillEllipse(lb, r);
		using (Pen p = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(1.5f, sz / 16f))) g.DrawEllipse(p, r);
		using (Pen p = new Pen(Color.White, Math.Max(2f, sz / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
		{
			if (kind == 3) { g.DrawLine(p, x, y + sz * 0.22f, x, y - sz * 0.2f); g.DrawLine(p, x - sz * 0.2f, y - sz * 0.02f, x, y - sz * 0.22f); g.DrawLine(p, x + sz * 0.2f, y - sz * 0.02f, x, y - sz * 0.22f); }
			else if (kind == 1) g.DrawLines(p, new PointF[] { new PointF(x - sz * 0.2f, y - sz * 0.2f), new PointF(x + sz * 0.2f, y - sz * 0.2f), new PointF(x + sz * 0.2f, y + sz * 0.05f), new PointF(x, y + sz * 0.26f), new PointF(x - sz * 0.2f, y + sz * 0.05f), new PointF(x - sz * 0.2f, y - sz * 0.2f) });
			else { g.DrawArc(p, x - sz * 0.22f, y - sz * 0.24f, sz * 0.44f, sz * 0.44f, 180, 180); g.DrawLine(p, x - sz * 0.22f, y - sz * 0.02f, x - sz * 0.22f, y + sz * 0.2f); g.DrawLine(p, x + sz * 0.22f, y - sz * 0.02f, x + sz * 0.22f, y + sz * 0.2f); }
		}
	}
	private void DrawHud(Graphics g, double t)
	{
		Txt(g, "거리 " + (int)(dist / 10f) + "m · 점수 " + Score, 12f, 2, Color.FromArgb(60, 70, 110), 0, 44, W, 22, StringAlignment.Center);
		float pr = Math.Min(1f, (el % 25f) / 25f);
		RectangleF br = new RectangleF(W / 2f - 80f, 70f, 160f, 8f);
		using (GraphicsPath gp = Gfx.Round(br, 4)) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 20, 30, 70))) g.FillPath(b, gp);
		if (pr > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(br.X, br.Y, Math.Max(8f, br.Width * pr), br.Height), 4)) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 200, 80))) g.FillPath(b, gp);
		Txt(g, "다음 단계", 9f, 1, Color.FromArgb(60, 70, 110), W / 2f - 134f, 65f, 50, 16, StringAlignment.Far);
		if (bigT > 0f) { using (GraphicsPath gp = Gfx.Round(new RectangleF(W / 2f - 80f, 82f, 160f * Math.Min(1f, bigT / 6f), 6f), 3)) using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 90, 220, 130))) g.FillPath(b, gp); }
		for (int k = 0; k < 3; k++) { bool on = k < lives; using (SolidBrush b = new SolidBrush(on ? Color.FromArgb(240, 80, 100) : Color.FromArgb(90, 120, 120, 150))) g.FillEllipse(b, W - 76 + k * 20, 46, 14, 14); }
	}
}

// ── 31. (런 안에서 쓰는) 마룡이 비행 (지렁이 대체: 마룡이 날개 활용) ───────────────
internal sealed class FlapGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class Bl { public float X, Y, Vx, Vy, Dmg; public int Kind; }
	private class Eb { public float X, Y, Vx, Vy; public int Kind; }
	private class Fo { public float X, Y, Vx, Hp, MaxHp, T, Fire, Y0, Ph, Hit, Ang; public int Kind, Phase; public bool Boss; }
	private class It { public float X, Y, Age; public int K; }
	private class Pt { public float X, Y, Vx, Vy, Life, Max, R; public Color C; }
	private readonly List<Bl> bls = new List<Bl>(); private readonly List<Eb> ebs = new List<Eb>(); private readonly List<Fo> fos = new List<Fo>(); private readonly List<It> its = new List<It>(); private readonly List<Pt> pts = new List<Pt>();
	private float bombX, bombY; private float px = 70f, py = 300f, inv, shootT, spawnT, el, bombT, flash, msgT, bossT, deadT, scrollX, optPh, wormT;
	private int lives, power, opts, spd, bombs, bossN, kills; private bool useMouse, bossOn, dying; private string msg = "";
	private readonly List<PointF> trail = new List<PointF>(); private Bitmap skyFlip; private System.Drawing.Image skyFlipSrc;
	public override string Costume { get { return "g_plane"; } }
	public override string Name { get { return "마룡이 날기"; } }
	public override string Key { get { return "flap"; } }
	public override string Desc { get { return "탄막을 뚫고 보스를 쓰러뜨려요"; } }
	public override string Help { get { return "마우스(또는 방향키·WASD)로 마룡이를 움직여요 · 미사일은 자동 발사!\n적의 빨강·분홍 탄환만 피하세요 (몸통 판정은 가운데 흰 점뿐)\n맞을 때 필살기가 남아 있으면 자동으로 발동해 막아 줘요\n클릭(스페이스) = 필살기: 탄환을 지우고 화면 전체에 큰 피해 (남은 개수만큼)\n아이템: P 미사일 강화 · A 애드온(보조 포대 최대 2) · S 이동 속도 · B 필살기 +1 · ★ 점수\n일정 시간마다 보스가 나타나요 · 보스를 잡으면 아이템이 우수수!"; } }
	public override string RightText { get { return "필살기 " + bombs; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 210, 230))) g.FillEllipse(b, cx - 18, cy - 8, 26, 16);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 110, 90))) { g.FillEllipse(b, cx + 12, cy - 12, 8, 8); g.FillEllipse(b, cx + 14, cy + 6, 8, 8); }
	}
	public override void Reset()
	{
		bls.Clear(); ebs.Clear(); fos.Clear(); its.Clear(); pts.Clear(); trail.Clear(); px = 70f; py = 300f; inv = 1.5f; shootT = 0f; spawnT = 1f; el = 0f; bombT = 0f; flash = 0f; msgT = 0f; bossT = 28f; deadT = 0f;
		lives = 4; power = 1; opts = 0; spd = 0; bombs = 3; bossN = 0; kills = 0; bossOn = false; dying = false; Score = 0; Over = false; msg = ""; wormT = 0f;
	}
	public override void Click(float x, float y) { Bomb(); }
	public override void Press(Keys k) { if (k == Keys.Space) Bomb(); }
	private void Bomb()
	{
		if (dying || bombs <= 0 || bombT > 0f) return;
		bombs--; bombT = 1.1f; bombX = px + 20f; bombY = py; inv = Math.Max(inv, 1.3f); flash = 1f; ebs.Clear(); Shake = 0.6f; Sfx.Play("fanfare");
		foreach (Fo f in fos) Dmg(f, f.Boss ? 40f : 30f);
		for (int i = 0; i < 24; i++) Spark(px + 20f, py, Color.FromArgb(160, 230, 255), 260f);
	}
	private void Spark(float x, float y, Color c, float sp)
	{
		float a = (float)(Rnd.NextDouble() * 6.283), v = sp * (0.3f + (float)Rnd.NextDouble() * 0.7f);
		pts.Add(new Pt { X = x, Y = y, Vx = (float)Math.Cos(a) * v, Vy = (float)Math.Sin(a) * v, Life = 0.5f, Max = 0.5f, R = 2f + (float)Rnd.NextDouble() * 2.5f, C = c });
	}
	private void Dmg(Fo f, float d)
	{
		f.Hp -= d; f.Hit = 0.08f; if (f.Hp > 0f) return;
		Color c = f.Boss ? Color.FromArgb(200, 160, 255) : Color.FromArgb(190, 230, 180); for (int i = 0; i < (f.Boss ? 40 : 10); i++) Spark(f.X, f.Y, c, f.Boss ? 320f : 180f);
		Sfx.Play(f.Boss ? "glass_big" : "glass"); kills++;
		if (f.Boss)
		{
			Score += 500 + 250 * bossN; bossOn = false; bossN++; bossT = el + 42f; ebs.Clear(); Shake = 0.9f; flash = 0.8f; msg = "보스 격파! 아이템 획득"; msgT = 2.2f;
			for (int i = 0; i < 6; i++) its.Add(new It { X = f.X + (float)(Rnd.NextDouble() - 0.5) * 80f, Y = f.Y + (float)(Rnd.NextDouble() - 0.5) * 120f, K = i == 0 ? 0 : (i == 1 ? 1 : (i == 2 ? 2 : (i == 3 ? 3 : 4))) });
		}
		else
		{
			Score += f.Kind == 0 ? 10 : (f.Kind == 1 ? 25 : (f.Kind == 2 ? 20 : 60));
			double dr = f.Kind == 3 ? 1.0 : (f.Kind == 1 ? 0.28 : (f.Kind == 2 ? 0.16 : 0.09));
			if (Rnd.NextDouble() < dr) { double q = Rnd.NextDouble(); its.Add(new It { X = f.X, Y = f.Y, K = q < 0.30 ? 0 : (q < 0.46 ? 1 : (q < 0.66 ? 2 : (q < 0.80 ? 3 : 4))) }); }
		}
	}
	private void Shoot(float x, float y, float vx, float vy, float d, int kind) { bls.Add(new Bl { X = x, Y = y, Vx = vx, Vy = vy, Dmg = d, Kind = kind }); }
	private void EShot(float x, float y, float ang, float sp, int kind) { if (ebs.Count > 140 || el < 6f && kind != 0) return; ebs.Add(new Eb { X = x, Y = y, Vx = (float)Math.Cos(ang) * sp, Vy = (float)Math.Sin(ang) * sp, Kind = kind }); }
	private float BSp { get { return Math.Min(160f, 85f + el * 0.3f + bossN * 6f); } }
	private float AimAng(float x, float y) { return (float)Math.Atan2(py - y, px - x); }
	private void Spawn()
	{
		double r = Rnd.NextDouble(); float y = 80f + (float)Rnd.NextDouble() * 440f;
		if (el < 8f || r < 0.45) { int n = el < 20f ? 3 : 5; float dy = (float)(Rnd.NextDouble() < 0.5 ? -1 : 1); for (int i = 0; i < n; i++) fos.Add(new Fo { X = 430f + i * 42f, Y = y, Y0 = y, Vx = -105f, Hp = 2f + (el > 40f ? 1f : 0f), MaxHp = 2f, Kind = 0, Ph = i * 0.5f, Ang = dy }); }
		else if (r < 0.70) fos.Add(new Fo { X = 430f, Y = y, Y0 = y, Vx = -80f, Hp = 5f + el / 25f, MaxHp = 5f + el / 25f, Kind = 1, Fire = 1.0f });
		else if (r < 0.88 && el > 14f) fos.Add(new Fo { X = 430f, Y = y < 300f ? 70f : 530f, Y0 = y, Vx = -140f, Hp = 3f, MaxHp = 3f, Kind = 2, Fire = 0.7f });
		else if (el > 24f) fos.Add(new Fo { X = 440f, Y = y, Y0 = y, Vx = -45f, Hp = 16f + el / 8f, MaxHp = 16f + el / 8f, Kind = 3, Fire = 1.2f });
		else fos.Add(new Fo { X = 430f, Y = y, Y0 = y, Vx = -80f, Hp = 5f, MaxHp = 5f, Kind = 1, Fire = 1.0f });
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (dt > 0.05f) dt = 0.05f; scrollX += dt * (60f + el); optPh += dt * 3f;
		for (int i = pts.Count - 1; i >= 0; i--) { Pt p = pts[i]; p.Life -= dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vx *= 0.96f; p.Vy *= 0.96f; if (p.Life <= 0f) pts.RemoveAt(i); }
		if (flash > 0f) flash -= dt * 1.6f; if (msgT > 0f) msgT -= dt;
		if (dying) { deadT += dt; if (deadT > 1.4f) Over = true; return; }
		el += dt; if (inv > 0f) inv -= dt; if (bombT > 0f) bombT -= dt;
		// 이동 (마우스를 쫓아가거나 방향키)
		float kx = 0f, ky = 0f;
		if (inp.Down != null) { if (inp.Down.Contains(Keys.Left) || inp.Down.Contains(Keys.A)) kx -= 1f; if (inp.Down.Contains(Keys.Right) || inp.Down.Contains(Keys.D)) kx += 1f; if (inp.Down.Contains(Keys.Up) || inp.Down.Contains(Keys.W)) ky -= 1f; if (inp.Down.Contains(Keys.Down) || inp.Down.Contains(Keys.S)) ky += 1f; }
		float ms = 240f + 55f * spd;
		if (kx != 0f || ky != 0f) { useMouse = false; float l = (float)Math.Sqrt(kx * kx + ky * ky); px += kx / l * ms * dt; py += ky / l * ms * dt; }
		else { if (inp.MouseMoved) useMouse = true; if (useMouse) { float dx = inp.Mx - 18f - px, dy = inp.My - py, d = (float)Math.Sqrt(dx * dx + dy * dy); if (d > 1f) { float st = Math.Min(d, ms * dt * Math.Min(1.6f, 0.4f + d / 40f)); px += dx / d * st; py += dy / d * st; } } }
		px = Clamp(px, 22f, 378f); py = Clamp(py, 62f, 570f);
		trail.Insert(0, new PointF(px, py)); if (trail.Count > 40) trail.RemoveAt(40);
		// 내 미사일
		shootT -= dt;
		if (shootT <= 0f)
		{
			shootT = 0.17f; float x = px + 26f; Sfx.Play("tick");
			switch (power)
			{
				case 1: Shoot(x, py, 560f, 0f, 1f, 0); break;
				case 2: Shoot(x, py - 8f, 560f, 0f, 1f, 0); Shoot(x, py + 8f, 560f, 0f, 1f, 0); break;
				case 3: Shoot(x, py, 580f, 0f, 1.2f, 0); Shoot(x, py - 8f, 540f, -90f, 1f, 0); Shoot(x, py + 8f, 540f, 90f, 1f, 0); break;
				case 4: Shoot(x, py - 7f, 580f, 0f, 1.2f, 0); Shoot(x, py + 7f, 580f, 0f, 1.2f, 0); Shoot(x, py - 10f, 540f, -140f, 1f, 0); Shoot(x, py + 10f, 540f, 140f, 1f, 0); break;
				default: Shoot(x, py, 600f, 0f, 1.5f, 0); Shoot(x, py - 7f, 580f, -80f, 1.2f, 0); Shoot(x, py + 7f, 580f, 80f, 1.2f, 0); Shoot(x, py - 10f, 540f, -190f, 1f, 0); Shoot(x, py + 10f, 540f, 190f, 1f, 0); break;
			}
			for (int i = 0; i < opts; i++) { PointF o = OptPos(i); Shoot(o.X + 8f, o.Y, 520f, 0f, 0.8f, 1); }
		}
		// 적 등장
		spawnT -= dt; if (spawnT <= 0f) { spawnT = Math.Max(0.8f, 1.9f - el * 0.009f) * (bossOn ? 2.4f : 1f) * (0.8f + (float)Rnd.NextDouble() * 0.4f); if (!bossOn || fos.Count < 5) Spawn(); }
		if (!bossOn && el >= bossT) { bossOn = true; msg = "WARNING! 보스 접근"; msgT = 2.4f; Sfx.Play("bad2"); float hp = 70f + 45f * bossN; fos.Add(new Fo { X = 480f, Y = 300f, Y0 = 300f, Vx = -90f, Hp = hp, MaxHp = hp, Kind = 9, Boss = true, Fire = 1.5f }); }
		// 적
		for (int i = fos.Count - 1; i >= 0; i--)
		{
			Fo f = fos[i]; f.T += dt; if (f.Hit > 0f) f.Hit -= dt;
			if (f.Boss) { BossAi(f, dt); }
			else if (f.Kind == 0) { f.X += f.Vx * dt; f.Y = f.Y0 + (float)Math.Sin(f.T * 3f + f.Ph) * 55f * f.Ang; }
			else if (f.Kind == 1) { if (f.X > 300f) f.X += f.Vx * dt * 2f; else f.X += f.Vx * dt * 0.15f; f.Y += (float)Math.Sin(f.T * 1.6f) * 30f * dt; f.Fire -= dt; if (f.Fire <= 0f && f.X < 400f) { f.Fire = Math.Max(1.3f, 2.2f - el * 0.008f); EShot(f.X - 14f, f.Y, AimAng(f.X, f.Y), BSp, 0); } }
			else if (f.Kind == 2) { f.X += f.Vx * dt; f.Y += (f.Y0 > f.Y ? 1f : -1f) * 70f * dt * (f.Y < 90f || f.Y > 510f ? 1.6f : 0.6f); f.Fire -= dt; if (f.Fire <= 0f && f.X < 380f && f.X > 80f) { f.Fire = 2.0f; float a = AimAng(f.X, f.Y); for (int k = -1; k <= 1; k++) EShot(f.X - 10f, f.Y, a + k * 0.32f, BSp * 0.9f, 2); } }
			else { f.X += f.Vx * dt * (f.X > 330f ? 1f : 0.2f); f.Fire -= dt; if (f.Fire <= 0f && f.X < 410f) { f.Fire = 2.8f; for (int k = 0; k < 8; k++) EShot(f.X, f.Y, k * 0.7854f + f.T, BSp * 0.58f, 1); } }
			if (f.X < -50f || f.Y < -50f || f.Y > 650f) { fos.RemoveAt(i); continue; }
			// 몸통 충돌
			float rr = f.Boss ? 34f : 13f; float ddx = f.X - px, ddy = f.Y - py; if (inv <= 0f && ddx * ddx + ddy * ddy < (rr + 4f) * (rr + 4f)) HitPlayer();
		}
		// 내 미사일 이동·피격
		for (int i = bls.Count - 1; i >= 0; i--)
		{
			Bl b = bls[i]; b.X += b.Vx * dt; b.Y += b.Vy * dt; if (b.X > 430f || b.Y < -10f || b.Y > 610f) { bls.RemoveAt(i); continue; }
			bool gone = false; foreach (Fo f in fos) { float rr = f.Boss ? 42f : 17f; float dx = f.X - b.X, dy = f.Y - b.Y; if (dx * dx + dy * dy < rr * rr) { Dmg(f, b.Dmg); Spark(b.X, b.Y, Color.FromArgb(180, 240, 255), 90f); gone = true; break; } }
			if (gone) bls.RemoveAt(i);
		}
		fos.RemoveAll(f => f.Hp <= 0f);
		// 적 탄환
		for (int i = ebs.Count - 1; i >= 0; i--)
		{
			Eb e = ebs[i]; e.X += e.Vx * dt; e.Y += e.Vy * dt; if (e.X < -20f || e.X > 420f || e.Y < -20f || e.Y > 620f) { ebs.RemoveAt(i); continue; }
			float rr = e.Kind == 1 ? 7f : 4.5f; float dx = e.X - px, dy = e.Y - py; if (inv <= 0f && dx * dx + dy * dy < rr * rr) { HitPlayer(); break; }
		}
		// 아이템
		for (int i = its.Count - 1; i >= 0; i--)
		{
			It it = its[i]; it.Age += dt; it.X -= 36f * dt; float dx = px - it.X, dy = py - it.Y, d2 = dx * dx + dy * dy; if (d2 < 90f * 90f) { float d = (float)Math.Sqrt(d2) + 0.01f; it.X += dx / d * 190f * dt; it.Y += dy / d * 190f * dt; }
			if (it.X < -20f) { its.RemoveAt(i); continue; }
			if (d2 < 24f * 24f) { Pick(it.K); its.RemoveAt(i); }
		}
	}
	private PointF OptPos(int i) { float a = optPh * 0.9f + i * 3.1416f; return new PointF(px - 4f + (float)Math.Cos(a) * 30f, py + (float)Math.Sin(a) * 40f); }   // 마룡이 주위를 타원으로 빙글빙글
	private void Pick(int k)
	{
		Sfx.Play("ok2");
		if (k == 0) { if (power < 5) { power++; msg = "미사일 강화! Lv." + power; } else { Score += 100; msg = "미사일 최대! +100"; } }
		else if (k == 1) { if (opts < 2) { opts++; msg = "애드온 장착! (" + opts + "/2)"; } else { Score += 100; msg = "애드온 최대! +100"; } }
		else if (k == 2) { if (spd < 4) { spd++; msg = "이동 속도 UP!"; } else { Score += 100; msg = "속도 최대! +100"; } }
		else if (k == 3) { if (bombs < 5) { bombs++; msg = "필살기 +1"; } else { Score += 100; msg = "필살기 가득! +100"; } }
		else { Score += 120; msg = "별 +120"; }
		msgT = 1.2f;
	}
	private void HitPlayer()
	{
		if (bombs > 0 && bombT <= 0f) { msg = "자동 필살기로 막았어요!"; msgT = 1.4f; Bomb(); inv = Math.Max(inv, 1.6f); return; }
		lives--; Sfx.Play("bad2"); Shake = 0.6f; flash = 0.5f; ebs.Clear(); for (int i = 0; i < 18; i++) Spark(px, py, Color.FromArgb(255, 170, 120), 220f);
		if (lives <= 0) { dying = true; deadT = 0f; Sfx.Play("fall"); return; }
		inv = 2.6f; if (power > 2) power--; if (opts > 0 && Rnd.NextDouble() < 0.3) opts--;
	}
	private void BossAi(Fo f, float dt)
	{
		if (f.X > 300f) { f.X += f.Vx * dt; return; }
		f.Y = 300f + (float)Math.Sin(f.T * 0.9f) * 190f; float frac = f.Hp / f.MaxHp; f.Phase = frac > 0.66f ? 0 : (frac > 0.33f ? 1 : 2);
		f.Fire -= dt; float sp = BSp;
		if (f.Phase == 0 && f.Fire <= 0f) { f.Fire = 1.6f; float a = AimAng(f.X, f.Y); for (int k = -2; k <= 2; k++) EShot(f.X - 30f, f.Y, a + k * 0.2f, sp, 0); }
		else if (f.Phase == 1 && f.Fire <= 0f) { f.Fire = 0.21f; f.Ang += 0.5f; EShot(f.X, f.Y, f.Ang, sp * 0.8f, 2); EShot(f.X, f.Y, f.Ang + 3.1416f, sp * 0.8f, 2); wormT -= 0.21f; if (wormT <= -2.4f) { wormT = 0f; f.Fire = 1.2f; for (int k = 0; k < 10; k++) EShot(f.X, f.Y, 3.1416f + (k - 4.5f) * 0.22f, sp * 0.7f, 1); } }
		else if (f.Phase == 2 && f.Fire <= 0f) { f.Fire = 0.5f; f.Ang += 0.37f; float a = AimAng(f.X, f.Y); EShot(f.X - 30f, f.Y, a, sp * 1.1f, 0); EShot(f.X - 30f, f.Y, a + 0.3f, sp, 0); EShot(f.X - 30f, f.Y, a - 0.3f, sp, 0); EShot(f.X, f.Y, f.Ang, sp * 0.7f, 2); EShot(f.X, f.Y, f.Ang + 2.094f, sp * 0.7f, 2); EShot(f.X, f.Y, f.Ang + 4.188f, sp * 0.7f, 2); }
	}
	// 애드온 친구: 작은 날개 달린 구름 방울 (그림이 있으면 그림)
	private static void Buddy(Graphics g, float x, float y, float k, double t)
	{
		float bob = (float)Math.Sin(t * 6) * 1.5f;
		if (Spr.Get("ms_fl_opt0") != null) { string n = ((int)(t * 6) & 1) == 0 ? "ms_fl_opt0" : (Spr.Get("ms_fl_opt1") != null ? "ms_fl_opt1" : "ms_fl_opt0"); Bitmap ob = Spr.Get(n); float w = 30f * k; Spr.Draw(g, n, x, y + bob, w, w * ob.Height / ob.Width, 0f); return; }
		float r = 9f * k, wf = (float)Math.Sin(t * 18) * 3f;
		using (SolidBrush wb = new SolidBrush(Color.FromArgb(230, 235, 245, 255))) { g.FillEllipse(wb, x - r * 1.9f, y - r * 0.9f + bob - wf, r * 1.2f, r * 0.9f); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 225, 190))) g.FillEllipse(b, x - r, y - r + bob, r * 2, r * 2);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, x - r * 0.6f, y - r * 0.75f + bob, r * 0.7f, r * 0.5f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 60))) { g.FillEllipse(b, x + r * 0.05f, y - r * 0.2f + bob, r * 0.3f, r * 0.36f); g.FillEllipse(b, x + r * 0.5f, y - r * 0.2f + bob, r * 0.3f, r * 0.36f); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(160, 255, 150, 170))) g.FillEllipse(b, x - r * 0.15f, y + r * 0.2f + bob, r * 0.4f, r * 0.25f);
	}
	private static void Star5(Graphics g, float cx, float cy, float r, float rot, Color c, Color core)
	{
		PointF[] st = new PointF[10]; for (int j = 0; j < 10; j++) { double a2 = rot - Math.PI / 2 + j * Math.PI / 5; float rr = j % 2 == 0 ? r : r * 0.45f; st[j] = new PointF(cx + (float)Math.Cos(a2) * rr, cy + (float)Math.Sin(a2) * rr); }
		using (SolidBrush gb = new SolidBrush(Color.FromArgb(70, c))) g.FillEllipse(gb, cx - r * 1.5f, cy - r * 1.5f, r * 3f, r * 3f);
		using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, st);
		using (SolidBrush b = new SolidBrush(core)) g.FillEllipse(b, cx - r * 0.3f, cy - r * 0.3f, r * 0.6f, r * 0.6f);
	}
	// 필살기 폭탄 (그림이 있으면 그림)
	private static void BombIcon(Graphics g, float cx, float cy, float s, float rot, double t)
	{
		if (Spr.Get("ms_fl_bomb") != null) { Spr.Draw(g, "ms_fl_bomb", cx, cy, s, s, rot); return; }
		GraphicsState st = g.Save(); g.TranslateTransform(cx, cy); g.RotateTransform(rot); float r = s * 0.42f;
		using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 44, 70))) g.FillEllipse(b, -r, -r * 0.85f, r * 2, r * 2);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 255, 255, 255))) g.FillEllipse(b, -r * 0.6f, -r * 0.5f, r * 0.6f, r * 0.45f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 96, 120))) g.FillRectangle(b, r * 0.2f, -r * 1.15f, r * 0.55f, r * 0.45f);
		using (Pen p = new Pen(Color.FromArgb(200, 150, 100), Math.Max(1.2f, s * 0.06f))) g.DrawBezier(p, r * 0.5f, -r * 1.15f, r * 0.7f, -r * 1.6f, r * 1.1f, -r * 1.4f, r * 1.2f, -r * 1.75f);
		float sp = 0.8f + 0.3f * (float)Math.Sin(t * 30); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 80))) g.FillEllipse(b, r * 1.2f - s * 0.1f * sp, -r * 1.75f - s * 0.1f * sp, s * 0.2f * sp, s * 0.2f * sp);
		g.Restore(st);
	}
	private static void Heart(Graphics g, float x, float y, float s, Color c)
	{
		if (Spr.Get("ms_fl_heart") != null) { Spr.Draw(g, "ms_fl_heart", x, y, s * 2.4f, s * 2.2f, 0f); return; }
		// 하트: 두 반원 + 아래 뾰족 (베지어로 매끈하게)
		using (GraphicsPath gp = new GraphicsPath())
		{
			gp.AddBezier(x, y + s * 0.95f, x - s * 0.2f, y + s * 0.6f, x - s * 1.15f, y + s * 0.25f, x - s * 1.1f, y - s * 0.3f);
			gp.AddBezier(x - s * 1.1f, y - s * 0.3f, x - s * 1.05f, y - s * 1.05f, x - s * 0.1f, y - s * 1.1f, x, y - s * 0.45f);
			gp.AddBezier(x, y - s * 0.45f, x + s * 0.1f, y - s * 1.1f, x + s * 1.05f, y - s * 1.05f, x + s * 1.1f, y - s * 0.3f);
			gp.AddBezier(x + s * 1.1f, y - s * 0.3f, x + s * 1.15f, y + s * 0.25f, x + s * 0.2f, y + s * 0.6f, x, y + s * 0.95f);
			gp.CloseFigure();
			SmoothingMode m0 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, gp);
			using (Pen p = new Pen(Color.FromArgb(200, 140, 20, 50), 1.4f)) g.DrawPath(p, gp);
			using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(hb, x - s * 0.75f, y - s * 0.75f, s * 0.45f, s * 0.32f);
			g.SmoothingMode = m0;
		}
	}
	private void Foe(Graphics g, Fo f, double t)
	{
		{
			// 그림 적: 서류 유령 · 날개 서류철 · 도장 드론 · 서류 더미 · 보스 결재판 괴물
			string sn = f.Boss ? "ms_fl_e4" : "ms_fl_e" + Math.Min(3, f.Kind); Bitmap sb0 = Spr.Get(sn);
			if (sb0 != null)
			{
				float sw = f.Boss ? 118f : (f.Kind == 3 ? 66f : 44f), shh = sw * sb0.Height / sb0.Width, bob = (float)Math.Sin(t * 5 + f.Ph) * 2f;
				using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 20))) g.FillEllipse(sh, f.X - sw * 0.4f, f.Y + shh * 0.38f, sw * 0.8f, shh * 0.16f);
				Spr.Draw(g, sn, f.X, f.Y + bob, sw, shh, f.Boss ? (float)Math.Sin(t * 1.4) * 4f : 0f);
				if (f.Hit > 0f) using (SolidBrush hb = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(hb, f.X - sw * 0.42f, f.Y - shh * 0.42f + bob, sw * 0.84f, shh * 0.84f);
				return;
			}
		}
		Color c = f.Kind == 0 ? Color.FromArgb(150, 110, 225) : (f.Kind == 1 ? Color.FromArgb(80, 190, 130) : (f.Kind == 2 ? Color.FromArgb(70, 170, 200) : Color.FromArgb(110, 100, 160)));
		if (f.Boss) c = Color.FromArgb(120, 70, 180);
		float w = f.Boss ? 92f : (f.Kind == 3 ? 56f : 34f), h = f.Boss ? 110f : (f.Kind == 3 ? 46f : 26f);
		Color body = f.Hit > 0f ? Color.White : c;
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 20))) g.FillEllipse(sh, f.X - w / 2f + 3f, f.Y - h / 2f + 4f, w, h);
		float wf = (float)Math.Sin(t * 14 + f.Ph) * 5f;
		using (SolidBrush wb = new SolidBrush(Theme.Mix(body, Color.White, 0.35))) { g.FillEllipse(wb, f.X - w * 0.1f, f.Y - h * 0.9f + wf, w * 0.5f, h * 0.7f); g.FillEllipse(wb, f.X - w * 0.1f, f.Y + h * 0.2f - wf, w * 0.5f, h * 0.7f); }
		using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(f.X - w / 2f, f.Y - h / 2f, w, h); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(f.X - w * 0.15f, f.Y - h * 0.2f); pb.CenterColor = Theme.Mix(body, Color.White, 0.45); pb.SurroundColors = new Color[] { Theme.Mix(body, Color.Black, 0.25) }; g.FillPath(pb, gp); } }
		using (Pen p = new Pen(Color.FromArgb(150, 20, 10, 40), 1.6f)) g.DrawEllipse(p, f.X - w / 2f, f.Y - h / 2f, w, h);
		float er = f.Boss ? 9f : (f.Kind == 3 ? 6f : 4.5f);
		using (SolidBrush eb = new SolidBrush(Color.White)) { g.FillEllipse(eb, f.X - w * 0.3f - er, f.Y - er * 0.4f - er, er * 2, er * 2); if (f.Boss) g.FillEllipse(eb, f.X - w * 0.3f - er, f.Y + h * 0.18f - er, er * 2, er * 2); }
		using (SolidBrush eb = new SolidBrush(Color.FromArgb(40, 20, 50))) { g.FillEllipse(eb, f.X - w * 0.3f - er * 0.9f, f.Y - er * 0.4f - er * 0.4f, er * 1.1f, er * 1.1f); if (f.Boss) g.FillEllipse(eb, f.X - w * 0.3f - er * 0.9f, f.Y + h * 0.18f - er * 0.4f, er * 1.1f, er * 1.1f); }
	}
	public override void Draw(Graphics g, double t)
	{
		Bitmap skyB = Spr.Get("bg_fly"); bool sky = skyB != null;
		if (!sky) Bg(g, Color.FromArgb(24, 30, 70), Color.FromArgb(86, 70, 130));
		else
		{
			// 하늘이 옆으로 천천히 흘러가요 (한 장을 번갈아 뒤집어 이어 붙여서 이음새가 안 보여요)
			float bsc = ImgCache.Scale(g); System.Drawing.Image sim = ImgCache.Get(skyB, W * bsc, H * bsc); float off = (scrollX * 0.35f) % (W * 2f);
			InterpolationMode m0 = g.InterpolationMode; PixelOffsetMode po0 = g.PixelOffsetMode; g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
			using (SolidBrush sb0 = new SolidBrush(Color.FromArgb(92, 156, 222))) g.FillRectangle(sb0, 0, 0, W, H);   // 이음새가 비치지 않게 바탕을 먼저 채워요
			for (int k = 0; k < 3; k++) { float x0 = k * W - off; if (x0 > W || x0 + W < 0) continue; if (k % 2 == 0) g.DrawImage(sim, new RectangleF(x0 - 1f, 0, W + 2f, H), new RectangleF(4, 0, sim.Width - 8, sim.Height), GraphicsUnit.Pixel); else { if (skyFlip == null || skyFlipSrc != sim) { if (skyFlip != null) skyFlip.Dispose(); skyFlip = new Bitmap(sim); skyFlip.RotateFlip(RotateFlipType.RotateNoneFlipX); skyFlipSrc = sim; } g.DrawImage(skyFlip, new RectangleF(x0 - 1f, 0, W + 2f, H), new RectangleF(4, 0, skyFlip.Width - 8, skyFlip.Height), GraphicsUnit.Pixel); } }
			g.InterpolationMode = m0; g.PixelOffsetMode = po0;
		}
		if (sky) using (SolidBrush dim = new SolidBrush(Color.FromArgb(40, 10, 20, 60))) g.FillRectangle(dim, 0, 0, W, H);   // 탄환이 잘 보이게 살짝 어둡게
		if (!sky) for (int i = 0; i < 40; i++) { float sx = ((i * 97f) % 420f) - (scrollX * (0.2f + (i % 3) * 0.2f)) % 420f; if (sx < -5f) sx += 420f; float sy = (i * 53f) % 600f; using (SolidBrush b = new SolidBrush(Color.FromArgb(70 + (i % 3) * 40, 255, 255, 255))) g.FillEllipse(b, sx, sy, 1.5f + i % 3, 1.5f + i % 3); }
		for (int i = 0; i < 5; i++) { float cx = (i * 160f - scrollX * 0.6f) % 640f; if (cx < -120f) cx += 640f; float cy = 100f + i * 105f; using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 220, 220, 255))) { g.FillEllipse(b, cx - 50, cy, 110, 28); g.FillEllipse(b, cx - 20, cy - 12, 70, 30); } }
		foreach (It it in its) { if (Spr.Get("ms_fl_i" + it.K) != null) { float bob2 = (float)Math.Sin(it.Age * 5f) * 3f; Bitmap ib = Spr.Get("ms_fl_i" + it.K); float iw = it.K == 4 ? 26f : 36f; Spr.Draw(g, "ms_fl_i" + it.K, it.X, it.Y + bob2, iw, iw * ib.Height / ib.Width, 0f); continue; } Color ic = it.K == 0 ? Color.FromArgb(70, 140, 255) : (it.K == 1 ? Color.FromArgb(70, 200, 120) : (it.K == 2 ? Color.FromArgb(40, 190, 200) : (it.K == 3 ? Color.FromArgb(150, 90, 230) : Color.FromArgb(235, 190, 60)))); float bob = (float)Math.Sin(it.Age * 5f) * 3f; using (GraphicsPath gp = Gfx.Round(new RectangleF(it.X - 12, it.Y - 12 + bob, 24, 24), 7)) { using (SolidBrush b = new SolidBrush(ic)) g.FillPath(b, gp); using (Pen p = new Pen(Color.White, 2f)) g.DrawPath(p, gp); } Txt(g, it.K == 0 ? "P" : (it.K == 1 ? "A" : (it.K == 2 ? "S" : (it.K == 3 ? "B" : "★"))), 10f, 2, Color.White, it.X - 14, it.Y - 11 + bob, 28, 22, StringAlignment.Center); }
		foreach (Fo f in fos) Foe(g, f, t);
		// 내 미사일 (하늘·흰색 길쭉한 탄)
		foreach (Bl b in bls) if (b.Kind == 1) { if (Spr.Get("ms_fl_ob") != null) { Spr.Draw(g, "ms_fl_ob", b.X - 4f, b.Y, 30f, 16f, 0f); continue; } Star5(g, b.X, b.Y, 6.5f, (float)(t * 6), Color.FromArgb(180, 255, 120), Color.FromArgb(255, 255, 250, 200)); }
		foreach (Bl b in bls) if (b.Kind != 1) { float ang = (float)(Math.Atan2(b.Vy, b.Vx) * 57.3); GraphicsState st = g.Save(); g.TranslateTransform(b.X, b.Y); g.RotateTransform(ang); float l = b.Kind == 1 ? 8f : 12f, th = b.Kind == 1 ? 2.6f : 3.6f; using (SolidBrush gb = new SolidBrush(Color.FromArgb(70, 120, 220, 255))) g.FillEllipse(gb, -l - 3, -th - 3, l * 2 + 6, th * 2 + 6); using (SolidBrush mb = new SolidBrush(Color.FromArgb(150, 230, 255))) g.FillEllipse(mb, -l, -th, l * 2, th * 2); using (SolidBrush wb = new SolidBrush(Color.White)) g.FillEllipse(wb, -l * 0.6f, -th * 0.5f, l * 1.4f, th); g.Restore(st); }
		// 적 탄환 (빨강·분홍, 어두운 테두리 + 흰 중심)
		foreach (Eb e in ebs) { float r = e.Kind == 1 ? 8f : (e.Kind == 2 ? 4.6f : 5.6f); Color c = e.Kind == 2 ? Color.FromArgb(255, 80, 190) : (e.Kind == 1 ? Color.FromArgb(255, 70, 70) : Color.FromArgb(255, 110, 50)); using (SolidBrush ob = new SolidBrush(Color.FromArgb(200, 60, 0, 30))) g.FillEllipse(ob, e.X - r - 1.8f, e.Y - r - 1.8f, (r + 1.8f) * 2, (r + 1.8f) * 2); using (SolidBrush cb = new SolidBrush(c)) g.FillEllipse(cb, e.X - r, e.Y - r, r * 2, r * 2); using (SolidBrush wb = new SolidBrush(Color.FromArgb(255, 255, 235, 235))) g.FillEllipse(wb, e.X - r * 0.45f, e.Y - r * 0.45f, r * 0.9f, r * 0.9f); }
		// 애드온
		for (int i = 0; i < opts; i++) { PointF o = OptPos(i); bool front = Math.Sin(optPh * 0.9f + i * 3.1416f) > 0; Buddy(g, o.X, o.Y, front ? 1f : 0.82f, t + i); }
		// 마룡이
		if (!dying || deadT < 0.4f)
		{
			bool blink = inv > 0f && ((int)(t * 16) & 1) == 0 && !dying; Bitmap fb = Spr.Get("ms_n_fy1"); float fsc = 0.4f, rot = Clamp((py - (trail.Count > 3 ? trail[3].Y : py)) * 1.2f, -18f, 18f);
			if (!blink) { if (fb != null) Spr.Draw(g, "ms_n_fy" + (((int)(t * 9) & 1) == 0 ? 1 : 4), px, py, Spr.OW("ms_n_fy1", fb) * fsc, Spr.OH("ms_n_fy1", fb) * fsc, rot); else DrawMascot(g, px, py + 24f, 60f, rot); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(inv > 0f ? 255 : 200, 255, 255, 255))) g.FillEllipse(b, px - 3.5f, py - 3.5f, 7f, 7f); using (Pen p = new Pen(Color.FromArgb(220, 255, 90, 90), 1.4f)) g.DrawEllipse(p, px - 4.5f, py - 4.5f, 9f, 9f);
		}
		foreach (Pt p in pts) { using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(220 * Math.Max(0f, p.Life / p.Max)), p.C))) g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2); }
		if (bombT > 0.82f) { float u = (1.1f - bombT) / 0.28f; float bx2 = bombX + (230f - bombX) * u, by2 = bombY + (300f - bombY) * u - (float)Math.Sin(u * Math.PI) * 90f; BombIcon(g, bx2, by2, 26f + u * 10f, (float)(u * 540), t); }   // 폭탄이 날아가서
		if (bombT <= 0.82f && bombT > 0.5f && Spr.Get("ms_fl_boom") != null) { float u = 1f - (bombT - 0.5f) / 0.32f; Spr.Draw(g, "ms_fl_boom", 230f, 300f, (150f + u * 170f) * 1.3f, 150f + u * 170f, 0f); }
		else if (bombT <= 0.82f && bombT > 0.5f) { float u = 1f - (bombT - 0.5f) / 0.32f; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(220 * (1f - u)), 255, 230, 140))) g.FillEllipse(b, 230 - 60 - u * 200, 300 - 60 - u * 200, 120 + u * 400, 120 + u * 400); }   // 펑!
		if (bombT > 0.5f) { float u = 1f - (bombT - 0.5f) / 0.6f; using (Pen p = new Pen(Color.FromArgb((int)(200 * (1f - u)), 170, 235, 255), 10f * (1f - u) + 2f)) g.DrawEllipse(p, px - u * 520f, py - u * 520f, u * 1040f, u * 1040f); }
		if (flash > 0f) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(150 * Math.Min(1f, flash)), 255, 255, 255))) g.FillRectangle(b, 0, 0, W, H);
		// HUD
		for (int i = 0; i < lives; i++) Heart(g, 24f + i * 26f, 54f, 9f, Color.FromArgb(255, 90, 120));
		for (int i = 0; i < bombs; i++) BombIcon(g, W - 24f - i * 24f, 84f, 18f, 0f, t);
		Txt(g, "미사일 Lv." + power + "   애드온 " + opts + "/2   속도 " + (spd + 1) + "   필살기 " + bombs, 9.5f, 2, Color.FromArgb(230, 255, 255, 255), 10, 574, 380, 20, StringAlignment.Near);
		foreach (Fo f in fos) if (f.Boss) { using (GraphicsPath gp = Gfx.Round(new RectangleF(60, 74, 280, 10), 5)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 10, 10, 30))) g.FillPath(b, gp); float fr = Math.Max(0.02f, f.Hp / f.MaxHp); using (GraphicsPath gp = Gfx.Round(new RectangleF(60, 74, 280f * fr, 10), 5)) using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 120, 255))) g.FillPath(b, gp); }
		if (msgT > 0f) Txt(g, msg, 17f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 236, 130), 0, 120, W, 30, StringAlignment.Center);
		if (el < 4f) Hint(g, "마우스로 움직이고 · 클릭=필살기", 520 + 15.0, 13f, false, 1f);
	}
}
