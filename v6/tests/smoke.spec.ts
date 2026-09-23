// The window opens, draws a screen, moves to another, and quits the ordinary way. Needs `npx vite build` first:
// without dist-ui the window stays blank and this fails, which is the point.
import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { AxeBuilder } from '@axe-core/playwright';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const V6 = join(dirname(fileURLToPath(import.meta.url)), '..');

/** A data folder of its own, never the owner's. A config already present also means no v5 import is tried, and
 *  closing quits instead of hiding, so the process really ends. The morning digest is off unless a test turns it
 *  on, or every test with accounts would open on it. */
function dataFolder(config: Record<string, unknown> = {}) {
  const data = mkdtempSync(join(tmpdir(), 'um-smoke-'));
  const settings = { closeToBackground: false, morningDigest: false, ...(config.settings as Record<string, unknown> | undefined) };
  writeFileSync(join(data, 'config.json'), JSON.stringify({ ...config, settings }));
  return data;
}

async function open(data: string, env: Record<string, string> = {}): Promise<{ app: ElectronApplication; win: Page }> {
  // No test ever reaches Google: its two addresses are the invented pages in tests/fixtures/google, and sign-in uses
  // an invented project whose every address is a closed port unless the test brings a fake of its own.
  //
  // **No cloud config by default.** A build that cannot ask anyone gates nobody (core/admission.ts), which is what
  // a test of the line or the reports wants. A test about signing in, workspaces or the gate itself passes CLOUD
  // in its own env, and is then admitted the way a real person is: as a member, or as the product owner.
  const google = join(V6, 'tests', 'fixtures', 'google');
  const app = await electron.launch({ args: ['app/main.ts'], cwd: V6, env: {
    ...process.env, UM_DATA: data, UM_V5: join(data, 'no-v5'), UM_LOCALAPPDATA: join(data, 'no-local-app-data'),
    UM_GOOGLE_REVIEWS_URL: pathToFileURL(join(google, 'reviews', 'index.html')).href, UM_GOOGLE_PROFILE_URL: pathToFileURL(join(google, 'profile.html')).href,
    UM_CLOUD_ENDPOINT: 'http://127.0.0.1:9',
    UM_FIRESTORE: 'http://127.0.0.1:9/v1/projects/test-project/databases/(default)', ...env,
  } });
  return { app, win: await app.firstWindow() };
}

/** The invented project a cloud test signs in to. Only tests that want the cloud pass it. */
const CLOUD = { UM_CLOUD_CONFIG: join(V6, 'tests', 'fixtures', 'cloud-config.json') };

async function quit(app: ElectronApplication, win: Page) {
  const closed = app.waitForEvent('close');
  await win.getByRole('button', { name: 'Quit', exact: true }).click();
  await closed;
}

const alertLines = (data: string) =>
  readFileSync(join(data, 'app.log'), 'utf8').split('\n').filter((l) => l.includes('"event":"alert"')).map((l) => JSON.parse(l));

const heading = (win: Page, name: string | RegExp) => win.getByRole('heading', { level: 1, name, exact: typeof name === 'string' });

