using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 운세 상세 풀이 (스크롤) ─────────────────────────────────────────────
internal partial class FortuneForm
{
	private float ySc, contentH = 740f; private int tY4 = DateTime.Now.Year + 1, tY7 = DateTime.Now.Year; private readonly RectangleF[] yR = new RectangleF[2];
	private static readonly string[] TGN = { "비견", "겁재", "식신", "상관", "편재", "정재", "편관", "정관", "편인", "정인" };
	private static readonly string[] CatN = { "애정", "재물", "출세", "건강" };

	private static float Est(string t, float pt, float w)
	{
		float cw = pt * 1.42f, line = 0, lim = w * 0.94f; int n = 1;
		foreach (char c in t) { if (c == '\n') { n++; line = 0; continue; } float cwid = c >= 0x1100 ? cw : cw * 0.55f; if (line + cwid > lim) { n++; line = 0; } line += cwid; }
		return n * pt * 1.8f;
	}
	private float SecHead(Graphics g, float y, string t, string sub)
	{
		FUi.Text(g, t, FUi.F(11f, 1), PRed, new RectangleF(26, y, 250, 22), StringAlignment.Near, StringAlignment.Center, false);
		if (sub != null) FUi.Text(g, sub, FUi.F(8.5f, 0), PMut, new RectangleF(200, y, 196, 22), StringAlignment.Far, StringAlignment.Center, false);
		return y + 26f;
	}
	private float Para(Graphics g, float y, string title, string body, bool hi, string tag)
	{
		float bh = Est(body, 9f, 346f), h = 30f + bh + 6f; if (y + h < 136f + ySc - 4f || y > 740f + ySc + 4f) return y + h + 6f;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(24, y, 372, h), 10)) { using (SolidBrush b = new SolidBrush(hi ? PSoft : PCard)) g.FillPath(b, gp); if (hi) using (Pen p = new Pen(PRed, 1.2f)) g.DrawPath(p, gp); }
		FUi.Text(g, title, FUi.F(9.5f, 1), hi ? PRed : PInk, new RectangleF(34, y + 4, 250, 20), StringAlignment.Near, StringAlignment.Center, false);
		if (tag != null) FUi.Text(g, tag, FUi.F(8.5f, 1), PSub, new RectangleF(250, y + 4, 138, 20), StringAlignment.Far, StringAlignment.Center, false);
		FUi.Text(g, body, FUi.F(9f, 0), PInk, new RectangleF(34, y + 26, 352, bh + 4), StringAlignment.Near, StringAlignment.Near, true);
		return y + h + 6f;
	}
	private static string StarS(int n) { n = Math.Max(0, Math.Min(5, n)); return new string('●', n) + new string('○', 5 - n); }
	private int HelpEl(int me, bool strong) { return strong ? (me + 2) % 5 : me; }

	// ── 공용 텍스트 ──
	private static readonly string[] YrTG = {
		"동료·친구와 함께하는 해예요. 협력은 힘이 되지만 돈 거래나 동업은 선을 지켜요.",
		"경쟁과 지출이 늘어나는 해예요. 승부욕은 실력으로 풀고 보증·충동구매는 피하세요.",
		"재능과 여유가 피어나는 해예요. 먹고 즐기고 표현할수록 복이 붙어요.",
		"표현력과 변화욕이 커지는 해예요. 새 도전엔 좋지만 윗사람에게 말실수를 조심해요.",
		"큰 돈과 활동 무대가 열리는 해예요. 기회는 많지만 들쭉날쭉하니 한도를 정하고 움직이세요.",
		"꾸준한 수입과 안정이 쌓이는 해예요. 성실한 저축과 계약이 결실을 맺어요.",
		"책임과 압박이 커지는 해예요. 도전적인 일을 맡을 수 있지만 건강과 시비를 관리하세요.",
		"인정과 승진, 명예의 기운이 오는 해예요. 규칙과 예의를 지키면 길해요.",
		"공부·연구·특수한 기술에 끌리는 해예요. 생각이 많아지니 결정은 천천히 하세요.",
		"귀인과 배움의 기운이 오는 해예요. 자격증·학업·문서 운이 좋아요." };
	private static readonly string[] YrTG2 = {
		"현실에서는 사람 관계가 바빠져요.", "현실에서는 쓰임새와 경쟁이 커져요.", "현실에서는 일상이 편안하고 풍성해져요.", "현실에서는 말과 변화가 잦아져요.", "현실에서는 돈과 일의 규모가 커져요.",
		"현실에서는 살림과 수입이 안정돼요.", "현실에서는 일의 부담이 늘어요.", "현실에서는 직장·조직 운이 받쳐 줘요.", "현실에서는 혼자 생각하는 시간이 늘어요.", "현실에서는 도움 주는 사람이 나타나요." };
	private static readonly string[] YrTot = { "", "조심하며 내실을 다지는 해예요. 크게 벌이기보다 지키는 쪽이 이득이에요.", "차분하게 준비하는 해예요. 급한 결정을 미루고 기본기를 쌓으면 다음이 편해져요.", "큰 파도 없이 무난하게 흘러가는 해예요. 꾸준함이 가장 큰 무기예요.", "흐름이 받쳐 주는 좋은 해예요. 미뤄 둔 도전을 시작하기에 알맞아요.", "기운이 활짝 열리는 해예요. 자신 있게 움직이되 욕심과 과속만 조심하세요." };
	private static readonly string[] LoveDo = { "", "연락과 약속은 한 번 더 확인하고, 서운함은 쌓지 말고 그날 풀어요.", "새 만남보다 내 취미와 외모를 가꾸는 데 시간을 쓰면 좋아요.", "만남이 많지 않아도 기존 관계를 정성껏 돌보면 안정적이에요.", "모임·소개에 적극적으로 나가 보세요. 인연이 닿는 해예요.", "고백·프러포즈·결혼 이야기를 꺼내기 좋아요. 마음을 표현하세요." };
	private static readonly string[] WealthDo = { "", "보증·큰 투자는 피하고 고정 지출을 줄여요. 비상금을 먼저 만드세요.", "가계부로 새는 돈부터 막아요. 충동구매는 하루 미루기!", "수입과 지출이 균형을 이뤄요. 적금·연금 같은 꾸준한 방식이 맞아요.", "부수입·성과급 기회가 있어요. 올라간 수입의 일부는 바로 저축해요.", "재물이 붙는 해예요. 계약·사업 확장을 검토하되 분산 투자로 지켜요." };
	private static readonly string[] CareerDo = { "", "책임은 늘고 인정은 더뎌요. 성과를 기록으로 남기고 묵묵히 버텨요.", "승부보다 실력 쌓기가 이득이에요. 자격증·교육에 투자하세요.", "맡은 일을 꾸준히 하면 평가가 따라와요. 보고와 소통을 성실히!", "승진·이직·새 프로젝트에 도전해 볼 만해요. 존재감을 드러내세요.", "인정과 명예가 따르는 해예요. 중요한 발표·면접·시험에 적극 나서요." };
	private static readonly string[] HealthDo = { "", "건강검진을 꼭 받고 수면·식사 리듬부터 바로잡아요. 과로는 금물!", "무리하면 탈이 나요. 가벼운 걷기와 스트레칭을 일과에 넣으세요.", "큰 문제는 없어요. 지금의 생활 습관을 유지하면 충분해요.", "컨디션이 좋은 편이에요. 운동 루틴을 만들기 좋은 해예요.", "활력이 넘쳐요. 다만 과신해서 무리하면 한 번에 무너질 수 있어요." };
	private static readonly string[][] MoTip = { null,
		new[] { "무리하지 말고 쉬어 가요. 큰 결정은 다음 달로!", "말과 지출을 줄이고 몸을 챙기세요.", "예민해지기 쉬워요. 약속을 줄이고 정리에 집중해요." },
		new[] { "차분히 준비하는 달이에요. 공부와 정리에 좋아요.", "급하게 서두르지 말고 기본기를 다져요.", "작은 성과를 모으는 달이에요. 비교는 잠시 내려놓아요." },
		new[] { "무난하게 흘러가요. 루틴을 지키면 충분해요.", "큰 변화 없이 안정적이에요. 밀린 일을 처리해요.", "사람을 만나면 좋은 소식이 있을 수 있어요." },
		new[] { "일이 술술 풀려요. 미뤄 둔 일을 시작해 보세요.", "귀인과 연결되기 쉬운 달이에요. 먼저 연락해 보세요.", "성과가 눈에 보여요. 자신 있게 의견을 내세요." },
		new[] { "가장 좋은 달이에요. 중요한 일정을 이 달에 몰아요!", "기회가 겹쳐 와요. 과속만 조심하면 큰 결실이에요.", "마음먹은 일을 실행하세요. 운이 등을 밀어 줘요." } };
	private static readonly string[] QName = { "1분기 (1~3월)", "2분기 (4~6월)", "3분기 (7~9월)", "4분기 (10~12월)" };
	private static readonly string[] QTx = { "", "한 걸음씩 신중하게 가야 하는 시기예요.", "준비하고 정비하는 시기예요.", "큰 변동 없이 안정적인 시기예요.", "성과가 붙는 활기찬 시기예요.", "한 해 중 가장 힘이 실리는 시기예요." };
	private static readonly string[] EDir2 = { "동쪽", "남쪽", "중앙·집 가까운 곳", "서쪽", "북쪽" };
	private static readonly string[] EFood = { "푸른 채소·신맛 나는 과일", "쓴 나물·붉은 음식", "곡물·뿌리채소·단호박", "흰 살 생선·도라지·배", "해조류·검은콩·견과류" };
	private static readonly string[] ENum2 = { "3·8", "2·7", "5·10", "4·9", "1·6" };

	// ── 연도별 상세 ──
	private float YearDetail(Graphics g, float y, int yr, int idx, Saju.LuckR r)
	{
		int dm = pil.D % 10, me = Saju.SE(dm); int g1 = Array.IndexOf(TGN, r.G1), g2 = Array.IndexOf(TGN, r.G2);
		y = SecHead(g, y, yr + "년 자세한 풀이", null);
		y = Para(g, y, "한 해 총평 · " + StarS(r.Total), YrTot[r.Total] + "\n\n▶ 천간(" + r.G1 + "): " + (g1 >= 0 ? YrTG[g1] : "") + "\n▶ 지지(" + r.G2 + "): " + (g2 >= 0 ? YrTG2[g2] : "") + (r.Strong ? "\n사주가 신강해서 힘이 넘치는 만큼 밀어붙이기보다 나누고 베푸는 쪽이 길해요." : "\n사주가 신약해서 무리하면 쉽게 지쳐요. 도움을 받는 선택이 길해요."), true, null);
		string[][] T = { LoveT[r.S[0]] == null ? null : LoveT[r.S[0]], WealthTx[r.S[1]], CareerTx[r.S[2]], HealthTx[r.S[3]] }; string[][] D = { LoveDo, WealthDo, CareerDo, HealthDo };
		y = SecHead(g, y + 4, "분야별 풀이", null);
		for (int c = 0; c < 4; c++)
		{
			string b = string.Join(" ", T[c]) + "\n▶ 이렇게 해보세요: " + D[c][r.S[c]]; if (c == 3) b += "\n▶ 신경 쓸 곳: " + r.Organs;
			if (c == 0) foreach (string n in r.Notes) if (n.Contains("일지") || n.Contains("도화") || n.Contains("천간이")) b += "\n● " + n;
			y = Para(g, y, CatN[c] + "운", b, false, StarS(r.S[c]));
		}
		y = SecHead(g, y + 4, "분기별 흐름", null);
		int[] mt = new int[13]; for (int m = 1; m <= 12; m++) mt[m] = Saju.Luck(pil, bg, Saju.Calc(new DateTime(yr, m, 15, 12, 0, 0), false, true, 0).M).Total;
		for (int q = 0; q < 4; q++) { int s = 0, bm0 = q * 3 + 1; for (int k = 0; k < 3; k++) { s += mt[q * 3 + 1 + k]; if (mt[q * 3 + 1 + k] > mt[bm0]) bm0 = q * 3 + 1 + k; } int av = (int)Math.Round(s / 3.0); y = Para(g, y, QName[q], QTx[av] + " 이 분기에서는 " + bm0 + "월이 가장 힘이 실려요.", false, StarS(av)); }
		y = SecHead(g, y + 4, "월별 자세히", "월주 기준");
		for (int m = 1; m <= 12; m++)
		{
			int mi = Saju.Calc(new DateTime(yr, m, 15, 12, 0, 0), false, true, 0).M; Saju.LuckR mr = Saju.Luck(pil, bg, mi); int bc = 0, wc = 0; for (int c = 1; c < 4; c++) { if (mr.S[c] > mr.S[bc]) bc = c; if (mr.S[c] < mr.S[wc]) wc = c; }
			string b = MoTip[mr.Total][Saju.Hash(yr + "-" + m + "-" + pil.D) % 3] + "\n▶ 강점 " + CatN[bc] + "운 · 주의 " + CatN[wc] + "운" + (mr.S[wc] <= 2 ? " (조심!)" : "");
			y = Para(g, y, m + "월 · " + Saju.Name(mi) + "월", b, false, StarS(mr.Total));
		}
		int help = HelpEl(me, r.Strong);
		y = SecHead(g, y + 4, "개운 팁", null);
		y = Para(g, y, "기운을 북돋우는 법 · " + Saju.EN[help] + " 기운", "행운색 " + ELuckColor[help] + " · 행운 방향 " + EDir2[help] + " · 행운 숫자 " + ENum2[help] + "\n음식: " + EFood[help] + "\n이 색의 소품을 지니고 이 방향으로 산책하면 마음이 한결 편해져요. 재미로 즐기는 가벼운 팁이에요.", false, null);
		return y + 8f;
	}


	// 평생 대운 점수: 사람마다 기복이 뚜렷하게 보이도록 본인 안에서 상대 비교로 늘려 줘요
	private List<Saju.LuckR> LifeLuck()
	{
		List<Saju.LuckR> L = new List<Saju.LuckR>(); if (pil.StartAge > 1) L.Add(Saju.Luck(pil, bg, pil.M)); for (int k = 0; k < 8; k++) L.Add(Saju.Luck(pil, bg, pil.Dae[k]));
		for (int c = 0; c < 5; c++)
		{
			int mn = 9, mx = 0; foreach (Saju.LuckR r in L) { int v = c < 4 ? r.S[c] : r.Total; mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
			if (mx - mn < 1) continue;
			foreach (Saju.LuckR r in L) { int v = c < 4 ? r.S[c] : r.Total; double st = 1 + 4.0 * (v - mn) / (mx - mn); int nv = Math.Max(1, Math.Min(5, (int)Math.Round(0.65 * st + 0.35 * v))); if (c < 4) r.S[c] = nv; else r.Total = nv; }
		}
		return L;
	}

	// ── 평생 사주(대운) 상세 ──
	private float LifeDetail(Graphics g, float y)
	{
		int dm = pil.D % 10, me = Saju.SE(dm); y = SecHead(g, y, "대운별 자세한 풀이", "10년 단위 흐름");
		DateTime now = DateTime.Now; int age = now.Year - solar.Year + 1; List<Saju.LuckR> LLd = LifeLuck();
		for (int k = 0; k < 8; k++)
		{
			int di = pil.Dae[k]; Saju.LuckR r = LLd[(pil.StartAge > 1 ? 1 : 0) + k]; int a0 = pil.StartAge + k * 10, a1 = a0 + 9; int g1 = Array.IndexOf(TGN, r.G1); bool cur = age >= a0 && age <= a1;
			string b = Dsc[Saju.Rel(me, Saju.SE(di % 10))] + " · 천간 " + r.G1 + " / 지지 " + r.G2 + "\n" + (g1 >= 0 ? YrTG[g1].Replace("해예요", "시기예요").Replace("해에", "시기에") : "") + "\n▶ 애정 " + StarS(r.S[0]) + "  재물 " + StarS(r.S[1]) + "\n▶ 출세 " + StarS(r.S[2]) + "  건강 " + StarS(r.S[3]);
			if (k == 0 && a0 > 0) b = "(그 이전은 어린 시절 기운이에요)\n" + b;
			y = Para(g, y, (cur ? "▶ 지금 · " : "") + a0 + "~" + a1 + "세 · " + Saju.Name(di) + " 대운", b, cur, StarS(r.Total));
		}
		int mb = pil.M % 12; int tgi = Saju.TGIndex(dm, Saju.BMain[mb]);
		y = SecHead(g, y + 4, "평생 직업·재물 스타일", null);
		y = Para(g, y, "직업 적성 (월지 " + Saju.TenGod(dm, Saju.BMain[mb]) + ")", "어울리는 일: " + TGJob[tgi] + "\n" + TG10[tgi], false, null);
		return y + 8f;
	}

	// ── 사주 원국 상세 ──
	private static readonly string[] TG10 = {
		"비견: 자립심과 고집이 강하고 친구·동료와 어울려 힘을 얻는 기운이에요.",
		"겁재: 경쟁심과 승부욕이 강하고 재물이 들고나는 기복이 있는 기운이에요.",
		"식신: 여유롭고 재능이 많으며 먹고 즐기는 복이 있는 기운이에요.",
		"상관: 재치와 표현력이 뛰어나고 틀을 깨는 아이디어가 많은 기운이에요.",
		"편재: 활동 무대가 넓고 돈을 크게 굴리는 사업가형 기운이에요.",
		"정재: 성실하고 꼼꼼하게 재산을 쌓는 안정형 기운이에요.",
		"편관: 추진력과 카리스마, 책임감이 강하고 압박 속에서 빛나는 기운이에요.",
		"정관: 반듯하고 원칙을 지키며 조직 속에서 인정받는 기운이에요.",
		"편인: 직관과 독특한 관심사가 있고 연구·예술·기술에 끌리는 기운이에요.",
		"정인: 배움과 어른 복이 있고 따뜻하며 학문에 강한 기운이에요." };
	private static readonly string[] SpPalace = {
		"배우자궁이 비견이라 친구 같은 동등한 관계를 원해요. 서로 독립을 존중하면 오래가요.",
		"배우자궁이 겁재라 열정적이지만 주도권 다툼이 생기기 쉬워요. 한 박자 양보가 비결이에요.",
		"배우자궁이 식신이라 편안하고 따뜻한 가정을 이루기 쉬워요. 함께 먹고 즐기는 시간이 약이에요.",
		"배우자궁이 상관이라 매력적이고 말이 통하는 상대에게 끌려요. 말투만 부드러우면 좋아요.",
		"배우자궁이 편재라 활동적이고 사교적인 상대와 인연이 있어요. 바쁜 만큼 대화 시간을 지키세요.",
		"배우자궁이 정재라 성실하고 살림 잘하는 배우자 인연이에요. 현실적인 안정이 큰 복이에요.",
		"배우자궁이 편관이라 카리스마 있는 상대에게 끌리고 긴장감도 따라요. 서로 존중이 핵심이에요.",
		"배우자궁이 정관이라 반듯하고 믿음직한 상대와 인연이 있어요. 늦더라도 안정적으로 맺어져요.",
		"배우자궁이 편인이라 개성 강한 상대와 인연이 있고 마음속 이야기를 잘 안 꺼낼 수 있어요.",
		"배우자궁이 정인이라 나를 챙겨 주는 따뜻한 상대와 인연이 있어요. 의지하는 만큼 베풀어요." };
	private static readonly string[] ELack = {
		"목(나무) 기운이 약해요. 초록 식물을 가까이 두고 아침 산책과 스트레칭, 새로운 배움으로 채워요.",
		"화(불) 기운이 약해요. 밝은 조명과 햇볕, 사람 만나기·표현하기, 붉은 소품으로 열정을 채워요.",
		"토(흙) 기운이 약해요. 규칙적인 식사와 생활 리듬, 노란·갈색 소품, 흙을 만지는 취미가 도움이 돼요.",
		"금(쇠) 기운이 약해요. 정리정돈과 마무리 습관, 흰색·은색 소품, 결단이 필요한 일을 연습해요.",
		"수(물) 기운이 약해요. 충분한 수분과 숙면, 명상·독서, 검정·파랑 소품으로 차분함을 채워요." };
	private static readonly string[] EOver = {
		"목 기운이 많아 추진력은 좋지만 고집과 스트레스가 쌓이기 쉬워요.", "화 기운이 많아 열정적이나 성급하고 쉽게 달아올라요.", "토 기운이 많아 듬직하지만 변화가 더디고 걱정이 많을 수 있어요.", "금 기운이 많아 원칙적이고 날카로우며 냉정해 보일 수 있어요.", "수 기운이 많아 생각이 깊지만 걱정과 우유부단함이 늘 수 있어요." };
	private static readonly string[] StageTx = {
		"장생: 시작과 성장의 기운. 배우면 쑥쑥 커요.", "목욕: 감수성과 매력이 풍부하고 변화가 잦은 기운.", "관대: 자신감 넘치고 사회에 나설 준비가 된 기운.", "건록: 독립적이고 자기 힘으로 먹고사는 든든한 기운.", "제왕: 기운이 절정. 리더십이 강하지만 고집도 세요.", "쇠: 한 걸음 물러나 지키는 안정의 기운.",
		"병: 섬세하고 감성적이며 몸을 챙겨야 하는 기운.", "사: 깊이 생각하고 정리하는 철학적인 기운.", "묘: 저장하고 모으는 기운. 실속 있고 신중해요.", "절: 끊고 새로 시작하는 기운. 변화에 강해요.", "태: 새 가능성이 잉태되는 기운. 아이디어가 많아요.", "양: 보살핌 속에 자라는 기운. 어른 복이 있어요." };
	private static readonly string[] DMLove = {
		"곧고 정직해서 한번 마음을 주면 변치 않아요. 표현이 서툴러 오해받을 수 있어요.", "부드럽고 눈치가 빨라 사랑받지만 속마음을 숨기는 편이에요.", "밝고 적극적이라 연애에서 분위기를 이끌어요. 쉽게 달아오르고 식을 수 있어요.", "다정하고 섬세해 상대를 깊이 배려해요. 서운함을 오래 품지 않도록 해요.", "묵직하고 믿음직한 연인이에요. 낭만 표현이 부족하니 작은 이벤트를 챙겨요.",
		"포근하고 알뜰하게 챙기는 스타일이에요. 속으로 참다가 폭발하지 않게 말로 풀어요.", "의리 있고 결단력 있는 연인이에요. 직설적인 말투가 상처가 될 수 있어요.", "예민하고 완벽을 추구해 상대에게도 기준이 높아요. 여유를 조금만 더 가져요.", "자유롭고 포용력이 커서 사람이 모여요. 한 곳에 매이는 걸 답답해할 수 있어요.", "감성적이고 헌신적이에요. 상대에게 기대를 걸다 상처받지 않게 마음을 단단히 해요." };

	private float SajuDetail(Graphics g, float y, int me)
	{
		int dm = pil.D % 10; int[] cnt = new int[5];
		int[] idx = { pil.Y, pil.M, pil.D, pil.H }; foreach (int ix in idx) if (ix >= 0) { cnt[Saju.SE(ix % 10)]++; cnt[Saju.BE(ix % 12)]++; }
		int mn = 0, mx = 0; for (int i = 1; i < 5; i++) { if (cnt[i] < cnt[mn]) mn = i; if (cnt[i] > cnt[mx]) mx = i; }
		y = SecHead(g, y, "사주 자세한 풀이", null);
		y = Para(g, y, "나의 중심 · " + Saju.SK[dm] + " 일간 (" + Saju.EN[me] + ")", DMText[dm] + "\n▶ 연애 스타일: " + DMLove[dm], true, null);
		int db = pil.D % 12; int dgi = Saju.TGIndex(dm, Saju.BMain[db]);
		y = Para(g, y, "배우자궁 · 일지 " + Saju.BK[db], SpPalace[dgi] + "\n▶ 일지 12운성 " + StageTx[Array.IndexOf(new[] { "장생", "목욕", "관대", "건록", "제왕", "쇠", "병", "사", "묘", "절", "태", "양" }, Saju.Stage(dm, db))], false, null);
		string[] pn = { "연주 (초년·가문)", "월주 (사회·직업)", "시주 (말년·자녀)" }; int[] pi = { pil.Y, pil.M, pil.H }; string[] pl = { "어린 시절과 집안 분위기, 조상 복을 보여줘요.", "사회생활과 직업, 타고난 환경을 보여줘요. 사주에서 가장 힘이 큰 자리예요.", "말년과 자녀, 내가 꿈꾸는 미래를 보여줘요." };
		y = SecHead(g, y + 4, "기둥별 의미", null);
		for (int k = 0; k < 3; k++)
		{
			if (pi[k] < 0) { y = Para(g, y, pn[k], "태어난 시각을 모르면 풀 수 없어요. 위쪽에서 시간을 골라 주세요.", false, null); continue; }
			int t1 = Saju.TGIndex(dm, pi[k] % 10), t2 = Saju.TGIndex(dm, Saju.BMain[pi[k] % 12]);
			y = Para(g, y, pn[k] + " · " + Saju.Name(pi[k]), pl[k] + "\n▶ 겉으로 드러나는 힘(천간 " + Saju.TenGod(dm, pi[k] % 10) + "): " + TG10[t1] + "\n▶ 속에 담긴 힘(지지 " + Saju.TenGod(dm, Saju.BMain[pi[k] % 12]) + "): " + TG10[t2], false, null);
		}
		y = SecHead(g, y + 4, "오행 균형과 보완", null);
		y = Para(g, y, "부족한 기운 · " + Saju.EN[mn] + (cnt[mn] == 0 ? " (없음)" : ""), ELack[mn] + "\n▶ 행운색 " + ELuckColor[mn] + " · 방향 " + EDir2[mn] + " · 음식 " + EFood[mn], false, null);
		y = Para(g, y, "넘치는 기운 · " + Saju.EN[mx] + " (" + cnt[mx] + "개)", EOver[mx] + "\n▶ 몸에서 신경 쓸 곳: " + Saju.Organ[mx] + " (과다) / " + Saju.Organ[mn] + " (부족)", false, null);
		int mb = pil.M % 12; int tgi = Saju.TGIndex(dm, Saju.BMain[mb]);
		y = SecHead(g, y + 4, "직업과 재물", null);
		y = Para(g, y, "어울리는 일", TGJob[tgi] + "\n" + TG10[tgi], false, null);
		y = Para(g, y, "인생 활용법", "원국에서 가장 많은 기운은 " + Saju.EN[mx] + ", 가장 약한 기운은 " + Saju.EN[mn] + "이에요. 강점은 살리고 약점은 생활 습관으로 채워 주면 균형이 잡혀요. 더 자세한 시기별 흐름은 '평생' 탭의 대운 풀이와 '연도' 탭의 해마다 풀이에서 볼 수 있어요.", false, null);
		return y + 8f;
	}

	// ── 토정비결 상세 ──
	private int tjA, tjC, tjD, tjNo, tjYr; private string tjSeed = "";
	private static readonly string[] TSangL = { "",
		"초반은 더디지만 갈수록 힘이 붙는 해예요. 급히 서두르면 어긋나고, 차근차근 쌓으면 가을 이후 크게 열매를 맺어요. 시작한 일은 끝까지 밀고 가세요.",
		"뜻하지 않은 귀인이 나타나는 해예요. 혼자 끙끙대지 말고 주변에 도움을 청하면 막힌 일이 풀려요. 사람을 통해 길이 열려요.",
		"변화와 이동이 많은 해예요. 이사·이직·여행 등 환경이 바뀔 수 있고, 새 출발에 유리해요. 다만 기반 없이 옮기면 흔들리니 준비를 먼저 하세요.",
		"재물과 일에서 결실이 따르는 해예요. 그동안의 노력이 인정받고 수입도 오르지만, 자랑하면 시기를 받으니 겸손하게 처신하세요.",
		"마음고생 뒤에 평안이 오는 해예요. 상반기에 걱정이 많아도 하반기엔 풀려요. 건강과 가정을 먼저 돌보세요.",
		"명예와 인정이 따르는 해예요. 책임 있는 자리를 맡을 수 있고 시험·승진에 유리해요. 규칙을 지키면 더 크게 길해요.",
		"구설과 시비를 조심해야 하는 해예요. 말을 아끼고 문서를 꼼꼼히 확인하면 큰 탈이 없어요. 한 박자 늦추는 것이 이득이에요.",
		"안정 속에 조용히 쌓이는 해예요. 큰 한 방보다 꾸준한 저축과 건강 관리가 복이 돼요. 욕심을 줄일수록 평화로워요." };
	private static readonly string[] TJungL = { "",
		"일과 학업에서는 꾸준함이 가장 큰 무기예요. 성과가 바로 보이지 않아도 쌓이고 있어요.",
		"금전은 들어오는 만큼 나가기 쉬워요. 고정 지출을 정리하고 목표 금액을 정해 모으세요.",
		"새로운 일이나 사람과의 인연이 열려요. 낯선 곳에서 기회가 오니 모임에 나가 보세요.",
		"집안과 가까운 사람과의 관계가 중요해요. 가족의 일이 곧 나의 운과 연결돼요.",
		"직장·사업에서 변동 수가 있어요. 서두르지 않고 상황을 보며 움직이면 오히려 이득이에요.",
		"배움과 문서 운이 좋아요. 계약·시험·자격증에 도전하기 좋은 흐름이에요." };
	private static readonly string[] THaL = { "",
		"마음 가는 대로보다 계획대로 움직이세요. 즉흥적인 결정은 후회를 부르기 쉬워요.",
		"주변 의견을 한 번 더 들어 보세요. 혼자만의 판단이 빗나갈 수 있어요.",
		"인연과 대인 관계가 풍성해요. 먼저 연락하고 베푸는 만큼 돌아와요." };
	private static readonly string[] TjTheme = { "새해의 시작, 계획을 세우는 달", "차가운 끝자락, 내실을 다지는 달", "봄기운이 도는 달, 새 인연의 달", "활동이 늘고 바빠지는 달", "결실과 가정의 달", "한 해의 중간 점검의 달",
		"더위 속 체력 관리의 달", "쉬어 가며 재정비하는 달", "수확을 준비하는 달", "성과가 드러나는 달", "정리와 마무리를 시작하는 달", "한 해를 갈무리하는 달" };
	private static readonly string[][] MoLv = { null,
		new[] { "운이 낮아요. 새 일을 벌이기보다 지키는 달이에요.", "마음고생이 따를 수 있어요. 말과 지출을 조심하세요.", "예상 밖의 변수가 있어요. 약속과 서류를 두 번 확인하세요." },
		new[] { "다소 답답해요. 서두르지 않으면 무난히 넘겨요.", "작은 걸림돌이 있으나 금방 풀려요.", "기다림이 필요한 달이에요. 준비에 집중하세요." },
		new[] { "무난하고 평온해요. 하던 일을 꾸준히 하면 돼요.", "큰 일은 없으나 소소한 기쁨이 있어요.", "일상이 안정적이에요. 건강 루틴을 챙기기 좋아요." },
		new[] { "운이 올라와요. 미뤄 둔 일을 추진해 보세요.", "귀인의 도움이 있어요. 먼저 손을 내밀어 보세요.", "성과가 보이기 시작해요. 자신감을 가져요." },
		new[] { "한 해 중 손꼽는 길한 달이에요. 중요한 일을 이때 몰아요!", "재물과 인연 모두 활짝 열려요. 과욕만 경계하세요.", "무엇을 해도 순풍이에요. 감사하는 마음으로 나누세요." } };
	private static readonly string[] MoDo = { "▶ 이달의 행동: 아침 10분 정리 시간 갖기.", "▶ 이달의 행동: 오래 연락 못한 사람에게 안부 전하기.", "▶ 이달의 행동: 큰 지출 전에 하루 미루기.", "▶ 이달의 행동: 가볍게 걷기 30분.",
		"▶ 이달의 행동: 감사한 일 세 가지 적기.", "▶ 이달의 행동: 책상과 지갑 정리하기.", "▶ 이달의 행동: 새로운 것 하나 배워 보기.", "▶ 이달의 행동: 충분히 자고 물 많이 마시기." };
	private static readonly string[] TjMoney = { "재물: 들어오는 문이 열리나 나가는 문도 넓어요. 가계부를 꼭 쓰세요.", "재물: 큰돈보다 꾸준한 수입이 이득이에요. 적금과 분산이 길해요.", "재물: 투자·보증은 신중하게. 아는 사람의 권유일수록 한 번 더 확인하세요.", "재물: 하반기에 수입이 늘어요. 상반기는 준비하고 모으는 시기예요." };
	private static readonly string[] TjRel = { "관계: 귀인이 나타나요. 먼저 인사하고 먼저 베푸세요.", "관계: 말 한마디가 오해를 낳기 쉬워요. 메시지는 보내기 전에 한 번 더 읽어요.", "관계: 가족·연인과 함께하는 시간이 복이에요. 대화를 늘리세요.", "관계: 모임이 늘어요. 다만 술자리와 구설을 조심해요." };
	private static readonly string[] TjHealth = { "건강: 소화기와 수면을 챙기세요. 규칙적인 식사가 최고의 약이에요.", "건강: 과로와 어깨·허리 결림을 조심하세요. 스트레칭을 생활화해요.", "건강: 큰 병은 없으나 환절기 감기에 주의하세요. 면역 관리를 해요.", "건강: 마음의 피로가 몸으로 와요. 취미와 휴식을 일정에 넣어요." };
	private static readonly string[] TjMove = { "이동: 이사·이직 이야기가 나올 수 있어요. 상반기는 알아보고 하반기에 결정하세요.", "이동: 먼 길보다 가까운 곳이 이로워요. 장거리 운전은 여유 있게 하세요.", "이동: 여행·출장 운이 좋아요. 새 환경에서 기회를 만나요.", "이동: 자리를 지키는 쪽이 길해요. 옮기려면 준비를 충분히 하세요." };
	private static readonly string[] TjDo = { "아침에 일어나 창문을 열고 환기하기", "지갑과 책상 정리하기", "하루 한 줄 일기 쓰기", "고마운 사람에게 안부 전하기", "작은 기부나 나눔 실천하기", "화가 날 때 열을 세고 말하기", "주 3회 가볍게 걷기", "밤 11시 이전에 눕기" };

	private float TojiDetail(Graphics g, float y)
	{
		int a = tjA, c = tjC, d = tjD; string seed = tjSeed;
		y = SecHead(g, y, tjYr + "년 토정비결 자세한 풀이", tjNo + "번 괘");
		y = Para(g, y, "한 해의 큰 흐름 · 상괘 " + a, TSangL[a], true, null);
		y = Para(g, y, "일·재물·인연 · 중괘 " + c, TJungL[c], false, null);
		y = Para(g, y, "마음가짐 · 하괘 " + d, THaL[d], false, null);
		y = SecHead(g, y + 4, "올해의 네 가지 포인트", null);
		y = Para(g, y, "재물 · 관계 · 건강 · 이동", TjMoney[(a + c) % 4] + "\n" + TjRel[(c + d) % 4] + "\n" + TjHealth[(a + d) % 4] + "\n" + TjMove[(a + c + d) % 4], false, null);
		y = SecHead(g, y + 4, "월별 자세히", "음력 흐름 기준 약식");
		for (int m = 1; m <= 12; m++)
		{
			int lv = Math.Max(1, Math.Min(5, 3 + (Saju.Hash(seed + "m" + m) % 5) - 2 + (a >= 5 && m % 4 == 0 ? 1 : 0)));
			y = Para(g, y, m + "월 · " + TjTheme[m - 1], MoLv[lv][Saju.Hash(seed + "d" + m) % 3] + "\n" + MoDo[Saju.Hash(seed + "x" + m) % 8].Replace("이달의 행동", "행동"), false, StarS(lv));
		}
		y = SecHead(g, y + 4, "한 해를 잘 보내는 다섯 가지", null);
		string tips = ""; for (int i = 0; i < 5; i++) tips += (i > 0 ? "\n" : "") + "● " + TjDo[(Saju.Hash(seed + "t" + i) % 8 + i * 3) % 8];
		y = Para(g, y, "생활 속 실천", tips + "\n토정비결은 한 해의 분위기를 가볍게 살펴보는 전통 풍습이에요. 좋은 말은 힘으로, 조심하라는 말은 점검의 기회로 삼아 보세요.", false, null);
		return y + 8f;
	}


	// ── 궁합 상세 ──
	private static readonly string[] MLvTx = {
		"서로 다른 점이 눈에 띄는 사이예요. 같은 상황도 다르게 받아들이니 오해가 생기기 쉽지만, 그만큼 서로에게 배울 점이 많아요. 결론을 서두르기보다 이유를 먼저 묻는 습관이 관계를 지켜 줘요.",
		"노력이 필요한 사이예요. 처음엔 어긋나는 부분이 보여도 규칙을 정해 두면 훨씬 편해져요. 연락 빈도, 돈 쓰는 방식, 쉬는 방법처럼 자주 부딪히는 주제를 미리 맞춰 보세요.",
		"무난하고 안정적인 사이예요. 큰 파도는 없지만 그래서 오래갈 수 있어요. 익숙함에 감사를 잊지 않고 가끔 새로운 경험을 함께 하면 더 단단해져요.",
		"서로를 편안하게 해 주는 좋은 사이예요. 함께 있을 때 힘이 나고 상대가 곁에 있다는 사실만으로 든든해요. 고마움을 말로 자주 표현하면 더 좋아져요.",
		"서로의 운을 끌어올리는 아주 좋은 사이예요. 같이 계획을 세우고 도전하면 혼자일 때보다 훨씬 잘 풀려요. 다만 너무 잘 맞는다고 대화를 줄이지는 마세요.",
		"기운이 찰떡처럼 맞는 천생연분이에요. 말하지 않아도 통하는 순간이 많아요. 이 좋은 흐름을 오래 지키려면 사소한 약속과 배려를 꾸준히 이어가세요." };
	private static readonly string[] MStemTx = {
		"일간끼리 합이 되는 관계예요. 서로에게 자연스럽게 끌리고, 한쪽이 약해질 때 다른 쪽이 감싸 주는 인연이에요.",
		"상대가 나를 생해 주는 관계예요. 상대 곁에서 내가 안정과 힘을 얻어요. 받는 만큼 고마움을 표현하면 균형이 맞아요.",
		"내가 상대를 생해 주는 관계예요. 내가 챙기고 베푸는 역할이 되기 쉬워요. 지치지 않게 나도 도움을 요청하세요.",
		"일간이 같은 오행이라 닮은 점이 많고 편해요. 하지만 고집이 부딪히면 양보가 없을 수 있어서 한 박자 쉬어 가세요.",
		"내가 상대를 이끄는 관계예요. 주도권이 내게 있다 보니 밀어붙이기 쉬워요. 결정 전에 상대 의견을 먼저 물어보세요.",
		"상대가 나를 이끄는 관계예요. 긴장감이 있지만 성장시켜 주는 인연이기도 해요. 눌린다고 느끼면 솔직하게 말해요.",
		"일간이 충을 이루어 성향이 정반대예요. 처음엔 신선하지만 갈수록 차이가 크게 느껴져요. 서로 다름을 틀림으로 보지 않는 것이 핵심이에요." };
	private static readonly string[] MBranchTx = {
		"배우자궁(일지)이 특별히 부딪히지도 끌리지도 않아요. 감정의 기복이 적은 대신 관계를 가꾸려는 노력이 필요해요.",
		"일지가 육합이에요. 마음 깊은 곳에서 통하고 정이 두터워지는 최고의 배우자궁 조합이에요.",
		"일지가 충이에요. 사소한 일로 크게 다툴 수 있으니 감정이 올라올 때는 잠시 자리를 피했다가 다시 이야기하세요.",
		"일지가 해예요. 말 못한 서운함이 쌓이기 쉬워요. 작은 불만도 그날 풀면 문제가 되지 않아요.",
		"일지가 삼합이에요. 함께 목표를 세우면 시너지가 커지는 조합이에요. 같은 일을 같이 하면 더 가까워져요.",
		"일지가 같아요. 속마음과 생활 패턴이 닮아 편안해요. 다만 단점도 닮아서 서로 지적할 때는 부드럽게 해요." };
	private static readonly string[] MAnimalTx = {
		"띠 궁합은 평범해요. 특별한 도움도 방해도 없으니 서로의 노력이 결과를 결정해요.",
		"띠가 육합이에요. 가족·친구 모임에서도 잘 어울리는 찰떡 띠예요.",
		"띠가 충이에요. 정반대 성향이 끌림이 되기도 하고 갈등이 되기도 해요. 취향 차이를 즐기는 쪽으로 풀어 보세요.",
		"띠가 해예요. 오해가 쌓이기 쉬우니 확인 질문을 아끼지 마세요.",
		"띠가 삼합이에요. 주변 사람들과 함께할 때 더 빛나는 사이예요.",
		"같은 띠예요. 통하는 부분이 많고 서로의 마음을 쉽게 알아요." };
	private static readonly string[] MDate = { "산책·자연 속 데이트가 잘 맞아요 (목).", "밝고 활기찬 곳, 공연·맛집 데이트가 좋아요 (화).", "집이나 카페에서 편안하게 시간을 보내는 데이트가 좋아요 (토).", "전시·영화처럼 정돈된 분위기의 데이트가 좋아요 (금).", "바다·호수 같은 물가나 조용한 분위기가 잘 어울려요 (수)." };

	private float MatchDetail(Graphics g, float y, int a, int b, int ea, int eb, int q2, int q3, int s1, int s2, int s3, int s4, int s5, int score, int li, int fe, int bothLack)
	{
		y = SecHead(g, y, "궁합 자세한 풀이", score + "점");
		y = Para(g, y, "한 줄 총평", MLvTx[li], true, null);
		int r1 = Saju.Rel(ea, eb); int k1 = Math.Abs(a - b) == 5 ? 0 : (r1 == 4 ? 1 : (r1 == 1 ? 2 : (r1 == 0 ? 3 : (r1 == 2 ? 4 : 5)))); int dd = ((a - b) % 10 + 10) % 10; if (dd == 4 || dd == 6) k1 = 6;
		y = Para(g, y, "성향 궁합 (일간) · " + s1 + "점", MStemTx[k1], false, null);
		y = Para(g, y, "배우자궁 (일지) · " + s2 + "점", MBranchTx[q2], false, null);
		y = Para(g, y, "띠 궁합 · " + s3 + "점", MAnimalTx[q3], false, null);
		string ft = fe >= 0 ? "서로 부족한 " + Saju.EN[fe] + " 기운을 채워 주는 조합이에요. 한쪽에게 모자란 점을 다른 쪽이 자연스럽게 보완해요." : (bothLack > 0 ? "둘 다 부족한 기운이 있어서 같은 약점을 가졌어요. 같이 그 기운을 채우는 생활을 하면 도움이 돼요." : "오행 균형이 비슷해서 큰 보완 관계는 없지만 안정적이에요.");
		y = Para(g, y, "오행 보완 · " + s4 + "점", ft, false, null);
		y = SecHead(g, y + 4, "서로에게 어떤 사람일까", null);
		y = Para(g, y, "상대가 나에게 · " + Saju.TenGod(a, b), TGMean[Saju.TGIndex(a, b)] + " 같은 존재예요. " + YrTG[Math.Max(0, Array.IndexOf(TGN, Saju.TenGod(a, b)))].Replace("해예요", "관계예요").Replace("시기예요", "관계예요"), false, null);
		y = Para(g, y, "내가 상대에게 · " + Saju.TenGod(b, a), TGMean[Saju.TGIndex(b, a)] + " 같은 존재예요.", false, null);
		if (s5 >= 0)
		{
			y = SecHead(g, y + 4, "MBTI 케미 · " + s5 + "점", mM + " × " + mP);
			string mt = (mM[1] == mP[1] ? "정보를 받아들이는 방식이 같아서 이야기가 잘 통해요. " : "한쪽은 현실, 다른 쪽은 가능성을 보는 편이라 서로 시야를 넓혀 줘요. ") + (mM[2] == mP[2] ? "판단 기준이 닮아 의견이 잘 모여요. " : "한쪽은 논리, 다른 쪽은 감정을 먼저 보니 대화할 때 결론 전에 마음부터 확인하세요. ") + (mM[3] == mP[3] ? "생활 방식이 닮아 약속과 계획 잡기가 쉬워요." : "계획형과 즉흥형이 만나 서로의 빈틈을 채워요. 일정은 미리 합의하세요.");
			y = Para(g, y, "마음 케미", mt, false, null);
		}
		y = SecHead(g, y + 4, "함께 잘 지내는 법", null);
		int he = fe >= 0 ? fe : ea;
		y = Para(g, y, "갈등이 생기면", q2 == 2 || q2 == 3 ? "감정이 올라오면 바로 결론 내지 말고 하루 뒤에 다시 이야기하세요. 비난보다 '나는 ~해서 속상했어'로 시작하면 풀려요." : "서로를 탓하기보다 상황을 같이 보세요. 먼저 사과하는 쪽이 지는 게 아니라 관계를 지키는 쪽이에요.", false, null);
		y = Para(g, y, "데이트·함께할 일", MDate[he] + " 둘이 같이 새로운 취미 하나를 정해 꾸준히 해 보세요.", false, null);
		y = Para(g, y, "오래 가는 비결", score >= 80 ? "이미 잘 맞는 사이일수록 감사 표현이 줄어요. 기념일보다 평범한 날의 안부와 고맙다는 말을 챙기세요." : "다른 점을 고치려 하기보다 서로의 방식으로 받아들이는 연습이 가장 큰 보약이에요. 한 달에 한 번 서로의 불만을 솔직하게 나누는 시간을 가져 보세요.", false, null);
		y = Para(g, y, "알아두세요", "사주 궁합은 두 사람의 기운을 재미로 견주어 보는 풀이예요. 실제 관계는 서로의 마음과 노력이 가장 중요해요.", false, null);
		return y + 8f;
	}

	// ── 스크롤 · 연도 선택 입력 ──
	private float MaxY() { return Math.Max(0f, contentH - 740f); }
	private void DrawYearArrows(Graphics g)
	{
		if ((tab != 4 && tab != 7) || ySc > 20f) return; int yr = tab == 4 ? tY4 : tY7;
		for (int i = 0; i < 2; i++)
		{
			yR[i] = new RectangleF(i == 0 ? 298f : 372f, 146f, 24f, 24f); using (GraphicsPath gp = Gfx.Round(yR[i], 12)) { using (SolidBrush b = new SolidBrush(PCard)) g.FillPath(b, gp); using (Pen p = new Pen(PLine, 1f)) g.DrawPath(p, gp); }
			FUi.Text(g, i == 0 ? "◀" : "▶", FUi.F(8.5f, 1), PRed, yR[i], StringAlignment.Center, StringAlignment.Center, false);
		}
		FUi.Text(g, yr + "년", FUi.F(9.5f, 1), PInk, new RectangleF(322, 146, 50, 24), StringAlignment.Center, StringAlignment.Center, false);
	}
	private void DrawScrollbar(Graphics g)
	{
		float mx = MaxY(); if (mx <= 0) return; float th = Math.Max(24f, 590f * 604f / (604f + mx)), ty = 142f + (592f - th) * ySc / mx;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(401, ty, 4, th), 2)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 170, 52, 44))) g.FillPath(b, gp);
	}
	private bool YearClick(float x, float y)
	{
		if (tab != 4 && tab != 7) return false; int lo = DateTime.Now.Year - 1, hi = DateTime.Now.Year + 9;
		for (int i = 0; i < 2; i++) if (yR[i].Contains(x, y - 38)) { int d = i == 0 ? -1 : 1; if (tab == 4) tY4 = Math.Max(lo, Math.Min(hi, tY4 + d)); else tY7 = Math.Max(lo, Math.Min(hi, tY7 + d)); ySc = 0; Sfx.Play("tick"); Invalidate(); return true; }
		return false;
	}
}
