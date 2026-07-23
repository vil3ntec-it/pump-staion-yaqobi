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

// ---------------------------------------------------------------------------
// جاسازیِ «سرور خانگی» داخل برنامه: server.js + کتابخانهٔ ws + package.json
// (بدون پوشهٔ data، بدون node-portable و بدون فایل‌های تست) تا برنامهٔ دسکتاپ
// بتواند خودش سرور را با یک کلیک اجرا کند — بدون نیاز به zip و bat جداگانه.
// ---------------------------------------------------------------------------
const srvOut = path.resolve(__dirname, '..', 'srv');
fs.rmSync(srvOut, { recursive: true, force: true });
fs.mkdirSync(srvOut, { recursive: true });
const srvSrc = path.join(repoRoot, 'server');
for (const f of ['server.js', 'package.json']) {
  const s = path.join(srvSrc, f);
  if (fs.existsSync(s)) fs.copyFileSync(s, path.join(srvOut, f));
}
// فقط node_modules (کتابخانهٔ ws) کپی می‌شود؛ data/node-portable/تست‌ها لازم نیستند
const nmSrc = path.join(srvSrc, 'node_modules');
if (fs.existsSync(nmSrc)) fs.cpSync(nmSrc, path.join(srvOut, 'node_modules'), { recursive: true });

console.log('✓ فایل‌های برنامه در desktop/app و سرور در desktop/srv آماده شد.');
