using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// 자동 업데이트: GitHub Releases의 최신 릴리스(tag_name · .exe 첨부)를 보고, 더 새 버전이면 받아서 교체한 뒤 다시 켜요.
// 배포 방법: 저장소에 새 Release를 만들고 태그를 ot-v1.4.1 처럼 붙인 뒤 OvertimeReminder.exe 를 첨부하면 끝. (같은 저장소의 다른 앱 릴리스와 섞여도 이 앱 것만 골라요)
internal partial class Program
{
	internal const string AppVersion = "1.4.59";
	// ← 본인 GitHub 저장소로 바꾸세요 (예: "uk/overtime-reminder"). 'OWNER'가 들어 있으면 업데이트 확인을 하지 않아요.
	private const string UpdateRepo = "kuh01041461581-ux/ukukuk";
	private static int updBusy; private static MenuItem updItem;
	private static string UpdDir { get { return Path.Combine(Dir, "update"); } }
	private static string UpdRepo
	{
		get
		{   // 테스트·재배포용: %AppData%\OvertimeReminder\update_repo.txt 에 owner/repo 한 줄을 적으면 그쪽을 써요
			try { string f = Path.Combine(Dir, "update_repo.txt"); if (File.Exists(f)) { string s = File.ReadAllText(f).Trim(); if (s.Contains("/")) return s; } } catch { }
			return UpdateRepo;
		}
	}
	internal static void UpdateInit()
	{
		try
		{
			try { Directory.CreateDirectory(UpdDir); foreach (string f in Directory.GetFiles(UpdDir)) { if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1)) File.Delete(f); } } catch { }
			System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 45000;
			t.Tick += delegate { t.Interval = 6 * 3600 * 1000; UpdateCheck(false); };
			t.Start();
		}
		catch { }
	}
	internal static void UpdateCheck(bool manual)
	{
		if (UpdRepo.Contains("OWNER")) { if (manual) MessageBox.Show("업데이트 저장소가 아직 정해지지 않았어요.\r\n(Updater.cs의 UpdateRepo 또는 update_repo.txt)", "초과근무 알리미"); return; }
		if (Interlocked.Exchange(ref updBusy, 1) == 1) return;
		SynchronizationContext ctx = SynchronizationContext.Current; string repo = UpdRepo;
		Thread th = new Thread(delegate()
		{
			try
			{
				ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192;
				string json = "";
				try { using (WebClient wc = NewWc()) { wc.Headers["Accept"] = "application/vnd.github+json"; json = wc.DownloadString("https://api.github.com/repos/" + repo + "/releases?per_page=30"); } UpdLog("확인: api.github.com 응답 " + json.Length + "자"); }
				catch (Exception ex1)
				{   // api.github.com 이 막힌 네트워크(회사·관공서)에서는 github.com 의 릴리스 목록(atom)으로 대신 확인해요
					UpdLog("api.github.com 실패: " + Why(ex1));
					try
					{
						string atom; using (WebClient wc = NewWc()) atom = wc.DownloadString("https://github.com/" + repo + "/releases.atom"); StringBuilder fs = new StringBuilder();
						foreach (Match am in Regex.Matches(atom, "/releases/tag/([^\"<]+)")) { string tg0 = Uri.UnescapeDataString(am.Groups[1].Value); if (tg0.StartsWith("ot-", StringComparison.OrdinalIgnoreCase)) fs.Append("{\"tag_name\":\"" + tg0 + "\",\"assets\":[{\"browser_download_url\":\"https://github.com/" + repo + "/releases/download/" + tg0 + "/OvertimeReminder.exe\"}]},"); }
						json = fs.ToString(); UpdLog("확인: releases.atom 으로 대신 확인");
					}
					catch (Exception ex2) { UpdLog("releases.atom 실패: " + Why(ex2)); throw new Exception("GitHub에 연결하지 못했어요 (" + Why(ex1) + ")"); }
				}
				// 같은 저장소에 다른 프로그램(데스크톱 캘린더 등) 릴리스가 섞여 있어도, 이 앱 것만 골라요:
				// 태그가 ot- 로 시작하거나(예: ot-v1.4.1) 첨부 파일 이름에 OvertimeReminder 가 들어간 가장 새 릴리스
				Match mt = Match.Empty, mu = Match.Empty; string seg = ""; Version bestV = null; MatchCollection tags = Regex.Matches(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
				for (int ti = 0; ti < tags.Count; ti++)
				{
					int a0 = tags[ti].Index, a1 = ti + 1 < tags.Count ? tags[ti + 1].Index : json.Length; string sg = json.Substring(a0, a1 - a0);
					if (Regex.IsMatch(sg, "\"(draft|prerelease)\"\\s*:\\s*true")) continue;
					// 같은 저장소의 다른 앱(달력 등)이 같은 릴리스에 exe를 붙여도, 이 앱 파일만 골라요
					string tg = tags[ti].Groups[1].Value; bool otTag = tg.StartsWith("ot-", StringComparison.OrdinalIgnoreCase); Match u2 = Match.Empty, other = Match.Empty;
					foreach (Match um in Regex.Matches(sg, "\"browser_download_url\"\\s*:\\s*\"([^\"]+?\\.exe)\""))
					{
						string fnm = um.Groups[1].Value; fnm = fnm.Substring(fnm.LastIndexOf('/') + 1);
						if (fnm.IndexOf("OvertimeReminder", StringComparison.OrdinalIgnoreCase) >= 0) { u2 = um; break; }
						if (otTag && !other.Success && !Regex.IsMatch(fnm, "Calendar|Seomu|Setup|Suite", RegexOptions.IgnoreCase)) other = um;
					}
					if (!u2.Success) u2 = other; if (!u2.Success) continue;
					// 태그에 버전 숫자가 없으면(예: 'overtime') 건너뛰고, 그중 가장 높은 버전을 골라요
					Version tv; if (!TryVer(tg, out tv)) continue;
					if (bestV != null && tv <= bestV) continue;
					bestV = tv; mt = tags[ti]; mu = u2; seg = sg;
				}
				if (seg.Length > 0) json = seg;
				if (!mt.Success || !mu.Success) { if (manual) Post(ctx, delegate { MessageBox.Show("이 앱의 릴리스를 찾지 못했어요.\r\n(태그가 ot-v1.4.1 처럼 ot- 로 시작하거나 OvertimeReminder.exe 가 첨부된 릴리스)", "초과근무 알리미"); }); return; }
				string tag = mt.Groups[1].Value, url = mu.Groups[1].Value.Replace("\\/", "/"); string body = ""; Match mb = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); if (mb.Success) body = Regex.Unescape(mb.Groups[1].Value);
				Version nv, cv; if (!TryVer(tag, out nv) || !TryVer(AppVersion, out cv)) return;
				if (nv <= cv) { if (manual) Post(ctx, delegate { MessageBox.Show("지금 최신 버전이에요 (v" + AppVersion + ")", "초과근무 알리미"); }); return; }
				string skip = ""; try { string sf = Path.Combine(Dir, "update.skip"); if (File.Exists(sf)) skip = File.ReadAllText(sf).Trim(); } catch { }
				if (!manual && skip == tag) return;
				Uri u = new Uri(url); if (u.Scheme != "https" || !(u.Host.EndsWith("github.com") || u.Host.EndsWith("githubusercontent.com"))) return;
				if (body.Length > 300) body = body.Substring(0, 300) + "…";
				Post(ctx, delegate { AskUpdate(tag, url, body, manual); });
			}
			catch (Exception ex) { UpdLog("확인 실패: " + Why(ex)); if (manual) Post(ctx, delegate { MessageBox.Show("업데이트를 확인하지 못했어요.\r\n" + ex.Message + "\r\n\r\n(자세한 원인: 폴더 %AppData%\\OvertimeReminder\\update.log)", "초과근무 알리미"); }); }
			finally { Interlocked.Exchange(ref updBusy, 0); }
		});
		th.IsBackground = true; th.Start();
	}
	private sealed class TWc : WebClient { protected override WebRequest GetWebRequest(Uri a) { WebRequest r = base.GetWebRequest(a); r.Timeout = 20000; return r; } }
	private static WebClient NewWc() { TWc w = new TWc(); w.Encoding = Encoding.UTF8; w.Headers["User-Agent"] = "OvertimeReminder-Updater"; try { IWebProxy p = WebRequest.GetSystemWebProxy(); p.Credentials = CredentialCache.DefaultCredentials; w.Proxy = p; } catch { } return w; }   // 회사 프록시(윈도우 로그인 계정으로 인증)도 쓰고, 20초 안에 응답이 없으면 포기해요
	private static string Why(Exception e) { string m = e.GetType().Name + ": " + e.Message; WebException we = e as WebException; if (we != null && we.Response is HttpWebResponse) m += " (HTTP " + (int)((HttpWebResponse)we.Response).StatusCode + ")"; if (e.InnerException != null) m += " / " + e.InnerException.Message; return m; }
	private static void UpdLog(string t) { try { Directory.CreateDirectory(Dir); string f = Path.Combine(Dir, "update.log"); if (File.Exists(f) && new FileInfo(f).Length > 60000) File.Delete(f); File.AppendAllText(f, DateTime.Now.ToString("MM-dd HH:mm:ss ") + t + "\r\n"); } catch { } }
	private static void Post(SynchronizationContext c, Action a) { if (c != null) c.Post(delegate { try { a(); } catch { } }, null); else a(); }
	private static bool TryVer(string s, out Version v) { s = (s ?? "").Trim(); if (s.StartsWith("ot-", StringComparison.OrdinalIgnoreCase)) s = s.Substring(3); s = s.TrimStart('v', 'V'); int i = 0; while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++; s = s.Substring(0, i).Trim('.'); if (s.IndexOf('.') < 0) s += ".0"; return Version.TryParse(s, out v); }
	private static string AutoPref() { try { string f = Path.Combine(Dir, "update.auto"); return File.Exists(f) ? File.ReadAllText(f).Trim() : ""; } catch { return ""; } }
	private static void SetAutoPref(string v) { try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, "update.auto"), v); } catch { } }
	private static void AskUpdate(string tag, string url, string body, bool manual)
	{
		if (!manual && AutoPref() == "1") { try { tray.ShowBalloonTip(3000, "초과근무 알리미", "새 버전 " + tag + " 을(를) 받아서 업데이트해요. 곧 다시 켜져요.", ToolTipIcon.Info); } catch { } DoUpdate(url); return; }
		DialogResult r = MessageBox.Show("새 버전 " + tag + " 이(가) 나왔어요 (지금 v" + AppVersion + ").\r\n" + (body.Length > 0 ? "\r\n" + body + "\r\n" : "") + "\r\n지금 받아서 업데이트할까요? (끝나면 자동으로 다시 켜져요)\r\n[아니요]를 누르면 이 버전은 다시 묻지 않아요.", "초과근무 알리미 업데이트", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
		if (r == DialogResult.Cancel) return;
		if (r == DialogResult.No) { try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, "update.skip"), tag); } catch { } return; }
		if (AutoPref() == "") { DialogResult ar = MessageBox.Show("앞으로 GitHub에 새 버전이 올라오면 물어보지 않고 자동으로 받아서 업데이트할까요?\r\n\r\n[예] 새 버전이 나오면 자동으로 받고 다시 켜요\r\n[아니요] 지금처럼 올라올 때마다 물어봐요\r\n\r\n(바꾸고 싶으면 %AppData%\\OvertimeReminder\\update.auto 파일을 지우면 다시 물어봐요)", "초과근무 알리미 업데이트", MessageBoxButtons.YesNo, MessageBoxIcon.Question); SetAutoPref(ar == DialogResult.Yes ? "1" : "0"); }
		DoUpdate(url);
	}
	private static void DoUpdate(string url)
	{
		SynchronizationContext ctx = SynchronizationContext.Current;
		Thread th = new Thread(delegate()
		{
			try
			{
				Directory.CreateDirectory(UpdDir); string nf = Path.Combine(UpdDir, "new.exe");
				ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192;
				using (WebClient wc = NewWc()) { wc.DownloadFile(url, nf); } UpdLog("내려받기 완료: " + url);
				FileInfo fi = new FileInfo(nf); bool ok = fi.Length > 1000000; if (ok) using (FileStream fs = File.OpenRead(nf)) ok = fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
				if (!ok) { Post(ctx, delegate { MessageBox.Show("받은 파일이 올바르지 않아 업데이트하지 않았어요.", "초과근무 알리미"); }); try { File.Delete(nf); } catch { } return; }
				string cur = Application.ExecutablePath, bat = Path.Combine(UpdDir, "apply.bat");
				string script = "@echo off\r\nchcp 65001 >nul\r\nset n=0\r\n:retry\r\nping 127.0.0.1 -n 2 >nul\r\nmove /y \"" + nf + "\" \"" + cur + "\" >nul 2>&1\r\nif errorlevel 1 (\r\nset /a n+=1\r\nif %n% LSS 30 goto retry\r\nexit /b 1\r\n)\r\nstart \"\" \"" + cur + "\"\r\n(goto) 2>nul & del \"%~f0\"\r\n";
				File.WriteAllText(bat, script, new UTF8Encoding(false));
				ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
				Process.Start(psi);
				Post(ctx, delegate { try { tray.Visible = false; FlushSettings(); } catch { } Application.Exit(); });
			}
			catch (Exception ex) { UpdLog("내려받기 실패: " + Why(ex)); Post(ctx, delegate { MessageBox.Show("업데이트를 받지 못했어요.\r\n" + ex.Message + "\r\n\r\n(자세한 원인: %AppData%\\OvertimeReminder\\update.log)", "초과근무 알리미"); }); }
		});
		th.IsBackground = true; th.Start();
	}
}
