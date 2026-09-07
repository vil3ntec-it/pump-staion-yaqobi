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

// ── ابزارهای بندِ ۱۹: میله‌زنی، تخلیهٔ تانکر، قرض‌های کهنه، کمبودی، گزارش ماهانه ──
//
// چرا این‌ها با هم در یک فایل: هر پنجِ‌شان «خلاصه‌ای از ثبت‌های موجود»اند و
// هیچ‌کدام دفترِ تازه‌ای نمی‌سازند. اگر یکی‌شان عدد را جور دیگری جمع بزند،
// کاربر همان روزِ آخرِ ماه می‌فهمد — پس هر پنج از خودِ نسخهٔ وب پرسیده می‌شوند.
const tools = await page.evaluate(() => {
  let seed = 90190;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const ri = (n) => Math.floor(rnd() * n);
  const dt = () => '140' + (4 + ri(2)) + '/' + String(1 + ri(12)).padStart(2, '0')
                 + '/' + String(1 + ri(28)).padStart(2, '0');

  // ── ۱) میله‌زنیِ مخزن — TankDip.calc ────────────────────────────────────
  // ظرفیتِ صفر عمداً هست: همان‌جا تقسیم بر صفر کمین کرده و «حد مجاز» به
  // کفِ ۵۰ لیتری می‌افتد.
  const dip = [];
  const keepTank = window.__tankInfo;
  for (let i = 0; i < 300; i++) {
    const fuel = rnd() < 0.5 ? 'diesel' : 'petrol';
    const capacity = ri(5) === 0 ? 0 : Math.round(rnd() * 40000);
    const book = Math.round((rnd() * 2.2 - 0.2) * 15000);
    const actual = Math.round(rnd() * 45000 * 100) / 100;
    window.__tankInfo = () => ({
      fuel, label: fuel === 'diesel' ? 'دیزل' : 'پطرول',
      icon: fuel === 'diesel' ? '🟤' : '⛽',
      book, capacity, capDefined: capacity > 0, threshold: 500,
    });
    const c = TankDip.calc(fuel, actual);
    dip.push({ fuel, capacity, book, actual, pct: c.pct, empty: c.empty,
               receivable: c.receivable, diff: c.diff, allowed: c.allowed, over: c.over });
  }
  window.__tankInfo = keepTank;

  // ── ۲) قرض‌های کهنه — _agingRows ────────────────────────────────────────
  const keepPersons = DB.debtPersons;
  const mkRow = () => ({
    date: rnd() < 0.12 ? '' : dt(),
    ftype: rnd() < 0.4 ? 'diesel' : 'petrol',
    fuel: Math.round(rnd() * 400 * 100) / 100,
    rasid: rnd() < 0.5 ? Math.round(rnd() * 50000) : 0,
    rasidFuel: rnd() < 0.3 ? Math.round(rnd() * 120) : 0,
    bardagi: Math.round(rnd() * 60000),
    albaqi: Math.round(rnd() * 60000),
  });
  const mkAcct = () => ({
    rows: Array.from({ length: ri(4) }, mkRow),
    moneyRows: Array.from({ length: ri(3) }, mkRow),
    rasidFuelP: rnd() < 0.5 ? Math.round(rnd() * 200) : 0,
    rasidFuelD: rnd() < 0.4 ? Math.round(rnd() * 200) : 0,
  });
  const aging = [];
  for (let c = 0; c < 40; c++) {
    const persons = [];
    for (let i = 0; i < 1 + ri(12); i++) {
      const p = Object.assign({ id: 'p' + c + '_' + i, name: 'شخص ' + c + '-' + i,
                                phone: rnd() < 0.5 ? '070000000' + i : '',
                                mode: rnd() < 0.35 ? 'money' : 'fuel',
                                subs: Array.from({ length: ri(3) }, mkAcct) }, mkAcct());
      persons.push(p);
    }
    DB.debtPersons = persons;
    const take = (filter) => _agingRows(filter).map((r) => ({
      id: r.p.id, albaqi: r.f.albaqi, money: r.f.money,
      bardagi: r.f.bardagi, rasid: r.f.rasid, days: r.days,
    }));
    aging.push({ today: persianDate(), persons, all: take(''), fuel: take('fuel'), money: take('money') });
  }
  DB.debtPersons = keepPersons;

  // ── ۳) کمبودی/اضافیِ کارمندان — همان جمعِ _renderStaffShortPanel ─────────
  const keepWaraq = DB.waraqEntries, keepSettle = DB.staffShortSettles;
  const mkShift = (name) => {
    const sd = { workerName: name, pumps: [], transactions: [],
                 fabricDebt: Math.round(rnd() * 40000),
                 pricePerLiter: rnd() < 0.6 ? Math.round(rnd() * 90) : 0,
                 pricePerLiterDiesel: rnd() < 0.5 ? Math.round(rnd() * 90) : 0 };
    for (let i = 0; i < 1 + ri(4); i++) {
      const start = Math.round(rnd() * 500000);
      sd.pumps.push({ num: i + 1, fuel: rnd() < 0.4 ? 'diesel' : 'petrol', start,
                      end: start + Math.round(rnd() * 4000),
                      pricePerLiter: [60, 62, 65, 70][ri(4)], debt: Math.round(rnd() * 30000) });
    }
    for (let i = 0; i < ri(6); i++) {
      const mode = rnd();
      sd.transactions.push({ name: 'ت' + i, liters: Math.round(rnd() * 300),
                             amount: rnd() < 0.5 ? Math.round(rnd() * 40000) : 0,
                             type: rnd() < 0.6 ? 'debt' : 'expense',
                             fuel: rnd() < 0.4 ? 'diesel' : 'petrol',
                             amountAuto: mode < 0.33 ? true : (mode < 0.66 ? false : undefined) });
    }
    return sd;
  };
  // نام‌ها عمداً با «ي/ك» عربی و فاصلهٔ دوتایی هم می‌آیند: کلیدِ گروه‌بندی
  // normFa است، پس «علي  احمد» و «علی احمد» باید یک نفر شمرده شوند.
  const names = ['علی احمد', 'علي  احمد', 'محمود', 'كريم', 'کریم', '', 'نصیر'];
  const staffShort = [];
  for (let c = 0; c < 30; c++) {
    const entries = [];
    for (let w = 0; w < 1 + ri(6); w++) {
      const e = { id: 'w' + w, date: dt() };
      if (rnd() < 0.85) e.day = mkShift(names[ri(names.length)]);
      if (rnd() < 0.7) e.night = mkShift(names[ri(names.length)]);
      entries.push(e);
    }
    const settles = [];
    for (let s = 0; s < ri(5); s++)
      settles.push({ id: 'ss' + s, key: normFa(names[ri(names.length)]),
                     name: names[ri(names.length)], amount: Math.round(rnd() * 20000),
                     type: rnd() < 0.5 ? 'excess' : (rnd() < 0.5 ? 'short' : undefined),
                     date: dt(), at: 1700000000 + s });
    DB.waraqEntries = entries; DB.staffShortSettles = settles;
    _ssSettleIdx = null;
    _renderStaffShortPanel();
    staffShort.push({
      entries, settles,
      rows: _staffShortRows.map((x) => ({ key: x.key, name: x.name, shifts: x.shifts,
        short: x.short, excess: x.excess, paidShort: x.paidShort, paidExcess: x.paidExcess,
        remainShort: x.remainShort, remainExcess: x.remainExcess })),
    });
  }
  DB.waraqEntries = keepWaraq; DB.staffShortSettles = keepSettle;

  // ── ۴) گزارش ماهانه — _mrCompute · _mrPrevKey · _mrAllKeys ──────────────
  const keep = {};
  ['reports', 'shifts', 'expenses', 'extraIncomes', 'fuelEntries',
   'debtQuickReceipts', 'safeEntries', 'tankerLogs'].forEach((k) => { keep[k] = DB[k]; });
  const month = [];
  for (let c = 0; c < 30; c++) {
    const shift = () => ({ money: Math.round(rnd() * 400000), sale: Math.round(rnd() * 6000),
                           profit: Math.round(rnd() * 30000) });
    // ⚠️ پطرول همیشه در ‎DB.reports‎ و دیزل همیشه در ‎DB.shifts‎ — چون
    // ‎_mrCompute‎ هر رکوردِ «در جدولِ اشتباه» را بی‌صدا دور می‌ریزد. آن
    // فیلتر یادگارِ دو جدولِ جدا بود؛ در نیتیو یک جدول با ستونِ سوخت است و
    // چنین رکوردی اصلاً نمی‌تواند وجود داشته باشد.
    DB.reports = Array.from({ length: 1 + ri(8) }, () => ({
      fuel: 'petrol', date: dt(),
      day: rnd() < 0.8 ? shift() : null, night: rnd() < 0.6 ? shift() : null }));
    DB.shifts = Array.from({ length: ri(8) }, () => Object.assign(
      { fuel: 'diesel', date: dt() }, shift()));
    DB.expenses = Array.from({ length: ri(8) }, () => ({ date: dt(), amount: Math.round(rnd() * 9000) }));
    DB.extraIncomes = Array.from({ length: ri(5) }, () => ({ date: dt(), amount: Math.round(rnd() * 9000) }));
    DB.fuelEntries = Array.from({ length: ri(6) }, () => ({
      date: dt(), fuelType: rnd() < 0.5 ? 'diesel' : 'petrol',
      liters: Math.round(rnd() * 12000), totalAFN: Math.round(rnd() * 900000) }));
    DB.debtQuickReceipts = Array.from({ length: ri(6) }, () => ({ date: dt(), amount: Math.round(rnd() * 50000) }));
    DB.safeEntries = Array.from({ length: ri(8) }, () => ({
      date: dt(), currency: rnd() < 0.25 ? 'usd' : 'afn',
      type: rnd() < 0.5 ? 'bardagi' : 'rasid', amount: Math.round(rnd() * 70000) }));
    DB.tankerLogs = Array.from({ length: ri(5) }, () => ({
      date: dt(), fuel: rnd() < 0.5 ? 'diesel' : 'petrol',
      manifest: Math.round(rnd() * 20000), actual: Math.round(rnd() * 20000) }));
    const keys = _mrAllKeys();
    const want = keys[ri(keys.length)];
    month.push({
      today: persianDate(),
      db: { reports: DB.reports, shifts: DB.shifts, expenses: DB.expenses,
            extraIncomes: DB.extraIncomes, fuelEntries: DB.fuelEntries,
            debtQuickReceipts: DB.debtQuickReceipts, safeEntries: DB.safeEntries,
            tankerLogs: DB.tankerLogs },
      keys, key: want, prevKey: _mrPrevKey(want),
      cur: _mrCompute(want), prev: _mrCompute(_mrPrevKey(want)),
      growth: _dashGrowth(_mrCompute(want).net, _mrCompute(_mrPrevKey(want)).net),
    });
  }
  Object.keys(keep).forEach((k) => { DB[k] = keep[k]; });

  return { dip, aging, staffShort, month };
});
fs.writeFileSync(path.join(OUT, 'golden-tools.json'), JSON.stringify(tools));
console.log('  ✔ golden-tools.json — ' + tools.dip.length + ' میله‌زنی، ' + tools.aging.length
            + ' قرض کهنه، ' + tools.staffShort.length + ' کمبودی و ' + tools.month.length + ' ماه');

