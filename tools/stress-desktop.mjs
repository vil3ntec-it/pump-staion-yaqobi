// ---------------------------------------------------------------------------
//  آزمونِ فشارِ برنامهٔ کامپیوتری — «با ده‌ها هزار حساب و ردیف هم لگ نزند»
//
//      node tools/stress-desktop.mjs
//      ROWS=50000 ACCOUNTS=10000 node tools/stress-desktop.mjs
//
//  فرقش با tools/bench-tables.mjs این است که آن‌جا یک کرومیومِ خالی باز
//  می‌شود؛ این‌جا خودِ پوستهٔ کامپیوتری (همان چیزی که کاربر نصب می‌کند) بالا
//  می‌آید و از بیرون با CDP رانده می‌شود. سه رزولوشنِ خواسته‌شده هم آزموده
//  می‌شوند تا «در تغییرِ اندازه چیزی نشکند» ادعا نماند و سنجیده شود:
//
//      1366×768 · 1920×1080 · 2560×1440
//
//  هر بار: باز کردنِ حسابِ بزرگ، ده گامِ اسکرول، یک جست‌وجو، مصرفِ حافظه،
//  خطاهای جاوااسکریپت، و سرریزِ افقیِ صفحه.
//  هیچ چیزی را عوض نمی‌کند و روی دادهٔ واقعیِ کاربر هم نمی‌نویسد (هر اجرا با
//  پوشهٔ دادهٔ موقتِ خودش بالا می‌آید).
// ---------------------------------------------------------------------------
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { chromium } from 'playwright-core';

const root = path.join(import.meta.dirname, '..');
const dir = path.join(root, 'desktop');
const bin = path.join(dir, 'node_modules', 'electron', 'dist', 'electron');
const ROWS = Number(process.env.ROWS || 50000);
const ACCOUNTS = Number(process.env.ACCOUNTS || 10000);
const SCREENS = [[1366, 768], [1920, 1080], [2560, 1440]];
const PORT = Number(process.env.CDP_PORT || 9333);

if (!fs.existsSync(bin)) { console.log('الکترون نصب نیست (npm i در پوشهٔ desktop).'); process.exit(1); }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const med = (a) => (a.length ? [...a].sort((x, y) => x - y)[Math.floor(a.length / 2)] : null);

