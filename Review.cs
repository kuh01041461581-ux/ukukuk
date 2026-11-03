using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// 수강후기 도우미: 강의명을 넣으면 200~210자 후기를 만들어 줘요 (인터넷 없이 문장 조합)
internal static class ReviewGen
{
	private static readonly string[] A = {
		"「{n}」 강의를 수강하며 많은 것을 배울 수 있었습니다.",
		"「{n}」 강의는 실무에 도움이 되는 알찬 시간이었습니다.",
		"「{n}」 강의를 통해 업무 전반을 되돌아보는 좋은 계기를 얻었습니다.",
		"이번에 「{n}」 강의를 수강하면서 평소 궁금했던 내용을 체계적으로 정리할 수 있어서 매우 뜻깊은 시간이었습니다.",
		"「{n}」 강의는 업무와 바로 연결되는 주제여서 처음부터 끝까지 집중해서 들을 수 있었습니다.",
		"「{n}」 강의를 듣기 전에는 막연하게만 알고 있었는데, 수강 후에는 전체 흐름이 또렷하게 그려졌습니다.",
		"바쁜 일정 중에 수강한 「{n}」 강의였지만 시간이 아깝지 않을 만큼 알찬 내용으로 구성되어 있었습니다.",
		"「{n}」 강의를 통해 기본 개념부터 실무 적용까지 차근차근 배울 수 있어 큰 도움이 되었습니다.",
		"기대를 가지고 신청한 「{n}」 강의는 기대 이상으로 짜임새 있고 이해하기 쉽게 진행되었습니다." };
	private static readonly string[] B = {
		"사례 중심의 쉬운 설명 덕분에 핵심 내용을 잘 이해할 수 있었습니다.",
		"강의 구성이 체계적이라 흐름에 맞춰 편하게 학습할 수 있었습니다.",
		"강사님의 명쾌한 설명 덕분에 어려운 부분도 부담 없이 따라갈 수 있었습니다.",
		"특히 실제 사례를 곁들인 설명 덕분에 어려운 개념도 쉽게 이해할 수 있었고, 기억에도 오래 남을 것 같습니다.",
		"강사님께서 핵심을 짚어 가며 쉬운 말로 풀어 설명해 주셔서 배경지식이 부족한 저도 무리 없이 따라갈 수 있었습니다.",
		"이론에 그치지 않고 현장에서 겪을 수 있는 상황을 예로 들어 주셔서 내용이 훨씬 생생하게 와닿았습니다.",
		"차례대로 이어지는 강의 구성 덕분에 중간에 막히는 부분 없이 흐름을 놓치지 않고 학습할 수 있었습니다.",
		"중요한 부분은 반복해서 강조해 주시고 정리 시간도 따로 있어서 배운 내용을 확실히 다질 수 있었습니다.",
		"자료가 깔끔하게 정리되어 있어 수강 중에는 물론 끝난 뒤에도 다시 찾아보기에 매우 편리하겠다고 느꼈습니다." };
	private static readonly string[] C = {
		"배운 내용을 업무에 적극 활용하겠습니다.",
		"앞으로 업무에 적용해 더 효율적으로 일하겠습니다.",
		"배운 점을 바탕으로 업무 역량을 꾸준히 키우겠습니다.",
		"앞으로 맡은 업무를 처리할 때 배운 내용을 적극적으로 활용해 더 정확하고 효율적으로 일하겠습니다.",
		"배운 내용을 동료들과도 공유하고 실제 업무에 적용해 보면서 한 단계 더 성장하는 계기로 삼겠습니다.",
		"이번 학습으로 얻은 지식을 바탕으로 업무의 완성도를 높이고 시행착오도 줄여 나가고 싶습니다.",
		"수강 후에는 업무를 바라보는 시야가 넓어졌고, 새로운 방법을 시도해 볼 자신감도 생겼습니다.",
		"당장 다음 주 업무부터 배운 방법을 하나씩 적용해 보면서 내용을 온전히 제 것으로 만들겠습니다." };
	private static readonly string[] D = {
		"유익한 강의에 감사드립니다.",
		"좋은 교육 기회를 주셔서 감사합니다.",
		"앞으로도 좋은 교육이 이어지길 바랍니다. 감사합니다.",
		"좋은 강의를 준비해 주신 관계자분들께 감사드리며, 비슷한 교육이 앞으로도 꾸준히 이어지길 바랍니다.",
		"유익한 강의를 마련해 주셔서 감사드리고, 다른 분들께도 자신 있게 추천하고 싶습니다.",
		"소중한 배움의 기회를 주셔서 감사드리며, 이어지는 심화 과정이 있다면 꼭 다시 수강하고 싶습니다.",
		"값진 시간을 만들어 주신 강사님과 운영진께 진심으로 감사드립니다.",
		"앞으로도 이런 유익한 교육 기회가 많이 마련되기를 기대합니다. 감사합니다." };
	private static readonly Random R = new Random();
	// 나라배움터 등 공무원 온라인 교육의 주요 분야별로 핵심 학습 내용(K)과 실천 다짐(U)을 가진 표예요
	private sealed class Tp { public string Name; public string[] Kw, K, U; }
	private static readonly Tp[] Tps = {
		new Tp { Name = "정보공개", Kw = new[] { "정보공개", "공개청구", "비공개" }, K = new[] { "정보공개 청구의 처리 절차와 공개 기준", "비공개 대상 정보를 판단하는 기준" }, U = new[] { "청구 내용을 꼼꼼히 확인하며", "법에서 정한 처리 기한을 지키며" } },
		new Tp { Name = "청렴·윤리", Kw = new[] { "청렴", "부패", "윤리", "공직가치", "공직", "갑질", "청탁", "이해충돌", "반부패", "행동강령", "공익신고", "부정" }, K = new[] { "청탁금지법과 이해충돌방지 기준", "공직자가 지켜야 할 청렴과 윤리 원칙" }, U = new[] { "청탁금지법 기준을 꼼꼼히 확인하며", "이해충돌 여부를 먼저 점검하며" } },
		new Tp { Name = "개인정보·보안", Kw = new[] { "개인정보", "정보보호", "정보보안", "보안", "사이버", "해킹", "랜섬", "피싱" }, K = new[] { "개인정보의 수집·이용과 안전한 처리 기준", "개인정보 유출을 막는 관리 방법" }, U = new[] { "개인정보가 담긴 문서를 더욱 철저히 관리하며", "보안 수칙을 일상 업무에서 빠짐없이 지키며" } },
		new Tp { Name = "성희롱·폭력 예방", Kw = new[] { "성희롱", "성폭력", "성매매", "가정폭력", "4대폭력", "폭력", "괴롭힘", "인권", "양성평등", "성인지", "젠더", "스토킹" }, K = new[] { "성희롱·성폭력 예방과 피해 대응 절차", "서로를 존중하는 직장 문화의 중요성" }, U = new[] { "서로를 존중하는 말과 행동을 실천하며", "불편한 상황이 생기면 정해진 대응 절차를 따르며" } },
		new Tp { Name = "장애인식", Kw = new[] { "장애", "인식개선", "다양성", "포용", "배려" }, K = new[] { "장애를 바라보는 올바른 인식과 배려 방법", "장애인과 함께 일하는 환경을 만드는 방법" }, U = new[] { "누구에게나 열린 자세로 민원인을 대하며", "작은 배려부터 먼저 실천하며" } },
		new Tp { Name = "문서·기록", Kw = new[] { "공문서", "문서", "기안", "보고서", "기록물", "기록", "행정업무", "글쓰기", "업무관리", "온나라", "한글" }, K = new[] { "공문서 작성 원칙과 올바른 표기 방법", "문서의 생산부터 보존까지의 관리 절차" }, U = new[] { "공문서를 정확하고 읽기 쉽게 작성하며", "문서 작성 기준을 다시 한번 확인하며" } },
		new Tp { Name = "예산·회계", Kw = new[] { "예산", "회계", "재정", "지출", "보조금", "결산", "세출", "정산", "재무", "원가" }, K = new[] { "예산 편성과 집행의 기본 원칙", "회계 처리 절차와 지출 증빙 기준" }, U = new[] { "예산을 계획에 맞게 투명하게 집행하며", "지출 증빙을 꼼꼼히 챙기며" } },
		new Tp { Name = "계약·조달", Kw = new[] { "계약", "조달", "입찰", "물품", "용역", "공사", "구매", "발주" }, K = new[] { "공공계약의 절차와 유의사항", "입찰과 계약 단계별로 주의할 점" }, U = new[] { "계약 절차를 관련 규정에 맞게 확인하며", "계약 관련 서류를 빠짐없이 점검하며" } },
		new Tp { Name = "민원·소통", Kw = new[] { "민원", "응대", "친절", "고객", "소통", "갈등", "감정", "서비스", "대화", "협상", "스피치", "프레젠" }, K = new[] { "민원 응대의 기본 자세와 갈등 상황 대처법", "상대의 입장을 이해하는 소통 방법" }, U = new[] { "민원인의 입장에서 차분하게 소통하며", "어려운 상황에서도 침착하게 대응하며" } },
		new Tp { Name = "AI·디지털", Kw = new[] { "ai", "인공지능", "데이터", "디지털", "챗gpt", "생성형", "ict", "클라우드", "엑셀", "코딩", "자동화", "빅데이터", "블록체인", "스마트", "공공데이터", "it" }, K = new[] { "인공지능과 데이터를 업무에 활용하는 방법", "디지털 도구를 활용한 업무 효율화 방안" }, U = new[] { "새로운 디지털 도구를 업무에 적극 활용하며", "반복되는 업무를 줄일 방법을 찾아 적용하며" } },
		new Tp { Name = "안전·재난", Kw = new[] { "안전", "재난", "소방", "산업안전", "응급", "심폐", "화재", "대피", "방재", "보건" }, K = new[] { "재난과 안전사고에 대비하는 기본 수칙", "위기 상황에서의 올바른 초기 대응 방법" }, U = new[] { "평소 안전수칙을 먼저 점검하며", "비상 상황 대응 요령을 숙지하며" } },
		new Tp { Name = "복무·인사", Kw = new[] { "복무", "인사", "연가", "급여", "수당", "채용", "공무원연금", "근무", "휴직", "징계" }, K = new[] { "공무원 복무 규정과 인사 제도의 기본", "복무 관련 주요 내용과 유의사항" }, U = new[] { "복무 기준을 정확히 이해하고 지키며", "인사·복무 규정을 먼저 확인하며" } },
		new Tp { Name = "건강·자기관리", Kw = new[] { "건강", "스트레스", "마음", "정신", "자기관리", "워라밸", "명상", "힐링", "치유", "번아웃", "운동" }, K = new[] { "스트레스 관리와 마음 건강을 지키는 방법", "일과 삶의 균형을 맞추는 자기관리 방법" }, U = new[] { "나만의 스트레스 관리 습관을 만들며", "일과 휴식의 균형을 지켜 가며" } },
		new Tp { Name = "법령·행정", Kw = new[] { "법령", "행정법", "행정", "조례", "감사", "규제", "법률", "처분", "소송", "행정심판", "민법", "형법" }, K = new[] { "법령 해석과 행정 절차의 기본 원리", "행정 처분과 감사 대응 시 유의사항" }, U = new[] { "관련 법령을 먼저 확인하며", "행정 절차를 원칙에 맞게 처리하며" } },
		new Tp { Name = "리더십·기획", Kw = new[] { "리더십", "관리자", "조직", "팀", "성과", "협업", "변화", "혁신", "기획", "정책", "문제해결", "의사결정", "역량", "신규", "신임", "직무" }, K = new[] { "조직 성과를 높이는 협업과 리더십", "변화에 대응하는 기획과 문제 해결 방법" }, U = new[] { "동료와 적극적으로 협업하며", "문제를 체계적으로 분석해 해결책을 찾으며" } }
	};
	private static readonly string[] BT = {
		"특히 {k}에 대한 설명이 인상 깊었고, 사례와 함께 살펴보니 이해하기 쉬웠습니다.",
		"{k}에 관한 내용을 체계적으로 배울 수 있어 평소 헷갈리던 부분이 정리되었습니다.",
		"강의에서는 {k}에 대해 구체적인 사례를 들어 설명해 주셔서 실무에 와닿았습니다.",
		"{k}에 관한 핵심을 쉬운 말로 짚어 주어 처음 접하는 사람도 따라가기 좋았습니다.",
		"그중에서도 {k}에 대한 부분은 앞으로 업무에서 꼭 기억하고 싶은 내용이었습니다.",
		"{k}에 대한 설명이 특히 유익했습니다.",
		"{k}에 관한 설명을 듣고 그동안 놓치고 있던 부분을 알게 되었습니다.",
		"이론과 사례가 균형 있게 구성되어 {k}에 대해 깊이 있게 이해할 수 있었습니다." };
	private static readonly string[] CT = {
		"앞으로 업무에서 {u} 한층 책임 있게 일하겠습니다.",
		"배운 내용을 바탕으로 {u} 맡은 일을 더욱 충실히 해내겠습니다.",
		"수강 후에는 {u} 업무에 임해야겠다는 다짐을 하게 되었습니다.",
		"일상 업무에서 {u} 배운 내용을 꾸준히 실천하겠습니다.",
		"이번 교육을 계기로 {u} 업무 완성도를 높여 가겠습니다.",
		"{u} 배운 내용을 동료들과도 나누겠습니다.",
		"앞으로 {u} 일하겠습니다." };
	private static Tp Find(string name)
	{
		string n = (name ?? "").ToLowerInvariant().Replace(" ", ""); Tp best = null; int bs = 0;
		foreach (Tp t in Tps) { int sc = 0; foreach (string k in t.Kw) if (n.Contains(k)) sc += k.Length; if (sc > bs) { bs = sc; best = t; } }
		return best;
	}
	internal static string Topic(string name) { Tp t = Find(name); return t == null ? null : t.Name; }
	internal static string Make(string name, int min, int max)
	{
		name = (name ?? "").Trim().Trim('「', '」', '"', '\''); bool none = name.Length == 0; Tp tp = none ? null : Find(name);
		List<string> Bs = new List<string>(B), Cs = new List<string>(C);
		if (tp != null) { Bs.Clear(); Cs.Clear(); foreach (string b in BT) foreach (string k in tp.K) Bs.Add(b.Replace("{k}", k)); foreach (string c in CT) foreach (string u in tp.U) Cs.Add(c.Replace("{u}", u)); }
		List<string> ok = new List<string>(); string best = null; int bd = int.MaxValue;
		foreach (string a in A) foreach (string b in Bs) foreach (string c in Cs) foreach (string d in D)
		{
			string s = a + " " + b + " " + c + " " + d; s = none ? s.Replace("「{n}」", "이번") : s.Replace("{n}", name);
			int dv = s.Length < min ? min - s.Length : (s.Length > max ? s.Length - max : 0);
			if (dv == 0) ok.Add(s); else if (dv < bd) { bd = dv; best = s; }
		}
		return ok.Count > 0 ? ok[R.Next(ok.Count)] : best;
	}
}

