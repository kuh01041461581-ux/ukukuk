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
	public override string Desc { get { return "코스 9개 · 부스트·점프 발판 · CPU 5대와 3바퀴"; } }
	public override string Help { get { return "먼저 코스를 골라요 (마을·사막·빙하·숲·외계·중세·요정·일본·청사)\n←→ (또는 마우스 좌우)로 핸들 · 가속은 자동, ↓ 브레이크 · 노란 화살표 발판은 부스트, 줄무늬 발판은 점프\n커브에서 스페이스·Shift(또는 마우스를 누른 채)로 드리프트 → 게이지를 모아 놓으면 부스터!\n아이템 상자: 부스터 · 바나나(뒤에 놓기) · 물풍선(앞차 맞히기) — Z·X·Ctrl 또는 아이템 칸 클릭으로 사용\n길 밖 잔디는 느려져요 · 3바퀴를 먼저 돌면 1등! · 갈고리 U턴 두 번 · S자 · 긴 내리막 커브 · 계단 커브 — 드리프트로 부스터를 모아요\nCPU 5대도 아이템을 써요 (물풍선 조심!)"; } }
	public override string RightText { get { return lap < Laps ? (lap + 1) + "/" + Laps + "바퀴" : "완주"; } }
	public override void Icon(Graphics g, float cx, float cy) { DrawKart(g, cx, cy + 16, 34f, Color.FromArgb(40, 170, 150), true, 0f, 0f, "ms_kart_me0"); }

	private const int N = 1260, DRAW = 140, Laps = 3; private const float SEG = 200f, ROADW = 2000f, CAMH = 1000f, DEPTH = 0.84f, MAXV = 10000f, LEN = N * SEG;
	private readonly float[] curve = new float[N], hill = new float[N]; private readonly List<PointF> mapPts = new List<PointF>();
	private class Car { public float Z, X, V, Tgt, TgtT, Spin, SpinMax = 1f, Bub, Skill, Boost, ItemT, Drift, Giant, GS = 1f, GV; public int Lap, Item = -1; public Color C; public string Name; public bool Done; public float DoneT; }
	private class Box { public int Seg; public float X, Hide; }
	private class Ban { public float Z, X; }
	private readonly List<Car> cars = new List<Car>(); private readonly List<Box> boxes = new List<Box>(); private readonly List<Ban> bans = new List<Ban>();
	private float pos, px, v, el, skyOff, drift, charge, boostT, spinT, msgT, bumpT, steerVis, finT, hitT; private int lap, item = -1, place = 6, driftDir; private bool finished, wasDrift; private string msg = ""; private float itemRoll;
	private static readonly string[] ItemName = { "부스터", "바나나", "물풍선", "거대화" };
	// ── 움직임 연출 ──
	private float gauge, driftVis, spinMax = 1f, rollT, rollDur, giantT, gS = 1f, gV, splashT, warnT; private int bst;
	private class Pt { public float X, Y, VX, VY, Life, Max, Size; public int K; public Color C; }
	private readonly List<Pt> parts = new List<Pt>();
	private class Shot { public float Z, X, V, T, Dur, Z0, X0; public Car Tgt, From; public bool AtMe; }
	private readonly List<Shot> shots = new List<Shot>();
	private class Spl { public float Z, X, T; }
	private readonly List<Spl> spls = new List<Spl>();
	private readonly float[] skL = new float[N], skR = new float[N], skT = new float[N];   // 바닥 스키드 자국 (칸마다 왼/오른 바퀴 위치 · 남긴 시각)
	private void Part(int k, float x, float y, float vx, float vy, float life, float size, Color c) { if (parts.Count > 160) return; parts.Add(new Pt { K = k, X = x, Y = y, VX = vx, VY = vy, Life = life, Max = life, Size = size, C = c }); }
	private float PZ { get { return CAMH * DEPTH; } }   // 카메라에서 플레이어까지 거리
	// ── 코스 · 테마 ──
	private sealed class Thm
	{
		public string Name, Sub, Tags; public Color SkyA, SkyB, Sun, HillF, HillN, G1, G2, R1, R2, Ru1, Ru2, Ln, Slow; public float OffSlow = 0.42f, Grip = 1f, Grav = 1f; public bool Night; public int Stars;
	}
	private static readonly Thm[] Th = {
		new Thm { Name = "마룡이 마을 순환로", Sub = "빌리지", Tags = "부스트 · 점프 · 언덕", Stars = 1, SkyA = Color.FromArgb(120, 190, 250), SkyB = Color.FromArgb(205, 232, 255), Sun = Color.FromArgb(255, 250, 210), HillF = Color.FromArgb(150, 190, 210), HillN = Color.FromArgb(110, 170, 130), G1 = Color.FromArgb(96, 182, 92), G2 = Color.FromArgb(86, 168, 84), R1 = Color.FromArgb(110, 112, 122), R2 = Color.FromArgb(104, 106, 116), Ru1 = Color.FromArgb(230, 70, 70), Ru2 = Color.FromArgb(245, 245, 245), Ln = Color.FromArgb(235, 235, 220), Slow = Color.FromArgb(120, 90, 60) },
		new Thm { Name = "모래바람 사막 랠리", Sub = "사막", Tags = "모래 언덕 · 점프 · 긴 직선", Stars = 2, SkyA = Color.FromArgb(250, 170, 110), SkyB = Color.FromArgb(255, 228, 170), Sun = Color.FromArgb(255, 245, 200), HillF = Color.FromArgb(222, 170, 120), HillN = Color.FromArgb(232, 190, 128), G1 = Color.FromArgb(236, 204, 140), G2 = Color.FromArgb(226, 192, 128), R1 = Color.FromArgb(150, 128, 106), R2 = Color.FromArgb(142, 120, 100), Ru1 = Color.FromArgb(214, 96, 52), Ru2 = Color.FromArgb(250, 236, 206), Ln = Color.FromArgb(250, 236, 206), Slow = Color.FromArgb(214, 176, 112), OffSlow = 0.34f },
		new Thm { Name = "꽁꽁 빙하 협곡", Sub = "빙판", Tags = "미끄러운 빙판 · 점프", Stars = 3, SkyA = Color.FromArgb(150, 196, 240), SkyB = Color.FromArgb(226, 240, 252), Sun = Color.FromArgb(255, 255, 240), HillF = Color.FromArgb(190, 214, 236), HillN = Color.FromArgb(236, 244, 252), G1 = Color.FromArgb(240, 246, 252), G2 = Color.FromArgb(226, 236, 248), R1 = Color.FromArgb(150, 196, 226), R2 = Color.FromArgb(140, 186, 220), Ru1 = Color.FromArgb(70, 130, 220), Ru2 = Color.FromArgb(250, 252, 255), Ln = Color.FromArgb(240, 250, 255), Slow = Color.FromArgb(200, 220, 236), Grip = 0.32f },
		new Thm { Name = "마룡이 숲 오솔길", Sub = "포레스트", Tags = "S자 커브 · 버섯 점프", Stars = 2, SkyA = Color.FromArgb(110, 190, 220), SkyB = Color.FromArgb(200, 236, 220), Sun = Color.FromArgb(255, 250, 210), HillF = Color.FromArgb(90, 150, 120), HillN = Color.FromArgb(50, 120, 80), G1 = Color.FromArgb(62, 140, 70), G2 = Color.FromArgb(54, 128, 64), R1 = Color.FromArgb(150, 120, 86), R2 = Color.FromArgb(140, 112, 80), Ru1 = Color.FromArgb(236, 200, 80), Ru2 = Color.FromArgb(110, 80, 50), Ln = Color.FromArgb(220, 196, 150), Slow = Color.FromArgb(100, 80, 54) },
		new Thm { Name = "보랏빛 외계 행성", Sub = "외계행성", Tags = "낮은 중력 · 큰 언덕 · 점프", Stars = 3, SkyA = Color.FromArgb(30, 16, 70), SkyB = Color.FromArgb(120, 60, 160), Sun = Color.FromArgb(150, 255, 220), HillF = Color.FromArgb(80, 50, 140), HillN = Color.FromArgb(50, 130, 140), G1 = Color.FromArgb(70, 160, 150), G2 = Color.FromArgb(60, 146, 140), R1 = Color.FromArgb(70, 60, 110), R2 = Color.FromArgb(64, 54, 102), Ru1 = Color.FromArgb(120, 255, 200), Ru2 = Color.FromArgb(200, 120, 255), Ln = Color.FromArgb(160, 255, 230), Slow = Color.FromArgb(160, 90, 200), Grav = 0.45f, Night = true },
		new Thm { Name = "중세 성곽 마을", Sub = "중세", Tags = "직각 시케인 · 돌길", Stars = 2, SkyA = Color.FromArgb(130, 170, 220), SkyB = Color.FromArgb(220, 228, 240), Sun = Color.FromArgb(255, 248, 220), HillF = Color.FromArgb(140, 160, 180), HillN = Color.FromArgb(110, 140, 110), G1 = Color.FromArgb(120, 160, 96), G2 = Color.FromArgb(110, 150, 90), R1 = Color.FromArgb(150, 140, 130), R2 = Color.FromArgb(140, 130, 122), Ru1 = Color.FromArgb(70, 80, 160), Ru2 = Color.FromArgb(236, 210, 120), Ln = Color.FromArgb(200, 190, 176), Slow = Color.FromArgb(110, 120, 140) },
		new Thm { Name = "요정의 꽃길", Sub = "요정", Tags = "부스트 꽃밭 · 무지개 점프", Stars = 1, SkyA = Color.FromArgb(250, 190, 230), SkyB = Color.FromArgb(255, 236, 246), Sun = Color.FromArgb(255, 255, 230), HillF = Color.FromArgb(210, 190, 240), HillN = Color.FromArgb(170, 220, 180), G1 = Color.FromArgb(160, 220, 150), G2 = Color.FromArgb(150, 210, 146), R1 = Color.FromArgb(232, 206, 230), R2 = Color.FromArgb(224, 196, 224), Ru1 = Color.FromArgb(255, 150, 200), Ru2 = Color.FromArgb(255, 250, 250), Ln = Color.FromArgb(255, 255, 255), Slow = Color.FromArgb(200, 160, 220) },
		new Thm { Name = "일본 신사 언덕길", Sub = "일본", Tags = "급 헤어핀 · 대나무 숲", Stars = 3, SkyA = Color.FromArgb(240, 200, 170), SkyB = Color.FromArgb(250, 236, 214), Sun = Color.FromArgb(250, 120, 90), HillF = Color.FromArgb(170, 170, 160), HillN = Color.FromArgb(100, 140, 100), G1 = Color.FromArgb(110, 156, 90), G2 = Color.FromArgb(100, 146, 84), R1 = Color.FromArgb(120, 110, 104), R2 = Color.FromArgb(112, 104, 98), Ru1 = Color.FromArgb(200, 40, 40), Ru2 = Color.FromArgb(30, 30, 34), Ln = Color.FromArgb(230, 220, 200), Slow = Color.FromArgb(150, 120, 90) },
		new Thm { Name = "청사 민원 대행진", Sub = "민원", Tags = "서류 더미 · 결재 부스트", Stars = 2, SkyA = Color.FromArgb(150, 200, 240), SkyB = Color.FromArgb(230, 240, 250), Sun = Color.FromArgb(255, 252, 230), HillF = Color.FromArgb(170, 186, 206), HillN = Color.FromArgb(150, 170, 190), G1 = Color.FromArgb(206, 210, 214), G2 = Color.FromArgb(196, 200, 206), R1 = Color.FromArgb(236, 226, 206), R2 = Color.FromArgb(228, 218, 198), Ru1 = Color.FromArgb(14, 138, 124), Ru2 = Color.FromArgb(250, 250, 250), Ln = Color.FromArgb(200, 180, 150), Slow = Color.FromArgb(250, 250, 244) },
		new Thm { Name = "미륵사지 석탑길", Sub = "익산 · 미륵사지", Tags = "석탑 모양 · 계단 커브", Stars = 3, SkyA = Color.FromArgb(150, 196, 236), SkyB = Color.FromArgb(232, 240, 248), Sun = Color.FromArgb(255, 250, 220), HillF = Color.FromArgb(150, 170, 190), HillN = Color.FromArgb(120, 160, 120), G1 = Color.FromArgb(120, 172, 100), G2 = Color.FromArgb(110, 160, 94), R1 = Color.FromArgb(196, 186, 170), R2 = Color.FromArgb(186, 176, 160), Ru1 = Color.FromArgb(150, 140, 126), Ru2 = Color.FromArgb(226, 220, 206), Ln = Color.FromArgb(240, 236, 226), Slow = Color.FromArgb(150, 130, 100) },
		new Thm { Name = "왕궁리 오층석탑길", Sub = "익산 · 왕궁리", Tags = "탑 모양 · 지그재그", Stars = 3, SkyA = Color.FromArgb(240, 150, 110), SkyB = Color.FromArgb(252, 214, 170), Sun = Color.FromArgb(255, 220, 160), HillF = Color.FromArgb(190, 140, 130), HillN = Color.FromArgb(140, 150, 100), G1 = Color.FromArgb(170, 170, 96), G2 = Color.FromArgb(160, 160, 90), R1 = Color.FromArgb(176, 160, 140), R2 = Color.FromArgb(166, 150, 132), Ru1 = Color.FromArgb(140, 120, 100), Ru2 = Color.FromArgb(230, 214, 190), Ln = Color.FromArgb(240, 230, 210), Slow = Color.FromArgb(140, 110, 80) },
		new Thm { Name = "사리장엄구 둘레길", Sub = "익산 · 사리호", Tags = "둥근 사리호 모양 · 금빛 길", Stars = 2, SkyA = Color.FromArgb(40, 50, 100), SkyB = Color.FromArgb(120, 100, 150), Sun = Color.FromArgb(255, 236, 170), HillF = Color.FromArgb(80, 80, 130), HillN = Color.FromArgb(60, 100, 90), G1 = Color.FromArgb(70, 110, 90), G2 = Color.FromArgb(64, 102, 84), R1 = Color.FromArgb(200, 170, 100), R2 = Color.FromArgb(190, 160, 92), Ru1 = Color.FromArgb(150, 110, 50), Ru2 = Color.FromArgb(250, 230, 160), Ln = Color.FromArgb(255, 240, 180), Slow = Color.FromArgb(120, 90, 60), Night = true },
	};
	private int course, cphase, hovCard = -1; private Thm T0 = Th[0];
	private static readonly List<PointF>[] courseMap = new List<PointF>[Th.Length];
	// 발판: K 1 부스트 · 2 점프 · 3 감속 구역
	private readonly byte[] padK = new byte[N]; private readonly float[] padX = new float[N], padW = new float[N];
	private float airH, airV, airT, padCd, latV, camPx;
	public override void Reset()
	{
		for (int c = 0; c < Th.Length; c++) if (courseMap[c] == null) { Build(c); courseMap[c] = new List<PointF>(mapPts); }
		cphase = 0; hovCard = -1; Score = 0; Over = false; msg = ""; msgT = 0f;
	}
	private void Pad(int seg, int len, int k, float x, float w) { for (int q = 0; q < len; q++) { int s = (seg + q) % N; padK[s] = (byte)k; padX[s] = x; padW[s] = w; } }
	private void Bump(int st, int len, float amp) { for (int k = 0; k < len; k++) { int s = st + k; if (s >= 0 && s < N) hill[s] += amp * (float)Math.Sin(Math.PI * k / len); } }
	// 코스 만들기: 커브(각도)의 합이 360도라 위에서 보면 닫힌 고리예요 (미니맵)
	private void Build(int id)
	{
		Array.Clear(curve, 0, N); Array.Clear(hill, 0, N); Array.Clear(padK, 0, N); int i = 0; float hd = 0f, mx0 = 0f, my0 = 0f; mapPts.Clear(); mapPts.Add(new PointF(0, 0));
		Action<int, float> sec = (len, deg) =>
		{
			float sum = 0f; for (int k = 0; k < len; k++) sum += (float)Math.Sin(Math.PI * k / len); if (sum <= 0f) sum = 1f;
			for (int k = 0; k < len && i < N; k++, i++)
			{
				float e = (float)Math.Sin(Math.PI * k / len), dh = deg * 0.0174533f * e / sum; curve[i] = dh * 48f; hd += dh;
				mx0 += (float)Math.Sin(hd); my0 -= (float)Math.Cos(hd); mapPts.Add(new PointF(mx0, my0));
			}
		};
		// 외곽선(꼭짓점 목록)을 따라 도는 코스: 변은 직선, 꼭짓점은 꺾이는 각도만큼 커브 (미니맵에 그 모양이 그대로 보여요)
		Action<float[]> outline = xy =>
		{
			int n = xy.Length / 2; List<PointF> P = new List<PointF>(); for (int k = 0; k < n; k++) P.Add(new PointF(xy[k * 2], xy[k * 2 + 1]));
			Func<List<PointF>, double[]> heads = Q => { double[] h = new double[Q.Count]; for (int k = 0; k < Q.Count; k++) { PointF p0 = Q[k], p1 = Q[(k + 1) % Q.Count]; h[k] = Math.Atan2(p1.X - p0.X, -(p1.Y - p0.Y)) * 57.29578; } return h; };
			Func<double, double> norm = d => { while (d > 180) d -= 360; while (d <= -180) d += 360; return d; };
			double[] hh = heads(P); double tsum = 0; for (int k = 0; k < n; k++) tsum += norm(hh[(k + 1) % n] - hh[k]);
			if (tsum < 0) { P.Reverse(); hh = heads(P); }
			// 가장 긴 변을 출발 직선으로
			int best = 0; double bl = -1; for (int k = 0; k < n; k++) { double l = Math.Sqrt(Math.Pow(P[(k + 1) % n].X - P[k].X, 2) + Math.Pow(P[(k + 1) % n].Y - P[k].Y, 2)); if (l > bl) { bl = l; best = k; } }
			List<PointF> R = new List<PointF>(); for (int k = 0; k < n; k++) R.Add(P[(best + k) % n]); hh = heads(R);
			double[] len = new double[n], turn = new double[n], cl = new double[n]; double sl = 0, sc0 = 0;
			for (int k = 0; k < n; k++) { len[k] = Math.Sqrt(Math.Pow(R[(k + 1) % n].X - R[k].X, 2) + Math.Pow(R[(k + 1) % n].Y - R[k].Y, 2)); turn[k] = norm(hh[(k + 1) % n] - hh[k]); cl[k] = Math.Max(8, Math.Abs(turn[k]) * 0.2); sl += len[k]; sc0 += cl[k]; }
			double scale = (N - 30 - sc0) / sl;
			hd = (float)(hh[0] / 57.29578);
			for (int k = 0; k < n; k++) { sec(Math.Max(3, (int)Math.Round(len[k] * scale - (cl[k] + cl[(k + n - 1) % n]) * 0.3)), 0); sec((int)Math.Round(cl[k]), (float)turn[k]); }
		};
		Func<float[], float[]> mirror = rside => { int n = rside.Length / 2; List<float> o = new List<float>(rside); for (int k = n - 1; k >= 0; k--) { if (rside[k * 2] == 0f) continue; o.Add(-rside[k * 2]); o.Add(rside[k * 2 + 1]); } return o.ToArray(); };
		int[] bx = { 100, 210, 430, 560, 780, 960, 1150 };
		switch (id)
		{
			case 0:   // 마을: 출발 직선 → 오르막 커브 → 갈고리 U턴 두 번 → S자 → 긴 내리막 커브 → 계단 커브 → 긴 직선 → 결승
				sec(141, 0); sec(80, 60); sec(6, 0); sec(56, -180); sec(35, 0); sec(56, 180); sec(52, 0); sec(36, 50); sec(36, -50); sec(10, 0); sec(110, 120); sec(109, 0);
				sec(44, -90); sec(20, 0); sec(44, 90); sec(101, 0); sec(50, 90); sec(191, 0); sec(50, 90); sec(N - i, 0);
				Bump(141, 119, 26f); Bump(590, 110, -18f); Bump(900, 100, 14f);
				Pad(60, 3, 1, -0.45f, 0.26f); Pad(1000, 3, 1, 0.45f, 0.26f); Pad(1080, 3, 1, -0.3f, 0.26f); Pad(820, 2, 2, 0f, 1f);
				break;
			case 1:   // 사막: 크게 휘는 곡선과 모래 언덕 S자가 번갈아 (오른쪽으로 크게 도는 고리)
				sec(80, 0); sec(60, 70); sec(30, 0); sec(50, -50); sec(50, 50); sec(40, 0); sec(70, 110); sec(60, 0); sec(40, -80); sec(40, 80); sec(50, 0); sec(60, 90); sec(40, -40); sec(40, 40); sec(60, 0); sec(70, 90); sec(40, -30); sec(40, 30); sec(N - i, 0);
				AutoPads(id, 3, 4, 4, 30f); break;
			case 2:   // 빙하: 미끄러운 빙판에 헤어핀이 두 번 · 협곡 S자
				sec(90, 0); sec(45, -120); sec(20, 0); sec(45, 120); sec(40, 0); sec(60, 90); sec(30, 0); sec(40, -60); sec(40, 60); sec(30, 0); sec(60, 90); sec(70, 0); sec(50, 150); sec(30, 0); sec(50, -150); sec(30, 0); sec(60, 90); sec(40, 0); sec(50, 90); sec(N - i, 0);
				AutoPads(id, 3, 3, 0, 22f); break;
			case 3:   // 숲: 좌우로 계속 꺾이는 오솔길 (S자 사슬 세 번)
				sec(70, 0); sec(40, 60); sec(40, -60); sec(40, 60); sec(40, -60); sec(30, 0); sec(60, 120); sec(30, 0); sec(40, -70); sec(40, 70); sec(40, -70); sec(40, 70); sec(30, 0); sec(60, 120); sec(40, 0); sec(50, -45); sec(50, 45); sec(30, -45); sec(30, 45); sec(60, 120); sec(N - i, 0);
				AutoPads(id, 2, 3, 3, 24f); break;
			case 4:   // 외계 행성: 큰 고리 두 개가 이어진 8자 느낌 · 거대한 언덕 · 낮은 중력
				sec(80, 0); sec(70, 150); sec(40, 0); sec(50, -90); sec(50, 90); sec(60, 0); sec(70, 150); sec(30, 0); sec(40, -60); sec(40, 60); sec(40, -60); sec(40, 60); sec(60, 0); sec(50, 60); sec(N - i, 0);
				AutoPads(id, 5, 3, 1, 50f); break;
			case 5:   // 중세: 성벽 사이 직각 커브와 시케인이 연달아
				sec(80, 0); sec(30, 90); sec(40, 0); sec(25, -90); sec(30, 0); sec(25, 90); sec(60, 0); sec(30, 90); sec(40, 0); sec(25, -90); sec(25, 90); sec(70, 0); sec(30, 90); sec(50, 0); sec(25, 90); sec(40, 0); sec(25, -90); sec(25, 90); sec(40, 0); sec(30, -45); sec(30, 45); sec(N - i, 0);
				AutoPads(id, 1, 4, 2, 14f); break;
			case 6:   // 요정: 소용돌이처럼 크게 감았다 푸는 꽃길
				sec(70, 0); sec(80, 200); sec(40, 0); sec(60, -130); sec(40, 0); sec(70, 180); sec(50, 0); sec(60, -80); sec(30, 0); sec(40, 40); sec(40, -40); sec(60, 190); sec(N - i, 0);
				AutoPads(id, 2, 7, 1, 22f); break;
			case 7:   // 일본: 신사 언덕을 오르내리는 급 헤어핀과 S자
				sec(100, 0); sec(45, 150); sec(30, 0); sec(45, -120); sec(40, 0); sec(40, 60); sec(40, -60); sec(50, 150); sec(50, 0); sec(40, -50); sec(40, 50); sec(50, 90); sec(30, 0); sec(30, -40); sec(30, 40); sec(40, 90); sec(N - i, 0);
				AutoPads(id, 1, 4, 2, 28f); break;
			case 9:   // 미륵사지 석탑: 넓은 기단과 여섯 층 지붕돌이 계단처럼 (넓고 낮은 탑 모양)
				outline(mirror(Sx(new float[] { 60, 100, 60, 95, 54, 95, 54, 90, 48, 90, 56, 82, 44, 80, 52, 72, 41, 70, 48, 63, 38, 61, 44, 55, 35, 53, 40, 48, 32, 46, 36, 42, 28, 40, 10, 38, 8, 30, 0, 26 }, 1.7f))); sec(N - i, 0);
				AutoPads(id, 2, 4, 2, 16f); break;
			case 10:   // 왕궁리 오층석탑: 날씬하게 솟은 다섯 층 지붕돌
				outline(mirror(Sx(new float[] { 40, 100, 40, 92, 26, 92, 36, 81, 22, 79, 32, 68, 19, 66, 28, 56, 16, 54, 24, 45, 13, 43, 20, 35, 8, 33, 4, 22, 0, 16 }, 0.75f))); sec(N - i, 0);
				AutoPads(id, 2, 4, 2, 20f); break;
			case 11:   // 사리장엄구(사리호): 둥근 몸통 · 잘록한 목 · 뚜껑과 꼭지
				{
					List<float> r = new List<float> { 16, 104, 18, 98 };
					for (int a2 = -55; a2 <= 60; a2 += 15) { double ra = a2 * Math.PI / 180; r.Add((float)(30 * Math.Cos(ra))); r.Add((float)(72 - 30 * Math.Sin(ra))); }
					r.AddRange(new float[] { 11, 42, 11, 36, 16, 33, 14, 26, 8, 21, 3, 19, 4, 14, 0, 9 });
					outline(mirror(r.ToArray())); sec(N - i, 0);
				}
				AutoPads(id, 2, 4, 1, 12f); break;
			default:   // 청사 민원 대행진: 복도를 도는 미로 같은 직각 코스 · 서류 더미 · 결재 완료 발판
				sec(90, 0); sec(30, 90); sec(50, 0); sec(30, 90); sec(30, 0); sec(30, -90); sec(30, 0); sec(30, -90); sec(40, 0); sec(30, 90); sec(60, 0); sec(30, 90); sec(70, 0); sec(30, 90); sec(40, 0); sec(30, -90); sec(30, 90); sec(40, 0); sec(30, 90); sec(N - i, 0);
				AutoPads(id, 1, 5, 4, 8f); break;
		}
		// 미니맵은 끝점이 출발점과 정확히 만나도록 조금씩 나눠 맞춰요 (주행에는 영향 없음)
		{ int n0 = mapPts.Count; PointF e0 = mapPts[n0 - 1]; for (int k = 0; k < n0; k++) { float f = k / (float)(n0 - 1); mapPts[k] = new PointF(mapPts[k].X - e0.X * f, mapPts[k].Y - e0.Y * f); } }
		FindShortcuts();
		boxes.Clear(); foreach (int sq in bx) { if (padK[sq] != 0) continue; for (int k = -1; k <= 1; k++) boxes.Add(new Box { Seg = sq, X = k * 0.55f }); }
	}
	private static float[] Sx(float[] a, float k) { float[] o = (float[])a.Clone(); for (int q = 0; q < o.Length; q += 2) o[q] *= k; return o; }
	// ── 벽 · 지름길 ──
	private const float WALL = 1.7f;
	private readonly sbyte[] gap = new sbyte[N];   // 벽이 트인 쪽: +1 오른쪽 · -1 왼쪽
	private readonly List<int[]> shorts = new List<int[]>();   // {들어가는 칸, 나오는 칸, 쪽, 지름길 길이}
	private float wallCd, curbBump; private int scSide, scHintLap = -1;
	// 지름길: 벽이 트인 곳으로 빠지면 흙길(따로 그리는 곧은 길)을 직접 운전해서 앞쪽 본선에 합류해요
	private bool scOn; private int[] scQ; private int scLen; private float scCam, scH0, scH1;
	private const float PATHW = 0.8f, PATHOFF = 2.4f, JOIN = 1.4f;
	private float PathHillAt(int v) { if (v < scLen) return Lerp(scH0, scH1, Clamp(v / (float)scLen, 0f, 1f)); return HillY((scQ[1] + v - scLen) * SEG); }
	private float PathCurve(int v) { return v < 0 || v >= scLen ? 0f : 1.3f * (float)Math.Sin(2 * Math.PI * v / scLen) * scSide; }   // 흙길도 살짝 굽이쳐요
	private float ScProg { get { return Clamp((scCam + PZ) / (scLen * SEG), 0f, 1f); } }
	// 미니맵에서 길이 크게 돌았다가 다시 가까워지는 두 지점을 찾아 벽을 트고 지름길로 이어요
	private void FindShortcuts()
	{
		Array.Clear(gap, 0, N); shorts.Clear(); if (mapPts.Count < N) return;
		for (int pass = 0; pass < 3 && shorts.Count < 2; pass++)
		{
			int b0 = -1, b1 = -1; double bs = 0, bd = 0;
			for (int s0 = 70; s0 < N - 100; s0 += 3)
			{
				bool near = false; foreach (int[] q in shorts) if (Math.Abs(q[0] - s0) < 160 || (s0 > q[0] && s0 < q[1])) near = true; if (near) continue;
				if (padK[s0] != 0) continue;
				bool loose = pass == 2 || (pass == 1 && shorts.Count == 0);
				for (int s1 = s0 + 60; s1 < Math.Min(N - 12, s0 + (loose ? 320 : 240)); s1 += 2)
				{
					// 한 바퀴의 1/6 이하만 줄여 주는 적당한 지름길 (너무 큰 건 재미가 없어요)
					float dx = mapPts[s1].X - mapPts[s0].X, dy = mapPts[s1].Y - mapPts[s0].Y; double d = Math.Sqrt(dx * dx + dy * dy);
					if (d < (loose ? 0.62 : 0.5) * (s1 - s0) && d > 14 && d < (loose ? 120 : 90)) { double sv = Math.Min((s1 - s0) - d * 1.3, 170); if (sv > bs + 4) { bs = sv; b0 = s0; b1 = s1; bd = d; } }
				}
			}
			if (b0 < 0 || bs < 30) continue;
			float hx = mapPts[b0 + 1].X - mapPts[b0].X, hy = mapPts[b0 + 1].Y - mapPts[b0].Y, vx = mapPts[b1].X - mapPts[b0].X, vy = mapPts[b1].Y - mapPts[b0].Y;
			float cs = 0f; for (int q = b0; q < b1; q++) cs += curve[q];
			int side = Math.Abs(cs) > 1f ? (cs > 0f ? 1 : -1) : (hx * vy - hy * vx > 0 ? 1 : -1);   // 길이 도는 안쪽으로 질러가요
			shorts.Add(new[] { b0, b1, side, (int)Math.Round(bd) }); for (int q = b0 - 46; q <= b0 + 10; q++) gap[(q + N) % N] = (sbyte)side;
		}
	}
	// 발판 자동 배치: 긴 직선 가운데에 점프, 직선 들머리에 부스트, 커브 바깥쪽에 감속 구역 · 언덕은 코스마다 다르게
	private void AutoPads(int id, int nJump, int nBoost, int nSlow, float hillAmp)
	{
		List<int[]> runs = new List<int[]>(); int st = -1;
		for (int k = 0; k <= N; k++) { bool str = k < N && Math.Abs(curve[k]) < 0.02f; if (str && st < 0) st = k; else if (!str && st >= 0) { if (k - st >= 18 && st > 8) runs.Add(new int[] { st, k - st }); st = -1; } }
		runs.Sort((a, b) => b[1].CompareTo(a[1]));
		int used = 0; for (int r = 0; r < runs.Count && used < nJump; r++) if (runs[r][1] >= 34) { Pad(runs[r][0] + runs[r][1] / 2, 2, 2, 0f, 1f); used++; }
		float[] bxs = { -0.42f, 0.42f, 0f };
		int b = 0; for (int r = runs.Count - 1; r >= 0 && b < nBoost; r--) { int sg = runs[r][0] + 4; if (padK[sg] == 0 && padK[sg + 2] == 0) { Pad(sg, 3, 1, bxs[b % 3], 0.26f); b++; } }
		for (int r = 0; r < runs.Count && b < nBoost; r++) { int sg = runs[r][0] + runs[r][1] / 4; if (padK[sg] == 0 && padK[sg + 2] == 0) { Pad(sg, 3, 1, bxs[b % 3], 0.26f); b++; } }
		int sl = 0; for (int k = 40; k < N - 20 && sl < nSlow; k += 97) { int q = k; while (q < N - 12 && Math.Abs(curve[q]) < 1.0f) q++; if (q >= N - 12) break; if (padK[q] == 0) { Pad(q, 8, 3, curve[q] > 0f ? -0.55f : 0.55f, 0.4f); sl++; } k = q; }
		Random hr = new Random(id * 77 + 5); for (int k = 0; k < 6; k++) { int s0 = 60 + k * 190 + hr.Next(60); int len = 70 + hr.Next(80); float amp = hillAmp * (0.5f + (float)hr.NextDouble() * 0.7f) * (k % 3 == 1 ? -0.7f : 1f); if (s0 + len < N - 10) Bump(s0, len, amp); }
	}
	private void StartRace(int id)
	{
		course = id; T0 = Th[id]; Build(id);
		Action<SolidBrush, Color> set = (b, c) => b.Color = c;
		set(grass1, T0.G1); set(grass2, T0.G2); set(road1, T0.R1); set(road2, T0.R2); set(rum1, T0.Ru1); set(rum2, T0.Ru2); set(lane, T0.Ln);
		cars.Clear(); string[] nm = { "팀장님", "민원인", "주무관", "과장님", "신규 직원" }; Color[] cc = { Color.FromArgb(230, 70, 70), Color.FromArgb(70, 110, 230), Color.FromArgb(240, 190, 40), Color.FromArgb(150, 80, 210), Color.FromArgb(250, 130, 60) };
		float[] sk = { 0.97f, 0.93f, 0.95f, 0.99f, 0.9f }; float dif = 0.97f + 0.015f * T0.Stars; for (int k = 0; k < 5; k++) cars.Add(new Car { Skill = sk[k] * dif, Z = PZ + 600f + k * 450f, X = (k % 2 == 0 ? -0.45f : 0.45f), V = 0f, C = cc[k], Name = nm[k], Tgt = (k % 2 == 0 ? -0.45f : 0.45f) });
		bans.Clear(); pos = 0f; px = 0f; v = 0f; el = 0f; skyOff = 0f; drift = 0f; charge = 0f; boostT = 0f; spinT = 0f; msgT = 0f; bumpT = 0f; finT = 0f; hitT = 0f; lap = 0; item = -1; place = 6; finished = false; wasDrift = false; Score = 0; Over = false;
		airH = 0f; airV = 0f; airT = 0f; padCd = 0f; latV = 0f; camPx = 0f; scOn = false; landT = 0f; wallHitT = 0f; steerRaw = 0f; brakeOn = false; bubT = 0f; getT = 0f; banSlip = false; pops.Clear(); lastSk = -1; dHop = 0f; gauge = 0f; bst = 0; driftVis = 0f; rollT = 0f; giantT = 0f; gS = 1f; gV = 0f; splashT = 0f; warnT = 0f; parts.Clear(); shots.Clear(); spls.Clear(); for (int q = 0; q < N; q++) skT[q] = -999f; wallCd = 0f; scHintLap = -1; cphase = 1; msg = T0.Name + " · 출발!"; msgT = 1.8f; Sfx.Play("ok");
	}
	private int SegOf(float z) { int s = (int)(Wrap(z) / SEG); return s < 0 ? 0 : (s >= N ? N - 1 : s); }
	private static float Wrap(float z) { z %= LEN; if (z < 0) z += LEN; return z; }
	private float PlayerZ { get { return pos + PZ; } }
	private float Dist(Car c) { return c.Lap * LEN + c.Z; }
	private float MyDist { get { return lap * LEN + (scOn ? (scQ[0] + (scQ[1] - scQ[0]) * ScProg) * SEG : Wrap(PlayerZ)); } }
	public override void Click(float x, float y)
	{
		if (cphase == 0) { int k = CardAt(x, y); if (k >= 0) StartRace(k); return; }
		if (new RectangleF(316, 486, 64, 64).Contains(x, y)) UseItem();
		else if (new RectangleF(316, 432, 64, 34).Contains(x, y)) UseBoost();
	}
	private RectangleF CardR(int k) { return new RectangleF(12 + (k % 3) * 128f, 90 + (k / 3) * 126f, 120f, 120f); }
	private int CardAt(float x, float y) { for (int k = 0; k < Th.Length; k++) if (CardR(k).Contains(x, y)) return k; return -1; }
	public override void Press(Keys k) { if (cphase == 0) { if (k >= Keys.D1 && k <= Keys.D9) StartRace(k - Keys.D1); return; } if (k == Keys.X) UseBoost(); else if (k == Keys.Z || k == Keys.ControlKey || k == Keys.Enter) { if (item >= 0) UseItem(); else UseBoost(); } }
	private void UseBoost()
	{
		if (bst <= 0 || finished) return; bst--; boostT = Math.Max(boostT, 1.5f); Sfx.Play("whoosh"); msg = "드리프트 부스터!"; msgT = 0.9f;
		for (int q = 0; q < 10; q++) Part(1, 200f + (float)(Rnd.NextDouble() - 0.5) * 80f, 560f, (float)(Rnd.NextDouble() - 0.5) * 160f, 80f + (float)Rnd.NextDouble() * 120f, 0.4f, 4f, Color.FromArgb(255, 200, 80));
	}
	private void UseItem()
	{
		if (item < 0 || finished || itemRoll > 0f) return; int it = item; item = -1;
		if (it == 0) { boostT = 1.6f; Sfx.Play("whoosh"); msg = "부스터!"; msgT = 1f; }
		else if (it == 1) { bans.Add(new Ban { Z = Wrap(PlayerZ - SEG * 3f), X = px }); Sfx.Play("tick"); msg = "바나나를 놓았어요"; msgT = 1f; }
		else if (it == 3) { giantT = 5f; gV = 6f; AddPop("ms_kfx_11", 200f, 500f, 230f, 0.45f); Sfx.Play("fanfare"); msg = "거대화!"; msgT = 1f; }
		else
		{
			// 물풍선: 바로 앞 차를 향해 포물선으로 날아가요
			Car best = null; float bd = 1e9f; float me = MyDist; foreach (Car c in cars) { float d = Dist(c) - me; if (d > 0f && d < bd) { bd = d; best = c; } }
			if (best != null && bd < SEG * 160f) { shots.Add(new Shot { Z0 = Wrap(PlayerZ), X0 = px, Z = Wrap(PlayerZ), X = px, Tgt = best, T = 0f, Dur = 0.35f + bd / (SEG * 160f) * 0.5f }); Sfx.Play("swing"); msg = "물풍선 발사!"; }
			else { shots.Add(new Shot { Z0 = Wrap(PlayerZ), X0 = px, Z = Wrap(PlayerZ), X = px, Tgt = null, T = 0f, Dur = 0.6f }); msg = "앞에 아무도 없어요"; }
			msgT = 1f;
		}
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (cphase == 0) { hovCard = CardAt(inp.Mx, inp.My); return; }
		if (dt > 0.05f) dt = 0.05f; el += dt; if (msgT > 0f) msgT -= dt; if (bumpT > 0f) bumpT -= dt; if (hitT > 0f) hitT -= dt; if (itemRoll > 0f) { itemRoll -= dt; if (itemRoll <= 0f) Sfx.Play("ok2"); }
		if (finished) { finT += dt; v *= (float)Math.Pow(0.4, dt); pos = Wrap(pos + v * dt); AiStep(dt); driftDir = 0; driftVis *= 0.9f; if (spinT > 0f) spinT -= dt; Fx(dt); if (finT > 3f) Over = true; return; }
		if (wallCd > 0f) wallCd -= dt; if (dHop > 0f) dHop -= dt;
		float sp = v / MAXV; bool left = inp.L, right = inp.R;
		if (inp.MouseMoved || mouseSteer) { mouseSteer = !(left || right); }
		float steer = (left ? -1f : 0f) + (right ? 1f : 0f);
		if (mouseSteer && steer == 0f) { float want = Clamp((inp.Mx - 200f) / 140f, -1f, 1f); steer = Math.Abs(want) < 0.08f ? 0f : want; }
		bool dHeld = inp.Down != null && (inp.Down.Contains(Keys.Space) || inp.Down.Contains(Keys.ShiftKey) || inp.Down.Contains(Keys.LShiftKey)) || (inp.Hold && mouseSteer && inp.Down != null && !inp.Down.Contains(Keys.Up));
		// 드리프트: 누른 채로 꺾으면 시작 → 게이지가 차요 → 놓으면 부스터
		if (dHeld && sp > 0.45f && (steer != 0f || driftDir != 0) && spinT <= 0f && airH <= 0f) { if (driftDir == 0) { driftDir = steer < 0 ? -1 : 1; dHop = 0.22f; Sfx.Play("swing"); } charge = Math.Min(2.2f, charge + dt * (0.6f + Math.Abs(steer) * 0.7f) * sp); drift = Math.Min(1f, drift + dt * 4f);
			// 드리프트하는 동안 부스터 게이지가 차요 → 가득 차면 부스터 아이템 1개 (최대 2개)
			if (bst < 2) { gauge += dt * 0.34f * sp * (0.7f + 0.5f * Math.Abs(steer)); if (gauge >= 1f) { gauge = 0f; bst++; msg = "부스터 충전! (X 키)"; msgT = 1.1f; Sfx.Play("ok2"); } } }
		else if (driftDir != 0 && airH > 0f) { }
		else
		{
			// 드리프트를 길게 했다가 놓으면 짧은 순간 부스터
			if (driftDir != 0 && charge > 0.7f) { boostT = Math.Max(boostT, 0.35f); msg = "순간 부스터!"; msgT = 0.6f; Sfx.Play("whoosh"); }
			driftDir = 0; charge = 0f; drift = Math.Max(0f, drift - dt * 5f);
		}
		driftVis += (driftDir - driftVis) * Math.Min(1f, dt * (driftDir != 0 ? 7f : 5f));
		if (spinT > 0f) { spinT -= dt; steer = 0f; }
		if (bubT > 0f) { bubT -= dt; steer = 0f; driftDir = 0; if (bubT <= 0f) { AddPop("ms_kfx_5", 200f, 490f, 190f, 0.4f); Sfx.Play("whoosh"); for (int q = 0; q < 16; q++) { double an = Rnd.NextDouble() * Math.PI * 2; Part(2, 200f, 490f, (float)Math.Cos(an) * 220f, (float)Math.Sin(an) * 220f - 120f, 0.6f, 6f, Color.FromArgb(230, 150, 215, 255)); } msg = "퐁! 빠져나왔어요"; msgT = 0.7f; } }
		steerRaw = steer; float st = driftDir != 0 ? (driftDir * 0.75f + steer * 0.55f) : steer;
		steerVis += (st - steerVis) * Math.Min(1f, dt * 10f);
		int ps = scOn ? scQ[0] : SegOf(PlayerZ); float cv = scOn ? PathCurve((int)((scCam + PZ) / SEG)) : curve[ps];
		// 빙판은 핸들이 늦게 먹고(미끄러져요) 커브 바깥으로 더 밀려요 · 공중에서는 핸들이 반만 먹어요
		float latT = st * 2.6f * Math.Max(0.25f, sp) * (driftDir != 0 ? 1.15f : 1f) * (airH > 0f ? 0.5f : 1f);
		latV += (latT - latV) * Math.Min(1f, dt * 14f * T0.Grip); px += latV * dt;
		// 커브 바깥으로 밀리는 힘: 핸들을 안 꺾으면 커브 바깥으로 밀려 나가요 (급커브는 드리프트가 필요해요)
		float cfg = sp * sp * cv * (driftDir != 0 ? 0.3f : 0.6f) * (T0.Grip < 1f ? 1.25f : 1f); cfg = Clamp(cfg, -3.0f, 3.0f); px -= dt * cfg;
		// 발판: 부스트 · 점프 · 감속 구역
		if (padCd > 0f) padCd -= dt; byte pk = scOn ? (byte)0 : padK[ps]; bool onPad = pk != 0 && Math.Abs(padX[ps] - px) < padW[ps]; bool slowZone = false;
		if (airH <= 0f && onPad)
		{
			if (pk == 1 && padCd <= 0f) { boostT = Math.Max(boostT, 1.2f); padCd = 0.5f; Sfx.Play("whoosh"); msg = course == 8 ? "결재 완료! 부스트" : "부스트 발판!"; msgT = 0.8f; }
			else if (pk == 2 && padCd <= 0f && v > MAXV * 0.3f) { airV = 430f * (0.6f + 0.4f * sp); airH = 0.01f; airT = 0f; padCd = 0.6f; Sfx.Play("swing"); float ft = 2f * airV / (1150f * T0.Grav); if (ft > 0.55f) { rollDur = ft * 0.9f; rollT = rollDur; msg = "점프! 한 바퀴~"; } else msg = "점프!"; msgT = 0.7f; }
			else if (pk == 3) slowZone = true;
		}
		if (airH > 0f) { airT += dt; airV -= 1150f * T0.Grav * dt; airH += airV * dt; if (airH <= 0f) { airH = 0f; landT = 0.16f; Shake = 0.22f; Sfx.Play("thud_soft"); if (airT > 0.7f) { boostT = Math.Max(boostT, 0.7f); msg = "착지 부스트!"; msgT = 0.8f; } } }
		// 속도
		float top = MAXV * (boostT > 0f ? 1.32f : (giantT > 0f ? 1.08f : 1f));
		// 차선 밖: 연석(살짝 느려지고 덜컹) → 그 바깥 풀밭(많이 느려짐) → 벽
		float ax = Math.Abs(px), edge = scOn ? PATHW : 1f; bool curb = !scOn && ax > 1f && ax <= 1.15f && airH <= 0f, offRoad = ax > (scOn ? PATHW + 0.05f : 1.15f) && airH <= 0f;
		if (curb) top = Math.Min(top, MAXV * (boostT > 0f ? 0.95f : 0.82f)); curbBump = curb || offRoad ? 1f : 0f;
		if (offRoad) top = Math.Min(top, MAXV * (boostT > 0f ? 0.75f : T0.OffSlow));
		if (slowZone) top = Math.Min(top, MAXV * (boostT > 0f ? 0.8f : 0.55f));
		bool brake = inp.Down != null && (inp.Down.Contains(Keys.Down) || inp.Down.Contains(Keys.S)); brakeOn = brake;
		if (bubT > 0f) v = Math.Max(MAXV * 0.3f, v - MAXV * 0.9f * dt);
		else if (spinT > 0f) v -= MAXV * 1.1f * dt;
		else if (brake) v -= MAXV * 1.2f * dt;
		else if (v < top) v += (boostT > 0f ? MAXV * 1.4f : MAXV / 3.6f * (1f - v / (top * 1.05f) * 0.6f)) * dt;
		else v -= (offRoad ? MAXV * 1.1f : MAXV * 0.5f) * dt;
		if (driftDir != 0) v -= MAXV * 0.05f * dt;
		v = Clamp(v, 0f, MAXV * 1.4f); if (boostT > 0f) boostT -= dt;
		{
			int gs = scOn ? 0 : gap[ps], sd = px < 0f ? -1 : 1; float wl = scOn ? 1.55f : WALL;
			if (gs == sd && px * sd > WALL + 0.05f && v > MAXV * 0.2f)
			{
				// 벽이 트인 곳으로 빠지면 지름길 흙길로 들어가요 (흙길 좌표로 바꿔요: 흙길 가운데가 본선보다 PATHOFF 만큼 바깥)
				foreach (int[] q in shorts) if (q[2] == sd && ps >= q[0] - 48 && ps <= q[0] + 12)
				{
					scOn = true; scQ = q; scSide = sd; scLen = q[3] + 14; scCam = 0f; scH0 = HillY(PlayerZ); scH1 = HillY(q[1] * SEG);
					px -= sd * PATHOFF; camPx -= sd * PATHOFF; msg = "지름길! 흙길을 따라 달려요"; msgT = 1.3f; Sfx.Play("whoosh"); lastSk = -1; break;
				}
			}
			else if (gs != sd && Math.Abs(px) > wl)
			{
				// 벽에 부딪히면 튕겨 나오고 느려져요
				px = sd * wl; float hitV = latV * sd; if (latV * sd > 0f) latV = -latV * 0.35f;
				if (wallCd <= 0f) { wallCd = 0.3f; v *= 0.8f; Shake = 0.3f; if (hitV > 0.3f) { wallHitT = 0.35f; wallSide = sd; } if (hitV > 0.5f) AddPop("ms_kfx_6", 200f + sd * 70f, 520f, 80f, 0.3f); Sfx.Play("thud_soft"); if (msgT <= 0f) { msg = scOn ? "쿵! 나무에 부딪혔어요" : "쿵! 벽에 부딪혔어요"; msgT = 0.8f; } }
			}
		}
		px = Clamp(px, -2.2f, 2.2f); camPx += (px * 0.72f - camPx) * Math.Min(1f, dt * 6f);   // 카메라는 길 가운데 쪽에 조금 남아 있어서, 카트가 길 위에서 좌우로 움직이는 게 보여요
		{ // 지름길 안내 (바퀴마다 한 번)
			if (!scOn) foreach (int[] q in shorts) if (scHintLap != lap * 10 + shorts.IndexOf(q) && ps >= q[0] - 110 && ps < q[0] - 46) { scHintLap = lap * 10 + shorts.IndexOf(q); msg = (q[2] > 0 ? "오른쪽" : "왼쪽") + " 벽이 트인 곳 → 지름길!"; msgT = 1.6f; }
		}
		if (scOn)
		{
			scCam += v * dt;
			if (scCam + PZ >= scLen * SEG)
			{
				// 본선 합류: 흙길 끝이 본선 바깥쪽 가장자리와 겹쳐요
				pos = Wrap(scQ[1] * SEG + (scCam + PZ - scLen * SEG) - PZ); px += scSide * JOIN; camPx += scSide * JOIN; scOn = false; msg = "합류!"; msgT = 0.7f;
			}
			Fx(dt); AiStep(dt); { float myd = MyDist; place = 1; foreach (Car c in cars) if (Dist(c) > myd) place++; } return;
		}
		float before = PlayerZ; pos = Wrap(pos + v * dt); skyOff += cv * sp * dt * 0.02f;
		if (Wrap(PlayerZ) < Wrap(before) - LEN * 0.5f) { lap++; if (lap >= Laps) { Finish(); return; } msg = lap == Laps - 1 ? "마지막 바퀴!" : (lap + 1) + "바퀴째!"; msgT = 1.6f; Sfx.Play("ok"); }
		// 아이템 상자 · 바나나
		float pz = Wrap(PlayerZ);
		foreach (Box b in boxes)
		{
			if (b.Hide > 0f) { b.Hide -= dt; continue; }
			float bz = b.Seg * SEG; if (Math.Abs(bz - pz) < SEG * 0.8f && Math.Abs(b.X - px) < 0.28f) { b.Hide = 3f; getT = 0.45f; if (item < 0) { item = Rnd.Next(4); if (place >= 4 && Rnd.NextDouble() < 0.5) item = Rnd.Next(2) == 0 ? 0 : 3; itemRoll = 0.7f; Sfx.Play("ui_open"); } }
		}
		for (int i = bans.Count - 1; i >= 0; i--) { Ban b = bans[i]; if (Math.Abs(Wrap(b.Z - pz + LEN * 0.5f) - LEN * 0.5f) < SEG * 0.7f && Math.Abs(b.X - px) < 0.22f * gS && spinT <= 0f && el > 1f && airH <= 0f) { bans.RemoveAt(i); if (giantT > 0f) { Sfx.Play("thud_soft"); msg = "바나나를 밟아 버렸어요!"; msgT = 0.8f; continue; } Spin(1.2f); banSlip = true; Sfx.Play("bad2"); msg = "바나나에 미끄러졌어요!"; msgT = 1.2f; } }
		AiStep(dt);
		// 다른 차와 부딪치기
		foreach (Car c in cars)
		{
			float dz = Wrap(c.Z - pz + LEN * 0.5f) - LEN * 0.5f;
			if (dz > -SEG * 0.6f * gS && dz < SEG * 1.4f * gS && Math.Abs(c.X - px) < 0.32f * (gS + c.GS) * 0.5f && bumpT <= 0f)
			{
				bumpT = 0.4f; bumpSide = c.X < px ? -1 : 1; AddPop("ms_kfx_6", 200f + (c.X < px ? -55f : 55f), 515f, 70f, 0.3f);
				if (giantT > 0f && c.Giant <= 0f) { HitCar(c, 1.2f); c.X += c.X < px ? -0.5f : 0.5f; Shake = 0.3f; Sfx.Play("thud"); msg = c.Name + " 꽝!"; msgT = 0.8f; }
				else if (c.Giant > 0f && giantT <= 0f) { Spin(1.1f); px += px < c.X ? -0.4f : 0.4f; Sfx.Play("bad2"); msg = "거대한 " + c.Name + "에 튕겨났어요!"; msgT = 1.2f; }
				else { v = Math.Min(v, c.V * 0.92f); px += px < c.X ? -0.18f : 0.18f; Shake = 0.25f; Sfx.Play("thud_soft"); for (int q = 0; q < 5; q++) Part(1, 200f + (c.X < px ? -50f : 50f), 520f, (float)(Rnd.NextDouble() - 0.5) * 200f, -(float)Rnd.NextDouble() * 150f, 0.3f, 3f, Color.FromArgb(255, 240, 160)); }
			}
		}
		// 스키드 자국: 드리프트 · 미끄러질 때 바퀴 자리에 남겨요
		{
			int ns = SegOf(PlayerZ); bool sk = (driftDir != 0 || spinT > 0f) && airH <= 0f && v > MAXV * 0.25f;
			if (sk) { float wob = spinT > 0f ? (float)Math.Sin(el * 20f) * 0.08f : driftVis * 0.05f; int from = lastSk >= 0 && ((ns - lastSk + N) % N) < 6 ? lastSk : ns; for (int q = from; ; q = (q + 1) % N) { skL[q] = px - 0.11f + wob; skR[q] = px + 0.11f + wob; skT[q] = el; if (q == ns) break; } lastSk = ns; } else lastSk = -1;
			// 화면 아래 바퀴 자국 (카트 바로 뒤로 이어지는 검은 줄)
			trK[trI] = 200f + Clamp((px - camPx) * 170f, -95f, 95f) - driftVis * 26f; trOn[trI] = sk; trI = (trI + 1) % trK.Length;
		}
		Fx(dt);
		// 순위
		float my = MyDist; place = 1; foreach (Car c in cars) if (Dist(c) > my) place++;
	}
	private class Pop { public string N; public float X, Y, S, T, D, R; public bool F; }
	private readonly List<Pop> pops = new List<Pop>();
	private void AddPop(string n, float x, float y, float s, float d, bool f = false) { if (pops.Count < 24) pops.Add(new Pop { N = n, X = x, Y = y, S = s, D = d, F = f, R = (float)(Rnd.NextDouble() - 0.5) * 30f }); }
	private float bubT, getT, landT, wallHitT, steerRaw; private int bumpSide, wallSide; private bool banSlip, brakeOn;
	private int lastSk = -1, trI; private readonly float[] trK = new float[14]; private readonly bool[] trOn = new bool[14]; private float dHop;
	private void Spin(float d) { if (spinT > 0f) return; banSlip = false; spinT = d; spinMax = d; AddPop("ms_kfx_7", 200f, 470f, 90f, 0.45f); Shake = 0.4f; driftDir = 0; charge = 0f; }
	private void HitCar(Car c, float d) { if (c.Giant > 0f) return; c.Spin = d; c.SpinMax = d; spls.Add(new Spl { Z = c.Z, X = c.X, T = 0f }); }
	// 연출: 물풍선 · 튄 물방울 · 연기 · 불꽃 · 거대화 스프링 · 공중 회전
	private void Fx(float dt)
	{
		float kx = 200f + Clamp((px - camPx) * 170f, -95f, 95f);
		if (rollT > 0f) rollT -= dt; if (getT > 0f) getT -= dt; if (landT > 0f) landT -= dt; if (wallHitT > 0f) wallHitT -= dt; for (int i = pops.Count - 1; i >= 0; i--) { pops[i].T += dt; if (pops[i].T >= pops[i].D) pops.RemoveAt(i); } if (splashT > 0f) splashT -= dt; if (warnT > 0f) warnT -= dt;
		if (giantT > 0f) { giantT -= dt; if (giantT <= 0f) { AddPop("ms_kfx_11", 200f, 510f, 170f, 0.4f); Sfx.Play("ui_open"); msg = "원래 크기로"; msgT = 0.7f; } }
		float gt = giantT > 0f ? 1.55f : 1f; gV += (gt - gS) * 160f * dt; gV *= (float)Math.Exp(-9f * dt); gS += gV * dt;   // 통통 튀는 스프링
		foreach (Car c in cars) { if (c.Giant > 0f) c.Giant -= dt; float ct = c.Giant > 0f ? 1.55f : 1f; c.GV += (ct - c.GS) * 160f * dt; c.GV *= (float)Math.Exp(-9f * dt); c.GS += c.GV * dt; }
		// 타이어 연기 · 불꽃
		if (driftDir != 0 && airH <= 0f)
		{
			Color sc = charge > 1.6f ? Color.FromArgb(255, 160, 50) : (charge > 0.55f ? Color.FromArgb(110, 200, 255) : Color.FromArgb(255, 250, 220));
			for (int sd = -1; sd <= 1; sd += 2) { if (Rnd.NextDouble() < 0.6) Part(0, kx + sd * 40f * gS, 560f, -driftDir * 60f + (float)(Rnd.NextDouble() - 0.5) * 40f, -30f - (float)Rnd.NextDouble() * 30f, 0.55f, 9f * gS, Color.FromArgb(235, 240, 240, 240)); if (Rnd.NextDouble() < 0.5) Part(1, kx + sd * 40f * gS, 562f, (float)(Rnd.NextDouble() - 0.5) * 220f - driftDir * 80f, -60f - (float)Rnd.NextDouble() * 140f, 0.25f, 3f, sc); }
		}
		if (curbBump > 0f && airH <= 0f && v > MAXV * 0.3f && Rnd.NextDouble() < 0.25) Part(0, kx + (px > 0 ? 44f : -44f), 562f, (float)(Rnd.NextDouble() - 0.5) * 60f, -20f, 0.5f, 7f, Color.FromArgb(200, Sh(T0.G1, -10)));
		for (int i = parts.Count - 1; i >= 0; i--)
		{
			Pt q = parts[i]; q.Life -= dt; if (q.Life <= 0f) { parts.RemoveAt(i); continue; }
			q.X += q.VX * dt; q.Y += q.VY * dt; if (q.K == 2) q.VY += 900f * dt; else if (q.K == 0) { q.VX *= 0.96f; q.Size += dt * 30f; q.Y += 40f * dt * (v / MAXV); } else q.VY += 300f * dt;
		}
		// 물풍선 날아가기 · 맞기
		for (int i = shots.Count - 1; i >= 0; i--)
		{
			Shot sh = shots[i]; sh.T += dt; float k = Math.Min(1f, sh.T / sh.Dur);
			if (sh.AtMe)
			{
				float tz = Wrap(PlayerZ); sh.Z = sh.Z0 + (Wrap(tz - sh.Z0 + LEN) % LEN) * k; sh.X = Lerp(sh.X0, sh.X0 * 0.3f + px * 0.7f, k);
				if (k >= 1f)
				{
					shots.RemoveAt(i); if (finished) continue;
					if (giantT > 0f) { msg = "거대해서 물풍선이 튕겨 나갔어요!"; msgT = 1f; Sfx.Play("thud_soft"); }
					else if (Math.Abs(sh.X - px) > 0.42f || airH > 30f) { msg = "휴~ 피했어요!"; msgT = 1f; Sfx.Play("ok"); }
					else { bubT = 1.1f; driftDir = 0; charge = 0f; splashT = 0.5f; AddPop("ms_kfx_2", kx, 500f, 170f, 0.35f); Sfx.Play("glass"); msg = (sh.From != null ? sh.From.Name + "의 " : "") + "물풍선에 갇혔어요!"; msgT = 1.3f; for (int q = 0; q < 26; q++) { double an = Rnd.NextDouble() * Math.PI * 2; float sp2 = 120f + (float)Rnd.NextDouble() * 260f; Part(2, kx, 520f, (float)Math.Cos(an) * sp2, (float)Math.Sin(an) * sp2 - 200f, 0.7f, 4f + (float)Rnd.NextDouble() * 5f, Color.FromArgb(230, 120, 200, 255)); } }
				}
				continue;
			}
			if (sh.Tgt != null) { float tz = sh.Tgt.Z; sh.Z = sh.Z0 + (Wrap(tz - sh.Z0 + LEN) % LEN) * k; sh.X = Lerp(sh.X0, sh.Tgt.X, k); }
			else { sh.Z = Wrap(sh.Z0 + SEG * 60f * k); }
			if (k >= 1f) { shots.RemoveAt(i); if (sh.Tgt != null) { if (sh.Tgt.Giant <= 0f) { sh.Tgt.Bub = 1.1f; spls.Add(new Spl { Z = sh.Tgt.Z, X = sh.Tgt.X, T = 0f }); } if (sh.From == null) { Sfx.Play("glass"); msg = sh.Tgt.Giant > 0f ? sh.Tgt.Name + "는 거대해서 안 통해요" : sh.Tgt.Name + " 명중!"; msgT = 1f; } } else spls.Add(new Spl { Z = sh.Z, X = sh.X, T = 0f }); }
		}
		for (int i = spls.Count - 1; i >= 0; i--) { spls[i].T += dt; if (spls[i].T > 0.6f) spls.RemoveAt(i); }
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
			if (c.Bub > 0f) { c.Bub -= dt; c.V = Math.Max(MAXV * 0.3f, c.V - MAXV * 1.0f * dt); if (c.Bub <= 0f) spls.Add(new Spl { Z = c.Z, X = c.X, T = 0.25f }); }
			else if (c.Spin > 0f) { c.Spin -= dt; c.V = Math.Max(MAXV * 0.25f, c.V - MAXV * 1.2f * dt); }
			else
			{
				// CPU: 차마다 실력이 달라요 · 급커브 앞에서는 드리프트로 속도를 조금만 줄여요 · 너무 벌어지면 고무줄처럼 따라와요
				float gap = Dist(c) - my; float want = MAXV * c.Skill;
				c.Drift = cvA > 2.2f ? Math.Min(1f, c.Drift + dt * 3f) : Math.Max(0f, c.Drift - dt * 3f);
				if (c.Drift > 0.5f) { int cs2 = SegOf(c.Z); if (el - skT[cs2] > 0.5f) { float sd2 = cv > 0 ? -0.04f : 0.04f; skL[cs2] = c.X - 0.1f + sd2; skR[cs2] = c.X + 0.1f + sd2; skT[cs2] = el; } }
				want *= 1f - Math.Min(0.3f, cvA * 0.06f) * (1f - 0.45f * c.Drift);
				if (gap > SEG * 120f) want *= 0.88f; else if (gap < -SEG * 80f) want *= 1.07f;
				if (c.Boost > 0f) { c.Boost -= dt; want = MAXV * 1.25f; }
				c.V += (want - c.V) * Math.Min(1f, dt * (c.Boost > 0f ? 2.5f : 0.9f));
				// 아이템 쓰기
				if (c.Item >= 0) { c.ItemT -= dt; if (c.ItemT <= 0f) AiItem(c); }
			}
			{ byte ck = padK[sg]; if (ck != 0 && Math.Abs(padX[sg] - c.X) < padW[sg]) { if (ck == 1) c.Boost = Math.Max(c.Boost, 1.0f); else if (ck == 3) c.V = Math.Min(c.V, MAXV * 0.62f); } }
			c.TgtT -= dt; if (c.TgtT <= 0f) { c.TgtT = 1.5f + (float)Rnd.NextDouble() * 2f; c.Tgt = (float)(Rnd.NextDouble() * 1.3 - 0.65); }
			c.X += (c.Tgt - cv * 0.06f - c.X) * Math.Min(1f, dt * 1.2f);
			float nz = c.Z + c.V * dt; if (nz >= LEN) { nz -= LEN; c.Lap++; } c.Z = nz;
			foreach (Box bx in boxes) if (bx.Hide <= 0f && Math.Abs(bx.Seg * SEG - c.Z) < SEG * 0.8f && Math.Abs(bx.X - c.X) < 0.3f) { bx.Hide = 3f; if (c.Item < 0) { c.Item = Rnd.Next(4); c.ItemT = 1.5f + (float)Rnd.NextDouble() * 4f; } }
			for (int i = bans.Count - 1; i >= 0; i--) { Ban b = bans[i]; if (Math.Abs(b.Z - c.Z) < SEG * 0.7f && Math.Abs(b.X - c.X) < 0.25f) { if (c.Giant <= 0f) { c.Spin = 1.1f; c.SpinMax = 1.1f; } bans.RemoveAt(i); } }
		}
	}
	private void AiItem(Car c)
	{
		int it = c.Item; c.Item = -1; float me = MyDist, cd = Dist(c);
		if (it == 0) c.Boost = 1.3f;
		else if (it == 3) c.Giant = 5f;
		else if (it == 1) bans.Add(new Ban { Z = Wrap(c.Z - SEG * 3f), X = c.X });
		else
		{
			// 바로 앞 차를 노려요 (그게 마룡이면 마룡이가 맞아요)
			Car best = null; float bd = 1e9f; foreach (Car o in cars) { if (o == c) continue; float d = Dist(o) - cd; if (d > 0f && d < bd) { bd = d; best = o; } }
			float dm = me - cd;
			if (dm > 0f && dm < bd && dm < SEG * 160f && !finished) { shots.Add(new Shot { AtMe = true, From = c, Z0 = c.Z, X0 = c.X * 0.4f + px * 0.6f, Z = c.Z, X = c.X, Dur = 1.1f }); warnT = 1.1f; msg = "뒤에서 물풍선이 날아와요! 옆으로 피해요"; msgT = 1.1f; Sfx.Play("tick"); }
			else if (best != null && bd < SEG * 160f) shots.Add(new Shot { From = c, Tgt = best, Z0 = c.Z, X0 = c.X, Z = c.Z, X = c.X, Dur = 0.5f });
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
		y2 -= 1f; g.FillPolygon(b, new PointF[] { new PointF(x1 - w1, y1), new PointF(x2 - w2, y2), new PointF(x2 + w2, y2), new PointF(x1 + w1, y1) });
	}
	private readonly SolidBrush grass1 = new SolidBrush(Color.FromArgb(96, 182, 92)), grass2 = new SolidBrush(Color.FromArgb(86, 168, 84)), road1 = new SolidBrush(Color.FromArgb(110, 112, 122)), road2 = new SolidBrush(Color.FromArgb(104, 106, 116)), rum1 = new SolidBrush(Color.FromArgb(230, 70, 70)), rum2 = new SolidBrush(Color.FromArgb(245, 245, 245)), lane = new SolidBrush(Color.FromArgb(235, 235, 220)), chk = new SolidBrush(Color.FromArgb(30, 30, 40));
	private class Sp { public float X, Y, Sc, Clip; public int K; public object O; }
	private struct Row { public Pj A, B; public int I; }
	private readonly List<Row> rows = new List<Row>(96);
	private readonly List<Sp> sps = new List<Sp>();
	public override void Draw(Graphics g, double t)
	{
		if (cphase == 0) { DrawSelect(g, t); return; }
		// 하늘 · 먼 산 (코스 테마 색)
		Bg(g, T0.SkyA, T0.SkyB);
		if (T0.Night) { using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) for (int k = 0; k < 40; k++) { float sx0 = (k * 97 + 13) % 400 - (skyOff * 60f) % 400f; if (sx0 < 0) sx0 += 400; float sy0 = 40 + (k * 53) % 180; float tw = 1f + (float)Math.Sin(t * 2 + k) * 0.6f; g.FillEllipse(b, sx0, sy0, tw * 1.6f, tw * 1.6f); } using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 160, 220))) g.FillEllipse(b, 60, 90, 70, 70); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, T0.Sun))) g.FillEllipse(b, 300, 120, 44, 44);
		Hills(g, 0.25f, 230f, 60f, T0.HillF, 7);
		Hills(g, 0.5f, 268f, 46f, T0.HillN, 5);
		SmoothingMode sm0 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;
		// 지름길 흙길 위에서는 '흙길 칸들 → 합류 뒤 본선 칸들'을 이어 붙인 가상의 길을 그려요 (I < 0 = 흙길 칸)
		bool vm = scOn; int vb = vm ? (int)(scCam / SEG) : 0;
		int bs = vm ? 0 : SegOf(pos); float bp = vm ? (scCam % SEG) / SEG : (Wrap(pos) % SEG) / SEG;
		float camY = CAMH + (vm ? Lerp(PathHillAt((int)((scCam + PZ) / SEG)), PathHillAt((int)((scCam + PZ) / SEG) + 1), ((scCam + PZ) % SEG) / SEG) : HillY(PlayerZ)) + airH * 4f;
		Func<int, float> vcurve = vv => vv < scLen ? PathCurve(vv) : curve[(scQ[1] + vv - scLen) % N];
		float x = 0f, dx = -(vm ? vcurve(vb) : curve[bs]) * bp, maxY = H; sps.Clear();
		Pj[] p1s = new Pj[DRAW]; float[] clip = new float[DRAW]; int[] segOf = new int[DRAW];
		for (int n = 0; n < DRAW; n++)
		{
			int i; float z, camZ, y1, y2, off = 0f, cvn;
			if (vm)
			{
				int vv = vb + n; z = vv * SEG; camZ = scCam; y1 = PathHillAt(vv); y2 = PathHillAt(vv + 1); cvn = vcurve(vv);
				if (vv < scLen) i = -1 - vv; else { i = (scQ[1] + vv - scLen) % N; off = -scSide * JOIN * ROADW; }
			}
			else { i = (bs + n) % N; bool looped = i < bs; camZ = Wrap(pos) - (looped ? LEN : 0f); z = i * SEG; y1 = HillY(i * SEG); y2 = HillY((i + 1) * SEG); cvn = curve[i]; }
			Pj a = Proj(off, y1, z, camPx * ROADW - x, camY, camZ), b = Proj(off, y2, z + SEG, camPx * ROADW - x - dx, camY, camZ);
			x += dx; dx += cvn; p1s[n] = a; clip[n] = maxY; segOf[n] = i;
			if (z - camZ <= DEPTH || b.Y >= maxY || b.Y >= a.Y - 0.01f) continue;
			rows.Add(new Row { A = a, B = b, I = i });
			maxY = b.Y;
		}
		// 먼 칸부터 가까운 칸 순서로 그려요. 칸 경계는 정수 픽셀에 맞추고 길은 위로 1픽셀 겹쳐서, 칸 사이로 잔디색 줄이 비치지 않게 해요
		for (int r = rows.Count - 1; r >= 0; r--)
		{
			Pj a = rows[r].A, b = rows[r].B; int i = rows[r].I; bool alt = ((i < 0 ? -1 - i : i) / 3) % 2 == 0;
			float yb = (float)Math.Round(b.Y), ya = (float)Math.Round(a.Y); if (r == 0) ya = Math.Max(ya, a.Y);
			if (ya <= yb) continue;
			a.Y = ya; b.Y = yb;
			g.FillRectangle(alt ? grass1 : grass2, 0, yb, W, ya - yb);
			if (i < 0) { PathRow(g, a, b, -1 - i, alt); continue; }
			if (course != 0 && i >= 3) { RoadStyle(g, a, b, i, alt, t); if (padK[i] != 0) PadRow(g, a, b, i, t); SkidRow(g, a, b, i); WallRow(g, a, b, i, alt); continue; }
			Quad(g, alt ? rum1 : rum2, a.X, a.Y, a.W * 1.12f, b.X, b.Y, b.W * 1.12f);
			Quad(g, (i < 3) ? chk : (alt ? road1 : road2), a.X, a.Y, a.W, b.X, b.Y, b.W);
			if (i < 3) { for (int k = 0; k < 8; k++) if ((k + i) % 2 == 0) { float u0 = -1f + k * 0.25f; Quad(g, rum2, a.X + a.W * (u0 + 0.125f), a.Y, a.W * 0.125f, b.X + b.W * (u0 + 0.125f), b.Y, b.W * 0.125f); } }
			else if (alt) { Quad(g, lane, a.X - a.W * 0.33f, a.Y, a.W * 0.02f, b.X - b.W * 0.33f, b.Y, b.W * 0.02f); Quad(g, lane, a.X + a.W * 0.33f, a.Y, a.W * 0.02f, b.X + b.W * 0.33f, b.Y, b.W * 0.02f); }
			if (padK[i] != 0) PadRow(g, a, b, i, t);
			SkidRow(g, a, b, i);
			WallRow(g, a, b, i, alt);
		}
		// 도로 가장자리를 부드러운 선으로 한 번 더 그어 계단 모양을 감춰요 (이어진 줄끼리만)
		if (rows.Count > 2)
		{
			g.SmoothingMode = SmoothingMode.AntiAlias; Color ec = course == 0 ? Color.FromArgb(150, T0.Ru2) : Color.FromArgb(150, Sh(T0.G1, -18));
			using (Pen ep = new Pen(ec, 1.3f))
			{
				List<PointF> L = new List<PointF>(), Rr = new List<PointF>(); float lastY = -1f;
				Action flush = () => { if (L.Count > 1) { g.DrawLines(ep, L.ToArray()); g.DrawLines(ep, Rr.ToArray()); } L.Clear(); Rr.Clear(); };
				for (int r = rows.Count - 1; r >= 0; r--) { Pj a = rows[r].A, b = rows[r].B; if (rows[r].I < 0) { flush(); continue; } if (L.Count > 0 && Math.Abs(b.Y - lastY) > 2.5f) flush(); float ew = course == 0 ? 1.12f : 1.13f; if (L.Count == 0) { L.Add(new PointF(b.X - b.W * ew, b.Y)); Rr.Add(new PointF(b.X + b.W * ew, b.Y)); } L.Add(new PointF(a.X - a.W * ew, a.Y)); Rr.Add(new PointF(a.X + a.W * ew, a.Y)); lastY = a.Y; }
				flush();
			}
		}
		rows.Clear();
		g.SmoothingMode = sm0;
		// 길가 장식 · 아이템 상자 · 바나나 · 다른 차 (먼 것부터)
		for (int n = DRAW - 1; n > 2; n--)
		{
			int i = segOf[n]; Pj a = p1s[n]; if (a.Sc <= 0f || a.Y <= 0f) continue; float cl = clip[n];
			if (i < 0) { int vv = -1 - i; if (vv % 4 == 0) { float sd2 = (vv / 4) % 2 == 0 ? -1f : 1f; Deco(g, a, sd2 * 1.75f, 0, cl, vv * 5); } if (vv % 4 == 2) Deco(g, a, ((vv / 4) % 2 == 0 ? 1f : -1f) * 2.3f, 0, cl, vv * 5 + 5); continue; }
			if (i % 5 == 0)
			{
				int r = (i * 7) % 11; float side = ((i / 5) % 2 == 0) ? -1f : 1f; bool bend = Math.Abs(curve[i]) > 1.2f;
				int kind = bend ? 3 : (i % 60 == 0 ? 1 : (r < 4 ? 2 : (r < 8 ? 0 : 4)));
				if (bend) side = curve[i] > 0f ? -1f : 1f;   // 급커브 바깥쪽에 울타리
				if (gap[i] == (int)side || gap[(i + 6) % N] == (int)side) { } else   // 지름길 입구는 비워 둬요
				Deco(g, a, side * (kind == 2 ? 2.6f : (kind == 3 ? 1.85f : 2.05f)), kind, cl, i);
			}
			foreach (Box bx in boxes) if (bx.Seg == i && bx.Hide <= 0f) ItemBox(g, a, bx.X, cl, t);
			foreach (Ban bn in bans) if (SegOf(bn.Z) == i) Banana(g, a, bn.X, cl);
			foreach (Car c in cars) if (SegOf(c.Z) == i)
			{
				float sx = a.X + a.Sc * c.X * ROADW * W / 2f, sz = a.Sc * 900f * W / 2f * c.GS; if (a.Y - sz * 0.8f >= cl) continue;
				float yaw = 0f, hp = 0f; if (c.Spin > 0f) { float pr = 1f - c.Spin / c.SpinMax; yaw = (float)(Math.PI * 4 * (1 - (1 - pr) * (1 - pr))); hp = (float)Math.Sin(Math.Min(1f, pr * 2.5f) * Math.PI) * sz * 0.25f; }
				float cs = (float)Math.Cos(yaw); DrawKart(g, sx, a.Y, Math.Min(240f, sz), c.C, false, c.Spin > 0f ? (float)Math.Sin(yaw * 2) * 6f : 0f, 0f, "ms_kart_cpu" + cars.IndexOf(c), Math.Sin(yaw) > 0, Math.Max(0.15f, Math.Abs(cs)), hp);
				if (c.Spin > c.SpinMax * 0.3f && sz > 30f) Dizzy(g, sx, a.Y - sz * 1.15f - hp, sz * 0.4f, t);
				if (c.Bub > 0f && sz > 6f) { float bs2 = Math.Min(300f, sz * 1.35f), fl = (float)Math.Sin(t * 6) * sz * 0.05f; Spr.Draw(g, "ms_kfx_4", sx, a.Y - sz * 0.5f + fl, bs2, bs2, 0f); }
			}
			foreach (Shot sh in shots) if (!sh.AtMe && SegOf(sh.Z) == i) { float k2 = Math.Min(1f, sh.T / sh.Dur), arc = (float)Math.Sin(k2 * Math.PI) * 700f + 250f; float sx = a.X + a.Sc * sh.X * ROADW * W / 2f, sy = a.Y - a.Sc * arc * W / 2f, r2 = Math.Max(3f, a.Sc * 220f * W / 2f); if (sy < cl) Balloon(g, sx, sy, r2, t); }
			foreach (Spl sp2 in spls) if (SegOf(sp2.Z) == i) { float sx = a.X + a.Sc * sp2.X * ROADW * W / 2f, r2 = a.Sc * 900f * W / 2f * (0.6f + sp2.T * 1.2f); if (r2 > 4f && a.Y - r2 < cl) Spr.Draw(g, sp2.T < 0.3f ? "ms_kfx_2" : "ms_kfx_3", sx, a.Y - r2 * 0.45f, r2, r2 * 0.9f, 0f); }
		}
		// 내 카트
		{ // 카트 뒤 화면 아래로 이어지는 바퀴 자국
			for (int q = 1; q < trK.Length; q++)
			{
				int i0 = (trI - q + trK.Length) % trK.Length, i1 = (trI - q - 1 + trK.Length * 2) % trK.Length; if (!trOn[i0] || !trOn[i1]) continue;
				float y0 = 552f + (q - 1) * 4f, y1 = y0 + 4f; int al = 150 - q * 9; if (al < 10) continue; skB.Color = Color.FromArgb(al, 28, 24, 28);
				for (int sd = -1; sd <= 1; sd += 2) g.FillPolygon(skB, new PointF[] { new PointF(trK[i0] + sd * 40f - 5f, y0), new PointF(trK[i0] + sd * 40f + 5f, y0), new PointF(trK[i1] + sd * 41f + 5.5f, y1), new PointF(trK[i1] + sd * 41f - 5.5f, y1) });
			}
		}
		DrawParts(g, 0);
		float bob = (float)Math.Sin(t * 30) * (v > 100f ? 1.2f : 0f) + (curbBump > 0f && airH <= 0f ? (float)Math.Sin(t * 50) * 2f : 0f);
		float kx = 200f + Clamp((px - camPx) * 170f, -95f, 95f) - driftVis * 26f;   // 카메라가 살짝 늦게 따라오고, 드리프트하면 뒤가 바깥으로 미끄러져요
		float yawM = 0f, hopM = 0f; if (spinT > 0f) { float pr = 1f - spinT / spinMax; yawM = (float)(Math.PI * 4 * (1 - (1 - pr) * (1 - pr))); hopM = (float)Math.Sin(Math.Min(1f, pr * 2.5f) * Math.PI) * 26f; }
		if (dHop > 0f) hopM += (float)Math.Sin(Math.PI * (1f - dHop / 0.22f)) * 12f;   // 드리프트 시작할 때 살짝 뛰어올라요
		float rollM = rollT > 0f ? 360f * (float)(0.5 - 0.5 * Math.Cos(Math.PI * (1f - rollT / rollDur))) : 0f;
		float gsv = Math.Max(0.6f, gS); if (giantT > 0f && giantT < 1f && ((int)(t * 12)) % 2 == 0) gsv = 1f + (gsv - 1f) * 0.6f;
		if (driftDir != 0 && airH <= 0f) { Sparks(g, kx - driftVis * 36f * gsv, 548f, charge, t); if (charge > 0.55f) { float fl = 0.8f + 0.25f * (float)Math.Sin(t * 40); Spr.Draw(g, "ms_kfx_9", kx - driftDir * 52f * gsv, 548f, -driftDir * 70f * fl, 46f * fl, 0f); } }
		if (boostT > 0f) { float fl = 0.85f + 0.2f * (float)Math.Sin(t * 45); for (int sd = -1; sd <= 1; sd += 2) Spr.Draw(g, "ms_kfx_10", kx + sd * 22f * gsv, 578f - airH - hopM, 74f * fl * gsv, 40f * gsv, -90f); }
		if (airH > 0f) bob -= airH;
		{
			// 상황마다 그림을 골라요: 물방울 · 바나나 · 제자리 회전(뒤→옆→앞→옆) · 어지러움 · 우승 · 공중 · 드리프트 · 부스터 · 핸들
			string ks = Math.Abs(steerVis) > 0.6f ? "ms_kart_me_s2" : (Math.Abs(steerVis) > 0.2f ? "ms_kart_me_s1" : "ms_kart_me0"); bool kf = steerVis > 0.2f; float sxs = 1f, shear = 0f;
			if (el < 1.6f && v < MAXV * 0.25f && !finished) { ks = "ms_kart_me_rev"; kf = false; hopM += Math.Abs((float)Math.Sin(t * 30)) * 2f; }   // 출발 직전 부릉부릉
			else if (brakeOn && v > MAXV * 0.2f && driftDir == 0) { ks = "ms_kart_me_brk"; kf = steerVis > 0.2f; }
			if (landT > 0f && airH <= 0f) { ks = "ms_kart_me_land"; kf = false; }
			float rot = rollT > 0f ? rollM : steerVis * 3f + driftVis * 6f + (driftDir != 0 ? (float)Math.Sin(t * 26) * 0.8f : 0f);
			if (bubT > 0f) { ks = "ms_kart_me_bub"; kf = false; rot = (float)Math.Sin(t * 5) * 6f; hopM += 22f + (float)Math.Sin(t * 6) * 6f; shear = 0f; }
			else if (spinT > 0f)
			{
				float pr = 1f - spinT / spinMax; shear = 0f; rot = (float)Math.Sin(yawM * 2) * 5f;
				if (banSlip && pr < 0.28f) { ks = "ms_kart_me_ban"; kf = false; }
				else if (pr > 0.72f) { ks = "ms_kart_me_dz"; kf = false; rot = (float)Math.Sin(t * 9) * 4f; }
				else { int qd = ((int)Math.Round(yawM / (Math.PI / 2))) % 4; ks = qd == 0 ? "ms_kart_me0" : (qd == 2 ? "ms_kart_me_f" : "ms_kart_me_side"); kf = qd == 3; sxs = 0.82f + 0.18f * (float)Math.Abs(Math.Cos(yawM * 2)); }
			}
			else if (finished && place == 1) { ks = "ms_kart_me_win"; kf = false; }
			else if (airH > 0f || rollT > 0f) { ks = "ms_kart_me_air"; kf = false; }
			else if (wallHitT > 0f) { ks = "ms_kart_me_wall"; kf = wallSide < 0; }   // 그림의 벽은 오른쪽에 있어요
			else if (bumpT > 0.15f) { ks = "ms_kart_me_bump"; kf = bumpSide < 0; }
			else if (driftDir != 0)
			{
				// 드리프트 각도: 시작 점프 → 15° → 30° → 45° (안쪽으로 더 꺾을수록), 반대로 꺾으면 카운터 자세
				float into = steerRaw * driftDir, dv = Math.Abs(driftVis);
				if (dHop > 0.05f) ks = "ms_kart_me_dh";
				else if (into < -0.3f) ks = "ms_kart_me_dc";
				else if (dv < 0.45f) ks = "ms_kart_me_d15";
				else if (into > 0.3f && charge > 0.5f) ks = "ms_kart_me_d45";
				else ks = "ms_kart_me_d30";
				kf = driftDir > 0; shear = -driftVis * 0.04f;
			}
			else if (boostT > 0f && Math.Abs(steerVis) < 0.35f) { ks = "ms_kart_me_bst"; kf = false; }
			if (Spr.Get(ks) == null) { ks = "ms_kart_me0"; kf = false; }
			float ky = 556f + bob, rcy = rollT > 0f ? 60f * gsv : 0f;
			if (rollT > 0f) { GraphicsState rs = g.Save(); g.TranslateTransform(kx, ky - rcy); g.RotateTransform(rollM); DrawKart(g, 0f, rcy, 120f * gsv, Color.FromArgb(30, 160, 140), true, 0f, steerVis, ks, kf); g.Restore(rs); }
			else DrawKart(g, kx, ky, 120f * gsv, Color.FromArgb(30, 160, 140), true, rot, steerVis, ks, kf, sxs, hopM, shear);
			if (spinT > spinMax * 0.25f && ks != "ms_kart_me_dz") Dizzy(g, kx, ky - 150f * gsv - hopM, 40f, t);
			if (giantT > 0f) using (Pen gp = new Pen(Color.FromArgb((int)(90 + 60 * Math.Sin(t * 10)), 255, 230, 120), 3f)) g.DrawEllipse(gp, kx - 70f * gsv, ky - 150f * gsv, 140f * gsv, 160f * gsv);
		}
		DrawParts(g, 1);
		foreach (Pop pp in pops) { float k = pp.T / pp.D, sc = pp.S * (0.6f + 0.6f * (float)Math.Sin(Math.Min(1f, k * 1.6f) * Math.PI / 2)) * (k > 0.75f ? 1f - (k - 0.75f) * 2f : 1f); Spr.Draw(g, pp.N, pp.X, pp.Y, pp.F ? -sc : sc, sc, pp.R); }
		if (splashT > 0f) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(110 * splashT / 0.7f), 120, 190, 255))) g.FillRectangle(b, 0, 0, W, H);
		if (warnT > 0f && ((int)(t * 8)) % 2 == 0) { using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 240, 70, 70))) g.FillEllipse(b, 182, 590 - 14, 36, 36); Txt(g, "!", 16f, 2, Color.White, 182, 576, 36, 36, StringAlignment.Center); Balloon(g, 200f + (1f - warnT / 1.1f) * 0f, 600f - (1f - warnT / 1.1f) * 40f, 10f, t); }
		// HUD
		Panel(g, new RectangleF(10, 40, 120, 56)); Txt(g, place + "등", 22f, 2, place == 1 ? Color.FromArgb(255, 220, 90) : Color.White, 10, 40, 120, 56, StringAlignment.Center);
		Panel(g, new RectangleF(140, 40, 250, 56)); Txt(g, Math.Min(Laps, lap + 1) + " / " + Laps + " 바퀴   " + ((int)(v / MAXV * 180)) + "km/h", 11f, 2, Color.White, 146, 42, 240, 24, StringAlignment.Center);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(152, 70, 226, 12), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillPath(b, gp);
		float cf = bst >= 2 ? 1f : Math.Min(1f, gauge); if (cf > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(152, 70, 226 * cf, 12), 6)) using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(150, 70, 230, 12), Color.FromArgb(80, 190, 255), Color.FromArgb(255, 150, 40), 0f)) g.FillPath(b, gp);
		if (driftDir != 0 && bst < 2) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 + 100 * Math.Sin(t * 20)), 255, 255, 255))) g.FillRectangle(b, 152 + 226 * cf - 3, 70, 3, 12);
		Txt(g, bst >= 2 ? "부스터 가득 · X 키로 써요" : "드리프트하면 부스터 게이지가 차요", 7.5f, 1, Color.FromArgb(220, 255, 255, 255), 152, 82, 226, 12, StringAlignment.Center);
		// 드리프트 부스터 칸 (2개까지)
		for (int q = 0; q < 2; q++) { RectangleF br = new RectangleF(316 + q * 34, 432, 30, 34); Panel(g, br); if (q < bst) BoostIcon(g, br.X + 15, br.Y + 17, 1f + (q == bst - 1 ? (float)Math.Sin(t * 6) * 0.06f : 0f)); }
		Txt(g, "X 부스터", 7f, 1, Color.FromArgb(220, 255, 255, 255), 316, 467, 64, 14, StringAlignment.Center);
		RectangleF ib = new RectangleF(316, 486, 64, 64); Panel(g, ib);
		if (getT > 0f) { float gk = 1f - getT / 0.45f; Spr.Draw(g, "ms_kt_get", ib.X + 32, ib.Y + 30, 60f + 50f * gk, 60f + 50f * gk, gk * 40f); }
		if (item >= 0) { int shown = itemRoll > 0f ? ((int)(t * 14)) % 4 : item; ItemIcon(g, ib.X + 32, ib.Y + 28, shown); Txt(g, itemRoll > 0f ? "" : ItemName[item], 8f, 2, Color.White, ib.X, ib.Y + 44, ib.Width, 18, StringAlignment.Center); }
		else Txt(g, "아이템", 8.5f, 1, Color.FromArgb(150, 255, 255, 255), ib.X, ib.Y, ib.Width, ib.Height, StringAlignment.Center);
		// 순위표 (작게)
		List<KeyValuePair<string, float>> rk = new List<KeyValuePair<string, float>>(); rk.Add(new KeyValuePair<string, float>("마룡이", MyDist)); foreach (Car c in cars) rk.Add(new KeyValuePair<string, float>(c.Name, Dist(c)));
		rk.Sort((a, b) => b.Value.CompareTo(a.Value)); Panel(g, new RectangleF(10, 104, 92, 6 * 15 + 8));
		for (int i = 0; i < rk.Count; i++) Txt(g, (i + 1) + " " + rk[i].Key, 8f, rk[i].Key == "마룡이" ? 2 : 1, rk[i].Key == "마룡이" ? Color.FromArgb(255, 230, 120) : Color.FromArgb(225, 255, 255, 255), 16, 108 + i * 15, 86, 15, StringAlignment.Near);
		{ RectangleF mr = new RectangleF(10, 470, 112, 104); Panel(g, mr); MiniMap(g, mr); }
		if (msgT > 0f) Hint(g, msg, 170, 14f, true, Math.Min(1f, msgT * 2f));
		if (el < 5f) Hint(g, course == 2 ? "빙판이라 미끄러워요! 핸들을 미리 꺾어요" : (course == 4 ? "중력이 약해요! 점프하면 오래 날아요" : "←→ 핸들 · Shift 드리프트로 부스터 모으기 · X 부스터 · Z 아이템"), 432, 10.5f, false, Math.Min(1f, (5f - el) * 2f));
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
		if (course != 0) { DecoTheme(g, a, side, kind, clip, seg); return; }
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
	// ── 테마별 길바닥: 마을만 아스팔트+차선, 나머지는 테마에 맞는 바닥 ──
	private readonly SolidBrush sbr = new SolidBrush(Color.White);
	private void Span(Graphics g, Color c, Pj a, Pj b, float u0, float u1) { sbr.Color = c; float m = (u0 + u1) * 0.5f, h = (u1 - u0) * 0.5f; Quad(g, sbr, a.X + a.W * m, a.Y, a.W * h, b.X + b.W * m, b.Y, b.W * h); }
	private static Color Sh(Color c, int d) { return Color.FromArgb(c.A, Math.Max(0, Math.Min(255, c.R + d)), Math.Max(0, Math.Min(255, c.G + d)), Math.Max(0, Math.Min(255, c.B + d))); }
	private static float Hash(int i, int k) { unchecked { int h = i * 73856093 ^ k * 19349663; h = (h ^ (h >> 13)) * 1274126177; return ((h >> 8) & 0xFFFF) / 65535f; } }
	private void RoadStyle(Graphics g, Pj a, Pj b, int i, bool alt, double t)
	{
		Color r1 = T0.R1, r2 = T0.R2;
		// 먼 칸(화면에서 2픽셀도 안 되는 줄)은 무늬를 빼고 바닥색만 → 멀리서 무늬가 지글거리지 않아요
		if (a.Y - b.Y < 2f) { Span(g, Sh(T0.G1, course == 2 ? 6 : -10), a, b, -1.13f, 1.13f); Span(g, Color.FromArgb((r1.R + r2.R) / 2, (r1.G + r2.G) / 2, (r1.B + r2.B) / 2), a, b, -1f, 1f); if (course == 4 && i % 8 == 0) Span(g, Color.FromArgb(90, 120, 255, 220), a, b, -1f, 1f); return; }
		switch (course)
		{
			case 1:   // 사막: 모래 흙길 · 부드러운 모래 턱 · 바퀴 자국 두 줄
				Span(g, alt ? T0.G2 : Sh(T0.G2, -12), a, b, -1.14f, 1.14f);
				Span(g, (i / 4) % 2 == 0 ? r1 : r2, a, b, -1f, 1f);
				Span(g, Sh(r1, -22), a, b, -0.46f, -0.3f); Span(g, Sh(r1, -22), a, b, 0.3f, 0.46f);
				if (Hash(i, 1) < 0.18f) { float u = Hash(i, 2) * 1.6f - 0.8f; Span(g, Sh(r1, 26), a, b, u - 0.05f, u + 0.05f); }
				break;
			case 2:   // 빙하: 반짝이는 얼음판 · 하얀 광택 줄무늬 · 가장자리 눈 둑
				Span(g, alt ? Color.FromArgb(250, 252, 255) : Color.FromArgb(232, 242, 252), a, b, -1.16f, 1.16f);
				Span(g, alt ? r1 : Sh(r1, 6), a, b, -1f, 1f);
				{ float u = Hash(i / 2, 3) * 1.5f - 0.75f, w = 0.12f + Hash(i / 2, 4) * 0.2f; if (Hash(i / 2, 5) < 0.55f) Span(g, Color.FromArgb(150, 255, 255, 255), a, b, u - w, u + w); }
				if (Hash(i, 6) < 0.25f) { float u = Hash(i, 7) * 1.7f - 0.85f; Span(g, Color.FromArgb(200, 120, 170, 210), a, b, u - 0.015f, u + 0.015f); }   // 얼음 금
				break;
			case 3:   // 숲: 흙 오솔길 · 가운데가 닳아 밝은 길 · 군데군데 풀
				Span(g, alt ? Sh(T0.G1, -14) : Sh(T0.G2, -14), a, b, -1.12f, 1.12f);
				Span(g, (i / 3) % 2 == 0 ? r1 : r2, a, b, -1f, 1f);
				Span(g, Sh(r1, 14), a, b, -0.34f, 0.34f);
				if (Hash(i, 8) < 0.22f) { float u = (Hash(i, 9) < 0.5f ? -1f : 1f) * (0.62f + Hash(i, 10) * 0.3f); Span(g, Sh(T0.G1, 10), a, b, u - 0.07f, u + 0.07f); }
				if (Hash(i, 11) < 0.1f) { float u = Hash(i, 12) * 1.2f - 0.6f; Span(g, Sh(r1, -26), a, b, u - 0.04f, u + 0.04f); }   // 돌멩이
				break;
			case 4:   // 외계: 어두운 금속판 · 빛나는 가운데 선 · 가로 빛줄
				Span(g, alt ? T0.Ru1 : T0.Ru2, a, b, -1.1f, 1.1f);
				Span(g, alt ? r1 : r2, a, b, -1f, 1f);
				if (i % 8 == 0) Span(g, Color.FromArgb(150, 120, 255, 220), a, b, -1f, 1f);
				{ int pulse = (int)(t * 10) % 6; Span(g, ((i + pulse) % 6) < 3 ? Color.FromArgb(160, 255, 230) : Color.FromArgb(70, 160, 150), a, b, -0.03f, 0.03f); }
				Span(g, Color.FromArgb(120, 200, 120, 255), a, b, -0.82f, -0.78f); Span(g, Color.FromArgb(120, 200, 120, 255), a, b, 0.78f, 0.82f);
				break;
			case 5: case 11:   // 중세(사리호는 금빛): 엇갈려 쌓은 돌바닥 (벽돌 무늬) · 돌 경계석
				Span(g, alt ? Color.FromArgb(170, 170, 176) : Color.FromArgb(120, 120, 128), a, b, -1.1f, 1.1f);
				{ int row = i / 2; float off = row % 2 == 0 ? 0f : 0.14f; for (int k = -1; k < 8; k++) { float u0 = -1f + k * 0.286f + off, u1 = u0 + 0.272f; if (u1 <= -1f || u0 >= 1f) continue; float h = Hash(row, k); Span(g, h < 0.33f ? r1 : (h < 0.66f ? r2 : Sh(r1, 14)), a, b, Math.Max(-1f, u0), Math.Min(1f, u1)); } }
				break;
			case 6:   // 요정: 분홍·보라·민트 파스텔 줄무늬 길 · 꽃 경계
				Span(g, alt ? Color.FromArgb(255, 255, 255) : Color.FromArgb(255, 186, 220), a, b, -1.12f, 1.12f);
				{ Color[] pc = { Color.FromArgb(255, 206, 228), Color.FromArgb(222, 206, 255), Color.FromArgb(200, 244, 226), Color.FromArgb(255, 240, 200) }; Span(g, pc[(i / 4) % 4], a, b, -1f, 1f); }
				if (Hash(i, 13) < 0.2f) { float u = Hash(i, 14) * 1.6f - 0.8f; Span(g, Color.FromArgb(180, 255, 255, 255), a, b, u - 0.03f, u + 0.03f); }   // 반짝
				break;
			case 7: case 9: case 10:   // 일본·석탑길: 큼직한 판석을 깐 돌길 · 자갈 가장자리
				Span(g, alt ? Color.FromArgb(200, 196, 186) : Color.FromArgb(186, 182, 172), a, b, -1.14f, 1.14f);
				{ int row = i / 4; float off = row % 2 == 0 ? 0f : 0.33f; Span(g, Sh(r1, -30), a, b, -1f, 1f); if (i % 4 != 0) for (int k = -1; k < 4; k++) { float u0 = -1f + k * 0.667f + off + 0.02f, u1 = u0 + 0.627f; if (u1 <= -1f || u0 >= 1f) continue; Span(g, Hash(row, k) < 0.5f ? r1 : r2, a, b, Math.Max(-1f, u0), Math.Min(1f, u1)); } }
				break;
			default:   // 청사: 바둑판 바닥 타일 · 청록 카펫 가장자리
				Span(g, alt ? T0.Ru1 : Sh(T0.Ru1, -12), a, b, -1.12f, 1.12f);
				{ int row = i / 2; for (int k = 0; k < 6; k++) { float u0 = -1f + k / 3f, u1 = u0 + 1f / 3f; Span(g, (k + row) % 2 == 0 ? r1 : Color.FromArgb(250, 248, 244), a, b, u0, u1); } }
				if (i % 2 == 0) Span(g, Color.FromArgb(60, 120, 110, 100), a, b, -1f, 1f);   // 타일 줄눈
				break;
		}
	}
	// ── 코스 고르기 화면 ──
	private void DrawSelect(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(40, 70, 120), Color.FromArgb(20, 36, 70));
		Txt(g, "코스를 골라요", 16f, 2, Color.White, 0, 42, W, 28, StringAlignment.Center);
		Txt(g, "카드를 눌러 골라요 · 숫자 1~9", 9f, 1, Color.FromArgb(180, 220, 235, 255), 0, 68, W, 16, StringAlignment.Center);
		for (int k = 0; k < Th.Length; k++)
		{
			Thm th = Th[k]; RectangleF r = CardR(k); bool hv = k == hovCard; if (hv) r.Inflate(3, 3);
			using (GraphicsPath gp = Gfx.Round(r, 14))
			{
				GraphicsState gs = g.Save(); g.SetClip(gp);
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, 56), th.SkyA, th.SkyB, 90f)) g.FillRectangle(lb, r.X, r.Y, r.Width, 56);
				using (SolidBrush b = new SolidBrush(th.HillN)) g.FillEllipse(b, r.X - 20, r.Y + 40, r.Width * 0.8f, 32); using (SolidBrush b = new SolidBrush(th.HillF)) g.FillEllipse(b, r.X + r.Width * 0.4f, r.Y + 38, r.Width * 0.8f, 32);
				using (SolidBrush b = new SolidBrush(th.G1)) g.FillRectangle(b, r.X, r.Y + 54, r.Width, r.Height - 54);
				using (SolidBrush b = new SolidBrush(th.R1)) g.FillPolygon(b, new PointF[] { new PointF(r.X + r.Width / 2f - 6, r.Y + 54), new PointF(r.X + r.Width / 2f + 6, r.Y + 54), new PointF(r.Right - 8, r.Y + 80), new PointF(r.X + 8, r.Y + 80) });
				using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 20, 40))) g.FillRectangle(b, r.X, r.Y + 74, r.Width, r.Height - 74);
				g.Restore(gs);
				using (Pen p = new Pen(hv ? Color.FromArgb(255, 255, 220, 110) : Color.FromArgb(110, 255, 255, 255), hv ? 2.6f : 1.2f)) g.DrawPath(p, gp);
			}
			// 코스 모양 (위에서 본 고리)
			List<PointF> mp = courseMap[k]; if (mp != null && mp.Count > 2) { RectangleF mr = new RectangleF(r.X + r.Width - 46, r.Y + 5, 40, 40); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) using (GraphicsPath gp2 = Gfx.Round(mr, 8)) g.FillPath(b, gp2); DrawLoop(g, mp, mr, 1.6f); }
			Txt(g, (k + 1).ToString(), 10f, 2, Color.White, r.X + 6, r.Y + 4, 20, 18, StringAlignment.Near);
			Txt(g, th.Sub, 8f, 2, Color.FromArgb(255, 230, 140), r.X + 6, r.Y + 76, r.Width - 12, 13, StringAlignment.Near);
			Txt(g, th.Name, 9f, 2, Color.White, r.X + 6, r.Y + 88, r.Width - 12, 15, StringAlignment.Near);
			Txt(g, new string('★', th.Stars) + new string('☆', 3 - th.Stars) + "  " + th.Tags, 7f, 1, Color.FromArgb(220, 230, 240, 250), r.X + 6, r.Y + 103, r.Width - 10, 14, StringAlignment.Near);
		}
	}
	private static void DrawLoop(Graphics g, List<PointF> pts, RectangleF r, float wpen)
	{
		float mnx = 1e9f, mny = 1e9f, mxx = -1e9f, mxy = -1e9f; foreach (PointF q in pts) { mnx = Math.Min(mnx, q.X); mny = Math.Min(mny, q.Y); mxx = Math.Max(mxx, q.X); mxy = Math.Max(mxy, q.Y); }
		float sc = Math.Min((r.Width - 8f) / Math.Max(1f, mxx - mnx), (r.Height - 8f) / Math.Max(1f, mxy - mny)), ox = r.X + (r.Width - (mxx - mnx) * sc) / 2f, oy = r.Y + (r.Height - (mxy - mny) * sc) / 2f;
		PointF[] line = new PointF[(pts.Count + 7) / 8 + 1]; for (int k = 0; k < line.Length - 1; k++) { PointF q = pts[Math.Min(pts.Count - 1, k * 8)]; line[k] = new PointF(ox + (q.X - mnx) * sc, oy + (q.Y - mny) * sc); } line[line.Length - 1] = line[0];
		using (Pen p = new Pen(Color.FromArgb(230, 255, 255, 255), wpen) { LineJoin = LineJoin.Round }) g.DrawLines(p, line);
	}
	// 길 위 발판 그리기 (먼 칸부터 그리는 줄과 함께)
	private void PadRow(Graphics g, Pj a, Pj b, int i, double t)
	{
		byte k = padK[i]; float x = padX[i], w = padW[i];
		if (k == 2)
		{
			// 점프 발판: 길 전체에 파랑·흰 줄무늬 + 앞쪽 가장자리
			using (SolidBrush sb = new SolidBrush(i % 2 == 0 ? Color.FromArgb(60, 140, 255) : Color.FromArgb(240, 250, 255))) Quad(g, sb, a.X, a.Y, a.W * 0.98f, b.X, b.Y, b.W * 0.98f);
			for (int q = -3; q <= 3; q += 2) using (SolidBrush sb = new SolidBrush(Color.FromArgb(255, 210, 60))) Quad(g, sb, a.X + a.W * q * 0.28f, a.Y, a.W * 0.06f, b.X + b.W * q * 0.28f, b.Y, b.W * 0.06f);
			return;
		}
		float ax = a.X + a.W * x, bxx = b.X + b.W * x;
		if (k == 1)
		{
			// 부스트 발판: 노랑·주황 화살표 줄 (반짝여요)
			bool on = ((int)(t * 8) + i) % 2 == 0;
			using (SolidBrush sb = new SolidBrush(on ? Color.FromArgb(255, 200, 40) : Color.FromArgb(255, 130, 30))) Quad(g, sb, ax, a.Y, a.W * w, bxx, b.Y, b.W * w);
			using (SolidBrush sb = new SolidBrush(Color.FromArgb(255, 255, 250, 220))) Quad(g, sb, ax, a.Y, a.W * w * 0.25f, bxx, b.Y, b.W * w * 0.25f);
			return;
		}
		// 감속 구역: 테마 색 덩어리 (모래 · 진흙 · 서류 더미 등)
		using (SolidBrush sb = new SolidBrush(Color.FromArgb(235, T0.Slow))) Quad(g, sb, ax, a.Y, a.W * w, bxx, b.Y, b.W * w);
		if (course == 8 && i % 2 == 0) using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 180, 190, 200))) Quad(g, sb, ax - a.W * w * 0.5f, a.Y, a.W * w * 0.3f, bxx - b.W * w * 0.5f, b.Y, b.W * w * 0.3f);
	}
	private static Bitmap pagodaBmp; private static bool pagodaTried;
	private static Bitmap Pagoda() { if (!pagodaTried) { pagodaTried = true; try { using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("pagoda.png")) if (st != null) using (Bitmap t = new Bitmap(st)) pagodaBmp = new Bitmap(t); } catch { } } return pagodaBmp; }
	// 테마 길가 소품: 그림(ms_kt{코스}_{번호})이 있으면 그걸 쓰고, 없으면 간단히 그려요
	//  0·1 큰 건물(집 자리) · 2 큰 식물(나무 자리) · 3 작은 식물/바위 · 4 급커브 울타리 · 5 가로등 자리 소품
	private void DecoTheme(Graphics g, Pj a, float side, int kind, float clip, int seg)
	{
		float u = a.Sc * W / 2f, sx = a.X + u * side * ROADW; float h = u * (kind == 2 ? 2300f : (kind == 1 ? 2400f : (kind == 3 ? 520f : (kind == 4 ? 1700f : 2000f)))); if (h < 2f || h > 340f || a.Y - h > clip) return;
		int slot = kind == 2 ? ((seg / 5) % 2) : (kind == 0 ? (seg % 10 == 0 ? 3 : 2) : (kind == 3 ? 4 : (kind == 4 ? 5 : 3)));
		GraphicsState st = g.Save(); g.SetClip(new RectangleF(0, 0, W, clip));
		int[] pool = slot <= 1 ? new[] { 0, 1, 6, 7 } : (slot <= 3 ? new[] { 2, 3, 8 } : (slot == 4 ? new[] { 4, 10 } : new[] { 5, 9, 3 }));
		string pn = null; { List<string> have = new List<string>(); foreach (int q in pool) { string nm = "ms_kt" + course + "_" + q; if (Spr.Get(nm) != null) have.Add(nm); } if (have.Count > 0) pn = have[(slot == 4 ? seg / 40 : seg / 5 + slot) % have.Count]; }
		Bitmap pb = pn == null ? null : Spr.Get(pn);
		if (pb != null) { float ph = kind == 3 ? h * 1.1f : h, pw = ph * pb.Width / pb.Height; Spr.Draw(g, pn, sx, a.Y - ph / 2f, pw, ph, 0f); g.Restore(st); return; }
		Color c1, c2, c3;
		switch (course)
		{
			case 1: c1 = Color.FromArgb(70, 150, 80); c2 = Color.FromArgb(200, 150, 100); c3 = Color.FromArgb(170, 110, 70); break;
			case 2: c1 = Color.FromArgb(60, 120, 110); c2 = Color.FromArgb(250, 252, 255); c3 = Color.FromArgb(150, 210, 240); break;
			case 3: c1 = Color.FromArgb(40, 110, 60); c2 = Color.FromArgb(220, 60, 60); c3 = Color.FromArgb(120, 80, 50); break;
			case 4: c1 = Color.FromArgb(160, 90, 230); c2 = Color.FromArgb(90, 240, 210); c3 = Color.FromArgb(60, 50, 110); break;
			case 5: c1 = Color.FromArgb(150, 150, 160); c2 = Color.FromArgb(70, 80, 160); c3 = Color.FromArgb(230, 220, 200); break;
			case 6: c1 = Color.FromArgb(255, 150, 200); c2 = Color.FromArgb(180, 140, 240); c3 = Color.FromArgb(255, 240, 120); break;
			case 7: c1 = Color.FromArgb(90, 160, 80); c2 = Color.FromArgb(200, 40, 40); c3 = Color.FromArgb(150, 150, 140); break;
			case 9: case 10: case 11: c1 = Color.FromArgb(70, 140, 80); c2 = Color.FromArgb(120, 110, 100); c3 = Color.FromArgb(170, 164, 150); break;
			default: c1 = Color.FromArgb(250, 250, 245); c2 = Color.FromArgb(14, 138, 124); c3 = Color.FromArgb(150, 160, 170); break;
		}
		using (SolidBrush b1 = new SolidBrush(c1)) using (SolidBrush b2 = new SolidBrush(c2)) using (SolidBrush b3 = new SolidBrush(c3))
		{
			if (slot == 4)
			{
				// 울타리
				g.FillRectangle(b3, sx - h * 1.6f, a.Y - h * 0.55f, h * 3.2f, h * 0.18f); for (int k = -2; k <= 2; k++) g.FillRectangle(k % 2 == 0 ? b2 : b3, sx + k * h * 0.75f - h * 0.08f, a.Y - h * 0.9f, h * 0.16f, h * 0.9f);
			}
			else if (slot <= 1)
			{
				// 큰 건물 자리: 테마별 모양
				float w = h * 0.8f;
				if (course >= 9 && Pagoda() != null) { Bitmap pg = Pagoda(); float ph = h * 1.15f, pw = ph * pg.Width / pg.Height; g.DrawImage(pg, sx - pw / 2f, a.Y - ph, pw, ph); }
				else if (course == 5) { g.FillRectangle(b1, sx - w * 0.3f, a.Y - h * 0.8f, w * 0.6f, h * 0.8f); g.FillPolygon(b2, new PointF[] { new PointF(sx - w * 0.38f, a.Y - h * 0.8f), new PointF(sx, a.Y - h * 1.15f), new PointF(sx + w * 0.38f, a.Y - h * 0.8f) }); }
				else if (course == 7) { g.FillRectangle(b2, sx - w * 0.45f, a.Y - h, w * 0.1f, h); g.FillRectangle(b2, sx + w * 0.35f, a.Y - h, w * 0.1f, h); g.FillRectangle(b2, sx - w * 0.6f, a.Y - h, w * 1.2f, h * 0.1f); g.FillRectangle(b2, sx - w * 0.5f, a.Y - h * 0.8f, w, h * 0.06f); }
				else if (course == 8) { g.FillRectangle(b3, sx - w * 0.5f, a.Y - h * 0.5f, w, h * 0.08f); g.FillRectangle(b3, sx - w * 0.45f, a.Y - h * 0.42f, w * 0.06f, h * 0.42f); g.FillRectangle(b3, sx + w * 0.39f, a.Y - h * 0.42f, w * 0.06f, h * 0.42f); g.FillRectangle(b1, sx - w * 0.3f, a.Y - h * 0.62f, w * 0.4f, h * 0.12f); using (SolidBrush mb = new SolidBrush(Color.FromArgb(40, 50, 60))) g.FillRectangle(mb, sx + w * 0.1f, a.Y - h * 0.82f, w * 0.3f, h * 0.3f); }
				else if (course == 2) { g.FillPie(b2, sx - w * 0.5f, a.Y - h * 0.6f, w, h * 1.2f, 180, 180); using (Pen p = new Pen(c3, Math.Max(1f, h * 0.02f))) g.DrawArc(p, sx - w * 0.5f, a.Y - h * 0.6f, w, h * 1.2f, 180, 180); }
				else if (course == 4) { g.FillPie(b3, sx - w * 0.55f, a.Y - h * 0.7f, w * 1.1f, h * 1.4f, 180, 180); using (SolidBrush gb = new SolidBrush(Color.FromArgb(120, c2))) g.FillPie(gb, sx - w * 0.45f, a.Y - h * 0.6f, w * 0.9f, h * 1.2f, 180, 180); }
				else if (course == 6) { g.FillRectangle(b3, sx - w * 0.06f, a.Y - h * 0.6f, w * 0.12f, h * 0.6f); g.FillEllipse(b1, sx - w * 0.5f, a.Y - h, w, h * 0.5f); using (SolidBrush wb = new SolidBrush(Color.White)) for (int k = 0; k < 4; k++) g.FillEllipse(wb, sx - w * 0.35f + k * w * 0.2f, a.Y - h * 0.85f + (k % 2) * h * 0.12f, w * 0.1f, w * 0.1f); }
				else { g.FillRectangle(b3, sx - w * 0.4f, a.Y - h * 0.55f, w * 0.8f, h * 0.55f); g.FillPolygon(b2, new PointF[] { new PointF(sx - w * 0.5f, a.Y - h * 0.55f), new PointF(sx, a.Y - h), new PointF(sx + w * 0.5f, a.Y - h * 0.55f) }); }
			}
			else if (slot == 2 || slot == 3)
			{
				// 큰 식물 자리: 선인장 · 눈 덮인 침엽수 · 큰 나무 · 수정 기둥 · 깃발 · 큰 꽃 · 대나무 · 화분
				if (course >= 9) { using (SolidBrush tb = new SolidBrush(Color.FromArgb(110, 80, 50))) g.FillRectangle(tb, sx - h * 0.05f, a.Y - h * 0.4f, h * 0.1f, h * 0.4f); g.FillEllipse(b1, sx - h * 0.3f, a.Y - h, h * 0.6f, h * 0.65f); using (SolidBrush lb4 = new SolidBrush(Color.FromArgb(90, 160, 90))) g.FillEllipse(lb4, sx - h * 0.2f, a.Y - h * 0.95f, h * 0.3f, h * 0.3f); }
				else if (course == 1) { g.FillRectangle(b1, sx - h * 0.07f, a.Y - h, h * 0.14f, h); g.FillRectangle(b1, sx - h * 0.25f, a.Y - h * 0.6f, h * 0.1f, h * 0.3f); g.FillRectangle(b1, sx + h * 0.15f, a.Y - h * 0.75f, h * 0.1f, h * 0.35f); g.FillRectangle(b1, sx - h * 0.25f, a.Y - h * 0.35f, h * 0.2f, h * 0.07f); g.FillRectangle(b1, sx + h * 0.05f, a.Y - h * 0.45f, h * 0.2f, h * 0.07f); }
				else if (course == 2 || course == 3) { using (SolidBrush tb = new SolidBrush(Color.FromArgb(110, 80, 50))) g.FillRectangle(tb, sx - h * 0.05f, a.Y - h * 0.25f, h * 0.1f, h * 0.25f); for (int k = 0; k < 3; k++) { float yy = a.Y - h * (0.25f + k * 0.25f), ww = h * (0.42f - k * 0.1f); g.FillPolygon(b1, new PointF[] { new PointF(sx - ww, yy), new PointF(sx, yy - h * 0.38f), new PointF(sx + ww, yy) }); if (course == 2) g.FillPolygon(b2, new PointF[] { new PointF(sx - ww * 0.4f, yy - h * 0.23f), new PointF(sx, yy - h * 0.38f), new PointF(sx + ww * 0.4f, yy - h * 0.23f) }); } }
				else if (course == 4) { g.FillPolygon(b1, new PointF[] { new PointF(sx - h * 0.12f, a.Y), new PointF(sx - h * 0.02f, a.Y - h), new PointF(sx + h * 0.1f, a.Y) }); g.FillPolygon(b2, new PointF[] { new PointF(sx + h * 0.05f, a.Y), new PointF(sx + h * 0.15f, a.Y - h * 0.6f), new PointF(sx + h * 0.25f, a.Y) }); }
				else if (course == 5) { g.FillRectangle(b3, sx - h * 0.02f, a.Y - h, h * 0.04f, h); g.FillPolygon(b2, new PointF[] { new PointF(sx + h * 0.02f, a.Y - h), new PointF(sx + h * 0.36f, a.Y - h * 0.9f), new PointF(sx + h * 0.02f, a.Y - h * 0.78f) }); }
				else if (course == 6) { using (SolidBrush sb2 = new SolidBrush(Color.FromArgb(90, 170, 90))) g.FillRectangle(sb2, sx - h * 0.03f, a.Y - h * 0.7f, h * 0.06f, h * 0.7f); for (int k = 0; k < 6; k++) { double an = k * Math.PI / 3; g.FillEllipse(k % 2 == 0 ? b1 : b2, sx + (float)Math.Cos(an) * h * 0.16f - h * 0.12f, a.Y - h * 0.85f + (float)Math.Sin(an) * h * 0.16f - h * 0.12f, h * 0.24f, h * 0.24f); } g.FillEllipse(b3, sx - h * 0.1f, a.Y - h * 0.95f, h * 0.2f, h * 0.2f); }
				else if (course == 7) { for (int k = -2; k <= 2; k++) { g.FillRectangle(b1, sx + k * h * 0.12f - h * 0.03f, a.Y - h * (0.85f + (k & 1) * 0.15f), h * 0.06f, h * (0.85f + (k & 1) * 0.15f)); using (SolidBrush nb = new SolidBrush(Color.FromArgb(60, 120, 60))) for (int q = 1; q < 5; q++) g.FillRectangle(nb, sx + k * h * 0.12f - h * 0.035f, a.Y - h * q * 0.2f, h * 0.07f, h * 0.012f); } }
				else { g.FillRectangle(b3, sx - h * 0.12f, a.Y - h * 0.2f, h * 0.24f, h * 0.2f); using (SolidBrush lb2 = new SolidBrush(Color.FromArgb(70, 160, 90))) { g.FillEllipse(lb2, sx - h * 0.25f, a.Y - h * 0.65f, h * 0.3f, h * 0.45f); g.FillEllipse(lb2, sx - h * 0.05f, a.Y - h * 0.7f, h * 0.3f, h * 0.5f); } }
			}
			else
			{
				// 작은 소품: 바위 · 얼음 · 버섯 · 수정 · 돌 · 꽃 · 석등 · 서류 더미
				if (course == 3) { g.FillRectangle(Brushes.White, sx - h * 0.06f, a.Y - h * 0.3f, h * 0.12f, h * 0.3f); g.FillPie(b2, sx - h * 0.25f, a.Y - h * 0.5f, h * 0.5f, h * 0.4f, 180, 180); }
				else if (course == 8) { for (int k = 0; k < 5; k++) g.FillRectangle(k % 2 == 0 ? b1 : Brushes.WhiteSmoke, sx - h * 0.3f + (k % 2) * h * 0.03f, a.Y - h * 0.1f * (k + 1), h * 0.6f, h * 0.1f); }
				else if (course == 7 || course >= 9) { g.FillRectangle(b3, sx - h * 0.06f, a.Y - h * 0.5f, h * 0.12f, h * 0.5f); g.FillRectangle(b3, sx - h * 0.18f, a.Y - h * 0.62f, h * 0.36f, h * 0.14f); using (SolidBrush lb3 = new SolidBrush(Color.FromArgb(255, 230, 150))) g.FillRectangle(lb3, sx - h * 0.08f, a.Y - h * 0.48f, h * 0.16f, h * 0.1f); }
				else g.FillEllipse(course == 6 ? b1 : b3, sx - h * 0.35f, a.Y - h * 0.4f, h * 0.7f, h * 0.45f);
			}
		}
		g.Restore(st);
	}
	// 지름길 흙길 한 줄: 모래빛 길 + 바퀴 자국 두 줄
	private void PathRow(Graphics g, Pj a, Pj b, int v, bool alt)
	{
		WallBrushes();
		Quad(g, alt ? dirt2 : dirt1, a.X, a.Y, a.W * (PATHW + 0.12f), b.X, b.Y, b.W * (PATHW + 0.12f));
		using (SolidBrush rb = new SolidBrush(Color.FromArgb(70, 90, 60, 30))) { Quad(g, rb, a.X - a.W * 0.35f, a.Y, a.W * 0.05f, b.X - b.W * 0.35f, b.Y, b.W * 0.05f); Quad(g, rb, a.X + a.W * 0.35f, a.Y, a.W * 0.05f, b.X + b.W * 0.35f, b.Y, b.W * 0.05f); }
		if (v >= scLen - 3) using (SolidBrush jb = new SolidBrush(Color.FromArgb(200, 255, 200, 70))) Quad(g, jb, a.X - scSide * a.W * 0.5f, a.Y, a.W * 0.12f, b.X - scSide * b.W * 0.5f, b.Y, b.W * 0.12f);   // 합류 표시
	}
	// 벽(낮은 방호벽) · 벽이 트인 곳의 흙길
	private int wbFor = -1; private SolidBrush wb1, wb2, wbTop, dirt1, dirt2;
	private void WallRow(Graphics g, Pj a, Pj b, int i, bool alt)
	{
		WallBrushes();
		if (a.W < 1.5f) return;
		WallFaces(g, a, b, i, alt);
	}
	private void WallBrushes()
	{
		if (wbFor != course)
		{
			wbFor = course; Color c1 = T0.Ru1, c2 = T0.Ru2;
			wb1 = new SolidBrush(Sh(c1, -12)); wb2 = new SolidBrush(Sh(c2, -14)); wbTop = new SolidBrush(Sh(c2, 12));
			Color d = Color.FromArgb(226, 196, 136); dirt1 = new SolidBrush(d); dirt2 = new SolidBrush(Sh(d, -10));
		}
	}
	private void WallFaces(Graphics g, Pj a, Pj b, int i, bool alt)
	{
		for (int sd = -1; sd <= 1; sd += 2)
		{
			if (gap[i] == sd)
			{
				float cA = a.X + sd * a.W * 1.62f, cB = b.X + sd * b.W * 1.62f; Quad(g, alt ? dirt1 : dirt2, cA, a.Y, a.W * 0.62f, cB, b.Y, b.W * 0.62f);
				if (i % 4 == 0 && a.Y - b.Y > 2f) { float ca = a.X + sd * a.W * 0.78f, cb = b.X + sd * b.W * 0.78f, wa = a.W * 0.1f, wb = b.W * 0.1f; using (SolidBrush cbr = new SolidBrush(Color.FromArgb(235, 255, 170, 60))) g.FillPolygon(cbr, new PointF[] { new PointF(ca - sd * wa, a.Y), new PointF((ca + cb) / 2f + sd * (wa + wb) / 2f, (a.Y + b.Y) / 2f), new PointF(cb - sd * wb, b.Y), new PointF((ca + cb) / 2f - sd * (wa + wb) * 0.1f, (a.Y + b.Y) / 2f) }); }
				continue;
			}
			float xa = a.X + sd * a.W * WALL, xb = b.X + sd * b.W * WALL, ha = a.W * 0.16f, hb = b.W * 0.16f;
			if (Math.Abs(xa - xb) < 2.5f && ha > 12f) continue;   // 바로 앞에서 모서리만 보이는 벽은 세로 줄처럼 보여서 생략해요
			g.FillPolygon(alt ? wb1 : wb2, new PointF[] { new PointF(xa, a.Y), new PointF(xa, a.Y - ha), new PointF(xb, b.Y - hb - 1f), new PointF(xb, b.Y - 1f) });
			if (ha > 3f) g.FillPolygon(wbTop, new PointF[] { new PointF(xa, a.Y - ha), new PointF(xa, a.Y - ha - ha * 0.18f), new PointF(xb, b.Y - hb * 1.18f - 1f), new PointF(xb, b.Y - hb - 1f) });
		}
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
		using (Pen p = new Pen(Color.FromArgb(240, 255, 170, 60), 2f) { DashStyle = DashStyle.Dash }) foreach (int[] s2 in shorts) g.DrawLine(p, at(s2[0]), at(s2[1]));
		foreach (Car c in cars) { PointF q = at(SegOf(c.Z)); using (SolidBrush b = new SolidBrush(c.C)) g.FillEllipse(b, q.X - 3f, q.Y - 3f, 6f, 6f); }
		PointF me = at(SegOf(PlayerZ)); if (scOn) { PointF q0 = at(scQ[0]), q1 = at(scQ[1]); float pr = ScProg; me = new PointF(q0.X + (q1.X - q0.X) * pr, q0.Y + (q1.Y - q0.Y) * pr); } using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 230, 90))) g.FillEllipse(b, me.X - 4.5f, me.Y - 4.5f, 9f, 9f); using (Pen p = new Pen(Color.FromArgb(30, 160, 140), 2f)) g.DrawEllipse(p, me.X - 4.5f, me.Y - 4.5f, 9f, 9f);
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
		string isp = k == 0 ? "ms_kt_boost" : (k == 2 ? "ms_kt_balloon" : (k == 3 ? "ms_kt_giant" : null)); if (isp != null && Spr.Get(isp) != null) { Spr.Draw(g, isp, cx, cy, 34f, 34f, 0f); return; }
		if (k == 3) { using (GraphicsPath gp = Gfx.Round(new RectangleF(cx - 10, cy - 8, 20, 22), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 200, 60))) g.FillPath(b, gp); using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 200, 120))) g.FillRectangle(b, cx - 6, cy - 15, 12, 7); using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, cx - 7, cy - 1, 14, 8); using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 90, 90))) g.FillPolygon(b, new PointF[] { new PointF(cx, cy - 1), new PointF(cx + 5, cy + 4), new PointF(cx + 2, cy + 4), new PointF(cx + 2, cy + 7), new PointF(cx - 2, cy + 7), new PointF(cx - 2, cy + 4), new PointF(cx - 5, cy + 4) }); return; }   // 쑥쑥 비타민 병
		if (k == 0) { using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 150, 40))) g.FillPolygon(b, new PointF[] { new PointF(cx - 6, cy - 16), new PointF(cx + 10, cy - 2), new PointF(cx + 1, cy - 2), new PointF(cx + 6, cy + 14), new PointF(cx - 10, cy - 1), new PointF(cx - 1, cy - 1) }); }
		else if (k == 1 && Spr.Get("ms_kp_7") != null) Spr.Draw(g, "ms_kp_7", cx, cy, 34f, 26f, 0f);
		else if (k == 1) { using (Pen p = new Pen(Color.FromArgb(250, 220, 60), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, cx - 14, cy - 16, 28, 26, 20, 140); }
		else { using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 180, 255))) g.FillEllipse(b, cx - 12, cy - 12, 24, 24); using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, cx - 7, cy - 8, 8, 6); }
	}
	private static void Panel(Graphics g, RectangleF r)
	{
		using (GraphicsPath gp = Gfx.Round(r, 12)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 12, 24, 46))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(80, 255, 255, 255), 1.2f)) g.DrawPath(p, gp); }
	}
	private void DrawParts(Graphics g, int layer)
	{
		foreach (Pt q in parts)
		{
			if ((q.K == 0) != (layer == 0)) continue; float f = q.Life / q.Max; int al = (int)(q.C.A * Math.Min(1f, f * 1.5f));
			using (SolidBrush b = new SolidBrush(Color.FromArgb(al, q.C))) { if (q.K == 2) g.FillEllipse(b, q.X - q.Size / 2f, q.Y - q.Size * 0.7f, q.Size, q.Size * 1.4f); else g.FillEllipse(b, q.X - q.Size / 2f, q.Y - q.Size / 2f, q.Size, q.Size); }
		}
	}
	// 어지러운 별 (빙글빙글 돌고 난 뒤)
	private static void Dizzy(Graphics g, float cx, float cy, float r, double t)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 225, 80))) for (int k = 0; k < 3; k++)
		{
			double an = t * 6 + k * Math.PI * 2 / 3; float x = cx + (float)Math.Cos(an) * r, y = cy + (float)Math.Sin(an) * r * 0.35f, s = Math.Max(3f, r * 0.28f) * (0.8f + 0.2f * (float)Math.Sin(an));
			PointF[] st = new PointF[10]; for (int j = 0; j < 10; j++) { double a2 = -Math.PI / 2 + j * Math.PI / 5; float rr = j % 2 == 0 ? s : s * 0.45f; st[j] = new PointF(x + (float)Math.Cos(a2) * rr, y + (float)Math.Sin(a2) * rr); }
			g.FillPolygon(b, st);
		}
	}
	private static void Balloon(Graphics g, float cx, float cy, float r, double t)
	{
		if (Spr.Get("ms_kt_balloon") != null) { Spr.Draw(g, "ms_kt_balloon", cx, cy, r * 2.2f, r * 2.2f, (float)Math.Sin(t * 8) * 10f); return; }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 90, 170, 245))) g.FillEllipse(b, cx - r, cy - r, r * 2f, r * 2.1f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 230, 248, 255))) g.FillEllipse(b, cx - r * 0.55f, cy - r * 0.6f, r * 0.6f, r * 0.45f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 130, 210))) g.FillPolygon(b, new PointF[] { new PointF(cx - r * 0.2f, cy + r * 1.25f), new PointF(cx, cy + r * 1.0f), new PointF(cx + r * 0.2f, cy + r * 1.25f) });
	}
	// 바닥 스키드 자국: 남긴 지 오래될수록 옅어져요
	private readonly SolidBrush skB = new SolidBrush(Color.Black);
	private void SkidRow(Graphics g, Pj a, Pj b, int i)
	{
		float age = el - skT[i]; if (age > 120f || a.W < 2f) return; int j = (i + 1) % N; bool nb = el - skT[j] <= 120f && Math.Abs(skT[j] - skT[i]) < 1.5f && Math.Abs(skL[j] - skL[i]) < 0.25f;
		int al = (int)(140 * (1f - age / 120f)); if (al < 8) return; skB.Color = Color.FromArgb(al, 28, 24, 28);
		float w1 = Math.Min(9f, Math.Max(0.8f, a.W * 0.03f)), w2 = Math.Min(9f, Math.Max(0.7f, b.W * 0.03f)); if (a.Y > H + 40f) return;
		float l2 = nb ? skL[j] : skL[i], r2 = nb ? skR[j] : skR[i];
		Quad(g, skB, a.X + a.W * skL[i], a.Y, w1, b.X + b.W * l2, b.Y, w2); Quad(g, skB, a.X + a.W * skR[i], a.Y, w1, b.X + b.W * r2, b.Y, w2);
	}
	private static void BoostIcon(Graphics g, float cx, float cy, float sc)
	{
		if (Spr.Get("ms_kt_boost") != null) { Spr.Draw(g, "ms_kt_boost", cx, cy, 26f * sc, 26f * sc, 0f); return; }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 140, 40))) g.FillPolygon(b, new PointF[] { new PointF(cx - 4 * sc, cy - 12 * sc), new PointF(cx + 8 * sc, cy - 1 * sc), new PointF(cx + 1 * sc, cy - 1 * sc), new PointF(cx + 5 * sc, cy + 11 * sc), new PointF(cx - 8 * sc, cy), new PointF(cx - 1 * sc, cy) });
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
	internal static void DrawKart(Graphics g, float cx, float by, float w, Color c, bool me, float rot, float steer, string spr = null, bool flip = false, float sxs = 1f, float hop = 0f, float shear = 0f)
	{
		if (w < 3f) return;
		if (spr != null && Spr.Get(spr) != null)
		{
			// 그림(스프라이트) 카트: 바닥 그림자 + 원본 비율 그대로 (모든 카트가 같은 배율)
			// sxs: 가로 배율(제자리 회전할 때 옆으로 도는 느낌) · hop: 떠오른 높이 · shear: 드리프트할 때 앞머리가 꺾이는 기울기
			float k = w / 120f, sw = w * Math.Max(0.7f, 1f - hop / 300f); using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(sb, cx - sw * 0.5f, by - w * 0.08f, sw, w * 0.18f);
			GraphicsState s0 = g.Save(); g.TranslateTransform(cx, by - hop); g.RotateTransform(rot);
			if (sxs != 1f || shear != 0f) using (Matrix m = new Matrix(sxs, 0f, shear, 1f, 0f, 0f)) g.MultiplyTransform(m);
			Spr.Frame(g, spr, 0f, 0f, k, 0f, flip); g.Restore(s0); return;
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
