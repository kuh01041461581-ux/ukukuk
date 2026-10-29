using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

internal partial class Program
{
	// PC를 켠 뒤 처음 실행됐을 때(하루 한 번) 오늘의 운세를 알림으로 보여줘요
	internal static void FoStartup()
	{
		try
		{
			string today = DateTime.Now.ToString("yyyy-MM-dd"); if (!foStart || foLast == today || tray == null) return; foLast = today; SaveSettings();
			string line = FortuneForm.TodayLine(); tray.BalloonTipTitle = "마룡이 오늘의 운세"; tray.BalloonTipText = line == null ? "생일을 넣어 두면 매일 운세를 알려 드려요 (눌러서 열기)" : line.Replace("\n", " "); foClickUntil = DateTime.Now.AddSeconds(30); tray.ShowBalloonTip(8000);
		}
		catch { }
	}
	internal static bool petOn; internal static int petSize = 1, bubbleSize = 2; internal static bool petRoam;
	private static AlertPopup alert;
	private static Slot snoozeSlot;
	private static DateTime snoozeAt = DateTime.MinValue;
	internal static Action<int> settingsGo;
	internal static Action<int, int> settingsSub;   // 같은 메뉴 안의 위쪽 탭 이동 (옛 쪽, 새 쪽): 화면은 바로 바꾸고 강조 알약만 미끄러져요
	internal static int MonthlyLimit { get { return monthlyLimit; } set { monthlyLimit = Math.Max(1, Math.Min(300, value)); SaveSettings(); } }

	internal static void LogErr(Exception e) { LogError(e); }
	internal static bool SoundOn
	{
		get { return soundOn; }
		set
		{
			soundOn = value;
			if (soundItem != null) soundItem.Checked = soundOn;
			SaveSettings();
		}
	}
	internal static Icon AppIcon { get { return appIcon; } }