test('the window renders, navigates and quits', async () => {
  const data = dataFolder();
  const { app, win } = await open(data);
  try {
    await expect(heading(win, /customers are waiting|No accounts are being read yet/)).toBeVisible();
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Accounts', exact: true }).click();
    await expect(heading(win, 'Accounts')).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('handled and snoozed chats leave the line, stay off it after a restart, and come back when put back', async () => {
  // Invented customers on a page that never loads WhatsApp, so no read replaces them.
  const now = Date.now();
  const chat = (key: string, name: string, minutesAgo: number, preview: string) => ({
    conversationKey: key, customerName: name, unread: 1, lastActivity: now - minutesAgo * 60_000, preview,
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  const data = dataFolder({ accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true }] });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'test-wa': { capturedAt: now, chats: [
      chat('a@c.us', 'Sample Customer A', 12, 'Do you have space on Friday afternoon?'),
      chat('b@c.us', 'Sample Customer B', 8, 'What time do you open tomorrow?'),
      chat('c@c.us', 'Sample Customer C', 4, 'Can I change my booking to next week?'),
      // Closed by the "ended the chat" rule: never on the line, listed in Set aside with no Put back.
      chat('d@c.us', 'Sample Customer D', 20, 'ok thanks'),
    ] },
  }));

  let { app, win } = await open(data);
  try {
    await expect(heading(win, '3 customers are waiting')).toBeVisible();
    // The longest wait is selected first, so its actions are showing; handling it selects the next.
    await win.getByRole('button', { name: 'Handled', exact: true }).click();
    await expect(heading(win, '2 customers are waiting')).toBeVisible();
    await expect(win.getByText('Sample Customer A')).toHaveCount(0);

    // In the dock, handling the customer moves the dock on to the next one waiting.
    await win.keyboard.press('Enter');
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer B');
    await win.locator('.dock-bar').getByRole('button', { name: /Handled/ }).click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer C');
    await win.keyboard.press('Escape');

    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'line-sparse.png') });
    await win.keyboard.press('s');
    await expect(heading(win, 'Nobody is waiting')).toBeVisible();
    await quit(app, win);

    const saved = JSON.parse(readFileSync(join(data, 'overrides.json'), 'utf8'));
    expect(saved['test-wa']['a@c.us'].kind).toBe('handled');
    expect(saved['test-wa']['b@c.us'].kind).toBe('handled');
    expect(saved['test-wa']['c@c.us'].kind).toBe('snoozed');

    ({ app, win } = await open(data));
    await expect(heading(win, 'Nobody is waiting')).toBeVisible();

    // Set aside lists all four with why; only the owner's marks can be put back.
    await win.keyboard.press('Control+k');
    await win.getByRole('textbox', { name: 'Search' }).fill('Set aside');
    await win.keyboard.press('Enter');
    await expect(heading(win, 'Set aside')).toBeVisible();
    const rowOf = (name: string) => win.getByRole('row').filter({ hasText: name });
    await expect(rowOf('Sample Customer A')).toContainText('Handled');
    await expect(rowOf('Sample Customer C')).toContainText('Snoozed');
    await expect(rowOf('Sample Customer D')).toContainText('Closed by rule');
    await expect(rowOf('Sample Customer D').getByRole('button', { name: 'Put back' })).toHaveCount(0);

    await rowOf('Sample Customer C').getByRole('button', { name: 'Put back' }).click();
    await expect(rowOf('Sample Customer C')).toHaveCount(0);

    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: /^The line/ }).click();
    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('a chat near its target alerts exactly once, across passes and a restart, and the alert opens its chat', async () => {
  const now = Date.now();
  const data = dataFolder({ accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true }] });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'test-wa': { capturedAt: now, chats: [{
      conversationKey: 'e@c.us', customerName: 'Sample Customer E', unread: 1, lastActivity: now - 13.4 * 60_000,
      preview: 'Could you call me back at seven?', awaiting: true, lastMessageFromMe: false, contactPhone: '',
      hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
    }] },
  }));

  let { app, win } = await open(data);
  try {
    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    // The alert pass runs with every push, every five seconds: wait for several.
    await expect.poll(() => alertLines(data).length, { timeout: 15_000 }).toBe(1);
    await win.waitForTimeout(11_000);
    expect(alertLines(data)).toEqual([expect.objectContaining({ kind: 'near-target', account: 'test-wa' })]);

    // What a click on the toast does: main asks the screens for the chat.
    await app.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].webContents.send('open', 'dock', 'test-wa', 'Sample Customer E'));
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer E');
    await quit(app, win);

    ({ app, win } = await open(data));
    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    await win.waitForTimeout(6_000);
    expect(alertLines(data)).toHaveLength(1);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('reports show the recorded days, measured replies and who is still owed a call', async () => {
  const now = Date.now();
  const HOUR = 3_600_000;
  const key = (at: number) => { const d = new Date(at); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };
  const byHour = (h: number, n: number) => Array.from({ length: 24 }, (_, i) => (i === h ? n : 0));
  const day = (at: number, o: Record<string, unknown>) => ({
    day: key(at), customersWrote: 0, wroteByHour: Array(24).fill(0), replies: 0, medianReplyMinutes: null, repliesWithinTarget: 0,
    targetMinutes: 15, waitingOverADayAtFirstRead: null, reopened: 0, missedCalls: 0, ...o,
  });
  const data = dataFolder({ accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true }] });
  writeFileSync(join(data, 'history.json'), JSON.stringify({
    'test-wa': { watchStart: now - 3 * 24 * HOUR, seen: {}, days: [
      day(now - 24 * HOUR, { customersWrote: 4, wroteByHour: byHour(11, 4), reopened: 1, waitingOverADayAtFirstRead: 2 }),
      day(now, { customersWrote: 3, wroteByHour: byHour(14, 3), missedCalls: 1, waitingOverADayAtFirstRead: 1 }),
    ] },
  }));
  writeFileSync(join(data, 'response-times.json'), JSON.stringify({
    pending: {}, watchStart: { 'test-wa': now - 3 * 24 * HOUR },
    samples: { 'test-wa': [{ answeredAt: now - 60_000, minutes: 10 }, { answeredAt: now - 2 * 60_000, minutes: 30 }] },
  }));
  const chat = (k: string, name: string, minutesAgo: number, o: Record<string, unknown>) => ({
    conversationKey: k, customerName: name, unread: 1, lastActivity: now - minutesAgo * 60_000, preview: '', awaiting: true,
    lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '', ...o,
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [
    chat('f@c.us', 'Sample Caller F', 20, { lastMessageType: 'call_log', lastCallOutcome: 'Missed' }),
    chat('g@c.us', 'Sample Customer G', 2 * 24 * 60, { preview: 'Could you send the price list?' }),
    chat('h@c.us', 'Sample Caller H', 5, { awaiting: false, lastMessageFromMe: true, preview: 'Sorry we missed you' }),
  ] } }));
  // One call not returned, one answered by message 12 minutes after it.
  writeFileSync(join(data, 'calls.json'), JSON.stringify({
    'test-wa|f@c.us|1': { account: 'test-wa', key: 'f@c.us', at: now - 20 * 60_000, returnedAt: null, returnedBy: null },
    'test-wa|h@c.us|1': { account: 'test-wa', key: 'h@c.us', at: now - 17 * 60_000, returnedAt: now - 5 * 60_000, returnedBy: 'message' },
  }));

  const { app, win } = await open(data);
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Reports', exact: true }).click();
    await expect(heading(win, 'Last 7 days: 50% answered on time')).toBeVisible();
    await expect(win.getByText('Customers, backlog and calls are recorded from')).toBeVisible();
    await expect(win.locator('.fact').filter({ hasText: 'Customers who wrote' })).toContainText('7');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'reports-overview.png') });

    await win.getByRole('group', { name: 'Report' }).getByRole('button', { name: 'Reply times' }).click();
    await expect(heading(win, 'Last 7 days: median first reply 10 min')).toBeVisible();
    await expect(win.getByRole('row').filter({ hasText: 'Test front desk' })).toContainText('50%');

    await win.getByRole('group', { name: 'Report' }).getByRole('button', { name: 'Backlog and reopened' }).click();
    await expect(heading(win, '1 customer has waited more than a day')).toBeVisible();
    await expect(win.getByRole('row').filter({ hasText: 'Sample Customer G' })).toContainText('Could you send the price list?');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'reports-backlog.png') });

    await win.getByRole('group', { name: 'Report' }).getByRole('button', { name: 'Missed calls' }).click();
    await expect(heading(win, '1 missed call has not been returned')).toBeVisible();
    await expect(win.getByText(/2 missed calls, 1 returned, a median 12 min later/)).toBeVisible();
    await expect(win.getByRole('row').filter({ hasText: 'Sample Caller H' })).toContainText('Answered by message 12 min later');
    await expect(win.getByRole('row').filter({ hasText: 'Sample Caller H' }).getByRole('button', { name: 'Open chat' })).toHaveCount(0);
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'reports-calls.png') });
    await win.getByRole('row').filter({ hasText: 'Sample Caller F' }).getByRole('button', { name: 'Open chat' }).click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Caller F');

    await win.keyboard.press('Escape');
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Reports', exact: true }).click();
    await win.getByRole('group', { name: 'Range' }).getByRole('button', { name: 'Today' }).click();
    await win.getByRole('group', { name: 'Report' }).getByRole('button', { name: 'Overview' }).click();
    await expect(heading(win, 'Today: 50% answered on time')).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('Open chat goes to the conversation: WhatsApp opens it, Instagram filters the list and stops', async () => {
  const now = Date.now();
  const fixture = (path: string) => pathToFileURL(join(V6, 'tests', 'fixtures', path)).href;
  const data = dataFolder({ accounts: [
    { id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: fixture('whatsapp-web.html'), professional: true },
    { id: 'test-ig', name: 'Test Instagram', channel: 'instagram', url: fixture('direct/inbox/index.html'), professional: true },
  ] });
  const chat = (k: string, name: string, minutesAgo: number, phone = '') => ({
    conversationKey: k, customerName: name, unread: 1, lastActivity: now - minutesAgo * 60_000, preview: 'Is there space this week?', awaiting: true,
    lastMessageFromMe: false, contactPhone: phone, hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'test-wa': { capturedAt: now, chats: [
      chat('al@c.us', 'Sample Customer Al', 9),
      chat('hidden@c.us', 'Sample Customer Hidden', 8),
      chat('923001112233@c.us', '+92 300 1112233', 7, '923001112233'),
    ] },
    'test-ig': { capturedAt: now, chats: [chat('333', 'Sample Insta Kay🦋', 6)] },
  }));
  /** Runs an expression in the account page whose address contains `part`. */
  const inPage = (part: string, expression: string) => app.evaluate(({ webContents }, [p, e]) =>
    webContents.getAllWebContents().find((w) => w.getURL().includes(p))?.executeJavaScript(e), [part, expression] as const);
  const focusLines = () => readFileSync(join(data, 'app.log'), 'utf8').split('\n').filter((l) => l.includes('"event":"focus"')).map((l) => JSON.parse(l));

  const { app, win } = await open(data);
  try {
    await expect(heading(win, '4 customers are waiting')).toBeVisible();
    const arrived = () => focusLines().filter((l) => l.result === 'arrived').length;
    const openChat = async (name: string) => {
      const before = arrived();
      await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: /^The line/ }).click();
      await win.locator('.queue .row').filter({ hasText: name }).click();
      await win.locator('.queue .row.sel').getByRole('button', { name: 'Open chat' }).click();
      // Each request finishes before the next, so none is replaced by a newer click.
      await expect.poll(arrived, { timeout: 15_000 }).toBe(before + 1);
    };

    // A name that another chat's title starts with: only the exact one may open.
    await openChat('Sample Customer Al');
    await expect.poll(() => inPage('whatsapp-web.html', "document.querySelector('#main header span')?.title ?? ''")).toBe('Sample Customer Al');
    // Not drawn on screen: opened through WhatsApp's own open-chat command, looked up by its key.
    await openChat('Sample Customer Hidden');
    await expect.poll(() => inPage('whatsapp-web.html', "document.querySelector('#main header span')?.title ?? ''"), { timeout: 15_000 }).toBe('Sample Customer Hidden');
    // An unsaved number, matched by its digits.
    await openChat('+92 300 1112233');
    await expect.poll(() => inPage('whatsapp-web.html', "document.querySelector('#main header span')?.title ?? ''"), { timeout: 15_000 }).toBe('+92 300 1112233');
    expect(await inPage('whatsapp-web.html', 'JSON.stringify(window.__opened.filter((t) => t.includes("Alpha")))')).toBe('[]');

    // Instagram: the list filtered by the letters of the name, and no thread opened.
    await openChat('Sample Insta Kay');
    await expect.poll(() => inPage('direct/inbox', 'window.__filteredBy')).toBe('Sample Insta Kay');
    expect(await inPage('direct/inbox', 'window.__threadOpened')).toBe(false);

    expect(arrived()).toBe(4);
    expect(focusLines().every((l) => !JSON.stringify(l).includes('Sample'))).toBe(true);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('the weekly report is computed from the figures, and saves as PDF, CSV and image without names unless asked', async () => {
  const now = Date.now();
  const HOUR = 3_600_000;
  const key = (at: number) => { const d = new Date(at); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };
  const day = (at: number, o: Record<string, unknown>) => ({
    day: key(at), customersWrote: 0, wroteByHour: Array(24).fill(0), replies: 0, medianReplyMinutes: null, repliesWithinTarget: 0,
    targetMinutes: 15, waitingOverADayAtFirstRead: null, reopened: 0, missedCalls: 0, ...o,
  });
  const data = dataFolder({ accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }] });
  const exportsDir = join(data, 'exports');
  mkdirSync(exportsDir);
  writeFileSync(join(data, 'history.json'), JSON.stringify({
    'test-wa': { watchStart: now - 2 * HOUR, seen: {}, days: [day(now, { customersWrote: 6, replies: 2, repliesWithinTarget: 1, medianReplyMinutes: 10, missedCalls: 1, waitingOverADayAtFirstRead: 1 })] },
  }));
  writeFileSync(join(data, 'response-times.json'), JSON.stringify({
    pending: {}, watchStart: { 'test-wa': now - 2 * HOUR }, samples: { 'test-wa': [{ answeredAt: now - 60_000, minutes: 10 }, { answeredAt: now - 120_000, minutes: 40 }] },
  }));
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [{
    conversationKey: 'g@c.us', customerName: 'Sample Customer Owed', unread: 1, lastActivity: now - 2 * 24 * HOUR, preview: 'Hello?', awaiting: true,
    lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  }] } }));

  const app = await electron.launch({ args: ['app/main.ts'], cwd: V6, env: { ...process.env, UM_DATA: data, UM_V5: join(data, 'no-v5'), UM_EXPORT_DIR: exportsDir } });
  const win = await app.firstWindow();
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Reports', exact: true }).click();
    await win.getByRole('group', { name: 'Report' }).getByRole('button', { name: 'Weekly report' }).click();
    // Nothing was recorded last week, so the report opens on this week.
    const doc = win.locator('[data-weekly-doc]');
    await expect(doc.getByRole('heading', { level: 1 })).toHaveText(/^This week so far: /);
    await expect(doc).toContainText('6 customers wrote to 1 location. 50% got a first reply within target.');
    await expect(doc).toContainText('Test front desk');
    await expect(doc).not.toContainText('Sample Customer Owed');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'weekly.png') });

    const save = async (button: string, pattern: RegExp) => {
      await win.getByRole('button', { name: button }).click();
      await expect(win.getByRole('status')).toHaveText(pattern, { timeout: 30_000 });
    };
    await save('Save as PDF', /^Saved to .*Weekly report \d{4}-\d{2}-\d{2}\.pdf$/);
    await save('Save figures as CSV', /^Saved to .*Weekly figures \d{4}-\d{2}-\d{2}\.csv$/);
    await save('Copy as image', /^Saved to .*Weekly report \d{4}-\d{2}-\d{2}\.png$/);

    const files = readdirSync(exportsDir);
    const pdf = readFileSync(join(exportsDir, files.find((f) => f.endsWith('.pdf'))!));
    expect(pdf.subarray(0, 5).toString()).toBe('%PDF-');
    const png = readFileSync(join(exportsDir, files.find((f) => f.endsWith('.png'))!));
    if (process.env.UM_SHOTS) writeFileSync(join(process.env.UM_SHOTS, 'weekly-export.png'), png);
    expect(png.subarray(1, 4).toString()).toBe('PNG');
    // A real page, not a blank capture: the image is the report's width and taller than a screenful of nothing.
    expect(png.readUInt32BE(16)).toBeGreaterThanOrEqual(800);
    expect(png.readUInt32BE(20)).toBeGreaterThan(400);
    const csv = readFileSync(join(exportsDir, files.find((f) => f.endsWith('.csv'))!), 'utf8');
    expect(csv.split('\r\n')[1]).toMatch(/^\d{4}-\d{2}-\d{2},Test front desk,Main branch,6,2,1,10,0,1,1$/);
    expect(csv).not.toContain('Sample Customer');

    // Names only when switched on, and the choice is kept.
    await win.getByRole('switch', { name: 'Customer names' }).click();
    await expect(doc).toContainText('Sample Customer Owed');
    const config = JSON.parse(readFileSync(join(data, 'config.json'), 'utf8'));
    expect(config.settings.weeklyReport.include.names).toBe(true);
    // The export never replaced the main window's content or closed it.
    await expect(win.getByRole('heading', { level: 1, name: 'The weekly report' })).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('opening hours and holidays stop a wait from growing, and are kept', async () => {
  const now = Date.now();
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }],
    locations: [{ name: 'Main branch' }, { name: 'North branch' }],
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [{
    conversationKey: 'h@c.us', customerName: 'Sample Customer H', unread: 1, lastActivity: now - 30 * 60_000, preview: 'Are you open today?', awaiting: true,
    lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  }] } }));
  const today = new Date().toLocaleDateString('en-US', { weekday: 'long' });
  const d = new Date();
  const todayKey = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  const config = () => JSON.parse(readFileSync(join(data, 'config.json'), 'utf8'));
  const token = (minutes: number) => win.getByRole('button', { name: `Sample Customer H, waiting ${minutes} min on` });

  const { app, win } = await open(data);
  try {
    // No hours yet: the wait counts around the clock.
    await expect(token(30)).toBeVisible();

    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('button', { name: 'Opening hours', exact: true }).click();
    await win.getByRole('switch', { name: 'Count waits only while open' }).click();
    // Closed all day today: no minute of the last half hour was inside opening hours.
    if (await win.getByRole('switch', { name: `${today} open` }).getAttribute('aria-checked') === 'true') {
      await win.getByRole('switch', { name: `${today} open` }).click();
    }
    await expect.poll(() => config().locations[0].hours?.week?.[d.getDay()] === null, { timeout: 5_000 }).toBe(true);
    expect(config().locations[0].hours.enabled).toBe(true);

    // Open all day today, but a holiday for this location only: still standing still.
    await win.getByRole('switch', { name: `${today} open` }).click();
    await win.getByLabel(`${today} opens`).fill('00:00');
    await win.getByLabel(`${today} closes`).fill('23:45');
    await win.getByLabel('Name').fill('Sample closed day');
    await win.getByLabel('Date').fill(todayKey);
    await win.getByRole('checkbox', { name: 'Main branch' }).check();
    await win.getByRole('button', { name: 'Add', exact: true }).click();
    await expect(win.getByText('Sample closed day')).toBeVisible();
    expect(config().holidays).toEqual([{ name: 'Sample closed day', date: todayKey, locations: ['Main branch'] }]);
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'opening-hours.png'), fullPage: true });

    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: /^The line/ }).click();
    await expect(token(0)).toBeVisible();

    // Hours switched back off: the whole half hour counts again.
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('button', { name: 'Opening hours', exact: true }).click();
    await win.getByRole('switch', { name: 'Count waits only while open' }).click();
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: /^The line/ }).click();
    await expect(token(30)).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('the morning digest opens once a day, with who is still owed and how many wrote since', async () => {
  const now = Date.now();
  const HOUR = 3_600_000;
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }],
    locations: [{ name: 'Main branch' }],
    settings: { morningDigest: true },
  });
  const chat = (k: string, name: string, at: number, preview: string) => ({
    conversationKey: k, customerName: name, unread: 1, lastActivity: at, preview, awaiting: true,
    lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  // No opening hours: the day starts at midnight, so "owed" is anyone waiting since before today.
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [
    chat('owed@c.us', 'Sample Customer Owed', now - 30 * HOUR, 'Is Saturday possible?'),
    chat('new@c.us', 'Sample Customer New', now - 60_000, 'Hello'),
  ] } }));

  let { app, win } = await open(data);
  try {
    await expect(heading(win, /^Good (morning|afternoon|evening)\. 1 customer wrote since midnight\.$/)).toBeVisible();
    await expect(win.getByText('Answer the 1 still owed from yesterday first.')).toBeVisible();
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'digest.png') });
    const owed = win.getByRole('row').filter({ hasText: 'Sample Customer Owed' });
    await expect(owed).toContainText('Is Saturday possible?');
    await expect(win.getByRole('row').filter({ hasText: 'Sample Customer New' })).toHaveCount(0);
    await owed.getByRole('button', { name: 'Open chat' }).click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer Owed');
    await quit(app, win);
    expect(JSON.parse(readFileSync(join(data, 'digest.json'), 'utf8')).lastShownDay).toMatch(/^\d{4}-\d{2}-\d{2}$/);

    // A second opening the same day goes straight to the line.
    ({ app, win } = await open(data));
    await expect(heading(win, '2 customers are waiting')).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('reports and their exports follow the location chosen in the title bar', async () => {
  const now = Date.now();
  const HOUR = 3_600_000;
  const data = dataFolder({
    accounts: [
      { id: 'wa-main', name: 'Main front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' },
      { id: 'wa-north', name: 'North front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'North branch' },
    ],
    locations: [{ name: 'Main branch' }, { name: 'North branch' }],
  });
  const exportsDir = join(data, 'exports');
  mkdirSync(exportsDir);
  const chat = (k: string, name: string) => ({
    conversationKey: k, customerName: name, unread: 1, lastActivity: now - 5 * 60_000, preview: 'Hello', awaiting: true,
    lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'wa-main': { capturedAt: now, chats: [chat('m1@c.us', 'Sample Main One'), chat('m2@c.us', 'Sample Main Two')] },
    'wa-north': { capturedAt: now, chats: [chat('n1@c.us', 'Sample North One')] },
  }));
  // Main answers everyone on time; North answers nobody on time.
  writeFileSync(join(data, 'response-times.json'), JSON.stringify({
    pending: {}, watchStart: { 'wa-main': now - 2 * HOUR, 'wa-north': now - 2 * HOUR },
    samples: { 'wa-main': [{ answeredAt: now - 60_000, minutes: 5 }, { answeredAt: now - 120_000, minutes: 6 }], 'wa-north': [{ answeredAt: now - 60_000, minutes: 50 }] },
  }));

  const app = await electron.launch({ args: ['app/main.ts'], cwd: V6, env: { ...process.env, UM_DATA: data, UM_V5: join(data, 'no-v5'), UM_EXPORT_DIR: exportsDir } });
  const win = await app.firstWindow();
  try {
    const locations = win.getByRole('group', { name: 'Locations' });
    // The counts come from the whole queue.
    await expect(locations.getByRole('button', { name: /^Main branch 2$/ })).toBeVisible();
    await expect(locations.getByRole('button', { name: /^North branch 1$/ })).toBeVisible();

    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Reports', exact: true }).click();
    await expect(heading(win, 'Last 7 days: 67% answered on time')).toBeVisible();

    await locations.getByRole('button', { name: /^North branch/ }).click();
    await expect(heading(win, 'Last 7 days: 0% answered on time')).toBeVisible();
    await expect(win.getByText('North branch', { exact: true }).first()).toBeVisible();
    await win.getByRole('group', { name: 'Report' }).getByRole('button', { name: 'Reply times' }).click();
    await expect(win.getByRole('row').filter({ hasText: 'Main front desk' })).toHaveCount(0);
    await expect(win.getByRole('row').filter({ hasText: 'North front desk' })).toBeVisible();

    await win.getByRole('button', { name: 'Export', exact: true }).click();
    await expect(win.getByRole('status')).toHaveText(/North branch\.csv$/);
    const csv = readFileSync(join(exportsDir, readdirSync(exportsDir)[0]), 'utf8');
    expect(csv).not.toContain('Main front desk');

    await locations.getByRole('button', { name: /^Main branch/ }).click();
    await expect(win.getByRole('row').filter({ hasText: 'Main front desk' })).toBeVisible();
    await expect(win.getByRole('row').filter({ hasText: 'North front desk' })).toHaveCount(0);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('accounts can be added, edited and removed, and removing one forgets its data', async () => {
  const now = Date.now();
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }],
    locations: [{ name: 'Main branch' }],
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [{
    conversationKey: 'x@c.us', customerName: 'Sample Customer X', unread: 1, lastActivity: now - 5 * 60_000, preview: 'Hello', awaiting: true,
    lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  }] } }));
  writeFileSync(join(data, 'calls.json'), JSON.stringify({ 'test-wa|x@c.us|1': { account: 'test-wa', key: 'x@c.us', at: now - 60_000, returnedAt: null, returnedBy: null } }));
  const config = () => JSON.parse(readFileSync(join(data, 'config.json'), 'utf8'));
  const rail = (name: string | RegExp) => win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name, exact: typeof name === 'string' });

  const { app, win } = await open(data);
  try {
    await expect(heading(win, '1 customer is waiting')).toBeVisible();

    // Add: another page, at a new location. ".invalid" never resolves, so the test reaches no real site.
    await rail('Accounts').click();
    await win.getByRole('button', { name: 'Add an account' }).click();
    const add = win.getByRole('dialog', { name: 'Add an account' });
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'add-account.png') });
    await add.getByRole('radio', { name: /Another page/ }).click();
    await add.getByRole('button', { name: 'Add and sign in' }).click();
    await expect(add.getByRole('alert')).toContainText('web address');
    await add.getByLabel('Web address').fill('https://booking.invalid/');
    await add.getByLabel('Name').fill('Booking page');
    await add.getByLabel('Location').fill('North branch');
    await add.getByRole('button', { name: 'Add and sign in' }).click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Booking page');
    const added = config().accounts.find((a: { name: string }) => a.name === 'Booking page');
    expect([added.channel, added.location, added.professional, added.url]).toEqual(['custom', 'North branch', false, 'https://booking.invalid/']);
    expect(config().locations.map((l: { name: string }) => l.name)).toEqual(['Main branch', 'North branch']);

    // Edit: rename, and stop counting it. With nothing counted, nothing is read.
    await rail('Accounts').click();
    await win.getByRole('button', { name: 'Edit', exact: true }).first().click();
    const edit = win.getByRole('dialog', { name: 'Edit account' });
    await edit.getByLabel('Name').fill('Reception');
    await edit.getByRole('switch', { name: 'Count its customers' }).click();
    await edit.getByRole('button', { name: 'Save' }).click();
    await expect(edit).toHaveCount(0);
    expect(config().accounts.find((a: { id: string }) => a.id === 'test-wa')).toMatchObject({ name: 'Reception', professional: false });
    await expect(win.getByText('A personal account: its page stays open, and nobody on it is counted.')).toBeVisible();
    await rail(/^The line/).click();
    await expect(heading(win, 'No accounts are being read yet')).toBeVisible();

    // Remove: asks first, then the account and everything stored under it are gone.
    await rail('Accounts').click();
    await win.getByRole('button', { name: 'Edit', exact: true }).first().click();
    await win.getByRole('dialog', { name: 'Edit account' }).getByRole('button', { name: 'Remove account' }).click();
    const remove = win.getByRole('dialog', { name: 'Remove account' });
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'remove-account.png') });
    await expect(remove).toContainText('Linked devices');
    await remove.getByRole('button', { name: 'Remove and wipe login' }).click();
    await expect(remove).toHaveCount(0);
    await expect.poll(() => config().accounts.map((a: { name: string }) => a.name)).toEqual(['Booking page']);
    expect(config().locations.map((l: { name: string }) => l.name)).toEqual(['Main branch', 'North branch']);
    expect(Object.keys(JSON.parse(readFileSync(join(data, 'snapshot.json'), 'utf8')))).toEqual([]);
    expect(JSON.parse(readFileSync(join(data, 'calls.json'), 'utf8'))).toEqual({});
    expect(readFileSync(join(data, 'app.log'), 'utf8')).not.toContain('Reception');
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('the reading record and the reader timeline show what the reads actually did', async () => {
  const now = Date.now();
  const data = dataFolder({
    accounts: [
      { id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' },
      { id: 'test-wa2', name: 'Second desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' },
    ],
    locations: [{ name: 'Main branch' }],
  });
  const event = (account: string, minutesAgo: number, outcome: string, o: Record<string, unknown> = {}) =>
    ({ account, channel: 'whatsapp', at: now - minutesAgo * 60_000, outcome, chats: null, waiting: null, stage: null, ...o });
  writeFileSync(join(data, 'events.json'), JSON.stringify({
    'test-wa': [
      event('test-wa', 12, 'read', { chats: 500, waiting: 6 }),
      event('test-wa', 11, 'read', { chats: 500, waiting: 6 }),
      event('test-wa', 10, 'reload'),
      event('test-wa', 9, 'signed-out'),
    ],
    'test-wa2': [event('test-wa2', 9, 'empty', { stage: 'empty' })],
  }));
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [] } }));

  const { app, win } = await open(data);
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Accounts', exact: true }).click();
    await win.getByRole('button', { name: 'Figures' }).first().click();
    await win.getByRole('button', { name: 'Reading record' }).click();

    // Signed in or out depends on whether the app's own first read has landed yet, so only the seeded lines are
    // checked here; how long a sign-out has lasted is covered by core/events.test.ts.
    await expect(heading(win, /^Test front desk is signed (in|out)$/)).toBeVisible();
    const record = win.getByRole('main');
    await expect(record).toContainText('2 good reads');
    await expect(record).toContainText('The last saw 500 chats read, 6 waiting.');
    await expect(record).toContainText('Page reloaded');
    await expect(record).toContainText('Sign-in screen');
    await expect(record).not.toContainText('Sample figures');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'reading-record.png') });

    // The reader is one story across both accounts on the channel.
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Accounts', exact: true }).click();
    await win.getByRole('button', { name: /WhatsApp reader/ }).click();
    const reader = win.getByRole('main');
    await expect(reader).toContainText('Nothing came back');
    await expect(reader).toContainText('2 accounts stopped reading');
    await expect(reader).not.toContainText('Sample figures');
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('the line shows every channel, sums up the long waits, and a click in it opens that chat', async () => {
  const now = Date.now();
  const data = dataFolder({
    accounts: [
      { id: 'test-wa', name: 'F-11 WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'F-11' },
      { id: 'test-ig', name: 'DHA-2 Instagram', channel: 'instagram', url: 'about:blank', professional: true, location: 'DHA-2' },
    ],
    locations: [{ name: 'F-11' }, { name: 'DHA-2' }],
  });
  const chat = (key: string, customer: string, minutesAgo: number, preview = 'Hello') => ({
    conversationKey: key, customerName: customer, unread: 1, lastActivity: now - minutesAgo * 60_000, preview,
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'test-wa': { capturedAt: now, chats: [
      chat('a@c.us', 'Sample Customer A', 4),
      chat('b@c.us', 'Sample Customer B', 12),
      chat('c@c.us', 'Sample Customer C', 9 * 60, 'Still waiting for an answer'),
      chat('d@c.us', '+92 300 0000042', 8 * 60, 'Photo'),
    ] },
    'test-ig': { capturedAt: now, chats: [chat('ig-1', 'Sample Customer E', 40)] },
  }));

  const { app, win } = await open(data);
  try {
    await expect(heading(win, '5 customers are waiting')).toBeVisible();

    // The indicators above the line, counted from the rows on screen.
    const fact = (label: string) => win.locator('.fact').filter({ hasText: label });
    await expect(fact('Waiting now')).toContainText('5');
    await expect(fact('Waiting now')).toContainText('4 WhatsApp · 1 Instagram');
    await expect(fact('Longest wait')).toContainText('Sample Customer C');
    await expect(fact('Past target')).toContainText('3');

    // The chart names every channel, and each lane counts its own.
    const line = win.getByRole('region', { name: 'The line' });
    await expect(line).toContainText('4 WhatsApp');
    await expect(line).toContainText('1 Instagram');
    // Two at F-11 have waited over an hour: one chip, not a pile, and it names the longest.
    const chip = line.getByRole('button', { name: /2 waiting more than an hour at F-11/ });
    await expect(chip).toContainText('over 1 h');
    await expect(chip).toContainText('longest 9 h');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'the-line.png') });

    // A click in the chart opens that conversation: first a token, then the chip.
    await line.getByRole('button', { name: /^Sample Customer B/ }).click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer B');
    await win.keyboard.press('Escape');
    await chip.click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer C');
    await win.keyboard.press('Escape');
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('the customer panel keeps a note and tags across a restart, and a saved reply copies', async () => {
  const now = Date.now();
  const data = dataFolder({ accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true }] });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'test-wa': { capturedAt: now, chats: [{
      conversationKey: 'a@c.us', customerName: 'Sample Customer A', unread: 1, lastActivity: now - 20 * 60_000,
      preview: 'Do you have space on Friday?', awaiting: true, lastMessageFromMe: false, contactPhone: '',
      hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
    }] },
  }));

  let { app, win } = await open(data);
  try {
    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    await win.getByRole('button', { name: 'Open chat' }).first().click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer A');
    const panel = win.getByRole('complementary', { name: 'About this customer' });
    await expect(panel).not.toContainText('Sample figures');
    await expect(panel).toContainText('Waiting since');
    await expect(panel).toContainText('None measured yet on this chat');

    await panel.getByRole('textbox', { name: 'Note about Sample Customer A' }).fill('Prefers a call back in the evening.');
    await panel.getByRole('button', { name: '+ Add' }).click();
    await panel.getByRole('textbox', { name: 'New tag' }).fill('Regular');
    await win.keyboard.press('Enter');
    await expect(panel.getByRole('button', { name: /Remove the tag Regular/ })).toBeVisible();
    await expect.poll(() => {
      const saved = JSON.parse(readFileSync(join(data, 'customers.json'), 'utf8'))['test-wa|a@c.us'];
      return [saved?.note, saved?.tags];
    }).toEqual(['Prefers a call back in the evening.', ['Regular']]);

    // A saved reply is written in Settings and copied from the panel; the app never sends one.
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings' }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Saved replies' }).click();
    await win.getByRole('textbox', { name: 'Name' }).fill('Prices');
    await win.getByRole('textbox', { name: 'Reply' }).fill('Our current price list is on the way.');
    await win.getByRole('button', { name: 'Add reply' }).click();
    await expect.poll(() => JSON.parse(readFileSync(join(data, 'config.json'), 'utf8')).settings.savedReplies)
      .toEqual([{ title: 'Prices', body: 'Our current price list is on the way.' }]);
    await quit(app, win);

    // After a restart the note, the tag and the reply are all still there.
    ({ app, win } = await open(data));
    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    await win.getByRole('button', { name: 'Open chat' }).first().click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer A');
    const panel2 = win.getByRole('complementary', { name: 'About this customer' });
    await expect(panel2.getByRole('textbox', { name: 'Note about Sample Customer A' })).toHaveValue('Prefers a call back in the evening.');
    await expect(panel2.getByRole('button', { name: /Remove the tag Regular/ })).toBeVisible();
    await expect(panel2).toContainText('Prices');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'customer-panel.png') });
    await panel2.getByRole('button', { name: 'Copy' }).click();
    // Copying is asynchronous in the page, so wait for the text rather than read at once.
    await expect.poll(() => app.evaluate(({ clipboard }) => clipboard.readText())).toBe('Our current price list is on the way.');
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

