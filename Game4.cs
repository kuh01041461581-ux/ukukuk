using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 파티게임식 새 미니게임 4종 (마리오 파티의 두더지·야바위, 와리오웨어의 5초 게임, 서류 분류) ──────────────

internal static class PF
{
	// 둥근 얼굴: mood 0 웃음 · 1 화남 · 2 울상
	public static void Face(Graphics g, float cx, float cy, float r, Color c, int mood)
	{
		using (SolidBrush b = new SolidBrush(Theme.Mix(c, Color.Black, 0.3))) g.FillEllipse(b, cx - r, cy - r * 0.92f, r * 2, r * 2);
		using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(b, cx - r * 0.55f, cy - r * 0.85f, r * 0.7f, r * 0.4f);
		float es = Math.Max(2.2f, r * 0.12f), ey = cy - r * 0.12f;
		using (SolidBrush e = new SolidBrush(Color.FromArgb(40, 30, 40)))
		{
			g.FillEllipse(e, cx - r * 0.4f - es, ey - es, es * 2, es * 2.3f); g.FillEllipse(e, cx + r * 0.4f - es, ey - es, es * 2, es * 2.3f);
		}
		using (Pen p = new Pen(Color.FromArgb(40, 30, 40), Math.Max(1.6f, r * 0.08f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		{
			if (mood == 1)
			{
				g.DrawLine(p, cx - r * 0.62f, ey - r * 0.38f, cx - r * 0.18f, ey - r * 0.16f); g.DrawLine(p, cx + r * 0.62f, ey - r * 0.38f, cx + r * 0.18f, ey - r * 0.16f);
				g.DrawArc(p, cx - r * 0.3f, cy + r * 0.28f, r * 0.6f, r * 0.4f, 200, 140);
			}
			else if (mood == 2) g.DrawArc(p, cx - r * 0.3f, cy + r * 0.34f, r * 0.6f, r * 0.4f, 200, 140);
			else g.DrawArc(p, cx - r * 0.34f, cy + r * 0.12f, r * 0.68f, r * 0.5f, 20, 140);
		}
	}
	public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
	public static float Cl(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
}

// ── 17. 민원 반려 ─────────────────────────────────────────
internal sealed class MoleGame : MiniGame
{
	private class Mo { public int Cell, Kind, Tag; public float Age, Life, HitT; public bool Hit; }
	private readonly List<Mo> mos = new List<Mo>();
	private double left, spawn, el; private int combo; private float mT, mx = W / 2f, my = 300f, swing, popT; private string pop = ""; private PointF popAt; private float slT, slX, slY, impT, impX, impY; private int impK;
	public override string Costume { get { return "g_mole"; } }
	public override string Name { get { return "민원 반려!"; } }
	public override string Key { get { return "mole"; } }
	public override string Desc { get { return "쏟아지는 민원을 반려해요"; } }
	public override string Help { get { return "민원 창구에서 이름표(반복 민원·책임자 나와·폭언 민원…)를 단 민원인이 튀어나와요 → 클릭해서 '반려' 도장을 쾅! (+10, 빨간 이름표는 +5 · 연속이면 보너스)\n담당자 마룡이는 반려하면 안 돼요 (-15)\n금빛 '퇴근!'은 +30점에 시간 +2초 · 40초 승부"; } }
	public override string RightText { get { return Math.Ceiling(left) + "초"; } }
	public override void Reset() { mos.Clear(); left = 40; spawn = 0.4; el = 0; combo = 0; Score = 0; Over = false; swing = 0; pop = ""; popT = 0; }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 56, 34))) g.FillEllipse(b, cx - 20, cy + 2, 40, 16);
		PF.Face(g, cx, cy - 2, 12, Color.FromArgb(236, 120, 72), 1);
	}
	private static PointF Hole(int i) { return new PointF(80f + (i % 3) * 120f, (Spr.Get("bg_mole") != null ? 248f + (i / 3) * 84f : 235f + (i / 3) * 105f)); }
	private static readonly string[] TagN = { "반복 민원", "책임자 나와", "환불해줘", "정보공개 청구", "폭언 민원", "기한 독촉", "담당자 교체", "SNS 폭로" };
	private static readonly int[] TagK = { 0, 1, 0, 2, 1, 2, 0, 1 };   // 0 일반(파랑) · 1 악성(빨강, 빨리 숨고 +5) · 2 서류형(초록)
	private static void TagPlate(Graphics g, Mo m, float x, float y)
	{
		int k = TagK[m.Tag]; Color c = k == 1 ? Color.FromArgb(214, 70, 70) : (k == 2 ? Color.FromArgb(60, 160, 110) : Color.FromArgb(70, 110, 200)); float w = 14f + TagN[m.Tag].Length * 9f;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(x - w / 2f, y, w, 17f), 7)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 255, 245))) g.FillPath(b, gp); using (Pen p = new Pen(c, 2f)) g.DrawPath(p, gp); }
		using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, x - w / 2f + 4f, y + 3f, 3f, 11f);
		Txt(g, TagN[m.Tag], 8.5f, 2, Color.FromArgb(40, 40, 60), x - w / 2f + 6f, y, w - 6f, 17f, StringAlignment.Center);
	}
	private static float Rise(Mo m)
	{
		if (m.Hit) return 1f;
		if (m.Age < 0.12f) { float u = m.Age / 0.12f; return 1.08f * (1f - (1f - u) * (1f - u)) - 0.08f * u; }   // 튀어나올 땐 살짝 오버슈트
		if (m.Age > m.Life - 0.15f) return Math.Max(0f, (m.Life - m.Age) / 0.15f);
		return 1f;
	}
	private static float CritterY(PointF h, float rise) { return h.Y + 46f - 70f * rise; }
	public override void Click(float x, float y)
	{
		swing = 1f; mT = 0.4f;
		int bi = -1; float bd = 1e9f;   // 겹쳐 있으면 가장 가까운 쪽을 때려요
		for (int i = mos.Count - 1; i >= 0; i--) { Mo m0 = mos[i]; if (m0.Hit) continue; PointF h0 = Hole(m0.Cell); float r0 = Rise(m0), cy0 = CritterY(h0, r0), ex = x - h0.X, ey = y - cy0, d0 = ex * ex + ey * ey; if (r0 > 0.25f && d0 < 50f * 50f && d0 < bd) { bd = d0; bi = i; } }
		if (bi >= 0)
		{
			{ int i = bi;
			Mo m = mos[i];
			PointF h = Hole(m.Cell); float rise = Rise(m); float cy = CritterY(h, rise);
			{
				m.Hit = true; m.HitT = 0.28f; impT = 0.26f; impX = h.X; impY = cy - 6f; impK = m.Kind;
				if (m.Kind == 0)
				{
					combo++; int pts = 10 + Math.Min(combo / 3, 6) * 2 + (TagK[m.Tag] == 1 ? 5 : 0); Score += pts; Pop("반려! +" + pts, h.X, cy - 40f);
					Sfx.Play("thud2"); Fx.Burst(h.X, cy, Color.FromArgb(255, 170, 80), 8); Shake = Math.Max(Shake, 0.1f + Math.Min(0.1f, combo * 0.01f));
					if (combo % 5 == 0) { Sfx.Play("ok2"); Shake = 0.2f; }
				}
				else if (m.Kind == 1) { Score = Math.Max(0, Score - 15); combo = 0; Shake = 0.35f; Pop("-15", h.X, cy - 40f); slT = 1.1f; slX = h.X; slY = h.Y - 8f; Sfx.Play("bad2"); Fx.Burst(h.X, cy, Color.FromArgb(150, 170, 230), 6); }
				else { Score += 30; left += 2; Pop("+30 · 시간 +2초", h.X, cy - 40f); Sfx.Play("fanfare"); Fx.Burst(h.X, cy, Color.FromArgb(255, 214, 80), 14); Shake = 0.2f; }
				return;
			}
			}
		}
		Sfx.Play("swing");
	}
	public override void Press(Keys k) { if (k == Keys.Space) Click(mx, my); }
	private void Pop(string s, float x, float y) { pop = s; popT = 0.8f; popAt = new PointF(x, y); }
	public override void Update(float dt, GameInput inp, double t)
	{
		mx = inp.Mx; my = inp.My;
		left -= dt; if (left > 60 - PlayT) left = 60 - PlayT; el += dt; if (left <= 0) { left = 0; Over = true; return; }
		if (swing > 0) swing = Math.Max(0f, swing - dt * 6f); if (mT > 0f) mT = Math.Max(0f, mT - dt); if (popT > 0) popT -= dt;
		float life = (float)Math.Max(el > 35 ? 0.48 : 0.65, 1.25 - el * 0.014 - Math.Max(0, el - 35) * 0.012);
		for (int i = mos.Count - 1; i >= 0; i--)
		{
			Mo m = mos[i]; m.Age += dt;
			if (m.Hit) { m.HitT -= dt; if (m.HitT <= 0f) mos.RemoveAt(i); }
			else if (m.Age >= m.Life) { if (m.Kind == 0) combo = 0; mos.RemoveAt(i); }
		}
		spawn -= dt; int maxAct = el < 12 ? 2 : (el < 26 ? 3 : (el < 40 ? 4 : 5));
		if (spawn <= 0 && mos.Count < maxAct)
		{
			List<int> free = new List<int>(); for (int c = 0; c < 9; c++) { bool used = false; foreach (Mo m in mos) if (m.Cell == c) used = true; if (!used) free.Add(c); }
			if (free.Count > 0)
			{
				double r = Rnd.NextDouble(); int kind = r < (el > 30 ? 0.22 : 0.14) ? 1 : (r < (el > 30 ? 0.27 : 0.20) ? 2 : 0);
				int tg = Rnd.Next(TagN.Length); mos.Add(new Mo { Cell = free[Rnd.Next(free.Count)], Kind = kind, Life = TagK[tg] == 1 && kind == 0 ? life * 0.8f : life, Tag = tg });
			}
			spawn = Math.Max(0.32, 0.8 - el * 0.012) * (0.7 + Rnd.NextDouble() * 0.6);
		}
	}
	private static readonly string[] MoFr = { "ms_m_mo1", "ms_m_mo2", "ms_m_mo1", "ms_m_mo3" };
	private bool MoleSprite(Graphics g, Mo m, PointF h, float cy)
	{
		Bitmap sp = Spr.Get(m.Hit ? "ms_m_mo4" : MoFr[(int)(m.Age * 4f) % 4]); if (sp == null) return false;
		float w = m.Hit ? 104f : 96f, full = w * sp.Height / sp.Width, vis = full * 0.68f; float sc = ImgCache.Scale(g);
		System.Drawing.Image im = ImgCache.Get(sp, w * sc, full * sc);
		g.DrawImage(im, new RectangleF(h.X - w / 2f, cy - 36f, w, vis), new RectangleF(0, 0, im.Width, im.Height * 0.68f), GraphicsUnit.Pixel); return true;
	}
	private void Critter(Graphics g, Mo m, PointF h, float rise, double t)
	{
		float cy = CritterY(h, rise); float r = 33f;
		GraphicsState s = g.Save();
		if (m.Hit) { g.TranslateTransform(h.X, cy + 8f); g.ScaleTransform(1.18f, 0.72f); g.TranslateTransform(-h.X, -(cy + 8f)); }
		if (m.Kind == 1) { DrawMascot(g, h.X, cy + 40f, 82f, (float)Math.Sin(t * 8) * 3f); }
		else if (m.Kind == 0 && MoleSprite(g, m, h, cy)) { }
		else if (m.Kind == 2 && Spr.Get("ms_coins") != null) { float bob2 = (float)Math.Sin(t * 9) * 2f; Spr.Draw(g, "ms_coins", h.X, cy + 24f + bob2, 66f, 66f * 0.84f, (float)Math.Sin(t * 6) * 6f); Txt(g, "퇴근!", 10f, 2, Color.FromArgb(120, 70, 10), h.X - 30, cy + 52f, 60, 18, StringAlignment.Center); }
		else
		{
			Color body = m.Kind == 2 ? Color.FromArgb(240, 184, 40) : Color.FromArgb(214, 96, 70);
			using (GraphicsPath gp = Gfx.Round(new RectangleF(h.X - r * 0.9f, cy + 4f, r * 1.8f, 70f), 18)) using (SolidBrush b = new SolidBrush(Theme.Mix(body, Color.Black, 0.2))) g.FillPath(b, gp);
			PF.Face(g, h.X, cy, r, m.Kind == 2 ? Color.FromArgb(255, 212, 80) : Color.FromArgb(240, 128, 84), m.Kind == 2 ? 0 : 1);
			if (m.Kind == 2) Txt(g, "퇴근!", 10f, 2, Color.FromArgb(120, 70, 10), h.X - 30, cy + 22f, 60, 18, StringAlignment.Center);
		}
		g.Restore(s);
		if (m.Hit)
			for (int k = 0; k < 3; k++) { double a = t * 9 + k * 2.1; StarShape(g, h.X + (float)Math.Cos(a) * 26f, cy - 34f + (float)Math.Sin(a) * 8f, 6f, Color.FromArgb(255, 226, 90)); }
	}
	// 구멍 뒷부분(그림자·구멍)과 앞부분(흙더미·풀)은 모든 구멍이 똑같아서 한 번만 그려 두고 재사용해요
	private Bitmap holeBack, holeFront; private float holeK;
	private void HoleArt(Graphics g, PointF h, bool back)
	{
		float k = ImgCache.Scale(g);
		if (holeBack == null || Math.Abs(holeK - k) > 0.01f)
		{
			if (holeBack != null) { holeBack.Dispose(); holeFront.Dispose(); }
			holeK = k; holeBack = new Bitmap((int)Math.Ceiling(112 * k), (int)Math.Ceiling(46 * k), System.Drawing.Imaging.PixelFormat.Format32bppPArgb); holeFront = new Bitmap((int)Math.Ceiling(116 * k), (int)Math.Ceiling(40 * k), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics g2 = Graphics.FromImage(holeBack))
			{
				g2.SmoothingMode = SmoothingMode.AntiAlias; g2.ScaleTransform(holeBack.Width / 112f, holeBack.Height / 46f); g2.TranslateTransform(56f, 14f);
				using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g2.FillEllipse(b, -54, -8, 108, 36);
				using (GraphicsPath hp = new GraphicsPath()) { hp.AddEllipse(-48, -12, 96, 30); using (PathGradientBrush pb = new PathGradientBrush(hp)) { pb.CenterPoint = new PointF(0, 6); pb.CenterColor = Color.FromArgb(18, 10, 6); pb.SurroundColors = new Color[] { Color.FromArgb(74, 44, 26) }; g2.FillPath(pb, hp); } }
			}
			using (Graphics g2 = Graphics.FromImage(holeFront))
			{
				g2.SmoothingMode = SmoothingMode.AntiAlias; g2.ScaleTransform(holeFront.Width / 116f, holeFront.Height / 40f); g2.TranslateTransform(58f, 6f);
				using (SolidBrush b = new SolidBrush(Color.FromArgb(104, 66, 38))) g2.FillEllipse(b, -52, -2, 104, 26);
				using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 100, 60))) g2.FillEllipse(b, -50, -4, 100, 20);
				using (Pen rp = new Pen(Color.FromArgb(90, 255, 220, 170), 2f)) g2.DrawArc(rp, -48, -3, 96, 14, 20, 140);
				using (Pen tp = new Pen(Color.FromArgb(120, 40, 110, 60), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) for (int q = 0; q < 7; q++) { float tx = -56 + q * 18.6f, ty = 12 + (q % 2) * 4; g2.DrawLine(tp, tx, ty, tx - 3, ty - 8); g2.DrawLine(tp, tx, ty, tx + 3, ty - 9); }
			}
		}
		InterpolationMode im0 = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Bilinear;
		if (back) g.DrawImage(holeBack, new RectangleF(h.X - 56f, h.Y - 14f, 112f, 46f), new RectangleF(0, 0, holeBack.Width, holeBack.Height), GraphicsUnit.Pixel);
		else g.DrawImage(holeFront, new RectangleF(h.X - 58f, h.Y - 6f, 116f, 40f), new RectangleF(0, 0, holeFront.Width, holeFront.Height), GraphicsUnit.Pixel);
		g.InterpolationMode = im0;
	}
		public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_mole", Color.FromArgb(96, 178, 112), Color.FromArgb(52, 126, 82));
		if (Spr.Get("bg_mole") == null)
		{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) { g.FillEllipse(b, 30, 96, 130, 26); g.FillEllipse(b, 250, 120, 120, 24); }
		using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 255, 250, 200))) g.FillPolygon(b, new PointF[] { new PointF(330, 0), new PointF(400, 0), new PointF(240, 330), new PointF(130, 330) });
		using (SolidBrush b = new SolidBrush(Color.FromArgb(24, 255, 250, 200))) g.FillPolygon(b, new PointF[] { new PointF(260, 0), new PointF(290, 0), new PointF(120, 300), new PointF(70, 300) });
		}
		for (int i = 0; i < 9; i++)
		{
			PointF h = Hole(i);
			HoleArt(g, h, true);
			foreach (Mo m in mos)
			{
				if (m.Cell != i) continue;
				GraphicsState s = g.Save(); g.SetClip(new RectangleF(h.X - 70, h.Y - 130, 140, 130 + 8));
				Critter(g, m, h, Rise(m), t); g.Restore(s);
				if (m.Age < 0.25f && !m.Hit) { float dk = m.Age / 0.25f; using (SolidBrush db = new SolidBrush(Color.FromArgb((int)(190 * (1f - dk)), 120, 80, 48))) for (int q = 0; q < 6; q++) { float ang = q * 1.05f; g.FillEllipse(db, h.X + (float)Math.Cos(ang) * (22 + 30 * dk) - 3, h.Y - 4 - dk * 22 - (float)Math.Abs(Math.Sin(ang)) * 10 * dk, 6, 6); } }
				if (m.Hit && m.HitT > 0f) { float rk = 1f - m.HitT / 0.28f; using (Pen rp2 = new Pen(Color.FromArgb((int)(200 * (1f - rk)), 255, 244, 190), 3f)) g.DrawEllipse(rp2, h.X - 20 - rk * 40, h.Y - 70 - rk * 20, 40 + rk * 80, 40 + rk * 40); }
			}
			HoleArt(g, h, false);
			foreach (Mo m2 in mos) if (m2.Cell == i && m2.Kind == 0 && !m2.Hit && Rise(m2) > 0.95f) TagPlate(g, m2, h.X, h.Y - 20f);
		}
		if (slT > 0f) { slT -= 1f / 60f; DrawFx(g, "slime", slX, slY, 74f + 14f * (1.1f - slT), Math.Min(1f, slT * 1.6f)); }
		if (popT > 0) Txt(g, pop, 15f, 2, Color.FromArgb((int)(255 * Math.Min(1f, popT * 2f)), pop[0] == '-' ? Color.FromArgb(255, 150, 160) : Color.FromArgb(255, 238, 130)), popAt.X - 80, popAt.Y - (0.8f - popT) * 40f - 12f, 160, 26, StringAlignment.Center);
		if (combo >= 3) Txt(g, combo + " 연속!", 12f, 2, Color.FromArgb(255, 238, 130), 14, 44, 180, 22, StringAlignment.Near);
		// 충격 효과: 퍼지는 링 + 별 모양 선
		if (impT > 0f)
		{
			impT -= 1f / 60f; float ip = 1f - impT / 0.26f; int ia = Math.Max(0, Math.Min(255, (int)(230 * (1f - ip)))); Color ic = impK == 1 ? Color.FromArgb(ia, 170, 190, 255) : (impK == 2 ? Color.FromArgb(ia, 255, 220, 90) : Color.FromArgb(ia, 255, 240, 200));
			using (Pen p = new Pen(ic, 4f * (1f - ip) + 1f)) { float rr = 12f + ip * 40f; g.DrawEllipse(p, impX - rr, impY - rr, rr * 2, rr * 2); for (int q = 0; q < 8; q++) { float an = q * 0.785f + 0.3f; g.DrawLine(p, impX + (float)Math.Cos(an) * (rr * 0.6f), impY + (float)Math.Sin(an) * (rr * 0.6f), impX + (float)Math.Cos(an) * (rr + 8f), impY + (float)Math.Sin(an) * (rr + 8f)); } }
		}
		// 망치 (그림이 있으면 그림으로, 없으면 직접 그려요)
		if (!DrawMallet(g))
		{
			GraphicsState hs = g.Save(); g.TranslateTransform(mx, my + swing * 6f); g.RotateTransform(42f - swing * 82f);
			using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, -22f, 54f, 44f, 10f);
			using (GraphicsPath hp = Gfx.Round(new RectangleF(-4.5f, -2f, 9f, 70f), 4)) { using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(-5f, 0f, 10f, 70f), Color.FromArgb(206, 150, 84), Color.FromArgb(140, 92, 48), 0f)) g.FillPath(lb, hp); using (Pen p = new Pen(Color.FromArgb(120, 70, 40, 20), 1.2f)) g.DrawPath(p, hp); }
			using (GraphicsPath gp2 = Gfx.Round(new RectangleF(-5.5f, 40f, 11f, 28f), 4)) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 60, 70))) g.FillPath(b, gp2);
			using (Pen p = new Pen(Color.FromArgb(110, 150, 150, 160), 1.4f)) for (int k = 0; k < 4; k++) g.DrawLine(p, -5.5f, 44f + k * 6f, 5.5f, 47f + k * 6f);
			using (GraphicsPath hd = Gfx.Round(new RectangleF(-27f, -26f, 54f, 32f), 9))
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(-27f, -26f, 54f, 32f), Color.FromArgb(246, 118, 108), Color.FromArgb(196, 62, 62), 90f)) g.FillPath(lb, hd);
				using (Pen p = new Pen(Color.FromArgb(150, 90, 30, 40), 1.8f)) g.DrawPath(p, hd);
			}
			using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 214, 220, 230))) { g.FillRectangle(b, -27f, -22f, 7f, 24f); g.FillRectangle(b, 20f, -22f, 7f, 24f); }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(b, -16f, -22f, 28f, 7f);
			g.Restore(hs);
		}
	}
	private bool DrawMallet(Graphics g)
	{
		if (Spr.Get("ms_hm0") == null) return false;
		float e = 0.4f - mT; int fi; float lift;
		if (mT <= 0f) { fi = 0; lift = 34f + (float)Math.Sin(el * 3.0) * 1.5f; }
		else if (e < 0.05f) { fi = 1; lift = 34f * (1f - e / 0.05f) + 10f; }
		else if (e < 0.16f) { fi = 2; lift = 0f; }
		else if (e < 0.30f) { fi = 3; lift = 22f * ((e - 0.16f) / 0.14f); }
		else { fi = 0; lift = 22f + 12f * ((e - 0.30f) / 0.10f); }
		float[] fx = { 0.50f, 0.68f, 0.62f, 0.70f }, fy = { 0.22f, 0.72f, 0.80f, 0.25f };
		string nm = "ms_hm" + fi; Bitmap b = Spr.Get(nm); float h = 118f, w = h * b.Width / b.Height; if (fi == 2) { h = 112f; w = h * b.Width / b.Height; }
		using (SolidBrush sh = new SolidBrush(Color.FromArgb((int)Math.Max(10f, 70f - lift * 1.5f), 0, 0, 0))) g.FillEllipse(sh, mx - 26f, my + 4f, 52f, 11f);
		Spr.Draw(g, nm, mx + (0.5f - fx[fi]) * w, my - lift + (0.5f - fy[fi]) * h, w, h, 0f); return true;
	}
}

