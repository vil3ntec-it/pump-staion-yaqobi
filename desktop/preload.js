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

  // ── مرحله‌های واقعیِ بالا آمدن ──
  // خودِ برنامه این‌ها را در همان لحظه‌ای صدا می‌زند که کار واقعاً تمام شده
  // (دفترها خوانده شد، جدول‌ها چیده شد، آمادهٔ کار است) و نوارِ لودینگ دقیقاً
  // همان را نشان می‌دهد. اگر صدا زده نشود، پوسته خودش بعد از مهلتی تمامش می‌کند.
  boot: (step) => { try { ipcRenderer.send('boot:step', String(step || '')); } catch (e) {} },

  // ── چاپ و ذخیرهٔ پی‌دی‌اف (کارِ خودِ پوسته، نه صفحه) ──
  // window.print()‎ی که از داخلِ صفحه صدا زده شود، رشتهٔ اجرای خودِ برنامه را
  // قفل می‌کند و تا بسته شدنِ پنجرهٔ چاپ همه‌چیز یخ می‌زند. این دو، سند را به
  // فرایندِ اصلی می‌سپارند: برنامه یک لحظه هم از کار نمی‌افتد.
  printDoc: (html, title) => ipcRenderer.invoke('doc:print', { html: String(html || ''), title: String(title || '') }),
  savePdf:  (html, name)  => ipcRenderer.invoke('doc:pdf',   { html: String(html || ''), name: String(name || '') }),

  // فایلی که برنامه ذخیره کرد → پوسته خبر می‌دهد تا خودِ برنامه پیام بدهد
  onFileSaved: (cb) => {
    if (typeof cb !== 'function') return;
    ipcRenderer.on('file:saved', (_e, info) => { try { cb(info); } catch (e) {} });
  },
  revealFile: (p) => ipcRenderer.invoke('shell:reveal', p),
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
