using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 히든 게임: 마룡이 서바이벌 (몬스터를 잡고 → 경험치 → 레벨업 → 능력 3택1 → 조합하면 진화) ──

internal sealed class SurvivalGame : MiniGame
{
	// 이펙트: 그림 한 장을 늘이고·돌리고·옅게 해서 여러 모양으로 써요
	private class Vf { public string N, Tn, Fk; public int F0, Fn; public float X, Y, Vx, Vy, Rot, Spin, Age, Life, W0, W1, A, Sy; }
	private class Cp { public float X, Y, Age, H; public string Pre; public bool Flip; }
	private readonly List<Vf> vfs = new List<Vf>(); private readonly List<Cp> cps = new List<Cp>(); private float hitStop;
	private void V(string n, float x, float y, float w0, float w1, float life, float rot, float spin, float vx, float vy, float a, string tn)
	{
		if (vfs.Count > 90) vfs.RemoveAt(0);
		vfs.Add(new Vf { N = n, X = x, Y = y, W0 = w0, W1 = w1, Life = life, Rot = rot, Spin = spin, Vx = vx, Vy = vy, A = a, Tn = tn });
	}
	// 프레임 애니메이션 이펙트 (시트의 f0번째부터 n장을 수명 동안 재생)
	private void VA(string key, int f0, int n, float x, float y, float w, float life, float rot, float a, string tn)
	{
		if (vfs.Count > 90) vfs.RemoveAt(0);
		vfs.Add(new Vf { N = key + f0, Fk = key, F0 = f0, Fn = n, X = x, Y = y, W0 = w * 0.85f, W1 = w, Life = life, Rot = rot, A = a, Tn = tn });
	}
	private static bool PjA(Graphics g, string key, double t, float x, float y, float w, float rot)
	{
		int f = (int)(t * 14.0) & 3; Bitmap b = Spr.Get("ms_" + key + f); if (b == null) return false; Spr.Draw(g, "ms_" + key + f, x, y, w, w * b.Height / b.Width, rot); return true;
	}
	private float Rr() { return (float)Rnd.NextDouble(); }
	private void VfxTick(float dt)
	{
		for (int i = vfs.Count - 1; i >= 0; i--) { Vf v = vfs[i]; v.Age += dt; if (v.Age >= v.Life) { vfs.RemoveAt(i); continue; } v.X += v.Vx * dt; v.Y += v.Vy * dt; float dr = (float)Math.Pow(0.04, dt); v.Vx *= dr; v.Vy *= dr; v.Rot += v.Spin * dt; }
		for (int i = cps.Count - 1; i >= 0; i--) { cps[i].Age += dt; if (cps[i].Age > 1.5f) cps.RemoveAt(i); }
		foreach (En e in en) if (e.Sq > 0f) e.Sq = Math.Max(0f, e.Sq - dt * 6.5f);
	}
	// 무기마다 다른 맞는 효과 (기본 그림 한두 장을 크기·각도·색만 바꿔서 변주)
	private void HitFx(int src, float x, float y, float ang)
	{
		float deg = ang * 57.2958f; V("e_hit", x, y, 12f, 36f + Rr() * 10f, 0.13f, Rr() * 360f, 0f, 0f, 0f, 1f, null);
		switch (src)
		{
			case 0: if (Spr.Get("ms_wfire4") != null) VA("wfire", 4, 4, x, y, 56f, 0.34f, Rr() * 40f - 20f, 1f, null); else V("e_atk", x, y, 18f, 50f, 0.24f, Rr() * 360f, 60f, 0f, 0f, 0.95f, null); Fx.Burst(x - px + 200f, y - py + 330f, Color.FromArgb(255, 150, 50), 3); break;
			case 1: if (Spr.Get("ms_wbub4") != null) { VA("wbub", 4, 4, x, y, 46f, 0.3f, 0f, 1f, null); break; } for (int k = 0; k < 4; k++) { double a = Rr() * 6.28; V("pj1", x, y, 12f, 4f, 0.38f, 0f, 0f, (float)Math.Cos(a) * 80f, (float)Math.Sin(a) * 80f, 0.9f, null); } V("fx_hit", x, y, 14f, 34f, 0.14f, Rr() * 360f, 0f, 0f, 0f, 0.8f, "frost"); break;
			case 2: if (Spr.Get("ms_wlig4") != null) VA("wlig", 4, 4, x, y - 4f, 78f, 0.38f, 0f, 1f, null); else V("sve1", x, y - 6f, 38f, 74f, 0.3f, 0f, 0f, 0f, 0f, 0.95f, null); break;
			case 3: if (Spr.Get("ms_wboo4") != null) { VA("wboo", 4, 4, x, y, 52f, 0.3f, deg, 1f, null); if (Spr.Get("ms_wsl0") != null) VA("wsl", 0, 3, x, y, 60f, 0.26f, deg + 30f, 0.95f, null); break; } V(Rr() < 0.5f ? "fx_sw1" : (Rr() < 0.5f ? "fx_sw2" : "fx_sw3"), x, y, 30f, 66f, 0.17f, deg + 40f + Rr() * 60f, 0f, 0f, 0f, 0.95f, null); break;
			case 4: Fx.Burst(x - px + 200f, y - py + 330f, Color.FromArgb(200, 150, 255), 2); break;
			case 5: if (Spr.Get("ms_wsta4") != null) VA("wsta", 4, 4, x, y, 64f, 0.34f, 0f, 1f, null); else V("fx_hit", x, y, 24f, 60f, 0.18f, Rr() * 360f, 0f, 0f, 0f, 0.9f, "gold"); break;
			case 6: if (Spr.Get("ms_wpla4") != null) { VA("wpla", 4, 4, x, y, 54f, 0.34f, 0f, 1f, null); break; } V("sve3", x, y, 22f, 46f, 0.28f, Rr() * 360f, 90f, 0f, 0f, 0.9f, null); for (int k = 0; k < 2; k++) { double a = Rr() * 6.28; V("pj6", x, y, 12f, 7f, 0.4f, Rr() * 360f, 400f, (float)Math.Cos(a) * 90f, (float)Math.Sin(a) * 90f, 0.95f, null); } break;
			case 7: if (Spr.Get("ms_wcof4") != null) VA("wcof", 4, 4, x, y, 50f, 0.4f, 0f, 1f, null); else V("slime", x, y, 14f, 34f, 0.26f, Rr() * 360f, 0f, 0f, 0f, 0.9f, "coffee"); break;
			case 8: if (Spr.Get("ms_wstp4") != null) VA("wstp", 4, 4, x, y, 36f, 0.24f, 0f, 1f, null); V("fx_hit", x, y, 10f, 28f, 0.12f, Rr() * 360f, 0f, 0f, 0f, 0.9f, null); Fx.Burst(x - px + 200f, y - py + 330f, Color.FromArgb(220, 230, 255), 3); break;
			case 9: if (Spr.Get("ms_wlas24") != null) VA("wlas2", 4, 4, x, y, 40f, 0.26f, 0f, 1f, null); else V("fx_hit", x, y, 10f, 28f, 0.12f, Rr() * 360f, 0f, 0f, 0f, 0.9f, "pink"); break;
		}
	}
	private void Trail(Pr p)
	{
		float ang = (float)(Math.Atan2(p.Vy, p.Vx) * 57.2958);
		if (p.Kind == 0) { V("pj0", p.X, p.Y, evo[0] ? 40f : 32f, 6f, 0.2f, ang, 0f, 0f, 0f, 0.6f, null); if (Rr() < 0.4f) Fx.Burst(p.X - px + 200f, p.Y - py + 330f, Color.FromArgb(255, 150, 50), 1); }
		else if (p.Kind == 2) V("e_jump", p.X, p.Y, 8f, 20f, 0.36f, Rr() * 360f, 0f, 0f, 0f, 0.5f, null);
		else if (p.Kind == 3) V("pj8", p.X, p.Y, 26f, 10f, 0.14f, ang, 0f, 0f, 0f, 0.45f, null);
		else V("pj3", p.X, p.Y, p.R * 4f, p.R * 3f, 0.2f, p.Age * 720f, 0f, 0f, 0f, 0.35f, null);
	}
	private class En { public float X, Y, Hp, MaxHp, Sp, R, Hit, Pcd, Ocd, Fz, Ph, Tm, Dx, Dy, Sp2, Ph3, Ang, Sq, Rx, Ry; public int T, Dmg, Pat, PatI, Bi; public bool Elite, IsBoss; }
	private class Ep { public float X, Y, Vx, Vy, R, Dmg, Life; public int Kind; }
	private class St { public float X, Y, T, R, Dmg, Fl; public bool Hit; }
	private class Pr { public float X, Y, Vx, Vy, Dmg, Life, R, Age, Tk; public int Kind, Pierce; public bool Back; }
	private class Gm { public float X, Y; public int V; }
	private class Pk { public float X, Y, T; public int K; }
	private class Bolt { public float X1, Y1, X2, Y2, T; public bool Big; }

	private const float WinT = 300f;
	private static readonly string[] WName = { "마룡 불꽃", "회전 방울", "번개", "마룡 부메랑", "수호 기운", "도장 쾅", "서류 비행기", "커피 웅덩이", "스테이플러", "레이저 포인터" };
	private static readonly string[] WDesc = { "가까운 민원에게 불꽃을 쏴요", "몸 주위를 도는 방울이 부딪혀요", "번개가 민원을 내리쳐요", "던졌다가 돌아오며 관통해요", "주변 민원에게 계속 피해를 줘요", "도장을 내리찍어 범위 피해를 줘요", "서류가 비행기가 되어 민원을 쫓아가요", "엎지른 커피가 민원을 느리게 하고 계속 아프게 해요", "가까운 민원에게 철침을 부채꼴로 쏴요", "빛줄기가 몸 주위를 빙글빙글 훑어요" };
	private static readonly Color[] WCol = { Color.FromArgb(255, 140, 60), Color.FromArgb(120, 200, 255), Color.FromArgb(255, 235, 90), Color.FromArgb(150, 255, 160), Color.FromArgb(200, 140, 255), Color.FromArgb(235, 70, 80), Color.FromArgb(205, 225, 255), Color.FromArgb(200, 130, 80), Color.FromArgb(240, 220, 120), Color.FromArgb(255, 90, 140) };
	private static readonly string[] PName = { "튼튼한 몸", "빠른 발", "자석 손", "힘이 불끈", "정시퇴근 수당", "철밥통", "복권 당첨", "넓은 시야", "영양제", "자기계발", "연차 휴가" };
	private static readonly string[] PDesc = { "최대 체력 +20 (바로 회복)", "이동 속도 +10%", "경험치를 더 멀리서 모아요", "모든 공격력 +15%", "모든 무기 재사용 속도 +8%", "받는 피해 -8%", "치명타 확률 +5% (치명타는 2배)", "장판·빛줄기·웅덩이 범위 +12%", "체력이 초당 +0.5씩 더 회복돼요", "경험치 +12% 더 얻어요", "쓰러져도 체력 50%로 한 번 부활해요 (최대 2번)" };
	private static readonly string[] EName = { "화염 고리", "뇌전 폭풍", "불칼 회오리", "서류 폭풍", "에스프레소 샷", "프리즘 레이저" };
	private static readonly string[] EDesc = { "방울이 불타올라 훨씬 강해지고 늘어나요", "번개가 여러 곳에 동시에 떨어져 폭발해요", "부메랑이 3갈래로 날며 전부 관통해요", "비행기가 사방으로 쏟아지고 더 세게 관통해요", "웅덩이가 커지고 사라질 때 폭발해요", "레이저가 3갈래로 갈라져 길게 훑어요" };
	// 진화 조건 {종류, 무기, 짝}: 종류 0 = 무기 + 능력(패시브) · 1 = 무기 + 무기(히든 포함)
	private static readonly int[,] EReq = { { 0, 1, 3 }, { 1, 2, 5 }, { 1, 3, 0 }, { 0, 6, 9 }, { 0, 7, 8 }, { 0, 9, 7 } };
	private const int MaxW = 6, MaxP = 5;   // 한 판에 가질 수 있는 무기·능력 칸 (진화하면 두 개가 한 칸이 돼요)
	private readonly bool[] wCons = new bool[10], pCons = new bool[11];
	private bool EvoReady(int e) { return !evo[e] && wl[EReq[e, 1]] >= MaxLv && (EReq[e, 0] == 0 ? pl[EReq[e, 2]] >= MaxLv : wl[EReq[e, 2]] >= MaxLv); }
	private void DoEvo(int e) { evo[e] = true; wl[EReq[e, 1]]++; if (EReq[e, 0] == 0) { pl[EReq[e, 2]]++; pCons[EReq[e, 2]] = true; } else { wl[EReq[e, 2]]++; wCons[EReq[e, 2]] = true; } }
	private int WSlots() { int n = 0; for (int i = 0; i < 10; i++) if (wl[i] > 0 && !wCons[i]) n++; return n; }
	private int PSlots() { int n = 0; for (int i = 0; i < 11; i++) if (pl[i] > 0 && !pCons[i]) n++; return n; }
	private bool WOk(int i) { return wl[i] < MaxLv && (wl[i] > 0 || WSlots() < MaxW); }
	private bool POk(int i) { return pl[i] < (i == 10 ? 2 : MaxLv) && (pl[i] > 0 || PSlots() < MaxP); }
	private static readonly string[] RName = { "승진 임명장 (공격력 +25%)", "구내식당 정식 (체력 가득)", "처리 완료 도장 (경험치 모두 흡수)", "정시퇴근 시계 (민원 4초 정지)", "결재 도장 (화면 폭발)", "해외연수 티켓 (즉시 레벨 업)", "특근수당 (스트레스 가득)", "공로패 (최대 체력 +30%)" };

	private readonly List<En> en = new List<En>(); private readonly List<Pr> pr = new List<Pr>(); private readonly List<Gm> gm = new List<Gm>(); private readonly List<Pk> pk = new List<Pk>(); private readonly List<Bolt> bolts = new List<Bolt>(); private readonly List<Ep> eps = new List<Ep>(); private readonly List<St> stp = new List<St>();
	private readonly bool[] bossSeen = new bool[4]; private float cdS, eliteT; private int waveN, bossKills; private static readonly string[][] BNames = {
		new[] { "악성 민원왕", "민원 대표 이장님", "취재 기자단", "호통 대마왕" },
		new[] { "질의 의원", "상임위원장", "예결위원장", "의장님" },
		new[] { "갑질 상사", "야근 강요 상사", "결재 반려 대마왕", "끝판왕 갑질 상사" },
		new[] { "현장소장", "시공업체 대표", "트집 잡는 점검 업체", "시위대장" },
		new[] { "피해 주민 대표", "책임 떠넘기기 달인", "언론 취재단", "호통 치는 현장 책임자" },
		new[] { "행사 대행사", "무대 감독", "축제 위원장", "VIP 의전 대마왕" },
		new[] { "현지 통역관", "바가지 상인", "국제회의 의장", "사막의 대마왕" },
		new[] { "제설 담당관", "빙판 민원인", "한파 대책 위원장", "눈보라 대마왕" } };
	private static string[] BName { get { return BNames[stage]; } }
	private static readonly string[] StName = { "민원대", "의회", "사무실", "현장", "재해복구", "축제현장", "해외출장", "제설작전" };
	private static readonly string[] StDesc = { "줄 선 민원인이 끝없이 몰려와요", "방패 든 의원들과 질의 공세", "서류 더미가 쌓이고 쌓여요", "돌진하는 민원인과 공사 소음", "무너진 잔해가 하늘에서 떨어져요", "폭죽이 터지는 인파 속 대혼잡", "뜨거운 모래바람 속 출장 민원", "눈보라 속 얼어붙은 민원인들" };
	private static readonly Color[] StCol = { Color.FromArgb(150, 168, 190), Color.FromArgb(118, 54, 62), Color.FromArgb(128, 132, 142), Color.FromArgb(58, 112, 70), Color.FromArgb(84, 78, 72), Color.FromArgb(38, 36, 78), Color.FromArgb(196, 170, 112), Color.FromArgb(170, 196, 220) };
	private static readonly float[] StHp = { 1f, 1.05f, 1.1f, 1.14f, 1.22f, 1.24f, 1.3f, 1.34f };
	private static readonly int[][] BPat = { new[] { 0, 3, 1 }, new[] { 1, 2, 1, 2 }, new[] { 4, 0, 3 }, new[] { 0, 1, 4, 2, 3 } };
	private readonly int[] wl = new int[10]; private readonly int[] pl = new int[11]; private readonly bool[] evo = new bool[6];
	private static int cleared;   // 클리어한 근무지 수 (0이면 민원대만 열림)
	private static string ProgPath { get { return System.IO.Path.Combine(Program.DataDir, "survive_progress.txt"); } }
	private static void LoadProg() { try { if (System.IO.File.Exists(ProgPath)) { int v; if (int.TryParse(System.IO.File.ReadAllText(ProgPath).Trim(), out v)) cleared = Math.Max(cleared, Math.Min(8, v)); } } catch { } }
	private static void SaveProg() { try { System.IO.File.WriteAllText(ProgPath, cleared.ToString()); } catch { } }
	private static bool Open(int i) { return i <= cleared; }
	private static readonly string[] DName = { "입문", "쉬움", "보통", "어려움", "고수", "지옥" };
	private static readonly string[] DDesc = { "민원이 적고 약해요 · 처음 해보는 분", "여유롭게 구경하며 키워요", "기본 난이도", "민원이 늘고 단단해져요", "빽빽한 민원 · 아픈 한 방", "퇴근은 꿈도 못 꿔요" };
	private static readonly float[] DSpawn = { 0.55f, 0.75f, 1f, 1.2f, 1.45f, 1.8f }, DHp = { 0.7f, 0.85f, 1f, 1.15f, 1.32f, 1.6f }, DDmg = { 0.6f, 0.8f, 1f, 1.15f, 1.3f, 1.5f }, DStress = { 0.55f, 0.8f, 1f, 1.1f, 1.2f, 1.3f }, DScore = { 0.6f, 0.8f, 1f, 1.3f, 1.7f, 2.4f };
	private static readonly int[] DCap = { 30, 38, 46, 54, 62, 70 };
	private static int diff = 1;   // 난이도 0~5 (처음엔 쉬움)
	private static string DiffPath { get { return System.IO.Path.Combine(Program.DataDir, "survive_diff.txt"); } }
	private static bool diffLoaded;
	private static void LoadDiff() { if (diffLoaded) return; diffLoaded = true; try { int v; if (System.IO.File.Exists(DiffPath) && int.TryParse(System.IO.File.ReadAllText(DiffPath).Trim(), out v)) diff = Math.Max(0, Math.Min(5, v)); } catch { } }
	private static void SetDiff(int d) { diff = Math.Max(0, Math.Min(5, d)); try { System.IO.File.WriteAllText(DiffPath, diff.ToString()); } catch { } }
	private const int MaxLv = 5;   // 무기·능력 최대 레벨 (둘이 5레벨이 되면 진화)
	private static readonly string[] HName = { "간식 타임", "구내식당 정식", "건강검진 휴가" };
	private static readonly string[] HDesc = { "체력을 30% 회복해요", "체력을 55% 회복해요", "체력을 80% 회복해요" };
	private static readonly float[] HFrac = { 0.30f, 0.55f, 0.80f };
	private static int stage;   // 0 민원대 · 1 의회 · 2 사무실 · 3 현장 · 4 재해복구 · 5 축제현장
	private float healFx, skFx, hitFx, px, py, hp, maxHp, el, spawnT, relicT, heartT, waveT, hurtT, msgT, freezeT, dmgBonus, orbA, cdF, cdL, cdB, auraT, fx, fy;
	private float flashT; private int lvl, xp, kills, mode; private string msg = "";   // mode 0 플레이 · 1 능력 고르기
	private int[] opt = new int[3]; private int[] optK = new int[3];   // optK 0 무기 · 1 패시브 · 2 진화
	private bool won;
	private class Pu { public float X, Y, T, Life, R, Tk; }
	private class Hz { public float X, Y, T, R; public int K; public bool Done; public float Fl; }
	private class Dn { public float X, Y, Age; public int V; public bool Crit; }
	private readonly List<Pu> pud = new List<Pu>(); private readonly List<Hz> hzs = new List<Hz>(); private readonly List<Dn> dns = new List<Dn>();
	private int[] chK = new int[3], chV = new int[3], chLv = new int[3]; private int chN, chCue; private int lvSel; private float chT, lvAge; private int usedRev; private readonly int[] rel = new int[8]; private float stress, cdP, cdC, cdK, beamA, beamT, surgeT, hzT, clockT, dnT; private readonly bool[] evDone = new bool[6];

