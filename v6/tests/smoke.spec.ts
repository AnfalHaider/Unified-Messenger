// The window opens, draws a screen, moves to another, and quits the ordinary way. Needs `npx vite build` first:
// without dist-ui the window stays blank and this fails, which is the point.
import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { AxeBuilder } from '@axe-core/playwright';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
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

async function open(data: string): Promise<{ app: ElectronApplication; win: Page }> {
  const app = await electron.launch({ args: ['app/main.ts'], cwd: V6, env: { ...process.env, UM_DATA: data, UM_V5: join(data, 'no-v5') } });
  return { app, win: await app.firstWindow() };
}

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
    expect(await app.evaluate(({ clipboard }) => clipboard.readText())).toBe('Our current price list is on the way.');
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
    }
    expect(problems, problems.join('\n')).toEqual([]);
    await quit(app, win);
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});
