# ═══════════════════════════════════════════════════════════════════════════
#  راننده‌ی ویزاردِ نصاب — همان کاری که آدم می‌کند: «بعدی»، پوشه، «نصب»
# ═══════════════════════════════════════════════════════════════════════════
#  چرا (۱۴۰۵/۰۷/۱۵): صاحب ریپو «نصاب ارور داد و انتخابِ فولدر نداشت». همهٔ
#  سنجه‌های قبلی بی‌صدا (/VERYSILENT) بودند و هیچ‌کدام از صفحه‌های ویزارد، پیام‌ها
#  و NextButtonClick رد نمی‌شدند. این اسکریپت نصاب را **با پنجره** باز می‌کند و با
#  پیامِ ویندوز (WM_COMMAND/WM_SETTEXT — بی ماوس) دکمه‌ها را می‌زند و هر پیامی که
#  نصاب نشان داد را با متنش ثبت می‌کند.
#
#  ⚠️ با BOM ذخیره شده تا هم pwsh و هم PowerShell 5.1 نوشتهٔ فارسی را درست بخوانند.
#  خروجی یک شیء است:
#    DirPage   صفحهٔ پوشه دیده شد؟         DirPrefill  نوشتهٔ پیش‌فرضِ کادرِ پوشه
#    Finished  به صفحهٔ «پایان» رسید؟       Messages    متنِ هر پیامِ نصاب
#    ExitCode  کدِ بیرون آمدن (پس از بستن)
param(
  [Parameter(Mandatory = $true)] [string]$Setup,
  [string[]]$Dirs = @(),          # هر بار که صفحهٔ پوشه دیده شد، بعدی از این فهرست
  [string[]]$SetupArgs = @(),
  [string]$Log = '',
  [int]$TimeoutSec = 300,
  [string[]]$Answer = @('Yes')    # جوابِ پیام‌های پرسشی: Yes یا No، به ترتیب
)

$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class PyqW {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, StringBuilder l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, string l);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);

  public static List<IntPtr> Tops(HashSet<uint> pids) {
    var r = new List<IntPtr>();
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (pids.Contains(p) && IsWindowVisible(h)) r.Add(h); return true; }, IntPtr.Zero);
    return r;
  }
  public static List<IntPtr> Kids(IntPtr top) {
    var r = new List<IntPtr>();
    EnumChildWindows(top, (h, l) => { r.Add(h); return true; }, IntPtr.Zero);
    return r;
  }
  public static string Cls(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
  public static string Text(IntPtr h) {
    int n = (int)SendMessage(h, 0x000E, IntPtr.Zero, IntPtr.Zero);   // WM_GETTEXTLENGTH
    var s = new StringBuilder(n + 2); SendMessage(h, 0x000D, (IntPtr)(n + 1), s); return s.ToString();
  }
  public static void SetText(IntPtr h, string t) { SendMessage(h, 0x000C, IntPtr.Zero, t); }   // WM_SETTEXT
  //  دکمهٔ VCL: WM_COMMAND/BN_CLICKED به پدر، با خودِ دکمه در lParam — Post، تا اگر
  //  کارِ دکمه پیامی باز کرد، راننده نماند.
  public static void Click(IntPtr btn) { PostMessage(GetParent(btn), 0x0111, IntPtr.Zero, btn); }
  public static void Command(IntPtr dlg, int id) { PostMessage(dlg, 0x0111, (IntPtr)id, IntPtr.Zero); }
}
"@

$argList = @($SetupArgs)
if ($Log) { $argList += "/LOG=$Log" }
$proc = Start-Process -FilePath $Setup -ArgumentList $argList -PassThru
$name = [IO.Path]::GetFileNameWithoutExtension($Setup)

$r = [ordered]@{ DirPage = $false; DirPrefill = ''; DirsUsed = @(); Finished = $false; Messages = @(); Pages = @(); ExitCode = $null }
$dirIx = 0; $ansIx = 0; $lastPage = ''; $lastSig = ''; $seenAny = $false; $started = Get-Date
$deadline = (Get-Date).AddSeconds($TimeoutSec)

function Plain($s) { ($s -replace '&', '').Trim() }