	public override bool NoLimit { get { return true; } }
	public override string Costume { get { return "g_knight"; } }
	private static System.Drawing.Imaging.ImageAttributes MkAttr(float[][] m) { System.Drawing.Imaging.ImageAttributes a = new System.Drawing.Imaging.ImageAttributes(); a.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(m)); return a; }
	private static readonly System.Drawing.Imaging.ImageAttributes FoeHit = MkAttr(new float[][] { new float[] { 0.4f, 0, 0, 0, 0 }, new float[] { 0, 0.4f, 0, 0, 0 }, new float[] { 0, 0, 0.4f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0.6f, 0.6f, 0.6f, 0, 1 } });
	private static readonly System.Drawing.Imaging.ImageAttributes FoeIce = MkAttr(new float[][] { new float[] { 0.5f, 0, 0, 0, 0 }, new float[] { 0, 0.7f, 0, 0, 0 }, new float[] { 0, 0, 1f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0.1f, 0.25f, 0.35f, 0, 1 } });
	public override string Name { get { return "마룡이 서바이벌"; } }
	public override string Key { get { return "surv"; } }
	public override string Desc { get { return "모든 게임을 해낸 당신에게!"; } }
	public override string Help { get { return "방향키·WASD (또는 마우스를 누른 채 끌기)로 움직여요 · 공격은 자동!\n먼저 근무지(민원대·의회·사무실·현장·재해복구·축제현장)를 골라요\n민원을 잡으면 경험치 구슬 → 레벨업 → 능력 3개 중 하나 선택(1·2·3 키도 돼요) · 무기는 최대 6개·능력은 최대 5개까지만 가질 수 있어요 · 무기와 능력을 5레벨까지 키워 짝을 맞추면 '진화' (한 칸만 차지해요, 무기끼리 진화하는 히든도 있어요)\n맞거나 잡으면 스트레스가 올라요 → 100%가 되면 스페이스/클릭으로 '퇴근 선언'(화면 정리!), 죽을 때도 한 번 살려줘요\n보스를 잡으면 상자가 나와요 (능력 1~3개, 3개면 대박!) · 월말 결산(경험치 2배)·점심시간(회복)·유물을 챙기고, 5분을 버티면 클리어!"; } }
	public override string RightText { get { int s = (int)el; return (s / 60) + ":" + (s % 60).ToString("00"); } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 160, 90))) g.FillEllipse(b, cx - 18, cy - 18, 36, 36);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 220, 90))) { g.FillEllipse(b, cx - 4, cy - 4, 8, 8); g.FillEllipse(b, cx - 14, cy + 4, 6, 6); g.FillEllipse(b, cx + 8, cy - 12, 6, 6); }
	}
	public override void Reset()
	{
		LoadProg(); LoadDiff(); usedRev = 0; Array.Clear(rel, 0, rel.Length);
		vfs.Clear(); cps.Clear(); hitStop = 0f; en.Clear(); eps.Clear(); stp.Clear(); pud.Clear(); hzs.Clear(); dns.Clear(); Array.Clear(evDone, 0, evDone.Length); stress = 0f; cdP = cdC = cdK = 0.5f; beamA = 0f; beamT = 0f; surgeT = 0f; hzT = 6f; clockT = 0f; dnT = 0f; Array.Clear(bossSeen, 0, 4); cdS = 0.5f; eliteT = 45f; waveN = 0; bossKills = 0; pr.Clear(); gm.Clear(); pk.Clear(); bolts.Clear(); Array.Clear(wl, 0, wl.Length); Array.Clear(pl, 0, pl.Length); Array.Clear(evo, 0, evo.Length); Array.Clear(wCons, 0, wCons.Length); Array.Clear(pCons, 0, pCons.Length);
		px = py = 0f; maxHp = 100f; hp = maxHp; el = 0f; spawnT = 1f; relicT = 28f; heartT = 22f; waveT = 38f; hurtT = 0f; msgT = 0f; freezeT = 0f; dmgBonus = 0f; orbA = 0f; cdF = cdL = cdB = 0.3f; auraT = 0f;
		lvl = 1; xp = 0; kills = 0; mode = 2; Score = 0; Over = false; won = false; msg = ""; wl[0] = 2; fx = 0f; fy = 1f;
	}
	private int XpNeed { get { return 3 + lvl * 3 + lvl * lvl / 8; } }
	private float Dm { get { return 1f + 0.15f * pl[3] + dmgBonus; } }
	private float Speed { get { return 118f * (1f + 0.10f * pl[1]); } }
	private float Magnet { get { return 56f * (1f + 0.35f * pl[2]); } }

	// ── 입력 ─────────────────────────────────
	public override void Click(float x, float y)
	{
		if (Over) return;
		if (mode == 3) { ChestClose(); return; }
		if (mode == 2) { for (int i = 0; i < 6; i++) if (DfRect(i).Contains(x, y)) { SetDiff(i); Sfx.Play("tick"); return; } for (int i = 0; i < 8; i++) if (StRect(i).Contains(x, y)) { if (!Open(i)) { Sfx.Play("bad2"); return; } stage = i; mode = 0; Sfx.Play("ok2"); Say(StName[i] + " 근무 시작! (" + DName[diff] + ")"); return; } return; }
		if (mode == 1) { for (int i = 0; i < 3; i++) if (CardRect(i).Contains(x, y)) { Pick(i); return; } }
		else if (mode == 0 && stress >= 100f) ClockOut(false);
	}
	public override void Press(Keys k)
	{
		if (mode == 3) { if (k == Keys.Space || k == Keys.Enter || k == Keys.Return) ChestClose(); return; }
		if (mode == 2) { if (k == Keys.Left) { SetDiff(diff - 1); Sfx.Play("tick"); } else if (k == Keys.Right) { SetDiff(diff + 1); Sfx.Play("tick"); } else if (k >= Keys.D1 && k <= Keys.D8) { if (!Open(k - Keys.D1)) { Sfx.Play("bad2"); return; } stage = k - Keys.D1; mode = 0; Sfx.Play("ok2"); Say(StName[stage] + " 근무 시작! (" + DName[diff] + ")"); } return; }
		if (mode == 0 && k == Keys.Space) { ClockOut(false); return; }
		if (mode != 1) return;
		if (k == Keys.Left || k == Keys.A) { lvSel = (lvSel + 2) % 3; Sfx.Play("tick"); return; }
		if (k == Keys.Right || k == Keys.D) { lvSel = (lvSel + 1) % 3; Sfx.Play("tick"); return; }
		if ((k == Keys.Space || k == Keys.Enter) && lvAge > 0.35f) { Pick(lvSel); return; }
		if (k == Keys.D1 || k == Keys.NumPad1) Pick(0); else if (k == Keys.D2 || k == Keys.NumPad2) Pick(1); else if (k == Keys.D3 || k == Keys.NumPad3) Pick(2);
	}
	private static RectangleF StRect(int i) { return new RectangleF(14 + (i % 2) * 194, 166 + (i / 2) * 106, 178, 98); }
	private static RectangleF DfRect(int i) { return new RectangleF(14 + i * 62.4f, 114f, 58f, 30f); }
	private static RectangleF CardRect(int i) { return new RectangleF(14 + i * 129, 150, 121, 300); }

	// ── 레벨업 선택지 ────────────────────────
	private void RollOptions()
	{
		List<int[]> pool = new List<int[]>();
		for (int i = 0; i < 10; i++) if (WOk(i) && !EvoUsed(i)) pool.Add(new int[] { 0, i });
		for (int i = 0; i < 11; i++) if (POk(i)) pool.Add(new int[] { 1, i });
		int n = 0;
		for (int e = 0; e < 6 && n < 3; e++) if (EvoReady(e)) { opt[n] = e; optK[n] = 2; n++; }
		int guard = 0;
		while (n < 3 && pool.Count > 0 && guard++ < 50)
		{
			int k = Rnd.Next(pool.Count); int[] c = pool[k]; pool.RemoveAt(k);
			opt[n] = c[1]; optK[n] = c[0]; n++;
		}
		while (n < 3) { opt[n] = n; optK[n] = 3; n++; }   // 더 키울 게 없으면 체력 회복만 나와요
		mode = 1; lvAge = 0f; lvSel = 0; Sfx.Play("ok2");
	}
	private void Pick(int i)
	{
		int k = optK[i], v = opt[i];
		if (k == 0) wl[v]++;
		else if (k == 3) { hp = Math.Min(maxHp, hp + maxHp * HFrac[v]); healFx = 1f; Say(HName[v] + "! 체력 회복"); }
		else if (k == 1) { pl[v] = Math.Min(v == 10 ? 2 : 5, pl[v] + 1); if (v == 0) { maxHp += 20f; hp = Math.Min(maxHp, hp + 20f); } }
		else { DoEvo(v); Sfx.Play("fanfare"); Shake = 0.4f; Say("진화! " + EName[v]); }
		mode = 0; Sfx.Play("ok");
		if (xp >= XpNeed) LevelUp();
	}
	private void LevelUp()
	{
		xp -= XpNeed; lvl++; hp = Math.Min(maxHp, hp + 8f); RollOptions();
	}
	private bool EvoUsed(int w) { return false; }
	private void Say(string s) { msg = s; msgT = 2.4f; }

	// ── 진행 ─────────────────────────────────
	// 그림 미리 읽기: 시작 직후 한 번 끊기지 않도록 프레임마다 몇 장씩 나눠서 읽어 둬요
	private static bool drawWarm; private static List<string> warmQ; private static int warmI;
	private static void WarmStep()
	{
		if (warmQ == null)
		{
			warmQ = new List<string>(); string[] pre = { "ms_op", "ms_opa", "ms_opb", "ms_opc", "ms_opd", "ms_ops", "ms_opx", "ms_edoc", "ms_emg", "ms_ecu", "ms_ebm", "ms_shg" };
			for (int sg = 2; sg < 8; sg++) { warmQ.Add("ms_s" + sg + "a0"); warmQ.Add("ms_s" + sg + "b0"); warmQ.Add("ms_s" + sg + "x0"); }
			foreach (string p in pre) for (int i = 0; i < 4; i++) warmQ.Add(p + i);
			string[] wp = { "wfire", "wlig", "wboo", "wpla", "wsta", "wbub", "waur", "wcof", "wstp", "wlas2", "wexp" };
			foreach (string p in wp) for (int i = 0; i < 8; i++) warmQ.Add("ms_" + p + i);
			for (int i = 0; i < 3; i++) warmQ.Add("ms_wsl" + i);
			for (int i = 0; i < 6; i++) { warmQ.Add("ms_zad" + i); warmQ.Add("ms_wkf" + i); warmQ.Add("ms_wks" + i); warmQ.Add("ms_wkb" + i); }
		}
		for (int k = 0; k < 6 && warmI < warmQ.Count; k++, warmI++) Spr.Get(warmQ[warmI]);
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		hotP = new PointF(inp.Mx, inp.My);
		WarmStep(); if (!drawWarm && warmQ != null && warmI >= warmQ.Count) { drawWarm = true; try { using (Bitmap wb = new Bitmap(16, 16)) using (Graphics wg = Graphics.FromImage(wb)) Draw(wg, t); } catch { } }
		if (Over) return;
		if (mode == 1) { lvAge += Math.Min(dt, 0.05f); if (inp.MouseMoved) for (int ci = 0; ci < 3; ci++) if (CardRect(ci).Contains(inp.Mx, inp.My)) lvSel = ci; return; }
		if (mode == 3) { ChestTick(Math.Min(dt, 0.05f)); return; }
		if (mode == 2) return;
		if (dt > 0.05f) dt = 0.05f;
		VfxTick(dt); if (hitStop > 0f) { hitStop -= dt; return; }
		if (flashT > 0f) flashT -= dt;
		el += dt; if (healFx > 0) healFx -= dt * 1.2f; if (skFx > 0) skFx -= dt * 1.5f; if (hitFx > 0) hitFx -= dt * 4f; if (msgT > 0) msgT -= dt; if (hurtT > 0) hurtT -= dt; if (freezeT > 0) freezeT -= dt;
		Move(dt, inp);
		Spawn(dt);
		Weapons(dt);
		Enemies(dt);
		Projectiles(dt);
		EnemyShots(dt);
		Pickups(dt);
		Hazards(dt); if (dnT > 0f) dnT -= dt; if (clockT > 0f) clockT -= dt; if (surgeT > 0f) surgeT -= dt;
		for (int i = dns.Count - 1; i >= 0; i--) { dns[i].Age += dt; dns[i].Y -= 38f * dt; if (dns[i].Age > 0.7f) dns.RemoveAt(i); }
		for (int i = pud.Count - 1; i >= 0; i--) { Pu u = pud[i]; u.T += dt; u.Tk -= dt; if (u.Tk <= 0f) { u.Tk = 0.35f; float dm2 = 5f + 2.2f * wl[7]; for (int j = 0; j < en.Count; j++) { En e = en[j]; float dx = e.X - u.X, dy = e.Y - u.Y, rr = u.R + e.R * 0.6f; if (dx * dx + dy * dy < rr * rr) { Hurt(e, dm2, 7, (float)Math.Atan2(dy, dx)); e.Fz = 0.5f; } } } if (u.T >= u.Life) { if (evo[4]) { float er = u.R * 1.25f, ed = 60f + 14f * wl[7]; for (int j = 0; j < en.Count; j++) { En e = en[j]; float dx = e.X - u.X, dy = e.Y - u.Y, rr = er + e.R; if (dx * dx + dy * dy < rr * rr) Hurt(e, ed); } Fx.Burst(u.X - px + 200f, u.Y - py + 330f, Color.FromArgb(200, 130, 70), 8); Sfx.Play("thud_soft"); } pud.RemoveAt(i); } }
		for (int i = bolts.Count - 1; i >= 0; i--) { bolts[i].T -= dt; if (bolts[i].T <= 0) bolts.RemoveAt(i); }
		if (el >= WinT && !won) { won = true; Over = true; if (stage + 1 > cleared) { cleared = Math.Min(8, stage + 1); SaveProg(); } Say(stage < 7 ? StName[stage] + " 클리어! 다음 근무지 열림" : "모든 근무지 클리어! 전설의 공무원!"); Sfx.Play("fanfare"); }
		Score = (int)((kills * 5 + bossKills * 300 + lvl * 40 + (int)el * 3 + (won ? 1000 : 0)) * DScore[diff]);
		if (hp <= 0f && stress >= 100f) { hp = 1f; ClockOut(true); }
		else if (hp <= 0f && usedRev < pl[10]) { usedRev++; hp = maxHp * 0.5f; hurtT = 2.5f; flashT = 1.2f; Shake = 0.8f; eps.Clear(); for (int j = 0; j < en.Count; j++) { En e = en[j]; float dx = e.X - px, dy = e.Y - py, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f; if (l < 240f) { e.X += dx / l * (240f - l); e.Y += dy / l * (240f - l); } } Say("연차 사용! 다시 출근해요 (" + (pl[10] - usedRev) + "회 남음)"); Sfx.Play("fanfare"); }
		else if (hp <= 0f) { hp = 0f; Over = true; Sfx.Play("fall"); Shake = 0.6f; }
	}
	private void Move(float dt, GameInput inp)
	{
		float dx = 0, dy = 0; HashSet<Keys> d = inp.Down;
		if (d != null)
		{
			if (d.Contains(Keys.Left) || d.Contains(Keys.A)) dx -= 1; if (d.Contains(Keys.Right) || d.Contains(Keys.D)) dx += 1;
			if (d.Contains(Keys.Up) || d.Contains(Keys.W)) dy -= 1; if (d.Contains(Keys.Down) || d.Contains(Keys.S)) dy += 1;
		}
		if (dx == 0 && dy == 0 && inp.Hold && inp.MouseMoved) { float mx = inp.Mx - 200f, my = inp.My - 330f, ml = (float)Math.Sqrt(mx * mx + my * my); if (ml > 14f) { dx = mx / ml; dy = my / ml; } }
		float l = (float)Math.Sqrt(dx * dx + dy * dy);
		if (l > 0f) { dx /= l; dy /= l; px += dx * Speed * dt; py += dy * Speed * dt; PushOut(ref px, ref py, 10f); fx = dx; fy = dy; walking = true; walkPh += dt * 10f; }
		else walking = false;
		if (hp < maxHp && hp > 0f) hp = Math.Min(maxHp, hp + (0.4f + 0.5f * pl[8]) * dt);
	}
	private void Spawn(float dt)
	{
		spawnT -= dt;
		// 화면에 있는 적의 수를 목표치에 맞춰 조절해요 (너무 많이 쌓이지도, 텅 비지도 않게). 시간이 갈수록 목표치는 천천히 늘고, 대신 적이 더 단단해져요
		int tgtN = Math.Min(DCap[diff], 12 + (int)(el * 0.13f * (float)Math.Sqrt(DSpawn[diff]))); if (surgeT > 0f) tgtN = (int)(tgtN * 1.3f);
		if (spawnT <= 0f)
		{
			spawnT = 0.42f / (float)Math.Sqrt(DSpawn[diff]); int deficit = tgtN - en.Count;
			if (deficit > 0) { int n = Math.Min(deficit, 1 + Math.Min(2, (int)(el / 90f))); for (int i = 0; i < n; i++) SpawnOne(); }
		}
		for (int i = 0; i < en.Count; i++) { En e = en[i]; if (e.IsBoss || e.Elite) continue; float dx = e.X - px, dy = e.Y - py; if (dx * dx + dy * dy > 640f * 640f) { double a2 = Math.Atan2(dy, dx) + 3.14159 + (Rnd.NextDouble() - 0.5) * 1.2; e.X = px + (float)Math.Cos(a2) * 360f; e.Y = py + (float)Math.Sin(a2) * 360f; } }
		waveT -= dt;
		if (waveT <= 0f)
		{
			waveT = 38f; int wk = waveN++ % 3;
			int[,] wt = { { 0, 4, 5 }, { 7, 4, 1 }, { 6, 0, 2 }, { 5, 6, 2 }, { 2, 8, 1 }, { 8, 1, 4 }, { 2, 8, 5 }, { 1, 2, 8 } };
			string[,] wm = { { "줄 선 민원인들이 몰려와요!", "악성 민원 폭주!", "번호표 뽑고 돌진!" }, { "야유 폭탄! 서류가 날아와요!", "규정 인용 공세!", "질의 시간이 시작됐어요!" }, { "결재 서류가 산더미!", "회의 소집! 줄줄이 몰려와요", "감사 나왔습니다!" }, { "공사 차량 돌진!", "자재가 쏟아져요", "현장 점검 나왔어요!" }, { "잔해 속 주민들이 몰려와요", "연쇄 폭발 주의!", "구호 요청이 쏟아져요" }, { "폭죽 인파가 몰려와요!", "공연 앞 대혼잡!", "VIP 행차! 길을 비켜요" }, { "모래바람 속 민원인 행렬!", "바가지 상인 습격!", "사막 열기에 지친 인파" }, { "눈보라 속 민원인 습격!", "빙판에 미끄러져 돌진!", "한파 대책 요구 폭주!" } };
			int ty2 = wt[stage, wk]; if ((ty2 == 7 && el < 40f) || (ty2 == 2 && el < 30f) || (ty2 == 5 && el < 20f)) ty2 = 0;
			Say(wm[stage, wk]); Ring(ty2, ty2 == 0 || ty2 == 1 ? 16 : (ty2 == 2 || ty2 == 7 ? 6 : 9), 340f); if (wk == 2) Ring(ty2 == 1 ? 0 : 1, 10, 380f);
		}
		float[] evAt = { 75f, 115f, 150f, 200f, 225f, 255f };   // 월말 결산 3번 · 점심시간 2번 · 마지막 돌격 1번
		for (int ev = 0; ev < 6; ev++) if (!evDone[ev] && el >= evAt[ev])
		{
			evDone[ev] = true;
			if (ev == 0 || ev == 2 || ev == 4) { surgeT = 22f; Say("월말 결산! 민원이 폭주해요 (경험치 2배)"); Sfx.Play("bad_big"); }
			else if (ev == 1 || ev == 3) { Say("점심시간! 믹스커피가 쏟아져요"); for (int h = 0; h < 4; h++) { double a = h * Math.PI / 2 + Rnd.NextDouble(); pk.Add(new Pk { X = px + (float)Math.Cos(a) * 120f, Y = py + (float)Math.Sin(a) * 120f, K = 0 }); } Sfx.Play("ok2"); }
			else { Say("마감 직전! 마지막 민원이 몰려와요!"); Ring(stage >= 4 ? 8 : 5, 14, 330f); Ring(1, 20, 380f); }
		}
		for (int b = 0; b < 4; b++) if (!bossSeen[b] && el >= 60f * (b + 1)) { bossSeen[b] = true; AddBoss(b); flashT = 1.2f; Sfx.Play("bad_big"); }
		eliteT -= dt;
		if (eliteT <= 0f && el > 45f) { eliteT = 48f; double ea = Rnd.NextDouble() * Math.PI * 2; AddEn(Rnd.Next(2) == 0 ? 2 : 7, px + (float)Math.Cos(ea) * 340f, py + (float)Math.Sin(ea) * 340f, true); Say("갑질 상사가 나타났어요!"); }
		relicT -= dt; if (relicT <= 0f) { relicT = 38f; double a = Rnd.NextDouble() * Math.PI * 2; float r = 260f + (float)Rnd.NextDouble() * 160f; pk.Add(new Pk { X = px + (float)Math.Cos(a) * r, Y = py + (float)Math.Sin(a) * r, K = 10 + Rnd.Next(8), T = 0 }); Say("지도 어딘가에 유물이 나타났어요!"); }
		heartT -= dt; if (heartT <= 0f) { heartT = 24f; double a = Rnd.NextDouble() * Math.PI * 2; float r = 140f + (float)Rnd.NextDouble() * 140f; pk.Add(new Pk { X = px + (float)Math.Cos(a) * r, Y = py + (float)Math.Sin(a) * r, K = 0, T = 0 }); }
	}
	private void Ring(int ty, int n, float rad) { n = Math.Min(n, Math.Max(0, DCap[diff] + 6 - en.Count)); for (int i = 0; i < n; i++) { double a = Math.PI * 2 * i / n; AddEn(ty, px + (float)Math.Cos(a) * rad, py + (float)Math.Sin(a) * rad, false); } }
	private void SpawnOne()
	{
		double a = Rnd.NextDouble() * Math.PI * 2; float r = 330f + (float)Rnd.NextDouble() * 60f; double q = Rnd.NextDouble();
		int ty = 0;
		if (el > 130f && q > 0.93) ty = 7; else if (el > 90f && q > 0.85) ty = 6; else if (el > 70f && q > 0.75) ty = 5; else if (el > 40f && q > 0.65) ty = 4;
		else if (el > 55f && q > 0.57) ty = 2; else if (el > 22f && q < 0.26) ty = 1;
		if (stage == 1) { if (el > 30f && q > 0.9) ty = 7; else if (el > 18f && q > 0.68) ty = 4; }
		else if (stage == 2) { if (el > 25f && q > 0.74) ty = 6; else if (el > 70f && q > 0.58) ty = 2; }
		else if (stage == 3) { if (el > 40f && q > 0.88) ty = 7; else if (el > 20f && q > 0.7) ty = 5; else if (el > 45f && q > 0.6) ty = 2; }
		else if (stage == 4) { if (el > 30f && q > 0.66) ty = 2; else if (el > 50f && q > 0.5) ty = 8; }
		else if (stage == 5) { if (el > 30f && q > 0.64) ty = 2; else if (el > 15f && q > 0.7) ty = 8; else if (q < 0.3) ty = 1; }
		else if (stage == 6) { if (el > 50f && q > 0.9) ty = 7; else if (el > 20f && q > 0.66) ty = 6; else if (el > 40f && q > 0.5) ty = 8; else if (el > 60f && q > 0.45) ty = 2; }
		else if (stage == 7) { if (el > 45f && q > 0.84) ty = 7; else if (el > 20f && q > 0.62) ty = 5; else if (el > 35f && q > 0.46) ty = 2; else if (el > 65f && q > 0.38) ty = 8; }
		AddEn(ty, px + (float)Math.Cos(a) * r, py + (float)Math.Sin(a) * r, false);
	}
	private void AddEn(int ty, float x, float y, bool elite)
	{
		float sc = (1f + el / 32f + el * el / 7000f) * StHp[stage] * DHp[diff]; float ds = 1f + el / 180f; En e = new En { X = x, Y = y, T = ty, Elite = elite };
		if (ty == 0) { e.MaxHp = 14f * sc; e.Sp = 44f + el * 0.12f; e.R = 12f; e.Dmg = 7; }
		else if (ty == 1) { e.MaxHp = 8f * sc; e.Sp = 84f + el * 0.1f; e.R = 9f; e.Dmg = 5; }
		else if (ty == 2) { e.MaxHp = 70f * sc; e.Sp = 32f; e.R = 19f; e.Dmg = 14; }
		else if (ty == 4) { e.MaxHp = 22f * sc; e.Sp = 54f; e.R = 13f; e.Dmg = 8; e.Ph = 1.2f + (float)Rnd.NextDouble(); }
		else if (ty == 5) { e.MaxHp = 30f * sc; e.Sp = 62f; e.R = 14f; e.Dmg = 12; e.Ph = 0.6f + (float)Rnd.NextDouble(); }
		else if (ty == 8) { e.MaxHp = 16f * sc; e.Sp = 96f; e.R = 11f; e.Dmg = 18; }
		else if (ty == 6) { e.MaxHp = 60f * sc; e.Sp = 40f; e.R = 17f; e.Dmg = 9; }
		else { e.MaxHp = 110f * sc; e.Sp = 36f; e.R = 18f; e.Dmg = 13; }
		if (elite) { e.MaxHp *= 4.5f; e.R *= 1.3f; e.Dmg = (int)(e.Dmg * 1.3f); }
		e.Dmg = (int)(e.Dmg * ds); e.Hp = e.MaxHp; en.Add(e);
	}
	private void AddBoss(int b)
	{
		float[] hp = { 1400f, 2800f, 4200f, 7000f }, rr = { 30f, 34f, 32f, 38f }, sp = { 42f, 34f, 46f, 40f }; int[] dm = { 22, 26, 24, 30 };
		double a = Rnd.NextDouble() * Math.PI * 2; float ds = 1f + el / 220f;
		En e = new En { X = px + (float)Math.Cos(a) * 340f, Y = py + (float)Math.Sin(a) * 340f, T = 3, Elite = true, IsBoss = true, Bi = b };
		e.MaxHp = hp[b] * (1f + el / 220f) * StHp[stage]; e.Hp = e.MaxHp; e.R = rr[b]; e.Sp = sp[b]; e.Dmg = (int)(dm[b] * ds); e.Ph = 2f;
		en.Add(e); Say("경고! " + BName[b] + " 등장!");
	}
	private En Nearest(float x, float y, float maxD, List<En> skip)
	{
		En best = null; float bd = maxD * maxD;
		for (int i = 0; i < en.Count; i++) { En e = en[i]; if (skip != null && skip.Contains(e)) continue; float dx = e.X - x, dy = e.Y - y, d = dx * dx + dy * dy; if (d < bd) { bd = d; best = e; } }
		return best;
	}
	private void Ouch(float d)
	{
		d *= (1f - 0.08f * pl[5]) * DDmg[diff]; hp -= d; stress = Math.Min(100f, stress + d * 0.38f * DStress[diff]); if (stress >= 100f && clockT <= -5f) { }
	}
	private void ClockOut(bool revive)
	{
		skFx = 1f;
		if (stress < 100f || mode != 0 || Over) return;
		stress = 0f; clockT = 1.3f; hurtT = 2.2f; eps.Clear(); Shake = 0.8f; flashT = 0.6f; Sfx.Play("fanfare");
		hp = revive ? maxHp * 0.45f : Math.Min(maxHp, hp + maxHp * 0.25f);
		for (int i = 0; i < en.Count; i++) { En e = en[i]; if (Math.Abs(e.X - px) < 260f && Math.Abs(e.Y - py) < 380f) Hurt(e, e.IsBoss ? 70f + e.MaxHp * 0.1f : 600f); }
		Say(revive ? "연차 사용! 다시 일어났어요" : "퇴근 선언! 정시 퇴근!");
		for (int k = 0; k < 4; k++) Fx.Burst(200f + (k % 2 == 0 ? -70f : 70f), 330f + (k < 2 ? -70f : 70f), Color.FromArgb(255, 226, 120), 14);
	}
	private void Hurt(En e, float dmg, int src = -1, float ang = 0f)
	{
		if (e.Hp <= 0f) return;
		if (e.T == 7) dmg *= 0.45f;
		float fin = dmg * Dm; bool crit = Rnd.NextDouble() < 0.05 + 0.05 * pl[6]; if (crit) fin *= 2f;
		if ((crit || dnT <= 0f) && dns.Count < 50) { dns.Add(new Dn { X = e.X + (float)(Rnd.NextDouble() - 0.5) * 10f, Y = e.Y - e.R, V = (int)Math.Round(fin), Crit = crit }); if (!crit) dnT = 0.09f; }
		e.Hp -= fin; e.Hit = 0.1f;
		if (src >= 0) { if (src != 4 || Rr() < 0.35f) HitFx(src, e.X + (Rr() - 0.5f) * e.R * 0.4f, e.Y - e.R * 0.3f, ang); e.Sq = 1f; float kb = e.IsBoss ? 1.5f : Math.Min(6f, 90f / (e.R + 4f)); e.Rx = (float)Math.Cos(ang) * kb; e.Ry = (float)Math.Sin(ang) * kb; }
		if (crit) { Shake = Math.Max(Shake, 0.08f); if (e.Elite || e.IsBoss) hitStop = Math.Max(hitStop, 0.045f); }
		if (e.Hp <= 0f) Kill(e);
	}
	private void Kill(En e)
	{
		{
			float big = e.IsBoss ? 2.4f : (e.Elite ? 1.5f : 1f);
			V("e_jump", e.X, e.Y - e.R * 0.2f, e.R * 1.1f * big, e.R * 2.8f * big, 0.36f, Rr() * 360f, 0f, 0f, 0f, 0.75f, null); V("e_hit", e.X, e.Y - e.R * 0.3f, e.R * 0.8f * big, e.R * 2.2f * big, 0.16f, Rr() * 360f, 0f, 0f, 0f, 1f, null);
			if (stage == 1 && e.T != 7 && !e.IsBoss && Spr.Get("ms_opx0") != null) { if (cps.Count > 36) cps.RemoveAt(0); cps.Add(new Cp { X = e.X, Y = e.Y, H = e.R * 3.8f, Pre = "opx", Flip = px < e.X }); }
			if (stage != 1 && (e.T == 0 || e.T == 2 || e.T == 3) && Spr.Get("ms_" + (e.T == 0 ? "zad" : (e.T == 2 ? "zcd" : "zbd")) + "0") != null) { if (cps.Count > 36) cps.RemoveAt(0); cps.Add(new Cp { X = e.X, Y = e.Y, H = e.R * (e.T == 3 ? 3.5f : 3.0f), Pre = e.T == 0 ? "zad" : (e.T == 2 ? "zcd" : "zbd"), Flip = px < e.X }); }
			if (e.IsBoss) hitStop = Math.Max(hitStop, 0.12f); else if (e.Elite) hitStop = Math.Max(hitStop, 0.07f);
		}
		kills++; stress = Math.Min(100f, stress + (e.IsBoss ? 6f : (e.Elite ? 1.5f : 0.1f)) * DStress[diff]); int v = e.IsBoss ? 30 : (e.T == 2 || e.T == 6 || e.T == 7 ? 4 : (e.T == 4 || e.T == 5 ? 2 : 1)); if (e.T == 2 || e.Elite) v += lvl / 3; if (e.Elite && !e.IsBoss) v += 4;
		if (!e.IsBoss) v = (int)Math.Ceiling(v * 1.8f);   // 적 수를 줄인 만큼 한 마리가 주는 경험치를 키웠어요
		if (surgeT > 0f) v *= 2;
		gm.Add(new Gm { X = e.X, Y = e.Y, V = v });
		if (e.T == 6) { for (int i = 0; i < 3; i++) { double a = Math.PI * 2 * i / 3 + Rnd.NextDouble(); AddEn(1, e.X + (float)Math.Cos(a) * 14f, e.Y + (float)Math.Sin(a) * 14f, false); } }
		if (e.IsBoss)
		{
			bossKills++; eps.Clear(); pk.Add(new Pk { X = e.X + 16, Y = e.Y, K = 30 }); pk.Add(new Pk { X = e.X - 14, Y = e.Y + 6, K = 10 + Rnd.Next(8) }); pk.Add(new Pk { X = e.X, Y = e.Y - 12, K = 0 });
			Shake = 0.6f; Say(BName[e.Bi] + " 처치!"); Sfx.Play("fanfare"); Fx.Burst(e.X - px + 200f, e.Y - py + 330f, Color.FromArgb(255, 214, 90), 22);
		}
		else if (e.Elite) { pk.Add(new Pk { X = e.X + 10, Y = e.Y, K = 10 + Rnd.Next(8) }); pk.Add(new Pk { X = e.X - 10, Y = e.Y, K = 0 }); Shake = 0.35f; }
		else if (Rnd.NextDouble() < 0.025 + 0.006 * pl[6]) pk.Add(new Pk { X = e.X, Y = e.Y, K = 0 });
		Fx.Burst(e.X - px + 200f, e.Y - py + 330f, e.T == 1 ? Color.FromArgb(150, 110, 200) : Color.FromArgb(120, 220, 120), e.Elite ? 14 : 4);
	}

	private void Weapons(float dt0)
	{
		float dt = dt0 * (1f + 0.08f * pl[4]);   // 정시퇴근 수당: 모든 무기가 더 빨리 재사용돼요
		// 불꽃
		if (wl[0] > 0)
		{
			cdF -= dt;
			if (cdF <= 0f)
			{
				int L = wl[0]; cdF = Math.Max(0.3f, 0.78f - 0.06f * L); int n = 1 + (L + 1) / 3; List<En> used = new List<En>();
				for (int i = 0; i < n; i++) { En e = Nearest(px, py, 300f, used); if (e == null) break; used.Add(e); float dx = e.X - px, dy = e.Y - py, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f; pr.Add(new Pr { X = px, Y = py, Vx = dx / l * 330f, Vy = dy / l * 330f, Dmg = 13f + 5f * L, Life = 1.3f, R = 6f, Kind = 0, Pierce = L >= 5 ? 1 : 0 }); }
				if (used.Count > 0) Sfx.Play("tick");
			}
		}
		// 번개
		if (wl[2] > 0)
		{
			cdL -= dt;
			if (cdL <= 0f)
			{
				int L = wl[2]; cdL = Math.Max(0.6f, 1.7f - 0.12f * L); int strikes = evo[1] ? 3 : 1; List<En> used = new List<En>();
				for (int s = 0; s < strikes; s++)
				{
					En e = null; List<En> cand = new List<En>(); for (int i = 0; i < en.Count; i++) { En q = en[i]; if (Math.Abs(q.X - px) < 210f && Math.Abs(q.Y - py) < 300f && !used.Contains(q)) cand.Add(q); }
					if (cand.Count > 0) e = cand[Rnd.Next(cand.Count)];
					if (e == null) break; used.Add(e);
					float dmg = 22f + 7f * L; float cx = e.X, cy = e.Y; Hurt(e, dmg, 2, 1.57f); V("sve1", cx, cy - 6f, 40f, 78f, 0.32f, 0f, 0f, 0f, 0f, 0.95f, null); bolts.Add(new Bolt { X1 = cx, Y1 = cy - 220f, X2 = cx, Y2 = cy, T = 0.18f, Big = evo[1] });
					int chain = L / 2; En last = e; List<En> hit = new List<En> { e };
					for (int c = 0; c < chain; c++) { En nx = Nearest(last.X, last.Y, 100f, hit); if (nx == null) break; hit.Add(nx); bolts.Add(new Bolt { X1 = last.X, Y1 = last.Y, X2 = nx.X, Y2 = nx.Y, T = 0.18f }); Hurt(nx, dmg * 0.75f, 2, 1.57f); last = nx; }
					if (evo[1]) { for (int i = 0; i < en.Count; i++) { En q = en[i]; float dx = q.X - cx, dy = q.Y - cy; if (dx * dx + dy * dy < 70f * 70f) Hurt(q, dmg * 0.6f, 2, 1.57f); } Fx.Burst(cx - px + 200f, cy - py + 330f, Color.FromArgb(255, 240, 120), 8); }
				}
				if (used.Count > 0) { Sfx.Play("flip"); }
			}
		}
		// 부메랑
		if (wl[3] > 0)
		{
			cdB -= dt;
			if (cdB <= 0f)
			{
				int L = wl[3]; cdB = Math.Max(0.9f, 1.9f - 0.1f * L); En e = Nearest(px, py, 360f, null); float ax = fx, ay = fy;
				if (e != null) { ax = e.X - px; ay = e.Y - py; float l0 = (float)Math.Sqrt(ax * ax + ay * ay) + 0.01f; ax /= l0; ay /= l0; }
				int n = evo[2] ? 3 : 1 + L / 4; float spread = evo[2] ? 0.5f : 0.3f;
				for (int i = 0; i < n; i++)
				{
					float ang = (float)Math.Atan2(ay, ax) + (i - (n - 1) / 2f) * spread;
					pr.Add(new Pr { X = px, Y = py, Vx = (float)Math.Cos(ang) * 270f, Vy = (float)Math.Sin(ang) * 270f, Dmg = (16f + 5f * L) * (evo[2] ? 1.8f : 1f), Life = 3f, R = evo[2] ? 14f : 11f, Kind = 1, Pierce = 999 });
				}
				Sfx.Play("whoosh");
			}
		}
		// 수호 기운 (장판)
		if (wl[4] > 0)
		{
			auraT -= dt;
			if (auraT <= 0f) { auraT = 0.5f; int L = wl[4]; float r = (52f + 11f * L) * (1f + 0.12f * pl[7]); for (int i = 0; i < en.Count; i++) { En e = en[i]; float dx = e.X - px, dy = e.Y - py; if (dx * dx + dy * dy < (r + e.R) * (r + e.R)) Hurt(e, 5f + 2.6f * L, 4, (float)Math.Atan2(dy, dx)); } }
		}
		// 도장 쾅
		if (wl[5] > 0)
		{
			cdS -= dt;
			if (cdS <= 0f)
			{
				int L = wl[5]; cdS = Math.Max(1.2f, 2.8f - 0.22f * L); En t0 = Nearest(px, py, 280f, null);
				if (t0 != null) { stp.Add(new St { X = t0.X, Y = t0.Y, T = 0.5f, R = 58f + 7f * L, Dmg = 34f + 11f * L }); }
			}
		}
		for (int i = stp.Count - 1; i >= 0; i--)
		{
			St q = stp[i];
			if (!q.Hit) { q.T -= dt; if (q.T <= 0f) { q.Hit = true; q.Fl = 0.35f; for (int j = 0; j < en.Count; j++) { En e = en[j]; float dx = e.X - q.X, dy = e.Y - q.Y, rr = q.R + e.R; if (dx * dx + dy * dy < rr * rr) Hurt(e, q.Dmg, 5, (float)Math.Atan2(dy, dx)); } for (int k2 = 0; k2 < 7; k2++) { double a2 = k2 * 0.8976 + Rr(); V("e_jump", q.X, q.Y, 10f, 30f, 0.42f, Rr() * 360f, 0f, (float)Math.Cos(a2) * 110f, (float)Math.Sin(a2) * 70f, 0.75f, null); } V("fx_hit", q.X, q.Y, q.R * 0.8f, q.R * 2.4f, 0.22f, 0f, 0f, 0f, 0f, 0.7f, "gold"); Shake = Math.Max(Shake, 0.18f); Sfx.Play("thud_soft"); } }
			else { q.Fl -= dt; if (q.Fl <= 0f) stp.RemoveAt(i); }
		}
		// 회전 방울
		if (wl[1] > 0)
		{
			int L = wl[1]; int n = Math.Min(5, 1 + L / 2) + (evo[0] ? 2 : 0); float rad = 64f + (evo[0] ? 14f : 0f); float dmg = (8f + 3f * L) * (evo[0] ? 2f : 1f);
			orbA += dt * (2.7f + L * 0.12f);
			for (int i = 0; i < n; i++)
			{
				float a = orbA + i * (float)Math.PI * 2f / n, ox = px + (float)Math.Cos(a) * rad, oy = py + (float)Math.Sin(a) * rad;
				for (int j = 0; j < en.Count; j++) { En e = en[j]; if (e.Ocd > 0f) continue; float dx = e.X - ox, dy = e.Y - oy, rr = e.R + 9f; if (dx * dx + dy * dy < rr * rr) { e.Ocd = 0.4f; Hurt(e, dmg, 1, (float)Math.Atan2(e.Y - oy, e.X - ox)); } }
			}
		}
		// 서류 비행기 (유도)
		if (wl[6] > 0)
		{
			cdP -= dt;
			if (cdP <= 0f)
			{
				int L = wl[6]; cdP = Math.Max(0.8f, 1.9f - 0.12f * L); int n = 1 + L / 2; bool any = false;
				List<En> used = new List<En>();
				for (int i = 0; i < n; i++)
				{
					En e = Nearest(px, py, 340f, used); float ang = e != null ? (float)Math.Atan2(e.Y - py, e.X - px) : (float)(Rnd.NextDouble() * 6.28); if (e != null) used.Add(e); any = true;
					ang += (i - (n - 1) / 2f) * 0.35f;
					pr.Add(new Pr { X = px, Y = py, Vx = (float)Math.Cos(ang) * 250f, Vy = (float)Math.Sin(ang) * 250f, Dmg = (11f + 4.5f * L) * (evo[3] ? 1.5f : 1f), Life = 3.2f, R = 7f, Kind = 2, Pierce = evo[3] ? 2 : 0 });
				}
				if (evo[3]) for (int i = 0; i < 8; i++) { float ang = i * 0.7854f + beamA; pr.Add(new Pr { X = px, Y = py, Vx = (float)Math.Cos(ang) * 230f, Vy = (float)Math.Sin(ang) * 230f, Dmg = 13f + 4f * L, Life = 2.4f, R = 7f, Kind = 2, Pierce = 2 }); }
				if (any) Sfx.Play("whoosh");
			}
		}
		// 커피 웅덩이
		if (wl[7] > 0)
		{
			cdC -= dt;
			if (cdC <= 0f)
			{
				int L = wl[7]; cdC = Math.Max(1.4f, 3.2f - 0.25f * L); int n = 1 + L / 4; List<En> used = new List<En>();
				for (int i = 0; i < n && pud.Count < 9; i++)
				{
					En e = Nearest(px, py, 260f, used); float x, y; if (e != null) { used.Add(e); x = e.X; y = e.Y; } else { double a = Rnd.NextDouble() * 6.28; x = px + (float)Math.Cos(a) * 90f; y = py + (float)Math.Sin(a) * 90f; }
					pud.Add(new Pu { X = x, Y = y, Life = 4.5f + 0.4f * L, R = (44f + 6f * L) * (evo[4] ? 1.45f : 1f) * (1f + 0.12f * pl[7]), Tk = 0.1f });
				}
				Sfx.Play("thud_soft");
			}
		}
		// 스테이플러 (부채꼴 샷건)
		if (wl[8] > 0)
		{
			cdK -= dt;
			if (cdK <= 0f)
			{
				int L = wl[8]; En e = Nearest(px, py, 210f, null);
				if (e != null)
				{
					cdK = Math.Max(0.6f, 1.3f - 0.08f * L); int n = 3 + L / 2; float ang = (float)Math.Atan2(e.Y - py, e.X - px);
					for (int i = 0; i < n; i++) { float a2 = ang + (i - (n - 1) / 2f) * 0.2f; pr.Add(new Pr { X = px, Y = py, Vx = (float)Math.Cos(a2) * 540f, Vy = (float)Math.Sin(a2) * 540f, Dmg = 8f + 3.2f * L, Life = 0.36f, R = 5f, Kind = 3, Pierce = L >= 6 ? 1 : 0 }); }
					Sfx.Play("tick");
				}
				else cdK = 0.2f;
			}
		}
		// 레이저 포인터 (회전 빛줄기)
		if (wl[9] > 0)
		{
			int L = wl[9]; beamA += dt * (1.6f + 0.08f * L); beamT -= dt;
			if (beamT <= 0f)
			{
				beamT = 0.22f; int nb = evo[5] ? 3 : 1; float len = (150f + 14f * L) * (1f + 0.12f * pl[7]); float dm3 = 6f + 2.4f * L;
				for (int b = 0; b < nb; b++)
				{
					float a = beamA + b * 2.0944f, ex = (float)Math.Cos(a) * len, ey = (float)Math.Sin(a) * len;
					for (int j = 0; j < en.Count; j++)
					{
						En e = en[j]; float rx = e.X - px, ry = e.Y - py, t0 = (rx * ex + ry * ey) / (len * len); if (t0 < 0f) t0 = 0f; else if (t0 > 1f) t0 = 1f;
						float qx = rx - ex * t0, qy = ry - ey * t0, rr = e.R + 7f; if (qx * qx + qy * qy < rr * rr) Hurt(e, dm3, 9, (float)Math.Atan2(ey, ex));
					}
				}
			}
		}
	}
	private void Shoot(float x, float y, float ang, float sp, float dmg, int kind, float r) { eps.Add(new Ep { X = x, Y = y, Vx = (float)Math.Cos(ang) * sp, Vy = (float)Math.Sin(ang) * sp, R = r, Dmg = dmg, Life = 6f, Kind = kind }); }
	// 돌진 상태(1 예고 · 2 돌진). 처리했으면 true
	private bool DashStep(En e, float dt, float ux, float uy, float telT, float goT, float spd)
	{
		if (e.Pat == 1) { e.Tm -= dt; if (e.Tm > 0.25f) { e.Dx = ux; e.Dy = uy; } if (e.Tm <= 0f) { e.Pat = 2; e.Tm = goT; } return true; }
		if (e.Pat == 2) { e.Tm -= dt; e.X += e.Dx * spd * dt; e.Y += e.Dy * spd * dt; if (e.Tm <= 0f) { e.Pat = 0; e.Ph = 2.6f; } return true; }
		return false;
	}
	private void BossAI(En e, float dt, float ux, float uy, float l)
	{
		bool enr = e.Hp / e.MaxHp < 0.45f; float spd = e.Sp * (enr ? 1.3f : 1f);
		if (DashStep(e, dt, ux, uy, 0.8f, 0.6f, 330f)) return;
		if (e.Sp2 > 0f)
		{
			e.Sp2 -= dt; e.Ph3 -= dt;
			if (e.Ph3 <= 0f) { e.Ph3 = enr ? 0.09f : 0.13f; e.Ang += 0.5f; Shoot(e.X, e.Y, e.Ang, 170f, e.Dmg * 0.6f, 1, 7f); if (enr) Shoot(e.X, e.Y, e.Ang + 3.14159f, 170f, e.Dmg * 0.6f, 1, 7f); }
		}
		else if (l > 70f) { e.X += ux * spd * dt; e.Y += uy * spd * dt; }
		e.Ph -= dt;
		if (e.Ph <= 0f && e.Sp2 <= 0f)
		{
			int[] pt = BPat[e.Bi]; int p = pt[e.PatI++ % pt.Length]; e.Ph = enr ? 1.7f : 2.4f; float aim = (float)Math.Atan2(uy, ux);
			if (p == 0) { int n = enr ? 9 : 5; for (int i = 0; i < n; i++) Shoot(e.X, e.Y, aim + (i - (n - 1) / 2f) * 0.22f, 190f, e.Dmg * 0.7f, 0, 7f); Sfx.Play("whoosh"); }
			else if (p == 1) { int n = enr ? 22 : 14; e.Ang += 0.2f; for (int i = 0; i < n; i++) Shoot(e.X, e.Y, e.Ang + i * 6.28318f / n, 130f, e.Dmg * 0.65f, 1, 7f); Sfx.Play("whoosh"); }
			else if (p == 2) { int n = enr ? 8 : 5; for (int i = 0; i < n; i++) { double a = Rnd.NextDouble() * Math.PI * 2; AddEn(1, e.X + (float)Math.Cos(a) * 40f, e.Y + (float)Math.Sin(a) * 40f, false); } if (enr && e.Bi >= 1) { AddEn(5, e.X + 30f, e.Y, false); AddEn(5, e.X - 30f, e.Y, false); } }
			else if (p == 3) { e.Pat = 1; e.Tm = 0.8f; e.Dx = ux; e.Dy = uy; }
			else { e.Sp2 = 2.4f; e.Ph3 = 0f; }
		}
	}
	private void Enemies(float dt)
	{
		for (int i = en.Count - 1; i >= 0; i--)
		{
			En e = en[i]; if (e.Hp <= 0f) { en.RemoveAt(i); continue; }
			if (e.Hit > 0) e.Hit -= dt; if (e.Ocd > 0) e.Ocd -= dt; if (e.Pcd > 0) e.Pcd -= dt;
			float dx = px - e.X, dy = py - e.Y, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f, ux = dx / l, uy = dy / l;
			float spBak = e.Sp; if (e.Fz > 0f) { e.Fz -= dt; e.Sp *= e.IsBoss ? 0.75f : 0.5f; }
			if (freezeT <= 0f)
			{
				if (e.T == 8) { if (e.Pat == 0) { e.X += ux * e.Sp * dt; e.Y += uy * e.Sp * dt; if (l < 62f) { e.Pat = 3; e.Tm = 0.65f; } } else { e.Tm -= dt; if (e.Tm <= 0f) { if (l < 74f && hurtT <= 0f) { Ouch(e.Dmg * (1f + el / 220f)); hurtT = 0.55f; Shake = 0.35f; Sfx.Play("bad2"); } if (Spr.Get("ms_wexp0") != null) VA("wexp", 0, 8, e.X, e.Y - 6f, 150f, 0.7f, 0f, 1f, null); Fx.Burst(e.X - px + 200f, e.Y - py + 330f, Color.FromArgb(255, 170, 60), 12); Sfx.Play("thud_soft"); e.Hp = 0f; } } }
				else if (e.IsBoss) BossAI(e, dt, ux, uy, l);
				else if (e.T == 4)
				{
					float mv = l > 190f ? 1f : (l < 130f ? -0.6f : 0f); e.X += ux * e.Sp * mv * dt; e.Y += uy * e.Sp * mv * dt; e.Ph -= dt;
					if (e.Ph <= 0f && l < 340f) { e.Ph = 2.4f + (float)Rnd.NextDouble(); Shoot(e.X, e.Y, (float)Math.Atan2(dy, dx), 150f, e.Dmg * 0.8f, 0, 7f); }
				}
				else if (e.T == 5)
				{
					if (!DashStep(e, dt, ux, uy, 0.7f, 0.55f, 340f)) { e.X += ux * e.Sp * dt; e.Y += uy * e.Sp * dt; e.Ph -= dt; if (e.Ph <= 0f && l < 270f) { e.Pat = 1; e.Tm = 0.7f; e.Dx = ux; e.Dy = uy; } }
				}
				else { e.X += ux * e.Sp * dt; e.Y += uy * e.Sp * dt; }
			}
			e.Sp = spBak;
			PushOut(ref e.X, ref e.Y, e.R * 0.7f);
			// 서로 겹치지 않게 살짝 밀어내기 (가까운 몇 마리만)
			if (!e.IsBoss && i % 3 == (int)(el * 20f) % 3) for (int j = i - 1; j >= 0 && j > i - 6; j--) { En o = en[j]; if (o.IsBoss) continue; float ox = e.X - o.X, oy = e.Y - o.Y, d2 = ox * ox + oy * oy, rr = (e.R + o.R) * 0.8f; if (d2 < rr * rr && d2 > 0.01f) { float d = (float)Math.Sqrt(d2), push = (rr - d) * 0.5f; e.X += ox / d * push; e.Y += oy / d * push; o.X -= ox / d * push; o.Y -= oy / d * push; } }
			if (l < e.R + 12f && hurtT <= 0f && e.T != 8) { Ouch(e.Dmg); hurtT = 0.55f; Shake = 0.2f; Sfx.Play("bad2"); }
			float far = 720f; if (l > far && !e.IsBoss) { double a = Rnd.NextDouble() * Math.PI * 2; e.X = px + (float)Math.Cos(a) * 340f; e.Y = py + (float)Math.Sin(a) * 340f; }
		}
	}
	private void EnemyShots(float dt)
	{
		for (int i = eps.Count - 1; i >= 0; i--)
		{
			Ep q = eps[i]; q.Life -= dt; q.X += q.Vx * dt; q.Y += q.Vy * dt; float dx = q.X - px, dy = q.Y - py, rr = q.R + 11f;
			if (dx * dx + dy * dy < rr * rr) { if (hurtT <= 0f) { Ouch(q.Dmg); hurtT = 0.45f; Shake = 0.15f; Sfx.Play("bad2"); } eps.RemoveAt(i); continue; }
			if (q.Life <= 0f || dx * dx + dy * dy > 640f * 640f) eps.RemoveAt(i);
		}
	}
	private void Projectiles(float dt)
	{
		for (int i = pr.Count - 1; i >= 0; i--)
		{
			Pr p = pr[i]; p.Life -= dt; p.Age += dt;
			if (p.Kind == 1)
			{
				if (!p.Back && p.Age > 0.6f) p.Back = true;
				if (p.Back) { float dx = px - p.X, dy = py - p.Y, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f; p.Vx += (dx / l * 340f - p.Vx) * Math.Min(1f, dt * 6f); p.Vy += (dy / l * 340f - p.Vy) * Math.Min(1f, dt * 6f); if (l < 16f) { pr.RemoveAt(i); continue; } }
			}
			if (p.Kind == 2 && p.Age > 0.12f)
			{
				En tg = Nearest(p.X, p.Y, 300f, null);
				if (tg != null) { float dx = tg.X - p.X, dy = tg.Y - p.Y, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f, spd = 255f; p.Vx += (dx / l * spd - p.Vx) * Math.Min(1f, dt * 4.5f); p.Vy += (dy / l * spd - p.Vy) * Math.Min(1f, dt * 4.5f); }
			}
			p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Tk -= dt; if (p.Tk <= 0f) { p.Tk = 0.03f; Trail(p); }
			bool dead = p.Life <= 0f;
			for (int j = 0; j < en.Count && !dead; j++)
			{
				En e = en[j]; if (e.Hp <= 0f) continue; float dx = e.X - p.X, dy = e.Y - p.Y, rr = e.R + p.R;
				if (dx * dx + dy * dy < rr * rr)
				{
					if (p.Kind == 1) { if (e.Pcd > 0f) continue; e.Pcd = 0.3f; Hurt(e, p.Dmg, 3, (float)Math.Atan2(p.Vy, p.Vx)); }
					else { Hurt(e, p.Dmg, p.Kind == 0 ? 0 : (p.Kind == 2 ? 6 : 8), (float)Math.Atan2(p.Vy, p.Vx)); if (p.Pierce-- <= 0) dead = true; }
				}
			}
			if (dead) pr.RemoveAt(i);
		}
	}
	// ── 보스 상자 ─────────────────────────────
	private void RollAward(out int k, out int v)
	{
		List<int[]> pool = new List<int[]>();
		for (int i = 0; i < 10; i++) if (WOk(i)) pool.Add(new int[] { 0, i });
		for (int i = 0; i < 11; i++) if (POk(i)) pool.Add(new int[] { 1, i });
		List<int> ev = new List<int>(); for (int e = 0; e < 6; e++) if (EvoReady(e)) ev.Add(e);
		if (ev.Count > 0 && (pool.Count == 0 || Rnd.NextDouble() < 0.5)) { k = 2; v = ev[Rnd.Next(ev.Count)]; return; }
		if (pool.Count == 0) { k = 3; v = Rnd.Next(3); return; }
		int[] c = pool[Rnd.Next(pool.Count)]; k = c[0]; v = c[1];
	}
	private void OpenChest()
	{
		double q = Rnd.NextDouble(), p3 = 0.12 + 0.03 * pl[6], p2 = 0.33;
		chN = q < p3 ? 3 : (q < p3 + p2 ? 2 : 1);
		for (int i = 0; i < chN; i++)
		{
			int k, v, tries = 0; do { RollAward(out k, out v); tries++; } while (tries < 8 && i > 0 && ((chK[0] == k && chV[0] == v) || (i > 1 && chK[1] == k && chV[1] == v)));
			chK[i] = k; chV[i] = v;
			if (k == 3) { hp = Math.Min(maxHp, hp + maxHp * HFrac[v]); healFx = 1f; chLv[i] = 0; }
			else if (k == 0) { wl[v] = Math.Min(MaxLv, wl[v] + 1); chLv[i] = wl[v]; }
			else if (k == 1) { pl[v] = Math.Min(v == 10 ? 2 : 5, pl[v] + 1); chLv[i] = pl[v]; if (v == 0) { maxHp += 20f; hp = Math.Min(maxHp, hp + 20f); } }
			else { DoEvo(v); chLv[i] = 0; }
		}
		chT = 0f; chCue = 0; mode = 3; eps.Clear(); Sfx.Play("thud");
	}
	private float ChestDone { get { return 1.1f + 0.5f * (chN - 1) + 0.8f; } }
	private void ChestTick(float dt)
	{
		chT += dt;
		while (chCue < chN + 2)
		{
			float at = chCue == 0 ? 0.9f : (chCue <= chN ? 1.1f + 0.5f * (chCue - 1) : 1.1f + 0.5f * chN + 0.05f);
			if (chT < at) break;
			if (chCue == 0) { Sfx.Play("bad_big"); Fx.Burst(200f, 230f, Color.FromArgb(255, 226, 120), 18); }
			else if (chCue <= chN) { Sfx.Play(chN == 3 ? "ok2" : "coin"); Fx.Burst(ChestCardX(chCue - 1) + 56f, 380f, chN == 3 ? Hsv(chCue * 110f) : Color.FromArgb(255, 226, 120), 14); }
			else if (chN == 3) { Sfx.Play("fanfare"); Sfx.Play("record"); Shake = 0.7f; for (int j = 0; j < 6; j++) Fx.Burst(60f + j * 56f, 120f + (j % 2) * 120f, Hsv(j * 60f), 20); }
			else if (chN == 2) Sfx.Play("ok2");
			chCue++;
		}
	}
	private void ChestClose()
	{
		if (chT < ChestDone) { chT = ChestDone; ChestTick(0f); return; }
		mode = 0; hurtT = Math.Max(hurtT, 1.2f); Say(chN == 3 ? "대박! 능력 3개 획득!" : "상자에서 능력 " + chN + "개 획득!");
		if (xp >= XpNeed) LevelUp();
	}
	private float ChestCardX(int i) { return 200f - (chN * 120f - 8f) / 2f + i * 120f; }
	private static Color Hsv(float h)
	{
		h = ((h % 360f) + 360f) % 360f; float c = 1f, x = 1f - Math.Abs((h / 60f) % 2f - 1f); float r = 0, gg = 0, b = 0;
		if (h < 60) { r = c; gg = x; } else if (h < 120) { r = x; gg = c; } else if (h < 180) { gg = c; b = x; } else if (h < 240) { gg = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
		return Color.FromArgb((int)(120 + 135 * r), (int)(120 + 135 * gg), (int)(120 + 135 * b));
	}
	private void Hazards(float dt)
	{
		if (stage >= 3)
		{
			hzT -= dt;
			if (hzT <= 0f)
			{
				hzT = Math.Max(1.6f, 5.5f - el * 0.012f) * (stage == 3 ? 1.7f : 1f) * (0.7f + (float)Rnd.NextDouble() * 0.6f); int n = 1 + (int)(el / 90f);
				for (int i = 0; i < n; i++) { double a = Rnd.NextDouble() * 6.28; float r = (i == 0 ? 30f : 70f) + (float)Rnd.NextDouble() * 180f; hzs.Add(new Hz { X = px + (float)Math.Cos(a) * r, Y = py + (float)Math.Sin(a) * r, T = stage == 5 ? 1.3f : 1.5f, R = stage == 5 ? 58f : (stage == 3 ? 38f : 44f), K = stage == 5 ? 1 : 0 }); }
			}
		}
		for (int i = hzs.Count - 1; i >= 0; i--)
		{
			Hz h = hzs[i];
			if (!h.Done) { h.T -= dt; if (h.T <= 0f) { h.Done = true; h.Fl = 0.4f; float dx = px - h.X, dy = py - h.Y, rr = h.R + 8f; if (dx * dx + dy * dy < rr * rr && hurtT <= 0f) { Ouch(h.K == 0 ? 15f : 11f); hurtT = 0.5f; Shake = 0.3f; Sfx.Play("bad2"); } if (h.K == 1) for (int j = 0; j < en.Count; j++) { En e = en[j]; float ex = e.X - h.X, ey = e.Y - h.Y, r2 = h.R + e.R; if (ex * ex + ey * ey < r2 * r2) Hurt(e, 45f); } Sfx.Play("thud_soft"); } }
			else { h.Fl -= dt; if (h.Fl <= 0f) hzs.RemoveAt(i); }
		}
	}
	private void Pickups(float dt)
	{
		float mg = Magnet;
		for (int i = gm.Count - 1; i >= 0; i--)
		{
			Gm g = gm[i]; float dx = px - g.X, dy = py - g.Y, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f;
			if (l < mg) { float sp = 260f + (mg - l) * 3f; g.X += dx / l * sp * dt; g.Y += dy / l * sp * dt; }
			if (l < 14f) { xp += (int)Math.Round(g.V * (1f + 0.12f * pl[9])); gm.RemoveAt(i); Sfx.Play("tick"); if (xp >= XpNeed && mode == 0) { LevelUp(); return; } }
		}
		for (int i = pk.Count - 1; i >= 0; i--)
		{
			Pk k = pk[i]; k.T += dt; float dx = px - k.X, dy = py - k.Y;
			if (dx * dx + dy * dy < 22f * 22f)
			{
				pk.RemoveAt(i);
				if (k.K == 30) { OpenChest(); return; }
				if (k.K == 0) { hp = Math.Min(maxHp, hp + 30f); Say("믹스커피! 체력 +30"); Sfx.Play("ok2"); }
				else
				{
					int r = k.K - 10; rel[r]++; Say("유물: " + RName[r]); Sfx.Play("fanfare"); Shake = 0.25f;
					if (r == 5) { xp += XpNeed; if (mode == 0) LevelUp(); } else if (r == 6) stress = 100f; else if (r == 7) { maxHp *= 1.3f; hp = maxHp; }
					else if (r == 0) dmgBonus += 0.25f; else if (r == 1) hp = maxHp; else if (r == 2) { foreach (Gm g in gm) { g.X = px; g.Y = py; } }
					else if (r == 3) freezeT = 4f; else { for (int j = 0; j < en.Count; j++) { En e = en[j]; if (Math.Abs(e.X - px) < 220f && Math.Abs(e.Y - py) < 320f) { Hurt(e, e.T == 3 ? 120f : 90f); } } Shake = 0.5f; }
				}
			}
		}
	}

	// ── 그리기 ───────────────────────────────
	// 장애물: 민원대(0)·사무실(2)에만 듬성듬성. 플레이어와 적 모두 통과 못 해요
	private bool walking; private float walkPh;
	private static readonly string[] ObSn = { "ms_sn0", "ms_sn1", "ms_sn2", "ms_sn3", "ms_sn4", "ms_sn5", "ms_cu_ball", "ms_cu_crys" }, ObDz = { "ms_dz0", "ms_dz1", "ms_dz2", "ms_dz3", "ms_dz4", "ms_dz5" }, ObOf = { "ms_of_pc", "ms_of_tel", "ms_of_stap", "ms_of_paper", "ms_of_note" }, ObOw = { "ms_ow_cab", "ms_ow_plant", "ms_ow_lamp", "ms_ow_board" };
	private static bool PjS(Graphics g, string n, float x, float y, float w, float rot)
	{
		Bitmap b = Spr.Get("ms_" + n); if (b == null) return false; Spr.Draw(g, "ms_" + n, x, y, w, w * b.Height / b.Width, rot); return true;
	}
	private static bool Obst(int tx, int ty, out float ox, out float oy, out int kind)
	{
		ox = oy = 0f; kind = 0; if (stage != 0 && stage != 2 && stage != 6 && stage != 7) return false;
		if (Math.Abs(tx) <= 1 && Math.Abs(ty) <= 1) return false;
		int h = ((tx * 73856093) ^ (ty * 19349663)) & 0x7fffffff; if (h % 13 != 0) return false;
		ox = tx * 80f + 40f; oy = ty * 80f + 40f; kind = (h / 13) % (stage == 0 ? 5 : (stage == 2 ? 4 : (stage == 7 ? 8 : 6))); return true;
	}
	private static void PushOut(ref float x, ref float y, float rad)
	{
		int cx = (int)Math.Floor(x / 80f), cy = (int)Math.Floor(y / 80f);
		for (int i = cx - 1; i <= cx + 1; i++) for (int j = cy - 1; j <= cy + 1; j++)
		{
			float ox, oy; int kd; if (!Obst(i, j, out ox, out oy, out kd)) continue;
			float dx = x - ox, dy = y - oy, d = (float)Math.Sqrt(dx * dx + dy * dy), m = 20f + rad; if (d < m) { if (d < 0.01f) { dx = 1f; d = 1f; } x = ox + dx / d * m; y = oy + dy / d * m; }
		}
	}
	public override void Draw(Graphics g, double t)
	{
		float cx = px - 200f, cy = py - 330f;
		g.FillRectangle(Br(StCol[stage]), 0, 0, W, H);
		Bitmap flb = stage == 3 ? Spr.Get("bg_t_grass") : (stage == 4 ? Spr.Get("bg_t_conc") : (stage == 5 ? Spr.Get("bg_t_dirt") : (stage == 6 ? Spr.Get("bg_t_sand") : (stage == 7 ? Spr.Get("bg_t_snow") : null))));
		if (flb != null)
		{
			float tsz = 256f; int fx0 = (int)Math.Floor(cx / tsz), fy0 = (int)Math.Floor(cy / tsz); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			for (int fy = fy0; fy <= fy0 + 3; fy++) for (int fx = fx0; fx <= fx0 + 2; fx++) g.DrawImage(flb, new RectangleF((float)Math.Floor(fx * tsz - cx), (float)Math.Floor(fy * tsz - cy), tsz + 1f, tsz + 1f), new RectangleF(0, 0, flb.Width, flb.Height), GraphicsUnit.Pixel);
			if (stage == 5) g.FillRectangle(Br(Color.FromArgb(110, 24, 22, 70)), 0, 0, W, H);
			if (stage == 7) g.FillRectangle(Br(Color.FromArgb(90, 70, 100, 150)), 0, 0, W, H);
		}
		int tx0 = (int)Math.Floor(cx / 80f) - 1, ty0 = (int)Math.Floor(cy / 80f) - 1;
		for (int ty = ty0; ty <= ty0 + 9; ty++) for (int tx = tx0; tx <= tx0 + 6; tx++)
		{
			float sx = tx * 80f - cx, sy = ty * 80f - cy; int h = ((tx * 73856093) ^ (ty * 19349663)) & 0x7fffffff;
			if (flb == null && ((tx + ty) & 1) == 0) g.FillRectangle(Br(Color.FromArgb(stage == 5 ? 14 : 24, 255, 255, 255)), sx, sy, 80, 80);
			int k = h % 9; float ox = h % 50 + 8, oy = (h / 7) % 50 + 8;
			if (flb != null && stage >= 6) { }
			else if (flb != null && stage == 3) { if (k == 0) g.FillEllipse(Br(Color.FromArgb(120, 150, 150, 160)), sx + ox, sy + oy, 16, 11); }
			else if (stage == 3)
			{
				if (k == 0) g.FillEllipse(Br(Color.FromArgb(120, 150, 150, 160)), sx + ox, sy + oy, 16, 11);
				else if (k <= 2) g.FillEllipse(Br(k == 1 ? Color.FromArgb(255, 220, 230) : Color.FromArgb(255, 235, 140)), sx + h % 60 + 8, sy + (h / 5) % 60 + 8, 6, 6);
				else if (k == 3) using (Pen p = new Pen(Color.FromArgb(70, 30, 80, 40), 2f)) { float gx = sx + h % 60 + 8, gy = sy + (h / 3) % 60 + 14; g.DrawLine(p, gx, gy, gx - 3, gy - 8); g.DrawLine(p, gx, gy, gx + 3, gy - 8); }
			}
			else if (stage == 0)   // 민원대 타일 + 번호표
			{
				using (Pen p = new Pen(Color.FromArgb(40, 255, 255, 255), 1f)) { g.DrawLine(p, sx, sy, sx + 80, sy); g.DrawLine(p, sx, sy, sx, sy + 80); }
				
			}
			else if (stage == 1)   // 의회 카펫 + 금빛 마름모
			{
				using (Pen p = new Pen(Color.FromArgb(46, 255, 214, 120), 1.6f)) g.DrawRectangle(p, sx + 4, sy + 4, 72, 72);
				if (k <= 2) g.FillPolygon(Br(Color.FromArgb(70, 255, 214, 120)), new PointF[] { new PointF(sx + 40, sy + 28), new PointF(sx + 52, sy + 40), new PointF(sx + 40, sy + 52), new PointF(sx + 28, sy + 40) });
			}
			else if (stage == 2)   // 사무실 타일 + 책상 + 화분
			{
				using (Pen p = new Pen(Color.FromArgb(34, 255, 255, 255), 1f)) { g.DrawLine(p, sx, sy, sx + 80, sy); g.DrawLine(p, sx, sy, sx, sy + 80); }
			}
			else if (stage == 4)   // 재해복구: 잔해 + 균열 + 안전콘
			{
				if (k <= 1) using (Pen p = new Pen(Color.FromArgb(120, 30, 28, 26), 2f)) { float gx = sx + ox, gy = sy + oy; g.DrawLines(p, new PointF[] { new PointF(gx, gy), new PointF(gx + 9, gy + 6), new PointF(gx + 6, gy + 14), new PointF(gx + 17, gy + 22) }); }
				else if (k == 2) g.FillPolygon(Br(Color.FromArgb(140, 130, 120, 112)), new PointF[] { new PointF(sx + ox, sy + oy + 10), new PointF(sx + ox + 8, sy + oy), new PointF(sx + ox + 20, sy + oy + 6), new PointF(sx + ox + 16, sy + oy + 14) });
				else if (k == 3) { g.FillPolygon(Br(Color.FromArgb(230, 255, 130, 40)), new PointF[] { new PointF(sx + ox, sy + oy + 18), new PointF(sx + ox + 6, sy + oy), new PointF(sx + ox + 12, sy + oy + 18) }); g.FillRectangle(Br(Color.FromArgb(240, 255, 255, 255)), sx + ox + 3, sy + oy + 8, 6, 3); }
			}
			else   // 축제현장: 밤하늘빛 광장 + 색종이 + 조명
			{
				if (k <= 4) g.FillEllipse(Br(Color.FromArgb(150, (h >> 3) % 2 == 0 ? 255 : 90, (h >> 5) % 2 == 0 ? 210 : 120, (h >> 7) % 2 == 0 ? 90 : 230)), sx + ox, sy + oy, 5, 5);
				if (k == 5) { g.FillEllipse(Br(Color.FromArgb(40, 255, 230, 140)), sx + ox - 8, sy + oy - 8, 34, 34); g.FillEllipse(Br(Color.FromArgb(220, 255, 240, 170)), sx + ox + 4, sy + oy + 4, 8, 8); }
			}
		}
		if (stage == 0 || stage == 2 || stage == 6 || stage == 7)
		{
			string[] on = stage == 7 ? ObSn : (stage == 6 ? ObDz : (stage == 0 ? ObOf : ObOw)); float oht = stage == 0 ? 46f : (stage >= 6 ? 58f : 52f);
			int ox0 = (int)Math.Floor(cx / 80f) - 1, oy0 = (int)Math.Floor(cy / 80f) - 1;
			for (int ty = oy0; ty <= oy0 + 9; ty++) for (int tx = ox0; tx <= ox0 + 6; tx++)
			{
				float wx, wy; int kd; if (!Obst(tx, ty, out wx, out wy, out kd)) continue; float sx = wx - cx, sy = wy - cy;
				Bitmap ob = Spr.Get(on[kd]); if (ob == null) continue; float oh2 = oht, ow2 = oh2 * ob.Width / ob.Height; if (ow2 > 60f) { ow2 = 60f; oh2 = ow2 * ob.Height / ob.Width; }
				g.FillEllipse(Br(Color.FromArgb(70, 0, 0, 0)), sx - ow2 * 0.42f, sy + 10, ow2 * 0.84f, 14);
				Spr.Draw(g, on[kd], sx, sy + 20f - oh2 / 2f, ow2, oh2, 0f);
			}
		}
		if (stage == 7) Snow(g, Environment.TickCount / 1000.0, 16);
		// 하트·유물
		foreach (Pk k in pk)
		{
			float sx = k.X - cx, sy = k.Y - cy + (float)Math.Sin(k.T * 4) * 3f;
			if (sx < -30 || sx > W + 30 || sy < -30 || sy > H + 30) continue;
			if (k.K == 0 && Spr.Get("ms_of_mug") != null) { g.FillEllipse(Br(Color.FromArgb(40, 255, 255, 255)), sx - 17, sy - 13, 34, 30); Spr.Draw(g, "ms_of_mug", sx, sy - 1f, 27f, 26f, 0f); using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 1.6f)) { g.DrawLine(p, sx - 3, sy - 14, sx - 5, sy - 20); g.DrawLine(p, sx + 3, sy - 14, sx + 5, sy - 20); } }
			else if (k.K == 0) { using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 250, 245))) g.FillPath(b, Gfx.Round(new RectangleF(sx - 9, sy - 8, 18, 17), 5)); using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 90, 50))) g.FillRectangle(b, sx - 7, sy - 6, 14, 5); using (Pen p = new Pen(Color.FromArgb(120, 130, 150), 2f)) { g.DrawArc(p, sx + 4, sy - 5, 9, 9, -80, 160); } using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 1.6f)) { g.DrawLine(p, sx - 3, sy - 12, sx - 5, sy - 18); g.DrawLine(p, sx + 3, sy - 12, sx + 5, sy - 18); } }
			else if (k.K == 30) DrawChest(g, sx, sy, 1.15f, 0f, t, true);
			else DrawRelic(g, sx, sy, k.K - 10, t);
		}
		// 경험치 구슬
		foreach (Gm m in gm) { float sx = m.X - cx, sy = m.Y - cy; if (sx < -10 || sx > W + 10 || sy < -10 || sy > H + 10) continue; if (Spr.Get("ms_svg0") != null) { if (m.V >= 20) Sv(g, "ms_svg5", sx, sy, 24f); else if (m.V >= 4) Sv(g, "ms_svg4", sx, sy, 16f); else if (m.V >= 2) Sv(g, "ms_svg1", sx, sy, 14f); else Sv(g, "ms_svg0", sx, sy, 11f); continue; } using (SolidBrush b = new SolidBrush(m.V >= 4 ? Color.FromArgb(255, 200, 60) : Color.FromArgb(90, 200, 255))) g.FillEllipse(b, sx - 4, sy - 4, 8, 8); }
		// 도장 쾅 예고·충격파
		foreach (St q in stp)
		{
			float sx = q.X - cx, sy = q.Y - cy;
			if (!q.Hit) { float k = q.T / 0.5f; if (Spr.Get("ms_wsta0") != null) DrawFx(g, "wsta" + Math.Min(3, (int)((1f - Math.Max(0f, k)) * 4f)), sx, sy - 16f - 70f * Math.Max(0f, k), q.R * 1.1f, 0.95f); g.FillEllipse(Br(Color.FromArgb(40, 235, 70, 80)), sx - q.R, sy - q.R, q.R * 2, q.R * 2); using (Pen pp = new Pen(Color.FromArgb(170, 235, 70, 80), 2f)) { g.DrawEllipse(pp, sx - q.R, sy - q.R, q.R * 2, q.R * 2); float ri = q.R * (1f - k); g.DrawEllipse(pp, sx - ri, sy - ri, ri * 2, ri * 2); } }
			else { float k = q.Fl / 0.35f, rr = q.R * (1.15f - 0.15f * k); DrawFx(g, "e_hit", sx, sy, q.R * 1.5f, k); DrawFx(g, Spr.Get("ms_wsta4") != null ? "wsta" + (4 + Math.Min(3, (int)((1f - k) * 4f))) : "pj5", sx, sy, q.R * 2.5f, k); g.FillEllipse(Br(Color.FromArgb((int)(120 * k), 235, 70, 80)), sx - rr, sy - rr, rr * 2, rr * 2); using (Pen pp = new Pen(Color.FromArgb((int)(255 * k), 255, 220, 200), 4f)) g.DrawEllipse(pp, sx - rr, sy - rr, rr * 2, rr * 2); using (Pen pp = new Pen(Color.FromArgb((int)(220 * k), 200, 30, 50), 3f)) g.DrawEllipse(pp, sx - rr * 0.55f, sy - rr * 0.55f, rr * 1.1f, rr * 1.1f); Txt(g, "결", Math.Max(12f, q.R * 0.32f), 2, Color.FromArgb((int)(240 * k), 200, 30, 50), sx - 30, sy - 24, 60, 48, StringAlignment.Center); }
		}
		// 수호 기운 (룬 장판)
		if (wl[4] > 0)
		{
			float r = (52f + 11f * wl[4]) * (1f + 0.12f * pl[7]), sx = px - cx, sy = py - cy;
			g.FillEllipse(Br(Color.FromArgb(30 + (int)(10 * Math.Sin(t * 5)), 200, 140, 255)), sx - r, sy - r, r * 2, r * 2);
			if (Spr.Get("ms_pj4") != null) { Bitmap ab = Spr.Get("ms_pj4"); float aw = r * 2.15f, ah = aw * 0.8f; g.FillEllipse(Br(Color.FromArgb(14, 160, 90, 255)), sx - aw / 2, sy - ah / 2 + 4, aw, ah); DrawFx(g, Spr.Get("ms_waur0") != null ? "waur" + ((int)(t * 9) & 3) : "pj4", sx, sy, aw * (1f + 0.02f * (float)Math.Sin(t * 5)), 0.9f); }
			else using (Pen pp = new Pen(Color.FromArgb(120, 220, 170, 255), 2f)) { g.DrawEllipse(pp, sx - r, sy - r, r * 2, r * 2); g.DrawEllipse(pp, sx - r * 0.72f, sy - r * 0.72f, r * 1.44f, r * 1.44f); }
			if (Spr.Get("ms_pj4") == null) using (Pen pp = new Pen(Color.FromArgb(170, 240, 210, 255), 2.4f)) for (int i = 0; i < 12; i++) { double a2 = t * 0.9 + i * Math.PI / 6; float c1 = (float)Math.Cos(a2), s1 = (float)Math.Sin(a2); g.DrawLine(pp, sx + c1 * (r - 9), sy + s1 * (r - 9), sx + c1 * (r - 1), sy + s1 * (r - 1)); }
			if (Spr.Get("ms_pj4") == null) using (Pen pp = new Pen(Color.FromArgb(100, 240, 210, 255), 1.6f)) for (int i = 0; i < 6; i++) { double a2 = -t * 0.6 + i * Math.PI / 3; g.DrawLine(pp, sx + (float)Math.Cos(a2) * r * 0.72f, sy + (float)Math.Sin(a2) * r * 0.72f, sx + (float)Math.Cos(a2 + 2.094) * r * 0.72f, sy + (float)Math.Sin(a2 + 2.094) * r * 0.72f); }
		}
		// 커피 웅덩이
		foreach (Pu u in pud)
		{
			float sx = u.X - cx, sy = u.Y - cy; if (sx < -90 || sx > W + 90 || sy < -90 || sy > H + 90) continue;
			float fade = Math.Min(1f, (u.Life - u.T) * 1.5f), rr = u.R * (0.88f + 0.12f * (float)Math.Sin(u.T * 3));
			if (Spr.Get("ms_pj7") != null) { DrawFx(g, Spr.Get("ms_wcof5") != null ? "wcof" + (5 + ((int)(t * 3) % 3)) : "pj7", sx, sy - rr * 0.1f, rr * 2.3f, fade); continue; }
			g.FillEllipse(Br(Color.FromArgb((int)(120 * fade), 92, 56, 34)), sx - rr, sy - rr * 0.78f, rr * 2, rr * 1.56f);
			g.FillEllipse(Br(Color.FromArgb((int)(120 * fade), 140, 92, 56)), sx - rr * 0.74f, sy - rr * 0.58f, rr * 1.48f, rr * 1.1f);
			g.FillEllipse(Br(Color.FromArgb((int)(90 * fade), 235, 205, 160)), sx - rr * 0.3f, sy - rr * 0.3f, rr * 0.6f, rr * 0.4f);
			for (int i = 0; i < 3; i++) { float ph = (float)((u.T * 0.8 + i * 0.33) % 1.0); g.FillEllipse(Br(Color.FromArgb((int)(110 * (1f - ph) * fade), 255, 255, 255)), sx + (i - 1) * rr * 0.4f - 4f, sy - ph * 34f - 4f, 8, 8 - ph * 3f); }
		}
		// 낙하물·폭죽 예고
		foreach (Hz h in hzs)
		{
			float sx = h.X - cx, sy = h.Y - cy; if (sx < -90 || sx > W + 90 || sy < -90 || sy > H + 90) continue;
			Color hc = h.K == 0 ? Color.FromArgb(255, 160, 60) : Color.FromArgb(255, 90, 190);
			if (!h.Done)
			{
				float tot = h.K == 0 ? 1.5f : 1.3f, k = 1f - Math.Max(0f, h.T) / tot, rr = h.R;
				g.FillEllipse(Br(Color.FromArgb(34 + (int)(30 * k), hc)), sx - rr, sy - rr, rr * 2, rr * 2);
				using (Pen pp = new Pen(Color.FromArgb(190, hc), 2f)) { g.DrawEllipse(pp, sx - rr, sy - rr, rr * 2, rr * 2); float ri = rr * k; g.DrawEllipse(pp, sx - ri, sy - ri, ri * 2, ri * 2); }
				if (h.K == 0) { float fy2 = sy - (1f - k) * 220f; g.FillPolygon(Br(Color.FromArgb(230, 120, 110, 100)), new PointF[] { new PointF(sx - 12, fy2), new PointF(sx - 4, fy2 - 11), new PointF(sx + 9, fy2 - 8), new PointF(sx + 13, fy2 + 3), new PointF(sx, fy2 + 10) }); }
				else { float fy2 = sy - (1f - k) * 240f; g.FillEllipse(Br(Color.FromArgb(240, 255, 230, 120)), sx - 4, fy2 - 4, 8, 8); }
			}
			else
			{
				float k = h.Fl / 0.4f, rr = h.R * (1.7f - 0.7f * k);
				g.FillEllipse(Br(Color.FromArgb((int)(130 * k), hc)), sx - rr, sy - rr, rr * 2, rr * 2);
				using (Pen pp = new Pen(Color.FromArgb((int)(255 * k), 255, 245, 200), 3f)) g.DrawEllipse(pp, sx - rr, sy - rr, rr * 2, rr * 2);
				if (h.K == 1) for (int i = 0; i < 10; i++) { double a = i * 0.628 + 0.3; float d = h.R * (1f - k) * 1.5f + 6f; g.FillEllipse(Br(Color.FromArgb((int)(240 * k), i % 2 == 0 ? 255 : 120, i % 3 == 0 ? 255 : 160, i % 2 == 0 ? 120 : 255)), sx + (float)Math.Cos(a) * d - 3, sy + (float)Math.Sin(a) * d - 3, 6, 6); }
			}
		}
		// 돌진 예고선
		foreach (En e in en) if (e.Pat == 1) { float sx = e.X - cx, sy = e.Y - cy, w = e.IsBoss ? e.R * 1.4f : e.R * 1.2f; using (Pen pp = new Pen(Color.FromArgb(70 + (int)(50 * Math.Sin(t * 30)), 255, 60, 60), w)) { pp.StartCap = LineCap.Flat; pp.EndCap = LineCap.Flat; g.DrawLine(pp, sx, sy, sx + e.Dx * 230f, sy + e.Dy * 230f); } }
		// 쓰러지는 민원 (죽을 때 모션)
		foreach (Cp c in cps) CorpseDraw(g, c, c.X - cx, c.Y - cy);
		// 민원
		string[] bnm = BName;
		foreach (En e in en)
		{
			float sx = e.X - cx, sy = e.Y - cy; if (sx < -60 || sx > W + 60 || sy < -60 || sy > H + 60) continue;
			if (e.Sq > 0f) { sx += e.Rx * e.Sq; sy += e.Ry * e.Sq; }
			g.FillEllipse(Br(Color.FromArgb(60, 0, 0, 0)), sx - e.R, sy + e.R * 0.5f, e.R * 2, e.R * 0.7f);
			if (e.IsBoss) Vfx.Glow(g, sx, sy - e.R * 0.2f, e.R * 2.3f, e.R * 2.1f, BossCol[e.Bi], 0.55f + 0.2f * (float)Math.Sin(t * 4));
			else if (e.Elite) Vfx.Glow(g, sx, sy - e.R * 0.2f, e.R * 1.8f, e.R * 1.6f, Color.FromArgb(255, 210, 60), 0.6f + 0.25f * (float)Math.Sin(t * 5));
			if (stage == 1 && Spr.Get("ms_op0") != null) OpDraw(g, e, sx, sy, t);
			else if (stage >= 2 && StDraw(g, e, sx, sy, t)) { }
			else if (stage == 0 && e.T <= 3 && Spr.Get("ms_ecu0") != null) FoeDraw(g, e, sx, sy, t);   // 민원대: 같은 그림체의 민원인들로 통일
			else if (e.T == 0 || e.T == 2 || e.T == 3) { ZombieDraw(g, e, sx, sy, t); }
			else if (e.T <= 3)
			{
				Bitmap fb = Spr.Get("foe" + e.T);
				if (fb != null)
				{
					float fw = e.R * (e.T == 3 ? 3.3f : 2.8f), bob = (float)Math.Sin(t * 7 + e.X * 0.05f) * (e.T == 1 ? 3.5f : 1.5f);
					RectangleF dr = new RectangleF(sx - fw / 2f, sy - fw * 0.56f + bob, fw, fw);
					int ti = e.Hit > 0 ? 1 : (freezeT > 0 ? 2 : (e.IsBoss && e.Bi > 0 ? 3 + e.Bi : 0));
					g.InterpolationMode = InterpolationMode.Bilinear; System.Drawing.Image fbs = ImgCache.Get(fb, dr.Width * ImgCache.Scale(g), dr.Height * ImgCache.Scale(g));
					if (ti != 0) fbs = Tinted(fbs, e.T, ti);   // 색 보정은 한 번만 구워 두고 재사용 (매 프레임 색행렬 변환은 느려요)
					g.DrawImage(fbs, dr, new RectangleF(0, 0, fbs.Width, fbs.Height), GraphicsUnit.Pixel);
				}
			}
			else FoeDraw(g, e, sx, sy, t);
			if (e.Elite && !e.IsBoss || e.T == 2 || e.T == 6 || e.T == 7) { float w = e.R * 2f; g.FillRectangle(Br(Color.FromArgb(120, 0, 0, 0)), sx - e.R, sy - e.R - 9f, w, 4); g.FillRectangle(Br(Color.FromArgb(240, 80, 80)), sx - e.R, sy - e.R - 9f, w * Math.Max(0f, e.Hp / e.MaxHp), 4); }
		}
		// 방울 (꼬리 달린 구슬)
		if (wl[1] > 0)
		{
			int L = wl[1]; int n = Math.Min(5, 1 + L / 2) + (evo[0] ? 2 : 0); float rad = 64f + (evo[0] ? 14f : 0f);
			for (int i = 0; i < n; i++)
			{
				Color c = evo[0] ? Color.FromArgb(255, 130, 50) : Color.FromArgb(110, 200, 255);
				if (!evo[0] && Spr.Get("ms_pj1") != null) { float oa = orbA + i * (float)Math.PI * 2f / n; if (!PjA(g, "wbub", t + i * 0.1, px - cx + (float)Math.Cos(oa) * rad, py - cy + (float)Math.Sin(oa) * rad, 36f, 0f)) PjS(g, "pj1", px - cx + (float)Math.Cos(oa) * rad, py - cy + (float)Math.Sin(oa) * rad, 34f, 0f); continue; }
				for (int k = 4; k >= 1; k--) { float a = orbA + i * (float)Math.PI * 2f / n - k * 0.13f, sx = px - cx + (float)Math.Cos(a) * rad, sy = py - cy + (float)Math.Sin(a) * rad, rr = 9f - k * 1.3f; g.FillEllipse(Br(Color.FromArgb(70 - k * 12, c)), sx - rr, sy - rr, rr * 2, rr * 2); }
				float a0 = orbA + i * (float)Math.PI * 2f / n, ox = px - cx + (float)Math.Cos(a0) * rad, oy = py - cy + (float)Math.Sin(a0) * rad;
				g.FillEllipse(Br(Color.FromArgb(70, c)), ox - 16, oy - 16, 32, 32);
				g.FillEllipse(Br(c), ox - 10, oy - 10, 20, 20);
				g.FillEllipse(Br(Theme.Mix(c, Color.White, 0.55)), ox - 6, oy - 7, 10, 9);
				g.FillEllipse(Br(Color.FromArgb(230, 255, 255, 255)), ox - 4, oy - 6, 4, 3);
				using (Pen pp = new Pen(Color.FromArgb(160, Theme.Mix(c, Color.White, 0.7)), 1.5f)) g.DrawEllipse(pp, ox - 10, oy - 10, 20, 20);
			}
		}
		// 적 탄환
		foreach (Ep q in eps)
		{
			float sx = q.X - cx, sy = q.Y - cy; if (sx < -20 || sx > W + 20 || sy < -20 || sy > H + 20) continue;
			if (Spr.Get("ms_pj6") != null && Spr.Get("ms_e_atk") != null)
			{
				if (q.Kind == 0) { float an0 = (float)(Math.Atan2(q.Vy, q.Vx) * 57.2958) + 180f; Vfx.Draw(g, Vfx.Tint(Spr.Get("ms_pj6"), "enemy"), sx, sy, 26f, 26f * Spr.Get("ms_pj6").Height / Spr.Get("ms_pj6").Width, an0, 1f, false); }
				else { float pu = 1f + 0.12f * (float)Math.Sin(t * 18 + q.X); Vfx.Glow(g, sx, sy, 17f, 17f, Color.FromArgb(255, 70, 90), 0.5f); Vfx.Draw(g, Vfx.Tint(Spr.Get("ms_e_atk"), "pink"), sx, sy, 20f * pu, 20f * pu * Spr.Get("ms_e_atk").Height / Spr.Get("ms_e_atk").Width, (float)(t * 120), 1f, false); }
				continue;
			}
			if (q.Kind == 0)
			{
				GraphicsState st2 = g.Save(); g.TranslateTransform(sx, sy); g.RotateTransform((float)(Math.Atan2(q.Vy, q.Vx) * 180 / Math.PI));
				g.FillPolygon(Br(Color.FromArgb(250, 250, 255)), new PointF[] { new PointF(10, 0), new PointF(-8, -6), new PointF(-4, 0), new PointF(-8, 6) });
				g.FillPolygon(Br(Color.FromArgb(190, 200, 225)), new PointF[] { new PointF(10, 0), new PointF(-4, 0), new PointF(-8, 6) });
				using (Pen pp = new Pen(Color.FromArgb(150, 90, 110, 160), 1f)) g.DrawPolygon(pp, new PointF[] { new PointF(10, 0), new PointF(-8, -6), new PointF(-4, 0), new PointF(-8, 6) });
				g.Restore(st2);
			}
			else { g.FillEllipse(Br(Color.FromArgb(70, 255, 60, 80)), sx - 11, sy - 11, 22, 22); g.FillEllipse(Br(Color.FromArgb(255, 220, 40, 60)), sx - 6.5f, sy - 6.5f, 13, 13); g.FillEllipse(Br(Color.FromArgb(220, 255, 200, 200)), sx - 3.5f, sy - 4.5f, 5, 4); }
		}
		// 투사체
		foreach (Pr p in pr)
		{
			float sx = p.X - cx, sy = p.Y - cy;
			if (p.Kind == 0)
			{
				float vl = (float)Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy) + 0.01f, bx = -p.Vx / vl, by = -p.Vy / vl;
				if (PjA(g, "wfire", t, sx + bx * 4f, sy + by * 4f, evo[0] ? 68f : 54f, (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI))) continue;
				if (PjS(g, "pj0", sx + bx * 9f, sy + by * 9f, evo[0] ? 54f : 44f, (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI))) continue;
				for (int k = 5; k >= 1; k--) { float rr = 7f - k * 0.9f; g.FillEllipse(Br(Color.FromArgb(150 - k * 25, 255, 110 + k * 12, 30)), sx + bx * k * 7f - rr, sy + by * k * 7f - rr, rr * 2, rr * 2); }
				g.FillEllipse(Br(Color.FromArgb(80, 255, 130, 40)), sx - 12, sy - 12, 24, 24); g.FillEllipse(Br(Color.FromArgb(255, 220, 80)), sx - 6.5f, sy - 6.5f, 13, 13); g.FillEllipse(Br(Color.FromArgb(255, 255, 240)), sx - 3.2f, sy - 3.2f, 6.4f, 6.4f);
			}
			else if (p.Kind == 2)   // 서류 비행기
			{
				if (PjA(g, "wpla", t, sx, sy, 50f, (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI) + 18f)) continue;
				if (PjS(g, "pj6", sx, sy, 44f, (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI) + 180f)) continue;
				float ang = (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI), vl = (float)Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy) + 0.01f;
				for (int k = 4; k >= 1; k--) g.FillEllipse(Br(Color.FromArgb(70 - k * 14, 220, 235, 255)), sx - p.Vx / vl * k * 6f - 3f, sy - p.Vy / vl * k * 6f - 3f, 6, 6);
				GraphicsState st2 = g.Save(); g.TranslateTransform(sx, sy); g.RotateTransform(ang);
				PointF[] w1 = { new PointF(11, 0), new PointF(-8, -8), new PointF(-4, 0) }, w2 = { new PointF(11, 0), new PointF(-8, 8), new PointF(-4, 0) };
				g.FillPolygon(Br(Color.FromArgb(252, 252, 255)), w1); g.FillPolygon(Br(Color.FromArgb(185, 200, 228)), w2);
				using (Pen pp = new Pen(Color.FromArgb(160, 100, 120, 170), 1f)) { g.DrawPolygon(pp, w1); g.DrawPolygon(pp, w2); }
				g.Restore(st2);
			}
			else if (p.Kind == 3)   // 스테이플러 철침
			{
				if (PjA(g, "wstp", t, sx, sy, 38f, (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI))) continue;
				if (PjS(g, "pj8", sx, sy, 40f, (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI))) continue;
				float ang = (float)(Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI);
				GraphicsState st2 = g.Save(); g.TranslateTransform(sx, sy); g.RotateTransform(ang);
				g.FillEllipse(Br(Color.FromArgb(70, 255, 240, 160)), -12, -5, 24, 10);
				using (Pen pp = new Pen(Color.FromArgb(240, 215, 220, 235), 2.4f)) { g.DrawLine(pp, -6, -3, 5, -3); g.DrawLine(pp, -6, -3, -6, 3); g.DrawLine(pp, -6, 3, 5, 3); }
				g.Restore(st2);
			}
			else
			{
				if (!evo[2] && PjA(g, "wboo", t, sx, sy, p.R * 4.4f, 0f)) continue;
				if (!evo[2] && PjS(g, "pj3", sx, sy, p.R * 4.2f, p.Age * 720f)) continue;
				GraphicsState st2 = g.Save(); g.TranslateTransform(sx, sy); g.RotateTransform(p.Age * 720f); float R = p.R; Color bc = evo[2] ? Color.FromArgb(255, 150, 70) : Color.FromArgb(150, 255, 160);
				g.FillEllipse(Br(Color.FromArgb(55, bc)), -R * 1.3f, -R * 1.3f, R * 2.6f, R * 2.6f);
				for (int k = 0; k < 4; k++)
				{
					PointF[] bl = { new PointF(0, 0), new PointF(R * 0.35f, -R * 0.32f), new PointF(R * 1.05f, -R * 0.1f), new PointF(R * 0.5f, R * 0.12f) };
					g.FillPolygon(Br(bc), bl); g.FillPolygon(Br(Theme.Mix(bc, Color.White, 0.5)), new PointF[] { bl[0], bl[1], new PointF(R * 0.7f, -R * 0.13f) });
					using (Pen pp = new Pen(Color.FromArgb(190, 255, 255, 255), 1.2f)) g.DrawPolygon(pp, bl);
					g.RotateTransform(90f);
				}
				g.FillEllipse(Br(Color.White), -R * 0.22f, -R * 0.22f, R * 0.44f, R * 0.44f);
				g.Restore(st2);
			}
		}
		// 번개
		foreach (Bolt bo in bolts)
		{
			float fa = Math.Min(1f, bo.T * 6f); Random zr = new Random((int)(bo.X2 * 31 + bo.Y2 * 17)); PointF[] z = new PointF[9];
			for (int i = 0; i < 9; i++) { float u = i / 8f, jx = i == 0 || i == 8 ? 0f : (float)(zr.NextDouble() - 0.5) * 24f, jy = i == 0 || i == 8 ? 0f : (float)(zr.NextDouble() - 0.5) * 8f; z[i] = new PointF(bo.X1 + (bo.X2 - bo.X1) * u - cx + jx, bo.Y1 + (bo.Y2 - bo.Y1) * u - cy + jy); }
			using (Pen pp = new Pen(Color.FromArgb((int)(60 * fa), 120, 170, 255), bo.Big ? 13f : 9f)) { pp.LineJoin = LineJoin.Round; g.DrawLines(pp, z); }
			using (Pen pp = new Pen(Color.FromArgb((int)(220 * fa), 255, 240, 120), bo.Big ? 5f : 3.2f)) { pp.LineJoin = LineJoin.Round; g.DrawLines(pp, z); }
			using (Pen pp = new Pen(Color.FromArgb((int)(255 * fa), 255, 255, 255), bo.Big ? 2.2f : 1.4f)) g.DrawLines(pp, z);
			float fr2 = bo.Big ? 24f : 15f; g.FillEllipse(Br(Color.FromArgb((int)(120 * fa), 255, 240, 140)), bo.X2 - cx - fr2, bo.Y2 - cy - fr2 * 0.7f, fr2 * 2, fr2 * 1.4f);
			DrawFx(g, "e_atk", bo.X2 - cx, bo.Y2 - cy, fr2 * 3.4f, fa); if (Spr.Get("ms_pj2") != null) { float bw = bo.Big ? 70f : 48f; DrawFx(g, Spr.Get("ms_wlig0") != null ? "wlig" + ((int)(t * 12) & 3) : "pj2", bo.X2 - cx, bo.Y2 - cy - bw * 0.9f, bw, fa); }
		}

		foreach (Vf v in vfs) { Bitmap vb = Spr.Get(v.Fk != null ? "ms_" + v.Fk + (v.F0 + Math.Min(v.Fn - 1, (int)(v.Age / v.Life * v.Fn))) : "ms_" + v.N); if (vb == null) continue; float u = v.Age / v.Life, ez = 1f - (1f - u) * (1f - u), w = v.W0 + (v.W1 - v.W0) * ez, al = v.A * (u < 0.15f ? 1f : 1f - (u - 0.15f) / 0.85f); float vx2 = v.X - cx, vy2 = v.Y - cy; if (vx2 < -60 || vx2 > W + 60 || vy2 < -60 || vy2 > H + 60) continue; Vfx.Draw(g, Vfx.Tint(vb, v.Tn), vx2, vy2, w, w * vb.Height / vb.Width, v.Rot, al, false); }
		if (freezeT > 0f && Spr.Get("ms_tm0") != null) { float fa2 = Math.Min(1f, freezeT * 2f); DrawFx(g, "tm0", W / 2f, 112f, 54f + 4f * (float)Math.Sin(t * 8), fa2); }
		// 레이저 포인터
		if (wl[9] > 0)
		{
			int L = wl[9], nb = evo[5] ? 3 : 1; float len = (150f + 14f * L) * (1f + 0.12f * pl[7]), psx = px - cx, psy = py - cy;
			for (int b2 = 0; b2 < nb; b2++)
			{
				float a = beamA + b2 * 2.0944f, ex = psx + (float)Math.Cos(a) * len, ey = psy + (float)Math.Sin(a) * len;
				Color bc = evo[5] ? Color.FromArgb(255, 120, 230) : Color.FromArgb(255, 70, 110);
				using (Pen pp = new Pen(Color.FromArgb(46, bc), 14f)) { pp.StartCap = LineCap.Round; pp.EndCap = LineCap.Round; g.DrawLine(pp, psx, psy, ex, ey); }
				using (Pen pp = new Pen(Color.FromArgb(150, bc), 5f)) { pp.StartCap = LineCap.Round; pp.EndCap = LineCap.Round; g.DrawLine(pp, psx, psy, ex, ey); }
				using (Pen pp = new Pen(Color.FromArgb(240, 255, 245, 245), 1.8f)) g.DrawLine(pp, psx, psy, ex, ey);
				{ Bitmap lb = Spr.Get("ms_pj9"); if (Spr.Get("ms_wlas20") != null && !evo[5]) { Bitmap lw = Spr.Get("ms_wlas2" + ((int)(t * 14) & 3)); float lww = 70f, lwh = lww * lw.Height / lw.Width; Spr.Draw(g, "ms_wlas2" + ((int)(t * 14) & 3), ex - (float)Math.Cos(a) * lww * 0.5f, ey - (float)Math.Sin(a) * lww * 0.5f, lww, lwh, a * 57.2958f); }
				else if (lb != null && !evo[5]) { float pul = 1f + 0.12f * (float)Math.Sin(t * 14); float fw0 = 64f * pul, dw = fw0 * 0.45f, dh = fw0 * lb.Height / lb.Width; g.DrawImage(lb, new RectangleF(ex - dw * 0.82f, ey - dh / 2f, dw, dh), new RectangleF(lb.Width * 0.55f, 0, lb.Width * 0.45f, lb.Height), GraphicsUnit.Pixel); float ox2 = psx + (float)Math.Cos(a) * 14f, oy2 = psy + (float)Math.Sin(a) * 14f; g.FillEllipse(Br(Color.FromArgb(120, 255, 90, 110)), ox2 - 5, oy2 - 5, 10, 10); }
				else { g.FillEllipse(Br(Color.FromArgb(90, bc)), ex - 9, ey - 9, 18, 18); g.FillEllipse(Br(Color.White), ex - 3.5f, ey - 3.5f, 7, 7); } }
			}
		}
		// 마룡이
		float ppx = 200f, ppy = 330f;
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, ppx - 18, ppy + 22, 36, 11);
		if (hurtT <= 0f || ((int)(t * 20) & 1) == 0)
		{
			bool sd = Math.Abs(fx) > Math.Abs(fy) * 0.9f, up = !sd && fy < 0f; string wp = sd ? "ms_wks" : (up ? "ms_wkb" : "ms_wkf"); int wf = walking ? (int)walkPh % 6 : (sd || up ? 1 : 1); Bitmap wb = Spr.Get(wp + wf);
			if (wb != null) { float wh = 78f, ww = wh * wb.Width / wb.Height; float bob = walking ? (float)Math.Abs(Math.Sin(walkPh * 0.5236 * 1.0)) * 0f : (float)Math.Sin(t * 3) * 1.2f; Spr.Draw(g, wp + wf, ppx, ppy + 34f - wh / 2f + bob, sd && fx < 0f ? -ww : ww, wh, 0f); }
			else DrawMascot(g, ppx, ppy + 34f, 66f, (float)Math.Sin(t * 8) * 3f);
		}
		if (healFx > 0f) DrawFx(g, "e_heal", ppx, ppy - 6f - (1f - healFx) * 30f, 70f + 30f * (1f - healFx), healFx);
		if (skFx > 0f) DrawFx(g, "e_skill", ppx, ppy + 6f, 80f + 260f * (1f - skFx), skFx);
		if (hurtT > 0.25f) DrawFx(g, "e_hit", ppx + 6f, ppy - 6f, 54f, Math.Min(1f, (hurtT - 0.25f) * 4f));
		// 피해 숫자
		foreach (Dn d in dns)
		{
			float k = d.Age / 0.7f; if (k >= 1f) continue; int al = (int)(255 * (k < 0.6f ? 1f : (1f - k) / 0.4f));
			Txt(g, d.V.ToString(), d.Crit ? 13f : 9.5f, 2, d.Crit ? Color.FromArgb(al, 255, 220, 70) : Color.FromArgb(al, 255, 255, 255), d.X - cx - 30, d.Y - cy - 12 - k * 26f, 60, 20, StringAlignment.Center);
		}
		// 유물 방향 표시
		foreach (Pk k in pk)
		{
			if (k.K == 0) continue; float dx = k.X - px, dy = k.Y - py; if (Math.Abs(dx) < 190f && Math.Abs(dy) < 290f) continue;
			float l = (float)Math.Sqrt(dx * dx + dy * dy), ax = 200f + dx / l * 170f, ay = 330f + dy / l * 250f; ax = Clamp(ax, 24f, W - 24f); ay = Clamp(ay, 70f, H - 70f);
			Star(g, ax, ay, 11f, Color.FromArgb(230, 255, 214, 60));
		}
		// 상단 HUD
		float fr = hp / maxHp;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(14, 42, 190, 14), 7)) using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 10, 10, 20))) g.FillPath(b, gp);
		if (fr > 0.01f) using (GraphicsPath gp = Gfx.Round(new RectangleF(14, 42, Math.Max(14f, 190f * fr), 14), 7)) using (SolidBrush b = new SolidBrush(fr > 0.4f ? Color.FromArgb(240, 90, 110) : Color.FromArgb(255, 60, 60))) g.FillPath(b, gp);
		Txt(g, "체력 " + (int)Math.Ceiling(hp) + " / " + (int)maxHp, 9f, 2, Color.White, 20, 41, 180, 16, StringAlignment.Near);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(14, 60, 190, 10), 5)) using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 10, 10, 20))) g.FillPath(b, gp);
		float xf = Math.Min(1f, (float)xp / XpNeed); if (xf > 0.01f) using (GraphicsPath gp = Gfx.Round(new RectangleF(14, 60, Math.Max(10f, 190f * xf), 10), 5)) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 200, 255))) g.FillPath(b, gp);
		Txt(g, "Lv." + lvl + " · 처치 " + kills + " · 무기 " + WSlots() + "/6 능력 " + PSlots() + "/5", 10f, 2, Color.White, 212, 40, 190, 22, StringAlignment.Near);
		// 능력 목록
		int ix = 14; for (int i = 0; i < 10; i++) if (wl[i] > 0) { IconW(g, i, ix + 12, 88, 12f, wl[i]); ix += 27; }
		int jx2 = 14; for (int i = 0; i < 11; i++) if (pl[i] > 0) { IconP(g, i, jx2 + 10, 113, 10f, pl[i]); jx2 += 23; }
		int rx = 14; for (int i = 0; i < 8; i++) if (rel[i] > 0) { IconR(g, i, rx + 10, 137, 10f, rel[i]); rx += 23; }
		// 스트레스 게이지
		{
			float sf = Math.Min(1f, stress / 100f); bool full = stress >= 100f;
			Rr(g, Color.FromArgb(130, 10, 10, 20), 212, 60, 174, 10, 5);
			if (sf > 0.01f) Rr(g, full ? Color.FromArgb(255, (int)(150 + 100 * Math.Sin(t * 12) * Math.Sin(t * 12)), 60) : Color.FromArgb(255, 170, 70), 212, 60, Math.Max(10f, 174f * sf), 10, 5);
			Txt(g, full ? "[스페이스/클릭] 퇴근 선언!" : "스트레스 " + (int)stress + "%", 8.5f, 2, Color.White, 212, 70, 174, 14, StringAlignment.Near);
		}
		Txt(g, StName[stage], 9f, 1, Color.FromArgb(200, 255, 255, 255), 14, 576, 120, 18, StringAlignment.Near);
		if (msgT > 0) Txt(g, msg, 13f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), 255, 236, 140), 0, 400, W, 26, StringAlignment.Center);
		if (hurtT > 0.3f) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 0, 0))) g.FillRectangle(b, 0, 0, W, H);
		if (flashT > 0f) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(70 * Math.Min(1f, flashT)), 255, 30, 30))) g.FillRectangle(b, 0, 0, W, H);
		foreach (En e in en) if (e.IsBoss)
		{
			float bf = Math.Max(0f, e.Hp / e.MaxHp);
			Txt(g, BName[e.Bi] + (bf < 0.45f ? "  (분노!)" : ""), 11f, 2, Color.FromArgb(255, 226, 150), 0, 158, W, 20, StringAlignment.Center);
			using (GraphicsPath gp = Gfx.Round(new RectangleF(50, 180, 300, 12), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 10, 20))) g.FillPath(b, gp);
			if (bf > 0.01f) using (GraphicsPath gp = Gfx.Round(new RectangleF(50, 180, Math.Max(12f, 300f * bf), 12), 6)) using (SolidBrush b = new SolidBrush(bf < 0.45f ? Color.FromArgb(255, 70, 40) : BossCol[e.Bi])) g.FillPath(b, gp);
			break;
		}
		if (el < 5f) Hint(g, "방향키 / WASD / 마우스 누르고 끌기로 이동", 450 + 11.0, 11f, false, 1f);
		if (surgeT > 0f) g.FillRectangle(Br(Color.FromArgb(22 + (int)(10 * Math.Sin(t * 6)), 255, 210, 60)), 0, 0, W, H);
		if (surgeT > 0f) Txt(g, "월말 결산! 경험치 2배  " + (int)Math.Ceiling(surgeT) + "초", 11f, 2, Color.FromArgb(255, 255, 220, 100), 0, 190, W, 20, StringAlignment.Center);
		if (clockT > 0f) { float k = Math.Min(1f, clockT / 1.3f); g.FillRectangle(Br(Color.FromArgb((int)(150 * k), 255, 255, 240)), 0, 0, W, H); Txt(g, "정시 퇴근!", 30f, 2, Color.FromArgb((int)(255 * k), 255, 120, 80), 0, 250, W, 50, StringAlignment.Center); }
		if (mode == 2)
		{
			g.FillRectangle(Br(Color.FromArgb(215, 8, 12, 30)), 0, 0, W, H);
			Txt(g, "근무지를 골라요", 22f, 2, Color.FromArgb(255, 226, 120), 0, 62, W, 36, StringAlignment.Center);
			Hint(g, "클릭하거나 1~6 키 · ← → 로 난이도 조절", 90 + 10.0, 10f, false, 1f);
			for (int i = 0; i < 6; i++)
			{
				RectangleF dr = DfRect(i); bool on = i == diff, dh = dr.Contains(Hot.X, Hot.Y); Color dc = Theme.Mix(Color.FromArgb(120, 210, 150), Color.FromArgb(255, 90, 90), i / 5.0);
				using (GraphicsPath gp = Gfx.Round(dr, 11)) { g.FillPath(Br(Color.FromArgb(on ? 235 : (dh ? 110 : 60), on ? dc : Color.FromArgb(255, 255, 255))), gp); using (Pen p = new Pen(Color.FromArgb(on ? 255 : 130, dc), on ? 2.4f : 1.4f)) g.DrawPath(p, gp); }
				Txt(g, DName[i], 10f, 2, on ? Color.FromArgb(20, 24, 40) : Color.White, dr.X, dr.Y, dr.Width, dr.Height, StringAlignment.Center);
			}
			Txt(g, "난이도 " + DName[diff] + " · " + DDesc[diff], 9.5f, 1, Color.FromArgb(235, 255, 226, 150), 0, 146, W, 18, StringAlignment.Center);
			for (int i = 0; i < 8; i++)
			{
				RectangleF r = StRect(i); bool op = Open(i), hot = op && r.Contains(Hot.X, Hot.Y);
				using (GraphicsPath gp = Gfx.Round(r, 16)) { g.FillPath(Br(Color.FromArgb(hot ? 90 : 55, StCol[i])), gp); using (Pen p = new Pen(Color.FromArgb(hot ? 255 : 170, Theme.Mix(StCol[i], Color.White, 0.55)), hot ? 3f : 2f)) g.DrawPath(p, gp); }
				g.FillEllipse(Br(StCol[i]), r.X + 12, r.Y + 12, 34, 34); Txt(g, (i + 1).ToString(), 14f, 2, Color.White, r.X + 12, r.Y + 12, 34, 34, StringAlignment.Center);
				if (op) Txt(g, StName[i], 14f, 2, Color.White, r.X + 54, r.Y + 14, r.Width - 60, 28, StringAlignment.Near);
				if (op) Txt(g, StDesc[i], 9.5f, 0, Color.FromArgb(220, 255, 255, 255), r.X + 12, r.Y + 48, r.Width - 22, 30, StringAlignment.Near);
				if (!op) { g.FillPath(Br(Color.FromArgb(170, 8, 10, 24)), Gfx.Round(r, 16)); Txt(g, "잠김", 15f, 2, Color.FromArgb(255, 220, 220, 235), r.X + 50, r.Y + 14, r.Width - 56, 28, StringAlignment.Near); Txt(g, StName[i - 1] + "을(를) 클리어하면 열려요", 9f, 1, Color.FromArgb(200, 255, 255, 255), r.X + 6, r.Y + 56, r.Width - 12, 36, StringAlignment.Center); continue; }
				if (i < cleared) Txt(g, "클리어 ★", 9f, 2, Color.FromArgb(255, 255, 214, 90), r.X + 6, r.Y + 78, r.Width - 12, 18, StringAlignment.Far);
				Txt(g, "근무지 " + new string((char)0x2605, Math.Min(5, 1 + i)), 9f, 1, Color.FromArgb(255, 226, 150), r.X + 12, r.Y + 78, r.Width - 22, 18, StringAlignment.Near);
			}
		}
		// 능력 고르기 (카드 3장)
		if (mode == 1)
		{
			g.FillRectangle(Br(Color.FromArgb(185, 8, 12, 30)), 0, 0, W, H);
			float ta = Math.Min(1f, lvAge / 0.3f);
			Txt(g, "레벨 업!", 24f, 2, Color.FromArgb((int)(255 * ta), 255, 226, 120), 0, 70 - (1f - ta) * 14f, W, 40, StringAlignment.Center);
			Hint(g, "카드를 골라요  (← → 방향키로 고르고 Enter/Space · 1·2·3 키 · 마우스)", 112 + 11.0, 10.5f, false, ta);
			for (int i = 0; i < 3; i++)
			{
				float ap = Math.Max(0f, Math.Min(1f, (lvAge - 0.12f * i) / 0.38f)); if (ap <= 0f) continue;
				float ease = 1f - (float)Math.Pow(1f - ap, 3), sc = 0.35f + 0.65f * ease;
				RectangleF r = CardRect(i); bool hot = lvAge > 0.6f && i == lvSel; int k = optK[i], v = opt[i];
				float lift = hot ? -10f : 0f, cxm = r.X + r.Width / 2f, cym = r.Y + r.Height / 2f + (1f - ease) * 40f + lift;
				Color ac = k == 2 ? Color.FromArgb(255, 200, 70) : (k == 0 ? WCol[v] : (k == 3 ? PCol[8] : PCol[v]));
				GraphicsState st = g.Save(); g.TranslateTransform(cxm, cym); g.ScaleTransform(sc * (hot ? 1.04f : 1f), sc * (hot ? 1.04f : 1f)); g.TranslateTransform(-r.Width / 2f, -r.Height / 2f);
				RectangleF cr = new RectangleF(0, 0, r.Width, r.Height); int al = (int)(255 * ease);
				if (hot || k == 2) g.FillPath(Br(Color.FromArgb((int)((hot ? 70 : 40) * ease), ac)), Gfx.Round(new RectangleF(-7, -7, r.Width + 14, r.Height + 14), 24));
				using (GraphicsPath gp = Gfx.Round(cr, 18))
				{
					g.FillPath(Br(Color.FromArgb(al, 28, 34, 62)), gp);
					using (LinearGradientBrush lg = new LinearGradientBrush(cr, Color.FromArgb((int)(120 * ease), ac), Color.FromArgb(0, ac), 90f)) g.FillPath(lg, gp);
					using (Pen p = new Pen(Color.FromArgb(al, Theme.Mix(ac, Color.White, hot ? 0.5 : 0.1)), k == 2 ? 3.2f : (hot ? 3f : 2f))) g.DrawPath(p, gp);
				}
				g.FillEllipse(Br(Color.FromArgb((int)(60 * ease), ac)), r.Width / 2f - 44, 24, 88, 88);
				if (k == 1 && v >= 0 && v < 11 && pl[v] > 0 && Spr.Get("ms_svp_up") != null) Spr.Draw(g, "ms_svp_up", r.Width - 24f, 22f, 26f, 26f, 0f);
				if (k == 0) IconW(g, v, r.Width / 2f, 68, 32f, 0); else if (k == 1) IconP(g, v, r.Width / 2f, 68, 32f, 0); else if (k == 3) IconH(g, v, r.Width / 2f, 68, 32f); else if (Spr.Get("ms_sve" + v) != null) Sv(g, "ms_sve" + v, r.Width / 2f, 68, 70f); else { IconW(g, EReq[v, 1], r.Width / 2f - 22, 68, 22f, 0); if (EReq[v, 0] == 0) IconP(g, EReq[v, 2], r.Width / 2f + 22, 68, 22f, 0); else IconW(g, EReq[v, 2], r.Width / 2f + 22, 68, 22f, 0); }
				Txt(g, (i + 1).ToString(), 10f, 2, Color.FromArgb(al, 255, 230, 200), 8, 6, 18, 18, StringAlignment.Center);
				string nm = k == 2 ? EName[v] : (k == 0 ? WName[v] : (k == 3 ? HName[v] : PName[v]));
				string sub = k == 2 ? "★ 진화!" : (k == 3 ? "체력 회복" : k == 0 ? (wl[v] == 0 ? "NEW 새 능력" : "Lv." + wl[v] + " → " + (wl[v] + 1)) : (pl[v] == 0 ? "NEW 새 능력" : "Lv." + pl[v] + " → " + (pl[v] + 1)));
				string desc = k == 2 ? EDesc[v] + "\n(" + WName[EReq[v, 1]] + " + " + (EReq[v, 0] == 0 ? PName[EReq[v, 2]] : WName[EReq[v, 2]]) + ")" : (k == 3 ? HDesc[v] : (k == 0 ? WDesc[v] : PDesc[v]));
				Txt(g, nm, 12.5f, 2, k == 2 ? Color.FromArgb(al, 255, 226, 120) : Color.FromArgb(al, 255, 255, 255), 4, 118, r.Width - 8, 24, StringAlignment.Center);
				Txt(g, sub, 10f, 2, Color.FromArgb(al, Theme.Mix(ac, Color.White, 0.45)), 4, 144, r.Width - 8, 20, StringAlignment.Center);
				g.FillRectangle(Br(Color.FromArgb((int)(40 * ease), 255, 255, 255)), 14, 170, r.Width - 28, 1.5f);
				Txt(g, desc, 9.5f, 0, ease >= 0.98f ? Color.FromArgb(255, 228, 230, 238) : Color.FromArgb((int)(225 * ease), 255, 255, 255), 8, 178, r.Width - 16, 112, StringAlignment.Center, true);
				g.Restore(st);
			}
		}
		// 상자 열기
		if (mode == 3)
		{
			g.FillRectangle(Br(Color.FromArgb(200, 6, 8, 24)), 0, 0, W, H);
			bool big = chN == 3; float open = Math.Max(0f, Math.Min(1f, (chT - 0.9f) / 0.3f)), cxm = 200f, cym = 230f;
			if (open > 0f)
			{
				int rays = big ? 16 : 10; float rl = (big ? 330f : 230f) * open;
				for (int i = 0; i < rays; i++)
				{
					double a0 = t * (big ? 0.7 : 0.4) + i * Math.PI * 2 / rays, a1 = a0 + Math.PI / rays * 0.7;
					Color rc = big ? Hsv(i * 360f / rays + (float)t * 60f) : Color.FromArgb(255, 226, 140);
					g.FillPolygon(Br(Color.FromArgb(big ? 70 : 46, rc)), new PointF[] { new PointF(cxm, cym), new PointF(cxm + (float)Math.Cos(a0) * rl, cym + (float)Math.Sin(a0) * rl), new PointF(cxm + (float)Math.Cos(a1) * rl, cym + (float)Math.Sin(a1) * rl) });
				}
				g.FillEllipse(Br(Color.FromArgb(big ? 90 : 60, 255, 236, 160)), cxm - 120 * open, cym - 120 * open, 240 * open, 240 * open);
			}
			float shake = chT < 0.9f ? (float)Math.Sin(chT * 60) * (1f + chT * 5f) : 0f;
			DrawChest(g, cxm + shake, cym + 18f, 3.4f, open, t, false);
			if (chT > 1.0f)
			{
				string tt = chN == 3 ? "대박! 3개 당첨!" : (chN == 2 ? "2개 획득!" : "1개 획득");
				float pu = chN == 3 ? 1f + 0.06f * (float)Math.Sin(chT * 10) : 1f;
				Txt(g, tt, (chN == 3 ? 24f : 20f) * pu, 2, chN == 3 ? Hsv((float)t * 140f) : Color.FromArgb(255, 226, 120), 0, 96, W, 44, StringAlignment.Center);
			}
			for (int i = 0; i < chN; i++)
			{
				float at = 1.1f + 0.5f * i, ap = Math.Max(0f, Math.Min(1f, (chT - at) / 0.35f)); if (ap <= 0f) continue;
				float pop = ap < 1f ? 1f + 0.18f * (float)Math.Sin(ap * Math.PI) : 1f, sc = (0.4f + 0.6f * ap) * pop;
				float x0 = ChestCardX(i), k0 = chK[i], v0 = chV[i]; int k = chK[i], v = chV[i];
				Color ac = k == 2 ? Color.FromArgb(255, 200, 70) : (k == 0 ? WCol[v] : (k == 3 ? PCol[8] : PCol[v]));
				GraphicsState st = g.Save(); g.TranslateTransform(x0 + 56f, 380f); g.ScaleTransform(sc, sc); g.TranslateTransform(-56f, -86f);
				RectangleF cr = new RectangleF(0, 0, 112, 172); int al = (int)(255 * Math.Min(1f, ap * 1.6f));
				if (big) g.FillPath(Br(Color.FromArgb((int)(70 * ap), Hsv(i * 120f + (float)t * 90f))), Gfx.Round(new RectangleF(-8, -8, 128, 188), 26));
				using (GraphicsPath gp = Gfx.Round(cr, 16))
				{
					g.FillPath(Br(Color.FromArgb(al, 28, 34, 62)), gp);
					using (LinearGradientBrush lg = new LinearGradientBrush(cr, Color.FromArgb((int)(120 * ap), ac), Color.FromArgb(0, ac), 90f)) g.FillPath(lg, gp);
					using (Pen p = new Pen(Color.FromArgb(al, big ? Hsv(i * 120f + (float)t * 90f) : Theme.Mix(ac, Color.White, 0.3)), big ? 3.4f : 2f)) g.DrawPath(p, gp);
				}
				if (k == 0) IconW(g, v, 56, 44, 26f, 0); else if (k == 1) IconP(g, v, 56, 44, 26f, 0); else if (k == 3) IconH(g, v, 56, 44, 26f); else if (Spr.Get("ms_sve" + v) != null) Sv(g, "ms_sve" + v, 56, 44, 52f); else { IconW(g, EReq[v, 1], 36, 44, 18f, 0); if (EReq[v, 0] == 0) IconP(g, EReq[v, 2], 76, 44, 18f, 0); else IconW(g, EReq[v, 2], 76, 44, 18f, 0); }
				string nm = k == 2 ? EName[v] : (k == 0 ? WName[v] : (k == 3 ? HName[v] : PName[v]));
				Txt(g, nm, 11.5f, 2, k == 2 ? Color.FromArgb(al, 255, 226, 120) : Color.FromArgb(al, 255, 255, 255), 2, 82, 108, 24, StringAlignment.Center);
				Txt(g, k == 2 ? "★ 진화!" : (k == 3 ? "체력 회복" : "Lv." + (chLv[i] - 1) + " → " + chLv[i]), 10f, 2, Color.FromArgb(al, 255, 220, 120), 2, 108, 108, 20, StringAlignment.Center);
				Txt(g, k == 2 ? EDesc[v] : (k == 3 ? HDesc[v] : (k == 0 ? WDesc[v] : PDesc[v])), 8.5f, 0, ap >= 0.98f ? Color.FromArgb(255, 214, 218, 232) : Color.FromArgb((int)(200 * ap), 255, 255, 255), 6, 130, 100, 40, StringAlignment.Center, true);
				g.Restore(st);
			}
			if (chT >= ChestDone) Hint(g, "아무 곳이나 눌러 계속", 531f, 10.5f, true, 0.75f + 0.25f * (float)Math.Sin(t * 5));
		}
	}

	private static readonly Dictionary<int, SolidBrush> brc = new Dictionary<int, SolidBrush>();
	private static SolidBrush Br(Color c) { int k = c.ToArgb(); SolidBrush b; if (!brc.TryGetValue(k, out b)) { if (brc.Count > 600) { foreach (SolidBrush o in brc.Values) o.Dispose(); brc.Clear(); } b = new SolidBrush(c); brc[k] = b; } return b; }
	private static readonly Color[] BossCol = { Color.FromArgb(255, 120, 90), Color.FromArgb(120, 220, 110), Color.FromArgb(190, 120, 255), Color.FromArgb(255, 200, 60) };
	private static readonly System.Drawing.Imaging.ImageAttributes[] BossTint = {
		null,
		MkAttr(new float[][] { new float[] { 0.55f, 0, 0, 0, 0 }, new float[] { 0, 1.1f, 0, 0, 0 }, new float[] { 0, 0, 0.6f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0, 0.03f, 0, 0, 1 } }),
		MkAttr(new float[][] { new float[] { 1.0f, 0, 0, 0, 0 }, new float[] { 0, 0.6f, 0, 0, 0 }, new float[] { 0, 0, 1.2f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0.03f, 0, 0.05f, 0, 1 } }),
		MkAttr(new float[][] { new float[] { 1.25f, 0, 0, 0, 0 }, new float[] { 0, 0.85f, 0, 0, 0 }, new float[] { 0, 0, 0.4f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0.05f, 0.02f, 0, 0, 1 } }) };
	private static void Rr(Graphics g, Color c, float x, float y, float w, float h, float rad) { using (GraphicsPath gp = Gfx.Round(new RectangleF(x, y, w, h), rad)) g.FillPath(Br(c), gp); }
	private static readonly Dictionary<long, Bitmap> mobCache = new Dictionary<long, Bitmap>();
	private static readonly Dictionary<long, System.Drawing.Image> tintCache = new Dictionary<long, System.Drawing.Image>();
	// 좀비 스프라이트 (니케 시트): 걷는 그림을 시간에 따라 바꿔 그리고, 플레이어가 있는 쪽을 바라보게 뒤집어요
	private static readonly string[] ZPre = { "za", "za", "zc", "zb" }; private static readonly int[] ZCnt = { 6, 6, 6, 6 };
	private void CorpseDraw(Graphics g, Cp c, float sx, float sy)
	{
		if (sx < -80 || sx > W + 80 || sy < -80 || sy > H + 80) return;
		bool ox = c.Pre == "opx"; int f = Math.Min(ox ? 3 : 5, (int)(c.Age / (ox ? 0.14f : 0.075f))); Bitmap b = Spr.Get("ms_" + c.Pre + f), r = Spr.Get(ox ? "ms_opx0" : "ms_za1"); if (b == null || r == null) return;
		float k = c.H / r.Height, w = b.Width * k, h = b.Height * k, al = c.Age < 0.95f ? 1f : 1f - (c.Age - 0.95f) / 0.55f;
		Vfx.Draw(g, b, sx, sy + 8f - h / 2f + (f >= 3 ? 4f : 0f), w, h, 0f, al, c.Flip);
	}
	// 근무지별 적 · 보스 (스테이지마다 2종 + 보스, 4프레임 걷기, 오른쪽을 보는 그림)
	private bool StDraw(Graphics g, En e, float sx, float sy, double t)
	{
		string pre;
		if (e.IsBoss) pre = "ms_s" + stage + "x";
		else pre = "ms_s" + stage + (e.T == 0 || e.T == 1 || e.T == 4 || e.T == 8 ? "a" : "b");
		Bitmap b0 = Spr.Get(pre + "0"); if (b0 == null) return false;
		int f = (int)((t * (e.T == 5 && !e.IsBoss ? 10.0 : 7.0) + e.X * 0.05) % 4.0); if (f < 0) f = 0; Bitmap fb = Spr.Get(pre + f); if (fb == null) return false;
		float hh = e.R * (e.IsBoss ? 4.6f : (e.T == 6 ? 4.4f : (e.T == 7 ? 4.2f : 3.9f))), ww = hh * fb.Width / fb.Height, bob = (float)Math.Abs(Math.Sin(t * 7 + e.X * 0.05f)) * 2f;
		if (e.Pat == 1) sx += (float)Math.Sin(t * 50) * 2f;
		if (e.T == 8 && !e.IsBoss) { float hot = e.Pat == 3 ? 0.5f + 0.5f * (float)Math.Sin(t * 40) : 0.15f; Vfx.Glow(g, sx, sy - e.R * 0.3f, e.R * 1.9f, e.R * 1.7f, Color.FromArgb(255, 80, 40), 0.35f + 0.5f * hot); }
		Bitmap use = e.Hit > 0f ? Vfx.Tint(fb, "white") : (freezeT > 0f ? Vfx.Tint(fb, "ice") : fb);
		float sq = 1f - 0.12f * e.Sq; Vfx.Draw(g, use, sx, sy + e.R * 0.62f - hh * sq / 2f - bob, ww * (2f - sq), hh * sq, 0f, 1f, px < e.X); return true;
	}
	private void FoeDraw(Graphics g, En e, float sx, float sy, double t)
	{
		string pre = e.T == 4 || e.T == 1 ? "ms_emg" : (e.T == 5 || e.T == 0 || e.T == 3 ? "ms_ecu" : (e.T == 6 || e.T == 2 ? "ms_edoc" : (e.T == 8 ? "ms_ebm" : (e.T == 7 ? "ms_ops" : null))));
		if (pre != null && Spr.Get(pre + "0") != null)   // 걷기·뛰기 4프레임 그림
		{
			int f = (int)((t * (e.T == 5 ? 11.0 : 7.0) + e.X * 0.05) % 4.0); if (f < 0) f = 0;
			if (e.T == 8) { int[] bs = { 0, 1, 2, 1 }; f = e.Pat == 3 ? 3 : bs[f]; } else if (e.T == 7) f = (f & 1) * 2;
			Bitmap fb2 = Spr.Get(pre + f); if (fb2 == null) return;
			float hh = e.R * (e.T == 6 || e.T == 2 ? 4.4f : (e.T == 8 ? 4.0f : (e.T == 7 ? 4.2f : 3.9f))), ww = hh * fb2.Width / fb2.Height, bob2 = e.T == 8 ? 0f : (float)Math.Abs(Math.Sin(t * 7 + e.X * 0.05f)) * 2f;
			if (e.Pat == 1) sx += (float)Math.Sin(t * 50) * 2f;
			if (e.T == 8) { float hot = e.Pat == 3 ? 0.5f + 0.5f * (float)Math.Sin(t * 40) : 0.15f; Vfx.Glow(g, sx, sy - e.R * 0.3f, e.R * 1.9f, e.R * 1.7f, Color.FromArgb(255, 80, 40), 0.35f + 0.5f * hot); }
			Bitmap use2 = e.Hit > 0f ? Vfx.Tint(fb2, "white") : (freezeT > 0f ? Vfx.Tint(fb2, "ice") : fb2);
			float sq2 = 1f - 0.12f * e.Sq; bool fl = (e.T != 6 && e.T != 2 && e.T != 8) && px < e.X;
			Vfx.Draw(g, use2, sx, sy + e.R * 0.62f - hh * sq2 / 2f - bob2, ww * (2f - sq2), hh * sq2, 0f, 1f, fl); return;
		}
		string nm = e.T == 6 ? "foe2" : (e.T == 4 || e.T == 5 || e.T == 8 ? "foe0" : "foe3"); string tn = e.T == 5 ? "red" : (e.T == 8 ? "bomb" : (e.T == 4 || e.T == 6 ? null : "steel"));
		Bitmap fb = Spr.Get(nm); if (fb == null) { DrawMob(g, e, sx, sy, t); return; }
		float fw = e.R * (e.T == 6 ? 4.1f : 3.7f), bob = (float)Math.Sin(t * 7 + e.X * 0.05f) * 1.8f; if (e.Pat == 1) sx += (float)Math.Sin(t * 50) * 2f;
		if (e.T == 8) { float hot = e.Pat == 3 ? 0.5f + 0.5f * (float)Math.Sin(t * 40) : 0.15f; Vfx.Glow(g, sx, sy - e.R * 0.3f, e.R * 1.9f, e.R * 1.7f, Color.FromArgb(255, 80, 40), 0.35f + 0.5f * hot); }
		Bitmap use = e.Hit > 0f ? Vfx.Tint(fb, "white") : (freezeT > 0f ? Vfx.Tint(fb, "ice") : Vfx.Tint(fb, tn));
		float sq = 1f - 0.12f * e.Sq; Vfx.Draw(g, use, sx, sy - fw * 0.06f + bob, fw * (2f - sq), fw * sq, e.Pat == 1 ? (float)Math.Sin(t * 40) * 4f : 0f, 1f, false);
	}
	// 의회: 반대 의원 (붉은 정장 · 악마 뿔 · '반대' 방패) 4프레임 걷기
	private void OpDraw(Graphics g, En e, float sx, float sy, double t)
	{
		string os = e.IsBoss || e.T == 2 || e.T == 6 ? "ms_opd" : (e.T == 4 ? "ms_opb" : (e.T == 1 || e.T == 5 || e.T == 8 ? "ms_opc" : (e.T == 7 ? "ms_op" : "ms_opa"))); if (Spr.Get(os + "0") == null) os = "ms_op"; bool facesL = os == "ms_op";
		int f = (int)((t * 7.0 + e.X * 0.05) % 4.0); if (f < 0) f = 0; Bitmap ob = Spr.Get(os + f); if (ob == null) return;
		float h = e.R * (e.T == 7 ? 4.3f : (e.T == 4 ? 3.4f : 3.8f)), w = h * ob.Width / ob.Height, bob = (float)Math.Abs(Math.Sin(t * 7 + e.X * 0.05f)) * 2f;
		if (e.Pat == 1) sx += (float)Math.Sin(t * 50) * 2f;
		Bitmap use = e.Hit > 0f ? Vfx.Tint(ob, "white") : (freezeT > 0f ? Vfx.Tint(ob, "ice") : ob);
		float sq = 1f - 0.12f * e.Sq; Vfx.Draw(g, use, sx, sy + e.R * 0.62f - h * sq / 2f - bob, w * (2f - sq), h * sq, 0f, 1f, facesL ? px > e.X : px < e.X);
	}
	private void ZombieDraw(Graphics g, En e, float sx, float sy, double t)
	{
		int f = (int)((t * 9.0 + e.X * 0.05) % ZCnt[e.T]); if (f < 0) f = 0; Bitmap zb = Spr.Get("ms_" + ZPre[e.T] + (1 + f)); if (zb == null) return;
		float h = e.R * (e.T == 3 ? 3.5f : 3.0f), w = h * zb.Width / zb.Height, bob = (float)Math.Abs(Math.Sin(t * 7 + e.X * 0.05f)) * 2f;
		float top = sy + e.R * 0.62f - h - bob, x0 = sx - w / 2f; bool flip = px < e.X; float k = ImgCache.Scale(g);
		System.Drawing.Image im = ImgCache.Get(zb, w * k, h * k); InterpolationMode im0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear;
		PointF[] dst = flip ? new[] { new PointF(x0 + w, top), new PointF(x0, top), new PointF(x0 + w, top + h) } : new[] { new PointF(x0, top), new PointF(x0 + w, top), new PointF(x0, top + h) };
		if (e.Hit > 0 || freezeT > 0)
		{
			using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
			{
				float[][] m = e.Hit > 0 ? new float[][] { new float[] { 1, 0, 0, 0, 0 }, new float[] { 0, 1, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0.55f, 0.55f, 0.55f, 0, 1 } }
					: new float[][] { new float[] { 0.6f, 0, 0, 0, 0 }, new float[] { 0, 0.8f, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0.05f, 0.12f, 0.3f, 0, 1 } };
				ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(m)); g.DrawImage(im, dst, new RectangleF(0, 0, im.Width, im.Height), GraphicsUnit.Pixel, ia);
			}
		}
		else g.DrawImage(im, dst, new RectangleF(0, 0, im.Width, im.Height), GraphicsUnit.Pixel);
		g.InterpolationMode = im0;
	}
	private Bitmap MobBmp(En e, float k)
	{
		long key = ((long)e.T << 40) | ((long)(int)Math.Round(e.R * 4f) << 16) | (long)(int)(k * 100f); Bitmap b;
		if (mobCache.TryGetValue(key, out b)) return b;
		if (mobCache.Count > 80) { foreach (Bitmap o in mobCache.Values) o.Dispose(); mobCache.Clear(); }
		int sz = Math.Max(16, (int)Math.Ceiling(e.R * 4f * k)); b = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g2 = Graphics.FromImage(b)) { g2.SmoothingMode = SmoothingMode.AntiAlias; g2.ScaleTransform(sz / (e.R * 4f), sz / (e.R * 4f)); En tmp = new En { T = e.T, R = e.R, Pat = 0, X = 0f }; DrawMob(g2, tmp, e.R * 2f, e.R * 2f, 0.0); }
		mobCache[key] = b; return b;
	}
	private static System.Drawing.Image Tinted(System.Drawing.Image src, int ty, int ti)
	{
		long key = ((long)ty << 40) | ((long)ti << 32) | (long)src.Width; System.Drawing.Image r;
		if (tintCache.TryGetValue(key, out r)) return r;
		if (tintCache.Count > 120) { foreach (System.Drawing.Image o in tintCache.Values) if (o != null) o.Dispose(); tintCache.Clear(); }
		System.Drawing.Imaging.ImageAttributes ia = ti == 1 ? FoeHit : (ti == 2 ? FoeIce : BossTint[ti - 3]);
		Bitmap b = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g2 = Graphics.FromImage(b)) { g2.CompositingMode = CompositingMode.SourceCopy; if (ia != null) g2.DrawImage(src, new Rectangle(0, 0, b.Width, b.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia); else g2.DrawImage(src, 0, 0, b.Width, b.Height); }
		tintCache[key] = b; return b;
	}
	private void DrawMob(Graphics g, En e, float sx, float sy, double t)
	{
		float r = e.R, bob = (float)Math.Sin(t * 7 + e.X * 0.05f) * 1.5f; Color skin = Color.FromArgb(255, 216, 184), ink = Color.FromArgb(50, 40, 50);
		if (e.T == 4)   // 악성 민원인 (확성기)
		{
			Rr(g, Color.FromArgb(70, 92, 160), sx - r * 0.75f, sy - r * 0.2f + bob, r * 1.5f, r * 1.15f, r * 0.4f);
			g.FillEllipse(Br(skin), sx - r * 0.7f, sy - r * 1.05f + bob, r * 1.4f, r * 1.3f);
			g.FillEllipse(Br(Color.FromArgb(150, 255, 110, 100)), sx - r * 0.62f, sy - r * 0.42f + bob, r * 0.38f, r * 0.26f); g.FillEllipse(Br(Color.FromArgb(150, 255, 110, 100)), sx + r * 0.24f, sy - r * 0.42f + bob, r * 0.38f, r * 0.26f);
			using (Pen pp = new Pen(ink, 2f)) { g.DrawLine(pp, sx - r * 0.5f, sy - r * 0.78f + bob, sx - r * 0.1f, sy - r * 0.55f + bob); g.DrawLine(pp, sx + r * 0.5f, sy - r * 0.78f + bob, sx + r * 0.1f, sy - r * 0.55f + bob); }
			g.FillEllipse(Br(ink), sx - r * 0.34f, sy - r * 0.5f + bob, r * 0.14f, r * 0.14f); g.FillEllipse(Br(ink), sx + r * 0.2f, sy - r * 0.5f + bob, r * 0.14f, r * 0.14f); g.FillEllipse(Br(Color.FromArgb(120, 30, 40)), sx - r * 0.2f, sy - r * 0.2f + bob, r * 0.4f, r * 0.28f);
			g.FillPolygon(Br(Color.FromArgb(255, 210, 70)), new PointF[] { new PointF(sx + r * 0.7f, sy + r * 0.05f + bob), new PointF(sx + r * 1.5f, sy - r * 0.35f + bob), new PointF(sx + r * 1.5f, sy + r * 0.65f + bob), new PointF(sx + r * 0.7f, sy + r * 0.4f + bob) });
		}
		else if (e.T == 5)   // 돌진 민원
		{
			float sh = e.Pat == 1 ? (float)Math.Sin(t * 50) * 2f : 0f; sx += sh;
			g.FillEllipse(Br(Color.FromArgb(232, 92, 62)), sx - r, sy - r * 0.9f + bob, r * 2f, r * 1.9f); g.FillEllipse(Br(Color.FromArgb(255, 170, 130)), sx - r * 0.6f, sy - r * 0.1f + bob, r * 1.2f, r * 0.8f);
			g.FillPolygon(Br(Color.FromArgb(250, 240, 210)), new PointF[] { new PointF(sx - r * 0.85f, sy - r * 0.55f + bob), new PointF(sx - r * 1.2f, sy - r * 1.15f + bob), new PointF(sx - r * 0.4f, sy - r * 0.8f + bob) });
			g.FillPolygon(Br(Color.FromArgb(250, 240, 210)), new PointF[] { new PointF(sx + r * 0.85f, sy - r * 0.55f + bob), new PointF(sx + r * 1.2f, sy - r * 1.15f + bob), new PointF(sx + r * 0.4f, sy - r * 0.8f + bob) });
			g.FillEllipse(Br(Color.White), sx - r * 0.62f, sy - r * 0.45f + bob, r * 0.5f, r * 0.4f); g.FillEllipse(Br(Color.White), sx + r * 0.12f, sy - r * 0.45f + bob, r * 0.5f, r * 0.4f);
			g.FillEllipse(Br(ink), sx - r * 0.4f, sy - r * 0.33f + bob, r * 0.2f, r * 0.2f); g.FillEllipse(Br(ink), sx + r * 0.2f, sy - r * 0.33f + bob, r * 0.2f, r * 0.2f);
			using (Pen pp = new Pen(ink, 2f)) { g.DrawLine(pp, sx - r * 0.7f, sy - r * 0.55f + bob, sx - r * 0.15f, sy - r * 0.4f + bob); g.DrawLine(pp, sx + r * 0.7f, sy - r * 0.55f + bob, sx + r * 0.15f, sy - r * 0.4f + bob); }
			g.FillEllipse(Br(Color.FromArgb(150, 60, 40)), sx - r * 0.18f, sy + r * 0.2f + bob, r * 0.36f, r * 0.26f);
		}
		else if (e.T == 6)   // 서류 더미
		{
			for (int k = 0; k < 3; k++)
			{
				GraphicsState st2 = g.Save(); g.TranslateTransform(sx, sy + r * 0.45f - k * r * 0.5f + bob); g.RotateTransform((k - 1) * 7f);
				Rr(g, k == 2 ? Color.FromArgb(255, 252, 240) : Color.FromArgb(235, 232, 220), -r * 0.95f, -r * 0.34f, r * 1.9f, r * 0.7f, 3f);
				if (k < 2) g.FillRectangle(Br(Color.FromArgb(120, 150, 150, 170)), -r * 0.7f, -r * 0.05f, r * 1.4f, 1.6f);
				g.Restore(st2);
			}
			g.FillEllipse(Br(ink), sx - r * 0.45f, sy - r * 0.78f + bob, r * 0.22f, r * 0.26f); g.FillEllipse(Br(ink), sx + r * 0.23f, sy - r * 0.78f + bob, r * 0.22f, r * 0.26f);
			using (Pen pp = new Pen(Color.FromArgb(200, 60, 60), 2f)) g.DrawLine(pp, sx - r * 0.2f, sy - r * 0.42f + bob, sx + r * 0.2f, sy - r * 0.42f + bob);
			g.FillRectangle(Br(Color.FromArgb(220, 230, 90, 80)), sx - r * 0.12f, sy - r * 1.02f + bob, r * 0.24f, r * 1.4f);
		}
		else if (e.T == 8)   // 自爆 민원 (민원 폭탄)
		{
			float hot = e.Pat == 3 ? (0.5f + 0.5f * (float)Math.Sin(t * 40)) : 0f;
			g.FillEllipse(Br(Color.FromArgb(60 + (int)(60 * hot), 255, 60, 40)), sx - r * 1.5f, sy - r * 1.4f + bob, r * 3f, r * 2.8f);
			g.FillEllipse(Br(Theme.Mix(Color.FromArgb(70, 72, 90), Color.FromArgb(255, 90, 60), hot)), sx - r, sy - r * 0.8f + bob, r * 2f, r * 1.9f);
			g.FillEllipse(Br(Color.FromArgb(90, 255, 255, 255)), sx - r * 0.6f, sy - r * 0.6f + bob, r * 0.6f, r * 0.4f);
			using (Pen pp = new Pen(Color.FromArgb(200, 170, 130), 2f)) g.DrawLine(pp, sx, sy - r * 0.8f + bob, sx + r * 0.4f, sy - r * 1.25f + bob);
			g.FillEllipse(Br(Color.FromArgb(255, 220 - (int)(120 * hot), 60)), sx + r * 0.3f, sy - r * 1.4f + bob, r * 0.35f, r * 0.35f);
			g.FillEllipse(Br(Color.White), sx - r * 0.5f, sy - r * 0.3f + bob, r * 0.42f, r * 0.42f); g.FillEllipse(Br(Color.White), sx + r * 0.08f, sy - r * 0.3f + bob, r * 0.42f, r * 0.42f);
			g.FillEllipse(Br(ink), sx - r * 0.36f, sy - r * 0.18f + bob, r * 0.2f, r * 0.2f); g.FillEllipse(Br(ink), sx + r * 0.2f, sy - r * 0.18f + bob, r * 0.2f, r * 0.2f);
			using (Pen pp = new Pen(Color.FromArgb(250, 90, 80), 2f)) { g.DrawLine(pp, sx - r * 0.55f, sy - r * 0.45f + bob, sx - r * 0.1f, sy - r * 0.3f + bob); g.DrawLine(pp, sx + r * 0.55f, sy - r * 0.45f + bob, sx + r * 0.1f, sy - r * 0.3f + bob); }
		}
		else   // 규정 철벽 (방패)
		{
			g.FillEllipse(Br(skin), sx - r * 0.45f, sy - r * 1.05f + bob, r * 0.9f, r * 0.85f);
			g.FillEllipse(Br(ink), sx - r * 0.25f, sy - r * 0.78f + bob, r * 0.14f, r * 0.14f); g.FillEllipse(Br(ink), sx + r * 0.11f, sy - r * 0.78f + bob, r * 0.14f, r * 0.14f);
			PointF[] sh2 = { new PointF(sx - r * 0.95f, sy - r * 0.35f + bob), new PointF(sx + r * 0.95f, sy - r * 0.35f + bob), new PointF(sx + r * 0.85f, sy + r * 0.5f + bob), new PointF(sx, sy + r * 1.05f + bob), new PointF(sx - r * 0.85f, sy + r * 0.5f + bob) };
			g.FillPolygon(Br(Color.FromArgb(120, 140, 170)), sh2); using (Pen pp = new Pen(Color.FromArgb(220, 230, 245), 2.5f)) g.DrawPolygon(pp, sh2);
			g.FillEllipse(Br(Color.FromArgb(255, 214, 90)), sx - r * 0.3f, sy - r * 0.05f + bob, r * 0.6f, r * 0.6f); g.FillRectangle(Br(Color.FromArgb(120, 140, 170)), sx - r * 0.18f, sy + r * 0.2f + bob, r * 0.36f, 2f);
		}
	}
	private static void DrawChest(Graphics g, float x, float y, float s, float open, double t, bool world)
	{
		if (world) { float pul = 1f + 0.1f * (float)Math.Sin(t * 5); g.FillEllipse(Br(Color.FromArgb(60, 255, 214, 90)), x - 30 * pul, y - 30 * pul, 60 * pul, 60 * pul); for (int i = 0; i < 4; i++) { double a = t * 1.5 + i * Math.PI / 2; g.FillEllipse(Br(Color.FromArgb(220, 255, 246, 190)), x + (float)Math.Cos(a) * 24f - 2f, y + (float)Math.Sin(a) * 24f - 2f, 4, 4); } }
		g.FillEllipse(Br(Color.FromArgb(70, 0, 0, 0)), x - 15 * s, y + 10 * s, 30 * s, 6 * s);
		if (Spr.Get("ms_svc0") != null) { Sv(g, "ms_svc" + (open < 0.12f ? 0 : (open < 0.75f ? 1 : 2)), x, y - 2f * s, 46f * s); return; }
		if (open > 0f) g.FillEllipse(Br(Color.FromArgb((int)(220 * open), 255, 240, 170)), x - 12 * s, y - 8 * s, 24 * s, 9 * s);
		Color wood = Color.FromArgb(142, 88, 50), dark = Color.FromArgb(98, 58, 32), gold = Color.FromArgb(246, 200, 70);
		Rr(g, wood, x - 14 * s, y - 4 * s, 28 * s, 15 * s, 3 * s); g.FillRectangle(Br(Color.FromArgb(60, 0, 0, 0)), x - 14 * s, y + 6 * s, 28 * s, 5 * s);
		g.FillRectangle(Br(gold), x - 3.5f * s, y - 4 * s, 7 * s, 15 * s); g.FillRectangle(Br(gold), x - 14 * s, y - 1 * s, 28 * s, 2.2f * s);
		if (open <= 0.02f) { Rr(g, dark, x - 14 * s, y - 13 * s, 28 * s, 9.5f * s, 4 * s); g.FillRectangle(Br(gold), x - 3.5f * s, y - 13 * s, 7 * s, 9.5f * s); g.FillRectangle(Br(Color.FromArgb(70, 255, 255, 255)), x - 12 * s, y - 12 * s, 24 * s, 1.6f * s); }
		else
		{
			float lh = 4 * s + open * 12 * s;   // 뚜껑이 뒤로 젖혀져 올라가요
			g.FillPolygon(Br(dark), new PointF[] { new PointF(x - 14 * s, y - 4 * s), new PointF(x + 14 * s, y - 4 * s), new PointF(x + 11 * s, y - 4 * s - lh), new PointF(x - 11 * s, y - 4 * s - lh) });
			g.FillPolygon(Br(Color.FromArgb(70, 255, 255, 255)), new PointF[] { new PointF(x - 12 * s, y - 4.5f * s), new PointF(x - 10 * s, y - 4.5f * s), new PointF(x - 9 * s, y - 4 * s - lh + 1.5f * s), new PointF(x - 11 * s, y - 4 * s - lh + 1.5f * s) });
			g.FillPolygon(Br(gold), new PointF[] { new PointF(x - 3.5f * s, y - 4 * s), new PointF(x + 3.5f * s, y - 4 * s), new PointF(x + 3 * s, y - 4 * s - lh), new PointF(x - 3 * s, y - 4 * s - lh) });
		}
		g.FillEllipse(Br(Color.FromArgb(255, 214, 80)), x - 2.6f * s, y - 1.2f * s, 5.2f * s, 5.2f * s); g.FillEllipse(Br(Color.FromArgb(120, 70, 20)), x - 0.9f * s, y + 0.6f * s, 1.8f * s, 2.2f * s);
	}
	private static void IconBase(Graphics g, float x, float y, float r, Color c, int lv)
	{
		g.FillEllipse(Br(Color.FromArgb(215, 14, 16, 34)), x - r, y - r, r * 2, r * 2);
		using (Pen pp = new Pen(Color.FromArgb(230, c), Math.Max(1.4f, r * 0.11f))) g.DrawEllipse(pp, x - r + 1, y - r + 1, r * 2 - 2, r * 2 - 2);
	}
	private static bool Sv(Graphics g, string n, float x, float y, float box)
	{
		Bitmap b = Spr.Get(n); if (b == null) return false; float w = box, h = box, ar = b.Width / (float)b.Height; if (ar > 1f) h = box / ar; else w = box * ar;
		return Spr.Draw(g, n, x, y, w, h, 0f);
	}
	private static void IconH(Graphics g, int v, float x, float y, float r)
	{
		string n = "ms_svi" + (v == 0 ? 4 : (v == 1 ? 0 : 5)); if (Spr.Get(n) == null) { IconP(g, 8, x, y, r, 0); return; }
		IconBase(g, x, y, r, Color.FromArgb(120, 220, 150), 0); Sv(g, n, x, y, r * 1.9f);
	}
	private static void IconLv(Graphics g, float x, float y, float r, int lv)
	{
		if (lv <= 0) return; g.FillEllipse(Br(Color.FromArgb(240, 255, 220, 90)), x + r * 0.35f, y + r * 0.2f, r * 0.95f, r * 0.95f);
		Txt(g, lv.ToString(), Math.Max(6.5f, r * 0.55f), 2, Color.FromArgb(60, 40, 10), x + r * 0.25f, y + r * 0.14f, r * 1.15f, r * 1.1f, StringAlignment.Center);
	}
	private static void IconW(Graphics g, int i, float x, float y, float r, int lv)
	{
		Color c = WCol[i]; IconBase(g, x, y, r, c, lv); if (Sv(g, "ms_svw" + i, x, y, r * 1.9f)) { IconLv(g, x, y, r, lv); return; } float u = r / 12f; Color w = Color.FromArgb(250, 250, 255);
		switch (i)
		{
			case 0: g.FillPolygon(Br(c), new PointF[] { new PointF(x, y - 9 * u), new PointF(x + 6 * u, y), new PointF(x + 4 * u, y + 7 * u), new PointF(x, y + 9 * u), new PointF(x - 4 * u, y + 7 * u), new PointF(x - 6 * u, y) }); g.FillEllipse(Br(Color.FromArgb(255, 235, 110)), x - 3 * u, y + 0.5f * u, 6 * u, 7 * u); break;
			case 1: g.FillEllipse(Br(c), x - 5 * u, y - 5 * u, 10 * u, 10 * u); g.FillEllipse(Br(w), x - 3 * u, y - 4 * u, 3 * u, 3 * u); using (Pen pp = new Pen(Color.FromArgb(150, c), 1.2f * u)) g.DrawEllipse(pp, x - 8 * u, y - 8 * u, 16 * u, 16 * u); break;
			case 2: g.FillPolygon(Br(c), new PointF[] { new PointF(x + 2 * u, y - 10 * u), new PointF(x - 6 * u, y + 1 * u), new PointF(x - 1 * u, y + 1 * u), new PointF(x - 3 * u, y + 10 * u), new PointF(x + 6 * u, y - 2 * u), new PointF(x + 1 * u, y - 2 * u) }); break;
			case 3: using (Pen pp = new Pen(c, 3.2f * u)) { pp.StartCap = LineCap.Round; pp.EndCap = LineCap.Round; g.DrawArc(pp, x - 8 * u, y - 6 * u, 16 * u, 16 * u, 200, 140); } g.FillEllipse(Br(w), x - 1.5f * u, y - 7 * u, 3 * u, 3 * u); break;
			case 4: using (Pen pp = new Pen(c, 1.8f * u)) { g.DrawEllipse(pp, x - 8 * u, y - 8 * u, 16 * u, 16 * u); g.DrawEllipse(pp, x - 4.5f * u, y - 4.5f * u, 9 * u, 9 * u); g.DrawLine(pp, x - 8 * u, y, x + 8 * u, y); } break;
			case 5: Rr(g, c, x - 6 * u, y - 1 * u, 12 * u, 8 * u, 2 * u); Rr(g, Theme.Mix(c, Color.White, 0.4), x - 3 * u, y - 8 * u, 6 * u, 8 * u, 2 * u); break;
			case 6: g.FillPolygon(Br(w), new PointF[] { new PointF(x + 10 * u, y - 1 * u), new PointF(x - 9 * u, y - 8 * u), new PointF(x - 4 * u, y - 1 * u), new PointF(x - 9 * u, y + 7 * u) }); g.FillPolygon(Br(Color.FromArgb(160, 175, 215)), new PointF[] { new PointF(x + 10 * u, y - 1 * u), new PointF(x - 4 * u, y - 1 * u), new PointF(x - 9 * u, y + 7 * u) }); break;
			case 7: Rr(g, Color.FromArgb(245, 240, 230), x - 6 * u, y - 3 * u, 11 * u, 10 * u, 3 * u); g.FillEllipse(Br(c), x - 4.5f * u, y - 2 * u, 8 * u, 3 * u); using (Pen pp = new Pen(Color.FromArgb(245, 240, 230), 1.6f * u)) g.DrawArc(pp, x + 2 * u, y - 1.5f * u, 6 * u, 6 * u, -90, 180); using (Pen pp = new Pen(Color.FromArgb(180, 255, 255, 255), 1.2f * u)) { g.DrawLine(pp, x - 2 * u, y - 5 * u, x - 1 * u, y - 8 * u); g.DrawLine(pp, x + 2 * u, y - 5 * u, x + 3 * u, y - 8 * u); } break;
			case 8: Rr(g, Color.FromArgb(150, 160, 175), x - 8 * u, y - 1 * u, 16 * u, 5 * u, 2 * u); Rr(g, c, x - 8 * u, y - 5 * u, 14 * u, 4 * u, 2 * u); using (Pen pp = new Pen(w, 1.4f * u)) { g.DrawLine(pp, x + 5 * u, y + 7 * u, x + 9 * u, y + 7 * u); g.DrawLine(pp, x + 5 * u, y + 7 * u, x + 5 * u, y + 4 * u); g.DrawLine(pp, x + 9 * u, y + 7 * u, x + 9 * u, y + 4 * u); } break;
			default: using (Pen pp = new Pen(Color.FromArgb(120, c), 4f * u)) g.DrawLine(pp, x - 8 * u, y + 5 * u, x + 6 * u, y - 5 * u); using (Pen pp = new Pen(c, 1.6f * u)) g.DrawLine(pp, x - 8 * u, y + 5 * u, x + 6 * u, y - 5 * u); g.FillEllipse(Br(w), x + 4 * u, y - 7 * u, 5 * u, 5 * u); break;
		}
		IconLv(g, x, y, r, lv);
	}
	private static readonly Color[] PCol = { Color.FromArgb(240, 90, 110), Color.FromArgb(110, 200, 255), Color.FromArgb(230, 90, 90), Color.FromArgb(255, 150, 60), Color.FromArgb(250, 220, 110), Color.FromArgb(170, 185, 205), Color.FromArgb(255, 210, 70), Color.FromArgb(160, 230, 220), Color.FromArgb(150, 230, 130), Color.FromArgb(150, 160, 255), Color.FromArgb(255, 160, 200) };
	private static void IconP(Graphics g, int i, float x, float y, float r, int lv)
	{
		if (i >= 0 && i < 11 && Spr.Get("ms_svp" + i) != null) { IconBase(g, x, y, r, PCol[i], lv); Sv(g, "ms_svp" + i, x, y, r * 1.9f); IconLv(g, x, y, r, lv); return; }
		Color c = PCol[i]; IconBase(g, x, y, r, c, lv); float u = r / 12f; Color w = Color.FromArgb(250, 250, 255);
		switch (i)
		{
			case 0: g.FillEllipse(Br(c), x - 7 * u, y - 6 * u, 8 * u, 8 * u); g.FillEllipse(Br(c), x - 1 * u, y - 6 * u, 8 * u, 8 * u); g.FillPolygon(Br(c), new PointF[] { new PointF(x - 7 * u, y - 1.5f * u), new PointF(x + 7 * u, y - 1.5f * u), new PointF(x, y + 8 * u) }); break;
			case 1: using (Pen pp = new Pen(c, 2.4f * u)) { pp.StartCap = LineCap.Round; pp.EndCap = LineCap.Round; g.DrawLines(pp, new PointF[] { new PointF(x - 6 * u, y - 6 * u), new PointF(x, y), new PointF(x - 6 * u, y + 6 * u) }); g.DrawLines(pp, new PointF[] { new PointF(x + 1 * u, y - 6 * u), new PointF(x + 7 * u, y), new PointF(x + 1 * u, y + 6 * u) }); } break;
			case 2: using (Pen pp = new Pen(c, 3.6f * u)) g.DrawArc(pp, x - 7 * u, y - 7 * u, 14 * u, 14 * u, 180, 180); g.FillRectangle(Br(c), x - 8.8f * u, y, 3.6f * u, 6 * u); g.FillRectangle(Br(c), x + 5.2f * u, y, 3.6f * u, 6 * u); g.FillRectangle(Br(w), x - 8.8f * u, y + 4 * u, 3.6f * u, 2.5f * u); g.FillRectangle(Br(w), x + 5.2f * u, y + 4 * u, 3.6f * u, 2.5f * u); break;
			case 3: g.FillPolygon(Br(c), new PointF[] { new PointF(x, y - 9 * u), new PointF(x + 7 * u, y), new PointF(x + 3 * u, y), new PointF(x + 3 * u, y + 8 * u), new PointF(x - 3 * u, y + 8 * u), new PointF(x - 3 * u, y), new PointF(x - 7 * u, y) }); break;
			case 4: g.FillEllipse(Br(w), x - 8 * u, y - 8 * u, 16 * u, 16 * u); using (Pen pp = new Pen(Color.FromArgb(70, 50, 30), 1.8f * u)) { g.DrawLine(pp, x, y, x, y - 5 * u); g.DrawLine(pp, x, y, x + 4 * u, y + 2 * u); } g.FillEllipse(Br(c), x - 1.2f * u, y - 1.2f * u, 2.4f * u, 2.4f * u); break;
			case 5: g.FillPie(Br(c), x - 8 * u, y - 7 * u, 16 * u, 16 * u, 0, 180); g.FillRectangle(Br(Theme.Mix(c, Color.White, 0.5)), x - 6 * u, y - 8 * u, 12 * u, 2.5f * u); break;
			case 6: Rr(g, c, x - 8 * u, y - 5 * u, 16 * u, 10 * u, 2 * u); Star(g, x, y, 4.5f * u, Color.FromArgb(255, 255, 255)); break;
			case 7: g.FillEllipse(Br(w), x - 9 * u, y - 5 * u, 18 * u, 10 * u); g.FillEllipse(Br(c), x - 4 * u, y - 4.5f * u, 8 * u, 8 * u); g.FillEllipse(Br(Color.FromArgb(30, 30, 50)), x - 1.8f * u, y - 2.3f * u, 3.6f * u, 3.6f * u); break;
			case 8: GraphicsState s8 = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(-40f); Rr(g, c, -8 * u, -3.5f * u, 8 * u, 7 * u, 3 * u); Rr(g, w, 0, -3.5f * u, 8 * u, 7 * u, 3 * u); g.Restore(s8); break;
			case 9: Rr(g, c, x - 7 * u, y - 6 * u, 14 * u, 12 * u, 2 * u); g.FillRectangle(Br(w), x - 5 * u, y - 4 * u, 4.2f * u, 8 * u); g.FillRectangle(Br(w), x + 0.8f * u, y - 4 * u, 4.2f * u, 8 * u); break;
			default: Rr(g, w, x - 7 * u, y - 5 * u, 14 * u, 12 * u, 2 * u); g.FillRectangle(Br(c), x - 7 * u, y - 5 * u, 14 * u, 4 * u); using (Pen pp = new Pen(Color.FromArgb(220, 60, 90), 2f * u)) { g.DrawLine(pp, x - 3 * u, y + 3 * u, x - 1 * u, y + 5 * u); g.DrawLine(pp, x - 1 * u, y + 5 * u, x + 4 * u, y + 0.5f * u); } break;
		}
		IconLv(g, x, y, r, lv);
	}
	private static void IconR(Graphics g, int i, float x, float y, float r, int n)
	{
		g.FillEllipse(Br(Color.FromArgb(215, 14, 16, 34)), x - r, y - r, r * 2, r * 2);
		GraphicsState st = g.Save(); g.TranslateTransform(x, y); float k = r / 17f; g.ScaleTransform(k, k); DrawRelic(g, 0, 0, i, 0.0); g.Restore(st);
		if (n > 1) IconLv(g, x, y, r, n);
	}
	private static void DrawRelic(Graphics g, float sx, float sy, int r, double t)
	{
		float pul = 1f + 0.08f * (float)Math.Sin(t * 5);
		g.FillEllipse(Br(Color.FromArgb(55, 255, 226, 120)), sx - 27 * pul, sy - 27 * pul, 54 * pul, 54 * pul);
		for (int i = 0; i < 4; i++) { double a = t * 1.6 + i * Math.PI / 2; g.FillEllipse(Br(Color.FromArgb(220, 255, 250, 200)), sx + (float)Math.Cos(a) * 23f - 2f, sy + (float)Math.Sin(a) * 23f - 2f, 4, 4); }
		g.FillEllipse(Br(Color.FromArgb(255, 252, 232)), sx - 16, sy - 16, 32, 32); using (Pen pp = new Pen(Color.FromArgb(255, 205, 70), 2.6f)) g.DrawEllipse(pp, sx - 16, sy - 16, 32, 32);
		{ string rs = r == 0 ? "ms_ow_cert" : (r == 1 ? "ms_svi0" : (r == 2 ? "ms_svi1" : (r == 3 ? "ms_ow_clock" : (r == 4 ? "ms_so_ok" : (r == 5 ? "ms_svi2" : (r == 6 ? "ms_svi3" : (r == 7 ? "ms_ui_cup" : null))))))); Bitmap rb = rs == null ? null : Spr.Get(rs); if (rb != null) { float rh = 26f, rw = rh * rb.Width / rb.Height; if (rw > 30f) { rw = 30f; rh = rw * rb.Height / rb.Width; } Spr.Draw(g, rs, sx, sy, rw, rh, 0f); return; } }
		if (r == 0) { g.FillEllipse(Br(Color.FromArgb(200, 160, 110)), sx - 11, sy - 6, 5, 12); g.FillEllipse(Br(Color.FromArgb(200, 160, 110)), sx + 6, sy - 6, 5, 12); g.FillRectangle(Br(Color.FromArgb(250, 238, 205)), sx - 8, sy - 6, 16, 12); g.FillRectangle(Br(Color.FromArgb(210, 50, 60)), sx - 1.5f, sy - 6, 3, 12); g.FillEllipse(Br(Color.FromArgb(210, 50, 60)), sx + 3, sy, 5, 5); }
		else if (r == 1) { Rr(g, Color.FromArgb(150, 160, 175), sx - 11, sy - 8, 22, 16, 3); g.FillEllipse(Br(Color.White), sx - 9, sy - 6, 8, 12); g.FillEllipse(Br(Color.FromArgb(225, 70, 60)), sx + 1, sy - 6, 8, 5); g.FillEllipse(Br(Color.FromArgb(90, 170, 80)), sx + 1, sy + 0.5f, 8, 5); }
		else if (r == 2) { Rr(g, Color.FromArgb(140, 90, 60), sx - 4, sy - 12, 8, 11, 3); Rr(g, Color.FromArgb(60, 170, 160), sx - 10, sy - 2, 20, 6, 2); g.FillRectangle(Br(Color.FromArgb(40, 120, 115)), sx - 10, sy + 3, 20, 3); }
		else if (r == 3) { g.FillEllipse(Br(Color.FromArgb(230, 70, 70)), sx - 9, sy - 12, 6, 6); g.FillEllipse(Br(Color.FromArgb(230, 70, 70)), sx + 3, sy - 12, 6, 6); g.FillEllipse(Br(Color.White), sx - 9, sy - 9, 18, 18); using (Pen pp = new Pen(Color.FromArgb(70, 80, 110), 2f)) { g.DrawEllipse(pp, sx - 9, sy - 9, 18, 18); g.DrawLine(pp, sx, sy, sx, sy - 6); g.DrawLine(pp, sx, sy, sx + 5, sy + 2); } }
		else if (r == 5) { Rr(g, Color.FromArgb(90, 150, 230), sx - 12, sy - 8, 24, 16, 3); g.FillRectangle(Br(Color.FromArgb(255, 255, 255)), sx + 3, sy - 8, 2, 16); g.FillPolygon(Br(Color.White), new PointF[] { new PointF(sx - 8, sy - 1), new PointF(sx + 1, sy - 5), new PointF(sx - 3, sy), new PointF(sx + 1, sy + 5) }); }
		else if (r == 6) { Rr(g, Color.FromArgb(250, 205, 70), sx - 10, sy - 7, 20, 14, 3); g.FillEllipse(Br(Color.FromArgb(255, 240, 160)), sx - 5, sy - 5, 10, 10); g.FillRectangle(Br(Color.FromArgb(150, 90, 30)), sx - 1, sy - 3, 2, 6); }
		else if (r == 7) { Rr(g, Color.FromArgb(215, 170, 60), sx - 11, sy - 11, 22, 22, 4); Rr(g, Color.FromArgb(255, 235, 150), sx - 7, sy - 7, 14, 14, 3); Txt(g, "공", 9f, 2, Color.FromArgb(120, 70, 20), sx - 8, sy - 9, 16, 18, StringAlignment.Center); }
		else { g.FillEllipse(Br(Color.FromArgb(215, 45, 60)), sx - 11, sy - 11, 22, 22); using (Pen pp = new Pen(Color.FromArgb(255, 235, 230), 1.6f)) g.DrawEllipse(pp, sx - 8, sy - 8, 16, 16); Txt(g, "결", 9f, 2, Color.White, sx - 12, sy - 10, 24, 20, StringAlignment.Center); }
	}
	private PointF Hot { get { return hotP; } }
	private PointF hotP;
	private static void Star(Graphics g, float cx, float cy, float r, Color c)
	{
		PointF[] pts = new PointF[10];
		for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float rr = (i & 1) == 0 ? r : r * 0.45f; pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr); }
		using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, pts);
		using (Pen p = new Pen(Color.FromArgb(160, 140, 90, 0), 1.4f)) g.DrawPolygon(p, pts);
	}
}

