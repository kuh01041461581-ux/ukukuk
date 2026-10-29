using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// ── 마룡이 솔리테어: 솔리테어(클론다이크) 판 + 발라트로식 포커 족보 점수 ──
//  8줄 카드의 맨 위 카드로 족보를 만들어 내요(최대 5장). 칩 × 배수로 목표 점수를 넘기면 다음 판, 판을 깰 때마다 부적을 하나 골라요.
//  카드를 끌어서 색이 다르고 숫자가 1 큰 카드 위에 놓으면(솔리테어 규칙) 공짜로 옮겨지고 밑에 숨은 카드가 열려요.
internal sealed class SoliGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	public override string Name { get { return "마룡이 솔리테어"; } }
	public override string Key { get { return "soli"; } }
	public override string Desc { get { return "솔리테어 + 포커 족보로 목표 점수 넘기기"; } }
	public override string Help { get { return "8줄 카드의 맨 위 카드를 눌러 골라요 (최대 5장) → [내기]로 포커 족보 점수!\n점수 = (족보 칩 + 카드 칩) × 배수 · 판마다 핸드 5번(보스 판 4번)·버리기 3번 안에 목표 점수를 넘겨요\n솔리테어 규칙: 카드를 끌어서 색이 다르고 숫자가 1 큰 카드 위에 놓으면 공짜로 옮겨지고 밑의 카드가 열려요\n카드는 마룡이 타로 마이너 카드예요: 검=♠ · 컵=♥(빨강) · 펜타클=♦(빨강) · 완드=♣ · J=기사 · Q=여왕 · K=왕\n판을 깨면 마룡이 부적(조커)을 골라요 · 부적은 최대 5개 · 3판마다 목표가 커져요"; } }
	public override string RightText { get { return "판 " + (round + 1); } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		CardFace(g, new RectangleF(cx - 20, cy - 16, 22, 30), 14, 1, false, 1f); CardFace(g, new RectangleF(cx - 2, cy - 12, 22, 30), 13, 0, false, 1f);
	}
	private class Cd { public int R, Su; public bool Up; }
	private readonly List<Cd>[] col = new List<Cd>[8]; private readonly List<Cd> stock = new List<Cd>(); private readonly List<int> sel = new List<int>(); private readonly List<int> jok = new List<int>();
	private int round, hands, discards, phase; private long roundScore, target; private float shownScore, popT, msgT; private string pop = "", msg = ""; private int[] offer = new int[3]; private float dragX, dragY; private int dragCol = -1; private float shakeCard;
	private static readonly string[] HName = { "하이 카드", "원 페어", "투 페어", "트리플", "스트레이트", "플러시", "풀 하우스", "포카드", "스트레이트 플러시" };
	private static readonly int[] HChip = { 5, 10, 20, 30, 30, 35, 40, 60, 100 }, HMult = { 1, 2, 2, 3, 4, 4, 4, 7, 8 };
	private static readonly string[] JName = { "마룡이 미소", "볼터치", "날개", "지팡이 뿔", "공무원증", "커피 한 잔", "별사탕", "결재 도장", "야근 수당", "1등 마룡", "짝수 좋아", "홀수 좋아" };
	private static readonly string[] JDesc = { "항상 +4 배수", "페어가 들어간 족보면 +8 배수", "플러시면 배수 ×2", "스트레이트면 +15 배수", "항상 +50 칩", "낸 하트 카드마다 +3 배수", "낸 그림 카드(J·Q·K)마다 +20 칩", "판의 마지막 핸드면 배수 ×3", "남은 버리기 1번마다 +20 칩", "낸 A 카드마다 +5 배수", "낸 짝수 카드마다 +4 배수", "낸 홀수 카드(A 포함)마다 +25 칩" };
	public override void Reset()
	{
		round = 0; jok.Clear(); Score = 0; Over = false; NewRound();
	}
	private void NewRound()
	{
		List<Cd> deck = new List<Cd>(); for (int s = 0; s < 4; s++) for (int r = 2; r <= 14; r++) deck.Add(new Cd { R = r, Su = s });
		for (int i = deck.Count - 1; i > 0; i--) { int j = Rnd.Next(i + 1); Cd t = deck[i]; deck[i] = deck[j]; deck[j] = t; }
		int k = 0; for (int c = 0; c < 8; c++) { col[c] = new List<Cd>(); for (int d = 0; d < 4; d++) { Cd cd = deck[k++]; cd.Up = d == 3; col[c].Add(cd); } }
		stock.Clear(); while (k < deck.Count) stock.Add(deck[k++]);
		int ante = round / 3, kind = round % 3; target = (long)(Math.Round(260 * Math.Pow(1.6, ante) * (kind == 0 ? 1.0 : (kind == 1 ? 1.4 : 1.8)) / 10.0) * 10);
		hands = kind == 2 ? 4 : 5; discards = 3; roundScore = 0; shownScore = 0; sel.Clear(); phase = 0;
		msg = kind == 2 ? "보스 판! 핸드가 4번뿐이에요" : (kind == 1 ? "큰 판" : "작은 판"); msgT = 2.2f;
	}
	private static bool Red(int su) { return su == 1 || su == 2; }
	private static int Chip(int r) { return r == 14 ? 11 : (r > 10 ? 10 : r); }
	// 족보 판정: 종류와 점수에 들어가는 카드
	private static int Eval(List<Cd> cs, out List<Cd> scoring)
	{
		scoring = new List<Cd>(); int n = cs.Count; if (n == 0) return -1;
		var groups = cs.GroupBy(c => c.R).OrderByDescending(gq => gq.Count()).ThenByDescending(gq => gq.Key).ToList();
		bool flush = n == 5 && cs.All(c => c.Su == cs[0].Su);
		List<int> rs = cs.Select(c => c.R).Distinct().OrderBy(r => r).ToList(); bool straight = false;
		if (n == 5 && rs.Count == 5) { if (rs[4] - rs[0] == 4) straight = true; else if (rs[4] == 14 && rs[0] == 2 && rs[3] == 5) straight = true; }
		if (straight && flush) { scoring.AddRange(cs); return 8; }
		if (groups[0].Count() >= 4) { scoring.AddRange(groups[0].Take(4)); return 7; }
		if (groups[0].Count() == 3 && groups.Count > 1 && groups[1].Count() >= 2) { scoring.AddRange(cs); return 6; }
		if (flush) { scoring.AddRange(cs); return 5; }
		if (straight) { scoring.AddRange(cs); return 4; }
		if (groups[0].Count() == 3) { scoring.AddRange(groups[0]); return 3; }
		if (groups[0].Count() == 2 && groups.Count > 1 && groups[1].Count() == 2) { scoring.AddRange(groups[0]); scoring.AddRange(groups[1]); return 2; }
		if (groups[0].Count() == 2) { scoring.AddRange(groups[0]); return 1; }
		scoring.Add(cs.OrderByDescending(c => c.R).First()); return 0;
	}
	private List<Cd> Picked() { List<Cd> l = new List<Cd>(); foreach (int c in sel) if (col[c].Count > 0) l.Add(col[c][col[c].Count - 1]); return l; }
	private void Calc(List<Cd> cs, out int ht, out long chips, out double mult)
	{
		List<Cd> sc; ht = Eval(cs, out sc); chips = 0; mult = 0; if (ht < 0) return;
		chips = HChip[ht]; mult = HMult[ht]; foreach (Cd c in sc) chips += Chip(c.R);
		double xm = 1.0; bool pairish = ht == 1 || ht == 2 || ht == 3 || ht == 6 || ht == 7;
		foreach (int j in jok)
			switch (j)
			{
				case 0: mult += 4; break;
				case 1: if (pairish) mult += 8; break;
				case 2: if (ht == 5 || ht == 8) xm *= 2; break;
				case 3: if (ht == 4 || ht == 8) mult += 15; break;
				case 4: chips += 50; break;
				case 5: mult += 3 * sc.Count(c => c.Su == 1); break;
				case 6: chips += 20 * sc.Count(c => c.R >= 11 && c.R <= 13); break;
				case 7: if (hands == 1) xm *= 3; break;
				case 8: chips += 20 * discards; break;
				case 9: mult += 5 * sc.Count(c => c.R == 14); break;
				case 10: mult += 4 * sc.Count(c => c.R <= 10 && c.R % 2 == 0); break;
				case 11: chips += 25 * sc.Count(c => c.R == 14 || (c.R <= 9 && c.R % 2 == 1)); break;
			}
		mult *= xm;
	}
	private void Reveal(int c)
	{
		if (col[c].Count == 0 && stock.Count > 0) { Cd d = stock[stock.Count - 1]; stock.RemoveAt(stock.Count - 1); d.Up = true; col[c].Add(d); }
		else if (col[c].Count > 0) col[c][col[c].Count - 1].Up = true;
	}
	private void Remove(List<int> cols) { foreach (int c in cols.OrderByDescending(x => x)) if (col[c].Count > 0) col[c].RemoveAt(col[c].Count - 1); foreach (int c in cols) Reveal(c); }
	private bool AnyCard { get { for (int c = 0; c < 8; c++) if (col[c].Count > 0) return true; return false; } }
	private void Play()
	{
		if (phase != 0 || sel.Count == 0 || hands <= 0) return;
		int ht; long ch; double mu; Calc(Picked(), out ht, out ch, out mu); long pts = (long)Math.Round(ch * mu);
		roundScore += pts; Score += (int)Math.Min(int.MaxValue / 4, pts); hands--; pop = HName[ht] + "  " + ch + " × " + (mu % 1 == 0 ? mu.ToString("0") : mu.ToString("0.#")) + " = " + pts; popT = 2.2f;
		Sfx.Play(ht >= 4 ? "fanfare" : "ok2"); if (ht >= 5) { Shake = 0.35f; Fx.Burst(200, 300, Color.FromArgb(255, 220, 120), 24); }
		Remove(new List<int>(sel)); sel.Clear();
		if (roundScore >= target) { phase = 1; Sfx.Play("fanfare"); MakeOffer(); msg = "목표 달성! 부적을 하나 골라요"; msgT = 3f; return; }
		if (hands <= 0 || !AnyCard) { phase = 2; msg = "목표 " + target + "점에 못 미쳤어요"; msgT = 9f; Sfx.Play("bad2"); Over = true; }
	}
	private void Discard()
	{
		if (phase != 0 || sel.Count == 0 || discards <= 0) return;
		discards--; Remove(new List<int>(sel)); sel.Clear(); Sfx.Play("swing");
	}
	private void MakeOffer()
	{
		List<int> pool = Enumerable.Range(0, JName.Length).Where(j => !jok.Contains(j)).ToList();
		for (int i = 0; i < 3; i++) { if (pool.Count == 0) { offer[i] = -1; continue; } int k = Rnd.Next(pool.Count); offer[i] = pool[k]; pool.RemoveAt(k); }
	}
	// 화면 배치
	private const float CW = 44f, CH = 62f, TopY = 166f;
	private float ColX(int c) { return 10f + c * (CW + 4f); }
	private float Step(int c, int i) { int n = col[c].Count; float down = 9f, up = 19f; float need = 0; for (int k = 0; k < n - 1; k++) need += col[c][k].Up ? up : down; float room = 420f - TopY - CH; float sq = need > room ? room / need : 1f; return (col[c][i].Up ? up : down) * sq; }
	private RectangleF CardR(int c, int i) { float y = TopY; for (int k = 0; k < i; k++) y += Step(c, k); bool lifted = i == col[c].Count - 1 && sel.Contains(c); return new RectangleF(ColX(c), y - (lifted ? 10f : 0f), CW, CH); }
	private RectangleF PlayBtn { get { return new RectangleF(20, 512, 172, 46); } }
	private RectangleF DiscBtn { get { return new RectangleF(208, 512, 172, 46); } }
	private RectangleF OfferR(int i) { return new RectangleF(22 + i * 122, 230, 112, 170); }
	private RectangleF SkipBtn { get { return new RectangleF(130, 420, 140, 40); } }
	private int TopAt(float x, float y)
	{
		for (int c = 0; c < 8; c++) { int n = col[c].Count; if (n == 0) continue; RectangleF r = CardR(c, n - 1); r.Inflate(2, 4); if (r.Contains(x, y)) return c; }
		return -1;
	}
	public override void Click(float x, float y)
	{
		dragX = x; dragY = y; dragCol = -1;
		if (phase == 1)
		{
			for (int i = 0; i < 3; i++) if (offer[i] >= 0 && OfferR(i).Contains(x, y)) { if (jok.Count < 5) jok.Add(offer[i]); else jok[jok.Count - 1] = offer[i]; Sfx.Play("ok"); Next(); return; }
			if (SkipBtn.Contains(x, y)) { Sfx.Play("ui_click"); Next(); }
			return;
		}
		if (phase != 0) return;
		if (PlayBtn.Contains(x, y)) { Play(); return; }
		if (DiscBtn.Contains(x, y)) { Discard(); return; }
		int c = TopAt(x, y); if (c < 0) return; dragCol = c;
		if (sel.Contains(c)) sel.Remove(c); else if (sel.Count < 5) sel.Add(c); else { shakeCard = 0.3f; Sfx.Play("bad"); return; }
		Sfx.Play("ui_click");
	}
	public override void Swipe(float dx, float dy)
	{
		// 카드를 끌어 다른 줄 위에 놓기 (솔리테어 규칙: 색이 다르고 숫자가 1 큰 카드 위)
		if (phase != 0 || dragCol < 0) return; int from = dragCol; dragCol = -1; int to = TopAt(dragX + dx, dragY + dy); sel.Remove(from);
		if (to < 0 || to == from || col[from].Count == 0) return;
		Cd a = col[from][col[from].Count - 1], b = col[to][col[to].Count - 1];
		if (b.R == a.R + 1 && Red(a.Su) != Red(b.Su)) { col[from].RemoveAt(col[from].Count - 1); col[to].Add(a); sel.Remove(to); Reveal(from); Sfx.Play("ok"); msg = "옮겼어요! 밑의 카드가 열렸어요"; msgT = 1.4f; }
		else { msg = "색이 다르고 숫자가 1 큰 카드 위에만 놓을 수 있어요"; msgT = 2f; Sfx.Play("bad"); }
	}
	private void Next() { round++; NewRound(); }
	public override void Press(Keys k)
	{
		if (k == Keys.Enter || k == Keys.Space) Play(); else if (k == Keys.D || k == Keys.Back) Discard();
		else if (k >= Keys.D1 && k <= Keys.D8 && phase == 0) Click(ColX(k - Keys.D1) + CW / 2f, CardR(k - Keys.D1, Math.Max(0, col[k - Keys.D1].Count - 1)).Y + 20f);
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		if (popT > 0f) popT -= dt; if (msgT > 0f) msgT -= dt; if (shakeCard > 0f) shakeCard -= dt;
		shownScore += (roundScore - shownScore) * Math.Min(1f, dt * 6f); if (Math.Abs(roundScore - shownScore) < 1f) shownScore = roundScore;
		if (inp.MouseMoved && dragCol < 0) { }
	}
	// ── 그리기 ──
	private static void Suit(Graphics g, int su, float cx, float cy, float s, Color c)
	{
		using (SolidBrush b = new SolidBrush(c))
		{
			if (su == 1) { g.FillEllipse(b, cx - s, cy - s * 0.9f, s * 1.1f, s * 1.1f); g.FillEllipse(b, cx - s * 0.1f, cy - s * 0.9f, s * 1.1f, s * 1.1f); g.FillPolygon(b, new PointF[] { new PointF(cx - s * 0.97f, cy - s * 0.15f), new PointF(cx + s * 0.97f, cy - s * 0.15f), new PointF(cx, cy + s) }); }
			else if (su == 2) g.FillPolygon(b, new PointF[] { new PointF(cx, cy - s), new PointF(cx + s * 0.75f, cy), new PointF(cx, cy + s), new PointF(cx - s * 0.75f, cy) });
			else if (su == 0) { g.FillEllipse(b, cx - s, cy - s * 0.25f, s * 1.1f, s * 1.1f); g.FillEllipse(b, cx - s * 0.1f, cy - s * 0.25f, s * 1.1f, s * 1.1f); g.FillPolygon(b, new PointF[] { new PointF(cx - s * 0.97f, cy + s * 0.2f), new PointF(cx + s * 0.97f, cy + s * 0.2f), new PointF(cx, cy - s) }); g.FillPolygon(b, new PointF[] { new PointF(cx, cy + s * 0.3f), new PointF(cx + s * 0.4f, cy + s * 1.05f), new PointF(cx - s * 0.4f, cy + s * 1.05f) }); }
			else { float r = s * 0.52f; g.FillEllipse(b, cx - r, cy - s, r * 2, r * 2); g.FillEllipse(b, cx - s, cy - s * 0.15f, r * 2, r * 2); g.FillEllipse(b, cx + s - r * 2, cy - s * 0.15f, r * 2, r * 2); g.FillPolygon(b, new PointF[] { new PointF(cx, cy), new PointF(cx + s * 0.4f, cy + s * 1.05f), new PointF(cx - s * 0.4f, cy + s * 1.05f) }); }
		}
	}
	private static string RankS(int r) { return r == 14 ? "A" : (r == 13 ? "K" : (r == 12 ? "Q" : (r == 11 ? "J" : r.ToString()))); }
	// ── 카드 그림: 마룡이 타로의 마이너 카드를 그대로 써요 (검=♠ · 컵=♥ · 펜타클=♦ · 완드=♣, J=기사 · Q=여왕 · K=왕, 뒷면=타로 뒷면) ──
	private static readonly string[] TRes = { "swords", "cups", "pents", "wands" }; private static readonly int[] TSuit = { 2, 1, 3, 0 };   // 포커 무늬 → 타로 무늬 순서(완드·컵·검·펜타클)
	private static readonly string[] SuitKo = { "검", "컵", "펜타클", "완드" };
	private static readonly Dictionary<int, Bitmap> faceC = new Dictionary<int, Bitmap>(); private static Bitmap backC; private static readonly Bitmap[] pipC = new Bitmap[4];
	private static Bitmap Res(string n)
	{
		try { using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("tarot." + n)) { if (st == null) return null; using (Bitmap t = new Bitmap(st)) return new Bitmap(t); } } catch { return null; }
	}
	private static Bitmap Pip(int su) { if (pipC[su] == null) pipC[su] = Res("p_" + TRes[su] + ".png"); return pipC[su]; }
	private const int CW0 = 120, CH0 = 172;
	private static Bitmap Rounded(Bitmap src)
	{
		Bitmap b = new Bitmap(CW0, CH0, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; using (GraphicsPath gp = Gfx.Round(new RectangleF(0, 0, CW0, CH0), 11)) { g.SetClip(gp); g.DrawImage(src, 0, 0, CW0, CH0); } }
		return b;
	}
	private static Bitmap FaceBmp(int rank, int su)
	{
		int key = rank * 4 + su; Bitmap b; if (faceC.TryGetValue(key, out b)) return b;
		int ts = TSuit[su];
		if (rank >= 11 && rank <= 13) { Bitmap src = Res("c" + (22 + ts * 4 + (rank - 10)).ToString("00") + ".jpg"); if (src != null) { b = Rounded(src); src.Dispose(); } }   // J 기사 · Q 여왕 · K 왕
		if (b == null)
		{
			// 숫자 카드: 타로 마이너와 같은 양피지 바탕 + 무늬 그림을 숫자만큼
			b = new Bitmap(CW0, CH0, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics g = Graphics.FromImage(b))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; RectangleF all = new RectangleF(0, 0, CW0, CH0);
				using (GraphicsPath gp = Gfx.Round(new RectangleF(1, 1, CW0 - 2, CH0 - 2), 11)) { using (LinearGradientBrush lb = new LinearGradientBrush(all, Color.FromArgb(246, 235, 208), Color.FromArgb(228, 209, 168), 70f)) g.FillPath(lb, gp); using (Pen p = new Pen(Color.FromArgb(214, 164, 70), 2.4f)) g.DrawPath(p, gp); }
				using (GraphicsPath gp = Gfx.Round(new RectangleF(6, 6, CW0 - 12, CH0 - 12), 7)) using (Pen p = new Pen(Color.FromArgb(170, 190, 140, 56), 1.2f)) g.DrawPath(p, gp);
				Bitmap sym = Pip(su); int n = rank == 14 ? 1 : rank; if (n > 10) n = 10;
				if (sym != null)
				{
					// 무늬 배치: 위아래로 고르게 (두 줄)
					float rx = 22, ry = 30, rw = CW0 - 44, rh = CH0 - 46; int cols = n <= 3 ? 1 : 2, rows = (n + cols - 1) / cols; float pbW = rw / cols * 0.92f, pbH = rh / rows * 0.92f; if (n == 1) { pbW = 70f; pbH = 110f; }
					float k = Math.Min(pbW / sym.Width, pbH / sym.Height), w = sym.Width * k, h = sym.Height * k;
					for (int i = 0; i < n; i++) { int c = cols == 1 ? 0 : i % 2, r = cols == 1 ? i : i / 2; float cx = rx + rw * (cols == 1 ? 0.5f : (c == 0 ? 0.27f : 0.73f)), cy = ry + rh * ((r + 0.5f) / rows); if (cols == 2 && n % 2 == 1 && i == n - 1) cx = rx + rw * 0.5f; g.DrawImage(sym, cx - w / 2, cy - h / 2, w, h); }
				}
			}
		}
		faceC[key] = b; return b;
	}
	private static void CardFace(Graphics g, RectangleF r, int rank, int su, bool hi, float a)
	{
		using (GraphicsPath sp = Gfx.Round(new RectangleF(r.X, r.Y + 2.5f, r.Width, r.Height), 6)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 20))) g.FillPath(sb, sp);
		Bitmap b = FaceBmp(rank, su); float s = ImgCache.Scale(g); Image im = ImgCache.Get(b, r.Width * s, r.Height * s);
		InterpolationMode m0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(im, r); g.InterpolationMode = m0;
		// 왼쪽 위: 숫자·무늬가 작게 보여도 읽히게 흰 딱지
		Color c = Red(su) ? Color.FromArgb(206, 44, 64) : Color.FromArgb(30, 36, 60); float fs = r.Width < 30 ? 7f : 10f, bw = r.Width < 30 ? r.Width * 0.62f : 22f, bh = r.Width < 30 ? 12f : 26f;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(r.X + 1.5f, r.Y + 1.5f, bw, bh), 5)) using (SolidBrush wb = new SolidBrush(Color.FromArgb(235, 255, 255, 252))) g.FillPath(wb, gp);
		Txt(g, RankS(rank), fs, 2, c, r.X + 1.5f, r.Y + 1f, bw, r.Width < 30 ? 12f : 15f, StringAlignment.Center);
		if (r.Width >= 30) { Bitmap pip = Pip(su); if (pip != null) { float k = 9f / Math.Max(pip.Width, pip.Height); g.DrawImage(pip, r.X + 1.5f + bw / 2f - pip.Width * k / 2f, r.Y + 16f, pip.Width * k, pip.Height * k); } }
		using (GraphicsPath gp = Gfx.Round(r, 6)) using (Pen p = new Pen(hi ? Color.FromArgb(255, 255, 196, 60) : Color.FromArgb(110, 120, 90, 50), hi ? 3f : 1f)) g.DrawPath(p, gp);
	}
	private static void CardBack(Graphics g, RectangleF r)
	{
		if (backC == null) { Bitmap src = Res("back.jpg"); if (src != null) { backC = Rounded(src); src.Dispose(); } }
		if (backC != null) { float s = ImgCache.Scale(g); Image im = ImgCache.Get(backC, r.Width * s, r.Height * s); InterpolationMode m0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(im, r); g.InterpolationMode = m0; using (GraphicsPath gp = Gfx.Round(r, 6)) using (Pen p = new Pen(Color.FromArgb(120, 255, 236, 190), 1f)) g.DrawPath(p, gp); return; }
		using (GraphicsPath gp = Gfx.Round(r, 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 120, 120))) g.FillPath(b, gp);
	}
	private static void Panel(Graphics g, RectangleF r, int a)
	{
		using (GraphicsPath gp = Gfx.Round(r, 12)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 10, 22, 40))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(70, 200, 230, 255), 1.2f)) g.DrawPath(p, gp); }
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(20, 92, 70), Color.FromArgb(10, 54, 46));
		// 판 정보
		Panel(g, new RectangleF(10, 40, 380, 64), 150);
		int kind = round % 3; string bn = (kind == 0 ? "작은 판" : (kind == 1 ? "큰 판" : "보스 판")) + "  ·  " + (round / 3 + 1) + "단계";
		Txt(g, bn, 9.5f, 2, kind == 2 ? Color.FromArgb(255, 160, 160) : Color.FromArgb(200, 235, 255), 20, 44, 200, 18, StringAlignment.Near);
		Txt(g, "목표 " + target.ToString("N0"), 10f, 2, Color.FromArgb(255, 220, 120), 200, 44, 180, 18, StringAlignment.Far);
		float fr = (float)Math.Min(1.0, shownScore / Math.Max(1.0, target));
		using (GraphicsPath gp = Gfx.Round(new RectangleF(20, 66, 360, 12), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillPath(b, gp);
		if (fr > 0.01f) using (GraphicsPath gp = Gfx.Round(new RectangleF(20, 66, 360 * fr, 12), 6)) using (SolidBrush b = new SolidBrush(fr >= 1f ? Color.FromArgb(120, 230, 140) : Color.FromArgb(90, 200, 255))) g.FillPath(b, gp);
		Txt(g, ((long)shownScore).ToString("N0") + "점", 11f, 2, Color.White, 20, 80, 160, 22, StringAlignment.Near);
		Txt(g, "핸드 " + hands + "  ·  버리기 " + discards + "  ·  더미 " + stock.Count, 9.5f, 1, Color.FromArgb(220, 230, 240), 170, 80, 210, 22, StringAlignment.Far);
		// 부적
		for (int i = 0; i < 5; i++)
		{
			RectangleF r = new RectangleF(10 + i * 77f, 110, 72, 46);
			if (i < jok.Count) { using (GraphicsPath gp = Gfx.Round(r, 10)) { using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(250, 120, 90, 200), Color.FromArgb(250, 70, 50, 140), 90f)) g.FillPath(lb, gp); using (Pen p = new Pen(Color.FromArgb(200, 255, 220, 120), 1.4f)) g.DrawPath(p, gp); } Txt(g, JName[jok[i]], 8.5f, 2, Color.White, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4, StringAlignment.Center, true); }
			else { using (GraphicsPath gp = Gfx.Round(r, 10)) using (Pen p = new Pen(Color.FromArgb(70, 255, 255, 255), 1.2f) { DashStyle = DashStyle.Dash }) g.DrawPath(p, gp); Txt(g, "부적", 8.5f, 1, Color.FromArgb(90, 255, 255, 255), r.X, r.Y, r.Width, r.Height, StringAlignment.Center); }
		}
		// 카드 판
		for (int c = 0; c < 8; c++)
		{
			if (col[c] == null) continue;
			if (col[c].Count == 0) { RectangleF r0 = new RectangleF(ColX(c), TopY, CW, CH); using (GraphicsPath gp = Gfx.Round(r0, 6)) using (Pen p = new Pen(Color.FromArgb(60, 255, 255, 255), 1.4f) { DashStyle = DashStyle.Dash }) g.DrawPath(p, gp); continue; }
			for (int i = 0; i < col[c].Count; i++)
			{
				RectangleF r = CardR(c, i); Cd cd = col[c][i]; bool top = i == col[c].Count - 1, hi = top && sel.Contains(c);
				if (hi && shakeCard > 0f) r.X += (float)Math.Sin(t * 60) * 2f;
				if (cd.Up) CardFace(g, r, cd.R, cd.Su, hi, 1f); else CardBack(g, r);
			}
		}
		// 고른 카드 미리 보기
		List<Cd> pk = Picked();
		if (phase == 0)
		{
			Panel(g, new RectangleF(10, 444, 380, 58), 140);
			if (pk.Count > 0) { int ht; long ch; double mu; Calc(pk, out ht, out ch, out mu); Txt(g, HName[ht], 13f, 2, Color.FromArgb(255, 226, 130), 20, 448, 360, 24, StringAlignment.Center); Txt(g, ch + " 칩  ×  " + (mu % 1 == 0 ? mu.ToString("0") : mu.ToString("0.#")) + " 배수  =  " + ((long)Math.Round(ch * mu)).ToString("N0"), 10.5f, 1, Color.White, 20, 474, 360, 22, StringAlignment.Center); }
			else Txt(g, "맨 위 카드를 눌러 골라요 · 끌어서 옮기면 솔리테어 이동", 10f, 1, Color.FromArgb(210, 230, 240), 20, 448, 360, 48, StringAlignment.Center, true);
			Button(g, PlayBtn, "내기 (" + hands + ")", Color.FromArgb(40, 150, 240), sel.Count > 0 && hands > 0);
			Button(g, DiscBtn, "버리기 (" + discards + ")", Color.FromArgb(230, 90, 90), sel.Count > 0 && discards > 0);
		}
		if (popT > 0f) { float a = Math.Min(1f, popT * 2f), yy = 300f - (2.2f - popT) * 18f; Panel(g, new RectangleF(30, yy - 22, 340, 44), (int)(200 * a)); Txt(g, pop, 13f, 2, Color.FromArgb((int)(255 * a), 255, 230, 120), 30, yy - 22, 340, 44, StringAlignment.Center); }
		if (msgT > 0f && popT <= 0f && phase != 1) Hint(g, msg, 425, 11f, true, Math.Min(1f, msgT * 2f));
		// 부적 고르기
		if (phase == 1)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 6, 14, 26))) g.FillRectangle(b, 0, 0, W, H);
			Txt(g, "판 클리어! 부적을 하나 골라요", 15f, 2, Color.FromArgb(255, 230, 130), 0, 170, W, 34, StringAlignment.Center);
			Txt(g, jok.Count >= 5 ? "부적이 꽉 찼어요 — 고르면 마지막 부적과 바뀌어요" : "부적은 최대 5개까지 가질 수 있어요", 9.5f, 1, Color.FromArgb(220, 230, 240), 0, 202, W, 20, StringAlignment.Center);
			for (int i = 0; i < 3; i++)
			{
				if (offer[i] < 0) continue; RectangleF r = OfferR(i); float bob = (float)Math.Sin(t * 2.4 + i) * 3f; r.Y += bob;
				using (GraphicsPath gp = Gfx.Round(r, 14)) { using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(255, 132, 100, 214), Color.FromArgb(255, 66, 46, 132), 90f)) g.FillPath(lb, gp); using (Pen p = new Pen(Color.FromArgb(230, 255, 220, 120), 2f)) g.DrawPath(p, gp); }
				StarShape(g, r.X + r.Width / 2f, r.Y + 34f, 14f, Color.FromArgb(255, 220, 110));
				Txt(g, JName[offer[i]], 11f, 2, Color.White, r.X + 4, r.Y + 58, r.Width - 8, 24, StringAlignment.Center);
				Txt(g, JDesc[offer[i]], 9f, 1, Color.FromArgb(230, 230, 255), r.X + 6, r.Y + 84, r.Width - 12, 80, StringAlignment.Center, true);
			}
			Button(g, SkipBtn, "건너뛰기", Color.FromArgb(110, 120, 140), true);
		}
	}
	private static void Button(Graphics g, RectangleF r, string s, Color c, bool on)
	{
		Color cc = on ? c : Color.FromArgb(90, 100, 110);
		using (GraphicsPath sp = Gfx.Round(new RectangleF(r.X, r.Y + 3, r.Width, r.Height), r.Height / 2f)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillPath(sb, sp);
		using (GraphicsPath gp = Gfx.Round(r, r.Height / 2f)) using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Theme.Mix(cc, Color.White, 0.18), Theme.Mix(cc, Color.Black, 0.12), 90f)) g.FillPath(lb, gp);
		Txt(g, s, 12f, 2, on ? Color.White : Color.FromArgb(190, 200, 210), r.X, r.Y, r.Width, r.Height, StringAlignment.Center);
	}
}
