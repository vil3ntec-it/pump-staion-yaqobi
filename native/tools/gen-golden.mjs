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

// ── پارچه (شیفت) ──────────────────────────────────────────────────────────
const parcha = await page.evaluate(() => {
  let seed = 13579;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const cases = [];
  for (let i = 0; i < 400; i++) {
    const start = Math.round(rnd() * 900000 * 100) / 100;
    const end = start + Math.round(rnd() * 20000 * 100) / 100;
    const price = Math.round(rnd() * 90 * 100) / 100;
    const profitPer = Math.round(rnd() * 6 * 100) / 100;
    const debt = Math.round(rnd() * 60000 * 100) / 100;
    // همان چهار خطِ saveShift
    const sale = end - start;
    const money = sale * price;
    const profit = sale * profitPer;
    const available = money - debt;
    cases.push({ start, end, price, profitPer, debt, sale, money, profit, available });
  }
  return cases;
});
fs.writeFileSync(path.join(OUT, 'golden-parcha.json'), JSON.stringify(parcha));
console.log('  ✔ golden-parcha.json — ' + parcha.length + ' شیفت');

// ── ورقِ روزانه ───────────────────────────────────────────────────────────
const waraq = await page.evaluate(() => {
  let seed = 2468013;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const cases = [];
  for (let c = 0; c < 250; c++) {
    const sd = {
      pumps: [], transactions: [], fabricDebt: Math.round(rnd() * 40000),
      pricePerLiter: rnd() < 0.6 ? Math.round(rnd() * 90) : 0,
      pricePerLiterDiesel: rnd() < 0.5 ? Math.round(rnd() * 90) : 0,
    };
    const np = 1 + Math.floor(rnd() * 5);
    for (let i = 0; i < np; i++) {
      const start = Math.round(rnd() * 500000);
      sd.pumps.push({
        num: i + 1,
        fuel: rnd() < 0.4 ? 'diesel' : 'petrol',
        start,
        end: start + (rnd() < 0.1 ? -Math.round(rnd() * 100) : Math.round(rnd() * 4000)),
        pricePerLiter: rnd() < 0.9 ? [60, 62, 65, 70][Math.floor(rnd() * 4)] : 0,
        debt: Math.round(rnd() * 30000),
      });
    }
    const nt = Math.floor(rnd() * 8);
    for (let i = 0; i < nt; i++) {
      const mode = rnd();
      const liters = rnd() < 0.8 ? Math.round(rnd() * 300) : 0;
      sd.transactions.push({
        name: rnd() < 0.85 ? 'ت' + i : '',
        liters,
        amount: rnd() < 0.5 ? Math.round(rnd() * 40000) : 0,
        type: rnd() < 0.6 ? 'debt' : 'expense',
        fuel: rnd() < 0.4 ? 'diesel' : 'petrol',
        amountAuto: mode < 0.33 ? true : (mode < 0.66 ? false : undefined),
      });
    }
    const before = JSON.parse(JSON.stringify(sd));
    const repP = _waraqRepPrice(sd, 'petrol');
    const repD = _waraqRepPrice(sd, 'diesel');
    const tot = computeShiftTotals(sd);
    const sh = computeWaraqShortage(sd, tot);
    cases.push({ before, repP, repD, tot, sh, after: sd.transactions });
  }
  return cases;
});
fs.writeFileSync(path.join(OUT, 'golden-waraq.json'), JSON.stringify(waraq));
console.log('  ✔ golden-waraq.json — ' + waraq.length + ' ورق');

// ── خریدِ تیل (مخزن) ──────────────────────────────────────────────────────
const purchase = await page.evaluate(() => {
  let seed = 777333;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const cases = [];
  for (let i = 0; i < 300; i++) {
    const kg = Math.round(rnd() * 60000 * 100) / 100;
    const density = rnd() < 0.1 ? 0 : Math.round(rnd() * 0.9 * 1000) / 1000;
    const priceTon = Math.round(rnd() * 1200 * 100) / 100;
    const usdRate = Math.round(rnd() * 90 * 100) / 100;
    // همان پنج خطِ confirmAddPurchase
    const ton = kg / 1000;
    const liters = density > 0 ? kg / density : 0;
    const totalUSD = ton * priceTon;
    const totalAFN = totalUSD * usdRate;
    const perLiter = liters > 0 ? totalAFN / liters : 0;
    cases.push({ kg, density, priceTon, usdRate, ton, liters, totalUSD, totalAFN, perLiter });
  }
  return cases;
});
fs.writeFileSync(path.join(OUT, 'golden-purchase.json'), JSON.stringify(purchase));
console.log('  ✔ golden-purchase.json — ' + purchase.length + ' خرید');

