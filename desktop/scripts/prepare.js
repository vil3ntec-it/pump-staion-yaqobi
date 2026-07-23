// ============================================================================
//  آماده‌سازیِ پیش از ساخت: فایل‌های خودِ برنامه (index.html و دارایی‌ها) را از
//  ریشهٔ ریپو داخل پوشهٔ desktop/app کپی می‌کند تا در نصب‌کنندهٔ ویندوز بسته شوند.
//  کل برنامه یک فایل است (index.html) پس این کار سبک و سریع است.
// ============================================================================
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..', '..');
const appDir = path.resolve(__dirname, '..', 'app');

// پاک‌سازی و ساخت دوبارهٔ پوشهٔ app
fs.rmSync(appDir, { recursive: true, force: true });
fs.mkdirSync(appDir, { recursive: true });

// فایل‌های تکی
const files = ['index.html', 'favicon.png', 'manifest.json'];
for (const f of files) {
  const src = path.join(repoRoot, f);
  if (fs.existsSync(src)) fs.copyFileSync(src, path.join(appDir, f));
}

// پوشهٔ آیکون‌ها (برای manifest و لوگوهای داخل برنامه)
const iconsSrc = path.join(repoRoot, 'icons');
if (fs.existsSync(iconsSrc)) {
  fs.cpSync(iconsSrc, path.join(appDir, 'icons'), { recursive: true });
}

console.log('✓ فایل‌های برنامه در desktop/app آماده شد.');
