// The only bridge between the screens and the main process. CommonJS because a sandboxed preload is not an ES
// module, and deliberately narrow: the page can ask and be told, never reach.
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('um', {
  /** The main process pushes a whole view model; the screens never compute a figure themselves. */
  onState: (fn) => ipcRenderer.on('state', (_e, state) => fn(state)),
  /** Says the screens are mounted and want the first state. */
  ready: () => ipcRenderer.send('ready'),
  show: (accountId) => ipcRenderer.send('show', accountId),
  readNow: () => ipcRenderer.send('read-now'),
  setTheme: (theme) => ipcRenderer.send('set-theme', theme),
  windowAction: (action) => ipcRenderer.send('window-action', action),
});