// ── تیل امانت ─────────────────────────────────────────────────────────────
const amanat = await page.evaluate(() => {
  let seed = 5150;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const s = amSettings();
  const cases = [];
  for (let i = 0; i < 400; i++) {
    const acc = {
      fuel: rnd() < 0.4 ? 'diesel' : 'petrol',
      myPct: rnd() < 0.75 ? Math.round(rnd() * 6 * 100) / 100 : '',
    };
    const row = {
      // «مدت زمان» دستی داده می‌شود تا آزمون به «امروز» بند نباشد
      days: Math.round(rnd() * 400),
      liters: rnd() < 0.08 ? 0 : Math.round(rnd() * 60000 * 100) / 100,
      taken: rnd() < 0.5 ? Math.round(rnd() * 20000 * 100) / 100 : '',
      temp: rnd() < 0.8 ? Math.round(rnd() * 55) : '',
      basePct: rnd() < 0.3 ? Math.round(rnd() * 0.1 * 1000) / 1000 : '',
      actual: rnd() < 0.4 ? Math.round(rnd() * 50000 * 100) / 100 : '',
      state: rnd() < 0.25 ? 'closed' : 'open',
      date: '', closeDate: '',
    };
    const c = amRowCalc(row, acc, s);
    cases.push({ acc, row, c: {
      liters: c.liters, taken: c.taken, days: c.days, temp: c.temp, base: c.base,
      closed: c.closed, loss: c.loss, lossPct: c.lossPct, rest: c.rest,
      hasActual: c.hasActual, actual: c.actual, realLoss: c.realLoss, diff: c.diff,
      myPct: c.myPct, targetL: c.targetL, needPct: c.needPct, askPct: c.askPct,
      netIfMy: c.netIfMy, askL: c.askL, netIfAsk: c.netIfAsk } });
  }
  return { settings: s, cases };
});
fs.writeFileSync(path.join(OUT, 'golden-amanat.json'), JSON.stringify(amanat));

// ── تیل امانت: جمعِ سربرگِ حساب (amAccCalc) ───────────────────────────────
// کارتِ هر حساب همین عددها را نشان می‌دهد، و «فیصدیِ لازم» این‌جا روی درصدِ
// بخارِ کلِ حساب حساب می‌شود نه جمعِ ردیف‌به‌ردیف — همان جایی که آسان اشتباه می‌شود.
const amAcc = await page.evaluate(() => {
  let seed = 31337;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const s = amSettings();
  const cases = [];
  for (let i = 0; i < 200; i++) {
    const acc = {
      fuel: rnd() < 0.4 ? 'diesel' : 'petrol',
      myPct: rnd() < 0.8 ? Math.round(rnd() * 6 * 100) / 100 : '',
      rate: rnd() < 0.5 ? Math.round(rnd() * 90) : '',
      rows: [],
    };
    const nRows = 1 + Math.floor(rnd() * 6);
    for (let k = 0; k < nRows; k++) {
      acc.rows.push({
        days: Math.round(rnd() * 400),
        liters: rnd() < 0.08 ? 0 : Math.round(rnd() * 60000 * 100) / 100,
        taken: rnd() < 0.5 ? Math.round(rnd() * 20000 * 100) / 100 : '',
        temp: rnd() < 0.8 ? Math.round(rnd() * 55) : '',
        basePct: rnd() < 0.3 ? Math.round(rnd() * 0.1 * 1000) / 1000 : '',
        actual: rnd() < 0.4 ? Math.round(rnd() * 50000 * 100) / 100 : '',
        state: rnd() < 0.25 ? 'closed' : 'open',
        date: '', closeDate: '',
      });
    }
    const t = amAccCalc(acc, s);
    cases.push({ acc, t: {
      n: t.n, open: t.open, liters: t.liters, litersOpen: t.litersOpen, taken: t.taken,
      loss: t.loss, lossPct: t.lossPct, rest: t.rest, share: t.share,
      hasActual: t.hasActual, actual: t.actual, realLoss: t.realLoss, realDiff: t.realDiff,
      myPct: t.myPct, targetL: t.targetL, needPct: t.needPct, askPct: t.askPct,
      askL: t.askL, netIfMy: t.netIfMy, netIfAsk: t.netIfAsk,
      rate: t.rate, lossMoney: t.lossMoney, myMoney: t.myMoney, diffMoney: t.diffMoney } });
  }
  return { settings: s, cases };
});
fs.writeFileSync(path.join(OUT, 'golden-amanat-acc.json'), JSON.stringify(amAcc));
console.log('  ✔ golden-amanat-acc.json — ' + amAcc.cases.length + ' حساب');
console.log('  ✔ golden-amanat.json — ' + amanat.cases.length + ' ردیف');

