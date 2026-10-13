'use strict';
/**
 * باتِ تلگرامِ پمپ — 🔕 کنترلِ شلوغی و هشدارهای تازه (۱۴۰۵/۰۷/۲۱).
 *
 * خواستهٔ صاحب سامانه: «جوری نباشد که شلوغی ایجاد شود و صدها پیام روزی بیاید…
 * اعلان‌هایی که لازم است و نیست را سنجیده درست کن.»
 *
 *   ۱) ساعتِ آرام: شب «کم مانده» نمی‌آید، «تمام شد» همان لحظه می‌آید؛ خاموش‌شدنی است.
 *   ۲) سقفِ روزانهٔ «کم مانده»: یک بار گفته می‌شود و بعد ساکت؛ «تمام شد» همچنان می‌آید.
 *   ۳) برنامهٔ کامپیوتر قطع شد ⇒ یک پیام (نه هر پنج دقیقه)؛ برگشت ⇒ «دوباره وصل شد».
 *   ۴) اشتراکِ رو به پایان ⇒ فقط به خصوصیِ صاحبِ پمپ، هر پله یک بار.
 */
const test = require('node:test');
const assert = require('node:assert/strict');
const h = require('./helpers');
const { query, one, many, newId, now } = require('../src/db');
const telegram = require('../src/lib/bots').pump();
const otp = require('../src/lib/otp');

const TOKEN = `123456789:${'Zx_c-'.repeat(8)}`;
const BOT = 'PumpLiveBot';

let calls = [];
let responder = null;
let updateId = 5000;
let msgSeq = 100;

test.before(async () => {
  //  آزمون برنامهٔ کامپیوترِ واقعی ندارد ⇒ هشدارِ مخزن متن (عکسش در pump-telegram-reports)
  telegram.setAutoTankImage(false);
  await h.start();
  const pw = require('../src/lib/password');
  await query(
    `INSERT INTO admins (id, username, name, password_hash, role, status, created_at)
     VALUES ($1,'admin','مدیر',$2,'superadmin','active',$3)`,
    [newId('adm'), await pw.hashPassword('Admin!12345'), now()]
  );
  telegram.setTransport(async (method, params, tok, files) => {
    calls.push({ method, params, tok, files });
    if (responder) {
      const r = await responder(method, params);
      if (r) return r;
    }
    if (method === 'getMe') return { ok: true, result: { id: 42, is_bot: true, username: BOT } };
    if (method === 'sendMessage') { msgSeq += 1; return { ok: true, result: { message_id: msgSeq } }; }
    if (method === 'getUpdates') return { ok: true, result: [] };
    return { ok: true, result: true };
  });
  const login = await h.post('/api/admin/login', { username: 'admin', password: 'Admin!12345' });
  const saved = await h.put('/api/admin/telegram', { token: TOKEN, enabled: true }, { token: login.body.token });
  assert.equal(saved.status, 200, JSON.stringify(saved.body));
});
test.after(async () => {
  telegram.setTransport(null);
  await h.stop();
});

const clear = () => { calls = []; };
const sent = () => calls.filter(c => c.method === 'sendMessage');
const to = (chatId) => calls.filter(c => ['sendMessage', 'editMessageText'].includes(c.method)
  && c.params.chat_id === String(chatId));
const lastTo = (chatId) => to(chatId).at(-1);
const rows = (m) => m?.params?.reply_markup?.inline_keyboard || [];

function msg(chatId, text, { type = 'private', from = null } = {}) {
  updateId += 1;
  const chat = type === 'private' ? { id: chatId, type, first_name: 'کاربر' } : { id: chatId, type, title: 'گروهِ پمپ' };
  return telegram.handleUpdate({
    update_id: updateId,
    message: { message_id: updateId, chat, text, from: from || { id: chatId } },
  });
}

