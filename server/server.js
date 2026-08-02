// ============================================================================
//  سرور شخصی پمپ یعقوبی — جایگزین فایربیس (Realtime Database)
//  فقط Node.js — بدون نیاز به PostgreSQL و بدون نیاز به npm install
//
//  این سرور همان کارهایی را می‌کند که فایربیس Realtime Database می‌کرد:
//    • ذخیره/خواندن درختِ JSON  (set / get / update / remove / push)
//    • رویدادهای بلادرنگ         (on: value / child_added / child_changed / child_removed)
//    • حضور آنلاین               (onDisconnect: وقتی اتصال قطع شد، مسیر پاک/ست شود)
//    • .info/connected           (وضعیت اتصال)
//
//  کل درختِ داده در حافظه نگه داشته می‌شود (سریع) و هر شاخهٔ سطح‌اول به‌صورت
//  یک فایل JSON در پوشهٔ data ذخیره می‌شود تا با خاموش/روشن شدن سرور از بین نرود.
//  کتابخانهٔ ws همراهِ همین پوشه است (در node_modules) — نیازی به npm install نیست.
// ============================================================================

import http from 'node:http';
import crypto from 'node:crypto';
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// ---------------------------------------------------------------------------
// خواندن فایل .env (اختیاری) — اگر کنارِ همین فایل باشد
// ---------------------------------------------------------------------------
(function loadEnv() {
  try {
    const envPath = path.join(__dirname, '.env');
    if (!fs.existsSync(envPath)) return;
    for (const rawLine of fs.readFileSync(envPath, 'utf8').split(/\r?\n/)) {
      const line = rawLine.trim();
      if (!line || line.startsWith('#')) continue;
      const eq = line.indexOf('=');
      if (eq < 0) continue;
      const key = line.slice(0, eq).trim();
      let val = line.slice(eq + 1).trim();
      if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) val = val.slice(1, -1);
      if (!(key in process.env)) process.env[key] = val;
    }
  } catch (e) { /* بی‌خیال */ }
})();

// ---------------------------------------------------------------------------
// تنظیمات
// ---------------------------------------------------------------------------
const PORT = parseInt(process.env.PORT || '8787', 10);
const PERSIST_DEBOUNCE_MS = parseInt(process.env.PERSIST_DEBOUNCE_MS || '400', 10);
// سقفِ عقب‌افتادنِ ذخیره روی دیسک: با ترافیکِ پیوسته، debounce تنها هر بار از نو
// شروع می‌شد و ذخیره تا آرام شدنِ کار عقب می‌افتاد (خطرِ از دست رفتنِ داده هنگام
// قطع برق). حالا حداکثر هر ۵ ثانیه به‌زور ذخیره می‌شود.
const PERSIST_MAX_DELAY_MS = parseInt(process.env.PERSIST_MAX_DELAY_MS || '5000', 10);
// بزرگ‌ترین پیامی که از یک کلاینت پذیرفته می‌شود — با سقفِ ۲۴MB رسانه در چت
// (که با base64 حدود ۳۲MB می‌شود) جا دارد و باز هم جلوی فریمِ غول‌پیکر را می‌گیرد
const MAX_PAYLOAD_BYTES = parseInt(process.env.MAX_PAYLOAD_BYTES || String(64 * 1024 * 1024), 10);
// مسیر ذخیرهٔ داده: به‌صورت پیش‌فرض کنار همین فایل، ولی اگر DATA_DIR در محیط تعیین
// شده باشد (مثلاً وقتی سرور داخل برنامهٔ دسکتاپ اجرا می‌شود و باید در پوشهٔ
// قابل‌نوشتنِ کاربر بنویسد) همان استفاده می‌شود.
const DATA_DIR = process.env.DATA_DIR ? path.resolve(process.env.DATA_DIR) : path.join(__dirname, 'data');
let AUTH_TOKEN = process.env.AUTH_TOKEN || '';   // اگر خالی بماند، از رمزِ داخلیِ برنامه برداشته و در data/token.txt ذخیره می‌شود
/* رمزِ داخلیِ خودِ برنامه — همان BUILTIN_TOKEN در index.html. اگر این دو با هم فرق
   کنند، هیچ دستگاهی نمی‌تواند وصل شود. این رمز از روزِ اول داخلِ index.htmlِ باز
   روی اینترنت است، پس پذیرفتنش چیزی را آشکارتر نمی‌کند؛ در عوض ساختنِ رمزِ
   تصادفیِ تازه با هر نصبِ دوباره، همهٔ دستگاه‌ها را بیرون می‌انداخت.
   برای رفتارِ سخت‌گیرانه: AUTH_TOKEN را خودتان تنظیم کنید یا STRICT_TOKEN=1 بگذارید. */
