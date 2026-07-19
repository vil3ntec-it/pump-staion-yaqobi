// ============================================================================
//  سرور شخصی پمپ یعقوبی — جایگزین فایربیس (Realtime Database)
//  Node.js (built-in http) + WebSocket (ws) + PostgreSQL (pg)
//
//  این سرور همان کارهایی را می‌کند که فایربیس Realtime Database می‌کرد:
//    • ذخیره/خواندن درختِ JSON  (set / get / update / remove / push)
//    • رویدادهای بلادرنگ         (on: value / child_added / child_changed / child_removed)
//    • حضور آنلاین               (onDisconnect: وقتی اتصال قطع شد، مسیر پاک/ست شود)
//    • .info/connected           (وضعیت اتصال)
//
//  کل درختِ داده در حافظه نگه داشته می‌شود (سریع) و هر شاخهٔ سطح‌اول به‌صورت
//  debounce در PostgreSQL ذخیره می‌شود تا با خاموش/روشن شدن سرور از بین نرود.
// ============================================================================

import http from 'node:http';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';
import pg from 'pg';

// ---------------------------------------------------------------------------
// خواندن فایل .env (بدون وابستگی) — اگر کنارِ همین فایل باشد
// ---------------------------------------------------------------------------
(function loadEnv() {
  try {
    const dir = path.dirname(fileURLToPath(import.meta.url));
    const envPath = path.join(dir, '.env');
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
  } catch (e) { /* بی‌خیال؛ از متغیرهای محیطی سیستم استفاده می‌شود */ }
})();

// ---------------------------------------------------------------------------
// تنظیمات (از متغیرهای محیطی؛ راهنما در README-fa.md و فایل .env.example)
// ---------------------------------------------------------------------------
const PORT = parseInt(process.env.PORT || '8787', 10);
const AUTH_TOKEN = process.env.AUTH_TOKEN || '';            // رمز اشتراکی؛ خالی یعنی بدون احراز هویت (توصیه نمی‌شود)
// آدرس دیتابیس: یا کلِ DATABASE_URL را بده، یا فقط تکه‌ها را (ساده‌تر — معمولاً فقط DB_PASSWORD کافی است)
function buildDbUrl() {
  if (process.env.DATABASE_URL) return process.env.DATABASE_URL;
  const host = process.env.DB_HOST || 'localhost';
  const port = process.env.DB_PORT || '5432';
  const user = process.env.DB_USER || 'postgres';
  const pass = process.env.DB_PASSWORD || 'postgres';
  const name = process.env.DB_NAME || 'pump_yaqobi';
  return 'postgresql://' + encodeURIComponent(user) + ':' + encodeURIComponent(pass) + '@' + host + ':' + port + '/' + name;
}
const DATABASE_URL = buildDbUrl();
const PERSIST_DEBOUNCE_MS = parseInt(process.env.PERSIST_DEBOUNCE_MS || '400', 10);

// شاخه‌های سطح‌اولی که جداگانه در دیتابیس ذخیره می‌شوند
const TOP_KEYS_SPECIAL = new Set(['stations', 'chat', 'backups', 'backupsIndex']);

const { Pool, Client } = pg;
const pool = new Pool({ connectionString: DATABASE_URL });