	// 마룡이 옷장: '마룡이옷장' 폴더에 넣어 둔 그림(PNG)이 있으면 가끔씩 무작위로 갈아입어요.
	// 파일 이름에 '한복'이 들어가면 설·추석에, '산타'가 들어가면 크리스마스 무렵에 입어요. 폴더가 비어 있으면 원래 모습 그대로.
	internal static string WardrobeDir { get { return Path.Combine(DataDir, "마룡이옷장"); } }
	private static Bitmap outfitImg; private static string outfitFile = ""; private static DateTime outfitAt = DateTime.MinValue; private static readonly Random outfitRnd = new Random();
	private static string[] OutfitList()
	{
		List<string> l = new List<string>();
		try { foreach (string n in System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames()) if (n.StartsWith("outfit.")) l.Add("res:" + n); } catch { }
		try { if (Directory.Exists(WardrobeDir)) l.AddRange(Directory.GetFiles(WardrobeDir, "*.png")); } catch { }
		return l.ToArray();
	}
	private static string OName(string k) { return k.StartsWith("res:") ? k.Substring(4) : Path.GetFileName(k); }
	// 날짜에 맞는 옷: 0 = 오늘은 안 입음, 1 = 평소에 입어도 됨, 2 = 오늘이 그날(우선)
	private static bool Win(int md, int a, int b) { return a <= b ? (md >= a && md <= b) : (md >= a || md <= b); }
	private static int OutfitFit(string name, DateTime n)
	{
		int md = n.Month * 100 + n.Day;
		string fe = Hanbok.Festival(n);
		Func<string, bool> has = delegate(string k) { return name.Contains(k); };
		// 이름에 들어간 말로 어느 날 입는 옷인지 정해요 (오늘이 그날이면 2, 그날이 아니면 0, 상관없으면 1)
		if (has("산타") || has("크리스마스")) return Win(md, 1220, 1225) ? 2 : 0;
		if (has("신정") && !has("새해")) return Win(md, 1229, 103) ? 2 : Win(md, 1201, 228) ? 1 : 0;
		if (has("새해") || has("해맞이")) return Win(md, 1230, 103) ? 2 : 0;
		if (has("겨울")) return Win(md, 1201, 228) ? 1 : 0;
		if (has("설날")) return fe == "설" ? 2 : 0;
		if (has("보름달") || has("송편") || has("풍성")) return fe == "추석" ? 2 : 0;
		if (has("추석")) return fe == "추석" ? 2 : 0;
		if (has("정월대보름")) return Hanbok.LunarNear(n, 1, 15, 1) ? 2 : 0;
		if (has("부처님오신날")) return Hanbok.LunarNear(n, 4, 8, 1) ? 2 : 0;
		if (has("단오")) return Hanbok.LunarNear(n, 5, 5, 1) ? 2 : 0;
		if (has("동지")) return Win(md, 1221, 1222) ? 2 : 0;
		if (has("김장")) return Win(md, 1110, 1210) ? 1 : 0;
		if (has("발렌타인")) return Win(md, 210, 214) ? 2 : 0;
		if (has("화이트데이")) return Win(md, 312, 314) ? 2 : 0;
		if (has("삼일절")) return Win(md, 227, 301) ? 2 : 0;
		if (has("물의날")) return Win(md, 321, 322) ? 2 : 0;
		if (has("식목일")) return Win(md, 404, 405) ? 2 : 0;
		if (has("지구의날")) return Win(md, 421, 422) ? 2 : 0;
		if (has("장애인의날")) return Win(md, 419, 420) ? 2 : 0;
		if (has("과학의날")) return Win(md, 420, 421) ? 2 : 0;
		if (has("근로자의날")) return Win(md, 430, 501) ? 2 : 0;
		if (has("어린이날")) return Win(md, 501, 505) ? 2 : 0;
		if (has("어버이날")) return Win(md, 506, 508) ? 2 : 0;
		if (has("스승의날")) return Win(md, 513, 515) ? 2 : 0;
		if (has("우편의날")) return Win(md, 520, 522) ? 2 : 0;
		if (has("간호사의날")) return Win(md, 511, 512) ? 2 : 0;
		if (has("바다의날")) return Win(md, 530, 531) ? 2 : 0;
		if (has("환경의날")) return Win(md, 604, 605) ? 2 : 0;
		if (has("현충")) return Win(md, 605, 606) ? 2 : 0;
		if (has("장마철")) return Win(md, 620, 720) ? 1 : 0;
		if (has("여름")) return Win(md, 615, 831) ? 1 : 0;
		if (has("정보보호의날")) return Win(md, 709, 711) ? 2 : 0;
		if (has("광복절")) return Win(md, 812, 815) ? 2 : 0;
		if (has("피크닉")) return (Win(md, 405, 610) || Win(md, 910, 1031)) ? 1 : 0;
		if (has("운동회")) return (Win(md, 501, 531) || Win(md, 915, 1031)) ? 1 : 0;
		if (has("군복")) return Win(md, 930, 1001) ? 2 : 1;
		if (has("한글날")) return Win(md, 1007, 1009) ? 2 : 0;
		if (has("태극기")) return (Win(md, 227, 301) || Win(md, 812, 815) || Win(md, 1001, 1003) || md == 1009) ? 2 : 0;
		if (has("경찰의날")) return Win(md, 1019, 1021) ? 2 : 0;
		if (has("산의날")) return Win(md, 1017, 1018) ? 2 : 0;
		if (has("독도의날")) return Win(md, 1023, 1025) ? 2 : 0;
		if (has("할로윈")) return Win(md, 1024, 1031) ? 2 : 0;
		if (has("가을")) return Win(md, 915, 1115) ? 1 : 0;
		if (has("소방의날")) return Win(md, 1107, 1109) ? 2 : 0;
		if (has("농민의날")) return Win(md, 1109, 1111) ? 2 : 0;
		if (has("수능일")) return Win(md, 1110, 1120) ? 2 : 0;
		if (has("교통안전의날")) return Win(md, 1203, 1205) ? 2 : 0;
		if (has("졸업식")) return Win(md, 205, 215) ? 2 : 0;
		if (has("봄")) return Win(md, 320, 520) ? 1 : 0;
		if (has("한복")) return fe != null ? 2 : 1;
		return 1;
	}
	// 마룡이 옷은 하루에 한 번만 정해요 (특별한 날 옷을 입히려고 만든 기능이라, 열 때마다 바뀌지 않게 날짜별로 기억하고 다시 켜도 그대로예요)
	private static string dayPickDay = "", dayPick;
	private static void SaveDayPick(string day, string v) { dayPickDay = day; dayPick = v; try { Directory.CreateDirectory(DataDir); File.WriteAllLines(Path.Combine(DataDir, "outfit_today.txt"), new string[] { day, v }, System.Text.Encoding.UTF8); } catch { } }
	private static string PickOutfit(DateTime n)
	{
		try
		{
			if (!Directory.Exists(WardrobeDir)) { try { Directory.CreateDirectory(WardrobeDir); } catch { } }
			string[] all = OutfitList();
			if (all.Length == 0) return null;
				string today = n.ToString("yyyy-MM-dd");
				if (dayPick != null && dayPickDay == today && (dayPick.Length == 0 || all.Contains(dayPick))) return dayPick.Length == 0 ? null : dayPick;
				try { string dfile = Path.Combine(DataDir, "outfit_today.txt"); if (File.Exists(dfile)) { string[] ln = File.ReadAllLines(dfile, System.Text.Encoding.UTF8); if (ln.Length >= 2 && ln[0] == today && (ln[1].Length == 0 || all.Contains(ln[1]))) { dayPickDay = today; dayPick = ln[1]; return ln[1].Length == 0 ? null : ln[1]; } } } catch { }
			Dictionary<string, int> fit = new Dictionary<string, int>();
			int best = 0;
			foreach (string f in all) { int v = OutfitFit(OName(f), n); fit[f] = v; if (v > best) best = v; }
			string[] pool = all.Where(f => fit[f] == (best == 2 ? 2 : 1)).ToArray();
			if (pool.Length == 0) return null;
			outfitAt = n;
			int pick = outfitRnd.Next(best == 2 ? pool.Length : pool.Length + 2);   // 평소엔 원래 모습도 후보에 넣어요
			if (pick >= pool.Length) { SaveDayPick(today, ""); return null; }
			if (pool.Length > 1 && pool[pick] == outfitFile) pick = (pick + 1) % pool.Length;   // 같은 옷이 연달아 나오지 않게
			SaveDayPick(today, pool[pick]); return pool[pick];
		}
		catch { return null; }
	}

