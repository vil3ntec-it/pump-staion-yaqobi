/* ══ 🖨 PDF و چاپِ اپِ گوشی — همان ورقِ برنامهٔ کامپیوتر ═════════════════════
 *
 *     node tools/check-kar-print.mjs [پوشهٔ عکس و PDF]
 *
 * دکمهٔ چاپ روی حسابِ قرض‌دار و روی یک بخش واقعاً زده می‌شود و ورقِ آماده
 * (همان ‎#printRoot‎ی لحظهٔ ‎window.print‎) با ‎DebtorStatementReport‎/‎DocStyle‎ی
 * برنامهٔ کامپیوتر سنجیده می‌شود.
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
const OUT = process.argv[2] || '';
if (OUT) fs.mkdirSync(OUT, { recursive: true });

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
    head: ['تاریخ', 'نام', 'حواله', 'تیل', i % 2 ? 'مقدار (افغانی)' : 'مقدار تیل', 'فی', 'بردگی', i % 2 ? 'رسید' : 'رسید تیل'],
    rows: Array.from({ length: 6 }, (_, r) => [`1405/07/0${r + 1}`, n, r % 3 ? '' : String(700 + r), r % 2 ? 'دیزل' : 'پطرول', String(100 + r * 20), '68', String((100 + r * 20) * 68), r % 2 ? '5,000' : '']),
    sum: [['جمله بردگی', '12,400'], ['جمله رسید', '8,000'], ['الباقی', '4,400']],
    st: st[i] === 'over' ? 'out' : st[i],
    fuels: [['پطرول (٪2)', '8,400', '5,000', '168', '3,568'], ['دیزل (٪1.5)', '4,000', '3,000', '60', '1,060']],
    //  ⛔ هر دو دفتر (برنامهٔ ۳.۱.۲۲۱ به بعد) — نفرِ دوم دفترِ تیل هم دارد
    books: i === 1 ? [
      { money: true, on: true, s: [['جمله بردگی', '12,400'], ['جمله رسید', '8,000'], ['فیصدی ما', '228'], ['الباقی', '4,400']],
        f: [['پطرول (٪2)', '8,400', '5,000', '168', '3,568'], ['دیزل (٪1.5)', '4,000', '3,000', '60', '1,060']],
        h: ['تاریخ', 'نام', 'حواله', 'تیل', 'مقدار (افغانی)', 'فی', 'بردگی', 'رسید'],
        r: Array.from({ length: 6 }, (_, r) => [`1405/07/0${r + 1}`, n, '', r % 2 ? 'دیزل' : 'پطرول', String(100 + r * 20), '68', String((100 + r * 20) * 68), r % 2 ? '5,000' : '']) },
      { money: false, on: false, s: [['جمله بردگی', '10,220'], ['جمله رسید', '0'], ['فیصدی ما', '0'], ['الباقی', '10,220']],
        f: [['پطرول (٪2)', '10,220', '0', '0', '10,220'], ['دیزل (٪1.5)', '0', '0', '0', '0']],
        h: ['تاریخ', 'نام', 'حواله', 'تیل', 'مقدار تیل', 'فی', 'بردگی', 'رسید تیل'],
        r: [['1405/07/03', n, '555', 'پطرول', '10,220', '', '10,220', '']] },
    ] : undefined,
  }].concat(i === 1 ? [{
    title: 'دکان', unit: 'fuel', st: 'ok', head: [], rows: [], sum: [],
    fuels: [['پطرول', '600', '600', '0', '0'], ['دیزل', '0', '0', '0', '0']],
  }] : []),
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

let bad = 0;
const ok = (c, what) => { console.log((c ? '  ✓ ' : '  ✗ ') + what); if (!c) bad++; };
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 } });
const page = await ctx.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
await page.route('https://accounts.google.com/**', r => r.fulfill({ status: 200, body: '' }));
await page.route(/\/kar\/version\.json/, r => r.fulfill({ status: 200, contentType: 'application/json', body: '{"v":"10"}' }));
await page.route('https://api.vill3n.top/**', async (route) => {
  const url = route.request().url(), h = { 'access-control-allow-origin': '*' };
  if (url.endsWith('/api/pump/public/join'))
    return route.fulfill({ status: 200, contentType: 'application/json', headers: h,
      body: JSON.stringify({ ok: true, station: { code: 'ac-one', name: snap.station.name, accessCode: '48291736' }, home: { url: '', readKey: '', station: 'ac-one' }, cloudLiveAt: 5 }) });
  if (url.includes('/api/pump/public/live'))
    return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: JSON.stringify({ ok: true, updatedAt: Date.now(), live: snap }) });
  return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: '{}' });
});
//  ⛔ چاپِ واقعی گرفته نمی‌شود: ‎window.print‎ همان ورقِ آماده را برمی‌دارد
await page.addInitScript(() => { window.__printed = []; window.print = () => { const r = document.getElementById('printRoot'); window.__printed.push(r ? r.innerHTML : ''); }; });

await page.goto(base);
await page.waitForSelector('#codePane:not(.hidden)');
await page.fill('#inCode', '48291736'); await page.click('#btnJoin');
await page.waitForSelector('#paneHome:not(.hidden)');
await page.click('.door.staff');
await page.click('#modeSeg button[data-mode="owner"]');
await page.waitForFunction(() => !document.getElementById('btnUnlock').disabled, null, { timeout: 8000 });
await page.fill('#inPass', '1234'); await page.click('#btnUnlock');
await page.waitForSelector('#paneDash:not(.hidden)');
await page.click('#nav button[data-pane="paneDebt"]');
await page.click('#debtList button[data-pid="2"]');

console.log('۱) PDF / چاپِ حسابِ قرض‌دار');
const btn = await page.$('[data-print-acct]');
ok(!!btn, 'دکمهٔ «🖨 PDF / چاپ» روی کارتِ حساب هست');
await btn.click();
const doc = (await page.evaluate(() => window.__printed))[0] || '';
ok(doc.includes('⛽ پمپِ بنزینِ یعقوبی — محمد کریم'), 'عنوانِ ورق همان «⛽ نامِ پمپ — نامِ قرض‌دار»ِ برنامه است');
ok(doc.includes('حساب قرض‌دار — واحد پول'), 'زیرنویس همان «حساب قرض‌دار — واحد …»');
ok(doc.includes('⛽ حساب پطرول') && doc.includes('🟤 حساب دیزل'), 'دو کادرِ «حساب پطرول» و «حساب دیزل»');
ok(['فیصدی ما', 'رسید قبلی', 'برد', 'الباقی'].every(w => doc.includes(w)), 'چهار خانهٔ هر کادر به همان ترتیب و نام');
ok(doc.includes('8,400') && doc.includes('3,568'), 'عددها همان رشته‌های عکسِ برنامه‌اند');
ok(/<th>#<\/th><th>تاریخ<\/th>/.test(doc), 'جدول با ستونِ «#» و «تاریخ» شروع می‌شود');
ok(['نوع تیل', 'فی لیتر', 'مقدار بردگی'].every(w => doc.includes('<th>' + w + '</th>')), 'نامِ سرستون‌ها همان ورقِ برنامه');
ok(doc.includes('⛽ پطرول') && doc.includes('🟤 دیزل'), 'ستونِ تیل با ⛽/🟤 مثلِ برنامه');
ok(/@bottom-right\{content:"\d{4}\/\d\d\/\d\d · /.test(doc), 'پاورقیِ تاریخ‌ها: شمسی · قمری · میلادی');
ok(doc.includes('#2d3748') && doc.includes('#b45309') && doc.includes('#fafbfc'), 'رنگ‌های ‎DocStyle‎ی برنامه');
ok(doc.includes('counter(page, persian)') && doc.includes('counter(pages, persian)'), 'پاورقیِ «ورق N از M»');
ok(/<td class="tf" colspan="\d+">جمله<\/td>/.test(doc) && doc.includes('12,400'), 'ردیفِ «جمله» با جمعِ بردگیِ خودِ برنامه');
ok(!/body\{margin/.test(doc) || doc.includes('#printRoot{'), 'سبکِ ورق فقط روی ورقِ چاپ می‌نشیند، نه روی کلِ اپ');

console.log('۱ب) دو دفترِ هر حساب — «واحد تیل» و «واحد پول»');
{
  const card = '#botOut .acard[data-acct="2|0"]';
  ok(await page.$$eval(card + ' .btab', b => b.length) === 2, 'دو دکمهٔ «واحد تیل / واحد پول» روی کارت');
  ok((await page.textContent(card + ' .btab.on')).includes('واحد پول'), 'اول همان دفترِ فعالِ کامپیوتر (پول) دیده می‌شود');
  ok(!(await page.$('#botOut .three')), '⛔ سه عددِ پول/پطرول/دیزل بالای کارت‌ها تکرار نشده');
  await page.click(card + ' .btab.fuel');
  ok((await page.textContent(card + ' .btab.on')).includes('واحد تیل'), 'زدنِ «واحد تیل» ⇒ همان دفتر');
  const txt = await page.textContent(card);
  ok(txt.includes('10,220') && !txt.includes('3,568'), 'سربرگ و عددها فقط از دفترِ تیل — نه قاطیِ دفترِ پول');
  await page.click(card + ' [data-print-acct]');
  const fdoc = (await page.evaluate(() => window.__printed)).slice(-1)[0] || '';
  ok(fdoc.includes('حساب قرض‌دار — واحد تیل') && fdoc.includes('10,220') && fdoc.includes('<th>مقدار تیل</th>'), 'PDF همان دفترِ روی صفحه (تیل)');
  await page.click(card + ' .btab.money');
  ok((await page.textContent(card)).includes('3,568'), 'برگشت به «واحد پول»');
  //  نفرِ اول برنامهٔ کهنه دارد (‎books‎ ندارد) ⇒ دکمهٔ دیگر می‌گوید چرا خالی است
}
await page.evaluate(() => { window.__printed = []; });
await page.click('#btnBackDebt');
await page.click('#debtList button[data-pid="1"]');
await page.click('#botOut .acard[data-acct="1|0"] .btab.money');
ok((await page.textContent('#botOut .acard[data-acct="1|0"]')).includes('به‌روز کنید'), 'برنامهٔ کامپیوترِ کهنه ⇒ دفترِ دیگر می‌گوید چرا نیامده');
await page.click('#btnBackDebt');
await page.click('#debtList button[data-pid="2"]');

console.log('۲) PDF / چاپِ یک بخش');
await page.click('#nav button[data-pane="paneSec"]');
await page.click('#secTabs button[data-sec="expense"]');
await page.click('[data-print-sec]');
await page.evaluate(() => { window.__printed = ['']; });
await page.click('[data-print-sec]');
const sdoc = (await page.evaluate(() => window.__printed))[1] || '';
ok(sdoc.includes('— مصارف') && sdoc.includes('برق') && sdoc.includes('جمله مصارف'), 'ورقِ «مصارف» با ردیف‌ها و جمعش');
await page.selectOption('#selMonth', '1405/06');
await page.click('[data-print-sec]');
const mdoc = (await page.evaluate(() => window.__printed))[2] || '';
ok(mdoc.includes('تعمیرِ پمپ') && !mdoc.includes('نان کارمندان') && mdoc.includes('ماهِ 1405/06'), 'صافیِ ماهِ صفحه روی ورق هم هست');

console.log('۳) درِ کارمندان');
await page.click('#modeSeg button[data-mode="staff"]');
await page.click('#staffList .st-row');
ok(!(await page.$('#paneBot [data-print-acct]')), '⛔ کارمند دکمهٔ چاپِ حساب را نمی‌بیند (فیصدی و ردیف‌ها مالِ حساب‌هاست)');
ok(errors.length === 0, 'بی خطای جاوااسکریپت' + (errors.length ? ': ' + errors.join(' | ') : ''));

if (OUT) {
  const pg = await ctx.newPage();
  const html = (await page.evaluate(() => window.__printed))[0];
  const st = /<style>@media print\{([\s\S]*)\}<\/style>/.exec(html);
  await pg.setContent('<!doctype html><html dir="rtl"><head><meta charset="utf-8"><style>' + (st ? st[1].replace('#printRoot{', 'body{') : '') + '</style></head><body>' + html.replace(/<style>[\s\S]*?<\/style>/, '') + '</body></html>');
  await pg.pdf({ path: path.join(OUT, 'kar-debtor.pdf'), format: 'A4', printBackground: true });
  await pg.setViewportSize({ width: 794, height: 1123 });
  await pg.screenshot({ path: path.join(OUT, 'kar-debtor.png'), fullPage: true });
  console.log('ورق در ' + OUT);
}
await browser.close(); srv.close();
console.log(bad ? `\n❌ ${bad} ایراد` : '\n✅ PDF و چاپِ اپِ گوشی همان ورقِ برنامهٔ کامپیوتر است');
process.exit(bad ? 1 : 0);
