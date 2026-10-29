using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ── QR 코드 생성기 (바이트 모드 · 오류정정 M · 버전 1~10) ──
internal static class Qr
{
	private static readonly int[] EccPer = { 0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
	private static readonly int[] Blocks = { 0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 };

	private static int RawModules(int v)
	{
		int r = (16 * v + 128) * v + 64;
		if (v >= 2) { int n = v / 7 + 2; r -= (25 * n - 10) * n - 55; if (v >= 7) r -= 36; }
		return r;
	}

	public static bool[,] Encode(string text)
	{
		byte[] data = Encoding.UTF8.GetBytes(text);
		int ver = 1;
		for (; ; ver++)
		{
			if (ver > 10) throw new ArgumentException("too long");
			int cap = RawModules(ver) / 8 - EccPer[ver] * Blocks[ver];
			int cb = ver <= 9 ? 8 : 16;
			if (4 + cb + data.Length * 8 <= cap * 8) break;
		}
		int dataCw = RawModules(ver) / 8 - EccPer[ver] * Blocks[ver];
		List<bool> bits = new List<bool>();
		Action<int, int> put = delegate(int val, int len) { for (int i = len - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0); };
		put(4, 4); put(data.Length, ver <= 9 ? 8 : 16);
		foreach (byte b in data) put(b, 8);
		put(0, Math.Min(4, dataCw * 8 - bits.Count));
		while (bits.Count % 8 != 0) bits.Add(false);
		byte[] dc = new byte[dataCw];
		for (int i = 0; i < bits.Count; i++) if (bits[i]) dc[i >> 3] |= (byte)(1 << (7 - (i & 7)));
		for (int i = bits.Count / 8, pad = 0xEC; i < dataCw; i++, pad ^= 0xEC ^ 0x11) dc[i] = (byte)pad;
		byte[] all = AddEcc(dc, ver);

		int size = ver * 4 + 17;
		bool[,] m = new bool[size, size]; bool[,] fn = new bool[size, size];
		DrawPatterns(m, fn, ver, size);
		Place(m, fn, all, size);
		int best = 0; int bestPen = int.MaxValue;
		for (int mask = 0; mask < 8; mask++)
		{
			Mask(m, fn, mask, size); DrawFormat(m, fn, mask, size);
			int pen = Penalty(m, size);
			if (pen < bestPen) { bestPen = pen; best = mask; }
			Mask(m, fn, mask, size);
		}
		Mask(m, fn, best, size); DrawFormat(m, fn, best, size);
		return m;
	}

	private static byte[] AddEcc(byte[] data, int ver)
	{
		int nb = Blocks[ver], eccLen = EccPer[ver], raw = RawModules(ver) / 8;
		int nShort = nb - raw % nb, shortLen = raw / nb;
		byte[] div = RsDiv(eccLen);
		byte[][] blocks = new byte[nb][];
		for (int i = 0, k = 0; i < nb; i++)
		{
			int len = shortLen - eccLen + (i < nShort ? 0 : 1);
			byte[] dat = new byte[len]; Array.Copy(data, k, dat, 0, len); k += len;
			byte[] block = new byte[shortLen + 1]; Array.Copy(dat, block, len);
			byte[] ecc = RsRem(dat, div);
			Array.Copy(ecc, 0, block, block.Length - eccLen, eccLen);
			blocks[i] = block;
		}
		byte[] res = new byte[raw]; int p = 0;
		for (int i = 0; i < blocks[0].Length; i++)
			for (int j = 0; j < nb; j++)
				if (i != shortLen - eccLen || j >= nShort) res[p++] = blocks[j][i];
		return res;
	}

	private static byte[] RsDiv(int deg)
	{
		byte[] r = new byte[deg]; r[deg - 1] = 1; int root = 1;
		for (int i = 0; i < deg; i++)
		{
			for (int j = 0; j < deg; j++) { r[j] = (byte)Mul(r[j], root); if (j + 1 < deg) r[j] ^= r[j + 1]; }
			root = Mul(root, 2);
		}
		return r;
	}
	private static byte[] RsRem(byte[] data, byte[] div)
	{
		byte[] r = new byte[div.Length];
		foreach (byte b in data)
		{
			int f = b ^ r[0];
			Array.Copy(r, 1, r, 0, r.Length - 1); r[r.Length - 1] = 0;
			for (int i = 0; i < r.Length; i++) r[i] ^= (byte)Mul(div[i], f);
		}
		return r;
	}
	private static int Mul(int x, int y)
	{
		int z = 0;
		for (int i = 7; i >= 0; i--) { z = (z << 1) ^ ((z >> 7) * 0x11D); z ^= ((y >> i) & 1) * x; }
		return z;
	}

	private static void Set(bool[,] m, bool[,] fn, int x, int y, bool d) { m[y, x] = d; fn[y, x] = true; }

	private static void DrawPatterns(bool[,] m, bool[,] fn, int ver, int size)
	{
		for (int i = 0; i < size; i++) { Set(m, fn, 6, i, i % 2 == 0); Set(m, fn, i, 6, i % 2 == 0); }
		int[] fx = { 3, size - 4, 3 }, fy = { 3, 3, size - 4 };
		for (int f = 0; f < 3; f++)
			for (int dy = -4; dy <= 4; dy++)
				for (int dx = -4; dx <= 4; dx++)
				{
					int x = fx[f] + dx, y = fy[f] + dy;
					if (x < 0 || y < 0 || x >= size || y >= size) continue;
					int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
					Set(m, fn, x, y, dist != 2 && dist != 4);
				}
		if (ver >= 2)
		{
			int n = ver / 7 + 2;
			int step = (ver * 4 + n * 2 + 1) / (n * 2 - 2) * 2;
			int[] pos = new int[n]; pos[0] = 6;
			for (int i = n - 1, p = size - 7; i >= 1; i--, p -= step) pos[i] = p;
			for (int a = 0; a < n; a++)
				for (int b = 0; b < n; b++)
				{
					if ((a == 0 && b == 0) || (a == 0 && b == n - 1) || (a == n - 1 && b == 0)) continue;
					for (int dy = -2; dy <= 2; dy++)
						for (int dx = -2; dx <= 2; dx++)
							Set(m, fn, pos[a] + dx, pos[b] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
				}
		}
		DrawFormat(m, fn, 0, size);
		if (ver >= 7)
		{
			int rem = ver;
			for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
			int bits = ver << 12 | rem;
			for (int i = 0; i < 18; i++)
			{
				bool bit = ((bits >> i) & 1) != 0; int a = size - 11 + i % 3, b = i / 3;
				Set(m, fn, a, b, bit); Set(m, fn, b, a, bit);
			}
		}
	}

	private static void DrawFormat(bool[,] m, bool[,] fn, int mask, int size)
	{
		int data = mask;   // 오류정정 M = 00
		int rem = data;
		for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
		int bits = (data << 10 | rem) ^ 0x5412;
		for (int i = 0; i <= 5; i++) Set(m, fn, 8, i, ((bits >> i) & 1) != 0);
		Set(m, fn, 8, 7, ((bits >> 6) & 1) != 0); Set(m, fn, 8, 8, ((bits >> 7) & 1) != 0); Set(m, fn, 7, 8, ((bits >> 8) & 1) != 0);
		for (int i = 9; i < 15; i++) Set(m, fn, 14 - i, 8, ((bits >> i) & 1) != 0);
		for (int i = 0; i < 8; i++) Set(m, fn, size - 1 - i, 8, ((bits >> i) & 1) != 0);
		for (int i = 8; i < 15; i++) Set(m, fn, 8, size - 15 + i, ((bits >> i) & 1) != 0);
		Set(m, fn, 8, size - 8, true);
	}

	private static void Place(bool[,] m, bool[,] fn, byte[] data, int size)
	{
		int i = 0;
		for (int right = size - 1; right >= 1; right -= 2)
		{
			if (right == 6) right = 5;
			for (int vert = 0; vert < size; vert++)
				for (int j = 0; j < 2; j++)
				{
					int x = right - j; bool up = ((right + 1) & 2) == 0; int y = up ? size - 1 - vert : vert;
					if (!fn[y, x] && i < data.Length * 8) { m[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0; i++; }
				}
		}
	}

	private static void Mask(bool[,] m, bool[,] fn, int mask, int size)
	{
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				if (fn[y, x]) continue;
				bool inv;
				switch (mask)
				{
					case 0: inv = (x + y) % 2 == 0; break;
					case 1: inv = y % 2 == 0; break;
					case 2: inv = x % 3 == 0; break;
					case 3: inv = (x + y) % 3 == 0; break;
					case 4: inv = (x / 3 + y / 2) % 2 == 0; break;
					case 5: inv = x * y % 2 + x * y % 3 == 0; break;
					case 6: inv = (x * y % 2 + x * y % 3) % 2 == 0; break;
					default: inv = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
				}
				if (inv) m[y, x] = !m[y, x];
			}
	}

	private static int Penalty(bool[,] m, int size)
	{
		int pen = 0;
		for (int pass = 0; pass < 2; pass++)
			for (int a = 0; a < size; a++)
			{
				int run = 1;
				for (int b = 1; b < size; b++)
				{
					bool cur = pass == 0 ? m[a, b] : m[b, a], prev = pass == 0 ? m[a, b - 1] : m[b - 1, a];
					if (cur == prev) { run++; if (run == 5) pen += 3; else if (run > 5) pen++; } else run = 1;
				}
			}
		for (int y = 0; y < size - 1; y++)
			for (int x = 0; x < size - 1; x++)
				if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1]) pen += 3;
		int dark = 0; foreach (bool b in m) if (b) dark++;
		int k = Math.Abs(dark * 20 - size * size * 10) / (size * size);
		return pen + k * 10;
	}
}

