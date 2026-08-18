// ============================================================================
//  پمپ یعقوبی — برنامهٔ کامپیوتری (پوستهٔ Electron)
//  ---------------------------------------------------------------------------
//  • خودِ برنامه (index.html) را به‌صورت محلی و کاملاً آفلاین اجرا می‌کند.
//  • برنامه از یک «پروتکل امنِ اختصاصی» (app://) سرو می‌شود، نه از file://،
//    تا Chromium ذخیره‌سازی IndexedDB/localStorage را مثل یک سایتِ واقعیِ امن
//    پایدار نگه دارد و سهمیهٔ ذخیره‌سازی بر اساس فضای دیسک باشد، نه سقفِ تب.
//  • یک صفحهٔ لودینگ (اسپلش) هنگام باز شدن نمایش داده می‌شود (مثل اکسل).
//  • آپدیتِ داخلِ برنامه: نسخهٔ تازهٔ سایت را خودش می‌گیرد و با یک دکمه سوار
//    می‌کند — دیگر برای هر تغییرِ کوچک نباید برنامه را از نو نصب کرد.
// ============================================================================
const { app, BrowserWindow, protocol, net, Menu, shell, session, ipcMain, dialog } = require('electron');
const path = require('node:path');
const fs = require('node:fs');
const fsp = require('node:fs/promises');
const { pathToFileURL } = require('node:url');

const APP_DIR = path.join(__dirname, 'app');          // نسخهٔ همراهِ نصب‌کننده
const SCHEME = 'app';
const START_URL = 'app://local/index.html';
const MIN_SPLASH_MS = 1400;

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
//  پنجره‌ها
// ---------------------------------------------------------------------------
function createSplash() {
  splashWin = new BrowserWindow({
    width: 460, height: 320, frame: false, resizable: false, center: true,
    alwaysOnTop: true, backgroundColor: '#0b0f17', show: true,
    webPreferences: { contextIsolation: true },
  });
  splashWin.loadFile(path.join(__dirname, 'splash.html'));
  splashWin.on('closed', () => { splashWin = null; });
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
    if (!mod || input.alt) return;

    const run = (fn) => { fn(); event.preventDefault(); };
    // این‌ها در برنامه میانبرِ دیگری ندارند → همیشه دستِ پوسته
    if (key === 'c') return run(() => wc.copy());
    if (key === 'v') return run(() => (input.shift ? wc.pasteAndMatchStyle() : wc.paste()));
    if (key === 'a') return run(() => wc.selectAll());
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

function createMainWindow() {
  mainWin = new BrowserWindow({
    width: 1360, height: 900, minWidth: 900, minHeight: 600,
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

  const startedAt = Date.now();
  const usingUpdate = activeIndexPath() === updIndex();
  if (usingUpdate) { try { fs.writeFileSync(updFlagFile(), String(Date.now())); } catch (e) {} }
  mainWin.loadURL(START_URL);

  mainWin.webContents.once('did-finish-load', () => {
    // نسخهٔ تازه سالم بالا آمد → نشانهٔ «امتحان‌نشده» برداشته می‌شود
    try { if (fs.existsSync(updFlagFile())) fs.unlinkSync(updFlagFile()); } catch (e) {}
    const wait = Math.max(0, MIN_SPLASH_MS - (Date.now() - startedAt));
    setTimeout(() => {
      if (splashWin) splashWin.close();
      if (mainWin) { mainWin.show(); mainWin.focus(); }
    }, wait);
    scheduleUpdateChecks();
  });

  mainWin.webContents.on('did-fail-load', (_e, code, desc, url, isMainFrame) => {
    if (!isMainFrame) return;
    // اگر نسخهٔ دانلودشده بالا نیامد، خودکار به نسخهٔ همراهِ نصب برمی‌گردیم
    if (activeIndexPath() === updIndex()) {
      rollbackUpdate('بارگذاری نشد: ' + desc + ' (' + code + ')');
      if (mainWin) mainWin.loadURL(START_URL);
      return;
    }
    if (splashWin) splashWin.close();
    if (mainWin) mainWin.show();
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

// بررسیِ خودکار: ۸ ثانیه بعد از باز شدن، سپس هر ۶ ساعت. فقط خبر می‌دهد؛
// دانلود و سوار کردن با تصمیمِ خودِ کاربر انجام می‌شود.
function scheduleUpdateChecks() {
  if (updateTimer) return;
  const run = async () => {
    try {
      const r = await checkUpdate();
      if (r.ok && r.hasUpdate && mainWin && !mainWin.isDestroyed()) mainWin.webContents.send('update:available', r);
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

  createSplash();
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
