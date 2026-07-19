// پل امن بین صفحهٔ اتصال و فرایند اصلی Electron
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('webvault', {
  setServer: (url) => ipcRenderer.invoke('set-server', url),
  getServer: () => ipcRenderer.invoke('get-server'),
  resetServer: () => ipcRenderer.invoke('reset-server'),
});