// ── بندِ ۱۵: شناختِ نوعِ لینکِ دوربین ────────────────────────────────────────
//
// ‎_camMount‎ کارِ اصلی‌اش یک تصمیمِ ساده است: این لینک عکس است، ویدیو است،
// HLS است، RTSP است، یا صفحهٔ وبِ خودِ دوربین؟ همان تصمیم در نیتیو هم باید
// مو‌به‌مو همین باشد، چون هر اشتباهش یعنی دوربینِ کاربر «تصویر نمی‌آید».
//
// ⚠️ ترتیبِ شرط‌ها هم مهم است، نه فقط خودِ الگوها: لینکی مثل
// «…/cgi-bin/hls.m3u8» هم به شرطِ HLS می‌خورد هم به شرطِ عکس، و در نسخهٔ وب
// چون HLS جلوتر است، HLS برنده می‌شود.
const camera = await page.evaluate(() => {
  const urls = [
    '', '   ',
    'rtsp://admin:1234@192.168.1.10:554/stream1',
    'RTSP://192.168.1.10/live',
    'http://cam.local/live/index.m3u8',
    'https://cam.local/live/index.M3U8?token=abc',
    'https://cam.local/live/index.m3u8#t=0',
    'https://cam.local/video.mp4',
    'https://cam.local/video.webm?x=1',
    'https://cam.local/video.ogv',
    'https://cam.local/video.ogg',
    'https://cam.local/snap.jpg',
    'https://cam.local/snap.JPEG?ts=9',
    'https://cam.local/snap.png',
    'https://cam.local/snap.gif',
    'https://cam.local/snap.webp',
    'http://192.168.1.9/mjpg/video.mjpg',
    'http://192.168.1.9/videostream.cgi?user=a&pwd=b',
    'http://192.168.1.9/cgi-bin/snapshot.cgi?channel=1',
    'http://192.168.1.9/faststream.jpg?stream=full',
    'http://192.168.1.9/cgi-bin/hls/index.m3u8',
    'http://192.168.1.9/onvif/snapshot',
    'http://192.168.1.9/',
    'http://192.168.1.9/doc/page/login.asp',
    'https://nvr.example.com:8443/ui/index.html',
    'ftp://cam.local/x.jpg',
    'http://cam.local/x.mp4.txt',
    'http://cam.local/mjpeg',
    'http://cam.local/MJPG/1',
  ];

  // همان زنجیرهٔ شرطِ ‎_camMount‎، خط‌به‌خط.
  const kindOf = (raw) => {
    const u = String(raw || '').trim();
    if (!u) return 'none';
    if (/^rtsp:/i.test(u)) return 'rtsp';
    if (/\.m3u8(\?|#|$)/i.test(u)) return 'hls';
    if (/\.(mp4|webm|ogv|ogg)(\?|#|$)/i.test(u)) return 'video';
    if (/\.(jpe?g|png|gif|webp)(\?|#|$)/i.test(u)
        || /mjpg|mjpeg|snapshot|faststream|videostream|\/cgi-bin\//i.test(u)) return 'image';
    return 'webpage';
  };

  // «تازه کردن» همیشه باید عکسِ نو بیاورد، نه عکسِ کشِ مرورگر.
  const bust = (u, t) => u + (u.indexOf('?') > -1 ? '&' : '?') + '_t=' + t;

  return {
    kinds: urls.map((u) => ({ url: u, kind: kindOf(u) })),
    bust: urls.filter((u) => u.trim()).map((u) => ({ url: u, out: bust(u, 1700000000000) })),
  };
});
fs.writeFileSync(path.join(OUT, 'golden-camera.json'), JSON.stringify(camera));
console.log('  ✔ golden-camera.json — ' + camera.kinds.length + ' لینکِ دوربین');

// ── بندِ ۲۴: موتورِ صدای آفلاین ─────────────────────────────────────────────
//
// جستجوی صوتی در این برنامه سرور ندارد: صدا → بریدنِ سکوت → MFCC(+دلتا) →
// مقایسهٔ DTW با صداهای ثبت‌شده. همه‌اش ریاضیِ محض است، پس همه‌اش هم می‌شود
// مو‌به‌مو سنجید.
//
// ⚠️ ورودی عمداً عددِ صحیح ذخیره می‌شود (مثلِ نمونه‌های ۱۶بیتیِ واقعی) و هر دو
// طرف با تقسیم بر ۳۲۷۶۸ به اعشار می‌برندش — وگرنه خودِ «ورودیِ آزمون» بینِ
// جاوااسکریپت و سی‌شارپ یکی نمی‌ماند و آزمون چیزی را می‌سنجید که نباید.
const voice = await page.evaluate(() => {
  let seed = 7731;
  const rnd = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;

  // ── صداهای ساختگی ولی «صدا‌مانند»: سکوت، بعد چند فرمنت، بعد سکوت ──
  const say = (n, f0, dur, noise) => {
    const pcm = new Int16Array(n);
    const start = Math.floor(n * 0.18), end = Math.min(n, start + dur);
    for (let i = 0; i < n; i++) {
      let v = (rnd() * 2 - 1) * noise;
      if (i >= start && i < end) {
        const t = (i - start) / 16000;
        const env = Math.sin(Math.PI * (i - start) / (end - start));
        v += env * (0.45 * Math.sin(2 * Math.PI * f0 * t)
                  + 0.25 * Math.sin(2 * Math.PI * f0 * 2.7 * t)
                  + 0.15 * Math.sin(2 * Math.PI * f0 * 5.1 * t));
      }
      pcm[i] = Math.max(-32768, Math.min(32767, Math.round(v * 32767)));
    }
    return Array.from(pcm);
  };

  const clips = [
    { id: 'ahmad', pcm: say(6400, 140, 3000, 0.004) },
    { id: 'karim', pcm: say(6400, 190, 2600, 0.004) },
    { id: 'nasir', pcm: say(7000, 165, 3400, 0.006) },
    { id: 'quiet', pcm: say(6400, 150, 3000, 0.0005) },
    { id: 'silence', pcm: say(6400, 150, 0, 0.0009) },   // چیزی گفته نشد
    { id: 'tiny', pcm: say(700, 150, 200, 0.004) },      // خیلی کوتاه
  ];

  const toF32 = (arr) => {
    const f = new Float32Array(arr.length);
    for (let i = 0; i < arr.length; i++) f[i] = arr[i] / 32768;
    return f;
  };

  // ── ۱) بانکِ مِل — ثابت است و پایهٔ همهٔ ویژگی‌ها ──
  const bank = _vxGetMelBank().map((b) => ({ idx: Array.from(b.idx), w: Array.from(b.w) }));

  // ── ۲) FFT ──
  const fft = [];
  for (let c = 0; c < 4; c++) {
    const n = 512;
    const re = new Float32Array(n), im = new Float32Array(n);
    const inRe = [], inIm = [];
    for (let i = 0; i < n; i++) {
      // مقدارهای «گِرد» تا خودِ ورودی بینِ دو زبان یکی بماند
      const a = Math.round((rnd() * 2 - 1) * 1000) / 1000;
      const b = c === 0 ? 0 : Math.round((rnd() * 2 - 1) * 1000) / 1000;
      re[i] = a; im[i] = b; inRe.push(a); inIm.push(b);
    }
    _vxFft(re, im);
    fft.push({ inRe, inIm, outRe: Array.from(re), outIm: Array.from(im) });
  }

  // ── ۳) بریدنِ سکوت ──
  const trim = clips.map((c) => {
    const f = toF32(c.pcm);
    const t = _vxTrim(f);
    return {
      id: c.id,
      ok: !!t,
      off: t ? t.byteOffset / 4 : -1,
      len: t ? t.length : 0,
    };
  });

  // ── ۴) ویژگی‌ها (MFCC + دلتا، فشرده در یک بایت) ──
  const feats = clips.map((c) => {
    const f = _vxFeatures(toF32(c.pcm));
    return { id: c.id, ok: !!f, n: f ? f.n : 0, dim: f ? f.dim : 0, d: f ? Array.from(f.d) : [] };
  });

  // ── ۵) فاصلهٔ DTW — هر صدا با همه، از جمله خودش ──
  const live = clips.map((c) => ({ id: c.id, f: _vxFeatures(toF32(c.pcm)) })).filter((x) => x.f);
  const dtw = [];
  for (const a of live) for (const b of live) dtw.push({ a: a.id, b: b.id, d: _vxDtw(a.f, b.f) });

  // ── ۶) کم کردنِ نرخِ نمونه به ۱۶ کیلوهرتز ──
  const down = [44100, 48000, 16000, 22050].map((rate) => {
    const src = clips[0].pcm.slice(0, 4000);
    const f = toF32(src);
    const out = _vxDownTo16k([f], rate, f.length);
    return { rate, len: src.length, out: Array.from(out) };
  });

  // ── ۷) تطبیقِ نام (مسیرِ آنلاین: متن → نزدیک‌ترین حساب) ──
  const names = ['احمد شاه', 'احمدولی', 'کریم', 'كريم خان', 'نصیر احمد', 'حاجی عبدالله',
                 'محمد', 'محمود', '', 'گل‌آغا'];
  const queries = ['احمد', 'احمدشاه', 'كريم', 'کریم خان', 'نصير', 'حاجي عبدالله',
                   'محمود', 'محمد', 'قاسم', '', 'گلاغا'];
  const lev = [], norm = [], score = [];
  names.forEach((a) => queries.forEach((b) => lev.push({ a, b, d: _staffLev(a, b) })));
  names.concat(queries).forEach((s) => norm.push({ s, out: _staffNorm(s) }));
  names.forEach((n) => queries.forEach((q) => score.push({ name: n, q, sc: _staffScoreName(n, q) })));

  return { bank, fft, trim, feats, dtw, down, lev, norm, score,
           consts: { SR: _VX_SR, FRAME: _VX_FRAME, HOP: _VX_HOP, NFFT: _VX_NFFT,
                     MEL: _VX_MEL, CEP: _VX_CEP, ACCEPT: _VX_ACCEPT, MARGIN: _VX_MARGIN },
           clips };
});
fs.writeFileSync(path.join(OUT, 'golden-voice.json'), JSON.stringify(voice));
console.log('  ✔ golden-voice.json — ' + voice.clips.length + ' صدا، ' + voice.dtw.length
            + ' فاصلهٔ DTW و ' + voice.score.length + ' نمرهٔ نام');

// ── بندِ ۳۱: بکاپِ کاملِ نسخهٔ وب، برای آزمونِ مهاجرت ────────────────────────
//
// فایلِ بکاپِ نسخهٔ وب چیزی جز ‎JSON.stringify(DB)‎ نیست. این‌جا یک ‎DB‎ی
// کوچک ولی **کامل** ساخته می‌شود — از هر جدولی چند رکورد — و کنارش عددهایی
// که خودِ برنامه از همان داده درمی‌آورد.
//
// ⚠️ عددها را خودِ توابعِ برنامه حساب می‌کنند، نه من: اگر نامِ فیلدی را اشتباه
// نوشته باشم، همان‌جا صفر یا NaN درمی‌آید و آزمون لو می‌دهد. یعنی این فایل
// هم «دادهٔ آزمون» است هم «دلیلِ درستیِ خودش».
const legacy = await page.evaluate(() => {
  const keep = JSON.parse(JSON.stringify(DB));

  const money = (n) => Math.round(n);
  const shift = (name, start, end, price, profitPer) => ({
    name, pumpNum: 1, start, end, price, profitPer, buyPerLiter: price - profitPer,
    sale: end - start, money: (end - start) * price, debt: 1200,
    available: (end - start) * price - 1200, availMan: 0,
    profit: (end - start) * profitPer, note: '', savedAt: '1405/06/12',
  });

  const db = {
    // ── پارچه ──
    reports: [
      { id: 1001, reportNum: 1, date: '1405/06/12', dateMi: '2026-09-03', dateQa: '1448/02/20',
        fuel: 'petrol', day: shift('احمد', 1000, 1450, 62, 6), night: shift('کریم', 1450, 1700, 62, 6) },
      { id: 1002, reportNum: 2, date: '1405/06/13', dateMi: '2026-09-04', dateQa: '1448/02/21',
        fuel: 'petrol', day: shift('نصیر', 1700, 2010, 63, 6.5), night: null },
    ],
    shifts: [
      Object.assign({ id: 2001, date: '1405/06/12', dateMi: '2026-09-03', dateQa: '1448/02/20',
                      type: 'day', fuel: 'diesel' }, shift('محمود', 500, 780, 58, 5)),
    ],

    // ── مخزن ──
    fuelEntries: [
      { id: 3001, fuelType: 'petrol', date: '1405/06/10', seller: 'شرکت الف', kg: 30000,
        density: 0.75, priceTon: 700, usdRate: 70, ton: 30, liters: 40000,
        totalUSD: 21000, totalAFN: 1470000, perLiter: 36.75, note: '' },
      { id: 3002, fuelType: 'diesel', date: '1405/06/11', seller: 'شرکت ب', kg: 20000,
        density: 0.83, priceTon: 690, usdRate: 70, ton: 20, liters: 24096.4,
        totalUSD: 13800, totalAFN: 966000, perLiter: 40.09, note: 'دیزل' },
    ],
    tilCompanies: [
      { id: 'c1', name: 'شرکت الف',
        rows: [{ name: 'شرکت الف', date: '1405/06/10', ton: 30, usd: 700, rate: 70, poul: 500000, srcPurchaseId: 3001 }],
        dieselRows: [] },
      { id: 'c2', name: 'شرکت ب', rows: [],
        dieselRows: [{ name: 'شرکت ب', date: '1405/06/11', ton: 20, usd: 690, rate: 70, poul: 0, srcPurchaseId: 3002 }] },
    ],
    tankDips: [
      { id: 'dip1', date: '1405/06/12', at: 1757000000000, fuel: 'petrol',
        actual: 39100, book: 39250, cap: 50000, user: 'مدیر', note: 'میله‌زنیِ شب', adjId: 'adj1' },
    ],
    tankAdjusts: [{ id: 'adj1', dipId: 'dip1', date: '1405/06/12', at: 1757000000000, fuel: 'petrol', liters: -150 }],
    tankerLogs: [
      { id: 'tk1', date: '1405/06/11', fuel: 'diesel', manifest: 24000, actual: 23940, note: 'رانندهٔ کریم' },
    ],
    rateHistory: [
      { date: '1405/06/12', at: 1757000000000, fuel: 'petrol', rate: 62 },
      { date: '1405/06/10', at: 1756000000000, fuel: 'diesel', rate: 58 },
    ],

    // ── قرض‌داران ──
    debtPersons: [
      { id: 'p1', name: 'حاجی عبدالله', phone: '0700000001', mode: 'fuel',
        percentP: 2, percentD: 1, rasidFuelP: 50, rasidFuelD: 0, rasidMoneyP: 0, rasidMoneyD: 0,
        rows: [{ date: '1405/06/12', name: 'حاجی', hawala: 'ح-۱', ftype: 'petrol',
                 fuel: 120, priceper: 62, bardagi: 7440, rasid: 2000, rasidFuel: 0, albaqi: 5440 }],
        moneyRows: [],
        subs: [{ id: 's1', name: 'دیزلِ حاجی', mode: 'fuel', percentP: 0, percentD: 3,
                 rasidFuelP: 0, rasidFuelD: 20, rows: [
                   { date: '1405/06/13', name: 'حاجی', hawala: '', ftype: 'diesel',
                     fuel: 80, priceper: 58, bardagi: 4640, rasid: 0, rasidFuel: 0, albaqi: 4640 }],
                 moneyRows: [] }] },
      { id: 'p2', name: 'کریم خان', phone: '', mode: 'money',
        rows: [], moneyRows: [{ date: '1405/06/12', name: 'کریم', hawala: '', ftype: 'petrol',
                                fuel: 0, priceper: 0, bardagi: 30000, rasid: 10000, rasidFuel: 0,
                                albaqi: 20000, byMoney: true }], subs: [] },
    ],
    noinvPersons: [
      { id: 'n1', name: 'بی‌فاکتورِ اول', mode: 'fuel', rows: [], moneyRows: [], subs: [] },
    ],
    debtQuickReceipts: [
      { id: 'dq1', date: '1405/06/13', account: 'حاجی عبدالله', note: 'نقدی', amount: 1500 },
    ],
    parchaReceipts: [
      { id: 'pr1', date: '1405/06/12', account: 'حاجی عبدالله', name: 'حاجی', hawala: 'ح-۲',
        fuel: 40, priceper: 62, bardagi: 2480, rasid: 480, albaqi: 2000, posted: false },
    ],

    // ── پول ──
    safeEntries: [
      { date: '1405/06/12', type: 'bardagi', title: 'فروشِ روز', amount: 27900, currency: 'afn', note: '' },
      { date: '1405/06/12', type: 'mandagi', title: 'مصرف', amount: 4000, currency: 'afn', note: '' },
      { date: '1405/06/12', type: 'bardagi', title: 'دالر', amount: 300, currency: 'usd', note: '' },
    ],
    sarrafiRows: [
      { id: 'sr1', date: '1405/06/12', desc: 'حوالهٔ شرکت الف', amount: 1000000,
        unit: 'toman', rate: 25000, bardagi: 40, albaqi: 0 },
    ],
    expenses: [
      { date: '1405/06/12', title: 'نان و چای', amount: 800, note: '' },
      { date: '1405/06/13', title: 'ترمیم پایه', amount: 2500, note: '' },
    ],
    chakanaRows: [
      { date: '1405/06/12', name: 'موترسایکل', ftype: 'petrol', fuel: 3, priceper: 62,
        bardagi: 186, rasid: 186, byMoney: false },
    ],
    extraIncomes: [
      { date: '1405/06/12', qty: 200, buy: 55, market: 62, seller: 'خرید عمده', amount: 1400 },
    ],

    // ── ورق ──
    waraqEntries: [
      { id: 'w1', date: '1405/06/12', station: 'پمپ یعقوبی', active: 'day', _migrated: true,
        day: { workerName: 'احمد', fabricDebt: 1200, availableFromShift: 0, pricePerLiter: 62,
               pumps: [{ num: 1, fuel: 'petrol', note: '', start: 1000, end: 1450, pricePerLiter: 62, debt: 900 }],
               transactions: [{ name: 'حاجی', liters: 20, amount: 1240, type: 'debt', fuel: 'petrol', amountAuto: true },
                              { name: 'چای', liters: 0, amount: 300, type: 'expense', fuel: 'petrol' }] },
        night: { workerName: 'کریم', fabricDebt: 0, availableFromShift: 0, pricePerLiter: 62,
                 pumps: [{ num: 1, fuel: 'petrol', note: '', start: 1450, end: 1700, pricePerLiter: 62, debt: 0 }],
                 transactions: [] } },
    ],

    // ── فاکتور ──
    invoices: [
      { id: 'inv1', invoice_number: 1, customer_name: 'حاجی عبدالله', fuel_type: 'petrol',
        price_per_liter: 62, rate_on_create: 62, liters: 40, amount: 0, by_money: false,
        vehicle_type: 'موتر', phone: '0700000001', debt_target_id: null, debt_alias: '',
        status: 'approved', rate_on_approve: 63, created_at: '2026-09-03T10:00:00.000Z', date_sh: '1405/06/12' },
      { id: 'inv2', invoice_number: 2, customer_name: 'کریم خان', fuel_type: 'petrol',
        price_per_liter: 0, rate_on_create: 0, liters: 0, amount: 5000, by_money: true,
        vehicle_type: '', phone: '', debt_target_id: null, debt_alias: '',
        status: 'pending', created_at: '2026-09-04T10:00:00.000Z', date_sh: '1405/06/13' },
    ],

    // ── امانت ──
    amanatAccounts: [
      { id: 'AMA1', name: 'حاجیِ امانت', fuel: 'diesel', myPct: 2, rate: 58, note: '',
        rows: [{ id: 'AMR1', date: '1405/06/10', name: 'محمولهٔ اول', toAcct: '', liters: 5000,
                 taken: 1000, days: 20, temp: 30, state: 'open', closeDate: '', lid: 'closed',
                 basePct: '', actual: '', note: '' }] },
    ],
    amanatSettings: { vaporPct: 0.5 },

    // ── کارمندان ──
    staffMembers: [
      { id: 'st1', name: 'احمد', shiftIn: '07:00', shiftOut: '19:00', salary: 12000 },
      { id: 'st2', name: 'کریم', shiftIn: '19:00', shiftOut: '07:00', salary: 11000 },
    ],
    attendance: [
      { id: 'a1', sid: 'st1', date: '1405/06/12', in: '07:05', out: '19:02' },
      { id: 'a2', sid: 'st2', date: '1405/06/12', in: '19:00', out: '07:10' },
    ],
    salaryPayments: [{ sid: 'st1', month: '1405/05', amount: 12000, date: '1405/06/01' }],
    staffShortSettles: [
      { id: 'ss1', key: 'احمد', name: 'احمد', amount: 400, type: 'short', date: '1405/06/13', at: 1757000000001 },
    ],

    // ── دیگر ──
    cameras: [
      { id: 'cam1', name: 'درِ ورودی', url: 'http://192.168.1.9/cgi-bin/snapshot.cgi', addedAt: '2026-09-03T10:00:00.000Z' },
    ],
    trash: [],

    // ── تنظیم‌ها ──
    stationName: 'پمپ یعقوبی', stationAddress: 'کابل', stationPhone: '0700000000',
    unionRatePetrol: 63, unionRateDiesel: 59,
    buyPerLiter_petrol: 36.75, buyPerLiter_diesel: 40.09,
    tankCapacity_petrol: 50000, tankCapacity_diesel: 30000,
    lowStockThreshold: 1000,
  };

  // ⚠️ عددها را خودِ برنامه می‌دهد — نه من.
  Object.keys(db).forEach((k) => { DB[k] = db[k]; });
  const probe = {
    recordCount: _dbRecordCount(DB),
    stockPetrol: _fuelStock('petrol'),
    stockDiesel: _fuelStock('diesel'),
    monthKeys: _mrAllKeys(),
    month: _mrCompute('1405/06'),
    waraqDay: computeShiftTotals(DB.waraqEntries[0].day),
    waraqShort: computeWaraqShortage(DB.waraqEntries[0].day, computeShiftTotals(DB.waraqEntries[0].day)),
    figures: DB.debtPersons.map((p) => ({ id: p.id, f: _debtSumFigures(p) })),
    aging: _agingRows('').map((r) => ({ id: r.p.id, albaqi: r.f.albaqi, days: r.days })),
    staffShort: (function () { _renderStaffShortPanel(); return _staffShortRows.map((x) => ({ name: x.name, shifts: x.shifts, remainShort: x.remainShort, remainExcess: x.remainExcess })); })(),
    today: persianDate(),
  };
  Object.keys(keep).forEach((k) => { DB[k] = keep[k]; });
  return { db, probe };
});
fs.writeFileSync(path.join(OUT, 'legacy-full-backup.json'), JSON.stringify(legacy.db));
fs.writeFileSync(path.join(OUT, 'legacy-full-expected.json'), JSON.stringify(legacy.probe));
console.log('  ✔ legacy-full-backup.json — ' + legacy.probe.recordCount + ' رکورد در '
            + Object.keys(legacy.db).length + ' کلید');

console.log('\n  خطای جاوااسکریپت:', errs.length ? errs.slice(0, 3) : 'ندارد');
await browser.close();
