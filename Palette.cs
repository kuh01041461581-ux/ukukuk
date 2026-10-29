using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// 기능 찾기: 단축키(기본 Alt+F12)로 검색창을 띄워 이름·초성(ㄱㅁㅎ)으로 기능을 찾아 바로 실행해요
internal sealed class PalCmd
{
	public string Name, Group, Keys, Hint; public int Hk = -1; public bool Hidden; public Action Run; public bool NeedsTarget;
	public string Norm, Cho;
}

internal partial class Program
{
	internal static void Note(string title, string text, int ms) { try { if (uiCtx != null) uiCtx.Post(delegate { Balloon(title, text, ms); }, null); else Balloon(title, text, ms); } catch { } }
	internal static Action toggleWidget;
	// 선택한 글을 바꾸는 기능을 검색창에서 실행 (원래 창으로 돌아간 뒤)
	internal static void RunTool(int id)
	{
		if (id == 3) { ToggleCharPad(); return; }
		if (id == 10) { ShotOverlay.Start(); return; }
		if (System.Threading.Interlocked.Exchange(ref toolBusy, 1) == 1) return;
		System.Threading.Thread t = new System.Threading.Thread(delegate() { try { if (id > 3) AdminTool(id); else ConvertSelection(id); } catch { } finally { System.Threading.Interlocked.Exchange(ref toolBusy, 0); } });
		t.IsBackground = true; t.Start();
	}
	private static void Page(int p) { ShowSettings(); if (settingsGo != null) try { settingsGo(p); } catch { } }
	internal static List<PalCmd> PaletteCommands()
	{
		List<PalCmd> L = new List<PalCmd>();
		Action<string, string, string, string, Action> add = delegate(string g, string n, string k, string h, Action a) { L.Add(new PalCmd { Group = g, Name = n, Keys = k, Hint = h, Run = a }); };
		Action<int, string, string, string> tool = delegate(int id, string n, string k, string h) { int ii = id; L.Add(new PalCmd { Group = "공문 도우미", Name = n, Keys = k, Hint = h, Hk = id - 1, NeedsTarget = id != 10 && id != 3, Run = delegate { RunTool(ii); } }); };
		tool(1, "금액 한글 변환", "숫자 돈 원 한글로 금 일금", "드래그한 금액 → 금 일십이만원정");
		tool(2, "날짜 공문서식", "날짜 일자 2026. 10. 29.", "드래그한 날짜 → 2026. 10. 29.");
		tool(3, "특수문자 팔레트", "기호 특수 문자 ※ ○ ㉠", "특수문자를 골라 넣어요");
		tool(4, "법령 바로 찾기", "법 법률 조문 시행령 국가법령정보", "드래그한 법령 이름을 찾아봐요");
		tool(5, "면적 변환 (㎡ ↔ 평)", "평수 제곱미터 넓이 면적", "드래그한 면적을 바꿔요");
		tool(6, "개인정보 가리기", "주민번호 전화번호 마스킹 별표 가림", "주민번호·전화번호를 ＊로 가려요");
		tool(7, "항목부호 자동 정리", "1. 가. 1) 가) 번호 항목 들여쓰기", "공문 항목 번호를 순서대로 맞춰요");
		tool(8, "금액 단위 변환", "천원 백만원 억원 단위", "드래그한 금액의 단위를 바꿔요");
		tool(9, "셀 합계", "엑셀 한글 표 더하기 합 계산", "고른 칸들의 숫자를 더해요");
		tool(10, "화면 캡처 · 플로팅", "스크린샷 캡쳐 띄우기 고정", "화면 일부를 떠 있는 창으로");
		add("공문 도우미", "그림 속 글자 복사", "OCR 글자 인식 텍스트 추출 캡처 읽기 이미지", "화면을 드래그하면 글자를 읽어 복사해요", delegate { ShotOverlay.Start(true); });
		L[L.Count - 1].NeedsTarget = false;
		add("공문 도우미", "캡처 창 모두 닫기", "캡처 플로팅 닫기 정리", "떠 있는 캡처를 모두 닫아요", delegate { ShotFloat.CloseAll(); });
		add("기록", "초과근무 기록 보기", "기록 내역 달력 시간외", "이번 달 기록과 달력", delegate { ShowHistory(); });
		add("기록", "지금 시간 알림 추가", "알림 지금 기록 추가", "지금 시각으로 기록 알림을 띄워요", delegate { AddAlertNow(); });
		add("기록", "오늘 요약 보기", "요약 카드 상태 오늘", "오늘 기록·다음 알림을 한눈에", delegate { ShowStatusToast(); });
		add("설정", "설정 열기", "홈 설정 창", "", delegate { Page(0); });
		add("설정", "알림 시간 설정", "알림 시간 정시 퇴근", "", delegate { Page(1); });
		add("설정", "휴일 설정", "휴일 공휴일 쉬는날 연가", "", delegate { Page(3); });
		add("설정", "휴대폰 알림", "폰 휴대폰 텔레그램 카톡 푸시", "", delegate { Page(5); });
		add("설정", "기한 관리", "마감 기한 할일 데드라인 일정", "", delegate { Page(6); });
		add("설정", "예상 수당 계산", "수당 시간외 급여 돈 시급", "", delegate { Page(8); });
		add("설정", "공문 도우미 설정 · 단축키", "단축키 핫키 키 바꾸기 도우미", "", delegate { Page(7); });
		add("설정", "금액 · 합계 설정", "단위 합계 누적", "", delegate { Page(10); });
		add("설정", "일반 설정", "시작 자동실행 화면 테마 소리 배율", "", delegate { Page(4); });
		add("설정", "사용법", "도움말 설명서 매뉴얼 안내", "", delegate { Page(9); });
		add("설정", "방해 금지 켜기 / 끄기", "방해금지 조용히 무음 회의", DndActive ? "지금 켜져 있어요" : "지금 꺼져 있어요", delegate { ToggleDnd(); });
		add("설정", "알림음 켜기 / 끄기", "소리 알림음 음소거", soundOn ? "지금 켜져 있어요" : "지금 꺼져 있어요", delegate { soundOn = !soundOn; if (soundItem != null) soundItem.Checked = soundOn; SaveSettings(); Balloon("알림음 " + (soundOn ? "켜짐" : "꺼짐"), soundOn ? "알림 때 소리가 나요." : "알림 때 소리가 나지 않아요.", 2500); });
		add("설정", "공항 시계 켜기 / 끄기", "시계 위젯 바탕화면", widgetOn ? "지금 켜져 있어요" : "지금 꺼져 있어요", delegate { if (toggleWidget != null) toggleWidget(); });
		add("설정", "업데이트 확인", "업데이트 새버전 버전 다운로드", "지금 v" + AppVersion, delegate { UpdateCheck(true); });
		add("쉬는 시간", "인사랑 열기", "인사랑 인사 복무 연가 출장", "", delegate { OpenInsarang(); });
		add("쉬는 시간", "오늘 뭐 먹지?", "점심 저녁 메뉴 식당 밥 맛집", "", delegate { SuggestMenu(); });
		add("쉬는 시간", "마룡이 운세 · 사주", "운세 오늘의운세 사주 띠 별자리", "", delegate { FortuneForm.Open(); });
		add("쉬는 시간", "마룡이 타로", "타로 카드 점 켈틱", "", delegate { TarotForm.Open(); });
		add("쉬는 시간", "로또 추첨기", "로또 번호 복권 추첨", "재미용", delegate { LottoForm.Open(); });
		add("쉬는 시간", "마룡이 펫 꺼내기 / 넣기", "펫 마룡이 캐릭터 바탕화면", "", delegate { PetWin.Toggle(); });
		add("쉬는 시간", "마룡이 돌보기", "다마고치 키우기 밥 먹이기 돌봄", "", delegate { RaiseForm.Open(); });
		// 숨은 기능은 이름을 다 쳐야만 나와요
		L.Add(new PalCmd { Group = "비밀", Name = "마룡이 오락실", Keys = "오락실", Hint = "쉿, 비밀 통로", Hidden = true, Run = delegate { MaryongGame.Open(); } });
		foreach (PalCmd c in L) { c.Norm = Pal.Norm(c.Name + " " + c.Keys + " " + c.Group); c.Cho = Pal.Cho(c.Name) + " " + Pal.Cho(c.Keys); }
		return L;
	}
}

