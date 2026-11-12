using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// 다중분개 도우미: 지급결의 내용(한 줄에 한 건)을 넣으면 e호조(지방재정관리시스템) 방식의 재무회계 분개를 알려 줘요
// 인터넷 없이 키워드로 분류해요. 계정과목 중 ※ 표시는 일반적인 명칭이라 e호조 계정 검색으로 꼭 확인해야 해요.
internal static class JournalGen
{
	public sealed class Ln { public bool Dr; public string Acct; public long Amt; }
	public sealed class St { public string Name; public List<Ln> L = new List<Ln>(); }
	private sealed class Rule
	{
		public string Name, Tong, Acct, Kind, Wh, Note; public string[] Kw;
		public Rule(string name, string kind, string tong, string acct, string wh, string note, params string[] kw) { Name = name; Kind = kind; Tong = tong; Acct = acct; Wh = wh; Note = note; Kw = kw; }
	}
	// Kind: E 비용 · A 자산취득 · W 공사(건설중) · P 인건비 · S 선급금 · D 일상경비 · I 이자 · R 원금상환
	// Wh: "" 없음 · "기타" 8.8% · "사업" 3.3% · "일용" 일용근로
	private static readonly Rule[] Rules = {
		new Rule("선급금(선금) 지급", "S", "", "선급금", "", "선금을 줄 때는 비용이 아니라 선급금(자산)으로 먼저 잡고, 나중에 검수할 때 정산해요", "선금", "선급금", "착수금", "계약금"),
		new Rule("일상경비 전도금", "D", "", "소모품비", "", "일상경비는 교부 → 사용 → 잔액 반납이 각각 다른 분개예요", "일상경비", "전도금", "정액자금"),
		new Rule("차입금 이자", "I", "", "차입금이자비용", "", "", "차입금이자", "대출이자", "지방채이자", "이자 지급"),
		new Rule("차입금 원금 상환", "R", "", "장기차입금", "", "만기 1년 이내로 바뀌면 유동성장기차입금으로 대체돼 있을 수 있어요", "차입금 상환", "원금상환", "원금 상환", "채무상환"),
		new Rule("공사·시설비(건설중)", "W", "401-01 시설비", "건설중인일반유형자산", "", "기성·준공 검수 때 자산(건설중인일반유형자산)을 선택·등록해야 해요. 준공되면 건물 등으로 대체해요", "공사", "기성", "준공", "설계", "감리", "시공", "철거", "토목", "신축", "증축", "포장공사", "시설비"),
		new Rule("기간제·공무직 인건비", "P", "101-04 기간제근로자등 보수", "기간제근로자등인건비", "", "4대보험 사업주 부담분은 법정부담금으로 따로 지출결의해요", "기간제", "공무직", "무기계약", "시간선택제"),
		new Rule("일용직 임금", "P", "101-04 기간제근로자등 보수", "기간제근로자등인건비", "일용", "일용근로소득은 일급에서 15만원을 빼고 6%의 45%만 원천징수해요(지방소득세 10% 별도)", "일용", "일당", "노임", "아르바이트", "단기근로"),
		new Rule("공무원 보수·수당", "P", "101-01 보수", "급여", "", "소득세·4대보험 등 공제액은 예수금으로 나눠 기록돼요(공제 내역 입력 필요)", "급여", "보수", "수당", "상여", "연가보상", "성과급", "초과근무", "시간외"),
		new Rule("통장·이장 수당", "E", "301-07 통장·이장·반장활동보상금", "기타운영비※", "", "", "통장", "이장", "반장수당"),
		new Rule("국내 출장·여비", "E", "202-01 국내여비", "여비교통비※", "", "", "출장", "여비", "숙박", "교통비", "국내여비", "KTX", "항공", "시외버스"),
		new Rule("업무추진비(간담회·격려)", "E", "203 업무추진비", "업무추진비※", "", "기관운영/시책추진/부서운영 중 어느 통계목인지 사업 성격에 맞춰 골라요", "간담회", "격려", "축의", "부의", "업무추진", "회식", "오찬", "만찬", "식대", "조의"),
		new Rule("연구·전산개발 용역", "E", "207 연구개발비", "연구용역비※", "사업", "연구용역은 207-01, 정보화·시스템 개발은 207-02예요. 개인 대상이면 사업소득 3.3%를 떼요", "연구용역", "정보화", "전산개발", "시스템 개발", "소프트웨어 개발", "프로그램 개발"),
		new Rule("개인 용역·번역·촬영 등", "E", "201-01 사무관리비(소규모 용역비)", "용역비※", "", "개인에게 주면 사업소득 3.3%(일시적이면 기타소득 8.8%), 사업자·법인이면 원천징수가 없어요. 금액 앞에 '3.3' 또는 '8.8'을 쓰면 계산해요", "용역", "번역", "통역", "촬영", "디자인", "컨설팅", "대행", "속기", "녹취"),
		new Rule("강사료·원고료·심사비", "E", "201-03 행사운영비", "행사운영비※", "기타", "일시적 강의·원고·심사는 기타소득(8.8%), 계속·반복이면 사업소득(3.3%)이에요. 지급액 12.5만원 이하는 원천징수가 없어요", "강사료", "강의료", "원고료", "심사비", "심사료", "자문료", "회의수당", "사례금", "위원수당", "면접수당", "평가수당"),
		new Rule("행사 경비", "E", "201-03 행사운영비", "행사운영비※", "", "", "행사", "현수막", "상패", "초청장", "축제", "기념식", "체육대회"),
		new Rule("위탁교육·교육훈련", "E", "201-01 사무관리비(위탁교육비)", "교육훈련비※", "", "공무원 본인 교육여비는 202-05 공무원 교육여비예요", "위탁교육", "교육훈련", "수강료", "교육비", "연수비"),
		new Rule("공공요금(전화·전기·유류)", "E", "201-02 공공운영비", "공공운영비※", "", "성격별 계정(통신료·수도광열비 등)이 갈려 있으면 그에 맞춰 골라요", "전화", "우편", "통신", "인터넷", "전기", "수도", "가스", "난방", "유류", "주유", "경유", "휘발유", "등유", "택배"),
		new Rule("건물·시설 수선유지", "E", "201-02 공공운영비(시설장비 유지비)", "건물수선유지비", "", "자산 가치를 높이는 큰 공사면 자본적 지출이라 자산으로 잡아야 해요", "수선", "수리", "보수공사", "유지보수", "도색", "방수", "누수", "정비", "점검"),
		new Rule("임차료·렌탈", "E", "201-01 사무관리비(임차료)", "임차료※", "", "", "임차", "임대", "렌탈", "복합기", "대여"),
		new Rule("보조금(민간)", "E", "307 민간이전", "민간보조금※", "", "교부결정 → 지급 → 정산(반납)이 단계별이에요. 자산이 생기는 사업이면 자산 등록이 필요해요", "보조금", "지원금", "위탁금", "운영비 보조"),
		new Rule("시·도비 보조금 등(정부간 이전)", "E", "308 자치단체등 이전", "시·도비보조금", "", "", "시도비", "시·도비", "시비 보조", "도비 보조", "자치단체 부담금", "분담금"),
		new Rule("손해배상금", "E", "", "손해배상금및국가배상금", "", "소송이면 확정 시점에 우발손실 부채를 먼저 잡았는지 확인해요", "배상", "손해배상", "소송", "합의금"),
		new Rule("포상금·상금", "E", "303 포상금", "포상금※", "기타", "상금·부상은 기타소득 원천징수 대상이 될 수 있어요", "포상금", "성과금", "상금", "부상"),
		new Rule("비품·장비 구입(자산)", "A", "405-01 자산 및 물품취득비", "집기비품", "", "단가가 기준금액 미만이면 소모품비로 비용처리하는 지자체가 많아요(재무회계 운영규정 확인)", "비품", "컴퓨터", "노트북", "모니터", "프린터", "책상", "의자", "캐비닛", "에어컨", "냉장고", "복사기", "TV", "카메라", "서버", "장비", "기계", "자동차", "차량", "집기"),
		new Rule("도서 구입", "A", "405-02 도서구입비", "집기비품", "", "일반 간행물·신문은 사무관리비(소모품)로 처리해요", "도서구입", "도서 구입", "도서관", "서적"),
		new Rule("소모품·사무용품·인쇄", "E", "201-01 사무관리비", "소모품비", "", "", "소모품", "사무용품", "문구", "복사용지", "용지", "토너", "잉크", "인쇄", "복사", "간행물", "신문", "구독", "피복", "급식", "다과", "청소용품", "비품 수선"),
	};

