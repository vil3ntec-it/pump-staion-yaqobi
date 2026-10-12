/* ══ «کی تیل دارد، کی قرضدار، کی صاف» — اپِ گوشی (۱۴۰۵/۰۷/۲۰) ═══════════════
 *
 *     node tools/check-kar-clear.mjs [پوشهٔ عکس]
 *
 * صاحب ریپو: «دقیق آدم متوجه نمی‌شود کی تیل دارد و کی نه… حسابِ فرعی را در یک
 * حساب نشان می‌دهد… واحدِ تیل است یا پول… مخزن‌ها را دقیق نشان نمی‌دهد.»
 * ۱) منطقِ خالص در Node: علامتِ الباقی ⇒ حال، واحدِ هر حساب، هر حساب جدا، مخزن.
 * ۲) همان در کرومیومِ واقعی (۳۹۰×۸۴۴): کارتِ جدا برای هر حسابِ فرعی، نوارِ درشتِ
 *    حال، واحد کنارِ هر عدد، درصدِ مخزن از ظرفیت و هر مخزنِ شماره‌دار.
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import http from 'node:http'; import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
const require = createRequire(import.meta.url);
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const OUT = process.argv[2] || '';
let bad = 0;
const ok = (c, what) => { console.log((c ? '  ✓ ' : '  ✗ ') + what); if (!c) bad++; };

console.log('۱) منطقِ خالص');
const app = require(path.join(ROOT, 'kar/app.js'));
ok(app.standOf('320') === 'owe' && app.standOf('-1,500') === 'has' && app.standOf('0') === 'zero' && app.standOf('0.001') === 'zero',
   'الباقیِ مثبت ⇒ قرضدار · منفی ⇒ دارد · صفر ⇒ صاف');
{
  const fuel = { unit: 'fuel', st: 'ok', fuels: [['پطرول (٪2)', '1,200', '2,000', '0', '-800'], ['دیزل', '0', '0', '0', '0']] };
  const s = app.acctState(fuel);
  ok(s.stand === 'has' && s.word === 'تیل دارد' && s.bookWord === 'حساب به لیتر' && s.unitWord === 'لیتر', 'حسابِ لیتری با الباقیِ منفی ⇒ «🛢️ تیل دارد» · «حساب به لیتر»');
  ok(s.lines.length === 1 && s.lines[0].amt === 800 && app.lineText(s.lines[0]) === '⛽ پطرول: 800 لیتر تیل دارد', 'فقط تیلی که عدد دارد، با واحدش: ' + app.lineText(s.lines[0]));
  const money = { unit: 'money', st: 'ok', fuels: [['پطرول', '84,000', '120,000', '0', '-36,000']] };
  ok(app.acctState(money).word === 'پول دارد' && app.acctState(money).bookWord === 'حساب به پول'
     && app.lineText(app.acctState(money).lines[0]).includes('36,000 افغانی'), 'حسابِ پولی ⇒ «💵 پول دارد» به افغانی');
  const mixed = { unit: 'fuel', st: 'out', fuels: [['پطرول', '500', '200', '0', '300'], ['دیزل', '0', '100', '0', '-100']] };
  ok(app.acctState(mixed).stand === 'owe' && app.acctState(mixed).warn.includes('اضافه ندهید'), 'یک تیل قرض ⇒ کلِ حساب «🔴 قرضدار» و «اضافه ندهید»');
  ok(app.acctState({ unit: 'fuel', fuels: [['پطرول', '600', '600', '0', '0']] }).stand === 'zero', 'برد = رسید ⇒ «⚪ صاف»');
  const two = { unit: 'fuel', books: [
    { money: false, on: true, f: [['پطرول', '100', '300', '0', '-200']] },
    { money: true, on: false, f: [['پطرول', '9,000', '0', '0', '9,000']] }] };
  const t = app.acctState(two);
  ok(t.stand === 'has' && t.other.length === 1 && t.other[0].unit === 'money' && t.other[0].stand === 'owe',
     'دفترِ فعال حال را می‌سازد؛ دفترِ دیگر فقط یادآوری است، قاطی نمی‌شود');
}
{
  const p = { id: 7, name: 'حاجی', status: 'out', accounts: [
    { title: '', unit: 'fuel', st: 'out', fuels: [['پطرول', '5,320', '5,000', '0', '320']] },
    { title: 'موتر', unit: 'fuel', st: 'ok', fuels: [['پطرول', '1,200', '2,000', '0', '-800']] },
    { title: 'دکان', unit: 'money', st: 'ok', fuels: [['پطرول', '84,000', '120,000', '0', '-36,000']] }] };
  const e = app.acctEntries(p);
  ok(e.length === 3 && e.map(x => x.s.stand).join() === 'owe,has,has' && e.map(x => x.s.unit).join() === 'fuel,fuel,money',
     'شخصی با سه حساب ⇒ سه کارتِ جدا، هر کدام حال و واحدِ خودش');
  ok(app.entryRank(e[0]) < app.entryRank(e[1]), 'قرضدارِ تمام‌شده پیش از «دارد»');
  ok(app.acctEntries({ id: 1, name: 'کهنه', status: 'ok', bal: { petrol: -50 } }).length === 1, 'برنامهٔ کهنه بی حساب ⇒ یک کارت از خودِ شخص');
  const kv = app.answer('حاجی', { debtors: [p], sections: {}, tank: {} })[0].kv.map(x => x.join(' ')).join(' | ');
  ok(kv.includes('موتر (حساب به لیتر)') && kv.includes('دکان (حساب به پول)') && kv.includes('36,000 افغانی'), 'ربات هم هر حساب را جدا با واحدش می‌گوید');
}
{
  const v = app.tankView({ show: 11500, cap: 20000, pct: 57.5, threshold: 2000, low: false,
    tanks: [{ n: 1, cap: 10000, cur: 1500, pct: 15, act: true, low: true }, { n: 2, cap: 10000, cur: 0, pct: 0, act: false }] });
  ok(v.pct === 57.5 && v.thrPct === 10 && v.level === 'ok', 'درصد همان ‎pct‎ِ برنامه (از ظرفیت)، خطِ هشدار در ٪۱۰');
  ok(v.tanks[0].state.includes('کم مانده') && v.tanks[1].state.includes('خالی'), 'هر مخزن حالِ خودش: «از این می‌کشد — کم مانده» / «خالی»');
  ok(app.tankView({ show: 900, in: 24000 }).pct === -1, 'برنامهٔ کهنه بی ظرفیت ⇒ درصدی ساخته نمی‌شود (نه از «کلِ واردشده»)');
}

console.log('۲) در مرورگر');
const globalRoot = execSync('npm root -g').toString().trim();
const { chromium } = createRequire(import.meta.url)(globalRoot + '/playwright');
if (OUT) fs.mkdirSync(OUT, { recursive: true });
const srv = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p.endsWith('/')) p += 'index.html';
  const f = path.join(ROOT, p); if (!fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  const ext = path.extname(f);
  res.setHeader('content-type', ext === '.js' ? 'text/javascript' : ext === '.html' ? 'text/html; charset=utf-8' : 'application/octet-stream');
  res.end(fs.readFileSync(f));
});
await new Promise(r => srv.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${srv.address().port}/kar/`;
const salt = crypto.randomBytes(16), iter = 1000;
const hash = crypto.pbkdf2Sync('1234', salt, iter, 32, 'sha256');
const gate = `pbkdf2$sha256$${iter}$${salt.toString('base64')}$${hash.toString('base64')}`;
const F = (p, d) => [['پطرول (٪2)', ...p], ['دیزل (٪1)', ...d]];
const book = (money, on, f) => ({ money, on, s: [], f, h: [], r: [] });
const debtors = [
  { id: 1, name: 'حاجی قدیر', status: 'out', stP: 'out', bal: { money: 0, petrol: 320, diesel: 0 }, accounts: [
    { id: 11, title: '', unit: 'fuel', st: 'out', fuels: F(['5,320', '5,000', '0', '320'], ['0', '0', '0', '0']), books: [book(false, true, F(['5,320', '5,000', '0', '320'], ['0', '0', '0', '0']))] },
    { id: 12, title: 'موترِ ۴۴۲۱', unit: 'fuel', st: 'ok', fuels: F(['1,200', '2,000', '0', '-800'], ['0', '0', '0', '0']), books: [book(false, true, F(['1,200', '2,000', '0', '-800'], ['0', '0', '0', '0']))] },
    { id: 13, title: 'دکان', unit: 'money', st: 'ok', fuels: F(['84,000', '120,000', '0', '-36,000'], ['0', '0', '0', '0']), books: [book(true, true, F(['84,000', '120,000', '0', '-36,000'], ['0', '0', '0', '0']))] },
  ] },
  { id: 2, name: 'محمد کریم', status: 'ok', bal: {}, accounts: [
    { id: 21, title: '', unit: 'money', st: 'ok', fuels: F(['12,400', '8,000', '0', '4,400'], ['3,000', '3,000', '0', '0']), books: [book(true, true, F(['12,400', '8,000', '0', '4,400'], ['3,000', '3,000', '0', '0']))] } ] },
  { id: 3, name: 'شرکتِ ترانسپورتی امید', status: 'low', bal: {}, accounts: [
    { id: 31, title: '', unit: 'fuel', st: 'low', fuels: F(['8,500', '10,000', '0', '-1,500'], ['2,000', '3,000', '0', '-1,000']), books: [book(false, true, F(['8,500', '10,000', '0', '-1,500'], ['2,000', '3,000', '0', '-1,000']))] } ] },
  { id: 4, name: 'زلمی', status: 'ok', bal: {}, accounts: [
    { id: 41, title: '', unit: 'fuel', st: 'ok', fuels: F(['600', '600', '0', '0'], ['0', '0', '0', '0']), books: [book(false, true, F(['600', '600', '0', '0'], ['0', '0', '0', '0']))] } ] },
];
const snap = { v: 1, seq: 9, at: '1405/07/20', gate, station: { name: 'پمپِ بنزینِ یعقوبی' },
  banner: [['قرض کل', '485,300', 'danger']],
  tank: {
    petrol: { in: 42000, out: 30500, current: 11500, show: 11500, low: false, near: false, threshold: 2000, cap: 20000, pct: 57.5,
      tanks: [{ n: 1, cap: 10000, cur: 1500, pct: 15, act: true, low: true }, { n: 2, cap: 10000, cur: 10000, pct: 100, act: false, low: false }] },
    diesel: { in: 24000, out: 23100, current: 900, show: 900, low: true, near: false, threshold: 2000, cap: 10000, pct: 9 } },
  debtors, alerts: [], sections: { storage: { t: 'خریدِ تیل', head: ['تاریخ', 'تیل', 'لیتر'], rows: [['1405/06/20', 'پطرول', '20,000']], m: ['1405/06'], sum: [] } } };
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 } });
const page = await ctx.newPage();
const errors = []; page.on('pageerror', e => errors.push(e.message));
await page.route('https://accounts.google.com/**', r => r.fulfill({ status: 200, body: '' }));
await page.route(/\/kar\/version\.json/, r => r.fulfill({ status: 200, contentType: 'application/json', body: '{"v":"10"}' }));
await page.route('https://api.vill3n.top/**', async (route) => {
  const url = route.request().url(), h = { 'access-control-allow-origin': '*' };
  if (url.endsWith('/api/pump/public/join')) return route.fulfill({ status: 200, contentType: 'application/json', headers: h,
      body: JSON.stringify({ ok: true, station: { code: 'ac-one', name: snap.station.name, accessCode: '48291736' }, home: { url: '', readKey: '', station: 'ac-one' }, cloudLiveAt: 5 }) });
  if (url.includes('/api/pump/public/live')) return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: JSON.stringify({ ok: true, updatedAt: Date.now(), live: snap }) });
  return route.fulfill({ status: 200, contentType: 'application/json', headers: h, body: '{}' });
});
await page.goto(base);
await page.waitForSelector('#codePane:not(.hidden)');
await page.fill('#inCode', '48291736'); await page.click('#btnJoin');
await page.waitForSelector('#paneHome:not(.hidden)');
await page.click('.door.staff');
await page.waitForFunction(() => document.querySelectorAll('#staffList .st-row').length === 6, null, { timeout: 8000 });
const cards = await page.$$eval('#staffList .st-row', els => els.map(e => ({ cls: e.className, pid: e.dataset.pid, acc: e.dataset.acc, t: e.textContent })));
ok(cards.filter(c => c.pid === '1').length === 3, 'حاجی قدیر با سه حساب ⇒ سه کارتِ جدا در فهرست');
ok(cards[0].cls.includes('s-owe') && cards[0].t.includes('قرضدار') && cards[0].t.includes('320') && cards[0].t.includes('لیتر'), 'اول قرضدارِ تمام‌شده: «🔴 قرضدار · 320 لیتر»');
const dokan = cards.find(c => c.pid === '1' && c.acc === '2');
ok(dokan && dokan.t.includes('دکان') && dokan.t.includes('حساب به پول') && dokan.t.includes('36,000') && dokan.t.includes('افغانی') && dokan.t.includes('پول دارد'),
   'حسابِ فرعیِ «دکان»: «💵 حساب به پول» · «36,000 افغانی پول دارد»');
ok(!cards.some(c => c.pid === '1' && c.t.includes('35,')), 'هیچ جمعی میانِ حساب‌های یک شخص نیست');
ok(cards.find(c => c.t.includes('زلمی')).cls.includes('s-zero') && cards.find(c => c.t.includes('زلمی')).t.includes('صاف'), 'برد = رسید ⇒ «⚪ صاف»');
const big = await page.$eval('#staffList .st-row .stand', e => parseFloat(getComputedStyle(e).fontSize));
ok(big >= 18, 'نوارِ حال درشت است (' + big + 'px)');
const wide = await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth);
ok(wide, 'بی اسکرولِ افقی روی گوشیِ ۳۹۰');
await page.click('#staffCounts button[data-f="has"]');
ok((await page.$$('#staffList .st-row')).length === 3, 'فیلترِ «🛢️ دارد» = سه حساب');
await page.click('#staffCounts button[data-f=""]');
await page.click('#staffList .st-row[data-pid="1"][data-acc="2"]');
const first = await page.$eval('#botOut .acard', e => e.getAttribute('data-acct'));
ok(first === '1|2', 'زدنِ کارتِ «دکان» ⇒ همان حساب اول دیده می‌شود');
ok((await page.textContent('#botOut')).includes('3 حسابِ جدا') && (await page.$$('#botOut .acard')).length === 3, 'بقیهٔ حساب‌ها زیرش، هر کدام کارتِ جدا');
ok(!(await page.$('#botOut [data-print-acct]')), '⛔ درِ کارمندان چاپ و جدول ندارد');
if (OUT) await page.screenshot({ path: path.join(OUT, 'clear-person.png'), fullPage: true });
await page.click('#btnBackStaff');
await page.click('#nav button[data-pane="paneTank"]');
const tank = await page.textContent('#tankBox');
ok(tank.includes('57.5') && tank.includes('20,000') && tank.includes('لیتر در مخزن'), 'پطرول: ٪57.5 از ظرفیتِ 20,000 لیتر');
ok(tank.includes('مخزنِ 1') && tank.includes('مخزنِ 2') && tank.includes('1,500') && tank.includes('کم مانده'), 'هر مخزنِ شماره‌دار جدا، با «کم مانده»');
ok(tank.includes('کم آمده') && tank.includes('900'), 'دیزل کم آمده، 900 لیتر');
const gw = await page.$eval('#tankBox .tank .gauge i', e => e.style.width);
ok(gw === '57.5%', 'نوار همان درصد است: ' + gw);
if (OUT) await page.screenshot({ path: path.join(OUT, 'clear-tank.png'), fullPage: true });
ok(errors.length === 0, 'بی خطای جاوااسکریپت' + (errors.length ? ': ' + errors.join(' | ') : ''));
await browser.close(); srv.close();
console.log(bad ? `\n❌ ${bad} ایراد` : '\n✅ هر حساب جدا، حالش درشت، هر عدد با واحدش، مخزن از ظرفیت');
process.exit(bad ? 1 : 0);
