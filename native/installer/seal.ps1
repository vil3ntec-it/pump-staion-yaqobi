# ═══════════════════════════════════════════════════════════════════════════
#  مُهرِ سلامتِ نصاب — پس از ساختن، پیش از چک‌سام و انتشار
# ═══════════════════════════════════════════════════════════════════════════
#  چرا (۱۴۰۵/۰۷/۱۵): روی هر دو کامپیوترِ صاحب ریپو نصاب وسطِ «Extracting files»
#  گفت «The source file is corrupted» — یعنی بایت‌های داخلِ همان فایلی که اجرا شد
#  با آن‌چه ساخته شده بود یکی نبودند (فایلِ روی گیت‌هاب سالم است: هشش با
#  SHA256SUMS.txt می‌خورد و CI همان را نصب کرد). Inno این را فقط وقتی می‌فهمد که
#  به همان فایلِ خراب برسد — نیمه‌راهِ نصب، با یک پیامِ انگلیسی.
#
#  این مُهر ۸۰ بایت ته فایل است:  PYSEAL01 + ‎SHA-256‎ (۶۴ نویسهٔ hex) + PYSEAL01
#  و ویزارد (PumpYaqobi.iss ⇒ SealState) پیش از نوشتنِ هر فایلی همان را
#  دوباره می‌سازد. خراب ⇒ پیامِ فارسی و هیچ چیزی روی دیسک نمی‌نشیند.
#
#  ⛔ شکلِ هش باید مو‌به‌مو همان کدِ Pascalِ نصاب باشد: فایل (بی ۸۰ بایتِ آخر)
#  تکه‌های ۴ مگابایتی ⇒ ‎SHA-256‎ِ هر تکه به hexِ کوچک ⇒ همه پشتِ هم ⇒
#  ‎SHA-256‎ِ همان رشته. (Pascal Script هشِ جریانی ندارد؛ این راه حافظهٔ کم
#  می‌خواهد و روی ویندوز ۷ هم می‌دود.)
#  ⚠️ Inno دادهٔ پس از بسته‌اش را نمی‌خواند (مثلِ امضای Authenticode که ته فایل
#  می‌نشیند)، پس مُهر به خودِ نصب دست نمی‌زند — installer-check.yml نصابِ
#  مُهرخورده را واقعاً نصب می‌کند.
param(
  [Parameter(Mandatory = $true)] [string]$Path,
  [switch]$Check                 # فقط بسنج: مُهر هست و می‌خورد؟
)
$ErrorActionPreference = 'Stop'
$Tag = [Text.Encoding]::ASCII.GetBytes('PYSEAL01')
$Chunk = 4194304
$Path = (Resolve-Path $Path).Path

function Hex([byte[]]$b) { -join ($b | ForEach-Object { $_.ToString('x2') }) }

function Digest([IO.FileStream]$f, [long]$len) {
  $sha = [Security.Cryptography.SHA256]::Create()
  $hexes = New-Object Text.StringBuilder
  $buf = New-Object byte[] $Chunk
  [void]$f.Seek(0, 'Begin')
  $left = $len
  while ($left -gt 0) {
    $n = [int][Math]::Min([long]$Chunk, $left)
    $got = 0
    while ($got -lt $n) {
      $r = $f.Read($buf, $got, $n - $got)
      if ($r -le 0) { throw "خواندنِ «$Path» نیمه‌کاره ماند" }
      $got += $r
    }
    [void]$hexes.Append((Hex $sha.ComputeHash($buf, 0, $n)))
    $left -= $n
  }
  Hex $sha.ComputeHash([Text.Encoding]::ASCII.GetBytes($hexes.ToString()))
}

function Tail([IO.FileStream]$f) {
  if ($f.Length -le 80) { return $null }
  $t = New-Object byte[] 80
  [void]$f.Seek(-80, 'End')
  $got = 0; while ($got -lt 80) { $got += $f.Read($t, $got, 80 - $got) }
  $a = [Text.Encoding]::ASCII.GetString($t, 0, 8); $b = [Text.Encoding]::ASCII.GetString($t, 72, 8)
  if ($a -ne 'PYSEAL01' -or $b -ne 'PYSEAL01') { return $null }
  [Text.Encoding]::ASCII.GetString($t, 8, 64)
}

$f = [IO.File]::Open($Path, 'Open', 'ReadWrite', 'Read')
try {
  $have = Tail $f
  if ($Check) {
    if (-not $have) { throw "«$Path» مُهرِ سلامت ندارد" }
    $want = Digest $f ($f.Length - 80)
    if ($want -ne $have) { throw "مُهرِ «$Path» نمی‌خورد (فایل پس از ساختن عوض شده)" }
    "✔ مُهرِ سلامت می‌خورد: $([IO.Path]::GetFileName($Path))  $have"
    return
  }
  #  ⛔ مُهرِ دوم روی مُهرِ اول ⇒ هشِ فایلِ مُهرخورده، که نصاب هرگز آن را نمی‌سازد
  if ($have) { throw "«$Path» از قبل مُهر خورده است" }
  $h = Digest $f $f.Length
  [void]$f.Seek(0, 'End')
  $f.Write($Tag, 0, 8)
  $hb = [Text.Encoding]::ASCII.GetBytes($h); $f.Write($hb, 0, 64)
  $f.Write($Tag, 0, 8)
  $f.Flush($true)
  "✔ مُهرِ سلامت: $([IO.Path]::GetFileName($Path))  $h"
} finally { $f.Dispose() }
