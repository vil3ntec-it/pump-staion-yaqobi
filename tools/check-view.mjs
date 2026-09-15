// ══ آزمونِ صفحهٔ «حسابِ من» (view/) ══════════════════════════════════════════
//
// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۴): «بعضی جدول‌ها یا کادرهای جدول‌ها نیست؛ یارو
// نمی‌تواند ببیند که واحدِ پول چقدر قرض‌دار است یا تیل چقدر… شرکت‌ها هم
// نمی‌دانند دیزل چقدر از من می‌خواهند یا پطرول چقدر… آرشیوها یادم نرود، و ماه
// و سال… و فقط خواندنی باشد ولی بتواند با دکمه‌ها عوض کند.»
//
// این فایل همان صفحه را واقعاً اجرا می‌کند — با یک DOMِ کوچکِ ساختگی — و
// می‌سنجد که هر کدام از آن خواسته‌ها روی صفحه هست. اگر کسی روزی یکی از
// جدول‌ها را بردارد، این‌جا قرمز می‌شود.
//
// ⚠️ «کد را بخوان و دنبالِ رشته بگرد» کافی نیست: باگِ واقعیِ قبلی این بود که
// جدول در کد **بود** ولی داده‌اش هیچ‌وقت به صفحه نمی‌رسید. پس صفحه اجرا
// می‌شود و به خروجیِ واقعی نگاه می‌کنیم.
//
//   node tools/check-view.mjs

import { readFileSync } from 'node:fs';
import { deflateRawSync } from 'node:zlib';
import vm from 'node:vm';

let bad = 0;
const ok = (c, m) => { if (!c) { bad++; console.error('  ✗ ' + m); } else console.log('  ✓ ' + m); };

// ── DOMِ کوچک ────────────────────────────────────────────────────────────────
// فقط همان چند چیزی که صفحه به کار می‌برد. هر چیزِ بیشتری یعنی آزمونی که
// خودش را می‌سنجد نه صفحه را.
function makePage(hash, fetchImpl) {
  const app = {
    innerHTML: '<div class="card"></div>',
    _click: null,
    addEventListener(type, fn) { if (type === 'click') this._click = fn; },
  };
  const ctx = {
    document: { getElementById: (id) => (id === 'app' ? app : null), title: '' },
    location: { hash },
    window: { print() { ctx.window._printed = true; }, _printed: false },
    atob, Blob, Response, DecompressionStream, console,
    setTimeout, Promise, Uint8Array,
  };
  // ‎fetch‎ فقط وقتی هست که آزمون بدهد — صفحهٔ بی‌‎fetch‎ باید بی‌صدا ایستا بماند.
  if (fetchImpl) ctx.fetch = fetchImpl;
  ctx.window.document = ctx.document;
  vm.createContext(ctx);

  const html = readFileSync(new URL('../view/index.html', import.meta.url), 'utf8');
  const src = html.slice(html.indexOf('<script>') + 8, html.lastIndexOf('</script>'));
  vm.runInContext(src, ctx);

  // دکمه‌ای که همان ‎data-act/data-v‎ را دارد — عینِ چیزی که مرورگر می‌دهد.
  const click = (act, v) => {
    const el = { getAttribute: (k) => (k === 'data-act' ? act : k === 'data-v' ? v : null) };
    app._click({ target: { closest: () => el } });
  };
  return { app, ctx, click, get html() { return app.innerHTML; } };
}

const encode = (obj) =>
  deflateRawSync(Buffer.from(JSON.stringify(obj), 'utf8'))
    .toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');

const settle = () => new Promise((r) => setTimeout(r, 60));

