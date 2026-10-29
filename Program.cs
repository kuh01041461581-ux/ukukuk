using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Media;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
internal class CalPanel : Panel
{
	public DateTime Month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

	public DateTime Selected = DateTime.Today;

	public Predicate<DateTime> IsHoliday;

	private const int CW = 32;

	private const int CH = 26;

	private const int HD = 28;

	private const int WD = 20;

	private static readonly Color Red = Color.FromArgb(220, 38, 38);

	private static readonly Color Blue = Color.FromArgb(37, 99, 235);

	private static readonly Color Ink = Color.FromArgb(31, 41, 55);

	private Font f;

	private Font fb;

	private Rectangle PrevRect => new Rectangle(1, 1, 34, 26);

	private Rectangle NextRect => new Rectangle(191, 1, 34, 26);

	public event EventHandler DateChanged;

	public CalPanel()
	{
		DoubleBuffered = true;
		BackColor = Theme.Bg;
		base.Size = new Size(226, 206);
		f = new Font("맑은 고딕", 9f);
		fb = new Font("맑은 고딕", 9f, FontStyle.Bold);
	}

	private static Color Blend(Color a, double t)
	{
		return Color.FromArgb((int)((double)(int)a.R + (double)(255 - a.R) * t), (int)((double)(int)a.G + (double)(255 - a.G) * t), (int)((double)(int)a.B + (double)(255 - a.B) * t));
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.Clear(BackColor);
		StringFormat stringFormat = new StringFormat();
		stringFormat.Alignment = StringAlignment.Center;
		stringFormat.LineAlignment = StringAlignment.Center;
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(241, 245, 249)))
		{
			graphics.FillRectangle(brush, 0, 0, base.Width, 28);
		}
		graphics.DrawString(Month.ToString("yyyy년 M월"), fb, Brushes.Black, new RectangleF(0f, 0f, base.Width, 28f), stringFormat);
		graphics.DrawString("◀", f, Brushes.DimGray, PrevRect, stringFormat);
		graphics.DrawString("▶", f, Brushes.DimGray, NextRect, stringFormat);
		string[] array = new string[7] { "일", "월", "화", "수", "목", "금", "토" };
		for (int i = 0; i < 7; i++)
		{
			using SolidBrush brush2 = new SolidBrush(i switch
			{
				0 => Red, 
				6 => Blue, 
				_ => Ink, 
			});
			graphics.DrawString(array[i], fb, brush2, new Rectangle(1 + i * 32, 28, 32, 20), stringFormat);
		}
		DateTime dateTime = Month.AddDays(0 - Month.DayOfWeek);
		for (int j = 0; j < 42; j++)
		{
			DateTime obj = dateTime.AddDays(j);
			Rectangle rectangle = new Rectangle(1 + j % 7 * 32, 48 + j / 7 * 26, 32, 26);
			bool flag = obj.Month == Month.Month;
			bool flag2 = IsHoliday != null && IsHoliday(obj);
			Color color = ((flag2 || obj.DayOfWeek == DayOfWeek.Sunday) ? Red : ((obj.DayOfWeek != DayOfWeek.Saturday) ? Ink : Blue));
			if (!flag)
			{
				color = Blend(color, 0.6);
			}
			if (obj.Date == Selected.Date)
			{
				Rectangle rect = rectangle;
				rect.Inflate(-1, -1);
				using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(219, 234, 254)))
				{
					graphics.FillRectangle(brush3, rect);
				}
				using Pen pen = new Pen(Color.FromArgb(59, 130, 246));
				graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
			}
			if (obj.Date == DateTime.Today)
			{
				Rectangle rectangle2 = rectangle;
				rectangle2.Inflate(-3, -3);
				using Pen pen2 = new Pen(Color.FromArgb(234, 88, 12), 2f);
				graphics.DrawRectangle(pen2, rectangle2.X, rectangle2.Y, rectangle2.Width - 1, rectangle2.Height - 1);
			}
			using SolidBrush brush4 = new SolidBrush(color);
			graphics.DrawString(obj.Day.ToString(), (!flag2) ? f : fb, brush4, rectangle, stringFormat);
		}
		using Pen pen3 = new Pen(Color.FromArgb(203, 213, 225));
		graphics.DrawRectangle(pen3, 0, 0, base.Width - 1, base.Height - 1);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		if (PrevRect.Contains(e.Location))
		{
			Month = Month.AddMonths(-1);
			Invalidate();
		}
		else if (NextRect.Contains(e.Location))
		{
			Month = Month.AddMonths(1);
			Invalidate();
		}
		else
		{
			if (e.Y < 48)
			{
				return;
			}
			int num = (e.X - 1) / 32;
			int num2 = (e.Y - 28 - 20) / 26;
			if (num >= 0 && num <= 6 && num2 >= 0 && num2 <= 5)
			{
				DateTime dateTime = (Selected = Month.AddDays(0 - Month.DayOfWeek).AddDays(num2 * 7 + num));
				if (dateTime.Month != Month.Month)
				{
					Month = new DateTime(dateTime.Year, dateTime.Month, 1);
				}
				Invalidate();
				if (this.DateChanged != null)
				{
					this.DateChanged(this, EventArgs.Empty);
				}
			}
		}
	}
}
internal class AlertForm : Form
{
	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 134217856;
			return createParams;
		}
	}
}
internal class MascotBox : Control
{
	public Image Img;

	public Color Glow = Color.FromArgb(124, 92, 255);

	public Color Ring = Color.FromArgb(255, 92, 157);

	private Stopwatch clock = Stopwatch.StartNew();

