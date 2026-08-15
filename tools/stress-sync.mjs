// ---------------------------------------------------------------------------
//  نشستِ طولانی *با سرورِ واقعی* — همان چیزی که روی گوشی می‌افتد:
//  دستگاهِ دیگری مدام می‌نویسد و این دستگاه پشتِ سرِ هم «عکسِ کاملِ دیتابیس»
//  می‌گیرد و اعمال می‌کند.
// ---------------------------------------------------------------------------
import { spawn } from 'node:child_process';
import fsp from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { chromium } from 'playwright-core';
import { WebSocket } from 'ws';

const PORT = Number(process.env.PORT || 4799);
// مسیرِ سرورِ خانگی (مخزن server). با HLP_SERVER_ENTRY عوض می‌شود.
const PANEL_SERVER = process.env.HLP_SERVER_ENTRY
  || path.join(import.meta.dirname, '..', 'homelab-panel', 'server', 'src', 'index.js');
const ROUNDS = Number(process.env.ROUNDS || 40);
const tmp = await fsp.mkdtemp(path.join(os.tmpdir(), 'syncstress-'));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const child = spawn(process.execPath, ['--disable-warning=ExperimentalWarning',
  PANEL_SERVER], {
  env: { ...process.env, HLP_PORT: String(PORT), HLP_SITESYNC_PORT: '0', HLP_HOST: '127.0.0.1',
    HLP_DATA_DIR: path.join(tmp, 'data'), HLP_SITES_ROOT: path.join(tmp, 'sites'),
    HLP_TUNNEL: '0', HLP_AI_ENABLED: '0' }, stdio: 'ignore' });

let up = false;
for (let i = 0; i < 80 && !up; i++) { try { up = (await fetch(`http://127.0.0.1:${PORT}/health`)).ok; } catch {} if (!up) await sleep(250); }
if (!up) { console.log('❌ سرور بالا نیامد'); process.exit(1); }
const token = (await fsp.readFile(path.join(tmp, 'data', 'site-sync', 'token.txt'), 'utf8')).trim();

// ── دستگاهِ دوم: همان کاری که یک گوشیِ دیگر می‌کند ──
const other = new WebSocket(`ws://127.0.0.1:${PORT}/?token=${encodeURIComponent(token)}`);
await new Promise((r) => other.once('open', r));
let mid = 1;
const put = (p, v) => other.send(JSON.stringify({ op: 'set', id: mid++, path: p, value: v }));
const pending = new Map();
other.on('message', (raw) => {
  let m; try { m = JSON.parse(raw.toString()); } catch { return; }
  const r = pending.get(m.id); if (r) { pending.delete(m.id); r(m.value); }
});
const get = (p) => new Promise((res) => { const id = mid++; pending.set(id, res); other.send(JSON.stringify({ op: 'get', id, path: p })); });

