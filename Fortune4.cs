using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// 행운 아이템: 물건 이름 + 간단한 그림 (직접 그려요)
internal partial class FortuneForm
{
	private static readonly string[] LuckName = { "따뜻한 차 한 잔", "손수건", "작은 메모지", "초콜릿 한 조각", "열쇠", "우산", "안경", "손목시계", "연필", "책 한 권", "작은 화분", "텀블러", "동전 지갑", "막대 사탕", "네잎클로버", "행운의 동전" };
	private static readonly int[][] LuckPool = { new[] { 10, 8, 9, 1, 14 }, new[] { 0, 13, 3, 4, 14 }, new[] { 2, 12, 11, 9, 15 }, new[] { 7, 6, 4, 12, 15 }, new[] { 5, 11, 1, 0, 14, 15 } };
	private static readonly string[] LuckTip = { "책상 위에 올려 두고 천천히 마셔요.", "가방이나 주머니에 넣어 다녀요.", "오늘 할 일을 세 줄만 적어 보세요.", "지치는 오후에 한 조각만 먹어요.", "눈에 띄는 곳에 걸어 두세요.", "비 소식이 없어도 챙겨 두면 든든해요.", "깨끗이 닦아서 써요.", "약속 시간 5분 전에 도착하도록 맞춰요.", "깎아서 필통에 꽂아 두세요.", "가방에 한 권 넣어 다녀요.", "햇볕이 드는 곳에 두고 물을 줘요.", "물을 자주 채워 마셔요.", "동전을 한 개 넣어 두세요.", "달콤하게 기분 전환해요.", "지갑이나 책 사이에 끼워 두세요.", "주머니에 넣고 만지작거려 보세요." };

