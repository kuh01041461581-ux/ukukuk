using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// ── 타로 카드 (78장 · 마룡이 덱) ─ 섞기 → 펼치기 → 고르기 → 뒤집기 → 풀이 ─────────────────
internal sealed partial class TarotForm : Form
{
	private static TarotForm inst;
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.WindowState = FormWindowState.Normal; inst.Activate(); return; } inst = new TarotForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }

	// ───────── 카드 자료 ─────────
	private static readonly string[] KR = { "바보", "마법사", "여사제", "여황제", "황제", "교황", "연인", "전차", "힘", "은둔자", "운명의 수레바퀴", "정의", "매달린 사람", "죽음", "절제", "악마", "탑", "별", "달", "태양", "심판", "세계" };
	private static readonly string[] KW = { "새 출발", "재능·의지", "직관", "풍요", "책임·질서", "조언·배움", "선택·사랑", "전진", "인내", "성찰", "전환점", "공정", "관점 전환", "끝과 시작", "조화", "집착", "충격·변화", "희망", "불안", "성공·기쁨", "부활", "완성" };
	private static readonly string[] UP = {
		"새로운 시작과 모험이에요. 계산보다 용기가 필요해요.", "재능과 의지가 현실이 돼요. 지금 시작해 보세요.", "직관을 믿을 때예요. 서두르지 말고 지켜봐요.", "풍요와 사랑이 자라요. 돌보는 만큼 돌아와요.", "책임과 질서로 안정을 얻어요. 리더십이 빛나요.", "믿을 만한 조언과 전통이 도움 돼요. 배움의 때예요.",
		"설레는 만남과 중요한 선택이에요. 마음이 향하는 쪽으로!", "의지로 밀고 나가 승리해요. 방향만 정하세요.", "부드러운 용기가 이겨요. 인내가 큰 힘이 돼요.", "혼자 생각할 시간이 답을 줘요. 내면을 들여다봐요.", "흐름이 바뀌는 행운의 전환점이에요. 기회를 잡아요.", "공정한 결과와 올바른 판단이에요. 약속·계약에 길해요.",
		"잠시 멈추고 관점을 바꾸면 길이 보여요.", "끝은 새로운 시작이에요. 낡은 것을 정리해요.", "균형과 조화로 순조로워요. 꾸준한 페이스가 좋아요.", "유혹과 집착에 묶이기 쉬워요. 습관을 점검해요.", "예상 밖의 충격이 와요. 무너진 뒤 더 단단해져요.", "희망과 치유의 기운이에요. 소망이 천천히 이뤄져요.",
		"불안과 모호함이 커요. 확실해질 때까지 기다려요.", "성공과 기쁨이 가득해요. 무엇이든 밝게 풀려요.", "과거가 정리되고 새 기회가 열려요. 부름에 답해요.", "완성과 성취의 순간이에요. 한 단계 도약해요." };
	private static readonly string[] RV = {
		"무모함을 조심해요. 준비 없이 뛰어들면 헛걸음해요.", "말만 앞서거나 집중이 흩어져요. 하나에 몰입해요.", "속마음을 숨기고 있어요. 감춘 걱정을 꺼내 봐요.", "의존이나 낭비를 조심해요. 나 자신도 챙겨요.", "고집과 통제욕이 문제예요. 한 발 물러나요.", "틀에 갇혀 답답해요. 나만의 방식을 찾아요.",
		"엇갈림과 망설임이 있어요. 솔직한 대화가 필요해요.", "속도만 내면 탈선해요. 목표를 다시 점검해요.", "자신감이 흔들려요. 스스로를 다독여 주세요.", "고립은 금물이에요. 도움을 청해도 괜찮아요.", "타이밍이 어긋나요. 조급해 말고 때를 기다려요.", "치우친 판단이나 책임 회피를 조심해요.",
		"헛된 희생과 미루기를 멈추고 결정해요.", "변화가 두려워 붙잡고 있어요. 놓아주면 가벼워져요.", "과하거나 모자라요. 생활 리듬을 다시 맞춰요.", "속박에서 벗어나는 때예요. 나쁜 고리를 끊어요.", "위기를 간신히 피하거나 변화를 미루고 있어요.", "기운이 빠져 의욕이 낮아요. 작은 위안부터 찾아요.",
		"안개가 걷혀요. 숨은 진실이 드러나요.", "기쁨이 잠깐 가려졌어요. 곧 다시 밝아져요.", "자책과 미련이 길어져요. 결단을 내려요.", "마무리가 조금 모자라요. 끝까지 챙겨요." };
	private static readonly string[] Suit = { "완드", "컵", "검", "펜타클" }, SuitDom = { "일과 열정 쪽", "마음과 관계 쪽", "생각과 판단 쪽", "돈과 현실 쪽" }, SuitRes = { "wands", "cups", "swords", "pents" };
	private static readonly string[] RankName = { "에이스", "2", "3", "4", "5", "6", "7", "8", "9", "10", "시종", "기사", "여왕", "왕" };
	private static readonly string[] RankKw = { "새로운 시작", "균형·선택", "성장·협력", "안정·정체", "갈등·시련", "회복·나눔", "시험·인내", "움직임·몰두", "고비·성취 직전", "완성·무게", "소식·배움", "추진·행동", "성숙·돌봄", "통솔·완성" };
	private static readonly string[] RankUp = {
		"{d}에서 새로운 기회가 열려요. 망설이지 말고 시작해 보세요.", "{d}에서 두 갈래 중 하나를 고를 때예요. 균형을 잡으면 순조로워요.", "{d}이 자라나요. 함께하면 성과가 커져요.", "{d}이 안정돼요. 기반을 다지고 한숨 돌려요.", "{d}에서 다툼과 경쟁이 있어요. 부딪혀도 배울 게 있어요.",
		"{d}이 회복돼요. 주고받는 도움이 흐름을 살려요.", "{d}에서 시험대에 서요. 끝까지 버티면 지켜 내요.", "{d}이 빠르게 움직여요. 한 가지에 집중하면 결실이 와요.", "{d}에서 거의 다 왔어요. 마지막 고비만 넘기면 돼요.", "{d}에서 한 사이클이 완성돼요. 짐이 무거우면 나눠 들어요.",
		"{d}에서 반가운 소식이나 배울 기회가 와요.", "{d}에서 행동할 때예요. 빠르게 밀고 나가요.", "{d}을 따뜻하고 안정적으로 이끌어요. 직관과 배려가 힘이에요.", "{d}을 믿음직하게 이끌어요. 책임과 경험이 빛나요." };
	private static readonly string[] RankRv = {
		"{d}의 기회가 늦어지거나 막혀 있어요. 준비를 더 해요.", "{d}에서 망설임이 길어져요. 한쪽을 정하는 용기가 필요해요.", "{d}에서 손발이 맞지 않아요. 역할을 다시 나눠 봐요.", "{d}이 굳어서 답답해요. 작은 변화를 줘 봐요.", "{d}의 갈등이 풀려 가요. 화해와 타협이 길이에요.",
		"{d}에서 주고받는 게 한쪽으로 쏠려요. 기대를 점검해요.", "{d}에서 지치고 흔들려요. 힘을 아끼고 우선순위를 정해요.", "{d}이 더디거나 흩어져요. 속도를 조절해요.", "{d}에서 불안이 커요. 혼자 끌어안지 말고 도움을 청해요.", "{d}의 부담이 한계예요. 내려놓을 것을 정해요.",
		"{d}에서 서툰 실수가 보여요. 차분히 배워요.", "{d}에서 성급하거나 멈춰 있어요. 방향부터 점검해요.", "{d}에서 감정 기복과 지나친 간섭을 조심해요.", "{d}에서 고집이나 독단을 조심해요. 한 발 물러서요." };
	private static string CardName(int id) { if (id < 22) return KR[id]; int m = id - 22, s = m / 14, r = m % 14; return r >= 10 ? Suit[s] + " " + RankName[r] : Suit[s] + " " + RankName[r]; }
	private static string CardKw(int id) { return id < 22 ? KW[id] : RankKw[(id - 22) % 14]; }
	private static string Meaning(int id, bool rev) { if (id < 22) return rev ? RV[id] : UP[id]; return MinorUp[id - 22]; }
	private static string Body(int id, bool rev, int topic)
	{
		if (topic == 0) return Meaning(id, rev);
		if (id < 22) return Meaning(id, rev) + " ▶ " + MajTopic[id][topic - 1] + (rev ? " 다만 역방향이라 서두르지 말고 점검하며 가세요." : "");
		int m = id - 22, sidx = m / 14; bool rel = Array.IndexOf(TopicSuits[topic], sidx) >= 0;
		return (rel ? "● 이 주제와 직접 맞닿은 카드예요. " : "") + MinorUp[m] + " ▶ " + SuitTopic[sidx][topic - 1];
	}

	// ───────── 색 · 상태 ─────────
	private static Color PInk { get { return Theme.Ink; } } private static Color PSub { get { return Theme.Sub; } } private static Color PMut { get { return Theme.Muted; } } private static Color PRed { get { return Theme.AccentBtn; } } private static Color PCard { get { return Theme.Raised; } } private static Color PLine { get { return Theme.LineStrong; } } private static Color PSoft { get { return Theme.AccentSoft; } }
	private const int CW = 420, LH0 = 700; private float lw = 420f, lh = 700f, cmp = 1f; private int LW { get { return (int)Math.Ceiling(lw); } } private int LH { get { return (int)Math.Ceiling(lh); } } private float CDX { get { return (lw - CW) / 2f; } } private float CTX { get { return CDX + 210f * (1f - cmp); } }
	private PointF ColPt(float x, float y) { return new PointF((x - CTX) / cmp, y / cmp); } private const float CAR = 176f / 252f;
	private enum St { Idle, Shuffle, Pick, Read }
	private sealed class Slot { public int Card = -1; public bool Rev; public int State; public float T; public RectangleF From; public float FromRot; }
	private float sc = FUi.Scale(CW, LH0), ox, oy;
	private St st = St.Idle; private int mode = 1, topic; private bool full = true, spOpen; private string[] posN = PosNames(1, 0); private readonly RectangleF[] tR = new RectangleF[5], spRows = new RectangleF[12]; private RectangleF spR;
	private readonly List<int> rest = new List<int>(); private readonly bool[] revAll = new bool[78]; private Slot[] slots = new Slot[3]; private int nPick;
	private float scroll, spreadT, shufT, rscroll; private int hover = -1, zoom = -1; private float zoomT;
	private readonly RectangleF[] dR = new RectangleF[2], sR = new RectangleF[12]; private RectangleF btn;
	private Random R = new Random(Guid.NewGuid().GetHashCode());
	private readonly Stopwatch clock = Stopwatch.StartNew(); private double lastT; private readonly System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer { Interval = 16 };
	private Bitmap back, backSm; private readonly Bitmap[] pip = new Bitmap[4]; private readonly Dictionary<int, Bitmap> faces = new Dictionary<int, Bitmap>();
	private int edgeDir; private bool down, dragging; private float downX, downScroll;
	private readonly bool[] taken = new bool[78]; private float gw, gh, gx0, gy0, gGap = 3f; private int gCols = 13, tinyW; private Bitmap backTiny; private List<Ent> entsCache;

	private static Bitmap LoadRes(string n)
	{
		try { using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("tarot." + n)) { if (s == null) return null; MemoryStream ms = new MemoryStream(); s.CopyTo(ms); ms.Position = 0; using (Bitmap t = new Bitmap(ms)) return new Bitmap(t); } }
		catch { return null; }
	}
	private int SlotN { get { return SpreadN[mode]; } }
	private void SetMode(int m) { mode = m; posN = PosNames(m, 0); st = St.Idle; ResetSlots(); Sfx.Play("tick"); }
	private int DeckN { get { return full && mode != 0 ? 78 : 22; } }
	private float StripY { get { return 480f; } }
	private const float FW = 56f, FH = 80f;
	private float Sp { get { return full ? 17f : 29f; } }
	private float TotalW { get { return (rest.Count - 1) * Sp * spreadT + FW; } }
	private float MaxScroll { get { return Math.Max(0f, TotalW - (CW - 28f)); } }

	private TarotForm()
	{
		Text = "타로 카드"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(LW * sc), (int)(LH * sc)); DoubleBuffered = true; Chrome.Attach(this); MinimumSize = new Size((int)(LW * sc * 0.62f), (int)(LH * sc * 0.62f)); Resize += delegate { Relayout(); }; LoadTextMul();
		back = LoadRes("back.jpg"); if (back == null) { back = new Bitmap(176, 252); using (Graphics g = Graphics.FromImage(back)) g.Clear(Color.FromArgb(140, 40, 40)); }
		backSm = RoundedScaled(back, (int)(FW * sc * 1.2f), (int)(FH * sc * 1.2f));
		for (int i = 0; i < 4; i++) pip[i] = LoadRes("p_" + SuitRes[i] + ".png");
		ResetSlots(); tm.Tick += delegate { Step(); }; lastT = clock.Elapsed.TotalSeconds;
		FormClosed += delegate { tm.Stop(); tm.Dispose(); foreach (Bitmap b in faces.Values) b.Dispose(); faces.Clear(); if (back != null) back.Dispose(); if (backSm != null) backSm.Dispose(); foreach (Bitmap p in pip) if (p != null) p.Dispose(); };
		MouseDown += OnDown; MouseUp += OnUp; MouseMove += OnMove; MouseLeave += delegate { hover = -1; hoverSlot = -1; edgeDir = 0; Wake(); Invalidate(); };
		MouseWheel += delegate(object s, MouseEventArgs e) { if (hist) { FHist.Wheel(ref hScroll, e.Delta, LH, sc); Invalidate(); return; } if (st == St.Pick) { scroll = Math.Max(0, Math.Min(MaxScroll, scroll - e.Delta * 0.35f / sc * 1.0f)); Invalidate(); } else if (st == St.Read) { rsTarget = Math.Max(0, Math.Min(ReadMax(), rsTarget - e.Delta * 0.55f / sc)); Wake(); Invalidate(); } };
		KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape && zoom >= 0) { zoom = -1; Invalidate(); } else if (st == St.Pick && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)) { scroll = Math.Max(0, Math.Min(MaxScroll, scroll + (e.KeyCode == Keys.Left ? -60 : 60))); Invalidate(); } };
	}
	private void Wake() { if (!tm.Enabled) { lastT = clock.Elapsed.TotalSeconds; tm.Start(); } }
	private void ResetSlots() { slots = new Slot[SlotN]; for (int i = 0; i < slots.Length; i++) slots[i] = new Slot(); nPick = 0; zoom = -1; rscroll = 0; rsTarget = 0; LayoutSlots(); }

	// ───────── 흐름 ─────────
	private void StartShuffle()
	{
		ResetSlots(); rest.Clear(); for (int i = 0; i < DeckN; i++) rest.Add(i); for (int i = rest.Count - 1; i > 0; i--) { int j = R.Next(i + 1); int t = rest[i]; rest[i] = rest[j]; rest[j] = t; }
		for (int i = 0; i < 78; i++) revAll[i] = mode != 8 && i < 22 && R.Next(100) < 38; foreach (Bitmap b in faces.Values) b.Dispose(); faces.Clear();
		Array.Clear(taken, 0, taken.Length); entsCache = null; FxReset(); st = St.Shuffle; shufT = 0f; spreadT = 0f; scroll = 0f; Sfx.Play("tick"); Wake(); Invalidate();
	}
	private void Step()
	{
		double now = clock.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.05, now - lastT); lastT = now; bool busy = false;
		{ float tg = st == St.Read ? 0.72f : 1f; if (Math.Abs(cmp - tg) > 0.004f) { cmp += (tg - cmp) * Math.Min(1f, dt * 9f); busy = true; } else cmp = tg; }
		if (st == St.Shuffle) { shufT += dt / 2.0f; if (shufT >= 1f) { st = St.Pick; spreadT = 0f; Sfx.Play("ok"); } busy = true; }
		if (st == St.Pick && spreadT < 1f) { spreadT = Math.Min(1f, spreadT + dt / 0.5f); busy = true; }
				bool allDone = true;
		for (int i = 0; i < slots.Length; i++)
		{
			Slot s = slots[i]; if (s.State == 1) { s.T += dt / 0.45f; if (s.T >= 1f) { s.State = 2; s.T = 0f; Sfx.Play("tick"); } busy = true; }
			else if (s.State == 2) { s.T += dt / 0.55f; if (s.T >= 1f) { s.State = 3; s.T = 1f; } busy = true; }
			if (s.State != 3) allDone = false;
		}
		if (st == St.Pick && nPick == slots.Length && allDone) { entsCache = null; st = St.Read; rscroll = 0; rsTarget = 0; Sfx.Play("ok"); busy = true; RecordRead(); }
		if (zoom >= 0 && zoomT < 1f) { zoomT = Math.Min(1f, zoomT + dt / 0.2f); busy = true; }
		if (FxStep(dt)) busy = true;
		if (Math.Abs(rsTarget - rscroll) > 0.3f) { rscroll += (rsTarget - rscroll) * Math.Min(1f, dt * 14f); busy = true; } else rscroll = rsTarget;
		if (!busy) tm.Stop(); Invalidate();
	}
	private void LayoutGrid()
	{
		int n = Math.Max(1, rest.Count); float y0 = SlotsBottom + 40f, H = LH - 14f - y0, W = CW - 28f, bw = 0f; int bc = 13;
		for (int c = 4; c <= 26; c++) { int r = (n + c - 1) / c; float w = (W - (c - 1) * gGap) / c, h = w / CAR; if (r * h + (r - 1) * gGap <= H && w > bw) { bw = w; bc = c; } }
		if (bw <= 0f) { bc = 15; bw = (W - 14 * gGap) / 15f; }
		gCols = bc; gw = Math.Min(bw, 62f); gh = gw / CAR; int rows = (n + bc - 1) / bc; gx0 = (CW - (bc * gw + (bc - 1) * gGap)) / 2f; gy0 = y0 + Math.Max(0f, (H - (rows * gh + (rows - 1) * gGap)) / 3f);
		int tw = (int)Math.Round(gw * sc); if (backTiny == null || tinyW != tw) { if (backTiny != null) backTiny.Dispose(); backTiny = RoundedScaled(back, tw, (int)Math.Round(gh * sc)); tinyW = tw; }
	}
	private RectangleF GridRect(int i) { int c = i % gCols, r = i / gCols; return new RectangleF(gx0 + c * (gw + gGap), gy0 + r * (gh + gGap), gw, gh); }
	private int HitGrid(float x, float y)
	{
		if (st != St.Pick || spreadT < 1f || rest.Count == 0 || nPick >= slots.Length) return -1; int c = (int)Math.Floor((x - gx0 + gGap / 2f) / (gw + gGap)), r = (int)Math.Floor((y - gy0 + gGap / 2f) / (gh + gGap));
		if (c < 0 || c >= gCols || r < 0) return -1; int i = r * gCols + c; return i < rest.Count && !taken[i] ? i : -1;
	}
	private void OnMove(object s, MouseEventArgs e)
	{
		float rx = (e.X - ox) / sc, ry = (e.Y - oy) / sc; if (UiMove(rx, ry)) return; PointF cp = ColPt(rx, ry); float x = cp.X, y = cp.Y; int h = HitGrid(x, y);
		if (h != hover) { hover = h; Invalidate(); }
		int hs = HitSlot(x, y); if (hs != hoverSlot) { hoverSlot = hs; Wake(); Invalidate(); }
		Cursor = (h >= 0 || hs >= 0 || uiHand || btn.Contains(x, y) || spR.Contains(x, y)) ? Cursors.Hand : Cursors.Default;
	}
	private void OnDown(object s, MouseEventArgs e)
	{
		float rx = (e.X - ox) / sc, ry = (e.Y - oy) / sc; if (e.Button == MouseButtons.Left && UiDown(rx, ry)) return; PointF cp = ColPt(rx, ry); float x = cp.X, y = cp.Y; down = true; dragging = false; downX = x; downScroll = scroll; if (zoom >= 0) return; if (hist && e.Button == MouseButtons.Left) { down = false; if (FHist.ChipHit(x, y) == 1) { hist = false; Invalidate(); } return; } if (e.Button == MouseButtons.Left) { int hc = FHist.ChipHit(x, y); if (hc == 1) { down = false; hist = true; hScroll = 0; FHist.Refresh(); Invalidate(); return; } if (hc == 2) { down = false; FHist.SaveFull(this, RenderFull, "마룡이 타로"); return; } int hb = Chrome.HubHit(x, y); if (hb == 1) { down = false; FortuneForm.Open(); Close(); return; } if (hb == 2) { down = false; return; } } if (ry < 54f && e.Button == MouseButtons.Left) { down = false; Chrome.Drag(this); return; }
		if (spOpen) { down = false; for (int i = 0; i < SpreadName.Length; i++) if (spRows[i].Contains(x, y)) { SetMode(i); spOpen = false; Invalidate(); return; } spOpen = false; Invalidate(); return; }
		if (spR.Contains(x, y)) { spOpen = true; Sfx.Play("tick"); Invalidate(); down = false; return; }
		for (int i = 0; i < 5; i++) if (tR[i].Contains(x, y)) { topic = i; Sfx.Play("tick"); Invalidate(); down = false; return; }
		for (int i = 0; i < 2; i++) if (dR[i].Contains(x, y)) { if (mode == 0) { Sfx.Play("tick"); down = false; return; } full = i == 1; st = St.Idle; ResetSlots(); Sfx.Play("tick"); Invalidate(); down = false; return; }
		if (btn.Contains(x, y) && st != St.Shuffle) { StartShuffle(); down = false; }
	}
	private void OnUp(object s, MouseEventArgs e)
	{
		float rx = (e.X - ox) / sc, ry = (e.Y - oy) / sc; PointF cp = ColPt(rx, ry); float x = cp.X, y = cp.Y; if (UiUp()) { down = false; dragging = false; return; } bool wasDrag = dragging; down = false; dragging = false;
		if (zoom >= 0) { zoom = -1; Invalidate(); return; }
		if (st == St.Pick && !wasDrag) { int h = HitGrid(x, y); if (h >= 0 && nPick < slots.Length) Pick(h); return; }
		if ((st == St.Read || (st == St.Pick)) && !wasDrag) for (int i = slots.Length - 1; i >= 0; i--) if (slots[i].State == 3 && sR[i].Contains(x, y)) { zoom = i; zoomT = 0f; Wake(); Invalidate(); return; }
	}
	private void Pick(int idx)
	{
		RectangleF gr = GridRect(idx); int card = rest[idx]; taken[idx] = true; Slot sl = slots[nPick]; sl.Card = card; sl.Rev = revAll[card]; sl.State = 1; sl.T = 0f; sl.From = gr; sl.FromRot = 0f; nPick++; hover = -1; Sfx.Play("tick"); Wake(); Invalidate();
	}

	// ───────── 카드 그림 ─────────
	private Bitmap Face(int id, bool rev)
	{
		int key = id * 2 + (rev ? 1 : 0); Bitmap b; if (faces.TryGetValue(key, out b)) return b;
		if (id < 22) b = LoadRes("c" + id.ToString("00") + ".jpg"); else { int m = id - 22, s = m / 14, r = m % 14; b = r >= 10 ? LoadRes("c" + (22 + s * 4 + (r - 10)).ToString("00") + ".jpg") : MakePip(s, r); }
		if (b == null) { b = new Bitmap(176, 252); using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.FromArgb(240, 226, 196)); } }
		if (rev) b.RotateFlip(RotateFlipType.Rotate180FlipNone); Bitmap rb = RoundedScaled(b, b.Width, b.Height); b.Dispose(); faces[key] = rb; return rb;
	}
	private static readonly float[][][] PipPos = {
		new[] { new[] { .5f, .5f } },
		new[] { new[] { .5f, .24f }, new[] { .5f, .76f } },
		new[] { new[] { .5f, .17f }, new[] { .5f, .5f }, new[] { .5f, .83f } },
		new[] { new[] { .27f, .25f }, new[] { .73f, .25f }, new[] { .27f, .75f }, new[] { .73f, .75f } },
		new[] { new[] { .27f, .25f }, new[] { .73f, .25f }, new[] { .5f, .5f }, new[] { .27f, .75f }, new[] { .73f, .75f } },
		new[] { new[] { .27f, .17f }, new[] { .73f, .17f }, new[] { .27f, .5f }, new[] { .73f, .5f }, new[] { .27f, .83f }, new[] { .73f, .83f } },
		new[] { new[] { .27f, .17f }, new[] { .73f, .17f }, new[] { .5f, .33f }, new[] { .27f, .5f }, new[] { .73f, .5f }, new[] { .27f, .83f }, new[] { .73f, .83f } },
		new[] { new[] { .27f, .17f }, new[] { .73f, .17f }, new[] { .5f, .33f }, new[] { .27f, .5f }, new[] { .73f, .5f }, new[] { .5f, .67f }, new[] { .27f, .83f }, new[] { .73f, .83f } },
		new[] { new[] { .27f, .12f }, new[] { .73f, .12f }, new[] { .27f, .37f }, new[] { .73f, .37f }, new[] { .5f, .5f }, new[] { .27f, .63f }, new[] { .73f, .63f }, new[] { .27f, .88f }, new[] { .73f, .88f } },
		new[] { new[] { .27f, .12f }, new[] { .73f, .12f }, new[] { .5f, .25f }, new[] { .27f, .37f }, new[] { .73f, .37f }, new[] { .27f, .63f }, new[] { .73f, .63f }, new[] { .5f, .75f }, new[] { .27f, .88f }, new[] { .73f, .88f } } };
	private static readonly float[][] PipBox = { new[] { 124f, 176f }, new[] { 70f, 82f }, new[] { 64f, 62f }, new[] { 56f, 76f }, new[] { 52f, 70f }, new[] { 52f, 58f }, new[] { 50f, 56f }, new[] { 50f, 56f }, new[] { 46f, 44f }, new[] { 46f, 44f } };
	private Bitmap MakePip(int suit, int rank)
	{
		Bitmap b = new Bitmap(176, 252, System.Drawing.Imaging.PixelFormat.Format32bppArgb); RectangleF all = new RectangleF(0, 0, 176, 252);
		using (Graphics g = Graphics.FromImage(b))
		{
			Gfx.HQ(g); g.Clear(Color.Transparent); RectangleF card = new RectangleF(1.5f, 1.5f, 173f, 249f);
			using (GraphicsPath gp = Gfx.Round(card, 12)) { using (LinearGradientBrush lb = new LinearGradientBrush(all, Color.FromArgb(246, 235, 208), Color.FromArgb(228, 209, 168), 70f)) g.FillPath(lb, gp); using (Pen p = new Pen(Color.FromArgb(214, 164, 70), 3f)) g.DrawPath(p, gp); }
			Random rr = new Random(suit * 31 + rank); for (int i = 0; i < 90; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(10 + rr.Next(20), 140, 100, 50))) g.FillEllipse(sb, rr.Next(14, 160), rr.Next(14, 238), 1 + rr.Next(3), 1 + rr.Next(3));
			using (GraphicsPath gp = Gfx.Round(new RectangleF(9, 9, 158, 234), 8)) using (Pen p = new Pen(Color.FromArgb(180, 190, 140, 56), 1.4f)) g.DrawPath(p, gp);
			Bitmap sym = pip[suit]; string num = rank == 0 ? "A" : (rank + 1).ToString();
			using (Font f = new Font("Arial", 13f, FontStyle.Bold)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(170, 120, 70, 40))) { g.DrawString(num, f, sb, 14, 12); GraphicsState gs = g.Save(); g.TranslateTransform(176 - 14, 252 - 12); g.RotateTransform(180); g.DrawString(num, f, sb, 0, 0); g.Restore(gs); }
			if (sym != null)
			{
				float[][] pp = PipPos[rank]; float bw = PipBox[rank][0], bh = PipBox[rank][1]; float rx = 26, ry = 30, rw = 124, rh = 192; if (suit == 0 || suit == 2) { bh *= 1.35f; if (rank > 0 && rank < 8) bw *= 1.2f; } float k = Math.Min(bw / sym.Width, bh / sym.Height); float w = sym.Width * k, h = sym.Height * k;
				foreach (float[] q in pp) g.DrawImage(sym, rx + q[0] * rw - w / 2, ry + q[1] * rh - h / 2, w, h);
			}
		}
		return b;
	}
	private static Bitmap RoundedScaled(Bitmap src, int w, int h)
	{
		Bitmap b = new Bitmap(Math.Max(2, w), Math.Max(2, h), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using (Graphics g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; using (GraphicsPath gp = Gfx.Round(new RectangleF(0, 0, b.Width, b.Height), Math.Max(3f, b.Width * 0.06f))) g.SetClip(gp, CombineMode.Replace); g.DrawImage(src, 0, 0, b.Width, b.Height); }
		return b;
	}
	private void DrawCard(Graphics g, Bitmap bmp, RectangleF r, float scaleX)
	{
		float w = r.Width * Math.Max(0.01f, scaleX); RectangleF d = new RectangleF(r.X + (r.Width - w) / 2, r.Y, w, r.Height);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 30, 20, 10))) g.FillRectangle(b, d.X + 2, d.Y + 3, d.Width, d.Height);
		g.DrawImage(bmp, d);
	}
	private void DrawBackSmall(Graphics g, PointF p, float rot, float lift, float zoom1)
	{
		GraphicsState gs = g.Save(); float w = FW * zoom1, h = FH * zoom1; g.TranslateTransform(p.X + FW / 2, p.Y + FH / 2 - lift); g.RotateTransform(rot);
		using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 30, 20, 10))) g.FillRectangle(b, -w / 2 + 2, -h / 2 + 2, w, h);
		g.DrawImage(backSm, -w / 2, -h / 2, w, h); g.Restore(gs);
	}

	// ───────── 그리기 ─────────
	private float slotBot = 300f; private bool noLab; private readonly float[] sRot = new float[12];
	private float SlotsBottom { get { return slotBot; } }
	private const float SlotTop = 160f;
	private void P(int i, float cx, float cy, float w) { sR[i] = new RectangleF(cx - w / 2f, cy - w / CAR / 2f, w, w / CAR); }
	private void LayoutSlots()
	{
		int n = SlotN; for (int i = 0; i < 12; i++) { sR[i] = RectangleF.Empty; sRot[i] = 0f; } noLab = false; float T = SlotTop, w, h;
		switch (mode)
		{
			case 2: w = 66f; h = w / CAR; P(0, 160f, T + h / 2, w); P(1, 260f, T + h / 2, w); P(2, 210f, T + h + 40f + h / 2, w); slotBot = T + 2 * h + 82f; break;
			case 3: w = 52f; h = w / CAR; noLab = true; { float cy = T + h * 1.5f + 6f; P(0, 210f, cy, w); P(1, 210f - 60f, cy, w); P(2, 210f, cy - h - 6f, w); P(3, 210f + 60f, cy, w); P(4, 210f, cy + h + 6f, w); slotBot = T + 3 * h + 12f + 22f; } break;
			case 5: w = 58f; h = w / CAR; P(0, 210f, T + h / 2, w); float by = T + h + 40f + h / 2; P(1, 130f, by, w); P(3, 210f, by, w); P(2, 290f, by, w); slotBot = T + 2 * h + 82f; break;
			case 6:
				// 켈틱 크로스: 왼쪽 십자(1 현재 · 2 도전은 1 위에 가로로 살짝 비켜 겹쳐서 둘 다 보이게) + 오른쪽 기둥(7~10, 아래에서 위로)
				noLab = true; { w = 54f; h = w / CAR; float sw = 46f, shh = sw / CAR, gap = 5f, colH = 4 * shh + 3 * gap, crossH = 3 * h + 62f, H0 = Math.Max(colH, crossH), top = T + 6f, cx = 132f, cy = top + H0 / 2f;
				P(0, cx, cy - 16f, w); P(1, cx + 6f, cy + 22f, w); sRot[1] = 90f; P(2, cx, cy + h + 32f, w); P(3, cx - w - 30f, cy, w); P(4, cx, cy - h - 26f, w); P(5, cx + w + 30f, cy, w);
				float sx = 332f, sy0 = top + (H0 - colH) / 2f; for (int k = 0; k < 4; k++) P(6 + k, sx, sy0 + shh / 2f + (3 - k) * (shh + gap), sw);
				slotBot = top + H0 + 20f; } break;
			case 7: w = 38f; h = w / CAR; noLab = true; { float cy = T + h / 2 + 104f; for (int i = 0; i < 12; i++) { double a = (-90 + 30 * i) * Math.PI / 180.0; P(i, 210f + 150f * (float)Math.Cos(a), cy + 104f * (float)Math.Sin(a), w); } slotBot = cy + 104f + h / 2 + 20f; } break;
			case 9: w = 64f; h = w / CAR; P(0, 134f, T + h / 2, w); P(1, 210f, T + h / 2, w); P(2, 286f, T + h / 2, w); P(3, 170f, T + h + 40f + h / 2, w); P(4, 250f, T + h + 40f + h / 2, w); slotBot = T + 2 * h + 82f; break;
			case 10: w = 44f; h = w / CAR; noLab = true; { float cy = T + h / 2; for (int i = 0; i < 7; i++) { double a = (180 + 30 * i) * Math.PI / 180.0; P(i, 210f + 150f * (float)Math.Cos(a), cy - 90f * (float)Math.Sin(a), w); } slotBot = cy + 90f + h / 2 + 20f; } break;
			default:
				w = n == 1 ? 112f : 98f; h = w / CAR; for (int i = 0; i < n; i++) P(i, 210f + (i - (n - 1) / 2f) * (w + 14f), T + h / 2, w); slotBot = T + h + 42f; break;
		}
	}
	private void Badge(Graphics g, int i)
	{
		RectangleF r = sR[i]; float bx = r.X + 1f, by = r.Y + 1f; if (sRot[i] != 0f) { bx = r.X + r.Width / 2f + 14f; by = r.Y + r.Height / 2f - 21f; }
		float bw = i >= 9 ? 19f : 13f;   // 두 자리(10번)는 옆으로 넓은 알약 모양이라 '1'로 잘리지 않아요
		using (GraphicsPath gp = Gfx.Round(new RectangleF(bx, by, bw, 13f), 6.5f)) using (SolidBrush b = new SolidBrush(PRed)) g.FillPath(b, gp);
		FUi.Text(g, (i + 1).ToString(), FUi.F(i >= 9 ? 6.6f : 7f, 1), Color.White, new RectangleF(bx - 3f, by, bw + 6f, 13f), StringAlignment.Center, StringAlignment.Center, false);
	}
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics; Chrome.Frame(g, ClientSize.Width, ClientSize.Height); Gfx.HQ(g); g.TranslateTransform(ox, oy); g.ScaleTransform(sc, sc); g.InterpolationMode = InterpolationMode.Bilinear; g.CompositingQuality = CompositingQuality.HighSpeed;
		try { Draw(g); } catch (Exception ex) { Program.LogErr(ex); }
	}
	private void Chip(Graphics g, RectangleF r, string t, bool on, bool primary)
	{
		using (GraphicsPath gp = Gfx.Round(r, r.Height / 2)) { using (SolidBrush b = new SolidBrush(on || primary ? PRed : PCard)) g.FillPath(b, gp); using (Pen p = new Pen(on || primary ? PRed : PLine, 1f)) g.DrawPath(p, gp); }
		FUi.Text(g, t, FUi.F(9f, 1), on || primary ? Color.White : PSub, r, StringAlignment.Center, StringAlignment.Center, false);
	}
	private void RecordRead() { try { string[] pn = PosNames(mode, 0); string s = SpreadName[mode] + " · " + TopicName[topic] + " : "; for (int i = 0; i < slots.Length; i++) s += (i > 0 ? ", " : "") + CardName(slots[i].Card) + (slots[i].Rev ? "(역)" : ""); FHist.Add("타로", DateTime.Now.ToString("HHmmss"), s); Bond.Daily("ta"); } catch { } }
	private bool hist; private float hScroll;
	private void Draw(Graphics g)
	{
		DrawMain(g);
		if (hist) { FHist.DrawOverlay(g, LW, LH, hScroll); GraphicsState cg3 = g.Save(); g.TranslateTransform(CTX, 0f); g.ScaleTransform(cmp, cmp); Chrome.Hub(g, true, PRed, PCard, Color.White, PSub); g.Restore(cg3); }
		{ GraphicsState cg2 = g.Save(); g.TranslateTransform(CTX, 0f); g.ScaleTransform(cmp, cmp); FHist.DrawChips(g, hist, PRed, PCard, PSub, Color.White); g.Restore(cg2); }
	}
	private void DrawMain(Graphics g)
	{
		GraphicsState cgs = g.Save(); g.TranslateTransform(CTX, 0f); g.ScaleTransform(cmp, cmp);
		FUi.Text(g, "마룡이 타로", FUi.F(15f, 1), PInk, new RectangleF(16, 8, 215, 28), StringAlignment.Near, StringAlignment.Center, false); if (!exporting) Chrome.Hub(g, true, PRed, PCard, Color.White, PSub);
		FUi.Text(g, "직접 고르는 타로 · 재미로 보는 풀이", FUi.F(8.5f, 0), PMut, new RectangleF(16, 36, 212, 16), StringAlignment.Near, StringAlignment.Center, false);
		spR = new RectangleF(14, 58, 262, 28);
		using (GraphicsPath gp = Gfx.Round(spR, 14)) { using (SolidBrush b = new SolidBrush(PCard)) g.FillPath(b, gp); using (Pen p = new Pen(PRed, 1.4f)) g.DrawPath(p, gp); }
		FUi.Text(g, SpreadName[mode] + " · " + SlotN + "장", FUi.F(9.5f, 1), PInk, new RectangleF(24, 58, 220, 28), StringAlignment.Near, StringAlignment.Center, false);
		using (SolidBrush b = new SolidBrush(PRed)) g.FillPolygon(b, new[] { new PointF(254, 69), new PointF(264, 69), new PointF(259, 75) });
		btn = new RectangleF(284, 58, 122, 28); Chip(g, btn, st == St.Idle ? "카드 섞기" : (st == St.Shuffle ? "섞는 중" : "다시 섞기"), false, true);
		dR[0] = new RectangleF(14, 92, 118, 26); dR[1] = new RectangleF(138, 92, 160, 26); if (mode == 0) { Chip(g, dR[0], "메이저 22장", true, false); Chip(g, dR[1], "한 장은 메이저만 써요", false, false); } else { Chip(g, dR[0], "메이저 22장", !full, false); Chip(g, dR[1], "메이저+마이너 78장", full, false); }
		float tx = 14f; float[] tw = { 48, 48, 66, 48, 48 }; for (int i = 0; i < 5; i++) { tR[i] = new RectangleF(tx, 124, tw[i], 26); Chip(g, tR[i], TopicName[i], i == topic, false); tx += tw[i] + 6f; }
		LayoutSlots();
		for (int i = 0; i < slots.Length; i++) DrawSlot(g, i);
		if (noLab) for (int i = 0; i < slots.Length; i++) if (slots[i].State >= 2) Badge(g, i);
		if (exporting) { g.Restore(cgs); if (st == St.Read) DrawReadRaw(g, ReadTop); return; }
		if (st == St.Idle) DrawIdle(g); else if (st == St.Shuffle) DrawShuffleMagic(g); else if (st == St.Pick) DrawPick(g);
		DrawSlotHover(g); DrawMotes(g);
		if (spOpen) DrawSpreadMenu(g);
		g.Restore(cgs);
		if (st == St.Read) DrawRead(g);
		if (zoom >= 0) DrawZoom(g);
	}
	private void DrawSpreadMenu(Graphics g)
	{
		using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 30, 20, 10))) g.FillRectangle(b, -600f, -10f, 1800f, LH + 20f);
		RectangleF pr = new RectangleF(14, 90, 392, SpreadName.Length * 46 + 16);
		using (GraphicsPath gp = Gfx.Round(pr, 14)) { using (SolidBrush b = new SolidBrush(Theme.Raised)) g.FillPath(b, gp); using (Pen p = new Pen(PRed, 1.8f)) g.DrawPath(p, gp); }
		for (int i = 0; i < SpreadName.Length; i++)
		{
			RectangleF r = new RectangleF(22, 98 + i * 46, 376, 42); spRows[i] = r; bool on = i == mode;
			using (GraphicsPath gp = Gfx.Round(r, 10)) { using (SolidBrush b = new SolidBrush(on ? PRed : Theme.Page)) g.FillPath(b, gp); if (!on) using (Pen p = new Pen(PLine, 1f)) g.DrawPath(p, gp); }
			FUi.Text(g, SpreadName[i], FUi.F(10.5f, 1), on ? Color.White : PInk, new RectangleF(34, r.Y + 3, 250, 20), StringAlignment.Near, StringAlignment.Center, false);
			FUi.Text(g, SpreadN[i] + "장", FUi.F(9f, 1), on ? Color.White : PRed, new RectangleF(320, r.Y + 3, 68, 20), StringAlignment.Far, StringAlignment.Center, false);
			FUi.Text(g, SpreadDesc[i], FUi.F(8.5f, 0), on ? Color.FromArgb(250, 235, 225) : PSub, new RectangleF(34, r.Y + 22, 350, 18), StringAlignment.Near, StringAlignment.Center, false);
		}
	}
	private void DrawSlot(Graphics g, int i)
	{
		Slot s = slots[i]; RectangleF r = sR[i]; string pn = posN[i]; bool small = r.Width < 80; GraphicsState rg = null; if (sRot[i] != 0f && s.State >= 2) { rg = g.Save(); g.TranslateTransform(r.X + r.Width / 2f, r.Y + r.Height / 2f); g.RotateTransform(sRot[i]); g.TranslateTransform(-(r.X + r.Width / 2f), -(r.Y + r.Height / 2f)); }
		if (s.State == 0) { using (GraphicsPath gp = Gfx.Round(r, 8)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 255, 250, 238))) g.FillPath(b, gp); using (Pen p = new Pen(PLine, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawPath(p, gp); } FUi.Text(g, (i + 1).ToString(), FUi.F(small ? 12f : 20f, 1), PMut, r, StringAlignment.Center, StringAlignment.Center, false); }
		else if (s.State == 1) { float t = 1f - (float)Math.Pow(1f - s.T, 3); RectangleF cur = new RectangleF(s.From.X + (r.X - s.From.X) * t, s.From.Y + (r.Y - s.From.Y) * t, s.From.Width + (r.Width - s.From.Width) * t, s.From.Height + (r.Height - s.From.Height) * t); GraphicsState gs = g.Save(); g.TranslateTransform(cur.X + cur.Width / 2, cur.Y + cur.Height / 2); g.RotateTransform(s.FromRot * (1f - t)); g.TranslateTransform(-(cur.X + cur.Width / 2), -(cur.Y + cur.Height / 2)); DrawCard(g, backSm, cur, 1f); g.Restore(gs); }
		else if (s.State == 2) { float t = s.T; if (t < 0.5f) DrawCard(g, backSm, r, 1f - t * 2f); else DrawCard(g, Face(s.Card, s.Rev), r, (t - 0.5f) * 2f); }
		else DrawCard(g, Face(s.Card, s.Rev), r, 1f);
		if (rg != null) g.Restore(rg);
		if (noLab) return;
		FUi.Text(g, pn, FUi.F(small ? 8f : 9f, 1), PRed, new RectangleF(r.X - 8, r.Bottom + 3, r.Width + 16, 15), StringAlignment.Center, StringAlignment.Center, false);
		if (s.State == 3) FUi.Text(g, CardName(s.Card) + (s.Rev ? " (역)" : ""), FUi.F(small ? 7f : 9f, 1), s.Rev ? PRed : PInk, new RectangleF(r.X - 8, r.Bottom + 18, r.Width + 16, 17), StringAlignment.Center, StringAlignment.Center, false);
	}
	private void DrawIdle(Graphics g)
	{
		float y = SlotsBottom + 6f; float h = 168f;
		using (GraphicsPath gp = Gfx.Round(new RectangleF(14, y, 392, h), 12)) { using (SolidBrush b = new SolidBrush(PCard)) g.FillPath(b, gp); using (Pen p = new Pen(PLine, 1f)) g.DrawPath(p, gp); }
		FUi.Text(g, SpreadName[mode] + " — " + SpreadDesc[mode], FUi.F(10f, 1), PRed, new RectangleF(26, y + 6, 372, 20), StringAlignment.Near, StringAlignment.Center, false);
		string[] ln = { "① 위에서 보기 방식·덱·주제를 골라요", "② 「카드 섞기」를 누르면 카드가 섞여 펼쳐져요", "③ 마음이 끌리는 카드 " + SlotN + "장을 눌러요 (마우스를 올리면 커져요)", "④ 카드가 뒤집히면 풀이가 나와요 · 카드를 누르면 크게 봐요" };
		for (int i = 0; i < 4; i++) FUi.Text(g, ln[i], FUi.F(9f, 0), PInk, new RectangleF(26, y + 30 + i * 20, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, full ? "메이저 22장은 인생의 큰 흐름을, 마이너 56장은 일상의 구체적인 일을 보여줘요." : "메이저 22장만 써서 큰 흐름과 전환점 위주로 가볍게 봐요.", FUi.F(8.5f, 0), PSub, new RectangleF(26, y + 114, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		FUi.Text(g, "메이저의 역방향은 점검할 부분을, 마이너는 정방향으로만 풀어요.", FUi.F(8.5f, 0), PSub, new RectangleF(26, y + 134, 372, 18), StringAlignment.Near, StringAlignment.Center, false);
		if (y + h + 120 < LH) for (int i = 4; i >= 0; i--) DrawBackSmallBig(g, new PointF(210 - 42 + i * 1.5f, y + h + 24 - i * 2f), 84f, 120f);
	}
	private void DrawBackSmallBig(Graphics g, PointF p, float w, float h) { GraphicsState gs = g.Save(); using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 30, 20, 10))) g.FillRectangle(b, p.X + 2, p.Y + 3, w, h); g.SetClip(Gfx.Round(new RectangleF(p.X, p.Y, w, h), 7), CombineMode.Replace); g.DrawImage(back, p.X, p.Y, w, h); g.Restore(gs); }
	private void DrawShuffle(Graphics g)
	{
		float t = shufT, cx = 210f, cy = 450f; FUi.Text(g, "카드를 섞는 중이에요… 마음속 고민을 떠올려 보세요", FUi.F(10f, 1), PSub, new RectangleF(20, SlotsBottom + 6, 380, 22), StringAlignment.Center, StringAlignment.Center, false);
		for (int i = 0; i < 14; i++)
		{
			float ph = t * 6.283f * 2f + i * 0.9f, side = (i % 2 == 0 ? -1f : 1f); float off = side * (30f + 34f * (float)Math.Abs(Math.Sin(ph))) * (1f - t * 0.2f); float rot = side * 10f * (float)Math.Sin(ph + 1f);
			DrawBackSmall(g, new PointF(cx - FW / 2 + off, cy + (float)Math.Sin(ph * 0.5f) * 10f - i * 1.2f), rot, 0f, 1.25f);
		}
	}
	private void DrawPick(Graphics g)
	{
		float y = SlotsBottom + 4f; FUi.Text(g, nPick < slots.Length ? "마음이 끌리는 카드를 눌러 주세요 (" + nPick + " / " + slots.Length + ")" : "카드를 뒤집는 중이에요…", FUi.F(10.5f, 1), PRed, new RectangleF(20, y, 380, 22), StringAlignment.Center, StringAlignment.Center, false);
		if (nPick >= slots.Length) return;
		FUi.Text(g, "카드에 마우스를 올리면 커져요 · " + (slots.Length - nPick) + "장 더 고르세요", FUi.F(8.5f, 0), PMut, new RectangleF(20, y + 20, 380, 16), StringAlignment.Center, StringAlignment.Center, false);
		LayoutGrid(); int n = rest.Count; float e = 1f - (float)Math.Pow(1f - spreadT, 3); float cx = CW / 2f - gw / 2f, cy = gy0 + (gh * 2f) - gh / 2f; InterpolationMode im = g.InterpolationMode; g.InterpolationMode = InterpolationMode.NearestNeighbor;
		for (int i = 0; i < n; i++)
		{
			if (taken[i] || i == hover) continue; RectangleF r = GridRect(i); float rx = cx + (r.X - cx) * e, ry = cy + (r.Y - cy) * e; g.DrawImage(backTiny, rx, ry, gw, gh);
		}
		g.InterpolationMode = im;
		if (hover >= 0 && hover < n && spreadT >= 1f)
		{
			RectangleF rc = GridRect(hover); float pw = Math.Max(gw * 2.3f, 62f), ph = pw / CAR; float px = Math.Max(6f, Math.Min(CW - 6f - pw, rc.X + gw / 2 - pw / 2)), py = Math.Max(SlotsBottom + 38f, Math.Min(LH - 6f - ph, rc.Y + gh / 2 - ph / 2));
			DrawCard(g, backSm, new RectangleF(px, py, pw, ph), 1f); using (Pen p = new Pen(PRed, 2f)) g.DrawRectangle(p, px, py, pw, ph);
		}
	}
	private sealed class Ent { public string Title, Frame, Body; public bool Red, Hi; public float H, FH; }
	private static string Dir(Slot s) { return s.Card < 22 ? (s.Rev ? " (역방향)" : " (정방향)") : ""; }
	private static float MH(string t, float pt, float w) { if (string.IsNullOrEmpty(t)) return 0f; return Gfx.MeasureHL(t, Theme.F(pt, 0), w) + 6f; }
	private static float EstH(string t, float cpl) { int n = 0; foreach (string ln in t.Split('\n')) n += Math.Max(1, (int)Math.Ceiling(ln.Length / cpl)); return n * 14.5f; }
	private List<Ent> Ents { get { if (entsCache == null) entsCache = BuildEnts(); return entsCache; } }
	private List<Ent> BuildEnts()
	{
		List<Ent> L = new List<Ent>(); int N = slots.Length; float tm = textMul;
		for (int i = 0; i < N; i++)
		{
			Slot s = slots[i]; string frame = mode == 7 ? "이 달의 흐름을 비추는 카드예요." : Frames[mode][i]; string body; string title;
			if (mode == 8)
			{
				if (s.Card < 22) body = OracleMaj[s.Card][0] + "\n▶ 해볼 일 · " + OracleMaj[s.Card][1] + "\n▶ 행운 · " + MajLucky[s.Card];
				else { int m = s.Card - 22, r = m % 14, su = m / 14; body = OracleRank[r].Replace("{d}", SuitDom[su].Replace(" 쪽", "")) + "\n▶ 해볼 일 · " + OracleAct[r] + "\n▶ 행운 · " + SuitLucky[su]; }
				title = posN[i] + " · " + CardName(s.Card);
			}
			else if (mode == 4)
			{
				int y = YesScore(s.Card, s.Rev); title = Verdict(y) + " · " + CardName(s.Card) + (s.Rev ? " (역)" : "");
				body = VerdictText(y) + "\n" + Meaning(s.Card, s.Rev);
			}
			else { title = (mode >= 3 && mode != 8 ? (i + 1) + ". " : "") + posN[i] + " · " + CardName(s.Card) + Dir(s) + " · " + CardKw(s.Card); body = Body(s.Card, s.Rev, topic); }
			Ent e = new Ent { Title = title, Frame = frame, Body = body, Red = s.Rev, Hi = (mode == 6 && i == 9) || (mode == 3 && i == 4) || (mode == 10 && i == 6) };
			e.FH = MH(frame, 8.5f * tm, (LW - 64f)); e.H = 26f * tm + e.FH + MH(body, 9f * tm, (LW - 64f)) + 10f; L.Add(e);
		}
		string sum = Summary();
		L.Add(new Ent { Title = "한 줄 요약", Body = sum, Hi = true, H = 26f * tm + MH(sum, 9f * tm, (LW - 64f)) + 10f });
		return L;
	}
	private string Summary()
	{
		int N = slots.Length, rc = 0, mj = 0, courts = 0, aces = 0; int[] su = new int[4];
		foreach (Slot s in slots) { if (s.Rev) rc++; if (s.Card < 22) mj++; else { int m = s.Card - 22; su[m / 14]++; if (m % 14 >= 10) courts++; if (m % 14 == 0) aces++; } }
		if (mode == 4) return Verdict(YesScore(slots[0].Card, slots[0].Rev)) + ". " + VerdictText(YesScore(slots[0].Card, slots[0].Rev));
		if (mode == 8) return "오늘은 " + posN[0] + "처럼 가볍게, 카드가 건넨 '해볼 일' 하나만 실천해 보세요.";
		if (mode == 5) { int a = YesScore(slots[1].Card, slots[1].Rev), b = YesScore(slots[2].Card, slots[2].Rev); string w = a > b ? "A쪽이 더 힘을 받아요." : (b > a ? "B쪽이 더 힘을 받아요." : "두 선택의 기운이 비슷해요. 마음이 더 끌리는 쪽을 고르세요."); return "A " + a + "점 · B " + b + "점 — " + w + " 선택 후의 태도는 마지막 카드가 알려줘요."; }
		if (mode == 7)
		{
			int best = 0, worst = 0; int[] sc = new int[N]; for (int i = 0; i < N; i++) { sc[i] = YesScore(slots[i].Card, slots[i].Rev); if (sc[i] > sc[best]) best = i; if (sc[i] < sc[worst]) worst = i; }
			return "가장 순조로운 달은 " + (best + 1) + "월, 조심할 달은 " + (worst + 1) + "월이에요. 역방향 " + rc + "장 — " + (rc * 3 > N ? "점검할 달이 많은 해예요." : "전반적으로 무난한 흐름이에요.");
		}
		string a1 = mj * 2 >= N ? "큰 흐름과 전환이 걸린 질문이에요." : "일상 속 구체적인 일들에 대한 질문이에요.";
		int bs = 0; for (int i = 1; i < 4; i++) if (su[i] > su[bs]) bs = i; string b2 = su[bs] >= 2 ? " " + SuitDom[bs].Replace(" 쪽", "") + " 이야기가 중심이에요." : "";
		string c2 = courts >= 2 ? " 궁정 카드가 여럿이라 사람과의 관계가 변수예요." : ""; string d2 = aces >= 1 ? " 에이스가 있어 새로운 시작의 기회가 와요." : "";
		string c = rc == 0 ? " 모두 정방향이라 흐름이 순조로워요." : (rc * 2 > N ? " 역방향이 많아 천천히 점검하며 가면 좋아요." : " 정·역방향이 섞여 있어 조심할 곳만 살피면 좋아요.");
		string last = mode == 6 ? " 최종 결과 카드(" + CardName(slots[9].Card) + ")를 마음에 새겨 두세요." : (mode == 3 ? " 결과 카드(" + CardName(slots[4].Card) + ")가 향하는 방향을 기억하세요." : "");
		return a1 + b2 + c2 + d2 + c + last;
	}
	private float ReadMax() { float need = 10f; foreach (Ent e in Ents) need += e.H + 6f; float avail = LH - 6f - ReadTop; return Math.Max(0f, need - avail); }
	private void DrawReadRaw(Graphics g, float y0)
	{
		float y = y0, tm = textMul;
		foreach (Ent e in Ents)
		{
			using (GraphicsPath gp = Gfx.Round(new RectangleF(20, y, LW - 48f, e.H), 10)) { using (SolidBrush b = new SolidBrush(e.Hi ? PSoft : PCard)) g.FillPath(b, gp); if (e.Hi) using (Pen p = new Pen(PRed, 1.2f)) g.DrawPath(p, gp); }
			FUi.Text(g, e.Title, FUi.F(9.5f * tm, 1), e.Red || e.Hi ? PRed : PInk, new RectangleF(30, y + 3, LW - 64f, 20f * tm), StringAlignment.Near, StringAlignment.Center, false);
			float yy = y + 24f * tm;
			if (e.Frame != null) { float h = e.FH; FUi.Text(g, e.Frame, FUi.F(8.5f * tm, 0), PMut, new RectangleF(30, yy, LW - 64f, h + 2), StringAlignment.Near, StringAlignment.Near, true); yy += h; }
			FUi.Text(g, e.Body, FUi.F(9f * tm, 0), PInk, new RectangleF(30, yy, LW - 64f, e.H - (yy - y) + 2), StringAlignment.Near, StringAlignment.Near, true);
			y += e.H + 6f;
		}
	}
	private void DrawZoom(Graphics g)
	{
		Slot s = slots[zoom]; using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(205 * zoomT), 16, 14, 12))) g.FillRectangle(b, 0, 0, LW, LH);
		float k = 0.8f + 0.2f * zoomT, w = 190f * k, h = w / CAR; RectangleF r = new RectangleF((LW - w) / 2, 40f, w, h); DrawCard(g, Face(s.Card, s.Rev), r, 1f);
		FUi.Text(g, posN[zoom] + " · " + CardName(s.Card) + Dir(s), FUi.F(11.5f, 1), Color.White, new RectangleF(20, r.Bottom + 10, 380, 24), StringAlignment.Center, StringAlignment.Center, false);
		FUi.Text(g, "키워드 · " + CardKw(s.Card), FUi.F(9.5f, 1), Color.FromArgb(240, 214, 140), new RectangleF(20, r.Bottom + 34, 380, 20), StringAlignment.Center, StringAlignment.Center, false);
		{ string zb = Body(s.Card, s.Rev, topic); float zp = 9.5f * textMul, avail = LH - 34f - (r.Bottom + 60f); while (zp > 7.5f && MH(zb, zp, 352f) > avail) zp -= 0.5f; FUi.Text(g, zb, FUi.F(zp, 0), Color.FromArgb(238, 232, 220), new RectangleF(34, r.Bottom + 60, 352, avail), StringAlignment.Center, StringAlignment.Near, true); }
		FUi.Text(g, "아무 곳이나 누르면 닫혀요", FUi.F(8.5f, 0), Color.FromArgb(180, 174, 164), new RectangleF(20, LH - 26, 380, 16), StringAlignment.Center, StringAlignment.Center, false);
	}
}
