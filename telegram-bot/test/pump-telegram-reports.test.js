'use strict';
/**
 * باتِ تلگرامِ پمپ — 📷 عکس و PDF از خودِ برنامهٔ کامپیوتر (۱۴۰۵/۰۷/۲۱).
 *
 * خواستهٔ صاحب سامانه: «از مخزن‌ها همین عکس باشد و پیام زیرش… حسابِ قرض‌دار:
 * عکس با رسید، بردگی، الباقی و واحد، و PDF و لینکِ کیو‌آرش… پیام ساده… بشود به
 * واتساپ و جاهای دیگر هم فرستاد… و شلوغ نشود.»
 *
 *   ۱) دکمهٔ «📷 حسابِ فلانی» زیرِ هشدار ⇒ درخواست به برنامهٔ **همان** پمپ ⇒ عکس (با
 *      متنِ کوتاه)، PDF و لینکِ کیو‌آر — عیناً همان بایت‌هایی که برنامه داد.
 *   ۲) دکمه با شناسهٔ پمپِ دیگری ⇒ رد؛ هیچ درخواستی ساخته نمی‌شود.
 *   ۳) «نشد»ِ برنامه همان‌طور به کاربر می‌رسد.
 *   ۴) برنامه‌ای که مدتی است خبری نداده ⇒ همان لحظه «شاید بسته است»، نه انتظار.
 *   ۵) دو دکمهٔ پشتِ هم ⇒ یک درخواست (شلوغ نشود).
 *   ۶) /tanks و دکمهٔ «🛢 عکسِ مخزن‌ها»ی وضعیت همان راه را می‌روند.
 *
 * ── گزارشِ صاحب سامانه (۱۴۰۵/۰۷/۱۴، با سه عکس) ─────────────────────
 * «به گروه‌ام وصلش کردم اما داره حرف‌های تکراری می‌زنه و هیچی رو دستگیرِ
 *  من نمی‌کنه و دنبالِ چیزی نمی‌گرده… دو سه تا هشدار استن اما سرور و
 *  برنامه بی‌توجهی می‌کنن… محض این که چیزی می‌گه چندین دکمه میان زیرش…
 *  برای یک پیام منو رو باز کردم ولی بعدِ هر پیام منو هم پشتِ سرش است…
 *  چرا توی بات هر کسی می‌تونه دست‌کاری کنه… کدِ هشت‌رقمیِ هر حساب که به
 *  کارمنداش بده تا اپِ اندروید و آیفون رو باز کنن.»
 *
 * هر بندِ این فایل یکی از همان جمله‌هاست.
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
  //  ⛔ ساعتِ آرام (۲۲ تا ۷ِ کابل) این‌جا سنجیده نمی‌شود — آزمون به ساعتِ اجرا وابسته نباشد
  telegram.setQuietHours(0, 0);
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

const PNG = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), Buffer.alloc(80, 7)]);
const PDF = Buffer.from('%PDF-1.7\n% حساب\n');

/** برنامهٔ کامپیوترِ ساختگی: یک بار `/rate` را می‌پرسد و به هر درخواست همان جواب را می‌دهد. */
async function pcAnswers(dev, answer) {
  for (let i = 0; i < 20; i++) {
    const r = await h.get('/api/pump/device/rate?wait=2', { token: dev });
    const reqs = r.body.reqs || [];
    if (!reqs.length) continue;
    for (const q of reqs) {
      const up = await h.post(`/api/pump/device/report/${q.id}`, answer(q), { token: dev });
      assert.equal(up.status, 200, JSON.stringify(up.body));
    }
    return reqs;
  }
  throw new Error('درخواستی به برنامه نرسید');
}

