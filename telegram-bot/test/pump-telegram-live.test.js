'use strict';
/**
 * باتِ تلگرامِ پمپ — حالِ زنده، دکمه‌های کم و درجا، جست‌وجو، و دسترسی در گروه.
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
  //  ⛔ ساعتِ آرام (۲۲ تا ۷ِ کابل) این‌جا سنجیده نمی‌شود — آزمون به ساعتِ اجرا وابسته نباشد
  telegram.setQuietHours(0, 0);
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

/* ══════════════════════════════════════════════════════════════════
   ۱) «برنامه به سرور بگوید، سرور به بات» — و «حرف‌های تکراری» نه
   ══════════════════════════════════════════════════════════════════ */

test('حالِ زنده: هشدارِ باز ⇒ یک پیام؛ همان حال دوباره (بستن و باز کردنِ برنامه) ⇒ هیچ پیامی', async () => {
  const owner = await pumpOwner('زنده');
  const dev = await bindDevice(owner, 'pc-live');
  await linkPrivate(8101, owner);
  clear();

  const first = await state(dev, { alerts: [debtOut(1, 'کریم'), tankOut], tank: { diesel: { show: 0, threshold: 1000, low: true } } });
  assert.equal(first.opened, 2);
  await telegram.flushOutbox();
  const m = sent().filter(c => c.params.chat_id === '8101');
  assert.equal(m.length, 1, 'یک پیام برای یک دسته');
  assert.match(m[0].params.text, /کریم/);
  assert.match(m[0].params.text, /مخزنِ دیزل/);
  //  ⛔ هشدار منو ندارد — فقط «📷 حسابِ فلانی» و «🛢 مخزن‌ها» (۱۴۰۵/۰۷/۲۱: «اگه زدم عکس رو بده»)
  const kb = m[0].params.reply_markup?.inline_keyboard || [];
  assert.deepEqual(kb.map(r => r.map(b => b.text)), [['📷 حسابِ کریم'], ['🛢 مخزن‌ها']]);
  assert.equal(kb[0][0].callback_data, `acct:${owner.stationId}:1`, 'دکمه شناسهٔ همان قرض‌دار و همان پمپ را دارد');
  assert.equal(kb[1][0].callback_data, `tanks:${owner.stationId}`);

  //  ⛔ همان هشدارها، دو بار دیگر — مثلِ بستن و باز کردنِ برنامه
  clear();
  for (let i = 0; i < 2; i++) {
    const again = await state(dev, { alerts: [tankOut, debtOut(1, 'کریم')] });
    assert.equal(again.opened, 0);
  }
  await telegram.flushOutbox();
  assert.equal(sent().length, 0, 'حرفِ تکراری رفت');
  const events = await one('SELECT COUNT(*)::int AS n FROM station_events WHERE station_id=$1', [owner.stationId]);
  assert.equal(events.n, 2, 'در دفترِ خبر هم دوتا ماند');
});

test('برطرف شد ⇒ «✅ برطرف شد»؛ ولی «تمام شد ⇒ کم مانده» فقط هشدارِ تازه است، نه برطرف', async () => {
  const owner = await pumpOwner('برطرف');
  const dev = await bindDevice(owner, 'pc-fix');
  await linkPrivate(8102, owner);
  await state(dev, { alerts: [debtOut(5, 'نبی'), debtOut(6, 'رحیم')] });
  await telegram.flushOutbox();

  clear();
  //  نبی: تمام شد ⇒ کم مانده · رحیم: دیگر هیچ هشداری ندارد
  const r = await state(dev, { alerts: [debtLow(5, 'نبی')] });
  assert.equal(r.opened, 1);
  assert.equal(r.resolved, 1);
  await telegram.flushOutbox();
  const texts = sent().filter(c => c.params.chat_id === '8102').map(c => c.params.text);
  assert.equal(texts.length, 2);
  const fixed = texts.find(t => t.startsWith('✅ برطرف'));
  assert.ok(fixed && fixed.includes('رحیم'), 'رحیم برطرف شد');
  assert.ok(!fixed.includes('نبی'), '⛔ نبی برطرف نشده، فقط حالش عوض شد');
  assert.ok(texts.some(t => t.includes('🚨') && t.includes('نبی')), 'هشدارِ تازهٔ «کم مانده»ِ نبی');
});

test('«وضعیت» از حالِ زنده حرف می‌زند: هشدارهای باز، مخزن، و این‌که برنامه کِی خبر داد', async () => {
  const owner = await pumpOwner('وضع');
  const dev = await bindDevice(owner, 'pc-st2');
  await linkPrivate(8103, owner);

  //  ⛔ برنامه هنوز چیزی نفرستاده ⇒ همین گفته می‌شود، نه «هیچ هشداری نیست»
  clear();
  await msg(8103, '/status');
  assert.match(lastTo(8103).params.text, /هنوز حالِ زنده‌اش را به سرور نفرستاده/);

  await state(dev, {
    alerts: [debtOut(9, 'حمید')],
    tank: { petrol: { show: 1200, threshold: 1000, near: true }, diesel: { show: 0, threshold: 1000, low: true } },
  });
  clear();
  await msg(8103, '/status');
  const t = lastTo(8103).params.text;
  assert.match(t, /حمید/);
  assert.match(t, /پطرول: ۱٬۲۰۰ لیتر/);
  assert.match(t, /🔴 دیزل/);
  assert.match(t, /برنامهٔ کامپیوتر همین حالا خبر داد/);

  //  ⛔ برنامه یک ساعت خبری نداده ⇒ راستش گفته می‌شود
  await query('UPDATE station_live_state SET updated_at=$2 WHERE station_id=$1', [owner.stationId, now() - 3600_000]);
  clear();
  await msg(8103, '/status');
  assert.match(lastTo(8103).params.text, /شاید بسته است/);
});

