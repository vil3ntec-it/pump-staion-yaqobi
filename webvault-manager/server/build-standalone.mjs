// ساخت نسخهٔ تک‌فایل (index.html + styles.css + app.js) → webvault.html
// اجرا:  npm run build   (از پوشهٔ server)
//
// دیگر نیازی به تزریق کد نیست: پشتیبانی از «آدرس سرور خانگی» به‌صورت بومی
// داخل app.js هست (API_BASE + پنجرهٔ ⚙️). این اسکریپت فقط CSS و JS را درون‌خطی می‌کند.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUB = path.join(__dirname, 'public');
let html = fs.readFileSync(path.join(PUB, 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(PUB, 'styles.css'), 'utf8');
const js = fs.readFileSync(path.join(PUB, 'app.js'), 'utf8');

// جای‌گذاری CSS و JS به‌صورت درون‌خطی (تک‌فایل، بدون درخواست بیرونی)
html = html.replace('<link rel="stylesheet" href="styles.css" />', () => `<style>\n${css}\n</style>`);
html = html.replace('<script src="app.js" type="module"></script>', () => `<script type="module">\n${js}\n</script>`);

const out = path.join(__dirname, '..', 'webvault.html');
fs.writeFileSync(out, html);
console.log('نوشته شد:', out, '(' + (html.length / 1024).toFixed(1) + ' KB)');
