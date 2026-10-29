using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

// 오락실 반짝이 이펙트 (Kenney Particle Pack · CC0). 작은 알파 스프라이트를 색을 입혀 겹쳐 그려요.
internal sealed class Glints
{
	private static readonly Dictionary<string, Bitmap> spr = new Dictionary<string, Bitmap>();
	private static readonly Dictionary<int, Bitmap> tint = new Dictionary<int, Bitmap>();
	private static readonly ImageAttributes[] AlphaIa = new ImageAttributes[16];
	private class P { public float X, Y, Vx, Vy, Age, Life, Size, Rot, Spin; public string K; public Color C; }
	private readonly List<P> ps = new List<P>(); private static readonly Random R = new Random();
	private static Bitmap Load(string n)
	{
		Bitmap b; if (spr.TryGetValue(n, out b)) return b;
		try { using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("fx." + n + ".png")) { if (st != null) b = new Bitmap(new MemoryStream(ReadAll(st))); } } catch { b = null; }
		spr[n] = b; return b;
	}
	private static byte[] ReadAll(Stream s) { byte[] b = new byte[s.Length]; int o = 0; while (o < b.Length) { int r = s.Read(b, o, b.Length - o); if (r <= 0) break; o += r; } return b; }
	private static Bitmap Tinted(string k, Color c)
	{
		int key = k.GetHashCode() * 31 + (c.ToArgb() & 0xFFFFFF); Bitmap t; if (tint.TryGetValue(key, out t)) return t;
		Bitmap s = Load(k); if (s == null) { tint[key] = null; return null; }
		t = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppPArgb);
		ColorMatrix cm = new ColorMatrix(new float[][] { new float[] { c.R / 255f, 0, 0, 0, 0 }, new float[] { 0, c.G / 255f, 0, 0, 0 }, new float[] { 0, 0, c.B / 255f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0, 0, 0, 0, 1 } });
		using (ImageAttributes ia = new ImageAttributes()) using (Graphics g = Graphics.FromImage(t)) { ia.SetColorMatrix(cm); g.DrawImage(s, new Rectangle(0, 0, s.Width, s.Height), 0, 0, s.Width, s.Height, GraphicsUnit.Pixel, ia); }
		tint[key] = t; return t;
	}
	public void Clear() { ps.Clear(); }
	// 한 곳에서 별/불꽃 알갱이가 사방으로 튀어요
	public void Burst(float x, float y, Color c, int n)
	{
		if (ps.Count > 120) return;
		ps.Add(new P { X = x, Y = y, K = "glow", C = c, Size = 74, Life = 0.35f });
		ps.Add(new P { X = x, Y = y, K = "ring", C = Color.White, Size = 20, Life = 0.4f, Spin = 0 });
		for (int i = 0; i < n; i++)
		{
			double a = R.NextDouble() * 6.283, sp = 70 + R.NextDouble() * 170;
			ps.Add(new P { X = x, Y = y, Vx = (float)(Math.Cos(a) * sp), Vy = (float)(Math.Sin(a) * sp - 50), K = i % 3 == 0 ? "spark" : "star", C = i % 2 == 0 ? c : Color.White, Size = 12 + (float)R.NextDouble() * 16, Life = 0.5f + (float)R.NextDouble() * 0.35f, Rot = (float)(R.NextDouble() * 360), Spin = (float)(R.NextDouble() * 360 - 180) });
		}
	}
	public void Update(float dt)
	{
		for (int i = ps.Count - 1; i >= 0; i--) { P p = ps[i]; p.Age += dt; if (p.Age >= p.Life) { ps.RemoveAt(i); continue; } p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vy += 260f * dt; p.Vx *= (1f - 2f * dt); p.Rot += p.Spin * dt; }
	}
	public void Draw(Graphics g)
	{
		if (ps.Count == 0) return;
		InterpolationMode im = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear;
		float[] m = { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1 };
		foreach (P p in ps)
		{
			float u = p.Age / p.Life; float a = 1f - u; a *= a; Bitmap b = Tinted(p.K, p.C); if (b == null) continue;
			float sz = p.K == "ring" ? p.Size + u * 70f : p.K == "glow" ? p.Size * (0.6f + u * 0.8f) : p.Size * (1f - 0.5f * u);
			GraphicsState st = g.Save(); g.TranslateTransform(p.X, p.Y); if (p.Rot != 0f) g.RotateTransform(p.Rot);
			{ int ai = (int)(Math.Max(0f, Math.Min(1f, a)) * 15f + 0.5f); if (ai > 0) { ImageAttributes ia = AlphaIa[ai]; if (ia == null) { ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = ai / 15f; ia = new ImageAttributes(); ia.SetColorMatrix(cm); AlphaIa[ai] = ia; } g.DrawImage(b, new Rectangle((int)(-sz / 2), (int)(-sz / 2), (int)sz, (int)sz), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia); } }
			g.Restore(st);
		}
		g.InterpolationMode = im;
	}
}
// 큰 그림을 화면에 쓰는 크기로 한 번만 줄여 두고 재사용 (프레임마다 큰 그림을 줄여 그리면 느려요)
internal static class ImgCache
{
	private static readonly Dictionary<System.Drawing.Image, Dictionary<int, Bitmap>> cache = new Dictionary<System.Drawing.Image, Dictionary<int, Bitmap>>(); private static int count;
	public static void Clear() { foreach (Dictionary<int, Bitmap> dd in cache.Values) { foreach (Bitmap x in dd.Values) x.Dispose(); dd.Clear(); } cache.Clear(); count = 0; }
	public static float Scale(Graphics g) { try { float[] e = g.Transform.Elements; return (float)Math.Sqrt(e[0] * e[0] + e[1] * e[1]); } catch { return 1f; } }
	// 화면에 pw×ph 픽셀로 그릴 거면 그 크기에 맞춰 줄인 그림을 돌려줘요 (이미 작으면 원본)
	public static System.Drawing.Image Get(System.Drawing.Image src, float pwF, float phF)
	{
		if (src == null) return null; if (Spr.LogOn) Spr.NoteSz(src, Math.Max(pwF, phF * src.Width / Math.Max(1f, src.Height)));
		int stp = pwF < 96f ? 4 : 8; int pw = ((int)Math.Ceiling(pwF) + stp - 1) / stp * stp, ph = Math.Max(1, (int)Math.Ceiling(phF * pw / Math.Max(1f, pwF)));
		if (pw < 8 || src.Width <= pw * 1.2f) return src;
		Dictionary<int, Bitmap> d; if (!cache.TryGetValue(src, out d)) { d = new Dictionary<int, Bitmap>(); cache[src] = d; }
		Bitmap b; if (d.TryGetValue(pw, out b)) return b;
		if (count > 700) { foreach (Dictionary<int, Bitmap> dd in cache.Values) { foreach (Bitmap x in dd.Values) x.Dispose(); dd.Clear(); } count = 0; d = new Dictionary<int, Bitmap>(); cache[src] = d; }
		int h2 = Math.Max(1, (int)Math.Round((double)src.Height * pw / src.Width));
		b = new Bitmap(pw, h2, PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(src, new Rectangle(0, 0, pw, h2), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel); }
		d[pw] = b; count++; return b;
	}
}
// 스프라이트(그림) 불러오기·그리기: looneybits 야구·농구 팩(CC0)에서 가져온 고해상도 그림
internal static class Spr
{
	private static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
	public static readonly Dictionary<System.Drawing.Image, float> LogSzB = new Dictionary<System.Drawing.Image, float>();
	public static void NoteSz(System.Drawing.Image b, float w) { float o; if (!LogSzB.TryGetValue(b, out o) || w > o) LogSzB[b] = w; }
	public static string[] DumpSizes() { List<string> r = new List<string>(); foreach (KeyValuePair<string, Bitmap> kv in cache) { float o; if (kv.Value != null && LogSzB.TryGetValue(kv.Value, out o)) r.Add(kv.Key + " " + kv.Value.Width + " " + kv.Value.Height + " " + o.ToString("0.0")); } return r.ToArray(); }
	// 메뉴로 돌아가거나 오락실을 닫을 때, 불러 둔 그림을 놓아줘서 메모리를 돌려줘요 (다음 게임을 시작할 때 필요한 것만 다시 불러와요)
	public static void ReleaseAll() { cache.Clear(); ImgCache.Clear(); try { Vfx.ClearTint(); } catch { } GC.Collect(); GC.WaitForPendingFinalizers(); }
	public static readonly HashSet<string> LogReq = new HashSet<string>(); public static bool LogOn;
	// 용량을 줄이려고 작게 저장한 그림은 처음 불러올 때 원래 크기로 되돌려요 (코드가 그림 크기를 기준으로 계산하는 곳이 있어서)
	private static Dictionary<string, int[]> dims;
	private static void OrigDim(string n, ref int w, ref int h)
	{
		if (dims == null)
		{
			dims = new Dictionary<string, int[]>();
			try { using (Stream ds = Assembly.GetExecutingAssembly().GetManifestResourceStream("sprdims.txt")) if (ds != null) using (StreamReader sr = new StreamReader(ds)) { string ln; while ((ln = sr.ReadLine()) != null) { string[] p = ln.Split(' '); int a, b2; if (p.Length == 3 && int.TryParse(p[1], out a) && int.TryParse(p[2], out b2)) dims[p[0]] = new int[] { a, b2 }; } } } catch { }
		}
		int[] d; if (dims.TryGetValue(n, out d)) { w = d[0]; h = d[1]; }
	}
	// 작게 줄여 둔 그림도 "원래 크기" 기준으로 계산하는 곳(동작 그림 배율 등)을 위해 원래 가로·세로를 돌려줘요
	public static float OW(string n, Bitmap b) { OrigEnsure(); int[] d; return dims.TryGetValue(n, out d) ? d[0] : b.Width; }
	public static float OH(string n, Bitmap b) { OrigEnsure(); int[] d; return dims.TryGetValue(n, out d) ? d[1] : b.Height; }
	private static void OrigEnsure() { if (dims == null) { int w = 0, h = 0; OrigDim("", ref w, ref h); } }
	public static Bitmap Get(string n)
	{
		if (LogOn) LogReq.Add(n);
		Bitmap b; if (cache.TryGetValue(n, out b)) return b;
		try
		{
			using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("spr." + n + ".png"))
			{
				if (st != null) { byte[] raw = new byte[st.Length]; int o = 0; while (o < raw.Length) { int r = st.Read(raw, o, raw.Length - o); if (r <= 0) break; o += r; } using (Bitmap src = new Bitmap(new MemoryStream(raw))) { int tw = src.Width, th = src.Height; b = new Bitmap(tw, th, PixelFormat.Format32bppPArgb); using (Graphics g = Graphics.FromImage(b)) { if (tw != src.Width || th != src.Height) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(src, new Rectangle(0, 0, tw, th), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel); } else g.DrawImage(src, 0, 0, src.Width, src.Height); } } }
			}
		}
		catch { b = null; }
		cache[n] = b; return b;
	}
	// 시트에서 오린 동작 그림: 발 아래(bottom) 가운데에 맞춰, 원본 픽셀 × sc 배율로 그려요 (frame마다 크기가 달라도 같은 비율). flip=true면 좌우 반전
	public static bool Frame(Graphics g, string n, float cx, float bottom, float sc, float rot, bool flip)
	{
		Bitmap b = Get(n); if (b == null) return false; float w = OW(n, b) * sc, h = OH(n, b) * sc;
		return Draw(g, n, cx, bottom - h / 2f, flip ? -w : w, h, rot);
	}
	// 가운데(cx,cy)에 w×h 로, rot 도만큼 돌려서 그려요. 없으면 false
	public static bool Draw(Graphics g, string n, float cx, float cy, float w, float h, float rot)
	{
		Bitmap b = Get(n); if (b == null) return false;
		float sc = ImgCache.Scale(g); System.Drawing.Image im = ImgCache.Get(b, Math.Abs(w) * sc, Math.Abs(h) * sc);
		GraphicsState st = g.Save(); g.TranslateTransform(cx, cy); if (rot != 0f) g.RotateTransform(rot);
		g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.CompositingQuality = CompositingQuality.HighSpeed;
		g.DrawImage(im, new RectangleF(-w / 2, -h / 2, w, h), new RectangleF(0, 0, im.Width, im.Height), GraphicsUnit.Pixel); g.Restore(st); return true;
	}
}