internal static class Pal
{
	private static readonly char[] ChoTab = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ".ToCharArray();
	internal static string Norm(string s) { StringBuilder b = new StringBuilder(); foreach (char c in s.ToLowerInvariant()) if (!char.IsWhiteSpace(c) && c != '·' && c != '/' && c != '(' && c != ')') b.Append(c); return b.ToString(); }
	internal static string Cho(string s) { StringBuilder b = new StringBuilder(); foreach (char c in s) { if (c >= 0xAC00 && c <= 0xD7A3) b.Append(ChoTab[(c - 0xAC00) / 588]); else if (c == ' ') b.Append(' '); else if (char.IsLetterOrDigit(c)) b.Append(char.ToLowerInvariant(c)); } return b.ToString().Replace(" ", ""); }
	private static bool AllCho(string q) { if (q.Length == 0) return false; foreach (char c in q) if (Array.IndexOf(ChoTab, c) < 0) return false; return true; }
	// 점수가 클수록 위에: 이름 앞부분 일치 > 이름 포함 > 초성 > 키워드 · 글자 순서대로 흩어져 있어도 조금 점수
	internal static int Score(PalCmd c, string raw)
	{
		string q = Norm(raw); if (q.Length == 0) return c.Hidden ? -1 : 1;
		string nm = Norm(c.Name);
		if (c.Hidden) return nm == q || Norm(c.Keys) == q ? 900 : -1;
		if (nm.StartsWith(q)) return 1000 - nm.Length;
		int i = nm.IndexOf(q); if (i >= 0) return 800 - i;
		if (AllCho(q)) { string cn = Cho(c.Name); if (cn.StartsWith(q)) return 700; if (cn.IndexOf(q) >= 0) return 650; if (c.Cho.IndexOf(q) >= 0) return 500; }
		if (c.Norm.IndexOf(q) >= 0) return 400;
		int p = 0; foreach (char ch in c.Norm) if (p < q.Length && ch == q[p]) p++;
		return p == q.Length && q.Length >= 2 ? 100 : -1;
	}
}