// ---- accessibility -----------------------------------------------------------------------------------------

/** Every WCAG 2.1 A and AA rule axe knows, on one screen. Returns one line per problem so a failure says what and
 *  where without opening a report. */
async function axe(win: Page, where: string) {
  // Legacy mode runs axe inside the page itself; the default opens a second blank page to finish, which
  // Electron refuses (Target.createTarget is not supported).
  const { violations } = await new AxeBuilder({ page: win }).setLegacyMode().withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  return violations.flatMap((v) => v.nodes.map((n) => {
    const c = n.any.find((x) => x.id === 'color-contrast')?.data as { fgColor?: string; bgColor?: string; contrastRatio?: number; expectedContrastRatio?: string } | undefined;
    const why = c?.contrastRatio ? ` · ${c.fgColor} on ${c.bgColor} is ${c.contrastRatio}:1, needs ${c.expectedContrastRatio}` : '';
    return `${where} · ${v.id} (${v.impact}) · ${n.target.join(' ')}${why}`;
  }));
}

test('every main screen and a dialog pass the WCAG 2.1 AA checks, in light and in dark', async () => {
  test.setTimeout(180_000);
  const now = Date.now();
  const data = dataFolder({
    accounts: [
      { id: 'test-wa', name: 'Front desk WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' },
      { id: 'test-ig', name: 'Front desk Instagram', channel: 'instagram', url: 'about:blank', professional: true, location: 'Main branch' },
    ],
    locations: [{ name: 'Main branch' }],
  });
  const chat = (key: string, customer: string, minutesAgo: number) => ({
    conversationKey: key, customerName: customer, unread: 1, lastActivity: now - minutesAgo * 60_000, preview: 'Hello there',
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'test-wa': { capturedAt: now, chats: [chat('a', 'Sample Customer A', 4), chat('b', 'Sample Customer B', 12), chat('c', 'Sample Customer C', 90),
      chat('e', 'Sample Customer E', 46 * 60), chat('f', 'Sample Customer F', 6 * 24 * 60)] },
    'test-ig': { capturedAt: now, chats: [chat('d', 'Sample Customer D', 30)] },
  }));

  const { app, win } = await open(data);
  const problems: string[] = [];
  const rail = (name: string | RegExp) => win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name, exact: typeof name === 'string' });
  try {
    for (const theme of ['Light', 'Dark'] as const) {
      await win.getByRole('button', { name: theme, exact: true }).click();
      await rail(/^The line/).click();
      await expect(heading(win, '6 customers are waiting')).toBeVisible();
      // Waits past a day are said in days; past a week they are backlog, said in weeks in Reports.
      await expect(win.locator('.queue .row').filter({ hasText: 'Sample Customer E' })).toContainText('1day 22 h');
      await expect(win.locator('.queue .row').filter({ hasText: 'Sample Customer F' })).toContainText('6days');
      if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, `line-${theme.toLowerCase()}.png`) });
      problems.push(...await axe(win, `${theme} · the line`));

      await win.getByRole('button', { name: 'Open chat' }).first().click();
      await expect(win.locator('.dock-bar .who b')).toBeVisible();
      problems.push(...await axe(win, `${theme} · docked chat`));
      await win.keyboard.press('Escape');

      await rail('Accounts').click();
      await expect(heading(win, 'Accounts')).toBeVisible();
      problems.push(...await axe(win, `${theme} · accounts`));

      await win.getByRole('button', { name: 'Add an account' }).click();
      await expect(win.getByRole('dialog', { name: 'Add an account' })).toBeVisible();
      problems.push(...await axe(win, `${theme} · add an account`));
      await win.keyboard.press('Escape');

      await win.getByRole('button', { name: 'Figures' }).first().click();
      problems.push(...await axe(win, `${theme} · account figures`));
      await win.getByRole('button', { name: 'Reading record' }).click();
      problems.push(...await axe(win, `${theme} · reading record`));

      await rail('Reports').click();
      await expect(win.getByRole('group', { name: 'Report' })).toBeVisible();
      problems.push(...await axe(win, `${theme} · reports`));

      await rail('Settings').click();
      for (const section of ['Look and reading', 'Opening hours', 'Notifications', 'Saved replies']) {
        await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: section }).click();
        problems.push(...await axe(win, `${theme} · settings › ${section}`));
      }

      await win.keyboard.press('Control+k');
      await expect(win.getByRole('dialog', { name: 'Command palette' })).toBeVisible();
      problems.push(...await axe(win, `${theme} · command palette`));
      await win.keyboard.press('Escape');
      await win.keyboard.press('F1');
      await expect(win.getByRole('complementary', { name: /^Help: / })).toBeVisible();
      problems.push(...await axe(win, `${theme} · help drawer`));
      await win.getByRole('button', { name: 'All help' }).click();
      await expect(heading(win, 'Help')).toBeVisible();
      problems.push(...await axe(win, `${theme} · help screen`));
      await win.keyboard.press('Control+k');
      await win.keyboard.press('Escape');
    }
    expect(problems, problems.join('\n')).toEqual([]);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('staff and team chats are left out by a rule or a mark, listed in Set aside, and a mark can be put back', async () => {
  const now = Date.now();
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true }],
    settings: { notCustomers: { words: ['Staff'], numbers: [] } },
  });
  const chat = (key: string, customer: string, minutesAgo: number) => ({
    conversationKey: key, customerName: customer, unread: 1, lastActivity: now - minutesAgo * 60_000, preview: 'Is the order ready?',
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [
    chat('923001112233@c.us', 'Sample Customer A', 20),
    chat('923004445566@c.us', 'Bilal Staff Front Desk', 30),
    chat('923007778899@c.us', 'Sample Supplier', 10),
  ] } }));

  const { app, win } = await open(data);
  const config = () => JSON.parse(readFileSync(join(data, 'config.json'), 'utf8'));
  try {
    // The rule leaves the staff chat out from the start.
    await expect(heading(win, '2 customers are waiting')).toBeVisible();
    await expect(win.locator('.queue')).not.toContainText('Bilal Staff');

    // A mark from the dock leaves out one more, for good.
    await win.locator('.queue .row').filter({ hasText: 'Sample Supplier' }).dblclick();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Supplier');
    await win.locator('.dock-bar').getByRole('button', { name: 'Not a customer' }).click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer A');
    await expect.poll(() => JSON.parse(readFileSync(join(data, 'overrides.json'), 'utf8'))['test-wa']['923007778899@c.us']?.kind).toBe('excluded');
    await win.keyboard.press('Escape');
    await expect(heading(win, '1 customer is waiting')).toBeVisible();

    // Set aside lists both with the reason; only the mark can be put back.
    await win.keyboard.press('Control+k');
    await win.getByRole('textbox', { name: 'Search' }).fill('Set aside');
    await win.keyboard.press('Enter');
    await win.getByRole('group', { name: 'Show' }).getByRole('button', { name: 'Not a customer' }).click();
    const rowOf = (name: string) => win.getByRole('row').filter({ hasText: name });
    await expect(rowOf('Bilal Staff Front Desk')).toContainText('Name contains “Staff”');
    await expect(rowOf('Bilal Staff Front Desk').getByRole('button', { name: 'Put back' })).toHaveCount(0);
    await expect(rowOf('Sample Supplier')).toContainText('Marked as not a customer');
    await rowOf('Sample Supplier').getByRole('button', { name: 'Put back' }).click();
    await expect(rowOf('Sample Supplier')).toHaveCount(0);

    // A team number added in Settings is saved digits-only, however it was typed.
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings' }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Look and reading' }).click();
    await win.getByRole('textbox', { name: "The team's own numbers" }).fill('+92 300 7778899');
    await win.getByRole('textbox', { name: 'Names containing any of these words' }).click();
    await expect.poll(() => config().settings.notCustomers.numbers).toEqual(['923007778899']);

    // Settings › Privacy measures this PC rather than showing invented sizes, and says where each thing is managed.
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Privacy' }).click();
    const keptRow = (name: string) => win.getByRole('row').filter({ hasText: name });
    await expect(keptRow('Account logins')).toContainText('Accounts \u203a Remove');
    // The log is written from the first launch, so it has a real size; the table never says 'Sample figures'.
    await expect(keptRow('The support log')).toContainText(/\d+(\.\d)? (B|KB|MB)/);
    await expect(keptRow('All of it')).toContainText(/\d+(\.\d)? (B|KB|MB|GB)/);
    await expect(win.getByRole('main')).not.toContainText('Sample figures');
    // Nothing is downloaded in a test, so the model row says so instead of quoting a size this PC does not hold.
    await expect(keptRow('model')).toContainText('Nothing downloaded');

    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: /^The line/ }).click();
    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('help opens for the screen you are on, the Help screen holds every page, and the pictures load', async () => {
  const now = Date.now();
  const data = dataFolder({ accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true }] });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [{
    conversationKey: 'a@c.us', customerName: 'Sample Customer A', unread: 1, lastActivity: now - 5 * 60_000, preview: 'Hello',
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  }] } }));
  const { app, win } = await open(data);
  const loaded = (where: ReturnType<Page['locator']>) => where.locator('img').evaluateAll((imgs) =>
    Promise.all(imgs.map((i) => (i as HTMLImageElement).decode().then(() => (i as HTMLImageElement).naturalWidth > 0, () => false))));
  try {
    await expect(heading(win, '1 customer is waiting')).toBeVisible();

    // F1 opens this screen's page beside it, and closes it again.
    await win.keyboard.press('F1');
    const drawer = win.getByRole('complementary', { name: 'Help: The line' });
    await expect(drawer).toBeVisible();
    await expect(drawer).toContainText('Longest wait first');
    expect(await loaded(drawer)).not.toContain(false);
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'help-drawer.png') });
    await win.keyboard.press('F1');
    await expect(drawer).toHaveCount(0);

    // The ? on another screen opens that screen's page.
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Accounts', exact: true }).click();
    await win.getByRole('button', { name: 'Help for this screen' }).click();
    await expect(win.getByRole('complementary', { name: 'Help: Accounts' })).toContainText('Add an account');

    // A link in a page goes to the Help screen at that page.
    await win.getByRole('complementary', { name: 'Help: Accounts' }).getByRole('button', { name: 'Channel readers' }).first().click();
    await expect(heading(win, 'Help')).toBeVisible();
    await expect(win.getByRole('article', { name: 'Channel readers' })).toBeVisible();

    // Every page can be opened from the list, and every picture on it loads.
    const pages = win.getByRole('navigation', { name: 'Help pages' }).getByRole('button');
    const count = await pages.count();
    expect(count).toBeGreaterThan(15);
    for (let i = 0; i < count; i++) {
      const title = (await pages.nth(i).textContent()) ?? '';
      await pages.nth(i).click();
      const article = win.getByRole('article', { name: title });
      await expect(article.getByRole('heading', { level: 2, name: title })).toBeVisible();
      expect(await loaded(article), title).not.toContain(false);
    }
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'help-screen.png') });
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('the Google reviews reader reads the rating, the total and the reviews, stars from their colour, and logs no names', async () => {
  const fixtures = join(V6, 'tests', 'fixtures', 'google');
  const reviewsUrl = pathToFileURL(join(fixtures, 'reviews', 'index.html')).href;
  const data = dataFolder({
    accounts: [{ id: 'test-g', name: 'North branch Google', channel: 'googlebusiness', url: reviewsUrl, professional: false, location: 'North branch' }],
    locations: [{ name: 'North branch' }],
  });
  const { app, win } = await open(data);
  const log = () => readFileSync(join(data, 'app.log'), 'utf8');
  try {
    await expect.poll(log, { timeout: 60_000 }).toContain('"event":"reviews-read"');
    expect(log()).toContain('"event":"profile-read","account":"test-g","rating":4.6,"total":991');
    expect(log()).toMatch(/"event":"reviews-read","account":"test-g","reviews":4,"unanswered":3,"more":true,"starsRead":4/);
    // The log is counts only: no reviewer and no review text.
    expect(log()).not.toMatch(/Sample Reviewer|forty minutes/);

    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Reviews', exact: true }).click();
    await expect(heading(win, '3 recent reviews have no reply')).toBeVisible();
    const card = win.locator('.panel').filter({ hasText: 'North branch' }).first();
    await expect(card).toContainText('4.6');
    await expect(card).toContainText('991 reviews');
    await expect(card).toContainText('3 without a reply');
    // Worst first, the stars read from their colour, and the long review expanded.
    const rows = win.locator('.review');
    await expect(rows.nth(0)).toContainText('Sample Reviewer A');
    await expect(rows.nth(0).getByLabel('1 of 5 stars')).toBeVisible();
    await expect(rows.nth(0)).toContainText('Will not be back.');
    await expect(rows.nth(1)).toContainText('Sample Reviewer D');
    await expect(rows.nth(2)).toContainText('A rating with no words.');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'reviews.png') });
    expect(await axe(win, 'reviews'), 'accessibility').toEqual([]);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('when the WhatsApp store bridge finds nothing, the saved chat list still yields the customers waiting', async () => {
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', professional: true,
      url: pathToFileURL(join(V6, 'tests', 'fixtures', 'whatsapp-saved-list.html')).href }],
  });
  const { app, win } = await open(data);
  const log = () => readFileSync(join(data, 'app.log'), 'utf8');
  try {
    await expect.poll(log, { timeout: 90_000 }).toContain('"source":"saved-list"');
    // A waits, B was answered, the group and the account's own number are not customers, and C's number came
    // from the contact list because the chat itself is keyed by a privacy id.
    expect(log()).toMatch(/"event":"read","account":"test-wa"[^\n]*"chats":3,"awaiting":2/);
    await expect(heading(win, '2 customers are waiting')).toBeVisible();
    await expect(win.locator('.queue .row').filter({ hasText: 'Sample Customer A' })).toContainText('Is the order ready?');
    await expect(win.locator('.queue .row').filter({ hasText: 'Sample Customer C' })).toBeVisible();
    await expect(win.locator('.queue')).not.toContainText('Sample Team Group');
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('break test: one channel whose reader throws costs only its own figures, and says so everywhere', async () => {
  const fixtures = join(V6, 'tests', 'fixtures');
  const data = dataFolder({
    // Instagram first, so every pass reads the broken page before the working one.
    accounts: [
      { id: 'test-ig', name: 'Test Instagram', channel: 'instagram', professional: true, location: 'Main branch',
        url: pathToFileURL(join(fixtures, 'broken-instagram.html')).href },
      { id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', professional: true, location: 'Main branch',
        url: pathToFileURL(join(fixtures, 'whatsapp-saved-list.html')).href },
    ],
    locations: [{ name: 'Main branch' }],
    settings: { readEverySeconds: 30 },
  });
  const reports = mkdtempSync(join(tmpdir(), 'um-support-'));
  const { app, win } = await open(data, { UM_EXPORT_DIR: reports });
  const events = () => readFileSync(join(data, 'app.log'), 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l) as { event: string; account?: string });
  const rail = (name: string) => win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name, exact: true });
  try {
    // Two passes: Instagram fails on each, and WhatsApp is read after each failure.
    await expect.poll(() => events().filter((e) => e.event === 'read-failed' && e.account === 'test-ig').length, { timeout: 90_000 }).toBeGreaterThanOrEqual(2);
    const log = events();
    const firstFailure = log.findIndex((e) => e.event === 'read-failed' && e.account === 'test-ig');
    expect(log.slice(firstFailure).some((e) => e.event === 'read' && e.account === 'test-wa'), 'WhatsApp is read after Instagram fails').toBe(true);

    // The line carries on with WhatsApp's customers.
    await expect(heading(win, '2 customers are waiting')).toBeVisible();

    // Accounts names the reader, not the account, and shows no zero for the broken channel.
    await rail('Accounts').click();
    await expect(win.getByRole('main')).toContainText('1 reading, 1 not being read');
    const igCell = win.locator('.cell').filter({ hasText: 'Reader not working' });
    await expect(igCell).toBeVisible();
    await expect(igCell).not.toContainText('0waiting');
    await expect(win.getByRole('button', { name: /Instagram reader/ })).toContainText('Not reading');
    await expect(win.getByRole('button', { name: /WhatsApp reader/ })).toContainText('Healthy');

    // Needs you and the reader screen say the same.
    await win.getByRole('button', { name: /^Needs you/ }).click();
    await expect(win.getByRole('complementary', { name: 'Needs you' })).toContainText('Instagram reader: Not reading');
    await win.keyboard.press('Escape');
    await win.getByRole('button', { name: /Instagram reader/ }).click();
    await expect(heading(win, 'The Instagram reader stopped working')).toBeVisible();
    await expect(win.getByRole('main')).toContainText('the page has changed, and the reader could not read it');
    await expect(win.getByRole('main')).not.toContainText('renderer console');
    if (process.env.UM_SHOTS) await win.screenshot({ path: join(process.env.UM_SHOTS, 'break-test.png') });

    // The report support asks for, saved from this very screen, and safe to send as it is.
    await win.getByRole('button', { name: 'Save a report for support' }).click();
    await expect(win.getByRole('main')).toContainText(/Saved to .*Unified Messenger report \d{4}-\d{2}-\d{2}\.txt/, { timeout: 30_000 });
    const saved = readFileSync(join(reports, readdirSync(reports)[0]), 'utf8');
    expect(saved).toContain('Test Instagram \u2014 instagram at Main branch');
    expect(saved).toContain('Test front desk');
    expect(saved).toMatch(/Readers/);
    expect(saved).toContain('report for support');
    // The fixture's own customers are in the snapshot and the line; none of them is the report's business.
    for (const secret of ['Sample Customer', 'Hello?', '@c.us']) expect(saved, secret).not.toContain(secret);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

// ---- the assistant ----------------------------------------------------------------------------------------

/** A stand-in for Ollama on this machine: answers the four calls the app makes, and records the questions it is
 *  asked, so a test can check what the app sent. It never runs a model. */
async function fakeOllama(answer: (messages: { role: string; content: string }[]) => string = () => 'A test answer.') {
  const { createServer } = await import('node:http');
  const models: string[] = [];
  const asked: { role: string; content: string }[][] = [];
  const server = createServer((req, res) => {
    let body = '';
    req.on('data', (c) => { body += c; });
    req.on('end', () => {
      const json = (o: unknown) => { res.setHeader('content-type', 'application/json'); res.end(JSON.stringify(o)); };
      if (req.url === '/api/version') return json({ version: 'test' });
      if (req.url === '/api/tags') return json({ models: models.map((name) => ({ name })) });
      if (req.url === '/api/pull') {
        const { model } = JSON.parse(body) as { model: string };
        res.setHeader('content-type', 'application/x-ndjson');
        const lines = [{ status: 'pulling manifest' }, { status: 'pulling', total: 100, completed: 40 }, { status: 'pulling', total: 100, completed: 100 }, { status: 'success' }];
        let i = 0;
        const next = () => { if (i < lines.length) { res.write(`${JSON.stringify(lines[i++])}\n`); setTimeout(next, 150); } else { models.push(model); res.end(); } };
        return next();
      }
      if (req.url === '/api/chat') {
        const { messages } = JSON.parse(body) as { messages: { role: string; content: string }[] };
        asked.push(messages);
        return json({ message: { role: 'assistant', content: answer(messages) } });
      }
      res.statusCode = 404; res.end();
    });
  });
  await new Promise<void>((r) => server.listen(0, '127.0.0.1', () => r()));
  const port = (server.address() as { port: number }).port;
  return { endpoint: `http://127.0.0.1:${port}/`, models, asked, close: () => new Promise<void>((r) => server.close(() => r())) };
}

test('the assistant is off until switched on, finds Ollama, downloads the model only on request, and says so', async () => {
  const ollama = await fakeOllama();
  const data = dataFolder({ settings: { assistant: { enabled: false, model: 'gemma3:4b', endpoint: ollama.endpoint } } });
  const { app, win } = await open(data);
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings' }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Assistant' }).click();
    await expect(win.getByRole('main')).not.toContainText('Sample figures');
    await expect(win.getByRole('status')).toHaveCount(0);

    await win.getByRole('switch', { name: 'Use the assistant' }).click();
    const status = win.getByRole('status');
    await expect(status).toHaveText('Ready to download the gemma3:4b model.');
    expect(ollama.models, 'nothing is downloaded until asked').toEqual([]);

    await win.getByRole('button', { name: /Download the model/ }).click();
    await expect(status).toHaveText('Ready, using gemma3:4b through the Ollama already running on this PC.');
    expect(ollama.models).toEqual(['gemma3:4b']);
    expect(JSON.parse(readFileSync(join(data, 'config.json'), 'utf8')).settings.assistant.enabled).toBe(true);

    await win.getByRole('switch', { name: 'Use the assistant' }).click();
    await expect(status).toHaveCount(0);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await ollama.close();
    rmSync(data, { recursive: true, force: true });
  }
});