// ── دادهٔ نمونه: همان شکلی که ‎AcctSnapshots‎ می‌سازد ─────────────────────────
const fuelBook = {
  k: 'واحد تیل', u: 'لیتر', dc: 0, fc: 3,
  s: [['جمله بردگی', '1,500'], ['جمله رسید', '500'], ['فیصدی ما', '40'], ['الباقی', '1,040']],
  fh: ['تیل', 'بردگی', 'رسید', 'فیصدی', 'الباقی'],
  f: [['پطرول (٪10)', '1,000', '400', '40', '640'], ['دیزل', '500', '100', '0', '400']],
  h: ['تاریخ', 'نام', 'حواله', 'تیل', 'مقدار تیل', 'فی', 'بردگی', 'رسید تیل'],
  r: [
    ['1404/12/03', 'الف', '', 'پطرول', '600', '', '600', '200'],
    ['1405/05/03', 'ب', '', 'پطرول', '400', '', '400', '200'],
    ['1405/06/11', 'ج', '', 'دیزل', '500', '', '500', '100'],
  ],
  g: [['1404/12', '600', '200', '400'], ['1405/05', '400', '200', '200'], ['1405/06', '500', '100', '400']],
};
const moneyBook = {
  k: 'واحد پول', u: 'افغانی', dc: 0, fc: 3,
  s: [['جمله بردگی', '9,000'], ['جمله رسید', '2,000'], ['فیصدی ما', '0'], ['الباقی', '7,000']],
  fh: ['تیل', 'بردگی', 'رسید', 'فیصدی', 'الباقی'],
  f: [['پطرول', '9,000', '2,000', '0', '7,000'], ['دیزل', '0', '0', '0', '0']],
  h: ['تاریخ', 'نام', 'حواله', 'تیل', 'مقدار (افغانی)', 'فی', 'بردگی', 'رسید'],
  r: [['1405/06/12', 'د', '', 'پطرول', '', '', '9,000', '2,000']],
  g: [['1405/06', '9,000', '2,000', '7,000']],
};
const snapV2 = {
  t: 'قرض‌دار — واحد تیل', n: 'محمد هارون', d: '1405/06/24',
  b: [fuelBook, moneyBook],
  s: fuelBook.s, h: fuelBook.h, r: fuelBook.r,
};

// ══════════════════════════════════════════════════════════════════════════
console.log('\n۱) هر دو دفتر — «واحد پول چقدر، تیل چقدر»');
const p = makePage('#d=' + encode(snapV2));
await settle();

ok(p.html.includes('محمد هارون'), 'نامِ حساب روی صفحه است');
ok(p.html.includes('واحد تیل') && p.html.includes('واحد پول'), 'هر دو دفتر تب دارند');
ok(p.html.includes('1,040'), 'الباقیِ دفترِ تیل دیده می‌شود');
ok(p.html.includes('لیتر'), 'واحدِ دفترِ باز نوشته شده');

console.log('\n۲) تفکیکِ پطرول و دیزل — «دیزل چقدر یا پطرول چقدر»');
ok(p.html.includes('پطرول (٪10)') && p.html.includes('640'), 'ردیفِ پطرول با عددهایش');
ok(p.html.includes('فیصدی'), 'ستونِ فیصدی در جدولِ تفکیک هست');

console.log('\n۳) آرشیوِ ماه و سال');
ok(p.html.includes('آرشیو'), 'کادرِ آرشیو هست');
ok(p.html.includes('سنبله 1405') && p.html.includes('حوت 1404'), 'ماه‌ها با نامِ شمسی');
ok(p.html.includes('سال‌به‌سال') && p.html.includes('>1404<'), 'جدولِ سال‌به‌سال با سالِ ۱۴۰۴');
ok(p.html.includes('جمعِ کل'), 'جمعِ کلِ آرشیو');

console.log('\n۴) فقط‌خواندنی، ولی با دکمه');
ok(p.html.includes('فقط خواندنی'), 'روی جدول نوشته «فقط خواندنی»');
ok(!/<input|<textarea|contenteditable/i.test(p.html), 'هیچ کادرِ تایپ‌شدنی‌ای روی صفحه نیست');

console.log('\n۵) دکمهٔ تیل واقعاً فیلتر می‌کند');
p.click('fuel', 'دیزل');
// ⚠️ دنبالِ خودِ واژهٔ «پطرول» نگرد — دکمهٔ فیلتر هم همان را رویش نوشته.
// محکِ درست، تاریخِ یک ردیفِ پطرول است.
ok(!p.html.includes('1404/12/03'), 'با فیلترِ دیزل، ردیفِ پطرول رفت');
ok(p.html.includes('1 ردیف از 3'), 'شمارِ ردیف‌های دیده‌شده نوشته می‌شود');
p.click('fuel', '');
ok(p.html.includes('1404/12/03'), 'با «هر دو» برگشت');

console.log('\n۶) دکمهٔ ماه واقعاً فیلتر می‌کند');
p.click('month', '1405/05');
ok(p.html.includes('1405/05/03') && !p.html.includes('1405/06/11'), 'فقط ردیفِ همان ماه ماند');

console.log('\n۷) عوض کردنِ دفتر — و صفر شدنِ فیلترها');
p.click('book', '1');
ok(p.html.includes('7,000') && p.html.includes('افغانی'), 'دفترِ پول باز شد');
ok(p.html.includes('1405/06/12'), 'ردیفِ دفترِ پول دیده می‌شود — یعنی فیلترِ ماه صفر شد');
ok(!p.html.includes('1,040'), 'عددِ دفترِ تیل دیگر روی صفحه نیست');

