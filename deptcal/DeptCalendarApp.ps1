# 부서 일정 공유 달력 v2 - 바탕화면 위젯형 / 개인(암호화)+부서 일정 / 토스트 알림
# 이름만 입력하면 사용 가능. 데이터는 Supabase(무료 클라우드)로 공유됩니다.
# 개인 일정은 이 PC의 암호키로 암호화되어 서버 관리자도 내용을 볼 수 없습니다.

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ReleaseCapture();
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
  [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
}
"@
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class HotKeyWin : System.Windows.Forms.NativeWindow {
  [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
  [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
  public event Action Pressed;
  public HotKeyWin() { CreateHandle(new System.Windows.Forms.CreateParams()); }
  public bool Register(uint mod, uint vk) { return RegisterHotKey(Handle, 1, mod, vk); }
  public void Release() { UnregisterHotKey(Handle, 1); }
  protected override void WndProc(ref System.Windows.Forms.Message m) {
    if (m.Msg == 0x312 && Pressed != null) Pressed();
    base.WndProc(ref m);
  }
}
// 테두리 없는 창 + 가장자리로 크기 조절
public class SizableForm : System.Windows.Forms.Form {
  public bool ResizeEnabled = true;
  public bool KeepBottom = false;
  [StructLayout(LayoutKind.Sequential)]
  struct WINDOWPOS { public IntPtr hwnd; public IntPtr hwndInsertAfter; public int x; public int y; public int cx; public int cy; public uint flags; }
  protected override System.Windows.Forms.CreateParams CreateParams {
    get { System.Windows.Forms.CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; }     // WS_EX_TOOLWINDOW
  }
  protected override void WndProc(ref System.Windows.Forms.Message m) {
    if (m.Msg == 0x46 && KeepBottom) {      // 클릭해도 다른 창 위로 올라오지 않고 항상 바탕화면 바로 위(맨 뒤)에 유지
      WINDOWPOS wp = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
      if ((wp.flags & 0x80) == 0) { wp.hwndInsertAfter = (IntPtr)1; wp.flags &= ~0x4u; Marshal.StructureToPtr(wp, m.LParam, false); }
    }
    if (m.Msg == 0x84) {
      base.WndProc(ref m);
      if ((int)m.Result == 1 && ResizeEnabled) {
        long lp = m.LParam.ToInt64();
        Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
        int g = 7;
        bool l = p.X < g, r = p.X >= ClientSize.Width - g, t = p.Y < g, b = p.Y >= ClientSize.Height - g;
        if (t && l) m.Result = (IntPtr)13; else if (t && r) m.Result = (IntPtr)14;
        else if (b && l) m.Result = (IntPtr)16; else if (b && r) m.Result = (IntPtr)17;
        else if (l) m.Result = (IntPtr)10; else if (r) m.Result = (IntPtr)11;
        else if (t) m.Result = (IntPtr)12; else if (b) m.Result = (IntPtr)15;
      }
      return;
    }
    base.WndProc(ref m);
  }
}
"@

# 바탕화면 아이콘 '뒤'(배경화면 바로 위)에 창을 끼워 넣기: WorkerW 의 자식 창으로 만들고,
# 아이콘 층이 마우스를 가로채므로 마우스 훅으로 빈 바탕 클릭만 달력에 전달한다.
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class DeskEmbed {
  delegate bool EnumProc(IntPtr h, IntPtr l);
  delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; }
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; }
  [StructLayout(LayoutKind.Sequential)] struct MSLL { public PT pt; public uint data, flags, time; public IntPtr extra; }
  [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
  [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
  [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr c, IntPtr p);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref PT p);
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(PT p);
  [DllImport("user32.dll")] static extern IntPtr ChildWindowFromPointEx(IntPtr h, PT p, uint f);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc f, IntPtr mod, uint tid);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
  [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool i, uint pid);
  [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr n, uint t, uint pr);
  [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr a, UIntPtr n, uint t);
  [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, UIntPtr n, out UIntPtr w);
  [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] b, UIntPtr n, out UIntPtr r);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

  static IntPtr workerw = IntPtr.Zero, hook = IntPtr.Zero, form = IntPtr.Zero;
  static HookProc proc;
  public static bool Active = false;     // 지금 아이콘 뒤에 끼워져 있는가
  public static bool Hold = false;       // true 면 끼우지 않음(대화상자·편집 중)

  static string Cls(IntPtr h) { StringBuilder sb = new StringBuilder(64); GetClassName(h, sb, 64); return sb.ToString(); }

  static IntPtr FindWorkerW() {
    IntPtr prog = FindWindow("Progman", null);
    if (prog == IntPtr.Zero) return IntPtr.Zero;
    IntPtr r; SendMessageTimeout(prog, 0x052C, (IntPtr)0xD, (IntPtr)1, 0, 1000, out r);     // 바탕화면 아래 WorkerW 층을 만들어 달라는 요청
    IntPtr w = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) w = FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
      return true;
    }, IntPtr.Zero);
    if (w == IntPtr.Zero) w = FindWindowEx(prog, IntPtr.Zero, "WorkerW", null);              // 최신 윈도우 11 구조
    return w;
  }

  public static bool Attach(IntPtr h) {
    if (Active && form == h) return true;
    IntPtr w = FindWorkerW();
    if (w == IntPtr.Zero) return false;
    RECT rc; GetWindowRect(h, out rc);
    SetParent(h, w);
    PT p; p.x = rc.l; p.y = rc.t; ScreenToClient(w, ref p);
    SetWindowPos(h, IntPtr.Zero, p.x, p.y, rc.r - rc.l, rc.b - rc.t, 0x4 | 0x10 | 0x40);     // NOZORDER | NOACTIVATE | SHOWWINDOW
    workerw = w; form = h; Active = true;
    if (hook == IntPtr.Zero) {
      proc = new HookProc(Callback);
      hook = SetWindowsHookEx(14, proc, GetModuleHandle(null), 0);
    }
    return true;
  }

  public static void Detach(IntPtr h) {
    if (!Active) return;
    RECT rc; GetWindowRect(h, out rc);
    SetParent(h, IntPtr.Zero);
    SetWindowPos(h, IntPtr.Zero, rc.l, rc.t, rc.r - rc.l, rc.b - rc.t, 0x4 | 0x10 | 0x40);
    Active = false; form = IntPtr.Zero;
    if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
  }

  // 바탕화면 아이콘 위인가? (아이콘이면 달력으로 보내지 않고 원래대로 아이콘이 받게 함)
  static bool OnIcon(IntPtr lv, PT pt) {
    uint pid; GetWindowThreadProcessId(lv, out pid);
    IntPtr ph = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);      // VM_OPERATION | VM_READ | VM_WRITE
    if (ph == IntPtr.Zero) return false;
    try {
      PT c = pt; ScreenToClient(lv, ref c);
      byte[] buf = new byte[24];
      BitConverter.GetBytes(c.x).CopyTo(buf, 0); BitConverter.GetBytes(c.y).CopyTo(buf, 4);
      IntPtr rem = VirtualAllocEx(ph, IntPtr.Zero, (UIntPtr)24, 0x3000, 4);
      if (rem == IntPtr.Zero) return false;
      UIntPtr n;
      WriteProcessMemory(ph, rem, buf, (UIntPtr)24, out n);
      IntPtr idx = SendMessage(lv, 0x1012, IntPtr.Zero, rem);          // LVM_HITTEST
      VirtualFreeEx(ph, rem, UIntPtr.Zero, 0x8000);
      return idx.ToInt32() >= 0;
    } finally { CloseHandle(ph); }
  }

  static IntPtr Callback(int code, IntPtr wParam, IntPtr lParam) {
    try {
      if (code >= 0 && Active && form != IntPtr.Zero && !Hold && IsWindowVisible(form)) {
        uint msg = (uint)wParam.ToInt32();
        if (msg == 0x200 || msg == 0x201 || msg == 0x202 || msg == 0x203 || msg == 0x204 || msg == 0x205 || msg == 0x20A) {
          MSLL ms = (MSLL)Marshal.PtrToStructure(lParam, typeof(MSLL));
          RECT r; GetWindowRect(form, out r);
          if (ms.pt.x >= r.l && ms.pt.x < r.r && ms.pt.y >= r.t && ms.pt.y < r.b) {
            IntPtr top = WindowFromPoint(ms.pt);
            string tc = top == IntPtr.Zero ? "" : Cls(top);
            if (tc == "SysListView32" || tc == "SHELLDLL_DefView" || tc == "WorkerW" || tc == "Progman") {
              if (msg != 0x200 && tc == "SysListView32" && OnIcon(top, ms.pt)) return CallNextHookEx(hook, code, wParam, lParam);
              IntPtr tgt = form; PT cp = ms.pt;
              for (int i = 0; i < 12; i++) {
                PT q = ms.pt; ScreenToClient(tgt, ref q);
                IntPtr ch = ChildWindowFromPointEx(tgt, q, 1);
                if (ch == IntPtr.Zero || ch == tgt) break;
                tgt = ch;
              }
              cp = ms.pt; ScreenToClient(tgt, ref cp);
              IntPtr lp = (IntPtr)((cp.y << 16) | (cp.x & 0xFFFF));
              if (msg == 0x20A) {
                PT sp = ms.pt; IntPtr wp = (IntPtr)((int)(ms.data & 0xFFFF0000));
                PostMessage(tgt, msg, wp, (IntPtr)((sp.y << 16) | (sp.x & 0xFFFF)));
              } else {
                IntPtr keys = (msg == 0x201 || msg == 0x203) ? (IntPtr)1 : (msg == 0x204) ? (IntPtr)2 : IntPtr.Zero;
                PostMessage(tgt, msg, keys, lp);
              }
              if (msg != 0x200) return (IntPtr)1;     // 이동은 그대로 흘려보내고, 클릭·휠은 달력이 처리했으니 아이콘 층에는 전달하지 않음
            }
          }
        }
      }
    } catch { }
    return CallNextHookEx(hook, code, wParam, lParam);
  }
}
"@

$ErrorActionPreference = 'Stop'
$SupaUrl = 'https://lrncoqzswsvacjppxqfn.supabase.co'
$SupaKey = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImxybmNvcXpzd3N2YWNqcHB4cWZuIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTIxMDQyMjQsImV4cCI6MjEwNzY4MDIyNH0.sBctkDBh_bVFwosLD7VcKm03IGvlX4FMUTrXVydN9bM'
$Utf8 = New-Object Text.UTF8Encoding($false)
$DefaultHolKey = '67a9dc30e4ac381425f675262f7c1bd2a35797d673cfe7e894907417f31c1c71'     # 내장 공용 API 키 (공공데이터포털 특일 정보)
$AppDir = if ($env:DEPT_CFGDIR) { $env:DEPT_CFGDIR } else { Join-Path $env:APPDATA 'DeptCalendar' }
$CfgPath = Join-Path $AppDir 'config.json'
$Palette = '#E5484D','#3E63DD','#30A46C','#F76B15','#8E4EC6','#12A594','#D6409F','#A18072','#6E7B8B','#AB6400','#0090FF','#C2410C'    # Radix 계열의 절제된 색
$FontName = '맑은 고딕'

function P($x,$y){ New-Object Drawing.Point ([int]$x),([int]$y) }
function S($w,$h){ New-Object Drawing.Size ([int]$w),([int]$h) }
$global:FontName = $FontName
function F($size,$style='Regular'){ New-Object Drawing.Font $global:FontName,([single]$size),([Drawing.FontStyle]$style) }
function HexColor($h){ [Drawing.ColorTranslator]::FromHtml($h) }
function RGB($r,$g,$b){ [Drawing.Color]::FromArgb($r,$g,$b) }
function Log-Error($msg){ try { [void](New-Item -ItemType Directory -Force -Path $AppDir); Add-Content -LiteralPath (Join-Path $AppDir 'error.log') -Value "$(Get-Date -Format s) $msg" -Encoding UTF8 } catch {} }
[Windows.Forms.Application]::SetUnhandledExceptionMode('CatchException')
[Windows.Forms.Application]::add_ThreadException({ param($s,$e)
  $pos=''; try { $pos = $e.Exception.ErrorRecord.InvocationInfo.PositionMessage } catch {}
  Log-Error ($e.Exception.Message + ' ' + ($pos -replace "`r?`n",' '))
})

# 한 PC에서 하나만 실행
$createdNew = $false
$mutex = New-Object Threading.Mutex($true, 'Local\DeptCalendarAppV2', [ref]$createdNew)
if (-not $createdNew -and -not $env:DEPT_SELFTEST) { exit }

# ---------------------------------------------------------------- 설정 / 암호 / 인증
$global:Cfg = [pscustomobject]@{ user=''; userId=''; rt=''; pinned=$true; theme='glass'; opacity=100; pkey=''; claim=''; x=0; y=0; w=0; h=0
  font='맑은 고딕'; fsize=9.5; fbold=$false; holKey=''; holApi=$true; showOwner=$true; showAnniv=$false; behind=$true }
if (Test-Path -LiteralPath $CfgPath) {
  try {
    $c = [IO.File]::ReadAllText($CfgPath,$Utf8) | ConvertFrom-Json
    foreach ($n in 'user','userId','rt','pinned','theme','opacity','pkey','claim','x','y','w','h','font','fsize','fbold','holKey','holApi','showOwner','showAnniv','behind') { if ($null -ne $c.$n) { $global:Cfg.$n = $c.$n } }
    if ($global:Cfg.font) { $global:FontName = [string]$global:Cfg.font }
    if ($null -eq $c.theme -and $null -ne $c.glass) { $global:Cfg.theme = if ($c.glass) { 'glass' } else { 'light' } }
  } catch {}
}
function Save-Cfg { if ($global:Cfg.user) { [void](New-Item -ItemType Directory -Force -Path $AppDir); [IO.File]::WriteAllText($CfgPath,($global:Cfg|ConvertTo-Json),$Utf8) } }
function Protect($s){ ConvertTo-SecureString $s -AsPlainText -Force | ConvertFrom-SecureString }
function Unprotect($e){ $ss = ConvertTo-SecureString $e; [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($ss)) }
function Sha256Hex($s){ ([BitConverter]::ToString((New-Object Security.Cryptography.SHA256Managed).ComputeHash($Utf8.GetBytes($s)))).Replace('-','').ToLower() }
function Rand-Bytes($n){ $b = New-Object byte[] $n; $r = New-Object Security.Cryptography.RNGCryptoServiceProvider; $r.GetBytes($b); ,$b }
function Rand-Hex($n){ $b = Rand-Bytes $n; ([BitConverter]::ToString([byte[]]$b)).Replace('-','').ToLower() }

# --- 개인 일정 암호화: AES-256-CBC + HMAC-SHA256 (키는 이 PC에만 보관, Windows 계정으로 보호)
$global:PKey = $null
function Get-PKey {
  if ($global:PKey) { return $global:PKey }
  if (-not $global:Cfg.pkey) { $global:Cfg.pkey = Protect ([Convert]::ToBase64String((Rand-Bytes 64))); Save-Cfg }
  $global:PKey = [Convert]::FromBase64String((Unprotect $global:Cfg.pkey))
  return $global:PKey
}
function Enc-Text($plain){
  $k = Get-PKey
  $aes = [Security.Cryptography.Aes]::Create(); $aes.Key = [byte[]]($k[0..31]); $aes.GenerateIV()
  $pb = $Utf8.GetBytes($plain)
  $ct = $aes.CreateEncryptor().TransformFinalBlock($pb,0,$pb.Length)
  $data = [byte[]]($aes.IV + $ct)
  $hm = New-Object Security.Cryptography.HMACSHA256 (,[byte[]]($k[32..63]))
  'enc1:' + [Convert]::ToBase64String([byte[]]($data + $hm.ComputeHash($data)))
}
function Dec-Text($s){
  try {
    if (-not $s -or -not ([string]$s).StartsWith('enc1:')) { return $null }
    $k = Get-PKey
    $all = [Convert]::FromBase64String(([string]$s).Substring(5))
    $data = [byte[]]($all[0..($all.Length-33)]); $mac = [byte[]]($all[($all.Length-32)..($all.Length-1)])
    $hm = New-Object Security.Cryptography.HMACSHA256 (,[byte[]]($k[32..63]))
    $calc = $hm.ComputeHash($data); $diff = 0
    for ($i=0; $i -lt 32; $i++) { $diff = $diff -bor ($calc[$i] -bxor $mac[$i]) }
    if ($diff -ne 0) { return $null }
    $aes = [Security.Cryptography.Aes]::Create(); $aes.Key = [byte[]]($k[0..31]); $aes.IV = [byte[]]($data[0..15])
    $ct = [byte[]]($data[16..($data.Length-1)])
    return $Utf8.GetString($aes.CreateDecryptor().TransformFinalBlock($ct,0,$ct.Length))
  } catch { return $null }
}

function Api-Raw($method,$path,$body,$bearer,$prefer){
  $r = [Net.HttpWebRequest]::Create($SupaUrl + $path)
  $r.Method = $method; $r.Timeout = 6000; $r.ReadWriteTimeout = 6000
  $r.Headers.Add('apikey',$SupaKey)
  if ($bearer) { $r.Headers.Add('Authorization',"Bearer $bearer") }
  if ($prefer) { $r.Headers.Add('Prefer',$prefer) }
  if ($null -ne $body) {
    $b = $Utf8.GetBytes($body); $r.ContentType = 'application/json; charset=utf-8'; $r.ContentLength = $b.Length
    $st = $r.GetRequestStream(); $st.Write($b,0,$b.Length); $st.Close()
  }
  try { $resp = $r.GetResponse() }
  catch [Net.WebException] { $resp = $_.Exception.Response; if (-not $resp) { throw } }
  $sr = New-Object IO.StreamReader($resp.GetResponseStream(),$Utf8)
  $text = $sr.ReadToEnd(); $sr.Close()
  $code = [int]$resp.StatusCode; $resp.Close()
  [pscustomobject]@{ S=$code; T=$text }
}

$global:Tok = @{ access=$null; exp=[datetime]::MinValue }
function Set-Session($json){
  $j = $json | ConvertFrom-Json
  $global:Tok.access = $j.access_token
  $global:Tok.exp = (Get-Date).AddSeconds([int]$j.expires_in)
  $global:Cfg.userId = $j.user.id
  $global:Cfg.rt = Protect $j.refresh_token
  Save-Cfg
}
function Ensure-Token {
  if ($global:Tok.access -and $global:Tok.exp -gt (Get-Date).AddMinutes(1)) { return }
  if ($global:Cfg.rt) {
    $rt = Unprotect $global:Cfg.rt
    $r = Api-Raw POST '/auth/v1/token?grant_type=refresh_token' (@{refresh_token=$rt}|ConvertTo-Json) $null $null
    if ($r.S -eq 200) { Set-Session $r.T; return }
    throw 'AUTH_LOST'
  }
  $r = Api-Raw POST '/auth/v1/signup' '{}' $null $null     # 익명 계정 자동 생성 (사용자는 로그인 불필요)
  if ($r.S -ne 200) { throw "SIGNUP_FAILED $($r.S) $($r.T)" }
  Set-Session $r.T
}
function Api($method,$path,$body,$prefer){ Ensure-Token; Api-Raw $method $path $body $global:Tok.access $prefer }
function Parse-Rows($text){
  # PowerShell 5.1 은 파이프로 JSON 배열을 한 덩어리로 넘기므로 파이프를 쓰지 않고 풀어서 반환
  $j = ConvertFrom-Json -InputObject $text
  if ($null -eq $j) { return @() }
  return @($j)
}

$global:HasSchema = $false     # 서버에 done/scope/claim_hash 열이 준비되었는지 (관리자 SQL 실행 여부)
function Check-Schema {
  try {
    $a = Api GET '/rest/v1/events?select=done,scope,files&limit=1'
    $b = Api GET '/rest/v1/members?select=claim_hash&limit=1'
    $global:HasSchema = ($a.S -eq 200 -and $b.S -eq 200)
  } catch {}
}
$SqlMsg = "개인 일정/완료 표시를 쓰려면 관리자가 Supabase SQL Editor에서 '서버설정.sql' 내용을 한 번 실행해야 합니다."

# 계정 복구용 값 준비 (암호키 + 이전 PC 복구 비밀값)
function Ensure-Identity {
  if (-not $global:HasSchema) { return }
  [void](Get-PKey)
  if (-not $global:Cfg.claim) {
    $secret = Rand-Hex 16
    try {
      $r = Api PATCH "/rest/v1/members?user_id=eq.$($global:Cfg.userId)" (@{claim_hash=(Sha256Hex $secret)}|ConvertTo-Json) 'return=representation'
      if ($r.S -eq 200 -and $r.T.Trim() -ne '[]') { $global:Cfg.claim = $secret; Save-Cfg }
    } catch {}
  }
}

function Show-NameDialog($default,$title,$allowRestore){
  $f = New-Object Windows.Forms.Form
  $f.Text=$title; $f.StartPosition='CenterScreen'; $f.ClientSize=(S 400 190)
  $f.FormBorderStyle='FixedDialog'; $f.MaximizeBox=$false; $f.MinimizeBox=$false; $f.Font=(F 10); $f.TopMost=$true
  $l = New-Object Windows.Forms.Label; $l.Text='이름을 입력하세요 (달력에 표시되는 이름)'; $l.Location=(P 15 15); $l.AutoSize=$true
  $t = New-Object Windows.Forms.TextBox; $t.Location=(P 15 45); $t.Width=370; $t.Font=(F 12); $t.Text=$default; $t.MaxLength=20
  $ok = New-Object Windows.Forms.Button; $ok.Text='확인'; $ok.Location=(P 200 95); $ok.Size=(S 90 32)
  $cn = New-Object Windows.Forms.Button; $cn.Text='취소'; $cn.Location=(P 295 95); $cn.Size=(S 90 32)
  $rs = New-Object Windows.Forms.Button; $rs.Text='이전 PC에서 쓰던 계정 복구 (복구 파일 선택)'; $rs.Location=(P 15 140); $rs.Size=(S 370 32)
  $rs.Visible = [bool]$allowRestore
  $ok.Add_Click({ if ($t.Text.Trim()) { $f.Tag=$t.Text.Trim(); $f.Close() } }.GetNewClosure())
  $cn.Add_Click({ $f.Close() }.GetNewClosure())
  $rs.Add_Click({ $f.Tag='__RESTORE__'; $f.Close() }.GetNewClosure())
  $f.AcceptButton=$ok; $f.Controls.AddRange(@($l,$t,$ok,$cn,$rs))
  [void]$f.ShowDialog()
  return $f.Tag
}

