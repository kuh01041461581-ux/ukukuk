using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

// ── 운세 확장: 내년 운세(애정·재물·출세·건강) · 평생 사주(대운) · 궁합 · 마룡이 보고용 한 줄 ───────────────
internal static partial class Saju
{
	// 일간의 힘 (0~1, 0.5 이상이면 신강으로 봐요)
	public static double Strength(Pil p)
	{
		int me = SE(p.D % 10); double sup = 0, tot = 0;
		int[] ps = { p.Y % 10, p.M % 10, p.H >= 0 ? p.H % 10 : -1 }, pb = { p.Y % 12, p.D % 12, p.H >= 0 ? p.H % 12 : -1 };
		foreach (int s in ps) if (s >= 0) { tot += 1; int e = SE(s); if (e == me || e == (me + 4) % 5) sup += 1; }
		foreach (int b in pb) if (b >= 0) { double w = b == p.D % 12 ? 1.5 : 1; tot += w; int e = BE(b); if (e == me || e == (me + 4) % 5) sup += w; }
		{ tot += 3; int e = BE(p.M % 12); if (e == me || e == (me + 4) % 5) sup += 3; }
		return sup / tot;
	}
	public static int[] Count(Pil p) { int[] c = new int[5]; int[] ix = { p.Y, p.M, p.D, p.H }; foreach (int i in ix) if (i >= 0) { c[SE(i % 10)]++; c[BE(i % 12)]++; } return c; }
	// 지지 관계: 0 없음 · 1 육합 · 2 충 · 3 해 · 4 삼합(반합) · 5 같음
	public static int BRel(int a, int b) { if (a == b) return 5; if ((a + b) % 12 == 1) return 1; if ((a - b + 12) % 12 == 6) return 2; if ((a + b) % 12 == 7) return 3; if (a % 4 == b % 4) return 4; return 0; }
	// 삼합 묶음(b%4): 0 申子辰 · 1 巳酉丑 · 2 寅午戌 · 3 亥卯未
	private static readonly int[] Flower = { 9, 6, 3, 0 }, Horse = { 2, 11, 8, 5 }, Canopy = { 4, 1, 10, 7 }, SamStart = { 2, 11, 8, 5 };
	public static List<string> Sin(Pil p, int br)
	{
		List<string> L = new List<string>(); int[] bases = { p.Y % 12, p.D % 12 };
		foreach (int bb in bases) { int g = bb % 4; if (br == Flower[g] && !L.Contains("도화")) L.Add("도화"); if (br == Horse[g] && !L.Contains("역마")) L.Add("역마"); if (br == Canopy[g] && !L.Contains("화개")) L.Add("화개"); }
		return L;
	}
	public static string NatalSin(Pil p)
	{
		List<string> all = new List<string>(); int[] ix = { p.Y, p.M, p.D, p.H };
		foreach (int i in ix) if (i >= 0) foreach (string s in Sin(p, i % 12)) if (!all.Contains(s)) all.Add(s);
		return all.Count == 0 ? "특별한 신살 없음" : string.Join(" · ", all.ToArray());
	}
	// 삼재: 0 없음 · 1 들삼재 · 2 눌삼재 · 3 날삼재 (태어난 해의 띠 기준)
	public static int Samjae(int birthYearBranch, int targetBranch) { int d = ((targetBranch - SamStart[birthYearBranch % 4]) % 12 + 12) % 12; return d < 3 ? d + 1 : 0; }
	public static readonly string[] Organ = { "간·담·눈·근육", "심장·혈압·혈관", "위·소화기·비장", "폐·호흡기·피부", "신장·방광·허리·냉증" };

