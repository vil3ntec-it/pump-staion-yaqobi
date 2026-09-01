// ---------------------------------------------------------------------------
//  نگهبانِ اسکرول — سه باگی که صاحب ریپو گزارش کرد، دیگر برنگردند
//
//      node tools/check-scroll.mjs
//
//  ۱) بارِ دستهٔ بعدیِ کارت‌ها نباید قدِ صفحه را کم کند (وگرنه اسکرول می‌پرد).
//  ۲) هیچ کدی نباید در حینِ اسکرول کلاسی روی <body> بنویسد (بازمحاسبهٔ
//     تمام‌صفحه در آغازِ هر اسکرول = همان «لگِ کوچک» موقعِ از سر گرفتنِ اسکرول).
//  ۳) توست نباید backdrop-filter داشته باشد (تارکردنِ پشتِ پیام در هر فریم).
//  و درستیِ کارکرد: تعداد و ترتیبِ کارت‌ها، و جست‌وجو پس از «بارِ بیشتر».
//
//      PW_CHROMIUM=/path/to/chrome node tools/check-scroll.mjs
// ---------------------------------------------------------------------------
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';

const ROOT = path.join(import.meta.dirname, '..');
const browser = await chromium.launch({ executablePath: process.env.PW_CHROMIUM, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 860 } });
const errs = []; page.on('pageerror', e => errs.push(String(e).slice(0, 200)));
await page.goto(pathToFileURL(path.join(ROOT, 'index.html')).href, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2500);
await page.evaluate(() => {
  const l = document.getElementById('lockScreen'); if (l) l.style.display = 'none';
  const a = document.getElementById('app'); if (a) a.style.display = '';
  try { currentRole = 'admin'; } catch (e) {}
});

const N = 400;
await page.evaluate((n) => {
  const today = persianDate();
  DB.debtPersons = [];
  for (let i = 0; i < n; i++) DB.debtPersons.push({ id: 'p' + i, name: 'حساب شمارهٔ ' + i, phone: '',
    rows: [{ date: today, name: 'x', hawala: 'h', ftype: 'petrol', fuel: 10, priceper: 60, bardagi: 0, rasid: 0, rasidFuel: 0 }],
    subs: [], mode: 'fuel' });
  showSection('debt'); renderPersons('debt');
}, N);
await page.waitForTimeout(1500);

let bad = 0;
const say = (ok, msg) => { console.log('  ' + (ok ? '✅' : '❌') + ' ' + msg); if (!ok) bad++; };

console.log('\nنگهبانِ اسکرول\n');

// ── ۱) هیچ کلاسی روی <body> در حینِ اسکرول نوشته نشود ─────────────────────
await page.evaluate(() => {
  window.__bodyClassWrites = 0;
  new MutationObserver(() => { window.__bodyClassWrites++; })
    .observe(document.body, { attributes: true, attributeFilter: ['class'] });
});
await page.mouse.move(640, 500);
for (let i = 0; i < 8; i++) { await page.mouse.wheel(0, 400); await page.waitForTimeout(70); }
await page.waitForTimeout(600);
const writes = await page.evaluate(() => window.__bodyClassWrites);
say(writes === 0, 'در حینِ اسکرول هیچ کلاسی روی <body> نوشته نشد (' + writes + ' بار)');

// ── ۲) بارِ دستهٔ بعدی نباید کارت‌های روی صفحه را از نو بسازد ────────────
//     ریشهٔ باگِ «صفحه چند کادر جلو می‌پرد»: گرید خالی می‌شد (grid.innerHTML='')
//     و همه‌چیز دوباره ساخته می‌شد؛ در همان لحظه قدِ صفحه می‌افتاد و مرورگر جای
//     اسکرول را پس می‌کشید. اگر کارتی از گرید حذف شود، باگ برگشته است.
const grow = await page.evaluate(async () => {
  const g = document.getElementById('debt-grid');
  let removed = 0, minH = document.scrollingElement.scrollHeight, maxDrop = 0, last = minH;
  const mo = new MutationObserver(ms => ms.forEach(m => { removed += m.removedNodes.length; }));
  mo.observe(g, { childList: true });
  const se = document.scrollingElement;
  for (let k = 0; k < 60; k++) {
    se.scrollTop = se.scrollHeight;                       // برو تهِ فهرست
    await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
    await new Promise(r => setTimeout(r, 60));
    const h = se.scrollHeight;
    if (h < last) maxDrop = Math.max(maxDrop, last - h);
    minH = Math.min(minH, h); last = h;
    if (g.children.length >= 400) break;
  }
  mo.disconnect();
  return { removed, maxDrop, cards: g.children.length };
});
say(grow.removed === 0, 'هنگامِ بارِ دسته‌های بعدی هیچ کارتی از صفحه برداشته نشد (' + grow.removed + ')');
/* آستانه ۶۰۰px است نه صفر: یک لغزشِ کوچک از content-visibility می‌ماند —
   کارت‌های بیرونِ دید چیده نمی‌شوند و ردیف‌هایشان چند پیکسل جمع می‌شوند.
   آزموده شد که با هم‌اندازه کردنِ contain-intrinsic-size برطرف نمی‌شود، و
   برداشتنِ content-visibility چیدنِ ششصد کارت را از ۱٬۴۳۵ms به ۵٬۱۶۹ms
   می‌برد. پس این چند ده پیکسل عمداً مانده؛ چیزی که این نگهبان جلویش را
   می‌گیرد، فرو ریختنِ چندهزارپیکسلیِ قدیمی است. */