# 복구 파일 저장 / 복원 (PC를 바꿔도 내 일정과 개인 일정을 그대로 사용)
function Export-Account {
  Ensure-Identity
  if (-not $global:HasSchema -or -not $global:Cfg.claim) { [void][Windows.Forms.MessageBox]::Show($SqlMsg); return }
  $d = New-Object Windows.Forms.SaveFileDialog; $d.FileName="달력복구파일_$($global:Cfg.user).json"; $d.Filter='복구 파일 (*.json)|*.json'
  if ($d.ShowDialog() -ne 'OK') { return }
  $obj = [ordered]@{ app='DeptCalendar'; name=$global:Cfg.user; userId=$global:Cfg.userId; claim=$global:Cfg.claim; pkey=[Convert]::ToBase64String((Get-PKey)); notes=@($global:Notes) }
  [IO.File]::WriteAllText($d.FileName,($obj|ConvertTo-Json),$Utf8)
  [void][Windows.Forms.MessageBox]::Show("복구 파일을 저장했습니다.`n`n이 파일에는 개인 일정을 푸는 암호키가 들어 있습니다.`n본인만 볼 수 있는 곳(USB, 개인 클라우드)에 보관하고 남에게 주지 마세요.")
}
function Restore-Account {
  $d = New-Object Windows.Forms.OpenFileDialog; $d.Filter='복구 파일 (*.json)|*.json'; $d.Title='복구 파일 선택'
  if ($d.ShowDialog() -ne 'OK') { return $false }
  try {
    $o = [IO.File]::ReadAllText($d.FileName,$Utf8) | ConvertFrom-Json
    if ($o.app -ne 'DeptCalendar' -or -not $o.userId -or -not $o.claim -or -not $o.pkey) { throw '올바른 복구 파일이 아닙니다.' }
    Ensure-Token
    $r = Api POST '/rest/v1/rpc/claim_account' (@{old_id=$o.userId;secret=$o.claim}|ConvertTo-Json) $null
    if ($r.S -ne 200 -or $r.T.Trim() -ne 'true') { throw "계정 복구에 실패했습니다. ($($r.S)) 서버 설정(SQL)이 실행되었는지, 파일이 맞는지 확인하세요." }
    $global:Cfg.user = [string]$o.name; $global:Cfg.claim = [string]$o.claim
    $global:Cfg.pkey = Protect ([string]$o.pkey); $global:PKey = $null
    Save-Cfg
    if ($o.notes) {       # 바탕화면 메모도 함께 복원
      try { [IO.File]::WriteAllText((Join-Path $AppDir 'notes.json'),(ConvertTo-Json -InputObject @($o.notes) -Depth 4),$Utf8) } catch {}
    }
    return $true
  } catch { [void][Windows.Forms.MessageBox]::Show($_.Exception.Message); return $false }
}

# 최초 실행: 이름만 등록
if (-not $global:Cfg.user) {
  try { Ensure-Token; Check-Schema } catch { [void][Windows.Forms.MessageBox]::Show("서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.`n$($_.Exception.Message)"); exit }
  while ($true) {
    $name = if ($env:DEPT_SELFTEST) { '__selftest' } else { Show-NameDialog '' '부서 일정 달력 - 최초 1회 설정' $true }
    if (-not $name) { exit }
    if ($name -eq '__RESTORE__') { if (Restore-Account) { break } else { continue } }
    try {
      $used = @(Parse-Rows (Api GET '/rest/v1/members?select=color').T | ForEach-Object { $_.color })
      $col = $Palette | Where-Object { $used -notcontains $_ } | Select-Object -First 1
      if (-not $col) { $col = $Palette[(Get-Random -Maximum $Palette.Count)] }
      $row = [ordered]@{ name=$name; color=$col }
      $secret = ''
      if ($global:HasSchema) { $secret = Rand-Hex 16; $row.claim_hash = Sha256Hex $secret }
      $r = Api POST '/rest/v1/members' ($row|ConvertTo-Json) 'return=minimal'
    } catch { [void][Windows.Forms.MessageBox]::Show("서버 연결 실패: $($_.Exception.Message)"); exit }
    if ($r.S -eq 201) { $global:Cfg.user = $name; $global:Cfg.claim = $secret; Save-Cfg; break }
    if ($r.S -eq 409) { [void][Windows.Forms.MessageBox]::Show("'$name' 이름은 이미 사용 중입니다.`n다른 이름을 입력하세요.`n(본인 이름인데 PC를 바꾼 경우: 이전 PC에서 저장한 복구 파일로 복구하세요)"); continue }
    [void][Windows.Forms.MessageBox]::Show("등록 실패 ($($r.S))`n$($r.T)"); exit
  }
}

# ---------------------------------------------------------------- 데이터 / 동기화
$global:Events = @{}      # id -> 일정
$global:Users  = @{}      # 이름 -> 색상(hex)
$global:LastRaw = ''
$global:FirstLoad = $true
$global:Online = $true
$global:AuthLost = $false
$global:View = (Get-Date -Day 1).Date
$global:Selected = (Get-Date).Date
$global:Filter = 'all'    # all | dept | personal
$global:Hits = New-Object Collections.ArrayList
$global:Geo = $null
$global:Pending = New-Object Collections.ArrayList

function Convert-Row($r){
  $scope = if ($r.scope) { [string]$r.scope } else { 'dept' }
  $o = [pscustomobject]@{
    id=$r.id; ownerId=$r.owner_id; owner=$r.owner_name; title=$r.title; place=[string]$r.place; kind=$r.kind
    start=$r.start_date; end=$r.end_date; time=[string]$r.time_text; memo=[string]$r.memo; done=[bool]$r.done; scope=$scope
    files=@($r.files | Where-Object { $_ })
    created=[DateTimeOffset]::Parse($r.created_at,[Globalization.CultureInfo]::InvariantCulture).UtcTicks
    updated=$r.updated_at
  }
  if ($scope -eq 'personal') {
    $j = Dec-Text $r.memo
    if ($j) {
      $p = ConvertFrom-Json -InputObject $j
      $o.title=[string]$p.t; $o.place=[string]$p.p; $o.time=[string]$p.tm; $o.memo=[string]$p.m; $o.kind=[string]$p.k
    } else { $o.title='(개인 일정 - 이 PC에서는 풀 수 없음. 복구 파일 필요)'; $o.place=''; $o.time=''; $o.memo='' }
  }
  $o
}

function Sync-Data {
  try {
    $since = (Get-Date).AddDays(-400).ToString('yyyy-MM-dd')
    $mr = Api GET '/rest/v1/members?select=user_id,name,color'
    $er = Api GET "/rest/v1/events?select=*&end_date=gte.$since&order=created_at.asc"
    if ($mr.S -ne 200 -or $er.S -ne 200) { throw "HTTP $($mr.S)/$($er.S)" }
    $global:Online = $true; $global:AuthLost = $false
  } catch {
    $global:Online = $false
    if ($_.Exception.Message -eq 'AUTH_LOST') { $global:AuthLost = $true }
    return $false
  }
  $raw = $mr.T + '|' + $er.T
  if ($raw -eq $global:LastRaw) { return $false }
  $global:LastRaw = $raw

  $users = @{}
  foreach ($m in (Parse-Rows $mr.T)) { $users[$m.name] = $m.color }
  $global:Users = $users

  $new = @{}
  foreach ($r in (Parse-Rows $er.T)) {
    $o = Convert-Row $r; $new[$o.id] = $o
    $old = $global:Events[$o.id]
    if (-not $global:FirstLoad -and $o.scope -eq 'dept' -and $o.ownerId -ne $global:Cfg.userId -and (-not $old -or $old.updated -ne $o.updated)) {
      [void]$global:Pending.Add([pscustomobject]@{ New=(-not $old); Ev=$o })
    }
  }
  $global:Events = $new
  $global:FirstLoad = $false
  return $true
}

