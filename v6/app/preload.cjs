// The only bridge between the toolbar page and the main process. CommonJS because a sandboxed preload is not
// an ES module, and deliberately three calls wide: the page can ask, never reach.
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('um', {
  show: (id) => ipcRenderer.send('show', id),
  readNow: () => ipcRenderer.send('read-now'),
  onStatus: (fn) => ipcRenderer.on('status', (_e, text) => fn(text)),
});