internal sealed class PaletteForm : Form
{
	[DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
	[DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
	private static PaletteForm cur; private static readonly List<string> recent = new List<string>();
	private static string RecentPath { get { return Path.Combine(Program.DataDir, "palette_recent.txt"); } }
	private readonly List<PalCmd> all; private List<PalCmd> shown = new List<PalCmd>();
	private readonly TextBox box; private int sel, top, hov = -1; private readonly IntPtr prev; private bool closing;
	private readonly int rowH, headH, pad, maxRows, W;
	private bool Recent(PalCmd c) { return recent.Contains(c.Name); }

	public static void Toggle()
	{
		try
		{
			if (cur != null && !cur.IsDisposed) { cur.Close(); return; }
			cur = new PaletteForm(); cur.Show(); cur.Activate(); cur.box.Focus();
		}
		catch (Exception ex) { cur = null; Program.LogErr(ex); }
	}

	private PaletteForm()
	{
		try { prev = GetForegroundWindow(); } catch { }
		if (recent.Count == 0) try { if (File.Exists(RecentPath)) foreach (string l in File.ReadAllLines(RecentPath, Encoding.UTF8)) if (l.Trim().Length > 0 && recent.Count < 6) recent.Add(l.Trim()); } catch { }
		all = Program.PaletteCommands();
		FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; KeyPreview = true; Text = "기능 찾기";
		BackColor = Theme.Bg; DoubleBuffered = true;
		W = Dpi.Z(540); rowH = Dpi.Z(48); headH = Dpi.Z(62); pad = Dpi.Z(10); maxRows = 8;
		box = new TextBox(); box.BorderStyle = BorderStyle.None; box.Font = Fonts.Native(13f); box.BackColor = Theme.Raised; box.ForeColor = Theme.Ink;
		box.Location = new Point(Dpi.Z(52), Dpi.Z(20)); box.Width = W - Dpi.Z(52) - Dpi.Z(64); box.ImeMode = ImeMode.Hangul;
		box.TextChanged += delegate { Filter(); };
		box.KeyDown += OnBoxKey;
		Controls.Add(box);
		Filter();
		Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
		Location = new Point(wa.X + (wa.Width - W) / 2, wa.Y + wa.Height / 5);
	}
	protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80; cp.ClassStyle |= 0x20000; return cp; } }   // 작업 전환창에 안 나오게 · 그림자