function UserColor($name){
  if ($global:Users.ContainsKey($name) -and $global:Users[$name]) { return (HexColor $global:Users[$name]) }
  [Drawing.Color]::Gray
}
function Parse-Day($s){ [DateTime]::ParseExact($s,'yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture) }
function Is-Mine($ev){ $ev.ownerId -eq $global:Cfg.userId }
function Visible-Events { @($global:Events.Values | Where-Object { $global:Filter -eq 'all' -or $_.scope -eq $global:Filter }) }

# ---------------------------------------------------------------- 음력 / 공휴일 / 테마
$global:LunCal = New-Object Globalization.KoreanLunisolarCalendar
$global:HolCache = @{}
function Lunar-Text($d){
  try {
    $c=$global:LunCal; $y=$c.GetYear($d); $m=$c.GetMonth($d); $dd=$c.GetDayOfMonth($d); $leap=$c.GetLeapMonth($y)
    $pre=''; if ($leap -gt 0 -and $m -ge $leap) { if ($m -eq $leap) { $pre='윤' }; $m-- }
    return "(음)$pre$m.$dd"
  } catch { return '' }
}
function Lunar-ToSolar($y,$m,$d){
  $leap=$global:LunCal.GetLeapMonth($y); $i = if ($leap -gt 0 -and $m -ge $leap) { $m+1 } else { $m }
  $global:LunCal.ToDateTime($y,$i,$d,0,0,0,0).Date
}
function Add-Hol($h,$d,$n){
  $k=$d.ToString('yyyy-MM-dd'); if (-not $h.ContainsKey($k)) { $h[$k]=New-Object Collections.ArrayList }; [void]$h[$k].Add($n)
}
function Get-Holidays($y){
  if ($global:HolCache.ContainsKey($y)) { return $global:HolCache[$y] }
  $h=@{}; $subDates=New-Object Collections.ArrayList; $blocks=New-Object Collections.ArrayList
  foreach ($f in @(@(1,1,'신정',0),@(3,1,'삼일절',1),@(5,5,'어린이날',1),@(6,6,'현충일',0),@(8,15,'광복절',1),@(10,3,'개천절',1),@(10,9,'한글날',1),@(12,25,'성탄절',1))) {
    $d=New-Object DateTime $y,$f[0],$f[1]; Add-Hol $h $d $f[2]; if ($f[3]) { [void]$subDates.Add($d) }
  }
  $bd=Lunar-ToSolar $y 4 8; Add-Hol $h $bd '부처님오신날'; [void]$subDates.Add($bd)
  foreach ($b in @(@(1,1,'설날'),@(8,15,'추석'))) {
    $c=Lunar-ToSolar $y $b[0] $b[1]
    Add-Hol $h $c.AddDays(-1) "$($b[2]) 연휴"; Add-Hol $h $c $b[2]; Add-Hol $h $c.AddDays(1) "$($b[2]) 연휴"
    [void]$blocks.Add($c)
  }
  if ($y -eq 2026) { Add-Hol $h (New-Object DateTime 2026,6,3) '지방선거일' }
  if ($y -ge 2021) {
    foreach ($c in $blocks) {      # 설날/추석 연휴가 일요일과 겹치면 연휴 다음 평일이 대체 휴일
      if ((@(-1,0,1) | Where-Object { $c.AddDays($_).DayOfWeek -eq 'Sunday' }).Count -gt 0) {
        $s=$c.AddDays(2); while ($s.DayOfWeek -in 'Saturday','Sunday' -or $h.ContainsKey($s.ToString('yyyy-MM-dd'))) { $s=$s.AddDays(1) }
        Add-Hol $h $s '대체 휴일'
      }
    }
    foreach ($d in ($subDates | Sort-Object)) {   # 주말/다른 공휴일과 겹치면 다음 평일이 대체 휴일
      if ($d.DayOfWeek -in 'Saturday','Sunday' -or $h[$d.ToString('yyyy-MM-dd')].Count -gt 1) {
        $s=$d.AddDays(1); while ($s.DayOfWeek -in 'Saturday','Sunday' -or $h.ContainsKey($s.ToString('yyyy-MM-dd'))) { $s=$s.AddDays(1) }
        Add-Hol $h $s '대체 휴일'
      }
    }
  }
  $global:HolCache[$y]=$h
  return $h
}
# ---- 공휴일/절기/기념일: 공공데이터포털 '한국천문연구원 특일 정보' API (키가 있으면 사용, 없거나 실패하면 내장 계산 사용)
$global:HolApi = @{}          # 'yyyy-MM' -> @{ stamp; rest=@{날짜=이름목록}; etc=@{날짜=이름목록} }
$global:HolTmp = @{}
$global:HolQueue = New-Object Collections.ArrayList
$global:HolErr = ''
$HolCachePath = Join-Path $AppDir 'holidays.json'
$global:HolOps = 'getRestDeInfo','getAnniversaryInfo','get24DivisionsInfo','getSundryDayInfo'
function Add-NameTo($map,$d,$n){ if (-not $map.ContainsKey($d)) { $map[$d]=New-Object Collections.ArrayList }; if (-not $map[$d].Contains($n)) { [void]$map[$d].Add($n) } }
function Load-HolCache {
  if (-not (Test-Path -LiteralPath $HolCachePath)) { return }
  try {
    foreach ($m in (Parse-Rows ([IO.File]::ReadAllText($HolCachePath,$Utf8)))) {
      $rest=@{}; foreach ($x in @($m.rest)) { if ($x) { Add-NameTo $rest ([string]$x.d) ([string]$x.n) } }
      $etc=@{};  foreach ($x in @($m.etc))  { if ($x) { Add-NameTo $etc  ([string]$x.d) ([string]$x.n) } }
      $anv=@{};  foreach ($x in @($m.anniv)) { if ($x) { Add-NameTo $anv  ([string]$x.d) ([string]$x.n) } }
      $global:HolApi[[string]$m.ym]=@{ stamp=[string]$m.stamp; rest=$rest; etc=$etc; anniv=$anv }
    }
  } catch { Log-Error "holcache: $($_.Exception.Message)" }
}
function Save-HolCache {
  $out=@()
  foreach ($ym in $global:HolApi.Keys) {
    $m=$global:HolApi[$ym]
    $r=@(); foreach ($d in $m.rest.Keys) { foreach ($n in $m.rest[$d]) { $r += @{d=$d;n=$n} } }
    $e=@(); foreach ($d in $m.etc.Keys)  { foreach ($n in $m.etc[$d])  { $e += @{d=$d;n=$n} } }
    $a=@(); if ($m.anniv) { foreach ($d in $m.anniv.Keys) { foreach ($n in $m.anniv[$d]) { $a += @{d=$d;n=$n} } } }
    $out += @{ ym=$ym; stamp=$m.stamp; rest=$r; etc=$e; anniv=$a }
  }
  try { [void](New-Item -ItemType Directory -Force -Path $AppDir); [IO.File]::WriteAllText($HolCachePath,(ConvertTo-Json -InputObject @($out) -Depth 6),$Utf8) } catch {}
}
function Convert-HolItems($text){
  $t = $text.Trim()
  if ($t.StartsWith('<')) {
    $msg=''
    if ($t -match '<returnAuthMsg>([^<]+)<') { $msg=$Matches[1] } elseif ($t -match '<(?:errMsg|resultMsg)>([^<]+)<') { $msg=$Matches[1] }
    if ($msg -match 'NOT_REGISTERED|UNREGISTERED') { $msg += ' (인증키가 등록되지 않았거나 활용신청이 승인되지 않았습니다)' }
    throw "API 오류: $msg"
  }
  $j = ConvertFrom-Json -InputObject $t
  $code = [string]$j.response.header.resultCode
  if ($code -and $code -ne '00') { throw "API 오류($code): $($j.response.header.resultMsg)" }
  $items = $j.response.body.items
  if ($null -eq $items -or $items -is [string]) { return @() }
  return @($items.item | Where-Object { $_ })
}
function Hol-Request($year,$month,$op,$keyOverride){
  $key = if ($keyOverride) { [string]$keyOverride } else { Get-HolKey }
  $k = if ($key -match '%') { $key } else { [Uri]::EscapeDataString($key) }
  $url = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/$op" + "?serviceKey=$k&solYear=$year&solMonth=$('{0:00}' -f [int]$month)&_type=json&numOfRows=100"
  $r = [Net.HttpWebRequest]::Create($url); $r.Timeout=6000; $r.ReadWriteTimeout=6000; $r.UserAgent='DeptCalendar'
  try { $resp = $r.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response; if (-not $resp) { throw } }
  $sr = New-Object IO.StreamReader($resp.GetResponseStream(),$Utf8); $text = $sr.ReadToEnd(); $sr.Close(); $resp.Close()
  Convert-HolItems $text
}
function Get-HolKey { if ($global:Cfg.holKey) { [string]$global:Cfg.holKey } else { $DefaultHolKey } }
function Queue-Holidays($year){
  if (-not (Get-HolKey) -or -not $global:Cfg.holApi) { return }
  foreach ($m in 1..12) {
    $ym = "{0}-{1:00}" -f $year,$m
    $c = $global:HolApi[$ym]
    $fresh = $false
    if ($c -and $c.stamp) { try { $fresh = (((Get-Date) - [datetime]::Parse($c.stamp)).TotalDays -lt 30) } catch {} }
    if ($fresh) { continue }
    if (@($global:HolQueue | Where-Object { $_.ym -eq $ym }).Count -gt 0) { continue }
    $global:HolTmp[$ym] = @{ rest=@{}; etc=@{}; anniv=@{}; n=0 }
    foreach ($op in $global:HolOps) { [void]$global:HolQueue.Add([pscustomobject]@{ ym=$ym; year=$year; month=$m; op=$op }) }
  }
}
$holTimer = New-Object Windows.Forms.Timer; $holTimer.Interval = 400
$holTimer.Add_Tick({
  if ($global:HolQueue.Count -eq 0) { $holTimer.Stop(); return }
  $q = $global:HolQueue[0]; $global:HolQueue.RemoveAt(0)
  try {
    $items = Hol-Request $q.year $q.month $q.op $null
    $tmp = $global:HolTmp[$q.ym]
    foreach ($it in $items) {
      $ld = [string]$it.locdate; $d = "{0}-{1}-{2}" -f $ld.Substring(0,4),$ld.Substring(4,2),$ld.Substring(6,2)
      $nm = [string]$it.dateName
      if ($q.op -eq 'getRestDeInfo') { if ([string]$it.isHoliday -eq 'Y') { Add-NameTo $tmp.rest $d $nm } }
      elseif ($q.op -eq 'getAnniversaryInfo') { Add-NameTo $tmp.anniv $d $nm }
      else { Add-NameTo $tmp.etc $d $nm }
    }
    $tmp.n++
    if ($tmp.n -ge $global:HolOps.Count) {
      $global:HolApi[$q.ym] = @{ stamp=(Get-Date).ToString('s'); rest=$tmp.rest; etc=$tmp.etc; anniv=$tmp.anniv }
      Save-HolCache; $grid.Invalidate()
    }
    $global:HolErr = ''
  } catch {
    $global:HolErr = $_.Exception.Message; $global:HolQueue.Clear(); $holTimer.Stop(); Log-Error "holiday: $($global:HolErr)"
  }
})
function Hol-Name($d){
  $k=$d.ToString('yyyy-MM-dd'); $m=$global:HolApi[$d.ToString('yyyy-MM')]
  if ($m) { if ($m.rest.ContainsKey($k)) { return ($m.rest[$k] -join '/') }; return $null }     # API 데이터가 있는 달은 API 기준
  $h=Get-Holidays $d.Year
  if ($h.ContainsKey($k)) { return ($h[$k] -join '/') }
  return $null
}
function Etc-Name($d){
  $m=$global:HolApi[$d.ToString('yyyy-MM')]; if (-not $m) { return $null }
  $k=$d.ToString('yyyy-MM-dd'); $names=@()
  if ($m.etc.ContainsKey($k)) { $names += @($m.etc[$k]) }                                                   # 절기·잡절
  if ($global:Cfg.showAnniv -and $m.anniv -and $m.anniv.ContainsKey($k)) { $names += @($m.anniv[$k]) }      # 기념일(설정에서 켠 경우만)
  if ($names.Count) { return ($names -join '·') }
  return $null
}
function Today-Text {
  $n=Get-Date; $dow='일','월','화','수','목','금','토'
  "오늘은 $($n.ToString('yyyy년MM월dd일')) $($dow[[int]$n.DayOfWeek])요일  $(Lunar-Text $n.Date)"
}
function Lighten($c,$f){ [Drawing.Color]::FromArgb([int]($c.R+(255-$c.R)*$f),[int]($c.G+(255-$c.G)*$f),[int]($c.B+(255-$c.B)*$f)) }
function RGBA($a,$r,$g,$b){ [Drawing.Color]::FromArgb($a,$r,$g,$b) }
# 라이트 = Notion(따뜻한 화이트·얇은 선·절제된 색) / 다크 = Linear(깊은 차콜 + 인디고 포인트) / 유리 = Windows 11(바탕화면이 비치는 반투명)
$global:Themes = @{
  light = @{ bg=(RGB 255 255 255); other=(RGB 251 250 247); hdr=(RGB 243 240 235); sel=(RGB 236 242 255); wk=(RGB 254 250 244)
             line=(RGB 236 231 223); txt=(RGB 62 58 54); dim=(RGB 166 159 150); hdrTxt=(RGB 122 115 107)
             sun=(RGB 226 84 84); sat=(RGB 66 126 230); hol=(RGB 214 142 50); glow=(RGB 99 102 241); pillTxt=(RGB 255 255 255)
             panel=(RGB 246 244 240); legend=(RGB 246 244 240); btnSel=(RGB 233 228 220); btnHover=(RGB 240 236 229); lighten=0.0; chip=36 }
  dark  = @{ bg=(RGB 36 39 48); other=(RGB 29 31 39); hdr=(RGB 44 47 58); sel=(RGB 52 57 84); wk=(RGB 41 43 54)
             line=(RGB 54 58 72); txt=(RGB 236 238 242); dim=(RGB 142 148 162); hdrTxt=(RGB 176 182 196)
             sun=(RGB 255 130 136); sat=(RGB 132 170 255); hol=(RGB 242 186 102); glow=(RGB 129 140 248); pillTxt=(RGB 255 255 255)
             panel=(RGB 24 26 32); legend=(RGB 24 26 32); btnSel=(RGB 56 60 78); btnHover=(RGB 44 47 60); lighten=0.40; chip=52 }
  glass = @{ bg=(RGBA 70 24 34 52); other=(RGBA 110 10 16 30); hdr=(RGBA 96 12 20 40); sel=(RGBA 150 70 100 150); wk=(RGBA 80 96 70 60)
             line=(RGBA 46 255 255 255); txt=(RGB 255 255 255); dim=(RGB 200 212 224); hdrTxt=(RGB 238 244 250)
             sun=(RGB 255 156 156); sat=(RGB 166 200 255); hol=(RGB 255 200 118); glow=(RGB 140 182 255); pillTxt=(RGB 12 22 40)
             panel=(RGB 30 50 70); legend=(RGB 30 50 70); btnSel=(RGB 80 106 142); btnHover=(RGB 66 90 124); lighten=0.55; chip=56 }
}
$global:T = $global:Themes.glass
function DrawColor($name){ $c=UserColor $name; $f=[double]$global:T.lighten; if ($f -gt 0) { Lighten $c $f } else { $c } }

# ---------------------------------------------------------------- 저장 / 삭제
function Build-Row($L){
  $scope = if ($L.scope) { [string]$L.scope } else { 'dept' }
  $row = [ordered]@{ owner_name=$global:Cfg.user }
  if ($scope -eq 'personal') {      # 개인 일정: 내용 전체를 암호화해서 memo 에 저장 (서버에는 날짜만 평문)
    $payload = ConvertTo-Json -Compress -InputObject @{ t=$L.title; p=$L.place; tm=$L.time; m=$L.memo; k=$L.kind }
    $row.title='개인 일정'; $row.place=''; $row.kind='개인'; $row.time_text=''; $row.memo=(Enc-Text $payload)
  } else {
    $row.title=$L.title; $row.place=$L.place; $row.kind=$L.kind; $row.time_text=$L.time; $row.memo=$L.memo
  }
  $row.start_date=$L.start; $row.end_date=$L.end
  if ($global:HasSchema) {
    $row.scope=$scope; $row.done=[bool]$L.done
    $fl = @()
    if ($scope -eq 'dept') { $fl = @($L.files | Where-Object { $_ }) }      # 첨부파일은 부서 일정에만 (빈 목록은 null 이 아니라 [] 로 보내야 함)
    $row.files = $fl
  }
  return $row
}
function Save-Event($existing,$L){
  if (-not $global:HasSchema -and $L.scope -eq 'personal') { [void][Windows.Forms.MessageBox]::Show($SqlMsg); return $false }
  if ($L.scope -ne 'personal' -and $L.conflict -ne $false) {       # 같은 장소로 이미 등록된 부서 일정이 있으면 확인 (중복 예약 방지)
    $cf = @(Find-Conflicts $L $(if ($existing) { $existing.id } else { '' }))
    if ($cf.Count -gt 0) {
      $lines = ($cf | Select-Object -First 6 | ForEach-Object {
        $e=$_.ev; $when=$e.start; if ($e.end -ne $e.start) { $when += "~$($e.end)" }
        "· $when $($e.time)  ($($e.place)) $($e.title)  - $($e.owner)" + $(if ($_.timeHit) { '   ◀ 시간도 겹침' } else { '' }) }) -join "`n"
      $q = [Windows.Forms.MessageBox]::Show("장소 '$($L.place)'(으)로 이미 등록된 부서 일정이 있습니다.`n`n$lines`n`n그래도 저장할까요?",'장소 중복 확인','YesNo','Warning')
      if ($q -ne 'Yes') { return $false }
    }
  }
  try {
    $row = Build-Row $L
    if (-not $existing) {
      $r = Api POST '/rest/v1/events' ($row|ConvertTo-Json -Depth 6) 'return=representation'
      $ok = ($r.S -eq 201)
      if ($ok) { try { $global:LastSavedId = (Parse-Rows $r.T)[0].id } catch {} }
    } else {
      $row.updated_at = (Get-Date).ToUniversalTime().ToString('o')
      $r = Api PATCH "/rest/v1/events?id=eq.$($existing.id)" ($row|ConvertTo-Json -Depth 6) 'return=representation'
      $ok = ($r.S -eq 200 -and $r.T.Trim() -ne '[]')
    }
  } catch { Log-Error "Save-Event exception: $($_.Exception.Message) @ $($_.InvocationInfo.PositionMessage)"; [void][Windows.Forms.MessageBox]::Show("저장 실패: $($_.Exception.Message)"); return $false }
  if (-not $ok) { Log-Error "Save-Event failed: status=$($r.S) body=$($r.T)"; [void][Windows.Forms.MessageBox]::Show("저장 실패 ($($r.S))`n본인이 작성한 일정만 수정할 수 있습니다.") }
  return $ok
}
function Delete-Event($ev){
  try { $r = Api DELETE "/rest/v1/events?id=eq.$($ev.id)" $null 'return=representation' } catch { [void][Windows.Forms.MessageBox]::Show('삭제 실패: 서버에 연결할 수 없습니다.'); return $false }
  if ($r.S -ne 200 -or $r.T.Trim() -eq '[]') { [void][Windows.Forms.MessageBox]::Show('삭제할 수 없습니다. (본인 일정만 삭제 가능)'); return $false }
  return $true
}
function Set-Done($ev,$val){
  if (-not $global:HasSchema) { [void][Windows.Forms.MessageBox]::Show($SqlMsg); return }
  try { $r = Api PATCH "/rest/v1/events?id=eq.$($ev.id)" (@{done=$val}|ConvertTo-Json) 'return=representation' } catch { return }
  if ($r.S -ne 200 -or $r.T.Trim() -eq '[]') { [void][Windows.Forms.MessageBox]::Show('본인이 작성한 일정만 완료 표시를 바꿀 수 있습니다.') }
  Refresh-All
}

# ---------------------------------------------------------------- 첨부파일 (공문 등) - Supabase Storage
$global:BlockedExt = '.exe','.bat','.cmd','.com','.scr','.msi','.ps1','.vbs','.vbe','.js','.jse','.wsf','.lnk','.reg','.dll','.jar','.hta'
$global:OpenExt = '.hwp','.hwpx','.pdf','.doc','.docx','.xls','.xlsx','.ppt','.pptx','.txt','.csv','.jpg','.jpeg','.png','.gif','.bmp'
function Upload-File($path){
  $fi = Get-Item -LiteralPath $path
  $ext = $fi.Extension.ToLower()
  if ($global:BlockedExt -contains $ext) { [void][Windows.Forms.MessageBox]::Show("보안상 실행 파일($ext)은 첨부할 수 없습니다."); return $null }
  if ($fi.Length -gt 20MB) { [void][Windows.Forms.MessageBox]::Show("20MB 이하 파일만 첨부할 수 있습니다.`n($($fi.Name))"); return $null }
  $obj = "$($global:Cfg.userId)/$([guid]::NewGuid().ToString('N'))$($ext -replace '[^a-z0-9.]','')"
  Ensure-Token
  $bytes = [IO.File]::ReadAllBytes($fi.FullName)
  $r = [Net.HttpWebRequest]::Create("$SupaUrl/storage/v1/object/attachments/$obj")
  $r.Method='POST'; $r.Timeout=120000; $r.ReadWriteTimeout=120000
  $r.Headers.Add('apikey',$SupaKey); $r.Headers.Add('Authorization',"Bearer $($global:Tok.access)")
  $r.ContentType='application/octet-stream'; $r.ContentLength=$bytes.Length
  $st=$r.GetRequestStream(); $st.Write($bytes,0,$bytes.Length); $st.Close()
  try { $resp=$r.GetResponse() } catch [Net.WebException] { $resp=$_.Exception.Response; if (-not $resp) { throw } }
  $code=[int]$resp.StatusCode; $resp.Close()
  if ($code -ne 200) { [void][Windows.Forms.MessageBox]::Show("업로드 실패 ($code)`n$SqlMsg"); return $null }
  return @{ name=$fi.Name; path=$obj; size=$fi.Length }
}
function Download-File($f,$dest){
  Ensure-Token
  $r = [Net.HttpWebRequest]::Create("$SupaUrl/storage/v1/object/authenticated/attachments/$($f.path)")
  $r.Timeout=120000; $r.ReadWriteTimeout=120000
  $r.Headers.Add('apikey',$SupaKey); $r.Headers.Add('Authorization',"Bearer $($global:Tok.access)")
  $resp=$r.GetResponse(); $in=$resp.GetResponseStream(); $out=[IO.File]::Create($dest)
  $in.CopyTo($out); $out.Close(); $in.Close(); $resp.Close()
}
function Remove-Files($paths){
  $p=@($paths | Where-Object { $_ }); if ($p.Count -eq 0) { return }
  try { [void](Api DELETE '/storage/v1/object/attachments' (ConvertTo-Json -InputObject @{prefixes=$p})) } catch {}
}

# ---------------------------------------------------------------- 토스트 (왼쪽 아래)
$global:Toasts = New-Object Collections.ArrayList
function Show-Toast($color,$head,$body){
  $wa = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
  $h = 88
  $slot = 0
  while (@($global:Toasts | Where-Object { $_.Tag -eq $slot }).Count -gt 0) { $slot++ }
  $f = New-Object Windows.Forms.Form
  $f.FormBorderStyle='None'; $f.ShowInTaskbar=$false; $f.TopMost=$true; $f.StartPosition='Manual'
  $f.Size=(S 350 $h); $f.BackColor=[Drawing.Color]::White; $f.Tag=$slot; $f.Font=(F 9.5)
  $f.Location=(P ($wa.Left+12) ($wa.Bottom - ($h+10)*($slot+1) - 2))
  $bar = New-Object Windows.Forms.Panel; $bar.Dock='Left'; $bar.Width=8; $bar.BackColor=$color
  $l1 = New-Object Windows.Forms.Label; $l1.Text=$head; $l1.Font=(F 10 'Bold'); $l1.ForeColor=$color
  $l1.Location=(P 18 8); $l1.Size=(S 320 22); $l1.AutoEllipsis=$true
  $l2 = New-Object Windows.Forms.Label; $l2.Text=$body; $l2.ForeColor=[Drawing.Color]::FromArgb(40,40,40)
  $l2.Location=(P 18 32); $l2.Size=(S 320 50); $l2.AutoEllipsis=$true
  $f.Controls.AddRange(@($bar,$l1,$l2))
  $f.Add_Paint({ param($s,$e) $e.Graphics.DrawRectangle([Drawing.Pens]::Silver,0,0,$s.Width-1,$s.Height-1) })
  $close = { $f.Close() }.GetNewClosure()
  foreach ($c in @($f,$bar,$l1,$l2)) { $c.Add_Click($close) }
  $t = New-Object Windows.Forms.Timer; $t.Interval=8000
  $t.Add_Tick({ $t.Stop(); $f.Close() }.GetNewClosure())
  $f.Add_FormClosed({ $t.Dispose(); [void]$global:Toasts.Remove($f) }.GetNewClosure())
  [void]$global:Toasts.Add($f)
  [void]$f.Handle
  [void][Win]::ShowWindow($f.Handle, 4)   # 포커스를 뺏지 않고 표시
  [Media.SystemSounds]::Asterisk.Play()
  $t.Start()
}
function Flush-Pending {
  $items = @($global:Pending); $global:Pending.Clear()
  $shown = 0
  foreach ($p in $items) {
    if ($shown -ge 3) { Show-Toast ([Drawing.Color]::DimGray) '새 소식' "외 $($items.Count-3)건의 일정 변경이 있습니다."; break }
    $ev = $p.Ev
    $when = $ev.start; if ($ev.end -ne $ev.start) { $when += " ~ $($ev.end)" }; if ($ev.time) { $when += "  $($ev.time)" }
    $verb = if ($p.New) { '새 부서 일정을 등록했습니다' } else { '부서 일정을 수정했습니다' }
    $pl = if ($ev.place) { " @$($ev.place)" } else { '' }
    Show-Toast (UserColor $ev.owner) "$($ev.owner) 님이 $verb" "[$($ev.kind)] $($ev.title)$pl`n$when"
    $shown++
  }
}

# ---------------------------------------------------------------- 일정 대화상자
function Split-Time($t){
  $time=''
  if ($t -match '^(\d{1,2}):(\d{2})\s*[~\-]\s*(\d{1,2}):(\d{2})\)?\s*(.*)$') { $time='{0:00}:{1:00}~{2:00}:{3:00}' -f [int]$Matches[1],[int]$Matches[2],[int]$Matches[3],[int]$Matches[4]; $t=$Matches[5] }
  elseif ($t -match '^(\d{1,2}):(\d{2})\)?\s*(.*)$') { $time='{0:00}:{1:00}' -f [int]$Matches[1],[int]$Matches[2]; $t=$Matches[3] }
  [pscustomobject]@{ time=$time; title=([string]$t).Trim() }
}

# ---------------------------------------------------------------- 개인 메모 입력 팝업 (바탕화면 달력 방식: 날짜 칸을 누르면 1. 2. 3. 줄 단위로 입력)
function Show-DayEditor($day,$focusId){
  if (-not $global:HasSchema) { [void][Windows.Forms.MessageBox]::Show($SqlMsg); return }
  if ($global:EditorOpen) { return }
  $global:EditorOpen = $true; $global:EditorSaving = $false
  $dd=$day.Date; $ds=$dd.ToString('yyyy-MM-dd')
  $mine=@($global:Events.Values | Where-Object { $_.scope -eq 'personal' -and (Is-Mine $_) -and $_.start -eq $ds -and $_.end -eq $ds } | Sort-Object created,id)
  $dark = ($global:Cfg.theme -ne 'light')
  $cardBg = if ($dark) { RGB 40 44 56 } else { RGB 255 255 255 }
  $txtBg  = if ($dark) { RGB 30 33 43 } else { RGB 246 246 249 }
  $fg     = if ($dark) { RGB 236 239 245 } else { RGB 40 40 46 }
  $dim    = if ($dark) { RGB 130 136 150 } else { RGB 160 160 168 }
  $fs     = [math]::Max(9.5,[double]$global:Cfg.fsize+1)
  $fNormal = F $fs; $fStrike = F $fs 'Strikeout'
  $f = New-Object Windows.Forms.Form
  $f.FormBorderStyle='None'; $f.ShowInTaskbar=$false; $f.StartPosition='Manual'; $f.TopMost=$true; $f.BackColor=$cardBg; $f.Font=(F 10)
  $f.Size=(S 320 390)
  $cur=[Windows.Forms.Cursor]::Position; $wa=[Windows.Forms.Screen]::FromPoint($cur).WorkingArea
  $f.Location=(P ([math]::Max($wa.Left,[math]::Min($cur.X-30,$wa.Right-320))) ([math]::Max($wa.Top,[math]::Min($cur.Y-40,$wa.Bottom-390))))
  $hd = New-Object Windows.Forms.Panel; $hd.Dock='Top'; $hd.Height=44; $hd.BackColor=$cardBg
  $bar = New-Object Windows.Forms.Panel; $bar.Location=(P 14 14); $bar.Size=(S 4 17); $bar.BackColor=$global:T.glow
  $hn = Hol-Name $dd; $sub = (Lunar-Text $dd); if ($hn) { $sub += "  $hn" }
  $lt = New-Object Windows.Forms.Label; $lt.Text=$dd.ToString('yyyy년M월d일'); $lt.Font=(F 11.5 'Bold'); $lt.ForeColor=$fg; $lt.Location=(P 26 11); $lt.AutoSize=$true
  $ls = New-Object Windows.Forms.Label; $ls.Text=$sub; $ls.Font=(F 8.5); $ls.ForeColor=$dim; $ls.Location=(P 28 29); $ls.AutoSize=$true; $ls.Visible=$false
  $okb = New-Object Windows.Forms.Label; $okb.Text='✓'; $okb.Font=(F 15 'Bold'); $okb.ForeColor=$global:T.glow; $okb.Size=(S 36 32); $okb.TextAlign='MiddleCenter'; $okb.Cursor='Hand'; $okb.Location=(P 276 6)
  $hd.Controls.AddRange(@($bar,$lt,$okb))
  $ft = New-Object Windows.Forms.Panel; $ft.Dock='Bottom'; $ft.Height=62; $ft.BackColor=$cardBg
  function TB($t,$x,$tip){ $l=New-Object Windows.Forms.Label; $l.Text=$t; $l.Font=(F 9.5 'Bold'); $l.ForeColor=$global:T.glow; $l.Cursor='Hand'; $l.Location=(P $x 6); $l.Size=(S 70 26); $l.TextAlign='MiddleCenter'; $l }
  $bDone = TB '✓ 완료' 8; $bTime = TB '⏱ 시간' 80; $bDelL = TB '✕ 줄삭제' 152
  $ln = New-Object Windows.Forms.Label; $ln.Text=$sub; $ln.Font=(F 8.5); $ln.ForeColor=$dim; $ln.Location=(P 14 38); $ln.Size=(S 292 18)
  $ft.Controls.AddRange(@($bDone,$bTime,$bDelL,$ln))
  $body = New-Object Windows.Forms.Panel; $body.Dock='Fill'; $body.Padding=(New-Object Windows.Forms.Padding 12,0,12,6); $body.BackColor=$cardBg
  $rtb = New-Object Windows.Forms.RichTextBox; $rtb.Dock='Fill'; $rtb.BorderStyle='None'; $rtb.BackColor=$txtBg; $rtb.ForeColor=$fg; $rtb.Font=$fNormal
  $rtb.DetectUrls=$false; $rtb.AcceptsTab=$false; $rtb.WordWrap=$true; $rtb.ScrollBars='Vertical'
  $body.Controls.Add($rtb); $f.Controls.Add($body); $f.Controls.Add($ft); $f.Controls.Add($hd)
  # 내용 채우기: "1. 14:00 제목" 형태
  $lines = @(); $i=0
  foreach ($e in $mine) { $i++; $lines += ("$i. " + $(if ($e.time) { "$($e.time) " } else { '' }) + $e.title) }
  if ($lines.Count -eq 0) { $lines = @('1. ') }
  $rtb.Text = ($lines -join "`n")
  $acc=0
  for ($k=0; $k -lt $lines.Count; $k++) {
    if ($k -lt $mine.Count -and $mine[$k].done) { $rtb.Select($acc,$lines[$k].Length); $rtb.SelectionFont=$fStrike; $rtb.SelectionColor=$dim }
    $acc += $lines[$k].Length + 1
  }
  $caret = $rtb.TextLength
  if ($focusId) { for ($k=0; $k -lt $mine.Count; $k++) { if ($mine[$k].id -eq $focusId) { $caret = 0; for ($q=0; $q -lt $k; $q++) { $caret += $lines[$q].Length + 1 }; break } } }
  $rtb.Select($caret,0)
  $global:dirty = $false
  $rtb.Add_TextChanged({ $global:dirty = $true })
  $lineAt = { param($pos) $ls2=$rtb.Text -split "`n"; $a=0; for ($z=0; $z -lt $ls2.Count; $z++) { $len=$ls2[$z].Length; if ($pos -le $a+$len) { return @{ i=$z; start=$a; len=$len } }; $a += $len+1 }; return @{ i=$ls2.Count-1; start=0; len=0 } }.GetNewClosure()
  $toggle = {
    $info = & $lineAt $rtb.SelectionStart; if ($info.len -le 0) { return }
    $keep = $rtb.SelectionStart; $rtb.Select($info.start,$info.len); $c0=$rtb.SelectionFont
    if ($c0 -and $c0.Strikeout) { $rtb.SelectionFont=$fNormal; $rtb.SelectionColor=$fg } else { $rtb.SelectionFont=$fStrike; $rtb.SelectionColor=$dim }
    $rtb.Select($keep,0); $global:dirty = $true
  }.GetNewClosure()
  $doSave = {
    if ($global:EditorSaving) { return }
    $global:EditorSaving = $true
    try {
      $ls3 = $rtb.Text -split "`n"; $a=0; $items=@()
      for ($z=0; $z -lt $ls3.Count; $z++) {
        $raw=$ls3[$z]; $st=$a; $a += $raw.Length + 1
        $t = ($raw -replace '^\s*\d+\.\s*','').Trim(); if (-not $t) { continue }
        $rtb.Select($st+$raw.Length-1,1); $fn=$rtb.SelectionFont; $dn = [bool]($fn -and $fn.Strikeout)
        $sp = Split-Time $t
        $items += [pscustomobject]@{ title=$sp.title; time=$sp.time; done=$dn }
      }
      for ($z=0; $z -lt [math]::Max($items.Count,$mine.Count); $z++) {
        if ($z -lt $items.Count) {
          $it=$items[$z]; $L=@{ title=$it.title; place=''; kind='개인'; start=$ds; end=$ds; time=$it.time; memo=''; done=$it.done; scope='personal' }
          if ($z -lt $mine.Count) { $ex=$mine[$z]; $L.memo=$ex.memo; if ($ex.title -ne $it.title -or $ex.time -ne $it.time -or [bool]$ex.done -ne $it.done) { [void](Save-Event $ex $L) } }
          else { [void](Save-Event $null $L) }
        } else { [void](Delete-Event $mine[$z]) }
      }
      $global:dirty = $false
    } finally { $global:EditorSaving = $false }
    $global:Selected = $dd
    Refresh-All
  }.GetNewClosure()
  $global:EditorRtb=$rtb; $global:EditorSave=$doSave; $global:EditorToggle=$toggle
  $bDone.Add_Click($toggle)
  $bTime.Add_Click({
    $info = & $lineAt $rtb.SelectionStart
    $t0 = (Get-Date).ToString('HH:mm')
    $pre = [regex]::Match(($rtb.Text -split "`n")[$info.i],'^\s*\d+\.\s*').Length
    $rtb.Select($info.start+$pre,0); $rtb.SelectionFont=$fNormal; $rtb.SelectedText = "$t0 "
    $rtb.Select($info.start+$pre,5)       # 시간 부분을 선택해 바로 고쳐 쓸 수 있게
  }.GetNewClosure())
  $bDelL.Add_Click({
    $info = & $lineAt $rtb.SelectionStart
    $rtb.Select($info.start,$info.len + $(if ($info.start+$info.len -lt $rtb.TextLength) { 1 } else { 0 })); $rtb.SelectedText=''
    if ($rtb.TextLength -eq 0) { $rtb.Text='1. ' }
  }.GetNewClosure())
  $closeSave = { & $doSave; $f.Close() }.GetNewClosure()
  $okb.Add_Click($closeSave)
  $rtb.Add_KeyDown({
    param($s,$e)
    if ($e.KeyCode -eq 'Escape') { $global:dirty=$false; $f.Close(); return }
    if ($e.KeyCode -eq 'Return' -and $e.Control) { $e.SuppressKeyPress=$true; & $closeSave; return }
    if ($e.KeyCode -eq 'Return') {
      $e.SuppressKeyPress=$true
      $info = & $lineAt $rtb.SelectionStart
      $rtb.SelectionFont=$fNormal; $rtb.SelectionColor=$fg
      $rtb.SelectedText = "`n$($info.i+2). "
      $rtb.SelectionFont=$fNormal; $rtb.SelectionColor=$fg
    }
  }.GetNewClosure())
  $f.Add_Deactivate({ if (-not $global:EditorSaving -and $global:dirty -and $f.Visible) { & $closeSave } }.GetNewClosure())      # 팝업 밖을 누르면 자동 저장
  $f.Add_Paint({ param($s,$e) $e.Graphics.DrawRectangle((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(90,128,128,140))),0,0,$s.Width-1,$s.Height-1) })
  $f.Add_FormClosed({ $global:EditorOpen=$false })
  $f.Add_Shown({ $rtb.Focus() }.GetNewClosure())
  $f.Show()
}

# ---------------------------------------------------------------- 부서 일정 등록/수정 (온나라 일정 방식)
function Show-EventDialog($ev,$defDate){
  if ($ev -and $ev.scope -eq 'personal') { Show-DayEditor (Parse-Day $ev.start) $ev.id; return }
  $isNew = -not $ev
  $mine = $isNew -or (Is-Mine $ev)
  $f = New-Object Windows.Forms.Form
  $f.Text = if ($isNew) { '부서 일정 등록' } elseif ($mine) { '부서 일정 수정' } else { '부서 일정 보기 (작성자만 수정 가능)' }
  $f.StartPosition='CenterParent'; $f.ClientSize=(S 600 664); $f.FormBorderStyle='FixedDialog'
  $f.MaximizeBox=$false; $f.MinimizeBox=$false; $f.Font=(F 9.5)
  $owner = if ($isNew) { $global:Cfg.user } else { $ev.owner }
  $lo = New-Object Windows.Forms.Label; $lo.Text="작성자: $owner"; $lo.Font=(F 10 'Bold'); $lo.ForeColor=(UserColor $owner); $lo.Location=(P 15 12); $lo.AutoSize=$true
  function L($t,$y){ $l=New-Object Windows.Forms.Label; $l.Text=$t; $l.Font=(F 9.5 'Bold'); $l.Location=(P 15 $y); $l.AutoSize=$true; $l }
  function DT($x,$w){ $d=New-Object Windows.Forms.DateTimePicker; $d.Format='Custom'; $d.CustomFormat='yyyy.MM.dd'; $d.Location=(P $x 78); $d.Width=$w; $d }
  function TM($x){ $d=New-Object Windows.Forms.DateTimePicker; $d.Format='Custom'; $d.CustomFormat='HH:mm'; $d.ShowUpDown=$true; $d.Location=(P $x 78); $d.Width=70; $d }
  $lk = New-Object Windows.Forms.Label; $lk.Text='부서 일정 (모두에게 공유됩니다)'; $lk.ForeColor=[Drawing.Color]::SteelBlue; $lk.Location=(P 95 44); $lk.AutoSize=$true
  $dS=DT 95 118; $tS=TM 218; $sep=New-Object Windows.Forms.Label; $sep.Text='~'; $sep.Location=(P 296 82); $sep.AutoSize=$true
  $dE=DT 316 118; $tE=TM 439; $chkAll=New-Object Windows.Forms.CheckBox; $chkAll.Text='종일'; $chkAll.AutoSize=$true; $chkAll.Location=(P 520 80)
  $cbH = New-Object Windows.Forms.ComboBox; $cbH.DropDownStyle='DropDown'; $cbH.Location=(P 95 118); $cbH.Width=150
  [void]$cbH.Items.Add('')
  foreach ($p in (@($global:Events.Values | Where-Object { $_.scope -eq 'dept' -and $_.place } | ForEach-Object { [string]$_.place } | Sort-Object -Unique))) { [void]$cbH.Items.Add($p) }
  $tTitle = New-Object Windows.Forms.TextBox; $tTitle.Location=(P 252 118); $tTitle.Width=328
  $lh = New-Object Windows.Forms.Label; $lh.Text="장소(춘포역, 근대역사관, 대회의실 등)를 직접 쓰거나 목록에서 고르세요. 한 번 쓴 장소는 목록에 쌓이고, 달력에는 `"(장소) 제목`" 으로 표시됩니다. 같은 장소로 이미 등록된 일정이 있으면 저장할 때 알려드려요."; $lh.ForeColor=[Drawing.Color]::Gray; $lh.Location=(P 95 146); $lh.AutoSize=$true; $lh.MaximumSize=(S 485 0)
  $tMemo = New-Object Windows.Forms.TextBox; $tMemo.Multiline=$true; $tMemo.ScrollBars='Vertical'; $tMemo.Location=(P 95 192); $tMemo.Size=(S 485 100)
  $c1=New-Object Windows.Forms.CheckBox; $c1.Text='1일전'; $c1.AutoSize=$true; $c1.Location=(P 95 306)
  $c3=New-Object Windows.Forms.CheckBox; $c3.Text='3일전'; $c3.AutoSize=$true; $c3.Location=(P 165 306)
  $c7=New-Object Windows.Forms.CheckBox; $c7.Text='7일전'; $c7.AutoSize=$true; $c7.Location=(P 235 306)
  $lr=New-Object Windows.Forms.Label; $lr.Text='(내 PC 화면 왼쪽 아래로 알려드려요)'; $lr.ForeColor=[Drawing.Color]::Gray; $lr.Location=(P 310 308); $lr.AutoSize=$true
  $chkConf=New-Object Windows.Forms.CheckBox; $chkConf.Text='같은 장소로 등록된 부서 일정이 있으면 알려주기 (중복 예약 방지)'; $chkConf.AutoSize=$true; $chkConf.Location=(P 95 340); $chkConf.Checked=$true
  $chkDone=New-Object Windows.Forms.CheckBox; $chkDone.Text='완료 (취소선으로 표시)'; $chkDone.AutoSize=$true; $chkDone.Location=(P 95 366)
  if (-not $global:HasSchema) { $chkDone.Enabled=$false }
  # 첨부파일(공문)
  $files = New-Object Collections.ArrayList; $added = New-Object Collections.ArrayList; $removed = New-Object Collections.ArrayList
  if (-not $isNew) { foreach ($x in @($ev.files)) { [void]$files.Add(@{ name=[string]$x.name; path=[string]$x.path; size=[long]$x.size }) } }
  $lbF = New-Object Windows.Forms.ListBox; $lbF.Location=(P 95 402); $lbF.Size=(S 485 112); $lbF.HorizontalScrollbar=$true
  $bFAdd = New-Object Windows.Forms.Button; $bFAdd.Text='파일 추가'; $bFAdd.Location=(P 95 520); $bFAdd.Size=(S 100 28)
  $bFOpen = New-Object Windows.Forms.Button; $bFOpen.Text='열기/저장'; $bFOpen.Location=(P 200 520); $bFOpen.Size=(S 100 28)
  $bFDel = New-Object Windows.Forms.Button; $bFDel.Text='제거'; $bFDel.Location=(P 305 520); $bFDel.Size=(S 100 28)
  $refreshF = { $lbF.Items.Clear(); foreach ($x in $files) { [void]$lbF.Items.Add("$($x.name)   ($([math]::Max(1,[math]::Round($x.size/1KB))) KB)") } }.GetNewClosure()
  & $refreshF
  # 초기값
  $dayZero=(Get-Date).Date
  if ($isNew) {
    $dS.Value=$defDate; $dE.Value=$defDate; $tS.Value=$dayZero.AddHours(10); $tE.Value=$dayZero.AddHours(11)
  } else {
    $chkDone.Checked=[bool]$ev.done; $cbH.Text=[string]$ev.place; $tTitle.Text=[string]$ev.title
    $dS.Value=(Parse-Day $ev.start); $dE.Value=(Parse-Day $ev.end); $tMemo.Text=($ev.memo -replace "`r?`n","`r`n")
    $tr = Parse-TimeRange $ev.time
    if ($tr) { $tS.Value=$dayZero.AddMinutes($tr[0]); $tE.Value=$dayZero.AddMinutes([math]::Min(1439,$tr[1])); $chkAll.Checked=$false } else { $tS.Value=$dayZero.AddHours(10); $tE.Value=$dayZero.AddHours(11); $chkAll.Checked=$true }
    $rm = @($global:Remind[$ev.id]); $c1.Checked=($rm -contains 1); $c3.Checked=($rm -contains 3); $c7.Checked=($rm -contains 7)
  }
  $tsw = { $tS.Enabled = -not $chkAll.Checked; $tE.Enabled = -not $chkAll.Checked }.GetNewClosure()
  $chkAll.Add_CheckedChanged($tsw); & $tsw
  $bSave = New-Object Windows.Forms.Button; $bSave.Text='저장'; $bSave.Size=(S 90 34); $bSave.Location=(P 320 614)
  $bDel  = New-Object Windows.Forms.Button; $bDel.Text='삭제';  $bDel.Size=(S 90 34);  $bDel.Location=(P 415 614)
  $bClose= New-Object Windows.Forms.Button; $bClose.Text='닫기'; $bClose.Size=(S 90 34); $bClose.Location=(P 510 614)
  if (-not $mine) {
    $tTitle.ReadOnly=$true; $cbH.Enabled=$false; $tMemo.ReadOnly=$true
    foreach ($c in @($dS,$dE,$tS,$tE,$chkAll,$chkDone,$chkConf,$bFAdd,$bFDel)) { $c.Enabled=$false }
    $bSave.Visible=$false; $bDel.Visible=$false
  }
  if ($isNew) { $bDel.Visible=$false }
  $bFAdd.Add_Click({
    $d = New-Object Windows.Forms.OpenFileDialog; $d.Multiselect=$true; $d.Title='첨부할 공문/파일 선택 (20MB 이하)'
    if ($d.ShowDialog() -ne 'OK') { return }
    $f.Cursor='WaitCursor'
    foreach ($p in $d.FileNames) {
      try { $u = Upload-File $p } catch { $u = $null; [void][Windows.Forms.MessageBox]::Show("업로드 실패: $($_.Exception.Message)") }
      if ($u) { [void]$files.Add($u); [void]$added.Add($u.path) }
    }
    $f.Cursor='Default'; & $refreshF
  }.GetNewClosure())
  $bFDel.Add_Click({
    $i = $lbF.SelectedIndex; if ($i -lt 0) { return }
    $x = $files[$i]; $files.RemoveAt($i)
    if ($added.Contains($x.path)) { Remove-Files @($x.path); $added.Remove($x.path) } else { [void]$removed.Add($x.path) }
    & $refreshF
  }.GetNewClosure())
  $bFOpen.Add_Click({
    $i = $lbF.SelectedIndex; if ($i -lt 0) { return }
    $x = $files[$i]; $ext = [IO.Path]::GetExtension($x.name).ToLower(); $name = [IO.Path]::GetFileName($x.name)
    try {
      if ($global:OpenExt -contains $ext) {
        $dir = Join-Path $env:TEMP 'DeptCalendar'; [void](New-Item -ItemType Directory -Force -Path $dir)
        $dest = Join-Path $dir "$([guid]::NewGuid().ToString('N').Substring(0,6))_$name"
        $f.Cursor='WaitCursor'; Download-File $x $dest; $f.Cursor='Default'
        Start-Process -FilePath $dest
      } else {
        $sd = New-Object Windows.Forms.SaveFileDialog; $sd.FileName=$name
        if ($sd.ShowDialog() -eq 'OK') { $f.Cursor='WaitCursor'; Download-File $x $sd.FileName; $f.Cursor='Default'; [void][Windows.Forms.MessageBox]::Show('저장했습니다.') }
      }
    } catch { $f.Cursor='Default'; [void][Windows.Forms.MessageBox]::Show("파일을 받지 못했습니다: $($_.Exception.Message)") }
  }.GetNewClosure())
  $f.Add_FormClosed({ if ($f.Tag -ne 'saved') { Remove-Files @($added) } }.GetNewClosure())
  $bClose.Add_Click({ $f.Close() }.GetNewClosure())
  $bSave.Add_Click({
    $t = $tTitle.Text.Trim()
    if (-not $t) { [void][Windows.Forms.MessageBox]::Show('제목을 입력해 주세요.'); return }
    if ($dE.Value.Date -lt $dS.Value.Date) { [void][Windows.Forms.MessageBox]::Show('종료일이 시작일보다 빠릅니다.'); return }
    $time=''
    if (-not $chkAll.Checked) {
      $a=$tS.Value.ToString('HH:mm'); $b=$tE.Value.ToString('HH:mm')
      if ($dE.Value.Date -eq $dS.Value.Date -and $tE.Value.TimeOfDay -lt $tS.Value.TimeOfDay) { [void][Windows.Forms.MessageBox]::Show('종료 시간이 시작 시간보다 빠릅니다.'); return }
      $time = if ($a -eq $b) { $a } else { "$a~$b" }
    }
    $L = @{ title=$t; place=$cbH.Text.Trim(); kind='행사'; start=$dS.Value.ToString('yyyy-MM-dd'); end=$dE.Value.ToString('yyyy-MM-dd')
      time=$time; memo=$tMemo.Text; done=$chkDone.Checked; scope='dept'; files=@($files); conflict=$chkConf.Checked }
    $global:LastSavedId = $null
    if (-not (Save-Event $ev $L)) { return }
    Remove-Files @($removed)
    $id = if ($ev) { $ev.id } else { $global:LastSavedId }
    if ($id) {
      $days=@(); if ($c1.Checked) { $days+=1 }; if ($c3.Checked) { $days+=3 }; if ($c7.Checked) { $days+=7 }
      if ($days.Count) { $global:Remind[$id]=$days } else { $global:Remind.Remove($id) }
      Save-Remind
    }
    $global:Selected = $dS.Value.Date
    $f.Tag='saved'; $f.Close()
  }.GetNewClosure())
  $bDel.Add_Click({
    if ([Windows.Forms.MessageBox]::Show('이 일정을 삭제할까요? (첨부파일도 함께 삭제됩니다)','삭제 확인','YesNo') -ne 'Yes') { return }
    if (Delete-Event $ev) { Remove-Files @($files | ForEach-Object { $_.path }); $global:Remind.Remove($ev.id); Save-Remind; $f.Tag='saved'; $f.Close() }
  }.GetNewClosure())
  $f.Controls.AddRange(@($lo,(L '구분' 44),$lk,(L '일시' 82),$dS,$tS,$sep,$dE,$tE,$chkAll,(L '장소·제목' 122),$cbH,$tTitle,$lh,(L '내용' 194),$tMemo,(L '알림 예약' 308),$c1,$c3,$c7,$lr,(L '옵션' 342),$chkConf,$chkDone,(L '첨부(공문)' 404),$lbF,$bFAdd,$bFOpen,$bFDel,$bSave,$bDel,$bClose))
  $f.TopMost=$true; Show-Owned $f
  Refresh-All
}

function Show-DayList($day){
  $f = New-Object Windows.Forms.Form
  $f.Text = $day.ToString('yyyy년 M월 d일 (ddd)') + ' 일정'
  $f.StartPosition='CenterParent'; $f.ClientSize=(S 560 360); $f.Font=(F 9.5); $f.MinimizeBox=$false; $f.MaximizeBox=$false; $f.FormBorderStyle='FixedDialog'
  $lv = New-Object Windows.Forms.ListView; $lv.View='Details'; $lv.FullRowSelect=$true; $lv.Location=(P 10 10); $lv.Size=(S 540 295); $lv.HideSelection=$false
  [void]$lv.Columns.Add('시간',90); [void]$lv.Columns.Add('행사 이름',200); [void]$lv.Columns.Add('장소',100); [void]$lv.Columns.Add('작성자',70); [void]$lv.Columns.Add('구분',70)
  foreach ($e in (Events-On $day)) {
    $it = New-Object Windows.Forms.ListViewItem($e.time)
    $kind = if ($e.scope -eq 'personal') { '개인' } else { [string]$e.kind }
    [void]$it.SubItems.Add($e.title); [void]$it.SubItems.Add([string]$e.place); [void]$it.SubItems.Add($e.owner); [void]$it.SubItems.Add($kind)
    $it.ForeColor = UserColor $e.owner; $it.Tag = $e
    if ($e.done) { $it.Font = (F 9.5 'Strikeout') }
    [void]$lv.Items.Add($it)
  }
  $bAdd = New-Object Windows.Forms.Button; $bAdd.Text='부서 일정 등록'; $bAdd.Location=(P 300 315); $bAdd.Size=(S 110 32)
  $bMemo = New-Object Windows.Forms.Button; $bMemo.Text='개인 메모'; $bMemo.Location=(P 216 315); $bMemo.Size=(S 76 32)
  $bC = New-Object Windows.Forms.Button; $bC.Text='닫기'; $bC.Location=(P 450 315); $bC.Size=(S 100 32)
  $lv.Add_DoubleClick({ if ($lv.SelectedItems.Count) { $e=$lv.SelectedItems[0].Tag; $f.Close(); Show-EventDialog $e $day } }.GetNewClosure())
  $bAdd.Add_Click({ $f.Close(); Show-EventDialog $null $day }.GetNewClosure())
  $bMemo.Add_Click({ $f.Close(); Show-DayEditor $day $null }.GetNewClosure())
  $bC.Add_Click({ $f.Close() }.GetNewClosure())
  $f.Controls.AddRange(@($lv,$bMemo,$bAdd,$bC))
  $f.TopMost=$true; Show-Owned $f
}

function Events-On($day){
  $d = $day.Date
  @(Visible-Events | Where-Object { (Parse-Day $_.start) -le $d -and (Parse-Day $_.end) -ge $d } | Sort-Object created, id)
}

# ---------------------------------------------------------------- 빠른 등록 (Ctrl+Shift+Enter)
function Parse-Quick($text){
  $s = $text; $today = (Get-Date).Date
  $place = ''; $time = ''; $date = $null; $dateFound = $false; $scope = 'personal'
  if ($s -match '^\s*[#/]?(부서|개인)\s+') { $scope = if ($Matches[1] -eq '부서') { 'dept' } else { 'personal' }; $s = $s.Substring($Matches[0].Length) }
  if ($s -match '(?:@|장소\s*[:：]?\s*)(\S+)') { $place = $Matches[1]; $s = $s.Replace($Matches[0],' ') }
  if ($s -match '(\d{1,2}):(\d{2})\s*[~\-]\s*(\d{1,2}):(\d{2})') {
    $time = '{0:00}:{1:00}~{2:00}:{3:00}' -f [int]$Matches[1],[int]$Matches[2],[int]$Matches[3],[int]$Matches[4]; $s = $s.Replace($Matches[0],' ')
  } elseif ($s -match '(오전|오후)?\s*(\d{1,2}):(\d{2})') {
    $h=[int]$Matches[2]; if ($Matches[1] -eq '오후' -and $h -lt 12) { $h+=12 }
    $time = '{0:00}:{1:00}' -f $h,[int]$Matches[3]; $s = $s.Replace($Matches[0],' ')
  } elseif ($s -match '(오전|오후)?\s*(\d{1,2})\s*시(?:\s*(\d{1,2})\s*분)?') {
    $h=[int]$Matches[2]; $m=0; if ($Matches[3]) { $m=[int]$Matches[3] }
    if ($Matches[1] -eq '오후' -and $h -lt 12) { $h+=12 }
    $time = '{0:00}:{1:00}' -f $h,$m; $s = $s.Replace($Matches[0],' ')
  }
  try {
    if ($s -match '(\d{4})[-./](\d{1,2})[-./](\d{1,2})') { $date = Get-Date -Year ([int]$Matches[1]) -Month ([int]$Matches[2]) -Day ([int]$Matches[3]); $dateFound=$true; $s=$s.Replace($Matches[0],' ') }
    elseif ($s -match '(\d{1,2})\s*월\s*(\d{1,2})\s*일?') { $date = Get-Date -Year $today.Year -Month ([int]$Matches[1]) -Day ([int]$Matches[2]); $dateFound=$true; $s=$s.Replace($Matches[0],' ') }
    elseif ($s -match '(?<![\d:])(\d{1,2})[/.](\d{1,2})(?![\d:])') { $date = Get-Date -Year $today.Year -Month ([int]$Matches[1]) -Day ([int]$Matches[2]); $dateFound=$true; $s=$s.Replace($Matches[0],' ') }
    elseif ($s -match '오늘') { $date=$today; $dateFound=$true; $s=$s.Replace('오늘',' ') }
    elseif ($s -match '내일') { $date=$today.AddDays(1); $dateFound=$true; $s=$s.Replace('내일',' ') }
    elseif ($s -match '모레') { $date=$today.AddDays(2); $dateFound=$true; $s=$s.Replace('모레',' ') }
  } catch { $date=$null; $dateFound=$false }
  if ($date) { $date = $date.Date; if ($date -lt $today.AddDays(-30) -and $s -notmatch '\d{4}') { $date = $date.AddYears(1) } }
  $guessed = -not $dateFound
  if (-not $date) { $date = $global:Selected.Date }
  $title = ($s -replace '\s+',' ').Trim()
  if (-not $place -and $title -match '^\(([^)]+)\)\s*(.+)$') { $place=$Matches[1]; $title=$Matches[2].Trim() }     # "(춘포역) 행사이름" 형식도 인식
  [pscustomobject]@{ Title=$title; Place=$place; Date=$date; Time=$time; Guessed=$guessed; Scope=$scope }
}

function Show-QuickAdd {
  if ($global:QuickOpen) { return }
  $global:QuickOpen = $true
  $f = New-Object Windows.Forms.Form
  $f.FormBorderStyle='FixedToolWindow'; $f.Text='빠른 일정 등록  (Enter: 등록 / Esc: 취소)'; $f.TopMost=$true
  $f.StartPosition='CenterScreen'; $f.ClientSize=(S 660 170); $f.Font=(F 11); $f.ShowInTaskbar=$false
  $t = New-Object Windows.Forms.TextBox; $t.Location=(P 12 12); $t.Width=636; $t.Font=(F 13)
  $pv = New-Object Windows.Forms.Label; $pv.Location=(P 12 52); $pv.Size=(S 636 66); $pv.Font=(F 10.5); $pv.ForeColor=(UserColor $global:Cfg.user)
  $hint = New-Object Windows.Forms.Label; $hint.Location=(P 12 120); $hint.Size=(S 636 44); $hint.Font=(F 9); $hint.ForeColor=[Drawing.Color]::Gray
  $hint.Text="예)  10/20 14:00 건강검진 @병원      (기본: 개인 일정 - 나만 보임)`n예)  부서 10/20 14:00 부서회의 @대회의실      (앞에 '부서'를 쓰면 모두에게 공유)"
  $f.Controls.AddRange(@($t,$pv,$hint))
  $upd = {
    $q = Parse-Quick $t.Text
    if (-not $q.Title) { $pv.Text='행사 이름을 입력하세요.'; return }
    $d = $q.Date.ToString('yyyy-MM-dd (ddd)'); if ($q.Guessed) { $d += ' [선택된 날짜]' }
    $sc = if ($q.Scope -eq 'dept') { '[부서 일정 - 모두에게 공유]' } else { '[개인 일정 - 나만 보임]' }
    $pv.Text = "$sc`n행사: $($q.Title)`n장소: $(if($q.Place){$q.Place}else{'-'})    일시: $d $($q.Time)"
  }.GetNewClosure()
  $t.Add_TextChanged($upd)
  $t.Add_KeyDown({
    param($s,$e)
    if ($e.KeyCode -eq 'Escape') { $f.Close(); return }
    if ($e.KeyCode -eq 'Return') {
      $e.SuppressKeyPress = $true
      $q = Parse-Quick $t.Text
      if (-not $q.Title) { return }
      $day = $q.Date.ToString('yyyy-MM-dd')
      $L = @{ title=$q.Title; place=$q.Place; kind=$(if ($q.Scope -eq 'dept') { '행사' } else { '개인' }); start=$day; end=$day; time=$q.Time; memo=''; done=$false; scope=$q.Scope }
      if (-not (Save-Event $null $L)) { return }
      $global:Selected = $q.Date; $global:View = $q.Date.AddDays(1 - $q.Date.Day)
      $f.Close()
      Refresh-All
    }
  }.GetNewClosure())
  $f.Add_FormClosed({ $global:QuickOpen = $false })
  $f.Add_Shown({ [void][Win]::SetForegroundWindow($f.Handle); $f.Activate(); $t.Focus() }.GetNewClosure())
  & $upd
  $f.Show()
}

