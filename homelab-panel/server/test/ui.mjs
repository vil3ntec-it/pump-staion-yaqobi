// ---------------------------------------------------------------------------
// آزمون واقعی رابط کاربری با کرومیوم بی‌سر — گردش کار کاربر از ابتدا تا انتها
//   node test/ui.mjs
// ---------------------------------------------------------------------------
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
// playwright-core ممکن است در همین پوشه یا در ریشهٔ ریپو نصب باشد
const { chromium } = await (async () => {
  for (const spec of ['playwright-core', '../../../node_modules/playwright-core/index.mjs']) {
    try {
      return await import(spec.startsWith('.') ? new URL(spec, import.meta.url).href : spec);
    } catch { /* بعدی را امتحان کن */ }
  }
  throw new Error('playwright-core نصب نیست: در پوشهٔ server دستور «npm install --include=dev» را بزنید');
})();

const CHROME =
  process.env.CHROME_PATH ||
  ['/opt/pw-browsers/chromium-1194/chrome-linux/chrome', '/opt/pw-browsers/chromium/chrome'].find((p) =>
    fs.existsSync(p)
  );

const PORT = Number(process.env.TEST_PORT || 4793);
const BASE = `http://127.0.0.1:${PORT}`;
const tmp = await fsp.mkdtemp(path.join(os.tmpdir(), 'hlp-ui-'));
const sitesRoot = path.join(tmp, 'sites');
fs.mkdirSync(path.join(sitesRoot, 'shop'), { recursive: true });
fs.writeFileSync(path.join(sitesRoot, 'shop', 'index.html'), '<h1>shop</h1>', 'utf8');

let pass = 0;
let fail = 0;
const check = (name, ok, extra = '') => {
  if (ok) {
    pass++;
    console.log(`  ✅ ${name}`);
  } else {
    fail++;
    console.log(`  ❌ ${name}${extra ? ' — ' + extra : ''}`);
  }
};

const server = spawn(
  process.execPath,
  ['--disable-warning=ExperimentalWarning', path.join(import.meta.dirname, '..', 'src', 'index.js')],
  {
    env: {
      ...process.env,
      HLP_PORT: String(PORT),
      HLP_HOST: '127.0.0.1',
      HLP_DATA_DIR: path.join(tmp, 'data'),
      HLP_SITES_ROOT: sitesRoot,
      HLP_METRICS_INTERVAL: '700',
    },
    stdio: 'ignore',
  }
);

async function waitUp() {
  for (let i = 0; i < 80; i++) {
    try {
      const r = await fetch(`${BASE}/health`);
      if (r.ok) return;
    } catch { /* هنوز */ }
    await new Promise((r) => setTimeout(r, 200));
  }
  throw new Error('سرور بالا نیامد');
}

