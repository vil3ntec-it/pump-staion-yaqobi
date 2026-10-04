# ══ شورا، پ۲ — «یک DLLِ دست‌کاری‌شده شش بخش را باز نمی‌کند» ══════════════
#  همان برنامهٔ ساخته‌شده را با ‎--integrity‎ می‌زند (بی پنجره، ‎Integrity.SelfTestExitCode‎):
#  سالم ⇒ ۰ · یک بایت ته ‎PumpYaqobi.Services.dll‎ ⇒ ۳ · برگرداندن ⇒ دوباره ۰.
#  ⛔ فایل بایت‌به‌بایت برمی‌گردد (finally).
param([Parameter(Mandatory)][string]$Dir)
$ErrorActionPreference = 'Stop'
$Dir = (Resolve-Path $Dir).Path
function Run {
  $p = Start-Process -FilePath (Join-Path $Dir 'PumpYaqobi.exe') -ArgumentList '--integrity' -Wait -PassThru -WindowStyle Hidden
  return $p.ExitCode
}
function Why { Get-Content (Join-Path $env:TEMP 'pump-integrity.txt') -Raw -ErrorAction SilentlyContinue }
$a = Run
if ($a -ne 0) { throw "برنامهٔ سالم یکپارچه خوانده نشد (کد $a): $(Why)" }
$dll = Join-Path $Dir 'PumpYaqobi.Services.dll'
$orig = [System.IO.File]::ReadAllBytes($dll)
try {
  [System.IO.File]::WriteAllBytes($dll, [byte[]]($orig + [byte]0))
  $b = Run
  if ($b -ne 3) { throw "DLLِ دست‌خورده دیده نشد (کد $b): $(Why)" }
  "دست‌کاری دیده شد: $(Why)"
} finally { [System.IO.File]::WriteAllBytes($dll, $orig) }
$c = Run
if ($c -ne 0) { throw "پس از برگرداندن هم سالم نیست (کد $c): $(Why)" }
"✅ $Dir — سالم ⇒ ۰ · دست‌خورده ⇒ ۳ · برگشته ⇒ ۰"
