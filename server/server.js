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
const DATA_DIR = path.join(__dirname, 'data');
let AUTH_TOKEN = process.env.AUTH_TOKEN || '';   // اگر خالی بماند، خودکار ساخته و در data/token.txt ذخیره می‌شود

// ---------------------------------------------------------------------------
//  درختِ داده در حافظه
// ---------------------------------------------------------------------------
const ROOT = {};                  // کل درخت realtime
const pendingPersist = new Map(); // topKey -> timer

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

function setNode(p, value) {
  const segs = splitPath(p);
  if (segs.length === 0) return;
  let cur = ROOT;
  for (let i = 0; i < segs.length - 1; i++) {
    const s = segs[i];
    if (cur[s] == null || typeof cur[s] !== 'object') cur[s] = {};
    cur = cur[s];
  }
  const last = segs[segs.length - 1];
  if (value === null || value === undefined) delete cur[last];
  else cur[last] = value;
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
  if (pendingPersist.has(topKey)) clearTimeout(pendingPersist.get(topKey));
  const t = setTimeout(() => {
    pendingPersist.delete(topKey);
    persistNow(topKey).catch(err => console.error('[persist]', topKey, err.message));
  }, PERSIST_DEBOUNCE_MS);
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
  const node = getNode(p);
  if (node == null || typeof node !== 'object' || Array.isArray(node)) return [];
  return Object.keys(node).sort();
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
  for (const sub of subscriptions) {
    const sSegs = splitPath(sub.path);
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
}

function reevalChildSub(sub) {
  let keys = childKeysSorted(sub.path);
  const fullSet = new Set(keys);
  if (sub.limit && keys.length > sub.limit) keys = keys.slice(keys.length - sub.limit);
  const windowSet = new Set(keys);
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
    if (!windowSet.has(k) && !fullSet.has(k)) {
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

const wss = new WebSocketServer({ server: httpServer });

wss.on('connection', (ws, req) => {
  ws._authed = AUTH_TOKEN === '';
  ws._onDisc = null;

  if (AUTH_TOKEN) {
    try {
      const url = new URL(req.url, 'http://x');
      const t = url.searchParams.get('token') || (req.headers['authorization'] || '').replace(/^Bearer\s+/i, '');
      ws._authed = safeEqual(t, AUTH_TOKEN);
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
    handleMessage(ws, m);
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
  AUTH_TOKEN = crypto.randomBytes(18).toString('hex'); // ۳۶ کاراکتر
  try { await fsp.writeFile(tokenFile, AUTH_TOKEN, 'utf8'); } catch (e) {}
}

// ---------------------------------------------------------------------------
//  راه‌اندازی
// ---------------------------------------------------------------------------
async function main() {
  await ensureAuthToken();
  await loadFromDisk();
  httpServer.listen(PORT, () => {
    console.log('');
    console.log('==================================================');
    console.log(' ✅ سرور شخصی پمپ یعقوبی بالا آمد');
    console.log('==================================================');
    console.log(' پورت:  ' + PORT);
    console.log(' تست سلامت در مرورگر:  http://localhost:' + PORT + '/health');
    console.log('');
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
}
process.on('SIGINT', async () => { console.log('\n[خاموش‌سازی] ذخیرهٔ نهایی...'); await flushAll(); process.exit(0); });
process.on('SIGTERM', async () => { await flushAll(); process.exit(0); });

main().catch(err => {
  console.error('❌ راه‌اندازی سرور شکست خورد:', err.message);
  process.exit(1);
});