test('with no Ollama anywhere, the assistant says so and offers the download, without starting one', async () => {
  // A port nothing listens on, and a LOCALAPPDATA with no Ollama in it (the open() helper points it at the data folder).
  const data = dataFolder({ settings: { assistant: { enabled: true, model: 'gemma3:4b', endpoint: 'http://127.0.0.1:9/' } } });
  const { app, win } = await open(data);
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings' }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Assistant' }).click();
    await expect(win.getByRole('status')).toHaveText('Ollama is not on this PC yet. It is free and runs entirely on this PC.');
    await expect(win.getByRole('button', { name: /Download Ollama/ })).toBeVisible();
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

test('the assistant answers from the app’s own figures, offers the chat it names, and keeps nothing', async () => {
  // The fake model chooses the fact about the longest wait, as the real one is asked to: by its id. Then it passes
  // the check the app puts to each chosen fact.
  const ollama = await fakeOllama((messages) => {
    if (messages[0].content.startsWith('Does the fact below answer the question?')) return JSON.stringify({ answers: true });
    const line = messages[0].content.split('\n').find((l) => l.includes('The longest wait is')) ?? '';
    return JSON.stringify({ facts: [line.split(':')[0]] });
  });
  ollama.models.push('gemma3:4b');
  const now = Date.now();
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }],
    locations: [{ name: 'Main branch' }],
    settings: { assistant: { enabled: true, model: 'gemma3:4b', endpoint: ollama.endpoint } },
  });
  const chat = (key: string, customer: string, minutesAgo: number) => ({
    conversationKey: key, customerName: customer, unread: 1, lastActivity: now - minutesAgo * 60_000, preview: 'Is the order ready?',
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [chat('a@c.us', 'Sample Customer A', 40), chat('b@c.us', 'Sample Customer B', 5)] } }));
  const { app, win } = await open(data);
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Assistant', exact: true }).click();
    await expect(win.getByRole('main')).not.toContainText('Sample figures');
    const input = win.getByRole('textbox', { name: 'Ask the assistant' });
    await expect(input).toBeEnabled({ timeout: 15_000 });
    await input.fill('Who has waited longest? (secret-marker-7731)');
    await win.getByRole('button', { name: 'Ask', exact: true }).click();

    // The answer is the app's own fact, word for word.
    await expect(win.getByText('The longest wait is Sample Customer A, 40 min, on Test front desk at Main branch.')).toBeVisible();
    // What the model was given: the instructions to choose, the app's own facts, and the question.
    const sent = ollama.asked[0];
    expect(sent[0].role).toBe('system');
    expect(sent[0].content).toContain('Reply with JSON only');
    expect(sent[0].content).toContain('2 customers are waiting for a reply.');
    expect(sent[0].content).toContain('C1: Sample Customer A is waiting on the account Test front desk at Main branch, for 40 min, and is past the target');
    // And the chosen fact was checked against the question before it was shown.
    expect(ollama.asked[1][0].content).toContain('Fact: The longest wait is Sample Customer A');
    expect(sent.at(-1)).toEqual({ role: 'user', content: 'Who has waited longest? (secret-marker-7731)' });

    // The customer the answer names is offered, and opens.
    await win.getByRole('button', { name: 'Open Sample Customer A’s chat' }).click();
    await expect(win.locator('.dock-bar .who b')).toHaveText('Sample Customer A');
    await quit(app, win);

    // Nothing kept: neither the question nor the answer is in any file, the log included.
    const files = readdirSync(data, { recursive: true }).map(String).filter((f) => /\.(json|log)$/.test(f));
    for (const f of files) {
      const text = readFileSync(join(data, f), 'utf8');
      expect(text, f).not.toContain('secret-marker-7731');
      expect(text, f).not.toContain('The longest wait is');
    }
  } finally {
    await app.close().catch(() => {});
    await ollama.close();
    rmSync(data, { recursive: true, force: true });
  }
});