const APP_BUILTIN_TOKEN = '3b1379be26f61b2f8e460dbccee01ff70364';
const STRICT_TOKEN = String(process.env.STRICT_TOKEN || '') === '1' || !!process.env.AUTH_TOKEN;

// ---------------------------------------------------------------------------
//  درختِ داده در حافظه
// ---------------------------------------------------------------------------
const ROOT = {};                  // کل درخت realtime
const pendingPersist = new Map(); // topKey -> timer
const persistSince = new Map();   // topKey -> زمانِ اولین تغییرِ ذخیره‌نشده

function splitPath(p) {
  return String(p || '').split('/').filter(seg => seg.length > 0);
}

function getNode(p) {
  const segs = splitPath(p);
  let cur = ROOT;
  for (const s of segs) {
    if (cur == null || typeof cur !== 'object') return undefined;
    cur = cur[s];
  }
  return cur;
}

function deepClone(v) {
  return v === undefined ? undefined : JSON.parse(JSON.stringify(v));
}

/* ── فهرستِ مرتبِ کلیدهای هر مسیر ─────────────────────────────────────────────
   قبلاً برای هر پیامِ تازه، کلِ کلیدهای آن مسیر دوباره Object.keys و sort می‌شد.
   با ۲۰ هزار پیام در یک چت، هر پیامِ تازه یعنی مرتب کردنِ ۲۰ هزار کلید — سرور
   زیر بار می‌خوابید. حالا فهرستِ مرتب یک‌بار ساخته می‌شود و با هر افزودن/حذف
   فقط همان یک کلید در جای درستش جا داده می‌شود (جستجوی دودویی).
   نتیجه: سرعت دیگر به اندازهٔ تاریخچه وابسته نیست. ── */
const sortedCache = new Map();     // 'a/b/c' -> کلیدهای مرتبِ فرزندان

function normPath(p) { return splitPath(p).join('/'); }
function bsearch(arr, k) {         // اندیسِ k یا اندیسِ جای درستِ آن (منفی-۱)
  let lo = 0, hi = arr.length - 1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (arr[mid] === k) return mid;
    if (arr[mid] < k) lo = mid + 1; else hi = mid - 1;
  }
  return -lo - 1;
}
function sortedHas(arr, k) { return bsearch(arr, k) >= 0; }
function sortedAdd(parent, key) {
  const arr = sortedCache.get(parent);
  if (!arr) return;
  const i = bsearch(arr, key);
  if (i < 0) arr.splice(-i - 1, 0, key);
}
function sortedRemove(parent, key) {
  const arr = sortedCache.get(parent);
  if (!arr) return;
  const i = bsearch(arr, key);
  if (i >= 0) arr.splice(i, 1);
}

function setNode(p, value) {
  const segs = splitPath(p);
  if (segs.length === 0) return;
  let cur = ROOT;
  let prefix = '';                                  // مسیرِ خودِ cur
  for (let i = 0; i < segs.length - 1; i++) {
    const s = segs[i];
    if (cur[s] == null || typeof cur[s] !== 'object') {
      const existed = Object.prototype.hasOwnProperty.call(cur, s);
      cur[s] = {};
      if (!existed) sortedAdd(prefix, s);
      else sortedCache.delete(prefix ? prefix + '/' + s : s);   // نوعِ گره عوض شد
    }
    prefix = prefix ? prefix + '/' + s : s;
    cur = cur[s];
  }
  const last = segs[segs.length - 1];
  const had = Object.prototype.hasOwnProperty.call(cur, last);
  if (value === null || value === undefined) {
    if (had) { delete cur[last]; sortedRemove(prefix, last); sortedCache.delete(prefix ? prefix + '/' + last : last); }
  } else {
    cur[last] = value;
    if (!had) sortedAdd(prefix, last);
    sortedCache.delete(prefix ? prefix + '/' + last : last);     // فرزندانِ خودِ این گره عوض شدند
  }
}

