// The window opens, draws a screen, moves to another, and quits the ordinary way. Needs `npx vite build` first:
// without dist-ui the window stays blank and this fails, which is the point.
import { _electron as electron, expect, test } from '@playwright/test';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const V6 = join(dirname(fileURLToPath(import.meta.url)), '..');

test('the window renders, navigates and quits', async () => {
  // A data folder of its own, never the owner's. A config already present also means no v5 import is tried,
  // and closing quits instead of hiding, so the process really ends.
  const data = mkdtempSync(join(tmpdir(), 'um-smoke-'));
  writeFileSync(join(data, 'config.json'), JSON.stringify({ settings: { closeToBackground: false } }));

  const app = await electron.launch({
    args: ['app/main.ts'],
    cwd: V6,
    env: { ...process.env, UM_DATA: data, UM_V5: join(data, 'no-v5') },
  });
  try {
    const win = await app.firstWindow();
    await expect(win.getByRole('heading', { level: 1, name: /customers are waiting|No accounts are being read yet/ })).toBeVisible();

    await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: 'Accounts', exact: true }).click();
    await expect(win.getByRole('heading', { level: 1, name: 'Accounts', exact: true })).toBeVisible();

    const closed = app.waitForEvent('close');
    await win.getByRole('button', { name: 'Quit', exact: true }).click();
    await closed;
  } finally {
    await app.close().catch(() => {});
    rmSync(data, { recursive: true, force: true });
  }
});
