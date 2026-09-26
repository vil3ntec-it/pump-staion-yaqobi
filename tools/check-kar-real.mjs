/**
 * ══ اپِ گوشی در دنیای واقعی — آیفونِ سافاری و اندرویدِ روی اینترنتِ همراه (۱۴۰۵/۰۷/۱۴) ══
 *
 * گزارشِ صاحب سامانه: «برنامهٔ اندروید و آیفون هیچ‌کدام کار نمی‌کنند… یا
 * می‌گوید به سرور وصل است و برنامه جواب نمی‌دهد، یا به سرور وصل است و
 * اطلاعاتِ همان حساب را نشان نمی‌دهد.»
 *
 * ‎check-kar-live.mjs‎ سبز بود و این‌ها را نمی‌دید، چون سه چیزِ دنیای واقعی را
 * نداشت — هر سه این‌جا هست:
 *
 *   ۱) صفحه روی **https** است (‎yaqobipump.top/kar‎ در سافاری). مرورگر
 *      ‎ws://‎ِ شبکهٔ پمپ را از صفحهٔ https **می‌بندد** (محتوای ناامن).
 *   ۲) نشانیِ شبکهٔ پمپ (‎http://192.168…‎) از اینترنتِ همراه **جواب نمی‌دهد**
 *      — نه رد، نه قبول: سوکت همان‌جا می‌ماند (اندروید روی دیتای گوشی).
 *   ۳) تونل (‎wss://api.vill3n.top‎) واقعاً به همان سرورِ خانگی می‌رسد؛ هر
 *      درخواستِ این میزبان این‌جا به پورتِ عمومیِ پشتهٔ واقعی می‌رود.
 *
 * پیش‌نیاز: همان دو گامِ ‎check-kar-live.mjs‎ (پشته + ‎oldacct … karcode real‎).
 *
 *   node tools/check-kar-real.mjs <live.json> <پوشه>
 */
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import https from 'node:https';
import net from 'node:net';
import os from 'node:os';
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const [liveFile, shots] = process.argv.slice(2);
const live = JSON.parse(fs.readFileSync(liveFile, 'utf8'));
const kc = JSON.parse(fs.readFileSync(path.join(shots, 'karcode.json'), 'utf8'));
const PUB = live.public.replace(/\/$/, '');
const WS_PUB = PUB.replace(/^http/, 'ws');

const globalRoot = execSync('npm root -g').toString().trim();
const req = createRequire(import.meta.url);
const { chromium } = req(globalRoot + '/playwright');
const WS = req(path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'server', 'homelab-panel', 'server', 'node_modules', 'ws'));
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');

let bad = 0;
const ok = (c, m) => { if (!c) bad++; console.log((c ? '✓ ' : '✗ ') + m); };
const info = (m) => console.log('     ⓘ ' + m);

// ── سرورِ صفحه: یک بار https (آیفون)، یک بار http (پوستهٔ اندروید، مثلِ file://) ──
function serveKar(req, res) {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(ROOT, p);
  if (!f.startsWith(path.join(ROOT, 'kar')) || !fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  const ext = path.extname(f);
  res.setHeader('content-type', ext === '.js' ? 'text/javascript' : ext === '.html' ? 'text/html; charset=utf-8' : ext === '.json' ? 'application/json' : 'application/octet-stream');
  res.end(fs.readFileSync(f));
}
const certDir = fs.mkdtempSync(path.join(os.tmpdir(), 'kar-cert-'));
execSync(`openssl req -x509 -newkey rsa:2048 -nodes -keyout ${certDir}/k.pem -out ${certDir}/c.pem -days 1 -subj /CN=localhost 2>/dev/null`);
const httpsSrv = https.createServer({ key: fs.readFileSync(`${certDir}/k.pem`), cert: fs.readFileSync(`${certDir}/c.pem`) }, serveKar);
const httpSrv = http.createServer(serveKar);
await new Promise(r => httpsSrv.listen(0, '127.0.0.1', r));
await new Promise(r => httpSrv.listen(0, '127.0.0.1', r));
//  ⚠️ «localhost»، نه «127.0.0.1»: مرورگر ‎ws://127.0.0.1‎ را از صفحهٔ https هم
//  «امن» می‌شمارد، پس محتوای ناامن هرگز بسته نمی‌شد. نشانیِ شبکهٔ پمپ هم
//  ‎192.0.2.x‎ است (نه loopback) — همان چیزی که گوشی می‌بیند.
const HTTPS_BASE = `https://localhost:${httpsSrv.address().port}/kar/`;
const HTTP_BASE = `http://127.0.0.1:${httpSrv.address().port}/kar/`;

// ── «شبکهٔ پمپ از روی دیتای گوشی»: سوکتی که قبول می‌کند و هرگز جواب نمی‌دهد ──
const black = net.createServer((s) => { s.on('error', () => { }); /* هیچ */ });
await new Promise(r => black.listen(0, '127.0.0.1', r));
const HANG_URL = `http://127.0.0.1:${black.address().port}`;

// ── برنامهٔ کامپیوتر: همان دو جایی که واقعاً می‌نویسد (سرورِ خانگی و سرورِ حساب) ──
const join = await (await fetch(PUB + '/api/pump/public/join', {
  method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ code: kc.code }),
})).json();
const stn = join.home.station;
const stnDir = path.join(live.tmp, 'data', 'stations', stn);
const writeTok = fs.readFileSync(path.join(stnDir, 'token.txt'), 'utf8').trim();
info(`پمپ ${join.station.code} · پوشهٔ خانگی ${stn} · نشانیِ شبکهٔ پمپ ${join.home.url}`);

