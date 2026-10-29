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
	private class Car { public float Z, X, V, Tgt, TgtT, Spin, Skill, Boost, ItemT, Drift; public int Lap, Item = -1; public Color C; public string Name; public bool Done; public float DoneT; }
	private class Box { public int Seg; public float X, Hide; }
	private class Ban { public float Z, X; }
	private readonly List<Car> cars = new List<Car>(); private readonly List<Box> boxes = new List<Box>(); private readonly List<Ban> bans = new List<Ban>();
	private float pos, px, v, el, skyOff, drift, charge, boostT, spinT, msgT, bumpT, steerVis, finT, hitT; private int lap, item = -1, place = 6, driftDir; private bool finished, wasDrift; private string msg = ""; private float itemRoll;
	private static readonly string[] ItemName = { "부스터", "바나나", "물풍선" };
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
	};
	private int course, cphase, hovCard = -1; private Thm T0 = Th[0];
	private static readonly List<PointF>[] courseMap = new List<PointF>[Th.Length];
	// 발판: K 1 부스트 · 2 점프 · 3 감속 구역
	private readonly byte[] padK = new byte[N]; private readonly float[] padX = new float[N], padW = new float[N];
	private float airH, airV, airT, padCd, latV;
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
			default:   // 청사 민원 대행진: 복도를 도는 미로 같은 직각 코스 · 서류 더미 · 결재 완료 발판
				sec(90, 0); sec(30, 90); sec(50, 0); sec(30, 90); sec(30, 0); sec(30, -90); sec(30, 0); sec(30, -90); sec(40, 0); sec(30, 90); sec(60, 0); sec(30, 90); sec(70, 0); sec(30, 90); sec(40, 0); sec(30, -90); sec(30, 90); sec(40, 0); sec(30, 90); sec(N - i, 0);
				AutoPads(id, 1, 5, 4, 8f); break;
		}
		// 미니맵은 끝점이 출발점과 정확히 만나도록 조금씩 나눠 맞춰요 (주행에는 영향 없음)
		{ int n0 = mapPts.Count; PointF e0 = mapPts[n0 - 1]; for (int k = 0; k < n0; k++) { float f = k / (float)(n0 - 1); mapPts[k] = new PointF(mapPts[k].X - e0.X * f, mapPts[k].Y - e0.Y * f); } }
		boxes.Clear(); foreach (int sq in bx) { if (padK[sq] != 0) continue; for (int k = -1; k <= 1; k++) boxes.Add(new Box { Seg = sq, X = k * 0.55f }); }
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
		airH = 0f; airV = 0f; airT = 0f; padCd = 0f; latV = 0f; cphase = 1; msg = T0.Name + " · 출발!"; msgT = 1.8f; Sfx.Play("ok");
	}
	private int SegOf(float z) { int s = (int)(Wrap(z) / SEG); return s < 0 ? 0 : (s >= N ? N - 1 : s); }
	private static float Wrap(float z) { z %= LEN; if (z < 0) z += LEN; return z; }
	private float PlayerZ { get { return pos + PZ; } }
	private float Dist(Car c) { return c.Lap * LEN + c.Z; }
	private float MyDist { get { return lap * LEN + Wrap(PlayerZ); } }
	public override void Click(float x, float y)
	{
		if (cphase == 0) { int k = CardAt(x, y); if (k >= 0) StartRace(k); return; }
		if (new RectangleF(316, 486, 64, 64).Contains(x, y)) UseItem();
	}
	private RectangleF CardR(int k) { return new RectangleF(12 + (k % 3) * 128f, 92 + (k / 3) * 160f, 120f, 152f); }
	private int CardAt(float x, float y) { for (int k = 0; k < Th.Length; k++) if (CardR(k).Contains(x, y)) return k; return -1; }
	public override void Press(Keys k) { if (cphase == 0) { if (k >= Keys.D1 && k <= Keys.D9) StartRace(k - Keys.D1); return; } if (k == Keys.Z || k == Keys.X || k == Keys.ControlKey || k == Keys.Enter) UseItem(); }
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
		if (cphase == 0) { hovCard = CardAt(inp.Mx, inp.My); return; }
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
		// 빙판은 핸들이 늦게 먹고(미끄러져요) 커브 바깥으로 더 밀려요 · 공중에서는 핸들이 반만 먹어요
		float latT = st * 2.1f * Math.Max(0.25f, sp) * (driftDir != 0 ? 1.15f : 1f) * (airH > 0f ? 0.5f : 1f);
		latV += (latT - latV) * Math.Min(1f, dt * 14f * T0.Grip); px += latV * dt;
		px -= dt * 2f * sp * cv * (driftDir != 0 ? 0.16f : 0.3f) * sp * (T0.Grip < 1f ? 1.25f : 1f);
		// 발판: 부스트 · 점프 · 감속 구역
		if (padCd > 0f) padCd -= dt; byte pk = padK[ps]; bool onPad = pk != 0 && Math.Abs(padX[ps] - px) < padW[ps]; bool slowZone = false;
		if (airH <= 0f && onPad)
		{
			if (pk == 1 && padCd <= 0f) { boostT = Math.Max(boostT, 1.2f); padCd = 0.5f; Sfx.Play("whoosh"); msg = course == 8 ? "결재 완료! 부스트" : "부스트 발판!"; msgT = 0.8f; }
			else if (pk == 2 && padCd <= 0f && v > MAXV * 0.3f) { airV = 430f * (0.6f + 0.4f * sp); airH = 0.01f; airT = 0f; padCd = 0.6f; Sfx.Play("swing"); msg = "점프!"; msgT = 0.7f; }
			else if (pk == 3) slowZone = true;
		}
		if (airH > 0f) { airT += dt; airV -= 1150f * T0.Grav * dt; airH += airV * dt; if (airH <= 0f) { airH = 0f; Shake = 0.22f; Sfx.Play("thud_soft"); if (airT > 0.7f) { boostT = Math.Max(boostT, 0.7f); msg = "착지 부스트!"; msgT = 0.8f; } } }
		// 속도
		float top = MAXV * (boostT > 0f ? 1.32f : 1f);
		bool offRoad = Math.Abs(px) > 1f && airH <= 0f; if (offRoad) top = Math.Min(top, MAXV * (boostT > 0f ? 0.75f : T0.OffSlow));
		if (slowZone) top = Math.Min(top, MAXV * (boostT > 0f ? 0.8f : 0.55f));
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
		for (int i = bans.Count - 1; i >= 0; i--) { Ban b = bans[i]; if (Math.Abs(Wrap(b.Z - pz + LEN * 0.5f) - LEN * 0.5f) < SEG * 0.7f && Math.Abs(b.X - px) < 0.22f && spinT <= 0f && el > 1f && airH <= 0f) { spinT = 1.0f; Shake = 0.4f; Sfx.Play("bad2"); msg = "바나나에 미끄러졌어요!"; msgT = 1.2f; bans.RemoveAt(i); } }
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
			{ byte ck = padK[sg]; if (ck != 0 && Math.Abs(padX[sg] - c.X) < padW[sg]) { if (ck == 1) c.Boost = Math.Max(c.Boost, 1.0f); else if (ck == 3) c.V = Math.Min(c.V, MAXV * 0.62f); } }
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
		int bs = SegOf(pos); float bp = (Wrap(pos) % SEG) / SEG; float camY = CAMH + Lerp(hill[bs], hill[(bs + 1) % N], bp) * 0 + HillY(PlayerZ) + airH * 4f;
		float x = 0f, dx = -curve[bs] * bp, maxY = H; sps.Clear();
		Pj[] p1s = new Pj[DRAW]; float[] clip = new float[DRAW];
		for (int n = 0; n < DRAW; n++)
		{
			int i = (bs + n) % N; bool looped = i < bs; float camZ = Wrap(pos) - (looped ? LEN : 0f);
			float y1 = HillY(i * SEG), y2 = HillY((i + 1) * SEG);
			Pj a = Proj(0, y1, i * SEG, px * ROADW - x, camY, camZ), b = Proj(0, y2, (i + 1) * SEG, px * ROADW - x - dx, camY, camZ);
			x += dx; dx += curve[i]; p1s[n] = a; clip[n] = maxY;
			if (i * SEG - camZ <= DEPTH || b.Y >= maxY || b.Y >= a.Y - 0.01f) continue;
			rows.Add(new Row { A = a, B = b, I = i });
			maxY = b.Y;
		}
		// 먼 칸부터 가까운 칸 순서로 그려요. 칸 경계는 정수 픽셀에 맞추고 길은 위로 1픽셀 겹쳐서, 칸 사이로 잔디색 줄이 비치지 않게 해요
		for (int r = rows.Count - 1; r >= 0; r--)
		{
			Pj a = rows[r].A, b = rows[r].B; int i = rows[r].I; bool alt = (i / 3) % 2 == 0;
			float yb = (float)Math.Round(b.Y), ya = (float)Math.Round(a.Y); if (r == 0) ya = Math.Max(ya, a.Y);
			if (ya <= yb) continue;
			a.Y = ya; b.Y = yb;
			g.FillRectangle(alt ? grass1 : grass2, 0, yb, W, ya - yb);
			if (course != 0 && i >= 3) { RoadStyle(g, a, b, i, alt, t); if (padK[i] != 0) PadRow(g, a, b, i, t); continue; }
			Quad(g, alt ? rum1 : rum2, a.X, a.Y, a.W * 1.12f, b.X, b.Y, b.W * 1.12f);
			Quad(g, (i < 3) ? chk : (alt ? road1 : road2), a.X, a.Y, a.W, b.X, b.Y, b.W);
			if (i < 3) { for (int k = 0; k < 8; k++) if ((k + i) % 2 == 0) { float u0 = -1f + k * 0.25f; Quad(g, rum2, a.X + a.W * (u0 + 0.125f), a.Y, a.W * 0.125f, b.X + b.W * (u0 + 0.125f), b.Y, b.W * 0.125f); } }
			else if (alt) { Quad(g, lane, a.X - a.W * 0.33f, a.Y, a.W * 0.02f, b.X - b.W * 0.33f, b.Y, b.W * 0.02f); Quad(g, lane, a.X + a.W * 0.33f, a.Y, a.W * 0.02f, b.X + b.W * 0.33f, b.Y, b.W * 0.02f); }
			if (padK[i] != 0) PadRow(g, a, b, i, t);
		}
		rows.Clear();
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
		if (driftDir != 0) Sparks(g, 200f + driftDir * -38f, 548f - airH, charge, t);
		if (boostT > 0f) Flame(g, 200f, 556f - airH, t);
		if (airH > 0f) { float sw0 = 110f * Math.Max(0.4f, 1f - airH / 220f); using (SolidBrush sb0 = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(sb0, 200f - sw0 / 2f, 550f, sw0, 14f); bob -= airH; }
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
		if (el < 5f) Hint(g, course == 2 ? "빙판이라 미끄러워요! 핸들을 미리 꺾어요" : (course == 4 ? "중력이 약해요! 점프하면 오래 날아요" : "←→ 핸들 · 노란 화살표 발판은 부스트 · 줄무늬 발판은 점프"), 432, 10.5f, false, Math.Min(1f, (5f - el) * 2f));
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
			case 5:   // 중세: 엇갈려 쌓은 돌바닥 (벽돌 무늬) · 돌 경계석
				Span(g, alt ? Color.FromArgb(170, 170, 176) : Color.FromArgb(120, 120, 128), a, b, -1.1f, 1.1f);
				{ int row = i / 2; float off = row % 2 == 0 ? 0f : 0.14f; for (int k = -1; k < 8; k++) { float u0 = -1f + k * 0.286f + off, u1 = u0 + 0.272f; if (u1 <= -1f || u0 >= 1f) continue; float h = Hash(row, k); Span(g, h < 0.33f ? r1 : (h < 0.66f ? r2 : Sh(r1, 14)), a, b, Math.Max(-1f, u0), Math.Min(1f, u1)); } }
				break;
			case 6:   // 요정: 분홍·보라·민트 파스텔 줄무늬 길 · 꽃 경계
				Span(g, alt ? Color.FromArgb(255, 255, 255) : Color.FromArgb(255, 186, 220), a, b, -1.12f, 1.12f);
				{ Color[] pc = { Color.FromArgb(255, 206, 228), Color.FromArgb(222, 206, 255), Color.FromArgb(200, 244, 226), Color.FromArgb(255, 240, 200) }; Span(g, pc[(i / 4) % 4], a, b, -1f, 1f); }
				if (Hash(i, 13) < 0.2f) { float u = Hash(i, 14) * 1.6f - 0.8f; Span(g, Color.FromArgb(180, 255, 255, 255), a, b, u - 0.03f, u + 0.03f); }   // 반짝
				break;
			case 7:   // 일본: 큼직한 판석을 깐 돌길 · 자갈 가장자리
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
		Txt(g, "코스를 골라요", 17f, 2, Color.White, 0, 46, W, 30, StringAlignment.Center);
		Txt(g, "카드를 누르거나 숫자 1~9", 9f, 1, Color.FromArgb(180, 220, 235, 255), 0, 72, W, 16, StringAlignment.Center);
		for (int k = 0; k < Th.Length; k++)
		{
			Thm th = Th[k]; RectangleF r = CardR(k); bool hv = k == hovCard; if (hv) r.Inflate(3, 3);
			using (GraphicsPath gp = Gfx.Round(r, 14))
			{
				GraphicsState gs = g.Save(); g.SetClip(gp);
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, 70), th.SkyA, th.SkyB, 90f)) g.FillRectangle(lb, r.X, r.Y, r.Width, 70);
				using (SolidBrush b = new SolidBrush(th.HillN)) g.FillEllipse(b, r.X - 20, r.Y + 52, r.Width * 0.8f, 40); using (SolidBrush b = new SolidBrush(th.HillF)) g.FillEllipse(b, r.X + r.Width * 0.4f, r.Y + 50, r.Width * 0.8f, 40);
				using (SolidBrush b = new SolidBrush(th.G1)) g.FillRectangle(b, r.X, r.Y + 68, r.Width, r.Height - 68);
				using (SolidBrush b = new SolidBrush(th.R1)) g.FillPolygon(b, new PointF[] { new PointF(r.X + r.Width / 2f - 6, r.Y + 68), new PointF(r.X + r.Width / 2f + 6, r.Y + 68), new PointF(r.Right - 8, r.Y + 112), new PointF(r.X + 8, r.Y + 112) });
				using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 20, 40))) g.FillRectangle(b, r.X, r.Y + 104, r.Width, r.Height - 104);
				g.Restore(gs);
				using (Pen p = new Pen(hv ? Color.FromArgb(255, 255, 220, 110) : Color.FromArgb(110, 255, 255, 255), hv ? 2.6f : 1.2f)) g.DrawPath(p, gp);
			}
			// 코스 모양 (위에서 본 고리)
			List<PointF> mp = courseMap[k]; if (mp != null && mp.Count > 2) { RectangleF mr = new RectangleF(r.X + r.Width - 50, r.Y + 6, 44, 40); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) using (GraphicsPath gp2 = Gfx.Round(mr, 8)) g.FillPath(b, gp2); DrawLoop(g, mp, mr, 1.6f); }
			Txt(g, (k + 1).ToString(), 10f, 2, Color.White, r.X + 6, r.Y + 4, 20, 18, StringAlignment.Near);
			Txt(g, th.Sub, 8.5f, 2, Color.FromArgb(255, 230, 140), r.X + 6, r.Y + 106, r.Width - 12, 14, StringAlignment.Near);
			Txt(g, th.Name, 9.5f, 2, Color.White, r.X + 6, r.Y + 119, r.Width - 12, 16, StringAlignment.Near);
			Txt(g, new string('★', th.Stars) + new string('☆', 3 - th.Stars) + "  " + th.Tags, 7f, 1, Color.FromArgb(220, 230, 240, 250), r.X + 6, r.Y + 135, r.Width - 10, 14, StringAlignment.Near);
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
				if (course == 5) { g.FillRectangle(b1, sx - w * 0.3f, a.Y - h * 0.8f, w * 0.6f, h * 0.8f); g.FillPolygon(b2, new PointF[] { new PointF(sx - w * 0.38f, a.Y - h * 0.8f), new PointF(sx, a.Y - h * 1.15f), new PointF(sx + w * 0.38f, a.Y - h * 0.8f) }); }
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
				if (course == 1) { g.FillRectangle(b1, sx - h * 0.07f, a.Y - h, h * 0.14f, h); g.FillRectangle(b1, sx - h * 0.25f, a.Y - h * 0.6f, h * 0.1f, h * 0.3f); g.FillRectangle(b1, sx + h * 0.15f, a.Y - h * 0.75f, h * 0.1f, h * 0.35f); g.FillRectangle(b1, sx - h * 0.25f, a.Y - h * 0.35f, h * 0.2f, h * 0.07f); g.FillRectangle(b1, sx + h * 0.05f, a.Y - h * 0.45f, h * 0.2f, h * 0.07f); }
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
				else if (course == 7) { g.FillRectangle(b3, sx - h * 0.06f, a.Y - h * 0.5f, h * 0.12f, h * 0.5f); g.FillRectangle(b3, sx - h * 0.18f, a.Y - h * 0.62f, h * 0.36f, h * 0.14f); using (SolidBrush lb3 = new SolidBrush(Color.FromArgb(255, 230, 150))) g.FillRectangle(lb3, sx - h * 0.08f, a.Y - h * 0.48f, h * 0.16f, h * 0.1f); }
				else g.FillEllipse(course == 6 ? b1 : b3, sx - h * 0.35f, a.Y - h * 0.4f, h * 0.7f, h * 0.45f);
			}
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
