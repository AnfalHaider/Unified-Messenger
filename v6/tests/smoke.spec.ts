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