test('Suggest a reply drafts from the open chat’s own messages, copies one, and keeps nothing', async () => {
  const ollama = await fakeOllama(() => 'WARM:\nYes, the blue one is in stock and you can collect it today from [time].\nSHORT:\nYes, collect it today from [time].');
  ollama.models.push('gemma3:4b');
  const now = Date.now();
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Main branch WhatsApp', channel: 'whatsapp', professional: true, location: 'Main branch',
      url: pathToFileURL(join(V6, 'tests', 'fixtures', 'whatsapp-chat.html')).href }],
    locations: [{ name: 'Main branch' }],
    settings: { assistant: { enabled: true, model: 'gemma3:4b', endpoint: ollama.endpoint } },
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({ 'test-wa': { capturedAt: now, chats: [{
    conversationKey: 'a@c.us', customerName: 'Sample Customer A', unread: 1, lastActivity: now - 10 * 60_000, preview: 'Can I collect it today?',
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
  }] } }));
  const { app, win } = await open(data);
  try {
    await expect(heading(win, '1 customer is waiting')).toBeVisible();
    await win.getByRole('button', { name: 'Open chat' }).first().click();
    const panel = win.getByRole('complementary', { name: 'About this customer' });
    await panel.getByRole('button', { name: 'Suggest a reply' }).click();
    await expect(panel).not.toContainText('Sample figures');
    await panel.getByRole('button', { name: 'Draft replies' }).click();

    await expect(panel).toContainText('Yes, the blue one is in stock and you can collect it today from [time].');
    await expect(panel).toContainText('Yes, collect it today from [time].');
    await expect(panel).toContainText('From the last 5 messages');

    // What the model was given: the chat as the customer and us, a photo by its caption, a voice note by name, and no
    // thumbnail data or security notice.
    const [system, user] = ollama.asked[0];
    expect(system.content).toContain('"Main branch WhatsApp"');
    expect(user.content).toBe([
      'The conversation so far, oldest first:',
      'Customer: Hi, do you have the blue one in stock?',
      'Us: Let me check for you.',
      'Customer: [photo] this one',
      'Customer: [voice message]',
      'Customer: Can I collect it today?',
    ].join('\n'));

    await panel.getByRole('button', { name: 'Copy' }).first().click();
    await expect.poll(() => app.evaluate(({ clipboard }) => clipboard.readText())).toBe('Yes, the blue one is in stock and you can collect it today from [time].');
    await quit(app, win);

    for (const f of readdirSync(data, { recursive: true }).map(String).filter((x) => /\.(json|log)$/.test(x))) {
      const text = readFileSync(join(data, f), 'utf8');
      expect(text, f).not.toContain('blue one');
    }
  } finally {
    await app.close().catch(() => {});
    await ollama.close();
    rmSync(data, { recursive: true, force: true });
  }
});

// ---- help screenshots ------------------------------------------------------------------------------------

/**
 * The pictures in the help pages, taken from invented data so they can never show a real customer. Skipped in an
 * ordinary run; `npm run help:shots` sets UM_HELP_SHOTS and writes them to help/shots, where the pages find them.
 */
test('help screenshots', async () => {
  test.skip(!process.env.UM_HELP_SHOTS, 'only when refreshing the help pictures: npm run help:shots');
  test.setTimeout(180_000);
  const out = join(V6, 'help', 'shots');
  mkdirSync(out, { recursive: true });
  const now = Date.now();
  const H = 60, D = 24 * H;
  const data = dataFolder({
    accounts: [
      { id: 'main-wa', name: 'Main branch WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' },
      { id: 'main-ig', name: 'Main branch Instagram', channel: 'instagram', url: 'about:blank', professional: true, location: 'Main branch' },
      { id: 'north-wa', name: 'North branch WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'North branch' },
      { id: 'north-g', name: 'North branch Google', channel: 'googlebusiness', url: 'about:blank', professional: false, location: 'North branch' },
    ],
    locations: [{ name: 'Main branch' }, { name: 'North branch' }],
    settings: {
      notCustomers: { words: ['Staff'], numbers: [] },
      savedReplies: [
        { title: 'Opening hours', body: 'We are open 11 am to 9 pm, Monday to Saturday.' },
        { title: 'Prices', body: 'Our current price list is attached. Let us know which option suits you.' },
      ],
    },
  });
  const chat = (key: string, customer: string, minutesAgo: number, preview: string, o: Record<string, unknown> = {}) => ({
    conversationKey: key, customerName: customer, unread: 1, lastActivity: now - minutesAgo * 60_000, preview,
    awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '', ...o,
  });
  writeFileSync(join(data, 'snapshot.json'), JSON.stringify({
    'main-wa': { capturedAt: now, chats: [
      chat('a@c.us', 'Sample Customer A', 3, 'Do you have space on Friday afternoon?'),
      chat('b@c.us', 'Sample Customer B', 12, 'What time do you open tomorrow?'),
      chat('c@c.us', 'Sample Customer C', 26, 'Can I change my booking to next week?'),
      chat('d@c.us', 'Sample Customer D', 2 * D + 5 * H, 'Is the offer still on?'),
      chat('e@c.us', 'Sample Customer E', 40, '', { lastMessageType: 'call_log', lastCallOutcome: 'Missed' }),
      chat('f@c.us', 'Sample Staff Member', 90, 'Can you check the stock list?'),
      chat('g@c.us', 'Sample Customer G', 60, 'ok thanks'),
    ] },
    'main-ig': { capturedAt: now, chats: [chat('ig-1', 'Sample Customer H', 18, ''), chat('ig-2', 'Sample Customer I', 7, '')] },
    'north-wa': { capturedAt: now, chats: [
      chat('j@c.us', 'Sample Customer J', 9, 'Price for the full package?'),
      chat('k@c.us', 'Sample Customer K', 3 * H, 'Hello, anyone there?'),
    ] },
  }));
  // A fortnight of invented days and replies, so Reports has something to draw.
  const days = Array.from({ length: 14 }, (_, i) => {
    const at = new Date(now - (13 - i) * D * 60_000);
    const day = `${at.getFullYear()}-${String(at.getMonth() + 1).padStart(2, '0')}-${String(at.getDate()).padStart(2, '0')}`;
    const wrote = 18 + ((i * 7) % 11);
    return {
      day, customersWrote: wrote, wroteByHour: Array.from({ length: 24 }, (_, h) => (h >= 11 && h <= 20 ? Math.round(wrote / 10) + (h % 3) : 0)),
      replies: wrote - 3, medianReplyMinutes: 9 + (i % 5), repliesWithinTarget: wrote - 6, targetMinutes: 15,
      waitingOverADayAtFirstRead: 2 + (i % 3), reopened: i % 4, missedCalls: i % 3,
    };
  });
  writeFileSync(join(data, 'history.json'), JSON.stringify(Object.fromEntries(['main-wa', 'main-ig', 'north-wa'].map((id) =>
    [id, { watchStart: now - 20 * D * 60_000, days, seen: {} }]))));
  writeFileSync(join(data, 'response-times.json'), JSON.stringify({
    pending: {}, watchStart: { 'main-wa': now - 20 * D * 60_000, 'north-wa': now - 20 * D * 60_000 },
    samples: {
      'main-wa': Array.from({ length: 40 }, (_, i) => ({ answeredAt: now - i * 3 * H * 60_000, minutes: 4 + (i * 7) % 30 })),
      'north-wa': Array.from({ length: 25 }, (_, i) => ({ answeredAt: now - i * 5 * H * 60_000, minutes: 6 + (i * 5) % 40 })),
    },
  }));
  // Invented reviews, read 'just now', so the picture shows a working Reviews screen without any read happening.
  const review = (reviewer: string, stars: number, age: string, text: string, replied = false) => ({ reviewer, stars, age, text, replied });
  writeFileSync(join(data, 'reviews.json'), JSON.stringify({ 'north-g': {
    capturedAt: now, ratingAt: now, rating: 4.6, total: 991, more: true, cards: [
      review('Sample Reviewer A', 1, '2 days ago', 'Waited forty minutes past my booking and nobody said why.'),
      review('Sample Reviewer B', 5, '3 days ago', '', true),
      review('Sample Reviewer C', 4, 'a day ago', 'Good service, a little crowded on Sunday.'),
      review('Sample Reviewer D', 2, '5 days ago', 'Called twice and nobody answered.'),
      review('Sample Reviewer E', 5, 'a week ago', 'Quick and friendly.', true),
    ],
  } }));
  writeFileSync(join(data, 'calls.json'), JSON.stringify({
    'main-wa|e@c.us|1': { account: 'main-wa', key: 'e@c.us', at: now - 40 * 60_000, returnedAt: null, returnedBy: null },
  }));

  const { app, win } = await open(data);
  const shot = (name: string) => win.screenshot({ path: join(out, `${name}.jpg`), type: 'jpeg', quality: 82 });
  const rail = (name: string | RegExp) => win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name, exact: typeof name === 'string' });
  const palette = async (what: string) => {
    await win.keyboard.press('Control+k');
    await win.getByRole('textbox', { name: 'Search' }).fill(what);
    await win.keyboard.press('Enter');
  };
  try {
    await win.getByRole('button', { name: 'Light', exact: true }).click();
    await expect(heading(win, /customers are waiting/)).toBeVisible();
    await shot('line');

    await win.getByRole('button', { name: 'Open chat' }).first().click();
    await expect(win.locator('.dock-bar .who b')).toBeVisible();
    await shot('dock');
    await win.keyboard.press('Escape');

    await win.getByRole('button', { name: /^Needs you/ }).click();
    await shot('needs');
    await win.keyboard.press('Escape');

    await win.keyboard.press('Control+k');
    await expect(win.getByRole('dialog', { name: 'Command palette' })).toBeVisible();
    await shot('palette');
    await win.keyboard.press('Escape');

    await palette('Set aside');
    await expect(heading(win, 'Set aside')).toBeVisible();
    await shot('set-aside');

    await palette('Morning digest');
    await shot('digest');

    await rail('Accounts').click();
    await expect(heading(win, 'Accounts')).toBeVisible();
    await shot('accounts');
    await win.getByRole('button', { name: 'Add an account' }).click();
    await shot('add-account');
    await win.keyboard.press('Escape');
    await win.getByRole('button', { name: 'Figures' }).first().click();
    await shot('account-detail');
    await win.getByRole('button', { name: 'Reading record' }).click();
    await shot('lost-login');
    await rail('Accounts').click();
    await win.getByRole('button', { name: /WhatsApp reader/ }).click();
    await shot('reader');

    await rail('Reviews').click();
    await shot('reviews');

    await rail('Reports').click();
    await expect(win.getByRole('group', { name: 'Report' })).toBeVisible();
    await shot('reports');
    for (const tab of ['Reply times', 'Backlog and reopened', 'Missed calls', 'Weekly report']) {
      await win.getByRole('group', { name: 'Report' }).getByRole('button', { name: tab }).click();
      await shot(`reports-${tab.toLowerCase().replace(/\s+/g, '-')}`);
    }

    await rail('Assistant').click();
    await shot('assistant');

    await rail('Settings').click();
    for (const section of ['Look and reading', 'Opening hours', 'Notifications', 'Saved replies']) {
      await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: section }).click();
      await shot(`settings-${section.toLowerCase().replace(/\s+/g, '-')}`);
    }
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});