# ---------------------------------------------------------------- 기존 달력 파일 가져오기 (개인 일정으로)
# 형식:  [2026-10-16]  /  ----  /  1. 항목  2. 항목 ...   ("(완료)"로 끝나면 완료 표시)
function Parse-Legacy($text){
  $items = New-Object Collections.ArrayList
  $date = $null; $last = $null
  foreach ($line in ($text -split "`r?`n")) {
    $l = $line.Trim()
    if ($l -match '^\[(\d{4}-\d{2}-\d{2})\]$') { $date = $Matches[1]; $last = $null; continue }
    if (-not $l -or $l -match '^-{5,}$' -or -not $date) { continue }
    if ($l -match '^\d+\.\s*(.*)$') {
      $o = [pscustomobject]@{ date=$date; seq=$items.Count; raw=$Matches[1] }
      [void]$items.Add($o); $last = $o
    } elseif ($last) { $last.raw += ' ' + $l }
  }
  $out = New-Object Collections.ArrayList
  foreach ($o in $items) {
    $t = $o.raw.Trim(); $done = $false; $time = ''
    if ($t -match '\(완료\)\s*$') { $done = $true; $t = ($t -replace '\s*\(완료\)\s*$','').Trim() }
    if ($t -match '^(\d{1,2}):(\d{2})\s*[~\-]\s*(\d{1,2}):(\d{2})\)?\s*(.*)$') { $time = '{0:00}:{1:00}~{2:00}:{3:00}' -f [int]$Matches[1],[int]$Matches[2],[int]$Matches[3],[int]$Matches[4]; $t = $Matches[5] }
    elseif ($t -match '^(\d{1,2}):(\d{2})\)?\s*(.*)$') { $time = '{0:00}:{1:00}' -f [int]$Matches[1],[int]$Matches[2]; $t = $Matches[3] }
    elseif ($t -match '^(\d{1,2})시\)?\s*(.*)$') { $time = '{0:00}:00' -f [int]$Matches[1]; $t = $Matches[2] }
    $t = $t.Trim(); if (-not $t) { $t = $o.raw.Trim() }
    if ($t -match '^[\)\s]*$') { continue }      # ")" 같은 잔여 글자만 있는 항목은 제외
    [void]$out.Add([pscustomobject]@{ date=$o.date; seq=$o.seq; title=$t; time=$time; done=$done })
  }
  return $out
}

