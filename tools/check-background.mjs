// ---------------------------------------------------------------------------
//  نگهبانِ «لایهٔ رنگیِ پس‌زمینه»
//
//      node tools/check-background.mjs
//
//  این باگ چند بار برگشته بود: نوارها و مستطیل‌های رنگیِ ناجور (صورتی/تیره) که
//  بالای صفحه، کنارِ صفحه یا زیرِ نوشتهٔ پایین پیدا می‌شدند و «با اسکرول می‌آمدند
//  و می‌رفتند». هر بار فقط همان یک جا وصله می‌شد، پس از جای دیگری برمی‌گشت.
//
//  ریشه‌اش یکی بود: رنگِ «بومِ صفحه» (پس‌زمینهٔ <html>) با رنگی که خودِ صفحه
//  می‌کشد فرق داشت. مثلاً در پوستهٔ «یاقوت اجرایی» بوم #fbdfe4 صورتی بود و صفحه
//  تقریباً سفید؛ هر جا بوم از پشتِ محتوا دیده می‌شد، یک مستطیلِ صورتی به چشم
//  می‌آمد. حالا بوم رنگِ جدا ندارد و پس‌زمینهٔ <body> طبق استانداردِ CSS به آن
//  تسرّی پیدا می‌کند، پس حتی اگر بیرون بزند چیزی دیده نمی‌شود.
//
//  این فایل همان قاعده‌ها را نگه می‌دارد تا کسی (یا سیزنِ بعدیِ Claude) دوباره
//  برنگرداندشان. اگر عمداً چیزی را عوض می‌کنید، اول این‌جا را بخوانید.
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import path from 'node:path';

const root = path.join(import.meta.dirname, '..');
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');

let failed = 0;
const fail = (title, detail) => {
  failed++;
  console.log(`  ❌ ${title}`);
  if (detail) console.log(`     ${detail}`);
};
const ok = (title) => console.log(`  ✅ ${title}`);

/** شمارهٔ خطِ اولین تطبیق — تا پیدا کردنش آسان باشد */
const lineOf = (re) => {
  const m = re.exec(html);
  if (!m) return null;
  return html.slice(0, m.index).split('\n').length;
};

console.log('\n── بومِ صفحه رنگِ جدا نداشته باشد ──');

// ۱) هیچ قاعدهٔ CSSای روی html رنگِ پس‌زمینه نگذارد (جز transparent)
{
  const bad = [];
  // «\n یا }» و نه فقط «}» — قاعده‌ها معمولاً بعد از یک کامنت می‌آیند
  const re = /(^|[\n};])\s*html\s*\{([^}]*)\}/g;
  let m;
  while ((m = re.exec(html))) {
    const body = m[2];
    const bg = /background(-color)?\s*:\s*([^;]+)/i.exec(body);
    if (!bg) continue;
    const value = bg[2].replace(/!important/i, '').trim();
    if (!/^(transparent|none)$/i.test(value)) {
      bad.push(`خط ${html.slice(0, m.index).split('\n').length}: background:${value}`);
    }
  }
  if (bad.length) {
    fail('قاعدهٔ html نباید رنگِ پس‌زمینه داشته باشد', bad.join('\n     ') +
      '\n     ← بوم باید شفاف بماند تا پس‌زمینهٔ <body> به آن تسرّی پیدا کند.');
  } else {
    ok('هیچ قاعدهٔ CSS روی <html> رنگ نمی‌گذارد');
  }
}

