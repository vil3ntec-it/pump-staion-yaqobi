# ══ شورا، پ۲ — امضای یکپارچگیِ فایل‌های برنامه ════════════════════════════
#  یک جفت‌کلیدِ P-256ِ **یک‌بارمصرف** می‌سازد. ‎-Key‎ خروجیِ کلیدِ عمومی (SPKIِ
#  base64) برای ‎-p:IntegrityKey=‎ است؛ ‎-Sign <پوشه>‎ هشِ هر فایلِ ‎PumpYaqobi*‎
#  (‎.dll/.exe/.json‎، جز خودِ فهرست) را در ‎PumpYaqobi.integrity.json‎ امضا می‌کند.
#  ⛔ قاعده همان ‎Integrity.Covered/Canonical‎ است: «نام⇥هش\n»، مرتبِ ترتیبی.
#  کلیدِ خصوصی فقط در فایلِ موقتِ رانر است و پس از امضا پاک می‌شود.
param([switch]$Key, [string]$Sign, [string]$KeyFile = "$env:RUNNER_TEMP/pump-integrity.pem", [switch]$Done)
$ErrorActionPreference = 'Stop'
if ($Key) {
  $ec = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
  [System.IO.File]::WriteAllText($KeyFile, $ec.ExportECPrivateKeyPem())
  [Convert]::ToBase64String($ec.ExportSubjectPublicKeyInfo())
  return
}
if ($Sign) {
  $ec = [System.Security.Cryptography.ECDsa]::Create()
  $ec.ImportFromPem([System.IO.File]::ReadAllText($KeyFile))
  $names = [string[]](Get-ChildItem $Sign -File |
    Where-Object { $_.Name -like 'PumpYaqobi*' -and $_.Name -ne 'PumpYaqobi.integrity.json' -and @('.dll','.exe','.json') -contains $_.Extension.ToLowerInvariant() } |
    ForEach-Object { $_.Name })
  [Array]::Sort($names, [StringComparer]::Ordinal)
  $files = [ordered]@{}
  $sb = [System.Text.StringBuilder]::new()
  foreach ($n in $names) {
    $h = (Get-FileHash (Join-Path $Sign $n) -Algorithm SHA256).Hash.ToLowerInvariant()
    $files[$n] = $h
    [void]$sb.Append($n).Append("`t").Append($h).Append("`n")
  }
  $sig = [Convert]::ToBase64String($ec.SignData([System.Text.Encoding]::UTF8.GetBytes($sb.ToString()),
    [System.Security.Cryptography.HashAlgorithmName]::SHA256))
  $out = Join-Path (Resolve-Path $Sign).Path 'PumpYaqobi.integrity.json'
  [System.IO.File]::WriteAllText($out, (@{ v = 1; files = $files; sig = $sig } | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
  "یکپارچگی: $($names.Count) فایل در $Sign امضا شد"
}
if ($Done) { Remove-Item $KeyFile -Force -ErrorAction SilentlyContinue }
