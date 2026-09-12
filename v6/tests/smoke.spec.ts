// The window opens, draws a screen, moves to another, and quits the ordinary way. Needs `npx vite build` first:
// without dist-ui the window stays blank and this fails, which is the point.
import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const V6 = join(dirname(fileURLToPath(import.meta.url)), '..');

/** A data folder of its own, never the owner's. A config already present also means no v5 import is tried, and
 *  closing quits instead of hiding, so the process really ends. */
function dataFolder(config: Record<string, unknown> = {}) {
  const data = mkdtempSync(join(tmpdir(), 'um-smoke-'));
  writeFileSync(join(data, 'config.json'), JSON.stringify({ ...config, settings: { closeToBackground: false } }));
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
  ] } }));

  const { app, win } = await open(data);
  try {
    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Reports', exact: true }).click();
    await expect(heading(win, 'Last 7 days: 50% answered on time')).toBeVisible();
    await expect(win.getByText('Recording since')).toBeVisible();
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
    await expect(heading(win, '1 missed call is still waiting for an answer')).toBeVisible();
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
