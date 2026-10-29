using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;


// 운세·로또 창 공통: 화면 크기에 맞춰 키우고(배율 그리기), 글씨는 오락실과 같은 방식으로 그려요
internal struct FF { public float Pt; public int Wt; }
internal static class FUi
{
	public static FF F(float pt, int wt) { return new FF { Pt = pt, Wt = wt }; }
	public static void Text(Graphics g, string s, FF f, Color c, RectangleF r, StringAlignment h, StringAlignment v, bool wrap) { MiniGame.Txt(g, s, f.Pt, f.Wt, c, r.X, r.Y, r.Width, r.Height, h, wrap); }
	public static float Scale(int w, int h) { try { Rectangle wa = Screen.PrimaryScreen.WorkingArea; float s = Math.Min(wa.Height * 0.9f / h, wa.Width * 0.6f / w); return Math.Max(1f, Math.Min(2.2f, s)); } catch { return 1.2f; } }
}

// ── 오늘의 운세 · 사주 (재미로 보는 운세예요) ───────────────────────────────
internal static partial class Saju
{
	public static readonly string[] SK = { "갑", "을", "병", "정", "무", "기", "경", "신", "임", "계" }, SH = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
	public static readonly string[] BK = { "자", "축", "인", "묘", "진", "사", "오", "미", "신", "유", "술", "해" }, BH = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
	public static readonly string[] EN = { "목", "화", "토", "금", "수" }, EH = { "木", "火", "土", "金", "水" };
	public static int SE(int s) { return s / 2 == 0 ? 0 : (s / 2 == 1 ? 1 : (s / 2 == 2 ? 2 : (s / 2 == 3 ? 3 : 4))); }   // 갑을 목 · 병정 화 · 무기 토 · 경신 금 · 임계 수
	private static readonly int[] bElem = { 4, 2, 0, 0, 2, 1, 1, 2, 3, 3, 2, 4 };
	public static int BE(int b) { return bElem[b]; }
	public static readonly int[] BMain = { 9, 5, 0, 1, 4, 2, 3, 5, 6, 7, 4, 8 };   // 지지의 본기(속 천간): 자-계 축-기 인-갑 묘-을 진-무 사-병 오-정 미-기 신-경 유-신 술-무 해-임
	public static string Name(int idx) { return SK[idx % 10] + BK[idx % 12]; }
	public static string Hanja(int idx) { return SH[idx % 10] + BH[idx % 12]; }
	// 오행 관계: 0 비겁(같은 기운) 1 식상(내가 낳는) 2 재성(내가 극하는) 3 관성(나를 극하는) 4 인성(나를 낳는)
	public static int Rel(int me, int other) { int d = ((other - me) % 5 + 5) % 5; return d == 0 ? 0 : (d == 1 ? 1 : (d == 2 ? 2 : (d == 3 ? 3 : 4))); }
	private static readonly string[][] TG = { new[] { "비견", "겁재" }, new[] { "식신", "상관" }, new[] { "편재", "정재" }, new[] { "편관", "정관" }, new[] { "편인", "정인" } };
	public static string TenGod(int meStem, int otherStem) { return TG[Rel(SE(meStem), SE(otherStem))][(meStem % 2 == otherStem % 2) ? 0 : 1]; }
	private static readonly int[] LS = { 11, 6, 2, 9, 2, 9, 5, 0, 8, 3 };   // 12운성의 장생 지지: 갑-해 을-오 병·무-인 정·기-유 경-사 신-자 임-신 계-묘
	private static readonly string[] LN = { "장생", "목욕", "관대", "건록", "제왕", "쇠", "병", "사", "묘", "절", "태", "양" };
	public static string Stage(int stem, int branch) { int s0 = LS[stem]; int k = stem % 2 == 0 ? ((branch - s0) % 12 + 12) % 12 : ((s0 - branch) % 12 + 12) % 12; return LN[k]; }
	public static readonly string[] AnC = { "푸른", "푸른", "붉은", "붉은", "황금", "황금", "하얀", "하얀", "검은", "검은" }, AnA = { "쥐", "소", "호랑이", "토끼", "용", "뱀", "말", "양", "원숭이", "닭", "개", "돼지" };
	public static string Animal(int idx) { return AnC[idx % 10] + AnA[idx % 12]; }
	public static readonly string[] TGText = { "독립적이고 주관이 뚜렷한", "경쟁심과 승부욕이 강한", "느긋하고 재능과 표현이 풍부한", "자유롭고 말솜씨·창의가 뛰어난", "활동적이고 사람과 돈 복이 있는", "꼼꼼하고 안정을 추구하는", "도전적이고 책임감·추진력이 있는", "반듯하고 원칙을 지키는", "생각이 깊고 독창적이며 직관이 좋은", "배려 깊고 배움을 좋아하는" };
	public static int TGIndex(int meStem, int otherStem) { int r = Rel(SE(meStem), SE(otherStem)); return r * 2 + ((meStem % 2 == otherStem % 2) ? 0 : 1); }
	public static int Hash(string s) { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } return (int)(h & 0x7fffffff); }

	// ── 태양 황경으로 절기 시각을 직접 계산해요 (Meeus 저정밀식, 오차 약 ±15분) ──
	private static double Jd(DateTime utc) { return utc.ToOADate() + 2415018.5; }
	private static DateTime FromJd(double jd) { return DateTime.FromOADate(jd - 2415018.5); }
	public static double SunLon(double jd)
	{
		double T = (jd - 2451545.0) / 36525.0, rad = Math.PI / 180.0;
		double L0 = 280.46646 + T * (36000.76983 + T * 0.0003032), M = (357.52911 + T * (35999.05029 - 0.0001537 * T)) * rad;
		double C = (1.914602 - T * (0.004817 + 0.000014 * T)) * Math.Sin(M) + (0.019993 - 0.000101 * T) * Math.Sin(2 * M) + 0.000289 * Math.Sin(3 * M);
		double lon = L0 + C - 0.00569 - 0.00478 * Math.Sin((125.04 - 1934.136 * T) * rad); lon %= 360.0; if (lon < 0) lon += 360.0; return lon;
	}
	public static DateTime TermKst(double target, DateTime nearKst)
	{
		double jd = Jd(nearKst.AddHours(-9));
		for (int i = 0; i < 14; i++) { double d = target - SunLon(jd); d = ((d + 540.0) % 360.0) - 180.0; jd += d / 0.98565; if (Math.Abs(d) < 1e-5) break; }
		return FromJd(jd).AddHours(9);
	}
	private static int Combine(int stem, int br) { for (int i = 0; i < 60; i++) if (i % 10 == stem && i % 12 == br) return i; return 0; }
	public sealed class Pil { public int Y, M, D, H = -1; public int Month; public double WarnMin; public bool Fwd; public int StartAge; public int[] Dae = new int[8]; public DateTime NextJie, PrevJie; }
	// kst: 한국 표준시 기준 출생 순간. corr: 서울 경도 보정(−30분)을 일주·시주에 적용
	public static Pil Calc(DateTime kst, bool hourKnown, bool corr, int gender)
	{
		Pil p = new Pil(); double lon = SunLon(Jd(kst.AddHours(-9)));
		int Y = kst.Year; if (kst < TermKst(315, new DateTime(Y, 2, 4, 12, 0, 0))) Y--;
		p.Y = ((Y - 4) % 60 + 60) % 60;
		int m = (int)(((lon - 315 + 360) % 360) / 30) + 1; if (m > 12) m = 12; p.Month = m;
		p.M = Combine((p.Y % 10 % 5 * 2 + 2 + m - 1) % 10, (m + 1) % 12);
		DateTime eff = kst.AddMinutes(corr ? -30 : 0); DateTime dd = eff.AddHours(1).Date;   // 자시(23시)부터 다음 날 일주로 봐요
		p.D = (((54 + (int)(dd - new DateTime(2000, 1, 1)).TotalDays) % 60) + 60) % 60;       // 2000-01-01 = 무오일
		if (hourKnown) { int hb = ((eff.Hour + 1) / 2) % 12; p.H = Combine((p.D % 10 % 5 * 2 + hb) % 10, hb); }
		double low = (315 + 30 * (m - 1)) % 360, up = (315 + 30 * m) % 360; p.PrevJie = TermKst(low, kst); p.NextJie = TermKst(up, kst);
		p.WarnMin = Math.Min(Math.Abs((kst - p.PrevJie).TotalMinutes), Math.Abs((p.NextJie - kst).TotalMinutes));
		bool yang = p.Y % 2 == 0, male = gender == 0; p.Fwd = (yang && male) || (!yang && !male);
		double days = p.Fwd ? (p.NextJie - kst).TotalDays : (kst - p.PrevJie).TotalDays; p.StartAge = Math.Max(1, (int)Math.Round(days / 3.0));
		for (int k = 0; k < 8; k++) p.Dae[k] = (((p.M + (p.Fwd ? 1 : -1) * (k + 1)) % 60) + 60) % 60;
		return p;
	}
}