/* ══════════════════════════════════════════════════════════════════
   ۲) دکمه‌ها: کم، و همان پیام را عوض می‌کنند
   ══════════════════════════════════════════════════════════════════ */

test('منو چهار دکمه در دو ردیف است، و هر دکمه همان پیام را ویرایش می‌کند — پیامِ تازه نه', async () => {
  const owner = await pumpOwner('دکمه');
  await linkPrivate(8201, owner);
  clear();
  await msg(8201, '/menu');
  const menu = lastTo(8201);
  //  دو ردیفِ همیشگی (چهار دکمه) + ردیفِ «💬 چت‌های میرزا» (۱۴۰۵/۰۷/۱۶) و
  //  «🏷️ نرخِ اتحادیه» که فقط قالب در کادرِ نوشتن می‌گذارد (۱۴۰۵/۰۷/۲۰) — و هیچ دکمهٔ دیگری.
  assert.equal(rows(menu).length, 3, 'سه ردیف');
  assert.equal(rows(menu).slice(0, 2).flat().length, 4, 'چهار دکمه در دو ردیفِ اول');
  assert.deepEqual(rows(menu)[2].map(b => b.callback_data), ['mirza', undefined], 'ردیفِ سوم: چت‌های میرزا و نرخ');
  assert.match(rows(menu)[2][1].switch_inline_query_current_chat, /^نرخ پطرول/, 'دکمهٔ نرخ قالب می‌گذارد، پیام نمی‌فرستد');

  clear();
  for (const d of ['status', 'app', 'settings', 'share', 'menu']) {
    await press(8201, d, { messageId: 555 });
  }
  assert.equal(sent().filter(c => c.params.chat_id === '8201').length, 0, '⛔ هیچ پیامِ تازه‌ای');
  const edits = calls.filter(c => c.method === 'editMessageText');
  assert.equal(edits.length, 5);
  assert.ok(edits.every(e => e.params.message_id === 555), 'همه روی همان پیام');
});

test('⛔ «بعدِ هر پیام منو پشتِ سرش است»: منوی تازه دکمه‌های منوی قبلی را برمی‌دارد', async () => {
  const owner = await pumpOwner('منو');
  await linkPrivate(8202, owner);
  clear();
  await msg(8202, '/menu');
  const firstId = msgSeq;
  await msg(8202, '/menu');
  const strip = calls.filter(c => c.method === 'editMessageReplyMarkup').at(-1);
  assert.ok(strip, 'دکمه‌های منوی قبلی برداشته نشد');
  assert.equal(strip.params.message_id, firstId);
  assert.deepEqual(strip.params.reply_markup, { inline_keyboard: [] });
  const row = await one('SELECT menu_msg_id FROM telegram_chats WHERE chat_id=$1', ['8202']);
  assert.equal(Number(row.menu_msg_id), msgSeq);
});

test('⛔ نوشتنِ هر چیزی دیگر منو نمی‌آورد: جست‌وجو است، بی دکمه', async () => {
  const owner = await pumpOwner('جست');
  const dev = await bindDevice(owner, 'pc-find');
  await linkPrivate(8203, owner);

  //  برنامه هنوز فهرست نفرستاده ⇒ راستش
  clear();
  await msg(8203, 'کریم');
  assert.match(lastTo(8203).params.text, /هنوز فهرستِ قرض‌داران را به سرور نفرستاده/);

  await state(dev, {
    alerts: [debtOut(1, 'كريم احمدی')],
    debtors: [
      { n: 'كريم احمدی', sp: 'out', sd: 'none', sm: 'ok', p: -40, d: 0, m: 2500 },
      { n: 'کریم‌الله', sp: 'ok', sd: 'low', sm: 'none', p: 300, d: 20, m: 0 },
      { n: 'نبی', sp: 'ok', sd: 'none', sm: 'none', p: 10, d: 0, m: 0 },
    ],
  });
  clear();
  await msg(8203, 'کریم');
  const reply = lastTo(8203);
  assert.equal(reply.method, 'sendMessage');
  //  ⛔ پاسخِ جست‌وجو منو ندارد — فقط «📷 نام» برای عکس و PDFِ همان حساب (۱۴۰۵/۰۷/۲۱)
  const sb = (reply.params.reply_markup?.inline_keyboard || []).flat();
  assert.ok(sb.length >= 2 && sb.every(b => /^📷 /.test(b.text) && /^acctn:/.test(b.callback_data)), JSON.stringify(sb));
  //  «ي/ك»ِ عربی و نیم‌فاصله یکی‌اند
  assert.match(reply.params.text, /كريم احمدی/);
  assert.match(reply.params.text, /کریم‌الله/);
  assert.match(reply.params.text, /تمام شد — اضافه نده/);
  assert.match(reply.params.text, /الباقی −۴۰ لیتر/);
  assert.ok(!reply.params.text.includes('نبی'));
  assert.ok(!calls.some(c => c.method === 'editMessageReplyMarkup'), 'منویی در کار نبود');

  clear();
  await msg(8203, 'زرگر');
  assert.match(lastTo(8203).params.text, /پیدا نشد/);
});