/** برنامهٔ کامپیوتر یک عکسِ تازه روی سرورِ خانگی می‌نویسد (همان ‎set live‎ی HomeSync). */
async function desktopPublish(snap) {
  const ws = new WS(`${WS_PUB}/station?station=${encodeURIComponent(stn)}&token=${encodeURIComponent(writeTok)}`);
  await new Promise((res, rej) => { ws.on('open', res); ws.on('error', rej); });
  await new Promise((res) => {
    ws.on('message', (d) => { const m = JSON.parse(d); if (m.op === 'ack') res(); });
    ws.send(JSON.stringify({ op: 'set', id: 1, path: 'live', value: snap }));
  });
  ws.close();
}
const base = JSON.parse(fs.readFileSync(path.join(stnDir, 'live.json'), 'utf8'));
const baseLive = base.live || base;

// ── مرورگر ──
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium/chrome-linux/chrome' }).catch(() => chromium.launch());

async function scenario(title, { origin, homeUrl, cloudLive = true, liveEmpty = false }) {
  console.log('\n══ ' + title);
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  const hits = [];
  page.on('pageerror', (e) => hits.push('⛔ pageerror: ' + String(e.message || e).slice(0, 200)));
  await ctx.route('https://api.vill3n.top/**', async (route) => {
    const r0 = route.request();
    const u = new URL(r0.url());
    if (!cloudLive && u.pathname === '/api/pump/public/live') {
      hits.push('GET live ⇒ 404 (ساختگی: هنوز چیزی نفرستاده)');
      return route.fulfill({ status: 404, contentType: 'application/json', headers: { 'access-control-allow-origin': '*' }, body: JSON.stringify({ ok: false, error: { code: 'no_live', message: 'برنامهٔ کامپیوتر هنوز چیزی به ابر نفرستاده' } }) });
    }
    const r = await fetch(PUB + u.pathname + u.search, {
      method: r0.method(), headers: { 'content-type': 'application/json', 'x-app-id': r0.headers()['x-app-id'] || '', authorization: r0.headers().authorization || '' },
      body: ['GET', 'HEAD'].includes(r0.method()) ? undefined : r0.postData() || undefined,
    });
    let body = Buffer.from(await r.arrayBuffer());
    if (u.pathname === '/api/pump/public/join' && homeUrl !== undefined) {
      const j = JSON.parse(body.toString('utf8')); j.home.url = homeUrl; body = Buffer.from(JSON.stringify(j));
    }
    hits.push(`${r0.method()} ${u.pathname} ⇒ ${r.status}`);
    await route.fulfill({ status: r.status, body, headers: { 'content-type': r.headers.get('content-type') || 'application/json', 'access-control-allow-origin': '*' } });
  });
  //  تونل ⇒ همان سرورِ خانگیِ واقعی (پورتِ عمومی)
  await ctx.routeWebSocket(/^wss:\/\/api\.vill3n\.top\//, (wsr) => {
    const u = new URL(wsr.url());
    hits.push('WSS تونل ' + u.pathname);
    const up = new WS(WS_PUB + u.pathname + u.search);
    const q = [];
    up.on('open', () => { while (q.length) up.send(q.shift()); });
    up.on('message', (d) => {
      let s = d.toString();
      if (liveEmpty) { try { const m = JSON.parse(s); if (m.op === 'event' && m.subId === 'live') { m.value = null; s = JSON.stringify(m); } } catch { } }
      wsr.send(s);
    });
    up.on('close', () => { try { wsr.close(); } catch { } });
    up.on('error', () => { try { wsr.close(); } catch { } });
    wsr.onMessage((m) => { if (up.readyState === 1) up.send(m); else q.push(m); });
    wsr.onClose(() => { try { up.close(); } catch { } });
  });

  let t0 = Date.now();
  if (process.env.KAR_DEBUG) {
    await page.addInitScript(() => {
      const W = window.WebSocket;
      window.WebSocket = function (u, p) { console.log('WS new ' + u); try { const w = new W(u, p); w.addEventListener('close', (e) => console.log('WS close ' + e.code)); w.addEventListener('open', () => console.log('WS open')); return w; } catch (e) { console.log('WS throw ' + e.name); throw e; } };
      const F = window.fetch; window.fetch = function (u, o) { console.log('FETCH ' + u); return F.apply(this, arguments); };
    });
    page.on('console', (m) => console.log('   · ' + (Date.now() - t0) + 'ms ' + m.text().slice(0, 160)));
  }
  await page.goto(origin);
  await page.fill('#inCode', kc.code);
  //  ⚠️ ساعت از زدنِ دکمه است، نه از بارِ صفحه: بارِ صفحه در سندباکس منتظرِ
  //  اسکریپتِ بیرونیِ گوگل می‌ماند و هیچ ربطی به اتصال ندارد.
  t0 = Date.now();
  await page.click('#btnJoin');
  //  برنامه باز شد ⇒ روی صفحهٔ دو در، «کارمندان» (همان کارِ کاربر) ⇒ نامِ قرض‌دار
  const opened = await page.waitForFunction(
    () => !document.getElementById('appPane').classList.contains('hidden'), null, { timeout: 30000 }
  ).then(() => true, () => false);
  if (opened && await page.locator('#paneHome:not(.hidden) button.door.staff').count())
    await page.click('#paneHome button.door.staff');
  const seen = opened && await page.waitForFunction(
    (name) => document.body.innerText.includes(name), kc.debtor, { timeout: 30000 }
  ).then(() => true, () => false);
  const ms = Date.now() - t0;
  const status = await page.evaluate(() => (document.getElementById('liveText') || {}).textContent || '');
  return { ctx, page, hits, seen, ms, status };
}

