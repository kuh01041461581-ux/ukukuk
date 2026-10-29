using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

// 명절(설·추석)에는 마룡이가 한복을 입어요
internal static class Hanbok
{
	// 설날(음력 1/1)·추석(음력 8/15) 앞뒤 하루
	public static string Festival(DateTime d)
	{
		try
		{
			if (Environment.GetEnvironmentVariable("OT_HANBOK") == "1") return "설";
			KoreanLunisolarCalendar c = new KoreanLunisolarCalendar();
			for (int off = -1; off <= 1; off++)
			{
				DateTime x = d.Date.AddDays(off);
				int m = c.GetMonth(x), dd = c.GetDayOfMonth(x);
				int leap = c.GetLeapMonth(c.GetYear(x));
				if (leap > 0 && m >= leap) { if (m == leap) continue; m--; }   // 윤달은 건너뜀
				if (m == 1 && dd == 1) return "설";
				if (m == 8 && dd == 15) return "추석";
			}
			// 설 전날(음력 12월 마지막 날)
			DateTime nx = d.Date.AddDays(1);
			if (c.GetMonth(nx) == 1 && c.GetDayOfMonth(nx) == 1) return "설";
		}
		catch { }
		return null;
	}

	// 음력 m월 dd일 앞뒤 span일 안인가 (윤달은 건너뜀)
	public static bool LunarNear(DateTime d, int m, int dd, int span)
	{
		try
		{
			KoreanLunisolarCalendar c = new KoreanLunisolarCalendar();
			for (int off = -span; off <= span; off++)
			{
				DateTime x = d.Date.AddDays(off);
				int mm = c.GetMonth(x), day = c.GetDayOfMonth(x);
				int leap = c.GetLeapMonth(c.GetYear(x));
				if (leap > 0 && mm >= leap) { if (mm == leap) continue; mm--; }
				if (mm == m && day == dd) return true;
			}
		}
		catch { }
		return false;
	}
}
