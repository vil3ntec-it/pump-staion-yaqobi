'use strict';
/**
 * باتِ تلگرامِ پمپ — وصل شدن، هشدار، گروه، و «جای نشتی نکنه».
 *
 * ── خواستهٔ صاحب مخزن (۱۴۰۵/۰۷/۱۳) ──────────────────────────────────
 * «بات تلگرام رو روی سرور بساز و برای پمپ باشد و دیگه جای نشتی نکنه…
 *  استارت رو که زدن بگه حساب کاربری شونو وارد کنن… و سرور پیدا کنه و به
 *  اون حساب متصل بشه… قرض‌داری که حسابش تموم شده و مخزن اگه تیل تموم شد
 *  پیام هشدار بره درجا… دکمه برای اندروید و آیفون… و گروه تلگرام.»
 *
 * ⚠️ تلگرام از ماشینِ آزمون در دسترس نیست؛ `telegram.setTransport` جایش
 * را می‌گیرد — همان الگوی `push.setDeliver`. هر فراخوانِ API این‌جا ثبت
 * می‌شود و آزمون همان را می‌سنجد.
 */
const test = require('node:test');
const assert = require('node:assert/strict');
const h = require('./helpers');
const { query, one, many, newId, now } = require('../src/db');
const telegram = require('../src/lib/bots').pump();
const otp = require('../src/lib/otp');

const TOKEN = `123456789:${'Ab_c-'.repeat(8)}`;
const BOT = 'PumpTestBot';

let calls = [];
let responder = null;
let updateId = 1000;

test.before(async () => {
  await h.start();
  const pw = require('../src/lib/password');
  await query(
    `INSERT INTO admins (id, username, name, password_hash, role, status, created_at)
     VALUES ($1,'admin','مدیر',$2,'superadmin','active',$3)`,
    [newId('adm'), await pw.hashPassword('Admin!12345'), now()]
  );
  telegram.setTransport(async (method, params, tok) => {
    calls.push({ method, params, tok });
    if (responder) {
      const r = await responder(method, params);
      if (r) return r;
    }
    if (method === 'getMe') return { ok: true, result: { id: 42, is_bot: true, username: BOT } };
    if (method === 'sendMessage') return { ok: true, result: { message_id: calls.length } };
    if (method === 'getUpdates') return { ok: true, result: [] };
    return { ok: true, result: true };
  });
});
test.after(async () => {
  telegram.setTransport(null);
  await h.stop();
});

const adminToken = async () =>
  (await h.post('/api/admin/login', { username: 'admin', password: 'Admin!12345' })).body.token;

const sent = () => calls.filter(c => c.method === 'sendMessage');
/*
 *  ⚠️ «آن‌چه کاربر همین حالا می‌بیند»: دکمه‌ها از ۱۴۰۵/۰۷/۱۴ همان پیام را
 *  ویرایش می‌کنند (`editMessageText`)، پس صفحهٔ تازه یا پیامِ نو است یا
 *  ویرایشِ همان پیام.
 */
const shown = () => calls.filter(c => c.method === 'sendMessage' || c.method === 'editMessageText');
const lastTo = (chatId) => shown().filter(c => c.params.chat_id === String(chatId)).at(-1);
const clear = () => { calls = []; };

function msg(chatId, text, { type = 'private', title = '' } = {}) {
  updateId += 1;
  const chat = type === 'private'
    ? { id: chatId, type, first_name: 'کاربر' }
    : { id: chatId, type, title: title || 'گروهِ پمپ' };
  return telegram.handleUpdate({ update_id: updateId, message: { message_id: updateId, chat, text } });
}

function press(chatId, data, { type = 'private' } = {}) {
  updateId += 1;
  return telegram.handleUpdate({
    update_id: updateId,
    callback_query: { id: `cb${updateId}`, data, message: { message_id: 1, chat: { id: chatId, type } } },
  });
}

async function pumpOwner(name) {
  const u = await h.newUser(name, 'pump');
  const made = await h.post('/api/pump', { name: `پمپِ ${name}` }, { token: u.accessToken });
  assert.equal(made.status, 201, JSON.stringify(made.body));
  return { ...u, stationId: made.body.station.id, stationName: made.body.station.name };
}

async function bindDevice(owner, uid) {
  const r = await h.post('/api/pump/device/bind',
    { device: { uid, name: 'کامپیوترِ پمپ', platform: 'windows' } },
    { token: owner.accessToken });
  assert.equal(r.status, 201, JSON.stringify(r.body));
  return r.body.deviceToken;
}

/** همان کدی که به ایمیل رفت — از دفترِ کد، مثلِ میزِ مدیر. */
async function codeOf(email) {
  const row = await one(
    `SELECT id FROM otp_codes WHERE destination=$1 AND purpose=$2 ORDER BY created_at DESC LIMIT 1`,
    [email, telegram.PURPOSE]
  );
  if (!row) return '';
  return (await otp.reveal(row.id)).code;
}

