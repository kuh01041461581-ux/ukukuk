using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// ─────────────────────────────────────────────────────────────
//  이스터에그: 마룡이를 2초 안에 5번 누르면 열리는 "마룡이 오락실"
//   별 받기 · 장애물 점프 · 야구공 치기 · 수박 합치기  (기록은 이 컴퓨터에만 저장, 게임마다 TOP 5)
// ─────────────────────────────────────────────────────────────
internal sealed class GameInput { public float Mx, My; public bool MouseMoved, L, R, Hold; public HashSet<Keys> Down; }

internal abstract class MiniGame
{
	public const int W = 400, H = 600;
	public readonly Glints Fx = new Glints(); public float Shake; public int Score; public bool Over; public Image Mascot; protected readonly Random Rnd = new Random();
	public float PlayT;   // 이 판을 시작한 뒤 흐른 시간 (모든 게임은 60초가 한계예요)
	public abstract string Name { get; }
	public abstract string Key { get; }
	public abstract string Desc { get; }
	public abstract string Help { get; }
	public abstract void Reset();
	public abstract void Update(float dt, GameInput inp, double t);
	public abstract void Draw(Graphics g, double t);
	public virtual bool NeedsInput { get { return false; } }
	public virtual bool Crosshair { get { return false; } }
	public string Typed = ""; public bool ClearTyped;
	public virtual float InputY { get { return 516f; } }   // 글자 입력칸(진짜 입력창)이 놓일 세로 위치
	public virtual void Click(float x, float y) { }
	public virtual void Press(Keys k) { }
	public virtual void Swipe(float dx, float dy) { }   // 마우스로 끌었다 놓았을 때 (2048 등)
	public virtual string RightText { get { return ""; } }
	public virtual bool NoLimit { get { return false; } }   // true면 60초 제한 없이 끝날 때까지 (2048·수박처럼 시간이 필요 없는 게임)
	public virtual string Costume { get { return null; } }   // 이 게임에서 입는 마룡이 옷 (outfits 파일 이름)
	public virtual void Icon(Graphics g, float cx, float cy) { }

	// 안내 문구: 어떤 배경 위에서도 읽히는 둥근 반투명 알약 (게임 안 모든 안내를 같은 모양으로)
	public static void Hint(Graphics g, string s, double cyd, float pt, bool gold, float alpha)
	{
		float cy = (float)cyd;
		if (string.IsNullOrEmpty(s) || alpha <= 0.02f) return;
		Font f = Theme.F(pt, 1); float tw = Gfx.MeasureL(s, f) + 4f, pw = Math.Min(W - 20f, tw + 34f), ph = pt * 1.75f + 9f; RectangleF r = new RectangleF((W - pw) / 2f, cy - ph / 2f, pw, ph);
		int A = (int)(255 * Math.Min(1f, alpha));
		using (GraphicsPath sh = Gfx.Round(new RectangleF(r.X, r.Y + 2.5f, r.Width, r.Height), ph / 2f)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(A * 55 / 255, 0, 0, 0))) g.FillPath(sb, sh);
		using (GraphicsPath gp = Gfx.Round(r, ph / 2f))
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(A * 178 / 255, 12, 20, 46))) g.FillPath(b, gp);
			using (Pen p = new Pen(Color.FromArgb(A * 80 / 255, 190, 215, 255), 1.2f)) g.DrawPath(p, gp);
		}
		using (SolidBrush d = new SolidBrush(Color.FromArgb(A, 255, 205, 80))) g.FillEllipse(d, r.X + 11f, cy - 3f, 6f, 6f);
		Txt(g, s, pt, 1, gold ? Color.FromArgb(A, 255, 232, 150) : Color.FromArgb(A, 255, 255, 255), r.X + 22f, r.Y, r.Width - 28f, r.Height, StringAlignment.Center);
	}
	protected static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
	public static void Txt(Graphics g, string s, float pt, int wt, Color c, float x, float y, float w, float h, StringAlignment ha, bool wrap = false)
	{
		if (string.IsNullOrEmpty(s) || c.A < 6) return;
		// 지금 그리는 곳의 변환(배율·이동)을 그대로 따라가요: 카드처럼 옮기고 줄인 상태에서도 글씨가 제자리에 나와요
		float[] el; using (Matrix mx = g.Transform) el = mx.Elements;
		bool simple = Math.Abs(el[1]) < 1e-4f && Math.Abs(el[2]) < 1e-4f && el[0] > 0.05f && Math.Abs(el[0] - el[3]) < 1e-3f;
		if (c.A >= 6 && simple)
		{
			int ca = c.A; c = Color.FromArgb(255, c);   // 투명도가 있는 글씨도 같은 그림을 재사용하고 찍을 때만 투명도를 입혀요
			float S = (float)Math.Round(el[0] * 16f) / 16f; if (S <= 0f) S = el[0];
			int pw = (int)Math.Ceiling(w * S), ph = (int)Math.Ceiling(h * S);
			if (pw > 0 && ph > 0 && pw * ph <= 420000)
			{
				// 같은 글씨는 한 번만 그려 두고 그림처럼 찍어요 (Windows 에서는 글씨 그리기가 프레임마다 하면 꽤 무거워요)
				float fx = x * el[0] + el[4], fy = y * el[3] + el[5]; int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
				string key = s + "\u0001" + pt + "/" + wt + "/" + c.ToArgb() + "/" + pw + "x" + ph + "/" + (int)ha + (wrap ? "w" : "") + "/" + S + "/" + (int)((fx - ix) * 4) + (int)((fy - iy) * 4);
				Bitmap tb; if (!txtCache.TryGetValue(key, out tb))
				{
					if (txtCache.Count > 500) { foreach (Bitmap o in txtCache.Values) o.Dispose(); txtCache.Clear(); }
					tb = new Bitmap(pw + 2, ph + 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); tb.SetResolution(96f * S, 96f * S);
					using (Graphics tg = Graphics.FromImage(tb)) { tg.SmoothingMode = SmoothingMode.AntiAlias; tg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; Gfx.Text(tg, s, Theme.F(pt, wt), Color.FromArgb(254, c), new RectangleF((int)((fx - ix) * 4) / 4f, (int)((fy - iy) * 4) / 4f, pw, ph), ha, StringAlignment.Center, wrap); }
					txtCache[key] = tb;
				}
				GraphicsState st = g.Save(); g.ResetTransform();
				g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
				if (ca >= 254) g.DrawImage(tb, ix, iy, tb.Width, tb.Height); else g.DrawImage(tb, new Rectangle(ix, iy, tb.Width, tb.Height), 0, 0, tb.Width, tb.Height, GraphicsUnit.Pixel, AlphaAttr(ca));
				g.Restore(st); return;
			}
		}
		Gfx.Text(g, s, Theme.F(pt, wt), c, new RectangleF(x, y, w, h), ha, StringAlignment.Center, wrap);
	}
	private static readonly System.Drawing.Imaging.ImageAttributes[] alphaAttrs = new System.Drawing.Imaging.ImageAttributes[17];
	private static System.Drawing.Imaging.ImageAttributes AlphaAttr(int a)
	{
		int q = Math.Max(0, Math.Min(16, (a * 16 + 127) / 255)); System.Drawing.Imaging.ImageAttributes ia = alphaAttrs[q];   // 투명도는 16단계로 나눠서 같은 설정을 재사용해요
		if (ia == null) { ia = new System.Drawing.Imaging.ImageAttributes(); ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] { new float[] { 1, 0, 0, 0, 0 }, new float[] { 0, 1, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 }, new float[] { 0, 0, 0, q / 16f, 0 }, new float[] { 0, 0, 0, 0, 1 } })); alphaAttrs[q] = ia; }
		return ia;
	}
	internal static float PS;   // 지금 그리는 화면 배율 (0이면 시스템 배율)
	private static readonly Dictionary<string, Bitmap> txtCache = new Dictionary<string, Bitmap>();
	public static void ClearTxt() { foreach (Bitmap o in txtCache.Values) o.Dispose(); txtCache.Clear(); }
	// 경기장 조명탑: 격자 기둥 · 램프 패널(전구 격자) · 빛 번짐 · 빛줄기. 탑 그림은 한 번만 그려 두고 재사용해요
	private static readonly Dictionary<string, Bitmap> towerCache = new Dictionary<string, Bitmap>();
	protected static void FloodTower(Graphics g, float cx, float topY, float groundY, int cols, int rows, float beamX, float beamY, double tm)
	{
		int bw = cols * 11 + 10, bh = rows * 10 + 10, ph = (int)Math.Max(10f, groundY - topY - bh), pad = 46; string key = cols + "," + rows + "," + ph;
		Bitmap sp; if (!towerCache.TryGetValue(key, out sp))
		{
			int cw = bw + pad * 2, ch = bh + ph + pad + 6; sp = new Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics q = Graphics.FromImage(sp))
			{
				q.SmoothingMode = SmoothingMode.AntiAlias; float mx = cw / 2f, by0 = pad, bot = by0 + bh + ph;
				// 격자 기둥 (아래로 갈수록 넓어지는 사다리꼴 + X 보강재)
				float tw0 = 5f, tw1 = 9f; using (Pen leg = new Pen(Color.FromArgb(235, 96, 102, 126), 2f)) using (Pen br = new Pen(Color.FromArgb(150, 80, 86, 108), 1f))
				{
					q.DrawLine(leg, mx - tw0, by0 + bh, mx - tw1, bot); q.DrawLine(leg, mx + tw0, by0 + bh, mx + tw1, bot);
					int seg = Math.Max(2, ph / 14); for (int i = 0; i < seg; i++) { float f0 = i / (float)seg, f1 = (i + 1) / (float)seg, y0 = by0 + bh + ph * f0, y1 = by0 + bh + ph * f1, w0 = tw0 + (tw1 - tw0) * f0, w1 = tw0 + (tw1 - tw0) * f1; q.DrawLine(br, mx - w0, y0, mx + w1, y1); q.DrawLine(br, mx + w0, y0, mx - w1, y1); q.DrawLine(br, mx - w1, y1, mx + w1, y1); }
				}
				// 빛 번짐 (겹겹이)
				for (int i = 0; i < 4; i++) { float rr = bw * (0.95f - i * 0.17f) + 14f; using (SolidBrush hb = new SolidBrush(Color.FromArgb(16 + i * 13, 255, 250, 225))) q.FillEllipse(hb, mx - rr, by0 + bh / 2f - rr * 0.8f, rr * 2, rr * 1.6f); }
				// 램프 패널
				using (GraphicsPath fp = Gfx.Round(new RectangleF(mx - bw / 2f, by0, bw, bh), 3)) { using (SolidBrush fb = new SolidBrush(Color.FromArgb(250, 52, 56, 74))) q.FillPath(fb, fp); using (Pen fo = new Pen(Color.FromArgb(220, 130, 138, 164), 1.4f)) q.DrawPath(fo, fp); }
				for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
				{
					float bxx = mx - bw / 2f + 7f + c * 11f, byy = by0 + 7f + r * 10f;
					using (SolidBrush gl = new SolidBrush(Color.FromArgb(70, 255, 246, 205))) q.FillEllipse(gl, bxx - 3f, byy - 3f, 12f, 12f);
					using (SolidBrush bb = new SolidBrush(Color.FromArgb(255, 255, 255, 238))) q.FillEllipse(bb, bxx, byy, 6f, 6f);
					using (SolidBrush hl = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) q.FillEllipse(hl, bxx + 1f, byy + 1f, 2.4f, 2f);
				}
			}
			towerCache[key] = sp;
		}
		// 빛줄기 (탑에서 경기장 쪽으로 퍼져요) — 한 번만 그려 두고 재사용해요
		float y0b = topY + bh / 2f, wb = bw * 0.5f, spread = Math.Max(30f, Math.Abs(beamY - y0b) * 0.34f);
		int bx0 = (int)Math.Floor(Math.Min(cx - wb, beamX - spread)) - 2, bx1 = (int)Math.Ceiling(Math.Max(cx + wb, beamX + spread)) + 2, by0b = (int)Math.Floor(y0b) - 1, by1b = (int)Math.Ceiling(beamY) + 1;
		string bk = "B" + bx0 + "," + bx1 + "," + by0b + "," + by1b + "," + (int)(cx - wb) + "," + (int)(beamX - spread);
		Bitmap bm; if (!towerCache.TryGetValue(bk, out bm))
		{
			int bww = Math.Max(2, bx1 - bx0), bhh = Math.Max(2, by1b - by0b); bm = new Bitmap(bww, bhh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics q = Graphics.FromImage(bm))
			{
				q.SmoothingMode = SmoothingMode.AntiAlias; q.TranslateTransform(-bx0, -by0b);
				PointF[] cone = { new PointF(cx - wb, y0b), new PointF(cx + wb, y0b), new PointF(beamX + spread, beamY), new PointF(beamX - spread, beamY) };
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(bx0, y0b, bww, Math.Max(1f, beamY - y0b)), Color.FromArgb(54, 255, 250, 220), Color.FromArgb(0, 255, 250, 220), 90f)) q.FillPolygon(lb, cone);
			}
			towerCache[bk] = bm;
		}
		g.DrawImage(bm, bx0, by0b, bm.Width, bm.Height);
		g.DrawImage(sp, cx - sp.Width / 2f, topY - pad, sp.Width, sp.Height);
	}
	protected static void BgImg(Graphics g, string pic, Color a, Color b)
	{
		Bitmap p = Spr.Get(pic); if (p == null) { Bg(g, a, b); return; }
		float bsc = ImgCache.Scale(g); System.Drawing.Image bim = ImgCache.Get(p, W * bsc, H * bsc);   // 화면 크기로 한 번만 줄여 두고 재사용
		InterpolationMode m0 = g.InterpolationMode; PixelOffsetMode po0 = g.PixelOffsetMode; CompositingMode cm0 = g.CompositingMode; g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.CompositingMode = CompositingMode.SourceCopy;
		g.DrawImage(bim, new Rectangle(0, 0, W, H), 0, 0, bim.Width, bim.Height, GraphicsUnit.Pixel); g.InterpolationMode = m0; g.PixelOffsetMode = po0; g.CompositingMode = cm0;
	}
	protected static void Bg(Graphics g, Color a, Color b)
	{
		// 배경(그라데이션+가장자리 음영)은 한 번만 만들어 두고 매 프레임 복사만 해요
		float S = PS > 0f ? PS : Dpi.Scale; long key = ((long)a.ToArgb() << 32) ^ (uint)b.ToArgb() ^ ((long)(S * 100) << 8);
		Bitmap bg; if (!bgCache.TryGetValue(key, out bg))
		{
			if (bgCache.Count > 24) { foreach (Bitmap o in bgCache.Values) o.Dispose(); bgCache.Clear(); }
			int pw = (int)Math.Ceiling(W * S), ph = (int)Math.Ceiling(H * S); bg = new Bitmap(pw, ph, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics bgg = Graphics.FromImage(bg))
			{
				bgg.ScaleTransform(pw / (float)W, ph / (float)H);
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, -1, W, H + 2), a, b, 90f)) bgg.FillRectangle(lb, 0, 0, W, H);
				if (vig == null) vig = MakeVig();
				bgg.InterpolationMode = InterpolationMode.Bilinear; bgg.DrawImage(vig, new RectangleF(0, 0, W, H), new RectangleF(0, 0, vig.Width, vig.Height), GraphicsUnit.Pixel);   // 가장자리가 살짝 어두워서 깊이가 생겨요
			}
			bgCache[key] = bg;
		}
		InterpolationMode im0 = g.InterpolationMode; CompositingMode cm0 = g.CompositingMode; PixelOffsetMode po0 = g.PixelOffsetMode;
		g.InterpolationMode = InterpolationMode.NearestNeighbor; g.CompositingMode = CompositingMode.SourceCopy; g.PixelOffsetMode = PixelOffsetMode.Half;
		g.DrawImage(bg, new RectangleF(0, 0, bg.Width / S, bg.Height / S), new RectangleF(0, 0, bg.Width, bg.Height), GraphicsUnit.Pixel);
		g.InterpolationMode = im0; g.CompositingMode = cm0; g.PixelOffsetMode = po0;
	}
	private static readonly Dictionary<long, Bitmap> bgCache = new Dictionary<long, Bitmap>();
	private static Bitmap vig;
	private static Bitmap MakeVig()
	{
		int w = 40, h = 60; Bitmap bm = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f, d = (float)Math.Sqrt(dx * dx * 0.9f + dy * dy * 0.75f); float k = Math.Max(0f, (d - 0.55f) / 0.7f); k = Math.Min(1f, k * k); bm.SetPixel(x, y, Color.FromArgb((int)(52 * k), 10, 14, 30)); }
		return bm;
	}
	// 도톰한 판: 아래 그림자 + 위는 밝고 아래는 어두운 그라데이션 + 윗면 하이라이트
	protected static void Bevel(Graphics g, RectangleF r, float rad, Color c)
	{
		using (GraphicsPath sp = Gfx.Round(new RectangleF(r.X, r.Y + Math.Max(2f, r.Height * 0.045f), r.Width, r.Height), rad)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 20, 14, 8))) g.FillPath(sb, sp);
		using (GraphicsPath gp = Gfx.Round(r, rad)) using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(r.X, r.Y - 0.5f, r.Width, r.Height + 1f), Theme.Mix(c, Color.White, 0.16), Theme.Mix(c, Color.Black, 0.10), 90f)) g.FillPath(lb, gp);
		using (Pen hp = new Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(1.2f, r.Height * 0.03f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(hp, r.X + rad * 0.9f, r.Y + r.Height * 0.07f + 1f, r.Right - rad * 0.9f, r.Y + r.Height * 0.07f + 1f);
	}
	protected static void Twinkle(Graphics g, double t)
	{
		for (int i = 0; i < 26; i++)
		{
			float sx = (i * 97 % 400), sy = (i * 61 % 500); double tw = 0.5 + 0.5 * Math.Sin(t * 2 + i);
			using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(40 + 120 * tw), 255, 255, 255))) g.FillEllipse(b, sx, sy, 2.4f, 2.4f);
		}
	}
	protected static void Snow(Graphics g, double t, int n)
	{
		if (Spr.Get("ms_cu_flake") == null) return;
		for (int k = 0; k < n; k++)
		{
			float sp = 18f + (k * 7) % 14, sz = 9f + (k * 5) % 9; float y = (float)((t * sp + k * 97) % (H + 40)) - 20f, x = (k * 61 + 17) % (W - 20) + 10f + (float)Math.Sin(t * 0.9 + k) * 12f;
			Spr.Draw(g, "ms_cu_flake", x, y, sz, sz * 120f / 104f, (float)(t * 30 + k * 40) % 360f);
		}
	}
		private static readonly System.Drawing.Imaging.ImageAttributes[] fxAttr = new System.Drawing.Imaging.ImageAttributes[21];
	protected static void DrawFx(Graphics g, string name, float cx, float cy, float w, float alpha)
	{
		Bitmap b = Spr.Get("ms_" + name); if (b == null || alpha <= 0.01f) return; float h = w * b.Height / b.Width;
		int ai = (int)Math.Round(Math.Min(1f, alpha) * 20f); System.Drawing.Imaging.ImageAttributes ia = fxAttr[ai];
		if (ia == null) { ia = new System.Drawing.Imaging.ImageAttributes(); System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix(); cm.Matrix33 = ai / 20f; ia.SetColorMatrix(cm); fxAttr[ai] = ia; }
		float sc = ImgCache.Scale(g); System.Drawing.Image im = ImgCache.Get(b, w * sc, h * sc);
		g.DrawImage(im, new Rectangle((int)(cx - w / 2), (int)(cy - h / 2), (int)w, (int)h), 0, 0, im.Width, im.Height, GraphicsUnit.Pixel, ia);
	}
	protected void DrawSpr(Graphics g, string name, float cx, float bottom, float h, float tilt)
	{
		Bitmap b = Spr.Get("ms_" + name); if (b == null) { DrawMascot(g, cx, bottom, h * 0.8f, tilt); return; }
		float k = h / b.Height, w = b.Width * k;
		System.Drawing.Image im = ImgCache.Get(b, w * ImgCache.Scale(g), h * ImgCache.Scale(g));
		GraphicsState st = g.Save(); g.TranslateTransform(cx, bottom - h / 2); g.RotateTransform(tilt); g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(im, -w / 2, -h / 2, w, h); g.Restore(st);
	}
	// 마룡이 동작 그림 (bt_*: 타격, mv_*: 이동, hit/down/faint: 피격). h 는 마룡이 키(px)
	protected void DrawPose(Graphics g, string name, float cx, float bottom, float h, float tilt)
	{
		Bitmap b = Spr.Get("ms_" + name); if (b == null) { DrawMascot(g, cx, bottom, h * 0.6f, tilt); return; }
		float refH = name.StartsWith("bt_") ? 133f : (name.StartsWith("mv_") ? 120f : 90f), k = h / refH; float w = Spr.OW("ms_" + name, b) * k, hh = Spr.OH("ms_" + name, b) * k;
		System.Drawing.Image im = ImgCache.Get(b, w * ImgCache.Scale(g), hh * ImgCache.Scale(g));
		GraphicsState st = g.Save(); g.TranslateTransform(cx, bottom - hh / 2); g.RotateTransform(tilt); g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(im, -w / 2, -hh / 2, w, hh); g.Restore(st);
	}
	protected void DrawMascot(Graphics g, float cx, float bottom, float h, float tilt)
	{
		if (Mascot == null) return;
		h *= 1.2f; float w = h * Mascot.Width / Mascot.Height;
		float sw = Math.Min(w, h) * 0.62f; using (SolidBrush sb = new SolidBrush(Color.FromArgb(46, 0, 0, 0))) { g.FillEllipse(sb, cx - sw / 2f, bottom - sw * 0.1f, sw, sw * 0.24f); g.FillEllipse(sb, cx - sw * 0.34f, bottom - sw * 0.04f, sw * 0.68f, sw * 0.14f); }   // 발밑 그림자
		System.Drawing.Image im = ImgCache.Get(Mascot, w * ImgCache.Scale(g), h * ImgCache.Scale(g));
		GraphicsState st = g.Save(); g.TranslateTransform(cx, bottom - h / 2); g.RotateTransform(tilt);
		g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.CompositingQuality = CompositingQuality.HighSpeed;   // 화면 크기로 한 번만 줄여 둔 그림을 써요
		g.DrawImage(im, -w / 2, -h / 2, w, h); g.Restore(st);
	}
	protected static void StarShape(Graphics g, float cx, float cy, float R, Color c)
	{
		if (c.R >= 250 && c.G >= 180 && c.B <= 160 && Spr.Draw(g, "ms_st1", cx, cy, R * 2.5f, R * 2.5f, 0f)) return;   // 말랑한 별 이미지
		using (GraphicsPath p = Gfx.Star(cx, cy, R, R * 0.48f)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
	}
}