// ── داشبورد ───────────────────────────────────────────────────────────────
// نکتهٔ ظریف: تقریباً همه‌چیزِ داشبورد به «امروز» بند است. پس خودِ لحظهٔ اجرا هم
// در فایل نوشته می‌شود و آزمونِ سی‌شارپ همان لحظه را به سرویس می‌دهد — وگرنه
// اگر آزمون درست سرِ نیمه‌شب اجرا شود، دو طرف دو «امروز» می‌بینند.
const dash = await page.evaluate(() => {
  let seed = 90210;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;

  const growth = [];
  for (let i = 0; i < 400; i++) {
    const prev = rnd() < 0.12 ? 0 : Math.round((rnd() - 0.35) * 200000);
    const cur  = rnd() < 0.10 ? 0 : Math.round((rnd() - 0.35) * 200000);
    growth.push({ cur, prev, g: _dashGrowth(cur, prev) });
  }

  const ranges = ['day', 'week', 'month', 'year'];
  const layout = ranges.map(r => {
    const save = _dashRange; _dashRange = r;
    const n = _dashCols();
    const mk = _dashMakeBuckets();
    _dashRange = save;
    return { range: r, cols: n, slot: mk.slot,
             labels: mk.buckets.map(b => b.label), fulls: mk.buckets.map(b => b.full) };
  });

  // مصارفِ ساختگی فقط برای همین مقایسه — چیزی در DB نوشته نمی‌شود
  const exps = [];
  for (let i = 0; i < 300; i++) {
    const off = Math.round((rnd() - 0.5) * 800);
    const p = _dashDayAt(off);
    exps.push({ date: p.y + '/' + p.m + '/' + p.d, amount: Math.round(rnd() * 90000) });
  }
  const realExp = DB.expenses;
  DB.expenses = exps;
  const q = _dashExpQuick();
  DB.expenses = realExp;

  const safe = [];
  for (let i = 0; i < 300; i++) {
    safe.push({ date: '1405/01/01', amount: Math.round(rnd() * 70000),
                currency: rnd() < 0.3 ? 'usd' : 'afn',
                type: rnd() < 0.5 ? 'bardagi' : 'mandagi' });
  }
  const realSafe = DB.safeEntries;
  DB.safeEntries = safe;
  const sb = _dashSafeBalance();
  DB.safeEntries = realSafe;

  return { nowIso: new Date().toISOString(), today: persianDate(),
           growth, layout, exps, expQuick: q, safe, safeBalance: sb };
});
fs.writeFileSync(path.join(OUT, 'golden-dash.json'), JSON.stringify(dash));
console.log('  ✔ golden-dash.json — ' + dash.growth.length + ' رشد و ' + dash.layout.length + ' نوار');

