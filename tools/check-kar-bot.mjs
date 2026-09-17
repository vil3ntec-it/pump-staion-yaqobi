/* ══ آزمونِ رباتِ اپِ کارمندان ═══════════════════════════════════════════════
 *
 * ربات باید «هر شخص که اسم برد، هر بخش، هر ماه و همهٔ بخش‌ها» را جواب بدهد.
 * این فایل همان را می‌سنجد — بی مرورگر، با همان موتوری که در گوشی اجرا می‌شود.
 *
 *     node tools/check-kar-bot.mjs
 */
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const require = createRequire(import.meta.url);
const here = path.dirname(fileURLToPath(import.meta.url));
const bot = require(path.join(here, '..', 'kar', 'app.js'));

let bad = 0;
const ok = (cond, msg) => { if (!cond) { bad++; console.error('✗ ' + msg); } else console.log('✓ ' + msg); };

// ── یک ایستگاهِ نمونه، همان شکلی که ‎StationSnapshot‎ می‌سازد ────────────────
const DATA = {
  v: 1, seq: 1, at: '1405/06/22',
  gate: 'pbkdf2$sha256$210000$c2FsdA==$aGFzaA==',
  station: { name: 'پمپ یعقوبی' },
  tank: {
    petrol: { in: 10000, out: 2500, current: 7500, show: 7500, low: false, near: false },
    diesel: { in: 4000, out: 3900, current: 100, show: 100, low: true, near: false }
  },
  debtors: [
    { id: 1, name: 'هارون', phone: '0700', noinv: false, status: 'ok',
      bal: { money: 0, petrol: 200, diesel: 0 },
      accounts: [{ id: 2, title: '', unit: 'fuel', sum: [['الباقی', '200']], head: ['تاریخ'], rows: [['1405/06/10']] }] },
    { id: 3, name: 'محمد علی', phone: '', noinv: false, status: 'out',
      bal: { money: -500, petrol: 0, diesel: 0 }, accounts: [] }
  ],
  sections: {
    expense: {
      t: 'مصارف',
      head: ['تاریخ', 'عنوان', 'مقدار', 'یادداشت'],
      rows: [
        ['1405/05/03', 'برق', '1,200', ''],
        ['1405/06/04', 'نان', '800', ''],
        ['1405/06/20', 'ترمیم بابِ پمپ', '2,000', '']
      ],
      m: ['1405/05', '1405/06', '1405/06'],
      sum: [['جمله مصارف', '4,000']]
    },
    safe: {
      t: 'گاوصندوق', head: ['تاریخ', 'نوع', 'شرح', 'مقدار', 'واحد'],
      rows: [['1405/06/01', 'ماندگی', 'فروشِ روز', '9,000', 'افغانی']],
      m: ['1405/06'], sum: [['خالص افغانی', '9,000']]
    }
  }
};

const titles = (q) => bot.answer(q, DATA).map(b => b.title);
const block = (q, i = 0) => bot.answer(q, DATA)[i];

// ── ماه‌ها ────────────────────────────────────────────────────────────────
ok(bot.askedMonth('مصارف ماه 1405/06') === '1405/06', 'ماهِ «1405/06» خوانده می‌شود');
ok(bot.askedMonth('مصارف ماه ۱۴۰۵/۰۶') === '1405/06', 'رقمِ فارسی هم خوانده می‌شود');
ok(bot.askedMonth('مصارف سنبله') === '/06', 'نامِ ماهِ افغانی («سنبله») خوانده می‌شود');
ok(bot.askedMonth('مصارف شهریور ۱۴۰۵') === '1405/06', 'نامِ ماهِ ایرانی هم، با سال');
ok(bot.askedMonth('مصارف ماه ۶') === '/06', '«ماه ۶» یعنی ماهِ ششم');
ok(bot.askedMonth('سال ۱۴۰۵') === '1405/', 'سالِ تنها یعنی همهٔ ماه‌های آن سال');
ok(bot.askedMonth('حساب هارون') === null, 'جمله‌ای که ماه ندارد، ماه نمی‌سازد');
ok(bot.monthHit('1405/06', '/06') && !bot.monthHit('1404/06', '1405/06'), 'محکِ ماه درست می‌سنجد');

