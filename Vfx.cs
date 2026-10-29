using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// 이펙트 도우미: 그림 한 장을 늘이고·돌리고·옅게 하고·색을 바꿔서 여러 모양으로 써요
internal static class Vfx
{
	private static readonly ImageAttributes[] ia = new ImageAttributes[21];
	private static ImageAttributes IA(float a)
	{
		int i = (int)Math.Round(Math.Max(0f, Math.Min(1f, a)) * 20f); ImageAttributes x = ia[i];
		if (x == null) { x = new ImageAttributes(); ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = i / 20f; x.SetColorMatrix(cm); ia[i] = x; }
		return x;
	}
	private static readonly Dictionary<string, Bitmap> tc = new Dictionary<string, Bitmap>();
	public static void ClearTint() { tc.Clear(); }
	// 이름 붙인 색 변형 (곱 r,g,b · 더함 r,g,b)
	private static readonly Dictionary<string, float[]> Tn = new Dictionary<string, float[]> {
		{ "red",    new float[] { 1.15f, 0.55f, 0.5f, 0.12f, 0f, 0f } },
		{ "bomb",   new float[] { 0.55f, 0.5f, 0.7f, 0.1f, 0.02f, 0.04f } },
		{ "steel",  new float[] { 0.7f, 0.85f, 1.15f, 0f, 0.04f, 0.1f } },
		{ "white",  new float[] { 1f, 1f, 1f, 0.55f, 0.55f, 0.55f } },
		{ "ice",    new float[] { 0.6f, 0.8f, 1f, 0.05f, 0.12f, 0.3f } },
		{ "coffee", new float[] { 0.5f, 0.3f, 0.16f, 0.06f, 0.02f, 0f } },
		{ "pink",   new float[] { 1f, 0.45f, 0.7f, 0.1f, 0f, 0.05f } },
		{ "gold",   new float[] { 1f, 0.85f, 0.4f, 0.1f, 0.05f, 0f } },
		{ "enemy",  new float[] { 1f, 0.6f, 0.65f, 0.18f, 0f, 0f } },
		{ "frost",  new float[] { 0.45f, 0.8f, 1.1f, 0f, 0.1f, 0.25f } },
	};
	public static Bitmap Tint(Bitmap src, string name)
	{
		if (src == null || name == null) return src; float[] m; if (!Tn.TryGetValue(name, out m)) return src;
		string key = name + "|" + src.GetHashCode() + "|" + src.Width; Bitmap o; if (tc.TryGetValue(key, out o)) return o;
		o = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
		ColorMatrix cm = new ColorMatrix(new float[][] { new float[] { m[0], 0, 0, 0, 0 }, new float[] { 0, m[1], 0, 0, 0 }, new float[] { 0, 0, m[2], 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { m[3], m[4], m[5], 0, 1 } });
		using (ImageAttributes a = new ImageAttributes()) using (Graphics g = Graphics.FromImage(o)) { a.SetColorMatrix(cm); g.DrawImage(src, new Rectangle(0, 0, o.Width, o.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, a); }
		tc[key] = o; return o;
	}
	// 가운데 (cx,cy) · 크기 w×h · 각도 · 투명도 · 좌우반전
	public static void Draw(Graphics g, Bitmap b, float cx, float cy, float w, float h, float rot, float alpha, bool flip)
	{
		if (b == null || alpha <= 0.03f || w < 1f || h < 1f) return;
		float sc = ImgCache.Scale(g); System.Drawing.Image im = ImgCache.Get(b, w * sc, h * sc);
		float hw = w / 2f, hh = h / 2f, rr = rot * 0.0174533f, cs = (float)Math.Cos(rr), sn = (float)Math.Sin(rr);
		float ax = flip ? hw : -hw, bx = flip ? -hw : hw;   // 왼쪽위 · 오른쪽위 · 왼쪽아래
		PointF[] d = { new PointF(cx + ax * cs + hh * sn, cy + ax * sn - hh * cs), new PointF(cx + bx * cs + hh * sn, cy + bx * sn - hh * cs), new PointF(cx + ax * cs - hh * sn, cy + ax * sn + hh * cs) };
		InterpolationMode im0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear;
		g.DrawImage(im, d, new RectangleF(0, 0, im.Width, im.Height), GraphicsUnit.Pixel, alpha >= 0.97f ? null : IA(alpha));
		g.InterpolationMode = im0;
	}
	private static readonly Dictionary<int, Bitmap> gl = new Dictionary<int, Bitmap>();
	// 부드럽게 퍼지는 빛 (딱딱한 원 대신)
	public static void Glow(Graphics g, float cx, float cy, float rx, float ry, Color c, float alpha)
	{
		int key = c.ToArgb() & 0xFFFFFF; Bitmap b;
		if (!gl.TryGetValue(key, out b))
		{
			b = new Bitmap(64, 64, PixelFormat.Format32bppPArgb);
			using (Graphics gg = Graphics.FromImage(b)) using (GraphicsPath p = new GraphicsPath())
			{
				p.AddEllipse(0, 0, 63, 63);
				using (PathGradientBrush pb = new PathGradientBrush(p)) { pb.CenterColor = Color.FromArgb(255, c); pb.SurroundColors = new Color[] { Color.FromArgb(0, c) }; pb.FocusScales = new PointF(0.2f, 0.2f); gg.FillPath(pb, p); }
			}
			gl[key] = b;
		}
		Draw(g, b, cx, cy, rx * 2f, ry * 2f, 0f, alpha, false);
	}
}
