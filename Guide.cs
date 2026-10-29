using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// 번호 + 제목 + 설명으로 읽는 사용법 카드 (설정의 '사용법' 화면에서 쓴다)
internal class GuideCard : Card
{
	public string Num = "1", Title = "", Body = "";
	public GuideCard() { }
	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics g = e.Graphics; Gfx.HQ(g);
		float d = Dpi.Zf(34), x = Dpi.Zf(16), y = Dpi.Zf(14);
		using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, x, y, d, d);
		Gfx.Text(g, Num, Theme.F(13f, 2), Color.White, new RectangleF(x, y, d, d), StringAlignment.Center, StringAlignment.Center, false);
		float tx = x + d + Dpi.Zf(14), tw = Width - tx - Dpi.Zf(16);
		Gfx.Text(g, Title, Theme.F(12.5f, 2), Theme.Ink, new RectangleF(tx, Dpi.Zf(10), tw, Dpi.Zf(28)), StringAlignment.Near, StringAlignment.Center, false);
		Gfx.Text(g, Body, Theme.F(11f, 0), Theme.Sub, new RectangleF(tx, Dpi.Zf(40), tw, Height - Dpi.Zf(46)), StringAlignment.Near, StringAlignment.Near, true);
	}
}


// 그림으로 보는 사용 설명서 (앱 안에 들어 있는 화면 설명서를 스크롤해서 봐요)
internal sealed class ManualViewer : Form
{
	private Bitmap bmp; private readonly VScrollBar vs = new VScrollBar();
	private float cur, target; private readonly FrameTimer ft = new FrameTimer();
	private bool syncing;
	public ManualViewer(Icon icon)
	{
		Text = "초과근무 알리미 · 사용 설명서"; BackColor = Color.FromArgb(250, 247, 242); Icon = icon; StartPosition = FormStartPosition.CenterScreen;
		{ Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea; int cw = Math.Min(Dpi.Z(1360), (int)(wa.Width * 0.92)); int chh = cw * 1600 / 2560; if (chh > wa.Height * 0.88) { chh = (int)(wa.Height * 0.88); cw = chh * 2560 / 1600; } ClientSize = new Size(cw, chh); }
		MinimumSize = new Size(Dpi.Z(640), Dpi.Z(420));
		BackColor = Color.FromArgb(250, 247, 242); KeyPreview = true;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
		try
		{
			using (Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("manual.png"))
			using (Image im = Image.FromStream(st)) bmp = new Bitmap(im);
		}
		catch { }
		vs.Visible = false; vs.Dock = DockStyle.Right; vs.Scroll += delegate { if (!syncing) { cur = target = vs.Value; Invalidate(); } };
		Controls.Add(vs);
		ft.Tick += delegate
		{
			float d = target - cur;
			if (Math.Abs(d) < 0.4f) { cur = target; ft.Stop(); } else cur += d * 0.28f;
			Invalidate();
		};
		Resize += delegate { Fit(); };
		Fit();
	}
	private float Scale { get { return bmp == null ? 1f : Math.Min(1.4f, Math.Min((float)ClientSize.Width / bmp.Width, (float)ClientSize.Height / bmp.Height)); } }
	private float Total { get { return bmp == null ? 0 : bmp.Height * Scale; } }
	private void Fit()
	{
		int view = ClientSize.Height; float max = Math.Max(0, Total - view);
		syncing = true;
		vs.Minimum = 0; vs.LargeChange = Math.Max(10, view); vs.SmallChange = Dpi.Z(60);
		vs.Maximum = (int)max + vs.LargeChange - 1; vs.Enabled = max > 0;
		target = Math.Min(target, max); cur = Math.Min(cur, max); vs.Value = (int)Math.Max(0, Math.Min(vs.Maximum - vs.LargeChange + 1, cur));
		syncing = false; Invalidate();
	}
	private void Go(float to)
	{
		float max = Math.Max(0, Total - ClientSize.Height); target = Math.Max(0, Math.Min(max, to));
		syncing = true; try { vs.Value = (int)Math.Max(vs.Minimum, Math.Min(vs.Maximum - vs.LargeChange + 1, target)); } catch { } syncing = false;
		if (Anim.Effects) ft.Start(); else { cur = target; Invalidate(); }
	}
	protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Go(target - Math.Sign(e.Delta) * Dpi.Z(130)); }
	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		int v = ClientSize.Height;
		if (e.KeyCode == Keys.Down) Go(target + Dpi.Z(90)); else if (e.KeyCode == Keys.Up) Go(target - Dpi.Z(90));
		else if (e.KeyCode == Keys.PageDown || e.KeyCode == Keys.Space) Go(target + v * 0.85f); else if (e.KeyCode == Keys.PageUp) Go(target - v * 0.85f);
		else if (e.KeyCode == Keys.Home) Go(0); else if (e.KeyCode == Keys.End) Go(Total); else if (e.KeyCode == Keys.Escape) Close();
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; g.Clear(BackColor);
		if (bmp == null) { TextRenderer.DrawText(g, "설명서를 불러오지 못했어요", Font, ClientRectangle, Color.Gray); return; }
		float s = Scale; int w = (int)(bmp.Width * s), x = (ClientSize.Width - w) / 2, hh = (int)(bmp.Height * s), yo = hh <= ClientSize.Height ? (ClientSize.Height - hh) / 2 : 0;
		float sy = cur / s, sh = ClientSize.Height / s;
		g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
		if (hh <= ClientSize.Height) g.DrawImage(bmp, new RectangleF(x, yo, w, hh), new RectangleF(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
		else g.DrawImage(bmp, new RectangleF(x, 0, w, ClientSize.Height), new RectangleF(0, sy, bmp.Width, Math.Min(sh, bmp.Height - sy)), GraphicsUnit.Pixel);
	}
	protected override void Dispose(bool d) { if (d) { ft.Stop(); if (bmp != null) { bmp.Dispose(); bmp = null; } } base.Dispose(d); }
}