// ── بردنِ ردیف به حسابِ صاحبش ─────────────────────────────────────────────
// «رسید پارچه‌ها» و «ورق روزانه» هر دو از همین چند تابع رد می‌شوند. تطبیقِ نام
// سه پله امتیاز دارد و ساده به‌نظر می‌رسد تا وقتی که یک حسابِ فرعیِ هم‌نام
// وسط باشد — این‌جا همان حالت‌ها هم ساخته می‌شوند.
const posting = await page.evaluate(() => {
  let seed = 771;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const pick = a => a[Math.floor(rnd() * a.length) % a.length];

  const first = ['احمد', 'محمود', 'حاجی', 'نور', 'عبدالله', 'ولی', 'سید', 'گل'];
  const last  = ['خان', 'محمد', 'الدین', 'آغا', 'جان', 'شاه'];
  const noise = ['دیزل', 'پطرول', 'بنزین', 'حواله', 'نقد', ''];

  // چند شخص با حساب‌های فرعی — سرِ همین‌ها تطبیق سخت می‌شود
  const persons = [];
  for (let i = 0; i < 14; i++) {
    const nm = pick(first) + ' ' + pick(last);
    const subs = [];
    const nSub = Math.floor(rnd() * 3);
    for (let k = 0; k < nSub; k++) subs.push({ id: 's' + i + k, name: nm + ' ' + pick(last), rows: [] });
    persons.push({ id: 'p' + i, name: nm, subs, rows: [] });
  }
  const realPersons = DB.debtPersons;
  DB.debtPersons = persons;

  const cases = [];
  for (let i = 0; i < 300; i++) {
    const target = pick(persons);
    const useSub = target.subs.length && rnd() < 0.45;
    const base = useSub ? pick(target.subs).name : target.name;
    // گاهی نامِ کامل، گاهی نصفه، گاهی با کلمهٔ اضافه
    const mode = Math.floor(rnd() * 4);
    const typed = mode === 0 ? base
                : mode === 1 ? base.split(' ')[0]
                : mode === 2 ? base + ' ' + pick(noise)
                : pick(first) + ' ' + pick(last);
    const raw = typed + ' ' + pick(noise);
    const found = findDebtAcctForText(raw, typed);
    cases.push({ raw, typed,
                 person: found ? found.person.name : null,
                 acct: found ? (found.acct.name || found.person.name) : null });
  }

  const texts = [];
  for (let i = 0; i < 200; i++) {
    const t = pick(first) + (rnd() < .5 ? '  ' : ' ') + pick(last)
            + (rnd() < .5 ? ' ' + pick(noise) : '')
            + (rnd() < .3 ? ' ۱۲۳' : '') + (rnd() < .3 ? ' ي ك' : '');
    texts.push({ t, norm: normFa(t), strip: _stripFuelWords(t), fuel: _detectFuelType(t) });
  }

  DB.debtPersons = realPersons;
  return { persons: persons.map(p => ({ name: p.name, subs: p.subs.map(s => s.name) })), cases, texts };
});
fs.writeFileSync(path.join(OUT, 'golden-posting.json'), JSON.stringify(posting));
console.log('  ✔ golden-posting.json — ' + posting.cases.length + ' تطبیق و ' + posting.texts.length + ' متن');

