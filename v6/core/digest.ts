// The morning digest: who is still owed a reply from before the location closed, and how many wrote since.
// "Closed" is the location's last closing when it has opening hours, and midnight when it does not, so the
// digest never claims a closing time the owner never set. The yesterday-by-location figures come from report.ts.
import type { ChatEntry } from './chat-entry.ts';
import { lastClosing } from './business-hours.ts';
import { hoursFor, type Config } from './config.ts';
import { DAY_MS, dayKey, startOfDay } from './days.ts';
import { awaitingChats, type Judge, type Snapshots } from './snapshot.ts';

export interface MorningSplit {
  /** True when at least one of these accounts has been read. */
  hasData: boolean;
  /** Waiting since before their location closed (or before midnight), oldest first. */
  owed: { account: string; chat: ChatEntry }[];
  /** Waiting, and wrote after that. */
  overnight: number;
  /** True when any counted account's split came from real opening hours rather than midnight. */
  byHours: boolean;
}

export function morningSplit(config: Config, snapshots: Snapshots, judge: Judge, accounts: string[], now: number): MorningSplit {
  const split: MorningSplit = { hasData: false, owed: [], overnight: 0, byHours: false };
  // The same backlog line as the line itself: older than that is backlog, reported elsewhere, not "owed this morning".
  const cutoff = now - Math.max(1, config.settings.backlogAfterDays) * DAY_MS;
  for (const id of accounts) {
    if (!snapshots[id]) continue;
    split.hasData = true;
    const location = config.accounts.find((a) => a.id === id)?.location ?? '';
    const closed = lastClosing(hoursFor(config, location), now);
    if (closed !== null) split.byHours = true;
    const since = closed ?? startOfDay(now);
    for (const chat of awaitingChats(snapshots, id, judge)) {
      if (chat.lastActivity < cutoff) continue;
      if (chat.lastActivity < since) split.owed.push({ account: id, chat });
      else split.overnight++;
    }
  }
  split.owed.sort((a, b) => a.chat.lastActivity - b.chat.lastActivity);
  return split;
}

/** Shown on the first opening of each local day, when the owner has it on. */
export const digestDue = (now: number, lastShownDay: string | null, enabled: boolean) => enabled && dayKey(now) !== lastShownDay;
