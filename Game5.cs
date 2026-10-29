using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 올림픽·퍼즐 종목: 양궁, 클라이밍, 전구 끄기 ──────────────────────────

// ── 21. 양궁 ────────────────────────────────────────────────
internal sealed class ArcheryGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private float tx = 200f, ty = 230f, rr = 12f; private float mvA, mvB, mvS = 1f, mvP;
	private readonly List<PointF> hits = new List<PointF>();
	private readonly List<int> pts = new List<int>();
	private int bullAt = -99999, missAt = -99999; private int shots, state; private float holdT, el, windX, flyT, msgT, rest; private bool wasHold; private PointF aim, target; private string msg = "";
	public override string Costume { get { return "g_archery"; } }
	public override string Name { get { return "양궁"; } }
	public override string Key { get { return "archery"; } }
	public override string Desc { get { return "바람을 읽고 10점을 노려요"; } }
	public override string Help { get { return "마우스로 조준점을 옮기고, 누르고 있으면 활을 당겨요 (흔들림이 줄어들어요)\n숨을 고르고 놓으면 발사! 너무 오래 당기면 팔이 떨려요\n바람 반대쪽을 노리세요 · 과녁은 갈수록 작아지고 움직여요 — 가는 방향 앞을 노리세요 · 6발"; } }
	public override string RightText { get { return "화살 " + Math.Min(6, shots + (state == 0 || state == 1 ? 1 : 0)) + "/6"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		for (int i = 0; i < 3; i++) using (SolidBrush b = new SolidBrush(i == 0 ? Color.FromArgb(230, 70, 70) : (i == 1 ? Color.White : Color.FromArgb(70, 120, 220)))) { float r = 18 - i * 6; g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2); }
	}
	public override void Reset() { mvP = 0f; tx = 200f; ty = 230f; rr = 12.5f; hits.Clear(); pts.Clear(); shots = 0; Score = 0; Over = false; state = 0; holdT = 0; el = 0; wasHold = false; msg = ""; msgT = 0; NewWind(); aim = new PointF(tx, ty); }
	private float windB; private void NewWind() { windB = ((float)Rnd.NextDouble() * 2f - 1f) * (24f + shots * 5f); windX = windB; }
	private void Move(float dt)
	{
		// 판이 올라갈수록 과녁이 작아지고, 좌우 → 상하까지 움직여요 (움직이는 과녁은 앞을 읽고 쏴야 해요)
		int k = Math.Min(5, shots); rr = 12.5f - k * 0.9f; mvA = k == 0 ? 0f : Math.Min(34f, 8f + k * 5f); mvB = k >= 3 ? Math.Min(14f, (k - 2) * 5f) : 0f; mvS = 0.55f + k * 0.08f;
		mvP += dt * mvS; tx = 200f + mvA * (float)Math.Sin(mvP * 1.3); ty = 230f + mvB * (float)Math.Sin(mvP * 2.1 + 1);
	}
	private PointF Sway()
	{
		float A = holdT <= 0 ? 24f : (holdT < 2.4f ? 24f - 17f * Math.Min(1f, holdT / 0.8f) : 7f + (holdT - 2.4f) * 22f); A = Math.Min(A, 34f);
		return new PointF(A * ((float)Math.Sin(el * 1.9) + 0.5f * (float)Math.Sin(el * 3.1 + 1)) / 1.5f, A * ((float)Math.Cos(el * 1.3) + 0.5f * (float)Math.Cos(el * 2.7)) / 1.5f);
	}
	private int Ring(PointF p)
	{
		float dx = p.X - tx, dy = p.Y - ty, d = (float)Math.Sqrt(dx * dx + dy * dy); if (d >= rr * 10f) return 0;
		int r = 11 - (int)Math.Ceiling(d / rr); return Math.Max(1, Math.Min(10, r));
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		el += dt; if (msgT > 0) msgT -= dt; Move(dt);
		if (state == 0)
		{
			aim = new PointF(PF.Cl(inp.Mx, 40f, 360f), PF.Cl(inp.My, 90f, 400f));
			windX = windB + (float)Math.Sin(el * 1.3 + shots) * Math.Min(16f, 3f + shots * 3f);   // 바람이 돌풍처럼 조금씩 변해요 (쏘는 순간의 바람이 적용)
			if (inp.Hold) holdT += dt;
			else if (wasHold)
			{
				if (holdT >= 0.25f)
				{
					PointF s = Sway(); target = new PointF(aim.X + s.X + windX, aim.Y + s.Y + 8f); state = 1; flyT = 0f; Sfx.Play("swing");
				}
				holdT = 0f;
			}
			if (!inp.Hold && !wasHold) holdT = 0f;
		}
		else if (state == 1)
		{
			flyT += dt / 0.32f;
			if (flyT >= 1f)
			{
				int r = Ring(target); hits.Add(new PointF((target.X - tx) / rr, (target.Y - ty) / rr)); pts.Add(r); if (r >= 10) bullAt = Environment.TickCount; if (r <= 4) missAt = Environment.TickCount; Score += r; shots++;
				msg = r == 0 ? "빗나갔어요" : (r == 10 ? "텐! 10점!" : r + "점"); msgT = 1.3f;
				if (r >= 9) { Sfx.Play("fanfare"); Fx.Burst(target.X, target.Y, Color.FromArgb(255, 220, 90), 14); Shake = 0.2f; } else if (r > 0) { Sfx.Play("thud2"); Fx.Burst(target.X, target.Y, Color.FromArgb(235, 210, 150), 3 + r / 2); Shake = Math.Max(Shake, 0.05f + r * 0.012f); } else Sfx.Play("bad2");
				state = 2; rest = 0f;
			}
		}
		else if (state == 2)
		{
			rest += dt;
			if (rest > 1.2f) { if (shots >= 6) Over = true; else { NewWind(); state = 0; holdT = 0f; wasHold = inp.Hold; return; } }
		}
		wasHold = inp.Hold;
	}
	private static Color RingCol(int r) { return r >= 9 ? Color.FromArgb(250, 214, 70) : r >= 7 ? Color.FromArgb(226, 70, 70) : r >= 5 ? Color.FromArgb(70, 130, 224) : r >= 3 ? Color.FromArgb(40, 40, 50) : Color.FromArgb(246, 246, 246); }
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, Spr.Get("bg_arch2") != null ? "bg_arch2" : "bg_arch", Color.FromArgb(120, 190, 240), Color.FromArgb(150, 214, 130));
		using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) { g.FillEllipse(b, 30, 60, 90, 24); g.FillEllipse(b, 250, 80, 110, 26); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 70, 130, 70))) g.FillRectangle(b, 0, 430, W, 170);
		// 받침대
		if (Spr.Get("ms_ar_cart") != null) { using (SolidBrush sb = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sb, tx - 66f, 426f, 132f, 14f); Bitmap cb = Spr.Get("ms_ar_cart"); float chh = 150f, cww = chh * cb.Width / cb.Height; Spr.Draw(g, "ms_ar_cart", tx, 434f - chh / 2f, cww, chh, 0f); }
		else using (Pen p = new Pen(Color.FromArgb(150, 90, 60), 8f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, tx - 40, 430, tx, ty + 130); g.DrawLine(p, tx + 40, 430, tx, ty + 130); }
		for (int r = 1; r <= 10; r++)
		{
			float rad = rr * (11 - r);
			using (SolidBrush b = new SolidBrush(RingCol(r))) g.FillEllipse(b, tx - rad, ty - rad, rad * 2, rad * 2);
			using (Pen p = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawEllipse(p, tx - rad, ty - rad, rad * 2, rad * 2);
		}
		for (int i = 0; i < hits.Count; i++)
		{
			PointF h = new PointF(tx + hits[i].X * rr, ty + hits[i].Y * rr);
			if (Spr.Get("ms_am_arrow") != null) { float al = 24f + rr * 0.9f; Spr.Draw(g, "ms_am_arrow", h.X + 0.29f * al * 1.0f, h.Y + 0.4f * al, al, al * 50f / 300f, -125.5f); }
			else { using (Pen p = new Pen(Color.FromArgb(220, 60, 60), 3f) { StartCap = LineCap.Round }) g.DrawLine(p, h.X, h.Y, h.X + 16, h.Y + 22); using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 30, 30))) g.FillEllipse(b, h.X - 3, h.Y - 3, 6, 6); }
		}
		if (state == 1)
		{
			float e = flyT * flyT; PointF s0 = new PointF(tx, 520f); float x = PF.Lerp(s0.X, target.X, e), y = PF.Lerp(s0.Y, target.Y, e), sc = 1f - 0.7f * e;
			if (Spr.Get("ms_am_arrow") != null) { float e2 = Math.Min(1f, flyT + 0.03f), ex2 = PF.Lerp(s0.X, target.X, e2 * e2), ey2 = PF.Lerp(s0.Y, target.Y, e2 * e2); float dx3 = ex2 - x, dy3 = ey2 - y; float ang = (float)(Math.Atan2(target.Y - s0.Y, target.X - s0.X) * 180.0 / Math.PI); float al = 24f + 80f * sc; Spr.Draw(g, "ms_am_arrow", x - (float)Math.Cos(ang / 57.2958f) * al * 0.5f, y - (float)Math.Sin(ang / 57.2958f) * al * 0.5f, al, al * 50f / 300f, ang); }
			else { using (Pen p = new Pen(Color.FromArgb(240, 200, 80), 4f * sc + 1f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x, y, x, y + 70f * sc); using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 60, 60))) g.FillEllipse(b, x - 4 * sc - 1, y - 4 * sc - 1, 8 * sc + 2, 8 * sc + 2); }
		}
		// 바람 표시
		float wc = 200f; using (GraphicsPath gp = Gfx.Round(new RectangleF(110, 40, 180, 36), 14)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 20, 40, 70))) g.FillPath(b, gp);
		string wt = Math.Abs(windX) < 4f ? "바람 거의 없음" : (windX > 0 ? "바람 → " : "← 바람 ") + (Math.Abs(windX) / 8f).ToString("0.0") + "m/s";
		Txt(g, wt, 12f, 2, Color.White, 110, 46, 180, 24, StringAlignment.Center);
		if (Math.Abs(windX) >= 4f) { float ax = wc + (windX > 0 ? 1 : -1) * 90f; using (Pen p = new Pen(Color.FromArgb(255, 226, 120), 3f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(p, ax - (windX > 0 ? 1 : -1) * 6, 58, ax + (windX > 0 ? 1 : -1) * Math.Min(24f, Math.Abs(windX) * 0.7f) - (windX > 0 ? 1 : -1) * 6, 58); }
		if (Spr.Get("ms_ar_wind") != null) { float sgn = windX >= 0 ? 1f : -1f; float str = Math.Min(1f, Math.Abs(windX) / 30f); Spr.Draw(g, "ms_ar_wind", 84f, 60f, sgn * (22f + 8f * str), 32f, sgn * (float)Math.Sin(el * 6 + 1) * (2f + 4f * str)); }
		if (Environment.TickCount - bullAt < 1300 && Spr.Get("ms_ar_tgt") != null) { float bs = 1f + 0.15f * (float)Math.Sin((Environment.TickCount - bullAt) / 60.0); Spr.Draw(g, Spr.Get("ms_ar_hit1") != null ? "ms_ar_hit1" : "ms_ar_tgt", 200f, 112f, 66f * bs, 70f * bs, 0f); }
		if (Spr.Get("ms_ar_quiv") != null) { Spr.Draw(g, "ms_ar_quiv", 376f, 500f, 22f, 55f, 0f); int left = Math.Max(0, 6 - shots - (state == 2 || state == 3 ? 1 : 0)); Txt(g, "x" + left, 12f, 2, Color.White, 318, 492, 40, 20, StringAlignment.Far); }
		// 조준점
		if (state == 0)
		{
			PointF s = Sway(); float cx = aim.X + s.X, cy = aim.Y + s.Y; bool tired = holdT > 2.4f;
			Color c = tired ? Color.FromArgb(255, 120, 110) : (holdT > 0.25f ? Color.FromArgb(120, 255, 170) : Color.FromArgb(255, 255, 255));
			using (Pen p = new Pen(c, 2.2f)) { g.DrawEllipse(p, cx - 14, cy - 14, 28, 28); g.DrawLine(p, cx - 20, cy, cx - 6, cy); g.DrawLine(p, cx + 6, cy, cx + 20, cy); g.DrawLine(p, cx, cy - 20, cx, cy - 6); }
			if (holdT > 0)
			{
				float f = Math.Min(1f, holdT / 0.8f); using (GraphicsPath gp = Gfx.Round(new RectangleF(120, 456, 160, 14), 7)) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 10, 20, 40))) g.FillPath(b, gp);
				if (f > 0.05f) using (GraphicsPath gp = Gfx.Round(new RectangleF(120, 456, 160 * f, 14), 7)) using (SolidBrush b = new SolidBrush(tired ? Color.FromArgb(255, 120, 110) : Color.FromArgb(120, 230, 160))) g.FillPath(b, gp);
				Txt(g, tired ? "팔이 떨려요! 어서 놓으세요" : (holdT < 0.8f ? "숨 고르는 중…" : "지금이에요!"), 11f, 1, Color.White, 0, 474, W, 22, StringAlignment.Center);
			}
			else Hint(g, "누르고 있다가 놓으면 발사", 474 + 11.0, 11f, false, 1f);
		}
		if (Spr.Get("ms_ar_pb0") != null) { bool angry = Environment.TickCount - missAt < 1400 && state == 2 && Spr.Get("ms_ar_pa0") != null; string an = angry ? (Environment.TickCount - missAt < 450 ? "ms_ar_pa2" : "ms_ar_pa0") : state == 1 || (state == 2 && rest < 0.5f) ? "ms_ar_pb2" : (state == 0 && holdT > 0.12f ? "ms_ar_pb1" : "ms_ar_pb0"); Bitmap ab = Spr.Get(an); float ah = 140f, aw = ah * ab.Width / ab.Height; Spr.Draw(g, an, Math.Max(64f, aw / 2f - 6f), 596f - ah / 2f, aw, ah, 0f); }
		else if (!Spr.Frame(g, "ms_ar" + (flyT > 0f ? 3 : (holdT > 0.6f ? 2 : (holdT > 0.12f ? 1 : 0))), 70f, 604f, 0.46f, 0f, false)) DrawSpr(g, "m_ar" + (flyT > 0f ? 4 : (holdT > 0.6f ? 3 : (holdT > 0.12f ? 2 : 1))), 66, 600, 108f, 0f);
		// 점수판
		for (int i = 0; i < 6; i++)
		{
			RectangleF r = new RectangleF(140 + i * 43, 546, 38, 38);
			using (GraphicsPath gp = Gfx.Round(r, 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(i < pts.Count ? 220 : 110, 255, 255, 255))) g.FillPath(b, gp);
			if (i < pts.Count) Txt(g, pts[i].ToString(), 15f, 2, pts[i] >= 9 ? Color.FromArgb(200, 120, 0) : Color.FromArgb(50, 60, 90), r.X, r.Y, r.Width, r.Height, StringAlignment.Center);
		}
		if (msgT > 0) Txt(g, msg, 22f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 255, 255), 0, 150, W, 40, StringAlignment.Center);
	}
}