// ── 1. 별 받기 ──────────────────────────────────────────────
internal sealed class CatchGame : MiniGame
{
	private class It { public float X, Y, V, Vy, R, Vr, Ph; public int Kind; }   // V = 최종 낙하 속도(공기저항과 균형), Vy = 지금 속도
	private readonly List<It> items = new List<It>();
	private class Rp { public float X, Y, T, Mx; public Color C; public bool Ring; }
	private readonly List<Rp> rps = new List<Rp>(); private float flash, lightT; private int bestCombo;
	private float px, vx; private bool mouseMode;
	private double left, spawn, shake, popT = -9, now, magnetT, catchT; private int combo; private string pop = ""; private PointF popAt;
	public override string Costume { get { return "우주비행사"; } }
	public override string Name { get { return "별 받기"; } }
	public override string Key { get { return "catch"; } }
	public override string Desc { get { return "떨어지는 별을 받아요"; } }
	public override string Help { get { return "← → 키를 누르고 있거나 마우스로 움직여요\n별은 +1, 금별은 +3, 먹구름은 -3점\n시계는 시간 +3초 · 자석은 5초 동안 별을 끌어당겨요\n별을 놓치면 연속이 반으로 줄어요"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override void Reset() { items.Clear(); rps.Clear(); flash = 0; lightT = 0; Score = 0; combo = 0; left = 30; spawn = 0; px = W / 2f; vx = 0; Over = false; pop = ""; magnetT = 0; catchT = 0; }
	public override void Icon(Graphics g, float cx, float cy) { StarShape(g, cx, cy, 20, Color.FromArgb(255, 214, 80)); }
	public override void Update(float dt, GameInput inp, double t)
	{
		now = t;
		bool key = inp.L || inp.R;
		if (key) { mouseMode = false; float dir = (inp.R ? 1 : 0) - (inp.L ? 1 : 0); vx += dir * 2900f * dt - (dir == 0f ? Math.Sign(vx) * Math.Min(Math.Abs(vx), 1800f * dt) : 0f); if (vx > 560f) vx = 560f; if (vx < -560f) vx = -560f; px += vx * dt; }
		else
		{
			if (inp.MouseMoved) mouseMode = true;
			if (mouseMode) { float ax = 480f * (inp.Mx - px) - 40f * vx; vx += ax * dt; if (vx > 900f) vx = 900f; if (vx < -900f) vx = -900f; px += vx * dt; }   // 마룡이에게 질량이 있어요: 스프링처럼 따라가며 살짝 관성이 남아요
			else { vx -= Math.Sign(vx) * Math.Min(Math.Abs(vx), 1800f * dt); px += vx * dt; }
		}
		if (px < 34f || px > W - 34f) vx *= -0.25f;   // 벽에 부딪히면 살짝 튕겨요
		px = Clamp(px, 34, W - 34);
		left -= dt; if (left > 60 - PlayT) left = 60 - PlayT; double lvl = 1 + (30 - left) / 14.0; spawn -= dt;
		if (spawn <= 0)
		{
			spawn = 0.42 / lvl + Rnd.NextDouble() * 0.25;
			double rr = Rnd.NextDouble(); int k = rr < 0.16 ? 2 : (rr < 0.20 ? 3 : (rr < 0.235 && magnetT <= 0 ? 4 : (Rnd.NextDouble() < 0.14 ? 1 : 0)));
			items.Add(new It { X = 24 + (float)Rnd.NextDouble() * (W - 48), Y = -30, V = (float)((150 + Rnd.NextDouble() * 90) * lvl), Kind = k, Vr = (float)((Rnd.NextDouble() - 0.5) * 240), Ph = (float)(Rnd.NextDouble() * 6.28) }); items[items.Count - 1].Vy = items[items.Count - 1].V * 0.3f;
		}
		float cy = H - 80;
		for (int i = items.Count - 1; i >= 0; i--)
		{
			It it = items[i];
			// 낙하 물리: 중력으로 가속하다가 공기저항과 균형을 이루는 속도(V)에서 멈춰요. 무거운 금별은 더 빨리, 가벼운 별은 천천히 · 바람에 살짝 밀려요
			float mass = it.Kind == 1 ? 1.25f : (it.Kind == 2 ? 1.1f : 0.9f), term = it.V * (it.Kind == 1 ? 1.12f : 1f);
			it.Vy += (620f * mass) * dt * Math.Max(0f, 1f - it.Vy / term); if (it.Vy > term) it.Vy = term;
			it.Y += it.Vy * dt; it.R += it.Vr * dt;
			it.X += (float)Math.Sin(now * 0.9 + it.Ph * 0.3) * 16f / mass * dt;
			if (lvl > 2.0 && it.Kind != 2) it.X = Clamp(it.X + (float)Math.Sin(now * 3.2 + it.Ph) * (float)Math.Min(70.0, (lvl - 2.0) * 60.0) * dt, 20, W - 20);   // 후반엔 별이 좌우로 살랑살랑 흔들리며 떨어져요
			if (magnetT > 0 && it.Kind != 2 && it.Y > cy - 300 && it.Y < cy + 20) it.X += (px - it.X) * Math.Min(1f, dt * 4f);   // 자석: 별이 마룡이 쪽으로 끌려와요
			if (it.Y > cy - 34 && it.Y < cy + 28 && Math.Abs(it.X - px) < 40)
			{
				if (it.Kind == 3) { rps.Add(new Rp { X = it.X, Y = it.Y, Mx = 0.5f, C = Color.FromArgb(140, 230, 255), Ring = true }); left += 3; Pop("휴게시간 +3초", it.X, it.Y); Sfx.Play("ok2"); Fx.Burst(it.X, it.Y, Color.FromArgb(140, 230, 255), 9); catchT = 0.18; }
				else if (it.Kind == 4) { rps.Add(new Rp { X = it.X, Y = it.Y, Mx = 0.5f, C = Color.FromArgb(255, 120, 130), Ring = true }); magnetT = 5; Pop("칼퇴 자석 5초", it.X, it.Y); Sfx.Play("ok2"); Fx.Burst(it.X, it.Y, Color.FromArgb(255, 120, 130), 9); catchT = 0.18; }
				else if (it.Kind == 2) { flash = 0.22f; lightT = 0.3f; rps.Add(new Rp { X = it.X, Y = it.Y, Mx = 0.4f, C = Color.FromArgb(160, 170, 210), Ring = true }); Score = Math.Max(0, Score - 3); combo = 0; shake = 0.25; Shake = 0.35f; Pop("야근 통보 -3", it.X, it.Y); Sfx.Play("bad2"); Fx.Burst(it.X, it.Y, Color.FromArgb(150, 160, 200), 5); }
				else { rps.Add(new Rp { X = it.X, Y = it.Y, Mx = it.Kind == 1 ? 0.55f : 0.4f, C = it.Kind == 1 ? Color.FromArgb(255, 200, 60) : Color.FromArgb(255, 238, 150), Ring = true }); int g2 = it.Kind == 1 ? 3 : 1; combo++; if (combo > bestCombo) bestCombo = combo; if (combo % 5 == 0) { flash = 0.18f; rps.Add(new Rp { X = px, Y = H - 70, Mx = 0.8f, C = Color.FromArgb(255, 214, 90), Ring = true }); } if (combo % 5 == 0) g2 += 2; Score += g2; Pop("+" + g2, it.X, it.Y); Sfx.Play(it.Kind == 1 ? "ok" : "tick"); Fx.Burst(it.X, it.Y, it.Kind == 1 ? Color.FromArgb(255, 200, 60) : Color.FromArgb(255, 238, 150), it.Kind == 1 || combo % 5 == 0 ? 9 : 4); if (combo % 5 == 0) Shake = 0.2f; catchT = 0.18; }
				items.RemoveAt(i);
			}
			else if (it.Y > H - 38 && it.Y < H - 20 && !(it.Kind == 2)) { rps.Add(new Rp { X = it.X, Y = H - 30, Mx = 0.45f, C = Color.FromArgb(120, 255, 238, 170), Ring = false }); if (it.Kind == 0 || it.Kind == 1) { if (combo >= 5) Pop("반려! 연속 끊김", it.X, it.Y - 10); combo /= 2; } items.RemoveAt(i); }
			else if (it.Y > H + 40) { items.RemoveAt(i); }
		}
		if (flash > 0) flash -= dt; if (lightT > 0) lightT -= dt; for (int ri = rps.Count - 1; ri >= 0; ri--) { rps[ri].T += dt; if (rps[ri].T > rps[ri].Mx) rps.RemoveAt(ri); }
		if (shake > 0) shake -= dt; if (magnetT > 0) magnetT -= dt; if (catchT > 0) catchT -= dt;
		if (left <= 0) { left = 0; Over = true; }
	}
	private void Pop(string s, float x, float y) { pop = s; popT = now; popAt = new PointF(x, y); }
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(14, 18, 54), Color.FromArgb(40, 52, 120)); Twinkle(g, t);
		// 오로라 띠와 달빛 번짐 · 가끔 지나가는 별똥별
		{ float sh0 = (float)Math.Sin(t * 0.4) * 12f; using (LinearGradientBrush ab = new LinearGradientBrush(new RectangleF(0, 100 + sh0, W, 130), Color.Transparent, Color.Transparent, 90f)) { ColorBlend cb = new ColorBlend(3); cb.Colors = new Color[] { Color.FromArgb(0, 120, 255, 200), Color.FromArgb(30, 130, 210, 255), Color.FromArgb(0, 120, 255, 200) }; cb.Positions = new float[] { 0f, 0.5f, 1f }; ab.InterpolationColors = cb; g.FillRectangle(ab, 0, 100 + sh0, W, 130); } }
		{ double mp0 = (t * 0.23) % 1.0; if (mp0 < 0.12) { float u = (float)(mp0 / 0.12); float mx0 = 60 + 220 * (float)((int)(t * 0.23) * 0.37 % 1.0), my0 = 30 + u * 90; using (Pen mpn = new Pen(Color.FromArgb((int)(200 * (1 - u)), 255, 255, 240), 2f) { StartCap = LineCap.Round }) g.DrawLine(mpn, mx0 + u * 120, my0, mx0 + u * 120 - 36, my0 - 14); } }
		using (SolidBrush mb2 = new SolidBrush(Color.FromArgb(14 + (int)(8 * Math.Sin(t * 1.3)), 255, 240, 200))) g.FillEllipse(mb2, 270, 34, 122, 122);
		using (SolidBrush mb = new SolidBrush(Color.FromArgb(26, 255, 240, 200))) g.FillEllipse(mb, 292, 56, 78, 78);
		using (SolidBrush mb = new SolidBrush(Color.FromArgb(225, 255, 246, 220))) g.FillEllipse(mb, 306, 70, 50, 50);
		using (SolidBrush hb0 = new SolidBrush(Color.FromArgb(26, 34, 84))) { List<PointF> hp0 = new List<PointF> { new PointF(0, H) }; for (float hx = 0; hx <= W; hx += 10) hp0.Add(new PointF(hx, H - 78 - 16 * (float)Math.Sin(hx * 0.013 + 3) - 8 * (float)Math.Sin(hx * 0.04))); hp0.Add(new PointF(W, H)); g.FillPolygon(hb0, hp0.ToArray()); }
		using (SolidBrush hb = new SolidBrush(Color.FromArgb(34, 40, 96))) { List<PointF> hp = new List<PointF> { new PointF(0, H) }; for (float hx = 0; hx <= W; hx += 10) hp.Add(new PointF(hx, H - 46 - 20 * (float)Math.Sin(hx * 0.021 + 1) - 10 * (float)Math.Sin(hx * 0.06))); hp.Add(new PointF(W, H)); g.FillPolygon(hb, hp.ToArray()); }
		foreach (It it in items)
		{
			if (it.Kind != 2) for (int tk = 1; tk <= 3; tk++) using (SolidBrush tb2 = new SolidBrush(Color.FromArgb(100 / tk, 255, 236, 160))) { float tr = (it.Kind == 1 ? 8f : 6f) - tk; g.FillEllipse(tb2, it.X - tr, it.Y - tk * it.V * 0.024f - tr, tr * 2, tr * 2); }
			if (it.Kind == 3)
			{
				using (SolidBrush gl = new SolidBrush(Color.FromArgb(60, 140, 230, 255))) g.FillEllipse(gl, it.X - 22, it.Y - 22, 44, 44);
				using (SolidBrush cb2 = new SolidBrush(Color.FromArgb(240, 250, 255))) g.FillEllipse(cb2, it.X - 14, it.Y - 14, 28, 28);
				using (Pen cp = new Pen(Color.FromArgb(70, 150, 220), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawEllipse(cp, it.X - 14, it.Y - 14, 28, 28); g.DrawLine(cp, it.X, it.Y, it.X, it.Y - 8); g.DrawLine(cp, it.X, it.Y, it.X + 6, it.Y + 3); }
			}
			else if (it.Kind == 4)
			{
				using (SolidBrush gl = new SolidBrush(Color.FromArgb(60, 255, 120, 130))) g.FillEllipse(gl, it.X - 22, it.Y - 22, 44, 44);
				using (Pen mp = new Pen(Color.FromArgb(235, 70, 80), 7f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat }) g.DrawArc(mp, it.X - 12, it.Y - 12, 24, 24, 180, 180);
				using (SolidBrush tipb = new SolidBrush(Color.White)) { g.FillRectangle(tipb, it.X - 15.5f, it.Y, 7, 8); g.FillRectangle(tipb, it.X + 8.5f, it.Y, 7, 8); }
			}
			else if (it.Kind == 2)
			{
				if (Spr.Get("ms_cloud") != null)
				{
					float wob = (float)Math.Sin(t * 6 + it.X) * 1.5f, flick = 0.55f + 0.45f * (float)Math.Abs(Math.Sin(t * 13 + it.X));
					using (SolidBrush gl = new SolidBrush(Color.FromArgb(40, 120, 130, 190))) g.FillEllipse(gl, it.X - 28, it.Y - 20, 56, 40);
					int cf0 = (int)(t * 9 + it.X) % 6; if (cf0 < 0) cf0 = 0;
					if (Spr.Get("ms_cl0") != null) { DrawFx(g, "rd" + cf0, it.X, it.Y + 34f, 42f, 0.8f); Spr.Draw(g, "ms_cl" + cf0, it.X, it.Y - 4 + wob, 62f, 62f * 112f / 160f, 0f); Spr.Draw(g, "ms_bl" + cf0, it.X + 1, it.Y + 16, 18f, 29f, 0f); }
					else {
					Spr.Draw(g, "ms_cloud", it.X, it.Y - 4 + wob, 54f, 54f * 0.84f, (float)Math.Sin(t * 3 + it.X) * 4f);
					using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)(70 * flick), 255, 255, 190))) g.FillEllipse(fb, it.X - 8, it.Y + 4, 16, 22);
					Spr.Draw(g, "ms_bolt", it.X + 1, it.Y + 15, 22f * (0.8f + 0.3f * flick), 28f * (0.8f + 0.3f * flick), -8f); }
				}
				else {
				using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 96, 108, 140))) { g.FillEllipse(b, it.X - 24, it.Y - 10 + (float)Math.Sin(t * 6 + it.X) * 1.5f, 48, 26); g.FillEllipse(b, it.X - 14, it.Y - 20, 30, 26); }
				using (GraphicsPath bolt = new GraphicsPath()) { bolt.AddPolygon(new PointF[] { new PointF(it.X + 2, it.Y - 6), new PointF(it.X - 6, it.Y + 6), new PointF(it.X, it.Y + 6), new PointF(it.X - 3, it.Y + 16), new PointF(it.X + 7, it.Y + 2), new PointF(it.X + 1, it.Y + 2) }); using (SolidBrush yb = new SolidBrush(Color.FromArgb(255, 226, 90))) g.FillPath(yb, bolt); }
				}
			}
			else
			{
				GraphicsState gs = g.Save(); g.TranslateTransform(it.X, it.Y); g.RotateTransform(it.R);
				{ float pl = 0.75f + 0.25f * (float)Math.Sin(t * 8 + it.X); using (SolidBrush gl = new SolidBrush(Color.FromArgb((int)((it.Kind == 1 ? 70 : 34) * pl), 255, 214, 90))) g.FillEllipse(gl, -(it.Kind == 1 ? 24 : 17) * pl, -(it.Kind == 1 ? 24 : 17) * pl, (it.Kind == 1 ? 48 : 34) * pl, (it.Kind == 1 ? 48 : 34) * pl); }
				StarShape(g, 0, 0, it.Kind == 1 ? 17 : 13, it.Kind == 1 ? Color.FromArgb(255, 196, 40) : Color.FromArgb(255, 238, 150));
				g.Restore(gs);
			}
		}
		foreach (Rp rp in rps) { float u = rp.T / rp.Mx, rr = rp.Ring ? 10f + 46f * u : 4f + 14f * u; using (Pen rpn = new Pen(Color.FromArgb((int)(rp.C.A * (1f - u)), rp.C.R, rp.C.G, rp.C.B), rp.Ring ? 3f * (1f - u) + 0.6f : 2f)) { if (rp.Ring) g.DrawEllipse(rpn, rp.X - rr, rp.Y - rr, rr * 2, rr * 2); else g.DrawEllipse(rpn, rp.X - rr, rp.Y - rr * 0.35f, rr * 2, rr * 0.7f); } }
		{ float cgy = H - 78; using (SolidBrush cg = new SolidBrush(Color.FromArgb(26 + (int)(10 * Math.Sin(t * 5)), 255, 236, 160))) g.FillEllipse(cg, px - 44, cgy - 6, 88, 30); }
		float sh = shake > 0 ? (float)Math.Sin(t * 70) * 4 : 0f;
		if (magnetT > 0) using (Pen mp = new Pen(Color.FromArgb((int)(60 + 60 * Math.Abs(Math.Sin(t * 8))), 255, 120, 130), 3f)) g.DrawEllipse(mp, px - 52, H - 112, 104, 104);
		DrawMascot(g, px + sh, H - 22, 88 * (1f + 0.1f * (float)Math.Max(0, catchT) / 0.18f), Clamp(vx / 40f, -12, 12));
		if (magnetT > 0) Txt(g, "자석 " + Math.Ceiling(magnetT) + "초", 11f, 1, Color.FromArgb(255, 150, 160), 14, 76, 180, 22, StringAlignment.Near);
		double pa = t - popT;
		if (pa < 0.6 && pop.Length > 0) Txt(g, pop, 14f, 2, Color.FromArgb((int)(255 * (1 - pa / 0.6)), pop[0] == '+' ? Color.FromArgb(255, 226, 120) : Color.FromArgb(255, 130, 140)), popAt.X - 30, popAt.Y - 30 - (float)pa * 40, 60, 26, StringAlignment.Center);
		if (combo >= 3) { float cs = 11f + Math.Min(7f, combo * 0.25f) + (combo % 5 == 0 && flash > 0 ? 3f : 0f); Txt(g, combo + " 연속!", cs, 2, combo >= 10 ? Color.FromArgb(255, 170, 80) : Color.FromArgb(255, 214, 90), 14, 44, 200, 28, StringAlignment.Near); }
		if (flash > 0) using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)Math.Min(90, flash * 360), lightT > 0 ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 236, 160)))) g.FillRectangle(fb, 0, 0, W, H);
	}
}

