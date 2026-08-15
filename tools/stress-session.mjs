// ---------------------------------------------------------------------------
//  آزمونِ نشستِ طولانی — همان کاری که کاربر در چند ساعت می‌کند، فشرده
//  اندازه‌گیری: هیپِ JS، تعدادِ گره‌های DOM، گره‌های جدا افتاده (detached)،
//  شنونده‌های رویداد، و تایمرهای زنده.
// ---------------------------------------------------------------------------
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';

const PAGE = pathToFileURL(path.join(import.meta.dirname, '..', 'index.html')).href;
const ROUNDS = Number(process.env.ROUNDS || 30);

const SECTIONS = ['dashboard', 'shifts', 'storage', 'tanker', 'debt', 'debtsum', 'chakana',
  'noinv', 'expenses', 'rasid', 'profit', 'safe', 'sarrafi', 'invoices', 'history',
  'attendance', 'monthreport', 'settings'];

// مسیرِ کرومیوم: اگر playwright خودش یکی دارد همان، وگرنه PW_CHROMIUM
const browser = await chromium.launch(
  process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {}
);
const page = await browser.newPage();
const errs = [];
page.on('pageerror', (e) => errs.push(String(e).slice(0, 200)));

await page.goto(PAGE, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(3000);

// قفل را مثل تستِ دستیِ توضیح‌دادهٔ CLAUDE.md باز می‌کنیم
await page.evaluate(() => {
  const l = document.getElementById('lockScreen'); if (l) l.style.display = 'none';
  const a = document.getElementById('app'); if (a) a.style.display = '';
  try { currentRole = 'admin'; } catch (e) {}
  // دادهٔ نمونه تا صفحه‌ها واقعاً چیزی برای رسم داشته باشند
  try { if (typeof seedDemoData === 'function' && !(DB.debtPersons || []).length) seedDemoData(1, { quiet: true }); } catch (e) {}
});
await page.waitForTimeout(1500);

const cdp = await page.context().newCDPSession(page);
await cdp.send('Performance.enable');

async function metrics() {
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  // سه بار جمع‌آوریِ زباله تا چیزی که واقعاً آزاد می‌شود در شمارش نماند
  for (let i = 0; i < 3; i++) { await cdp.send('HeapProfiler.collectGarbage').catch(() => {}); }
  await page.waitForTimeout(250);
  const m = await cdp.send('Performance.getMetrics');
  const get = (n) => (m.metrics.find((x) => x.name === n) || {}).value || 0;
  const extra = await page.evaluate(() => ({
    intervals: (window.__timerProbe && window.__timerProbe.liveIntervals()) || -1,
    timeouts: (window.__timerProbe && window.__timerProbe.liveTimeouts()) || -1,
    listeners: (window.__listenerProbe && window.__listenerProbe.count()) || -1,
  }));
  return {
    heapMB: +(get('JSHeapUsedSize') / 1048576).toFixed(1),
    nodes: get('Nodes'),
    liveListeners: get('JSEventListeners'),   // شنونده‌های واقعاً زنده (از خودِ کروم)
    docs: get('Documents'),
    registered: extra.listeners,              // ثبت منهای حذف — «زنده» نیست
    intervals: extra.intervals,
    timeouts: extra.timeouts,
  };
}

await cdp.send('HeapProfiler.enable');

// ردیابِ تایمرها و شنونده‌ها — فقط برای اندازه‌گیری، هیچ رفتاری عوض نمی‌شود
await page.evaluate(() => {
  const liveI = new Set(), liveT = new Set();
  const si = window.setInterval, ci = window.clearInterval;
  const st = window.setTimeout, ct = window.clearTimeout;
  window.setInterval = function (...a) { const id = si.apply(this, a); liveI.add(id); return id; };
  window.clearInterval = function (id) { liveI.delete(id); return ci.call(this, id); };
  window.setTimeout = function (fn, ms, ...a) {
    const id = st.call(this, function () { liveT.delete(id); try { fn && fn.apply(this, arguments); } catch (e) { throw e; } }, ms, ...a);
    liveT.add(id); return id;
  };
  window.clearTimeout = function (id) { liveT.delete(id); return ct.call(this, id); };
  window.__timerProbe = { liveIntervals: () => liveI.size, liveTimeouts: () => liveT.size };

  let n = 0;
  const ael = EventTarget.prototype.addEventListener;
  const rel = EventTarget.prototype.removeEventListener;
  EventTarget.prototype.addEventListener = function (...a) { n++; return ael.apply(this, a); };
  EventTarget.prototype.removeEventListener = function (...a) { n--; return rel.apply(this, a); };
  window.__listenerProbe = { count: () => n };
});

const base = await metrics();
console.log('پایه (قبل از کار):', JSON.stringify(base));

const samples = [];
for (let round = 1; round <= ROUNDS; round++) {
  await page.evaluate(async (sections) => {
    const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
    for (const s of sections) {
      try { showSection(s); } catch (e) {}
      await sleep(12);
    }
    // مودالِ شخص: سنگین‌ترین صفحهٔ برنامه — باز و بسته
    try {
      const p = (DB.debtPersons || [])[0];
      if (p && typeof openPersonModal === 'function') { openPersonModal(p.id); await sleep(60); closeModal('personModal'); }
    } catch (e) {}
    // ربات (چت) باز و بسته
    try { toggleRobot(); await sleep(50); toggleRobot(); } catch (e) {}
    // جست‌وجو
    try {
      const box = document.getElementById('debtSearch') || document.querySelector('input[oninput*="earch"]');
      if (box) { box.value = 'احمد'; box.dispatchEvent(new Event('input', { bubbles: true })); await sleep(40); box.value = ''; box.dispatchEvent(new Event('input', { bubbles: true })); }
    } catch (e) {}
    // تمِ روز/شب
    try { toggleTheme(); await sleep(30); toggleTheme(); } catch (e) {}
    await sleep(30);
  }, SECTIONS);

  if (round % 5 === 0 || round === ROUNDS) {
    const m = await metrics();
    samples.push({ round, ...m });
    console.log(`دور ${String(round).padStart(3)}:`, JSON.stringify(m));
  }
}

const last = samples[samples.length - 1];
console.log('\n──────── خلاصه ────────');
console.log(`هیپ:        ${base.heapMB} MB → ${last.heapMB} MB   (${(last.heapMB - base.heapMB >= 0 ? '+' : '')}${(last.heapMB - base.heapMB).toFixed(1)})`);
console.log(`گره‌های DOM: ${base.nodes} → ${last.nodes}   (${last.nodes - base.nodes >= 0 ? '+' : ''}${last.nodes - base.nodes})`);
console.log(`شنونده‌های زنده: ${base.liveListeners} → ${last.liveListeners}   (${last.liveListeners - base.liveListeners >= 0 ? '+' : ''}${last.liveListeners - base.liveListeners})`);
console.log(`interval زنده: ${base.intervals} → ${last.intervals}`);
console.log(`timeout زنده:  ${base.timeouts} → ${last.timeouts}`);
console.log(`خطاهای صفحه: ${errs.length}`);
if (errs.length) console.log(errs.slice(0, 6).join('\n'));

await browser.close();
