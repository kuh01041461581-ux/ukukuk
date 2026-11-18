using System;
using System.Collections.Generic;
using System.IO;

// ── 오락실 난이도 · 오늘의 도전 ─────────────────────────────────────────────────────
// 난이도: 게임 속도를 0.8배(쉬움) · 1배(보통) · 1.25배(어려움)로 바꿔요. 기록(랭킹)은 보통에서만 올라가요.
// 오늘의 도전: 하루에 한 게임 · '내 최고 기록의 70%' 넘기 (기록이 없으면 한 판 해보기). 못 해도 아무 불이익이 없어요.
internal static class Arcade
{
	public static readonly string[] DiffNames = { "쉬움", "보통", "어려움" };
	private static int diff = -1;
	private static string DiffPath { get { return Path.Combine(Program.DataDir, "arcade_diff.txt"); } }
	public static int Diff
	{
		get { if (diff < 0) { diff = 1; try { int v; if (File.Exists(DiffPath) && int.TryParse(File.ReadAllText(DiffPath).Trim(), out v) && v >= 0 && v <= 2) diff = v; } catch { } } return diff; }
		set { diff = Math.Max(0, Math.Min(2, value)); try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(DiffPath, diff.ToString()); } catch { } }
	}
	public static float Speed { get { int d = Diff; return d == 0 ? 0.8f : (d == 2 ? 1.25f : 1f); } }

	private static string DayPath { get { return Path.Combine(Program.DataDir, "daily_challenge.txt"); } }
	private static string dDate = "", dKey = "", lastDone = ""; private static int streak; private static bool dDone, dLoaded;
	// 연속 성공 일수 (어제도 성공했으면 이어져요. 주말·휴일에 쉬어도 끊기지 않게 최근 성공일에서 4일 안이면 이어 줘요)
	public static int Streak { get { Load(); return streak; } }
	private static int BestOf(string key)
	{
		int best = 0;
		try { string p = Path.Combine(Program.DataDir, "ranking_" + key + ".txt"); if (File.Exists(p)) foreach (string ln in File.ReadAllLines(p)) { int k = ln.LastIndexOf('|'); int v; if (k >= 0 && int.TryParse(ln.Substring(k + 1), out v) && v > best) best = v; } } catch { }
		return best;
	}
	private static void Load()
	{
		string today = DateTime.Now.ToString("yyyyMMdd");
		if (!dLoaded) { dLoaded = true; try { if (File.Exists(DayPath)) { string[] a = File.ReadAllText(DayPath).Trim().Split('|'); if (a.Length >= 3) { dDate = a[0]; dKey = a[1]; dDone = a[2] == "1"; } if (a.Length >= 5) { int.TryParse(a[3], out streak); lastDone = a[4]; } } } catch { } }
		if (dDate != today || dKey.Length == 0)
		{
			List<KeyValuePair<string, string>> cat = MaryongGame.Catalog(); dDate = today; dDone = false; dKey = "";
			if (cat.Count > 0) { int seed = int.Parse(today); dKey = cat[(seed * 7 + seed / 100) % cat.Count].Key; }
			Save();
		}
	}
	private static void Save() { try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(DayPath, dDate + "|" + dKey + "|" + (dDone ? "1" : "0") + "|" + streak + "|" + lastDone); } catch { } }
	// 오늘의 도전: 게임 이름 · 목표 점수 · 달성 여부
	public static bool Today(out string key, out string name, out int goal, out bool done)
	{
		Load(); key = dKey; name = ""; goal = 1; done = dDone;
		foreach (KeyValuePair<string, string> kv in MaryongGame.Catalog()) if (kv.Key == dKey) name = kv.Value;
		if (dKey.Length == 0 || name.Length == 0) return false;
		int b = BestOf(dKey); goal = b > 0 ? Math.Max(1, (int)Math.Ceiling(b * 0.7)) : 1; return true;
	}
	// 한 판이 끝났을 때: 오늘의 도전을 막 달성했으면 true
	public static bool Check(string key, int score)
	{
		string k, n; int goal; bool done; if (!Today(out k, out n, out goal, out done)) return false;
		if (done || key != k || score < goal || score <= 0 || Diff < 1) return false;   // 쉬움 난이도로는 도전 성공이 되지 않아요
		DateTime ld; bool cont = DateTime.TryParseExact(lastDone, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out ld) && (DateTime.Now.Date - ld.Date).TotalDays <= 4;
		streak = cont ? streak + 1 : 1; lastDone = DateTime.Now.ToString("yyyyMMdd");
		dDone = true; Save(); try { Bond.Daily("daily"); Bond.Add(streak >= 5 ? 5 : 3); } catch { } return true;
	}
}
