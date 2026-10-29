using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 마룡이 배구: 네트를 사이에 두고 공을 넘겨요. 상대 코트 바닥에 공이 닿으면 1점 · 먼저 7점이면 승리
internal sealed class VolleyGame : MiniGame
{
	public override bool NoLimit { get { return true; } }
	public override string Name { get { return "마룡이 배구"; } }
	public override string Key { get { return "volley"; } }
	public override string Desc { get { return "공을 넘겨 먼저 7점!"; } }
	public override string Help { get { return "←→ (A·D) 이동 · ↑ (W) 점프\n공중에서 스페이스(Z) = 스파이크 (↑ 같이 누르면 높게, ↓ 같이 누르면 내리꽂기)\n땅에서 방향키 + 스페이스 = 몸 던져 받기\n마우스: 마룡이가 커서를 따라가요 · 클릭 = 점프, 공중에서 클릭 = 스파이크\n상대 코트 바닥에 공을 떨어뜨리면 1점 · 먼저 7점이면 이겨요"; } }
	public override string RightText { get { return me.Pts + " : " + cpu.Pts; } }
	public override void Icon(Graphics g, float cx, float cy) { if (!Spr.Draw(g, "ms_pet_toy_ball", cx, cy, 30f, 30f, 0f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 170, 245))) g.FillEllipse(b, cx - 14, cy - 14, 28, 28); }

	private const float GY = 490f, NX = 200f, NTOP = 382f, NW = 5f, BR = 17f, SC = 0.78f, PR = 34f;
	private class Pl { public float X, Y, Vx, Vy, Spike, Dive, DiveDir, HitCd, Anim; public int Pts, Face; public bool Air { get { return Y < GY - 0.5f; } } }
	private readonly Pl me = new Pl(), cpu = new Pl();
	private float bx, by, bvx, bvy, brot, pauseT, fxT, flashT, msgT, el, aiReact, aiJumpAt; private int fxKind = -1, lastScorer, serveTo; private float fxX, fxY; private string msg = ""; private bool matchOver, mouseMode; private float lastMx;
	private class Pt { public float X, Y, Vx, Vy, Life; }
	private readonly List<Pt> sand = new List<Pt>();

	public override void Reset()
	{
		me.Pts = 0; cpu.Pts = 0; Score = 0; Over = false; matchOver = false; el = 0f; sand.Clear(); mouseMode = false;
		Place(); Serve(0); msg = "먼저 7점을 내면 이겨요!"; msgT = 2.2f;
	}
	private void Place()
	{
		me.X = 90f; me.Y = GY; me.Vx = me.Vy = 0f; me.Spike = me.Dive = 0f; me.Face = 1;
		cpu.X = 310f; cpu.Y = GY; cpu.Vx = cpu.Vy = 0f; cpu.Spike = cpu.Dive = 0f; cpu.Face = -1;
	}
	// 공은 방금 점수를 낸 쪽 머리 위에서 떨어져요
	private void Serve(int who) { serveTo = who; bx = who == 0 ? me.X : cpu.X; by = 150f; bvx = 0f; bvy = 0f; pauseT = 0.8f; fxKind = -1; aiReact = 0.25f; }

	public override void Click(float x, float y) { mouseMode = true; Act(me, false, false, true); }
	public override void Press(Keys k)
	{
		if (k == Keys.Space || k == Keys.Z || k == Keys.Enter || k == Keys.ControlKey) { mouseMode = false; Spike(me, keyUp, keyDown); }
		if (k == Keys.Up || k == Keys.W) { mouseMode = false; Jump(me); }
	}
	private bool keyUp, keyDown;
	private void Jump(Pl p) { if (!p.Air && p.Dive <= 0f && !matchOver) { p.Vy = -640f; Sfx.Play("swing"); } }
	private void Spike(Pl p, bool up, bool down)
	{
		if (matchOver) return;
		if (p.Air) { if (p.Spike <= 0f) { p.Spike = 0.32f; spikeUp = up; spikeDown = down; } }
		else if (p.Dive <= 0f && Math.Abs(p.Vx) > 40f) { p.Dive = 0.45f; p.DiveDir = Math.Sign(p.Vx); p.Vy = -160f; Sfx.Play("whoosh"); }
	}
	private bool spikeUp, spikeDown;
	private void Act(Pl p, bool up, bool down, bool click) { if (p.Air) Spike(p, up, down); else Jump(p); }

	public override void Update(float dt, GameInput inp, double t)
	{
		if (dt > 0.04f) dt = 0.04f; el += dt; if (msgT > 0f) msgT -= dt; if (flashT > 0f) flashT -= dt; if (fxT > 0f) fxT -= dt;
		keyUp = inp.Down != null && (inp.Down.Contains(Keys.Up) || inp.Down.Contains(Keys.W)); keyDown = inp.Down != null && (inp.Down.Contains(Keys.Down) || inp.Down.Contains(Keys.S));
		bool kl = inp.L || (inp.Down != null && inp.Down.Contains(Keys.A)), kr = inp.R || (inp.Down != null && inp.Down.Contains(Keys.D));
		if (kl || kr) mouseMode = false; else if (inp.MouseMoved && Math.Abs(inp.Mx - lastMx) > 2f) mouseMode = true; lastMx = inp.Mx;
		for (int i = sand.Count - 1; i >= 0; i--) { Pt q = sand[i]; q.Life -= dt; q.Vy += 900f * dt; q.X += q.Vx * dt; q.Y += q.Vy * dt; if (q.Life <= 0f || q.Y > GY + 8f) sand.RemoveAt(i); }
		if (matchOver) { pauseT -= dt; Body(me, 0f, dt); Body(cpu, 0f, dt); if (pauseT <= 0f) Over = true; return; }
		// 내 이동
		float mv = (kl ? -1f : 0f) + (kr ? 1f : 0f);
		if (mouseMode) { float d = inp.Mx - me.X; mv = Math.Abs(d) < 8f ? 0f : Math.Sign(d) * Math.Min(1f, Math.Abs(d) / 40f); }
		Body(me, mv, dt);
		AiMove(dt);
		if (pauseT > 0f) { pauseT -= dt; return; }
		// 공
		bvy += 760f * dt; bx += bvx * dt; by += bvy * dt; brot += bvx * dt * 1.6f;
		if (bx < BR) { bx = BR; bvx = Math.Abs(bvx) * 0.9f; } if (bx > W - BR) { bx = W - BR; bvx = -Math.Abs(bvx) * 0.9f; }
		if (by < BR) { by = BR; bvy = Math.Abs(bvy) * 0.6f; }
		// 네트: 위쪽 끝에 맞으면 튀어 오르고, 옆면에 맞으면 되돌아가요
		if (Math.Abs(bx - NX) < NW + BR && by > NTOP - BR)
		{
			if (by < NTOP && Math.Abs(bx - NX) < NW + BR * 0.6f) { by = NTOP - BR; bvy = -Math.Abs(bvy) * 0.75f; bvx += (bx < NX ? -60f : 60f); }
			else { bx = bx < NX ? NX - NW - BR : NX + NW + BR; bvx = -bvx * 0.7f; }
			Sfx.Play("tick");
		}
		Touch(me); Touch(cpu);
		float sp = (float)Math.Sqrt(bvx * bvx + bvy * bvy); if (sp > 1050f) { bvx *= 1050f / sp; bvy *= 1050f / sp; }
		// 바닥에 닿으면 점수
		if (by >= GY - BR)
		{
			by = GY - BR; int scorer = bx < NX ? 1 : 0; Pl w = scorer == 0 ? me : cpu; w.Pts++; lastScorer = scorer;
			fxKind = 0; fxX = bx; fxY = GY - BR; fxT = 0.9f; Shake = 0.25f; Sfx.Play(scorer == 0 ? "ok" : "bad2");
			for (int q = 0; q < 14; q++) sand.Add(new Pt { X = bx, Y = GY - 4f, Vx = (float)(Rnd.NextDouble() - 0.5) * 300f, Vy = -150f - (float)Rnd.NextDouble() * 250f, Life = 0.8f });
			Score = me.Pts * 100;
			if (me.Pts >= 7 || cpu.Pts >= 7)
			{
				matchOver = true; pauseT = 2.6f; bool win = me.Pts >= 7; if (win) Score += 500 + Math.Max(0, (7 - cpu.Pts) * 60);
				msg = win ? "우승! " + me.Pts + " : " + cpu.Pts : "아쉬워요… " + me.Pts + " : " + cpu.Pts; msgT = 3f; Sfx.Play(win ? "fanfare" : "bad2"); return;
			}
			msg = scorer == 0 ? "득점! " + me.Pts + " : " + cpu.Pts : "실점… " + me.Pts + " : " + cpu.Pts; msgT = 1.2f;
			Place(); Serve(scorer); pauseT = 1.3f;
		}
	}
	// 몸 움직임: 좌우 · 점프 · 몸 던지기
	private void Body(Pl p, float mv, float dt)
	{
		bool left = p == me; float lo = left ? 26f : NX + NW + 26f, hi = left ? NX - NW - 26f : W - 26f;
		if (p.Dive > 0f) { p.Dive -= dt; p.Vx = p.DiveDir * 330f; }
		else { float tv = mv * 250f; p.Vx += (tv - p.Vx) * Math.Min(1f, dt * (p.Air ? 6f : 16f)); if (mv != 0f && !p.Air) p.Face = left ? 1 : -1; }
		p.Vy += 1500f * dt; p.X += p.Vx * dt; p.Y += p.Vy * dt;
		if (p.Y >= GY) { if (p.Vy > 300f) { for (int q = 0; q < 4; q++) sand.Add(new Pt { X = p.X, Y = GY - 2f, Vx = (float)(Rnd.NextDouble() - 0.5) * 160f, Vy = -60f - (float)Rnd.NextDouble() * 90f, Life = 0.4f }); } p.Y = GY; p.Vy = 0f; }
		p.X = Clamp(p.X, lo, hi); if (p.Spike > 0f) p.Spike -= dt; if (p.HitCd > 0f) p.HitCd -= dt; p.Anim += dt * (Math.Abs(p.Vx) > 30f ? 1f : 0f);
	}
	// 공 닿기: 몸 위쪽 동그라미와 공이 겹치면 튕겨 보내요. 스파이크 중이면 세게 내리꽂아요
	private void Touch(Pl p)
	{
		if (p.HitCd > 0f) return; float cx = p.X, cy = p.Y - 52f, dx = bx - cx, dy = by - cy; float rr = PR + BR + (p.Dive > 0f ? 6f : 0f);
		if (p.Dive > 0f) cy = p.Y - 22f;
		dy = by - cy; if (dx * dx + dy * dy > rr * rr) return;
		int dir = p == me ? 1 : -1; p.HitCd = 0.18f;
		if (p.Spike > 0f && p.Air)
		{
			bool up = p == me ? spikeUp : cpuUp, down = p == me ? spikeDown : cpuDown;
			bvx = dir * (up ? 520f : 820f); bvy = up ? -420f : (down ? 520f : 120f);
			flashT = 0.15f; fxKind = 1; fxX = bx; fxY = by; fxT = 0.25f; Shake = 0.2f; Sfx.Play("whoosh"); p.Spike = 0.12f;
		}
		else
		{
			bvx = dx * 7.5f + dir * 60f; bvy = -Math.Max(520f, 380f + Math.Abs(bvy) * 0.35f);
			if (p.Dive > 0f) bvy = -560f;
			Sfx.Play("thud_soft");
		}
		bx = cx + dx / (float)Math.Sqrt(dx * dx + dy * dy + 0.01f) * rr; by = Math.Min(by, cy);
	}
	// 상대(컴퓨터): 공이 떨어질 자리를 미리 계산해서 달려가고, 가까우면 점프해서 스파이크
	private bool cpuUp, cpuDown;
	private void AiMove(float dt)
	{
		float tx = 300f; bool mine = false;
		if (pauseT <= 0f || serveTo == 1)
		{
			// 공이 내 코트(오른쪽)로 떨어질 위치 예측
			float sx = bx, sy = by, svx = bvx, svy = bvy; for (int k = 0; k < 120; k++) { svy += 760f * 0.016f; sx += svx * 0.016f; sy += svy * 0.016f; if (sx < BR) { sx = BR; svx = -svx; } if (sx > W - BR) { sx = W - BR; svx = -svx; } if (sy > GY - 60f && svy > 0f) break; }
			if (sx > NX) { tx = sx + 10f; mine = true; } else tx = 290f;
		}
		if (aiReact > 0f) aiReact -= dt;
		float d = tx - cpu.X, mv = aiReact > 0f ? 0f : (Math.Abs(d) < 6f ? 0f : Math.Sign(d) * Math.Min(1f, Math.Abs(d) / 30f)) * 0.92f;
		Body(cpu, mv, dt);
		if (pauseT > 0f || matchOver) return;
		float bdx = bx - cpu.X, bdy = by - (cpu.Y - 52f);
		if (mine && !cpu.Air && Math.Abs(bdx) < 45f && bdy > -170f && bdy < -60f && bvy > -100f && Rnd.NextDouble() < 0.18) Jump(cpu);
		if (cpu.Air && Math.Abs(bdx) < 50f && Math.Abs(bdy) < 55f && cpu.Spike <= 0f && Rnd.NextDouble() < 0.35) { cpu.Spike = 0.3f; cpuUp = Rnd.NextDouble() < 0.25; cpuDown = !cpuUp && Rnd.NextDouble() < 0.5; }
		if (!cpu.Air && mine && Math.Abs(d) > 70f && by > 330f && bvy > 0f && cpu.Dive <= 0f && Rnd.NextDouble() < 0.06) { cpu.Dive = 0.45f; cpu.DiveDir = Math.Sign(d); cpu.Vy = -160f; }
	}

	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "ms_vb_bg", Color.FromArgb(120, 190, 250), Color.FromArgb(250, 220, 150));
		if (Spr.Get("ms_vb_bg") == null)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 214, 150))) g.FillRectangle(b, 0, GY, W, H - GY);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 110, 200))) g.FillRectangle(b, NX - 4, NTOP, 8, GY - NTOP);
		}
		// 그림자
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 60, 40, 20)))
		{
			foreach (Pl p in new[] { me, cpu }) { float k = Math.Max(0.4f, 1f - (GY - p.Y) / 300f); g.FillEllipse(b, p.X - 30f * k, GY - 6f, 60f * k, 12f); }
			if (fxKind != 0 || fxT <= 0f) { float k = Math.Max(0.3f, 1f - (GY - by) / 400f); g.FillEllipse(b, bx - 16f * k, GY - 5f, 32f * k, 10f); }
		}
		DrawPl(g, me, "ms_vb_m", false, t);
		DrawPl(g, cpu, "ms_vb_o", true, t);
		// 공 (바닥에 떨어진 순간에는 모래 튀김 그림으로)
		if (fxKind == 0 && fxT > 0.5f) Spr.Draw(g, "ms_vb_fx0", fxX, fxY - 4f, 74f, 86f, 0f);
		else
		{
			if (fxKind == 1 && fxT > 0f && Spr.Draw(g, "ms_vb_fx1", bx, by, 92f, 104f, 0f)) { }   // 스파이크 순간: 번쩍이는 공 그림
			else if (!Spr.Draw(g, "ms_pet_toy_ball", bx, by, BR * 2.2f, BR * 2.2f, brot)) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 170, 245))) g.FillEllipse(b, bx - BR, by - BR, BR * 2, BR * 2);
		}
		using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 240, 210, 150))) foreach (Pt q in sand) g.FillEllipse(b, q.X - 3f, q.Y - 3f, 6f, 6f);
		// 점수판
		for (int s = 0; s < 2; s++)
		{
			RectangleF r = new RectangleF(s == 0 ? 20 : W - 120, 20, 100, 64);
			using (GraphicsPath gp = Gfx.Round(r, 14)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 12, 24, 46))) g.FillPath(b, gp);
			Txt(g, s == 0 ? "마룡이" : "분홍이", 9f, 2, s == 0 ? Color.FromArgb(150, 240, 220) : Color.FromArgb(255, 200, 180), r.X, r.Y + 4, r.Width, 18, StringAlignment.Center);
			Txt(g, (s == 0 ? me.Pts : cpu.Pts).ToString(), 22f, 2, Color.White, r.X, r.Y + 20, r.Width, 42, StringAlignment.Center);
		}
		if (flashT > 0f) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(flashT / 0.15f * 90), 255, 250, 200))) g.FillRectangle(b, 0, 0, W, H);
		if (msgT > 0f) Hint(g, msg, 130, 14f, true, Math.Min(1f, msgT * 2f));
		if (el < 4f && !matchOver) Hint(g, "←→ 이동 · ↑ 점프 · 공중에서 스페이스 = 스파이크", 560, 10f, false, Math.Min(1f, (4f - el) * 2f));
	}
	private void DrawPl(Graphics g, Pl p, string pre, bool flip, double t)
	{
		int f;
		bool scoredPause = pauseT > 0f && fxKind == 0 && fxT > 0f;
		if (matchOver) f = ((p == me) == (me.Pts >= 7)) ? 6 : 7;
		else if (scoredPause) f = ((p == me) == (lastScorer == 0)) ? 6 : 7;
		else if (p.Dive > 0f) f = 5;
		else if (p.Spike > 0f && p.Air) f = 4;
		else if (p.Air) f = 3;
		else if (Math.Abs(p.Vx) > 30f) f = 1 + ((int)(p.Anim * 9f)) % 2;
		else f = 0;
		string n = pre + f; bool fl = flip;
		if (f == 5) fl = p.DiveDir < 0;   // 몸 던지기는 날아가는 방향을 봐요
		float bob = f == 0 ? (float)Math.Sin(t * 4 + (flip ? 1 : 0)) * 1.2f : 0f;
		if (!Spr.Frame(g, n, p.X, p.Y + 2f + bob, SC, 0f, fl))
			using (SolidBrush b = new SolidBrush(flip ? Color.FromArgb(250, 180, 160) : Color.FromArgb(30, 160, 140))) g.FillEllipse(b, p.X - 30, p.Y - 80, 60, 80);
	}
}
