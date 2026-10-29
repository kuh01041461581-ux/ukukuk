using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 마룡이 방어선: 벽 뒤에 숨어 있다가 몸을 내밀어 쏘는 엄폐 사격 게임
internal sealed class CoverGame : MiniGame
{
	private class En { public float X, Y, Hp, MaxHp, Sp, R, Hit, Ph, Kv, Sq, Dust; public int T; }
	private class Ob { public float X, Y, W, H, Hp, MaxHp, Life, Hit; public int Kind; }
	private class Db { public float X, Y, T; public int Kind; }
	private readonly List<Db> debs = new List<Db>();
	private readonly List<Db> holes = new List<Db>();
	private static readonly Dictionary<string, Bitmap> flipC = new Dictionary<string, Bitmap>();
	private static Bitmap Flip(string n) { Bitmap r; if (flipC.TryGetValue(n, out r)) return r; Bitmap s0 = Spr.Get(n); if (s0 != null) { r = new Bitmap(s0); r.RotateFlip(RotateFlipType.RotateNoneFlipX); } else r = null; flipC[n] = r; return r; }
	private void Sp(Graphics g, string n, float cx, float bottom, float h, bool flip, float alpha, bool white)
	{
		Bitmap b = flip ? Flip(n) : Spr.Get(n); if (b == null) return; float w = b.Width * h / b.Height; System.Drawing.Image im = ImgCache.Get(b, w * ImgCache.Scale(g), h * ImgCache.Scale(g));
		using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
		{
			System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix(); cm.Matrix33 = alpha; if (white) { cm.Matrix40 = 0.55f; cm.Matrix41 = 0.55f; cm.Matrix42 = 0.55f; } ia.SetColorMatrix(cm);
			g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(im, new Rectangle((int)(cx - w / 2), (int)(bottom - h), (int)Math.Ceiling(w), (int)Math.Ceiling(h)), 0, 0, im.Width, im.Height, GraphicsUnit.Pixel, ia);
		}
	}
	private class Tr { public float X1, Y1, X2, Y2, T, T0, Wd, Len, Age; public Color C; public bool Slow; }
	private class Cs { public float X, Y, Vx, Vy, R, Vr, T; public bool Big; }
	private class Sm { public float X, Y, Vx, Vy, R, T, L; }
	private class Dy { public float X, Y, Vx, Vy, R, Vr, T; public int Ty; public float Rad; }
	private class Fq { public float X, Y, T, T0, W; public string N; }
	private readonly List<Fq> fqs = new List<Fq>();
	private class Rg { public float X, Y, R, Rmax, T, T0; public Color C; }
	private const float OX = 200f, WallTop = 438f, OY = 398f;
	private static readonly string[] GN = { "쌍권총", "샷건", "돌격소총", "저격총" };
	private static readonly string[] GD = { "정확해요 · 쉽고 안정적 · 장전이 빨라요 (탄 24)", "가까울수록 강한 산탄 · 한 발씩 장전해요 (탄 8)", "자동 연사 · 반동 주의 · 장전이 길어요 (탄 30)", "한 방이 아주 강하고 관통 · 연사·장전이 느려요 (탄 5)" };
	private static readonly string[] GU = { "필살 · 난사 (적을 알아서 연속 사격)", "필살 · 폭풍 산탄 (모두 날려 보내요)", "필살 · 집중 사격 (탄 무한 · 2배 속사)", "필살 · 관통 레이저 (모든 걸 꿰뚫어요)" };
	private static readonly Color[] GC = { Color.FromArgb(120, 200, 255), Color.FromArgb(255, 170, 90), Color.FromArgb(150, 230, 130), Color.FromArgb(220, 150, 255) };
	private static readonly float[] Rate = { 0.14f, 0.75f, 0.09f, 1.1f };
	private static readonly int[] Mag = { 24, 8, 30, 5 };
	private static readonly float[] Rel = { 0.9f, 0.34f, 1.6f, 2.2f };
	private static readonly float[] Kick = { 3.5f, 13f, 3f, 17f };       // 반동 (총이 무거울수록 커요)
	private static readonly float[] GLen = { 36f, 76f, 70f, 98f };
	private static readonly float[] Mass = { 1f, 1.8f, 5f };              // 적 무게
	private readonly List<En> en = new List<En>(); private readonly List<Ob> ob = new List<Ob>(); private readonly List<Tr> tr = new List<Tr>();
	private readonly List<Cs> cas = new List<Cs>(); private readonly List<Sm> smk = new List<Sm>(); private readonly List<Dy> dead = new List<Dy>(); private readonly List<Rg> rings = new List<Rg>();
	private bool sel; private int gun, lives, ammo, kills, hand, shots; private float hurtT, peek, cd, reloadP, gauge, el, spawnT, obT, ultT, beamT, flash, msgT, aimX = 200f, aimY = 200f, recoil, flashT, kick, camK, ox = OX, oy = OY, pop; private bool reloading, ultAim, pierceObs; private string msg = ""; private float beamAng;
	public override string Costume { get { return null; } }
	public override string Name { get { return "마룡이 방어선"; } }
	public override string Key { get { return "cover"; } }
	public override string Desc { get { return "벽 뒤에서 몸을 내밀어 쏴요"; } }
	public override bool NoLimit { get { return true; } }
	public override bool Crosshair { get { return true; } }
		public override string Help { get { return "먼저 총을 골라요 (쌍권총·샷건·돌격소총·저격총)\n마우스를 누르고 있으면 벽 위로 올라와 (뒷모습) 쏘고, 떼면 벽 앞에 숨어서 (앞모습) 장전해요\n왼쪽에서 오른쪽으로 걸어가는 적을 막아요 · 오른쪽 끝에 닿으면 하트 -1\n상자 뒤의 적은 총알이 막혀요 · 게이지가 차면 스페이스(또는 필살 버튼)로 필살기"; } }
	public override string RightText { get { return "하트 " + lives + " · 처치 " + kills; } }
	public override void Icon(Graphics g, float cx, float cy)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 130, 110))) g.FillRectangle(b, cx - 15, cy + 2, 30, 13);
		using (Pen p = new Pen(Color.FromArgb(240, 80, 70), 2.5f)) { g.DrawEllipse(p, cx - 9, cy - 15, 18, 18); g.DrawLine(p, cx - 13, cy - 6, cx + 13, cy - 6); g.DrawLine(p, cx, cy - 18, cx, cy + 6); }
	}
	public override void Reset() { en.Clear(); ob.Clear(); tr.Clear(); cas.Clear(); smk.Clear(); dead.Clear(); rings.Clear(); fqs.Clear(); debs.Clear(); holes.Clear(); sel = true; hurtT = 0; lives = 3; kills = 0; Score = 0; Over = false; peek = 0; cd = 0; reloadP = 0; gauge = 0; el = 0; spawnT = 1f; obT = 22f; ultT = 0; beamT = 0; flash = 0; msg = ""; msgT = 0; reloading = false; ammo = 0; recoil = 0; kick = 0; camK = 0; pop = 0; Fx.Clear(); }
	private void Start(int i) { gun = i; sel = false; ammo = Mag[i]; msg = GN[i] + " 장착!"; msgT = 1.2f; Sfx.Play("ok2"); }
	private static RectangleF Card(int i) { return new RectangleF(24 + (i % 2) * 180, 112 + (i / 2) * 208, 172, 200); }
	private static RectangleF UltBtn { get { return new RectangleF(318, 534, 70, 52); } }
	public override void Click(float x, float y)
	{
		if (sel) { for (int i = 0; i < 4; i++) if (Card(i).Contains(x, y)) { Start(i); return; } return; }
		if (UltBtn.Contains(x, y)) Ult();
	}
	public override void Press(Keys k)
	{
		if (sel) { if (k >= Keys.D1 && k <= Keys.D4) Start(k - Keys.D1); return; }
		if (k == Keys.Space || k == Keys.R) Ult();
	}
	private void Ult()
	{
		if (sel || gauge < 100f || ultT > 0f || Over) return;
		gauge = 0f; Sfx.Play("fanfare"); flash = 0.5f; fqs.Add(new Fq { X = OX, Y = OY, T = 0.5f, T0 = 0.5f, W = 190f, N = "e_skill" }); Say(new[] { "난사!", "폭풍 산탄!", "집중 사격!", "관통 레이저!" }[gun]);
		if (gun == 0) ultT = 3.6f;
		else if (gun == 2) { ultT = 3.5f; ammo = Mag[2]; }
		else if (gun == 3) { ultT = 1.0f; ultAim = true; }
		else
		{
			Shake = 0.8f; camK = 14f; Sfx.Play("thud2"); rings.Add(new Rg { X = OX, Y = OY, R = 20f, Rmax = 520f, T = 0.5f, T0 = 0.5f, C = Color.FromArgb(255, 200, 110) });
			foreach (En e in en) { Hurt(e, 48f, 160f, Color.FromArgb(255, 190, 90)); }
			for (int i = 0; i < 14; i++) tr.Add(new Tr { X1 = OX, Y1 = OY, X2 = OX + (i - 6.5f) * 70f, Y2 = 180f, T = 0.22f, T0 = 0.22f, Wd = 3f, Len = 400f, C = Color.FromArgb(255, 200, 110) });
			foreach (Ob o in ob) { o.Hp -= 30f; o.Hit = 0.2f; }
			Sweep();
		}
	}
	private float Sc(float y) { return 0.72f + (y - 290f) / 110f * 0.45f; }
	private void Hurt(En e, float d, float imp, Color c)
	{
		e.Hp -= d; e.Hit = 0.14f; if (fqs.Count < 14) fqs.Add(new Fq { X = e.X, Y = e.Y - e.R, T = 0.18f, T0 = 0.18f, W = 26f + Math.Min(30f, d * 0.5f), N = d > 40f ? "e_atk" : "e_hit" }); e.Kv += imp / Mass[e.T]; e.Sq = 1f; gauge = Math.Min(100f, gauge + d * 0.025f); Fx.Burst(e.X, e.Y - e.R, c, 5);
	}
	private void Sweep()
	{
		for (int i = en.Count - 1; i >= 0; i--) if (en[i].Hp <= 0f) Kill(en[i]);
		ob.RemoveAll(delegate(Ob o) { if (o.Hp > 0f) return false; debs.Add(new Db { X = o.X + o.W / 2, Y = o.Y + o.H, T = 1.0f, Kind = o.Kind }); Fx.Burst(o.X + o.W / 2, o.Y + o.H / 2, Color.FromArgb(190, 150, 100), 12); Shake = Math.Max(Shake, 0.15f); for (int i = 0; i < 6; i++) smk.Add(new Sm { X = o.X + (float)Rnd.NextDouble() * o.W, Y = o.Y + o.H * 0.7f, Vx = ((float)Rnd.NextDouble() - 0.5f) * 40f, Vy = -20f - (float)Rnd.NextDouble() * 30f, R = 8f, T = 0.7f, L = 0.7f }); return true; });
	}
	private void Kill(En e)
	{
		kills++; Score += e.T == 2 ? 40 : (e.T == 1 ? 15 : 10); gauge = Math.Min(100f, gauge + (e.T == 2 ? 9f : 3.5f)); Fx.Burst(e.X, e.Y - e.R, e.T == 2 ? Color.FromArgb(255, 120, 90) : Color.FromArgb(255, 220, 120), e.T == 2 ? 18 : 9); Sfx.Play("coin"); if (e.T == 2) { Shake = Math.Max(Shake, 0.35f); camK = Math.Max(camK, 6f); }
		float k = Math.Max(60f, 260f / Mass[e.T] * 0.7f + Math.Abs(e.Kv) * 0.4f);
		dead.Add(new Dy { X = e.X, Y = e.Y, Vx = Math.Min(160f, k * 0.6f), Vy = -(180f + (float)Rnd.NextDouble() * 120f) / (e.T == 2 ? 1.8f : 1f), Vr = 8f + (float)Rnd.NextDouble() * 8f, T = 1.5f, Ty = e.T, Rad = e.R }); en.Remove(e);
	}
	private void Smoke(float x, float y, int n, float sp, float r)
	{
		for (int i = 0; i < n; i++) smk.Add(new Sm { X = x, Y = y, Vx = ((float)Rnd.NextDouble() - 0.3f) * sp, Vy = -((float)Rnd.NextDouble() * sp * 0.7f + 6f), R = r * (0.6f + (float)Rnd.NextDouble() * 0.6f), T = 0.55f, L = 0.55f });
	}
	// 총알 한 발: (ox,oy)에서 각도 a 방향으로 쏴요. 상자는 막고 적은 맞아요
	private void Shot(float a, float dmg, bool pierce, float range, float falloff, float width, Color c, bool crit, float imp)
	{
		float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a); float tEnd = range;
		if (!pierceObs) foreach (Ob o in ob) { float t = RectT(o, dx, dy); if (t > 0f && t < tEnd) tEnd = t; }
		List<En> hits = new List<En>(); List<float> ts = new List<float>();
		foreach (En e in en)
		{
			float cx = e.X - ox, cy = (e.Y - e.R) - oy; float t = cx * dx + cy * dy; if (t < 0f || t > tEnd) continue; float px = cx - dx * t, py = cy - dy * t; if (px * px + py * py > (e.R + width) * (e.R + width)) continue;
			int i = 0; while (i < ts.Count && ts[i] < t) i++; ts.Insert(i, t); hits.Insert(i, e);
		}
		float tHit = tEnd;
		for (int i = 0; i < hits.Count; i++)
		{
			En e = hits[i]; float d = dmg; if (falloff > 0f) d *= Math.Max(0.3f, 1f - ts[i] / falloff);
			bool head = (oy + dy * ts[i]) < e.Y - e.R * 1.3f; if (head) d *= 2f;
			Hurt(e, d, imp * (head ? 1.4f : 1f), head ? Color.FromArgb(255, 240, 120) : Color.FromArgb(255, 150, 130));
			Fx.Burst(ox + dx * ts[i], oy + dy * ts[i], head ? Color.FromArgb(255, 240, 120) : Color.FromArgb(255, 170, 140), head ? 7 : 3);
			if (crit || head) rings.Add(new Rg { X = ox + dx * ts[i], Y = oy + dy * ts[i], R = 4f, Rmax = head ? 46f : 30f, T = 0.25f, T0 = 0.25f, C = head ? Color.FromArgb(255, 240, 120) : Color.FromArgb(255, 200, 200) });
			if (head && dmg > 40f) { Say("정곡!"); Shake = Math.Max(Shake, 0.25f); } else if (head && fqs.Count < 14) fqs.Add(new Fq { X = e.X, Y = e.Y - e.R * 2f, T = 0.22f, T0 = 0.22f, W = 30f, N = "e_atk" });
			tHit = ts[i]; if (!pierce) break;
		}
		if (!pierce && hits.Count == 0) { foreach (Ob o in ob) { float t = RectT(o, dx, dy); if (t > 0f && Math.Abs(t - tEnd) < 0.5f) { o.Hp -= dmg; o.Hit = 0.1f; Fx.Burst(ox + dx * t, oy + dy * t, Color.FromArgb(200, 170, 120), 3); if (holes.Count < 24) holes.Add(new Db { X = ox + dx * t, Y = oy + dy * t, T = 2.5f, Kind = 0 }); } } }
		float tl = pierce ? tEnd : tHit; tr.Add(new Tr { X1 = ox, Y1 = oy, X2 = ox + dx * tl, Y2 = oy + dy * tl, T = pierce ? 0.28f : 0.1f, T0 = pierce ? 0.28f : 0.1f, Wd = width > 6f ? width : (dmg > 40f ? 3.2f : 1.7f), Len = tl, C = c, Slow = dmg > 40f });
	}
	private float RectT(Ob o, float dx, float dy)
	{
		float t0 = 0f, t1 = 2000f;
		float[] bo = { o.X - ox, o.Y - oy }, bs = { o.W, o.H }, d = { dx, dy };
		for (int i = 0; i < 2; i++)
		{
			if (Math.Abs(d[i]) < 1e-5f) { if (bo[i] > 0f || bo[i] + bs[i] < 0f) return -1f; continue; }
			float a = bo[i] / d[i], b = (bo[i] + bs[i]) / d[i]; if (a > b) { float q = a; a = b; b = q; }
			t0 = Math.Max(t0, a); t1 = Math.Min(t1, b); if (t0 > t1) return -1f;
		}
		return t0;
	}
	private float AimAng() { float a = (float)Math.Atan2(aimY - OY, aimX - OX); if (a > -0.12f) a = -0.12f; if (a < -3.02f) a = -3.02f; return a; }
	// 총을 쥔 손 위치와 총구 위치
	private float ShootBottom() { float e1 = peek * peek * (3f - 2f * peek); float hop = (float)Math.Sin(Math.Min(1f, peek) * Math.PI) * 10f + (pop > 0f ? pop * 30f : 0f); return WallTop + 100f + (WallTop + 30f - (WallTop + 100f)) * e1 - hop; }
	private void Hold(int h, float a, out float hx, out float hy, out float mx, out float my)
	{
		float sgn = aimX < OX ? -1f : 1f; if (gun == 0 && h == 1) sgn = -sgn; float w = gun == 0 ? 108f : 111f; mx = OX + sgn * (w * 0.48f - kick * 0.4f); my = ShootBottom() - 150f * (1f - (gun == 0 ? 0.41f : 0.49f)) - kick * 0.15f; hx = mx - sgn * 30f; hy = my + 8f;
	}
	private void Fire(float spread, float dmgK, bool free)
	{
		float a = AimAng(); if (spread > 0f) a += ((float)Rnd.NextDouble() - 0.5f) * 2f * spread + (gun == 2 ? recoil * ((float)Rnd.NextDouble() - 0.5f) : 0f);
		cd = (gun == 2 && ultT > 0f ? Rate[2] / 2.0f : Rate[gun]); if (!free) ammo--; shots++; flashT = gun == 3 ? 0.1f : (gun == 1 ? 0.09f : 0.055f); recoil = Math.Min(0.09f, recoil + (gun == 2 ? 0.01f : 0f));
		hand ^= 1; float hx, hy, mx, my; Hold(gun == 0 ? hand : 0, a, out hx, out hy, out mx, out my); float a1 = (float)Math.Atan2(aimY - my, aimX - mx); if (a1 > -0.12f) a1 = -0.12f; if (a1 < -3.02f) a1 = -3.02f; a += a1 - AimAng(); Hold(gun == 0 ? hand : 0, a, out hx, out hy, out mx, out my); ox = mx; oy = my;
		kick = Math.Max(kick, Kick[gun]); camK = Math.Max(camK, Kick[gun] * 0.35f);
		switch (gun)
		{
			case 0: Shot(a, 9f * dmgK, false, 700f, 0f, 4f, GC[0], false, 30f); if ((shots & 1) == 0) Sfx.Play("tick"); break;
			case 1: for (int i = 0; i < 9; i++) Shot(a + ((float)Rnd.NextDouble() - 0.5f) * 0.3f, 9f * dmgK, false, 420f, 640f, 6f, GC[1], false, 34f); Shake = Math.Max(Shake, 0.24f); Sfx.Play("thud2"); break;
			case 2: Shot(a, 9.5f * dmgK, false, 700f, 0f, 3f, GC[2], false, 18f); if ((shots & 1) == 0) Sfx.Play("tick"); break;
			default: pierceObs = true; Shot(a, 120f, true, 900f, 0f, 6f, GC[3], true, 190f); pierceObs = false; Shake = Math.Max(Shake, 0.32f); Sfx.Play("bong"); break;
		}
		// 탄피 · 연기 · 총구 불꽃
		if (gun != 3 || true) cas.Add(new Cs { X = hx + 6f, Y = hy - 6f, Vx = 70f + (float)Rnd.NextDouble() * 80f, Vy = -(170f + (float)Rnd.NextDouble() * 90f), R = 0f, Vr = 14f + (float)Rnd.NextDouble() * 10f, T = 1.3f, Big = gun == 1 || gun == 3 });
		Smoke(mx, my, gun == 1 ? 6 : (gun == 3 ? 5 : 1), gun == 1 ? 90f : 40f, gun == 1 ? 10f : 6f);
	}
	public override void Update(float dt, GameInput inp, double t)
	{
		aimX = inp.Mx; aimY = inp.My;
		for (int i = cas.Count - 1; i >= 0; i--) { Cs c = cas[i]; c.T -= dt; c.Vy += 900f * dt; c.X += c.Vx * dt; c.Y += c.Vy * dt; c.R += c.Vr * dt; if (c.Y > WallTop + 4f && c.Vy > 0f) { c.Y = WallTop + 4f; c.Vy = -c.Vy * 0.35f; c.Vx *= 0.5f; c.Vr *= 0.4f; } if (c.T <= 0f) cas.RemoveAt(i); }
		for (int i = smk.Count - 1; i >= 0; i--) { Sm s = smk[i]; s.T -= dt; s.X += s.Vx * dt; s.Y += s.Vy * dt; s.Vx *= 0.96f; s.R += dt * 22f; if (s.T <= 0f) smk.RemoveAt(i); }
		for (int i = dead.Count - 1; i >= 0; i--) { Dy d = dead[i]; d.T -= dt; d.X += d.Vx * dt; d.Vx *= (float)Math.Exp(-7f * dt); if (d.T <= 0f) dead.RemoveAt(i); }
		for (int i = holes.Count - 1; i >= 0; i--) { holes[i].T -= dt; if (holes[i].T <= 0f) holes.RemoveAt(i); }
		for (int i = debs.Count - 1; i >= 0; i--) { debs[i].T -= dt; if (debs[i].T <= 0f) debs.RemoveAt(i); }
		for (int i = fqs.Count - 1; i >= 0; i--) { fqs[i].T -= dt; if (fqs[i].T <= 0f) fqs.RemoveAt(i); }
		for (int i = rings.Count - 1; i >= 0; i--) { rings[i].T -= dt; if (rings[i].T <= 0f) rings.RemoveAt(i); }
		for (int i = tr.Count - 1; i >= 0; i--) { tr[i].T -= dt; tr[i].Age += dt; if (tr[i].T <= 0f) tr.RemoveAt(i); }
		kick = Math.Max(0f, kick - dt * 70f - kick * dt * 9f); camK = Math.Max(0f, camK - dt * 40f - camK * dt * 8f); if (pop > 0f) pop -= dt; if (hurtT > 0f) hurtT -= dt;
		if (sel) return;
		el += dt; if (cd > 0f) cd -= dt; if (msgT > 0f) msgT -= dt; if (flash > 0f) flash -= dt; if (flashT > 0f) flashT -= dt; if (recoil > 0f) recoil = Math.Max(0f, recoil - dt * 0.1f);
		bool onBtn = UltBtn.Contains(inp.Mx, inp.My); bool hold = inp.Hold && !onBtn; if (inp.R && gauge >= 100f) Ult();
		bool autoUlt = ultT > 0f && (gun == 0 || gun == 2);
		bool want = (hold || autoUlt) && !(ammo <= 0 && ultT <= 0f);   // 잔탄이 있으면 장전 중에도 몸을 내밀어 쏠 수 있어요 (탄이 0일 때만 끝까지 장전)
		float pk = peek; peek += ((want ? 1f : 0f) - peek) * Math.Min(1f, dt * 20f); if ((pk < 0.5f) != (peek < 0.5f)) { pop = 0.16f; Smoke(OX, WallTop + 6f, 3, 60f, 7f); }
		if (!want && ammo < Mag[gun] && ultT <= 0f && gun == 1) { reloading = true; reloadP += dt / Rel[1]; if (reloadP >= 1f) { ammo++; reloadP = 0f; Sfx.Play("tick"); if (ammo >= Mag[1]) reloading = false; } }   // 샷건은 한 발씩 채워요 (중간에 몸을 내밀어 쏠 수도 있어요)
		else if (!want && ammo < Mag[gun] && ultT <= 0f) { reloading = true; reloadP += dt / Rel[gun]; if (reloadP >= 1f) { ammo = Mag[gun]; reloadP = 0f; reloading = false; Sfx.Play("tick"); } }
		else if (want) { reloading = false; if (gun != 1) reloadP = 0f; }
		if (ultT > 0f)
		{
			ultT -= dt;
			if (gun == 0 && cd <= 0f && en.Count > 0) { En tg = en[0]; foreach (En e in en) if (e.X > tg.X) tg = e; float a0 = (float)Math.Atan2((tg.Y - tg.R) - OY, tg.X - OX); aimX = OX + (float)Math.Cos(a0) * 200f; aimY = OY + (float)Math.Sin(a0) * 200f; Fire(0.02f, 1.3f, true); cd = 0.06f; Sweep(); }
			if (gun == 2 && peek > 0.7f && cd <= 0f) { Fire(0f, 1.4f, true); Sweep(); }
			if (gun == 3 && ultAim && ultT <= 0f)
			{
				ultAim = false; beamAng = AimAng(); beamT = 0.4f; pierceObs = true; ox = OX; oy = OY - 6f; Shot(beamAng, 220f, true, 1200f, 0f, 16f, Color.FromArgb(240, 190, 255), false, 330f); pierceObs = false;
				foreach (Ob o in ob) { float tt = RectT(o, (float)Math.Cos(beamAng), (float)Math.Sin(beamAng)); if (tt > 0f) o.Hp = 0f; }
				Shake = 0.9f; camK = 20f; kick = 22f; Sfx.Play("glass_big"); rings.Add(new Rg { X = OX, Y = OY - 6f, R = 8f, Rmax = 120f, T = 0.35f, T0 = 0.35f, C = Color.FromArgb(240, 190, 255) }); Sweep();
			}
		}
		else if (gun == 3 && ultAim) ultAim = false;
		if (beamT > 0f) beamT -= dt;
		if (peek > 0.8f && cd <= 0f && ultT <= 0f && hold && ammo > 0) { Fire(gun == 0 ? 0.03f : (gun == 2 ? 0.02f + recoil : 0.0f), 1f, false); Sweep(); }
		if (!hold && !autoUlt) recoil = Math.Max(0f, recoil - dt * 0.3f);
		spawnT -= dt; if (spawnT <= 0f)
		{
			spawnT = Math.Max(0.55f, 1.8f - el * 0.009f) * (0.7f + (float)Rnd.NextDouble() * 0.6f); int n = el > 70f && Rnd.NextDouble() < 0.3 ? 2 : 1;
			for (int i = 0; i < n; i++)
			{
				double r = Rnd.NextDouble(); int ty = r < 0.45 ? 0 : (r < (el < 25f ? 0.85 : 0.75) ? 1 : (el < 15f ? 0 : 2)); if (el < 6f) ty = 0;
				float sp = ty == 0 ? 82f : (ty == 1 ? 48f : 27f); sp *= 0.85f + (float)Rnd.NextDouble() * 0.3f + Math.Min(0.3f, el * 0.002f); float hp = (ty == 0 ? 18f : (ty == 1 ? 42f : 150f)) * Math.Min(2.0f, 1f + el * 0.004f); float y = 292f + (float)Rnd.NextDouble() * 108f;
				en.Add(new En { X = -24f - i * 38f, Y = y, Hp = hp, MaxHp = hp, Sp = sp, T = ty, R = (ty == 0 ? 13f : (ty == 1 ? 16f : 23f)) * Sc(y), Ph = (float)Rnd.NextDouble() * 6f });
			}
		}
		obT -= dt; if (obT <= 0f && ob.Count < 3) { obT = 16f; bool bar = el > 35f && Rnd.NextDouble() < 0.4; float w = bar ? 72f : 58f + (float)Rnd.NextDouble() * 12f, h = bar ? 66f : w * 0.9f; float hpp = bar ? 140f : 70f; ob.Add(new Ob { X = 60f + (float)Rnd.NextDouble() * 250f, Y = 310f + (float)Rnd.NextDouble() * 50f, W = w, H = h, Hp = hpp, MaxHp = hpp, Life = 24f, Kind = bar ? 1 : 0 }); Say(bar ? "접수대 방벽이 설치됐어요! 총알이 막혀요" : "서류 상자가 쌓였어요! 총알이 막혀요"); }
		foreach (Ob o in ob) { o.Life -= dt; if (o.Hit > 0f) o.Hit -= dt; }
		ob.RemoveAll(delegate(Ob o) { return o.Life <= 0f; });
		for (int i = en.Count - 1; i >= 0; i--)
		{
			En e = en[i]; e.Ph += dt * (e.Sp / 12f); if (e.Hit > 0f) e.Hit -= dt; e.Sq = Math.Max(0f, e.Sq - dt * 6f);
			e.X += (e.Sp * (e.Hit > 0f ? 0.4f : 1f) + e.Kv) * dt; e.Kv *= (float)Math.Exp(-9f * dt);
			e.Dust -= dt; if (e.Dust <= 0f) { e.Dust = 0.22f; smk.Add(new Sm { X = e.X - e.R * 0.5f, Y = e.Y - 2f, Vx = -14f, Vy = -8f, R = 3f * e.R / 14f, T = 0.35f, L = 0.35f }); }
			if (e.X > W + 16f) { en.RemoveAt(i); lives--; hurtT = 0.7f; Shake = 0.5f; camK = 8f; Sfx.Play("bad2"); Fx.Burst(W - 14f, e.Y - e.R, Color.FromArgb(255, 110, 110), 10); Say("창구가 뚫렸어요!"); if (lives <= 0) { Over = true; return; } }
		}
		Sweep();
	}
	private void Say(string s) { msg = s; msgT = 1.2f; }
	private static void Rr(Graphics g, Brush b, float x, float y, float w, float h, float r)
	{
		if (r * 2f > h) r = h / 2f; if (r * 2f > w) r = w / 2f; using (GraphicsPath p = new GraphicsPath()) { p.AddArc(x, y, r * 2, r * 2, 180, 90); p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90); p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90); p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure(); g.FillPath(b, p); }
	}
	private static void Steel(Graphics g, float x, float y, float w, float h, float r, int tone)
	{
		using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y - 0.5f, w, h + 1f), Color.FromArgb(Math.Min(255, 150 + tone), 158 + tone, 176 + tone), Color.FromArgb(44 + tone / 2, 50 + tone / 2, 64 + tone / 2), 90f)) Rr(g, b, x, y, w, h, r);
	}
	private static void Wood(Graphics g, float x, float y, float w, float h, float r)
	{
		using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y - 0.5f, w, h + 1f), Color.FromArgb(186, 126, 78), Color.FromArgb(104, 62, 38), 90f)) Rr(g, b, x, y, w, h, r);
	}
	// 총 그림 (뒤쪽 끝이 원점, 앞쪽이 +x). 정면에서 본 옆모습이에요
	private void DrawGun(Graphics g, int k, float x, float y, float ang, float sc, Color acc)
	{
		GraphicsState st = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(ang * 180f / (float)Math.PI); g.ScaleTransform(sc, sc);
		if (k == 0 || k == 2)
		{
			Bitmap gb = Spr.Get(k == 0 ? "ms_g_pistol" : "ms_g_rifle");
			if (gb != null) { float gw = k == 0 ? 40f : 74f, gh = gw * gb.Height / gb.Width; g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(gb, k == 0 ? -4f : -6f, k == 0 ? -10f : -gh * 0.52f, gw, gh); g.Restore(st); return; }
		}
		using (SolidBrush acb = new SolidBrush(acc)) using (SolidBrush dk = new SolidBrush(Color.FromArgb(34, 38, 50))) using (SolidBrush lt = new SolidBrush(Color.FromArgb(220, 228, 240)))
		{
			if (k == 0)
			{
				Rr(g, dk, 3, 1, 11, 16, 3); Rr(g, dk, 12, 5, 8, 3, 1.2f);
				Steel(g, 0, -8, 34, 10, 3, 0); Rr(g, acb, 6, -5.5f, 22, 2.2f, 1f); Steel(g, 32, -6, 6, 6, 1.5f, -20); g.FillRectangle(dk, 33, -10, 2, 3); g.FillRectangle(dk, 3, -10, 5, 3);
				g.FillRectangle(lt, 1, -7.5f, 31, 1f);
			}
			else if (k == 1)
			{
				Wood(g, -26, -4, 30, 14, 4); Rr(g, dk, -2, 8, 7, 11, 2.5f);
				Steel(g, 0, -9, 26, 13, 3, 0); Steel(g, 16, -7, 60, 5, 2, 10); Steel(g, 16, -1.5f, 50, 4.5f, 2, -25); Wood(g, 34, -1, 22, 9.5f, 3.5f);
				Steel(g, 72, -8.5f, 5, 8, 1.5f, -10); Rr(g, acb, 3, -7, 18, 2.4f, 1f); g.FillRectangle(lt, 18, -6.6f, 56, 1f);
				g.FillRectangle(dk, 8, 4, 8, 2);
			}
			else if (k == 2)
			{
				Steel(g, -24, -7, 26, 9, 3, -30); Rr(g, dk, 10, 4, 7, 15, 2.5f);
				using (GraphicsPath mp = new GraphicsPath()) { mp.AddPolygon(new PointF[] { new PointF(14, 3), new PointF(25, 3), new PointF(30, 23), new PointF(20, 23) }); g.FillPath(dk, mp); }
				Rr(g, acb, 17, 8, 6, 3, 1f);
				Steel(g, 0, -9, 36, 12, 3, 0); Steel(g, 34, -8, 26, 9, 2, -15); for (int i = 0; i < 4; i++) g.FillRectangle(dk, 38 + i * 5, -9.5f, 2, 2);
				Steel(g, 58, -5, 12, 3.5f, 1, 10); Steel(g, 68, -6.5f, 6, 6.5f, 1.5f, -25); Rr(g, acb, 6, -6, 24, 2.4f, 1f);
				Rr(g, dk, 8, -15, 13, 6, 2); using (SolidBrush rd = new SolidBrush(Color.FromArgb(255, 70, 70))) g.FillEllipse(rd, 18, -13.5f, 2.6f, 2.6f);
				g.FillRectangle(lt, 2, -8.5f, 32, 1f);
			}
			else
			{
				Wood(g, -32, -7, 36, 14, 5); Rr(g, dk, -22, -10, 14, 4, 2); Rr(g, dk, 0, 6, 7, 11, 2.5f);
				Steel(g, 0, -9, 34, 12, 3, 0); Steel(g, 30, -6.5f, 62, 4.5f, 2, 12); Steel(g, 88, -8, 11, 8.5f, 2, -30); for (int i = 0; i < 3; i++) g.FillRectangle(dk, 91 + i * 3, -7, 1.5f, 6.5f);
				Rr(g, dk, 8, -20, 40, 10, 4.5f); Rr(g, acb, 12, -18, 32, 2.2f, 1f); g.FillRectangle(dk, 14, -11, 3, 4); g.FillRectangle(dk, 38, -11, 3, 4);
				using (SolidBrush lens = new SolidBrush(Color.FromArgb(150, 220, 255))) { g.FillEllipse(lens, 46, -19.5f, 4, 9); g.FillEllipse(lens, 7, -18.5f, 3.5f, 7); }
				g.FillRectangle(lt, 48, -18.5f, 1.4f, 3f);
				using (Pen p = new Pen(Color.FromArgb(70, 76, 92), 2f)) { g.DrawLine(p, 52, -2, 62, 12); g.DrawLine(p, 58, -2, 70, 12); }
				g.FillRectangle(lt, 2, -8.5f, 30, 1f);
			}
		}
		g.Restore(st);
	}
	private void Flash(Graphics g, float x, float y, float ang, float R, double t)
	{
		GraphicsState st = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(ang * 180f / (float)Math.PI);
		using (GraphicsPath p = new GraphicsPath())
		{
			PointF[] pts = new PointF[12]; for (int i = 0; i < 12; i++) { float aa = i * (float)Math.PI / 6f; float rr = (i % 2 == 0 ? R : R * 0.38f) * (i == 0 ? 1.6f : 1f) * (0.85f + 0.3f * (float)Rnd.NextDouble()); pts[i] = new PointF((float)Math.Cos(aa) * rr, (float)Math.Sin(aa) * rr); }
			p.AddPolygon(pts); using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 200, 90))) g.FillPath(b, p);
		}
		using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 255, 250, 220))) g.FillEllipse(b, -R * 0.38f, -R * 0.38f, R * 0.76f, R * 0.76f);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 190, 80))) g.FillEllipse(b, -R * 1.2f, -R * 1.2f, R * 2.4f, R * 2.4f);
		g.Restore(st);
	}
	private static readonly string[] ZP = { "za", "zb", "zc" }; private static readonly int[] ZN = { 6, 6, 6 }; private static readonly float[] ZH = { 3.4f, 3.4f, 3.4f };
	private void DrawEn(Graphics g, En e)
	{
		float s = e.R / 14f, x = e.X, y = e.Y; bool hit = e.Hit > 0f; float h = e.R * ZH[e.T] * 1.15f;
		using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(sh, x - e.R * 0.9f, y - 3 * s, e.R * 1.8f, 7 * s);
		GraphicsState st = g.Save(); g.TranslateTransform(x, y); g.ScaleTransform(1f + e.Sq * 0.18f, 1f - e.Sq * 0.14f); g.TranslateTransform(-x, -y);
		float bob = Math.Abs((float)Math.Sin(e.Ph * 2f)) * 1.5f * s; Sp(g, "ms_" + ZP[e.T] + (1 + ((int)(e.Ph * 2.0f) % ZN[e.T])), x, y + 2f - bob, h, false, 1f, hit);
		g.Restore(st);
		if (e.Hp < e.MaxHp) { float w = 26f * s; float ty = y - h - 4f; using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillRectangle(b, e.X - w / 2, ty, w, 4f); using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 110, 100))) g.FillRectangle(b, e.X - w / 2, ty, w * Math.Max(0f, e.Hp / e.MaxHp), 4f); }
	}
	private void DrawBack(Graphics g, float cx, float bottom, float h, float tilt)
	{
		Bitmap bk = Spr.Get("mascot_back"); if (bk == null) return; float w = h * bk.Width / bk.Height;
		using (SolidBrush sb = new SolidBrush(Color.FromArgb(46, 0, 0, 0))) g.FillEllipse(sb, cx - w * 0.3f, bottom - 4, w * 0.6f, 9);
		System.Drawing.Image im = ImgCache.Get(bk, w * ImgCache.Scale(g), h * ImgCache.Scale(g));
		GraphicsState st = g.Save(); g.TranslateTransform(cx, bottom - h / 2); g.RotateTransform(tilt); g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(im, -w / 2, -h / 2, w, h); g.Restore(st);
	}
	public override void Draw(Graphics g, double t)
	{
		BgImg(g, "bg_street", Color.FromArgb(70, 80, 120), Color.FromArgb(210, 160, 130));
		GraphicsState world = g.Save(); if (camK > 0.1f) g.TranslateTransform(0f, camK * 0.5f * (float)Math.Sin(t * 70));
		if (Spr.Get("bg_street") == null) using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 60, 90))) for (int i = 0; i < 7; i++) { float bh = 60f + (i * 37 % 50); g.FillRectangle(b, i * 60 - 10, 190f - bh, 52, bh + 10); }
		if (Spr.Get("bg_street") == null) using (SolidBrush b = new SolidBrush(Color.FromArgb(104, 100, 112))) g.FillRectangle(b, 0, 190, W, 248);
		if (Spr.Get("bg_street") == null) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) for (int i = 0; i < 8; i++) g.FillRectangle(b, i * 56 + 10, 290, 28, 4);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 100, 90))) g.FillRectangle(b, W - 10, 190, 10, 248);
		if (sel) { g.Restore(world); DrawSelect(g, t); return; }
		float a = AimAng(), e1 = peek * peek * (3f - 2f * peek);
		bool shooting = peek >= 0.5f;
		// 쏘는 중: 벽 위로 올라온 뒷모습 (벽 뒤쪽에 그려요)
		float hop = (float)Math.Sin(Math.Min(1f, peek) * Math.PI) * 10f + (pop > 0f ? pop * 30f : 0f);
		if (shooting)
		{
			float bt = ShootBottom(); float jolt = kick * 0.5f;
			if (ultT > 0f && (gun == 0 || gun == 2)) using (SolidBrush b = new SolidBrush(Color.FromArgb(70 + (int)(40 * Math.Sin(t * 20)), gun == 0 ? 255 : 255, gun == 0 ? 220 : 140, gun == 0 ? 90 : 60))) g.FillEllipse(b, OX - 90, bt - 150, 180, 170);
			{ float sg = aimX < OX ? -1f : 1f; Sp(g, gun == 0 ? "ms_k_bpist" : "ms_k_brifle", OX - sg * kick * 0.3f, bt + jolt * 0.4f, 150f, sg < 0f, 1f, false); }
			if (gun == 0)
			{
				// 쌍권총: 반대쪽 손에도 권총을 하나 더 들고, 번갈아 양쪽에서 쏴요
				float g2x, g2y, m2x, m2y; Hold(1, a, out g2x, out g2y, out m2x, out m2y); float a2 = (float)Math.Atan2(aimY - m2y, aimX - m2x); if (a2 > -0.12f) a2 = -0.12f; if (a2 < -3.02f) a2 = -3.02f;
				float recoil2 = hand == 1 ? kick * 0.5f : 0f; GraphicsState gs2 = g.Save(); g.TranslateTransform(g2x - (float)Math.Cos(a2) * recoil2, g2y - (float)Math.Sin(a2) * recoil2);
				if (Math.Cos(a2) < 0) { g.ScaleTransform(1f, -1f); DrawGun(g, 0, 0f, 0f, -a2, 1.25f, GC[0]); } else DrawGun(g, 0, 0f, 0f, a2, 1.25f, GC[0]); g.Restore(gs2);
			}
		}
		List<object> all = new List<object>(); foreach (En e in en) all.Add(e); foreach (Ob o in ob) all.Add(o);
		all.Sort(delegate(object p, object q) { float ya = p is En ? ((En)p).Y : ((Ob)p).Y + ((Ob)p).H, yb = q is En ? ((En)q).Y : ((Ob)q).Y + ((Ob)q).H; return ya.CompareTo(yb); });
		foreach (object o in all)
		{
			if (o is En) DrawEn(g, (En)o); else { Ob b = (Ob)o; string sn = b.Kind == 1 ? "ms_barr1" : (b.Hp < b.MaxHp * 0.45f ? "ms_crate2" : "ms_crate"); using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sh, b.X - 2, b.Y + b.H - 7, b.W + 4, 12); Sp(g, sn, b.X + b.W / 2 + (b.Hit > 0f ? (float)Math.Sin(b.Hit * 90f) * 2f : 0f), b.Y + b.H + 2f, b.H + 4f, false, 1f, b.Hit > 0f); using (SolidBrush br = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillRectangle(br, b.X, b.Y - 7, b.W, 3); using (SolidBrush br = new SolidBrush(Color.FromArgb(240, 200, 100))) g.FillRectangle(br, b.X, b.Y - 7, b.W * Math.Max(0f, b.Hp / b.MaxHp), 3); }
		}
		// 쓰러지는 적 (날아가며 빙글빙글)
		foreach (Db d in debs) Sp(g, d.Kind == 1 ? "ms_barr1" : "ms_planks", d.X, d.Y + 2f, d.Kind == 1 ? 26f : 34f, false, Math.Min(1f, d.T / 0.5f), false);
		foreach (Db d in holes) DrawFx(g, "bh", d.X, d.Y, 15f, Math.Min(1f, d.T / 0.8f));
		foreach (Dy d in dead) { float el = 1.5f - d.T; int df = el < 0.1f ? 0 : (el < 0.26f ? 1 : (el < 0.44f ? 2 : (el < 0.62f ? 3 : (el < 0.85f ? 4 : 5)))); string dn = "ms_" + ZP[d.Ty] + "d" + df; Bitmap db = Spr.Get(dn); float dsc = d.Rad * ZH[d.Ty] * 1.15f / 246f; if (db != null) Sp(g, dn, d.X, d.Y + 2f, Spr.OH(dn, db) * dsc, false, Math.Min(1f, d.T / 0.4f), el < 0.05f); }
		foreach (Fq q in fqs) DrawFx(g, q.N, q.X, q.Y, q.W * (1.3f - q.T / q.T0 * 0.3f), q.T / q.T0);
		foreach (Rg r in rings) { float k = 1f - r.T / r.T0; float rr = r.R + (r.Rmax - r.R) * (1f - (1f - k) * (1f - k)); using (Pen p = new Pen(Color.FromArgb((int)(220 * (1f - k)), r.C), 2f + 4f * (1f - k))) g.DrawEllipse(p, r.X - rr, r.Y - rr, rr * 2, rr * 2); }
		// 벽
		using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, WallTop, W, 172), Color.FromArgb(150, 142, 148), Color.FromArgb(96, 90, 100), 90f)) g.FillRectangle(b, 0, WallTop, W, 172);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(176, 168, 172))) g.FillRectangle(b, 0, WallTop, W, 10);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillRectangle(b, 0, WallTop + 10, W, 4);
		using (Pen p = new Pen(Color.FromArgb(70, 60, 66, 76), 2f)) { for (int r = 0; r < 4; r++) g.DrawLine(p, 0, WallTop + 14 + r * 40, W, WallTop + 14 + r * 40); for (int r = 0; r < 4; r++) for (int i = 0; i < 9; i++) g.DrawLine(p, i * 50 + (r % 2) * 25, WallTop + 14 + r * 40, i * 50 + (r % 2) * 25, WallTop + 54 + r * 40); }
		// 숨은 모습: 벽 앞에 쭈그린 앞모습
		if (!shooting)
		{
			float bt = WallTop + 82f + (454f - (WallTop + 82f)) * e1 - hop; float sq = 1f - 0.08f * (1f - e1);
			{ int sf = Over ? 4 : (hurtT > 0f ? 3 : (reloading ? 2 : (peek > 0.12f ? 1 : 0))); string sfn = "ms_sc" + sf; if (sf == 3 && Spr.Get("ms_sh0") != null) { float hp0 = 1f - hurtT / 0.7f; sfn = "ms_sh" + (hp0 < 0.22f ? 0 : (hp0 < 0.5f ? 1 : (hp0 < 0.78f ? 2 : 3))); if (hp0 < 0.1f && Spr.Get("ms_s_hit") != null) sfn = "ms_s_hit"; } float ssc = 0.42f * (sf == 0 ? sq : 1f); float stl = Over ? 0f : (hurtT > 0f ? (float)Math.Sin(t * 40.0) * 3f : (float)Math.Sin(t * 3.0) * 1.4f); if (!Spr.Frame(g, sfn, OX, bt, ssc, stl, false)) DrawSpr(g, "k_front", OX, bt, 80f * sq, stl);  if (hurtT > 0f) Spr.Draw(g, "ms_dizzy", OX, bt - 118f + (float)Math.Sin(t * 12.0) * 2f, 54f, 30f, (float)(t * 90.0) % 360f * 0f); }
			if (reloading) { using (Pen p = new Pen(Color.FromArgb(230, 255, 230, 120), 3f)) g.DrawArc(p, OX - 30, bt - 128, 60, 60, -90, 360f * reloadP); using (Pen p = new Pen(Color.FromArgb(70, 255, 255, 255), 3f)) g.DrawEllipse(p, OX - 30, bt - 128, 60, 60); Txt(g, "장전", 9f, 700, Color.White, OX - 30, bt - 112, 60, 18, StringAlignment.Center); }
		}
		else
		{
			float gx, gy, mx2, my2; Hold(gun == 0 ? hand : 0, a, out gx, out gy, out mx2, out my2);
			if (flashT > 0f) { Bitmap fb = Spr.Get(gun == 1 ? "ms_fl2" : (gun == 3 ? "ms_fl3" : "ms_fl1")); if (fb != null) { float fw = gun == 1 ? 62f : (gun == 3 ? 54f : (gun == 0 ? 34f : 40f)), fh = fw * fb.Height / fb.Width; GraphicsState fs = g.Save(); g.TranslateTransform(mx2, my2); g.RotateTransform(a * 57.2958f); g.DrawImage(fb, fw * 0.1f, -fh / 2f, fw, fh); g.Restore(fs); } }
		}
		// 연기 · 탄피 · 총알 궤적
		foreach (Sm s in smk) { float k = Math.Max(0f, Math.Min(1f, s.T / s.L)); if (Spr.Get("ms_sm0") != null) DrawFx(g, "sm" + Math.Min(5, (int)((1f - k) * 6f)), s.X, s.Y, s.R * 3.4f, 0.9f); else using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(110 * k), 205, 205, 215))) g.FillEllipse(b, s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2); }
		foreach (Cs c in cas) { Bitmap cb = Spr.Get(c.Big ? "ms_cs1" : "ms_cs3"); if (cb == null) continue; float cw = c.Big ? 8f : 3.4f, ch = cw * cb.Height / cb.Width; GraphicsState st = g.Save(); g.TranslateTransform(c.X, c.Y); g.RotateTransform(c.R * 57.3f); g.DrawImage(cb, -cw / 2, -ch / 2, cw, ch); g.Restore(st); }
		foreach (Tr q in tr)
		{
			float L = q.Len; if (L < 1f) continue; float ux = (q.X2 - q.X1) / L, uy = (q.Y2 - q.Y1) / L; float head = Math.Min(L, q.Age * (q.Slow ? 3200f : 2400f)), tail = Math.Max(0f, head - (q.Slow ? 260f : 110f));
			if (q.Wd > 6f) { float fa = Math.Max(0f, q.T / q.T0); foreach (float wd in new[] { q.Wd * 2.4f, q.Wd * 1.4f, q.Wd * 0.5f }) using (Pen p = new Pen(Color.FromArgb((int)(150 * fa), wd > q.Wd ? q.C : Color.White), wd)) g.DrawLine(p, q.X1, q.Y1, q.X2, q.Y2); continue; }
			if (q.Slow) using (Pen p = new Pen(Color.FromArgb((int)(90 * Math.Max(0f, q.T / q.T0)), q.C), 1.4f)) g.DrawLine(p, q.X1, q.Y1, q.X1 + ux * head, q.Y1 + uy * head);
			using (Pen p = new Pen(Color.FromArgb(240, q.C), q.Wd)) g.DrawLine(p, q.X1 + ux * tail, q.Y1 + uy * tail, q.X1 + ux * head, q.Y1 + uy * head);
			using (Pen p = new Pen(Color.FromArgb(255, 255, 255, 255), Math.Max(1f, q.Wd * 0.5f))) g.DrawLine(p, q.X1 + ux * Math.Max(tail, head - 30f), q.Y1 + uy * Math.Max(tail, head - 30f), q.X1 + ux * head, q.Y1 + uy * head);
		}
		if (ultAim && gun == 3) { float cs = 1f - ultT / 1.0f; float hx, hy, mx, my; Hold(0, a, out hx, out hy, out mx, out my); using (Pen p = new Pen(Color.FromArgb(90 + (int)(120 * cs), 230, 170, 255), 1.5f + 3f * cs)) g.DrawLine(p, mx, my, mx + (float)Math.Cos(a) * 700f, my + (float)Math.Sin(a) * 700f); float rr = 6f + 22f * cs; using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 240, 200, 255))) g.FillEllipse(b, mx - rr, my - rr, rr * 2, rr * 2); }
		g.Restore(world);
		if (flash > 0f) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * flash / 0.5f), 255, 255, 255))) g.FillRectangle(b, 0, 0, W, H);
		// HUD
		for (int i = 0; i < 3; i++) using (SolidBrush b = new SolidBrush(i < lives ? Color.FromArgb(240, 80, 100) : Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(b, 14 + i * 22, 44, 18, 16);
		DrawGun(g, gun, 14, 570, 0f, gun == 0 ? 0.7f : 0.45f, GC[gun]);
		Txt(g, GN[gun] + "  " + (ultT > 0f && gun == 2 ? "∞" : ammo + "/" + Mag[gun]) + (reloading ? "  장전 중…" : ""), 10.5f, 700, GC[gun], 14 + (gun == 0 ? 36 : 54), 558, 200, 20, StringAlignment.Near);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRectangle(b, 12, 590, 220, 6);
		using (SolidBrush b = new SolidBrush(gauge >= 100f ? Color.FromArgb(255, 220, 80) : GC[gun])) g.FillRectangle(b, 12, 590, 220 * gauge / 100f, 6);
		RectangleF ub = UltBtn; bool rdy = gauge >= 100f;
		using (SolidBrush b = new SolidBrush(rdy ? Color.FromArgb((int)(190 + 60 * Math.Sin(t * 8)), 255, 200, 70) : Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(b, ub.X, ub.Y, ub.Width, ub.Height);
		Txt(g, rdy ? "필살!" : (int)gauge + "%", 12f, 700, rdy ? Color.FromArgb(60, 40, 10) : Color.White, ub.X, ub.Y + 14, ub.Width, 24, StringAlignment.Center);
		if (msgT > 0f) Txt(g, msg, 15f, 700, Color.FromArgb(255, 245, 190), 0, 130, W, 28, StringAlignment.Center);
	}
	private void DrawSelect(Graphics g, double t)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 12, 30))) g.FillRectangle(b, 0, 0, W, H);
		Txt(g, "무기를 골라요", 17f, 2, Color.FromArgb(255, 240, 190), 0, 40, W, 30, StringAlignment.Center);
		Txt(g, "벽 뒤에 숨어 있다가 몸을 내밀어 막아요", 10.5f, 0, Color.White, 0, 76, W, 22, StringAlignment.Center);
		string[] pn = { "ms_k_pistol", "ms_k_shotgun", "ms_k_rifle", "ms_k_sniper" };
		for (int i = 0; i < 4; i++)
		{
			RectangleF c = Card(i); bool hov = c.Contains(aimX, aimY);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(hov ? 230 : 190, 30, 36, 64))) g.FillRectangle(b, c.X, c.Y, c.Width, c.Height);
			using (Pen p = new Pen(hov ? GC[i] : Color.FromArgb(120, GC[i]), hov ? 3f : 1.5f)) g.DrawRectangle(p, c.X, c.Y, c.Width, c.Height);
			float cx = c.X + c.Width / 2f;
			using (SolidBrush b = new SolidBrush(Color.FromArgb(hov ? 60 : 28, GC[i]))) g.FillEllipse(b, cx - 56, c.Y + 54, 112, 36);
			Sp(g, pn[i], cx - 6f, c.Y + 94f - (hov ? 3f * (float)Math.Abs(Math.Sin(t * 6)) : 0f), 90f, false, 1f, false);
			Txt(g, (i + 1) + "  " + GN[i], 13f, 2, Color.White, c.X, c.Y + 100, c.Width, 22, StringAlignment.Center);
			Txt(g, GD[i], 9.5f, 0, Color.FromArgb(215, 220, 240), c.X + 8, c.Y + 126, c.Width - 16, 38, StringAlignment.Center, true);
			Txt(g, GU[i], 9f, 1, GC[i], c.X + 8, c.Y + 166, c.Width - 16, 30, StringAlignment.Center, true);
		}
	}
}
