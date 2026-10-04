/**
 * ══ دو حساب، همهٔ اجزا — هیچ‌چیز قاطی نشود (۱۴۰۵/۰۷/۱۴) ═══════════════════
 *
 * خواستهٔ صاحب سامانه: «ببین برنامهٔ کامپیوتر، اپِ اندروید، سایت، ربات‌ها،
 * تلگرام و سرور همه یکی‌اند یا نه… هر حسابِ کاربری جداست و هر کس باید
 * حساب‌های خودش را ببیند… سرور همه را در یک فولدر نگه می‌دارد یا برای هر کدام
 * یک فولدر؟»
 *
 * پیش‌نیاز (پشتهٔ واقعی — پنلِ خانگی + سرورِ حساب + صندوقِ ایمیل):
 *   node test/signup-stack.mjs live.json                                  (ریپوی server)
 *   PUMP_KAR_TAG=A dotnet run … -- oldacct live.json <پوشه> karcode real
 *   PUMP_KAR_TAG=B dotnet run … -- oldacct live.json <پوشه> karcode real
 *
 *   node tools/check-multi-account.mjs live.json <پوشه>
 *
 * هر دو حساب با **خودِ برنامهٔ کامپیوتر** ساخته شده‌اند، هر کدام قرض‌دارِ
 * نام‌دارِ خودش را دارد، و این‌جا از هر درِ ممکن سنجیده می‌شود که نامِ حسابِ
 * دیگر هیچ‌جا دیده نشود: پوشهٔ سرورِ خانگی، رمزِ خواندن، کدِ هشت‌رقمی، عکسِ
 * سرورِ حساب، دفترِ همگام‌سازی، و خودِ اپِ گوشی (کرومیوم) — با جابه‌جا شدن
 * از پمپِ A به پمپِ B روی **همان** گوشی.
 */
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const [liveFile, shots] = process.argv.slice(2);
const live = JSON.parse(fs.readFileSync(liveFile, 'utf8'));
const PUB = live.public.replace(/\/$/, '');
const acc = Object.fromEntries(['A', 'B'].map((t) =>
  [t, JSON.parse(fs.readFileSync(path.join(shots, `karcode-${t}.json`), 'utf8'))]));
const other = { A: 'B', B: 'A' };

let bad = 0;
const ok = (c, m, d = '') => { if (!c) bad++; console.log((c ? '  ✓ ' : '  ✗ ') + m + (d ? ` — ${d}` : '')); };
const fa = (s) => s.replace(/[0-9]/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[d]);
const has = (text, t) => text.includes(acc[t].debtor);
const get = async (p, headers = {}) => {
  const r = await fetch(PUB + p, { headers: { 'x-app-id': 'tohid-pump-app', ...headers } });
  return { status: r.status, text: await r.text() };
};
const post = async (p, body) => {
  const r = await fetch(PUB + p, {
    method: 'POST', headers: { 'content-type': 'application/json', 'x-app-id': 'tohid-pump-app' }, body: JSON.stringify(body),
  });
  return { status: r.status, text: await r.text() };
};

// ── ۱) سرورِ حساب: هر کد ⇒ پمپِ خودش، پوشهٔ خودش، رمزِ خودش ─────────────
console.log('══ ۱) سرورِ حساب — کدِ هشت‌رقمی');
const join = {};
for (const t of ['A', 'B']) {
  const j = await post('/api/pump/public/join', { code: acc[t].code });
  join[t] = JSON.parse(j.text);
  ok(j.status === 200 && join[t].station?.code === acc[t].station,
    `کدِ ${t} ⇒ پمپِ خودِ ${t}`, `${join[t].station?.code} ⇐ ${acc[t].station}`);
}
ok(acc.A.station !== acc.B.station, 'دو حساب، دو پمپِ جدا روی سرورِ حساب', `${acc.A.station} · ${acc.B.station}`);
ok(acc.A.code !== acc.B.code, 'دو کدِ هشت‌رقمیِ جدا', `${acc.A.display} · ${acc.B.display}`);
ok(join.A.home?.station && join.A.home.station !== join.B.home?.station,
  'دو پوشهٔ جدا روی سرورِ خانگی', `${join.A.home?.station} · ${join.B.home?.station}`);
for (const t of ['A', 'B']) {
  const h = join[t].home?.station || '';
  ok(h === acc[t].station || h.startsWith(acc[t].station + '-'),
    `پوشهٔ سرورِ خانگیِ ${t} به نامِ پمپِ خودِ حساب است، نه کدِ این کامپیوتر`, `${h} ⇐ ${acc[t].station}`);
}
ok(join.A.home?.readKey && join.A.home.readKey !== join.B.home?.readKey, 'دو رمزِ خواندنِ جدا');
for (const t of ['A', 'B']) {
  const l = await get('/api/pump/public/live?code=' + acc[t].code);
  ok(l.status === 200 && has(l.text, t) && !has(l.text, other[t]),
    `عکسِ زندهٔ کدِ ${t} فقط قرض‌دارِ ${t} را دارد`);
}
ok((await post('/api/pump/public/join', { code: '99999999' })).status === 404, 'کدِ ساختگی ⇒ ۴۰۴، نه پمپِ کسی');

