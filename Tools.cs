using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// ═════ 한글 금액 · 날짜 서식 변환 ═════
internal static class KoText
{
	private static readonly string[] D = { "", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구" };
	private static readonly string[] Big = { "", "만", "억", "조", "경" };
	public static readonly string[] MoneyFmts = { "금1,000,000원(금일백만원)", "1,000,000원(일백만원)", "금일백만원 (한글만)" };
	public static readonly string[] DateFmts = { "2026. 10. 13.(화)", "2026. 10. 13.", "2026년 10월 13일(화요일)" };

	public static string NumToKo(long n)
	{
		if (n == 0) return "영";
		if (n < 0) return "마이너스" + NumToKo(-n);
		List<long> groups = new List<long>();
		while (n > 0) { groups.Add(n % 10000); n /= 10000; }
		StringBuilder sb = new StringBuilder();
		for (int i = groups.Count - 1; i >= 0; i--)
		{
			long g = groups[i]; if (g == 0) continue;
			long th = g / 1000, h = g / 100 % 10, t = g / 10 % 10, o = g % 10;
			if (th > 0) sb.Append(D[th]).Append("천");
			if (h > 0) sb.Append(D[h]).Append("백");
			if (t > 0) sb.Append(D[t]).Append("십");
			if (o > 0) sb.Append(D[o]);
			sb.Append(Big[i]);
		}
		return sb.ToString();
	}

	public static long KoToNum(string s)
	{
		long total = 0, section = 0, num = 0;
		foreach (char c in s)
		{
			int d = "영일이삼사오육칠팔구".IndexOf(c);
			if (d >= 0) { num = d; continue; }
			long mult = c == '십' ? 10 : c == '백' ? 100 : c == '천' ? 1000 : 0;
			if (mult > 0) { section += (num == 0 ? 1 : num) * mult; num = 0; continue; }
			long unit = c == '만' ? 10000L : c == '억' ? 100000000L : c == '조' ? 1000000000000L : 0;
			if (unit > 0) { section += num; total += (section == 0 ? 1 : section) * unit; section = 0; num = 0; }
		}
		return total + section + num;
	}

	private static readonly Regex KoMoney = new Regex("(금\\s*)?([영일이삼사오육칠팔구십백천만억조]+)\\s*원(\\s*정)?");
	private static readonly Regex NumMoney = new Regex("(금\\s*)?(\\d{1,3}(?:,\\d{3})+|\\d+)\\s*(원)?(\\s*정)?");
	private static readonly Regex KoParen = new Regex("\\(\\s*금?\\s*[영일이삼사오육칠팔구십백천만억조]+\\s*원\\s*\\)");

	public static bool HasKoMoney(string s) { return KoMoney.IsMatch(s); }
	public static bool HasNumber(string s) { return Regex.IsMatch(s, "\\d"); }

	// 숫자 → 한글 금액 (형식: 0 금1,000,000원(금일백만원) · 1 1,000,000원(일백만원) · 2 금일백만원)
	public static string MoneyToKo(string s, int fmt)
	{
		// 글 속에 '원'이 붙은 숫자가 있으면 그것만 바꿔요 (조항 번호·날짜·수량이 금액으로 바뀌지 않게). 숫자만 골랐을 때는 그 숫자를 금액으로 봐요
		bool hasWon = Regex.IsMatch(s, "\\d\\s*원"); bool bareOnly = Regex.IsMatch(s.Trim(), "^금?\\s*(\\d{1,3}(?:,\\d{3})+|\\d+)\\s*(원)?(\\s*정)?$");
		if (!hasWon && !bareOnly) return s;
		return NumMoney.Replace(s, delegate(Match m)
		{
			if (hasWon && !m.Groups[3].Success) return m.Value;
			string digits = m.Groups[2].Value.Replace(",", "");
			if (digits.Length > 16) return m.Value;
			long n; if (!long.TryParse(digits, out n)) return m.Value;
			string ko = NumToKo(n);
			string num = n.ToString("N0", CultureInfo.InvariantCulture);
			if (fmt == 1) return num + "원(" + ko + "원)";
			if (fmt == 2) return "금" + ko + "원";
			return "금" + num + "원(금" + ko + "원)";
		});
	}

	// 한글 금액 → 숫자 ("금일백만원" → "1,000,000원")
	public static string KoToMoney(string s)
	{
		string t = KoParen.Replace(s, ""); if (t.Trim().Length == 0) t = s;   // 괄호 속 한글 금액만 골랐으면 그걸 바꿔요
		return KoMoney.Replace(t, delegate(Match m) { return KoToNum(m.Groups[2].Value).ToString("N0", CultureInfo.InvariantCulture) + "원"; });
	}

	// 선택한 글에서 날짜를 찾아 공문서식으로 바꿈. 바꿀 게 없으면 null
	private static readonly Regex DateFull = new Regex("(?<![\\d])(20\\d{2}|\\d{2})\\s*(?:년|[-./])\\s*(\\d{1,2})\\s*(?:월|[-./])\\s*(\\d{1,2})\\s*(?:일|\\.)?(?:\\s*\\(?\\s*[월화수목금토일]\\s*(?:요일)?\\s*\\)?)?(?!\\d)");
	private static readonly Regex DateMD = new Regex("^\\s*(\\d{1,2})\\s*(?:월|[./])\\s*(\\d{1,2})\\s*일?\\s*$");
	private static readonly Regex Date8 = new Regex("^\\s*(20\\d{2})(\\d{2})(\\d{2})\\s*$");

	public static string FormatDate(DateTime d, int fmt)
	{
		string w = Records.Dow[(int)d.DayOfWeek];
		if (fmt == 1) return d.Year + ". " + d.Month + ". " + d.Day + ".";
		if (fmt == 2) return d.Year + "년 " + d.Month + "월 " + d.Day + "일(" + w + "요일)";
		return d.Year + ". " + d.Month + ". " + d.Day + ".(" + w + ")";
	}

	private static bool TryDate(int y, int m, int d, out DateTime dt)
	{
		dt = DateTime.MinValue;
		if (y < 100) y += 2000;
		try { dt = new DateTime(y, m, d); return true; } catch { return false; }
	}

	public static string ConvertDates(string s, int fmt, DateTime today)
	{
		bool any = false;
		string r = DateFull.Replace(s, delegate(Match m)
		{
			DateTime dt;
			if (!TryDate(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), out dt)) return m.Value;
			any = true; return FormatDate(dt, fmt);
		});
		if (any) return r;
		Match m8 = Date8.Match(s); DateTime d8;
		if (m8.Success && TryDate(int.Parse(m8.Groups[1].Value), int.Parse(m8.Groups[2].Value), int.Parse(m8.Groups[3].Value), out d8)) return FormatDate(d8, fmt);
		Match md = DateMD.Match(s); DateTime d2;
		if (md.Success && TryDate(today.Year, int.Parse(md.Groups[1].Value), int.Parse(md.Groups[2].Value), out d2)) return FormatDate(d2, fmt);
		return null;
	}
}

// ═════ 예상 시간외근무수당 ═════
internal class PayGrade
{
	public string Id, Label; public int Base; public int Rate; public bool Verified; public int Fixed;
	public PayGrade(string id, string label, int b, int rate, bool ok) { Id = id; Label = label; Base = b; Rate = rate; Verified = ok; }
}

internal static class Pay
{
	// 기준호봉 봉급액(2026 일반직 봉급표 10호봉) · 봉급기준액 비율 (9급·8급 60%, 7급 이상 55%)
	public static readonly PayGrade[] Grades = {
		new PayGrade("g9", "일반직 9급", 2542700, 60, true),
		new PayGrade("g8", "일반직 8급", 2813000, 60, true),
		new PayGrade("g7", "일반직 7급", 3133300, 55, true),
		new PayGrade("g6", "일반직 6급", 3468700, 55, true),
		new PayGrade("g5", "일반직 5급", 4066800, 55, true),
		new PayGrade("rs", "학예연구사·연구사 (10호봉)", 3343400, 55, true),
		new PayGrade("rc", "학예연구관·연구관 (10호봉 추정)", 4481600, 55, false),
		new PayGrade("ca", "전문경력관 가군 (10호봉 추정)", 4135800, 55, false),
		new PayGrade("cb", "전문경력관 나군 (11호봉)", 3283500, 55, true),
		new PayGrade("cc", "전문경력관 다군 (10호봉 추정)", 2558300, 60, false),
		new PayGrade("xx", "직접 입력", 0, 55, false),
	};

	public static PayGrade Find(string id) { foreach (PayGrade g in Grades) if (g.Id == id) return g; return Grades[2]; }

	public static double HourlyUnit(int baseSalary, int rate) { return baseSalary * (rate / 100.0) / 209.0 * 1.5; }

	public class Result { public int Days, RawHours, Counted, Paid; public double Unit; public long Amount; public bool Capped; }

	// 평일은 하루 1시간 공제, 공휴일·토요일은 전부 인정. 월 합계 57시간 상한. (하루 4시간 상한은 폐지됨)
	public static Result Month(int y, int m, int baseSalary, int rate, bool dayCap4) { return Month(y, m, baseSalary, rate, dayCap4, 0); }
	public static Result Month(int y, int m, int baseSalary, int rate, bool dayCap4, int fixedUnit)
	{
		Result r = new Result(); r.Unit = fixedUnit > 0 ? fixedUnit : Math.Round(HourlyUnit(baseSalary, rate), MidpointRounding.AwayFromZero);
		foreach (IGrouping<DateTime, Rec> g in Records.All().Where(x => x.Date.Year == y && x.Date.Month == m).GroupBy(x => x.Date))
		{
			int c = g.Count(); r.Days++; r.RawHours += c;
			int counted = Program.IsOffDayExt(g.Key) ? c : Math.Max(0, c - 1);
			
			r.Counted += counted;
		}
		if (r.Counted > 57) { r.Counted = 57; r.Capped = true; }
		r.Paid = r.Counted + 10;   // 실제 계산 시간에 기본 10시간분을 별도로 더해요
		r.Amount = (long)(r.Unit * r.Paid);
		return r;
	}
}