// اگر دیتابیسِ هدف هنوز ساخته نشده، خودمان می‌سازیمش (تا کاربر مجبور نباشد دستی
// CREATE DATABASE بزند). به دیتابیسِ پیش‌فرضِ «postgres» وصل می‌شویم و در صورت
// نبودن، دیتابیس را ایجاد می‌کنیم.
async function ensureDatabaseExists() {
  let dbName = 'pump_yaqobi';
  let adminUrl;
  try {
    const u = new URL(DATABASE_URL);
    dbName = decodeURIComponent(u.pathname.replace(/^\//, '')) || dbName;
    u.pathname = '/postgres';
    adminUrl = u.toString();
  } catch (e) { return; } // اگر URL قابل‌تجزیه نبود، بی‌خیال؛ مسیر عادی خطای واضح می‌دهد
  const admin = new Client({ connectionString: adminUrl });
  try {
    await admin.connect();
    const r = await admin.query('SELECT 1 FROM pg_database WHERE datname = $1', [dbName]);
    if (r.rowCount === 0) {
      await admin.query('CREATE DATABASE "' + dbName.replace(/"/g, '') + '"');
      console.log('[db] دیتابیس «' + dbName + '» وجود نداشت و ساخته شد.');
    }
  } catch (e) {
    console.warn('[db] ساختِ خودکار دیتابیس ممکن نشد (' + e.message + ') — اگر دیتابیس را دستی ساخته‌اید مشکلی نیست.');
  } finally {
    try { await admin.end(); } catch (e) {}
  }
}

// ---------------------------------------------------------------------------
//  درختِ داده در حافظه
// ---------------------------------------------------------------------------
const ROOT = {};                 // کل درخت realtime
const pendingPersist = new Map(); // topKey -> timer

function splitPath(path) {
  return String(path || '').split('/').filter(seg => seg.length > 0);
}

// خواندن مقدار در یک مسیر (کپیِ عمیق برنمی‌گرداند؛ فقط برای خواندن استفاده شود)
function getNode(path) {
  const segs = splitPath(path);
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

// نوشتن مقدار در مسیر. مقدار null/undefined → حذف آن مسیر.
function setNode(path, value) {
  const segs = splitPath(path);
  if (segs.length === 0) return; // نوشتن روی ریشه پشتیبانی نمی‌شود
  let cur = ROOT;
  for (let i = 0; i < segs.length - 1; i++) {
    const s = segs[i];
    if (cur[s] == null || typeof cur[s] !== 'object') cur[s] = {};
    cur = cur[s];
  }
  const last = segs[segs.length - 1];
  if (value === null || value === undefined) {
    delete cur[last];
  } else {
    cur[last] = value;
  }
}

// شناسهٔ push مثل فایربیس: ۲۰ کاراکتر، مرتب بر اساس زمان (لِکسیکوگرافیک = زمانی)
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
//  پایداری در PostgreSQL (debounce بر اساس شاخهٔ سطح‌اول)
// ---------------------------------------------------------------------------
function topKeyOf(path) {
  const segs = splitPath(path);
  return segs.length ? segs[0] : null;
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
  const val = ROOT[topKey];
  if (val === undefined) {
    await pool.query('DELETE FROM fb_store WHERE k = $1', [topKey]);
  } else {
    await pool.query(
      `INSERT INTO fb_store (k, v, updated_at) VALUES ($1, $2, now())
       ON CONFLICT (k) DO UPDATE SET v = EXCLUDED.v, updated_at = now()`,
      [topKey, JSON.stringify(val)]
    );
  }
}

async function loadFromDb() {
  const { rows } = await pool.query('SELECT k, v FROM fb_store');
  for (const r of rows) ROOT[r.k] = r.v;
  console.log(`[db] ${rows.length} شاخه از دیتابیس بازخوانی شد:`, rows.map(r => r.k).join(', ') || '(خالی)');
}

// ---------------------------------------------------------------------------
//  اشتراک‌ها و ارسال رویدادهای بلادرنگ
// ---------------------------------------------------------------------------
// هر اشتراک: { ws, path, event, subId, limit, snapChildren:Map<key,serialized>, snapValue }
const subscriptions = new Set();

function send(ws, obj) {
  if (ws.readyState === ws.OPEN) {
    try { ws.send(JSON.stringify(obj)); } catch (e) {}
  }
}

function serialize(v) {
  return v === undefined ? ' undef' : JSON.stringify(v);
}

// کلیدهای فرزندِ مستقیمِ یک مسیر (مرتب‌شده صعودی = ترتیب زمانیِ pushId)
function childKeysSorted(path) {
  const node = getNode(path);
  if (node == null || typeof node !== 'object' || Array.isArray(node)) return [];
  return Object.keys(node).sort();
}

// ارزیابیِ اولیهٔ یک اشتراک هنگام ساخت و ارسال وضعیت فعلی
function primeSubscription(sub) {
  if (sub.event === 'value') {
    const v = getNode(sub.path);
    sub.snapValue = serialize(v);
    send(sub.ws, { op: 'event', subId: sub.subId, type: 'value', key: lastSeg(sub.path), value: deepClone(v) ?? null });
  } else {
    // child_*  — وضعیت اولیه را با child_added بفرست
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
    // برای child_changed/child_removed وضعیت اولیه رویداد ندارد، فقط snapshot را نگه می‌داریم
    if (sub.event !== 'child_added') {
      // ولی هنوز باید کلیدهای فعلی را بشناسیم؛ snapChildren پر شد. تمام.
    }
  }
}

function lastSeg(path) {
  const segs = splitPath(path);
  return segs.length ? segs[segs.length - 1] : null;
}

// آیا a پیشوندِ مسیرِ b است (یا برابر)؟  segments-based
function isPrefixOrEqual(aSegs, bSegs) {
  if (aSegs.length > bSegs.length) return false;
  for (let i = 0; i < aSegs.length; i++) if (aSegs[i] !== bSegs[i]) return false;
  return true;
}

// پس از هر تغییر در مسیرِ changedPath، اشتراک‌های مرتبط را دوباره ارزیابی کن
function dispatch(changedPath) {
  const cSegs = splitPath(changedPath);
  for (const sub of subscriptions) {
    const sSegs = splitPath(sub.path);
    // مرتبط بودن: مسیرِ اشتراک و مسیرِ تغییر روی یک خط از ریشه‌اند (یکی پیشوندِ دیگری)
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

  // added / changed
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
  // removed — فقط وقتی واقعاً از داده حذف شده (نه صرفاً از پنجرهٔ limit خارج شده)
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
function applyMutation(kind, path, value) {
  if (kind === 'set') {
    setNode(path, value);
    schedulePersist(topKeyOf(path));
    dispatch(path);
  } else if (kind === 'remove') {
    setNode(path, null);
    schedulePersist(topKeyOf(path));
    dispatch(path);
  } else if (kind === 'update') {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      for (const k of Object.keys(value)) setNode(path + '/' + k, value[k]);
      schedulePersist(topKeyOf(path));
      // هر فرزند را جدا dispatch کن تا child_changed درست بیفتد
      for (const k of Object.keys(value)) dispatch(path + '/' + k);
      dispatch(path);
    }
  } else if (kind === 'push') {
    const key = genPushId();
    setNode(path + '/' + key, value);
    schedulePersist(topKeyOf(path));
    dispatch(path + '/' + key);
    return key;
  }
}

// ---------------------------------------------------------------------------
//  onDisconnect — عملیاتی که هنگام قطع اتصال اجرا می‌شود (برای presence)
// ---------------------------------------------------------------------------
function runOnDisconnect(ws) {
  const ops = ws._onDisc;
  if (!ops) return;
  for (const [path, spec] of ops) {
    try {
      if (spec.action === 'remove') applyMutation('remove', path, null);
      else if (spec.action === 'set') applyMutation('set', path, spec.value);
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

  // احراز هویت با توکن در query یا هدر
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

  // اعلام اتصال موفق → در سمت کلاینت .info/connected = true
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
//  راه‌اندازی
// ---------------------------------------------------------------------------
async function main() {
  await ensureDatabaseExists();  // اگر دیتابیس نبود، بساز
  await pool.query('SELECT 1');  // تست اتصال دیتابیس
  await ensureSchema();
  await loadFromDb();
  httpServer.listen(PORT, () => {
    console.log(`\n✅ سرور پمپ یعقوبی روی پورت ${PORT} بالا آمد.`);
    console.log(`   WebSocket: ws://localhost:${PORT}${AUTH_TOKEN ? '?token=***' : ''}`);
    console.log(`   سلامت:     http://localhost:${PORT}/health`);
    if (!AUTH_TOKEN) console.log('   ⚠️  AUTH_TOKEN تنظیم نشده — سرور بدون رمز است. برای اینترنت حتماً تنظیمش کنید.');
    console.log('');
  });
}

async function ensureSchema() {
  await pool.query(`
    CREATE TABLE IF NOT EXISTS fb_store (
      k TEXT PRIMARY KEY,
      v JSONB NOT NULL,
      updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
    );
  `);
  await pool.query('CREATE INDEX IF NOT EXISTS fb_store_updated_idx ON fb_store (updated_at);');
}

// ذخیرهٔ نهایی هنگام خاموش شدن تمیز
async function flushAll() {
  for (const [topKey, t] of pendingPersist) { clearTimeout(t); try { await persistNow(topKey); } catch (e) {} }
  pendingPersist.clear();
}
process.on('SIGINT', async () => { console.log('\n[خاموش‌سازی] ذخیرهٔ نهایی...'); await flushAll(); process.exit(0); });
process.on('SIGTERM', async () => { await flushAll(); process.exit(0); });

main().catch(err => {
  console.error('❌ راه‌اندازی سرور شکست خورد:', err.message);
  console.error('   مطمئن شوید PostgreSQL روشن است و DATABASE_URL درست است.');
  process.exit(1);
});
