using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

// 성능 측정용 (OT_BENCH=1 일 때만 동작): 쪽 이동·테마 변경·마룡이 클릭을 자동으로 하고 걸린 시간을 bench.txt 에 적어요
internal partial class Program
{
	internal static void BenchStart()
	{
		string rin = Environment.GetEnvironmentVariable("OT_BENCH_REALINPUT"); if (rin != null) { RealInputTest(rin); return; }
		string har = Environment.GetEnvironmentVariable("OT_BENCH_HOMEARRIVE"); if (har != null) { HomeArrive(har); return; }
		string hid = Environment.GetEnvironmentVariable("OT_BENCH_HOMEIDLE"); if (hid != null) { HomeIdle(hid); return; }
		string sct = Environment.GetEnvironmentVariable("OT_BENCH_SECRET"); if (sct != null) { SecretTest(sct); return; }
		string t48 = Environment.GetEnvironmentVariable("OT_BENCH_2048"); if (t48 != null) { try { Test2048(t48); } catch (Exception e9) { Directory.CreateDirectory(t48); File.WriteAllText(Path.Combine(t48, "fatal.txt"), e9.ToString()); } Environment.Exit(0); }
		string bsn = Environment.GetEnvironmentVariable("OT_BENCH_BATSHOTS"); if (bsn != null) { try { BatShots(bsn); } catch (Exception e8) { Directory.CreateDirectory(bsn); File.WriteAllText(Path.Combine(bsn, "fatal.txt"), e8.ToString()); } Environment.Exit(0); }
		string rf = Environment.GetEnvironmentVariable("OT_BENCH_REALFPS"); if (rf != null) { Timer tl4 = new Timer(); tl4.Interval = 1500; tl4.Tick += delegate { tl4.Stop(); RealFps(rf); }; tl4.Start(); return; }
		string gf = Environment.GetEnvironmentVariable("OT_BENCH_GAMEFPS"); if (gf != null) { try { GameFps(gf); } catch (Exception e7) { Directory.CreateDirectory(gf); File.WriteAllText(Path.Combine(gf, "fatal.txt"), e7.ToString()); } Environment.Exit(0); }
		string cl = Environment.GetEnvironmentVariable("OT_BENCH_CLAY"); if (cl != null) { try { ClayTest(cl); } catch (Exception e6) { Directory.CreateDirectory(cl); File.WriteAllText(Path.Combine(cl, "fatal.txt"), e6.ToString()); } Environment.Exit(0); }
		string ro = Environment.GetEnvironmentVariable("OT_BENCH_REOPEN"); if (ro != null) { ReopenTest(ro); return; }
		string fr0 = Environment.GetEnvironmentVariable("OT_BENCH_FRAMES"); if (fr0 != null) { FrameTest(fr0); return; }
		string ms = Environment.GetEnvironmentVariable("OT_BENCH_MENU"); if (ms != null) { Timer tl3 = new Timer(); tl3.Interval = 1500; tl3.Tick += delegate { tl3.Stop(); MenuShots(ms); }; tl3.Start(); return; }
		string ml = Environment.GetEnvironmentVariable("OT_BENCH_MOUSE"); if (ml != null) { Timer tl2 = new Timer(); tl2.Interval = 1500; tl2.Tick += delegate { tl2.Stop(); MouseLive(ml); }; tl2.Start(); return; }
		string gl = Environment.GetEnvironmentVariable("OT_BENCH_LIVE"); if (gl != null) { Timer tl = new Timer(); tl.Interval = 1500; tl.Tick += delegate { tl.Stop(); GameLive(gl); }; tl.Start(); return; }
		string jb = Environment.GetEnvironmentVariable("OT_BENCH_JUMPBOT"); if (jb != null) { try { JumpBot(jb); } catch (Exception e1) { Directory.CreateDirectory(jb); File.WriteAllText(Path.Combine(jb, "fatal.txt"), e1.ToString()); } Environment.Exit(0); }
		string tb = Environment.GetEnvironmentVariable("OT_BENCH_TOWERBOT"); if (tb != null) { try { TowerBot(tb); } catch (Exception e2) { Directory.CreateDirectory(tb); File.WriteAllText(Path.Combine(tb, "fatal.txt"), e2.ToString()); } Environment.Exit(0); }
		string bb = Environment.GetEnvironmentVariable("OT_BENCH_BATBOT"); if (bb != null) { try { BatBot(bb); } catch (Exception e3) { Directory.CreateDirectory(bb); File.WriteAllText(Path.Combine(bb, "fatal.txt"), e3.ToString()); } Environment.Exit(0); }
		string nn = Environment.GetEnvironmentVariable("OT_BENCH_NEW"); if (nn != null) { try { NewGamesTest(nn); } catch (Exception e4) { Directory.CreateDirectory(nn); File.WriteAllText(Path.Combine(nn, "fatal.txt"), e4.ToString()); } Environment.Exit(0); }
		string sg = Environment.GetEnvironmentVariable("OT_BENCH_SLING"); if (sg != null) { try { SlingGrid(sg); } catch (Exception e5) { Directory.CreateDirectory(sg); File.WriteAllText(Path.Combine(sg, "fatal.txt"), e5.ToString()); } Environment.Exit(0); }
		string gd = Environment.GetEnvironmentVariable("OT_BENCH_GAMES"); if (gd != null) { try { GameShots(gd); } catch (Exception e0) { File.WriteAllText(Path.Combine(gd, "fatal.txt"), e0.ToString()); } Environment.Exit(0); }
		Timer t0 = new Timer(); t0.Interval = 2500;
		t0.Tick += delegate { t0.Stop(); ShowSettings(); BenchRun(); };
		t0.Start();
	}

	private static void BenchRun()
	{
		if (settingsForm != null) { settingsForm.Activate(); settingsForm.BringToFront(); }
		StringBuilder sb = new StringBuilder();
		string outPath = Path.Combine(Dir, "bench.txt");
		List<KeyValuePair<string, Action>> steps = new List<KeyValuePair<string, Action>>();
		int[] order = { 2, 1, 3, 5, 6, 7, 8, 4, 9, 0, 2, 1, 3, 5, 6, 7, 8, 4, 9, 0 };
		for (int i = 0; i < order.Length; i++) { int p = order[i]; steps.Add(new KeyValuePair<string, Action>((i < 10 ? "쪽(첫방문) " : "쪽(재방문) ") + p, delegate { settingsGo(p); }));
		}
		steps.Add(new KeyValuePair<string, Action>("홈으로", delegate { settingsGo(0); }));
		for (int i = 0; i < 4; i++) { int m = (i % 2 == 0) ? 1 : 0; steps.Add(new KeyValuePair<string, Action>("테마 " + m, delegate { SetTheme(m); })); }
		for (int i = 0; i < 5; i++) steps.Add(new KeyValuePair<string, Action>("가만히(홈) 1.2초", delegate { }));
		for (int i = 0; i < 4; i++) steps.Add(new KeyValuePair<string, Action>("마룡이 클릭", delegate { MaryongGame.Tap(); }));
		int idx = -1;
		Stopwatch gapSw = new Stopwatch();
		double maxGap = 0, sumOver = 0; int over33 = 0, ticks = 0;
		Timer mon = new Timer(); mon.Interval = 1;
		Anim.HiRes(true);
		mon.Tick += delegate
		{
			double g = gapSw.Elapsed.TotalMilliseconds; gapSw.Restart(); ticks++;
			if (g > maxGap) maxGap = g;
			if (g > 33) { over33++; sumOver += g; }
		};
		Timer step = new Timer(); step.Interval = 1200;
		double syncMs = 0;
		step.Tick += delegate
		{
			if (idx >= 0)
			{
				sb.AppendLine(steps[idx].Key.PadRight(16) + "동기 " + syncMs.ToString("F0").PadLeft(4) + "ms | 최대 끊김 " + maxGap.ToString("F0").PadLeft(4) + "ms | 33ms 넘은 틱 " + over33 + " (합 " + sumOver.ToString("F0") + "ms) / 틱 " + ticks + " | 그리기: " + PaintProf.Dump());
			}
			idx++;
			if (idx >= steps.Count)
			{
				step.Stop(); mon.Stop();
				File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
				Application.Exit();
				return;
			}
			maxGap = 0; sumOver = 0; over33 = 0; ticks = 0; PaintProf.Ms.Clear(); PaintProf.N.Clear();
			Stopwatch s = Stopwatch.StartNew();
			gapSw.Restart();
			if (!mon.Enabled) mon.Start();
			try { steps[idx].Value(); } catch (Exception ex) { sb.AppendLine("오류 " + ex.Message); }
			syncMs = s.Elapsed.TotalMilliseconds;
		};
		step.Start();
	}
}