function Import-Legacy {
  Check-Schema
  if (-not $global:HasSchema) { [void][Windows.Forms.MessageBox]::Show($SqlMsg); return }
  $dlg = New-Object Windows.Forms.OpenFileDialog
  $dlg.Title='기존 달력 파일 선택'; $dlg.Filter='텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*'
  if ($dlg.ShowDialog() -ne 'OK') { return }
  $bytes = [IO.File]::ReadAllBytes($dlg.FileName)
  $text = $Utf8.GetString($bytes)
  if ($text.Contains([string][char]0xFFFD)) { $text = [Text.Encoding]::GetEncoding(949).GetString($bytes) }
  $text = $text.TrimStart([char]0xFEFF)
  $all = @(Parse-Legacy $text)
  if ($all.Count -eq 0) { [void][Windows.Forms.MessageBox]::Show('가져올 일정을 찾지 못했습니다.'); return }
  [void](Sync-Data)
  $have = @{}
  foreach ($e in $global:Events.Values) { if ((Is-Mine $e) -and $e.scope -eq 'personal') { $have["$($e.start)|$($e.title)"] = $true } }
  $todo = @($all | Where-Object { -not $have.ContainsKey("$($_.date)|$($_.title)") } | Sort-Object date, seq)
  $skip = $all.Count - $todo.Count
  if ($todo.Count -eq 0) { [void][Windows.Forms.MessageBox]::Show("이미 모두 가져온 파일입니다. ($skip 건)"); return }
  $first = ($todo | Select-Object -First 1).date; $last = ($todo | Select-Object -Last 1).date
  $msg = "$($todo.Count)건을 '개인 일정'으로 가져옵니다. (나만 보이며 암호화되어 저장됩니다)`n기간: $first ~ $last"
  if ($skip) { $msg += "`n(이미 있는 $skip 건은 건너뜁니다)" }
  $msg += "`n`n'(완료)' 항목은 완료(취소선) 표시로 가져옵니다. 진행할까요?"
  if ([Windows.Forms.MessageBox]::Show($msg,'기존 일정 가져오기','YesNo') -ne 'Yes') { return }
  $global:Main.Cursor = 'WaitCursor'
  $base = [DateTime]::UtcNow.AddSeconds(-($todo.Count + 5))
  $rows = New-Object Collections.ArrayList
  for ($i=0; $i -lt $todo.Count; $i++) {
    $it = $todo[$i]
    $row = Build-Row @{ title=$it.title; place=''; kind='개인'; start=$it.date; end=$it.date; time=$it.time; memo=''; done=$it.done; scope='personal' }
    $row.created_at = $base.AddSeconds($i).ToString('o')      # 파일 순서대로 먼저 등록한 것이 위
    [void]$rows.Add($row)
  }
  $done = 0
  try {
    for ($i=0; $i -lt $rows.Count; $i+=100) {
      $chunk = @($rows[$i..([math]::Min($i+99,$rows.Count-1))])
      $r = Api POST '/rest/v1/events' (ConvertTo-Json -InputObject $chunk -Depth 6) 'return=minimal'
      if ($r.S -ne 201) { throw "HTTP $($r.S) $($r.T)" }
      $done += $chunk.Count
    }
    $global:Main.Cursor = 'Default'
    [void][Windows.Forms.MessageBox]::Show("$done 건을 개인 일정으로 가져왔습니다.")
  } catch {
    $global:Main.Cursor = 'Default'
    [void][Windows.Forms.MessageBox]::Show("가져오기 중단: $done 건 완료.`n$($_.Exception.Message)`n(다시 실행하면 이어서 가져옵니다)")
  }
  $global:View = (Get-Date -Day 1).Date
  Refresh-All
}

# ---------------------------------------------------------------- 글꼴 / 화면 설정
function Rebuild-Fonts {
  $sz=[double]$global:Cfg.fsize; if ($sz -lt 7) { $sz=9.5 }
  $rs = if ($global:Cfg.fbold) { 'Bold' } else { 'Regular' }
  $global:fEv=(F $sz $rs); $global:fEvB=(F $sz 'Bold'); $global:fEvS=(F $sz "$rs, Strikeout"); $global:fEvBS=(F $sz 'Bold, Strikeout')
  $global:fEvI=(F $sz 'Italic'); $global:fEvIS=(F $sz 'Italic, Strikeout')
  $global:fNum=(F ($sz+1) 'Bold'); $global:fHdr=(F ($sz+0.5) 'Bold'); $global:fMore=(F ([math]::Max(7,$sz-0.5))); $global:fLun=(F ([math]::Max(7,$sz-1.5)))
}
function Apply-Fonts {
  $form.Font=(F 9.5); $lblMonth.Font=(F 13 'Bold'); $lblToday.Font=(F 10.5)
  foreach ($b in $btnF.Values) { $b.Font=(F 9.5 'Bold') }
  foreach ($b in @($btnAdd,$btnMenu)) { $b.Font=(F 12 'Bold') }
  $btnNote.Font=(F 9.5 'Bold'); $btnLayout.Font=(F 9.5 'Bold')
  Refresh-Legend; $grid.Invalidate()
}
function Show-Settings {
  $f=New-Object Windows.Forms.Form; $f.Text='설정 - 글꼴 · 공휴일'; $f.StartPosition='CenterParent'; $f.ClientSize=(S 480 470)
  $f.FormBorderStyle='FixedDialog'; $f.MaximizeBox=$false; $f.MinimizeBox=$false; $f.Font=(F 9.5)
  function L($t,$x,$y){ $l=New-Object Windows.Forms.Label; $l.Text=$t; $l.Location=(P $x $y); $l.AutoSize=$true; $l }
  $g1=New-Object Windows.Forms.GroupBox; $g1.Text='글꼴 · 표시'; $g1.Location=(P 12 8); $g1.Size=(S 456 206)
  $cb=New-Object Windows.Forms.ComboBox; $cb.DropDownStyle='DropDownList'; $cb.Location=(P 92 24); $cb.Width=350
  $fams=@((New-Object Drawing.Text.InstalledFontCollection).Families | ForEach-Object { $_.Name })
  [void]$cb.Items.AddRange($fams); $i=$cb.Items.IndexOf([string]$global:Cfg.font); if ($i -lt 0) { $i=$cb.Items.IndexOf('맑은 고딕') }; if ($i -lt 0) { $i=0 }; $cb.SelectedIndex=$i
  $nud=New-Object Windows.Forms.NumericUpDown; $nud.Location=(P 122 60); $nud.Width=70; $nud.Minimum=7; $nud.Maximum=20; $nud.DecimalPlaces=1; $nud.Increment=0.5; $nud.Value=[decimal][math]::Max(7,[math]::Min(20,[double]$global:Cfg.fsize))
  $chkB=New-Object Windows.Forms.CheckBox; $chkB.Text='일정 글자 굵게'; $chkB.AutoSize=$true; $chkB.Location=(P 214 62); $chkB.Checked=[bool]$global:Cfg.fbold
  $prev=New-Object Windows.Forms.Label; $prev.Location=(P 14 98); $prev.Size=(S 428 50); $prev.BorderStyle='FixedSingle'; $prev.BackColor=[Drawing.Color]::White; $prev.TextAlign='MiddleLeft'
  $prev.Text='  1. (대회의실) 부서회의 14:00   ABC abc 123'
  $chkO=New-Object Windows.Forms.CheckBox; $chkO.Text='다른 사람이 등록한 부서 일정에 (등록자 이름) 표시'; $chkO.AutoSize=$true; $chkO.Location=(P 14 160); $chkO.Checked=[bool]$global:Cfg.showOwner
  $lh=L '일정 글자는 "(장소) 행사이름" 형식으로 표시됩니다.' 14 184; $lh.ForeColor=[Drawing.Color]::Gray
  $upd={ try { $sty= if ($chkB.Checked) { 'Bold' } else { 'Regular' }; $prev.Font=(New-Object Drawing.Font ([string]$cb.SelectedItem),([single]$nud.Value),([Drawing.FontStyle]$sty)) } catch {} }.GetNewClosure()
  $cb.Add_SelectedIndexChanged($upd); $nud.Add_ValueChanged($upd); $chkB.Add_CheckedChanged($upd); & $upd
  $g1.Controls.AddRange(@((L '글꼴' 14 28),$cb,(L '일정 글자 크기' 14 64),$nud,$chkB,$prev,$chkO,$lh))
  $g2=New-Object Windows.Forms.GroupBox; $g2.Text='공휴일 · 절기 · 기념일 (공공데이터포털 특일 정보 API)'; $g2.Location=(P 12 222); $g2.Size=(S 456 190)
  $chkA=New-Object Windows.Forms.CheckBox; $chkA.Text='API로 공휴일·절기·기념일 표시 (키가 없으면 내장 계산 사용)'; $chkA.AutoSize=$true; $chkA.Location=(P 14 26); $chkA.Checked=[bool]$global:Cfg.holApi
  $tk=New-Object Windows.Forms.TextBox; $tk.Location=(P 92 56); $tk.Width=350; $tk.Text=[string]$global:Cfg.holKey
  $help=L "공공데이터포털(data.go.kr)의 '한국천문연구원 특일 정보' 활용신청 후`n발급된 일반 인증키를 붙여넣으세요. (비워 두면 내장 공용 키를 사용합니다)" 14 90; $help.ForeColor=[Drawing.Color]::Gray
  $bt=New-Object Windows.Forms.Button; $bt.Text='키 확인'; $bt.Location=(P 14 134); $bt.Size=(S 90 28)
  $rl=L '' 114 140
  $bt.Add_Click({
    $rl.ForeColor=[Drawing.Color]::Gray; $rl.Text='확인 중...'; $f.Refresh()
    try { $now=Get-Date; $it=@(Hol-Request $now.Year $now.Month 'getRestDeInfo' $tk.Text.Trim()); $rl.ForeColor=[Drawing.Color]::SeaGreen; $rl.Text="연결 성공 (이번 달 공휴일 정보 $($it.Count)건)" }
    catch { $rl.ForeColor=[Drawing.Color]::Crimson; $rl.Text=$_.Exception.Message }
  }.GetNewClosure())
  $chkAn=New-Object Windows.Forms.CheckBox; $chkAn.Text='기념일(국군의 날, 경찰의 날 등)도 달력에 표시'; $chkAn.AutoSize=$true; $chkAn.Location=(P 14 166); $chkAn.Checked=[bool]$global:Cfg.showAnniv
  $g2.Controls.AddRange(@($chkA,(L 'API 인증키' 14 60),$tk,$help,$bt,$rl,$chkAn))
  $ok=New-Object Windows.Forms.Button; $ok.Text='저장'; $ok.Location=(P 290 424); $ok.Size=(S 85 32)
  $cn=New-Object Windows.Forms.Button; $cn.Text='취소'; $cn.Location=(P 383 424); $cn.Size=(S 85 32)
  $cn.Add_Click({ $f.Close() }.GetNewClosure())
  $ok.Add_Click({
    $global:Cfg.font=[string]$cb.SelectedItem; $global:FontName=$global:Cfg.font
    $global:Cfg.fsize=[double]$nud.Value; $global:Cfg.fbold=$chkB.Checked; $global:Cfg.showOwner=$chkO.Checked; $global:Cfg.showAnniv=$chkAn.Checked
    $keyChanged = ($tk.Text.Trim() -ne [string]$global:Cfg.holKey)
    $global:Cfg.holKey=$tk.Text.Trim(); $global:Cfg.holApi=$chkA.Checked
    Save-Cfg; Rebuild-Fonts; Apply-Fonts
    if ($keyChanged) { $global:HolApi=@{}; $global:HolQueue.Clear(); Remove-Item -LiteralPath $HolCachePath -Force -ErrorAction SilentlyContinue }
    Queue-Holidays $global:View.Year; if ($global:HolQueue.Count -gt 0) { $holTimer.Start() }
    Reload-Notes
    $f.Close()
  }.GetNewClosure())
  $f.AcceptButton=$ok; $f.Controls.AddRange(@($g1,$g2,$ok,$cn))
  $f.TopMost=$true; Show-Owned $f
}

# ---------------------------------------------------------------- 장소 중복 예약 경고 / 일정 검색
function Parse-TimeRange($t){
  if ($t -match '^\s*(\d{1,2}):(\d{2})\s*[~\-]\s*(\d{1,2}):(\d{2})') { return @(([int]$Matches[1]*60+[int]$Matches[2]),([int]$Matches[3]*60+[int]$Matches[4])) }
  if ($t -match '^\s*(\d{1,2}):(\d{2})') { $s=[int]$Matches[1]*60+[int]$Matches[2]; return @($s,($s+60)) }
  return $null
}
function Find-Conflicts($L,$exceptId){
  $pl=([string]$L.place).Trim()
  if (-not $pl -or $L.scope -eq 'personal') { return @() }
  $s1=Parse-Day $L.start; $e1=Parse-Day $L.end; $r1=Parse-TimeRange $L.time
  $out=@()
  foreach ($x in $global:Events.Values) {
    if ($x.scope -ne 'dept' -or $x.id -eq $exceptId -or ([string]$x.place).Trim() -ne $pl) { continue }
    $s2=Parse-Day $x.start; $e2=Parse-Day $x.end
    if ($e2 -lt $s1 -or $s2 -gt $e1) { continue }
    # 장소가 같고 날짜가 겹치면 모두 '겹침'으로 알림. 시간까지 겹치는지는 표시용(timeHit)으로만 계산
    $r2=Parse-TimeRange $x.time
    $th = $true
    if ($r1 -and $r2 -and $s1 -eq $e1 -and $s2 -eq $e2) { $th = -not ($r1[1] -le $r2[0] -or $r2[1] -le $r1[0]) }
    $out += [pscustomobject]@{ ev=$x; timeHit=$th }
  }
  return $out
}
function Show-Search {
  $f=New-Object Windows.Forms.Form; $f.Text='일정 검색'; $f.StartPosition='CenterParent'; $f.ClientSize=(S 720 460); $f.Font=(F 9.5); $f.MinimizeBox=$false; $f.MaximizeBox=$false; $f.FormBorderStyle='FixedDialog'
  $t=New-Object Windows.Forms.TextBox; $t.Location=(P 12 12); $t.Width=560; $t.Font=(F 11)
  $lc=New-Object Windows.Forms.Label; $lc.Location=(P 584 16); $lc.AutoSize=$true; $lc.ForeColor=[Drawing.Color]::Gray
  $lv=New-Object Windows.Forms.ListView; $lv.View='Details'; $lv.FullRowSelect=$true; $lv.Location=(P 12 48); $lv.Size=(S 696 360); $lv.HideSelection=$false
  foreach ($c in @(@('일자',90),@('시간',90),@('행사 이름',230),@('장소',110),@('등록자',80),@('구분',70))) { [void]$lv.Columns.Add($c[0],$c[1]) }
  $fill={
    $q=$t.Text.Trim(); $lv.BeginUpdate(); $lv.Items.Clear(); $n=0
    $src=@($global:Events.Values | Where-Object { $_.scope -ne 'x' } | Sort-Object @{e={$_.start};Descending=$true}, created)
    foreach ($e in $src) {
      if ($q -and -not (("$($e.title) $($e.place) $($e.owner) $($e.memo)").IndexOf($q,[StringComparison]::OrdinalIgnoreCase) -ge 0)) { continue }
      $it=New-Object Windows.Forms.ListViewItem($e.start)
      [void]$it.SubItems.Add([string]$e.time); [void]$it.SubItems.Add($(if($e.done){"✔ $($e.title)"}else{$e.title})); [void]$it.SubItems.Add([string]$e.place); [void]$it.SubItems.Add($e.owner)
      [void]$it.SubItems.Add($(if($e.scope -eq 'personal'){'개인'}else{'부서'}))
      $it.ForeColor=(UserColor $e.owner); $it.Tag=$e; [void]$lv.Items.Add($it); $n++
      if ($n -ge 300) { break }
    }
    $lv.EndUpdate(); $lc.Text="$n 건"
  }.GetNewClosure()
  $t.Add_TextChanged($fill)
  $lv.Add_DoubleClick({ if ($lv.SelectedItems.Count) { $e=$lv.SelectedItems[0].Tag; $f.Close(); $global:Selected=(Parse-Day $e.start); $global:View=$global:Selected.AddDays(1-$global:Selected.Day); Show-EventDialog $e $global:Selected } }.GetNewClosure())
  $bc=New-Object Windows.Forms.Button; $bc.Text='닫기'; $bc.Location=(P 608 416); $bc.Size=(S 100 32); $bc.Add_Click({ $f.Close() }.GetNewClosure())
  $f.Controls.AddRange(@($t,$lc,$lv,$bc)); & $fill
  $f.Add_Shown({ $t.Focus() }.GetNewClosure())
  $f.TopMost=$true; Show-Owned $f
}

