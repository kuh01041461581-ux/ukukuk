using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 마룡이 골프: 홀 3개 (파3·파4·파5) · 바람 · 호수/벙커/OB · 퍼팅 ──────────────────────
internal sealed class GolfGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class Hole { public int Par; public PointF Tee, Pin, Slope; public PointF[] Way; public float WayW, GreenR, MinY, MaxY; public float[][] Sand, Water; }
	private static readonly Hole[] Holes = {
		new Hole { Par = 3, Tee = new PointF(200, 470), Pin = new PointF(215, 170), Way = new[] { new PointF(200, 470), new PointF(205, 300), new PointF(215, 190) }, WayW = 120, GreenR = 46, MinY = 40, MaxY = 520, Slope = new PointF(7f, -4f),
			Sand = new[] { new[] { 150f, 190f, 30f, 20f }, new[] { 285f, 170f, 30f, 22f }, new[] { 215f, 240f, 26f, 14f } }, Water = new[] { new[] { 90f, 330f, 55f, 60f } } },
		new Hole { Par = 4, Tee = new PointF(130, 980), Pin = new PointF(285, 330), Way = new[] { new PointF(130, 980), new PointF(140, 800), new PointF(190, 600), new PointF(270, 420), new PointF(285, 340) }, WayW = 100, GreenR = 44, MinY = 170, MaxY = 1030, Slope = new PointF(-8f, 5f),
			Sand = new[] { new[] { 205f, 640f, 32f, 18f }, new[] { 60f, 540f, 30f, 20f }, new[] { 335f, 335f, 26f, 20f }, new[] { 240f, 285f, 26f, 16f } }, Water = new[] { new[] { 330f, 650f, 60f, 90f }, new[] { 140f, 365f, 48f, 32f } } },
		new Hole { Par = 5, Tee = new PointF(280, 1400), Pin = new PointF(120, 380), Way = new[] { new PointF(280, 1400), new PointF(265, 1180), new PointF(190, 980), new PointF(150, 760), new PointF(125, 560), new PointF(120, 400) }, WayW = 96, GreenR = 42, MinY = 220, MaxY = 1450, Slope = new PointF(6f, 7f),
			Sand = new[] { new[] { 190f, 930f, 28f, 18f }, new[] { 100f, 640f, 26f, 18f }, new[] { 165f, 430f, 22f, 14f }, new[] { 70f, 370f, 26f, 18f } }, Water = new[] { new[] { 300f, 880f, 52f, 100f }, new[] { 60f, 1100f, 45f, 70f }, new[] { 200f, 1010f, 36f, 16f } } },
	};
	private static readonly string[] CName = { "드라이버", "3번 우드", "5번 아이언", "7번 아이언", "9번 아이언", "피칭 웨지", "샌드 웨지" }; private static readonly float[] CMax = { 460f, 400f, 340f, 290f, 240f, 180f, 110f }, CRoll = { 0.14f, 0.12f, 0.09f, 0.08f, 0.06f, 0.04f, 0.02f };
	private static readonly string[] SName = { "러프", "페어웨이", "그린", "벙커", "호수", "OB" };
	private int hole, strokes, club, state; private PointF ball, start, end, land, cen = new PointF(200, 300); private float zoom = 1f, gauge, gdir = 1f, accV, accDir = 1f, pPow, pAcc, flyT, flyDur, rollT, swingT, putV, putAng, msgT, sumT, wx, wy, flightH, fallT, tm;
	private float sadT, faceSg = 1f, rtFade, rollRot, hopA, rollDur = 0.7f; private int hopN; private bool lipDone; private readonly List<PointF> rtrail = new List<PointF>();
	private float AimSign() { PointF aw = ToWorld(aimW.X, aimW.Y); return aw.X >= ball.X ? 1f : -1f; }
	private string msg = ""; private PointF aimW; private int hscore; private List<PointF> trail = new List<PointF>(); private bool holed, perfect; private int totalStrokes;
	public override string Costume { get { return "g_golf"; } }
	public override string Name { get { return "마룡이 골프"; } }
	public override string Key { get { return "golf"; } }
	public override string Desc { get { return "바람을 읽고 3홀을 돌아요"; } }
	public override string Help { get { return "① 클럽을 고르면 최대 거리가 정해져요 (아래 ◀ ▶ 또는 1~7 키 · 드라이버 230m ~ 샌드 웨지 55m)\n② 마우스로 방향만 잡고 클릭 (점선 끝 = 이 클럽으로 꽉 쳤을 때 떨어지는 곳 · 바람 보정!)\n③ 마룡이가 나와서 스윙해요: 힘 게이지가 오르내려요 → 원하는 거리에서 클릭 (노란 눈금 = 핀까지 거리)\n④ 정확도 바 한가운데에서 클릭하면 똑바로! → 홀 화면으로 돌아와 공이 날아가요\n호수·OB는 벌타 +1 · 벙커·러프는 거리가 줄어요\n그린에서는 퍼터: 방향은 마우스, 세기는 게이지 · 초록 화살표는 그린의 경사예요\n파 기준 타수가 적을수록 점수가 높아요"; } }
	public override string RightText { get { return (hole + 1) + "/3홀 · " + strokes + "타"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 190, 100))) g.FillEllipse(b, cx - 20, cy - 4, 40, 20);
		using (Pen p = new Pen(Color.FromArgb(240, 240, 240), 2f)) g.DrawLine(p, cx + 4, cy + 6, cx + 4, cy - 18);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 70, 70))) g.FillPolygon(b, new[] { new PointF(cx + 4, cy - 18), new PointF(cx + 16, cy - 13), new PointF(cx + 4, cy - 8) });
	}
	public override void Reset() { hole = -1; Score = 0; Over = false; totalStrokes = 0; NextHole(); }
	private Hole H { get { return Holes[Math.Max(0, hole)]; } }
	private void NextHole()
	{
		hole++; if (hole >= 3) { hole = 2; Over = true; return; }
		strokes = 0; ball = H.Tee; start = ball; state = 0; holed = false; msg = ""; msgT = 0; trail.Clear(); efs.Clear(); rtrail.Clear(); sadT = 0f; rtFade = 0f;
		double a = Rnd.NextDouble() * 6.283; float sp = 1.5f + (float)Rnd.NextDouble() * 6f + hole * 0.8f; wx = (float)Math.Cos(a) * sp; wy = (float)Math.Sin(a) * sp * 0.6f;
		club = 0; AutoClub(); cen = new PointF(200, ball.Y - 170); zoom = 1f;
	}
	private void AutoClub() { if (Surf(ball.X, ball.Y) == 2) return; float d = Dist(ball, H.Pin), lp = LieP(Surf(ball.X, ball.Y)); club = 0; for (int i = CName.Length - 1; i >= 0; i--) if (CMax[i] * lp >= d * 1.02f) { club = i; break; } if (Surf(ball.X, ball.Y) == 3) club = Math.Max(club, 5); }   // 핀까지 닿는 가장 짧은 클럽
	private static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }
	private static float SegD(PointF p, PointF a, PointF b) { float dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy, t = l2 < 1f ? 0f : ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2; t = Math.Max(0f, Math.Min(1f, t)); float qx = a.X + dx * t - p.X, qy = a.Y + dy * t - p.Y; return (float)Math.Sqrt(qx * qx + qy * qy); }
	private int Surf(float x, float y)
	{
		Hole h = H; if (x < 16f || x > 384f || y < h.MinY || y > h.MaxY) return 5;
		foreach (float[] w in h.Water) { float dx = (x - w[0]) / w[2], dy = (y - w[1]) / w[3]; if (dx * dx + dy * dy <= 1f) return 4; }
		if (Dist(new PointF(x, y), h.Pin) <= h.GreenR) return 2;
		foreach (float[] s in h.Sand) { float dx = (x - s[0]) / s[2], dy = (y - s[1]) / s[3]; if (dx * dx + dy * dy <= 1f) return 3; }
		for (int i = 0; i + 1 < h.Way.Length; i++) if (SegD(new PointF(x, y), h.Way[i], h.Way[i + 1]) <= h.WayW / 2f) return 1;
		return 0;
	}
	private float LieP(int s) { return s == 0 ? 0.82f : (s == 3 ? 0.6f : 1f); }
	private float LieA(int s) { return s == 0 ? 1.35f : (s == 3 ? 1.8f : 1f); }
	private float PuttDist { get { return 103f; } }
	private bool Putting { get { return Surf(ball.X, ball.Y) == 2 && state <= 2; } }
	private PointF ToWorld(float sx, float sy) { return new PointF((sx - 200f) / zoom + cen.X, (sy - 300f) / zoom + cen.Y); }
	public override void Press(Keys k) { if (state == 0 && !Putting) { if (k >= Keys.D1 && k <= Keys.D7) club = k - Keys.D1; else if (k == Keys.Left || k == Keys.A) club = Math.Max(0, club - 1); else if (k == Keys.Right || k == Keys.D) club = Math.Min(CName.Length - 1, club + 1); } if (k == Keys.Space) Click(200f, 300f); }
	public override void Click(float x, float y)
	{
		if (Over) return;
		if (state == 0)
		{
			if (!Putting && y >= 552f) { if (x < 70f) club = Math.Max(0, club - 1); else if (x > 330f) club = Math.Min(CName.Length - 1, club + 1); Sfx.Play("tick"); return; }
			state = 1; gauge = 0f; gdir = 1f; Sfx.Play("tick");
		}
		else if (state == 1)
		{
			pPow = gauge; Sfx.Play("tick");
			state = 2; accV = -1f; accDir = 1f;
		}
		else if (state == 2) { pAcc = accV; Launch(); }
	}
	private void Launch()
	{
		strokes++; totalStrokes++; start = ball; trail.Clear(); Sfx.Play("swing");
		PointF aw = ToWorld(aimW.X, aimW.Y);
		float ang = (float)Math.Atan2(aw.Y - ball.Y, aw.X - ball.X);
		if (Putting)
		{
			putAng = ang + pAcc * 0.05f; putV = 20f + pPow * 120f; state = 6; swingT = 0.42f; faceSg = (float)Math.Cos(ang) >= 0f ? 1f : -1f; lipDone = false; rtrail.Clear(); rtrail.Add(ball); rtFade = 3f; rollRot = 0f; sadT = 0f; return;
		}
		int sf = Surf(ball.X, ball.Y); float a = pAcc; perfect = Math.Abs(a) < 0.08f;
		float dist = CMax[club] * LieP(sf) * pPow * (1f - 0.12f * Math.Abs(a)); float an = ang + a * 0.17f * LieA(sf);
		float Tf = 0.8f + dist / 420f; PointF drift = new PointF(wx * Tf * 2.2f, wy * Tf * 2.2f);
		land = new PointF(ball.X + (float)Math.Cos(an) * dist + drift.X, ball.Y + (float)Math.Sin(an) * dist + drift.Y);
		int ls = Surf(land.X, land.Y); float rf = ls == 1 ? 1f : (ls == 0 ? 0.35f : (ls == 2 ? 0.7f : 0f));
		float rd = dist * CRoll[club] * rf; end = land; if (ls != 4 && ls != 5) end = new PointF(land.X + (float)Math.Cos(an) * rd, land.Y + (float)Math.Sin(an) * rd);
		flyDur = Tf; flyT = 0f; rollT = 0f; flightH = 26f + dist * (0.22f + club * 0.03f); swingT = SwingDur; state = 3; faceSg = (float)Math.Cos(ang) >= 0f ? 1f : -1f; hopN = 0; rollRot = 0f; sadT = 0f;
			rollDur = (ls == 4 || ls == 5) ? 1.1f : Math.Max(0.6f, Math.Min(2.2f, 0.55f + rd * 0.024f)); hopA = (ls == 4 || ls == 5) ? 0f : (ls == 1 || ls == 2 ? 1f : 0.45f) * Math.Min(16f, 3f + dist * 0.05f);
	}
	private void Finish()
	{
		int s = Surf(ball.X, ball.Y);
		if (s == 4 || s == 5) { strokes++; totalStrokes++; msg = s == 4 ? "호수에 빠졌어요! 벌타 +1" : "OB! 벌타 +1"; msgT = 2f; Sfx.Play(s == 4 ? "glass" : "bad2"); Shake = 0.3f; ball = start; AutoClub(); state = 0; return; }
		if (strokes >= 9) { msg = "9타 — 여기까지!"; msgT = 1.5f; HoleDone(); return; }
		msg = SName[s] + " · 핀까지 " + (int)(Dist(ball, H.Pin) * (s == 2 ? 0.06f : 0.5f)) + "m"; msgT = 1.6f; if (s == 2 && Dist(ball, H.Pin) > 22f) sadT = 1.0f; AutoClub(); state = 0;
	}
	private void HoleDone()
	{
		int d = strokes - H.Par; string nm = strokes == 1 ? "홀인원!!" : (d <= -3 ? "알바트로스!" : (d == -2 ? "이글!" : (d == -1 ? "버디!" : (d == 0 ? "파" : (d == 1 ? "보기" : "더블보기+")))));
		hscore = Math.Max(0, (H.Par + 3 - strokes)) * 40 + (strokes == 1 ? 300 : 0); Score += hscore; msg = nm + "  +" + hscore; msgT = 2.4f; sumT = 2.4f; state = 7;
		Sfx.Play(d <= -1 ? "fanfare" : "ok2"); if (d <= -1) Fx.Burst(200f, 300f, Color.FromArgb(255, 220, 90), 20);
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (dt > 0.05f) dt = 0.05f; tm += dt; if (msgT > 0f) msgT -= dt; if (swingT > 0f) swingT -= dt; if (sadT > 0f) sadT -= dt; aimW = new PointF(inp.Mx, inp.My); UpdateEfs(dt);
		if (state != 4 && state != 6 && rtFade > 0f) { rtFade -= dt; if (rtFade <= 0f) rtrail.Clear(); }
		// 카메라
		PointF ct; float zt;
		if (Surf(ball.X, ball.Y) == 2 && state != 3 && state != 7) { ct = new PointF((ball.X + H.Pin.X) / 2f, (ball.Y + H.Pin.Y) / 2f); zt = 2.3f; }
		else if (state == 3) { float u = Math.Min(1f, flyT / flyDur); ct = new PointF(200f, Math.Min(ball.Y, Lerp(start.Y, land.Y, u)) - 170f); zt = 1f; }
		else { ct = new PointF(200f, Math.Max(H.MinY + 130f, ball.Y - 170f)); zt = 1f; }
		if (state == 3 || state == 4) ct = new PointF(200f, Math.Max(H.MinY + 130f, (state == 3 ? Lerp(start.Y, land.Y, Math.Min(1f, flyT / flyDur)) : end.Y) - 170f));
		float k = Math.Min(1f, dt * 4f); cen = new PointF(cen.X + (ct.X - cen.X) * k, cen.Y + (ct.Y - cen.Y) * k); zoom += (zt - zoom) * k;
		if (state == 1) { gauge += gdir * dt * 0.85f; if (gauge >= 1f) { gauge = 1f; gdir = -1f; } else if (gauge <= 0f) { gauge = 0f; gdir = 1f; } }
		else if (state == 2) { float asp = Putting ? 2.9f : 2.3f; accV += accDir * dt * asp; if (accV >= 1f) { accV = 1f; accDir = -1f; } else if (accV <= -1f) { accV = -1f; accDir = 1f; } }
		else if (state == 3)
		{
			if (swingT > 0f) return; flyT += dt; { float fu = Math.Min(1f, flyT / flyDur); PointF fp = Pos3(fu); trail.Add(new PointF(fp.X, fp.Y - 4f * flightH * fu * (1f - fu))); } if (flyT >= flyDur) { flyT = flyDur; ball = land; state = 4; rollT = 0f; hopN = 0; rtrail.Clear(); rtrail.Add(land); rtFade = 3f; int ls = Surf(land.X, land.Y); Impact(land, ls, 1f); if (ls == 4) Sfx.Play("glass"); else Sfx.Play("thud2"); }
		}
		else if (state == 4)
		{
			rollT += dt; float u = Math.Min(1f, rollT / rollDur); float e = 1f - (1f - u) * (1f - u); PointF pb0 = ball; ball = new PointF(Lerp(land.X, end.X, e), Lerp(land.Y, end.Y, e)); rollRot += Dist(pb0, ball) / 4.2f * 1.2f;
			if (rtrail.Count == 0 || Dist(rtrail[rtrail.Count - 1], ball) > 2.5f) rtrail.Add(ball);
			if (hopN == 0 && rollT >= 0.24f && hopA > 0.5f) { hopN = 1; Impact(ball, Surf(ball.X, ball.Y), 0.5f); } else if (hopN == 1 && rollT >= 0.42f) { hopN = 2; Impact(ball, Surf(ball.X, ball.Y), 0.25f); }
			if (u >= 1f) { ball = end; Finish(); }
		}
		else if (state == 6)
		{
			if (swingT > 0f) return; float sp = putV, st = 1f / 60f; float rem = dt; Hole h = H;
			while (rem > 0f && state == 6)
			{
				float s = Math.Min(st, rem); rem -= s; float vx = (float)Math.Cos(putAng) * putV + 0f, vy = (float)Math.Sin(putAng) * putV;
				vx += h.Slope.X * s * 4.6f; vy += h.Slope.Y * s * 4.6f; float v = (float)Math.Sqrt(vx * vx + vy * vy); v = Math.Max(0f, v - 95f * s); putAng = (float)Math.Atan2(vy, vx); putV = v;
				ball = new PointF(ball.X + (float)Math.Cos(putAng) * v * s, ball.Y + (float)Math.Sin(putAng) * v * s); rollRot += v * s / 4.2f * 1.2f; if (rtrail.Count == 0 || Dist(rtrail[rtrail.Count - 1], ball) > 2.2f) rtrail.Add(ball);
				if (Dist(ball, h.Pin) < 5.3f)
				{
					if (v < 60f) { ball = h.Pin; holed = true; state = 8; fallT = 0f; Sfx.Play("ok2"); break; }   // 느리게 와야 들어가요
					if (v < 100f && !lipDone) { lipDone = true; putAng += (Rnd.Next(2) == 0 ? 1f : -1f) * (0.7f + (float)Rnd.NextDouble() * 0.5f); putV = v * 0.6f; msg = "아깝! 컵 가장자리에서 튕겼어요"; msgT = 1.3f; Sfx.Play("tick"); continue; }
				}
				if (Surf(ball.X, ball.Y) != 2 && Surf(ball.X, ball.Y) >= 3) { Finish(); if (state == 6) state = 0; break; }
				if (v < 6f) { putV = 0f; Finish(); break; }
			}
		}
		else if (state == 8) { fallT += dt; if (fallT > 0.45f) HoleDone(); }
		else if (state == 7) { sumT -= dt; if (sumT <= 0f) NextHole(); }
	}
	private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
	private PointF Pos3(float u) { return new PointF(Lerp(start.X, land.X, u), Lerp(start.Y, land.Y, u)); }
	private TextureBrush grassTb, sandTb, dirtTb;
	private static TextureBrush Tb(string n, float sc) { Bitmap b = Spr.Get(n); if (b == null) return null; TextureBrush t = new TextureBrush(b); t.ScaleTransform(sc, sc); return t; }
	private Bitmap terBmp; private int terHole = -1; private float terK; private Color terOb = Color.FromArgb(62, 96, 56);
	private Bitmap greenBmp; private int greenHole = -1; private float greenK; private RectangleF greenRc;
	private static readonly string[] TreeN = { "ms_kt1_8", "ms_kt3_2", "ms_kt3_3", "ms_kt7_2", "ms_kt7_8", "ms_kt3_2", "ms_kt1_8" };
	private float SlopeLen { get { return (float)Math.Sqrt(H.Slope.X * H.Slope.X + H.Slope.Y * H.Slope.Y); } }
	private void Terrain(Graphics g, float x0, float y0, float x1, float y1, bool hi)
	{
		Hole h = H; if (grassTb == null) grassTb = Tb("bg_t_grass", 0.55f); if (sandTb == null) sandTb = Tb("bg_t_sand", 0.4f); if (dirtTb == null) dirtTb = Tb("bg_t_dirt", 0.5f);
		RectangleF all = new RectangleF(x0, y0, x1 - x0, y1 - y0);
		if (grassTb != null) g.FillRectangle(grassTb, all); else using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 150, 80))) g.FillRectangle(b, all);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 20, 70, 30))) g.FillRectangle(b, all);
		// OB 바깥: 흙 바닥 + 어둡게 + 나무
		RectangleF[] ob = { new RectangleF(x0, y0, 16f - x0, y1 - y0), new RectangleF(384f, y0, x1 - 384f, y1 - y0), new RectangleF(x0, y0, x1 - x0, h.MinY - y0), new RectangleF(x0, h.MaxY, x1 - x0, y1 - h.MaxY) };
		foreach (RectangleF r in ob) { if (r.Width <= 0f || r.Height <= 0f) continue; if (dirtTb != null) g.FillRectangle(dirtTb, r); using (SolidBrush b = new SolidBrush(Color.FromArgb(dirtTb != null ? 100 : 150, 20, 30, 20))) g.FillRectangle(b, r); }
		using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255), 2f) { DashStyle = DashStyle.Dash }) { g.DrawLine(p, 16, h.MinY, 16, h.MaxY); g.DrawLine(p, 384, h.MinY, 384, h.MaxY); g.DrawLine(p, 16, h.MinY, 384, h.MinY); g.DrawLine(p, 16, h.MaxY, 384, h.MaxY); }
		// 페어웨이 (잔디 결 + 깎은 줄무늬)
		using (Pen p = new Pen(Color.FromArgb(235, 124, 196, 96), h.WayW) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(p, h.Way);
		if (grassTb != null) using (Pen p = new Pen(grassTb, h.WayW * 0.96f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) { g.DrawLines(p, h.Way); using (Pen q = new Pen(Color.FromArgb(120, 150, 220, 110), h.WayW * 0.96f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(q, h.Way); }
		using (Pen p = new Pen(Color.FromArgb(90, 190, 240, 150), h.WayW * 0.55f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(p, h.Way);
		using (Pen p = new Pen(Color.FromArgb(30, 255, 255, 255), h.WayW * 0.96f) { LineJoin = LineJoin.Round }) { p.DashPattern = new float[] { 0.14f, 0.14f }; g.DrawLines(p, h.Way); }
		// 티박스
		{ float tx = h.Tee.X, ty = h.Tee.Y; using (GraphicsPath gp = Gfx.Round(new RectangleF(tx - 34f, ty - 8f, 68f, 26f), 8f)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 150, 214, 110))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(160, 90, 160, 70), 1.6f)) g.DrawPath(p, gp); } using (SolidBrush b = new SolidBrush(Color.White)) { g.FillEllipse(b, tx - 28f, ty - 2f, 5f, 5f); g.FillEllipse(b, tx + 23f, ty - 2f, 5f, 5f); } }
		// 그린 (깃 주변 가장자리 → 부드러운 그린)
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 190, 120))) g.FillEllipse(b, h.Pin.X - h.GreenR - 6, h.Pin.Y - h.GreenR - 6, (h.GreenR + 6) * 2, (h.GreenR + 6) * 2);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 225, 130))) g.FillEllipse(b, h.Pin.X - h.GreenR, h.Pin.Y - h.GreenR, h.GreenR * 2, h.GreenR * 2);
		using (Pen p = new Pen(Color.FromArgb(46, 255, 255, 255), 2f)) for (int i = 1; i < 4; i++) { float r = h.GreenR * i / 4f; g.DrawEllipse(p, h.Pin.X - r, h.Pin.Y - r, r * 2, r * 2); }
		if (hi) GreenDetail(g, h);
		foreach (float[] s in h.Sand)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 150, 110))) g.FillEllipse(b, s[0] - s[2] - 3, s[1] - s[3] - 3, (s[2] + 3) * 2, (s[3] + 3) * 2);
			if (sandTb != null) g.FillEllipse(sandTb, s[0] - s[2], s[1] - s[3], s[2] * 2, s[3] * 2); else using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 216, 150))) g.FillEllipse(b, s[0] - s[2], s[1] - s[3], s[2] * 2, s[3] * 2);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 240, 190))) g.FillEllipse(b, s[0] - s[2], s[1] - s[3], s[2] * 2, s[3] * 2);
			// 갈퀴 자국
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(s[0] - s[2], s[1] - s[3], s[2] * 2, s[3] * 2); GraphicsState gs = g.Save(); g.SetClip(gp, CombineMode.Intersect); using (Pen p = new Pen(Color.FromArgb(46, 150, 110, 60), 1f)) for (float k = -s[2]; k <= s[2]; k += 5f) g.DrawLine(p, s[0] + k - 4f, s[1] - s[3], s[0] + k + 4f, s[1] + s[3]); using (Pen p = new Pen(Color.FromArgb(70, 120, 90, 50), 2f)) g.DrawArc(p, s[0] - s[2], s[1] - s[3], s[2] * 2, s[3] * 2, 200, 120); g.Restore(gs); }
		}
		foreach (float[] w in h.Water)
		{
			RectangleF r = new RectangleF(w[0] - w[2], w[1] - w[3], w[2] * 2, w[3] * 2); using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 230, 255, 255))) g.FillEllipse(b, r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(r); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(110, 190, 255); pb.SurroundColors = new[] { Color.FromArgb(40, 110, 200) }; g.FillPath(pb, gp); } }
			using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 2f)) g.DrawEllipse(p, r.X + 2f, r.Y + 2f, r.Width - 4f, r.Height - 4f);
		}
		// 나무 (다른 게임의 그림을 가져와요) — 코스 양옆 OB 바깥쪽에 줄지어 서 있어요
		List<float[]> trees = new List<float[]>();
		for (int y = (int)(Math.Max(h.MinY - 60f, y0) / 46f) * 46; y < Math.Min(h.MaxY + 60f, y1) + 46; y += 46) for (int side = 0; side < 2; side++) { int sd = ((y * 31 + side * 17) ^ (y >> 3)) & 255; trees.Add(new float[] { side == 0 ? -6f + sd % 12 : 394f + sd % 12, y + sd % 20, 58f + (sd % 5) * 6f, sd % TreeN.Length }); }
		foreach (float[] tr in trees)
		{
			string tn = TreeN[(int)tr[3]]; Bitmap tb = Spr.Get(tn); float th = tr[2], tw = tb != null ? th * tb.Width / tb.Height : th;
			using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(b, tr[0] - tw * 0.36f, tr[1] - th * 0.06f, tw * 0.72f, th * 0.16f);
			if (tb != null) Spr.Draw(g, tn, tr[0], tr[1] - th / 2f + th * 0.04f, tw, th, 0f);
			else { using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 120, 60))) g.FillEllipse(b, tr[0] - 16, tr[1] - 36, 36, 36); }
		}
		// 홀컵
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 220, 140))) g.FillEllipse(b, h.Pin.X - 7f, h.Pin.Y - 5.6f, 14f, 11.2f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 30, 30))) g.FillEllipse(b, h.Pin.X - 5.2f, h.Pin.Y - 4f, 10.4f, 8f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(b, h.Pin.X - 3.5f, h.Pin.Y - 3.2f, 5f, 2f);
	}
	// 확대한 그린: 경사 색(높은 쪽 밝게·낮은 쪽 어둡게) · 등고선 · 굴러가는 방향 화살표
	private void GreenDetail(Graphics g, Hole h)
	{
		float R = h.GreenR, sl = SlopeLen, ux = h.Slope.X / sl, uy = h.Slope.Y / sl, vx = -uy, vy = ux;
		using (GraphicsPath gp = new GraphicsPath())
		{
			gp.AddEllipse(h.Pin.X - R, h.Pin.Y - R, R * 2, R * 2); GraphicsState gs = g.Save(); g.SetClip(gp, CombineMode.Intersect);
			using (LinearGradientBrush lb = new LinearGradientBrush(new PointF(h.Pin.X - ux * R, h.Pin.Y - uy * R), new PointF(h.Pin.X + ux * R, h.Pin.Y + uy * R), Color.FromArgb(80, 255, 255, 190), Color.FromArgb(96, 10, 70, 40))) g.FillRectangle(lb, h.Pin.X - R, h.Pin.Y - R, R * 2, R * 2);
			using (Pen p = new Pen(Color.FromArgb(70, 255, 255, 255), 1f)) for (int i = -3; i <= 3; i++) { float off = i * R / 3.4f; float cx = h.Pin.X + ux * off, cy = h.Pin.Y + uy * off; float bu = 5f * (float)Math.Cos(i * 0.9f); g.DrawCurve(p, new[] { new PointF(cx - vx * R * 1.2f, cy - vy * R * 1.2f), new PointF(cx + ux * bu, cy + uy * bu), new PointF(cx + vx * R * 1.2f, cy + vy * R * 1.2f) }); }
			float len = 5f + sl * 0.7f;
			using (SolidBrush ab = new SolidBrush(Color.FromArgb(120, 20, 80, 50))) for (int a = -3; a <= 3; a++) for (int b = -3; b <= 3; b++)
			{
				float ax = h.Pin.X + a * 17f, ay = h.Pin.Y + b * 17f; if (Dist(new PointF(ax, ay), h.Pin) > R - 7f) continue;
				PointF tip = new PointF(ax + ux * len, ay + uy * len), bl = new PointF(ax + ux * (len - 5f) + vx * 3.2f, ay + uy * (len - 5f) + vy * 3.2f), br = new PointF(ax + ux * (len - 5f) - vx * 3.2f, ay + uy * (len - 5f) - vy * 3.2f);
				using (Pen sp = new Pen(Color.FromArgb(120, 20, 80, 50), 1.6f)) g.DrawLine(sp, ax - ux * len, ay - uy * len, ax + ux * (len - 4f), ay + uy * (len - 4f)); g.FillPolygon(ab, new[] { tip, bl, br });
			}
			g.Restore(gs);
		}
	}
	private void EnsureCache(float K)
	{
		Hole h = H; float cx0 = -40f, cy0 = h.MinY - 120f, cw = 480f, ch = h.MaxY - h.MinY + 240f;
		if (terBmp == null || terHole != hole || Math.Abs(terK - K) > 0.01f)
		{
			if (terBmp != null) terBmp.Dispose(); terBmp = null;
			try
			{
				terBmp = new Bitmap((int)Math.Ceiling(cw * K), (int)Math.Ceiling(ch * K), System.Drawing.Imaging.PixelFormat.Format32bppPArgb); terHole = hole; terK = K;
				using (Graphics tg = Graphics.FromImage(terBmp)) { tg.SmoothingMode = SmoothingMode.AntiAlias; tg.ScaleTransform(K, K); tg.TranslateTransform(-cx0, -cy0); tg.Clear(Color.FromArgb(70, 105, 60)); Terrain(tg, cx0, cy0, cx0 + cw, cy0 + ch, false); }
				Color c0 = terBmp.GetPixel(3, 3); terOb = Color.FromArgb(255, c0.R, c0.G, c0.B);
			}
			catch { terBmp = null; }
		}
		if (greenBmp == null || greenHole != hole || Math.Abs(greenK - K) > 0.01f)
		{
			if (greenBmp != null) greenBmp.Dispose(); greenBmp = null; float gk = K * 2.3f, rw = (h.GreenR + 170f) * 2f, rh = (h.GreenR + 250f) * 2f, rx = h.Pin.X - rw / 2f, ry = h.Pin.Y - rh / 2f;
			try
			{
				greenBmp = new Bitmap((int)Math.Ceiling(rw * gk), (int)Math.Ceiling(rh * gk), System.Drawing.Imaging.PixelFormat.Format32bppPArgb); greenHole = hole; greenK = K; greenRc = new RectangleF(rx, ry, rw, rh);
				using (Graphics tg = Graphics.FromImage(greenBmp)) { tg.SmoothingMode = SmoothingMode.AntiAlias; tg.ScaleTransform(gk, gk); tg.TranslateTransform(-rx, -ry); tg.Clear(Color.FromArgb(70, 105, 60)); Terrain(tg, rx, ry, rx + rw, ry + rh, true); }
			}
			catch { greenBmp = null; }
		}
	}
	// ── 효과: 물결·물보라 / 모래·잔디 알갱이 ──
	private class Ef { public int K; public float X, Y, Z, Vx, Vy, Vz, T, Life, Sz; public Color C; }
	private readonly List<Ef> efs = new List<Ef>();
	private void Bits(PointF p, int n, Color c, float sp, float vz, float sz)
	{
		for (int i = 0; i < n; i++)
		{
			double a = Rnd.NextDouble() * 6.283; float v = sp * (0.35f + (float)Rnd.NextDouble() * 0.65f); int sh = Rnd.Next(-22, 22);
			if (efs.Count > 140) efs.RemoveAt(0);
			efs.Add(new Ef { K = 3, X = p.X, Y = p.Y, Z = 1f, Vx = (float)Math.Cos(a) * v, Vy = (float)Math.Sin(a) * v * 0.45f, Vz = vz * (0.5f + (float)Rnd.NextDouble() * 0.7f), Life = 0.7f + (float)Rnd.NextDouble() * 0.4f, Sz = sz * (0.7f + (float)Rnd.NextDouble() * 0.7f), C = Color.FromArgb(Math.Max(0, Math.Min(255, c.R + sh)), Math.Max(0, Math.Min(255, c.G + sh)), Math.Max(0, Math.Min(255, c.B + sh))) });
		}
	}
	private void Impact(PointF p, int s, float sc)
	{
		if (s == 4)
		{
			if (sc < 0.9f) return;
			for (int i = 0; i < 3; i++) efs.Add(new Ef { K = 0, X = p.X, Y = p.Y, T = -i * 0.24f, Life = 1.3f });
			efs.Add(new Ef { K = 1, X = p.X, Y = p.Y, Life = 0.65f }); efs.Add(new Ef { K = 2, X = p.X, Y = p.Y, Life = 0.85f }); Bits(p, 12, Color.FromArgb(190, 230, 255), 55f, 95f, 1.9f);
		}
		else if (s == 3) { efs.Add(new Ef { K = 4, X = p.X, Y = p.Y, Life = 0.8f }); Bits(p, (int)(22 * sc), Color.FromArgb(226, 200, 140), 60f, 80f, 1.8f); }
		else if (s == 0) Bits(p, (int)(12 * sc), Color.FromArgb(80, 150, 60), 44f, 70f, 1.6f);
		else if (s == 1 || s == 2) Bits(p, (int)(6 * sc), Color.FromArgb(150, 210, 110), 36f, 55f, 1.4f);
		else Bits(p, (int)(8 * sc), Color.FromArgb(150, 150, 140), 40f, 60f, 1.6f);
	}
	private void UpdateEfs(float dt)
	{
		for (int i = efs.Count - 1; i >= 0; i--)
		{
			Ef e = efs[i]; e.T += dt; if (e.T >= e.Life) { efs.RemoveAt(i); continue; }
			if (e.K == 3 && e.T > 0f) { e.X += e.Vx * dt; e.Y += e.Vy * dt; e.Z += e.Vz * dt; e.Vz -= 230f * dt; if (e.Z < 0f) { e.Z = 0f; e.Vz = -e.Vz * 0.28f; e.Vx *= 0.55f; e.Vy *= 0.55f; } }
		}
	}
	private void DrawEfs(Graphics g)
	{
		using (SolidBrush pb = new SolidBrush(Color.White))
			foreach (Ef e in efs)
			{
				if (e.T < 0f) continue; float u = e.T / e.Life;
				if (e.K == 0) { float sz = 8f + u * 62f; using (Pen p = new Pen(Color.FromArgb((int)(200 * (1f - u)), 215, 240, 255), 2.2f * (1f - u) + 0.6f)) g.DrawEllipse(p, e.X - sz / 2f, e.Y - sz * 0.3f, sz, sz * 0.6f); }
				else if (e.K == 1) DrawFx(g, "kfx_2", e.X, e.Y - 12f - u * 6f, 30f + u * 14f, 1f - u);
				else if (e.K == 2) DrawFx(g, "kfx_3", e.X, e.Y - 16f - u * 10f, 34f + u * 24f, 0.9f * (1f - u));
				else if (e.K == 4) DrawFx(g, "kfx_11", e.X, e.Y - 6f - u * 12f, 22f + u * 26f, 0.5f * (1f - u));
				else { pb.Color = Color.FromArgb((int)(240 * Math.Min(1f, (1f - u) * 2f)), e.C); g.FillEllipse(pb, e.X - e.Sz, e.Y - e.Z - e.Sz, e.Sz * 2f, e.Sz * 2f); }
			}
	}
	private void WaterFx(Graphics g, float hw, float hh)
	{
		Hole h = H;
		foreach (float[] w in h.Water)
		{
			if (Math.Abs(w[0] - cen.X) > hw + w[2] || Math.Abs(w[1] - cen.Y) > hh + w[3]) continue;
			RectangleF r = new RectangleF(w[0] - w[2], w[1] - w[3], w[2] * 2, w[3] * 2);
			using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f)) for (int i = 0; i < 3; i++) { float yy = r.Y + r.Height * (0.3f + 0.2f * i); float ww = (float)Math.Sqrt(Math.Max(0f, 1f - Math.Pow((yy - w[1]) / w[3], 2))) * w[2] * 0.6f; g.DrawArc(p, w[0] - ww / 2f + (float)Math.Sin(tm * 2 + i) * 4f, yy, ww, 5f, 0, 180); }
			float ph = (float)((tm * 0.5 + w[0] * 0.013) % 1.0); float rs = 6f + ph * Math.Min(w[2], w[3]) * 0.9f;
			using (Pen p = new Pen(Color.FromArgb((int)(130 * (1f - ph)), 255, 255, 255), 1.4f)) g.DrawEllipse(p, w[0] + w[2] * 0.15f - rs, w[1] - rs * 0.55f, rs * 2f, rs * 1.1f);
		}
	}
	private void SlopeFlow(Graphics g)
	{
		Hole h = H; float sl = SlopeLen, ux = h.Slope.X / sl, uy = h.Slope.Y / sl, vx = -uy, vy = ux;
		for (int i = 0; i < 18; i++)
		{
			float a = i * 2.39996f, rr = (float)Math.Sqrt((i + 0.5f) / 18f) * h.GreenR * 0.9f, bx = h.Pin.X + (float)Math.Cos(a) * rr, by = h.Pin.Y + (float)Math.Sin(a) * rr;
			float ph = (float)((tm * 0.5 + i * 0.173) % 1.0), mv = (ph - 0.5f) * 24f * (sl / 8f), x = bx + ux * mv, y = by + uy * mv; int al = (int)(190 * Math.Sin(ph * Math.PI));
			using (Pen p = new Pen(Color.FromArgb(al, 255, 255, 255), 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, x + ux * 3f, y + uy * 3f, x - ux * 2f + vx * 3f, y - uy * 2f + vy * 3f); g.DrawLine(p, x + ux * 3f, y + uy * 3f, x - ux * 2f - vx * 3f, y - uy * 2f - vy * 3f); }
		}
	}
	// 마룡이를 공 옆에 세워요: 그림은 오른쪽을 보고 있어서 조준이 왼쪽이면 뒤집어요 (headF = 기준 그림에서 클럽 머리의 가로 위치 비율)
	private void Golfer(Graphics g, string n, string baseN, float headF, PointF at, float sg, float hh, float shift)
	{
		Bitmap b = Spr.Get(n), b0 = Spr.Get(baseN); if (b == null || b0 == null) return;
		float k = hh / b0.Height, w0 = b0.Width * k, w = b.Width * k, h2 = b.Height * k; float cx = at.X - sg * ((headF - 0.5f) * w0 + 4f + shift);
		using (SolidBrush sb = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sb, cx - w * 0.3f, at.Y + 1f, w * 0.6f, 6f);
		Spr.Draw(g, n, cx, at.Y + 4f - h2 / 2f, sg < 0f ? -w : w, h2, 0f);
	}
	private float BallZ()
	{
		if (state != 4) return 0f; float z = 0f;
		if (rollT < 0.24f) { float u = rollT / 0.24f; z = hopA * 4f * u * (1f - u); } else if (rollT < 0.42f) { float u = (rollT - 0.24f) / 0.18f; z = hopA * 0.4f * 4f * u * (1f - u); }
		return z;
	}
	private void BallAt(Graphics g, float x, float y, float z, float r, float rot)
	{
		float sh = Math.Max(0.35f, 1f - z * 0.035f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(70 * sh), 0, 0, 0))) g.FillEllipse(b, x - r * 0.95f * sh, y + r * 0.1f, r * 1.9f * sh, r * 0.8f * sh);
		float by = y - z - r * 0.3f; DrawBall(g, x, by, r);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 150, 150, 170))) for (int k = 0; k < 3; k++) { float a = rot + k * 2.094f, c = (float)Math.Cos(a); if (c > 0.1f) g.FillEllipse(b, x + (float)Math.Sin(a) * r * 0.6f - r * 0.12f, by - r * 0.3f * c - r * 0.12f + r * 0.1f * k - r * 0.1f, r * 0.26f, r * 0.26f); }
	}
	public override void Draw(Graphics g, double t)
	{
		Hole h = H; float K = Math.Max(1f, ImgCache.Scale(g)); g.Clear(Color.FromArgb(80, 140, 80));
		GraphicsState st = g.Save(); g.TranslateTransform(200f, 300f); g.ScaleTransform(zoom, zoom); g.TranslateTransform(-cen.X, -cen.Y);
		float hw = 200f / zoom + 10f, hh = 300f / zoom + 10f;
		// 코스 그림(잔디·페어웨이·벙커·물·나무)과 확대한 그린은 홀마다 한 번만 그려 두고 복사만 해요 (매 프레임 다시 그리면 무거워요)
		EnsureCache(K);
		if (terBmp != null)
		{
			float cx0 = -40f, cy0 = h.MinY - 120f, cw = 480f, ch = h.MaxY - h.MinY + 240f;
			using (SolidBrush ob = new SolidBrush(terOb)) g.FillRectangle(ob, cen.X - hw, cen.Y - hh, hw * 2, hh * 2);
			InterpolationMode im0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(terBmp, new RectangleF(cx0, cy0, cw, ch), new RectangleF(0, 0, terBmp.Width, terBmp.Height), GraphicsUnit.Pixel);
			if (zoom > 1.25f && greenBmp != null) g.DrawImage(greenBmp, greenRc, new RectangleF(0, 0, greenBmp.Width, greenBmp.Height), GraphicsUnit.Pixel);
			g.InterpolationMode = im0;
		}
		else Terrain(g, cen.X - hw, cen.Y - hh, cen.X + hw, cen.Y + hh, false);
		WaterFx(g, hw, hh);
		bool putting = Surf(ball.X, ball.Y) == 2 && (state <= 2 || state == 6 || state == 8);
		if (putting && state != 8 && state != 6) SlopeFlow(g);
		// 깃발 (바람에 펄럭여요)
		if (!holed) { float fw = (float)Math.Sin(tm * 6f) * (1.5f + Math.Min(4f, (float)Math.Sqrt(wx * wx + wy * wy) * 0.5f)); using (Pen p = new Pen(Color.White, 2f)) g.DrawLine(p, h.Pin.X, h.Pin.Y, h.Pin.X, h.Pin.Y - 42f); using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 60, 60))) g.FillPolygon(b, new[] { new PointF(h.Pin.X, h.Pin.Y - 42f), new PointF(h.Pin.X + 12f + wx * 0.6f, h.Pin.Y - 39f + fw * 0.5f), new PointF(h.Pin.X + 18f + wx * 0.8f, h.Pin.Y - 36f + wy * 0.5f + fw), new PointF(h.Pin.X, h.Pin.Y - 28f) }); }
		// 조준선
		if (state == 0 || state == 1 || state == 2)
		{
			PointF aw = ToWorld(aimW.X, aimW.Y); float ang = (float)Math.Atan2(aw.Y - ball.Y, aw.X - ball.X), want = Dist(ball, aw);
			if (putting) { float len = Math.Min(want, PuttDist); using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f) { DashStyle = DashStyle.Dot }) g.DrawLine(p, ball.X, ball.Y, ball.X + (float)Math.Cos(ang) * len, ball.Y + (float)Math.Sin(ang) * len); }
			else
			{
				int sf = Surf(ball.X, ball.Y); float mx = CMax[club] * LieP(sf), d = mx, Tf = 0.8f + d / 420f;
				using (Pen rp = new Pen(Color.FromArgb(70, 255, 255, 255), 1f) { DashStyle = DashStyle.Dash }) foreach (float fr in new[] { 0.5f, 0.75f }) { float rr = mx * fr; g.DrawArc(rp, ball.X - rr, ball.Y - rr, rr * 2, rr * 2, ang * 57.3f - 25f, 50f); }
				PointF lp = new PointF(ball.X + (float)Math.Cos(ang) * d + wx * Tf * 2.2f, ball.Y + (float)Math.Sin(ang) * d + wy * Tf * 2.2f);
				using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255), 1.6f) { DashStyle = DashStyle.Dot }) g.DrawLine(p, ball.X, ball.Y, lp.X, lp.Y);
				using (Pen p = new Pen(Color.FromArgb(230, 255, 230, 90), 2f)) { g.DrawEllipse(p, lp.X - 9, lp.Y - 9, 18, 18); g.DrawLine(p, lp.X - 13, lp.Y, lp.X + 13, lp.Y); g.DrawLine(p, lp.X, lp.Y - 13, lp.X, lp.Y + 13); }
			}
		}
		// 공이 굴러간 자국 (퍼팅은 휘어진 길이 그대로 보여요)
		if (rtrail.Count > 1 && rtFade > 0f)
		{
			bool pt = Surf(rtrail[0].X, rtrail[0].Y) == 2; float fa = Math.Min(1f, rtFade);
			using (Pen p = new Pen(pt ? Color.FromArgb((int)(120 * fa), 255, 255, 255) : Color.FromArgb((int)(90 * fa), 220, 245, 170), pt ? 1.6f : 2.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(p, rtrail.ToArray());
		}
		// 마룡이
		{
			PointF stand = (state == 6 || state == 8 || state == 3 || state == 4 || sadT > 0f) ? start : ball; float sgn = (state == 0 || state == 1 || state == 2) ? AimSign() : faceSg;
			if (putting || (state == 6 || state == 8) || sadT > 0f)
			{
				string pn = state == 8 ? "ms_golf_pt2" : (state == 6 ? (swingT > 0.16f ? "ms_golf_pt0" : (swingT > 0f ? "ms_golf_pt1" : "ms_golf_pt4")) : (sadT > 0f ? "ms_golf_pt3" : "ms_golf_pt0"));
				float pull = state == 1 ? gauge * 7f : (state == 6 && swingT > 0.16f ? 7f * pPow : 0f);
				if (Spr.Get("ms_golf_pt0") != null) Golfer(g, pn, "ms_golf_pt0", 0.8f, stand, sgn, 40f, -pull); else DrawMascot(g, ball.X - 24f, ball.Y + 12f, 52f, 0f);
			}
			else if (state == 0 || state == 1 || state == 2) { if (Spr.Get("ms_golf_sw0") != null) Golfer(g, "ms_golf_sw0", "ms_golf_sw0", 0.75f, ball, sgn, 42f, 0f); else DrawMascot(g, ball.X - 24f, ball.Y + 12f, 52f, 0f); }
			else if ((state == 3 && swingT <= 0f) || state == 4) { if (Spr.Get("ms_golf_pt4") != null) Golfer(g, "ms_golf_pt4", "ms_golf_pt0", 0.8f, stand, sgn, 40f, 0f); }
		}
		// 공
		if (state == 3)
		{
			float u = Math.Min(1f, flyT / flyDur); PointF p0 = Pos3(u); float z = 4f * flightH * u * (1f - u);
			using (SolidBrush b = new SolidBrush(Color.White)) for (int i = 0; i < trail.Count; i += 2) { float tf = (float)i / Math.Max(1, trail.Count); b.Color = Color.FromArgb((int)(130 * tf), 255, 255, 255); PointF q = trail[i]; g.FillEllipse(b, q.X - 1.5f, q.Y - 1.5f, 3f, 3f); }
			BallAt(g, p0.X, p0.Y, z, 4.2f + z * 0.02f, flyT * 22f);
		}
		else if (!(holed && fallT > 0.2f) && !(state == 4 && Surf(land.X, land.Y) == 4 && rollT > 0.1f))
		{
			float br = putting ? 4.6f : 4.2f; float sink = state == 8 ? Math.Max(0.1f, 1f - fallT / 0.45f) : 1f;
			BallAt(g, ball.X, ball.Y, BallZ(), br * sink, rollRot);
		}
		DrawEfs(g);
		g.Restore(st);
		DrawHud(g, t);
	}
	private static void DrawBall(Graphics g, float x, float y, float r)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 250, 250))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2); using (Pen p = new Pen(Color.FromArgb(120, 90, 90, 110), 1f)) g.DrawEllipse(p, x - r, y - r, r * 2, r * 2); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 120, 120, 150))) { g.FillEllipse(b, x - r * 0.3f, y - r * 0.2f, r * 0.4f, r * 0.4f); }
	}
	private void Bar(Graphics g, float x, float y, float w, float hh, Color c, float f)
	{
		using (GraphicsPath gp = Gfx.Round(new RectangleF(x, y, w, hh), hh / 2f)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 10, 30))) g.FillPath(b, gp); }
		if (f > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(x, y, Math.Max(hh, w * f), hh), hh / 2f)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, gp);
	}
	private void DrawHud(Graphics g, double t)
	{
		Hole h = H; bool putting = Surf(ball.X, ball.Y) == 2;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(8, 42, 160, 44), 12)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 20, 30))) g.FillPath(b, gp);
		Txt(g, (hole + 1) + "번 홀 · 파 " + h.Par, 11f, 2, Color.White, 16, 44, 150, 20, StringAlignment.Near);
		Txt(g, "핀까지 " + (int)(Dist(ball, h.Pin) * (putting ? 0.06f : 0.5f)) + "m · " + SName[Math.Min(5, Surf(ball.X, ball.Y))], 9.5f, 2, Color.FromArgb(235, 255, 255, 255), 16, 63, 150, 20, StringAlignment.Near);
		// 바람
		using (GraphicsPath gp = Gfx.Round(new RectangleF(W - 108, 42, 100, 44), 12)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 20, 30))) g.FillPath(b, gp);
		float ws = (float)Math.Sqrt(wx * wx + wy * wy), wa = (float)Math.Atan2(wy, wx); GraphicsState s0 = g.Save(); g.TranslateTransform(W - 84f, 64f); g.RotateTransform(wa * 57.3f);
		using (Pen p = new Pen(Color.FromArgb(255, 150, 220, 255), 3f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(p, -12, 0, 12, 0); g.Restore(s0);
		Txt(g, "바람 " + ws.ToString("0.0") + "m/s", 9f, 2, Color.White, W - 66, 50, 62, 18, StringAlignment.Near); Txt(g, WindDir(wa), 9f, 1, Color.FromArgb(200, 255, 255, 255), W - 66, 65, 62, 16, StringAlignment.Near);
		if (state == 0) { if (!putting) { RectangleF r = new RectangleF(10, 556, 380, 36); using (GraphicsPath gp = Gfx.Round(r, 12)) using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 10, 20, 30))) g.FillPath(b, gp); Txt(g, "◀", 14f, 2, club > 0 ? Color.White : Color.FromArgb(90, 255, 255, 255), 14, 558, 50, 32, StringAlignment.Center); Txt(g, "▶", 14f, 2, club < CName.Length - 1 ? Color.White : Color.FromArgb(90, 255, 255, 255), 336, 558, 50, 32, StringAlignment.Center); Txt(g, (club + 1) + ". " + CName[club] + "  ·  최대 " + (int)(CMax[club] * LieP(Surf(ball.X, ball.Y)) * 0.5f) + "m", 11.5f, 2, Color.FromArgb(255, 230, 140), 64, 558, 272, 32, StringAlignment.Center); } else Txt(g, "퍼터 · 마우스로 방향, 클릭으로 세기", 11f, 2, Color.White, 0, 568, W, 22, StringAlignment.Center); if (msgT <= 0f) Hint(g, putting ? "방향을 잡고 클릭!" : "클럽을 고르고 방향을 잡은 뒤 클릭!", 520 + 15.0, 12f, false, 1f); }
		float want = 0f;
		if (!putting && (state == 1 || state == 2 || (state == 3 && swingT > 0f))) SwingScene(g, t);
		if (state == 1 || state == 2)
		{
			PointF aw = ToWorld(aimW.X, aimW.Y); float d = Dist(ball, aw);
			if (putting) want = Math.Min(1f, (float)((Math.Sqrt(2f * 95f * Math.Min(d, PuttDist)) - 20f) / 120f)); else { float mx = CMax[club] * LieP(Surf(ball.X, ball.Y)); want = Math.Min(1f, Dist(ball, H.Pin) / mx); }   // 노란 눈금 = 핀까지 거리
			float bx = 60f, bw = 280f; Bar(g, bx, 524, bw, 16, Color.FromArgb(255, 160, 80), state == 1 ? gauge : pPow); using (Pen p = new Pen(Color.FromArgb(255, 255, 255, 90), 3f)) g.DrawLine(p, bx + bw * want, 519, bx + bw * want, 545);
			Txt(g, "힘", 9f, 2, Color.White, 14, 522, 40, 20, StringAlignment.Near);
			if (!putting) { Bar(g, bx, 548, bw, 14, Color.FromArgb(0, 0, 0, 0), 0f); using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 110, 230, 150))) g.FillRectangle(b, bx + bw / 2f - 9f, 548, 18f, 14); using (Pen p = new Pen(Color.White, 2f)) g.DrawLine(p, bx + bw / 2f, 545, bx + bw / 2f, 565); if (state == 2) using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, bx + bw / 2f + accV * (bw / 2f - 7f) - 7f, 548, 14, 14); Txt(g, "정확도", 9f, 2, Color.White, 8, 546, 50, 20, StringAlignment.Near); }
		}
		if (msgT > 0f) Txt(g, msg, 17f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 236, 130), 0, 100, W, 30, StringAlignment.Center);
		if (state == 3 && perfect && flyT < 0.6f) Txt(g, "굿 샷!", 18f, 2, Color.FromArgb(255, 255, 230, 100), 0, 260, W, 30, StringAlignment.Center);
	}
	// ── 스윙 장면: 거리·방향을 정한 뒤 마룡이가 나와서 백스윙(힘 게이지) → 다운스윙 → 임팩트 → 다시 홀 화면에서 공이 날아가요 ──
	private const float SwingDur = 0.8f;
	private void SwingScene(Graphics g, double t)
	{
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, 0, W, 430), Color.FromArgb(120, 190, 245), Color.FromArgb(200, 232, 255), 90f)) g.FillRectangle(lb, 0, 0, W, 430);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 190, 110))) g.FillRectangle(b, 0, 300, W, 130);
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, 428, W, MiniGame.H - 428), Color.FromArgb(96, 170, 84), Color.FromArgb(70, 140, 64), 90f)) g.FillRectangle(lb, 0, 428, W, MiniGame.H - 428);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 210, 140))) for (int i = 0; i < 9; i++) g.FillEllipse(b, i * 52 - 20 + (float)((t * 4) % 1), 296, 70, 16);   // 먼 언덕
		// 클럽 각도: 0 = 어드레스(공을 향해 아래) · 양수 = 백스윙(뒤로 들어 올림) · 음수 = 팔로스루
		float th, u = 0f; bool after = false;
		if (state == 1 || state == 2) th = 215f * (state == 1 ? gauge : pPow) * (state == 1 ? 1f : 1f);
		else { u = 1f - swingT / SwingDur; float top = 215f * pPow; if (u < 0.35f) { float k = u / 0.35f; th = top * (1f - k * k); } else { after = true; float k = (u - 0.35f) / 0.65f; th = -200f * (1f - (1f - k) * (1f - k)); } }
		string sp = null; if (Spr.Get("ms_golf_sw0") != null) { int fi = th > 150f ? 2 : (th > 50f ? 1 : (th > -40f ? (after ? 3 : 0) : (th > -150f ? 4 : 5))); sp = "ms_golf_sw" + fi; }
		float hx = 192f, hy = 372f, len = 62f - club * 1.5f;
		// 공 (임팩트 뒤엔 날아가요)
		float bx = 218f, by = 424f; if (after) { float k = (u - 0.35f) / 0.65f; bx += k * 260f; by -= k * 200f + k * k * 60f; }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, 210, 424, 18, 6);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 240, 230))) g.FillRectangle(b, 216, 424, 4, 6);   // 티
		DrawBall(g, bx, by - 5f, 6f);
		if (sp != null) { Spr.Frame(g, sp, 170f, 432f, 1f, 0f, false); }
		else
		{
			// 그림이 없을 때: 마룡이(골프 옷) + 클럽을 직접 그려요
			Image m = Mascot; if (m != null) { float mh = 190f, mw = mh * m.Width / m.Height; g.DrawImage(m, 160f - mw / 2f, 432f - mh, mw, mh); }
			double a = (90 + th) * Math.PI / 180;   // 아래(90°)에서 백스윙은 시계 반대로
			float ex = hx + (float)Math.Cos(a) * len * 0.92f, ey = hy + (float)Math.Sin(a) * len;
			using (Pen p = new Pen(Color.FromArgb(70, 70, 80), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, hx, hy, ex, ey);
			using (SolidBrush b = new SolidBrush(club == 0 || club == 1 ? Color.FromArgb(50, 50, 60) : Color.FromArgb(190, 196, 210))) g.FillEllipse(b, ex - (club <= 1 ? 9f : 6f), ey - 5f, club <= 1 ? 18f : 12f, 10f);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 240, 240, 245))) g.FillEllipse(b, hx - 7, hy - 7, 14, 14);   // 장갑
		}
		if (state == 3 && u > 0.33f && u < 0.45f) { using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 245, 160))) g.FillEllipse(b, 196, 400, 50, 40); Txt(g, perfect ? "나이스 샷!" : "딱!", 18f, 2, Color.FromArgb(255, 120, 60, 20), 0, 200, W, 34, StringAlignment.Center); }
		// 거리 표시
		float mx = CMax[club] * LieP(Surf(ball.X, ball.Y)); float pw = state == 1 ? gauge : pPow;
		Txt(g, CName[club], 13f, 2, Color.FromArgb(40, 60, 90), 0, 50, W, 26, StringAlignment.Center);
		Txt(g, (int)(mx * pw * 0.5f) + "m", 26f, 2, Color.FromArgb(30, 50, 80), 0, 80, W, 44, StringAlignment.Center);
		Txt(g, "핀까지 " + (int)(Dist(ball, H.Pin) * 0.5f) + "m · 최대 " + (int)(mx * 0.5f) + "m", 10f, 1, Color.FromArgb(70, 90, 120), 0, 126, W, 20, StringAlignment.Center);
	}
	private static string WindDir(float a) { int i = (int)Math.Round(((a * 57.3f + 360f) % 360f) / 45f) % 8; string[] n = { "오른쪽으로", "앞·오른쪽", "앞쪽으로", "앞·왼쪽", "왼쪽으로", "뒤·왼쪽", "뒤에서 앞으로", "뒤·오른쪽" }; return n[i]; }
}
