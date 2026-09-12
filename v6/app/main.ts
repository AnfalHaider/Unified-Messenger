// The Electron shell: the window, one signed-in session per account, and the timers. Every decision about who
// to read, what counts as waiting and when to sleep an account lives in core/ — this file only carries it out,
// and hands the screens a finished view model so no figure is computed twice.
import { app, BrowserWindow, ipcMain, session, WebContentsView } from 'electron';
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseConversations } from '../core/chat-entry.ts';
import { CHANNELS, emptyConfig, parseConfig, type Account } from '../core/config.ts';
import { pruneExpired, type Overrides } from '../core/awaiting-overrides.ts';
import { emptyResponseTimes, pruneResponseTimes, type ResponseTimes } from '../core/response-times.ts';
import { accountsToSleep, dueForRead, readableAccounts } from '../core/schedule.ts';
import { distrustColdScan, recordRead, type Snapshots } from '../core/snapshot.ts';
import { importFromV5 } from './first-run.ts';
import { loadJson, saveJson } from './store.ts';
import { buildUiState } from './view-model.ts';

const HERE = dirname(fileURLToPath(import.meta.url));

// UM_DATA exists because an agent shell runs inside an MSIX container, where a write to the real user-data
// path is silently redirected to a private copy. Customers never set it.
const DATA = process.env.UM_DATA || app.getPath('userData');
mkdirSync(DATA, { recursive: true });
app.setPath('userData', DATA);

const FILE = {
  config: join(DATA, 'config.json'),
  snapshot: join(DATA, 'snapshot.json'),
  times: join(DATA, 'response-times.json'),
  overrides: join(DATA, 'overrides.json'),
  log: join(DATA, 'app.log'),
};

/** Counts only. Never a customer name, a phone number or message text — this is the file support asks for. */
const log = (entry: Record<string, unknown>) =>
  appendFileSync(FILE.log, `${JSON.stringify({ t: new Date().toISOString(), ...entry })}\n`);

// ponytail: the readers are still the v5 files, read from the tree. Phase 3 moves them into channels/<name>/
// with their own tests; copying them now would fork them before that work starts.
const SCRIPTS = join(HERE, '..', '..', 'UnifiedMessenger', 'Assets', 'Scripts');
const script = (f: string) => readFileSync(join(SCRIPTS, f), 'utf8');
// The store bridge calls window.__umTruncate, which lives in adapter-core.js. Same surrogate-safe cut: a slice
// through an emoji leaves a lone surrogate, and one conversation then vanishes from every scan.
const TRUNCATE =
  'window.__umTruncate = window.__umTruncate || function (v, max) { var t = String(v || ""); if (!(max > 0) || t.length <= max) return t; var e = max, c = t.charCodeAt(e - 1); if (c >= 0xd800 && c <= 0xdbff) e -= 1; return t.slice(0, e); };';

const WHATSAPP = {
  inject: () => TRUNCATE + script('whatsapp-store-bridge.js'),
  scan: 'window.__umStartStoreScan ? (window.__umStartStoreScan(500), window.__umGetStoreScanResult()) : ""',
};
const READERS: Partial<Record<string, { inject: () => string; scan: string }>> = {
  whatsapp: WHATSAPP,
  whatsappbusiness: WHATSAPP,
  instagram: { inject: () => script('instagram-adapter.js'), scan: 'window.__umReadInstagramThreads ? window.__umReadInstagramThreads() : ""' },
};

// ---- state ---------------------------------------------------------------------------------------

const problems: string[] = [];
const note = (file: string) => (why: string) => { problems.push(`${file}: ${why}`); log({ event: 'store-problem', file, why }); };

// Before anything is loaded: a first launch on a PC that already runs v5 brings the whole install across,
// history included. It writes the files below, which are then read exactly as if they had always been there.
const imported = importFromV5(FILE, log);

const { config, dropped } = parseConfig(loadJson<unknown>(FILE.config, emptyConfig(), note('config')));
const snapshots = loadJson<Snapshots>(FILE.snapshot, {}, note('snapshot'));
const times = loadJson<ResponseTimes>(FILE.times, emptyResponseTimes(), note('response-times'));
const overrides = loadJson<Overrides>(FILE.overrides, {}, note('overrides'));

