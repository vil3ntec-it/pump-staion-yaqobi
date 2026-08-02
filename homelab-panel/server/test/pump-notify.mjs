// ---------------------------------------------------------------------------
//  آزمونِ مرورگری: آیا خبرِ فرستاده‌شده واقعاً در «تانک تیل یعقوبی» می‌نشیند؟
//
//  سرورِ پنل بالا می‌آید، خودِ index.html در مرورگرِ واقعی باز می‌شود، و بعد با
//  همان دستورِ سادهٔ curl یک خبر فرستاده می‌شود. اگر زنگ قرمز شد و خبر در فهرست
//  آمد، یعنی زنجیره از سر تا ته کار می‌کند.
//
//      node test/pump-notify.mjs
// ---------------------------------------------------------------------------
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { chromium } from 'playwright-core';

const PORT = Number(process.env.TEST_PORT || 4880);
const SITE_PORT = PORT + 2;
const BASE = `http://127.0.0.1:${PORT}`;
const ROOT = path.join(import.meta.dirname, '..', '..', '..'); // ریشهٔ ریپو — همان‌جا که index.html است
const tmp = await fsp.mkdtemp(path.join(os.tmpdir(), 'hlp-pumpntf-'));

const CHROME =
  process.env.CHROME_PATH ||
  ['/opt/pw-browsers/chromium', '/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/google-chrome'].find(
    (p) => fs.existsSync(p)
  );

let passed = 0;
let failed = 0;
const check = (n, ok, extra = '') => {
  if (ok) { passed++; console.log(`  ✅ ${n}`); }
  else { failed++; console.log(`  ❌ ${n}${extra ? ' — ' + extra : ''}`); }
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---- سرورِ کوچکِ ایستا که خودِ سایت پمپ را سرو می‌کند (مثل GitHub Pages) ----
const TYPES = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.json': 'application/json', '.png': 'image/png' };
const siteServer = http.createServer(async (req, res) => {
  const rel = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  const file = path.join(ROOT, rel === '/' ? 'index.html' : rel.replace(/^\/+/, ''));
  if (!file.startsWith(ROOT)) { res.writeHead(403).end(); return; }
  try {
    const body = await fsp.readFile(file);
    res.writeHead(200, { 'Content-Type': TYPES[path.extname(file)] || 'application/octet-stream' }).end(body);
  } catch {
    res.writeHead(404).end('نیست');
  }
});
await new Promise((r) => siteServer.listen(SITE_PORT, '127.0.0.1', r));

const child = spawn(process.execPath, ['--disable-warning=ExperimentalWarning', path.join(import.meta.dirname, '..', 'src', 'index.js')], {
  env: {
    ...process.env,
    HLP_PORT: String(PORT), HLP_SITESYNC_PORT: String(PORT + 1), HLP_HOST: '127.0.0.1',
    HLP_DATA_DIR: path.join(tmp, 'data'), HLP_SITES_ROOT: path.join(tmp, 'sites'),
    HLP_METRICS_INTERVAL: '5000', HLP_TUNNEL: '0',
  },
  stdio: ['ignore', 'pipe', 'pipe'],
});
let out = '';
child.stdout.on('data', (d) => (out += d));
child.stderr.on('data', (d) => (out += d));

async function waitUp() {
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch(`${BASE}/health`)).ok) return; } catch { /* هنوز */ }
    await sleep(250);
  }
  throw new Error('سرور بالا نیامد');
}

/** فرستادنِ خبر، دقیقاً همان‌طور که یک برنامهٔ بیرونی می‌فرستد */
const publish = (topic, body, headers = {}) =>
  fetch(`${BASE}/api/notify/${topic}`, { method: 'POST', headers: { 'Content-Type': 'text/plain', ...headers }, body })
    .then((r) => r.json());

