using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// 화면 캡처 → 플로팅: 단축키(기본 Alt+V)로 화면을 얼려 놓고 영역을 드래그하면, 그 부분이 항상 위에 떠 있는 작은 창이 돼요.
internal sealed class ShotOverlay : Form
{
	private Bitmap shot; private Rectangle vs; private Point a, b; private bool drag;
	public static bool Active; private bool ocr;
	public static void Start() { Start(false); }
	// ocr: 고른 영역의 글자를 바로 읽어서 복사해요 (떠 있는 창 없이)
	public static void Start(bool ocr)
	{
		if (Active) return;
		try { Active = true; ShotOverlay o = new ShotOverlay(); o.ocr = ocr; o.Show(); o.Activate(); }
		catch { Active = false; }
	}
	private ShotOverlay()
	{
		vs = SystemInformation.VirtualScreen;
		shot = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppRgb);
		using (Graphics g = Graphics.FromImage(shot)) g.CopyFromScreen(vs.Location, Point.Empty, vs.Size);
		FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; Bounds = vs; TopMost = true; ShowInTaskbar = false; Cursor = Cursors.Cross; KeyPreview = true;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
	}
	private Rectangle Sel { get { return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)); } }
	protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Right) { Close(); return; } if (e.Button != MouseButtons.Left) return; a = b = e.Location; drag = true; }
	protected override void OnMouseMove(MouseEventArgs e) { b = e.Location; Invalidate(); }
	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (!drag || e.Button != MouseButtons.Left) return; drag = false; b = e.Location; Rectangle r = Sel; r.Intersect(new Rectangle(0, 0, shot.Width, shot.Height));
		if (r.Width < 8 || r.Height < 8) { Invalidate(); return; }
		Bitmap crop = shot.Clone(r, PixelFormat.Format32bppRgb); Point at = new Point(vs.X + r.X, vs.Y + r.Y); Close(); if (ocr) { ShotFloat.ReadText(crop, null); crop.Dispose(); } else ShotFloat.Open(crop, at);
	}
	protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); base.OnKeyDown(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; g.DrawImageUnscaled(shot, 0, 0);
		Rectangle r = Sel; bool has = (drag || r.Width > 0) && r.Width > 0 && r.Height > 0;
		using (Region dim = new Region(ClientRectangle)) using (SolidBrush db = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
		{
			if (has) dim.Exclude(r); g.FillRegion(db, dim);
		}
		if (has)
		{
			g.DrawImage(shot, r, r, GraphicsUnit.Pixel);
			using (Pen p = new Pen(Color.FromArgb(255, 20, 184, 166), 2f)) g.DrawRectangle(p, r);
			string t = r.Width + " × " + r.Height; using (Font f = new Font("맑은 고딕", 9f, FontStyle.Bold)) { SizeF sz = g.MeasureString(t, f); float ty = r.Y - sz.Height - 4 < 0 ? r.Y + 4 : r.Y - sz.Height - 4; using (SolidBrush bb = new SolidBrush(Color.FromArgb(220, 20, 30, 40))) g.FillRectangle(bb, r.X, ty, sz.Width + 8, sz.Height); g.DrawString(t, f, Brushes.White, r.X + 4, ty); }
		}
		else using (Font f = new Font("맑은 고딕", 11f, FontStyle.Bold)) { string t = ocr ? "글자를 읽을 영역을 드래그하세요  (Esc · 오른쪽 클릭: 취소)" : "캡처할 영역을 드래그하세요  (Esc · 오른쪽 클릭: 취소)"; SizeF sz = g.MeasureString(t, f); float x = (ClientSize.Width - sz.Width) / 2f; using (SolidBrush bb = new SolidBrush(Color.FromArgb(200, 20, 30, 40))) g.FillRectangle(bb, x - 12, 24, sz.Width + 24, sz.Height + 12); g.DrawString(t, f, Brushes.White, x, 30); }
	}
	protected override void OnFormClosed(FormClosedEventArgs e) { Active = false; if (shot != null) { shot.Dispose(); shot = null; } base.OnFormClosed(e); }
}

