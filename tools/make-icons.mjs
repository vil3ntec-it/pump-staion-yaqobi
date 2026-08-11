/* ساختنِ همهٔ آیکون‌های برنامه از روی یک نشان — icons/logo.svg
 * ---------------------------------------------------------------------------
 * خروجی‌ها:
 *   icons/icon-192.png            آیکونِ برنامه (قابِ تیرهٔ گردگوشه)
 *   icons/icon-512.png
 *   icons/icon-maskable-192.png   آیکونِ ماسک‌دارِ اندروید (قابِ تیرهٔ لبه‌تا‌لبه)
 *   icons/icon-maskable-512.png
 *   icons/apple-touch-icon.png    آیکونِ آیفون (۱۸۰)
 *   favicon.png                   آیکونِ تبِ مرورگر (۱۲۸، بی‌قاب و شفاف)
 *   icons/badge-96.png            نشانِ کوچکِ اعلانِ اندروید — تک‌رنگ
 *
 * چرا badge جداست: اندروید برای آیکونِ کوچکِ اعلان فقط کانالِ آلفا را می‌خواند و
 * رنگ‌ها را دور می‌ریزد. اگر آیکونِ رنگیِ پُر بدهیم یک مربعِ سفیدِ خالی نشان
 * می‌دهد. پس همان نشان به سیلوئتِ تک‌رنگ تبدیل می‌شود: خودِ قطره توپر و گیجِ
 * سفیدِ داخلش سوراخ. چون از خودِ نشان ساخته می‌شود، با هر تغییرِ آن خودکار
 * هم‌گام می‌ماند.
 *
 * اجرا:
 *   node tools/make-icons.mjs
 * نیاز: کرومیوم (برای رسم). اگر playwright نصب نیست، مسیرِ کرومیوم را بدهید:
 *   CHROMIUM=/path/to/chrome node tools/make-icons.mjs
 */
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const svg = readFileSync(join(root, 'icons/logo.svg'), 'utf8');

/* قابِ آیکون‌ها این‌جا کشیده می‌شود، نه داخلِ خودِ نشان: یک صفحهٔ تیرهٔ گردگوشه
   که نشانِ کهربایی رویش می‌نشیند. چرا جدا؟ چون نشانِ کوچکِ اعلان باید فقط
   سیلوئتِ خودِ نشان باشد؛ اگر پس‌زمینه داخلِ logo.svg بود، آن نشان یک مربعِ
   توپرِ بی‌شکل می‌شد.
     radius: گردیِ گوشه‌ها (نسبتِ اندازه) — ماسکِ اندروید خودش گوشه را می‌برد،
             پس برای maskable صفر است و به‌جایش «حاشیهٔ امن» بزرگ‌تر می‌شود.
     scale : سهمِ خودِ نشان از قاب. */
const JOBS = [
  { file: 'icons/icon-192.png',          size: 192, plate: true,  radius: 0.22, scale: 0.62 },
  { file: 'icons/icon-512.png',          size: 512, plate: true,  radius: 0.22, scale: 0.62 },
  { file: 'icons/icon-maskable-192.png', size: 192, plate: true,  radius: 0,    scale: 0.50 },
  { file: 'icons/icon-maskable-512.png', size: 512, plate: true,  radius: 0,    scale: 0.50 },
  { file: 'icons/apple-touch-icon.png',  size: 180, plate: true,  radius: 0.22, scale: 0.62 },
  { file: 'favicon.png',                 size: 128, plate: false, radius: 0,    scale: 1.0 },
];

const chromiumPath = process.env.CHROMIUM ||
  ['/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
   '/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/google-chrome'
  ].find(p => existsSync(p));

// playwright از جای نصبش خوانده می‌شود؛ اگر داخلِ همین پوشه نیست، مسیرش را بدهید:
//   PLAYWRIGHT=/path/to/node_modules/playwright node tools/make-icons.mjs
const { chromium } = await import(process.env.PLAYWRIGHT || 'playwright');
const browser = await chromium.launch(chromiumPath ? { executablePath: chromiumPath } : {});
const page = await browser.newPage();
await page.setContent('<body style="margin:0"></body>');

const draw = await page.evaluateHandle(() => 0);   // فقط برای گرم کردنِ صفحه
void draw;

async function render(job) {
  return page.evaluate(async ({ svg, size, plate, radius, scale }) => {
    const img = new Image();
    const url = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
    await new Promise((res, rej) => { img.onload = res; img.onerror = rej; img.src = url; });
    const c = document.createElement('canvas');
    c.width = c.height = size;
    const x = c.getContext('2d');
    if (plate) {
      const g = x.createLinearGradient(0, 0, size, size);
      g.addColorStop(0, '#1E2A3E');
      g.addColorStop(1, '#0D141F');
      x.fillStyle = g;
      const r = size * radius;
      x.beginPath();
      if (r > 0) {
        x.moveTo(r, 0); x.arcTo(size, 0, size, size, r); x.arcTo(size, size, 0, size, r);
        x.arcTo(0, size, 0, 0, r); x.arcTo(0, 0, size, 0, r); x.closePath();
      } else x.rect(0, 0, size, size);
      x.fill();
    }
    const s = Math.round(size * scale), o = Math.round((size - s) / 2);
    x.drawImage(img, o, o, s, s);
    return c.toDataURL('image/png');
  }, { svg, size: job.size, plate: job.plate, radius: job.radius, scale: job.scale });
}

for (const job of JOBS) {
  const dataUrl = await render(job);
  writeFileSync(join(root, job.file), Buffer.from(dataUrl.split(',')[1], 'base64'));
  console.log('✅', job.file);
}

/* نشانِ اعلان: همان نشان، ولی تک‌رنگ. پیکسل‌های سفیدِ نشان (گیجِ داخلِ قطره)
   سوراخ می‌شوند و بقیه سفیدِ توپر — یعنی اندروید سیلوئتِ قطره را با گیجِ
   تو‌خالی نشان می‌دهد. */
const badge = await page.evaluate(async ({ svg, size }) => {
  const img = new Image();
  await new Promise((res, rej) => {
    img.onload = res; img.onerror = rej;
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
  });
  const c = document.createElement('canvas');
  c.width = c.height = size;
  const x = c.getContext('2d');
  x.drawImage(img, 0, 0, size, size);
  const d = x.getImageData(0, 0, size, size), p = d.data;
  for (let i = 0; i < p.length; i += 4) {
    const a = p[i + 3];
    if (!a) continue;
    const white = p[i] > 205 && p[i + 1] > 205 && p[i + 2] > 205;   // سفیدِ نشان = سوراخ
    p[i] = p[i + 1] = p[i + 2] = 255;
    p[i + 3] = white ? 0 : a;
  }
  x.putImageData(d, 0, 0);
  return c.toDataURL('image/png');
}, { svg, size: 96 });
writeFileSync(join(root, 'icons/badge-96.png'), Buffer.from(badge.split(',')[1], 'base64'));
console.log('✅ icons/badge-96.png (تک‌رنگ، از روی همان لوگو)');

await browser.close();