internal class QrBox : Control
{
	private bool[,] mod;
	public QrBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.White; }
	public void SetText(string t)
	{
		try { mod = Qr.Encode(t); } catch { mod = null; }
		Invalidate();
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; g.Clear(Color.White);
		if (mod == null) return;
		int n = mod.GetLength(0), quiet = 2;
		int cell = Math.Max(1, Math.Min(Width, Height) / (n + quiet * 2));
		int total = cell * (n + quiet * 2);
		int ox = (Width - total) / 2 + quiet * cell, oy = (Height - total) / 2 + quiet * cell;
		g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.Half;
		using (SolidBrush b = new SolidBrush(Color.FromArgb(24, 24, 22)))
			for (int y = 0; y < n; y++)
				for (int x = 0; x < n; x++)
					if (mod[y, x]) g.FillRectangle(b, ox + x * cell, oy + y * cell, cell, cell);
	}
}

// ── 휴대폰 푸시 (ntfy.sh) ──
internal static class Push
{
	public const string Server = "https://ntfy.sh";

	public static string NewTopic()
	{
		const string abc = "abcdefghijklmnopqrstuvwxyz0123456789";
		byte[] r = new byte[14];
		using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(r);
		StringBuilder sb = new StringBuilder("maryong-");
		foreach (byte b in r) sb.Append(abc[b % abc.Length]);
		return sb.ToString();
	}