while ((Get-Date) -lt $deadline) {
  #  setup.exe خودش یک ‎.tmp‎ هم‌نام می‌سازد که ویزارد مالِ اوست
  $pids = New-Object 'System.Collections.Generic.HashSet[uint32]'
  #  ⚠️ نامِ فرآیندِ ویزارد «PumpYaqobi-Setup.tmp» است، نه «PumpYaqobi-Setup» — دات‌نت
  #  فقط پسوندِ ‎.exe‎ را برمی‌دارد. بارِ اول همین سنجه را کور کرد.
  Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq $name -or $_.ProcessName -eq "$name.tmp" } | ForEach-Object { [void]$pids.Add([uint32]$_.Id) }
  if ($pids.Count -eq 0) { break }

  $acted = $false
  foreach ($top in [PyqW]::Tops($pids)) {
    $cls = [PyqW]::Cls($top)
    $kids = [PyqW]::Kids($top)

    if ($cls -eq '#32770') {
      #  پیامِ نصاب (MessageBox) — متن و جواب
      $text = (($kids | Where-Object { [PyqW]::Cls($_) -eq 'Static' } | ForEach-Object { [PyqW]::Text($_) }) -join ' ').Trim()
      $ids = $kids | Where-Object { [PyqW]::Cls($_) -eq 'Button' } | ForEach-Object { [PyqW]::GetDlgCtrlID($_) }
      $r.Messages += "[$([PyqW]::Text($top))] $text"
      $a = if ($ansIx -lt $Answer.Count) { $Answer[$ansIx] } else { 'Yes' }
      if (-not $ids) {
        #  پنجرهٔ «تسک‌دیالوگ» (بی دکمهٔ Button) ⇒ TDM_CLICK_BUTTON؛ شناسه‌ای که
        #  نباشد نادیده گرفته می‌شود، پس «بله/نه» و بعد «تأیید».
        if ($text -eq '') { $r.Messages[-1] += ' (متن در لاگ)' }
        $yn = if ($a -eq 'No') { 7 } else { 6 }; $ansIx++
        [void][PyqW]::PostMessage($top, 0x0466, [IntPtr]$yn, [IntPtr]::Zero)
        [void][PyqW]::PostMessage($top, 0x0466, [IntPtr]1, [IntPtr]::Zero)
      } elseif ($ids -contains 6) {           # IDYES / IDNO
        $ansIx++
        if ($a -eq 'No') { [PyqW]::Command($top, 7) } else { [PyqW]::Command($top, 6) }
      } elseif ($ids -contains 1) { [PyqW]::Command($top, 1) }        # IDOK
      elseif ($ids -contains 3) { [PyqW]::Command($top, 3) }          # IDABORT (خطای نصب)
      else { [PyqW]::Command($top, 2) }
      $acted = $true
      break
    }

    if ($cls -ne 'TWizardForm') { continue }

    $vis = $kids | Where-Object { [PyqW]::IsWindowVisible($_) }
    $buttons = $vis | Where-Object { [PyqW]::Cls($_) -match 'Button' -and [PyqW]::IsWindowEnabled($_) }
    $names = $buttons | ForEach-Object { Plain ([PyqW]::Text($_)) }

    #  صفحهٔ پوشه: تنها صفحه‌ای که کادرِ مسیر دارد
    #  ⚠️ نامِ کلاسِ کادر در هر نسخهٔ Inno فرق دارد (TEdit/TNewEdit/…) — پس هر
    #  «…Edit»ی که نوشته‌اش مسیر است. (بارِ اول با TEdit صفحه را نشناخت.)
    $edit = $vis | Where-Object { [PyqW]::Cls($_) -match 'Edit' -and ([PyqW]::Text($_) -match '^[A-Za-z]:\\|^\\\\') } | Select-Object -First 1
    $page = if ($edit) { 'dir' } elseif ($names -contains 'پایان') { 'finish' } elseif ($names -contains 'نصب') { 'ready' } elseif ($names -contains 'بعدی') { 'next' } else { 'busy' }
    #  امضای هر صفحه (کلاس‌های دیدنی) برای گزارش — تا اگر صفحه‌ای شناخته نشد، معلوم باشد چه بود
    $sig = (($vis | ForEach-Object { [PyqW]::Cls($_) } | Sort-Object -Unique) -join ',')
    if ($page -ne $lastPage -or $sig -ne $lastSig) { $r.Pages += "$page [$sig]"; $lastPage = $page; $lastSig = $sig }

    if ($page -eq 'finish') {
      #  نصب تمام است (ثبتِ حذف پیش از این صفحه نوشته شده). «پایان» زده نمی‌شود
      #  چون تیکِ «باز کردنِ برنامه» روشن است و سنجه برنامه را خودش باز می‌کند.
      $r.Finished = $true
      Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq $name -or $_.ProcessName -eq "$name.tmp" } | Stop-Process -Force
      break
    }
    if ($page -eq 'dir') {
      if (-not $r.DirPage) { $r.DirPage = $true; $r.DirPrefill = [PyqW]::Text($edit) }
      if ($dirIx -lt $Dirs.Count) {
        [PyqW]::SetText($edit, $Dirs[$dirIx]); $r.DirsUsed += $Dirs[$dirIx]; $dirIx++
        Start-Sleep -Milliseconds 300
      }
    }
    if ($page -eq 'busy') { break }
    $btn = $buttons | Where-Object { (Plain ([PyqW]::Text($_))) -in @('نصب', 'بعدی') } | Select-Object -First 1
    if ($btn) { [PyqW]::Click($btn); $acted = $true }
    break
  }
  if (-not $seenAny -and $r.Pages.Count -eq 0 -and ((Get-Date) - $started).TotalSeconds -gt 20) {
    #  هیچ صفحه‌ای دیده نشد ⇒ نامِ پنجره‌های نصاب ثبت شود تا معلوم شود چرا
    $seenAny = $true
    $cl = ([PyqW]::Tops($pids) | ForEach-Object { [PyqW]::Cls($_) + ':' + [PyqW]::Text($_) }) -join ', '
    $r.Messages += "[راننده] پس از ۲۰ ثانیه هیچ صفحه‌ای؛ پنجره‌ها: $cl"
  }
  Start-Sleep -Milliseconds $(if ($acted) { 900 } else { 400 })
}

if (-not $proc.HasExited) {
  if ((Get-Date) -ge $deadline) {
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq $name -or $_.ProcessName -eq "$name.tmp" } | Stop-Process -Force
    $r.Messages += '[راننده] وقت تمام شد — ویزارد جلو نرفت'
  }
  $proc.WaitForExit(15000) | Out-Null
}
try { $r.ExitCode = $proc.ExitCode } catch { }
[pscustomobject]$r
