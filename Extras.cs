using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// 놓침 방지 · 바로 기록 · 방해 금지
internal partial class Program
{
	internal static bool phoneOn; internal static string phoneTopic = "";
	internal static void PhonePush(string title, string msg)
	{
		if (phoneOn && !string.IsNullOrEmpty(phoneTopic)) Push.Send(phoneTopic, title, msg, 4, null);
	}
	private static Slot pending;                 // 알림이 울렸지만 아직 확인하지 않은 시간대
	private static bool pendingWarned, pendingSuppressed;
	private static bool dndManual, dndAuto = true;
	private static MenuItem dndItem;

	[DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);

	// 전체화면 앱 · 발표 중이면 true (2 busy, 3 D3D 전체화면, 4 발표 모드)
	internal static bool AutoBusy()
	{
		if (!dndAuto) return false;
		try { int s; if (SHQueryUserNotificationState(out s) == 0) return s == 2 || s == 3 || s == 4; }
		catch { }
		return false;
	}
	internal static bool DndActive { get { return dndManual || AutoBusy(); } }

	private static void Balloon(string title, string text, int ms)
	{
		try { tray.BalloonTipTitle = title; tray.BalloonTipText = text; tray.ShowBalloonTip(ms); } catch { }
	}

	private static bool PopupOpen() { return (alert != null && !alert.IsDisposed && !alert.Closing) || AlertToast.IsOpen; }

	// 1초마다: 마감 5분 전 마지막 경고, 방해 금지가 끝나면 밀린 알림, 마감 지나면 '놓침' 기록
	private static void PendingTick(DateTime now)
	{
		if (pending == null) return;
		if (Records.IsConfirmed(pending.Start)) { pending = null; return; }
		if (now >= pending.Deadline)
		{
			Records.AddMissed(pending.Start);
			pending = null;
			return;
		}
		bool dnd = DndActive;
		if (pendingSuppressed)
		{
			if (!dnd && !PopupOpen()) { pendingSuppressed = false; ShowAlertWindow(pending, true); }
			return;
		}
		if (!pendingWarned && snoozeSlot == null && (pending.Deadline - now).TotalMinutes <= 5)
		{
			pendingWarned = true;
			PhonePush("\u26A0\uFE0F 마감 5분 전", "\U0001F409 " + pending.Hour + "시 초과근무를 아직 확인하지 않았어요\n\u23F3 마감 " + pending.Deadline.ToString("HH:mm"));
			if (PopupOpen()) return;
			Balloon("마감 5분 전", pending.Hour + "시 초과근무를 아직 확인하지 않았어요. 마감 " + pending.Deadline.ToString("HH:mm"), 5000);
			if (dnd) pendingSuppressed = true; else ShowAlertWindow(pending, true);
		}
	}

	// 팝업을 ✕로 그냥 닫았을 때: 마감까지 여유가 있으면 3분 뒤 다시 알려줌
	internal static void OnPopupIgnored(Slot s)
	{
		if (s.Test || Records.IsConfirmed(s.Start)) return;
		if ((s.Deadline - DateTime.Now).TotalMinutes > 4.5) DoSnooze(s, 3, "놓침 방지: ");
	}

	// 트레이에서 현재 시간대 바로 기록
	// 트레이 메뉴: 지금 시각의 시간대에 대한 알림을 바로 추가(띄움). 방해 금지여도 직접 요청이므로 바로 띄움
	internal static void AddAlertNow()
	{
		DateTime n = DateTime.Now;
		Slot s = new Slot { At = n, Start = new DateTime(n.Year, n.Month, n.Day, n.Hour, 0, 0), Test = false };
		if (Records.IsConfirmed(s.Start)) { Balloon("이미 기록됐어요", s.Hour + "시 초과근무는 이미 기록되어 있어요.", 3000); return; }
		snoozeSlot = null; pending = s; pendingWarned = false; pendingSuppressed = false;
		ShowAlertWindow(s, false);
	}



	internal static void ToggleDnd()
	{
		dndManual = !dndManual;
		if (dndItem != null) dndItem.Checked = dndManual;
		SaveSettings(); UpdateTrayText();
		Balloon("방해 금지 " + (dndManual ? "켜짐" : "꺼짐"), dndManual ? "팝업·소리 없이 트레이 알림만 보내요." : "알림 팝업이 다시 나타나요.", 3000);
	}
}
