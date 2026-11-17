using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

// ── 관상 보기 (재미용) ─────────────────────────────────────────────────────────────
// 사진 속 얼굴에서 점 10개(이마 위, 미간, 코 아래, 턱, 양쪽 광대, 양쪽 눈, 입꼬리 양쪽)를 찍으면
// 삼정(상·중·하) 비율, 얼굴형, 눈 사이, 입 크기, 눈꼬리 기울기를 계산해 전통 관상 풀이처럼 설명해요.
internal static class FaceRead
{
	private static float D(PointF a, PointF b) { float x = a.X - b.X, y = a.Y - b.Y; return (float)Math.Sqrt(x * x + y * y); }
	// p: 0 이마(머리선), 1 미간, 2 코 아래, 3 턱, 4 왼쪽 광대, 5 오른쪽 광대, 6 왼눈, 7 오른눈, 8 입꼬리 왼, 9 입꼬리 오
	public static string Analyze(PointF[] p)
	{
		PointF mid = new PointF((p[0].X + p[3].X) / 2f, (p[0].Y + p[3].Y) / 2f);
		float H = D(p[0], p[3]), W = D(p[4], p[5]); if (H < 20f || W < 20f) return "점을 다시 찍어 주세요.";
		float up = D(p[0], p[1]), md = D(p[1], p[2]), lo = D(p[2], p[3]), tot = up + md + lo;
		float uf = up / tot, mf = md / tot, lf = lo / tot, ratio = H / W;
		float eyeGap = D(p[6], p[7]) / W, mouthW = D(p[8], p[9]) / W;
		// 눈 기울기 (두 눈을 잇는 선이 얼굴 축에 얼마나 수직인지) · 입꼬리 기울기
		float tilt = (float)(Math.Atan2(p[7].Y - p[6].Y, p[7].X - p[6].X) * 180 / Math.PI);
		float mtilt = (float)(Math.Atan2(p[9].Y - p[8].Y, p[9].X - p[8].X) * 180 / Math.PI);
		float cheek = D(p[4], p[5]) / Math.Max(1f, D(p[8], p[9]) * 2.6f);
		StringBuilder sb = new StringBuilder();
		sb.Append("◆ 얼굴 길이 대비 비율로 계산했어요 (얼굴 세로 ").Append(Math.Round(H)).Append("px · 가로 ").Append(Math.Round(W)).Append("px)\r\n\r\n");
		sb.Append("① 얼굴형 (타고난 기질)\r\n");
		sb.Append(ratio >= 1.45f ? "갸름하고 긴 얼굴이에요. 생각이 깊고 섬세하며 이상이 높아요. 한 번 정하면 끝까지 가는 끈기가 있고, 지식·기획 쪽에서 빛나요.\r\n" : (ratio >= 1.22f ? "계란형에 가까운 균형 잡힌 얼굴이에요. 사람들과 두루 잘 어울리고 상황 판단이 안정적이에요. 어느 자리에서든 조율을 잘하는 타입이에요.\r\n" : "넓고 둥근 얼굴이에요. 인정이 많고 사람을 모으는 힘이 있어요. 현실 감각이 좋고 먹고사는 일에 복이 따른다고 읽어요.\r\n"));
		sb.Append("\r\n② 삼정 (이마·코·턱 세 부분의 균형: 초년·중년·말년)\r\n");
		sb.Append("상정(이마) ").Append(Math.Round(uf * 100)).Append("% · 중정(미간~코끝) ").Append(Math.Round(mf * 100)).Append("% · 하정(코끝~턱) ").Append(Math.Round(lf * 100)).Append("%\r\n");
		float mx = Math.Max(uf, Math.Max(mf, lf)), mn = Math.Min(uf, Math.Min(mf, lf));
		if (mx - mn < 0.07f) sb.Append("세 부분이 고르게 발달한 균형형이에요. 인생의 큰 기복이 적고 초년·중년·말년이 고르게 안정적이라고 읽어요.\r\n");
		else if (uf == mx) sb.Append("이마(상정)가 넉넉해요. 어릴 때부터 두뇌와 부모 덕이 좋고 학업·시험 운이 잘 따라요. 초년이 든든한 흐름이에요.\r\n");
		else if (mf == mx) sb.Append("중간(중정)이 발달했어요. 30~50대에 자기 힘으로 자리를 잡고 일이 가장 잘 풀리는 중년 활동형이에요. 의지와 추진력이 좋아요.\r\n");
		else sb.Append("턱 쪽(하정)이 든든해요. 말년이 편안하고 재물과 인복이 늦게 꽃피는 대기만성형이에요. 끈기와 버티는 힘이 강해요.\r\n");
		sb.Append("\r\n③ 눈 (마음과 판단)\r\n");
		sb.Append(eyeGap >= 0.40f ? "두 눈 사이가 넓은 편이에요. 마음이 넓고 느긋하며 여러 일을 한꺼번에 보는 시야가 좋아요. 다만 마음먹기까지 시간이 걸릴 수 있어요.\r\n" : (eyeGap >= 0.30f ? "두 눈 사이가 알맞아요. 감정과 이성의 균형이 좋고 상대의 말을 잘 듣는 타입이에요.\r\n" : "두 눈이 가까운 편이에요. 집중력이 뛰어나고 한 가지를 파고드는 힘이 강해요. 가끔 시야를 넓혀 쉬어 가면 좋아요.\r\n"));
		sb.Append(tilt <= -2.5f || tilt >= 2.5f ? "   두 눈의 높이가 조금 달라요. 사람마다 흔한 모습이고, 표정이 살아 있어 인상이 친근하게 남아요.\r\n" : "   두 눈 높이가 가지런해 안정감과 신뢰감을 주는 인상이에요.\r\n");
		sb.Append("\r\n④ 입 (표현과 복)\r\n");
		sb.Append(mouthW >= 0.46f ? "입이 시원하게 큰 편이에요. 말과 표현에 힘이 있고 활달해요. 사람을 이끌고 분위기를 살리는 능력이 좋아요.\r\n" : (mouthW >= 0.34f ? "입 크기가 알맞아요. 말과 행동이 균형 잡혀 신뢰를 얻어요.\r\n" : "입이 작고 야무진 편이에요. 말을 아끼고 신중하며 비밀을 잘 지켜요. 한 번 한 약속은 꼭 지키는 타입이에요.\r\n"));
		sb.Append(mtilt <= -2f ? "   입꼬리가 한쪽으로 올라가 있어요. 낙천적이고 유머가 있어 주변을 밝게 해요.\r\n" : (mtilt >= 2f ? "   입꼬리가 한쪽으로 처져 있어요. 사진 찍는 각도의 영향이 커요. 웃는 표정을 다시 찍어 보면 달라질 수 있어요.\r\n" : "   입꼬리가 수평이라 차분하고 침착한 인상이에요.\r\n"));
		sb.Append("\r\n⑤ 광대와 턱 (의지와 인복)\r\n");
		sb.Append(cheek >= 1.15f ? "광대가 넓은 편이에요. 자존심과 추진력이 강하고 한 번 정하면 밀어붙여요. 리더 자리에서 힘을 발휘해요.\r\n" : "광대가 부드러운 편이에요. 사람들과 부딪히지 않고 둥글게 어울려서 인복이 좋아요.\r\n");
		sb.Append("\r\n◆ 한 줄 요약: ").Append(ratio >= 1.45f ? "섬세하고 생각이 깊으며" : (ratio >= 1.22f ? "균형 잡히고 조율을 잘하며" : "인정 많고 현실 감각이 좋으며")).Append(' ').Append(uf == mx && mx - mn >= 0.07f ? "초년 운이 든든한" : (mf == mx && mx - mn >= 0.07f ? "중년에 크게 피는" : (lf == mx && mx - mn >= 0.07f ? "말년이 편안한" : "운의 흐름이 고른"))).Append(" 인상이에요.\r\n");
		sb.Append("\r\n※ 재미로 보는 풀이예요. 사진 각도·표정·점을 찍은 위치에 따라 달라지고, 사람의 능력이나 운명을 판단하는 근거가 될 수 없어요. 좋은 말만 골라 가볍게 즐겨 주세요.");
		return sb.ToString();
	}
}

