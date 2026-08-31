// پلِ کوچکِ نوارِ جست‌وجو. هیچ دسترسیِ اضافه‌ای نمی‌دهد: فقط سه پیام به بالا و
// دو خبر به پایین. (contextIsolation روشن است، پس صفحه به Node نمی‌رسد.)
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('pumpFind', {
  query: (text, findNext, forward) => ipcRenderer.send('find:query', { text: String(text || ''), findNext: !!findNext, forward: forward !== false }),
  stop: () => ipcRenderer.send('find:stop'),
  close: () => ipcRenderer.send('find:close'),
  onResult: (fn) => ipcRenderer.on('find:result', (_e, r) => { try { fn(r); } catch (e) {} }),
  onFocus: (fn) => ipcRenderer.on('find:focus', () => { try { fn(); } catch (e) {} }),
});