/* ══════════════════════════════════════════════════════════════════
   ۳) گروه: «هر کسی نتواند دست‌کاری کند»
   ══════════════════════════════════════════════════════════════════ */

test('⛔ در گروه، عضوِ عادی تنظیمات را عوض نمی‌کند؛ مدیرِ گروه و وصل‌کننده می‌کنند', async () => {
  const owner = await pumpOwner('گروهی');
  await linkPrivate(8301, owner);
  await linkGroup(-1008301, 8301);
  const g = () => one('SELECT only_out, station_id FROM telegram_chats WHERE chat_id=$1', ['-1008301']);
  assert.equal((await g()).station_id, owner.stationId);

  //  عضوِ عادی
  clear();
  responder = (m) => (m === 'getChatMember' ? { ok: true, result: { status: 'member' } } : null);
  await press(-1008301, 'toggle', { type: 'supergroup', from: { id: 999001 } });
  await press(-1008301, 'unlink_yes', { type: 'supergroup', from: { id: 999001 } });
  responder = null;
  assert.equal((await g()).only_out, false, '⛔ عضوِ عادی «فقط تمام شد» را روشن کرد');
  assert.equal((await g()).station_id, owner.stationId, '⛔ عضوِ عادی گروه را جدا کرد');
  const denied = calls.filter(c => c.method === 'answerCallbackQuery' && c.params.show_alert);
  assert.equal(denied.length, 2);
  assert.match(denied[0].params.text, /فقط مدیرانِ همین گروه/);

  //  مدیرِ گروه (از خودِ تلگرام)
  responder = (m) => (m === 'getChatMember' ? { ok: true, result: { status: 'administrator' } } : null);
  await press(-1008301, 'toggle', { type: 'supergroup', from: { id: 999002 } });
  responder = null;
  assert.equal((await g()).only_out, true, 'مدیرِ گروه نتوانست');

  //  همان کسی که گروه را وصل کرد (بی این‌که مدیرِ تلگرامیِ گروه باشد)
  responder = (m) => (m === 'getChatMember' ? { ok: true, result: { status: 'member' } } : null);
  await press(-1008301, 'toggle', { type: 'supergroup', from: { id: 8301 } });
  responder = null;
  assert.equal((await g()).only_out, false, 'وصل‌کننده نتوانست');
});

/* ══════════════════════════════════════════════════════════════════
   ۴) «کدِ هشت‌رقمیِ هر حساب برای اپِ اندروید و آیفون»
   ══════════════════════════════════════════════════════════════════ */

test('کدِ پمپ: در خصوصی با دو لینک؛ در گروه فقط به مدیر و در پنجرهٔ خصوصیِ همان کلیک', async () => {
  const owner = await pumpOwner('کد');
  await linkPrivate(8401, owner);
  const code = (await one('SELECT access_code FROM stations WHERE id=$1', [owner.stationId]))?.access_code;

  clear();
  await msg(8401, '/code');
  const m = lastTo(8401);
  const real = (await one('SELECT access_code FROM stations WHERE id=$1', [owner.stationId])).access_code;
  assert.ok(!code || code === real, 'کد عوض نشد، فقط نشان داده شد');
  const shownCode = `${real.slice(0, 4)}-${real.slice(4)}`;
  assert.ok(m.params.text.includes(shownCode), 'کدِ پمپ نیامد');
  const urls = rows(m).flat().map(b => b.url).filter(Boolean);
  assert.ok(urls.some(u => u.endsWith('/downloads/PumpYaqobiKar.apk')), 'اندروید');
  assert.ok(urls.some(u => u.endsWith('/kar/')), 'آیفون');

  await linkGroup(-1008401, 8401);
  clear();
  await msg(-1008401, '/code', { type: 'supergroup', from: { id: 999003 } });
  assert.ok(!lastTo(-1008401).params.text.includes(shownCode), '⛔ کد روی پیامِ گروه نوشته شد');

  responder = (mm) => (mm === 'getChatMember' ? { ok: true, result: { status: 'member' } } : null);
  await press(-1008401, 'code', { type: 'supergroup', from: { id: 999003 } });
  responder = (mm) => (mm === 'getChatMember' ? { ok: true, result: { status: 'creator' } } : null);
  await press(-1008401, 'code', { type: 'supergroup', from: { id: 999004 } });
  responder = null;
  const answers = calls.filter(c => c.method === 'answerCallbackQuery' && c.params.show_alert);
  assert.equal(answers.length, 2);
  assert.ok(!answers[0].params.text.includes(shownCode), '⛔ عضوِ عادی کد را دید');
  assert.ok(answers[1].params.text.includes(shownCode), 'سازندهٔ گروه کد را ندید');
  assert.ok(!sent().some(c => c.params.text.includes(shownCode)), '⛔ کد در یک پیامِ گروه رفت');
});

