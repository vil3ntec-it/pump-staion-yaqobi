// ============================================================================
//  WebVault Manager — پوستهٔ دسکتاپ (Electron)
//  این پوسته به «سرور خانگی» شما وصل می‌شود (همان‌طور که در تصویر معماری آمده).
//  بار اول آدرس سرور را می‌پرسد و ذخیره می‌کند؛ دفعات بعد مستقیم وصل می‌شود.
// ============================================================================
const { app, BrowserWindow, ipcMain, Menu } = require('electron');
const path = require('node:path');
const fs = require('node:fs');

const CONFIG_PATH = path.join(app.getPath('userData'), 'webvault-config.json');

function loadConfig() {
  try { return JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8')); } catch { return {}; }
}
function saveConfig(cfg) {
  fs.writeFileSync(CONFIG_PATH, JSON.stringify(cfg, null, 2));
}

let win;

function createWindow() {
  win = new BrowserWindow({
    width: 1360,
    height: 900,
    minWidth: 900,
    minHeight: 620,
    backgroundColor: '#0b0f17',
    title: 'WebVault Manager',
    icon: path.join(__dirname, 'icon.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });
  Menu.setApplicationMenu(null);

  const cfg = loadConfig();
  if (cfg.serverUrl) win.loadURL(cfg.serverUrl);
  else win.loadFile(path.join(__dirname, 'connect.html'));
}

// از صفحهٔ اتصال: ذخیرهٔ آدرس سرور و رفتن به برنامه
ipcMain.handle('set-server', (_e, url) => {
  const clean = String(url).trim().replace(/\/+$/, '');
  saveConfig({ serverUrl: clean });
  win.loadURL(clean);
  return true;
});
ipcMain.handle('get-server', () => loadConfig().serverUrl || '');
ipcMain.handle('reset-server', () => {
  saveConfig({});
  win.loadFile(path.join(__dirname, 'connect.html'));
  return true;
});

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
