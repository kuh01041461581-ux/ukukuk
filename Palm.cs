using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

// ── 손금 도우미 (재미용) ─────────────────────────────────────────────────────────────
// 사진 속 손바닥에서 점 3개(손목·중지 뿌리·엄지 쪽)를 찍고, 생명선·감정선·두뇌선(·운명선)을 따라 그리면
// 손바닥 크기에 대한 길이·휘어짐·시작과 끝 위치를 계산해서 전통 손금 풀이에 맞춰 설명해요.
// (사진을 자동으로 분석하는 게 아니라, 내가 따라 그린 선을 해석해요)
internal static class PalmRead
{
	public sealed class Res { public string Text; }
	private static float Dot(PointF a, PointF b) { return a.X * b.X + a.Y * b.Y; }
	private static PointF Sub(PointF a, PointF b) { return new PointF(a.X - b.X, a.Y - b.Y); }
	private static float Len(PointF a) { return (float)Math.Sqrt(a.X * a.X + a.Y * a.Y); }
	private static float PolyLen(List<PointF> p) { float s = 0; for (int i = 1; i < p.Count; i++) s += Len(Sub(p[i], p[i - 1])); return s; }
	// 선의 휘어짐: 시작-끝을 잇는 직선에서 가장 멀리 떨어진 거리 ÷ 직선 길이
	private static float Bulge(List<PointF> p) { PointF a = p[0], b = p[p.Count - 1]; PointF d = Sub(b, a); float L = Len(d); if (L < 1f) return 0f; float mx = 0; foreach (PointF q in p) { float c = Math.Abs(d.X * (q.Y - a.Y) - d.Y * (q.X - a.X)) / L; if (c > mx) mx = c; } return mx / L; }