// ── ۲) سرورِ خانگی: هر حساب یک پوشه روی دیسک ───────────────────────────
console.log('══ ۲) سرورِ خانگی — پوشه‌ها روی دیسک');
const root = path.join(live.tmp, 'data', 'stations');
const dirs = fs.readdirSync(root).filter((d) => fs.statSync(path.join(root, d)).isDirectory());
for (const t of ['A', 'B']) {
  const d = join[t].home.station;
  const f = path.join(root, d, 'live.json');
  const txt = fs.existsSync(f) ? fs.readFileSync(f, 'utf8') : '';
  ok(dirs.includes(d), `پوشهٔ ${t} هست: data/stations/${d}/`);
  ok(has(txt, t) && !has(txt, other[t]), `live.jsonِ پوشهٔ ${t} فقط دادهٔ ${t} را دارد`);
}
for (const [s, k, t] of [[join.A.home.station, join.A.home.readKey, 'A'], [join.B.home.station, join.B.home.readKey, 'B']]) {
  const mine = await get(`/api/stations/${encodeURIComponent(s)}/live?token=${k}`);
  ok(mine.status === 200 && has(mine.text, t), `رمزِ خواندنِ ${t} پوشهٔ خودش را باز می‌کند`);
  const o = join[other[t]].home.station;
  const theirs = await get(`/api/stations/${encodeURIComponent(o)}/live?token=${k}`);
  ok(theirs.status === 404 && !has(theirs.text, other[t]), `⛔ رمزِ ${t} پوشهٔ ${other[t]} را باز نمی‌کند`, String(theirs.status));
  const w = await post(`/api/stations/${encodeURIComponent(o)}/chat?token=${k}`, { text: 'نفوذ', from: t });
  ok(w.status === 404, `⛔ رمزِ ${t} در گروهِ کارکنانِ ${other[t]} نمی‌نویسد`, String(w.status));
}

// ── ۳) دفترِ همگام‌سازی روی سرورِ حساب ─────────────────────────────────
console.log('══ ۳) دفترِ همگام‌سازی (Sync v1)');
for (const t of ['A', 'B']) {
  if (!acc[t].access) { ok(false, `توکنِ ${t} در فایلِ سنجه نیست`); continue; }
  const r = await get(`/api/sync/v1/pull?device_id=audit-${t}&since=0&limit=500`, { authorization: 'Bearer ' + acc[t].access });
  const n = (JSON.parse(r.text).ops || []).length;
  ok(r.status === 200 && has(r.text, t), `دفترِ ${t} قرض‌دارِ خودش را دارد (${n} تغییر)`);
  ok(!has(r.text, other[t]), `⛔ و هیچ تغییری از ${other[t]} ندارد`);
  const spoof = await get(`/api/sync/v1/pull?device_id=audit-${t}&since=0&limit=500&account=${acc[other[t]].station}`,
    { authorization: 'Bearer ' + acc[t].access });
  ok(!has(spoof.text, other[t]), `⛔ ‎?account=‎ِ دستی هم دفترِ ${other[t]} را باز نمی‌کند`);
}

