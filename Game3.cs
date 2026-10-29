using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 13. 농구 슛 ─────────────────────────────────────────────
// 공을 뒤로 당겼다 놓으면 그 힘과 방향대로 포물선을 그리며 날아가요. 림(테두리)과 백보드에 튕기고, 골대 위에서 아래로 통과해야 득점이에요.
internal sealed class BasketGame : MiniGame
{
	private const float R = 14f, G = 1500f, SY = 505f, HoopX = 326f, RimHalfBase = 34f, HoopY = 300f, Apex = 70f, VxMin = 60f, VxMax = 300f;
	private static readonly float[] Spots = { 95f, 130f, 165f, 200f, 235f };   // 슛 자리 (왼쪽일수록 멀어서 3점)
	private float sx = 165f, gv, gT, wind, angD = 72f, lastPw = -1f, hoopBob; private bool rimT, boardT; private readonly List<PointF> lastPath = new List<PointF>(), curPath = new List<PointF>(); private bool charging; private int lastSpot = -1;
	private static float Vy0() { return (float)Math.Sqrt(2f * G * (SY - HoopY + Apex)); }
	private void PickSpot()
	{
		int k; do { k = Rnd.Next(Spots.Length); } while (k == lastSpot); lastSpot = k; sx = Spots[k];
		float wmax = made >= 2 ? Math.Min(90f, 30f + made * 6f) : 0f; wind = wmax * (0.35f + 0.65f * (float)Rnd.NextDouble()) * (Rnd.Next(2) == 0 ? -1f : 1f);   // 바람: 공이 날아가는 동안 옆으로 밀어요 (점수가 오를수록 세져요)
	}
	private float RimHalf { get { return made >= 10 ? 29f : (made >= 6 ? 31.5f : RimHalfBase); } }
	private float lT, floorT, netT, hy = 240f, bx, by, bvx, bvy, pbx, pby, ax, ay, cx, cy, hoopC, resetT, popT, time; private bool flying, aiming, touched, scored, wasHold, buzz; private float buzzT; private double left; private int made, streak; private string pop = "";
	public override string Costume { get { return "g_bball"; } }
	public override string Name { get { return "농구 슛"; } }
	public override string Key { get { return "hoop"; } }
	public override string Desc { get { return "각도와 힘을 맞춰 슛!"; } }
	public override string Help { get { return "마우스 위치(또는 ← →)로 던질 각도를 정해요\n꾹 누르면 힘 게이지가 차올라요 → 원하는 힘에서 놓으면 슛! (▼ 표시는 지난 슛의 힘)\n백보드에 맞고 들어가는 뱅크슛은 +1점 · 지난 슛이 날아간 길이 점선으로 남아요\n5골부터 골대가 위아래로 움직여요\n왼쪽 자리(3점 거리)는 +1점 · 림에 안 닿고 쏙 들어가면 3점\n점수가 오르면 바람이 불어 공이 옆으로 밀려요 (위에 화살표 · 초록 구간도 바람에 맞게 옮겨져요) · 잘할수록 초록 구간이 좁아져요\n45초 승부 · 종료 직전에 던진 공은 끝까지 날아가요(버저비터!)"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override void Reset() { buzz = false; left = 60; made = 0; angD = 72f; lastPw = -1f; lastPath.Clear(); curPath.Clear(); hoopBob = 0f; streak = 0; Score = 0; Over = false; aiming = false; charging = false; wasHold = false; time = 0; hoopC = HoopX; hy = HoopY; pop = ""; popT = 0; lastSpot = -1; ResetBall(); resetT = 0.3f; }
	public override void Icon(Graphics g, float cx2, float cy2)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 140, 50))) g.FillEllipse(b, cx2 - 15, cy2 - 15, 30, 30);
		using (Pen p = new Pen(Color.FromArgb(120, 60, 20), 1.6f)) { g.DrawEllipse(p, cx2 - 15, cy2 - 15, 30, 30); g.DrawLine(p, cx2 - 15, cy2, cx2 + 15, cy2); g.DrawLine(p, cx2, cy2 - 15, cx2, cy2 + 15); }
	}
	private void ResetBall() { PickSpot(); floorT = 0f; bx = sx; by = SY; bvx = bvy = 0; flying = false; touched = false; scored = false; resetT = 0.08f; }
	private void Launch(out float vx, out float vy) { float v = 470f + 930f * gv, a = angD * (float)Math.PI / 180f; vx = v * (float)Math.Cos(a); vy = -v * (float)Math.Sin(a); }
	private static void Bounce(ref float bx, ref float by, ref float vx, ref float vy, float px, float py, float pr, float br, out bool hit)
	{
		hit = false; float dx = bx - px, dy = by - py, d = (float)Math.Sqrt(dx * dx + dy * dy);
		if (d >= br + pr || d < 0.001f) return;
		float nx = dx / d, ny = dy / d, vn = vx * nx + vy * ny; hit = true;
		bx = px + nx * (br + pr); by = py + ny * (br + pr);
		if (vn < 0) { vx -= 1.62f * vn * nx; vy -= 1.62f * vn * ny; }
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		time += dt; left -= dt; if (left > 60 - PlayT) left = 60 - PlayT;
		if (left <= 0)
		{
			left = 0;   // 버저비터: 종료 전에 손을 떠난 공은 끝까지 날아가서 들어가면 점수가 인정돼요
			if (!buzz && flying) { buzz = true; buzzT = 0f; pop = "버저! 마지막 슛…"; popT = 1.6f; Sfx.Play("over"); }
			if (!buzz || !flying) { Over = true; return; }
			buzzT += dt; if (buzzT > 6f) { Over = true; return; }
		}
		if (popT > 0) popT -= dt; if (resetT > 0) resetT -= dt; if (netT > 0) netT -= dt * 2.2f;
		hoopBob = made >= 5 ? Math.Min(42f, (made - 4) * 7f) : 0f; hoopC = HoopX; hy = HoopY + hoopBob * (float)Math.Sin(time * 0.9);   // 5골부터 골대가 위아래로 천천히 움직여요
		cx = inp.Mx; cy = inp.My;
		if (!wasHold && inp.Hold && !flying && resetT <= 0 && left > 0f) { charging = true; gT = 0f; gv = 0f; }
		if (left <= 0f) charging = false;
		if (!flying) { if (inp.MouseMoved) angD = Clamp((float)(Math.Atan2(SY - cy, cx - sx) * 180.0 / Math.PI), 30f, 86f); if (inp.L) angD = Clamp(angD - 40f * dt, 30f, 86f); if (inp.R) angD = Clamp(angD + 40f * dt, 30f, 86f); }   // 마우스 위치(또는 ← →)로 각도
		if (charging) { gT += dt * (0.5f + Math.Min(0.2f, made * 0.01f)); float ph = gT % 2f; gv = ph < 1f ? ph : 2f - ph; }   // 힘 게이지: 꾹 누르는 동안 차올랐다 내려갔다 해요
		if (charging && !inp.Hold) { charging = false; float vx, vy; Launch(out vx, out vy); bvx = vx; bvy = vy; flying = true; lT = 0f; touched = false; rimT = false; boardT = false; scored = false; lastPw = gv; lastPath.Clear(); curPath.Clear(); Sfx.Play("swing"); }
		wasHold = inp.Hold;
		if (!flying) return;
		lT += dt; int n = 5; float h = dt / n;
		for (int i = 0; i < n; i++)
		{
			pbx = bx; pby = by; bvy += G * h; if (!scored && by < 556f) bvx += wind * h; bx += bvx * h; by += bvy * h;
			bool hit;
			Bounce(ref bx, ref by, ref bvx, ref bvy, hoopC - RimHalf, hy, 4f, R, out hit); if (hit) { if (!touched) { Sfx.Play("bong"); Shake = Math.Max(Shake, 0.12f); Fx.Burst(bx, by, Color.FromArgb(255, 200, 120), 3); } touched = true; rimT = true; }
			Bounce(ref bx, ref by, ref bvx, ref bvy, hoopC + RimHalf, hy, 4f, R, out hit); if (hit) { if (!touched) { Sfx.Play("bong"); Shake = Math.Max(Shake, 0.12f); Fx.Burst(bx, by, Color.FromArgb(255, 200, 120), 3); } touched = true; rimT = true; }
			float b0 = hoopC + RimHalf + 4f, b1 = b0 + 8f, y0 = hy - 78f, y1 = hy + 8f;   // 백보드
			if (bx + R > b0 && bx - R < b1 && by > y0 - R && by < y1 + R) { if (bx < (b0 + b1) / 2) { bx = b0 - R; if (bvx > 0) bvx = -bvx * 0.55f; } else { bx = b1 + R; if (bvx < 0) bvx = -bvx * 0.55f; } touched = true; boardT = true; }
			if (!scored && pby < hy && by >= hy && bvy > 0 && Math.Abs(bx - hoopC) < RimHalf - 8f)
			{
				scored = true; made++; streak++; netT = 1f; Shake = touched ? 0.25f : 0.5f; { PointF hq = new PointF(hoopC, hy + 8f); Fx.Burst(hq.X, hq.Y, touched ? Color.FromArgb(255, 200, 90) : Color.FromArgb(255, 120, 70), touched ? 8 : 16); if (streak >= 3) Fx.Burst(hoopC, hy - 30f, Color.FromArgb(255, 90, 40), 12); } Sfx.Play(touched ? "ok" : "fanfare"); bool bank = boardT && !rimT; int pts = touched ? (bank ? 3 : 2) : 3; bool far3 = sx < 150f; Score += pts + (streak >= 3 ? 1 : 0) + (far3 ? 1 : 0);
				pop = (buzz ? "버저비터!! " : "") + (touched ? (boardT && !rimT ? "뱅크슛! +3" : "골인! +2") : "깨끗한 3점슛! +3") + (far3 ? " (먼 거리 +1)" : "") + (streak >= 3 ? " (연속 보너스 +1)" : ""); popT = 1.8f; if (buzz) { Shake = 0.6f; Sfx.Play("fanfare"); }
			}
			if (scored && by > hy && by < hy + 70f) { bvx *= (float)Math.Exp(-6f * h); }
			if (by > 560f - R) { by = 560f - R; if (bvy > 60f) { if (bvy > 180f) Sfx.Play("tick"); bvy = -bvy * 0.55f; bvx *= 0.8f; } else bvy = 0f; floorT += h; }   // 바닥에서 통통 튀어요
			if (bx > W - R) { bx = W - R; if (bvx > 0) bvx = -bvx * 0.5f; }
			if (bx < R && bvx < 0) { bx = R; bvx = -bvx * 0.5f; }
		}
		if (flying && curPath.Count < 90 && (int)(lT * 30f) > curPath.Count) curPath.Add(new PointF(bx, by));
		if (floorT > 0.1f || (scored && by > 548f) || by > H + 40) { if (!scored) streak = 0; floorT = 0f; lastPath.Clear(); lastPath.AddRange(curPath); curPath.Clear(); ResetBall(); }
	}
	// 옆모습(사선) 골대: 백보드는 오른쪽에서 비스듬히 보이고, 림은 납작한 타원, 그물은 아래로 모여요
	private void DrawHoop(Graphics g, double tm)
	{
		float rl = hoopC - RimHalf, rr = hoopC + RimHalf, b0 = rr + 4f, y0 = hy - 78f, y1 = hy + 8f, sk = 12f;
		// 지지대
		using (LinearGradientBrush pb = new LinearGradientBrush(new RectangleF(b0 + 4, hy, 10, 560 - hy), Color.FromArgb(200, 205, 222), Color.FromArgb(120, 126, 150), 0f)) g.FillRectangle(pb, b0 + 5f, hy + 6f, 8f, 560f - hy - 6f);
		using (Pen ap = new Pen(Color.FromArgb(150, 156, 178), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(ap, b0 + 9f, hy - 10f, b0 + 9f, hy + 24f);
		// 백보드 (비스듬한 면 + 두께)
		PointF[] face = { new PointF(b0, y0 + sk), new PointF(b0 + 14f, y0), new PointF(b0 + 14f, y1 - sk), new PointF(b0, y1) };
		PointF[] edge = { new PointF(b0 + 14f, y0), new PointF(b0 + 20f, y0 - 4f), new PointF(b0 + 20f, y1 - sk - 4f), new PointF(b0 + 14f, y1 - sk) };
		using (SolidBrush eb = new SolidBrush(Color.FromArgb(150, 156, 180))) g.FillPolygon(eb, edge);
		using (LinearGradientBrush fb = new LinearGradientBrush(new RectangleF(b0, y0, 16, y1 - y0), Color.FromArgb(250, 252, 255), Color.FromArgb(196, 214, 238), 90f)) g.FillPolygon(fb, face);
		using (Pen op = new Pen(Color.FromArgb(120, 70, 90, 140), 1.6f)) { g.DrawPolygon(op, face); g.DrawPolygon(op, edge); }
		using (Pen sq = new Pen(Color.FromArgb(220, 232, 70, 50), 2f)) g.DrawPolygon(sq, new PointF[] { new PointF(b0 + 2f, hy - 30f), new PointF(b0 + 12f, hy - 35f), new PointF(b0 + 12f, hy - 6f), new PointF(b0 + 2f, hy - 1f) });
		// 림 뒤쪽 반원 → 그물 → 림 앞쪽 반원
		float ry = 6f, sw = netT > 0f ? (float)Math.Sin(netT * 14f) * 3.2f * Math.Min(1f, netT) : 0f;
		using (Pen rb = new Pen(Color.FromArgb(200, 160, 40, 30), 4.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(rb, rl - 1f, hy - ry, rr - rl + 2f, ry * 2f, 180, 180);
		using (Pen np = new Pen(Color.FromArgb(215, 255, 255, 255), 1.3f))
		{
			int cols = 7; float nb = hy + 36f, bw = (rr - rl) * 0.52f;
			for (int i = 0; i <= cols; i++) { float u = i / (float)cols; float x1 = rl + u * (rr - rl), x2 = hoopC + (u - 0.5f) * bw + sw; g.DrawLine(np, x1, hy + 1f, x2, nb); }
			for (int k = 1; k <= 4; k++) { float v = k / 5f, yy = hy + 1f + v * (nb - hy), wl = (rr - rl) * (1f - v * 0.48f); float cxn = hoopC + sw * v; PointF[] zz = new PointF[cols + 1]; for (int i = 0; i <= cols; i++) { float u = i / (float)cols; zz[i] = new PointF(cxn + (u - 0.5f) * wl, yy + ((i & 1) == 0 ? -2f : 2f)); } g.DrawLines(np, zz); }
		}
		using (Pen rf = new Pen(Color.FromArgb(255, 238, 84, 56), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(rf, rl - 1f, hy - ry, rr - rl + 2f, ry * 2f, 0, 180);
		using (Pen rt = new Pen(Color.FromArgb(255, 200, 52, 40), 5f) { StartCap = LineCap.Round }) g.DrawLine(rt, rr, hy, b0 + 1f, hy);
	}
	public override void Draw(Graphics g, double tm)
	{
		BgImg(g, "bg_plaza", Color.FromArgb(38, 30, 80), Color.FromArgb(120, 86, 150)); if (Spr.Get("bg_plaza") != null) using (SolidBrush ob = new SolidBrush(Color.FromArgb(110, 30, 24, 70))) g.FillRectangle(ob, 0, 0, W, H);
		using (Pen p = new Pen(Color.FromArgb(30, 255, 255, 255), 2f)) for (int i = 0; i <= 8; i++) g.DrawLine(p, i * 50f, 0, i * 50f, 560);
		for (int i = 0; i < 4; i++) { float lx = 50 + i * 100; using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 255, 240, 190))) g.FillPolygon(b, new PointF[] { new PointF(lx - 10, 0), new PointF(lx + 10, 0), new PointF(lx + 46, 560), new PointF(lx - 46, 560) }); using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 255, 244, 210))) g.FillEllipse(b, lx - 9, 6, 18, 9); }
		if (Spr.Get("ms_bk_crowd") != null) { float cj = ((made > 0 && popT > 0) || streak >= 3) ? -Math.Abs((float)Math.Sin(tm * 8)) * 4f : 0f; Bitmap cb = Spr.Get("ms_bk_crowd"); float cw = 392f, chh = cw * cb.Height / cb.Width; Spr.Draw(g, "ms_bk_crowd", W / 2f, 512f - chh / 2f + cj, cw, chh, 0f); } else for (int row = 0; row < 3; row++) for (int k = 0; k < 14; k++) { float cxp = k * 31f + (row % 2) * 15f + 8f, cyp = 470 + row * 28f; bool cheer = (made > 0 && popT > 0) || streak >= 3; float jmp = cheer ? -Math.Abs((float)Math.Sin(tm * 8 + k + row)) * 6f : 0f; Color hc = Color.FromArgb(60, 30 + (k * 53) % 120, 50 + (k * 29 + row * 40) % 120, 120); using (SolidBrush cb = new SolidBrush(Color.FromArgb(70, 22, 16, 50))) g.FillEllipse(cb, cxp - 11, cyp + 4 + jmp, 22, 34); using (SolidBrush hb2 = new SolidBrush(hc)) g.FillEllipse(hb2, cxp - 6, cyp - 6 + jmp, 12, 12); }
		using (LinearGradientBrush fl = new LinearGradientBrush(new RectangleF(0, 560, W, 40), Color.FromArgb(176, 112, 64), Color.FromArgb(120, 76, 44), 90f)) g.FillRectangle(fl, 0, 560, W, 40);
		using (Pen wp = new Pen(Color.FromArgb(40, 60, 30, 10), 1.4f)) for (int k = 0; k < 9; k++) g.DrawLine(wp, k * 46f, 560, k * 46f - 18f, 600);
		using (Pen cp = new Pen(Color.FromArgb(70, 255, 255, 255), 2f)) g.DrawArc(cp, 180, 548, 320, 90, 180, 180);
		using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawLine(p, 0, 560, W, 560);
		// 골대
		if (netT > 0.55f) { float ga = (netT - 0.55f) / 0.45f; using (SolidBrush gb = new SolidBrush(Color.FromArgb((int)(70 * ga), 255, 220, 120))) g.FillEllipse(gb, hoopC - 54, hy - 26, 108, 60); float rr2 = 20f + (1f - netT) * 120f; using (Pen rp = new Pen(Color.FromArgb((int)(200 * ga), 255, 230, 140), 3f)) g.DrawEllipse(rp, hoopC - rr2, hy - rr2 * 0.35f, rr2 * 2, rr2 * 0.7f); }
		DrawHoop(g, tm);
		if (Math.Abs(wind) > 1f)
		{   // 바람 표시: 화살표 방향으로 공이 밀려요 (초록 구간도 그만큼 옮겨져 있어요)
			float wk = Math.Abs(wind) / 90f; float dir = wind > 0 ? 1f : -1f; float wx0 = W / 2f - 54f, wy0 = 92f;
			using (GraphicsPath wp0 = Gfx.Round(new RectangleF(wx0, wy0, 108f, 24f), 12)) using (SolidBrush wb0 = new SolidBrush(Color.FromArgb(150, 10, 16, 40))) g.FillPath(wb0, wp0);
			Txt(g, "바람", 9f, 1, Color.FromArgb(230, 190, 225, 255), wx0 + 8f, wy0 + 4f, 36f, 16f, StringAlignment.Near);
			using (Pen ap = new Pen(Color.FromArgb(240, 150, 215, 255), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { float ax0 = wx0 + 76f - dir * 18f * (0.4f + wk), ax1 = wx0 + 76f + dir * 18f * (0.4f + wk); g.DrawLine(ap, ax0, wy0 + 12f, ax1, wy0 + 12f); g.DrawLine(ap, ax1, wy0 + 12f, ax1 - dir * 6f, wy0 + 7f); g.DrawLine(ap, ax1, wy0 + 12f, ax1 - dir * 6f, wy0 + 17f); }
			using (Pen sp = new Pen(Color.FromArgb(55, 255, 255, 255), 1.6f)) for (int k = 0; k < 6; k++) { float px0 = (float)((tm * (60 + 160 * wk) * dir + k * 83f) % 440f); if (px0 < 0) px0 += 440f; px0 -= 20f; float py0 = 130f + k * 52f + (float)Math.Sin(k * 2.3) * 12f; g.DrawLine(sp, px0, py0, px0 + dir * (14f + 24f * wk), py0); }
		}
		// 조준선
		{   // 포트리스식 조준: 각도(마우스 위치 · ← →)와 힘(꾹 누르는 동안 차오르는 게이지)
			float gx = 120f, gy = 574f, gw = 262f, gh = 14f;
			using (GraphicsPath gp0 = Gfx.Round(new RectangleF(gx - 3, gy - 3, gw + 6, gh + 6), 8)) using (SolidBrush bb = new SolidBrush(Color.FromArgb(160, 10, 14, 30))) g.FillPath(bb, gp0);
			float fillv = charging ? gv : 0f;
			if (fillv > 0.005f) using (GraphicsPath fp = Gfx.Round(new RectangleF(gx, gy, Math.Max(6f, gw * fillv), gh), 6)) using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(gx, gy, gw, gh), Color.FromArgb(255, 90, 190, 255), Color.FromArgb(255, 255, 110, 80), 0f)) { GraphicsState fs = g.Save(); g.SetClip(fp); g.FillRectangle(lb, gx, gy, gw, gh); g.Restore(fs); }
			if (lastPw >= 0f) { float lx = gx + gw * lastPw; using (SolidBrush lbr = new SolidBrush(Color.FromArgb(230, 255, 232, 150))) g.FillPolygon(lbr, new PointF[] { new PointF(lx - 4f, gy - 8f), new PointF(lx + 4f, gy - 8f), new PointF(lx, gy - 1f) }); using (Pen lp = new Pen(Color.FromArgb(150, 255, 232, 150), 1.4f)) g.DrawLine(lp, lx, gy - 1f, lx, gy + gh); }   // 지난 슛의 힘 위치
			Txt(g, charging ? "힘 " + (int)Math.Round(gv * 100f) + "%" : "힘", 9f, 1, Color.FromArgb(235, 255, 255, 255), gx - 44f, gy - 2f, 40f, gh + 4f, StringAlignment.Far);
			Txt(g, sx < 150f ? "3점 거리 +1" : "2점 거리", 8.5f, 1, Color.FromArgb(220, sx < 150f ? 255 : 200, sx < 150f ? 214 : 220, sx < 150f ? 90 : 255), sx - 40f, SY - 62f, 80f, 14f, StringAlignment.Center);
			if (!flying)
			{   // 각도 화살표
				float ar = angD * (float)Math.PI / 180f, ax0 = sx + (float)Math.Cos(ar) * 26f, ay0 = SY - (float)Math.Sin(ar) * 26f, ax1 = sx + (float)Math.Cos(ar) * 70f, ay1 = SY - (float)Math.Sin(ar) * 70f;
				using (Pen apn = new Pen(Color.FromArgb(220, 255, 232, 150), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, DashStyle = DashStyle.Dash }) g.DrawLine(apn, ax0, ay0, ax1, ay1);
				using (SolidBrush hb = new SolidBrush(Color.FromArgb(240, 255, 232, 150))) g.FillPolygon(hb, new PointF[] { new PointF(ax1 + (float)Math.Cos(ar) * 9f, ay1 - (float)Math.Sin(ar) * 9f), new PointF(ax1 - (float)Math.Sin(ar) * 6f, ay1 - (float)Math.Cos(ar) * 6f), new PointF(ax1 + (float)Math.Sin(ar) * 6f, ay1 + (float)Math.Cos(ar) * 6f) });
				Txt(g, (int)Math.Round(angD) + "°", 10.5f, 2, Color.White, ax1 - 20f + (float)Math.Cos(ar) * 22f, ay1 - 10f - (float)Math.Sin(ar) * 16f, 40f, 20f, StringAlignment.Center);
			}
			if (lastPath.Count > 2) using (SolidBrush lb2 = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) for (int k = 0; k < lastPath.Count; k += 2) g.FillEllipse(lb2, lastPath[k].X - 2f, lastPath[k].Y - 2f, 4f, 4f);   // 지난 슛이 날아간 길 (감 잡는 데 써요)
			if (!charging && !flying && resetT <= 0f) Hint(g, "마우스로 각도 · 꾹 눌러 힘 모으고 놓기", 540f, 10f, false, 1f);
		}
		// 공
		bool ps = Spr.Get("ms_bk_s0") != null && Spr.Get("ms_bk_s1") != null && Spr.Get("ms_bk_s2") != null; bool showBall = !ps || flying;
		float ox = flying ? bx : sx, oy = flying ? by : SY;
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, sx - 16, 556, 32, 8);
		if (streak >= 3) { float pulse = 0.7f + 0.3f * (float)Math.Sin(tm * 14); using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)(120 * pulse), 255, 120, 40))) g.FillEllipse(fb, ox - R * 1.9f, oy - R * 1.9f, R * 3.8f, R * 3.8f); using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)(150 * pulse), 255, 220, 80))) g.FillEllipse(fb, ox - R * 1.3f, oy - R * 1.3f, R * 2.6f, R * 2.6f); }
		if (flying) for (int k = 4; k >= 1; k--) using (SolidBrush tb = new SolidBrush(Color.FromArgb(34 - k * 6, 255, 160, 70))) { float tx = ox - bvx * 0.011f * k, ty = oy - bvy * 0.011f * k, tr = R * (1f - k * 0.12f); g.FillEllipse(tb, tx - tr, ty - tr, tr * 2, tr * 2); }
		bool bsp2 = !showBall || Spr.Draw(g, "bball", ox, oy, R * 2.15f, R * 2.15f, flying ? (float)(tm * 420.0) : 0f); if (!bsp2) using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 140, 50))) g.FillEllipse(b, ox - R, oy - R, R * 2, R * 2);
		if (!bsp2) using (Pen p = new Pen(Color.FromArgb(120, 60, 20), 1.6f)) { g.DrawEllipse(p, ox - R, oy - R, R * 2, R * 2); g.DrawLine(p, ox - R, oy, ox + R, oy); g.DrawLine(p, ox, oy - R, ox, oy + R); }
		if (ps) { string pn = flying ? (lT < 0.2f ? "ms_bk_s1" : "ms_bk_s2") : "ms_bk_s0"; Bitmap pbm = Spr.Get(pn); float psq = charging ? 1f - 0.07f * gv : 1f; float ph2 = 130f * psq; float pw2 = ph2 * pbm.Width / pbm.Height; float bob = flying ? 0f : (float)Math.Sin(tm * 3) * 1.2f; float lift = flying && lT < 0.45f ? -18f * (float)Math.Sin(lT / 0.45f * 3.14f) : 0f; Spr.Draw(g, pn, sx - 35f, 550f - ph2 / 2f + bob + lift, pw2, ph2, 0f); }
		else DrawSpr(g, "m_bk" + (flying ? 5 : (charging ? 4 : 2)), sx - 58f, 576, 100f, (float)Math.Sin(tm * 3) * 1.5f);
		if (popT > 0) Txt(g, pop, 18f, 2, Color.FromArgb((int)(255 * Math.Min(1f, popT)), 255, 226, 120), 0, 350, W, 32, StringAlignment.Center);
		if (streak >= 3) Txt(g, "ON FIRE! 보너스 +1", 11f, 2, Color.FromArgb(255, 150, 70), 14, 66, 200, 22, StringAlignment.Near);
		if (streak >= 2) Txt(g, streak + "연속 성공!", 11f, 1, Color.FromArgb(255, 214, 90), 14, 44, 180, 22, StringAlignment.Near);
	}
}

