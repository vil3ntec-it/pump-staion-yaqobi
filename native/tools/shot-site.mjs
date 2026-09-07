// ---------------------------------------------------------------------------
//  ══ عکس‌گیر از نسخهٔ وب ═════════════════════════════════════════════════════
//
//      node native/tools/shot-site.mjs [پوشهٔ خروجی]
//
//  از هر هجده بخشِ نوارِ سایت یک عکس می‌گیرد، در پنجره‌ای دقیقاً هم‌اندازهٔ
//  پنجرهٔ برنامهٔ نیتیو (۱۴۴۰×۹۰۰ — همان چیزی که ‎AppSettings‎ می‌سازد).
//
//  همتای نیتیوش ‎PumpYaqobi.UiTests -- shots‎ است. دو خروجی کنارِ هم یعنی
//  می‌شود بخش‌به‌بخش دید که کجا فرق دارند.
//
//  ⚠️ چرا خودش بخش‌ها را از صفحه می‌خواند و فهرستِ دستی ندارد: اگر روزی
//  دکمه‌ای به نوارِ سایت اضافه شود، این ابزار خودش عکسش را هم می‌گیرد.
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';

const ROOT = path.join(import.meta.dirname, '..', '..');
const OUT = process.argv[2] || path.join(ROOT, 'shots-site');
fs.mkdirSync(OUT, { recursive: true });

const exe = process.env.PW_CHROMIUM;
const browser = await chromium.launch(
  exe ? { executablePath: exe, args: ['--no-sandbox'] } : { args: ['--no-sandbox'] });

const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
page.on('pageerror', () => {});

await page.goto(pathToFileURL(path.join(ROOT, 'index.html')).href,
                { waitUntil: 'domcontentloaded' });
// صفحه سنگین است و بخشی از چیدمان بعد از بار شدنِ داده می‌نشیند
await page.waitForTimeout(4500);

await page.evaluate(() => {
  // قفلِ رمز کنار می‌رود — همان کاری که ابزارِ نیتیو با ورودِ ۱۲۳۴ می‌کند
  const l = document.getElementById('lockScreen'); if (l) l.style.display = 'none';
  const a = document.getElementById('app'); if (a) a.style.display = '';
  try { window.currentRole = 'admin'; } catch (_) {}
  // ساعتِ شناور روی عکس می‌افتد و مقایسه را شلوغ می‌کند
  document.querySelectorAll('#glassClock, .glass-clock')
          .forEach((e) => { e.style.display = 'none'; });
});
await page.waitForTimeout(900);

const nav = await page.evaluate(() =>
  [...document.querySelectorAll('.nav-btn')]
    .map((b) => {
      const m = /showSection\('([^']+)'/.exec(b.getAttribute('onclick') || '');
      return m ? m[1] : null;
    })
    .filter(Boolean));

let n = 0;
for (const id of nav) {
  n += 1;
  const name = `${String(n).padStart(2, '0')}-${id}.png`;
  try {
    await page.evaluate((s) => window.showSection(s), id);
    await page.waitForTimeout(1100);
    await page.screenshot({ path: path.join(OUT, name) });
    console.log('✓', name);
  } catch (e) {
    console.log('✗', name, String(e.message).slice(0, 90));
  }
}

await browser.close();
console.log(`\n${n} عکس در ${OUT}`);