// ── 2. 장애물 점프 ──────────────────────────────────────────
internal sealed class JumpGame : MiniGame
{
	private class Ob { public float X, W, H, Clear = 99f; public int Kind; public bool Done; }   // Kind 0 작은 돌 · 1 높은 기둥 · 2 긴 울타리 · 3 낭떠러지
	private class Coin { public float X, Y; public float Ph; }
	private class Pop { public float X, Y, Age; public string S = "+10"; }
	private class Pf { public float X, W, Top; }   // 공중 발판: 위로 올라탈 수 있어요
	private class Item { public float X, Y, Ph; public int K; }   // K 0 거대화 · 1 시간 느리게
	private readonly List<Pf> pfs = new List<Pf>(); private readonly List<Item> items = new List<Item>(); private float giantT, slowT, itemT = 9f, grace; private double lastGiant = -99;
	private readonly List<Ob> obs = new List<Ob>(); private readonly List<Coin> coins = new List<Coin>(); private readonly List<Pop> pops = new List<Pop>();
	private float fallT, buf, chainT, y, vy, speed, dist, nextGap, hitT; private int chain, nearCnt; private bool onGround, falling; private double time; private int coinPts, coinCnt;
	private const float Ground = H - 120, Gv = 2300f, Jv = -720f, Cut = -440f, HoldG = 0.7f;
	private bool hold;
	private class Dst { public float X, Y, Vx, Vy, Age, R, Life; }
	private readonly List<Dst> dst = new List<Dst>(); private float runT, landT; private bool wasAir;
	private void Puff(float x, float y, int n, float spd, float r) { for (int k = 0; k < n; k++) dst.Add(new Dst { X = x + (float)(Rnd.NextDouble() - 0.5) * 8f, Y = y, Vx = (float)(Rnd.NextDouble() * 2 - 1) * spd, Vy = -(float)Rnd.NextDouble() * spd * 0.7f, R = r * (0.7f + (float)Rnd.NextDouble() * 0.6f), Life = 0.35f + (float)Rnd.NextDouble() * 0.2f }); }
	public override string Costume { get { return "캠핑"; } }
	public override string Name { get { return "장애물 점프"; } }
	public override string Key { get { return "jump"; } }
	public override string Desc { get { return "코인을 먹고 낭떠러지를 넘어요!"; } }
	public override string Help { get { return "톡 누르면 낮은 점프, 꾹 누르면 높고 긴 점프예요\n공중 발판 위에 올라탈 수 있어요 (위에 코인!)\n별 아이템 = 거대화(장애물을 부수고 낭떠러지도 건너요) · 시계 아이템 = 시간이 느려져요\n코인을 이어서 먹으면 콤보 보너스! 장애물을 아슬아슬하게 넘으면 +5\n(낭떠러지 끝·착지 직전에 눌러도 점프돼요)"; } }
	public override string RightText { get { return "코인 " + coinCnt; } }
	public override void Reset() { dst.Clear(); runT = 0; landT = 0; wasAir = false; obs.Clear(); coins.Clear(); pops.Clear(); pfs.Clear(); items.Clear(); giantT = 0; slowT = 0; itemT = 9f; grace = 0; lastGiant = -99; Score = 0; y = Ground; vy = 0; speed = 300; dist = 0; nextGap = 380; onGround = true; falling = false; Over = false; hitT = 0; time = 0; coinPts = 0; coinCnt = 0; fallT = 0; buf = 0; chainT = 0; chain = 0; nearCnt = 0; }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 205, 70))) g.FillEllipse(b, cx - 13, cy - 16, 26, 26);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 238, 160))) g.FillEllipse(b, cx - 8, cy - 11, 10, 10);
		using (Pen p = new Pen(Color.FromArgb(150, 160, 190), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, cx - 26, cy + 18, cx - 8, cy + 18); g.DrawLine(p, cx + 8, cy + 18, cx + 26, cy + 18); }
	}
	private void Jump()
	{
		if (Over) return;
		if ((onGround && !falling) || (falling && fallT < 0.09f && y < Ground + 12f)) { falling = false; fallT = 0; vy = Jv; onGround = false; Sfx.Play("pluck"); }   // 낭떠러지 끝에서 조금 늦게 눌러도 점프돼요
		else buf = 0.12f;                                                                                                 // 착지 직전에 눌러도 착지하자마자 점프해요
	}
	public override void Click(float x, float yy) { Jump(); }
	public override void Press(Keys k) { if (k == Keys.Space || k == Keys.Up || k == Keys.W) Jump(); }
	// 장애물·낭떠러지 위에 점프 곡선을 따라 코인을 깔아 줘요
	private void AddCoins(float x0, float w, float baseH)
	{
		int n = w > 100 ? 5 : 3;
		for (int i = 0; i < n; i++) { float k = (i + 0.5f) / n; coins.Add(new Coin { X = x0 + w * k, Y = Ground - baseH - 42f * (float)Math.Sin(Math.PI * k), Ph = (float)(Rnd.NextDouble() * 6.28) }); }
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		hold = inp.Hold; time += dt; if (buf > 0) buf -= dt; if (chainT > 0) { chainT -= dt; if (chainT <= 0) chain = 0; }
		speed = Math.Min(760f, 300f + (float)time * 11f); if (slowT > 0f) { slowT -= dt; speed *= 0.62f; } if (giantT > 0f) { giantT -= dt; if (giantT <= 0f) grace = 0.9f; } if (grace > 0f) grace -= dt; itemT -= dt; bool giant = giantT > 0f;
		dist += speed * dt; Score = (int)(dist / 18f) + coinPts;
		if (vy < 0 && hold) vy += Gv * HoldG * dt; else { if (vy < Cut && !hold) vy = Cut; vy += Gv * dt; }
		float prevY = y; y += vy * dt;
		float floor = Ground; foreach (Pf pf in pfs) if (vy >= 0f && 70 > pf.X - 8 && 70 < pf.X + pf.W + 8 && prevY <= pf.Top + 3f && y >= pf.Top) floor = Math.Min(floor, pf.Top);
		bool overPit = false;
		foreach (Ob o in obs) if (!giant && o.Kind == 3 && 70 > o.X + 12 && 70 < o.X + o.W - 12) overPit = true;
		if (giant && falling && y > Ground - 2f) { falling = false; y = Ground; vy = 0; }
		if (falling || (overPit && y >= Ground)) { if (!falling) fallT = 0; falling = true; fallT += dt; onGround = false; if (y > Ground + 95 && !Over) { Over = true; hitT = 0.3f; Sfx.Play("fall"); } }
		else if (y >= floor) { y = floor; vy = 0; onGround = true; if (buf > 0) { buf = 0; Jump(); } }
		else if (onGround && y < floor - 1.5f) onGround = false;
		bool air = !onGround && !falling;
		if (wasAir && onGround) { Puff(64f, Ground, 7, 90f, 6f); landT = 0.16f; }
		if (!wasAir && air && vy < -300f) Puff(64f, Ground, 5, 70f, 5f);
		wasAir = air || falling;
		if (onGround && !Over) { runT -= dt; if (runT <= 0f) { runT = 0.085f; Puff(54f, Ground - 1f, 1, 30f, 4f); } }
		if (landT > 0) landT -= dt;
		for (int di = dst.Count - 1; di >= 0; di--) { Dst d = dst[di]; d.Age += dt; d.X += (d.Vx - speed * 0.85f) * dt; d.Y += d.Vy * dt; d.Vy += 60f * dt; if (d.Age > d.Life) dst.RemoveAt(di); }
		nextGap -= speed * dt;
		if (nextGap <= 0)
		{
			double r = Rnd.NextDouble(); int kind = r < 0.26 ? 0 : r < 0.42 ? 1 : r < 0.54 ? 2 : r < 0.72 ? 3 : 4;
			if (kind == 3 && time < 6) kind = 0; if (kind == 4 && time < 3) kind = 0;
			float sx = W + 40;
			if (kind == 0) { bool two = Rnd.NextDouble() < 0.3; obs.Add(new Ob { X = sx, W = 32, H = 40, Kind = 0 }); if (two) obs.Add(new Ob { X = sx + 62, W = 32, H = 40, Kind = 0 }); AddCoins(sx - 6, two ? 100 : 44, 82f); }
			else if (kind == 1) { obs.Add(new Ob { X = sx, W = 36, H = 104, Kind = 1 }); AddCoins(sx - 20, 76, 146f); }
			else if (kind == 2) { obs.Add(new Ob { X = sx, W = 150, H = 34, Kind = 2 }); AddCoins(sx, 150, 90f); }
			else if (kind == 3) { float pw = 120f + (float)Rnd.NextDouble() * 50f; obs.Add(new Ob { X = sx, W = pw, H = 0, Kind = 3 }); AddCoins(sx, pw, 108f); }
			else { float top = Ground - 92f; pfs.Add(new Pf { X = sx, W = 140f, Top = top }); if (Rnd.NextDouble() < 0.7) obs.Add(new Ob { X = sx + 54f, W = 32, H = 40, Kind = 0 }); for (int ci = 0; ci < 4; ci++) coins.Add(new Coin { X = sx + 22f + ci * 32f, Y = top - 28f, Ph = (float)(Rnd.NextDouble() * 6.28) }); }
			if (itemT <= 0f && kind != 1) { itemT = 13f + (float)Rnd.NextDouble() * 6f; float ix = kind == 0 ? sx + 16f : (kind == 2 ? sx + 75f : (kind == 3 ? sx + 60f : sx + 70f)), iy = kind == 0 ? Ground - 124f : (kind == 2 ? Ground - 132f : (kind == 3 ? Ground - 150f : Ground - 92f - 62f)); items.Add(new Item { X = ix, Y = iy, K = (Rnd.NextDouble() < 0.18 && time - lastGiant > 40) ? 0 : 1 }); if (items[items.Count - 1].K == 0) lastGiant = time; }
			nextGap = speed * ((time > 40 ? 0.68f : 0.8f) + (float)Rnd.NextDouble() * 0.6f) + 170f + (kind == 2 ? 150f : 0f) + (kind == 3 ? 200f : 0f) + (kind == 4 ? 90f : 0f);
		}
		RectangleF me = giant ? new RectangleF(70 - 34, y - 132, 68, 128) : new RectangleF(70 - 20, y - 74, 40, 70);
		for (int i = pfs.Count - 1; i >= 0; i--) { pfs[i].X -= speed * dt; if (pfs[i].X < -240) pfs.RemoveAt(i); }
		for (int i = items.Count - 1; i >= 0; i--) { Item it = items[i]; it.X -= speed * dt; float iy2 = it.Y + (float)Math.Sin(time * 4 + it.Ph) * 4f; float ddx = Clamp(it.X, me.Left, me.Right) - it.X, ddy = Clamp(iy2, me.Top, me.Bottom) - iy2; if (!Over && ddx * ddx + ddy * ddy < 22f * 22f) { if (it.K == 0) { giantT = 6f; pops.Add(new Pop { X = 90f, Y = y - 150f, S = "거대화! 다 부숴요" }); } else { slowT = 5f; pops.Add(new Pop { X = 90f, Y = y - 100f, S = "시간이 느려져요!" }); } Sfx.Play("fanfare"); Fx.Burst(it.X, iy2, Color.FromArgb(255, 230, 120), 14); Shake = 0.25f; coinPts += 15; items.RemoveAt(i); } else if (it.X < -30) items.RemoveAt(i); }
		for (int i = obs.Count - 1; i >= 0; i--)
		{
			Ob o = obs[i]; o.X -= speed * dt;
			if (o.Kind != 3 && giant && me.IntersectsWith(new RectangleF(o.X, Ground - o.H, o.W, o.H))) { Fx.Burst(o.X + o.W / 2f, Ground - o.H / 2f, Color.FromArgb(255, 220, 150), 12); Sfx.Play("thud2"); Shake = 0.3f; coinPts += 8; pops.Add(new Pop { X = o.X, Y = Ground - o.H - 20f, S = "파괴! +8" }); obs.RemoveAt(i); continue; }
			if (o.Kind != 3 && !falling && grace <= 0f)
			{
				RectangleF r = new RectangleF(o.X + 4, Ground - o.H + 4, o.W - 8, o.H - 4);
				if (me.IntersectsWith(r)) { if (!Over) { Sfx.Play("bad_big"); Shake = 0.5f; Fx.Burst(70f, y - 30f, Color.FromArgb(255, 200, 150), 10); } Over = true; hitT = 0.3f; }
					else if (o.X < me.Right && o.X + o.W > me.Left) o.Clear = Math.Min(o.Clear, (Ground - o.H) - y);   // 장애물 위를 지날 때 얼마나 아슬아슬했는지
				}
				if (!o.Done && o.Kind != 3 && o.X + o.W < me.Left) { o.Done = true; if (o.Clear >= 0f && o.Clear < 16f && !Over) { nearCnt++; coinPts += 5; Sfx.Play("ok"); pops.Add(new Pop { X = 70f, Y = y - 90f, S = "아슬아슬! +5" }); Fx.Burst(70f, y - 40f, Color.FromArgb(120, 230, 255), 5); }
			}
			if (o.X < -220) obs.RemoveAt(i);
		}
		for (int i = coins.Count - 1; i >= 0; i--)
		{
			Coin c = coins[i]; c.X -= speed * dt;
			float cy = c.Y + (float)Math.Sin(time * 5 + c.Ph) * 3f;
			float nx = Clamp(c.X, me.Left, me.Right), ny = Clamp(cy, me.Top, me.Bottom);
			if (!Over && (c.X - nx) * (c.X - nx) + (cy - ny) * (cy - ny) < 14f * 14f) { chain = chainT > 0 ? chain + 1 : 1; chainT = 0.75f; int cb = 10 + Math.Min(chain - 1, 5) * 2; coinPts += cb; coinCnt++; Sfx.Play("coin"); pops.Add(new Pop { X = c.X, Y = cy - 20f, S = chain >= 2 ? "+" + cb + " x" + chain : "+10" }); Fx.Burst(c.X, cy, Color.FromArgb(255, 214, 80), 7); Shake = 0.12f; pops.Add(new Pop { X = c.X, Y = cy }); coins.RemoveAt(i); continue; }
			if (c.X < -30) coins.RemoveAt(i);
		}
		for (int i = pops.Count - 1; i >= 0; i--) { pops[i].Age += dt; pops[i].Y -= 50 * dt; if (pops[i].Age > 0.7f) pops.RemoveAt(i); }
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(28, 22, 70), Color.FromArgb(96, 70, 140)); Twinkle(g, t);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 255, 240, 200))) g.FillEllipse(b, 286, 80, 84, 84);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 244, 214))) g.FillEllipse(b, 298, 92, 60, 60);
		for (int layer = 0; layer < 2; layer++)
		{
			float k = layer == 0 ? 0.12f : 0.3f, amp = layer == 0 ? 90f : 56f, baseY = Ground - (layer == 0 ? 30f : 8f);
			List<PointF> pts = new List<PointF> { new PointF(-10, Ground) };
			for (float x = -10; x <= W + 10; x += 8) { float wx = x + dist * k; float hh = amp * (0.55f + 0.3f * (float)Math.Sin(wx * 0.011 + layer * 2) + 0.15f * (float)Math.Sin(wx * 0.031 + layer)); pts.Add(new PointF(x, baseY - hh)); }
			pts.Add(new PointF(W + 10, Ground));
			using (SolidBrush b = new SolidBrush(layer == 0 ? Color.FromArgb(60, 46, 100) : Color.FromArgb(44, 34, 84))) g.FillPolygon(b, pts.ToArray());
		}
		for (int ff = 0; ff < 9; ff++) { float fxp = ((ff * 97 - (float)dist * 0.18f) % 440f + 440f) % 440f - 20f, fyp = Ground - 40 - (ff * 53) % 150 + (float)Math.Sin(t * 1.6 + ff) * 8f; using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)(40 + 50 * Math.Abs(Math.Sin(t * 2 + ff))), 255, 240, 150))) g.FillEllipse(fb, fxp - 2.2f, fyp - 2.2f, 4.4f, 4.4f); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 34, 70))) g.FillRectangle(b, 0, Ground, W, H - Ground);
		using (LinearGradientBrush gb = new LinearGradientBrush(new RectangleF(0, Ground, W, 26), Color.FromArgb(70, 120, 110, 180), Color.FromArgb(0, 120, 110, 180), 90f)) g.FillRectangle(gb, 0, Ground, W, 26);
		using (Pen p = new Pen(Color.FromArgb(120, 130, 180), 2f)) g.DrawLine(p, 0, Ground, W, Ground);
		float off = -(dist % 60f);
		using (Pen p = new Pen(Color.FromArgb(60, 255, 255, 255), 2f)) for (float x = off; x < W; x += 60) g.DrawLine(p, x, Ground + 16, x + 28, Ground + 16);
		// 낭떠러지: 땅을 파내고 어두운 구멍을 보여줘요
		foreach (Ob o in obs)
		{
			if (o.Kind != 3) continue;
			using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 6, 24))) g.FillRectangle(b, o.X, Ground - 1, o.W, H - Ground + 1);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 130, 180))) { g.FillRectangle(b, o.X - 3, Ground - 1, 3, H - Ground + 1); g.FillRectangle(b, o.X + o.W, Ground - 1, 3, H - Ground + 1); }
		}
		foreach (Ob o in obs)
		{
			if (o.Kind == 3) continue;
			RectangleF r = new RectangleF(o.X, Ground - o.H, o.W, o.H);
			{   // 공무원 장애물: 안전콘 · 결재 서류탑 · 공사 방벽 (그림이 없으면 아래 도형으로 그려요)
				bool okSp = true;
				if (o.Kind == 0) okSp = Spr.Draw(g, "ms_n_cone", r.X + r.Width / 2f, r.Bottom - 24f, 34f, 44f, 0f);
				else if (o.Kind == 1) { int stk = 3; float ph = o.H / stk; for (int si = 0; si < stk && okSp; si++) okSp = Spr.Draw(g, "ms_n_paper", r.X + r.Width / 2f + (si % 2 == 0 ? -1.5f : 1.5f), r.Bottom - ph * (si + 0.5f), o.W * 1.1f, o.W * 1.1f * 126f / 111f, 0f); }
				else if (o.Kind == 2) { int nb = 3; float bw = o.W / nb; for (int si = 0; si < nb && okSp; si++) okSp = Spr.Draw(g, "ms_n_barr", r.X + bw * (si + 0.5f), r.Bottom - 20f, bw * 1.05f, 40f, 0f); }
				if (okSp) continue;
			}
			Color c = o.Kind == 0 ? Color.FromArgb(120, 130, 168) : o.Kind == 1 ? Color.FromArgb(214, 120, 96) : Color.FromArgb(96, 170, 150);
			using (GraphicsPath gp = Gfx.Round(r, o.Kind == 2 ? 14 : 10)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, gp);
			using (GraphicsPath gp = Gfx.Round(new RectangleF(r.X + 4, r.Y + 4, r.Width * 0.45f, Math.Min(r.Height * 0.4f, 18)), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillPath(b, gp);
			using (SolidBrush shb = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(shb, r.X - 4, Ground - 3, r.Width + 8, 8);
			if (o.Kind == 0) using (Pen cp = new Pen(Color.FromArgb(70, 40, 44, 80), 1.6f)) { g.DrawLine(cp, r.X + r.Width * 0.55f, r.Y + 6, r.X + r.Width * 0.4f, r.Y + 20); g.DrawLine(cp, r.X + r.Width * 0.4f, r.Y + 20, r.X + r.Width * 0.58f, r.Y + 30); }
			if (o.Kind == 1) { using (SolidBrush cb = new SolidBrush(Color.FromArgb(70, 120, 50, 40))) { g.FillRectangle(cb, r.X, r.Y + r.Height * 0.3f, r.Width, 6); g.FillRectangle(cb, r.X, r.Y + r.Height * 0.62f, r.Width, 6); } using (SolidBrush lb = new SolidBrush(Color.FromArgb(150 + (int)(50 * Math.Sin(t * 6 + o.X)), 255, 214, 110))) g.FillEllipse(lb, r.X + r.Width / 2 - 5, r.Y - 9, 10, 10); }
			if (o.Kind == 2) using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 2f)) for (float fx = r.X + 20; fx < r.Right - 8; fx += 26) g.DrawLine(p, fx, r.Y + 8, fx, r.Bottom - 6);
			if (o.Kind != 0 && o.X > W - 150 && o.X < W + 40) Txt(g, "꾹!", 12f, 2, Color.FromArgb(255, 214, 90), o.X + o.W / 2 - 30, r.Y - 26, 60, 20, StringAlignment.Center);
		}
		foreach (Pf pf in pfs)
		{
			RectangleF pr = new RectangleF(pf.X, pf.Top, pf.W, 14f);
			using (SolidBrush shb = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(shb, pr.X + 10, Ground - 3f, pr.Width - 20, 7f);
			using (GraphicsPath gp = Gfx.Round(pr, 6)) { using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(pr.X, pr.Y - 1, pr.Width, pr.Height + 2), Color.FromArgb(214, 160, 100), Color.FromArgb(128, 84, 52), 90f)) g.FillPath(lb, gp); using (Pen op = new Pen(Color.FromArgb(120, 60, 36, 20), 1.4f)) g.DrawPath(op, gp); }
			using (SolidBrush hb = new SolidBrush(Color.FromArgb(120, 255, 240, 200))) g.FillRectangle(hb, pr.X + 6, pr.Y + 2, pr.Width - 12, 2.5f);
			using (Pen lp = new Pen(Color.FromArgb(150, 130, 120, 170), 2f)) { g.DrawLine(lp, pr.X + 14, pr.Bottom, pr.X + 14, Ground); g.DrawLine(lp, pr.Right - 14, pr.Bottom, pr.Right - 14, Ground); }
		}
		foreach (Item it in items)
		{
			float iy = it.Y + (float)Math.Sin(t * 4 + it.Ph) * 4f; using (SolidBrush b = new SolidBrush(Color.FromArgb(it.K == 0 ? 70 : 60, it.K == 0 ? 255 : 120, it.K == 0 ? 200 : 220, it.K == 0 ? 90 : 255))) g.FillEllipse(b, it.X - 24, iy - 24, 48, 48);
			if (!Spr.Draw(g, it.K == 0 ? "ms_cstar" : "ms_of_clock", it.X, iy, 34f, 34f, 0f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 90))) g.FillEllipse(b, it.X - 13, iy - 13, 26, 26);
		}
		foreach (Coin c in coins)
		{
			float cy = c.Y + (float)Math.Sin(t * 5 + c.Ph) * 3f, sq = 0.35f + 0.65f * Math.Abs((float)Math.Cos(t * 4 + c.Ph));   // 빙글빙글 도는 느낌
			float rw = 11f * sq;
			using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 214, 90))) g.FillEllipse(b, c.X - 17, cy - 17, 34, 34);
			bool csp = Spr.Draw(g, "coin" + (1 + (((int)(t * 9 + c.Ph * 2)) % 4 + 4) % 4), c.X, cy, 28f, 28f, 0f); if (!csp) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 205, 70))) g.FillEllipse(b, c.X - rw, cy - 11, rw * 2, 22);
			if (!csp) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 238, 160))) g.FillEllipse(b, c.X - rw * 0.55f, cy - 7, rw * 1.1f, 14);
		}
		if (speed > 420f) { float sa = Math.Min(1f, (speed - 420f) / 300f); using (Pen sp = new Pen(Color.FromArgb((int)(60 * sa), 255, 255, 255), 1.6f)) for (int sl = 0; sl < 7; sl++) { float sx0 = ((sl * 131 - (float)dist * 1.6f) % 460f + 460f) % 460f - 30f, sy0 = 120 + (sl * 71) % 300; g.DrawLine(sp, sx0, sy0, sx0 + 34f + 30f * sa, sy0); } }
		{ float hgt = Math.Max(0f, Ground - y); float sw = 38f * (1f - Math.Min(0.55f, hgt / 280f)); if (!falling) using (SolidBrush sb = new SolidBrush(Color.FromArgb((int)(70 * (1f - Math.Min(0.6f, hgt / 280f))), 0, 0, 0))) g.FillEllipse(sb, 70 - sw, Ground - 4f, sw * 2, 9f); }
		foreach (Dst d in dst) { float u = d.Age / d.Life, rr = d.R * (0.6f + 0.9f * u); using (SolidBrush db = new SolidBrush(Color.FromArgb((int)(120 * (1f - u)), 200, 190, 235))) g.FillEllipse(db, d.X - rr, d.Y - rr, rr * 2, rr * 2); }
		float tilt = falling ? 20f : (onGround ? (float)Math.Sin(t * 16) * 3 : Clamp(vy / 90f, -10, 14));
		float bob = onGround ? Math.Abs((float)Math.Sin(t * 16)) * 4 : 0;
		if (giantT > 0f && (giantT > 1.5f || ((int)(t * 10) & 1) == 0)) using (SolidBrush ab = new SolidBrush(Color.FromArgb(50, 255, 220, 90))) g.FillEllipse(ab, 70 - 70, y - 150, 140, 150);
		if (slowT > 0f) using (Pen sp2 = new Pen(Color.FromArgb(70, 150, 230, 255), 2f)) for (int q = 0; q < 3; q++) g.DrawEllipse(sp2, 200 - 60 - q * 40 + (float)(t * 20 % 40), 300 - 60 - q * 40 + (float)(t * 20 % 40), 120 + q * 80 - (float)(t * 40 % 80), 120 + q * 80 - (float)(t * 40 % 80));
		DrawMascot(g, 70, y - bob, (giantT > 0f ? 150f : 84f) * (1f + (landT > 0 ? -0.08f * landT / 0.16f : (!onGround && vy < -200f ? 0.06f : 0f))), tilt);
		foreach (Pop p in pops) Txt(g, p.S, 13f, 2, Color.FromArgb((int)(255 * Math.Max(0, 1 - p.Age / 0.7f)), 255, 226, 120), p.X - 70, p.Y - 12, 140, 24, StringAlignment.Center);
		if (giantT > 0f) Txt(g, "거대화 " + giantT.ToString("0.0") + "초", 11f, 2, Color.FromArgb(255, 255, 214, 90), 10, 62, 120, 18, StringAlignment.Near);
		if (slowT > 0f) Txt(g, "시간 느리게 " + slowT.ToString("0.0") + "초", 11f, 2, Color.FromArgb(255, 150, 230, 255), 10, giantT > 0f ? 80 : 62, 140, 18, StringAlignment.Near);
		if (hitT > 0) hitT -= 0.016f;
	}
}
// ── 3. 야구공 치기 (3차원 물리) ───────────────────────────────
// 홈플레이트 뒤에서 본 시점. 실제 타격처럼 방망이는 '수평면'에서 몸 뒤 → 몸 앞으로 돌아요(타자 오른손 기준).
// 방망이 면의 방향(법선)·속도로 공이 튕겨 나가서, 타이밍이 빠르면 당겨 치고(왼쪽), 늦으면 밀어 치고(오른쪽), 너무 어긋나면 파울이에요.
internal sealed class BatGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private const int Pitches = 10; private static bool ringDone; private static float calib = 0.03f; private float lastDtm = 9f;   // calib: 늘 늦게 누르는 만큼 스윙을 앞당겨 주는 자동 보정(초) · 화면·입력 지연도 함께 메워요   // 안타 이상을 한 번 치면 초록 링 도움이 사라져요
	private const float CamX = -0.27f, CamY = 1.55f, CamZ = -1.57f, FL = 270f, HorY = 200f;     // 카메라(홈 뒤, 타자 눈높이보다 조금 위)
	private const float BatL = 1.0f, SwT = 0.22f, Phi0 = -125f, Phi1 = 95f, Tilt = 0.17f;   // 방망이 길이(m) · 스윙 시간 · 시작/끝 각도 · 끝이 살짝 처짐
	private const float AtkK0 = 0.0012f;   // (공 높이에 따라 달라져요: SetPlane)
	private static float cTilt = 0.17f, cAtk = 0.0012f;   // 지금 스윙면의 기울기(방망이 끝이 처지는 정도) · 스윙 각도당 올라가는 높이
	// 공이 높으면 방망이 끝이 내려오며 찍어치고, 낮으면 끝이 올라가며 퍼올려요 (스윙 궤적이 공 높이마다 달라져요)
	private static void SetPlane(float h) { cTilt = Clamp((1.06f - h) * 0.85f, 0f, 0.55f);   // 손은 공보다 위에 있어요: 낮은 공은 방망이 끝이 아래로 기울어 내려치고, 높은 공은 거의 수평으로 휘둘러요
		 cAtk = Clamp(0.0012f + (0.70f - h) * 0.0040f, -0.0010f, 0.0030f); }
	private const float Sw = 0.76f, PhiM = -7f;   // 방망이 중심(빨간 띠)이 손에서 떨어진 비율 · 스윙 중간 각도
	// 마우스가 가리키는 '방망이 중심' 높이 hc를 만들려면 손이 어느 높이여야 하는지 (스윙면 기울기까지 계산해서 커서 자리에 정확히 와요)
	private static float HandFor(float hc) { float yh = hc; for (int i = 0; i < 8; i++) { SetPlane(yh); yh = hc - cAtk * PhiM + Sw * BatL * cTilt; } SetPlane(yh); return yh; }
	private static float PlaneDeg() { return (cTilt - 0.17f) * 55f; }   // 스윙 각도 1도당 방망이가 올라가는 높이(m)
	private const float HandX = -0.62f, HandZ = 0.10f, BallR = 0.052f, PZ0 = 18.4f, PY0 = 1.85f;
	private int idx, phase, kmh; private double wait;
	private float pt, ptPlate, pv, px0, pa, pb, pc, pd, xp, yp; private int ptype; private string pname = ""; private float nameT; private static readonly string[] PNames = { "직구", "커브", "슬라이더", "체인지업", "포크볼", "투심", "직구" };
	private float bx, by, bz, bvx, bvy, bvz; private bool swinging, hit; private float swT, batYh, swingAt;
	private readonly List<float[]> fl = new List<float[]>(); private float flT, flDur, landD, apex, loft, azim, vsp, hitFlash; private int flKind; private double rndSeed;
	private float fwT = -1f; private int fwSeed; private bool bfOn; private float bfX, bfY, bfVx, bfVy, bfRot, bfRv, bfT;
	private string res = ""; private int resScore; private float resT; private string hint = "", timeMsg = ""; private float mouseH = 0.55f; private bool mouseSeen;
	private readonly List<float[]> trail = new List<float[]>();
	public override string Costume { get { return "g_bat"; } }
	public override string Name { get { return "야구공 치기"; } }
	public override string Key { get { return "bat"; } }
	public override string Desc { get { return "타이밍을 맞춰 시원하게!"; } }
	public override string Help { get { return "마우스를 위아래로 움직이면 방망이 높이(점선)가 따라와요 → 공 높이(노란 십자)에 맞춰요\n공이 노란 '문'을 지나는 순간 클릭(스페이스)해서 휘둘러요 (초록 링이 줄어들다 공에 딱 닫히는 순간이 치는 때 · 늘 늦게 누르면 저절로 조금 앞당겨 줘요)\n마우스 커서가 곧 방망이 중심(빨간 띠)이에요 → 점선 원 안으로 공이 지나가면 맞아요 (공이 지나갈 자리는 노란 십자)\n빠르면 왼쪽(당겨 치기) · 늦으면 오른쪽(밀어 치기)\n커브·포크볼·슬라이더 같은 변화구도 와요 (공 위에 구질 이름이 떠요)\n너무 빠르거나 늦으면 파울 · 10구 승부 · 홈런은 100점"; } }
	public override string RightText { get { return Math.Min(idx + 1, Pitches) + " / " + Pitches + "구"; } }
	public override void Reset() { idx = 0; phase = 0; wait = 0.9; Score = 0; Over = false; res = ""; resT = 0; swinging = false; hit = false; bfOn = false; flKind = -1; hint = ""; fl.Clear(); trail.Clear(); fwT = -1f; }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - 15, cy - 15, 30, 30);
		using (Pen p = new Pen(Color.FromArgb(220, 70, 80), 2f)) { g.DrawArc(p, cx - 22, cy - 12, 20, 24, -50, 100); g.DrawArc(p, cx + 2, cy - 12, 20, 24, 130, 100); }
	}
	private static PointF Proj(float x, float y, float z) { float k = FL / (z - CamZ); return new PointF(200f + k * (x - CamX), HorY + k * (CamY - y)); }
	private static float Scl(float z) { return FL / (z - CamZ); }
	private void NextPitch() { idx++; if (idx >= Pitches) { Over = true; return; } phase = 0; wait = 0.8 + Rnd.NextDouble() * 0.5; res = ""; flKind = -1; swinging = false; hit = false; bfOn = false; fl.Clear(); trail.Clear(); hint = ""; timeMsg = ""; }
	private void Throw()
	{
		phase = 1; pt = 0; swinging = false; hit = false; swT = 0; trail.Clear();
		float lvl = Math.Min(1f, idx / 9f);
		// 3구까지는 직구만, 4구부터 변화구: 구종마다 속도와 궤적이 달라요 (직구 120 · 투심 115 · 슬라이더 105 · 포크 105 · 체인지업 100 · 커브 90 km/h 가 기본값)
		if (idx < 3) ptype = 0; else { double r = Rnd.NextDouble(); ptype = r < 0.22 ? 0 : (r < 0.40 ? 2 : (r < 0.56 ? 5 : (r < 0.72 ? 3 : (r < 0.86 ? 4 : 1)))); }
		float[] baseK = { 120f, 90f, 105f, 100f, 105f, 115f, 115f };
		float diff = 0.90f + 0.18f * lvl + (float)(Rnd.NextDouble() - 0.5) * 0.03f;   // 난이도(진행)에 따라 구속이 올라가요
		kmh = (int)Math.Round(baseK[ptype] * diff); pv = kmh / 3.6f;
		pname = PNames[ptype]; nameT = 1.6f;
		ptPlate = PZ0 / pv; px0 = (float)(Rnd.NextDouble() - 0.5) * 0.3f;
		xp = (float)(Rnd.NextDouble() - 0.5) * 0.36f; yp = 0.37f + (float)Rnd.NextDouble() * 0.38f;
		// 직선 경로에서 벗어나는 정도 (가로 hx · 세로 vy, 클수록 늦게 꺾여요)
		hxA = 0f; hxK = 2f; vyA = 0f; vyK = 2f; rise = 0f;
		// 구종별 꺾임: 처음엔 '가상의 직선'으로 오다가 늦게 꺾여서 플레이트에서 제 위치에 들어와요 (A=꺾이는 크기, K=클수록 늦게 꺾임)
		switch (ptype)
		{
			case 1: vyA = -0.95f; vyK = 3.0f; rise = 0.13f; break;                      // 커브: 느리게, 위로 살짝 솟았다가 크게 아래로
			case 2: hxA = -0.60f; hxK = 3.0f; vyA = -0.05f; vyK = 2.4f; break;          // 슬라이더: 왼쪽으로 횡 이동 (투수 글러브 쪽)
			case 3: hxA = 0.14f; hxK = 2.2f; vyA = -0.22f; vyK = 2.2f; break;           // 체인지업: 느려지며 오른쪽 아래로 살짝 (팔 쪽)
			case 4: vyA = -0.78f; vyK = 4.6f; break;                                    // 포크볼: 직구처럼 오다 막판에 아래로 뚝
			case 5: hxA = 0.45f; hxK = 2.8f; vyA = -0.06f; vyK = 2.2f; break;           // 투심: 오른쪽으로 횡 이동 (팔 쪽)
		}
		PitchPos(0);
	}
	private float hxA, hxK = 2f, vyA, vyK = 2f, rise;
	private void PitchPos(float t)
	{
		float tau = Math.Min(1.4f, t / ptPlate); float tk = (float)Math.Pow(tau, hxK), tv = (float)Math.Pow(tau, vyK);
		float rs = tau < 1f ? rise * (float)Math.Sin(Math.PI * tau) : 0f, drs = tau < 1f ? rise * (float)Math.PI * (float)Math.Cos(Math.PI * tau) : 0f;
		bx = px0 + (xp - hxA - px0) * tau + hxA * tk; by = PY0 + (yp - vyA - PY0) * tau + vyA * tv + rs; bz = PZ0 - pv * t;
		bvx = ((xp - hxA - px0) + hxA * hxK * (float)Math.Pow(tau, hxK - 1f)) / ptPlate; bvy = ((yp - vyA - PY0) + vyA * vyK * (float)Math.Pow(tau, vyK - 1f) + drs) / ptPlate; bvz = -pv;
	}
	// 휘두를 때마다 '원래 누른 시각'이 늦은 만큼 보정을 조금씩 맞춰요 (빠르면 보정을 줄여요). 터무니없이 빗나간 건 빼요
	private void Calibrate(float dtm)
	{
		lastDtm = dtm; float raw = dtm + calib; if (Math.Abs(dtm) > 0.25f) return;
		calib += ((raw > 0f ? raw : 0f) - calib) * 0.35f; calib = Clamp(calib, 0f, 0.12f);
	}
	public override void Click(float x, float y) { if (phase == 1 && !swinging && !hit) { swinging = true; swT = calib; swingAt = pt - calib; Sfx.Play("swing"); batYh = HandFor(mouseSeen ? mouseH : yp + (float)(Rnd.NextDouble() - 0.5) * 0.10f); } }
	public override void Press(Keys k) { if (k == Keys.Space || k == Keys.Enter) Click(0, 0); }
	private void Phi(float t, out float phi, out float omega)
	{
		float u = Clamp(t / SwT, 0, 1); float e = u * u * (3 - 2 * u);
		phi = Phi0 + (Phi1 - Phi0) * e; omega = (Phi1 - Phi0) * (float)Math.PI / 180f * 6f * u * (1 - u) / SwT;
	}
	// 타자(마룡이)는 방망이를 잡은 손 바로 아래에 서서 손 위치를 따라 좌우로 움직여요 (홈플레이트와 겹치지 않는 범위 안에서)
	private static float BatterX() { return Clamp(HandX + hxo - 0.35f, -1.18f, -0.95f); }   // 마룡이는 존을 가리지 않게 왼쪽 가장자리에서 살짝만 움직여요
	private static float hxo;   // 손(방망이) 가로 위치 보정: 마우스를 좌우로 움직여 방망이의 어느 자리에 공이 맞을지 조절해요
	private static float[] BatPt(float phiDeg, float s, float yh) { double a = phiDeg * Math.PI / 180.0; float hs = (phiDeg - PhiM) / 100f; return new float[] { HandX + hxo + 0.12f * hs + s * (float)Math.Cos(a), yh - s * cTilt, HandZ + 0.40f * hs + s * (float)Math.Sin(a) }; }   // 손이 몸쪽에서 투수 쪽으로 같이 나가며 휘둘러요 (맞는 순간 위치는 그대로)
	private bool Collide()
	{
		SetPlane(batYh);
		float phi, om; Phi(Math.Min(swT, SwT), out phi, out om);
		float ya = batYh + cAtk * phi; float[] A = BatPt(phi, 0, ya), B = BatPt(phi, BatL, ya);   // 실제 타자처럼 방망이가 살짝 올려 치는 궤적(공의 내려오는 길과 겹치는 시간이 길어 약간 빨라도/늦어도 맞아요)
		float abx = B[0] - A[0], aby = B[1] - A[1], abz = B[2] - A[2], apx = bx - A[0], apy = by - A[1], apz = bz - A[2];
		float tt = Clamp((apx * abx + apy * aby + apz * abz) / (abx * abx + aby * aby + abz * abz), 0, 1);
		float cx = A[0] + abx * tt, cy = A[1] + aby * tt, cz = A[2] + abz * tt, dx = bx - cx, dy = by - cy, dz = bz - cz, d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
		float rb = (0.028f + 0.022f * tt) * 3.4f;   // 초보자도 잘 맞도록 맞는 범위를 넉넉하게
		if (d >= rb + BallR) return false;
		double a = phi * Math.PI / 180.0; float nx, ny, nz;
		if (d > 1e-4f) { nx = dx / d; ny = dy / d; nz = dz / d; } else { nx = -(float)Math.Sin(a); ny = 0; nz = (float)Math.Cos(a); }
		float vbx = om * tt * BatL * -(float)Math.Sin(a), vbz = om * tt * BatL * (float)Math.Cos(a);   // 방망이 그 지점의 속도 (수평 회전)
		float vrel = (bvx - vbx) * nx + bvy * ny + (bvz - vbz) * nz;
		if (vrel >= 0) return false;                                                                   // 이미 멀어지는 중
		/* 스윗스팟에서 멀수록 힘이 줄어요: 홈런은 정확해야 하고 빗맞아도 안타·뜬공은 돼요 */ float qual = 1f - Math.Min(1f, Math.Abs(tt - 0.78f) / 0.45f) * 0.28f; float e = 0.55f * qual; float ovx = bvx - (1 + e) * vrel * nx, ovy = bvy - (1 + e) * vrel * ny, ovz = bvz - (1 + e) * vrel * nz;
		float sp = (float)Math.Sqrt(ovx * ovx + ovy * ovy + ovz * ovz); if (sp > 56f) { float k = 56f / sp; ovx *= k; ovy *= k; ovz *= k; }
		StartFlight(ovx, ovy, ovz, tt);
		if (flKind == 0 || flKind == 1) { PointF ph0 = Proj(A[0], A[1], A[2]); bfOn = true; bfX = ph0.X; bfY = ph0.Y; bfT = 0f; bfVx = -60f - (float)Rnd.NextDouble() * 40f; bfVy = -300f - (flKind == 0 ? 80f : 0f); bfRot = 30f; bfRv = -720f - (flKind == 0 ? 240f : 0f); Sfx.Play("swing"); }   // 빠던: 안타·홈런이면 방망이를 던져요
		return true;
	}
	private void StartFlight(float vx, float vy, float vz, float u)
	{
		hit = true; phase = 2; wait = 1.9; hitFlash = 1f;
		fl.Clear(); float x = bx, y = by, z = bz, t = 0, dt = 1f / 240f; bool landed = false; int bounces = 0; float land = 0, lx = 0, lz = 0; apex = y;
		loft = (float)(Math.Atan2(vy, Math.Sqrt(vx * vx + vz * vz)) * 180 / Math.PI); azim = (float)(Math.Atan2(vx, vz) * 180 / Math.PI); vsp = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
		fl.Add(new float[] { 0, x, y, z });
		for (int i = 0; i < 240 * 9; i++)
		{
			float v = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz), k = 0.0032f;
			vx -= k * v * vx * dt; vy -= (9.8f + k * v * vy) * dt; vz -= k * v * vz * dt; x += vx * dt; y += vy * dt; z += vz * dt; t += dt; if (y > apex) apex = y;
			if (y <= 0) { if (!landed) { landed = true; lx = x; lz = z; land = (float)Math.Sqrt(x * x + z * z); } y = 0; if (bounces < 3) { vy = -vy * 0.36f; vx *= 0.72f; vz *= 0.72f; bounces++; } else { vx *= 0.9f; vz *= 0.9f; vy = 0; } if (Math.Abs(vy) < 0.4f && vx * vx + vz * vz < 0.5f) { fl.Add(new float[] { t, x, 0, z }); break; } }
			if (i % 4 == 0) fl.Add(new float[] { t, x, y, z });
		}
		if (!landed) { lx = x; lz = z; land = (float)Math.Sqrt(x * x + z * z); }
		flDur = t; landD = land; float landAz = (float)(Math.Atan2(lx, lz) * 180 / Math.PI); flT = 0; flKind = 0;
		if (vz <= 0.5f || Math.Abs(landAz) > 56f) { res = loft > 50 ? "파울 플라이" : "파울"; resScore = 5; flKind = 3; hint = azim < 0 ? "너무 일찍 휘둘렀어요 (왼쪽 파울)" : "너무 늦게 휘둘렀어요 (오른쪽 파울)"; }
		else if (loft > 52f) { res = "높이 뜬 내야 플라이 (아웃)"; resScore = 0; flKind = 2; hint = "공 아래를 쳤어요"; }
		else if (loft < 8f) { if (vsp > 30f) { res = "강습 안타!"; resScore = 40; flKind = 1; } else { res = "땅볼 아웃 (반송)"; resScore = 0; flKind = 2; hint = "공 위를 쳤어요"; } }
		else if (landD >= 100f) { res = "홈런!! 민원 완결!"; resScore = 100; flKind = 0; fwT = 0f; fwSeed = Rnd.Next(1000); }
		else if (landD >= 75f) { res = "큰 타구! 장타"; resScore = 70; flKind = 1; }
		else if (landD >= 45f) { res = "안타! 처리 완료"; resScore = 45; flKind = 1; }
		else if (landD >= 25f) { res = "뜬공 아웃"; resScore = 0; flKind = 2; }
		else { res = "빗맞았어요"; resScore = 0; flKind = 2; }
		if (flKind != 3 && hint.Length == 0) hint = azim < -12 ? "당겨 쳤어요 (왼쪽)" : (azim > 12 ? "밀어 쳤어요 (오른쪽)" : "가운데로!");
		{ float dtm = swingAt - (ptPlate - 0.5f * SwT - 0.015f); Calibrate(dtm); float ad = Math.Abs(dtm); if (ad < 0.022f) { timeMsg = "퍼펙트 타이밍!" + (resScore > 0 ? " +10" : ""); if (resScore > 0) Score += 10; Fx.Burst(W / 2f, 250f, Color.FromArgb(255, 226, 120), 8); } else { string ms = " (" + (dtm < 0 ? "-" : "+") + (int)Math.Round(ad * 1000f) + "ms)"; if (ad < 0.055f) timeMsg = (dtm < 0 ? "타이밍 약간 빠름" : "타이밍 약간 늦음") + ms; else timeMsg = (dtm < 0 ? "너무 빨라요" : "너무 늦어요") + ms; } } if (resScore >= 40) ringDone = true; Score += resScore; resT = 2.2f; Shake = resScore >= 100 ? 0.9f : (resScore > 0 ? 0.45f : 0.15f); { PointF hq = Proj(fl[0][1], fl[0][2], fl[0][3]); Fx.Burst(hq.X, hq.Y, resScore >= 100 ? Color.FromArgb(255, 214, 80) : (resScore > 0 ? Color.FromArgb(255, 244, 200) : Color.FromArgb(200, 210, 230)), resScore >= 100 ? 16 : (resScore > 0 ? 9 : 3)); if (resScore >= 100) Fx.Burst(W / 2f, 210f, Color.FromArgb(255, 120, 90), 18); } Sfx.Play(resScore >= 100 ? "homerun" : (resScore > 0 ? "thud" : "thud_soft"));
	}
	public override void Update(float dt, GameInput inp, double tm)
	{
		if (fwT >= 0f) { fwT += dt; if (fwT > 4.2f) fwT = -1f; }
		if (resT > 0) resT -= dt; if (hitFlash > 0) hitFlash -= dt * 3f; if (nameT > 0) nameT -= dt;
		if (inp.MouseMoved) mouseSeen = true;
		if (!swinging && phase <= 1) { float sz = HandZ + Sw * BatL * (float)Math.Sin(PhiM * Math.PI / 180.0), tgt = xp - HandX - Sw * BatL * (float)Math.Cos(PhiM * Math.PI / 180.0); if (phase == 0) tgt = 0.1f - Sw * BatL - HandX; if (mouseSeen) { float wx = CamX + (inp.Mx - 200f) / Scl(sz); tgt = wx - HandX - Sw * BatL * (float)Math.Cos(PhiM * Math.PI / 180.0); } hxo += (Clamp(tgt, -1.2f, 1.2f) - hxo) * Math.Min(1f, dt * 26f); float wy = CamY - (inp.My - HorY) / Scl(sz); if (mouseSeen) mouseH += (Clamp(wy, 0.12f, 1.05f) - mouseH) * Math.Min(1f, dt * 26f); else mouseH = yp; }   // 커서가 가리키는 자리가 곧 방망이 중심이에요 (도움 없이 마우스 그대로)
		if (phase == 0) { wait -= dt; if (wait <= 0) Throw(); }
		else if (phase == 1)
		{
			int n = Math.Max(1, (int)Math.Ceiling(dt / (1f / 960f))); float sd = dt / n;
			for (int i = 0; i < n && !hit; i++)
			{
				pt += sd; PitchPos(pt); if (swinging) swT += sd;
				if (swinging && swT <= SwT + 0.06f && bz < 1.6f && bz > -0.9f && Collide()) break;
			}
			if (!hit)
			{
				if (trail.Count == 0 || pt - trail[trail.Count - 1][3] > 0.012f) { trail.Add(new float[] { bx, by, bz, pt }); if (trail.Count > 10) trail.RemoveAt(0); }
				if (bz < -1.3f)
				{
					if (swinging) { float ideal = ptPlate - 0.5f * SwT - 0.015f; Calibrate(swingAt - ideal); hint = swingAt < ideal ? "조금 빨랐어요" : "조금 늦었어요"; res = "헛스윙! 반려!"; } else { hint = ""; res = "스트라이크! 결재 보류"; }
					resScore = 0; resT = 1.5f; phase = 2; wait = 1.0; flKind = -1; Sfx.Play(swinging ? "bad" : "tick");
				}
			}
		}
		else
		{
			if (flKind >= 0) flT += dt * 2.2f; if (swinging) swT += dt; if (bfOn) { bfT += dt; bfVy += 900f * dt; bfX += bfVx * dt; bfY += bfVy * dt; bfRot += bfRv * dt; float gy = Proj(BatterX(), 0, 0.05f).Y + 2f; if (bfY > gy && bfVy > 0f) { bfY = gy; bfVy = -bfVy * 0.28f; bfVx *= 0.5f; bfRv *= 0.5f; if (Math.Abs(bfVy) < 60f) { bfVy = 0f; bfRv = 0f; bfRot = 84f; } } }
			wait -= dt; if (wait <= 0) NextPitch();
		}
	}
	// 방망이 그림(스프라이트)을 손잡이 A → 끝 B 방향으로 늘여 그려요
	private static bool SprBat(Graphics g, PointF a, PointF b, float wb)
	{
		float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 6f) return false; Bitmap bm = Spr.Get("ms_bat" + (DateTime.Now.DayOfYear % 3)) ?? Spr.Get("bat"); if (bm == null) return false;
		float w = Math.Max(len * 0.17f, wb * 1.1f); GraphicsState st = g.Save(); g.TranslateTransform(a.X, a.Y); g.RotateTransform((float)(Math.Atan2(dy, dx) * 180 / Math.PI) + 90f);
		g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(bm, new RectangleF(-w / 2, -len, w, len), new RectangleF(0, 0, bm.Width, bm.Height), GraphicsUnit.Pixel); g.Restore(st); return true;
	}
	private static void BallDraw(Graphics g, PointF c, float r, float rot)
	{
		if (r > 3f && Spr.Draw(g, "ms_n_bball", c.X, c.Y, r * 2f, r * 2f, rot)) return;
		using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2);
		if (r > 4f) { GraphicsState st = g.Save(); g.TranslateTransform(c.X, c.Y); g.RotateTransform(rot); using (Pen p = new Pen(Color.FromArgb(214, 60, 70), Math.Max(1f, r * 0.12f))) { g.DrawArc(p, -r * 1.25f, -r * 0.8f, r, r * 1.6f, -50, 100); g.DrawArc(p, r * 0.25f, -r * 0.8f, r, r * 1.6f, 130, 100); } g.Restore(st); }
		using (Pen o = new Pen(Color.FromArgb(70, 0, 0, 0), 1f)) g.DrawEllipse(o, c.X - r, c.Y - r, r * 2, r * 2);
	}
	// 타자 그림 속 손 위치(그림 비율)와 방망이 각도: 방망이를 항상 마룡이 손에 맞춰 그려요
	private static readonly Dictionary<string, float[]> HandTab = new Dictionary<string, float[]> {
		{ "ms_bt4", new[] { 0.915f, 0.49f, 70f } }, { "ms_bt5", new[] { 0.90f, 0.50f, 38f } }, { "ms_bt6", new[] { 0.74f, 0.34f, -12f } }, { "ms_bt7", new[] { 0.42f, 0.78f, 14f } },
		{ "ms_br0", new[] { 0.78f, 0.56f, 72f } }, { "ms_br1", new[] { 0.60f, 0.51f, -8f } }, { "ms_br4", new[] { 0.46f, 0.62f, 12f } } };
	private bool BatHand(string frame, out PointF hand, out float len, out float ang)
	{
		hand = PointF.Empty; len = 0f; ang = 0f; float[] h; Bitmap b = Spr.Get(frame); if (b == null || !HandTab.TryGetValue(frame, out h)) return false;
		PointF bp = Proj(BatterX(), 0, 0.05f); float w = Spr.OW(frame, b) * 0.62f, hh = Spr.OH(frame, b) * 0.62f;
		hand = new PointF(bp.X - w / 2f + h[0] * w, bp.Y - hh + h[1] * hh); len = hh * 0.62f; ang = h[2]; return true;
	}
	private void DrawHeldBat(Graphics g, string frame)
	{
		PointF hp; float hl, ang; if (!BatHand(frame, out hp, out hl, out ang)) return; PointF tp = new PointF(hp.X + (float)Math.Sin(ang * Math.PI / 180.0) * hl, hp.Y - (float)Math.Cos(ang * Math.PI / 180.0) * hl);
		if (!SprBat(g, hp, tp, 11f)) using (Pen p = new Pen(Color.FromArgb(222, 172, 110), 8f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, hp, tp);
		using (SolidBrush gb = new SolidBrush(Color.FromArgb(235, 70, 72, 92))) g.FillEllipse(gb, hp.X - 4, hp.Y - 4, 8, 8);
	}
	private void DrawFlyBat(Graphics g)
	{
		Bitmap bm = Spr.Get("ms_bat" + (DateTime.Now.DayOfYear % 3)) ?? Spr.Get("bat"); float len = 108f, w = 18f;
		GraphicsState st = g.Save(); g.TranslateTransform(bfX, bfY); g.RotateTransform(bfRot);
		if (bm != null) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(bm, new RectangleF(-w / 2, -len / 2, w, len), new RectangleF(0, 0, bm.Width, bm.Height), GraphicsUnit.Pixel); }
		else using (Pen p = new Pen(Color.FromArgb(222, 172, 110), 9f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, 0, -len / 2, 0, len / 2);
		g.Restore(st);
	}
	private void DrawBat(Graphics g, double tm)
	{
		if (bfOn) { DrawFlyBat(g); return; }
		float phi, om = 0f; if (swinging) Phi(Math.Min(swT, SwT), out phi, out om); else phi = Phi0 + (float)Math.Sin(tm * 3) * 3f;
		if (swinging) SetPlane(batYh); float yh = swinging ? batYh + cAtk * phi : 0.87f;
		float[] A = BatPt(phi, 0.0f, yh), M = BatPt(phi, BatL * 0.3f, yh), B = BatPt(phi, BatL, yh);
		{
			// 대기 자세: 방망이를 뒤쪽 어깨 위로 세워 올리고 있다가, 휘두르기 시작하면 수평 스윙으로 이어져요
			float bw0 = BatterX(); float[] sA = { bw0 - 0.07f, 0.40f, 0.05f }, sB = { sA[0] - 0.20f, sA[1] + 0.95f, sA[2] - 0.10f }; float bl = 0f; if (swinging) bl = Clamp(swT / 0.07f, 0f, 1f); bl = bl * bl * (3 - 2 * bl);
			if (swinging && phi > 40f) {   // 휘두른 뒤엔 방망이를 어깨 위로 감아 올려요 (끝이 투수 쪽을 향해 작아 보이지 않게)
					float fe = Clamp((phi - 40f) / 55f, 0f, 1f); fe = fe * fe * (3 - 2 * fe); float[] fB = { A[0] - 0.55f, A[1] + 0.70f, A[2] - 0.28f }, fM = { A[0] + (fB[0] - A[0]) * 0.3f, A[1] + (fB[1] - A[1]) * 0.3f, A[2] + (fB[2] - A[2]) * 0.3f };
					for (int q = 0; q < 3; q++) { B[q] = B[q] + (fB[q] - B[q]) * fe; M[q] = M[q] + (fM[q] - M[q]) * fe; }
				}
				if (bl < 1f) { float[] sM = { sA[0] + (sB[0] - sA[0]) * 0.3f, sA[1] + (sB[1] - sA[1]) * 0.3f, sA[2] + (sB[2] - sA[2]) * 0.3f }; for (int q = 0; q < 3; q++) { A[q] = sA[q] + (A[q] - sA[q]) * bl; M[q] = sM[q] + (M[q] - sM[q]) * bl; B[q] = sB[q] + (B[q] - sB[q]) * bl; } }
		}
		PointF pa2 = Proj(A[0], A[1], A[2]), pm = Proj(M[0], M[1], M[2]), pb2 = Proj(B[0], B[1], B[2]); float ka = Scl(A[2]), kb = Scl(B[2]);
		if (swinging) { float su0 = Clamp(swT / SwT, 0f, 1.2f); if (su0 > 0.36f) { int bf0 = su0 < 0.42f ? 3 : (su0 < 0.56f ? 4 : (su0 < 0.7f ? 5 : (su0 < 0.88f ? 6 : 7))); float f0 = Clamp((su0 - 0.36f) / 0.12f, 0f, 1f); PointF hp; float hl, ang; if (BatHand("ms_bt" + (bf0 < 4 ? 4 : bf0), out hp, out hl, out ang)) { PointF tp = new PointF(hp.X + (float)Math.Sin(ang * Math.PI / 180.0) * hl, hp.Y - (float)Math.Cos(ang * Math.PI / 180.0) * hl); pa2 = new PointF(pa2.X + (hp.X - pa2.X) * f0, pa2.Y + (hp.Y - pa2.Y) * f0); pb2 = new PointF(pb2.X + (tp.X - pb2.X) * f0, pb2.Y + (tp.Y - pb2.Y) * f0); } } }
		float dx = pb2.X - pa2.X, dy = pb2.Y - pa2.Y, len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 0.5f) return; float nx = -dy / len, ny = dx / len;
		float wa = 0.040f * ka, wb = 0.085f * kb;
		PointF[] quad = { new PointF(pa2.X + nx * wa / 2, pa2.Y + ny * wa / 2), new PointF(pb2.X + nx * wb / 2, pb2.Y + ny * wb / 2), new PointF(pb2.X - nx * wb / 2, pb2.Y - ny * wb / 2), new PointF(pa2.X - nx * wa / 2, pa2.Y - ny * wa / 2) };
		bool bsp = SprBat(g, pa2, pb2, wb); if (!bsp) using (SolidBrush br = new SolidBrush(Color.FromArgb(222, 172, 110))) g.FillPolygon(br, quad);
		if (!bsp) using (SolidBrush br = new SolidBrush(Color.FromArgb(120, 70, 34))) g.FillPolygon(br, new PointF[] { quad[0], new PointF(pm.X + nx * (wa + (wb - wa) * 0.3f) / 2, pm.Y + ny * (wa + (wb - wa) * 0.3f) / 2), new PointF(pm.X - nx * (wa + (wb - wa) * 0.3f) / 2, pm.Y - ny * (wa + (wb - wa) * 0.3f) / 2), quad[3] });
		if (!bsp) using (Pen o = new Pen(Color.FromArgb(70, 60, 30, 10), 1f)) g.DrawPolygon(o, quad);
		{ float gr = Math.Max(3f, 0.055f * ka); using (SolidBrush gb = new SolidBrush(Color.FromArgb(235, 70, 72, 92))) g.FillEllipse(gb, pa2.X - gr, pa2.Y - gr, gr * 2, gr * 2); using (SolidBrush gb = new SolidBrush(Color.FromArgb(235, 70, 72, 92))) g.FillEllipse(gb, pa2.X + (pm.X - pa2.X) * 0.14f - gr * 0.85f, pa2.Y + (pm.Y - pa2.Y) * 0.14f - gr * 0.85f, gr * 1.7f, gr * 1.7f); }
		if (swinging && om > 6f)
		{
			// 스윙 궤적: 방망이 끝과 중간이 지나간 자리를 부채꼴 잔상으로 그려요
			float span = Math.Min(88f, 22f + om * 3.4f); int steps = 9; List<PointF> outer = new List<PointF>(), inner = new List<PointF>();
			for (int i = 0; i <= steps; i++) { float ph3 = phi - span * (1f - i / (float)steps); float yy3 = batYh + cAtk * ph3; float[] To = BatPt(ph3, BatL, yy3), Ti = BatPt(ph3, BatL * 0.42f, yy3); outer.Add(Proj(To[0], To[1], To[2])); inner.Add(Proj(Ti[0], Ti[1], Ti[2])); }
			List<PointF> poly = new List<PointF>(outer); for (int i = inner.Count - 1; i >= 0; i--) poly.Add(inner[i]);
			using (SolidBrush sb = new SolidBrush(Color.FromArgb(Math.Min(150, 30 + (int)(om * 6f)), 255, 248, 214))) g.FillPolygon(sb, poly.ToArray());
			using (Pen sp = new Pen(Color.FromArgb(Math.Min(235, 60 + (int)(om * 9f)), 255, 255, 255), Math.Max(2.2f, wb * 0.26f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(sp, outer.ToArray());
		}
	}
	// 가상 방망이: '지금 휘두르면' 공을 맞히는 자리(스윙 중간)를 방망이 모양 점선으로 보여줘요. 공이 이 선 근처로 올 때 휘두르면 돼요
	private void DrawGhostBat(Graphics g, double tm)
	{
		float yh = HandFor(mouseSeen ? mouseH : 0.55f), phi = PhiM; float yg = yh + cAtk * phi; float[] A = BatPt(phi, 0f, yg), B = BatPt(phi, BatL, yg);
		PointF pa2 = Proj(A[0], A[1], A[2]), pb2 = Proj(B[0], B[1], B[2]); float ka = Scl(A[2]), kb = Scl(B[2]);
		float dx = pb2.X - pa2.X, dy = pb2.Y - pa2.Y, len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 2f) return; float nx = -dy / len, ny = dx / len, wa = 0.045f * ka, wb = 0.095f * kb;
		{   // 스윙 궤적: 실제 스윙과 똑같은 길(손이 앞으로 나가며 방망이 끝이 위로 훑는 곡선)을 맞는 지점으로 갈수록 진하게
			float p0 = PhiM - 58f, p1 = PhiM + 42f; int N = 22; PointF[] to = new PointF[N + 1], ti = new PointF[N + 1];
			for (int fi = 0; fi <= N; fi++) { float ph4 = p0 + (p1 - p0) * fi / N; float yy = yh + cAtk * ph4; float[] a1 = BatPt(ph4, BatL, yy), a2 = BatPt(ph4, BatL * 0.45f, yy); to[fi] = Proj(a1[0], a1[1], a1[2]); ti[fi] = Proj(a2[0], a2[1], a2[2]); }
			for (int fi = 0; fi < N; fi++) { float mid = p0 + (p1 - p0) * (fi + 0.5f) / N; float w = Math.Max(0f, 1f - Math.Abs(mid - PhiM) / 62f); int al = (int)(14 + 70 * w);
				using (SolidBrush fb = new SolidBrush(Color.FromArgb(al, 255, 240, 170))) g.FillPolygon(fb, new PointF[] { ti[fi], to[fi], to[fi + 1], ti[fi + 1] });
				using (Pen fpn = new Pen(Color.FromArgb(Math.Min(235, al * 2 + 20), 255, 244, 190), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(fpn, to[fi], to[fi + 1]); }
		}
		float near = phase == 1 ? Clamp(1f - Math.Abs(bz - 3.4f) / 4f, 0f, 1f) : 0f;
		{   // 맞는 범위: 커서(방망이 중심) 둘레의 원 안으로 공이 지나가면 맞아요
			float[] CS = BatPt(phi, Sw * BatL, yg); PointF cs = Proj(CS[0], CS[1], CS[2]); float cr = (0.028f + 0.022f * Sw) * 3.4f * Scl(CS[2]) + BallR * Scl(CS[2]);
			using (SolidBrush rb2 = new SolidBrush(Color.FromArgb((int)(26 + 40 * near), 255, 120, 90))) g.FillEllipse(rb2, cs.X - cr, cs.Y - cr, cr * 2, cr * 2);
			using (Pen rp2 = new Pen(Color.FromArgb((int)(130 + 110 * near), 255, 150, 110), 2f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(rp2, cs.X - cr, cs.Y - cr, cr * 2, cr * 2);
		}   // 휘두를 때가 가까울수록 진해져요
		PointF[] q = { new PointF(pa2.X + nx * wa / 2, pa2.Y + ny * wa / 2), new PointF(pb2.X + nx * wb / 2, pb2.Y + ny * wb / 2), new PointF(pb2.X - nx * wb / 2, pb2.Y - ny * wb / 2), new PointF(pa2.X - nx * wa / 2, pa2.Y - ny * wa / 2) };
		using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(34 + 60 * near), 255, 236, 150))) g.FillPolygon(b, q);
		{ float[] S0 = BatPt(phi, BatL * 0.62f, yg), S1 = BatPt(phi, BatL * 0.9f, yg); PointF s0 = Proj(S0[0], S0[1], S0[2]), s1 = Proj(S1[0], S1[1], S1[2]); using (Pen sw = new Pen(Color.FromArgb((int)(90 + 130 * near), 255, 120, 90), Math.Max(4f, wb * 0.9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(sw, s0, s1); }   // 빨간 띠 = 방망이 중심(스윗스팟)
		using (Pen p = new Pen(Color.FromArgb((int)(170 + 85 * near), 255, 236, 150), 2.4f) { DashStyle = DashStyle.Dash }) g.DrawPolygon(p, q);
			}
	public override void Draw(Graphics g, double tm)
	{
		// 하늘 · 관중석 · 잔디
		{ Bitmap bp0 = Spr.Get("bg_base"); if (bp0 != null) BgImg(g, "bg_base", Color.FromArgb(52, 96, 180), Color.FromArgb(52, 96, 180)); else Bg(g, Color.FromArgb(52, 96, 180), Color.FromArgb(176, 208, 240)); }
		{
			// 전광판: 점수 · 투구 수 · 구종과 구속
			RectangleF sbr = new RectangleF(112f, 42f, 176f, 58f);
			using (Pen pp = new Pen(Color.FromArgb(150, 60, 64, 84), 4f)) { g.DrawLine(pp, 136f, sbr.Bottom, 136f, HorY - 28); g.DrawLine(pp, 264f, sbr.Bottom, 264f, HorY - 28); }
			using (GraphicsPath gp = Gfx.Round(sbr, 6)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 20, 26, 40))) g.FillPath(b, gp); using (Pen pp = new Pen(Color.FromArgb(220, 120, 130, 160), 2f)) g.DrawPath(pp, gp); }
			Txt(g, "마룡 VS 익산", 8f, 2, Color.FromArgb(255, 150, 190, 255), sbr.X, sbr.Y + 3, sbr.Width, 13, StringAlignment.Center);
			Txt(g, "SCORE " + Score, 15f, 2, Color.FromArgb(255, 255, 214, 90), sbr.X, sbr.Y + 15, sbr.Width, 24, StringAlignment.Center);
			Txt(g, Math.Min(idx + 1, Pitches) + "/" + Pitches + "구  " + (phase >= 1 && kmh > 0 ? pname + " " + kmh + "km/h" : "투구 준비"), 8.5f, 2, Color.FromArgb(255, 120, 235, 160), sbr.X, sbr.Y + 41, sbr.Width, 15, StringAlignment.Center);
		}
		if (fwT >= 0f)
		{
			Color[] fc = { Color.FromArgb(255, 120, 130), Color.FromArgb(255, 220, 90), Color.FromArgb(130, 220, 255), Color.FromArgb(170, 255, 170), Color.FromArgb(230, 150, 255), Color.FromArgb(255, 170, 90) };
			for (int fb = 0; fb < 8; fb++)
			{
				Random fr = new Random(fwSeed * 31 + fb); float t0 = 0.2f + fb * 0.4f, u = fwT - t0, bx0 = 40 + (float)fr.NextDouble() * 320f, by0 = 40 + (float)fr.NextDouble() * 150f; Color c0 = fc[(fb + fwSeed) % fc.Length];
				if (u > -0.45f && u < 0f) { float k = (u + 0.45f) / 0.45f, ry = (HorY - 30f) + (by0 - (HorY - 30f)) * k; using (Pen rp = new Pen(Color.FromArgb(200, 255, 240, 200), 2f)) g.DrawLine(rp, bx0, ry, bx0, ry + 18f); }
				else if (u >= 0f && u < 1.5f)
				{
					float fade = 1f - u / 1.5f; int n = 30;
					using (SolidBrush glow = new SolidBrush(Color.FromArgb((int)(60 * Math.Max(0f, 1f - u * 3f)), c0))) g.FillEllipse(glow, bx0 - 50, by0 - 50, 100, 100);
					for (int k = 0; k < n; k++) { double an = k * Math.PI * 2 / n; float sp = 70f + (k % 3) * 18f, ex = bx0 + (float)Math.Cos(an) * sp * u, ey = by0 + (float)Math.Sin(an) * sp * u + 38f * u * u, tx = bx0 + (float)Math.Cos(an) * sp * (u - 0.08f), ty = by0 + (float)Math.Sin(an) * sp * (u - 0.08f) + 38f * (u - 0.08f) * (u - 0.08f);
						using (Pen sp2 = new Pen(Color.FromArgb((int)(230 * fade), c0), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(sp2, tx, ty, ex, ey); }
				}
			}
		}
		if (resT > 0 && resScore >= 45) for (int cf = 0; cf < 16; cf++) { double ph2 = tm * 9 + cf * 1.9; if (Math.Sin(ph2) > 0.75) { using (SolidBrush fb = new SolidBrush(Color.FromArgb(230, 255, 255, 235))) g.FillEllipse(fb, 10 + (cf * 71) % 380, HorY - 24 + (cf * 13) % 22, 4, 4); } }
		PointF mound = Proj(0, 0, PZ0);
		// 스트라이크 존
			{ PointF z1 = Proj(-0.22f, 0.85f, 0), z2 = Proj(0.22f, 0.25f, 0); float zw = z2.X - z1.X, zh = z2.Y - z1.Y; using (SolidBrush zb = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) g.FillRectangle(zb, z1.X, z1.Y, zw, zh); using (Pen gp2 = new Pen(Color.FromArgb(70, 255, 255, 255), 1f)) { for (int q = 1; q < 3; q++) { g.DrawLine(gp2, z1.X + zw * q / 3f, z1.Y, z1.X + zw * q / 3f, z2.Y); g.DrawLine(gp2, z1.X, z1.Y + zh * q / 3f, z2.X, z1.Y + zh * q / 3f); } } using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), 2f)) g.DrawRectangle(p, z1.X, z1.Y, zw, zh); }
		// 투수
		{ int pf = 0; if (phase == 0) { double wl = wait; pf = wl > 0.7 ? 0 : 1 + (int)((0.7 - wl) / 0.7 * 4.0); if (pf > 4) pf = 4; } else if (phase == 1) pf = pt < 0.06f ? 5 : (pt < 0.22f ? 6 : 7); else pf = 7;
		  if (Spr.Get("ms_pc0") == null || !Spr.Frame(g, "ms_pc" + pf, mound.X, mound.Y + 4, 0.3f, 0f, false)) DrawSpr(g, "n_bp", mound.X, mound.Y + 2, 74f, (float)Math.Sin(tm * 3) * 2); }
		// 타자: 방망이가 몸 뒤에 있을 때는 타자 뒤에, 휘두르는 동안(몸 앞)은 타자 앞에 그려요
		PointF bp = Proj(BatterX(), 0, 0.05f); float lean = 0; if (swinging) { float ph, om0; Phi(Math.Min(swT, SwT), out ph, out om0); lean = Clamp((ph + 20f) / 115f, -1f, 1f) * 5f; }
		bool batFront = !swinging || swT > SwT * 0.28f;
		int rfr = -1;
		if (!swinging && resT > 0f && res.Length > 0 && Spr.Get("ms_br0") != null) rfr = res.Contains("헛스윙") ? 0 : (res.Contains("파울") ? 1 : (res.Contains("스트라이크") ? 4 : (flKind == 0 ? 6 : (flKind == 1 ? (resScore >= 70 ? 6 : 2) : 7))));
		if (Over && !swinging && Spr.Get("ms_br5") != null) rfr = 5;
		if (!batFront && rfr < 0) DrawBat(g, tm);
		{ float su = swinging ? Clamp(swT / SwT, 0f, 1.2f) : 0f; int bf = !swinging ? 0 : (su < 0.08f ? 1 : (su < 0.26f ? 2 : (su < 0.42f ? 3 : (su < 0.56f ? 4 : (su < 0.7f ? 5 : (su < 0.88f ? 6 : 7)))))); if (hit && !swinging) bf = 7;
		  if (rfr >= 0) Spr.Frame(g, "ms_br" + rfr, bp.X, bp.Y, 0.62f, 0f, false); else if (!Spr.Frame(g, "ms_bt" + bf, bp.X, bp.Y, 0.62f, swinging ? 0f : (float)Math.Sin(tm * 3) * 1.2f, false)) DrawSpr(g, "n_bb2", bp.X, bp.Y, 140f, lean + (float)Math.Sin(tm * 3) * 1.2f); }
		if (batFront && rfr < 0) DrawBat(g, tm); else if (bfOn) DrawFlyBat(g); else if (rfr == 0 || rfr == 1 || rfr == 4) DrawHeldBat(g, "ms_br" + rfr);
		if (swinging && swT < SwT + 0.1f) { float u = Clamp(swT / SwT, 0f, 1f); string fn = u < 0.34f ? "fx_sw1" : (u < 0.67f ? "fx_sw2" : "fx_sw3"); { SetPlane(batYh); GraphicsState fs0 = g.Save(); float fcx = bp.X + 34f, fcy = bp.Y - 90f + (cTilt - 0.17f) * 70f; g.TranslateTransform(fcx, fcy); g.RotateTransform(PlaneDeg()); g.TranslateTransform(-fcx, -fcy); DrawFx(g, fn, fcx, fcy, 110f, Clamp(1.2f - Math.Abs(u - 0.5f) * 1.4f, 0f, 1f)); g.Restore(fs0); } }
			if (phase <= 1 && !swinging && !hit) DrawGhostBat(g, tm);
		// 공
		if (phase == 1 && !hit)
		{
			// 거리감 장치: 공이 '이 문'을 지나는 순간 클릭하면 돼요 (공 속도에 맞춰 자리가 달라져요)
			{
				float zi = pv * (0.5f * SwT + 0.015f), dz = bz - zi, near2 = Clamp(1f - Math.Abs(dz) / 6f, 0f, 1f), tw = Math.Abs(dz) / pv;
				PointF g1 = Proj(-0.6f, 0f, zi), g2 = Proj(0.6f, 0f, zi), g3 = Proj(0.6f, 1.5f, zi), g4 = Proj(-0.6f, 1.5f, zi);
				if (dz > -0.6f)
				{
					using (SolidBrush gb = new SolidBrush(Color.FromArgb((int)(18 + 46 * near2), 255, 236, 150))) g.FillPolygon(gb, new PointF[] { g1, g2, g3, g4 });
					using (Pen gp3 = new Pen(Color.FromArgb((int)(90 + 150 * near2), 255, 236, 150), 1.6f)) { g.DrawLine(gp3, g1, g2); g.DrawLine(gp3, g1, g4); g.DrawLine(gp3, g2, g3); g.DrawLine(gp3, g4, g3); }
					Txt(g, "여기서 클릭!", 8.5f, 1, Color.FromArgb((int)(120 + 120 * near2), 255, 240, 170), g4.X - 40, g4.Y - 14, 80, 14, StringAlignment.Center);
				}
				PointF b0 = Proj(bx, 0f, bz), b1 = Proj(bx, by, bz);
				using (Pen dp = new Pen(Color.FromArgb(60, 255, 255, 255), 1f) { DashStyle = DashStyle.Dot }) g.DrawLine(dp, b0, b1);   // 공 아래 선 (높이)
				float br = Math.Max(2.2f, 0.12f * Scl(bz));
				if (dz > 0f && tw < 0.6f) { float u6 = tw / 0.6f, pr6 = br * 1.5f + 46f * u6; using (Pen rp6 = new Pen(Color.FromArgb((int)(120 + 130 * (1f - u6)), 120, 255, 150), 2f + 1.5f * (1f - u6))) g.DrawEllipse(rp6, b1.X - pr6, b1.Y - pr6, pr6 * 2, pr6 * 2); using (SolidBrush cb6 = new SolidBrush(Color.FromArgb((int)(60 * (1f - u6)), 120, 255, 150))) g.FillEllipse(cb6, b1.X - br * 1.5f, b1.Y - br * 1.5f, br * 3f, br * 3f); }   // 줄어드는 링: 링이 공에 딱 닫히는 순간 클릭!
				if (false) { float pr3 = br; using (Pen rp = new Pen(Color.FromArgb(235, 120, 255, 150), 2.2f)) g.DrawEllipse(rp, b1.X - pr3, b1.Y - pr3, pr3 * 2, pr3 * 2); if (tw < 0.04f) Txt(g, "지금!", 12f, 2, Color.FromArgb(255, 150, 255, 170), b1.X - 30, b1.Y - pr3 - 18, 60, 16, StringAlignment.Center); }
			}
			for (int i = 0; i < trail.Count; i++) { float[] q = trail[i]; PointF qp = Proj(q[0], q[1], q[2]); float rr = Math.Max(1.6f, 0.07f * Scl(q[2])); using (SolidBrush b = new SolidBrush(Color.FromArgb(10 + i * 8, 255, 255, 255))) g.FillEllipse(b, qp.X - rr, qp.Y - rr, rr * 2, rr * 2); }
			PointF sh = Proj(bx, 0, bz); using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(b, sh.X - 5 - 0.05f * Scl(bz), sh.Y - 1.5f - 0.012f * Scl(bz), 10 + 0.1f * Scl(bz), 3 + 0.024f * Scl(bz));
			PointF c = Proj(bx, by, bz); BallDraw(g, c, Math.Max(2.2f, 0.12f * Scl(bz)), pt * 700f);
				if (pt > ptPlate * 0.38f) { PointF tp = Proj(xp, yp, 0); float pr2 = 5f + 2f * (float)Math.Sin(pt * 30f); using (Pen p = new Pen(Color.FromArgb(190, 255, 214, 90), 1.6f)) { g.DrawEllipse(p, tp.X - pr2, tp.Y - pr2, pr2 * 2, pr2 * 2); g.DrawLine(p, tp.X - pr2 - 3, tp.Y, tp.X + pr2 + 3, tp.Y); g.DrawLine(p, tp.X, tp.Y - pr2 - 3, tp.X, tp.Y + pr2 + 3); } }   // 공이 지나갈 자리(스트라이크 존 안)
		}
		if (phase == 2 && flKind >= 0 && fl.Count > 1)
		{
			// 날아가는 공 (3차원 궤적을 시점에 투영)
			int n = fl.Count - 1; float tt = Math.Min(flT, flDur); int k = 0; while (k < n && fl[k + 1][0] < tt) k++;
			float[] a = fl[k], b2 = fl[Math.Min(n, k + 1)]; float f = b2[0] > a[0] ? (tt - a[0]) / (b2[0] - a[0]) : 0f; float X = a[1] + (b2[1] - a[1]) * f, Y = a[2] + (b2[2] - a[2]) * f, Z = a[3] + (b2[3] - a[3]) * f;
			using (Pen p = new Pen(Color.FromArgb(70, 255, 255, 255), 1.6f)) { PointF last = Proj(fl[0][1], fl[0][2], Math.Max(0.1f, fl[0][3])); for (int i = 1; i <= k; i += 2) { PointF cur = Proj(fl[i][1], fl[i][2], Math.Max(0.1f, fl[i][3])); g.DrawLine(p, last, cur); last = cur; } }
			PointF gs = Proj(X, 0, Math.Max(0.1f, Z)); using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, gs.X - 5, gs.Y - 1.5f, 10, 3);
			PointF cp = Proj(X, Y, Math.Max(0.1f, Z)); BallDraw(g, cp, Math.Max(2f, 0.14f * Scl(Math.Max(0.1f, Z))), flT * 900f);
			if (hitFlash > 0) { PointF hp2 = Proj(fl[0][1], fl[0][2], fl[0][3]); DrawFx(g, "fx_hit", hp2.X, hp2.Y, 40f + 110f * (1.3f - hitFlash), hitFlash); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(180 * hitFlash), 255, 244, 190))) g.FillEllipse(b, hp2.X - 22 * (1.3f - hitFlash), hp2.Y - 22 * (1.3f - hitFlash), 44 * (1.3f - hitFlash), 44 * (1.3f - hitFlash)); }
			if (hitFlash > 0.15f) { PointF hq3 = Proj(fl[0][1], fl[0][2], fl[0][3]); float u3 = 1.3f - hitFlash; using (Pen sp3 = new Pen(Color.FromArgb((int)(220 * Math.Min(1f, hitFlash)), 255, 240, 170), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) for (int ra = 0; ra < 10; ra++) { double an3 = ra * Math.PI / 5 + 0.3; float r1 = 14f + 30f * u3, r2 = r1 + (ra % 2 == 0 ? 20f : 11f); g.DrawLine(sp3, hq3.X + (float)Math.Cos(an3) * r1, hq3.Y + (float)Math.Sin(an3) * r1, hq3.X + (float)Math.Cos(an3) * r2, hq3.Y + (float)Math.Sin(an3) * r2); }
				string ono = resScore >= 100 ? "쾅!!" : (resScore > 0 ? "딱!" : "툭"); Txt(g, ono, resScore >= 100 ? 26f : 20f, 2, Color.FromArgb((int)(255 * Math.Min(1f, hitFlash * 1.4f)), resScore > 0 ? Color.FromArgb(255, 238, 120) : Color.FromArgb(220, 220, 230)), hq3.X - 60, hq3.Y - 62 - 20f * u3, 120, 40, StringAlignment.Center); }
			// 타구 방향 미니맵 (위에서 본 그림)
			float mx0 = 296, my0 = 28, mw = 98, mh = 92; using (GraphicsPath gp = Gfx.Round(new RectangleF(mx0, my0, mw, mh), 10)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 20, 40, 30))) g.FillPath(b, gp);
			float hx = mx0 + mw / 2f, hy = my0 + mh - 9f, sc = 0.42f;
			using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), 1.2f)) { g.DrawLine(p, hx, hy, hx - 70 * sc * 1.0f, hy - 70 * sc); g.DrawLine(p, hx, hy, hx + 70 * sc, hy - 70 * sc); g.DrawArc(p, hx - 100 * sc, hy - 100 * sc, 200 * sc, 200 * sc, 225, 90); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 244, 190))) g.FillEllipse(b, hx - 2, hy - 2, 4, 4);
			using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1.4f)) { float lx = hx, ly = hy; for (int i = 1; i <= k; i += 2) { float px2 = hx + fl[i][1] * sc, py2 = hy - fl[i][3] * sc; g.DrawLine(p, lx, ly, px2, py2); lx = px2; ly = py2; } }
			using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, hx + X * sc - 2.5f, hy - Z * sc - 2.5f, 5, 5);
			Txt(g, "왼쪽", 7.5f, 1, Color.FromArgb(160, 255, 255, 255), mx0 + 2, my0 + 2, 40, 12, StringAlignment.Near); Txt(g, "오른쪽", 7.5f, 1, Color.FromArgb(160, 255, 255, 255), mx0 + mw - 42, my0 + 2, 40, 12, StringAlignment.Far);
			if (tt >= flDur * 0.98f && landD > 5f) Txt(g, "비거리 " + (int)landD + "m", 13f, 2, Color.White, 0, 210, W, 24, StringAlignment.Center);
		}
		if (resT > 0 && res.Length > 0)
		{
			int al = (int)(255 * Math.Min(1f, resT)); Color c = resScore >= 100 ? Color.FromArgb(al, 255, 214, 90) : (resScore > 0 ? Color.FromArgb(al, 255, 255, 255) : Color.FromArgb(al, 255, 150, 160));
			Txt(g, res, 22f, 2, c, 0, 150, W, 44, StringAlignment.Center); if (resScore > 0) Txt(g, "+" + resScore, 15f, 2, Color.FromArgb(al, 255, 226, 120), 0, 192, W, 26, StringAlignment.Center);
			{ float ta = Math.Min(1f, resT), tp = 0.8f + 0.2f * Math.Min(1f, (1.5f - resT) * 5f); if (resScore >= 100 && Spr.Get("ms_pr_trophy") != null) Spr.Draw(g, "ms_pr_trophy", 52f, 130f, 56f * tp, 69f * tp, (float)Math.Sin(Environment.TickCount / 120.0) * 6f); else if (resScore >= 40 && resScore < 100 && Spr.Get("ms_pr_helmet") != null) Spr.Draw(g, "ms_pr_helmet", 52f, 134f, 54f * tp, 49f * tp, 6f); else if (resScore == 0 && res.Contains("아웃") && Spr.Get("ms_pr_glove") != null) Spr.Draw(g, "ms_pr_glove", 52f, 134f, 58f * tp, 59f * tp, 10f); }
			if (lastDtm < 1f) { float ta2 = Math.Min(1f, resT); RectangleF tb2 = new RectangleF(80, 284, 240, 10); using (GraphicsPath gp = Gfx.Round(tb2, 5)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * ta2), 0, 0, 0))) g.FillPath(b, gp); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(150 * ta2), 120, 255, 150))) g.FillRectangle(b, tb2.X + tb2.Width / 2f - 10f, tb2.Y, 20f, tb2.Height); using (Pen p = new Pen(Color.FromArgb((int)(200 * ta2), 255, 255, 255), 1.4f)) g.DrawLine(p, tb2.X + tb2.Width / 2f, tb2.Y - 3, tb2.X + tb2.Width / 2f, tb2.Bottom + 3); float mk = tb2.X + tb2.Width / 2f + Clamp(lastDtm / 0.12f, -1f, 1f) * tb2.Width / 2f; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * ta2), 255, 214, 90))) g.FillEllipse(b, mk - 6, tb2.Y - 2, 12, 14); Txt(g, "빠름", 9f, 2, Color.FromArgb((int)(200 * ta2), 200, 220, 255), tb2.X - 44, tb2.Y - 5, 40, 20, StringAlignment.Far); Txt(g, "늦음", 9f, 2, Color.FromArgb((int)(200 * ta2), 255, 190, 190), tb2.Right + 4, tb2.Y - 5, 40, 20, StringAlignment.Near); }
			if (timeMsg.Length > 0) Txt(g, timeMsg, 12f, 2, Color.FromArgb((int)(255 * Math.Min(1f, resT)), timeMsg.StartsWith("퍼펙트") ? Color.FromArgb(255, 226, 120) : Color.FromArgb(200, 220, 255)), 0, 258, W, 22, StringAlignment.Center);
				if (hint.Length > 0) Txt(g, hint, 11f, 1, Color.FromArgb((int)(220 * Math.Min(1f, resT)), 255, 255, 255), 0, 232, W, 22, StringAlignment.Center);
		}
		if (phase == 1 && nameT > 0 && !hit) Txt(g, pname, 13f, 2, Color.FromArgb((int)(255 * Math.Min(1f, nameT)), 255, 236, 150), 0, 104, W, 24, StringAlignment.Center);
		if (phase == 0 && idx == 0 && wait > 0.2) Hint(g, "초록 링이 줄어들어 공에 딱 닫힐 때 클릭!", 150 + 13.0, 13f, false, 1f);
	}
}
// ── 4. 수박 합치기 ──────────────────────────────────────────
internal sealed class SuikaGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class Fr { public float X, Y, Vx, Vy, R; public int Lv; public float Age; public bool Dead, Merged; }
	private class Rg { public float X, Y, Age, R; public Color C; }
	private readonly List<Rg> rgs = new List<Rg>();
	private readonly List<Fr> fs = new List<Fr>();
	private static readonly float[] Rad = { 15, 21, 28, 35, 43, 52, 62, 73, 85, 98 };
	private static readonly int[] Pts = { 1, 3, 6, 10, 15, 21, 28, 36, 45, 55 };
	private static readonly Color[] Col = {
		Color.FromArgb(120, 100, 200), Color.FromArgb(236, 84, 96), Color.FromArgb(160, 90, 200), Color.FromArgb(255, 160, 50), Color.FromArgb(220, 70, 70),
		Color.FromArgb(214, 200, 90), Color.FromArgb(255, 150, 160), Color.FromArgb(244, 190, 40), Color.FromArgb(150, 210, 110), Color.FromArgb(50, 160, 80) };
	private static readonly string[] Nm = { "블루베리", "딸기", "포도", "귤", "사과", "배", "복숭아", "파인애플", "멜론", "수박" };
	private const float L = 36, Rr = W - 36, Floor = H - 50, Danger = 120;
	private KeyValuePair<string, Image>[] faces;
	private float dropX = W / 2f, cool, chainT; private int chain, cur, next; private float overT; private bool mouseMode;
	public override string Name { get { return "수박 합치기"; } }
	public override string Key { get { return "suika"; } }
	public override string Desc { get { return "같은 마룡이를 합쳐 큰 마룡이까지"; } }
	public override string Help { get { return "마우스(또는 ← →)로 위치를 잡고\n클릭(스페이스)으로 떨어뜨려요. 같은 옷의 마룡이끼리 합쳐져요\n합치기를 연달아 이어가면 연쇄 보너스!"; } }
	public override string RightText { get { return "다음 " + (faces != null ? faces[next].Key : Nm[next]); } }
	public override void Reset() { if (faces == null) faces = Program.GameFaces(10); fs.Clear(); rgs.Clear(); Score = 0; Over = false; cool = 0.3f; overT = 0; cur = Rnd.Next(4); next = Rnd.Next(4); dropX = W / 2f; }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Col[9])) g.FillEllipse(b, cx - 18, cy - 18, 36, 36);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 90, 100))) g.FillEllipse(b, cx - 12, cy - 12, 24, 24);
	}
	private void Drop() { if (cool > 0 || Over) return; fs.Add(new Fr { X = dropX, Y = 70, R = Rad[cur], Lv = cur }); cur = next; next = Rnd.Next(Score > 500 ? 5 : 4); cool = 0.55f; Sfx.Play("thud_soft"); }
	public override void Click(float x, float y) { dropX = Clamp(x, L + Rad[cur], Rr - Rad[cur]); Drop(); }
	public override void Press(Keys k) { if (k == Keys.Space || k == Keys.Down) Drop(); }
	public override void Update(float dt, GameInput inp, double t)
	{
		if (inp.L || inp.R) { mouseMode = false; dropX += ((inp.R ? 1 : 0) - (inp.L ? 1 : 0)) * 320f * dt; }
		else if (inp.MouseMoved) { mouseMode = true; }
		if (mouseMode && !(inp.L || inp.R)) dropX += (inp.Mx - dropX) * Math.Min(1f, dt * 18f);
		dropX = Clamp(dropX, L + Rad[cur], Rr - Rad[cur]);
		if (cool > 0) cool -= dt; if (chainT > 0) { chainT -= dt; if (chainT <= 0) chain = 0; }
		int sub = 6; float h = Math.Min(dt, 0.033f) / sub;
		for (int s = 0; s < sub; s++) Step(h);
		for (int ri = rgs.Count - 1; ri >= 0; ri--) { rgs[ri].Age += dt; if (rgs[ri].Age > 0.45f) rgs.RemoveAt(ri); }
		bool danger = false;
		foreach (Fr f in fs) { if (f.Age > 1.4f && f.Y - f.R < Danger) danger = true; }
		overT = danger ? overT + dt : 0; if (overT > 1.6f) Over = true;
	}
	private void Step(float h)
	{
		foreach (Fr f in fs)
		{
			f.Vy += 1500f * h; f.X += f.Vx * h; f.Y += f.Vy * h; f.Vx *= 0.9995f;
			if (f.X - f.R < L) { f.X = L + f.R; f.Vx = Math.Abs(f.Vx) * 0.2f; }
			if (f.X + f.R > Rr) { f.X = Rr - f.R; f.Vx = -Math.Abs(f.Vx) * 0.2f; }
			if (f.Y + f.R > Floor) { f.Y = Floor - f.R; f.Vy = -Math.Abs(f.Vy) * 0.12f; f.Vx *= 0.98f; }
		}
		for (int it = 0; it < 4; it++)
			for (int i = 0; i < fs.Count; i++)
				for (int j = i + 1; j < fs.Count; j++)
				{
					Fr a = fs[i], b = fs[j]; if (a.Dead || b.Dead) continue;
					float dx = b.X - a.X, dy = b.Y - a.Y, rs = a.R + b.R, d2 = dx * dx + dy * dy;
					if (d2 >= rs * rs) continue;
					float d = (float)Math.Sqrt(d2); if (d < 0.001f) { d = 0.001f; dx = 0.001f; }
					if (a.Lv == b.Lv && a.Age > 0.05f && b.Age > 0.05f)
					{
						a.Dead = b.Dead = true; if (a.Lv >= 4) Shake = Math.Max(Shake, 0.05f * a.Lv); chain = chainT > 0 ? chain + 1 : 1; chainT = 0.9f; Score += Pts[a.Lv] + (chain >= 2 ? chain * 2 : 0); Sfx.Play(chain >= 3 || a.Lv >= 5 ? "ok2" : (a.Lv >= 3 ? "ok" : "pluck")); Fx.Burst((a.X + b.X) / 2, (a.Y + b.Y) / 2, Col[a.Lv], 4 + a.Lv); rgs.Add(new Rg { X = (a.X + b.X) / 2, Y = (a.Y + b.Y) / 2, R = Rad[Math.Min(9, a.Lv + 1)], C = Col[a.Lv] }); if (a.Lv >= 6 || chain >= 3) Shake = Math.Max(Shake, 0.3f);
						if (a.Lv < 9) fs.Add(new Fr { X = (a.X + b.X) / 2, Y = (a.Y + b.Y) / 2, R = Rad[a.Lv + 1], Lv = a.Lv + 1, Age = 0.1f, Vy = -40, Merged = true });
						else Score += 100;
						continue;
					}
					float nx = dx / d, ny = dy / d, over = rs - d;
					float ma = a.R * a.R, mb = b.R * b.R, tot = ma + mb;
					a.X -= nx * over * (mb / tot); a.Y -= ny * over * (mb / tot); b.X += nx * over * (ma / tot); b.Y += ny * over * (ma / tot);
					float rvx = b.Vx - a.Vx, rvy = b.Vy - a.Vy, vn = rvx * nx + rvy * ny;
					if (vn < 0)
					{
						float jimp = -(1 + 0.08f) * vn / (1 / ma + 1 / mb);
						a.Vx -= jimp * nx / ma; a.Vy -= jimp * ny / ma; b.Vx += jimp * nx / mb; b.Vy += jimp * ny / mb;
						float tx = -ny, ty = nx, vt = rvx * tx + rvy * ty; float fr = vt * 0.04f;
						a.Vx += fr * tx; a.Vy += fr * ty; b.Vx -= fr * tx; b.Vy -= fr * ty;
					}
				}
		fs.RemoveAll(f => f.Dead);
		foreach (Fr f in fs) f.Age += h;
	}
	private void Fruit(Graphics g, float x, float y, float r, int lv, double tm)
	{
		Image im = faces != null ? faces[lv].Value : null;
		if (im != null)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(235, Theme.Mix(Col[lv], Color.White, 0.72)))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
			using (Pen o = new Pen(Color.FromArgb(220, Col[lv]), Math.Max(2f, r * 0.07f))) g.DrawEllipse(o, x - r + 1, y - r + 1, r * 2 - 2, r * 2 - 2);
			float hh = r * 1.78f, ww = hh * im.Width / im.Height; if (ww > r * 1.8f) { ww = r * 1.8f; hh = ww * im.Height / im.Width; }
			g.DrawImage(im, x - ww / 2, y - hh / 2, ww, hh);
			return;
		}
		using (GraphicsPath p = new GraphicsPath()) { p.AddEllipse(x - r, y - r, r * 2, r * 2);
			using (PathGradientBrush pb = new PathGradientBrush(p)) { pb.CenterPoint = new PointF(x - r * 0.3f, y - r * 0.35f); pb.CenterColor = Theme.Mix(Col[lv], Color.White, 0.45); pb.SurroundColors = new Color[] { Theme.Mix(Col[lv], Color.Black, 0.12) }; g.FillPath(pb, p); } }
		if (lv == 9)
			using (Pen sp = new Pen(Color.FromArgb(120, 20, 90, 40), r * 0.12f)) for (int k = -2; k <= 2; k++) g.DrawArc(sp, x - r * 0.62f + k * r * 0.28f - r * 0.1f, y - r * 0.95f, r * 0.7f, r * 1.9f, 60, 60);
		if (lv == 7) using (SolidBrush lb = new SolidBrush(Color.FromArgb(60, 40, 120, 40))) g.FillEllipse(lb, x - r * 0.3f, y - r * 1.12f, r * 0.6f, r * 0.34f);
		float ey = y - r * 0.05f, es = Math.Max(2f, r * 0.10f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 40, 36, 50))) { g.FillEllipse(b, x - r * 0.36f - es, ey - es, es * 2, es * 2.4f); g.FillEllipse(b, x + r * 0.36f - es, ey - es, es * 2, es * 2.4f); }
		using (Pen mp = new Pen(Color.FromArgb(200, 40, 36, 50), Math.Max(1.2f, r * 0.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(mp, x - r * 0.16f, ey + r * 0.12f, r * 0.32f, r * 0.24f, 20, 140);
		using (Pen o = new Pen(Color.FromArgb(60, 0, 0, 0), 1.2f)) g.DrawEllipse(o, x - r, y - r, r * 2, r * 2);
	}
	public override void Draw(Graphics g, double tm)
	{
		Bg(g, Color.FromArgb(255, 238, 205), Color.FromArgb(255, 214, 160));
		using (GraphicsPath gp = Gfx.Round(new RectangleF(L - 6, Danger - 20, Rr - L + 12, Floor - Danger + 26), 14)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillPath(b, gp); using (Pen p = new Pen(Color.FromArgb(200, 170, 120, 70), 4f)) g.DrawPath(p, gp); }
		using (GraphicsPath inn = Gfx.Round(new RectangleF(L - 3, Danger - 17, Rr - L + 6, Floor - Danger + 20), 12)) using (LinearGradientBrush ib = new LinearGradientBrush(new RectangleF(0, Danger - 17, W, Floor - Danger + 20), Color.FromArgb(40, 255, 255, 255), Color.FromArgb(54, 255, 190, 120), 90f)) g.FillPath(ib, inn);
		using (SolidBrush shn = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillRectangle(shn, L + 6, Danger - 6, 7, Floor - Danger - 30);
		bool warn = overT > 0; int al = warn ? (int)(120 + 120 * Math.Abs(Math.Sin(tm * 10))) : 70;
		using (Pen p = new Pen(Color.FromArgb(al, 230, 70, 80), 2f) { DashStyle = DashStyle.Dash }) g.DrawLine(p, L, Danger, Rr, Danger);
		if (warn) using (LinearGradientBrush wb = new LinearGradientBrush(new RectangleF(L, Danger, Rr - L, 50), Color.FromArgb((int)(60 * Math.Abs(Math.Sin(tm * 10))), 230, 70, 80), Color.FromArgb(0, 230, 70, 80), 90f)) g.FillRectangle(wb, L, Danger, Rr - L, 50);
		foreach (Fr f in fs) { using (SolidBrush shb = new SolidBrush(Color.FromArgb(34, 120, 70, 30))) g.FillEllipse(shb, f.X - f.R * 0.9f, f.Y + f.R * 0.78f, f.R * 1.8f, f.R * 0.4f); }
		foreach (Fr f in fs) { float pr = f.R; if (f.Merged && f.Age < 0.5f) { float u = Math.Max(0f, (f.Age - 0.1f) / 0.4f); pr = f.R * (1f + 0.2f * (float)Math.Sin(Math.Min(1f, u) * Math.PI) * (1f - 0.4f * u)); } Fruit(g, f.X, f.Y, pr, f.Lv, tm); }
		foreach (Rg rg in rgs) { float u = rg.Age / 0.45f, rr = rg.R * (0.8f + 0.9f * u); using (Pen rp = new Pen(Color.FromArgb((int)(210 * (1 - u)), rg.C), 4f * (1 - u) + 0.8f)) g.DrawEllipse(rp, rg.X - rr, rg.Y - rr, rr * 2, rr * 2); using (SolidBrush rb = new SolidBrush(Color.FromArgb((int)(60 * (1 - u)), 255, 255, 255))) g.FillEllipse(rb, rg.X - rr * 0.7f, rg.Y - rr * 0.7f, rr * 1.4f, rr * 1.4f); }
		if (!Over)
		{
			using (Pen p = new Pen(Color.FromArgb(70, 120, 90, 60), 2f) { DashStyle = DashStyle.Dot }) g.DrawLine(p, dropX, 70 + Rad[cur], dropX, Floor);
			if (cool <= 0.2f) Fruit(g, dropX, 70, Rad[cur], cur, tm);
		}
		if (chain >= 2 && chainT > 0) Txt(g, chain + "연쇄! +" + chain * 2, 18f, 2, Color.FromArgb((int)(255 * Math.Min(1f, chainT * 2)), 232, 110, 40), 0, 96, W, 32, StringAlignment.Center); Txt(g, "★ 마지막엔 원래 마룡이가 나와요", 10f, 0, Color.FromArgb(150, 110, 80, 50), 0, H - 26, W, 20, StringAlignment.Center);
	}
}

// ── 5. 석탑 쌓기 (균형 물리) ─────────────────────────────
// 크레인이 돌을 옮기다가 눌러서 떨어뜨리면 아래 돌 위에 쌓여요. 위에 쌓인 돌들의 무게중심이 아래 돌의 접촉면을 벗어나면 그 윗부분이 쏠려 무너져요.
internal sealed class TowerGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private class Ly { public float X, W, H; public int Lv; }
	private class Fl { public float X, Y, W, H, Ang, Vx, Vy, Va; public int Lv; }   // 무너지는 돌 (세계 좌표)
	private readonly List<Ly> st = new List<Ly>(); private readonly List<Fl> fl = new List<Fl>();
	private const float GY = H - 64f, CraneY = 52f, HangY = 100f, BaseW = 130f, BaseH = 26f;
	private float cam, camT, ph, craneX, wind, windT, windChg, msgT, glow, margin = 99f, shake, nw, nh, fx, fy, fvx, fvy, wait, dyingT;
	private double time; private bool holding, falling, done9; private int lives, combo, nlv; private string msg = "";
	public override string Costume { get { return "근로자의날"; } }
	public override string Name { get { return "석탑 쌓기"; } }
	public override string Key { get { return "tower2"; } }
	public override string Desc { get { return "균형을 잡아 석탑을 쌓아요"; } }
	public override string Help { get { return "크레인이 돌을 옮겨요. 클릭(스페이스)하면 돌이 떨어져요\n아래 돌 위에 중심을 맞춰 쌓으세요. 한쪽으로 쏠리면 무너져요!\n9층을 쌓으면 석탑 완성 · 6층부터는 바람이 불어요\n기회는 3번 (헛디디거나 무너지면 하나씩 줄어요)"; } }
	public override string RightText { get { return (st.Count - 1) + "층"; } }
	public override void Reset()
	{
		st.Clear(); fl.Clear(); st.Add(new Ly { X = W / 2f - BaseW / 2f, W = BaseW, H = BaseH, Lv = 0 });
		Score = 0; Over = false; cam = camT = 0; ph = 0; lives = 3; combo = 0; wait = 0.6f; holding = false; falling = false; dyingT = 0; wind = windT = 0; windChg = 0; done9 = false; msg = ""; msgT = 0; margin = 99f; shake = 0; time = 0; glow = 0;
	}
	public override void Icon(Graphics g, float cx2, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 186, 176)))
			for (int i = 0; i < 4; i++) g.FillRectangle(b, cx2 - 20 + i * 3, cy + 16 - i * 10, 40 - i * 6, 9);
	}
	private float SumH() { float s = 0; foreach (Ly l in st) s += l.H; return s; }
	private float HeldX() { return Clamp(craneX - nw / 2f, 6f, W - 6f - nw); }
	private void NewStone()
	{
		int f = st.Count - 1;
		if (f >= 4 && Rnd.NextDouble() < 0.2) { nw = 130f + (float)Rnd.NextDouble() * 24f; nh = 18f + (float)Rnd.NextDouble() * 6f; }   // 넓고 얇은 지붕돌
		else { float lo = f < 3 ? 84f : 60f, hi = f < 3 ? 130f : 128f; nw = lo + (float)Rnd.NextDouble() * (hi - lo); nh = 24f + (float)Rnd.NextDouble() * 10f; }
		nlv = f + 1; holding = true;
	}
	private void Drop()
	{
		if (!holding || Over || dyingT > 0 || wait > 0) return;
		fx = HeldX(); fy = HangY; { int f0 = st.Count - 1; float om = Math.Min(3.1f, 1.5f + f0 * 0.07f); float cv = 150f * om * (float)Math.Cos(ph) + (f0 >= 7 ? Math.Min(46f, (f0 - 6) * 12f) * om * 2.3f * (float)Math.Cos(ph * 2.3f + 1f) : 0f); fvx = cv * 0.3f; } fvy = 0; falling = true; holding = false;   // 크레인이 움직이던 속도 그대로 관성이 남아요
	}
	public override void Click(float x, float y) { Drop(); }
	public override void Press(Keys k) { if (k == Keys.Space || k == Keys.Down || k == Keys.Enter) Drop(); }
	private void Land(float topS)
	{
		Ly top = st[st.Count - 1];
		float cTop = top.X + top.W / 2f; bool perfect = Math.Abs(fx + nw / 2f - cTop) < 8f;
		st.Add(new Ly { X = perfect ? cTop - nw / 2f : fx, W = nw, H = nh, Lv = nlv });
		falling = false; wait = 0.5f; Sfx.Play("thud");
		int f = st.Count - 1; Shake = Math.Max(Shake, 0.08f + Math.Min(0.25f, f * 0.02f));   // 높이 쌓을수록 쿵 하는 울림이 커져요
		{ float ly0 = fy + nh + cam; Fx.Burst(fx + 4, ly0, Color.FromArgb(190, 170, 140), 4); Fx.Burst(fx + nw - 4, ly0, Color.FromArgb(190, 170, 140), 4); }
		if (perfect) { combo++; Sfx.Play("ok2"); Fx.Burst(MiniGame.W / 2f, 260f, Color.FromArgb(255, 226, 120), 10); glow = 1f; msg = combo >= 2 ? combo + "연속 딱!" : "딱!"; msgT = 1f; } else combo = 0;
		Score += 10 + f + (perfect ? 5 + Math.Min(combo, 6) * 3 : 0);
		if (f == 9 && !done9) { done9 = true; Score += 100; msg = "준공 완료! +100"; msgT = 2.4f; }
		CheckBalance();
	}
	// 각 층마다: 그 층 위에 쌓인 돌 전체의 무게중심이 아래 돌과 맞닿은 면 안에 있어야 서 있어요
	private void CheckBalance()
	{
		int n = st.Count; float[] M = new float[n + 1], Mx = new float[n + 1];
		for (int i = n - 1; i >= 0; i--) { float m = st[i].W * st[i].H; M[i] = M[i + 1] + m; Mx[i] = Mx[i + 1] + m * (st[i].X + st[i].W / 2f); }
		int bad = -1; int dir = 1; margin = 99f;
		for (int k = 1; k < n; k++)
		{
			float com = Mx[k] / M[k], sl = Math.Max(st[k].X, st[k - 1].X), sr = Math.Min(st[k].X + st[k].W, st[k - 1].X + st[k - 1].W);
			float mg = Math.Min(com - sl, sr - com);
			if (mg < 0 && bad < 0) { bad = k; dir = com < sl ? -1 : 1; }
			if (mg < margin) margin = mg;
		}
		if (bad > 0) Collapse(bad, dir);
	}
	private void Collapse(int k, int dir)
	{
		float bottom = GY; for (int i = 0; i < k; i++) bottom -= st[i].H;
		for (int i = k; i < st.Count; i++)
		{
			Ly l = st[i]; bottom -= l.H;
			fl.Add(new Fl { X = l.X, Y = bottom, W = l.W, H = l.H, Lv = l.Lv, Vx = dir * (60f + (i - k) * 16f + (float)Rnd.NextDouble() * 24f), Vy = -80f - (float)Rnd.NextDouble() * 70f, Va = dir * (1.1f + (float)Rnd.NextDouble() * 1.3f) });
		}
		st.RemoveRange(k, st.Count - k);
		lives--; combo = 0; shake = 0.5f; Sfx.Play("bad_big"); margin = 99f; msg = "붕괴! 감사 나오겠어요"; msgT = 1.5f; wait = 1.1f;
		if (lives <= 0) dyingT = 1.6f;
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		time += dt;
		int f = st.Count - 1;
		float omega = Math.Min(3.1f, 1.5f + f * 0.07f); ph += omega * dt; craneX = W / 2f + 150f * (float)Math.Sin(ph) + (f >= 7 ? Math.Min(46f, (f - 6) * 12f) * (float)Math.Sin(ph * 2.3f + 1f) : 0f);   // 7층부터 크레인이 불규칙하게 흔들려요
		if (f >= 6) { windChg -= dt; if (windChg <= 0) { windChg = 3f + (float)Rnd.NextDouble() * 3f; float[] o = { -46f, -28f, 0f, 28f, 46f }; windT = o[Rnd.Next(o.Length)]; } wind += (windT - wind) * Math.Min(1f, dt * 1.5f); }
		else { wind = 0; windT = 0; }
		camT = Math.Max(0f, SumH() - 200f); cam += (camT - cam) * Math.Min(1f, dt * 5f);
		if (glow > 0) glow -= dt * 2.2f; if (msgT > 0) msgT -= dt; if (shake > 0) shake -= dt;
		for (int i = fl.Count - 1; i >= 0; i--) { Fl d = fl[i]; d.Vy += 1500f * dt; d.X += d.Vx * dt; d.Y += d.Vy * dt; d.Ang += d.Va * dt; if (d.Y + cam > H + 120) fl.RemoveAt(i); }
		if (dyingT > 0) { dyingT -= dt; if (dyingT <= 0) Over = true; return; }
		if (wait > 0) wait -= dt;
		if (!holding && !falling && wait <= 0) NewStone();
		if (falling)
		{
			fvy += 1500f * dt; fvx += (wind - fvx) * Math.Min(1f, dt * 1.2f);
			float prevB = fy + nh; fx += fvx * dt; fy += fvy * dt;
			float topS = GY - SumH() + cam; Ly top = st[st.Count - 1];
			if (prevB <= topS + 0.01f && fy + nh >= topS)
			{
				float l = Math.Max(fx, top.X), r = Math.Min(fx + nw, top.X + top.W);
				if (r - l >= 10f) { Land(topS); return; }
			}
			if (fy + nh >= GY + cam) { falling = false; lives--; combo = 0; Sfx.Play("bad2"); msg = "공사 중단!"; msgT = 1.2f; wait = 0.7f; if (lives <= 0) dyingT = 1.0f; }
		}
	}
	private static void Slab(Graphics g, float x, float y, float w, float h, int lvl)
	{
		Color baseC = lvl % 2 == 0 ? Color.FromArgb(196, 190, 178) : Color.FromArgb(178, 172, 160);
		RectangleF body = new RectangleF(x, y + 4, w, Math.Max(2f, h - 4));
		using (SolidBrush b = new SolidBrush(baseC)) g.FillRectangle(b, body);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillRectangle(b, body.X, body.Y, body.Width, 3);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillRectangle(b, body.X, body.Bottom - 4, body.Width, 4);
		RectangleF eave = new RectangleF(x - 4, y, w + 8, 6);   // 처마돌
		using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 208, 196))) g.FillRectangle(b, eave);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillRectangle(b, eave.X, eave.Bottom - 2, eave.Width, 2);
		if (w > 40 && h > 20) using (Pen p = new Pen(Color.FromArgb(60, 60, 55, 45), 1.5f)) { g.DrawLine(p, x + w * 0.33f, y + 8, x + w * 0.33f, y + h - 5); g.DrawLine(p, x + w * 0.66f, y + 8, x + w * 0.66f, y + h - 5); }
	}
	private static void Heart(Graphics g, float cx, float cy, float s, Color c)
	{
		using (SolidBrush b = new SolidBrush(c))
		{
			g.FillEllipse(b, cx - s, cy - s * 0.9f, s, s); g.FillEllipse(b, cx, cy - s * 0.9f, s, s);
			g.FillPolygon(b, new PointF[] { new PointF(cx - s * 0.97f, cy - s * 0.25f), new PointF(cx + s * 0.97f, cy - s * 0.25f), new PointF(cx, cy + s * 1.0f) });
		}
	}
	public override void Draw(Graphics g, double t)
	{
		Bg(g, Color.FromArgb(34, 40, 96), Color.FromArgb(250, 170, 130)); Twinkle(g, t);
		float sh = shake > 0 ? (float)Math.Sin(time * 60) * shake * 8f : 0f;
		GraphicsState gs0 = g.Save(); g.TranslateTransform(sh, 0);
		// 멀리 보이는 전각 실루엣 · 떠오르는 등불 · 바람 줄기
		{ float px0 = 258f, py0 = GY + cam - 6f; using (SolidBrush pb = new SolidBrush(Color.FromArgb(70, 70, 50, 90))) { g.FillRectangle(pb, px0, py0 - 34, 96, 34); g.FillPolygon(pb, new PointF[] { new PointF(px0 - 14, py0 - 34), new PointF(px0 + 110, py0 - 34), new PointF(px0 + 90, py0 - 48), new PointF(px0 + 6, py0 - 48) }); g.FillRectangle(pb, px0 + 18, py0 - 66, 60, 18); g.FillPolygon(pb, new PointF[] { new PointF(px0 + 8, py0 - 66), new PointF(px0 + 88, py0 - 66), new PointF(px0 + 70, py0 - 80), new PointF(px0 + 26, py0 - 80) }); } }
		for (int ln = 0; ln < 6; ln++) { float lx = (ln * 83 + 20) % 380f + (float)Math.Sin(t * 0.8 + ln) * 10f, ly = ((ln * 211 - (float)(t * 14)) % 420f + 420f) % 420f + 60f; using (SolidBrush lb = new SolidBrush(Color.FromArgb(40, 255, 190, 110))) g.FillEllipse(lb, lx - 9, ly - 9, 18, 18); using (SolidBrush lb = new SolidBrush(Color.FromArgb(150, 255, 170, 80))) g.FillEllipse(lb, lx - 3.5f, ly - 4.5f, 7, 9); }
		if (Math.Abs(windT) >= 20f) using (Pen wp = new Pen(Color.FromArgb(40, 255, 255, 255), 1.5f)) for (int wl = 0; wl < 6; wl++) { float wy = 120 + wl * 55 + (float)Math.Sin(t + wl) * 6f, wx = (((float)(t * windT * 4f) + wl * 97) % 460f + 460f) % 460f - 30f; g.DrawLine(wp, wx, wy, wx + Math.Sign(windT) * 32f, wy); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 40, 70))) { g.FillEllipse(b, -80, GY - 10 + cam, 340, 120); g.FillEllipse(b, 160, GY - 4 + cam, 330, 110); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 34, 56))) g.FillRectangle(b, -10, GY + cam, W + 20, H);
		// 탑 (아슬아슬하면 흔들려요)
		float tilt = margin < 16f ? (16f - Math.Max(0f, margin)) / 16f * 3f * (float)Math.Sin(time * 11) : 0f;
		GraphicsState gs = g.Save(); g.TranslateTransform(W / 2f, GY + cam); g.RotateTransform(tilt); g.TranslateTransform(-W / 2f, -(GY + cam));
		float yb = GY;
		for (int i = 0; i < st.Count; i++) { Ly l = st[i]; yb -= l.H; Slab(g, l.X, yb + cam, l.W, l.H, l.Lv); }
		if (glow > 0 && st.Count > 1) { Ly l = st[st.Count - 1]; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * Math.Min(1f, glow)), 255, 240, 160))) g.FillRectangle(b, l.X - 6, yb + cam - 2, l.W + 12, l.H + 2); }
		if (!Over && dyingT <= 0) { Ly l = st[st.Count - 1]; DrawMascot(g, l.X + l.W / 2f, yb + cam + 2, 52, (float)Math.Sin(t * 5) * 4); }
		g.Restore(gs);
		foreach (Fl d in fl)
		{
			GraphicsState s2 = g.Save(); g.TranslateTransform(d.X + d.W / 2f, d.Y + d.H / 2f + cam); g.RotateTransform(d.Ang * 180f / (float)Math.PI);
			Slab(g, -d.W / 2f, -d.H / 2f, d.W, d.H, d.Lv); g.Restore(s2);
		}
		float topS = GY - SumH() + cam;
		if (holding && dyingT <= 0)
		{
			float hx = HeldX();
			using (Pen p = new Pen(Color.FromArgb(70, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dash }) { g.DrawLine(p, hx + nw / 2f, HangY + nh, hx + nw / 2f, topS); g.DrawRectangle(p, hx, topS - nh, nw, nh); }
			using (Pen p = new Pen(Color.FromArgb(150, 80, 70, 90), 1.6f)) { g.DrawLine(p, craneX, CraneY, hx + 8, HangY + 2); g.DrawLine(p, craneX, CraneY, hx + nw - 8, HangY + 2); }
			Slab(g, hx, HangY, nw, nh, nlv);
		}
		using (Pen p = new Pen(Color.FromArgb(120, 70, 60, 90), 3f)) g.DrawLine(p, 0, CraneY - 8, W, CraneY - 8);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 90, 80, 110))) g.FillRectangle(b, craneX - 14, CraneY - 14, 28, 14);
		if (falling) Slab(g, fx, fy, nw, nh, nlv);
		g.Restore(gs0);
		// 바람 · 기회 · 안내
		if (Math.Abs(windT) >= 20f && st.Count - 1 >= 6) Txt(g, windT < 0 ? (windT < -40f ? "◀◀ 바람 세게" : "◀ 바람") : (windT > 40f ? "바람 세게 ▶▶" : "바람 ▶"), 11f, 1, Color.FromArgb(255, 230, 160), 0, 74, W, 20, StringAlignment.Center);
		for (int i = 0; i < 3; i++) { if (Spr.Get("ms_ui_heart") != null) DrawFx(g, "ui_heart", W - 22 - i * 24, 52, 22f, i < lives ? 1f : 0.22f); else Heart(g, W - 22 - i * 24, 52, 8f, i < lives ? Color.FromArgb(235, 90, 110) : Color.FromArgb(70, 255, 255, 255)); }
		if (msgT > 0) Txt(g, msg, msg.StartsWith("무너") || msg.StartsWith("헛") ? 22f : 20f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT)), msg.StartsWith("무너") || msg.StartsWith("헛") ? 255 : 255, msg.StartsWith("무너") || msg.StartsWith("헛") ? 150 : 226, msg.StartsWith("무너") || msg.StartsWith("헛") ? 160 : 120), 0, 230, W, 34, StringAlignment.Center);
		Hint(g, "클릭 · 스페이스: 돌 떨어뜨리기", H - 22, 10f, false, 1f);
	}
}
// ── 오락실 (게임 고르기 · 진행 · 기록) ───────────────────────
internal sealed class MaryongGame : Form
{
	[System.Runtime.InteropServices.DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr h);
	[System.Runtime.InteropServices.DllImport("imm32.dll")] private static extern bool ImmReleaseContext(IntPtr h, IntPtr c);
	[System.Runtime.InteropServices.DllImport("imm32.dll")] private static extern bool ImmNotifyIME(IntPtr c, int act, int idx, int val);
	// 한글은 마지막 글자가 '조합 중'이라, 칸을 비워도 그 글자가 다음 문구에 다시 나타나요 → 조합을 취소하고 비워요
	// 입력칸을 새로 만들어서 한글 조합 중이던 글자가 다시 튀어나오지 않게 해요
	private void RebuildTb()
	{
		try
		{
			TextBox old = tb; TextBox n = new TextBox(); n.BorderStyle = BorderStyle.FixedSingle; n.Font = Fonts.Native(14f);
			n.SetBounds(gx + (int)(20 * S), gy + (int)((g != null ? g.InputY : 516f) * S), (int)(360 * S), (int)(34 * S)); Controls.Add(n); n.Visible = true; tb = n;
			if (old != null) { Controls.Remove(old); old.Dispose(); }
			n.Focus();
		}
		catch (Exception ex) { Program.LogErr(ex); try { ResetIme(); tb.Text = ""; } catch { } }
	}
	private void ResetIme() { try { IntPtr c = ImmGetContext(tb.Handle); if (c != IntPtr.Zero) { ImmNotifyIME(c, 0x15, 4, 0); ImmReleaseContext(tb.Handle, c); } } catch { } }
	private static MaryongGame cur; private static Icon mascotIcon;
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);
	// 마룡이 얼굴을 작은 아이콘(32px)으로 줄여서 창 아이콘으로 써요
	private static Icon MascotIcon()
	{
		if (mascotIcon != null) return mascotIcon;
		Image src = Program.MascotImage(); if (src == null) return null;
		using (Bitmap bm = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
		{
			using (Graphics gg = Graphics.FromImage(bm))
			{
				gg.InterpolationMode = InterpolationMode.HighQualityBicubic; gg.SmoothingMode = SmoothingMode.AntiAlias; gg.PixelOffsetMode = PixelOffsetMode.HighQuality;
				float sc = Math.Min(30f / src.Width, 30f / src.Height); float w = src.Width * sc, h = src.Height * sc; gg.DrawImage(src, (32f - w) / 2f, (32f - h) / 2f, w, h);
			}
			IntPtr hi = bm.GetHicon(); try { mascotIcon = (Icon)Icon.FromHandle(hi).Clone(); } finally { DestroyIcon(hi); }
		}
		return mascotIcon;
	}
	private readonly MiniGame[] games;
	private MiniGame g; private int state;      // 0 목록 · 1 시작 전 · 2 진행 · 3 끝
	private readonly FrameTimer tm = new FrameTimer();
	private readonly Stopwatch sw = Stopwatch.StartNew();
	private readonly GameInput inp = new GameInput();
	private readonly HashSet<Keys> down = new HashSet<Keys>(); private bool mouseHeld; private float dnX, dnY;
	private double last, tt; private float S;
	private float introT, finT, hudBump, stopT, dispScore; private bool paused; private int prevBest; private string thisKey = ""; private readonly List<float[]> floats = new List<float[]>(); private int introNum = -1, lastScore; private static readonly Random Rz = new Random();
	private List<KeyValuePair<string, int>> board = new List<KeyValuePair<string, int>>(); private bool newRecord;
	private readonly Dictionary<string, int> best = new Dictionary<string, int>();

	private static int taps; private static DateTime tapT = DateTime.MinValue; private static bool invited;
	// 마룡이를 몇 번 쓰다듬으면(3번) 말풍선으로 슬쩍 놀자고 해요. 말풍선을 누르면 오락실이 열려요.
	public static void Tap()
	{
		DateTime n = DateTime.Now;
		if ((n - tapT).TotalSeconds > 2.5) taps = 0;
		tapT = n; taps++;
		if (taps >= 5) { taps = 0; Open(); return; }
		string[] say = { null, "앗, 간지러워요!", "하하, 그만~", null, "한 번만 더 누르면...?" };
		if (taps < say.Length && say[taps] != null) Bubble.Say(say[taps], 1.8);
	}
	public static void Invite(string msg) { Bubble.Say(msg, 7, delegate { Open(); }); }
	// 홈 화면을 잠시 열어 두면 한 번만 먼저 말을 걸어요
	public static void InviteOnce() { if (invited) return; invited = true; Invite("잠깐 쉬어 갈까요? 저랑 게임 한 판 해요! (눌러 보세요)"); }
	// 코드를 입력해서 들어온 경우만 처음부터 게임 메뉴를 보여줘요. 그냥 들어오면 기록이 남는 판을 3번 마칠 때까지 무작위 게임이 이어져요
	private static bool codeEntry, runsLoaded; private static int runs; private bool gated;
	private static string RunsPath { get { return Path.Combine(Program.DataDir, "arcade_runs.txt"); } }
	private static int Runs { get { if (!runsLoaded) { runsLoaded = true; try { int.TryParse(File.ReadAllText(RunsPath).Trim(), out runs); } catch { } } return runs; } }
	private static void AddRun() { int n = Runs + 1; runs = n; try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(RunsPath, n.ToString()); } catch { } }
	public static void OpenMenu() { codeEntry = true; Open(); codeEntry = false; if (cur != null && !cur.IsDisposed) { cur.gated = false; cur.state = 0; cur.page = 0; } }
	public static void Open()
	{
		try { if (cur != null && !cur.IsDisposed) { cur.WindowState = FormWindowState.Normal; cur.Activate(); return; } cur = new MaryongGame(); cur.Show(); cur.Activate(); }
		catch (Exception ex) { cur = null; Program.LogErr(ex); try { Bubble.Say("게임을 열지 못했어요 (" + ex.GetType().Name + ")", 3); } catch { } }
	}

	private MaryongGame()
	{
		// 화면이 작거나 배율이 큰 PC에서도 창이 화면 밖으로 나가지 않게, 쓸 수 있는 크기에 맞춰 줄여요 (비율은 그대로)
		S = Dpi.Scale;
		try
		{
			Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
			float chromeH = SystemInformation.CaptionHeight + SystemInformation.FixedFrameBorderSize.Height * 2 + 12f, chromeW = SystemInformation.FixedFrameBorderSize.Width * 2 + 12f;
			float fit = Math.Min((wa.Height - chromeH - 8f) / MiniGame.H, (wa.Width - chromeW - 8f) / MiniGame.W);
			if (fit < S) S = Math.Max(0.5f, fit);
		}
		catch { }
		Image mas = Program.MascotImage();
		List<MiniGame> gl = new List<MiniGame>();
		Func<MiniGame>[] mk = { () => new KartGame(), () => new VolleyGame(), () => new SoliGame(), () => new CatchGame(), () => new JumpGame(), () => new BatGame(), () => new StressGame(), () => new SuikaGame(), () => new TowerGame(), () => new TimeGame(), () => new MemoryGame(), () => new Game2048(), () => new TypeGame(), () => new BasketGame(), () => new BowlGame(), () => new SlingGame(), () => new MoleGame(), () => new ShellGame(), () => new MicroGame(), () => new OfficeGame(), () => new ArcheryGame(), () => new ClimbGame(), () => new IceGame(), () => new CurlGame(), () => new RhythmGame(), () => new GolfGame(), () => new JumpRopeGame(), () => new RandomTdGame(), () => new MatchPuzzleGame(), () => new RunGame(), () => new FlapGame(), () => new FreeKickGame(), () => new CoverGame() };
		foreach (Func<MiniGame> f in mk)
		{
			try { MiniGame m = f(); m.Mascot = (m.Costume != null ? Program.OutfitImage(m.Costume) : null) ?? mas; m.Reset(); gl.Add(m); } catch (Exception ex) { Program.LogErr(ex); }
		}
		games = gl.ToArray();
		ResetRanksOnce();
		foreach (MiniGame m in games) { try { best[m.Key] = LoadBoard(m.Key).Select(k => k.Value).DefaultIfEmpty(0).Max(); } catch { best[m.Key] = 0; } }
		try
		{
			// 히든 게임: 다른 게임을 모두 한 번씩 해보면 열려요
			playedN = games.Count(m => best[m.Key] > 0); totalN = games.Length;
			MiniGame hid = new SurvivalGame(); hid.Mascot = Program.OutfitImage(hid.Costume) ?? mas; hid.Reset(); best[hid.Key] = LoadBoard(hid.Key).Select(k => k.Value).DefaultIfEmpty(0).Max();
			MiniGame hid2 = new CityHallGame(); hid2.Mascot = Program.OutfitImage(hid2.Costume) ?? mas; hid2.Reset(); best[hid2.Key] = LoadBoard(hid2.Key).Select(k => k.Value).DefaultIfEmpty(0).Max();
			if (playedN >= totalN || best[hid.Key] > 0 || best[hid2.Key] > 0) { List<MiniGame> l2 = new List<MiniGame>(games); l2.Add(hid); l2.Add(hid2); games = l2.ToArray(); hiddenOpen = true; }
		}
		catch (Exception ex) { Program.LogErr(ex); }
		FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true; Text = "마룡이 오락실"; ShowInTaskbar = true; StartPosition = FormStartPosition.CenterScreen;
		ClientSize = new Size((int)(MiniGame.W * S), (int)(MiniGame.H * S)); KeyPreview = true; TopMost = true; MinimumSize = new Size(240, 380);
		LoadLayout(); Resize += delegate { Relayout(); }; Relayout();
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
		inp.Mx = MiniGame.W / 2f;
		gated = !codeEntry && Runs < 3;
		Shuffle();
		tb = new TextBox(); tb.BorderStyle = BorderStyle.FixedSingle; tb.Font = Fonts.Native(14f); tb.Visible = false;
		tb.SetBounds(gx + (int)(20 * S), gy + (int)(516 * S), (int)(360 * S), (int)(34 * S)); Controls.Add(tb);
		MouseMove += delegate(object o, MouseEventArgs e) { float nx = (e.X - gx) / S, ny = (e.Y - gy) / S; if (Math.Abs(nx - inp.Mx) + Math.Abs(ny - inp.My) > 0.5f) inp.MouseMoved = true; inp.Mx = nx; inp.My = ny; };
		MouseDown += delegate(object o, MouseEventArgs e) { if ((e.X - gx) / S > MiniGame.W - 46 && (e.Y - gy) / S > 34 && (e.Y - gy) / S < 66) { Sfx.GameOn = !Sfx.GameOn; if (Sfx.GameOn) Sfx.Play("ok"); return; } mouseHeld = true; dnX = (e.X - gx) / S; dnY = (e.Y - gy) / S; inp.Mx = dnX; inp.My = dnY; OnTap((e.X - gx) / S, (e.Y - gy) / S); };
		MouseUp += delegate(object o, MouseEventArgs e) { mouseHeld = false; if (state == 2 && g != null) { float ddx = (e.X - gx) / S - dnX, ddy = (e.Y - gy) / S - dnY; if (Math.Abs(ddx) + Math.Abs(ddy) > 24f) g.Swipe(ddx, ddy); } };
		Deactivate += delegate { mouseHeld = false; if (state == 2 && finT <= 0f) paused = true; };
		KeyDown += delegate(object o, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.F2) { ToggleWide(); e.Handled = true; return; }
			if (state == 4) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape) { e.Handled = true; e.SuppressKeyPress = true; Commit(); } return; }
			bool fresh = down.Add(e.KeyCode); if (!(state == 2 && g.NeedsInput)) e.Handled = true;
			if (state == 5) { if (e.KeyCode == Keys.Escape) GoMenu(); else if (e.KeyCode == Keys.Left) RankTurn(-1); else if (e.KeyCode == Keys.Right) RankTurn(1); return; }
			if (e.KeyCode == Keys.Escape) { Close(); return; } if (e.KeyCode == Keys.M && !(state == 2 && g != null && g.NeedsInput)) { Sfx.GameOn = !Sfx.GameOn; if (Sfx.GameOn) Sfx.Play("ok"); return; }
			if (!fresh) return;
			if (state == 2 && paused) { paused = false; if (tb.Visible) tb.Focus(); return; }
			if (e.KeyCode == Keys.P && state == 2 && introT <= 0f && !g.NeedsInput) { paused = true; return; }
			if (state == 0) { if (e.KeyCode == Keys.Left) Turn(-1); else if (e.KeyCode == Keys.Right) Turn(1); return; }
			if (state == 2) { if (!g.NeedsInput) g.Press(e.KeyCode); }
			else if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && (state == 1 || state == 3)) StartGame();
		};
		KeyUp += delegate(object o, KeyEventArgs e) { down.Remove(e.KeyCode); };
		Deactivate += delegate { down.Clear(); };
		tm.Tick += delegate { Step(); };
		Shown += delegate { sw.Restart(); last = 0; tm.Start(); };
		try { Icon = MascotIcon(); ShowIcon = true; } catch { }
		FormClosing += delegate { SaveLayout(); };
		FormClosed += delegate { Anim.GameBusy = false; tm.Stop(); cur = null; Sfx.Stop(); try { Spr.ReleaseAll(); } catch { } Mem.Later(2500); };
	}

	// 이 버전을 처음 켤 때 한 번만, 이 PC에 저장된 게임 기록(순위표·최고 점수·보내지 못한 기록)을 비워요
	private static void ResetRanksOnce()
	{
		try
		{
			// 예전에는 여기서 기록을 한 번 지웠어요. 이제는 버전이 바뀌어도 같은 게임의 기록은 그대로 둬요.
			// 혹시 기록 파일이 사라졌으면 백업 폴더(rank_backup)에서 되살려요
			string bk = Path.Combine(Program.DataDir, "rank_backup"); if (!Directory.Exists(Program.DataDir)) return; Directory.CreateDirectory(bk); foreach (string f in Directory.GetFiles(Program.DataDir, "ranking*.txt")) { string d2 = Path.Combine(bk, Path.GetFileName(f)); try { if (!File.Exists(d2)) File.Copy(f, d2); } catch { } }
			foreach (string f in Directory.GetFiles(bk, "ranking*.txt")) { string dst = Path.Combine(Program.DataDir, Path.GetFileName(f)); try { if (!File.Exists(dst)) File.Copy(f, dst); } catch { } }
		}
		catch { }
	}
	private static string RankPath(string key) { return Path.Combine(Program.DataDir, "ranking_" + key + ".txt"); }
	private List<KeyValuePair<string, int>> LoadBoard(string key)
	{
		List<KeyValuePair<string, int>> r = new List<KeyValuePair<string, int>>();
		try
		{
			string p = RankPath(key);
			if (!File.Exists(p) && key == "catch") p = Path.Combine(Program.DataDir, "ranking.txt");   // 예전 기록 이어받기
			if (File.Exists(p))
				foreach (string l in File.ReadAllLines(p)) { int i = l.LastIndexOf('|'); int s; if (i > 0 && int.TryParse(l.Substring(i + 1), out s)) r.Add(new KeyValuePair<string, int>(l.Substring(0, i), s)); }
		}
		catch { }
		return r.OrderByDescending(k => k.Value).Take(5).ToList();
	}

	private int playedN, totalN; private bool hiddenOpen;
	private const int PerPage = 6;
	private int page; private TextBox tb; private int rkIdx, pendScore;
	private RectangleF RankBtn { get { return new RectangleF(12, 40, 84, 30); } }
	private string quip = ""; private object quipG; private int quipS = -1;
	private RectangleF OkBtn { get { return new RectangleF(120, 392, 160, 46); } }
	private RectangleF RkPrev { get { return new RectangleF(26, 78, 48, 36); } }
	private RectangleF RkNext { get { return new RectangleF(326, 78, 48, 36); } }
	private RectangleF RkBack { get { return new RectangleF(120, 528, 160, 40); } }
	private int Pages { get { return (games.Length + PerPage - 1) / PerPage; } }
	private RectangleF CardRect(int i) { int k = i % PerPage; float cw = 172, chh = 128; return new RectangleF(20 + (k % 2) * (cw + 16), 98 + (k / 2) * (chh + 10), cw, chh); }
	private RectangleF PrevBtn { get { return new RectangleF(20, 528, 80, 34); } }
	private RectangleF NextBtn { get { return new RectangleF(300, 528, 80, 34); } }
	private void Turn(int d) { if (Pages > 1) { page = (page + d + Pages) % Pages; Sfx.Play("ui_click"); } }
	private RectangleF BackBtn { get { return new RectangleF(12, 40, 100, 30); } }
	private RectangleF AgainBtn { get { return new RectangleF(212, 470, 168, 46); } }
	private RectangleF ListBtn { get { return new RectangleF(20, 470, 168, 46); } }

	private static string lastKey = "";
	private void OpenRank(int i) { rkIdx = Math.Max(0, Math.Min(games.Length - 1, i)); state = 5; Sfx.Play("ui_open"); Rank.Fetch(games[rkIdx].Key, true); }
	private void RankTurn(int d) { rkIdx = (rkIdx + d + games.Length) % games.Length; Sfx.Play("ui_click"); Rank.Fetch(games[rkIdx].Key, false); }
	private void GoMenu() { if (gated) { if (Runs >= 3) gated = false; else { g.Fx.Clear(); Shuffle(); paused = false; down.Clear(); mouseHeld = false; try { tb.Visible = false; } catch { } Sfx.Play("ui_click"); return; } } g.Fx.Clear(); try { Spr.ReleaseAll(); ImgCache.Clear(); MiniGame.ClearTxt(); GC.Collect(); } catch { } state = 0; paused = false; down.Clear(); mouseHeld = false; try { tb.Visible = false; } catch { } Sfx.Play("ui_click"); }
	private void Shuffle()
	{
		Random r = new Random(); MiniGame n;
		do { n = games[r.Next(games.Length)]; } while (games.Length > 1 && (n.Key == lastKey || n == g));
		g = n; lastKey = n.Key; g.Reset(); Warm(g); state = 1;
	}
	// 게임을 고르는 순간 한 번 미리 그려 보고 돌려 봐서, 그림 읽기·크기 맞추기 같은 첫 프레임 준비를 시작 화면 전에 끝내요 (게임 중 버벅임 방지)
	private void Warm(MiniGame m)
	{
		bool so = Sfx.GameOn; Sfx.GameOn = false;
		try
		{
			float rs = Math.Min(S, 1.25f); int w = (int)Math.Ceiling(MiniGame.W * rs), h = (int)Math.Ceiling(MiniGame.H * rs); MiniGame.PS = rs;
			using (Bitmap b = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
			{
				b.SetResolution(96f * rs, 96f * rs);
				using (Graphics gg = Graphics.FromImage(b)) { Gfx.HQ(gg); gg.InterpolationMode = InterpolationMode.Bilinear; gg.ScaleTransform(rs, rs); GameInput wi = new GameInput(); m.Draw(gg, 0.0); for (int k = 1; k <= 4; k++) { m.Update(0.016f, wi, 0.016 * k); m.Draw(gg, 0.016 * k); } }
			}
		}
		catch { }
		finally { MiniGame.PS = 0; Sfx.GameOn = so; try { m.Fx.Clear(); m.Reset(); } catch { } }
	}
	private string lastGKey = ""; private void StartGame() { try { if (lastGKey != g.Key) { if (lastGKey != "") { ImgCache.Clear(); MiniGame.ClearTxt(); GC.Collect(); } lastGKey = g.Key; } } catch { } try { if (Rank.On) Rank.Fetch(g.Key, false); } catch { } qSum = 0; qN = 0; qBad = qGood = 0; bool again = state == 3; g.Fx.Clear(); g.Reset(); g.PlayT = 0f; state = 2; paused = false; stopT = 0f; dispScore = 0f; floats.Clear(); inp.MouseMoved = false; introT = again ? 1.9f : 2.6f; introNum = -1; finT = 0; lastScore = 0; hudBump = 0; g.Shake = 0; down.Clear(); mouseHeld = false; }

	private void OnTap(float x, float y)
	{
		if (state == 0)
		{
			for (int i = page * PerPage; i < Math.Min(games.Length, (page + 1) * PerPage); i++) if (CardRect(i).Contains(x, y)) { g = games[i]; g.Reset(); Warm(g); state = 1; Sfx.Play("ui_open"); return; }
			if (RankBtn.Contains(x, y)) { OpenRank(page * PerPage); return; }
			if (PrevBtn.Contains(x, y)) Turn(-1); else if (NextBtn.Contains(x, y)) Turn(1);
		}
		else if (state == 4) { if (OkBtn.Contains(x, y)) Commit(); }
		else if (state == 5) { if (RkBack.Contains(x, y)) GoMenu(); else if (RkPrev.Contains(x, y)) RankTurn(-1); else if (RkNext.Contains(x, y)) RankTurn(1); }
		else if (state == 1) { if (BackBtn.Contains(x, y)) GoMenu(); else StartGame(); }
		else if (state == 3) { if (ListBtn.Contains(x, y)) GoMenu(); else if (AgainBtn.Contains(x, y)) StartGame(); }
		else if (state == 2) { if (paused) { paused = false; mouseHeld = false; if (tb.Visible) tb.Focus(); return; } g.Click(x, y); }
	}

	private void Finish()
	{
		state = 3; board = LoadBoard(g.Key);
		int bst = board.Count > 0 ? board[0].Value : 0; prevBest = bst; thisKey = "";
		newRecord = g.Score > bst && g.Score > 0;
		// 5위 안에 들면 옛 오락실처럼 이름을 직접 적어요
		if (g.Score > 0 && (board.Count < 5 || g.Score > board[board.Count - 1].Value))
		{
			pendScore = g.Score; state = 4; try { if (Rank.On) Rank.Fetch(g.Key, true); } catch { } Sfx.Play(newRecord ? "record" : "ok2"); if (newRecord) Confetti.Fire(Bounds);
			try { tb.Text = Rank.LastName; tb.Visible = true; tb.SelectAll(); tb.Focus(); } catch { }
			return;
		}
		Sfx.Play("over");
	}
	// 이름을 적고 확인하면 기록이 저장돼요
	private void Commit()
	{
		string nm = Rank.Clean(tb.Text); if (nm.Length == 0) nm = "익명"; Rank.LastName = nm;
		thisKey = nm; board = LoadBoard(g.Key);
		board.Add(new KeyValuePair<string, int>(thisKey, pendScore));
		board = board.OrderByDescending(k => k.Value).Take(5).ToList();
		try { Directory.CreateDirectory(Program.DataDir); string[] ln = board.Select(k => k.Key + "|" + k.Value).ToArray(); File.WriteAllLines(RankPath(g.Key), ln); string bk = Path.Combine(Program.DataDir, "rank_backup"); Directory.CreateDirectory(bk); File.WriteAllLines(Path.Combine(bk, Path.GetFileName(RankPath(g.Key))), ln); } catch { }
		best[g.Key] = board[0].Value; Rank.Submit(g.Key, nm, pendScore); if (gated) { AddRun(); if (Runs >= 3) { gated = false; try { Bubble.Say("게임 메뉴가 열렸어요!", 2.5); Confetti.Fire(Bounds); } catch { } } }
		try { tb.Visible = false; tb.TextAlign = HorizontalAlignment.Left; tb.Font = Fonts.Native(14f); tb.MaxLength = 32767; tb.SetBounds(gx + (int)(20 * S), gy + (int)(516 * S), (int)(360 * S), (int)(34 * S)); tb.Text = ""; } catch { }
		state = 3; Sfx.Play("ok2"); this.Focus();
	}

	private static void GameFrameStamp() { Program.FrameStamp(); }
	// 오른쪽 위 스피커 버튼 (눌러서 소리 켜기/끄기 · M 키)
	private void DrawSpeaker(Graphics gr)
	{
		bool on = Sfx.GameOn && Program.SoundOn; float x = MiniGame.W - 34, y = 40;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(x - 8, y - 5, 30, 26), 8)) using (SolidBrush b = new SolidBrush(Color.FromArgb(on ? 70 : 40, 255, 255, 255))) gr.FillPath(b, gp);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(on ? 235 : 140, 255, 255, 255))) { gr.FillPolygon(b, new PointF[] { new PointF(x, y + 5), new PointF(x + 5, y + 5), new PointF(x + 11, y), new PointF(x + 11, y + 16), new PointF(x + 5, y + 11), new PointF(x, y + 11) }); }
		using (Pen p = new Pen(Color.FromArgb(on ? 235 : 140, 255, 255, 255), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		{
			if (on) { gr.DrawArc(p, x + 9, y + 3, 7, 10, -60, 120); gr.DrawArc(p, x + 11, y, 11, 16, -60, 120); }
			else { gr.DrawLine(p, x + 14, y + 4, x + 21, y + 12); gr.DrawLine(p, x + 21, y + 4, x + 14, y + 12); }
		}
	}
	private void Step()
	{
		Anim.GameBusy = Visible && WindowState != FormWindowState.Minimized;
		if (IsDisposed) { tm.Stop(); return; }
		double now = sw.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.05, now - last); last = now; if (paused && state == 2) { InvSlow(now); return; } tt += dt;
			if (hudBump > 0) hudBump -= dt * 3.5f; dispScore += (g != null ? g.Score - dispScore : 0f) * Math.Min(1f, dt * 14f); for (int fi = floats.Count - 1; fi >= 0; fi--) { floats[fi][0] += dt; if (floats[fi][0] > 0.9f) floats.RemoveAt(fi); } if (g != null && g.Shake > 0) g.Shake = Math.Max(0f, g.Shake - dt * 2.5f);
			if (state == 2 && introT > 0) { introT -= dt; int num = introT > 1.9f ? 3 : (introT > 1.25f ? 2 : (introT > 0.6f ? 1 : 0)); if (num != introNum) { introNum = num; Sfx.Play(num > 0 ? "tick" : "ok2"); } g.Fx.Update(dt); }
			else if (state == 2 && finT > 0) { finT -= dt; g.Fx.Update(dt); if (finT <= 0) Finish(); }
			else if (state == 2)
		{
			inp.Down = down; inp.Hold = mouseHeld || down.Contains(Keys.Space) || down.Contains(Keys.Up) || down.Contains(Keys.W);
			inp.L = down.Contains(Keys.Left) || down.Contains(Keys.A); inp.R = down.Contains(Keys.Right) || down.Contains(Keys.D);
			if (g.NeedsInput)
			{
				if (!tb.Visible) { tb.Visible = true; tb.Text = ""; tb.Focus(); } int iy = gy + (int)(g.InputY * S); if (tb.Top != iy) tb.Top = iy;
				g.Typed = tb.Text; if (g.ClearTyped) { RebuildTb(); g.ClearTyped = false; g.Typed = ""; }
			}
			if (stopT > 0f) stopT -= dt;   // 강한 충격이 있으면 0.07초 정도 멈칫해서 타격감을 줘요
			else { g.PlayT += dt; float sb = g.Shake; g.Update(dt, inp, tt); if (!g.NoLimit && g.PlayT >= 60f && !g.Over) g.Over = true; g.Fx.Update(dt); inp.MouseMoved = false; stopCd -= dt; if (g.Shake - sb > 0.2f && !g.Over && stopCd <= 0f) { stopT = 0.04f; stopCd = 0.7f; } }
			if (g.Score != lastScore) { int dl = g.Score - lastScore; if (dl > 0) hudBump = 1f; if (floats.Count > 0 && floats[floats.Count - 1][0] < 0.18f && Math.Sign(floats[floats.Count - 1][1]) == Math.Sign(dl)) floats[floats.Count - 1][1] += dl; else { floats.Add(new float[] { 0f, dl }); if (floats.Count > 4) floats.RemoveAt(0); } lastScore = g.Score; } if (g.Over) { finT = 1.25f; Sfx.Play("whoosh"); }
		}
		if (tb.Visible && state != 4 && !(state == 2 && g != null && g.NeedsInput)) tb.Visible = false;
		Cursor cc = (state == 2 && g != null && g.Crosshair) ? Cursors.Cross : Cursors.Default; if (Cursor != cc) Cursor = cc;
		InvSlow(now);
	}
	// 오락실 창이 뒤에 있거나(설정창 등을 쓰는 중) 멈춰 있을 땐 초당 8번만 다시 그려요 → 다른 창 반응이 느려지지 않아요
	private double lastInv; private float stopCd;
	private void InvSlow(double now) { if (WindowState == FormWindowState.Minimized) return; if (Form.ActiveForm != this && now - lastInv < 0.125) return; if ((state == 1 || state == 3 || state == 4) && now - lastInv < 0.030) return; lastInv = now; if (!wide || SideSig() != sideSig) Invalidate(); else Invalidate(new Rectangle(gx, gy, GW + 1, GH + 1)); }   // 설명·결과 화면은 초당 30번이면 충분해요

	private void Panel(Graphics gr, RectangleF r, Color c, float rad) { using (GraphicsPath p = Gfx.Round(r, rad)) using (SolidBrush b = new SolidBrush(c)) gr.FillPath(b, p); }
	// 게임 시작 전 설명 카드: 제목 · 방법(번호/점 표시 줄) · 최고 기록 · 시작 안내를 깔끔하게 나눠서 보여줘요
	private int lastDrawState = -1; private double stSince;
	private static void HelpCard(Graphics gr, MiniGame g, int bestScore, double tt)
	{
		string[] lines = (g.Help ?? "").Split('\n'); float tw = 276f, y = 0f, fpt = 11f; float[] hs = new float[lines.Length]; string[] txt = new string[lines.Length]; string[] chip = new string[lines.Length];
		string circ = "①②③④⑤⑥"; float ch = 0f;
		for (int i = 0; i < lines.Length; i++)
		{
			string l = lines[i].Trim(); chip[i] = null; if (l.Length > 1 && circ.IndexOf(l[0]) >= 0) { chip[i] = (circ.IndexOf(l[0]) + 1).ToString(); l = l.Substring(1).Trim(); }
			txt[i] = l;
		}
		// 글이 길면 글자를 조금씩 줄여서 카드가 화면(시작 버튼 포함) 안에 들어오게 해요 (측정은 화면 배율과 무관한 논리 크기로)
		foreach (float tryPt in new float[] { 11f, 10.5f, 10f, 9.5f, 9f })
		{
			fpt = tryPt; Font fb = Theme.F(fpt, 0); y = 0f;
			for (int i = 0; i < lines.Length; i++) { hs[i] = Math.Max(20f, Gfx.MeasureHL(txt[i], fb, tw - 12f) + 6f); y += hs[i] + 6f; }
			ch = 66f + y + 16f; if (ch <= 454f) break;
		}
		float cy0 = Math.Max(60f, 250f - ch / 2f - 20f); cy0 = Math.Max(14f, Math.Min(cy0, 468f - ch)); RectangleF card = new RectangleF(22f, cy0, MiniGame.W - 44f, ch);
		using (GraphicsPath sh = Gfx.Round(new RectangleF(card.X, card.Y + 4f, card.Width, card.Height), 20f)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) gr.FillPath(sb, sh);
		using (GraphicsPath gp = Gfx.Round(card, 20f))
		{
			using (LinearGradientBrush lb = new LinearGradientBrush(card, Color.FromArgb(238, 30, 42, 86), Color.FromArgb(238, 16, 22, 52), 90f)) gr.FillPath(lb, gp);
			using (Pen p = new Pen(Color.FromArgb(90, 170, 200, 255), 1.4f)) gr.DrawPath(p, gp);
		}
		MiniGame.Txt(gr, g.Name, 22f, 2, Color.White, card.X, card.Y + 12f, card.Width, 38f, StringAlignment.Center);
		using (Pen p = new Pen(Color.FromArgb(120, 255, 205, 80), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) gr.DrawLine(p, MiniGame.W / 2f - 22f, card.Y + 54f, MiniGame.W / 2f + 22f, card.Y + 54f);
		float yy = card.Y + 68f;
		for (int i = 0; i < lines.Length; i++)
		{
			float cyc = yy + 11f;
			if (chip[i] != null) { using (SolidBrush cb = new SolidBrush(Color.FromArgb(255, 255, 205, 80))) gr.FillEllipse(cb, card.X + 16f, cyc - 9f, 18f, 18f); MiniGame.Txt(gr, chip[i], 9.5f, 2, Color.FromArgb(255, 40, 30, 10), card.X + 16f, cyc - 9f, 18f, 18f, StringAlignment.Center); }
			else using (SolidBrush cb = new SolidBrush(Color.FromArgb(200, 140, 190, 255))) gr.FillEllipse(cb, card.X + 22f, cyc - 3f, 6f, 6f);
			MiniGame.Txt(gr, txt[i], fpt, 0, Color.FromArgb(255, 232, 238, 252), card.X + 42f, yy, tw - 6f, hs[i], StringAlignment.Near, true);
			yy += hs[i] + 6f;
		}
		float by = card.Bottom + 18f; string bt = bestScore > 0 ? "내 최고 기록  " + bestScore + "점" : "첫 기록에 도전!";
		using (GraphicsPath bp = Gfx.Round(new RectangleF(MiniGame.W / 2f - 80f, by, 160f, 28f), 14f)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 12, 20, 46))) gr.FillPath(b, bp); using (Pen p = new Pen(Color.FromArgb(120, 255, 205, 80), 1.2f)) gr.DrawPath(p, bp); }
		MiniGame.Txt(gr, bt, 11f, 1, Color.FromArgb(255, 255, 222, 120), MiniGame.W / 2f - 80f, by, 160f, 28f, StringAlignment.Center);
		float pulse = 1f + 0.035f * (float)Math.Sin(tt * 4); float sw = 190f * pulse, sh2 = 44f * pulse, sy = by + 48f; RectangleF sr = new RectangleF((MiniGame.W - sw) / 2f, sy, sw, sh2);
		using (GraphicsPath sp = Gfx.Round(sr, sh2 / 2f)) { using (LinearGradientBrush lb = new LinearGradientBrush(sr, Color.FromArgb(255, 255, 220, 110), Color.FromArgb(255, 255, 170, 60), 90f)) gr.FillPath(lb, sp); }
		MiniGame.Txt(gr, "화면을 눌러 시작", 14f, 2, Color.FromArgb(255, 60, 38, 8), sr.X, sr.Y, sr.Width, sr.Height, StringAlignment.Center);
	}
	// 결과·이름 입력·일시정지 화면이 함께 쓰는 둥근 카드
	private static void Card(Graphics gr, RectangleF card)
	{
		using (GraphicsPath sh = Gfx.Round(new RectangleF(card.X, card.Y + 4f, card.Width, card.Height), 20f)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) gr.FillPath(sb, sh);
		using (GraphicsPath gp = Gfx.Round(card, 20f))
		{
			using (LinearGradientBrush lb = new LinearGradientBrush(card, Color.FromArgb(238, 30, 42, 86), Color.FromArgb(238, 16, 22, 52), 90f)) gr.FillPath(lb, gp);
			using (Pen p = new Pen(Color.FromArgb(90, 170, 200, 255), 1.4f)) gr.DrawPath(p, gp);
		}
	}
	private void Btn(Graphics gr, RectangleF r, string s, bool main)
	{
		using (GraphicsPath p = Gfx.Round(r, r.Height / 2f))
		{
			if (main) using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(255, 255, 220, 110), Color.FromArgb(255, 255, 170, 60), 90f)) gr.FillPath(lb, p);
			else { using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) gr.FillPath(b, p); using (Pen pn = new Pen(Color.FromArgb(110, 190, 215, 255), 1.2f)) gr.DrawPath(pn, p); }
		}
		MiniGame.Txt(gr, s, 12.5f, 2, main ? Color.FromArgb(60, 38, 8) : Color.White, r.X, r.Y, r.Width, r.Height, StringAlignment.Center);
	}

	// ── 창 크기 · 가로 모드 ──
	// 창을 늘리고 줄이면 게임 화면이 비율 그대로 맞춰져요. 창이 가로로 넓으면 게임은 왼쪽에, 오른쪽엔 설명·기록 패널이 나와요 (F2 = 세로/가로 전환)
	private int gx, gy; private bool wide; private Bitmap sideBmp; private string sideSig = "";
	private int GW { get { return (int)Math.Round(MiniGame.W * S); } }
	private int GH { get { return (int)Math.Round(MiniGame.H * S); } }
	private static string LayoutPath { get { return Path.Combine(Program.DataDir, "arcade_layout.txt"); } }
	private void LoadLayout()
	{
		try
		{
			if (!File.Exists(LayoutPath)) return; string[] p = File.ReadAllText(LayoutPath).Trim().Split(','); int w, h; if (p.Length < 2 || !int.TryParse(p[0], out w) || !int.TryParse(p[1], out h)) return;
			Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea; w = Math.Max(240, Math.Min(w, wa.Width - 20)); h = Math.Max(380, Math.Min(h, wa.Height - 60)); ClientSize = new Size(w, h);
		}
		catch { }
	}
	private void SaveLayout() { try { if (WindowState != FormWindowState.Normal) return; Directory.CreateDirectory(Program.DataDir); File.WriteAllText(LayoutPath, ClientSize.Width + "," + ClientSize.Height); } catch { } }
	private void Relayout()
	{
		int cw = ClientSize.Width, ch = ClientSize.Height; if (cw <= 0 || ch <= 0) return;
		wide = cw > ch * 0.95f;
		if (wide) { S = Math.Max(0.35f, Math.Min(ch / (float)MiniGame.H, (cw - 220f) / MiniGame.W)); gx = 0; gy = (int)((ch - MiniGame.H * S) / 2f); }
		else { S = Math.Max(0.35f, Math.Min(cw / (float)MiniGame.W, ch / (float)MiniGame.H)); gx = (int)((cw - MiniGame.W * S) / 2f); gy = (int)((ch - MiniGame.H * S) / 2f); }
		try { if (tb != null) tb.SetBounds(gx + (int)(20 * S), gy + (int)((g != null ? g.InputY : 516f) * S), (int)(360 * S), (int)(34 * S)); } catch { }
		sideSig = ""; ql = 0; qSum = 0; qN = 0; qBad = 0; Invalidate();
	}
	private void ToggleWide()
	{
		if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
		float s0 = S; Rectangle wa = Screen.FromControl(this).WorkingArea;
		Size want = wide ? new Size((int)(MiniGame.W * s0), (int)(MiniGame.H * s0)) : new Size((int)(MiniGame.W * s0 + 380 * s0), (int)(MiniGame.H * s0));
		if (want.Width > wa.Width - 20) { float k = (wa.Width - 20f) / want.Width; want = new Size((int)(want.Width * k), (int)(want.Height * k)); }
		ClientSize = want; if (Right > wa.Right) Left = Math.Max(wa.Left, wa.Right - Width); Relayout();
	}
	// 게임 화면 바깥: 세로 모드는 어두운 여백, 가로 모드는 오른쪽 안내 패널
	private string SideSig()
	{
		MiniGame cg = (state >= 1 && state <= 3) ? g : null; int bst = 0; if (cg != null) best.TryGetValue(cg.Key, out bst);
		return (ClientSize.Width - GW) + "x" + ClientSize.Height + ":" + (cg == null ? "-" : cg.Key) + ":" + bst;
	}
	private void PaintAround(Graphics eg)
	{
		int cw = ClientSize.Width, ch = ClientSize.Height, gw = GW, gh = GH;
		using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 18, 34)))
		{
			if (gy > 0) { eg.FillRectangle(b, 0, 0, cw, gy); eg.FillRectangle(b, 0, gy + gh, cw, ch - gy - gh + 1); }
			if (gx > 0) eg.FillRectangle(b, 0, 0, gx, ch);
			if (!wide && gx + gw < cw) eg.FillRectangle(b, gx + gw, 0, cw - gx - gw + 1, ch);
		}
		if (!wide) return;
		int pw = cw - gw; if (pw < 40) return;
		MiniGame cg = (state >= 1 && state <= 3) ? g : null; int bst = 0; if (cg != null) best.TryGetValue(cg.Key, out bst);
		string sig = SideSig();
		if (sideBmp == null || sig != sideSig)
		{
			sideSig = sig; if (sideBmp != null) sideBmp.Dispose(); sideBmp = new Bitmap(pw, ch, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics sg = Graphics.FromImage(sideBmp))
			{
				Gfx.HQ(sg);
				using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, pw, ch), Color.FromArgb(22, 28, 60), Color.FromArgb(40, 52, 110), 90f)) sg.FillRectangle(lb, 0, 0, pw, ch);
				float k = S; sg.ScaleTransform(k, k); float lw = pw / k, y = 24f;
				MiniGame.Txt(sg, "마룡이 오락실", 16f, 2, Color.FromArgb(255, 214, 90), 18, y, lw - 36, 28, StringAlignment.Near); y += 36f;
				if (cg != null)
				{
					MiniGame.Txt(sg, cg.Name, 14f, 2, Color.White, 18, y, lw - 36, 26, StringAlignment.Near); y += 28f;
					MiniGame.Txt(sg, cg.Desc, 10f, 1, Color.FromArgb(210, 225, 240), 18, y, lw - 36, 20, StringAlignment.Near); y += 30f;
					MiniGame.Txt(sg, "하는 법", 10.5f, 2, Color.FromArgb(150, 220, 255), 18, y, lw - 36, 20, StringAlignment.Near); y += 22f;
					float hh = Math.Min(300f, sg.MeasureString(cg.Help, Theme.F(9.5f, 1), (int)(lw - 36)).Height + 6f); MiniGame.Txt(sg, cg.Help, 9.5f, 1, Color.FromArgb(235, 240, 250), 18, y, lw - 36, hh, StringAlignment.Near, true); y += hh + 12f;
					MiniGame.Txt(sg, "내 최고 기록  " + bst.ToString("N0"), 11f, 2, Color.FromArgb(255, 226, 140), 18, y, lw - 36, 22, StringAlignment.Near); y += 28f;
					try
					{
						List<KeyValuePair<string, int>> rows = LoadBoard(cg.Key);
						for (int i = 0; i < Math.Min(5, rows.Count); i++) { MiniGame.Txt(sg, (i + 1) + ".  " + rows[i].Key, 9.5f, 1, Color.FromArgb(220, 230, 245), 26, y, lw - 140, 20, StringAlignment.Near); MiniGame.Txt(sg, rows[i].Value.ToString("N0"), 9.5f, 2, Color.White, lw - 120, y, 100, 20, StringAlignment.Far); y += 21f; }
					}
					catch { }
				}
				else
				{
					MiniGame.Txt(sg, "게임을 고르면 여기에 하는 법과 기록이 나와요", 10f, 1, Color.FromArgb(210, 225, 240), 18, y, lw - 36, 40, StringAlignment.Near, true); y += 50f;
				}
				MiniGame.Txt(sg, "창 크기를 바꾸면 게임 화면이 맞춰져요 · F2 = 세로/가로 전환", 8.5f, 1, Color.FromArgb(150, 200, 210, 230), 18, ch / k - 30f, lw - 36, 20, StringAlignment.Near);
			}
		}
		eg.DrawImageUnscaled(sideBmp, gw, 0);
	}
	private long pfT0, pfLast; private int pfN; private double pfCost, pfMaxCost, pfMaxGap;
	protected override void OnPaint(PaintEventArgs e)
	{
		long t0 = Stopwatch.GetTimestamp();
		try { PaintAround(e.Graphics); } catch (Exception ex) { Program.LogErr(ex); }
		// 화면 배율이 큰 PC(고해상도)에서는 그림 그릴 픽셀이 너무 많아 느려져요 → 1.25배까지만 직접 그리고, 그 이상이면 작게 그려서 늘려 보여요
		float rs = Math.Min(S, ql == 0 ? 1.25f : (ql == 1 ? 1.0f : (ql == 2 ? 0.8f : 0.65f)));
		double tInner = 0, tBlit = 0; long tq = Stopwatch.GetTimestamp(); bool done = false;
		if (useGdi && ClientSize.Width > 0 && ClientSize.Height > 0)
		{
			try
			{
				int ow = (int)Math.Ceiling(MiniGame.W * rs), oh = (int)Math.Ceiling(MiniGame.H * rs);
				if (dib == null || dib.W != ow || dib.H != oh) { if (dib != null) dib.Dispose(); dib = new GdiBuf(ow, oh); dib.Bmp.SetResolution(96f * rs, 96f * rs); }
				using (Graphics og = Graphics.FromImage(dib.Bmp)) { MiniGame.PS = rs; paintScale = rs; try { OnPaintInner(og); DrawSpeaker(og); } catch (Exception ex) { Program.LogErr(ex); } }
				MiniGame.PS = 0; long tm1 = Stopwatch.GetTimestamp(); tInner = (tm1 - tq) * 1000.0 / Stopwatch.Frequency;
				IntPtr hdc = e.Graphics.GetHdc();
				try { done = dib.Blit(hdc, gx, gy, GW, GH, ow != GW || oh != GH, ql >= 1); }
				finally { e.Graphics.ReleaseHdc(hdc); }
				tBlit = (Stopwatch.GetTimestamp() - tm1) * 1000.0 / Stopwatch.Frequency;
				if (!done) { useGdi = false; Anim.PerfNote("GDI 출력 실패 → 예전 방식으로 전환"); }
			}
			catch (Exception ex) { useGdi = false; MiniGame.PS = 0; Program.LogErr(ex); Anim.PerfNote("GDI 출력 오류 → 예전 방식으로 전환 (" + ex.GetType().Name + ")"); }
		}
		if (!done)
		{
			if (S > rs + 0.01f && ClientSize.Width > 0 && ClientSize.Height > 0)
			{
				int ow = (int)Math.Ceiling(MiniGame.W * rs), oh = (int)Math.Ceiling(MiniGame.H * rs);
				if (offBmp == null || offBmp.Width != ow || offBmp.Height != oh) { if (offBmp != null) offBmp.Dispose(); offBmp = new Bitmap(ow, oh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); offBmp.SetResolution(96f * rs, 96f * rs); }
				using (Graphics og = Graphics.FromImage(offBmp)) { MiniGame.PS = rs; paintScale = rs; try { OnPaintInner(og); DrawSpeaker(og); } catch (Exception ex) { Program.LogErr(ex); } }
				MiniGame.PS = 0; Graphics eg = e.Graphics; eg.InterpolationMode = InterpolationMode.Bilinear; eg.PixelOffsetMode = PixelOffsetMode.Half; eg.CompositingMode = CompositingMode.SourceCopy;
				eg.DrawImage(offBmp, new Rectangle(gx, gy, GW, GH), 0, 0, offBmp.Width, offBmp.Height, GraphicsUnit.Pixel); eg.CompositingMode = CompositingMode.SourceOver;
			}
			else { MiniGame.PS = 0; paintScale = S; GraphicsState gs0 = e.Graphics.Save(); e.Graphics.TranslateTransform(gx, gy); OnPaintInner(e.Graphics); try { DrawSpeaker(e.Graphics); } catch { } e.Graphics.Restore(gs0); }
		}
		pInner += tInner; pBlit += tBlit;
		// 그리기가 오래 걸리는 PC(큰 창·고해상도)에서는 스스로 화질을 한 단계씩 낮춰서 끊김을 줄여요 (창 크기를 바꾸면 다시 처음부터 재 봐요)
		if (state == 2 && g != null && !paused)
		{
			double qc = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency; qSum += qc; qN++;
			if (qN >= 24) { double qa = qSum / qN; qSum = 0; qN = 0; if (qa > 9.0 && ql < 3) { qBad++; if (qBad >= 2) { ql++; qBad = 0; Anim.PerfNote("화질 낮춤 → " + ql + " (평균 " + qa.ToString("0.0") + "ms)"); } } else qBad = 0; }
		}
		if (Anim.PerfLog && state == 2 && g != null)   // perf.on 이 있을 때만: 게임 중 프레임 기록
		{
			long t1 = Stopwatch.GetTimestamp(); double f = 1000.0 / Stopwatch.Frequency, cost = (t1 - t0) * f;
			if (pfT0 == 0) pfT0 = t1; if (pfLast != 0) pfMaxGap = Math.Max(pfMaxGap, (t0 - pfLast) * f); pfLast = t1; pfN++; pfCost += cost; pfMaxCost = Math.Max(pfMaxCost, cost);
			if ((t1 - pfT0) * f >= 5000.0) { Anim.PerfNote("게임 " + g.Name + ": " + (pfN * 1000.0 / ((t1 - pfT0) * f)).ToString("0") + "프레임/초 · 그리기 평균 " + (pfCost / pfN).ToString("0.0") + "ms 최대 " + pfMaxCost.ToString("0") + "ms · 가장 긴 간격 " + pfMaxGap.ToString("0") + "ms · 화면배율 " + S + " · " + (useGdi ? "GDI" : "예전방식") + (pfN > 0 ? " (그림 " + (pInner / pfN).ToString("0.0") + "ms + 화면 붙이기 " + (pBlit / pfN).ToString("0.0") + "ms)" : "")); pInner = 0; pBlit = 0; pfT0 = t1; pfN = 0; pfCost = 0; pfMaxCost = 0; pfMaxGap = 0; }
		}
		else { pfT0 = 0; pfLast = 0; pfN = 0; pfCost = 0; pfMaxCost = 0; pfMaxGap = 0; }
	}
	private float menuT; private float[] menuHv;
	private GdiBuf dib; private bool useGdi = !File.Exists(Path.Combine(Program.DataDir, "gdi.off")); private double pInner, pBlit;
	private Bitmap offBmp; private float paintScale = 1f; private int ql, qBad, qGood; private double qSum; private int qN;
	private void OnPaintInner(Graphics gr)
	{
		GameFrameStamp(); Gfx.HQ(gr); gr.InterpolationMode = InterpolationMode.Bilinear; if (ql >= 2) { gr.SmoothingMode = SmoothingMode.HighSpeed; gr.PixelOffsetMode = PixelOffsetMode.HighSpeed; }
		gr.ScaleTransform(paintScale, paintScale);
			if (state == 2 && g != null && g.Shake > 0f) gr.TranslateTransform((float)(Rz.NextDouble() * 2 - 1) * g.Shake * 7f, (float)(Rz.NextDouble() * 2 - 1) * g.Shake * 7f);
		if (state == 5)
		{
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, -1, MiniGame.W, MiniGame.H + 2), Color.FromArgb(14, 18, 54), Color.FromArgb(44, 56, 126), 90f)) gr.FillRectangle(lb, 0, 0, MiniGame.W, MiniGame.H);
			for (int i = 0; i < 26; i++) { float sx = (i * 97 % 400), sy = (i * 61 % 560); double tw = 0.5 + 0.5 * Math.Sin(tt * 2 + i); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(40 + 120 * tw), 255, 255, 255))) gr.FillEllipse(b, sx, sy, 2, 2); }
			MiniGame m5 = games[rkIdx]; string key5 = m5.Key; int st5 = Rank.Status(key5);
			MiniGame.Txt(gr, "명예의 전당", 22f, 2, Color.White, 0, 30, MiniGame.W, 40, StringAlignment.Center);
			MiniGame.Txt(gr, m5.Name, 17f, 2, Color.FromArgb(255, 214, 90), 60, 80, MiniGame.W - 120, 30, StringAlignment.Center);
			Btn(gr, RkPrev, "◀", false); Btn(gr, RkNext, "▶", false);
			List<KeyValuePair<string, int>> rows = Rank.On && st5 == 2 ? Rank.Get(key5) : LoadBoard(key5);
			string sub = !Rank.On ? "이 PC의 기록 (랭킹 서버가 연결되면 전체 순위가 나와요)" : (st5 == 1 ? "전체 순위를 불러오는 중..." : (st5 == 2 ? "전체 TOP 10 (온라인)" : (st5 == 3 ? "서버에 연결하지 못했어요 · 이 PC의 기록이에요" : "")));
			MiniGame.Txt(gr, sub, 10f, 1, Color.FromArgb(200, 255, 255, 255), 0, 122, MiniGame.W, 20, StringAlignment.Center);
			Color[] medal = { Color.FromArgb(255, 205, 70), Color.FromArgb(205, 214, 228), Color.FromArgb(222, 150, 96) };
			for (int i = 0; i < 10; i++)
			{
				float ry = 150 + i * 36; RectangleF rr = new RectangleF(28, ry, 344, 32); bool has = i < rows.Count;
				Panel(gr, rr, Color.FromArgb(has ? (i < 3 ? 46 : 28) : 14, 255, 255, 255), 12);
				Color mc = i < 3 ? medal[i] : Color.FromArgb(150, 255, 255, 255);
				using (SolidBrush mb = new SolidBrush(Color.FromArgb(has ? 230 : 70, mc))) gr.FillEllipse(mb, 36, ry + 4, 24, 24); MiniGame.Txt(gr, (i + 1).ToString(), 10.5f, 2, Color.FromArgb(40, 30, 50), 36, ry + 4, 24, 24, StringAlignment.Center);
				if (has) { MiniGame.Txt(gr, rows[i].Key, 12.5f, i < 3 ? 2 : 1, Color.White, 72, ry + 2, 170, 28, StringAlignment.Near); MiniGame.Txt(gr, rows[i].Value + "점", 12.5f, 2, i < 3 ? mc : Color.FromArgb(235, 255, 255, 255), 240, ry + 2, 122, 28, StringAlignment.Far); }
				else MiniGame.Txt(gr, "- - -", 11f, 0, Color.FromArgb(90, 255, 255, 255), 72, ry + 2, 170, 28, StringAlignment.Near);
			}
			Btn(gr, RkBack, "게임 메뉴", false);
			return;
		}
		if (state == 0)
		{
			using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, -1, MiniGame.W, MiniGame.H + 2), Color.FromArgb(14, 18, 54), Color.FromArgb(44, 56, 126), 90f)) gr.FillRectangle(lb, 0, 0, MiniGame.W, MiniGame.H);
			for (int i = 0; i < 26; i++) { float sx = (i * 97 % 400), sy = (i * 61 % 560); double tw = 0.5 + 0.5 * Math.Sin(tt * 2 + i); using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(40 + 120 * tw), 255, 255, 255))) gr.FillEllipse(b, sx, sy, 2.4f, 2.4f); }
			MiniGame.Txt(gr, "마룡이 오락실", 22f, 2, Color.White, 0, 34, MiniGame.W, 40, StringAlignment.Center);
			MiniGame.Txt(gr, "하고 싶은 게임을 눌러 보세요", 11f, 0, Color.FromArgb(200, 255, 255, 255), 0, 72, MiniGame.W, 24, StringAlignment.Center);
			// 은은하게 떠다니는 빛망울
			for (int i = 0; i < 7; i++) { float bx = (i * 151 + (float)tt * (6 + i * 2)) % 460 - 30, by = 90 + (i * 83) % 420 + (float)Math.Sin(tt * 0.5 + i) * 14; float br = 22 + (i % 3) * 14; using (SolidBrush b = new SolidBrush(Color.FromArgb(14 + (i % 3) * 5, 170, 190, 255))) gr.FillEllipse(b, bx - br, by - br, br * 2, br * 2); }
			float mdt = (float)Math.Min(0.05, Math.Max(0.0, tt - menuT)); menuT = (float)tt; if (menuHv == null || menuHv.Length != PerPage) menuHv = new float[PerPage];
			Color[] acc = { Color.FromArgb(255, 170, 120), Color.FromArgb(130, 210, 255), Color.FromArgb(190, 160, 255), Color.FromArgb(130, 230, 170), Color.FromArgb(255, 150, 190), Color.FromArgb(255, 214, 110) };
			for (int i = page * PerPage; i < Math.Min(games.Length, (page + 1) * PerPage); i++)
			{
				RectangleF r0 = CardRect(i); MiniGame m = games[i]; int ci = i % PerPage;
				bool hot = r0.Contains(inp.Mx, inp.My); menuHv[ci] += ((hot ? 1f : 0f) - menuHv[ci]) * Math.Min(1f, mdt * 14f); float hv = menuHv[ci];
				RectangleF r = new RectangleF(r0.X, r0.Y - 3f * hv, r0.Width, r0.Height); Color ac = acc[(i * 5 + 1) % acc.Length];
				using (GraphicsPath sp = Gfx.Round(new RectangleF(r.X + 1, r.Y + 5 + 2 * hv, r.Width - 2, r.Height), 20)) using (SolidBrush sb = new SolidBrush(Color.FromArgb((int)(55 + 35 * hv), 4, 6, 24))) gr.FillPath(sb, sp);
				using (GraphicsPath cp = Gfx.Round(r, 20))
				{
					using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(r.X, r.Y - 0.5f, r.Width, r.Height + 1f), Color.FromArgb((int)(64 + 30 * hv), 255, 255, 255), Color.FromArgb((int)(24 + 16 * hv), 200, 215, 255), 90f)) gr.FillPath(lb, cp);
					using (Pen bp = new Pen(Color.FromArgb((int)(60 + 150 * hv), hv > 0.5f ? Color.FromArgb(255, 226, 130) : Color.White), 1.4f)) gr.DrawPath(bp, cp);
				}
				using (GraphicsPath tp = Gfx.Round(new RectangleF(r.X + 22, r.Y + 4, r.Width - 44, 4), 2)) using (SolidBrush tb = new SolidBrush(Color.FromArgb((int)(150 + 90 * hv), ac))) gr.FillPath(tb, tp);
				if (m.Mascot != null) { float mh = 50f + 3f * hv, mw = mh * m.Mascot.Width / m.Mascot.Height; gr.InterpolationMode = InterpolationMode.Bilinear; gr.DrawImage(ImgCache.Get(m.Mascot, mw * paintScale, mh * paintScale), r.X + 16, r.Y + 6 - 1.5f * hv, mw, mh); gr.InterpolationMode = InterpolationMode.HighQualityBicubic; }
				m.Icon(gr, r.X + r.Width - 46, r.Y + 36 - 2f * hv);
				MiniGame.Txt(gr, m.Name, 14f, 2, Color.White, r.X, r.Y + 58, r.Width, 26, StringAlignment.Center);
				MiniGame.Txt(gr, m.Desc, 9.5f, 0, Color.FromArgb(205, 255, 255, 255), r.X + 6, r.Y + 82, r.Width - 12, 22, StringAlignment.Center);
				bool has = best[m.Key] > 0;
				if (!has && (m.Key == "soli" || m.Key == "kart")) { RectangleF nb = new RectangleF(r.X + r.Width / 2f - 21f, r.Y + 12, 42, 18); using (GraphicsPath np = Gfx.Round(nb, 9)) using (SolidBrush nbb = new SolidBrush(Color.FromArgb(235, 255, 86, 110))) gr.FillPath(nbb, np); MiniGame.Txt(gr, "NEW", 8.5f, 2, Color.White, nb.X, nb.Y, nb.Width, nb.Height, StringAlignment.Center); }
				MiniGame.Txt(gr, has ? "★ 최고 " + best[m.Key] + "점" : "아직 기록 없음", 10f, has ? 2 : 0, has ? Color.FromArgb(255, 214, 90) : Color.FromArgb(170, 255, 255, 255), r.X, r.Y + 102, r.Width, 22, StringAlignment.Center);
			}
			if (!hiddenOpen) MiniGame.Txt(gr, "? 비밀 게임 2종 : 모든 게임을 한 번씩 해보면 열려요 (" + playedN + " / " + totalN + ")", 9.5f, 1, Color.FromArgb(190, 255, 226, 140), 0, 502, MiniGame.W, 20, StringAlignment.Center);
			if (Pages > 1) { Btn(gr, PrevBtn, "◀ 이전", false); Btn(gr, NextBtn, "다음 ▶", false); MiniGame.Txt(gr, (page + 1) + " / " + Pages, 11f, 1, Color.FromArgb(200, 255, 255, 255), 0, 528, MiniGame.W, 34, StringAlignment.Center); }
			MiniGame.Txt(gr, "Esc : 닫기  ·  M : 소리", 9.5f, 0, Color.FromArgb(130, 255, 255, 255), 0, MiniGame.H - 28, MiniGame.W, 20, StringAlignment.Center);
			{ bool rh = RankBtn.Contains(inp.Mx, inp.My); Panel(gr, RankBtn, Color.FromArgb(rh ? 90 : 55, 255, 214, 90), 15); gr.FillPolygon(Brushes.Gold, new PointF[] { new PointF(24, 49), new PointF(40, 49), new PointF(38, 59), new PointF(26, 59) }); gr.FillRectangle(Brushes.Gold, 30, 59, 4, 4); gr.FillRectangle(Brushes.Gold, 26, 63, 12, 2); MiniGame.Txt(gr, "랭킹", 10.5f, 2, Color.FromArgb(255, 236, 170), 44, 40, 50, 30, StringAlignment.Near); }
			return;
		}
		g.Draw(gr, tt); g.Fx.Draw(gr);
		// 점수판
			MiniGame.Txt(gr, "점수 " + (state == 2 ? (int)Math.Round(Math.Abs(dispScore - g.Score) < 0.6f ? g.Score : dispScore) : g.Score), 16f + 5f * Math.Max(0f, hudBump), 2, hudBump > 0 ? Color.FromArgb(255, 226, 120) : Color.White, 14, 8, 180, 30, StringAlignment.Near);
		if (state == 2) foreach (float[] fp in floats) { float a = 1f - fp[0] / 0.9f; MiniGame.Txt(gr, (fp[1] > 0 ? "+" : "") + (int)fp[1], 13f, 2, fp[1] > 0 ? Color.FromArgb((int)(255 * a), 255, 226, 120) : Color.FromArgb((int)(255 * a), 255, 130, 140), 112, 30 - fp[0] * 26f, 80, 22, StringAlignment.Near); }
		string rt = g.RightText;
		if (state == 2 && introT <= 0f && !g.NoLimit) { float fr0 = Math.Max(0f, 1f - g.PlayT / 60f); using (SolidBrush tb2 = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) gr.FillRectangle(tb2, 0, 0, MiniGame.W, 4); using (SolidBrush tb2 = new SolidBrush(fr0 > 0.2f ? Color.FromArgb(200, 120, 230, 200) : Color.FromArgb(230, 255, 130, 120))) gr.FillRectangle(tb2, 0, 0, MiniGame.W * fr0, 4); }
		if (state == 2 && rt.Length > 0) MiniGame.Txt(gr, rt, 14f, 2, Color.White, MiniGame.W - 190, 8, 176, 30, StringAlignment.Far);
			if (state == 2 && introT > 0) 			{ 				float ph = (introT * 1000f) % 650f / 650f; float sc = 1f + 0.9f * ph; string s = introNum > 0 ? introNum.ToString() : "시작!"; int a = introNum > 0 ? (int)(255 * (1f - ph * 0.6f)) : 255; 				using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 8, 10, 30))) gr.FillRectangle(b, 0, 190, MiniGame.W, 150); 				MiniGame.Txt(gr, s, 54f * sc / 1.5f, 2, introNum > 0 ? Color.FromArgb(a, 255, 255, 255) : Color.FromArgb(255, 255, 214, 90), 0, 214, MiniGame.W, 100, StringAlignment.Center); 				MiniGame.Txt(gr, g.Name, 13f, 1, Color.FromArgb(220, 255, 255, 255), 0, 302, MiniGame.W, 24, StringAlignment.Center); 			} 			if (state == 2 && finT > 0) 			{ 				float u = Math.Min(1f, (1.25f - finT) / 0.22f); float sc = 2.2f - 1.2f * (1f - (1f - u) * (1f - u)); 				using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 8, 10, 30))) gr.FillRectangle(b, 0, 230, MiniGame.W, 110); 				MiniGame.Txt(gr, "끝!", 40f * sc / 1.5f, 2, Color.FromArgb(255, 255, 214, 90), 0, 244, MiniGame.W, 80, StringAlignment.Center); 			}
		if (state == 2 && paused)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 8, 10, 30))) gr.FillRectangle(b, 0, 0, MiniGame.W, MiniGame.H);
			Card(gr, new RectangleF(50f, 200f, MiniGame.W - 100f, 132f));
			MiniGame.Txt(gr, "일시정지", 24f, 2, Color.White, 0, 214f, MiniGame.W, 44, StringAlignment.Center);
			using (Pen dp = new Pen(Color.FromArgb(120, 255, 205, 80), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) gr.DrawLine(dp, MiniGame.W / 2f - 22f, 262f, MiniGame.W / 2f + 22f, 262f);
			MiniGame.Txt(gr, "화면을 누르거나 아무 키나 누르면 계속해요", 11f, 1, Color.FromArgb(255, 255, 222, 120), 56f, 274f, MiniGame.W - 112f, 40f, StringAlignment.Center, true);
		}
		if (state == 1 || state == 3 || state == 4)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 8, 10, 30))) gr.FillRectangle(b, 0, 0, MiniGame.W, MiniGame.H);
			if (state != lastDrawState) { lastDrawState = state; stSince = tt; }
			float ka = (float)Math.Min(1.0, Math.Max(0.0, (tt - stSince) / 0.45)); ka = 1f - (1f - ka) * (1f - ka) * (1f - ka);
			GraphicsState gAnim = null;   // 결과·랭킹 카드는 흔들림 없이 바로 나와요
			if (state == 1)
			{
				Btn(gr, BackBtn, gated ? "다른 게임" : "게임 메뉴", false);
				HelpCard(gr, g, best[g.Key], tt);
			}
			else if (state == 4)
			{
				Card(gr, new RectangleF(22f, 52f, MiniGame.W - 44f, 408f));
				if (newRecord) { Spr.Draw(gr, "ms_cstar", MiniGame.W / 2f, -140f + (float)((tt * 70) % 760), 300f, 316f, 0f); } if (newRecord) { float cp0 = 1f + 0.06f * (float)Math.Sin(tt * 6); Spr.Draw(gr, "ms_cray", MiniGame.W / 2f, 96f, 330f, 330f, (float)(tt * 25) % 360f); Spr.Draw(gr, "ms_cf", MiniGame.W / 2f, 120f, 340f * cp0, 340f * cp0 * 198f / 300f, 0f); } if (newRecord) { float wob = (float)Math.Sin(tt * 4) * 4f; Spr.Draw(gr, "ms_glow", 56f, 92f + wob, 80f, 80f, wob); Spr.Draw(gr, "ms_glow", MiniGame.W - 56f, 92f - wob, -80f, 80f, -wob); Spr.Draw(gr, "ms_medal", MiniGame.W / 2f, 42f, 34f, 39f, wob * 0.8f); }
				if (newRecord && Spr.Get("ms_ui_medal") != null) Spr.Draw(gr, "ms_ui_medal", MiniGame.W - 66f, 176f, 40f, 49f, (float)Math.Sin(tt * 3) * 6f);
				if (newRecord) Spr.Draw(gr, "ms_rib", MiniGame.W / 2f, 96f, 270f, 127f, 0f); else Spr.Draw(gr, "ms_crn", MiniGame.W / 2f, 40f, 52f, 42f, (float)Math.Sin(tt * 3) * 4f); if (newRecord) MiniGame.Txt(gr, "신기록!", 28f, 2, Color.FromArgb(150, 8, 30, 100), 1.5f, 56.5f, MiniGame.W, 50, StringAlignment.Center); MiniGame.Txt(gr, newRecord ? "신기록!" : "랭킹 진입!", 28f, 2, newRecord ? Color.White : Color.FromArgb(255, 214, 90), 0, newRecord ? 55 : 70, MiniGame.W, 50, StringAlignment.Center);
				MiniGame.Txt(gr, pendScore + "점", 34f, 2, Color.White, 0, 114, MiniGame.W, 54, StringAlignment.Center);
				MiniGame.Txt(gr, g.Name, 11f, 1, Color.FromArgb(180, 255, 255, 255), 0, 166, MiniGame.W, 20, StringAlignment.Center);
				float blink = 0.55f + 0.45f * (float)Math.Sin(tt * 6);
				MiniGame.Txt(gr, "이름을 적어 주세요", 14f, 2, Color.FromArgb((int)(255 * blink), 140, 235, 200), 0, 186, MiniGame.W, 26, StringAlignment.Center);
				// 다른 사람들 기록: 온라인 순위(받아오면) 또는 이 PC의 기록 위에 내 점수가 어디 끼는지 미리 보여 줘요
				{
					bool onl = Rank.On && Rank.Status(g.Key) == 2; List<KeyValuePair<string, int>> src = onl ? Rank.Get(g.Key) : LoadBoard(g.Key);
					MiniGame.Txt(gr, (onl ? "다른 사람들 기록 (온라인 TOP)" : (Rank.On && Rank.Status(g.Key) == 1 ? "다른 사람들 기록 불러오는 중…" : (Rank.On ? "서버에 연결 못 했어요 · 이 PC의 기록" : "이 PC의 기록"))), 9.5f, 1, Color.FromArgb(200, 170, 200, 255), 0, 214, MiniGame.W, 18, StringAlignment.Center);
					List<KeyValuePair<string, int>> pv = new List<KeyValuePair<string, int>>(src); int mi = 0; while (mi < pv.Count && pv[mi].Value >= pendScore) mi++; pv.Insert(mi, new KeyValuePair<string, int>("(나) 이름 입력 중", pendScore));
					for (int i = 0; i < Math.Min(8, pv.Count); i++)
					{
						bool me = i == mi; RectangleF rr = new RectangleF(40f, 234f + i * 17f, MiniGame.W - 80f, 16f);
						if (me) using (GraphicsPath rp = Gfx.Round(rr, 9f)) { using (SolidBrush rb = new SolidBrush(Color.FromArgb(80, 120, 230, 190))) gr.FillPath(rb, rp); using (Pen rpn = new Pen(Color.FromArgb(160, 140, 235, 200), 1f)) gr.DrawPath(rpn, rp); }
						Color tc = me ? Color.FromArgb(150, 245, 210) : (i == 0 ? Color.FromArgb(255, 220, 110) : Color.White);
						MiniGame.Txt(gr, (i + 1).ToString(), 9.5f, 2, i < 3 ? Color.FromArgb(255, 220, 110) : Color.FromArgb(190, 255, 255, 255), rr.X + 4f, rr.Y, 20f, rr.Height, StringAlignment.Center);
						MiniGame.Txt(gr, pv[i].Key, 9.5f, me ? 1 : 0, tc, rr.X + 28f, rr.Y, 150f, rr.Height, StringAlignment.Near);
						MiniGame.Txt(gr, pv[i].Value + "점", 9.5f, 1, tc, rr.Right - 86f, rr.Y, 80f, rr.Height, StringAlignment.Far);
					}
				}
				MiniGame.Txt(gr, "최대 8글자 · Enter 로 등록", 10f, 0, Color.FromArgb(170, 255, 255, 255), 0, 372, MiniGame.W, 20, StringAlignment.Center);
				Btn(gr, OkBtn, "등록하기", true);
				if (Rank.On) MiniGame.Txt(gr, "온라인 랭킹에도 올라가요", 9.5f, 0, Color.FromArgb(140, 255, 226, 140), 0, 448, MiniGame.W, 18, StringAlignment.Center);
			}
			else
			{
				Card(gr, new RectangleF(22f, 96f, MiniGame.W - 44f, 356f));
				if (newRecord && Spr.Get("ms_ch0") == null) { float wob = (float)Math.Sin(tt * 4) * 4f; Spr.Draw(gr, "ms_glow", 62f, 82f + wob, 76f, 76f, wob); Spr.Draw(gr, "ms_glow", MiniGame.W - 62f, 82f - wob, -76f, 76f, -wob); }
				if (Spr.Get("ms_ch0") != null)
				{
					float wob2 = (float)Math.Sin(tt * 4) * 4f; string cp = newRecord ? "ms_ch0" : ((prevBest <= 0 || g.Score * 10 >= prevBest * 5) ? "ms_ch5" : (g.Score <= 0 ? "ms_ch4" : "ms_ch2")); string cq = newRecord ? "ms_ch3" : (cp == "ms_ch5" ? "ms_ch1" : cp);
					if (newRecord) { Spr.Draw(gr, cp, 56f, 92f + wob2, 82f, 84f, 0f); Spr.Draw(gr, cq, MiniGame.W - 56f, 92f - wob2, -82f, 82f, 0f); }
					else { Spr.Draw(gr, cp, 52f, 94f, 70f, 72f, 0f); Spr.Draw(gr, cq, MiniGame.W - 52f, 94f, -70f, 72f, 0f); }
				}
				if (Spr.Get("ms_ui_st1") != null)
				{
					float ratio = newRecord ? 1f : (prevBest > 0 ? g.Score / (float)prevBest : 0.5f); int ns = newRecord ? 3 : (ratio >= 0.7f ? 2 : (ratio >= 0.35f ? 1 : 0));
					for (int si = 0; si < 3; si++) { string sn = si < ns ? "ms_ui_st1" : "ms_ui_st2"; float sc2 = si < ns && newRecord ? 1f + 0.06f * (float)Math.Sin(tt * 6 + si) : 1f; Spr.Draw(gr, sn, MiniGame.W / 2f + (si - 1) * 36f, 30f, 30f * sc2, 29f * sc2, 0f); }
				}
				if (newRecord && Spr.Get("ms_rib") != null) { Spr.Draw(gr, "ms_rib", MiniGame.W / 2f, 112f, 190f, 90f, 0f); MiniGame.Txt(gr, "신기록!", 21f, 2, Color.FromArgb(150, 8, 30, 100), 1f, 82f, MiniGame.W, 40, StringAlignment.Center); MiniGame.Txt(gr, "신기록!", 21f, 2, Color.White, 0, 81f, MiniGame.W, 40, StringAlignment.Center); } else MiniGame.Txt(gr, newRecord ? "신기록!" : "끝!", 24f, 2, newRecord ? Color.FromArgb(255, 214, 90) : Color.White, 0, 108, MiniGame.W, 40, StringAlignment.Center);
				MiniGame.Txt(gr, g.Score + "점", 34f, 2, Color.White, 0, 146, MiniGame.W, 54, StringAlignment.Center);
				{ string sub = newRecord ? (prevBest > 0 ? "이전 최고 " + prevBest + "점 → +" + (g.Score - prevBest) + "점 갱신!" : "첫 기록이에요!") : (prevBest > g.Score ? "최고 기록까지 " + (prevBest - g.Score) + "점 남았어요" : ""); MiniGame.Txt(gr, sub, 11f, 1, Color.FromArgb(255, 255, 214, 90), 0, 200, MiniGame.W, 22, StringAlignment.Center); }
				using (Pen dp = new Pen(Color.FromArgb(60, 190, 215, 255), 1f)) gr.DrawLine(dp, 46f, 232f, MiniGame.W - 46f, 232f);
				MiniGame.Txt(gr, g.Name + "  TOP 5", 10.5f, 1, Color.FromArgb(200, 170, 200, 255), 0, 238, MiniGame.W, 20, StringAlignment.Center);
				for (int i = 0; i < board.Count; i++)
				{
					bool me = board[i].Key == thisKey && board[i].Value == g.Score; RectangleF rr = new RectangleF(38f, 262f + i * 26f, MiniGame.W - 76f, 23f);
					if (me) using (GraphicsPath rp = Gfx.Round(rr, 11f)) { using (SolidBrush rb = new SolidBrush(Color.FromArgb(70, 120, 230, 190))) gr.FillPath(rb, rp); using (Pen rpn = new Pen(Color.FromArgb(150, 140, 235, 200), 1f)) gr.DrawPath(rpn, rp); }
					Color tc = me ? Color.FromArgb(150, 245, 210) : (i == 0 ? Color.FromArgb(255, 220, 110) : Color.White);
					MiniGame.Txt(gr, (i + 1).ToString(), 11f, 2, i < 3 ? Color.FromArgb(255, 220, 110) : Color.FromArgb(190, 255, 255, 255), rr.X + 6f, rr.Y, 22f, rr.Height, StringAlignment.Center);
					MiniGame.Txt(gr, board[i].Key, 11f, (i == 0 || me) ? 1 : 0, tc, rr.X + 34f, rr.Y, 150f, rr.Height, StringAlignment.Near);
					MiniGame.Txt(gr, board[i].Value + "점", 11f, 1, tc, rr.Right - 96f, rr.Y, 90f, rr.Height, StringAlignment.Far);
				}
				if (quipG != g || quipS != g.Score) { quip = Quips.Get(g.Key, g.Score, newRecord); quipG = g; quipS = g.Score; Bond.Game(); }
				MiniGame.Txt(gr, quip, 11f, 1, Color.FromArgb(225, 255, 255, 255), 38, 408, MiniGame.W - 76, 44, StringAlignment.Center, true);
				Btn(gr, ListBtn, gated ? "다음 게임" : "게임 메뉴", false); Btn(gr, AgainBtn, "다시 하기", true);
			}
			if (gAnim != null) gr.Restore(gAnim);
		}
	}
	// 폼 안에 텍스트 상자가 있어 방향키를 창이 먼저 가져가 버려요(포커스 이동용) → 여기서 직접 받아서 게임에 넘겨요
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		Keys k = keyData & Keys.KeyCode;
		if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down)
		{
			if (state == 2 && g != null && !g.NeedsInput) { if (down.Add(k)) g.Press(k); return true; }
			if (state == 0) { if (down.Add(k)) Turn(k == Keys.Left ? -1 : 1); return true; }
			if (state == 5) { if (down.Add(k) && (k == Keys.Left || k == Keys.Right)) RankTurn(k == Keys.Left ? -1 : 1); return true; }
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}
	protected override void Dispose(bool d) { if (d) tm.Stop(); base.Dispose(d); }
}

