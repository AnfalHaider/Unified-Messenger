// Port of OversightRollupBuilder.cs, rebuilt on the chat snapshot. v5 grouped thread-registry rows (written only
// by its WhatsApp ingress pipeline) and let the snapshot override the headline figures whenever one existed.
// v6 has no thread registry, so every figure comes from the snapshot. Two v5 thread figures are redefined from
// the same rules: "past target" is a waiting chat whose business-hours wait exceeds its location's SLA (v5
// IsSlaBreached), and "at risk" is one past max(2 x SLA, 30 min) (v5 revenue leakage). v5's "urgent" came from
// an AI urgency score on threads and has no equivalent yet.
import { elapsedBusinessMinutes, type BusinessHours } from './business-hours.ts';
import { calendarDaysBetween } from './days.ts';
import { honestPercent } from './percent.ts';
import { isAwaiting, windowed, type Judge, type Snapshots } from './snapshot.ts';

export const DEFAULT_SLA_MINUTES = 15, MIN_SLA_MINUTES = 5, MAX_SLA_MINUTES = 120;
const AT_RISK_FLOOR_MINUTES = 30;
const TREND_DAYS = 7;

export interface RollupAccount {
  id: string;
  name: string;
  /** Location name. Blank means the account stands alone under its own name. */
  location?: string;
  /** The channel can read message times and direction; others are never scored past target. */
  supportsTiming: boolean;
  /** Not connected: the numbers are old. */
  stale?: boolean;
  /** Showing a sign-in screen: there are no numbers, so the card must show none rather than zeroes. */
  signedOut?: boolean;
  /** The last read failed outright. Never inferred from a zero. */
  readFailed?: boolean;
}

export interface LocationRules { slaMinutes?: number | null; hours?: BusinessHours | null }

export interface RollupOptions {
  groupBy: 'account' | 'location';
  from?: number | null;
  to?: number | null;
  /** Global target; a location's own overrides it. */
  slaMinutes?: number;
  locations?: Record<string, LocationRules>;
}

export interface EntityHealth {
  key: string;
  name: string;
  kind: 'account' | 'location';
  accountIds: string[];
  /** Chats the on-time % is computed over. 0 means there is no measurement, so show none rather than a %. */
  measured: number;
  awaiting: number;
  onTimePercent: number;
  pastTarget: number;
  atRisk: number;
  /** False until a member has been read: show "syncing", never zeroes. */
  hasChatData: boolean;
  supportsTiming: boolean;
  stale: boolean;
  signedOut: boolean;
  readFailed: boolean;
  lastActivity: number | null;
  /** Chats last active on each of the last 7 local days, oldest first. Empty, not zeroes, when nothing was read. */
  trend: number[];
}

export interface Rollup { entities: EntityHealth[]; totalPastTarget: number; totalAtRisk: number; worstKey: string | null; summary: string }

export function buildRollup(accounts: RollupAccount[], snapshots: Snapshots, judge: Judge, options: RollupOptions): Rollup {
  const groups = new Map<string, RollupAccount[]>();
  for (const a of accounts) {
    const key = options.groupBy === 'location' ? a.location?.trim() || a.name.trim() || a.id : a.id;
    groups.set(key, [...(groups.get(key) ?? []), a]);
  }

  const entities = [...groups]
    .map(([key, members]) => entityHealth(key, members, snapshots, judge, options))
    .sort((x, y) => y.pastTarget - x.pastTarget || x.onTimePercent - y.onTimePercent || y.atRisk - x.atRisk);

  const sum = (pick: (e: EntityHealth) => number) => entities.reduce((n, e) => n + pick(e), 0);
  const totalPastTarget = sum((e) => e.pastTarget), totalAtRisk = sum((e) => e.atRisk), totalAwaiting = sum((e) => e.awaiting);
  const worst = entities.find((e) => e.pastTarget > 0 || e.onTimePercent < 100) ?? null;
  const customers = (n: number) => `${n} customer${n === 1 ? '' : 's'}`;
  const summary = !entities.some((e) => e.hasChatData) ? 'Nothing has been read yet.'
    : totalPastTarget > 0 ? `${customers(totalPastTarget)} need${totalPastTarget === 1 ? 's' : ''} a reply now — most urgent at ${entities[0].name}`
    : totalAwaiting > 0 ? `${customers(totalAwaiting)} waiting`
    : 'All caught up.';

  return { entities, totalPastTarget, totalAtRisk, worstKey: worst?.key ?? null, summary };
}

function entityHealth(key: string, members: RollupAccount[], snapshots: Snapshots, judge: Judge, o: RollupOptions): EntityHealth {
  let active = 0, caughtUp = 0, pastTarget = 0, atRisk = 0, hasChatData = false;
  let lastActivity: number | null = null;
  const trend = Array<number>(TREND_DAYS).fill(0);

  for (const a of members) {
    const id = a.id.trim();
    const counts = windowed(snapshots, id, judge, o.from ?? null, o.to ?? null);
    if (!counts) continue;
    hasChatData = true;
    active += counts.active;
    caughtUp += counts.caughtUp;

    const rules = o.locations?.[a.location?.trim() ?? ''];
    const sla = Math.min(MAX_SLA_MINUTES, Math.max(MIN_SLA_MINUTES, rules?.slaMinutes ?? o.slaMinutes ?? DEFAULT_SLA_MINUTES));
    for (const c of snapshots[id].chats) {
      lastActivity = Math.max(lastActivity ?? c.lastActivity, c.lastActivity);
      const daysAgo = calendarDaysBetween(c.lastActivity, judge.now);
      if (daysAgo >= 0 && daysAgo < TREND_DAYS) trend[TREND_DAYS - 1 - daysAgo]++;
      if (!a.supportsTiming || !isAwaiting(id, c, judge)) continue;
      // The clock runs only inside the location's working hours, so a message at closing time is not a breach by morning.
      const waited = elapsedBusinessMinutes(new Date(c.lastActivity), new Date(judge.now), rules?.hours);
      if (waited > sla) pastTarget++;
      if (waited > Math.max(2 * sla, AT_RISK_FLOOR_MINUTES)) atRisk++;
    }
  }

  return {
    key,
    name: o.groupBy === 'location' ? key : members[0].name.trim() || key,
    kind: o.groupBy,
    accountIds: members.map((m) => m.id),
    measured: active,
    awaiting: active - caughtUp,
    onTimePercent: active > 0 ? honestPercent(caughtUp, active) : 100,
    pastTarget,
    atRisk,
    hasChatData,
    // Any member, so a location mixing channels still reports the timing it does have.
    supportsTiming: members.some((m) => m.supportsTiming),
    // Stale and signed out need EVERY member: one working account still gives real, partial numbers.
    stale: members.every((m) => m.stale === true),
    signedOut: members.every((m) => m.signedOut === true),
    // ANY member: a location quietly missing one branch is reporting incomplete numbers.
    readFailed: members.some((m) => m.readFailed === true),
    lastActivity,
    trend: hasChatData ? trend : [],
  };
}