// ── 22. 클라이밍 ────────────────────────────────────────────
internal sealed class ClimbGame : MiniGame
{
	private class Hd { public float X, Y; public int Type; public float Crumb; }
	private readonly List<Hd> holds = new List<Hd>();
	private int cur, from, to; private float stam, mvT, mvD = 0.26f, camY, px, py, msgT, el, sw, swV, fvy, fT, fRot; private bool moving, falling; private class Db { public float X, Y, Vx, Vy, A; } private readonly List<Db> dbs = new List<Db>(); private string msg = "";
	private const float Reach = 140f;
	private float brkX, brkY, brkT; private float popT; private string popN = ""; private int chalk = 1, carab = 1, helm = 1, anchor = -1, nextCar = 50, nextHel = 70; private float chalkT, rockT, rockX, rockW, rockY, nextRock = 7f; private bool rockOn;
	public override string Costume { get { return "산의날"; } }
	public override string Name { get { return "클라이밍"; } }
	public override string Key { get { return "climb"; } }
	public override string Desc { get { return "손잡이를 골라 위로 올라가요"; } }
	public override string Help { get { return "초록 테두리가 뜬 손잡이(손이 닿는 곳)를 클릭해서 위로 올라가요\n매달려 있으면 체력이 줄어요 · 흰색 '분필' 손잡이를 잡으면 분필 +1 → 위쪽 분필 아이콘을 눌러 쓰면 체력이 천천히 줄어요\n카라비너: 아이콘을 눌러 지금 손잡이에 고정, 떨어져도 거기로 돌아와요 · 헬멧: 15m부터 떨어지는 낙석(!)을 1번 막아줘요\n갈색으로 금이 간 손잡이는 곧 부서지니 빨리 다음으로 옮기세요"; } }
	public override string RightText { get { return Score + "m"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 120, 90))) g.FillRectangle(b, cx - 18, cy - 18, 36, 36);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 120, 70))) { g.FillEllipse(b, cx - 12, cy + 4, 10, 10); g.FillEllipse(b, cx + 4, cy - 12, 10, 10); }
	}
	public override void Reset()
	{
		chalk = 1; carab = 1; helm = 1; anchor = -1; nextCar = 50; nextHel = 70; chalkT = 0f; rockOn = false; nextRock = 7f; dbs.Clear(); falling = false; sw = swV = 0f; holds.Clear(); holds.Add(new Hd { X = 200f, Y = 520f }); cur = 0; from = to = 0; moving = false; stam = 1f; Score = 0; Over = false; px = 200f; py = 520f; camY = 120f; msg = ""; msgT = 0; el = 0; Gen();
	}
	private void Gen()
	{
		while (holds[holds.Count - 1].Y > camY - 260f)
		{
			Hd l = holds[holds.Count - 1]; float y = l.Y - (50f + (float)Rnd.NextDouble() * 40f), x = PF.Cl(l.X + ((float)Rnd.NextDouble() - 0.5f) * 220f, 40f, 360f);
			double r = Rnd.NextDouble(); int ty = r < 0.11 ? 1 : (r < 0.24 && holds.Count > 4 ? 2 : 0);
			holds.Add(new Hd { X = x, Y = y, Type = ty });
			if (Rnd.NextDouble() < 0.6) { float x2 = PF.Cl(x + (Rnd.NextDouble() < 0.5 ? -1 : 1) * (80f + (float)Rnd.NextDouble() * 60f), 40f, 360f); holds.Add(new Hd { X = x2, Y = y + (float)Rnd.NextDouble() * 30f - 10f, Type = Rnd.NextDouble() < 0.1 ? 1 : 0 }); }
		}
	}
	private bool CanReach(Hd a, Hd b) { float dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy <= Reach * Reach && b.Y <= a.Y + 30f; }
	public override void Click(float x, float y)
	{
		if (Over || falling) return;
		if (y < 72f && y > 30f)
		{
			if (x > 186f && x < 214f && chalk > 0 && chalkT <= 0f) { chalk--; chalkT = 9f; popN = "ms_cl_chalk"; popT = 1.4f; msg = "분필 바르기! 9초간 체력이 천천히 줄어요"; msgT = 1.2f; Sfx.Play("ok2"); Fx.Burst(200f, 52f, Color.White, 8); }
			else if (x > 240f && x < 268f && carab > 0 && anchor < 0 && !moving) { carab--; anchor = cur; popN = "ms_cl_rope"; popT = 1.4f; msg = "카라비너 고정! 떨어져도 여기로 돌아와요"; msgT = 1.3f; Sfx.Play("ok2"); }
			return;
		}
		if (moving) return; float wy = y + camY;
		for (int i = 0; i < holds.Count; i++)
		{
			if (i == cur) continue; Hd h = holds[i]; float dx = x - h.X, dy = wy - h.Y; if (dx * dx + dy * dy > 36f * 36f) continue;
			if (!CanReach(holds[cur], h)) { msg = "너무 멀어요"; msgT = 0.9f; Sfx.Play("tick"); return; }
			from = cur; to = i; moving = true; mvT = 0f; float ddx = h.X - holds[cur].X, ddy = h.Y - holds[cur].Y, dist = (float)Math.Sqrt(ddx * ddx + ddy * ddy); mvD = 0.2f + dist / 650f; stam -= 0.012f + dist / 5200f + Math.Max(0f, -ddy) * 0.0002f - Math.Max(0f, ddy) * 0.00005f;   // 몸무게를 끌어올리는 일: 위로 갈수록 힘이 더 들고 내려갈 땐 덜 들어요
				swV += ddx * 0.012f;  Sfx.Play("whoosh"); return;
		}
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		el += dt; if (msgT > 0) msgT -= dt; if (chalkT > 0f) chalkT -= dt; if (popT > 0f) popT -= dt; if (brkT > 0f) brkT -= dt;
		if (!falling && Score >= 15)
		{
			if (!rockOn) { nextRock -= dt; if (nextRock <= 0f) { rockOn = true; rockT = 0f; rockX = PF.Cl(px + ((float)Rnd.NextDouble() - 0.5f) * 80f, 50f, 350f); Sfx.Play("tick"); } }
			else
			{
				rockT += dt; rockY = -30f + Math.Max(0f, rockT - 1.1f) * 900f;
				if (rockT >= 1.1f + (px > 0 ? (py - camY + 30f) / 900f : 0.5f))
				{
					rockOn = false; nextRock = Math.Max(3.5f, 8f - Score * 0.03f) + (float)Rnd.NextDouble() * 2f;
					if (Math.Abs(px - rockX) < 34f)
					{
						if (helm > 0) { helm--; msg = "헬멧이 막아줬어요! (헬멧 -1)"; msgT = 1.4f; Sfx.Play("thud2"); Fx.Burst(px, py - camY - 30f, Color.FromArgb(255, 220, 120), 8); Shake = 0.2f; }
						else { stam -= 0.3f; msg = "낙석에 맞았어요! 체력 -30%"; msgT = 1.4f; Sfx.Play("bad2"); Shake = 0.35f; swV += 6f; }
					}
				}
			}
		}
		if (moving)
		{
			mvT += dt / mvD; Hd a = holds[from], b = holds[to]; float e = Math.Min(1f, mvT); float up = Math.Max(0f, a.Y - b.Y);
			px = PF.Lerp(a.X, b.X, e); py = PF.Lerp(a.Y, b.Y, e * (2f - e) * 0.35f + e * 0.65f) - 4f * (12f + up * 0.35f) * e * (1f - e);   // 던지듯 뛰어올랐다가 내려앉는 포물선
			if (mvT >= 1f)
			{
				moving = false; cur = to; px = b.X; py = b.Y; int m = (int)((520f - b.Y) / 10f); if (m > Score) Score = m;
				if (b.Type == 1) { stam = Math.Min(1f, stam + 0.2f); chalk = Math.Min(3, chalk + 1); b.Type = 0; msg = "분필 획득! (위의 분필 아이콘을 눌러 쓰기)"; msgT = 1.1f; Sfx.Play("ok2"); Fx.Burst(px, py - camY, Color.White, 10); } else { Sfx.Play("thud_soft"); Fx.Burst(px, py - camY + 4f, Color.FromArgb(200, 190, 170), 4); swV += (b.X - holds[from].X) * 0.01f; }
			}
		}
		else
		{
			Hd h = holds[cur]; float drain = Math.Min(0.075f, 0.026f + Score * 0.0002f) * (1f + Math.Abs(sw) * 0.05f) * (chalkT > 0f ? 0.45f : 1f); stam -= drain * dt;
			if (Score >= nextCar) { nextCar += 50; carab = Math.Min(2, carab + 1); msg = "카라비너 +1"; msgT = 1.2f; }
			if (Score >= nextHel) { nextHel += 70; helm = Math.Min(2, helm + 1); msg = "헬멧 +1"; msgT = 1.2f; }
			swV += (-sw * 22f - swV * 2.2f) * dt; sw += swV * dt;   // 매달린 몸이 흔들리다 가라앉아요 (진자)
			if (h.Type == 2) { h.Crumb += dt; if (el % 0.12f < dt) Fx.Burst(h.X + (float)(Rnd.NextDouble() - 0.5) * 20f, h.Y - camY + 10f, Color.FromArgb(150, 110, 70), 1); if (h.Crumb > 1.25f) { brkX = h.X; brkY = h.Y; brkT = 0.5f; for (int k = 0; k < 7; k++) dbs.Add(new Db { X = h.X + (float)(Rnd.NextDouble() - 0.5) * 24f, Y = h.Y, Vx = ((float)Rnd.NextDouble() - 0.5f) * 160f, Vy = -60f - (float)Rnd.NextDouble() * 80f }); Fall(); return; } }
			if (stam < 0.25f && el % 0.4f < dt) Fx.Burst(px + 20f, py - camY - 40f, Color.FromArgb(160, 200, 255), 1);
		}
		foreach (Db d0 in dbs) { if (!falling) { d0.Vy += 1500f * dt; d0.X += d0.Vx * dt; d0.Y += d0.Vy * dt; d0.A += dt * 240f; } }
		dbs.RemoveAll(d1 => d1.Y - camY > 900f);
		if (falling)
		{
			fT += dt; fvy += 1500f * dt; py += fvy * dt; fRot += 360f * dt; camY += (py - 400f - camY) * Math.Min(1f, dt * 4f);
			foreach (Db d in dbs) { d.Vy += 1500f * dt; d.X += d.Vx * dt; d.Y += d.Vy * dt; d.A += dt * 240f; }
			if (fT > 1.1f) Over = true; return;
		}
		if (stam <= 0f) { stam = 0f; Fall(); return; }
		if (!falling) { camY += (py - 400f - camY) * Math.Min(1f, dt * 4f); } Gen();
	}
	private void Fall()
	{
		if (falling) return;
		if (anchor >= 0 && anchor < holds.Count) { cur = anchor; px = holds[cur].X; py = holds[cur].Y; moving = false; stam = 0.4f; anchor = -1; sw = swV = 0f; msg = "카라비너가 잡아줬어요!"; msgT = 1.5f; Sfx.Play("ok2"); Shake = 0.3f; if (holds[cur].Type == 2) holds[cur].Type = 0; return; }
		FallReal();
	}
	private void FallReal() { if (falling) return; falling = true; moving = false; fT = 0f; fvy = -260f; fRot = 0f; Sfx.Play("fall"); Shake = 0.5f; Fx.Burst(px, py - camY, Color.FromArgb(255, 200, 150), 8); }
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_rock", Color.FromArgb(108, 100, 112), Color.FromArgb(62, 58, 74));
		using (Pen p = new Pen(Color.FromArgb(34, 255, 255, 255), 1f)) { float off = -(camY % 100f); for (int i = -1; i < 8; i++) g.DrawLine(p, 0, off + i * 100f, W, off + i * 100f); for (int i = 1; i < 4; i++) g.DrawLine(p, i * 100f, 0, i * 100f, H); }
		float cy0 = holds[cur].Y;
		foreach (Hd h in holds)
		{
			float sy = h.Y - camY; if (sy < -30 || sy > H + 30) continue;
			bool reach = !moving && h != holds[cur] && CanReach(holds[cur], h); float sh = h.Type == 2 && h == holds[cur] ? (float)Math.Sin(el * 40f) * 2f : 0f;
			Color c = h.Type == 1 ? Color.FromArgb(246, 246, 250) : (h.Type == 2 ? Color.FromArgb(150, 110, 70) : Color.FromArgb(240, 120 + (int)Math.Abs(h.X % 60), 70 + (int)Math.Abs(h.Y % 80)));
			if (reach) using (Pen p = new Pen(Color.FromArgb(120 + (int)(100 * Math.Sin(t * 6)), 120, 255, 170), 3f)) g.DrawEllipse(p, h.X - 24, sy - 24, 48, 48);
			string sp2 = h.Type == 1 ? "ms_hd_chalk" : (h.Type == 2 ? (h == holds[cur] && h.Crumb > 0.6f ? "ms_hd_crk1" : "ms_hd_crk0") : null); bool spOk = sp2 != null && Spr.Get(sp2) != null;
			bool hs = h.Type == 0 && Spr.Get("ms_hd1") != null;
			if (spOk) { using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, h.X - 16, sy - 6, 34, 22); Bitmap hb1 = Spr.Get(sp2); float hh1 = 44f, hw1 = hh1 * hb1.Width / hb1.Height; Spr.Draw(g, sp2, h.X + sh, sy - 2, hw1, hh1, 0f); }
			else if (hs) { using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, h.X - 15, sy - 6, 32, 22); { string hn = "ms_hd" + (1 + (int)(Math.Abs(h.X * 7 + h.Y * 3) % 5)); Bitmap hb0 = Spr.Get(hn); float hw0 = 40f, hh0 = hb0 != null ? hw0 * hb0.Height / hb0.Width : 40f; if (hh0 > 44f) { hh0 = 44f; hw0 = hb0.Width * hh0 / hb0.Height; } Spr.Draw(g, hn, h.X + sh, sy - 2, hw0, hh0, (float)((h.X * 13 + h.Y * 7) % 40 - 20)); } }
			else {
			using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, h.X - 16 + sh, sy - 10, 34, 28);
			using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, h.X - 18 + sh, sy - 16, 36, 30);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 255, 255, 255))) g.FillEllipse(b, h.X - 10 + sh, sy - 12, 14, 8);
			}
			if (h.Type == 2 && !spOk) using (Pen p = new Pen(Color.FromArgb(90, 40, 20), 2f)) { g.DrawLine(p, h.X - 8, sy - 6, h.X + 2, sy + 4); g.DrawLine(p, h.X + 2, sy + 4, h.X + 10, sy - 2); }
			if (h.Type == 1 && !spOk) Txt(g, "분필", 8f, 2, Color.FromArgb(110, 110, 140), h.X - 20, sy - 9, 40, 16, StringAlignment.Center);
		}
		foreach (Db d in dbs) { float dy2 = d.Y - camY; using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 150, 110, 70))) g.FillEllipse(b, d.X - 5, dy2 - 4, 10, 8); }
		float tilt = falling ? fRot : (float)Math.Sin(t * 3) * 2.5f + sw * 6f; float tr = stam < 0.25f && !falling ? (float)Math.Sin(el * 50f) * 1.5f : 0f; { int cf = falling ? (fT > 0.35f ? 7 : 6) : (moving ? (int)(Math.Min(0.999f, mvT / Math.Max(0.01f, mvD)) * 4f) % 4 : (stam < 0.25f ? 5 : 4)); if (!Spr.Frame(g, "ms_cb" + cf, px + tr, py - camY + 76f, 0.5f, falling ? fRot : Clamp(tilt * 0.5f, -5f, 5f), false)) DrawMascot(g, px + tr, py - camY + 66f, 82f, tilt); }
		// 체력
		using (GraphicsPath gp = Gfx.Round(new RectangleF(14, 40, 170, 18), 9)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 10, 10, 20))) g.FillPath(b, gp);
		if (stam > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(14, 40, Math.Max(18f, 170f * stam), 18), 9)) using (SolidBrush b = new SolidBrush(stam > 0.5f ? Color.FromArgb(120, 230, 150) : (stam > 0.25f ? Color.FromArgb(255, 214, 90) : Color.FromArgb(255, 110, 100)))) g.FillPath(b, gp);
		Txt(g, "체력", 9.5f, 2, Color.White, 20, 40, 60, 18, StringAlignment.Near);
		if (chalkT > 0f) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(b, 184, 34, 30, 30);
		Spr.Draw(g, "ms_chalk", 198f, 49f, 22f, 26f, 0f); Spr.Draw(g, "ms_cp_helmet", 226f, 49f, 24f, 27f, 0f); Spr.Draw(g, "ms_cp_carab", 254f, 49f, 17f, 28f, 0f);
		Txt(g, "x" + chalk, 9f, 2, chalk > 0 ? Color.White : Color.FromArgb(150, 255, 255, 255), 196, 60, 30, 14, StringAlignment.Near); Txt(g, "x" + helm, 9f, 2, Color.White, 224, 60, 30, 14, StringAlignment.Near); Txt(g, "x" + carab + (anchor >= 0 ? "✔" : ""), 9f, 2, anchor >= 0 ? Color.FromArgb(120, 255, 170) : Color.White, 250, 60, 40, 14, StringAlignment.Near);
		if (anchor >= 0 && anchor < holds.Count) { Spr.Draw(g, "ms_cp_carab", holds[anchor].X, holds[anchor].Y - camY - 20f, 14f, 24f, 0f); using (Pen p = new Pen(Color.FromArgb(150, 255, 190, 80), 2f)) g.DrawLine(p, holds[anchor].X, holds[anchor].Y - camY - 20f, px, py - camY - 30f); }
		if (rockOn) { float wy2 = Math.Min(py - camY + 20f, 560f); if (rockT < 1.1f + (py - camY + 30f) / 900f) { using (SolidBrush b = new SolidBrush(Color.FromArgb(70 + (int)(60 * Math.Sin(rockT * 14)), 255, 80, 60))) g.FillEllipse(b, rockX - 30, py - camY + 12f, 60, 14); if (rockT < 1.1f) Txt(g, "!", 18f, 2, Color.FromArgb(255, 120, 90), rockX - 15, 130, 30, 26, StringAlignment.Center); else { Bitmap rb = Spr.Get("ms_rk"); if (rb != null) Spr.Draw(g, "ms_rk", rockX, rockY - 10f, 52f, 52f * rb.Height / rb.Width, 0f); else Spr.Draw(g, "stone", rockX, rockY, 36f, 36f, rockT * 400f); } } }
		if (holds.Count > 0 && Spr.Get("ms_cp_rope") != null) Spr.Draw(g, "ms_cp_rope", 128f, 548f - camY, 54f, 46f, -8f);
		if (brkT > 0f && Spr.Get("ms_hd_brk") != null) { float bk = brkT / 0.5f; Spr.Draw(g, "ms_hd_brk", brkX, brkY - camY, 56f, 56f, 0f); }
		if (popT > 0f && Spr.Get(popN) != null) { Bitmap pb = Spr.Get(popN); float ph = 120f, pw = ph * pb.Width / pb.Height; float pa = Math.Min(1f, popT * 3f); float pyy = 330f - (1.4f - popT) * 14f; using (SolidBrush sh = new SolidBrush(Color.FromArgb((int)(40 * pa), 0, 0, 0))) g.FillEllipse(sh, 20f, pyy + 56f, 100f, 12f); Spr.Draw(g, popN, 76f, pyy, pw, ph, 0f); }
		if (falling && fT > 0.35f) Spr.Draw(g, "ms_dizzy", px, py - camY - 62f, 56f, 45f, 0f);
		if (holds[cur].Type == 2 && !moving) Hint(g, "부서져요! 빨리 옮기세요", 80 + 13.0, 13f, true, 1f);
		if (msgT > 0) Txt(g, msg, 15f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 255, 255), 0, 110, W, 28, StringAlignment.Center);
		if (Score == 0 && !moving) Hint(g, "초록 테두리 손잡이를 클릭!", 130 + 12.0, 12f, false, 1f);
	}
}