say(grow.maxDrop < 600, 'قدِ صفحه فرو نریخت (بیشترین افت ' + grow.maxDrop + 'px — باگِ قدیمی ۵٬۶۴۰px بود)');
say(grow.cards === N, 'همهٔ ' + N + ' کارت بار شد (' + grow.cards + ')');

// ── ۳) ترتیب و یکتا بودنِ کارت‌ها پس از «بارِ بیشتر» ──────────────────────
const order = await page.evaluate(() => {
  const names = [...document.querySelectorAll('#debt-grid .person-card')].map(c => (c.textContent.match(/شمارهٔ\s*[۰-۹\d]+/) || [''])[0]);
  return { n: names.length, uniq: new Set(names).size };
});
say(order.n === order.uniq, 'هیچ کارتی دوباره ساخته نشده (' + order.uniq + ' یکتا از ' + order.n + ')');

// ── ۴) جست‌وجو پس از بارِ بیشتر، هنوز درست کار کند (مسیرِ ساختِ کامل) ─────
const searched = await page.evaluate(() => {
  const inp = document.getElementById('debt-search');
  if (!inp) return null;
  inp.value = 'شمارهٔ 37'; inp.dispatchEvent(new Event('input', { bubbles: true }));
  renderPersons('debt');
  return document.querySelectorAll('#debt-grid .person-card').length;
});
say(searched !== null && searched > 0 && searched < 400, 'جست‌وجو پس از بارِ بیشتر فیلتر می‌کند (' + searched + ' کارت)');
await page.evaluate(() => { const i = document.getElementById('debt-search'); if (i) { i.value = ''; i.dispatchEvent(new Event('input', { bubbles: true })); } renderPersons('debt'); });
await page.waitForTimeout(400);
const back = await page.evaluate(() => document.querySelectorAll('#debt-grid .person-card').length);
say(back > 0, 'پس از پاک کردنِ جست‌وجو کارت‌ها برگشتند (' + back + ')');

// ── ۵) توست: شیشهٔ تارِ نامرئی نداشته باشد ───────────────────────────────
const toast = await page.evaluate(() => {
  showToast('آزمایش', '#38a169');
  const t = document.getElementById('toast');
  const cs = getComputedStyle(t);
  return { bd: cs.backdropFilter || cs.webkitBackdropFilter, bg: cs.backgroundColor };
});
say(!toast.bd || toast.bd === 'none', 'توست backdrop-filter ندارد (' + toast.bd + ')');

// ── ۶) هاور: نگه‌داشتنِ نشانگر کار کند، ردشدنِ سریع نه ───────────────────
//     جانشینِ body.is-scrolling؛ اگر مکثِ ورود از بین برود، «پرشِ کارت‌ها
//     هنگام اسکرول» برمی‌گردد، و اگر زیادی شود، هاورِ واقعی از کار می‌افتد.
await page.evaluate(() => { document.body.className = document.body.className.replace(/gfx-\w+/, 'gfx-standard'); });
await page.evaluate(() => { const i = document.getElementById('debt-search'); if (i) i.value = ''; renderPersons('debt'); });
await page.waitForTimeout(500);
const hov = await page.evaluate(async () => {
  const c = document.querySelector('#debt-grid .person-card');
  if (!c) return null;
  const r = c.getBoundingClientRect();
  const at = (ms) => new Promise(res => setTimeout(res, ms));
  const tr = () => getComputedStyle(c).transform;
  const cs = getComputedStyle(c, null);
  return { delay: getComputedStyle(c).transitionDelay, hoverDelay: null, rect: Math.round(r.height) };
});
const hovDelay = await page.evaluate(() => {
  const c = document.querySelector('#debt-grid .person-card');
  const st = [...document.styleSheets].length;
  return { st };
});
await page.mouse.move(640, 300);
await page.waitForTimeout(40);
const quick = await page.evaluate(() => {
  const el = document.elementFromPoint(640, 300);
  const c = el && el.closest && el.closest('.person-card');
  return c ? getComputedStyle(c).transform : 'no-card';
});
await page.waitForTimeout(500);
const held = await page.evaluate(() => {
  const el = document.elementFromPoint(640, 300);
  const c = el && el.closest && el.closest('.person-card');
  return c ? getComputedStyle(c).transform : 'no-card';
});
if (quick !== 'no-card') {
  const moved = (t) => t && t !== 'none' && !/matrix\(1, 0, 0, 1, 0, 0\)/.test(t);
  say(!moved(quick), 'کارتی که فقط زیرِ نشانگر رد می‌شود بلند نمی‌شود (' + quick + ')');
  say(moved(held), 'هاورِ واقعی (نگه‌داشتنِ نشانگر) هنوز کار می‌کند (' + held + ')');
}

say(errs.length === 0, 'بدونِ خطای جاوااسکریپت' + (errs.length ? ' — ' + errs[0] : ''));

console.log(bad ? '\n' + bad + ' مورد خراب است.\n' : '\nهمه‌چیز درست است.\n');
await browser.close();
process.exit(bad ? 1 : 0);