async function done(s, name) {
  await s.page.screenshot({ path: path.join(shots, name + '.png') });
  info('درخواست‌ها: ' + [...new Set(s.hits)].join(' · '));
  await s.ctx.close();
}

// ۱) آیفون: https + نشانیِ شبکهٔ پمپ از دیتای گوشی جواب نمی‌دهد
{
  const s = await scenario('آیفون (سافاری، https) بیرون از پمپ', { origin: HTTPS_BASE, homeUrl: 'http://192.0.2.2:4931' });
  ok(s.seen, `قرض‌دارِ همین پمپ دیده شد (${s.ms}ms) — «${s.status}»`);
  ok(s.ms < 8000, 'زیرِ هشت ثانیه، نه نیم‌دقیقه «در حالِ وصل شدن»');
  ok(/زنده/.test(s.status), 'چراغ «زنده» است — از تونلِ همان سرورِ خانگی، نه عکسِ کهنه');
  //  برنامهٔ کامپیوتر همین حالا یک قرض‌دارِ تازه می‌نویسد
  const snap = JSON.parse(JSON.stringify(baseLive));
  snap.seq = Date.now(); snap.at = 'تازه';
  (snap.debtors = snap.debtors || []).push({ ...(snap.debtors[0] || {}), id: 99999, name: 'قرض‌دارِ زندهٔ تازه' });
  await desktopPublish(snap);
  const fresh = await s.page.waitForFunction(() => {
    document.querySelector('[data-pane="paneDebt"]')?.click();
    return document.body.innerText.includes('قرض‌دارِ زندهٔ تازه');
  }, null, { timeout: 10000 }).then(() => true, () => false);
  ok(fresh, 'تغییرِ برنامهٔ کامپیوتر زیرِ ده ثانیه روی گوشی آمد');
  await done(s, 'real-1-iphone');
}

// ۲) اندروید روی دیتای گوشی: نشانیِ شبکهٔ پمپ سوکت را نگه می‌دارد و جواب نمی‌دهد
{
  const s = await scenario('اندروید (دیتای گوشی) — نشانیِ شبکهٔ پمپ آویزان', { origin: HTTP_BASE, homeUrl: HANG_URL });
  ok(s.seen, `قرض‌دارِ همین پمپ دیده شد (${s.ms}ms) — «${s.status}»`);
  ok(s.ms < 8000, 'آویزان ماندنِ نشانیِ شبکهٔ پمپ برنامه را نگه نداشت');
  await done(s, 'real-2-android');
}

// ۳) «وصل است ولی چیزی نمی‌آید»: در باز شد، عکسی نداد ⇒ عکسِ سرورِ حساب
{
  const s = await scenario('در باز است ولی عکسی نمی‌دهد', { origin: HTTPS_BASE, homeUrl: 'http://192.0.2.2:4931', liveEmpty: true });
  ok(s.seen, `با این حال قرض‌دارِ همین پمپ دیده شد (${s.ms}ms) — «${s.status}»`);
  await done(s, 'real-3-empty');
}

// ۴) هیچ عکسی هیچ‌جا نیست ⇒ راستش گفته شود، نه «در حالِ وصل شدن» تا ابد
{
  const s = await scenario('هیچ‌جا عکسی نیست', { origin: HTTPS_BASE, homeUrl: 'http://192.0.2.2:4931', liveEmpty: true, cloudLive: false });
  await s.page.waitForTimeout(6000);
  const st = await s.page.evaluate(() => (document.getElementById('liveText') || {}).textContent + ' | ' + (document.getElementById('lockState') || {}).textContent);
  ok(/هنوز|نفرستاده/.test(st), 'می‌گوید برنامهٔ کامپیوتر هنوز چیزی نفرستاده — «' + st + '»');
  await done(s, 'real-4-nothing');
}

await browser.close();
httpsSrv.close(); httpSrv.close(); black.close();
console.log(bad ? `\n❌ ${bad} ایراد` : '\n✅ اپِ گوشی در هر چهار حالِ واقعی داده را نشان داد');
process.exit(bad ? 1 : 0);
