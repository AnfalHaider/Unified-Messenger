// First launch only. If this PC has a v5 install and v6 has no config yet, bring the whole thing across:
// accounts, locations and settings, plus the history that makes the first morning honest — what was on
// screen, how fast replies went out, and which chats the owner had already handled or snoozed.
//
// v5's files are only ever read. The app keeps working afterwards, and running v6 again never re-imports.
import { existsSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { importV5, importV5History } from '../core/import-v5.ts';
import { loadJson, saveJson } from './store.ts';

/** Where v5 keeps its files. UM_V5 points elsewhere, which is how this is exercised without a v5 install. */
export const v5Folder = () =>
  process.env.UM_V5 || join(process.env.LOCALAPPDATA || join(homedir(), 'AppData', 'Local'), 'UnifiedMessenger');

export interface V6Files { config: string; snapshot: string; times: string; overrides: string }

/** True when an import happened, so the caller knows to read the files it just wrote. */
export function importFromV5(files: V6Files, log: (entry: Record<string, unknown>) => void): boolean {
  // A config already here means this is not a first run. Never import over the owner's own setup.
  if (existsSync(files.config)) return false;
  const from = v5Folder();
  if (!existsSync(join(from, 'instances.json'))) return false;

  const read = (name: string) => loadJson<unknown>(join(from, name), null, (why) => log({ event: 'v5-file-problem', file: name, why }));
  const { config, report } = importV5(read('instances.json'), read('settings.json'));
  const history = importV5History(config.accounts.map((a) => a.id), {
    snapshot: read('oversight-snapshot.json'),
    responseTimes: read('response-times.json'),
    overrides: read('awaiting-overrides.json'),
  });

  saveJson(files.config, config);
  saveJson(files.snapshot, history.snapshots);
  saveJson(files.times, history.times);
  saveJson(files.overrides, history.overrides);

  // Counts and the owner's own business names only — no customer is named here.
  log({
    event: 'imported-v5', from,
    accounts: report.accounts, archived: report.archived, skipped: report.skipped.length,
    locations: report.locations.length, guessedLocations: report.guessedLocations.length,
    chats: history.report.chats, replySamples: history.report.samples, pendingWaits: history.report.pending,
    handled: history.report.handled, snoozed: history.report.snoozed,
    orphanedAccounts: history.report.orphaned.length, droppedRows: history.report.dropped,
    notes: [...report.notes, ...history.report.notes],
  });
  return true;
}
