using System;
using System.IO;

// 마룡이 호감도: 쓰다듬기·게임·운세·매일 만나기로 쌓여요 (하루 상한이 있어서 하루 만에 끝까지 오르지는 않아요)
internal static class Bond
{
	private static string P { get { return Path.Combine(Program.DataDir, "bond.txt"); } }
	public static readonly string[] Names = { "낯선 사이", "인사하는 사이", "친한 사이", "단짝", "절친", "가족 같은 사이", "평생 친구", "마음이 통하는 사이" };
	private static readonly int[] Th = { 0, 30, 100, 250, 500, 900, 1500, 2400 };
	private static int pts, streak, loaded, rubToday, gameToday, lastDay = -1; private static string lastDate = ""; private static bool vis;
	public static Action<int> OnLevel;
	private static void Load()
	{
		if (loaded != 0) return; loaded = 1;
		try { if (File.Exists(P)) { string[] p = File.ReadAllText(P).Trim().Split('|'); pts = int.Parse(p[0]); streak = int.Parse(p[1]); lastDate = p[2]; if (p.Length > 5 && p[3] == DateTime.Now.ToString("yyyyMMdd")) { rubToday = int.Parse(p[4]); gameToday = int.Parse(p[5]); } } } catch { }
	}
	private static void Save() { try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(P, pts + "|" + streak + "|" + lastDate + "|" + DateTime.Now.ToString("yyyyMMdd") + "|" + rubToday + "|" + gameToday); } catch { } }
	public static int Points { get { Load(); return pts; } }
	public static int Level { get { Load(); int l = 0; for (int i = 0; i < Th.Length; i++) if (pts >= Th[i]) l = i; return l; } }
	public static string Info()
	{
		int l = Level; string nx = l + 1 < Th.Length ? " (" + (pts - Th[l]) + "/" + (Th[l + 1] - Th[l]) + ")" : " (최고 단계)";
		return "호감도 Lv." + (l + 1) + " " + Names[l] + nx;
	}
	public static void Add(int n)
	{
		Load(); int before = Level; pts += n; Save(); int after = Level; if (after > before && OnLevel != null) { try { OnLevel(after); } catch { } }
	}
	public static void Rub() { Load(); if (rubToday >= 40) return; rubToday++; Add(1); }
	public static void Game() { Load(); if (gameToday >= 8) return; gameToday++; Add(2); }
	public static void Daily(string what) { Load(); string key = DateTime.Now.ToString("yyyyMMdd") + what; if (key == lastDate2) return; lastDate2 = key; Add(2); }
	private static string lastDate2 = "";
	// 오늘 처음 만났을 때 (연속으로 만나면 보너스) · 인사말을 돌려줘요
	public static string Visit()
	{
		Load(); if (vis) return null; vis = true; string today = DateTime.Now.ToString("yyyyMMdd"); if (lastDate == today) return null;
		bool cont = lastDate == DateTime.Now.AddDays(-1).ToString("yyyyMMdd"); streak = cont ? streak + 1 : 1; lastDate = today; rubToday = 0; gameToday = 0; Add(3 + Math.Min(5, streak - 1));
		int l = Level; string t = streak >= 3 ? streak + "일째 만나고 있어요!" : "오늘도 와 줬네요!";
		return l >= 3 ? t + " 보고 싶었어요" : t;
	}
	public static readonly string[] Perks = { "기본 쓰다듬기 반응", "쓰다듬을 때 다정한 말", "인사말에 보고 싶었다는 말 · 야근 응원", "10번 쓰다듬으면 하이파이브", "매일 첫 만남에 오늘의 운세 한 줄", "야근 응원이 더 자주", "쓰다듬기 하트가 더 자주", "특별한 인사와 가장 다정한 대사" };
	public static string[] Lines()
	{
		int l = Level;
		if (l <= 0) return new[] { "조금 부끄러워요…", "헤헤, 고마워요" };
		if (l == 1) return new[] { "쓰다듬어 주니 좋아요", "오늘도 힘내요!" };
		if (l == 2) return new[] { "옆에 있으면 든든해요", "당신 손길이 좋아요", "같이 있어서 좋아요" };
		if (l == 3) return new[] { "우린 단짝이에요!", "퇴근할 때까지 같이 버텨요", "마룡이는 당신 편이에요" };
		if (l == 4) return new[] { "절친이라고 불러도 되죠?", "오늘 고생 많았어요, 정말로", "힘들면 말해 줘요, 들어줄게요" };
		return new[] { "당신이 최고예요", "계속 곁에 있을게요", "야근해도 퇴근해도 마룡이는 같은 편!", "함께라서 행복해요" };
	}
}
