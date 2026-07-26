// پلِ باریک بینِ پوستهٔ دسکتاپ و خودِ برنامه. فقط همین چند کارِ مشخص از بیرون
// در دسترس است (contextIsolation روشن است، پس صفحه به Node دسترسی ندارد).
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('pumpDesktop', {
  isDesktop: true,
  platform: process.platform,

  // ── آپدیتِ داخلِ برنامه ──
  info:        () => ipcRenderer.invoke('update:info'),
  check:       () => ipcRenderer.invoke('update:check'),
  download:    () => ipcRenderer.invoke('update:download'),
  apply:       () => ipcRenderer.invoke('update:apply'),
  rollback:    () => ipcRenderer.invoke('update:rollback'),
  setBase:  (b) => ipcRenderer.invoke('update:set-base', b),

  // پوستهٔ برنامه خودش خبر می‌دهد که نسخهٔ تازه‌ای هست
  onUpdateAvailable: (cb) => {
    if (typeof cb !== 'function') return;
    ipcRenderer.on('update:available', (_e, info) => { try { cb(info); } catch (e) {} });
  },
  onOpenPanel: (cb) => {
    if (typeof cb !== 'function') return;
    ipcRenderer.on('update:open-panel', () => { try { cb(); } catch (e) {} });
  },

  openExternal: (url) => ipcRenderer.invoke('shell:open', url),
});