// A snapshot written by a cold scan claims almost every chat has no message. Honouring that on load would
// close the whole queue until a warm read replaced it — 354 real conversations once rendered as 5.
for (const snap of Object.values(snapshots)) snap.chats = distrustColdScan(snap.chats);
pruneResponseTimes(times);
pruneExpired(overrides, Date.now());

const views = new Map<string, WebContentsView>();
const lastReadAt: Record<string, number> = {};
const lastUsedAt: Record<string, number> = {};
/** What the previous read of this account saw, so a lost login can say what happened just before it. */
const lastRead: Record<string, { chats: number; awaiting: number; at: number }> = {};
/** Accounts whose last read found a sign-in screen. Their figures are hidden on screen, never guessed. */
const signedOut = new Set<string>();
let visible: string | null = null;
let win: BrowserWindow;

const account = (id: string) => config.accounts.find((a) => a.id === id);

// ---- window and sessions -------------------------------------------------------------------------

// Must match tokens.css: the account's own page sits exactly where the dashboard would be.
const BAR = 38, RAIL = 228;

function layout() {
  if (!win || win.isDestroyed()) return;
  const { width, height } = win.getContentBounds();
  for (const [id, view] of views) {
    view.setBounds({ x: RAIL, y: BAR, width: Math.max(0, width - RAIL), height: Math.max(0, height - BAR) });
    view.setVisible(id === visible);
  }
}

function wake(a: Account) {
  if (views.has(a.id)) return;
  const partition = `persist:${a.id}`;
  const ses = session.fromPartition(partition);
  // WhatsApp refuses a browser whose user agent carries the Electron token, and shows "update your browser".
  ses.setUserAgent(ses.getUserAgent().replace(/ (unified-messenger-v6|Electron)\/\S+/g, ''));
  const view = new WebContentsView({ webPreferences: { partition, backgroundThrottling: false, contextIsolation: true, sandbox: true } });
  const reader = READERS[a.channel];
  if (reader) {
    view.webContents.on('did-finish-load', () => {
      view.webContents.executeJavaScript(reader.inject())
        .catch((e: Error) => log({ event: 'inject-failed', account: a.id, error: e.message.slice(0, 120) }));
    });
  }
  view.webContents.loadURL(a.url || CHANNELS[a.channel].url);
  win.contentView.addChildView(view);
  views.set(a.id, view);
  lastUsedAt[a.id] = Date.now();
  log({ event: 'awake', account: a.id, channel: a.channel });
}

/** Sleeping closes the page and keeps the login: the partition on disk is untouched. */
function sleep(id: string) {
  const view = views.get(id);
  if (!view) return;
  win.contentView.removeChildView(view);
  view.webContents.close();
  views.delete(id);
  log({ event: 'asleep', account: id });
}

/** Wiping is the leaver case: the login goes too, and it cannot be undone from here. */
async function wipe(id: string) {
  sleep(id);
  await session.fromPartition(`persist:${id}`).clearStorageData();
  log({ event: 'wiped', account: id });
}

// ---- reading -------------------------------------------------------------------------------------

/** Did the page stop being signed in? Asked only when a read comes back empty, so it costs nothing normally. */
const SIGNED_OUT_PROBE = `({
  qr: !!document.querySelector('canvas'),
  login: !!document.querySelector('input[name="username"], input[type="password"]'),
  unsupported: /works with google chrome|browser (isn.t|is not) supported/i.test(document.body ? document.body.innerText : '')
})`;