	// pts: 손목, 중지뿌리, 엄지쪽 점 (픽셀 좌표) · 선: 생명, 감정, 두뇌, 운명(없으면 null)
	public static string Analyze(PointF wrist, PointF mid, PointF thumb, List<PointF> life, List<PointF> heart, List<PointF> brain, List<PointF> fate)
	{
		PointF u = Sub(mid, wrist); float L = Len(u); if (L < 5f) return "점을 다시 찍어 주세요."; u = new PointF(u.X / L, u.Y / L);
		PointF v = new PointF(-u.Y, u.X); PointF ctr = new PointF((wrist.X + mid.X) / 2f, (wrist.Y + mid.Y) / 2f);
		float sg = Dot(Sub(thumb, ctr), v) >= 0 ? 1f : -1f; v = new PointF(v.X * sg, v.Y * sg); float ts = Math.Max(0.15f, Dot(Sub(thumb, ctr), v) / L);
		Func<PointF, PointF> C = delegate(PointF p) { return new PointF(Dot(Sub(p, ctr), v) / L / ts, Dot(Sub(p, wrist), u) / L); };   // X: 손바닥 가운데=0, 엄지쪽 가장자리=+1 / Y: 손목=0, 중지뿌리=1
		StringBuilder sb = new StringBuilder();
		sb.Append("◆ 손바닥 기준 길이 ").Append(Math.Round(L)).Append("px (손목 → 중지 뿌리)를 1로 놓고 계산했어요.\r\n\r\n");
		// 생명선
		List<PointF> lf = Orient(life, C, delegate(PointF a, PointF b) { return a.Y >= b.Y; });   // 시작 = 위쪽(엄지·검지 사이)
		PointF ls = C(lf[0]), le = C(lf[lf.Count - 1]); float lr = PolyLen(life) / L, lb = Bulge(life);
		sb.Append("① 생명선 (체력·활력·삶의 기반)\r\n");
		sb.Append(lr >= 0.95f ? "길고 또렷하게 손목 쪽까지 뻗은 선이에요. 타고난 체력과 회복력이 좋고, 오래 꾸준히 가는 힘이 있어요. " : (lr >= 0.65f ? "보통 길이로 안정적으로 흐르는 선이에요. 체력과 컨디션의 균형이 잘 잡혀 있어요. " : "비교적 짧게 끝나는 선이에요. 손금에서 선의 길이는 수명이 아니라 '에너지를 쓰는 방식'을 말해요. 몰아서 쓰고 푹 쉬는 리듬이 잘 맞아요. "));
		sb.Append(lb >= 0.3f ? "엄지 쪽으로 크게 둥글게 휘어져 에너지가 넘치고 열정적이에요. 사람과 활동을 좋아해요.\r\n" : (lb >= 0.14f ? "적당히 둥글게 휘어져 활동성과 신중함이 균형을 이뤄요.\r\n" : "곧게 뻗은 편이라 에너지를 아껴 쓰는 신중한 타입이에요. 무리하지 않고 꾸준히 가는 힘이 있어요.\r\n"));
		if (le.Y < 0.2f) sb.Append("   끝이 손목 가까이까지 닿아 있어 마무리 힘과 지구력이 좋아요.\r\n"); else if (le.Y > 0.5f) sb.Append("   손바닥 중간에서 끝나요. 큰 일을 한 번에 몰아치기보다 단계로 나눠 가면 좋아요.\r\n");
		sb.Append("\r\n");
		// 감정선
		List<PointF> hf = Orient(heart, C, delegate(PointF a, PointF b) { return a.X <= b.X; });   // 시작 = 새끼손가락 쪽
		PointF hs = C(hf[0]), he = C(hf[hf.Count - 1]); float hr = PolyLen(heart) / L, hb = Bulge(heart);
		sb.Append("② 감정선 (사랑·감정 표현·인간관계)\r\n");
		if (he.X >= 0.6f) sb.Append("검지 아래까지 길게 뻗은 선이에요. 마음이 따뜻하고 헌신적이며 이상이 높아요. 사랑하면 깊이 주는 편이라 기대가 클수록 서운함도 커질 수 있어요.\r\n");
		else if (he.X >= 0.15f) sb.Append("중지 아래쯤에서 끝나는 선이에요. 감정 표현이 솔직하고 자기 감정과 상대를 함께 보는 균형형이에요.\r\n");
		else sb.Append("짧게 끝나는 선이에요. 감정에 휩쓸리지 않고 실리적으로 판단하는 편이에요. 마음은 깊지만 표현이 담백해서 말로 한 번 더 전하면 오해가 줄어요.\r\n");
		sb.Append(hb >= 0.22f ? "   위로 크게 휘어 감정이 풍부하고 표현이 다채로워요.\r\n" : (hb >= 0.1f ? "   완만하게 휘어 감정을 적당히 드러내는 편이에요.\r\n" : "   곧게 뻗어 감정이 안정적이고 변덕이 적어요.\r\n"));
		sb.Append("\r\n");
		// 두뇌선
		List<PointF> bf = Orient(brain, C, delegate(PointF a, PointF b) { return a.X >= b.X; });   // 시작 = 엄지 쪽
		PointF bs = C(bf[0]), be = C(bf[bf.Count - 1]); float br = PolyLen(brain) / L;
		float dxs = Math.Max(0.25f, Math.Abs(bs.X - be.X)); float slope = (bs.Y - be.Y) / dxs;
		sb.Append("③ 두뇌선 (사고방식·판단력·재능)\r\n");
		sb.Append(slope >= 0.55f ? "손목 쪽으로 아래로 내려가는 선이에요. 상상력과 감성이 풍부하고 창의적인 아이디어가 좋아요. 논리와 감정을 함께 쓰는 타입이에요.\r\n" : (slope >= 0.18f ? "완만하게 내려가는 선이에요. 현실 감각과 상상력을 고르게 쓰는 균형형이에요. 판단이 안정적이에요.\r\n" : "거의 수평으로 뻗은 선이에요. 현실적이고 논리적이며 계획을 세워 움직이는 타입이에요. 숫자·규칙·서류에 강해요.\r\n"));
		sb.Append(be.X <= -0.1f ? "   손바닥을 가로질러 길게 뻗어 폭넓게 생각하고 계획이 꼼꼼해요.\r\n" : (be.X <= 0.3f ? "   중간 길이라 생각을 정리해 핵심을 짚는 데 능해요.\r\n" : "   짧은 편이라 빠르게 판단하고 바로 행동하는 직관형이에요.\r\n"));
		float gap = Len(Sub(life[0], brain[0])); { PointF a1 = lf[0], b1 = bf[0]; gap = Len(Sub(a1, b1)) / L; }
		sb.Append(gap < 0.12f ? "   생명선과 두뇌선이 붙어서 시작해 신중하고 안전을 중요하게 생각해요. 새로운 일은 충분히 준비한 뒤 움직여요.\r\n" : (gap < 0.35f ? "   두 선이 적당히 떨어져 시작해 신중함과 도전정신의 균형이 좋아요.\r\n" : "   두 선이 넓게 떨어져 시작해 독립심과 모험심이 강해요. 스스로 정하고 움직이는 걸 좋아해요.\r\n"));
		sb.Append("\r\n");
		// 운명선
		sb.Append("④ 운명선 (일·자리·삶의 방향)\r\n");
		if (fate == null || fate.Count < 4) sb.Append("운명선을 그리지 않았어요. 운명선이 뚜렷하지 않은 사람도 많아요. 정해진 길보다 상황에 맞게 길을 만들어 가는 유연한 타입으로 읽어요.\r\n");
		else
		{
			List<PointF> ff = Orient(fate, C, delegate(PointF a, PointF b) { return a.Y <= b.Y; }); PointF fs = C(ff[0]); float fr = PolyLen(fate) / L;
			sb.Append(fr >= 0.6f ? "손바닥 한가운데를 길게 올라가는 선이에요. 일찍부터 목표가 뚜렷하고 한 분야에서 꾸준히 쌓아 올리는 힘이 있어요. " : "짧은 선이에요. 인생의 큰 방향이 시기마다 바뀌며 여러 경험을 쌓는 타입이에요. ");
			sb.Append(fs.Y <= 0.2f ? "손목 가까이에서 시작해 이른 시기부터 자리를 잡는 흐름이에요.\r\n" : (fs.Y <= 0.45f ? "손바닥 중간에서 시작해 사회생활 이후 본격적으로 방향이 잡혀요.\r\n" : "위쪽에서 시작해 중년 이후 일과 역할이 크게 자리 잡는 흐름이에요.\r\n"));
		}
		sb.Append("\r\n◆ 한 줄 요약: ");
		string a = lr >= 0.65f ? (lb >= 0.3f ? "활력이 넘치고" : "체력이 안정적이고") : "에너지를 몰아 쓰는 리듬으로", b = he.X >= 0.6f ? "마음이 깊고 따뜻하며" : (he.X >= 0.15f ? "감정 표현이 솔직하며" : "감정이 담백하며"), c = slope >= 0.55f ? "상상력으로 길을 여는 사람이에요." : (slope >= 0.18f ? "현실과 감성의 균형으로 판단하는 사람이에요." : "논리와 계획으로 길을 다지는 사람이에요.");
		sb.Append(a).Append(' ').Append(b).Append(' ').Append(c).Append("\r\n\r\n※ 재미로 보는 풀이예요. 사진 속 선을 직접 따라 그린 비율로 계산했고, 사진 각도나 그린 정확도에 따라 달라질 수 있어요. 건강·진로 결정의 근거로 쓰지 마세요.");
		return sb.ToString();
	}
	// 선의 두 끝 중 조건에 맞는 쪽이 시작이 되도록 방향을 맞춰요
	private static List<PointF> Orient(List<PointF> p, Func<PointF, PointF> C, Func<PointF, PointF, bool> startFirst)
	{
		PointF a = C(p[0]), b = C(p[p.Count - 1]); if (startFirst(a, b)) return p; List<PointF> r = new List<PointF>(p); r.Reverse(); return r;
	}
}