	public sealed class LuckR { public int[] S = new int[4]; public int Total; public string G1, G2; public bool Strong; public List<string> Notes = new List<string>(); public string Organs; }
	private static readonly double[][] WealthT = { new[] { 0.5, -0.5, 0, -0.5, 0.5 }, new[] { -1.0, 0.5, 1.5, 0, -0.5 } };   // [신약/신강][비겁 식상 재성 관성 인성]
	private static readonly double[][] CareerT = { new[] { 0.5, -0.5, -0.5, -0.5, 1.5 }, new[] { -0.5, 0.5, 0.5, 1.5, 0.5 } };
	// target: 60갑자 번호(해·달·대운). 점수 0=애정 1=재물 2=출세 3=건강
	public static LuckR Luck(Pil p, int gender, int target)
	{
		LuckR r = new LuckR(); int dm = p.D % 10, me = SE(dm), ts = target % 10, tb = target % 12, bs = BMain[tb]; bool st = Strength(p) >= 0.5; int si = st ? 1 : 0; r.Strong = st;
		int[] g = { Rel(me, SE(ts)), Rel(me, SE(bs)) }, gi = { TGIndex(dm, ts), TGIndex(dm, bs) }; r.G1 = TenGod(dm, ts); r.G2 = TenGod(dm, bs);
		double w = 0, c = 0, l = 0, h = 0; int dB = p.D % 12;
		for (int k = 0; k < 2; k++) { w += WealthT[si][g[k]]; c += CareerT[si][g[k]]; }
		int sg = gender == 0 ? 2 : 3;
		for (int k = 0; k < 2; k++) { if (g[k] == sg) l += 1.4; if (gender == 0 && g[k] == 0) l -= gi[k] == 1 ? 0.8 : 0.3; if (gender == 1 && gi[k] == 3) l -= 0.7; }
		int rel = BRel(tb, dB);
		if (rel == 1) { l += 1.2; r.Notes.Add("지지가 일지(배우자궁)와 합 · 안정적인 인연이 닿아요"); }
		else if (rel == 4) { l += 0.6; r.Notes.Add("일지와 삼합 기운 · 사람 인연이 모여요"); }
		else if (rel == 2) { l -= 1.2; h -= 1; r.Notes.Add("일지와 충 · 다툼·이동·변동수를 조심해요"); }
		else if (rel == 3) { l -= 0.5; r.Notes.Add("일지와 해 · 작은 오해가 생기기 쉬워요"); }
		if (Math.Abs(dm - ts) == 5) { l += 0.8; r.Notes.Add("천간이 일간과 합 · 끌림과 결합의 기운"); }
		List<string> sn = Sin(p, tb);
		if (sn.Contains("도화")) { l += 0.8; r.Notes.Add("도화 기운 · 이성에게 인기가 올라요"); }
		if (sn.Contains("역마")) r.Notes.Add("역마 기운 · 이동·출장·변화가 많아요");
		if (sn.Contains("화개")) r.Notes.Add("화개 기운 · 공부·예술에 몰입하기 좋아요");
		int[] cnt = Count(p); int[] c2 = (int[])cnt.Clone(); int[] te = { SE(ts), BE(tb) };
		foreach (int e in te) { c2[e]++; if (cnt[e] == 0) h += 0.7; else if (cnt[e] >= 3) h -= 0.7; }
		if (BRel(tb, p.M % 12) == 2) h -= 0.5;
		string stg = Stage(dm, tb); if (stg == "장생" || stg == "건록" || stg == "제왕" || stg == "관대") h += 0.5; else if (stg == "병" || stg == "사" || stg == "묘" || stg == "절") h -= 0.5;
		if (!st && (g[0] == 3 || g[1] == 3)) h -= 0.5;
		double[] raw = { l, w, c, h }; for (int i = 0; i < 4; i++) r.S[i] = Math.Max(1, Math.Min(5, (int)Math.Floor(3 + raw[i] + 0.5)));
		r.Total = (int)Math.Floor((r.S[0] + r.S[1] + r.S[2] + r.S[3]) / 4.0 + 0.5);
		int mn = 0, mx = 0; for (int i = 0; i < 5; i++) { if (c2[i] < c2[mn]) mn = i; if (c2[i] > c2[mx]) mx = i; }
		r.Organs = "약한 " + EN[mn] + " 기운 → " + Organ[mn] + " 관리" + (c2[mx] >= 3 && mx != mn ? " · 넘치는 " + EN[mx] + " 기운 → " + Organ[mx] + " 과로 주의" : "");
		return r;
	}
}

internal sealed partial class FortuneForm
{
	private sealed class Rd { public int Rel, Total, RefIdx; public int[] Sc = new int[4]; public string Pname, Seed; public DateTime Key; }
	// 오늘·주·달·올해 점수: 창과 마룡이 보고가 같은 값을 쓰도록 한 곳에서 계산해요
	private static Rd ReadCalc(int period, DateTime now, int cal, int by, int bm, int bd, int bh, int bmin, int bg, string mbti, Saju.Pil pil)
	{
		Rd R = new Rd(); int me = Saju.SE(pil.D % 10); int refIdx; string pname; DateTime key = now.Date;
		if (period == 0) { refIdx = Saju.Calc(now, true, true, 0).D; pname = "오늘의 기운 " + Saju.Name(refIdx) + "일"; }
		else if (period == 1) { DateTime th = now.Date.AddDays(4 - (((int)now.DayOfWeek + 6) % 7)); refIdx = Saju.Calc(th.AddHours(12), true, true, 0).D; pname = "이번 주 기운 " + Saju.Name(refIdx); key = th; }
		else if (period == 2) { refIdx = Saju.Calc(now, true, true, 0).M; pname = "이번 달 기운 " + Saju.Name(refIdx) + "월"; key = new DateTime(now.Year, now.Month, 1); }
		else { refIdx = Saju.Calc(now, true, true, 0).Y; pname = now.Year + "년 기운 " + Saju.Name(refIdx) + "년"; key = new DateTime(now.Year, 1, 1); }
		int se = Saju.SE(refIdx % 10), be = Saju.BE(refIdx % 12), rel = Saju.Rel(me, se), rel2 = Saju.Rel(me, be);
		string seed = cal + "-" + by + "-" + bm + "-" + bd + "-" + bh + "-" + bmin + "-" + bg + "-" + mbti + "-" + period + "-" + key.ToString("yyyyMMdd");
		int sum = 0; for (int i = 0; i < 4; i++) { int b0 = (CatBase[i][rel] * 2 + CatBase[i][rel2]) / 3 + (i == 1 && bg == 1 && rel == 3 ? 1 : (i == 1 && bg == 0 && rel == 2 ? 1 : 0)); int v = b0 + (Saju.Hash(seed + "c" + i) % 3) - 1; R.Sc[i] = Math.Max(1, Math.Min(5, v)); sum += R.Sc[i]; }
		R.Total = Math.Max(1, Math.Min(5, (int)Math.Round((sum / 4.0 + RelBase[rel]) / 2.0 + (Saju.Hash(seed + "t") % 3 - 1) * 0.4)));
		R.Rel = rel; R.RefIdx = refIdx; R.Pname = pname; R.Seed = seed; R.Key = key; return R;
	}
	// 마룡이 보고용: 저장된 내 정보로 오늘의 운세 한 줄 (정보가 없으면 null)
	public static string TodayLine()
	{
		try
		{
			string path = Path.Combine(Program.DataDir, "saju2.txt"); if (!File.Exists(path)) return null;
			string[] p = File.ReadAllText(path).Trim().Split('|'); int cal = int.Parse(p[0]), by = int.Parse(p[1]), bm = int.Parse(p[2]), bd = int.Parse(p[3]), bh = int.Parse(p[4]), bmin = int.Parse(p[5]), bg = int.Parse(p[6]); bool leap = p[7] == "1", corr = p[8] == "1"; string mb = p.Length > 9 ? p[9] : "";
			DateTime so = SolarOf(cal, by, bm, bd, leap); DateTime t = so.AddHours(bh >= 0 ? bh : 12).AddMinutes(bh >= 0 ? bmin : 0); Saju.Pil pl = Saju.Calc(t, bh >= 0, corr, bg);
			DateTime now = DateTime.Now; Rd R = ReadCalc(0, now, cal, by, bm, bd, bh, bmin, bg, mb, pl); int me = Saju.SE(pl.D % 10);
			int lo = 0, hi = 0; for (int i = 1; i < 4; i++) { if (R.Sc[i] < R.Sc[lo]) lo = i; if (R.Sc[i] > R.Sc[hi]) hi = i; }
			string[] good = { "재물 기회", "설레는 일", "활력 충만", "성과 기대" }, bad = { "지갑 단속", "말조심", "컨디션 관리", "실수 점검" };
			int lucky = Saju.Rel(me, 0) == 0 ? ((me + 4) % 5) : me;
			string s = "오늘의 운세 " + Stars(R.Total);
			if (R.Sc[hi] >= 4) s += " · 좋음: " + good[hi]; if (R.Sc[lo] <= 2) s += " · 조심: " + bad[lo];
			if (R.Sc[2] <= 2 && lo != 2) s += " · 건강 챙기세요"; return s + " · 행운색 " + ELuckColor[lucky];
		}
		catch { return null; }
	}

