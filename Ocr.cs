using System;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// 그림 속 글자 읽기: 윈도우에 들어 있는 글자 인식(Windows.Media.Ocr)을 PowerShell 로 불러 써요 (추가 설치·인터넷 필요 없음)
internal static class Ocr
{
	private static int busy;
	public static bool Busy { get { return busy != 0; } }
	private const string Script =
"param([string]$p,[string]$o)\r\n" +
"$ErrorActionPreference='Stop'\r\n" +
"try {\r\n" +
"Add-Type -AssemblyName System.Runtime.WindowsRuntime\r\n" +
"$as=([System.WindowsRuntimeSystemExtensions].GetMethods()|?{$_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'})[0]\r\n" +
"function W($op,[Type]$t){$k=$as.MakeGenericMethod($t).Invoke($null,@($op));$k.Wait(-1)|Out-Null;$k.Result}\r\n" +
"$null=[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]\r\n" +
"$null=[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]\r\n" +
"$null=[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics,ContentType=WindowsRuntime]\r\n" +
"$null=[Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime]\r\n" +
"$e=$null\r\n" +
"try{$e=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage((New-Object Windows.Globalization.Language 'ko'))}catch{}\r\n" +
"if($e -eq $null){$e=[Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()}\r\n" +
"if($e -eq $null){[IO.File]::WriteAllText($o,'#NOENGINE');exit 2}\r\n" +
"$n=0\r\n" +
"foreach($pp in ($p -split '\\|')){\r\n" +
"$f=W ([Windows.Storage.StorageFile]::GetFileFromPathAsync($pp)) ([Windows.Storage.StorageFile])\r\n" +
"$s=W ($f.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])\r\n" +
"$d=W ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($s)) ([Windows.Graphics.Imaging.BitmapDecoder])\r\n" +
"$b=W ($d.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])\r\n" +
"$r=W ($e.RecognizeAsync($b)) ([Windows.Media.Ocr.OcrResult])\r\n" +
"$sb=New-Object Text.StringBuilder\r\n" +
"foreach($l in $r.Lines){$t='';$px=$null\r\n" +
" foreach($w in $l.Words){$bx=$w.BoundingRect\r\n" +
"  if($px -ne $null){$gap=$bx.X-$px;$h=[Math]::Max(4,$bx.Height);$a=[int]$w.Text[0];$hg=($a -ge 0xAC00 -and $a -le 0xD7A3)\r\n" +
"   if(-not $hg -or $gap -gt $h*0.2){$t+=' '}}\r\n" +
"  $t+=$w.Text;$px=$bx.X+$bx.Width}\r\n" +
" [void]$sb.AppendLine($t)}\r\n" +
"$of=$o; if($n -gt 0){$of=$o+'.'+$n}\r\n" +
"[IO.File]::WriteAllText($of,$sb.ToString(),(New-Object Text.UTF8Encoding $false))\r\n" +
"$n++}\r\n" +
"} catch { [IO.File]::WriteAllText($o,'#ERR '+$_.Exception.Message,(New-Object Text.UTF8Encoding $false)); exit 1 }\r\n";

