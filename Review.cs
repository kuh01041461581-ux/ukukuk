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
	internal static int Len(string s) { return s.Length; }
	internal static string Make(string name, int min, int max)
	{
		name = (name ?? "").Trim().Trim('「', '」', '"', '\''); bool none = name.Length == 0;
		string[][] P = { A, B, C, D }; List<string> ok = new List<string>(); string best = null; int bd = int.MaxValue;
		foreach (string a in A) foreach (string b in B) foreach (string c in C) foreach (string d in D)
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
		lCnt.Left = (int)(14 * s); lCnt.Top = (int)(268 * s); lCnt.Width = (int)(200 * s); lCnt.Height = (int)(22 * s); lCnt.ForeColor = Color.FromArgb(80, 110, 150);
		Button cp = new Button { Text = "복사", Left = (int)(324 * s), Top = (int)(264 * s), Width = (int)(82 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		Label tip = new Label { Text = "공백 포함 글자 수예요. 마음에 안 들면 다시 눌러 새로 만들어요. 내용은 꼭 직접 읽고 본인 경험에 맞게 고쳐 쓰세요.", Left = (int)(14 * s), Top = (int)(296 * s), Width = (int)(392 * s), Height = (int)(30 * s), ForeColor = Color.Gray, Font = new Font("맑은 고딕", 8f * s) };
		Action make = delegate { tOut.Text = ReviewGen.Make(tName.Text, 200, 210); Cnt(); };
		go.Click += delegate { make(); }; tName.KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; make(); } };
		cp.Click += delegate { try { if (tOut.Text.Length > 0) { Clipboard.SetText(tOut.Text); lCnt.Text = "복사했어요 · " + tOut.Text.Length + "자"; } } catch { } };
		tOut.TextChanged += delegate { Cnt(); };
		Controls.AddRange(new Control[] { l1, tName, go, tOut, lCnt, cp, tip }); AcceptButton = null;
	}
	private void Cnt() { int n = tOut.Text.Length; lCnt.Text = n + "자"; lCnt.ForeColor = n >= 200 && n <= 210 ? Color.FromArgb(40, 140, 80) : Color.FromArgb(200, 90, 60); }
}
