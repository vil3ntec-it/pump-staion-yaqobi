/* ══ آزمونِ مرورگریِ اپِ کارمندان ═══════════════════════════════════════════
 *
 *     node tools/check-kar-ui.mjs
 *
 * همان کاری که کارمند می‌کند، در کرومیومِ بی‌پنجره: کدِ پمپ (غلط و درست)،
 * قفل (رمزِ غلط و درست)، خانه با دو در، درِ کارمندان (چراغ‌ها، فیلتر،
 * کلیک روی نفر)، درِ حساب‌ها (چهار عدد، مخزن، کاشیِ بخش)، باز شدنِ دوباره
 * با همان در، و «پمپِ دیگر» که همه‌چیز را پاک می‌کند. ابر ساختگی است
 * (‎page.route‎). Playwright از نصبِ سراسری خوانده می‌شود
 * (‎npm i -g playwright‎). قاعدهٔ صاحب ریپو: «با تست بگو، نه با حدس» —
 * بارِ اول ترتیبِ چراغ‌ها و کلیدِ جاماندهٔ ‎mode‎ را همین گرفت.
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
const globalRoot = execSync('npm root -g').toString().trim();
const { chromium } = createRequire(import.meta.url)(globalRoot + '/playwright');
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

import { fileURLToPath } from 'node:url';
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

// pbkdf2 gate like PasswordHasher
const salt = crypto.randomBytes(16), iter = 1000;
const hash = crypto.pbkdf2Sync('1234', salt, iter, 32, 'sha256');
const gate = `pbkdf2$sha256$${iter}$${salt.toString('base64')}$${hash.toString('base64')}`;
const snap = {
  v: 1, seq: 9, at: '1405/06/26', gate,
  station: { name: 'پمپِ آزمون' },
  banner: [['شرکت ها تیل (الباقی)', '12,000', 'accent'], ['قرض کل', '5,000', 'danger'], ['مفاد امروز', '800', 'ok'], ['مصارف امروز', '300', 'warn']],
  tank: { petrol: { in: 10000, out: 2500, show: 7500 }, diesel: { in: 4000, out: 3900, show: 100, low: true } },
  debtors: [
    { id: 1, name: 'هارون', status: 'ok', bal: { money: 0, petrol: 200, diesel: 0 }, accounts: [] },
    { id: 2, name: 'محمد', status: 'out', bal: { money: -500, petrol: 0, diesel: 0 }, accounts: [] },
    { id: 3, name: 'علی', status: 'low', bal: { money: 0, petrol: 10, diesel: 0 }, accounts: [] },
  ],
  alerts: [{ k: 'd2-out', s: 'out', t: 'محمد — اضافه برد' }],
  sections: { expense: { t: 'مصارف', head: ['تاریخ', 'مبلغ'], rows: [['1405/06/01', '300']], m: ['1405/06'], sum: [['جمله مصارف', '300']] },
              parcha: { t: 'پارچه', head: ['تاریخ', 'فایده'], rows: [['1405/06/01', '800']], m: ['1405/06'], sum: [['جمله فایده', '800']] } },
};

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());
const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
const errors = [];
page.on('pageerror', e => errors.push('pageerror: ' + e.message));
page.on('console', m => { if (m.type() === 'error' && !/ERR_CERT|404/.test(m.text())) errors.push('console: ' + m.text()); });

// mock cloud
await page.route('https://api.vill3n.top/**', async (route) => {
  const url = route.request().url();
  const body = route.request().postDataJSON?.() || {};
  if (url.endsWith('/api/pump/public/join')) {
    if (body.code === 'K7PM3XQ2')
      return route.fulfill({ status: 200, contentType: 'application/json', headers: { 'access-control-allow-origin': '*' },
        body: JSON.stringify({ ok: true, station: { code: 'ac-one', name: 'پمپِ آزمون' }, home: { url: '', readKey: '', station: 'ac-one' }, cloudLiveAt: 5 }) });
    return route.fulfill({ status: 404, contentType: 'application/json', headers: { 'access-control-allow-origin': '*' },
      body: JSON.stringify({ error: { code: 'bad_access_code', message: 'این کد به هیچ پمپی نمی‌رسد' } }) });
  }
  if (url.includes('/api/pump/public/live'))
    return route.fulfill({ status: 200, contentType: 'application/json', headers: { 'access-control-allow-origin': '*' },
      body: JSON.stringify({ ok: true, updatedAt: Date.now(), live: snap }) });
  if (url.includes('/api/config')) return route.fulfill({ status: 200, contentType: 'application/json', headers: { 'access-control-allow-origin': '*' }, body: '{}' });
  return route.fulfill({ status: 404, body: '{}' });
});

let bad = 0;
const ok = (c, m) => { if (!c) bad++; console.log((c ? '✓ ' : '✗ ') + m); };
const vis = (id) => page.evaluate((id) => { const e = document.getElementById(id); return !!e && !e.classList.contains('hidden'); }, id);

await page.goto(base);
ok(await vis('codePane'), 'صفحهٔ اول کدِ پمپ است');
ok(!(await vis('signinPane')), 'گوگل پنهان است');

// wrong code
await page.fill('#inCode', 'AAAAAAAA');
await page.click('#btnJoin');
await page.waitForFunction(() => !document.getElementById('codeErr').classList.contains('hidden'));
ok((await page.textContent('#codeErr')).includes('هیچ پمپی'), 'کدِ غلط پیامِ درست می‌دهد');

// right code (pump without home url ⇒ cloud snapshot)
await page.fill('#inCode', 'k7pm3xq2');
ok((await page.inputValue('#inCode')) === 'K7PM-3XQ2', 'خطِ تیره خودکار می‌نشیند');
await page.click('#btnJoin');
await page.waitForFunction(() => !document.getElementById('lockPane').classList.contains('hidden'));
ok(await vis('lockPane'), 'بعد از کد، صفحهٔ قفل می‌آید');
await page.waitForFunction(() => !document.getElementById('btnUnlock').disabled, null, { timeout: 8000 });
ok((await page.textContent('#lockChip')).includes('K7PM-3XQ2'), 'نشانِ کدِ پمپ روی قفل');
ok((await page.textContent('#lockTitle')).includes('پمپِ آزمون'), 'نامِ پمپ از عکسِ ابری');

await page.fill('#inPass', '0000'); await page.click('#btnUnlock');
await page.waitForFunction(() => !document.getElementById('lockErr').classList.contains('hidden'));
ok(true, 'رمزِ غلط رد شد');
await page.fill('#inPass', '1234'); await page.click('#btnUnlock');
await page.waitForFunction(() => !document.getElementById('appPane').classList.contains('hidden'));
ok(await vis('paneHome'), 'بعد از رمز، خانه با دو در');
ok(!(await vis('nav')), 'روی خانه نوارِ پایین نیست');

await page.click('.door.staff');
ok(await vis('paneStaff'), 'درِ کارمندان باز شد');
ok(await vis('nav'), 'نوارِ پایین آمد');
const rows = await page.$$eval('#staffList .st-row', els => els.map(e => e.className + '|' + e.querySelector('.n').textContent));
ok(rows.length === 3 && rows[0].includes('out') && rows[0].includes('محمد'), 'چراغِ سرخ اول می‌آید: ' + rows.join(' , '));
await page.click('#staffCounts button[data-f="ok"]');
ok((await page.$$('#staffList .st-row')).length === 1, 'فیلترِ «دارد» یک نفر');
await page.click('#staffCounts button[data-f=""]');
await page.click('#staffList .st-row');
ok(await vis('paneBot'), 'کلیک روی نفر ⇒ حال و الباقی');
ok((await page.textContent('#botOut')).includes('محمد'), 'نامش آمده');
await page.click('#btnBackStaff');
ok(await vis('paneStaff'), 'برگشت به فهرست');
ok(await vis('alertCard') && (await page.textContent('#alertBox')).includes('اضافه برد'), 'خبرها روی درِ کارمندان');

await page.click('#modeSeg button[data-mode="owner"]');
ok(await vis('paneDash'), 'درِ حساب‌ها ⇒ داشبورد');
ok((await page.$$('#bannerBox .bn')).length === 4, 'چهار عددِ نوار');
ok((await page.textContent('#dashTank')).includes('7,500'), 'مخزن در داشبورد');
await page.click('#dashTiles button[data-sec="parcha"]');
ok(await vis('paneSec') && (await page.textContent('#secBox')).includes('پارچه'), 'کاشیِ بخش ⇒ همان بخش');
const navTxt = await page.textContent('#nav');
ok(navTxt.includes('بخش‌ها') && navTxt.includes('قرض‌داران'), 'نوارِ حساب‌ها کامل است');

// reload keeps mode & station
await page.reload();
await page.waitForFunction(() => !document.getElementById('lockPane').classList.contains('hidden'));
await page.waitForFunction(() => !document.getElementById('btnUnlock').disabled, null, { timeout: 8000 });
await page.fill('#inPass', '1234'); await page.click('#btnUnlock');
await page.waitForFunction(() => !document.getElementById('appPane').classList.contains('hidden'));
ok(await vis('paneDash'), 'بعد از باز شدنِ دوباره، همان در (حساب‌ها)');

// leave pump ⇒ everything forgotten
await page.click('#btnLock');
await page.click('#btnForget');
ok(await vis('codePane'), '«پمپِ دیگر» ⇒ صفحهٔ کد');
const left = await page.evaluate(() => Object.keys(localStorage).filter(k => k.startsWith('pumpKar.v1.')));
ok(left.length === 0, 'هیچ چیزی از پمپ در گوشی نماند: ' + JSON.stringify(left));

ok(errors.length === 0, 'بی خطای جاوااسکریپت' + (errors.length ? ': ' + errors.join(' | ') : ''));
await browser.close(); srv.close();
console.log(bad ? `\n${bad} ✗` : '\nهمه درست');
process.exit(bad ? 1 : 0);
