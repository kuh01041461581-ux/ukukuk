using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

// 긴 그림 한 장을 A4 PDF 여러 쪽으로 나눠 저장 (추가 라이브러리 없이 · 쪽은 글줄 사이 빈 줄에서 잘라요)
internal static class Pdf
{
	public static void SaveTall(Bitmap tall, string path, string title)
	{
		int W = tall.Width; int pageH = (int)(W * 842.0 / 595.0 * 0.94);   // 여백을 빼고 한 쪽에 들어가는 높이
		List<int> cuts = new List<int>(); int y = 0;
		while (y < tall.Height)
		{
			int end = Math.Min(tall.Height, y + pageH);
			if (end < tall.Height) { int best = FindGap(tall, end, Math.Max(y + pageH / 2, end - pageH / 5)); if (best > y) end = best; }
			cuts.Add(y); y = end;
		}
		cuts.Add(tall.Height);
		List<byte[]> jpgs = new List<byte[]>(); List<int> hs = new List<int>();
		ImageCodecInfo jc = null; foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders()) if (c.MimeType == "image/jpeg") jc = c;
		using (EncoderParameters ep = new EncoderParameters(1))
		{
			ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
			for (int i = 0; i + 1 < cuts.Count; i++)
			{
				int h = cuts[i + 1] - cuts[i]; if (h <= 0) continue;
				using (Bitmap part = new Bitmap(W, h, PixelFormat.Format24bppRgb))
				{
					using (Graphics g = Graphics.FromImage(part)) { g.Clear(Color.White); g.DrawImage(tall, new Rectangle(0, 0, W, h), new Rectangle(0, cuts[i], W, h), GraphicsUnit.Pixel); }
					using (MemoryStream ms = new MemoryStream()) { if (jc != null) part.Save(ms, jc, ep); else part.Save(ms, ImageFormat.Jpeg); jpgs.Add(ms.ToArray()); hs.Add(h); }
				}
			}
		}
		Write(path, jpgs, hs, W, title);
	}
	// end 근처에서 위로 올라가며, 한 줄 전체가 거의 같은 색인 곳(글자 없는 틈)을 찾아요
	private static int FindGap(Bitmap b, int end, int minY)
	{
		BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			int stride = d.Stride; byte[] row = new byte[Math.Abs(stride)];
			int run = 0;
			for (int yy = end - 1; yy >= minY; yy--)
			{
				System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)yy * stride), row, 0, row.Length);
				int x0 = b.Width / 20, x1 = b.Width - x0; int r0 = row[x0 * 4 + 2], g0 = row[x0 * 4 + 1], b0 = row[x0 * 4]; bool flat = true;
				for (int x = x0; x < x1; x += 2) { int o = x * 4; if (Math.Abs(row[o + 2] - r0) + Math.Abs(row[o + 1] - g0) + Math.Abs(row[o] - b0) > 40) { flat = false; break; } }
				run = flat ? run + 1 : 0; if (run >= 6) return yy + 3;
			}
		}
		finally { b.UnlockBits(d); }
		return end;
	}
	private static void Write(string path, List<byte[]> jpgs, List<int> hs, int W, string title)
	{
		using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
		{
			List<long> off = new List<long>(); Action<string> w = s => { byte[] bb = Encoding.ASCII.GetBytes(s); fs.Write(bb, 0, bb.Length); };
			int n = jpgs.Count; // 객체: 1 카탈로그, 2 쪽 목록, 3 정보, 그 뒤 쪽마다 (쪽, 내용, 그림)
			w("%PDF-1.4\n%âãÏÓ\n");
			Action<int> obj = id => { while (off.Count < id) off.Add(0); off[id - 1] = fs.Position; w(id + " 0 obj\n"); };
			obj(1); w("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
			StringBuilder kids = new StringBuilder(); for (int i = 0; i < n; i++) kids.Append((4 + i * 3) + " 0 R ");
			obj(2); w("<< /Type /Pages /Kids [" + kids + "] /Count " + n + " >>\nendobj\n");
			obj(3); w("<< /Title <FEFF" + Hex(title) + "> /Producer (OvertimeReminder) >>\nendobj\n");
			for (int i = 0; i < n; i++)
			{
				int pid = 4 + i * 3, cid = pid + 1, iid = pid + 2;
				float mx = 595f * 0.03f, dw = 595f - mx * 2, dh = dw * hs[i] / W, top = 842f - mx;
				obj(pid); w("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im" + i + " " + iid + " 0 R >> >> /Contents " + cid + " 0 R >>\nendobj\n");
				string cs = "q " + F(dw) + " 0 0 " + F(dh) + " " + F(mx) + " " + F(top - dh) + " cm /Im" + i + " Do Q\n";
				obj(cid); w("<< /Length " + cs.Length + " >>\nstream\n" + cs + "endstream\nendobj\n");
				obj(iid); w("<< /Type /XObject /Subtype /Image /Width " + W + " /Height " + hs[i] + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpgs[i].Length + " >>\nstream\n");
				fs.Write(jpgs[i], 0, jpgs[i].Length); w("\nendstream\nendobj\n");
			}
			long xref = fs.Position; int cnt = off.Count + 1;
			w("xref\n0 " + cnt + "\n0000000000 65535 f \n"); foreach (long o in off) w(o.ToString("D10") + " 00000 n \n");
			w("trailer\n<< /Size " + cnt + " /Root 1 0 R /Info 3 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
		}
	}
	private static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
	private static string Hex(string s) { StringBuilder b = new StringBuilder(); foreach (char c in s ?? "") b.Append(((int)c).ToString("X4")); return b.ToString(); }
}