// ── 14. 볼링 ────────────────────────────────────────────────
// 공 뒤에서 레인을 바라보는 시점(마리오 파티·Wii 볼링처럼). 위치 → 누른 채 좌우로 끌어 '회전' → 힘 게이지 타이밍에 놓기.
// 회전을 주면 공이 뒤쪽에서 휘어요. 공이 핀에 다가가면 카메라가 따라가고, 핀들은 서로 부딪혀 쓰러져요.
internal sealed class BowlGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class Pin { public float X, Z, Vx, Vz, Fall, Dir = 1f, Fade = 1f, Rot, Hop; public bool Down; }
	private readonly List<Pin> pins = new List<Pin>();
	private const float HalfW = 0.525f, LaneLen = 9f, PR = 0.085f, BR = 0.095f, PH = 0.62f, Hk = 3.4f;
	// 배경 그림(레인이 그려진 것)이 있으면 카메라를 고정하고 그 그림의 레인 모양에 투영을 맞춰요 (그림 위에 핀·공만 올려요)
	private static readonly bool B2 = Spr.Get("bg_bowl2") != null;
	private static readonly float CamY = B2 ? 0.911f : 1.9f, FL = B2 ? 1884f : 440f, HorY = B2 ? 25.8f : 180f, CamNear = B2 ? -4.95f : -2.5f, CamFar = B2 ? -4.95f : 3.9f;
	private float angV, spinDir; private float TanA { get { return angV * 0.010f; } }
	private float wob, hookK = 1f, bx, bz, bvx, bvz, spin, power, rollX, drift, chargeT, pressX, cam = CamNear, roll, settle, msgT, ballFade = 1f; private int state, frame, throwNo, downBefore; private bool wasHold, gutter; private string msg = "";
	public override string Costume { get { return "여름"; } }
	public override string Name { get { return "볼링"; } }
	public override string Key { get { return "bowl"; } }
	public override string Desc { get { return "위치·회전·힘, 세 번 클릭!"; } }
	public override string Help { get { return "클릭 세 번으로 던져요\n① 마우스를 좌우로 움직여 공 위치를 잡고 클릭\n② 회전 막대가 왔다 갔다 해요 → 클릭한 만큼 공이 살짝 휘어요 (가운데 = 직진)\n③ 힘 게이지가 오르내려요 → 초록 구간에서 클릭 (약하면 핀까지 못 가고, 너무 세면 흔들려요)\n1번 핀과 옆 핀 사이 포켓을 노리면 스트라이크! · 5프레임 · 스트라이크 +10 · 스페어 +5"; } }
	public override string RightText { get { return frame + "/5 프레임 · " + throwNo + "구"; } }
	public override void Reset() { Score = 0; Over = false; frame = 1; throwNo = 1; wasHold = false; msg = ""; msgT = 0; cam = CamNear; SetPins(); ResetBall(); Fx.Clear(); }
	public override void Icon(Graphics g, float cx2, float cy2)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 70, 150))) g.FillEllipse(b, cx2 - 16, cy2 - 4, 22, 22);
		using (SolidBrush b = new SolidBrush(Color.White)) { g.FillEllipse(b, cx2 + 4, cy2 - 18, 9, 14); g.FillEllipse(b, cx2 + 14, cy2 - 14, 9, 14); }
	}
	private void SetPins()
	{
		pins.Clear();
		for (int row = 0; row < 4; row++) for (int i = 0; i <= row; i++) pins.Add(new Pin { X = (i - row / 2f) * (B2 ? 0.24f : 0.42f), Z = LaneLen + row * 0.44f });
	}
	private void ResetBall() { bx = 0f; bz = 0f; bvx = bvz = 0f; state = 0; gutter = false; settle = 0; spin = 0; power = 0; ballFade = 1f; roll = 0; rollX = 0f; drift = 0f; }
	private int DownCount() { int n = 0; foreach (Pin p in pins) if (p.Down) n++; return n; }
	private PointF Pr(float x, float y, float z) { float k = FL / Math.Max(0.35f, z - cam); return new PointF(200f + k * x, HorY + k * (CamY - y)); }
	private float Sc(float z) { return FL / Math.Max(0.35f, z - cam); }
	// 배경 그림의 핀 자리는 실제 거리보다 깊이가 늘어나 보이게 그려져 있어서, 핀과 공이 같은 규칙으로 세로 위치를 옮겨 그려요 (그래야 공이 핀에 닿는 순간 그림도 닿아요)
	private float Wy(float z) { if (!B2) return 0f; float y0 = Pr(0f, 0f, 0f).Y, yL = Pr(0f, 0f, LaneLen).Y, den = yL - y0; if (Math.Abs(den) < 1f) return 0f; return (Pr(0f, 0f, z).Y - y0) * (38f / den); }   // 길 전체를 같은 비율로 눌러 맞춰서 중간에 꺾이거나 내려가는 느낌이 없어요
	private void Knock(Pin p, float impact)
	{
		if (p.Down || impact < 0.7f) return;
		p.Down = true; p.Dir = p.Vx >= 0 ? 1f : -1f; p.Hop = Math.Min(1f, 0.25f + impact * 0.07f); Sfx.Play("glass"); Shake = Math.Max(Shake, 0.1f); PointF q = Pr(p.X, 0.3f, p.Z); Fx.Burst(q.X, q.Y, Color.FromArgb(255, 236, 190), 3);
	}
	private void Hit(Pin a, Pin b, float vn) { Knock(a, vn * 0.78f); Knock(b, vn * 0.78f); }   // 핀끼리는 잘 안 넘어가요
	private static float Hook(float z) { float u = Math.Max(0f, Math.Min(1f, (z - 2.5f) / 6f)); return u * u * (3f - 2f * u); }   // 공은 앞에서는 곧게 가다가 뒤쪽에서 휘어요
	private void RollStep(ref float x, ref float z, ref float vx, ref float vz, float h, float sd, float hk, bool free)
	{
		float ob0 = vz; vz = Math.Max(0f, vz - 2.2f * h); vz *= 1f - 0.06f * h; if (ob0 > 0.01f) vx *= vz / ob0;
		if (!free)
		{
			float ex = Math.Abs(x) - 0.20f; float slow = Clamp((6.5f - vz) / 4f, 0f, 1f); if (ex > 0f && slow > 0f) vx += (x < 0 ? -1f : 1f) * ex * 10f * slow * h;   // 가장자리 경사
			if (vz > 0.6f) vx += sd * 0.75f * hk * Hook(z) * h * Math.Min(1f, vz / 6f);
		}
		x += vx * h; z += vz * h;
	}
	private float Smooth(float z) { return Clamp((z - 1.2f) / 5f, 0f, 1f); }
	public override void Update(float dt, GameInput inp, double t)
	{
		if (msgT > 0) msgT -= dt; bool press = !wasHold && inp.Hold;
		float camT = state == 1 ? CamNear + (CamFar - CamNear) * Clamp((bz - 1.2f) / 3.8f, 0f, 1f) : (state == 2 ? CamFar : CamNear);
		cam += (camT - cam) * Math.Min(1f, dt * (state == 2 ? 3f : 5f));
		if (state == 0)
		{
			float k0 = FL / (0f - CamNear);
			if (inp.MouseMoved) bx = (inp.Mx - 200f) / k0; if (inp.L) bx -= 0.8f * dt; if (inp.R) bx += 0.8f * dt; bx = Clamp(bx, -0.39f, 0.39f);
			if (press && cam < CamNear + 0.3f) { state = 6; chargeT = 0; angV = 0f; Sfx.Play("tick"); }
		}
		else if (state == 6)
		{
			chargeT += dt; float ua = (chargeT * 0.75f) % 2f; angV = (ua < 1f ? ua : 2f - ua) * 2f - 1f;
			if (press) { state = 5; chargeT = 0; Sfx.Play("tick"); }
		}
		else if (state == 5)
		{
			chargeT += dt; float u = (chargeT * 1.15f) % 2f; power = u < 1f ? u : 2f - u;
			if (press) { float sp = 5.5f + power * 6f; bvz = sp; float over = Math.Max(0f, power - 0.78f) / 0.22f, under = Math.Max(0f, 0.35f - power) / 0.35f; wob = ((float)Rnd.NextDouble() * 2f - 1f) * (0.06f + frame * 0.012f + over * 0.30f); bvx = bvz * TanA + wob * 0.3f; spinDir = angV; drift = ((float)Rnd.NextDouble() * 2f - 1f) * (0.10f + Math.Max(0f, 0.6f - power) / 0.6f * 1.2f + over * 0.25f + frame * 0.01f); hookK = (1.15f - power * 0.55f + under * 0.5f) * (0.88f + (float)Rnd.NextDouble() * 0.24f); state = 1; downBefore = DownCount(); settle = 0; Sfx.Play("swing"); }
		}
		wasHold = inp.Hold;
		if (state != 1 && state != 2) return;
		int n = 8; float h = dt / n;
		for (int s = 0; s < n; s++)
		{
			if (state == 1)
			{
				RollStep(ref bx, ref bz, ref bvx, ref bvz, h, gutter ? 0f : spinDir, hookK, gutter); roll += bvz * h / BR; rollX += bvx * h / BR; if (!gutter) bvx += drift * h * (8f / (bvz + 2f));

				if (!gutter && Math.Abs(bx) > HalfW - 0.01f) { gutter = true; bvx = 0f; msg = "거터! 공이 홈으로 빠졌어요"; msgT = 1.5f; Sfx.Play("glass"); }
				if (gutter) { float tx = bx < 0 ? -HalfW - 0.1f : HalfW + 0.1f; bx += (tx - bx) * Math.Min(1f, h * 14f); }   // 거터로는 순간이동 없이 부드럽게 빠져요
				if (!gutter) foreach (Pin p in pins) BallHit(p);
				if (bz > LaneLen + 2.3f || bvz < 0.4f) state = 2;
			}
			for (int i = 0; i < pins.Count; i++)
			{
				Pin a = pins[i]; a.X += a.Vx * h; a.Z += a.Vz * h;
				for (int j = i + 1; j < pins.Count; j++) PinHit(a, pins[j]);
			}
		}
		float fr = (float)Math.Exp(-2.6f * dt);
		foreach (Pin p in pins)
		{
			p.Vx *= fr; p.Vz *= fr; if (p.Down) { p.Rot = Clamp(p.Rot + p.Vx * dt * 45f, -35f, 35f); p.Fall = Math.Min(1f, p.Fall + dt * 2.3f); if (p.Fall >= 1f) p.Fade = Math.Max(0.4f, p.Fade - dt * 0.35f); }
			if (Math.Abs(p.X) > HalfW + 0.12f || p.Z > LaneLen + 1.6f) { if (!p.Down) { p.Down = true; p.Dir = p.X >= 0 ? 1f : -1f; } }
		}
		if (state == 2) { ballFade = Math.Max(0f, ballFade - dt * 1.2f); settle += dt; bool still = true; foreach (Pin p in pins) if (Math.Abs(p.Vx) + Math.Abs(p.Vz) > 0.25f) still = false; if (settle > 1.0f && (still || settle > 3.2f)) EndThrow(); }
	}
	private void BallHit(Pin p)
	{
		float dx = p.X - bx, dz = p.Z - bz, d = (float)Math.Sqrt(dx * dx + dz * dz), rr = BR + PR; if (d >= rr || d < 0.0001f) return;
		float nx = dx / d, nz = dz / d, rvn = (p.Vx - bvx) * nx + (p.Vz - bvz) * nz; float m1 = 5.2f, m2 = 1.2f, push = rr - d;
		bx -= nx * push * m2 / (m1 + m2); bz -= nz * push * m2 / (m1 + m2); p.X += nx * push * m1 / (m1 + m2); p.Z += nz * push * m1 / (m1 + m2);
		if (rvn < 0f) { float j = -1.45f * rvn / (1f / m1 + 1f / m2); bvx -= j * nx / m1; bvz -= j * nz / m1; p.Vx += j * nx / m2; p.Vz += j * nz / m2; Knock(p, -rvn); }
	}
	private void PinHit(Pin a, Pin b)
	{
		float dx = b.X - a.X, dz = b.Z - a.Z, d = (float)Math.Sqrt(dx * dx + dz * dz), rr = PR * 2f; if (d >= rr || d < 0.0001f) return;
		float nx = dx / d, nz = dz / d, rvn = (b.Vx - a.Vx) * nx + (b.Vz - a.Vz) * nz, push = (rr - d) / 2f;
		a.X -= nx * push; a.Z -= nz * push; b.X += nx * push; b.Z += nz * push;
		if (rvn < 0f) { float j = -1.7f * rvn / 2f; a.Vx -= j * nx; a.Vz -= j * nz; b.Vx += j * nx; b.Vz += j * nz; Hit(a, b, -rvn); }
	}
	private void EndThrow()
	{
		int total = DownCount(), knocked = total - downBefore; Score += knocked;
		bool strike = throwNo == 1 && total >= 10, spare = throwNo == 2 && total >= 10;
		if (strike) { Score += 10; msg = "스트라이크! 민원 일괄 처리 +10"; Sfx.Play("fanfare"); Fx.Burst(200f, 300f, Color.FromArgb(255, 214, 80), 18); Shake = 0.7f; Fx.Burst(120f, 260f, Color.FromArgb(255, 120, 90), 10); Fx.Burst(280f, 260f, Color.FromArgb(120, 200, 255), 10); }
		else if (spare) { Score += 5; msg = "스페어! 잔여 민원 처리 +5"; Sfx.Play("ok"); }
		else if (knocked == 0) msg = gutter ? "거터… 0개" : "0개"; else msg = (gutter ? "거터 · " : "") + knocked + "개 쓰러뜨렸어요 +" + knocked;
		msgT = 1.6f;
		if (strike || throwNo == 2) { frame++; if (frame > 5) { Over = true; return; } throwNo = 1; SetPins(); }
		else { throwNo = 2; pins.RemoveAll(p => p.Down); }
		ResetBall();
	}
	private void Quad(Graphics g, Brush b, float x0, float x1, float z0, float z1, float y)
	{
		g.FillPolygon(b, new PointF[] { Pr(x0, y, z0), Pr(x1, y, z0), Pr(x1, y, z1), Pr(x0, y, z1) });
	}
	private readonly Dictionary<long, Bitmap> pinBmp = new Dictionary<long, Bitmap>();
	private Bitmap PinBitmap(float h, float w, float dk)
	{
		long key = ((long)Math.Round(h * 2f) << 20) | (long)(int)(dk * 100f); Bitmap b; if (pinBmp.TryGetValue(key, out b)) return b;
		if (pinBmp.Count > 90) { foreach (Bitmap o in pinBmp.Values) o.Dispose(); pinBmp.Clear(); }
		float lw = 2f * w + 4f, lh = h + 6f; b = new Bitmap(Math.Max(4, (int)Math.Ceiling(lw * dk)), Math.Max(4, (int)Math.Ceiling(lh * dk)), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(b))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(b.Width / lw, b.Height / lh); g.TranslateTransform(w + 2f, h + 2f);
			float[,] prof = { { 0f, 0.55f }, { 0.08f, 0.78f }, { 0.28f, 1.0f }, { 0.44f, 0.86f }, { 0.60f, 0.46f }, { 0.72f, 0.32f }, { 0.82f, 0.42f }, { 0.93f, 0.50f }, { 1.0f, 0.30f } };
			List<PointF> pts = new List<PointF>(); for (int i = 0; i < prof.GetLength(0); i++) pts.Add(new PointF(prof[i, 1] * w, -prof[i, 0] * h)); for (int i = prof.GetLength(0) - 1; i >= 0; i--) pts.Add(new PointF(-prof[i, 1] * w, -prof[i, 0] * h));
			using (SolidBrush br = new SolidBrush(Color.FromArgb(248, 248, 252))) g.FillClosedCurve(br, pts.ToArray(), FillMode.Winding, 0.3f);
			using (SolidBrush br = new SolidBrush(Color.FromArgb(85, 120, 130, 170))) g.FillClosedCurve(br, new PointF[] { new PointF(w * 0.25f, -h * 0.02f), new PointF(w * 0.9f, -h * 0.28f), new PointF(w * 0.4f, -h * 0.62f), new PointF(w * 0.2f, -h * 0.9f), new PointF(w * 0.25f, -h * 0.5f) }, FillMode.Winding, 0.3f);
			using (SolidBrush br = new SolidBrush(Color.FromArgb(214, 60, 70))) { g.FillRectangle(br, -w * 0.36f, -h * 0.70f, w * 0.72f, h * 0.035f); g.FillRectangle(br, -w * 0.38f, -h * 0.76f, w * 0.76f, h * 0.035f); }
			using (Pen o = new Pen(Color.FromArgb(85, 60, 60, 90), 1f)) g.DrawClosedCurve(o, pts.ToArray(), 0.3f, FillMode.Winding);
		}
		pinBmp[key] = b; return b;
	}
	private void DrawPin(Graphics g, Pin p)
	{
		if (p.Z - cam < 0.5f) return;
		PointF b = Pr(p.X, 0f, p.Z); b.Y += Wy(p.Z); float k = Sc(p.Z) * (B2 ? 0.72f : 1f), h = PH * k, w = 0.085f * k; int al = (int)(255 * p.Fade); if (al <= 2) return;
		h = (float)Math.Round(h * 2f) / 2f; w = h * (0.085f / PH);   // 높이를 0.5칸 단위로 맞춰서 그림을 재사용해요 (모양은 그대로)
		GraphicsState st = g.Save(); g.TranslateTransform(b.X, b.Y);
		if (al >= 250 && Spr.Get("ms_bwp0") != null)   // 표정 있는 볼링핀: 서 있음 → 기울어짐 → 누움
		{
			int fi = p.Fall <= 0f ? 0 : (p.Fall < 0.4f ? 1 : (p.Fall < 0.75f ? 2 : 3)); Bitmap b0 = Spr.Get("ms_bwp0"); float ph = h + 4f, pw = ph * b0.Width / b0.Height;
			using (SolidBrush sh0 = new SolidBrush(Color.FromArgb(al / 4, 0, 0, 0))) g.FillEllipse(sh0, -w * 1.1f, -h * 0.03f, w * 2.2f, h * 0.07f);
			float hopY = p.Hop * 4f * Math.Min(1f, p.Fall * 1.3f) * (1f - Math.Min(1f, p.Fall * 1.3f)) * h * 0.55f; if (p.Fall > 0f && p.Fall < 0.75f) { g.TranslateTransform(0f, 0f); g.RotateTransform(p.Dir * 7f * (float)Math.Sin(p.Fall / 0.75f * 3.14159f)); }   // 맞는 순간 핀이 살짝 튀어올라요
			g.TranslateTransform(0f, -hopY); if (p.Fall >= 0.75f && p.Rot != 0f) { g.TranslateTransform(0f, -h * 0.2f); g.RotateTransform(p.Rot); g.TranslateTransform(0f, h * 0.2f); }   // 누운 핀은 미끄러지며 살짝 돌아요
			if (p.Dir < 0f) g.ScaleTransform(-1f, 1f);
			Spr.Draw(g, "ms_bwp" + fi, 0f, -h * 0.5f + 2f, pw, ph, 0f); g.Restore(st); return;
		}
		if (p.Fall > 0f) g.RotateTransform(p.Dir * 84f * p.Fall * p.Fall * (3f - 2f * p.Fall));
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(al / 4, 0, 0, 0))) g.FillEllipse(sh, -w * 1.1f, -h * 0.03f, w * 2.2f, h * 0.07f);
		if (al >= 250 && Spr.Draw(g, "ms_m_bw_pin", 0f, -h * 0.5f + 2f, Math.Max(3f, h * 0.41f), h + 4f, 0f)) { g.Restore(st); return; }
		Bitmap sp = PinBitmap(h, w, ImgCache.Scale(g)); RectangleF dr = new RectangleF(-w - 2f, -h - 2f, 2f * w + 4f, h + 6f); InterpolationMode im0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear;
		if (al >= 250) g.DrawImage(sp, dr, new RectangleF(0, 0, sp.Width, sp.Height), GraphicsUnit.Pixel);
		else using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes()) { ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] { new float[] { 1, 0, 0, 0, 0 }, new float[] { 0, 1, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 }, new float[] { 0, 0, 0, al / 255f, 0 }, new float[] { 0, 0, 0, 0, 1 } })); g.DrawImage(sp, Rectangle.Round(dr), 0, 0, sp.Width, sp.Height, GraphicsUnit.Pixel, ia); }
		g.InterpolationMode = im0; g.Restore(st);
	}
	private static readonly float[][] BallSpots = { new float[] { -0.35f, 0.25f, 0.10f, 0 }, new float[] { 0.02f, 0.38f, 0.10f, 0 }, new float[] { -0.12f, -0.08f, 0.10f, 0 }, new float[] { -1.0f, 0.9f, 0.16f, 1 }, new float[] { 0.9f, 1.7f, 0.2f, 1 }, new float[] { 0.1f, 2.9f, 0.18f, 1 }, new float[] { -0.8f, 3.9f, 0.2f, 1 }, new float[] { 0.7f, 4.6f, 0.16f, 1 }, new float[] { -0.2f, 5.6f, 0.2f, 1 }, new float[] { 1.3f, 3.2f, 0.18f, 1 }, new float[] { -1.4f, 2.0f, 0.18f, 1 } };
	// 공 표면의 구멍·무늬를 구 위에 얹어서 굴러가는 방향으로 돌려요 (평면 회전이 아니라 앞으로 구르는 느낌)
	private void DrawBallSpots(Graphics g, PointF c, float r, int al)
	{
		foreach (float[] sp in BallSpots)
		{
			float lam = sp[0] + rollX * 0.7f, psi = sp[1] + roll; float cl = (float)Math.Cos(lam), cp = (float)Math.Cos(psi); float vz = cl * cp; if (vz < 0.12f) continue;
			float x = c.X + r * (float)Math.Sin(lam) * cp * 0.96f, y = c.Y - r * (float)Math.Sin(psi) * 0.96f, w = sp[2] * r * 2f; float ew = w * (0.55f + 0.45f * Math.Abs(cl)), eh = w * vz; int a2 = (int)(al * Math.Min(1f, (vz - 0.12f) * 3f));
			Color col = sp[3] > 0 ? Color.FromArgb(a2 * 110 / 255, 235, 200, 255) : Color.FromArgb(a2, 14, 8, 30);
			using (SolidBrush b = new SolidBrush(col)) g.FillEllipse(b, x - ew / 2f, y - eh / 2f, ew, eh);
		}
	}
	private void DrawBall(Graphics g, float x, float z, float y)
	{
		if (z - cam < 0.5f) return; PointF c = Pr(x, y, z); float k = Sc(z) * (B2 ? 0.72f : 1f) * (B2 ? 0.095f / BR : 1f) * (B2 ? 1.37f : 1f), r = BR * k; int al = (int)(255 * ballFade); if (al <= 4) return;
		PointF sh = Pr(x, 0f, z); sh.Y += Wy(z); if (B2) c.Y = sh.Y - y * k; using (SolidBrush b = new SolidBrush(Color.FromArgb(70 * al / 255, 0, 0, 0))) g.FillEllipse(b, sh.X - r * 0.95f, sh.Y - r * 0.28f, r * 1.9f, r * 0.56f);
		{ float fs = 0.35f + 0.65f * ballFade; r *= fs; }
		using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(c.X - r, c.Y - r, r * 2, r * 2); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(c.X - r * 0.35f, c.Y - r * 0.4f); pb.CenterColor = Color.FromArgb(al, 190, 130, 255); pb.SurroundColors = new Color[] { Color.FromArgb(al, 62, 24, 130) }; g.FillPath(pb, gp); } }
		DrawBallSpots(g, c, r, al);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(110 * al / 255, 255, 255, 255))) g.FillEllipse(b, c.X - r * 0.55f, c.Y - r * 0.75f, r * 0.55f, r * 0.38f);
	}
	private bool predGutter;
	private List<PointF> Predict(float ta, float sd)
	{
		// 실제 공과 같은 규칙으로 미리 굴려 봐서 (힘은 중간 정도로 가정) 휘어지는 길까지 그대로 보여줘요
		List<PointF> r = new List<PointF>(); float x = bx, z = 0f, vz = 5.5f + 0.62f * 6f, vx = vz * ta, h = 1f / 90f, nextZ = 0f; r.Add(AimPt(x, 0f));
		predGutter = false; for (int i = 0; i < 900 && z < LaneLen - 0.5f && vz > 0.6f; i++)
		{
			RollStep(ref x, ref z, ref vx, ref vz, h, sd, 0.85f, false); if (Math.Abs(x) > HalfW - 0.01f) { predGutter = true; r.Add(AimPt(x < 0 ? -HalfW + 0.01f : HalfW - 0.01f, z)); break; }
			if (z >= nextZ + 0.25f) { nextZ = z; r.Add(AimPt(x, z)); }
		}
		return r;
	}
	private PointF AimPt(float x, float z) { PointF pp = Pr(x, 0.01f, z); pp.Y += Wy(z); return pp; }
	public override void Draw(Graphics g, double tm)
	{
		// 볼링장 벽·천장
		BgImg(g, Spr.Get("bg_bowl2") != null ? "bg_bowl2" : "bg_bowl", Color.FromArgb(18, 14, 44), Color.FromArgb(70, 38, 110));
		bool b2 = Spr.Get("bg_bowl2") != null;
		if (!b2) for (int i = 0; i < 7; i++) { float lx = 30 + i * 56; using (SolidBrush b = new SolidBrush(Color.FromArgb(60 + (i % 2) * 40, 255, 220, 160))) g.FillEllipse(b, lx - 14, 18 + (i % 2) * 10, 28, 8); }
		if (!b2) using (Pen p = new Pen(Color.FromArgb(120, 255, 90, 160), 3f)) g.DrawLine(p, 60, 120, 340, 120);
		if (!b2) using (Pen p = new Pen(Color.FromArgb(90, 90, 200, 255), 3f)) g.DrawLine(p, 90, 134, 310, 134);
		if (!b2) using (GraphicsPath cp = new GraphicsPath()) { PointF[] cq = { Pr(-3f, 0f, LaneLen + 4.2f), Pr(3f, 0f, LaneLen + 4.2f), Pr(3f, 2.4f, LaneLen + 4.2f), Pr(-3f, 2.4f, LaneLen + 4.2f) }; cp.AddPolygon(cq); using (LinearGradientBrush lb = new LinearGradientBrush(cp.GetBounds(), Color.FromArgb(120, 30, 70), Color.FromArgb(40, 20, 70), 90f)) g.FillPath(lb, cp); }
		Txt(g, "STRIKE!", 20f, 2, Color.FromArgb(150 + (int)(90 * Math.Sin(tm * 4)), 255, 120, 190), 0, 56, W, 34, StringAlignment.Center);
		float zA = cam + 0.6f, zB = LaneLen + 4.2f;
		if (!b2) using (SolidBrush b = new SolidBrush(b2 ? Color.FromArgb(34, 40, 58) : Color.FromArgb(36, 22, 58))) Quad(g, b, -4f, 4f, zA, zB, 0f);          // 바닥
		if (!b2) using (SolidBrush b = new SolidBrush(b2 ? Color.FromArgb(52, 58, 74) : Color.FromArgb(24, 24, 36))) { Quad(g, b, -HalfW - 0.2f, -HalfW, zA, LaneLen + 2.2f, -0.02f); Quad(g, b, HalfW, HalfW + 0.2f, zA, LaneLen + 2.2f, -0.02f); }   // 거터
		if (!b2) using (SolidBrush b = new SolidBrush(b2 ? Color.FromArgb(236, 186, 112) : Color.FromArgb(214, 170, 108))) Quad(g, b, -HalfW, HalfW, zA, LaneLen + 1.5f, 0f);       // 레인
		if (!b2) using (SolidBrush b = new SolidBrush(b2 ? Color.FromArgb(30, 34, 46) : Color.FromArgb(12, 8, 18))) Quad(g, b, -HalfW - 0.2f, HalfW + 0.2f, LaneLen + 1.5f, zB, 0f);      // 핀 뒤 구덩이
		if (!b2) using (Pen p = new Pen(Color.FromArgb(46, 120, 70, 30), 1f)) for (int i = 1; i < 10; i++) { float x = -HalfW + i * HalfW * 2f / 10f; g.DrawLine(p, Pr(x, 0f, zA), Pr(x, 0f, LaneLen + 1.5f)); }
		if (!b2) using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 255, 250, 235))) Quad(g, b, -HalfW * 0.34f, HalfW * 0.34f, zA, LaneLen + 1.5f, 0.001f);   // 기름 반사광
		if (!b2) using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) Quad(g, b, -HalfW * 0.12f, HalfW * 0.12f, zA, LaneLen + 1.5f, 0.001f);
		using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), 2f)) g.DrawLine(p, Pr(-HalfW, 0f, 0f), Pr(HalfW, 0f, 0f));   // 파울 라인
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 130, 80, 40))) for (int i = -3; i <= 3; i++) { float z0 = 3.1f + Math.Abs(i) * 0.25f, x0 = i * 0.12f; if (z0 - cam < 0.6f) continue; g.FillPolygon(b, new PointF[] { Pr(x0, 0f, z0 + 0.5f), Pr(x0 - 0.035f, 0f, z0), Pr(x0 + 0.035f, 0f, z0) }); }
		// 조준선
		if (state == 0 || state == 5 || state == 6)
		{
			List<PointF> path = Predict(state == 0 ? 0f : TanA, state == 0 ? 0f : angV);
			if (path.Count > 1) { PointF[] pa = path.ToArray(); int oa = state == 5 ? 235 : 170, ca = state == 5 ? 255 : 210; using (Pen po = new Pen(Color.FromArgb(oa, 255, 255, 255), 5.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) { if (pa.Length >= 3) g.DrawCurve(po, pa, 0.4f); else g.DrawLines(po, pa); } using (Pen pc = new Pen(Color.FromArgb(ca, 20, 90, 255), 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) { if (pa.Length >= 3) g.DrawCurve(pc, pa, 0.4f); else g.DrawLines(pc, pa); } using (SolidBrush dot = new SolidBrush(predGutter ? Color.FromArgb(ca, 235, 60, 60) : Color.FromArgb(ca, 255, 70, 140))) { PointF e = pa[pa.Length - 1]; g.FillEllipse(dot, e.X - 4f, e.Y - 4f, 8f, 8f); if (predGutter) Txt(g, "거터!", 9f, 2, Color.FromArgb(ca, 255, 120, 110), e.X - 30, e.Y - 20, 60, 14, StringAlignment.Center); } }   // 휘는 길: 하얀 테두리 + 파란 선 (바닥 나무색과 겹쳐도 잘 보여요)
		}
		// 공과 핀을 먼 것부터 그려요
		pins.Sort((a, b) => b.Z.CompareTo(a.Z));
		bool ballDrawn = false;
		foreach (Pin p in pins) { if (!ballDrawn && bz > p.Z) { DrawBall(g, bx, bz, BR); ballDrawn = true; } DrawPin(g, p); }
		if (!ballDrawn) DrawBall(g, bx, bz, BR);
		DrawMascot(g, 52, 598, 84, (float)Math.Sin(tm * 3) * 2);
		// 방향·힘 표시
		if (state == 6 || state == 5)
		{
			float sx0 = 100f, sw = 200f, sy0 = 548f; using (GraphicsPath gp = Gfx.Round(new RectangleF(sx0 - 6, sy0 - 8, sw + 12, 22), 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 10, 30))) g.FillPath(b, gp);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 90, 210, 120))) g.FillRectangle(b, sx0 + sw / 2 - 8, sy0 - 4, 16, 14);
			using (Pen p = new Pen(Color.FromArgb(170, 255, 255, 255), 1.5f)) g.DrawLine(p, sx0 + sw / 2, sy0 - 6, sx0 + sw / 2, sy0 + 12);
			using (SolidBrush b = new SolidBrush(state == 6 ? Color.FromArgb(255, 214, 90) : Color.FromArgb(200, 200, 210))) g.FillEllipse(b, sx0 + sw / 2 + angV * sw / 2 - 7, sy0 - 3, 14, 14);
			Txt(g, state == 6 ? "회전 — 원하는 만큼일 때 클릭 (가운데=직진)" : "회전 고정됨", 9f, 1, Color.FromArgb(230, 255, 255, 255), sx0 - 30, sy0 + 14, sw + 60, 16, StringAlignment.Center);
		}
		if (state == 5)
		{
			float gx = 360f, gy0 = 330f, gh = 190f; using (GraphicsPath gp = Gfx.Round(new RectangleF(gx - 8, gy0 - 6, 28, gh + 12), 8)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 10, 10, 30))) g.FillPath(b, gp);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 90, 210, 120))) g.FillRectangle(b, gx - 5, gy0 + gh * (1f - 0.78f), 22, gh * (0.78f - 0.35f));
			Color pc = power > 0.78f ? Color.FromArgb(255, 110, 90) : (power >= 0.35f ? Color.FromArgb(110, 230, 150) : Color.FromArgb(120, 170, 255));
			using (SolidBrush b = new SolidBrush(pc)) g.FillRectangle(b, gx, gy0 + gh * (1f - power), 12, gh * power);
			Txt(g, "힘", 10f, 1, Color.White, gx - 12, gy0 + gh + 4, 36, 18, StringAlignment.Center);
			Txt(g, "초록에서 클릭!", 9f, 1, Color.FromArgb(230, 255, 255, 255), gx - 60, gy0 - 24, 100, 16, StringAlignment.Center);
		}
		if (msgT > 0 && msg.StartsWith("스트라이크")) { float ma = Math.Min(1f, msgT); using (Pen rp = new Pen(Color.FromArgb((int)(60 * ma), 255, 214, 90), 14f)) for (int rk = 0; rk < 14; rk++) { double an = rk * Math.PI / 7 + tm * 0.8; g.DrawLine(rp, 200 + (float)Math.Cos(an) * 40, 248 + (float)Math.Sin(an) * 40, 200 + (float)Math.Cos(an) * 260, 248 + (float)Math.Sin(an) * 260); } }
		if (msgT > 0) Txt(g, msg, 20f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 226, 120), 0, 430, W, 36, StringAlignment.Center);
		if (state == 0 && frame == 1 && throwNo == 1 && cam < CamNear + 0.3f) Hint(g, "마우스로 위치 → 클릭 → 방향 → 클릭 → 힘 → 클릭", 478 + 11.0, 11f, false, 1f);
	}
}
// ── 16. 새총 ────────────────────────────────────────────────
// 공을 당겼다 놓아 날려요. 통나무와 '악성 민원'이 중력으로 굴러떨어지고 서로 부딪혀요. 충돌이 클수록 민원이 사라져요.
internal sealed class SlingGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class Bd { public float X, Y, Vx, Vy, R, M, Rot; public int Kind, Sk, Am; public bool Alive = true; public float Hit; }   // 0 공 · 1 통나무 · 2 민원
	private class Pt { public float X, Y, Vx, Vy, Age, Rot, Spin; public int Sp = -1; public Color C; }
	private readonly List<Bd> bs = new List<Bd>(); private readonly List<RectangleF> st = new List<RectangleF>(); private readonly List<int> wd = new List<int>(); private readonly List<Pt> pts = new List<Pt>();
	private const float SX = 76f, SY = 430f, Floor = 520f, Gv = 1150f, MaxPull = 90f;
	private readonly int[] stock = new int[6]; private int sel; private static readonly string[] AmN = { "", "관통탄", "바운스탄", "폭탄", "불꽃탄", "얼음탄" }; private float fxT2, fx2X, fx2Y; private bool impPend; private float impX, impY, deadT, fxT, fxX, fxY; private int impAm; private int heavy, splitCharge, waveShots; private bool splitUsed; private float snapT, relX, relY; private int shotKills; private Bd ball; private int shots, wave; private bool aiming, wasHold, flying; private float ax, ay, cx, cy, flyT, msgT, still; private string msg = "";
	public override string Costume { get { return "g_sling"; } }
	public override string Name { get { return "새총"; } }
	public override string Key { get { return "sling"; } }
	public override string Desc { get { return "새총으로 민원 탑을 무너뜨려요"; } }
	public override string Help { get { return "공을 뒤로 당겼다 놓으면 날아가요\n통나무가 굴러 부딪히면 민원도 쓰러져요\n공은 5발 · 민원을 다 없애면 다음 판 (적게 쏠수록 ★ 보너스)\n아래 특수탄을 눌러 고르고 쏘세요: 관통(민원 통과) · 바운스(튕김) · 폭탄 · 불꽃(나무 벽) · 얼음(파편)\nTNT 통을 맞히면 폭발! · 홀수 판 클리어 → 묵직한 공 · 2판부터 공중에서 클릭하면 분열"; } }
	public override string RightText { get { return "공 " + shots + "발"; } }
	public override void Reset() { heavy = 0; splitCharge = 0; for (int i = 0; i < 6; i++) stock[i] = 0; stock[1] = 1; stock[2] = 1; sel = 0; impPend = false; fxT = 0; Score = 0; Over = false; wave = 0; shots = 5; msg = ""; msgT = 0; wasHold = false; NewWave(); }
	public override void Icon(Graphics g, float cx2, float cy2)
	{
		using (Pen p = new Pen(Color.FromArgb(150, 100, 60), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, cx2 - 12, cy2 + 16, cx2 - 12, cy2 - 4); g.DrawLine(p, cx2 - 12, cy2 - 4, cx2 - 22, cy2 - 16); g.DrawLine(p, cx2 - 12, cy2 - 4, cx2 - 2, cy2 - 16); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 84, 92))) g.FillEllipse(b, cx2 + 4, cy2 - 8, 16, 16);
	}
	private Bd Make(int kind, float x, float y) { Bd b = new Bd { Kind = kind, X = x, Y = y, Sk = Rnd.Next(4) }; b.R = kind == 0 ? 12f : (kind == 1 ? 15f : (kind == 3 ? 16f : (kind == 4 ? 22f : 14f))); b.M = kind == 0 ? 3f : (kind == 1 ? 2f : (kind == 3 ? 2.6f : (kind == 4 ? 5f : 1.4f))); if (kind == 4) b.Sk = 3; return b; }
	private void NewWave()
	{
		bs.Clear(); st.Clear(); wd.Clear(); wave++; waveShots = 0; splitUsed = false;
		int d = wave <= 1 ? 0 : (wave <= 5 ? wave - 1 : 1 + (wave - 2) % 4);
		if (d == 0)
		{
			int n = Math.Min(3, 1 + wave); if (n < 2) n = 2;
			for (int i = 0; i < n; i++)
			{
				float x0 = 196f + i * 62f, h = 50f + (float)Rnd.NextDouble() * 110f; st.Add(new RectangleF(x0, Floor - h, 54f, h));
				bool target = i == n - 1 || i % 2 == 0;
				bs.Add(Make(target ? 2 : 1, x0 + 27f, Floor - h - (target ? 14f : 15f) - 1f));
			}
			bs.Add(Make(1, 160f, Floor - 16f));
		}
		else if (d == 1)   // 계단: 점점 높아지는 기둥
		{
			float[] hs = { 50f, 95f, 140f };
			for (int i = 0; i < 3; i++) { float x0 = 190f + i * 52f; st.Add(new RectangleF(x0, Floor - hs[i], 46f, hs[i])); bs.Add(Make(i == 1 ? 1 : 2, x0 + 23f, Floor - hs[i] - 15f)); }
			bs.Add(Make(1, 160f, Floor - 16f));
		}
		else if (d == 2)   // 벽 뒤에 숨은 민원: 위로 높이 쏴서 넘겨야 해요
		{
			st.Add(new RectangleF(206f, Floor - 230f, 26f, 230f)); st.Add(new RectangleF(262f, Floor - 60f, 64f, 60f));
			bs.Add(Make(2, 294f, Floor - 75f)); bs.Add(Make(2, 352f, Floor - 15f)); bs.Add(Make(1, 244f, Floor - 16f));
		}
		else if (d == 3)   // TNT 창고: 통을 맞히면 한꺼번에 터져요
		{
			st.Add(new RectangleF(196f, Floor - 70f, 48f, 70f)); st.Add(new RectangleF(300f, Floor - 110f, 48f, 110f));
			bs.Add(Make(2, 220f, Floor - 85f)); bs.Add(Make(3, 268f, Floor - 17f)); bs.Add(Make(3, 244f + 28f * 0f + 10f, Floor - 50f)); bs.Add(Make(2, 324f, Floor - 125f)); bs.Add(Make(2, 372f, Floor - 15f));
		}
		else   // 쌍탑: 탑 위와 두 탑 사이 틈의 민원
		{
			st.Add(new RectangleF(212f, Floor - 150f, 40f, 150f)); st.Add(new RectangleF(300f, Floor - 150f, 40f, 150f));
			bs.Add(Make(2, 232f, Floor - 165f)); bs.Add(Make(2, 320f, Floor - 165f)); bs.Add(Make(2, 276f, Floor - 15f)); bs.Add(Make(1, 232f, Floor - 197f)); bs.Add(Make(3, 372f, Floor - 17f));
		}
		for (int i = 0; i < st.Count; i++) wd.Add(0);
		if (wave >= 2 && st.Count >= 2) { int wi = d == 0 ? st.Count - 1 : (d == 3 ? 0 : 1); wd[wi] = 1; if (wave >= 3) { int oi = wi == 0 ? 1 : 0; wd[oi] = wave % 3 == 0 ? 2 : (wave % 3 == 1 ? 3 : 4); } }   // 기둥 재질: 나무·유리·얼음은 세게 맞으면 부서지고 철은 안 부서져요
		if (wave > 5) bs.Add(Make(2, 380f, Floor - 15f - 34f * (wave % 2)));
		if (wave % 5 == 0) bs.Add(Make(4, 362f, Floor - 24f));
		SpawnBall();
	}
	private void Boom(Bd t)
	{
		if (!t.Alive) return; t.Alive = false; Sfx.Play("thud_soft"); Shake = Math.Max(Shake, 0.5f); Fx.Burst(t.X, t.Y, Color.FromArgb(255, 170, 60), 18); Fx.Burst(t.X, t.Y, Color.FromArgb(255, 230, 120), 10);
		foreach (Bd o in bs)
		{
			if (!o.Alive || o == t) continue; float dx = o.X - t.X, dy = o.Y - t.Y, dd = (float)Math.Sqrt(dx * dx + dy * dy);
			if (dd < 110f) { float f = (1f - dd / 110f) * 520f / o.M; if (dd < 0.01f) dd = 1f; o.Vx += dx / dd * f; o.Vy += dy / dd * f - 120f / o.M; if (o.Kind == 3 && dd < 70f) Boom(o); else if (o.Kind == 2 && dd < 74f) Kill(o); }
		}
		msg = "펑! 민원 폭발"; msgT = 1.0f;
	}
	private void SpawnBall() { ball = Make(0, SX, SY); if (heavy > 0) { heavy--; ball.R = 15f; ball.M = 6.5f; ball.Sk = 9; } bs.Add(ball); flying = false; aiming = false; flyT = 0; still = 0; deadT = 0; }
	private void Kill(Bd b) { if (!b.Alive) return; b.Alive = false; if (b.Kind == 2) { shotKills++; if (shotKills >= 2) { Score += 50 * (shotKills - 1); msg = shotKills + "연속 격파! +" + 50 * (shotKills - 1); msgT = 1.3f; } Sfx.Play("glass"); Fx.Burst(b.X, b.Y, Color.FromArgb(255, 120, 130), 9); Shake = 0.3f; Score += 100; for (int i = 0; i < 14; i++) { double an = Rnd.NextDouble() * 6.28, sp = 80 + Rnd.NextDouble() * 200; pts.Add(new Pt { X = b.X, Y = b.Y, Vx = (float)(Math.Cos(an) * sp), Vy = (float)(Math.Sin(an) * sp - 90), C = i % 3 == 0 ? Color.White : Color.FromArgb(232, 84, 92) }); } } }
	private bool Pull(out float vx, out float vy)
	{
		float dx = ax - cx, dy = ay - cy, d = (float)Math.Sqrt(dx * dx + dy * dy); if (d > MaxPull) { dx *= MaxPull / d; dy *= MaxPull / d; d = MaxPull; }
		vx = dx * 9.6f; vy = dy * 9.6f; return d > 14f;
	}
	public override void Click(float x, float y)
	{
		if (y > 548f && !flying && !aiming) { for (int i = 0; i < 6; i++) { float ix = 118f + i * 40f; if (Math.Abs(x - ix) < 19f && (i == 0 || stock[i] > 0)) { sel = i; Sfx.Play("pluck"); } } return; }
		if (!flying || ball == null || !ball.Alive || flyT < 0.2f || splitUsed || splitCharge <= 0) return;
		splitUsed = true; splitCharge--; Sfx.Play("whoosh"); msg = "분열!"; msgT = 0.8f;
		for (int k = -1; k <= 1; k += 2) { Bd c = Make(0, ball.X, ball.Y); c.Vx = ball.Vx * (float)Math.Cos(0.28f * k) - ball.Vy * (float)Math.Sin(0.28f * k); c.Vy = ball.Vx * (float)Math.Sin(0.28f * k) + ball.Vy * (float)Math.Cos(0.28f * k); bs.Add(c); }
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (msgT > 0) msgT -= dt; if (fxT > 0f) fxT -= dt; if (fxT2 > 0f) fxT2 -= dt; if (snapT > 0f) snapT -= dt; cx = inp.Mx; cy = inp.My;
		if (!wasHold && inp.Hold && !flying && ball != null && ball.Alive) { float dd = (inp.Mx - SX) * (inp.Mx - SX) + (inp.My - SY) * (inp.My - SY); if (dd < 110f * 110f) { aiming = true; ax = SX; ay = SY; } }
		if (aiming)
		{
			float dx = cx - SX, dy = cy - SY, d = (float)Math.Sqrt(dx * dx + dy * dy); if (d > MaxPull) { dx *= MaxPull / d; dy *= MaxPull / d; }
			ball.X = SX + dx; ball.Y = SY + dy;
			if (!inp.Hold) { aiming = false; float vx, vy; ax = SX; ay = SY; cx = ball.X; cy = ball.Y; if (Pull(out vx, out vy)) { relX = ball.X - SX; relY = ball.Y - SY; snapT = 0.4f; shotKills = 0; splitUsed = false; waveShots++; if (sel > 0 && stock[sel] > 0) { ball.Am = sel; stock[sel]--; if (stock[sel] == 0) sel = 0; } deadT = 0; ball.Vx = vx; ball.Vy = vy; flying = true; shots--; Sfx.Play("pluck"); flyT = 0; still = 0; } else { ball.X = SX; ball.Y = SY; } }
		}
		wasHold = inp.Hold;
		// 물리
		int n = 4; float h = dt / n;
		for (int s = 0; s < n; s++)
		{
			foreach (Bd b in bs)
			{
				if (!b.Alive || (b == ball && !flying)) continue;
				b.Vy += Gv * h; b.X += b.Vx * h; b.Y += b.Vy * h; b.Rot += b.Vx * h / b.R;
				for (int si = 0; si < st.Count; si++) { float ovx = b.Vx, ovy = b.Vy; float im = CircleRect(b, st[si]); if (b == ball && b.Am >= 3 && b.Am <= 5 && im > 90f) Impact(b); int mt = wd[si] == 5 ? 1 : wd[si]; if (mt >= 1 && mt <= 3 && im > (mt == 1 ? (wd[si] == 5 ? 200f : 320f) : (mt == 2 ? 150f : 230f)) && b.Kind != 2) { RectangleF wr = st[si]; Sfx.Play(mt == 1 ? "thud_soft" : "glass"); Shake = Math.Max(Shake, 0.25f); Fx.Burst(wr.X + wr.Width / 2, b.Y, mt == 1 ? Color.FromArgb(200, 150, 80) : (mt == 2 ? Color.FromArgb(180, 225, 255) : Color.FromArgb(210, 240, 255)), 12); if (mt >= 2) for (int q = 0; q < 6; q++) pts.Add(new Pt { X = wr.X + (float)Rnd.NextDouble() * wr.Width, Y = wr.Y + (float)Rnd.NextDouble() * wr.Height, Vx = ((float)Rnd.NextDouble() - 0.5f) * 240f, Vy = -(float)Rnd.NextDouble() * 200f, Sp = q, Rot = q * 60f, Spin = ((float)Rnd.NextDouble() - 0.5f) * 600f, C = Color.White }); st.RemoveAt(si); wd.RemoveAt(si); b.Vx = ovx * 0.62f; b.Vy = ovy * 0.62f; si--; } else if (wd[si] == 1 && im > 120f && b.Kind != 2) { wd[si] = 5; Sfx.Play("thud_soft"); Fx.Burst(b.X, b.Y, Color.FromArgb(200, 150, 80), 5); } }
				RectangleF fl = new RectangleF(-100, Floor, W + 200, 200); float fim = CircleRect(b, fl); if (b == ball && b.Am >= 3 && b.Am <= 5 && fim > 90f) Impact(b);
			}
			for (int i = 0; i < bs.Count; i++) { Bd a = bs[i]; if (!a.Alive || (a == ball && !flying)) continue; for (int j = i + 1; j < bs.Count; j++) { Bd c = bs[j]; if (!c.Alive || (c == ball && !flying)) continue; CircleCircle(a, c); } }
		}
		ResolveImp();
		foreach (Bd b in bs) { if (!b.Alive) continue; if (b.Y > H + 60 || b.X > W + 80 || b.X < -80) Kill(b); if (b.Hit > 0) b.Hit -= dt; }
		for (int i = pts.Count - 1; i >= 0; i--) { Pt p = pts[i]; p.Age += dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vy += 700 * dt; p.Rot += p.Spin * dt; if (p.Age > (p.Sp >= 0 ? 1.1f : 0.8f)) pts.RemoveAt(i); }
		bs.RemoveAll(b => !b.Alive && b != ball);
		bool targets = false; foreach (Bd b in bs) if (b.Alive && (b.Kind == 2 || b.Kind == 4)) targets = true;
		if (flying)
		{
			flyT += dt; float sp = Math.Abs(ball.Vx) + Math.Abs(ball.Vy); still = sp < 40f ? still + dt : 0f;
			if ((!ball.Alive && (deadT += dt) > 1.1f) || flyT > 6f || still > 0.7f || ball.X > W + 30)
			{
				if (!targets) { Sfx.Play("fanfare"); int stars = waveShots <= 1 ? 3 : (waveShots <= 2 ? 2 : 1); Score += 50 * shots + 40 * stars; string rw = ""; if (wave % 2 == 1) { heavy += 2; rw = " · 묵직한 공 +2"; } if (wave >= 2 && splitCharge < 2) { splitCharge++; rw += " · 분열(클릭) +1"; } { int gk = 1 + Rnd.Next(5); stock[gk]++; rw += " · " + AmN[gk] + " +1"; } msg = (stars == 3 ? "★★★ " : stars == 2 ? "★★ " : "★ ") + "민원 해결! +" + (50 * shots + 40 * stars) + rw; msgT = 2.6f; shots += 2; NewWave(); }
				else if (shots <= 0) { Over = true; }
				else { bs.Remove(ball); SpawnBall(); }
			}
		}
	}
	private void Hurt(Bd b, float imp)
	{
		if (b.Hit > 0f || imp < 110f) return; b.Hit = 0.45f; b.Sk--; Shake = Math.Max(Shake, 0.35f); Sfx.Play("thud_soft"); Fx.Burst(b.X, b.Y, Color.FromArgb(220, 90, 80), 10);
		if (b.Sk <= 0) { b.Alive = false; Score += 400; Sfx.Play("glass"); Fx.Burst(b.X, b.Y, Color.FromArgb(255, 200, 90), 22); Shake = 0.6f; msg = "민원 반장 격파! +400"; msgT = 1.6f; fxT2 = 0.7f; fx2X = b.X; fx2Y = b.Y; }
		else { msg = "반장 체력 " + b.Sk; msgT = 0.7f; }
	}
	private void Impact(Bd b) { if (impPend) return; impPend = true; impX = b.X; impY = b.Y; impAm = b.Am; b.Am = 0; }
	private void BreakWalls(float x, float y, float r, int maxMat)
	{
		for (int si = st.Count - 1; si >= 0; si--)
		{
			RectangleF w = st[si]; int mt = wd[si] == 5 ? 1 : wd[si]; if (mt < 1 || mt > maxMat) continue; float qx = Clamp(x, w.Left, w.Right), qy = Clamp(y, w.Top, w.Bottom);
			if ((qx - x) * (qx - x) + (qy - y) * (qy - y) > r * r) continue;
			Fx.Burst(w.X + w.Width / 2, w.Y + w.Height / 2, mt == 1 ? Color.FromArgb(200, 150, 80) : Color.FromArgb(200, 235, 255), 12); Sfx.Play(mt == 1 ? "thud_soft" : "glass"); st.RemoveAt(si); wd.RemoveAt(si);
		}
	}
	private void ResolveImp()
	{
		if (!impPend) return; impPend = false; fxX = impX; fxY = impY;
		if (impAm == 3) { fxT = 0.5f; BreakWalls(impX, impY, 95f, 3); if (ball != null && ball.Alive) { ball.X = impX; ball.Y = impY; Boom(ball); } }
		else if (impAm == 4)
		{
			fxT = 0.6f; Sfx.Play("whoosh"); Fx.Burst(impX, impY, Color.FromArgb(255, 140, 40), 16); msg = "불꽃!"; msgT = 0.8f; BreakWalls(impX, impY, 64f, 1);
			foreach (Bd o in bs) { if (!o.Alive || o == ball) continue; float dx = o.X - impX, dy = o.Y - impY; if (dx * dx + dy * dy > 64f * 64f) continue; if (o.Kind == 2) Kill(o); else if (o.Kind == 3) Boom(o); }
		}
		else if (impAm == 5)
		{
			Sfx.Play("glass"); Fx.Burst(impX, impY, Color.FromArgb(180, 225, 255), 14); msg = "얼음 파편!"; msgT = 0.8f;
			for (int k = -2; k <= 2; k++) { if (k == 0) continue; Bd c = Make(0, impX, impY - 8f); c.Am = 6; c.R = 7f; c.M = 1f; c.Vx = k * 120f; c.Vy = -330f - Math.Abs(k) * 40f; bs.Add(c); }
		}
	}
	private float CircleRect(Bd b, RectangleF r)
	{
		float qx = Clamp(b.X, r.Left, r.Right), qy = Clamp(b.Y, r.Top, r.Bottom), dx = b.X - qx, dy = b.Y - qy, d2 = dx * dx + dy * dy;
		if (d2 >= b.R * b.R) return 0f;
		float d = (float)Math.Sqrt(d2), nx, ny;
		if (d > 0.001f) { nx = dx / d; ny = dy / d; } else { nx = 0; ny = -1; d = 0; }
		b.X += nx * (b.R - d); b.Y += ny * (b.R - d);
		float vn = b.Vx * nx + b.Vy * ny;
		float imp0 = vn < 0 ? -vn : 0f;
		if (vn < 0)
		{
			float e = b.Kind == 0 ? 0.35f : 0.28f; if (-vn < 45f) e = 0f; if (b.Am == 2) e = 0.88f;   // 작은 충격은 튕기지 않고 가만히 앉아요
			b.Vx -= (1 + e) * vn * nx; b.Vy -= (1 + e) * vn * ny;
			if (ny < -0.7f) { b.Vx *= b.Kind == 1 ? 0.9975f : 0.9945f; if (Math.Abs(b.Vx) < 6f) b.Vx = 0f; }   // 바닥 굴림 마찰: 공은 천천히 멈추고 통나무는 더 굴러가요
			float tvx = -ny, tvy = nx; float vt = b.Vx * tvx + b.Vy * tvy; b.Vx -= vt * 0.04f * tvx; b.Vy -= vt * 0.04f * tvy;
			if (b.Kind == 2 && -vn > 330f) Kill(b); if (b.Kind == 3 && -vn > 420f) Boom(b);
		}
		b.Vx *= 0.9985f; return imp0;
	}
	private void CircleCircle(Bd a, Bd c)
	{
		float dx = c.X - a.X, dy = c.Y - a.Y, d = (float)Math.Sqrt(dx * dx + dy * dy), rr = a.R + c.R;
		if (d >= rr || d < 0.001f) return;
		if (a == ball && a.Am == 1 && c.Kind == 2) { Kill(c); return; } if (c == ball && c.Am == 1 && a.Kind == 2) { Kill(a); return; }
		float nx = dx / d, ny = dy / d, rvn = (c.Vx - a.Vx) * nx + (c.Vy - a.Vy) * ny, push = rr - d, tm = a.M + c.M;
		a.X -= nx * push * c.M / tm; a.Y -= ny * push * c.M / tm; c.X += nx * push * a.M / tm; c.Y += ny * push * a.M / tm;
		if (rvn < 0)
		{
			float j = -1.4f * rvn / (1f / a.M + 1f / c.M); a.Vx -= j * nx / a.M; a.Vy -= j * ny / a.M; c.Vx += j * nx / c.M; c.Vy += j * ny / c.M;
			float imp = -rvn; if (a == ball && a.Am >= 3 && a.Am <= 5 && imp > 60f) Impact(a); if (c == ball && c.Am >= 3 && c.Am <= 5 && imp > 60f) Impact(c);
			if (a.Kind == 4 && (c.Kind == 0 || imp > 150f)) Hurt(a, imp); if (c.Kind == 4 && (a.Kind == 0 || imp > 150f)) Hurt(c, imp);
			if (a.Kind == 2 && (c.Kind == 0 || imp > 150f)) Kill(a); if (c.Kind == 2 && (a.Kind == 0 || imp > 150f)) Kill(c);
			if (a.Kind == 3 && (c.Kind == 0 || imp > 170f)) Boom(a); if (c.Kind == 3 && (a.Kind == 0 || imp > 170f)) Boom(c);
		}
	}
	public override void Draw(Graphics g, double tm)
	{
		BgImg(g, "bg_sling", Color.FromArgb(120, 180, 236), Color.FromArgb(226, 240, 252));
		if (Spr.Get("bg_sling") == null) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) { g.FillEllipse(b, 30, 90, 110, 34); g.FillEllipse(b, 240, 140, 130, 38); }
		if (Spr.Get("bg_sling") == null) { using (SolidBrush hb = new SolidBrush(Color.FromArgb(90, 150, 200, 200))) { g.FillEllipse(hb, -80, Floor - 70, 320, 140); g.FillEllipse(hb, 160, Floor - 46, 340, 100); } using (SolidBrush hb = new SolidBrush(Color.FromArgb(110, 110, 175, 150))) g.FillEllipse(hb, 20, Floor - 30, 300, 70); }
		if (Spr.Get("bg_sling") == null) using (LinearGradientBrush gb = new LinearGradientBrush(new RectangleF(0, Floor, W, H - Floor), Color.FromArgb(96, 170, 100), Color.FromArgb(56, 130, 84), 90f)) g.FillRectangle(gb, 0, Floor, W, H - Floor);
		if (Spr.Get("bg_sling") == null) using (Pen gp = new Pen(Color.FromArgb(90, 40, 110, 60), 2f)) for (int k = 0; k < 26; k++) { float gx = k * 16f + (k % 3) * 3f, gy = Floor + 8 + (k % 4) * 14f; g.DrawLine(gp, gx, gy, gx - 2, gy - 6); g.DrawLine(gp, gx, gy, gx + 3, gy - 5); }
		using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawLine(p, 0, Floor, W, Floor);
		for (int si2 = 0; si2 < st.Count; si2++)
		{   // 벽: 재질마다 색만 다른 둥근 모서리 블록 (게임 전체의 동글동글한 분위기에 맞춰요)
			RectangleF r = st[si2]; int m = wd[si2]; Color fc, lc, dc;
			if (m == 1) { fc = Color.FromArgb(196, 140, 84); lc = Color.FromArgb(222, 172, 116); dc = Color.FromArgb(120, 76, 40); }
			else if (m == 5) { fc = Color.FromArgb(176, 124, 76); lc = Color.FromArgb(206, 156, 104); dc = Color.FromArgb(100, 60, 34); }
			else if (m == 2) { fc = Color.FromArgb(170, 220, 240); lc = Color.FromArgb(235, 250, 255); dc = Color.FromArgb(90, 150, 180); }
			else if (m == 3) { fc = Color.FromArgb(150, 210, 245); lc = Color.FromArgb(225, 245, 255); dc = Color.FromArgb(70, 130, 190); }
			else if (m == 4) { fc = Color.FromArgb(120, 128, 146); lc = Color.FromArgb(180, 188, 206); dc = Color.FromArgb(60, 66, 84); }
			else { fc = Color.FromArgb(150, 140, 130); lc = Color.FromArgb(200, 192, 182); dc = Color.FromArgb(90, 82, 76); }
			using (GraphicsPath gp = Gfx.Round(r, Math.Min(7f, Math.Min(r.Width, r.Height) / 2.5f)))
			{
				using (SolidBrush b = new SolidBrush(fc)) g.FillPath(b, gp);
				using (SolidBrush b = new SolidBrush(Color.FromArgb(90, lc))) g.FillRectangle(b, r.X + 3, r.Y + 2, Math.Max(2f, r.Width - 6), Math.Min(5f, r.Height / 3f));
				using (Pen p = new Pen(dc, 2f)) g.DrawPath(p, gp);
			}
			if (m == 1 || m == 5) using (Pen p = new Pen(Color.FromArgb(110, dc), 1.4f)) { for (float ty = r.Y + 10f; ty < r.Bottom - 4f; ty += 12f) g.DrawLine(p, r.X + 4, ty, r.Right - 4, ty + 2f); }
			if (m == 5) using (Pen p = new Pen(Color.FromArgb(200, 50, 30, 16), 2f)) { float cx0 = r.X + r.Width / 2, cy0 = r.Y + r.Height / 2; g.DrawLines(p, new PointF[] { new PointF(cx0 - 5, cy0 - 10), new PointF(cx0 + 2, cy0 - 2), new PointF(cx0 - 3, cy0 + 4), new PointF(cx0 + 5, cy0 + 12) }); }
			if (m == 4) using (SolidBrush b = new SolidBrush(Color.FromArgb(200, dc))) { for (float ty = r.Y + 8f; ty < r.Bottom - 4f; ty += 22f) { g.FillEllipse(b, r.X + 3, ty, 4, 4); g.FillEllipse(b, r.Right - 7, ty, 4, 4); } }
			if (m == 2 || m == 3) using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), 2f)) g.DrawLine(p, r.X + 5, r.Y + 8, r.X + 5, r.Y + Math.Min(26f, r.Height - 6f));
		}
		// 새총
		using (Pen p = new Pen(Color.FromArgb(120, 76, 40), 7f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, SX, Floor, SX, SY + 18); g.DrawLine(p, SX, SY + 18, SX - 14, SY - 8); g.DrawLine(p, SX, SY + 18, SX + 14, SY - 8); }
		float bx2 = ball != null ? ball.X : SX, by2 = ball != null ? ball.Y : SY;
		if (aiming || (ball != null && !flying))
		{
			float stretch = (float)Math.Sqrt((bx2 - SX) * (bx2 - SX) + (by2 - SY) * (by2 - SY)); float bw = aiming ? Math.Max(1.6f, 4.6f - 3f * stretch / MaxPull) : 3.6f;   // 당길수록 고무줄이 가늘어져요
			using (Pen p = new Pen(Color.FromArgb(aiming ? 200 : 140, 70 + (int)(150 * stretch / MaxPull), 30, 30), bw) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, SX - 14, SY - 8, bx2, by2); g.DrawLine(p, SX + 14, SY - 8, bx2, by2); }
			using (SolidBrush pb = new SolidBrush(Color.FromArgb(150, 100, 60, 40))) g.FillEllipse(pb, bx2 - 14, by2 - 9, 28, 18);   // 공을 감싼 가죽
		}
		if (aiming)
		{
			float vx, vy; ax = SX; ay = SY; float sx0 = cx, sy0 = cy; cx = ball.X; cy = ball.Y; bool ok = Pull(out vx, out vy); cx = sx0; cy = sy0;
			if (ok) for (int k = 1; k <= 20; k++) { float t2 = k * 0.05f; float x = ball.X + vx * t2, y = ball.Y + vy * t2 + 0.5f * Gv * t2 * t2; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(230 * (1 - k / 22f)), 255, 255, 255))) g.FillEllipse(b, x - 2.5f, y - 2.5f, 5, 5); }
		}
		if (snapT > 0f && flying) { float kk = snapT / 0.4f; float ox = relX * (float)Math.Cos((0.4f - snapT) * 42f) * kk * 0.7f, oy = relY * (float)Math.Cos((0.4f - snapT) * 42f) * kk * 0.7f; using (Pen p = new Pen(Color.FromArgb((int)(200 * kk), 120, 40, 30), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, SX - 14, SY - 8, SX + ox, SY + oy); g.DrawLine(p, SX + 14, SY - 8, SX + ox, SY + oy); } }
		foreach (Bd b in bs) { if (!b.Alive) continue; float hh = Math.Max(0f, Floor - b.Y), sc = Math.Max(0.35f, 1f - hh / 400f); using (SolidBrush sb = new SolidBrush(Color.FromArgb((int)(60 * sc), 20, 50, 30))) g.FillEllipse(sb, b.X - b.R * sc * 1.1f, Floor - 3, b.R * 2.2f * sc, 7); }
		if (flying && ball != null && ball.Alive) { float sp = (float)Math.Sqrt(ball.Vx * ball.Vx + ball.Vy * ball.Vy); if (sp > 120f) for (int k = 1; k <= 6; k++) { float tt = k * 0.012f; using (SolidBrush tb = new SolidBrush(Color.FromArgb(70 - k * 10, 255, 255, 255))) g.FillEllipse(tb, ball.X - ball.Vx * tt - ball.R * (1f - k * 0.1f), ball.Y - ball.Vy * tt - ball.R * (1f - k * 0.1f), ball.R * 2 * (1f - k * 0.1f), ball.R * 2 * (1f - k * 0.1f)); } }
		foreach (Bd b in bs)
		{
			if (!b.Alive) continue;
			if (b.Kind == 1)
			{
				using (SolidBrush br = new SolidBrush(Color.FromArgb(176, 120, 70))) g.FillEllipse(br, b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2);
				GraphicsState s0 = g.Save(); g.TranslateTransform(b.X, b.Y); g.RotateTransform(b.Rot * 57.3f);
				using (Pen p = new Pen(Color.FromArgb(110, 70, 40), 2f)) { g.DrawEllipse(p, -b.R * 0.55f, -b.R * 0.55f, b.R * 1.1f, b.R * 1.1f); g.DrawLine(p, -b.R * 0.5f, 0, b.R * 0.5f, 0); }
				g.Restore(s0);
			}
			else if (b.Kind == 2)
				{
					{ }
				using (SolidBrush br = new SolidBrush(Color.FromArgb(232, 84, 92))) g.FillEllipse(br, b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2);
				using (SolidBrush br = new SolidBrush(Color.White)) { g.FillEllipse(br, b.X - 8, b.Y - 6, 6, 7); g.FillEllipse(br, b.X + 2, b.Y - 6, 6, 7); }
				using (SolidBrush br = new SolidBrush(Color.FromArgb(40, 20, 40))) { g.FillEllipse(br, b.X - 6, b.Y - 4, 3, 3.5f); g.FillEllipse(br, b.X + 3, b.Y - 4, 3, 3.5f); }
				using (Pen p = new Pen(Color.FromArgb(60, 20, 40), 2f)) { g.DrawLine(p, b.X - 9, b.Y - 10, b.X - 2, b.Y - 7); g.DrawLine(p, b.X + 9, b.Y - 10, b.X + 2, b.Y - 7); g.DrawArc(p, b.X - 5, b.Y + 2, 10, 7, 200, 140); }
			}
			else if (b.Kind == 4)
			{
				float shk = b.Hit > 0f ? (float)Math.Sin(b.Hit * 90f) * 3f : 0f; float bxx = b.X + shk;
				using (SolidBrush br = new SolidBrush(Color.FromArgb(b.Hit > 0f ? 255 : 196, 62, 74))) g.FillEllipse(br, bxx - b.R, b.Y - b.R, b.R * 2, b.R * 2);
				using (Pen p = new Pen(Color.FromArgb(110, 24, 36), 3f)) g.DrawEllipse(p, bxx - b.R, b.Y - b.R, b.R * 2, b.R * 2);
				using (SolidBrush br = new SolidBrush(Color.FromArgb(255, 226, 120))) { g.FillPolygon(br, new PointF[] { new PointF(bxx - b.R * 0.55f, b.Y - b.R * 0.75f), new PointF(bxx - b.R * 0.35f, b.Y - b.R * 1.15f), new PointF(bxx - b.R * 0.12f, b.Y - b.R * 0.9f), new PointF(bxx, b.Y - b.R * 1.2f), new PointF(bxx + b.R * 0.12f, b.Y - b.R * 0.9f), new PointF(bxx + b.R * 0.35f, b.Y - b.R * 1.15f), new PointF(bxx + b.R * 0.55f, b.Y - b.R * 0.75f) }); }
				using (SolidBrush br = new SolidBrush(Color.White)) { g.FillEllipse(br, bxx - b.R * 0.5f, b.Y - b.R * 0.3f, b.R * 0.4f, b.R * 0.45f); g.FillEllipse(br, bxx + b.R * 0.1f, b.Y - b.R * 0.3f, b.R * 0.4f, b.R * 0.45f); }
				using (SolidBrush br = new SolidBrush(Color.FromArgb(40, 20, 40))) { g.FillEllipse(br, bxx - b.R * 0.34f, b.Y - b.R * 0.18f, b.R * 0.2f, b.R * 0.22f); g.FillEllipse(br, bxx + b.R * 0.2f, b.Y - b.R * 0.18f, b.R * 0.2f, b.R * 0.22f); }
				using (Pen p = new Pen(Color.FromArgb(60, 20, 40), 3f)) { g.DrawLine(p, bxx - b.R * 0.55f, b.Y - b.R * 0.42f, bxx - b.R * 0.1f, b.Y - b.R * 0.28f); g.DrawLine(p, bxx + b.R * 0.55f, b.Y - b.R * 0.42f, bxx + b.R * 0.1f, b.Y - b.R * 0.28f); g.DrawArc(p, bxx - b.R * 0.3f, b.Y + b.R * 0.15f, b.R * 0.6f, b.R * 0.4f, 200, 140); }
				for (int q = 0; q < 3; q++) using (SolidBrush hb = new SolidBrush(q < b.Sk ? Color.FromArgb(235, 70, 70) : Color.FromArgb(120, 40, 40, 40))) g.FillRectangle(hb, b.X - 21f + q * 15f, b.Y - b.R - 12f, 12f, 4f);
			}
			else if (b.Kind == 3)
			{
				using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 60, 50))) g.FillEllipse(br, b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2);
				using (Pen p = new Pen(Color.FromArgb(90, 20, 20), 2f)) { g.DrawEllipse(p, b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2); g.DrawLine(p, b.X - b.R * 0.7f, b.Y - 4, b.X + b.R * 0.7f, b.Y - 4); g.DrawLine(p, b.X - b.R * 0.7f, b.Y + 5, b.X + b.R * 0.7f, b.Y + 5); }
				Txt(g, "TNT", 7.5f, 2, Color.White, b.X - 20, b.Y - 7, 40, 14, StringAlignment.Center);
			}
			else
			{
				int am = (b == ball && !flying) ? (sel > 0 && stock[sel] > 0 ? sel : 0) : b.Am; string an = am == 1 ? "ms_am_pierce" : am == 2 ? "ms_am_bounce" : am == 3 ? "ms_am_bomb" : am == 4 ? "ms_am_fire1" : am == 5 ? "ms_am_ice1" : am == 6 ? "ms_ice_sh" + (b.Sk & 3) : null;
				if (an != null && Spr.Get(an) != null)
				{
					if (am == 1) { float ang = (float)(Math.Atan2(flying ? b.Vy : SY - b.Y, flying ? b.Vx : SX - b.X) * 180.0 / Math.PI); float pw2 = b.R * 3.6f; Spr.Draw(g, an, b.X, b.Y, pw2, pw2 * 52f / 200f, ang); }
					else { float k2 = am == 4 ? 1f + 0.07f * (float)Math.Sin(tm * 22) : 1f; Spr.Draw(g, an, b.X, b.Y, b.R * 2.5f * k2, b.R * 2.5f * k2, am == 3 ? b.Rot * 20f : b.Rot * 57.3f); }
					continue;
				}
				if (Spr.Draw(g, "bball", b.X, b.Y, b.R * 2.2f, b.R * 2.2f, b.Rot * 57.3f)) continue; using (SolidBrush br = new SolidBrush(Color.FromArgb(60, 70, 150))) g.FillEllipse(br, b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2); using (SolidBrush br = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(br, b.X - 7, b.Y - 8, 8, 6); }
		}
		if (ball != null && !flying) using (Pen p = new Pen(Color.FromArgb(120, 40, 30), 3f)) { g.DrawLine(p, SX + 14, SY - 8, bx2, by2); }
		if (aiming) { float d0 = (float)Math.Sqrt((bx2 - SX) * (bx2 - SX) + (by2 - SY) * (by2 - SY)); if (d0 > 8f) { float pw = d0 / MaxPull; using (SolidBrush pb2 = new SolidBrush(Color.FromArgb((int)(110 * pw), 255, 200, 90))) g.FillEllipse(pb2, bx2 - 22 * pw - 4, by2 - 22 * pw - 4, 44 * pw + 8, 44 * pw + 8); } }
		foreach (Pt p in pts) if (p.Sp >= 0) { if (Spr.Get("ms_shd" + p.Sp) != null) Spr.Draw(g, "ms_shd" + p.Sp, p.X, p.Y, Spr.OW("ms_shd" + p.Sp, Spr.Get("ms_shd" + p.Sp)) * 0.7f, Spr.OH("ms_shd" + p.Sp, Spr.Get("ms_shd" + p.Sp)) * 0.7f, p.Rot); } else using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * Math.Max(0, 1 - p.Age / 0.8)), p.C))) g.FillEllipse(b, p.X - 3, p.Y - 3, 6, 6);
		if (fxT > 0f && Spr.Get("ms_am_fire2") != null) { float fk = fxT / 0.6f; float fs = 70f + (1f - fk) * 50f; Spr.Draw(g, "ms_am_fire2", fxX, fxY - 10f, fs, fs, 0f); }
		{
			bool any = false; for (int i = 1; i < 6; i++) if (stock[i] > 0) any = true;
			if (any || sel > 0) for (int i = 0; i < 6; i++)
			{
				if (i > 0 && stock[i] == 0) continue; float ix = 118f + i * 40f, iy = 572f; bool on = sel == i;
				using (SolidBrush hb = new SolidBrush(Color.FromArgb(on ? 200 : 110, on ? 255 : 30, on ? 230 : 40, on ? 120 : 70))) g.FillEllipse(hb, ix - 18f, iy - 18f, 36f, 36f);
				string hn = i == 0 ? "bball" : (i == 1 ? "ms_am_pierce" : i == 2 ? "ms_am_bounce" : i == 3 ? "ms_am_bomb" : i == 4 ? "ms_am_fire1" : "ms_am_ice1");
				if (i == 1) Spr.Draw(g, hn, ix, iy, 30f, 8f, -30f); else Spr.Draw(g, hn, ix, iy, 26f, 26f, 0f);
				if (i > 0) Txt(g, "x" + stock[i], 9f, 2, Color.White, ix - 4f, iy + 4f, 26f, 14f, StringAlignment.Center);
			}
		}
		if (msgT > 0) Txt(g, msg, 15f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 60, 50, 110), 0, 200, W, 30, StringAlignment.Center);
		if (!flying && shots == 5 && wave == 1) Hint(g, "공을 뒤로 당겼다 놓으세요 →", 300 + 11.0, 12f, false, 1f);
		if (heavy > 0 || splitCharge > 0) Txt(g, (heavy > 0 ? "묵직한 공 " + heavy + "발  " : "") + (splitCharge > 0 ? "공중 클릭=분열 " + splitCharge : ""), 10f, 1, Color.FromArgb(230, 160, 70, 40), 0, 66, W, 20, StringAlignment.Center);
		Txt(g, "판 " + wave, 11f, 1, Color.FromArgb(220, 60, 50, 110), 14, 44, 100, 22, StringAlignment.Near);
	}
}
