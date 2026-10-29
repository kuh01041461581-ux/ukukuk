using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// 인사랑 바로가기: 이미 열려 있으면 그 창을 앞으로, 없으면 등록해 둔 주소를 연다
// (기관마다 주소가 다르고 로그인은 브라우저·새올 쪽 세션이 처리한다 — 이 프로그램은 아이디·비밀번호를 다루지 않는다)
internal partial class Program
{
	private delegate bool EnumProc(IntPtr h, IntPtr l);
	[DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
	[DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
	[DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
	[DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
	[DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
	[DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
	[DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
	[DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

	internal static bool HasInsarang { get { return insarangUrl.Trim().Length > 0; } }

	// 제목에 '인사랑'이 들어 있는 보이는 창(우리 프로그램 제외)을 찾는다
	private static IntPtr FindInsarangWindow()
	{
		IntPtr found = IntPtr.Zero;
		try
		{
			uint me = (uint)Process.GetCurrentProcess().Id;
			EnumWindows(delegate(IntPtr h, IntPtr l)
			{
				if (!IsWindowVisible(h)) return true;
				StringBuilder sb = new StringBuilder(256);
				if (GetWindowText(h, sb, 256) <= 0) return true;
				if (sb.ToString().IndexOf("인사랑", StringComparison.Ordinal) < 0) return true;
				uint pid; GetWindowThreadProcessId(h, out pid);
				if (pid == me) return true;
				found = h; return false;
			}, IntPtr.Zero);
		}
		catch { }
		return found;
	}

	private static bool BringToFront(IntPtr h)
	{
		try
		{
			if (IsIconic(h)) ShowWindow(h, 9);   // SW_RESTORE
			keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);   // 포그라운드 제한 우회용 Alt 입력
			return SetForegroundWindow(h);
		}
		catch { return false; }
	}

	// 알림이 뜰 때: 이미 열려 있는 인사랑 창만 앞으로 (새로 열지는 않는다)
	internal static void BringInsarangFront()
	{
		try { if (!insarangFront) return; System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { IntPtr h = FindInsarangWindow(); if (h != IntPtr.Zero) BringToFront(h); } catch (Exception ex2) { LogError(ex2); } }); } catch (Exception ex) { LogError(ex); }
	}

	// '확인했어요'를 눌렀을 때: 열려 있으면 앞으로, 없으면 등록한 주소를 연다 (주소가 없으면 아무것도 안 한다)
	internal static void OpenInsarangAuto()
	{
		// 창 찾기·브라우저 실행은 느릴 수 있어서 화면(결과 표시)을 먼저 그리고, 뒤에서 처리해요 → 확인을 눌러도 멈칫하지 않아요
		try
		{
			System.Threading.SynchronizationContext ui = System.Threading.SynchronizationContext.Current;
			System.Threading.ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					System.Threading.Thread.Sleep(140);
					IntPtr h = FindInsarangWindow();
					if (h != IntPtr.Zero && BringToFront(h)) return;
					string u = insarangUrl.Trim();
					if (u.Length == 0) { if (ui != null) ui.Post(delegate { OpenInsarang(); }, null); return; }
					if (u.IndexOf("://") < 0 && !File.Exists(u) && !Directory.Exists(u)) u = "http://" + u;
					Process.Start(u);
				}
				catch (Exception ex2) { LogError(ex2); if (ui != null) ui.Post(delegate { OpenInsarang(); }, null); }
			});
		}
		catch (Exception ex) { LogError(ex); }
	}

	// 버튼·메뉴로 눌렀을 때: 열려 있으면 그 창, 없으면 등록한 주소
	internal static void OpenInsarang()
	{
		try
		{
			IntPtr h = FindInsarangWindow();
			if (h != IntPtr.Zero && BringToFront(h)) return;
			string u = insarangUrl.Trim();
			if (u.Length == 0)
			{
				ShowSettings();
				if (settingsGo != null) settingsGo(1);
				if (tray != null) { tray.BalloonTipText = "알림 메뉴 맨 아래에 인사랑 주소를 한 번만 넣어 두세요."; tray.ShowBalloonTip(4000); }
				return;
			}
			if (u.IndexOf("://") < 0 && !File.Exists(u) && !Directory.Exists(u)) u = "http://" + u;
			Process.Start(u);
		}
		catch (Exception ex)
		{
			LogError(ex);
			try { if (tray != null) { tray.BalloonTipText = "인사랑을 열지 못했어요. 주소를 다시 확인해 주세요."; tray.ShowBalloonTip(4000); } } catch { }
		}
	}

	// '글자 크게' 전환 (다시 시작해야 적용)
	internal static bool BigUi { get { return bigUi; } }
	internal static void ToggleBigUi() { RestartWith(delegate { bigUi = !bigUi; }); }
	// 화면 색: 껐다 켜지 않고, 옛 화면 사진을 위에 얹은 채로 새 색의 창을 만든 뒤 사진을 스르륵 걷어낸다
	internal static Bitmap themeSnap; internal static Rectangle themeRect; internal static int themePage = -1; internal static ThemeVeil themeVeil;
	internal static Func<int> settingsPage; internal static Func<Rectangle, Bitmap> shotProvider; internal static Action<int> themeTrans; internal static Action settingsReopen; internal static bool prebuilding;
	internal static void OpenArcadeWithFireworks(Form from)
	{
		try { Sfx.Play("fanfare"); Confetti.Fire(from.Bounds); MaryongGame.OpenMenu(); System.Windows.Forms.Timer tcf = new System.Windows.Forms.Timer(); tcf.Interval = 450; tcf.Tick += delegate { tcf.Stop(); tcf.Dispose(); try { Form ar = null; foreach (Form of in Application.OpenForms) if (of is MaryongGame) ar = of; if (ar != null) Confetti.Fire(ar.Bounds); } catch { } }; tcf.Start(); }
		catch (Exception ex) { LogError(ex); }
	}
	internal static void PrebuildSettings() { if (settingsForm != null && !settingsForm.IsDisposed) return; prebuilding = true; try { ShowSettings(); } finally { prebuilding = false; } }
	// 한도·하루 시간에 따라 건강을 챙기는 한마디
	internal static string CareText(DateTime d, out int level)
	{
		try { return Care.Message(d, Records.CountMonth(d.Year, d.Month), Records.CountOn(d), monthlyLimit, out level, Records.CountYear(d.Year)); }
		catch { level = 0; return "수고하셨어요. 오늘도 고생 많았어요."; }
	}
	internal static void SetAccent(int idx) { Theme.AccentIdx = idx; SetTheme(Theme.Mode); }
	internal static void SetStyle(int idx) { Theme.Style = idx; SetTheme(Theme.Mode); }
	internal static void SetTheme(int mode)
	{
		AlertPopup.DropSpare(); AlertPopup.PrebuildLater(4000);
		Anim.Mark("테마 변경");
		Form old = settingsForm;
		try
		{
			// 숨겨 둔 설정창은 색이 옛것이라 없애고, 다음에 열 때 새 색으로 만들어요
			if (old != null && !old.IsDisposed && !old.Visible) { SettingsForm.HideOnClose = false; try { old.Close(); } finally { SettingsForm.HideOnClose = true; } Theme.Apply(mode); SaveSettings(); return; }
			// 앱 창 안에서 부드럽게 겹쳐 넘기는 방식 (덮개 창 없음)
			if (old != null && !old.IsDisposed && old.Visible && old.WindowState == FormWindowState.Normal && Anim.Effects && themeTrans != null) { themeTrans(mode); return; }
			if (old != null && !old.IsDisposed && old.Visible && old.WindowState == FormWindowState.Normal && Anim.Effects && LowPower)
			{
				// 느린 PC에서는 사진·덮개 없이 바로 바꿔요 (덮개를 만드는 시간까지 줄여서 한 번만 짧게 멈춰요)
				ThemeFx.Apply(old, mode); SaveSettings(); old.Invalidate(true); old.Update();
				return;
			}
			if (old == null || old.IsDisposed || !old.Visible || old.WindowState != FormWindowState.Normal || !Anim.Effects)
			{
				Theme.Apply(mode); SaveSettings();
				if (old != null && !old.IsDisposed) { themePage = settingsPage != null ? settingsPage() : 0; SettingsForm.HideOnClose = false; try { old.Close(); } finally { SettingsForm.HideOnClose = true; } ShowSettings(); }
				return;
			}
			{
				// 1) 옛 화면을 사진으로 떠서 위에 얹고  2) 그 밑에서 색을 한 번에 갈아 끼운 뒤  3) 사진을 서서히 걷어내요 (프레임마다 다시 그리지 않아서 부드러워요)
				Rectangle rr = old.Bounds;
				Bitmap sn = (shotProvider != null ? shotProvider(rr) : null) ?? Grab.Screen(rr);
				ThemeVeil tv = new ThemeVeil(sn, rr); tv.Show(); tv.Refresh();
				// 덮개(옛 화면 사진)부터 띄우고, 색 교체는 다음 차례에 해요: 클릭하자마자 반응이 보이고, 멈칫하는 부분은 움직임 시작 전에 끝나요
				old.BeginInvoke((MethodInvoker)delegate
				{
					try
					{
						Stopwatch thSw = Stopwatch.StartNew();
						ThemeFx.Apply(old, mode); SaveSettings();
						old.Invalidate(true); old.Update();
						NoteSlow((int)thSw.ElapsedMilliseconds + 40, 100);
						Anim.Run(380, Ease.InOutCubic, delegate(float k) { if (!tv.IsDisposed) tv.Opacity = 1.0 - k * 0.999; }, delegate { if (!tv.IsDisposed) tv.Dispose(); Mem.Later(3000); });
						System.Windows.Forms.Timer tg2 = new System.Windows.Forms.Timer(); tg2.Interval = 1500;
						tg2.Tick += delegate { tg2.Stop(); tg2.Dispose(); if (!tv.IsDisposed) tv.Dispose(); };
						tg2.Start();
					}
					catch (Exception ex3) { LogError(ex3); }
				});
				return;
			}
			Rectangle r = old.Bounds;
			Bitmap snap = new Bitmap(r.Width, r.Height);
			using (Graphics g = Graphics.FromImage(snap)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
			themeSnap = snap; themeRect = r; themePage = settingsPage != null ? settingsPage() : 0;
			Theme.Apply(mode); SaveSettings();
			themeVeil = new ThemeVeil(snap, r); themeVeil.Show();
			old.Close();
			ShowSettings();
		}
		catch (Exception ex) { LogError(ex); if (themeVeil != null) { try { themeVeil.Dispose(); } catch { } themeVeil = null; } themeSnap = null; }
	}
	private static void RestartWith(Action change)
	{
		try
		{
			change();
			SaveSettings();
			string exe = Application.ExecutablePath;
			ProcessStartInfo psi = new ProcessStartInfo(exe); psi.UseShellExecute = false;   // 새 프로그램이 뜨면 먼저 켜진 쪽은 알아서 정리돼요
			if (tray != null) tray.Visible = false;
			FlushSettings(); try { Process.Start(psi); } catch { } Application.Exit();
		}
		catch (Exception ex) { LogError(ex); }
	}
}

// 화면 색을 바꿀 때 옛 화면 사진을 잠깐 얹어 두는 창 (클릭은 그대로 통과)
internal sealed class ThemeVeil : Form
{
	protected override bool ShowWithoutActivation { get { return true; } }
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x20 | 0x80; return cp; } }
	private Bitmap bmp;
	public ThemeVeil(Bitmap b, Rectangle r)
	{
		bmp = b; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
		SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
		Bounds = r;
	}
	protected override void OnPaint(PaintEventArgs e) { e.Graphics.DrawImageUnscaled(bmp, 0, 0); }
	protected override void Dispose(bool d) { base.Dispose(d); if (d && bmp != null) { bmp.Dispose(); bmp = null; } }
}