/** An invented Google and Firebase in the test's own process: the authorize page sends the browser straight back to
 *  the app's loopback port, and each call is recorded so the test can check what was sent. */
async function fakeCloud() {
  const { createServer } = await import('node:http');
  const calls: { path: string; query: URLSearchParams; body: string }[] = [];
  const o = { deny: false, refresh: 'ok' as 'ok' | 'expired', user: { uid: 'test-uid', email: 'owner@example.com', name: 'Sample Owner' } };
  const server = createServer(async (req, res) => {
    const url = new URL(req.url ?? '/', 'http://x');
    let body = '';
    for await (const chunk of req) body += chunk;
    calls.push({ path: url.pathname, query: url.searchParams, body });
    const json = (status: number, v: unknown) => res.writeHead(status, { 'Content-Type': 'application/json' }).end(JSON.stringify(v));
    if (url.pathname === '/authorize') {
      const back = new URL(url.searchParams.get('redirect_uri')!);
      back.search = new URLSearchParams(o.deny ? { error: 'access_denied', state: url.searchParams.get('state')! } : { code: 'test-code', state: url.searchParams.get('state')! }).toString();
      res.writeHead(302, { Location: back.toString() }).end();
    } else if (url.pathname === '/token') json(200, { id_token: 'test-id-token' });
    else if (url.pathname === '/signin') json(200, { localId: o.user.uid, email: o.user.email, displayName: o.user.name, refreshToken: 'test-refresh-7731' });
    else if (url.pathname === '/refresh') o.refresh === 'ok' ? json(200, { id_token: 'fresh', refresh_token: 'test-refresh-7731' }) : json(400, { error: { message: 'TOKEN_EXPIRED' } });
    else res.writeHead(404).end();
  });
  await new Promise<void>((done) => server.listen(0, '127.0.0.1', done));
  const endpoint = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
  return { endpoint, calls, o, close: () => new Promise<void>((done) => server.close(() => done())) };
}

test('signing in with Google: in the browser, kept encrypted across a restart, and signed out on request or when it ends', async () => {
  const cloud = await fakeCloud();
  const data = dataFolder();
  const env = { ...CLOUD, UM_CLOUD_ENDPOINT: cloud.endpoint, UM_SIGNIN_OPEN: 'fetch' };
  // The gate shows the sign-in screen before anything else, so while signed out there is no rail to navigate;
  // once signed in and admitted, the workspace panel is where the session is managed.
  const workspace = async (win: Page) => {
    const rail = win.getByRole('navigation', { name: 'Screens' });
    if (!(await rail.isVisible().catch(() => false))) return;
    await rail.getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Workspace' }).click();
  };
  let { app, win } = await open(data, env);
  try {
    // Cancelled in the browser: said in words, and nothing kept.
    cloud.o.deny = true;
    await win.getByRole('button', { name: 'Quit', exact: true }).waitFor();
    await win.getByRole('button', { name: 'Continue with Google' }).click();
    await expect(win.getByRole('alert')).toHaveText('Sign-in was cancelled in the browser.');

    // Signed in: Google was asked for name and email only, with PKCE, and the reply came back to this PC.
    cloud.o.deny = false;
    await win.getByRole('button', { name: 'Continue with Google' }).click();
    // Signed in, and nothing admits this address: the gate says so and names it, rather than letting them in.
    await expect(heading(win, 'This account has not been invited')).toBeVisible({ timeout: 30_000 });
    await expect(win.locator('body')).toContainText('owner@example.com');
    const authorize = cloud.calls.filter((c) => c.path === '/authorize').at(-1)!;
    expect(authorize.query.get('scope')).toBe('openid email profile');
    expect(authorize.query.get('redirect_uri')).toMatch(/^http:\/\/127\.0\.0\.1:\d+$/);
    const { createHash } = await import('node:crypto');
    const verifier = new URLSearchParams(cloud.calls.find((c) => c.path === '/token')!.body).get('code_verifier')!;
    expect(createHash('sha256').update(verifier).digest('base64url')).toBe(authorize.query.get('code_challenge'));
    expect(cloud.calls.find((c) => c.path === '/signin')!.query.get('key')).toBe('test-key');

    // Kept: the refresh token encrypted, and no email in the log.
    const kept = readFileSync(join(data, 'cloud.json'), 'utf8');
    expect(kept).toContain('owner@example.com');
    expect(kept).not.toContain('test-refresh-7731');
    expect(readFileSync(join(data, 'app.log'), 'utf8')).not.toContain('owner@example.com');
    await quit(app, win);

    // After a restart: still signed in, so the gate goes straight back to what it said, and the kept token
    // was decrypted and used.
    ({ app, win } = await open(data, env));
    await expect(heading(win, 'This account has not been invited')).toBeVisible({ timeout: 30_000 });
    await expect.poll(() => cloud.calls.filter((c) => c.path === '/refresh').some((c) => new URLSearchParams(c.body).get('refresh_token') === 'test-refresh-7731')).toBe(true);

    // Signed out from the gate: forgotten on this PC, and the sign-in screen comes back.
    await win.getByRole('button', { name: 'Sign out' }).click();
    await expect(win.getByRole('button', { name: 'Continue with Google' })).toBeVisible();
    expect(readdirSync(data)).not.toContain('cloud.json');

    // Signed in again, straight from the gate.
    await win.getByRole('button', { name: 'Continue with Google' }).click();
    await expect(heading(win, 'This account has not been invited')).toBeVisible({ timeout: 30_000 });
    // Still no rail: an account that has not been invited never reaches the app.
    await expect(win.getByRole('navigation', { name: 'Screens' })).toHaveCount(0);
    await quit(app, win);
    // Firebase says the sign-in is over: signed out at the next start, with the reason.
    cloud.o.refresh = 'expired';
    ({ app, win } = await open(data, env));
    await workspace(win);
    await expect(win.getByRole('alert')).toHaveText('Your sign-in has ended. Sign in again.');
    expect(readdirSync(data)).not.toContain('cloud.json');
    await quit(app, win);

    // A build without the project's identifiers cannot ask anyone whether this PC may run, so it gates nobody:
    // the app opens as usual, and Settings says sign-in is not available.
    ({ app, win } = await open(data, { UM_CLOUD_CONFIG: join(data, 'none.json') }));
    await expect(win.getByRole('navigation', { name: 'Screens' })).toBeVisible({ timeout: 30_000 });
    await workspace(win);
    await expect(win.getByText('Sign-in is not available in this build of the app.')).toBeVisible();
    await expect(win.getByRole('button', { name: 'Sign in with Google' })).toHaveCount(0);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await cloud.close();
    rmSync(data, { recursive: true, force: true });
  }
});

/** An invented Firestore in the test's own process, answering the three calls the app makes (a query across
 *  collections by one field, a read, a write) the way the REST API does. It keeps no rules: the emulator tests in
 *  cloud/ prove those. It records every document, so the test can check exactly what left the PC. */