// ── 공무원 맞춤: 민원 분류 (민원 내용을 읽고 알맞은 부서로 빠르게 넘겨요) ──
internal sealed class ComplaintGame : MiniGame
{
	internal static readonly string[] Dept = { "건설과", "환경과", "교통과", "복지과" };
	internal static readonly Color[] DCol = { Color.FromArgb(255, 170, 70), Color.FromArgb(100, 200, 120), Color.FromArgb(90, 160, 250), Color.FromArgb(240, 120, 160) };
	internal static readonly string[] DSub = { "도로 · 시설물", "쓰레기 · 소음", "주정차 · 버스", "연금 · 돌봄" };
	internal static readonly string[] Txts = {
		"집 앞 도로가\n움푹 패였어요", "가로등이\n나갔어요", "보도블록이\n깨졌어요", "하수구가\n막혔어요", "공원 벤치가\n부러졌어요",
		"옆집 쓰레기\n불법 투기", "밤마다 공사\n소음이 심해요", "하천에서\n악취가 나요", "음식물 쓰레기\n수거가 안 왔어요", "재활용 분리배출\n문의",
		"아파트 앞\n불법 주정차", "버스 배차\n간격 문의", "횡단보도 신호\n시간이 짧아요", "주차 단속\n이의신청", "자전거도로에\n차가 서 있어요",
		"기초연금\n신청하고 싶어요", "독거 어르신\n안부 확인", "아이 돌봄\n서비스 문의", "긴급 생계\n지원 문의", "장애인 활동\n지원 신청" };
	internal static readonly int[] Ans = { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3 };
	private readonly List<int> bag = new List<int>();
	private double left; private int cur, combo, solved, last = -1; private float slideT, outT, flashT, msgT; private bool flashOk; private string msg = ""; private int outDir;
	public override string Name { get { return "민원 분류"; } }
	public override string Key { get { return "complaint"; } }
	public override string Desc { get { return "알맞은 부서로 빠르게!"; } }
	public override string Help { get { return "민원 내용을 읽고 알맞은 부서 버튼을 눌러요 (숫자 1·2·3·4 키도 돼요)\n맞히면 시간이 조금 늘고 연속 점수가 올라가요 · 틀리면 시간이 줄어요\n공무원이라면 쉽다고요? 빨라지면 정신이 없어요!"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 250, 245))) g.FillRectangle(b, cx - 14, cy - 18, 28, 36);
		using (Pen p = new Pen(Color.FromArgb(120, 130, 150), 2f)) { g.DrawLine(p, cx - 8, cy - 8, cx + 8, cy - 8); g.DrawLine(p, cx - 8, cy, cx + 8, cy); g.DrawLine(p, cx - 8, cy + 8, cx + 2, cy + 8); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 80, 80))) g.FillEllipse(b, cx + 4, cy + 6, 14, 14);
	}
	public override void Reset() { left = 45; Score = 0; Over = false; combo = 0; solved = 0; bag.Clear(); last = -1; msg = ""; msgT = 0; flashT = 0; outT = 0; Next(); }
	private void Next()
	{
		if (bag.Count == 0) { for (int i = 0; i < Txts.Length; i++) bag.Add(i); }
		int k; int guard = 0; do { k = Rnd.Next(bag.Count); } while (bag[k] == last && bag.Count > 1 && guard++ < 6);
		cur = bag[k]; bag.RemoveAt(k); last = cur; slideT = 0f;
	}
	private static RectangleF Btn(int i) { return new RectangleF(20 + (i % 2) * 186, 360 + (i / 2) * 104, 174, 92); }
	public override void Click(float x, float y) { if (Over || outT > 0f) return; for (int i = 0; i < 4; i++) if (Btn(i).Contains(x, y)) { Answer(i); return; } }
	public override void Press(Keys k) { if (k >= Keys.D1 && k <= Keys.D4) Answer(k - Keys.D1); else if (k >= Keys.NumPad1 && k <= Keys.NumPad4) Answer(k - Keys.NumPad1); }
	private void Answer(int d)
	{
		if (Over || outT > 0f) return;
		if (d == Ans[cur])
		{
			combo++; solved++; int pts = 10 + Math.Min(combo, 10) * 2; Score += pts; left = Math.Min(60 - PlayT, left + 0.7); flashOk = true; flashT = 0.25f; outDir = d % 2 == 0 ? -1 : 1; outT = 0.18f;
			msg = combo >= 3 ? combo + "연속! +" + pts : "+" + pts; msgT = 0.7f; Sfx.Play(combo >= 5 ? "ok2" : "ok"); Fx.Burst(200f, 230f, DCol[d], 8);
		}
		else { combo = 0; left -= 2.5; flashOk = false; flashT = 0.3f; msg = "다른 부서예요! -2.5초"; msgT = 0.9f; Shake = 0.3f; Sfx.Play("bad2"); }
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		hotP = new PointF(inp.Mx, inp.My);
		left -= dt; if (left > 60 - PlayT) left = 60 - PlayT; if (left <= 0) { left = 0; Over = true; return; }
		if (flashT > 0) flashT -= dt; if (msgT > 0) msgT -= dt; if (slideT < 1f) slideT = Math.Min(1f, slideT + dt * 7f);
		if (outT > 0f) { outT -= dt; if (outT <= 0f) Next(); }
	}
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_desk", Color.FromArgb(70, 84, 112), Color.FromArgb(40, 48, 74));
		Txt(g, "접수된 민원", 11f, 1, Color.FromArgb(200, 255, 255, 255), 0, 78, W, 22, StringAlignment.Center);
		float ox = outT > 0f ? outDir * (1f - outT / 0.18f) * 260f : (1f - slideT) * -120f; float al = outT > 0f ? outT / 0.18f : slideT;
		RectangleF dr = new RectangleF(40 + ox, 108, 320, 200);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(dr.X + 3, dr.Y + 5, dr.Width, dr.Height), 18)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(70 * al), 0, 0, 0))) g.FillPath(b, gp);
		using (GraphicsPath gp = Gfx.Round(dr, 18)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * al), 252, 250, 244))) g.FillPath(b, gp);
		using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * al), 230, 90, 80))) g.FillRectangle(b, dr.X + 22, dr.Y + 18, 60, 6);
		using (Pen p = new Pen(Color.FromArgb((int)(80 * al), 120, 130, 150), 2f)) { g.DrawLine(p, dr.X + 22, dr.Y + 160, dr.X + dr.Width - 22, dr.Y + 160); g.DrawLine(p, dr.X + 22, dr.Y + 176, dr.X + dr.Width - 80, dr.Y + 176); }
		if (Spr.Get("ms_dp4") != null) { string fn = flashT > 0f && flashOk ? "ms_dp5" : "ms_dp4"; Spr.Draw(g, fn, dr.X + dr.Width - 44f, dr.Y + 36f, 44f, 44f * 150f / 131f, 6f); }
		string[] ln = Txts[cur].Split('\n');
		for (int i = 0; i < ln.Length; i++) Txt(g, ln[i], 20f, 2, Color.FromArgb((int)(255 * al), 40, 48, 70), dr.X + ox * 0f, dr.Y + 52 + i * 38 - (ln.Length - 1) * 8, dr.Width, 36, StringAlignment.Center);
		Txt(g, "어느 부서일까요?", 11f, 1, Color.FromArgb((int)(180 * al), 90, 100, 120), dr.X, dr.Y + 134, dr.Width, 22, StringAlignment.Center);
		for (int i = 0; i < 4; i++)
		{
			RectangleF r = Btn(i); bool hot = r.Contains(Hot.X, Hot.Y);
			using (GraphicsPath gp = Gfx.Round(r, 18)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(hot ? 255 : 235, DCol[i]))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawPath(p, gp); }
			bool dic = Spr.Get("ms_dp0") != null; if (dic) { Bitmap db = Spr.Get("ms_dp" + i); float dh = 54f, dw = dh * db.Width / db.Height; if (dw > 60f) { dw = 60f; dh = dw * db.Height / db.Width; } Spr.Draw(g, "ms_dp" + i, r.X + 38f, r.Y + r.Height / 2f, dw, dh, 0f); }
			Txt(g, (i + 1) + "  " + Dept[i], 16f, 2, Color.FromArgb(40, 36, 50), r.X + (dic ? 34f : 0f), r.Y + 20, r.Width - (dic ? 34f : 0f), 28, StringAlignment.Center);
			Txt(g, DSub[i], 10f, 1, Color.FromArgb(120, 40, 36, 50), r.X + (dic ? 34f : 0f), r.Y + 52, r.Width - (dic ? 34f : 0f), 22, StringAlignment.Center);
		}
		if (combo >= 2) Txt(g, combo + "연속", 13f, 2, Color.FromArgb(255, 226, 120), 16, 84, 120, 24, StringAlignment.Near);
		if (msgT > 0) Txt(g, msg, 14f, 2, flashOk ? Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2)), 255, 236, 130) : Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2)), 255, 130, 130), 0, 316, W, 28, StringAlignment.Center);
		if (flashT > 0) using (SolidBrush b = new SolidBrush(flashOk ? Color.FromArgb((int)(40 * flashT / 0.25f), 120, 255, 160) : Color.FromArgb((int)(60 * flashT / 0.3f), 255, 60, 60))) g.FillRectangle(b, 0, 0, W, H);
		DrawMascot(g, 350, 80, 46, (float)Math.Sin(t * 3) * 2);
	}
	private PointF Hot { get { return hotP; } }
	private PointF hotP;
}