// ── مفاد / ضرر ────────────────────────────────────────────────────────────
// جای لغزشش «اختلافِ نرخِ ثبت و تاییدِ فاکتور» است: یک عددِ علامت‌دار که مثبتش
// به مصارف می‌رود و منفی‌اش به درآمدها، ولی هر دو کادر باید مثبت بمانند.
const profit = await page.evaluate(() => {
  let seed = 6060;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;

  const cases = [];
  for (let c = 0; c < 120; c++) {
    const reports = [], shifts = [], noinv = [], extras = [], exps = [], invs = [];
    for (let i = 0; i < 4; i++)
      reports.push({ fuel: 'petrol', date: '1405/06/0' + (i + 1),
                     day: { profit: Math.round((rnd() - 0.2) * 9000) },
                     night: { profit: Math.round((rnd() - 0.2) * 9000) } });
    for (let i = 0; i < 3; i++)
      shifts.push({ fuel: 'diesel', date: '1405/06/0' + (i + 1),
                    profit: Math.round((rnd() - 0.2) * 7000) });
    noinv.push({ rows: [ { date: '1405/06/01', bardagi: Math.round(rnd() * 40000) },
                         { date: '1405/06/02', bardagi: Math.round(rnd() * 40000) } ] });
    extras.push({ date: '1405/06/03', amount: Math.round(rnd() * 30000) });
    for (let i = 0; i < 3; i++)
      exps.push({ date: '1405/06/0' + (i + 1), amount: Math.round(rnd() * 20000) });
    for (let i = 0; i < 4; i++) {
      const approved = rnd() < 0.7;
      invs.push({ by_money: rnd() < 0.2, status: approved ? 'approved' : 'pending',
                  liters: rnd() < 0.15 ? 0 : Math.round(rnd() * 900),
                  price_per_liter: 60, rate_on_create: rnd() < 0.5 ? 60 : null,
                  rate_on_approve: approved ? Math.round(55 + rnd() * 12) : 0,
                  approved_at: '2026-09-01T00:00:00Z' });
    }
    const manualIn = Math.round(rnd() * 5000), manualExp = Math.round(rnd() * 5000);

    const rReports = DB.reports, rShifts = DB.shifts, rNoinv = DB.noinvPersons,
          rExtra = DB.extraIncomes, rExp = DB.expenses, rInv = DB.invoices;
    DB.reports = reports; DB.shifts = shifts; DB.noinvPersons = noinv;
    DB.extraIncomes = extras; DB.expenses = exps; DB.invoices = invs;

    const diff = _plInvRateDiff(null);
    const bd = _plBreakdownData(null);
    const shiftProfit = reports.reduce((a, r) => a + (r.day.profit + r.night.profit), 0)
                      + shifts.reduce((a, s) => a + s.profit, 0);
    const noinvTotal = noinv.reduce((a, p) => a + p.rows.reduce((b, r) => b + r.bardagi, 0), 0);
    const extraSum = extras.reduce((a, e) => a + e.amount, 0);
    const expSum = exps.reduce((a, e) => a + e.amount, 0);
    const income = shiftProfit + noinvTotal + extraSum + manualIn + Math.max(-diff, 0);
    const expenses = expSum + manualExp + Math.max(diff, 0);

    DB.reports = rReports; DB.shifts = rShifts; DB.noinvPersons = rNoinv;
    DB.extraIncomes = rExtra; DB.expenses = rExp; DB.invoices = rInv;

    cases.push({ reports, shifts, noinv, extras, exps, invs, manualIn, manualExp,
                 diff, bd, income, expenses, net: income - expenses });
  }

  const bulk = [];
  for (let i = 0; i < 60; i++) {
    const qty = Math.round(rnd() * 20000), buy = Math.round(rnd() * 70), market = Math.round(rnd() * 80);
    bulk.push({ qty, buy, market, seller: qty * buy, income: qty * (market - buy) });
  }
  return { cases, bulk };
});
fs.writeFileSync(path.join(OUT, 'golden-profit.json'), JSON.stringify(profit));
console.log('  ✔ golden-profit.json — ' + profit.cases.length + ' حالت');