let browser;
try {
  if (!CHROME) throw new Error('کرومیوم پیدا نشد (CHROME_PATH را تنظیم کنید)');
  await waitUp();
  const admin = await fetch(`${BASE}/api/auth/setup`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'admin', password: 'HomeServer!2026' }),
  }).then((r) => r.json()).then((j) => j.token);

  browser = await chromium.launch({ executablePath: CHROME });
  const page = await browser.newPage({ viewport: { width: 420, height: 860 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });

  // سایت را به سرورِ آزمایشی وصل کن و قفل را رد کن (همان روشِ آزمون‌های دستی)
  await page.addInitScript((wsUrl) => {
    window.SELF_HOST_URL = wsUrl;
    window.SELF_HOST_TOKEN = '';
  }, `ws://127.0.0.1:${PORT}`);

  console.log('\n── باز شدنِ برنامهٔ پمپ ──');
  await page.goto(`http://127.0.0.1:${SITE_PORT}/index.html`, { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => typeof window.ntfOpen === 'function', { timeout: 20000 });
  await page.evaluate(() => {
    document.getElementById('lockScreen').style.display = 'none';
    document.getElementById('app').style.display = '';
  });
  check('برنامه باز شد و زنگ اعلان در سربرگ هست', await page.locator('#ntfBellBtn').isVisible());

  console.log('\n── خبری که از بیرون می‌آید ──');
  // برنامه ۱٫۵ ثانیه بعدِ بالا آمدن وصل می‌شود؛ به‌جای حدس زدنِ زمان، از خودِ
  // سرور می‌پرسیم تا بگوید یک شنونده روی موضوع نشسته است
  let listeners = 0;
  for (let i = 0; i < 60 && !listeners; i++) {
    listeners = (await fetch(`${BASE}/api/notify-admin`, { headers: { Authorization: `Bearer ${admin}` } })
      .then((r) => r.json())).stats?.listeners || 0;
    if (!listeners) await sleep(250);
  }
  check('برنامه خودش به موضوع pump وصل شد', listeners >= 1, `listeners=${listeners}`);

  const sent = await publish('pump', 'تیل مخزن ۲ کم شد');
  check('سرور خبر را پذیرفت', sent.ok === true, JSON.stringify(sent).slice(0, 120));
  check('و برنامهٔ باز، همان لحظه گرفتش', sent.live >= 1, `live=${sent.live}`);

  await page.waitForFunction(
    () => document.getElementById('ntfDot')?.style.display === 'flex',
    { timeout: 8000 }
  );
  check('زنگ نشانهٔ نخوانده گرفت', (await page.locator('#ntfDot').innerText()).trim() === '1');

  console.log('\n── فهرستِ خبرها ──');
  await page.click('#ntfBellBtn');
  await sleep(500);
  check('پنجرهٔ اعلان‌ها باز شد', await page.locator('#ntfModal.open').isVisible());
  check('متنِ خبر در فهرست هست', (await page.locator('#ntfList').innerText()).includes('تیل مخزن ۲ کم شد'));
  check('و بعد از باز کردن، نشانهٔ نخوانده پاک شد', (await page.locator('#ntfDot').getAttribute('style'))?.includes('none'));

  console.log('\n── عنوان و اولویت ──');
  await publish('pump', 'مخزن خالی شد', { 'X-Title': encodeURIComponent('هشدار'), 'X-Priority': '5' });
  await sleep(900);
  const listText = await page.locator('#ntfList').innerText();
  check('عنوانِ فارسی درست نشست', listText.includes('هشدار'), listText.slice(0, 80));
  check('خبرِ فوری کادرِ قرمز گرفت', (await page.locator('#ntfList .ntf-p5').count()) > 0);
  check('تازه‌ترین خبر بالای فهرست است', (await page.locator('#ntfList .ntf-item').first().innerText()).includes('مخزن خالی شد'));

  console.log('\n── تاریخچه بعد از باز شدنِ دوبارهٔ برنامه ──');
  const page2 = await browser.newPage({ viewport: { width: 420, height: 860 } });
  await page2.addInitScript((wsUrl) => { window.SELF_HOST_URL = wsUrl; window.SELF_HOST_TOKEN = ''; }, `ws://127.0.0.1:${PORT}`);
  await page2.goto(`http://127.0.0.1:${SITE_PORT}/index.html`, { waitUntil: 'domcontentloaded' });
  await page2.waitForFunction(() => typeof window.ntfOpen === 'function', { timeout: 20000 });
  await page2.evaluate(() => {
    document.getElementById('lockScreen').style.display = 'none';
    document.getElementById('app').style.display = '';
    window.ntfOpen();
  });
  await page2.waitForFunction(
    () => (document.getElementById('ntfList')?.innerText || '').includes('تیل مخزن ۲ کم شد'),
    { timeout: 12000 }
  );
  check('برنامه‌ای که تازه باز شده، خبرهای قبلی را هم می‌گیرد', true);

  console.log('\n── خطاهای کنسول ──');
  const real = errors.filter((e) => !/favicon|ResizeObserver|firebase|manifest|icon-/i.test(e));
  check('کنسولِ برنامه خطای واقعی ندارد', real.length === 0, real.slice(0, 2).join(' | '));

  console.log('\n════════════════════════════════════');
  console.log(`  موفق: ${passed}    ناموفق: ${failed}`);
  console.log('════════════════════════════════════\n');
} catch (e) {
  failed++;
  console.error('\n❌ آزمون شکست:', e.stack);
  console.error(out.slice(-1200));
} finally {
  await browser?.close();
  child.kill('SIGTERM');
  siteServer.close();
  await sleep(400);
  await fsp.rm(tmp, { recursive: true, force: true });
  process.exit(failed ? 1 : 0);
}
