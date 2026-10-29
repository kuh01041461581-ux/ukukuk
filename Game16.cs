using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 시청 철거 대작전 : 1) 낡은 구청사를 미사일·운석·연속포·폭격기로 철거 2) 그 자리 뒤편에 신청사를 층층이 올려요 (블록 약식 모형, 실제 설계도 아님) ──
internal sealed class CityHallGame : MiniGame
{
	private const int N = 28, HH = 14; private const float S = 12f, OX = 200f, OY = 352f, EL = 0.6f;
	private sealed class Blk { public int X, Y, Z; public Color C; public float Hp, Max; public byte K; public bool Win, Roof; public float Dp; }
	private sealed class Fall { public Blk B; public float Y, Vy; }
	private sealed class Deb { public float X, Y, Z, Vx, Vy, Vz, Life, Sz; public Color C; }
	private sealed class Puff { public float X, Y, Z, Age, Life, R; }
	private sealed class Ball { public float X, Y, Z, R, Age; }
	private sealed class Shot { public int Kind; public float T, Dur; public PointF From; public float Tx, Ty, Tz, R, Pow; public List<PointF> Tr = new List<PointF>(); public PointF Cur; }
	private sealed class Plane { public float X, Y, Vx, Dir; public float Tx, Tz, Ty; public float[] Ks; public bool[] Done; }
	private Blk[,,] grid = new Blk[N, HH, N]; private readonly List<Blk> all = new List<Blk>(), vis = new List<Blk>();
	private readonly List<Fall> falls = new List<Fall>(); private readonly List<Deb> debs = new List<Deb>(); private readonly List<Puff> puffs = new List<Puff>(); private readonly List<Ball> balls = new List<Ball>(); private readonly List<Shot> shots = new List<Shot>(); private readonly List<Plane> planes = new List<Plane>();
	private float yaw, yawT, cs = 1f, sn = 0f, builtYaw = 99f, msgT, wonT, ringT; private string msg = ""; private bool dirty = true, sceneDirty = true, won; private int hallTotal, hallAlive, weapon, kills, combo, phase, buildStep, buildClicks; private float phaseT, popT; private float comboT;
	private readonly float[] cd = new float[4]; private static readonly float[] CdMax = { 0.55f, 6f, 2.2f, 8f }; private static readonly string[] WName = { "미사일", "운석", "연속포", "폭격기" }; private static readonly float[] WR = { 2.6f, 4.4f, 1.6f, 2.2f };
	private bool aimOk; private float ax, ay, az; private Bitmap scene; private float sceneSc; private readonly float sinE = (float)Math.Sin(EL), cosE = (float)Math.Cos(EL);
	public override bool Crosshair { get { return true; } }
	public override string Costume { get { return "군복_공군"; } }
	public override string Name { get { return "시청 철거 대작전"; } }
	public override string Key { get { return "hall"; } }
	public override string Desc { get { return "구청사를 부수고 신청사를 지어요"; } }
	public override string Help { get { return "① 철거: 클릭하면 고른 무기로 발사해요 · 1~4(아래 버튼)로 무기 바꾸기 · A/D 또는 ◀▶ 버튼으로 화면 돌리기\n건물 아랫부분을 부수면 위쪽이 통째로 무너져요 · 구청사를 88% 이상 부수면 ② 건설 시작!\n② 건설: 마구 클릭하면 신청사가 한 층씩 올라가요 (지상 10층) · 60초 안에 준공하면 보너스\n※ 블록으로 만든 약식 모형이에요 (구청사 4층 · 신청사 지상 10층, 외관은 약식이에요)"; } }
	public override string RightText { get { return phase == 0 ? Pct() + "%" : buildStep + "/10층"; } }
	private int Pct() { return hallTotal <= 0 ? 0 : (int)Math.Round(100.0 * (hallTotal - hallAlive) / hallTotal); }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 222, 230))) { g.FillRectangle(b, cx - 13, cy - 6, 26, 22); g.FillRectangle(b, cx - 6, cy - 16, 12, 10); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 150, 190))) for (int i = 0; i < 3; i++) g.FillRectangle(b, cx - 10 + i * 8, cy, 5, 5);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 150, 40))) g.FillEllipse(b, cx + 2, cy - 18, 16, 16); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 230, 90))) g.FillEllipse(b, cx + 6, cy - 14, 8, 8);
	}
	// ── 모형 만들기 ──────────────────────────────────────────────
	private void Put(int x, int y, int z, Color c, byte k, float hp, bool win, bool roof)
	{
		if (x < 0 || z < 0 || y < 0 || x >= N || z >= N || y >= HH || grid[x, y, z] != null) return;
		int h = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791); int v = ((h & 7) - 3); Color cc = Color.FromArgb(Math.Max(0, Math.Min(255, c.R + v)), Math.Max(0, Math.Min(255, c.G + v)), Math.Max(0, Math.Min(255, c.B + v)));
		Blk b = new Blk { X = x, Y = y, Z = z, C = cc, Hp = hp, Max = hp, K = k, Win = win, Roof = roof }; grid[x, y, z] = b; all.Add(b); if (k == 0) hallTotal++;
	}
	private void Shell(int x0, int z0, int w, int d, int y0, int h, Color wall, Color roof, byte k, float hp, int winMode)
	{
		for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) for (int z = z0; z < z0 + d; z++)
		{
			bool per = x == x0 || z == z0 || x == x0 + w - 1 || z == z0 + d - 1, top = y == y0 + h - 1; if (!per && !top) continue;
			bool win = !top && per && y > y0 - (winMode == 2 ? 0 : 0) && ((winMode == 1 && y >= y0 + 1 && (x + z) % 2 == 0) || (winMode == 2 && y >= y0 + 1 && (x + z) % 2 == 0));
			Put(x, y, z, top ? roof : wall, k, hp, win, top);
		}
	}
	private void Tree(int x, int z) { Put(x, 0, z, Color.FromArgb(120, 84, 52), 2, 0.5f, false, false); Put(x, 1, z, Color.FromArgb(120, 84, 52), 2, 0.5f, false, false); Color gc = Color.FromArgb(70, 160, 78); for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) Put(x + dx, 2, z + dz, gc, 2, 0.5f, false, false); Put(x, 3, z, Color.FromArgb(86, 176, 92), 2, 0.5f, false, false); }
	private void Car(int x, int z, bool alongX, Color c) { Put(x, 0, z, c, 3, 0.6f, false, false); Put(alongX ? x + 1 : x, 0, alongX ? z : z + 1, c, 3, 0.6f, false, false); Put(x, 1, z, Color.FromArgb(150, 190, 220), 3, 0.6f, false, false); }
	private void Build()
	{
		grid = new Blk[N, HH, N]; all.Clear(); hallTotal = 0;
		Color old = Color.FromArgb(206, 200, 186), oroof = Color.FromArgb(128, 128, 124);
		Shell(6, 13, 16, 8, 0, 4, old, oroof, 0, 0.5f, 1);                                      // 구청사 (4층, 외관은 자료가 없어 약식)
		for (int x = 12; x < 16; x++) for (int z = 21; z < 23; z++) Put(x, 2, z, Color.FromArgb(170, 164, 152), 0, 0.6f, false, true);   // 현관 지붕
		foreach (int px in new[] { 12, 15 }) for (int y = 0; y < 2; y++) Put(px, y, 22, Color.FromArgb(222, 218, 208), 0, 0.6f, false, false);
		for (int y = 4; y < 7; y++) Put(21, y, 14, Color.FromArgb(120, 120, 126), 0, 0.5f, false, false);   // 옥상 안테나
		for (int i = 0; i < 5; i++) Put(23, i, 21, Color.FromArgb(200, 200, 205), 2, 0.5f, false, false); Put(24, 4, 21, Color.FromArgb(220, 70, 70), 2, 0.5f, false, false);   // 깃대
		Shell(0, 0, 5, 6, 0, 7, Color.FromArgb(236, 220, 170), Color.FromArgb(170, 150, 120), 1, 0.7f, 1);
		Shell(22, 1, 6, 6, 0, 9, Color.FromArgb(180, 204, 226), Color.FromArgb(120, 138, 160), 1, 0.7f, 1);
		Shell(0, 9, 4, 6, 0, 4, Color.FromArgb(232, 190, 190), Color.FromArgb(160, 120, 120), 1, 0.7f, 1);
		Shell(24, 10, 4, 7, 0, 5, Color.FromArgb(226, 214, 190), Color.FromArgb(150, 134, 112), 1, 0.7f, 1);
		Shell(0, 17, 4, 5, 0, 3, Color.FromArgb(196, 220, 196), Color.FromArgb(120, 150, 120), 1, 0.7f, 1);
		Shell(24, 18, 4, 5, 0, 4, Color.FromArgb(214, 196, 226), Color.FromArgb(140, 120, 160), 1, 0.7f, 1);
		int[,] ts = { { 5, 11 }, { 4, 22 }, { 23, 12 }, { 22, 23 }, { 8, 23 }, { 18, 23 }, { 5, 9 }, { 22, 9 }, { 3, 8 }, { 26, 8 }, { 1, 24 }, { 26, 24 } };
		for (int i = 0; i < ts.GetLength(0); i++) Tree(ts[i, 0], ts[i, 1]);
		Color[] cc = { Color.FromArgb(220, 70, 70), Color.FromArgb(70, 120, 220), Color.FromArgb(240, 200, 60), Color.FromArgb(240, 240, 240), Color.FromArgb(60, 60, 70) };
		for (int i = 0; i < 5; i++) Car(2 + i * 5, 24 + (i % 2) * 2, true, cc[(i + 2) % 5]);
		for (int i = 0; i < 3; i++) Car(7 + i * 5, 11, true, cc[i % 5]);
		hallAlive = hallTotal;
	}
	private static readonly Color NewWall = Color.FromArgb(236, 238, 242), NewGlass = Color.FromArgb(206, 220, 234), NewRoof = Color.FromArgb(150, 160, 176);
	private void NewLayer(int y)
	{
		// 미륵사지 석탑을 건물 앞쪽에 상징적으로 담았다는 당선작 설명에 맞춰, 위로 갈수록 줄어드는 층층 모양으로 만들었어요 (약식)
		int x0, z0, w, d; bool slab;
		if (y <= 2) { x0 = 7; z0 = 2; w = 14; d = 9; slab = y == 2; } else if (y <= 5) { x0 = 9; z0 = 3; w = 10; d = 7; slab = y == 5; } else { x0 = 11; z0 = 4; w = 6; d = 5; slab = y == 9; }
		for (int x = x0; x < x0 + w; x++) for (int z = z0; z < z0 + d; z++)
		{
			bool per = x == x0 || z == z0 || x == x0 + w - 1 || z == z0 + d - 1; if (!per && !slab) continue;
			Put(x, y, z, slab ? NewRoof : (y <= 2 ? NewWall : NewGlass), 4, 99f, !slab && per && y >= 1 && (x + z) % 2 == 0, slab);
		}
		if (y == 9) { for (int k = 10; k < 13; k++) Put(16, k, 4, Color.FromArgb(200, 70, 70), 4, 99f, false, false); Put(12, 10, 5, Color.FromArgb(120, 126, 138), 4, 99f, false, true); Put(13, 10, 5, Color.FromArgb(120, 126, 138), 4, 99f, false, true); }
		if (y == 2) { for (int x = 10; x < 18; x++) Put(x, 2, 11, Color.FromArgb(196, 190, 178), 4, 99f, false, true); foreach (int px in new[] { 10, 13, 14, 17 }) { Put(px, 0, 11, Color.FromArgb(228, 226, 220), 4, 99f, false, false); Put(px, 1, 11, Color.FromArgb(228, 226, 220), 4, 99f, false, false); } }
	}
	public override void Reset()
	{
		Build(); falls.Clear(); debs.Clear(); puffs.Clear(); balls.Clear(); shots.Clear(); planes.Clear(); Fx.Clear(); yaw = yawT = 0.55f; builtYaw = 99f; dirty = true; sceneDirty = true; Score = 0; Over = false; won = false; wonT = 0f; weapon = 0; combo = 0; comboT = 0f; kills = 0; msg = ""; msgT = 0f; phase = 0; buildStep = 0; buildClicks = 0; phaseT = 0f; popT = 0f;
		for (int i = 0; i < 4; i++) cd[i] = 0f;
	}
	// ── 투영 ──────────────────────────────────────────────────
	private PointF Pr(float x, float y, float z) { float dx = x - N / 2f, dz = z - N / 2f, rx = dx * cs - dz * sn, rz = dx * sn + dz * cs; return new PointF(OX + rx * S, OY + rz * sinE * S - y * cosE * S); }
	private float Depth(float x, float y, float z) { float dx = x - N / 2f, dz = z - N / 2f; float rz = dx * sn + dz * cs; return y * sinE + rz * cosE; }
	private bool Ground(float sx, float sy, out float gx, out float gz) { float rx = (sx - OX) / S, rz = (sy - OY) / (sinE * S); gx = rx * cs + rz * sn + N / 2f; gz = -rx * sn + rz * cs + N / 2f; return gx > -1 && gz > -1 && gx < N + 1 && gz < N + 1; }
	private static readonly float[] Shade = { 0.84f, 0.66f, 0.76f, 0.58f };   // +x, -x, +z, -z
	private static readonly int[] Nx = { 1, -1, 0, 0 }, Nz = { 0, 0, 1, -1 };
	private readonly PointF[][] fp = { new PointF[4], new PointF[4], new PointF[4], new PointF[4], new PointF[4] }; private readonly float[] fs = new float[5]; private readonly int[] fk = new int[5];   // 면: 0~3 옆, 4 윗면
	private int Faces(float x, float y, float z, bool[] open)
	{
		int n = 0;
		if (open[4]) { fp[n][0] = Pr(x, y + 1, z); fp[n][1] = Pr(x + 1, y + 1, z); fp[n][2] = Pr(x + 1, y + 1, z + 1); fp[n][3] = Pr(x, y + 1, z + 1); fs[n] = 1f; fk[n] = 4; n++; }
		for (int s = 0; s < 4; s++)
		{
			if (!open[s]) continue; float nrz = Nx[s] * sn + Nz[s] * cs; if (nrz <= 0.001f) continue;
			if (s == 0) { fp[n][0] = Pr(x + 1, y, z); fp[n][1] = Pr(x + 1, y, z + 1); fp[n][2] = Pr(x + 1, y + 1, z + 1); fp[n][3] = Pr(x + 1, y + 1, z); }
			else if (s == 1) { fp[n][0] = Pr(x, y, z); fp[n][1] = Pr(x, y, z + 1); fp[n][2] = Pr(x, y + 1, z + 1); fp[n][3] = Pr(x, y + 1, z); }
			else if (s == 2) { fp[n][0] = Pr(x, y, z + 1); fp[n][1] = Pr(x + 1, y, z + 1); fp[n][2] = Pr(x + 1, y + 1, z + 1); fp[n][3] = Pr(x, y + 1, z + 1); }
			else { fp[n][0] = Pr(x, y, z); fp[n][1] = Pr(x + 1, y, z); fp[n][2] = Pr(x + 1, y + 1, z); fp[n][3] = Pr(x, y + 1, z); }
			fs[n] = Shade[s]; fk[n] = s; n++;
		}
		return n;
	}
	private readonly bool[] op = new bool[5], opAll = { true, true, true, true, true };
	private bool Empty(int x, int y, int z) { return x < 0 || z < 0 || y < 0 || x >= N || z >= N || y >= HH || grid[x, y, z] == null; }
	private void Rebuild()
	{
		cs = (float)Math.Cos(yaw); sn = (float)Math.Sin(yaw); builtYaw = yaw; vis.Clear();
		foreach (Blk b in all) { bool ex = Empty(b.X, b.Y + 1, b.Z); for (int s = 0; s < 4 && !ex; s++) ex = Empty(b.X + Nx[s], b.Y, b.Z + Nz[s]); if (!ex) continue; b.Dp = Depth(b.X + 0.5f, b.Y + 0.5f, b.Z + 0.5f); vis.Add(b); }
		vis.Sort((a, c) => a.Dp.CompareTo(c.Dp)); dirty = false; sceneDirty = true;
	}
	private static Color Mul(Color c, float f, int a = 255) { return Color.FromArgb(a, Math.Min(255, (int)(c.R * f)), Math.Min(255, (int)(c.G * f)), Math.Min(255, (int)(c.B * f))); }
	private void RenderScene(float sc)
	{
		if (scene == null || Math.Abs(sceneSc - sc) > 0.01f) { if (scene != null) scene.Dispose(); scene = new Bitmap((int)Math.Ceiling(W * sc), (int)Math.Ceiling(H * sc), System.Drawing.Imaging.PixelFormat.Format32bppPArgb); sceneSc = sc; }
		using (Graphics g = Graphics.FromImage(scene))
		{
			g.ScaleTransform(sc, sc); g.SmoothingMode = SmoothingMode.None;
			using (LinearGradientBrush sky = new LinearGradientBrush(new RectangleF(0, 0, W, 300), Color.FromArgb(130, 190, 246), Color.FromArgb(220, 238, 248), 90f)) g.FillRectangle(sky, 0, 0, W, 300);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 232, 214))) g.FillRectangle(b, 0, 300, W, H - 300);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) { g.FillEllipse(b, 40, 80, 90, 24); g.FillEllipse(b, 70, 70, 60, 26); g.FillEllipse(b, 270, 110, 100, 24); g.FillEllipse(b, 300, 100, 56, 24); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 130, 168, 150))) g.FillPolygon(b, new[] { new PointF(0, 250), new PointF(60, 218), new PointF(110, 190), new PointF(150, 176), new PointF(196, 196), new PointF(250, 220), new PointF(320, 200), new PointF(400, 236), new PointF(400, 280), new PointF(0, 280) });   // 미륵산 느낌의 먼 산
			g.SmoothingMode = SmoothingMode.None;
			PointF[] outer = { Pr(-8, 0, -8), Pr(N + 8, 0, -8), Pr(N + 8, 0, N + 8), Pr(-8, 0, N + 8) }; using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 196, 120))) g.FillPolygon(b, outer);
			PointF[] inner = { Pr(0, 0, 0), Pr(N, 0, 0), Pr(N, 0, N), Pr(0, 0, N) }; using (SolidBrush b = new SolidBrush(Color.FromArgb(172, 208, 132))) g.FillPolygon(b, inner);
			FillRect(g, 3, 12, 24, 24, Color.FromArgb(222, 214, 196)); FillRect(g, 0, 24, N, N, Color.FromArgb(120, 124, 132)); FillRect(g, 5.5f, 1f, 21.5f, 11.5f, Color.FromArgb(176, 170, 158)); FillRect(g, 6.5f, 1.8f, 20.5f, 10.8f, Color.FromArgb(198, 194, 184));
			for (int i = 0; i < 9; i++) FillRect(g, 1 + i * 3f, 25.9f, 2.2f + i * 3f, 26.1f, Color.FromArgb(240, 240, 232));
			for (int i = 0; i < vis.Count; i++)
			{
				Blk b = vis[i]; op[4] = Empty(b.X, b.Y + 1, b.Z); for (int s = 0; s < 4; s++) op[s] = Empty(b.X + Nx[s], b.Y, b.Z + Nz[s]);
				int n = Faces(b.X, b.Y, b.Z, op); float dm = b.Hp < b.Max * 0.99f ? 0.78f + 0.22f * Math.Max(0f, b.Hp / b.Max) : 1f;
				for (int f = 0; f < n; f++)
				{
					using (SolidBrush br = new SolidBrush(Mul(b.C, fs[f] * dm))) g.FillPolygon(br, fp[f]);
					if (b.Win && fk[f] < 4) { PointF[] q = fp[f]; PointF a0 = Lerp(q[0], q[1], 0.2f), a1 = Lerp(q[0], q[1], 0.8f), b0 = Lerp(q[3], q[2], 0.2f), b1 = Lerp(q[3], q[2], 0.8f); PointF[] w = { Lerp(a0, b0, 0.25f), Lerp(a1, b1, 0.25f), Lerp(a1, b1, 0.75f), Lerp(a0, b0, 0.75f) }; using (SolidBrush wb = new SolidBrush(Mul(Color.FromArgb(96, 130, 176), fs[f]))) g.FillPolygon(wb, w); }
				}
			}
			g.SmoothingMode = SmoothingMode.AntiAlias;
		}
		sceneDirty = false;
	}
	private static PointF Lerp(PointF a, PointF b, float t) { return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }
	private void FillRect(Graphics g, float x0, float z0, float x1, float z1, Color c) { PointF[] p = { Pr(x0, 0, z0), Pr(x1, 0, z0), Pr(x1, 0, z1), Pr(x0, 0, z1) }; using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, p); }
	// ── 조준·발사 ─────────────────────────────────────────────
	private void Pick(float mx, float my)
	{
		aimOk = false; if (my < 50 || my > 520) return;
		for (int i = vis.Count - 1; i >= 0; i--)
		{
			Blk b = vis[i]; op[4] = Empty(b.X, b.Y + 1, b.Z); for (int s = 0; s < 4; s++) op[s] = Empty(b.X + Nx[s], b.Y, b.Z + Nz[s]); int n = Faces(b.X, b.Y, b.Z, op);
			for (int f = 0; f < n; f++) if (Inside(fp[f], mx, my)) { aimOk = true; ax = b.X + 0.5f; ay = b.Y + 0.5f; az = b.Z + 0.5f; return; }
		}
		float gx, gz; if (Ground(mx, my, out gx, out gz)) { aimOk = true; ax = gx; ay = 0f; az = gz; }
	}
	private static bool Inside(PointF[] p, float x, float y) { bool c = false; for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) if (((p[i].Y > y) != (p[j].Y > y)) && (x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X)) c = !c; return c; }
	public override void Click(float x, float y)
	{
		if (Over || won) return;
		if (phase == 1) { if (y < 526f) Build1(); else { if (y > 470f && (x < 50f || x > 350f)) yawT += x < 50f ? -0.5f : 0.5f; } return; }
		if (y >= 526f) { int i = (int)((x - 6f) / 98f); if (i >= 0 && i < 4) { weapon = i; Sfx.Play("tick"); } return; }
		if (y > 470f && (x < 50f || x > 350f)) { yawT += x < 50f ? -0.5f : 0.5f; Sfx.Play("tick"); return; }
		Pick(x, y); if (aimOk) Fire(ax, ay, az);
	}
	public override void Press(Keys k)
	{
		if (phase == 1 && !Over && !won && k == Keys.Space) { Build1(); return; }
		if (k >= Keys.D1 && k <= Keys.D4) weapon = k - Keys.D1; else if (k >= Keys.NumPad1 && k <= Keys.NumPad4) weapon = k - Keys.NumPad1;
		else if (k == Keys.Left || k == Keys.A) yawT -= 0.4f; else if (k == Keys.Right || k == Keys.D) yawT += 0.4f;
	}
	private void Build1()
	{
		if (buildStep >= 10) return; buildClicks++; popT = 0.12f; Sfx.Play("tick"); Score += 1;
		if (buildClicks >= 4) { buildClicks = 0; NewLayer(buildStep); buildStep++; Score += 8; Shake = Math.Max(Shake, 0.12f); Sfx.Play("ok"); dirty = true; PointF c = Pr(14f, buildStep, 6f); Fx.Burst(c.X, c.Y, Color.FromArgb(255, 236, 160), 8); for (int i = 0; i < 3 && puffs.Count < 70; i++) puffs.Add(new Puff { X = 8f + (float)Rnd.NextDouble() * 12f, Y = buildStep - 0.5f, Z = 2f + (float)Rnd.NextDouble() * 8f, Life = 1.0f, R = 1.1f }); msg = buildStep + "층 완성!"; msgT = 0.6f; }
	}
	private void Fire(float tx, float ty, float tz)
	{
		if (cd[weapon] > 0f) { msg = WName[weapon] + " 재장전 중…"; msgT = 0.6f; return; }
		cd[weapon] = CdMax[weapon]; PointF t = Pr(tx, ty, tz);
		if (weapon == 0) { shots.Add(new Shot { Kind = 0, Dur = 0.55f, From = new PointF(60f + (float)Rnd.NextDouble() * 280f, H - 80f), Tx = tx, Ty = ty, Tz = tz, R = 2.6f, Pow = 2.1f }); Sfx.Play("whoosh"); }
		else if (weapon == 1) { shots.Add(new Shot { Kind = 1, Dur = 1.0f, From = new PointF(t.X + 150f, -60f), Tx = tx, Ty = ty, Tz = tz, R = 4.4f, Pow = 3.6f }); Sfx.Play("fall"); }
		else if (weapon == 2) { for (int i = 0; i < 7; i++) shots.Add(new Shot { Kind = 2, T = -i * 0.08f, Dur = 0.3f, From = new PointF(200f + (i - 3) * 40f, H - 80f), Tx = tx + ((float)Rnd.NextDouble() - 0.5f) * 3f, Ty = ty, Tz = tz + ((float)Rnd.NextDouble() - 0.5f) * 3f, R = 1.6f, Pow = 1.7f }); Sfx.Play("whoosh"); }
		else { Plane p = new Plane { X = -50f, Y = Math.Max(70f, t.Y - 170f), Vx = 330f, Dir = 1f, Tx = tx, Ty = ty, Tz = tz, Ks = new float[11], Done = new bool[11] }; planes.Add(p); Sfx.Play("whoosh"); }
	}
	private void Explode(float x, float y, float z, float r, float pow)
	{
		balls.Add(new Ball { X = x, Y = y, Z = z, R = r }); if (puffs.Count < 70) for (int i = 0; i < 3; i++) puffs.Add(new Puff { X = x + ((float)Rnd.NextDouble() - 0.5f) * r, Y = y + (float)Rnd.NextDouble() * r * 0.6f, Z = z + ((float)Rnd.NextDouble() - 0.5f) * r, Life = 1.2f + (float)Rnd.NextDouble(), R = r * 0.5f });
		int x0 = Math.Max(0, (int)(x - r)), x1 = Math.Min(N - 1, (int)(x + r)), y0 = Math.Max(0, (int)(y - r)), y1 = Math.Min(HH - 1, (int)(y + r)), z0 = Math.Max(0, (int)(z - r)), z1 = Math.Min(N - 1, (int)(z + r)); int broke = 0;
		for (int a = x0; a <= x1; a++) for (int b = y0; b <= y1; b++) for (int c = z0; c <= z1; c++)
		{
			Blk bl = grid[a, b, c]; if (bl == null) continue; float dx = a + 0.5f - x, dy = b + 0.5f - y, dz = c + 0.5f - z, d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz); if (d >= r) continue;
			bl.Hp -= pow * (1f - d / r); if (bl.Hp <= 0f) { Kill(bl, 5, 1f); broke++; }
		}
		Shake = Math.Max(Shake, 0.12f + r * 0.07f); Sfx.Play(r > 3f ? "bad_big" : "thud"); Collapse(); dirty = true;
		if (broke > 0) { combo++; comboT = 2.2f; if (combo >= 3) { msg = combo + "연속 타격!"; msgT = 0.8f; Score += combo; } }
	}
	private void Kill(Blk b, int pieces, float spd)
	{
		grid[b.X, b.Y, b.Z] = null; all.Remove(b); int pts = b.K == 0 ? 4 : 1; if (b.K == 0) hallAlive--; Score += pts; kills++;
		for (int i = 0; i < pieces && debs.Count < 260; i++) debs.Add(new Deb { X = b.X + 0.5f, Y = b.Y + 0.5f, Z = b.Z + 0.5f, Vx = ((float)Rnd.NextDouble() - 0.5f) * 7f * spd, Vy = 3f + (float)Rnd.NextDouble() * 6f * spd, Vz = ((float)Rnd.NextDouble() - 0.5f) * 7f * spd, Life = 1.1f + (float)Rnd.NextDouble() * 0.8f, Sz = 0.18f + (float)Rnd.NextDouble() * 0.2f, C = b.C });
		if (b.K == 3 && puffs.Count < 70) puffs.Add(new Puff { X = b.X + 0.5f, Y = b.Y + 1f, Z = b.Z + 0.5f, Life = 2.2f, R = 1.2f });
	}
	private readonly bool[,,] seen = new bool[N, HH, N]; private readonly Stack<int> stk = new Stack<int>();
	private void Collapse()
	{
		Array.Clear(seen, 0, seen.Length); stk.Clear();
		foreach (Blk b in all) if (b.Y == 0) { seen[b.X, b.Y, b.Z] = true; stk.Push((b.X * HH + b.Y) * N + b.Z); }
		while (stk.Count > 0) { int v = stk.Pop(); int z = v % N, y = (v / N) % HH, x = v / N / HH; for (int s = 0; s < 6; s++) { int a = x + (s == 0 ? 1 : s == 1 ? -1 : 0), b = y + (s == 2 ? 1 : s == 3 ? -1 : 0), c = z + (s == 4 ? 1 : s == 5 ? -1 : 0); if (a < 0 || b < 0 || c < 0 || a >= N || b >= HH || c >= N || seen[a, b, c] || grid[a, b, c] == null) continue; seen[a, b, c] = true; stk.Push((a * HH + b) * N + c); } }
		List<Blk> drop = null; foreach (Blk b in all) if (!seen[b.X, b.Y, b.Z]) { if (drop == null) drop = new List<Blk>(); drop.Add(b); }
		if (drop == null) return;
		foreach (Blk b in drop) { grid[b.X, b.Y, b.Z] = null; all.Remove(b); falls.Add(new Fall { B = b, Y = b.Y, Vy = 0f }); }
		if (drop.Count > 12) { msg = "와르르 무너져요!"; msgT = 1.0f; Shake = Math.Max(Shake, 0.35f); Sfx.Play("glass_big"); }
	}
	// ── 갱신 ──────────────────────────────────────────────────
	public override void Update(float dt, GameInput inp, double t)
	{
		if (dt > 0.05f) dt = 0.05f; if (msgT > 0f) msgT -= dt; ringT += dt;
		for (int i = 0; i < 4; i++) if (cd[i] > 0f) cd[i] -= dt; if (comboT > 0f) { comboT -= dt; if (comboT <= 0f) combo = 0; }
		if (inp.Down != null) { if (inp.Down.Contains(Keys.Q) || inp.Down.Contains(Keys.Z)) yawT -= dt * 1.6f; if (inp.Down.Contains(Keys.E) || inp.Down.Contains(Keys.C)) yawT += dt * 1.6f; }
		yaw += (yawT - yaw) * Math.Min(1f, dt * 8f);
		if (dirty || Math.Abs(yaw - builtYaw) > 0.002f) Rebuild();
		if (!Over && !won && phase == 0) Pick(inp.Mx, inp.My); else aimOk = false; if (popT > 0f) popT -= dt;
		for (int i = shots.Count - 1; i >= 0; i--)
		{
			Shot s = shots[i]; s.T += dt; if (s.T < 0f) continue; float u = Math.Min(1f, s.T / s.Dur); PointF e = Pr(s.Tx, s.Ty, s.Tz);
			float e2 = s.Kind == 1 ? u * u : (s.Kind == 2 ? u : 1f - (1f - u) * (1f - u));
			s.Cur = new PointF(s.From.X + (e.X - s.From.X) * e2, s.From.Y + (e.Y - s.From.Y) * e2 - (s.Kind == 0 ? 90f * (float)Math.Sin(u * Math.PI) : 0f)); s.Tr.Add(s.Cur); if (s.Tr.Count > 12) s.Tr.RemoveAt(0);
			if (u >= 1f) { Explode(s.Tx, s.Ty, s.Tz, s.R, s.Pow); shots.RemoveAt(i); }
		}
		for (int i = planes.Count - 1; i >= 0; i--)
		{
			Plane p = planes[i]; p.X += p.Vx * dt; PointF dir = new PointF(cs, -sn);
			for (int k = 0; k < 11; k++)
			{
				if (p.Done[k]) continue; float off = (k - 5) * 1.5f; PointF tp = Pr(p.Tx + dir.X * off, p.Ty, p.Tz + dir.Y * off);
				if (p.X >= tp.X) { p.Done[k] = true; shots.Add(new Shot { Kind = 3, Dur = 0.42f, From = new PointF(p.X, p.Y + 6f), Tx = p.Tx + dir.X * off, Ty = p.Ty, Tz = p.Tz + dir.Y * off, R = 2.2f, Pow = 2.1f }); }
			}
			if (p.X > W + 60f) planes.RemoveAt(i);
		}
		for (int i = falls.Count - 1; i >= 0; i--)
		{
			Fall f = falls[i]; f.Vy -= 24f * dt; f.Y += f.Vy * dt; int top = 0; for (int y = HH - 1; y >= 0; y--) if (y < f.Y - 0.01f + 0.5f && grid[f.B.X, y, f.B.Z] != null) { top = y + 1; break; }
			if (f.Y <= top) { for (int k = 0; k < 4 && debs.Count < 260; k++) debs.Add(new Deb { X = f.B.X + 0.5f, Y = top + 0.4f, Z = f.B.Z + 0.5f, Vx = ((float)Rnd.NextDouble() - 0.5f) * 5f, Vy = 2f + (float)Rnd.NextDouble() * 4f, Vz = ((float)Rnd.NextDouble() - 0.5f) * 5f, Life = 0.9f, Sz = 0.2f, C = f.B.C }); if (f.B.K == 0) hallAlive--; Score += f.B.K == 0 ? 6 : 2; kills++; if (puffs.Count < 70 && Rnd.Next(5) == 0) puffs.Add(new Puff { X = f.B.X + 0.5f, Y = top + 0.4f, Z = f.B.Z + 0.5f, Life = 1.6f, R = 1f }); falls.RemoveAt(i); Sfx.Play("thud_soft"); }
		}
		for (int i = debs.Count - 1; i >= 0; i--) { Deb d = debs[i]; d.Life -= dt; d.Vy -= 18f * dt; d.X += d.Vx * dt; d.Y += d.Vy * dt; d.Z += d.Vz * dt; if (d.Y < 0.05f) { d.Y = 0.05f; d.Vy = -d.Vy * 0.3f; d.Vx *= 0.6f; d.Vz *= 0.6f; } if (d.Life <= 0f) debs.RemoveAt(i); }
		for (int i = puffs.Count - 1; i >= 0; i--) { Puff p = puffs[i]; p.Age += dt; p.Y += dt * 0.8f; if (p.Age >= p.Life) puffs.RemoveAt(i); }
		for (int i = balls.Count - 1; i >= 0; i--) { balls[i].Age += dt; if (balls[i].Age > 0.55f) balls.RemoveAt(i); }
		if (phase == 0 && hallTotal > 0 && hallAlive <= hallTotal * 0.12f)
		{
			foreach (Blk b in new List<Blk>(all)) if (b.K == 0) Kill(b, 3, 1.3f); Collapse(); dirty = true; shots.Clear(); planes.Clear(); phase = 1; phaseT = 0f; int bonus = (int)(Math.Max(0f, 60f - PlayT) * 3f); Score += 150 + bonus; msg = "구청사 철거 완료! 이제 신청사를 지어요 (+" + (150 + bonus) + ")"; msgT = 3f; Sfx.Play("fanfare"); Shake = 0.5f; for (int i = 0; i < 3; i++) Fx.Burst(70f + i * 130f, 220f, Color.FromArgb(255, 226, 110), 12);
		}
		if (phase == 1) { phaseT += dt; if (!won && buildStep >= 10) { won = true; wonT = 0f; int bonus = (int)(Math.Max(0f, 60f - PlayT) * 6f); Score += 400 + bonus; msg = "신청사 준공! 보너스 +" + (400 + bonus); msgT = 3.5f; Sfx.Play("homerun"); for (int i = 0; i < 5; i++) Fx.Burst(50f + i * 75f, 190f + (i % 2) * 40f, Color.FromArgb(255, 226, 110), 14); } }
		if (won) { wonT += dt; if (wonT > 3f) Over = true; }
	}
	// ── 그리기 ────────────────────────────────────────────────
	public override void Draw(Graphics g, double t)
	{
		float sc = Math.Max(1f, Math.Min(2f, ImgCache.Scale(g))); if (dirty) Rebuild(); if (scene == null || sceneDirty || Math.Abs(sceneSc - sc) > 0.01f) RenderScene(sc);
		GraphicsState st0 = g.Save(); g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(scene, 0, 0, W, H); g.Restore(st0);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		// 폭발 범위 표시
		if (aimOk && !Over && !won) { PointF a = Pr(ax, ay, az); float r = WR[weapon] * S; using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 255, 70, 50))) g.FillEllipse(b, a.X - r, a.Y - r * sinE, r * 2, r * 2 * sinE); using (Pen p = new Pen(Color.FromArgb(150 + (int)(60 * Math.Sin(ringT * 8)), 255, 90, 60), 1.6f)) g.DrawEllipse(p, a.X - r, a.Y - r * sinE, r * 2, r * 2 * sinE); }
		// 무너지는 블록
		bool[] all5 = opAll; foreach (Fall f in falls) { int n = Faces(f.B.X, f.Y, f.B.Z, all5); for (int k = 0; k < n; k++) using (SolidBrush b = new SolidBrush(Mul(f.B.C, fs[k]))) g.FillPolygon(b, fp[k]); }
		foreach (Puff p in puffs) { float u = p.Age / p.Life; PointF a = Pr(p.X, p.Y, p.Z); float r = (p.R + u * 1.4f) * S * 0.7f; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * (1f - u)), 90, 90, 96))) g.FillEllipse(b, a.X - r, a.Y - r, r * 2, r * 2); }
		foreach (Deb d in debs) { PointF a = Pr(d.X, d.Y, d.Z); float z = d.Sz * S; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1f, d.Life * 2f)), d.C))) g.FillRectangle(b, a.X - z / 2, a.Y - z / 2, z, z); }
		foreach (Ball bl in balls) { float u = bl.Age / 0.55f; PointF a = Pr(bl.X, bl.Y, bl.Z); float r = bl.R * S * (0.5f + 0.9f * (float)Math.Sqrt(u)); int al = (int)(235 * (1f - u)); using (SolidBrush b = new SolidBrush(Color.FromArgb(al, 255, 120, 30))) g.FillEllipse(b, a.X - r, a.Y - r, r * 2, r * 2); r *= 0.62f; using (SolidBrush b = new SolidBrush(Color.FromArgb(al, 255, 214, 90))) g.FillEllipse(b, a.X - r, a.Y - r, r * 2, r * 2); r *= 0.5f; using (SolidBrush b = new SolidBrush(Color.FromArgb(al, 255, 252, 220))) g.FillEllipse(b, a.X - r, a.Y - r, r * 2, r * 2); using (Pen p = new Pen(Color.FromArgb((int)(al * 0.6f), 255, 255, 255), 2f)) { float rr = bl.R * S * 1.6f * u; g.DrawEllipse(p, a.X - rr, a.Y - rr * sinE, rr * 2, rr * 2 * sinE); } }
		foreach (Shot s in shots) { if (s.T < 0f) continue; DrawShot(g, s); }
		foreach (Plane p in planes) DrawPlane(g, p);
		Fx.Draw(g);
		// HUD
		float pc = phase == 1 ? buildStep / 10f : (hallTotal <= 0 ? 0f : (hallTotal - hallAlive) / (float)hallTotal);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(70, 44, 260, 16), 8)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 12, 20, 46))) g.FillPath(b, gp); }
		if (pc > 0.01f) using (GraphicsPath gp = Gfx.Round(new RectangleF(70, 44, Math.Max(10f, 260f * Math.Min(1f, pc)), 16), 8)) using (SolidBrush b = new SolidBrush(phase == 1 ? Color.FromArgb(90, 190, 130) : (pc > 0.88f ? Color.FromArgb(255, 210, 70) : Color.FromArgb(255, 110, 70)))) g.FillPath(b, gp);
		Txt(g, phase == 1 ? "신청사 건설 " + buildStep + " / 10층" : "구청사 철거율 " + Pct() + "%", 9f, 2, Color.White, 70, 44, 260, 16, StringAlignment.Center);
		DrawMascot(g, 34f, 112f, 56f, 0f);
		if (msgT > 0f) Txt(g, msg, 15f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2f)), 255, 236, 130), 0, 150, W, 28, StringAlignment.Center);
		// 화면 돌리기 버튼
		for (int s2 = 0; s2 < 2; s2++) { float cx = s2 == 0 ? 26f : W - 26f; using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 12, 20, 46))) g.FillEllipse(b, cx - 18, 478, 36, 36); PointF[] tri = s2 == 0 ? new[] { new PointF(cx + 5, 488), new PointF(cx + 5, 504), new PointF(cx - 7, 496) } : new[] { new PointF(cx - 5, 488), new PointF(cx - 5, 504), new PointF(cx + 7, 496) }; using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, tri); }
		DrawCrane(g);
		// 무기 막대
		if (phase == 1) { DrawBuildBar(g); return; }
		for (int i = 0; i < 4; i++)
		{
			RectangleF r = new RectangleF(6 + i * 98, 528, 92, 64); bool sel = i == weapon;
			using (GraphicsPath gp = Gfx.Round(r, 12)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(sel ? 215 : 160, sel ? 40 : 14, sel ? 52 : 22, sel ? 90 : 46))) g.FillPath(b, gp); using (Pen p = new Pen(sel ? Color.FromArgb(255, 214, 90) : Color.FromArgb(90, 190, 215, 255), sel ? 2.2f : 1f)) g.DrawPath(p, gp); }
			if (cd[i] > 0f) { float h = 64f * Math.Min(1f, cd[i] / CdMax[i]); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRectangle(b, r.X + 2, r.Bottom - h, r.Width - 4, h - 2); }
			WIcon(g, i, r.X + 46, r.Y + 24); Txt(g, (i + 1) + " " + WName[i], 9.5f, 2, cd[i] > 0f ? Color.FromArgb(200, 200, 210) : Color.White, r.X, r.Y + 42, r.Width, 18, StringAlignment.Center);
		}
		if (Pct() == 0 && PlayT < 6f && !Over) Hint(g, "구청사를 클릭해서 부숴요! 밑을 노리면 와르르", 132, 12.5f, false, 1f);
	}
	private void DrawCrane(Graphics g)
	{
		float h = Math.Max(12f, buildStep + 4f); PointF b = Pr(6.2f, 0f, 1.6f), t = Pr(6.2f, h, 1.6f), jb = Pr(15.5f, h, 1.6f), jf = Pr(4.5f, h, 1.6f), hk = Pr(13f, h - 2f - (popT > 0f ? 0.6f : 0f), 1.6f);
		using (Pen p = new Pen(Color.FromArgb(230, 232, 176, 36), 3.2f)) { g.DrawLine(p, b, t); g.DrawLine(p, jf, jb); }
		using (Pen p = new Pen(Color.FromArgb(200, 60, 60, 66), 1f)) { g.DrawLine(p, jb.X, jb.Y, hk.X, hk.Y); g.DrawLine(p, t.X, t.Y - 12f, jb.X, jb.Y); g.DrawLine(p, t.X, t.Y - 12f, jf.X, jf.Y); }
		using (Pen p = new Pen(Color.FromArgb(230, 232, 176, 36), 2.2f)) g.DrawLine(p, t.X, t.Y, t.X, t.Y - 12f);
		using (SolidBrush br = new SolidBrush(Color.FromArgb(220, 60, 60, 66))) g.FillRectangle(br, hk.X - 4, hk.Y, 8, 6); using (SolidBrush br = new SolidBrush(Color.FromArgb(230, 232, 176, 36))) g.FillRectangle(br, t.X - 5, t.Y + 1, 10, 7);
	}
	private void DrawBuildBar(Graphics g)
	{
		RectangleF r = new RectangleF(10, 530, 380, 60); float u = buildStep >= 10 ? 1f : buildClicks / 4f;
		using (GraphicsPath gp = Gfx.Round(r, 14)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(popT > 0f ? 235 : 195, 40, 90, 70))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(255, 214, 90), 2f)) g.DrawPath(p, gp); }
		using (GraphicsPath gp = Gfx.Round(new RectangleF(24, 566, 352, 8), 4)) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(b, gp);
		if (u > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(24, 566, 352f * u, 8), 4)) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 226, 120))) g.FillPath(b, gp);
		Txt(g, buildStep >= 10 ? "준공!" : "클릭! 클릭! 한 층씩 올려요", 13f, 2, Color.White, 10, 536, 380, 26, StringAlignment.Center);
	}
	private void WIcon(Graphics g, int i, float cx, float cy)
	{
		if (i == 0) { using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 224, 232))) g.FillRectangle(b, cx - 3, cy - 14, 6, 26); using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 70, 60))) g.FillPolygon(b, new[] { new PointF(cx - 3, cy - 14), new PointF(cx + 3, cy - 14), new PointF(cx, cy - 22) }); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 130, 150))) { g.FillPolygon(b, new[] { new PointF(cx - 3, cy + 6), new PointF(cx - 9, cy + 14), new PointF(cx - 3, cy + 12) }); g.FillPolygon(b, new[] { new PointF(cx + 3, cy + 6), new PointF(cx + 9, cy + 14), new PointF(cx + 3, cy + 12) }); } }
		else if (i == 1) { using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 150, 40))) g.FillPolygon(b, new[] { new PointF(cx - 6, cy - 6), new PointF(cx + 16, cy - 18), new PointF(cx + 2, cy + 2) }); using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 110, 90))) g.FillEllipse(b, cx - 12, cy - 8, 20, 20); using (SolidBrush b = new SolidBrush(Color.FromArgb(100, 76, 60))) g.FillEllipse(b, cx - 6, cy - 2, 5, 5); }
		else if (i == 2) { using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 214, 90))) for (int k = 0; k < 3; k++) g.FillEllipse(b, cx - 14 + k * 12, cy - 6 + (k % 2) * 8, 7, 7); }
		else { using (SolidBrush b = new SolidBrush(Color.FromArgb(210, 216, 228))) { g.FillEllipse(b, cx - 16, cy - 4, 32, 9); g.FillPolygon(b, new[] { new PointF(cx - 2, cy), new PointF(cx + 8, cy - 10), new PointF(cx + 12, cy - 10), new PointF(cx + 6, cy) }); } using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 76, 96))) { g.FillEllipse(b, cx - 3, cy + 8, 5, 7); g.FillEllipse(b, cx + 6, cy + 8, 5, 7); } }
	}
	private void DrawPlane(Graphics g, Plane p)
	{
		GraphicsState st = g.Save(); g.TranslateTransform(p.X, p.Y);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, -26, 14, 52, 8);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 220, 232))) { g.FillEllipse(b, -26, -6, 52, 12); g.FillPolygon(b, new[] { new PointF(-4, 0), new PointF(8, -16), new PointF(14, -16), new PointF(8, 0) }); g.FillPolygon(b, new[] { new PointF(-4, 0), new PointF(8, 14), new PointF(14, 14), new PointF(8, 0) }); g.FillPolygon(b, new[] { new PointF(-24, -2), new PointF(-30, -12), new PointF(-22, -2) }); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 150, 220))) g.FillEllipse(b, 12, -4, 10, 6);
		g.Restore(st);
	}
	private void DrawShot(Graphics g, Shot s)
	{
		int n = s.Tr.Count; for (int i = 0; i < n; i++) { float u = (i + 1f) / n; float r = (s.Kind == 1 ? 9f : 3.5f) * u; Color c = s.Kind == 1 ? Color.FromArgb((int)(150 * u), 255, 150, 50) : Color.FromArgb((int)(130 * u), 220, 220, 230); using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, s.Tr[i].X - r, s.Tr[i].Y - r, r * 2, r * 2); }
		if (s.Kind == 1) { using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 150, 50))) g.FillEllipse(b, s.Cur.X - 17, s.Cur.Y - 17, 34, 34); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 92, 74))) g.FillEllipse(b, s.Cur.X - 12, s.Cur.Y - 12, 24, 24); using (SolidBrush b = new SolidBrush(Color.FromArgb(86, 66, 54))) { g.FillEllipse(b, s.Cur.X - 6, s.Cur.Y - 4, 7, 7); g.FillEllipse(b, s.Cur.X + 2, s.Cur.Y + 3, 5, 5); } }
		else if (s.Kind == 0) { float ang = 0f; if (n >= 2) ang = (float)(Math.Atan2(s.Tr[n - 1].Y - s.Tr[n - 2].Y, s.Tr[n - 1].X - s.Tr[n - 2].X) * 180 / Math.PI); GraphicsState st = g.Save(); g.TranslateTransform(s.Cur.X, s.Cur.Y); g.RotateTransform(ang); using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 232, 238))) g.FillRectangle(b, -10, -3, 20, 6); using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 70, 60))) g.FillPolygon(b, new[] { new PointF(10, -3), new PointF(10, 3), new PointF(17, 0) }); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 130, 150))) g.FillPolygon(b, new[] { new PointF(-10, -3), new PointF(-14, -8), new PointF(-6, -3) }); g.Restore(st); }
		else if (s.Kind == 3) { using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 54, 66))) g.FillEllipse(b, s.Cur.X - 4, s.Cur.Y - 6, 8, 12); }
		else { using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 226, 110))) g.FillEllipse(b, s.Cur.X - 3, s.Cur.Y - 3, 6, 6); }
	}
}