# ---------------------------------------------------------------- 바탕화면 메모 (스티커 메모)
$NotesPath = Join-Path $AppDir 'notes.json'
$global:Notes = New-Object Collections.ArrayList
$global:NoteForms = @{}
$global:NotesHidden = $false
$global:NoteColors = @(
  @{ bg=(RGB 255 246 187); fg=(RGB 96 82 24);  hd=(RGB 251 235 150) },   # 노랑
  @{ bg=(RGB 212 241 224); fg=(RGB 36 82 58);  hd=(RGB 190 230 206) },   # 민트
  @{ bg=(RGB 255 220 229); fg=(RGB 122 44 68); hd=(RGB 250 200 214) },   # 핑크
  @{ bg=(RGB 211 232 255); fg=(RGB 34 68 112); hd=(RGB 190 217 250) },   # 하늘
  @{ bg=(RGB 232 222 255); fg=(RGB 72 54 122); hd=(RGB 217 204 250) },   # 연보라
  @{ bg=(RGB 240 239 235); fg=(RGB 64 62 58);  hd=(RGB 224 222 216) }    # 회색
)
function Load-Notes {
  $global:Notes.Clear()
  if (-not (Test-Path -LiteralPath $NotesPath)) { return }
  try {
    foreach ($x in (Parse-Rows ([IO.File]::ReadAllText($NotesPath,$Utf8)))) {
      [void]$global:Notes.Add([pscustomobject]@{ id=[string]$x.id; text=[string]$x.text; color=[int]$x.color; x=[int]$x.x; y=[int]$x.y; w=[int]$x.w; h=[int]$x.h; fs=[double]$x.fs; col=[bool]$x.col })
    }
  } catch { Log-Error "notes: $($_.Exception.Message)" }
}
function Save-Notes {
  try { [void](New-Item -ItemType Directory -Force -Path $AppDir); [IO.File]::WriteAllText($NotesPath,(ConvertTo-Json -InputObject @($global:Notes) -Depth 4),$Utf8) } catch { Log-Error "savenotes: $($_.Exception.Message)" }
}
function New-Note {
  $wa=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea; $i=$global:Notes.Count
  $n=[pscustomobject]@{ id=[guid]::NewGuid().ToString('N'); text=''; color=($i % 6); x=($wa.Right-300-($i % 5)*28); y=($wa.Top+70+($i % 5)*28); w=270; h=240; fs=11; col=$false }
  [void]$global:Notes.Add($n); Save-Notes; Open-Note $n $true
}
function Note-Title($text){ $l=@(([string]$text) -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }); if ($l.Count) { $l[0] } else { '(빈 메모)' } }
$global:NoteApi = @{}
function Open-Note($n,$front){
  if ($global:NoteForms.ContainsKey($n.id)) { return }
  $c = $global:NoteColors[[int]$n.color % $global:NoteColors.Count]
  $bd = [Drawing.Color]::FromArgb([int]($c.hd.R*0.84),[int]($c.hd.G*0.84),[int]($c.hd.B*0.84))
  $fs = if ([double]$n.fs -ge 8) { [double]$n.fs } else { 11 }
  $collapsed = [bool]$n.col
  $rect = New-Object Drawing.Rectangle ([int]$n.x),([int]$n.y),([int]$n.w),([int]$n.h)
  if (-not ([Windows.Forms.Screen]::AllScreens | Where-Object { $_.WorkingArea.IntersectsWith($rect) })) { $wa=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea; $n.x=$wa.Right-300; $n.y=$wa.Top+70 }
  $f = New-Object SizableForm
  $f.FormBorderStyle='None'; $f.ShowInTaskbar=$false; $f.StartPosition='Manual'; $f.BackColor=$c.hd; $f.Font=(F 10)
  $f.Padding=(New-Object Windows.Forms.Padding 3); $f.ResizeEnabled=$true
  $f.KeepBottom = ([bool]$global:Cfg.pinned -and -not $front)          # 새로 만든 메모는 잠깐 앞에 보였다가, 다른 곳을 누르면 맨 뒤로
  if ($collapsed) { $f.MinimumSize=(S 120 32); $f.MaximumSize=(S 4000 32) } else { $f.MinimumSize=(S 150 110) }
  $f.Size=(S ([int]$n.w) $(if ($collapsed) { 32 } else { [int]$n.h })); $f.Location=(P ([int]$n.x) ([int]$n.y))
  $f.Opacity=[math]::Max(0.5,[math]::Min(1.0,[double]$global:Cfg.opacity/100.0))
  if ($env:DEPT_SELFTEST) { $f.TopMost=$true }      # 점검 화면 캡처용
  # 머리줄: [▾] 제목(첫 줄) ........ [＋][◐][A-][A+][✕]
  $h = New-Object Windows.Forms.Panel; $h.Dock='Top'; $h.Height=26; $h.BackColor=$c.hd
  function NB($t,$w){ $l=New-Object Windows.Forms.Label; $l.Text=$t; $l.Location=(P 0 1); $l.Size=(S $w 24); $l.TextAlign='MiddleCenter'; $l.Font=(F 9.5 'Bold'); $l.ForeColor=$c.fg; $l.Cursor='Hand'; $l }
  $bCol=NB $(if ($collapsed) { '▸' } else { '▾' }) 22; $bCol.Left=2
  $bar = New-Object Windows.Forms.Label; $bar.Location=(P 26 1); $bar.Height=24; $bar.Width=100; $bar.TextAlign='MiddleLeft'; $bar.AutoEllipsis=$true
  $bar.Font=(F 9.5 'Bold'); $bar.ForeColor=$c.fg; $bar.Text=(Note-Title $n.text)
  $bNew=NB '＋' 24; $bClr=NB '◐' 24; $bMinus=NB 'A-' 26; $bPlus=NB 'A+' 26; $bDel=NB '✕' 24
  $h.Controls.AddRange(@($bCol,$bar,$bNew,$bClr,$bMinus,$bPlus,$bDel))
  $layoutHdr = {
    $w=$h.Width; $bDel.Left=$w-26
    if ([bool]$n.col) { $bNew.Left=$w-52; $x=$bNew.Left } else { $bPlus.Left=$w-54; $bMinus.Left=$w-82; $bClr.Left=$w-108; $bNew.Left=$w-134; $x=$bNew.Left }
    $bar.Width=[math]::Max(20,$x-$bar.Left-2)
  }.GetNewClosure()
  $h.Add_Resize($layoutHdr)
  foreach ($b in @($bCol,$bNew,$bClr,$bMinus,$bPlus,$bDel)) {      # 마우스를 올리면 살짝 진해지는 버튼
    $b.Add_MouseEnter({ param($s,$e) $s.BackColor=$bd }.GetNewClosure()); $b.Add_MouseLeave({ param($s,$e) $s.BackColor=$c.hd }.GetNewClosure())
  }
  # 본문
  $body = New-Object Windows.Forms.Panel; $body.Dock='Fill'; $body.BackColor=$c.bg; $body.Padding=(New-Object Windows.Forms.Padding 10,8,8,8); $body.Visible=(-not $collapsed)
  $tb = New-Object Windows.Forms.TextBox; $tb.Multiline=$true; $tb.BorderStyle='None'; $tb.Dock='Fill'; $tb.BackColor=$c.bg; $tb.ForeColor=$c.fg
  $tb.ScrollBars='Vertical'; $tb.Font=(F $fs); $tb.AcceptsReturn=$true; $tb.Text=([string]$n.text -replace "`r?`n","`r`n")
  $body.Controls.Add($tb); $f.Controls.Add($body); $f.Controls.Add($h)
  if ($collapsed) { foreach ($x in @($bMinus,$bPlus,$bClr)) { $x.Visible=$false } }
  & $layoutHdr
  # 자동 저장
  $st = New-Object Windows.Forms.Timer; $st.Interval=700
  $flush = { $n.text=$tb.Text; if ($f.WindowState -eq 'Normal') { $n.x=$f.Left; $n.y=$f.Top; $n.w=$f.Width; if (-not [bool]$n.col) { $n.h=$f.Height } }; Save-Notes }.GetNewClosure()
  $st.Add_Tick({ $st.Stop(); & $flush }.GetNewClosure())
  $kick = { $st.Stop(); $st.Start() }.GetNewClosure()
  $tb.Add_TextChanged({ $bar.Text=(Note-Title $tb.Text); & $kick }.GetNewClosure()); $f.Add_LocationChanged($kick); $f.Add_ResizeEnd($kick)
  # 접기 / 펴기 (한 줄짜리 메모)
  $setCol = {
    param($v)
    $n.col=[bool]$v
    if ($v) { if ($f.Height -gt 40) { $n.h=$f.Height }; $f.MinimumSize=(S 120 32); $f.MaximumSize=(S 4000 32); $f.Height=32; $body.Visible=$false }
    else    { $f.MaximumSize=(S 4000 4000); $f.MinimumSize=(S 150 110); $f.Height=[math]::Max(110,[int]$n.h); $body.Visible=$true }
    $bCol.Text = if ($v) { '▸' } else { '▾' }
    foreach ($x in @($bMinus,$bPlus,$bClr)) { $x.Visible = -not $v }
    & $layoutHdr; & $flush
  }.GetNewClosure()
  $global:NoteApi[$n.id] = $setCol
  $bCol.Add_Click({ & $setCol (-not [bool]$n.col) }.GetNewClosure())
  $h.Add_DoubleClick({ & $setCol (-not [bool]$n.col) }.GetNewClosure()); $bar.Add_DoubleClick({ & $setCol (-not [bool]$n.col) }.GetNewClosure())
  # 머리줄을 끌어서 자유롭게 이동 (항상 가능)
  $ds=@{ on=$false; mx=0; my=0; fx=0; fy=0 }
  $down={ param($s,$e) if ($e.Button -eq 'Left') { $ds.on=$true; $pp=[Windows.Forms.Cursor]::Position; $ds.mx=$pp.X; $ds.my=$pp.Y; $ds.fx=$f.Left; $ds.fy=$f.Top } }.GetNewClosure()
  $move={ param($s,$e) if ($ds.on) { $pp=[Windows.Forms.Cursor]::Position; $f.Location=(P ($ds.fx+$pp.X-$ds.mx) ($ds.fy+$pp.Y-$ds.my)) } }.GetNewClosure()
  $up={ param($s,$e) $ds.on=$false }.GetNewClosure()
  foreach ($d in @($h,$bar)) { $d.Add_MouseDown($down); $d.Add_MouseMove($move); $d.Add_MouseUp($up) }
  $bNew.Add_Click({ New-Note })
  $bClr.Add_Click({ $n.color=([int]$n.color+1) % $global:NoteColors.Count; $f.Tag='reopen'; $f.Close() }.GetNewClosure())
  $bPlus.Add_Click({ $n.fs=[math]::Min(30,$fs+1); $f.Tag='reopen'; $f.Close() }.GetNewClosure())
  $bMinus.Add_Click({ $n.fs=[math]::Max(8,$fs-1); $f.Tag='reopen'; $f.Close() }.GetNewClosure())
  $bDel.Add_Click({
    if ($tb.Text.Trim() -and [Windows.Forms.MessageBox]::Show('이 메모를 삭제할까요?','메모 삭제','YesNo') -ne 'Yes') { return }
    $f.Tag='deleted'; [void]$global:Notes.Remove($n); Save-Notes; $f.Close()
  }.GetNewClosure())
  $f.Add_FormClosed({
    $st.Stop(); $st.Dispose(); [void]$global:NoteForms.Remove($n.id); [void]$global:NoteApi.Remove($n.id)
    if ($f.Tag -eq 'deleted') { return }
    & $flush
    if ($f.Tag -eq 'reopen') { Open-Note $n $true }
  }.GetNewClosure())
  $f.Add_Deactivate({ if ($global:Cfg.pinned) { $f.KeepBottom=$true; [void][Win]::SetWindowPos($f.Handle,[IntPtr]1,0,0,0,0,0x13) } }.GetNewClosure())
  $f.Add_Resize({ if ($f.WindowState -eq 'Minimized' -and $global:Cfg.pinned) { [void][Win]::ShowWindow($f.Handle,4) } }.GetNewClosure())
  $f.Add_Paint({ param($s,$e) $e.Graphics.DrawRectangle((New-Object Drawing.Pen $bd),0,0,$s.Width-1,$s.Height-1) }.GetNewClosure())
  $global:NoteForms[$n.id] = $f
  [void]$f.Handle; $f.Show()
  if ($front) { $f.Activate(); $tb.Focus() } elseif ($global:Cfg.pinned) { [void][Win]::SetWindowPos($f.Handle,[IntPtr]1,0,0,0,0,0x13) }
}
function Reload-Notes {
  foreach ($id in @($global:NoteForms.Keys)) { $nf=$global:NoteForms[$id]; if ($nf) { $nf.Tag='quiet'; $nf.Close() } }
  $global:NoteForms.Clear()
  if (-not $global:NotesHidden) { foreach ($n in @($global:Notes)) { Open-Note $n $false } }
}


# ---------------------------------------------------------------- 일정 알림 예약 (1일전/3일전/7일전 - 이 PC 토스트로 알림)
$RemindPath = Join-Path $AppDir 'reminders.json'
$global:Remind = @{}          # 일정 id -> 일수 배열
$global:Reminded = @{}
function Load-Remind {
  $global:Remind = @{}
  if (-not (Test-Path -LiteralPath $RemindPath)) { return }
  try { $o = [IO.File]::ReadAllText($RemindPath,$Utf8) | ConvertFrom-Json; foreach ($p in $o.PSObject.Properties) { $global:Remind[$p.Name] = @($p.Value | ForEach-Object { [int]$_ }) } } catch { Log-Error "remind: $($_.Exception.Message)" }
}
function Save-Remind {
  try { [void](New-Item -ItemType Directory -Force -Path $AppDir); $h=@{}; foreach ($k in $global:Remind.Keys) { $h[$k]=@($global:Remind[$k]) }; [IO.File]::WriteAllText($RemindPath,(ConvertTo-Json -InputObject $h -Depth 3),$Utf8) } catch {}
}
function Check-Reminders {
  $today=(Get-Date).Date
  foreach ($id in @($global:Remind.Keys)) {
    $ev=$global:Events[$id]; if (-not $ev) { continue }
    $dl=((Parse-Day $ev.start) - $today).Days
    foreach ($d in @($global:Remind[$id])) {
      if ($dl -ne [int]$d) { continue }
      $key="$id|$d|$($today.ToString('yyMMdd'))"; if ($global:Reminded.ContainsKey($key)) { continue }
      $global:Reminded[$key]=$true
      $pl = if ($ev.place) { "($($ev.place)) " } else { '' }
      Show-Toast ([Drawing.Color]::DarkOrange) "$d일 전 알림" "$pl$($ev.title)`n$($ev.start) $($ev.time)"
    }
  }
}

# ---------------------------------------------------------------- 메인 창 (테두리 없는 바탕화면 위젯)
$form = New-Object SizableForm
$global:Main = $form; $form.ResizeEnabled = $false
$form.Text='부서 일정 공유 달력'; $form.FormBorderStyle='None'; $form.Padding=(New-Object Windows.Forms.Padding 6)
$form.Size=(S 1100 760); $form.StartPosition='CenterScreen'; $form.Font=(F 9.5); $form.MinimumSize=(S 860 480)
if ([int]$global:Cfg.w -ge 860 -and [int]$global:Cfg.h -ge 480) {
  $rect = New-Object Drawing.Rectangle ([int]$global:Cfg.x),([int]$global:Cfg.y),([int]$global:Cfg.w),([int]$global:Cfg.h)
  if ([Windows.Forms.Screen]::AllScreens | Where-Object { $_.WorkingArea.IntersectsWith($rect) }) { $form.StartPosition='Manual'; $form.Bounds=$rect }
}

$hdr = New-Object Windows.Forms.Panel; $hdr.Dock='Top'; $hdr.Height=40
$btnPrev = New-Object Windows.Forms.Button; $btnPrev.Text='◀'; $btnPrev.Location=(P 4 5); $btnPrev.Size=(S 34 30)
$lblMonth = New-Object Windows.Forms.Label; $lblMonth.Font=(F 13 'Bold'); $lblMonth.AutoSize=$false; $lblMonth.Location=(P 40 5); $lblMonth.Size=(S 128 30); $lblMonth.TextAlign='MiddleCenter'
$btnNext = New-Object Windows.Forms.Button; $btnNext.Text='▶'; $btnNext.Location=(P 170 5); $btnNext.Size=(S 34 30)
$btnF = @{}
$fx = 220
foreach ($pair in @(@('all','전체'),@('dept','부서'),@('personal','개인'))) {
  $b = New-Object Windows.Forms.Button; $b.Text=$pair[1]; $b.Tag=$pair[0]; $b.Location=(P $fx 6); $b.Size=(S 54 28); $b.Font=(F 9.5 'Bold')
  $btnF[$pair[0]] = $b; $fx += 56
}
$lblToday = New-Object Windows.Forms.Label; $lblToday.AutoSize=$false; $lblToday.Size=(S 330 30); $lblToday.Font=(F 10.5); $lblToday.TextAlign='MiddleCenter'; $lblToday.Text=(Today-Text)
$btnAdd = New-Object Windows.Forms.Button; $btnAdd.Text='＋'; $btnAdd.Size=(S 34 30); $btnAdd.Font=(F 12 'Bold')
$btnNote = New-Object Windows.Forms.Button; $btnNote.Text='메모'; $btnNote.Size=(S 48 30); $btnNote.Font=(F 9.5 'Bold')
$btnLayout = New-Object Windows.Forms.Button; $btnLayout.Text='위치·크기 변경'; $btnLayout.Size=(S 108 30); $btnLayout.Font=(F 9.5 'Bold')
$btnMenu = New-Object Windows.Forms.Button; $btnMenu.Text='☰'; $btnMenu.Size=(S 34 30); $btnMenu.Font=(F 12 'Bold')
$hdr.Controls.AddRange(@($btnPrev,$lblMonth,$btnNext,$btnF['all'],$btnF['dept'],$btnF['personal'],$lblToday,$btnLayout,$btnNote,$btnAdd,$btnMenu))
$hdr.Add_Resize({
  $btnMenu.Location=(P ($hdr.Width-38) 5); $btnAdd.Location=(P ($hdr.Width-76) 5); $btnNote.Location=(P ($hdr.Width-128) 5); $btnLayout.Location=(P ($hdr.Width-242) 5)
  $lblToday.Location=(P ([int](($hdr.Width-$lblToday.Width)/2)) 5)
})
# 머리줄을 끌어서 창 이동
$drag = { param($s,$e) if ($e.Button -eq 'Left' -and $global:Unlocked) { [void][Win]::ReleaseCapture(); [void][Win]::SendMessage($form.Handle,0xA1,[IntPtr]2,[IntPtr]::Zero) } }
foreach ($c in @($hdr,$lblToday,$lblMonth)) { $c.Add_MouseDown($drag) }

$bottom = New-Object Windows.Forms.Panel; $bottom.Dock='Bottom'; $bottom.Height=32
$lblStat = New-Object Windows.Forms.Label; $lblStat.Dock='Right'; $lblStat.Width=250; $lblStat.TextAlign='MiddleRight'; $lblStat.Padding=(New-Object Windows.Forms.Padding 0,0,8,0)
$legend = New-Object Windows.Forms.FlowLayoutPanel; $legend.Dock='Fill'; $legend.Padding=(New-Object Windows.Forms.Padding 8,5,8,0); $legend.WrapContents=$false
$bottom.Controls.Add($legend); $bottom.Controls.Add($lblStat)

$grid = New-Object Windows.Forms.Panel; $grid.Dock='Fill'
[void]$grid.GetType().GetProperty('DoubleBuffered',[Reflection.BindingFlags]'NonPublic,Instance').SetValue($grid,$true)
$tip = New-Object Windows.Forms.ToolTip; $tip.InitialDelay=300; $tip.AutoPopDelay=15000
$form.Controls.AddRange(@($grid,$bottom,$hdr))

Rebuild-Fonts
$brushCache=@{}
function Brush($c){ $k=$c.ToArgb(); if (-not $brushCache.ContainsKey($k)) { $brushCache[$k]=New-Object Drawing.SolidBrush $c }; $brushCache[$k] }
$sf = New-Object Drawing.StringFormat; $sf.Trimming='EllipsisCharacter'; $sf.FormatFlags='NoWrap'
$sfC = New-Object Drawing.StringFormat; $sfC.Alignment='Center'
$sfW = New-Object Drawing.StringFormat; $sfW.Trimming='EllipsisCharacter'
$sfR = New-Object Drawing.StringFormat; $sfR.Alignment='Far'; $sfR.FormatFlags='NoWrap'; $sfR.Trimming='EllipsisCharacter'