	private void Filter()
	{
		string q = box.Text.Trim();
		List<KeyValuePair<int, PalCmd>> sc = new List<KeyValuePair<int, PalCmd>>(); int order = 0;
		foreach (PalCmd c in all)
		{
			int s = Pal.Score(c, q); if (s < 0) { order++; continue; }
			int ri = recent.IndexOf(c.Name); if (ri >= 0) s += q.Length == 0 ? 5000 - ri * 10 : 30;
			sc.Add(new KeyValuePair<int, PalCmd>(s * 1000 - order, c)); order++;
		}
		sc.Sort((a, b) => b.Key.CompareTo(a.Key));
		shown = new List<PalCmd>(); foreach (KeyValuePair<int, PalCmd> kv in sc) shown.Add(kv.Value);
		sel = 0; top = 0; hov = -1;
		int rows = Math.Max(1, Math.Min(maxRows, shown.Count));
		int h = headH + rows * rowH + pad + Dpi.Z(30);
		if (ClientSize.Height != h || ClientSize.Width != W) { ClientSize = new Size(W, h); try { using (GraphicsPath gp = Gfx.Round(new RectangleF(0, 0, W, h), Dpi.Zf(16))) { Region old = Region; Region = new Region(gp); if (old != null) old.Dispose(); } } catch { } }
		Invalidate();
	}
	private int ListTop { get { return headH; } }
	private Rectangle RowRect(int i) { return new Rectangle(pad, ListTop + (i - top) * rowH, W - pad * 2, rowH); }
	private void Ensure() { if (sel < top) top = sel; if (sel >= top + maxRows) top = sel - maxRows + 1; }

