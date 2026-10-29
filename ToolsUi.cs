using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ‹ 항목 › 선택기
internal class TChooser : Ctl
{
	public string[] Items = new string[0];
	private int idx, hot;
	public event EventHandler IndexChanged;
	public TChooser() { Size = new Size(Dpi.Z(220), Dpi.Z(34)); Cursor = Cursors.Hand; }
	public int Index
	{
		get { return idx; }
		set { if (Items.Length == 0) return; int v = ((value % Items.Length) + Items.Length) % Items.Length; if (v == idx) return; idx = v; Invalidate(); if (IndexChanged != null) IndexChanged(this, EventArgs.Empty); }
	}
	public void SetImmediate(int i) { if (Items.Length > 0) idx = Math.Max(0, Math.Min(Items.Length - 1, i)); Invalidate(); }
	private int Part(Point p) { int b = Dpi.Z(30); return p.X < b ? 1 : (p.X > Width - b ? 2 : 0); }
	protected override void OnMouseMove(MouseEventArgs e) { int h = Part(e.Location); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
	protected override void OnMouseLeave(EventArgs e) { hot = 0; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseClick(MouseEventArgs e) { int h = Part(e.Location); Index = idx + (h == 1 ? -1 : 1); base.OnMouseClick(e); }
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(BackColor);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		Gfx.Fill(g, r, Dpi.Zf(8), Theme.Bg); Gfx.Border(g, r, Dpi.Zf(8), Theme.LineStrong, 1f);
		float b = Dpi.Zf(30);
		if (hot == 1) Gfx.Fill(g, new RectangleF(2, 2, b - 2, Height - 5), Dpi.Zf(6), Theme.Hover);
		if (hot == 2) Gfx.Fill(g, new RectangleF(Width - b, 2, b - 2, Height - 5), Dpi.Zf(6), Theme.Hover);
		Gfx.Text(g, "‹", Theme.F(13f, 1), Theme.Sub, new RectangleF(0, -2, b, Height), StringAlignment.Center, StringAlignment.Center, false);
		Gfx.Text(g, "›", Theme.F(13f, 1), Theme.Sub, new RectangleF(Width - b, -2, b, Height), StringAlignment.Center, StringAlignment.Center, false);
		string txt = Items.Length > 0 ? Items[idx] : "";
		Gfx.Text(g, txt, Theme.F(10f, 1), Theme.Ink, new RectangleF(b, 0, Width - b * 2, Height - 1), StringAlignment.Center, StringAlignment.Center, false);
	}
}

// 공문 처리기한 목록 (D-day 배지 · 완료 · 삭제)
internal class DeadList : Ctl
{
	public event Action<string> Completed, Removed;
	private int rowH, hotRow = -1, hotBtn, off;
	private List<Deadline> items = new List<Deadline>();
	public DeadList() { rowH = Dpi.Z(46); Cursor = Cursors.Default; }
	public void SetItems(List<Deadline> l) { items = l; off = Math.Max(0, Math.Min(off, Math.Max(0, items.Count - Visible))); Invalidate(); }
	private int Visible { get { return Math.Max(1, (Height - (items.Count > (Height / rowH) ? Dpi.Z(20) : 0)) / rowH); } }
	private Rectangle DoneRect(int row) { int w = Dpi.Z(52), h = Dpi.Z(28); return new Rectangle(Width - Dpi.Z(14) - Dpi.Z(52) * 2 - Dpi.Z(6), row * rowH + (rowH - h) / 2, w, h); }
	private Rectangle DelRect(int row) { int w = Dpi.Z(52), h = Dpi.Z(28); return new Rectangle(Width - Dpi.Z(14) - w, row * rowH + (rowH - h) / 2, w, h); }
	protected override void OnMouseMove(MouseEventArgs e)
	{
		int row = e.Y / rowH, btn = 0;
		if (row >= 0 && row < Visible && row + off < items.Count) { if (DoneRect(row).Contains(e.Location)) btn = 1; else if (DelRect(row).Contains(e.Location)) btn = 2; }
		else row = -1;
		if (row != hotRow || btn != hotBtn) { hotRow = row; hotBtn = btn; Cursor = btn > 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
		base.OnMouseMove(e);
	}
	protected override void OnMouseLeave(EventArgs e) { hotRow = -1; hotBtn = 0; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseWheel(MouseEventArgs e) { off = Math.Max(0, Math.Min(Math.Max(0, items.Count - Visible), off - (e.Delta > 0 ? 1 : -1))); Invalidate(); base.OnMouseWheel(e); }
	protected override void OnMouseClick(MouseEventArgs e)
	{
		int row = e.Y / rowH; if (row < 0 || row >= Visible || row + off >= items.Count) return;
		Deadline d = items[row + off];
		if (DoneRect(row).Contains(e.Location) && Completed != null) Completed(d.Id);
		else if (DelRect(row).Contains(e.Location) && Removed != null) Removed(d.Id);
		base.OnMouseClick(e);
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(BackColor);
		if (items.Count == 0)
		{
			Gfx.Text(g, "등록된 기한이 없어요", Theme.F(10.5f, 0), Theme.Muted, new RectangleF(0, 0, Width, Height), StringAlignment.Center, StringAlignment.Center, false);
			return;
		}
		for (int r = 0; r < Visible && r + off < items.Count; r++)
		{
			Deadline d = items[r + off]; int left = d.Left;
			float y = r * rowH;
			if (r > 0) using (Pen p = new Pen(Theme.Line, 1f)) g.DrawLine(p, 0, y, Width, y);
			Color pc = d.Done ? Theme.Muted : (left < 0 ? Theme.Red : (left <= 1 ? Theme.Red : (left <= 3 ? Theme.Amber : Theme.Accent)));
			Color pb = d.Done ? Theme.Hover : Theme.Mix(Theme.Bg, pc, 0.12);
			string tag = d.Done ? "완료" : (left < 0 ? "D+" + (-left) : (left == 0 ? "D-Day" : "D-" + left));
			RectangleF pill = new RectangleF(0, y + (rowH - Dpi.Zf(26)) / 2, Dpi.Zf(58), Dpi.Zf(26));
			Gfx.Fill(g, pill, Dpi.Zf(7), pb);
			Gfx.Text(g, tag, Theme.F(10f, 2), pc, pill, StringAlignment.Center, StringAlignment.Center, false);
			float tx = Dpi.Zf(70), bw = Dpi.Zf(52) * 2 + Dpi.Zf(26);
			Color tc = d.Done ? Theme.Muted : Theme.Ink;
			Gfx.Text(g, d.Title, Theme.F(10.5f, 1), tc, new RectangleF(tx, y + Dpi.Zf(3), Width - tx - bw - Dpi.Zf(10), Dpi.Zf(22)), StringAlignment.Near, StringAlignment.Center, false);
			Gfx.Text(g, d.WhenText, Theme.F(9.5f, 0), Theme.Muted, new RectangleF(tx, y + Dpi.Zf(24), Width - tx - bw, Dpi.Zf(18)), StringAlignment.Near, StringAlignment.Center, false);
			Rectangle a = DoneRect(r), b = DelRect(r);
			bool ha = hotRow == r && hotBtn == 1, hb = hotRow == r && hotBtn == 2;
			RectangleF ra = new RectangleF(a.X + 0.5f, a.Y + 0.5f, a.Width - 1, a.Height - 1), rb = new RectangleF(b.X + 0.5f, b.Y + 0.5f, b.Width - 1, b.Height - 1);
			Gfx.Fill(g, ra, Dpi.Zf(7), ha ? Theme.Hover : Theme.Bg); Gfx.Border(g, ra, Dpi.Zf(7), Theme.LineStrong, 1f);
			Gfx.Fill(g, rb, Dpi.Zf(7), hb ? Theme.DangerSoft : Theme.Bg); Gfx.Border(g, rb, Dpi.Zf(7), hb ? Theme.DangerLine : Theme.LineStrong, 1f);
			Gfx.Text(g, d.Done ? "되살림" : "완료", Theme.F(9.5f, 1), Theme.Ink, ra, StringAlignment.Center, StringAlignment.Center, false);
			Gfx.Text(g, "삭제", Theme.F(9.5f, 1), hb ? Theme.Red : Theme.Sub, rb, StringAlignment.Center, StringAlignment.Center, false);
		}
		if (items.Count > Visible)
			Gfx.Text(g, "마우스 휠로 더 보기 (" + (off + 1) + "~" + Math.Min(items.Count, off + Visible) + " / " + items.Count + ")", Theme.F(9f, 0), Theme.Muted, new RectangleF(0, Height - Dpi.Zf(16), Width, Dpi.Zf(16)), StringAlignment.Center, StringAlignment.Center, false);
	}
}

// 단축키 입력 칸: 눌러서 선택한 뒤 원하는 키 조합을 누르면 기록
internal class TKeyBox : Ctl
{
	public uint Mod = 1; public int Vk = 0x70; private bool cap, hov; private string warn = "";
	public event EventHandler Changed;
	public TKeyBox() { Cursor = Cursors.Hand; TabStop = true; SetStyle(ControlStyles.Selectable, true); Size = new Size(Dpi.Z(170), Dpi.Z(36)); }
	public void Set(uint m, int v) { Mod = m; Vk = v; warn = ""; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
	protected override void OnClick(EventArgs e) { Focus(); cap = true; warn = ""; Invalidate(); base.OnClick(e); }
	protected override void OnLostFocus(EventArgs e) { cap = false; Invalidate(); base.OnLostFocus(e); }
	protected override void OnMouseEnter(EventArgs e) { hov = true; Invalidate(); base.OnMouseEnter(e); }
	protected override void OnMouseLeave(EventArgs e) { hov = false; Invalidate(); base.OnMouseLeave(e); }
	protected override bool IsInputKey(Keys k) { return true; }
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (!cap) return base.ProcessCmdKey(ref msg, keyData);
		Keys key = keyData & Keys.KeyCode;
		if (key == Keys.Menu || key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.LWin || key == Keys.RWin) return true;
		if (key == Keys.Escape && (keyData & Keys.Modifiers) == 0) { cap = false; Invalidate(); return true; }
		uint m = 0;
		if ((keyData & Keys.Alt) != 0) m |= 1; if ((keyData & Keys.Control) != 0) m |= 2; if ((keyData & Keys.Shift) != 0) m |= 4;
		bool fkey = key >= Keys.F1 && key <= Keys.F12;
		if (m == 0 && !fkey) { warn = "Ctrl · Alt 와 함께 눌러 주세요"; Invalidate(); return true; }
		if (m == 1 && key == Keys.F4) { warn = "Alt+F4 는 쓸 수 없어요"; Invalidate(); return true; }
		cap = false; Set(m, (int)key);
		return true;
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(BackColor);
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
		Gfx.Fill(g, r, Dpi.Zf(8), cap ? Theme.AccentSoft : (hov ? Theme.Side : Theme.Bg));
		Gfx.Border(g, r, Dpi.Zf(8), cap ? Theme.Accent : Theme.LineStrong, cap ? 1.6f : 1f);
		string txt = cap ? (warn.Length > 0 ? warn : "키를 누르세요...") : Program.HotkeyName(Mod, Vk);
		Gfx.Text(g, txt, Theme.F(cap ? 9.5f : 10.5f, 1), cap ? (warn.Length > 0 ? Theme.Red : Theme.Accent) : Theme.Ink, new RectangleF(0, 0, Width, Height - 1), StringAlignment.Center, StringAlignment.Center, false);
	}
}

// 특수문자 팔레트 (포커스를 가져가지 않아서, 누르면 쓰던 창에 바로 입력됨) — 스크롤로 많은 글자를 넣을 수 있어요
internal class CharPad : Form
{
	private static readonly string[] Tabs = { "기호", "도형", "부호", "숫자", "알파벳", "로마·그리스", "화살표", "단위·수학", "한글 자모" };
	private static string R(int a, int b) { System.Text.StringBuilder sb = new System.Text.StringBuilder(); for (int c = a; c <= b; c++) sb.Append(char.ConvertFromUtf32(c)).Append(' '); return sb.ToString(); }
	private static string[] Sp(string s) { return s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); }
	private static string[][] sets;
	private static string[][] Sets
	{
		get
		{
			if (sets != null) return sets;
			sets = new string[][] {
				// 기호
				Sp("※ ☆ ★ ☎ ☏ ♀ ♂ ☀ ☁ ☂ ♨ ♩ ♪ ♫ ♬ ♭ ♯ ☞ ☜ ♠ ♤ ♣ ♧ ♥ ♡ ◐ ◑ ☀ ¶ § † ‡ © ® ™ № ℡ ℗ ‰ ¤"),
				// 도형
				Sp("○ ● ◎ ◉ ◌ ◍ ◯ ⊙ □ ■ ▢ ▣ ▤ ▥ ▦ ▧ ▨ ▩ ◇ ◆ ◈ ◊ △ ▲ ▽ ▼ ▷ ▶ ◁ ◀ ▴ ▾ ▸ ◂ ▹ ◃ ◦ • ∙ ▪ ▫ ▒ ░ ▓ █ ▀ ▄ ▌ ▐ ─ │ ┌ ┐ └ ┘ ├ ┤ ┬ ┴ ┼ ═ ║ ╔ ╗ ╚ ╝ ╠ ╣ ╦ ╩ ╬"),
				// 부호
				Sp("ㆍ ‥ … ∼ ～ 「 」 『 』 〔 〕 【 】 《 》 〈 〉 ‘ ’ “ ” ‚ „ ‹ › « » · ‧ ‐ ‑ – — ― ′ ″ ‵ ＼ ／ ｜ ¡ ¿ ※ ‼ 、 。 〃 〓 〒 〕 （ ） ［ ］ ｛ ｝ ＜ ＞"),
				// 숫자
				Sp(R(0x2460,0x2473)+R(0x2474,0x2487)+R(0x2488,0x249B)+R(0x2776,0x277F)+R(0x24EB,0x24F4)+"⓿ "+R(0x278A,0x2793)+R(0x3251,0x325F)+R(0x32B1,0x32BF)+"⑴ ¹ ² ³ ⁴ ⁵ ⁶ ⁷ ⁸ ⁹ ⁰ ₀ ₁ ₂ ₃ ₄ ₅ ₆ ₇ ₈ ₉ ¼ ½ ¾ ⅓ ⅔ ⅛ ⅜ ⅝ ⅞"),
				// 알파벳 (동그라미·괄호)
				Sp(R(0x24B6,0x24CF)+R(0x24D0,0x24E9)+R(0x249C,0x24B5)+R(0x3260,0x326D)+R(0x326E,0x327B)+R(0x3200,0x320D)+R(0x320E,0x321B)+"㈜ ㈔ ㈜ ㉿ ㉾"),
				// 로마자·그리스
				Sp(R(0x2160,0x216B)+R(0x2170,0x217B)+R(0x391,0x3A1)+R(0x3A3,0x3A9)+R(0x3B1,0x3C1)+R(0x3C3,0x3C9)+"Æ æ Ð ð Ø ø Þ þ ß Œ œ Ł ł"),
				// 화살표
				Sp(R(0x2190,0x219B)+R(0x21A9,0x21AA)+R(0x21B0,0x21B5)+R(0x21BA,0x21BB)+R(0x21C4,0x21C5)+R(0x21D0,0x21D5)+R(0x21E6,0x21E9)+"➔ ➜ ☞ ☜ ⇨ ⇦ ⇧ ⇩ ▶ ◀ ▲ ▼"),
				// 단위·수학
				Sp("℃ ℉ ㎡ ㎥ ㎠ ㎢ ㎤ ㎦ ㎜ ㎝ ㎞ ㎖ ㎗ ㎘ ㎎ ㎏ ㎍ ㎛ ㎐ ㎑ ㎒ ㎓ ㎄ ㎅ ㎆ ㎇ ㎈ ㎉ ㏊ ㏈ ㏖ ㏜ ㏅ ㏄ ㏂ ㏁ ㏃ ㏆ ㏇ ℓ Å ‰ ‱ ° ′ ″ ± × ÷ ≒ ≠ ≤ ≥ ≈ ≡ ∴ ∵ ∞ ∑ ∏ ∫ ∬ √ ∂ ∇ ∠ ⊥ ∥ ∟ ∈ ∋ ⊂ ⊃ ⊆ ⊇ ∪ ∩ ∧ ∨ ¬ ⇒ ⇔ ∀ ∃ ∝ ≪ ≫ ∮ ∯ ∼ ≅ ≃ ₩ ¥ € £ ¢ ¤ ㈜ ㈔ №"),
				// 한글 자모
				Sp(R(0x3131,0x3163)+R(0x3165,0x318E)+"ㆍ ᄀ ᄂ ᄃ"),
			};
			return sets;
		}
	}
	private int tab, hovCell = -1, hovTab = -1, hovClose, scroll;
	private readonly int cols = 9, rows = 6, cell, tabH, headH, pad, sbW;
	private Point dragFrom; private bool dragging, sbDrag; private int sbOff;

	public CharPad()
	{
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
		BackColor = Theme.Bg; DoubleBuffered = true;
		cell = Dpi.Z(42); tabH = Dpi.Z(30); headH = Dpi.Z(40); pad = Dpi.Z(12); sbW = Dpi.Z(10);
		ClientSize = new Size(cols * cell + pad * 2 + sbW + Dpi.Z(2), headH + tabH * 2 + rows * cell + pad * 2 + Dpi.Z(24));
		Rectangle ps = Screen.FromPoint(Cursor.Position).WorkingArea; Location = new Point(ps.Right - Width - Dpi.Z(24), ps.Bottom - Height - Dpi.Z(24));
		try { using (GraphicsPath gp = Gfx.Round(new RectangleF(0, 0, Width, Height), Dpi.Zf(14))) Region = new Region(gp); } catch { }
	}
	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; } }   // NOACTIVATE | TOOLWINDOW
	protected override void WndProc(ref Message m) { if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } base.WndProc(ref m); }   // MA_NOACTIVATE

	private int TotalRows { get { return (Sets[tab].Length + cols - 1) / cols; } }
	private int MaxScroll { get { return Math.Max(0, TotalRows - rows); } }
	private int GridTop { get { return headH + tabH * 2 + Dpi.Z(8); } }
	private Rectangle TabRect(int i) { int per = 5; int w = (cols * cell) / per; return new Rectangle(pad + (i % per) * w, headH + (i / per) * tabH, w, tabH); }
	private Rectangle CellRect(int i) { int r = i / cols - scroll; return new Rectangle(pad + (i % cols) * cell, GridTop + r * cell, cell, cell); }
	private bool Vis(int i) { int r = i / cols - scroll; return r >= 0 && r < rows; }
	private Rectangle SbTrack { get { return new Rectangle(pad + cols * cell + Dpi.Z(4), GridTop + 2, sbW, rows * cell - 4); } }
	private Rectangle SbThumb
	{
		get
		{
			Rectangle t = SbTrack; int tot = Math.Max(rows, TotalRows); int h = Math.Max(Dpi.Z(28), t.Height * rows / tot);
			int y = MaxScroll == 0 ? t.Y : t.Y + (t.Height - h) * scroll / MaxScroll; return new Rectangle(t.X, y, t.Width, h);
		}
	}
	private Rectangle CloseRect { get { return new Rectangle(Width - Dpi.Z(40), Dpi.Z(6), Dpi.Z(32), Dpi.Z(28)); } }
	private void SetScroll(int v) { v = Math.Max(0, Math.Min(MaxScroll, v)); if (v != scroll) { scroll = v; hovCell = -1; Invalidate(); } }
	protected override void OnMouseWheel(MouseEventArgs e) { SetScroll(scroll - Math.Sign(e.Delta) * 1); }

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging) { Location = new Point(Cursor.Position.X - dragFrom.X, Cursor.Position.Y - dragFrom.Y); return; }
		if (sbDrag)
		{
			Rectangle t = SbTrack, th = SbThumb; int span = Math.Max(1, t.Height - th.Height);
			SetScroll((int)Math.Round((double)(e.Y - sbOff - t.Y) / span * MaxScroll)); return;
		}
		int hc = -1, ht = -1, cl = CloseRect.Contains(e.Location) ? 1 : 0;
		for (int i = 0; i < Tabs.Length; i++) if (TabRect(i).Contains(e.Location)) ht = i;
		if (e.Y >= GridTop && e.X < pad + cols * cell) for (int i = 0; i < Sets[tab].Length; i++) if (Vis(i) && CellRect(i).Contains(e.Location)) hc = i;
		if (hc != hovCell || ht != hovTab || cl != hovClose) { hovCell = hc; hovTab = ht; hovClose = cl; Invalidate(); }
	}
	protected override void OnMouseLeave(EventArgs e) { hovCell = -1; hovTab = -1; hovClose = 0; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Left) return;
		if (MaxScroll > 0 && new Rectangle(SbTrack.X - Dpi.Z(4), SbTrack.Y, SbTrack.Width + Dpi.Z(8), SbTrack.Height).Contains(e.Location))
		{
			Rectangle th = SbThumb;
			if (th.Contains(e.Location)) sbOff = e.Y - th.Y; else { sbOff = th.Height / 2; SetScroll((int)Math.Round((double)(e.Y - sbOff - SbTrack.Y) / Math.Max(1, SbTrack.Height - th.Height) * MaxScroll)); }
			sbDrag = true; return;
		}
		if (e.Y < headH && !CloseRect.Contains(e.Location)) { dragging = true; dragFrom = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top); }
	}
	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (dragging) { dragging = false; return; }
		if (sbDrag) { sbDrag = false; return; }
		if (CloseRect.Contains(e.Location)) { Close(); return; }
		for (int i = 0; i < Tabs.Length; i++) if (TabRect(i).Contains(e.Location)) { tab = i; scroll = 0; hovCell = -1; Invalidate(); return; }
		if (e.Y >= GridTop && e.X < pad + cols * cell) for (int i = 0; i < Sets[tab].Length; i++) if (Vis(i) && CellRect(i).Contains(e.Location)) { Program.InsertChar(Sets[tab][i]); return; }
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(Theme.Bg);
		Gfx.Text(g, "특수문자", Theme.F(11f, 2), Theme.Ink, new RectangleF(pad + 2, 4, 200, headH - 6), StringAlignment.Near, StringAlignment.Center, false);
		Rectangle cr = CloseRect;
		if (hovClose == 1) Gfx.Fill(g, new RectangleF(cr.X, cr.Y, cr.Width, cr.Height), Dpi.Zf(9), Theme.Hover);
		Gfx.Text(g, "×", Theme.F(14f, 0), Theme.Sub, new RectangleF(cr.X, cr.Y - 2, cr.Width, cr.Height), StringAlignment.Center, StringAlignment.Center, false);
		for (int i = 0; i < Tabs.Length; i++)
		{
			Rectangle t = TabRect(i); bool on = i == tab;
			if (on) Gfx.Fill(g, new RectangleF(t.X + 2, t.Y + 3, t.Width - 4, t.Height - 6), t.Height / 2f - 3, Theme.AccentSoft);
			else if (hovTab == i) Gfx.Fill(g, new RectangleF(t.X + 2, t.Y + 3, t.Width - 4, t.Height - 6), t.Height / 2f - 3, Theme.Hover);
			Gfx.Text(g, Tabs[i], Theme.F(9.5f, on ? 1 : 0), on ? Theme.Accent : Theme.Sub, new RectangleF(t.X, t.Y, t.Width, t.Height - 1), StringAlignment.Center, StringAlignment.Center, false);
		}
		Region oldClip = g.Clip; g.SetClip(new Rectangle(0, GridTop, Width, rows * cell));
		using (Font f = Fonts.Native(14f))
			for (int i = 0; i < Sets[tab].Length; i++)
			{
				if (!Vis(i)) continue;
				Rectangle c = CellRect(i);
				if (hovCell == i) Gfx.Fill(g, new RectangleF(c.X + 2, c.Y + 2, c.Width - 4, c.Height - 4), Dpi.Zf(12), Theme.AccentSoft);
				Gfx.Text(g, Sets[tab][i], f, hovCell == i ? Theme.Accent : Theme.Ink, new RectangleF(c.X, c.Y, c.Width, c.Height), StringAlignment.Center, StringAlignment.Center, false);
			}
		g.Clip = oldClip;
		if (MaxScroll > 0)
		{
			Rectangle t = SbTrack, th = SbThumb;
			Gfx.Fill(g, new RectangleF(t.X, t.Y, t.Width, t.Height), t.Width / 2f, Theme.Hover);
			Gfx.Fill(g, new RectangleF(th.X, th.Y, th.Width, th.Height), th.Width / 2f, sbDrag ? Theme.Accent : Theme.LineStrong);
		}
		Gfx.Text(g, "누르면 쓰던 창에 바로 입력돼요 · 휠로 더 보기 (" + Sets[tab].Length + "개)", Theme.F(9f, 0), Theme.Muted, new RectangleF(0, Height - Dpi.Zf(26), Width, Dpi.Zf(20)), StringAlignment.Center, StringAlignment.Center, false);
	}
}