	private static readonly Regex Amt = new Regex(@"(\d{1,3}(?:,\d{3})+|\d+)\s*(만\s*원|천\s*원|만|원)", RegexOptions.Compiled);
	public static long ParseAmt(string s)
	{
		long v = 0; foreach (Match m in Amt.Matches(s)) { long n; string d = m.Groups[1].Value.Replace(",", ""); if (!long.TryParse(d, out n)) continue; string u = m.Groups[2].Value.Replace(" ", ""); v = u.StartsWith("만") ? n * 10000 : u.StartsWith("천") ? n * 1000 : n; }
		return v;
	}
	private static Rule Pick(string t)
	{
		Rule best = null; int bs = 0;
		foreach (Rule r in Rules) { int sc = 0; foreach (string k in r.Kw) if (t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) sc += 1 + k.Length / 4; if (sc > bs) { bs = sc; best = r; } }
		return best;
	}
	private static bool Has(string t, params string[] ks) { foreach (string k in ks) if (t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
	// 원천징수액: [소득세, 지방소득세, 설명]
	public static long[] Withhold(string kind, long amt, out string desc)
	{
		desc = ""; long tax = 0;
		if (amt <= 0) { desc = "금액을 쓰면 원천징수액도 계산해 드려요"; return new long[] { 0, 0 }; }
		if (kind == "기타") { if (amt <= 125000) { desc = "지급액 125,000원 이하(기타소득금액 5만원 이하)는 원천징수 없음"; return new long[] { 0, 0 }; } tax = (long)Math.Floor(amt * 0.08); desc = "기타소득 8.8% (필요경비 60% 의제: 소득세 8% + 지방소득세 0.8%)"; }
		else if (kind == "사업") { tax = (long)Math.Floor(amt * 0.03); desc = "사업소득 3.3% (소득세 3% + 지방소득세 0.3%)"; }
		else if (kind == "일용") { long b = Math.Max(0, amt - 150000); tax = (long)Math.Floor(b * 0.06 * 0.45); if (tax < 1000) { tax = 0; desc = "일용근로 소득세 1,000원 미만은 소액부징수"; return new long[] { 0, 0 }; } desc = "일용근로 (일급-15만원)×6%×45% + 지방소득세 10%"; }
		long loc = (long)Math.Floor(tax * 0.1 / 10) * 10; return new long[] { tax, loc };
	}
	private static string Won(long v) { return v.ToString("N0") + "원"; }

	public sealed class Item { public string Text, Title, Tong, Note, WhDesc; public long Amt, Wh; public bool Matched; public List<St> S = new List<St>(); }

	public static Item Make(string line)
	{
		Item it = new Item { Text = line.Trim() }; long a = ParseAmt(line); it.Amt = a;
		Rule r = Pick(line);
		if (r == null) { it.Title = "분류하지 못했어요"; it.Note = "내용을 조금 더 구체적으로 써 주세요 (예: 사무용품 구입, 강사료 지급, 출장비, 전기요금, 공사 기성금)"; return it; }
		it.Matched = true; it.Title = r.Name; it.Tong = r.Tong; it.Note = r.Note;
		string wh = r.Wh;
		if (Has(line, "3.3", "사업소득")) wh = "사업"; else if (Has(line, "8.8", "기타소득")) wh = "기타"; else if (Has(line, "일용", "일당")) wh = "일용";
		if (Has(line, "(주)", "㈜", "주식회사", "업체", "세금계산서", "법인", "사업자", "카드", "원천징수 없음", "원천 없음")) wh = "";
		if (r.Kind == "P" && r.Wh == "" && !Has(line, "3.3", "8.8")) wh = "";
		if (r.Kind == "S" || r.Kind == "D" || r.Kind == "I" || r.Kind == "R" || r.Kind == "W" || r.Kind == "A") wh = "";
		string dd = ""; long[] w = new long[] { 0, 0 }; if (wh != "") w = Withhold(wh, a, out dd); long wsum = w[0] + w[1]; it.Wh = wsum; it.WhDesc = wh == "" ? "" : dd + (wsum > 0 ? " → 소득세 " + Won(w[0]) + " + 지방소득세 " + Won(w[1]) : "");
		Func<bool, string, long, Ln> L = delegate(bool dr, string ac, long m) { return new Ln { Dr = dr, Acct = ac, Amt = m }; };
		string kind = r.Kind;
		if (kind == "E" || kind == "A" || kind == "P")
		{
			string pay = kind == "A" ? "일반미지급금" : "일반미지급비용";
			St s1 = new St { Name = "① 지출결의(검수)" }; s1.L.Add(L(true, r.Acct, a)); s1.L.Add(L(false, pay, a)); it.S.Add(s1);
			St s2 = new St { Name = "② 지급명령(이체)" }; s2.L.Add(L(true, pay, a)); if (wsum > 0) s2.L.Add(L(false, "예수금(원천징수)", wsum)); s2.L.Add(L(false, "현금과예금", a - wsum)); it.S.Add(s2);
			if (wsum > 0) { St s3 = new St { Name = "③ 원천세 납부(다음 달 10일까지)" }; s3.L.Add(L(true, "예수금(원천징수)", wsum)); s3.L.Add(L(false, "현금과예금", wsum)); it.S.Add(s3); }
		}
		else if (kind == "W")
		{
			St s1 = new St { Name = "① 선금·기성금 검수" }; s1.L.Add(L(true, r.Acct, a)); s1.L.Add(L(false, "일반미지급금", a)); it.S.Add(s1);
			St s2 = new St { Name = "② 지급명령(이체)" }; s2.L.Add(L(true, "일반미지급금", a)); s2.L.Add(L(false, "현금과예금", a)); it.S.Add(s2);
			St s3 = new St { Name = "③ 준공 시 자산 대체(별도 분개)" }; s3.L.Add(L(true, "건물 등 해당 자산※", 0)); s3.L.Add(L(false, "건설중인일반유형자산", 0)); it.S.Add(s3);
		}
		else if (kind == "S")
		{
			St s1 = new St { Name = "① 선급금 지급" }; s1.L.Add(L(true, "선급금", a)); s1.L.Add(L(false, "현금과예금", a)); it.S.Add(s1);
			St s2 = new St { Name = "② 정산(검수·지급 때, 별도 분개)" }; s2.L.Add(L(true, "해당 자산·비용 계정", 0)); s2.L.Add(L(false, "선급금", 0)); s2.L.Add(L(false, "현금과예금(잔액 지급분)", 0)); it.S.Add(s2);
		}
		else if (kind == "D")
		{
			St s1 = new St { Name = "① 전도금 교부" }; s1.L.Add(L(true, "일상경비전도금", a)); s1.L.Add(L(false, "현금과예금", a)); it.S.Add(s1);
			St s2 = new St { Name = "② 경비 사용(별도 분개)" }; s2.L.Add(L(true, "소모품비 등 해당 비용", 0)); s2.L.Add(L(false, "일상경비전도금", 0)); it.S.Add(s2);
			St s3 = new St { Name = "③ 잔액 반납(별도 분개)" }; s3.L.Add(L(true, "현금과예금", 0)); s3.L.Add(L(false, "일상경비전도금", 0)); it.S.Add(s3);
		}
		else if (kind == "I" || kind == "R")
		{
			St s1 = new St { Name = "① 지급" }; s1.L.Add(L(true, r.Acct, a)); s1.L.Add(L(false, "현금과예금", a)); it.S.Add(s1);
		}
		return it;
	}

	public static string Run(string input)
	{
		string[] lines = input.Replace("\r", "").Split('\n'); List<Item> items = new List<Item>();
		foreach (string ln in lines) { if (ln.Trim().Length == 0) continue; items.Add(Make(ln)); }
		if (items.Count == 0) return "";
		StringBuilder sb = new StringBuilder(); int no = 0; bool qm = false; long total = 0;
		foreach (Item it in items)
		{
			no++; sb.Append("■ ").Append(no).Append(". ").Append(it.Text).Append("\r\n");
			if (!it.Matched) { sb.Append("   ※ ").Append(it.Title).Append(" — ").Append(it.Note).Append("\r\n\r\n"); continue; }
			total += it.Amt;
			sb.Append("   분류: ").Append(it.Title); if (it.Tong.Length > 0) sb.Append("  /  세출 통계목: ").Append(it.Tong); sb.Append("\r\n");
			if (it.WhDesc.Length > 0) sb.Append("   원천징수: ").Append(it.WhDesc).Append("\r\n");
			foreach (St s in it.S) { sb.Append("   ").Append(s.Name).Append("\r\n"); foreach (Ln l in s.L) { if (l.Acct.IndexOf('※') >= 0) qm = true; sb.Append("      ").Append(l.Dr ? "(차) " : "(대) ").Append(l.Acct).Append(l.Amt > 0 ? "  " + l.Amt.ToString("N0") : "").Append("\r\n"); } }
			if (it.Note.Length > 0) sb.Append("   ※ 참고: ").Append(it.Note).Append("\r\n");
			sb.Append("\r\n");
		}
		// 다중분개 합계: 같은 단계끼리 계정별로 합쳐요 (금액이 있는 건만)
		Dictionary<string, Dictionary<string, long>> agg = new Dictionary<string, Dictionary<string, long>>(); List<string> order = new List<string>(); int cnt = 0;
		foreach (Item it in items) { if (!it.Matched) continue; bool any = false; foreach (St s in it.S) { foreach (Ln l in s.L) if (l.Amt > 0) any = true; } if (any) cnt++; }
		if (cnt >= 2)
		{
			sb.Append("══ 묶어서 한 번에 결의할 때 (다중분개 합계) ══\r\n");
			foreach (Item it in items) foreach (St s in it.S)
			{
				string key = s.Name; bool has = false; foreach (Ln l in s.L) if (l.Amt > 0) has = true; if (!has) continue;
				if (!agg.ContainsKey(key)) { agg[key] = new Dictionary<string, long>(); order.Add(key); }
				foreach (Ln l in s.L) { if (l.Amt <= 0) continue; string k = (l.Dr ? "D|" : "C|") + l.Acct; long o; agg[key].TryGetValue(k, out o); agg[key][k] = o + l.Amt; }
			}
			order.Sort(StringComparer.Ordinal);
			foreach (string key in order)
			{
				sb.Append(key).Append("\r\n"); long dr = 0, cr = 0;
				foreach (string pass in new[] { "D|", "C|" }) foreach (KeyValuePair<string, long> kv in agg[key]) if (kv.Key.StartsWith(pass)) { sb.Append("      ").Append(pass == "D|" ? "(차) " : "(대) ").Append(kv.Key.Substring(2)).Append("  ").Append(kv.Value.ToString("N0")).Append("\r\n"); if (pass == "D|") dr += kv.Value; else cr += kv.Value; }
				sb.Append("      차변 합계 ").Append(dr.ToString("N0")).Append(" / 대변 합계 ").Append(cr.ToString("N0")).Append(dr == cr ? "  ✔ 일치" : "  ✘ 불일치").Append("\r\n");
			}
			sb.Append("\r\n");
		}
		if (qm) sb.Append("※ 표시 계정은 일반적인 명칭이에요. e호조 계정과목 검색에서 정확한 명칭을 확인하세요.\r\n");
		sb.Append("※ 참고용이에요. 실제 분개는 e호조가 세출 과목과 연계해 자동 생성하며, 건설중 자산·예산외 거래 등은 직접 입력해야 해요. 확정은 회계 담당자와 재무회계 운영규정으로 확인하세요.");
		return sb.ToString();
	}
}

internal sealed class JournalForm : Form
{
	private static JournalForm inst;
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new JournalForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }
	private readonly TextBox tIn = new TextBox(), tOut = new TextBox(); private readonly Label lSt = new Label();
	public JournalForm()
	{
		float s = Math.Min(1.2f, FUi.Scale(560, 560)); Text = "다중분개 도우미 (e호조 지급결의)"; MaximizeBox = true; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(560 * s), (int)(560 * s)); MinimumSize = new Size((int)(420 * s), (int)(420 * s));
		Font = new Font("맑은 고딕", 8.5f * s); BackColor = Color.FromArgb(246, 249, 252);
		Label l1 = new Label { Text = "지출 내용 (한 줄에 한 건 · 금액을 같이 쓰면 계산해요)", Left = (int)(12 * s), Top = (int)(10 * s), Width = (int)(430 * s), Height = (int)(20 * s), Anchor = AnchorStyles.Top | AnchorStyles.Left };
		tIn.Left = (int)(12 * s); tIn.Top = (int)(32 * s); tIn.Width = (int)(536 * s); tIn.Height = (int)(86 * s); tIn.Multiline = true; tIn.ScrollBars = ScrollBars.Vertical; tIn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		tIn.Text = "사무용품 구입 350,000원\r\n외부강사 강사료 200,000원\r\n컴퓨터 구입 1,200,000원";
		Button go = new Button { Text = "분개 만들기", Left = (int)(12 * s), Top = (int)(124 * s), Width = (int)(100 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		Button cp = new Button { Text = "결과 복사", Left = (int)(120 * s), Top = (int)(124 * s), Width = (int)(90 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		Button ex = new Button { Text = "예시 보기", Left = (int)(218 * s), Top = (int)(124 * s), Width = (int)(90 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		lSt.Left = (int)(316 * s); lSt.Top = (int)(130 * s); lSt.Width = (int)(232 * s); lSt.Height = (int)(20 * s); lSt.ForeColor = Color.FromArgb(80, 110, 150);
		tOut.Left = (int)(12 * s); tOut.Top = (int)(160 * s); tOut.Width = (int)(536 * s); tOut.Height = (int)(388 * s); tOut.Multiline = true; tOut.ReadOnly = true; tOut.ScrollBars = ScrollBars.Vertical; tOut.BackColor = Color.White; tOut.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		Action make = delegate { try { tOut.Text = JournalGen.Run(tIn.Text); lSt.Text = tOut.Text.Length == 0 ? "내용을 입력해 주세요" : "만들었어요"; } catch (Exception e) { Program.LogErr(e); lSt.Text = "오류가 났어요"; } };
		go.Click += delegate { make(); };
		cp.Click += delegate { try { if (tOut.Text.Length > 0) { Clipboard.SetText(tOut.Text); lSt.Text = "복사했어요"; } } catch { } };
		string[] exs = { "사무용품 구입 350,000원\r\n외부강사 강사료 200,000원\r\n컴퓨터 구입 1,200,000원", "도로 포장공사 기성금 25,000,000원\r\n공사 감리비 2,000,000원", "직원 국내 출장비 80,000원\r\n간담회 식대 120,000원\r\n전기요금 540,000원", "일용직 노임 220,000원\r\n(주)OO 청소용역 선금 1,000,000원", "일상경비 전도금 500,000원\r\n전화요금 70,000원" }; int ei = 0;
		ex.Click += delegate { tIn.Text = exs[ei++ % exs.Length]; make(); };
		tIn.KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; make(); } };
		Controls.AddRange(new Control[] { l1, tIn, go, cp, ex, lSt, tOut }); make(); Shown += delegate { tIn.SelectionStart = tIn.Text.Length; tIn.SelectionLength = 0; };
	}
}