test('در گروه: /find و /status برای همه؛ نوشته‌های عادیِ گروه بی‌پاسخ', async () => {
  const owner = await pumpOwner('کارکنان');
  const dev = await bindDevice(owner, 'pc-grp');
  await linkPrivate(8501, owner);
  await linkGroup(-1008501, 8501);
  await state(dev, { alerts: [], debtors: [{ n: 'صمد', sp: 'ok', sd: 'none', sm: 'none', p: 50, d: 0, m: 0 }] });

  clear();
  await msg(-1008501, 'سلام بچه‌ها', { type: 'supergroup', from: { id: 999005 } });
  assert.equal(calls.filter(c => c.method === 'sendMessage').length, 0, 'به گپِ گروه جواب داد');

  await msg(-1008501, `/find@${BOT} صمد`, { type: 'supergroup', from: { id: 999005 } });
  assert.match(lastTo(-1008501).params.text, /صمد/);
  await msg(-1008501, '/status', { type: 'supergroup', from: { id: 999005 } });
  assert.match(lastTo(-1008501).params.text, /هیچ هشدارِ بازی نیست/);
});

/* ══════════════════════════════════════════════════════════════════
   ۵) درِ حالِ زنده: جداسازی و بدنهٔ بد
   ══════════════════════════════════════════════════════════════════ */

test('⛔ حالِ زندهٔ یک پمپ به پمپِ دیگر نمی‌رسد، و بی توکنِ دستگاه در بسته است', async () => {
  const a = await pumpOwner('زنده‌الف');
  const b = await pumpOwner('زنده‌ب');
  const devB = await bindDevice(b, 'pc-lb');
  await linkPrivate(8601, a);
  await linkPrivate(8602, b);
  clear();
  await state(devB, { alerts: [debtOut(3, 'فقط ب')] });
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '8601').length, 0);
  assert.equal(sent().filter(c => c.params.chat_id === '8602').length, 1);
  assert.equal(await one('SELECT 1 FROM station_live_state WHERE station_id=$1', [a.stationId]), null);

  const anon = await h.post('/api/pump/device/state', { alerts: [] });
  assert.equal(anon.status, 401);
  const bad = await h.post('/api/pump/device/state', { alerts: 'x' }, { token: devB });
  assert.equal(bad.status, 400);
  //  کلیدِ بدشکل بی‌صدا رد می‌شود
  const odd = await state(devB, { alerts: [{ k: '../../x', s: 'out', t: 'نه' }, debtOut(3, 'فقط ب')] });
  assert.equal(odd.open, 1);
});

/* ══════════════════════════════════════════════════════════════════
   ۶) «محدودیت هم نداشته باشه» و «برای هر حساب، حسابِ همان کاربر»
   ══════════════════════════════════════════════════════════════════ */

test('⛔ بی سقف: همهٔ نتیجه‌های جست‌وجو و همهٔ هشدارها می‌آیند — متنِ بلند چند پیام می‌شود، بریده نمی‌شود', async () => {
  const owner = await pumpOwner('بزرگ');
  const dev = await bindDevice(owner, 'pc-big');
  await linkPrivate(8701, owner);
  //  ۱۲۰ قرض‌دارِ هم‌نام‌پیشوند و ۲۵۰ هشدارِ باز
  const debtors = Array.from({ length: 120 }, (_, i) => ({
    n: `احمد ${i + 1}`, sp: 'out', sd: 'low', sm: 'ok', p: -i, d: i, m: 1000 + i,
  }));
  const alerts = Array.from({ length: 250 }, (_, i) => debtOut(1000 + i, `احمد ${i + 1}`));
  const big = await h.post('/api/pump/device/state', { alerts, debtors }, { token: dev });
  assert.equal(big.status, 200, JSON.stringify(big.body));
  assert.equal(big.body.open, 250);
  await telegram.flushOutbox();
  await telegram.flushOutbox();
  const pushed = sent().filter(c => c.params.chat_id === '8701').map(c => c.params.text).join('\n');
  for (const i of [1, 125, 250]) assert.ok(pushed.includes(`احمد ${i} —`), `هشدارِ ${i} در خبر نیامد`);

  clear();
  await msg(8701, 'احمد');
  const found = sent().filter(c => c.params.chat_id === '8701');
  assert.ok(found.length > 1, 'متنِ بلند چند پیام نشد');
  for (const m of found) assert.ok(m.params.text.length <= 4096, 'پیامی از سقفِ تلگرام بلندتر شد');
  const all = found.map(m => m.params.text).join('\n');
  for (let i = 1; i <= 120; i++) assert.ok(all.includes(`👤 احمد ${i}\n`), `قرض‌دارِ ${i} نیامد`);
  assert.ok(!/نفرِ دیگر/.test(all), '⛔ «… و N نفرِ دیگر» یعنی بریده شد');
  //  دکمه‌های «📷» فقط زیرِ تکهٔ آخر، و نه بیشتر از پنج
  assert.ok(found.slice(0, -1).every(m => !m.params.reply_markup), 'دکمه روی تکهٔ میانی آمد');
  const fb = (found.at(-1).params.reply_markup?.inline_keyboard || []).flat();
  assert.ok(fb.length === 5 && fb.every(b => /^📷 /.test(b.text)), JSON.stringify(fb));

  clear();
  await press(8701, 'status', { messageId: 901 });
  const edits = calls.filter(c => c.method === 'editMessageText' && c.params.chat_id === '8701');
  const more = sent().filter(c => c.params.chat_id === '8701');
  assert.equal(edits.length, 1, 'تکهٔ اول جای همان پیام نشست');
  assert.ok(more.length >= 1);
  const status = [edits[0], ...more].map(m => m.params.text).join('\n');
  for (const i of [1, 250]) assert.ok(status.includes(`احمد ${i} —`), `هشدارِ ${i} در وضعیت نیامد`);
  assert.ok(!/هشدارِ دیگر/.test(status), '⛔ وضعیت بریده شد');
  assert.equal(rows(edits[0]).length, 0, 'دکمه روی تکهٔ اول ماند');
  assert.ok(rows(more.at(-1)).length > 0, 'دکمه زیرِ تکهٔ آخر نیامد');
});

