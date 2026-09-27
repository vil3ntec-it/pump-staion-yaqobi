/* ══ عکسِ صفحهٔ کیو‌آرِ مشتری برای چشم ═════════════════════════════════════
 *
 *     node tools/shot-view.mjs <پوشهٔ عکس> [light|dark|both]
 *
 * ‎view/‎ در کرومیومِ بی‌پنجره با اندازهٔ گوشی، با همان دادهٔ نمونهٔ
 * ‎check-view.mjs‎ (دو دفتر، تفکیکِ تیل، آرشیو) و کیو‌آرِ زنده با سرورِ حسابِ
 * ساختگی. فقط برای دیدن است؛ سنجه ‎check-view.mjs‎ است.
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { deflateRawSync } from 'node:zlib';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const globalRoot = execSync('npm root -g').toString().trim();
const { chromium } = createRequire(import.meta.url)(globalRoot + '/playwright');
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const OUT = process.argv[2] || 'view-shots';
const WHICH = process.argv[3] || 'both';
fs.mkdirSync(OUT, { recursive: true });

const srv = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(ROOT, p);
  if (!fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  res.setHeader('content-type', f.endsWith('.js') ? 'text/javascript' : 'text/html; charset=utf-8');
  res.end(fs.readFileSync(f));
});
await new Promise(r => srv.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${srv.address().port}/view/`;

const encode = (obj) => deflateRawSync(Buffer.from(JSON.stringify(obj), 'utf8'))
  .toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
const rows = [];
for (let m = 1; m <= 6; m++) for (let d = 1; d <= 4; d++)
  rows.push([`1405/0${m}/${String(d * 6).padStart(2, '0')}`, d % 2 ? 'موتر ۴۴۲۱' : 'خودش', d === 3 ? '742' : '',
    d % 3 ? 'پطرول' : 'دیزل', String(100 + d * 20), '68', String((100 + d * 20) * 68), d === 4 ? '5,000' : '']);
const fuelBook = {
  k: 'واحد تیل', u: 'لیتر', dc: 0, fc: 3,
  s: [['جمله بردگی', '12,400'], ['جمله رسید', '8,000'], ['فیصدی ما', '340'], ['الباقی', '4,740']],
  fh: ['تیل', 'بردگی', 'رسید', 'فیصدی', 'الباقی'],
  f: [['پطرول (٪10)', '9,000', '6,000', '340', '3,340'], ['دیزل', '3,400', '2,000', '0', '1,400']],
  h: ['تاریخ', 'نام', 'حواله', 'تیل', 'مقدار تیل', 'فی', 'بردگی', 'رسید تیل'],
  r: rows,
  g: [['1404/12', '600', '200', '400'], ['1405/01', '2,000', '1,000', '1,000'], ['1405/02', '2,100', '1,500', '600'],
      ['1405/03', '2,200', '1,500', '700'], ['1405/04', '1,900', '1,300', '600'], ['1405/05', '1,800', '1,200', '600'], ['1405/06', '1,800', '1,300', '500']],
};
const moneyBook = {
  k: 'واحد پول', u: 'افغانی', dc: 0, fc: 3,
  s: [['جمله بردگی', '90,000'], ['جمله رسید', '62,000'], ['فیصدی ما', '0'], ['الباقی', '28,000']],
  fh: ['تیل', 'بردگی', 'رسید', 'فیصدی', 'الباقی'],
  f: [['پطرول', '60,000', '40,000', '0', '20,000'], ['دیزل', '30,000', '22,000', '0', '8,000']],
  h: ['تاریخ', 'نام', 'حواله', 'تیل', 'مقدار (افغانی)', 'فی', 'بردگی', 'رسید'],
  r: [['1405/06/12', 'د', '', 'پطرول', '', '', '9,000', '2,000']],
  g: [['1405/06', '9,000', '2,000', '7,000']],
};
const snap = { t: 'قرض‌دار — واحد تیل', n: 'محمد هارون', d: '1405/07/05', b: [fuelBook, moneyBook] };
const frag = 's=ac-one&a=d7&k=kk&t=1&d=' + encode(snap);

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());
async function run(scheme) {
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, colorScheme: scheme });
  const page = await ctx.newPage();
  page.on('pageerror', e => console.log('pageerror: ' + e.message));
  await page.route('https://api.vill3n.top/**', (route) => {
    const u = route.request().url(), h = { 'access-control-allow-origin': '*' };
    if (u.includes('/chat?k=')) return route.fulfill({ status: 200, contentType: 'application/json', headers: h,
      body: JSON.stringify({ ok: true, messages: [{ seq: 1, from: 'owner', name: 'پمپ', text: 'سلام، حسابتان به‌روز شد.', at: Date.now() - 60000 }], owner_seen_seq: 0 }) });
    if (u.includes('/acct/')) return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: JSON.stringify({ at: 2, d: snap }) });
    return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: '{}' });
  });
  const shot = async (n) => { await page.waitForTimeout(400); await page.screenshot({ path: path.join(OUT, `${scheme}-${n}.png`), fullPage: true }); };
  await page.goto(base + '#' + frag);
  await page.waitForTimeout(800);
  await shot('01-fuel');
  await page.click('[data-act="book"][data-v="1"]'); await shot('02-money');
  await ctx.close();
}
for (const s of WHICH === 'both' ? ['light', 'dark'] : [WHICH]) await run(s);
await browser.close(); srv.close();
console.log('عکس‌ها در ' + OUT);
