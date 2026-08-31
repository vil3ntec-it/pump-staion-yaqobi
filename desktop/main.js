// ============================================================================
//  پمپ یعقوبی — برنامهٔ کامپیوتری (پوستهٔ Electron)
//  ---------------------------------------------------------------------------
//  • خودِ برنامه (index.html) را به‌صورت محلی و کاملاً آفلاین اجرا می‌کند.
//  • برنامه از یک «پروتکل امنِ اختصاصی» (app://) سرو می‌شود، نه از file://،
//    تا Chromium ذخیره‌سازی IndexedDB/localStorage را مثل یک سایتِ واقعیِ امن
//    پایدار نگه دارد و سهمیهٔ ذخیره‌سازی بر اساس فضای دیسک باشد، نه سقفِ تب.
//  • صفحهٔ لودینگ (اسپلش) «واقعی» است: نوارش دقیقاً همان کاری را نشان می‌دهد
//    که همین حالا انجام شده — بایت‌های خوانده‌شدهٔ فایلِ برنامه، ساخته‌شدنِ
//    صفحه، خوانده‌شدنِ دفترها و چیده‌شدنِ جدول‌ها. هیچ تایمر و هیچ نوارِ
//    «همیشه در حال حرکت»ی در کار نیست و پنجره دقیقاً وقتی باز می‌شود که
//    برنامه واقعاً آمادهٔ کار است، نه یک ثانیه زودتر یا دیرتر.
//  • آپدیتِ داخلِ برنامه: نسخهٔ تازهٔ سایت را خودش می‌گیرد و با یک دکمه سوار
//    می‌کند — دیگر برای هر تغییرِ کوچک نباید برنامه را از نو نصب کرد.
// ============================================================================
const { app, BrowserWindow, protocol, net, Menu, shell, session, ipcMain, dialog } = require('electron');
const os = require('node:os');
const path = require('node:path');
const fs = require('node:fs');
const fsp = require('node:fs/promises');
const { pathToFileURL } = require('node:url');

const APP_DIR = path.join(__dirname, 'app');          // نسخهٔ همراهِ نصب‌کننده
const SCHEME = 'app';
const START_URL = 'app://local/index.html';
// اگر خودِ برنامه (نسخهٔ کهنه‌ای که هنوز مرحله‌هایش را خبر نمی‌دهد) هیچ خبری
// نداد، بیشتر از این منتظرش نمی‌مانیم و پنجره را باز می‌کنیم.
const READY_FALLBACK_MS = 4000;
// و اگر کلاً چیزی بالا نیامد، پنجره بعد از این باز می‌شود تا کاربر پشتِ
// اسپلش گیر نکند.
const HARD_TIMEOUT_MS = 45000;

// منبعِ آپدیت — به ترتیب امتحان می‌شود (اولی دامنهٔ خودمان، دومی گیت‌هاب)
const DEFAULT_UPDATE_BASES = [
  'https://yaqobipump.top/',
  'https://vil3ntec-it.github.io/pump-staion-yaqobi/',
];

let mainWin = null;
let splashWin = null;
let updateTimer = null;

// ---------------------------------------------------------------------------
//  پوشهٔ آپدیت (کنارِ داده‌های کاربر، نه داخلِ پوشهٔ نصب — پس اجازهٔ نوشتن دارد)
// ---------------------------------------------------------------------------
function updDir()      { return path.join(app.getPath('userData'), 'updates'); }
function updIndex()    { return path.join(updDir(), 'index.html'); }
function updMetaFile() { return path.join(updDir(), 'meta.json'); }
function updFlagFile() { return path.join(updDir(), 'verify.flag'); }   // نشانهٔ «هنوز امتحان نشده»
function updCfgFile()  { return path.join(app.getPath('userData'), 'update-config.json'); }
function winStateFile(){ return path.join(app.getPath('userData'), 'window-state.json'); }

