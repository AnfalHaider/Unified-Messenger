// The Electron shell: windows, one signed-in session per account, and the timers. Every decision about who to
// read, what counts as waiting and when to sleep an account lives in core/ — this file only carries it out.
//
// The window here is deliberately a strip of buttons. The real screens are Phase 4; this exists so v6 can read
// live accounts and show honest numbers today.
import { app, BrowserWindow, ipcMain, session, WebContentsView } from 'electron';
import { appendFileSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseConversations } from '../core/chat-entry.ts';
import { CHANNELS, emptyConfig, parseConfig, type Account } from '../core/config.ts';
import { describeFreshness } from '../core/freshness.ts';
import { pruneExpired, type Overrides } from '../core/awaiting-overrides.ts';
import { emptyResponseTimes, pruneResponseTimes, type ResponseTimes } from '../core/response-times.ts';
import { accountsToSleep, dueForRead, readableAccounts } from '../core/schedule.ts';
import { awaitingSplit, distrustColdScan, lastCaptured, recordRead, type Judge, type Snapshots } from '../core/snapshot.ts';
import { importFromV5 } from './first-run.ts';
import { loadJson, saveJson } from './store.ts';

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
let visible: string | null = null;
let win: BrowserWindow;

const judge = (): Judge => ({ now: Date.now(), overrides, filterClosed: config.settings.filterClosedConversations });
const account = (id: string) => config.accounts.find((a) => a.id === id);

// ---- sessions ------------------------------------------------------------------------------------

const BAR = 46;

function layout() {
  if (!win) return;
  const { width, height } = win.getContentBounds();
  for (const [id, view] of views) {
    view.setBounds({ x: 0, y: BAR, width, height: Math.max(0, height - BAR) });
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
      const waiting = entries.filter((c) => c.awaiting).length;
      lastRead[a.id] = { chats: entries.length, awaiting: waiting, at: now };
      log({ event: 'read', account: a.id, reason, chats: entries.length, awaiting: waiting, skipped, awaitingInferred });
      saveJson(FILE.snapshot, snapshots);
      saveJson(FILE.times, times);
      return;
    }
    // Empty is not the same as quiet. Ask the page why before believing it.
    const state = await view.webContents.executeJavaScript(SIGNED_OUT_PROBE).catch(() => ({}));
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
  status();
}

// ---- window --------------------------------------------------------------------------------------

function status() {
  if (!win || win.isDestroyed()) return;
  const ids = readableAccounts(config).map((a) => a.id);
  const split = awaitingSplit(snapshots, ids, judge(), config.settings.backlogAfterDays);
  const fresh = describeFreshness(lastCaptured(snapshots));
  win.webContents.send('status', ids.length
    ? `${split.needsReply} waiting · ${split.backlog} backlog · ${split.closedAutomatically} closed by the rules · ${fresh.text}`
    : 'No accounts to read yet. Add one in config.json.');
}

const toolbar = () => `<!doctype html><meta charset="utf-8"><title>Unified Messenger v6</title><style>
body{margin:0;height:${BAR}px;display:flex;align-items:center;gap:6px;padding:0 10px;background:#0C1018;color:#EAECF7;
font:12.5px/1.4 "Segoe UI",system-ui,sans-serif}
button{height:26px;border:0;border-radius:4px;background:#161B2B;color:#EAECF7;padding:0 10px;cursor:pointer}
button.on{background:#4B4FD6}#status{margin-left:auto;color:#9298BE}</style>
${config.accounts.map((a) => `<button onclick="um.show('${a.id}')">${a.name}</button>`).join('')}
<button onclick="um.readNow()">Read now</button>
<span id="status">Starting…</span>
<script>um.onStatus((s) => { document.getElementById('status').textContent = s; });</script>`;

app.whenReady().then(async () => {
  log({ event: 'startup', electron: process.versions.electron, node: process.versions.node, importedFromV5: imported, accounts: config.accounts.length, readable: readableAccounts(config).length, droppedFromConfig: dropped, problems: problems.length });

  win = new BrowserWindow({
    width: 1280, height: 860, title: 'Unified Messenger', backgroundColor: '#0C1018',
    webPreferences: { preload: join(HERE, 'preload.cjs') },
  });
  win.loadURL(`data:text/html;charset=utf-8,${encodeURIComponent(toolbar())}`);
  win.on('resize', layout);

  // Every account stays awake by default; the sleep setting is the exception, and never touches an account
  // whose numbers the dashboard shows.
  for (const a of config.accounts) wake(a);
  visible = config.accounts[0]?.id ?? null;
  layout();

  ipcMain.on('show', (_e, id: string) => { visible = id; lastUsedAt[id] = Date.now(); const a = account(id); if (a) wake(a); layout(); });
  ipcMain.on('read-now', () => void tick('button'));

  setInterval(() => void tick('schedule'), 5_000);
  status();

  // Unattended check: start, read what there is, write one line, quit. Used by npm run smoke.
  if (process.env.UM_SELFTEST) {
    await tick('selftest');
    log({ event: 'selftest', accounts: config.accounts.length, awake: views.size, snapshots: Object.keys(snapshots).length });
    app.quit();
  }
});

app.on('window-all-closed', () => app.quit());

// Wipe has no UI yet: Settings owns it in Phase 4. Exported through IPC so the screens can call it then.
ipcMain.handle('wipe', (_e, id: string) => wipe(id));