function Round-Path($x,$y,$w,$h,$r){
  $p=New-Object Drawing.Drawing2D.GraphicsPath; $d=[single]($r*2)
  $p.AddArc([single]$x,[single]$y,$d,$d,180,90); $p.AddArc([single]($x+$w-$d),[single]$y,$d,$d,270,90)
  $p.AddArc([single]($x+$w-$d),[single]($y+$h-$d),$d,$d,0,90); $p.AddArc([single]$x,[single]($y+$h-$d),$d,$d,90,90)
  $p.CloseFigure(); return $p
}

$grid.Add_Paint({
  param($s,$e)
  $T=$global:T
  $g=$e.Graphics
  $g.SmoothingMode='AntiAlias'
  $g.TextRenderingHint = if ($global:Cfg.theme -eq 'glass') { 'AntiAliasGridFit' } else { 'ClearTypeGridFit' }
  $wkw=34; $hh=30; $gap=3
  $W=$s.ClientSize.Width-$wkw; $H=$s.ClientSize.Height
  $first=$global:View; $off=[int]$first.DayOfWeek
  $rows=[int][math]::Ceiling(($off+[DateTime]::DaysInMonth($first.Year,$first.Month))/7)
  $cw=$W/7.0; $ch=($H-$hh)/$rows
  $start=$first.AddDays(-$off)
  $global:Geo=@{cw=$cw;ch=$ch;hdr=$hh;wk=$wkw;start=$start;rows=$rows}
  $global:Hits.Clear()
  $today=(Get-Date).Date
  $days='일요일','월요일','화요일','수요일','목요일','금요일','토요일'
  $isoCal=[Globalization.CultureInfo]::InvariantCulture.Calendar
  $linePen=New-Object Drawing.Pen $T.line,1
  $glowPen=New-Object Drawing.Pen $T.glow,2
  for($c=0;$c -lt 7;$c++){
    $col = if($c -eq 0){$T.sun}elseif($c -eq 6){$T.sat}else{$T.hdrTxt}
    $g.DrawString($days[$c],$fHdr,(Brush $col),[Drawing.RectangleF]::new([single]($wkw+$c*$cw),6,[single]$cw,22),$sfC)
  }
  # 보이는 범위의 일정 -> 날짜별 (먼저 등록한 것이 위)
  $map=@{}
  $gEnd=$start.AddDays($rows*7-1)
  foreach($ev in (Visible-Events | Sort-Object created,id)){
    $a=Parse-Day $ev.start; $b=Parse-Day $ev.end
    if($b -lt $start -or $a -gt $gEnd){continue}
    $d=if($a -lt $start){$start}else{$a}; $z=if($b -gt $gEnd){$gEnd}else{$b}
    while($d -le $z){ $k=$d.ToString('yyyy-MM-dd'); if(-not $map.ContainsKey($k)){$map[$k]=New-Object Collections.ArrayList}; [void]$map[$k].Add($ev); $d=$d.AddDays(1) }
  }
  for($r=0;$r -lt $rows;$r++){
    $y0=$hh+$r*$ch
    # 왼쪽 주차 번호 (둥근 알약)
    $wp=Round-Path 4 ($y0+$gap) ($wkw-8) ($ch-2*$gap) 10
    $g.FillPath((Brush $T.hdr),$wp); $wp.Dispose()
    $wn=$isoCal.GetWeekOfYear($start.AddDays($r*7+1),'FirstFourDayWeek','Monday')
    $g.DrawString([string]$wn,$fLun,(Brush $T.dim),[Drawing.RectangleF]::new(0,[single]($y0+$ch/2-8),$wkw,16),$sfC)
    for($c=0;$c -lt 7;$c++){
      $d=$start.AddDays($r*7+$c); $x=$wkw+$c*$cw; $y=$y0
      $rx=$x+$gap; $ry=$y+$gap; $rw=$cw-2*$gap; $rh=$ch-2*$gap
      $inMonth=($d.Month -eq $first.Month)
      $hn=Hol-Name $d
      $etc= if($hn){$null}else{Etc-Name $d}
      $bg= if($d -eq $global:Selected){$T.sel}elseif(-not $inMonth){$T.other}elseif($c -eq 0 -or $c -eq 6 -or $hn){$T.wk}else{$T.bg}
      $cp=Round-Path $rx $ry $rw $rh 10
      $g.FillPath((Brush $bg),$cp)
      if($d -eq $today){ $g.FillPath((Brush ([Drawing.Color]::FromArgb(30,$T.glow))),$cp); $g.DrawPath($glowPen,$cp) } else { $g.DrawPath($linePen,$cp) }
      $cp.Dispose()
      $nc= if(-not $inMonth){$T.dim}elseif($c -eq 0 -or $hn){$T.sun}elseif($c -eq 6){$T.sat}else{$T.txt}
      if($d -eq $today){ $g.FillEllipse((Brush $T.glow),[single]($rx+5),[single]($ry+4),24,22); $nc=$T.pillTxt }
      $g.DrawString([string]$d.Day,$fNum,(Brush $nc),[Drawing.RectangleF]::new([single]($rx+5),[single]($ry+6),24,18),$sfC)
      $lt=(Lunar-Text $d); $g.DrawString($lt,$fLun,(Brush $T.dim),[single]($rx+32),[single]($ry+8))
      $nx=$rx+32+$g.MeasureString($lt,$fLun).Width-2; $nw=[math]::Max(10,$rx+$rw-6-$nx)
      if($hn){ $g.DrawString($hn,$fLun,(Brush $T.hol),[Drawing.RectangleF]::new([single]$nx,[single]($ry+8),[single]$nw,16),$sfR) }
      elseif($etc){ $g.DrawString($etc,$fLun,(Brush $T.dim),[Drawing.RectangleF]::new([single]$nx,[single]($ry+8),[single]$nw,16),$sfR) }
      $k=$d.ToString('yyyy-MM-dd')
      if($map.ContainsKey($k)){
        $list=$map[$k]; $n=$list.Count; $ly=$ry+32; $shown=0; $bot=$ry+$rh-3
        $cx=$rx+5; $cwid=$rw-10
        for($i=0;$i -lt $n;$i++){
          $ev=$list[$i]
          $ttl=$ev.title; if($ev.place){ $ttl="($($ev.place)) $ttl" }
          $txt= if($ev.time){"$($ev.time) $ttl"}else{$ttl}
          if($global:Cfg.showOwner -and $ev.scope -eq 'dept' -and -not (Is-Mine $ev)){ $txt="$txt ($($ev.owner))" }
          if($ev.files.Count -gt 0){$txt="$txt [첨부$($ev.files.Count)]"}
          $txt="$($i+1). $txt"
          $pers = ($ev.scope -eq 'personal')
          $font= if($pers){ if($ev.done){$fEvIS}else{$fEvI} } elseif($ev.done){ if(Is-Mine $ev){$fEvBS}else{$fEvS} } else { if(Is-Mine $ev){$fEvB}else{$fEv} }
          $sz=$g.MeasureString($txt,$font,[int]($cwid-10),$sfW)
          $h=[int][math]::Min([math]::Ceiling($sz.Height)+4,($font.GetHeight($g)*3)+4)
          $limit= if($i -lt $n-1){$bot-14}else{$bot}
          if($ly+$h -gt $limit){ if($i -eq 0){ $h=[int][math]::Max(16,$limit-$ly) } else { break } }
          $col=DrawColor $ev.owner
          $ca= if($ev.done){[int]($T.chip*0.5)}else{[int]$T.chip}
          $chp=Round-Path $cx $ly $cwid $h 6
          $g.FillPath((Brush ([Drawing.Color]::FromArgb($ca,$col))),$chp); $chp.Dispose()
          if($ev.done){ $col=[Drawing.Color]::FromArgb(150,$col) }
          $g.DrawString($txt,$font,(Brush $col),[Drawing.RectangleF]::new([single]($cx+5),[single]($ly+2),[single]($cwid-10),[single]($h-3)),$sfW)
          [void]$global:Hits.Add([pscustomobject]@{R=[Drawing.Rectangle]::new([int]$cx,[int]$ly,[int]$cwid,$h);Ev=$ev;Day=$d;More=$false})
          $ly+=$h+3; $shown++
        }
        if($shown -lt $n){
          $g.DrawString("+ $($n-$shown)건 더보기",$fMore,(Brush $T.dim),[single]($cx+4),[single]$ly)
          [void]$global:Hits.Add([pscustomobject]@{R=[Drawing.Rectangle]::new([int]$cx,[int]$ly,[int]$cwid,15);Ev=$null;Day=$d;More=$true})
        }
      }
    }
  }
  $linePen.Dispose(); $glowPen.Dispose()
})

function Find-Hit($pt){ foreach($h in $global:Hits){ if($h.R.Contains($pt)){ return $h } }; return $null }
function Day-At($pt){
  $g=$global:Geo; if(-not $g -or $pt.Y -lt $g.hdr -or $pt.X -lt $g.wk){ return $null }
  $c=[int][math]::Floor(($pt.X-$g.wk)/$g.cw); $r=[int][math]::Floor(($pt.Y-$g.hdr)/$g.ch)
  if($c -lt 0 -or $c -gt 6 -or $r -lt 0 -or $r -ge $g.rows){ return $null }
  $g.start.AddDays($r*7+$c)
}
$global:LastTip=$null
$grid.Add_MouseMove({
  param($s,$e)
  $h=Find-Hit $e.Location
  if($h -and -not $h.More){
    if($global:LastTip -ne $h.Ev.id){
      $global:LastTip=$h.Ev.id; $ev=$h.Ev
      $head = if($ev.scope -eq 'personal'){ '[개인 일정 - 나만 보임]' } else { "[$($ev.kind)]" }
      $t="$head $($ev.title)`n작성자: $($ev.owner)"; if($ev.place){$t+="`n장소: $($ev.place)"}; $t+="`n일시: $($ev.start)"; if($ev.end -ne $ev.start){$t+=" ~ $($ev.end)"}; if($ev.time){$t+="  $($ev.time)"}; if($ev.memo){$t+="`n`n$($ev.memo)"}
      $tip.SetToolTip($grid,$t)
    }
    $grid.Cursor='Hand'
  } else { $global:LastTip=$null; $tip.SetToolTip($grid,''); $grid.Cursor='Default' }
})
$grid.Add_MouseClick({
  param($s,$e)
  $h=Find-Hit $e.Location
  if($h -and $e.Button -eq 'Right' -and -not $h.More){
    $ev=$h.Ev; $day=$h.Day; $mine=Is-Mine $ev
    $cm=New-Object Windows.Forms.ContextMenuStrip
    $m1=$cm.Items.Add($(if($ev.done){'완료 해제'}else{'완료 표시 (취소선)'})); $m1.Enabled=$mine
    $m1.Add_Click({ Set-Done $ev (-not $ev.done) }.GetNewClosure())
    $m2=$cm.Items.Add($(if($mine){'수정'}else{'보기'})); $m2.Add_Click({ Show-EventDialog $ev $day }.GetNewClosure())
    $m3=$cm.Items.Add('삭제'); $m3.Enabled=$mine
    $m3.Add_Click({ if([Windows.Forms.MessageBox]::Show('이 일정을 삭제할까요?','삭제 확인','YesNo') -eq 'Yes'){ if(Delete-Event $ev){ Refresh-All } } }.GetNewClosure())
    $cm.Show($grid,$e.Location); return
  }
  if($e.Button -eq 'Right' -and -not $h){
    $d=Day-At $e.Location; if(-not $d){ return }
    $day=$d.Date; $global:Selected=$day; $grid.Invalidate()
    $cm=New-Object Windows.Forms.ContextMenuStrip
    $r1=$cm.Items.Add('개인 메모 입력 (나만 보임)'); $r1.Add_Click({ Show-DayEditor $day $null }.GetNewClosure())
    $r2=$cm.Items.Add('부서 일정 등록 (모두에게 공유)'); $r2.Add_Click({ Show-EventDialog $null $day }.GetNewClosure())
    $cm.Show($grid,$e.Location); return
  }
  if($e.Button -ne 'Left'){ return }
  if($h){
    if($h.More){ $global:Selected=$h.Day; $grid.Invalidate(); Show-DayList $h.Day }
    elseif($h.Ev.scope -eq 'personal'){ Show-DayEditor $h.Day $h.Ev.id }
    else { Show-EventDialog $h.Ev $h.Day }
    return
  }
  # 빈 칸: 개인 메모(바탕화면 달력 방식). '부서' 보기에서는 부서 일정 등록(온나라 방식)
  $d=Day-At $e.Location
  if($d){
    $global:Selected=$d.Date; $grid.Invalidate()
    if($global:Filter -eq 'dept'){ Show-EventDialog $null $d.Date } else { Show-DayEditor $d.Date $null }
  }
})

function Refresh-Legend {
  $legend.Controls.Clear()
  $T=$global:T
  $lt=New-Object Windows.Forms.Label; $lt.Text='부서원:'; $lt.AutoSize=$true; $lt.ForeColor=$T.dim; $lt.Margin=(New-Object Windows.Forms.Padding 3,2,6,0)
  $legend.Controls.Add($lt)
  foreach($n in ($global:Users.Keys | Sort-Object)){
    $l=New-Object Windows.Forms.Label; $l.AutoSize=$true; $l.Font=(F 9.5 'Bold'); $l.ForeColor=(DrawColor $n); $l.Margin=(New-Object Windows.Forms.Padding 3,2,12,0)
    $l.Text= if($n -eq $global:Cfg.user){"● $n (나)"}else{"● $n"}
    $legend.Controls.Add($l)
  }
  $ln=New-Object Windows.Forms.Label; $ln.AutoSize=$true; $ln.ForeColor=$T.dim; $ln.Margin=(New-Object Windows.Forms.Padding 16,2,3,0); $ln.Text='기울임 = 개인 일정(나만 보임)'
  $legend.Controls.Add($ln)
}
function Update-FilterButtons {
  $T=$global:T
  foreach($k in $btnF.Keys){ $b=$btnF[$k]; $b.BackColor= if($k -eq $global:Filter){$T.btnSel}else{[Drawing.Color]::Transparent} }
}
function Refresh-All {
  [void](Sync-Data)
  $lblMonth.Text=$global:View.ToString('yyyy년 M월')
  $lblToday.Text=(Today-Text)
  Refresh-Legend
  $grid.Invalidate()
  Flush-Pending
}

# --- 유리 테마: 실제 바탕화면 이미지를 창 뒤쪽 영역만 잘라 흐리게 만들어 배경으로 깔아줌 (Windows 11 Acrylic 느낌)
$global:Wall = $null; $global:BackBmp = $null
function Load-Wall {
  if ($global:Wall) { return }
  $cands = @()
  try { $cands += (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -ErrorAction Stop).WallPaper } catch {}
  $cands += (Join-Path $env:APPDATA 'Microsoft\Windows\Themes\TranscodedWallpaper')
  foreach ($p in $cands) {
    if ($p -and (Test-Path -LiteralPath $p)) {
      try { $ms = New-Object IO.MemoryStream (,[IO.File]::ReadAllBytes($p)); $global:Wall = [Drawing.Image]::FromStream($ms); return } catch {}
    }
  }
}
function Update-Backdrop {
  if ($global:Cfg.theme -ne 'glass') { return }
  $w=$form.ClientSize.Width; $h=$form.ClientSize.Height; if ($w -lt 20 -or $h -lt 20) { return }
  try {
    Load-Wall
    $sm = New-Object Drawing.Bitmap ([math]::Max(8,[int]($w/20))),([math]::Max(8,[int]($h/20)))
    $g = [Drawing.Graphics]::FromImage($sm); $g.InterpolationMode='HighQualityBicubic'
    if ($global:Wall) {
      $scr=[Windows.Forms.Screen]::FromControl($form).Bounds
      $sc=[math]::Max($scr.Width/$global:Wall.Width,$scr.Height/$global:Wall.Height)
      $ox=($scr.Width-$global:Wall.Width*$sc)/2; $oy=($scr.Height-$global:Wall.Height*$sc)/2
      $sx=(($form.Left-$scr.Left)-$ox)/$sc; $sy=(($form.Top-$scr.Top)-$oy)/$sc
      $g.DrawImage($global:Wall,(New-Object Drawing.Rectangle 0,0,$sm.Width,$sm.Height),[single]$sx,[single]$sy,[single]($w/$sc),[single]($h/$sc),[Drawing.GraphicsUnit]::Pixel)
    } else {
      $lg = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Rectangle 0,0,$sm.Width,$sm.Height),(RGB 38 62 96),(RGB 22 34 58),45.0
      $g.FillRectangle($lg,0,0,$sm.Width,$sm.Height)
    }
    $g.Dispose()
    $big = New-Object Drawing.Bitmap $w,$h
    $g2 = [Drawing.Graphics]::FromImage($big); $g2.InterpolationMode='HighQualityBicubic'; $g2.PixelOffsetMode='Half'
    $g2.DrawImage($sm,0,0,$w,$h); $g2.Dispose(); $sm.Dispose()
    if ($global:BackBmp) { $global:BackBmp.Dispose() }
    $global:BackBmp = $big
    $form.Invalidate($true)
  } catch { Log-Error "backdrop: $($_.Exception.Message)" }
}
[void]$form.GetType().GetProperty('DoubleBuffered',[Reflection.BindingFlags]'NonPublic,Instance').SetValue($form,$true)
$form.Add_Paint({
  param($s,$e)
  if ($global:Cfg.theme -ne 'glass') { if ($global:Unlocked) { $e.Graphics.DrawRectangle((New-Object Drawing.Pen $global:T.glow,3),1,1,$form.ClientSize.Width-3,$form.ClientSize.Height-3) }; return }
  $g=$e.Graphics; $w=$form.ClientSize.Width; $h=$form.ClientSize.Height
  if ($global:BackBmp) { $g.DrawImage($global:BackBmp,0,0,$w,$h) } else { $g.Clear((RGB 30 50 70)) }
  $g.FillRectangle((Brush (RGBA 96 8 14 24)),0,0,$w,$h)                         # 전체 색조
  $g.FillRectangle((Brush (RGBA 70 8 14 24)),$hdr.Bounds)                      # 머리줄
  $g.FillRectangle((Brush (RGBA 70 8 14 24)),$bottom.Bounds)                   # 아래줄
  $g.DrawRectangle((New-Object Drawing.Pen (RGBA 70 255 255 255)),0,0,$w-1,$h-1)
  if ($global:Unlocked) { $g.DrawRectangle((New-Object Drawing.Pen $global:T.glow,3),1,1,$w-3,$h-3) }
})
$form.Add_LocationChanged({ Update-Backdrop })
$form.Add_SizeChanged({ Update-Backdrop })

function Apply-Theme {
  if ($global:Cfg.theme -notin 'light','dark','glass') { $global:Cfg.theme='glass' }
  $global:T = $global:Themes[$global:Cfg.theme]
  $T=$global:T; $glass = ($global:Cfg.theme -eq 'glass'); $tr=[Drawing.Color]::Transparent
  if ($glass) {
    $form.BackColor=(RGB 30 50 70)
    foreach ($p in @($hdr,$bottom,$legend,$grid)) { $p.BackColor=$tr }
  } else {
    $form.BackColor=$T.panel; $hdr.BackColor=$T.hdr; $bottom.BackColor=$T.legend; $legend.BackColor=$T.legend; $grid.BackColor=$T.bg
  }
  foreach ($c in @($lblMonth,$lblToday)) { $c.ForeColor=$T.hdrTxt }
  $lblStat.ForeColor=$T.dim
  foreach ($b in @($btnPrev,$btnNext,$btnAdd,$btnMenu,$btnNote,$btnLayout) + @($btnF.Values)) {
    $b.FlatStyle='Flat'; $b.FlatAppearance.BorderSize=0; $b.ForeColor=$T.hdrTxt; $b.FlatAppearance.MouseOverBackColor=$T.btnHover; $b.Cursor='Hand'; $b.UseVisualStyleBackColor=$false
  }
  Update-FilterButtons
  $form.Opacity = [math]::Max(0.5,[math]::Min(1.0,[double]$global:Cfg.opacity/100.0))
  Update-Backdrop
  Refresh-Legend; $grid.Invalidate(); $form.Invalidate($true); Update-LayoutButton
}

function Go-Month($delta){
  $y0=$global:View.Year; $global:View=$global:View.AddMonths($delta); $lblMonth.Text=$global:View.ToString('yyyy년 M월'); $grid.Invalidate()
  if ($global:View.Year -ne $y0) { Queue-Holidays $global:View.Year; if ($global:HolQueue.Count -gt 0) { $holTimer.Start() } }
}
$btnPrev.Add_Click({ Go-Month -1 })
$btnNext.Add_Click({ Go-Month 1 })
$btnNote.Add_Click({ New-Note })
$btnLayout.Add_Click({ Set-LayoutMode (-not $global:Unlocked) })
$btnAdd.Add_Click({
  $cm=New-Object Windows.Forms.ContextMenuStrip
  $a1=$cm.Items.Add('개인 메모 입력 (나만 보임)'); $a1.Add_Click({ Show-DayEditor $global:Selected $null })
  $a2=$cm.Items.Add('부서 일정 등록 (모두에게 공유)'); $a2.Add_Click({ Show-EventDialog $null $global:Selected })
  $cm.Show($btnAdd,(P 0 $btnAdd.Height))
})
foreach ($b in $btnF.Values) { $b.Add_Click({ param($s,$e) $global:Filter=[string]$s.Tag; Update-FilterButtons; $grid.Invalidate() }) }
$form.KeyPreview=$true
$form.Add_KeyDown({ param($s,$e)
  if($e.Control -and $e.Shift -and $e.KeyCode -eq 'Return'){ $e.SuppressKeyPress=$true; Show-QuickAdd }
  elseif($e.Control -and $e.KeyCode -eq 'F'){ $e.SuppressKeyPress=$true; Show-Search }
})
# 전역 단축키: 어느 프로그램을 쓰고 있어도 Ctrl+Shift+Enter 로 빠른 등록창 열기
$global:Hotkey = New-Object HotKeyWin
$global:HotkeyOk = $global:Hotkey.Register(6, 0x0D)
$global:Hotkey.add_Pressed({ Show-QuickAdd })

