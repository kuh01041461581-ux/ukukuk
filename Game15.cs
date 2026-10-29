using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 마룡이 퍼즐: 가득 찬 판에서 옆 칸과 자리를 바꿔 같은 마룡이 3개 이상을 이어 터뜨려요 (캔디크러시·애니팡 방식) ───────
// 4개 → 줄 폭탄(가로/세로 한 줄 제거) · 5개 이상 → 무지개 폭탄(바꾼 마룡이와 같은 종류 전부 제거)
internal sealed class MatchPuzzleGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private const int N = 8; private const float CELL = 44f, GX = 24f, GY = 118f;
	private readonly int[,] grid = new int[N, N], spec = new int[N, N];   // spec: 0 없음 · 1 가로 드릴 · 2 세로 드릴 · 3 무지개 · 4 폭탄 · 5 십자 드릴 · 6 X 드릴
	private readonly bool[,] jelly = new bool[N, N]; private readonly int[,] lockc = new int[N, N]; private int obj, objType, objN, objGot, itemMode = -1; private readonly int[] inv = new int[3]; private static readonly string[] IName = { "망치", "섞기", "폭탄" };
	private readonly bool[,] mark = new bool[N, N]; private readonly float[,] off = new float[N, N], vel = new float[N, N];
	private static readonly string[] Spr1 = { "ms_pet_stand0", "ms_pet_happy0", "ms_pet_sit0", "ms_pet_eat0", "ms_pet_yawn0" };
	private static readonly Color[] Col = { Color.FromArgb(120, 170, 255), Color.FromArgb(255, 150, 190), Color.FromArgb(120, 205, 140), Color.FromArgb(255, 190, 100), Color.FromArgb(190, 150, 240) };
	private int stageBase, stage, moves, goal, types, chain, state, selR = -1, selC = -1, swR1, swC1, swR2, swC2, hintR = -1, hintC = -1, hintR2, hintC2, lastR = -1, lastC = -1, comboMax; private bool swapBack;
	private float stateT, idleT, msgT, overT, shk; private string msg = "";
	// state: 0 대기 · 1 바꾸는 중 · 2 터지는 중 · 3 떨어지는 중
	public override string Costume { get { return "g_puzzle"; } }
	public override string Name { get { return "마룡이 퍼즐"; } }
	public override string Key { get { return "puzzle"; } }
	public override string Desc { get { return "같은 마룡이 3개를 맞춰 터뜨려요"; } }
	public override string Help { get { return "마룡이를 눌러 고른 뒤 옆 칸을 누르거나, 끌어서 자리를 바꿔요\n같은 마룡이 3개 이상이 한 줄로 모이면 터져요\n4개 한 줄: 가로/세로 드릴 (한 줄 전부 제거)\nL자 모양: 폭탄 (3×3 제거) · T자·十자 모양: 십자 드릴 (가로+세로)\n5개 이상: 무지개 (같은 종류 전부 제거)\n특수 블록은 옆 칸과 바꾸면 바로 터져요 · 특수끼리 바꾸면 합체 폭발!\n무지개 + 특수 = 같은 종류가 모두 그 특수로 변신\n3연쇄 이상이면 보너스 특수 블록(폭탄·X 드릴)이 나와요\n스테이지마다 목표가 달라요: 점수 · 특정 마룡이 모으기 · 젤리 없애기\n5스테이지부터 얼음 블록(옆에서 두 번), 6스테이지부터 돌 블록, 9스테이지부터 철 블록(폭탄·드릴로만 두 번 쳐서 깨요)이 나와요. 굳은 블록은 못 움직이고, 옆에서 줄이 터지면 깨져요 (금이 간 블록은 두 번)\n4스테이지부터 자물쇠 칸이 나와요: 못 움직이지만 줄에 끼면 풀려요 (겹자물쇠는 두 번)\n아래 아이템(망치·섞기·폭탄)은 클리어할 때마다 하나씩 얻어요\n정해진 횟수 안에 목표를 이루면 다음 스테이지!"; } }
	public override string RightText { get { return "스테이지 " + stage + " · " + moves + "번"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		for (int i = 0; i < 3; i++) using (GraphicsPath gp = Gfx.Round(new RectangleF(cx - 20 + i * 14, cy - 6 + (i == 1 ? -8 : 4), 14, 14), 4)) using (SolidBrush b = new SolidBrush(Col[i])) g.FillPath(b, gp);
	}
	public override void Reset() { inv[0] = 1; inv[1] = 1; inv[2] = 1; itemMode = -1; stage = 1; Score = 0; Over = false; comboMax = 0; msg = ""; msgT = 0f; overT = 0f; NewStage(); }
	private void NewStage()
	{
		types = stage >= 2 ? 5 : 4; moves = 18; stageBase = Score; goal = 500 + (stage - 1) * 300; state = 0; chain = 0; selR = selC = -1; hintR = -1; idleT = 0f; lastR = -1;
		Fill(); while (!HasMove()) Fill();
		obj = stage == 1 ? 0 : (stage % 3 == 2 ? 1 : stage % 3 == 0 ? 2 : 0); objGot = 0; itemMode = -1; Array.Clear(jelly, 0, jelly.Length); Array.Clear(lockc, 0, lockc.Length);
		if (stage >= 5) { int k = Math.Min(8, stage - 3); while (k > 0) { int r = Rnd.Next(2, 7), c = Rnd.Next(N); if (spec[r, c] == 0 && grid[r, c] != 5) { int q = Rnd.Next(100); grid[r, c] = 5; spec[r, c] = (stage >= 9 && q < 18) ? 24 : ((stage < 6 || q < 50) ? 22 : (stage >= 10 && Rnd.Next(3) == 0 ? 21 : 20)); k--; } } while (!HasMove()) { Fill2(); } }
		if (stage >= 4) { int k = Math.Min(10, 2 + stage); while (k > 0) { int r = Rnd.Next(N), c = Rnd.Next(N); if (lockc[r, c] == 0) { lockc[r, c] = stage >= 8 && Rnd.Next(3) == 0 ? 2 : 1; k--; } } }
		if (obj == 1) { objType = Rnd.Next(types); objN = 14 + stage * 2; }
		if (obj == 2) { int k = Math.Min(30, 12 + stage * 2); while (k > 0) { int r = Rnd.Next(N), c = Rnd.Next(N); if (!jelly[r, c]) { jelly[r, c] = true; k--; } } }
	}
	private int JellyLeft() { int n = 0; for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (jelly[r, c]) n++; return n; }
	private bool Done() { return obj == 0 ? Score - stageBase >= goal : obj == 1 ? objGot >= objN : JellyLeft() == 0; }
	private float Prog() { return obj == 0 ? Math.Min(1f, Math.Max(0, Score - stageBase) / (float)goal) : obj == 1 ? Math.Min(1f, objGot / (float)objN) : 1f - JellyLeft() / (float)Math.Max(1, Math.Min(30, 12 + stage * 2)); }
	private void Fill()
	{
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++)
		{
			int v; int tries = 0;
			do { v = Rnd.Next(types); tries++; } while (tries < 20 && ((c >= 2 && grid[r, c - 1] == v && grid[r, c - 2] == v) || (r >= 2 && grid[r - 1, c] == v && grid[r - 2, c] == v)));
			grid[r, c] = v; spec[r, c] = 0; mark[r, c] = false; off[r, c] = 0f; vel[r, c] = 0f;
		}
	}
	// ── 규칙 ──
	private int RunLen(int r, int c, int dr, int dc)
	{
		int v = grid[r, c]; if (v < 0 || v == 5) return 0; int n = 1;
		for (int k = 1; k < N; k++) { int rr = r + dr * k, cc = c + dc * k; if (rr < 0 || rr >= N || cc < 0 || cc >= N || grid[rr, cc] != v) break; n++; }
		for (int k = 1; k < N; k++) { int rr = r - dr * k, cc = c - dc * k; if (rr < 0 || rr >= N || cc < 0 || cc >= N || grid[rr, cc] != v) break; n++; }
		return n;
	}
	private bool MatchAt(int r, int c) { return RunLen(r, c, 0, 1) >= 3 || RunLen(r, c, 1, 0) >= 3; }
	private void SwapCells(int r1, int c1, int r2, int c2) { int t = grid[r1, c1]; grid[r1, c1] = grid[r2, c2]; grid[r2, c2] = t; t = spec[r1, c1]; spec[r1, c1] = spec[r2, c2]; spec[r2, c2] = t; }
	private bool TrySwapOk(int r1, int c1, int r2, int c2)
	{
		if (lockc[r1, c1] > 0 || lockc[r2, c2] > 0 || spec[r1, c1] >= 20 || spec[r2, c2] >= 20) return false;
		if (spec[r1, c1] != 0 || spec[r2, c2] != 0) return true;
		SwapCells(r1, c1, r2, c2); bool ok = MatchAt(r1, c1) || MatchAt(r2, c2); SwapCells(r1, c1, r2, c2); return ok;
	}
	private bool HasMove()
	{
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++)
		{
			if (c + 1 < N && TrySwapOk(r, c, r, c + 1)) { hintR = r; hintC = c; hintR2 = r; hintC2 = c + 1; return true; }
			if (r + 1 < N && TrySwapOk(r, c, r + 1, c)) { hintR = r; hintC = c; hintR2 = r + 1; hintC2 = c; return true; }
		}
		hintR = -1; return false;
	}
	// ── 입력 ──
	private void UseItem(int k)
	{
		if (itemMode == k) { itemMode = -1; return; } itemMode = -1; if (inv[k] <= 0) { Sfx.Play("bad2"); msg = "클리어하면 아이템을 받아요"; msgT = 1.2f; return; } selR = -1;
		if (k == 0) { itemMode = 0; msg = "없앨 마룡이를 누르세요"; msgT = 1.5f; Sfx.Play("tick"); }
		else if (k == 1) { inv[1]--; Reshuffle(); }
		else { for (int i = 0; i < 30; i++) { int r = Rnd.Next(N), c = Rnd.Next(N); if (spec[r, c] == 0) { spec[r, c] = 4; inv[2]--; msg = "폭탄 생성!"; msgT = 1.1f; Sfx.Play("fanfare"); Fx.Burst(GX + c * CELL + CELL / 2f, GY + r * CELL + CELL / 2f, Color.FromArgb(255, 120, 90), 10); break; } } }
	}
	private bool Adj(int r1, int c1, int r2, int c2) { return Math.Abs(r1 - r2) + Math.Abs(c1 - c2) == 1; }
	public override void Click(float x, float y)
	{
		if (Over || state != 0) return;
		if (y >= 486f && y < 528f) { int k = (int)((x - 24f) / 124f); if (k >= 0 && k < 3 && x >= 24f) UseItem(k); return; }
		int c = (int)Math.Floor((x - GX) / CELL), r = (int)Math.Floor((y - GY) / CELL); if (r < 0 || r >= N || c < 0 || c >= N) { selR = -1; return; }
		idleT = 0f; hintR = -1;
		if (itemMode == 0) { itemMode = -1; inv[0]--; pendR = -1; for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) mark[i, j] = false; mark[r, c] = true; ExpandSpecials(); chain = 1; state = 2; stateT = 0f; Sfx.Play("thud2"); Shake = Math.Max(Shake, 0.08f); return; }
		if (selR < 0) { if (spec[r, c] >= 20) { Sfx.Play("bad2"); msg = "굳은 블록은 옆에서 줄을 맞춰 깨요 (철은 폭탄·드릴로만)"; msgT = 1.5f; return; } if (lockc[r, c] > 0) { Sfx.Play("bad2"); msg = "잠긴 칸은 옆 칸과 못 바꿔요 · 줄을 맞춰 풀어요"; msgT = 1.5f; return; } selR = r; selC = c; Sfx.Play("tick"); return; }
		if (selR == r && selC == c) { selR = -1; return; }
		if (Adj(selR, selC, r, c)) { if (lockc[r, c] > 0 || spec[r, c] >= 20) { Sfx.Play("bad2"); msg = "잠긴 칸이에요"; msgT = 1.2f; selR = -1; return; } StartSwap(selR, selC, r, c); selR = -1; } else if (lockc[r, c] > 0 || spec[r, c] >= 20) { Sfx.Play("bad2"); } else { selR = r; selC = c; Sfx.Play("tick"); }
	}
	public override void Swipe(float dx, float dy)
	{
		if (Over || state != 0 || selR < 0) return; int dr = 0, dc = 0; if (Math.Abs(dx) > Math.Abs(dy)) dc = dx > 0 ? 1 : -1; else dr = dy > 0 ? 1 : -1;
		int r = selR + dr, c = selC + dc; if (r < 0 || r >= N || c < 0 || c >= N) return; if (lockc[r, c] > 0 || spec[r, c] >= 20) { Sfx.Play("bad2"); selR = -1; return; } StartSwap(selR, selC, r, c); selR = -1;
	}
	private void StartSwap(int r1, int c1, int r2, int c2)
	{
		swR1 = r1; swC1 = c1; swR2 = r2; swC2 = c2; swapBack = false; state = 1; stateT = 0f; hintR = -1; idleT = 0f; lastR = r2; lastC = c2; chain = 0; Sfx.Play("tick");
	}
	// ── 진행 ──
	public override void Update(float dt, GameInput inp, double t)
	{
		if (dt > 0.05f) dt = 0.05f; if (msgT > 0f) msgT -= dt; if (shk > 0f) shk -= dt;
		if (Over) { overT += dt; return; }
		if (state == 0) { idleT += dt; if (idleT > 6f && hintR < 0) HasMove(); return; }
		stateT += dt;
		if (state == 1)
		{
			if (stateT >= 0.14f)
			{
				SwapCells(swR1, swC1, swR2, swC2);
				if (swapBack) { state = 0; idleT = 0f; return; }
				if (spec[swR1, swC1] != 0 || spec[swR2, swC2] != 0) { moves--; UseSpecials(); return; }
				if (MatchAt(swR1, swC1) || MatchAt(swR2, swC2)) { moves--; FindClears(); }
				else { swapBack = true; stateT = 0f; Sfx.Play("tick"); }
			}
			return;
		}
		if (state == 2) { if (stateT >= 0.26f) RemoveMarked(); return; }
		if (state == 3)
		{
			bool moving = false;
			for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (off[r, c] < 0f) { vel[r, c] += 46f * dt; off[r, c] += vel[r, c] * dt; if (off[r, c] >= 0f) { off[r, c] = 0f; vel[r, c] = 0f; } else moving = true; }
			if (!moving) { FindClears(); }
		}
	}
	private static bool Drill(int x) { return x == 1 || x == 2 || x == 5 || x == 6; }
	private void MarkRow(int r) { for (int k = 0; k < N; k++) mark[r, k] = true; }
	private void MarkCol(int c) { for (int k = 0; k < N; k++) mark[k, c] = true; }
	private void MarkBox(int r0, int c0, int rad) { for (int r = r0 - rad; r <= r0 + rad; r++) for (int c = c0 - rad; c <= c0 + rad; c++) if (r >= 0 && r < N && c >= 0 && c < N) mark[r, c] = true; }
	private void MarkDiag(int r0, int c0) { for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (Math.Abs(r - r0) == Math.Abs(c - c0)) mark[r, c] = true; }
	private void UseSpecials()
	{
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) mark[r, c] = false;
		int sa = spec[swR1, swC1], sb = spec[swR2, swC2]; mark[swR1, swC1] = true; mark[swR2, swC2] = true;
		if (sa == 3 || sb == 3)
		{
			bool aR = sa == 3; int rr = aR ? swR1 : swR2, rc = aR ? swC1 : swC2, orr = aR ? swR2 : swR1, oc = aR ? swC2 : swC1; int so = aR ? sb : sa;
			if (so == 3) { for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) mark[r, c] = true; msg = "무지개 대폭발!"; }
			else if (so == 0) { int v = grid[orr, oc]; for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (grid[r, c] == v) mark[r, c] = true; msg = "무지개 폭탄!"; }
			else { int v = grid[orr, oc]; for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (grid[r, c] == v) { mark[r, c] = true; spec[r, c] = so == 4 || so == 5 || so == 6 ? so : 1 + Rnd.Next(2); } msg = "무지개 + 특수 변신!"; }
			msgT = 1.4f;
		}
		else if (sa != 0 && sb != 0)
		{
			if (sa == 4 && sb == 4) { MarkBox(swR2, swC2, 2); msg = "초대형 폭탄!"; }
			else if (sa == 4 || sb == 4) { MarkBox(swR2, swC2, 1); for (int k = -1; k <= 1; k++) { if (swR2 + k >= 0 && swR2 + k < N) MarkRow(swR2 + k); if (swC2 + k >= 0 && swC2 + k < N) MarkCol(swC2 + k); } msg = "폭탄 + 드릴 합체!"; }
			else { MarkRow(swR2); MarkCol(swC2); if (sa == 6 || sb == 6) MarkDiag(swR2, swC2); msg = "드릴 합체 폭발!"; }
			msgT = 1.4f;
		}
		ExpandSpecials(); chain = 1; state = 2; stateT = 0f; Sfx.Play("ok2"); Shake = Math.Max(Shake, 0.12f);
	}
	private void ExpandSpecials()
	{
		bool again = true; int guard = 0;
		while (again && guard++ < 10)
		{
			again = false;
			for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (mark[r, c] && spec[r, c] != 0 && spec[r, c] != 3 && spec[r, c] < 8)
			{
				int s = spec[r, c]; spec[r, c] = 9;   // 한 번만 터뜨리려고 표시
				bool[,] before = (bool[,])mark.Clone();
				if (s == 1 || s == 5) MarkRow(r); if (s == 2 || s == 5) MarkCol(c); if (s == 4) MarkBox(r, c, 1); if (s == 6) MarkDiag(r, c);
				for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) if (mark[i, j] && !before[i, j]) again = true;
			}
		}
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (spec[r, c] == 9) spec[r, c] = 8;
	}
	private void FindClears()
	{
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) mark[r, c] = false;
		int[,] mk = new int[N, N];   // 가로 런 길이 · 세로 런 길이 기록
		bool any = false; int sr = -1, sc = -1, sl = 0; bool sh = true;
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++)
		{
			int v = grid[r, c]; if (v < 0 || v == 5) continue;
			int k = c; while (k + 1 < N && grid[r, k + 1] == v) k++; int len = k - c + 1;
			if (len >= 3 && (c == 0 || grid[r, c - 1] != v)) { for (int i = c; i <= k; i++) mark[r, i] = true; any = true; if (len >= 4 && len > sl) { sl = len; sh = true; sr = r; sc = (lastR == r && lastC >= c && lastC <= k) ? lastC : (c + k) / 2; } }
			k = r; while (k + 1 < N && grid[k + 1, c] == v) k++; len = k - r + 1;
			if (len >= 3 && (r == 0 || grid[r - 1, c] != v)) { for (int i = r; i <= k; i++) mark[i, c] = true; any = true; if (len >= 4 && len > sl) { sl = len; sh = false; sr = (lastC == c && lastR >= r && lastR <= k) ? lastR : (r + k) / 2; sc = c; } }
		}
		if (!any)
		{
			chain = 0; lastR = -1; state = 0; idleT = 0f;
			if (moves <= 0 || Done()) { EndOfTurn(); return; }
			if (!HasMove()) { Reshuffle(); }
			return;
		}
		chain++; if (chain > comboMax) comboMax = chain;
		// 특수 마룡이 만들기 (터진 뒤 그 자리에 남겨요)
		pendR = -1;
		if (sl >= 4 && sr >= 0) { pendR = sr; pendC = sc; pendSpec = sl >= 5 ? 3 : (sh ? 1 : 2); pendType = grid[sr, sc]; }
		if (pendSpec != 3 || pendR < 0)
		{
			int br = -1, bc = -1, bs = 0, bt = 0;
			for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (mark[r, c] && grid[r, c] >= 0)
			{
				int v = grid[r, c]; int l = Ext(r, c, 0, -1), rt = Ext(r, c, 0, 1), u = Ext(r, c, -1, 0), d = Ext(r, c, 1, 0); int h = 1 + l + rt, vv = 1 + u + d;
				if (h >= 3 && vv >= 3) { int sc2 = h + vv + ((r == lastR && c == lastC) ? 5 : 0); if (sc2 > bs) { bs = sc2; br = r; bc = c; bt = ((l == 0 || rt == 0) && (u == 0 || d == 0)) ? 4 : 5; } }
			}
			if (br >= 0) { pendR = br; pendC = bc; pendSpec = bt; pendType = grid[br, bc]; }
		}
		ExpandSpecials(); state = 2; stateT = 0f; Sfx.Play(chain > 1 ? "ok2" : "ok");
		if (chain > 1) { msg = chain + "연쇄!"; msgT = 1f; }
	}
	private int Ext(int r, int c, int dr, int dc) { int v = grid[r, c], n = 0; r += dr; c += dc; while (r >= 0 && r < N && c >= 0 && c < N && grid[r, c] == v) { n++; r += dr; c += dc; } return n; }
	private int pendR = -1, pendC, pendSpec, pendType;
	private void Fill2() { int[,] sv = new int[N, N]; for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) sv[r, c] = grid[r, c] == 5 ? spec[r, c] : 0; Fill(); for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (sv[r, c] >= 20) { grid[r, c] = 5; spec[r, c] = sv[r, c]; } }
	private void RemoveMarked()
	{
		{ bool[,] m0 = (bool[,])mark.Clone(); for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) { if (spec[r, c] >= 24 && mark[r, c]) { if (spec[r, c] == 24) { spec[r, c] = 25; mark[r, c] = false; Fx.Burst(GX + c * CELL + CELL / 2f, GY + r * CELL + CELL / 2f, Color.FromArgb(220, 224, 236), 6); Sfx.Play("thud2"); } continue; } if (spec[r, c] >= 20 && spec[r, c] < 24 && !m0[r, c]) { bool hit = (r > 0 && m0[r - 1, c]) || (r < N - 1 && m0[r + 1, c]) || (c > 0 && m0[r, c - 1]) || (c < N - 1 && m0[r, c + 1]); if (hit) { if (spec[r, c] == 21) spec[r, c] = 20; else if (spec[r, c] == 22) { spec[r, c] = 23; Fx.Burst(GX + c * CELL + CELL / 2f, GY + r * CELL + CELL / 2f, Color.FromArgb(170, 225, 255), 5); } else mark[r, c] = true; } } } }
		int n = 0; for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (mark[r, c]) { n++; if (obj == 1 && grid[r, c] == objType && spec[r, c] != 3) objGot++; jelly[r, c] = false; if (lockc[r, c] > 0) lockc[r, c]--; grid[r, c] = -1; spec[r, c] = 0; mark[r, c] = false; Fx.Burst(GX + c * CELL + CELL / 2f, GY + r * CELL + CELL / 2f, Col[(r + c) % Col.Length], 3); }
		Score += n * 10 * chain + (n >= 5 ? 30 : 0); shk = 0.06f * chain; Shake = Math.Max(Shake, 0.04f * chain);
		if (pendR >= 0) { grid[pendR, pendC] = pendType; spec[pendR, pendC] = pendSpec; msg = pendSpec == 3 ? "무지개 폭탄 탄생!" : pendSpec == 4 ? "폭탄 탄생!" : pendSpec == 5 ? "십자 드릴 탄생!" : "드릴 탄생!"; msgT = 1.1f; pendR = -1; Sfx.Play("fanfare"); }
		Drop(); state = 3; stateT = 0f;
		if (chain >= 3) { for (int tries = 0; tries < 20; tries++) { int r = Rnd.Next(N), c = Rnd.Next(N); if (grid[r, c] >= 0 && spec[r, c] == 0) { spec[r, c] = Rnd.Next(2) == 0 ? 4 : 6; msg = "연쇄 보너스 특수 블록!"; msgT = 1.2f; break; } } }
	}
	private void Drop()
	{
		for (int c = 0; c < N; c++)
		{
			int w = N - 1; for (int r = N - 1; r >= 0; r--) if (grid[r, c] >= 0) { if (w != r) { grid[w, c] = grid[r, c]; spec[w, c] = spec[r, c]; grid[r, c] = -1; spec[r, c] = 0; off[w, c] = -(w - r); vel[w, c] = 0f; } w--; }
			int k = w + 1; for (int r = w; r >= 0; r--) { grid[r, c] = Rnd.Next(types); spec[r, c] = 0; off[r, c] = -k; vel[r, c] = 0f; }
		}
	}
	private void Reshuffle()
	{
		msg = "더 이을 게 없어서 다시 섞었어요"; msgT = 1.6f; do { Fill2(); } while (!HasMove()); Sfx.Play("tick");
	}
	private void EndOfTurn()
	{
		if (Done())
		{
			Score += moves * 30; inv[Rnd.Next(3)]++; stage++; msg = "스테이지 클리어! 남은 횟수 보너스"; msgT = 2.2f; Sfx.Play("fanfare"); Fx.Burst(200f, 300f, Color.FromArgb(255, 220, 90), 22); NewStage(); return;
		}
		Over = true;
	}
	// ── 그리기 ──
	private static readonly Bitmap[] baseC = new Bitmap[5]; private static readonly System.Drawing.Imaging.ColorMatrix fm = new System.Drawing.Imaging.ColorMatrix(); private static readonly System.Drawing.Imaging.ImageAttributes fadeA = MkFade();
	private static System.Drawing.Imaging.ImageAttributes MkFade() { fm.Matrix33 = 1f; var ia = new System.Drawing.Imaging.ImageAttributes(); ia.SetColorMatrix(fm); return ia; }
	private static readonly FadeHolder fade = new FadeHolder();
	private sealed class FadeHolder { public float Matrix33 { set { fm.Matrix33 = value; fadeA.SetColorMatrix(fm); } } public static implicit operator System.Drawing.Imaging.ImageAttributes(FadeHolder h) { return fadeA; } }
	private static readonly float[] HueDeg = { 43f, 163f, -41f, -140f, 93f };   // 기본 마룡이(청록)를 칸 색에 맞춰 돌려요
	private static System.Drawing.Imaging.ImageAttributes HueAttr(float deg)
	{
		double an = deg * Math.PI / 180.0; float c = (float)Math.Cos(an), sn = (float)Math.Sin(an);
		float[,] m = { { 0.213f + 0.787f * c - 0.213f * sn, 0.715f - 0.715f * c - 0.715f * sn, 0.072f - 0.072f * c + 0.928f * sn }, { 0.213f - 0.213f * c + 0.143f * sn, 0.715f + 0.285f * c + 0.140f * sn, 0.072f - 0.072f * c - 0.283f * sn }, { 0.213f - 0.213f * c - 0.787f * sn, 0.715f - 0.715f * c + 0.715f * sn, 0.072f + 0.928f * c + 0.072f * sn } };
		var cm = new System.Drawing.Imaging.ColorMatrix(); for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) cm.Matrix00 = cm.Matrix00; float[][] f = new float[5][]; for (int i = 0; i < 5; i++) { f[i] = new float[5]; f[i][i] = 1f; } for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) f[i][j] = m[j, i]; var ia = new System.Drawing.Imaging.ImageAttributes(); ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(f)); return ia;
	}
	private Bitmap Base(int v)
	{
		if (baseC[v] != null) return baseC[v];
		int sz = (int)(CELL - 4f) * 2; Bitmap bm = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(bm))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; float s = sz - 2f; RectangleF r = new RectangleF(1, 1, s, s); Color c = Col[v];
			using (GraphicsPath gp = Gfx.Round(r, 20))
			{
				using (LinearGradientBrush b = new LinearGradientBrush(r, Theme.Mix(c, Color.White, 0.45), Theme.Mix(c, Color.Black, 0.12), 90f)) g.FillPath(b, gp);
				using (Pen p = new Pen(Theme.Mix(c, Color.Black, 0.35), 4f)) g.DrawPath(p, gp);
			}
			if (Spr.Get(Spr1[v]) != null) { Bitmap pb0 = Spr.Get(Spr1[v]); float pw = s * 0.82f, ph = s * 0.82f * 1.05f; using (var ia = HueAttr(HueDeg[v])) g.DrawImage(pb0, new Rectangle((int)(sz / 2f - pw / 2f), (int)(sz / 2f + 2f - ph / 2f), (int)pw, (int)ph), 0, 0, pb0.Width, pb0.Height, GraphicsUnit.Pixel, ia); }
			float mx = 14f, my = 14f, ms = 9f; using (SolidBrush mb = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
				switch (v) { case 0: g.FillEllipse(mb, mx - ms, my - ms, ms * 2, ms * 2); break; case 1: g.FillRectangle(mb, mx - ms, my - ms, ms * 2, ms * 2); break; case 2: g.FillPolygon(mb, new[] { new PointF(mx, my - ms), new PointF(mx + ms, my + ms), new PointF(mx - ms, my + ms) }); break; case 3: g.FillPolygon(mb, new[] { new PointF(mx, my - ms), new PointF(mx + ms, my), new PointF(mx, my + ms), new PointF(mx - ms, my) }); break; default: g.FillPolygon(mb, new[] { new PointF(mx - ms, my - ms), new PointF(mx + ms, my - ms), new PointF(mx, my + ms) }); break; }
		}
		return baseC[v] = bm;
	}
	private void Tile(Graphics g, float x, float y, int v, int sp, float a, float scale, double t)
	{
		float s = CELL - 4f; float cx = x + CELL / 2f, cy = y + CELL / 2f; s *= scale; RectangleF r = new RectangleF(cx - s / 2f, cy - s / 2f, s, s);
		if (sp == 3)
		{
			using (GraphicsPath gp = Gfx.Round(r, 12)) { using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb((int)(255 * a), 255, 240, 200), Color.FromArgb((int)(255 * a), 190, 140, 255), 45f)) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb((int)(240 * a), 255, 255, 255), 2f)) g.DrawPath(p, gp); }
			for (int i = 0; i < 5; i++) { double ang = t * 3 + i * 1.2566; float rx = cx + (float)Math.Cos(ang) * s * 0.26f, ry = cy + (float)Math.Sin(ang) * s * 0.26f; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(230 * a), Col[i]))) g.FillEllipse(b, rx - 5, ry - 5, 10, 10); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(240 * a), 255, 255, 255))) g.FillEllipse(b, cx - 5, cy - 5, 10, 10); return;
		}
		if (v == 5 && sp >= 22 && sp < 24) { if (Spr.Get("ms_blk_ice") != null) { Bitmap ib = Spr.Get("ms_blk_ice"); if (a >= 0.99f) g.DrawImage(ib, r); else { fade.Matrix33 = a; g.DrawImage(ib, new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height), 0, 0, ib.Width, ib.Height, GraphicsUnit.Pixel, fade); } } else using (GraphicsPath gp = Gfx.Round(r, 8)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(220 * a), 150, 210, 255))) g.FillPath(b, gp); if (sp == 23) using (Pen p = new Pen(Color.FromArgb((int)(230 * a), 255, 255, 255), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, cx - 10, cy - 12, cx - 2, cy - 1); g.DrawLine(p, cx - 2, cy - 1, cx - 8, cy + 9); g.DrawLine(p, cx - 2, cy - 1, cx + 9, cy + 3); g.DrawLine(p, cx + 9, cy + 3, cx + 12, cy + 11); } return; }
		if (v == 5 && sp >= 24) { using (GraphicsPath gp = Gfx.Round(r, 7)) { using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb((int)(255 * a), 214, 220, 232), Color.FromArgb((int)(255 * a), 104, 112, 130), 90f)) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb((int)(255 * a), 52, 58, 74), 3f)) g.DrawPath(p, gp); } using (Pen p = new Pen(Color.FromArgb((int)(150 * a), 60, 66, 84), 2f)) { g.DrawLine(p, r.X + 6, r.Y + 6, r.Right - 6, r.Bottom - 6); g.DrawLine(p, r.Right - 6, r.Y + 6, r.X + 6, r.Bottom - 6); } using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * a), 70, 76, 94))) { g.FillEllipse(b, r.X + 3, r.Y + 3, 6, 6); g.FillEllipse(b, r.Right - 9, r.Y + 3, 6, 6); g.FillEllipse(b, r.X + 3, r.Bottom - 9, 6, 6); g.FillEllipse(b, r.Right - 9, r.Bottom - 9, 6, 6); } if (sp == 25) using (Pen p = new Pen(Color.FromArgb((int)(240 * a), 30, 34, 46), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, cx - 9, cy - 11, cx + 1, cy); g.DrawLine(p, cx + 1, cy, cx - 5, cy + 11); } return; }
		if (v == 5) { using (GraphicsPath gp = Gfx.Round(r, 8)) { using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb((int)(255 * a), 170, 172, 182), Color.FromArgb((int)(255 * a), 92, 96, 110), 90f)) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb((int)(240 * a), 60, 64, 78), 2.4f)) g.DrawPath(p, gp); } using (Pen p = new Pen(Color.FromArgb((int)(200 * a), 50, 54, 68), 1.8f)) { g.DrawLine(p, cx - 8, cy - 10, cx + 2, cy); g.DrawLine(p, cx + 2, cy, cx - 4, cy + 10); if (sp == 20) g.DrawLine(p, cx + 2, cy, cx + 11, cy + 4); } if (sp == 21) using (Pen p = new Pen(Color.FromArgb((int)(220 * a), 235, 235, 245), 2f)) g.DrawRectangle(p, r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8); return; }
		if (sp == 1 || sp == 2 || sp == 4 || sp == 5 || sp == 6)
		{   // 특수 블록: 마룡이 없이 색 바탕 + 큰 표시만 (일반 블록과 한눈에 구분)
			Color c0 = Col[v];
			using (GraphicsPath gp = Gfx.Round(r, 12))
			{
				using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb((int)(255 * a), Theme.Mix(c0, Color.White, 0.25)), Color.FromArgb((int)(255 * a), Theme.Mix(c0, Color.Black, 0.35)), 90f)) g.FillPath(b, gp);
				using (Pen p = new Pen(Color.FromArgb((int)(255 * a), 255, 255, 255), 2.5f)) g.DrawPath(p, gp);
			}
		}
		else
		{
			Bitmap bb = Base(v);
			if (a >= 0.99f) g.DrawImage(bb, r);
			else { fade.Matrix33 = a; g.DrawImage(bb, new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height), 0, 0, bb.Width, bb.Height, GraphicsUnit.Pixel, fade); }
		}
		if (sp == 4)
		{
			float pu = 0.6f + 0.4f * (float)Math.Sin(t * 7); using (Pen p = new Pen(Color.FromArgb((int)(220 * a * pu), 255, 90, 70), 3f)) using (GraphicsPath gp = Gfx.Round(r, 10)) g.DrawPath(p, gp);
			float bx = cx, by = cy; if (Spr.Get("ms_am_bomb") != null) { Spr.Draw(g, "ms_am_bomb", cx, cy, s * 0.66f, s * 0.74f, (float)Math.Sin(t * 6) * 8f); return; } else using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * a), 30, 30, 40))) g.FillEllipse(b, bx - 7, by - 5, 14, 14); using (Pen p = new Pen(Color.FromArgb((int)(255 * a), 255, 180, 60), 2f)) g.DrawLine(p, bx + 2, by - 4, bx + 6, by - 9); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * a * pu), 255, 230, 90))) g.FillEllipse(b, bx + 4, by - 12, 5, 5);
		}
		if (sp == 5 || sp == 6)
		{
			float pulse = 0.65f + 0.35f * (float)Math.Sin(t * 8);
			using (Pen p = new Pen(Color.FromArgb((int)(235 * a * pulse), sp == 5 ? Color.FromArgb(255, 255, 255) : Color.FromArgb(255, 240, 120)), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				if (sp == 5) { g.DrawLine(p, r.X + 5, cy, r.Right - 5, cy); g.DrawLine(p, cx, r.Y + 5, cx, r.Bottom - 5); }
				else { g.DrawLine(p, r.X + 6, r.Y + 6, r.Right - 6, r.Bottom - 6); g.DrawLine(p, r.Right - 6, r.Y + 6, r.X + 6, r.Bottom - 6); }
			}
		}
		if (sp == 1 || sp == 2)
		{
			float pulse = 0.65f + 0.35f * (float)Math.Sin(t * 8);
			using (Pen p = new Pen(Color.FromArgb((int)(230 * a * pulse), 255, 255, 255), 4.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				if (sp == 1) { g.DrawLine(p, r.X + 6, cy - 7, r.Right - 6, cy - 7); g.DrawLine(p, r.X + 6, cy + 7, r.Right - 6, cy + 7); }
				else { g.DrawLine(p, cx - 7, r.Y + 6, cx - 7, r.Bottom - 6); g.DrawLine(p, cx + 7, r.Y + 6, cx + 7, r.Bottom - 6); }
			}
		}
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(50, 70, 130), Color.FromArgb(130, 110, 180));
		// 위쪽 안내판: 점수 · 목표 · 남은 횟수
		using (GraphicsPath gp = Gfx.Round(new RectangleF(16, 42, W - 32, 66), 14)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 12, 18, 40))) g.FillPath(b, gp);
		if (obj == 1) { Spr.Draw(g, Spr1[objType], 40f, 57f, 24f, 25f, 0f); Txt(g, objN + "마리 모으기 (" + Math.Min(objGot, objN) + ")", 11f, 2, Color.FromArgb(255, 236, 130), 56, 46, 170, 22, StringAlignment.Near); }
		else Txt(g, obj == 2 ? "젤리 모두 없애기 (남은 " + JellyLeft() + "칸)" : "목표 " + goal + "점 (+" + Math.Max(0, Score - stageBase) + ")", 11f, 2, Color.FromArgb(255, 236, 130), 28, 46, 190, 22, StringAlignment.Near);
		Txt(g, "남은 횟수 " + moves, 13f, 2, moves <= 3 ? Color.FromArgb(255, 150, 130) : Color.White, W - 188, 46, 160, 22, StringAlignment.Far);
		float fr = Prog(); using (GraphicsPath gp = Gfx.Round(new RectangleF(28, 72, W - 56, 12), 7)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillPath(b, gp);
		if (fr > 0.02f) using (GraphicsPath gp = Gfx.Round(new RectangleF(28, 72, Math.Max(14f, (W - 56) * fr), 12), 7)) using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 230, 150))) g.FillPath(b, gp);
		Txt(g, "점수 " + Score, 10f, 1, Color.FromArgb(230, 255, 255, 255), 28, 86, 200, 18, StringAlignment.Near);
		Txt(g, "스테이지 " + stage, 10f, 1, Color.FromArgb(230, 255, 255, 255), W - 228, 86, 200, 18, StringAlignment.Far);
		GraphicsState st0 = g.Save(); if (shk > 0f) g.TranslateTransform((float)Math.Sin(t * 70) * shk * 14f, 0f);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(GX - 6, GY - 6, N * CELL + 12, N * CELL + 12), 14)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 12, 18, 40))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) g.DrawPath(p, gp); }
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if ((r + c) % 2 == 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) g.FillRectangle(b, GX + c * CELL, GY + r * CELL, CELL, CELL);
		GraphicsState st1 = g.Save(); g.SetClip(new RectangleF(GX, GY, N * CELL, N * CELL));
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++)
		{
			if (grid[r, c] < 0) continue; float x = GX + c * CELL, y = GY + (r + off[r, c]) * CELL;
			if (state == 1) { float k = Math.Min(1f, stateT / 0.14f); if (swapBack) k = 1f - k; if (r == swR1 && c == swC1) { x += (swC2 - swC1) * CELL * k; y += (swR2 - swR1) * CELL * k; } else if (r == swR2 && c == swC2) { x += (swC1 - swC2) * CELL * k; y += (swR1 - swR2) * CELL * k; } }
			float a = 1f, sca = 1f; if (mark[r, c]) { float k = Math.Min(1f, stateT / 0.26f); a = 1f - 0.6f * k; sca = 1f + 0.18f * (float)Math.Sin(k * 3.14159f) - 0.3f * k; }
			Tile(g, x, y, grid[r, c], spec[r, c] == 8 ? 0 : spec[r, c], a, sca, t);
			if (mark[r, c]) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(110 * (0.5f + 0.5f * Math.Sin(t * 40))), 255, 255, 255))) g.FillEllipse(b, x + 4, y + 4, CELL - 8, CELL - 8);
		}
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (jelly[r, c]) { RectangleF jr = new RectangleF(GX + c * CELL + 1, GY + r * CELL + 1, CELL - 2, CELL - 2); using (GraphicsPath gp = Gfx.Round(jr, 9)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 120, 230, 255))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(200, 150, 240, 255), 2f)) g.DrawPath(p, gp); } }
		for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) if (lockc[r, c] > 0) { float lx = GX + c * CELL, ly = GY + r * CELL; using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 20, 24, 40))) g.FillRectangle(b, lx + 2, ly + 2, CELL - 4, CELL - 4); using (Pen p = new Pen(Color.FromArgb(235, 170, 175, 190), 2.5f)) { g.DrawRectangle(p, lx + 3, ly + 3, CELL - 6, CELL - 6); if (lockc[r, c] > 1) g.DrawRectangle(p, lx + 7, ly + 7, CELL - 14, CELL - 14); } float cx2 = lx + CELL / 2f, cy2 = ly + CELL / 2f; using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 90, 95, 110))) g.FillRectangle(b, cx2 - 7, cy2 - 2, 14, 10); using (Pen p = new Pen(Color.FromArgb(235, 90, 95, 110), 2.4f)) g.DrawArc(p, cx2 - 4.5f, cy2 - 9f, 9f, 11f, 180, 180); }
		if (selR >= 0 && state == 0) using (Pen p = new Pen(Color.FromArgb(255, 255, 236, 130), 3f)) using (GraphicsPath gp = Gfx.Round(new RectangleF(GX + selC * CELL + 1, GY + selR * CELL + 1, CELL - 2, CELL - 2), 10)) g.DrawPath(p, gp);
		if (hintR >= 0 && state == 0 && ((int)(t * 3) & 1) == 0) using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 2.5f) { DashStyle = DashStyle.Dash }) { g.DrawRectangle(p, GX + hintC * CELL + 3, GY + hintR * CELL + 3, CELL - 6, CELL - 6); g.DrawRectangle(p, GX + hintC2 * CELL + 3, GY + hintR2 * CELL + 3, CELL - 6, CELL - 6); }
		g.Restore(st1); g.Restore(st0);
		for (int k = 0; k < 3; k++) { RectangleF ir = new RectangleF(24 + k * 124, 486, 116, 40); bool has = inv[k] > 0, on = itemMode == k; using (GraphicsPath gp = Gfx.Round(ir, 12)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(on ? 220 : has ? 150 : 90, on ? 90 : 12, on ? 70 : 18, on ? 40 : 40))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(has || on ? 230 : 80, on ? Color.FromArgb(255, 236, 130) : Color.White), on ? 2.6f : 1.4f)) g.DrawPath(p, gp); } Txt(g, IName[k] + " ×" + inv[k], 11f, 2, has ? Color.White : Color.FromArgb(130, 255, 255, 255), ir.X, ir.Y + 3, ir.Width, 20, StringAlignment.Center); Txt(g, k == 0 ? "한 칸 없애기" : k == 1 ? "전부 섞기" : "폭탄 만들기", 8f, 1, Color.FromArgb(has ? 210 : 110, 255, 255, 255), ir.X, ir.Y + 22, ir.Width, 14, StringAlignment.Center); }
		if (chain > 1 && state >= 2) Txt(g, chain + "연쇄!", 22f, 2, Color.FromArgb(255, 236, 110), 0, GY - 36, W, 32, StringAlignment.Center);
		if (msgT > 0f) Txt(g, msg, 14f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2f)), 255, 236, 130), 0, GY + N * CELL * 0.45f, W, 28, StringAlignment.Center);
		if (Score == 0 && state == 0 && selR < 0) Hint(g, "마룡이를 누르고 옆 칸으로 끌어 바꿔요", GY + N * CELL + 80.0, 12f, false, 1f);
	}
}
