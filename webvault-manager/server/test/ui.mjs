// آزمون رابط کاربری با کرومیوم بی‌سر (headless) — گردش واقعی کاربر را بررسی می‌کند.
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PORT = 4702;
const BASE = `http://127.0.0.1:${PORT}`;
let pass = 0, fail = 0;
const check = (n, c) => { if (c) { pass++; console.log('  ✓', n); } else { fail++; console.log('  ✗', n); } };

const srv = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'server.js')], {
  env: { ...process.env, WV_PORT: String(PORT), WV_DATA_DIR: '/tmp/wv-ui' }, stdio: 'ignore',
});

async function waitUp() {
  for (let i = 0; i < 50; i++) { try { const r = await fetch(BASE + '/api/status'); if (r.ok) return; } catch {} await new Promise((r) => setTimeout(r, 100)); }
  throw new Error('سرور بالا نیامد');
}

const execFileSync = (await import('node:child_process')).execFileSync;
execFileSync('rm', ['-rf', '/tmp/wv-ui']);

let browser;
try {
  await waitUp();
  browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });

  await page.goto(BASE, { waitUntil: 'networkidle' });

  // راه‌اندازی اولیه: تعیین رمز اصلی
  await page.fill('#masterPass', 'TestMaster123');
  await page.fill('#masterPass2', 'TestMaster123');
  await page.click('#lockBtn');
  await page.waitForSelector('.app.show', { timeout: 5000 });
  check('ورود و ورود به برنامه', await page.isVisible('.app.show'));

  // داشبورد بارگذاری شد
  await page.waitForSelector('.stat', { timeout: 5000 });
  check('داشبورد کارت‌های آمار را نشان می‌دهد', (await page.$$('.stat')).length >= 4);

  // ساخت مشتری
  await page.click('.nav-item[data-route="clients"]');
  await page.waitForSelector('[data-add]');
  await page.click('[data-add]');
  await page.waitForSelector('.modal-back.show');
  await page.fill('[name="name"]', 'مشتری آزمایشی');
  await page.fill('[name="email"]', 'test@demo.com');
  await page.click('[data-save]');
  await page.waitForSelector('.modal-back:not(.show)', { timeout: 5000 }).catch(() => {});
  await page.waitForSelector('table td');
  check('مشتری ساخته شد و در جدول دیده می‌شود', (await page.textContent('#content')).includes('مشتری آزمایشی'));

  // ساخت سایت
  await page.click('.nav-item[data-route="websites"]');
  await page.waitForSelector('[data-add]');
  await page.click('[data-add]');
  await page.waitForSelector('.modal-back.show');
  await page.fill('[name="name"]', 'سایت آزمایشی');
  await page.fill('[name="url"]', 'https://demo.example');
  await page.selectOption('[name="status"]', 'sold');
  await page.fill('[name="sale_price"]', '7500000');
  await page.fill('[name="tags_str"]', 'wordpress, sold');
  await page.click('[data-save]');
  await page.waitForTimeout(600);
  check('سایت ساخته شد', (await page.textContent('#content')).includes('سایت آزمایشی'));
  check('تگ سایت نمایش داده می‌شود', (await page.textContent('#content')).includes('#wordpress'));

  // ساخت رمز و کپی
  await page.click('.nav-item[data-route="passwords"]');
  await page.waitForSelector('[data-add]');
  await page.click('[data-add]');
  await page.waitForSelector('.modal-back.show');
  await page.fill('[name="title"]', 'ورود ادمین وردپرس');
  await page.fill('[name="username"]', 'admin');
  await page.click('[data-gen="secret"]'); // تولید رمز
  await page.waitForFunction(() => document.querySelector('[name="secret"]').value.length >= 12, { timeout: 3000 }).catch(() => {});
  const gen = await page.inputValue('[name="secret"]');
  check('تولید رمز کار می‌کند', gen && gen.length >= 12);
  await page.click('[data-save]');
  await page.waitForTimeout(600);
  check('رمز ذخیره شد و مخفی نمایش داده می‌شود', (await page.textContent('#content')).includes('••••••••'));

  // جستجوی سراسری
  await page.fill('#globalSearch', 'آزمایشی');
  await page.waitForSelector('.search-results.show', { timeout: 3000 });
  check('جستجو نتیجه می‌دهد', (await page.textContent('#searchResults')).includes('سایت آزمایشی'));

  // بازگشت به داشبورد و بررسی درآمد
  await page.click('.nav-item[data-route="dashboard"]');
  await page.waitForSelector('.stat');
  const dash = await page.textContent('#content');
  check('داشبورد سایت فروخته‌شده را می‌شمارد', dash.includes('فروخته‌شده'));

  check('هیچ خطای JS در کنسول رخ نداد', errors.length === 0);
  if (errors.length) console.log('    خطاها:', errors.slice(0, 5));
} catch (e) {
  console.error('خطا:', e.message); fail++;
} finally {
  if (browser) await browser.close();
  srv.kill();
  console.log(`\n  نتیجه UI: ${pass} موفق، ${fail} ناموفق`);
  process.exit(fail ? 1 : 0);
}
