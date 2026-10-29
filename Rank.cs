using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// 온라인 랭킹 (Firebase Realtime Database REST). 주소가 없거나 연결이 안 되면 조용히 이 PC 기록만 써요.
internal static class Rank
{
	// settings.ini 의 rankUrl=... 가 있으면 그걸 써요 (예: https://이름-default-rtdb.asia-southeast1.firebasedatabase.app)
	public const string DefaultUrl = "https://maryong-50d2b-default-rtdb.asia-southeast1.firebasedatabase.app";
	private static string url; private static bool loaded;
	private static readonly object lk = new object();
	private static readonly Dictionary<string, List<KeyValuePair<string, int>>> boards = new Dictionary<string, List<KeyValuePair<string, int>>>();
	private static readonly Dictionary<string, int> status = new Dictionary<string, int>();   // 1 불러오는 중 · 2 성공 · 3 실패
	private static readonly Dictionary<string, DateTime> when = new Dictionary<string, DateTime>();

	private static void Load()
	{
		loaded = true; url = DefaultUrl;
		try
		{
			string p = Path.Combine(Program.DataDir, "settings.ini");
			if (File.Exists(p)) foreach (string l in File.ReadAllLines(p)) if (l.StartsWith("rankUrl=")) { string v = l.Substring(8).Trim(); if (v.Length > 0) url = v; }
		}
		catch { }
		url = (url ?? "").Trim().TrimEnd('/');
		if (url.Length > 0 && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "";
		try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | ServicePointManager.SecurityProtocol; } catch { }
	}
	public static bool On { get { if (!loaded) Load(); return url.Length > 0; } }

	private static string NamePath { get { return Path.Combine(Program.DataDir, "rank_name.txt"); } }
	public static string LastName
	{
		get { try { if (File.Exists(NamePath)) return File.ReadAllText(NamePath, Encoding.UTF8).Trim(); } catch { } return ""; }
		set { try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(NamePath, value ?? "", Encoding.UTF8); } catch { } }
	}
	public static string Clean(string n)
	{
		if (n == null) n = ""; StringBuilder sb = new StringBuilder();
		foreach (char c in n) { if (char.IsControl(c) || c == '|' || c == '"' || c == '\\' || c == '<' || c == '>') continue; sb.Append(c); }
		string r = sb.ToString().Trim(); if (r.Length > 8) r = r.Substring(0, 8).Trim(); return r;
	}

	public static int Status(string game) { lock (lk) { int s; return status.TryGetValue(game, out s) ? s : 0; } }
	public static List<KeyValuePair<string, int>> Get(string game) { lock (lk) { List<KeyValuePair<string, int>> b; if (boards.TryGetValue(game, out b)) return new List<KeyValuePair<string, int>>(b); return new List<KeyValuePair<string, int>>(); } }

	private static HttpWebRequest Req(string path, string method, int ms)
	{
		HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url + path); r.Method = method; r.Timeout = ms; r.ReadWriteTimeout = ms; r.UserAgent = "Maryong/1.0"; r.KeepAlive = false; return r;
	}

	// 순위표를 받아와요 (20초 안에 다시 부르면 건너뛰어요)
	public static void Fetch(string game, bool force)
	{
		if (!On) return;
		lock (lk) { int s; if (status.TryGetValue(game, out s) && s == 1) return; DateTime w; if (!force && when.TryGetValue(game, out w) && (DateTime.Now - w).TotalSeconds < 20) return; status[game] = 1; when[game] = DateTime.Now; }
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				Flush();
				HttpWebRequest r = Req("/scores/" + game + ".json?orderBy=%22s%22&limitToLast=10", "GET", 7000);
				string body; using (WebResponse w = r.GetResponse()) using (StreamReader sr = new StreamReader(w.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd();
				List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
				foreach (Match m in Regex.Matches(body, "\"n\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*,\\s*\"s\"\\s*:\\s*(-?\\d+)"))
				{ int sc; if (int.TryParse(m.Groups[2].Value, out sc)) list.Add(new KeyValuePair<string, int>(Regex.Unescape(m.Groups[1].Value), sc)); }
				list.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });
				if (list.Count > 10) list.RemoveRange(10, list.Count - 10);
				lock (lk) { boards[game] = list; status[game] = 2; }
			}
			catch { lock (lk) status[game] = 3; }
		});
	}

	// 기록을 올려요 (실패하면 파일에 남겨 두었다가 다음에 다시 올려요)
	public static void Submit(string game, string name, int score)
	{
		if (!On || score <= 0) return; name = Clean(name); if (name.Length == 0) name = "익명";
		long t = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
		ThreadPool.QueueUserWorkItem(delegate { if (!Post(game, name, score, t)) Pend(game, name, score, t); else { Flush(); lock (lk) { when.Remove(game); } } });
	}
	private static bool Post(string game, string name, int score, long t)
	{
		try
		{
			byte[] data = Encoding.UTF8.GetBytes("{\"n\":\"" + name + "\",\"s\":" + score + ",\"t\":" + t + "}");
			HttpWebRequest r = Req("/scores/" + game + ".json", "POST", 7000); r.ContentType = "application/json; charset=utf-8"; r.ContentLength = data.Length;
			using (Stream s = r.GetRequestStream()) s.Write(data, 0, data.Length);
			using (WebResponse w = r.GetResponse()) { }
			return true;
		}
		catch { return false; }
	}
	private static string PendPath { get { return Path.Combine(Program.DataDir, "rank_pending.txt"); } }
	private static void Pend(string game, string name, int score, long t) { try { lock (lk) { Directory.CreateDirectory(Program.DataDir); File.AppendAllText(PendPath, game + "|" + score + "|" + t + "|" + name + "\n", Encoding.UTF8); } } catch { } }
	private static void Flush()
	{
		try
		{
			string[] lines; lock (lk) { if (!File.Exists(PendPath)) return; lines = File.ReadAllLines(PendPath, Encoding.UTF8); File.Delete(PendPath); }
			List<string> left = new List<string>();
			foreach (string l in lines)
			{
				string[] p = l.Split(new char[] { '|' }, 4); int sc; long tt; if (p.Length < 4 || !int.TryParse(p[1], out sc) || !long.TryParse(p[2], out tt)) continue;
				if (left.Count > 0 || !Post(p[0], p[3], sc, tt)) left.Add(l);
			}
			if (left.Count > 0) lock (lk) File.AppendAllText(PendPath, string.Join("\n", left.ToArray()) + "\n", Encoding.UTF8);
		}
		catch { }
	}
}
