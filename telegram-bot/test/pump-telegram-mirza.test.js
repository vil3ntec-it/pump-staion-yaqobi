'use strict';
/**
 * باتِ تلگرام — «💬 چت‌های میرزا» و «🏷️ نرخِ اتحادیه از تلگرام».
 *
 * خواستهٔ صاحب سامانه (۱۴۰۵/۰۷/۱۶): «توی منو قابلیتِ جدید بذار به اسمِ چت‌های
 * میرزا… بات کدِ هشت‌رقمیِ برنامه رو بخواد… وقتی برنامه خاموشه، پیام‌های
 * مشتری‌های کیو‌آر بیاد تلگرام… و نرخِ اتحادیه رو از تلگرام بنویسم و اتومات
 * توی برنامهٔ کامپیوتر لایف بشینه… روی حساب‌های دیگهٔ کاربران تأثیری نذاره.»
 */
const test = require('node:test');
const assert = require('node:assert/strict');
const h = require('./helpers');
const { query, one, newId, now } = require('../src/db');
const telegram = require('../src/lib/bots').pump();
const rates = require('../src/lib/station-rates');
const otp = require('../src/lib/otp');

const TOKEN = `123456789:${'Mr_z-'.repeat(8)}`;
const BOT = 'PumpMirzaBot';
const KEY = 'abcdef0123456789abcd';

let calls = [];
let responder = null;
let updateId = 9000;
let msgSeq = 700;

