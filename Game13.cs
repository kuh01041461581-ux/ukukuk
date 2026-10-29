using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ── 마룡이 줄넘기 (미니게임천국식): 줄이 발밑을 지나는 순간 맞춰 점프! ──────────────────────
internal sealed class JumpRopeGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	private float phi, jt, jdur = 0.5f, speedMul = 1f, mulT, mulTo = 1f, tripT, msgT, ropeFlash, gleam; private int jumps, combo, perfects; private bool jumping, queued, tripped, passed; private string msg = "";
	private const float Floor = 470f, HandY = 396f, LX = 58f, RX = 342f, Amp = 96f, JH = 74f;
	public override string Costume { get { return "g_rope"; } }
	public override string Name { get { return "마룡이 줄넘기"; } }
	public override string Key { get { return "rope"; } }
	public override string Desc { get { return "줄이 지나갈 때 맞춰 점프!"; } }
	public override string Help { get { return "클릭(스페이스)하면 점프해요 · 줄이 발밑을 지나는 순간 공중에 떠 있어야 해요\n점프의 가장 높은 순간에 줄이 지나가면 '굿 타이밍!' 보너스\n줄은 점점 빨라지고, 가끔 느려졌다 빨라졌다 해요 · 한 번 걸리면 끝!\n10번마다 보너스 점수가 있어요"; } }
	public override string RightText { get { return jumps + "회"; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (Pen p = new Pen(Color.FromArgb(230, 90, 90), 3f)) g.DrawArc(p, cx - 18, cy - 14, 36, 40, 200, 140);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 90, 60))) { g.FillRectangle(b, cx - 20, cy + 6, 5, 12); g.FillRectangle(b, cx + 15, cy + 6, 5, 12); }
	}
	public override void Reset() { phi = 0.6f; jt = 0f; jumping = false; queued = false; tripped = false; tripT = 0f; jumps = 0; combo = 0; perfects = 0; speedMul = 1f; mulTo = 1f; mulT = 4f; Score = 0; Over = false; msg = ""; msgT = 0f; passed = true; }
	public override void Click(float x, float y) { Jump(); }
	public override void Press(Keys k) { if (k == Keys.Space || k == Keys.Up) Jump(); }
	private void Jump() { if (tripped) return; if (jumping) { queued = true; return; } jumping = true; jt = 0f; Sfx.Play("whoosh"); }
	private float Period { get { return Math.Max(0.52f, 1.15f - jumps * 0.011f) * speedMul; } }
	private float Height { get { if (!jumping) return 0f; float u = jt / jdur; return 4f * JH * u * (1f - u); } }
	public override void Update(float dt, GameInput inp, double t)
	{
		if (dt > 0.05f) dt = 0.05f; gleam += dt; if (msgT > 0f) msgT -= dt; if (ropeFlash > 0f) ropeFlash -= dt;
		if (tripped) { tripT += dt; if (tripT > 1.3f) Over = true; return; }
		mulT -= dt; if (mulT <= 0f) { mulT = 3.5f + (float)Rnd.NextDouble() * 3f; mulTo = jumps < 8 ? 1f : 0.78f + (float)Rnd.NextDouble() * 0.5f; }
		speedMul += (mulTo - speedMul) * Math.Min(1f, dt * 1.2f);
		jdur = Math.Min(0.5f, Period * 0.8f);
		if (jumping) { jt += dt; if (jt >= jdur) { jumping = false; if (queued) { queued = false; jumping = true; jt = 0f; } } }
		float old = phi; phi += dt * 6.2832f / Period;
		// 줄이 바닥(φ=π)을 지나는 순간 판정
		float pn = 3.14159f, a0 = old % 6.2832f, b0 = phi % 6.2832f; bool cross = a0 < pn && b0 >= pn;
		if (cross)
		{
			float h = Height;
			if (h < 24f) { tripped = true; tripT = 0f; Sfx.Play("bad2"); Shake = 0.5f; msg = "걸렸어요! " + jumps + "회"; msgT = 3f; Fx.Burst(200f, Floor, Color.FromArgb(255, 200, 150), 10); }
			else
			{
				jumps++; combo++; int pts = 1; float u = jumping ? jt / jdur : 0f;
				if (u > 0.3f && u < 0.7f) { pts++; perfects++; msg = "굿 타이밍! +2"; msgT = 0.6f; Sfx.Play("ok2"); Fx.Burst(200f, Floor - 20f, Color.FromArgb(255, 226, 120), 6); } else { Sfx.Play("tick"); }
				if (jumps % 10 == 0) { pts += 5; msg = jumps + "회! 보너스 +5"; msgT = 1.2f; Sfx.Play("fanfare"); Fx.Burst(200f, 300f, Color.FromArgb(255, 220, 90), 16); }
				Score += pts; ropeFlash = 0.15f;
			}
		}
	}
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_hjump", Color.FromArgb(110, 180, 240), Color.FromArgb(200, 230, 200));
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 216, 190, 140))) g.FillRectangle(b, 0, Floor + 6, W, H - Floor);
		using (Pen p = new Pen(Color.FromArgb(80, 120, 90, 50), 3f)) g.DrawLine(p, 0, Floor + 6, W, Floor + 6);
		float h = Height; float px = 200f;
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, px - 26 + h * 0.15f, Floor + 2, 52 - h * 0.3f, 10);
		// 줄: 뒤쪽(올라가는 중)은 얇고 흐리게, 앞쪽(내려오는 중)은 굵고 진하게
		float ph = phi % 6.2832f; bool front = ph > 3.1416f; float midY = HandY - Amp * (float)Math.Cos(ph) - (float)(Floor - HandY) * 0f;
		bool tripDraw = tripped;
		if (!front) DrawRope(g, midY, false, tripDraw);
		// 핸들 잡은 두 친구(기둥)
		foreach (float hx in new[] { LX, RX }) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 110, 80))) g.FillRectangle(b, hx - 4, HandY, 8, Floor - HandY + 8); using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 90, 90))) g.FillEllipse(b, hx - 8, HandY - 8, 16, 16); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 255, 255))) g.FillEllipse(b, hx - 3, HandY - 5, 6, 6); }
		float sq = jumping ? 1f + 0.05f * (float)Math.Sin(jt / jdur * 3.14f) : (tripped ? 1f : 1f); float tilt = tripped ? Math.Min(80f, tripT * 160f) : 0f;
		DrawMascot(g, px, Floor - h + (tripped ? 6f : 0f), 92f * sq, tilt);
		if (front) DrawRope(g, midY, true, tripDraw);
		Txt(g, jumps.ToString(), 44f, 2, Color.White, 0, 80, W, 70, StringAlignment.Center);
		Txt(g, "점프", 11f, 2, Color.FromArgb(220, 255, 255, 255), 0, 150, W, 20, StringAlignment.Center);
		if (combo >= 2 && !tripped) Txt(g, combo + "연속", 12f, 2, Color.FromArgb(255, 236, 130), 0, 172, W, 22, StringAlignment.Center);
		if (msgT > 0f) Txt(g, msg, 18f, 2, Color.FromArgb((int)(255 * Math.Min(1f, msgT * 2f)), 255, 236, 130), 0, 250, W, 30, StringAlignment.Center);
		if (jumps == 0 && !tripped) Hint(g, "클릭! 줄이 내려올 때 점프", 540 + 15.0, 13f, false, 1f);
	}
	private void DrawRope(Graphics g, float midY, bool front, bool trip)
	{
		PointF[] pts = new PointF[21]; for (int i = 0; i <= 20; i++) { float u = i / 20f; float x = LX + (RX - LX) * u; float y = HandY + (midY - HandY) * (float)Math.Sin(u * 3.1416f); pts[i] = new PointF(x, Math.Min(Floor + 6f, y)); }
		Color c = trip ? Color.FromArgb(255, 110, 80) : Color.FromArgb(front ? 255 : 160, 240, 80, 80);
		using (Pen p = new Pen(Color.FromArgb(front ? 80 : 40, 0, 0, 0), front ? 7f : 5f) { LineJoin = LineJoin.Round }) { PointF[] sh = new PointF[pts.Length]; for (int i = 0; i < pts.Length; i++) sh[i] = new PointF(pts[i].X + 2f, pts[i].Y + 3f); g.DrawCurve(p, sh); }
		using (Pen p = new Pen(c, front ? 4.5f : 3f) { LineJoin = LineJoin.Round }) g.DrawCurve(p, pts);
		if (front) using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1.4f)) g.DrawCurve(p, pts);
	}
}