// ── شخص ──────────────────────────────────────────────────────────────────
ok(titles('حساب هارون')[0].indexOf('هارون') >= 0, 'نامِ قرض‌دار شناخته می‌شود');
ok(block('حساب هارون').person, 'حسابِ همان شخص با جدولش می‌آید');
ok(titles('محمد علی چطور است')[0].indexOf('محمد علی') >= 0, 'نامِ دوکلمه‌ای هم');
ok(String(block('هارون').kv[0][1]).indexOf('موجودی دارد') >= 0, 'حالِ «موجودی دارد» گفته می‌شود');
ok(String(block('محمد علی').kv[0][1]).indexOf('اضافه ندهید') >= 0,
   'برای حسابِ تمام‌شده صریح می‌گوید تیلِ اضافه ندهید');

// ── مخزن ─────────────────────────────────────────────────────────────────
ok(titles('مخزن')[0] === 'مخزن', '«مخزن» جواب دارد');
ok(JSON.stringify(block('موجودی تیل').kv).indexOf('کم آمده') >= 0, 'کمبودِ دیزل هشدار می‌دهد');

// ── بخش‌ها و ماه ─────────────────────────────────────────────────────────
ok(titles('مصارف')[0] === 'مصارف', 'بخشِ مصارف با نامش پیدا می‌شود');
ok(block('مصارف').table.rows.length === 3, 'بی ماه، همهٔ ردیف‌ها می‌آیند');
const june = block('مصارف ماه 1405/06');
ok(june.table.rows.length === 2, 'با ماه، فقط ردیف‌های همان ماه می‌مانند');
ok(JSON.stringify(june.kv).indexOf('2,800') >= 0, 'جمعِ همان ماه درست است (1,200 کنار می‌رود)');
ok(block('گاوصندوق').title === 'گاوصندوق', 'گاوصندوق هم');

// ── همه بخش‌ها ───────────────────────────────────────────────────────────
const all = titles('همه بخش ها');
ok(all[0].indexOf('خلاصهٔ ایستگاه') >= 0, '«همه بخش‌ها» با خلاصهٔ ایستگاه شروع می‌شود');
ok(all.length >= 3, 'و هر بخش یک بلوک دارد');

// ── ماهِ تنها، بی نامِ بخش ────────────────────────────────────────────────
const onlyMonth = bot.answer('ماه 1405/05', DATA);
ok(onlyMonth[0].title.indexOf('همهٔ بخش‌ها') >= 0, 'ماهِ تنها یعنی همهٔ بخش‌ها در آن ماه');
ok(onlyMonth.length >= 2, 'و بخشی که در آن ماه ردیف دارد می‌آید');

// ── جست‌وجوی آزاد ────────────────────────────────────────────────────────
const free = bot.answer('ترمیم باب', DATA);
ok(free.length === 1 && free[0].table.rows.length === 1,
   'واژه‌ای که فقط داخلِ یک ردیف است، همان ردیف را می‌آورد');

// ── حالت‌های خالی ────────────────────────────────────────────────────────
ok(bot.answer('', DATA)[0].title === 'چه بپرسم؟', 'پرسشِ خالی راهنمایی می‌گیرد');
ok(bot.answer('حساب هارون', null)[0].title.indexOf('هنوز داده') >= 0, 'بی داده، صریح می‌گوید');
ok(bot.answer('یک چیزِ بی‌ربطِ نایاب', DATA)[0].title === 'چیزی پیدا نشد',
   'پرسشِ بی‌جواب، فهرستِ بلدی‌ها را می‌دهد');

// ── یکسان‌سازیِ نوشته ────────────────────────────────────────────────────
ok(bot.norm('كتاب ي') === bot.norm('کتاب ی'), 'ك/ی عربی با فارسی یکی شمرده می‌شود');
ok(bot.num('۱٬۲۳۴') === 1234, 'عددِ فارسیِ کاما‌دار خوانده می‌شود');

// ── قفلِ رمز — همان هشی که ‎PasswordHasher‎ی دات‌نت می‌سازد ────────────────
//
// ⚠️ این مهم‌ترین آزمونِ این فایل است: اگر شکلِ هش یا کدگذاریِ رمز یک سرِ سوزن
// فرق کند، کارمند رمزِ درستِ خودش را می‌زند و اپ بازش نمی‌کند — و هیچ‌کس
// نمی‌فهمد چرا. پس این‌جا یک هشِ واقعی با ‎PBKDF2-HMAC-SHA256‎ (۲۱۰٬۰۰۰ دور،
// رمزِ UTF-8) ساخته و با همان تابعی سنجیده می‌شود که در گوشی اجرا می‌شود.
// (‎crypto.subtle‎ در Node ۱۸ به بالا سراسری است، مثلِ مرورگر)
import { pbkdf2Sync } from 'node:crypto';

