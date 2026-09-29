/* ══ عکس، ویدیو و صدا در «گروهِ کارکنان»ِ اپِ گوشی — در کرومیومِ واقعی ════════
 *
 *     node tools/check-kar-group-media.mjs
 *
 * خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «صدا، عکس و ویدیو از طریقِ سرور به یارو برود،
 * نه این‌که روی سرور بماند… توی حافظهٔ گوشیِ خودشان باشند.» سرورِ خانگیِ ساختگی
 * همان رفتارِ واقعی را دارد (فقط یک بار می‌دهد، بعد ۴۰۴ِ media_gone)، و سنجیده
 * می‌شود که:
 *   ۱) رسانهٔ دیگران یک بار گرفته و از حافظهٔ گوشی (blob) نشان داده می‌شود
 *   ۲) 📎 فایل بالا می‌رود و پیامِ رسانه با همان شناسه می‌رود؛ رسانهٔ خودم
 *      هیچ‌وقت از سرور خوانده نمی‌شود
 *   ۳) 🎤 ضبط و فرستادنِ صدا
 *   ۴) پس از بستن و باز کردنِ اپ، همه از حافظهٔ گوشی — بی پرسشِ دوباره
 *   ۵) رفتن به پمپِ دیگر رسانهٔ این پمپ را از گوشی پاک می‌کند
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
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
  const ext = path.extname(f);
  res.setHeader('content-type', ext === '.js' ? 'text/javascript' : ext === '.html' ? 'text/html; charset=utf-8' : 'application/octet-stream');
  res.end(fs.readFileSync(f));
});
await new Promise(r => srv.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${srv.address().port}/kar/`;

const PNG = Buffer.from('89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d49444154789c6360000002000154a24f5d0000000049454e44ae426082', 'hex');
const snap = { v: 1, seq: 9, at: '1405/07/05', station: { name: 'پمپِ آزمون' }, debtors: [], alerts: [], sections: {}, tank: {} };
let seq = 1, n = 0;
const msgs = [{ seq: 1, cid: 'd1', from: 'مدیر', role: 'admin', text: '', kind: 'image', mediaId: 'mOWNERxxxxxxxxxx', at: Date.now() - 60000 }];
const gets = {}, log = [];
const browser = await chromium.launch({
  executablePath: fs.existsSync('/opt/pw-browsers/chromium/chrome-linux/chrome') ? '/opt/pw-browsers/chromium/chrome-linux/chrome' : undefined,
  args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'],
});
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, permissions: ['microphone'] });
const page = await ctx.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
await page.route('https://home.invalid/**', r => r.abort());
await page.route('https://accounts.google.com/**', r => r.fulfill({ status: 200, body: '' }));
await page.route(/\/kar\/version\.json/, r => r.fulfill({ status: 200, contentType: 'application/json', body: '{"v":"10"}' }));
await page.route('https://api.vill3n.top/**', async (route) => {
  const r = route.request(), u = new URL(r.url()), h = { 'access-control-allow-origin': '*' };
  const json = (b, s = 200) => route.fulfill({ status: s, contentType: 'application/json', headers: h, body: JSON.stringify(b) });
  log.push(r.method() + ' ' + u.pathname + ' ' + (r.headers().authorization || ''));
  if (u.pathname.endsWith('/api/pump/public/join'))
    return json({ ok: true, station: { code: 'ac-one', name: snap.station.name, accessCode: '48291736' }, home: { url: 'wss://home.invalid', readKey: 'rk-1', station: 'ac-one' }, cloudLiveAt: 5 });
  if (u.pathname.includes('/api/pump/public/live')) return json({ ok: true, updatedAt: Date.now(), live: snap });
  let m = u.pathname.match(/\/api\/stations\/([^/]+)\/chat\/media\/([^/]+)$/);
  if (m) {
    gets[m[2]] = (gets[m[2]] || 0) + 1;
    if (gets[m[2]] > 1 || m[2].startsWith('mMINE')) return json({ error: 'media_gone' }, 404);
    return route.fulfill({ status: 200, contentType: 'image/png', headers: h, body: PNG });
  }
  if (/\/api\/stations\/[^/]+\/chat\/media$/.test(u.pathname) && r.method() === 'POST')
    return json({ ok: true, mediaId: 'mMINE' + String(++n).padStart(12, '0'), kind: (r.headers()['content-type'] || '').split('/')[0], keepHours: 48 }, 201);
  if (/\/api\/stations\/[^/]+\/chat$/.test(u.pathname) && r.method() === 'POST') {
    const b = JSON.parse(r.postData() || '{}');
    const msg = { seq: ++seq, cid: b.cid, from: b.from, role: b.role, text: b.text || '', at: Date.now(), ...(b.kind ? { kind: b.kind, mediaId: b.mediaId } : {}) };
    msgs.push(msg); return json({ ok: true, message: msg });
  }
  if (/\/api\/stations\/[^/]+\/chat$/.test(u.pathname)) {
    const since = Number(u.searchParams.get('since') || 0);
    return json({ ok: true, relayDays: 15, last: seq, messages: msgs.filter(x => x.seq > since) });
  }
  return json({});
});

const blobImgs = () => page.$$eval('#chatList img', l => l.filter(i => i.src.startsWith('blob:') && i.naturalWidth > 0).length);
async function openChat() {
  await page.click('#nav button[data-pane="paneChat"]');
  await page.waitForSelector('#paneChat:not(.hidden)');
}

await page.goto(base);
await page.waitForSelector('#codePane:not(.hidden)');
await page.fill('#inCode', '48291736'); await page.click('#btnJoin');
await page.waitForSelector('#paneHome:not(.hidden)');
await page.click('.door.staff');
await openChat();

console.log('۱) رسانهٔ دیگران');
await page.waitForTimeout(1500); if (process.env.DBG) { console.log(await page.innerHTML('#chatList')); console.log(await page.textContent('#chatState')); console.log(log.join('\n')); }
await page.waitForFunction(() => [...document.querySelectorAll('#chatList img')].some(i => i.src.startsWith('blob:') && i.naturalWidth > 0), null, { timeout: 6000 }).catch(() => {});
ok(await blobImgs() === 1, 'عکسِ مدیر از حافظهٔ گوشی (blob) نشان داده می‌شود');
ok(gets.mOWNERxxxxxxxxxx === 1, 'و فقط یک بار از سرور گرفته شد: ' + gets.mOWNERxxxxxxxxxx);
ok(log.some(l => /GET \/api\/stations\/ac-one\/chat\/media\/mOWNER.* Bearer rk-1$/.test(l)), 'با رمزِ خواندنِ همین پمپ');
await page.waitForTimeout(6500);   // یک دورِ دیگرِ پرسیدن
ok(gets.mOWNERxxxxxxxxxx === 1 && await blobImgs() === 1, 'دورِ بعدیِ پرسیدن هم دوباره نمی‌گیرد و عکس سرِ جایش است');

console.log('۲) 📎 پیوست');
await page.fill('#inChatName', 'کریم');
const tmp = path.join(os.tmpdir(), 'kar-probe.png'); fs.writeFileSync(tmp, PNG);
const [chooser] = await Promise.all([page.waitForEvent('filechooser', { timeout: 3000 }).catch(() => null), page.click('#btnChatAttach')]);
ok(!!chooser, '📎 گزینش‌گرِ فایل باز شد');
if (chooser) await chooser.setFiles(tmp);
await page.waitForFunction(() => [...document.querySelectorAll('#chatList img')].filter(i => i.src.startsWith('blob:')).length >= 2, null, { timeout: 5000 }).catch(() => {});
ok(log.some(l => l.startsWith('POST /api/stations/ac-one/chat/media ')), 'فایل بالا رفت');
const sent = msgs.find(x => x.mediaId === 'mMINE000000000001');
ok(!!sent && sent.kind === 'image' && sent.from === 'کریم', 'پیامِ رسانه با همان شناسه و نام رفت');
ok(await blobImgs() === 2, 'عکسِ خودم هم در گفت‌وگوست');

console.log('۳) 🎤 پیامِ صوتی');
await page.click('#btnChatRec'); await page.waitForTimeout(1200);
ok((await page.textContent('#btnChatRec')).includes('⏹'), 'ضبط شروع شد');
await page.click('#btnChatRec');
await page.waitForFunction(() => !!document.querySelector('#chatList audio[src^="blob:"]'), null, { timeout: 5000 }).catch(() => {});
for (let i = 0; i < 40 && !msgs.some(x => x.kind === 'audio'); i++) await page.waitForTimeout(100);
ok(msgs.some(x => x.kind === 'audio'), 'پیامِ صوتی رفت');
ok(!!(await page.$('#chatList audio[src^="blob:"]')), 'پیامِ صوتی از حافظهٔ گوشی پخش‌شدنی است');
ok(!Object.keys(gets).some(k => k.startsWith('mMINE')), 'رسانهٔ خودم هیچ‌وقت از سرور خوانده نشد');

console.log('۴) بستن و باز کردنِ اپ');
await page.reload();
await page.waitForSelector('#nav button[data-pane="paneChat"]', { state: 'visible' });
await openChat();
await page.waitForFunction(() => [...document.querySelectorAll('#chatList img')].filter(i => i.src.startsWith('blob:') && i.naturalWidth > 0).length >= 2, null, { timeout: 6000 }).catch(() => {});
ok(await blobImgs() === 2 && !!(await page.$('#chatList audio[src^="blob:"]')), 'همهٔ رسانه‌ها از حافظهٔ گوشی آمدند');
ok(gets.mOWNERxxxxxxxxxx === 1, 'و سرور دوباره پرسیده نشد');
ok(!(await page.textContent('#chatList')).includes('۴۸ ساعت'), 'هیچ رسانه‌ای گم‌شده نشان داده نشد');

console.log('۵) پمپِ دیگر');
await page.evaluate(() => { const b = document.getElementById('btnOther'); if (b) b.click(); });
await page.waitForSelector('#codePane:not(.hidden)', { timeout: 5000 }).catch(() => {});
await page.waitForTimeout(400);
const left = await page.evaluate(() => new Promise((ok) => {
  const r = indexedDB.open('pump-kar-media'); r.onsuccess = () => {
    const q = r.result.transaction('m').objectStore('m').count(); q.onsuccess = () => ok(q.result); q.onerror = () => ok(-1);
  }; r.onerror = () => ok(-2);
}));
ok(left === 0, 'رفتن به پمپِ دیگر رسانهٔ این پمپ را از گوشی پاک کرد: ' + left);
ok(errors.length === 0, 'بی خطای جاوااسکریپت' + (errors.length ? ': ' + errors.join(' | ') : ''));

await browser.close(); srv.close();
console.log(bad ? `\n❌ ${bad} ایراد` : '\n✅ رسانهٔ گروهِ کارکنان در اپِ گوشی درست است');
process.exit(bad ? 1 : 0);
