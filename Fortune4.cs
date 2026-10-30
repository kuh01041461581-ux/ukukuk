using System;
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
}
