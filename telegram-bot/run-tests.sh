#!/usr/bin/env bash
# ---------------------------------------------------------------------------
#  آزمون‌های باتِ پمپ — روی سرورِ حسابِ واقعی (همان پردازه و همان دیتابیس که بات
#  در آن اجرا می‌شود). PGlite در حافظه؛ بی داکر و بی PostgreSQL.
#
#    SHOP_SERVER=<پوشهٔ shop/server با node_modules>  bash telegram-bot/run-tests.sh
#    (بی SHOP_SERVER ⇒ آخرین shop از گیت‌هاب کلون و نصب می‌شود)
#
#  ⛔ چیزی در shop نمی‌ماند: آزمون‌ها با پیشوندِ __pumpbot__ کنارِ آزمون‌های سرور
#     گذاشته و پس از اجرا پاک می‌شوند؛ خودِ بات با PUMP_BOT_DIR از همین پوشه بار می‌شود.
# ---------------------------------------------------------------------------
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="$(mktemp -d)"
SRC="${SHOP_SERVER:-}"
if [ -z "$SRC" ]; then
  git clone -q --depth 1 https://github.com/vil3ntec-it/shop.git "$WORK/shop"
  SRC="$WORK/shop/server"
  (cd "$SRC" && (npm ci --silent || npm install --silent))
fi
SRC="$(cd "$SRC" && pwd)"
#  ⛔ سرورِ حسابی که هنوز جای بات (src/lib/bots.js) را ندارد، باتِ کهنهٔ خودش را دارد —
#     دو بات در یک پردازه؛ آزمون معنا ندارد.
if [ ! -f "$SRC/src/lib/bots.js" ]; then echo "سرورِ حساب src/lib/bots.js ندارد (پیش از ۲.۱۲.۰)"; rm -rf "$WORK"; exit 1; fi
cleanup() { rm -f "$SRC"/test/__pumpbot__*.test.js; rm -rf "$WORK"; }
trap cleanup EXIT
for f in "$HERE"/test/*.test.js; do cp "$f" "$SRC/test/__pumpbot__$(basename "$f")"; done
cd "$SRC"
export PUMP_BOT_DIR="$HERE"
export TEST_DATABASE_URL="${TEST_DATABASE_URL:-pglite:memory}"
node --test --test-concurrency=1 test/__pumpbot__*.test.js
