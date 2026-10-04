#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  شورا، ت۴ — سنجه‌های سرتاسریِ زنده: برنامهٔ پمپ ⇄ پنلِ واقعی ⇄ سرورِ حسابِ واقعی
#
#      tools/live-stack.sh <server/homelab-panel/server> <shop/server> [سنجه‌ها...]
#
#  برای هر سنجه یک پشتهٔ **تازه** بالا می‌آید (پنل + سرورِ حساب روی PGlite +
#  صندوقِ ایمیلِ ساختگی، یا آینهٔ آپدیت با گیت‌هابِ ساختگی) و پس از آن بسته
#  می‌شود: سقفِ نرخِ ثبت‌نام و ‎device/bind‎ِ سرورِ حساب نباید سنجه‌ها را به هم
#  ببندد. پیش‌فرض: livestack signuptrial linkstates oldacct pumpmirror.
#  خروجیِ هر سنجه در ‎$LIVE_OUT‎ (پیش‌فرض /tmp/live-stack). کدِ بیرون آمدن صفر
#  فقط وقتی همه سبزند.
#  ⚠️ پیش از این ‎dotnet build native/PumpYaqobi.UiTests -c Release‎ لازم است.
# ═══════════════════════════════════════════════════════════════════════════
set -uo pipefail
SRV=$(cd "$1" && pwd); SHOP=$(cd "$2" && pwd); shift 2
PROBES=("$@"); [ ${#PROBES[@]} -eq 0 ] && PROBES=(livestack signuptrial linkstates oldacct pumpmirror)
OUT=${LIVE_OUT:-/tmp/live-stack}; mkdir -p "$OUT"
ROOT=$(cd "$(dirname "$0")/.." && pwd)
port=5200; failed=(); summary=()

for p in "${PROBES[@]}"; do
  port=$((port + 20)); live="$OUT/$p.json"; rm -f "$live"
  echo "══ $p (پورتِ $port)"
  if [ "$p" = pumpmirror ]; then
    TEST_PORT=$port setsid node "$SRV/test/pump-mirror-stack.mjs" "$live" >"$OUT/$p-stack.log" 2>&1 &
  else
    acct=0; [ "$p" = livestack ] && acct=1
    TEST_PORT=$port STACK_ACCOUNT=$acct HLP_ACCOUNT_DIR="$SHOP" setsid node "$SRV/test/signup-stack.mjs" "$live" >"$OUT/$p-stack.log" 2>&1 &
  fi
  stack=$!
  for _ in $(seq 1 360); do [ -s "$live" ] && break; kill -0 $stack 2>/dev/null || break; sleep 0.5; done
  if [ ! -s "$live" ]; then
    echo "::error::پشتهٔ $p بالا نیامد"; tail -40 "$OUT/$p-stack.log"
    failed+=("$p"); summary+=("❌ $p — پشته بالا نیامد"); kill -- -$stack 2>/dev/null; continue
  fi
  case $p in
    pumpmirror) args=(pumpmirror "$(node -p "require('$live').pub")" "$(node -p "require('$live').version")") ;;
    oldacct)    args=(oldacct "$live" "$OUT/$p" real) ;;
    *)          args=("$p" "$live" "$OUT/$p") ;;
  esac
  (cd "$ROOT/native" && timeout 1500 dotnet run --project PumpYaqobi.UiTests -c Release --no-build -- "${args[@]}") >"$OUT/$p.log" 2>&1
  rc=$?
  kill -- -$stack 2>/dev/null; sleep 1; kill -9 -- -$stack 2>/dev/null
  bad=$(grep -c '✖\|❌' "$OUT/$p.log" || true)
  if [ $rc -ne 0 ] || [ "$bad" != 0 ]; then
    echo "::error::$p سرخ (کد $rc، $bad ایراد)"; grep '✖\|❌' "$OUT/$p.log" | head -20; tail -15 "$OUT/$p.log"
    failed+=("$p"); summary+=("❌ $p — کد $rc، $bad ایراد")
  else
    summary+=("✅ $p — $(grep -c '✔\|✅' "$OUT/$p.log" || true) بند سبز")
  fi
done

printf '%s\n' "${summary[@]}" | tee "$OUT/summary.txt"
[ ${#failed[@]} -eq 0 ]