const toFa = (s) => String(s).replace(/\d/g, d => '۰۱۲۳۴۵۶۷۸۹'[Number(d)]);

/** یک گفت‌وگوی خصوصی را تا ته به پمپِ صاحبش وصل می‌کند. */
async function linkPrivate(chatId, owner) {
  await msg(chatId, '/start');
  await msg(chatId, owner.email);
  const code = await codeOf(owner.email);
  assert.match(code, /^\d{6}$/);
  await msg(chatId, code);
  const row = await one('SELECT * FROM telegram_chats WHERE chat_id=$1', [String(chatId)]);
  assert.equal(row.station_id, owner.stationId);
  return row;
}

async function alert(deviceToken, events) {
  const r = await h.post('/api/pump/device/events', { events }, { token: deviceToken });
  assert.equal(r.status, 201, JSON.stringify(r.body));
  return r.body;
}

/* ══════════════════════════════════════════════════════════════════
   ۰) تا رمز داده نشده، بات هیچ کاری نمی‌کند
   ══════════════════════════════════════════════════════════════════ */

test('بی رمزِ بات، هیچ هشداری در صف نمی‌نشیند و درِ عمومی «نیست» می‌گوید', async () => {
  const owner = await pumpOwner('بی‌رمز');
  const dev = await bindDevice(owner, 'pc-off');
  await alert(dev, [{ kind: 'debt', title: 'کریم — پطرولِ حسابش تمام شد', clientId: 'off-1', data: { state: 'out' } }]);
  const { n } = await one('SELECT COUNT(*)::int AS n FROM telegram_outbox');
  assert.equal(n, 0);
  const pub = await h.get('/api/pump/public/telegram');
  assert.equal(pub.status, 200);
  assert.equal(pub.body.enabled, false);
});

/* ══════════════════════════════════════════════════════════════════
   ۱) پنلِ مدیر: رمز فقط این‌جا، و هرگز بیرون
   ══════════════════════════════════════════════════════════════════ */

test('رمزِ بدشکل و رمزی که تلگرام نپذیرفت ذخیره نمی‌شوند', async () => {
  const token = await adminToken();
  const bad = await h.put('/api/admin/telegram', { token: 'not-a-token' }, { token });
  assert.equal(bad.status, 400);
  assert.equal(bad.body.error.code, 'bad_token');

  responder = (m) => (m === 'getMe' ? { ok: false, error_code: 401, description: 'Unauthorized' } : null);
  const rejected = await h.put('/api/admin/telegram', { token: TOKEN }, { token });
  responder = null;
  assert.equal(rejected.status, 400);
  assert.equal(rejected.body.error.code, 'token_rejected');
  const got = await h.get('/api/admin/telegram', { token });
  assert.equal(got.body.telegram.configured, false);
});

test('رمزِ درست ذخیره می‌شود — رمزشده در دیتابیس، و هیچ پاسخی آن را برنمی‌گرداند', async () => {
  const token = await adminToken();
  const saved = await h.put('/api/admin/telegram', { token: TOKEN }, { token });
  assert.equal(saved.status, 200, JSON.stringify(saved.body));
  assert.equal(saved.body.checked, true);
  assert.equal(saved.body.telegram.configured, true);
  assert.equal(saved.body.telegram.username, BOT);
  assert.equal(saved.body.telegram.tokenHint, `…${TOKEN.slice(-4)}`);

  //  ⛔ رمز در هیچ پاسخی نیست
  const everything = JSON.stringify([
    saved.body,
    (await h.get('/api/admin/telegram', { token })).body,
    (await h.get('/api/admin/plans', { token })).body,
    (await h.get('/api/pump/public/telegram')).body,
  ]);
  assert.ok(!everything.includes(TOKEN.split(':')[1]), 'رمزِ بات در یک پاسخ آمد');

  //  ⛔ و روی دیسک هم خام نیست
  const row = await one("SELECT value FROM app_config WHERE key='telegram_token'");
  assert.ok(row.value.startsWith('v1.'), 'رمزِ بات خام در app_config نشست');
  assert.ok(!row.value.includes(TOKEN.split(':')[1]));

  const pub = await h.get('/api/pump/public/telegram');
  assert.deepEqual(pub.body, { enabled: true, username: BOT, url: `https://t.me/${BOT}` });
});

/* ══════════════════════════════════════════════════════════════════
   ۲) وصل شدن در خصوصی: ایمیل ⇒ کد به همان ایمیل ⇒ وصل
   ══════════════════════════════════════════════════════════════════ */

