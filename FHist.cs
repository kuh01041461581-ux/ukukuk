using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// 운세·타로 기록 보기 + 현재 화면 이미지 저장 (두 창 공통)
internal static class FHist
{
	private static string P { get { return Path.Combine(Program.DataDir, "fortune_hist.txt"); } }
	public static readonly RectangleF ChipA = new RectangleF(232, 37, 44, 17), ChipB = new RectangleF(280, 37, 44, 17);
	public static string Note = "";
	public static int ChipHit(float x, float y) { return ChipA.Contains(x, y) ? 1 : ChipB.Contains(x, y) ? 2 : 0; }
	public static void Add(string kind, string key, string text)
	{
		try
		{
			Directory.CreateDirectory(Program.DataDir); string id = DateTime.Now.ToString("yyyy-MM-dd") + "|" + kind + "|" + key + "|"; List<string> L = new List<string>();
			if (File.Exists(P)) foreach (string l in File.ReadAllLines(P)) if (l.Length > 0 && !l.StartsWith(id)) L.Add(l);
			L.Add(id + (text ?? "").Replace("\r", "").Replace("\n", " / ")); if (L.Count > 300) L.RemoveRange(0, L.Count - 300); File.WriteAllLines(P, L.ToArray());
		}
		catch { }
	}
	private static List<string[]> Load()
	{
		List<string[]> r = new List<string[]>();
		try { if (File.Exists(P)) foreach (string l in File.ReadAllLines(P)) { string[] p = l.Split(new[] { '|' }, 4); if (p.Length == 4) r.Add(p); } } catch { }
		r.Reverse(); return r;
	}
	private static int cnt = -1; private static List<string[]> cache;
	public static void Refresh() { cache = Load(); cnt = cache.Count; }
	public static float MaxScroll(float h) { if (cache == null) Refresh(); return Math.Max(0f, cache.Count * 62f - (h - 80f)); }
	public static void Wheel(ref float scroll, int delta, float h, float sc) { scroll = Math.Max(0f, Math.Min(MaxScroll(h), scroll - delta * 0.5f / sc)); }
	public static void DrawChips(Graphics g, bool hist, Color on, Color off, Color txt, Color txtOn)
	{
		if (Note.Length > 0) { FUi.Text(g, Note, FUi.F(8.5f, 1), on, new RectangleF(ChipA.X, ChipA.Y, ChipB.Right - ChipA.X, ChipA.Height), StringAlignment.Center, StringAlignment.Center, false); return; }
		for (int i = 0; i < 2; i++)
		{
			RectangleF r = i == 0 ? ChipA : ChipB; bool act = hist && i == 0;
			using (GraphicsPath gp = Gfx.Round(r, 8)) using (SolidBrush b = new SolidBrush(act ? on : off)) g.FillPath(b, gp);
			FUi.Text(g, i == 0 ? (hist ? "닫기" : "기록") : "저장", FUi.F(8.5f, 1), act ? txtOn : txt, r, StringAlignment.Center, StringAlignment.Center, false);
		}
	}
	public static void DrawOverlay(Graphics g, float w, float h, float scroll)
	{
		if (cache == null) Refresh();
		using (SolidBrush b = new SolidBrush(Theme.Page)) g.FillRectangle(b, 2, 2, w - 4, h - 4);
		FUi.Text(g, "운세·타로 기록", FUi.F(15f, 1), Theme.Ink, new RectangleF(16, 10, 215, 28), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "최근 순 · 최대 300개까지 저장돼요", FUi.F(8.5f, 0), Theme.Muted, new RectangleF(16, 38, 212, 16), StringAlignment.Near, StringAlignment.Center, false);
		GraphicsState gs = g.Save(); g.SetClip(new RectangleF(8, 64, w - 16, h - 72)); float y = 68f - scroll;
		if (cache.Count == 0) FUi.Text(g, "아직 기록이 없어요. 운세나 타로를 보면 여기에 쌓여요.", FUi.F(10f, 0), Theme.Sub, new RectangleF(20, 90, w - 40, 24), StringAlignment.Center, StringAlignment.Center, false);
		foreach (string[] e in cache)
		{
			if (y + 58f > 64f && y < h)
			{
				RectangleF r = new RectangleF(14, y, w - 28, 56);
				using (GraphicsPath gp = Gfx.Round(r, 10)) { using (SolidBrush b = new SolidBrush(Theme.Raised)) g.FillPath(b, gp); using (Pen p = new Pen(Theme.LineStrong, 1f)) g.DrawPath(p, gp); }
				FUi.Text(g, e[0] + "  ·  " + e[1], FUi.F(9f, 1), e[1] == "타로" ? Theme.AccentBtn : Theme.Ink, new RectangleF(24, y + 4, w - 48, 16), StringAlignment.Near, StringAlignment.Center, false);
				FUi.Text(g, e[3], FUi.F(8.5f, 0), Theme.Sub, new RectangleF(24, y + 21, w - 48, 32), StringAlignment.Near, StringAlignment.Near, true);
			}
			y += 62f;
		}
		g.Restore(gs);
	}
	public static void SavePng(Form f)
	{
		try
		{
			using (SaveFileDialog d = new SaveFileDialog())
			{
				d.Filter = "PNG 그림|*.png"; d.FileName = "마룡이운세_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".png";
				try { d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); } catch { }
				if (d.ShowDialog(f) != DialogResult.OK) return;
				using (Bitmap bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height)); bmp.Save(d.FileName, System.Drawing.Imaging.ImageFormat.Png); }
			}
			Note = "✓ 저장했어요"; Sfx.Play("ok");
		}
		catch { Note = "저장 실패"; }
		Timer t = new Timer(); t.Interval = 2500; t.Tick += delegate { t.Stop(); t.Dispose(); Note = ""; try { f.Invalidate(); } catch { } }; t.Start(); f.Invalidate();
	}
}
