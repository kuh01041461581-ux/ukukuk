using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 마룡이 카트: 마리오카트·카트라이더식 유사 3D 레이싱 (3바퀴, 6대) ──
//  드리프트(스페이스·Shift·마우스 누르기)로 게이지를 모았다가 놓으면 부스터! 아이템 상자: 부스터 · 바나나 · 물풍선
internal sealed class KartGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	public override string Name { get { return "마룡이 카트"; } }
	public override string Key { get { return "kart"; } }
	public override string Desc { get { return "마룡이 마을 순환로 · CPU 5대와 3바퀴"; } }
	public override string Help { get { return "←→ (또는 마우스 좌우)로 핸들 · 가속은 자동, ↓ 브레이크\n커브에서 스페이스·Shift(또는 마우스를 누른 채)로 드리프트 → 게이지를 모아 놓으면 부스터!\n아이템 상자: 부스터 · 바나나(뒤에 놓기) · 물풍선(앞차 맞히기) — Z·X·Ctrl 또는 아이템 칸 클릭으로 사용\n길 밖 잔디는 느려져요 · 3바퀴를 먼저 돌면 1등! · 갈고리 U턴 두 번 · S자 · 긴 내리막 커브 · 계단 커브 — 드리프트로 부스터를 모아요\nCPU 5대도 아이템을 써요 (물풍선 조심!)"; } }
	public override string RightText { get { return lap < Laps ? (lap + 1) + "/" + Laps + "바퀴" : "완주"; } }
	public override void Icon(Graphics g, float cx, float cy) { DrawKart(g, cx, cy + 16, 34f, Color.FromArgb(40, 170, 150), true, 0f, 0f, "ms_kart_me0"); }

	private const int N = 1260, DRAW = 140, Laps = 3; private const float SEG = 200f, ROADW = 2000f, CAMH = 1000f, DEPTH = 0.84f, MAXV = 10000f, LEN = N * SEG;
	private readonly float[] curve = new float[N], hill = new float[N]; private readonly List<PointF> mapPts = new List<PointF>();
	private class Car { public float Z, X, V, Tgt, TgtT, Spin, Skill, Boost, ItemT, Drift; public int Lap, Item = -1; public Color C; public string Name; public bool Done; public float DoneT; }
	private class Box { public int Seg; public float X, Hide; }
	private class Ban { public float Z, X; }
	private readonly List<Car> cars = new List<Car>(); private readonly List<Box> boxes = new List<Box>(); private readonly List<Ban> bans = new List<Ban>();
	private float pos, px, v, el, skyOff, drift, charge, boostT, spinT, msgT, bumpT, steerVis, finT, hitT; private int lap, item = -1, place = 6, driftDir; private bool finished, wasDrift; private string msg = ""; private float itemRoll;
	private static readonly string[] ItemName = { "부스터", "바나나", "물풍선" };
	private float PZ { get { return CAMH * DEPTH; } }   // 카메라에서 플레이어까지 거리
	public override void Reset()
	{
		// 트랙은 위에서 보면 닫힌 고리가 되도록 각도·길이를 맞춰 설계했어요 (미니맵에 그대로 보여요)
		Array.Clear(curve, 0, N); Array.Clear(hill, 0, N); int i = 0; float hd = 0f, mx0 = 0f, my0 = 0f; mapPts.Clear(); mapPts.Add(new PointF(0, 0));
		Action<int, float> sec = (len, deg) =>
		{
			float sum = 0f; for (int k = 0; k < len; k++) sum += (float)Math.Sin(Math.PI * k / len); if (sum <= 0f) sum = 1f;
			for (int k = 0; k < len && i < N; k++, i++)
			{
				float e = (float)Math.Sin(Math.PI * k / len), dh = deg * 0.0174533f * e / sum; curve[i] = dh * 48f; hd += dh;
				mx0 += (float)Math.Sin(hd); my0 -= (float)Math.Cos(hd); mapPts.Add(new PointF(mx0, my0));
			}
		};
		// 마룡이 마을 순환로: 출발 직선 → 오르막 오른쪽 커브 → 갈고리(급 U턴 두 번) → S자 → 긴 내리막 오른쪽 커브 → 계단 커브 → 긴 직선 두 개 → 결승
		sec(141, 0); sec(80, 60); sec(6, 0); sec(56, -180); sec(35, 0); sec(56, 180); sec(52, 0); sec(36, 50); sec(36, -50); sec(10, 0); sec(110, 120); sec(109, 0);
		sec(44, -90); sec(20, 0); sec(44, 90); sec(101, 0); sec(50, 90); sec(191, 0); sec(50, 90); sec(N - i, 0);
		for (int k = 0; k < N; k++) hill[k] = (k > 141 && k < 260) ? 26f * (float)Math.Sin((k - 141) / 119.0 * Math.PI) : ((k > 590 && k < 700) ? -18f * (float)Math.Sin((k - 590) / 110.0 * Math.PI) : ((k > 900 && k < 1000) ? 14f * (float)Math.Sin((k - 900) / 50.0 * Math.PI) : 0f));
		cars.Clear(); string[] nm = { "팀장님", "민원인", "주무관", "과장님", "신규 직원" }; Color[] cc = { Color.FromArgb(230, 70, 70), Color.FromArgb(70, 110, 230), Color.FromArgb(240, 190, 40), Color.FromArgb(150, 80, 210), Color.FromArgb(250, 130, 60) };
		float[] sk = { 0.97f, 0.93f, 0.95f, 0.99f, 0.9f }; for (int k = 0; k < 5; k++) cars.Add(new Car { Skill = sk[k], Z = PZ + 600f + k * 450f, X = (k % 2 == 0 ? -0.45f : 0.45f), V = 0f, C = cc[k], Name = nm[k], Tgt = (k % 2 == 0 ? -0.45f : 0.45f) });
		boxes.Clear(); foreach (int s in new[] { 100, 210, 430, 560, 780, 960, 1150 }) for (int k = -1; k <= 1; k++) boxes.Add(new Box { Seg = s, X = k * 0.55f });
		bans.Clear(); pos = 0f; px = 0f; v = 0f; el = 0f; skyOff = 0f; drift = 0f; charge = 0f; boostT = 0f; spinT = 0f; msgT = 0f; bumpT = 0f; finT = 0f; hitT = 0f; lap = 0; item = -1; place = 6; finished = false; wasDrift = false; Score = 0; Over = false; msg = "출발!"; msgT = 1.6f;
	}
	private int SegOf(float z) { int s = (int)(Wrap(z) / SEG); return s < 0 ? 0 : (s >= N ? N - 1 : s); }
	private static float Wrap(float z) { z %= LEN; if (z < 0) z += LEN; return z; }
	private float PlayerZ { get { return pos + PZ; } }
	private float Dist(Car c) { return c.Lap * LEN + c.Z; }
	private float MyDist { get { return lap * LEN + Wrap(PlayerZ); } }
	public override void Click(float x, float y) { if (new RectangleF(316, 486, 64, 64).Contains(x, y)) UseItem(); }
	public override void Press(Keys k) { if (k == Keys.Z || k == Keys.X || k == Keys.ControlKey || k == Keys.Enter) UseItem(); }
	private void UseItem()
	{
		if (item < 0 || finished || itemRoll > 0f) return; int it = item; item = -1;
		if (it == 0) { boostT = 1.6f; Sfx.Play("whoosh"); msg = "부스터!"; msgT = 1f; }
		else if (it == 1) { bans.Add(new Ban { Z = Wrap(PlayerZ - SEG * 3f), X = px }); Sfx.Play("tick"); msg = "바나나를 놓았어요"; msgT = 1f; }
		else
		{
			Car best = null; float bd = 1e9f; float me = MyDist; foreach (Car c in cars) { float d = Dist(c) - me; if (d > 0f && d < bd) { bd = d; best = c; } }
			if (best != null && bd < SEG * 160f) { best.Spin = 1.4f; Sfx.Play("glass"); msg = best.Name + " 명중!"; } else msg = "앞에 아무도 없어요"; msgT = 1.2f;
		}
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (dt > 0.05f) dt = 0.05f; el += dt; if (msgT > 0f) msgT -= dt; if (bumpT > 0f) bumpT -= dt; if (hitT > 0f) hitT -= dt; if (itemRoll > 0f) { itemRoll -= dt; if (itemRoll <= 0f) Sfx.Play("ok2"); }
		if (finished) { finT += dt; v *= (float)Math.Pow(0.4, dt); pos = Wrap(pos + v * dt); AiStep(dt); if (finT > 3f) Over = true; return; }
		float sp = v / MAXV; bool left = inp.L, right = inp.R;
		if (inp.MouseMoved || mouseSteer) { mouseSteer = !(left || right); }
		float steer = (left ? -1f : 0f) + (right ? 1f : 0f);
		if (mouseSteer && steer == 0f) { float want = Clamp((inp.Mx - 200f) / 140f, -1f, 1f); steer = Math.Abs(want) < 0.08f ? 0f : want; }
		bool dHeld = inp.Down != null && (inp.Down.Contains(Keys.Space) || inp.Down.Contains(Keys.ShiftKey) || inp.Down.Contains(Keys.LShiftKey)) || (inp.Hold && mouseSteer && inp.Down != null && !inp.Down.Contains(Keys.Up));
		// 드리프트: 누른 채로 꺾으면 시작 → 게이지가 차요 → 놓으면 부스터
		if (dHeld && sp > 0.45f && (steer != 0f || driftDir != 0) && spinT <= 0f) { if (driftDir == 0) { driftDir = steer < 0 ? -1 : 1; Sfx.Play("swing"); } charge = Math.Min(2.2f, charge + dt * (0.6f + Math.Abs(steer) * 0.7f) * sp); drift = Math.Min(1f, drift + dt * 4f); }
		else
		{
			if (driftDir != 0 && charge > 0.55f) { boostT = Math.Max(boostT, 0.45f + charge * 0.55f); msg = charge > 1.6f ? "슈퍼 부스터!" : "부스터!"; msgT = 0.9f; Sfx.Play(charge > 1.6f ? "fanfare" : "whoosh"); }
			driftDir = 0; charge = 0f; drift = Math.Max(0f, drift - dt * 5f);
		}
		if (spinT > 0f) { spinT -= dt; steer = 0f; }
		float st = driftDir != 0 ? (driftDir * 0.75f + steer * 0.55f) : steer;
		steerVis += (st - steerVis) * Math.Min(1f, dt * 10f);
		int ps = SegOf(PlayerZ); float cv = curve[ps];
		px += st * dt * 2.1f * Math.Max(0.25f, sp) * (driftDir != 0 ? 1.15f : 1f);
		px -= dt * 2f * sp * cv * (driftDir != 0 ? 0.16f : 0.3f) * sp;
		// 속도
		float top = MAXV * (boostT > 0f ? 1.32f : 1f);
		bool offRoad = Math.Abs(px) > 1f; if (offRoad) top = Math.Min(top, MAXV * (boostT > 0f ? 0.75f : 0.42f));
		bool brake = inp.Down != null && (inp.Down.Contains(Keys.Down) || inp.Down.Contains(Keys.S));
		if (spinT > 0f) v -= MAXV * 1.1f * dt;
		else if (brake) v -= MAXV * 1.2f * dt;
		else if (v < top) v += (boostT > 0f ? MAXV * 1.4f : MAXV / 3.6f * (1f - v / (top * 1.05f) * 0.6f)) * dt;
		else v -= (offRoad ? MAXV * 1.1f : MAXV * 0.5f) * dt;
		if (driftDir != 0) v -= MAXV * 0.05f * dt;
		v = Clamp(v, 0f, MAXV * 1.4f); if (boostT > 0f) boostT -= dt;
		px = Clamp(px, -2.2f, 2.2f);
		float before = PlayerZ; pos = Wrap(pos + v * dt); skyOff += cv * sp * dt * 0.02f;
		if (Wrap(PlayerZ) < Wrap(before) - LEN * 0.5f) { lap++; if (lap >= Laps) { Finish(); return; } msg = lap == Laps - 1 ? "마지막 바퀴!" : (lap + 1) + "바퀴째!"; msgT = 1.6f; Sfx.Play("ok"); }
		// 아이템 상자 · 바나나
		float pz = Wrap(PlayerZ);
		foreach (Box b in boxes)
		{
			if (b.Hide > 0f) { b.Hide -= dt; continue; }
			float bz = b.Seg * SEG; if (Math.Abs(bz - pz) < SEG * 0.8f && Math.Abs(b.X - px) < 0.28f) { b.Hide = 3f; if (item < 0) { item = Rnd.Next(3); if (place >= 4 && Rnd.NextDouble() < 0.5) item = 0; itemRoll = 0.7f; Sfx.Play("ui_open"); } }
		}
		for (int i = bans.Count - 1; i >= 0; i--) { Ban b = bans[i]; if (Math.Abs(Wrap(b.Z - pz + LEN * 0.5f) - LEN * 0.5f) < SEG * 0.7f && Math.Abs(b.X - px) < 0.22f && spinT <= 0f && el > 1f) { spinT = 1.0f; Shake = 0.4f; Sfx.Play("bad2"); msg = "바나나에 미끄러졌어요!"; msgT = 1.2f; bans.RemoveAt(i); } }
		AiStep(dt);
		// 다른 차와 부딪치기
		foreach (Car c in cars)
		{
			float dz = Wrap(c.Z - pz + LEN * 0.5f) - LEN * 0.5f;
			if (dz > -SEG * 0.6f && dz < SEG * 1.4f && Math.Abs(c.X - px) < 0.32f && bumpT <= 0f) { bumpT = 0.4f; v = Math.Min(v, c.V * 0.92f); px += px < c.X ? -0.18f : 0.18f; Shake = 0.25f; Sfx.Play("thud_soft"); }
		}
		// 순위
		float my = MyDist; place = 1; foreach (Car c in cars) if (Dist(c) > my) place++;
	}
	private bool mouseSteer;
	private void Finish()
	{
		finished = true; lap = Laps; finT = 0f; int pl = place; int[] pts = { 1000, 800, 650, 500, 400, 300 }; Score = pts[Math.Min(5, pl - 1)] + Math.Max(0, (int)((110f - el) * 8f));
		msg = pl == 1 ? "1등! 우승이에요!" : pl + "등으로 완주!"; msgT = 3f; Sfx.Play(pl <= 3 ? "fanfare" : "ok");
	}
	private void AiStep(float dt)
	{
		float my = MyDist;
		foreach (Car c in cars)
		{
			int sg = SegOf(c.Z); float cv = curve[sg], cvA = Math.Max(Math.Abs(cv), Math.Abs(curve[(sg + 12) % N]));
			if (c.Spin > 0f) { c.Spin -= dt; c.V = Math.Max(MAXV * 0.25f, c.V - MAXV * 1.2f * dt); }
			else
			{
				// CPU: 차마다 실력이 달라요 · 급커브 앞에서는 드리프트로 속도를 조금만 줄여요 · 너무 벌어지면 고무줄처럼 따라와요
				float gap = Dist(c) - my; float want = MAXV * c.Skill;
				c.Drift = cvA > 2.2f ? Math.Min(1f, c.Drift + dt * 3f) : Math.Max(0f, c.Drift - dt * 3f);
				want *= 1f - Math.Min(0.3f, cvA * 0.06f) * (1f - 0.45f * c.Drift);
				if (gap > SEG * 120f) want *= 0.88f; else if (gap < -SEG * 80f) want *= 1.07f;
				if (c.Boost > 0f) { c.Boost -= dt; want = MAXV * 1.25f; }
				c.V += (want - c.V) * Math.Min(1f, dt * (c.Boost > 0f ? 2.5f : 0.9f));
				// 아이템 쓰기
				if (c.Item >= 0) { c.ItemT -= dt; if (c.ItemT <= 0f) AiItem(c); }
			}
			c.TgtT -= dt; if (c.TgtT <= 0f) { c.TgtT = 1.5f + (float)Rnd.NextDouble() * 2f; c.Tgt = (float)(Rnd.NextDouble() * 1.3 - 0.65); }
			c.X += (c.Tgt - cv * 0.06f - c.X) * Math.Min(1f, dt * 1.2f);
			float nz = c.Z + c.V * dt; if (nz >= LEN) { nz -= LEN; c.Lap++; } c.Z = nz;
			foreach (Box bx in boxes) if (bx.Hide <= 0f && Math.Abs(bx.Seg * SEG - c.Z) < SEG * 0.8f && Math.Abs(bx.X - c.X) < 0.3f) { bx.Hide = 3f; if (c.Item < 0) { c.Item = Rnd.Next(3); c.ItemT = 1.5f + (float)Rnd.NextDouble() * 4f; } }
			for (int i = bans.Count - 1; i >= 0; i--) { Ban b = bans[i]; if (Math.Abs(b.Z - c.Z) < SEG * 0.7f && Math.Abs(b.X - c.X) < 0.25f) { c.Spin = 1.1f; bans.RemoveAt(i); } }
		}
	}
	private void AiItem(Car c)
	{
		int it = c.Item; c.Item = -1; float me = MyDist, cd = Dist(c);
		if (it == 0) c.Boost = 1.3f;
		else if (it == 1) bans.Add(new Ban { Z = Wrap(c.Z - SEG * 3f), X = c.X });
		else
		{
			// 바로 앞 차를 노려요 (그게 마룡이면 마룡이가 맞아요)
			Car best = null; float bd = 1e9f; foreach (Car o in cars) { if (o == c) continue; float d = Dist(o) - cd; if (d > 0f && d < bd) { bd = d; best = o; } }
			float dm = me - cd;
			if (dm > 0f && dm < bd && dm < SEG * 160f && !finished) { if (spinT <= 0f) { spinT = 0.9f; Shake = 0.35f; Sfx.Play("bad2"); msg = c.Name + "의 물풍선에 맞았어요!"; msgT = 1.4f; } }
			else if (best != null && bd < SEG * 160f) best.Spin = 1.1f;
		}
	}
	// ── 그리기 ──
	private struct Pj { public float X, Y, W, Sc; }
	private static Pj Proj(float wx, float wy, float wz, float camX, float camY, float camZ)
	{
		float cz = wz - camZ; if (cz < 1f) cz = 1f; float sc = DEPTH / cz; Pj p;
		p.Sc = sc; p.X = W / 2f + sc * (wx - camX) * W / 2f; p.Y = 300f - sc * (wy - camY) * W / 2f; p.W = sc * ROADW * W / 2f; return p;
	}
	private static void Quad(Graphics g, SolidBrush b, float x1, float y1, float w1, float x2, float y2, float w2)
	{
		y2 -= 0.8f; y1 += 0.4f; g.FillPolygon(b, new PointF[] { new PointF(x1 - w1, y1), new PointF(x2 - w2, y2), new PointF(x2 + w2, y2), new PointF(x1 + w1, y1) });
	}
	private readonly SolidBrush grass1 = new SolidBrush(Color.FromArgb(96, 182, 92)), grass2 = new SolidBrush(Color.FromArgb(86, 168, 84)), road1 = new SolidBrush(Color.FromArgb(110, 112, 122)), road2 = new SolidBrush(Color.FromArgb(104, 106, 116)), rum1 = new SolidBrush(Color.FromArgb(230, 70, 70)), rum2 = new SolidBrush(Color.FromArgb(245, 245, 245)), lane = new SolidBrush(Color.FromArgb(235, 235, 220)), chk = new SolidBrush(Color.FromArgb(30, 30, 40));
	private class Sp { public float X, Y, Sc, Clip; public int K; public object O; }
	private readonly List<Sp> sps = new List<Sp>();
	public override void Draw(Graphics g, double t)
	{
		// 하늘 · 먼 산
		Bg(g, Color.FromArgb(120, 190, 250), Color.FromArgb(205, 232, 255));
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 250, 210))) g.FillEllipse(b, 300, 120, 44, 44);
		Hills(g, 0.25f, 230f, 60f, Color.FromArgb(150, 190, 210), 7);
		Hills(g, 0.5f, 268f, 46f, Color.FromArgb(110, 170, 130), 5);
		SmoothingMode sm0 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;
		int bs = SegOf(pos); float bp = (Wrap(pos) % SEG) / SEG; float camY = CAMH + Lerp(hill[bs], hill[(bs + 1) % N], bp) * 0 + HillY(PlayerZ);
		float x = 0f, dx = -curve[bs] * bp, maxY = H; sps.Clear();
		Pj[] p1s = new Pj[DRAW]; float[] clip = new float[DRAW];
		for (int n = 0; n < DRAW; n++)
		{
			int i = (bs + n) % N; bool looped = i < bs; float camZ = Wrap(pos) - (looped ? LEN : 0f);
			float y1 = HillY(i * SEG), y2 = HillY((i + 1) * SEG);
			Pj a = Proj(0, y1, i * SEG, px * ROADW - x, camY, camZ), b = Proj(0, y2, (i + 1) * SEG, px * ROADW - x - dx, camY, camZ);
			x += dx; dx += curve[i]; p1s[n] = a; clip[n] = maxY;
			if (i * SEG - camZ <= DEPTH || b.Y >= maxY || b.Y >= a.Y - 0.01f) continue;
			bool alt = (i / 3) % 2 == 0;
			g.FillRectangle(alt ? grass1 : grass2, 0, b.Y, W, a.Y - b.Y + 1);
			Quad(g, alt ? rum1 : rum2, a.X, a.Y, a.W * 1.12f, b.X, b.Y, b.W * 1.12f);
			Quad(g, (i < 3) ? chk : (alt ? road1 : road2), a.X, a.Y, a.W, b.X, b.Y, b.W);
			if (i < 3) { for (int k = 0; k < 8; k++) if ((k + i) % 2 == 0) { float u0 = -1f + k * 0.25f; Quad(g, rum2, a.X + a.W * (u0 + 0.125f), a.Y, a.W * 0.125f, b.X + b.W * (u0 + 0.125f), b.Y, b.W * 0.125f); } }
			else if (alt) { Quad(g, lane, a.X - a.W * 0.33f, a.Y, a.W * 0.02f, b.X - b.W * 0.33f, b.Y, b.W * 0.02f); Quad(g, lane, a.X + a.W * 0.33f, a.Y, a.W * 0.02f, b.X + b.W * 0.33f, b.Y, b.W * 0.02f); }
			maxY = b.Y;
		}
		g.SmoothingMode = sm0;
		// 길가 장식 · 아이템 상자 · 바나나 · 다른 차 (먼 것부터)
		for (int n = DRAW - 1; n > 2; n--)
		{
			int i = (bs + n) % N; Pj a = p1s[n]; if (a.Sc <= 0f || a.Y <= 0f) continue; float cl = clip[n];
			if (i % 5 == 0)
			{
				int r = (i * 7) % 11; float side = ((i / 5) % 2 == 0) ? -1f : 1f; bool bend = Math.Abs(curve[i]) > 1.2f;
				int kind = bend ? 3 : (i % 60 == 0 ? 1 : (r < 4 ? 2 : (r < 8 ? 0 : 4)));
				if (bend) side = curve[i] > 0f ? -1f : 1f;   // 급커브 바깥쪽에 울타리
				Deco(g, a, side * (kind == 2 ? 2.2f : (kind == 3 ? 1.3f : 1.5f)), kind, cl, i);
			}
			foreach (Box bx in boxes) if (bx.Seg == i && bx.Hide <= 0f) ItemBox(g, a, bx.X, cl, t);
			foreach (Ban bn in bans) if (SegOf(bn.Z) == i) Banana(g, a, bn.X, cl);
			foreach (Car c in cars) if (SegOf(c.Z) == i) { float sx = a.X + a.Sc * c.X * ROADW * W / 2f, sz = a.Sc * 900f * W / 2f; if (a.Y - sz * 0.8f < cl) DrawKart(g, sx, a.Y, Math.Min(150f, sz), c.C, false, c.Spin > 0f ? (float)(t * 900 % 360) : 0f, 0f, "ms_kart_cpu" + cars.IndexOf(c)); }
		}
		// 내 카트
		float bob = (float)Math.Sin(t * 30) * (v > 100f ? 1.2f : 0f) + (Math.Abs(px) > 1f ? (float)Math.Sin(t * 50) * 2f : 0f);
		if (driftDir != 0) Sparks(g, 200f + driftDir * -38f, 548f, charge, t);
		if (boostT > 0f) Flame(g, 200f, 556f, t);
		{ string ks = driftDir < 0 ? "ms_kart_me3" : (driftDir > 0 ? "ms_kart_me4" : (Math.Abs(steerVis) > 0.35f ? "ms_kart_me1" : "ms_kart_me0")); bool kf = driftDir == 0 && steerVis > 0.35f;   // 오른쪽 핸들은 왼쪽 그림을 뒤집어 써요
			DrawKart(g, 200f, 556f + bob, 120f, Color.FromArgb(30, 160, 140), true, spinT > 0f ? (float)(t * 1100 % 360) : steerVis * 3f, steerVis, ks, kf); }
		// HUD
		Panel(g, new RectangleF(10, 40, 120, 56)); Txt(g, place + "등", 22f, 2, place == 1 ? Color.FromArgb(255, 220, 90) : Color.White, 10, 40, 120, 56, StringAlignment.Center);
		Panel(g, new RectangleF(140, 40, 250, 56)); Txt(g, Math.Min(Laps, lap + 1) + " / " + Laps + " 바퀴   " + ((int)(v / MAXV * 180)) + "km/h", 11f, 2, Color.White, 146, 42, 240, 24, StringAlignment.Center);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(152, 70, 226, 12), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillPath(b, gp);
		float cf = Math.Min(1f, charge / 2.2f); if (cf > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(152, 70, 226 * cf, 12), 6)) using (SolidBrush b = new SolidBrush(charge > 1.6f ? Color.FromArgb(255, 150, 40) : (charge > 0.55f ? Color.FromArgb(80, 190, 255) : Color.FromArgb(200, 220, 230)))) g.FillPath(b, gp);
		Txt(g, "드리프트 게이지", 7.5f, 1, Color.FromArgb(220, 255, 255, 255), 152, 82, 226, 12, StringAlignment.Center);
		RectangleF ib = new RectangleF(316, 486, 64, 64); Panel(g, ib);
		if (item >= 0) { int shown = itemRoll > 0f ? ((int)(t * 14)) % 3 : item; ItemIcon(g, ib.X + 32, ib.Y + 28, shown); Txt(g, itemRoll > 0f ? "" : ItemName[item], 8f, 2, Color.White, ib.X, ib.Y + 44, ib.Width, 18, StringAlignment.Center); }
		else Txt(g, "아이템", 8.5f, 1, Color.FromArgb(150, 255, 255, 255), ib.X, ib.Y, ib.Width, ib.Height, StringAlignment.Center);
		// 순위표 (작게)
		List<KeyValuePair<string, float>> rk = new List<KeyValuePair<string, float>>(); rk.Add(new KeyValuePair<string, float>("마룡이", MyDist)); foreach (Car c in cars) rk.Add(new KeyValuePair<string, float>(c.Name, Dist(c)));
		rk.Sort((a, b) => b.Value.CompareTo(a.Value)); Panel(g, new RectangleF(10, 104, 92, 6 * 15 + 8));
		for (int i = 0; i < rk.Count; i++) Txt(g, (i + 1) + " " + rk[i].Key, 8f, rk[i].Key == "마룡이" ? 2 : 1, rk[i].Key == "마룡이" ? Color.FromArgb(255, 230, 120) : Color.FromArgb(225, 255, 255, 255), 16, 108 + i * 15, 86, 15, StringAlignment.Near);
		{ RectangleF mr = new RectangleF(10, 470, 112, 104); Panel(g, mr); MiniMap(g, mr); }
		if (msgT > 0f) Hint(g, msg, 170, 14f, true, Math.Min(1f, msgT * 2f));
		if (el < 5f) Hint(g, "←→ 핸들 · 커브에서 스페이스 누른 채 → 놓으면 부스터", 432, 10.5f, false, Math.Min(1f, (5f - el) * 2f));
	}
	private static float Lerp(float a, float b, float k) { return a + (b - a) * k; }
	private float HillY(float z) { float s = Wrap(z) / SEG; int i = (int)s; float k = s - i; i %= N; return Lerp(hill[i], hill[(i + 1) % N], k) * 20f; }
	private void Hills(Graphics g, float par, float baseY, float amp, Color c, int n)
	{
		float off = (skyOff * par * 900f) % 400f; List<PointF> pts = new List<PointF>(); pts.Add(new PointF(-10, 300));
		for (int k = -2; k <= 44; k++) { float xx = k * 10f - off; float yy = baseY - amp * (0.5f + 0.5f * (float)Math.Sin((k * 10f + Math.Floor((skyOff * par * 900f) / 400f) * 400f) / 400f * Math.PI * 2 * n / 4f + par * 7f)) * (0.7f + 0.3f * (float)Math.Sin(k * 0.9f)); pts.Add(new PointF(xx, yy)); }
		pts.Add(new PointF(410, 300)); using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, pts.ToArray());
	}
	private static readonly Color[] Wall = { Color.FromArgb(250, 238, 214), Color.FromArgb(246, 214, 214), Color.FromArgb(214, 230, 246), Color.FromArgb(232, 246, 214) }, Roof = { Color.FromArgb(206, 78, 62), Color.FromArgb(92, 120, 190), Color.FromArgb(150, 96, 70), Color.FromArgb(70, 150, 130) };
	// 마을 장식: 0 나무 · 1 표지판 · 2 집 · 3 울타리 · 4 가로등과 꽃
	private void Deco(Graphics g, Pj a, float side, int kind, float clip, int seg)
	{
		float u = a.Sc * W / 2f, sx = a.X + u * side * ROADW; float h = u * (kind == 2 ? 2300f : (kind == 1 ? 2600f : (kind == 3 ? 520f : (kind == 4 ? 1700f : 2000f)))); if (h < 2f || h > 340f || a.Y - h > clip) return;
		GraphicsState st = g.Save(); g.SetClip(new RectangleF(0, 0, W, clip));
		{
			// 그림 소품: 0 나무(둥근/뾰족) · 2 집(빨강/파랑 지붕) · 3 울타리 · 4 가로등
			string pn = kind == 0 ? (seg % 10 == 0 ? "ms_kp_3" : "ms_kp_2") : (kind == 2 ? ((seg / 5) % 2 == 0 ? "ms_kp_0" : "ms_kp_1") : (kind == 3 ? "ms_kp_4" : (kind == 4 ? "ms_kp_5" : null))); Bitmap pb = pn == null ? null : Spr.Get(pn);
			if (pb != null) { float ph = kind == 3 ? h * 1.1f : h, pw = ph * pb.Width / pb.Height; Spr.Draw(g, pn, sx, a.Y - ph / 2f, pw, ph, 0f); g.Restore(st); return; }
		}
		if (kind == 0) { using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 80, 50))) g.FillRectangle(b, sx - h * 0.06f, a.Y - h * 0.45f, h * 0.12f, h * 0.45f); using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 140, 70))) g.FillEllipse(b, sx - h * 0.32f, a.Y - h, h * 0.64f, h * 0.66f); using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 170, 90))) g.FillEllipse(b, sx - h * 0.22f, a.Y - h * 0.95f, h * 0.36f, h * 0.36f); }
		else if (kind == 1) { using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 90, 100))) g.FillRectangle(b, sx - h * 0.025f, a.Y - h, h * 0.05f, h); using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 138, 124))) g.FillRectangle(b, sx - h * 0.34f, a.Y - h, h * 0.68f, h * 0.22f); if (h > 40f) Txt(g, "마룡이 카트", Math.Max(5f, h * 0.07f), 2, Color.White, sx - h * 0.34f, a.Y - h, h * 0.68f, h * 0.22f, StringAlignment.Center); }
		else if (kind == 2)
		{
			int v = (seg / 5) % 4; float w = h * 0.9f, wh = h * 0.55f;
			using (SolidBrush b = new SolidBrush(Wall[v])) g.FillRectangle(b, sx - w / 2f, a.Y - wh, w, wh);
			using (SolidBrush b = new SolidBrush(Roof[(v + seg / 10) % 4])) g.FillPolygon(b, new PointF[] { new PointF(sx - w * 0.6f, a.Y - wh), new PointF(sx, a.Y - h), new PointF(sx + w * 0.6f, a.Y - wh) });
			using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 84, 60))) g.FillRectangle(b, sx - w * 0.1f, a.Y - wh * 0.5f, w * 0.2f, wh * 0.5f);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 210, 240))) { g.FillRectangle(b, sx - w * 0.38f, a.Y - wh * 0.78f, w * 0.18f, wh * 0.24f); g.FillRectangle(b, sx + w * 0.2f, a.Y - wh * 0.78f, w * 0.18f, wh * 0.24f); }
		}
		else if (kind == 3)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 250, 245))) { g.FillRectangle(b, sx - h * 1.6f, a.Y - h * 0.62f, h * 3.2f, h * 0.16f); g.FillRectangle(b, sx - h * 1.6f, a.Y - h * 0.3f, h * 3.2f, h * 0.14f); for (int k = -2; k <= 2; k++) g.FillRectangle(b, sx + k * h * 0.75f - h * 0.08f, a.Y - h, h * 0.16f, h); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 70, 70))) for (int k = -2; k <= 2; k += 2) g.FillRectangle(b, sx + k * h * 0.75f - h * 0.08f, a.Y - h, h * 0.16f, h * 0.2f);
		}
		else
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 74, 84))) g.FillRectangle(b, sx - h * 0.02f, a.Y - h, h * 0.04f, h);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 240, 180))) g.FillEllipse(b, sx - h * 0.07f, a.Y - h * 1.04f, h * 0.14f, h * 0.12f);
			Color[] fl = { Color.FromArgb(240, 90, 120), Color.FromArgb(250, 200, 70), Color.FromArgb(170, 110, 230) };
			for (int k = 0; k < 6; k++) using (SolidBrush b = new SolidBrush(fl[k % 3])) g.FillEllipse(b, sx - h * 0.3f + k * h * 0.11f, a.Y - h * 0.08f - (k % 2) * h * 0.04f, h * 0.08f, h * 0.08f);
		}
		g.Restore(st);
	}
	// 미니맵: 위에서 본 코스 + 차 위치
	private void MiniMap(Graphics g, RectangleF r)
	{
		if (mapPts.Count < 2) return; float mnx = 1e9f, mny = 1e9f, mxx = -1e9f, mxy = -1e9f; foreach (PointF q in mapPts) { mnx = Math.Min(mnx, q.X); mny = Math.Min(mny, q.Y); mxx = Math.Max(mxx, q.X); mxy = Math.Max(mxy, q.Y); }
		float sc = Math.Min((r.Width - 16f) / (mxx - mnx), (r.Height - 16f) / (mxy - mny)), ox = r.X + (r.Width - (mxx - mnx) * sc) / 2f, oy = r.Y + (r.Height - (mxy - mny) * sc) / 2f;
		Func<int, PointF> at = k => { PointF q = mapPts[Math.Max(0, Math.Min(mapPts.Count - 1, k))]; return new PointF(ox + (q.X - mnx) * sc, oy + (q.Y - mny) * sc); };
		PointF[] line = new PointF[(mapPts.Count + 3) / 4 + 1]; for (int k = 0; k < line.Length; k++) line[k] = at(Math.Min(mapPts.Count - 1, k * 4)); line[line.Length - 1] = at(0);
		using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 4f) { LineJoin = LineJoin.Round }) g.DrawLines(p, line);
		using (Pen p = new Pen(Color.FromArgb(255, 110, 112, 122), 2.2f) { LineJoin = LineJoin.Round }) g.DrawLines(p, line);
		foreach (Car c in cars) { PointF q = at(SegOf(c.Z)); using (SolidBrush b = new SolidBrush(c.C)) g.FillEllipse(b, q.X - 3f, q.Y - 3f, 6f, 6f); }
		PointF me = at(SegOf(PlayerZ)); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 230, 90))) g.FillEllipse(b, me.X - 4.5f, me.Y - 4.5f, 9f, 9f); using (Pen p = new Pen(Color.FromArgb(30, 160, 140), 2f)) g.DrawEllipse(p, me.X - 4.5f, me.Y - 4.5f, 9f, 9f);
	}
	private void ItemBox(Graphics g, Pj a, float bx, float clip, double t)
	{
		float sx = a.X + a.Sc * bx * ROADW * W / 2f, s = a.Sc * 420f * W / 2f; if (s < 2f) return; float y = a.Y - s * 1.3f + (float)Math.Sin(t * 4 + bx) * s * 0.1f; if (y > clip) return;
		GraphicsState st = g.Save(); g.SetClip(new RectangleF(0, 0, W, clip));
		if (Spr.Get("ms_kp_6") != null) { Spr.Draw(g, "ms_kp_6", sx, y + s / 2f, s * 1.1f, s * 1.05f, (float)Math.Sin(t * 3 + bx) * 6f); g.Restore(st); return; }
		using (GraphicsPath gp = Gfx.Round(new RectangleF(sx - s / 2f, y, s, s), s * 0.2f)) { using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(sx - s / 2f, y - 1, s, s + 2), Color.FromArgb(220, 255, 210, 90), Color.FromArgb(220, 240, 120, 200), 45f)) g.FillPath(lb, gp); using (Pen p = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(1f, s * 0.06f))) g.DrawPath(p, gp); }
		if (s > 10f) Txt(g, "?", s * 0.42f, 2, Color.White, sx - s / 2f, y, s, s, StringAlignment.Center);
		g.Restore(st);
	}
	private static void Banana(Graphics g, Pj a, float bx, float clip)
	{
		float sx = a.X + a.Sc * bx * ROADW * W / 2f, s = a.Sc * 300f * W / 2f; if (s < 2f || a.Y - s > clip) return;
		if (Spr.Get("ms_kp_7") != null) { Spr.Draw(g, "ms_kp_7", sx, a.Y - s * 0.4f, s * 1.3f, s, 0f); return; }
		using (Pen p = new Pen(Color.FromArgb(250, 220, 60), Math.Max(1.5f, s * 0.28f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, sx - s * 0.6f, a.Y - s * 1.1f, s * 1.2f, s, 20, 140);
	}
	private static void ItemIcon(Graphics g, float cx, float cy, int k)
	{
		if (k == 0) { using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 150, 40))) g.FillPolygon(b, new PointF[] { new PointF(cx - 6, cy - 16), new PointF(cx + 10, cy - 2), new PointF(cx + 1, cy - 2), new PointF(cx + 6, cy + 14), new PointF(cx - 10, cy - 1), new PointF(cx - 1, cy - 1) }); }
		else if (k == 1 && Spr.Get("ms_kp_7") != null) Spr.Draw(g, "ms_kp_7", cx, cy, 34f, 26f, 0f);
		else if (k == 1) { using (Pen p = new Pen(Color.FromArgb(250, 220, 60), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, cx - 14, cy - 16, 28, 26, 20, 140); }
		else { using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 180, 255))) g.FillEllipse(b, cx - 12, cy - 12, 24, 24); using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, cx - 7, cy - 8, 8, 6); }
	}
	private static void Panel(Graphics g, RectangleF r)
	{
		using (GraphicsPath gp = Gfx.Round(r, 12)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 12, 24, 46))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(80, 255, 255, 255), 1.2f)) g.DrawPath(p, gp); }
	}
	private static void Sparks(Graphics g, float x, float y, float charge, double t)
	{
		Color c = charge > 1.6f ? Color.FromArgb(255, 150, 40) : (charge > 0.55f ? Color.FromArgb(90, 190, 255) : Color.FromArgb(240, 240, 240));
		for (int i = 0; i < 6; i++) { float a = (float)(t * 37 + i * 1.7), r = 6f + (i % 3) * 5f; using (SolidBrush b = new SolidBrush(Color.FromArgb(200, c))) g.FillEllipse(b, x + (float)Math.Cos(a) * r - 3, y - Math.Abs((float)Math.Sin(a)) * r - 3, 6, 6); }
	}
	private static void Flame(Graphics g, float x, float y, double t)
	{
		for (int k = -1; k <= 1; k += 2) { float l = 22f + (float)Math.Sin(t * 40 + k) * 6f; using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 140, 40))) g.FillEllipse(b, x + k * 22 - 7, y - 4, 14, l); using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 255, 230, 120))) g.FillEllipse(b, x + k * 22 - 4, y - 2, 8, l * 0.6f); }
	}
	// 카트 그림: me=true 면 뒤에서 본 마룡이 (머리·귀·뿔·날개), 아니면 색 헬멧 운전자
	internal static void DrawKart(Graphics g, float cx, float by, float w, Color c, bool me, float rot, float steer, string spr = null, bool flip = false)
	{
		if (w < 3f) return;
		if (spr != null && Spr.Get(spr) != null)
		{
			// 그림(스프라이트) 카트: 바닥 그림자 + 원본 비율 그대로 (모든 카트가 같은 배율)
			float k = w / 120f; using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(sb, cx - w * 0.5f, by - w * 0.08f, w, w * 0.18f);
			GraphicsState s0 = g.Save(); g.TranslateTransform(cx, by); g.RotateTransform(rot); Spr.Frame(g, spr, 0f, 0f, k, 0f, flip); g.Restore(s0); return;
		}
		float h = w * 0.62f;
		GraphicsState st = g.Save(); g.TranslateTransform(cx, by); g.RotateTransform(rot);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, -w * 0.55f, -h * 0.12f, w * 1.1f, h * 0.24f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 46))) { g.FillEllipse(b, -w * 0.52f, -h * 0.42f, w * 0.24f, h * 0.42f); g.FillEllipse(b, w * 0.28f, -h * 0.42f, w * 0.24f, h * 0.42f); }
		using (GraphicsPath gp = Gfx.Round(new RectangleF(-w * 0.42f, -h * 0.62f, w * 0.84f, h * 0.44f), h * 0.16f)) using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(-w, -h * 0.65f, w * 2, h * 0.5f), Theme.Mix(c, Color.White, 0.25), Theme.Mix(c, Color.Black, 0.25), 90f)) g.FillPath(lb, gp);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 60))) { g.FillRectangle(b, -w * 0.36f, -h * 0.34f, w * 0.1f, h * 0.06f); g.FillRectangle(b, w * 0.26f, -h * 0.34f, w * 0.1f, h * 0.06f); }
		if (me)
		{
			float hr = w * 0.26f, hx = steer * w * 0.03f, hy = -h * 0.62f - hr * 0.9f; Color body = Color.FromArgb(22, 150, 136), dk = Color.FromArgb(14, 110, 100);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(215, 235, 250))) { g.FillEllipse(b, hx - hr * 1.7f, hy + hr * 0.3f, hr * 1.1f, hr * 0.8f); g.FillEllipse(b, hx + hr * 0.6f, hy + hr * 0.3f, hr * 1.1f, hr * 0.8f); }   // 날개
			using (SolidBrush b = new SolidBrush(dk)) { g.FillPolygon(b, new PointF[] { new PointF(hx - hr * 0.95f, hy - hr * 0.2f), new PointF(hx - hr * 0.75f, hy - hr * 1.15f), new PointF(hx - hr * 0.3f, hy - hr * 0.6f) }); g.FillPolygon(b, new PointF[] { new PointF(hx + hr * 0.95f, hy - hr * 0.2f), new PointF(hx + hr * 0.75f, hy - hr * 1.15f), new PointF(hx + hr * 0.3f, hy - hr * 0.6f) }); }   // 귀
			using (Pen p = new Pen(body, hr * 0.28f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawArc(p, hx - hr * 0.42f, hy - hr * 1.35f, hr * 0.5f, hr * 0.7f, 180, 160); g.DrawArc(p, hx + hr * 0.05f, hy - hr * 1.35f, hr * 0.5f, hr * 0.7f, 180, 160); }   // 짧은 뿔
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(hx - hr, hy - hr, hr * 2, hr * 1.9f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterPoint = new PointF(hx - hr * 0.3f, hy - hr * 0.5f); pb.CenterColor = Theme.Mix(body, Color.White, 0.3); pb.SurroundColors = new Color[] { dk }; g.FillPath(pb, gp); } }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 110, 200))) g.FillEllipse(b, hx + hr * 0.2f, hy + hr * 0.5f, hr * 0.9f, hr * 0.6f);   // 꼬리 끝
		}
		else
		{
			float hr = w * 0.2f, hy = -h * 0.62f - hr * 0.85f;
			using (SolidBrush b = new SolidBrush(Theme.Mix(c, Color.White, 0.15))) g.FillEllipse(b, -hr, hy - hr, hr * 2, hr * 2);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(b, -hr * 0.6f, hy - hr * 0.7f, hr * 0.7f, hr * 0.5f);
		}
		g.Restore(st);
	}
}
