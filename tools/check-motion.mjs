// ---------------------------------------------------------------------------
//  نگهبانِ حرکت‌ها
//
//      node tools/check-motion.mjs
//
//  صاحب ریپو صریح گفت: «منطق‌ها و فرمول‌ها حتی ذره‌ای دست نخورد» و «انیمیشنِ
//  بخشِ مخزن دست نخورد». پالایشِ حرکت‌ها در <style id="motion-refine"> انجام شد
//  و همان بلوک ممکن است در سیزن‌های بعدی وسوسه‌کننده باشد که «فقط یک خط» به
//  مخزن هم اضافه شود. این فایل جلویش را می‌گیرد.
//
//  چهار قاعده:
//    ۱) قاعده‌های حرکتِ مخزن باید مو‌به‌مو سرِ جایشان باشند.
//    ۲) بلوکِ motion-refine حق ندارد اسمِ مخزن را بیاورد.
//    ۳) بلوکِ motion-refine حق ندارد ویژگیِ چیدمانی عوض کند (فقط حرکت).
//    ۴) بلوکِ motion-refine حق ندارد transition:all را برگرداند.
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

console.log('\nنگهبانِ حرکت‌ها\n');

// ── ۱) حرکتِ مخزن دست‌نخورده ───────────────────────────────────────────────
const tankMotion = [
  ['موجِ سطحِ مخزن (w1)', '.tank-wave.w1{ animation:tankWave 4.5s linear infinite; opacity:.85; }'],
  ['موجِ سطحِ مخزن (w2)', '.tank-wave.w2{ animation:tankWave 7s linear infinite reverse; opacity:.5; top:-9px; }'],
  ['کلیدفریمِ موج', '@keyframes tankWave{ from{ transform:translateX(0); } to{ transform:translateX(-50%); } }'],
  ['تپشِ نشانِ «کم مانده»', 'animation:tankPulse 1.6s ease-in-out infinite;'],
  ['کلیدفریمِ تپش', '@keyframes tankPulse{ 50%{ box-shadow:0 0 14px rgba(229,62,62,.5); } }'],
  ['جریانِ مایع', '@keyframes tankFlow{ 0%,100%{ background-position:120% 0; } 50%{ background-position:-20% 0; } }'],
  ['هشدارِ مخزن در کیفیتِ استاندارد', 'animation:gfxTankDangerStd 1.6s ease-in-out infinite;'],
];
for (const [title, needle] of tankMotion) {
  if (html.includes(needle)) ok(`${title} — دست‌نخورده`);
  else fail(`${title} عوض شده`, `این متن دیگر در index.html نیست:\n     ${needle}`);
}

// ── بلوکِ motion-refine ────────────────────────────────────────────────────
const m = html.match(/<style id="motion-refine">([\s\S]*?)<\/style>/);
if (!m) {
  fail('بلوکِ <style id="motion-refine"> پیدا نشد', 'اگر عمداً برداشته شده، این نگهبان را هم بردارید.');
} else {
  const css = m[1];
  // متنِ توضیحاتِ بالای بلوک عمداً از «مخزن» حرف می‌زند؛ فقط خودِ قاعده‌ها بررسی می‌شوند
  const rules = css.replace(/\/\*[\s\S]*?\*\//g, '');

  // ۲) هیچ گزینشگری به مخزن نخورد
  if (/tank/i.test(rules)) fail('motion-refine به عناصرِ مخزن دست زده', 'هر گزینشگرِ .tank-* باید از این بلوک بیرون بماند.');
  else ok('motion-refine هیچ گزینشگرِ مخزنی ندارد');

  // ۳) فقط حرکت — نه چیدمان
  const layoutProps = /(^|[;{\s])(width|height|padding|margin|font-size|display|position|top|left|right|bottom|flex|grid)\s*:/m;
  const hit = rules.match(layoutProps);
  if (hit) fail('motion-refine ویژگیِ چیدمانی گذاشته', `«${hit[2]}» این‌جا جایش نیست — این بلوک فقط حرکت است.`);
  else ok('motion-refine فقط حرکت را دست می‌زند');

  // ۴) transition:all برنگردد
  if (/transition\s*:\s*all/.test(rules)) fail('transition:all برگشته', 'دامنهٔ گذر باید صریح باشد، وگرنه چیدمان هم انیمیت می‌شود.');
  else ok('transition:all در motion-refine نیست');
}

console.log('');
if (failed) {
  console.log(`${failed} قاعده شکست. لطفاً پیام‌های بالا را بخوانید — وصله نزنید.\n`);
  process.exit(1);
}
console.log('همه‌چیز درست است.\n');