function readJson(file, fallback) {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch (e) { return fallback; }
}
function updateBases() {
  const cfg = readJson(updCfgFile(), {});
  const list = [];
  if (process.env.PUMP_UPDATE_BASE) list.push(process.env.PUMP_UPDATE_BASE);
  if (cfg && typeof cfg.base === 'string' && /^https?:\/\//i.test(cfg.base)) list.push(cfg.base);
  for (const b of DEFAULT_UPDATE_BASES) if (!list.includes(b)) list.push(b);
  return list.map(b => (b.endsWith('/') ? b : b + '/'));
}
function parseVersion(html) {
  const m = String(html || '').match(/const\s+APP_VERSION\s*=\s*['"]([0-9]+(?:\.[0-9]+)*)['"]/);
  return m ? m[1] : null;
}
function cmpVersion(a, b) {                 // ۱ اگر a تازه‌تر باشد
  const pa = String(a || '0').split('.').map(n => parseInt(n, 10) || 0);
  const pb = String(b || '0').split('.').map(n => parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const x = pa[i] || 0, y = pb[i] || 0;
    if (x !== y) return x > y ? 1 : -1;
  }
  return 0;
}
function bundledVersion() { return parseVersion(readFileSafe(path.join(APP_DIR, 'index.html'))) || '0'; }
function readFileSafe(f) { try { return fs.readFileSync(f, 'utf8'); } catch (e) { return ''; } }

// نسخهٔ دانلودشده فقط وقتی استفاده می‌شود که سالم و تازه‌تر از نسخهٔ همراهِ نصب باشد
function activeIndexPath() {
  try {
    if (!fs.existsSync(updIndex())) return path.join(APP_DIR, 'index.html');
    const meta = readJson(updMetaFile(), null);
    if (!meta || !meta.version) return path.join(APP_DIR, 'index.html');
    if (cmpVersion(meta.version, bundledVersion()) <= 0) return path.join(APP_DIR, 'index.html');
    const st = fs.statSync(updIndex());
    if (!st.size || st.size < 100000) return path.join(APP_DIR, 'index.html');   // فایلِ ناقص
    return updIndex();
  } catch (e) { return path.join(APP_DIR, 'index.html'); }
}
function activeVersion() {
  const p = activeIndexPath();
  if (p === updIndex()) { const m = readJson(updMetaFile(), {}); return m.version || bundledVersion(); }
  return bundledVersion();
}
function rollbackUpdate(why) {
  try { fs.rmSync(updDir(), { recursive: true, force: true }); } catch (e) {}
  console.warn('[آپدیت] برگشت به نسخهٔ همراهِ نصب:', why || '');
}

// ---------------------------------------------------------------------------
//  گرفتنِ نسخهٔ تازه از سایت
// ---------------------------------------------------------------------------
async function fetchLatest() {
  const errs = [];
  for (const base of updateBases()) {
    const url = base + 'index.html?ts=' + Date.now();
    try {
      const res = await net.fetch(url, { cache: 'no-store' });
      if (!res.ok) { errs.push(base + ' → HTTP ' + res.status); continue; }
      const html = await res.text();
      const version = parseVersion(html);
      // اعتبارسنجی: باید واقعاً خودِ برنامه باشد، نه صفحهٔ خطا یا فایلِ نیمه‌کاره
      if (!version || html.length < 100000 || !/<\/html>/i.test(html)) { errs.push(base + ' → فایل معتبر نبود'); continue; }
      return { ok: true, base, version, html };
    } catch (e) { errs.push(base + ' → ' + (e && e.message)); }
  }
  return { ok: false, error: errs.join(' · ') || 'اتصال برقرار نشد' };
}

async function checkUpdate() {
  const cur = activeVersion();
  const r = await fetchLatest();
  if (!r.ok) return { ok: false, current: cur, error: r.error };
  return { ok: true, current: cur, latest: r.version, hasUpdate: cmpVersion(r.version, cur) > 0, from: r.base };
}

async function downloadUpdate() {
  const cur = activeVersion();
  const r = await fetchLatest();
  if (!r.ok) return { ok: false, current: cur, error: r.error };
  if (cmpVersion(r.version, cur) <= 0) return { ok: true, current: cur, latest: r.version, hasUpdate: false };
  await fsp.mkdir(updDir(), { recursive: true });
  const tmp = updIndex() + '.tmp';
  await fsp.writeFile(tmp, r.html, 'utf8');            // اول کامل نوشته می‌شود…
  await fsp.rename(tmp, updIndex());                   // …بعد یک‌باره جایگزین (اتمیک)
  await fsp.writeFile(updMetaFile(), JSON.stringify({ version: r.version, from: r.base, ts: Date.now() }), 'utf8');
  // فایل‌های جانبی (اختیاری — نبودشان برنامه را خراب نمی‌کند)
  for (const extra of ['manifest.json', 'favicon.png']) {
    try {
      const res = await net.fetch(r.base + extra + '?ts=' + Date.now(), { cache: 'no-store' });
      if (res.ok) await fsp.writeFile(path.join(updDir(), extra), Buffer.from(await res.arrayBuffer()));
    } catch (e) {}
  }
  return { ok: true, current: cur, latest: r.version, hasUpdate: true, downloaded: true };
}

// ---------------------------------------------------------------------------
//  پیشرفتِ واقعیِ بالا آمدن
//  ---------------------------------------------------------------------------
//  گلایهٔ صاحب ریپو: «نه لودینگِ واقعی دارد، نه مثل برنامهٔ واقعی است — الکی
//  می‌گوید من لودینگ دارم.» درست بود: نوارِ قبلی یک انیمیشنِ بی‌پایان بود و
//  پنجره با یک تایمرِ ۱٫۴ ثانیه‌ای باز می‌شد، حتی اگر برنامه زودتر آماده شده
//  بود یا هنوز نشده بود.
//
//  حالا هر درصدی که روی نوار می‌بینید یک کارِ واقعیِ تمام‌شده است:
//    ۰…۵    پوستهٔ برنامه و پیدا کردنِ فایل
//    ۵…۴۵   خواندنِ خودِ فایلِ برنامه — دقیقاً بر اساسِ بایت‌هایی که خوانده شده
//    ۵۵…۸۵  مرحله‌هایی که خودِ برنامه خبر می‌دهد: اجرای برنامه، خواندنِ دفترها،
//           چیده شدنِ بخش‌ها
//    ۹۰…۹۵  ساخته شدنِ صفحه (dom-ready / بارگذاری کامل)
//    ۱۰۰    آمادهٔ کار — همین‌جا پنجره باز می‌شود و اسپلش می‌رود
//  اگر نسخهٔ کهنه‌ای بالا بیاید که مرحله‌هایش را خبر نمی‌دهد، پس از
//  READY_FALLBACK_MS خودمان تمامش می‌کنیم تا کسی پشتِ اسپلش گیر نکند.
// ---------------------------------------------------------------------------
let splashReady = false;      // خودِ صفحهٔ اسپلش بار شده؟
let splashQueue = null;       // آخرین خبری که پیش از بار شدنش رسید
let bootPct = 0;
let bootDone = false;
let readyFallbackTimer = null;
let hardTimeoutTimer = null;

function pushSplash(patch) {
  if (!splashWin || splashWin.isDestroyed()) return;
  if (!splashReady) { splashQueue = Object.assign(splashQueue || {}, patch); return; }
  try {
    splashWin.webContents.executeJavaScript(
      'window.__pumpSplash && window.__pumpSplash(' + JSON.stringify(patch) + ')', true
    ).catch(() => {});
  } catch (e) {}
}

/** یک مرحلهٔ واقعی. درصد هرگز عقب نمی‌رود. */
function bootMark(pct, stage, detail) {
  if (bootDone) return;
  const p = Math.max(bootPct, Math.min(100, Math.round(pct)));
  const patch = { pct: p };
  if (stage) patch.stage = stage;
  if (typeof detail === 'string') patch.detail = detail;
  bootPct = p;
  pushSplash(patch);
}

function humanSize(n) {
  if (n >= 1048576) return (n / 1048576).toFixed(1) + ' مگابایت';
  return Math.round(n / 1024) + ' کیلوبایت';
}

/** خودِ فایلِ برنامه را تکه‌تکه می‌دهد و هر تکه را روی نوار خبر می‌کند.
    اگر به هر دلیلی نشد، null برمی‌گرداند تا مسیرِ همیشگی کار کند. */
function streamAppFile(filePath) {
  let total = 0;
  try { total = fs.statSync(filePath).size; } catch (e) { return null; }
  if (!total) return null;
  bootMark(5, 'خواندنِ فایلِ برنامه', humanSize(total));
  let read = 0, lastPct = 5;
  let node;
  try { node = fs.createReadStream(filePath); } catch (e) { return null; }
  const body = new ReadableStream({
    start(controller) {
      node.on('data', (chunk) => {
        read += chunk.length;
        controller.enqueue(new Uint8Array(chunk));
        const pct = 5 + Math.floor((read / total) * 40);
        if (pct > lastPct) {                    // فقط وقتی عددِ روی نوار عوض شود
          lastPct = pct;
          bootMark(pct, 'خواندنِ فایلِ برنامه', humanSize(read) + ' از ' + humanSize(total));
        }
      });
      node.on('end', () => { try { controller.close(); } catch (e) {} });
      node.on('error', (err) => { try { controller.error(err); } catch (e) {} });
    },
    cancel() { try { node.destroy(); } catch (e) {} },
  });
  return new Response(body, {
    headers: { 'content-type': 'text/html; charset=utf-8', 'content-length': String(total) },
  });
}

/** برنامه آماده است → پنجره باز، اسپلش بسته. فقط یک‌بار. */
function bootFinish(why) {
  if (bootDone) return;
  bootDone = true;
  clearTimeout(readyFallbackTimer); readyFallbackTimer = null;
  clearTimeout(hardTimeoutTimer);   hardTimeoutTimer = null;
  bootPct = 100;
  pushSplash({ pct: 100, stage: 'آماده', detail: '' });
  // یک پلکِ کوتاه فقط برای اینکه ۱۰۰٪ دیده شود، نه برای وقت‌کشی
  setTimeout(() => {
    if (splashWin && !splashWin.isDestroyed()) splashWin.close();
    if (mainWin && !mainWin.isDestroyed()) { mainWin.show(); mainWin.focus(); }
  }, 140);
  if (why) console.log('[لودینگ] پایان:', why);
}

/** خودِ برنامه مرحله‌هایش را از همین‌جا خبر می‌دهد (از طریقِ preload) */
ipcMain.on('boot:step', (_e, step) => {
  switch (String(step || '')) {
    case 'script':  bootMark(55, 'اجرای برنامه'); break;
    case 'data':    bootMark(70, 'خواندنِ دفترها'); break;
    case 'render':  bootMark(85, 'چیدنِ بخش‌ها'); break;
    case 'ready':   bootFinish('خودِ برنامه گفت آماده است'); break;
    default: break;
  }
  // تا وقتی خبر می‌رسد یعنی برنامه زنده است — مهلتِ «نسخهٔ کهنه» را عقب می‌بریم
  if (readyFallbackTimer) {
    clearTimeout(readyFallbackTimer);
    readyFallbackTimer = setTimeout(() => bootFinish('مهلتِ مرحلهٔ بعدی تمام شد'), READY_FALLBACK_MS);
  }
});

// ---------------------------------------------------------------------------
//  پنجره‌ها
// ---------------------------------------------------------------------------
function createSplash() {
  // هر بار که برنامه از نو بالا می‌آید (مثلاً روی مک با کلیکِ دوباره روی آیکون)
  // شمارشِ لودینگ هم از صفر شروع شود، وگرنه اسپلش هرگز بسته نمی‌شد.
  bootDone = false; bootPct = 0; splashReady = false; splashQueue = null;
  splashWin = new BrowserWindow({
    width: 460, height: 320, frame: false, resizable: false, center: true,
    alwaysOnTop: true, backgroundColor: '#0b0f17', show: true,
    webPreferences: { contextIsolation: true },
  });
  splashWin.loadFile(path.join(__dirname, 'splash.html'));
  splashWin.webContents.once('did-finish-load', () => {
    splashReady = true;
    const first = Object.assign({ pct: bootPct, version: activeVersion() }, splashQueue || {});
    splashQueue = null;
    pushSplash(first);
  });
  splashWin.on('closed', () => { splashWin = null; splashReady = false; });
}

// نوارِ منو («برنامه / ویرایش / نما») به‌طور کامل برداشته شد — نه دیده می‌شود و
// نه با Alt برمی‌گردد.
//
// ولی در Electron، اگر منو نباشد کلیدهای Ctrl+C / Ctrl+V / Ctrl+X / Ctrl+Z /
// Ctrl+A اصلاً کار نمی‌کنند (این کلیدها را خودِ منو ثبت می‌کند، نه مرورگر).
// برای برنامه‌ای که تمامِ کارش وارد کردنِ عدد و نام است این بزرگ‌ترین باگ بود؛
// پس همان کلیدها را مستقیم روی خودِ پنجره می‌گیریم و به webContents می‌سپاریم.
function stripMenu() {
  Menu.setApplicationMenu(null);
}

/** کلیدهای ویرایش/نما بدونِ منو — دقیقاً همان کاری که roleهای منو می‌کردند */
// فوکوس داخلِ کادرِ متنی است یا نه (preload خبر می‌دهد). مهم است: بیرونِ کادر،
// خودِ برنامه Ctrl+Z و Ctrl+X را برای «واگرد/ازنوِ کلِ دفتر» می‌خواهد، پس
// آن‌جا نباید پوسته کلید را بدزدد.
let editableFocus = false;
ipcMain.on('focus:editable', (_e, value) => { editableFocus = Boolean(value); });

function bindShortcuts(win) {
  // نوار منو حتی با Alt هم بالا نیاید (این دو فقط روی ویندوز/لینوکس هستند)
  try { win.removeMenu(); } catch (e) {}
  try { win.setMenuBarVisibility(false); } catch (e) {}
  win.webContents.on('before-input-event', (event, input) => {
    if (input.type !== 'keyDown') return;
    const wc = win.webContents;
    const key = String(input.key || '').toLowerCase();
    const mod = process.platform === 'darwin' ? input.meta : input.control;

    if (key === 'f11') { win.setFullScreen(!win.isFullScreen()); event.preventDefault(); return; }
    // Esc نوارِ جست‌وجو را می‌بندد — ولی فقط وقتی باز است، وگرنه دستِ خودِ برنامه
    if (key === 'escape' && findWin && !findWin.isDestroyed()) { closeFindBar(); event.preventDefault(); return; }
    if (key === 'f3') { openFindBar(); event.preventDefault(); return; }
    if (!mod || input.alt) return;

    const run = (fn) => { fn(); event.preventDefault(); };
    // این‌ها در برنامه میانبرِ دیگری ندارند → همیشه دستِ پوسته
    if (key === 'c') return run(() => wc.copy());
    if (key === 'v') return run(() => (input.shift ? wc.pasteAndMatchStyle() : wc.paste()));
    if (key === 'a') return run(() => wc.selectAll());
    // Ctrl+F: در برنامه میانبرِ دیگری ندارد (بررسی شد) → جست‌وجوی کلِ صفحه
    if (key === 'f') return run(() => openFindBar());
    if (key === 'y') return run(() => wc.redo());
    if (key === 'r') return run(() => wc.reload());
    // بزرگ‌نمایی: هم ردیفِ عددها (+ - 0) و هم کلیدهای numpad
    if (key === '+' || key === '=' || key === 'add') return run(() => wc.setZoomLevel(Math.min(6, wc.getZoomLevel() + 0.5)));
    if (key === '-' || key === 'subtract') return run(() => wc.setZoomLevel(Math.max(-6, wc.getZoomLevel() - 0.5)));
    if (key === '0') return run(() => wc.setZoomLevel(0));
    // این دو فقط داخلِ کادرِ متنی — بیرونش مالِ واگرد/ازنوِ خودِ برنامه است
    if (!editableFocus) return;
    if (key === 'x') return run(() => wc.cut());
    if (key === 'z') return run(() => (input.shift ? wc.redo() : wc.undo()));
  });
  // پنجره که عوض/بارگذاری مجدد شد، حالتِ فوکوس از صفر
  win.webContents.on('did-start-navigation', () => { editableFocus = false; });
}

// ---------------------------------------------------------------------------
//  یادِ پنجره — اندازه، جا و «تمام‌صفحه بودن»
//
//  کارِ نسخهٔ کامپیوتری با نسخهٔ وب فرق دارد: کاربر پنجره را یک‌بار همان‌طور
//  که می‌خواهد می‌گذارد (مثلاً تمام‌صفحه روی مانیتورِ دوم) و انتظار دارد دفعهٔ
//  بعد همان‌جا باز شود. تا حالا هر بار از صفر ۱۳۶۰×۹۰۰ وسطِ صفحهٔ اول باز
//  می‌شد. مرورگر چنین چیزی ندارد؛ این یکی از کارهایی است که فقط پوستهٔ
//  کامپیوتری می‌تواند بکند.
//
//  ⚠️ ایمنی: جای ذخیره‌شده فقط وقتی به کار می‌رود که واقعاً روی یکی از
//  نمایشگرهای همین لحظه بیفتد. وگرنه (مانیتورِ دوم جدا شده، رزولوشن عوض
//  شده) پنجره بیرونِ دیدِ کاربر باز می‌شد و برنامه «باز نمی‌شود» به نظر
//  می‌رسید. در آن حالت به همان اندازهٔ پیش‌فرضِ وسطِ صفحه برمی‌گردیم.
// ---------------------------------------------------------------------------
const WIN_DEFAULT = { width: 1360, height: 900 };

function loadWinState() {
  const st = readJson(winStateFile(), null);
  if (!st || typeof st !== 'object') return null;
  const b = st.bounds;
  if (!b || !Number.isFinite(b.x) || !Number.isFinite(b.y)
        || !Number.isFinite(b.width) || !Number.isFinite(b.height)) return null;
  if (b.width < 600 || b.height < 400) return null;
  // آیا این مستطیل روی یکی از نمایشگرهای موجود دیده می‌شود؟
  let visible = false;
  try {
    const { screen } = require('electron');
    visible = screen.getAllDisplays().some((d) => {
      const w = d.workArea;
      return b.x < w.x + w.width && b.x + b.width > w.x
          && b.y < w.y + w.height && b.y + b.height > w.y;
    });
  } catch (e) { visible = false; }
  if (!visible) return null;
  return { bounds: b, maximized: Boolean(st.maximized), fullScreen: Boolean(st.fullScreen) };
}

/** ذخیره با تاخیر — هنگام کشیدن/تغییرِ اندازه ده‌ها رویداد می‌آید و نباید
 *  برای هرکدام روی دیسک بنویسیم. */
let winSaveTimer = null;
function rememberWin(win) {
  if (!win || win.isDestroyed()) return;
  clearTimeout(winSaveTimer);
  winSaveTimer = setTimeout(() => {
    try {
      if (!win || win.isDestroyed()) return;
      // اندازهٔ «عادی» را می‌خواهیم، نه اندازهٔ تمام‌صفحه
      const bounds = win.isMaximized() || win.isFullScreen() ? win.getNormalBounds() : win.getBounds();
      fs.writeFileSync(winStateFile(), JSON.stringify({
        bounds, maximized: win.isMaximized(), fullScreen: win.isFullScreen(),
      }), 'utf8');
    } catch (e) {}
  }, 400);
}

function bindWinState(win) {
  ['resize', 'move', 'maximize', 'unmaximize', 'enter-full-screen', 'leave-full-screen']
    .forEach((ev) => win.on(ev, () => rememberWin(win)));
  // بستنِ برنامه: بدونِ تاخیر، وگرنه آخرین تغییر از دست می‌رود
  win.on('close', () => {
    clearTimeout(winSaveTimer);
    try {
      const bounds = win.isMaximized() || win.isFullScreen() ? win.getNormalBounds() : win.getBounds();
      fs.writeFileSync(winStateFile(), JSON.stringify({
        bounds, maximized: win.isMaximized(), fullScreen: win.isFullScreen(),
      }), 'utf8');
    } catch (e) {}
  });
}

// ---------------------------------------------------------------------------
//  جست‌وجو در صفحه (Ctrl+F) — کارِ خودِ پوسته، نه برنامه
//
//  در مرورگر این کار را خودِ مرورگر می‌کند؛ در پوستهٔ کامپیوتری هیچ‌کس
//  نمی‌کرد و Ctrl+F عملاً بی‌اثر بود. حالا نوارِ کوچکی بالا-چپِ پنجره باز
//  می‌شود و از موتورِ خودِ کروم (findInPage) استفاده می‌کند: همان جست‌وجوی
//  «کلِ صفحه» با شمارشِ نتیجه‌ها و رفت‌وبرگشت.
//
//  ⚠️ هیچ ربطی به جست‌وجوهای خودِ برنامه ندارد و هیچ کدی از index.html را
//  صدا نمی‌زند — پس نه منطقی عوض می‌شود و نه فیلترهای هر بخش.
// ---------------------------------------------------------------------------
let findWin = null;

function findBarBounds() {
  const b = mainWin.getContentBounds();
  const w = Math.min(430, Math.max(300, b.width - 48));
  return { x: b.x + 24, y: b.y + 18, width: w, height: 52 };
}

function openFindBar() {
  if (!mainWin || mainWin.isDestroyed()) return;
  if (findWin && !findWin.isDestroyed()) {
    findWin.show();
    findWin.webContents.send('find:focus');
    return;
  }
  findWin = new BrowserWindow(Object.assign({
    parent: mainWin, frame: false, resizable: false, movable: true,
    minimizable: false, maximizable: false, fullscreenable: false,
    skipTaskbar: true, transparent: true, backgroundColor: '#00000000',
    show: false, webPreferences: {
      preload: path.join(__dirname, 'find-preload.js'),
      contextIsolation: true, nodeIntegration: false, spellcheck: false,
    },
  }, findBarBounds()));
  findWin.setMenu && findWin.setMenu(null);
  findWin.loadFile(path.join(__dirname, 'find.html'));
  findWin.once('ready-to-show', () => { findWin.show(); findWin.focus(); });
  findWin.on('closed', () => { findWin = null; });
}

function closeFindBar() {
  try { if (mainWin && !mainWin.isDestroyed()) mainWin.webContents.stopFindInPage('clearSelection'); } catch (e) {}
  if (findWin && !findWin.isDestroyed()) { try { findWin.close(); } catch (e) {} }
  findWin = null;
  try { if (mainWin && !mainWin.isDestroyed()) mainWin.focus(); } catch (e) {}
}

/** نوار با پنجره جابه‌جا و هم‌اندازه می‌ماند */
function syncFindBar() {
  if (!findWin || findWin.isDestroyed() || !mainWin || mainWin.isDestroyed()) return;
  try { findWin.setBounds(findBarBounds()); } catch (e) {}
}

ipcMain.on('find:query', (_e, { text, findNext, forward }) => {
  if (!mainWin || mainWin.isDestroyed() || !text) return;
  try { mainWin.webContents.findInPage(text, { findNext, forward, matchCase: false }); } catch (e) {}
});
ipcMain.on('find:stop', () => {
  try { if (mainWin && !mainWin.isDestroyed()) mainWin.webContents.stopFindInPage('clearSelection'); } catch (e) {}
});
ipcMain.on('find:close', () => closeFindBar());

function bindFind(win) {
  win.webContents.on('found-in-page', (_e, r) => {
    if (findWin && !findWin.isDestroyed()) {
      findWin.webContents.send('find:result', { matches: r.matches || 0, active: r.activeMatchOrdinal || 0 });
    }
    if (process.env.PUMP_BENCH) console.log('[یافتن] نتیجه:', r.activeMatchOrdinal + '/' + r.matches);
  });
  // صفحه که عوض شد، نوار بی‌معنا می‌شود
  win.webContents.on('did-start-navigation', () => { if (findWin) closeFindBar(); });
  ['move', 'resize', 'maximize', 'unmaximize'].forEach((ev) => win.on(ev, syncFindBar));
  win.on('closed', () => { if (findWin && !findWin.isDestroyed()) { try { findWin.destroy(); } catch (e) {} } findWin = null; });
}

function createMainWindow() {
  const saved = loadWinState();
  mainWin = new BrowserWindow({
    width: (saved && saved.bounds.width) || WIN_DEFAULT.width,
    height: (saved && saved.bounds.height) || WIN_DEFAULT.height,
    x: saved ? saved.bounds.x : undefined,
    y: saved ? saved.bounds.y : undefined,
    minWidth: 900, minHeight: 600,
    backgroundColor: '#0b0f17', title: 'پمپ یعقوبی',
    autoHideMenuBar: true,   // هیچ نوارِ منویی — نه در دید، نه با Alt
    icon: path.join(__dirname, 'build', 'icon.png'), show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true, nodeIntegration: false, spellcheck: false,
      /* پنجره وقتی پشتِ پنجرهٔ دیگری می‌رود هم با تمامِ سرعت کار کند —
         بدونِ این، برگشتن به برنامه چند لحظه «یخ‌زده» دیده می‌شود. */
      backgroundThrottling: false,
      /* کشِ کدِ کامپایل‌شدهٔ V8: بارِ دومِ اجرا، جاوااسکریپتِ برنامه از صفر
         کامپایل نمی‌شود. برای فایلی به این بزرگی، بیشترین اثر را روی
         «چند ثانیه تا بالا آمدن» دارد. */
      v8CacheOptions: 'bypassHeatCheckAndEagerCompile',
    },
  });

  stripMenu();
  bindShortcuts(mainWin);
  if (saved && saved.maximized) mainWin.maximize();
  if (saved && saved.fullScreen) mainWin.setFullScreen(true);
  bindWinState(mainWin);
  bindFind(mainWin);
  // اگر فایلِ ذخیره‌شده کهنه یا بیرونِ دید بود و نادیده گرفته شد، همین اول با
  // جای واقعیِ پنجره اصلاحش می‌کنیم — نه اینکه تا اولین جابه‌جاییِ کاربر غلط
  // بماند.
  // ⚠️ فقط در همین حالت. اگر حالتِ ذخیره‌شده معتبر بود این‌جا چیزی نمی‌نویسیم:
  //   maximize() روی X11/ویندوز آنی نیست و در همین لحظه isMaximized() هنوز
  //   false است — یک نوشتنِ زودهنگام «بیشینه بودن» را پاک می‌کرد (آزموده شد).
  if (!saved) rememberWin(mainWin);

  const usingUpdate = activeIndexPath() === updIndex();
  if (usingUpdate) { try { fs.writeFileSync(updFlagFile(), String(Date.now())); } catch (e) {} }

  // ── مرحله‌های واقعیِ خودِ موتورِ صفحه ──
  mainWin.webContents.on('dom-ready', () => bootMark(90, 'ساختنِ صفحه'));
  hardTimeoutTimer = setTimeout(() => bootFinish('مهلتِ کلی تمام شد'), HARD_TIMEOUT_MS);

  mainWin.loadURL(START_URL);

  mainWin.webContents.once('did-finish-load', () => {
    // نسخهٔ تازه سالم بالا آمد → نشانهٔ «امتحان‌نشده» برداشته می‌شود
    try { if (fs.existsSync(updFlagFile())) fs.unlinkSync(updFlagFile()); } catch (e) {}
    bootMark(95, 'آخرین آماده‌سازی');
    /* از این‌جا به بعد خودِ برنامه خبر می‌دهد (boot:step). اگر نسخهٔ بالا‌آمده
       آن‌قدر کهنه باشد که این خبرها را ندهد، بعد از این مهلت خودمان تمامش
       می‌کنیم — پنجره باز می‌شود و کاربر پشتِ اسپلش نمی‌ماند. */
    clearTimeout(readyFallbackTimer);
    readyFallbackTimer = setTimeout(() => bootFinish('نسخهٔ بالا‌آمده مرحله‌هایش را خبر نداد'), READY_FALLBACK_MS);
    scheduleUpdateChecks();
    /* راهِ آزمودنِ خودکارِ نوارِ جست‌وجو (فقط با PUMP_BENCH=find، وگرنه هرگز
       اجرا نمی‌شود): نوار را باز می‌کند و یک واژهٔ حتماً موجود را می‌جوید تا
       شمارشِ نتیجه در لاگ چاپ شود. tools/check-desktop-find.mjs همین را می‌سنجد. */
    if (process.env.PUMP_BENCH === 'find') {
      setTimeout(() => {
        try {
          openFindBar();
          setTimeout(() => { try { mainWin.webContents.findInPage('پمپ', {}); } catch (e) {} }, 900);
        } catch (e) { console.log('[یافتن] باز نشد:', e && e.message); }
      }, 1200);
    }
  });

  mainWin.webContents.on('did-fail-load', (_e, code, desc, url, isMainFrame) => {
    if (!isMainFrame) return;
    // اگر نسخهٔ دانلودشده بالا نیامد، خودکار به نسخهٔ همراهِ نصب برمی‌گردیم
    if (activeIndexPath() === updIndex()) {
      rollbackUpdate('بارگذاری نشد: ' + desc + ' (' + code + ')');
      if (mainWin) mainWin.loadURL(START_URL);
      return;
    }
    bootFinish('صفحه بار نشد: ' + desc);
  });

  // هیچ خطای رندری نباید پنجره را سفید و بی‌استفاده بگذارد
  mainWin.webContents.on('render-process-gone', (_e, details) => {
    console.error('[رندر] از کار افتاد:', details && details.reason);
    if (activeIndexPath() === updIndex() && fs.existsSync(updFlagFile())) rollbackUpdate('رندر از کار افتاد');
    if (mainWin && !mainWin.isDestroyed()) mainWin.reload();
  });

  mainWin.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:\/\//i.test(url)) { shell.openExternal(url); return { action: 'deny' }; }
    return { action: 'allow' };
  });

  mainWin.on('closed', () => { mainWin = null; });
}

