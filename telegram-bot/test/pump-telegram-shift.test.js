'use strict';
/**
 * باتِ تلگرامِ پمپ — 💰 کسریِ صندوق و ⏰ شیفتِ بسته‌نشده (۱۴۰۵/۰۷/۲۱).
 *
 * خواستهٔ صاحب سامانه: «کسری صندوق و شیفت بسته‌نشده رو هم درست کن.»
 * برنامهٔ پمپ این دو را از ورق می‌سازد (`AlertWatch.ShiftAlerts`)؛ سرورِ حساب نوعِ `shift` می‌دهد.
 *
 *   ۱) نشانِ خودشان را دارند، بی دکمهٔ حساب/مخزن، و سقفِ «کم مانده» جلویشان را نمی‌گیرد.
 *   ۲) ساعتِ آرام را رعایت می‌کنند.
 *   ۳) در /status و اعلامیهٔ صبح جدا از قرض‌داران؛ درست شدن ⇒ «برطرف شد».
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

const ev = (kind, key, who, title) => ({ kind, title, data: { key, who, state: 'low', action: '' } });
const cash = (id) => ({ k: `cash-${id}-night`, n: 'احمد', f: '', s: 'low', t: 'کسریِ صندوق — شیفتِ شبِ 1405/07/20 (احمد): 2,500 افغانی کم است', a: 'قرض یا مصرفِ جامانده را در ورق ثبت کنید' });
const open = (id) => ({ k: `shift-${id}-day`, n: 'محمود', f: '', s: 'low', t: 'شیفتِ روزِ 1405/07/20 (محمود) بسته نشده — ختمِ 2 پایه نوشته نشده', a: 'ختمِ پایه‌ها را در ورق بنویسید' });

test('۱) کسری و شیفتِ بسته‌نشده ⇒ 💰/⏰، بی دکمهٔ حساب یا مخزن، و سقفِ «کم مانده» را نمی‌خورند', async () => {
  const owner = await pumpOwner('صندوق');
  await linkPrivate(9301, owner);
  //  سقفِ امروز را پر کن
  for (let i = 0; i < telegram.CALM_CAP + 1; i++) {
    await telegram.notifyStation(owner.stationId, [{ kind: 'debt', title: `نفر ${i}`, data: { key: `d${500 + i}-stP-w70`, who: `نفر ${i}`, state: 'low' } }], at(12));
  }
  await telegram.flushOutbox();
  await telegram.flushOutbox();
  clear();
  await telegram.notifyStation(owner.stationId, [ev('shift', 'cash-7-night', 'احمد', cash(7).t)], at(12));
  await telegram.notifyStation(owner.stationId, [ev('shift', 'shift-8-day', 'محمود', open(8).t)], at(12));
  await telegram.flushOutbox();
  const mine = sent().filter(c => c.params.chat_id === '9301');
  const c1 = mine.find(c => /کسریِ صندوق/.test(c.params.text));
  const c2 = mine.find(c => /بسته نشده/.test(c.params.text));
  assert.ok(c1, 'کسری پس از سقف هم باید بیاید');
  assert.ok(c2, 'شیفتِ بسته‌نشده پس از سقف هم باید بیاید');
  assert.match(c1.params.text, /💰🟡/);
  assert.match(c2.params.text, /⏰🟡/);
  assert.equal(rows(c1).length, 0, 'کسری دکمهٔ حساب یا مخزن ندارد');
  assert.equal(rows(c2).length, 0);
});

test('۲) ساعتِ آرام: شب نمی‌آید (در /status و اعلامیهٔ صبح هست)', async () => {
  const owner = await pumpOwner('شبِ صندوق');
  await linkPrivate(9302, owner);
  clear();
  await telegram.notifyStation(owner.stationId, [ev('shift', 'cash-9-night', 'احمد', cash(9).t)], at(23));
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '9302').length, 0, 'شب کسری آمد');
});

test('۳) سرورِ حساب کلیدِ ورق را shift می‌کند؛ /status و اعلامیه نشانش می‌دهند؛ درست شد ⇒ «برطرف شد»', async () => {
  const owner = await pumpOwner('ورق');
  const dev = await bindDevice(owner, 'pc-waraq');
  await linkPrivate(9303, owner);
  clear();
  await state(dev, { alerts: [cash(11), open(12)] });
  await telegram.flushOutbox();
  const evs = await many('SELECT kind FROM station_events WHERE tenant_id=$1', [owner.stationId]).catch(() => []);
  if (evs.length) assert.ok(evs.every(e => e.kind === 'shift'), JSON.stringify(evs));

  await msg(9303, '/status');
  const st = lastTo(9303)?.params.text || '';
  assert.match(st, /💰🟡 کسریِ صندوق/);
  assert.match(st, /⏰🟡 شیفتِ روزِ/);

  const s = await one('SELECT * FROM station_live_state WHERE station_id=$1', [owner.stationId]);
  const ann = telegram.announcementText('ورق', { alerts: s.alerts, tank: {}, owe: [], updatedAt: Date.now() });
  assert.match(ann, /🧾 صندوق و شیفت‌ها:/);
  assert.doesNotMatch(ann, /👥 قرض‌داران/, 'کسری قرض‌دار نیست');

  clear();
  await state(dev, { alerts: [] });
  await telegram.flushOutbox();
  const fixed = sent().filter(c => c.params.chat_id === '9303').at(-1)?.params.text || '';
  assert.match(fixed, /💰🟢 احمد: کسریِ صندوق درست شد/);
  assert.match(fixed, /⏰🟢 محمود: شیفت بسته شد/);
});
