// ---------------------------------------------------------------------------
//  آزمونِ «بکاپ گرفتن کافی نیست — بازیابی هم باید واقعاً کار کند»
//
//      node tools/check-backup-restore.mjs
//
//  سناریو دقیقاً همان کاری است که کاربر می‌کند:
//    ۱) یک دفترِ بزرگ ساخته می‌شود (بزرگ‌تر از سقفِ کشِ localStorage) و ذخیره.
//    ۲) از همان لحظه بکاپ گرفته می‌شود (همان چیزی که دکمهٔ «دانلود بکاپ» می‌دهد).
//    ۳) بعد داده عوض می‌شود (مثل چند روز کارِ بعدی).
//    ۴) بکاپ بازیابی می‌شود و صفحه از نو بالا می‌آید.
//    ۵) باید دقیقاً همان دادهٔ بندِ ۱ برگشته باشد.
//
//  نیاز: playwright-core و یک کرومیوم.
//      PW_CHROMIUM=/path/to/chrome node tools/check-backup-restore.mjs
// ---------------------------------------------------------------------------
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';

const PAGE = pathToFileURL(path.join(import.meta.dirname, '..', 'index.html')).href;
const PERSONS = Number(process.env.PERSONS || 300);
const ROWS = Number(process.env.ROWS || 200);

const browser = await chromium.launch(
  process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM, args: ['--no-sandbox'] } : { args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
const errs = [];
page.on('pageerror', (e) => errs.push(String(e).slice(0, 160)));

const unlock = async () => {
  await page.waitForFunction(() => typeof window.save === 'function', null, { timeout: 40000 });
  await page.evaluate(() => {
    const l = document.getElementById('lockScreen'); if (l) l.style.display = 'none';
    const a = document.getElementById('app'); if (a) a.style.display = '';
    try { currentRole = 'admin'; } catch (e) {}
    window.confirm = () => true;
  });
};

let failed = 0;
const fail = (t, d) => { failed++; console.log(`  ❌ ${t}`); if (d) console.log(`     ${d}`); };
const ok = (t) => console.log(`  ✅ ${t}`);

console.log('\nآزمونِ بکاپ و بازیابی\n');

await page.goto(PAGE, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2500);
await unlock();

// ── ۱) دفترِ بزرگ ──
const made = await page.evaluate(({ persons, rows }) => {
  const today = persianDate();
  DB.debtPersons = [];
  for (let i = 0; i < persons; i++) {
    const r = [];
    for (let k = 0; k < rows; k++) r.push({ date: today, name: 'ردیف ' + k, hawala: 'H' + k,
      ftype: (k % 2) ? 'diesel' : 'petrol', fuel: 100 + k, priceper: 60, bardagi: 0, rasid: 0, rasidFuel: 0 });
    DB.debtPersons.push({ id: 'bk' + i, name: 'حسابِ بکاپ ' + i, phone: '', rows: r, subs: [], mode: 'fuel' });
  }
  save();
  return { persons: DB.debtPersons.length, bytes: JSON.stringify(DB).length };
}, { persons: PERSONS, rows: ROWS });
await page.waitForTimeout(2500);   // مهلتِ نوشتنِ debounce‌دار
console.log(`  دفترِ آزمون: ${made.persons} حساب · ${Math.round(made.bytes / 1048576 * 10) / 10} مگابایت`);
if (made.bytes > 4 * 1024 * 1024) ok('از سقفِ کشِ localStorage بزرگ‌تر است (همان حالتی که در واقعیت مهم است)');
else console.log('  ⚠️ کوچک‌تر از سقفِ کش — برای آزمونِ سخت‌تر PERSONS را بالا ببرید');

// ── ۲) بکاپ ──
const backup = await page.evaluate(() => JSON.stringify(DB));

// ── ۳) کارِ روزهای بعد: داده عوض می‌شود ──
await page.evaluate(() => {
  DB.debtPersons = [{ id: 'after', name: 'حسابِ بعد از بکاپ', phone: '', rows: [], subs: [], mode: 'fuel' }];
  DB._rev = (parseInt(DB._rev, 10) || 0) + 50;   // مثل چند روز کارِ واقعی
  save();
});
await page.waitForTimeout(2500);

// ── ۴) بازیابی ──
await page.evaluate((txt) => { _bkRestoreData(txt); }, backup);
await page.waitForTimeout(1500);
await page.waitForLoadState('load').catch(() => {});
await page.waitForTimeout(3000);
await unlock();
await page.waitForTimeout(1500);   // مهلتِ آشتیِ localStorage/IndexedDB در load()

// ── ۵) داوری ──
const after = await page.evaluate(() => ({
  persons: (DB.debtPersons || []).length,
  first: (DB.debtPersons && DB.debtPersons[0] && DB.debtPersons[0].name) || '',
  rows: (DB.debtPersons && DB.debtPersons[0] && (DB.debtPersons[0].rows || []).length) || 0,
}));

if (after.persons === made.persons) ok(`همهٔ ${made.persons} حساب برگشت`);
else fail('حساب‌ها برنگشتند', `انتظار ${made.persons} حساب، ولی ${after.persons} حساب هست (نامِ اولی: «${after.first}»)`);

if (after.rows === ROWS) ok(`ردیف‌های هر حساب هم برگشتند (${ROWS} ردیف)`);
else fail('ردیف‌ها برنگشتند', `انتظار ${ROWS} ردیف، ولی ${after.rows} ردیف`);

if (errs.length) fail('خطای جاوااسکریپت هنگام بازیابی', errs[0]);
else ok('بدونِ خطای جاوااسکریپت');

await browser.close();
console.log('');
if (failed) { console.log(`${failed} قاعده شکست — بازیابیِ بکاپ قابل اتکا نیست.\n`); process.exit(1); }
console.log('بکاپ گرفته شد و کاملاً بازیابی شد.\n');