// شناسهٔ push مثل فایربیس: ۲۰ کاراکتر، مرتب بر اساس زمان
const PUSH_CHARS = '-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz';
let _lastPushTime = 0;
let _lastRand = [];
function genPushId(now = Date.now()) {
  const dup = (now === _lastPushTime);
  _lastPushTime = now;
  const ts = new Array(8);
  for (let i = 7; i >= 0; i--) { ts[i] = PUSH_CHARS.charAt(now % 64); now = Math.floor(now / 64); }
  let id = ts.join('');
  if (!dup) { for (let i = 0; i < 12; i++) _lastRand[i] = Math.floor(Math.random() * 64); }
  else { let i = 11; for (; i >= 0 && _lastRand[i] === 63; i--) _lastRand[i] = 0; _lastRand[i]++; }
  for (let i = 0; i < 12; i++) id += PUSH_CHARS.charAt(_lastRand[i]);
  return id;
}

// ---------------------------------------------------------------------------
//  پایداری در فایل (هر شاخهٔ سطح‌اول = یک فایل JSON در پوشهٔ data)
// ---------------------------------------------------------------------------
function topKeyOf(p) {
  const segs = splitPath(p);
  return segs.length ? segs[0] : null;
}

function fileForKey(topKey) {
  const safe = String(topKey).replace(/[^a-zA-Z0-9_-]/g, '_');
  return path.join(DATA_DIR, safe + '.json');
}

function schedulePersist(topKey) {
  if (!topKey) return;
  if (!persistSince.has(topKey)) persistSince.set(topKey, Date.now());
  const waited = Date.now() - persistSince.get(topKey);
  if (pendingPersist.has(topKey)) clearTimeout(pendingPersist.get(topKey));
  const delay = Math.max(0, Math.min(PERSIST_DEBOUNCE_MS, PERSIST_MAX_DELAY_MS - waited));
  const t = setTimeout(() => {
    pendingPersist.delete(topKey);
    persistSince.delete(topKey);
    persistNow(topKey).catch(err => console.error('[persist]', topKey, err.message));
  }, delay);
  pendingPersist.set(topKey, t);
}

async function persistNow(topKey) {
  const file = fileForKey(topKey);
  const val = ROOT[topKey];
  if (val === undefined) {
    try { await fsp.unlink(file); } catch (e) { /* شاید از قبل نبود */ }
    return;
  }
  const tmp = file + '.tmp';
  await fsp.writeFile(tmp, JSON.stringify(val), 'utf8');
  await fsp.rename(tmp, file); // نوشتنِ اتمیک (روی ویندوز هم جایگزین می‌کند)
}

async function loadFromDisk() {
  await fsp.mkdir(DATA_DIR, { recursive: true });
  let files = [];
  try { files = await fsp.readdir(DATA_DIR); } catch (e) { return; }
  const loaded = [];
  for (const f of files) {
    if (!f.endsWith('.json')) continue;
    const key = f.slice(0, -5);
    try {
      const txt = await fsp.readFile(path.join(DATA_DIR, f), 'utf8');
      ROOT[key] = JSON.parse(txt);
      loaded.push(key);
    } catch (e) { console.warn('[data] خواندن', f, 'ناموفق:', e.message); }
  }
  sortedCache.clear();   // درخت مستقیم جایگزین شد → کشِ کلیدها از نو ساخته شود
  console.log(`[data] ${loaded.length} شاخه از فایل بازخوانی شد:`, loaded.join(', ') || '(خالی)');
}

// ---------------------------------------------------------------------------
//  اشتراک‌ها و ارسال رویدادهای بلادرنگ
// ---------------------------------------------------------------------------
const subscriptions = new Set();

function send(ws, obj) {
  if (ws.readyState === ws.OPEN) {
    try { ws.send(JSON.stringify(obj)); } catch (e) {}
  }
}

function serialize(v) {
  return v === undefined ? ' undef' : JSON.stringify(v);
}

function childKeysSorted(p) {
  const np = normPath(p);
  const hit = sortedCache.get(np);
  if (hit) return hit;
  const node = getNode(p);
  if (node == null || typeof node !== 'object' || Array.isArray(node)) return [];
  const keys = Object.keys(node).sort();
  sortedCache.set(np, keys);
  return keys;
}

