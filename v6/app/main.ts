// The Electron shell: the window, one signed-in session per account, and the timers. Every decision about who
// to read, what counts as waiting and when to sleep an account lives in core/; how a channel is read lives in
// channels/. This file only carries both out, and hands the screens a finished view model so no figure is
// computed twice.
import { app, BrowserWindow, clipboard, ClipboardItem, dialog, ipcMain, Menu, nativeTheme, Notification, session, Tray, WebContentsView } from 'electron';
import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { moduleFor, newHealth, type ModuleHealth } from '../channels/index.ts';
import { alertsDue, pruneNotified, type Alert, type Notified } from '../core/alerts.ts';
import { CHANNELS, emptyConfig, parseConfig, type Account } from '../core/config.ts';
import { clear, markHandled, pruneExpired, snooze, type Overrides } from '../core/awaiting-overrides.ts';
import { emptyResponseTimes, pruneResponseTimes, type ResponseTimes } from '../core/response-times.ts';
import { accountsToSleep, dueForRead, readableAccounts } from '../core/schedule.ts';
import { DAY_MS } from '../core/days.ts';
import { dayKey, recordHistory, type History } from '../core/history.ts';
import { reportCsv, weekEnding, weeklyDue } from '../core/report.ts';
import { digestDue } from '../core/digest.ts';
import { awaitingChats, distrustColdScan, recordRead, type Snapshots } from '../core/snapshot.ts';
import { importFromV5 } from './first-run.ts';
import { loadJson, saveJson } from './store.ts';
import { ACCOUNT_ROUTES, buildUiState, reportAccounts, targetFor, waitingQueue, type Route } from './view-model.ts';

const HERE = dirname(fileURLToPath(import.meta.url));

// UM_DATA exists because an agent shell runs inside an MSIX container, where a write to the real user-data
// path is silently redirected to a private copy. Customers never set it.
// The folder is named explicitly rather than taken from the app's name, so the installed app and a run from
// source share one set of logins and history, and renaming the product can never strand them.
const DATA = process.env.UM_DATA || join(app.getPath('appData'), 'unified-messenger-v6');
mkdirSync(DATA, { recursive: true });
app.setPath('userData', DATA);

// One copy at a time. Two would open the same saved sessions and fight over them; a second launch (a
// shortcut clicked twice, the installer starting it) brings the first window forward instead.
if (!app.requestSingleInstanceLock()) {
  app.exit(0);
  process.exit(0);
}
// `UnifiedMessenger6.exe --quit` is how the installer, the uninstaller and scripts close a running copy: with
// close-to-background on, a window close only hides the app, so they need a way that really quits.
app.on('second-instance', (_e, argv) => {
  if (argv.includes('--quit')) void quitNow('quit-requested');
  else showWindow();
});

const FILE = {
  config: join(DATA, 'config.json'),
  snapshot: join(DATA, 'snapshot.json'),
  times: join(DATA, 'response-times.json'),
  overrides: join(DATA, 'overrides.json'),
  alerts: join(DATA, 'alerts.json'),
  history: join(DATA, 'history.json'),
  exports: join(DATA, 'exports.json'),
  digest: join(DATA, 'digest.json'),
  log: join(DATA, 'app.log'),
};

// Windows shows a toast only for an app id that a Start Menu shortcut carries; installer.iss sets the same one.
app.setAppUserModelId('UnifiedMessenger.v6');

/** Counts only. Never a customer name, a phone number or message text — this is the file support asks for. */
const log = (entry: Record<string, unknown>) =>
  appendFileSync(FILE.log, `${JSON.stringify({ t: new Date().toISOString(), ...entry })}\n`);

// Page scripts live beside the module that injects them, so the installed app carries its own readers.
const script = (file: string) => readFileSync(join(HERE, '..', 'channels', file), 'utf8');

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
const notified = loadJson<Notified>(FILE.alerts, {}, note('alerts'));
const history = loadJson<History>(FILE.history, {}, note('history'));
/** Which week's report was last saved on its own, so Monday's save happens once. */
const exportsState = loadJson<{ lastWeeklyWeek: string | null }>(FILE.exports, { lastWeeklyWeek: null }, note('exports'));
/** The local day the morning digest was last shown, so it opens once a day. */
const digestState = loadJson<{ lastShownDay: string | null }>(FILE.digest, { lastShownDay: null }, note('digest'));

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
let route: Route = 'line';
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