	private static readonly string[][] LoveT = { null, new[] { "인연이 엇갈리기 쉬운 해예요. 말 한마디를 부드럽게!" }, new[] { "연애보다 나를 가꾸기 좋은 해예요. 조급함은 금물!" }, new[] { "잔잔한 흐름이에요. 먼저 다가가면 문이 열려요." }, new[] { "좋은 인연이 닿는 해예요. 모임·소개 자리를 놓치지 마세요." }, new[] { "설레는 인연과 결실의 해! 마음을 표현해 보세요." } };
	private static readonly string[][] WealthTx = { null, new[] { "지출·손실 수가 있어요. 큰 투자·보증은 피해요." }, new[] { "새는 돈을 막는 게 곧 이익이에요. 계획 소비를!" }, new[] { "수입과 지출이 균형을 이뤄요. 꾸준히 모아 보세요." }, new[] { "일한 만큼 돈이 따라와요. 부수입 기회도 있어요." }, new[] { "재물이 크게 붙는 해! 단, 욕심과 충동구매는 금물." } };
	private static readonly string[][] CareerTx = { null, new[] { "책임은 늘고 인정은 더딘 해예요. 버티면 큰 힘이 돼요." }, new[] { "무리한 승부보다 실력 쌓기가 이득이에요." }, new[] { "맡은 일을 꾸준히 하면 평가가 따라와요." }, new[] { "인정받고 직책·기회가 열려요. 준비해 둔 만큼 잡아요." }, new[] { "승진·합격·명예 운이 활짝! 과감히 도전하세요." } };
	private static readonly string[][] HealthTx = { null, new[] { "컨디션 관리가 최우선! 건강검진을 꼭 받아요." }, new[] { "무리하면 탈이 나요. 수면·식사를 규칙적으로!" }, new[] { "무난해요. 가벼운 운동을 꾸준히 해요." }, new[] { "활력이 좋아요. 운동을 시작하기 좋은 해예요." }, new[] { "몸도 마음도 가벼운 해! 체력을 키워 두세요." } };
	private static readonly string[] SamName = { "", "들삼재", "눌삼재", "날삼재" };
	private static readonly string[] SamTip = { "", "삼재가 시작되는 해예요. 무리한 확장·이사는 천천히!", "삼재 한가운데예요. 조심조심 지키는 해로 보내요.", "삼재의 마지막 해예요. 곧 지나가니 마무리를 잘해요." };