function primeSubscription(sub) {
  if (sub.event === 'value') {
    const v = getNode(sub.path);
    sub.snapValue = serialize(v);
    send(sub.ws, { op: 'event', subId: sub.subId, type: 'value', key: lastSeg(sub.path), value: deepClone(v) ?? null });
  } else {
    let keys = childKeysSorted(sub.path);
    if (sub.limit && keys.length > sub.limit) keys = keys.slice(keys.length - sub.limit);
    sub.snapChildren = new Map();
    for (const k of keys) {
      const cv = getNode(sub.path + '/' + k);
      sub.snapChildren.set(k, serialize(cv));
      if (sub.event === 'child_added') {
        send(sub.ws, { op: 'event', subId: sub.subId, type: 'child_added', key: k, value: deepClone(cv) ?? null });
      }
    }
  }
}

function lastSeg(p) {
  const segs = splitPath(p);
  return segs.length ? segs[segs.length - 1] : null;
}

function isPrefixOrEqual(aSegs, bSegs) {
  if (aSegs.length > bSegs.length) return false;
  for (let i = 0; i < aSegs.length; i++) if (aSegs[i] !== bSegs[i]) return false;
  return true;
}

function dispatch(changedPath) {
  const cSegs = splitPath(changedPath);
  try {
    for (const sub of subscriptions) {
      const sSegs = sub.segs || (sub.segs = splitPath(sub.path));   // یک‌بار حساب، نه هر پیام
      const related = isPrefixOrEqual(sSegs, cSegs) || isPrefixOrEqual(cSegs, sSegs);
      if (!related) continue;

      if (sub.event === 'value') {
        const v = getNode(sub.path);
        const ser = serialize(v);
        if (ser !== sub.snapValue) {
          sub.snapValue = ser;
          send(sub.ws, { op: 'event', subId: sub.subId, type: 'value', key: lastSeg(sub.path), value: deepClone(v) ?? null });
        }
      } else {
        reevalChildSub(sub);
      }
    }
  } catch (e) {
    console.error('[dispatch]', e && e.message);   // یک اشتراکِ خراب نباید کلِ سرور را بخواباند
  }
}

function reevalChildSub(sub) {
  // all: فهرستِ مرتبِ همهٔ فرزندان (از کش، بدون مرتب‌سازی دوباره)
  // keys: فقط پنجرهٔ موردِ اشتراک (مثلاً ۶۰ پیام آخر) — کارِ واقعی همیشه کوچک می‌ماند
  const all = childKeysSorted(sub.path);
  const keys = (sub.limit && all.length > sub.limit) ? all.slice(all.length - sub.limit) : all;
  const prev = sub.snapChildren || new Map();
  const next = new Map();

  for (const k of keys) {
    const cv = getNode(sub.path + '/' + k);
    const ser = serialize(cv);
    next.set(k, ser);
    if (!prev.has(k)) {
      if (sub.event === 'child_added') {
        send(sub.ws, { op: 'event', subId: sub.subId, type: 'child_added', key: k, value: deepClone(cv) ?? null });
      }
    } else if (prev.get(k) !== ser) {
      if (sub.event === 'child_changed') {
        send(sub.ws, { op: 'event', subId: sub.subId, type: 'child_changed', key: k, value: deepClone(cv) ?? null });
      }
    }
  }
  for (const k of prev.keys()) {
    if (!next.has(k) && !sortedHas(all, k)) {       // واقعاً پاک شده (نه فقط از پنجره بیرون رفته)
      if (sub.event === 'child_removed') {
        send(sub.ws, { op: 'event', subId: sub.subId, type: 'child_removed', key: k, value: null });
      }
    }
  }
  sub.snapChildren = next;
}

// ---------------------------------------------------------------------------
//  اجرای mutation ها + پایداری + dispatch
// ---------------------------------------------------------------------------
function applyMutation(kind, p, value) {
  if (kind === 'set') {
    setNode(p, value);
    schedulePersist(topKeyOf(p));
    dispatch(p);
  } else if (kind === 'remove') {
    setNode(p, null);
    schedulePersist(topKeyOf(p));
    dispatch(p);
  } else if (kind === 'update') {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      for (const k of Object.keys(value)) setNode(p + '/' + k, value[k]);
      schedulePersist(topKeyOf(p));
      for (const k of Object.keys(value)) dispatch(p + '/' + k);
      dispatch(p);
    }
  } else if (kind === 'push') {
    const key = genPushId();
    setNode(p + '/' + key, value);
    schedulePersist(topKeyOf(p));
    dispatch(p + '/' + key);
    return key;
  }
}

