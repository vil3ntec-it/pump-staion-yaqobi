/* ══ هر دکمهٔ صفحهٔ کیو‌آر، با کلیکِ واقعی در کرومیوم ═══════════════════════
 *
 *     node tools/check-view-buttons.mjs
 *
 * گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «دکمه‌های سایتِ کیو‌آر بعضی‌هاشون باگ دارن
 * و کار نمی‌کنن.» پس هر دکمه واقعاً زده می‌شود و اثرش سنجیده می‌شود — با سرورِ
 * حسابِ ساختگی که هر درخواست را ثبت می‌کند.
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { deflateRawSync } from 'node:zlib';
import http from 'node:http';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const globalRoot = execSync('npm root -g').toString().trim();
const { chromium } = createRequire(import.meta.url)(globalRoot + '/playwright');
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');

let bad = 0;
const ok = (c, what) => { console.log((c ? '  ✓ ' : '  ✗ ') + what); if (!c) bad++; };

const srv = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(ROOT, p);
  if (!fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  res.setHeader('content-type', f.endsWith('.js') ? 'text/javascript' : 'text/html; charset=utf-8');
  res.end(fs.readFileSync(f));
});
await new Promise(r => srv.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${srv.address().port}/view/`;

const encode = (obj) => deflateRawSync(Buffer.from(JSON.stringify(obj), 'utf8'))
  .toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
const book = (k, u) => ({
  k, u, dc: 0, fc: 3,
  s: [['جمله بردگی', '1,000'], ['جمله رسید', '400'], ['فیصدی ما', '0'], ['الباقی', '600']],
  fh: ['تیل', 'بردگی', 'رسید', 'فیصدی', 'الباقی'],
  f: [['پطرول', '600', '200', '0', '400'], ['دیزل', '400', '200', '0', '200']],
  h: ['تاریخ', 'نام', 'حواله', 'تیل', 'مقدار', 'فی', 'بردگی', 'رسید'],
  r: [['1405/05/02', 'الف', '', 'پطرول', '10', '60', '600', ''], ['1405/06/03', 'ب', '', 'دیزل', '5', '80', '400', '400']],
  g: [['1405/05', '600', '0', '600'], ['1405/06', '400', '400', '0']],
});
const snap = { t: 'قرض‌دار — واحد تیل', n: 'محمد هارون', p: 'پمپِ آزمون', d: '1405/07/05', b: [book('واحد تیل', 'لیتر'), book('واحد پول', 'افغانی')] };
const frag = 's=ac-one&a=d7&k=kk&t=1&d=' + encode(snap);

const log = [];
let seq = 1;
const msgs = [{ id: 'm1', seq: 1, from: 'o', name: 'پمپ', kind: 'text', text: 'سلام', at: Date.now() - 60000 },
  { id: 'm0', seq: 1, from: 'o', name: 'پمپ', kind: 'image', mediaId: 'mo1', at: Date.now() - 50000 }];
const mediaGets = {};   // ⛔ سرورِ ساختگی مثلِ واقعی: رسانهٔ پمپ فقط یک بار، بعد ۴۰۴ِ media_gone
const browser = await chromium.launch({
  executablePath: fs.existsSync('/opt/pw-browsers/chromium/chrome-linux/chrome') ? '/opt/pw-browsers/chromium/chrome-linux/chrome' : undefined,
  args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'],
}).catch(() => chromium.launch({ args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'] }));
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, permissions: ['microphone'] });
const page = await ctx.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
page.on('dialog', d => { log.push('dialog:' + d.message()); d.dismiss().catch(() => {}); });
await page.route('https://api.vill3n.top/**', async (route) => {
  const r = route.request(), u = new URL(r.url()), h = { 'access-control-allow-origin': '*' };
  log.push(r.method() + ' ' + u.pathname);
  const json = (b, s = 200) => route.fulfill({ status: s, contentType: 'application/json', headers: h, body: JSON.stringify(b) });
  if (/\/chat\/media$/.test(u.pathname) && r.method() === 'POST') return json({ ok: true, mediaId: 'md' + (++seq) });
  if (/\/chat\/media\//.test(u.pathname)) {
    const id = u.pathname.split('/').pop(); mediaGets[id] = (mediaGets[id] || 0) + 1;
    if (mediaGets[id] > 1 || id.startsWith('md')) return json({ error: { code: 'media_gone' } }, 404);
  }
  if (/\/chat\/media\//.test(u.pathname)) return route.fulfill({ status: 200, contentType: 'image/png', headers: h, body: Buffer.from('89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d49444154789c6360000002000154a24f5d0000000049454e44ae426082', 'hex') });
  if (/\/chat\/seen$/.test(u.pathname)) return json({ ok: true });
  if (/\/chat\/push$/.test(u.pathname)) return json({ ok: true });
  if (/\/chat\/[^/]+$/.test(u.pathname) && r.method() === 'DELETE') {
    const id = u.pathname.split('/').pop(); const m = msgs.find(x => x.id === id);
    if (m) { m.deleted = true; m.text = ''; }
    return json({ ok: true, message: m });
  }
  if (/\/chat$/.test(u.pathname) && r.method() === 'POST') {
    const b = JSON.parse(r.postData() || '{}');
    const m = { id: 'm' + (++seq), seq, from: 'c', name: b.name, kind: b.kind || 'text', text: b.text || '', mediaId: b.mediaId, at: Date.now() };
    msgs.push(m); return json({ ok: true, message: m });
  }
  if (/\/chat$/.test(u.pathname)) return json({ ok: true, messages: msgs.slice(), vapid: '' });
  if (/\/acct\//.test(u.pathname)) return json({ at: 2, d: snap });
  return json({});
});

await page.goto(base + '#' + frag);
await page.waitForSelector('[data-act="refresh"]');
ok((await page.textContent('body')).includes('پمپِ آزمون'), 'نامِ پمپ روی صفحه');
ok(!(await page.textContent('body')).includes('یعقوبی'), 'هیچ «یعقوبی»ای روی صفحه نیست');
ok((await page.title()).includes('پمپِ آزمون'), 'عنوانِ برگه نامِ پمپ است: ' + await page.title());

// دفترها و فیلترها
await page.click('[data-act="book"][data-v="1"]');
ok((await page.textContent('body')).includes('واحد پول'), 'تبِ «واحد پول» کار می‌کند');
await page.click('[data-act="book"][data-v="0"]');
const fuelBtns = await page.$$('[data-act="fuel"]');
ok(fuelBtns.length >= 2, 'دکمه‌های فیلترِ تیل هست');
if (fuelBtns.length >= 2) {
  await fuelBtns[1].click();
  const rows = await page.evaluate(() => { const t = [...document.querySelectorAll('table')].find(x => x.textContent.includes('حواله')); return t ? t.querySelectorAll('tbody tr').length : -2; }).catch(() => -1);
  ok(rows === 1, 'فیلترِ تیل ردیف‌ها را کم می‌کند: ' + rows);
  await (await page.$$('[data-act="fuel"]'))[0].click();
}
const sel = await page.$('select[data-act="month-sel"]');
ok(!!sel, 'کشوییِ ماه هست');
if (sel) {
  await page.selectOption('select[data-act="month-sel"]', '1405/05').catch(() => {});
  const rows = await page.evaluate(() => { const t = [...document.querySelectorAll('table')].find(x => x.textContent.includes('حواله')); return t ? t.querySelectorAll('tbody tr').length : -2; }).catch(() => -1);
  ok(rows === 1, 'کشوییِ ماه ردیف‌ها را صافی می‌کند: ' + rows);
  await page.selectOption('select[data-act="month-sel"]', '');
}

// به‌روز کن
const before = log.filter(l => l.includes('/acct/')).length;
await page.click('[data-act="refresh"]');
await page.waitForTimeout(400);
ok(log.filter(l => l.includes('/acct/')).length > before, '«به‌روز کن» واقعاً از سرور می‌پرسد');

// چاپ
await page.evaluate(() => { window.__printed = 0; window.print = () => { window.__printed++; }; });
await page.click('[data-act="print"]');
ok(await page.evaluate(() => window.__printed) === 1, '«چاپ / ذخیره PDF» چاپ را صدا می‌زند');

// چت
await page.click('.live [data-act="chat-open"]');
await page.waitForSelector('.chat:not(.hidden)');
ok(true, '«💬 پیام» چت را باز می‌کند');
ok((await page.textContent('.chat')).includes('پیام به پمپِ آزمون'), 'سربرگِ چت نامِ پمپ را دارد');
if (await page.$('#chatNameIn')) {
  await page.fill('#chatNameIn', 'هارون');
  await page.click('[data-act="chat-name"]');
}
ok(!!(await page.$('#chatText')), 'پس از نام، کادرِ نوشتن هست');
await page.fill('#chatText', 'سلام از آزمون');
await page.click('[data-act="chat-send"]');
await page.waitForTimeout(400);
ok(log.some(l => l === 'POST /api/pump/public/ac-one/acct/d7/chat'), 'فرستادنِ پیام به سرور رفت');
ok((await page.textContent('.chat')).includes('سلام از آزمون'), 'پیامِ فرستاده‌شده در گفت‌وگو دیده می‌شود');

// Enter هم می‌فرستد
await page.fill('#chatText', 'با اینتر');
await page.press('#chatText', 'Enter');
await page.waitForTimeout(400);
ok((await page.textContent('.chat')).includes('با اینتر'), 'Enter پیام را می‌فرستد');

// پاک کردنِ پیامِ خودم
const del = await page.$('[data-act="chat-del"]');
ok(!!del, 'دکمهٔ پاک کردنِ پیامِ خودم هست');
if (del) { await del.click(); await page.waitForTimeout(400); ok(log.some(l => l.startsWith('DELETE ')), 'پاک کردن به سرور رفت'); }

// پیوستِ عکس
const tmp = path.join(os.tmpdir(), 'qr-probe.png');
fs.writeFileSync(tmp, Buffer.from('89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d49444154789c6360000002000154a24f5d0000000049454e44ae426082', 'hex'));
const [chooser] = await Promise.all([page.waitForEvent('filechooser', { timeout: 3000 }).catch(() => null), page.click('[data-act="chat-attach"]')]);
ok(!!chooser, '📎 پنجرهٔ انتخابِ فایل باز شد');
if (chooser) { await chooser.setFiles(tmp); await page.waitForTimeout(600); }
ok(log.some(l => l.endsWith('/chat/media') && l.startsWith('POST')), 'عکس بالا رفت');
ok(!!(await page.$('.chat img')), 'عکس در گفت‌وگو دیده می‌شود');

// ضبطِ صدا
const rec = await page.$('[data-act="chat-rec"]');
ok(!!rec, 'دکمهٔ ضبطِ صدا هست');
if (rec) {
  const nMedia = log.filter(l => l.endsWith('/chat/media')).length;
  await rec.click(); await page.waitForTimeout(1200);
  await page.click('[data-act="chat-rec"]'); await page.waitForTimeout(1200);
  ok(log.filter(l => l.endsWith('/chat/media')).length > nMedia, 'صدای ضبط‌شده بالا رفت');
  ok(!!(await page.$('.chat audio')), 'پیامِ صوتی در گفت‌وگو پخش‌شدنی است');
}

// ⛔ رسانه روی همین گوشی: رسانهٔ پمپ یک بار گرفته شد و پس از رفتنش از سرور هم دیده می‌شود
await page.waitForFunction(() => [...document.querySelectorAll('.chat img')].some(i => i.src.startsWith('blob:') && i.naturalWidth > 0), null, { timeout: 4000 }).catch(() => {});
ok(await page.$$eval('.chat img', l => l.filter(i => i.src.startsWith('blob:')).length) >= 2, 'عکسِ پمپ و عکسِ خودم هر دو از حافظهٔ گوشی (blob) نشان داده می‌شوند');
ok(mediaGets.mo1 === 1, 'رسانهٔ پمپ فقط یک بار از سرور گرفته شد: ' + mediaGets.mo1);
ok(!Object.keys(mediaGets).some(k => k.startsWith('md')), 'رسانهٔ خودم هیچ‌وقت از سرور خوانده نشد (پیش از رفتن روی گوشی نشست)');
await page.reload();
await page.waitForSelector('[data-act="refresh"]');
await page.click('.live [data-act="chat-open"]');
await page.waitForFunction(() => [...document.querySelectorAll('.chat img')].filter(i => i.src.startsWith('blob:') && i.naturalWidth > 0).length >= 2, null, { timeout: 4000 }).catch(() => {});
ok(await page.$$eval('.chat img', l => l.filter(i => i.src.startsWith('blob:') && i.naturalWidth > 0).length) >= 2, 'پس از بستن و باز کردنِ صفحه، رسانه‌ها از حافظهٔ گوشی می‌آیند');
ok(mediaGets.mo1 === 1, 'و سرور دوباره پرسیده نشد: ' + mediaGets.mo1);
ok(!(await page.textContent('.chat')).includes('سرور هم آن را نگه نمی‌دارد'), 'هیچ رسانه‌ای «گم‌شده» نشان داده نشد');

await page.click('[data-act="chat-close"]');
ok(await page.$eval('.chat', e => e.classList.contains('hidden')).catch(() => true), 'بستنِ چت');
ok(errors.length === 0, 'بی خطای جاوااسکریپت' + (errors.length ? ': ' + errors.join(' | ') : ''));

await browser.close(); srv.close();
console.log(bad ? `\n❌ ${bad} ایراد` : '\n✅ همهٔ دکمه‌های کیو‌آر کار می‌کنند');
process.exit(bad ? 1 : 0);