// ── 공무원 맞춤: 결재·민원 분류 (결재 분류 + 민원 분류를 합쳤어요) ──
internal sealed class OfficeGame : MiniGame
{
	private readonly List<int> bag = new List<int>();
	private double left; private int cur, combo, solved, last = -1, rule, docNo; private bool classify; private float slideT, outT, flashT, msgT, ruleT; private bool flashOk; private string msg = ""; private int outDir;
	public override string Costume { get { return "정장"; } }
	public override string Name { get { return "결재·민원 분류"; } }
	public override string Key { get { return "stamp"; } }
	public override string Desc { get { return "읽고 분류하고 결재!"; } }
	public override string Help { get { return "서류는 두 가지로 나와요\n[분류] 알맞은 부서 버튼을 눌러요 (1·2·3·4 키)\n[결재] 위 규칙(오늘의 결재 부서)에 맞으면 결재(→), 아니면 반려(←)\n규칙은 몇 장마다 바뀌니 배너를 잘 보세요! · 오답 -2.5초"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (GraphicsPath p = Gfx.Round(new RectangleF(cx - 14, cy - 18, 28, 36), 4)) using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 70, 70))) g.FillEllipse(b, cx - 2, cy + 2, 14, 14);
		using (Pen p = new Pen(Color.FromArgb(120, 130, 150), 2f)) { g.DrawLine(p, cx - 8, cy - 9, cx + 8, cy - 9); g.DrawLine(p, cx - 8, cy - 3, cx + 8, cy - 3); }
	}
	public override void Reset() { left = 45; Score = 0; Over = false; combo = 0; solved = 0; bufMode = 0; bag.Clear(); last = -1; docNo = 0; msg = ""; msgT = 0; flashT = 0; outT = 0; rule = Rnd.Next(4); ruleT = 1.2f; Next(); }
	private void Next()
	{
		if (bag.Count == 0) for (int i = 0; i < ComplaintGame.Txts.Length; i++) bag.Add(i);
		int k; int guard = 0; do { k = Rnd.Next(bag.Count); } while (bag[k] == last && bag.Count > 1 && guard++ < 6);
		cur = bag[k]; bag.RemoveAt(k); last = cur; slideT = 0f; docNo++;
		if (docNo % (solved >= 20 ? 4 : 6) == 0) { int nr; do { nr = Rnd.Next(4); } while (nr == rule); rule = nr; ruleT = 1.4f; }
		classify = Rnd.NextDouble() < 0.5;
	}
	private static RectangleF DBtn(int i) { return new RectangleF(20 + (i % 2) * 186, 372 + (i / 2) * 96, 174, 86); }
	private static RectangleF Rej { get { return new RectangleF(20, 380, 174, 150); } }
	private static RectangleF App { get { return new RectangleF(206, 380, 174, 150); } }
	private int bufMode; private float bufX, bufY; private Keys bufKey;   // 서류가 넘어가는 동안 누른 입력도 기억했다가 다음 서류에 써요
	public override void Click(float x, float y)
	{
		if (Over) return; if (outT > 0f) { bufMode = 1; bufX = x; bufY = y; return; }
		if (classify) { for (int i = 0; i < 4; i++) if (DBtn(i).Contains(x, y)) { Answer(i == ComplaintGame.Ans[cur], i); return; } }
		else { if (Rej.Contains(x, y)) Answer(ComplaintGame.Ans[cur] != rule, 0); else if (App.Contains(x, y)) Answer(ComplaintGame.Ans[cur] == rule, 1); }
	}
	public override void Press(Keys k)
	{
		if (Over) return; if (outT > 0f) { bufMode = 2; bufKey = k; return; }
		if (classify) { if (k >= Keys.D1 && k <= Keys.D4) Answer(k - Keys.D1 == ComplaintGame.Ans[cur], k - Keys.D1); else if (k >= Keys.NumPad1 && k <= Keys.NumPad4) Answer(k - Keys.NumPad1 == ComplaintGame.Ans[cur], k - Keys.NumPad1); }
		else { if (k == Keys.Left || k == Keys.A) Answer(ComplaintGame.Ans[cur] != rule, 0); else if (k == Keys.Right || k == Keys.D) Answer(ComplaintGame.Ans[cur] == rule, 1); }
	}
	private void Answer(bool ok, int which)
	{
		if (ok)
		{
			combo++; solved++; int pts = 10 + Math.Min(combo, 10) * 2; Score += pts; left = Math.Min(60 - PlayT, left + (solved >= 25 ? 0.5 : 0.7)); flashOk = true; flashT = 0.25f; outDir = classify ? (which % 2 == 0 ? -1 : 1) : (which == 0 ? -1 : 1); outT = 0.18f;
			msg = combo >= 3 ? combo + "연속! +" + pts : "+" + pts; msgT = 0.7f; Sfx.Play(combo >= 5 ? "ok2" : "ok"); Fx.Burst(200f, 230f, Color.FromArgb(255, 226, 120), 8);
		}
		else { combo = 0; left -= 2.5; flashOk = false; flashT = 0.3f; msg = classify ? "다른 부서예요! -2.5초" : "규칙을 확인하세요! -2.5초"; msgT = 0.9f; Shake = 0.3f; Sfx.Play("bad2"); }
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		hotP = new PointF(inp.Mx, inp.My);
		left -= dt; if (left > 60 - PlayT) left = 60 - PlayT; if (left <= 0) { left = 0; Over = true; return; }
		if (flashT > 0) flashT -= dt; if (msgT > 0) msgT -= dt; if (ruleT > 0) ruleT -= dt; if (slideT < 1f) slideT = Math.Min(1f, slideT + dt * 7f);
		if (outT > 0f) { outT -= dt; if (outT <= 0f) { Next(); if (bufMode == 1) { bufMode = 0; Click(bufX, bufY); } else if (bufMode == 2) { bufMode = 0; Press(bufKey); } } }
	}
	private PointF hotP;
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_desk", Color.FromArgb(70, 84, 112), Color.FromArgb(40, 48, 74));
		// 오늘의 결재 규칙 배너
		float pulse = ruleT > 0 ? 1f + 0.04f * (float)Math.Sin(t * 30) : 1f;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(20, 74, 360, 40), 14)) using (SolidBrush b = new SolidBrush(Color.FromArgb(ruleT > 0 ? 210 : 140, ComplaintGame.DCol[rule]))) g.FillPath(b, gp);
		if (Spr.Get("ms_of_clip") != null) Spr.Draw(g, "ms_of_clip", 44f, 94f, 30f * 68f / 90f * 1.0f + 6f, 36f, -8f + (ruleT > 0 ? (float)Math.Sin(t * 20) * 8f : 0f));
		Txt(g, "오늘의 결재 : " + ComplaintGame.Dept[rule] + " 서류만 결재!", 13f * pulse, 2, Color.FromArgb(40, 36, 50), 20, 74, 360, 40, StringAlignment.Center);
		Txt(g, classify ? "[분류]  알맞은 부서는?" : "[결재]  규칙에 맞으면 결재 →", 12f, 2, Color.FromArgb(255, 236, 150), 0, 122, W, 22, StringAlignment.Center);
		float ox = outT > 0f ? outDir * (1f - outT / 0.18f) * 260f : (1f - slideT) * -120f; float al = outT > 0f ? outT / 0.18f : slideT;
		RectangleF dr = new RectangleF(50 + ox, 150, 300, 200);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(dr.X + 3, dr.Y + 5, dr.Width, dr.Height), 18)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(70 * al), 0, 0, 0))) g.FillPath(b, gp);
		using (GraphicsPath gp = Gfx.Round(dr, 18)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * al), 252, 250, 244))) g.FillPath(b, gp);
		using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * al), 230, 90, 80))) g.FillRectangle(b, dr.X + 22, dr.Y + 18, 60, 6);
		string[] ln = ComplaintGame.Txts[cur].Split('\n');
		for (int i = 0; i < ln.Length; i++) Txt(g, ln[i], 20f, 2, Color.FromArgb((int)(255 * al), 40, 48, 70), dr.X, dr.Y + 54 + i * 38 - (ln.Length - 1) * 8, dr.Width, 36, StringAlignment.Center);
		if (flashT > 0f) { float pp = Math.Min(1f, 1f - flashT / (flashOk ? 0.25f : 0.3f)), pop = pp < 0.35f ? 1.9f - 0.9f * (pp / 0.35f) : 1f; float aa = Math.Min(1f, flashT * 6f);   // 결재 도장 / 반려 도장이 쾅 찍혀요
			{ float mx0 = flashOk ? dr.X + dr.Width - 62 : dr.X + dr.Width / 2f, my0 = flashOk ? dr.Y + dr.Height - 52 : dr.Y + dr.Height / 2f + 40f; float lift = pp < 0.35f ? (1f - pp / 0.35f) * 90f : (pp - 0.35f) / 0.65f * 70f; if (Spr.Get("ms_so_ok") != null) DrawFx(g, flashOk ? "so_ok" : "so_no", mx0, my0 - 52f - lift, 64f, pp < 0.35f ? 1f : Math.Max(0f, 1f - (pp - 0.35f) / 0.65f)); }
			if (flashOk) Spr.Draw(g, "ms_sp1", dr.X + dr.Width - 62, dr.Y + dr.Height - 52, 70f * pop, 70f * pop * 0.95f, -8f); else Spr.Draw(g, "ms_sp2", dr.X + dr.Width / 2f, dr.Y + dr.Height / 2f + 40f, 118f * pop, 118f * pop * 0.62f, -14f); }
		if (classify)
			for (int i = 0; i < 4; i++)
			{
				RectangleF r = DBtn(i); bool hot = r.Contains(hotP.X, hotP.Y);
				using (GraphicsPath gp = Gfx.Round(r, 18)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(hot ? 255 : 235, ComplaintGame.DCol[i]))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawPath(p, gp); }
				bool dic2 = Spr.Get("ms_dp0") != null; if (dic2) { Bitmap db = Spr.Get("ms_dp" + i); float dh = 54f, dw = dh * db.Width / db.Height; if (dw > 60f) { dw = 60f; dh = dw * db.Height / db.Width; } Spr.Draw(g, "ms_dp" + i, r.X + 38f, r.Y + r.Height / 2f, dw, dh, 0f); }
				Txt(g, (i + 1) + "  " + ComplaintGame.Dept[i], 15f, 2, Color.FromArgb(40, 36, 50), r.X + (dic2 ? 34f : 0f), r.Y + 16, r.Width - (dic2 ? 34f : 0f), 28, StringAlignment.Center);
				Txt(g, ComplaintGame.DSub[i], 9.5f, 1, Color.FromArgb(120, 40, 36, 50), r.X + (dic2 ? 34f : 0f), r.Y + 46, r.Width - (dic2 ? 34f : 0f), 22, StringAlignment.Center);
			}
		else
		{
			bool h1 = Rej.Contains(hotP.X, hotP.Y), h2 = App.Contains(hotP.X, hotP.Y);
			using (GraphicsPath gp = Gfx.Round(Rej, 22)) using (SolidBrush b = new SolidBrush(Color.FromArgb(h1 ? 255 : 230, 220, 90, 90))) g.FillPath(b, gp);
			using (GraphicsPath gp = Gfx.Round(App, 22)) using (SolidBrush b = new SolidBrush(Color.FromArgb(h2 ? 255 : 230, 90, 190, 120))) g.FillPath(b, gp);
			Txt(g, "← 반려", 20f, 2, Color.White, Rej.X, Rej.Y, Rej.Width, Rej.Height, StringAlignment.Center); Txt(g, "결재 →", 20f, 2, Color.White, App.X, App.Y, App.Width, App.Height, StringAlignment.Center);
		}
		if (combo >= 2) Txt(g, combo + "연속", 13f, 2, Color.FromArgb(255, 226, 120), 16, 44, 120, 24, StringAlignment.Near);
		if (msgT > 0) Txt(g, msg, 14f, 2, flashOk ? Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2)), 255, 236, 130) : Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2)), 255, 130, 130), 0, 346, W, 26, StringAlignment.Center);
		if (flashT > 0) using (SolidBrush b = new SolidBrush(flashOk ? Color.FromArgb((int)(40 * flashT / 0.25f), 120, 255, 160) : Color.FromArgb((int)(60 * flashT / 0.3f), 255, 60, 60))) g.FillRectangle(b, 0, 0, W, H);
	}
}