test('/start خوش‌آمد می‌گوید، ایمیل می‌خواهد و دکمهٔ اندروید و آیفون دارد', async () => {
  clear();
  await msg(5001, '/start');
  const m = lastTo(5001);
  assert.ok(m, 'پاسخی نیامد');
  assert.match(m.params.text, /ایمیل/);
  const buttons = m.params.reply_markup.inline_keyboard.flat();
  const urls = buttons.map(b => b.url);
  assert.ok(urls.includes('https://yaqobipump.top/downloads/PumpYaqobiKar.apk'), 'لینکِ فایلِ نصبِ اندروید نیست');
  assert.ok(urls.includes('https://yaqobipump.top/kar/'), 'لینکِ آیفون نیست');
  //  ⛔ فایلِ نصبِ سایتِ قدیم هرگز
  assert.ok(!urls.some(u => /PumpYaqobi\.apk$/.test(u)));
});

test('ایمیلِ درست ⇒ کد به همان ایمیل می‌رود و با رقمِ فارسی هم پذیرفته می‌شود', async () => {
  const owner = await pumpOwner('صاحب');
  clear();
  await msg(5002, '/start');
  await msg(5002, 'not-an-email');
  assert.match(lastTo(5002).params.text, /درست نیست/);

  await msg(5002, `  ${owner.email.toUpperCase()}  `);
  assert.match(lastTo(5002).params.text, /کدِ شش‌رقمی/);
  const code = await codeOf(owner.email);
  assert.match(code, /^\d{6}$/, 'کدی برای این ایمیل ساخته نشد');

  const wrong = String((Number(code) + 1) % 1000000).padStart(6, '0');
  await msg(5002, wrong);
  assert.match(lastTo(5002).params.text, /درست نیست/);
  let row = await one('SELECT * FROM telegram_chats WHERE chat_id=$1', ['5002']);
  assert.equal(row.station_id, null);

  await msg(5002, toFa(code));
  row = await one('SELECT * FROM telegram_chats WHERE chat_id=$1', ['5002']);
  assert.equal(row.station_id, owner.stationId);
  assert.equal(row.state, '');
  assert.match(lastTo(5002).params.text, /وصل شد/);
  assert.match(lastTo(5002).params.text, new RegExp(owner.stationName));
});

test('⛔ ایمیلی که حساب یا پمپ ندارد همان پاسخ را می‌گیرد و هیچ کدی نمی‌رود', async () => {
  const shopOnly = await h.newUser('دکان‌دار', 'shop');     // حساب هست، پمپ نه
  const owner = await pumpOwner('مقایسه');
  clear();

  await msg(5003, '/start');
  await msg(5003, owner.email);
  const real = lastTo(5003).params.text.replace(owner.email, '<E>');

  for (const [chat, email] of [[5004, 'nobody@nowhere.test'], [5005, shopOnly.email]]) {
    await msg(chat, '/start');
    await msg(chat, email);
    assert.equal(lastTo(chat).params.text.replace(email, '<E>'), real, 'پاسخ لو داد که این ایمیل حساب ندارد');
    assert.equal(await codeOf(email), '', 'برای ایمیلِ بی‌پمپ کد رفت');
    await msg(chat, '123456');
    assert.match(lastTo(chat).params.text, /درست نیست/);
    const row = await one('SELECT station_id FROM telegram_chats WHERE chat_id=$1', [String(chat)]);
    assert.equal(row.station_id, null);
  }
});

test('⛔ از یک گفت‌وگو بیش از پنج درخواستِ کد در ساعت نمی‌رود', async () => {
  const owner = await pumpOwner('پرکار');
  await msg(5006, '/start');
  for (let i = 0; i < 5; i += 1) await msg(5006, owner.email);
  const before = (await one('SELECT COUNT(*)::int AS n FROM otp_codes WHERE destination=$1', [owner.email])).n;
  clear();
  await msg(5006, owner.email);
  assert.match(lastTo(5006).params.text, /زیاد شد/);
  const after = (await one('SELECT COUNT(*)::int AS n FROM otp_codes WHERE destination=$1', [owner.email])).n;
  assert.equal(after, before);
});

/* ══════════════════════════════════════════════════════════════════
   ۳) هشدار: درجا، یک پیام برای هر دسته، متنِ ساده
   ══════════════════════════════════════════════════════════════════ */