	private void DrawCatBlock(Graphics g, float y, string name, int s, string text, string tag)
	{
		using (GraphicsPath gp = Gfx.Round(new RectangleF(24, y, 372, 66), 10)) using (SolidBrush b = new SolidBrush(PCard)) g.FillPath(b, gp);
		FUi.Text(g, name, FUi.F(10f, 1), PInk, new RectangleF(34, y + 4, 70, 22), StringAlignment.Near, StringAlignment.Center, false); Star(g, 104, y + 6, s);
		FUi.Text(g, tag, FUi.F(8.5f, 1), s >= 4 ? Theme.Green : (s <= 2 ? PRed : PSub), new RectangleF(220, y + 4, 168, 22), StringAlignment.Far, StringAlignment.Center, false);
		FUi.Text(g, text, FUi.F(9f, 0), PSub, new RectangleF(34, y + 28, 352, 34), StringAlignment.Near, StringAlignment.Near, true);
	}
	private static readonly string[] LoveTag = { "", "엇갈림 주의", "나를 가꿔요", "잔잔해요", "인연 가득", "사랑 폭발!" }, WealthTag = { "", "지갑 닫기", "아껴 쓰기", "무난해요", "돈이 붙어요", "대박 기운!" }, CareerTag = { "", "버티는 해", "실력 쌓기", "꾸준히", "인정받아요", "승진·합격운" }, HealthTag = { "", "검진 필수", "무리 금지", "보통이에요", "활기차요", "컨디션 최고" };