// ---------------------------------------------------------------------------
//  چاپ و «ذخیرهٔ PDF» — کارِ خودِ برنامه، نه یک صفحهٔ وب
//  ---------------------------------------------------------------------------
//  باگِ واقعیِ نسخهٔ قبل: سندِ چاپ داخلِ یک <iframe> باز می‌شد و دکمهٔ چاپ
//  window.print()ِ همان iframe را صدا می‌زد. در Electron این فراخوانی رشتهٔ
//  اجرای صفحه را می‌بندد: از همان لحظه تا وقتی پنجرهٔ چاپِ ویندوز بسته نشود،
//  کلِ برنامه یخ می‌زند (آزموده شد — حتی ساده‌ترین کدِ بعدی هم اجرا نمی‌شد).
//  اگر چاپگری نصب نبود یا کاربر پنجره را نمی‌دید، تنها راهِ خلاصی بستنِ
//  برنامه بود.
//
//  حالا سند به خودِ پوسته سپرده می‌شود: در یک پنجرهٔ پنهان بار می‌شود و
//    • «چاپ» → پنجرهٔ چاپِ خودِ ویندوز، بدونِ قفل شدنِ برنامه
//    • «ذخیرهٔ PDF» → یک فایلِ پی‌دی‌افِ واقعی با پنجرهٔ ذخیرهٔ ویندوز
//  و خودِ برنامه در تمامِ این مدت زنده و قابلِ استفاده می‌ماند.
// ---------------------------------------------------------------------------
let docSeq = 0;