test('۱) «📷 حسابِ فلانی» زیرِ هشدار ⇒ عکس با متنِ کوتاه، PDF و لینکِ کیو‌آر — از خودِ برنامه', async () => {
  const owner = await pumpOwner('عکس');
  const dev = await bindDevice(owner, 'pc-photo');
  await linkPrivate(9101, owner);
  await state(dev, { alerts: [debtOut(7, 'هارون')] });
  await telegram.flushOutbox();
  const alert = sent().filter(c => c.params.chat_id === '9101').at(-1);
  const btn = rows(alert).flat().find(b => /هارون/.test(b.text));
  assert.ok(btn, 'دکمهٔ حساب زیرِ هشدار نیامد');

  clear();
  const pressing = press(9101, btn.callback_data);
  const reqs = await pcAnswers(dev, () => ({
    ok: true,
    caption: '👤 هارون · 🔴 تمام شده · الباقی 640 لیتر',
    files: [
      { name: 'هارون — کارت', mime: 'image/png', b64: PNG.toString('base64') },
      { name: 'حساب هارون', mime: 'application/pdf', b64: PDF.toString('base64') },
    ],
    extra: { qr: 'https://yaqobipump.top/view/#s=p&a=d7&k=abc&d=xyz' },
  }));
  await pressing;
  assert.deepEqual(reqs.map(q => [q.kind, q.arg.id]), [['debtor', 7]], 'درخواست همان قرض‌دار را می‌خواهد');

  const photo = calls.find(c => c.method === 'sendPhoto');
  assert.ok(photo, 'عکس نرفت');
  assert.equal(photo.params.chat_id, '9101');
  assert.match(photo.params.caption, /الباقی 640 لیتر/);
  assert.ok(photo.files.photo.data.equals(PNG), '⛔ عکس بایت‌به‌بایت همانِ برنامه است');
  const doc = calls.find(c => c.method === 'sendDocument');
  assert.ok(doc && doc.files.document.data.equals(PDF), 'PDF نرفت یا عوض شد');
  assert.equal(doc.files.document.name, 'حساب هارون.pdf');
  const link = sent().find(c => /لینکِ حساب/.test(c.params.text || ''));
  assert.ok(link && link.params.text.includes('https://yaqobipump.top/view/#s=p&a=d7'), 'لینکِ کیو‌آر نیامد');
});

test('۲) دکمه با شناسهٔ پمپِ دیگر ⇒ رد؛ هیچ درخواستی ساخته نمی‌شود', async () => {
  const a = await pumpOwner('الف-عکس');
  const b = await pumpOwner('ب-عکس');
  const devB = await bindDevice(b, 'pc-b-photo');
  await linkPrivate(9102, a);
  clear();
  await press(9102, `acct:${b.stationId}:7`);
  const ans = calls.find(c => c.method === 'answerCallbackQuery');
  assert.match(ans.params.text, /وصل نیست/);
  const r = await h.get('/api/pump/device/rate', { token: devB });
  assert.deepEqual(r.body.reqs, [], '⛔ پمپِ دیگر درخواستی گرفت');
});

test('۳) «نشد»ِ برنامه همان‌طور به کاربر می‌رسد', async () => {
  const owner = await pumpOwner('نشد-عکس');
  const dev = await bindDevice(owner, 'pc-fail');
  await linkPrivate(9103, owner);
  clear();
  const pressing = msg(9103, '/acct ناشناس');
  await pcAnswers(dev, () => ({ ok: false, error: 'قرض‌داری با نامِ «ناشناس» پیدا نشد.' }));
  await pressing;
  assert.match(lastTo(9103).params.text, /پیدا نشد/);
  assert.equal(calls.filter(c => c.method === 'sendPhoto').length, 0);
});

test('۴) برنامه‌ای که مدتی است خبری نداده ⇒ همان لحظه «شاید بسته است»', async () => {
  const owner = await pumpOwner('کهنه-عکس');
  const dev = await bindDevice(owner, 'pc-old');
  await linkPrivate(9104, owner);
  await state(dev, { alerts: [] });
  await query('UPDATE station_live_state SET updated_at=$2 WHERE station_id=$1', [owner.stationId, now() - 3 * 3600 * 1000]);
  clear();
  const t0 = Date.now();
  await msg(9104, '/tanks');
  assert.ok(Date.now() - t0 < 5000, 'منتظرِ برنامهٔ خاموش ماند');
  assert.match(lastTo(9104).params.text, /شاید بسته است/);
});

