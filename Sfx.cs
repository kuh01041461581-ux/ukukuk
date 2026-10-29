using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Reflection;

// 오락실 효과음 (Kenney Interface Sounds · WobbleBoxx — 모두 CC0 공공 도메인). exe 안에 들어 있는 짧은 WAV 를 재생해요.
// 알림음 설정이 꺼져 있거나 게임 안 스피커 버튼으로 끄면 소리가 나지 않아요.
internal static class Sfx
{
	// 효과음은 용량을 줄이려고 압축해서 넣어 뒀어요 (앞 두 바이트가 1f 8b 이면 풀어서 써요)
	private static Stream Res(string name)
	{
		using (Stream raw = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
		{
			if (raw == null) return null; byte[] b = new byte[raw.Length]; int o = 0; while (o < b.Length) { int r = raw.Read(b, o, b.Length - o); if (r <= 0) break; o += r; }
			if (b.Length > 2 && b[0] == 0x1f && b[1] == 0x8b) { MemoryStream ms = new MemoryStream(); using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(new MemoryStream(b), System.IO.Compression.CompressionMode.Decompress)) { byte[] buf = new byte[8192]; int n; while ((n = gz.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n); } ms.Position = 0; return ms; }
			return new MemoryStream(b);
		}
	}
	private static readonly Dictionary<string, SoundPlayer> cache = new Dictionary<string, SoundPlayer>();
	private static readonly Dictionary<string, double> dur = new Dictionary<string, double>();
	private static readonly Dictionary<string, long> last = new Dictionary<string, long>();
	private static long busyUntil; private static int busyPrio; private static bool loaded, on = true;
	private static string FlagPath { get { return Path.Combine(Program.DataDir, "gamesound.txt"); } }
	public static bool GameOn
	{
		get { if (!loaded) { loaded = true; try { if (File.Exists(FlagPath)) on = File.ReadAllText(FlagPath).Trim() != "0"; } catch { } } return on; }
		set { loaded = true; on = value; try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(FlagPath, value ? "1" : "0"); } catch { } if (!value) Stop(); }
	}
	private static long Ms() { return Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency; }
	private static int Prio(string n)
	{
		switch (n)
		{
			case "fanfare": case "record": case "homerun": case "over": case "bad_big": case "glass_big": return 3;
			case "ok": case "ok2": case "coin": case "glass": case "glass2": return 2;
			default: return 1;
		}
	}
	// 초과근무 알림음: 게임 소리 설정과 상관없이 '알림음' 설정만 따라요
	public static void PlayAlert()
	{
		try
		{
			SoundPlayer sp; bool hv; lock (cache) hv = cache.TryGetValue("alert", out sp); if (!hv) { sp = null; using (Stream st = Res("sfx.alert.wav")) { if (st != null) { byte[] b = new byte[st.Length]; st.Read(b, 0, b.Length); sp = new SoundPlayer(new MemoryStream(b)); sp.Load(); } } lock (cache) cache["alert"] = sp; }
			if (sp != null) sp.Play(); else SystemSounds.Exclamation.Play();
		}
		catch { try { SystemSounds.Exclamation.Play(); } catch { } }
	}
	public static void Stop() { try { lock (q) q.Clear(); lock (cache) foreach (SoundPlayer sp in cache.Values) if (sp != null) sp.Stop(); } catch { } busyUntil = 0; }
	// 소리 재생은 전용 스레드 하나가 맡아요: 게임 화면 쪽은 이름만 넣고 바로 돌아오므로 처음 불러오는 순간이나 재생 때문에 화면이 멈칫하지 않고, 스레드도 계속 늘어나지 않아요
	private static readonly Queue<string> q = new Queue<string>(); private static System.Threading.Thread worker; private static long lastAny;
	private static void Work()
	{
		while (true)
		{
			string n;
			lock (q) { while (q.Count == 0) System.Threading.Monitor.Wait(q); n = q.Dequeue(); }
			try
			{
				SoundPlayer sp; bool have; lock (cache) have = cache.TryGetValue(n, out sp);
				if (!have)
				{
					sp = null;
					using (Stream st = Res("sfx." + n + ".wav"))
					{
						if (st != null)
						{
							byte[] b = new byte[st.Length]; st.Read(b, 0, b.Length);
							int rate = BitConverter.ToInt32(b, 28); lock (cache) dur[n] = rate > 0 ? (b.Length - 44) * 1000.0 / rate : 300;
							sp = new SoundPlayer(new MemoryStream(b)); sp.Load();
						}
					}
					lock (cache) cache[n] = sp;
				}
				if (sp != null && GameOn && Program.SoundOn) sp.Play();
			}
			catch { }
		}
	}
	public static void Play(string n)
	{
		if (!GameOn || !Program.SoundOn) return;
		try
		{
			long now = Ms(); int pr = Prio(n);
			if (now < busyUntil && pr < busyPrio) return;                      // 중요한 소리가 나는 중이면 가벼운 소리는 쉬어요
			long l; if (last.TryGetValue(n, out l) && now - l < 70) return; last[n] = now;   // 같은 소리가 너무 연달아 겹치지 않게
			if (now - lastAny < 25 && pr < 3) return; lastAny = now;            // 어떤 소리든 25ms 안에 또 시작하지 않아요
			double d; lock (cache) { if (!dur.TryGetValue(n, out d)) d = 300; }
			busyUntil = now + (long)d; busyPrio = pr;
			lock (q)
			{
				if (q.Count >= 4) return;
				if (worker == null) { worker = new System.Threading.Thread(Work); worker.IsBackground = true; worker.Name = "sfx"; worker.Priority = System.Threading.ThreadPriority.BelowNormal; worker.Start(); }
				q.Enqueue(n); System.Threading.Monitor.Pulse(q);
			}
		}
		catch { }
	}
}