	// 수박 게임용: 마룡이 옷 10가지(마지막은 원래 모습)
	internal static KeyValuePair<string, Image>[] GameFaces(int n)
	{
		List<KeyValuePair<string, Image>> r = new List<KeyValuePair<string, Image>>();
		try
		{
			string[] all = OutfitList(); Random rd = new Random();
			for (int i = all.Length - 1; i > 0; i--) { int j = rd.Next(i + 1); string t = all[i]; all[i] = all[j]; all[j] = t; }
			foreach (string f in all)
			{
				if (r.Count >= n - 1) break;
				try
				{
					Stream st = f.StartsWith("res:") ? System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(f.Substring(4)) : (Stream)File.OpenRead(f);
					using (st) using (Bitmap raw = new Bitmap(st))
					{
						float k = Math.Min(1f, 220f / Math.Max(raw.Width, raw.Height));
						Bitmap b = new Bitmap(Math.Max(1, (int)(raw.Width * k)), Math.Max(1, (int)(raw.Height * k)));
						using (Graphics g = Graphics.FromImage(b)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(raw, 0, 0, b.Width, b.Height); }
						string nm = OName(f); if (nm.StartsWith("outfit.")) nm = nm.Substring(7); if (nm.EndsWith(".png")) nm = nm.Substring(0, nm.Length - 4);
						r.Add(new KeyValuePair<string, Image>(nm.Replace('_', ' '), b));
					}
				}
				catch { }
			}
		}
		catch { }
		if (mascotImg == null) mascotImg = LoadMascot();
		while (r.Count < n - 1) r.Add(new KeyValuePair<string, Image>("마룡이", mascotImg));
		r.Add(new KeyValuePair<string, Image>("마룡이", mascotImg));
		return r.ToArray();
	}