	public MascotBox()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.FromArgb(248, 246, 255);
	}

	public void Restart()
	{
		clock.Reset();
		clock.Start();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.Clear(BackColor);
		double totalSeconds = clock.Elapsed.TotalSeconds;
		float num = base.Height - 46;
		float num2 = ((Img == null) ? num : (num * (float)Img.Width / (float)Img.Height));
		float num3 = base.Height - 20;
		float num4 = (float)base.Width / 2f;
		float num5 = num3 - num * 0.5f;
		float num6 = (float)Math.Min(base.Width, base.Height) * 0.5f - 4f;
		float num7 = (float)(1.0 + 0.05 * Math.Sin(totalSeconds * 3.0));
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddEllipse(num4 - num6 * num7, num5 - num6 * num7, 2f * num6 * num7, 2f * num6 * num7);
			using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb(120, Glow);
			pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, Glow) };
			graphics.FillPath(pathGradientBrush, graphicsPath);
		}
		for (int i = 0; i < 2; i++)
		{
			double num8 = (totalSeconds * 0.55 + (double)i * 0.5) % 1.0;
			float num9 = num6 * (float)(0.45 + 0.55 * num8);
			int alpha = (int)(130.0 * (1.0 - num8));
			using Pen pen = new Pen(Color.FromArgb(alpha, Ring), 3f);
			graphics.DrawEllipse(pen, num4 - num9, num5 - num9, 2f * num9, 2f * num9);
		}
		double num10 = Math.Exp((0.0 - totalSeconds) * 1.5);
		double num11 = Math.Abs(Math.Sin(totalSeconds * 7.0)) * 18.0 * num10;
		double num12 = Math.Sin(totalSeconds * 2.6) * 3.0;
		float angle = (float)(Math.Sin(totalSeconds * 1.7) * 3.5 + Math.Sin(totalSeconds * 7.0) * 3.0 * num10);
		float sx = (float)(1.0 + Math.Sin(totalSeconds * 2.6) * 0.015);
		float sy = (float)(1.0 - Math.Sin(totalSeconds * 2.6) * 0.015);
		float num13 = (float)(num11 + num12 + 4.0);
		float num14 = Math.Min(0.4f, num13 / 70f);
		float num15 = (float)base.Width * 0.5f * (1f - num14);
		float num16 = 10f * (1f - num14);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)(60f * (1f - Math.Min(0.5f, num13 / 60f))), 15, 23, 42)))
		{
			graphics.FillEllipse(brush, num4 - num15 / 2f, num3 - num16 / 2f + 3f, num15, num16);
		}
		if (Img != null)
		{
			GraphicsState gstate = graphics.Save();
			graphics.TranslateTransform(num4, num3 - num13);
			graphics.RotateTransform(angle);
			graphics.ScaleTransform(sx, sy);
			graphics.DrawImage(Img, (0f - num2) / 2f, 0f - num, num2, num);
			graphics.Restore(gstate);
		}
	}
}
internal partial class Program
{
	private class Item
	{
		public DateTime[] Days;

		public string Name;

		public bool Lunar2;
	}

	private const string AppName = "OvertimeReminder";


	private const string KasiUrl = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo";

	private static NotifyIcon tray;

	private static System.Windows.Forms.Timer timer;

	private static Control ui;

	private static Icon appIcon;

	private static string lastKey = "";

	private static int lastTrayMinute = -1;

	private static int[] minutes = new int[1] { 30 };

	private static string weekdayHoursText = "18-23,0-8";

	private static bool[] weekdayHours = new bool[24];

	private static bool autoStart = false;

	private static bool soundOn = true; internal static bool foStart; private static string foLast = ""; private static DateTime foClickUntil = DateTime.MinValue;

	private static int monthlyLimit = 57;
	internal const int YearLimit = 480;   // 연 초과근무 한도(시간)
	internal static int quitHour = 18; internal static bool quitOn = true; internal static bool preHourOn = true; internal static bool popupSmall = false; internal static bool lightAlert = false; internal static bool widgetOn = false; internal static bool widgetTop = false; internal static int widgetX = -99999, widgetY = -99999; private static DateTime preHourAt = DateTime.MinValue; private static DateTime quitDay = DateTime.MinValue;

	private static MenuItem soundItem;

	private static string kasiKey = "";
	private const string InsarangBuiltin = "http://109.3.3.80:8001/sso/passni21/user/sso_redirect.jsp?app_id=nexin";
	private static string insarangUrl = InsarangBuiltin;   // 프로그램 안에 들어 있는 주소 (설정 파일 값은 쓰지 않음)
	private static bool insarangFront = true;
	private static bool bigUi = false;

	private static string lastSync = "";

	private static string syncStatus = "업데이트 안 함";

	private static bool syncing = false;

	private static Dictionary<int, Dictionary<DateTime, string>> autoCache = new Dictionary<int, Dictionary<DateTime, string>>();

	private static Dictionary<int, Dictionary<DateTime, string>> synced = new Dictionary<int, Dictionary<DateTime, string>>();

	private static Dictionary<DateTime, string> userAdd = new Dictionary<DateTime, string>();

	private static Dictionary<DateTime, bool> userRemove = new Dictionary<DateTime, bool>();

	private static Form settingsForm; private static int memN;

	private static MethodInvoker SyncDone;

	private static Bitmap mascotImg;

	private static string Dir => Environment.GetEnvironmentVariable("OT_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OvertimeReminder");

	private static bool firstRun;
	// 처음 설치하면 그림 사용법을 한 번 보여줘요
	private static void ShowManualFirst()
	{
		try
		{
			string f = Path.Combine(Dir, "manual.seen"); if (File.Exists(f)) return;
			Directory.CreateDirectory(Dir); File.WriteAllText(f, DateTime.Now.ToString("s"));
			System.Windows.Forms.Timer mt = new System.Windows.Forms.Timer(); mt.Interval = 700; mt.Tick += delegate { mt.Stop(); mt.Dispose(); ShowManual(); }; mt.Start();
		}
		catch (Exception ex) { LogError(ex); }
	}
	private static string SetupDonePath { get { return Path.Combine(Dir, "setup.done"); } }
	internal const bool AutoAllowed = true;
	private static string SettingsPath => Path.Combine(Dir, "settings.ini");

	private static string HolidayPath => Path.Combine(Dir, "holidays.txt");

	private static string CachePath => Path.Combine(Dir, "holidays_online.txt");

	private static string ErrorPath => Path.Combine(Dir, "error.log");

	private static string lastErrKey = ""; private static DateTime lastErrAt = DateTime.MinValue;
	private static void LogError(Exception e)
	{
		try
		{
			string key = e.GetType().Name + e.Message;
			if (key == lastErrKey && (DateTime.Now - lastErrAt).TotalSeconds < 30) return;   // 같은 오류가 쏟아져도 한 번만
			lastErrKey = key; lastErrAt = DateTime.Now;
			Directory.CreateDirectory(Dir);
			FileInfo fi = new FileInfo(ErrorPath);
			if (fi.Exists && fi.Length > 256 * 1024) { try { File.Copy(ErrorPath, ErrorPath + ".old", true); File.Delete(ErrorPath); } catch { } }   // 로그가 계속 커지지 않게
			File.AppendAllText(ErrorPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + e.ToString() + "\r\n\r\n", Encoding.UTF8);
		}
		catch
		{
		}
	}

	[STAThread]
	private static void Main()
	{
		try
		{
			Run();
		}
		catch (Exception ex)
		{
			LogError(ex);
			try
			{
				MessageBox.Show("시작 중 오류가 발생했습니다.\r\n" + ex.Message + "\r\n\r\n자세한 내용: " + ErrorPath, "초과근무 알리미");
			}
			catch
			{
			}
		}
	}