// ── 18. 도장 찾기 (야바위) ──────────────────────────────────
internal sealed class ShellGame : MiniGame
{
	private int colOff;
	private class Cup { public int Slot, Id; public float X, Y, Lf; public bool Up; }
	private readonly List<Cup> cups = new List<Cup>();
	private int n = 3, target, round, lives, streak, phase, swaps, sa, sb, lastA = -1, lastB = -1, chosen; private float phT, st, swDur; private string msg = "";
	private const float BaseY = 392f;
	public override string Costume { get { return "정장"; } }
	public override string Name { get { return "도장 찾기"; } }
	public override string Key { get { return "shell"; } }
	public override string Desc { get { return "서류철 밑 도장을 찾아라"; } }
	public override string Help { get { return "도장이 어느 서류철 밑에 있는지 잘 보고 있다가\n서류철은 전부 같은 색이라 색으로 못 찾아요 — 눈으로 따라가세요\n멈추면 도장이 있는 서류철을 클릭! (기회 3번)\n판이 올라갈수록 빨라지고 5판부터는 4개가 돼요"; } }
	public override string RightText { get { return "기회 " + lives; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 170, 160))) g.FillPolygon(b, new PointF[] { new PointF(cx - 18, cy + 14), new PointF(cx - 12, cy - 14), new PointF(cx + 12, cy - 14), new PointF(cx + 18, cy + 14) });
		using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 70, 70))) g.FillEllipse(b, cx - 6, cy + 4, 12, 12);
	}
	public override void Reset() { round = 0; lives = 3; streak = 0; Score = 0; Over = false; msg = ""; Setup(); }
	private float SlotX(int slot) { return n == 3 ? 90f + slot * 110f : 64f + slot * 90.7f; }
	private void Setup()
	{
		n = round >= 4 ? 4 : 3; cups.Clear(); colOff = round;
		for (int i = 0; i < n; i++) cups.Add(new Cup { Slot = i, Id = i, X = SlotX(i), Y = 0f, Lf = 0f, Up = false });
		target = Rnd.Next(n); cups[target].Up = true; phase = 0; phT = 0f; chosen = -1; lastA = lastB = -1;
	}
	private void NewPair()
	{
		int a, b; do { a = Rnd.Next(n); b = Rnd.Next(n); } while (a == b || (a == lastA && b == lastB) || (a == lastB && b == lastA));
		lastA = a; lastB = b; sa = a; sb = b; st = 0f;
	}
	public override void Click(float x, float y)
	{
		if (phase != 3) return;
		float cw = n == 3 ? 84f : 72f;
		for (int i = 0; i < cups.Count; i++)
		{
			Cup c = cups[i];
			if (x >= c.X - cw / 2 - 8 && x <= c.X + cw / 2 + 8 && y >= BaseY - 112f && y <= BaseY + 14f)
			{
				chosen = i; phase = 4; phT = 0f; c.Up = true; cups[target].Up = true;
				if (i == target)
				{
					streak++; int pts = 20 + round * 5 + Math.Min(streak, 5) * 5; Score += pts; round++; msg = "접수 완료! +" + pts + (streak >= 2 ? " · " + streak + "연속" : "");
					Sfx.Play("ok2"); Fx.Burst(c.X, BaseY - 60f, Color.FromArgb(255, 214, 90), 12);
				}
				else { lives--; streak = 0; msg = "반송! 여기 있었어요"; Sfx.Play("bad2"); Shake = 0.25f; }
				return;
			}
		}
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		float cw0 = n == 3 ? 84f : 72f; foreach (Cup c in cups) { float tgt = c.Up ? 1f : 0f; if (phase == 3 && Math.Abs(inp.Mx - c.X) < cw0 / 2 + 8 && inp.My > BaseY - 112f && inp.My < BaseY + 14f) tgt = 0.12f; c.Lf += (tgt - c.Lf) * Math.Min(1f, dt * 9f); }
		phT += dt;
		if (phase == 0) { if (phT > 1.4f) { foreach (Cup c in cups) c.Up = false; phase = 1; phT = 0f; } }
		else if (phase == 1)
		{
			if (phT > 0.55f) { phase = 2; swaps = Math.Min(26, 5 + round * 2 + Math.Max(0, round - 7)); swDur = (float)Math.Max(0.11, 0.46 - 0.04 * round - Math.Max(0, round - 6) * 0.012); NewPair(); }
		}
		else if (phase == 2)
		{
			st += dt / swDur;
			Cup A = null, B = null; foreach (Cup c in cups) { if (c.Slot == sa) A = c; if (c.Slot == sb) B = c; }
			float k = Math.Min(1f, st); float e = k * k * (3f - 2f * k);
			A.X = PF.Lerp(SlotX(sa), SlotX(sb), e); B.X = PF.Lerp(SlotX(sb), SlotX(sa), e); float arc = Math.Min(40f, 14f + Math.Abs(SlotX(sa) - SlotX(sb)) * 0.14f); A.Y = -(float)Math.Sin(Math.PI * e) * arc; B.Y = (float)Math.Sin(Math.PI * e) * arc * 0.7f;
			if (st >= 1f)
			{
				A.Slot = sb; B.Slot = sa; A.X = SlotX(A.Slot); B.X = SlotX(B.Slot); A.Y = B.Y = 0f; swaps--;
				if (swaps <= 0) { phase = 3; phT = 0f; } else NewPair();
			}
		}
		else if (phase == 4) { if (phT > 1.5f) { if (lives <= 0) Over = true; else Setup(); } }
	}
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_desk", Color.FromArgb(24, 34, 70), Color.FromArgb(56, 76, 130)); if (Spr.Get("bg_desk") == null) Twinkle(g, t);
		using (SolidBrush cb = new SolidBrush(Color.FromArgb(70, 120, 40, 70))) { g.FillRectangle(cb, 0, 0, 30, BaseY); g.FillRectangle(cb, W - 30, 0, 30, BaseY); }
		using (Pen cp = new Pen(Color.FromArgb(40, 255, 200, 220), 2f)) for (int k = 0; k < 4; k++) { g.DrawLine(cp, 6 + k * 7, 0, 6 + k * 7, BaseY); g.DrawLine(cp, W - 6 - k * 7, 0, W - 6 - k * 7, BaseY); }
		using (SolidBrush sp = new SolidBrush(Color.FromArgb(phase == 2 ? 26 : 38, 255, 240, 190))) g.FillPolygon(sp, new PointF[] { new PointF(150, 0), new PointF(250, 0), new PointF(W - 20, BaseY), new PointF(20, BaseY) });
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, BaseY, W, H - BaseY), Color.FromArgb(150, 100, 62), Color.FromArgb(104, 66, 40), 90f)) g.FillRectangle(lb, 0, BaseY, W, H - BaseY);
		using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 2f)) g.DrawLine(p, 0, BaseY, W, BaseY);
		string top = phase == 0 ? "도장 위치를 기억하세요!" : (phase == 1 ? "…" : (phase == 2 ? "눈으로 따라가세요!" : (phase == 3 ? "도장은 어느 서류철 밑에?" : msg)));
		Txt(g, top, 15f, 2, phase == 4 ? (chosen == target ? Color.FromArgb(255, 226, 120) : Color.FromArgb(255, 150, 160)) : Color.White, 0, 130, W, 34, StringAlignment.Center);
		Txt(g, (round + 1) + "번째 판" + (n == 4 ? " · 서류철 4개" : ""), 11f, 1, Color.FromArgb(180, 255, 255, 255), 0, 100, W, 22, StringAlignment.Center);
		float cw = n == 3 ? 84f : 72f;
		// 도장 (서류철이 올라갔을 때만 보여요)
		Cup tc = cups.Count > target ? cups[target] : null;
		if (tc != null)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, tc.X - 26, BaseY - 6, 52, 14);
			if (Spr.Get("ms_fo_stamp") != null) Spr.Draw(g, "ms_fo_stamp", tc.X, BaseY - 31f + tc.Y, 46f, 57f, 0f); else {
			using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 64, 64))) g.FillEllipse(b, tc.X - 24, BaseY - 46 + tc.Y, 48, 48);
			using (Pen p = new Pen(Color.FromArgb(255, 235, 235), 2f)) g.DrawEllipse(p, tc.X - 19, BaseY - 41 + tc.Y, 38, 38);
			Txt(g, "결재", 11f, 2, Color.White, tc.X - 24, BaseY - 46 + tc.Y, 48, 48, StringAlignment.Center); }
		}
		List<Cup> order = new List<Cup>(cups); order.Sort(delegate(Cup a, Cup b) { return a.Y.CompareTo(b.Y); });
		foreach (Cup c in order)
		{
			float lift = c.Lf * 84f; float x = c.X - cw / 2f, y = BaseY - 100f + c.Y - lift;
			if (phase == 3 && (float)Math.Sin(t * 4) > 0.2f) using (SolidBrush hb = new SolidBrush(Color.FromArgb(18, 255, 255, 200))) g.FillEllipse(hb, c.X - cw / 2 - 6, BaseY - 106, cw + 12, 116);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, c.X - cw / 2, BaseY - 4, cw, 12);
			{ string fn = "ms_fo" + (colOff % 4); Bitmap fb = Spr.Get(fn); if (fb != null) { float fw = cw * 1.16f, fh = fw * fb.Height / fb.Width; Spr.Draw(g, fn, c.X, y + 104f - fh / 2f + 2f, fw, fh, 0f); continue; } }
			using (GraphicsPath gp = Gfx.Round(new RectangleF(x, y, cw, 104f), 12))
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(x, y, cw, 104f), Color.FromArgb(96, 196, 186), Color.FromArgb(38, 144, 138), 90f)) g.FillPath(lb, gp);
				using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1.6f)) g.DrawPath(p, gp);
			}
			using (GraphicsPath tab = Gfx.Round(new RectangleF(x + 8, y - 8, cw * 0.42f, 14), 5)) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 170, 160))) g.FillPath(b, tab);
			using (GraphicsPath lbl = Gfx.Round(new RectangleF(x + cw * 0.2f, y + 30, cw * 0.6f, 30), 6)) using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillPath(b, lbl);
			using (Pen p = new Pen(Color.FromArgb(120, 100, 110, 120), 2f)) { g.DrawLine(p, x + cw * 0.28f, y + 40, x + cw * 0.72f, y + 40); g.DrawLine(p, x + cw * 0.28f, y + 50, x + cw * 0.6f, y + 50); }
		}
		if (phase == 4 && chosen == target && cups.Count > target && Spr.Get("ms_fo_paper") != null) { float pk0 = Math.Min(1f, phT / 0.35f); DrawFx(g, "fo_paper", cups[target].X, BaseY - 150f, 70f + 30f * (1f - pk0), pk0); }
		if (phase == 4 && chosen >= 0 && chosen < cups.Count) { float pk = Math.Min(1f, phT / 0.6f); Cup cc = cups[target]; for (int q = 0; q < 8; q++) { float an = q * 0.785f; using (SolidBrush sb2 = new SolidBrush(Color.FromArgb((int)(200 * (1f - pk)), chosen == target ? Color.FromArgb(255, 220, 100) : Color.FromArgb(200, 120, 130)))) g.FillEllipse(sb2, cc.X + (float)Math.Cos(an) * (20 + pk * 50) - 3, BaseY - 40 + (float)Math.Sin(an) * (20 + pk * 50) - 3, 6, 6); } }
		DrawMascot(g, W - 56, H - 14, 76, (float)Math.Sin(t * 3) * 3);
		if (streak >= 2 && phase != 4) Txt(g, streak + "연속 정답!", 11f, 1, Color.FromArgb(255, 214, 90), 14, 44, 180, 22, StringAlignment.Near);
	}
}

