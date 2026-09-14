// The only bridge between the screens and the main process. CommonJS because a sandboxed preload is not an ES
// module, and deliberately narrow: the page can ask and be told, never reach.
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('um', {
  /** The main process pushes a whole view model; the screens never compute a figure themselves. */
  onState: (fn) => ipcRenderer.on('state', (_e, state) => fn(state)),
  /** Main asks for a screen, when a notification was clicked. */
  onOpen: (fn) => ipcRenderer.on('open', (_e, route, accountId, sub) => fn(route, accountId, sub)),
  /** Says the screens are mounted and want the first state. */
  ready: () => ipcRenderer.send('ready'),
  navigate: (route, accountId) => ipcRenderer.send('navigate', route, accountId ?? null),
  readNow: () => ipcRenderer.send('read-now'),
  reloadAccount: (accountId) => ipcRenderer.send('reload-account', accountId),
  sleepAccount: (accountId) => ipcRenderer.send('sleep-account', accountId),
  /** Takes a waiting chat off the line until the customer writes again. */
  markHandled: (accountId, key) => ipcRenderer.send('mark-handled', accountId, key),
  snooze: (accountId, key, minutes) => ipcRenderer.send('snooze', accountId, key, minutes),
  /** Takes the account's page to one conversation: opened on WhatsApp, found but not opened on Instagram. */
  openChat: (accountId, key) => ipcRenderer.send('open-chat', accountId, key),
  /** The location chosen in the title bar; null for all. Reports follow it. */
  setScope: (location) => ipcRenderer.send('set-scope', location ?? null),
  /** Opening hours and holidays. Main runs them through the config parser before saving. */
  setLocationHours: (location, hours) => ipcRenderer.send('set-location-hours', location, hours),
  setHolidays: (holidays) => ipcRenderer.send('set-holidays', holidays),
  /** A report saved (PDF, CSV) or copied as an image. Resolves with where it went, or why it did not. */
  exportReport: (request) => ipcRenderer.invoke('export-report', request),
  /** The hidden report window says it has drawn the page, and how tall the page is. */
  printRendered: (height) => ipcRenderer.send('print-rendered', height),
  /** Undoes either mark. */
  putBack: (accountId, key) => ipcRenderer.send('put-back', accountId, key),
  /** A patch of settings. Main merges it, runs it back through the config parser so limits hold, and saves. */
  setSettings: (patch) => ipcRenderer.send('set-settings', patch),
  setTheme: (theme) => ipcRenderer.send('set-theme', theme),
  windowAction: (action) => ipcRenderer.send('window-action', action),
});