internal static class PaintProf
{
	public static bool On = Environment.GetEnvironmentVariable("OT_BENCH") == "1";
	public static Dictionary<string, double> Ms = new Dictionary<string, double>();
	public static Dictionary<string, int> N = new Dictionary<string, int>();
	public static void Add(string k, long t0)
	{
		double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency; double v; Ms.TryGetValue(k, out v); Ms[k] = v + ms; int n; N.TryGetValue(k, out n); N[k] = n + 1;
	}
	public static string Dump()
	{
		StringBuilder s = new StringBuilder();
		foreach (KeyValuePair<string, double> kv in Ms) if (kv.Value >= 3) s.Append(kv.Key + " " + kv.Value.ToString("F0") + "ms/" + N[kv.Key] + "회, ");
		Ms.Clear(); N.Clear(); return s.ToString();
	}
}
internal partial class Program
{
	// 미니게임을 창 없이 그려서 PNG 로 저장 (OT_BENCH_GAMES=폴더)
	internal static void GameShots(string dir)
	{
		Directory.CreateDirectory(dir);
		StringBuilder log = new StringBuilder();
		Func<MiniGame>[] mk = { () => new JumpGame(), () => new BatGame(), () => new StressGame(), () => new Game2048() };
		Image mas = MascotImage();
		foreach (Func<MiniGame> f in mk)
		{
			MiniGame g = null;
			try
			{
				g = f(); g.Mascot = mas; g.Reset();
				GameInput inp = new GameInput(); inp.Mx = 200; inp.My = 200;
				double t = 0;
				for (int i = 0; i < (g is JumpGame ? 900 : 150); i++) { t += 0.016; g.Update(0.016f, inp, t); if (i == 62 && g is BatGame) g.Click(200, 440); if (g is BatGame && i >= 64 && i <= 72 && i % 4 == 0) { using (Bitmap jb = new Bitmap(400, 600)) using (Graphics jg = Graphics.FromImage(jb)) { Gfx.HQ(jg); g.Draw(jg, t); jb.Save(Path.Combine(dir, "batswing" + i + ".png"), System.Drawing.Imaging.ImageFormat.Png); } } if (g is JumpGame && i >= 60 && i % 10 == 0 && false) { using (Bitmap jb = new Bitmap(400, 600)) using (Graphics jg = Graphics.FromImage(jb)) { Gfx.HQ(jg); g.Draw(jg, t); jb.Save(Path.Combine(dir, "jumpf" + i + ".png"), System.Drawing.Imaging.ImageFormat.Png); } } if (g is StressGame && i == 100) g.Click(200, 100); if (g is JumpGame && i % 12 == 0) { g.Over = false; var of = g.GetType().GetField("obs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic); var lst = (System.Collections.IList)of.GetValue(g); foreach (object o in lst) { int k = (int)o.GetType().GetField("Kind").GetValue(o); float ox = (float)o.GetType().GetField("X").GetValue(o); if (k > 0 && ox > 150 && ox < 330 && !File.Exists(Path.Combine(dir, "jumpk" + k + ".png"))) { using (Bitmap jb = new Bitmap(400, 600)) using (Graphics jg = Graphics.FromImage(jb)) { Gfx.HQ(jg); g.Draw(jg, t); jb.Save(Path.Combine(dir, "jumpk" + k + ".png"), System.Drawing.Imaging.ImageFormat.Png); } } } } }
				for (int shot = 0; shot < 2; shot++)
				{
					using (Bitmap bm = new Bitmap(400, 600)) using (Graphics gr = Graphics.FromImage(bm))
					{
						Gfx.HQ(gr); g.Draw(gr, t);
						bm.Save(Path.Combine(dir, g.Key + shot + ".png"), System.Drawing.Imaging.ImageFormat.Png);
					}
					if (g is BatGame) { for (int i = 0; i < 12; i++) { t += 0.016; g.Update(0.016f, inp, t); } }
				}
				log.AppendLine(g.Key + " ok");
			}
			catch (Exception ex) { log.AppendLine((g != null ? g.Key : "?") + " 오류 " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); }
		}
		File.WriteAllText(Path.Combine(dir, "games.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	// 실제 오락실 창을 띄워 키 입력까지 시험 (OT_BENCH_LIVE=폴더)
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
	internal static void GameLive(string dir)
	{
		Directory.CreateDirectory(dir);
		StringBuilder log = new StringBuilder();
		MaryongGame.Open();
		Form form = null; foreach (Form of in Application.OpenForms) if (of is MaryongGame) form = of;
		if (form == null) { File.WriteAllText(Path.Combine(dir, "live.txt"), "창 없음"); Application.Exit(); return; }
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
		Type mt = typeof(MaryongGame);
		MiniGame[] games = (MiniGame[])mt.GetField("games", bf).GetValue(form);
		string[] keys = { "g2048", "snake", "jump", "bat", "shoot" };
		int gi = -1; Timer tm = new Timer(); tm.Interval = 1500; int phase = 0; MiniGame cur = null;
		Action<string> shot = delegate(string name)
		{
			using (Bitmap bm = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) { form.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(dir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png); }
		};
		tm.Tick += delegate
		{
			try
			{
				if (phase == 0)
				{
					gi++;
					if (gi >= keys.Length) { tm.Stop(); File.WriteAllText(Path.Combine(dir, "live.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); return; }
					foreach (MiniGame m in games) if (m.Key == keys[gi]) cur = m;
					mt.GetField("g", bf).SetValue(form, cur); mt.GetMethod("StartGame", bf).Invoke(form, null);
					form.Activate(); form.Focus();
					log.AppendLine("== " + cur.Key + " 시작");
					if (cur.Key == "g2048")
					{
						System.Reflection.FieldInfo bfld = cur.GetType().GetField("b", bf); int[,] bb = (int[,])bfld.GetValue(cur); int before = 0; foreach (int v in bb) before += v;
						foreach (int vk in new int[] { 37, 38, 39, 40 }) { PostMessage(form.Handle, 0x100, (IntPtr)vk, IntPtr.Zero); PostMessage(form.Handle, 0x101, (IntPtr)vk, IntPtr.Zero); Application.DoEvents(); System.Threading.Thread.Sleep(60); Application.DoEvents(); }
						int after = 0; foreach (int v in (int[,])bfld.GetValue(cur)) after += v; log.AppendLine("2048 방향키 4번 전 합 " + before + " → 후 합 " + after + " (점수 " + cur.Score + ")");
					}
					phase = 1; tm.Interval = 1600;
				}
				else { shot("live_" + cur.Key); log.AppendLine("점수 " + cur.Score + " 끝남 " + cur.Over); phase = 0; tm.Interval = 800; }
			}
			catch (Exception ex) { log.AppendLine("오류 " + ex); phase = 0; }
			tm.Start();
		};
		tm.Start();
	}
}
internal partial class Program
{
	// 점프 게임 자동 시험: 가장 가까운 장애물·구덩이 앞에서 꾹 눌러 뛰는 봇 (OT_BENCH_JUMPBOT=폴더)
	internal static void JumpBot(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder();
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		for (int run = 0; run < 5; run++)
		{
			JumpGame g = new JumpGame(); g.Mascot = MascotImage(); g.Reset(); GameInput inp = new GameInput(); double t = 0; float holdLeft = 0; bool shot = false; int pits = 0;
			System.Collections.IList obs = (System.Collections.IList)g.GetType().GetField("obs", bf).GetValue(g); if (Environment.GetEnvironmentVariable("OT_BOT_NOJUMP") != null) { Type ot = g.GetType().GetNestedType("Ob", bf); object po = Activator.CreateInstance(ot); ot.GetField("X").SetValue(po, 250f); ot.GetField("W").SetValue(po, 140f); ot.GetField("Kind").SetValue(po, 3); obs.Add(po); g.GetType().GetField("nextGap", bf).SetValue(g, 99999f); }
			for (int i = 0; i < 60 * 120 && !g.Over; i++)
			{
				float spd = (float)(g.GetType().GetField("speed", bf).GetValue(g));
				float nearest = 9999; int nk = -1;
				foreach (object o in obs) { float ox = (float)o.GetType().GetField("X").GetValue(o); float ow = (float)o.GetType().GetField("W").GetValue(o); int k = (int)o.GetType().GetField("Kind").GetValue(o); if (ox + ow > 50 && ox - 70 < nearest) { nearest = ox - 70; nk = k; } }
				bool onG = (bool)g.GetType().GetField("onGround", bf).GetValue(g);
				float lead = nk == 3 ? spd * 0.18f : spd * 0.30f;
				if (Environment.GetEnvironmentVariable("OT_BOT_NOJUMP") == null && onG && nk >= 0 && nearest > 0 && nearest < lead) { g.Click(0, 0); holdLeft = nk == 3 ? 0.50f : 0.55f; }
				inp.Hold = holdLeft > 0; holdLeft -= 0.016f;
				t += 0.016; g.Update(0.016f, inp, t);
				if (!shot && nk == 3 && nearest < 120 && nearest > 60 && run == 0) { shot = true; using (Bitmap bm = new Bitmap(400, 600)) using (Graphics gr = Graphics.FromImage(bm)) { Gfx.HQ(gr); g.Draw(gr, t); bm.Save(Path.Combine(dir, "pit.png"), System.Drawing.Imaging.ImageFormat.Png); } }
			}
			log.AppendLine("run " + run + " 낙사? " + ((bool)g.GetType().GetField("falling", bf).GetValue(g)) + ": 점수 " + g.Score + " 코인 " + g.RightText + " 시간 " + t.ToString("F1") + "초 끝남 " + g.Over);
		}
		File.WriteAllText(Path.Combine(dir, "bot.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	// 석탑 자동 시험 (OT_BENCH_TOWERBOT=폴더): 정확히 겨냥하는 봇 / 아무 때나 떨어뜨리는 봇
	internal static void TowerBot(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder();
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		string[] modes = { "정확(오차4)", "대충(오차35)", "막(아무때나)" };
		float[] tol = { 4f, 35f, 9999f };
		for (int m = 0; m < 3; m++)
		{
			Random rr = new Random(7 + m);
			for (int run = 0; run < 3; run++)
			{
				TowerGame g = new TowerGame(); g.Mascot = MascotImage(); g.Reset(); GameInput inp = new GameInput(); double t = 0; int maxFloor = 0; int shots = 0; bool shotCollapse = false;
				Type tt = g.GetType();
				Func<string, object> fld = delegate(string n) { return tt.GetField(n, bf).GetValue(g); };
				System.Collections.IList st = (System.Collections.IList)fld("st");
				for (int i = 0; i < 60 * 600 && !g.Over; i++)
				{
					bool holding = (bool)fld("holding"); float wait = (float)fld("wait");
					if (holding && wait <= 0 && (float)fld("dyingT") <= 0)
					{
						object top = st[st.Count - 1]; float tx = (float)top.GetType().GetField("X").GetValue(top), tw = (float)top.GetType().GetField("W").GetValue(top);
						float nw = (float)fld("nw"); float craneX = (float)fld("craneX"); float hx = Math.Max(6f, Math.Min(640f - 6f - nw, craneX - nw / 2f)); hx = Math.Max(6f, Math.Min(400f - 6f - nw, craneX - nw / 2f));
						float err = Math.Abs(hx + nw / 2f - (tx + tw / 2f));
						if (m == 2 ? rr.NextDouble() < 0.02 : err < tol[m]) g.Click(0, 0);
					}
					t += 0.016; g.Update(0.016f, inp, t);
					int fl2 = st.Count - 1; if (fl2 > maxFloor) maxFloor = fl2;
					if (run == 0 && m == 0 && fl2 == 7 && !shots.Equals(1) && (bool)fld("holding")) { shots = 1; using (Bitmap bm = new Bitmap(400, 600)) using (Graphics gr = Graphics.FromImage(bm)) { Gfx.HQ(gr); g.Draw(gr, t); bm.Save(Path.Combine(dir, "tower7.png"), System.Drawing.Imaging.ImageFormat.Png); } }
					if (run == 0 && m == 1 && !shotCollapse && ((System.Collections.IList)fld("fl")).Count > 0) { shotCollapse = true; for (int k = 0; k < 14; k++) { t += 0.016; g.Update(0.016f, inp, t); } using (Bitmap bm = new Bitmap(400, 600)) using (Graphics gr = Graphics.FromImage(bm)) { Gfx.HQ(gr); g.Draw(gr, t); bm.Save(Path.Combine(dir, "towerfall.png"), System.Drawing.Imaging.ImageFormat.Png); } }
				}
				log.AppendLine(modes[m] + " run" + run + ": 최고 " + maxFloor + "층, 점수 " + g.Score + ", 종료 " + g.Over + ", 걸린시간 " + t.ToString("F0") + "초");
			}
		}
		File.WriteAllText(Path.Combine(dir, "tower.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	// 야구 타이밍별 결과 분포 (OT_BENCH_BATBOT=폴더)
	internal static void BatBot(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder();
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		for (int di = -10; di <= 10; di++)
		{
			float delta = di * 0.012f; Dictionary<string, int> cnt = new Dictionary<string, int>(); double sumD = 0, sumAz = 0, sumLoft = 0, sumV = 0; int nHit = 0, pitches = 0;
			BatGame g = new BatGame(); g.Mascot = MascotImage(); g.Reset(); Type tt = g.GetType(); GameInput inp = new GameInput(); double t = 0; int guard = 0;
			while (pitches < 50 && guard++ < 400000)
			{
				if (g.Over) g.Reset();
				int ph = (int)tt.GetField("phase", bf).GetValue(g); bool sw = (bool)tt.GetField("swinging", bf).GetValue(g);
				if (ph == 1 && !sw) { float pt = (float)tt.GetField("pt", bf).GetValue(g), plate = (float)tt.GetField("ptPlate", bf).GetValue(g); if (pt >= plate - 0.095f + delta) g.Click(0, 0); }
				t += 1.0 / 120; g.Update(1f / 120f, inp, t);
				int ph2 = (int)tt.GetField("phase", bf).GetValue(g);
				if (ph == 1 && ph2 == 2) { string r = (string)tt.GetField("res", bf).GetValue(g); int c; cnt.TryGetValue(r, out c); cnt[r] = c + 1; pitches++; bool hit = (bool)tt.GetField("hit", bf).GetValue(g); if (hit) { sumD += (float)tt.GetField("landD", bf).GetValue(g); sumAz += (float)tt.GetField("azim", bf).GetValue(g); sumLoft += (float)tt.GetField("loft", bf).GetValue(g); sumV += (float)tt.GetField("vsp", bf).GetValue(g); nHit++; } }
			}
			StringBuilder sb = new StringBuilder(); foreach (KeyValuePair<string, int> kv in cnt) sb.Append(kv.Key + " " + kv.Value + ", ");
			log.AppendLine("타이밍 " + (delta * 1000).ToString("F0").PadLeft(4) + "ms: " + sb.ToString() + (nHit > 0 ? " | 평균 방향 " + (sumAz / nHit).ToString("F0") + "° · 발사각 " + (sumLoft / nHit).ToString("F0") + "° · 속도 " + (sumV / nHit).ToString("F0") + "m/s · 비거리 " + (sumD / nHit).ToString("F0") + "m" : ""));
		}
		File.WriteAllText(Path.Combine(dir, "bat.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	// 새 게임 4종 자동 시험 (OT_BENCH_NEW=폴더)
	internal static void Drag(MiniGame g, GameInput inp, ref double t, float x0, float y0, float x1, float y1)
	{
		inp.Mx = x0; inp.My = y0; inp.Hold = true; for (int i = 0; i < 3; i++) { t += 1 / 60.0; g.Update(1f / 60f, inp, t); }
		for (int i = 1; i <= 6; i++) { inp.Mx = x0 + (x1 - x0) * i / 6f; inp.My = y0 + (y1 - y0) * i / 6f; t += 1 / 60.0; g.Update(1f / 60f, inp, t); }
		inp.Hold = false; t += 1 / 60.0; g.Update(1f / 60f, inp, t);
	}
	internal static void Run(MiniGame g, GameInput inp, ref double t, double sec) { for (int i = 0; i < sec * 60 && !g.Over; i++) { t += 1 / 60.0; g.Update(1f / 60f, inp, t); } }
	private static void Snap(MiniGame g, double t, string path) { using (Bitmap bm = new Bitmap(400, 600)) using (Graphics gr = Graphics.FromImage(bm)) { Gfx.HQ(gr); g.Draw(gr, t); bm.Save(path, System.Drawing.Imaging.ImageFormat.Png); } }
	internal static void NewGamesTest(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder(); Random rr = new Random(11); Image mas = MascotImage();
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		// 농구
		{
			BasketGame g = new BasketGame(); g.Mascot = mas; g.Reset(); GameInput inp = new GameInput(); double t = 0; int tries = 400, made = 0, swish = 0;
			for (int i = 0; i < tries; i++)
			{
				g.Reset(); t = 0; Run(g, inp, ref t, 0.5);
				double th = (58 + rr.NextDouble() * 14) * Math.PI / 180, ddx = 206, ddy = 265, den = 2 * Math.Cos(th) * Math.Cos(th) * (ddx * Math.Tan(th) - ddy); double v0 = den > 0 ? Math.Sqrt(1500 * ddx * ddx / den) * (1 + (rr.NextDouble() - 0.5) * 0.05) : 800; float vx = (float)(v0 * Math.Cos(th)), vy = -(float)(v0 * Math.Sin(th));
				Drag(g, inp, ref t, 120, 505, 120 - vx / 5.2f, 505 - vy / 5.2f); Run(g, inp, ref t, 3.2);
				if (g.Score > 0) { made++; if (g.Score >= 3) swish++; }
				if (i == 0) Snap(g, t, Path.Combine(dir, "hoop.png"));
			}
			log.AppendLine("농구: 무작위 슛 " + tries + "번 중 골인 " + made + " (깨끗한 3점 " + swish + ")");
		}
			{ BatGame bg = new BatGame(); bg.Mascot = mas; bg.Reset(); GameInput bi = new GameInput(); double bt = 0; Run(bg, bi, ref bt, 1.25); Snap(bg, bt, Path.Combine(dir, "batghost.png")); Run(bg, bi, ref bt, 0.5); Snap(bg, bt, Path.Combine(dir, "batghost2.png")); }
		// 볼링
		{
			BowlGame g = new BowlGame(); g.Mascot = mas; GameInput inp = new GameInput(); double t = 0; int[] histR = new int[11], histS = new int[11]; int strikesR = 0, strikesS = 0, gutR = 0, gutS = 0; int runs = 150;
			for (int mode = 0; mode < 2; mode++)
				for (int i = 0; i < runs; i++)
				{
					g.Reset(); t = 0; inp.Hold = false; Run(g, inp, ref t, 0.6); float px = (float)(rr.NextDouble() * 70 - 35); inp.Mx = 200 + px; inp.My = 500; inp.MouseMoved = true; Run(g, inp, ref t, 0.1); inp.MouseMoved = false;
					float spin = mode == 0 ? (float)(rr.NextDouble() * 2 - 1) : Math.Max(-1f, Math.Min(1f, -px / 35f * 0.6f + (float)(rr.NextDouble() - 0.5) * 0.3f));
					inp.Hold = true; Run(g, inp, ref t, 0.05); inp.Mx = 200 + px + spin * 55f; double rel = 0.52 + rr.NextDouble() * 0.26; Run(g, inp, ref t, rel - 0.05); inp.Hold = false;
					if (i == 3 && mode == 1) { Run(g, inp, ref t, 0.75); Snap(g, t, Path.Combine(dir, "bowl_roll.png")); Run(g, inp, ref t, 0.45); Snap(g, t, Path.Combine(dir, "bowl_hit.png")); Run(g, inp, ref t, 0.5); Snap(g, t, Path.Combine(dir, "bowl_after.png")); Run(g, inp, ref t, 4); } else Run(g, inp, ref t, 7);
					int k = Math.Min(10, g.Score >= 10 ? 10 : g.Score); if (mode == 0) { histR[k]++; if (g.Score >= 20) strikesR++; if (g.Score == 0) gutR++; } else { histS[k]++; if (g.Score >= 20) strikesS++; if (g.Score == 0) gutS++; }
					if (i == 1 && mode == 1) Snap(g, t, Path.Combine(dir, "bowl.png"));
				}
			StringBuilder sb = new StringBuilder(); for (int k = 0; k <= 10; k++) sb.Append(k + ":" + histR[k] + " "); StringBuilder sb2 = new StringBuilder(); for (int k = 0; k <= 10; k++) sb2.Append(k + ":" + histS[k] + " ");
			log.AppendLine("볼링 무작위: " + sb + "| 스트라이크 " + strikesR + " · 0개 " + gutR); log.AppendLine("볼링 (위치 보정 회전): " + sb2 + "| 스트라이크 " + strikesS + " · 0개 " + gutS);
		}
		// 새총
		{
			SlingGame g = new SlingGame(); g.Mascot = mas; GameInput inp = new GameInput(); double t = 0; int kills = 0, cleared = 0, tries = 120; Type tt = g.GetType();
			g.Reset(); Run(g, inp, ref t, 4); log.AppendLine("새총: 아무것도 안 하고 4초 뒤 점수 " + g.Score + " (0이면 구조물이 스스로 무너지지 않음)");
			for (int i = 0; i < tries; i++)
			{
				g.Reset(); t = 0; Run(g, inp, ref t, 1.0); float px = (float)(40 + rr.NextDouble() * 50), py = (float)(-20 - rr.NextDouble() * 80);
				Drag(g, inp, ref t, 76, 430, 76 - px, 430 + (-py) * -1); Run(g, inp, ref t, 4.5);
				if (g.Score > 0) kills++; if (g.Score >= 200) cleared++;
				if (i == 5) Snap(g, t, Path.Combine(dir, "sling.png"));
			}
			log.AppendLine("새총: 무작위 1발 " + tries + "회 중 민원 제거 " + kills + "회, 2개 이상 " + cleared + "회");
		}
		File.WriteAllText(Path.Combine(dir, "new.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	internal static void SlingGrid(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder(); Image mas = MascotImage(); int tot = 0, ok = 0, ok2 = 0; StringBuilder rows = new StringBuilder();
		for (int wave = 1; wave <= 2; wave++)
		{
			tot = 0; ok = 0; ok2 = 0;
			for (int len = 50; len <= 90; len += 10)
			{
				rows.Append("wave" + wave + " 당김 " + len + ": ");
				for (int ang = 10; ang <= 70; ang += 5)
				{
					SlingGame g = new SlingGame(); g.Mascot = mas; GameInput inp = new GameInput(); double t = 0; Random r = new Random(5 + ang); g.Reset();
					if (wave == 2) { Type tt = g.GetType(); for (int k = 0; k < 1; k++) { tt.GetMethod("NewWave", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(g, null); } g.Score = 0; }
					Run(g, inp, ref t, 1.0); double rad = ang * Math.PI / 180.0; float ex = 76 - (float)(Math.Cos(rad) * len), ey = 430 + (float)(Math.Sin(rad) * len);
					Drag(g, inp, ref t, 76, 430, ex, ey); Run(g, inp, ref t, 5.0);
					tot++; if (g.Score > 0) ok++; if (g.Score >= 200) ok2++; rows.Append(g.Score + " ");
				}
				rows.AppendLine();
			}
			log.AppendLine("wave" + wave + ": 격자 " + tot + "발 중 민원 제거 " + ok + ", 2개 이상 " + ok2);
		}
		File.WriteAllText(Path.Combine(dir, "sling.txt"), log.ToString() + rows.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	[System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "PostMessage")] private static extern bool PostMessage3(IntPtr h, int m, IntPtr w, IntPtr l);
	// 실제 오락실 창에서 마우스 끌기 시험 (OT_BENCH_MOUSE=폴더)
	internal static void MouseLive(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder();
		MaryongGame.Open(); Form form = null; foreach (Form of in Application.OpenForms) if (of is MaryongGame) form = of;
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
		Type mt = typeof(MaryongGame); MiniGame[] games = (MiniGame[])mt.GetField("games", bf).GetValue(form);
		log.AppendLine("오락실 게임 수: " + games.Length);
		float S = Dpi.Scale;
		string[] keys = { "hoop", "bowl", "sling" }; int gi = -1; int phase = 0; MiniGame cur = null; Timer tm = new Timer(); tm.Interval = 1200;
		Func<float, float, IntPtr> pos = delegate(float x, float y) { return (IntPtr)(((int)(y * S) << 16) | ((int)(x * S) & 0xFFFF)); };
		tm.Tick += delegate
		{
			tm.Stop();
			try
			{
				if (phase == 0)
				{
					gi++; if (gi >= keys.Length) { tm.Stop(); File.WriteAllText(Path.Combine(dir, "mouse.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); return; }
					foreach (MiniGame m in games) if (m.Key == keys[gi]) cur = m;
					mt.GetField("g", bf).SetValue(form, cur); mt.GetMethod("StartGame", bf).Invoke(form, null); form.Activate();
					float sx = keys[gi] == "sling" ? 76 : 200, sy = keys[gi] == "sling" ? 430 : (keys[gi] == "bowl" ? 470 : 505);
					float ex = keys[gi] == "sling" ? 76 - 60 : 200, ey = keys[gi] == "sling" ? 430 + 40 : (keys[gi] == "bowl" ? 470 + 170 : 505 + 190);
					IntPtr h = form.Handle; for (int w0 = 0; w0 < 25; w0++) { System.Threading.Thread.Sleep(30); Application.DoEvents(); }
					PostMessage3(h, 0x201, (IntPtr)1, pos(sx, sy)); Application.DoEvents(); System.Threading.Thread.Sleep(80);
					for (int i = 1; i <= 8; i++) { PostMessage3(h, 0x200, (IntPtr)1, pos(sx + (ex - sx) * i / 8, sy + (ey - sy) * i / 8)); Application.DoEvents(); System.Threading.Thread.Sleep(30); }
					PostMessage3(h, 0x202, IntPtr.Zero, pos(ex, ey)); Application.DoEvents(); System.Threading.Thread.Sleep(120); Application.DoEvents();
					bool moving = false; Type ct = cur.GetType();
					if (keys[gi] == "hoop") moving = (bool)ct.GetField("flying", bf).GetValue(cur);
					else if (keys[gi] == "bowl") moving = (int)ct.GetField("state", bf).GetValue(cur) == 1 || (int)ct.GetField("state", bf).GetValue(cur) == 2;
					else moving = (bool)ct.GetField("flying", bf).GetValue(cur);
					log.AppendLine(keys[gi] + ": 마우스로 끌었다 놓으니 " + (moving ? "공이 날아감(정상)" : "반응 없음(문제)"));
					phase = 1;
				}
				else phase = 0;
			}
			catch (Exception ex) { log.AppendLine("오류 " + ex); phase = 0; }
			tm.Start();
		};
		tm.Start();
	}
}
internal partial class Program
{
	internal static void MenuShots(string dir)
	{
		Directory.CreateDirectory(dir); MaryongGame.Open(); Form form = null; foreach (Form of in Application.OpenForms) if (of is MaryongGame) form = of;
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
		Type mt = typeof(MaryongGame); int pg = 0; Timer tm = new Timer(); tm.Interval = 900;
		tm.Tick += delegate
		{
			tm.Stop();
			try
			{
				mt.GetField("state", bf).SetValue(form, 0); mt.GetField("page", bf).SetValue(form, pg);
				for (int i = 0; i < 6; i++) { System.Threading.Thread.Sleep(30); Application.DoEvents(); }
				using (Bitmap bm = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) { form.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(dir, "menu" + pg + ".png"), System.Drawing.Imaging.ImageFormat.Png); }
			}
			catch (Exception ex) { File.WriteAllText(Path.Combine(dir, "err.txt"), ex.ToString()); }
			pg++; if (pg >= 3) { Application.Exit(); return; }
			tm.Start();
		};
		tm.Start();
	}
}
internal partial class Program
{
	// 설정창 홈 화면 캡처 + 전환 중 프레임 연속 촬영 (OT_BENCH_FRAMES=폴더)
	static bool firstPaintLogged;
	internal static void FrameTest(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder();
		Stopwatch openSw = Stopwatch.StartNew(); buildSw = openSw; ShowSettings(); double openCall = openSw.Elapsed.TotalMilliseconds; Form f = settingsForm; f.Shown += delegate { log.AppendLine("구간: " + buildLog.ToString()); log.AppendLine("창 열기: ShowSettings 호출 " + openCall.ToString("F0") + "ms · 화면에 뜰 때까지 " + openSw.Elapsed.TotalMilliseconds.ToString("F0") + "ms"); }; f.Paint += delegate { if (!firstPaintLogged) { firstPaintLogged = true; log.AppendLine("첫 그리기까지 " + openSw.Elapsed.TotalMilliseconds.ToString("F0") + "ms"); } };
		Timer t1 = new Timer(); t1.Interval = 2500; int stage = 0;
		List<KeyValuePair<double, Bitmap>> frames = new List<KeyValuePair<double, Bitmap>>(); bool rec = false; Stopwatch sw = Stopwatch.StartNew();
		Rectangle region = Rectangle.Empty;
		System.Threading.Thread cap = new System.Threading.Thread(delegate()
		{
			while (true)
			{
				if (rec) { try { Bitmap b = Grab.Screen(region); Bitmap s = new Bitmap(b.Width / 4, b.Height / 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); using (Graphics g = Graphics.FromImage(s)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear; g.DrawImage(b, 0, 0, s.Width, s.Height); } b.Dispose(); lock (frames) frames.Add(new KeyValuePair<double, Bitmap>(sw.Elapsed.TotalMilliseconds, s)); } catch { } }
				System.Threading.Thread.Sleep(2);
			}
		}); cap.IsBackground = true; cap.Start();
		Action<string> report = delegate(string name)
		{
			List<KeyValuePair<double, Bitmap>> fr; lock (frames) { fr = new List<KeyValuePair<double, Bitmap>>(frames); frames.Clear(); }
			double prevL = -1; double maxJump = 0; double maxDiff = 0; StringBuilder sb = new StringBuilder(); double t0 = fr.Count > 0 ? fr[0].Key : 0; Bitmap prev = null;
			int cols = 8, cw = fr.Count > 0 ? fr[0].Value.Width : 1, ch = fr.Count > 0 ? fr[0].Value.Height : 1; int shown = Math.Min(fr.Count, 40);
			Bitmap strip = new Bitmap(cw * cols, ch * ((shown + cols - 1) / cols), System.Drawing.Imaging.PixelFormat.Format32bppArgb); Graphics sg = Graphics.FromImage(strip); int si = 0;
			for (int i = 0; i < fr.Count; i++)
			{
				Bitmap b = fr[i].Value; double sum = 0, d = 0; int n = 0;
				for (int y = 0; y < b.Height; y += 3) for (int x = 0; x < b.Width; x += 3) { Color c = b.GetPixel(x, y); sum += 0.299 * c.R + 0.587 * c.G + 0.114 * c.B; n++; if (prev != null) { Color c0 = prev.GetPixel(x, y); d += Math.Abs(c.R - c0.R) + Math.Abs(c.G - c0.G) + Math.Abs(c.B - c0.B); } }
				double l = sum / n; if (prevL >= 0) { maxJump = Math.Max(maxJump, Math.Abs(l - prevL)); maxDiff = Math.Max(maxDiff, d / (3.0 * n)); }
				if (i < 40 && i % 1 == 0) { sg.DrawImage(b, (si % cols) * cw, (si / cols) * ch); si++; }
				sb.Append(((int)(fr[i].Key - t0)) + "ms:" + l.ToString("F1") + " "); prevL = l; prev = b;
			}
			sg.Dispose(); strip.Save(Path.Combine(dir, name + "_frames.png")); strip.Dispose();
			double span = fr.Count > 1 ? fr[fr.Count - 1].Key - fr[0].Key : 0;
			log.AppendLine(name + ": 프레임 " + fr.Count + "장 / " + span.ToString("F0") + "ms (" + (fr.Count > 1 ? (fr.Count * 1000.0 / span).ToString("F0") : "-") + "장/초) · 밝기 최대 변화 " + maxJump.ToString("F1") + " · 프레임간 최대 차이 " + maxDiff.ToString("F1"));
			log.AppendLine("  그리기: " + PaintProf.Dump()); log.AppendLine("  밝기: " + sb.ToString().Substring(0, Math.Min(900, sb.Length)));
			foreach (KeyValuePair<double, Bitmap> kv in fr) kv.Value.Dispose();
		};
		t1.Tick += delegate
		{
			try
			{
				if (stage == 0) { log.AppendLine("첫 그리기 컨트롤별: " + PaintProf.Dump()); using (Bitmap pb = new Bitmap(f.ClientSize.Width, f.ClientSize.Height)) { f.DrawToBitmap(pb, new Rectangle(0, 0, pb.Width, pb.Height)); pb.Save(Path.Combine(dir, "home_print.png")); } using (Bitmap bm = Grab.Screen(f.Bounds)) bm.Save(Path.Combine(dir, "home.png"), System.Drawing.Imaging.ImageFormat.Png); foreach (Control pc in f.Controls) { if (pc is WashPanel && pc.Controls.Count > 3) { int mb = 0; string who = ""; foreach (Control ch2 in pc.Controls) if (ch2.Visible || true) { if (ch2.Bottom > mb) { mb = ch2.Bottom; who = ch2.GetType().Name; } } log.AppendLine("패널 " + pc.Bounds + " 자식 " + pc.Controls.Count + "개 · 가장 아래 " + (mb / Dpi.Scale).ToString("F0") + "논리px (" + who + ") / 패널 높이 " + (pc.Height / Dpi.Scale).ToString("F0")); } }
					log.AppendLine("창 위치/크기(px): " + f.Bounds + " · 화면 작업영역: " + Screen.PrimaryScreen.WorkingArea + " · Dpi.Scale=" + Dpi.Scale + " Fit=" + Dpi.Fit); region = f.RectangleToScreen(f.ClientRectangle); region = new Rectangle(region.X + (int)(region.Width * 0.22), region.Y + (int)(region.Height * 0.08), (int)(region.Width * 0.76), (int)(region.Height * 0.84)); rec = true; t1.Interval = 350; stage = 1; }
				else if (stage == 1) { settingsGo(2); t1.Interval = 1300; stage = 2; }
				else if (stage == 2) { rec = false; System.Threading.Thread.Sleep(50); report("쪽전환(홈→기록)"); using (Bitmap bm = Grab.Screen(f.Bounds)) bm.Save(Path.Combine(dir, "page2.png"), System.Drawing.Imaging.ImageFormat.Png); rec = true; t1.Interval = 350; stage = 3; }
				else if (stage == 3) { SetTheme(1); t1.Interval = 1500; stage = 4; }
				else if (stage == 4) { rec = false; System.Threading.Thread.Sleep(50); report("테마변경(밝게→어둡게)"); using (Bitmap bm = Grab.Screen(f.Bounds)) bm.Save(Path.Combine(dir, "dark.png"), System.Drawing.Imaging.ImageFormat.Png); rec = true; t1.Interval = 350; stage = 5; }
				else if (stage == 5) { settingsGo(0); t1.Interval = 1300; stage = 6; }
				else { rec = false; System.Threading.Thread.Sleep(50); report("쪽전환(어둡게-기록-홈)"); File.WriteAllText(Path.Combine(dir, "frames.txt"), log.ToString(), Encoding.UTF8); t1.Stop(); Application.Exit(); }
			}
			catch (Exception ex) { log.AppendLine("오류 " + ex); File.WriteAllText(Path.Combine(dir, "frames.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); }
		};
		t1.Start();
	}
}
internal partial class Program
{
	internal static Stopwatch buildSw; internal static StringBuilder buildLog = new StringBuilder();
	internal static void BM(string s) { if (buildSw != null) { buildLog.Append(s + " " + (int)buildSw.Elapsed.TotalMilliseconds + "ms | "); } }
}

internal partial class Program
{
	// 미리 만들기 → 열기 → 닫기(숨김) → 다시 열기 → 숨은 상태에서 색 변경 → 열기 (OT_BENCH_REOPEN=폴더)
	internal static void ReopenTest(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder(); int stage = 0; Stopwatch sw = new Stopwatch(); Timer tm = new Timer(); tm.Interval = 2500;
		Action<string> snap = delegate(string n) { if (settingsForm != null && !settingsForm.IsDisposed && settingsForm.Visible) using (Bitmap bm = Grab.Screen(settingsForm.Bounds)) bm.Save(Path.Combine(dir, n + ".png"), System.Drawing.Imaging.ImageFormat.Png); };
		tm.Tick += delegate
		{
			tm.Stop();
			try
			{
				if (stage == 0) { sw.Restart(); PrebuildSettings(); log.AppendLine("① 미리 만들기: " + sw.ElapsedMilliseconds + "ms · 폼 있음 " + (settingsForm != null) + " · 보임 " + (settingsForm != null && settingsForm.Visible)); tm.Interval = 1500; }
				else if (stage == 1) { sw.Restart(); ShowSettings(); log.AppendLine("② 첫 열기(미리 만든 것): ShowSettings 호출 " + sw.ElapsedMilliseconds + "ms · 보임 " + settingsForm.Visible); tm.Interval = 900; }
				else if (stage == 2) { snap("open1"); log.AppendLine("   열린 뒤 투명도 " + settingsForm.Opacity.ToString("F3") + " 위치 " + settingsForm.Location); settingsForm.Close(); log.AppendLine("③ 닫기(X): 폼 살아있음 " + (settingsForm != null && !settingsForm.IsDisposed) + " · 보임 " + (settingsForm != null && settingsForm.Visible)); tm.Interval = 1200; }
				else if (stage == 3) { sw.Restart(); ShowSettings(); log.AppendLine("④ 다시 열기: ShowSettings 호출 " + sw.ElapsedMilliseconds + "ms · 보임 " + settingsForm.Visible); tm.Interval = 900; }
				else if (stage == 4) { snap("open2"); settingsForm.Close(); tm.Interval = 800; }
				else if (stage == 5) { SetTheme(1); log.AppendLine("⑤ 숨은 상태에서 색 변경 → 폼 없어짐 " + (settingsForm == null)); tm.Interval = 600; }
				else if (stage == 6) { sw.Restart(); ShowSettings(); log.AppendLine("⑥ 새 색으로 열기(다시 만듦): ShowSettings 호출 " + sw.ElapsedMilliseconds + "ms"); tm.Interval = 900; }
				else { snap("open3_dark"); File.WriteAllText(Path.Combine(dir, "reopen.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); return; }
				stage++; tm.Start();
			}
			catch (Exception ex) { log.AppendLine("오류 " + ex); File.WriteAllText(Path.Combine(dir, "reopen.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); }
		};
		tm.Start();
	}
}
internal partial class Program
{
	internal static void ClayTest(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder(); System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic; Image mas = MascotImage();
		string[] modes = { "정확히 조준(접시 중심)", "조금 빗나감(30px)", "아무 데나" }; Random rr = new Random(3);
		for (int m = 0; m < 3; m++)
		{
			StressGame g = new StressGame(); g.Mascot = mas; g.Reset(); GameInput inp = new GameInput(); double t = 0; Type tt = g.GetType(); System.Collections.IList bls = (System.Collections.IList)tt.GetField("bls", bf).GetValue(g); int shots = 0; bool snap = false;
			for (int i = 0; i < 60 * 46 && !g.Over; i++)
			{
				float cd = (float)(double)tt.GetField("cd", bf).GetValue(g);
				if (cd <= 0)
				{
					object tgt = null; float bestY = -1; foreach (object o in bls) { int k = (int)o.GetType().GetField("Kind").GetValue(o); float y = (float)o.GetType().GetField("Y").GetValue(o), x = (float)o.GetType().GetField("X").GetValue(o); if (k != 1 && y > 60 && y < 440 && x > 20 && x < 380 && y > bestY) { bestY = y; tgt = o; } }
					if (tgt != null || m == 2)
					{
						float x = 200, y = 200; if (tgt != null) { x = (float)tgt.GetType().GetField("X").GetValue(tgt); y = (float)tgt.GetType().GetField("Y").GetValue(tgt); }
						if (m == 1) { x += (float)(rr.NextDouble() * 60 - 30); y += (float)(rr.NextDouble() * 60 - 30); } if (m == 2) { x = (float)(rr.NextDouble() * 380 + 10); y = (float)(rr.NextDouble() * 400 + 40); }
						inp.Mx = x; inp.My = y; g.Click(x, y); shots++;
					}
				}
				t += 1 / 60.0; g.Update(1f / 60f, inp, t);
				if (m == 0 && !snap && i == 60 * 8 && bls.Count >= 2) { snap = true; using (Bitmap bm = new Bitmap(400, 600)) using (Graphics gr = Graphics.FromImage(bm)) { Gfx.HQ(gr); g.Draw(gr, t); bm.Save(Path.Combine(dir, "clay.png"), System.Drawing.Imaging.ImageFormat.Png); } }
			}
			log.AppendLine(modes[m] + ": 점수 " + g.Score + " · 쏜 횟수 " + shots);
		}
		File.WriteAllText(Path.Combine(dir, "clay.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	// 게임별 한 프레임 그리는 시간 (200% 배율, 실제 오락실 창과 같은 그리기 설정)
	internal static void GameFps(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder(); Image mas = MascotImage(); float S = Dpi.Scale;
		Func<MiniGame>[] mk = { () => new CatchGame(), () => new JumpGame(), () => new BatGame(), () => new StressGame(), () => new SuikaGame(), () => new TowerGame(), () => new TimeGame(), () => new MemoryGame(), () => new Game2048(), () => new TypeGame(), () => new BasketGame(), () => new BowlGame(), () => new SlingGame() };
		using (Bitmap bm = new Bitmap((int)(400 * S), (int)(600 * S), System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
		using (Graphics g = Graphics.FromImage(bm))
		{
			foreach (Func<MiniGame> f in mk)
			{
				MiniGame m = null; try { m = f(); m.Mascot = (m.Costume != null ? OutfitImage(m.Costume) : null) ?? mas; m.Reset(); } catch { continue; }
				GameInput inp = new GameInput(); inp.Mx = 200; inp.My = 300; double t = 0;
				for (int i = 0; i < 90; i++) { t += 1 / 60.0; m.Update(1f / 60f, inp, t); }
				// 워밍업 + 측정
				for (int i = 0; i < 10; i++) { g.ResetTransform(); Gfx.HQ(g); g.ScaleTransform(S, S); m.Draw(g, t); }
				Stopwatch sw = Stopwatch.StartNew(); double max = 0; int n = 60;
				for (int i = 0; i < n; i++) { long a = Stopwatch.GetTimestamp(); g.ResetTransform(); Gfx.HQ(g); g.ScaleTransform(S, S); t += 1 / 60.0; m.Update(1f / 60f, inp, t); m.Draw(g, t); double ms = (Stopwatch.GetTimestamp() - a) * 1000.0 / Stopwatch.Frequency; if (ms > max) max = ms; }
				double avg = sw.Elapsed.TotalMilliseconds / n;
				log.AppendLine(m.Name.PadRight(12) + " 평균 " + avg.ToString("F1").PadLeft(5) + "ms · 최대 " + max.ToString("F1").PadLeft(5) + "ms  " + (avg > 12 ? "← 느림" : avg > 8 ? "← 보통" : ""));
			}
		}
		File.WriteAllText(Path.Combine(dir, "fps.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	internal static List<long> frameStamps = new List<long>();
	internal static void FrameStamp() { if (PaintProf.On) frameStamps.Add(Stopwatch.GetTimestamp()); }
	// 실제 오락실 창에서 화면이 갱신되는 간격 (OT_BENCH_REALFPS=폴더)
	internal static void RealFps(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder(); MaryongGame.Open(); Form form = null; foreach (Form of in Application.OpenForms) if (of is MaryongGame) form = of;
		System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public; Type mt = typeof(MaryongGame);
		MiniGame[] games = (MiniGame[])mt.GetField("games", bf).GetValue(form); string[] keys = { "catch", "bat", "suika", "jump" };
		int gi = -1, phase = 0; Timer tm = new Timer(); tm.Interval = 1200;
		tm.Tick += delegate
		{
			tm.Stop();
			try
			{
				if (phase == 0)
				{
					gi++; if (gi >= keys.Length) { File.WriteAllText(Path.Combine(dir, "realfps.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); return; }
					foreach (MiniGame m in games) if (m.Key == keys[gi]) { mt.GetField("g", bf).SetValue(form, m); }
					mt.GetMethod("StartGame", bf).Invoke(form, null); form.Activate(); frameStamps.Clear(); phase = 1; tm.Interval = 4000;
				}
				else
				{
					List<long> fs = new List<long>(frameStamps); double[] gaps = new double[Math.Max(0, fs.Count - 1)]; for (int i = 1; i < fs.Count; i++) gaps[i - 1] = (fs[i] - fs[i - 1]) * 1000.0 / Stopwatch.Frequency;
					Array.Sort(gaps); double span = fs.Count > 1 ? (fs[fs.Count - 1] - fs[0]) * 1000.0 / Stopwatch.Frequency : 1;
					double med = gaps.Length > 0 ? gaps[gaps.Length / 2] : 0, p99 = gaps.Length > 0 ? gaps[(int)(gaps.Length * 0.99)] : 0, mx = gaps.Length > 0 ? gaps[gaps.Length - 1] : 0; int over20 = 0, over33 = 0; foreach (double d in gaps) { if (d > 20) over20++; if (d > 33) over33++; }
					log.AppendLine(keys[gi].PadRight(7) + ": " + (fs.Count * 1000.0 / span).ToString("F1") + "fps · 간격 중앙 " + med.ToString("F1") + "ms · 99% " + p99.ToString("F1") + "ms · 최대 " + mx.ToString("F1") + "ms · 20ms 넘은 프레임 " + over20 + "/" + gaps.Length + " · 33ms 넘음 " + over33);
					phase = 0; tm.Interval = 600;
				}
			}
			catch (Exception ex) { log.AppendLine("오류 " + ex); phase = 0; }
			tm.Start();
		};
		tm.Start();
	}
}
internal partial class Program
{
	internal static void BatShots(string dir)
	{
		Directory.CreateDirectory(dir); System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		BatGame g = new BatGame(); g.Mascot = OutfitImage("어린이날") ?? MascotImage(); g.Reset(); Type tt = g.GetType(); GameInput inp = new GameInput(); double t = 0; float delta = -0.026f; bool[] got = new bool[8]; int guard = 0;
		Action<string> snap = delegate(string n) { using (Bitmap bm = new Bitmap(400, 600)) using (Graphics gr = Graphics.FromImage(bm)) { Gfx.HQ(gr); g.Draw(gr, t); bm.Save(Path.Combine(dir, n + ".png"), System.Drawing.Imaging.ImageFormat.Png); } };
		while (guard++ < 40000)
		{
			int ph = (int)tt.GetField("phase", bf).GetValue(g); bool sw = (bool)tt.GetField("swinging", bf).GetValue(g); float pt = (float)tt.GetField("pt", bf).GetValue(g), plate = (float)tt.GetField("ptPlate", bf).GetValue(g); float swT = (float)tt.GetField("swT", bf).GetValue(g);
			if (ph == 0 && !got[0] && t > 0.4) { got[0] = true; snap("0_stance"); }
			if (ph == 1 && !sw) { if (!got[1] && pt > plate * 0.55f) { got[1] = true; snap("1_approach"); } if (pt >= plate - 0.115f + delta) g.Click(0, 0); }
			if (ph == 1 && sw && !got[2] && swT > 0.07f) { got[2] = true; snap("2_swing_early"); }
			if (ph == 1 && sw && !got[3] && swT > 0.12f) { got[3] = true; snap("3_swing_contact"); }
			if (ph == 2) { float flT = (float)tt.GetField("flT", bf).GetValue(g); if (!got[4] && flT > 0.15f) { got[4] = true; snap("4_flight_a"); } if (!got[5] && flT > 0.9f) { got[5] = true; snap("5_flight_b"); } if (!got[6] && flT > 2.3f) { got[6] = true; snap("6_land"); break; } }
			t += 1.0 / 120; g.Update(1f / 120f, inp, t);
		}
	}
}
internal partial class Program
{
	internal static void Test2048(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder(); System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic; Random rr = new Random(9);
		int games = 0, bad = 0, totalMoves = 0, maxTile = 0; Keys[] dirs = { Keys.Left, Keys.Right, Keys.Up, Keys.Down };
		for (int gme = 0; gme < 40; gme++)
		{
			Game2048 g = new Game2048(); g.Reset(); Type tt = g.GetType(); Array grid; int moves = 0; games++;
			Func<int[,]> read = delegate { Array gr = (Array)tt.GetField("grid", bf).GetValue(g); int[,] v = new int[4, 4]; for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) { object o = gr.GetValue(r, c); if (o != null) v[r, c] = (int)o.GetType().GetField("V").GetValue(o); } return v; };
			while (!g.Over && moves < 3000)
			{
				int[,] before = read(); int sumB = 0; foreach (int v in before) sumB += v; Keys k = dirs[rr.Next(4)]; int scoreB = g.Score; g.Press(k); int[,] after = read(); int sumA = 0; foreach (int v in after) sumA += v;
				bool changed = false; for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) if (before[r, c] != after[r, c]) changed = true;
				int dSum = sumA - sumB; if (changed) { moves++; if (dSum != 2 && dSum != 4) { bad++; log.AppendLine("합계 변화 이상: " + dSum); } } else if (dSum != 0) { bad++; log.AppendLine("안 움직였는데 변함"); }
				// 밀린 방향으로 빈칸이 앞에 남으면 안 돼요(스폰된 새 타일 제외 어렵기 때문에 합쳐진 뒤 줄에서 '빈칸 뒤 타일' 검사는 생략하고, 합계·점수 규칙만 검사)
				for (int i = 0; i < 3; i++) g.Update(0.016f, new GameInput(), i);
			}
			totalMoves += moves; for (int r = 0; r < 4; r++) { } int[,] fin = read(); foreach (int v in fin) if (v > maxTile) maxTile = v;
		}
		// 정해진 예: 한 줄 [2,2,4,0] 왼쪽 → [4,4,0,0], [2,2,2,2] → [4,4,0,0], [2,0,2,4] → [4,4,0,0]
		string[] cases = { "2,2,4,0", "2,2,2,2", "2,0,2,4", "4,4,8,8", "2,4,0,0" };
		foreach (string cs in cases)
		{
			Game2048 g = new Game2048(); g.Reset(); Type tt = g.GetType(); Array gr = (Array)tt.GetField("grid", bf).GetValue(g); System.Collections.IList tl = (System.Collections.IList)tt.GetField("tiles", bf).GetValue(g);
			tl.Clear(); for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) gr.SetValue(null, r, c);
			string[] p2 = cs.Split(','); Type tlt = tt.GetNestedType("Tl", bf);
			for (int c = 0; c < 4; c++) { int v = int.Parse(p2[c]); if (v == 0) continue; object o = Activator.CreateInstance(tlt); tlt.GetField("V").SetValue(o, v); tlt.GetField("R").SetValue(o, 0); tlt.GetField("C").SetValue(o, c); tlt.GetField("X").SetValue(o, (float)c); tlt.GetField("Appear").SetValue(o, 1f); gr.SetValue(o, 0, c); tl.Add(o); }
			System.Reflection.MethodInfo mv = tt.GetMethod("Move", bf); mv.Invoke(g, new object[] { 0, -1 });
			StringBuilder sb = new StringBuilder(); for (int c = 0; c < 4; c++) { object o = gr.GetValue(0, c); sb.Append((o == null ? 0 : (int)tlt.GetField("V").GetValue(o)) + (c < 3 ? "," : "")); }
			log.AppendLine("왼쪽으로 [" + cs + "] → [" + sb + "] · 점수 " + g.Score);
		}
		log.AppendLine("무작위 " + games + "판 · 총 " + totalMoves + "번 이동 · 규칙 위반 " + bad + " · 가장 큰 타일 " + maxTile);
		File.WriteAllText(Path.Combine(dir, "g2048.txt"), log.ToString(), Encoding.UTF8);
	}
}
internal partial class Program
{
	[System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "PostMessage")] private static extern bool PostMessage4(IntPtr h, int m, IntPtr w, IntPtr l);
	internal static void SecretTest(string dir)
	{
		Directory.CreateDirectory(dir); StringBuilder log = new StringBuilder();
		// 효과음 리소스 확인
		string[] names = { "ui_click", "ui_open", "ui_back", "tick", "pluck", "pluck2", "thud", "thud2", "thud_soft", "bong", "glass", "glass2", "glass_big", "ok", "ok2", "fanfare", "bad", "bad2", "bad_big", "whoosh", "swing", "flip", "ask", "fall", "alert", "alert2", "alert3", "coin", "record", "homerun", "over" };
		int missing = 0; foreach (string n in names) { using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("sfx." + n + ".wav")) if (s == null) { missing++; log.AppendLine("효과음 없음: " + n); } }
		log.AppendLine("효과음 리소스 " + names.Length + "개 중 없는 것 " + missing + "개");
		ShowSettings(); Form f = settingsForm; int stage = 0; Timer tm = new Timer(); tm.Interval = 1800;
		Keys[] good = { Keys.Up, Keys.Up, Keys.Down, Keys.Down, Keys.Left, Keys.Right, Keys.Left, Keys.Right };
		Keys[] wrong = { Keys.Up, Keys.Up, Keys.Down, Keys.Down, Keys.Down, Keys.Left, Keys.Right, Keys.Left, Keys.Right };
		Action<Keys[]> send = delegate(Keys[] ks) { foreach (Keys k in ks) { PostMessage4(f.Handle, 0x100, (IntPtr)(int)k, IntPtr.Zero); PostMessage4(f.Handle, 0x101, (IntPtr)(int)k, IntPtr.Zero); for (int i = 0; i < 4; i++) { System.Threading.Thread.Sleep(15); Application.DoEvents(); } } };
		Func<Form> arcade = delegate { foreach (Form of in Application.OpenForms) if (of is MaryongGame) return of; return null; };
		tm.Tick += delegate
		{
			tm.Stop();
			try
			{
				f.Activate(); 
				if (stage == 0) { send(wrong); log.AppendLine("틀린 순서(아래 2번) 후 오락실 열림: " + (arcade() != null)); }
				else if (stage == 1) { send(good); }
				else if (stage == 2)
				{
					Form ar = arcade(); log.AppendLine("맞는 순서 후 오락실 열림: " + (ar != null));
					if (ar != null) { int st = (int)typeof(MaryongGame).GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ar); log.AppendLine("오락실 처음 화면 = 메뉴(state 0): " + (st == 0) + " (state=" + st + ")"); using (Bitmap bm = new Bitmap(ar.ClientSize.Width, ar.ClientSize.Height)) { ar.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(dir, "secret_menu.png"), System.Drawing.Imaging.ImageFormat.Png); } }
					int cf = 0; foreach (Form of in Application.OpenForms) if (of.GetType().Name.IndexOf("Confetti", StringComparison.OrdinalIgnoreCase) >= 0 || of.GetType().Name.IndexOf("Fx", StringComparison.OrdinalIgnoreCase) >= 0) cf++; log.AppendLine("폭죽 관련 창 수(참고): " + cf);
					File.WriteAllText(Path.Combine(dir, "secret.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); return;
				}
				stage++; tm.Start();
			}
			catch (Exception ex) { log.AppendLine("오류 " + ex); File.WriteAllText(Path.Combine(dir, "secret.txt"), log.ToString(), Encoding.UTF8); Application.Exit(); }
		};
		tm.Start();
	}
}
internal partial class Program
{
	private static void FindByType(Control c, string tn, List<Control> o) { if (c.GetType().Name == tn) o.Add(c); foreach (Control k in c.Controls) FindByType(k, tn, o); }
	// 진짜 마우스 클릭으로 반응 속도 측정 (OT_BENCH_REALINPUT=폴더)
	// 홈 화면에서 가만히 있을 때 프레임이 끊기는지 재요 (OT_BENCH_HOMEIDLE=폴더)
	// 다른 탭에 있다가 홈으로 돌아온 뒤 가만히 있는 동안의 끊김 (OT_BENCH_HOMEARRIVE=폴더, perf.on 이 있어야 UiProf 가 perf.log 에 적어요)
	internal static void HomeArrive(string dir)
	{
		Directory.CreateDirectory(dir); ShowSettings(); int stage = 0; Timer tm = new Timer(); tm.Interval = 3000;
		tm.Tick += delegate
		{
			stage++;
			if (stage == 1) { Anim.Mark("시험: 알림 탭으로"); settingsGo(2); tm.Interval = 2500; }
			else if (stage == 2) { Anim.Mark("시험: 홈으로"); settingsGo(0); tm.Interval = 9000; }
			else if (stage == 3) { Anim.Mark("시험: 끝"); tm.Stop(); File.WriteAllText(Path.Combine(dir, "arrive.txt"), "done"); Environment.Exit(0); }
		};
		tm.Start();
	}	internal static void HomeIdle(string dir) { }
	internal static void RealInputTest(string dir) { }
		private static double[] clickPending; private static int[] gcBefore = new int[3];
}