/** سند را در یک پنجرهٔ پنهان بار می‌کند. مسیرِ فایلِ موقت هم برگردانده می‌شود. */
async function loadDocWindow(html) {
  const file = path.join(os.tmpdir(), 'pump-doc-' + Date.now() + '-' + (++docSeq) + '.html');
  await fsp.writeFile(file, String(html || ''), 'utf8');
  const win = new BrowserWindow({
    show: false,
    webPreferences: { contextIsolation: true, nodeIntegration: false, sandbox: true, javascript: true },
  });
  const done = new Promise((resolve) => {
    win.webContents.once('did-finish-load', resolve);
    win.webContents.once('did-fail-load', resolve);
  });
  await win.loadFile(file).catch(() => {});
  await done;
  // یک نفس تا فونت و چیدمان بنشیند (وگرنه ورقِ اول گاهی خام چاپ می‌شود)
  await new Promise((r) => setTimeout(r, 250));
  return { win, file };
}

function cleanupDoc(win, file) {
  try { if (win && !win.isDestroyed()) win.destroy(); } catch (e) {}
  try { fs.unlinkSync(file); } catch (e) {}
}

async function printDocument(html) {
  const { win, file } = await loadDocWindow(html);
  return new Promise((resolve) => {
    let settled = false;
    const finish = (r) => { if (settled) return; settled = true; clearTimeout(guard); cleanupDoc(win, file); resolve(r); };
    /* اگر پنجرهٔ چاپِ ویندوز باز بماند و هیچ‌وقت جوابی برنگردد، پنجرهٔ پنهان
       تا بسته شدنِ برنامه می‌ماند. این نگهبان فقط جلوی همان را می‌گیرد؛ روی
       کارِ عادیِ چاپ اثری ندارد (کاربر خیلی زودتر از این تصمیم می‌گیرد). */
    const guard = setTimeout(() => finish({ ok: true, cancelled: true }), 10 * 60 * 1000);
    try {
      win.webContents.print({ silent: false, printBackground: true }, (ok, reason) => {
        if (ok) return finish({ ok: true });
        // «cancelled» یعنی خودِ کاربر بستش — خطا نیست
        if (/cancel/i.test(String(reason || ''))) return finish({ ok: true, cancelled: true });
        finish({ ok: false, error: String(reason || 'چاپ انجام نشد') });
      });
    } catch (e) { finish({ ok: false, error: String((e && e.message) || e) }); }
  });
}