	private void LuckIcon(Graphics g, int id, RectangleF r, Color c)
	{
		{ Bitmap ib = Spr.Get("ms_luck_" + id); if (ib != null) { float k = Math.Min(r.Width / ib.Width, r.Height / ib.Height) * 0.92f; Spr.Draw(g, "ms_luck_" + id, r.X + r.Width / 2f, r.Y + r.Height / 2f, ib.Width * k, ib.Height * k, 0f); return; } }   // 그림이 있으면 그림으로
		float u = Math.Min(r.Width, r.Height) / 64f; float ox = r.X + (r.Width - 64f * u) / 2f, oy = r.Y + (r.Height - 64f * u) / 2f;
		Func<float, float> X = v => ox + v * u; Func<float, float> Y = v => oy + v * u;
		Color soft = Color.FromArgb(70, c), mid = Color.FromArgb(150, c), ink = Theme.Ink;
		using (Pen pen = new Pen(c, 2.4f * u) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
		using (Pen thin = new Pen(Color.FromArgb(200, ink), 1.6f * u) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
		using (SolidBrush fs = new SolidBrush(soft)) using (SolidBrush fm = new SolidBrush(mid)) using (SolidBrush fi = new SolidBrush(Color.FromArgb(220, ink)))
		{
			switch (id)
			{
				case 0: // 찻잔
					g.FillRectangle(fs, X(14), Y(26), 30 * u, 22 * u); g.DrawRectangle(pen, X(14), Y(26), 30 * u, 22 * u); g.DrawArc(pen, X(40), Y(30), 14 * u, 12 * u, -90, 180); g.DrawLine(pen, X(10), Y(52), X(48), Y(52));
					g.DrawBezier(thin, X(24), Y(20), X(20), Y(14), X(28), Y(12), X(24), Y(6)); g.DrawBezier(thin, X(34), Y(20), X(30), Y(14), X(38), Y(12), X(34), Y(6)); break;
				case 1: // 손수건
					g.FillPolygon(fs, new[] { new PointF(X(10), Y(14)), new PointF(X(54), Y(14)), new PointF(X(54), Y(50)), new PointF(X(10), Y(50)) }); g.DrawRectangle(pen, X(10), Y(14), 44 * u, 36 * u);
					g.FillPolygon(fm, new[] { new PointF(X(36), Y(50)), new PointF(X(54), Y(50)), new PointF(X(54), Y(32)) }); g.DrawLine(thin, X(36), Y(50), X(54), Y(32)); g.DrawLine(thin, X(16), Y(22), X(34), Y(22)); break;
				case 2: // 메모지
					g.FillRectangle(fs, X(14), Y(10), 36 * u, 46 * u); g.DrawRectangle(pen, X(14), Y(10), 36 * u, 46 * u); for (int i = 0; i < 4; i++) g.DrawLine(thin, X(20), Y(24 + i * 8), X(44), Y(24 + i * 8)); g.FillRectangle(fm, X(14), Y(10), 36 * u, 8 * u); break;
				case 3: // 초콜릿
					g.FillRectangle(fm, X(12), Y(14), 40 * u, 36 * u); g.DrawRectangle(pen, X(12), Y(14), 40 * u, 36 * u); for (int i = 1; i < 4; i++) g.DrawLine(thin, X(12 + i * 10), Y(14), X(12 + i * 10), Y(50)); for (int j = 1; j < 3; j++) g.DrawLine(thin, X(12), Y(14 + j * 12), X(52), Y(14 + j * 12)); break;
				case 4: // 열쇠
					g.FillEllipse(fs, X(10), Y(14), 22 * u, 22 * u); g.DrawEllipse(pen, X(10), Y(14), 22 * u, 22 * u); g.DrawEllipse(thin, X(17), Y(21), 8 * u, 8 * u); g.DrawLine(pen, X(30), Y(32), X(54), Y(52)); g.DrawLine(pen, X(46), Y(46), X(42), Y(52)); g.DrawLine(pen, X(52), Y(51), X(48), Y(56)); break;
				case 5: // 우산
					g.FillPie(fs, X(8), Y(10), 48 * u, 44 * u, 180, 180); g.DrawArc(pen, X(8), Y(10), 48 * u, 44 * u, 180, 180); g.DrawLine(pen, X(32), Y(32), X(32), Y(52)); g.DrawArc(pen, X(26), Y(46), 12 * u, 12 * u, 0, 180); for (int i = 0; i < 2; i++) g.DrawArc(thin, X(8 + i * 16), Y(26), 16 * u, 10 * u, 0, 180); break;
				case 6: // 안경
					g.FillEllipse(fs, X(6), Y(24), 22 * u, 20 * u); g.FillEllipse(fs, X(36), Y(24), 22 * u, 20 * u); g.DrawEllipse(pen, X(6), Y(24), 22 * u, 20 * u); g.DrawEllipse(pen, X(36), Y(24), 22 * u, 20 * u); g.DrawArc(pen, X(26), Y(26), 12 * u, 8 * u, 180, 180); g.DrawLine(pen, X(6), Y(30), X(2), Y(24)); g.DrawLine(pen, X(58), Y(30), X(62), Y(24)); break;
				case 7: // 손목시계
					g.FillRectangle(fm, X(24), Y(4), 16 * u, 56 * u); g.FillEllipse(fi, X(12), Y(16), 40 * u, 40 * u); g.FillEllipse(new SolidBrush(Color.FromArgb(250, 252, 250)), X(15), Y(19), 34 * u, 34 * u); g.DrawEllipse(pen, X(12), Y(16), 40 * u, 40 * u); g.DrawLine(thin, X(32), Y(36), X(32), Y(26)); g.DrawLine(thin, X(32), Y(36), X(40), Y(40)); break;
				case 8: // 연필
					g.FillPolygon(fs, new[] { new PointF(X(46), Y(8)), new PointF(X(56), Y(18)), new PointF(X(22), Y(52)), new PointF(X(12), Y(52)), new PointF(X(12), Y(42)) }); g.DrawPolygon(pen, new[] { new PointF(X(46), Y(8)), new PointF(X(56), Y(18)), new PointF(X(22), Y(52)), new PointF(X(10), Y(54)), new PointF(X(12), Y(42)) });
					g.DrawLine(thin, X(40), Y(14), X(50), Y(24)); g.FillPolygon(fi, new[] { new PointF(X(10), Y(54)), new PointF(X(12), Y(44)), new PointF(X(20), Y(52)) }); break;
				case 9: // 책
					g.FillRectangle(fs, X(10), Y(10), 44 * u, 46 * u); g.DrawRectangle(pen, X(10), Y(10), 44 * u, 46 * u); g.FillRectangle(fm, X(10), Y(10), 8 * u, 46 * u); for (int i = 0; i < 3; i++) g.DrawLine(thin, X(24), Y(22 + i * 8), X(48), Y(22 + i * 8)); break;
				case 10: // 화분
					g.FillPolygon(fm, new[] { new PointF(X(18), Y(38)), new PointF(X(46), Y(38)), new PointF(X(42), Y(56)), new PointF(X(22), Y(56)) }); g.DrawPolygon(pen, new[] { new PointF(X(18), Y(38)), new PointF(X(46), Y(38)), new PointF(X(42), Y(56)), new PointF(X(22), Y(56)) });
					g.DrawLine(pen, X(32), Y(38), X(32), Y(20)); g.FillEllipse(fs, X(14), Y(12), 18 * u, 12 * u); g.FillEllipse(fs, X(32), Y(8), 18 * u, 12 * u); g.DrawEllipse(thin, X(14), Y(12), 18 * u, 12 * u); g.DrawEllipse(thin, X(32), Y(8), 18 * u, 12 * u); break;
				case 11: // 텀블러
					g.FillPolygon(fs, new[] { new PointF(X(18), Y(14)), new PointF(X(46), Y(14)), new PointF(X(42), Y(56)), new PointF(X(22), Y(56)) }); g.DrawPolygon(pen, new[] { new PointF(X(18), Y(14)), new PointF(X(46), Y(14)), new PointF(X(42), Y(56)), new PointF(X(22), Y(56)) });
					g.FillRectangle(fm, X(16), Y(8), 32 * u, 8 * u); g.DrawRectangle(pen, X(16), Y(8), 32 * u, 8 * u); g.DrawLine(thin, X(24), Y(28), X(40), Y(28)); break;
				case 12: // 동전 지갑
					g.FillEllipse(fs, X(8), Y(20), 48 * u, 36 * u); g.DrawEllipse(pen, X(8), Y(20), 48 * u, 36 * u); g.DrawLine(pen, X(14), Y(24), X(50), Y(24)); g.FillEllipse(fm, X(26), Y(32), 12 * u, 12 * u); g.DrawEllipse(thin, X(26), Y(32), 12 * u, 12 * u); break;
				default: // 막대 사탕
					g.DrawLine(pen, X(34), Y(34), X(50), Y(56)); g.FillEllipse(fm, X(10), Y(6), 38 * u, 38 * u); g.DrawEllipse(pen, X(10), Y(6), 38 * u, 38 * u); g.DrawArc(thin, X(18), Y(14), 22 * u, 22 * u, 0, 270); g.DrawArc(thin, X(24), Y(20), 10 * u, 10 * u, 180, 270); break;
			}
		}
	}
	// 오늘의 행운템 (물건) 고르기
	private static int LuckItem(int el, string seed) { int[] p = LuckPool[el % 5]; return p[Saju.Hash(seed + "i") % p.Length]; }

	// ───────── 사주 심화 풀이: 일간의 힘 · 십성 구조 · 용신 · 지지 합충 · MBTI 연결 ─────────
	private static readonly string[] StrTx = {
		"일간이 매우 약한 사주예요. 환경과 사람의 영향을 크게 받아서, 혼자 밀어붙이기보다 받쳐 주는 선배·가족·팀 안에서 실력이 나와요. 무리한 도전은 체력과 휴식부터 챙긴 뒤에 하세요.",
		"일간이 약한 편이에요. 책임이 한꺼번에 몰리면 지치기 쉬워서 일을 나누고 쉬는 시간을 먼저 정해 두면 좋아요. 배움·자격·좋은 멘토(인성)가 큰 힘이 돼요.",
		"힘의 균형이 잘 잡힌 사주예요. 상황에 맞춰 밀고 당기는 유연함이 강점이고 어느 길을 가도 무난히 적응해요. 대신 한 방향을 정해 집중해야 성과가 눈에 띄어요.",
		"일간이 강한 편이에요. 추진력과 주관이 뚜렷해서 일을 주도할 때 빛나요. 내 방식만 고집하면 부딪히기 쉬우니, 힘을 쏟을 곳(재능·재물·책임)이 있을수록 운이 좋아져요.",
		"일간이 매우 강한 사주예요. 에너지가 넘쳐 한 가지에 꽂히면 끝까지 가요. 힘을 빼는 통로(운동·창작·사업·책임 있는 역할)가 없으면 답답함이 대인관계 충돌로 번지니 의식적으로 써 주세요." };
	private static readonly string[] StrName = { "극신약", "신약", "중화", "신강", "극신강" };
	private static readonly string[] GrName = { "비겁", "식상", "재성", "관성", "인성" };
	private static readonly string[] GrMany = {
		"비겁이 많아 독립심과 승부욕이 강해요. 동료·친구와 경쟁하거나 돈이 새기 쉬우니 동업·보증은 계약서로 선을 그으세요.",
		"식상이 많아 표현력·재능·말솜씨가 풍부해요. 말이 앞서 구설이 생기거나 윗사람과 마찰이 날 수 있으니 결과물로 증명하세요.",
		"재성이 많아 현실 감각과 돈·기회를 보는 눈이 밝아요. 욕심이 앞서면 몸이 지치기 쉬우니 체력과 시간을 같이 관리하세요.",
		"관성이 많아 책임감·체면·규칙 의식이 강해요. 남의 시선과 압박을 크게 느끼니 '해야 해'를 '하고 싶어'로 바꿀 영역을 따로 만드세요.",
		"인성이 많아 생각이 깊고 배움을 좋아하지만 실행이 느려질 수 있어요. 완벽히 준비하려 하지 말고 70%에서 시작해 보세요." };
	private static readonly string[] GrNone = {
		"비겁이 없어서 내 편이 되어 줄 동료의 힘이 약하다고 느끼기 쉬워요. 대신 먼저 협력을 청하는 습관이 큰 복이 돼요.",
		"식상이 없어서 마음속 이야기를 꺼내는 데 시간이 걸려요. 글쓰기·취미·운동처럼 표현하는 연습을 하면 숨은 재능이 열려요.",
		"재성이 없어서 돈과 성과에 대한 집착이 적은 편이에요. 돈 관리는 자동이체·예산표 같은 시스템으로 하면 새는 곳이 막혀요.",
		"관성이 없어서 얽매이는 것을 싫어하고 자유로워요. 조직의 틀 안에서는 답답할 수 있으니 재량이 큰 일이 잘 맞아요.",
		"인성이 없어서 의지할 곳이 적다고 느끼기 쉬워요. 스스로 배우고 자격·학습으로 나만의 뒷받침을 만들면 든든해져요." };
	private static readonly string[] BrRel = {   // [기둥: 0 연지 · 1 월지 · 2 시지][관계: 0 합 · 1 충 · 2 해]
		"어린 시절부터 자연스럽게 어울린 사람이 많고, 집안과 인연이 부드럽게 이어져요.", "집안·고향과 떨어져 일찍 독립하거나 환경이 크게 바뀌는 흐름이 있어요.", "가까운 사람과 사소한 오해가 쌓이기 쉬워요. 말은 한 번 더 확인하세요.",
		"사회·직장에서 사람과 잘 맞물려요. 협업과 소개로 기회가 와요.", "직장과 사생활 사이 충돌이나 이직·부서 이동 같은 변화가 한 번은 와요. 준비해 두면 기회가 돼요.", "직장에서 오해가 쌓이기 쉬워요. 보고와 기록을 남기세요.",
		"자녀·후배와 인연이 좋고 말년이 따뜻해요.", "말년에 환경이 바뀌거나 자녀·후배와 생각 차이가 생길 수 있어요. 대화를 일찍 열어 두세요.", "가까운 사람에게 서운함을 숨기기 쉬워요. 작게 자주 표현하세요." };
	private static readonly string[] MbEI = {
		"E(외향)와 신강이 겹쳐 에너지를 밖으로 크게 쓰는 타입이에요. 앞에 서는 자리에서 빛나지만 남의 속도를 기다리는 연습이 필요해요.",
		"E(외향)지만 사주 힘은 부드러운 편이라 사람 속에서 힘을 얻되 맞추다 지치기 쉬워요. 만남 뒤 혼자 쉬는 시간을 꼭 넣으세요.",
		"I(내향)인데 사주는 신강이라 겉은 조용해도 속에 단단한 고집과 추진력이 있어요. 필요할 때 단호한 모습에 주변이 놀라곤 해요.",
		"I(내향)에 신약 쪽이라 혼자 충전하는 시간이 곧 생산성이에요. 사람 많은 일정 뒤에는 회복 시간을 먼저 일정표에 넣으세요." };
	private static readonly string[] MbNS = {
		"N(직관)에 인성·식상이 어우러져 상상력과 기획력이 뛰어나요. 아이디어를 '오늘 할 한 가지'로 쪼개야 현실이 돼요.",
		"N(직관)이지만 사주는 현실(재성·관성) 쪽이 강해서 큰 그림을 보면서 숫자와 일정까지 챙기는 균형형이에요.",
		"S(감각)에 재성·관성이 겹쳐 현실 감각과 성실함이 돋보여요. 믿고 맡기는 실무형이라 돈·일정 관리에 강해요.",
		"S(감각)인데 사주는 인성·식상이 강해서 꼼꼼한 실행력에 생각과 표현력이 더해져요. 경험을 글이나 말로 정리하면 강점이 커져요." };
	private static readonly string[] MbTF = {
		"T(사고)에 금·수 기운이 겹쳐 판단이 냉정하고 정확해요. 맞는 말에 온도를 한 스푼 더하면 설득력이 두 배가 돼요.",
		"T(사고)인데 일간은 따뜻한 기운이라 논리적이지만 속은 정이 많아요. 차갑게 보일 땐 이유와 함께 마음도 말해 주세요.",
		"F(감정)에 따뜻한 일간이 겹쳐 공감 능력이 아주 커요. 남의 감정을 떠안지 않도록 '내 몫과 네 몫'의 선을 그어 두세요.",
		"F(감정)이지만 일간이 단단한 기운이라 속은 여려도 겉은 단정하고 의리 있어요. 서운함을 참다 폭발하지 않게 작게 자주 표현하세요." };
	private static readonly string[] MbJP = {
		"J(계획)에 관성·인성이 겹쳐 규칙과 계획을 지키는 힘이 강해요. 예상 밖 변수에 스트레스를 받으니 일정에 여유 20%를 남겨 두세요.",
		"J(계획)이지만 사주에 자유로운 기운이 있어 계획 안에서도 즉흥을 즐겨요. 일정표에 '비워 둔 칸'을 넣으면 균형이 잡혀요.",
		"P(탐색)에 식상·비겁이 겹쳐 자유롭고 도전적이에요. 시작은 빠르니 마감을 남이 정해 주는 구조(스터디·약속)를 쓰면 완성률이 올라가요.",
		"P(탐색)이지만 사주에 책임감 기운이 있어 막판 집중력이 대단해요. 몰아치기는 건강을 해치니 중간 점검일 하나만 두세요." };
	private static readonly string[] GkTx = {
		"월지가 나와 같은 기운(비견)이라 '건록격'으로 봐요. 스스로 일어서는 자립심이 중심이고, 독립적으로 일할 때 힘이 나요.",
		"월지가 겁재라 '양인격'의 결이에요. 승부욕과 추진력이 강해요. 충동적인 결정과 금전 보증만 조심하면 큰일을 해내요.",
		"식신격이에요. 월지에서 표현과 생산의 기운이 나와 재능을 꾸준히 쌓아 결과를 내요. 만들고 가꾸는 일에서 복이 따라요.",
		"상관격이에요. 틀을 깨는 아이디어와 말솜씨가 뛰어나요. 창작·기술처럼 개성이 곧 실력인 곳에서 빛나요.",
		"편재격이에요. 큰 흐름을 읽고 사람과 돈을 움직이는 사업가형이에요. 수입 변동에 대비해 저축 비율을 먼저 정하세요.",
		"정재격이에요. 성실하게 쌓아 안정된 재물을 만드는 구조예요. 급한 투자보다 적금처럼 꾸준한 성장이 맞아요.",
		"편관격(칠살격)이에요. 압박 속에서 더 강해져 책임이 큰 자리에서 능력이 나와요. 휴식을 일정에 꼭 넣으세요.",
		"정관격이에요. 원칙과 신용으로 인정받는 구조예요. 조직에서 차근차근 올라가는 길이 맞고 평판이 곧 재산이에요.",
		"편인격이에요. 남다른 직관과 독특한 학문·기술에 끌려요. 한 우물을 파면 전문가가 되니 생각만 길어지지 않게 하세요.",
		"정인격이에요. 배움과 어른의 도움이 따라요. 자격·공부가 곧 기반이 되니 결정을 미루는 습관만 줄이세요." };
	private static readonly string[] SeaTx = {
		"봄(인·묘·진월)에 태어나 목 기운이 오르는 때예요. 시작과 성장의 에너지가 좋아 차분히 마무리하는 힘이 숙제예요.",
		"여름(사·오·미월)에 태어나 불기운이 세요. 조후(한난조습) 원리로 열을 식히는 수·금 기운, 곧 휴식·학습이 큰 도움이 돼요.",
		"가을(신·유·술월)에 태어나 금 기운이 맑고 건조해요. 결단과 정리에 강하니 사람·취미로 물과 온기를 보태세요.",
		"겨울(해·자·축월)에 태어나 물이 깊고 차요. 조후 원리로 햇볕·운동·따뜻한 관계 같은 불·목 기운이 열쇠예요." };
	private string sxKey; private List<string[]> sxList;
	private List<string[]> SajuExtra(int me)
	{
		string key = pil.Y + "," + pil.M + "," + pil.D + "," + pil.H + "," + mbti; if (sxList != null && key == sxKey) return sxList;
		List<string[]> L = new List<string[]>(); int dm = pil.D % 10, de = Saju.SE(dm); double st = Saju.Strength(pil);
		int[] gc = new int[5]; int sg = 0, jg = 0; int[] stems = { pil.Y % 10, pil.M % 10, pil.H >= 0 ? pil.H % 10 : -1 };
		foreach (int sm in stems) if (sm >= 0) { int r = Saju.Rel(de, Saju.SE(sm)); gc[r]++; bool diff = sm % 2 != dm % 2; if (r == 1 && diff) sg++; if (r == 3 && diff) jg++; }
		int[] brs = { pil.Y % 12, pil.M % 12, pil.D % 12, pil.H >= 0 ? pil.H % 12 : -1 };
		foreach (int b in brs) if (b >= 0) { int sm = Saju.BMain[b]; int r = Saju.Rel(de, Saju.SE(sm)); gc[r]++; bool diff = sm % 2 != dm % 2; if (r == 1 && diff) sg++; if (r == 3 && diff) jg++; }
		int grade = st < 0.3 ? 0 : (st < 0.42 ? 1 : (st < 0.58 ? 2 : (st < 0.72 ? 3 : 4)));
		L.Add(new[] { "#", "일간의 힘과 십성 구조" });
		L.Add(new[] { "일간의 힘 · " + StrName[grade], StrTx[grade] + "\n▶ 십성 분포: 비겁 " + gc[0] + " · 식상 " + gc[1] + " · 재성 " + gc[2] + " · 관성 " + gc[3] + " · 인성 " + gc[4] });
		string gt = ""; int mxg = 0; for (int i = 1; i < 5; i++) if (gc[i] > gc[mxg]) mxg = i; if (gc[mxg] >= 3) gt += GrMany[mxg]; int mng = -1; for (int i = 0; i < 5; i++) if (gc[i] == 0) { mng = i; break; } if (mng >= 0) gt += (gt.Length > 0 ? "\n" : "") + GrNone[mng];
		if (gt.Length > 0) L.Add(new[] { "기운이 몰리고 빈 곳", gt });
		string cb = "";
		if (gc[1] >= 1 && gc[2] >= 1) cb += "● 식상생재: 재능과 결과물이 수입으로 이어지는 구조예요. 기술·콘텐츠·서비스처럼 내 결과물이 돈이 되는 일이 잘 맞아요.\n";
		if (gc[3] >= 1 && gc[4] >= 1) cb += "● 관인상생: 조직·자격·학위가 명예로 이어지는 구조예요. 공직·교육·전문직처럼 체계 안에서 인정받아요.\n";
		if (sg >= 1 && jg >= 1) cb += "● 상관견관: 말과 행동이 규칙과 부딪히기 쉬운 구조예요. 윗사람에게는 의견을 '제안과 근거'로 전하면 오히려 신뢰를 얻어요.\n";
		if (gc[2] >= 3 && st < 0.42) cb += "● 재다신약: 기회와 돈은 많이 보이는데 내 힘이 부족한 구조예요. 욕심을 줄이고 체력·시간 안에서 한두 가지만 단단히 하세요.\n";
		if (gc[0] >= 3 && gc[2] >= 1) cb += "● 비겁쟁재: 사람과 돈이 엮이면 손해가 나기 쉬워요. 돈 거래는 기록하고 보증은 피하세요.\n";
		if (cb.Length > 0) L.Add(new[] { "사주의 구조 읽기", cb.TrimEnd('\n') });
		{ int mb = pil.M % 12, mm = Saju.BMain[mb], mr = Saju.Rel(de, Saju.SE(mm)); bool dl = mr == 0 || mr == 4;
		  L.Add(new[] { "격국 · 월지가 만든 성격", GkTx[Saju.TGIndex(dm, mm)] + "\n▶ 격국은 태어난 달의 기운으로 삶의 큰 방향을 읽는 자평명리의 핵심 원리예요." });
		  L.Add(new[] { "월령과 계절의 조후", SeaTx[((mb + 10) % 12) / 3] + "\n▶ " + (dl ? "월령을 얻었어요(득령). 달의 기운이 나를 도와 뿌리가 단단해요." : "월령을 얻지 못했어요(실령). 스스로 힘을 보태는 노력과 도와주는 사람이 중요해요.") }); }
		// 용신(도움이 되는 기운): 신강이면 힘을 빼 주는 쪽, 신약이면 받쳐 주는 쪽, 중화면 가장 약한 오행
		int[] ec = Saju.Count(pil); int ys, bad; string how;
		if (grade >= 3) { ys = 1; for (int r = 2; r <= 3; r++) if (gc[r] < gc[ys]) ys = r; bad = 4; how = "넘치는 힘을 쓸 곳(" + GrName[ys] + ")을 늘리는 것이 좋아요."; }
		else if (grade <= 1) { ys = gc[4] <= gc[0] ? 4 : 0; bad = 3; how = "나를 받쳐 주는 곳(" + GrName[ys] + ")을 채우는 것이 좋아요."; }
		else { int mn = 0; for (int i = 1; i < 5; i++) if (ec[i] < ec[mn]) mn = i; ys = ((mn - de) % 5 + 5) % 5; bad = (ys + 2) % 5; how = "가장 약한 기운을 채워 균형을 맞추는 것이 좋아요."; }
		int ye = (de + ys) % 5, be = (de + bad) % 5;
		L.Add(new[] { "도움이 되는 기운 · " + Saju.EN[ye], how + "\n▶ 행운색 " + ELuckColor[ye] + " · 방향 " + EDir2[ye] + " · 음식 " + EFood[ye] + "\n▶ 과해지면 부담이 되는 기운: " + Saju.EN[be] + " (" + ELuckColor[be] + " 계열을 많이 쓰는 날은 쉬어 가세요)\n※ 간단한 억부 기준이라 실제 상담과 다를 수 있어요." });
		// 지지 합충
		string rel = ""; int[] pj = { 0, 1, 3 }; string[] pnm = { "연지", "월지", "시지" }; int db = pil.D % 12;
		for (int k = 0; k < 3; k++) { int b = brs[pj[k]]; if (b < 0) continue; int br = Saju.BRel(db, b); int ri = br == 1 || br == 4 ? 0 : (br == 2 ? 1 : (br == 3 ? 2 : -1)); if (ri >= 0) rel += "● 일지 " + Saju.BK[db] + " ↔ " + pnm[k] + " " + Saju.BK[b] + " (" + (br == 1 ? "육합" : (br == 4 ? "삼합 기운" : (br == 2 ? "충" : "해"))) + "): " + BrRel[k * 3 + ri] + "\n"; }
		L.Add(new[] { "지지의 합과 충", rel.Length > 0 ? rel.TrimEnd('\n') : "일지와 다른 기둥 사이에 큰 합·충이 없어요. 환경 변화가 완만하고, 변화는 스스로 정하는 시점에 오는 편이에요." });
		L.Add(new[] { "#", "MBTI와 사주 연결" });
		if (mbti == "" || mbti.Length != 4) L.Add(new[] { "MBTI를 고르면 더 구체적으로 풀어 드려요", "위쪽 MBTI 칸에서 유형을 고르면 일간의 힘, 십성 구조와 겹쳐서 성향·일하는 방식·관계 습관을 함께 풀어 드려요." });
		else
		{
			bool e = mbti[0] == 'E', n = mbti[1] == 'N', t = mbti[2] == 'T', j = mbti[3] == 'J', strong = grade >= 3; bool cool = de == 3 || de == 4;
			bool inf = gc[4] + gc[1] >= gc[2] + gc[3], act = gc[1] + gc[0] >= gc[3] + gc[4];
			L.Add(new[] { mbti + " × " + Saju.SK[dm] + " 일간 · 에너지와 인식", MbEI[(e ? 0 : 2) + (strong ? 0 : 1)] + "\n" + MbNS[(n ? 0 : 2) + ((n ? inf : !inf) ? 0 : 1)] });
			L.Add(new[] { "판단과 생활 방식", MbTF[(t ? 0 : 2) + ((t ? cool : !cool) ? 0 : 1)] + "\n" + MbJP[(j ? 0 : 2) + ((j ? !act : act) ? 0 : 1)] });
			L.Add(new[] { "한 줄 정리", mbti + "(" + MBT[Array.IndexOf(MB, mbti)] + ")의 " + (e ? "바깥으로 향하는" : "안으로 깊어지는") + " 힘과 " + Saju.EN[me] + " 일간(" + StrName[grade] + ")이 만나 " + ELine[me] + "이 돋보여요. 부족한 부분은 " + Saju.EN[ye] + " 기운(" + ELuckColor[ye] + "·" + EDir2[ye] + ")을 생활에 더해 채워 보세요.", "hi" });
		}
		sxKey = key; sxList = L; return L;
	}
	private float SajuExtraDraw(Graphics g, float y, int me)
	{
		foreach (string[] it in SajuExtra(me)) { if (it[0] == "#") y = SecHead(g, y + 4, it[1], null); else y = Para(g, y, it[0], it[1], it.Length > 2, null); }
		return y;
	}
}
