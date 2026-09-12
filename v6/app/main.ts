// The Electron shell: the window, one signed-in session per account, and the timers. Every decision about who
// to read, what counts as waiting and when to sleep an account lives in core/; how a channel is read lives in
// channels/. This file only carries both out, and hands the screens a finished view model so no figure is
// computed twice.
import { app, BrowserWindow, ipcMain, nativeTheme, session, WebContentsView } from 'electron';
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { moduleFor, newHealth, type ModuleHealth } from '../channels/index.ts';
import { CHANNELS, emptyConfig, parseConfig, type Account } from '../core/config.ts';
import { pruneExpired, type Overrides } from '../core/awaiting-overrides.ts';
import { emptyResponseTimes, pruneResponseTimes, type ResponseTimes } from '../core/response-times.ts';
import { accountsToSleep, dueForRead, readableAccounts } from '../core/schedule.ts';
import { distrustColdScan, recordRead, type Snapshots } from '../core/snapshot.ts';
import { importFromV5 } from './first-run.ts';
import { loadJson, saveJson } from './store.ts';
import { buildUiState, type Route } from './view-model.ts';

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

// ponytail: the reader scripts are still the shipped v5 files, read from the tree. channels/ owns which file
// each module wants and what to do with the result; moving the files themselves is the end of this phase.
const SCRIPTS = join(HERE, '..', '..', 'UnifiedMessenger', 'Assets', 'Scripts');
const script = (file: string) => readFileSync(join(SCRIPTS, file), 'utf8');

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
/** Per channel, not per account: a reader that breaks breaks for every account on that channel. */
const health = new Map<string, ModuleHealth>();
// Seeded from the accounts that exist, so the screen names every reader in use from the moment the app opens
// rather than only once one has succeeded or failed.
for (const a of config.accounts) {
  const module = moduleFor(a.channel);
  if (module && !health.has(a.channel)) health.set(a.channel, newHealth(module));
}
let route: Route = 'dashboard';
let visible: string | null = null;
let win: BrowserWindow;

const account = (id: string) => config.accounts.find((a) => a.id === id);

function recordHealth(channel: string, ok: boolean, error?: string) {
  const module = moduleFor(channel);
  if (!module) return;
  const entry = health.get(channel) ?? newHealth(module);
  if (ok) { entry.ok++; entry.lastOkAt = Date.now(); entry.lastError = null; }
  else { entry.failed++; if (error) entry.lastError = error.slice(0, 120); }
  health.set(channel, entry);
}

// ---- window and sessions -------------------------------------------------------------------------

// Must match tokens.css: the account's own page sits exactly under the header the screens draw.
const BAR = 38, RAIL = 228, HEADER = 48;

function layout() {
  if (!win || win.isDestroyed()) return;
  const { width, height } = win.getContentBounds();
  for (const [id, view] of views) {
    view.setBounds({ x: RAIL, y: BAR + HEADER, width: Math.max(0, width - RAIL), height: Math.max(0, height - BAR - HEADER) });
    // Only the live-page route shows a page; the figures screen is ours to draw.
    view.setVisible(id === visible && route === 'account');
  }
}