# 바탕화면 고정: 다른 창 뒤(바탕화면 위)로 보내고 작업표시줄 버튼 숨김
# 고정 + '아이콘 뒤에 표시' 이면 배경화면처럼 바탕화면 아이콘보다 뒤에 끼워 넣는다(실패하면 예전처럼 다른 창 뒤)
function Apply-Pin {
  $form.ShowInTaskbar = $false; $form.KeepBottom = ([bool]$global:Cfg.pinned -and -not $global:Unlocked)
  $wantEmbed = ([bool]$global:Cfg.pinned -and [bool]$global:Cfg.behind -and -not $global:Unlocked -and -not [DeskEmbed]::Hold -and $form.Visible)
  if ($wantEmbed) {
    if (-not [DeskEmbed]::Attach($form.Handle)) { $wantEmbed = $false; Log-Error '아이콘 뒤 배치 실패: WorkerW 를 찾지 못해 일반 고정으로 표시합니다' }
  } else { [DeskEmbed]::Detach($form.Handle) }
  if (-not $wantEmbed -and $global:Cfg.pinned -and -not $global:Unlocked) { [void][Win]::SetWindowPos($form.Handle,[IntPtr]1,0,0,0,0,0x13) }
}
# 앞으로 꺼내 쓰기(글자 입력 등): 아이콘 뒤에서는 키보드를 받을 수 없으므로 잠깐 빼낸다
function Bring-Front { [DeskEmbed]::Detach($form.Handle); $form.KeepBottom=$false; $form.Show(); $form.WindowState='Normal'; $form.Activate() }
function Show-Owned($dlg){
  $was = [DeskEmbed]::Active; [DeskEmbed]::Hold = $true
  if ($was) { [DeskEmbed]::Detach($form.Handle) }
  try { [void]$dlg.ShowDialog($global:Main) } finally { [DeskEmbed]::Hold = $false; if ($was) { Apply-Pin } }
}
$form.Add_Deactivate({ if($global:Cfg.pinned -and -not $global:Unlocked -and -not [DeskEmbed]::Hold){ Apply-Pin } })
$form.Add_Resize({ if ($form.WindowState -eq 'Minimized' -and $global:Cfg.pinned) { [void][Win]::ShowWindow($form.Handle,4) } })     # 바탕화면 보기(Win+D)에서도 사라지지 않게
$form.Add_ResizeEnd({ if($form.WindowState -eq 'Normal'){ $global:Cfg.x=$form.Left; $global:Cfg.y=$form.Top; $global:Cfg.w=$form.Width; $global:Cfg.h=$form.Height; Save-Cfg } })

# ---------------------------------------------------------------- 위치·크기 변경 모드 (평소엔 고정, 버튼을 눌러야 옮기거나 크기를 바꿀 수 있음)
$global:Unlocked = $false
function Update-LayoutButton {
  $T=$global:T
  if ($global:Unlocked) { $btnLayout.Text='✔ 고정'; $btnLayout.BackColor=$T.glow; $btnLayout.ForeColor=$T.pillTxt; $btnLayout.FlatAppearance.MouseOverBackColor=$T.glow }
  else { $btnLayout.Text='위치·크기 변경'; $btnLayout.BackColor=[Drawing.Color]::Transparent; $btnLayout.ForeColor=$T.hdrTxt; $btnLayout.FlatAppearance.MouseOverBackColor=$T.btnHover }
  if ($miLayout) { $miLayout.Text = if ($global:Unlocked) { '✔ 고정 (위치·크기 확정)' } else { '위치·크기 변경' } }
}
function Set-LayoutMode($unlock){
  $global:Unlocked = [bool]$unlock
  $form.ResizeEnabled = $global:Unlocked
  $form.KeepBottom = ([bool]$global:Cfg.pinned -and -not $global:Unlocked)
  if ($global:Unlocked) {
    if (-not $form.Visible) { $form.Show() }
    [DeskEmbed]::Detach($form.Handle)
    [void][Win]::SetWindowPos($form.Handle,[IntPtr]::Zero,0,0,0,0,0x13)

    $form.Activate()
  } else {
    if ($form.WindowState -eq 'Normal') { $global:Cfg.x=$form.Left; $global:Cfg.y=$form.Top; $global:Cfg.w=$form.Width; $global:Cfg.h=$form.Height; Save-Cfg }
    Apply-Pin

  }
  Update-LayoutButton
  $form.Invalidate($true)
}
# ---------------------------------------------------------------- 메뉴 (☰ 버튼 + 트레이 아이콘 공용)
$global:Exiting=$false
$menu=New-Object Windows.Forms.ContextMenuStrip
function Sep { [void]$menu.Items.Add((New-Object Windows.Forms.ToolStripSeparator)) }
$miMemo=$menu.Items.Add('개인 메모 입력 (선택한 날짜)');        $miMemo.Add_Click({ Show-DayEditor $global:Selected $null })
$miAdd=$menu.Items.Add('부서 일정 등록 (온나라 방식)');         $miAdd.Add_Click({ Show-EventDialog $null $global:Selected })
$miSearch=$menu.Items.Add('일정 검색 (Ctrl+F)');              $miSearch.Add_Click({ Show-Search })
$miNote=$menu.Items.Add('새 바탕화면 메모');                    $miNote.Add_Click({ New-Note })
$miLayout=$menu.Items.Add('위치·크기 변경');             $miLayout.Add_Click({ Set-LayoutMode (-not $global:Unlocked) })
$miNoteVis=$menu.Items.Add('메모 숨기기 / 보이기');             $miNoteVis.Add_Click({ $global:NotesHidden = -not $global:NotesHidden; Reload-Notes })
$miQuick=$menu.Items.Add('빠른 일정 등록 (Ctrl+Shift+Enter)'); $miQuick.Add_Click({ Show-QuickAdd })
$miToday=$menu.Items.Add('오늘로 이동');                      $miToday.Add_Click({ $global:View=(Get-Date -Day 1).Date; $global:Selected=(Get-Date).Date; Refresh-All })
Sep
$miImport=$menu.Items.Add('기존 일정 가져오기 (개인 일정으로)'); $miImport.Add_Click({ Import-Legacy })
$miBackup=$menu.Items.Add('내 계정 복구 파일 저장');           $miBackup.Add_Click({ Export-Account })
$miRestore=$menu.Items.Add('이전 PC에서 쓰던 계정 복구');      $miRestore.Add_Click({ if (Restore-Account) { $global:Events=@{}; $global:LastRaw=''; $global:FirstLoad=$true; Refresh-All; [void][Windows.Forms.MessageBox]::Show('계정을 복구했습니다.') } })
Sep
$miSet=$menu.Items.Add('글꼴 · 공휴일 설정...');                $miSet.Add_Click({ Show-Settings })
$miTheme=New-Object Windows.Forms.ToolStripMenuItem '테마'
foreach ($pair in @(@('light','라이트 (Notion 스타일)'),@('dark','다크 (Linear 스타일)'),@('glass','유리 (Windows 11 스타일, 바탕화면이 비침)'))) {
  $it=$miTheme.DropDownItems.Add($pair[1]); $it.Tag=$pair[0]; $it.Checked=($global:Cfg.theme -eq $pair[0])
  $it.Add_Click({ param($s,$e) $global:Cfg.theme=[string]$s.Tag; Save-Cfg; foreach($x in $miTheme.DropDownItems){ $x.Checked=([string]$x.Tag -eq [string]$s.Tag) }; Apply-Theme })
}
[void]$menu.Items.Add($miTheme)
$miOpa=New-Object Windows.Forms.ToolStripMenuItem '투명도'
foreach ($v in 100,90,80,70,60,50) {
  $it=$miOpa.DropDownItems.Add("$v%"); $it.Tag=$v; $it.Checked=([int]$global:Cfg.opacity -eq $v)
  $it.Add_Click({ param($s,$e) $global:Cfg.opacity=[int]$s.Tag; Save-Cfg; foreach($x in $miOpa.DropDownItems){ $x.Checked=([int]$x.Tag -eq [int]$s.Tag) }; Apply-Theme })
}
[void]$menu.Items.Add($miOpa)
$miPin=$menu.Items.Add('바탕화면 고정 (다른 창 뒤에 표시)'); $miPin.CheckOnClick=$true; $miPin.Checked=[bool]$global:Cfg.pinned
$miPin.Add_Click({ $global:Cfg.pinned=$miPin.Checked; Save-Cfg; Apply-Pin })
$miBehind=$menu.Items.Add('아이콘 뒤에 표시 (배경화면처럼)'); $miBehind.CheckOnClick=$true; $miBehind.Checked=[bool]$global:Cfg.behind
$miBehind.Add_Click({ $global:Cfg.behind=$miBehind.Checked; Save-Cfg; Apply-Pin })
$miAuto=$menu.Items.Add('윈도우 시작 시 자동 실행'); $miAuto.CheckOnClick=$true
$lnk=Join-Path ([Environment]::GetFolderPath('Startup')) '부서일정달력.lnk'
$miAuto.Checked=(Test-Path -LiteralPath $lnk)
$miAuto.Add_Click({
  if($miAuto.Checked){
    $ws=New-Object -ComObject WScript.Shell; $sc=$ws.CreateShortcut($lnk)
    if ($env:DEPT_EXE_PATH) { $sc.TargetPath=$env:DEPT_EXE_PATH; $sc.Arguments='' } else { $sc.TargetPath='wscript.exe'; $sc.Arguments="`"$(Join-Path $PSScriptRoot '달력실행.vbs')`"" }; $sc.Save()
  } else { Remove-Item -LiteralPath $lnk -Force -ErrorAction SilentlyContinue }
})
$miName=$menu.Items.Add('이름 변경')
$miName.Add_Click({
  $n = Show-NameDialog $global:Cfg.user '이름 변경' $false
  if(-not $n -or $n -eq $global:Cfg.user){ return }
  try {
    $r = Api PATCH "/rest/v1/members?user_id=eq.$($global:Cfg.userId)" (@{name=$n}|ConvertTo-Json) 'return=minimal'
    if($r.S -eq 409){ [void][Windows.Forms.MessageBox]::Show("'$n' 이름은 이미 사용 중입니다."); return }
    if($r.S -ge 300){ [void][Windows.Forms.MessageBox]::Show("변경 실패 ($($r.S))"); return }
    [void](Api PATCH "/rest/v1/events?owner_id=eq.$($global:Cfg.userId)" (@{owner_name=$n}|ConvertTo-Json) 'return=minimal')
    $global:Cfg.user=$n; Save-Cfg; $global:LastRaw=''; Refresh-All
  } catch { [void][Windows.Forms.MessageBox]::Show('서버에 연결할 수 없습니다.') }
})
Sep
$miHide=$menu.Items.Add('숨기기 (트레이로)'); $miHide.Add_Click({ $form.Hide() })
$miExit=$menu.Items.Add('종료');             $miExit.Add_Click({ $global:Exiting=$true; $tray.Visible=$false; $form.Close(); [Windows.Forms.Application]::Exit() })
$btnMenu.Add_Click({ $menu.Show($btnMenu,(P 0 $btnMenu.Height)) })

$tray=New-Object Windows.Forms.NotifyIcon; $tray.Icon=[Drawing.SystemIcons]::Information; if ($env:DEPT_EXE_PATH) { try { $tray.Icon=[Drawing.Icon]::ExtractAssociatedIcon($env:DEPT_EXE_PATH) } catch {} }; $tray.Text='부서 일정 공유 달력'; $tray.Visible=$true
$tray.ContextMenuStrip=$menu
$showMain={ Bring-Front }
$tray.Add_DoubleClick($showMain)
$form.Add_FormClosing({ param($s,$e) if(-not $global:Exiting){ $e.Cancel=$true; $form.Hide() } })

# ---------------------------------------------------------------- 실시간 동기화 (3초마다)
$poll=New-Object Windows.Forms.Timer; $poll.Interval=3000
$poll.Add_Tick({
  try {
    if(Sync-Data){ $lblMonth.Text=$global:View.ToString('yyyy년 M월'); Refresh-Legend; $grid.Invalidate(); Flush-Pending }
    Check-Reminders
    $tt=(Today-Text); if($lblToday.Text -ne $tt){ $lblToday.Text=$tt }
    if($global:Online){
      $poll.Interval=3000
      $lblStat.ForeColor=[Drawing.Color]::MediumSeaGreen; $lblStat.Text="● 연결됨  $($global:Cfg.user)  ($((Get-Date).ToString('HH:mm:ss')))"
    } else {
      $poll.Interval=10000
      $lblStat.ForeColor=[Drawing.Color]::Tomato
      $lblStat.Text= if($global:AuthLost){'● 인증 만료 - 관리자에게 문의'}else{'● 인터넷 연결 끊김 - 재시도 중'}
    }
  } catch { Log-Error $_.Exception.ToString() }
})

$form.Add_Shown({
  try { $corner=2; [void][Win]::DwmSetWindowAttribute($form.Handle,33,[ref]$corner,4) } catch {}    # Windows 11 둥근 모서리
  Check-Schema
  if ($env:DEPT_FORCESCHEMA) { $global:HasSchema = $true }     # 개발용 점검 전용
  Ensure-Identity
  Load-HolCache; Load-Remind; Load-Notes
  Apply-Theme
  Apply-Fonts
  Refresh-All
  Apply-Pin
  $poll.Start()
  Queue-Holidays $global:View.Year; if ($global:HolQueue.Count -gt 0) { $holTimer.Start() }
  foreach ($n in @($global:Notes)) { Open-Note $n $false }
  if(-not $global:HasSchema -and -not $env:DEPT_SELFTEST){ Show-Toast ([Drawing.Color]::DarkOrange) '서버 설정이 필요합니다' "개인 일정/완료 표시를 쓰려면 관리자가`n서버설정.sql 을 한 번 실행해야 합니다." }
  $todays=@(Events-On (Get-Date))
  if($todays.Count -gt 0 -and -not $env:DEPT_SELFTEST){
    $lines=($todays | Select-Object -First 3 | ForEach-Object { "$($_.owner): $($_.title)" }) -join "`n"
    Show-Toast ([Drawing.Color]::FromArgb(25,118,210)) "오늘 일정 $($todays.Count)건" $lines
  }
})

# 자체 점검(개발용): 대화상자들을 차례로 열었다 닫고 종료
if($env:DEPT_SELFTEST){
  $global:stStep=0
  $form.Add_Shown({
    $form.TopMost=$true; $form.Activate()
    $st=New-Object Windows.Forms.Timer; $st.Interval= $(if ($env:DEPT_STEPMS) { [int]$env:DEPT_STEPMS } else { 1500 })
    $st.Add_Tick({
      $global:stStep++
      $close={ foreach($f in @([Windows.Forms.Application]::OpenForms)){ if($f -ne $form -and $f.Visible -and -not ($f -is [SizableForm])){ $f.Close() } } }
      switch($global:stStep){
        1 {
          $lg={ param($m) Add-Content -LiteralPath (Join-Path $AppDir 'selftest.txt') -Value $m -Encoding UTF8 }
          $L=@{ title='개인 암호화 테스트'; place=''; kind='개인'; start='2026-10-20'; end='2026-10-20'; time='14:00'; memo='메모 한글'; done=$false; scope='personal' }
          $ok=Save-Event $null $L; & $lg "HasSchema=$($global:HasSchema) personal save=$ok"
          Refresh-All
          $e=@($global:Events.Values | Where-Object { $_.title -eq '개인 암호화 테스트' })
          & $lg "읽어서 복호화: found=$($e.Count) scope=$($e[0].scope) time=$($e[0].time) memo=$($e[0].memo)"
          if ($e.Count) {
            Set-Done $e[0] $true; Refresh-All
            $e2=@($global:Events.Values | Where-Object { $_.title -eq '개인 암호화 테스트' }); & $lg "완료 표시 후 done=$($e2[0].done)"
            [void](Delete-Event $e2[0]); Refresh-All
            & $lg "삭제 후 남은 건수=$(@($global:Events.Values | Where-Object { $_.title -eq '개인 암호화 테스트' }).Count)"
          }
        }
        2 { Show-EventDialog $null (Get-Date).Date }
        3 { & $close }
        4 { Show-QuickAdd }
        5 { & $close }
        6 { Show-DayList (Get-Date).Date }
        7 { & $close }
        8 { Show-Settings }
        9 { & $close }
        10 { Show-Search }
        11 { & $close }
        12 { New-Note }
        13 {
          $lg={ param($m) Add-Content -LiteralPath (Join-Path $AppDir 'selftest.txt') -Value $m -Encoding UTF8 }
          & $lg "메모 개수=$($global:Notes.Count) 열린 창=$($global:NoteForms.Count) api=$($global:NoteApi.Count)"
          $nid=$global:Notes[0].id; $nf=$global:NoteForms[$nid]
          & $global:NoteApi[$nid] $true
          & $lg "메모 접기: 높이=$($nf.Height) col=$($global:Notes[0].col)"
        }
        14 { Show-DayEditor (Get-Date).Date $null }
        15 {
          $lg={ param($m) Add-Content -LiteralPath (Join-Path $AppDir 'selftest.txt') -Value $m -Encoding UTF8 }
          $r=$global:EditorRtb; $r.Text="1. 14:00 에디터테스트 A`n2. 에디터테스트 B"
          $r.Select($r.Text.Length-3,0); & $global:EditorToggle          # 2번째 줄에 취소선(완료)
          & $global:EditorSave
          $td2=(Get-Date).Date.ToString('yyyy-MM-dd')
          $ev=@($global:Events.Values | Where-Object { $_.scope -eq 'personal' -and $_.start -eq $td2 -and $_.title -like '에디터테스트*' } | Sort-Object created)
          & $lg "편집 팝업 저장: 건수=$($ev.Count) [$((($ev | ForEach-Object { "$($_.title)|time=$($_.time)|done=$($_.done)" }) -join ' ; '))]"
        }
        16 {
          & $close
          $lg={ param($m) Add-Content -LiteralPath (Join-Path $AppDir 'selftest.txt') -Value $m -Encoding UTF8 }
          $nid=$global:Notes[0].id; $nf=$global:NoteForms[$nid]
          & $global:NoteApi[$nid] $false
          & $lg "메모 펴기: 높이=$($nf.Height) col=$($global:Notes[0].col) 맨뒤유지=$($nf.KeepBottom)"
        }
        17 { Set-LayoutMode $true }
        18 { Set-LayoutMode $false }
        19 {
          $lg={ param($m) Add-Content -LiteralPath (Join-Path $AppDir 'selftest.txt') -Value $m -Encoding UTF8 }
          $td2=(Get-Date).Date.ToString('yyyy-MM-dd')
          foreach ($x in @($global:Events.Values | Where-Object { $_.scope -eq 'personal' -and $_.title -like '에디터테스트*' })) { [void](Delete-Event $x) }
          Refresh-All; & $lg "정리 후 남은 테스트 일정=$(@($global:Events.Values | Where-Object { $_.title -like '에디터테스트*' }).Count)"
        }
        20 {
          $lg={ param($m) Add-Content -LiteralPath (Join-Path $AppDir 'selftest.txt') -Value $m -Encoding UTF8 }
          $tmpf=Join-Path $env:TEMP '공문_테스트.txt'; [IO.File]::WriteAllText($tmpf,'공문 내용 123',(New-Object Text.UTF8Encoding($false)))
          $u=Upload-File $tmpf; & $lg "첨부 업로드: $([bool]$u) 이름=$($u.name) 크기=$($u.size)"
          $L=@{ title='첨부 테스트'; place='테스트장소'; kind='행사'; start='2026-10-21'; end='2026-10-21'; time='10:00~11:00'; memo=''; done=$false; scope='dept'; files=@($u); conflict=$false }
          $global:LastSavedId=$null; $ok=Save-Event $null $L; Refresh-All
          $e=@($global:Events.Values | Where-Object { $_.title -eq '첨부 테스트' })
          & $lg "부서 일정 저장=$ok found=$($e.Count) files=$($e[0].files.Count) 파일명=$($e[0].files[0].name) 새 id 반환=$([bool]$global:LastSavedId)"
          $dst=Join-Path $env:TEMP 'dl_test.txt'; Download-File @{ path=$e[0].files[0].path } $dst
          & $lg "다운로드 내용=[$([IO.File]::ReadAllText($dst,(New-Object Text.UTF8Encoding($false))))]"
          $cf=@(Find-Conflicts @{ place='테스트장소'; scope='dept'; start='2026-10-21'; end='2026-10-21'; time='15:00' } '')
          & $lg "같은 장소 중복 판정=$($cf.Count)건 timeHit=$($cf[0].timeHit)"
          $cf2=@(Find-Conflicts @{ place='테스트장소'; scope='dept'; start='2026-10-21'; end='2026-10-21'; time='10:30' } '')
          & $lg "같은 장소+시간 겹침 판정=$($cf2.Count)건 timeHit=$($cf2[0].timeHit)"
          Remove-Files @($e[0].files[0].path); [void](Delete-Event $e[0]); Refresh-All
          & $lg "정리 후 남은 건수=$(@($global:Events.Values | Where-Object { $_.title -eq '첨부 테스트' }).Count)"
        }
        23 { $global:Exiting=$true; $tray.Visible=$false; [Windows.Forms.Application]::Exit() }
      }    })
    $st.Start()
  })
}
[Windows.Forms.Application]::Run($form)
$global:Hotkey.Release()
$tray.Visible=$false