test('⛔ یک بات، ولی هر حساب فقط حسابِ خودش: هم‌نامِ پمپِ دیگر در جست‌وجو، وضعیت و کد دیده نمی‌شود', async () => {
  const a = await pumpOwner('جدا‌الف');
  const b = await pumpOwner('جدا‌ب');
  const devA = await bindDevice(a, 'pc-ja');
  const devB = await bindDevice(b, 'pc-jb');
  await linkPrivate(8801, a);
  await linkPrivate(8802, b);
  await state(devA, { alerts: [debtOut(1, 'رحیم الف')], debtors: [{ n: 'رحیم', sp: 'out', sd: 'none', sm: 'none', p: -11, d: 0, m: 0 }] });
  await state(devB, { alerts: [], debtors: [{ n: 'رحیم', sp: 'ok', sd: 'none', sm: 'none', p: 777, d: 0, m: 0 }] });

  clear();
  await msg(8801, 'رحیم');
  await msg(8802, 'رحیم');
  const ta = sent().filter(c => c.params.chat_id === '8801').map(c => c.params.text).join('\n');
  const tb = sent().filter(c => c.params.chat_id === '8802').map(c => c.params.text).join('\n');
  assert.match(ta, /جدا‌الف/); assert.doesNotMatch(ta, /۷۷۷|777/);
  assert.match(tb, /جدا‌ب/);   assert.doesNotMatch(tb, /۱۱|تمام شد/);

  clear();
  await press(8802, 'status');
  assert.doesNotMatch(lastTo(8802).params.text, /رحیم الف/, '⛔ هشدارِ پمپِ دیگر در وضعیت آمد');

  //  عضوی که از پمپ بیرون شد، همان لحظه دیگر چیزی نمی‌بیند
  await query(`UPDATE station_members SET status='removed' WHERE station_id=$1`, [a.stationId]);
  clear();
  await msg(8801, 'رحیم');
  assert.doesNotMatch(sent().map(c => c.params.text).join('\n'), /الباقی/, '⛔ عضوِ بیرون‌شده هنوز حساب را دید');
});

/* ══════════════════════════════════════════════════════════════════
   ۸) «هم توی بات پیام میاد هم توی گروه» — گروه وصل ⇒ فقط گروه
   ══════════════════════════════════════════════════════════════════ */

test('گروه وصل ⇒ هشدار فقط در گروه؛ بی گروه ⇒ در خصوصی؛ جدا کردنِ گروه ⇒ دوباره خصوصی', async () => {
  const owner = await pumpOwner('یک‌جا');
  const dev = await bindDevice(owner, 'pc-one');
  await linkPrivate(8901, owner);

  //  بی گروه ⇒ خصوصی
  clear();
  await state(dev, { alerts: [debtOut(1, 'ولی')] });
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '8901').length, 1, 'بی گروه، خصوصی خبر نگرفت');

  //  گروه وصل شد ⇒ خصوصی خبر می‌گیرد که از این پس کجا
  clear();
  await linkGroup(-1008901, 8901);
  const told = sent().filter(c => c.params.chat_id === '8901').map(c => c.params.text).join('\n');
  assert.match(told, /از این پس هشدارها در همان گروه/);

  //  هشدارِ تازه و برطرف شدن ⇒ فقط گروه
  clear();
  await state(dev, { alerts: [debtOut(1, 'ولی'), debtOut(2, 'جلال')] });
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '8901').length, 0, '⛔ هم خصوصی هم گروه');
  const g = sent().filter(c => c.params.chat_id === '-1008901').map(c => c.params.text);
  assert.equal(g.length, 1);
  assert.match(g[0], /جلال/);
  assert.match(g[0], /🚨/);
  assert.ok(!g[0].includes('ولی'), 'هشدارِ قدیمی دوباره آمد');

  //  منوی خصوصی راستش را می‌گوید
  clear();
  await msg(8901, '/menu');
  assert.match(lastTo(8901).params.text, /در «.*» می‌آیند، نه این‌جا/);

  //  جدا کردنِ گروه ⇒ دوباره خصوصی
  responder = (m) => (m === 'getChatMember' ? { ok: true, result: { status: 'creator' } } : null);
  await press(-1008901, 'unlink_yes', { type: 'supergroup', from: { id: 8901 } });
  responder = null;
  clear();
  await state(dev, { alerts: [debtOut(1, 'ولی'), debtOut(2, 'جلال'), debtOut(3, 'کاظم')] });
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '-1008901').length, 0, 'گروهِ جداشده خبر گرفت');
  assert.equal(sent().filter(c => c.params.chat_id === '8901').length, 1, 'پس از جدا کردنِ گروه، خصوصی ساکت ماند');
});

