// The only bridge between the screens and the main process. CommonJS because a sandboxed preload is not an ES
// module, and deliberately narrow: the page can ask and be told, never reach.
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('um', {
  /** The main process pushes a whole view model; the screens never compute a figure themselves. */
  onState: (fn) => ipcRenderer.on('state', (_e, state) => fn(state)),
  /** Says the screens are mounted and want the first state. */
  ready: () => ipcRenderer.send('ready'),
  navigate: (route, accountId) => ipcRenderer.send('navigate', route, accountId ?? null),
  readNow: () => ipcRenderer.send('read-now'),
  reloadAccount: (accountId) => ipcRenderer.send('reload-account', accountId),
  sleepAccount: (accountId) => ipcRenderer.send('sleep-account', accountId),
  /** Takes a waiting chat off the line until the customer writes again. */
  markHandled: (accountId, key) => ipcRenderer.send('mark-handled', accountId, key),
  snooze: (accountId, key, minutes) => ipcRenderer.send('snooze', accountId, key, minutes),
  /** Undoes either mark. */
  putBack: (accountId, key) => ipcRenderer.send('put-back', accountId, key),
  /** A patch of settings. Main merges it, runs it back through the config parser so limits hold, and saves. */
  setSettings: (patch) => ipcRenderer.send('set-settings', patch),
  setTheme: (theme) => ipcRenderer.send('set-theme', theme),
  windowAction: (action) => ipcRenderer.send('window-action', action),
});
