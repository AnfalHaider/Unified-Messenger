// What happened on each account's reads, so the app can say what led up to a lost login or a broken reader instead
// of guessing. Counts and outcomes only: never a customer name, a number or message text — this is the same rule as
// `app.log`, because these lines are shown on screen and could be read out to support.
import { durationText } from './duration.ts';

export type Outcome = 'read' | 'empty' | 'not-ready' | 'signed-out' | 'signed-in' | 'failed' | 'awake' | 'asleep' | 'reload' | 'page-gone';

export interface ReadEvent {
  account: string;
  channel: string;
  at: number;
  outcome: Outcome;
  /** Chats the read saw, and how many of them were waiting. Null for anything that was not a read. */
  chats: number | null;
  waiting: number | null;
  /** Where the reader stopped, in the reader's own words: 'no-store', 'bridge-absent', 'empty'. */
  stage: string | null;
}

export type Events = Record<string, ReadEvent[]>;

/** Per account. Enough to show what led up to a sign-out an hour ago, small enough to keep in one file. */
export const KEEP_EVENTS = 60;

export interface TimelineItem {
  /** When, or null for a line about the present ("Still signed out"). */
  at: number | null;
  tone: 'ok' | 'due' | 'late' | 'neutral';
  title: string;
  detail: string;
}

/** Adds one event, keeping the newest `KEEP_EVENTS` per account. Consecutive identical outcomes are kept: the
 *  timeline collapses them when it draws, which is where the collapsing can be read. */
export function recordEvent(events: Events, event: ReadEvent, keep = KEEP_EVENTS) {
  const list = (events[event.account] ??= []);
  list.push(event);
  if (list.length > keep) list.splice(0, list.length - keep);
}

export const eventsFor = (events: Events, account: string): ReadEvent[] => events[account] ?? [];

/** Whether the run of events at the end is a sign-out, and when it began. */
export function signedOutSince(events: Events, account: string): number | null {
  const list = eventsFor(events, account);
  let since: number | null = null;
  for (let i = list.length - 1; i >= 0; i--) {
    const e = list[i];
    if (e.outcome === 'signed-out') since = e.at;
    else if (e.outcome === 'awake' || e.outcome === 'asleep' || e.outcome === 'reload') continue;
    else break;
  }
  return since;
}

const plural = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`;
const minutesText = durationText;

function readDetail(e: ReadEvent): string {
  const chats = e.chats === null ? 'The page answered' : `${plural(e.chats, 'chat')} read`;
  return e.waiting === null ? `${chats}.` : `${chats}, ${e.waiting} waiting.`;
}

const STOPPED: Record<string, string> = {
  'no-store': 'The page was signed in, but its own data had not been built yet.',
  'bridge-absent': 'The page had not finished loading its own code.',
  empty: 'The page answered with nothing at all.',
};

/** One event as a line. `count` is how many identical reads it stands for. */
function line(e: ReadEvent, count: number): TimelineItem {
  switch (e.outcome) {
    case 'read': return count > 1
      ? { at: e.at, tone: 'ok', title: `${count} good reads`, detail: `The last saw ${readDetail(e).toLowerCase()}` }
      : { at: e.at, tone: 'ok', title: 'Good read', detail: readDetail(e) };
    case 'signed-out': return { at: e.at, tone: 'late', title: 'Sign-in screen', detail: 'The login screen replaced the page. Marked as needing a sign-in, and its figures are hidden rather than counted as zero.' };
    case 'signed-in': return { at: e.at, tone: 'ok', title: 'Signed in again', detail: 'The page is being read again.' };
    case 'not-ready': return { at: e.at, tone: 'due', title: count > 1 ? `Not ready, ${count} times` : 'Reader not ready', detail: e.stage ? STOPPED[e.stage] ?? `The reader stopped at ${e.stage}.` : 'The reader was not ready yet.' };
    case 'empty': return { at: e.at, tone: 'due', title: count > 1 ? `Nothing came back, ${count} times` : 'Nothing came back', detail: 'The page answered, but with no chats. Its figures are hidden rather than counted as zero.' };
    case 'failed': return { at: e.at, tone: 'late', title: count > 1 ? `${count} reads did not finish` : 'A read did not finish', detail: e.stage ?? 'The page did not answer in time. The other accounts were read as usual.' };
    case 'reload': return { at: e.at, tone: 'due', title: 'Page reloaded', detail: 'The page loaded itself again. The app did not ask it to.' };
    case 'page-gone': return { at: e.at, tone: 'late', title: 'Page closed itself', detail: 'The page stopped running and was opened again.' };
    case 'awake': return { at: e.at, tone: 'neutral', title: 'Page opened', detail: 'The account’s page was opened so it could be read.' };
    default: return { at: e.at, tone: 'neutral', title: 'Page put to sleep', detail: 'Nobody had looked at it for a while, so it was closed to save memory.' };
  }
}

/** Runs of the same outcome become one line, so a quiet hour does not push the interesting minute off the screen. */
function collapse(list: ReadEvent[]): TimelineItem[] {
  const items: TimelineItem[] = [];
  for (let i = 0; i < list.length;) {
    let n = 1;
    while (i + n < list.length && list[i + n].outcome === list[i].outcome && list[i + n].stage === list[i].stage) n++;
    items.push(line(list[i + n - 1], n));
    i += n;
  }
  return items;
}

/** What led up to this account's lost login: the reads before it, the sign-out itself, and how long since. When the
 *  account is signed in, the last few events, so the screen still says something true. */
export function lostLoginTimeline(events: Events, account: string, now: number, lines = 5): TimelineItem[] {
  const list = eventsFor(events, account);
  if (!list.length) return [];
  const since = signedOutSince(events, account);
  const cut = since === null ? list.length : list.findIndex((e) => e.at === since && e.outcome === 'signed-out') + 1;
  const items = collapse(list.slice(0, cut)).slice(-lines);
  if (since !== null) {
    items.push({
      at: null, tone: 'neutral', title: `Still signed out, ${minutesText(Math.max(0, Math.round((now - since) / 60_000)))}`,
      detail: 'Customers who wrote since then are not being counted. Signing in again picks them up.',
    });
  }
  return items;
}

/** What happened to a channel's reader across every account on it: one page changing shape breaks them all at once,
 *  so it is told as one story rather than per account. */
export function readerTimeline(events: Events, accounts: string[], now: number, lines = 6): TimelineItem[] {
  const all = accounts.flatMap((id) => eventsFor(events, id)).sort((a, b) => a.at - b.at);
  if (!all.length) return [];
  const items: TimelineItem[] = [];
  const bad = all.filter((e) => e.outcome === 'not-ready' || e.outcome === 'empty' || e.outcome === 'failed');
  const firstBad = bad[0];
  if (firstBad && accounts.length > 1) {
    const alsoFailed = new Set(bad.filter((e) => e.at >= firstBad.at).map((e) => e.account));
    if (alsoFailed.size > 1) {
      items.push({
        at: firstBad.at, tone: 'late', title: `${alsoFailed.size} accounts stopped reading`,
        detail: 'Every account on this channel, so it is reported once, as the reader, not as one account.',
      });
    }
  }
  const tail = collapse(all.slice(-40)).slice(-(lines - items.length));
  items.push(...tail);
  const last = all[all.length - 1];
  const quiet = Math.round((now - last.at) / 60_000);
  if (quiet >= 5) items.push({ at: null, tone: 'due', title: `Nothing read for ${minutesText(quiet)}`, detail: 'Reading is tried again every minute.' });
  return items;
}