async function runOne(w, h) {
  const userData = fs.mkdtempSync(path.join(os.tmpdir(), 'pump-stress-'));
  const useXvfb = !process.env.DISPLAY && fs.existsSync('/usr/bin/xvfb-run');
  const eArgs = ['--no-sandbox', `--remote-debugging-port=${PORT}`, `--user-data-dir=${userData}`, dir];
  const cmd = useXvfb ? 'xvfb-run' : bin;
  const args = useXvfb ? ['-a', '-s', `-screen 0 ${w}x${h}x24`, bin, ...eArgs] : eArgs;
  const proc = spawn(cmd, args, { cwd: dir, detached: true, env: { ...process.env } });
  const killAll = (sig) => { try { process.kill(-proc.pid, sig); } catch (e) { try { proc.kill(sig); } catch (e2) {} } };

  let browser = null, out = { screen: `${w}×${h}` };
  try {
    // انتظار برای بالا آمدنِ درگاهِ اشکال‌زدایی
    for (let i = 0; i < 60 && !browser; i++) {
      await sleep(500);
      try { browser = await chromium.connectOverCDP(`http://127.0.0.1:${PORT}`); } catch (e) {}
    }
    if (!browser) throw new Error('به پوسته وصل نشد');
    let page = null;
    for (let i = 0; i < 40 && !page; i++) {
      const pages = browser.contexts().flatMap((c) => c.pages());
      page = pages.find((pg) => /index\.html/.test(pg.url())) || null;
      if (!page) await sleep(500);
    }
    if (!page) throw new Error('صفحهٔ برنامه پیدا نشد');

    const errs = [];
    page.on('pageerror', (e) => errs.push(String(e).slice(0, 160)));
    await page.waitForFunction(() => typeof window.showSection === 'function', null, { timeout: 40000 });

    await page.evaluate(({ rows, accounts }) => {
      const l = document.getElementById('lockScreen'); if (l) l.style.display = 'none';
      const a = document.getElementById('app'); if (a) a.style.display = '';
      try { currentRole = 'admin'; } catch (e) {}
      const today = persianDate();
      const big = [];
      for (let i = 0; i < rows; i++) big.push({ date: today, name: 'r' + i, hawala: 'h' + i,
        ftype: (i % 3 === 0) ? 'diesel' : 'petrol', fuel: (i % 20) + 1, priceper: 60,
        bardagi: 0, rasid: (i % 5) * 10, rasidFuel: 0 });
      DB.debtPersons = [{ id: 'benchP', name: 'حسابِ بزرگ', phone: '', rows: big, subs: [], mode: 'fuel' }];
      for (let i = 1; i < accounts; i++) DB.debtPersons.push({ id: 'p' + i, name: 'حساب شمارهٔ ' + i, phone: '',
        rows: [{ date: today, name: 'x', hawala: 'h', ftype: 'petrol', fuel: 10, priceper: 60, bardagi: 0, rasid: 0, rasidFuel: 0 }],
        subs: [], mode: 'fuel' });
    }, { rows: ROWS, accounts: ACCOUNTS });

    // فهرستِ حساب‌ها
    out.listMs = await page.evaluate(() => { const t = performance.now(); showSection('debt'); renderPersons('debt'); return Math.round(performance.now() - t); });
    await sleep(500);

    // باز کردنِ حسابِ بزرگ (اجرای اول سرد است و همان را هم گزارش می‌کنیم)
    const opens = [];
    for (let i = 0; i < 3; i++) {
      opens.push(await page.evaluate(() => { const t = performance.now(); openPersonModal('debt', 'benchP'); return performance.now() - t; }));
      await sleep(400);
      if (i === 0) out.openColdMs = Math.round(opens[0]);
      await page.evaluate(() => { const m = document.getElementById('personModal'); if (m) m.classList.remove('open'); });
      await sleep(200);
    }
    out.openWarmMs = Math.round(med(opens.slice(1)));

    // اسکرولِ جدولِ بزرگ
    await page.evaluate(() => openPersonModal('debt', 'benchP'));
    await sleep(500);
    out.scrollStepMs = await page.evaluate(() => {
      const sc = document.querySelector('#personModal .modal-body'); if (!sc) return null;
      const t = performance.now();
      for (let k = 1; k <= 10; k++) { sc.scrollTop = k * 4000; sc.dispatchEvent(new Event('scroll')); }
      return Math.round((performance.now() - t) / 10 * 10) / 10;
    });
    await page.evaluate(() => { const m = document.getElementById('personModal'); if (m) m.classList.remove('open'); });
    await sleep(300);

    // جست‌وجو در فهرستِ حساب‌ها
    out.searchMs = await page.evaluate(() => {
      const inp = document.querySelector('#sec-debt input[type=text], #sec-debt input[type=search]');
      if (!inp) return null;
      const t = performance.now();
      inp.value = 'شمارهٔ 7777'; inp.dispatchEvent(new Event('input', { bubbles: true }));
      return Math.round(performance.now() - t);
    });
    await sleep(400);

    const st = await page.evaluate(() => ({
      heapMB: performance.memory ? Math.round(performance.memory.usedJSHeapSize / 1048576) : null,
      nodes: document.getElementsByTagName('*').length,
      overflowX: document.documentElement.scrollWidth > document.documentElement.clientWidth + 2,
    }));
    Object.assign(out, st);
    out.errs = errs.slice(0, 3);
  } catch (e) {
    out.error = String(e && e.message || e).slice(0, 160);
  } finally {
    try { if (browser) await browser.close(); } catch (e) {}
    killAll('SIGKILL');
    await sleep(1200);
    try { fs.rmSync(userData, { recursive: true, force: true }); } catch (e) {}
  }
  return out;
}

console.log(`\n── فشارِ برنامهٔ کامپیوتری: ${ROWS.toLocaleString('fa')} ردیف در یک حساب، ${ACCOUNTS.toLocaleString('fa')} حساب ──\n`);
let bad = 0;
for (const [w, h] of SCREENS) {
  const r = await runOne(w, h);
  if (r.error) { bad++; console.log(`  ${r.screen}  ❌ ${r.error}`); continue; }
  const problems = [];
  if (r.errs && r.errs.length) problems.push('خطای جاوااسکریپت: ' + r.errs[0]);
  if (r.overflowX) problems.push('سرریزِ افقیِ صفحه');
  if (r.scrollStepMs != null && r.scrollStepMs > 16) problems.push('گامِ اسکرول از ۱۶ms بیشتر');
  if (problems.length) bad++;
  console.log(`  ${r.screen}`);
  console.log(`     فهرستِ حساب‌ها ${r.listMs}ms · باز کردنِ حساب (سرد) ${r.openColdMs}ms · (گرم) ${r.openWarmMs}ms`);
  console.log(`     هر گامِ اسکرول ${r.scrollStepMs}ms · جست‌وجو ${r.searchMs}ms · حافظه ${r.heapMB}MB · گره‌های صفحه ${r.nodes}`);
  console.log(`     ${problems.length ? '❌ ' + problems.join(' · ') : '✅ بدونِ خطا، بدونِ سرریز، اسکرول زیرِ یک فریم'}`);
}
console.log('');
process.exit(bad ? 1 : 0);