function press(chatId, data, { type = 'private', from = null, messageId = 77 } = {}) {
  updateId += 1;
  return telegram.handleUpdate({
    update_id: updateId,
    callback_query: {
      id: `cb${updateId}`, data, from: from || { id: chatId },
      message: { message_id: messageId, chat: { id: chatId, type } },
    },
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

async function linkPrivate(chatId, owner) {
  await msg(chatId, '/start');
  await msg(chatId, owner.email);
  const row = await one(
    'SELECT id FROM otp_codes WHERE destination=$1 AND purpose=$2 ORDER BY created_at DESC LIMIT 1',
    [owner.email, telegram.PURPOSE]
  );
  await msg(chatId, (await otp.reveal(row.id)).code);
  const r = await one('SELECT * FROM telegram_chats WHERE chat_id=$1', [String(chatId)]);
  assert.equal(r.station_id, owner.stationId);
  return r;
}

async function linkGroup(groupId, ownerChat) {
  await press(ownerChat, 'share');
  const url = rows(lastTo(ownerChat)).flat().find(b => /startgroup=/.test(b.url || ''))?.url || '';
  const tok = /startgroup=([A-Za-z0-9_-]+)$/.exec(url)?.[1];
  assert.ok(tok, `نشانیِ گروه نیامد: ${url}`);
  await msg(groupId, `/start@${BOT} ${tok}`, { type: 'supergroup', from: { id: ownerChat } });
}

/** همان چیزی که برنامهٔ کامپیوتر از `StationSnapshot.Alerts` می‌فرستد. */
const debtOut = (id, name) => ({ k: `d${id}-stP-out`, n: name, f: 'پطرول', s: 'out', t: `${name} — پطرولِ حسابش تمام شد، اضافه نده` });
const debtLow = (id, name) => ({ k: `d${id}-stP-low`, n: name, f: 'پطرول', s: 'low', t: `${name} — پطرولِ حسابش کم مانده` });
const tankOut = { k: 'tank-diesel-out', n: 'مخزنِ دیزل', f: 'دیزل', s: 'out', t: 'مخزنِ دیزل ته کشید — 0 لیتر' };

async function state(dev, body) {
  const r = await h.post('/api/pump/device/state', body, { token: dev });
  assert.equal(r.status, 200, JSON.stringify(r.body));
  return r.body;
}

const KABUL = 4.5 * 3600 * 1000;
/** لحظه‌ای در امروز، ساعتِ `hour`ِ کابل. */
const at = (hour) => {
  const d = new Date(Date.now() + KABUL);
  d.setUTCHours(hour, 10, 0, 0);
  return d.getTime() - KABUL;
};
const ev = (kind, key, who, state = 'low') => ({ kind, title: `${who} — ${state === 'out' ? 'تمام شد' : 'کم مانده'}`, data: { key, who, state, action: '' } });

test('۱) ساعتِ آرام: شب «کم مانده» نمی‌آید، «تمام شد» می‌آید؛ و خاموش‌شدنی است', async () => {
  const owner = await pumpOwner('آرام');
  await linkPrivate(9201, owner);
  clear();
  await telegram.notifyStation(owner.stationId, [ev('debt', 'd1-stP-w70', 'کریم')], at(23));
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '9201').length, 0, 'شب «کم مانده» آمد');

  await telegram.notifyStation(owner.stationId, [ev('debt', 'd2-stP-out', 'نبی', 'out')], at(23));
  await telegram.flushOutbox();
  assert.match(sent().filter(c => c.params.chat_id === '9201').at(-1)?.params.text || '', /نبی/, '«تمام شد» شب هم باید بیاید');

  //  خاموش ⇒ شب هم می‌آید
  await press(9201, 'quiet');
  clear();
  await telegram.notifyStation(owner.stationId, [ev('debt', 'd3-stP-w90', 'رحیم')], at(23));
  await telegram.flushOutbox();
  assert.match(sent().filter(c => c.params.chat_id === '9201').at(-1)?.params.text || '', /رحیم/);

  //  روز ⇒ می‌آید
  await press(9201, 'quiet');
  clear();
  await telegram.notifyStation(owner.stationId, [ev('debt', 'd4-stP-w70', 'حسن')], at(11));
  await telegram.flushOutbox();
  assert.match(sent().filter(c => c.params.chat_id === '9201').at(-1)?.params.text || '', /حسن/);
});

test('۲) سقفِ روزانه: «کم مانده» پس از سقف یک بار گفته و بعد ساکت؛ «تمام شد» همچنان', async () => {
  const owner = await pumpOwner('سقف');
  await linkPrivate(9202, owner);
  clear();
  for (let i = 0; i < telegram.CALM_CAP + 4; i++) {
    await telegram.notifyStation(owner.stationId, [ev('debt', `d${100 + i}-stP-w70`, `نفر ${i}`)], at(12));
  }
  await telegram.flushOutbox();
  await telegram.flushOutbox();
  const mine = sent().filter(c => c.params.chat_id === '9202');
  assert.equal(mine.filter(c => /^👤🟡/.test(c.params.text)).length, telegram.CALM_CAP, 'بیش از سقف آمد');
  assert.equal(mine.filter(c => /🔕 امروز/.test(c.params.text)).length, 1, 'خبرِ سقف یک بار');
  clear();
  await telegram.notifyStation(owner.stationId, [ev('debt', 'd999-stP-out', 'آخر', 'out')], at(12));
  await telegram.flushOutbox();
  assert.match(sent().filter(c => c.params.chat_id === '9202').at(-1)?.params.text || '', /آخر/, '«تمام شد» پس از سقف هم می‌آید');
});

test('۳) برنامه قطع شد ⇒ یک پیام؛ دوباره ⇒ هیچ؛ برگشت ⇒ «دوباره وصل شد»', async () => {
  const owner = await pumpOwner('قطع');
  const dev = await bindDevice(owner, 'pc-down');
  await linkPrivate(9203, owner);
  await state(dev, { alerts: [] });
  await query('UPDATE station_live_state SET updated_at=$2 WHERE station_id=$1', [owner.stationId, at(12) - 3 * 3600 * 1000]);
  clear();
  await telegram.pcWatchTick(at(12));
  await telegram.pcWatchTick(at(12) + 5 * 60_000);
  await telegram.flushOutbox();
  const down = sent().filter(c => c.params.chat_id === '9203' && /آخرین بار خبر داد/.test(c.params.text));
  assert.equal(down.length, 1, 'قطعی باید یک بار گفته شود');

  await state(dev, { alerts: [], tank: { petrol: { show: 10, threshold: 1, low: false } } });
  clear();
  //  ⛔ ساعتِ روز، نه ساعتِ اجرا: شب «دوباره وصل شد» عمداً نمی‌آید
  await telegram.pcWatchTick(at(12) + 60_000);
  await telegram.flushOutbox();
  assert.ok(sent().some(c => c.params.chat_id === '9203' && /دوباره وصل شد/.test(c.params.text)), 'برگشت گفته نشد');
});

test('۴) اشتراکِ رو به پایان ⇒ فقط به خصوصیِ صاحبِ پمپ، و هر پله یک بار', async () => {
  const owner = await pumpOwner('اشتراک');
  await linkPrivate(9204, owner);
  //  دورهٔ آزمایشی: ۲ روزِ دیگر تمام شود
  const e0 = await require('../src/lib/entitlement').pump.entitlementOf(owner.stationId);
  if (!e0.trial?.enabled) return;   //  سرورِ بی دورهٔ آزمایشی — این بند جا ندارد
  const DAY = 24 * 3600 * 1000;
  const total = Math.round((e0.trial.endsAt - e0.trial.startsAt) / DAY);
  await query('UPDATE stations SET trial_started_at=$2 WHERE id=$1', [owner.stationId, Date.now() - (total - 2) * DAY - 3600_000]);
  clear();
  await telegram.subTick(at(10));
  await telegram.subTick(at(10) + 3600_000);
  await telegram.flushOutbox();
  const mine = sent().filter(c => c.params.chat_id === '9204' && /⏳/.test(c.params.text));
  assert.equal(mine.length, 1, 'یادآوری یک بار');
  assert.match(mine[0].params.text, /۲ روزِ دیگر/);
});