console.log('\n۸) کیو‌آرهای چاپ‌شدهٔ قدیمی هنوز باز می‌شوند');
const old = makePage('#d=' + encode({
  t: 'قرض‌دار — واحد تیل', n: 'حسابِ قدیمی', u: 'لیتر', d: '1405/01/01',
  s: [['الباقی', '250']],
  h: ['تاریخ', 'نام'], r: [['1405/01/01', 'کهنه']], m: 'فقط ۵ ردیفِ آخر',
}));
await settle();
ok(old.html.includes('حسابِ قدیمی'), 'نام');
ok(old.html.includes('250') && old.html.includes('لیتر'), 'کادرِ خلاصه با واحدش');
ok(old.html.includes('کهنه'), 'جدولِ قدیمی');
ok(old.html.includes('فقط ۵ ردیفِ آخر'), 'پیامِ پایینِ جدول');
ok(!old.html.includes('آرشیو'), 'کدِ قدیم آرشیو ندارد و صفحه هم چیزی از خودش نمی‌سازد');

console.log('\n۹) نشانیِ بی‌داده');
const empty = makePage('');
await settle();
ok(empty.html.includes('باز نشد'), 'پیامِ روشن، نه صفحهٔ سفید');

console.log('\n۱۰) کیو‌آرِ زنده — «هر دقیقه بتونه چک کنه و بفهمه»');
// همان چهار پارامتر که ‎AcctLive.Fragment‎ پیش از ‎d=‎ می‌گذارد.
const liveHash = '#s=pump1&a=d7&k=abc123&t=1000&d=' + encode(snapV2);
const okJson = (body, status = 200) => ({
  ok: status < 300, status, json: async () => body,
});
const calls = [];
const newer = { ...snapV2, n: 'محمد هارون (تازه)', d: '1405/06/25' };
const fresh = makePage(liveHash, async (url) => { calls.push(url); return okJson({ at: 2000, d: newer }); });
await settle();
ok(fresh.html.includes('محمد هارون (تازه)'), 'عکسِ تازه‌ترِ ابر جای دادهٔ داخلِ کد نشست');
ok(fresh.html.includes('زنده — آخرین بررسی'), 'نشانِ سبزِ «زنده» با ساعتِ بررسی');
ok(calls.length === 1 && calls[0].startsWith('https://api.vill3n.top/api/pump/public/pump1/acct/d7?k=abc123'),
   'از نشانیِ قفل‌شدهٔ ابر، با کدِ پمپ و شناسه و رمزِ همین حساب می‌پرسد: ' + calls[0]);
fresh.click('refresh');
await settle();
ok(calls.length === 2, 'دکمهٔ «به‌روز کن» دوباره می‌پرسد');

const older = makePage(liveHash, async () => okJson({ at: 500, d: newer }));
await settle();
ok(older.html.includes('محمد هارون') && !older.html.includes('(تازه)'),
   'عکسِ کهنه‌تر از خودِ کد (t) نادیده می‌ماند');

const off = makePage(liveHash, async () => { throw new Error('network'); });
await settle();
ok(off.html.includes('محمد هارون'), 'بی‌اینترنت، دادهٔ داخلِ کد همچنان دیده می‌شود');
ok(off.html.includes('ابر در دسترس نیست'), '…و صفحه می‌گوید که نتوانست بپرسد');

const none = makePage(liveHash, async () => okJson({ error: 'not_found' }, 404));
await settle();
ok(none.html.includes('هنوز نسخهٔ تازه‌تری نیامده'), '۴۰۴ یعنی هنوز منتشر نشده، نه خطا');

let staticCalls = 0;
const still = makePage('#d=' + encode(snapV2), async () => { staticCalls++; return okJson({}); });
await settle();
ok(staticCalls === 0 && !still.html.includes('به‌روز کن'), 'کیو‌آرِ ایستا (بی s/a/k) هیچ درخواستی نمی‌زند');

const tailFirst = makePage('#d=' + encode(snapV2) + '&s=pump1&a=d7&k=abc123&t=1',
                           async () => okJson({ at: 2, d: newer }));
await settle();
ok(tailFirst.html.includes('(تازه)'), 'پارامترها بعد از d= هم خوانده می‌شوند');

console.log(bad === 0 ? '\n✅ صفحهٔ view/ سالم است\n' : `\n❌ ${bad} ایراد\n`);
process.exit(bad === 0 ? 0 : 1);