internal sealed class FaceForm : Form
{
	private static FaceForm inst;
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new FaceForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }
	private static readonly string[] Hint = { "① 이마 맨 위(머리카락이 시작되는 선, 가운데)를 눌러 주세요", "② 미간(두 눈썹 사이)을 눌러 주세요", "③ 코 아래(콧구멍 바로 밑 가운데)를 눌러 주세요", "④ 턱 맨 아래 가운데를 눌러 주세요", "⑤ 화면 왼쪽 광대(얼굴 가장 넓은 가장자리)를 눌러 주세요", "⑥ 화면 오른쪽 광대를 눌러 주세요", "⑦ 화면 왼쪽 눈 가운데를 눌러 주세요", "⑧ 화면 오른쪽 눈 가운데를 눌러 주세요", "⑨ 화면 왼쪽 입꼬리를 눌러 주세요", "⑩ 화면 오른쪽 입꼬리를 눌러 주세요", "풀이가 나왔어요. [처음부터]로 다른 사진도 볼 수 있어요" };
	private static readonly string[] PN = { "이마", "미간", "코 아래", "턱", "왼 광대", "오른 광대", "왼눈", "오른눈", "입꼬리", "입꼬리" };
	private Bitmap img; private int step; private readonly PointF[] pts = new PointF[10];
	private readonly Panel cv = new FacePanel(); private readonly Label lHint = new Label(); private readonly TextBox tOut = new TextBox(); private readonly Button bOpen = new Button(), bUndo = new Button(), bReset = new Button(), bCopy = new Button();
	private sealed class FacePanel : Panel { public FacePanel() { DoubleBuffered = true; ResizeRedraw = true; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); } }

	public FaceForm()
	{
		float s = Math.Min(1.25f, FUi.Scale(520, 760)); Text = "관상 보기 (재미용)"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(520 * s), (int)(760 * s)); MinimumSize = new Size((int)(440 * s), (int)(600 * s)); Font = new Font("맑은 고딕", 8.5f * s); BackColor = Color.FromArgb(246, 249, 252); AllowDrop = true;
		int m = (int)(10 * s); bOpen.Text = "사진 열기"; bUndo.Text = "한 단계 취소"; bReset.Text = "처음부터"; bCopy.Text = "풀이 복사";
		Button[] bs = { bOpen, bUndo, bReset, bCopy }; int bw = (ClientSize.Width - m * 2 - 3 * (int)(6 * s)) / 4; for (int i = 0; i < 4; i++) { bs[i].SetBounds(m + i * (bw + (int)(6 * s)), m, bw, (int)(28 * s)); bs[i].FlatStyle = FlatStyle.System; Controls.Add(bs[i]); }
		lHint.SetBounds(m, (int)(44 * s), ClientSize.Width - m * 2, (int)(22 * s)); lHint.ForeColor = Color.FromArgb(40, 90, 150); lHint.Font = new Font(Font, FontStyle.Bold); lHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; Controls.Add(lHint);
		cv.SetBounds(m, (int)(70 * s), ClientSize.Width - m * 2, (int)(430 * s)); cv.BackColor = Color.FromArgb(30, 34, 42); cv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; Controls.Add(cv);
		tOut.SetBounds(m, (int)(508 * s), ClientSize.Width - m * 2, ClientSize.Height - (int)(508 * s) - m); tOut.Multiline = true; tOut.ReadOnly = true; tOut.ScrollBars = ScrollBars.Vertical; tOut.BackColor = Color.White; tOut.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; Controls.Add(tOut);
		cv.Paint += OnPaint; cv.MouseDown += OnDown; DragEnter += delegate(object o, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; }; DragDrop += delegate(object o, DragEventArgs e) { try { string[] f = (string[])e.Data.GetData(DataFormats.FileDrop); if (f != null && f.Length > 0) LoadPhoto(f[0]); } catch (Exception ex) { Program.LogErr(ex); } };
		bOpen.Click += delegate { using (OpenFileDialog d = new OpenFileDialog { Filter = "사진|*.jpg;*.jpeg;*.png;*.bmp;*.gif|모든 파일|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) LoadPhoto(d.FileName); };
		bReset.Click += delegate { ResetAll(); }; bUndo.Click += delegate { if (step > 0) { step = Math.Min(step, 10) - 1; tOut.Text = ""; UpdateUi(); } }; bCopy.Click += delegate { try { if (tOut.Text.Length > 0) Clipboard.SetText(tOut.Text); } catch { } };
		FormClosed += delegate { if (img != null) img.Dispose(); }; cv.Resize += delegate { cv.Invalidate(); }; ResetAll();
	}
	public void LoadPhoto(string path)
	{
		try
		{
			using (Bitmap src = new Bitmap(path))
			{
				float k = Math.Min(1f, 1600f / Math.Max(src.Width, src.Height)); Bitmap nb = new Bitmap((int)(src.Width * k), (int)(src.Height * k)); using (Graphics g = Graphics.FromImage(nb)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, nb.Width, nb.Height); }
				if (img != null) img.Dispose(); img = nb;
			}
			ResetAll();
		}
		catch (Exception ex) { Program.LogErr(ex); tOut.Text = "사진을 열 수 없어요. jpg·png 사진을 골라 주세요."; }
	}
	private void ResetAll() { step = 0; tOut.Text = ""; UpdateUi(); }
	private void UpdateUi() { lHint.Text = img == null ? "먼저 [사진 열기]로 정면 얼굴 사진을 열거나 끌어다 놓아 주세요" : Hint[Math.Min(step, 10)]; cv.Invalidate(); }
	private RectangleF ImgRect() { if (img == null) return RectangleF.Empty; float k = Math.Min((float)cv.ClientSize.Width / img.Width, (float)cv.ClientSize.Height / img.Height); float w = img.Width * k, h = img.Height * k; return new RectangleF((cv.ClientSize.Width - w) / 2f, (cv.ClientSize.Height - h) / 2f, w, h); }
	private PointF ToImg(Point p) { RectangleF r = ImgRect(); return new PointF((p.X - r.X) / r.Width * img.Width, (p.Y - r.Y) / r.Height * img.Height); }
	private PointF ToScr(PointF q) { RectangleF r = ImgRect(); return new PointF(r.X + q.X / img.Width * r.Width, r.Y + q.Y / img.Height * r.Height); }
	private void OnDown(object s, MouseEventArgs e)
	{
		if (img == null || e.Button != MouseButtons.Left || step >= 10 || !ImgRect().Contains(e.Location)) return;
		pts[step] = ToImg(e.Location); step++;
		if (step == 10) { try { tOut.Text = FaceRead.Analyze(pts); tOut.SelectionStart = 0; tOut.SelectionLength = 0; } catch (Exception ex) { Program.LogErr(ex); tOut.Text = "풀이 중 문제가 생겼어요. 처음부터 다시 해 주세요."; } }
		UpdateUi();
	}
	private void OnPaint(object s, PaintEventArgs e)
	{
		Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBilinear;
		if (img == null) { using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 200, 215))) g.DrawString("얼굴 사진을 열거나 이곳에 끌어다 놓으세요\n(정면을 보고 찍은 사진이 좋아요)", Font, b, cv.ClientRectangle, sf); return; }
		g.DrawImage(img, ImgRect());
		if (step >= 4) using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), 1.4f) { DashStyle = DashStyle.Dash }) { g.DrawLine(p, ToScr(pts[0]), ToScr(pts[3])); }
		for (int i = 0; i < Math.Min(step, 10); i++) { PointF q = ToScr(pts[i]); using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 210, 60))) g.FillEllipse(b, q.X - 5, q.Y - 5, 10, 10); using (Pen p = new Pen(Color.Black, 1.2f)) g.DrawEllipse(p, q.X - 5, q.Y - 5, 10, 10); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 240, 120))) g.DrawString(PN[i], Font, b, q.X + 8f, q.Y - 8f); }
	}
}
