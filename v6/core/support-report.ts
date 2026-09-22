// The report the owner saves from the reader screen and sends to support.
//
// The whole point is that it can be sent **as it is**, without anyone reading it first. So the same rule as
// `app.log` holds, and harder: counts, timings, outcomes and the owner's own labels for their accounts and
// locations. Never a customer's name or number, never message or review text, never a login, never an address
// a person could be found by. `report()` is given only what it is allowed to say, and `SAFE` pins it.
import type { Settings } from './config.ts';

export interface SupportInput {
  version: string;
  electron: string;
  node: string;
  platform: string;
  /** Now, so the reader knows how old the figures are. */
  now: number;
  settings: Settings;
  accounts: SupportAccount[];
  /** Per channel, as the reader screen shows it. */
  modules: { id: string; name: string; ok: number; failed: number; lastError: string | null }[];
  /** The tail of app.log, newest last. Already counts-only by its own rule; passed through untouched. */
  log: string[];
}

export interface SupportAccount {
  /** The id the log uses, so a line in the log can be matched to an account here. */
  id: string;
  /** The owner's own label: "DHA-2 WhatsApp". Not a customer, not a login. */
  name: string;
  channel: string;
  location: string;
  awake: boolean;
  signedOut: boolean;
  /** When the last read finished, and what it saw. Null before the first read of this run. */
  lastReadAt: number | null;
  chats: number | null;
  waiting: number | null;
  /** The outcomes on record, newest last: 'read read failed failed'. Outcomes only, no figures. */
  recent: string[];
}

const when = (at: number | null, now: number) => {
  if (at === null) return 'not yet this run';
  const minutes = Math.round((now - at) / 60_000);
  return minutes < 1 ? 'just now' : minutes < 60 ? `${minutes} min ago` : `${Math.round(minutes / 60)} h ago`;
};

/**
 * The report as plain text. Written to be read by a person: what the app is, what each account is doing, what
 * each reader has managed, and the tail of the log underneath.
 */
export function supportReport(input: SupportInput): string {
  const { version, electron, node, platform, now, settings, accounts, modules, log } = input;
  const out: string[] = [];
  const line = (s = '') => out.push(s);

  line('Unified Messenger — report for support');
  line(`Saved ${new Date(now).toISOString()}`);
  line();
  line(`App ${version} · Electron ${electron} · Node ${node} · ${platform}`);
  line(`Reading every ${settings.readEverySeconds}s · reply target ${settings.slaMinutes} min · ${settings.readLimits.whatsappChats} chats per WhatsApp read`);
  line(`Closing ${settings.closeToBackground ? 'keeps reading' : 'quits'} · sleeping idle accounts ${settings.sleepUnusedAccounts ? 'on' : 'off'} · assistant ${settings.assistant.enabled ? `on (${settings.assistant.model})` : 'off'}`);
  line();

  line(`Accounts (${accounts.length})`);
  if (!accounts.length) line('  none');
  for (const a of accounts) {
    const state = a.signedOut ? 'SIGNED OUT' : a.awake ? 'awake' : 'asleep';
    line(`  ${a.name} — ${a.channel} at ${a.location} — ${state}`);
    line(`    id ${a.id} · last read ${when(a.lastReadAt, now)}${a.chats === null ? '' : ` · ${a.chats} chats, ${a.waiting} waiting`}`);
    if (a.recent.length) line(`    recent: ${a.recent.join(' ')}`);
  }
  line();

  line('Readers');
  if (!modules.length) line('  none');
  for (const m of modules) {
    line(`  ${m.name} — ${m.ok} good, ${m.failed} failed${m.lastError ? ` · last error: ${m.lastError}` : ''}`);
  }
  line();

  line(`Log, last ${log.length} lines`);
  for (const l of log) line(`  ${l}`);
  line();
  line('This report carries counts, timings and the names you gave your own accounts. It carries no customer');
  line('names or numbers, no message or review text, and no logins.');
  return out.join('\n');
}

/**
 * Anything here in a report means something leaked. Used by the test, and by main before it writes the file:
 * a report that trips this is not saved, because a report nobody can send is better than one that says too much.
 */
export function unsafeIn(report: string, forbidden: string[]): string[] {
  const hay = report.toLowerCase();
  return forbidden.filter((word) => word.trim().length > 2 && hay.includes(word.toLowerCase()));
}
