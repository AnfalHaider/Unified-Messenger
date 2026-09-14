// Which Windows notifications are due right now. The app layer shows them and saves `Notified`; this only decides.
// Each alert fires as its moment is crossed and never again for the same wait, so opening the app on a long
// backlog replays nothing, and a pass every few seconds cannot repeat itself.
import { inQuietHours } from './schedule.ts';
import type { Settings } from './config.ts';

/** A customer waiting now, as the line shows them. `waited` is minutes inside the location's hours. */
export interface WaitingRow {
  accountId: string;
  accountName: string;
  key: string;
  customer: string;
  waited: number;
  targetMinutes: number;
  lastActivity: number;
  /** The location is open now. A closed location's clock is paused, so it has nothing to warn about. */
  open: boolean;
}

export interface AlertInput {
  rows: WaitingRow[];
  signedOut: { id: string; name: string }[];
  /** Accounts whose latest read succeeded. Only these re-arm their sign-in alert: an account the app has not read
   *  yet since starting is unknown, not signed back in, and treating it as signed in repeated the alert on restart. */
  signedIn: string[];
  /** Missed calls not returned yet, with who called. Absent means none. */
  calls?: { accountId: string; accountName: string; key: string; customer: string; at: number }[];
  settings: Settings;
  now: number;
}

export type AlertKind = 'near-target' | 'waited-hour' | 'signed-out' | 'call-not-returned';

/** Names a customer and an account, never message text: a toast can sit on a screen anyone walks past. */
export interface Alert { id: string; kind: AlertKind; title: string; body: string; accountId: string | null; key: string | null; customer: string | null }

/** Alert id → when it was shown. */
export type Notified = Record<string, number>;

/** A missed call not returned is announced after this many minutes, within a window so old calls are not replayed. */
export const CALL_ALERT_MINUTES = 30;
const CALL_WINDOW_MINUTES = 60;

/** How long before the target the warning comes. */
export const WARN_MINUTES = 2;
/** An hour's wait is announced only as it is crossed, not for every wait already older. */
const HOUR_WINDOW_MINUTES = 5;
/** More than this at once become a single alert that counts them. */
const MAX_SEPARATE = 3;
const FORGET_AFTER_MS = 2 * 24 * 60 * 60_000;

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

/** Returns what to show now and records it in `notified`. Quiet hours hold alerts back without using them up. */
export function alertsDue({ rows, signedOut, signedIn, calls = [], settings, now }: AlertInput, notified: Notified): Alert[] {
  // Signed back in: the next sign-out is news again.
  for (const id of signedIn) delete notified[`signed-out:${id}`];
  if (inQuietHours(settings, now)) return [];

  const { nearTarget, waitedHour, signedOut: signIn, callNotReturned } = settings.alerts;
  const candidates: Alert[] = [];
  for (const r of rows) {
    const chat = { accountId: r.accountId, key: r.key, customer: r.customer };
    const wait = `${r.accountId}:${r.key}:${r.lastActivity}`;
    if (nearTarget && r.open && r.waited >= r.targetMinutes - WARN_MINUTES && r.waited < r.targetMinutes) {
      const left = Math.max(1, Math.ceil(r.targetMinutes - r.waited));
      candidates.push({ id: `near:${wait}`, kind: 'near-target', title: `${r.customer} passes the target in ${plural(left, 'minute')}`, body: `${r.accountName} · waiting ${Math.floor(r.waited)} min`, ...chat });
    }
    if (waitedHour && r.waited >= 60 && r.waited < 60 + HOUR_WINDOW_MINUTES) {
      candidates.push({ id: `hour:${wait}`, kind: 'waited-hour', title: `${r.customer} has waited over an hour`, body: r.accountName, ...chat });
    }
  }
  if (callNotReturned) {
    for (const c of calls) {
      const minutes = (now - c.at) / 60_000;
      // Said once, as the half hour passes; a call already older when the app opens is not replayed.
      if (minutes < CALL_ALERT_MINUTES || minutes >= CALL_ALERT_MINUTES + CALL_WINDOW_MINUTES) continue;
      candidates.push({ id: `call:${c.accountId}:${c.key}:${c.at}`, kind: 'call-not-returned', title: `${c.customer} called ${Math.floor(minutes)} minutes ago and has had no call back or reply`, body: c.accountName, accountId: c.accountId, key: c.key, customer: c.customer });
    }
  }
  if (signIn) {
    for (const a of signedOut) {
      candidates.push({ id: `signed-out:${a.id}`, kind: 'signed-out', title: `${a.name} needs signing in again`, body: 'Its figures are hidden until it is signed in.', accountId: a.id, key: null, customer: null });
    }
  }

  const fresh = candidates.filter((a) => !(a.id in notified));
  for (const a of fresh) notified[a.id] = now;

  const shown: Alert[] = [];
  for (const kind of ['near-target', 'waited-hour', 'call-not-returned', 'signed-out'] as const) {
    const group = fresh.filter((a) => a.kind === kind);
    if (group.length <= MAX_SEPARATE) { shown.push(...group); continue; }
    const title = kind === 'near-target' ? `${group.length} customers pass the target within ${WARN_MINUTES} minutes`
      : kind === 'waited-hour' ? `${group.length} customers have waited over an hour`
        : kind === 'call-not-returned' ? `${group.length} missed calls have not been returned`
        : `${group.length} accounts need signing in again`;
    shown.push({ id: `${kind}:summary:${now}`, kind, title, body: 'Open the app to see them.', accountId: null, key: null, customer: null });
  }
  return shown;
}

/** Forgets old alerts so the record cannot grow without end. Call before saving. */
export function pruneNotified(notified: Notified, now: number) {
  for (const [id, at] of Object.entries(notified)) if (now - at > FORGET_AFTER_MS) delete notified[id];
}
