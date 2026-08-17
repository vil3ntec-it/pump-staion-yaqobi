#!/usr/bin/env node
/* ══ کوچک‌کردنِ فایل‌ها فقط برای انتشار — نه در ریپو ═══════════════════════════
   گزارشِ صاحب ریپو: «سایت آن‌قدر لگ می‌زند که گوشی‌ام دیگر بازش نمی‌کند.»

   index.html نزدیک ۴ مگابایت است و ۲٫۲ مگابایتش جاوااسکریپت. مرورگر باید هر بار
   کلش را بخواند و تجزیه کند؛ روی گوشی همین «باز نشدن» است. اندازه‌گیری‌شده
   (کروم، پروفایلرِ واقعی):
     • ۲۸٪ِ آن جاوااسکریپت فقط کامنت و تورفتگی است.
     • برداشتنشان: حجمِ فشرده‌ای که گوشی دانلود می‌کند ۹۷۷ → ۷۱۴ کیلوبایت
       (۲۷٪ کمتر) و کارِ CPUِ بازشدن ۱۳٪ کمتر.

   ⚠️ این اسکریپت فقط هنگام دیپلوی روی *نسخهٔ منتشرشده* اجرا می‌شود. فایلِ داخلِ
   ریپو دست‌نخورده و خوانا می‌ماند — کامنت‌های فارسیِ این پروژه توضیحِ باگ‌های
   برگشتنی‌اند و نباید از منبع پاک شوند.

   ⚠️ فقط فاصله و کامنت برداشته می‌شود (minifyWhitespace). نامِ متغیرها و ساختارِ
   کد دست نمی‌خورد، چون کلِ برنامه با نامِ سراسریِ توابع کار می‌کند
   (onclick="..." داخلِ HTML) و تغییرِ نام همه‌چیز را می‌شکند.
   ⚠️ minifyIdentifiers یا minifySyntax را روشن نکنید.

   اگر هر بلاکی نشکافد یا هر بررسیِ ایمنی رد شود، اسکریپت با کدِ خطا بیرون
   می‌آید و ورک‌فلو فایلِ اصلی را منتشر می‌کند — سایتِ خرابْ منتشر نمی‌شود.
*/
import fs from 'fs';
import path from 'path';

let esbuild;
try {
  esbuild = (await import('esbuild')).default || (await import('esbuild'));
} catch (e) {
  console.error('esbuild در دسترس نیست — کوچک‌سازی رد شد، فایلِ اصلی منتشر می‌شود.');
  process.exit(2);
}

const SCRIPT_RE = /<script(?![^>]*\ssrc=)([^>]*)>([\s\S]*?)<\/script>/g;

/** هرچه *بیرونِ* <script> است — باید بایت‌به‌بایت دست‌نخورده بماند.
    ⚠️ این محکم‌ترین بررسیِ ممکن است و جای شمردنِ id ها را گرفت: شمارشِ
    id="..." اشتباه بود، چون همان الگو داخلِ رشته‌های خودِ جاوااسکریپت هم هست و
    با جمع‌شدنِ فاصله‌های عملگرِ + (' id="' + id + '"'  →  ' id="'+id+'"')
    عددش عوض می‌شد، بی‌آنکه ذره‌ای از محتوای رشته‌ها فرق کند. */
function outsideScripts(src) {
  let out = '', last = 0, m;
  SCRIPT_RE.lastIndex = 0;
  while ((m = SCRIPT_RE.exec(src))) { out += src.slice(last, m.index); last = m.index + m[0].length; }
  return out + src.slice(last);
}
/** نشانه‌هایی که باید بعد از کوچک‌سازی هم سرِ جایشان باشند */
function markers(src) {
  const m = {};
  const v = src.match(/const\s+APP_VERSION\s*=\s*['"]([0-9.]+)['"]/);
  m.version = v ? v[1] : null;
  SCRIPT_RE.lastIndex = 0;
  m.scripts = (src.match(SCRIPT_RE) || []).length;
  m.html = outsideScripts(src);
  return m;
}

function minifyHtml(file) {
  const src = fs.readFileSync(file, 'utf8');
  const before = markers(src);
  if (!before.scripts) return { file, skipped: 'بدونِ اسکریپتِ درون‌خطی' };

  let out = '', last = 0, m, done = 0, jsBefore = 0, jsAfter = 0;
  SCRIPT_RE.lastIndex = 0;
  while ((m = SCRIPT_RE.exec(src))) {
    const attrs = m[1], body = m[2];
    if (!body.trim()) continue;
    jsBefore += body.length;
    let code;
    try {
      code = esbuild.transformSync(body, {
        loader: 'js',
        minifyWhitespace: true,   // فقط فاصله و کامنت
        minifyIdentifiers: false, // ⚠️ روشن نکنید — نام‌ها سراسری‌اند
        minifySyntax: false,      // ⚠️ روشن نکنید — بازنویسیِ ساختار لازم نیست
        legalComments: 'none',
        charset: 'utf8',
        target: 'es2019',
      }).code;
    } catch (err) {
      throw new Error(`بلاکِ اسکریپت در ${file} نشکافت: ${String(err).slice(0, 200)}`);
    }
    // بررسیِ نحو روی خروجی — اگر خروجی خراب باشد همین‌جا می‌ایستیم
    try { new Function(code); } catch (err) {
      throw new Error(`خروجیِ کوچک‌شدهٔ ${file} نحوِ درستی ندارد: ${String(err).slice(0, 200)}`);
    }
    jsAfter += code.length;
    out += src.slice(last, m.index) + '<script' + attrs + '>' + code + '</script>';
    last = m.index + m[0].length;
    done++;
  }
  out += src.slice(last);

  const after = markers(out);
  const fail = [];
  if (after.version !== before.version) fail.push(`APP_VERSION عوض شد: ${before.version} → ${after.version}`);
  if (after.scripts !== before.scripts) fail.push(`تعدادِ بلاک‌های اسکریپت عوض شد: ${before.scripts} → ${after.scripts}`);
  if (after.html !== before.html) fail.push('HTML/CSSِ بیرونِ اسکریپت دست خورد — نباید می‌خورد');
  if (out.length >= src.length) fail.push('فایل کوچک‌تر نشد');
  if (fail.length) throw new Error(`${file}: ` + fail.join(' — '));

  fs.writeFileSync(file, out);
  return { file, blocks: done,
           mb: (src.length / 1048576).toFixed(2) + ' → ' + (out.length / 1048576).toFixed(2) + ' MB',
           js: (jsBefore / 1048576).toFixed(2) + ' → ' + (jsAfter / 1048576).toFixed(2) + ' MB',
           cut: (100 * (1 - jsAfter / jsBefore)).toFixed(0) + '٪ از JS' };
}

const files = process.argv.slice(2).length ? process.argv.slice(2)
            : ['index.html', 'payam/index.html'].filter(f => fs.existsSync(f));
let bad = 0;
for (const f of files) {
  try {
    const r = minifyHtml(f);
    console.log(r.skipped ? `— ${f}: ${r.skipped}`
      : `✅ ${f}: ${r.blocks} بلاک، ${r.mb} (${r.cut})`);
  } catch (e) {
    console.error('❌ ' + e.message);
    bad++;
  }
}
process.exit(bad ? 1 : 0);
