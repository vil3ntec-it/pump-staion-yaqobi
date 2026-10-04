#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  شورا، د۷ — «دندانِ آزمون»: کدِ اصلی را عمداً خراب کن و ببین آزمون سرخ می‌شود.
#
#      tools/mut.sh <فایل> "<متنِ کهنه>" "<متنِ تازه>" "<صافیِ dotnet test>"
#
#  نخستین «متنِ کهنه» در فایل با «متنِ تازه» عوض می‌شود، همان آزمون‌ها می‌دوند و
#  فایل **همیشه** برمی‌گردد (حتی با Ctrl+C — ‎trap‎). خروجی: «🦷 دندان دارد»
#  وقتی دستِ‌کم یک آزمون سرخ شد؛ «⚠️ بی‌دندان» (کدِ ۱) وقتی همه سبز ماندند —
#  یعنی آن آزمون این قاعده را نمی‌گیرد. ساخت نشد ⇒ «ساخت نشد» (کدِ ۲)، چون
#  خطای کامپایل دندان نیست.
#
#  نمونه:
#      tools/mut.sh native/PumpYaqobi.Services/Data/SyncStore.cs \
#        'RenormalizeDebtRows(touchedDebt);' '' 'FullyQualifiedName~SyncConflictTests'
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail
[ $# -eq 4 ] || { sed -n '4,6p' "$0"; exit 64; }
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
F="$1"; [ -f "$F" ] || F="$ROOT/$1"
[ -f "$F" ] || { echo "فایل نیست: $1"; exit 64; }
F="$(cd "$(dirname "$F")" && pwd)/$(basename "$F")"   # پس از ‎cd‎ هم همان فایل
BAK="$(mktemp)"; cp "$F" "$BAK"
trap 'cp "$BAK" "$F"; rm -f "$BAK"' EXIT

python3 - "$F" "$2" "$3" <<'P'
import sys
p, o, n = sys.argv[1:4]
s = open(p, encoding='utf-8').read()
if o not in s:
    sys.exit("متنِ کهنه در فایل نیست")
open(p, 'w', encoding='utf-8').write(s.replace(o, n, 1))
P

cd "$ROOT/native"
if ! dotnet build PumpYaqobi.Tests -c Release --nologo -v q >/tmp/mut-build.log 2>&1; then
  grep -m5 " error " /tmp/mut-build.log || true
  echo "ساخت نشد — خطای کامپایل دندان نیست؛ جهشِ دیگری بنویسید."; exit 2
fi
if dotnet test PumpYaqobi.Tests -c Release --no-build --nologo --filter "$4" >/tmp/mut-test.log 2>&1; then
  grep -E "Passed!|Total" /tmp/mut-test.log | tail -1 || true
  echo "⚠️ بی‌دندان — با این خرابی همهٔ آزمون‌ها سبز ماندند."; exit 1
fi
grep -E "^\s+Failed |Failed!" /tmp/mut-test.log | head -10 || true
echo "🦷 دندان دارد — خرابی گرفته شد."
