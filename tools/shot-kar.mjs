/* ══ عکسِ اپِ گوشی برای چشم ═══════════════════════════════════════════════
 *
 *     node tools/shot-kar.mjs <پوشهٔ عکس> [light|dark|both]
 *
 * همان اپِ کارمندان (‎kar/‎) در کرومیومِ بی‌پنجره با اندازهٔ یک گوشی (۳۹۰×۸۴۴)،
 * با ابرِ ساختگیِ ‎check-kar-ui.mjs‎ و یک عکسِ پُرتر (دوازده قرض‌دار، حساب،
 * بخش‌ها، خبر). از هر صفحه یک عکسِ تمام‌قد. هیچ چیزی را نمی‌سنجد — برای دیدن
 * است؛ سنجه‌ها ‎check-kar-ui.mjs‎ و ‎check-kar-bot.mjs‎اند.
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const globalRoot = execSync('npm root -g').toString().trim();
const { chromium } = createRequire(import.meta.url)(globalRoot + '/playwright');
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const OUT = process.argv[2] || 'kar-shots';
const WHICH = process.argv[3] || 'both';
fs.mkdirSync(OUT, { recursive: true });

const srv = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(ROOT, p);
  if (!fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  const ext = path.extname(f);
  res.setHeader('content-type', ext === '.js' ? 'text/javascript' : ext === '.html' ? 'text/html; charset=utf-8'
    : ext === '.png' ? 'image/png' : 'application/octet-stream');
  res.end(fs.readFileSync(f));
});
await new Promise(r => srv.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${srv.address().port}/kar/`;

const salt = crypto.randomBytes(16), iter = 1000;
const hash = crypto.pbkdf2Sync('1234', salt, iter, 32, 'sha256');
const gate = `pbkdf2$sha256$${iter}$${salt.toString('base64')}$${hash.toString('base64')}`;

const names = ['هارون', 'محمد کریم', 'علی‌احمد', 'نجیب‌الله', 'شرکتِ ترانسپورتی امید', 'داوود', 'پرویز', 'عبدالرحمن',
  'سید جلال', 'موترِ ۴۴۲۱', 'حاجی قدیر', 'زلمی'];
const st = ['ok', 'out', 'low', 'ok', 'over', 'ok', 'low', 'ok', 'none', 'ok', 'out', 'ok'];
const debtors = names.map((n, i) => ({
  id: i + 1, name: n, status: st[i] === 'over' ? 'out' : st[i],
  bal: { money: i % 3 === 0 ? -1500 * i : 0, petrol: (i % 4) * 120, diesel: i % 5 === 0 ? 0 : 60 * i },
  accounts: [{
    title: 'حسابِ اصلی', unit: i % 2 ? 'money' : 'fuel',
    head: ['تاریخ', 'نام', 'مقدار', 'فی', 'رسید', 'الباقی'],
    rows: Array.from({ length: 6 }, (_, r) => [`1405/07/0${r + 1}`, n, String(100 + r * 20), '68', r % 2 ? '5,000' : '', String(9000 - r * 700)]),
    sum: [['برد', '12,400'], ['رسید', '8,000'], ['الباقی', '4,400']],
  }],
}));
const snap = {
  v: 1, seq: 9, at: '1405/07/05', gate,
  station: { name: 'پمپِ بنزینِ یعقوبی' },
  banner: [['شرکت ها تیل (الباقی)', '1,212,000', 'accent'], ['قرض کل', '485,300', 'danger'], ['مفاد امروز', '18,800', 'ok'], ['مصارف امروز', '3,200', 'warn']],
  tank: { petrol: { in: 42000, out: 30500, show: 11500 }, diesel: { in: 24000, out: 23100, show: 900, low: true } },
  debtors,
  alerts: [
    { k: 'd2-stP-out', s: 'out', t: 'محمد کریم — پطرول تمام شد (۱۰۲٪)', a: 'به او دیگر پطرول ندهید.' },
    { k: 'd5-stD-over', s: 'out', t: 'شرکتِ ترانسپورتی امید — ۳۲۰ لیتر اضافه داده شده', a: 'بازپرسی کنید: چه کسی و چرا اضافه داد؟' },
    { k: 'tank-diesel', s: 'low', t: 'مخزنِ دیزل — ۹۰۰ لیتر مانده (حد: ۲٬۰۰۰)', a: 'امروز دیزل سفارش بدهید.' },
  ],
  sections: {
    expense: { t: 'مصارف', head: ['تاریخ', 'شرح', 'مبلغ'], rows: [['1405/07/01', 'برق', '2,300'], ['1405/07/02', 'نان کارمندان', '900'], ['1405/06/28', 'تعمیرِ پمپ', '4,000']], m: ['1405/07', '1405/07', '1405/06'], sum: [['جمله مصارف', '7,200']] },
    parcha: { t: 'پارچه', head: ['تاریخ', 'شیفت', 'لیتر', 'فروش', 'فایده'], rows: [['1405/07/01', 'روز', '1,200', '81,600', '4,800'], ['1405/07/01', 'شب', '900', '61,200', '3,600']], m: ['1405/07', '1405/07'], sum: [['جمله فایده', '8,400'], ['جمله لیتر', '2,100']] },
    safe: { t: 'گاوصندوق', head: ['تاریخ', 'شرح', 'بردگی', 'ماندگی'], rows: [['1405/07/03', 'فروش', '', '120,000']], m: ['1405/07'], sum: [['خالص', '120,000 افغانی']] },
    storage: { t: 'مخزن', head: ['تاریخ', 'تیل', 'لیتر', 'فی'], rows: [['1405/06/20', 'پطرول', '20,000', '62'], ['1405/06/25', 'دیزل', '12,000', '58']], m: ['1405/06', '1405/06'], sum: [] },
  },
};

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());

async function run(scheme) {
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: Number(process.env.SHOT_SCALE || 2), colorScheme: scheme });
  const page = await ctx.newPage();
  page.on('pageerror', e => console.log('pageerror: ' + e.message));
  await page.route('https://accounts.google.com/**', r => r.fulfill({ status: 200, body: '' }));
  await page.route('https://api.vill3n.top/**', async (route) => {
    const url = route.request().url();
    const h = { 'access-control-allow-origin': '*' };
    if (url.endsWith('/api/pump/public/join'))
      return route.fulfill({ status: 200, contentType: 'application/json', headers: h,
        body: JSON.stringify({ ok: true, station: { code: 'ac-one', name: snap.station.name, accessCode: '4829-1736' }, home: { url: '', readKey: '', station: 'ac-one' }, cloudLiveAt: 5 }) });
    if (url.includes('/api/pump/public/live'))
      return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: JSON.stringify({ ok: true, updatedAt: Date.now(), live: snap }) });
    return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: '{}' });
  });
  await page.route(/\/kar\/version\.json/, r => r.fulfill({ status: 200, contentType: 'application/json', body: '{"v":"10"}' }));
  const shot = async (name) => {
    await page.waitForTimeout(250);
    await page.screenshot({ path: path.join(OUT, `${scheme}-${name}.png`), fullPage: process.env.SHOT_FULL !== '0' });
  };
  const click = (sel) => page.click(sel);

  await page.goto(base);
  await page.waitForSelector('#codePane:not(.hidden)');
  await shot('01-code');
  await click('#btnGoogleWay'); await shot('02-signin');
  await click('#btnBackCode');
  await page.fill('#inCode', '48291736'); await click('#btnJoin');
  await page.waitForSelector('#paneHome:not(.hidden)');
  await page.waitForFunction(() => (document.getElementById('stName').textContent || '').length > 0);
  await shot('04-home');
  await click('.door.staff'); await shot('05-staff');
  await page.evaluate(() => { const e = document.getElementById('staffCounts'); window.scrollTo(0, e.getBoundingClientRect().top + scrollY - 190); });
  await shot('05b-staff-list');
  await page.evaluate(() => window.scrollTo(0, 0));
  await click('#staffList .st-row'); await shot('06-staff-person');
  await click('#modeSeg button[data-mode="owner"]');
  await page.waitForFunction(() => !document.getElementById('btnUnlock').disabled, null, { timeout: 8000 });
  await shot('03-lock');
  await page.fill('#inPass', '1234'); await click('#btnUnlock');
  await page.waitForSelector('#paneDash:not(.hidden)');
  await shot('07-dash');
  await page.evaluate(() => { const e = document.getElementById('bannerBox'); window.scrollTo(0, e.getBoundingClientRect().top + scrollY - 240); });
  await shot('07b-dash-numbers');
  await page.evaluate(() => window.scrollTo(0, 0));
  await click('#nav button[data-pane="paneSec"]'); await shot('08-sections');
  await click('#nav button[data-pane="paneDebt"]'); await shot('09-debtors');
  await click('#debtList button[data-pid="2"]'); await shot('10-person');
  await click('#nav button[data-pane="paneTank"]'); await shot('11-tank');
  await click('#nav button[data-pane="paneChat"]'); await shot('12-chat');
  await click('#nav button[data-pane="paneBot"]');
  await page.fill('#inAsk', 'مخزن'); await click('#btnAsk'); await shot('13-bot');
  await ctx.close();
}
for (const s of WHICH === 'both' ? ['light', 'dark'] : [WHICH]) await run(s);
await browser.close(); srv.close();
console.log('عکس‌ها در ' + OUT);