// ۲) هیچ جای جاوااسکریپت روی documentElement پس‌زمینه ننویسد
{
  const re = /documentElement\s*\.\s*style\s*\.\s*(background\b|setProperty\s*\(\s*['"]background)/g;
  const line = lineOf(re);
  if (line) {
    fail('جاوااسکریپت روی <html> پس‌زمینه می‌نویسد', `خط ${line}` +
      '\n     ← قبلاً سه جا این کار را می‌کردند و چون فقط یکی !important داشت،' +
      '\n       بوم روی رنگِ پوستهٔ قبلی گیر می‌کرد. فقط removeProperty مجاز است.');
  } else {
    ok('جاوااسکریپت روی <html> پس‌زمینه نمی‌نویسد');
  }
}

// ۳) <body> باید پس‌زمینه داشته باشد وگرنه چیزی برای تسرّی نیست
{
  // قاعدهٔ پایهٔ body (نه body.light-mode و نه بقیه) باید رنگ داشته باشد
  const re = /(^|[\n}])\s*body\s*\{[^}]*background\s*:\s*var\(--dark\)/;
  if (re.test(html)) ok('<body> پس‌زمینهٔ واقعی دارد (var(--dark))');
  else fail('<body> پس‌زمینه ندارد', 'بدونِ آن بوم خالی می‌ماند و رنگِ مرورگر دیده می‌شود.');
}

console.log('\n── لایه‌های تزئینی هنگام اسکرول بازرسم نشوند ──');

// ۴) لایه‌های تمام‌صفحه با dvh اندازه نگیرند (با نوارِ آدرس کم‌وزیاد می‌شود)
{
  // بدونِ \b — در «100dvh» بینِ 0 و d مرزِ کلمه نیست
  const re = /#fxLayer(Top)?\s*\{[^}]*dvh/;
  if (re.test(html)) {
    fail('#fxLayer/#fxLayerTop از dvh استفاده می‌کند',
      'dvh با پنهان/آشکار شدنِ نوارِ آدرس عوض می‌شود و این لایه‌ها را وسطِ اسکرول' +
      '\n     بازرسم می‌کند (همان «پرپرک»). از lvh استفاده کنید.');
  } else {
    ok('لایه‌های تزئینی با lvh اندازه می‌گیرند، نه dvh');
  }
}

// ۵) هم‌گام‌سازی با visualViewport برنگردد
{
  if (/pinFxToVisualViewport\s*\(/.test(html)) {
    fail('pinFxToVisualViewport برگشته است',
      'این تابع قد/بالا/چپِ لایه‌های تزئینی را در حینِ اسکرول بازنویسی می‌کرد.' +
      '\n     با lvh دیگر لازم نیست.');
  } else {
    ok('هیچ کدی لایه‌های تزئینی را با اسکرول تغییر اندازه نمی‌دهد');
  }
}

// ۶) backdrop-filterِ بی‌اثر روی #siteBg برنگردد
{
  const re = /#siteBg\s*\{[^}]*backdrop-filter\s*:\s*(?!none)/;
  if (re.test(html)) {
    fail('#siteBg دوباره backdrop-filter دارد',
      '#siteBg با z-index:-1 پشتِ همه‌چیز است و چیزی زیرش نیست، پس آن blur هیچ' +
      '\n     چیزی را تار نمی‌کند — فقط یک سطحِ تمام‌صفحه به مرورگر تحمیل می‌کند.');
  } else {
    ok('#siteBg دیگر backdrop-filterِ بی‌اثر ندارد');
  }
}

console.log('\n── شبکهٔ شش‌ضلعی برنگردد ──');

// ۷) خواستهٔ صریحِ صاحب ریپو: نوارهای شش‌ضلعی نباشد
{
  if (/fx-grid/.test(html)) {
    fail('شبکهٔ شش‌ضلعی (fx-grid) برگشته است',
      'صاحب ریپو صریحاً گفته این نوارها را نمی‌خواهد. پنهان کردنش با کلاس کافی' +
      '\n     نیست — با یک تنظیمِ ذخیره‌شدهٔ کهنه دوباره پیدا می‌شود.');
  } else {
    ok('هیچ ردی از شبکهٔ شش‌ضلعی نمانده');
  }
}

console.log('\n── دکمه‌های سربرگِ مودالِ شخص ──');

// ۸) قاعدهٔ !importantِ سربرگ نباید دکمه‌های پنهان را هم نشان بدهد
{
  const re = /#personModal\s+\.modal-head\s+button\s*\{[^}]*display\s*:[^;}]*!important/;
  if (re.test(html)) {
    fail('قاعدهٔ سربرگ دکمه‌های پنهان را هم نشان می‌دهد',
      '«#personModal .modal-head button» با display:...!important روی «هر» دکمهٔ' +
      '\n     سربرگ می‌نشیند، پس دکمه‌ای که باید پنهان باشد (مثل «حذف حساب» که فقط' +
      '\n     داخلِ حساب فرعی معنی دارد) با هیچ راهی پنهان نمی‌شود.' +
      '\n     ← باید «button:not([hidden])» باشد.');
  } else {
    ok('قاعدهٔ سربرگ دکمه‌های [hidden] را استثنا می‌کند');
  }
}

// ۹) و دکمهٔ hidden صریحاً پنهان بماند
{
  if (/#personModal\s+\.modal-head\s+button\[hidden\]\s*\{[^}]*display\s*:\s*none\s*!important/.test(html)) {
    ok('دکمهٔ [hidden] در سربرگ صریحاً پنهان است');
  } else {
    fail('گاردِ #personModal .modal-head button[hidden] نیست',
      'بدونِ آن، قاعده‌های دیگر (مثل .add-row-btn{display:flex}) دوباره نشانش می‌دهند.');
  }
}

console.log('\n════════════════════════════════════');
if (failed) {
  console.log(`  ❌ ${failed} مورد — باگِ «لایهٔ رنگی» می‌تواند برگردد`);
  console.log('════════════════════════════════════\n');
  process.exit(1);
}
console.log('  ✅ همه‌چیز سرِ جایش است');
console.log('════════════════════════════════════\n');
