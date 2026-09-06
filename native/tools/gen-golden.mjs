// ---------------------------------------------------------------------------
//  ══ سازندهٔ «دادهٔ طلایی» ══════════════════════════════════════════════════
//
//      PW_CHROMIUM=/opt/pw-browsers/chromium/chrome-linux/chrome \
//      node native/tools/gen-golden.mjs
//
//  خودِ index.html را در یک کرومیومِ واقعی باز می‌کند و توابعِ اصلیِ همان
//  برنامه را با هزاران ورودیِ تصادفی صدا می‌زند، و پاسخ‌ها را در
//  native/PumpYaqobi.Tests/*.json می‌ریزد.
//
//  چرا: بازنویسیِ نیتیو فقط وقتی «همان برنامه» است که عددهایش با عددهای
//  نسخه‌ای که کاربر سال‌ها با آن کار کرده مو‌به‌مو یکی باشد. آزمونِ دستی
//  فرضِ من را می‌سنجد؛ این، خودِ رفتارِ واقعی را.
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';

const ROOT = path.join(import.meta.dirname, '..', '..');
const OUT = path.join(ROOT, 'native', 'PumpYaqobi.Tests');

const exe = process.env.PW_CHROMIUM;
const browser = await chromium.launch(exe ? { executablePath: exe, args: ['--no-sandbox'] }
                                         : { args: ['--no-sandbox'] });
const page = await browser.newPage();
const errs = [];
page.on('pageerror', (e) => errs.push(String(e).slice(0, 200)));
await page.goto(pathToFileURL(path.join(ROOT, 'index.html')).href, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2500);

// ── چکنه ──────────────────────────────────────────────────────────────────
const chakana = await page.evaluate(() => {
  let seed = 20260906;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const money = () => Math.round(rnd() * 5000 * 100) / 100;
  const cases = [];
  for (let c = 0; c < 200; c++) {
    const rows = [];
    const n = 1 + Math.floor(rnd() * 8);
    for (let i = 0; i < n; i++) {
      const byMoney = rnd() < 0.3;
      rows.push({
        date: '1405/06/' + String(1 + Math.floor(rnd() * 28)).padStart(2, '0'),
        name: 'م' + i,
        byMoney,
        fuel: byMoney ? 0 : Math.round(rnd() * 200 * 10) / 10,
        priceper: Math.round(rnd() * 90 * 10) / 10,
        bardagi: byMoney ? money() : 0,
        rasid: rnd() < 0.5 ? money() : 0,
      });
    }
    const perRow = rows.map((e) => ({ bardagi: _chakanaBardagi(e) }));
    const totBord = rows.reduce((a, e) => a + _chakanaBardagi(e), 0);
    const totRasid = rows.reduce((a, e) => a + (parseFloat(e.rasid) || 0), 0);
    cases.push({ rows, perRow, totBord, totRasid, totAlb: totBord - totRasid });
  }
  return cases;
});

fs.writeFileSync(path.join(OUT, 'golden-chakana.json'), JSON.stringify(chakana));
console.log('  ✔ golden-chakana.json — ' + chakana.length + ' حالت');

// ── خوددرمانیِ ردیفِ قرض‌دار (_round0 + بردگی + الباقی) ────────────────────
const rowHeal = await page.evaluate(() => {
  let seed = 424242;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const cases = [];
  for (let i = 0; i < 600; i++) {
    const byMoney = rnd() < 0.35;
    const r = {
      byMoney,
      ftype: rnd() < 0.4 ? 'diesel' : 'petrol',
      fuel: rnd() < 0.15 ? 0 : Math.round(rnd() * 400 * 100) / 100,
      priceper: rnd() < 0.2 ? '' : Math.round(rnd() * 95 * 100) / 100,
      bardagi: byMoney && rnd() < 0.8 ? Math.round(rnd() * 90000 * 100) / 100 : 0,
      rasid: rnd() < 0.6 ? Math.round(rnd() * 60000 * 100) / 100 : 0,
      albaqi: 0,
    };
    const before = JSON.parse(JSON.stringify(r));
    if (r.byMoney && !(parseFloat(r.bardagi) || 0) && (parseFloat(r.fuel) || 0) > 0) r.byMoney = false;
    const bardagi = _round0(r.byMoney ? (parseFloat(r.bardagi) || 0) : _personRowBardagi(r));
    const albaqi = _round0(bardagi - (r.rasid || 0));
    cases.push({ before, byMoney: r.byMoney, bardagi, albaqi });
  }
  return cases;
});
fs.writeFileSync(path.join(OUT, 'golden-debtrow.json'), JSON.stringify(rowHeal));
console.log('  ✔ golden-debtrow.json — ' + rowHeal.length + ' ردیف');

// ── شرکت‌های تیل ──────────────────────────────────────────────────────────
const companies = await page.evaluate(() => {
  let seed = 987654321;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const cases = [];
  for (let c = 0; c < 200; c++) {
    const company = { name: 'ش' + c, usdRate: rnd() < 0.3 ? Math.round(rnd() * 80 * 100) / 100 : 0 };
    const rows = [];
    const n = 1 + Math.floor(rnd() * 7);
    for (let i = 0; i < n; i++) {
      const usdPay = rnd() < 0.35;
      rows.push({
        name: 'ر' + i,
        kg: rnd() < 0.5 ? Math.round(rnd() * 40000) : 0,
        ton: rnd() < 0.5 ? Math.round(rnd() * 40 * 1000) / 1000 : '',
        usd: Math.round(rnd() * 900 * 100) / 100,
        rate: rnd() < 0.85 ? Math.round(rnd() * 80 * 100) / 100 : 0,
        payRate: rnd() < 0.3 ? Math.round(rnd() * 80 * 100) / 100 : 0,
        poul: rnd() < 0.7 ? Math.round(rnd() * 200000 * 100) / 100 : 0,
        poulCurrency: usdPay ? 'usd' : 'afn',
      });
    }
    const rate = _companyConvRateOf(company, rows);
    const perRow = rows.map((r) => ({
      ton: cmpTon(r), usd: cmpTotalUsd(r), afn: cmpAfnTotal(r),
      paidAfn: cmpPoulAfn(r, rate), paidUsd: cmpPoulUsd(r, rate),
      albAfn: cmpAlbaqi(r, rate), albUsd: cmpAlbaqiUsd(r, rate),
    }));
    cases.push({ company, rows, rate, perRow,
      totUsd: perRow.reduce((a, x) => a + x.usd, 0),
      totAfn: perRow.reduce((a, x) => a + x.afn, 0),
      paidAfn: perRow.reduce((a, x) => a + x.paidAfn, 0),
      paidUsd: perRow.reduce((a, x) => a + x.paidUsd, 0) });
  }
  return cases;
});
fs.writeFileSync(path.join(OUT, 'golden-company.json'), JSON.stringify(companies));
console.log('  ✔ golden-company.json — ' + companies.length + ' شرکت');

console.log('\n  خطای جاوااسکریپت:', errs.length ? errs.slice(0, 3) : 'ندارد');
await browser.close();