internal sealed class ReviewForm : Form
{
	private static ReviewForm inst;
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new ReviewForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }
	private readonly TextBox tName = new TextBox(), tOut = new TextBox(); private readonly Label lCnt = new Label();
	public ReviewForm()
	{
		float s = FUi.Scale(420, 360); Text = "수강후기 도우미 (200~210자)"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(420 * s), (int)(330 * s));
		Font = new Font("맑은 고딕", 9.5f * s); BackColor = Color.FromArgb(246, 249, 252);
		Label l1 = new Label { Text = "강의명", Left = (int)(14 * s), Top = (int)(16 * s), Width = (int)(50 * s), Height = (int)(22 * s) };
		tName.Left = (int)(66 * s); tName.Top = (int)(13 * s); tName.Width = (int)(250 * s);
		Button go = new Button { Text = "후기 만들기", Left = (int)(324 * s), Top = (int)(11 * s), Width = (int)(82 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		tOut.Left = (int)(14 * s); tOut.Top = (int)(50 * s); tOut.Width = (int)(392 * s); tOut.Height = (int)(210 * s); tOut.Multiline = true; tOut.ScrollBars = ScrollBars.Vertical; tOut.WordWrap = true;
		lCnt.Left = (int)(14 * s); lCnt.Top = (int)(268 * s); lCnt.Width = (int)(300 * s); lCnt.Height = (int)(22 * s); lCnt.ForeColor = Color.FromArgb(80, 110, 150);
		Button cp = new Button { Text = "복사", Left = (int)(324 * s), Top = (int)(264 * s), Width = (int)(82 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		Label tip = new Label { Text = "공백 포함 글자 수예요. 마음에 안 들면 다시 눌러 새로 만들어요. 내용은 꼭 직접 읽고 본인 경험에 맞게 고쳐 쓰세요.", Left = (int)(14 * s), Top = (int)(296 * s), Width = (int)(392 * s), Height = (int)(30 * s), ForeColor = Color.Gray, Font = new Font("맑은 고딕", 8f * s) };
		Action make = delegate { tOut.Text = ReviewGen.Make(tName.Text, 200, 210); Cnt(); string tp = ReviewGen.Topic(tName.Text); if (tp != null) lCnt.Text += " · 분야: " + tp; };
		go.Click += delegate { make(); }; tName.KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; make(); } };
		cp.Click += delegate { try { if (tOut.Text.Length > 0) { Clipboard.SetText(tOut.Text); lCnt.Text = "복사했어요 · " + tOut.Text.Length + "자"; } } catch { } };
		tOut.TextChanged += delegate { Cnt(); };
		Controls.AddRange(new Control[] { l1, tName, go, tOut, lCnt, cp, tip }); AcceptButton = null;
	}
	private void Cnt() { int n = tOut.Text.Length; lCnt.Text = n + "자"; lCnt.ForeColor = n >= 200 && n <= 210 ? Color.FromArgb(40, 140, 80) : Color.FromArgb(200, 90, 60); }
}
