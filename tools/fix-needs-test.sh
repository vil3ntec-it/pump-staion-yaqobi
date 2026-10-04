#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  شورا، ت۲ — «هر رفعِ باگ با آزمونِ اول سرخ»
#
#      tools/fix-needs-test.sh <base-sha> <head-sha> "<عنوانِ PR>" "<برچسب‌ها>" [فایلِ متنِ PR]
#
#  عنوانی که «اصلاح»، «باگ» یا «درست» دارد ⇒ PR باید دست‌کم یک فایلِ آزمونِ
#  ‎native/PumpYaqobi.Tests/*.cs‎ را بیفزاید یا عوض کند، و همان کلاس‌های آزمون
#  باید **روی base سرخ** (ساخت یا آزمون شکست بخورد) و **روی PR سبز** باشند.
#  یعنی آزمون واقعاً همان باگ را می‌گیرد، نه این‌که فقط کنارش نوشته شده باشد.
#
#  استثنا: برچسبِ ‎no-test‎ **و** واژهٔ «دلیل» در متنِ PR.
#  ⚠️ آزمونِ ‎tools/check-*.mjs‎ هم «آزمون» شمرده می‌شود ولی دو اجرای خودکار ندارد
#  (هر کدام ورودیِ خودش را دارد) — فقط نوشته می‌شود.
#  ‎FIX_DRY=1‎ ⇒ فقط تصمیم چاپ می‌شود، هیچ ساختی نمی‌دود (برای سنجیدنِ خودِ اسکریپت).
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail
BASE="$1"; HEAD="$2"; TITLE="$3"; LABELS="${4:-}"; BODY="${5:-}"

#  شورا، د۷: واژهٔ کامل — «درستی»، «نادرست» و «بادرستی» رفعِ باگ نیستند. «اصلاح…» و
#  «باگ…» از سرِ واژه (اصلاحات، باگ‌ها)، «درست» فقط تنها یا با نیم‌فاصله (درست‌شد).
#  ‎FIX_TITLE_ONLY=1‎ ⇒ فقط «fix» یا «nofix» چاپ می‌شود (آزمونِ همین قاعده).
is_fix() {
  printf '%s' "$1" | LC_ALL=C.UTF-8 grep -Pq '(?<!\p{L})(اصلاح|باگ)|(?<!\p{L})درست(?!\p{L})'
}
if [ "${FIX_TITLE_ONLY:-0}" = 1 ]; then
  if is_fix "$TITLE"; then echo fix; else echo nofix; fi; exit 0
fi
if ! is_fix "$TITLE"; then
  echo "عنوان رفعِ باگ نیست — این سنجه کاری ندارد."; exit 0
fi
if printf '%s' "$LABELS" | grep -Eqw 'no-test'; then
  if [ -n "$BODY" ] && [ -f "$BODY" ] && grep -q 'دلیل' "$BODY"; then
    echo "برچسبِ no-test با دلیل در متنِ PR — پذیرفته شد."; exit 0
  fi
  echo "::error::برچسبِ no-test هست ولی متنِ PR «دلیل» ندارد."; exit 1
fi

mapfile -t CS < <(git diff --name-only --diff-filter=AM "$BASE...$HEAD" -- 'native/PumpYaqobi.Tests/*.cs' | grep -v '/obj/' || true)
mapfile -t JS < <(git diff --name-only --diff-filter=AM "$BASE...$HEAD" -- 'tools/check-*.mjs' || true)
if [ "${#CS[@]}" -eq 0 ] && [ "${#JS[@]}" -eq 0 ]; then
  echo "::error::عنوانِ این PR رفعِ باگ است ولی هیچ آزمونی نیفزوده یا عوض نکرده. یک آزمون که روی main سرخ است بنویسید، یا برچسبِ no-test با دلیل."
  exit 1
fi
if [ "${#CS[@]}" -eq 0 ]; then
  echo "فقط آزمونِ ابزار عوض شده (${JS[*]}) — پذیرفته شد (دو اجرای خودکار ندارد)."; exit 0
fi

CLASSES=()
for f in "${CS[@]}"; do
  grep -qE '\[(Fact|Theory)' "$f" || continue
  while read -r c; do [ -n "$c" ] && CLASSES+=("$c"); done < <(grep -oP '^\s*public\s+(sealed\s+|static\s+|partial\s+)*class\s+\K\w+' "$f" || true)
done
if [ "${#CLASSES[@]}" -eq 0 ]; then
  echo "::error::فایلِ آزمونِ عوض‌شده هیچ کلاسِ [Fact]/[Theory] ندارد: ${CS[*]}"; exit 1
fi
FILTER=$(printf 'FullyQualifiedName~.%s.|' "${CLASSES[@]}"); FILTER="${FILTER%|}"
echo "کلاس‌های آزمون: ${CLASSES[*]}"
echo "صافی: $FILTER"
[ "${FIX_DRY:-0}" = 1 ] && { echo "DRY"; exit 0; }

run() { (cd "$1/native" && dotnet test PumpYaqobi.Tests -c Release --nologo --filter "$FILTER" >"$2" 2>&1); }

echo "── روی PR (باید سبز باشد)"
if ! run "$PWD" /tmp/fix-head.log; then
  tail -60 /tmp/fix-head.log
  echo "::error::آزمونِ تازه روی خودِ PR سبز نیست."; exit 1
fi

echo "── روی base با همان آزمون‌ها (باید سرخ باشد)"
WT=$(mktemp -d)/base
git worktree add -q --detach "$WT" "$BASE"
for f in "${CS[@]}"; do mkdir -p "$WT/$(dirname "$f")"; cp "$f" "$WT/$f"; done
if run "$WT" /tmp/fix-base.log; then
  tail -30 /tmp/fix-base.log
  echo "::error::همین آزمون‌ها روی base هم سبزند — پس باگ را نمی‌گیرند. آزمونی بنویسید که بی این اصلاح سرخ شود."
  git worktree remove --force "$WT"; exit 1
fi
git worktree remove --force "$WT"
echo "✅ روی base سرخ، روی PR سبز — آزمون واقعاً همان باگ را می‌گیرد."