test('حسابِ قرض‌دار و مخزن ⇒ یک پیام در تلگرام، با نامِ پمپ و هر هشدار', async () => {
  const owner = await pumpOwner('هشدار');
  const dev = await bindDevice(owner, 'pc-alert');
  await linkPrivate(6001, owner);
  clear();

  await alert(dev, [
    { kind: 'debt', title: 'کریم — پطرولِ حسابش تمام شد، اضافه نده', clientId: 'a1', data: { state: 'out' } },
    { kind: 'debt', title: 'نبی — دیزلِ حسابش کم مانده', clientId: 'a2', data: { state: 'low' } },
    { kind: 'stock_out', title: 'مخزنِ پطرول ته کشید — 120 لیتر', clientId: 'a3', data: { state: 'out' } },
    { kind: 'sale', title: 'فروش — نباید برود', clientId: 'a4' },
  ]);
  const out = await telegram.flushOutbox();
  assert.equal(out.sent, 1);
  const m = lastTo(6001);
  assert.ok(m, 'هشداری نرفت');
  assert.match(m.params.text, new RegExp(owner.stationName));
  assert.match(m.params.text, /👤🔴 کریم/);
  assert.match(m.params.text, /👤🟡 نبی/);
  assert.match(m.params.text, /🛢️🔴 مخزنِ پطرول/);
  assert.doesNotMatch(m.params.text, /فروش/);
  //  ⛔ متنِ ساده — نامی که کاربر نوشته نمی‌تواند پیام را دست‌کاری کند
  assert.equal(m.params.parse_mode, undefined);

  //  همان خبر دوباره ⇒ هیچ پیامِ تازه‌ای
  clear();
  await alert(dev, [{ kind: 'debt', title: 'کریم — پطرولِ حسابش تمام شد، اضافه نده', clientId: 'a1' }]);
  await telegram.flushOutbox();
  assert.equal(sent().length, 0);
});