async function savePdfDocument(html, name) {
  const safe = String(name || 'سند').replace(/[\\/:*?"<>|]/g, '-').slice(0, 80) || 'سند';
  const target = await dialog.showSaveDialog(mainWin && !mainWin.isDestroyed() ? mainWin : undefined, {
    title: 'ذخیرهٔ PDF',
    defaultPath: path.join(app.getPath('downloads'), safe + '.pdf'),
    filters: [{ name: 'PDF', extensions: ['pdf'] }],
  });
  if (target.canceled || !target.filePath) return { ok: true, cancelled: true };
  const { win, file } = await loadDocWindow(html);
  try {
    const buf = await win.webContents.printToPDF({
      printBackground: true, pageSize: 'A4', margins: { marginType: 'default' },
    });
    await fsp.writeFile(target.filePath, buf);
    return { ok: true, path: target.filePath };
  } catch (e) {
    return { ok: false, error: String((e && e.message) || e) };
  } finally {
    cleanupDoc(win, file);
  }
}

// بررسیِ خودکار: ۸ ثانیه بعد از باز شدن، سپس هر ۶ ساعت. فقط خبر می‌دهد؛
// دانلود و سوار کردن با تصمیمِ خودِ کاربر انجام می‌شود.
function scheduleUpdateChecks() {
  if (updateTimer) return;
  const run = async () => {
    try {
      const r = await checkUpdate();
      if (!r.ok || !r.hasUpdate) return;
      /* خودش می‌گیردش. سوار کردنش با خودِ کاربر است (دکمهٔ «الان سوار کن»)، ولی
         حتی اگر هیچ‌وقت آن دکمه را نزند، دفعهٔ بعد که برنامه را باز کند نسخهٔ
         تازه بالا می‌آید — چون activeIndexPath همیشه تازه‌ترینِ سالم را
         برمی‌دارد و اگر خراب باشد خودش به نسخهٔ همراهِ نصب برمی‌گردد.
         این همان چیزی است که نگذاشت نسخهٔ نصب‌شده یک ماه عقب بماند. */
      const d = await downloadUpdate().catch(() => ({ ok: false }));
      if (mainWin && !mainWin.isDestroyed()) {
        mainWin.webContents.send('update:available', Object.assign({}, r, { downloaded: !!(d && d.downloaded) }));
      }
    } catch (e) {}
  };
  setTimeout(run, 8000);
  updateTimer = setInterval(run, 6 * 3600 * 1000);
}

// ---------------------------------------------------------------------------
//  سرعت — چیزهایی که فقط «برنامهٔ نصب‌شده» می‌تواند داشته باشد
// ---------------------------------------------------------------------------
/*  خواستهٔ صاحب ریپو: «در مرورگر هر محدودیتی هست باشد، ولی در برنامهٔ ویندوز یک
    ذره هم محدودیت نبینم.»

    اینجا موتورِ کروم مالِ خودِ ماست، پس می‌شود چیزهایی را روشن کرد که مرورگر
    برای صدها تبِ ناشناس نمی‌تواند:
      • بلاک‌لیستِ کارتِ گرافیک: کروم روی خیلی از درایورها شتاب‌دهنده را خودش
        خاموش می‌کند (چون نمی‌داند کدام سایت چه می‌کشد). این‌جا فقط یک برنامه
        هست و خودمان می‌دانیم چه می‌کشیم.
      • کندکردنِ پس‌زمینه: مرورگر تایمرهای تبِ پنهان را کُند و رندر را متوقف
        می‌کند تا باتریِ بقیهٔ تب‌ها برود. برنامهٔ ما تنها است — این کند شدن
        فقط یعنی «برگشتم و صفحه هنوز خواب بود».
      • حافظهٔ جاوااسکریپت: سهمِ پیش‌فرضِ هر تب حدود ۴ گیگ نیست؛ این‌جا صریح
        بالا برده می‌شود تا دفترهای چندین‌سالهٔ بزرگ جا شوند.
      • کشِ کدِ کامپایل‌شده: فایلِ برنامه ۵۰ هزار خط جاوااسکریپت دارد. با کشِ
        کد، اجرای دومِ برنامه دیگر آن را از صفر کامپایل نمی‌کند.
    ⚠️ همهٔ این‌ها باید *پیش از* app.whenReady گذاشته شوند، وگرنه دیر است. */
[
  ['ignore-gpu-blocklist', ''],                 // شتاب‌دهنده روی درایورهای قدیمی هم روشن
  ['enable-gpu-rasterization', ''],             // کشیدنِ صفحه با کارت گرافیک، نه با CPU
  ['enable-zero-copy', ''],                     // بدونِ کپیِ اضافه بینِ CPU و GPU
  ['disable-background-timer-throttling', ''],  // تایمرها در پس‌زمینه کُند نشوند
  ['disable-renderer-backgrounding', ''],       // پنجرهٔ پشت هم با تمامِ توان کار کند
  ['disable-backgrounding-occluded-windows', ''],
  ['disable-features', 'IntensiveWakeUpThrottling,ExpensiveBackgroundTimerThrottling'],
  ['js-flags', '--max-old-space-size=4096'],    // تا ۴ گیگ حافظهٔ جاوااسکریپت
].forEach(([k, v]) => { try { v ? app.commandLine.appendSwitch(k, v) : app.commandLine.appendSwitch(k); } catch (e) {} });

// ---------------------------------------------------------------------------
//  راه‌اندازی
// ---------------------------------------------------------------------------
protocol.registerSchemesAsPrivileged([
  { scheme: SCHEME, privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } },
]);

const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWin) { if (mainWin.isMinimized()) mainWin.restore(); mainWin.focus(); }
  });
}