	private void OnBoxKey(object s, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Down) { if (shown.Count > 0) { sel = (sel + 1) % shown.Count; Ensure(); Invalidate(); } e.Handled = e.SuppressKeyPress = true; }
		else if (e.KeyCode == Keys.Up) { if (shown.Count > 0) { sel = (sel - 1 + shown.Count) % shown.Count; Ensure(); Invalidate(); } e.Handled = e.SuppressKeyPress = true; }
		else if (e.KeyCode == Keys.PageDown) { if (shown.Count > 0) { sel = Math.Min(shown.Count - 1, sel + maxRows); Ensure(); Invalidate(); } e.Handled = e.SuppressKeyPress = true; }
		else if (e.KeyCode == Keys.PageUp) { if (shown.Count > 0) { sel = Math.Max(0, sel - maxRows); Ensure(); Invalidate(); } e.Handled = e.SuppressKeyPress = true; }
		else if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; Exec(sel); }
		else if (e.KeyCode == Keys.Escape) { e.Handled = e.SuppressKeyPress = true; if (box.Text.Length > 0) box.Text = ""; else Close(); }
	}
	private void Exec(int i)
	{
		if (i < 0 || i >= shown.Count || closing) return;
		PalCmd c = shown[i]; closing = true;
		recent.Remove(c.Name); recent.Insert(0, c.Name); while (recent.Count > 6) recent.RemoveAt(recent.Count - 1);
		try { Directory.CreateDirectory(Program.DataDir); File.WriteAllLines(RecentPath, recent.ToArray(), new UTF8Encoding(false)); } catch { }
		bool back = c.NeedsTarget; IntPtr pv = prev; Action run = c.Run;
		Hide(); Close();
		if (back && pv != IntPtr.Zero) try { SetForegroundWindow(pv); } catch { }
		// 창이 사라지고(선택한 글이 있는 창으로 돌아간) 뒤에 실행
		Timer t = new Timer(); t.Interval = back ? 220 : 60; t.Tick += delegate { t.Stop(); t.Dispose(); try { run(); } catch (Exception ex) { Program.LogErr(ex); } }; t.Start();
	}
	protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (!closing) { closing = true; BeginInvoke((Action)Close); } }
	protected override void OnFormClosed(FormClosedEventArgs e) { if (cur == this) cur = null; base.OnFormClosed(e); }
	protected override void OnMouseMove(MouseEventArgs e)
	{
		int h = -1; for (int i = top; i < Math.Min(shown.Count, top + maxRows); i++) if (RowRect(i).Contains(e.Location)) h = i;
		if (h != hov) { hov = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
	}
	protected override void OnMouseLeave(EventArgs e) { if (hov != -1) { hov = -1; Invalidate(); } base.OnMouseLeave(e); }
	protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left && hov >= 0) Exec(hov); }
	protected override void OnMouseWheel(MouseEventArgs e)
	{
		int mx = Math.Max(0, shown.Count - maxRows); int nt = Math.Max(0, Math.Min(mx, top - Math.Sign(e.Delta) * 2));
		if (nt != top) { top = nt; if (sel < top) sel = top; if (sel >= top + maxRows) sel = top + maxRows - 1; Invalidate(); }
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Gfx.HQ(g); g.Clear(Theme.Bg);
		using (Pen p = new Pen(Theme.LineStrong, 1f)) using (GraphicsPath gp = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Dpi.Zf(16))) g.DrawPath(p, gp);
		// 검색칸
		RectangleF sb = new RectangleF(pad, Dpi.Z(10), W - pad * 2, Dpi.Z(42));
		Gfx.Fill(g, sb, Dpi.Zf(12), Theme.Raised);
		using (Pen p = new Pen(Theme.Accent, Dpi.Zf(1.6f))) using (GraphicsPath gp = Gfx.Round(sb, Dpi.Zf(12))) g.DrawPath(p, gp);
		float cx = Dpi.Zf(32), cy = sb.Y + sb.Height / 2f - Dpi.Zf(1), r = Dpi.Zf(7);
		using (Pen p = new Pen(Theme.Accent, Dpi.Zf(2f))) { g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2); g.DrawLine(p, cx + r * 0.7f, cy + r * 0.7f, cx + r * 1.5f, cy + r * 1.5f); }
		if (box.Text.Length == 0) Gfx.Text(g, "기능 이름이나 초성으로 찾기 (예: 캡처, ㄱㅇ)", Theme.F(11.5f, 0), Theme.Muted, new RectangleF(box.Left + 1, sb.Y, sb.Width - box.Left, sb.Height), StringAlignment.Near, StringAlignment.Center, false);
		Gfx.Text(g, "Esc", Theme.F(8.5f, 1), Theme.Muted, new RectangleF(sb.Right - Dpi.Z(52), sb.Y, Dpi.Z(44), sb.Height), StringAlignment.Far, StringAlignment.Center, false);
		if (shown.Count == 0) Gfx.Text(g, "찾는 기능이 없어요", Theme.F(10.5f, 0), Theme.Sub, new RectangleF(0, ListTop, W, rowH), StringAlignment.Center, StringAlignment.Center, false);
		string q = box.Text.Trim();
		for (int i = top; i < Math.Min(shown.Count, top + maxRows); i++)
		{
			Rectangle rr = RowRect(i); PalCmd c = shown[i]; bool on = i == sel;
			if (on) Gfx.Fill(g, new RectangleF(rr.X, rr.Y + 2, rr.Width, rr.Height - 4), Dpi.Zf(10), Theme.AccentSoft);
			else if (i == hov) Gfx.Fill(g, new RectangleF(rr.X, rr.Y + 2, rr.Width, rr.Height - 4), Dpi.Zf(10), Theme.Hover);
			// 묶음 표시 점
			Color gc = c.Group == "공문 도우미" ? Theme.Accent : c.Group == "기록" ? Theme.Blue : c.Group == "설정" ? Theme.Sub : Theme.Gold;
			using (SolidBrush b = new SolidBrush(gc)) g.FillEllipse(b, rr.X + Dpi.Z(12), rr.Y + rr.Height / 2f - Dpi.Z(4), Dpi.Z(8), Dpi.Z(8));
			string right = c.Hk >= 0 && c.Hk < Program.NHK ? Program.HotkeyText(c.Hk) : (q.Length == 0 && Recent(c) ? "최근" : c.Group);
			float rw = Dpi.Z(110);
			Gfx.Text(g, c.Name, Theme.F(10.5f, on ? 2 : 1), on ? Theme.Accent : Theme.Ink, new RectangleF(rr.X + Dpi.Z(30), rr.Y + Dpi.Z(5), rr.Width - Dpi.Z(30) - rw, Dpi.Z(22)), StringAlignment.Near, StringAlignment.Center, false);
			string hint = c.Hint; if (string.IsNullOrEmpty(hint)) hint = c.Group;
			Gfx.Text(g, hint, Theme.F(8.5f, 0), Theme.Muted, new RectangleF(rr.X + Dpi.Z(30), rr.Y + Dpi.Z(25), rr.Width - Dpi.Z(30) - rw, Dpi.Z(18)), StringAlignment.Near, StringAlignment.Center, false);
			if (c.Hk >= 0)
			{
				SizeF ks = g.MeasureString(right, Theme.F(8.5f, 1)); RectangleF kr = new RectangleF(rr.Right - ks.Width - Dpi.Z(26), rr.Y + rr.Height / 2f - Dpi.Z(11), ks.Width + Dpi.Z(14), Dpi.Z(22));
				Gfx.Fill(g, kr, Dpi.Zf(6), on ? Theme.Bg : Theme.Raised);
				using (Pen p = new Pen(Theme.Line, 1f)) using (GraphicsPath gp = Gfx.Round(kr, Dpi.Zf(6))) g.DrawPath(p, gp);
				Gfx.Text(g, right, Theme.F(8.5f, 1), Theme.Sub, kr, StringAlignment.Center, StringAlignment.Center, false);
			}
			else Gfx.Text(g, right, Theme.F(8.5f, 0), Theme.Muted, new RectangleF(rr.Right - rw, rr.Y, rw - Dpi.Z(14), rr.Height), StringAlignment.Far, StringAlignment.Center, false);
		}
		// 스크롤 위치 (목록이 길 때만, 얇고 부드러운 막대)
		if (shown.Count > maxRows)
		{
			float th = maxRows * rowH, tk = Math.Max(Dpi.Z(24), th * maxRows / shown.Count), y = ListTop + (th - tk) * top / Math.Max(1, shown.Count - maxRows);
			Gfx.Fill(g, new RectangleF(W - Dpi.Z(6), y + 2, Dpi.Z(3), tk - 4), Dpi.Zf(1.5f), Color.FromArgb(110, Theme.Sub));
		}
		Gfx.Text(g, "↑↓ 고르기 · Enter 실행 · 글자 바꾸기는 먼저 글을 드래그해 두세요", Theme.F(8.5f, 0), Theme.Muted, new RectangleF(pad + Dpi.Z(4), Height - Dpi.Z(30), W - pad * 2, Dpi.Z(24)), StringAlignment.Near, StringAlignment.Center, false);
	}
}