async function fakeFirestore() {
  const { createServer } = await import('node:http');
  const docs = new Map<string, { fields: Record<string, unknown>; updateTime: string }>();
  let clock = 0;
  const server = createServer(async (req, res) => {
    let body = '';
    for await (const chunk of req) body += chunk;
    const url = new URL(req.url ?? '/', 'http://x');
    const json = (status: number, v: unknown) => res.writeHead(status, { 'Content-Type': 'application/json' }).end(JSON.stringify(v));
    const root = url.pathname.split('/documents')[0].replace(/^\/v1\//, '') + '/documents';
    if (url.pathname.endsWith('/documents:runQuery')) {
      const q = JSON.parse(body).structuredQuery;
      const { field, value } = q.where.fieldFilter;
      const rows = [...docs.entries()].filter(([name, d]) => name.split('/').at(-2) === q.from[0].collectionId
        && JSON.stringify(d.fields[field.fieldPath]) === JSON.stringify(value));
      json(200, rows.length ? rows.map(([name, d]) => ({ document: { name, ...d } })) : [{ readTime: 'x' }]);
    } else if (url.pathname.endsWith('/documents:commit')) {
      const stamp = new Date(Date.now() + ++clock).toISOString();
      for (const w of JSON.parse(body).writes) {
        if (w.delete) continue;
        const prior = docs.get(w.update.name);
        if (w.currentDocument?.exists === false && prior) return json(409, { error: { status: 'ALREADY_EXISTS' } });
        if (w.currentDocument?.updateTime && prior?.updateTime !== w.currentDocument.updateTime) return json(400, { error: { status: 'FAILED_PRECONDITION' } });
      }
      const results = [];
      for (const w of JSON.parse(body).writes) {
        if (w.delete) { docs.delete(w.delete); results.push({}); continue; }
        const prior = docs.get(w.update.name);
        const fields = w.updateMask ? { ...(prior?.fields ?? {}), ...w.update.fields } : { ...w.update.fields };
        for (const t of w.updateTransforms ?? []) fields[t.fieldPath] = { timestampValue: stamp };
        docs.set(w.update.name, { fields, updateTime: stamp });
        results.push({ updateTime: stamp });
      }
      json(200, { writeResults: results });
    } else if (req.method === 'GET' && url.pathname.split('/documents/')[1].split('/').length % 2 === 1) {
      // A collection: its direct children.
      const parent = decodeURIComponent(`${root}/${url.pathname.split('/documents/')[1]}`);
      json(200, { documents: [...docs.entries()].filter(([name]) => name.startsWith(`${parent}/`) && !name.slice(parent.length + 1).includes('/')).map(([name, d]) => ({ name, ...d })) });
    } else if (req.method === 'GET') {
      const name = `${root}${url.pathname.split('/documents')[1]}`;
      const d = docs.get(decodeURIComponent(name));
      d ? json(200, { name, ...d }) : json(404, { error: { status: 'NOT_FOUND' } });
    } else res.writeHead(404).end();
  });
  await new Promise<void>((done) => server.listen(0, '127.0.0.1', done));
  const port = (server.address() as { port: number }).port;
  return { base: `http://127.0.0.1:${port}/v1/projects/test-project/databases/(default)`, docs, close: () => new Promise<void>((done) => server.close(() => done())) };
}

test('a workspace: started from this PC’s setup, and a second PC signed in to it gets the accounts', async () => {
  const cloud = await fakeCloud();
  const fs = await fakeFirestore();
  const env = { ...CLOUD, UM_CLOUD_ENDPOINT: cloud.endpoint, UM_SIGNIN_OPEN: 'fetch', UM_FIRESTORE: fs.base };
  const first = dataFolder({
    accounts: [
      { id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' },
      { id: 'test-ig', name: 'Test Instagram', channel: 'instagram', url: 'about:blank', professional: true, location: 'Main branch' },
    ],
    locations: [{ name: 'Main branch', slaMinutes: 20 }],
    settings: { slaMinutes: 25, theme: 'dark', savedReplies: [{ title: 'Hours', body: 'We open at 11.' }] },
  });
  const second = dataFolder();
  // Creating a workspace means being the product owner: with invitation-only access (core/admission.ts) nobody
  // else is admitted to an app that belongs to no workspace yet. This is the marker made by hand in the console.
  const ownerMarker = (uid: string) => fs.docs.set(`projects/test-project/databases/(default)/documents/owners/${uid}`,
    { fields: { note: { stringValue: 'product owner' } }, updateTime: new Date().toISOString() });
  ownerMarker('test-uid');
  // Signed out, the gate is the whole window, and the window takes a moment to draw: wait for whichever of the
  // two appears before deciding, or the check races the first render and reads 'no gate' every time.
  const workspace = async (win: Page) => {
    const rail = win.getByRole('navigation', { name: 'Screens' });
    const gateIn = win.getByRole('button', { name: 'Continue with Google' });
    await gateIn.or(rail).first().waitFor({ timeout: 30_000 });
    if (await gateIn.isVisible()) { await gateIn.click(); await rail.waitFor({ timeout: 30_000 }); }
    await rail.getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Workspace' }).click();
  };
  let { app, win } = await open(first, env);
  try {
    // Signing in happens at the gate now, so Settings opens already signed in.
    await workspace(win);
    await expect(win.getByText('No workspace yet')).toBeVisible();
    await win.getByLabel('Workspace name').fill('Sample Business');
    await win.getByRole('button', { name: 'Start the workspace' }).click();
    await expect(win.getByText('You are an admin: changes made here reach the workspace.')).toBeVisible();
    await expect(win.getByRole('status').filter({ hasText: 'Setup in step with the workspace' })).toBeVisible();

    // What left the PC: the workspace, its first admin, and the shared setup. Nothing personal, nothing about customers.
    const setup = [...fs.docs.entries()].find(([name]) => name.endsWith('/config/main'))![1];
    const text = JSON.stringify(setup.fields);
    expect(text).toContain('Test front desk');
    expect(text).toContain('We open at 11.');
    for (const personal of ['theme', 'dark', 'quietHours', 'assistant', 'muted']) expect(text, personal).not.toContain(personal);
    expect(Object.keys(setup.fields).sort()).toEqual(['accounts', 'locations', 'settings', 'updatedAt', 'updatedBy']);
    await quit(app, win);

    // A second PC, signed in as the same person: the accounts and business rules arrive; its own theme stays its own.
    ({ app, win } = await open(second, env));
    await workspace(win);
    await expect(win.getByText('Sample Business')).toBeVisible();
    await expect.poll(() => JSON.parse(readFileSync(join(second, 'config.json'), 'utf8')).accounts?.map((a: { name: string }) => a.name)).toEqual(['Test front desk', 'Test Instagram']);
    const received = JSON.parse(readFileSync(join(second, 'config.json'), 'utf8'));
    expect(received.settings.slaMinutes).toBe(25);
    expect(received.settings.savedReplies).toEqual([{ title: 'Hours', body: 'We open at 11.' }]);
    expect(received.settings.theme).toBe('system');
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Accounts', exact: true }).click();
    await expect(win.getByRole('main')).toContainText('2 accounts at 1 location.');
    // Nothing about the workspace's people or setup is in the log.
    expect(readFileSync(join(second, 'app.log'), 'utf8')).not.toContain('Sample Business');
    expect(readFileSync(join(second, 'app.log'), 'utf8')).not.toContain('Test front desk');
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await cloud.close();
    await fs.close();
    rmSync(first, { recursive: true, force: true });
    rmSync(second, { recursive: true, force: true });
  }
});

test('members: an admin invites by address, the person joins on their PC, and removing them wipes what it had', async () => {
  const cloud = await fakeCloud();
  const fs = await fakeFirestore();
  const env = { ...CLOUD, UM_CLOUD_ENDPOINT: cloud.endpoint, UM_SIGNIN_OPEN: 'fetch', UM_FIRESTORE: fs.base };
  const OWNER = { uid: 'test-uid', email: 'owner@example.com', name: 'Sample Owner' };
  const STAFF = { uid: 'staff-uid', email: 'staff@example.com', name: 'Sample Staff' };
  // Only the product owner may open an app that belongs to no workspace yet (core/admission.ts), and that is
  // who starts one: the marker is made by hand in the console.
  fs.docs.set(`projects/test-project/databases/(default)/documents/owners/${OWNER.uid}`, { fields: { note: { stringValue: 'product owner' } }, updateTime: new Date().toISOString() });
  const ownerPc = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }],
    locations: [{ name: 'Main branch' }],
  });
  const staffPc = dataFolder({ accounts: [{ id: 'own', name: 'Staff own WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: false, location: '' }] });
  // The gate first: sign in there when it is showing, then Settings. The owner's marker is what admits the
  // person who starts a workspace; everyone else gets in by being invited to one.
  const workspace = async (win: Page) => {
    const rail = win.getByRole('navigation', { name: 'Screens' });
    const gateIn = win.getByRole('button', { name: 'Continue with Google' });
    await gateIn.or(rail).first().waitFor({ timeout: 30_000 });
    if (await gateIn.isVisible()) { await gateIn.click(); await rail.waitFor({ timeout: 60_000 }); }
    await rail.getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Workspace' }).click();
  };
  cloud.o.user = OWNER;
  let { app, win } = await open(ownerPc, env);
  try {
    // Signed in at the gate, so Settings opens already signed in.
    await workspace(win);
    await win.getByLabel('Workspace name').fill('Sample Business');
    await win.getByRole('button', { name: 'Start the workspace' }).click();
    await expect(win.getByRole('cell', { name: /Sample Owner \(you\)/ })).toBeVisible();
    await win.getByRole('button', { name: 'Invite someone' }).click();
    await win.getByLabel('Their Google address').fill('Staff@Example.com');
    await win.getByRole('button', { name: 'Save the invitation' }).click();
    await expect(win.getByRole('row', { name: /staff@example.com.*Invite waiting/ })).toBeVisible();
    await quit(app, win);

    // The invited person, on their own PC: which workspace, then Join.
    cloud.o.user = STAFF;
    // The invited person meets the gate, not the app: it names who they are signed in as and what they were
    // invited to, and Join is right there, because Settings is behind the gate.
    ({ app, win } = await open(staffPc, env));
    await win.getByRole('button', { name: 'Continue with Google' }).click();
    await expect(heading(win, 'You have been invited')).toBeVisible({ timeout: 30_000 });
    await expect(win.locator('body')).toContainText('Sample Business');
    await win.getByRole('button', { name: 'Join Sample Business' }).click();
    await win.getByRole('navigation', { name: 'Screens' }).waitFor({ timeout: 30_000 });
    await workspace(win);
    await expect(win.getByText('You are a member: this PC takes its setup from the workspace.')).toBeVisible();
    await expect.poll(() => JSON.parse(readFileSync(join(staffPc, 'config.json'), 'utf8')).accounts.map((a: { id: string }) => a.id).sort()).toEqual(['own', 'test-wa']);
    // A member sees who is in the workspace, and cannot manage it.
    await expect(win.getByRole('cell', { name: /Sample Owner/ })).toBeVisible();
    await expect(win.getByRole('button', { name: 'Invite someone' })).toHaveCount(0);
    await expect(win.getByRole('button', { name: 'Remove' })).toHaveCount(0);
    await quit(app, win);

    // The admin removes them, after being told what it does.
    cloud.o.user = OWNER;
    ({ app, win } = await open(ownerPc, env));
    await workspace(win);
    const row = win.getByRole('row', { name: /Sample Staff/ });
    await row.getByRole('button', { name: 'Remove' }).click();
    await expect(row.getByRole('alertdialog')).toContainText('Linked devices');
    await row.getByRole('button', { name: 'Remove and wipe their logins' }).click();
    await expect(row.getByText('Removed')).toBeVisible();
    await quit(app, win);

    // Their PC at its next check: signed out, the workspace's account wiped, their own kept, and a screen that says so.
    cloud.o.user = STAFF;
    ({ app, win } = await open(staffPc, env));
    await expect(win.getByRole('heading', { name: 'This PC is no longer part of Sample Business' })).toBeVisible();
    await expect(win.getByText('Test front desk')).toBeVisible();
    expect(JSON.parse(readFileSync(join(staffPc, 'config.json'), 'utf8')).accounts.map((a: { id: string }) => a.id)).toEqual(['own']);
    expect(readdirSync(staffPc)).not.toContain('cloud.json');
    await win.getByRole('button', { name: 'Close' }).click();
    // Closing the notice does not let them back in: removal signed this PC out, and getting back in means being
    // invited again.
    await expect(win.getByRole('button', { name: 'Continue with Google' })).toBeVisible();
    await expect(win.getByRole('navigation', { name: 'Screens' })).toHaveCount(0);
    // No name or address in the log.
    for (const f of [ownerPc, staffPc]) for (const secret of ['staff@example.com', 'Sample Staff', 'Sample Business']) expect(readFileSync(join(f, 'app.log'), 'utf8')).not.toContain(secret);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await cloud.close();
    await fs.close();
    rmSync(ownerPc, { recursive: true, force: true });
    rmSync(staffPc, { recursive: true, force: true });
  }
});

test('the owner console suspends a workspace, and the PCs in it lock without losing anything', async () => {
  const cloud = await fakeCloud();
  const fs = await fakeFirestore();
  const env = { ...CLOUD, UM_CLOUD_ENDPOINT: cloud.endpoint, UM_SIGNIN_OPEN: 'fetch', UM_FIRESTORE: fs.base };
  const PRODUCT_OWNER = { uid: 'test-uid', email: 'owner@example.com', name: 'Sample Owner' };
  const STAFF = { uid: 'staff-uid', email: 'staff@example.com', name: 'Sample Staff' };
  // The product owner's marker, made by hand in the console.
  fs.docs.set('projects/test-project/databases/(default)/documents/owners/test-uid', { fields: { note: { stringValue: 'product owner' } }, updateTime: new Date().toISOString() });
  const ownerPc = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }],
    locations: [{ name: 'Main branch' }],
  });
  const staffPc = dataFolder();
  // The gate first: sign in there when it is showing, then Settings. The owner's marker is what admits the
  // person who starts a workspace; everyone else gets in by being invited to one.
  const workspace = async (win: Page) => {
    const rail = win.getByRole('navigation', { name: 'Screens' });
    const gateIn = win.getByRole('button', { name: 'Continue with Google' });
    await gateIn.or(rail).first().waitFor({ timeout: 30_000 });
    if (await gateIn.isVisible()) { await gateIn.click(); await rail.waitFor({ timeout: 60_000 }); }
    await rail.getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Workspace' }).click();
  };
  cloud.o.user = PRODUCT_OWNER;
  let { app, win } = await open(ownerPc, env);
  try {
    // Signed in at the gate, so Settings opens already signed in.
    await workspace(win);
    await win.getByLabel('Workspace name').fill('Sample Business');
    await win.getByRole('button', { name: 'Start the workspace' }).click();
    await expect(win.getByText('You are an admin: changes made here reach the workspace.')).toBeVisible();
    await win.getByRole('button', { name: 'Invite someone' }).click();
    await win.getByLabel('Their Google address').fill('staff@example.com');
    await win.getByRole('button', { name: 'Save the invitation' }).click();
    await expect(win.getByRole('row', { name: /staff@example.com/ })).toBeVisible();
    await quit(app, win);

    // The member joins on their own PC, and gets the account.
    cloud.o.user = STAFF;
    ({ app, win } = await open(staffPc, env));
    // Invited, so the gate carries the invitation and Join is on it.
    await win.getByRole('button', { name: 'Continue with Google' }).click();
    await win.getByRole('button', { name: 'Join Sample Business' }).click({ timeout: 30_000 });
    await win.getByRole('navigation', { name: 'Screens' }).waitFor({ timeout: 30_000 });
    await expect.poll(() => JSON.parse(readFileSync(join(staffPc, 'config.json'), 'utf8')).accounts?.length ?? 0).toBe(1);
    await quit(app, win);

    // The product owner's console: every workspace, membership only, and Suspend.
    cloud.o.user = PRODUCT_OWNER;
    ({ app, win } = await open(ownerPc, env));
    await workspace(win);
    await win.getByRole('button', { name: 'Owner console' }).click();
    const row = win.getByRole('row', { name: /Sample Business/ });
    await expect(row).toContainText('Active');
    await expect(row).toContainText('owner@example.com');
    await row.getByRole('button', { name: 'Suspend' }).click();
    await expect(row.getByRole('alertdialog')).toContainText('Nothing is deleted');
    await row.getByRole('button', { name: 'Suspend' }).click();
    await expect(row).toContainText('Suspended');
    // The keyboard shortcut still opens the palette on a window opened later in the session.
    await win.keyboard.press('Control+k');
    await expect(win.getByRole('dialog', { name: 'Command palette' })).toBeVisible();
    await win.keyboard.press('Escape');
    // The product owner is not locked out of their own console by it.
    await expect(win.getByRole('heading', { name: 'Owner console' })).toBeVisible();
    await quit(app, win);

    // The member's PC locks at its next check, and keeps everything.
    cloud.o.user = STAFF;
    ({ app, win } = await open(staffPc, env));
    await expect(win.getByRole('heading', { name: 'Sample Business is suspended' })).toBeVisible();
    await expect(win.getByRole('navigation', { name: 'Screens' })).toHaveCount(0);
    expect(JSON.parse(readFileSync(join(staffPc, 'config.json'), 'utf8')).accounts?.length ?? 0).toBe(1);
    expect(readdirSync(staffPc)).toContain('cloud.json');
    await quit(app, win);

    // Restored: the member's PC comes back by itself.
    cloud.o.user = PRODUCT_OWNER;
    ({ app, win } = await open(ownerPc, env));
    await workspace(win);
    await win.getByRole('button', { name: 'Owner console' }).click();
    await win.getByRole('row', { name: /Sample Business/ }).getByRole('button', { name: 'Restore' }).click();
    await expect(win.getByRole('row', { name: /Sample Business/ })).toContainText('Active');
    await quit(app, win);

    cloud.o.user = STAFF;
    ({ app, win } = await open(staffPc, env));
    await expect(win.getByRole('navigation', { name: 'Screens' })).toBeVisible();
    await expect(win.getByRole('heading', { name: /is suspended/ })).toHaveCount(0);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await cloud.close();
    await fs.close();
    rmSync(ownerPc, { recursive: true, force: true });
    rmSync(staffPc, { recursive: true, force: true });
  }
});

/** An invented GitHub Releases, and the Setup it carries. */
async function fakeReleases(o: { version?: string; bytes?: number } = {}) {
  const { createServer } = await import('node:http');
  const version = o.version ?? '9.9.9';
  const setup = Buffer.alloc(o.bytes ?? 4096, 7);
  let at = '';
  const server = createServer((req, res) => {
    if (req.url?.startsWith('/releases/latest')) {
      res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify({
        tag_name: `v${version}`,
        body: '- A made-up improvement for this test.\n- Another one.',
        assets: [{ name: 'UnifiedMessenger6Setup.exe', size: setup.length, browser_download_url: `${at}/setup` }],
      }));
    } else if (req.url === '/setup') {
      res.writeHead(200, { 'Content-Type': 'application/octet-stream', 'Content-Length': String(setup.length) }).end(setup);
    } else res.writeHead(404).end();
  });
  await new Promise<void>((done) => server.listen(0, '127.0.0.1', done));
  at = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
  return { url: `${at}/releases/latest`, setupBytes: setup.length, close: () => new Promise<void>((done) => { server.close(() => done()); }) };
}