/* ══════════════════════════════════════════════════════════════════
   ۹) «دقیق‌تر: چند لیتر مانده، ۷۰ و ۹۰ درصد، تمام شد، اضافه داد — و اعلامیه»
   ══════════════════════════════════════════════════════════════════ */

const lv = (id, name, level, t, a) => ({
  k: `d${id}-stP-${level}`, n: name, f: 'پطرول',
  s: level === 'out' || level === 'over' ? 'out' : 'low', t, a,
});

test('هشدار دستورِ کار دارد؛ ۷۰ ⇒ ۹۰ ⇒ اضافه‌داده هر کدام خبرِ تازه است نه «برطرف»', async () => {
  const owner = await pumpOwner('دقیق');
  const dev = await bindDevice(owner, 'pc-exact');
  await linkPrivate(9001, owner);
  clear();
  await state(dev, { alerts: [lv(1, 'امین', 'w70', 'امین — ۷۰٪ِ پطرولِ حسابش مصرف شد؛ ۳۰ لیتر مانده', 'متوجهِ امین باشید.')] });
  await telegram.flushOutbox();
  let m = sent().filter(c => c.params.chat_id === '9001').map(c => c.params.text);
  assert.equal(m.length, 1);
  assert.match(m[0], /۳۰ لیتر مانده/);
  assert.match(m[0], /📋 دستورِ کار:\n▫️ متوجهِ امین باشید\./);

  for (const [level, t, a] of [
    ['w90', 'امین — ۹۰٪ رفت؛ فقط ۱۰ لیتر مانده', 'به امین بگویید حسابش را پر کند.'],
    ['over', 'امین — ۲۵ لیتر بیشتر از حسابش پطرول داده شده', 'بازپرسی کنید: چه کسی و چرا به امین اضافه داد؟'],
  ]) {
    clear();
    const r = await state(dev, { alerts: [lv(1, 'امین', level, t, a)] });
    assert.equal(r.opened, 1);
    assert.equal(r.resolved, 0, `${level} برطرف شدن خوانده شد`);
    await telegram.flushOutbox();
    const texts = sent().filter(c => c.params.chat_id === '9001').map(c => c.params.text);
    assert.equal(texts.length, 1);
    assert.ok(texts[0].includes(a), 'دستورِ کار نیامد');
    assert.ok(!texts[0].startsWith('✅'), '⛔ «برطرف شد» آمد');
  }

  //  حساب پر شد ⇒ برطرف
  clear();
  const r = await state(dev, { alerts: [] });
  assert.equal(r.resolved, 1);
});

test('وضعیت: مخزن با «لیتر مانده» و حدِ هشدار، بدهیِ شرکت‌ها و دستورِ کار', async () => {
  const owner = await pumpOwner('وضعِ دقیق');
  const dev = await bindDevice(owner, 'pc-exact2');
  await linkPrivate(9002, owner);
  await state(dev, {
    alerts: [{ ...tankOut, t: 'مخزنِ دیزل کم شد — ۸۵۰ لیتر مانده', a: 'امروز دیزل سفارش بدهید.' }],
    tank: { petrol: { show: 5200, threshold: 1000 }, diesel: { show: 850, threshold: 1000, low: true } },
    owe: [{ n: 'شرکتِ الف', afn: 125000, usd: 3200 }, { n: 'صاف‌شده', afn: 0, usd: 0 }],
  });
  clear();
  await msg(9002, '/status');
  const t = lastTo(9002).params.text;
  assert.match(t, /دیزل: ۸۵۰ لیتر مانده — کم است \(حدِ هشدار ۱٬۰۰۰ لیتر\)/);
  assert.match(t, /پطرول: ۵٬۲۰۰ لیتر مانده — پُر است/);
  assert.match(t, /💼 بدهیِ پمپ به شرکت‌ها:\n▫️ شرکتِ الف: ۱۲۵٬۰۰۰ افغانی · ۳٬۲۰۰ دالر/);
  assert.ok(!t.includes('صاف‌شده'), 'شرکتِ بی‌بدهی آمد');
  assert.match(t, /📋 دستورِ کار:\n▫️ امروز دیزل سفارش بدهید\./);
});

