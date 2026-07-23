// ============================================================================
//  پمپ یعقوبی — برنامهٔ کامپیوتری (پوستهٔ Electron)
//  ---------------------------------------------------------------------------
//  • خودِ برنامه (index.html) را به‌صورت محلی و کاملاً آفلاین اجرا می‌کند.
//  • برنامه از یک «پروتکل امنِ اختصاصی» (app://) سرو می‌شود، نه از file://،
//    تا Chromium ذخیره‌سازی IndexedDB/localStorage را مثل یک سایتِ واقعیِ امن
//    پایدار نگه دارد و سهمیهٔ ذخیره‌سازی بر اساس فضای دیسک باشد، نه سقفِ تب.
//  • یک صفحهٔ لودینگ (اسپلش) هنگام باز شدن نمایش داده می‌شود (مثل اکسل).
//  • «سرور خانگی» داخل خودِ برنامه جاسازی شده: با یک کلیک، این کامپیوتر تبدیل
//    به سرور می‌شود و آدرس + رمز را می‌دهد تا در بقیهٔ دستگاه‌ها بگذاری —
//    بدون نیاز به zip و فایل bat جداگانه.
// ============================================================================
const { app, BrowserWindow, protocol, net, Menu, shell, ipcMain, clipboard } = require('electron');
const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');
const crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const { pathToFileURL } = require('node:url');

const APP_DIR = path.join(__dirname, 'app');
const SRV_DIR = path.join(__dirname, 'srv');
const SRV_ENTRY = path.join(SRV_DIR, 'server.js');
const SRV_PORT = 8787;
const SCHEME = 'app';
const START_URL = 'app://local/index.html';
const MIN_SPLASH_MS = 1400;

const CFG_PATH = path.join(app.getPath('userData'), 'desktop-config.json');
const SRV_DATA = path.join(app.getPath('userData'), 'server-data');

// پروتکل app:// باید پیش از app.ready به‌عنوان «امن و استاندارد» ثبت شود.
protocol.registerSchemesAsPrivileged([
  { scheme: SCHEME, privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } },
]);

let mainWin = null;
let splashWin = null;
let panelWin = null;
let serverProc = null;

// ---------------------------------------------------------------------------
//  تنظیمات ماندگار (نقشِ سرور و رمزِ ثابتِ سرور)
// ---------------------------------------------------------------------------
function loadCfg() { try { return JSON.parse(fs.readFileSync(CFG_PATH, 'utf8')) || {}; } catch (e) { return {}; } }
function saveCfg(c) { try { fs.writeFileSync(CFG_PATH, JSON.stringify(c, null, 2)); } catch (e) {} }

function getServerToken() {
  const cfg = loadCfg();
  if (cfg.serverToken) return cfg.serverToken;
  const tok = crypto.randomBytes(24).toString('hex');
  cfg.serverToken = tok; saveCfg(cfg);
  return tok;
}

// ---------------------------------------------------------------------------
//  مدیریتِ سرورِ خانگیِ جاسازی‌شده
// ---------------------------------------------------------------------------
function lanIPs() {
  const out = [];
  const ifs = os.networkInterfaces();
  for (const name in ifs) {
    for (const ni of (ifs[name] || [])) {
      if (ni && ni.family === 'IPv4' && !ni.internal) out.push(ni.address);
    }
  }
  return out;
}

function serverInfo() {
  return { running: !!serverProc, port: SRV_PORT, token: getServerToken(), ips: lanIPs(), dataDir: SRV_DATA };
}

function startServer() {
  if (serverProc) return serverInfo();
  try { fs.mkdirSync(SRV_DATA, { recursive: true }); } catch (e) {}
  const token = getServerToken();
  try {
    serverProc = spawn(process.execPath, [SRV_ENTRY], {
      env: Object.assign({}, process.env, {
        ELECTRON_RUN_AS_NODE: '1',
        PORT: String(SRV_PORT),
        DATA_DIR: SRV_DATA,
        AUTH_TOKEN: token,
      }),
      stdio: 'ignore',
      windowsHide: true,
    });
  } catch (e) { serverProc = null; return serverInfo(); }
  serverProc.on('exit', () => { serverProc = null; });
  serverProc.on('error', () => { serverProc = null; });
  const cfg = loadCfg(); cfg.beServer = true; saveCfg(cfg);
  // این کامپیوتر خودش را به سرورِ خودش وصل می‌کند تا داده‌اش هم روی سرور برود
  autoConnectHostApp(token);
  return serverInfo();
}

