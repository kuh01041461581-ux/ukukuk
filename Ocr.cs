using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
"$f=W ([Windows.Storage.StorageFile]::GetFileFromPathAsync($p)) ([Windows.Storage.StorageFile])\r\n" +
"$s=W ($f.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])\r\n" +
"$d=W ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($s)) ([Windows.Graphics.Imaging.BitmapDecoder])\r\n" +
"$b=W ($d.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])\r\n" +
"$e=$null\r\n" +
"try{$e=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage((New-Object Windows.Globalization.Language 'ko'))}catch{}\r\n" +
"if($e -eq $null){$e=[Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()}\r\n" +
"if($e -eq $null){[IO.File]::WriteAllText($o,'#NOENGINE');exit 2}\r\n" +
"$r=W ($e.RecognizeAsync($b)) ([Windows.Media.Ocr.OcrResult])\r\n" +
"$sb=New-Object Text.StringBuilder\r\n" +
"foreach($l in $r.Lines){$t='';$px=$null\r\n" +
" foreach($w in $l.Words){$bx=$w.BoundingRect\r\n" +
"  if($px -ne $null){$gap=$bx.X-$px;$h=[Math]::Max(4,$bx.Height);$a=[int]$w.Text[0];$hg=($a -ge 0xAC00 -and $a -le 0xD7A3)\r\n" +
"   if(-not $hg -or $gap -gt $h*0.2){$t+=' '}}\r\n" +
"  $t+=$w.Text;$px=$bx.X+$bx.Width}\r\n" +
" [void]$sb.AppendLine($t)}\r\n" +
"[IO.File]::WriteAllText($o,$sb.ToString(),(New-Object Text.UTF8Encoding $false))\r\n" +
"} catch { [IO.File]::WriteAllText($o,'#ERR '+$_.Exception.Message,(New-Object Text.UTF8Encoding $false)); exit 1 }\r\n";

	// 글자를 읽어서 done(글자, 오류문구) 를 처음 부른 창의 스레드에서 불러 줘요. 그림은 복사해서 쓰니 원본은 바로 버려도 돼요
	public static bool Run(Bitmap src, Action<string, string> done)
	{
		if (src == null || Interlocked.Exchange(ref busy, 1) == 1) return false;
		SynchronizationContext ctx = SynchronizationContext.Current;
		Bitmap work;
		try { work = Prep(src); } catch (Exception ex) { busy = 0; Post(ctx, done, null, "그림을 준비하지 못했어요 (" + ex.Message + ")"); return true; }
		Thread t = new Thread(delegate()
		{
			string text = null, err = null;
			string dir = Path.Combine(Path.GetTempPath(), "ot_ocr"); string png = null, outp = null;
			try
			{
				if (Environment.OSVersion.Platform != PlatformID.Win32NT) { err = "윈도우에서만 쓸 수 있어요"; return; }
				Directory.CreateDirectory(dir);
				string ps1 = Path.Combine(dir, "ocr2.ps1");
				if (!File.Exists(ps1) || File.ReadAllText(ps1) != Script) File.WriteAllText(ps1, Script, new UTF8Encoding(true));
				string id = Guid.NewGuid().ToString("N").Substring(0, 8);
				png = Path.Combine(dir, "c" + id + ".png"); outp = Path.Combine(dir, "c" + id + ".txt");
				work.Save(png, ImageFormat.Png); work.Dispose(); work = null;
				ProcessStartInfo si = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + ps1 + "\" \"" + png + "\" \"" + outp + "\"");
				si.CreateNoWindow = true; si.UseShellExecute = false; si.WindowStyle = ProcessWindowStyle.Hidden;
				using (Process p = Process.Start(si))
				{
					if (!p.WaitForExit(25000)) { try { p.Kill(); } catch { } err = "글자 인식이 너무 오래 걸려서 멈췄어요"; return; }
				}
				string r = File.Exists(outp) ? File.ReadAllText(outp, Encoding.UTF8) : null;
				if (r == null) err = "글자 인식을 실행하지 못했어요";
				else if (r.StartsWith("#NOENGINE")) err = "윈도우에 글자 인식 언어가 없어요\n설정 › 시간 및 언어 › 언어에서 '한국어'를 추가해 주세요";
				else if (r.StartsWith("#ERR")) err = "글자 인식 오류: " + r.Substring(4).Trim();
				else text = Tidy(r);
			}
			catch (Exception ex) { err = "글자 인식을 실행하지 못했어요 (" + ex.Message + ")"; }
			finally
			{
				if (work != null) work.Dispose();
				try { if (png != null) File.Delete(png); } catch { } try { if (outp != null) File.Delete(outp); } catch { }
				busy = 0; Post(ctx, done, text, err);
			}
		});
		t.IsBackground = true; t.Start(); return true;
	}
	private static void Post(SynchronizationContext ctx, Action<string, string> done, string text, string err)
	{
		if (done == null) return;
		if (ctx != null) ctx.Post(delegate { try { done(text, err); } catch { } }, null); else try { done(text, err); } catch { }
	}
	// 작은 글씨는 키워서(최대 약 2배) 더 잘 읽히게, 큰 그림은 줄여서 빠르게
	private static Bitmap Prep(Bitmap src)
	{
		int w = src.Width, h = src.Height; float k = 1f;
		if (Math.Max(w, h) < 1300) k = Math.Min(2.2f, 1300f / Math.Max(w, h)); if (k < 1.2f) k = 1f;
		if (Math.Max(w, h) * k > 3600f) k = 3600f / Math.Max(w, h);
		int nw = Math.Max(1, (int)(w * k)), nh = Math.Max(1, (int)(h * k));
		Bitmap b = new Bitmap(nw + 16, nh + 16, PixelFormat.Format24bppRgb);
		using (Graphics g = Graphics.FromImage(b))
		{
			Color edge = src.GetPixel(0, 0); g.Clear(edge);   // 가장자리 글자가 잘리지 않게 바탕색으로 살짝 여백
			g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.DrawImage(src, new Rectangle(8, 8, nw, nh), 0, 0, w, h, GraphicsUnit.Pixel);
		}
		return b;
	}
	// 줄 끝 공백 정리 · 빈 줄 여러 개는 하나로
	internal static string Tidy(string s)
	{
		string[] ls = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'); StringBuilder sb = new StringBuilder(); bool blank = false;
		foreach (string l0 in ls) { string l = l0.TrimEnd(); if (l.Length == 0) { blank = true; continue; } if (sb.Length > 0) sb.Append(blank ? "\r\n\r\n" : "\r\n"); sb.Append(l); blank = false; }
		return sb.ToString();
	}
}