test('an update: found, downloaded when the owner asks, and installed only when they say', async () => {
  const releases = await fakeReleases();
  const data = dataFolder();
  const { app, win } = await open(data, { UM_UPDATE_URL: releases.url });
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'About' }).click();
    await expect(win.getByText(/^Version \d/)).toBeVisible();
    // Nothing is downloaded before the owner asks.
    await win.getByRole('button', { name: 'Check for updates' }).click();
    await expect(win.getByRole('status').filter({ hasText: 'Version 9.9.9 is ready to download.' })).toBeVisible();
    expect(readdirSync(data)).not.toContain('updates');

    await win.getByRole('button', { name: 'Show the update' }).click();
    const drawer = win.getByRole('dialog', { name: 'Update' });
    await expect(drawer).toContainText('A made-up improvement for this test.');
    await drawer.getByRole('button', { name: 'Download it' }).click();
    await expect(drawer.getByRole('button', { name: 'Install and restart' })).toBeVisible({ timeout: 15_000 });
    await expect(drawer).toContainText('Logins, figures and waiting customers are all kept.');
    // The Setup is on this PC, whole.
    const file = join(data, 'updates', '9.9.9', 'UnifiedMessenger6Setup.exe');
    expect(statSync(file).size).toBe(releases.setupBytes);
    // Putting it off leaves it downloaded and the app running.
    await drawer.getByRole('button', { name: 'When the business closes' }).click();
    await expect(win.getByRole('navigation', { name: 'Screens' })).toBeVisible();
    // The check said nothing about this PC: it is a plain GET, and the log holds counts only.
    const log = readFileSync(join(data, 'app.log'), 'utf8');
    expect(log).toContain('"event":"update-checked"');
    expect(log).toContain('"event":"update-downloaded"');
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await releases.close();
    rmSync(data, { recursive: true, force: true });
  }
});

test('an update already installed is not offered again', async () => {
  const current = JSON.parse(readFileSync(join(V6, 'package.json'), 'utf8')).version as string;
  const releases = await fakeReleases({ version: current });
  const data = dataFolder();
  const { app, win } = await open(data, { UM_UPDATE_URL: releases.url });
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'About' }).click();
    await win.getByRole('button', { name: 'Check for updates' }).click();
    await expect(win.getByRole('status').filter({ hasText: 'Unified Messenger is up to date.' })).toBeVisible();
    await expect(win.getByRole('button', { name: 'Show the update' })).toHaveCount(0);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await releases.close();
    rmSync(data, { recursive: true, force: true });
  }
});

test('moving from the previous version: everything comes across, and the screen says what to do once', async () => {
  // A v5 install of its own: two accounts and the settings beside them. Only ever read.
  const v5 = mkdtempSync(join(tmpdir(), 'um-v5-'));
  writeFileSync(join(v5, 'instances.json'), JSON.stringify({ version: 3, instances: [
    { id: 'acc-1', displayName: 'Main branch WhatsApp', profileName: 'whatsapp-acc-1', startUrl: 'about:blank', platform: 'whatsapp', category: 'Professional', branchKey: 'Main branch' },
    { id: 'acc-2', displayName: 'Main branch Instagram', profileName: 'instagram-acc-2', startUrl: 'about:blank', platform: 'instagram', category: 'Professional', branchKey: 'Main branch' },
  ] }));
  writeFileSync(join(v5, 'settings.json'), JSON.stringify({ slaThresholdMinutes: 20, themePreference: 'Dark' }));
  const data = mkdtempSync(join(tmpdir(), 'um-smoke-'));
  const { app, win } = await open(data, { UM_V5: v5 });
  try {
    // The screen stands in front of everything, once.
    await expect(win.getByRole('heading', { name: /Everything came across/ })).toBeVisible();
    await expect(win.getByText('Main branch WhatsApp')).toBeVisible();
    await expect(win.getByText('Main branch Instagram')).toBeVisible();
    await expect(win.getByText(/Scan the code on the phone/)).toBeVisible();
    await expect(win.getByRole('navigation', { name: 'Screens' })).toHaveCount(0);
    // The setup itself came across, and v5's own files were only read.
    const config = JSON.parse(readFileSync(join(data, 'config.json'), 'utf8'));
    expect(config.accounts.map((a: { name: string }) => a.name)).toEqual(['Main branch WhatsApp', 'Main branch Instagram']);
    expect(config.settings.slaMinutes).toBe(20);
    expect(readdirSync(v5).sort()).toEqual(['instances.json', 'settings.json']);

    // Read once: the app opens, and the next launch will not show it again.
    await win.getByRole('button', { name: 'Start using it' }).click();
    await expect(win.getByRole('navigation', { name: 'Screens' })).toBeVisible();
    await expect.poll(() => JSON.parse(readFileSync(join(data, 'upgraded.json'), 'utf8')).read).toBe(true);
  } finally {
    // v5 carries no "closing really quits" setting, so this app would sit in the tray: end it outright.
    await app.evaluate(({ app: electronApp }) => electronApp.exit(0)).catch(() => {});
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
    rmSync(v5, { recursive: true, force: true });
  }
});

test('the upgrade screen is shown until it has been read, and never again after', async () => {
  for (const [read, shown] of [[false, true], [true, false]] as const) {
    const data = dataFolder({ accounts: [{ id: 'a1', name: 'Main branch WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'Main branch' }] });
    writeFileSync(join(data, 'upgraded.json'), JSON.stringify({ read }));
    const { app, win } = await open(data);
    try {
      await expect(win.getByRole('heading', { name: /Everything came across/ })).toHaveCount(shown ? 1 : 0);
      await expect(win.getByRole('navigation', { name: 'Screens' })).toHaveCount(shown ? 0 : 1);
      await quit(app, win);
    } finally {
      await app.close().catch(() => {});
      rmSync(data, { recursive: true, force: true });
    }
  }
});

test('the gate: a fresh install reads nothing until it has been let in, and a stranger never gets past it', async () => {
  const cloud = await fakeCloud();
  const fs = await fakeFirestore();
  const env = { ...CLOUD, UM_CLOUD_ENDPOINT: cloud.endpoint, UM_SIGNIN_OPEN: 'fetch', UM_FIRESTORE: fs.base };
  const fixtures = join(V6, 'tests', 'fixtures');
  const data = dataFolder({
    accounts: [{ id: 'test-wa', name: 'Test front desk', channel: 'whatsapp', professional: true, location: 'Main branch',
      url: pathToFileURL(join(fixtures, 'whatsapp-saved-list.html')).href }],
    locations: [{ name: 'Main branch' }],
  });
  const events = () => readFileSync(join(data, 'app.log'), 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l) as { event: string });
  const STRANGER = { uid: 'stranger-uid', email: 'stranger@example.com', name: 'A Stranger' };
  cloud.o.user = STRANGER;

  let { app, win } = await open(data, env);
  try {
    // Signed out: the sign-in screen is the whole window, and there is no way round it.
    await expect(win.getByRole('button', { name: 'Continue with Google' })).toBeVisible({ timeout: 30_000 });
    await expect(win.getByRole('navigation', { name: 'Screens' })).toHaveCount(0);
    await expect(win.getByRole('button', { name: 'Back to the app' })).toHaveCount(0);

    // Long enough for two reading passes to have happened, and none has: nothing was read, and the account's
    // page was never opened, so its WhatsApp session was not touched at all.
    await win.waitForTimeout(8_000);
    const before = events().map((e) => e.event);
    expect(before.filter((e) => e === 'read' || e === 'read-empty' || e === 'read-failed' || e === 'reader-not-ready')).toEqual([]);
    expect(before).not.toContain('awake');

    // A stranger signs in: refused by name, still nothing read.
    await win.getByRole('button', { name: 'Continue with Google' }).click();
    await expect(heading(win, 'This account has not been invited')).toBeVisible({ timeout: 30_000 });
    await expect(win.getByRole('navigation', { name: 'Screens' })).toHaveCount(0);
    expect(events().map((e) => e.event)).not.toContain('awake');
    await quit(app, win);

    // The same PC, with the product owner's marker in place: in, and reading within a pass.
    fs.docs.set('projects/test-project/databases/(default)/documents/owners/stranger-uid',
      { fields: { note: { stringValue: 'product owner' } }, updateTime: new Date().toISOString() });
    ({ app, win } = await open(data, env));
    await expect(win.getByRole('navigation', { name: 'Screens' })).toBeVisible({ timeout: 30_000 });
    await expect.poll(() => events().filter((e) => e.event === 'read').length, { timeout: 90_000 }).toBeGreaterThanOrEqual(1);
    expect(events().map((e) => e.event)).toContain('awake');
    const gates = events().filter((e) => e.event === 'gate') as { event: string; phase?: string; because?: string }[];
    expect(gates.some((g) => g.phase === 'admitted' && g.because === 'product-owner'), 'the log says why it opened').toBe(true);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await cloud.close();
    await fs.close();
    rmSync(data, { recursive: true, force: true });
  }
});

test('an invitation names the accounts: the member gets only those, and loses one when the admin narrows it', async () => {
  const cloud = await fakeCloud();
  const fs = await fakeFirestore();
  const env = { ...CLOUD, UM_CLOUD_ENDPOINT: cloud.endpoint, UM_SIGNIN_OPEN: 'fetch', UM_FIRESTORE: fs.base };
  const OWNER = { uid: 'test-uid', email: 'owner@example.com', name: 'Sample Owner' };
  const STAFF = { uid: 'staff-uid', email: 'staff@example.com', name: 'Sample Staff' };
  fs.docs.set(`projects/test-project/databases/(default)/documents/owners/${OWNER.uid}`,
    { fields: { note: { stringValue: 'product owner' } }, updateTime: new Date().toISOString() });
  const ownerPc = dataFolder({
    accounts: [
      { id: 'dha-wa', name: 'DHA-2 WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'DHA-2' },
      { id: 'dha-ig', name: 'DHA-2 Instagram', channel: 'instagram', url: 'about:blank', professional: true, location: 'DHA-2' },
      { id: 'f11-wa', name: 'F-11 WhatsApp', channel: 'whatsapp', url: 'about:blank', professional: true, location: 'F-11' },
    ],
    locations: [{ name: 'DHA-2' }, { name: 'F-11' }],
  });
  const staffPc = dataFolder();
  const settings = async (win: Page) => {
    const rail = win.getByRole('navigation', { name: 'Screens' });
    const gateIn = win.getByRole('button', { name: 'Continue with Google' });
    await gateIn.or(rail).first().waitFor({ timeout: 30_000 });
    if (await gateIn.isVisible()) { await gateIn.click(); await rail.waitFor({ timeout: 30_000 }); }
    await rail.getByRole('button', { name: 'Settings', exact: true }).click();
    await win.getByRole('navigation', { name: 'Settings sections' }).getByRole('button', { name: 'Workspace' }).click();
  };
  const staffAccounts = () => JSON.parse(readFileSync(join(staffPc, 'config.json'), 'utf8')).accounts.map((a: { id: string }) => a.id).sort();

  cloud.o.user = OWNER;
  let { app, win } = await open(ownerPc, env);
  try {
    await settings(win);
    await win.getByLabel('Workspace name').fill('Sample Business');
    await win.getByRole('button', { name: 'Start the workspace' }).click();
    await expect(win.getByText('You are an admin: changes made here reach the workspace.')).toBeVisible();

    // Invited to one branch, not the business: ticking the branch ticks its accounts.
    await win.getByRole('button', { name: 'Invite someone' }).click();
    await win.getByLabel('Their Google address').fill('staff@example.com');
    await win.getByRole('group', { name: 'Accounts they can see' }).getByRole('button', { name: 'Only the ones I pick' }).click();
    await win.getByRole('checkbox', { name: 'DHA-2', exact: true }).check();
    await expect(win.getByRole('checkbox', { name: 'DHA-2 WhatsApp' })).toBeChecked();
    await expect(win.getByRole('checkbox', { name: 'F-11 WhatsApp' })).not.toBeChecked();
    await win.getByLabel('A line for them (optional)').fill('Welcome \u2014 you are on the DHA-2 desk.');
    await win.getByRole('button', { name: 'Save the invitation' }).click();
    await expect(win.getByText('staff@example.com')).toBeVisible();
    await quit(app, win);

    // Their PC gets the two DHA-2 accounts and never hears of F-11.
    cloud.o.user = STAFF;
    ({ app, win } = await open(staffPc, env));
    await win.getByRole('button', { name: 'Continue with Google' }).click();
    // The gate names what they were invited to, and carries the admin's own line to them.
    await expect(win.locator('body')).toContainText('Welcome \u2014 you are on the DHA-2 desk.', { timeout: 30_000 });
    await win.getByRole('button', { name: 'Join Sample Business' }).click({ timeout: 30_000 });
    await win.getByRole('navigation', { name: 'Screens' }).waitFor({ timeout: 30_000 });
    await expect.poll(staffAccounts, { timeout: 30_000 }).toEqual(['dha-ig', 'dha-wa']);
    const theirs = JSON.parse(readFileSync(join(staffPc, 'config.json'), 'utf8'));
    // A branch with none of their accounts is not sent at all.
    expect(theirs.locations.map((l: { name: string }) => l.name)).toEqual(['DHA-2']);
    await quit(app, win);

    // The admin narrows it to one account; their PC drops the other at its next check.
    cloud.o.user = OWNER;
    ({ app, win } = await open(ownerPc, env));
    await settings(win);
    const row = win.getByRole('row').filter({ hasText: 'staff@example.com' });
    await expect(row).toContainText('2 of 3');
    await row.getByRole('button', { name: 'Change' }).click();
    await row.getByRole('checkbox', { name: 'DHA-2 Instagram' }).click();
    await expect(row).toContainText('1 of 3');
    await quit(app, win);

    cloud.o.user = STAFF;
    ({ app, win } = await open(staffPc, env));
    await expect.poll(staffAccounts, { timeout: 60_000 }).toEqual(['dha-wa']);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    await cloud.close();
    await fs.close();
    rmSync(ownerPc, { recursive: true, force: true });
    rmSync(staffPc, { recursive: true, force: true });
  }
});