// ═════ 공문 처리기한 ═════
internal class Deadline
{
	public string Id, Title; public DateTime Date; public bool Done, HasTime; public string Reminded = "";
	public string WhenText { get { return Date.Month + "월 " + Date.Day + "일(" + Records.Dow[(int)Date.DayOfWeek] + ")" + (HasTime ? " " + Date.ToString("HH:mm") : ""); } }
	public int Left { get { return (int)(Date.Date - DateTime.Today).TotalDays; } }
}

internal static class Deadlines
{
	private static List<Deadline> list;
	private static string PathFile { get { return Path.Combine(Program.DataDir, "deadlines.txt"); } }

	public static List<Deadline> All()
	{
		if (list != null) return list;
		list = new List<Deadline>();
		try
		{
			if (File.Exists(PathFile))
				foreach (string line in File.ReadAllLines(PathFile, Encoding.UTF8))
				{
					string[] p = line.Split(new char[] { '|' });
					DateTime d; if (p.Length < 4 || !DateTime.TryParseExact(p[2], new string[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
					list.Add(new Deadline { Id = p[0], Title = p[1], Date = d, HasTime = p[2].Length > 10, Done = p[3] == "1", Reminded = p.Length > 4 ? p[4] : "" });
				}
		}
		catch { }
		return list;
	}

	public static void Save()
	{
		try
		{
			Directory.CreateDirectory(Program.DataDir);
			AtomicIO.WriteLines(PathFile, All().Select(x => x.Id + "|" + x.Title.Replace("|", "/").Replace("\r", " ").Replace("\n", " ") + "|" + x.Date.ToString(x.HasTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd") + "|" + (x.Done ? "1" : "0") + "|" + x.Reminded).ToArray(), Encoding.UTF8);
		}
		catch { }
	}

	public static void Add(string title, DateTime date) { Add(title, date, false); }
	public static void Add(string title, DateTime date, bool hasTime)
	{
		All().Add(new Deadline { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Title = title.Trim(), Date = hasTime ? new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0) : date.Date, HasTime = hasTime });
		Save();
	}
	public static void SetDone(string id, bool done) { Deadline d = All().FirstOrDefault(x => x.Id == id); if (d != null) { d.Done = done; Save(); } }
	public static void Remove(string id) { All().RemoveAll(x => x.Id == id); Save(); }

	// 진행 중인 것 먼저(기한 임박순), 완료는 뒤로
	public static List<Deadline> Sorted() { return All().OrderBy(x => x.Done ? 1 : 0).ThenBy(x => x.Date).ToList(); }


	// 알릴 항목 문구 목록 (09시 이후 · 단계별 한 번씩: 3일 이내 → 1일 전 → 당일(오전·15시) → 지남)
	public static List<string> Due(DateTime now)
	{
		List<string> msgs = new List<string>();
		bool morning = now.Hour >= 9;
		bool changed = false;
		foreach (Deadline d in All())
		{
			if (d.Done) continue;
			int left = d.Left; string stage = null;
			if (d.HasTime && left == 0)
			{
				// 시각을 정한 기한: 1시간 전 · 30분 전 · 시각이 지났을 때 (시간대는 아침 9시 제한 없이)
				double mins = (d.Date - now).TotalMinutes; string ts = null; string tmsg = null;
				if (mins <= 0 && mins > -180) { ts = "tover"; tmsg = d.Title + " · " + d.Date.ToString("HH:mm") + " 마감 시각이 지났어요"; }
				else if (mins > 0 && mins <= 30) { ts = "t30"; tmsg = d.Title + " · " + (int)Math.Ceiling(mins) + "분 뒤 (" + d.Date.ToString("HH:mm") + ") 마감이에요"; }
				else if (mins > 30 && mins <= 60) { ts = "t60"; tmsg = d.Title + " · 1시간 안에 (" + d.Date.ToString("HH:mm") + ") 마감이에요"; }
				if (ts != null && !d.Reminded.Contains(ts + ",")) { d.Reminded += ts + ","; changed = true; msgs.Add(tmsg); }
			}
			if (!morning) continue;
			if (left < 0) stage = "over";
			else if (left == 0) stage = now.Hour >= 15 && d.Reminded.Contains("d0,") ? "d0b" : "d0";
			else if (left == 1) stage = "d1";
			else if (left <= 3) stage = "d3";
			if (stage == null || d.Reminded.Contains(stage + ",")) continue;
			d.Reminded += stage + ","; changed = true;
			string tag = left < 0 ? "기한이 " + (-left) + "일 지났어요" : left == 0 ? (d.HasTime ? "오늘 " + d.Date.ToString("HH:mm") + " 마감이에요" : "오늘 마감이에요") : "D-" + left + (d.HasTime ? " (" + d.Date.ToString("HH:mm") + ")" : "");
			msgs.Add(d.Title + " · " + tag);
		}
		if (changed) Save();
		return msgs;
	}
}

// ═════ 단축키 · 선택 글 변환 · 자동 붙여넣기 ═════
internal partial class Program
{
	internal static bool toolsOn = true, toolsPaste = true, payCap4;
	internal static int moneyFmt, dateFmt, payRate = 55, payBase;
	internal static string payGradeId = "g7";
	internal const int NHK = 11;   // 단축키 개수
	internal static int moneyUnitTo = 1, moneyUnitBare = 0; internal static bool sumAcc = true;   // 금액 단위 변환: 바꿀 단위 · 단위 없는 숫자의 원래 단위 / 셀 합계 누적
	internal static string[] hotStatus = new string[11] { "", "", "", "", "", "", "", "", "", "", "" };
	// 단축키: 보조키(1 Alt · 2 Ctrl · 4 Shift · 8 Win) + 가상키 코드. 기본 Alt+F1 · Alt+F2 · Alt+F3
	internal static uint[] hkMod = new uint[11] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
	// Alt+F1 금액 · F2 날짜 · F3 특수문자 · F5 법령 찾기 · F6 면적 변환 · F7 개인정보 가리기 · F8 항목부호 정리 (Alt+F4는 창 닫기라 건너뛰었어요)
	internal static int[] hkVk = new int[11] { 0x70, 0x71, 0x72, 0x76, 0x77, 0x78, 0x79, 0x74, 0x75, 0x56, 0x7B };
	internal static readonly int[] HkDefault = { 0x70, 0x71, 0x72, 0x76, 0x77, 0x78, 0x79, 0x74, 0x75, 0x56, 0x7B };
	private static MenuItem charItem;

	internal static string HotkeyName(uint mod, int vk)
	{
		StringBuilder sb = new StringBuilder();
		if ((mod & 2) != 0) sb.Append("Ctrl+"); if ((mod & 1) != 0) sb.Append("Alt+"); if ((mod & 4) != 0) sb.Append("Shift+"); if ((mod & 8) != 0) sb.Append("Win+");
		string k = ((Keys)vk).ToString();
		if (k.Length == 2 && k[0] == 'D' && char.IsDigit(k[1])) k = k.Substring(1);
		sb.Append(k); return sb.ToString();
	}
	internal static string HotkeyText(int i) { return HotkeyName(hkMod[i], hkVk[i]); }
	internal static string HkSave() { string[] p = new string[NHK]; for (int i = 0; i < NHK; i++) p[i] = hkMod[i] + ":" + hkVk[i]; return string.Join(",", p); }
	internal static void HkLoad(string s)
	{
		try
		{
			string[] p = s.Split(new char[] { ',' }); if (p.Length != 3 && p.Length != 7 && p.Length != 9 && p.Length != 10 && p.Length != NHK) return;
			uint[] m = (uint[])hkMod.Clone(); int[] v = (int[])hkVk.Clone();
			for (int i = 0; i < p.Length; i++) { string[] q = p[i].Split(new char[] { ':' }); m[i] = uint.Parse(q[0]); v[i] = int.Parse(q[1]); if (v[i] <= 0 || v[i] > 255 || m[i] > 15) return; }
			if (p.Length < NHK) for (int i = p.Length; i < NHK; i++) { m[i] = hkMod[i]; v[i] = HkDefault[i]; }
			if (p.Length == 7) for (int i = 3; i < 7; i++) v[i] = HkDefault[i];   // 예전 기본 키 배치(F5~F8)는 새 배치로 옮겨요
			else if (p.Length >= 9 && v[3] == 0x74 && v[4] == 0x75 && v[5] == 0x76 && v[6] == 0x77 && v[7] == 0x78 && v[8] == 0x7B) for (int i = 3; i < NHK; i++) v[i] = HkDefault[i];
			hkMod = m; hkVk = v;
		}
		catch { }
	}

	internal static bool IsOffDayExt(DateTime d) { return IsOffDay(d); }

	private static string payHintKey, payHintVal = "";
	// 홈 카드에 붙이는 한 줄: 설정된 직급이 있으면 예상 수당, 없으면 안내 문구
	internal static string PayHint()
	{
		DateTime n = DateTime.Now;
		string key = Records.CountAll() + "|" + n.ToString("yyyyMMdd") + "|" + payGradeId + "|" + payBase + "|" + payRate + "|" + payCap4;
		if (key == payHintKey) return payHintVal;
		payHintKey = key;
		PayGrade g = Pay.Find(payGradeId); int b = g.Id == "xx" ? payBase : g.Base, r = g.Id == "xx" ? payRate : g.Rate;
		Pay.Result pr = (b > 0 || g.Fixed > 0) ? Pay.Month(n.Year, n.Month, b, r, payCap4, g.Fixed) : null;
		payHintVal = pr != null && pr.Amount > 0 ? "  ·  예상 수당 약 " + pr.Amount.ToString("N0") + "원" : "  ·  눌러서 기록 보기";
		return payHintVal;
	}

	[DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
	[DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h, int id);
	[DllImport("user32.dll")] private static extern uint SendInput(uint n, INPUT[] inputs, int size);
	[DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
	[DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
	[DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr h);
	[DllImport("user32.dll")] private static extern bool CloseClipboard();
	[DllImport("user32.dll")] private static extern bool EmptyClipboard();
	[DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint fmt);
	[DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint fmt, IntPtr h);
	[DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
	[DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr h);
	[DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr h);

	[StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
	[StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
	[StructLayout(LayoutKind.Explicit)] private struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
	[StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public INPUTUNION u; }



	private class HotWin : NativeWindow
	{
		public Action<int> OnHot;
		public HotWin() { CreateParams cp = new CreateParams(); cp.Parent = (IntPtr)(-3); CreateHandle(cp); }
		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 0x0312 && OnHot != null) OnHot(m.WParam.ToInt32());
			base.WndProc(ref m);
		}
	}
	private static HotWin hotWin;
	private static SynchronizationContext uiCtx;
	private static int toolBusy;
	private static CharPad charPad;

	// 설정에 맞게 단축키 등록/해제
	internal static void ApplyHotkeys()
	{
		try
		{
			if (uiCtx == null) uiCtx = SynchronizationContext.Current;
			if (hotWin == null) { hotWin = new HotWin(); hotWin.OnHot = delegate(int id) { HotPressed(id); }; }
			for (int i = 1; i <= NHK; i++)
			{
				UnregisterHotKey(hotWin.Handle, i);
				hotStatus[i - 1] = "";
				if (!toolsOn) continue;
				bool ok = RegisterHotKey(hotWin.Handle, i, hkMod[i - 1] | 0x4000, (uint)hkVk[i - 1]);
				hotStatus[i - 1] = ok ? "사용 중" : "다른 프로그램이 쓰고 있어요";
			}
			if (charItem != null) charItem.Text = "특수문자 입력 (" + HotkeyText(2) + ")";
		}
		catch { }
	}

	private static void HotPressed(int id)
	{
		if (!toolsOn) return;
		// Alt 키만 떼면 앱의 메뉴가 활성화되는 걸 막기 위해, 아직 눌려 있을 때 의미 없는 키를 한 번 끼워 넣음
		if ((GetAsyncKeyState(0x12) & 0x8000) != 0 || (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0)
		{
			List<INPUT> l = new List<INPUT>(); Key(l, 0xE8, false); Key(l, 0xE8, true); SendInput((uint)l.Count, l.ToArray(), Marshal.SizeOf(typeof(INPUT)));
		}
		if (id == 3) { ToggleCharPad(); return; }
		if (id == 10) { ShotOverlay.Start(); return; }
		if (id == 11) { PaletteForm.Toggle(); return; }
		if (id > 3) { if (Interlocked.Exchange(ref toolBusy, 1) == 1) return; int idd = id; Thread t2 = new Thread(delegate() { try { AdminTool(idd); } catch { } finally { Interlocked.Exchange(ref toolBusy, 0); } }); t2.IsBackground = true; t2.Start(); return; }
		if (Interlocked.Exchange(ref toolBusy, 1) == 1) return;
		Thread t = new Thread(delegate() { try { ConvertSelection(id); } catch { } finally { Interlocked.Exchange(ref toolBusy, 0); } });
		t.IsBackground = true; t.Start();
	}

	internal static void ToggleCharPad()
	{
		try
		{
			if (charPad != null && !charPad.IsDisposed && charPad.Visible) { charPad.Close(); charPad = null; return; }
			charPad = new CharPad(); charPad.Show();
		}
		catch { }
	}

	private static void ToolNote(string title, string text, int ms = 3000)
	{
		try { if (uiCtx != null) uiCtx.Post(delegate { Balloon(title, text, ms); }, null); } catch { }
	}

	private static void Key(List<INPUT> l, ushort vk, bool up, bool ext = false)
	{
		INPUT i = new INPUT(); i.type = 1; i.u.ki.wVk = vk; i.u.ki.dwFlags = (up ? 2u : 0u) | (ext ? 1u : 0u); l.Add(i);
	}
	private static void Chord(ushort mod, ushort key, bool ext = false)
	{
		List<INPUT> l = new List<INPUT>();
		Key(l, mod, false); Key(l, key, false, ext); Key(l, key, true, ext); Key(l, mod, true);
		SendInput((uint)l.Count, l.ToArray(), Marshal.SizeOf(typeof(INPUT)));
	}

	// 아직 눌려 있는 보조키를 가상으로 떼어 줌 (사용자가 키를 떼길 기다리지 않아도 바로 복사 가능)
	private static void ReleaseMods()
	{
		List<INPUT> l = new List<INPUT>();
		foreach (int vk in new int[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
			if ((GetAsyncKeyState(vk) & 0x8000) != 0) Key(l, (ushort)vk, true, vk == 0xA3 || vk == 0xA5);
		if (l.Count > 0) SendInput((uint)l.Count, l.ToArray(), Marshal.SizeOf(typeof(INPUT)));
	}

	// ── 클립보드 (Win32 직접 사용: 빠르고 가볍게) ──
	private static string ReadClip()
	{
		for (int i = 0; i < 8; i++)
		{
			if (OpenClipboard(hotWin != null ? hotWin.Handle : IntPtr.Zero))
			{
				try
				{
					IntPtr h = GetClipboardData(13);
					if (h == IntPtr.Zero) return null;
					IntPtr p = GlobalLock(h); if (p == IntPtr.Zero) return null;
					try { return Marshal.PtrToStringUni(p); } finally { GlobalUnlock(h); }
				}
				finally { CloseClipboard(); }
			}
			Thread.Sleep(10);
		}
		return null;
	}

	private static bool WriteClip(string s)
	{
		for (int i = 0; i < 8; i++)
		{
			if (OpenClipboard(hotWin != null ? hotWin.Handle : IntPtr.Zero))
			{
				try
				{
					EmptyClipboard();
					byte[] b = Encoding.Unicode.GetBytes(s + "\0");
					IntPtr h = GlobalAlloc(0x0002, (UIntPtr)b.Length); if (h == IntPtr.Zero) return false;
					IntPtr p = GlobalLock(h); Marshal.Copy(b, 0, p, b.Length); GlobalUnlock(h);
					return SetClipboardData(13, h) != IntPtr.Zero;
				}
				finally { CloseClipboard(); }
			}
			Thread.Sleep(10);
		}
		return false;
	}

	// 선택한 글을 복사해서 읽어옴 (선택이 없으면 null)
	private static string CopySelection()
	{
		ReleaseMods();
		uint seq = GetClipboardSequenceNumber();
		Chord(0x11, 0x43);
		for (int i = 0; i < 40; i++)
		{
			Thread.Sleep(8);
			if (GetClipboardSequenceNumber() != seq) { Thread.Sleep(4); return ReadClip(); }
		}
		Chord(0x11, 0x2D, true);   // Ctrl+Insert 로 한 번 더 (Ctrl+C가 먹지 않는 프로그램용)
		for (int i = 0; i < 25; i++)
		{
			Thread.Sleep(8);
			if (GetClipboardSequenceNumber() != seq) { Thread.Sleep(4); return ReadClip(); }
		}
		return null;
	}

	private static void PasteText(string text, string restore)
	{
		if (!WriteClip(text)) return;
		if (toolsPaste)
		{
			Chord(0x11, 0x56);
			Thread.Sleep(180);   // 붙여넣는 앱이 읽어갈 시간
			if (restore != null) WriteClip(restore);
		}
		else ToolNote("복사했어요", "Ctrl+V로 붙여넣으세요");
	}

	// 선택한 글에 변환 적용 (1: 금액 · 2: 날짜 서식)
	private static void ConvertSelection(int id)
	{
		string saved = ReadClip();
		string sel = CopySelection();
		if (id == 2)
		{
			string res = sel == null ? KoText.FormatDate(DateTime.Today, dateFmt) : KoText.ConvertDates(sel, dateFmt, DateTime.Today);
			if (res == null) { if (saved != null) WriteClip(saved); ToolNote("날짜를 찾지 못했어요", "2026-10-13, 2026.10.13, 10/13 같은 날짜를 선택해 주세요"); return; }
			PasteText(res, saved); return;
		}
		if (sel == null) { if (saved != null) WriteClip(saved); ToolNote("선택한 글이 없어요", "바꿀 금액을 드래그로 선택한 뒤 눌러 주세요"); return; }
		string outText = KoText.HasKoMoney(sel) ? KoText.KoToMoney(sel) : (KoText.HasNumber(sel) ? KoText.MoneyToKo(sel, moneyFmt) : null);
		if (outText == null || outText == sel) { if (saved != null) WriteClip(saved); ToolNote("바꿀 금액이 없어요", "숫자 또는 한글 금액(예: 일백만원)을 선택해 주세요"); return; }
		PasteText(outText, saved);
	}

	// 4 법령 바로 찾기 · 5 면적 변환 · 6 개인정보 가리기 · 7 항목부호 정리 (선택한 글에 적용)
	private static void AdminTool(int id)
	{
		string saved = ReadClip(); string sel = CopySelection();
		if (sel == null) { if (saved != null) WriteClip(saved); ToolNote("선택한 글이 없어요", id == 4 ? "법령 이름(예: 도로교통법 제5조)을 드래그로 선택한 뒤 눌러 주세요" : "바꿀 글을 드래그로 선택한 뒤 눌러 주세요"); return; }
		if (id == 4)
		{
			if (saved != null) WriteClip(saved);
			System.Collections.Generic.List<string> urls = AdminText.LawUrls(sel);
			string cs = null; if (urls.Count == 0) { urls = AdminText.CaseLaws(sel, out cs); }
			if (urls.Count == 0) { ToolNote("법령 이름을 찾지 못했어요", "사례(예: 불법주정차 민원)나 법령 이름·조문(예: 도로교통법 제5조)을 선택해 주세요"); return; }
			if (cs != null && cs.Length > 0) ToolNote(cs.Contains("검색어:") ? "법령 검색" : "관련 법령 (추정)", cs + (cs.Contains("검색어:") ? "" : "\n※ 사례 글자로 추정한 것이니 조문 내용은 꼭 확인하세요"), 9000);
			int opened = 0;
			foreach (string url in urls) { if (opened >= 4) break; try { System.Diagnostics.Process.Start(url); opened++; System.Threading.Thread.Sleep(300); } catch { ToolNote("브라우저를 열지 못했어요", url); return; } }
			if (urls.Count > 4) ToolNote("조문 " + urls.Count + "개 중 4개만 열었어요", "나머지는 나눠서 선택해 주세요");
			return;
		}
		if (id == 9)
		{
			if (saved != null) WriteClip(saved);
			string info = AdminText.CellSum(sel, sumAcc, this_ => WriteClip(this_));
			if (info == null) { ToolNote("더할 숫자가 없어요", "숫자가 든 칸을 드래그로 선택한 뒤 눌러 주세요 (한글·엑셀 표 모두 돼요)"); return; }
			ToolNote("합계", info, 5000); return;
		}
		if (id == 8)
		{
			string cv = AdminText.UnitConv(sel, moneyUnitTo, moneyUnitBare);
			if (cv == null) { if (saved != null) WriteClip(saved); ToolNote("바꿀 금액이 없어요", "원 · 천원 · 만원 · 백만원 · 억원이 붙은 금액을 선택해 주세요 (단위 없는 숫자만 선택해도 돼요)"); return; }
			PasteText(cv, saved); return;
		}
		string res = id == 5 ? AdminText.Area(sel) : (id == 6 ? AdminText.Mask(sel) : AdminText.Items(sel));
		if (res == null) { if (saved != null) WriteClip(saved); ToolNote(id == 5 ? "바꿀 면적이 없어요" : (id == 6 ? "가릴 개인정보가 없어요" : "정리할 항목이 없어요"), id == 5 ? "숫자(㎡ 또는 평)를 선택해 주세요" : (id == 6 ? "주민번호·전화번호·계좌번호·이메일을 선택해 주세요" : "1. 가. 1) 같은 항목부호가 있는 글이나 들여쓴 글을 선택해 주세요")); return; }
		PasteText(res, saved);
	}

	// 특수문자 팔레트에서 호출: 포커스를 빼앗지 않으므로 바로 붙여넣기
	internal static void InsertChar(string ch)
	{
		if (Interlocked.Exchange(ref toolBusy, 1) == 1) return;
		Thread t = new Thread(delegate() { try { PasteText(ch, ReadClip()); } catch { } finally { Interlocked.Exchange(ref toolBusy, 0); } });
		t.IsBackground = true; t.Start();
	}

	// 1분마다: 공문 처리기한 알림
	private static int lastDeadlineMinute = -1;
	private static void DeadlineTick(DateTime now)
	{
		if (now.Minute == lastDeadlineMinute) return;
		lastDeadlineMinute = now.Minute;
		List<string> due = Deadlines.Due(now);
		if (due.Count == 0) return;
		string body = string.Join("\n", due.Take(4).ToArray()) + (due.Count > 4 ? "\n외 " + (due.Count - 4) + "건" : "");
		Balloon("공문 처리기한", body, 8000);
		PhonePush("\U0001F4CC 공문 처리기한", "\U0001F409 마룡이가 알려드려요\n" + body);
		if (soundOn) { try { System.Media.SystemSounds.Asterisk.Play(); } catch { } }
	}
}

// ═════ 행정 글 도우미: 면적 변환 · 개인정보 가리기 · 항목부호 정리 · 법령 주소 ═════
internal static class AdminText
{
	internal static readonly string[] Units = { "원", "천원", "만원", "백만원", "억원" };
	private static readonly double[] UnitF = { 1, 1e3, 1e4, 1e6, 1e8 };
	private static string FmtN(double v)
	{
		v = Math.Round(v, 6); if (Math.Abs(v - Math.Round(v)) < 1e-9) return Math.Round(v).ToString("N0", CultureInfo.InvariantCulture);
		return v.ToString("#,##0.######", CultureInfo.InvariantCulture);
	}
	// 금액 단위 바꾸기: "1,000,000원" → "1,000천원" · "1백만원" → "1,000천원" · "1,000천원" → "1,000,000원". 단위 없는 숫자만 있으면 bare 단위로 보고 숫자만 바꿔요
	public static string UnitConv(string s, int to, int bare)
	{
		if (s == null || to < 0 || to > 4) return null;
		Regex rx = new Regex(@"(\d[\d,]*(?:\.\d+)?)\s*(억원|백만원|천원|만원|원)");
		if (rx.IsMatch(s))
		{
			bool changed = false;
			string r = rx.Replace(s, delegate(Match m)
			{
				double n; if (!double.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return m.Value;
				int u = Array.IndexOf(Units, m.Groups[2].Value); if (u < 0 || u == to) return m.Value; changed = true;
				return FmtN(n * UnitF[u] / UnitF[to]) + Units[to];
			});
			return changed ? r : null;
		}
		if (bare == to) return null;
		Regex nb = new Regex(@"\d[\d,]*(?:\.\d+)?"); if (!nb.IsMatch(s)) return null;
		if (Regex.IsMatch(s, @"[가-힣A-Za-z]{2,}")) return null;   // 글이 섞여 있으면 (날짜·번호일 수 있어) 건드리지 않아요
		if (Regex.IsMatch(s, @"\d+\.\d+\.\d+|\d+[-/]\d+[-/]\d+|\d{1,2}:\d{2}")) return null;   // 날짜·시각은 금액이 아니에요
		bool ch2 = false;
		string r2 = nb.Replace(s, delegate(Match m)
		{
			double n; if (!double.TryParse(m.Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return m.Value; ch2 = true;
			return FmtN(n * UnitF[bare] / UnitF[to]);
		});
		return ch2 ? r2 : null;
	}
	private static double accSum; private static int accCnt, accRuns; private static DateTime accAt = DateTime.MinValue;
	// 선택한 칸의 숫자를 모두 더해요. 한글·엑셀 표를 복사하면 칸이 탭·줄바꿈으로 나뉘어 와요. 떨어진 칸은 차례로 선택해 누를 때마다 누적돼요 (2분 쉬면 새로 시작)
	public static string CellSum(string s, bool acc, Action<string> setClip)
	{
		if (s == null) return null; List<double> nums = new List<double>();
		bool tabular = s.IndexOf('\t') >= 0 || s.IndexOf('\n') >= 0;
		if (tabular)
		{
			foreach (string cell in Regex.Split(s, @"[\t\r\n]+"))
			{
				string c = cell.Trim(); if (c.Length == 0) continue;
				Match m = Regex.Match(c, @"^([△▲\-−(]?)\s*(\d[\d,]*(?:\.\d+)?)\s*\)?\s*(?:원|천원|만원|백만원|억원|명|건|개|%|㎡|평)?$"); if (!m.Success) continue;
				double v; if (!double.TryParse(m.Groups[2].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
				string sg = m.Groups[1].Value; if (sg.Length > 0) v = -v; nums.Add(v);
			}
		}
		else foreach (Match m in Regex.Matches(s, @"([△▲\-−]?)(\d[\d,]*(?:\.\d+)?)")) { double v; if (double.TryParse(m.Groups[2].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) nums.Add(m.Groups[1].Value.Length > 0 ? -v : v); }
		if (nums.Count == 0) return null;
		double sum = 0; foreach (double v in nums) sum += v;
		string ss = FmtN(sum), msg = nums.Count + "개 · 합계 " + ss + " · 평균 " + FmtN(Math.Round(sum / nums.Count, 2));
		if (acc)
		{
			if ((DateTime.Now - accAt).TotalSeconds > 120) { accSum = 0; accCnt = 0; accRuns = 0; }
			accSum += sum; accCnt += nums.Count; accRuns++; accAt = DateTime.Now; string sa = FmtN(accSum);
			if (accRuns > 1) { msg += "\n누적 " + accRuns + "번 · " + accCnt + "개 · 합계 " + sa; ss = sa; }
		}
		setClip(ss); return msg + "\n(합계 " + ss + " 를 복사했어요 · Ctrl+V)";
	}
	private const double Pyeong = 3.305785;   // 1평 = 3.305785㎡
	private static string Num(double v, int dec) { string s = v.ToString("0." + new string('#', dec), CultureInfo.InvariantCulture); return s; }

	// ㎡ ↔ 평: "123.4㎡" → "123.4㎡(약 37.32평)" · "30평" → "30평(약 99.17㎡)". 단위 없는 숫자는 ㎡로 봐요
	public static string Area(string s)
	{
		Regex r = new Regex(@"(\d[\d,]*(?:\.\d+)?)\s*(㎡|m²|m2|제곱미터|평방미터|평)?(?:\s*\((?:약\s*)?[\d,.]+\s*(?:㎡|평)\))?", RegexOptions.IgnoreCase);
		bool any = false; bool anyUnit = Regex.IsMatch(s, @"\d\s*(㎡|m²|m2|제곱미터|평방미터|평)", RegexOptions.IgnoreCase);
		if (!anyUnit && Regex.IsMatch(s, @"\d+\.\d+\.\d+|\d+[-/]\d+[-/]\d+")) return null;
		string o = r.Replace(s, delegate(Match m)
		{
			double v; if (!double.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return m.Value;
			string u = m.Groups[2].Value; bool py = u == "평";
			if (!m.Groups[2].Success && (m.Groups[1].Value.Length < 1 || anyUnit)) return m.Value;
			any = true;
			return py ? Num(v, 4) + "평(약 " + Num(v * Pyeong, 2) + "㎡)" : Num(v, 4) + "㎡(약 " + Num(v / Pyeong, 2) + "평)";
		});
		return any ? o : null;
	}

	// 개인정보 가리기: 주민·외국인등록번호, 전화번호, 계좌·카드번호, 이메일
	public static string Mask(string s)
	{
		string o = s;
		o = Regex.Replace(o, @"\b(\d{6})\s*-\s*([1-8])\d{6}\b", "$1-$2******");
		o = Regex.Replace(o, @"\b(01[016789]|02|0[3-6][1-5])[-. ]?(\d{3,4})[-. ]?(\d{4})\b", delegate(Match m) { string mid = new string('*', m.Groups[2].Value.Length); return m.Groups[1].Value + "-" + mid + "-" + m.Groups[3].Value; });
		o = Regex.Replace(o, @"\b(\d{4})[- ](\d{4})[- ](\d{4})[- ](\d{4})\b", "$1-****-****-$4");
		o = Regex.Replace(o, @"\b(\d{2,6})-(\d{2,6})-(\d{2,8})(?:-(\d{1,4}))?\b", delegate(Match m) { if (m.Value.Contains("*")) return m.Value; int tot = m.Value.Replace("-", "").Length; if (tot < 10) return m.Value; return m.Groups[1].Value + "-" + new string('*', m.Groups[2].Value.Length) + "-" + new string('*', Math.Max(0, m.Groups[3].Value.Length - 2)) + m.Groups[3].Value.Substring(Math.Max(0, m.Groups[3].Value.Length - 2)) + (m.Groups[4].Success ? "-" + m.Groups[4].Value : ""); });
		o = Regex.Replace(o, @"([A-Za-z0-9._%+-])([A-Za-z0-9._%+-]*)@([A-Za-z0-9.-]+\.[A-Za-z]{2,})", delegate(Match m) { return m.Groups[1].Value + new string('*', Math.Min(6, m.Groups[2].Value.Length + 1)) + "@" + m.Groups[3].Value; });
		return o == s ? null : o;
	}

	// ── 항목부호: 1. → 가. → 1) → 가) → (1) → (가) → ① → ㉮ ──
	private static readonly string Ga = "가나다라마바사아자차카타파하거너더러머버서어저처커터퍼허";
	private static readonly string Circ = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳";
	private static readonly string CircGa = "㉮㉯㉰㉱㉲㉳㉴㉵㉶㉷㉸㉹㉺㉻";
	private static string Mark(int lv, int n)
	{
		switch (lv)
		{
			case 0: return n + ".";
			case 1: return Ga[(n - 1) % Ga.Length] + ".";
			case 2: return n + ")";
			case 3: return Ga[(n - 1) % Ga.Length] + ")";
			case 4: return "(" + n + ")";
			case 5: return "(" + Ga[(n - 1) % Ga.Length] + ")";
			case 6: return n <= Circ.Length ? Circ[n - 1].ToString() : "(" + n + ")";
			default: return CircGa[(n - 1) % CircGa.Length].ToString();
		}
	}
	private static readonly Regex MarkRx = new Regex(@"^(\s*)(?:(\d+)\.|([가-힣])\.|(\d+)\)|([가-힣])\)|\((\d+)\)|\(([가-힣])\)|([①-⑳])|([㉮-㉻]))\s*(.*)$");
	private static int LevelOf(Match m)
	{
		if (m.Groups[2].Success) return 0; if (m.Groups[3].Success) return 1; if (m.Groups[4].Success) return 2; if (m.Groups[5].Success) return 3;
		if (m.Groups[6].Success) return 4; if (m.Groups[7].Success) return 5; if (m.Groups[8].Success) return 6; return 7;
	}
	public static string Items(string s)
	{
		string[] lines = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		int[] cnt = new int[8]; List<string> o = new List<string>(); int marked = 0;
		foreach (string ln in lines) if (MarkRx.IsMatch(ln) && !Regex.IsMatch(ln, @"^\s*\d+\.\d")) marked++;
		bool byIndent = marked == 0;
		int last = -1;
		foreach (string raw in lines)
		{
			string ln = raw.TrimEnd();
			if (ln.Trim().Length == 0) { o.Add(""); continue; }
			int lv; string body;
			Match m = MarkRx.Match(ln);
			if (!byIndent && m.Success && !Regex.IsMatch(ln, @"^\s*\d+\.\d"))
			{
				lv = LevelOf(m); body = m.Groups[10].Value.Trim();
				if (lv > last + 1 && last >= -1) lv = Math.Min(lv, last + 1);   // 단계를 건너뛰지 않게
			}
			else if (byIndent)
			{
				int tabs = 0, sp = 0; foreach (char c in ln) { if (c == '\t') tabs++; else if (c == ' ') sp++; else if (c == '　') tabs++; else break; }
				lv = Math.Min(7, tabs + sp / 2); body = ln.Trim(); if (lv > last + 1) lv = last + 1;
			}
			else { o.Add(new string(' ', Math.Max(0, (last + 1) * 2)) + ln.Trim()); continue; }   // 마디 없는 줄은 앞 항목 내용으로 이어 붙여요
			if (lv < 0) lv = 0;
			cnt[lv]++; for (int k = lv + 1; k < 8; k++) cnt[k] = 0; last = lv;
			o.Add(new string(' ', lv * 2) + Mark(lv, cnt[lv]) + " " + body);
		}
		string res = string.Join("\r\n", o.ToArray());
		return res == s ? null : res;
	}

	// 선택한 글에 나온 「법령 + 조문」을 모두 찾아 국가법령정보센터 주소로 만들어요. 예: "「도로교통법」 제5조 및 주차장법 제12조의2, 같은 법 제13조" → 3개
	public static System.Collections.Generic.List<string> LawUrls(string s)
	{
		System.Collections.Generic.List<string> res = new System.Collections.Generic.List<string>(); if (s == null) return res;
		string t = Regex.Replace(s.Trim(), @"\s+", " "); if (t.Length == 0) return res;
		foreach (string known in new string[] { "개인정보 보호법", "지방공무원 징계령", "지방공무원 복무규정", "국가공무원 복무규정", "공공기관의 운영에 관한 법률", "지방자치단체를 당사자로 하는 계약에 관한 법률", "국가를 당사자로 하는 계약에 관한 법률", "행정절차법 시행령" }) { if (t.IndexOf("「" + known) < 0) t = t.Replace(known, "「" + known + "」"); }
		Regex rx = new Regex(@"(?:[「『<\[]\s*([^」』>\]]+?)\s*[」』>\]]|(같은\s*법|동\s*법|이\s*법|(?:[가-힣A-Za-z0-9·]+\s+){0,6}[가-힣A-Za-z0-9·]*조례|[가-힣A-Za-z0-9·]{2,}(?:\s+(?:시행령|시행규칙))?))(?:\s*(?:에\s*따(?:라|른)|에서|상|의))?\s*(제\s*\d+\s*조(?:\s*의\s*\d+)?)");
		string last = null;
		foreach (Match m in rx.Matches(t))
		{
			string name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value; string art = Regex.Replace(m.Groups[3].Value, @"\s+", "");
			string nn = Regex.Replace(name, @"\s+", "");
			if (Regex.IsMatch(nn, @"^(같은법|동법|이법)$")) nn = last; else { nn = Regex.Replace(nn, @"(상|의|에서|에|이|가|은|는|을|를|과|와|및)$", ""); if (!Regex.IsMatch(nn, @"(법|령|규칙|조례|법률|규정|규약|헌법)$") || nn.Length < 2) nn = null; }
			if (nn == null) continue; last = nn;
			string u = LawBase(nn) + Uri.EscapeDataString(nn) + "/" + Uri.EscapeDataString(art); if (!res.Contains(u)) res.Add(u);
		}
		if (res.Count == 0 && Regex.IsMatch(t, @"^[「『<]?[가-힣A-Za-z0-9·\s]{2,40}(법|령|규칙|조례|법률)[」』>]?$")) { string one = LawUrl(s); if (one != null) res.Add(one); }
		return res;
	}

	// 사례 글 → 관련 법령 추정표. 확실한 조문만 적고, 확실하지 않으면 조문 없이 법령 이름만 둡니다. 형식: 키워드 정규식 ‖ 법령|조문,조문|메모 ; ...
	private static readonly string[] CaseTable = {
		@"불법\s*(주정차|주차|정차)|주정차|견인|주차위반‖도로교통법|제32조,제33조,제35조,제160조|정차·주차 금지, 주차위반 조치, 과태료;질서위반행위규제법|제16조,제17조,제20조|사전통지·부과·이의제기",
		@"어린이\s*보호구역|스쿨존|민식이‖도로교통법|제12조|어린이 보호구역;특정범죄 가중처벌 등에 관한 법률|제5조의13|어린이 보호구역 사고",
		@"방치\s*(차량|자동차)|자동차\s*방치‖자동차관리법||방치 자동차 처리",
		@"노점|적치물|보행\s*방해|도로\s*(무단)?\s*(적치|점용)|도로\s*점용‖도로법|제61조,제73조|도로 점용 허가;도로교통법|제68조|도로에서의 금지행위",
		@"현수막|전단|불법\s*광고|입간판|옥외광고‖옥외광고물 등의 관리와 사업진흥에 관한 법률|제3조,제10조|허가·신고, 위반 광고물 정비",
		@"정보\s*공개|정보공개‖공공기관의 정보공개에 관한 법률|제9조,제11조,제18조|비공개 대상, 결정, 이의신청",
		@"개인정보|주민등록번호|CCTV|씨씨티비‖개인정보 보호법|제15조,제17조,제29조|수집·이용, 제공, 안전조치",
		@"민원\s*(처리|접수|기한|반복)|악성\s*민원|반복\s*민원‖민원 처리에 관한 법률||민원 처리 일반",
		@"과태료|이의\s*제기|질서위반‖질서위반행위규제법|제16조,제17조,제20조|사전통지·부과·이의제기",
		@"행정\s*심판|심판\s*청구‖행정심판법|제27조|심판청구 기간",
		@"행정\s*소송|취소\s*소송‖행정소송법|제20조|제소기간",
		@"영업\s*정지|영업\s*허가|허가\s*취소|처분\s*(사전)?\s*통지|청문‖행정절차법|제21조,제23조|처분의 사전통지, 이유 제시",
		@"이행\s*강제금‖행정기본법|제31조|이행강제금",
		@"대집행|철거\s*명령|무단\s*(철거|증축)‖행정대집행법|제2조|대집행과 비용징수;행정기본법|제30조|행정상 강제",
		@"제재\s*처분|제척\s*기간‖행정기본법|제23조|제재처분 제척기간",
		@"위반\s*건축|불법\s*(건축|증축|용도\s*변경)|무허가\s*건축‖건축법|제79조,제80조,제11조|위반 건축물 조치, 이행강제금, 허가;행정대집행법|제2조|대집행",
		@"개발\s*행위‖국토의 계획 및 이용에 관한 법률|제56조|개발행위 허가",
		@"농지\s*전용|불법\s*농지|농지‖농지법|제34조|농지전용 허가",
		@"산지\s*전용|불법\s*벌채|산림\s*훼손‖산지관리법|제14조|산지전용 허가",
		@"하천\s*(점용|무단)|하천‖하천법|제33조|하천 점용 허가",
		@"음식점|식품\s*위생|영업\s*신고‖식품위생법|제37조,제75조|영업 허가·신고, 허가 취소 등",
		@"무단\s*투기|쓰레기|폐기물‖폐기물관리법|제8조|폐기물 투기 금지",
		@"금연|흡연‖국민건강증진법|제9조|금연구역",
		@"층간\s*소음|소음|진동‖공동주택관리법|제20조|층간소음;소음·진동관리법||소음·진동",
		@"반려|유기\s*동물|유기견|개\s*물림|동물\s*학대‖동물보호법||동물 보호·관리",
		@"공유\s*재산|시유지|군유지|도유지|변상금‖공유재산 및 물품 관리법|제20조,제81조|사용·수익 허가, 변상금",
		@"국유\s*재산|국유지‖국유재산법|제72조|변상금",
		@"보조금‖보조금 관리에 관한 법률|제30조,제31조|교부 결정 취소, 반환",
		@"계약|입찰|수의\s*계약‖지방자치단체를 당사자로 하는 계약에 관한 법률|제9조|계약의 방법;지방자치단체를 당사자로 하는 계약에 관한 법률 시행령|제25조|수의계약",
		@"징계|비위|품위\s*유지‖지방공무원법|제69조|징계 사유;국가공무원법|제78조|징계 사유",
		@"청탁|금품|김영란|접대‖부정청탁 및 금품등 수수의 금지에 관한 법률|제5조,제8조|부정청탁 금지, 금품등 수수 금지",
		@"국가\s*배상|도로\s*(파손|하자)|포트홀‖국가배상법|제2조,제5조|공무원 직무상 불법행위, 공공시설 하자",
		@"아동\s*학대|학대\s*의심‖아동복지법|제17조|금지행위;아동학대범죄의 처벌 등에 관한 특례법|제10조|신고의무",
		@"가정\s*폭력‖가정폭력방지 및 피해자보호 등에 관한 법률||신고·보호",
		@"기초\s*생활|수급자‖국민기초생활 보장법||수급권자 선정",
		@"감염병‖감염병의 예방 및 관리에 관한 법률||신고·관리",
		@"전입\s*신고|주민\s*등록‖주민등록법||주민등록 신고",
		@"지방세|취득세|재산세|체납\s*처분‖지방세법||지방세 부과;지방세기본법||이의신청 등",
		@"세외\s*수입|부과금‖지방행정제재·부과금의 징수 등에 관한 법률||징수",
		@"전월세|임대차|전세\s*보증금|보증금\s*반환‖주택임대차보호법|제3조,제3조의2|대항력, 보증금 회수",
		@"화재|소방‖화재의 예방 및 안전관리에 관한 법률||화재 예방;소방시설 설치 및 관리에 관한 법률||소방시설",
		@"기록물|문서\s*보존‖공공기록물 관리에 관한 법률||기록물 관리",
		@"시간외|초과\s*근무|야근|수당‖지방공무원 수당 등에 관한 규정||시간외근무수당",
		@"교통\s*통제|행사\s*(교통|도로)|도로\s*통행\s*제한‖도로교통법|제6조|통행의 금지 및 제한",
		@"출생\s*신고|사망\s*신고|혼인\s*신고|이혼\s*신고|가족\s*관계|등록\s*기준지‖가족관계의 등록 등에 관한 법률||신고·증명서",
		@"인감‖인감증명법||인감 신고·증명",
		@"사유지\s*통행|통행권|길\s*막|맹지‖민법|제219조|주위토지통행권",
		@"담장|경계|경계\s*침범|토지\s*경계‖민법|제237조|경계표·담의 설치",
		@"상속|상속\s*포기|상속\s*재산‖민법|제1000조|상속 순위",
		@"주차장|거주자\s*우선|주차\s*공간|부설\s*주차‖주차장법||주차장 설치·관리",
		@"휴직|육아\s*휴직|질병\s*휴직‖지방공무원법|제63조|휴직",
		@"공익\s*신고|내부\s*고발‖공익신고자 보호법||신고자 보호",
		@"자체\s*감사|감사\s*(결과|처분)‖공공감사에 관한 법률||자체감사",
		@"용도\s*지역|도시\s*계획|지구\s*단위‖국토의 계획 및 이용에 관한 법률|제36조|용도지역 지정",
		@"재난|재해|풍수해|호우|태풍‖재난 및 안전관리 기본법||재난 대응;자연재해대책법||자연재해 대책",
		@"악취|대기\s*오염|매연|미세\s*먼지‖악취방지법||악취;대기환경보전법||대기오염",
		@"하수|정화조|오수‖하수도법||하수·정화조",
		@"상수도|단수|수도\s*요금‖수도법||수도",
		@"부동산\s*거래|실거래|거래\s*신고‖부동산 거래신고 등에 관한 법률|제3조|부동산 거래 신고",
		@"공시\s*지가|개별\s*공시|토지\s*가격‖부동산 가격공시에 관한 법률||공시지가",
		@"지적|토지\s*분할|분할\s*측량|지목‖공간정보의 구축 및 관리 등에 관한 법률||지적·측량",
		@"직장\s*내\s*괴롭힘|성희롱|갑질‖양성평등기본법||성희롱 방지",
		@"휴가|연가|복무‖지방공무원 복무규정||휴가·복무"
	};
	private static readonly string LawIdx = "헌법|대한민국헌법|민법|상법|형법|민사소송법|형사소송법|행정기본법|행정절차법|행정심판법|행정소송법|행정대집행법|행정조사기본법|행정규제기본법|국가배상법|질서위반행위규제법|국가를 당사자로 하는 계약에 관한 법률|지방자치단체를 당사자로 하는 계약에 관한 법률|국가재정법|지방재정법|지방자치법|지방공무원법|국가공무원법|공무원연금법|지방공무원 수당 등에 관한 규정|지방공무원 복무규정|국가공무원 복무규정|공직자윤리법|부정청탁 및 금품등 수수의 금지에 관한 법률|공공기관의 정보공개에 관한 법률|개인정보 보호법|전자정부법|민원 처리에 관한 법률|공공기록물 관리에 관한 법률|공공감사에 관한 법률|감사원법|보조금 관리에 관한 법률|지방보조금 관리에 관한 법률|공유재산 및 물품 관리법|국유재산법|물품관리법|지방세법|지방세기본법|지방세징수법|지방세특례제한법|지방행정제재·부과금의 징수 등에 관한 법률|지방교부세법|공익신고자 보호법|부패방지 및 국민권익위원회의 설치와 운영에 관한 법률|지방자치분권 및 지역균형발전에 관한 특별법|주민등록법|가족관계의 등록 등에 관한 법률|인감증명법|도로명주소법|지방공기업법|공공기관의 운영에 관한 법률|국가기술자격법|전자서명법|전자문서 및 전자거래 기본법|도로교통법|도로법|자동차관리법|주차장법|교통약자의 이동편의 증진법|교통안전법|대중교통의 육성 및 이용촉진에 관한 법률|여객자동차 운수사업법|화물자동차 운수사업법|철도안전법|항공안전법|공항시설법|항만법|해운법|도로교통공단법|특정범죄 가중처벌 등에 관한 법률|교통사고처리 특례법|농어촌도로 정비법|사도법|자전거 이용 활성화에 관한 법률|물류정책기본법|건축법|국토의 계획 및 이용에 관한 법률|도시 및 주거환경정비법|도시재정비 촉진을 위한 특별법|도시개발법|주택법|공동주택관리법|주택임대차보호법|상가건물 임대차보호법|집합건물의 소유 및 관리에 관한 법률|건축물관리법|건축기본법|건설산업기본법|건설기술 진흥법|산업집적활성화 및 공장설립에 관한 법률|부동산 거래신고 등에 관한 법률|부동산 가격공시에 관한 법률|공간정보의 구축 및 관리 등에 관한 법률|공익사업을 위한 토지 등의 취득 및 보상에 관한 법률|개발이익 환수에 관한 법률|농지법|산지관리법|산림자원의 조성 및 관리에 관한 법률|하천법|소하천정비법|공유수면 관리 및 매립에 관한 법률|수도법|하수도법|물환경보전법|지하수법|댐건설 및 주변지역지원 등에 관한 법률|도시공원 및 녹지 등에 관한 법률|도시교통정비 촉진법|주택도시기금법|민간임대주택에 관한 특별법|빈집 및 소규모주택 정비에 관한 특례법|자연재해대책법|재난 및 안전관리 기본법|급경사지 재해예방에 관한 법률|시설물의 안전 및 유지관리에 관한 특별법|옥외광고물 등의 관리와 사업진흥에 관한 법률|경관법|농어촌정비법|개발제한구역의 지정 및 관리에 관한 특별조치법|택지개발촉진법|산업입지 및 개발에 관한 법률|관광진흥법|국토기본법|지역 개발 및 지원에 관한 법률|공공주택 특별법|문화유산의 보존 및 활용에 관한 법률|국가유산기본법|국가유산수리 등에 관한 법률|매장유산 보호 및 조사에 관한 법률|자연유산의 보존 및 활용에 관한 법률|무형유산의 보전 및 진흥에 관한 법률|문화예술진흥법|문화기본법|박물관 및 미술관 진흥법|도서관법|지역문화진흥법|전통사찰의 보존 및 지원에 관한 법률|한옥 등 건축자산의 진흥에 관한 법률|건축자산의 진흥에 관한 법률|고도 보존 및 육성에 관한 특별법|궁중유적보존에 관한 법률|문화산업진흥 기본법|콘텐츠산업 진흥법|전통문화의 계승 및 발전에 관한 법률|공연법|영화 및 비디오물의 진흥에 관한 법률|출판문화산업 진흥법|체육시설의 설치·이용에 관한 법률|국민체육진흥법|학교체육 진흥법|관광기본법|국제회의산업 육성에 관한 법률|독립기념관법|국가보훈 기본법|국가유공자 등 예우 및 지원에 관한 법률|환경정책기본법|대기환경보전법|소음·진동관리법|악취방지법|폐기물관리법|자원의 절약과 재활용촉진에 관한 법률|토양환경보전법|자연환경보전법|야생생물 보호 및 관리에 관한 법률|동물보호법|가축분뇨의 관리 및 이용에 관한 법률|가축전염병 예방법|축산법|수산업법|식품위생법|식품 등의 표시·광고에 관한 법률|건강기능식품에 관한 법률|공중위생관리법|감염병의 예방 및 관리에 관한 법률|의료법|약사법|마약류 관리에 관한 법률|국민건강증진법|학교보건법|학교급식법|화학물질관리법|화학물질의 등록 및 평가 등에 관한 법률|석면안전관리법|실내공기질 관리법|환경영향평가법|탄소중립기본법|전기안전관리법|전기사업법|도시가스사업법|액화석유가스의 안전관리 및 사업법|에너지이용 합리화법|신에너지 및 재생에너지 개발·이용·보급 촉진법|먹는물관리법|장사 등에 관한 법률|농수산물 품질관리법|농수산물 유통 및 가격안정에 관한 법률|양곡관리법|친환경농어업 육성 및 유기식품 등의 관리·지원에 관한 법률|산림보호법|산림문화·휴양에 관한 법률|수목원·정원의 조성 및 진흥에 관한 법률|소방기본법|화재의 예방 및 안전관리에 관한 법률|소방시설 설치 및 관리에 관한 법률|위험물안전관리법|다중이용업소의 안전관리에 관한 특별법|초고층 및 지하연계 복합건축물 재난관리에 관한 특별법|응급의료에 관한 법률|119구조·구급에 관한 법률|근로기준법|최저임금법|근로자퇴직급여 보장법|산업안전보건법|중대재해 처벌 등에 관한 법률|고용보험법|산업재해보상보험법|국민연금법|국민건강보험법|노인장기요양보험법|고용정책 기본법|남녀고용평등과 일·가정 양립 지원에 관한 법률|기간제 및 단시간근로자 보호 등에 관한 법률|파견근로자 보호 등에 관한 법률|노동조합 및 노동관계조정법|외국인근로자의 고용 등에 관한 법률|직업안정법|장애인고용촉진 및 직업재활법|공무원 노동조합 설립 및 운영 등에 관한 법률|근로복지기본법|임금채권보장법|사회보장기본법|사회복지사업법|국민기초생활 보장법|긴급복지지원법|기초연금법|장애인복지법|장애인차별금지 및 권리구제 등에 관한 법률|장애인·노인·임산부 등의 편의증진 보장에 관한 법률|노인복지법|아동복지법|영유아보육법|유아교육법|초·중등교육법|고등교육법|평생교육법|학원의 설립·운영 및 과외교습에 관한 법률|청소년기본법|청소년복지 지원법|청소년 보호법|아동학대범죄의 처벌 등에 관한 특례법|가정폭력방지 및 피해자보호 등에 관한 법률|가정폭력범죄의 처벌 등에 관한 특례법|성폭력방지 및 피해자보호 등에 관한 법률|성폭력범죄의 처벌 등에 관한 특례법|양성평등기본법|다문화가족지원법|한부모가족지원법|건강가정기본법|저출산·고령사회기본법|의료급여법|정신건강복지법|자살예방 및 생명존중문화 조성을 위한 법률|사회적기업 육성법|협동조합 기본법|자원봉사활동 기본법|재해구호법|치매관리법|보건의료기본법|지역보건법|농어업인 삶의 질 향상 및 농어촌지역 개발촉진에 관한 특별법|소비자기본법|전자상거래 등에서의 소비자보호에 관한 법률|할부거래에 관한 법률|방문판매 등에 관한 법률|약관의 규제에 관한 법률|독점규제 및 공정거래에 관한 법률|하도급거래 공정화에 관한 법률|대규모유통업에서의 거래 공정화에 관한 법률|가맹사업거래의 공정화에 관한 법률|대리점거래의 공정화에 관한 법률|표시·광고의 공정화에 관한 법률|중소기업기본법|소상공인기본법|소상공인 보호 및 지원에 관한 법률|전통시장 및 상점가 육성을 위한 특별법|유통산업발전법|부가가치세법|소득세법|법인세법|상속세 및 증여세법|국세기본법|국세징수법|조세특례제한법|지방세외수입금의 징수 등에 관한 법률|관세법|전자금융거래법|금융소비자 보호에 관한 법률|대부업 등의 등록 및 금융이용자 보호에 관한 법률|채권의 공정한 추심에 관한 법률|신용정보의 이용 및 보호에 관한 법률|여신전문금융업법|사행행위 등 규제 및 처벌 특례법|게임산업진흥에 관한 법률|정보통신망 이용촉진 및 정보보호 등에 관한 법률|전기통신사업법|방송법|저작권법|특허법|상표법|디자인보호법|부정경쟁방지 및 영업비밀보호에 관한 법률|지능정보화 기본법|데이터 기반행정 활성화에 관한 법률|위치정보의 보호 및 이용 등에 관한 법률|통신비밀보호법|전파법|우편법|언론중재 및 피해구제 등에 관한 법률|신문 등의 진흥에 관한 법률|인터넷주소자원에 관한 법률|인공지능 발전과 신뢰 기반 조성 등에 관한 기본법|경찰관 직무집행법|경범죄 처벌법|집회 및 시위에 관한 법률|총포·도검·화약류 등의 안전관리에 관한 법률|청원경찰법|민방위기본법|통합방위법|병역법|예비군법|군사기지 및 군사시설 보호법|출입국관리법|재한외국인 처우 기본법|국적법|난민법|여권법|공직선거법|정당법|주민투표법|주민소환에 관한 법률|보안관찰법|법원조직법|검찰청법|변호사법|법무사법|공증인법|민사집행법|채무자 회생 및 파산에 관한 법률|소액사건심판법|가사소송법|형의 집행 및 수용자의 처우에 관한 법률|소년법|성매매알선 등 행위의 처벌에 관한 법률|스토킹범죄의 처벌 등에 관한 법률|폭력행위 등 처벌에 관한 법률|특정경제범죄 가중처벌 등에 관한 법률|범죄수익은닉의 규제 및 처벌 등에 관한 법률|부정수표 단속법|실종아동등의 보호 및 지원에 관한 법률|범죄피해자 보호법";
	private const string Alias = "어린이집=영유아보육법;보육=영유아보육법;유치원=유아교육법;급식=학교급식법;드론=항공안전법;킥보드=도로교통법;전동휠=도로교통법;아파트=공동주택관리법;전세=주택임대차보호법;월세=주택임대차보호법;쓰레기=폐기물관리법;노인=노인복지법;장애인=장애인복지법;산재=산업재해보상보험법;체납=지방세징수법;취득세=지방세법;재산세=지방세법;저작=저작권법;학대=아동복지법;폭력=가정폭력방지 및 피해자보호 등에 관한 법률;공원=도시공원 및 녹지 등에 관한 법률;가로수=산림자원의 조성 및 관리에 관한 법률;간판=옥외광고물 등의 관리와 사업진흥에 관한 법률;묘지=장사 등에 관한 법률;납골=장사 등에 관한 법률;봉안=장사 등에 관한 법률;사찰=전통사찰의 보존 및 지원에 관한 법률;한옥=한옥 등 건축자산의 진흥에 관한 법률;문화재=문화유산의 보존 및 활용에 관한 법률;매장문화재=매장유산 보호 및 조사에 관한 법률;전통시장=전통시장 및 상점가 육성을 위한 특별법;소방=소방기본법";
	private const string GenericTerms = "|관리|보호|지원|운영|진흥|촉진|안전|특례|기본|사업|이용|설치|조성|활성화|육성|제정|개발|정비|보전|보존|활용|등에|대한|관한|법률|규정|규칙|시행|";
	// 글에서 뽑은 말이 이름에 들어간 법령을 목록에서 찾는다. 예: 「문화유산 보수정비 …」 → 이름에 '문화유산'이 들어간 법들
	public static System.Collections.Generic.List<string> LawsByName(string text, out string why)
	{
		why = ""; System.Collections.Generic.List<string> outl = new System.Collections.Generic.List<string>(); if (text == null) return outl;
		string[] kws = Keywords(text, 12).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); if (kws.Length == 0) return outl;
		System.Collections.Generic.Dictionary<string, float> tw = new System.Collections.Generic.Dictionary<string, float>();
		foreach (string k in kws)
		{
			if (GenericTerms.IndexOf("|" + k + "|") < 0 && k.Length >= 2) tw[k] = Math.Max(tw.ContainsKey(k) ? tw[k] : 0f, (float)Math.Pow(k.Length, 1.5));
			if (k.Length >= 4) for (int i = 0; i + 2 <= k.Length; i++) { string sub = k.Substring(i, 2); if (GenericTerms.IndexOf("|" + sub + "|") < 0 && !tw.ContainsKey(sub)) tw[sub] = 1.2f; }
		}
		System.Collections.Generic.List<KeyValuePair<float, string>> sc = new System.Collections.Generic.List<KeyValuePair<float, string>>(); System.Collections.Generic.Dictionary<string, string> hit = new System.Collections.Generic.Dictionary<string, string>();
		foreach (string law in LawIdx.Split('|'))
		{
			float v = 0f; string best = ""; float bw = 0f; foreach (KeyValuePair<string, float> kv in tw) if (law.IndexOf(kv.Key, StringComparison.Ordinal) >= 0) { v += kv.Value; if (kv.Value > bw) { bw = kv.Value; best = kv.Key; } }
			if (v >= 3f) { sc.Add(new KeyValuePair<float, string>(v, law)); hit[law] = best; }
		}
		// 법 이름에 안 들어 있지만 흔히 같이 쓰는 말 → 법령 (짧은 별칭표)
		foreach (string al in Alias.Split(';')) { string[] kv2 = al.Split('='); if (kv2.Length != 2 || text.IndexOf(kv2[0], StringComparison.Ordinal) < 0) continue; int ix = sc.FindIndex(delegate(KeyValuePair<float, string> e) { return e.Value == kv2[1]; }); if (ix >= 0) sc[ix] = new KeyValuePair<float, string>(sc[ix].Key + 6f, kv2[1]); else if (LawIdx.IndexOf("|" + kv2[1] + "|", StringComparison.Ordinal) >= 0 || LawIdx.StartsWith(kv2[1] + "|", StringComparison.Ordinal) || LawIdx.EndsWith("|" + kv2[1])) { sc.Add(new KeyValuePair<float, string>(6f, kv2[1])); hit[kv2[1]] = kv2[0]; } }
		sc.Sort(delegate(KeyValuePair<float, string> a, KeyValuePair<float, string> b) { return b.Key.CompareTo(a.Key); });
		if (sc.Count == 0) return outl; float top = sc[0].Key;
		foreach (KeyValuePair<float, string> kv in sc) { if (kv.Key < top * 0.45f || outl.Count >= 8) break; outl.Add(kv.Value); why += (why.Length > 0 ? "\n" : "") + "「" + kv.Value + "」 ('" + hit[kv.Value] + "' 관련)"; }
		return outl;
	}
	// 긁은 문장에서 검색에 쓸 핵심 명사만 뽑는다: 조사·어미를 떼고, 흔한 말은 버리고, 긴 말·반복되는 말을 먼저 고른다.
	private static readonly string Stop = "|관련|대한|대해|경우|문의|요청|확인|가능|여부|어떻게|어떤|있는|없는|하는|되는|해당|이후|이전|현재|다음|아래|위와|같은|통해|따라|위한|때문|내용|사항|방법|절차|처리|민원|질문|답변|안내|부탁|합니다|입니다|있습니다|없습니다|바랍니다|드립니다|문의드립니다|그리고|그러나|하지만|또는|또한|매우|너무|계속|자꾸|이런|그런|저희|제가|우리|본인|관계자|담당자|담당|사람|이용|발생|진행|신청|접수|조치|가요|인가요|하나요|되나요|할까요|궁금|";
	public static string Keywords(string s, int max)
	{
		if (s == null) return ""; string t = Regex.Replace(s, @"[^가-힣A-Za-z0-9\s]", " ");
		System.Collections.Generic.Dictionary<string, int> cnt = new System.Collections.Generic.Dictionary<string, int>(); System.Collections.Generic.List<string> order = new System.Collections.Generic.List<string>();
		foreach (string w0 in Regex.Split(t, @"\s+"))
		{
			string w = Regex.Replace(w0, @"(으로서|으로써|에서는|에게서|이라고|라고|입니다|합니다|했습니다|하였습니다|해주세요|해 주세요|하고자|하려고|하려면|하면서|하는데|했는데|인데|에서|에게|으로|부터|까지|처럼|보다|하여|하고|해서|하며|이며|이고|이나|이다|한다|된다|이|가|은|는|을|를|의|에|와|과|로|도|만|께)+$", ""); if (w.Length < 2) continue;
			if (Regex.IsMatch(w, @"(니다|었다|았다|되었|하였|했다|는지|은지|인지|할지|하지|해야|하게|도록|되어|하여|두어|놓아|들어|나서|시오|세요|어요|아요|까요|네요|군요|죠|싶은데|싶은|싶습니다|싶어|치우게|하게|되게)$|^(있|없|되|하|않|같|나오|들어|쌓아)")) continue;
			w = Regex.Replace(w, @"(하|되|시키)$", ""); if (w.Length < 2) continue;
			if (Stop.IndexOf("|" + w + "|") >= 0 || Regex.IsMatch(w, @"^[0-9]+$")) continue;
			if (!cnt.ContainsKey(w)) { cnt[w] = 0; order.Add(w); } cnt[w]++;
		}
		order.Sort(delegate(string x, string y) { int sx = cnt[x] * 3 + Math.Min(x.Length, 6), sy = cnt[y] * 3 + Math.Min(y.Length, 6); return sy != sx ? sy.CompareTo(sx) : order.IndexOf(x).CompareTo(order.IndexOf(y)); });
		if (order.Count > max) order.RemoveRange(max, order.Count - max); return string.Join(" ", order.ToArray());
	}
	// 사례 글에서 관련 법령을 추정한다. 법령마다 탭 하나만 열고(첫 조문), 나머지 조문은 설명으로 알려 준다. 못 찾으면 법령센터 검색으로 넘긴다.
	public static System.Collections.Generic.List<string> CaseLaws(string s, out string summary)
	{
		System.Collections.Generic.List<string> res = new System.Collections.Generic.List<string>(); System.Collections.Generic.List<string> sm = new System.Collections.Generic.List<string>(); summary = "";
		if (s == null) return res; string t = Regex.Replace(s.Trim(), @"\s+", " "); if (t.Length < 2) return res;
		System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();
		foreach (string row in CaseTable)
		{
			string[] kv = row.Split(new string[] { "‖" }, StringSplitOptions.None); if (!Regex.IsMatch(t, kv[0])) continue;
			foreach (string it in kv[1].Split(';'))
			{
				string[] p = it.Split('|'); if (p.Length < 3 || !seen.Add(p[0])) continue;
				string arts = p[1], first = arts.Length > 0 ? arts.Split(',')[0] : "";
				res.Add(LawBase(p[0]) + Uri.EscapeDataString(p[0]) + (first.Length > 0 ? "/" + Uri.EscapeDataString(first) : ""));
				sm.Add("「" + p[0] + "」" + (arts.Length > 0 ? " " + arts.Replace(",", "·") : "") + " (" + p[2] + ")");
			}
		}
		{   // 정해 둔 사례표에 없거나 모자라면, 글에서 뽑은 핵심어가 이름에 들어간 법령을 목록에서 찾아 채운다
			string why; System.Collections.Generic.List<string> byName = LawsByName(t, out why);
			if (byName.Count > 0) { foreach (string ln in byName) if (seen.Add(ln) && res.Count < 4) res.Add(LawBase(ln) + Uri.EscapeDataString(ln)); if (why.Length > 0) sm.Add((sm.Count > 0 ? "— 이름이 비슷한 법령 —\n" : "") + why); }
		}
		if (res.Count > 4) { res.RemoveRange(4, res.Count - 4); }
		if (res.Count == 0) { string q = Keywords(t, 3); if (q.Length == 0) q = t.Length > 30 ? t.Substring(0, 30) : t; res.Add("https://www.law.go.kr/lsSc.do?menuId=1&subMenuId=15&section=bdyText&query=" + Uri.EscapeDataString(q)); summary = "정해 둔 법령이 없어, 글에서 뽑은 핵심어로 법령센터 본문 검색을 열었어요\n검색어: " + q; }
		else summary = string.Join("\n", sm.ToArray());
		return res;
	}
	private static string LawBase(string name) { return name.EndsWith("조례") ? "https://www.law.go.kr/자치법규/" : "https://www.law.go.kr/법령/"; }

	// 선택한 글(법령 이름 + 조문)로 국가법령정보센터 주소를 만들어요. 예: "도로교통법 제5조" · "「민법」 제750조의2" · "익산시 행정규칙 조례"
	public static string LawUrl(string s)
	{
		string t = s.Trim().Replace("\r", " ").Replace("\n", " "); if (t.Length == 0) return null;
		Match q = Regex.Match(t, @"[「『<\[]\s*([^」』>\]]+?)\s*[」』>\]]"); string name, art = "";
		Match a = Regex.Match(t, @"제\s*(\d+)\s*조(?:\s*의\s*(\d+))?");
		if (q.Success) name = q.Groups[1].Value; else name = a.Success ? t.Substring(0, a.Index) : t;
		if (a.Success) art = "제" + a.Groups[1].Value + "조" + (a.Groups[2].Success ? "의" + a.Groups[2].Value : "");
		name = Regex.Replace(name, @"\s+", ""); name = Regex.Replace(name, @"(?:제\d+항.*|제\d+호.*|에따라.*|의$|상$)$", ""); if (name.Length < 2) return null;
		bool ord = name.EndsWith("조례") || name.EndsWith("규칙") && !name.EndsWith("시행규칙") || name.EndsWith("교육규칙") && false;
		string baseUrl = ord && name.EndsWith("조례") ? "https://www.law.go.kr/자치법규/" : "https://www.law.go.kr/법령/";
		return baseUrl + Uri.EscapeDataString(name) + (art.Length > 0 ? "/" + Uri.EscapeDataString(art) : "");
	}
}
