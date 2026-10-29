using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// 운세 · 타로 창을 설정창과 같은 모양(테두리 없는 창 + 최소화/닫기 버튼 + 테마 색)으로 맞춰 줘요
internal static class Chrome
{
	[DllImport("user32.dll")] private static extern bool ReleaseCapture();
	[DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int m, int w, int l);
	public static void Drag(Form f) { try { ReleaseCapture(); SendMessage(f.Handle, 0xA1, 2, 0); } catch { } }
	public static void Attach(Form f)
	{
		f.FormBorderStyle = FormBorderStyle.None; f.BackColor = Theme.Page;
		WinBtn min = new WinBtn(false), close = new WinBtn(true); min.BackColor = Theme.Page; close.BackColor = Theme.Page;
		int top = Dpi.Z(8);
		close.Location = new Point(f.ClientSize.Width - close.Width - Dpi.Z(10), top); min.Location = new Point(close.Left - min.Width - Dpi.Z(4), top);
		close.Click += delegate { f.Close(); }; min.Click += delegate { f.WindowState = FormWindowState.Minimized; };
		f.Controls.Add(min); f.Controls.Add(close);
	}
	// 운세 ↔ 타로 전환 알약 (두 창 공통)
	public static readonly RectangleF HubA = new RectangleF(220, 11, 50, 24), HubB = new RectangleF(272, 11, 50, 24);
	public static void Hub(Graphics g, bool tarot, Color on, Color off, Color txtOn, Color txtOff)
	{
		for (int i = 0; i < 2; i++)
		{
			RectangleF r = i == 0 ? HubA : HubB; bool act = (i == 1) == tarot;
			using (GraphicsPath gp = Gfx.Round(r, 12)) using (SolidBrush b = new SolidBrush(act ? on : off)) g.FillPath(b, gp);
			FUi.Text(g, i == 0 ? "운세" : "타로", FUi.F(9.5f, 1), act ? txtOn : txtOff, r, StringAlignment.Center, StringAlignment.Center, false);
		}
	}
	public static int HubHit(float x, float y) { return HubA.Contains(x, y) ? 1 : HubB.Contains(x, y) ? 2 : 0; }
	public static void Frame(Graphics g, float w, float h)
	{
		g.Clear(Theme.Page);
		using (Pen p = new Pen(Theme.LineStrong, 1.2f)) g.DrawRectangle(p, 0.6f, 0.6f, w - 1.4f, h - 1.4f);
	}
}