// ---------------------------------------------------------------------------
//  onDisconnect — عملیاتی که هنگام قطع اتصال اجرا می‌شود (برای presence)
// ---------------------------------------------------------------------------
function runOnDisconnect(ws) {
  const ops = ws._onDisc;
  if (!ops) return;
  for (const [p, spec] of ops) {
    try {
      if (spec.action === 'remove') applyMutation('remove', p, null);
      else if (spec.action === 'set') applyMutation('set', p, spec.value);
    } catch (e) {}
  }
  ws._onDisc = null;
}

// ---------------------------------------------------------------------------
//  سرور HTTP (سلامت) + WebSocket
// ---------------------------------------------------------------------------
const httpServer = http.createServer((req, res) => {
  if (req.url === '/health' || req.url === '/') {
    res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
    res.end(JSON.stringify({ ok: true, service: 'pump-yaqobi-server', time: new Date().toISOString() }));
    return;
  }
  res.writeHead(404); res.end('not found');
});

// maxPayload: یک کلاینتِ خراب/بدخواه نتواند با یک فریمِ غول‌پیکر حافظهٔ سرور را پر کند
const wss = new WebSocketServer({ server: httpServer, maxPayload: MAX_PAYLOAD_BYTES });

wss.on('connection', (ws, req) => {
  ws._authed = AUTH_TOKEN === '';
  ws._onDisc = null;

  if (AUTH_TOKEN) {
    try {
      const url = new URL(req.url, 'http://x');
      const t = url.searchParams.get('token') || (req.headers['authorization'] || '').replace(/^Bearer\s+/i, '');
      ws._authed = safeEqual(t, AUTH_TOKEN) || (!STRICT_TOKEN && safeEqual(t, APP_BUILTIN_TOKEN));
    } catch (e) { ws._authed = false; }
  }

  if (!ws._authed) {
    send(ws, { op: 'error', msg: 'auth_failed' });
    ws.close();
    return;
  }

  send(ws, { op: 'connected' });

  ws.on('message', (raw) => {
    let m;
    try { m = JSON.parse(raw.toString()); } catch (e) { return; }
    if (!m || typeof m !== 'object') return;
    // هیچ پیامِ خرابی نباید سرور را بخواباند — خطا فقط ثبت می‌شود و کار ادامه می‌یابد
    try { handleMessage(ws, m); }
    catch (e) {
      console.error('[handleMessage]', m && m.op, e && e.message);
      try { send(ws, { op: 'ack', id: m.id, ok: false }); } catch (e2) {}
    }
  });

  ws.on('close', () => {
    runOnDisconnect(ws);
    for (const sub of [...subscriptions]) if (sub.ws === ws) subscriptions.delete(sub);
  });
  ws.on('error', () => {});
});

function handleMessage(ws, m) {
  switch (m.op) {
    case 'get': {
      const v = getNode(m.path);
      send(ws, { op: 'result', id: m.id, value: deepClone(v) ?? null });
      break;
    }
    case 'set': {
      applyMutation('set', m.path, m.value);
      send(ws, { op: 'ack', id: m.id, ok: true });
      break;
    }
    case 'update': {
      applyMutation('update', m.path, m.value);
      send(ws, { op: 'ack', id: m.id, ok: true });
      break;
    }
    case 'remove': {
      applyMutation('remove', m.path, null);
      send(ws, { op: 'ack', id: m.id, ok: true });
      break;
    }
    case 'push': {
      const key = applyMutation('push', m.path, m.value);
      send(ws, { op: 'ack', id: m.id, ok: true, key });
      break;
    }
    case 'sub': {
      const sub = {
        ws, path: m.path, event: m.event, subId: m.subId,
        limit: m.limitToLast || 0, snapChildren: null, snapValue: undefined
      };
      subscriptions.add(sub);
      primeSubscription(sub);
      break;
    }
    case 'unsub': {
      for (const sub of [...subscriptions]) if (sub.ws === ws && sub.subId === m.subId) subscriptions.delete(sub);
      break;
    }
    case 'onDisc': {
      if (!ws._onDisc) ws._onDisc = new Map();
      ws._onDisc.set(m.path, { action: m.action, value: m.value });
      break;
    }
    case 'onDiscCancel': {
      if (ws._onDisc) ws._onDisc.delete(m.path);
      break;
    }
    default:
      break;
  }
}

function safeEqual(a, b) {
  const ba = Buffer.from(String(a || ''));
  const bb = Buffer.from(String(b || ''));
  if (ba.length !== bb.length) return false;
  try { return crypto.timingSafeEqual(ba, bb); } catch (e) { return false; }
}

