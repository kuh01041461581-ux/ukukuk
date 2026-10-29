using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 31. 프리킥 (골대 · 파워 · 감아차기 · 수비벽 · 골키퍼) ──────────────────
internal sealed class FreeKickGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private const float FL = 520f, HorY = 250f, CamH = 1.7f, CamBack = 4.5f;   // 카메라: 공 뒤 4.5m · 눈높이 1.7m
	private const float GW = 3.66f, GH = 2.44f, BR = 0.11f, G = 9.8f, Spin = 10f;
	private float cxk, cyk, accM, accDir = 1f, pwF = 1f, accDev; private bool accPh, accPerf; private int spinSel { get { return Math.Abs(cxk) < 0.2f ? 0 : (cxk > 0f ? -1 : 1); } } private int goals, lives = 3, st; private float dist, kx, camX, power, pdir = 1f, kickT, resT, rip, ripX, ripY, hitT, flyT, wz, wallJ;
	private float bx, by, bz, vx, vy, vz, rotA, rotB, lockedPower; private string msg = "", sub = "";
	private bool keeperOn, scored, decided; private int wallN; private float kpx, kpy, kTx, kTy, kDelay, kSpeed, kDive, kFx, kFy;
	private readonly List<float[]> prev = new List<float[]>(); private float pvAx = 99f, pvAy, pvSp = 99f, pvD, pvK;

	public override string Costume { get { return "축구"; } }
	public override string Name { get { return "프리킥"; } }
	public override string Key { get { return "kick"; } }
	public override string Desc { get { return "세기·감아차기로 골을 넣어요"; } }
	public override string Help { get { return "마우스로 골대 위 조준 → 왼쪽 아래 공을 눌러 맞출 곳 고르기(좌우=감기는 정도, 위=낮고 빠르게, 아래=띄워 차기 · A/D/W/S 키도 돼요)\n클릭 → 파워 게이지에서 원하는 세기에 클릭(세면 빠르지만 빗나가기 쉬워요) → 정확도 게이지 가운데 초록 칸에서 클릭하면 조준대로 정확히 차요\n처음엔 빈 골대 · 골을 넣을수록 골키퍼 → 수비벽 → 점프하는 벽 · 거리도 멀어져요 · 기회 3번"; } }
	public override string RightText { get { return "골 " + goals + " · 기회 " + lives; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (Pen p = new Pen(Color.FromArgb(235, 235, 240), 3f)) { g.DrawLine(p, cx - 20, cy + 14, cx - 20, cy - 10); g.DrawLine(p, cx - 20, cy - 10, cx + 20, cy - 10); g.DrawLine(p, cx + 20, cy - 10, cx + 20, cy + 14); }
		BallDraw(g, cx, cy + 4, 11f, 0.6f, 0.3f);
	}

	// ── 설정 ──
	public override void Reset() { goals = 0; lives = 3; Score = 0; Over = false; cxk = 0f; cyk = 0f; msg = ""; sub = ""; NewKick(); }
	private void NewKick()
	{
		dist = 16f + Math.Min(15f, goals * 1.3f) + (float)Rnd.NextDouble() * 1.5f;
		float lat = Math.Min(11f, 1.5f + goals * 1.05f); kx = goals < 1 ? 0f : (float)(Rnd.NextDouble() * 2 - 1) * lat; if (goals >= 1 && Math.Abs(kx) < 1.2f) kx = kx < 0 ? -1.6f : 1.6f;
		camX = kx * 0.85f; keeperOn = goals >= 3; wallN = goals < 5 ? 0 : Math.Min(3, 1 + (goals - 5) / 3); wz = Math.Min(9.15f, dist * 0.55f); wallJ = 0f;
		kpx = 0f; kpy = 0.95f; kTx = 0f; kTy = 0.95f; kDive = 0f; kDelay = Math.Max(0.14f, 0.34f - goals * 0.016f); kSpeed = Math.Min(3.9f, 2.2f + goals * 0.22f);
		accPh = false; pwF = 1f; accPerf = false; bx = kx; by = BR; bz = 0f; vx = vy = vz = 0f; rotA = rotB = 0f; st = 0; power = 0f; pdir = 1f; kickT = 0f; resT = 0f; scored = false; decided = false; hitT = 0f; rip = 0f; pvAx = 99f; prev.Clear();
	}
	private float WallX { get { return kx * (1f - wz / dist); } }

	// ── 화면 변환 ──
	private PointF P(float x, float y, float z) { float k = FL / Math.Max(0.4f, z + CamBack); return new PointF(200f + k * (x - camX), HorY + k * (CamH - y)); }
	private float K(float z) { return FL / Math.Max(0.4f, z + CamBack); }
	private void AimPoint(float mx, float my, out float ax, out float ay)
	{
		float k = K(dist), sx0 = 200f + k * (0f - camX), gy0 = HorY + k * CamH;
		ax = Clamp((mx - sx0) / k, -GW - 1.6f, GW + 1.6f); ay = Clamp((gy0 - my) / k, 0.05f, GH + 1.0f);
	}
	private float aimX, aimY;

	// ── 발사 ──
	private float VRef { get { return (22f + 0.3f * dist) * (1f - 0.2f * cyk) * pwF; } }
	private void Velocity(float ax, float ay, float sa, float pf, out float ox, out float oy, out float oz)
	{
		float t0 = dist / VRef; ox = ((ax - kx) / t0 - 0.5f * sa * t0) * pf; oy = ((ay - BR) / t0 + 0.5f * G * t0) * pf; oz = dist / t0 * pf;
	}
	private float SpinAcc { get { return -cxk * Spin * 1.2f; } }
	private void Sim(float ax, float ay, float pf, List<float[]> outPath, out float fx, out float fy, bool noCurve = false)
	{
		float sacc = noCurve ? 0f : SpinAcc; float x = kx, y = BR, z = 0f, ux, uy, uz; Velocity(ax, ay, SpinAcc, pf, out ux, out uy, out uz); float dt = 1f / 120f, t = 0f; fx = 0f; fy = 0f;
		for (int i = 0; i < 600; i++)
		{
			ux += sacc * dt; uy -= G * dt; x += ux * dt; y += uy * dt; z += uz * dt; t += dt; if (y < BR) { y = BR; uy = -uy * 0.5f; }
			if (outPath != null && i % 6 == 0) outPath.Add(new float[] { x, y, z });
			if (z >= dist) { fx = x; fy = y; return; }
		}
		fx = x; fy = y;
	}
	private void Kick()
	{
		pwF = 0.8f + 0.4f * lockedPower; float pf = 1f; float amp = 0.55f + 0.9f * lockedPower; float ax = aimX + accDev * 2.0f * amp, ay = Math.Max(0.05f, aimY - Math.Abs(accDev) * 0.55f * amp); Velocity(ax, ay, SpinAcc, pf, out vx, out vy, out vz); st = 3; flyT = 0f; decided = false;
		Sfx.Play("thud"); Fx.Burst(P(bx, 0.1f, 0f).X, P(bx, 0.1f, 0f).Y, Color.FromArgb(230, 255, 230), 6);
		accPerf = Math.Abs(accDev) < 0.001f; sub = accPerf ? "정확하게 맞췄어요!" : (Math.Abs(accDev) < 0.25f ? "살짝 빗맞았어요" : "많이 빗맞았어요");
		if (keeperOn)
		{
			float fx, fy, tx2, ty2; Sim(ax, ay, pf, null, out fx, out fy); Sim(ax, ay, pf, null, out tx2, out ty2, true); float sig = Math.Max(0.3f, 1.2f - goals * 0.07f);
			kTx = Clamp(tx2 + (float)(Rnd.NextDouble() * 2 - 1) * sig, -GW - 0.3f, GW + 0.3f); kTy = Clamp(ty2, 0.45f, 2.1f); kFx = fx; kFy = fy;   // 처음엔 감아차기를 모르고 직선 길로 읽다가, 휘는 걸 보고 뒤늦게 고쳐요
			if (Math.Abs(tx2) > GW + 1.2f) { kTx = kpx; kTy = kpy; }
		}
		wallJ = (wallN > 0 && goals >= 10 && Rnd.NextDouble() < 0.5) ? 1f : 0f;
	}

	public override void Click(float x, float y)
	{
		if (st >= 2) return;
		{ float ddx = x - 58f, ddy = y - 560f; if (st == 0 && ddx * ddx + ddy * ddy <= 36f * 36f) { cxk = Clamp(ddx / 30f, -1f, 1f); cyk = Clamp(ddy / 30f, -1f, 1f); if (Math.Abs(cxk) < 0.12f) cxk = 0f; if (Math.Abs(cyk) < 0.12f) cyk = 0f; Sfx.Play("tick"); return; } }
		if (st == 0) { st = 1; power = 0f; pdir = 1f; accPh = false; Sfx.Play("whoosh"); }
		else if (st == 1 && !accPh) { lockedPower = power; accPh = true; accM = 0f; accDir = 1f; Sfx.Play("tick"); }
		else if (st == 1) { float nrm = (accM - 0.5f) * 2f, gz = Math.Max(0.07f, 0.22f - goals * 0.008f), aa = Math.Abs(nrm); accDev = aa <= gz ? 0f : Math.Sign(nrm) * (aa - gz) / (1f - gz); st = 2; kickT = 0f; }
	}
	public override void Press(Keys k)
	{
		if (st != 0) { if (k == Keys.Space || k == Keys.Enter) Click(200f, 300f); return; }
		if (k == Keys.A || k == Keys.Left) { cxk = Math.Max(-1f, cxk - 0.25f); Sfx.Play("tick"); } else if (k == Keys.D || k == Keys.Right) { cxk = Math.Min(1f, cxk + 0.25f); Sfx.Play("tick"); } else if (k == Keys.S || k == Keys.Down) { cxk = 0f; cyk = 0f; Sfx.Play("tick"); } else if (k == Keys.W || k == Keys.Up) { cyk = Math.Max(-1f, cyk - 0.25f); Sfx.Play("tick"); }
		else if (k == Keys.Space || k == Keys.Enter) Click(200f, 300f);
	}

	private void Result(string m, string s, int pts, bool goal)
	{
		decided = true; msg = m; if (s.Length > 0) sub = s; resT = 1.7f; st = 4;
		if (goal) { goals++; Score += pts; scored = true; rip = 1f; ripX = bx; ripY = by; Sfx.Play(pts >= 15 ? "homerun" : "fanfare"); Shake = pts >= 15 ? 0.7f : 0.45f; PointF q = P(bx, by, dist); Fx.Burst(q.X, q.Y, Color.FromArgb(255, 226, 120), pts >= 15 ? 18 : 12); Fx.Burst(200f, 200f, Color.FromArgb(255, 255, 255), 8); }
		else { lives--; Sfx.Play("bad2"); Shake = 0.2f; }
	}

	public override void Update(float dt, GameInput inp, double t)
	{
		if (st <= 1 && inp.MouseMoved) { }
		AimPoint(inp.Mx, inp.My, out aimX, out aimY);
		if (rip > 0f) rip -= dt * 0.8f; if (hitT > 0f) hitT -= dt;
		if (st == 1 && accPh) { float sp3 = 1.5f + Math.Min(1.6f, goals * 0.1f); accM += accDir * dt * sp3; if (accM >= 1f) { accM = 1f; accDir = -1f; } else if (accM <= 0f) { accM = 0f; accDir = 1f; } }
		else if (st == 1) { power += pdir * dt * 1.25f; if (power >= 1f) { power = 1f; pdir = -1f; } else if (power <= 0f) { power = 0f; pdir = 1f; } }
		else if (st == 2) { kickT += dt; if (kickT >= 0.3f) Kick(); }
		else if (st == 3) Fly(dt);
		else if (st == 4) { resT -= dt; if (scored) { bx += vx * dt; bz = Math.Min(dist + 2.0f, bz + vz * dt); vx *= 0.96f; vz *= 0.93f; vy -= G * dt; by = Math.Max(BR, by + vy * dt); if (by <= BR) vy = 0f; } if (resT <= 0f) { if (lives <= 0) { Over = true; st = 5; } else NewKick(); } }
		if (st == 3 || st == 4) { rotA += 9f * dt; rotB += spinSel * 7f * dt; }
	}

	private void Fly(float dt)
	{
		int n = Math.Max(1, (int)Math.Ceiling(dt / (1f / 240f))); float sd = dt / n; flyT += dt;
		for (int i = 0; i < n && st == 3; i++)
		{
			float ox = bx, oz = bz; vx += SpinAcc * sd; vy -= G * sd; bx += vx * sd; by += vy * sd; bz += vz * sd;
			if (by < BR) { by = BR; vy = -vy * 0.5f; vx *= 0.92f; vz *= 0.9f; if (Math.Abs(vy) > 1.5f) Sfx.Play("thud_soft"); }
			// 키퍼
			if (keeperOn) { float kt = flyT - kDelay; if (kt > 0.4f && spinSel != 0) { kTx = kFx; kTy = Clamp(kFy, 0.45f, 2.1f); } if (kt > 0f) { kDive = Math.Min(1f, kDive + sd * 3f); kpx += Math.Sign(kTx - kpx) * Math.Min(Math.Abs(kTx - kpx), kSpeed * sd); kpy += Math.Sign(kTy - kpy) * Math.Min(Math.Abs(kTy - kpy), 4.2f * sd); } }
			// 수비벽
			if (wallN > 0 && oz < wz && bz >= wz)
			{
				float wh = 1.8f + (wallJ > 0f && flyT > 0.12f ? 0.45f : 0f), half = wallN * 0.26f;
				if (Math.Abs(bx - WallX) < half + BR && by < wh + BR) { vz = -vz * 0.15f; vx *= 0.3f; vy = Math.Max(vy, 1f); Sfx.Play("bong"); Fx.Burst(P(bx, by, wz).X, P(bx, by, wz).Y, Color.FromArgb(255, 200, 150), 6); Shake = 0.25f; Result("수비벽에 막혔어요 (반려)", wallN >= 3 ? "감아차기로 벽을 돌아 보세요" : "조금 더 높게 · 감아서 차 보세요", 0, false); return; }
			}
			// 골 라인
			if (oz < dist && bz >= dist)
			{
				float px2 = Math.Abs(bx), dp = Math.Abs(px2 - GW), db = Math.Abs(by - GH);
				if ((dp < BR + 0.07f && by < GH + BR) || (db < BR + 0.07f && px2 < GW + BR))
				{ vz = -vz * 0.35f; vx *= 0.5f; Sfx.Play("bong"); Shake = 0.35f; Fx.Burst(P(bx, by, dist).X, P(bx, by, dist).Y, Color.FromArgb(255, 255, 255), 8); Result(db < dp ? "크로스바!" : "골대를 맞췄어요!", "아깝다!", 0, false); bz = dist - 0.2f; return; }
				if (px2 < GW && by < GH)
				{
					if (keeperOn && kDive > 0f)
					{
						float dx = bx - kpx, dy = by - kpy; float reach = 0.85f + 0.2f * kDive;
						if (dx * dx + dy * dy * 0.8f < reach * reach) { vz = -vz * 0.3f; vx *= 0.4f; vy = 1.2f; Sfx.Play("whoosh"); hitT = 0.5f; Fx.Burst(P(bx, by, dist).X, P(bx, by, dist).Y, Color.FromArgb(255, 220, 90), 8); Result("키퍼가 막았어요! 결재 불가", "코너를 노려 보세요", 0, false); bz = dist - 0.2f; return; }
					}
					bool corner = px2 > GW - 1.1f && by > GH - 0.85f; int pts = 10 + (corner ? 8 : 0) + (int)Math.Round(Math.Max(0f, dist - 16f) * 0.6f) + (accPerf ? 3 : 0) + (int)Math.Round(lockedPower * 3f);
					Result(corner ? "슈퍼 골!!" : "골!!", corner ? "상단 구석! 대단해요" : (sub.Length > 0 ? sub : ""), pts, true); return;
				}
				Result(by > GH ? "크로스바 위로 떴어요" : "골대를 벗어났어요", by > GH ? "파워를 조금 낮춰 보세요" : (bx < 0 ? "왼쪽으로 빗나갔어요" : "오른쪽으로 빗나갔어요"), 0, false); return;
			}
			if (bz > 0.5f && vz < 1.2f && by <= BR + 0.02f) { Result("힘이 부족했어요", "파워 게이지를 초록 칸에서 눌러 보세요", 0, false); return; }
			if (flyT > 3.2f || bz < -0.5f) { Result("빗나갔어요", "", 0, false); return; }
		}
	}

	// ── 그리기 ──
	private static void BallDraw(Graphics g, float cx, float cy, float r, float ra, float rb)
	{
		if (r < 1f) return; if (r > 3.2f && Spr.Draw(g, "ms_n_sball", cx, cy, r * 2f, r * 2f, (ra + rb) * 57.3f)) return; GraphicsState gs = g.Save();
		using (GraphicsPath cp = new GraphicsPath()) { cp.AddEllipse(cx - r, cy - r, r * 2, r * 2); g.SetClip(cp, CombineMode.Intersect);
			using (PathGradientBrush pb = new PathGradientBrush(cp)) { pb.CenterPoint = new PointF(cx - r * 0.3f, cy - r * 0.35f); pb.CenterColor = Color.White; pb.SurroundColors = new Color[] { Color.FromArgb(176, 184, 198) }; g.FillPath(pb, cp); }
			if (r > 3.2f)
			{
				float phi = 1.618f; float[][] vs = { new[] { 0f, 1f, phi }, new[] { 0f, -1f, phi }, new[] { 0f, 1f, -phi }, new[] { 0f, -1f, -phi }, new[] { 1f, phi, 0f }, new[] { -1f, phi, 0f }, new[] { 1f, -phi, 0f }, new[] { -1f, -phi, 0f }, new[] { phi, 0f, 1f }, new[] { -phi, 0f, 1f }, new[] { phi, 0f, -1f }, new[] { -phi, 0f, -1f } };
				float ca = (float)Math.Cos(ra), sa = (float)Math.Sin(ra), cb = (float)Math.Cos(rb), sb = (float)Math.Sin(rb);
				using (SolidBrush pbk = new SolidBrush(Color.FromArgb(235, 30, 34, 44)))
					foreach (float[] v0 in vs)
					{
						float l = (float)Math.Sqrt(v0[0] * v0[0] + v0[1] * v0[1] + v0[2] * v0[2]), x0 = v0[0] / l, y0 = v0[1] / l, z0 = v0[2] / l;
						float y1 = y0 * ca - z0 * sa, z1 = y0 * sa + z0 * ca; float x2 = x0 * cb + z1 * sb, z2 = -x0 * sb + z1 * cb; if (z2 < 0.12f) continue;
						float tx = z2, ty = 0f, tz = -x2; float tl = (float)Math.Sqrt(tx * tx + tz * tz); if (tl < 1e-3f) continue; tx /= tl; tz /= tl; float ux = y1 * tz - z2 * ty, uy = z2 * tx - x2 * tz, uz = x2 * ty - y1 * tx;
						PointF[] pts = new PointF[5];
						for (int q = 0; q < 5; q++) { double an = q * Math.PI * 2 / 5; float c1 = (float)Math.Cos(an) * 0.37f, s1 = (float)Math.Sin(an) * 0.37f; float px = x2 + (tx * c1 + ux * s1), py = y1 + (ty * c1 + uy * s1); pts[q] = new PointF(cx + px * r, cy - py * r); }
						g.FillPolygon(pbk, pts);
					}
			}
			using (SolidBrush hl = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(hl, cx - r * 0.55f, cy - r * 0.7f, r * 0.7f, r * 0.45f);
		}
		g.Restore(gs); using (Pen o = new Pen(Color.FromArgb(120, 40, 44, 56), 1f)) g.DrawEllipse(o, cx - r, cy - r, r * 2, r * 2);
	}
	private void Person(Graphics g, float x, float z, float h, Color jersey, Color shorts, float lean, float armsUp, bool keeper)
	{
		PointF b = P(x, 0f, z); float k = K(z), pxh = h * k, w = pxh * 0.3f; if (b.Y < HorY - 5f) return;
		GraphicsState gs = g.Save(); g.TranslateTransform(b.X, b.Y); g.RotateTransform(lean);
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, -w * 0.9f, -pxh * 0.03f, w * 1.8f, pxh * 0.07f);
		using (SolidBrush lg = new SolidBrush(Color.FromArgb(232, 190, 150))) { g.FillRectangle(lg, -w * 0.42f, -pxh * 0.42f, w * 0.34f, pxh * 0.4f); g.FillRectangle(lg, w * 0.08f, -pxh * 0.42f, w * 0.34f, pxh * 0.4f); }
		using (SolidBrush sk = new SolidBrush(Color.FromArgb(240, 245, 245, 250))) { g.FillRectangle(sk, -w * 0.42f, -pxh * 0.2f, w * 0.34f, pxh * 0.2f); g.FillRectangle(sk, w * 0.08f, -pxh * 0.2f, w * 0.34f, pxh * 0.2f); }
		using (SolidBrush sb = new SolidBrush(shorts)) g.FillRectangle(sb, -w * 0.5f, -pxh * 0.55f, w, pxh * 0.17f);
		using (GraphicsPath tp = Gfx.Round(new RectangleF(-w * 0.55f, -pxh * 0.86f, w * 1.1f, pxh * 0.36f), Math.Max(2f, w * 0.25f))) using (SolidBrush jb = new SolidBrush(jersey)) g.FillPath(jb, tp);
		using (Pen ap = new Pen(jersey, Math.Max(2f, w * 0.26f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { float ay = -pxh * 0.8f; float ex = w * (0.7f + 0.5f * armsUp), ey = ay - pxh * (0.12f + 0.3f * armsUp); g.DrawLine(ap, -w * 0.5f, ay, -ex, ey); g.DrawLine(ap, w * 0.5f, ay, ex, ey); }
		if (keeper) using (SolidBrush gl = new SolidBrush(Color.FromArgb(255, 250, 250, 250))) { float ay = -pxh * 0.8f, ex = w * (0.7f + 0.5f * armsUp), ey = ay - pxh * (0.12f + 0.3f * armsUp); g.FillEllipse(gl, -ex - w * 0.15f, ey - w * 0.15f, w * 0.34f, w * 0.34f); g.FillEllipse(gl, ex - w * 0.19f, ey - w * 0.15f, w * 0.34f, w * 0.34f); }
		using (SolidBrush hb = new SolidBrush(Color.FromArgb(236, 196, 156))) g.FillEllipse(hb, -w * 0.3f, -pxh * 1.0f, w * 0.6f, w * 0.64f);
		using (SolidBrush hr = new SolidBrush(Color.FromArgb(50, 40, 36))) g.FillPie(hr, -w * 0.3f, -pxh * 1.0f, w * 0.6f, w * 0.64f, 180f, 180f);
		g.Restore(gs);
	}
	private void Net(Graphics g, float bulgeX, float bulgeY, float bulge)
	{
		float zb = dist + 2.1f;
		using (Pen np = new Pen(Color.FromArgb(70, 240, 240, 250), 1f))
		{
			for (int xi = -7; xi <= 7; xi++)
			{
				float x = xi * GW / 7f; PointF[] pl = new PointF[11];
				for (int yi = 0; yi <= 10; yi++) { float y = yi * GH / 10f; float d = ((x - bulgeX) * (x - bulgeX) + (y - bulgeY) * (y - bulgeY)); pl[yi] = P(x, y * 0.92f, zb - bulge * 0.9f * (float)Math.Exp(-d / 1.6f)); }
				g.DrawLines(np, pl);
			}
			for (int yi = 0; yi <= 10; yi++)
			{
				float y = yi * GH / 10f; PointF[] pl = new PointF[15];
				for (int xi = 0; xi < 15; xi++) { float x = (xi - 7) * GW / 7f; float d = ((x - bulgeX) * (x - bulgeX) + (y - bulgeY) * (y - bulgeY)); pl[xi] = P(x, y * 0.92f, zb - bulge * 0.9f * (float)Math.Exp(-d / 1.6f)); }
				g.DrawLines(np, pl);
			}
			for (int s = -1; s <= 1; s += 2) for (int yi = 0; yi <= 5; yi++) { float y = yi * GH / 5f; g.DrawLine(np, P(s * GW, y, dist), P(s * GW, y * 0.92f, zb)); }
			for (int xi = -3; xi <= 3; xi++) { float x = xi * GW / 3f; g.DrawLine(np, P(x, GH, dist), P(x, GH * 0.92f, zb)); }
		}
	}
	public override void Draw(Graphics g, double tm)
	{
		// 하늘 · 관중석 · 광고판
		Bitmap sb0 = Spr.Get("bg_soccer");
		if (sb0 != null) { using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, 0, W, 96), Color.FromArgb(4, 8, 44), Color.FromArgb(6, 14, 74), 90f)) g.FillRectangle(lb, 0, 0, W, 96); float bsc = ImgCache.Scale(g); System.Drawing.Image bim = ImgCache.Get(sb0, W * bsc, H * bsc); InterpolationMode m0 = g.InterpolationMode; PixelOffsetMode po0 = g.PixelOffsetMode; CompositingMode cm0 = g.CompositingMode; g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(bim, new Rectangle(0, 84, W, H - 84), 0, 0, bim.Width, (int)(bim.Height * (H - 84f) / H), GraphicsUnit.Pixel); g.InterpolationMode = m0; g.PixelOffsetMode = po0; g.CompositingMode = cm0; }
		else {
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, 0, W, HorY + 2), Color.FromArgb(24, 34, 74), Color.FromArgb(96, 130, 190), 90f)) g.FillRectangle(lb, 0, 0, W, HorY + 2);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(48, 52, 76))) g.FillRectangle(b, 0, HorY - 62, W, 62); }
		if (sb0 == null) { Color[] cc = { Color.FromArgb(232, 84, 92), Color.FromArgb(250, 210, 90), Color.FromArgb(120, 190, 250), Color.FromArgb(240, 240, 250), Color.FromArgb(120, 210, 150) }; for (int row = 0; row < 4; row++) for (int i = 0; i < 44; i++) { int sd = row * 53 + i * 17; using (SolidBrush b = new SolidBrush(cc[sd % 5])) g.FillEllipse(b, (i * 9.3f + row * 4f) % 400f, HorY - 58 + row * 13f, 5f, 6f); } }
		if (sb0 == null) {
		FloodTower(g, 28f, 10f, HorY - 58f, 4, 3, 140f, HorY + 10f, tm); FloodTower(g, W - 28f, 10f, HorY - 58f, 4, 3, W - 140f, HorY + 10f, tm);
		FloodTower(g, 92f, 52f, HorY - 58f, 3, 2, 170f, HorY, tm); FloodTower(g, W - 92f, 52f, HorY - 58f, 3, 2, W - 170f, HorY, tm);
		}
		if (sb0 == null) { Color[] ad = { Color.FromArgb(230, 60, 80), Color.FromArgb(40, 130, 220), Color.FromArgb(250, 180, 40), Color.FromArgb(40, 170, 120) }; float zA = dist + 6f; for (int i = -5; i < 5; i++) { PointF a1 = P(i * 5f, 0f, zA), a2 = P(i * 5f + 5f, 1.0f, zA); using (SolidBrush b = new SolidBrush(ad[(i + 5) % 4])) g.FillRectangle(b, Math.Min(a1.X, a2.X), a2.Y, Math.Abs(a2.X - a1.X) + 0.5f, Math.Abs(a1.Y - a2.Y)); } PointF t1 = P(-12f, 0.5f, zA), t2 = P(12f, 0.5f, zA); Txt(g, "마룡 · 익산시", 8.5f, 2, Color.FromArgb(235, 255, 255, 255), (t1.X + t2.X) / 2f - 60f, t1.Y - 7f, 120f, 14f, StringAlignment.Center); }
		// 잔디 (원근 줄무늬)
		if (sb0 == null) using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 128, 66))) g.FillRectangle(b, 0, HorY, W, H - HorY);
		if (sb0 == null) for (int i = 0; i < 18; i++) { float z0 = -CamBack + 0.5f + i * i * 0.22f + i * 1.4f, z1 = z0 + (1.4f + i * 0.44f) * 0.5f; if (i % 2 != 0) continue; PointF a = P(-80f, 0f, z0), bb = P(80f, 0f, z0), c2 = P(80f, 0f, z1), d = P(-80f, 0f, z1); using (SolidBrush b = new SolidBrush(Color.FromArgb(24, 255, 255, 255))) g.FillPolygon(b, new PointF[] { a, bb, c2, d }); }
		using (Pen lp = new Pen(Color.FromArgb(200, 255, 255, 255), 2f))
		{
			g.DrawLine(lp, P(-30f, 0f, dist), P(30f, 0f, dist));
			float bz0 = dist - 16.5f, sz0 = dist - 5.5f; if (bz0 > -CamBack + 1f) { g.DrawLine(lp, P(-20.15f, 0f, bz0), P(20.15f, 0f, bz0)); g.DrawLine(lp, P(-20.15f, 0f, bz0), P(-20.15f, 0f, dist)); g.DrawLine(lp, P(20.15f, 0f, bz0), P(20.15f, 0f, dist)); }
			g.DrawLine(lp, P(-9.16f, 0f, sz0), P(9.16f, 0f, sz0)); g.DrawLine(lp, P(-9.16f, 0f, sz0), P(-9.16f, 0f, dist)); g.DrawLine(lp, P(9.16f, 0f, sz0), P(9.16f, 0f, dist));
		}
		// 골대 뒤 그물 → 키퍼 → 골대 틀 → 수비벽 → 공
		Net(g, ripX, ripY, Math.Max(0f, rip));
		if (keeperOn)
		{
			float dive = kDive * (kTx - 0f); float lean = Clamp((kpx - 0f) * 6f * kDive, -70f, 70f) * (Math.Abs(kTx) > 0.5f ? 1f : 0.3f);
			float kz = dist - 0.2f, kk = K(kz), ksc = 1.7f * kk / 240f; PointF kb = P(kpx, 0f, kz); float jy = Math.Max(0f, kpy - 0.95f);
			using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, kb.X - 0.45f * kk, kb.Y - 0.03f * kk, 0.9f * kk, 0.1f * kk);
			bool central = Math.Abs(kTx) < 0.7f; float dir = kTx < 0f ? -1f : 1f; string fn; bool flipK = false; bool horiz = false;
			if (kDive < 0.08f) fn = kpy > 1.3f ? "ms_gk1" : "ms_gk0";
			else if (central) fn = kDive < 0.5f ? "ms_gk1" : "ms_gk6";
			else { horiz = true; if (kDive < 0.62f) { fn = dir < 0f ? "ms_gk2" : "ms_gk4"; } else { fn = dir < 0f ? "ms_gk3" : "ms_gk5"; flipK = dir > 0f; } }
			if (Spr.Get(fn) != null)
			{
				Bitmap kbm = Spr.Get(fn); float hh = Spr.OH(fn, kbm) * ksc;
				float bottom = horiz ? kb.Y - Math.Max(0.18f, kpy * (kDive < 0.62f ? 1f : 0.35f)) * kk + hh / 2f : kb.Y - jy * kk * 0.9f - (kDive >= 0.5f && central ? 0.25f * kk : 0f);
				Spr.Frame(g, fn, kb.X, bottom, ksc, horiz ? dir * 6f * kDive : 0f, flipK);
			}
			else Person(g, kpx, kz, 1.85f, Color.FromArgb(246, 200, 40), Color.FromArgb(30, 30, 40), 0f, kDive > 0f ? 1f : 0.5f, true);
		}
		float zg = dist; using (Pen fp = new Pen(Color.FromArgb(250, 250, 250), Math.Max(3f, K(zg) * 0.13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
		{ PointF l0 = P(-GW, 0f, zg), l1 = P(-GW, GH, zg), r1 = P(GW, GH, zg), r0 = P(GW, 0f, zg); g.DrawLines(fp, new PointF[] { l0, l1, r1, r0 }); }
		if (wallN > 0)
		{
			float jmp = (wallJ > 0f && st == 3 && flyT > 0.12f && flyT < 0.75f) ? (float)Math.Sin((flyT - 0.12f) / 0.63f * Math.PI) * 0.55f : 0f;
			for (int i = 0; i < wallN; i++)
			{
				float wx = WallX + (i - (wallN - 1) / 2f) * 0.52f; PointF fb = P(wx, jmp, wz); PointF fg0 = P(wx, 0f, wz); float wk = K(wz), wsc = 1.75f * wk / 220f;
				string wn = jmp > 0.3f ? "ms_wl2" : (jmp > 0.02f ? "ms_wl1" : "ms_wl0");
				using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, fg0.X - 0.4f * wk, fg0.Y - 0.03f * wk, 0.8f * wk, 0.1f * wk);
				if (!Spr.Frame(g, wn, fb.X, fb.Y, wsc, 0f, false)) Person(g, wx, wz, 1.85f, Color.FromArgb(220, 60, 70), Color.FromArgb(240, 240, 245), 0f, 0f, false);
			}
			if (jmp > 0f) { }
		}
		// 예상 경로 (점선)
		if (st <= 1) { if (Math.Abs(pvAx - aimX) > 0.03f || Math.Abs(pvAy - aimY) > 0.03f || pvSp != SpinAcc || pvD != dist + cyk || pvK != kx) { pvAx = aimX; pvAy = aimY; pvSp = SpinAcc; pvD = dist + cyk; pvK = kx; prev.Clear(); float fx, fy; Sim(aimX, aimY, 1f, prev, out fx, out fy); }
			for (int i = 0; i < prev.Count; i++) { float[] q = prev[i]; PointF pp = P(q[0], q[1], q[2]); float rr = Math.Max(1.4f, K(q[2]) * 0.05f); using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Min(230, 90 + i * 6), 255, 240, 140))) g.FillEllipse(b, pp.X - rr, pp.Y - rr, rr * 2, rr * 2); }
			PointF aim = P(aimX, aimY, dist); float ar = Math.Max(8f, K(dist) * 0.32f); using (Pen ap = new Pen(Color.FromArgb(235, 255, 240, 120), 2f)) { g.DrawEllipse(ap, aim.X - ar, aim.Y - ar, ar * 2, ar * 2); g.DrawLine(ap, aim.X - ar - 5, aim.Y, aim.X + ar + 5, aim.Y); g.DrawLine(ap, aim.X, aim.Y - ar - 5, aim.X, aim.Y + ar + 5); }
		}
		// 공
		{ PointF bp = P(bx, by, bz), sp = P(bx, 0f, bz); float k = K(bz), r = Math.Max(3f, BR * k * 1.15f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, sp.X - r, sp.Y - r * 0.3f, r * 2, r * 0.6f);
		if (st == 3) for (int i = 1; i <= 4; i++) { float bz2 = bz - vz * 0.012f * i, bx2 = bx - vx * 0.012f * i, by2 = by - vy * 0.012f * i; PointF tp = P(bx2, Math.Max(BR, by2), bz2); float tr = Math.Max(2f, BR * K(bz2) * 1.1f); using (SolidBrush b = new SolidBrush(Color.FromArgb(40 - i * 8, 255, 255, 255))) g.FillEllipse(b, tp.X - tr, tp.Y - tr, tr * 2, tr * 2); }
		BallDraw(g, bp.X, bp.Y, r, rotA, rotB); }
		// 차는 마룡이
		{ float kk = st == 2 ? Clamp(kickT / 0.3f, 0f, 1f) : (st >= 3 ? 1f : 0f); float mx0 = 70f + kk * 40f;
			string kf = st == 2 ? (kk < 0.25f ? "ms_kk1" : (kk < 0.5f ? "ms_kk2" : (kk < 0.78f ? "ms_kk3" : "ms_kk4"))) : (st >= 3 ? (flyT < 0.18f ? "ms_kk5" : (scored ? "ms_kk7" : "ms_kk6")) : "ms_kk0");
			float bob = st < 2 ? (float)Math.Sin(tm * 3) * 1.5f : 0f;
			using (SolidBrush shb = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(shb, mx0 - 34f, 502f, 68f, 12f);
			if (!Spr.Frame(g, kf, mx0, 510f + bob * 0.4f, 0.47f, 0f, false)) DrawSpr(g, st == 2 ? (kk < 0.5f ? "n_ks2" : "n_ks3") : (st >= 3 ? (scored ? "n_ks4" : "n_ks3") : "n_ks1"), mx0, 508f, 100f, 0f); }
		// HUD
		Txt(g, "거리 " + (int)Math.Round(dist) + "m" + (wallN > 0 ? " · 벽 " + wallN + "명" : "") + (keeperOn ? " · 골키퍼" : " · 빈 골대"), 10.5f, 2, Color.FromArgb(235, 255, 255, 255), 0, 40, W, 18, StringAlignment.Center);
		if (st <= 1 && msg.Length == 0) { }
		if (st == 0) { using (GraphicsPath hp0 = Gfx.Round(new RectangleF(40f, 108f, W - 80f, 24f), 12)) using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 8, 20, 50))) g.FillPath(hb, hp0); Txt(g, goals == 0 ? "조준 · 공 맞출 곳 고르기 → 클릭해서 파워 → 정확도!" : "공 맞출 곳을 고르고 클릭해요", 12f, 2, Color.White, 40, 108, W - 80, 24, StringAlignment.Center); }
		if (st <= 2)
		{
			// ① 공 맞출 곳 (좌우 = 감기는 정도, 위아래 = 띄우기)
			float bcx = 58f, bcy = 560f; bool pick = st == 0;
			using (SolidBrush b = new SolidBrush(Color.FromArgb(pick ? 150 : 90, 8, 14, 30))) g.FillEllipse(b, bcx - 36f, bcy - 36f, 72f, 72f);
			BallDraw(g, bcx, bcy, 28f, 0.5f, 0.3f);
			using (Pen cp = new Pen(Color.FromArgb(110, 255, 255, 255), 1f)) { g.DrawLine(cp, bcx - 28f, bcy, bcx + 28f, bcy); g.DrawLine(cp, bcx, bcy - 28f, bcx, bcy + 28f); }
			float dx0 = bcx + cxk * 24f, dy0 = bcy + cyk * 24f; using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 90, 70))) g.FillEllipse(b, dx0 - 5f, dy0 - 5f, 10f, 10f); using (Pen cp = new Pen(Color.White, 1.6f)) g.DrawEllipse(cp, dx0 - 5f, dy0 - 5f, 10f, 10f);
			using (Pen cp = new Pen(Color.FromArgb(pick ? 230 : 120, 255, 240, 140), 2f)) g.DrawEllipse(cp, bcx - 36f, bcy - 36f, 72f, 72f);
			string curl = Math.Abs(cxk) < 0.12f ? "직선" : (cxk > 0f ? "왼쪽으로 " : "오른쪽으로 ") + (int)Math.Round(Math.Abs(cxk) * 100f) + "% 감김";
			string lof = Math.Abs(cyk) < 0.12f ? "보통 높이" : (cyk > 0f ? "띄워 차기 (느리고 높게)" : "낮고 빠르게");
			using (GraphicsPath tp2 = Gfx.Round(new RectangleF(96f, 545f, 296f, pick ? 50f : 36f), 10)) using (SolidBrush tb2 = new SolidBrush(Color.FromArgb(140, 8, 14, 30))) g.FillPath(tb2, tp2);
			Txt(g, "공 맞출 곳", 9.5f, 1, Color.FromArgb(220, 255, 255, 255), 100f, 548f, 120f, 14f, StringAlignment.Near);
			Txt(g, curl + " · " + lof, 10.5f, 2, Color.FromArgb(255, 255, 240, 150), 100f, 563f, 290f, 16f, StringAlignment.Near);
			if (pick) Txt(g, "공을 눌러 정해요 (오른쪽을 차면 왼쪽으로 휘어요)", 8.5f, 0, Color.FromArgb(235, 255, 255, 255), 100f, 579f, 290f, 14f, StringAlignment.Near);
			// ② 파워 → ③ 정확도 게이지
			RectangleF gr = new RectangleF(110f, 524f, 260f, 18f); using (GraphicsPath gp = Gfx.Round(gr, 9)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 8, 14, 30))) g.FillPath(b, gp);
			if (!accPh)
			{
				float pv2 = st == 2 ? lockedPower : power; if (st >= 1) using (GraphicsPath gp = Gfx.Round(new RectangleF(gr.X, gr.Y, Math.Max(18f, gr.Width * pv2), gr.Height), 9)) using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 255 - (int)(120 * pv2), 214 - (int)(90 * pv2), 90))) g.FillPath(b, gp);
				using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, gr.X + gr.Width * pv2 - 2.5f, gr.Y - 4, 5f, gr.Height + 8);
				Txt(g, "약", 8.5f, 1, Color.FromArgb(200, 255, 255, 255), gr.X, gr.Y - 15, 30, 14, StringAlignment.Near); Txt(g, "세게 (빠르지만 빗나가기 쉬워요)", 8.5f, 1, Color.FromArgb(200, 255, 255, 255), gr.Right - 160, gr.Y - 15, 160, 14, StringAlignment.Far);
				if (st == 1) Hint(g, "원하는 세기에서 클릭!", 62 + 11.0, 12f, true, 1f);
			}
			else
			{
				float gz = Math.Max(0.07f, 0.22f - goals * 0.008f); float zw0 = gr.Width * gz;
				using (SolidBrush zb = new SolidBrush(Color.FromArgb(210, 110, 230, 150))) g.FillRectangle(zb, gr.X + gr.Width * 0.5f - zw0, gr.Y + 2, zw0 * 2f, gr.Height - 4);
				using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, gr.X + gr.Width * accM - 2.5f, gr.Y - 4, 5f, gr.Height + 8);
				Hint(g, "정확도: 가운데 초록 칸에서 클릭!", 62 + 11.0, 12f, true, 1f);
				Txt(g, "파워 " + (int)Math.Round(lockedPower * 100f) + "%", 8.5f, 1, Color.FromArgb(200, 255, 255, 255), gr.X, gr.Y - 15, 90, 14, StringAlignment.Near);
			}
		}
		for (int fi = 0; fi < 3; fi++) if (Spr.Get("ms_sc_flag") != null) DrawFx(g, "sc_flag", W - 22f - fi * 26f, 92f, 22f, fi < lives ? 1f : 0.25f);
		if (st >= 3 && msg.Length > 0 && resT > 0f)
		{
			int al = (int)(255 * Math.Min(1f, resT)); bool good = scored; Txt(g, msg, 24f, 2, good ? Color.FromArgb(al, 255, 226, 100) : Color.FromArgb(al, 255, 150, 160), 0, 120, W, 42, StringAlignment.Center);
			if (sub.Length > 0) Txt(g, sub, 12f, 1, Color.FromArgb((int)(220 * Math.Min(1f, resT)), 255, 255, 255), 0, 166, W, 22, StringAlignment.Center);
			{ float cal = Math.Min(1f, resT); string cn = scored ? "ms_sc_whis" : (lives <= 0 ? "ms_sc_red" : "ms_sc_yel"); if (Spr.Get(cn) != null) { float cs = 0.5f + 0.5f * Math.Min(1f, (1.7f - resT) * 6f); if (scored) Spr.Draw(g, cn, 46f, 138f, 44f * cs, 35f * cs, -12f); else Spr.Draw(g, cn, 352f, 142f, 26f * cs, 34f * cs, 10f); } }
		}
	}
}