const PASS = 'رمزِ من 1234';
const salt = Buffer.from('0123456789abcdef');
const stored = 'pbkdf2$sha256$210000$' + salt.toString('base64') + '$' +
  pbkdf2Sync(Buffer.from(PASS, 'utf8'), salt, 210000, 32, 'sha256').toString('base64');

ok(await bot.verifyPassword(PASS, stored), 'رمزِ درست باز می‌کند');
ok(!(await bot.verifyPassword('1234', stored)), 'رمزِ غلط باز نمی‌کند');
ok(!(await bot.verifyPassword('x', 'not-a-hash')), 'رشتهٔ خراب هم باز نمی‌کند — نه استثنا');

// ── دو در به سرور ─────────────────────────────────────────────────────────
//
// ⚠️ چرا این‌جا قفل می‌شود: اپِ کارمند روی گوشیِ کسی است که هیچ‌وقت نمی‌تواند
// خطای شبکه را برای ما بخواند. اگر نشانی یک نویسه فرق کند، فقط یک صفحهٔ خالی
// می‌بیند. پس شکلِ هر دو نشانی و هر دو مسیر همین‌جا نوشته و سنجیده می‌شود.

{
  const d = bot.doorsFor({ srv: 'https://api.example.com', tok: 'k1', stn: 'pump2' });

  ok(d.length === 2, 'دو در دارد: پوشهٔ اختصاصی، و راهِ قدیمی');
  ok(d[0].name === 'station' && d[1].name === 'legacy', 'اول درِ تازه امتحان می‌شود');

  ok(d[0].url === 'wss://api.example.com/station?station=pump2&token=k1',
     'درِ تازه: /station با کد و رمزِ همان پمپ');
  ok(d[0].path === 'live', 'و مسیرش داخلِ پوشهٔ همان پمپ است');

  ok(d[1].url === 'wss://api.example.com/?token=k1', 'درِ قدیمی: همان ریشه با رمز');
  ok(d[1].path === 'stations/pump2-live', 'و مسیرِ قدیمیِ شاخهٔ مشترک');

  // هر پمپ باید نشانیِ خودش را بگیرد، وگرنه دو پمپ یک دفتر را می‌خوانند
  const other = bot.doorsFor({ srv: 'https://api.example.com', tok: 'k2', stn: 'pump3' });
  ok(other[0].url !== d[0].url && other[1].path !== d[1].path,
     'دو پمپ هرگز به یک نشانی نمی‌روند');

  ok(bot.doorsFor({ srv: 'api.example.com' })[0].url.startsWith('wss://'),
     'نشانیِ بی‌پیشوند، امن فرض می‌شود');
  ok(bot.doorsFor({ srv: 'http://192.168.1.9:4700' })[0].url.startsWith('ws://192.168.1.9:4700/station'),
     'شبکهٔ خانگی با ws:// می‌ماند — wss اجباری آن‌جا اصلاً وصل نمی‌شود');
  ok(bot.doorsFor({ srv: 'https://x/' })[1].url === 'wss://x',
     'اسلشِ آخر دو تا نمی‌شود');
  ok(bot.doorsFor({ srv: 'https://x', tok: 'k' })[0].url.includes('station=pump1'),
     'کدِ نانوشته یعنی pump1');
}

// ══════════════════════════════════════════════════════════════════════════
//  «برنامه یک پیام بدهد» — کدام خبر تازه است
// ══════════════════════════════════════════════════════════════════════════
//
// خواستهٔ صریحِ صاحب ریپو: «وقتی که یک قرض‌دار اضافه برد یا کم مانده بود از
// حسابش، برنامه یک پیام بدهد — حتی اگر گوشی خاموش یا حتی اگر توی برنامه نبود
// هم پیام برود تا بفهمد.»
//
// ⚠️ قلبِ ماجرا همین تابع است. اگر اشتباه کند، یا هر بیست ثانیه زنگ می‌زند
// (و کارمند زنگ را بی‌صدا می‌کند) یا هیچ‌وقت زنگ نمی‌زند.

