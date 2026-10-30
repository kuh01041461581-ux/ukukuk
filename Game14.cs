	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Windows.Forms;
	
	// ── 마룡이 랜덤 타워 디펜스: 랜덤 소환 · 같은 타워끼리 합쳐 등급업 · 코인으로 강화 ────────────
	internal sealed class RandomTdGame : MiniGame
	{
		public override bool NoLimit { get { return true; } }
		private class Tw { public int Cell, Type, Grade; public float Cd, Flash, Ang; }
		private class En { public float D, Hp, MaxHp, Sp, Slow, Stun, Hit, X, Y, Vx; public int Kind, BossNo, Var, Pat; public float Shield, MaxSh, Bt; public bool Boss; }
		private class Pj { public float X, Y, Vx, Vy, Dmg, Life, Sz = 1f, Rad, Slw; public int Type, Lv; public En T; public List<En> Hits = new List<En>(); }
		private class Fl { public float X, Y, X2, Y2, T, Max, R; public int K, C; }
		private readonly List<Tw> tws = new List<Tw>(); private readonly List<En> ens = new List<En>(); private readonly List<Pj> pjs = new List<Pj>(); private readonly List<Fl> fls = new List<Fl>(); private readonly List<En> spawnQ = new List<En>();
		private const int NC = 16; private const float CS = 54f;
		private static readonly float[] CX = { 113f, 287f }, CY = { 238f, 394f };
		private static readonly PointF[] Pts = { new PointF(26, 160), new PointF(200, 160), new PointF(200, 316), new PointF(374, 316), new PointF(374, 472), new PointF(200, 472), new PointF(200, 316), new PointF(26, 316) };   // 8자 모양 길: 가운데에서 한 번 만나요
		private static readonly float PathLen = CalcLen();
		private static float CalcLen() { float L = 0; for (int i = 0; i < Pts.Length; i++) { PointF a = Pts[i], b = Pts[(i + 1) % Pts.Length]; L += Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y); } return L; }
		private static readonly string[] IName = { "이동 깃발", "교환 스위치", "리롤 주사위" }, IDesc = { "타워를 빈 칸으로 옮겨요", "타워 두 개의 자리를 바꿔요", "타워 종류를 다시 뽑아요" };
		private static readonly Color[] ICol = { Color.FromArgb(90, 190, 255), Color.FromArgb(120, 230, 140), Color.FromArgb(255, 170, 90) };
		private static readonly string[] MName = { "", "질주", "튼튼", "황금 습격", "단비", "안개" }, MDesc = { "", "적 이동 속도 +35%", "적 체력 +40%, 코인 +1", "황금 적 등장 · 코인 3배", "타워 공격 속도 +25%", "타워 사거리 -15%, 적도 느려짐" };
		private static readonly Color[] MCol = { Color.White, Color.FromArgb(255, 120, 100), Color.FromArgb(180, 150, 255), Color.FromArgb(255, 220, 70), Color.FromArgb(110, 200, 255), Color.FromArgb(190, 200, 210) };
		private static readonly string[] TName = { "마룡 불꽃", "서류 비행기", "커피 타임", "번개", "도장 쾅" };
		private static readonly string[] TDesc = { "한 마리를 정확히 맞혀요", "일직선으로 관통해요", "범위 피해 + 이동 둔화", "3마리를 이어서 감전", "강한 한 방 + 잠깐 기절" };
		private static readonly string[] TPet = { "ms_pet_kick0", "ms_pet_fly0", "ms_pet_sip0", "ms_pet_wave0", "ms_pet_stand0" }, TWep = { "ms_wfire0", "ms_wpla0", "ms_wcof0", "ms_wlig0", "ms_wsta0" };
		private static readonly Color[] TCol = { Color.FromArgb(255, 130, 60), Color.FromArgb(240, 240, 250), Color.FromArgb(170, 110, 70), Color.FromArgb(255, 230, 90), Color.FromArgb(230, 70, 80) };
		private static readonly Color[] GCol = { Color.FromArgb(150, 155, 165), Color.FromArgb(90, 200, 110), Color.FromArgb(70, 140, 255), Color.FromArgb(180, 90, 240), Color.FromArgb(255, 200, 40) };
		private static readonly string[] GName = { "일반", "고급", "희귀", "영웅", "전설" };
		private static readonly float[] GMul = { 1f, 2.8f, 8f, 24f, 80f }, GRange = { 84f, 90f, 96f, 102f, 112f }, GRate = { 1f, 1.05f, 1.1f, 1.18f, 1.3f };
		private readonly int[] tlv = new int[5]; private int coins, wave, lives, summons, sel = -1, spawnLeft, bossNo, killsN; private float spawnT, waveT, msgT, shk, elapsed; private bool waveOn; private string msg = ""; private int selType = -1, mod, nextMod, itemMode = -1, itemSel = -1; private readonly int[] inv = new int[3]; private bool fast;
		public override string Costume { get { return "g_tower"; } }
		public override string Name { get { return "마룡이 랜덤타워디펜스"; } }
		public override string Key { get { return "rtd"; } }
		public override string Desc { get { return "랜덤 소환 · 합쳐서 더 강하게!"; } }
		public override string Help { get { return "빈 칸(4구역 × 4칸 = 16칸)을 누르면 코인으로 타워가 랜덤 소환돼요\n타워 위치는 바꿀 수 없어요! 아래 아이템으로만 이동·교환·리롤할 수 있어요 (웨이브 보상으로 획득)\n같은 종류·같은 등급 타워를 차례로 누르면 합쳐져서 한 등급 위의 랜덤 타워가 돼요\n아래 종류별 버튼으로 강화하면 이펙트와 능력이 점점 화려해져요\n웨이브마다 변수가 생겨요 (질주·튼튼·황금 습격·단비·안개) · 힐러·분열 적도 조심!\n적은 왼쪽 위에서 출발해 모든 길을 돌아 다시 왼쪽 위로 돌아와요 · 40마리가 넘으면 패배 · 20웨이브를 지키면 난이도가 올라가며 계속돼요 · 보스는 분열·돌진·방패·치유 패턴이 있어요\n2배속 버튼 또는 F 키 · Space 로 바로 시작"; } }
		public override string RightText { get { return (Rd > 0 ? "Lv" + (Rd + 1) + " " : "") + "웨이브 " + Math.Max(1, Ww); } }
		public override void Icon(Graphics g, float cx, float cy)
		{
			for (int i = 0; i < 3; i++) using (GraphicsPath gp = Gfx.Round(new RectangleF(cx - 20 + i * 14, cy - 8 + (i == 1 ? -8 : 4), 14, 14), 4)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 80, 110))) g.FillPath(b, gp); using (Pen p = new Pen(GCol[i + 1], 2f)) g.DrawPath(p, gp); }
		}
		public override void Reset()
		{
			tws.Clear(); ens.Clear(); pjs.Clear(); fls.Clear(); Array.Clear(tlv, 0, 5); coins = 50; wave = 0; lives = 0; summons = 0; sel = -1; spawnLeft = 0; bossNo = 0; killsN = 0; spawnT = 0; waveT = 4f; waveOn = false; Score = 0; Over = false; msg = "첫 웨이브 준비! 빈 칸을 눌러 소환하세요"; msgT = 3f; elapsed = 0f; inv[0] = 1; inv[1] = 1; inv[2] = 0; itemMode = -1; itemSel = -1; mod = 0; nextMod = 0; fast = false; spawnQ.Clear();
		}
		private int SummonCost { get { return 10 + summons / 4; } }
		private int UpCost(int t) { return 25 + tlv[t] * 30; }
		private static PointF Cell(int c) { int z = c / 4, k = c % 4; return new PointF(CX[z % 2] + (k % 2 == 0 ? -31f : 31f), CY[z / 2] + (k / 2 == 0 ? -31f : 31f)); }
		private int CellAt(float x, float y) { for (int c = 0; c < NC; c++) { PointF p = Cell(c); if (Math.Abs(x - p.X) <= CS / 2f && Math.Abs(y - p.Y) <= CS / 2f) return c; } return -1; }
		private Tw TwAt(int c) { foreach (Tw t in tws) if (t.Cell == c) return t; return null; }
		private static PointF PathPos(float d, out float vx)
		{
			d = ((d % PathLen) + PathLen) % PathLen; vx = 0f;
			for (int i = 0; i < Pts.Length; i++)
			{
				PointF a = Pts[i], b = Pts[(i + 1) % Pts.Length]; float l = Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
				if (d <= l || i == Pts.Length - 1) { float u = l > 0 ? Math.Min(1f, d / l) : 0f; if (b.X != a.X) vx = b.X > a.X ? 1f : -1f; return new PointF(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u); }
				d -= l;
			}
			return Pts[0];
		}
		private float Dmg(Tw t) { return 10f * GMul[t.Grade] * (1f + 0.3f * tlv[t.Type]); }
		private void Say(string s) { msg = s; msgT = 1.6f; }
		private void Cancel() { itemMode = -1; itemSel = -1; }
		private void UseItem(int k) { inv[k]--; Cancel(); sel = -1; Sfx.Play("fanfare"); }
		public override void Click(float x, float y)
		{
			if (Over) return;
			if (x >= 296f && y >= 86f && y < 110f) { fast = !fast; Sfx.Play("tick"); Say(fast ? "2배속!" : "1배속"); return; }
			if (y >= 534f) { Cancel(); int i = (int)(x / 80f); if (i >= 0 && i < 5) { int c = UpCost(i); if (coins >= c) { coins -= c; tlv[i]++; Sfx.Play("ok2"); Say(TName[i] + " 강화! Lv." + (tlv[i] + 1)); Fx.Burst(200f, 540f, TCol[i], 10); } else { Sfx.Play("bad2"); Say("코인이 부족해요"); } } return; }
			if (y >= 496f && y < 534f) { int k = (int)((x - 4f) / 132f); if (k >= 0 && k < 3 && x >= 4f) { if (itemMode == k) { Cancel(); } else if (inv[k] > 0) { Cancel(); itemMode = k; sel = -1; Sfx.Play("tick"); Say(k == 0 ? "옮길 타워를 고르세요" : k == 1 ? "첫 번째 타워를 고르세요" : "바꿀 타워를 고르세요"); } else { Sfx.Play("bad2"); Say("아이템이 없어요 · 웨이브 보상으로 얻어요"); } } return; }
			if (itemMode >= 0)
			{
				int ic = CellAt(x, y); if (ic < 0) { Cancel(); return; }
				Tw it = TwAt(ic); PointF ip = Cell(ic);
				if (itemMode == 0)
				{
					if (itemSel < 0) { if (it == null) { Sfx.Play("bad2"); Say("타워를 먼저 고르세요"); return; } itemSel = ic; Say("이제 빈 칸을 고르세요"); Sfx.Play("tick"); return; }
					if (it != null) { itemSel = ic; Sfx.Play("tick"); return; }
					Tw s0 = TwAt(itemSel); if (s0 == null) { Cancel(); return; }
					PointF from = Cell(s0.Cell); s0.Cell = ic; s0.Flash = 0.6f; Fx.Burst(from.X, from.Y, ICol[0], 8); Fx.Burst(ip.X, ip.Y, ICol[0], 12); UseItem(0); Say("이동 완료!"); return;
				}
				if (itemMode == 1)
				{
					if (it == null) { Sfx.Play("bad2"); Say("타워를 고르세요"); return; }
					if (itemSel < 0) { itemSel = ic; Say("바꿀 두 번째 타워를 고르세요"); Sfx.Play("tick"); return; }
					if (itemSel == ic) { itemSel = -1; return; }
					Tw s1 = TwAt(itemSel); if (s1 == null) { Cancel(); return; }
					int tmp = s1.Cell; s1.Cell = it.Cell; it.Cell = tmp; s1.Flash = it.Flash = 0.6f; Fx.Burst(ip.X, ip.Y, ICol[1], 12); PointF q = Cell(s1.Cell); Fx.Burst(q.X, q.Y, ICol[1], 12); UseItem(1); Say("자리 교환!"); return;
				}
				if (it == null) { Sfx.Play("bad2"); Say("타워를 고르세요"); return; }
				int old = it.Type; int nt = (old + 1 + Rnd.Next(4)) % 5; it.Type = nt; it.Flash = 0.8f; Fx.Burst(ip.X, ip.Y, TCol[nt], 16); UseItem(2); Say("리롤! " + TName[nt]); return;
			}
			if (sel >= 0 && y >= 106f && y < 142f && x >= 300f) { Tw s = TwAt(sel); if (s != null) { int back = (int)(SummonCost * 0.5f * (float)Math.Pow(2.2, s.Grade)) + 3; coins += back; tws.Remove(s); Say("판매 +" + back); Sfx.Play("coin"); } sel = -1; return; }
			if (!waveOn && y >= 110f && y < 140f && x < 100f && wave > 0) { waveT = 0f; return; }
			int ce = CellAt(x, y); if (ce < 0) { sel = -1; return; }
			Tw tw = TwAt(ce);
			if (tw == null)
			{
				sel = -1;
				if (coins < SummonCost) { Sfx.Play("bad2"); Say("코인이 부족해요 (필요 " + SummonCost + ")"); return; }
				coins -= SummonCost; summons++; tws.Add(new Tw { Cell = ce, Type = Rnd.Next(5), Grade = 0, Flash = 0.4f }); Sfx.Play("ok"); PointF p = Cell(ce); Fx.Burst(p.X, p.Y, Color.FromArgb(255, 230, 140), 8);
				return;
			}
			if (sel >= 0 && sel != ce)
			{
				Tw s = TwAt(sel);
				if (s != null && s.Type == tw.Type && s.Grade == tw.Grade && s.Grade < 4)
				{
					tws.Remove(s); tw.Grade++; tw.Type = Rnd.Next(5); tw.Flash = 0.8f; sel = ce; Sfx.Play("fanfare"); PointF p = Cell(ce); Fx.Burst(p.X, p.Y, GCol[tw.Grade], 18); Shake = 0.25f; Say("합체! " + GName[tw.Grade] + " " + TName[tw.Type]); return;
				}
			}
			sel = ce; Sfx.Play("tick");
		}
		public override void Press(Keys k) { if (k == Keys.Space && !waveOn && wave > 0) waveT = 0f; if (k == Keys.F) { fast = !fast; Say(fast ? "2배속!" : "1배속"); } }
		private void StartWave()
		{
			wave++; waveOn = true; spawnLeft = 14 + Ww * 2 + Rd * 4; spawnT = 0.3f; mod = nextMod; nextMod = 0;
			Say((wave % 5 == 0 ? Ww + "웨이브 · 보스 등장!" : Ww + "웨이브 시작!") + (mod > 0 ? " · " + MName[mod] : "")); Sfx.Play("whoosh");
		}
		private int Ww { get { return (wave - 1) % 20 + 1; } } private int Rd { get { return (wave - 1) / 20; } }
		private static readonly string[] PatName = { "분열의 보스", "돌진의 보스", "방패의 보스", "치유의 보스" };
		private float BaseHp { get { return 26f * (float)Math.Pow(1.34, Ww - 1) * (1f + 0.9f * Rd); } }
		private void SpawnEn()
		{
			bool boss = wave % 5 == 0 && spawnLeft == 1; int kind = Ww < 3 ? 0 : Rnd.Next(Ww < 6 ? 2 : 3); float hp = BaseHp, sp = 54f; int vr = 0;
			if (kind == 1) { hp *= 0.6f; sp = 86f; } else if (kind == 2) { hp *= 2.2f; sp = 38f; }
			if (!boss) { if (mod == 3 && Rnd.Next(100) < 35) { vr = 1; hp *= 0.7f; sp *= 1.2f; } else if (Ww >= 7 && Rnd.Next(12) == 0) { vr = 2; hp *= 1.2f; } else if (Ww >= 10 && Rnd.Next(10) == 0) { vr = 3; hp *= 1.3f; } }
			if (Rd > 0) sp *= 1f + 0.06f * Rd;
			int pat = 0; float shield = 0f; if (boss) { hp = BaseHp * 12f; sp = 34f; kind = 3; pat = bossNo % 4; if (pat == 2) shield = hp * 0.5f; Say(PatName[pat] + " 등장!"); }
			if (mod == 2) hp *= 1.4f; if (mod == 1) sp *= 1.35f; if (mod == 5) sp *= 0.85f;
			ens.Add(new En { D = 0f, Hp = hp, MaxHp = hp, Sp = sp, Kind = kind, Boss = boss, BossNo = bossNo, Var = vr, Pat = pat, Shield = shield, MaxSh = shield, Bt = 3f }); if (boss) bossNo++;
		}
		private void Kill(En e)
		{
			int c = e.Boss ? 20 + wave : 1 + wave / 6; if (e.Var == 1) c *= 3; if (mod == 2) c++; coins += c; if (e.Boss && e.Pat == 0) { for (int k = 0; k < 4; k++) spawnQ.Add(new En { D = Math.Max(0f, e.D - 30f + k * 20f), Hp = e.MaxHp * 0.06f, MaxHp = e.MaxHp * 0.06f, Sp = 60f, Kind = 1, X = e.X, Y = e.Y, Vx = e.Vx }); }
			if (e.Var == 3) { for (int k = 0; k < 2; k++) spawnQ.Add(new En { D = Math.Max(0f, e.D - 14f + k * 28f), Hp = e.MaxHp * 0.3f, MaxHp = e.MaxHp * 0.3f, Sp = e.Sp * 1.15f, Kind = 1, X = e.X, Y = e.Y, Vx = e.Vx }); } killsN++; Score += e.Boss ? 80 : 2; Fx.Burst(e.X, e.Y, e.Boss ? Color.FromArgb(255, 220, 90) : Color.FromArgb(255, 240, 160), e.Boss ? 20 : 4); Sfx.Play(e.Boss ? "glass_big" : "coin"); if (e.Boss) Shake = 0.5f;
		}
		private void Hurt(En e, float d) { if (e.Hp <= 0f) return; if (e.Shield > 0f) { e.Shield -= d; e.Hit = 0.08f; if (e.Shield < 0f) { e.Hp += e.Shield; e.Shield = 0f; } if (e.Hp > 0f) return; } else e.Hp -= d; e.Hit = 0.08f; if (e.Hp <= 0f) Kill(e); }
		public override void Update(float dt, GameInput inp, double t)
		{
			if (dt > 0.05f) dt = 0.05f; Step(dt); if (fast && !Over) Step(dt);
		}
		private void Step(float dt)
		{
			if (msgT > 0f) msgT -= dt; elapsed += dt;
			for (int i = fls.Count - 1; i >= 0; i--) { fls[i].T -= dt; if (fls[i].T <= 0f) fls.RemoveAt(i); }
			if (!waveOn) { waveT -= dt; if (waveT <= 0f) StartWave(); }
			else
			{
				spawnT -= dt; if (spawnLeft > 0 && spawnT <= 0f) { SpawnEn(); spawnLeft--; spawnT = wave % 5 == 0 && spawnLeft == 1 ? 1.2f : 0.55f; }
				if (spawnLeft <= 0) { waveOn = false; waveT = 6f; coins += 8 + wave; Score += wave * 20; { string ex = ""; if (wave % 20 == 0) { Score += 1000 * (Rd + 1); coins += 60; inv[0]++; inv[1]++; inv[2]++; ex = " · 난이도 " + (Rd + 2) + " 시작! 아이템 +3"; Sfx.Play("fanfare"); Shake = 0.4f; } if (wave % 5 == 0 || Rnd.Next(100) < 40) { int k = Rnd.Next(3); inv[k]++; ex = " · " + IName[k] + " 획득!"; Sfx.Play("ok2"); } nextMod = Rnd.Next(7) < 5 ? 1 + Rnd.Next(5) : 0; Say(Ww + "웨이브 방어! +" + (8 + wave) + ex); } }
			}
			foreach (En e in ens)
			{
				if (e.Var == 2) { foreach (En o in ens) { if (o == e || o.Hp <= 0f) continue; float hx = o.X - e.X, hy = o.Y - e.Y; if (hx * hx + hy * hy < 60f * 60f) o.Hp = Math.Min(o.MaxHp, o.Hp + o.MaxHp * 0.05f * dt); } }
				if (e.Boss) { if (e.Pat == 3) { e.Stun = 0f; foreach (En o in ens) { if (o == e || o.Hp <= 0f) continue; float hx = o.X - e.X, hy = o.Y - e.Y; if (hx * hx + hy * hy < 90f * 90f) o.Hp = Math.Min(o.MaxHp, o.Hp + o.MaxHp * 0.08f * dt); } } if (e.Pat == 1) { e.Bt -= dt; if (e.Bt < -1.5f) e.Bt = 3.5f; } }
				e.D += e.Sp * (e.Boss && e.Pat == 1 && e.Bt < 0f ? 2.6f : 1f) * (e.Slow > 0f ? 0.6f : 1f) * (e.Stun > 0f ? 0f : 1f) * dt; if (e.Slow > 0f) e.Slow -= dt; if (e.Stun > 0f) e.Stun -= dt; if (e.Hit > 0f) e.Hit -= dt;
				float vx; PointF p = PathPos(e.D, out vx); e.X = p.X; e.Y = p.Y; if (vx != 0f) e.Vx = vx;
			}
			// 타워
			foreach (Tw w in tws)
			{
				if (w.Flash > 0f) w.Flash -= dt; w.Cd -= dt; if (w.Cd > 0f) continue; PointF c = Cell(w.Cell); float rg = (GRange[w.Grade] + (w.Type == 1 ? 14f : 0f)) * (mod == 5 ? 0.85f : 1f);
				En tg = null; float best = -1f; foreach (En e in ens) { if (e.Hp <= 0f) continue; float dx = e.X - c.X, dy = e.Y - c.Y; if (dx * dx + dy * dy <= rg * rg && e.D > best) { best = e.D; tg = e; } }
				if (tg == null) continue; float d = Dmg(w); float rate = GRate[w.Grade] * (mod == 4 ? 1.25f : 1f); int L = tlv[w.Type]; w.Ang = (float)Math.Atan2(tg.Y - c.Y, tg.X - c.X); w.Flash = Math.Max(w.Flash, 0.12f);
				switch (w.Type)
				{
					case 0: w.Cd = 0.7f / rate; pjs.Add(new Pj { X = c.X, Y = c.Y, Dmg = d, T = tg, Type = 0, Life = 1.5f, Lv = L, Sz = 1f + 0.1f * L }); Sfx.Play("tick"); break;
					case 1: { w.Cd = 1.0f / rate; int n = 1 + (L >= 3 ? 1 : 0) + (L >= 6 ? 1 : 0); for (int k = 0; k < n; k++) { float a = w.Ang + (k - (n - 1) / 2f) * 0.22f; pjs.Add(new Pj { X = c.X, Y = c.Y, Vx = (float)Math.Cos(a) * 330f, Vy = (float)Math.Sin(a) * 330f, Dmg = d * 0.75f, Type = 1, Life = 0.55f + 0.02f * L, Lv = L, Sz = 1f + 0.07f * L }); } Sfx.Play("swing"); break; }
					case 2: w.Cd = 1.1f / rate; pjs.Add(new Pj { X = c.X, Y = c.Y, Dmg = d * 0.6f, T = tg, Type = 2, Life = 1.5f, Lv = L, Rad = 40f + L * 4f, Slw = 1.6f + 0.2f * L, Sz = 1f + 0.06f * L }); Sfx.Play("tick"); break;
					case 3:
						{
							w.Cd = 1.25f / rate; int ch = 3 + L / 2; float hop = 70f + L * 3f; float px = c.X, py = c.Y; List<En> used = new List<En>(); En cur = tg;
							for (int k = 0; k < ch && cur != null; k++) { used.Add(cur); fls.Add(new Fl { X = px, Y = py, X2 = cur.X, Y2 = cur.Y, T = 0.18f + 0.01f * L, Max = 0.18f + 0.01f * L, K = 3, R = 3f + L * 0.45f }); Hurt(cur, d * 0.8f); px = cur.X; py = cur.Y; En nx = null; float bd = hop * hop; foreach (En e in ens) { if (e.Hp <= 0f || used.Contains(e)) continue; float dx = e.X - px, dy = e.Y - py; if (dx * dx + dy * dy < bd) { bd = dx * dx + dy * dy; nx = e; } } cur = nx; }
							Sfx.Play("glass"); break;
						}
					default:
						{
							w.Cd = 1.8f / rate; Hurt(tg, d * 2.2f); tg.Stun = 0.35f + 0.04f * L; float sr = 26f + L * 4f; fls.Add(new Fl { X = tg.X, Y = tg.Y, T = 0.25f, Max = 0.25f, K = 4, R = sr });
							if (L >= 3) { float wr = 40f + L * 5f; fls.Add(new Fl { X = tg.X, Y = tg.Y, T = 0.35f, Max = 0.35f, K = 4, R = wr }); foreach (En e in ens) { if (e.Hp <= 0f || e == tg) continue; float dx = e.X - tg.X, dy = e.Y - tg.Y; if (dx * dx + dy * dy < wr * wr) { Hurt(e, d * 0.5f); e.Stun = Math.Max(e.Stun, 0.2f); } } }
							Sfx.Play("thud2"); Shake = Math.Max(Shake, 0.05f + 0.01f * L); break;
						}
				}
			}
			for (int i = pjs.Count - 1; i >= 0; i--)
			{
				Pj p = pjs[i]; p.Life -= dt; if (p.Life <= 0f) { pjs.RemoveAt(i); continue; }
				if (p.Type == 1)
				{
					p.X += p.Vx * dt; p.Y += p.Vy * dt; if (p.Lv >= 2 && fls.Count < 160 && Rnd.Next(3) == 0) fls.Add(new Fl { X = p.X, Y = p.Y, T = 0.25f, Max = 0.25f, K = 5, C = 1, R = 2f + p.Lv * 0.3f }); foreach (En e in ens) { if (e.Hp <= 0f || p.Hits.Contains(e)) continue; float dx = e.X - p.X, dy = e.Y - p.Y; if (dx * dx + dy * dy < 17f * 17f * p.Sz) { p.Hits.Add(e); Hurt(e, p.Dmg); } }
				}
				else
				{
					if (p.T == null || p.T.Hp <= 0f) { pjs.RemoveAt(i); continue; }
					float dx = p.T.X - p.X, dy = p.T.Y - p.Y, l = (float)Math.Sqrt(dx * dx + dy * dy); float st = 340f * dt; if (p.Lv >= 1 && fls.Count < 160 && Rnd.Next(2) == 0) fls.Add(new Fl { X = p.X, Y = p.Y, T = 0.3f, Max = 0.3f, K = 5, C = p.Type, R = 2.5f + p.Lv * 0.4f });
					if (l <= st + 6f)
					{
						if (p.Type == 0) { En tt = p.T; Hurt(tt, p.Dmg); Fx.Burst(tt.X, tt.Y, Color.FromArgb(255, 150, 70), 2 + p.Lv); if (p.Lv >= 2) { float sr = 30f + p.Lv * 4f; fls.Add(new Fl { X = tt.X, Y = tt.Y, T = 0.3f, Max = 0.3f, K = 2, C = 0, R = sr }); foreach (En e in ens) { if (e.Hp <= 0f || e == tt) continue; float ex = e.X - tt.X, ey = e.Y - tt.Y; if (ex * ex + ey * ey < sr * sr) Hurt(e, p.Dmg * 0.4f); } } }
						else { float cr = p.Rad; fls.Add(new Fl { X = p.T.X, Y = p.T.Y, T = 0.3f, Max = 0.3f, K = 2, C = 2, R = cr }); foreach (En e in ens) { if (e.Hp <= 0f) continue; float ex = e.X - p.T.X, ey = e.Y - p.T.Y; if (ex * ex + ey * ey < cr * cr) { Hurt(e, p.Dmg); e.Slow = p.Slw; } } }
						pjs.RemoveAt(i); continue;
					}
					p.X += dx / l * st; p.Y += dy / l * st;
				}
			}
			ens.RemoveAll(e => e.Hp <= 0f); if (spawnQ.Count > 0) { ens.AddRange(spawnQ); spawnQ.Clear(); }
			if (ens.Count >= 40) { Over = true; Sfx.Play("fall"); Shake = 0.6f; msg = "적이 너무 많아요…"; msgT = 3f; }
		}
		public override void Draw(Graphics g, double t)
		{
			// 배경은 화면 배율에 맞춰 한 번만 그려 두고, 크기를 지정해서 찍어요 (창 크기·배율이 바뀌어도 깨지지 않게)
			float bk = Math.Max(1f, ImgCache.Scale(g));
			if (backC == null || Math.Abs(backK - bk) > 0.01f || backBad)
			{
				if (backC != null) { backC.Dispose(); backC = null; } backBad = false;
				try { backC = new Bitmap((int)Math.Ceiling(W * bk), (int)Math.Ceiling(H * bk), System.Drawing.Imaging.PixelFormat.Format32bppPArgb); backK = bk; using (Graphics bg = Graphics.FromImage(backC)) { bg.SmoothingMode = SmoothingMode.AntiAlias; bg.ScaleTransform(bk, bk); DrawBack(bg); } }
				catch { backC = null; }
			}
			if (backC != null) { InterpolationMode bi = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(backC, new RectangleF(0, 0, W, H), new RectangleF(0, 0, backC.Width, backC.Height), GraphicsUnit.Pixel); g.InterpolationMode = bi; }
			else DrawBack(g);
			if (itemMode >= 0) { for (int c = 0; c < NC; c++) { Tw q = TwAt(c); bool ok = itemMode == 0 ? (itemSel < 0 ? q != null : q == null) : q != null; if (!ok) continue; PointF p = Cell(c); using (Pen pe = new Pen(Color.FromArgb((int)(150 + 100 * Math.Sin(t * 8)), ICol[itemMode]), 3f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pe, p.X - 28, p.Y - 28, 56, 56); } }
			Tw st = sel >= 0 ? TwAt(sel) : null;
			if (st != null) { PointF p = Cell(st.Cell); float rg = (GRange[st.Grade] + (st.Type == 1 ? 14f : 0f)) * (mod == 5 ? 0.85f : 1f); using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillEllipse(b, p.X - rg, p.Y - rg, rg * 2, rg * 2); using (Pen pe = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f)) g.DrawEllipse(pe, p.X - rg, p.Y - rg, rg * 2, rg * 2); }
			foreach (Tw w in tws) DrawTower(g, w, t, st);
			// 적
			foreach (En e in ens) DrawEnemy(g, e, t);
			foreach (Pj p in pjs)
			{
				if (p.Type == 0) { using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 150, 50))) g.FillEllipse(b, p.X - 7 * p.Sz, p.Y - 7 * p.Sz, 14 * p.Sz, 14 * p.Sz); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 90))) g.FillEllipse(b, p.X - 4 * p.Sz, p.Y - 4 * p.Sz, 8 * p.Sz, 8 * p.Sz); }
				else if (p.Type == 1) { float a = (float)(Math.Atan2(p.Vy, p.Vx) * 57.3); GraphicsState s0 = g.Save(); g.TranslateTransform(p.X, p.Y); g.RotateTransform(a); g.ScaleTransform(p.Sz, p.Sz); using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new[] { new PointF(10, 0), new PointF(-7, -6), new PointF(-3, 0), new PointF(-7, 6) }); using (Pen pe = new Pen(Color.FromArgb(160, 110, 130, 200), 1.2f)) g.DrawPolygon(pe, new[] { new PointF(10, 0), new PointF(-7, -6), new PointF(-3, 0), new PointF(-7, 6) }); g.Restore(s0); }
				else { using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 110, 70, 40))) g.FillEllipse(b, p.X - 6 * p.Sz, p.Y - 6 * p.Sz, 12 * p.Sz, 12 * p.Sz); using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, p.X - 2, p.Y - 4, 4, 4); }
			}
			foreach (Fl f in fls)
			{
				float u = f.T / f.Max;
				if (f.K == 3) { float lw = Math.Max(2f, f.R); float mx = (f.X + f.X2) / 2f + (float)(Rnd.NextDouble() - 0.5) * 12f, my = (f.Y + f.Y2) / 2f + (float)(Rnd.NextDouble() - 0.5) * 12f; using (Pen p = new Pen(Color.FromArgb((int)(90 * u), 255, 240, 120), lw * 2.2f)) { g.DrawLine(p, f.X, f.Y, mx, my); g.DrawLine(p, mx, my, f.X2, f.Y2); } using (Pen p = new Pen(Color.FromArgb((int)(255 * u), 255, 250, 190), lw)) { g.DrawLine(p, f.X, f.Y, mx, my); g.DrawLine(p, mx, my, f.X2, f.Y2); } }
				else if (f.K == 4) { float r = 10f + (1f - u) * Math.Max(26f, f.R); using (Pen p = new Pen(Color.FromArgb((int)(230 * u), 230, 70, 80), 4f)) g.DrawEllipse(p, f.X - r, f.Y - r, r * 2, r * 2); }
				else if (f.K == 5) { float r = f.R * (0.4f + u * 0.6f); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(190 * u), TCol[f.C % 5]))) g.FillEllipse(b, f.X - r, f.Y - r, r * 2, r * 2); }
				else { float r0 = f.R > 0 ? f.R : 40f; float r = r0 * (1f - u * 0.5f); Color cc = TCol[f.C % 5]; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(110 * u), cc))) g.FillEllipse(b, f.X - r, f.Y - r, r * 2, r * 2); }
			}
			DrawHud(g, t, st);
		}
		private static Bitmap backC; private static float backK; private static bool backBad;
		private void DrawBack(Graphics g)
		{
			g.Clear(Color.FromArgb(90, 150, 90));
			Bitmap gr = Spr.Get("bg_t_grass"), dirt = Spr.Get("bg_t_dirt");
			if (gr != null) using (TextureBrush tb = new TextureBrush(gr)) { tb.ScaleTransform(0.6f, 0.6f); g.FillRectangle(tb, 0, 0, W, H); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 10, 40, 20))) g.FillRectangle(b, 0, 0, W, H);
			// 길
			using (Pen p = new Pen(Color.FromArgb(110, 60, 40, 20), 46f) { LineJoin = LineJoin.Round }) g.DrawPolygon(p, Pts);
			using (Pen p = new Pen(Color.FromArgb(255, 176, 134, 88), 38f) { LineJoin = LineJoin.Round }) g.DrawPolygon(p, Pts);
			if (dirt != null) using (TextureBrush tb = new TextureBrush(dirt)) { tb.ScaleTransform(0.5f, 0.5f); using (Pen p = new Pen(tb, 34f) { LineJoin = LineJoin.Round }) g.DrawPolygon(p, Pts); }
			using (Pen p = new Pen(Color.FromArgb(55, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dash }) g.DrawPolygon(p, Pts);
			Txt(g, "출발·도착", 8.5f, 2, Color.FromArgb(230, 255, 255, 255), 2, 138, 70, 14, StringAlignment.Near);
			// 칸
			for (int c = 0; c < NC; c++)
			{
				PointF p = Cell(c); RectangleF r = new RectangleF(p.X - CS / 2f + 2, p.Y - CS / 2f + 2, CS - 4, CS - 4); using (GraphicsPath gp = Gfx.Round(r, 9)) { using (SolidBrush b = new SolidBrush(Color.FromArgb((((c / 4) + (c % 4)) & 1) == 0 ? 120 : 90, 40, 70, 50))) g.FillPath(b, gp); using (Pen pe = new Pen(Color.FromArgb(60, 255, 255, 255), 1f)) g.DrawPath(pe, gp); }
			}
			
		}
		private void DrawTower(Graphics g, Tw w, double t, Tw selT) { PointF p0 = Cell(w.Cell); GraphicsState s0 = g.Save(); g.TranslateTransform(p0.X, p0.Y); g.ScaleTransform(0.92f, 0.92f); g.TranslateTransform(-p0.X, -p0.Y); DrawTower0(g, w, t, selT); g.Restore(s0); }
		private void DrawTower0(Graphics g, Tw w, double t, Tw selT)
		{
			PointF p = Cell(w.Cell); float sc = 1f + (w.Flash > 0.3f ? (w.Flash - 0.3f) * 0.6f : 0f); RectangleF r = new RectangleF(p.X - 27f * sc, p.Y - 27f * sc, 54f * sc, 54f * sc);
			Color gc = GCol[w.Grade]; int tl = tlv[w.Type]; if (tl > 0) { int nr = Math.Min(3, (tl + 1) / 2); for (int i = 0; i < nr; i++) { float pu = (float)((t * 1.6 + i * 0.33) % 1.0); float rr = 28f + pu * 12f + i * 2f; using (Pen pe = new Pen(Color.FromArgb((int)(120 * (1f - pu)), TCol[w.Type]), 2f)) g.DrawEllipse(pe, p.X - rr, p.Y - rr, rr * 2, rr * 2); } int ns = Math.Min(5, tl / 2); for (int i = 0; i < ns; i++) { double an = t * 2.2 + i * 6.283 / ns; float sx = p.X + (float)Math.Cos(an) * 33f, sy = p.Y + (float)Math.Sin(an) * 33f; using (SolidBrush b = new SolidBrush(Color.FromArgb(220, TCol[w.Type]))) g.FillEllipse(b, sx - 2.5f, sy - 2.5f, 5f, 5f); } }
			if (w.Grade == 4) { float pu = 0.5f + 0.5f * (float)Math.Sin(t * 5); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(60 + 60 * pu), 255, 210, 60))) g.FillEllipse(b, p.X - 32, p.Y - 32, 64, 64); }
			using (GraphicsPath gp = Gfx.Round(r, 10))
			{
				using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(250, 250, 255), Color.FromArgb(200, 210, 230), 90f)) g.FillPath(b, gp);
				using (Pen pe = new Pen(gc, 3.4f)) g.DrawPath(pe, gp);
			}
			using (SolidBrush b = new SolidBrush(Color.FromArgb(70, TCol[w.Type]))) g.FillEllipse(b, p.X - 19, p.Y - 15, 38, 34);
			if (!Spr.Draw(g, TPet[w.Type], p.X, p.Y + 3f, 36f, 40f, 0f)) { }
			if (Spr.Get(TWep[w.Type]) != null) Spr.Draw(g, TWep[w.Type], p.X + 15f, p.Y - 15f, 20f, 20f, 0f);
			for (int i = 0; i <= w.Grade; i++) using (SolidBrush b = new SolidBrush(gc)) g.FillEllipse(b, p.X - 22f + i * 7f, p.Y + 17f, 5.5f, 5.5f);
			if (selT == w) using (Pen pe = new Pen(Color.FromArgb(230, 255, 255, 120), 2.4f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pe, p.X - 31, p.Y - 31, 62, 62);
			else if (selT != null && selT.Type == w.Type && selT.Grade == w.Grade && selT.Grade < 4) using (Pen pe = new Pen(Color.FromArgb((int)(150 + 100 * Math.Sin(t * 8)), 120, 255, 150), 2.6f)) g.DrawRectangle(pe, p.X - 28, p.Y - 28, 56, 56);
		}
		private void DrawEnemy(Graphics g, En e, double t)
		{
			string[] pre = { "ms_ecu", "ms_emg", "ms_edoc" }; string name; float h;
			if (e.Boss) { string[] bs = { "ms_s2x", "ms_s3x", "ms_s4x", "ms_s5x" }; name = bs[e.BossNo % bs.Length]; h = 66f; } else { name = pre[Math.Min(2, e.Kind)]; h = e.Kind == 2 ? 44f : 36f; }
			int f = (int)((t * 7.0 + e.D * 0.03) % 4.0); if (f < 0) f = 0; Bitmap b = Spr.Get(name + f);
			using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sh, e.X - h * 0.3f, e.Y + h * 0.28f, h * 0.6f, h * 0.2f);
			if (b != null) { float w = h * b.Width / b.Height; if (e.Hit > 0f) { Spr.Draw(g, name + f, e.X, e.Y - h * 0.05f, e.Vx < 0 ? -w : w, h, 0f); using (SolidBrush wb = new SolidBrush(Color.FromArgb(110, 255, 255, 255))) g.FillEllipse(wb, e.X - w * 0.3f, e.Y - h * 0.4f, w * 0.6f, h * 0.7f); } else Spr.Draw(g, name + f, e.X, e.Y - h * 0.05f, e.Vx < 0 ? -w : w, h, 0f); }
			else using (SolidBrush bb = new SolidBrush(Color.FromArgb(200, 90, 80))) g.FillEllipse(bb, e.X - 12, e.Y - 14, 24, 28);
			float bw = e.Boss ? 44f : 28f; float fr = Math.Max(0f, e.Hp / e.MaxHp); float by = e.Y - h * 0.58f;
			using (SolidBrush bb = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillRectangle(bb, e.X - bw / 2f, by, bw, 4f); using (SolidBrush bb = new SolidBrush(e.Boss ? Color.FromArgb(220, 90, 220) : Color.FromArgb(120, 230, 120))) g.FillRectangle(bb, e.X - bw / 2f, by, bw * fr, 4f);
			if (e.Var > 0) { Color vc = e.Var == 1 ? Color.FromArgb(255, 255, 215, 60) : e.Var == 2 ? Color.FromArgb(255, 110, 230, 130) : Color.FromArgb(255, 255, 150, 60); using (Pen pe = new Pen(Color.FromArgb(200, vc), 2.2f)) g.DrawEllipse(pe, e.X - 16, e.Y - h * 0.5f, 32, h * 0.9f); if (e.Var == 2) { float hp2 = (float)((t * 1.2) % 1.0); using (Pen pe = new Pen(Color.FromArgb((int)(110 * (1f - hp2)), 110, 230, 130), 1.5f)) g.DrawEllipse(pe, e.X - 60 * hp2, e.Y - 60 * hp2, 120 * hp2, 120 * hp2); } Txt(g, e.Var == 1 ? "$" : e.Var == 2 ? "+" : "×2", 9f, 2, vc, e.X + 8, e.Y - h * 0.75f, 20, 14, StringAlignment.Near); }
			if (e.Boss) { Txt(g, PatName[e.Pat], 8f, 2, Color.FromArgb(255, 255, 220, 140), e.X - 40, by - 14, 80, 12, StringAlignment.Center); if (e.Shield > 0f) using (SolidBrush bb = new SolidBrush(Color.FromArgb(220, 110, 190, 255))) g.FillRectangle(bb, e.X - bw / 2f, by - 4f, bw * e.Shield / e.MaxSh, 3f); if (e.Pat == 1 && e.Bt < 0f) using (Pen pe = new Pen(Color.FromArgb(200, 255, 120, 80), 2f)) g.DrawEllipse(pe, e.X - 22, e.Y - 26, 44, 52); }
			if (e.Slow > 0f) using (Pen pe = new Pen(Color.FromArgb(180, 160, 110, 70), 2f)) g.DrawEllipse(pe, e.X - 12, e.Y + 6, 24, 8);
			if (e.Stun > 0f) Txt(g, "*", 14f, 2, Color.FromArgb(255, 255, 230, 90), e.X - 10, e.Y - h * 0.9f, 20, 18, StringAlignment.Center);
		}
		private void DrawHud(Graphics g, double t, Tw st)
		{
			using (GraphicsPath gp = Gfx.Round(new RectangleF(8, 40, W - 16, 46), 14)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 20, 30))) g.FillPath(b, gp);
			Spr.Draw(g, "ms_coins", 30f, 63f, 28f, 23f, 0f); Txt(g, coins.ToString(), 15f, 2, Color.FromArgb(255, 236, 130), 48, 42, 90, 24, StringAlignment.Near);
			Txt(g, "소환 " + SummonCost + "코인", 9.5f, 2, Color.FromArgb(230, 255, 255, 255), 48, 66, 100, 16, StringAlignment.Near);
			Txt(g, "적 " + ens.Count + "/40", 13f, 2, ens.Count >= 30 ? Color.FromArgb(255, 130, 120) : Color.White, 150, 44, 100, 20, StringAlignment.Center);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(100, 0, 0, 0))) g.FillRectangle(b, 160, 68, 80, 8); using (SolidBrush b = new SolidBrush(ens.Count >= 30 ? Color.FromArgb(255, 110, 100) : Color.FromArgb(120, 220, 130))) g.FillRectangle(b, 160, 68, 80 * Math.Min(1f, ens.Count / 40f), 8);
			Txt(g, (Rd > 0 ? "Lv" + (Rd + 1) + " " : "") + (waveOn ? "웨이브 " + Ww + "/20" : "다음 웨이브 " + Math.Ceiling(waveT) + "초"), 12f, 2, Color.White, 252, 44, 140, 20, StringAlignment.Center);
			Txt(g, "처치 " + killsN, 9.5f, 2, Color.FromArgb(220, 255, 255, 255), 252, 66, 140, 16, StringAlignment.Center);
			if (!waveOn && wave > 0) { using (GraphicsPath gp = Gfx.Round(new RectangleF(10, 112, 90, 26), 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 80, 160, 220))) g.FillPath(b, gp); Txt(g, "바로 시작", 10.5f, 2, Color.White, 10, 114, 90, 22, StringAlignment.Center); }
			if (st != null)
			{
				using (GraphicsPath gp = Gfx.Round(new RectangleF(108, 108, 188, 34), 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 10, 20, 30))) g.FillPath(b, gp); using (GraphicsPath gp = Gfx.Round(new RectangleF(108, 108, 188, 34), 10)) using (Pen pe = new Pen(GCol[st.Grade], 2.4f)) g.DrawPath(pe, gp);
				Txt(g, GName[st.Grade] + " " + TName[st.Type], 11f, 2, GCol[st.Grade].GetBrightness() < 0.5f ? Color.White : GCol[st.Grade], 112, 108, 180, 18, StringAlignment.Center);
				Txt(g, TDesc[st.Type] + " · 공격력 " + (int)Dmg(st), 8.5f, 1, Color.FromArgb(230, 255, 255, 255), 112, 124, 180, 16, StringAlignment.Center);
				using (GraphicsPath gp = Gfx.Round(new RectangleF(304, 108, 88, 32), 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(210, 200, 90, 80))) g.FillPath(b, gp); Txt(g, "판매", 11f, 2, Color.White, 304, 113, 88, 22, StringAlignment.Center);
			}
			else Txt(g, "빈 칸 = 랜덤 소환 · 같은 타워 두 개 = 합체", 9.5f, 2, Color.FromArgb(220, 255, 255, 255), 100, 116, 200, 20, StringAlignment.Center);
			{ RectangleF fr2 = new RectangleF(298, 88, 92, 20); using (GraphicsPath gp = Gfx.Round(fr2, 9)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(fast ? 230 : 150, fast ? 240 : 30, fast ? 150 : 50, fast ? 60 : 70))) g.FillPath(b, gp); using (Pen pe = new Pen(Color.FromArgb(200, 255, 255, 255), 1.2f)) g.DrawPath(pe, gp); } Txt(g, fast ? ">> 2배속 (F)" : "> 1배속 (F)", 9.5f, 2, Color.White, fr2.X, fr2.Y + 2, fr2.Width, 18, StringAlignment.Center); }
			{ int mm = waveOn ? mod : nextMod; if (mm > 0) { using (GraphicsPath gp = Gfx.Round(new RectangleF(8, 88, 284, 20), 9)) using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 10, 20, 30))) g.FillPath(b, gp); using (Pen pe = new Pen(Color.FromArgb(220, MCol[mm]), 1.6f)) g.DrawRectangle(pe, 14, 90, 3, 16); Txt(g, (waveOn ? "변수 " : "다음 변수 ") + MName[mm] + " · " + MDesc[mm], 9.5f, 2, MCol[mm], 22, 90, 268, 16, StringAlignment.Near); } }
			// 아이템: 한 번 쓰고 사라지는 것 → 금색 점선 알약 + 동그란 아이콘 + 개수 배지
			for (int k = 0; k < 3; k++)
			{
				RectangleF ir = new RectangleF(4 + k * 132, 498, 128, 32); bool has = inv[k] > 0, on = itemMode == k; int al = has || on ? 255 : 110;
				using (GraphicsPath gp = Gfx.Round(ir, 16))
				{
					using (SolidBrush b = new SolidBrush(Color.FromArgb(on ? 235 : has ? 200 : 110, on ? Color.FromArgb(120, 90, 20) : Color.FromArgb(46, 38, 22)))) g.FillPath(b, gp);
					using (Pen pe = new Pen(Color.FromArgb(al, on ? Color.FromArgb(255, 236, 130) : Color.FromArgb(230, 190, 80)), on ? 3f : 1.8f) { DashStyle = on ? DashStyle.Solid : DashStyle.Dash }) g.DrawPath(pe, gp);
				}
				float ix = ir.X + 17f, iy = ir.Y + 16f;
				using (SolidBrush b = new SolidBrush(Color.FromArgb(al, ICol[k]))) g.FillEllipse(b, ix - 12, iy - 12, 24, 24);
				using (Pen pw = new Pen(Color.FromArgb(al, 20, 24, 36), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
				{
					if (k == 0) { g.DrawLine(pw, ix - 4, iy + 6, ix - 4, iy - 7); using (SolidBrush b = new SolidBrush(Color.FromArgb(al, 20, 24, 36))) g.FillPolygon(b, new[] { new PointF(ix - 4, iy - 7), new PointF(ix + 6, iy - 3), new PointF(ix - 4, iy + 1) }); }
					else if (k == 1) { g.DrawLine(pw, ix - 6, iy - 3, ix + 6, iy - 3); g.DrawLine(pw, ix + 2, iy - 7, ix + 6, iy - 3); g.DrawLine(pw, ix + 2, iy + 1, ix + 6, iy - 3); g.DrawLine(pw, ix + 6, iy + 4, ix - 6, iy + 4); g.DrawLine(pw, ix - 2, iy + 0, ix - 6, iy + 4); g.DrawLine(pw, ix - 2, iy + 8, ix - 6, iy + 4); }
					else { g.DrawRectangle(pw, ix - 6, iy - 6, 12, 12); using (SolidBrush b = new SolidBrush(Color.FromArgb(al, 20, 24, 36))) { g.FillEllipse(b, ix - 4, iy - 4, 3, 3); g.FillEllipse(b, ix + 1, iy + 1, 3, 3); } }
				}
				Txt(g, IName[k], 9.5f, 2, has ? Color.White : Color.FromArgb(150, 255, 255, 255), ir.X + 34, ir.Y + 2, 70, 16, StringAlignment.Near);
				Txt(g, IDesc[k], 7f, 1, Color.FromArgb(has ? 215 : 110, 255, 255, 255), ir.X + 34, ir.Y + 17, 94, 12, StringAlignment.Near);
				RectangleF cb = new RectangleF(ir.Right - 26, ir.Y + 3, 22, 14); using (GraphicsPath gp = Gfx.Round(cb, 7)) using (SolidBrush b = new SolidBrush(has ? Color.FromArgb(240, 200, 60) : Color.FromArgb(90, 120, 120, 120))) g.FillPath(b, gp);
				Txt(g, "×" + inv[k], 9f, 2, has ? Color.FromArgb(40, 28, 0) : Color.FromArgb(200, 255, 255, 255), cb.X, cb.Y, cb.Width, cb.Height, StringAlignment.Center);
			}
			// 강화: 타워 종류별 영구 업그레이드 → 위쪽 색 띠 + "강화" 이름표 + 레벨 변화 + 코인 가격표
			for (int i = 0; i < 5; i++)
			{
				RectangleF r = new RectangleF(4 + i * 80, 536, 74, 58); bool can = coins >= UpCost(i);
				using (GraphicsPath gp = Gfx.Round(r, 8))
				{
					using (SolidBrush b = new SolidBrush(Color.FromArgb(can ? 215 : 150, 18, 28, 36))) g.FillPath(b, gp);
					GraphicsState gs = g.Save(); g.SetClip(gp, CombineMode.Intersect);
					using (SolidBrush b = new SolidBrush(Color.FromArgb(can ? 255 : 120, TCol[i]))) g.FillRectangle(b, r.X, r.Y, r.Width, 15);
					g.Restore(gs);
					using (Pen pe = new Pen(Color.FromArgb(can ? 255 : 90, TCol[i]), 2f)) g.DrawPath(pe, gp);
				}
				Txt(g, "▲ 강화 " + TName[i], 7.5f, 2, Color.FromArgb(can ? 255 : 190, 20, 20, 30), r.X, r.Y + 1, r.Width, 13, StringAlignment.Center);
				Spr.Draw(g, TWep[i], r.X + 17f, r.Y + 31f, 22f, 22f, 0f);
				Txt(g, "Lv." + (tlv[i] + 1) + "→" + (tlv[i] + 2), 8.5f, 2, Color.White, r.X + 28, r.Y + 17, 46, 16, StringAlignment.Center);
				RectangleF pr = new RectangleF(r.X + 30, r.Y + 36, 40, 16); using (GraphicsPath gp = Gfx.Round(pr, 8)) using (SolidBrush b = new SolidBrush(can ? Color.FromArgb(240, 200, 60) : Color.FromArgb(80, 110, 110, 110))) g.FillPath(b, gp);
				Txt(g, UpCost(i).ToString(), 9f, 2, can ? Color.FromArgb(40, 28, 0) : Color.FromArgb(200, 255, 255, 255), pr.X, pr.Y + 1, pr.Width, 14, StringAlignment.Center);
			}
			if (msgT > 0f) Txt(g, msg, 14f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2f)), 255, 236, 130), 0, 144, W, 22, StringAlignment.Center);
		}
	}