async function readAccount(a: Account, reason: string) {
  const reader = READERS[a.channel];
  const view = views.get(a.id);
  if (!reader || !view) return;
  const now = Date.now();
  try {
    const raw = await view.webContents.executeJavaScript(reader.scan);
    const { entries, skipped, awaitingInferred } = parseConversations(raw ? JSON.parse(raw) : null);
    lastReadAt[a.id] = now;
    if (entries.length) {
      recordRead(snapshots, times, a.id, entries, now);
      signedOut.delete(a.id);
      const waiting = entries.filter((c) => c.awaiting).length;
      lastRead[a.id] = { chats: entries.length, awaiting: waiting, at: now };
      log({ event: 'read', account: a.id, reason, chats: entries.length, awaiting: waiting, skipped, awaitingInferred });
      saveJson(FILE.snapshot, snapshots);
      saveJson(FILE.times, times);
      return;
    }
    // Empty is not the same as quiet. Ask the page why before believing it.
    const state = await view.webContents.executeJavaScript(SIGNED_OUT_PROBE).catch(() => ({}));
    if (state.qr || state.login) signedOut.add(a.id); else signedOut.delete(a.id);
    log({ event: state.qr || state.login ? 'signed-out' : 'read-empty', account: a.id, reason, ...state, before: lastRead[a.id] ?? null });
  } catch (e) {
    lastReadAt[a.id] = now;
    log({ event: 'read-failed', account: a.id, reason, error: (e as Error).message.slice(0, 120), before: lastRead[a.id] ?? null });
  }
}

async function tick(reason: string) {
  const now = Date.now();
  for (const id of dueForRead(config, lastReadAt, now)) {
    const a = account(id);
    if (a) { if (!views.has(id)) wake(a); await readAccount(a, reason); }
  }
  for (const id of accountsToSleep(config, lastUsedAt, now, visible ?? undefined)) sleep(id);
  push();
}

/** The screens draw what this sends and nothing else. */
function push() {
  if (!win || win.isDestroyed()) return;
  win.webContents.send('state', buildUiState(config, snapshots, times, overrides, { now: Date.now(), visible, signedOut }));
}

// ---- start ---------------------------------------------------------------------------------------

app.whenReady().then(async () => {
  log({ event: 'startup', electron: process.versions.electron, node: process.versions.node, importedFromV5: imported, accounts: config.accounts.length, readable: readableAccounts(config).length, droppedFromConfig: dropped, problems: problems.length });

  win = new BrowserWindow({
    width: 1440, height: 900, minWidth: 1100, minHeight: 700, show: false,
    // The title bar is drawn by the app, as the designs have it.
    frame: false, backgroundColor: '#0C1018',
    webPreferences: { preload: join(HERE, 'preload.cjs') },
  });

  const built = join(HERE, '..', 'dist-ui', 'index.html');
  if (process.env.UM_DEV) await win.loadURL('http://localhost:5173');
  else if (existsSync(built)) await win.loadFile(built);
  else log({ event: 'no-ui', hint: 'run: npm run build:ui' });
  win.once('ready-to-show', () => win.show());
  win.on('resize', layout);

  // Every account stays awake by default; the sleep setting is the exception, and never touches an account
  // whose numbers the dashboard shows.
  for (const a of config.accounts) wake(a);
  layout();

  ipcMain.on('ready', () => push());
  ipcMain.on('show', (_e, id: string | null) => {
    visible = id;
    if (id) { lastUsedAt[id] = Date.now(); const a = account(id); if (a) wake(a); }
    layout();
    push();
  });
  ipcMain.on('read-now', () => void tick('button'));
  ipcMain.on('set-theme', (_e, theme: 'system' | 'light' | 'dark') => {
    config.settings.theme = theme;
    saveJson(FILE.config, config);
    push();
  });
  ipcMain.on('window-action', (_e, action: 'minimise' | 'maximise' | 'close') => {
    if (action === 'minimise') win.minimize();
    else if (action === 'close') win.close();
    else if (win.isMaximized()) win.unmaximize();
    else win.maximize();
  });
  ipcMain.handle('wipe', (_e, id: string) => wipe(id));

  setInterval(() => void tick('schedule'), 5_000);
  push();

  // Unattended check: start, read what there is, write one line, quit. Used by UM_SELFTEST=1 npm start.
  if (process.env.UM_SELFTEST) {
    await tick('selftest');
    log({ event: 'selftest', accounts: config.accounts.length, awake: views.size, snapshots: Object.keys(snapshots).length });
    app.quit();
  }
});

app.on('window-all-closed', () => app.quit());
