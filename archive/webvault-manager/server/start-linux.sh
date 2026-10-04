#!/usr/bin/env bash
# راه‌انداز سرور WebVault Manager روی لینوکس (سرور خانگی)
set -e
cd "$(dirname "$0")"

if ! command -v node >/dev/null 2>&1; then
  echo "خطا: Node.js نصب نیست. نسخهٔ ۲۲ به بالا را نصب کنید: https://nodejs.org"
  exit 1
fi

NODE_MAJOR=$(node -p "process.versions.node.split('.')[0]")
if [ "$NODE_MAJOR" -lt 22 ]; then
  echo "خطا: به Node.js نسخهٔ ۲۲ یا بالاتر نیاز است (نسخهٔ فعلی: $(node -v))."
  exit 1
fi

echo "در حال اجرای سرور WebVault Manager..."
exec node src/server.js