// Must match tokens.css: the account's own page fills the dock's page slot, between the line on the left, the
// dock bar above and the customer panel on the right.
const BAR = 44, RAIL = 84, LINE = 400, DOCK_BAR = 57, CUSTOMER = 290;

function layout() {
  if (!win || win.isDestroyed()) return;
  const { width, height } = win.getContentBounds();
  for (const [id, view] of views) {
    const x = RAIL + LINE, y = BAR + DOCK_BAR;
    view.setBounds({ x, y, width: Math.max(0, width - x - CUSTOMER), height: Math.max(0, height - y) });
    // Only the live-page route shows a page; the figures screen is ours to draw.
    view.setVisible(id === visible && route === 'dock');
  }
}

/** Chrome's own user agent, without the tokens Electron and the app add to it. */
const userAgent = (ua: string) => ua.replace(/\(KHTML, like Gecko\) .*?Chrome\//, '(KHTML, like Gecko) Chrome/').replace(/ Electron\/\S+/, '');

function wake(a: Account) {
  if (quitting || views.has(a.id)) return;
  const partition = `persist:${a.id}`;
  const ses = session.fromPartition(partition);
  // WhatsApp refuses a browser whose user agent carries the Electron token, and shows "update your browser".
  // Electron adds the app's name and version before "Chrome/" and its own token before "Safari/". The name can
  // contain spaces, so everything between Chrome's "(KHTML, like Gecko)" and "Chrome/" goes, not one token.
  ses.setUserAgent(userAgent(ses.getUserAgent()));
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
  // A page that crashes or fails to load says so in the log; otherwise it only shows up as reads that never come.
  view.webContents.on('render-process-gone', (_e, details) => log({ event: 'page-gone', account: a.id, reason: details.reason, exitCode: details.exitCode }));
  view.webContents.on('did-fail-load', (_e, code, description, _url, isMainFrame) => {
    if (isMainFrame) log({ event: 'page-load-failed', account: a.id, code, description: String(description).slice(0, 80) });
  });
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

/** How long a page may take to answer one read. A page that is loading, crashed or stuck behind a dialog can leave
 *  executeJavaScript unanswered for ever, and reads run one account at a time, so one silent page would stop every
 *  account being read. With a limit it costs only its own read. */
const PAGE_ANSWER_MS = 30_000;

function pageAnswer<T>(view: WebContentsView, expression: string): Promise<T> {
  return new Promise<T>((done, fail) => {
    const timer = setTimeout(() => fail(new Error(`the page did not answer within ${PAGE_ANSWER_MS / 1000} s`)), PAGE_ANSWER_MS);
    view.webContents.executeJavaScript(expression).then(
      (value) => { clearTimeout(timer); done(value as T); },
      (error) => { clearTimeout(timer); fail(error); },
    );
  });
}

async function readAccount(a: Account, reason: string) {
  const module = moduleFor(a.channel);
  const view = views.get(a.id);
  if (!module || !view) return;
  const now = Date.now();
  try {
    const raw = await pageAnswer<unknown>(view, module.scan);
    // parse never throws: a page that changed shape costs this read, and the loop moves to the next account.
    const { entries, skipped, awaitingInferred, notReady, stage } = module.parse(raw);
    lastReadAt[a.id] = now;
    if (entries.length) {
      const prior = snapshots[a.id]?.chats;
      recordRead(snapshots, times, a.id, entries, now);
      // The day's record, by the same waiting rule as the line: overrides and the closed-chat setting apply.
      // A history file edited into a strange shape costs the day's record, never the read or the reader's health.
      try {
        const judge = { now, overrides, filterClosed: config.settings.filterClosedConversations };
        recordHistory(history, a.id, prior, snapshots[a.id].chats, {
          now, samples: times.samples[a.id] ?? [], targetMinutes: targetFor(config, a),
          waitingOverADay: awaitingChats(snapshots, a.id, judge).filter((c) => c.lastActivity < now - DAY_MS).length,
        });
      } catch (e) {
        log({ event: 'history-failed', account: a.id, error: (e as Error).message.slice(0, 120) });
      }
      signedOut.delete(a.id);
      recordHealth(a.channel, true);
      const waiting = entries.filter((c) => c.awaiting).length;
      lastRead[a.id] = { chats: entries.length, awaiting: waiting, at: now };
      log({ event: 'read', account: a.id, channel: a.channel, reason, chats: entries.length, awaiting: waiting, skipped, awaitingInferred });
      saveJson(FILE.snapshot, snapshots);
      saveJson(FILE.times, times);
      saveJson(FILE.history, history);
      return;
    }
    // Empty is not the same as quiet. Ask the page why before believing it — sign-in first, because a page
    // showing a QR code has no store to read and would otherwise look like a reader that is still starting.
    const state = await pageAnswer<Record<string, boolean>>(view, module.signedOutProbe).catch(() => ({} as Record<string, boolean>));
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
  if (ticking || quitting) return;
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

/** The view model for a route: the one on screen, or 'reports' for the hidden window a report is saved from. */
function stateFor(forRoute: Route) {
  const asleep = new Set(config.accounts.filter((a) => !views.has(a.id)).map((a) => a.id));
  return buildUiState(config, snapshots, times, overrides, {
    now: Date.now(), route: forRoute, visible, signedOut, asleep, modules: [...health.values()], history,
  });
}

/** The screens draw what this sends and nothing else. */
function push() {
  if (!win || win.isDestroyed()) return;
  const state = stateFor(route);
  win.webContents.send('state', state);
  tray?.setToolTip(`Unified Messenger: ${state.split.needsReply} waiting`);
  notifyDue();
}

// ---- saving reports ------------------------------------------------------------------------------

/** Tests set this, so a save goes straight to a folder instead of a dialog nobody can answer, and an image is
 *  written there instead of replacing whatever is on the clipboard. Customers never set it. */
const EXPORT_DIR = process.env.UM_EXPORT_DIR;
const REPORT_WIDTH = 820;
type ExportRequest = { format: 'pdf' | 'csv' | 'png'; week?: 'this' | 'last'; days?: number };
type ExportResult = { saved?: string; copied?: boolean; cancelled?: boolean; error?: string };

/**
 * Draws the weekly report in a hidden window from the same component the screen uses, so what is saved is what
 * was shown. The page says when it has drawn and how tall it is; the window is then sized to the whole page.
 * The caller destroys it (never closes it: a close would run the main window's hide-or-quit).
 */
async function drawWeekly(week: 'this' | 'last') {
  const page = new BrowserWindow({
    show: false, width: REPORT_WIDTH, height: 1100, useContentSize: true, backgroundColor: '#ffffff',
    webPreferences: { preload: join(HERE, 'preload.cjs') },
  });
  try {
    const drawn = new Promise<number>((done, fail) => {
      const timer = setTimeout(() => fail(new Error('the report did not finish drawing')), 20_000);
      page.webContents.ipc.once('print-rendered', (_e, height: number) => { clearTimeout(timer); done(Math.max(400, Math.ceil(Number(height) || 1100))); });
    });
    page.webContents.ipc.on('ready', () => page.webContents.send('state', stateFor('reports')));
    await page.loadFile(join(HERE, '..', 'dist-ui', 'index.html'), { hash: `print=weekly&week=${week}` });
    const height = await drawn;
    page.setContentSize(REPORT_WIDTH, height);
    await new Promise((done) => setTimeout(done, 300));
    return { page, height };
  } catch (e) {
    page.destroy();
    throw e;
  }
}

async function saveAs(name: string, filter: { name: string; extensions: string[] }, data: Buffer | string): Promise<ExportResult> {
  let path: string;
  if (EXPORT_DIR) path = join(EXPORT_DIR, name);
  else {
    const choice = await dialog.showSaveDialog(win, { defaultPath: join(app.getPath('documents'), name), filters: [filter] });
    if (choice.canceled || !choice.filePath) return { cancelled: true };
    path = choice.filePath;
  }
  writeFileSync(path, data);
  return { saved: path };
}

const pdfOf = (page: BrowserWindow, height: number) => page.webContents.printToPDF({
  printBackground: true, margins: { top: 0, bottom: 0, left: 0, right: 0 },
  // One tall page, inches at 96 dpi: the report is read on a screen, and a page break through a chart helps nobody.
  pageSize: { width: REPORT_WIDTH / 96, height: height / 96 },
});

async function exportReport(request: ExportRequest): Promise<ExportResult> {
  const week = request.week === 'this' ? 'this' : 'last';
  const now = Date.now();
  if (request.format === 'csv') {
    const days = request.week ? 7 : Math.min(400, Math.max(1, Math.round(Number(request.days) || 7)));
    const end = request.week ? weekEnding(now, week) : now;
    const name = request.week ? `Weekly figures ${dayKey(end - 6 * DAY_MS)}.csv` : `Report figures ${days === 1 ? dayKey(end) : `${dayKey(end - (days - 1) * DAY_MS)} to ${dayKey(end)}`}.csv`;
    return saveAs(name, { name: 'CSV', extensions: ['csv'] }, reportCsv(history, reportAccounts(config), days, end));
  }
  const { page, height } = await drawWeekly(week);
  try {
    const monday = dayKey(weekEnding(now, week) - 6 * DAY_MS);
    if (request.format === 'pdf') return await saveAs(`Weekly report ${monday}.pdf`, { name: 'PDF', extensions: ['pdf'] }, await pdfOf(page, height));
    const image = await page.webContents.capturePage();
    if (image.isEmpty()) throw new Error('the report image came out empty');
    if (EXPORT_DIR) return await saveAs(`Weekly report ${monday}.png`, { name: 'PNG', extensions: ['png'] }, image.toPNG());
    // Electron 44's clipboard is the W3C one: an item per MIME type, written asynchronously.
    await clipboard.write([new ClipboardItem({ 'image/png': new Blob([new Uint8Array(image.toPNG())], { type: 'image/png' }) })]);
    return { copied: true };
  } finally {
    page.destroy();
  }
}

let savingWeekly = false;

/** Last week's PDF, once, from Monday 10 am, when the owner turned it on. A week with nothing recorded is skipped. */
async function saveWeeklyIfDue() {
  if (!config.settings.weeklyReport.autoSave || savingWeekly || quitting) return;
  const week = weeklyDue(Date.now(), exportsState.lastWeeklyWeek);
  if (!week) return;
  savingWeekly = true;
  try {
    const hasData = stateFor('reports').reports?.weekly.last.hasData ?? false;
    if (hasData) {
      const folder = EXPORT_DIR ?? join(app.getPath('documents'), 'Unified Messenger reports');
      mkdirSync(folder, { recursive: true });
      const { page, height } = await drawWeekly('last');
      try { writeFileSync(join(folder, `Weekly report ${week}.pdf`), await pdfOf(page, height)); } finally { page.destroy(); }
    }
    exportsState.lastWeeklyWeek = week;
    saveJson(FILE.exports, exportsState);
    log({ event: 'weekly-saved', week, skipped: !hasData });
  } catch (e) {
    log({ event: 'weekly-save-failed', week, error: (e as Error).message.slice(0, 120) });
  } finally {
    savingWeekly = false;
  }
}

// ---- going to a conversation ---------------------------------------------------------------------

const FOCUS_STEP_MS = 700, FOCUS_BUDGET_MS = 20_000, FOCUS_STEP_TIMEOUT_MS = 3_000;
/** The latest request per account. A newer one ends the older loop, so two quick clicks never fight. */
const focusRequest: Record<string, number> = {};

/**
 * The owner asked for one chat. The page is asked to take a step every 700 ms until it says it arrived, for up
 * to 20 s: a page woken for this, or a WhatsApp still building its stores, needs several seconds first, and an
 * unsaved number took 8.5 s to open on the live page. Who to
 * look for comes from the snapshot, never the screen. The log records the outcome, never the customer.
 */
async function focusChat(id: string, key: string) {
  const a = account(id);
  const module = a && moduleFor(a.channel);
  const chat = snapshots[id]?.chats.find((c) => c.conversationKey === key);
  if (!a || !module?.focus || !chat) return;
  if (!views.has(id)) wake(a);
  const request = (focusRequest[id] = (focusRequest[id] ?? 0) + 1);
  const started = Date.now();
  const expression = module.focus({ key, name: chat.customerName, phone: chat.contactPhone });
  let step = 'working', attempts = 0;
  while (!quitting && focusRequest[id] === request && Date.now() - started < FOCUS_BUDGET_MS) {
    const view = views.get(id);
    if (!view || view.webContents.isDestroyed()) break;
    attempts++;
    // A page mid-navigation can leave executeJavaScript unanswered; a step that does not answer is a step to retry.
    step = await Promise.race([
      view.webContents.executeJavaScript(expression).then((s) => String(s)).catch(() => 'working'),
      new Promise<string>((done) => setTimeout(() => done('working'), FOCUS_STEP_TIMEOUT_MS)),
    ]);
    if (step === 'done' || step === 'no-target') break;
    await new Promise((done) => setTimeout(done, FOCUS_STEP_MS));
  }
  const result = focusRequest[id] !== request ? 'replaced' : step === 'done' ? 'arrived' : step;
  log({ event: 'focus', account: id, channel: a.channel, result, attempts, ms: Date.now() - started });
}

// ---- the owner's marks ---------------------------------------------------------------------------

// Main looks the chat up itself: "handled" holds until a message newer than the one on record, so that time comes
// from the snapshot, never from the screen. The log names the account, not the chat.
function saveOverrides(event: string, id: string) {
  pruneExpired(overrides, Date.now());
  saveJson(FILE.overrides, overrides);
  log({ event, account: id });
  push();
}

function markChatHandled(id: string, key: string) {
  const chat = snapshots[id]?.chats.find((c) => c.conversationKey === key);
  if (!chat) return;
  markHandled(overrides, id, key, chat.lastActivity, Date.now());
  saveOverrides('marked-handled', id);
}

function snoozeChat(id: string, key: string, minutes: number) {
  if (!snapshots[id]?.chats.some((c) => c.conversationKey === key)) return;
  snooze(overrides, id, key, Date.now() + Math.min(7 * 24 * 60, Math.max(1, Number(minutes) || 60)) * 60_000, Date.now());
  saveOverrides('snoozed', id);
}

// ---- alerts --------------------------------------------------------------------------------------

/** Shown toasts, kept so their click and button handlers are not collected while the toast is on screen. */
const toasts = new Set<Notification>();

/** Runs with every push, so a wait crossing its warning point is caught within seconds, not at the next read. */
function notifyDue() {
  if (quitting || !Notification.isSupported()) return;
  const now = Date.now();
  const before = JSON.stringify(notified);
  const shown = alertsDue({
    rows: waitingQueue(config, snapshots, overrides, now, signedOut),
    signedOut: config.accounts.filter((a) => signedOut.has(a.id)).map((a) => ({ id: a.id, name: a.name })),
    // lastRead is set only by a read that found chats, so it is proof of a login this run.
    signedIn: Object.keys(lastRead).filter((id) => !signedOut.has(id)),
    settings: config.settings, now,
  }, notified);
  pruneNotified(notified, now);
  if (JSON.stringify(notified) !== before) saveJson(FILE.alerts, notified);
  for (const alert of shown) showAlert(alert);
}

function showAlert(alert: Alert) {
  const toast = new Notification({
    title: alert.title, body: alert.body, icon: join(HERE, '..', 'assets', 'icon.ico'),
    actions: alert.key ? [{ type: 'button', text: 'Open chat' }, { type: 'button', text: 'Snooze 1 hour' }] : [],
  });
  toasts.add(toast);
  const done = () => toasts.delete(toast);
  toast.on('click', () => { openFromAlert(alert); done(); });
  toast.on('action', (details) => {
    if (details.actionIndex === 1 && alert.accountId && alert.key) snoozeChat(alert.accountId, alert.key, 60);
    else openFromAlert(alert);
    done();
  });
  toast.on('close', done);
  toast.show();
  log({ event: 'alert', kind: alert.kind, account: alert.accountId });
}

/** A chat alert opens that chat in the dock; a sign-in alert opens the account's page; a summary opens the line. */
function openFromAlert(alert: Alert) {
  showWindow();
  win.webContents.send('open', alert.accountId ? 'dock' : 'line', alert.accountId, alert.key ?? '');
  if (alert.accountId && alert.key) void focusChat(alert.accountId, alert.key);
}

// ---- running in the background -------------------------------------------------------------------

let tray: Tray | undefined;
let toldAboutTray = false;
/** Windows is signing out or shutting down: a close must really close, not hide. */
let endingSession = false;

function showWindow() {
  if (!win || win.isDestroyed()) return;
  if (win.isMinimized()) win.restore();
  win.show();
  win.focus();
  push();
  showDigestIfDue();
}

/**
 * The first time the window is opened on a local day, it opens on the morning digest. "Opened" is the app starting
 * or the window coming back from the tray, so an app left reading overnight still greets the owner in the morning.
 * Only when something is read: a PC with no accounts has nothing to summarise.
 */
function showDigestIfDue() {
  const now = Date.now();
  if (!win || win.isDestroyed() || quitting || !readableAccounts(config).length) return;
  if (!digestDue(now, digestState.lastShownDay, config.settings.morningDigest)) return;
  digestState.lastShownDay = dayKey(now);
  saveJson(FILE.digest, digestState);
  win.webContents.send('open', 'digest', null, '');
  log({ event: 'digest-shown' });
}

/** Hides the window and keeps every account reading. Said once per run, from the tray, so it is not a mystery. */
function hideToBackground(reason: string) {
  win.hide();
  log({ event: 'hidden', reason });
  if (toldAboutTray) return;
  toldAboutTray = true;
  tray?.displayBalloon({ iconType: 'info', title: 'Unified Messenger is still reading', content: 'It keeps counting who is waiting. Open it or quit from this icon.' });
}

/** What every close does: hide when the owner chose background running, otherwise quit. */
function closeWindow(reason: string) {
  if (config.settings.closeToBackground && !endingSession) hideToBackground(reason);
  else void quitNow(reason);
}

/** Set once shutdown starts, so a read, a wake or a second close cannot race it. */
let quitting = false;
let readTimer: ReturnType<typeof setInterval> | undefined;

/**
 * Closing closes. A WebContentsView's page is not destroyed with its window, and while any page is still alive
 * Electron will not finish quitting — which is why this app once had to kill its own process, and why that kill
 * cost two WhatsApp logins: a page ended mid-write leaves its storage damaged. So the order is: stop reading,
 * ask every session to write what it holds, close each page and wait until it is really gone, then quit the
 * ordinary way. A page that ignores the close is logged and left to Electron after five seconds; nothing is
 * ever killed.
 */
async function quitNow(reason: string) {
  if (quitting) return;
  quitting = true;
  const started = Date.now();
  clearInterval(readTimer);
  log({ event: 'quitting', reason, awake: views.size });
  await Promise.all(config.accounts.map((a) => new Promise<void>((done) => {
    session.fromPartition(`persist:${a.id}`).flushStorageData();
    done();
  })));
  const closing = [...views.entries()].map(([id, view]) => new Promise<string | null>((done) => {
    const contents = view.webContents;
    if (contents.isDestroyed()) return done(null);
    contents.once('destroyed', () => done(null));
    setTimeout(() => done(id), 5_000);
    if (win && !win.isDestroyed()) win.contentView.removeChildView(view);
    contents.close();
  }));
  const stuck = (await Promise.all(closing)).filter((id): id is string => id !== null);
  views.clear();
  tray?.destroy();
  log({ event: 'pages-closed', ms: Date.now() - started, stuck });
  if (win && !win.isDestroyed()) win.destroy();
  app.quit();
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
    frame: false, backgroundColor: nativeTheme.shouldUseDarkColors ? '#121513' : '#ECEEEA',
    icon: join(HERE, '..', 'assets', 'icon.ico'),
    webPreferences: { preload: join(HERE, 'preload.cjs') },
  });
  // Windows signing out or shutting down gives the app seconds, not a normal close: write the sessions first.
  win.on('session-end', () => {
    endingSession = true;
    for (const a of config.accounts) session.fromPartition(`persist:${a.id}`).flushStorageData();
  });

  tray = new Tray(join(HERE, '..', 'assets', 'icon.ico'));
  tray.setToolTip('Unified Messenger');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Open Unified Messenger', click: showWindow },
    { label: 'Read every account now', click: () => void tick('tray') },
    { type: 'separator' },
    { label: 'Quit Unified Messenger', click: () => void quitNow('tray-quit') },
  ]));
  tray.on('click', showWindow);

  const built = join(HERE, '..', 'dist-ui', 'index.html');
  if (process.env.UM_DEV) await win.loadURL('http://localhost:5173');
  else if (existsSync(built)) await win.loadFile(built);
  else log({ event: 'no-ui', hint: 'run: npm run build:ui' });
  win.once('ready-to-show', () => win.show());
  // Only the main window hides or quits on close. Other windows (the hidden report page) just go.
  win.on('close', (e) => { if (!quitting) { e.preventDefault(); closeWindow('window-close'); } });
  win.on('resize', layout);

  // Every account stays awake by default; the sleep setting is the exception, and never touches an account
  // whose numbers the dashboard shows.
  for (const a of config.accounts) wake(a);
  layout();

  ipcMain.on('ready', (e) => { push(); if (e.sender === win.webContents) showDigestIfDue(); });
  ipcMain.on('navigate', (_e, to: Route, id: string | null) => {
    route = to;
    visible = ACCOUNT_ROUTES.includes(to) ? id : null;
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
    if (visible === id) { route = 'line'; visible = null; }
    layout();
    push();
  });
  ipcMain.on('open-chat', (_e, id: string, key: string) => void focusChat(id, key));
  // Opening hours and holidays go back through the config parser, so a window that closes before it opens, a
  // date that does not exist or a location that is gone is corrected here, never reaches the rules.
  const saveLocations = (event: string, next: unknown) => {
    const { config: parsed } = parseConfig(next);
    config.locations = parsed.locations;
    config.holidays = parsed.holidays;
    saveJson(FILE.config, config);
    log({ event, locations: config.locations.length, holidays: config.holidays.length });
    push();
  };
  ipcMain.on('set-location-hours', (_e, name: string, hours: unknown) => {
    if (!config.locations.some((l) => l.name === name)) return;
    const location = config.locations.find((l) => l.name === name)!;
    // Keep what v5 wrote beside the new week, so an older build reading this file still sees sensible hours.
    const merged = { ...(location.hours ?? {}), ...(typeof hours === 'object' && hours ? hours : {}) };
    saveLocations('hours-changed', { ...config, locations: config.locations.map((l) => (l.name === name ? { ...l, hours: merged } : l)) });
  });
  ipcMain.on('set-holidays', (_e, holidays: unknown) => saveLocations('holidays-changed', { ...config, holidays }));
  ipcMain.handle('export-report',async (_e, request: ExportRequest) => {
    try {
      const result = await exportReport(request);
      log({ event: 'export', format: request.format, result: result.error ? 'error' : result.cancelled ? 'cancelled' : result.copied ? 'copied' : 'saved' });
      return result;
    } catch (e) {
      log({ event: 'export', format: request.format, result: 'error', error: (e as Error).message.slice(0, 120) });
      return { error: (e as Error).message };
    }
  });
  ipcMain.on('mark-handled', (_e, id: string, key: string) => markChatHandled(id, key));
  ipcMain.on('snooze', (_e, id: string, key: string, minutes: number) => snoozeChat(id, key, minutes));
  ipcMain.on('put-back', (_e, id: string, key: string) => {
    clear(overrides, id, key);
    saveOverrides('put-back', id);
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
    else if (action === 'close') closeWindow('close-button');
    else if (win.isMaximized()) win.unmaximize();
    else win.maximize();
  });
  ipcMain.handle('wipe', (_e, id: string) => wipe(id));

  readTimer = setInterval(() => { void tick('schedule'); void saveWeeklyIfDue(); }, 5_000);
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
    // The same shutdown the close button uses, so the check proves closing works too.
    void quitNow('selftest');
  }
});

app.on('window-all-closed', () => void quitNow('window-closed'));

app.on('will-quit', () => log({ event: 'will-quit' }));
app.on('quit', (_e, code) => log({ event: 'quit', code }));