	private static void Run()
	{
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs ev)
		{
			LogError(ev.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs ev)
		{
			Exception ue = ev.ExceptionObject as Exception;
			if (ue != null) LogError(ue);
		};
		bool createdNew;
		Mutex obj = new Mutex(initiallyOwned: true, "OvertimeReminder_Single" + (Environment.GetEnvironmentVariable("OT_DIR") != null ? "_dev" : ""), out createdNew);
		if (!createdNew && Environment.GetEnvironmentVariable("OT_DIR") == null && ReplaceOldInstances() > 0)
		{
			for (int rt = 0; rt < 24 && !createdNew; rt++) { try { obj.Dispose(); } catch { } obj = new Mutex(initiallyOwned: true, "OvertimeReminder_Single", out createdNew); if (!createdNew) Thread.Sleep(250); }
		}
		if (!createdNew)
		{
			try
			{
				EventWaitHandle.OpenExisting("OvertimeReminder_Show").Set();
				return;
			}
			catch
			{
				MessageBox.Show("초과근무 알리미가 이미 실행 중입니다.\r\n\r\n이전 버전이 켜져 있는 경우:\r\n우측 하단 트레이(^)의 아이콘을 우클릭해 '종료'하거나,\r\n작업 관리자에서 '초과근무 알리미'를 종료한 뒤 다시 실행하세요.", "초과근무 알리미");
				return;
			}
		}
		EventWaitHandle showEvt = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "OvertimeReminder_Show" + (Environment.GetEnvironmentVariable("OT_DIR") != null ? "_dev" : ""));
		try
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		}
		catch
		{
		}
		Application.EnableVisualStyles();
		ui = new Control();
		ui.CreateControl();
		IntPtr handle = ui.Handle;
		Thread thread = new Thread((ThreadStart)delegate
		{
			while (showEvt.WaitOne())
			{
				try
				{
					ui.BeginInvoke((MethodInvoker)delegate
					{
						ShowSettings();
					});
				}
				catch
				{
				}
			}
		});
		thread.IsBackground = true;
		thread.Start();
		try
		{
			appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		if (appIcon == null)
		{
			appIcon = SystemIcons.Information;
		}
		bool flag = !File.Exists(SettingsPath);
		firstRun = flag;
		if (!flag && !File.Exists(SetupDonePath)) { try { Directory.CreateDirectory(Dir); File.WriteAllText(SetupDonePath, "existing"); } catch { } }
		LoadSettings(); if (replacedOld && autoStart) ApplyAutoStart();
		LoadUserHolidays();
		LoadCache();
		if (flag)
		{
			SaveSettings();
		}
		// 자동 실행 등록은 설정에서 사용자가 직접 켤 때만 한다 (실행할 때마다 몰래 등록하지 않음)
		tray = new NotifyIcon();
		tray.Icon = appIcon;
		tray.Visible = true;
		if (Environment.GetEnvironmentVariable("OT_BENCH") == "1") BenchStart();
		Glow.Prewarm(); AlertPopup.PrebuildLater(7000); PrebuildGlowLater(9500); AlertBanner.PrebuildLater(11000); Confetti.PrebuildLater(12500);
		{ System.Windows.Forms.Timer wt = new System.Windows.Forms.Timer(); wt.Interval = 1200; wt.Tick += delegate { wt.Stop(); wt.Dispose(); Warm(); }; wt.Start(); }
		ContextMenu contextMenu = new ContextMenu();
		MenuItem menuItem = new MenuItem("설정 열기");
		menuItem.DefaultItem = true;
		menuItem.Click += delegate
		{
			ShowSettings();
		};
		contextMenu.MenuItems.Add(menuItem);
		contextMenu.MenuItems.Add("초과근무 기록 보기", delegate
		{
			ShowHistory();
		});
		contextMenu.MenuItems.Add("인사랑 열기", delegate { OpenInsarang(); });
		contextMenu.MenuItems.Add("오늘 뭐 먹지? (메뉴·근처 식당)", delegate { SuggestMenu(); });
		contextMenu.MenuItems.Add("마룡이 운세 · 사주 · 타로", delegate { FortuneForm.Open(); });
		MenuItem bondTray = new MenuItem(Bond.Info()); bondTray.Enabled = false; contextMenu.MenuItems.Add(bondTray); contextMenu.Popup += delegate { bondTray.Text = Bond.Info(); };
		contextMenu.MenuItems.Add("로또 추첨기 (재미용)", delegate { LottoForm.Open(); });
		MenuItem widgetItem = new MenuItem("공항 시계 (끌어서 옮겨요 · 우클릭으로 위치 설정)"); widgetItem.Checked = widgetOn;
		toggleWidget = delegate { widgetOn = !widgetOn; widgetItem.Checked = widgetOn; ClockWidget.Toggle(widgetOn); SaveSettings(); };
		widgetItem.Click += delegate { toggleWidget(); };
		contextMenu.MenuItems.Add(widgetItem);
		if (widgetOn) { System.Windows.Forms.Timer wt = new System.Windows.Forms.Timer(); wt.Interval = 1500; wt.Tick += delegate { wt.Stop(); wt.Dispose(); ClockWidget.Toggle(true); }; wt.Start(); }
		contextMenu.MenuItems.Add("-");
		contextMenu.MenuItems.Add("현재 시간에 알림 추가", delegate
		{
			AddAlertNow();
		});
		contextMenu.MenuItems.Add("기능 찾기…", delegate { PaletteForm.Toggle(); });
		contextMenu.MenuItems.Add("그림 속 글자 복사", delegate { ShotOverlay.Start(true); });
		charItem = new MenuItem("특수문자 입력");
		charItem.Click += delegate { ToggleCharPad(); };
		contextMenu.MenuItems.Add(charItem);
		contextMenu.MenuItems.Add("-");
		MenuItem petItem = new MenuItem("마룡이 펫 꺼내기/넣기");
		petItem.Click += delegate { PetWin.Toggle(); };
		contextMenu.MenuItems.Add(petItem);
		dndItem = new MenuItem("방해 금지 모드");
		dndItem.Checked = dndManual;
		dndItem.Click += delegate { ToggleDnd(); };
		contextMenu.MenuItems.Add(dndItem);
		soundItem = new MenuItem("알림음");
		soundItem.Checked = soundOn;
		soundItem.Click += delegate
		{
			soundOn = !soundOn;
			soundItem.Checked = soundOn;
			SaveSettings();
		};
		contextMenu.MenuItems.Add(soundItem);
		contextMenu.MenuItems.Add("-");
		contextMenu.MenuItems.Add("업데이트 확인 (v" + AppVersion + ")", delegate { UpdateCheck(true); });
		contextMenu.MenuItems.Add("알림 테스트", delegate
		{
			Notify(DateTime.Now, true);
		});
		contextMenu.MenuItems.Add("-");
		contextMenu.MenuItems.Add("종료", delegate
		{
			tray.Visible = false;
			FlushSettings(); Application.Exit();
		});
		tray.ContextMenu = contextMenu;
		// 한 번 클릭 = 요약 카드, 더블클릭 = 설정. 카드는 더블클릭 시간만큼 기다렸다가 띄워 서로 방해하지 않게 한다
		System.Windows.Forms.Timer clickWait = new System.Windows.Forms.Timer();
		clickWait.Interval = SystemInformation.DoubleClickTime + 40;
		clickWait.Tick += delegate { clickWait.Stop(); ShowStatusToast(); };
		DateTime lastDbl = DateTime.MinValue;
		tray.DoubleClick += delegate
		{
			clickWait.Stop(); lastDbl = DateTime.Now;
			try { CloseToast(); if ((Control.ModifierKeys & Keys.Shift) != 0) { Bubble.Say("비밀 통로 발견! 오락실로 가요", 2); MaryongGame.Open(); } else ShowSettings(); } catch (Exception ex) { LogError(ex); }
		};
		UpdateTrayText();
		tray.BalloonTipClicked += delegate { if (DateTime.Now < foClickUntil) { foClickUntil = DateTime.MinValue; FortuneForm.Open(); return; } if (pending != null && !Records.IsConfirmed(pending.Start) && DateTime.Now < pending.Deadline) { pendingSuppressed = false; ShowAlertWindow(pending, true); } };
		tray.MouseClick += delegate(object so, MouseEventArgs me) { if (me.Button == MouseButtons.Left && (DateTime.Now - lastDbl).TotalMilliseconds > 700) { clickWait.Stop(); clickWait.Start(); } };
		System.Windows.Forms.Timer startToast = new System.Windows.Forms.Timer();
		startToast.Interval = 1200;
		startToast.Tick += delegate { startToast.Stop(); startToast.Dispose(); Mem.Later(9000); if (!File.Exists(SetupDonePath)) { ShowSettings(); ShowManualFirst(); } else ShowStatusToast(); System.Windows.Forms.Timer ft = new System.Windows.Forms.Timer(); ft.Interval = 7000; ft.Tick += delegate { ft.Stop(); ft.Dispose(); FoStartup(); }; ft.Start(); UpdateInit(); };
		startToast.Start();
		timer = new System.Windows.Forms.Timer();
		timer.Interval = 1000;
		timer.Tick += delegate
		{
			DateTime now = DateTime.Now;
			SnoozeTick(now);
			QuitTick(now); PreHourTick(now);
			if (++memN >= 300) { memN = 0; if (Mem.NoVisibleForms()) Mem.Trim(); }
			PendingTick(now);
			DeadlineTick(now);
			if (now.Minute != lastTrayMinute)
			{
				lastTrayMinute = now.Minute;
				UpdateTrayText();
				MaybeAutoSync();
			}
			if (IsAlertMinute(now.Minute) && ShouldAlert(now))
			{
				string text = now.ToString("yyyyMMddHHmm");
				if (text != lastKey)
				{
					lastKey = text;
					Notify(now);
				}
			}
		};
		timer.Start();
		if (flag)
		{
			ui.BeginInvoke((MethodInvoker)delegate
			{
				ShowSettings();
			});
		}
		MaybeAutoSync();
		ApplyHotkeys();
		// 절전에서 깨어나거나 시스템 시간이 바뀌면 표시를 바로 다시 계산
		MethodInvoker resync = delegate { lastTrayMinute = -1; nacKey = null; };
		Microsoft.Win32.SystemEvents.PowerModeChanged += delegate(object so, Microsoft.Win32.PowerModeChangedEventArgs pe)
		{
			if (pe.Mode == Microsoft.Win32.PowerModes.Resume) { try { ui.BeginInvoke(resync); } catch { } }
		};
		Microsoft.Win32.SystemEvents.TimeChanged += delegate { try { ui.BeginInvoke(resync); } catch { } };
		Application.Run();
		GC.KeepAlive(obj);
		GC.KeepAlive(showEvt);
	}

	private static bool IsAlertMinute(int min)
	{
		for (int i = 0; i < minutes.Length; i++)
		{
			if (minutes[i] == min)
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsWeekend(DateTime d)
	{
		return d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
	}

	// 퇴근 30초 전: 토스트를 띄워 두고 퇴근까지 초 단위로 세어 줘요 (평일만, 하루 한 번)
	private static void QuitTick(DateTime now)
	{
		try
		{
			if (!quitOn || quitDay == now.Date || IsOffDay(now)) return;
			DateTime q = now.Date.AddHours(quitHour);
			double left = (q - now).TotalSeconds;
			if (left <= 30 && left > -3) { quitDay = now.Date; ShowQuitToast(q); }
		}
		catch (Exception ex) { LogError(ex); }
	}

	// 정각 10초 전: 오른쪽 아래에 10초 카운트다운 카드를 띄워 줘요 (알림이 울리는 시각에만)
	private static void PreHourTick(DateTime now)
	{
		try
		{
			if (!preHourOn || now.Minute != 59 || now.Second < 50) return;
			DateTime h = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
			if (preHourAt == h || !ShouldAlert(h)) return;
			if (quitOn && !IsOffDay(now) && Math.Abs((h - now.Date.AddHours(quitHour)).TotalMinutes) < 1) return;   // 퇴근 카운트다운과 겹치면 그쪽만
			preHourAt = h; ShowQuitToast(h, true);
		}
		catch (Exception ex) { LogError(ex); }
	}

	private static bool IsOffDay(DateTime d)
	{
		return IsWeekend(d) || IsHoliday(d);
	}

	private static bool ShouldAlert(DateTime n)
	{
		if (IsOffDay(n))
		{
			return true;
		}
		return weekdayHours[n.Hour];
	}

	private static string DayLabel(DateTime d)
	{
		if (HolidayName(d, out var name))
		{
			return "공휴일/휴일(" + name + ") - 매시간 알림";
		}
		if (IsWeekend(d))
		{
			return "주말 - 매시간 알림";
		}
		return "평일 - " + weekdayHoursText.Replace("-", "~").Replace(",", ", ") + "시에만 알림";
	}

	private static string NextAlertText()
	{
		DateTime now = DateTime.Now;
		DateTime n = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1.0);
		for (int i = 0; i < 14400; i++)
		{
			if (IsAlertMinute(n.Minute) && ShouldAlert(n))
			{
				return n.ToString("M월 d일 HH:mm");
			}
			n = n.AddMinutes(1.0);
		}
		return "없음";
	}

	private static bool BaseHoliday(DateTime d, out string name)
	{
		Dictionary<DateTime, string> dictionary = BaseYear(d.Year);
		return dictionary.TryGetValue(d.Date, out name);
	}

	private static Dictionary<DateTime, string> BaseYear(int y)
	{
		if (synced.TryGetValue(y, out var value))
		{
			return value;
		}
		if (!autoCache.TryGetValue(y, out var value2))
		{
			value2 = ComputeYear(y);
			autoCache[y] = value2;
		}
		return value2;
	}

	private static bool HolidayName(DateTime d, out string name)
	{
		d = d.Date;
		name = "";
		if (userRemove.ContainsKey(d))
		{
			return false;
		}
		if (userAdd.TryGetValue(d, out name))
		{
			return true;
		}
		return BaseHoliday(d, out name);
	}

	private static bool IsHoliday(DateTime d)
	{
		string name;
		return HolidayName(d, out name);
	}

	private static Dictionary<DateTime, string> ComputeYear(int y)
	{
		Dictionary<DateTime, string> map = new Dictionary<DateTime, string>();
		Dictionary<DateTime, int> cnt = new Dictionary<DateTime, int>();
		List<Item> items = new List<Item>();
		Action<DateTime[], string, bool> action = delegate(DateTime[] days, string name, bool l2)
		{
			Item item = new Item
			{
				Days = days,
				Name = name,
				Lunar2 = l2
			};
			items.Add(item);
			for (int j = 0; j < days.Length; j++)
			{
				cnt.TryGetValue(days[j], out var value);
				cnt[days[j]] = value + 1;
				if (!map.ContainsKey(days[j]))
				{
					map[days[j]] = name;
				}
			}
		};
		action(new DateTime[1]
		{
			new DateTime(y, 1, 1)
		}, "신정", arg3: false);
		action(new DateTime[1]
		{
			new DateTime(y, 3, 1)
		}, "*삼일절", arg3: false);
		action(new DateTime[1]
		{
			new DateTime(y, 5, 5)
		}, "*어린이날", arg3: false);
		action(new DateTime[1]
		{
			new DateTime(y, 6, 6)
		}, "현충일", arg3: false);
		action(new DateTime[1]
		{
			new DateTime(y, 8, 15)
		}, "*광복절", arg3: false);
		action(new DateTime[1]
		{
			new DateTime(y, 10, 3)
		}, "*개천절", arg3: false);
		action(new DateTime[1]
		{
			new DateTime(y, 10, 9)
		}, "*한글날", arg3: false);
		action(new DateTime[1]
		{
			new DateTime(y, 12, 25)
		}, "*성탄절", arg3: false);
		if (y == 2026)
		{
			action(new DateTime[1]
			{
				new DateTime(2026, 6, 3)
			}, "지방선거일", arg3: false);
		}
		if (y == 2028)
		{
			action(new DateTime[1]
			{
				new DateTime(2028, 4, 12)
			}, "국회의원선거일", arg3: false);
		}
		try
		{
			KoreanLunisolarCalendar koreanLunisolarCalendar = new KoreanLunisolarCalendar();
			int leapMonth = koreanLunisolarCalendar.GetLeapMonth(y);
			DateTime dateTime = koreanLunisolarCalendar.ToDateTime(y, 1, 1, 0, 0, 0, 0);
			action(new DateTime[3]
			{
				dateTime.AddDays(-1.0),
				dateTime,
				dateTime.AddDays(1.0)
			}, "*설날", arg3: true);
			int month = ((leapMonth == 0 || leapMonth > 8) ? 8 : 9);
			DateTime dateTime2 = koreanLunisolarCalendar.ToDateTime(y, month, 15, 0, 0, 0, 0);
			action(new DateTime[3]
			{
				dateTime2.AddDays(-1.0),
				dateTime2,
				dateTime2.AddDays(1.0)
			}, "*추석", arg3: true);
			int month2 = ((leapMonth == 0 || leapMonth > 4) ? 4 : 5);
			DateTime dateTime3 = koreanLunisolarCalendar.ToDateTime(y, month2, 8, 0, 0, 0, 0);
			action(new DateTime[1] { dateTime3 }, "*부처님오신날", arg3: false);
		}
		catch
		{
		}
		items.Sort((Item a, Item b) => a.Days[0].CompareTo(b.Days[0]));
		Dictionary<DateTime, bool> dictionary = new Dictionary<DateTime, bool>();
		foreach (Item item2 in items)
		{
			string text = item2.Name.TrimStart(new char[1] { '*' });
			bool flag = item2.Name.StartsWith("*") && y >= 2021;
			if (item2.Days.Length == 3)
			{
				map[item2.Days[0]] = text + "연휴";
				map[item2.Days[2]] = text + "연휴";
				map[item2.Days[1]] = text;
			}
			else
			{
				map[item2.Days[0]] = text;
			}
			if (!flag)
			{
				continue;
			}
			bool flag2 = false;
			List<DateTime> list = new List<DateTime>();
			DateTime[] days2 = item2.Days;
			for (int i = 0; i < days2.Length; i++)
			{
				DateTime dateTime4 = days2[i];
				if (item2.Lunar2)
				{
					if (dateTime4.DayOfWeek == DayOfWeek.Sunday)
					{
						flag2 = true;
					}
				}
				else if (IsWeekend(dateTime4))
				{
					flag2 = true;
				}
				if (cnt[dateTime4] > 1)
				{
					flag2 = true;
					list.Add(dateTime4);
				}
			}
			if (!flag2)
			{
				continue;
			}
			bool flag3 = false;
			foreach (DateTime item3 in list)
			{
				if (dictionary.ContainsKey(item3))
				{
					flag3 = true;
				}
			}
			if (flag3)
			{
				continue;
			}
			DateTime dateTime5 = item2.Days[item2.Days.Length - 1];
			DateTime dateTime6 = dateTime5.AddDays(1.0);
			while (IsWeekend(dateTime6) || map.ContainsKey(dateTime6))
			{
				dateTime6 = dateTime6.AddDays(1.0);
			}
			map[dateTime6] = "대체공휴일(" + text + ")";
			foreach (DateTime item4 in list)
			{
				dictionary[item4] = true;
			}
		}
		return map;
	}

	private static void LoadSettings()
	{
		try
		{
			if (File.Exists(SettingsPath))
			{
				string[] array = File.ReadAllLines(SettingsPath, Encoding.UTF8);
				foreach (string text in array)
				{
					int num = text.IndexOf('=');
					if (num < 0)
					{
						continue;
					}
					string text2 = text.Substring(0, num).Trim();
					string text3 = text.Substring(num + 1).Trim();
					switch (text2)
					{
					case "minutes":
					{
						int[] array2 = ParseMinutes(text3);
						if (array2 != null)
						{
							minutes = array2;
						}
						break;
					}
					case "weekdayHours":
						if (ParseHours(text3) != null)
						{
							weekdayHoursText = text3;
						}
						break;
					case "autostart":
						autoStart = text3 == "1";
						break;
					case "foStart":
						foStart = text3 == "1";
						break;
					case "foLast":
						foLast = text3.Trim();
						break;
					case "sound":
						soundOn = text3 == "1";
						break;
					case "dnd":
						dndManual = text3 == "1";
						break;
					case "dndAuto":
						dndAuto = text3 != "0";
						break;
					case "phoneOn":
						phoneOn = text3 == "1";
						break;
					case "phoneTopic":
						phoneTopic = text3.Trim();
						break;
					case "toolsOn": toolsOn = text3 != "0"; break;
					case "toolsPaste": toolsPaste = text3 != "0"; break;
					case "moneyFmt": { int v; if (int.TryParse(text3, out v) && v >= 0 && v < 3) moneyFmt = v; break; }
					case "dateFmt": { int v; if (int.TryParse(text3, out v) && v >= 0 && v < 3) dateFmt = v; break; }
					case "payGrade": payGradeId = text3.Trim(); break;
					case "payBase": { int v; if (int.TryParse(text3, out v) && v >= 0) payBase = v; break; }
					case "payRate": { int v; if (int.TryParse(text3, out v) && (v == 55 || v == 60)) payRate = v; break; }
					case "payCap4": payCap4 = false; break;
					case "hotkeys": HkLoad(text3.Trim()); break;
					case "quitHour": { int v; quitHour = 18; break; }
					case "quitOn": quitOn = text3.Trim() != "0"; break;
					case "preHourOn": preHourOn = text3.Trim() != "0"; break;
					case "widgetOn": widgetOn = text3.Trim() == "1"; break;
					case "widgetTop": widgetTop = text3.Trim() == "1"; break;
					case "popupSmall": popupSmall = text3.Trim() == "1"; break;
					case "lightAlert": lightAlert = text3.Trim() == "1"; break;
					case "moneyUnitTo": { int mu; if (int.TryParse(text3.Trim(), out mu) && mu >= 0 && mu <= 4) moneyUnitTo = mu; break; }
					case "moneyUnitBare": { int mb; if (int.TryParse(text3.Trim(), out mb) && mb >= 0 && mb <= 4) moneyUnitBare = mb; break; }
					case "sumAcc": sumAcc = text3.Trim() != "0"; break;
					case "widgetX": { int v; if (int.TryParse(text3, out v)) widgetX = v; break; }
					case "widgetY": { int v; if (int.TryParse(text3, out v)) widgetY = v; break; }
					case "monthLimit":
					{
						int lim;
						if (int.TryParse(text3, out lim) && lim >= 1 && lim <= 300) monthlyLimit = lim;
						break;
					}
					case "insarang":
						break;
					case "uiScale":
						bigUi = text3.Length > 0 && text3 != "1" && text3 != "1.0";
						break;
					case "insarangFront":
						insarangFront = text3 != "0";
						break;
					case "kasiKey":
						kasiKey = text3;
						break;
					case "lastSync":
						lastSync = text3;
						break;
					}
				}
			}
		}
		catch
		{
		}
		bool[] array3 = ParseHours(weekdayHoursText);
		weekdayHours = ((array3 == null) ? ParseHours("18-23,0-8") : array3);
		if (kasiKey.Length == 0)
		{
			kasiKey = Secrets.KasiKey;
		}
	}

	internal static void SaveSettingsPublic() { SaveSettings(); }
	internal static void OpenSettingsPublic() { ShowSettings(); }
	private static void SaveSettings()
	{
		try
		{
			Directory.CreateDirectory(Dir);
			string text = "";
			for (int i = 0; i < minutes.Length; i++)
			{
				text = text + ((i <= 0) ? "" : ",") + minutes[i];
			}
			SaveAsync(SettingsPath, new string[40]
			{
				"minutes=" + text,
				"weekdayHours=" + weekdayHoursText,
				"autostart=" + ((!autoStart) ? "0" : "1"),
				"sound=" + ((!soundOn) ? "0" : "1"),
				"foStart=" + (foStart ? "1" : "0"),
				"foLast=" + foLast,
				"kasiKey=" + kasiKey,
				"insarang=" + insarangUrl,
				"insarangFront=" + (insarangFront ? "1" : "0"),
				"uiScale=" + (bigUi ? "1.15" : "1"),
				"theme=" + Theme.Mode,
				"accent=" + Theme.AccentIdx,
				"style=" + Theme.Style,
				"lastSync=" + lastSync,
				"monthLimit=" + monthlyLimit,
				"quitHour=" + quitHour,
				"quitOn=" + (quitOn ? "1" : "0"),
				"preHourOn=" + (preHourOn ? "1" : "0"),
				"widgetOn=" + (widgetOn ? "1" : "0"),
				"widgetX=" + widgetX,
				"widgetY=" + widgetY,
				"widgetTop=" + (widgetTop ? "1" : "0"),
				"popupSmall=" + (popupSmall ? "1" : "0"),
				"lightAlert=" + (lightAlert ? "1" : "0"),
				"moneyUnitTo=" + moneyUnitTo,
				"moneyUnitBare=" + moneyUnitBare,
				"sumAcc=" + (sumAcc ? "1" : "0"),
				"dnd=" + (dndManual ? "1" : "0"),
				"dndAuto=" + (dndAuto ? "1" : "0"),
				"phoneOn=" + (phoneOn ? "1" : "0"),
				"phoneTopic=" + phoneTopic,
				"toolsOn=" + (toolsOn ? "1" : "0"),
				"toolsPaste=" + (toolsPaste ? "1" : "0"),
				"moneyFmt=" + moneyFmt,
				"dateFmt=" + dateFmt,
				"payGrade=" + payGradeId,
				"payBase=" + payBase,
				"payRate=" + payRate,
				"payCap4=0",
				"hotkeys=" + HkSave()
			});
		}
		catch
		{
		}
	}

	private static void LoadUserHolidays()
	{
		userAdd.Clear();
		userRemove.Clear();
		try
		{
			if (!File.Exists(HolidayPath))
			{
				return;
			}
			string[] array = File.ReadAllLines(HolidayPath, Encoding.UTF8);
			foreach (string text in array)
			{
				string text2 = text.Trim();
				if (text2.Length < 10 || text2[0] == '#')
				{
					continue;
				}
				bool flag = text2[0] == '-';
				if (flag)
				{
					text2 = text2.Substring(1).Trim();
				}
				if (text2.Length >= 10 && DateTime.TryParse(text2.Substring(0, 10), out var result))
				{
					if (flag)
					{
						userRemove[result.Date] = true;
						userAdd.Remove(result.Date);
					}
					else
					{
						userAdd[result.Date] = ((text2.Length <= 11) ? "사용자 지정" : text2.Substring(11).Trim());
						userRemove.Remove(result.Date);
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void SaveUserHolidays()
	{
		try
		{
			Directory.CreateDirectory(Dir);
			List<string> list = new List<string>();
			list.Add("# 설정 창의 '휴일' 탭에서 지정한 날짜 (직접 편집도 가능)");
			list.Add("# 추가:  2028-01-01 신정      제외:  -2026-05-05");
			List<DateTime> list2 = new List<DateTime>(userAdd.Keys);
			list2.Sort();
			foreach (DateTime item in list2)
			{
				list.Add(item.ToString("yyyy-MM-dd") + " " + userAdd[item]);
			}
			List<DateTime> list3 = new List<DateTime>(userRemove.Keys);
			list3.Sort();
			foreach (DateTime item2 in list3)
			{
				list.Add("-" + item2.ToString("yyyy-MM-dd"));
			}
			AtomicIO.WriteLines(HolidayPath, list.ToArray(), Encoding.UTF8);
		}
		catch
		{
		}
	}

	private static void LoadCache()
	{
		synced.Clear();
		try
		{
			if (!File.Exists(CachePath))
			{
				return;
			}
			string[] array = File.ReadAllLines(CachePath, Encoding.UTF8);
			foreach (string text in array)
			{
				string text2 = text.Trim();
				DateTime result2;
				Dictionary<DateTime, string> value;
				if (text2.StartsWith("@year "))
				{
					if (int.TryParse(text2.Substring(6).Trim(), out var result) && !synced.ContainsKey(result))
					{
						synced[result] = new Dictionary<DateTime, string>();
					}
				}
				else if (text2.Length >= 10 && text2[0] != '#' && DateTime.TryParse(text2.Substring(0, 10), out result2) && synced.TryGetValue(result2.Year, out value))
				{
					value[result2.Date] = ((text2.Length <= 11) ? "공휴일" : text2.Substring(11).Trim());
				}
			}
		}
		catch
		{
		}
	}

	private static void SaveCache()
	{
		try
		{
			Directory.CreateDirectory(Dir);
			List<string> list = new List<string>();
			list.Add("# 공공데이터포털 특일정보에서 받은 공휴일 (자동 생성)");
			List<int> list2 = new List<int>(synced.Keys);
			list2.Sort();
			foreach (int item in list2)
			{
				list.Add("@year " + item);
				List<DateTime> list3 = new List<DateTime>(synced[item].Keys);
				list3.Sort();
				foreach (DateTime item2 in list3)
				{
					list.Add(item2.ToString("yyyy-MM-dd") + " " + synced[item][item2]);
				}
			}
			AtomicIO.WriteLines(CachePath, list.ToArray(), Encoding.UTF8);
		}
		catch
		{
		}
	}

	private static bool ParseKasi(string xml, Dictionary<DateTime, string> into, out string err)
	{
		err = "";
		XmlDocument xmlDocument = new XmlDocument();
		try
		{
			xmlDocument.LoadXml(xml);
		}
		catch (Exception ex)
		{
			err = "응답 해석 실패: " + ex.Message;
			return false;
		}
		XmlNode xmlNode = xmlDocument.SelectSingleNode("//returnAuthMsg");
		if (xmlNode != null)
		{
			err = "인증 오류: " + xmlNode.InnerText;
			return false;
		}
		XmlNode xmlNode2 = xmlDocument.SelectSingleNode("//resultCode");
		if (xmlNode2 != null && xmlNode2.InnerText.Trim() != "00")
		{
			XmlNode xmlNode3 = xmlDocument.SelectSingleNode("//resultMsg");
			err = "서버 오류 " + xmlNode2.InnerText + ((xmlNode3 == null) ? "" : (" " + xmlNode3.InnerText));
			return false;
		}
		if (xmlNode2 == null)
		{
			err = "예상하지 못한 응답 형식";
			return false;
		}
		foreach (XmlNode item in xmlDocument.SelectNodes("//item"))
		{
			XmlNode xmlNode5 = item.SelectSingleNode("isHoliday");
			XmlNode xmlNode6 = item.SelectSingleNode("locdate");
			XmlNode xmlNode7 = item.SelectSingleNode("dateName");
			if (xmlNode5 != null && xmlNode6 != null && !(xmlNode5.InnerText.Trim() != "Y") && DateTime.TryParseExact(xmlNode6.InnerText.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				into[result.Date] = ((xmlNode7 == null) ? "공휴일" : xmlNode7.InnerText.Trim());
			}
		}
		return true;
	}

	private static string FetchMonth(int y, int mo)
	{
		string text = ((!kasiKey.Contains("%")) ? Uri.EscapeDataString(kasiKey) : kasiKey);
		string requestUriString = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo?solYear=" + y + "&solMonth=" + mo.ToString("00") + "&numOfRows=50&ServiceKey=" + text;
		HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUriString);
		httpWebRequest.Timeout = 15000;
		httpWebRequest.Proxy = WebRequest.GetSystemWebProxy();
		httpWebRequest.Proxy.Credentials = CredentialCache.DefaultCredentials;
		using WebResponse webResponse = httpWebRequest.GetResponse();
		using StreamReader streamReader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8);
		return streamReader.ReadToEnd();
	}

	private static void MaybeAutoSync()
	{
		if (!syncing && kasiKey.Length != 0 && !(lastSync == DateTime.Now.ToString("yyyy-MM-dd")))
		{
			StartSync(manual: false);
		}
	}

	private static void StartSync(bool manual)
	{
		if (syncing)
		{
			return;
		}
		if (kasiKey.Length == 0)
		{
			syncStatus = "인증키를 먼저 입력하세요";
			return;
		}
		syncing = true;
		syncStatus = "업데이트 중...";
		Thread thread = new Thread((ThreadStart)delegate
		{
			Dictionary<int, Dictionary<DateTime, string>> got = new Dictionary<int, Dictionary<DateTime, string>>();
			string error = "";
			int year = DateTime.Now.Year;
			for (int i = year; i <= year + 1; i++)
			{
				if (error.Length != 0)
				{
					break;
				}
				Dictionary<DateTime, string> dictionary = new Dictionary<DateTime, string>();
				for (int j = 1; j <= 12; j++)
				{
					try
					{
						if (!ParseKasi(FetchMonth(i, j), dictionary, out var err))
						{
							error = i + "년 " + j + "월: " + err;
							break;
						}
					}
					catch (Exception ex)
					{
						error = "연결 실패(" + ex.Message + ")";
						break;
					}
				}
				if (error.Length == 0)
				{
					got[i] = dictionary;
				}
			}
			ui.BeginInvoke((MethodInvoker)delegate
			{
				syncing = false;
				if (error.Length == 0)
				{
					int num = 0;
					foreach (KeyValuePair<int, Dictionary<DateTime, string>> item in got)
					{
						synced[item.Key] = item.Value;
						num += item.Value.Count;
					}
					SaveCache();
					lastSync = DateTime.Now.ToString("yyyy-MM-dd");
					SaveSettings();
					syncStatus = "업데이트 완료 (" + DateTime.Now.ToString("M월 d일 HH:mm") + ", " + num + "일)";
					if (manual)
					{
						tray.BalloonTipText = "공휴일 업데이트 완료 (" + num + "일)";
						tray.ShowBalloonTip(3000);
					}
				}
				else
				{
					syncStatus = "업데이트 실패: " + error + " (내장 계산값 사용 중)";
					if (manual)
					{
						tray.BalloonTipText = syncStatus;
						tray.ShowBalloonTip(5000);
					}
				}
				UpdateTrayText();
				if (SyncDone != null)
				{
					SyncDone();
				}
			});
		});
		thread.IsBackground = true;
		thread.Start();
	}

	private static int[] ParseMinutes(string t)
	{
		List<int> list = new List<int>();
		string[] array = t.Split(new char[2] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			if (!int.TryParse(text.Trim(), out var result) || result < 0 || result > 59)
			{
				return null;
			}
			if (!list.Contains(result))
			{
				list.Add(result);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		list.Sort();
		return list.ToArray();
	}

	private static string HoursToText(bool[] h)
	{
		int num = 0;
		for (int i = 0; i < 24; i++)
		{
			if (h[i])
			{
				num++;
			}
		}
		switch (num)
		{
		case 0:
			return null;
		case 24:
			return "0-23";
		default:
		{
			int j;
			for (j = 0; h[j]; j++)
			{
			}
			List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>();
			int k = 0;
			while (k < 24)
			{
				int num2 = (j + k) % 24;
				if (!h[num2])
				{
					k++;
					continue;
				}
				int num3 = num2;
				int num4 = 0;
				for (; k < 24 && h[(j + k) % 24]; k++)
				{
					num4++;
				}
				int num5 = (num3 + num4 - 1) % 24;
				list.Add(new KeyValuePair<int, string>(num3, (num4 != 1) ? (num3 + "-" + num5) : num3.ToString()));
			}
			list.Sort((KeyValuePair<int, string> a, KeyValuePair<int, string> b) => a.Key.CompareTo(b.Key));
			List<string> list2 = new List<string>();
			foreach (KeyValuePair<int, string> item in list)
			{
				list2.Add(item.Value);
			}
			return string.Join(",", list2.ToArray());
		}
		}
	}

	private static bool[] ParseHours(string t)
	{
		bool[] array = new bool[24];
		bool flag = false;
		string[] array2 = t.Split(new char[2] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array2)
		{
			string[] array3 = text.Split(new char[1] { '-' });
			int result;
			if (array3.Length == 1)
			{
				if (!int.TryParse(array3[0], out result) || result < 0 || result > 23)
				{
					return null;
				}
				array[result] = true;
				flag = true;
				continue;
			}
			if (array3.Length == 2)
			{
				if (!int.TryParse(array3[0], out result) || !int.TryParse(array3[1], out var result2) || result < 0 || result > 23 || result2 < 0 || result2 > 23)
				{
					return null;
				}
				int num = result;
				while (true)
				{
					array[num] = true;
					if (num == result2)
					{
						break;
					}
					num = (num + 1) % 24;
				}
				flag = true;
				continue;
			}
			return null;
		}
		return (!flag) ? null : array;
	}

	private static void ApplyAutoStart()
	{
		try
		{
			RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey != null)
			{
				if (autoStart)
				{
					registryKey.SetValue("OvertimeReminder", "\"" + Application.ExecutablePath + "\"");
				}
				else
				{
					registryKey.DeleteValue("OvertimeReminder", throwOnMissingValue: false);
				}
				registryKey.Close();
			}
		}
		catch
		{
			if (tray != null)
			{
				tray.BalloonTipText = "자동 실행 등록에 실패했습니다(보안 정책일 수 있음).";
				tray.ShowBalloonTip(4000);
			}
		}
	}

	private static void UpdateTrayText()
	{
		if (tray != null)
		{
			string text = (snoozeSlot != null) ? ("초과근무 알리미 · 다시 알림 " + snoozeAt.ToString("HH:mm")) : ("초과근무 알리미 · 다음 " + NextAlertText());
			if (dndManual) text = "[방해금지] " + text;
			tray.Text = ((text.Length <= 63) ? text : text.Substring(0, 63));
		}
	}

	private static Bitmap LoadMascot()
	{
		try
		{
			using Stream stream = typeof(Program).Assembly.GetManifestResourceStream("mascot.png");
			if (stream != null)
			{
				using (Bitmap original = new Bitmap(stream))
				{
					return new Bitmap(original);
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static Bitmap ClockBitmap(int sz)
	{
		Bitmap bitmap = new Bitmap(sz, sz);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		float num = (float)sz * 0.03f;
		float num2 = (float)sz / 2f;
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(124, 92, 255)))
		{
			graphics.FillEllipse(brush, num, num, (float)sz - 2f * num, (float)sz - 2f * num);
		}
		float num3 = (float)sz * 0.09f;
		graphics.FillEllipse(Brushes.White, num3, num3, (float)sz - 2f * num3, (float)sz - 2f * num3);
		using Pen pen = new Pen(Color.FromArgb(30, 41, 59), (float)sz * 0.07f);
		pen.StartCap = LineCap.Round;
		pen.EndCap = LineCap.Round;
		double num4 = 5.235987755982989;
		double num5 = Math.PI / 3.0;
		graphics.DrawLine(pen, num2, num2, num2 + (float)Math.Sin(num4) * (float)sz * 0.2f, num2 - (float)Math.Cos(num4) * (float)sz * 0.2f);
		graphics.DrawLine(pen, num2, num2, num2 + (float)Math.Sin(num5) * (float)sz * 0.3f, num2 - (float)Math.Cos(num5) * (float)sz * 0.3f);
		return bitmap;
	}



	internal static int Z(int v)
	{
		return Dpi.Z(v);
	}
}