test.before(async () => {
  await h.start();
  const pw = require('../src/lib/password');
  await query(
    `INSERT INTO admins (id, username, name, password_hash, role, status, created_at)
     VALUES ($1,'admin','مدیر',$2,'superadmin','active',$3)`,
    [newId('adm'), await pw.hashPassword('Admin!12345'), now()]
  );
  telegram.setTransport(async (method, params) => {
    calls.push({ method, params });
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
const to = (chatId) => calls.filter(c => ['sendMessage', 'editMessageText'].includes(c.method)
  && c.params.chat_id === String(chatId));
const lastTo = (chatId) => to(chatId).at(-1);
const textsTo = (chatId) => to(chatId).map(c => c.params.text).join('\n---\n');
const rows = (m) => m?.params?.reply_markup?.inline_keyboard || [];

function msg(chatId, text, { type = 'private', from = null, replyTo = 0 } = {}) {
  updateId += 1;
  const chat = type === 'private' ? { id: chatId, type, first_name: 'میرزا' } : { id: chatId, type, title: 'گروهِ پمپ' };
  const m = { message_id: updateId, chat, text, from: from || { id: chatId, first_name: 'میرزا' } };
  if (replyTo) m.reply_to_message = { message_id: replyTo, chat };
  return telegram.handleUpdate({ update_id: updateId, message: m });
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

/** پمپ + کامپیوترِ بند‌شده + یک حسابِ قرض‌دار با کیو‌آرِ زنده (‎acct-d7‎). */
async function pump(name) {
  const u = await h.newUser(name, 'pump');
  const made = await h.post('/api/pump', { name: `پمپِ ${name}` }, { token: u.accessToken });
  assert.equal(made.status, 201, JSON.stringify(made.body));
  const bound = await h.post('/api/pump/device/bind',
    { device: { uid: `pc-${name}`, name: 'کامپیوترِ پمپ', platform: 'windows' } }, { token: u.accessToken });
  assert.equal(bound.status, 201, JSON.stringify(bound.body));
  const dev = bound.body.deviceToken;
  const put = await h.put('/api/pump/device/files/acct-d7', { data: { v: 1, k: KEY, at: 1, d: { n: 'هارون' } } }, { token: dev });
  assert.ok(put.status < 300, JSON.stringify(put.body));
  const ac = await h.get('/api/pump/device/access-code', { token: dev });
  assert.equal(ac.status, 200, JSON.stringify(ac.body));
  return {
    ...u, dev, stationId: made.body.station.id, stationName: made.body.station.name,
    code: made.body.station.code, access: String(ac.body.code).replace(/\D/g, ''),
  };
}

/** برنامهٔ کامپیوتر بسته شد: دستگاه ده دقیقه است چیزی نپرسیده. */
const desktopOff = (p) => query('UPDATE station_devices SET last_seen_at=$2 WHERE station_id=$1', [p.stationId, now() - 10 * 60_000]);

async function customerSays(p, text) {
  const r = await h.post(`/api/pump/public/${p.code}/acct/d7/chat?k=${KEY}`, { name: 'هارون', text });
  assert.equal(r.status, 201, JSON.stringify(r.body));
  //  رساندن به تلگرام منتظرِ پاسخ نمی‌ماند
  await new Promise(r2 => setTimeout(r2, 150));
  return r.body.message;
}

const fa = (s) => String(s).replace(/\d/g, d => '۰۱۲۳۴۵۶۷۸۹'[Number(d)]);

async function linkRelay(chatId, p, opts = {}) {
  await press(chatId, 'mirza', opts);
  await press(chatId, 'mzadd', opts);
  //  ⚠️ با رقمِ فارسی و خط‌تیره — همان‌طور که میرزا تایپ می‌کند
  await msg(chatId, fa(`${p.access.slice(0, 4)}-${p.access.slice(4)}`), opts);
}

async function linkPrivateEmail(chatId, user) {
  await msg(chatId, '/start');
  await msg(chatId, user.email);
  const row = await one(
    'SELECT id FROM otp_codes WHERE destination=$1 AND purpose=$2 ORDER BY created_at DESC LIMIT 1',
    [user.email, telegram.PURPOSE]);
  await msg(chatId, (await otp.reveal(row.id)).code);
}

/* ══════════════════ نرخ: خواندنِ پیام ══════════════════ */

test('خواندنِ نرخ: سه شکلی که صاحبِ سامانه نوشت، رقمِ فارسی، و عددِ ناممکن', () => {
  assert.deepEqual(rates.parse('پطرول ۴۵ دیزل ۹۹'), { petrol: 45, diesel: 99 });
  assert.deepEqual(rates.parse('نرخ اتحادیه پطرول ۷۹ . دیزل ۸۰'), { petrol: 79, diesel: 80 });
  assert.deepEqual(rates.parse('نرخ جدید پطرول ۷۹ . دیزل'), { petrol: 79, diesel: null });
  assert.deepEqual(rates.parse('دیزل: ۸۲٫۵'), { petrol: null, diesel: 82.5 });
  assert.equal(rates.parse('سلام، امروز پطرول نداریم'), null, 'نامِ تیل بی عدد نرخ نیست');
  assert.equal(rates.parse('کریم'), null);
  assert.deepEqual(rates.parse('پطرول ۴ دیزل ۸۰'), { petrol: null, diesel: 80, bad: ['petrol'] });
});

/* ══════════════════ چت‌های میرزا ══════════════════ */

test('منو دکمهٔ «💬 چت‌های میرزا» دارد — حتی برای کسی که هنوز وصل نیست', async () => {
  clear();
  await msg(9100, '/start');
  const kb = rows(lastTo(9100)).flat().map(b => b.callback_data);
  assert.ok(kb.includes('mirza'), JSON.stringify(kb));
  assert.match(lastTo(9100).params.text, /گامِ ۱/, 'راهنمای قدم‌به‌قدم برای تازه‌وارد');
});

test('وصل شدن با کدِ هشت‌رقمی (رقمِ فارسی) ⇒ پیامِ مشتری فقط وقتی برنامه خاموش است می‌آید', async () => {
  const p = await pump('میرزا');
  clear();
  await linkRelay(9101, p);
  assert.match(textsTo(9101), /✅ وصل شد/, textsTo(9101));
  const r = await one('SELECT chat_id FROM telegram_relays WHERE station_id=$1', [p.stationId]);
  assert.equal(r.chat_id, '9101');

  //  برنامه روشن است (همین حالا فایل فرستاد) ⇒ هیچ پیامی به تلگرام
  clear();
  await customerSays(p, 'سلام، الباقیِ من چند است؟');
  assert.equal(to(9101).length, 0, '⛔ برنامهٔ روشن خودش صندوق دارد');

  //  برنامه خاموش شد ⇒ همان لحظه به تلگرام، با نام و راهنمای Reply
  await desktopOff(p);
  clear();
  await customerSays(p, 'فردا دیزل دارید؟');
  const got = lastTo(9101);
  assert.ok(got, 'پیامِ مشتری به تلگرام نرسید');
  assert.match(got.params.text, /فردا دیزل دارید؟/);
  assert.match(got.params.text, /هارون/);
  assert.match(got.params.text, /Reply/);
  assert.equal(got.params.parse_mode, undefined, 'بی parse_mode');
});

test('↩️ جواب با Reply ⇒ به همان مشتری می‌رسد؛ Reply روی پیامِ دیگر ⇒ هیچ', async () => {
  const p = await pump('جواب');
  await linkRelay(9102, p);
  await desktopOff(p);
  clear();
  await customerSays(p, 'کی باز هستید؟');
  const relayed = lastTo(9102);
  const mid = msgSeq;
  assert.ok(relayed);

  clear();
  await msg(9102, 'تا ساعتِ ده شب باز هستیم', { replyTo: mid });
  assert.match(textsTo(9102), /✅ جواب به مشتری رسید/);
  const list = await h.get(`/api/pump/public/${p.code}/acct/d7/chat?k=${KEY}`);
  const last = list.body.messages.at(-1);
  assert.equal(last.from, 'o');
  assert.equal(last.text, 'تا ساعتِ ده شب باز هستیم');

  //  Reply روی پیامی که از مشتری نیامده (مثلاً منو) ⇒ مالِ ما نیست
  const before = (await h.get(`/api/pump/public/${p.code}/acct/d7/chat?k=${KEY}`)).body.messages.length;
  await msg(9102, 'این جواب نیست', { replyTo: 1 });
  const after = (await h.get(`/api/pump/public/${p.code}/acct/d7/chat?k=${KEY}`)).body.messages.length;
  assert.equal(after, before, '⛔ هیچ پیامی به مشتری نرفت');

  //  ⛔ همان شمارهٔ پیام در گفت‌وگوی دیگری نگاشت ندارد
  await msg(9199, 'از گفت‌وگوی بیگانه', { replyTo: mid });
  const after2 = (await h.get(`/api/pump/public/${p.code}/acct/d7/chat?k=${KEY}`)).body.messages.length;
  assert.equal(after2, before);
});

test('⛔ پمپِ دیگر: پیامِ مشتریِ پمپِ «ب» به تلگرامِ پمپِ «الف» نمی‌رود', async () => {
  const a = await pump('الفِ');
  const b = await pump('بِ');
  await linkRelay(9103, a);
  await desktopOff(a);
  await desktopOff(b);
  clear();
  await customerSays(b, 'پیامِ مشتریِ پمپِ ب');
  assert.equal(to(9103).length, 0, JSON.stringify(to(9103)));
});

test('⛔ کدِ غلط: شش بار ⇒ یک ساعت بسته؛ و هیچ پیوندی ساخته نمی‌شود', async () => {
  clear();
  await press(9104, 'mzadd');
  for (let i = 0; i < 6; i++) await msg(9104, '12345678');
  assert.match(textsTo(9104), /پیدا نشد/);
  await press(9104, 'mzadd');
  await msg(9104, '87654321');
  assert.match(lastTo(9104).params.text, /زیاد شد/);
  const any = await one('SELECT count(*)::int AS n FROM telegram_relays WHERE chat_id=$1', ['9104']);
  assert.equal(any.n, 0);
});

test('⛔ کدِ پمپ که عوض شد، «چت‌های میرزا»ی قبلی همان لحظه خاموش است', async () => {
  const p = await pump('چرخش');
  await linkRelay(9105, p);
  await desktopOff(p);
  const rot = await h.post('/api/pump/device/access-code/rotate', {}, { token: p.dev });
  assert.ok(rot.status < 300, JSON.stringify(rot.body));
  await desktopOff(p);
  clear();
  await customerSays(p, 'بعد از عوض شدنِ کد');
  assert.equal(to(9105).length, 0, '⛔ کدِ کهنه نباید پیامی برساند');
});

test('یک پمپ، یک مقصد: وصل کردن در جای دوم ⇒ جای اول خبر می‌گیرد و دیگر پیامی نمی‌گیرد', async () => {
  const p = await pump('جابه‌جا');
  await linkRelay(9106, p);
  clear();
  await linkRelay(9107, p);
  assert.match(textsTo(9106), /جای دیگری می‌آید/);
  await desktopOff(p);
  clear();
  await customerSays(p, 'سلام');
  assert.equal(to(9106).length, 0);
  assert.equal(to(9107).length, 1);
});

test('⛔ در گروه فقط مدیرِ گروه «وصل کردنِ پمپ» را می‌زند؛ کدِ عضوِ دیگر پذیرفته نمی‌شود', async () => {
  const p = await pump('گروهی');
  const g = -100900;
  clear();
  await press(g, 'mzadd', { type: 'supergroup', from: { id: 555 } });
  const denied = calls.filter(c => c.method === 'answerCallbackQuery').at(-1);
  assert.match(denied.params.text || '', /فقط مدیرانِ/);

  responder = async (method, params) => (method === 'getChatMember' && String(params.user_id) === '556'
    ? { ok: true, result: { status: 'administrator' } } : null);
  try {
    await press(g, 'mzadd', { type: 'supergroup', from: { id: 556 } });
    //  کسِ دیگری در گروه کدی بنویسد ⇒ نادیده
    await msg(g, p.access, { type: 'supergroup', from: { id: 999 } });
    assert.equal((await one('SELECT count(*)::int AS n FROM telegram_relays WHERE chat_id=$1', [String(g)])).n, 0);
    //  خودِ مدیر ⇒ وصل
    await msg(g, p.access, { type: 'supergroup', from: { id: 556 } });
    assert.equal((await one('SELECT chat_id FROM telegram_relays WHERE station_id=$1', [p.stationId])).chat_id, String(g));
  } finally { responder = null; }
});

/* ══════════════════ نرخِ اتحادیه ══════════════════ */

test('نرخ از تلگرام ⇒ فقط برنامهٔ همان پمپ می‌گیرد، «نشست» می‌گوید و بات خبر می‌دهد', async () => {
  const a = await pump('نرخ‌الف');
  const b = await pump('نرخ‌ب');
  await linkPrivateEmail(9201, a);
  clear();
  await msg(9201, 'نرخ اتحادیه پطرول ۷۹ . دیزل ۸۰');
  assert.match(textsTo(9201), /⏳ نرخِ اتحادیه/);
  assert.match(textsTo(9201), /دست نمی‌خورند/);

  const mine = await h.get('/api/pump/device/rate', { token: a.dev });
  assert.equal(mine.status, 200);
  assert.equal(mine.body.cmd.petrol, 79);
  assert.equal(mine.body.cmd.diesel, 80);
  //  ⛔ پمپِ دیگر هیچ فرمانی نمی‌بیند
  const other = await h.get('/api/pump/device/rate', { token: b.dev });
  assert.equal(other.body.cmd, null);
  //  ⛔ و نمی‌تواند فرمانِ پمپِ دیگر را «نشست» کند
  const steal = await h.post(`/api/pump/device/rate/${mine.body.cmd.id}/ack`, { applied: true }, { token: b.dev });
  assert.equal(steal.status, 404);

  clear();
  const ack = await h.post(`/api/pump/device/rate/${mine.body.cmd.id}/ack`, { applied: true }, { token: a.dev });
  assert.equal(ack.status, 200, JSON.stringify(ack.body));
  await new Promise(r => setTimeout(r, 100));
  assert.match(textsTo(9201), /✅ نرخِ اتحادیهٔ .* نشست/);
  assert.equal((await h.get('/api/pump/device/rate', { token: a.dev })).body.cmd, null, 'دیگر در صف نیست');
  assert.equal((await h.post(`/api/pump/device/rate/${mine.body.cmd.id}/ack`, {}, { token: a.dev })).status, 404, 'دو بار نه');
});

test('آخرین حرف مرجع است: «۴۵» بعد «۴۶» ⇒ برنامه فقط ۴۶ را می‌گیرد', async () => {
  const p = await pump('نرخ‌دوبار');
  await linkPrivateEmail(9202, p);
  await msg(9202, 'پطرول ۴۵');
  await msg(9202, 'پطرول ۴۶');
  const got = await h.get('/api/pump/device/rate', { token: p.dev });
  assert.equal(got.body.cmd.petrol, 46);
  assert.equal(got.body.cmd.diesel, null, 'دیزلِ نگفته دست نمی‌خورد');
});

test('«درجا»: پرسشِ باز (?wait=) همان لحظهٔ پیامِ تلگرام جواب می‌گیرد، نه دقیقهٔ بعد', async () => {
  const a = await pump('درجاالف');
  const b = await pump('درجاب');
  await linkPrivateEmail(9291, a);
  const until = async (fn) => { for (let i = 0; i < 100 && !fn(); i++) await new Promise(r => setTimeout(r, 10)); };

  //  برنامهٔ هر دو پمپ منتظر است
  const t0 = Date.now();
  const pa = h.get('/api/pump/device/rate?wait=10', { token: a.dev });
  const pb = h.get('/api/pump/device/rate?wait=1', { token: b.dev });
  await until(() => rates.waiting(a.stationId) === 1);
  assert.equal(rates.waiting(a.stationId), 1, 'برنامه باید منتظر بماند');
  await msg(9291, 'پطرول ۸۱ دیزل ۸۲');
  const got = await pa;
  const ms = Date.now() - t0;
  assert.equal(got.status, 200, JSON.stringify(got.body));
  assert.equal(got.body.cmd.petrol, 81);
  assert.equal(got.body.cmd.diesel, 82);
  assert.equal(got.body.waitMax, rates.WAIT_MAX_S, 'برنامه از این می‌فهمد که سرور «درجا» را می‌شناسد');
  assert.ok(ms < 5000, `باید درجا برسد، نه با پایانِ مهلت: ${ms}ms`);
  assert.ok('liveConfig' in got.body, 'نسخهٔ تنظیماتِ زنده همچنان روی همین پاسخ');

  //  ⛔ پمپِ دیگر بیدار نشد: با پایانِ مهلتِ خودش، بی فرمان
  const other = await pb;
  assert.equal(other.body.cmd, null);

  //  فرمانِ در صف ⇒ بی انتظار همان لحظه
  const t1 = Date.now();
  const again = await h.get('/api/pump/device/rate?wait=10', { token: a.dev });
  assert.equal(again.body.cmd.petrol, 81);
  assert.ok(Date.now() - t1 < 2000);

  //  سقفِ انتظار: عددِ بزرگ به WAIT_MAX_S بریده می‌شود و بی `wait` همان رفتارِ قدیم
  await h.post(`/api/pump/device/rate/${again.body.cmd.id}/ack`, { applied: true }, { token: a.dev });
  const t2 = Date.now();
  assert.equal((await h.get('/api/pump/device/rate', { token: a.dev })).body.cmd, null);
  assert.ok(Date.now() - t2 < 2000, 'بی wait پاسخ منتظر نمی‌ماند');
});

test('«درجا»: برنامه‌ای که اتصال را بست شنونده‌ای روی سرور جا نمی‌گذارد', async () => {
  const p = await pump('درجابسته');
  const ctrl = new AbortController();
  const req = fetch(`${h.base()}/api/pump/device/rate?wait=10`, {
    headers: { Authorization: `Bearer ${p.dev}` }, signal: ctrl.signal,
  }).catch(() => null);
  for (let i = 0; i < 100 && rates.waiting(p.stationId) === 0; i++) await new Promise(r => setTimeout(r, 10));
  assert.equal(rates.waiting(p.stationId), 1);
  ctrl.abort();
  await req;
  for (let i = 0; i < 100 && rates.waiting(p.stationId) > 0; i++) await new Promise(r => setTimeout(r, 10));
  assert.equal(rates.waiting(p.stationId), 0, 'شنوندهٔ اتصالِ بسته باید پاک شود');
  //  و فرمانِ بعدی همچنان در صف می‌نشیند تا پرسشِ بعدی
  await rates.create(p.stationId, { petrol: 70 });
  assert.equal((await h.get('/api/pump/device/rate', { token: p.dev })).body.cmd.petrol, 70);
});

test('«درجا»: ⛔ یک توکنِ دستگاه بیش از سقف پرسشِ باز نگه نمی‌دارد', async () => {
  const p = await pump('درجاسقف');
  const ctrls = [];
  const open = [];
  for (let i = 0; i < rates.MAX_WAITERS; i++) {
    const c = new AbortController();
    ctrls.push(c);
    open.push(fetch(`${h.base()}/api/pump/device/rate?wait=10`, {
      headers: { Authorization: `Bearer ${p.dev}` }, signal: c.signal,
    }).catch(() => null));
  }
  for (let i = 0; i < 200 && rates.waiting(p.stationId) < rates.MAX_WAITERS; i++) await new Promise(r => setTimeout(r, 10));
  assert.equal(rates.waiting(p.stationId), rates.MAX_WAITERS);
  //  پرسشِ اضافه منتظر نمی‌ماند
  const t = Date.now();
  const extra = await h.get('/api/pump/device/rate?wait=10', { token: p.dev });
  assert.equal(extra.status, 200);
  assert.ok(Date.now() - t < 2000, `پرسشِ بیش از سقف باید فوری جواب بگیرد: ${Date.now() - t}ms`);
  assert.equal(rates.waiting(p.stationId), rates.MAX_WAITERS);
  ctrls.forEach(c => c.abort());
  await Promise.all(open);
  for (let i = 0; i < 200 && rates.waiting(p.stationId) > 0; i++) await new Promise(r => setTimeout(r, 10));
  assert.equal(rates.waiting(p.stationId), 0);
});

test('⛔ نرخ: بی ایمیل، کارمند، عددِ ناممکن و گروهِ بی «نرخ» — هیچ فرمانی ساخته نمی‌شود', async () => {
  const p = await pump('نرخ‌قفل');
  const count = async () => (await one('SELECT count(*)::int AS n FROM station_rate_cmds WHERE station_id=$1', [p.stationId])).n;

  //  فقط کدِ هشت‌رقمی (چت‌های میرزا) ⇒ نه — و شناسه‌اش را می‌گوید تا صاحبِ پمپ اضافه کند
  await linkRelay(9203, p);
  clear();
  await msg(9203, 'پطرول ۷۰ دیزل ۷۰');
  assert.match(textsTo(9203), /مدیرِ نرخ/);
  assert.match(textsTo(9203), /9203/);

  //  کارمند (عضوِ فعال، نه صاحب و نه مدیر) ⇒ نه
  const staff = await h.newUser('کارمندِ نرخ', 'pump');
  await query(
    `INSERT INTO station_members (id, station_id, user_id, role, status, created_at, updated_at)
     VALUES ($1,$2,$3,'staff','active',$4,$4)`, [newId('mem'), p.stationId, staff.userId || staff.user?.id || staff.id, now()]);
  await linkPrivateEmail(9204, staff);
  clear();
  await msg(9204, 'پطرول ۷۰');
  assert.match(textsTo(9204), /مدیرِ نرخ/);

  //  عددِ ناممکن ⇒ نه، و می‌گوید چرا
  await linkPrivateEmail(9205, p);
  clear();
  await msg(9205, 'پطرول ۷ دیزل ۸۰۰');
  assert.match(textsTo(9205), /باورکردنی نیست/);

  //  گروه: «پطرول ۵۰ لیتر دادم» حرفِ کارمندان است ⇒ بات ساکت
  const g = -100901;
  clear();
  await msg(g, 'پطرول ۵۰ لیتر دادم', { type: 'supergroup', from: { id: 1234 } });
  assert.equal(to(g).length, 0);

  assert.equal(await count(), 0, JSON.stringify(await one('SELECT * FROM station_rate_cmds WHERE station_id=$1', [p.stationId])));
});

/* ══════════════════ 👮 مدیرانِ نرخ — فقط شناسه‌هایی که صاحبِ پمپ داده ══════════════════ */

async function addRateAdmins(ownerChat, text) {
  await press(ownerChat, 'rad');
  const add = rows(lastTo(ownerChat)).flat().find(b => String(b.callback_data).startsWith('radadd:'));
  assert.ok(add, 'دکمهٔ افزودن نیامد: ' + JSON.stringify(rows(lastTo(ownerChat))));
  await press(ownerChat, add.callback_data);
  await msg(ownerChat, text);
}

test('👮 صاحبِ پمپ شناسه می‌دهد ⇒ فقط همان شناسه‌ها نرخ را عوض می‌کنند، در خصوصی و در گروه', async () => {
  const p = await pump('فهرست');
  await linkPrivateEmail(9301, p);
  clear();
  await addRateAdmins(9301, fa('777001') + ' 777002');
  assert.match(textsTo(9301), /۲ شناسه اضافه شد/);
  const n = async () => (await one('SELECT count(*)::int AS n FROM station_rate_admins WHERE station_id=$1', [p.stationId])).n;
  assert.equal(await n(), 2);

  const cmds = async () => (await one('SELECT count(*)::int AS n FROM station_rate_cmds WHERE station_id=$1', [p.stationId])).n;
  //  مدیرِ نرخ در خصوصیِ خودش — بی هیچ ایمیلی
  clear();
  await msg(777001, 'پطرول ۸۱ دیزل ۸۲', { from: { id: 777001, first_name: 'کارفرما' } });
  assert.match(textsTo(777001), /⏳ نرخِ اتحادیه/);
  assert.equal(await cmds(), 1);
  //  نامش یاد گرفته شد
  assert.equal((await one('SELECT label FROM station_rate_admins WHERE station_id=$1 AND tg_id=$2', [p.stationId, '777001'])).label, 'کارفرما');

  //  ⛔ غریبه در خصوصی
  clear();
  await msg(888001, 'پطرول ۱۰۰ دیزل ۱۰۰', { from: { id: 888001 } });
  assert.match(textsTo(888001), /مدیرِ نرخ/);
  assert.equal(await cmds(), 1);

  //  گروه: مدیرِ گروه که در فهرست نیست ⇒ نه؛ مدیرِ نرخ ⇒ آری
  const g = -100930;
  responder = async (method) => (method === 'getChatMember' ? { ok: true, result: { status: 'administrator' } } : null);
  try {
    clear();
    await msg(g, 'نرخ پطرول ۹۰', { type: 'supergroup', from: { id: 888002 } });
    assert.match(textsTo(g), /مدیرانِ نرخ/);
    assert.equal(await cmds(), 1, '⛔ مدیرِ گروه به‌تنهایی کافی نیست');
    await msg(g, 'نرخ پطرول ۹۰', { type: 'supergroup', from: { id: 777002 } });
    assert.equal(await cmds(), 2);
  } finally { responder = null; }
  const got = await h.get('/api/pump/device/rate', { token: p.dev });
  assert.equal(got.body.cmd.petrol, 90);

  //  برداشتن ⇒ همان لحظه دیگر نه
  await press(9301, 'rad');
  const rm = rows(lastTo(9301)).flat().find(b => /777002/.test(b.text) || /radrm:/.test(b.callback_data || ''));
  const rmAll = rows(lastTo(9301)).flat().filter(b => String(b.callback_data).startsWith('radrm:'));
  assert.equal(rmAll.length, 2);
  const target = (await one('SELECT id FROM station_rate_admins WHERE station_id=$1 AND tg_id=$2', [p.stationId, '777002'])).id;
  await press(9301, `radrm:${target}`);
  assert.equal(await n(), 1);
  clear();
  await msg(g, 'نرخ پطرول ۹۵', { type: 'supergroup', from: { id: 777002 } });
  assert.equal(await cmds(), 2);
  assert.ok(rm);
});

test('⛔ فهرستِ یک پمپ پمپِ دیگر را باز نمی‌کند، و فقط صاحبِ همان پمپ فهرست را عوض می‌کند', async () => {
  const a = await pump('فهرست‌الف');
  const b = await pump('فهرست‌ب');
  await linkPrivateEmail(9311, a);
  await addRateAdmins(9311, '777311');
  //  777311 مدیرِ نرخِ «الف» است ⇒ فرمان فقط برای «الف»
  await msg(777311, 'پطرول ۷۷', { from: { id: 777311 } });
  assert.equal((await h.get('/api/pump/device/rate', { token: a.dev })).body.cmd.petrol, 77);
  assert.equal((await h.get('/api/pump/device/rate', { token: b.dev })).body.cmd, null, '⛔ پمپِ ب دست نخورد');

  //  کارمندِ «ب» نمی‌تواند فهرستِ کسی را بسازد
  const staff = await h.newUser('کارمندِ فهرست', 'pump');
  await query(
    `INSERT INTO station_members (id, station_id, user_id, role, status, created_at, updated_at)
     VALUES ($1,$2,$3,'staff','active',$4,$4)`, [newId('mem'), b.stationId, staff.userId || staff.user?.id || staff.id, now()]);
  await linkPrivateEmail(9312, staff);
  clear();
  await press(9312, 'rad');
  assert.match(textsTo(9312), /فقط صاحبِ پمپ/);
  await press(9312, `radadd:${b.stationId}`);
  await msg(9312, '999999');
  assert.equal((await one('SELECT count(*)::int AS n FROM station_rate_admins WHERE station_id=$1', [b.stationId])).n, 0);
  //  و دکمهٔ برداشتنِ فهرستِ «الف» از دستِ کسِ دیگر هیچ کاری نمی‌کند
  const id = (await one('SELECT id FROM station_rate_admins WHERE station_id=$1', [a.stationId])).id;
  await press(9312, `radrm:${id}`);
  assert.equal((await one('SELECT count(*)::int AS n FROM station_rate_admins WHERE station_id=$1', [a.stationId])).n, 1);

  //  /myid شناسه را می‌گوید
  clear();
  await msg(555123, '/myid', { from: { id: 555123 } });
  assert.match(textsTo(555123), /555123/);
});

test('⛔ نامِ کاربری به شناسهٔ نخستین کسی که با آن آمد سنجاق می‌شود — کسی که بعداً همان نام را بگیرد هیچ حقی ندارد', async () => {
  const p = await pump('فهرست‌نام');
  await linkPrivateEmail(9321, p);
  await addRateAdmins(9321, '@mirza_rate');
  const cmds = async () => (await one('SELECT count(*)::int AS n FROM station_rate_cmds WHERE station_id=$1', [p.stationId])).n;

  //  صاحبِ واقعیِ نام ⇒ پذیرفته و سنجاق شد
  await msg(777321, 'پطرول ۸۱', { from: { id: 777321, username: 'Mirza_Rate' } });
  assert.equal(await cmds(), 1);
  assert.equal((await one('SELECT tg_id FROM station_rate_admins WHERE station_id=$1', [p.stationId])).tg_id, '777321');

  //  نام رها شد و کسِ دیگری گرفتش ⇒ هیچ
  clear();
  await msg(888321, 'پطرول ۱۰۰', { from: { id: 888321, username: 'mirza_rate' } });
  assert.match(textsTo(888321), /مدیرِ نرخ/);
  assert.equal(await cmds(), 1);

  //  همان شخص با نامِ تازه ⇒ هنوز پذیرفته (شناسه ملاک است)
  await msg(777321, 'پطرول ۸۲', { from: { id: 777321, username: 'someone_else' } });
  assert.equal(await cmds(), 2);
});

/* ══════════════════ 🏷️ نرخ از منو: قالب در کادرِ نوشتن، نه پیام ══════════════════ */

test('🏷️ نرخ از منو: دکمه هیچ پیامی نمی‌فرستد و قالب (با آخرین نرخ) در کادرِ نوشتن می‌گذارد؛ پیامِ همان قالب نرخ را عوض می‌کند', async () => {
  const p = await pump('قالب');
  await linkPrivateEmail(9601, p);
  //  ⛔ /rate دیگر در فهرستِ «/»ِ تلگرام نیست (انتخابش همان لحظه پیام می‌فرستاد)
  const cmds = calls.filter(c => c.method === 'setMyCommands');
  for (const c of cmds) assert.ok(!c.params.commands.some(x => x.command === 'rate'), '⛔ rate در فهرستِ «/»');

  clear();
  await msg(9601, '/menu');
  const btn = rows(lastTo(9601)).flat().find(b => /نرخِ اتحادیه/.test(b.text));
  assert.ok(btn, 'دکمهٔ نرخ در منو');
  assert.equal(btn.callback_data, undefined, '⛔ دکمه callback ندارد ⇒ زدنش چیزی به سرور نمی‌فرستد');
  assert.equal(btn.switch_inline_query_current_chat, 'نرخ پطرول  دیزل ', 'بی تاریخچه: جای عدد خالی');

  //  همان چیزی که تلگرام در کادر می‌گذارد و کاربر با عددِ تازه می‌فرستد: «@بات نرخ …»
  clear();
  await msg(9601, `@${BOT} نرخ پطرول ۷۹ دیزل ۸۰`);
  assert.match(textsTo(9601), /⏳ نرخِ اتحادیه/);
  const got = await h.get('/api/pump/device/rate', { token: p.dev });
  assert.equal(got.body.cmd.petrol, 79);
  assert.equal(got.body.cmd.diesel, 80);
  await h.post(`/api/pump/device/rate/${got.body.cmd.id}/ack`, { applied: true }, { token: p.dev });

  //  قالبِ بعدی همان آخرین نرخِ نشسته را دارد — فقط عددِ تازه عوض می‌شود
  clear();
  await msg(9601, '/menu');
  const again = rows(lastTo(9601)).flat().find(b => /نرخِ اتحادیه/.test(b.text));
  assert.equal(again.switch_inline_query_current_chat, 'نرخ پطرول ۷۹ دیزل ۸۰');

  //  فقط پطرول (جای دیزل خالی ماند) ⇒ فقط پطرول
  clear();
  await msg(9601, `@${BOT} نرخ پطرول ۸۱ دیزل `);
  const only = await h.get('/api/pump/device/rate', { token: p.dev });
  assert.equal(only.body.cmd.petrol, 81);
  assert.equal(only.body.cmd.diesel, null);

  //  /rate تنها (اگر کسی تایپ کرد) ⇒ راهنما با همان دکمهٔ قالب، نه «بعد از /rate بنویسید»
  clear();
  await msg(9601, '/rate');
  const help = lastTo(9601);
  assert.match(help.params.text, /چیزی فرستاده نمی‌شود/);
  assert.ok(rows(help).flat().some(b => /^نرخ پطرول/.test(b.switch_inline_query_current_chat || '')));
});

test('🏷️ در گروه: قالبِ «@بات نرخ …» از مدیرِ نرخ می‌نشیند؛ از دیگری نه', async () => {
  const p = await pump('قالب‌گروه');
  await linkPrivateEmail(9602, p);
  await addRateAdmins(9602, '777601');
  const g = -100960;
  const n = async () => (await one('SELECT count(*)::int AS n FROM station_rate_cmds WHERE station_id=$1', [p.stationId])).n;
  responder = async (method) => (method === 'getChatMember' ? { ok: true, result: { status: 'member' } } : null);
  try {
    clear();
    await msg(g, `@${BOT} نرخ پطرول ۷۷ دیزل ۷۸`, { type: 'supergroup', from: { id: 888601 } });
    assert.equal(await n(), 0, '⛔ غریبه');
    await msg(g, `@${BOT} نرخ پطرول ۷۷ دیزل ۷۸`, { type: 'supergroup', from: { id: 777601, first_name: 'کارفرما' } });
    assert.equal(await n(), 1);
    assert.match(textsTo(g), /⏳ نرخِ اتحادیه/);
  } finally { responder = null; }
});

test('🏷️ اگر تلگرام دکمهٔ قالب را نپذیرفت، منو نمی‌افتد: دکمه به «راهنمای نرخ» بدل می‌شود', async () => {
  const p = await pump('بی‌قالب');
  await linkPrivateEmail(9603, p);
  responder = async (method, params) => {
    const kb = params?.reply_markup?.inline_keyboard;
    if ((method === 'sendMessage' || method === 'editMessageText') && kb && kb.flat().some(b => b.switch_inline_query_current_chat !== undefined)) {
      return { ok: false, error_code: 400, description: 'Bad Request: BUTTON_TYPE_INVALID' };
    }
    return null;
  };
  try {
    clear();
    await msg(9603, '/menu');
    const m = lastTo(9603);
    const b = rows(m).flat().find(x => /نرخِ اتحادیه/.test(x.text));
    assert.ok(b && b.callback_data === 'rate_help', JSON.stringify(rows(m)));
    clear();
    await press(9603, 'rate_help');
    assert.match(textsTo(9603), /نرخ پطرول ۷۹ دیزل ۸۰/);
  } finally { responder = null; }
});

