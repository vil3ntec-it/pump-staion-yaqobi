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

// ── فوکوسِ کادرهای متنی ──────────────────────────────────────────────────────
// نوارِ منو برداشته شده، پس پوسته خودش Ctrl+C/V/X/Z را می‌گیرد. ولی خودِ برنامه
// هم بیرونِ کادرهای متنی از Ctrl+Z و Ctrl+X برای «واگرد/ازنوِ کلِ دفتر» استفاده
// می‌کند. این خبر می‌دهد که همین حالا فوکوس داخل کادرِ متنی هست یا نه، تا پوسته
// فقط همان‌جا دخالت کند و بیرونِ کادر، کلید دست‌نخورده به برنامه برسد.
function isEditable(el) {
  if (!el) return false;
  const tag = el.tagName;
  if (tag === 'TEXTAREA') return !el.readOnly && !el.disabled;
  if (tag === 'INPUT') return !el.readOnly && !el.disabled;
  return Boolean(el.isContentEditable);
}
let lastEditable = null;
function reportFocus() {
  const now = isEditable(document.activeElement);
  if (now === lastEditable) return;
  lastEditable = now;
  try { ipcRenderer.send('focus:editable', now); } catch (e) {}
}
window.addEventListener('focusin', reportFocus, true);
window.addEventListener('focusout', () => setTimeout(reportFocus, 0), true);
window.addEventListener('DOMContentLoaded', reportFocus);