	// ── 오늘 뭐 먹지? : 점심/저녁 메뉴 추천 + 네이버 지도로 주변 식당 찾기 ──
	private static readonly string[] LunchMenus = { "김치찌개", "된장찌개", "순두부찌개", "부대찌개", "제육볶음", "비빔밥", "불고기 정식", "칼국수", "냉면", "국밥", "순대국", "설렁탕", "갈비탕", "삼계탕", "돈가스", "초밥", "짜장면", "짬뽕", "볶음밥", "쌀국수", "샐러드", "샌드위치", "햄버거", "김밥", "떡볶이", "라멘", "우동", "쭈꾸미", "오징어볶음", "닭갈비", "곰탕", "추어탕", "콩나물국밥", "백반", "보쌈", "족발", "낙지볶음", "동태탕", "해물탕", "파스타" };
	private static readonly string[] DinnerMenus = { "삼겹살", "갈비", "치킨", "곱창", "회", "족발", "보쌈", "닭갈비", "감자탕", "찜닭", "쭈꾸미", "낙지볶음", "해물탕", "샤브샤브", "스테이크", "피자", "파스타", "초밥", "막걸리에 파전", "닭발", "오리구이", "대패삼겹살", "양꼬치", "돈가스", "부대찌개", "순대국", "국밥", "곰장어" };
	private static string lastMenu = ""; private static readonly Random menuRnd = new Random();
	// ── 새 버전을 실행하면 이전 버전(다른 파일)을 자동으로 종료하고 지워요 ──
	// 월별 기록을 엑셀에서 바로 열리는 CSV(UTF-8, BOM)로 바탕화면에 저장해요
	internal static string ExportMonthCsv(int y, int m)
	{
		List<Rec> rows = Records.All().Where(r => r.Date.Year == y && r.Date.Month == m).OrderBy(r => r.Date).ThenBy(r => r.Hour).ToList();
		if (rows.Count == 0) return null;
		StringBuilder sb = new StringBuilder(); sb.AppendLine("날짜,요일,시작,종료,시간,구분");
		foreach (var grp in rows.GroupBy(r => r.Date))
		{
			DateTime d = grp.Key; bool off = IsOffDay(d); int tot = 0;
			foreach (Rec r in grp) { tot++; sb.AppendLine(d.ToString("yyyy-MM-dd") + "," + Records.Dow[(int)d.DayOfWeek] + "," + r.Hour + ":00," + (r.Hour + 1) + ":00,1," + (off ? "휴일" : "평일")); }
			sb.AppendLine(d.ToString("yyyy-MM-dd") + ",,,,일계 " + tot + (Records.HasPre8(d) ? ",8시 이전 근무 표시" : ""));
		}
		sb.AppendLine(",,,,월 합계 " + rows.Count + "시간,");
		string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); if (!Directory.Exists(dir)) dir = Program.DataDir;
		string path = Path.Combine(dir, "초과근무기록_" + y + "-" + m.ToString("00") + ".csv");
		File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); return path;
	}
	internal static bool replacedOld = false;
	internal static int ReplaceOldInstances()
	{
		int killed = 0;
		try
		{
			List<string> toDelete = new List<string>();
			string me = Application.ExecutablePath; DateTime myTime = File.GetLastWriteTimeUtc(me); int myId = System.Diagnostics.Process.GetCurrentProcess().Id;
						foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())
			{
				try
				{
					if (p.Id == myId) continue; string path = p.MainModule.FileName; string name = Path.GetFileName(path);
					bool ours = name.IndexOf("초과근무", StringComparison.Ordinal) >= 0; if (!ours) { string prod = p.MainModule.FileVersionInfo.ProductName; ours = prod == "초과근무 기록 알리미" || prod == "초과근무 알리미"; }
					if (!ours || string.Equals(path, me, StringComparison.OrdinalIgnoreCase)) continue;
					try { LogErr(new Exception("replace-check: " + path + " mtime=" + File.GetLastWriteTimeUtc(path).ToString("o") + " me=" + myTime.ToString("o"))); } catch { }
					if (File.GetLastWriteTimeUtc(path) > myTime) continue;   // 더 새 파일이 켜져 있으면 건드리지 않아요
					p.Kill(); p.WaitForExit(4000); killed++; toDelete.Add(path);
				}
				catch (Exception) { }
			}
			foreach (string f in toDelete)
				for (int i = 0; i < 12; i++) { try { if (File.Exists(f)) File.Delete(f); break; } catch { System.Threading.Thread.Sleep(250); } }
		}
		catch (Exception ex) { LogErr(ex); }
		if (killed > 0) replacedOld = true; return killed;
	}
		internal static string SearchBase = "익산시";
	// 홈 카드: 평일 11:30 점심, 18:00 퇴근까지 남은 시간
	internal static string CountdownText(DateTime n, out string cap)
	{
		if (IsHoliday(n) || IsWeekend(n)) { cap = "점심 · 퇴근"; return "오늘은 쉬는 날이에요"; }
		DateTime lunch = n.Date.AddHours(11.5), lunchEnd = n.Date.AddHours(13), quit = n.Date.AddHours(18);
		if (n < lunch) { cap = "점심시간까지"; return Fmt(lunch - n); }
		if (n < lunchEnd) { cap = "점심시간이에요 · 13:00까지"; return "맛있게 드세요"; }
		if (n < quit) { cap = "퇴근까지"; return Fmt(quit - n); }
		cap = "퇴근 시간이에요"; return "수고하셨어요";
	}
	private static string Fmt(TimeSpan t) { int s = (int)Math.Ceiling(t.TotalSeconds); if (s < 0) s = 0; return (s / 3600) + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00"); }
	internal static string PickMenuAndSearch()
	{
		string m; int guard = 0; do { m = LunchMenus[menuRnd.Next(LunchMenus.Length)]; } while (m == lastMenu && ++guard < 8);
		lastMenu = m; OpenNaverMap(m); return m;
	}
	internal static void OpenNaverMap(string menu)
	{
		try
		{
			string q = SearchBase + (string.IsNullOrEmpty(menu) ? " 맛집" : " " + menu);
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://map.naver.com/p/search/" + Uri.EscapeDataString(q)) { UseShellExecute = true });
		}
		catch (Exception ex) { LogErr(ex); }
	}
	internal static void SuggestMenu()
	{
		DateTime n = DateTime.Now; bool lunch = n.Hour < 15; string[] l = lunch ? LunchMenus : DinnerMenus;
		string m; int guard = 0; do { m = l[menuRnd.Next(l.Length)]; } while (m == lastMenu && ++guard < 8);
		lastMenu = m;
		string head = lunch ? "오늘 점심은 " : "오늘 저녁은 "; string[] tail = { " 어때요?", " 먹어요!", " 한 그릇 어때요?", " 땡기지 않아요?" };
		Bubble.Say(head + m + tail[menuRnd.Next(tail.Length)] + "\n(누르면 근처 식당을 찾아요)", 9, delegate { OpenNaverMap(m); });
	}
	private static readonly Dictionary<string, Image> outfitCache = new Dictionary<string, Image>();
	// 게임마다 어울리는 의상 마룡이 (이름은 outfits 폴더의 파일 이름)
	internal static Image OutfitImage(string name)
	{
		try
		{
			Image hit; if (outfitCache.TryGetValue(name, out hit)) return hit;
			using (Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("outfit." + name + ".png"))
			{
				if (st == null) { outfitCache[name] = null; return null; }
				using (Bitmap raw = new Bitmap(st)) { Bitmap b = new Bitmap(raw); outfitCache[name] = b; return b; }
			}
		}
		catch { return null; }
	}
	internal static Image MascotImage()
	{
		if (mascotImg == null) mascotImg = LoadMascot();
		if (mascotImg == null) return (Image)ClockBitmap(160);
		string f = PickOutfit(DateTime.Now);
		if (f == null) { outfitFile = ""; return (Image)mascotImg; }
		try
		{
			if (f != outfitFile || outfitImg == null)
			{
				Stream st = f.StartsWith("res:") ? System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(f.Substring(4)) : (Stream)File.OpenRead(f);
				using (st)
				using (Bitmap raw = new Bitmap(st))
				{
					float k = Math.Min(1f, 420f / Math.Max(raw.Width, raw.Height));
					Bitmap b = new Bitmap(Math.Max(1, (int)(raw.Width * k)), Math.Max(1, (int)(raw.Height * k)));
					using (Graphics g = Graphics.FromImage(b)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(raw, 0, 0, b.Width, b.Height); }
					outfitImg = b; outfitFile = f;
				}
			}
			return outfitImg;
		}
		catch { outfitFile = ""; return (Image)mascotImg; }
	}

	internal static void ShowHistory()
	{
		ShowSettings();
		if (settingsGo != null) settingsGo(2);
	}

	// ── 다시 알림 ──
	internal static void DoSnooze(Slot s, int minutes, string prefix = "")
	{
		snoozeSlot = s;
		snoozeAt = DateTime.Now.AddMinutes(minutes);
		try
		{
			tray.BalloonTipTitle = "초과근무 알리미";
			tray.BalloonTipText = prefix + minutes + "분 뒤(" + snoozeAt.ToString("HH:mm") + ")에 다시 알려드릴게요. " + s.Hour + "시 확인 마감: " + s.Deadline.ToString("HH:mm");
			tray.ShowBalloonTip(3000);
		}
		catch { }
		UpdateTrayText();
	}

	internal static bool InsarangOnConfirm { get { return insarangFront; } }
	// '이번 시간 무시': 이 시간대는 기록도 놓침 표시도 하지 않고 알림을 끝낸다
	internal static void SkipSlot(Slot s)
	{
		snoozeSlot = null; if (pending != null && pending.Start == s.Start) pending = null;
		Balloon("이번 시간 알림을 껐어요", s.Hour + "시는 기록 없이 넘어가요. 다음 시간부터 다시 알려드려요.", 3000);
		UpdateTrayText();
	}
	// 알림창의 '방해 금지 켜기': 방해 금지를 켜고 창을 닫는다. 이번 시간 알림은 방해 금지가 끝나면(마감 전이면) 다시 알려 준다
	internal static void DndFromPopup(Slot s)
	{
		if (!dndManual) ToggleDnd();
		snoozeSlot = null; if (pending != null && pending.Start == s.Start) pendingSuppressed = true;
		Balloon("방해 금지 켜짐", s.Hour + "시 알림은 방해 금지를 끄면 마감(" + s.Deadline.ToString("HH:mm") + ") 전까지 다시 알려드려요.", 4000);
	}
	internal static void CancelSnooze()
	{
		snoozeSlot = null;
		UpdateTrayText();
	}

	private static void SnoozeTick(DateTime now)
	{
		if (snoozeSlot == null || now < snoozeAt) return;
		Slot s = snoozeSlot;
		snoozeSlot = null;
		if (now < s.Deadline) Alert(s, true);
		UpdateTrayText();
	}

	// ── 알림 ──
	private static void Notify(DateTime n, bool test = false)
	{
		Slot s = new Slot { At = n, Start = new DateTime(n.Year, n.Month, n.Day, n.Hour, 0, 0), Test = test };
		// 이미 이 시간대를 확인·기록했으면 같은 시간대의 두 번째 알림은 건너뜀 (테스트는 항상 울림)
		if (!test && Records.IsConfirmed(s.Start)) return;
		snoozeSlot = null;
		if (!test) { pending = s; pendingWarned = false; pendingSuppressed = false; }
		Alert(s, false);
	}

	private static void Alert(Slot s, bool again)
	{
		if (s.Test) PhonePush("\U0001F9EA [테스트] 초과근무 알림", "\U0001F409 마룡이가 알려드려요\n알림 테스트예요. 휴대폰 알림이 잘 연결됐어요! \u2728");
		else PhonePush((again ? "\U0001F514 [다시 알림] " : "\u23F0 ") + s.Hour + "시 초과근무 확인 시간이에요", "\U0001F409 마룡이가 알려드려요\n인사랑에서 확인을 눌러주세요 \U0001F4DD (마감 " + s.Deadline.ToString("HH:mm") + ")");
		if (!s.Test && DndActive)
		{
			pendingSuppressed = true;
			Balloon("방해 금지 중", s.Hour + "시 초과근무 확인 시간이에요. 눌러서 열기 · 마감 " + s.Deadline.ToString("HH:mm"), 6000);
			return;
		}
		ShowAlertWindow(s, again);   // 화면(팝업)을 먼저 띄우고, 소리·풍선은 바로 뒤에 (먼저 하면 그만큼 팝업이 늦게 떠요)
		if (soundOn)
		{
			System.Windows.Forms.Timer st2 = new System.Windows.Forms.Timer(); st2.Interval = 40;
			st2.Tick += delegate
			{
				st2.Stop(); st2.Dispose();
				try
				{
					Sfx.PlayAlert();
					tray.BalloonTipTitle = "초과근무 알리미";
					tray.BalloonTipText = (again ? "[다시 알림] " : "") + s.Hour + "시 초과근무 확인 시간입니다. 인사랑에서 직접 확인을 눌러주세요.";
					tray.ShowBalloonTip(5000);
				}
				catch { }
			};
			st2.Start();
		}
	}

	private static void PrebuildGlowLater(int ms) { System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = ms; t.Tick += delegate { t.Stop(); t.Dispose(); Glow.Prepare(); }; t.Start(); }

	private static void ShowAlertWindow(Slot s, bool again)
	{
		try
		{
			// 팝업을 먼저 만든다. 팝업이 실패하면 글로우만 덩그러니 남지 않게 한다.
			System.Diagnostics.Stopwatch aw = System.Diagnostics.Stopwatch.StartNew(); Action<string> am = delegate(string n0) { if (Anim.PerfLog) Anim.Mark("알림 " + n0 + " " + aw.ElapsedMilliseconds + "ms"); };
			if (lightAlert)
			{   // 간단 알림: 큰 팝업·글로우 없이 위쪽 리본 + 오른쪽 아래 작은 카드만
				if (alert != null && !alert.IsDisposed && !alert.Closing) alert.Dismiss();
				AlertToast.ShowFor(s, again); AlertBanner.Pop(); return;
			}
			bool reuse = alert != null && !alert.IsDisposed && !alert.Closing;
			if (reuse)
			{
				alert.SetSlot(s, again);
			}
			else
			{
				AlertPopup p = AlertPopup.TakeSpare(s, again) ?? new AlertPopup(s, again);
				p.FormClosed += delegate { if (alert == p) alert = null; AlertBanner.Hide(); Mem.Later(3000); AlertPopup.PrebuildLater(2500); };
				alert = p;
				am("팝업 준비");
				p.Begin();
				am("팝업 표시");
				WatchPopup(p, s);
			}
			int st = Theme.StateFor(s.Deadline - DateTime.Now);
			if (st != 3) Glow.Show(st);
			am("글로우");
			alert.TopMost = false;
			alert.TopMost = true;   // 글로우 위로
			am("끝");
			System.Windows.Forms.Timer bt = new System.Windows.Forms.Timer(); bt.Interval = 90;   // 팝업이 먼저 뜨고, 위쪽 띠는 한 박자 뒤에 (한꺼번에 만들면 멈칫해요)
			bt.Tick += delegate { bt.Stop(); bt.Dispose(); AlertBanner.Pop(); };
			bt.Start();
		}
		catch (Exception ex)
		{
			LogError(ex);
			Glow.Hide();
			try { if (alert != null && !alert.IsDisposed) alert.Close(); } catch { }
			alert = null;
			ShowSimple(s, ex.GetType().Name + ": " + ex.Message);
		}
	}

	// 팝업이 1.5초 뒤에도 화면에 안 보이면(오류 없이 숨은 경우 포함) 기록하고 기본 창으로 대신 알린다.
	private static void WatchPopup(AlertPopup p, Slot s)
	{
		Timer chk = new Timer();
		chk.Interval = 1500;
		chk.Tick += delegate
		{
			chk.Stop(); chk.Dispose();
			try
			{
				if (p.IsDisposed || p.Closing) return;
				bool onScreen = Screen.AllScreens.Any(sc => sc.Bounds.IntersectsWith(p.Bounds));
				if (!p.Visible || p.Opacity < 0.5 || !onScreen)
				{
					LogError(new Exception("팝업이 보이지 않는 상태: Visible=" + p.Visible + " Opacity=" + p.Opacity + " Bounds=" + p.Bounds + " onScreen=" + onScreen));
					ShowSimple(s, "팝업이 화면에 나타나지 않았어요 (Visible=" + p.Visible + ", Opacity=" + p.Opacity + ")");
				}
			}
			catch (Exception ex) { LogError(ex); }
		};
		chk.Start();
		// 확인 안 하고 두면 40초마다 다시 소리를 내고 팝업을 맨 앞으로 올려요 (마감 전까지)
		Timer nag = new Timer(); nag.Interval = 40000;
		nag.Tick += delegate
		{
			try
			{
				if (p.IsDisposed || p.Closing || DateTime.Now >= s.Deadline) { nag.Stop(); nag.Dispose(); return; }
				if (DndActive) return;
				if (soundOn) Sfx.PlayAlert();
				p.TopMost = false; p.TopMost = true;
			}
			catch { try { nag.Stop(); } catch { } }
		};
		nag.Start();
	}

	private static Form simpleWin;

	// 예쁜 팝업이 안 될 때 쓰는 기본 알림 창 (기록 · 다시 알림은 똑같이 동작)
	private static void ShowSimple(Slot s, string why)
	{
		try
		{
			if (simpleWin != null && !simpleWin.IsDisposed) simpleWin.Close();
			Form f = new Form();
			simpleWin = f;
			f.Text = "초과근무 알리미";
			f.TopMost = true;
			f.ShowInTaskbar = true;
			f.StartPosition = FormStartPosition.CenterScreen;
			f.FormBorderStyle = FormBorderStyle.FixedDialog;
			f.MaximizeBox = false; f.MinimizeBox = false;
			f.ClientSize = new Size(380, 190);
			f.Font = new Font("맑은 고딕", 10f);
			Label t = new Label { Text = s.Hour + "시 초과근무 확인 시간이에요", Font = new Font("맑은 고딕", 13f, FontStyle.Bold), Location = new Point(14, 12), Size = new Size(352, 28) };
			Label d = new Label { Text = "인사랑을 열고 초과근무기록 - 확인을 눌러주세요\r\n마감: " + s.Deadline.ToString("HH:mm"), Location = new Point(14, 46), Size = new Size(352, 44) };
			Button ok = new Button { Text = "확인했어요", Location = new Point(14, 100), Size = new Size(110, 34) };
			Button b5 = new Button { Text = "5분 뒤", Location = new Point(134, 100), Size = new Size(70, 34) };
			Button b10 = new Button { Text = "10분 뒤", Location = new Point(210, 100), Size = new Size(70, 34) };
			Button b15 = new Button { Text = "15분 뒤", Location = new Point(286, 100), Size = new Size(70, 34) };
			Label w = new Label { Text = "※ 기본 알림 창입니다 · " + why, ForeColor = Color.Gray, Font = new Font("맑은 고딕", 8f), Location = new Point(14, 146), Size = new Size(352, 36) };
			TimeSpan left = s.Deadline - DateTime.Now;
			b5.Enabled = left.TotalMinutes > 6; b10.Enabled = left.TotalMinutes > 11; b15.Enabled = left.TotalMinutes > 16;
			ok.Click += delegate
			{
				if (!s.Test && !Records.IsConfirmed(s.Start)) Records.Add(s, DateTime.Now);
				CancelSnooze();
				int n = Records.CountOn(s.Start.Date);
				try { tray.BalloonTipTitle = "기록했어요"; tray.BalloonTipText = "오늘 " + n + "회 · " + n + "시간 / 총 " + Records.CountAll() + "시간"; tray.ShowBalloonTip(3000); } catch { }
				f.Close();
			};
			b5.Click += delegate { DoSnooze(s, 5); f.Close(); };
			b10.Click += delegate { DoSnooze(s, 10); f.Close(); };
			b15.Click += delegate { DoSnooze(s, 15); f.Close(); };
			f.Controls.AddRange(new Control[] { t, d, ok, b5, b10, b15, w });
			f.Show();
			f.Activate();
		}
		catch (Exception ex2)
		{
			LogError(ex2);
			try { MessageBox.Show(s.Hour + "시 초과근무 확인 시간입니다.\r\n인사랑에서 확인을 눌러주세요.\r\n\r\n(" + why + ")", "초과근무 알리미"); } catch { }
		}
	}

	// 시작하고 잠깐 뒤(아무도 안 쓰는 때)에 미리 해 두는 준비운동: 글꼴·그림·그리기 코드를 먼저 한 번씩 써 봐서, 설정 창을 처음 열 때 버벅이지 않게 해요
	// 이 PC가 화면 전환을 부드럽게 그리기 어려우면(오래 걸리는 전환이 연달아 나오면) 사진 덮개 효과를 끄고 바로 바꿔요
	internal static bool LowPower; private static int slowCnt;
	internal static void NoteSlow(int ms, int limit)
	{
		if (LowPower) return;
		if (ms > limit) slowCnt++; else slowCnt = Math.Max(0, slowCnt - 1);
		if (slowCnt >= 3) { LowPower = true; Anim.Mark("느린 PC 모드 켬"); }
	}
	internal static void Warm()
	{
		Bubble.Prepare();
		try
		{
			using (Bitmap bm = new Bitmap(320, 80))
			using (Graphics g = Graphics.FromImage(bm))
			{
				Gfx.HQ(g);
				float[] pts = { 9f, 9.5f, 10f, 10.5f, 11f, 11.5f, 12f, 14f, 16f, 17f, 22f, 30f };
				foreach (float pt in pts) for (int w = 0; w < 3; w++)
				{
					Font f = Fonts.F(pt, w);
					Gfx.Text(g, "초과근무 확인 시간이에요 0123456789 : 가나다", f, Color.Black, new RectangleF(0, 0, 320, 40), StringAlignment.Near, StringAlignment.Center, false);
					Gfx.Text(g, "초과근무 확인 시간이에요 0123456789 : 가나다", f, Color.FromArgb(200, 0, 0, 0), new RectangleF(0, 0, 320, 40), StringAlignment.Near, StringAlignment.Center, false);
					Gfx.Measure(g, "초과근무", f);
				}
			}
		}
		catch { }
		try
		{
			Image mi = MascotImage();
			using (SkyBanner sb = new SkyBanner())
			{
				sb.Size = new Size(Dpi.Z(560), Dpi.Z(140)); sb.Mascot = mi; sb.TimeText = "12:34:56"; sb.Caption = "지금 시간"; sb.DateText = "10월 14일 수요일"; sb.Radius = 24; sb.Alive = true;
				using (Bitmap b2 = new Bitmap(sb.Width, sb.Height)) { sb.DrawToBitmap(b2, new Rectangle(0, 0, sb.Width, sb.Height)); sb.DrawToBitmap(b2, new Rectangle(0, 0, sb.Width, sb.Height)); }
			}
		}
		catch { }
		try { PrebuildSettings(); } catch (Exception ex) { LogError(ex); }
		try { GC.Collect(); } catch { }
	}

	// 설정 저장은 화면을 멈추지 않게 뒤에서 해요 (백신이 파일 쓰기를 검사하는 PC에서는 한 번에 수십 ms 걸려서 뚝 끊겼어요). 연달아 바꾸면 마지막 것만 써요
	private static readonly object saveLock = new object();
	private static string[] savePending; private static string savePath; private static bool saving;
	private static string lastSaveSig;
	private static void SaveAsync(string path, string[] lines)
	{
		string sig = path + "\n" + string.Join("\n", lines);
		lock (saveLock) { if (sig == lastSaveSig && !saving) return; lastSaveSig = sig; savePending = lines; savePath = path; if (saving) return; saving = true; }   // 내용이 그대로면 다시 쓰지 않아요
		System.Threading.ThreadPool.QueueUserWorkItem(delegate
		{
			while (true)
			{
				string[] l; string pth;
				lock (saveLock) { l = savePending; pth = savePath; savePending = null; if (l == null) { saving = false; return; } }
				try { System.Threading.Thread.Sleep(250); lock (saveLock) { if (savePending != null) { l = savePending; savePending = null; } } AtomicIO.WriteLines(pth, l, System.Text.Encoding.UTF8); } catch { }
			}
		});
	}
	internal static void FlushSettings() { for (int i = 0; i < 30; i++) { lock (saveLock) { if (!saving) return; } System.Threading.Thread.Sleep(30); } }
	private static ManualViewer manualForm;
	internal static void ShowManual()
	{
		try
		{
			if (manualForm != null && !manualForm.IsDisposed) { if (manualForm.WindowState == FormWindowState.Minimized) manualForm.WindowState = FormWindowState.Normal; manualForm.Activate(); manualForm.BringToFront(); return; }
			manualForm = new ManualViewer(appIcon);
			manualForm.FormClosed += delegate { manualForm = null; Mem.Later(2500); };
			manualForm.Show();
		}
		catch (Exception ex) { LogError(ex); }
	}
	internal static string DataDir { get { return Dir; } }
}