internal sealed partial class FortuneForm : Form
{
	private static FortuneForm inst;
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.WindowState = FormWindowState.Normal; inst.Activate(); return; } inst = new FortuneForm(); inst.Show(); try { string tl = TodayLine(); if (tl != null) FHist.Add("운세", "오늘", tl); Bond.Daily("fo"); } catch { } } catch (Exception ex) { Program.LogErr(ex); } }
	private readonly float sc = FUi.Scale(420, 790); private readonly RectangleF[] fR = new RectangleF[10]; private int pk = -1, pkScroll, pkHov = -1;
	private int cal, by = 1990, bm = 1, bd = 1, bh = -1, bmin, bg; private bool leap, corr = true; private string mbti = ""; private Saju.Pil pil; private DateTime solar;
	private static readonly string[] MB = { "ISTJ", "ISFJ", "INFJ", "INTJ", "ISTP", "ISFP", "INFP", "INTP", "ESTP", "ESFP", "ENFP", "ENTP", "ESTJ", "ESFJ", "ENFJ", "ENTJ" };
	private static readonly string[] MBT = { "꼼꼼한 원칙주의자", "다정한 수호자", "깊이 있는 조언가", "치밀한 전략가", "침착한 해결사", "조용한 예술가", "이상을 품은 몽상가", "탐구하는 사색가", "행동파 모험가", "분위기 메이커", "아이디어 스파크", "토론하는 발명가", "든든한 관리자", "챙겨 주는 사교가", "따뜻한 리더", "추진력 있는 지휘관" };
	private static readonly string[] ELine = { "성장과 추진력", "열정과 표현력", "안정감과 포용력", "결단력과 의리", "지혜와 유연함" };
	private static readonly string[][] MTip = { new[] { "오늘은 사람을 만날수록 기운이 올라요", "오늘은 혼자 조용히 충전할수록 좋아요" }, new[] { "눈앞의 현실적인 일부터 처리해요", "새 아이디어를 메모해 두면 쓸 데가 있어요" }, new[] { "판단은 논리로, 말은 부드럽게 해요", "마음 가는 쪽을 믿되 근거도 한 줄 챙겨요" }, new[] { "계획표대로 가면 마음이 편해요", "즉흥으로 움직이면 뜻밖의 행운이 와요" } };
	private static readonly int[] PCols = { 2, 1, 2, 5, 1, 6, 4, 7, 5, 10 };
	private string[] Opts(int i)
	{
		List<string> L = new List<string>();
		if (i == 0) { L.Add("양력"); L.Add("음력"); }
		else if (i == 2) { L.Add("남성"); L.Add("여성"); }
		else if (i == 3) { L.Add("모름"); L.AddRange(MB); }
		else if (i == 5) for (int y = DateTime.Now.Year; y >= 1930; y--) L.Add(y + "년");
		else if (i == 6) for (int m = 1; m <= 12; m++) L.Add(m + "월");
		else if (i == 7) { int dn = cal == 0 ? DateTime.DaysInMonth(by, bm) : 30; for (int d = 1; d <= dn; d++) L.Add(d + "일"); }
		else if (i == 8) { L.Add("시간 모름"); for (int h = 0; h < 24; h++) L.Add(h + "시"); }
		else if (i == 9) for (int m = 0; m < 60; m++) L.Add(m.ToString("00"));
		return L.ToArray();
	}
	private int Cur(int i)
	{
		switch (i) { case 0: return cal; case 2: return bg; case 3: return mbti == "" ? 0 : Array.IndexOf(MB, mbti) + 1; case 5: return DateTime.Now.Year - by; case 6: return bm - 1; case 7: return Math.Min(bd, cal == 0 ? DateTime.DaysInMonth(by, bm) : 30) - 1; case 8: return bh + 1; case 9: return bmin; }
		return -1;
	}
	private string FieldText(int i)
	{
		switch (i) { case 1: return "윤달"; case 4: return "시간 보정 −30분"; case 9: return bh < 0 ? "—" : bmin + "분"; case 8: return bh < 0 ? "시간 모름" : bh + "시"; case 3: return mbti == "" ? "MBTI" : mbti; }
		string[] o = Opts(i); int c = Cur(i); return c >= 0 && c < o.Length ? o[c] : "";
	}
	private void SetOpt(int i, int idx)
	{
		if (i == 0) cal = idx; else if (i == 2) bg = idx; else if (i == 3) mbti = idx <= 0 ? "" : MB[idx - 1]; else if (i == 5) by = DateTime.Now.Year - idx; else if (i == 6) bm = idx + 1; else if (i == 7) bd = idx + 1; else if (i == 8) bh = idx - 1; else if (i == 9) bmin = idx;
		bd = Math.Min(bd, cal == 0 ? DateTime.DaysInMonth(by, bm) : 30); Changed();
	}
	private void Changed() { Recalc(); if (cp == 1) pSet = true; try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(SavePath, cal + "|" + by + "|" + bm + "|" + bd + "|" + bh + "|" + bmin + "|" + bg + "|" + (leap ? 1 : 0) + "|" + (corr ? 1 : 0) + "|" + mbti); } catch { } }
	private void Load()
	{
		bool had = false; try { if (File.Exists(SavePath)) { had = true; string[] p = File.ReadAllText(SavePath).Trim().Split('|'); cal = int.Parse(p[0]); by = int.Parse(p[1]); bm = int.Parse(p[2]); bd = int.Parse(p[3]); bh = int.Parse(p[4]); bmin = int.Parse(p[5]); bg = int.Parse(p[6]); leap = p[7] == "1"; corr = p[8] == "1"; mbti = p.Length > 9 ? p[9] : ""; } } catch { }
		if (cal < 0 || cal > 1 || by < 1930 || by > DateTime.Now.Year || bm < 1 || bm > 12 || bd < 1 || bd > 31 || bh < -1 || bh > 23 || bmin < 0 || bmin > 59 || bg < 0 || bg > 1 || (mbti != "" && Array.IndexOf(MB, mbti) < 0)) { cal = 0; by = 1990; bm = 1; bd = 1; bh = -1; bmin = 0; bg = 0; leap = false; corr = true; mbti = ""; had = false; }
		if (cp == 1) { pSet = had; if (!had) { cal = 0; by = 1992; bm = 1; bd = 1; bh = -1; bmin = 0; leap = false; corr = true; mbti = ""; bg = 1 - gM; } }
	}
	private DateTime SolarDate() { return SolarOf(cal, by, bm, bd, leap); }
	private static DateTime SolarOf(int cal, int by, int bm, int bd, bool leap)
	{
		try
		{
			if (cal == 0) return new DateTime(by, bm, Math.Min(bd, DateTime.DaysInMonth(by, bm)));
			System.Globalization.KoreanLunisolarCalendar kc = new System.Globalization.KoreanLunisolarCalendar(); int L = kc.GetLeapMonth(by);
			int idx = (leap && L > 0 && bm == L - 1) ? L : ((L > 0 && bm >= L) ? bm + 1 : bm); return kc.ToDateTime(by, idx, Math.Min(bd, kc.GetDaysInMonth(by, idx)), 0, 0, 0, 0);
		}
		catch { return new DateTime(Math.Max(1930, Math.Min(by, 2049)), Math.Min(bm, 12), 1); }
	}
	private static string LunarText(DateTime d)
	{
		try { System.Globalization.KoreanLunisolarCalendar kc = new System.Globalization.KoreanLunisolarCalendar(); int ly = kc.GetYear(d), idx = kc.GetMonth(d), L = kc.GetLeapMonth(ly), dd = kc.GetDayOfMonth(d); bool lp = L > 0 && idx == L; int lm = (L > 0 && idx >= L) ? idx - 1 : idx; return "음력 " + ly + "." + lm.ToString("00") + "." + dd.ToString("00") + (lp ? " 윤달" : ""); }
		catch { return ""; }
	}
	private void Recalc() { solar = SolarDate(); DateTime t = solar.AddHours(bh >= 0 ? bh : 12).AddMinutes(bh >= 0 ? bmin : 0); pil = Saju.Calc(t, bh >= 0, corr, bg); if (cp == 0) { pM = pil; sM = solar; gM = bg; mM = mbti; } else { pP = pil; sP = solar; gP = bg; mP = mbti; } }
	private void SwitchProfile(int n) { if (n == cp) return; pk = -1; cp = n; Load(); Recalc(); }
	private const int PkRows = 8; private const float PkRh = 30f;
	private int PkVis(int i) { return Math.Min(PkRows, Opts(i).Length); }
	private RectangleF PkRect(int i) { float w = Math.Max(FW[i], 112f), x = Math.Min(fR[i].X, 412f - w), y = fR[i].Bottom + 3f; return new RectangleF(x, y, w, PkVis(i) * PkRh + 12f); }
	private int PkHit(int i, float x, float y)
	{
		RectangleF r = PkRect(i); if (!r.Contains(x, y)) return -2; if (y < r.Y + 6 || y >= r.Y + 6 + PkVis(i) * PkRh) return -1;
		int idx = (int)((y - r.Y - 6) / PkRh) + pkScroll; return idx >= 0 && idx < Opts(i).Length ? idx : -1;
	}
	private static readonly float[] FX = { 14, 82, 142, 208, 284, 14, 106, 172, 238, 318 }, FW = { 64, 56, 62, 72, 122, 88, 62, 62, 76, 60 };
	private void DrawMeCard(Graphics g)
	{
		RectangleF r = new RectangleF(14, 64, 392, 62); using (GraphicsPath gp = Gfx.Round(r, 14)) { using (SolidBrush b = new SolidBrush(PCard)) g.FillPath(b, gp); using (Pen p = new Pen(PRed, 1.4f)) g.DrawPath(p, gp); }
		int dm = pM.D % 10; FUi.Text(g, "나의 정보", FUi.F(9.5f, 1), PRed, new RectangleF(26, 68, 120, 20), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, sM.ToString("yyyy.MM.dd") + (gM == 0 ? " · 남성" : " · 여성") + (mM != "" ? " · " + mM : "") + " · 일간 " + Saju.SK[dm] + "(" + Saju.EN[Saju.SE(dm)] + ")", FUi.F(9.5f, 1), PInk, new RectangleF(26, 88, 372, 20), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "내 정보는 '오늘' 탭 등에서 바꿀 수 있어요", FUi.F(8f, 0), PMut, new RectangleF(26, 106, 372, 16), StringAlignment.Near, StringAlignment.Center, false);
	}
	private void DrawFields(Graphics g, float y1 = 64f, float y2 = 98f)
	{
		for (int i = 0; i < 10; i++)
		{
			fR[i] = (i == 1 && cal == 0) ? RectangleF.Empty : new RectangleF(FX[i], i < 5 ? y1 : y2, FW[i], 28); if (fR[i].IsEmpty) continue;
			bool tg = i == 1 || i == 4, on = tg ? (i == 1 ? leap : corr) : pk == i, dis = i == 9 && bh < 0;
			using (GraphicsPath gp = Gfx.Round(fR[i], 14)) { using (SolidBrush b = new SolidBrush(tg ? (on ? PRed : PCard) : (on ? PSoft : PCard))) g.FillPath(b, gp); using (Pen p = new Pen(on ? PRed : PLine, on ? 1.8f : 1f)) g.DrawPath(p, gp); }
			FUi.Text(g, FieldText(i), FUi.F(i == 4 ? 8.5f : 9f, 1), tg && on ? Color.White : (dis ? PMut : PInk), new RectangleF(fR[i].X + (tg ? 4 : 6), fR[i].Y, fR[i].Width - (tg ? 8 : 22), fR[i].Height), StringAlignment.Center, StringAlignment.Center, false);
			if (!tg) { float ax = fR[i].Right - 12, ay = fR[i].Y + 14; using (SolidBrush b = new SolidBrush(PRed)) g.FillPolygon(b, new[] { new PointF(ax - 4, ay - 2), new PointF(ax + 4, ay - 2), new PointF(ax, ay + 3) }); }
		}
	}
	private void DrawPicker(Graphics g)
	{
		if (pk < 0) return; string[] o = Opts(pk); RectangleF r = PkRect(pk); int cur = Cur(pk), vr = PkVis(pk); pkScroll = Math.Max(0, Math.Min(o.Length - vr, pkScroll));
		using (GraphicsPath gp = Gfx.Round(new RectangleF(r.X + 2, r.Y + 4, r.Width, r.Height), 12)) using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 60, 40, 20))) g.FillPath(b, gp);
		using (GraphicsPath gp = Gfx.Round(r, 12)) { using (SolidBrush b = new SolidBrush(Theme.Raised)) g.FillPath(b, gp); using (Pen p = new Pen(PRed, 1.8f)) g.DrawPath(p, gp); }
		for (int row = 0; row < vr; row++)
		{
			int idx = row + pkScroll; RectangleF cr = new RectangleF(r.X + 5, r.Y + 6 + row * PkRh + 1, r.Width - (o.Length > vr ? 14 : 10), PkRh - 2); bool sel = idx == cur, hv = idx == pkHov;
			using (GraphicsPath gp = Gfx.Round(cr, 9)) { if (sel || hv) using (SolidBrush b = new SolidBrush(sel ? PRed : Color.FromArgb(200, 238, 214, 176))) g.FillPath(b, gp); }
			FUi.Text(g, o[idx], FUi.F(9.5f, sel ? 1 : 0), sel ? Color.White : PInk, cr, StringAlignment.Center, StringAlignment.Center, false);
		}
		if (o.Length > vr) { float th = Math.Max(16f, (r.Height - 12) * vr / o.Length), ty = r.Y + 6 + (r.Height - 12 - th) * pkScroll / (o.Length - vr); using (GraphicsPath gp = Gfx.Round(new RectangleF(r.Right - 8, ty, 4, th), 2)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 170, 52, 44))) g.FillPath(b, gp); }
	}

	private int tab; private readonly RectangleF[] tabR = new RectangleF[9]; private static readonly string[] Tabs = { "오늘", "주", "달", "올해", "연도", "사주", "평생", "토정", "궁합" }; private static readonly float[] TabW = { 40, 32, 32, 40, 40, 40, 40, 40, 40 };
	private static string SavePath0 { get { return Path.Combine(Program.DataDir, "saju2.txt"); } }
	private string SavePath { get { return cp == 0 ? SavePath0 : Path.Combine(Program.DataDir, "saju2b.txt"); } }
	private int cp; private Saju.Pil pM, pP; private DateTime sM, sP; private int gM, gP; private string mM = "", mP = ""; private bool pSet;
	private static readonly Color[] EC = { Color.FromArgb(86, 168, 98), Color.FromArgb(224, 92, 80), Color.FromArgb(206, 160, 66), Color.FromArgb(150, 160, 184), Color.FromArgb(70, 120, 212) };
	private static readonly string[] ELuckColor = { "초록색", "빨간색", "노란색·갈색", "흰색·은색", "파란색·검정" }, EDir = { "동쪽", "남쪽", "가운데(제자리)", "서쪽", "북쪽" };
	private static readonly int[][] ENum = { new[] { 3, 8 }, new[] { 2, 7 }, new[] { 5, 10 }, new[] { 4, 9 }, new[] { 1, 6 } };
	private static readonly string[] RelName = { "비견 · 겁재 (나와 같은 기운)", "식신 · 상관 (재능을 펼치는 기운)", "편재 · 정재 (재물의 기운)", "편관 · 정관 (책임과 규칙의 기운)", "편인 · 정인 (도움과 배움의 기운)" };
	private static readonly string[] CatName = { "재물운", "애정운", "건강운", "일·학업운" };
	private static readonly int[][] CatBase = { new[] { 2, 3, 5, 2, 3 }, new[] { 3, 4, 4, 3, 3 }, new[] { 4, 3, 3, 2, 5 }, new[] { 3, 4, 2, 5, 4 } };
	private static readonly int[] RelBase = { 3, 4, 4, 3, 4 };
	// 기간별 한 줄 풀이 [기간][관계]
	private static readonly string[][] Head = {
		new[] { "오늘은 내 마음이 단단해지는 날이에요. 주관대로 밀고 가도 좋아요.", "하고 싶은 말·아이디어가 술술 나오는 날! 표현하면 점수를 따요.", "작은 이익과 기회가 눈에 띄는 날이에요. 지갑은 꽉 쥐고 기회는 놓치지 마세요.", "해야 할 일이 몰리는 날이에요. 규칙대로 차근차근 하면 칭찬이 와요.", "귀인의 도움이 닿는 날이에요. 모르는 건 물어보면 길이 열려요." },
		new[] { "이번 주는 주변과 보조를 맞추면 더 힘이 나요. 경쟁보다 협력!", "이번 주는 재능이 빛나요. 미뤄 둔 기획을 꺼내 보세요.", "이번 주는 돈과 일의 결실이 보여요. 꼼꼼한 정산이 복을 불러요.", "이번 주는 책임이 늘지만 인정받을 기회도 커요. 약속은 지켜요.", "이번 주는 배움과 휴식이 잘 맞아요. 몸과 마음을 채우는 한 주예요." },
		new[] { "이번 달은 자기 페이스가 중요해요. 비교하지 말고 내 길을 가요.", "이번 달은 아이디어와 말솜씨가 무기예요. 사람들 앞에 서 보세요.", "이번 달은 수입·성과 쪽 흐름이 좋아요. 계획적으로 쓰고 모으면 풍성해요.", "이번 달은 조직 안에서 역할이 커져요. 무리하지 말고 완급 조절을!", "이번 달은 도움을 받기 좋아요. 공부·자격·정리에 힘쓰면 보람이 커요." },
		new[] { "올해는 나다움을 단단히 세우는 해예요. 동료·친구와 힘을 합치면 큰 일도 해내요.", "올해는 재능을 꽃피우는 해예요. 새 도전과 표현에 운이 붙어요.", "올해는 재물과 성과가 따라오는 해예요. 큰 지출만 신중하게!", "올해는 승진·책임 등 큰 변화의 해예요. 바른 태도가 곧 운이에요.", "올해는 배움과 안정의 해예요. 귀인과 문서(계약·자격) 운이 좋아요." } };
	private static readonly string[] Advice = { "고집은 살짝만, 대화는 넉넉히!", "말을 뱉기 전에 한 번만 더 생각하면 완벽해요.", "쓰는 만큼 기록하면 돈이 새지 않아요.", "컨디션 관리가 곧 성과예요. 물 많이 마시기!", "고마운 사람에게 먼저 인사를 건네 보세요." };
	private static readonly string[][] CatTag = { new[] { "조심, 지갑 닫기", "아껴 쓰기", "무난해요", "작은 행운", "대박 기운!" }, new[] { "혼자가 편해요", "차분하게", "무난해요", "설레는 기운", "사랑이 넘쳐요" }, new[] { "푹 쉬어요", "무리 금지", "보통이에요", "활기차요", "컨디션 최고" }, new[] { "천천히 가요", "꾸준히", "무난해요", "성과가 보여요", "인정받아요" } };
	private static readonly string[] DMText = { "큰 나무처럼 곧고 정직한 성향이에요. 자존심이 강하고 리더십이 있어요.", "덩굴·풀처럼 유연하고 섬세해요. 적응력이 뛰어나고 사람을 잘 챙겨요.", "태양처럼 밝고 열정적이에요. 솔직하고 에너지가 넘쳐요.", "촛불처럼 따뜻하고 세심해요. 집중력과 배려심이 좋아요.", "산처럼 듬직하고 믿음직해요. 책임감이 강하고 중심이 잡혀 있어요.", "논밭의 흙처럼 포용력이 있어요. 현실적이고 꼼꼼해요.", "바위·쇠처럼 결단력이 있어요. 의리 있고 추진력이 강해요.", "보석처럼 예민하고 섬세해요. 깔끔하고 완벽을 추구해요.", "큰 강·바다처럼 생각이 넓어요. 지혜롭고 자유로운 성향이에요.", "비·이슬처럼 차분하고 영리해요. 감수성이 풍부하고 직관이 좋아요." };
	private static Color PInk { get { return Theme.Ink; } } private static Color PSub { get { return Theme.Sub; } } private static Color PMut { get { return Theme.Muted; } } private static Color PRed { get { return Theme.AccentBtn; } } private static Color PCard { get { return Theme.Raised; } } private static Color PLine { get { return Theme.LineStrong; } } private static Color PSoft { get { return Theme.AccentSoft; } } private static Color PPage { get { return Theme.Page; } } private static Color PBg { get { return Theme.Page; } }
	private static Bitmap paper;
	internal static Bitmap Paper(int w, int h)
	{
		if (paper != null) return paper; Random r = new Random(7); Bitmap bm0 = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
		using (Graphics g = Graphics.FromImage(bm0))
		{
			g.Clear(PBg);
			using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(-w * 0.2f, -h * 0.2f, w * 1.4f, h * 1.4f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(0, 255, 255, 255); pb.SurroundColors = new[] { Color.FromArgb(70, 150, 110, 60) }; g.FillRectangle(pb, 0, 0, w, h); } }
			for (int i = 0; i < 900; i++) { float x = r.Next(w), y = r.Next(h), l = 6 + r.Next(26), a = (float)(r.NextDouble() * 6.283); using (Pen p = new Pen(Color.FromArgb(10 + r.Next(26), r.Next(2) == 0 ? 255 : 150, r.Next(2) == 0 ? 250 : 120, 205 - r.Next(60)), 1f)) g.DrawLine(p, x, y, x + (float)Math.Cos(a) * l, y + (float)Math.Sin(a) * l); }
			for (int i = 0; i < 160; i++) using (SolidBrush br = new SolidBrush(Color.FromArgb(14 + r.Next(18), 120, 90, 50))) g.FillEllipse(br, r.Next(w), r.Next(h), 1 + r.Next(3), 1 + r.Next(3));
			using (Pen p = new Pen(Color.FromArgb(150, 120, 52, 40), 2f)) g.DrawRectangle(p, 6, 6, w - 13, h - 13); using (Pen p = new Pen(Color.FromArgb(90, 120, 52, 40), 1f)) g.DrawRectangle(p, 10, 10, w - 21, h - 21);
		}
		paper = bm0; return paper;
	}
	private FortuneForm()
	{
		Text = "오늘의 운세 · 사주"; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(420 * sc), (int)(790 * sc)); DoubleBuffered = true; Chrome.Attach(this);
		Load(); Recalc(); cp = 1; Load(); Recalc(); cp = 0; Load(); Recalc();
		MouseDown += delegate(object s, MouseEventArgs e)
		{
			float x = e.X / sc, y = e.Y / sc; if (hist && e.Button == MouseButtons.Left) { if (FHist.ChipHit(x, y) == 1) { hist = false; Invalidate(); } return; }
			if (pk < 0 && e.Button == MouseButtons.Left) { int hc = FHist.ChipHit(x, y); if (hc == 1) { hist = true; hScroll = 0; FHist.Refresh(); Invalidate(); return; } if (hc == 2) { FHist.SavePng(this); return; } }
			if (pk < 0 && e.Button == MouseButtons.Left) { int hb = Chrome.HubHit(x, y); if (hb == 2) { TarotForm.Open(); Close(); return; } if (hb == 1) return; } if (y < 56f && e.Button == MouseButtons.Left && pk < 0) { Chrome.Drag(this); return; }
			if (pk >= 0) { int h = PkHit(pk, x, y); if (h >= 0) { SetOpt(pk, h); pk = -1; Sfx.Play("tick"); } else if (h == -2) pk = -1; Invalidate(); return; }
			for (int i = 0; i < 10; i++) if (!fR[i].IsEmpty && fR[i].Contains(x, y))
			{
				if (i == 1) { leap = !leap; Changed(); Sfx.Play("tick"); Invalidate(); return; }
				if (i == 4) { corr = !corr; Changed(); Sfx.Play("tick"); Invalidate(); return; }
				if (i == 9 && bh < 0) return;
				pk = i; pkScroll = Math.Max(0, Math.Min(Opts(i).Length - PkVis(i), Cur(i) - PkVis(i) / 2)); pkHov = -1; Sfx.Play("tick"); Invalidate(); return;
			}
			if (YearClick(x, y)) return;
			for (int i = 0; i < 9; i++) if (tabR[i].Contains(x, y - 38)) { SwitchProfile(i == 8 ? 1 : 0); tab = i; ySc = 0; Invalidate(); }
		};
		MouseMove += delegate(object s, MouseEventArgs e) { if (pk < 0) return; int h = PkHit(pk, e.X / sc, e.Y / sc); if (h < 0) h = -1; if (h != pkHov) { pkHov = h; Invalidate(); } };
		MouseWheel += delegate(object s, MouseEventArgs e) { if (hist) { FHist.Wheel(ref hScroll, e.Delta, 790f, sc); Invalidate(); return; } if (pk < 0) { float mxy = MaxY(); if (mxy > 0) { ySc = Math.Max(0, Math.Min(mxy, ySc - e.Delta * 0.5f / sc)); Invalidate(); } return; } pkScroll = Math.Max(0, Math.Min(Opts(pk).Length - PkVis(pk), pkScroll + (e.Delta > 0 ? -2 : 2))); Invalidate(); };
		KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape && pk >= 0) { pk = -1; Invalidate(); e.Handled = true; } };
	}
	private int DM { get { return pil.D % 10; } }
	private static string Stars(int n) { return new string('●', n) + new string('○', 5 - n); }
	private void Star(Graphics g, float x, float y, int n)
	{
		for (int i = 0; i < 5; i++) { float cx = x + i * 22f + 9f, cy2 = y + 9f; if (i < n) using (SolidBrush b = new SolidBrush(Theme.Gold)) g.FillEllipse(b, cx - 7.5f, cy2 - 7.5f, 15f, 15f); else using (Pen p = new Pen(PLine, 1.8f)) g.DrawEllipse(p, cx - 6.5f, cy2 - 6.5f, 13f, 13f); }
	}
	private bool hist; private float hScroll;
	protected override void OnPaint(PaintEventArgs e)
	{
		PaintMain(e);
		if (hist) { Graphics g = e.Graphics; Gfx.HQ(g); g.ScaleTransform(sc, sc); FHist.DrawOverlay(g, 420f, 790f, hScroll); FHist.DrawChips(g, true, PRed, PCard, PSub, Color.White); Chrome.Hub(g, false, PRed, PCard, Color.White, PSub); }
	}
	private void PaintMain(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Chrome.Frame(g, ClientSize.Width, ClientSize.Height); Gfx.HQ(g); g.ScaleTransform(sc, sc); g.InterpolationMode = InterpolationMode.Bilinear; DateTime now = DateTime.Now;
		FUi.Text(g, "오늘의 운세 · 사주", FUi.F(15f, 1), PInk, new RectangleF(16, 10, 215, 28), StringAlignment.Near, StringAlignment.Center, false); Chrome.Hub(g, false, PRed, PCard, Color.White, PSub); FHist.DrawChips(g, false, PRed, PCard, PSub, Color.White);
		FUi.Text(g, (tab == 8 ? "상대방의 사주를 넣으면 궁합을 봐 드려요" : now.ToString("yyyy.MM.dd") + " · 재미로 보는 운세예요"), FUi.F(8.5f, 0), PMut, new RectangleF(16, 38, 215, 18), StringAlignment.Near, StringAlignment.Center, false);
		if (tab == 8) DrawMeCard(g); else DrawFields(g);
		g.TranslateTransform(0, 38f);
		float tx = 14f; for (int i = 0; i < 9; i++) { tabR[i] = new RectangleF(tx, 98, TabW[i], 28); using (GraphicsPath gp = Gfx.Round(tabR[i], 14)) using (SolidBrush b = new SolidBrush(i == tab ? PRed : PCard)) g.FillPath(b, gp); FUi.Text(g, Tabs[i], FUi.F(9f, 1), i == tab ? Color.White : PSub, tabR[i], StringAlignment.Center, StringAlignment.Center, false); tx += tabR[i].Width + 6f; }
		using (GraphicsPath gp = Gfx.Round(new RectangleF(12, 136, 396, 604), 16)) { using (SolidBrush b = new SolidBrush(PPage)) g.FillPath(b, gp); using (Pen p = new Pen(PLine, 1f)) g.DrawPath(p, gp); }
		int me = Saju.SE(DM); string who = "내 일간 " + Saju.SK[DM] + "(" + Saju.EN[me] + ")";
		if (tab < 4) contentH = 740f; GraphicsState gsC = g.Save(); g.SetClip(tab == 8 ? new RectangleF(12, 244, 396, 496) : new RectangleF(12, 136, 396, 604)); g.TranslateTransform(0, -Math.Min(ySc, MaxY()));
		try { if (tab == 7) DrawToji(g, now); else if (tab == 5) DrawSaju(g, me); else if (tab == 4) DrawNext(g, now); else if (tab == 6) DrawLife(g, now); else if (tab == 8) DrawMatch(g, now); else DrawReading(g, tab, me, who, now); } catch (Exception ex) { Program.LogErr(ex); }
		g.Restore(gsC); if (ySc > MaxY()) ySc = MaxY(); DrawYearArrows(g); DrawScrollbar(g);
		if (tab == 8) { g.ResetTransform(); g.ScaleTransform(sc, sc); g.TranslateTransform(0, 38f); FUi.Text(g, "상대방 사주 입력", FUi.F(11f, 1), PRed, new RectangleF(26, 142, 200, 22), StringAlignment.Near, StringAlignment.Center, false); FUi.Text(g, pSet ? "바꾸면 자동 저장돼요" : "생일과 성별을 골라 주세요", FUi.F(8.5f, 0), pSet ? PMut : PRed, new RectangleF(200, 142, 196, 22), StringAlignment.Far, StringAlignment.Center, false); g.ResetTransform(); g.ScaleTransform(sc, sc); DrawFields(g, 212f, 246f); g.ResetTransform(); g.ScaleTransform(sc, sc); g.TranslateTransform(0, 38f); }
		g.ResetTransform(); g.ScaleTransform(sc, sc); DrawPicker(g);
	}
	private void DrawReading(Graphics g, int period, int me, string who, DateTime now)
	{
		Rd R = ReadCalc(period, now, cal, by, bm, bd, bh, bmin, bg, mbti, pil); int rel = R.Rel; int[] sc = R.Sc; int total = R.Total; string pname = R.Pname, seed = R.Seed;
		float y = 150f; FUi.Text(g, pname, FUi.F(10f, 1), PSub, new RectangleF(26, y, 370, 20), StringAlignment.Near, StringAlignment.Center, false); y += 22f;
		FUi.Text(g, who + " · " + RelName[rel], FUi.F(8.5f, 0), PMut, new RectangleF(26, y, 370, 18), StringAlignment.Near, StringAlignment.Center, false); y += 26f;
		FUi.Text(g, "총운", FUi.F(11f, 1), PInk, new RectangleF(26, y, 60, 24), StringAlignment.Near, StringAlignment.Center, false); Star(g, 76, y + 1, total);
		FUi.Text(g, new[] { "조심조심", "차분하게", "무난한", "좋아요", "아주 좋아요!" }[total - 1], FUi.F(10f, 1), total >= 4 ? Theme.Green : (total <= 2 ? Theme.Red : PSub), new RectangleF(200, y, 190, 24), StringAlignment.Far, StringAlignment.Center, false); y += 38f;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(24, y, 372, 78), 12)) using (SolidBrush b = new SolidBrush(PSoft)) g.FillPath(b, gp);
		FUi.Text(g, Head[period][rel], FUi.F(9.5f, 1), PInk, new RectangleF(34, y + 6, 352, 42), StringAlignment.Near, StringAlignment.Near, true); FUi.Text(g, "▶ " + Advice[rel], FUi.F(9f, 0), PSub, new RectangleF(34, y + 52, 352, 20), StringAlignment.Near, StringAlignment.Center, false); y += 92f;
		for (int i = 0; i < 4; i++)
		{
			FUi.Text(g, CatName[i], FUi.F(9.5f, 1), PInk, new RectangleF(26, y, 80, 22), StringAlignment.Near, StringAlignment.Center, false);
			using (GraphicsPath gp = Gfx.Round(new RectangleF(108, y + 6, 170, 10), 5)) using (SolidBrush b = new SolidBrush(PLine)) g.FillPath(b, gp);
			using (GraphicsPath gp = Gfx.Round(new RectangleF(108, y + 6, 170f * sc[i] / 5f, 10), 5)) using (SolidBrush b = new SolidBrush(sc[i] >= 4 ? Theme.Green : (sc[i] <= 2 ? Theme.Amber : PRed))) g.FillPath(b, gp);
			FUi.Text(g, CatTag[i][sc[i] - 1], FUi.F(8.5f, 0), PSub, new RectangleF(284, y, 112, 22), StringAlignment.Far, StringAlignment.Center, false); y += 28f;
		}
		y += 8f; int lucky = Saju.Rel(me, 0) == 0 ? ((me + 4) % 5) : me;   // 나를 도와주는(낳아 주는) 기운
		FUi.Text(g, "행운 포인트", FUi.F(10f, 1), PInk, new RectangleF(26, y, 200, 20), StringAlignment.Near, StringAlignment.Center, false); y += 24f;
		int[] ns = ENum[lucky]; string[] hours = { "아침 7~9시", "오전 9~11시", "낮 11~13시", "오후 1~3시", "오후 3~5시", "저녁 5~7시" };
		int li = LuckItem(lucky, seed); RectangleF ib = new RectangleF(26, y - 2, 76, 76);
		using (GraphicsPath gp = Gfx.Round(ib, 14)) { using (SolidBrush b = new SolidBrush(PCard)) g.FillPath(b, gp); using (Pen p = new Pen(PLine, 1f)) g.DrawPath(p, gp); }
		LuckIcon(g, li, new RectangleF(ib.X + 6, ib.Y + 6, 64, 64), EC[lucky]);
		FUi.Text(g, "행운템 · " + LuckName[li], FUi.F(10f, 1), PInk, new RectangleF(112, y - 2, 284, 20), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, LuckTip[li], FUi.F(8.5f, 0), PSub, new RectangleF(112, y + 18, 284, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "색 " + ELuckColor[lucky] + " · 숫자 " + ns[0] + ", " + ns[1], FUi.F(8.5f, 0), PSub, new RectangleF(112, y + 38, 284, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "방향 " + EDir[lucky] + " · 좋은 시간 " + hours[Saju.Hash(seed + "h") % 6], FUi.F(8.5f, 0), PSub, new RectangleF(112, y + 56, 284, 18), StringAlignment.Near, StringAlignment.Center, false); y += 56f;
		FUi.Text(g, "※ 생년월일을 바꾸면 결과가 달라져요 · 재미로만 봐 주세요", FUi.F(8f, 0), PMut, new RectangleF(26, 662, 372, 22), StringAlignment.Near, StringAlignment.Center, false);
	}
	private static readonly string[] TSang = { "", "처음엔 더디지만 끝이 좋은 해예요. 서두르지 않으면 열매가 커요.", "귀인을 만나 일이 술술 풀리는 해예요.", "움직일수록 길이 열리는 해예요. 새 일에 도전해 보세요.", "지키는 힘이 필요한 해예요. 지출·약속을 단단히 관리해요.", "재물과 성과가 쌓이는 해예요. 작은 것부터 챙기면 크게 돌아와요.", "관계가 넓어지는 해예요. 사람 덕이 큰 만큼 말을 아끼면 복이 와요.", "변화의 바람이 부는 해예요. 이동·이직·이사 이야기가 나올 수 있어요.", "한 해를 마무리하고 다지는 해예요. 건강과 가정을 먼저 돌보세요." };
	private static readonly string[] TJung = { "", "일·학업에서는 꾸준함이 무기예요.", "금전은 들어오는 만큼 새기 쉬우니 기록하세요.", "사람 사이의 오해만 조심하면 순탄해요.", "건강 점검을 한 번 해 두면 든든해요.", "배움과 자격 쪽에 기회가 있어요.", "여행·외출에서 좋은 소식이 올 수 있어요." };
	private static readonly string[] THa = { "", "마음 가는 대로보다 계획대로 가세요.", "주변 의견을 한 번 더 들으면 실수가 줄어요.", "과감한 선택이 통하는 시기가 있어요." };
	private static readonly string[][] MPool = { null,
		new[] { "몸을 낮추고 때를 기다려요", "무리하면 탈이 나요", "작은 손해 주의, 서두르지 마요", "쉬어 가는 달이에요" },
		new[] { "차분히 지키면 무난해요", "소소한 걱정은 곧 사라져요", "한 걸음씩 천천히", "주변의 도움이 닿아요" },
		new[] { "평탄하고 안정적이에요", "꾸준함이 보상받아요", "뜻밖의 연락이 반가워요", "계획대로 진행돼요" },
		new[] { "좋은 소식이 들려와요", "일이 풀리고 귀인이 보여요", "작은 행운이 따라와요", "도전하기 좋은 달이에요" },
		new[] { "재물·성과가 활짝 피어요", "큰 기쁨이 찾아와요", "하는 일마다 순풍이에요", "인정받고 웃는 달이에요" } };
	private void DrawToji(Graphics g, DateTime now)
	{
		int yr = tY7, age = yr - solar.Year + 1; System.Globalization.KoreanLunisolarCalendar kc = new System.Globalization.KoreanLunisolarCalendar();
		int lm, ld, ly, dm; try { DateTime b = solar; ly = kc.GetYear(b); lm = kc.GetMonth(b); ld = kc.GetDayOfMonth(b); int leap = kc.GetLeapMonth(ly); if (leap > 0 && lm >= leap) lm--; dm = kc.GetDaysInMonth(ly, kc.GetMonth(b)); } catch { lm = bm; ld = bd; dm = 30; }
		int ti = Saju.Calc(new DateTime(yr, 6, 1, 12, 0, 0), false, true, 0).Y, tae = (ti % 10 + 1) + (ti % 12 + 1);
		int a = (age + tae) % 8; if (a == 0) a = 8; int c = (dm + lm) % 6; if (c == 0) c = 6; int hb = bh < 0 ? 0 : ((bh + 1) / 2) % 12 + 1; int d = (ld + hb) % 3; if (d == 0) d = 3;
		int no = (a - 1) * 18 + (c - 1) * 3 + d; tjA = a; tjC = c; tjD = d; tjNo = no; tjYr = yr;
		FUi.Text(g, yr + "년 토정비결", FUi.F(11f, 1), PInk, new RectangleF(26, 148, 250, 24), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "상괘 " + a + " · 중괘 " + c + " · 하괘 " + d + "   →   " + no + "번 괘 (" + age + "세)", FUi.F(9f, 0), PMut, new RectangleF(26, 172, 370, 20), StringAlignment.Near, StringAlignment.Center, false);
		using (GraphicsPath gp = Gfx.Round(new RectangleF(24, 198, 372, 100), 12)) using (SolidBrush br = new SolidBrush(PSoft)) g.FillPath(br, gp);
		FUi.Text(g, TSang[a], FUi.F(9.5f, 1), PInk, new RectangleF(34, 202, 352, 44), StringAlignment.Near, StringAlignment.Center, true);
		FUi.Text(g, TJung[c] + " " + THa[d], FUi.F(9f, 0), PSub, new RectangleF(34, 248, 352, 46), StringAlignment.Near, StringAlignment.Center, true);
		string seed = by + "-" + bm + "-" + bd + "-" + bh + "-" + bg + "-" + no + "-" + yr; tjSeed = seed; int best = 0, worst = 0, bs = -1, ws = 9;
		for (int m = 1; m <= 12; m++)
		{
			int lv = Math.Max(1, Math.Min(5, 3 + (Saju.Hash(seed + "m" + m) % 5) - 2 + (a >= 5 && m % 4 == 0 ? 1 : 0))); if (lv > bs) { bs = lv; best = m; } if (lv < ws) { ws = lv; worst = m; }
			int col = (m - 1) / 6, row = (m - 1) % 6; float x = 24f + col * 190f, y = 312f + row * 44f;
			using (GraphicsPath gp = Gfx.Round(new RectangleF(x, y, 182, 40), 8)) using (SolidBrush br = new SolidBrush(PCard)) g.FillPath(br, gp);
			FUi.Text(g, m + "월", FUi.F(9f, 1), PInk, new RectangleF(x + 8, y + 2, 40, 18), StringAlignment.Near, StringAlignment.Center, false);
			for (int k = 0; k < 5; k++) using (SolidBrush br = new SolidBrush(k < lv ? Theme.Gold : PLine)) g.FillEllipse(br, x + 48 + k * 13, y + 7, 9, 9);
			FUi.Text(g, MPool[lv][Saju.Hash(seed + "p" + m) % 4], FUi.F(8f, 0), PSub, new RectangleF(x + 8, y + 20, 170, 18), StringAlignment.Near, StringAlignment.Center, false);
		}
		FUi.Text(g, "가장 좋은 달 " + best + "월 · 조심할 달 " + worst + "월", FUi.F(9.5f, 1), PRed, new RectangleF(26, 580, 370, 20), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "▼ 스크롤하면 자세한 풀이가 나와요 · 음력 약식 풀이, 재미로만!", FUi.F(8f, 0), PMut, new RectangleF(26, 602, 372, 20), StringAlignment.Near, StringAlignment.Center, false);
		contentH = TojiDetail(g, 630f);
	}
	private string tgText = "";
	private void DrawSaju(Graphics g, int me)
	{
		int[] idx = { pil.H, pil.D, pil.M, pil.Y }; string[] lab = { "시주", "일주", "월주", "연주" }; int dm = pil.D % 10;
		FUi.Text(g, "나의 사주팔자", FUi.F(11f, 1), PInk, new RectangleF(26, 146, 200, 22), StringAlignment.Near, StringAlignment.Center, false);
		string lunar = cal == 0 ? LunarText(solar) : "양력 " + solar.ToString("yyyy.MM.dd"); string info = (cal == 0 ? "양력 " + solar.ToString("yyyy.MM.dd") : lunar.StartsWith("양력") ? lunar : "") ;
		string head = (cal == 0 ? "양력 " + solar.ToString("yyyy.MM.dd") + (lunar != "" ? " (" + lunar + ")" : "") : (leap ? "음력(윤달) " : "음력 ") + by + "." + bm.ToString("00") + "." + Math.Min(bd, 30).ToString("00") + " (양력 " + solar.ToString("yyyy.MM.dd") + ")") + (bh >= 0 ? " " + bh.ToString("00") + ":" + bmin.ToString("00") : " · 시간 모름") + " · " + (bg == 0 ? "남" : "여");
		FUi.Text(g, head, FUi.F(8.5f, 0), PMut, new RectangleF(26, 168, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		int[] cnt = new int[5];
		for (int i = 0; i < 4; i++)
		{
			float x = 24f + i * 94f; RectangleF r = new RectangleF(x, 190, 86, 186); bool dayc = i == 1;
			using (GraphicsPath gp = Gfx.Round(r, 12)) { using (SolidBrush br = new SolidBrush(PCard)) g.FillPath(br, gp); using (Pen p = new Pen(dayc ? PRed : PLine, dayc ? 2f : 1f)) g.DrawPath(p, gp); }
			FUi.Text(g, lab[i], FUi.F(9f, 1), dayc ? PRed : PSub, new RectangleF(x, 192, 86, 18), StringAlignment.Center, StringAlignment.Center, false);
			if (idx[i] < 0) { FUi.Text(g, "시간 모름", FUi.F(9f, 0), PMut, new RectangleF(x, 250, 86, 40), StringAlignment.Center, StringAlignment.Center, false); continue; }
			int s = idx[i] % 10, b = idx[i] % 12, se = Saju.SE(s), be = Saju.BE(b); cnt[se]++; cnt[be]++;
			FUi.Text(g, dayc ? "일간(나)" : Saju.TenGod(dm, s), FUi.F(8.5f, 1), dayc ? PRed : PSub, new RectangleF(x, 210, 86, 16), StringAlignment.Center, StringAlignment.Center, false);
			FUi.Text(g, Saju.SH[s], FUi.F(20f, 1), EC[se], new RectangleF(x, 226, 86, 34), StringAlignment.Center, StringAlignment.Center, false); FUi.Text(g, Saju.SK[s] + "(" + Saju.EN[se] + ")", FUi.F(8.5f, 0), PSub, new RectangleF(x, 260, 86, 16), StringAlignment.Center, StringAlignment.Center, false);
			FUi.Text(g, Saju.BH[b], FUi.F(20f, 1), EC[be], new RectangleF(x, 284, 86, 34), StringAlignment.Center, StringAlignment.Center, false); FUi.Text(g, Saju.BK[b] + "(" + Saju.EN[be] + ")", FUi.F(8.5f, 0), PSub, new RectangleF(x, 318, 86, 16), StringAlignment.Center, StringAlignment.Center, false);
			FUi.Text(g, Saju.TenGod(dm, Saju.BMain[b]), FUi.F(8.5f, 1), PSub, new RectangleF(x, 336, 86, 16), StringAlignment.Center, StringAlignment.Center, false);
			FUi.Text(g, Saju.Stage(dm, b), FUi.F(8.5f, 1), PRed, new RectangleF(x, 354, 86, 16), StringAlignment.Center, StringAlignment.Center, false);
		}
		{ int mb = pil.M % 12; int tgi = Saju.TGIndex(dm, Saju.BMain[mb]); FUi.Text(g, "일주 동물  " + Saju.Animal(pil.D) + "   ·   타고난 성향(월지)  " + Saju.TenGod(dm, Saju.BMain[mb]), FUi.F(9.5f, 1), PInk, new RectangleF(26, 382, 372, 18), StringAlignment.Near, StringAlignment.Center, false); tgText = Saju.TGText[tgi]; }
		g.TranslateTransform(0, 38f);
		// 오행 분포
		FUi.Text(g, "오행 분포", FUi.F(10.5f, 1), PInk, new RectangleF(26, 366, 200, 20), StringAlignment.Near, StringAlignment.Center, false);
		int mx = 0, mn = 0; for (int i = 0; i < 5; i++) { if (cnt[i] > cnt[mx]) mx = i; if (cnt[i] < cnt[mn]) mn = i; }
		for (int i = 0; i < 5; i++) { float y = 390f + i * 21f; FUi.Text(g, Saju.EH[i] + " " + Saju.EN[i], FUi.F(9f, 1), EC[i], new RectangleF(26, y, 56, 20), StringAlignment.Near, StringAlignment.Center, false); using (GraphicsPath gp = Gfx.Round(new RectangleF(84, y + 5, 130, 10), 5)) using (SolidBrush br = new SolidBrush(PLine)) g.FillPath(br, gp); if (cnt[i] > 0) using (GraphicsPath gp = Gfx.Round(new RectangleF(84, y + 5, 130f * cnt[i] / 8f, 10), 5)) using (SolidBrush br = new SolidBrush(EC[i])) g.FillPath(br, gp); FUi.Text(g, cnt[i] + "", FUi.F(9f, 0), PSub, new RectangleF(218, y, 20, 20), StringAlignment.Near, StringAlignment.Center, false); }
		// 신강/신약과 도움이 되는 오행 (간단한 근사)
		int[] ps = { pil.Y % 10, pil.M % 10, pil.H >= 0 ? pil.H % 10 : -1 }; int[] pb = { pil.Y % 12, pil.D % 12, pil.H >= 0 ? pil.H % 12 : -1 }; double sup = 0, tot = 0;
		foreach (int s in ps) if (s >= 0) { tot += 1; int e = Saju.SE(s); if (e == me || e == (me + 4) % 5) sup += 1; }
		foreach (int b in pb) if (b >= 0) { double w = b == pil.D % 12 ? 1.5 : 1; tot += w; int e = Saju.BE(b); if (e == me || e == (me + 4) % 5) sup += w; }
		{ tot += 3; int e = Saju.BE(pil.M % 12); if (e == me || e == (me + 4) % 5) sup += 3; }
		bool strong = sup / tot >= 0.5; int[] help = strong ? new[] { (me + 1) % 5, (me + 2) % 5, (me + 3) % 5 } : new[] { me, (me + 4) % 5 };
		using (GraphicsPath gp = Gfx.Round(new RectangleF(246, 388, 150, 112), 10)) using (SolidBrush br = new SolidBrush(PSoft)) g.FillPath(br, gp);
		FUi.Text(g, "일간의 힘: " + (strong ? "신강" : "신약") + " (" + Math.Round(sup / tot * 100) + "%)", FUi.F(9f, 1), PInk, new RectangleF(254, 392, 138, 18), StringAlignment.Near, StringAlignment.Center, false);
		string hs = ""; foreach (int e in help) hs += (hs == "" ? "" : "·") + Saju.EN[e]; FUi.Text(g, "도움 되는 오행: " + hs, FUi.F(8.5f, 0), PSub, new RectangleF(254, 412, 138, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, cnt[mn] == 0 ? Saju.EN[mn] + " 기운이 없어요" : Saju.EN[mn] + " 기운이 가장 적어요", FUi.F(8.5f, 0), PSub, new RectangleF(254, 432, 138, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, Saju.EN[mx] + " 기운이 가장 많아요", FUi.F(8.5f, 0), PSub, new RectangleF(254, 452, 138, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "※ 간단한 근사 계산", FUi.F(8f, 0), PMut, new RectangleF(254, 474, 138, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "나는 '" + Saju.SK[dm] + "' 일간(" + Saju.EN[me] + ")이에요", FUi.F(9.5f, 1), PInk, new RectangleF(26, 506, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, DMText[dm] + " 월지 기운은 " + tgText + " 성향이에요.", FUi.F(9f, 0), PSub, new RectangleF(26, 526, 372, 34), StringAlignment.Near, StringAlignment.Near, true);
		// 대운
		FUi.Text(g, "대운 (" + pil.StartAge + "세부터 " + (pil.Fwd ? "순행" : "역행") + ")", FUi.F(9.5f, 1), PInk, new RectangleF(26, 566, 300, 18), StringAlignment.Near, StringAlignment.Center, false);
		for (int k = 0; k < 8; k++)
		{
			float x = 24f + k * 46.5f; RectangleF r = new RectangleF(x, 586, 44, 40); int di = pil.Dae[k];
			using (GraphicsPath gp = Gfx.Round(r, 8)) { using (SolidBrush br = new SolidBrush(PCard)) g.FillPath(br, gp); using (Pen p = new Pen(PLine, 1f)) g.DrawPath(p, gp); }
			FUi.Text(g, (pil.StartAge + k * 10) + "세", FUi.F(8f, 0), PMut, new RectangleF(x, 588, 44, 14), StringAlignment.Center, StringAlignment.Center, false);
			FUi.Text(g, Saju.Name(di), FUi.F(10.5f, 1), EC[Saju.SE(di % 10)], new RectangleF(x, 603, 44, 20), StringAlignment.Center, StringAlignment.Center, false);
		}
		string mt = mbti == "" ? "MBTI를 고르면 사주와 함께 풀어 드려요 (위쪽 MBTI 칸)" : mbti + " (" + MBT[Array.IndexOf(MB, mbti)] + ") × " + Saju.EN[me] + " 일간 → " + ELine[me] + "이 돋보이는 조합이에요.";
		using (GraphicsPath gp = Gfx.Round(new RectangleF(24, 634, 372, 34), 10)) using (SolidBrush br = new SolidBrush(PSoft)) g.FillPath(br, gp);
		FUi.Text(g, mt, FUi.F(8.5f, 0), PInk, new RectangleF(32, 636, 358, 30), StringAlignment.Near, StringAlignment.Center, true);
		string warn = pil.WarnMin < 30 ? "절기 경계에서 " + (int)pil.WarnMin + "분 차이라 만세력과 다를 수 있어요" : "절기는 태양 위치로 계산했어요 (오차 ±15분)";
		FUi.Text(g, "※ " + warn + " · 재미로만 봐 주세요", FUi.F(8f, 0), PMut, new RectangleF(26, 672, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "▼ 스크롤하면 자세한 풀이가 나와요", FUi.F(8f, 1), PRed, new RectangleF(26, 690, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		contentH = SajuDetail(g, 722f, me) + 38f;
	}
}
