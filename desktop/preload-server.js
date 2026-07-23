// پلِ امنِ پنجرهٔ «پنل سرور» با پردازهٔ اصلی (contextIsolation روشن)
const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('pumpServer', {
  start: () => ipcRenderer.invoke('server:start'),
  stop: () => ipcRenderer.invoke('server:stop'),
  info: () => ipcRenderer.invoke('server:info'),
  copy: (text) => ipcRenderer.invoke('server:copy', text),
});
