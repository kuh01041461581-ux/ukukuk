using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// ── 타로 연출: 마법진 셔플 · 카드가 날아갈 때 빛 꼬리 · 뒤집힐 때 별빛 폭발 · 뽑은 카드에 마우스를 올리면 크게 떠오르는 미리보기 ──
internal sealed partial class TarotForm
{
	private sealed class Mote { public float X, Y, Vx, Vy, Life, Max, Size, Rot, Spin; public int K; public Color C; }   // K 0 반짝 별 · 1 둥근 빛 · 2 퍼지는 고리
	private readonly List<Mote> motes = new List<Mote>(); private readonly Random fxR = new Random();
	private int hoverSlot = -1, hoverShown = -1; private float hoverA, fxClock; private readonly int[] lastState = new int[12];
	private static readonly Color Gold = Color.FromArgb(255, 222, 150), Violet = Color.FromArgb(190, 160, 255), Mint = Color.FromArgb(160, 240, 225);

	private void Emit(float x, float y, int n, float sp, Color c, int k, float life, float size)
	{
		for (int i = 0; i < n && motes.Count < 420; i++)
		{
			double a = fxR.NextDouble() * Math.PI * 2; float v = sp * (0.25f + (float)fxR.NextDouble() * 0.75f), l = life * (0.6f + (float)fxR.NextDouble() * 0.6f);
			motes.Add(new Mote { X = x, Y = y, Vx = (float)Math.Cos(a) * v, Vy = (float)Math.Sin(a) * v - sp * 0.15f, Life = l, Max = l, Size = size * (0.6f + (float)fxR.NextDouble() * 0.8f), Rot = (float)(fxR.NextDouble() * 90), Spin = (float)(fxR.NextDouble() * 200 - 100), K = k, C = c });
		}
	}
	private void Ring(float x, float y, Color c, float life) { motes.Add(new Mote { X = x, Y = y, Life = life, Max = life, Size = 10f, K = 2, C = c }); }
	// 매 프레임: 빛 입자 움직이기 + 카드 상태가 바뀌는 순간 연출 (true 면 계속 그려야 해요)
	private bool FxStep(float dt)
	{
		fxClock += dt; bool busy = false;
		for (int i = motes.Count - 1; i >= 0; i--)
		{
			Mote m = motes[i]; m.Life -= dt; if (m.Life <= 0f) { motes.RemoveAt(i); continue; }
			m.X += m.Vx * dt; m.Y += m.Vy * dt; m.Vx *= (float)Math.Pow(0.35, dt); m.Vy = m.Vy * (float)Math.Pow(0.35, dt) - 14f * dt; m.Rot += m.Spin * dt; if (m.K == 2) m.Size += 150f * dt;
		}
		if (motes.Count > 0) busy = true;
		// 섞는 동안: 마법진 둘레에서 빛가루가 피어올라요
		if (st == St.Shuffle) { float a = (float)(fxR.NextDouble() * Math.PI * 2); Emit(210f + (float)Math.Cos(a) * 120f, ShufCy + (float)Math.Sin(a) * 52f, 2, 30f, fxR.Next(3) == 0 ? Violet : Gold, 0, 1.1f, 4.5f); }
		for (int i = 0; i < slots.Length && i < lastState.Length; i++)
		{
			Slot s = slots[i]; RectangleF r = sR[i];
			if (s.State == 1)
			{
				// 날아가는 카드 뒤로 빛 꼬리
				float t = 1f - (float)Math.Pow(1f - s.T, 3); float cx = s.From.X + s.From.Width / 2 + (r.X + r.Width / 2 - s.From.X - s.From.Width / 2) * t, cy = s.From.Y + s.From.Height / 2 + (r.Y + r.Height / 2 - s.From.Y - s.From.Height / 2) * t;
				Emit(cx, cy, 2, 22f, fxR.Next(2) == 0 ? Gold : Mint, fxR.Next(3) == 0 ? 1 : 0, 0.7f, 4f);
			}
			if (s.State != lastState[i])
			{
				float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
				if (s.State == 1) Emit(s.From.X + s.From.Width / 2, s.From.Y + s.From.Height / 2, 10, 70f, Gold, 0, 0.6f, 5f);
				else if (s.State == 2) { Ring(cx, cy, Gold, 0.5f); Emit(cx, cy, 8, 60f, Mint, 1, 0.6f, 4f); }
				else if (s.State == 3) { Color c = s.Rev ? Violet : Gold; Ring(cx, cy, c, 0.7f); Ring(cx, cy, Color.White, 0.45f); Emit(cx, cy, 26, 150f, c, 0, 1.0f, 6f); Emit(cx, cy, 10, 90f, Color.White, 1, 0.7f, 3.5f); }
				lastState[i] = s.State;
			}
		}
		// 뽑은 카드 미리보기: 부드럽게 나타나고 사라져요
		if (hoverSlot >= 0) hoverShown = hoverSlot;
		float want = hoverSlot >= 0 ? 1f : 0f; if (Math.Abs(hoverA - want) > 0.001f) { hoverA += (want - hoverA) * Math.Min(1f, dt * 9f); if (Math.Abs(hoverA - want) < 0.01f) hoverA = want; busy = true; }
		if (hoverA > 0.01f) { busy = true; if (fxR.NextDouble() < dt * 6) { RectangleF pr = PreviewRect(hoverShown); Emit(pr.X + (float)fxR.NextDouble() * pr.Width, pr.Bottom - 4f, 1, 12f, Gold, 0, 1.2f, 3.5f); } }
		else if (hoverSlot < 0) hoverShown = -1;
		return busy;
	}
	private float ShufCy { get { return Math.Min(LH - 130f, SlotsBottom + 150f); } }
	private void FxReset() { motes.Clear(); Array.Clear(lastState, 0, lastState.Length); hoverSlot = -1; hoverShown = -1; hoverA = 0f; }
	// 뽑아서 놓인 카드 위에 마우스가 있는지 (뒤집힌 카드만)
	private int HitSlot(float x, float y)
	{
		if (zoom >= 0 || spOpen || hist) return -1;
		for (int i = slots.Length - 1; i >= 0; i--) if (slots[i].State == 3 && sR[i].Contains(x, y)) return i;
		return -1;
	}
	private RectangleF PreviewRect(int i)
	{
		if (i < 0 || i >= slots.Length) return RectangleF.Empty; RectangleF r = sR[i];
		float w = Math.Min(170f, Math.Max(150f, r.Width * 2f)), h = w / CAR, cx = r.X + r.Width / 2f;
		float x = Math.Max(10f, Math.Min(CW - 10f - w, cx - w / 2f)), y = r.Bottom + (noLab ? 10f : 38f); if (y + h > LH - 8f) y = Math.Max(8f, r.Y - h - 10f); if (y < 8f) y = 8f;
		return new RectangleF(x, y, w, h);
	}
	private static void Star(Graphics g, float x, float y, float r, float rot, Color c)
	{
		PointF[] p = new PointF[8]; for (int k = 0; k < 8; k++) { double a = (rot + k * 45) * Math.PI / 180.0; float rr = k % 2 == 0 ? r : r * 0.32f; p[k] = new PointF(x + (float)Math.Cos(a) * rr, y + (float)Math.Sin(a) * rr); }
		using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, p);
	}
	private void DrawMotes(Graphics g)
	{
		if (motes.Count == 0) return; SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
		foreach (Mote m in motes)
		{
			float u = m.Life / m.Max; int a = (int)(255 * Math.Min(1f, u * 1.6f));
			if (m.K == 2) { using (Pen p = new Pen(Color.FromArgb((int)(170 * u), m.C), 2.5f * u + 0.5f)) g.DrawEllipse(p, m.X - m.Size, m.Y - m.Size * 0.9f, m.Size * 2, m.Size * 1.8f); continue; }
			if (m.K == 1) { using (SolidBrush b = new SolidBrush(Color.FromArgb(a / 3, m.C))) g.FillEllipse(b, m.X - m.Size * 1.8f, m.Y - m.Size * 1.8f, m.Size * 3.6f, m.Size * 3.6f); using (SolidBrush b = new SolidBrush(Color.FromArgb(a, Color.White))) g.FillEllipse(b, m.X - m.Size * 0.5f, m.Y - m.Size * 0.5f, m.Size, m.Size); continue; }
			using (SolidBrush b = new SolidBrush(Color.FromArgb(a / 4, m.C))) g.FillEllipse(b, m.X - m.Size, m.Y - m.Size, m.Size * 2, m.Size * 2);
			Star(g, m.X, m.Y, m.Size, m.Rot, Color.FromArgb(a, m.C));
		}
		g.SmoothingMode = sm;
	}
	// 마법진: 겹 원 + 룬 눈금 + 돌아가는 별 모양 (금빛)
	private static void MagicCircle(Graphics g, float cx, float cy, float rx, float ry, float rot, float alpha)
	{
		if (alpha <= 0.01f) return; int A = (int)(255 * alpha); SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
		using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(cx - rx * 1.15f, cy - ry * 1.15f, rx * 2.3f, ry * 2.3f); using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(A * 70 / 255, 255, 220, 160); pb.SurroundColors = new[] { Color.FromArgb(0, 255, 220, 160) }; g.FillPath(pb, gp); } }
		using (Pen p = new Pen(Color.FromArgb(A * 200 / 255, 245, 205, 120), 1.6f)) { g.DrawEllipse(p, cx - rx, cy - ry, rx * 2, ry * 2); g.DrawEllipse(p, cx - rx * 0.84f, cy - ry * 0.84f, rx * 1.68f, ry * 1.68f); }
		using (Pen p = new Pen(Color.FromArgb(A * 150 / 255, 200, 170, 255), 1.2f))
		{
			for (int k = 0; k < 24; k++) { double a = (rot * 0.5f + k * 15) * Math.PI / 180.0; float c = (float)Math.Cos(a), s = (float)Math.Sin(a); g.DrawLine(p, cx + c * rx * 0.86f, cy + s * ry * 0.86f, cx + c * rx * (k % 3 == 0 ? 0.98f : 0.93f), cy + s * ry * (k % 3 == 0 ? 0.98f : 0.93f)); }
			PointF[] st = new PointF[7]; for (int k = 0; k < 7; k++) { double a = (rot + k * 360.0 * 3 / 7) * Math.PI / 180.0; st[k] = new PointF(cx + (float)Math.Cos(a) * rx * 0.8f, cy + (float)Math.Sin(a) * ry * 0.8f); }
			g.DrawPolygon(p, st); g.DrawEllipse(p, cx - rx * 0.3f, cy - ry * 0.3f, rx * 0.6f, ry * 0.6f);
		}
		g.SmoothingMode = sm;
	}
	// 섞기: 카드들이 마법진 위를 돌다가 가운데로 모여 빛나요
	private void DrawShuffleMagic(Graphics g)
	{
		float t = shufT, cx = 210f, cy = ShufCy;
		FUi.Text(g, t < 0.75f ? "카드를 섞는 중이에요… 마음속 고민을 떠올려 보세요" : "카드가 당신의 질문을 들었어요", FUi.F(10f, 1), PSub, new RectangleF(20, SlotsBottom + 6, 380, 22), StringAlignment.Center, StringAlignment.Center, false);
		float ca = Math.Min(1f, t * 3f) * (t > 0.85f ? Math.Max(0f, (1f - t) / 0.15f) : 1f); MagicCircle(g, cx, cy + 18f, 132f, 56f, fxClock * 40f, ca);
		int n = 14; float gather = t < 0.7f ? 0f : (t - 0.7f) / 0.3f; float ge = gather * gather * (3 - 2 * gather);
		for (int i = 0; i < n; i++)
		{
			float ang = fxClock * 2.4f + i * 6.283f / n, rad = (1f - ge) * (1f - (float)Math.Pow(1f - Math.Min(1f, t * 2.2f), 2)); float x = cx + (float)Math.Cos(ang) * 118f * rad, y = cy + (float)Math.Sin(ang) * 44f * rad - 6f;
			float rot = (float)Math.Sin(ang) * 14f * (1f - ge), lift = (float)Math.Sin(fxClock * 3 + i) * 3f;
			DrawBackSmall(g, new PointF(x - FW / 2f, y - FH / 2f), rot, lift, 0.95f + 0.25f * (float)Math.Max(0, Math.Sin(ang)));
		}
		if (t > 0.9f) { float f = (t - 0.9f) / 0.1f; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(150 * (1f - Math.Abs(f * 2f - 1f))), 255, 240, 200))) g.FillEllipse(b, cx - 90f, cy - 70f, 180f, 140f); }
	}
	// 뽑은 카드: 살짝 빛나는 테두리 + 크게 떠오른 미리보기 (천천히 둥실둥실)
	private void DrawSlotHover(Graphics g)
	{
		if (hoverShown < 0 || hoverShown >= slots.Length || hoverA <= 0.01f || slots[hoverShown].State != 3) return;
		RectangleF r = sR[hoverShown]; float a = hoverA;
		for (int k = 3; k >= 1; k--) using (GraphicsPath gp = Gfx.Round(new RectangleF(r.X - k * 2.5f, r.Y - k * 2.5f, r.Width + k * 5f, r.Height + k * 5f), 8 + k * 2)) using (Pen p = new Pen(Color.FromArgb((int)(a * (90 - k * 22)), 255, 214, 120), 2.2f)) g.DrawPath(p, gp);
		RectangleF pr = PreviewRect(hoverShown); float bob = (float)Math.Sin(fxClock * 2.2f) * 3f, k0 = 0.9f + 0.1f * a;
		RectangleF d = new RectangleF(pr.X + pr.Width * (1 - k0) / 2f, pr.Y + pr.Height * (1 - k0) / 2f + bob + (1f - a) * 10f, pr.Width * k0, pr.Height * k0);
		using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(60 * a), 20, 10, 40))) using (GraphicsPath gp = Gfx.Round(new RectangleF(d.X + 4, d.Y + 8, d.Width, d.Height), 12)) g.FillPath(b, gp);
		for (int k = 4; k >= 1; k--) using (GraphicsPath gp = Gfx.Round(new RectangleF(d.X - k * 3f, d.Y - k * 3f, d.Width + k * 6f, d.Height + k * 6f), 12 + k * 3)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(a * (18 + (4 - k) * 10)), slots[hoverShown].Rev ? Violet : Gold))) g.FillPath(b, gp);
		Bitmap f = Face(slots[hoverShown].Card, slots[hoverShown].Rev);
		if (a >= 0.98f) g.DrawImage(f, d); else using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes()) { System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix(); cm.Matrix33 = a; ia.SetColorMatrix(cm); g.DrawImage(f, new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height), 0, 0, f.Width, f.Height, GraphicsUnit.Pixel, ia); }
		for (int k = 0; k < 5; k++) { double an = fxClock * 1.3 + k * 1.2566; Star(g, d.X + d.Width / 2f + (float)Math.Cos(an) * (d.Width * 0.62f), d.Y + d.Height / 2f + (float)Math.Sin(an) * (d.Height * 0.55f), 4.5f + 1.5f * (float)Math.Sin(fxClock * 4 + k), fxClock * 60f, Color.FromArgb((int)(220 * a), Gold)); }
		Slot s = slots[hoverShown]; RectangleF lb = new RectangleF(d.X - 20f, d.Bottom + 6f, d.Width + 40f, 22f); if (lb.Bottom > LH - 4f) lb.Y = d.Y - 28f;
		using (GraphicsPath gp = Gfx.Round(lb, 11)) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(225 * a), 34, 24, 52))) g.FillPath(b, gp);
		FUi.Text(g, CardName(s.Card) + (s.Rev ? " (역)" : "") + " · " + CardKw(s.Card), FUi.F(9f, 1), Color.FromArgb((int)(255 * a), 255, 236, 190), lb, StringAlignment.Center, StringAlignment.Center, false);
	}
}