// مسیرِ کرومیوم: اگر playwright خودش یکی دارد همان، وگرنه PW_CHROMIUM
const browser = await chromium.launch(
  process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {}
);
const page = await browser.newPage();
const errs = [];
page.on('pageerror', (e) => errs.push(String(e).slice(0, 200)));
await page.goto(`${pathToFileURL(path.join(import.meta.dirname, '..', 'index.html')).href}?server=${encodeURIComponent('ws://127.0.0.1:' + PORT)}&token=${encodeURIComponent(token)}`,
  { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(3500);

await page.evaluate(() => {
  const l = document.getElementById('lockScreen'); if (l) l.style.display = 'none';
  const a = document.getElementById('app'); if (a) a.style.display = '';
  try { currentRole = 'admin'; } catch (e) {}
  try { if (typeof seedDemoData === 'function' && !(DB.debtPersons || []).length) seedDemoData(1, { quiet: true }); } catch (e) {}
});
await page.waitForTimeout(4000);

const connected = await page.evaluate(() => {
  try { return { ready: !!(window.__pumpHome__ && _srvReady()), started: (typeof _srvListenerStarted !== 'undefined') ? _srvListenerStarted : 'n/a', startupDone: (typeof _srvStartupDone !== 'undefined') ? _srvStartupDone : 'n/a', code: (DB && DB.syncCode) || null, host: window.__SELF_HOST_URL__ || null }; }
  catch (e) { return { err: String(e) }; }
});
console.log('وضعیتِ اتصال:', JSON.stringify(connected));

const cdp = await page.context().newCDPSession(page);
await cdp.send('Performance.enable');
await cdp.send('HeapProfiler.enable');
async function metrics() {
  for (let i = 0; i < 3; i++) await cdp.send('HeapProfiler.collectGarbage').catch(() => {});
  await sleep(250);
  const m = await cdp.send('Performance.getMetrics');
  const g = (n) => (m.metrics.find((x) => x.name === n) || {}).value || 0;
  return { heapMB: +(g('JSHeapUsedSize') / 1048576).toFixed(1), nodes: g('Nodes'), listeners: g('JSEventListeners'), docs: g('Documents') };
}

// پاسخگو بودنِ نخِ اصلی — «فریز» یعنی همین عدد بالا برود
async function responsiveness() {
  const t0 = Date.now();
  await page.evaluate(() => new Promise((r) => setTimeout(r, 0)));
  return Date.now() - t0;
}

const base = await metrics();
console.log('پایه:', JSON.stringify(base), ' پاسخ‌دهی:', await responsiveness() + 'ms');

const SECTIONS = ['dashboard', 'debt', 'noinv', 'expenses', 'safe', 'invoices', 'settings'];
let worstLag = 0;

for (let round = 1; round <= ROUNDS; round++) {
  // دستگاهِ دوم عکسِ کاملِ ایستگاه را با شمارندهٔ بالاتر می‌نویسد — تنها چیزی که
  // برنامه واقعاً اعمالش می‌کند (_serverIsNewer). یعنی هر بار یک اعمالِ کامل.
  const snap = await get('stations/pump1');
  if (snap && typeof snap === 'object') {
    for (let i = 0; i < 8; i++) {
      snap._seq = (parseInt(snap._seq, 10) || 0) + 1;
      snap.updatedAt = Date.now();
      put('stations/pump1', snap);
      await sleep(60);
    }
  } else if (round === 1) console.log('   ⚠️ هنوز عکسی روی سرور نیست');
  await page.evaluate(async (sections) => {
    const s2 = (ms) => new Promise((r) => setTimeout(r, ms));
    for (const s of sections) { try { showSection(s); } catch (e) {} await s2(10); }
    try { toggleRobot(); await s2(30); toggleRobot(); } catch (e) {}
  }, SECTIONS);
  const lag = await responsiveness();
  if (round === 3) {
    const got = await page.evaluate(() => ({ seq: (DB && DB._seq) || null, started: (typeof _srvListenerStarted !== 'undefined') ? _srvListenerStarted : 'n/a' }));
    console.log('   ↳ آیا عکسِ سرور می‌رسد؟', JSON.stringify(got));
  }
  if (lag > worstLag) worstLag = lag;
  if (round % 10 === 0 || round === ROUNDS) {
    const m = await metrics();
    console.log(`دور ${String(round).padStart(3)}:`, JSON.stringify(m), ' پاسخ‌دهی:', lag + 'ms');
  }
}

const last = await metrics();
console.log('\n──────── خلاصه ────────');
console.log(`هیپ:            ${base.heapMB} MB → ${last.heapMB} MB`);
console.log(`گره‌های DOM:     ${base.nodes} → ${last.nodes}`);
console.log(`شنونده‌های زنده: ${base.listeners} → ${last.listeners}`);
console.log(`بدترین تاخیرِ نخِ اصلی: ${worstLag}ms`);
console.log(`خطاهای صفحه: ${errs.length}`);
if (errs.length) console.log([...new Set(errs)].slice(0, 8).join('\n'));

other.close(); await browser.close();
child.kill('SIGTERM'); await sleep(500); child.kill('SIGKILL');
await fsp.rm(tmp, { recursive: true, force: true });
