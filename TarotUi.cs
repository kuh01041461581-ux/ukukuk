using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// ── 타로 창 편의: 가장자리를 끌어 창 크기 조절 · 글자 크기 3단계 · 부드러운 스크롤 막대 · 풀이 글은 한 번만 그려 두고 복사(끊김 방지) ──
internal sealed partial class TarotForm
{
	private float textMul = 1f, rsTarget; private bool sbDrag, sbHover, uiHand; private float sbGrab;
	private Bitmap readBmp; private object readKey; private float readSc, readTm; private int readTheme;
	private static readonly float[] TextSteps = { 1f, 1.15f, 1.3f };
	private static string TextPath { get { return Path.Combine(Program.DataDir, "tarot_text.txt"); } }
	private void LoadTextMul() { try { float v; if (File.Exists(TextPath) && float.TryParse(File.ReadAllText(TextPath).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) textMul = Math.Max(1f, Math.Min(1.3f, v)); } catch { } }
	private void SetTextMul(float v)
	{
		textMul = v; entsCache = null; readKey = null; rsTarget = rscroll = Math.Min(rscroll, ReadMax()); Sfx.Play("tick"); Invalidate();
		try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(TextPath, v.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch { }
	}
	// 창 크기가 바뀌면 같은 비율로 키우거나 줄여요 (가운데 정렬)
	private void Relayout()
	{
		if (ClientSize.Width < 50 || ClientSize.Height < 50) return;
		sc = Math.Min(ClientSize.Width / (float)LW, ClientSize.Height / (float)LH); ox = (ClientSize.Width - LW * sc) / 2f; oy = (ClientSize.Height - LH * sc) / 2f;
		try { if (back != null) { if (backSm != null) backSm.Dispose(); backSm = RoundedScaled(back, (int)(FW * sc * 1.2f), (int)(FH * sc * 1.2f)); } } catch { }
		tinyW = -1; readKey = null;
		List<Control> btns = new List<Control>(); foreach (Control c in Controls) if (c is WinBtn) btns.Add(c);
		if (btns.Count == 2) { int top = Dpi.Z(8); btns[1].Location = new Point(ClientSize.Width - btns[1].Width - Dpi.Z(10), top); btns[0].Location = new Point(btns[1].Left - btns[0].Width - Dpi.Z(4), top); }
		Invalidate();
	}
	// 테두리 없는 창이라 가장자리 크기 조절을 직접 알려줘요
	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 0x84 && WindowState == FormWindowState.Normal)
		{
			long lp = m.LParam.ToInt64(); Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF))); int b = Dpi.Z(7);
			bool l = p.X < b, r = p.X >= ClientSize.Width - b, t = p.Y < b, bo = p.Y >= ClientSize.Height - b;
			int hit = t ? (l ? 13 : (r ? 14 : 12)) : (bo ? (l ? 16 : (r ? 17 : 15)) : (l ? 10 : (r ? 11 : 0)));
			if (hit != 0) m.Result = (IntPtr)hit;
		}
	}
	private float ReadTop { get { return SlotsBottom + 38f; } }
	private RectangleF TsBtn(int i) { return new RectangleF(LW - 136f + i * 40f, ReadTop - 30f, 36f, 22f); }
	private RectangleF SbTrack { get { float top = ReadTop; return new RectangleF(LW - 14f, top + 2f, 10f, LH - 8f - top - 2f); } }
	private RectangleF SbThumb
	{
		get { RectangleF tr = SbTrack; float mx = ReadMax(), vis = LH - 6f - ReadTop; if (mx <= 0f) return RectangleF.Empty; float th = Math.Max(34f, tr.Height * vis / (vis + mx)); return new RectangleF(tr.X, tr.Y + (tr.Height - th) * Math.Min(1f, rscroll / mx), tr.Width, th); }
	}
	// 풀이: 글을 한 장의 그림으로 한 번만 그려 두고, 스크롤할 때는 잘라서 보여줘요 (반짝이 효과가 도는 동안에도 끊기지 않게)
	private void DrawRead(Graphics g)
	{
		float top = ReadTop; List<Ent> E = Ents; float total = 4f; foreach (Ent e in E) total += e.H + 6f;
		int th = Theme.Page.ToArgb() ^ Theme.Ink.ToArgb();
		if (readBmp == null || readKey != E || Math.Abs(readSc - sc) > 0.001f || readTm != textMul || readTheme != th)
		{
			if (readBmp != null) readBmp.Dispose(); readBmp = null;
			int bw = Math.Max(2, (int)Math.Ceiling(LW * sc)), bh = Math.Max(2, Math.Min(30000, (int)Math.Ceiling(total * sc)));
			try
			{
				readBmp = new Bitmap(bw, bh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
				using (Graphics rg = Graphics.FromImage(readBmp)) { Gfx.HQ(rg); rg.ScaleTransform(sc, sc); DrawReadRaw(rg, 0f); }
				readKey = E; readSc = sc; readTm = textMul; readTheme = th;
			}
			catch { readBmp = null; }
		}
		GraphicsState gs = g.Save(); g.SetClip(new RectangleF(0, top - 2, LW - 16f, LH - top + 2));
		if (readBmp != null) { InterpolationMode im = g.InterpolationMode; g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(readBmp, new RectangleF(0, top - rscroll, readBmp.Width / sc, readBmp.Height / sc), new RectangleF(0, 0, readBmp.Width, readBmp.Height), GraphicsUnit.Pixel); g.InterpolationMode = im; }
		else { g.TranslateTransform(0, -rscroll); DrawReadRaw(g, top); }
		g.Restore(gs);
		// 부드러운 스크롤 막대 (둥근 알약 · 마우스를 올리거나 끌면 진해져요)
		RectangleF th2 = SbThumb;
		if (!th2.IsEmpty)
		{
			RectangleF tr = SbTrack; float wv = sbHover || sbDrag ? 8f : 5f; RectangleF tk = new RectangleF(tr.Right - wv - 1f, tr.Y, wv, tr.Height), tb = new RectangleF(tk.X, th2.Y, wv, th2.Height);
			using (GraphicsPath gp = Gfx.Round(tk, wv / 2f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, PLine))) g.FillPath(b, gp);
			using (GraphicsPath gp = Gfx.Round(tb, wv / 2f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(sbHover || sbDrag ? 200 : 120, PRed))) g.FillPath(b, gp);
		}
		// 글자 크기
		FUi.Text(g, "글자", FUi.F(8.5f, 1), PMut, new RectangleF(LW - 180f, ReadTop - 30f, 40f, 22f), StringAlignment.Far, StringAlignment.Center, false);
		string[] tl = { "가", "가+", "가++" };
		for (int i = 0; i < 3; i++) { RectangleF r = TsBtn(i); bool on = Math.Abs(TextSteps[i] - textMul) < 0.01f; using (GraphicsPath gp = Gfx.Round(r, 11)) { using (SolidBrush b = new SolidBrush(on ? PRed : PCard)) g.FillPath(b, gp); using (Pen p = new Pen(on ? PRed : PLine, 1f)) g.DrawPath(p, gp); } FUi.Text(g, tl[i], FUi.F(8f + i * 0.8f, 1), on ? Color.White : PSub, r, StringAlignment.Center, StringAlignment.Center, false); }
	}
	// 마우스 처리 (true 면 이 부분에서 처리 끝)
	private bool UiDown(float x, float y)
	{
		if (st != St.Read || zoom >= 0 || spOpen || hist) return false;
		for (int i = 0; i < 3; i++) if (TsBtn(i).Contains(x, y)) { SetTextMul(TextSteps[i]); return true; }
		RectangleF tr = SbTrack; tr.Inflate(6f, 0f);
		if (ReadMax() > 0f && tr.Contains(x, y))
		{
			RectangleF th = SbThumb; sbDrag = true; sbGrab = th.Contains(x, y) ? y - th.Y : th.Height / 2f; ScrollToThumb(y); Wake(); return true;
		}
		return false;
	}
	private void ScrollToThumb(float y) { RectangleF tr = SbTrack, th = SbThumb; float room = Math.Max(1f, tr.Height - th.Height), u = Math.Max(0f, Math.Min(1f, (y - sbGrab - tr.Y) / room)); rsTarget = rscroll = u * ReadMax(); Invalidate(); }
	private bool UiMove(float x, float y)
	{
		if (sbDrag) { ScrollToThumb(y); return true; }
		bool hv = st == St.Read && ReadMax() > 0f && x > LW - 22f && y > ReadTop; if (hv != sbHover) { sbHover = hv; Invalidate(); }
		uiHand = false; if (st == St.Read) { for (int i = 0; i < 3; i++) if (TsBtn(i).Contains(x, y)) uiHand = true; if (hv) uiHand = true; }
		return false;
	}
	private bool UiUp() { if (!sbDrag) return false; sbDrag = false; Invalidate(); return true; }
}