// ── پارچه: calcShift و saveShift ─────────────────────────────────────────
// این‌جا هیچ فرمولی در جاوااسکریپت بازنویسی نمی‌شود: کادرهای واقعیِ صفحه پر
// می‌شوند، خودِ calcShift/saveShift صدا زده می‌شوند، و بعد هرچه روی صفحه و
// در DB نشسته خوانده می‌شود. پس اگر رفتارِ نیتیو با نسخهٔ وب فرق کند، همین
// عددها لو می‌دهند.
const shiftGolden = await page.evaluate(() => {
  let seed = 31337;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const en = (s) => (typeof toEnDigits === 'function' ? toEnDigits(String(s)) : String(s));
  const num = (s) => { const v = parseFloat(en(s).replace(/[^0-9.\-]/g, '')); return isFinite(v) ? v : 0; };
  const setV = (id, v) => { const e = document.getElementById(id); if (e) e.value = (v === '' ? '' : String(v)); };
  const txt = (id) => { const e = document.getElementById(id); return e ? en(e.textContent).trim() : ''; };

  const calc = [];
  for (let c = 0; c < 400; c++) {
    const p = ['d', 'n', 'dd', 'dn'][Math.floor(rnd() * 4)];
    const fuel = (p === 'dd' || p === 'dn') ? 'diesel' : 'petrol';
    const start = Math.round(rnd() * 900000 * 10) / 10;
    const end = rnd() < 0.15 ? start - Math.round(rnd() * 500) : start + Math.round(rnd() * 9000 * 10) / 10;
    const price = rnd() < 0.15 ? 0 : Math.round(rnd() * 95 * 10) / 10;
    const debt = rnd() < 0.35 ? Math.round(rnd() * 400000) : 0;
    const buy = rnd() < 0.25 ? 0 : Math.round(rnd() * 88 * 10) / 10;
    const box = rnd() < 0.5 ? Math.round(rnd() * 20 * 10) / 10 : '';

    DB.buyPerLiter_petrol = 0; DB.buyPerLiter_diesel = 0;
    DB['buyPerLiter_' + fuel] = buy;
    setV(p + '-start', start); setV(p + '-end', end); setV(p + '-price', price);
    setV(p + '-debt', debt); setV(p + '-profit-per', box);
    calcShift(p);

    const availEl = document.getElementById(p + '-available');
    calc.push({
      p, fuel, start, end, price, debt, buy, box: box === '' ? 0 : box,
      profitPerBox: parseFloat(document.getElementById(p + '-profit-per').value) || 0,
      sale: txt(p + '-sale'), money: txt(p + '-money'),
      profit: txt(p + '-profit-auto'), avail: txt(p + '-available'),
      availNeg: !!(availEl && availEl.style.color.indexOf('red') >= 0),
      buyLbl: txt(p + '-buy-per-lbl'),
      saleN: num(txt(p + '-sale')), moneyN: num(txt(p + '-money')),
      profitN: num(txt(p + '-profit-auto')), availN: num(txt(p + '-available')),
    });
  }

  // ── saveShift: رکوردی که واقعاً ذخیره می‌شود ──────────────────────────
  // ⚠️ ‎saveShift‎ در نسخهٔ وب پشتِ نقش است: ‎currentRole === 'viewer'‎ آن را
  // بی‌صدا رد می‌کند. پس نقشِ مدیر لازم است — همان چیزی که در نیتیو
  // ‎_perm.Require(Permission.EditData)‎ نگه می‌دارد.
  try { currentRole = 'admin'; } catch (e) {}
  DB.reports = []; DB.shifts = []; DB.waraqEntries = [];
  const saved = [];
  for (let c = 0; c < 120; c++) {
    const fuel = rnd() < 0.5 ? 'petrol' : 'diesel';
    const type = rnd() < 0.5 ? 'day' : 'night';
    const p = fuel === 'diesel' ? (type === 'day' ? 'dd' : 'dn') : (type === 'day' ? 'd' : 'n');
    const start = Math.round(rnd() * 500000);
    const end = start + Math.round(rnd() * 6000);
    const price = Math.round(rnd() * 90 * 10) / 10;
    const debt = rnd() < 0.4 ? Math.round(rnd() * 200000) : 0;
    const buy = rnd() < 0.3 ? 0 : Math.round(rnd() * 80 * 10) / 10;
    const pumpNum = 1 + Math.floor(rnd() * 6);
    const date = '1405/06/' + String(1 + Math.floor(rnd() * 28)).padStart(2, '0');
    const forceNew = rnd() < 0.4;
    // کادرِ فایده صریح نوشته می‌شود: وقتی فیِ خرید صفر است، calcShift دست به
    // این کادر نمی‌زند و عددِ حالتِ پیش در آن می‌ماند. اگر این‌جا ننویسیم،
    // دادهٔ طلایی به حالتِ پنهانِ قبلی بند می‌شود و بازپخش‌شدنی نیست.
    const box = Math.round(rnd() * 25 * 10) / 10;

    DB.buyPerLiter_petrol = 0; DB.buyPerLiter_diesel = 0;
    DB['buyPerLiter_' + fuel] = buy;
    setV(fuel === 'diesel' ? 'pa-date-diesel' : 'pa-date', date);
    if (forceNew) newParcha(type, fuel);
    setV(p + '-name', 'کارمند' + c); setV(p + '-pumpnum', pumpNum);
    setV(p + '-start', start); setV(p + '-end', end);
    setV(p + '-price', price); setV(p + '-debt', debt);
    setV(p + '-profit-per', box);
    calcShift(p);
    saveShift(type, fuel);

    // رکوردی که همین حالا نوشته شد — با همان قاعده‌ای که خودِ saveShift
    // برای پیدا کردنش به کار می‌برد، نه «آخرین ردیفِ فهرست»: پارچهٔ دیزلِ
    // بی‌پرچم ممکن است ردیفی وسطِ فهرست را به‌روز کرده باشد.
    let rec = null;
    if (fuel === 'petrol') { const r = getCurrentReport('petrol'); rec = r ? r[type] : null; }
    else {
      const l = DB.shifts.filter((s) => s.fuel === 'diesel' && s.type === type && s.date === date);
      rec = l.length ? l[l.length - 1] : null;
    }

    const w = (DB.waraqEntries || []).find((x) => x.date === date);
    const sd = w ? w[type] : null;
    // ⚠️ خودِ srcKey ذخیره نمی‌شود: وسطش Date.now() است، پس هر بار که این
    // ابزار اجرا شود عوض می‌شود و فایلِ طلایی بی‌جهت تغییر می‌کند. آنچه
    // اهمیت دارد شکلش است («d-…-night») و اینکه برای هر پارچه جدا باشد —
    // و جدا بودنشان را آزمون از روی خودِ دیتابیس می‌سنجد.
    const shape = (k) => (k || '').replace(/^([dp])-\d+-(day|night)$/, '$1-<id>-$2');
    const pumps = sd ? sd.pumps.map((e) => ({
      num: e.num, fuel: e.fuel, worker: e.worker, start: e.start, end: e.end,
      pricePerLiter: e.pricePerLiter, debt: e.debt, srcKeyShape: shape(e.srcKey),
    })) : [];

    saved.push({
      fuel, type, date, forceNew, pumpNum, start, end, price, debt, buy, box,
      rec: rec && {
        name: rec.name, pumpNum: rec.pumpNum, start: rec.start, end: rec.end,
        price: rec.price, profitPer: rec.profitPer, buyPerLiter: rec.buyPerLiter,
        sale: rec.sale, money: rec.money, debt: rec.debt, available: rec.available,
        profit: rec.profit, savedAt: rec.savedAt,
      },
      reportCount: DB.reports.filter((r) => r.fuel === 'petrol').length,
      dieselCount: DB.shifts.filter((s) => s.fuel === 'diesel').length,
      pumps,
      availableFromShift: sd ? (sd.availableFromShift || 0) : 0,
      shiftPrice: sd ? (fuel === 'diesel' ? (sd.pricePerLiterDiesel || 0) : (sd.pricePerLiter || 0)) : 0,
      workerName: sd ? (sd.workerName || '') : '',
    });
  }
  // ‎ensureReport‎ پارچهٔ نخست را با تاریخِ **امروز** می‌سازد، پس دادهٔ طلایی
  // به روزِ ساخته‌شدنش بند است. همان روز این‌جا نوشته می‌شود تا آزمون هر روز
  // همان را بازپخش کند و به تقویمِ ماشینِ آزمون وابسته نباشد.
  return { calc, saved, today: (typeof persianDate === 'function' ? persianDate() : '') };
});
fs.writeFileSync(path.join(OUT, 'golden-shift.json'), JSON.stringify(shiftGolden));
console.log('  ✔ golden-shift.json — ' + shiftGolden.calc.length + ' calcShift و '
            + shiftGolden.saved.length + ' saveShift');