// 그림을 윈도우 기본 그래픽(GDI) 메모리 비트맵에 그리고, 화면에는 StretchBlt 로 한 번에 붙여요 (GDI+ DrawImage 보다 훨씬 빨라요)
internal sealed class GdiBuf : IDisposable
{
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	private struct BIH { public int Size, Width, Height; public short Planes, BitCount; public int Compression, SizeImage, XPels, YPels, ClrUsed, ClrImportant; }
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BIH bi, uint usage, out IntPtr bits, IntPtr section, uint offset);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr old);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool GdiFlush();
	public readonly int W, H; public readonly Bitmap Bmp; private IntPtr dc, hbm, old;
	public GdiBuf(int w, int h)
	{
		W = w; H = h; BIH bi = new BIH(); bi.Size = 40; bi.Width = w; bi.Height = -h; bi.Planes = 1; bi.BitCount = 32; IntPtr bits;
		dc = CreateCompatibleDC(IntPtr.Zero); if (dc == IntPtr.Zero) throw new InvalidOperationException("dc");
		hbm = CreateDIBSection(dc, ref bi, 0, out bits, IntPtr.Zero, 0); if (hbm == IntPtr.Zero || bits == IntPtr.Zero) { DeleteDC(dc); throw new InvalidOperationException("dib"); }
		old = SelectObject(dc, hbm); Bmp = new Bitmap(w, h, w * 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, bits);
	}
	public bool Blit(IntPtr hdc, int x, int y, int cw, int ch, bool stretch, bool fast)
	{
		GdiFlush(); bool ok;
		if (!stretch) ok = BitBlt(hdc, x, y, cw, ch, dc, 0, 0, 0x00CC0020);
		else { SetStretchBltMode(hdc, fast ? 3 : 4); if (!fast) SetBrushOrgEx(hdc, 0, 0, IntPtr.Zero); ok = StretchBlt(hdc, x, y, cw, ch, dc, 0, 0, W, H, 0x00CC0020); }
		return ok;
	}
	public void Dispose() { try { Bmp.Dispose(); } catch { } if (dc != IntPtr.Zero) { SelectObject(dc, old); DeleteObject(hbm); DeleteDC(dc); dc = IntPtr.Zero; } }
}
