/**
 * ══ کدِ هشت‌رقمی با اپِ گوشیِ واقعی و سرورهای واقعی (۱۴۰۵/۰۷/۱۴) ═════════
 *
 * خواستهٔ صاحب سامانه: «برای هر حساب کاربری یک کد هشت‌رقمی درست بشه تا به
 * برنامهٔ گوشیِ اندروید و آیفون وصل شد و برنامه رو دید؛ و برای برنامه‌ها هم
 * یک بار زده بشه کافی است… بدون حدس، در عمل.»
 *
 * پیش‌نیاز:
 *   node test/signup-stack.mjs live.json                 (ریپوی server)
 *   dotnet run … -- oldacct live.json <پوشه> karcode [pass] real
 *
 *   node tools/check-kar-live.mjs <live.json> <پوشه> [pass]
 *
 * خودِ `kar/` در کرومیوم باز می‌شود و هر درخواستِ `api.vill3n.top` به همان
 * سرورِ حسابِ **واقعیِ** پشته می‌رود (نه پاسخِ ساختگی). کد همان است که
 * برنامهٔ کامپیوتر خودش گرفته — با رقمِ فارسی تایپ می‌شود.
 */
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const [liveFile, shots, passArg] = process.argv.slice(2);
const withPass = passArg === 'pass';
const live = JSON.parse(fs.readFileSync(liveFile, 'utf8'));
const kc = JSON.parse(fs.readFileSync(path.join(shots, withPass ? 'karcode-pass.json' : 'karcode.json'), 'utf8'));
const PUB = live.public.replace(/\/$/, '');

const globalRoot = execSync('npm root -g').toString().trim();
const { chromium } = createRequire(import.meta.url)(globalRoot + '/playwright');
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const srv = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(ROOT, p);
  if (!fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  const ext = path.extname(f);
  res.setHeader('content-type', ext === '.js' ? 'text/javascript' : ext === '.html' ? 'text/html; charset=utf-8' : 'application/octet-stream');
  res.end(fs.readFileSync(f));
});
await new Promise(r => srv.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${srv.address().port}/kar/`;

let bad = 0;
const ok = (c, m) => { if (!c) bad++; console.log((c ? '✓ ' : '✗ ') + m); };
const fa = (s) => s.replace(/[0-9]/g, d => '۰۱۲۳۴۵۶۷۸۹'[d]);

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 } });
const page = await ctx.newPage();
const hits = [];
//  ⛔ نه پاسخِ ساختگی: همان درخواست به سرورِ حسابِ واقعیِ پشته
await ctx.route('https://api.vill3n.top/**', async (route) => {
  const req = route.request();
  const u = new URL(req.url());
  const r = await fetch(PUB + u.pathname + u.search, {
    method: req.method(), headers: { 'content-type': 'application/json', 'x-app-id': req.headers()['x-app-id'] || '' },
    body: ['GET', 'HEAD'].includes(req.method()) ? undefined : req.postData() || undefined,
  });
  const body = Buffer.from(await r.arrayBuffer());
  hits.push(`${req.method()} ${u.pathname} ⇒ ${r.status}`);
  await route.fulfill({ status: r.status, body, headers: { 'content-type': r.headers.get('content-type') || 'application/json', 'access-control-allow-origin': '*' } });
});
const vis = (id) => page.evaluate((id) => { const e = document.getElementById(id); return !!e && !e.classList.contains('hidden'); }, id);
const shot = (n) => page.screenshot({ path: path.join(shots, n + '.png') });

console.log(`══ اپِ گوشی با کدِ ${kc.display}` + (withPass ? ' — پمپِ رمزدار' : ' — پمپِ بی‌رمز'));
await page.goto(base);
ok(await vis('codePane'), 'صفحهٔ اول کدِ پمپ است');
ok((await page.getAttribute('#inCode', 'inputmode')) === 'numeric', 'صفحه‌کلیدِ عددیِ گوشی');
await page.fill('#inCode', fa(kc.code));
ok((await page.inputValue('#inCode')) === kc.display, 'رقمِ فارسی تایپ شد ⇒ ' + (await page.inputValue('#inCode')));
await shot(withPass ? 'kar-1-code-pass' : 'kar-1-code');
await page.click('#btnJoin');

if (withPass) {
  await page.waitForFunction(() => !document.getElementById('btnUnlock').disabled, null, { timeout: 60000 });
  ok(await vis('lockPane'), 'پمپِ رمزدار ⇒ یک بار رمزِ برنامه');
  await page.fill('#inPass', '0000'); await page.click('#btnUnlock');
  await page.waitForFunction(() => !document.getElementById('lockErr').classList.contains('hidden'));
  ok(true, 'رمزِ غلط رد شد');
  await page.fill('#inPass', kc.pass); await page.click('#btnUnlock');
}
await page.waitForFunction(() => !document.getElementById('appPane').classList.contains('hidden'), null, { timeout: 60000 })
  .catch(() => {});
ok(await vis('appPane'), withPass ? 'رمزِ درست ⇒ برنامه' : '⛔ بی‌رمز ⇒ همان لحظه برنامه، بی هیچ پرسشی');
await shot(withPass ? 'kar-2-home-pass' : 'kar-2-home');

await page.click('.door.staff').catch(() => {});
await page.waitForFunction((n) => (document.getElementById('staffList') || {}).textContent?.includes(n), kc.debtor, { timeout: 20000 })
  .catch(() => {});
ok(((await page.textContent('#staffList')) || '').includes(kc.debtor), 'قرض‌دارِ همین پمپ دیده می‌شود: ' + kc.debtor);
await shot(withPass ? 'kar-3-staff-pass' : 'kar-3-staff');

//  ⛔ «یک بار زده بشه کافی است»: باز شدنِ دوباره — نه کد، نه رمز
await page.reload();
await page.waitForFunction(() => !document.getElementById('appPane').classList.contains('hidden'), null, { timeout: 30000 })
  .catch(() => {});
ok(await vis('appPane'), 'باز شدنِ دوباره ⇒ مستقیم برنامه، بی کد و بی رمز');
ok(!(await vis('codePane')) && !(await vis('lockPane')), 'نه صفحهٔ کد، نه صفحهٔ قفل');
await shot(withPass ? 'kar-4-reopen-pass' : 'kar-4-reopen');

console.log('     ⓘ درخواست‌ها: ' + [...new Set(hits)].join(' · '));
await browser.close();
srv.close();
console.log(bad ? `❌ ${bad} ایراد` : '✅ اپِ گوشی با کدِ هشت‌رقمی وصل شد و برنامه را دید');
process.exit(bad ? 1 : 0);