// ── خرید مخزن ← شرکت ─────────────────────────────────────────────────────
// دو چیز سنجیده می‌شود، هر دو با خودِ توابعِ نسخهٔ وب:
//   ۱. _findCompanyByName — چهار پلهٔ تطبیقِ نام. یک اشتباه این‌جا یعنی
//      خریدِ یک شرکت در حسابِ شرکتِ دیگری می‌نشیند، یا شرکتِ تکراری ساخته
//      می‌شود (هر دو را صاحب ریپو صریحاً ممنوع کرده).
//   ۲. syncPurchaseToCompany — ردیفی که در حساب می‌نشیند، و این‌که در
//      نخستین ردیفِ خالی می‌نشیند نه با بازنویسیِ ردیفِ پُر.
const purchaseCompany = await page.evaluate(() => {
  let seed = 88231;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;

  const names = [
    'ح قادر', 'ح قادر و شیر آقا', 'شرکت نفت هرات', 'نفت هرات',
    'محمد يوسف', 'محمد یوسف زاده', 'كابل تيل', 'کابل تیل',
    'برادران احمدی', 'احمدی', 'الفت', 'شرکت الفت جنوب',
  ];

  // ── ۱) تطبیقِ نام ────────────────────────────────────────────────────
  const keepCompanies = DB.tilCompanies;
  DB.tilCompanies = names.map((n, i) => ({ id: 'c' + i, name: n, rows: [], dieselRows: [] }));
  const queries = names.concat([
    'ح', 'قادر', 'ح قادر و', 'شرکت نفت', 'هرات', 'محمد یوسف',
    'كابل', 'تیل کابل', 'برادران', 'الفت جنوب', 'هیچ‌کس', '', 'ا',
    'شرکت   نفت   هرات', 'مـحمد یوسف',
  ]);
  const match = queries.map((q) => {
    const c = _findCompanyByName(q);
    return { q, name: c ? c.name : null };
  });
  DB.tilCompanies = keepCompanies;

  // ── ۲) نشستنِ خرید در حساب ───────────────────────────────────────────
  const sync = [];
  for (let c = 0; c < 60; c++) {
    const seller = names[Math.floor(rnd() * names.length)];
    const fuel = rnd() < 0.5 ? 'diesel' : 'petrol';
    const kg = Math.round(rnd() * 40000);
    const density = Math.round((0.7 + rnd() * 0.15) * 1000) / 1000;
    const priceTon = Math.round(rnd() * 900);
    const usdRate = Math.round(rnd() * 90);
    const date = '1405/06/' + String(1 + Math.floor(rnd() * 28)).padStart(2, '0');

    // حسابِ شرکت گاهی از پیش چند ردیف دارد — بعضی خالی، بعضی پُر، و
    // ⚠️ ترتیبشان مهم است: خرید در نخستین ردیفِ **خالی** می‌نشیند، پس
    // همان ترتیب هم ذخیره می‌شود تا آزمون بتواند مو‌به‌مو بازش بسازد.
    const preRows = [];
    const preBlanks = [];
    const pre = Math.floor(rnd() * 3);
    for (let i = 0; i < pre; i++) {
      const blank = rnd() < 0.5;
      preBlanks.push(blank);
      preRows.push(blank
        ? { name: '', date: '', ton: 0, usd: 0, rate: 0, poul: 0 }
        : { name: 'قبلی' + i, date: '1405/06/01', ton: 10 + i, usd: 500, rate: 70, poul: 0 });
    }

    DB.tilCompanies = [{ id: 'cx', name: seller, rows: fuel === 'petrol' ? preRows.slice() : [],
                         dieselRows: fuel === 'diesel' ? preRows.slice() : [] }];

    const ton = kg / 1000;
    const liters = kg / density;
    const totalUSD = ton * priceTon;
    const totalAFN = totalUSD * usdRate;
    const perLiter = liters > 0 ? totalAFN / liters : 0;
    const entry = { id: 'p' + c, fuelType: fuel, date, seller, kg, density, priceTon,
                    usdRate, ton, liters, totalUSD, totalAFN, perLiter, note: '' };

    const ok = syncPurchaseToCompany(entry);
    const rows = fuel === 'diesel' ? DB.tilCompanies[0].dieselRows : DB.tilCompanies[0].rows;
    const idx = rows.findIndex((r) => r.srcPurchaseId === entry.id);

    sync.push({
      seller, fuel, date, kg, density, priceTon, usdRate,
      ton, liters, totalUSD, totalAFN, perLiter,
      preBlanks,
      ok, landedAt: idx, rowCount: rows.length,
      row: idx >= 0 ? { name: rows[idx].name, date: rows[idx].date, ton: rows[idx].ton,
                        usd: rows[idx].usd, rate: rows[idx].rate, poul: rows[idx].poul } : null,
      // ردیف‌های پُرِ پیشین باید دست‌نخورده مانده باشند
      survivors: rows.filter((r) => (r.name || '').startsWith('قبلی')).map((r) => r.name),
    });
    DB.tilCompanies = keepCompanies;
  }

  return { match, sync };
});
fs.writeFileSync(path.join(OUT, 'golden-purchase-company.json'), JSON.stringify(purchaseCompany));
console.log('  ✔ golden-purchase-company.json — ' + purchaseCompany.match.length
            + ' تطبیقِ نام و ' + purchaseCompany.sync.length + ' خرید');

console.log('\n  خطای جاوااسکریپت:', errs.length ? errs.slice(0, 3) : 'ندارد');
await browser.close();