app.whenReady().then(() => {
  // اگر آخرین اجرا با نسخهٔ دانلودشده ناتمام ماند (کرش/برق رفت)، برگرد به نسخهٔ سالم
  try { if (fs.existsSync(updFlagFile())) rollbackUpdate('اجرای قبلی کامل نشد'); } catch (e) {}

  protocol.handle(SCHEME, (request) => {
    const url = new URL(request.url);
    let rel = decodeURIComponent(url.pathname);
    if (rel === '/' || rel === '') rel = '/index.html';
    // خودِ برنامه از نسخهٔ آپدیت‌شده (اگر باشد) سرو می‌شود؛ بقیهٔ فایل‌ها از نصب
    let filePath;
    if (rel === '/index.html') filePath = activeIndexPath();
    else {
      const upd = path.normalize(path.join(updDir(), rel));
      filePath = (upd.startsWith(updDir()) && fs.existsSync(upd)) ? upd : path.normalize(path.join(APP_DIR, rel));
    }
    if (!filePath.startsWith(APP_DIR) && !filePath.startsWith(updDir())) return new Response('403 Forbidden', { status: 403 });
    // خودِ فایلِ برنامه، تنها بارِ اول: تکه‌تکه خوانده می‌شود تا نوارِ لودینگ
    // واقعاً «بایت‌های خوانده‌شده» را نشان بدهد، نه یک انیمیشنِ تزئینی.
    if (rel === '/index.html' && !bootDone) {
      const streamed = streamAppFile(filePath);
      if (streamed) return streamed;
    }
    return net.fetch(pathToFileURL(filePath).toString());
  });

  // مایکروفون/دوربین/اعلان برای خودِ برنامه: بدون این، دکمهٔ ضبطِ صدا و
  // جستجوی صوتی در نسخهٔ کامپیوتری هیچ کاری نمی‌کردند.
  const allow = new Set(['media', 'audioCapture', 'videoCapture', 'notifications', 'clipboard-sanitized-write', 'clipboard-read', 'fullscreen']);
  session.defaultSession.setPermissionRequestHandler((wc, permission, callback) => {
    callback(allow.has(permission));
  });
  session.defaultSession.setPermissionCheckHandler((wc, permission) => allow.has(permission));

  ipcMain.handle('update:info', () => ({
    current: activeVersion(), bundled: bundledVersion(),
    usingUpdate: activeIndexPath() === updIndex(), bases: updateBases(),
  }));
  ipcMain.handle('update:check', async () => { try { return await checkUpdate(); } catch (e) { return { ok: false, error: String(e && e.message || e) }; } });
  ipcMain.handle('update:download', async () => { try { return await downloadUpdate(); } catch (e) { return { ok: false, error: String(e && e.message || e) }; } });
  ipcMain.handle('update:apply', () => {
    if (mainWin && !mainWin.isDestroyed()) { mainWin.loadURL(START_URL); return { ok: true }; }
    return { ok: false };
  });
  ipcMain.handle('update:rollback', () => { rollbackUpdate('درخواستِ کاربر'); if (mainWin) mainWin.loadURL(START_URL); return { ok: true }; });
  ipcMain.handle('update:set-base', (_e, base) => {
    try {
      if (base && !/^https?:\/\//i.test(String(base))) return { ok: false, error: 'آدرس باید با http شروع شود' };
      fs.writeFileSync(updCfgFile(), JSON.stringify({ base: base || '' }), 'utf8');
      return { ok: true, bases: updateBases() };
    } catch (e) { return { ok: false, error: String(e && e.message || e) }; }
  });
  ipcMain.handle('shell:open', (_e, url) => {
    if (/^https?:\/\//i.test(String(url || ''))) { shell.openExternal(url); return { ok: true }; }
    return { ok: false };
  });
  ipcMain.handle('shell:reveal', (_e, p) => {
    try { shell.showItemInFolder(String(p || '')); return { ok: true }; } catch (e) { return { ok: false }; }
  });
  ipcMain.handle('doc:print', async (_e, a) => {
    try { return await printDocument(a && a.html); }
    catch (e) { return { ok: false, error: String((e && e.message) || e) }; }
  });
  ipcMain.handle('doc:pdf', async (_e, a) => {
    try { return await savePdfDocument(a && a.html, a && a.name); }
    catch (e) { return { ok: false, error: String((e && e.message) || e) }; }
  });

  /* دانلودِ فایل از داخلِ برنامه (بکاپ، اکسل، …): پنجرهٔ ذخیرهٔ ویندوز با نامِ
     درست و پوشهٔ Downloads به‌عنوان پیش‌فرض باز می‌شود، و وقتی فایل نشست خودِ
     برنامه پیامش را نشان می‌دهد. پیش از این هیچ خبری از سرانجامِ دانلود
     نمی‌رسید و کاربر نمی‌دانست فایل کجا رفت. */
  session.defaultSession.on('will-download', (_ev, item) => {
    try {
      item.setSaveDialogOptions({
        title: 'ذخیرهٔ فایل',
        defaultPath: path.join(app.getPath('downloads'), item.getFilename()),
      });
    } catch (e) {}
    item.once('done', (_e2, state) => {
      if (!mainWin || mainWin.isDestroyed()) return;
      if (state !== 'completed') return;              // لغوِ کاربر → بی‌صدا
      try {
        mainWin.webContents.send('file:saved', { name: item.getFilename(), path: item.getSavePath() });
      } catch (e) {}
    });
  });

  createSplash();
  bootMark(3, 'آماده‌سازیِ پوسته');
  createMainWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) { createSplash(); createMainWindow(); }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

// هیچ خطای پیش‌بینی‌نشده‌ای نباید برنامه را ببندد
process.on('uncaughtException', (err) => { console.error('[خطای برنامه]', err && err.stack || err); });
process.on('unhandledRejection', (r) => { console.error('[promise رهاشده]', r && r.stack || r); });