test('«فقط تمام شد» ⇒ «کم مانده» دیگر نمی‌آید', async () => {
  const owner = await pumpOwner('آرام');
  const dev = await bindDevice(owner, 'pc-calm');
  await linkPrivate(6002, owner);
  await press(6002, 'toggle');
  const row = await one('SELECT only_out FROM telegram_chats WHERE chat_id=$1', ['6002']);
  assert.equal(row.only_out, true);

  clear();
  await alert(dev, [{ kind: 'low_stock', title: 'مخزنِ دیزل دارد ته می‌کشد', clientId: 'c1', data: { state: 'low' } }]);
  await telegram.flushOutbox();
  assert.equal(sent().length, 0);

  await alert(dev, [{ kind: 'stock_out', title: 'مخزنِ دیزل ته کشید', clientId: 'c2', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  assert.match(lastTo(6002).params.text, /ته کشید/);
});

test('⛔ هشدارِ یک پمپ هرگز به گفت‌وگوی پمپِ دیگر نمی‌رسد', async () => {
  const a = await pumpOwner('الف');
  const b = await pumpOwner('ب');
  const devB = await bindDevice(b, 'pc-b');
  await linkPrivate(6003, a);
  await linkPrivate(6004, b);
  clear();
  await alert(devB, [{ kind: 'debt', title: 'فقط برای ب', clientId: 'b1', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '6003').length, 0);
  assert.equal(sent().filter(c => c.params.chat_id === '6004').length, 1);
});

test('⛔ کسی که از پمپ بیرون شد از همان لحظه هیچ هشداری نمی‌گیرد', async () => {
  const owner = await pumpOwner('بیرون');
  const dev = await bindDevice(owner, 'pc-out');
  await linkPrivate(6005, owner);
  await query("UPDATE station_members SET status='removed' WHERE user_id=$1", [owner.user.id]);

  clear();
  await alert(dev, [{ kind: 'debt', title: 'نباید برسد', clientId: 'x1', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '6005').length, 0);

  //  و فهرستِ «وضعیت» هم دیگر چیزی نشان نمی‌دهد
  await press(6005, 'status');
  assert.match(lastTo(6005).params.text, /وصل نیست/);
  const row = await one('SELECT station_id FROM telegram_chats WHERE chat_id=$1', ['6005']);
  assert.equal(row.station_id, null);
});

test('«وضعیت» آخرین هشدارهای همان پمپ را نشان می‌دهد', async () => {
  const owner = await pumpOwner('وضعیت');
  const dev = await bindDevice(owner, 'pc-st');
  await linkPrivate(6006, owner);
  await alert(dev, [{ kind: 'debt', title: 'رحیم — پولِ حسابش تمام شد', clientId: 's1', data: { state: 'out' } }]);
  clear();
  await press(6006, 'status');
  assert.match(lastTo(6006).params.text, /رحیم/);
});

test('قطعِ اتصال می‌پرسد و بعد واقعاً جدا می‌کند', async () => {
  const owner = await pumpOwner('جدا');
  const dev = await bindDevice(owner, 'pc-un');
  await linkPrivate(6007, owner);
  await press(6007, 'unlink');
  assert.match(lastTo(6007).params.text, /جدا شود/);
  let row = await one('SELECT station_id FROM telegram_chats WHERE chat_id=$1', ['6007']);
  assert.equal(row.station_id, owner.stationId, 'پیش از «بله» جدا شد');
  await press(6007, 'unlink_yes');
  row = await one('SELECT station_id FROM telegram_chats WHERE chat_id=$1', ['6007']);
  assert.equal(row.station_id, null);
  clear();
  await alert(dev, [{ kind: 'debt', title: 'نباید برسد', clientId: 'u1', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '6007').length, 0);
});

/* ══════════════════════════════════════════════════════════════════
   ۴) گروه: نشانیِ یک‌بارمصرف
   ══════════════════════════════════════════════════════════════════ */

function groupTokenFrom(message) {
  const url = message.params.reply_markup.inline_keyboard.flat().find(b => b.url)?.url || '';
  const m = new RegExp(`^https://t\\.me/${BOT}\\?startgroup=([A-Za-z0-9_-]+)$`).exec(url);
  assert.ok(m, `نشانیِ گروه درست نیست: ${url}`);
  return m[1];
}

test('گروه با دکمهٔ یک‌بارمصرف وصل می‌شود و هشدار برای همه می‌آید', async () => {
  const owner = await pumpOwner('گروه');
  const dev = await bindDevice(owner, 'pc-g');
  await linkPrivate(7001, owner);
  clear();
  await press(7001, 'group');
  const tok = groupTokenFrom(lastTo(7001));

  //  ⛔ خودِ نشانه روی دیسک نیست، فقط هشش
  const stored = await many('SELECT token_hash FROM telegram_link_tokens');
  assert.ok(stored.every(r => r.token_hash !== tok));

  await msg(-100777, `/start@${BOT} ${tok}`, { type: 'supergroup', title: 'کارمندانِ پمپ' });
  const g = await one('SELECT * FROM telegram_chats WHERE chat_id=$1', ['-100777']);
  assert.equal(g.kind, 'group');
  assert.equal(g.station_id, owner.stationId);
  assert.match(lastTo(-100777).params.text, /وصل شد/);

  //  ⛔ همان نشانی برای گروهِ دوم کار نمی‌کند
  await msg(-100888, `/start@${BOT} ${tok}`, { type: 'supergroup' });
  const g2 = await one('SELECT station_id FROM telegram_chats WHERE chat_id=$1', ['-100888']);
  assert.equal(g2.station_id, null);
  assert.match(lastTo(-100888).params.text, /باطل/);

  clear();
  await alert(dev, [{ kind: 'stock_out', title: 'مخزنِ پطرول ته کشید', clientId: 'g1', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  assert.ok(lastTo(-100777), 'هشدار به گروه نرسید');
  //  ⛔ گروه وصل است ⇒ همان خبر در خصوصی تکرار نمی‌شود (۲.۱۱.۲)
  assert.ok(!sent().some(c => c.params.chat_id === '7001' && /🚨/.test(c.params.text)), 'هم گروه هم خصوصی');
});

test('⛔ نشانیِ گروهِ کهنه (بیش از پانزده دقیقه) و گفت‌وگوی وصل‌نشده نشانی نمی‌گیرند', async () => {
  const owner = await pumpOwner('کهنه');
  await linkPrivate(7002, owner);
  await press(7002, 'group');
  const tok = groupTokenFrom(lastTo(7002));
  await query('UPDATE telegram_link_tokens SET expires_at=$1', [now() - 1000]);
  await msg(-100999, `/start ${tok}`, { type: 'group' });
  const g = await one('SELECT station_id FROM telegram_chats WHERE chat_id=$1', ['-100999']);
  assert.equal(g.station_id, null);

  //  گفت‌وگوی وصل‌نشده دکمهٔ گروه ندارد
  clear();
  await msg(7003, '/start');
  await press(7003, 'group');
  assert.ok(!shown().some(c => /startgroup/.test(JSON.stringify(c.params))));
});

test('فرمانی که به باتِ دیگری نشانی دارد نادیده گرفته می‌شود', async () => {
  clear();
  await msg(-100555, '/start@SomeOtherBot abc', { type: 'supergroup' });
  assert.equal(sent().length, 0);
});

test('بات را از گروه بیرون کنند ⇒ گروه فراموش می‌شود', async () => {
  const owner = await pumpOwner('بیرون‌کردن');
  await linkPrivate(7004, owner);
  await press(7004, 'group');
  const tok = groupTokenFrom(lastTo(7004));
  await msg(-100444, `/start ${tok}`, { type: 'group' });
  assert.ok(await one("SELECT 1 FROM telegram_chats WHERE chat_id='-100444' AND station_id IS NOT NULL"));
  updateId += 1;
  await telegram.handleUpdate({
    update_id: updateId,
    my_chat_member: { chat: { id: -100444, type: 'group' }, new_chat_member: { status: 'left' } },
  });
  assert.equal(await one("SELECT 1 FROM telegram_chats WHERE chat_id='-100444'"), null);
});

/* ══════════════════════════════════════════════════════════════════
   ۵) صف: قطعی، سقفِ نرخ، بات بسته، ابرگروه
   ══════════════════════════════════════════════════════════════════ */

async function queuedFor(chatId) {
  return one('SELECT * FROM telegram_outbox WHERE chat_id=$1 ORDER BY created_at DESC LIMIT 1', [String(chatId)]);
}

test('قطعیِ اینترنت هشدار را گم نمی‌کند — دوباره تلاش می‌شود', async () => {
  const owner = await pumpOwner('قطعی');
  const dev = await bindDevice(owner, 'pc-net');
  await linkPrivate(8001, owner);
  responder = (m) => (m === 'sendMessage' ? { ok: false, error_code: 0, description: 'fetch failed' } : null);
  await alert(dev, [{ kind: 'debt', title: 'در صف', clientId: 'n1', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  responder = null;
  let row = await queuedFor(8001);
  assert.equal(row.sent_at, null);
  assert.equal(row.attempts, 1);
  assert.ok(Number(row.next_at) > now(), 'تلاشِ بعدی زمان نگرفت');

  await query('UPDATE telegram_outbox SET next_at=0 WHERE id=$1', [row.id]);
  clear();
  await telegram.flushOutbox();
  row = await queuedFor(8001);
  assert.ok(row.sent_at, 'بارِ دوم هم نرفت');
  assert.match(lastTo(8001).params.text, /در صف/);
});

test('۴۲۹ ⇒ همان‌قدر که تلگرام گفت صبر', async () => {
  const owner = await pumpOwner('نرخ');
  const dev = await bindDevice(owner, 'pc-429');
  await linkPrivate(8002, owner);
  responder = (m) => (m === 'sendMessage'
    ? { ok: false, error_code: 429, description: 'Too Many Requests', parameters: { retry_after: 30 } } : null);
  await alert(dev, [{ kind: 'debt', title: 'صبر', clientId: 'r1', data: { state: 'out' } }]);
  const before = now();
  await telegram.flushOutbox();
  responder = null;
  const row = await queuedFor(8002);
  assert.equal(row.sent_at, null);
  assert.ok(Number(row.next_at) >= before + 29_000);
  await query('DELETE FROM telegram_outbox WHERE id=$1', [row.id]);
});

test('کاربر بات را بست (۴۰۳) ⇒ گفت‌وگو فراموش می‌شود', async () => {
  const owner = await pumpOwner('بسته');
  const dev = await bindDevice(owner, 'pc-403');
  await linkPrivate(8003, owner);
  responder = (m) => (m === 'sendMessage'
    ? { ok: false, error_code: 403, description: 'Forbidden: bot was blocked by the user' } : null);
  await alert(dev, [{ kind: 'debt', title: 'بسته', clientId: 'f1', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  responder = null;
  assert.equal(await one("SELECT 1 FROM telegram_chats WHERE chat_id='8003'"), null);
});

test('گروهی که ابرگروه شد با شناسهٔ تازه همان هشدار را می‌گیرد', async () => {
  const owner = await pumpOwner('ابرگروه');
  const dev = await bindDevice(owner, 'pc-mig');
  await linkPrivate(8004, owner);
  await press(8004, 'group');
  const tok = groupTokenFrom(lastTo(8004));
  await msg(-4001, `/start ${tok}`, { type: 'group' });
  responder = (m, p) => (m === 'sendMessage' && p.chat_id === '-4001'
    ? { ok: false, error_code: 400, description: 'group chat was upgraded', parameters: { migrate_to_chat_id: -1004001 } }
    : null);
  await alert(dev, [{ kind: 'stock_out', title: 'ابرگروه', clientId: 'm1', data: { state: 'out' } }]);
  await telegram.flushOutbox();
  responder = null;
  const g = await one("SELECT station_id FROM telegram_chats WHERE chat_id='-1004001'");
  assert.equal(g?.station_id, owner.stationId);
  clear();
  await telegram.flushOutbox();
  assert.match(lastTo(-1004001).params.text, /ابرگروه/);
});

/* ══════════════════════════════════════════════════════════════════
   ۶) حلقهٔ گرفتن و قیدهای کلی
   ══════════════════════════════════════════════════════════════════ */

test('getUpdates: هر پیام یک بار، و شمارهٔ بعدی نگه داشته می‌شود', async () => {
  const first = updateId + 1;
  updateId += 2;
  responder = (m) => (m === 'getUpdates' ? {
    ok: true,
    result: [
      { update_id: first, message: { message_id: 1, chat: { id: 9001, type: 'private' }, text: '/start' } },
      { update_id: first + 1, message: { message_id: 2, chat: { id: 9001, type: 'private' }, text: 'x@y' } },
    ],
  } : null);
  clear();
  const n = await telegram.pollOnce({ timeout: 0 });
  responder = null;
  assert.equal(n, 2);
  assert.equal(sent().filter(c => c.params.chat_id === '9001').length, 2);
  const off = await one("SELECT value FROM app_config WHERE key='telegram_offset'");
  assert.equal(off.value, String(first + 2));
  clear();
  await telegram.pollOnce({ timeout: 0 });
  assert.equal(calls.find(c => c.method === 'getUpdates').params.offset, first + 2);
});

test('⛔ رمزِ بات در هیچ پیامی که فرستاده شد نیست، و هیچ پیامی parse_mode ندارد', async () => {
  const all = JSON.stringify(sent().map(c => c.params));
  assert.ok(!all.includes(TOKEN.split(':')[1]));
  //  و در کلِ این آزمون هم
  assert.ok(calls.every(c => c.method !== 'sendMessage' || c.params.parse_mode === undefined));
});

test('خاموش کردن از پنل ⇒ هشدار در صف نمی‌نشیند', async () => {
  const token = await adminToken();
  const owner = await pumpOwner('خاموش');
  const dev = await bindDevice(owner, 'pc-offp');
  await linkPrivate(9101, owner);
  const off = await h.put('/api/admin/telegram', { enabled: false }, { token });
  assert.equal(off.status, 200);
  assert.equal(off.body.telegram.enabled, false);
  const before = (await one('SELECT COUNT(*)::int AS n FROM telegram_outbox')).n;
  await alert(dev, [{ kind: 'debt', title: 'خاموش', clientId: 'o1', data: { state: 'out' } }]);
  assert.equal((await one('SELECT COUNT(*)::int AS n FROM telegram_outbox')).n, before);
  await h.put('/api/admin/telegram', { enabled: true }, { token });
  telegram.stop();
});

test('نوشته‌های کمکی: رقمِ فارسی، ایمیل، فرمان', () => {
  assert.equal(telegram.asciiDigits('۱۲۳٤٥٦'), '123456');
  assert.equal(telegram.normEmail(' A@B.co '), 'a@b.co');
  assert.equal(telegram.normEmail('a@b'), '');
  assert.deepEqual(telegram.parseCommand('/start@Bot xyz', 'bot'), { name: 'start', arg: 'xyz' });
  assert.equal(telegram.parseCommand('/start@Other', 'bot'), null);
  assert.equal(telegram.parseCommand('سلام', 'bot'), null);
});

/* ══════════════════════════════════════════════════════════════════
   ۱۴۰۵/۰۷/۱۳ — عکس‌های صاحب سامانه: دو بار، منو، کانال، «کد نرفت»
   ══════════════════════════════════════════════════════════════════ */

const tick = (ms) => new Promise((r) => setTimeout(r, ms));

test('⛔ ذخیرهٔ دوبارهٔ رمز در پنل ⇒ هر پیام **یک** پاسخ، نه دو (حلقهٔ دوم زنده نمی‌ماند)', async () => {
  const token = await adminToken();
  await h.put('/api/admin/telegram', { token: TOKEN, enabled: true }, { token });
  updateId += 1;
  const id = updateId;
  let release;
  const gate = new Promise((r) => { release = r; });
  let firstPoll = true;
  //  هر getUpdatesی که پیش از ذخیره شدنِ شماره بیاید، همان پیام را می‌گیرد —
  //  درست مثلِ خودِ تلگرام
  responder = async (m, p) => {
    if (m !== 'getUpdates') return null;
    if (firstPoll) { firstPoll = false; await gate; }
    else await tick(20);
    return Number(p.offset) <= id
      ? { ok: true, result: [{ update_id: id, message: { message_id: 1, chat: { id: 9201, type: 'private' }, text: '/start' } }] }
      : { ok: true, result: [] };
  };
  clear();
  telegram.start();
  await tick(50);
  //  همان کاری که «ذخیره» در پنل می‌کند، وسطِ getUpdatesِ حلقهٔ نخست
  await h.put('/api/admin/telegram', { enabled: true }, { token });
  telegram.reload();
  release();
  await tick(400);
  telegram.stop();
  responder = null;
  const replies = sent().filter(c => c.params.chat_id === '9201');
  assert.equal(replies.length, 1, `پاسخ‌ها: ${replies.length}`);
  //  و منو: فرمان‌ها به تلگرام گفته شدند
  const cmds = calls.filter(c => c.method === 'setMyCommands');
  assert.ok(cmds.some(c => c.params.scope?.type === 'all_private_chats'
    && ['menu', 'status', 'find', 'code', 'group', 'help'].every(n => c.params.commands.some(x => x.command === n))),
  'فرمان‌های «منو»');
  assert.ok(calls.some(c => c.method === 'setChatMenuButton'), 'دکمهٔ منو');
});

test('⛔ یک به‌روزرسانی دو بار پاسخ نمی‌گیرد، حتی اگر دو بار برسد', async () => {
  updateId += 1;
  const u = { update_id: updateId, message: { message_id: 1, chat: { id: 9202, type: 'private' }, text: '/help' } };
  clear();
  await telegram.handleUpdate(u);
  await telegram.handleUpdate(u);
  assert.equal(sent().filter(c => c.params.chat_id === '9202').length, 1);
});

test('منو برای کسی که هنوز وصل نیست هم هست: راهنما و دکمه‌هایش', async () => {
  clear();
  await msg(9203, '/start');
  const kb = JSON.stringify(lastTo(9203).params.reply_markup);
  assert.ok(kb.includes('"help"'), 'دکمهٔ راهنما');
  await press(9203, 'help');
  const text = lastTo(9203).params.text;
  assert.ok(text.includes('کانال') && text.includes('گروه'), 'راهنما کانال و گروه را می‌گوید');
});

test('📣 کانال: صاحبِ وصل‌شده بات را مدیر می‌کند ⇒ کانال وصل و هشدار آن‌جا منتشر می‌شود', async () => {
  const owner = await pumpOwner('کانال');
  const dev = await bindDevice(owner, 'pc-ch');
  await linkPrivate(9301, owner);
  clear();
  await press(9301, 'channel');
  const how = lastTo(9301);
  assert.ok(JSON.stringify(how.params.reply_markup).includes('startchannel'), 'دکمهٔ «افزودن به کانال»');

  updateId += 1;
  clear();
  await telegram.handleUpdate({
    update_id: updateId,
    my_chat_member: {
      chat: { id: -1009301, type: 'channel', title: 'کانالِ پمپ' },
      from: { id: 9301, is_bot: false, first_name: 'صاحب' },
      old_chat_member: { status: 'left' },
      new_chat_member: { status: 'administrator', can_post_messages: true },
    },
  });
  const row = await one('SELECT * FROM telegram_chats WHERE chat_id=$1', ['-1009301']);
  assert.equal(row.kind, 'channel');
  assert.equal(row.station_id, owner.stationId);
  const inChannel = lastTo(-1009301);
  assert.ok(inChannel && inChannel.params.text.includes('وصل شد'), 'خودِ کانال خبر گرفت');
  assert.equal(inChannel.params.reply_markup, undefined, '⛔ در کانال دکمه نیست');
  assert.ok(lastTo(9301).params.text.includes('کانالِ پمپ'), 'صاحب هم خبر گرفت');

  await alert(dev, [{ kind: 'stock_out', title: 'مخزنِ دیزل تمام شد', clientId: 'ch-1', data: {} }]);
  const q = await one('SELECT COUNT(*)::int AS n FROM telegram_outbox WHERE chat_id=$1', ['-1009301']);
  assert.equal(q.n, 1, 'هشدار به صفِ کانال رفت');

  updateId += 1;
  await telegram.handleUpdate({
    update_id: updateId,
    my_chat_member: {
      chat: { id: -1009301, type: 'channel', title: 'کانالِ پمپ' },
      from: { id: 9301 }, new_chat_member: { status: 'left' },
    },
  });
  assert.equal(await one('SELECT * FROM telegram_chats WHERE chat_id=$1', ['-1009301']), null, 'برداشتن ⇒ جدا');
});

test('⛔ کسی که وصل نیست نمی‌تواند کانالی را به پمپی ببندد — فقط به خودش گفته می‌شود', async () => {
  updateId += 1;
  clear();
  await telegram.handleUpdate({
    update_id: updateId,
    my_chat_member: {
      chat: { id: -1009302, type: 'channel', title: 'کانالِ غریبه' },
      from: { id: 9302 }, new_chat_member: { status: 'administrator', can_post_messages: true },
    },
  });
  const row = await one('SELECT * FROM telegram_chats WHERE chat_id=$1', ['-1009302']);
  assert.ok(!row || !row.station_id, 'وصل نشد');
  assert.equal(lastTo(-1009302), undefined, 'در کانال چیزی نوشته نشد');
  assert.ok(lastTo(9302)?.params.text.includes('وصل نیستید'), 'به خودش گفته شد');
});

test('⛔ ایمیلی که حسابِ پمپ ندارد: پاسخِ بات همان است، ولی خودِ صندوق می‌گوید چرا کدی نیامد', async () => {
  const mailer = require('../src/lib/mailer');
  const orig = mailer.send;
  const mails = [];
  mailer.send = async (m) => { mails.push(m); return { delivered: true }; };
  try {
    clear();
    await msg(9401, '/start');
    await msg(9401, 'nobody-pump@example.com');
    await tick(50);
    const reply = lastTo(9401).params.text;
    assert.ok(reply.includes('اگر') && reply.includes('حسابِ پمپ داشته باشد'), 'پاسخِ یکسان (قیدِ ۲)');
    const m = mails.find(x => x.to === 'nobody-pump@example.com');
    assert.ok(m, 'نامه رفت');
    assert.equal(m.subject, 'این ایمیل به هیچ پمپی وصل نیست');
    assert.ok(m.html.includes('این ایمیل به هیچ پمپی وصل نیست'), 'همان مدلِ ایمیلِ پمپ');
    assert.ok(!/\d{6}/.test(m.subject), 'کدی در کار نیست');
  } finally {
    mailer.send = orig;
  }
});
