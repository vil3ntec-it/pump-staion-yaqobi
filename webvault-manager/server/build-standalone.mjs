// ساخت نسخهٔ تک‌فایل (index.html + styles.css + app.js) → webvault.html
// اجرا:  npm run build   (از پوشهٔ server)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUB = path.join(__dirname, 'public');
let html = fs.readFileSync(path.join(PUB, 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(PUB, 'styles.css'), 'utf8');
let js = fs.readFileSync(path.join(PUB, 'app.js'), 'utf8');

// --- تغییر app.js برای پشتیبانی از «آدرس سرور» قابل‌تنظیم (حالت تک‌فایل) ---
// ۱) افزودن API_BASE و منطق اتصال بالای فایل، بعد از تعریف state
const injectAfter = "const state = {";
const stateBlockEnd = js.indexOf("};", js.indexOf(injectAfter)) + 2;
const apiBaseCode = `

// --- حالت تک‌فایل: آدرس سرور قابل‌تنظیم (وقتی فایل مستقیم باز می‌شود) ---
let API_BASE = localStorage.getItem('wv_api_base') || '';
`;
js = js.slice(0, stateBlockEnd) + apiBaseCode + js.slice(stateBlockEnd);

// ۲) همهٔ فراخوانی‌های fetch به API را با API_BASE پیشوند بده
js = js.replace(/fetch\('\/api' \+ path/g, "fetch(API_BASE + '/api' + path");
js = js.replace(/fetch\('\/api' \+ path,/g, "fetch(API_BASE + '/api' + path,");
js = js.replace(/fetch\(`\/api\/files\/\$\{id\}\/download`/g, "fetch(`${API_BASE}/api/files/${id}/download`");

// ۳) بازنویسی initLock برای نمایش فیلد آدرس سرور هنگام شکست اتصال
const initLockStart = js.indexOf('async function initLock()');
const initLockEnd = js.indexOf('\n}', initLockStart) + 2;
const newInitLock = `async function initLock() {
  try {
    const st = await GET('/status');
    state.autoLockMin = st.autoLockMinutes || 15;
    isSetup = !st.initialized;
    $('#serverField').style.display = 'none';
    $('#masterPass').style.display = 'block';
    $('#lockSubtitle').textContent = isSetup
      ? 'برای شروع، یک «رمز اصلی» قوی تعیین کنید'
      : 'برای دسترسی، رمز اصلی را وارد کنید';
    $('#confirmField').style.display = isSetup ? 'block' : 'none';
    $('#lockBtn').textContent = isSetup ? 'ساخت صندوق' : 'باز کردن';
  } catch {
    // اتصال برقرار نشد → آدرس سرور را بپرس
    $('#serverField').style.display = 'block';
    $('#serverUrl').value = API_BASE || 'http://192.168.1.20:4600';
    $('#masterPass').style.display = 'none';
    $('#confirmField').style.display = 'none';
    $('#lockSubtitle').textContent = 'ابتدا آدرس سرور خانگی خود را وارد کنید';
    $('#lockBtn').textContent = 'اتصال به سرور';
  }
}`;
js = js.slice(0, initLockStart) + newInitLock + js.slice(initLockEnd);

// ۴) در submit فرم قفل، اگر فیلد سرور نمایش داده شده، اول وصل شو
const submitAnchor = "$('#lockForm').addEventListener('submit', async (e) => {\n  e.preventDefault();\n  $('#lockError').textContent = '';";
const connectStep = `$('#lockForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  $('#lockError').textContent = '';
  // حالت تک‌فایل: مرحلهٔ اتصال به سرور
  if ($('#serverField').style.display !== 'none') {
    const u = $('#serverUrl').value.trim().replace(/\\/+$/, '');
    if (!/^https?:\\/\\//.test(u)) { $('#lockError').textContent = 'آدرس باید با http:// شروع شود'; return; }
    API_BASE = u; localStorage.setItem('wv_api_base', u);
    await initLock();
    if ($('#serverField').style.display !== 'none') $('#lockError').textContent = 'اتصال برقرار نشد — آدرس و روشن‌بودن سرور را بررسی کنید';
    else setTimeout(() => $('#masterPass').focus(), 50);
    return;
  }`;
js = js.replace(submitAnchor, connectStep);

// --- ساخت HTML تک‌فایل ---
// افزودن فیلد آدرس سرور به کارت قفل (پیش از فیلد رمز)
html = html.replace(
  '        <div class="field">\n          <input type="password" id="masterPass" placeholder="رمز اصلی" autocomplete="off" />\n        </div>',
  '        <div class="field" id="serverField" style="display:none">\n' +
  '          <input type="text" id="serverUrl" placeholder="http://192.168.1.20:4600" style="direction:ltr;text-align:center;letter-spacing:0" />\n' +
  '        </div>\n' +
  '        <div class="field">\n          <input type="password" id="masterPass" placeholder="رمز اصلی" autocomplete="off" />\n        </div>'
);
// جای‌گذاری CSS و JS به‌صورت درون‌خطی
html = html.replace('<link rel="stylesheet" href="styles.css" />', () => `<style>\n${css}\n</style>`);
html = html.replace('<script src="app.js" type="module"></script>', () => `<script type="module">\n${js}\n</script>`);

const out = path.join(__dirname, '..', 'webvault.html');
fs.writeFileSync(out, html);
console.log('نوشته شد:', out, '(' + (html.length / 1024).toFixed(1) + ' KB)');
console.log('تعداد fetch با API_BASE:', (js.match(/API_BASE \+ '\/api'|\$\{API_BASE\}/g) || []).length);