test('۵) دو دکمهٔ پشتِ هم ⇒ یک درخواست — شلوغ نمی‌شود', async () => {
  const owner = await pumpOwner('پیاپی-عکس');
  const dev = await bindDevice(owner, 'pc-twice');
  await linkPrivate(9105, owner);
  clear();
  const first = press(9105, `tanks:${owner.stationId}`);
  await new Promise(r => setTimeout(r, 100));
  await press(9105, `tanks:${owner.stationId}`);
  assert.ok(sent().some(c => /قبلی هنوز در راه است/.test(c.params.text || '')), 'دومی باید صبر کند');
  const reqs = await pcAnswers(dev, () => ({
    ok: true, caption: '🛢 مخزن‌ها', files: [{ name: 'مخزن‌ها', mime: 'image/png', b64: PNG.toString('base64') }],
  }));
  await first;
  assert.equal(reqs.length, 1, 'دو درخواست ساخته شد');
});

test('۶) /tanks و «🛢 عکسِ مخزن‌ها»ی وضعیت ⇒ عکسِ مخزن‌ها با متن و سندِ خریدها', async () => {
  const owner = await pumpOwner('مخزن-عکس');
  const dev = await bindDevice(owner, 'pc-tanks');
  await linkPrivate(9106, owner);
  clear();
  await press(9106, 'status');
  assert.ok(rows(lastTo(9106)).flat().some(b => b.callback_data === 'tanks'), 'دکمهٔ مخزن‌ها در وضعیت نیست');

  clear();
  const asking = msg(9106, '/tanks');
  const reqs = await pcAnswers(dev, () => ({
    ok: true,
    caption: '🛢 مخزن‌ها\n🟢 پطرول: 13,600 لیتر (68٪)\n🏢 بدهی به شرکت‌ها:\n   • شرکتِ نور: 120,000 ؋',
    files: [
      { name: 'مخزن‌ها', mime: 'image/png', b64: PNG.toString('base64') },
      { name: 'خریدهای پطرول', mime: 'application/pdf', b64: PDF.toString('base64') },
    ],
  }));
  await asking;
  assert.equal(reqs[0].kind, 'tanks');
  const photo = calls.find(c => c.method === 'sendPhoto');
  assert.match(photo.params.caption, /شرکتِ نور/);
  assert.equal(calls.find(c => c.method === 'sendDocument').files.document.name, 'خریدهای پطرول.pdf');
});

test('۷) هشدارِ مخزن ⇒ همان عکسِ مخزنِ برنامه، خبرِ هشدار بالای زیرنویس — نه متنِ تنها', async () => {
  const owner = await pumpOwner('هشدارِ مخزن');
  const dev = await bindDevice(owner, 'pc-tankalert');
  await linkPrivate(9107, owner);
  telegram.setAutoTankImage(true);
  try {
    clear();
    await state(dev, { alerts: [tankOut, debtOut(9, 'کاظم')] });
    const reqs = await pcAnswers(dev, () => ({
      ok: true,
      caption: '🛢 مخزن‌ها\n🔴 دیزل: 0 لیتر (0٪)',
      files: [{ name: 'مخزن‌ها', mime: 'image/png', b64: PNG.toString('base64') }],
    }));
    assert.equal(reqs[0].kind, 'tanks');
    for (let i = 0; i < 40 && !calls.some(c => c.method === 'sendPhoto'); i++) await new Promise(r => setTimeout(r, 50));
    await telegram.flushOutbox();
    const photo = calls.find(c => c.method === 'sendPhoto');
    assert.ok(photo, 'عکسِ مخزن نیامد');
    assert.equal(photo.params.chat_id, '9107');
    assert.match(photo.params.caption, /^🛢️🔴 مخزنِ دیزل ته کشید/);
    assert.match(photo.params.caption, /🔴 دیزل: 0 لیتر/);
    const texts = sent().filter(c => c.params.chat_id === '9107').map(c => c.params.text);
    assert.ok(texts.some(t => /^👤🔴 کاظم/.test(t)), 'هشدارِ قرض‌دار جدا نیامد');
    assert.ok(!texts.some(t => /مخزنِ دیزل/.test(t)), '⛔ مخزن هم متن آمد هم عکس');
  } finally {
    telegram.setAutoTankImage(false);
  }
});