// ── 19. 5초 챌린지 (와리오웨어식) ────────────────────────────
internal sealed class MicroGame : MiniGame
{
	private int kind = -1, lastKind = -1, phase, round, lives = 3, streak, need, got, cnt, ans, showN; private bool win; private float phT, tl, dur, mxp, mp, zone, zw, sx, sy, svx, svy, px; private string word = "";
	private readonly List<float[]> rocks = new List<float[]>();   // x, y, v, delay
	private readonly List<float[]> coins = new List<float[]>();   // x, y, showAt
	private MTask[] tasks = MTask.All(); private MTask cur;
	private float starT, tvx, tvy, missT, mxc, myc; private int sad; private float gpx, gpy; private int cols, rows; private float cell;
	private static readonly string[] Words = { "잡아라!", "피해라!", "멈춰라!", "골라라!", "연타!", "세어라!" };
	public override string Costume { get { return "음악"; } }
	public override string Name { get { return "5초 챌린지"; } }
	public override string Key { get { return "micro"; } }
	public override string Desc { get { return "짧은 미션을 연달아 성공!"; } }
	public override string Help { get { return "'잡아라! 피해라! 멈춰라! 골라라! 연타! 세어라! 더 큰 수! 다른 색! 쾅 찍어라! 초록불에! 차례대로! 전화 받기! 메뉴 룰렛! 오탈자! 계산! 방향! 찾아라! 몇 개? 코인만! 작은 것부터! 몇 시? 기억해!' (22가지)\n화면에 뜨는 한마디를 보고 5초 안에 해내요\n성공하면 점수 · 실패하면 기회가 줄어요 (기회 3번)\n계속 성공하면 점점 빨라져요"; } }
	public override string RightText { get { return "기회 " + lives; } }
	public override bool Crosshair { get { return false; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		StarShape(g, cx - 8, cy - 4, 14, Color.FromArgb(255, 214, 80)); using (Pen p = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(p, cx + 6, cy + 12, cx + 16, cy + 2); g.DrawLine(p, cx + 16, cy + 2, cx + 22, cy + 10); }
	}
	public override void Reset() { kind = -1; lastKind = -1; round = 0; lives = 3; streak = 0; Score = 0; Over = false; Next(); }
	private float Speedf { get { return Math.Min(2.2f, 1f + round * 0.07f); } }
	private void Next()
	{
		int k; do { k = Rnd.Next(6 + tasks.Length); } while (k == lastKind); kind = k; lastKind = k; word = k < 6 ? Words[k] : tasks[k - 6].Word; phase = 0; phT = 0f; win = false; got = 0;
		dur = Math.Max(2.8f, 4.6f - round * 0.07f); tl = dur;
		if (kind >= 6) { cur = tasks[kind - 6]; cur.Init(round); dur = Math.Max(2.6f, cur.Dur - round * 0.04f); if (cur.Dur > 4.9f || cur is LightTask) dur = cur.Dur; tl = dur; return; }
		switch (kind)
		{
			case 0: sx = 100 + (float)Rnd.NextDouble() * 200; sy = 200 + (float)Rnd.NextDouble() * 220; { float a = (float)(Rnd.NextDouble() * 6.28); float sp = 190f * Speedf; svx = (float)Math.Cos(a) * sp; svy = (float)Math.Sin(a) * sp; } break;
			case 1:
				rocks.Clear(); { int nr = 6 + round / 3; for (int i = 0; i < nr; i++) rocks.Add(new float[] { 30 + (float)Rnd.NextDouble() * 340, -30f, (260f + (float)Rnd.NextDouble() * 120f) * Speedf, 0.15f + i * (dur - 1.2f) / nr + (float)Rnd.NextDouble() * 0.2f }); }
				px = W / 2f; break;
			case 2: mp = 0; zone = 0.2f + (float)Rnd.NextDouble() * 0.6f; zw = Math.Max(0.09f, 0.24f - round * 0.006f); break;
			case 3:
				cols = round >= 8 ? 4 : 3; rows = 3; cell = cols == 3 ? 100f : 82f; sad = Rnd.Next(cols * rows); break;
			case 4: need = 9 + round / 2; if (need > 20) need = 20; break;
			default:
				cnt = 2 + Rnd.Next(2) + Math.Min(3, round / 4); if (cnt > 6) cnt = 6; coins.Clear(); showN = 0;
				for (int i = 0; i < cnt; i++) coins.Add(new float[] { 70 + (float)Rnd.NextDouble() * 260, 200 + (float)Rnd.NextDouble() * 160, 0.3f + i * 0.38f });
				dur = 0.3f + cnt * 0.38f + 0.5f + 3.2f; tl = dur; ans = 0; break;
		}
	}
	private void Finish(bool ok)
	{
		if (phase != 1) return; phase = 2; phT = 0f; win = ok;
		if (ok) { int pts = 20 + Math.Min(streak, 10) * 3; Score += pts; streak++; round++; Sfx.Play("ok2"); Fx.Burst(W / 2f, 300f, Color.FromArgb(255, 226, 110), 12); }
		else { lives--; streak = 0; Sfx.Play("bad2"); Shake = 0.3f; }
	}
	private bool InGrid(float x, float y, out int idx)
	{
		float gx = (W - cols * cell) / 2f, gy = 200f; idx = -1;
		if (x < gx || y < gy || x >= gx + cols * cell || y >= gy + rows * cell) return false;
		idx = (int)((y - gy) / cell) * cols + (int)((x - gx) / cell); return true;
	}
	private RectangleF AnsBtn(int i) { return new RectangleF(30f + (i % 3) * 122f, 370f + (i / 3) * 70f, 104f, 56f); }
	public override void Click(float x, float y)
	{
		if (phase != 1) return;
		if (kind >= 6) { int rr = cur.Click(x, y); if (rr != 0) Finish(rr > 0); return; }
		switch (kind)
		{
			case 0: { float dx = x - sx, dy = y - sy; if (dx * dx + dy * dy < 46f * 46f) { Fx.Burst(sx, sy, Color.FromArgb(255, 226, 90), 14); Finish(true); } else { Sfx.Play("swing"); missT = 0.35f; mxc = x; myc = y; } break; }
			case 2: Finish(Math.Abs(mp - zone) < zw / 2f); break;
			case 3: { int idx; if (InGrid(x, y, out idx)) Finish(idx == sad); break; }
			case 4: got++; Sfx.Play("tick"); if (got >= need) Finish(true); break;
			case 5:
				if (tl < dur - (0.3f + cnt * 0.38f + 0.5f)) for (int i = 0; i < 6; i++) if (AnsBtn(i).Contains(x, y)) { Finish(i + 1 == cnt); break; }
				break;
		}
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		mxp = inp.Mx; phT += dt;
		if (phase == 0) { if (phT > 0.85f) { phase = 1; phT = 0f; } return; }
		if (phase == 2) { if (phT > 0.9f) { if (lives <= 0) Over = true; else Next(); } return; }
		tl -= dt;
		if (kind >= 6) { int rr = cur.Update(dt, inp); if (rr != 0) { Finish(rr > 0); return; } if (tl <= 0f) { tl = 0f; Finish(cur.TimeoutWins); } return; }
		switch (kind)
		{
			case 0:
				sx += svx * dt; sy += svy * dt;
				if (sx < 30) { sx = 30; svx = Math.Abs(svx); } if (sx > 370) { sx = 370; svx = -Math.Abs(svx); } if (sy < 130) { sy = 130; svy = Math.Abs(svy); } if (sy > 500) { sy = 500; svy = -Math.Abs(svy); }
				starT -= dt; if (starT <= 0f) { starT = 0.5f + (float)Rnd.NextDouble() * 0.7f; float a = (float)(Rnd.NextDouble() * 6.28), sp = 190f * Speedf; tvx = (float)Math.Cos(a) * sp; tvy = (float)Math.Sin(a) * sp; }
				{ float k2 = Math.Min(1f, dt * 5f); svx += (tvx - svx) * k2; svy += (tvy - svy) * k2; }
				if (missT > 0f) missT -= dt;
				break;
			case 1:
				px += (PF.Cl(inp.Mx, 28f, W - 28f) - px) * Math.Min(1f, dt * 16f);
				foreach (float[] r in rocks)
				{
					float el = dur - tl; if (el < r[3]) continue; r[2] += 260f * dt; r[1] += r[2] * dt;
					float dx = r[0] - px, dy = r[1] - 520f; if (dx * dx + dy * dy < 25f * 25f) { Fx.Burst(px, 520f, Color.FromArgb(255, 150, 150), 10); Finish(false); return; }
				}
				break;
			case 2: mp = 0.5f + 0.5f * (float)Math.Sin((dur - tl) * (3.2f + round * 0.12f) * Speedf * 0.8f - 1.57f); break;
			case 5: { float el = dur - tl; showN = 0; foreach (float[] c in coins) if (el >= c[2]) showN++; break; }
		}
		if (tl <= 0f) { tl = 0f; if (kind == 1) Finish(true); else Finish(false); }
	}
	public override void Draw(Graphics g, double t)
	{
		Color[][] bgs = { new Color[] { Color.FromArgb(255, 214, 120), Color.FromArgb(255, 150, 100) }, new Color[] { Color.FromArgb(90, 120, 200), Color.FromArgb(40, 50, 110) }, new Color[] { Color.FromArgb(120, 214, 170), Color.FromArgb(50, 140, 130) }, new Color[] { Color.FromArgb(214, 160, 230), Color.FromArgb(120, 80, 170) }, new Color[] { Color.FromArgb(250, 150, 160), Color.FromArgb(200, 70, 110) }, new Color[] { Color.FromArgb(130, 200, 240), Color.FromArgb(60, 110, 190) } };
		if (kind >= 6) Bg(g, cur.C1, cur.C2); else { int bi = Math.Max(0, Math.Min(5, kind)); Bg(g, bgs[bi][0], bgs[bi][1]); }
		// 시간 막대
		if (phase == 1) { float f = dur > 0 ? tl / dur : 0; using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillRectangle(b, 20, 562, W - 40, 10); using (SolidBrush b = new SolidBrush(f > 0.35f ? Color.FromArgb(255, 255, 255) : Color.FromArgb(255, 120, 120))) g.FillRectangle(b, 20, 562, (W - 40) * f, 10); }
		if (phase >= 1) DrawKind(g, t);
		if (phase == 0)
		{
			float k = Math.Min(1f, phT / 0.25f); float sc = 0.6f + 0.4f * k + (float)Math.Sin(phT * 14) * 0.03f;
			Txt(g, word, 40f * sc, 2, Color.FromArgb(240, 255, 255, 255), 0, 220, W, 90, StringAlignment.Center);
			Txt(g, kind == 0 ? "움직이는 별을 클릭!" : kind == 1 ? "마우스로 마룡이를 움직여요" : kind == 2 ? "초록 칸에서 클릭!" : kind == 3 ? "혼자 다른 얼굴을 클릭!" : kind == 4 ? "빠르게 연타!" : kind >= 6 ? cur.Hint : "나온 동전 개수를 기억!", 13f, 1, Color.FromArgb(220, 255, 255, 255), 0, 310, W, 28, StringAlignment.Center);
			if (round > 0 && round % 5 == 0) Txt(g, "속도 UP!", 16f, 2, Color.FromArgb(255, 255, 160), 0, 350, W, 30, StringAlignment.Center);
		}
		if (phase == 2)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(110, win ? 20 : 60, win ? 80 : 10, win ? 40 : 20))) g.FillRectangle(b, 0, 0, W, H);
			Txt(g, win ? "성공!" : "실패…", 38f, 2, win ? Color.FromArgb(255, 238, 130) : Color.FromArgb(255, 170, 170), 0, 230, W, 80, StringAlignment.Center);
		}
		if (streak >= 2 && phase != 2) Txt(g, streak + "연속!", 11f, 1, Color.FromArgb(255, 255, 255), 14, 44, 180, 22, StringAlignment.Near);
		Txt(g, (round + 1) + "번째", 11f, 1, Color.FromArgb(190, 255, 255, 255), 0, 44, W, 22, StringAlignment.Center);
	}
	private void DrawKind(Graphics g, double t)
	{
		if (kind >= 6) { cur.Draw(g, t, tl); return; }
		switch (kind)
		{
			case 0:
				using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(b, sx - 34, sy - 34, 68, 68);
				StarShape(g, sx, sy, 28, Color.FromArgb(255, 214, 60));
				if (missT > 0f) { float mk = 1f - missT / 0.35f; using (Pen mp2 = new Pen(Color.FromArgb((int)(200 * (1f - mk)), 255, 255, 255), 2.5f)) g.DrawEllipse(mp2, mxc - 8 - mk * 22, myc - 8 - mk * 22, 16 + mk * 44, 16 + mk * 44); }
				break;
			case 1:
				foreach (float[] r in rocks) { float el = dur - tl; if (el < r[3]) { if (el > r[3] - 0.5f) using (SolidBrush wb = new SolidBrush(Color.FromArgb(90, 255, 120, 120))) g.FillEllipse(wb, r[0] - 17, 528, 34, 10); continue; } using (SolidBrush wb = new SolidBrush(Color.FromArgb(26, 255, 255, 255))) g.FillRectangle(wb, r[0] - 3, r[1] - 60, 6, 60); using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 140, 170))) g.FillEllipse(b, r[0] - 17, r[1] - 17, 34, 34); using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(b, r[0] - 9, r[1] - 11, 12, 9); }
				using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(b, px - 30, 526, 60, 14);
				DrawMascot(g, px, 540, 70, 0f); break;
			case 2:
				{
					float bx = 40f, bw = W - 80f, by = 320f;
					using (GraphicsPath gp = Gfx.Round(new RectangleF(bx - 6, by - 6, bw + 12, 52), 14)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 20, 40, 60))) g.FillPath(b, gp);
					using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 110, 230, 150))) g.FillRectangle(b, bx + bw * (zone - zw / 2f), by, bw * zw, 40);
					using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, bx + bw * mp - 4, by - 10, 8, 60);
					break;
				}
			case 3:
				{
					float gx = (W - cols * cell) / 2f, gy = 200f;
					for (int i = 0; i < cols * rows; i++)
					{
						float cx = gx + (i % cols) * cell + cell / 2f, cy = gy + (i / cols) * cell + cell / 2f; float wob = (float)Math.Sin(t * 4 + i) * 1.5f;
						PF.Face(g, cx, cy + wob, cell * 0.36f, Color.FromArgb(255, 220, 110), i == sad ? 2 : 0);
					}
					break;
				}
			case 4:
				{
					float f = Math.Min(1f, got / (float)need);
					using (GraphicsPath gp = Gfx.Round(new RectangleF(60, 340, W - 120, 36), 18)) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 255, 255, 255))) g.FillPath(b, gp);
					if (f > 0) using (GraphicsPath gp = Gfx.Round(new RectangleF(60, 340, Math.Max(36f, (W - 120) * f), 36), 18)) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 238, 120))) g.FillPath(b, gp);
					float pulse = 1f + 0.08f * (float)Math.Sin(t * 20); float h = 130f * pulse;
					DrawMascot(g, W / 2f, 300f, h, (float)Math.Sin(t * 20) * 4f);
					Txt(g, got + " / " + need, 16f, 2, Color.White, 0, 384, W, 30, StringAlignment.Center); break;
				}
			default:
				{
					float el = dur - tl; float showEnd = 0.3f + cnt * 0.38f + 0.5f;
					if (el < showEnd) { for (int i = 0; i < showN; i++) { float[] c = coins[i]; float age = el - c[2]; float s2 = age < 0.18f ? 0.5f + age / 0.18f * 0.5f : 1f; using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 214, 70))) g.FillEllipse(b, c[0] - 24 * s2, c[1] - 24 * s2, 48 * s2, 48 * s2); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 244, 170))) g.FillEllipse(b, c[0] - 14 * s2, c[1] - 14 * s2, 28 * s2, 28 * s2); } }
					else
					{
						Txt(g, "동전이 몇 개였나요?", 16f, 2, Color.White, 0, 250, W, 34, StringAlignment.Center);
						for (int i = 0; i < 6; i++) { RectangleF r = AnsBtn(i); using (GraphicsPath gp = Gfx.Round(r, 14)) using (SolidBrush b = new SolidBrush(Color.FromArgb(r.Contains(mxp, 0f) ? 230 : 210, 255, 255, 255))) g.FillPath(b, gp); Txt(g, (i + 1).ToString(), 22f, 2, Color.FromArgb(60, 80, 140), r.X, r.Y, r.Width, r.Height, StringAlignment.Center); }
					}
					break;
				}
		}
	}
}


