// Updates, carried out (roadmap 7.3). Owner's decision 2026-09-20: keep the Inno Setup installer, and have the app
// ask GitHub Releases whether a newer one exists. core/update.ts decides what is newer and what the owner is told.
//
// Three rules this keeps: the check sends nothing about this PC (a plain GET, no identifier); nothing is downloaded
// until the owner asks; and nothing is installed until they say when, because installing closes the app and every
// account page with it. No Electron here, so the tests run it against a GitHub of their own.
import { createWriteStream } from 'node:fs';
import { mkdir, rm, stat } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { join } from 'node:path';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import { CHECK_EVERY_MS, FIRST_CHECK_MS, readRelease, RELEASES_URL, SETUP_NAME, type Release, type UpdateState } from '../core/update.ts';

export interface UpdaterHost {
  /** Where the release list is. Tests point this at a GitHub of their own. */
  url?: string;
  /** The version running now, from package.json. */
  version: string;
  /** A folder of this PC's own for the downloaded Setup. */
  dir: string;
  /** Quits the app so the installer can replace it. */
  quit: () => void;
  log: (entry: Record<string, unknown>) => void;
  changed: () => void;
}

export class Updater {
  state: UpdateState = { phase: 'none' };
  private timers: NodeJS.Timeout[] = [];
  private readonly h: UpdaterHost;

  constructor(host: UpdaterHost) { this.h = host; }

  /** The first check waits a couple of minutes; after that, every six hours. */
  start() {
    this.timers.push(setTimeout(() => void this.check(), FIRST_CHECK_MS));
    this.timers.push(setInterval(() => void this.check(), CHECK_EVERY_MS));
    for (const t of this.timers) t.unref?.();
  }

  stop() { for (const t of this.timers) { clearTimeout(t); clearInterval(t); } this.timers = []; }

  /** Asks GitHub whether there is a newer release. Sends nothing but the request itself. */
  async check(manual = false): Promise<void> {
    if (this.state.phase === 'downloading' || this.state.phase === 'ready') return;
    try {
      const res = await fetch(this.h.url ?? RELEASES_URL, { headers: { Accept: 'application/vnd.github+json', 'User-Agent': 'UnifiedMessenger' } });
      if (!res.ok) throw new Error(`releases ${res.status}`);
      const release = readRelease(await res.json(), this.h.version, this.h.url ?? RELEASES_URL);
      this.set(release ? { phase: 'found', release } : { phase: 'none' });
      this.h.log({ event: 'update-checked', found: release?.version ?? null, manual });
    } catch (e) {
      this.h.log({ event: 'update-check-failed', error: String((e as Error).message).slice(0, 120) });
      // Only worth saying when the owner asked; a quiet failure is just this PC being offline.
      if (manual) this.set({ phase: 'failed', error: 'The update could not be checked for. Check the connection and try again.' });
    }
  }

  /** Downloads the Setup the release carries, to a folder of this PC's own. The owner asked for this. */
  async download(): Promise<void> {
    const release: Release | undefined = this.state.phase === 'found' ? this.state.release : undefined;
    if (!release) return;
    const dir = join(this.h.dir, release.version);
    const file = join(dir, SETUP_NAME);
    try {
      this.set({ phase: 'downloading', release, progress: 0 });
      await rm(dir, { recursive: true, force: true });
      await mkdir(dir, { recursive: true });
      const res = await fetch(release.url);
      if (!res.ok || !res.body) throw new Error(`download ${res.status}`);
      const total = release.size || Number(res.headers.get('content-length')) || 0;
      let had = 0, said = 0;
      const body = Readable.fromWeb(res.body as Parameters<typeof Readable.fromWeb>[0]);
      body.on('data', (chunk: Buffer) => {
        had += chunk.length;
        const progress = total ? Math.min(1, had / total) : 0;
        // The screens need this a few times, not a few thousand.
        if (progress - said >= 0.05) { said = progress; this.set({ phase: 'downloading', release, progress }); }
      });
      await pipeline(body, createWriteStream(file));
      const written = (await stat(file)).size;
      if (total && written !== total) throw new Error(`size ${written} of ${total}`);
      this.set({ phase: 'ready', release, file });
      this.h.log({ event: 'update-downloaded', version: release.version, bytes: written });
    } catch (e) {
      await rm(dir, { recursive: true, force: true }).catch(() => undefined);
      this.h.log({ event: 'update-download-failed', error: String((e as Error).message).slice(0, 120) });
      this.set({ phase: 'failed', error: 'The update could not be downloaded. It can be tried again when the connection is better.' });
    }
  }

  /** Runs the downloaded Setup and closes the app: the installer waits for it, replaces it and opens it again. */
  install(): { error?: string } {
    if (this.state.phase !== 'ready') return { error: 'There is no update ready to install.' };
    try {
      spawn(this.state.file, ['/SILENT', '/SUPPRESSMSGBOXES', '/NORESTART'], { detached: true, stdio: 'ignore' }).unref();
      this.h.log({ event: 'update-installing', version: this.state.release.version });
      this.h.quit();
      return {};
    } catch (e) {
      this.h.log({ event: 'update-install-failed', error: String((e as Error).message).slice(0, 120) });
      return { error: 'The update could not be started. Try again, or run the Setup yourself.' };
    }
  }

  /** Put off until later: the drawer closes and the update waits, already downloaded. */
  private set(state: UpdateState) { this.state = state; this.h.changed(); }
}
