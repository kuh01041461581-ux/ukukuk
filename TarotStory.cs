using System;
using System.Collections.Generic;
using System.Text;

// ── 타로 흐름 풀이: 카드를 하나씩 나열하지 않고, 카드들이 이어지며 만드는 이야기를 사주·점 보듯 줄글로 풀어요 ──────────
internal sealed partial class TarotForm
{
	private static string FS(string t) { if (string.IsNullOrEmpty(t)) return ""; int i = t.IndexOf('.'); return i > 0 ? t.Substring(0, i + 1) : t; }
	private string Cd(int i) { Slot s = slots[i]; return "'" + CardName(s.Card) + (s.Rev && s.Card < 22 ? "(역)" : "") + "' 카드(" + CardKw(s.Card) + ")"; }
	private string CdS(int i) { Slot s = slots[i]; return "'" + CardName(s.Card) + (s.Rev && s.Card < 22 ? "(역)" : "") + "'"; }
	private string Intro(int i, int k, int g)
	{
		string c = CdS(i) + " 카드", kw = CardKw(slots[i].Card), p = posN[i]; int v = (i + g) % 3;
		if (k == 0) return v == 0 ? p + " 자리에는 " + c + "가 놓였어요." : (v == 1 ? p + " 자리를 차지한 " + c + "는 '" + kw + "'의 기운이에요." : p + " 자리에서는 " + c + "가 중심에 서요. 키워드는 '" + kw + "'예요.");
		return v == 0 ? "여기에 " + p + " 자리의 " + c + "가 더해져요." : (v == 1 ? "곁에는 " + p + " 자리의 " + c + "가 있어요." : "그리고 " + p + " 자리의 " + c + "는 '" + kw + "' 쪽의 이야기예요.");
	}
	private string Say(int i) { Slot s = slots[i]; return FS(Meaning(s.Card, s.Rev)); }
	private int Sc(int i) { return YesScore(slots[i].Card, slots[i].Rev); }
	private static readonly int[][] ElPairIdx = { new[] { 0, 3 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 1, 2 }, new[] { 2, 3 } };
	private static string AfterFirst(string s) { int k = s.IndexOf(". "); return k >= 0 ? s.Substring(k + 2) : s; }

	// 두 카드가 이어질 때 생기는 의미
	private string PairText(int i, int j)
	{
		int a = slots[i].Card, b = slots[j].Card;
		for (int p = 0; p < PairMaj.Length; p++) if ((PairMaj[p][0] == a && PairMaj[p][1] == b) || (PairMaj[p][1] == a && PairMaj[p][0] == b)) return AfterFirst(PairTx[p]);
		if (a >= 22 && b >= 22)
		{
			int xa = (a - 22) / 14, xb = (b - 22) / 14;
			if (xa == xb) return "같은 계열의 카드가 이어져서 이 이야기가 한 가지 주제로 깊어져요.";
			for (int k = 0; k < 6; k++) if ((ElPairIdx[k][0] == xa && ElPairIdx[k][1] == xb) || (ElPairIdx[k][0] == xb && ElPairIdx[k][1] == xa)) return ElPair[k];
		}
		int sa = Sc(i), sb = Sc(j); int ca = sa >= 1 ? 0 : (sa <= -1 ? 2 : 1), cb = sb >= 1 ? 0 : (sb <= -1 ? 2 : 1); return FS(Trans[ca * 3 + cb]);
	}
	private static string Tone(double v) { return v >= 1.2 ? "기운이 크게 열려 있어요." : (v >= 0.5 ? "대체로 순풍이에요." : (v > -0.4 ? "좋은 일과 어려움이 고르게 섞여 있어요." : (v > -1.0 ? "조심스럽게 살펴야 할 부분이 많아요." : "시험과 점검이 이어져요."))); }
	private string TopicOpen()
	{
		switch (topic) { case 1: return "연애와 관계 이야기로 읽어 볼게요."; case 2: return "일과 학업 이야기로 읽어 볼게요."; case 3: return "돈과 재물 이야기로 읽어 볼게요."; case 4: return "몸과 컨디션 이야기로 읽어 볼게요."; default: return "지금 마음에 품은 질문의 전체 그림을 읽어 볼게요."; }
	}
	private string Close(int[] sc, int N)
	{
		double av = 0; int best = 0, worst = 0; for (int i = 0; i < N; i++) { av += sc[i]; if (sc[i] > sc[best]) best = i; if (sc[i] < sc[worst]) worst = i; } av /= N;
		StringBuilder sb = new StringBuilder(); sb.Append(Verd[topic * 3 + (av >= 0.6 ? 0 : (av <= -0.4 ? 2 : 1))]);
		if (sc[best] > sc[worst]) { string ak = Act(slots[best].Card); sb.Append(" 가장 큰 힘이 되는 카드는 ").Append(CdS(best)).Append("(").Append(posN[best]).Append(")예요. ").Append(ak.Substring(ak.IndexOf('·') + 2).Trim()); if (sc[worst] <= -1) sb.Append(" 반대로 ").Append(CdS(worst)).Append("(").Append(posN[worst]).Append(")의 신호, 그 경고만은 흘려듣지 마세요."); }
		return sb.ToString();
	}