console.log('\n── خبرها ─────────────────────────────────────────────────');
{
  const A = { k: 'd7-stP-out', s: 'out', t: 'کریم — پطرولِ حسابش تمام شد' };
  const B = { k: 'd9-stD-low', s: 'low', t: 'سمیع — دیزلِ حسابش کم مانده' };

  const first = bot.freshAlerts([A, B], {});
  ok(first.fresh.length === 2, 'بارِ اول هر دو خبر تازه‌اند');

  const second = bot.freshAlerts([A, B], first.told);
  ok(second.fresh.length === 0, 'همان عکس دوباره ⇒ هیچ زنگی؛ وگرنه هر بیست ثانیه زنگ می‌زد');

  // حالِ بدتر ⇒ کلیدِ دیگر ⇒ خبرِ تازه
  const worse = bot.freshAlerts([{ k: 'd9-stD-out', s: 'out', t: 'سمیع — دیزلش تمام شد' }], second.told);
  ok(worse.fresh.length === 1, '«کم مانده ⇒ تمام شد» خبرِ تازهٔ خودش را می‌دهد');

  // تسویه، و بعد دوباره خراب شدن
  const cleared = bot.freshAlerts([], worse.told);
  ok(cleared.fresh.length === 0 && Object.keys(cleared.told).length === 0,
     'تسویه که شد، کلیدها فراموش می‌شوند');
  ok(bot.freshAlerts([A], cleared.told).fresh.length === 1,
     'حسابی که دوباره خراب شود، دوباره خبر می‌دهد — نه این‌که برای همیشه ساکت بماند');

  // فهرستِ خراب نباید چیزی را بخواباند
  ok(bot.freshAlerts(null, {}).fresh.length === 0, 'فهرستِ نبوده');
  ok(bot.freshAlerts([null, {}, { t: 'بی‌کلید' }], {}).fresh.length === 0,
     'خبرِ بی‌کلید شمرده نمی‌شود — وگرنه هر بار دوباره زنگ می‌زد');
}

// ══════════════════════════════════════════════════════════════════════
//  ابر — «دیگر از کسی آدرس نپرس»
// ══════════════════════════════════════════════════════════════════════
//
// خواستهٔ صریحِ صاحب ریپو: «برنامه پمپ یعقوبی کارمندان چرا ادرس اینترنتی
// میخان؟ این رو نخان.»
//
// چیزی که این‌جا قفل می‌شود: نشانیِ ابر دستی نمی‌شود، اپ اول ورود را
// نشان می‌دهد نه فرمِ نشانی، و راهِ کیو‌آر برداشته نشده.

