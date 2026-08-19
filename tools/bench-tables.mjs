// ---------------------------------------------------------------------------
//  بنچمارکِ جدول‌های برنامه — «با ده‌ها هزار ردیف هم لگ نزند»
//
//      N=50000 node tools/bench-tables.mjs
//
//  هیچ چیزی را عوض نمی‌کند: برنامه را در یک کرومیومِ واقعی باز می‌کند، هر جدول را
//  با N ردیف پر می‌کند و سه عدد را از خودِ کروم می‌گیرد (نه از حسِ چشم):
//
//      style  — بازچینیِ سبک‌ها (RecalcStyleDuration)
//      layout — چیدمان (LayoutDuration)
//      task   — کلِ کاری که نخِ اصلی کرد (TaskDuration) ← «لگ» یعنی همین
//
//  هر گامِ اسکرول باید خیلی کمتر از ۱۶ms باشد تا روی صفحهٔ ۶۰ فریمی هیچ فریمی
//  از دست نرود. عددهای «بی‌کار» کفِ همان مرورگر است؛ هر چیزی نزدیکِ آن یعنی
//  جدول عملاً هیچ هزینه‌ای ندارد.
//
//  نیاز به playwright-core و یک کرومیوم دارد (روی این مخزن نصب نیست):
//      PW_CHROMIUM=/path/to/chrome N=50000 node tools/bench-tables.mjs
// ---------------------------------------------------------------------------
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';

const PAGE = pathToFileURL(path.join(import.meta.dirname, '..', 'index.html')).href;
const N = Number(process.env.N || 20000);

const browser = await chromium.launch(
  process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM, args: ['--no-sandbox'] } : { args: ['--no-sandbox'] }
);
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errs = [];
page.on('pageerror', (e) => errs.push(String(e).slice(0, 200)));

await page.goto(PAGE, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2500);
// قفل را مثل تستِ دستیِ توضیح‌دادهٔ CLAUDE.md باز می‌کنیم
await page.evaluate(() => {
  const l = document.getElementById('lockScreen'); if (l) l.style.display = 'none';
  const a = document.getElementById('app'); if (a) a.style.display = '';
  try { currentRole = 'admin'; } catch (e) {}
});

// دادهٔ آزمایشی — همه در یک ماه، تا جدولِ هر بخش واقعاً N ردیف داشته باشد
await page.evaluate((n) => {
  const today = persianDate();
  const rows = [];
  for (let i = 0; i < n; i++) rows.push({ date: today, name: 'r' + i, hawala: 'h' + i,
    ftype: (i % 3 === 0) ? 'diesel' : 'petrol', fuel: (i % 20) + 1, priceper: 60,
    bardagi: 0, rasid: (i % 5) * 10, rasidFuel: 0 });
  DB.debtPersons = [{ id: 'benchP', name: 'حسابِ بزرگ', phone: '', rows, subs: [], mode: 'fuel' }];
  const crows = [];
  for (let i = 0; i < n; i++) crows.push({ date: today, name: 'c' + i, kg: 1000 + i, usd: 900, rate: 70, poul: 100, poulCurrency: 'afn' });
  DB.tilCompanies = [{ id: 'benchC', name: 'شرکتِ بزرگ', rows: crows, dieselRows: [] }];
  DB.sarrafiRows = []; for (let i = 0; i < n; i++) DB.sarrafiRows.push({ date: today, desc: 'ت' + i, amount: 1000 + i, currency: 'toman', rate: 70, bardagi: 0 });
  DB.chakanaRows = []; for (let i = 0; i < n; i++) DB.chakanaRows.push({ date: today, name: 'چ' + i, fuel: (i % 20) + 1, priceper: 55, rasid: 0 });
  DB.expenses = []; for (let i = 0; i < n; i++) DB.expenses.push({ date: today, title: 'م' + i, amount: 100 + i, note: '' });
}, N);

const cdp = await page.context().newCDPSession(page);
await cdp.send('Performance.enable');
const snap = async () => {
  const m = (await cdp.send('Performance.getMetrics')).metrics;
  const g = (x) => (m.find((y) => y.name === x) || {}).value || 0;
  return { style: g('RecalcStyleDuration'), layout: g('LayoutDuration'), task: g('TaskDuration') };
};
const diff = (a, b, d) => Object.keys(a).map((k) => k + ' ' + Math.round((b[k] - a[k]) * 1000 / d) + 'ms').join(' · ');

async function measure(label, open, scrollBox) {
  let a = await snap();
  await page.evaluate(open);
  await page.waitForTimeout(1200);
  let b = await snap();
  console.log(('  ' + label + ' — باز کردن').padEnd(38), diff(a, b, 1));
  a = await snap();
  await page.evaluate(async (sel) => {
    const sc = sel ? document.querySelector(sel) : document.scrollingElement;
    sc.scrollTop = 0;
    for (let i = 0; i < 50; i++) { sc.scrollTop += 250; await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))); }
  }, scrollBox);
  b = await snap();
  console.log(('  ' + label + ' — هر گامِ اسکرول').padEnd(38), diff(a, b, 50));
}

console.log('\n── جدول‌ها با ' + N.toLocaleString('fa-IR') + ' ردیف ──');
await measure('حسابِ قرض‌دار', () => openPersonModal('debt', 'benchP'), '#personModal .modal-body');
await page.evaluate(() => { const m = document.getElementById('personModal'); if (m) m.classList.remove('open'); });
await measure('حسابِ شرکت', () => openCompanyModal('benchC'), '#companyModal .modal-body');
await page.evaluate(() => { const m = document.getElementById('companyModal'); if (m) m.classList.remove('open'); });
await measure('صرافی', () => showSection('sarrafi'), null);
await measure('چکنه', () => showSection('chakana'), null);
await measure('مصارف', () => showSection('expenses'), null);

let a = await snap();
await page.evaluate(async () => { for (let i = 0; i < 50; i++) await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))); });
let b = await snap();
console.log(('  کفِ مرورگر — هر فریمِ بی‌کار').padEnd(38), diff(a, b, 50));

console.log('\n  خطای جاوااسکریپت:', errs.length ? errs.slice(0, 5) : 'ندارد', '\n');
await browser.close();
