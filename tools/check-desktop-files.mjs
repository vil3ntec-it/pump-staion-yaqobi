// ---------------------------------------------------------------------------
//  نگهبانِ «فایلی جا نماند در نصب‌کننده»
//
//      node tools/check-desktop-files.mjs
//
//  الکترون‌بیلدر فقط فایل‌هایی را داخلِ نصب‌کننده می‌گذارد که در فهرستِ
//  ‎build.files‎ آمده باشند. روی کامپیوترِ خودمان برنامه از پوشهٔ منبع اجرا
//  می‌شود و همه‌چیز هست، پس اگر کسی فایلِ تازه‌ای اضافه کند و فهرست را به‌روز
//  نکند، همه‌چیز سالم به‌نظر می‌رسد و فقط در نسخهٔ نصب‌شدهٔ دستِ کاربر خراب
//  است. دقیقاً همین یک بار افتاد: find.html و find-preload.js (نوارِ Ctrl+F)
//  در فهرست نبودند.
//
//  این فایل هر ‎path.join(__dirname, '…')‎ ی را که main.js/preload.js به آن
//  اشاره می‌کنند پیدا می‌کند و می‌سنجد که هم روی دیسک باشد و هم در فهرست.
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import path from 'node:path';

const root = path.join(import.meta.dirname, '..');
const dir = path.join(root, 'desktop');
const pkg = JSON.parse(fs.readFileSync(path.join(dir, 'package.json'), 'utf8'));
const list = (pkg.build && pkg.build.files) || [];

let failed = 0;
const fail = (t, d) => { failed++; console.log(`  ❌ ${t}`); if (d) console.log(`     ${d}`); };
const ok = (t) => console.log(`  ✅ ${t}`);

console.log('\nنگهبانِ فایل‌های نصب‌کنندهٔ کامپیوتری\n');

/** آیا این نام با یکی از الگوهای فهرست پوشش داده می‌شود؟ */
function covered(name) {
  return list.some((pat) => {
    if (pat === name) return true;
    const star = pat.indexOf('*');
    if (star < 0) return false;
    const head = pat.slice(0, star);                 // مثلاً «app/»
    if (name.startsWith(head)) return true;
    return head === name + '/';                      // خودِ پوشه: «app» ↔ «app/**/*»
  });
}

/* پوشهٔ buildResources (آیکن و مانندش) از راهِ دیگری داخلِ نصب‌کننده می‌رود،
   نه از build.files — پس این‌جا سنجیده نمی‌شود. */
const SKIP = /^build\//;

const wanted = new Set();
for (const f of ['main.js', 'preload.js']) {
  const src = fs.readFileSync(path.join(dir, f), 'utf8');
  const re = /path\.join\(\s*__dirname\s*,\s*'([^']+)'\s*(?:,\s*'([^']+)'\s*)?\)/g;
  let m;
  while ((m = re.exec(src))) wanted.add(m[2] ? `${m[1]}/${m[2]}` : m[1]);
}

if (!wanted.size) fail('هیچ ارجاعی به فایل پیدا نشد', 'شاید شکلِ کد عوض شده — این نگهبان را به‌روز کنید.');

for (const name of [...wanted].sort()) {
  if (SKIP.test(name)) continue;
  const onDisk = fs.existsSync(path.join(dir, name));
  const inList = covered(name);
  if (!onDisk) fail(`«${name}» روی دیسک نیست`, 'main.js به فایلی اشاره می‌کند که وجود ندارد.');
  else if (!inList) fail(`«${name}» در build.files نیست`, 'در نسخهٔ نصب‌شده وجود نخواهد داشت — به فهرست اضافه‌اش کنید.');
  else ok(`${name} — هم هست، هم بسته‌بندی می‌شود`);
}

console.log('');
if (failed) { console.log(`${failed} قاعده شکست.\n`); process.exit(1); }
console.log('همهٔ فایل‌های لازم داخل نصب‌کننده می‌روند.\n');