	private static string Esc(string s)
	{
		StringBuilder sb = new StringBuilder();
		foreach (char c in s)
		{
			if (c == '"') sb.Append("\\\""); else if (c == '\\') sb.Append("\\\\");
			else if (c < 32) sb.Append(' '); else sb.Append(c);
		}
		return sb.ToString();
	}

	private static byte[] mascotPng;
	private static byte[] Mascot()
	{
		if (mascotPng != null) return mascotPng;
		try
		{
			using (System.IO.Stream st = typeof(Push).Assembly.GetManifestResourceStream("mascot.png"))
			{
				if (st == null) return null;
				System.IO.MemoryStream ms = new System.IO.MemoryStream(); byte[] buf = new byte[8192]; int n;
				while ((n = st.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
				mascotPng = ms.ToArray();
			}
		}
		catch { mascotPng = null; }
		return mascotPng;
	}

	// 헤더 값은 ASCII만 가능해서 RFC 2047로 인코딩 (ntfy가 해석해 줌)
	private static string Hdr(string s) { return "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(s)) + "?="; }

	// 백그라운드로 전송. 마룡이 이미지를 첨부해 보내고, 실패하면 글자만 다시 시도. 결과 콜백은 선택
	public static void Send(string topic, string title, string message, int priority, Action<bool, string> done)
	{
		if (string.IsNullOrEmpty(topic)) { if (done != null) done(false, "주제가 없어요"); return; }
		Thread t = new Thread(delegate()
		{
			bool ok = false; string msg = "";
			try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192); } catch { }
			byte[] img = Mascot();
			if (img != null)
			{
				try
				{
					using (WebClient wc = new TWebClient())
					{
						wc.Headers["Title"] = Hdr(title);
						wc.Headers["Message"] = Hdr(message);
						wc.Headers["Priority"] = priority.ToString();
						wc.Headers["Filename"] = "maryong.png";
						wc.UploadData(Server + "/" + topic, "PUT", img);
					}
					ok = true; msg = "보냈어요";
				}
				catch { ok = false; }
			}
			if (!ok)
			{
				try
				{
					string json = "{\"topic\":\"" + Esc(topic) + "\",\"title\":\"" + Esc(title) + "\",\"message\":\"" + Esc(message) +
						"\",\"priority\":" + priority + "}";
					using (WebClient wc = new TWebClient())
					{
						wc.Encoding = Encoding.UTF8;
						wc.Headers[HttpRequestHeader.ContentType] = "application/json; charset=utf-8";
						wc.UploadString(Server + "/", "POST", json);
					}
					ok = true; msg = "보냈어요";
				}
				catch { msg = "전송 실패 - 인터넷 연결을 확인하세요"; }
			}
			if (done != null) { try { done(ok, msg); } catch { } }
		});
		t.IsBackground = true; t.Start();
	}
}

// 시간 제한이 있는 WebClient (네트워크가 멈춰도 스레드가 영영 안 끝나는 일 방지)
internal class TWebClient : WebClient
{
	protected override WebRequest GetWebRequest(Uri address)
	{
		WebRequest r = base.GetWebRequest(address);
		if (r != null) r.Timeout = 12000;
		return r;
	}
}