	// 글자를 읽어서 done(글자, 오류문구) 를 처음 부른 창의 스레드에서 불러 줘요. 그림은 복사해서 쓰니 원본은 바로 버려도 돼요
	public static bool Run(Bitmap src, Action<string, string> done)
	{
		if (src == null || Interlocked.Exchange(ref busy, 1) == 1) return false;
		SynchronizationContext ctx = SynchronizationContext.Current;
		List<Bitmap> work;
		try { work = Prep(src); } catch (Exception ex) { busy = 0; Post(ctx, done, null, "그림을 준비하지 못했어요 (" + ex.Message + ")"); return true; }
		Thread t = new Thread(delegate()
		{
			string text = null, err = null;
			string dir = Path.Combine(Path.GetTempPath(), "ot_ocr"); string png = null, outp = null; List<string> pngs = new List<string>();
			try
			{
				if (Environment.OSVersion.Platform != PlatformID.Win32NT) { err = "윈도우에서만 쓸 수 있어요"; return; }
				Directory.CreateDirectory(dir);
				string ps1 = Path.Combine(dir, "ocr3.ps1");
				if (!File.Exists(ps1) || File.ReadAllText(ps1) != Script) File.WriteAllText(ps1, Script, new UTF8Encoding(true));
				string id = Guid.NewGuid().ToString("N").Substring(0, 8);
				outp = Path.Combine(dir, "c" + id + ".txt");
				for (int vi = 0; vi < work.Count; vi++) { string pp = Path.Combine(dir, "c" + id + "_" + vi + ".png"); work[vi].Save(pp, ImageFormat.Png); pngs.Add(pp); work[vi].Dispose(); }
				work.Clear(); png = string.Join("|", pngs.ToArray());
				// 1차: 파워셸 없이 프로그램 안에서 바로 윈도우 글자 인식을 불러요 (회사 PC처럼 파워셸이 막힌 곳에서도 돼요)
				string nerr = null; List<string> nat = null;
				try { nat = Native(pngs, out nerr); } catch (Exception nex) { nerr = nex.Message; }
				if (nat != null) { string bt = null; double bsc = -1; foreach (string tx in nat) { double sc2 = Score(tx); if (sc2 > bsc * 1.03 || bt == null) { bsc = sc2; bt = tx; } } text = Tidy(bt ?? ""); return; }
				if (nerr == "NOENGINE") { err = "윈도우에 글자 인식 언어가 없어요\n설정 › 시간 및 언어 › 언어에서 '한국어'를 추가해 주세요"; return; }
				ProcessStartInfo si = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + ps1 + "\" \"" + png + "\" \"" + outp + "\"");
				si.CreateNoWindow = true; si.UseShellExecute = false; si.WindowStyle = ProcessWindowStyle.Hidden;
				using (Process p = Process.Start(si))
				{
					if (!p.WaitForExit(45000)) { try { p.Kill(); } catch { } err = "글자 인식이 너무 오래 걸려서 멈췄어요"; return; }
				}
				string r = File.Exists(outp) ? File.ReadAllText(outp, Encoding.UTF8) : null;
				if (r == null) err = "글자 인식을 실행하지 못했어요";
				else if (r.StartsWith("#NOENGINE")) err = "윈도우에 글자 인식 언어가 없어요\n설정 › 시간 및 언어 › 언어에서 '한국어'를 추가해 주세요";
				else if (r.StartsWith("#ERR")) err = "글자 인식 오류: " + r.Substring(4).Trim();
				else text = Tidy(Best(r, outp, pngs.Count));
			}
			catch (Exception ex) { err = "글자 인식을 실행하지 못했어요 (" + ex.Message + ")"; }
			finally
			{
				if (work != null) foreach (Bitmap wb in work) wb.Dispose();
				foreach (string pp in pngs) try { File.Delete(pp); } catch { } try { if (outp != null) { File.Delete(outp); for (int vi = 1; vi < 4; vi++) File.Delete(outp + "." + vi); } } catch { }
				busy = 0; Post(ctx, done, text, err);
			}
		});
		t.IsBackground = true; t.Start(); return true;
	}
	// ── 파워셸 없이 쓰는 방법: 윈도우 글자 인식(WinRT)을 리플렉션으로 직접 불러요. 안 되면 null 을 돌려주고 파워셸 방식으로 넘어가요
	private static object Wait(MethodInfo asTask, object op, Type result)
	{
		object task = asTask.MakeGenericMethod(result).Invoke(null, new object[] { op });
		task.GetType().GetMethod("Wait", Type.EmptyTypes).Invoke(task, null);
		return task.GetType().GetProperty("Result").GetValue(task, null);
	}
	private static Type WT(string name) { return Type.GetType(name + ", Windows.Foundation, ContentType=WindowsRuntime"); }
	private static object Prop(object o, string n)
	{
		PropertyInfo p = o.GetType().GetProperty(n); if (p != null) return p.GetValue(o, null);
		FieldInfo f = o.GetType().GetField(n); if (f != null) return f.GetValue(o); throw new MissingMemberException(n);
	}
	private static List<string> Native(List<string> pngs, out string err)
	{
		err = null; if (Environment.OSVersion.Platform != PlatformID.Win32NT) return null;
		Assembly sr = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
		Type ext = sr.GetType("System.WindowsRuntimeSystemExtensions"); MethodInfo asTask = null;
		foreach (MethodInfo m in ext.GetMethods()) { ParameterInfo[] ps = m.GetParameters(); if (m.Name == "AsTask" && ps.Length == 1 && ps[0].ParameterType.Name == "IAsyncOperation`1") { asTask = m; break; } }
		if (asTask == null) { err = "AsTask"; return null; }
		Type tEng = WT("Windows.Media.Ocr.OcrEngine"), tLang = WT("Windows.Globalization.Language"), tFile = WT("Windows.Storage.StorageFile"), tStream = WT("Windows.Storage.Streams.IRandomAccessStream"), tDec = WT("Windows.Graphics.Imaging.BitmapDecoder"), tBmp = WT("Windows.Graphics.Imaging.SoftwareBitmap"), tRes = WT("Windows.Media.Ocr.OcrResult"), tAcc = WT("Windows.Storage.FileAccessMode");
		if (tEng == null || tLang == null || tFile == null || tStream == null || tDec == null || tBmp == null || tRes == null || tAcc == null) { err = "TYPES"; return null; }
		object eng = null;
		try { object lang = Activator.CreateInstance(tLang, new object[] { "ko" }); eng = tEng.GetMethod("TryCreateFromLanguage").Invoke(null, new object[] { lang }); } catch { }
		if (eng == null) eng = tEng.GetMethod("TryCreateFromUserProfileLanguages").Invoke(null, null);
		if (eng == null) { err = "NOENGINE"; return null; }
		MethodInfo getFile = tFile.GetMethod("GetFileFromPathAsync"), create = null, openAsync = tFile.GetMethod("OpenAsync", new Type[] { tAcc }), getBmp = tDec.GetMethod("GetSoftwareBitmapAsync", Type.EmptyTypes), recog = tEng.GetMethod("RecognizeAsync");
		foreach (MethodInfo m in tDec.GetMethods()) if (m.Name == "CreateAsync" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == tStream) { create = m; break; }
		if (getFile == null || create == null || openAsync == null || getBmp == null || recog == null) { err = "METHODS"; return null; }
		List<string> outs = new List<string>(); object read = Enum.ToObject(tAcc, 0);
		foreach (string path in pngs)
		{
			object file = Wait(asTask, getFile.Invoke(null, new object[] { path }), tFile);
			object stream = Wait(asTask, openAsync.Invoke(file, new object[] { read }), tStream);
			object dec = Wait(asTask, create.Invoke(null, new object[] { stream }), tDec);
			object bmp = Wait(asTask, getBmp.Invoke(dec, null), tBmp);
			object res = Wait(asTask, recog.Invoke(eng, new object[] { bmp }), tRes);
			StringBuilder sb = new StringBuilder();
			foreach (object line in (System.Collections.IEnumerable)Prop(res, "Lines"))
			{
				StringBuilder t = new StringBuilder(); bool has = false; double px = 0;
				foreach (object w in (System.Collections.IEnumerable)Prop(line, "Words"))
				{
					string wt = (string)Prop(w, "Text"); object r = Prop(w, "BoundingRect"); double bx = Convert.ToDouble(Prop(r, "X")), bw = Convert.ToDouble(Prop(r, "Width")), bh = Convert.ToDouble(Prop(r, "Height"));
					if (has && wt.Length > 0) { double gap = bx - px, h = Math.Max(4.0, bh); bool hg = wt[0] >= 0xAC00 && wt[0] <= 0xD7A3; if (!hg || gap > h * 0.2) t.Append(' '); }
					t.Append(wt); px = bx + bw; has = true;
				}
				sb.AppendLine(t.ToString());
			}
			outs.Add(sb.ToString());
			try { IDisposable d = stream as IDisposable; if (d != null) d.Dispose(); } catch { }
		}
		return outs;
	}
	private static void Post(SynchronizationContext ctx, Action<string, string> done, string text, string err)
	{
		if (done == null) return;
		if (ctx != null) ctx.Post(delegate { try { done(text, err); } catch { } }, null); else try { done(text, err); } catch { }
	}
	// 인식 전에 그림을 다듬어요: 작은 글씨는 키우고 · 대비를 펴고 · 어두운 바탕은 뒤집고 · 살짝 선명하게. 두 가지(부드러운 글레이/흑백)로 만들어 더 잘 읽힌 쪽을 골라요
	private static List<Bitmap> Prep(Bitmap src)
	{
		int w = src.Width, h = src.Height; float k = 1f; int mx = Math.Max(w, h);
		if (mx < 1600) k = Math.Min(3f, 1600f / mx); if (k < 1.15f) k = 1f;
		if (mx * k > 3600f) k = 3600f / mx;
		int nw = Math.Max(1, (int)(w * k)) + 24, nh = Math.Max(1, (int)(h * k)) + 24;
		Bitmap col = new Bitmap(nw, nh, PixelFormat.Format24bppRgb);
		using (Graphics g = Graphics.FromImage(col))
		{
			g.Clear(src.GetPixel(0, 0));
			g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.DrawImage(src, new Rectangle(12, 12, nw - 24, nh - 24), 0, 0, w, h, GraphicsUnit.Pixel);
		}
		List<Bitmap> L = new List<Bitmap>();
		byte[] gray = new byte[nw * nh];
		BitmapData bd = col.LockBits(new Rectangle(0, 0, nw, nh), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
		try { byte[] row = new byte[Math.Abs(bd.Stride)]; for (int y = 0; y < nh; y++) { System.Runtime.InteropServices.Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), row, 0, nw * 3); for (int x = 0; x < nw; x++) gray[y * nw + x] = (byte)((row[x * 3] * 29 + row[x * 3 + 1] * 150 + row[x * 3 + 2] * 77) >> 8); } }
		finally { col.UnlockBits(bd); }
		col.Dispose();
		// 대비 늘리기(밝기 1%~99%) · 어두운 바탕이면 뒤집기
		int[] hist = new int[256]; foreach (byte v in gray) hist[v]++; int tot = gray.Length, lo = 0, hi = 255, acc = 0;
		for (int q = 0; q < 256; q++) { acc += hist[q]; if (acc >= tot * 0.01) { lo = q; break; } } acc = 0; for (int q = 255; q >= 0; q--) { acc += hist[q]; if (acc >= tot * 0.01) { hi = q; break; } }
		if (hi - lo < 24) { lo = Math.Max(0, lo - 12); hi = Math.Min(255, hi + 12); }
		long sum = 0; for (int q = 0; q < 256; q++) sum += (long)q * hist[q]; bool inv = sum / (double)tot < 118.0;
		byte[] lut = new byte[256]; for (int q = 0; q < 256; q++) { float u = Math.Max(0f, Math.Min(1f, (q - lo) / (float)Math.Max(1, hi - lo))); lut[q] = (byte)((inv ? 1f - u : u) * 255f); }
		for (int q = 0; q < gray.Length; q++) gray[q] = lut[gray[q]];
		// 살짝 선명하게 (언샤프)
		byte[] sh = new byte[gray.Length]; Buffer.BlockCopy(gray, 0, sh, 0, gray.Length);
		for (int y = 1; y < nh - 1; y++) for (int x = 1; x < nw - 1; x++) { int p = y * nw + x; int bl = (gray[p - 1] + gray[p + 1] + gray[p - nw] + gray[p + nw] + gray[p] * 4) >> 3; int v = gray[p] + (int)((gray[p] - bl) * 0.9f); sh[p] = (byte)(v < 0 ? 0 : (v > 255 ? 255 : v)); }
		L.Add(FromGray(sh, nw, nh));
		// 흑백 변형: 주변 평균보다 어두우면 글자로 (그늘진 바탕·그라데이션에 강해요)
		if ((long)nw * nh <= 7000000L)
		{
			int win = Math.Max(15, Math.Min(nw, nh) / 18) | 1, r = win / 2; long[] ii = new long[(nw + 1) * (nh + 1)];
			for (int y = 0; y < nh; y++) { long rs = 0; for (int x = 0; x < nw; x++) { rs += sh[y * nw + x]; ii[(y + 1) * (nw + 1) + x + 1] = ii[y * (nw + 1) + x + 1] + rs; } }
			byte[] bw = new byte[gray.Length];
			for (int y = 0; y < nh; y++) { int y0 = Math.Max(0, y - r), y1 = Math.Min(nh, y + r + 1); for (int x = 0; x < nw; x++) { int x0 = Math.Max(0, x - r), x1 = Math.Min(nw, x + r + 1); long sm = ii[y1 * (nw + 1) + x1] - ii[y0 * (nw + 1) + x1] - ii[y1 * (nw + 1) + x0] + ii[y0 * (nw + 1) + x0]; float mean = sm / (float)((y1 - y0) * (x1 - x0)); bw[y * nw + x] = (byte)(sh[y * nw + x] < mean * 0.9f ? 0 : 255); } }
			L.Add(FromGray(bw, nw, nh));
		}
		return L;
	}
	private static Bitmap FromGray(byte[] g, int w, int h)
	{
		Bitmap b = new Bitmap(w, h, PixelFormat.Format24bppRgb); BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
		try { byte[] row = new byte[Math.Abs(bd.Stride)]; for (int y = 0; y < h; y++) { for (int x = 0; x < w; x++) { byte v = g[y * w + x]; row[x * 3] = v; row[x * 3 + 1] = v; row[x * 3 + 2] = v; } System.Runtime.InteropServices.Marshal.Copy(row, 0, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), w * 3); } }
		finally { b.UnlockBits(bd); }
		return b;
	}
	// 읽힌 글 중 점수가 높은 쪽 고르기: 한글·영문·숫자가 많고, 엉뚱한 기호가 적을수록 좋아요
	private static string Best(string first, string outp, int n)
	{
		string best = first; double bs = Score(first);
		for (int i = 1; i < n; i++) { try { string f = outp + "." + i; if (!File.Exists(f)) continue; string t = File.ReadAllText(f, Encoding.UTF8); double sc = Score(t); if (sc > bs * 1.03) { bs = sc; best = t; } } catch { } }
		return best;
	}
	private static double Score(string t)
	{
		if (string.IsNullOrEmpty(t)) return 0; double sc = 0; const string odd = "|¦[]{}~^`\\<>=_*#@";
		foreach (char c in t) { if ((c >= 0xAC00 && c <= 0xD7A3) || char.IsLetterOrDigit(c)) sc += 1.0; else if (odd.IndexOf(c) >= 0) sc -= 1.5; else if (c == '\uFFFD') sc -= 3.0; }
		return Math.Max(0, sc);
	}
	// 줄 끝 공백 정리 · 빈 줄 여러 개는 하나로
	internal static string Tidy(string s)
	{
		string[] ls = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'); StringBuilder sb = new StringBuilder(); bool blank = false;
		foreach (string l0 in ls) { string l = l0.TrimEnd(); if (l.Length == 0) { blank = true; continue; } if (sb.Length > 0) sb.Append(blank ? "\r\n\r\n" : "\r\n"); sb.Append(l); blank = false; }
		return sb.ToString();
	}
}
