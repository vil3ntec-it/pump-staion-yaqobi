/* ══ شورا، چ۴ — «پمپ‌هایم»: یک گوشی، دو پمپ، هیچ قاطی ══════════════════════
 *
 *     node tools/check-kar-pumps.mjs
 *
 * در کرومیومِ بی‌پنجره، با ابرِ ساختگی (‎page.route‎) و دو پمپِ جدا:
 *   کدِ A ⇒ ➕ پمپِ دیگر ⇒ کدِ B ⇒ «📊 همهٔ پمپ‌هایم» ⇒ هر کارت عددِ خودِ
 *   همان پمپ ⇒ رمزِ B روی کارتِ A رد ⇒ رمزِ A فقط کارتِ A را باز می‌کند ⇒
 *   باز کردنِ A از فهرست ⇒ فقط قرض‌دارِ A ⇒ ⇄ خروج ⇒ A از فهرست بیرون.
 * و در هر گام: ⛔ نامِ هیچ قرض‌داری در نمای «همهٔ پمپ‌ها» نیست، و حافظهٔ گوشی
 * دادهٔ پمپی را که باز نیست ندارد.
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

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

const gateOf = (pass) => {
  const salt = crypto.randomBytes(16), iter = 1000;
  const hash = crypto.pbkdf2Sync(pass, salt, iter, 32, 'sha256');
  return `pbkdf2$sha256$${iter}$${salt.toString('base64')}$${hash.toString('base64')}`;
};
const P = {
  A: { code: '11112222', stn: 'ac-a', name: 'پمپِ الف', pass: 'alef1', debtor: 'هارونِ‌الف', tank: 7000, owed: '91,111' },
  B: { code: '33334444', stn: 'ac-b', name: 'پمپِ ب', pass: 'beh22', debtor: 'کریمِ‌به', tank: 1200, owed: '42,222' },
};
for (const t of ['A', 'B']) {
  const p = P[t];
  p.snap = {
    v: 1, seq: 5, at: '1405/07/20', gate: gateOf(p.pass), station: { name: p.name },
    banner: [['قرض کل', p.owed, 'danger'], ['مفاد امروز', t === 'A' ? '1,234' : '5,678', 'ok']],
    tank: { petrol: { show: p.tank, low: t === 'B' }, diesel: { show: 300 } },
    debtors: [{ id: 1, name: p.debtor, status: t === 'A' ? 'ok' : 'out', bal: { money: 0, petrol: 0, diesel: 0 }, accounts: [] }],
    alerts: [], sections: {},
  };
}
//  ‎SEALED=1‎: همان عکس به شکلِ برنامهٔ ۳.۱.۲۶۰ به بعد (‎OwnerSeal.cs‎) — نوار و
//  بخش‌ها فقط رمزشده، ‎gate‎ بی هش. همهٔ بندهای پایین باید همان‌طور سبز بمانند.
const SEALED = process.env.SEALED === '1';
if (SEALED) for (const p of Object.values(P)) {
  const [a, b, iter, salt] = p.snap.gate.split('$');
  const dk = crypto.pbkdf2Sync(p.pass, Buffer.from(salt, 'base64'), +iter, 32, 'sha256');
  const key = crypto.createHash('sha256').update(Buffer.concat([Buffer.from('pump-owner-seal-v1'), dk])).digest();
  const iv = crypto.randomBytes(12);
  const c = crypto.createCipheriv('aes-256-gcm', key, iv);
  const pt = Buffer.from(JSON.stringify({ sections: p.snap.sections, banner: p.snap.banner }));
  const ct = Buffer.concat([c.update(pt), c.final(), c.getAuthTag()]);
  delete p.snap.sections; delete p.snap.banner;
  p.snap.gate = [a, b, iter, salt].join('$');
  p.snap.seal = { v: 1, iv: iv.toString('base64'), ct: ct.toString('base64') };
}
const byCode = (c) => Object.values(P).find((p) => p.code === c);

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());
const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error' && !/401|404|net::ERR_/.test(m.text())) errors.push('console: ' + m.text()); });

const cors = { 'access-control-allow-origin': '*' };
await page.route('https://api.vill3n.top/**', async (route) => {
  const u = new URL(route.request().url());
  const body = route.request().postDataJSON?.() || {};
  if (u.pathname.endsWith('/api/pump/public/join')) {
    const p = byCode(body.code);
    if (!p) return route.fulfill({ status: 404, contentType: 'application/json', headers: cors, body: '{"error":{"code":"bad_access_code"}}' });
    return route.fulfill({ status: 200, contentType: 'application/json', headers: cors,
      body: JSON.stringify({ ok: true, station: { code: p.stn, name: p.name, accessCode: p.code }, home: { url: '', readKey: '', station: p.stn }, cloudLiveAt: 5 }) });
  }
  if (u.pathname.endsWith('/api/pump/public/live')) {
    const p = byCode(u.searchParams.get('code'));
    if (!p) return route.fulfill({ status: 404, contentType: 'application/json', headers: cors, body: '{}' });
    //  پمپِ A عمداً دیرتر جواب می‌دهد — جوابِ دیررس نباید روی کارتِ دیگر بنشیند
    if (p === P.A) await new Promise((r) => setTimeout(r, 300));
    return route.fulfill({ status: 200, contentType: 'application/json', headers: cors,
      body: JSON.stringify({ ok: true, updatedAt: Date.now(), live: p.snap }) });
  }
  return route.fulfill({ status: 404, headers: cors, body: '{}' });
});
await page.route(/\/kar\/version\.json/, (r) => r.fulfill({ status: 200, contentType: 'application/json', body: '{"v":"1"}' }));

let bad = 0;
const ok = (c, m, d = '') => { if (!c) bad++; console.log((c ? '✓ ' : '✗ ') + m + (d ? ` — ${d}` : '')); };
const vis = (id) => page.evaluate((id) => { const e = document.getElementById(id); return !!e && !e.classList.contains('hidden'); }, id);
const text = (id) => page.evaluate((id) => (document.getElementById(id) || {}).textContent || '', id);
const storage = () => page.evaluate(() => JSON.stringify(localStorage));
const fa = (s) => String(s).replace(/[0-9]/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[d]);
const card = (t) => `.pump-card[data-key="${P[t].stn}"]`;

async function join(t) {
  await page.fill('#inCode', P[t].code);
  await page.click('#btnJoin');
  await page.waitForFunction((n) => !document.getElementById('appPane').classList.contains('hidden')
    && (document.getElementById('stName').textContent || '').includes(n), P[t].name, { timeout: 10000 }).catch(() => {});
  ok(await vis('appPane') && (await text('stName')).includes(P[t].name), `کدِ ${t} ⇒ پمپِ ${t} باز شد`);
}
async function staffSees(t) {
  await page.click('.door.staff');
  await page.waitForFunction((n) => (document.getElementById('staffList') || {}).textContent.includes(n), P[t].debtor, { timeout: 8000 }).catch(() => {});
  const s = await text('staffList');
  const o = t === 'A' ? 'B' : 'A';
  ok(s.includes(P[t].debtor) && !s.includes(P[o].debtor), `کارمندان: فقط قرض‌دارِ ${t}`);
}

await page.goto(base);
await join('A');
ok(!(await vis('btnPumps')), 'با یک پمپ، «همهٔ پمپ‌ها» پنهان است');
ok(await vis('btnAddPump'), '«➕ پمپِ دیگر» دیده می‌شود');

await page.click('#btnAddPump');
ok(await vis('codePane') && await vis('btnCodePumps'), '➕ ⇒ صفحهٔ کد، با «برگشت به پمپ‌هایم»');
await join('B');
await staffSees('B');
ok(!(await storage()).includes(P.A.debtor), '⛔ حافظهٔ گوشی هیچ دادهٔ A ندارد (B باز است)');
const list = JSON.parse(await page.evaluate(() => localStorage.getItem('pumpKar.v1.pumps') || '[]'));
ok(list.length === 2 && list.some((p) => p.stn === 'ac-a') && list.some((p) => p.stn === 'ac-b'),
  'فهرستِ پمپ‌ها هر دو را دارد', JSON.stringify(list));
ok(list.every((p) => Object.keys(p).sort().join() === 'code,name,stn'), '⛔ فهرست فقط کد و نام دارد — نه عکس، نه رمز');

ok(await vis('btnPumpsTop'), '🗂 «پمپ‌هایم» در سربرگ، از هر صفحه');
ok(/[2۲]/.test(await text('btnPumps')) && !(await page.$eval('#btnPumps', (e) => e.classList.contains('hidden'))),
  'و درِ «📊 همهٔ پمپ‌هایم (۲)» روی خانه آماده است');
await page.click('#btnPumpsTop');
await page.waitForFunction(() => document.querySelectorAll('.pump-card .fig').length >= 4, null, { timeout: 10000 }).catch(() => {});
ok(await vis('pumpsPane'), 'نمای «پمپ‌هایم» باز شد');
const cA = await page.textContent(card('A')), cB = await page.textContent(card('B'));
ok(cA.includes(fa('7,000')) || cA.includes('7,000'), 'کارتِ A مخزنِ A را دارد (۷٬۰۰۰)', cA.slice(0, 120));
ok(cB.includes(fa('1,200')) || cB.includes('1,200'), 'کارتِ B مخزنِ B را دارد (۱٬۲۰۰)');
ok(!cA.includes('1,200') && !cA.includes(fa('1,200')) && !cB.includes('7,000') && !cB.includes(fa('7,000')), '⛔ عددِ هیچ پمپی روی کارتِ دیگری نیست');
const all = await text('pumpsList');
ok(!all.includes(P.A.debtor) && !all.includes(P.B.debtor), '⛔ نامِ هیچ قرض‌داری در «پمپ‌هایم» نیست');
ok(!all.includes(P.A.owed) && !all.includes(P.B.owed), '⛔ عددهای «حساب‌ها» پیش از رمز دیده نمی‌شوند');

await page.fill(card('A') + ' input', P.B.pass);
await page.click(card('A') + ' button[data-act="pass"]');
await page.waitForFunction((s) => !document.querySelector(s + ' .pump-err').classList.contains('hidden'), card('A'), { timeout: 8000 }).catch(() => {});
ok(!(await text('pumpsList')).includes(P.A.owed), '⛔ رمزِ B کارتِ A را باز نکرد');
await page.fill(card('A') + ' input', P.A.pass);
await page.click(card('A') + ' button[data-act="pass"]');
await page.waitForFunction((s) => !!document.querySelector(s + ' .pump-banner'), card('A'), { timeout: 8000 }).catch(() => {});
ok((await page.textContent(card('A'))).includes(P.A.owed), 'رمزِ A ⇒ «قرض کل»ِ A روی کارتِ A');
ok(!(await page.textContent(card('B'))).includes(P.B.owed) && !(await page.$(card('B') + ' .pump-banner')),
  '⛔ و کارتِ B همچنان بسته است');
ok(!(await storage()).includes(P.A.owed) && !(await storage()).includes('7000'), '⛔ هیچ عددی از نمای «پمپ‌هایم» روی دیسک ننشست');

await page.click('#btnPumpsBack');
ok(await vis('appPane') && (await text('stName')).includes(P.B.name), '↩ ⇒ همان پمپِ B که باز بود');

await page.click('#btnPumpsTop');
await page.waitForFunction((s) => !!document.querySelector(s + ' .fig'), card('A'), { timeout: 8000 }).catch(() => {});
ok(!(await page.$(card('B') + ' .pump-banner')), '⛔ باز کردنِ دوباره هم کارتِ B را باز نکرد (رمزِ A فقط مالِ A)');
await page.click(card('A') + ' button[data-act="open"]');
await page.waitForFunction((n) => (document.getElementById('stName').textContent || '').includes(n)
  && !document.getElementById('appPane').classList.contains('hidden'), P.A.name, { timeout: 10000 }).catch(() => {});
ok((await text('stName')).includes(P.A.name), 'باز کردنِ A از فهرست ⇒ پمپِ A');
await staffSees('A');
ok(!(await storage()).includes(P.B.debtor), '⛔ حافظهٔ گوشی هیچ دادهٔ B ندارد (A باز است)');

await page.click('#btnOther');
await page.waitForSelector('#dlgOk', { state: 'visible', timeout: 5000 }).catch(() => {});
await page.click('#dlgOk').catch(() => {});
await page.waitForFunction(() => !document.getElementById('codePane').classList.contains('hidden'), null, { timeout: 8000 }).catch(() => {});
const after = JSON.parse(await page.evaluate(() => localStorage.getItem('pumpKar.v1.pumps') || '[]'));
ok(after.length === 1 && after[0].stn === 'ac-b', '⇄ خروج از A ⇒ A از «پمپ‌هایم» بیرون رفت، B ماند', JSON.stringify(after));
ok(await vis('btnCodePumps'), 'و صفحهٔ کد راهِ برگشت به پمپ‌هایم را دارد');

ok(errors.length === 0, 'هیچ خطای جاوااسکریپت', errors.join(' | '));
await browser.close();
srv.close();
console.log(bad ? `❌ ${bad} ایراد` : '✅ یک گوشی، دو پمپ، هیچ قاطی');
process.exit(bad ? 1 : 0);
