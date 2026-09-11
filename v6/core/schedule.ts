// Who gets read, when, and what may be put to sleep. Pure decisions: the app layer owns the timers, the windows
// and the actual reading, and asks this what to do next.
//
// The rule that matters most here is the one v5 learned the hard way: an account that is not awake is never
// read, and its numbers simply stop moving while the dashboard keeps showing them. So an account that feeds
// oversight is never put to sleep, whatever the setting says.
import { CHANNELS, type Account, type Config, type Settings } from './config.ts';

/** Accounts read for oversight: marked professional, on a channel that has a reader. */
export const readableAccounts = (config: Config): Account[] =>
  config.accounts.filter((a) => a.professional && CHANNELS[a.channel].reads);

/** Ids due for a read, longest-waiting first. An account never read yet is due immediately. */
export function dueForRead(config: Config, lastReadAt: Record<string, number>, now: number): string[] {
  const every = config.settings.readEverySeconds * 1000;
  return readableAccounts(config)
    .map((a) => ({ id: a.id, last: lastReadAt[a.id] ?? Number.NEGATIVE_INFINITY }))
    .filter((a) => now - a.last >= every)
    .sort((x, y) => x.last - y.last)
    .map((a) => a.id);
}

/** When the next read falls due: now (or earlier) when one already is, null when nothing is read at all. */
export function nextReadDueAt(config: Config, lastReadAt: Record<string, number>, now: number): number | null {
  const every = config.settings.readEverySeconds * 1000;
  const due = readableAccounts(config).map((a) => (lastReadAt[a.id] ?? Number.NEGATIVE_INFINITY) + every);
  return due.length ? Math.min(...due) : null;
}

/** Ids safe to put to sleep: the setting is on, the account is not read for oversight, it is not on screen, and
 *  it has been untouched for long enough. */
export function accountsToSleep(config: Config, lastUsedAt: Record<string, number>, now: number, visibleAccountId?: string): string[] {
  const { sleepUnusedAccounts, sleepAfterMinutes } = config.settings;
  if (!sleepUnusedAccounts || sleepAfterMinutes <= 0) return [];
  const reads = new Set(readableAccounts(config).map((a) => a.id));
  return config.accounts
    .filter((a) => !reads.has(a.id) && a.id !== visibleAccountId && now - (lastUsedAt[a.id] ?? 0) >= sleepAfterMinutes * 60_000)
    .map((a) => a.id);
}

/** Quiet hours suppress alerts, never reads: the numbers stay current, the owner is simply not interrupted.
 *  The window wraps past midnight (21 → 8); a start equal to its end is no window at all. */
export function inQuietHours(settings: Settings, now: number): boolean {
  const { enabled, startHour, endHour } = settings.quietHours;
  if (!enabled || startHour === endHour) return false;
  const hour = new Date(now).getHours();
  return startHour < endHour ? hour >= startHour && hour < endHour : hour >= startHour || hour < endHour;
}