	private string Story()
	{
		int N = slots.Length; if (N < 3 || mode == 4 || mode == 5 || mode == 8) return null; if (mode == 7) return YearStory();
		int[] sc = new int[N]; double av = 0; int mj = 0, rc = 0; for (int i = 0; i < N; i++) { sc[i] = Sc(i); av += sc[i]; if (slots[i].Card < 22) mj++; if (slots[i].Rev) rc++; } av /= N;
		int[][] G; string[] lead;
		switch (mode)
		{
			case 6: G = new[] { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6, 7 }, new[] { 8 }, new[] { 9 } }; lead = new[] { "먼저 지금 놓인 장면의 중심부터 볼게요.", "그 장면이 어디서 왔는지 뿌리를 따라가 볼게요.", "이제 시선을 앞쪽으로 옮겨 볼게요.", "한편 이 일을 대하는 나와 주변의 모습이에요.", "마음 깊은 곳의 목소리도 들어 볼게요.", "그래서 이 이야기가 닿는 결론이에요." }; break;
			case 1: G = new[] { new[] { 0 }, new[] { 1 }, new[] { 2 } }; lead = new[] { "이야기의 출발점부터 볼게요.", "그 위에 지금의 장면이 겹쳐요.", "그리고 이 흐름이 향하는 곳을 볼게요." }; break;
			case 2: G = new[] { new[] { 0, 1 }, new[] { 2 } }; lead = new[] { "먼저 두 사람의 마음부터 볼게요.", "그래서 앞으로의 흐름을 이어서 볼게요." }; break;
			case 3: G = new[] { new[] { 0, 1 }, new[] { 2 }, new[] { 3, 4 } }; lead = new[] { "지금의 상황과 그 앞을 막는 것부터 볼게요.", "이 국면을 푸는 열쇠를 찾아볼게요.", "그러면 곧 이어질 장면과 결과를 볼게요." }; break;
			case 9: G = new[] { new[] { 0, 2 }, new[] { 1 }, new[] { 3, 4 } }; lead = new[] { "먼저 나와 상대의 모습부터 볼게요.", "두 사람 사이에 흐르는 기운을 볼게요.", "함께 풀어 갈 숙제와 가꾸는 방법을 볼게요." }; break;
			case 10: G = new[] { new[] { 0, 1 }, new[] { 2, 3, 4 }, new[] { 5, 6 } }; lead = new[] { "지나온 길과 지금의 자리부터 볼게요.", "눈에 보이지 않는 영향과 걸림돌, 주변 환경을 볼게요.", "그래서 취할 행동과 가능한 결과를 볼게요." }; break;
			default: { G = new int[N][]; lead = new string[N]; for (int i = 0; i < N; i++) { G[i] = new[] { i }; lead[i] = i == 0 ? "먼저 처음 카드부터 볼게요." : (i == N - 1 ? "마지막으로 이야기가 향하는 곳이에요." : "이어서,"); } } break;
		}
		StringBuilder sb = new StringBuilder();
		sb.Append(TopicOpen()).Append(" 뽑힌 ").Append(N).Append("장 중 ").Append(mj >= (N + 1) / 2 ? "메이저가 " + mj + "장이라 개인의 노력보다 큰 시기와 흐름이 이번 일을 좌우해요" : (mj == 0 ? "큰 사건을 뜻하는 메이저가 없어서 내 선택과 노력으로 바꿀 수 있는 일상의 이야기예요" : "메이저가 " + mj + "장이라 큰 흐름과 일상의 노력이 함께 작용해요")).Append(". 전체적으로 ").Append(Tone(av)).Append(rc == 0 ? " 모든 카드가 정방향이라 기운이 밖으로 시원하게 흘러요." : (rc * 2 > N ? " 역방향이 많아 겉으로는 잘 안 보이는 내면의 사정이 크게 작용해요." : " 역방향이 " + rc + "장 섞여 있어 그 자리만큼은 한 번 더 살펴야 해요.")).Append("\n\n");
		for (int g = 0; g < G.Length; g++)
		{
			int[] ix = G[g]; sb.Append(lead[g]).Append(' ');
			for (int k = 0; k < ix.Length; k++)
			{
				int i = ix[k];
				sb.Append(Intro(i, k, g)).Append(' ');
				sb.Append(Say(i)).Append(' ');
				if (k + 1 < ix.Length) { }
			}
			if (ix.Length >= 2) sb.Append(new[] { "두 카드를 함께 읽으면, ", "이 둘이 만나면, ", "겹쳐 보면, " }[g % 3]).Append(PairText(ix[0], ix[ix.Length - 1])).Append(' ');
			if (g + 1 < G.Length)
			{
				int a = ix[ix.Length - 1], b = G[g + 1][0]; int d = sc[b] - sc[a];
				string[][] TR = { new[] { "여기서 분위기가 한 번 크게 바뀌며 시험이 와요.", "흐름이 크게 꺾이며 시험의 구간으로 들어가요.", "이 지점에서 분위기가 확 달라져요." }, new[] { "조금씩 무게가 실리는 쪽으로 이어져요.", "살짝 긴장이 더해지는 쪽으로 넘어가요.", "다음 장면에서는 조금 더 신중해야 해요." }, new[] { "비슷한 결로 이어져요.", "분위기는 그대로 이어져요.", "" }, new[] { "흐름이 조금씩 나아지는 쪽으로 기울어요.", "점점 숨통이 트이는 쪽으로 이어져요.", "한 걸음씩 나아지는 방향이에요." }, new[] { "이 장면은 한결 밝은 쪽으로 이어져요.", "여기서 분위기가 눈에 띄게 밝아져요.", "이야기가 한결 가벼워지는 쪽으로 넘어가요." } };
				sb.Append(TR[Math.Max(-2, Math.Min(2, d)) + 2][g % 3]);
			}
			sb.Append("\n\n");
		}
		sb.Append("정리하면, ").Append(Close(sc, N));
		return sb.ToString();
	}

	// ───────── 1년 12달: 계절을 따라 흐르는 이야기 ─────────
	private string YearStory()
	{
		int N = 12; int[] sc = new int[N]; int mj = 0, rc = 0; int[] su = new int[4]; double av = 0;
		for (int i = 0; i < N; i++) { sc[i] = Sc(i); av += sc[i]; if (slots[i].Card < 22) mj++; else su[(slots[i].Card - 22) / 14]++; if (slots[i].Rev) rc++; } av /= N;
		StringBuilder sb = new StringBuilder();
		sb.Append("한 해의 큰 그림부터 말씀드릴게요. ");
		if (mj >= 5) sb.Append("열두 장 중 메이저가 ").Append(mj).Append("장이나 나와서, 내 노력만이 아니라 시기와 운이 크게 작용하는 해예요. 굵직한 만남과 전환이 여러 번 찾아오니, 흐름이 오면 망설이지 말고 올라타세요.");
		else if (mj >= 2) sb.Append("메이저가 ").Append(mj).Append("장이라 뚜렷한 변곡점이 몇 번 있는 해예요. 그 달에는 평소보다 신중하게 선택하세요.");
		else sb.Append("메이저가 ").Append(mj == 0 ? "한 장도 없어서" : "한 장뿐이라").Append(" 큰 사건보다는 일상 속 노력이 차곡차곡 결과를 만드는 해예요. 꾸준함이 곧 운이에요.");
		int bs = 0; for (int i = 1; i < 4; i++) if (su[i] > su[bs]) bs = i;
		if (su[bs] >= 3) sb.Append(" 올해 가장 자주 나온 기운은 ").Append(SuitDom[bs].Replace(" 쪽", "")).Append(" 이야기(").Append(su[bs]).Append("장)라서, 한 해의 중심 무대가 그쪽이에요.");
		sb.Append(" 전체적으로 ").Append(Tone(av)).Append(rc == 0 ? " 열두 달 모두 정방향이라 기운이 밖으로 시원하게 흘러요." : (rc >= 5 ? " 역방향이 " + rc + "장이라 겉으로 드러나지 않은 점검거리가 많은 해예요." : " 역방향이 " + rc + "장 섞여 있어 그 달만 한 번 더 살피면 돼요."));
		sb.Append("\n\n");
		string[] qn = { "1~3월", "4~6월", "7~9월", "10~12월" }, qs = { "겨울의 끝에서 봄의 문턱으로", "봄에서 초여름으로", "한여름에서 가을의 문턱으로", "가을에서 겨울로" };
		double[] qa = new double[4];
		for (int q = 0; q < 4; q++)
		{
			int a = q * 3; qa[q] = (sc[a] + sc[a + 1] + sc[a + 2]) / 3.0; int pk = a, dp = a; for (int i = a; i < a + 3; i++) { if (sc[i] > sc[pk]) pk = i; if (sc[i] < sc[dp]) dp = i; }
			sb.Append("[").Append(qn[q]).Append(" · ").Append(qs[q]).Append("] ");
			if (q == 0) sb.Append("한 해는 ").Append(Cd(0)).Append("로 문을 열어요. "); else { double d = qa[q] - qa[q - 1]; sb.Append(d >= 0.8 ? "앞선 석 달보다 분위기가 한층 밝아져요. " : (d <= -0.8 ? "앞선 석 달보다 힘이 한 풀 꺾여요. " : "앞의 흐름이 비슷한 결로 이어져요. ")); }
			sb.Append(a + 1).Append("월에는 ").Append(CdS(a)).Append("(").Append(CardKw(slots[a].Card)).Append(")의 기운으로 시작해, ").Append(a + 2).Append("월에는 ").Append(CdS(a + 1)).Append("(").Append(CardKw(slots[a + 1].Card)).Append(")의 기운을 지나, ").Append(a + 3).Append("월에는 ").Append(CdS(a + 2)).Append("(").Append(CardKw(slots[a + 2].Card)).Append(")의 기운에 닿아요. ");
			sb.Append(q == 0 ? "한 해의 흐름으로 보면 씨앗을 뿌리는 시기예요. " : (q == 1 ? "한 해의 흐름으로 보면 가꾸고 키우는 시기예요. " : (q == 2 ? "한 해의 흐름으로 보면 무르익어 가는 시기예요. " : "한 해의 흐름으로 보면 거두고 정리하는 시기예요. ")));
			sb.Append(qa[q] >= 0.6 ? "힘을 모아 올라타기 좋은 석 달이에요. " : (qa[q] <= -0.4 ? "속도를 줄이고 기반을 점검하는 석 달이에요. " : "기회와 걸림돌이 섞여 있어 선택이 중요한 석 달이에요. "));
			if (sc[pk] >= 1) sb.Append("특히 ").Append(pk + 1).Append("월의 ").Append(CdS(pk)).Append(" 카드가 이 구간의 열쇠예요. ").Append(Say(pk)).Append(' ');
			if (sc[dp] <= -1 && dp != pk) sb.Append(dp + 1).Append("월의 ").Append(CdS(dp)).Append(" 카드는 주의 신호예요. ").Append(Say(dp)).Append(' ');
			sb.Append("\n\n");
		}
		// 변곡점
		List<string> tp = new List<string>(); for (int i = 0; i + 1 < N && tp.Count < 2; i++) { int d = sc[i + 1] - sc[i]; if (d >= 3 || d <= -3) tp.Add((i + 1) + "월에서 " + (i + 2) + "월로 넘어가며 " + CdS(i) + " 카드에서 " + CdS(i + 1) + " 카드로 분위기가 " + (d > 0 ? "확 밝아져요" : "크게 가라앉아요")); }
		if (tp.Count > 0) sb.Append("올해의 변곡점은 이래요. ").Append(string.Join(". 또 ", tp.ToArray())).Append(". 이 시기에는 계획을 한 번 더 점검하고 서두르지 마세요.\n\n");
		// 영역 이동
		int[] h1 = new int[4], h2 = new int[4]; for (int i = 0; i < N; i++) if (slots[i].Card >= 22) { if (i < 6) h1[(slots[i].Card - 22) / 14]++; else h2[(slots[i].Card - 22) / 14]++; }
		int b1 = 0, b2 = 0; for (int i = 1; i < 4; i++) { if (h1[i] > h1[b1]) b1 = i; if (h2[i] > h2[b2]) b2 = i; }
		if (h1[b1] > 0 && h2[b2] > 0) sb.Append(b1 != b2 ? "상반기에는 " + SuitDom[b1].Replace(" 쪽", "") + " 이야기가 중심이다가, 하반기에는 " + SuitDom[b2].Replace(" 쪽", "") + " 쪽으로 무게중심이 옮겨 가요. 계절이 바뀌듯 관심사도 자연스럽게 옮겨 가니 억지로 붙들지 마세요.\n\n" : "상반기와 하반기 모두 " + SuitDom[b1].Replace(" 쪽", "") + " 이야기가 중심이에요. 한 해 내내 같은 주제가 이어지니 깊이 있게 다룰수록 성과가 커져요.\n\n");
		int best = 0, worst = 0; for (int i = 1; i < N; i++) { if (sc[i] > sc[best]) best = i; if (sc[i] < sc[worst]) worst = i; }
		sb.Append("가장 순조로운 달은 ").Append(best + 1).Append("월(").Append(CdS(best)).Append("), 가장 조심할 달은 ").Append(worst + 1).Append("월(").Append(CdS(worst)).Append(")이에요. ");
		string ak = Act(slots[best].Card); sb.Append(ak.Substring(ak.IndexOf('·') + 2).Trim()).Append("\n\n");
		sb.Append("정리하면, ").Append(Verd[topic * 3 + (av >= 0.6 ? 0 : (av <= -0.4 ? 2 : 1))]);
		return sb.ToString();
	}
}