test('اعلامیهٔ صبح: از ساعتِ ۸ کابل، روزی یک بار، فقط در گروه اگر وصل است', async () => {
  const owner = await pumpOwner('اعلامیه');
  const dev = await bindDevice(owner, 'pc-ann');
  await linkPrivate(9003, owner);
  await state(dev, {
    alerts: [lv(4, 'نادر', 'over', 'نادر — ۲۵ لیتر اضافه', 'بازپرسی کنید چرا به نادر اضافه دادید.'),
      lv(5, 'قادر', 'w70', 'قادر — ۷۰٪', 'متوجهِ قادر باشید.')],
    tank: { petrol: { show: 0, threshold: 1000, low: true } },
    owe: [{ n: 'شرکتِ ب', afn: 5000, usd: 0 }],
  });
  await telegram.flushOutbox();

  //  ساعتِ ۶ صبحِ کابل ⇒ هنوز نه
  const day = Date.UTC(2031, 2, 3);
  const early = day + 1.5 * 3600e3;          // ۶:۰۰ کابل
  const nine = day + 4.5 * 3600e3;           // ۹:۰۰ کابل
  clear();
  assert.equal((await telegram.announceTick(early)).skipped, 'early');

  const r1 = await telegram.announceTick(nine);
  assert.ok(r1.queued >= 1);
  await telegram.flushOutbox();
  const a = sent().filter(c => c.params.chat_id === '9003').map(c => c.params.text).join('\n');
  assert.match(a, /📢 اعلامیهٔ امروزِ پمپِ/);
  assert.match(a, /پطرول: ۰ لیتر مانده — خالی است/);
  assert.match(a, /بازپرسی شود:\n {3}نادر \(پطرول\)/);
  assert.match(a, /متوجه باشید:\n {3}قادر/);
  assert.match(a, /شرکتِ ب: ۵٬۰۰۰ افغانی/);
  assert.match(a, /بازپرسی کنید چرا به نادر اضافه دادید\./);

  //  همان روز دوباره ⇒ هیچ
  clear();
  await telegram.announceTick(nine + 3600e3);
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '9003').length, 0, 'اعلامیه دو بار آمد');

  //  گروه وصل شد ⇒ فردا فقط گروه
  await linkGroup(-1009003, 9003);
  clear();
  await telegram.announceTick(nine + 86400e3);
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '9003').length, 0, '⛔ هم خصوصی هم گروه');
  assert.equal(sent().filter(c => c.params.chat_id === '-1009003').length, 1);
});

/* ══════════════════════════════════════════════════════════════════
   ۱۰) «چندین شعبه توی یک گروه — پیام‌ها و حساب‌ها قاطی نشن»
   ══════════════════════════════════════════════════════════════════ */

