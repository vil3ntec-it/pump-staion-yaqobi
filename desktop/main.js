// ============================================================================
//  پمپ یعقوبی — برنامهٔ کامپیوتری (پوستهٔ Electron)
//  ---------------------------------------------------------------------------
//  • خودِ برنامه (index.html) را به‌صورت محلی و کاملاً آفلاین اجرا می‌کند.
//  • برنامه از یک «پروتکل امنِ اختصاصی» (app://) سرو می‌شود، نه از file://،
//    تا Chromium ذخیره‌سازی IndexedDB/localStorage را مثل یک سایتِ واقعیِ امن
//    پایدار نگه دارد (روی file:// این ذخیره‌سازی‌ها گاهی پاک می‌شوند). این همان
//    چیزی است که «بدون محدودیت حجم و بدون کندی» را روی کامپیوتر ممکن می‌کند:
//    سهمیهٔ IndexedDB روی دسکتاپ بر اساس فضای دیسک است، نه سقفِ ۵ مگابایتیِ تب.
//  • یک صفحهٔ لودینگ (اسپلش) هنگام باز شدن نمایش داده می‌شود (مثل اکسل).
// ============================================================================
const { app, BrowserWindow, protocol, net, Menu, shell } = require('electron');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

const APP_DIR = path.join(__dirname, 'app');
const SCHEME = 'app';
const START_URL = 'app://local/index.html';
const MIN_SPLASH_MS = 1400; // حداقل زمان نمایش اسپلش تا حس «در حال آماده‌سازی» بدهد

// پروتکل app:// باید پیش از app.ready به‌عنوان «امن و استاندارد» ثبت شود تا
// APIهای ذخیره‌سازی (IndexedDB/localStorage/Cache) روی آن فعال باشند.
protocol.registerSchemesAsPrivileged([
  { scheme: SCHEME, privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } },
]);

let mainWin = null;
let splashWin = null;

function createSplash() {
  splashWin = new BrowserWindow({
    width: 460,
    height: 320,
    frame: false,
    transparent: false,
    resizable: false,
    center: true,
    alwaysOnTop: true,
    backgroundColor: '#0b0f17',
    show: true,
    webPreferences: { contextIsolation: true },
  });
  splashWin.loadFile(path.join(__dirname, 'splash.html'));
  splashWin.on('closed', () => { splashWin = null; });
}

function createMainWindow() {
  mainWin = new BrowserWindow({
    width: 1360,
    height: 900,
    minWidth: 900,
    minHeight: 600,
    backgroundColor: '#0b0f17',
    title: 'پمپ یعقوبی',
    icon: path.join(__dirname, 'build', 'icon.png'),
    show: false, // تا آماده شدن کامل مخفی می‌ماند؛ اسپلش جایش را می‌گیرد
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      spellcheck: false,
    },
  });

  // منوی پیش‌فرض حذف می‌شود تا مثل یک برنامهٔ واقعی دیده شود (نه یک مرورگر)
  Menu.setApplicationMenu(null);

  const startedAt = Date.now();
  mainWin.loadURL(START_URL);

  // وقتی صفحه کامل بارگذاری شد، حداقل‌زمانِ اسپلش را کامل می‌کنیم، بعد نمایش می‌دهیم
  mainWin.webContents.once('did-finish-load', () => {
    const wait = Math.max(0, MIN_SPLASH_MS - (Date.now() - startedAt));
    setTimeout(() => {
      if (splashWin) splashWin.close();
      if (mainWin) { mainWin.show(); mainWin.focus(); }
    }, wait);
  });

  // اگر بارگذاری شکست خورد، باز هم اسپلش را ببند و پنجره را نشان بده تا کاربر گیر نکند
  mainWin.webContents.on('did-fail-load', () => {
    if (splashWin) splashWin.close();
    if (mainWin) mainWin.show();
  });

  // لینک‌های بیرونی در مرورگر پیش‌فرض سیستم باز شوند، نه داخل برنامه
  mainWin.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:\/\//i.test(url)) { shell.openExternal(url); return { action: 'deny' }; }
    return { action: 'allow' };
  });

  mainWin.on('closed', () => { mainWin = null; });
}

app.whenReady().then(() => {
  // سرو کردنِ فایل‌های برنامه از پوشهٔ app با پروتکل امنِ app://
  protocol.handle(SCHEME, (request) => {
    const url = new URL(request.url);
    let rel = decodeURIComponent(url.pathname);
    if (rel === '/' || rel === '') rel = '/index.html';
    const filePath = path.normalize(path.join(APP_DIR, rel));
    // جلوگیری از خروج از پوشهٔ app (path traversal)
    if (!filePath.startsWith(APP_DIR)) {
      return new Response('403 Forbidden', { status: 403 });
    }
    return net.fetch(pathToFileURL(filePath).toString());
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

// فقط یک نمونهٔ برنامه اجرا شود (اگر کاربر دوباره باز کرد، همان پنجره جلو بیاید)
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWin) { if (mainWin.isMinimized()) mainWin.restore(); mainWin.focus(); }
  });
}