function stopServer() {
  if (serverProc) { try { serverProc.kill(); } catch (e) {} serverProc = null; }
  const cfg = loadCfg(); cfg.beServer = false; saveCfg(cfg);
  return serverInfo();
}

// برنامهٔ همین کامپیوتر را (اگر لازم بود) به سرورِ محلیِ خودش وصل می‌کند
function autoConnectHostApp(token) {
  if (!mainWin) return;
  const url = 'ws://localhost:' + SRV_PORT;
  const js = 'try{' +
    'if(localStorage.getItem("pumpServerUrl")!==' + JSON.stringify(url) + '){' +
    'localStorage.setItem("pumpServerUrl",' + JSON.stringify(url) + ');' +
    'localStorage.setItem("pumpServerToken",' + JSON.stringify(token) + ');' +
    'location.reload();' +
    '}}catch(e){}';
  mainWin.webContents.executeJavaScript(js).catch(() => {});
}

// ---------------------------------------------------------------------------
//  IPC برای پنلِ سرور
// ---------------------------------------------------------------------------
ipcMain.handle('server:start', () => startServer());
ipcMain.handle('server:stop', () => stopServer());
ipcMain.handle('server:info', () => serverInfo());
ipcMain.handle('server:copy', (_e, text) => { try { clipboard.writeText(String(text == null ? '' : text)); } catch (e) {} return true; });

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

function openServerPanel() {
  if (panelWin) { panelWin.focus(); return; }
  panelWin = new BrowserWindow({
    width: 540, height: 680, title: 'سرور خانگی', backgroundColor: '#0b0f17',
    parent: mainWin || undefined, minimizable: true, maximizable: false,
    webPreferences: { preload: path.join(__dirname, 'preload-server.js'), contextIsolation: true },
  });
  panelWin.setMenuBarVisibility(false);
  panelWin.loadFile(path.join(__dirname, 'server-panel.html'));
  panelWin.on('closed', () => { panelWin = null; });
}

function buildMenu() {
  const template = [
    { label: 'سرور خانگی', submenu: [
      { label: '🖥️  باز کردن پنل سرور', click: openServerPanel },
      { type: 'separator' },
      { label: 'بارگذاری مجدد', role: 'reload' },
      { label: 'تمام‌صفحه', role: 'togglefullscreen' },
      { type: 'separator' },
      { label: 'خروج', role: 'quit' },
    ] },
  ];
  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

function createMainWindow() {
  mainWin = new BrowserWindow({
    width: 1360, height: 900, minWidth: 900, minHeight: 600,
    backgroundColor: '#0b0f17', title: 'پمپ یعقوبی',
    icon: path.join(__dirname, 'build', 'icon.png'), show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true, nodeIntegration: false, spellcheck: false,
    },
  });

  buildMenu();

  const startedAt = Date.now();
  mainWin.loadURL(START_URL);

  mainWin.webContents.once('did-finish-load', () => {
    const wait = Math.max(0, MIN_SPLASH_MS - (Date.now() - startedAt));
    setTimeout(() => {
      if (splashWin) splashWin.close();
      if (mainWin) { mainWin.show(); mainWin.focus(); }
      // اگر قبلاً این کامپیوتر به‌عنوان سرور انتخاب شده بود، خودکار روشن شود
      try { if (loadCfg().beServer) startServer(); } catch (e) {}
    }, wait);
  });

  mainWin.webContents.on('did-fail-load', () => {
    if (splashWin) splashWin.close();
    if (mainWin) mainWin.show();
  });

  mainWin.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:\/\//i.test(url)) { shell.openExternal(url); return { action: 'deny' }; }
    return { action: 'allow' };
  });

  mainWin.on('closed', () => { mainWin = null; });
}

app.whenReady().then(() => {
  protocol.handle(SCHEME, (request) => {
    const url = new URL(request.url);
    let rel = decodeURIComponent(url.pathname);
    if (rel === '/' || rel === '') rel = '/index.html';
    const filePath = path.normalize(path.join(APP_DIR, rel));
    if (!filePath.startsWith(APP_DIR)) return new Response('403 Forbidden', { status: 403 });
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

app.on('before-quit', () => { if (serverProc) { try { serverProc.kill(); } catch (e) {} } });

const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWin) { if (mainWin.isMinimized()) mainWin.restore(); mainWin.focus(); }
  });
}