test('چند شعبه در یک گروه و یک خصوصی: هر شعبه جدا، هم‌نامی یکی نمی‌شود', async () => {
  const a = await pumpOwner('شعبهٔ الف');
  const b = await pumpOwner('شعبهٔ ب');
  const devA = await bindDevice(a, 'pc-br-a');
  const devB = await bindDevice(b, 'pc-br-b');

  //  خصوصی: اول الف، بعد «افزودنِ شعبه» ⇒ ب — الف دست نمی‌خورد
  await linkPrivate(9101, a);
  await press(9101, 'settings');
  assert.ok(JSON.stringify(rows(lastTo(9101))).includes('"addacct"'), 'دکمهٔ «افزودنِ شعبه» نیست');
  await press(9101, 'addacct');
  await msg(9101, b.email);
  const code = await one(
    'SELECT id FROM otp_codes WHERE destination=$1 AND purpose=$2 ORDER BY created_at DESC LIMIT 1',
    [b.email, telegram.PURPOSE]
  );
  clear();
  await msg(9101, (await otp.reveal(code.id)).code);
  assert.match(lastTo(9101).params.text, /شعبهٔ تازه اضافه شد/);
  const priv = await many('SELECT station_id FROM telegram_chat_links WHERE chat_id=$1 ORDER BY linked_at', ['9101']);
  assert.deepEqual(priv.map(r => r.station_id), [a.stationId, b.stationId], 'هر دو شعبه، به ترتیب');
  assert.equal((await one('SELECT station_id FROM telegram_chats WHERE chat_id=$1', ['9101'])).station_id, a.stationId,
    'نخستین شعبه جابه‌جا شد');
  assert.match(lastTo(9101).params.text, new RegExp(a.stationName));
  assert.match(lastTo(9101).params.text, new RegExp(b.stationName));

  //  «کریم»ِ الف و «کریم»ِ ب دو حسابِ جدا با یک کلید (d1)
  await state(devA, {
    alerts: [debtOut(1, 'کریم')],
    debtors: [{ n: 'کریم', sp: 'out', sd: 'none', sm: 'none', p: -40, d: 0, m: 0 }],
  });
  await state(devB, {
    alerts: [],
    debtors: [{ n: 'کریم', sp: 'ok', sd: 'none', sm: 'none', p: 900, d: 0, m: 0 }],
  });
  await telegram.flushOutbox();

  //  گروه: یک دکمه ⇒ هر دو شعبه
  clear();
  await linkGroup(-1009101, 9101);
  const gl = await many('SELECT station_id FROM telegram_chat_links WHERE chat_id=$1', ['-1009101']);
  assert.equal(gl.length, 2, 'هر دو شعبه به گروه نرفت');

  //  هشدار: هر شعبه پیامِ خودش را، با نامِ خودش؛ خصوصی ساکت
  clear();
  await state(devB, {
    alerts: [debtOut(1, 'کریم')],
    debtors: [{ n: 'کریم', sp: 'out', sd: 'none', sm: 'none', p: -5, d: 0, m: 0 }],
  });
  await telegram.flushOutbox();
  const g = sent().filter(c => c.params.chat_id === '-1009101').map(c => c.params.text);
  assert.equal(g.length, 1, 'فقط هشدارِ شعبهٔ ب باید بیاید');
  assert.ok(g[0].includes(`«${b.stationName}»`));
  assert.ok(!g[0].includes(`«${a.stationName}»`), '⛔ نامِ شعبهٔ الف روی هشدارِ ب');
  assert.equal(sent().filter(c => c.params.chat_id === '9101').length, 0, '⛔ هم خصوصی هم گروه');

  //  وضعیت: هر شعبه بخشِ خودش
  clear();
  await msg(-1009101, `/status@${BOT}`, { type: 'supergroup', from: { id: 9101 } });
  const st = to(-1009101).map(c => c.params.text).join('\n');
  const parts = st.split('━━━━━━━━━━');
  assert.equal(parts.length, 2, 'دو بخش برای دو شعبه');
  //  هر بخش نامِ دقیقاً یک شعبه را دارد (ترتیب مهم نیست)
  const only = (t, x, y) => t.includes(`«${x}»`) && !t.includes(`«${y}»`);
  assert.ok(parts.some(t => only(t, a.stationName, b.stationName)), 'بخشِ الف');
  assert.ok(parts.some(t => only(t, b.stationName, a.stationName)), 'بخشِ ب');

  //  جست‌وجو: دو «کریم»، هر کدام زیرِ شعبهٔ خودش، با عددِ خودش
  clear();
  await msg(-1009101, `/find@${BOT} کریم`, { type: 'supergroup', from: { id: 9101 } });
  const f = to(-1009101).map(c => c.params.text).join('\n');
  const secs = f.split('━━━━━━━━━━');
  assert.equal(secs.length, 2, 'دو کریم در دو بخش');
  const fa = secs.find(t => t.includes(`«${a.stationName}»`)) || '';
  const fb = secs.find(t => t.includes(`«${b.stationName}»`)) || '';
  assert.ok(fa !== fb && fa.includes('−۴۰'), 'کریمِ الف با عددِ خودش');
  assert.ok(fb.includes('−۵'), 'کریمِ ب با عددِ خودش');
  assert.ok(!fa.includes('−۵ ') && !fb.includes('−۴۰'), '⛔ حسابِ دو کریم قاطی شد');

  //  برطرف شدنِ الف فقط برای الف
  clear();
  await state(devA, { alerts: [] });
  await telegram.flushOutbox();
  const fixed = sent().filter(c => c.params.chat_id === '-1009101').map(c => c.params.text);
  assert.equal(fixed.length, 1);
  assert.ok(fixed[0].includes(`«${a.stationName}»`));
  assert.ok(!fixed[0].includes(`«${b.stationName}»`));

  //  اعلامیهٔ صبح: هر شعبه یکی
  const nine = Date.UTC(2032, 5, 7) + 4.5 * 3600e3;
  clear();
  await telegram.announceTick(nine);
  await telegram.flushOutbox();
  const ann = sent().filter(c => c.params.chat_id === '-1009101').map(c => c.params.text);
  assert.equal(ann.length, 2, 'اعلامیهٔ هر شعبه جدا');
  assert.ok(ann.some(t => t.includes(`«${a.stationName}»`)) && ann.some(t => t.includes(`«${b.stationName}»`)));
  clear();
  await telegram.announceTick(nine + 3600e3);
  await telegram.flushOutbox();
  assert.equal(sent().filter(c => c.params.chat_id === '-1009101').length, 0, 'اعلامیه دو بار آمد');

  //  کدِ پمپِ هر شعبه دکمهٔ خودش؛ شناسهٔ پمپِ بیگانه پذیرفته نمی‌شود
  responder = (m) => (m === 'getChatMember' ? { ok: true, result: { status: 'creator' } } : null);
  const stranger = await pumpOwner('بیگانه');
  clear();
  await press(-1009101, `code:${stranger.stationId}`, { type: 'supergroup', from: { id: 555 } });
  const ans = calls.filter(c => c.method === 'answerCallbackQuery').map(c => c.params.text || '').join('\n');
  assert.ok(!ans.includes('کدِ پمپِ'), '⛔ کدِ پمپِ بیگانه نشان داده شد');

  //  جدا کردنِ یک شعبه از گروه ⇒ آن شعبه دوباره در خصوصی، دیگری در گروه می‌ماند
  await press(-1009101, `rmy:${b.stationId}`, { type: 'supergroup', from: { id: 555 } });
  responder = null;
  const left = await many('SELECT station_id FROM telegram_chat_links WHERE chat_id=$1', ['-1009101']);
  assert.deepEqual(left.map(r => r.station_id), [a.stationId]);
  clear();
  await state(devB, { alerts: [debtOut(1, 'کریم'), debtOut(2, 'نصیر')] });
  await state(devA, { alerts: [debtOut(3, 'بشیر')] });
  await telegram.flushOutbox();
  const pv = sent().filter(c => c.params.chat_id === '9101').map(c => c.params.text).join('\n');
  const gv = sent().filter(c => c.params.chat_id === '-1009101').map(c => c.params.text).join('\n');
  assert.match(pv, /نصیر/, 'شعبهٔ جداشده به خصوصی برنگشت');
  assert.doesNotMatch(pv, /بشیر/, '⛔ شعبهٔ الف هم در خصوصی');
  assert.match(gv, /بشیر/);
  assert.doesNotMatch(gv, /نصیر/, '⛔ شعبهٔ جداشده هنوز در گروه');
});