// ---------------------------------------------------------------------------
//  رمز خودکار — اگر AUTH_TOKEN تنظیم نشده باشد، یک‌بار می‌سازیم و در فایل نگه می‌داریم
// ---------------------------------------------------------------------------
async function ensureAuthToken() {
  if (AUTH_TOKEN) return;
  await fsp.mkdir(DATA_DIR, { recursive: true });
  const tokenFile = path.join(DATA_DIR, 'token.txt');
  try {
    const existing = (await fsp.readFile(tokenFile, 'utf8')).trim();
    if (existing) { AUTH_TOKEN = existing; return; }
  } catch (e) { /* هنوز ساخته نشده */ }
  AUTH_TOKEN = STRICT_TOKEN ? crypto.randomBytes(18).toString('hex') : APP_BUILTIN_TOKEN;
  try { await fsp.writeFile(tokenFile, AUTH_TOKEN, 'utf8'); } catch (e) {}
}

// ---------------------------------------------------------------------------
//  راه‌اندازی
// ---------------------------------------------------------------------------
// آدرس‌های شبکهٔ محلی (LAN) این کامپیوتر — برای وصل‌شدن از گوشی/دستگاه دیگر
function lanAddresses() {
  const out = [];
  try {
    const ifaces = os.networkInterfaces();
    for (const name of Object.keys(ifaces)) {
      for (const ni of ifaces[name] || []) {
        if (ni.family === 'IPv4' && !ni.internal) out.push(ni.address);
      }
    }
  } catch (e) { /* بی‌خیال */ }
  // آی‌پی‌های معمول خانگی را اول بیاور
  out.sort((a, b) => (b.startsWith('192.168.') - a.startsWith('192.168.')));
  return out;
}

async function main() {
  await ensureAuthToken();
  await loadFromDisk();
  httpServer.listen(PORT, () => {
    const ips = lanAddresses();
    console.log('');
    console.log('==================================================');
    console.log(' ✅ سرور شخصی پمپ یعقوبی بالا آمد');
    console.log('==================================================');
    console.log(' پورت:  ' + PORT);
    console.log('');
    console.log(' 🖥️  روی همین کامپیوتر (تست):');
    console.log('     http://localhost:' + PORT + '/health');
    console.log('');
    if (ips.length) {
      console.log(' 📱 از گوشی/تبلت (روی همان وای‌فای) — همین را در برنامه بگذار:');
      for (const ip of ips) {
        console.log('     آدرس سرور:  ws://' + ip + ':' + PORT);
        console.log('     تست مرورگر: http://' + ip + ':' + PORT + '/health');
      }
      console.log('');
    } else {
      console.log(' 📱 برای وصل‌شدن از گوشی، کامپیوتر باید به وای‌فای/شبکه وصل باشد.');
      console.log('');
    }
    console.log(' 👇 این «رمز سرور» را در برنامه (فیلد رمز سرور) وارد کنید:');
    console.log('    ' + AUTH_TOKEN);
    console.log('');
    console.log(' برای توقف سرور، این پنجره را ببندید یا Ctrl+C بزنید.');
    console.log('==================================================');
    console.log('');
  });
}

async function flushAll() {
  for (const [topKey, t] of pendingPersist) { clearTimeout(t); try { await persistNow(topKey); } catch (e) {} }
  pendingPersist.clear();
  persistSince.clear();
}

// ---------------------------------------------------------------------------
//  سرور هرگز نباید بمیرد: هر خطای پیش‌بینی‌نشده ثبت می‌شود و کار ادامه می‌یابد.
//  (قبلاً یک خطای ساده در یک درخواست، کلِ سرور — و در نتیجه چت و هم‌زمان‌سازیِ
//  همهٔ دستگاه‌ها — را می‌خواباند تا وقتی کسی دستی روشنش کند.)
// ---------------------------------------------------------------------------
process.on('uncaughtException', (err) => {
  console.error('[خطای پیش‌بینی‌نشده]', err && err.stack || err);
  flushAll().catch(() => {});
});
process.on('unhandledRejection', (reason) => {
  console.error('[promise رهاشده]', reason && reason.stack || reason);
});
process.on('SIGINT', async () => { console.log('\n[خاموش‌سازی] ذخیرهٔ نهایی...'); await flushAll(); process.exit(0); });
process.on('SIGTERM', async () => { await flushAll(); process.exit(0); });

main().catch(err => {
  console.error('❌ راه‌اندازی سرور شکست خورد:', err.message);
  process.exit(1);
});