function wake(a: Account) {
  if (views.has(a.id)) return;
  const partition = `persist:${a.id}`;
  const ses = session.fromPartition(partition);
  // WhatsApp refuses a browser whose user agent carries the Electron token, and shows "update your browser".
  ses.setUserAgent(ses.getUserAgent().replace(/ (unified-messenger-v6|Electron)\/\S+/g, ''));
  const view = new WebContentsView({ webPreferences: { partition, backgroundThrottling: false, contextIsolation: true, sandbox: true } });
  const module = moduleFor(a.channel);
  if (module) {
    view.webContents.on('did-finish-load', () => {
      view.webContents.executeJavaScript(module.inject(script))
        .catch((e: Error) => {
          recordHealth(a.channel, false, e.message);
          log({ event: 'inject-failed', account: a.id, channel: a.channel, error: e.message.slice(0, 120) });
        });
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

async function readAccount(a: Account, reason: string) {
  const module = moduleFor(a.channel);
  const view = views.get(a.id);
  if (!module || !view) return;
  const now = Date.now();
  try {
    const raw = await view.webContents.executeJavaScript(module.scan);
    // parse never throws: a page that changed shape costs this read, and the loop moves to the next account.
    const { entries, skipped, awaitingInferred, notReady, stage } = module.parse(raw);
    lastReadAt[a.id] = now;
    if (entries.length) {
      recordRead(snapshots, times, a.id, entries, now);
      signedOut.delete(a.id);
      recordHealth(a.channel, true);
      const waiting = entries.filter((c) => c.awaiting).length;
      lastRead[a.id] = { chats: entries.length, awaiting: waiting, at: now };
      log({ event: 'read', account: a.id, channel: a.channel, reason, chats: entries.length, awaiting: waiting, skipped, awaitingInferred });
      saveJson(FILE.snapshot, snapshots);
      saveJson(FILE.times, times);
      return;
    }
    // Empty is not the same as quiet. Ask the page why before believing it — sign-in first, because a page
    // showing a QR code has no store to read and would otherwise look like a reader that is still starting.
    const state = await view.webContents.executeJavaScript(module.signedOutProbe).catch(() => ({}));
    if (state.qr || state.login) {
      signedOut.add(a.id);
      log({ event: 'signed-out', account: a.id, channel: a.channel, reason, ...state, stage: stage ?? null, before: lastRead[a.id] ?? null });
      return;
    }
    signedOut.delete(a.id);
    // Signed in, but the reader is still coming up. WhatsApp Web builds its stores a few seconds after the
    // page loads, so an early read legitimately has nothing to give and must not count as a failure.
    if (notReady) {
      log({ event: 'reader-not-ready', account: a.id, channel: a.channel, reason, stage: stage ?? null });
      return;
    }
    recordHealth(a.channel, false, skipped ? 'scan returned nothing usable' : 'scan returned nothing');
    log({ event: 'read-empty', account: a.id, channel: a.channel, reason, ...state, skipped, stage: stage ?? null, before: lastRead[a.id] ?? null });
  } catch (e) {
    lastReadAt[a.id] = now;
    recordHealth(a.channel, false, (e as Error).message);
    log({ event: 'read-failed', account: a.id, channel: a.channel, reason, error: (e as Error).message.slice(0, 120), before: lastRead[a.id] ?? null });
  }
}

/** One pass at a time. A page that is slow to answer must not stack the next pass on top of this one. */
let ticking = false;

async function tick(reason: string) {
  if (ticking) return;
  ticking = true;
  try {
    await readPass(reason);
  } finally {
    ticking = false;
  }
}

async function readPass(reason: string) {
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
  const asleep = new Set(config.accounts.filter((a) => !views.has(a.id)).map((a) => a.id));
  win.webContents.send('state', buildUiState(config, snapshots, times, overrides, {
    now: Date.now(), route, visible, signedOut, asleep, modules: [...health.values()],
  }));
}

/**
 * Closing must actually close. Measured on this machine: with the account pages open, neither closing the
 * window nor app.exit() ends the process — it keeps running with every login held open, so the next launch
 * would find its sessions locked. So the pages are closed first, the graceful path is given two seconds, and
 * then the process ends outright. Nothing is lost by that: snapshots and reply times are written on every
 * read, and settings on every change.
 */
function quitNow(reason: string) {
  log({ event: 'quitting', reason, awake: views.size });
  for (const id of [...views.keys()]) sleep(id);
  // Ending the process abruptly would otherwise risk a login that was never written to disk, and a lost
  // login costs the owner a QR scan. Ask each account's session to write what it is holding first.
  for (const a of config.accounts) session.fromPartition(`persist:${a.id}`).flushStorageData();
  // ponytail: a blunt stop. app.exit() halts the app's own code but leaves the process running, so the app
  // ends itself instead. Find the page that refuses to shut down and this can go back to being app.exit().
  setTimeout(() => {
    log({ event: 'quit-forced', reason });
    process.kill(process.pid, 'SIGKILL');
  }, 500);
}

// ---- start ---------------------------------------------------------------------------------------

app.whenReady().then(async () => {
  // One theme for everything in the window. nativeTheme drives prefers-color-scheme in every page (so WhatsApp
  // and Instagram follow it), plus menus, scrollbars and form controls; "system" tracks Windows live.
  nativeTheme.themeSource = config.settings.theme;
  log({ event: 'startup', electron: process.versions.electron, node: process.versions.node, importedFromV5: imported, accounts: config.accounts.length, readable: readableAccounts(config).length, droppedFromConfig: dropped, problems: problems.length });

  win = new BrowserWindow({
    width: 1440, height: 900, minWidth: 1100, minHeight: 700, show: false,
    // The title bar is drawn by the app, as the designs have it.
    frame: false, backgroundColor: nativeTheme.shouldUseDarkColors ? '#0C1018' : '#E8EBF2',
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
  ipcMain.on('navigate', (_e, to: Route, id: string | null) => {
    route = to;
    visible = to === 'dashboard' || to === 'settings' ? null : id;
    if (visible) { lastUsedAt[visible] = Date.now(); const a = account(visible); if (a) wake(a); }
    layout();
    push();
  });
  ipcMain.on('read-now', () => void tick('button'));
  ipcMain.on('reload-account', (_e, id: string) => {
    views.get(id)?.webContents.reload();
    log({ event: 'reload', account: id });
  });
  ipcMain.on('sleep-account', (_e, id: string) => {
    sleep(id);
    if (visible === id) { route = 'dashboard'; visible = null; }
    layout();
    push();
  });
  ipcMain.on('set-theme', (_e, theme: 'system' | 'light' | 'dark') => {
    config.settings.theme = theme;
    nativeTheme.themeSource = theme;
    saveJson(FILE.config, config);
    push();
  });
  ipcMain.on('set-settings', (_e, patch: Record<string, unknown>) => {
    // Straight back through the config parser, so a value out of range is brought inside the limits here
    // rather than reaching the rules that use it.
    const { config: next } = parseConfig({ ...config, settings: { ...config.settings, ...patch } });
    config.settings = next.settings;
    saveJson(FILE.config, config);
    log({ event: 'settings-changed', keys: Object.keys(patch) });
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

  // Unattended check: start, give the pages time to bring their readers up, read, write one line, quit.
  // Used by UM_SELFTEST=1 npm start. The wait is the point: reading the instant the window opens only ever
  // measures how fast WhatsApp Web loads.
  if (process.env.UM_SELFTEST) {
    await new Promise((done) => setTimeout(done, Number(process.env.UM_SELFTEST_WAIT ?? 30_000)));
    // Every readable account, not only the ones the schedule says are due: the point is to prove each reader
    // works right now, and a background pass a moment earlier would otherwise skip them all.
    for (const a of readableAccounts(config)) await readAccount(a, 'selftest');
    log({ event: 'selftest', accounts: config.accounts.length, awake: views.size, snapshots: Object.keys(snapshots).length, modules: [...health.values()].map((m) => ({ id: m.id, ok: m.ok, failed: m.failed })) });
    // The open account pages outlive app.quit(), so close them and then end the process outright.
    for (const id of [...views.keys()]) sleep(id);
    quitNow('selftest');
  }
});

app.on('window-all-closed', () => quitNow('window-closed'));