let browser;
try {
  if (!CHROME) throw new Error('کرومیوم پیدا نشد (CHROME_PATH را تنظیم کنید)');
  await waitUp();
  browser = await chromium.launch({ executablePath: CHROME });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const consoleErrors = [];
  page.on('pageerror', (e) => consoleErrors.push(e.message));
  page.on('console', (m) => {
    if (m.type() === 'error') consoleErrors.push(m.text());
  });

  console.log('\n── ورود و راه‌اندازی اولیه ──');
  await page.goto(BASE, { waitUntil: 'networkidle' });
  check('صفحهٔ راه‌اندازی اولیه باز شد', await page.locator('text=ساخت حساب مدیر').first().isVisible());

  await page.fill('input[autocomplete="username"]', 'admin');
  await page.fill('input[type="password"]', 'HomeServer!2026');
  await page.click('button:has-text("ساخت حساب مدیر")');
  await page.waitForSelector('text=داشبورد', { timeout: 15000 });
  check('بعد از ساخت حساب، داشبورد باز شد', true);

  console.log('\n── داشبورد ──');
  await page.waitForTimeout(2500); // یک چرخهٔ معیارها
  const cpuText = await page.locator('text=پردازنده').first().isVisible();
  check('کارت پردازنده دیده می‌شود', cpuText);
  check('کارت حافظه دیده می‌شود', await page.locator('text=حافظه').first().isVisible());
  check('کارت دیسک دیده می‌شود', await page.locator('text=دیسک').first().isVisible());
  const rings = await page.locator('svg circle').count();
  check('حلقه‌های درصد رسم شده‌اند', rings >= 6, `تعداد: ${rings}`);
  const chartPaths = await page.locator('svg path[stroke-width="2"]').count();
  check('نمودارهای زنده رسم شده‌اند', chartPaths >= 3, `تعداد: ${chartPaths}`);

  const uptimeShown = await page.locator('text=مدت روشن بودن').first().isVisible();
  check('مدت روشن بودن سرور نمایش داده می‌شود', uptimeShown);

  console.log('\n── سایت‌ها ──');
  await page.click('a[href="/sites"]');
  await page.waitForSelector('text=کشف خودکار', { timeout: 10000 });
  await page.click('button:has-text("کشف خودکار")');
  await page.waitForSelector('text=shop', { timeout: 15000 });
  check('کشف خودکار سایت واقعی را پیدا کرد', true);
  await page.click('button:has-text("افزودن انتخاب‌شده‌ها")');
  await page.waitForTimeout(1500);
  check('سایت به فهرست اضافه شد', (await page.locator('h2:has-text("shop")').count()) > 0);

  await page.click('button:has-text("اجرا")');
  await page.waitForTimeout(2500);
  const online = await page.locator('text=آنلاین').count();
  check('بعد از اجرا وضعیت آنلاین شد', online > 0);

  await page.click('button:has-text("لاگ‌ها")');
  await page.waitForSelector('h3:has-text("لاگ‌ها —")', { timeout: 8000 });
  await page.waitForSelector('pre', { timeout: 8000 });
  check('لاگ اختصاصی سایت باز شد', (await page.locator('pre >> text=static server listening').count()) > 0);
  await page.keyboard.press('Escape');

  console.log('\n── مانیتورینگ ──');
  await page.click('a[href="/monitoring"]');
  await page.waitForTimeout(2500);
  const monitorCharts = await page.locator('svg path[stroke-width="2"]').count();
  check('نمودارهای مانیتورینگ رسم شدند', monitorCharts >= 3, `تعداد: ${monitorCharts}`);

  console.log('\n── فایل‌منیجر ──');
  await page.click('a[href="/files"]');
  await page.waitForTimeout(1500);
  check('ریشهٔ سایت‌ها فهرست شد', (await page.locator('button:has-text("shop")').count()) > 0);
  await page.click('button:has-text("shop")');
  await page.waitForTimeout(1200);
  check('ورود به پوشهٔ سایت و دیدن فایل واقعی', (await page.locator('text=index.html').count()) > 0);

  console.log('\n── سرور سایت ──');
  await page.click('a[href="/site-server"]');
  await page.waitForSelector('text=آدرس سرور', { timeout: 8000 });
  check('آدرس ws:// نمایش داده می‌شود', (await page.locator('text=ws://').count()) > 0);
  await page.click('button:has-text("نمایش رمز")');
  await page.waitForTimeout(600);
  const tokenShown = await page.locator('code').first().innerText();
  check('رمز کامل نمایش داده شد', /^[0-9a-f]{30,}$/.test(tokenShown.trim()), tokenShown.slice(0, 12));

  console.log('\n── دامنه‌ها، لاگ‌ها، شبکه ──');
  await page.click('a[href="/domains"]');
  await page.waitForTimeout(900);
  check('صفحهٔ دامنه‌ها باز شد', (await page.locator('button:has-text("افزودن دامنه")').count()) > 0);
  await page.click('a[href="/network"]');
  await page.waitForTimeout(1200);
  check('صفحهٔ شبکه باز شد', (await page.locator('text=Gateway').count()) > 0);
  await page.click('a[href="/logs"]');
  await page.waitForTimeout(900);
  check('صفحهٔ لاگ‌ها باز شد', (await page.locator('text=پاک کردن لاگ‌ها').count()) > 0);

  console.log('\n── پوسته و زبان ──');
  await page.click('a[href="/settings"]');
  await page.waitForSelector('text=پوسته', { timeout: 8000 });
  await page.click('button:has-text("روشن")');
  await page.waitForTimeout(500);
  check('پوستهٔ روشن اعمال شد', (await page.getAttribute('html', 'data-theme')) === 'light');
  await page.click('button:has-text("English")');
  await page.waitForTimeout(600);
  check('زبان انگلیسی اعمال شد', (await page.getAttribute('html', 'lang')) === 'en');
  check('جهت صفحه به چپ‌به‌راست تغییر کرد', (await page.getAttribute('html', 'dir')) === 'ltr');
  check('ترجمه واقعاً عوض شد', (await page.locator('text=Settings').count()) > 0);
  await page.click('button:has-text("العربية")');
  await page.waitForTimeout(600);
  check('عربی راست‌به‌چپ است', (await page.getAttribute('html', 'dir')) === 'rtl');
  await page.click('button:has-text("پښتو")');
  await page.waitForTimeout(600);
  check('پشتو اعمال شد', (await page.getAttribute('html', 'lang')) === 'ps');

  console.log('\n── ریسپانسیو ──');
  await page.setViewportSize({ width: 390, height: 780 });
  await page.waitForTimeout(700);
  const bodyScroll = await page.evaluate(() => document.body.scrollWidth <= window.innerWidth + 2);
  check('در موبایل کشیدگی افقی ندارد', bodyScroll);
  check('دکمهٔ منوی موبایل دیده می‌شود', await page.locator('button[aria-label="menu"]').isVisible());

  console.log('\n── خطاهای کنسول ──');
  const realErrors = consoleErrors.filter((e) => !/favicon|ResizeObserver loop/i.test(e));
  check('کنسول مرورگر خطای واقعی ندارد', realErrors.length === 0, realErrors.slice(0, 2).join(' | '));
} catch (e) {
  fail++;
  console.log(`\n❌ خطای غیرمنتظره: ${e.stack}`);
} finally {
  await browser?.close();
  server.kill('SIGTERM');
  await new Promise((r) => setTimeout(r, 700));
  server.kill('SIGKILL');
  await fsp.rm(tmp, { recursive: true, force: true });
}

console.log(`\n════════════════════════════════════`);
console.log(`  موفق: ${pass}    ناموفق: ${fail}`);
console.log(`════════════════════════════════════\n`);
process.exit(fail ? 1 : 0);