// 떠 있는 캡처 창: 끌어서 이동 · 휠로 크기 · Ctrl+휠로 투명도 · 오른쪽 클릭 메뉴(복사·저장·닫기) · 모서리를 끌어도 크기 조절
internal sealed class ShotFloat : Form
{
	private static readonly List<ShotFloat> all = new List<ShotFloat>();
	private Bitmap img; private float zoom = 1f; private bool dragging, sizing; private Point dsM, dsL; private Size dsS; private bool hover, overX, overT; private string pill; private DateTime pillUntil; private Timer pillTm;
	private const int Grip = 16;
	public static void Open(Bitmap bmp, Point at) { try { ShotFloat f = new ShotFloat(bmp, at); all.Add(f); f.Show(); } catch { bmp.Dispose(); } }
	public static void CloseAll() { foreach (ShotFloat f in new List<ShotFloat>(all)) f.Close(); }
	private ShotFloat(Bitmap bmp, Point at)
	{
		img = bmp; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; ShowInTaskbar = false; TopMost = true; Text = "캡처";
		Size = img.Size; Location = at; MinimumSize = new Size(40, 30); BackColor = Color.Black; Opacity = 1.0;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
		ContextMenuStrip m = new ContextMenuStrip();
		m.Items.Add("클립보드로 복사", null, delegate { try { Clipboard.SetImage(img); } catch { } });
		m.Items.Add("글자 복사 (그림 속 글자 읽기)", null, delegate { ReadText(img, this); });
		m.Items.Add("파일로 저장…", null, delegate { Save(); });
		m.Items.Add("원래 크기로", null, delegate { SetZoom(1f); });
		ToolStripMenuItem op = new ToolStripMenuItem("투명도");
		foreach (int pc in new int[] { 100, 80, 60, 40 }) { int pp = pc; op.DropDownItems.Add(pc + "%", null, delegate { Opacity = pp / 100.0; }); }
		m.Items.Add(op); m.Items.Add("-"); m.Items.Add("닫기", null, delegate { Close(); }); m.Items.Add("모두 닫기", null, delegate { CloseAll(); });
		ContextMenuStrip = m;
	}
	private void Save()
	{
		try { using (SaveFileDialog d = new SaveFileDialog()) { d.Filter = "PNG 그림|*.png"; d.FileName = "캡처_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"; if (d.ShowDialog() == DialogResult.OK) img.Save(d.FileName, ImageFormat.Png); } } catch { }
	}
	private void SetZoom(float z)
	{
		zoom = Math.Max(0.15f, Math.Min(4f, z)); Point c = new Point(Left + Width / 2, Top + Height / 2); Size ns = new Size(Math.Max(40, (int)(img.Width * zoom)), Math.Max(30, (int)(img.Height * zoom))); SetBounds(c.X - ns.Width / 2, c.Y - ns.Height / 2, ns.Width, ns.Height);
	}
	private Rectangle XRect { get { return new Rectangle(Width - 26, 4, 22, 22); } }
	private Rectangle TRect { get { return Width < 90 ? Rectangle.Empty : new Rectangle(Width - 52, 4, 22, 22); } }
	// 그림 속 글자를 읽어 클립보드에 복사 (f 가 있으면 그 창에 결과를 잠깐 띄워요)
	internal static void ReadText(Bitmap b, ShotFloat f)
	{
		if (b == null) return;
		if (Ocr.Busy) { if (f != null) f.Pill("글자를 읽는 중이에요…", 2); return; }
		if (f != null) f.Pill("글자 읽는 중…", 30); else Program.Note("글자 읽는 중…", "잠시만 기다려 주세요", 2000);
		Ocr.Run(b, delegate(string text, string err)
		{
			bool live = f != null && !f.IsDisposed;
			if (err != null) { if (live) f.Pill("글자를 읽지 못했어요", 3); Program.Note("글자 복사", err, 6000); return; }
			if (string.IsNullOrEmpty(text)) { if (live) f.Pill("읽을 글자가 없어요", 3); else Program.Note("글자 복사", "그림에서 글자를 찾지 못했어요", 3000); return; }
			try { Clipboard.SetText(text); } catch { }
			int n = text.Replace("\r", "").Replace("\n", "").Length; string head = text.Length > 60 ? text.Substring(0, 60).Replace("\r\n", " ") + "…" : text.Replace("\r\n", " ");
			if (live) f.Pill("글자 " + n + "자 복사했어요 · Ctrl+V", 3); else Program.Note("글자 " + n + "자를 복사했어요", head + "\n원하는 곳에 Ctrl+V", 4000);
		});
	}
	private void Pill(string s, double sec)
	{
		pill = s; pillUntil = DateTime.Now.AddSeconds(sec); Invalidate();
		if (pillTm == null) { pillTm = new Timer(); pillTm.Interval = 250; pillTm.Tick += delegate { if (pill != null && DateTime.Now > pillUntil) { pill = null; pillTm.Stop(); Invalidate(); } }; }
		pillTm.Start();
	}
	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Left) return;
		if (hover && XRect.Contains(e.Location)) { Close(); return; }
		if (hover && TRect.Contains(e.Location)) { ReadText(img, this); return; }
		dsM = Cursor.Position; dsL = Location; dsS = Size; if (e.X > Width - Grip && e.Y > Height - Grip) sizing = true; else dragging = true; Capture = true;
	}
	protected override void OnMouseMove(MouseEventArgs e)
	{
		bool ox = XRect.Contains(e.Location), ot = TRect.Contains(e.Location); if (ox != overX || ot != overT || !hover) { overX = ox; overT = ot; hover = true; Invalidate(); }
		Cursor = (e.X > Width - Grip && e.Y > Height - Grip) ? Cursors.SizeNWSE : (overX || overT ? Cursors.Hand : Cursors.SizeAll);
		if (!dragging && !sizing) return; Point c = Cursor.Position; int dx = c.X - dsM.X, dy = c.Y - dsM.Y;
		if (dragging) Location = new Point(dsL.X + dx, dsL.Y + dy);
		else { int nw = Math.Max(40, dsS.Width + dx); int nh = Math.Max(30, (int)Math.Round(nw * (double)img.Height / img.Width)); Size = new Size(nw, nh); zoom = (float)nw / img.Width; }
	}
	protected override void OnMouseUp(MouseEventArgs e) { dragging = sizing = false; Capture = false; }
	protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseDoubleClick(MouseEventArgs e) { if (e.Button == MouseButtons.Left && !XRect.Contains(e.Location) && !TRect.Contains(e.Location)) SetZoom(1f); }
	protected override void OnMouseWheel(MouseEventArgs e)
	{
		if ((ModifierKeys & Keys.Control) != 0) Opacity = Math.Max(0.25, Math.Min(1.0, Opacity + Math.Sign(e.Delta) * 0.1));
		else SetZoom(zoom * (e.Delta > 0 ? 1.12f : 1f / 1.12f));
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half;
		g.DrawImage(img, new Rectangle(0, 0, Width, Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
		using (Pen p = new Pen(Color.FromArgb(255, 20, 184, 166), 2f)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
		if (hover)
		{
			Rectangle x = XRect; using (SolidBrush b = new SolidBrush(overX ? Color.FromArgb(230, 220, 60, 60) : Color.FromArgb(170, 20, 30, 40))) g.FillEllipse(b, x);
			using (Pen p = new Pen(Color.White, 2f)) { g.DrawLine(p, x.X + 7, x.Y + 7, x.Right - 7, x.Bottom - 7); g.DrawLine(p, x.Right - 7, x.Y + 7, x.X + 7, x.Bottom - 7); }
			Rectangle tr = TRect;
			if (!tr.IsEmpty)
			{
				using (SolidBrush b = new SolidBrush(overT ? Color.FromArgb(235, 20, 160, 145) : Color.FromArgb(170, 20, 30, 40))) g.FillEllipse(b, tr);
				g.SmoothingMode = SmoothingMode.AntiAlias;
				using (Pen p = new Pen(Color.White, 2f)) { g.DrawLine(p, tr.X + 7, tr.Y + 7, tr.Right - 7, tr.Y + 7); g.DrawLine(p, tr.X + 11, tr.Y + 7, tr.X + 11, tr.Bottom - 6); }
				g.SmoothingMode = SmoothingMode.None;
				if (overT && pill == null) DrawPill(g, "글자 복사");
			}
			using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 2f)) { g.DrawLine(p, Width - 6, Height - 14, Width - 14, Height - 6); g.DrawLine(p, Width - 6, Height - 8, Width - 8, Height - 6); }
		}
		if (pill != null) DrawPill(g, pill);
	}
	private void DrawPill(Graphics g, string s)
	{
		using (Font f = new Font("맑은 고딕", 9f, FontStyle.Bold))
		{
			SizeF sz = g.MeasureString(s, f); float w = Math.Min(Width - 8, sz.Width + 16), h = sz.Height + 6, x = (Width - w) / 2f, y = Height - h - 8; if (y < 30) y = Math.Max(2, Height - h - 2);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath gp = Gfx.Round(new RectangleF(x, y, w, h), h / 2f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(225, 20, 30, 40))) g.FillPath(b, gp);
			g.SmoothingMode = SmoothingMode.None; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
			using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; sf.Trimming = StringTrimming.EllipsisCharacter; sf.FormatFlags = StringFormatFlags.NoWrap; g.DrawString(s, f, Brushes.White, new RectangleF(x, y, w, h), sf); }
		}
	}
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }   // 작업 전환창(Alt+Tab)에는 안 나오게
	protected override void OnFormClosed(FormClosedEventArgs e) { all.Remove(this); if (pillTm != null) { pillTm.Dispose(); pillTm = null; } if (img != null) { img.Dispose(); img = null; } base.OnFormClosed(e); }
}