internal sealed class PalmForm : Form
{
	private static PalmForm inst;
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new PalmForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }
	private static readonly string[] Hint = {
		"① 손목 아래 가운데를 한 번 눌러 주세요", "② 중지 손가락이 손바닥과 만나는 뿌리(가운데)를 눌러 주세요", "③ 엄지 쪽 손바닥 가장자리(엄지 뿌리 옆)를 눌러 주세요",
		"④ 생명선(엄지 뿌리를 감싸는 선)을 위에서 아래로 따라 그려 주세요", "⑤ 감정선(손가락 아래 가로선)을 새끼손가락 쪽에서 따라 그려 주세요", "⑥ 두뇌선(손바닥 가운데 가로선)을 엄지 쪽에서 따라 그려 주세요",
		"⑦ 운명선(손목에서 중지로 올라가는 세로선)이 있으면 그려 주세요. 없으면 [풀이 보기]를 눌러요", "풀이가 나왔어요. [처음부터]로 다른 손도 볼 수 있어요" };
	private static readonly Color[] LC = { Color.FromArgb(230, 70, 70), Color.FromArgb(240, 100, 170), Color.FromArgb(60, 120, 230), Color.FromArgb(50, 170, 90) };
	private static readonly string[] LN = { "생명선", "감정선", "두뇌선", "운명선" };
	private Bitmap img; private int step; private readonly PointF[] pts = new PointF[3]; private readonly List<PointF>[] lines = { new List<PointF>(), new List<PointF>(), new List<PointF>(), new List<PointF>() };
	private readonly Panel cv = new DoubleBufferedPanel(); private readonly Label lHint = new Label(); private readonly TextBox tOut = new TextBox(); private readonly Button bOpen = new Button(), bUndo = new Button(), bReset = new Button(), bRead = new Button(), bCopy = new Button();
	private bool drawing; private RectangleF ir;
	private sealed class DoubleBufferedPanel : Panel { public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); } }

	public PalmForm()
	{
		float s = Math.Min(1.25f, FUi.Scale(520, 760)); Text = "손금 도우미 (재미용)"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(520 * s), (int)(760 * s)); MinimumSize = new Size((int)(440 * s), (int)(600 * s)); Font = new Font("맑은 고딕", 8.5f * s); BackColor = Color.FromArgb(246, 249, 252); AllowDrop = true;
		int m = (int)(10 * s); bOpen.Text = "사진 열기"; bUndo.Text = "한 단계 취소"; bReset.Text = "처음부터"; bRead.Text = "풀이 보기"; bCopy.Text = "풀이 복사";
		Button[] bs = { bOpen, bUndo, bReset, bRead, bCopy }; int bw = (ClientSize.Width - m * 2 - 4 * (int)(6 * s)) / 5; for (int i = 0; i < 5; i++) { bs[i].SetBounds(m + i * (bw + (int)(6 * s)), m, bw, (int)(28 * s)); bs[i].FlatStyle = FlatStyle.System; bs[i].Anchor = AnchorStyles.Top | AnchorStyles.Left; Controls.Add(bs[i]); }
		lHint.SetBounds(m, (int)(44 * s), ClientSize.Width - m * 2, (int)(22 * s)); lHint.ForeColor = Color.FromArgb(40, 90, 150); lHint.Font = new Font(Font, FontStyle.Bold); lHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; Controls.Add(lHint);
		cv.SetBounds(m, (int)(70 * s), ClientSize.Width - m * 2, (int)(430 * s)); cv.BackColor = Color.FromArgb(30, 34, 42); cv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; Controls.Add(cv);
		tOut.SetBounds(m, (int)(508 * s), ClientSize.Width - m * 2, ClientSize.Height - (int)(508 * s) - m); tOut.Multiline = true; tOut.ReadOnly = true; tOut.ScrollBars = ScrollBars.Vertical; tOut.BackColor = Color.White; tOut.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; Controls.Add(tOut);
		cv.Paint += OnCvPaint; cv.MouseDown += OnCvDown; cv.MouseMove += OnCvMove; cv.MouseUp += OnCvUp; DragEnter += delegate(object o, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; }; DragDrop += delegate(object o, DragEventArgs e) { try { string[] f = (string[])e.Data.GetData(DataFormats.FileDrop); if (f != null && f.Length > 0) Load(f[0]); } catch (Exception ex) { Program.LogErr(ex); } };
		bOpen.Click += delegate { using (OpenFileDialog d = new OpenFileDialog { Filter = "사진|*.jpg;*.jpeg;*.png;*.bmp;*.gif|모든 파일|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) Load(d.FileName); };
		bReset.Click += delegate { ResetAll(); }; bUndo.Click += delegate { Undo(); }; bRead.Click += delegate { DoRead(); }; bCopy.Click += delegate { try { if (tOut.Text.Length > 0) Clipboard.SetText(tOut.Text); } catch { } };
		FormClosed += delegate { if (img != null) img.Dispose(); }; cv.Resize += delegate { cv.Invalidate(); }; ResetAll();
	}
	public void Load(string path)
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
	private void ResetAll() { step = 0; drawing = false; for (int i = 0; i < 4; i++) lines[i].Clear(); tOut.Text = ""; UpdateUi(); }
	private void Undo() { drawing = false; if (step <= 0) return; step = Math.Min(step, 7) - 1; if (step >= 3) lines[step - 3].Clear(); tOut.Text = ""; UpdateUi(); }
	private void UpdateUi() { lHint.Text = img == null ? "먼저 [사진 열기]로 손바닥 사진을 열거나 사진을 끌어다 놓아 주세요" : Hint[Math.Min(step, 7)]; bRead.Enabled = img != null && step >= 6; cv.Invalidate(); }
	private RectangleF ImgRect() { if (img == null) return RectangleF.Empty; float k = Math.Min((float)cv.ClientSize.Width / img.Width, (float)cv.ClientSize.Height / img.Height); float w = img.Width * k, h = img.Height * k; return new RectangleF((cv.ClientSize.Width - w) / 2f, (cv.ClientSize.Height - h) / 2f, w, h); }
	private PointF ToImg(Point p) { RectangleF r = ImgRect(); return new PointF((p.X - r.X) / r.Width * img.Width, (p.Y - r.Y) / r.Height * img.Height); }
	private PointF ToScr(PointF q) { RectangleF r = ImgRect(); return new PointF(r.X + q.X / img.Width * r.Width, r.Y + q.Y / img.Height * r.Height); }
	private bool Inside(Point p) { return ImgRect().Contains(p); }
	private void OnCvDown(object s, MouseEventArgs e)
	{
		if (img == null || e.Button != MouseButtons.Left || !Inside(e.Location) || step >= 7) return;
		if (step < 3) { pts[step] = ToImg(e.Location); step++; UpdateUi(); return; }
		drawing = true; lines[step - 3].Clear(); lines[step - 3].Add(ToImg(e.Location)); cv.Invalidate();
	}
	private void OnCvMove(object s, MouseEventArgs e) { if (!drawing) return; PointF q = ToImg(e.Location); List<PointF> l = lines[step - 3]; PointF last = l[l.Count - 1]; if ((q.X - last.X) * (q.X - last.X) + (q.Y - last.Y) * (q.Y - last.Y) >= 16f) { l.Add(q); cv.Invalidate(); } }
	private void OnCvUp(object s, MouseEventArgs e)
	{
		if (!drawing) return; drawing = false; List<PointF> l = lines[step - 3];
		if (l.Count < 4) { l.Clear(); cv.Invalidate(); return; }
		step++; if (step == 7) { DoRead(); return; } UpdateUi();
	}
	private void DoRead()
	{
		try
		{
			if (img == null || lines[0].Count < 4 || lines[1].Count < 4 || lines[2].Count < 4) { tOut.Text = "생명선·감정선·두뇌선을 모두 그려 주세요."; return; }
			tOut.Text = PalmRead.Analyze(pts[0], pts[1], pts[2], lines[0], lines[1], lines[2], lines[3].Count >= 4 ? lines[3] : null); step = 7; UpdateUi(); tOut.SelectionStart = 0; tOut.SelectionLength = 0;
		}
		catch (Exception ex) { Program.LogErr(ex); tOut.Text = "풀이 중 문제가 생겼어요. 처음부터 다시 해 주세요."; }
	}
	private void OnCvPaint(object s, PaintEventArgs e)
	{
		Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBilinear;
		if (img == null) { using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 200, 215))) g.DrawString("손바닥 사진을 열거나 이곳에 끌어다 놓으세요\n(손가락을 펴고 손바닥이 잘 보이게 찍은 사진)", Font, b, cv.ClientRectangle, sf); return; }
		RectangleF r = ImgRect(); g.DrawImage(img, r);
		for (int i = 0; i < 4; i++)
		{
			List<PointF> l = lines[i]; if (l.Count < 2) continue; PointF[] a = new PointF[l.Count]; for (int k = 0; k < a.Length; k++) a[k] = ToScr(l[k]);
			using (Pen p = new Pen(Color.FromArgb(230, LC[i]), 3.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(p, a);
			using (SolidBrush b = new SolidBrush(LC[i])) using (Font f = new Font(Font, FontStyle.Bold)) g.DrawString(LN[i], f, b, a[a.Length - 1].X + 6f, a[a.Length - 1].Y - 6f);
		}
		string[] pn = { "손목", "중지 뿌리", "엄지 쪽" };
		for (int i = 0; i < Math.Min(step, 3); i++) { PointF q = ToScr(pts[i]); using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 210, 60))) g.FillEllipse(b, q.X - 6, q.Y - 6, 12, 12); using (Pen p = new Pen(Color.Black, 1.4f)) g.DrawEllipse(p, q.X - 6, q.Y - 6, 12, 12); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 230, 100))) g.DrawString(pn[i], Font, b, q.X + 9f, q.Y - 8f); }
		if (step >= 2) { PointF a = ToScr(pts[0]), c = ToScr(pts[1]); using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dash }) g.DrawLine(p, a, c); }
	}
}