console.log('\n── ابر و ورود ────────────────────────────────────────────');
{
  const { readFileSync } = await import("node:fs");
  const cloudSrc = readFileSync(new URL('../kar/cloud.js', import.meta.url), 'utf8');
  const htmlSrc  = readFileSync(new URL('../kar/index.html', import.meta.url), 'utf8');
  const appSrc   = readFileSync(new URL('../kar/app.js', import.meta.url), 'utf8');
  const swSrc    = readFileSync(new URL('../kar/sw.js', import.meta.url), 'utf8');

  // ── نشانیِ ابر قفل است ───────────────────────────────────────────
  //  اگر روزی از localStorage یا نوارِ نشانی خوانده شود، هر کسی می‌تواند
  //  اپِ کارمند را به سرورِ خودش ببرد و توکنِ گوگلِ او را برداردَ.
  ok(/var CLOUD = 'https:\/\/api\.vill3n\.top'/.test(cloudSrc),
     'نشانیِ ابر در خودِ کد نوشته شده است');
  const afterCloud = cloudSrc.slice(cloudSrc.indexOf('var CLOUD'));
  ok(!/CLOUD\s*=\s*(localStorage|params|p\.get|location)/.test(afterCloud),
     'نشانیِ ابر از تنظیمات یا نوارِ نشانی خوانده نمی‌شود');

  // ── اپ اول ورود را نشان می‌دهد، نه فرمِ نشانی ────────────────────
  ok(/id="signinPane"/.test(htmlSrc), 'صفحهٔ ورود هست');
  ok(/show\('signinPane'\)/.test(appSrc), 'اپ صفحهٔ ورود را باز می‌کند');
  ok(!/show\(cfg\.srv \? 'lockPane' : 'setupPane'\)/.test(appSrc),
     'دیگر با نبودِ نشانی، فرمِ نشانی باز نمی‌شود');

  // ── ولی راهِ کیو‌آر برداشته نشده ─────────────────────────────────
  //  ⚠️ این سنجه عمدی است. پمپی که برنامه‌اش به‌روز نشده و جایی که
  //  اینترنت نیست، فقط همین را دارند.
  ok(/id="setupPane"/.test(htmlSrc), 'راهِ کیو‌آر هنوز هست');
  ok(/btnManual/.test(appSrc) && /btnManual/.test(htmlSrc),
     'دکمهٔ «اینترنت ندارم» به راهِ کیو‌آر می‌برد');
  ok(/readUrl\s*\(\s*\)/.test(appSrc), 'نشانیِ داخلِ کیو‌آر هنوز خوانده می‌شود');

  // ── کارمند رمزِ فقط‌خواندنی می‌گیرد، نه رمزِ برنامه ──────────────
  ok(/home\.readKey/.test(appSrc), 'رمزِ فقط‌خواندنیِ سرور از ابر برداشته می‌شود');

  // ── سرویس‌ورکر فایلِ تازه را می‌شناسد ─────────────────────────────
  //  بی این، گوشیِ کارمند نسخهٔ قدیمی را نگه می‌دارد و cloud.js هرگز
  //  نمی‌رسد — اپ بی‌صدا همان فرمِ قدیمی را نشان می‌دهد.
  ok(/'\.\/cloud\.js'/.test(swSrc), 'cloud.js در فهرستِ سرویس‌ورکر هست');
  ok(!/pump-kar-v1'/.test(swSrc), 'شمارهٔ کش بالا رفته است');

  // ── اپ می‌گوید کدام برنامه است ───────────────────────────────────
  //
  // ⛔ باگی که این می‌بندد: سرور نشست را به بخشِ برنامه مهر می‌زند
  //    (`tokens.app`) و توکنِ یک بخش در بخشِ دیگر **پیدا نمی‌شود**.
  //    این اپ هیچ‌وقت نمی‌گفت کیست، پس نشستش «دکان» می‌شد و همان
  //    لحظه `GET /api/pump/me` می‌گفت «چنین نشستی نیست» — یعنی
  //    «صاحبِ پمپ هستم» تا امروز اصلاً کار نمی‌کرد.
  const calls = [];
  const realFetch = globalThis.fetch;
  globalThis.fetch = async (url, opt) => {
    calls.push({ url: String(url), opt });
    if (/\/auth\/google$/.test(url)) {
      return new Response(JSON.stringify({
        accessToken: 'tok-a', refreshToken: 'tok-r', accessExpiresAt: Date.now() + 3600e3,
        user: { name: 'کارمند', email: 'k@example.com' },
      }), { status: 200 });
    }
    if (/\/pump\/me$/.test(url)) {
      return new Response(JSON.stringify({
        home: { url: 'wss://home.example', readKey: 'read-k', station: 'ac-one' },
      }), { status: 200 });
    }
    return new Response('{}', { status: 404 });
  };
  try {
    const cloud = require(path.join(here, '..', 'kar', 'cloud.js'));
    await cloud.signInWithGoogle('id-token-from-google');
    const login = calls.find(c => /\/auth\/google$/.test(c.url));
    ok(!!login, 'ورود با گوگل به همان مسیرِ ابر می‌رود');
    ok(JSON.parse(login.opt.body).app === 'pump',
       'بدنهٔ ورود می‌گوید این برنامه پمپ است');
    ok((login.opt.headers || {})['X-App-Id'] === 'tohid-pump-app',
       'هدرِ X-App-Id هم روی همان درخواست هست');

    //  و روی **هر** درخواست، نه فقط ورود
    //  (مستقیم `call`، چون `myStation` نشستِ ذخیره‌شده می‌خواهد و
    //   این‌جا localStorage نیست)
    await cloud.call('GET', '/api/pump/me').catch(() => {});
    const home = calls.find(c => /\/pump\/me$/.test(c.url));
    ok(home && (home.opt.headers || {})['X-App-Id'] === 'tohid-pump-app',
       'هر درخواستِ دیگری هم شناسهٔ برنامه را می‌برد');
  } finally { globalThis.fetch = realFetch; }
}

// ══════════════════════════════════════════════════════════════════════
//  کدِ پمپ — «هر کسی که برنامه را نصب می‌کند باید آن کد را بزند»
// ══════════════════════════════════════════════════════════════════════
//
// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «برای هر پمپ یک کد باشد که در
// اندروید و آیفون بزند، حساب‌های همان پمپ را نشان بدهد و با پمپ‌های دیگر
// قاطی نشود — این را خیلی جدی بگیر.»

console.log('\n── کدِ پمپ و جداسازیِ پمپ‌ها ─────────────────────────────');
{
  const { readFileSync } = await import("node:fs");
  const cloudSrc = readFileSync(new URL('../kar/cloud.js', import.meta.url), 'utf8');
  const htmlSrc  = readFileSync(new URL('../kar/index.html', import.meta.url), 'utf8');
  const appSrc   = readFileSync(new URL('../kar/app.js', import.meta.url), 'utf8');
  const swSrc    = readFileSync(new URL('../kar/sw.js', import.meta.url), 'utf8');
  const apkYml   = readFileSync(new URL('../.github/workflows/build-kar-apk.yml', import.meta.url), 'utf8');
  const cloud    = require(path.join(here, '..', 'kar', 'cloud.js'));

  // ── صفحهٔ اول کدِ پمپ است ─────────────────────────────────────────
  ok(/id="codePane"/.test(htmlSrc) && /id="inCode"/.test(htmlSrc) && /id="btnJoin"/.test(htmlSrc),
     'صفحهٔ «کدِ پمپ» با کادر و دکمه هست');
  const bootPart = appSrc.slice(appSrc.indexOf('function boot()'));
  ok(/else \{[\s\S]{0,400}show\('codePane'\)/.test(bootPart),
     'بی هیچ تنظیمی، اولین صفحه کدِ پمپ است — نه گوگل، نه فرمِ نشانی');
  ok(/btnGoogleWay/.test(htmlSrc) && /btnGoogleWay/.test(appSrc), 'راهِ گوگل از همان صفحه در دسترس است');
  ok(htmlSrc.indexOf('id="codePane"') < htmlSrc.indexOf('id="signinPane"'), 'کدِ پمپ پیش از ورودِ گوگل می‌آید');

  // ── کد به ابر می‌رود و فقط نشانی و رمزِ خواندنِ همان پمپ برمی‌گردد ──
  ok(/\/api\/pump\/public\/join/.test(cloudSrc), 'کد از درِ عمومیِ join می‌رود');
  ok(!/\/api\/pump\/public\/join[^\n]*token/.test(cloudSrc), 'برای join هیچ توکنی فرستاده نمی‌شود');
  ok(cloud.normalizeCode(' k7pm-3xq2 ') === 'K7PM3XQ2', 'کدِ کوچک و خط‌تیره‌دار یکسان می‌شود');
  ok(cloud.formatCode('K7PM3XQ2') === 'K7PM-3XQ2', 'کد برای نمایش خط‌تیره می‌گیرد');

  // ── رفتار با سرورِ ساختگی ──────────────────────────────────────────
  const calls = [];
  const realFetch = globalThis.fetch;
  globalThis.fetch = async (url, opt) => {
    calls.push({ url: String(url), opt });
    const body = opt && opt.body ? JSON.parse(opt.body) : {};
    if (/\/join$/.test(url)) {
      if (body.code === 'K7PM3XQ2')
        return new Response(JSON.stringify({ ok: true, station: { code: 'ac-one', name: 'پمپِ یک' },
          home: { url: 'wss://a.example', readKey: 'read-a', station: 'ac-one' }, cloudLiveAt: 5 }), { status: 200 });
      return new Response(JSON.stringify({ error: { code: 'bad_access_code', message: 'این کد به هیچ پمپی نمی‌رسد' } }), { status: 404 });
    }
    if (/\/live\?code=K7PM3XQ2$/.test(url))
      return new Response(JSON.stringify({ ok: true, updatedAt: 7, live: { seq: 9, station: { name: 'پمپِ یک' } } }), { status: 200 });
    return new Response('{}', { status: 404 });
  };
  try {
    const st = await cloud.joinWithCode('k7pm-3xq2');
    ok(st && st.code === 'ac-one' && st.home.url === 'wss://a.example' && st.home.readKey === 'read-a',
       'کدِ درست ⇒ نشانی و رمزِ خواندنِ همان پمپ');
    ok(st.accessCode === 'K7PM3XQ2' && st.cloudLiveAt === 5, 'کد و تازگیِ عکسِ ابری همراهش می‌آید');
    ok(calls[0].url === 'https://api.vill3n.top/api/pump/public/join' && !(calls[0].opt.headers || {}).Authorization,
       'join به نشانیِ قفل‌شدهٔ ابر و بی‌توکن می‌رود');
    let failed = null;
    try { await cloud.joinWithCode('AAAA-AAAA'); } catch (e) { failed = e; }
    ok(failed && failed.status === 404, 'کدِ غلط ⇒ خطای ۴۰۴ با پیامِ سرور');
    let short = null;
    try { await cloud.joinWithCode('ABC'); } catch (e) { short = e; }
    ok(short && short.code === 'bad_access_code' && calls.length === 2, 'کدِ کوتاه اصلاً به سرور نمی‌رود');
    const lv = await cloud.cloudLive('k7pm-3xq2');
    ok(lv && lv.live.seq === 9 && lv.updatedAt === 7, 'عکسِ ابری با همان کد می‌آید');
  } finally { globalThis.fetch = realFetch; }

  // ── جداسازی: هر پمپ کلیدِ خودش ──────────────────────────────────────
  ok(/function stnKey\(suffix\)/.test(appSrc) && /stnKey\('snap'\)/.test(appSrc) && /stnKey\('told'\)/.test(appSrc),
     'عکس و خبرهای گفته‌شده زیرِ کلیدِ همان پمپ می‌نشینند');
  ok(!/localStorage\.setItem\(KEY \+ '\.snap'/.test(appSrc), 'کلیدِ مشترکِ قدیمیِ عکس دیگر نوشته نمی‌شود');
  ok(/function switchStation\(stn\)[\s\S]{0,600}removeItem\(stnKey\('snap'\)\)/.test(appSrc),
     'رفتن به پمپِ دیگر عکسِ پمپِ قبلی را پاک می‌کند');
  ok(/function forgetAll\(/.test(appSrc) && /btnForget[\s\S]{0,300}forgetAll\(/.test(appSrc),
     '«پمپِ دیگر» همه‌چیز را پاک می‌کند و به صفحهٔ کد برمی‌گردد');
  ok(/function acceptSnapshot\(/.test(appSrc) && /\(v\.seq \|\| 0\) < data\.seq/.test(appSrc),
     'عکسِ کهنه (چه از ابر چه از خانه) جای تازه را نمی‌گیرد');
  ok(/function cloudFallback\(/.test(appSrc) && /retry >= 2\) cloudFallback\(\)/.test(appSrc),
     'اگر سرورِ خانگی جواب نداد، عکسِ ابریِ همان پمپ می‌آید');
  ok(/status === 404[\s\S]{0,120}forgetAll\(/.test(appSrc), 'کدِ عوض‌شده ⇒ این گوشی بیرون می‌رود');

  // ── فایلِ نصبِ اندروید cloud.js را دارد ─────────────────────────────
  ok(/cp kar\/index\.html kar\/app\.js kar\/cloud\.js kar\/update\.js kar\/manifest\.json/.test(apkYml),
     'cloud.js داخلِ فایلِ نصبِ اندروید می‌رود — بی آن، کدِ پمپ در اپِ نصبی کار نمی‌کرد');
  ok(!/pump-kar-v3'/.test(swSrc), 'شمارهٔ کشِ سرویس‌ورکر بالا رفته است');
}

// ══════════════════════════════════════════════════════════════════════
//  دو در: «حساب‌های پمپ» و «کارمندان»
// ══════════════════════════════════════════════════════════════════════
console.log('\n── دو در ─────────────────────────────────────────────────');
{
  const { readFileSync } = await import("node:fs");
  const htmlSrc = readFileSync(new URL('../kar/index.html', import.meta.url), 'utf8');
  const appSrc  = readFileSync(new URL('../kar/app.js', import.meta.url), 'utf8');
  ok(/id="paneHome"/.test(htmlSrc) && /class="door owner"/.test(htmlSrc) && /class="door staff"/.test(htmlSrc),
     'صفحهٔ خانه دو در دارد: حساب‌های پمپ و کارمندان');
  ok(/id="paneDash"/.test(htmlSrc) && /id="bannerBox"/.test(htmlSrc), 'داشبوردِ حساب‌ها با چهار عددِ نوار هست');
  ok(/id="paneStaff"/.test(htmlSrc) && /id="staffList"/.test(htmlSrc), 'درِ کارمندان چراغِ هر قرض‌دار را دارد');
  ok(/var NAVS = \{[\s\S]*owner:[\s\S]*staff:/.test(appSrc), 'هر در نوارِ خودش را دارد');
  ok(/stnKey\('mode'\)/.test(appSrc), 'درِ انتخاب‌شده زیرِ کلیدِ همان پمپ می‌ماند');
  ok(/mode = '';\s*\}/.test(appSrc.slice(appSrc.indexOf('function switchStation'))), 'رفتن به پمپِ دیگر در را فراموش می‌کند');
  ok(/PumpAndroid\.boot\('ready'\)/.test(appSrc), 'پردهٔ لودینگِ اندروید با «ready» برداشته می‌شود');
  // ربات بخش‌های تازه را می‌شناسد
  for (const id of ['parcha', 'waraq', 'debtrasid', 'parcharasid', 'attendance'])
    ok(new RegExp(id + ':\\s*\\[').test(appSrc), 'ربات واژه‌های بخشِ ' + id + ' را دارد');
  ok(!/pump-kar-v4'/.test(readFileSync(new URL('../kar/sw.js', import.meta.url), 'utf8')), 'شمارهٔ کش بالا رفته');
}

// ══════════════════════════════════════════════════════════════════════
//  به‌روزرسانیِ خودکار از گیت‌هاب — برنامه و سایت
// ══════════════════════════════════════════════════════════════════════
console.log('\n── به‌روزرسانیِ خودکار ───────────────────────────────────');
{
  const { readFileSync } = await import("node:fs");
  const updSrc  = readFileSync(new URL('../kar/update.js', import.meta.url), 'utf8');
  const htmlSrc = readFileSync(new URL('../kar/index.html', import.meta.url), 'utf8');
  const swSrc   = readFileSync(new URL('../kar/sw.js', import.meta.url), 'utf8');
  const apkYml  = readFileSync(new URL('../.github/workflows/build-kar-apk.yml', import.meta.url), 'utf8');
  const pagesYml= readFileSync(new URL('../.github/workflows/deploy-pages.yml', import.meta.url), 'utf8');
  const javaSrc = readFileSync(new URL('../android/app/src/main/java/top/yaqobipump/app/MainActivity.java', import.meta.url), 'utf8');
  const upd = require(path.join(here, '..', 'kar', 'update.js'));

  ok(/var SITE = 'https:\/\/yaqobipump\.top\/kar\/'/.test(updSrc), 'نشانیِ سایت در خودِ کد قفل است');
  ok(!/SITE\s*=\s*(localStorage|location|p\.get)/.test(updSrc.slice(updSrc.indexOf('var SITE'))), 'نشانی از تنظیمات خوانده نمی‌شود');
  ok(/<script src="\.\/update\.js">/.test(htmlSrc) && /id="updBar"/.test(htmlSrc), 'update.js و نوارِ نسخهٔ تازه در صفحه‌اند');
  ok(/'\.\/update\.js'/.test(swSrc) && !/pump-kar-v5'/.test(swSrc), 'update.js در سرویس‌ورکر و شمارهٔ کش بالا رفته');
  ok(/version\.json\(\\\?\|\$\)/.test(swSrc) || /version\\\.json/.test(swSrc), 'version.json هیچ‌وقت از کش نمی‌آید');
  ok(/kar\/update\.js/.test(apkYml) && /assets\/www\/version\.json/.test(apkYml) && /git rev-list --count HEAD/.test(apkYml),
     'فایلِ نصب update.js و version.json (شمارِ کامیت‌ها) را دارد');
  ok(/kar\/version\.json/.test(pagesYml) && /git rev-list --count HEAD/.test(pagesYml) && /kar-latest', 'version\.txt'/.test(pagesYml),
     'سایت kar/version.json را با همان شماره و نسخهٔ فایلِ نصب می‌سازد');
  ok(/updateOk\(\)/.test(updSrc), 'سالم بالا آمدن به پوسته گفته می‌شود (وگرنه آپدیت دور می‌ریخت)');
  for (const m of ['updateFileBegin', 'updateFileCommit', 'updateFinish', 'openUrl', 'multiComplete'])
    ok(javaSrc.includes(m + '('), 'پوسته ' + m + ' دارد');
  ok(javaSrc.includes('www/version.json'), 'پوسته نسخهٔ همراهِ نصب را از version.json می‌خواند');
  ok(upd.verCmp('1.0.12', '1.0.8') === 1 && upd.verCmp('1.0.8', '1.0.8') === 0 && upd.num('1234') === 1234, 'مقایسهٔ نسخه درست است');
  ok(JSON.stringify(upd.FILES) === JSON.stringify(['index.html', 'app.js', 'cloud.js', 'update.js', 'manifest.json']),
     'هر پنج فایلِ اپ با هم به‌روز می‌شوند');
}

console.log(bad ? '\n' + bad + ' آزمون شکست خورد' : '\nهمه درست');
process.exit(bad ? 1 : 0);