// ── ۴) اپِ گوشی — یک گوشی، اول پمپِ A، بعد پمپِ B ─────────────────────
console.log('══ ۴) اپِ گوشی (کرومیوم) — A ⇒ «پمپِ دیگر» ⇒ B');
const globalRoot = execSync('npm root -g').toString().trim();
const { chromium } = createRequire(import.meta.url)(globalRoot + '/playwright');
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const srv = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(ROOT, p);
  if (!f.startsWith(ROOT) || !fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  const ext = path.extname(f);
  res.setHeader('content-type', ext === '.js' ? 'text/javascript' : ext === '.html' ? 'text/html; charset=utf-8' : 'application/octet-stream');
  res.end(fs.readFileSync(f));
});
await new Promise((r) => srv.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${srv.address().port}/kar/`;
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 } });
//  ⛔ نه پاسخِ ساختگی: همان درخواست به سرورِ حسابِ واقعیِ پشته
await ctx.route('https://api.vill3n.top/**', async (route) => {
  const req = route.request();
  const u = new URL(req.url());
  const r = await fetch(PUB + u.pathname + u.search, {
    method: req.method(), headers: { 'content-type': 'application/json', 'x-app-id': req.headers()['x-app-id'] || '' },
    body: ['GET', 'HEAD'].includes(req.method()) ? undefined : req.postData() || undefined,
  });
  await route.fulfill({ status: r.status, body: Buffer.from(await r.arrayBuffer()),
    headers: { 'content-type': r.headers.get('content-type') || 'application/json', 'access-control-allow-origin': '*' } });
});
const page = await ctx.newPage();
const errors = [];
page.on('pageerror', (e) => errors.push(String(e)));
const vis = (id) => page.evaluate((id) => { const e = document.getElementById(id); return !!e && !e.classList.contains('hidden'); }, id);
const staffText = () => page.evaluate(() => (document.getElementById('staffList') || {}).textContent || '');
const storage = () => page.evaluate(() => JSON.stringify(localStorage));

async function joinAs(t) {
  await page.fill('#inCode', fa(acc[t].code));
  await page.click('#btnJoin');
  await page.waitForFunction(() => !document.getElementById('appPane').classList.contains('hidden'), null, { timeout: 60000 }).catch(() => {});
  ok(await vis('appPane'), `کدِ ${t} ⇒ برنامه باز شد (پمپِ بی‌رمز)`);
  await page.click('.door.staff').catch(() => {});
  await page.waitForFunction((n) => (document.getElementById('staffList') || {}).textContent?.includes(n), acc[t].debtor, { timeout: 30000 }).catch(() => {});
  const txt = await staffText();
  ok(has(txt, t), `قرض‌دارِ ${t} دیده می‌شود`);
  ok(!has(txt, other[t]), `⛔ قرض‌دارِ ${other[t]} دیده نمی‌شود`);
  await page.screenshot({ path: path.join(shots, `multi-${t}.png`) });
}

await page.goto(base);
await joinAs('A');
ok(await vis('btnOther'), '⛔ دکمهٔ «پمپِ دیگر» در خودِ برنامه دیده می‌شود (پمپِ بی‌رمز صفحهٔ قفل ندارد)');
await page.click('#btnOther');
//  ⇄ از ۱۴۰۵/۰۷/۱۶ پیش از خروج می‌پرسد (‎askLeave‎) — سنجه تا امروز تأییدش نمی‌کرد و همین‌جا می‌ماسید
await page.waitForSelector('#dlgOk', { state: 'visible', timeout: 5000 }).catch(() => {});
await page.click('#dlgOk').catch(() => {});
await page.waitForFunction(() => !document.getElementById('codePane').classList.contains('hidden'), null, { timeout: 10000 }).catch(() => {});
ok(await vis('codePane'), '«پمپِ دیگر» ⇒ دوباره صفحهٔ کد');
ok(!(await storage()).includes(acc.A.debtor), '⛔ هیچ ردی از دادهٔ A در حافظهٔ گوشی نماند');
await joinAs('B');
await page.reload();
await page.waitForFunction(() => !document.getElementById('appPane').classList.contains('hidden'), null, { timeout: 30000 }).catch(() => {});
await page.click('.door.staff').catch(() => {});
await page.waitForFunction((n) => (document.getElementById('staffList') || {}).textContent?.includes(n), acc.B.debtor, { timeout: 30000 }).catch(() => {});
const after = await staffText();
ok(has(after, 'B') && !has(after, 'A'), 'باز شدنِ دوباره ⇒ هنوز فقط پمپِ B');
ok(!(await storage()).includes(acc.A.debtor), '⛔ و حافظهٔ گوشی هیچ دادهٔ A ندارد');

// ── ۵) شورا چ۴ — «پمپ‌هایم»: همان گوشی، A و B کنارِ هم، هیچ قاطی ──────────
console.log('══ ۵) اپِ گوشی — «🗂 پمپ‌هایم» با هر دو پمپ');
await page.click('#btnPumpsTop');
await page.click('#btnPumpsAdd');
await joinAs('A');
await page.click('#btnPumpsTop');
await page.waitForFunction(() => document.querySelectorAll('.pump-card .fig').length >= 4, null, { timeout: 60000 }).catch(() => {});
const mine = await page.evaluate(() => (document.getElementById('pumpsList') || {}).textContent || '');
ok(await page.evaluate(() => document.querySelectorAll('.pump-card').length) === 2, 'هر دو پمپ در «پمپ‌هایم»، هر کدام کارتِ خودش');
ok(!has(mine, 'A') && !has(mine, 'B'), '⛔ نامِ هیچ قرض‌داری در «پمپ‌هایم» نیست');
for (const t of ['A', 'B']) {
  const c = await page.evaluate((stn) => {
    const e = document.querySelector('.pump-card[data-key="' + stn + '"]'); return e ? e.textContent : '';
  }, acc[t].station);
  ok(c.length > 0 && !c.includes('در حالِ خواندن'), `کارتِ ${t} از عکسِ ابریِ خودِ ${t} پر شد`);
}
ok(!(await storage()).includes(acc.B.debtor), '⛔ پس از دیدنِ «پمپ‌هایم»، حافظهٔ گوشی هیچ دادهٔ B ندارد (A باز است)');
await page.click('#btnPumpsBack');

ok(errors.length === 0, 'هیچ خطای جاوااسکریپت', errors.join(' | '));
await browser.close();
srv.close();

console.log(bad ? `❌ ${bad} ایراد` : '✅ دو حساب از هر در جدا ماندند');
process.exit(bad ? 1 : 0);