	private void DrawNext(Graphics g, DateTime now)
	{
		int yr = tY4, idx = Saju.Calc(new DateTime(yr, 6, 1, 12, 0, 0), false, true, 0).Y; Saju.LuckR r = Saju.Luck(pil, bg, idx); int dm = pil.D % 10, me = Saju.SE(dm);
		FUi.Text(g, yr + "년 " + Saju.Name(idx) + "(" + Saju.Hanja(idx) + ")년 · " + Saju.Animal(idx) + "의 해", FUi.F(11f, 1), PInk, new RectangleF(26, 146, 372, 22), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "내 일간 " + Saju.SK[dm] + "(" + Saju.EN[me] + ") 기준 · 천간 " + r.G1 + " · 지지 " + r.G2 + " · " + (r.Strong ? "신강" : "신약") + " 사주", FUi.F(8.5f, 0), PMut, new RectangleF(26, 168, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		float y = 192f; FUi.Text(g, "총운", FUi.F(11f, 1), PInk, new RectangleF(26, y, 60, 24), StringAlignment.Near, StringAlignment.Center, false); Star(g, 76, y + 1, r.Total);
		FUi.Text(g, new[] { "", "조심조심", "차분하게", "무난한", "좋아요", "아주 좋아요!" }[r.Total], FUi.F(10f, 1), r.Total >= 4 ? Theme.Green : (r.Total <= 2 ? Theme.Red : PSub), new RectangleF(200, y, 190, 24), StringAlignment.Far, StringAlignment.Center, false);
		y = 222f;
		DrawCatBlock(g, y, "애정운", r.S[0], LoveT[r.S[0]][0], LoveTag[r.S[0]]); y += 70f;
		DrawCatBlock(g, y, "재물운", r.S[1], WealthTx[r.S[1]][0], WealthTag[r.S[1]]); y += 70f;
		DrawCatBlock(g, y, "출세운", r.S[2], CareerTx[r.S[2]][0], CareerTag[r.S[2]]); y += 70f;
		DrawCatBlock(g, y, "건강운", r.S[3], HealthTx[r.S[3]][0], HealthTag[r.S[3]]); y += 74f;
		for (int i = 0; i < r.Notes.Count && i < 3; i++) { FUi.Text(g, "● " + r.Notes[i], FUi.F(8.5f, 0), PSub, new RectangleF(26, y, 372, 18), StringAlignment.Near, StringAlignment.Center, false); y += 18f; }
		int sj = Saju.Samjae(pil.Y % 12, idx % 12);
		FUi.Text(g, sj == 0 ? "삼재: 이 해는 해당 없어요 (" + Saju.AnA[pil.Y % 12] + "띠)" : "삼재: " + SamName[sj] + " · " + SamTip[sj], FUi.F(8.5f, 1), sj == 0 ? PSub : PRed, new RectangleF(26, y, 372, 18), StringAlignment.Near, StringAlignment.Center, false); y += 20f;
		FUi.Text(g, "건강 포인트: " + r.Organs, FUi.F(8.5f, 0), PSub, new RectangleF(26, y, 372, 32), StringAlignment.Near, StringAlignment.Near, true);
		// 월별 흐름
		float by0 = 636f; int best = 1, worst = 1, bk = -1, wk = 99; int[] mt = new int[13];
		for (int m = 1; m <= 12; m++) { Saju.LuckR mr = Saju.Luck(pil, bg, Saju.Calc(new DateTime(yr, m, 15, 12, 0, 0), false, true, 0).M); mt[m] = mr.Total; int k = mr.Total * 100 + mr.S[0] + mr.S[1] + mr.S[2] + mr.S[3]; if (k > bk) { bk = k; best = m; } if (k < wk) { wk = k; worst = m; } }
		FUi.Text(g, "월별 흐름 (월주 기준)", FUi.F(9.5f, 1), PInk, new RectangleF(26, by0, 180, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "좋은 달 " + best + "월 · 조심할 달 " + worst + "월", FUi.F(8.5f, 1), PRed, new RectangleF(200, by0, 190, 18), StringAlignment.Far, StringAlignment.Center, false);
		for (int m = 1; m <= 12; m++)
		{
			float x = 28f + (m - 1) * 30f, hgt = 6f + mt[m] * 7f; RectangleF br = new RectangleF(x, by0 + 24f + (42f - hgt), 22, hgt);
			using (GraphicsPath gp = Gfx.Round(br, 4)) using (SolidBrush b = new SolidBrush(m == best ? Theme.Green : (m == worst ? Theme.Amber : (mt[m] >= 4 ? Color.FromArgb(150, 86, 168, 98) : Color.FromArgb(140, 190, 168, 136))))) g.FillPath(b, gp);
			FUi.Text(g, m + "", FUi.F(7.5f, 0), PMut, new RectangleF(x - 4, by0 + 68f, 30, 12), StringAlignment.Center, StringAlignment.Center, false);
		}
		FUi.Text(g, "▼ 스크롤하면 분야별·분기별·월별 자세한 풀이가 나와요", FUi.F(8f, 1), PRed, new RectangleF(26, 718, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		contentH = YearDetail(g, 748f, yr, idx, r);
	}

	private static readonly string[] Dsc = { "동료·경쟁의 시기", "재능·표현의 시기", "재물·성과의 시기", "직장·명예의 시기", "학업·귀인의 시기" };
	private static readonly string[] TGJob = { "독립 사업·전문직·프리랜서처럼 자기 주도로 하는 일", "영업·스포츠·승부가 있는 분야 (동업은 신중히)", "요리·교육·서비스·콘텐츠 등 꾸준히 베푸는 일", "기획·방송·예술·IT 등 창의와 표현이 필요한 일", "무역·영업·투자·유통 등 활동적인 사업", "회계·금융·공공 등 안정적인 관리·재무 분야", "군·경·법조·의료 등 책임과 카리스마가 필요한 일", "공직·대기업·행정 등 조직 안의 정통 커리어", "연구·기술·상담·예술 등 독창적인 전문 분야", "교육·학문·출판·복지 등 가르치고 돌보는 일" };
	private void DrawLife(Graphics g, DateTime now)
	{
		int age = now.Year - sM.Year + 1, dm = pil.D % 10; List<int[]> rows = new List<int[]>();
		if (pil.StartAge > 1) rows.Add(new[] { 1, pil.StartAge - 1, pil.M }); for (int k = 0; k < 8; k++) rows.Add(new[] { pil.StartAge + k * 10, pil.StartAge + k * 10 + 9, pil.Dae[k] });
		List<Saju.LuckR> LL = LifeLuck(); FUi.Text(g, "평생 사주 · 대운 흐름", FUi.F(11f, 1), PInk, new RectangleF(26, 146, 250, 22), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "대운 " + pil.StartAge + "세부터 " + (pil.Fwd ? "순행" : "역행") + " · 지금 " + age + "세 · 애 재 출 건 = 애정·재물·출세·건강", FUi.F(8.5f, 0), PMut, new RectangleF(26, 168, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		string[] lab = { "애", "재", "출", "건" }; int[][] S = new int[rows.Count][]; int[] TT = new int[rows.Count]; float y = 190f;
		for (int i = 0; i < rows.Count; i++)
		{
			int[] rw = rows[i]; Saju.LuckR r = LL[i]; S[i] = r.S; TT[i] = r.Total; bool now0 = age >= rw[0] && age <= rw[1]; int di = rw[2];
			RectangleF rc = new RectangleF(24, y, 372, 34);
			using (GraphicsPath gp = Gfx.Round(rc, 8)) { using (SolidBrush b = new SolidBrush(now0 ? PSoft : PCard)) g.FillPath(b, gp); using (Pen p = new Pen(now0 ? PRed : PLine, now0 ? 1.8f : 1f)) g.DrawPath(p, gp); }
			FUi.Text(g, (i == 0 && pil.StartAge > 1 ? "초년 " : "") + rw[0] + "~" + rw[1] + "세", FUi.F(8.5f, 1), now0 ? PRed : PSub, new RectangleF(30, y + 2, 76, 16), StringAlignment.Near, StringAlignment.Center, false);
			FUi.Text(g, Saju.Hanja(di), FUi.F(11f, 1), EC[Saju.SE(di % 10)], new RectangleF(100, y + 1, 46, 18), StringAlignment.Center, StringAlignment.Center, false);
			for (int c = 0; c < 4; c++)
			{
				float gx = 150f + c * 62f; FUi.Text(g, lab[c], FUi.F(8f, 1), PMut, new RectangleF(gx, y + 2, 12, 16), StringAlignment.Center, StringAlignment.Center, false);
				for (int k = 0; k < 5; k++) using (SolidBrush b = new SolidBrush(k < r.S[c] ? (c == 3 ? Theme.Green : Theme.Gold) : PLine)) g.FillEllipse(b, gx + 14 + k * 9, y + 7, 7, 7);
			}
			FUi.Text(g, "천간 " + r.G1 + " · 지지 " + r.G2 + " — " + Dsc[Saju.Rel(Saju.SE(dm), Saju.SE(di % 10))], FUi.F(7.5f, 0), PMut, new RectangleF(32, y + 19, 358, 14), StringAlignment.Near, StringAlignment.Center, false);
			y += 36f;
		}
		y += 2f; FUi.Text(g, "평생 운 요약", FUi.F(10f, 1), PInk, new RectangleF(26, y, 200, 18), StringAlignment.Near, StringAlignment.Center, false); y += 20f;
		string[] cn = { "애정운", "재물운", "출세운" };
		for (int c = 0; c < 3; c++)
		{
			int bi = 0; for (int i = 1; i < rows.Count; i++) if (S[i][c] > S[bi][c]) bi = i;
			FUi.Text(g, "● " + cn[c] + " 최고: " + rows[bi][0] + "~" + rows[bi][1] + "세", FUi.F(8.5f, 0), PSub, new RectangleF(26 + (c % 2) * 190, y + (c / 2) * 18, 186, 18), StringAlignment.Near, StringAlignment.Center, false);
		}
		{ int w0 = pil.StartAge > 1 ? 1 : 0, wi = w0; for (int i = w0 + 1; i < rows.Count; i++) if (S[i][3] < S[wi][3]) wi = i; FUi.Text(g, "● 건강 주의: " + rows[wi][0] + "~" + rows[wi][1] + "세", FUi.F(8.5f, 0), PRed, new RectangleF(216, y + 18, 186, 18), StringAlignment.Near, StringAlignment.Center, false); }
		y += 40f;
		FUi.Text(g, "타고난 신살: " + Saju.NatalSin(pil), FUi.F(8.5f, 1), PSub, new RectangleF(26, y, 372, 18), StringAlignment.Near, StringAlignment.Center, false); y += 20f;
		int tgi = Saju.TGIndex(dm, Saju.BMain[pil.M % 12]);
		FUi.Text(g, "어울리는 일: " + TGJob[tgi], FUi.F(8.5f, 0), PSub, new RectangleF(26, y, 372, 32), StringAlignment.Near, StringAlignment.Near, true);
				y += 38f; FUi.Text(g, "인생 흐름 그래프 (총운)", FUi.F(9.5f, 1), PInk, new RectangleF(26, y, 220, 16), StringAlignment.Near, StringAlignment.Center, false);
		{
			int n = rows.Count; float gx0 = 44f, gw = 332f, gy0 = y + 22f, gh = 28f; PointF[] pts = new PointF[n];
			for (int i = 0; i < n; i++) pts[i] = new PointF(gx0 + gw * i / Math.Max(1, n - 1), gy0 + gh * (1f - (TT[i] - 1) / 4f));
			using (Pen gl = new Pen(PLine, 1f)) { g.DrawLine(gl, gx0 - 8, gy0 + gh / 2, gx0 + gw + 8, gy0 + gh / 2); }
			if (n > 1) using (Pen lp = new Pen(PRed, 2f)) g.DrawLines(lp, pts);
			for (int i = 0; i < n; i++) { bool cur = age >= rows[i][0] && age <= rows[i][1]; using (SolidBrush b = new SolidBrush(cur ? PRed : Theme.Gold)) g.FillEllipse(b, pts[i].X - (cur ? 5 : 3.5f), pts[i].Y - (cur ? 5 : 3.5f), cur ? 10 : 7, cur ? 10 : 7); FUi.Text(g, rows[i][0] + "", FUi.F(7f, 0), PMut, new RectangleF(pts[i].X - 14, gy0 + gh + 4, 28, 12), StringAlignment.Center, StringAlignment.Center, false); }
		}
		FUi.Text(g, "▼ 스크롤하면 대운별 자세한 풀이가 나와요", FUi.F(8f, 1), PRed, new RectangleF(26, 718, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		contentH = LifeDetail(g, 748f);
	}

	private static readonly string[] TGMean = { "친구 같은 동료", "경쟁하며 자극을 주는 사이", "편하게 베푸는 사이", "재치와 자극을 주는 사이", "활동적이고 신나는 인연", "안정과 현실을 챙겨 주는 사이", "카리스마 있고 긴장감 있는 인연", "반듯하게 이끌어 주는 사이", "독특한 영감을 주는 사이", "품어 주고 보살피는 사이" };
	private void MiniPil(Graphics g, float x, float y, Saju.Pil p, string title, bool isMe)
	{
		using (GraphicsPath gp = Gfx.Round(new RectangleF(x, y, 182, 84), 10)) { using (SolidBrush b = new SolidBrush(PCard)) g.FillPath(b, gp); using (Pen pn = new Pen(isMe ? PRed : PLine, isMe ? 1.8f : 1f)) g.DrawPath(pn, gp); }
		FUi.Text(g, title, FUi.F(9f, 1), isMe ? PRed : PInk, new RectangleF(x + 8, y + 3, 166, 18), StringAlignment.Near, StringAlignment.Center, false);
		int[] ix = { p.H, p.D, p.M, p.Y }; string[] lb = { "시", "일", "월", "연" };
		for (int i = 0; i < 4; i++)
		{
			float cx = x + 8 + i * 42f; FUi.Text(g, lb[i], FUi.F(7.5f, 0), PMut, new RectangleF(cx, y + 22, 40, 12), StringAlignment.Center, StringAlignment.Center, false);
			if (ix[i] < 0) { FUi.Text(g, "—", FUi.F(11f, 0), PMut, new RectangleF(cx, y + 36, 40, 40), StringAlignment.Center, StringAlignment.Center, false); continue; }
			FUi.Text(g, Saju.SH[ix[i] % 10], FUi.F(14f, 1), EC[Saju.SE(ix[i] % 10)], new RectangleF(cx, y + 34, 40, 22), StringAlignment.Center, StringAlignment.Center, false);
			FUi.Text(g, Saju.BH[ix[i] % 12], FUi.F(14f, 1), EC[Saju.BE(ix[i] % 12)], new RectangleF(cx, y + 56, 40, 22), StringAlignment.Center, StringAlignment.Center, false);
		}
	}
	private void MatchBar(Graphics g, float y, string name, int s, string note)
	{
		FUi.Text(g, name, FUi.F(9f, 1), PInk, new RectangleF(26, y, 86, 20), StringAlignment.Near, StringAlignment.Center, false);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(112, y + 5, 150, 10), 5)) using (SolidBrush b = new SolidBrush(PLine)) g.FillPath(b, gp);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(112, y + 5, 150f * s / 100f, 10), 5)) using (SolidBrush b = new SolidBrush(s >= 80 ? Theme.Green : (s < 55 ? Theme.Amber : PRed))) g.FillPath(b, gp);
		FUi.Text(g, s + "점", FUi.F(8.5f, 1), PSub, new RectangleF(266, y, 40, 20), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, note, FUi.F(8f, 0), PMut, new RectangleF(306, y, 92, 20), StringAlignment.Near, StringAlignment.Center, false);
	}
	private void DrawMatch(Graphics g, DateTime now)
	{
		g.TranslateTransform(0, 98f); Saju.Pil A = pM, B = pP; int a = A.D % 10, b = B.D % 10, ea = Saju.SE(a), eb = Saju.SE(b); int dA = A.D % 12, dB = B.D % 12, yA = A.Y % 12, yB = B.Y % 12;
		FUi.Text(g, "나 ♥ 상대 궁합", FUi.F(11f, 1), PInk, new RectangleF(26, 146, 200, 22), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, pSet ? "위 칸의 상대방 정보를 바꾸면 바로 다시 계산해요" : "먼저 위 칸에서 상대방의 생일·성별을 골라 주세요", FUi.F(8.5f, 0), pSet ? PMut : PRed, new RectangleF(26, 168, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		MiniPil(g, 24, 190, A, "나 · " + Saju.SK[a] + "(" + Saju.EN[ea] + ") 일간", true); MiniPil(g, 214, 190, B, "상대 · " + Saju.SK[b] + "(" + Saju.EN[eb] + ") 일간", false);
		// 1 일간(성향)
		int s1; string n1; int r1 = Saju.Rel(ea, eb);
		if (Math.Abs(a - b) == 5) { s1 = 95; n1 = "일간끼리 합 · 서로 끌리는 천생연분 기운이에요"; }
		else if (r1 == 4) { s1 = 85; n1 = "상대가 나를 도와주는(생해 주는) 관계예요"; }
		else if (r1 == 1) { s1 = 82; n1 = "내가 상대에게 베푸는 관계예요"; }
		else if (r1 == 0) { s1 = 72; n1 = "비슷한 기운이라 편하지만 고집 대결도 있어요"; }
		else if (r1 == 2) { s1 = 58; n1 = "내가 상대를 이끄는 관계예요 · 밀어붙이지 않기"; }
		else { s1 = 55; n1 = "상대가 나를 이끄는 관계예요 · 긴장감이 있어요"; }
		int dd = ((a - b) % 10 + 10) % 10; if (dd == 4 || dd == 6) { s1 -= 10; n1 = "일간이 충 · 성향 차이가 커서 서로 배려가 필요해요"; }
		// 2 일지(배우자궁)
		int q2 = Saju.BRel(dA, dB); int[] sc2 = { 65, 95, 35, 45, 85, 70 }; int s2 = sc2[q2];
		string[] nt2 = { "일지(배우자궁) 관계는 무난해요", "일지가 육합 · 마음이 잘 통하고 정이 깊어요", "일지가 충 · 의견 충돌이 잦으니 대화로 풀어요", "일지가 해 · 작은 오해를 조심해요", "일지가 삼합 · 함께하면 힘이 커져요", "일지가 같아요 · 닮은 점이 많아요" };
		// 3 띠(년지)
		int q3 = Saju.BRel(yA, yB); int[] sc3 = { 65, 90, 40, 50, 82, 72 }; int s3 = sc3[q3];
		string[] nt3 = { "무난한 띠궁합", "찰떡 띠궁합", "정반대 띠 · 다름을 인정해요", "오해가 생기기 쉬운 띠", "잘 맞는 띠궁합", "같은 띠 · 통하는 게 많아요" };
		// 4 오행 보완
		int[] cA = Saju.Count(A), cB = Saju.Count(B); int fill = 0, bothLack = 0; int fe = -1;
		for (int e = 0; e < 5; e++) { if (cA[e] == 0 && cB[e] >= 2) { fill++; fe = e; } if (cB[e] == 0 && cA[e] >= 2) { fill++; fe = e; } if (cA[e] == 0 && cB[e] == 0) bothLack++; }
		int s4 = Math.Max(35, Math.Min(95, 60 + 12 * fill - 8 * bothLack)); string n4 = fill > 0 ? "서로 부족한 " + Saju.EN[fe] + " 기운을 채워 줘요" + (bothLack > 0 ? "" : "") : (bothLack > 0 ? "둘 다 부족한 기운이 있어요" : "오행 균형은 비슷해요");
		// 5 MBTI
		bool hasM = mM != "" && mP != ""; int s5 = 0; string n5 = "";
		if (hasM) { int ns = mM[1] == mP[1] ? 1 : 0, tf = mM[2] == mP[2] ? 1 : 0, jp = mM[3] == mP[3] ? 1 : 0, ei = mM[0] != mP[0] ? 1 : 0; s5 = 55 + ns * 15 + tf * 10 + jp * 8 + ei * 7; n5 = (ns == 1 ? "생각 방식이 닮았고" : "생각 방식이 달라 신선하고") + (tf == 1 ? " 감정 코드가 같아요" : " 표현 방식은 맞춰 가요"); }
		double tot = hasM ? s1 * .27 + s2 * .27 + s3 * .12 + s4 * .19 + s5 * .15 : s1 * .3 + s2 * .3 + s3 * .15 + s4 * .25; int score = Math.Max(30, Math.Min(98, (int)Math.Round(tot)));
		string[] lv = { "서로 맞춰 가는 궁합", "노력이 필요한 궁합", "무난한 궁합", "좋은 궁합", "아주 좋은 궁합", "천생연분!" }; int li = score >= 90 ? 5 : (score >= 80 ? 4 : (score >= 70 ? 3 : (score >= 60 ? 2 : (score >= 50 ? 1 : 0))));
		// 종합 점수 링
		float cy = 296f; using (Pen pb = new Pen(PLine, 10f)) g.DrawEllipse(pb, 24 + 14, cy + 4, 72, 72);
		using (Pen pa = new Pen(li >= 3 ? Theme.Green : (li <= 1 ? Theme.Amber : PRed), 10f)) { pa.StartCap = LineCap.Round; pa.EndCap = LineCap.Round; g.DrawArc(pa, 24 + 14, cy + 4, 72, 72, -90, 360f * score / 100f); }
		FUi.Text(g, score + "", FUi.F(20f, 2), PInk, new RectangleF(38, cy + 18, 72, 30), StringAlignment.Center, StringAlignment.Center, false); FUi.Text(g, "점", FUi.F(9f, 1), PSub, new RectangleF(38, cy + 48, 72, 16), StringAlignment.Center, StringAlignment.Center, false);
		FUi.Text(g, lv[li], FUi.F(13f, 1), li >= 3 ? Theme.Green : (li <= 1 ? PRed : PInk), new RectangleF(130, cy + 6, 266, 26), StringAlignment.Near, StringAlignment.Center, false);
		string sumTx = li >= 4 ? "함께하면 서로의 운이 올라가는 사이예요. 고마움을 자주 표현해요!" : (li == 3 ? "대체로 편안하고 든든한 사이예요. 작은 배려가 큰 힘이 돼요." : (li == 2 ? "무난하게 이어지는 사이예요. 서로의 리듬을 존중하면 좋아져요." : "다른 점이 많아 맞춰 가야 하는 사이예요. 대화와 양보가 열쇠예요."));
		FUi.Text(g, sumTx, FUi.F(9f, 0), PSub, new RectangleF(130, cy + 34, 266, 42), StringAlignment.Near, StringAlignment.Near, true);
		float y = 388f; MatchBar(g, y, "성향 (일간)", s1, ""); y += 24f; MatchBar(g, y, "배우자궁(일지)", s2, ""); y += 24f; MatchBar(g, y, "띠 궁합", s3, Saju.AnA[yA] + "×" + Saju.AnA[yB]); y += 24f; MatchBar(g, y, "오행 보완", s4, "");
		if (hasM) { y += 24f; MatchBar(g, y, "MBTI 케미", s5, mM + "×" + mP); }
		y += 30f;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(24, y, 372, 112), 10)) using (SolidBrush br = new SolidBrush(PSoft)) g.FillPath(br, gp);
		FUi.Text(g, "● " + n1, FUi.F(8.5f, 0), PInk, new RectangleF(34, y + 6, 356, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "● " + nt2[q2], FUi.F(8.5f, 0), PInk, new RectangleF(34, y + 26, 356, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "● " + Saju.AnA[yA] + "띠 × " + Saju.AnA[yB] + "띠 · " + nt3[q3], FUi.F(8.5f, 0), PInk, new RectangleF(34, y + 46, 356, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "● " + n4, FUi.F(8.5f, 0), PInk, new RectangleF(34, y + 66, 356, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, hasM ? "● " + n5 : "● MBTI를 둘 다 고르면 마음 케미도 함께 봐요", FUi.F(8.5f, 0), PInk, new RectangleF(34, y + 86, 356, 18), StringAlignment.Near, StringAlignment.Center, false);
		y += 120f;
		FUi.Text(g, "상대는 나에게 '" + Saju.TenGod(a, b) + "' · " + TGMean[Saju.TGIndex(a, b)], FUi.F(8.5f, 1), PSub, new RectangleF(26, y, 372, 18), StringAlignment.Near, StringAlignment.Center, false); y += 20f;
		FUi.Text(g, "나는 상대에게 '" + Saju.TenGod(b, a) + "' · " + TGMean[Saju.TGIndex(b, a)], FUi.F(8.5f, 1), PSub, new RectangleF(26, y, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "▼ 스크롤하면 자세한 궁합 풀이가 나와요", FUi.F(8f, 1), PRed, new RectangleF(26, 718, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		contentH = MatchDetail(g, 746f, a, b, ea, eb, q2, q3, s1, s2, s3, s4, hasM ? s5 : -1, score, li, fe, bothLack) + 98f;
	}
}
