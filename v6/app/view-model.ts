// What the screens draw. The main process computes this from core and pushes it; the renderer only renders,
// which is what keeps every number in one place and the UI free of rules.
//
// Nothing here decides anything on its own: waiting comes from snapshot, grouping from rollup, elapsed time
// from business-hours, freshness from freshness. This file only shapes their answers for the screen.
import { elapsedBusinessMinutes } from '../core/business-hours.ts';
import { CHANNELS, type Config } from '../core/config.ts';
import { describeFreshness } from '../core/freshness.ts';
import { responseStats, type ResponseTimes } from '../core/response-times.ts';
import { buildRollup } from '../core/rollup.ts';
import { readableAccounts } from '../core/schedule.ts';
import { awaitingChats, awaitingSplit, lastCaptured, type Judge, type Snapshots } from '../core/snapshot.ts';
import type { Overrides } from '../core/awaiting-overrides.ts';

/** How close to the target counts as "due soon". The design's warning window. */
const DUE_SOON_MINUTES = 5;
/** The meter runs to three times the target, so being past it is visible before the number is read. */
const METER_SCALE = 3;

export type Tone = 'ok' | 'due' | 'late' | 'neutral';

export interface QueueRow {
  accountId: string;
  accountName: string;
  location: string;
  channel: string;
  customer: string;
  preview: string;
  /** Minutes waited inside the location's working hours. */
  waited: number;
  status: string;
  tone: Tone;
  /** 0–100 across the meter, and where the target sits on it. */
  fill: number;
  target: number;
}

export interface UiState {
  theme: 'system' | 'light' | 'dark';
  /** Which account's live page is on screen, or null for the dashboard. */
  visible: string | null;
  greetingName: string;
  meta: string;
  freshness: { text: string; isStale: boolean; hasData: boolean };
  strip: { tone: Tone; text: string } | null;
  figures: { label: string; value: string; unit: string; note: string; tone: Tone }[];
  split: { needsReply: number; backlog: number; closedAutomatically: number; unreadable: number };
  queue: QueueRow[];
  queueTotal: number;
  locations: { name: string; waiting: number; onTimePercent: number; tone: Tone; accounts: number }[];
  accounts: { id: string; name: string; channel: string; location: string; waiting: number | null; signedOut: boolean }[];
  reads: boolean;
}

export interface Context {
  now: number;
  visible: string | null;
  /** Accounts whose last read found a sign-in screen. Their figures are hidden, never guessed. */
  signedOut: Set<string>;
}

export function buildUiState(config: Config, snapshots: Snapshots, times: ResponseTimes, overrides: Overrides, ctx: Context): UiState {
  const { now } = ctx;
  const judge: Judge = { now, overrides, filterClosed: config.settings.filterClosedConversations };
  const readable = readableAccounts(config);
  const ids = readable.map((a) => a.id);
  const split = awaitingSplit(snapshots, ids, judge, config.settings.backlogAfterDays);
  const stats = responseStats(times, ids, config.settings.slaMinutes, { now });
  const freshness = describeFreshness(lastCaptured(snapshots), now);

  const rollup = buildRollup(
    config.accounts.map((a) => ({
      id: a.id, name: a.name, location: a.location, supportsTiming: CHANNELS[a.channel].reads,
      signedOut: ctx.signedOut.has(a.id),
    })),
    snapshots, judge,
    { groupBy: 'location', slaMinutes: config.settings.slaMinutes, locations: Object.fromEntries(config.locations.map((l) => [l.name, { slaMinutes: l.slaMinutes, hours: l.hours }])) },
  );

  const queue: QueueRow[] = [];
  for (const account of readable) {
    const rules = config.locations.find((l) => l.name === account.location);
    const target = Math.max(1, rules?.slaMinutes ?? config.settings.slaMinutes);
    for (const chat of awaitingChats(snapshots, account.id, judge)) {
      const waited = Math.round(elapsedBusinessMinutes(new Date(chat.lastActivity), new Date(now), rules?.hours));
      const remaining = target - waited;
      queue.push({
        accountId: account.id, accountName: account.name, location: account.location, channel: account.channel,
        customer: chat.customerName || chat.contactPhone || 'Unknown number',
        preview: chat.preview,
        waited,
        status: remaining < 0 ? 'Past target' : remaining <= DUE_SOON_MINUTES ? `Due in ${remaining} min` : 'On time',
        tone: remaining < 0 ? 'late' : remaining <= DUE_SOON_MINUTES ? 'due' : 'ok',
        fill: Math.min(100, (waited / (target * METER_SCALE)) * 100),
        target: 100 / METER_SCALE,
      });
    }
  }
  queue.sort((a, b) => b.waited - a.waited);

  const pastTarget = queue.filter((q) => q.tone === 'late').length;
  const dueSoon = queue.filter((q) => q.tone === 'due').length;
  const onTime = rollup.entities.length
    ? Math.round(rollup.entities.reduce((n, e) => n + e.onTimePercent, 0) / rollup.entities.length)
    : 100;

  return {
    theme: config.settings.theme,
    visible: ctx.visible,
    greetingName: '',
    meta: `${config.locations.length} location${config.locations.length === 1 ? '' : 's'} · ${readable.length} account${readable.length === 1 ? '' : 's'} read${ctx.signedOut.size ? ` · ${ctx.signedOut.size} needs sign-in` : ''}`,
    freshness,
    strip: dueSoon > 0
      ? { tone: 'due', text: `${dueSoon} customer${dueSoon === 1 ? '' : 's'} pass${dueSoon === 1 ? 'es' : ''} the ${config.settings.slaMinutes}-minute target within ${DUE_SOON_MINUTES} minutes.` }
      : ctx.signedOut.size
        ? { tone: 'neutral', text: `${ctx.signedOut.size} account${ctx.signedOut.size === 1 ? '' : 's'} need signing in again. Their figures are hidden rather than guessed.` }
        : null,
    figures: [
      { label: 'Waiting now', value: String(split.needsReply), unit: split.needsReply === 1 ? 'customer' : 'customers', note: split.backlog ? `${split.backlog} more in backlog` : 'Nothing older than the backlog line', tone: pastTarget ? 'late' : split.needsReply ? 'due' : 'ok' },
      { label: 'Past target', value: String(pastTarget), unit: `over ${config.settings.slaMinutes} min`, note: dueSoon ? `${dueSoon} due within ${DUE_SOON_MINUTES} min` : 'None due in the next few minutes', tone: pastTarget ? 'late' : 'ok' },
      { label: 'Answered on time', value: String(onTime), unit: '%', note: `Target is 90%`, tone: onTime >= 90 ? 'ok' : onTime >= 80 ? 'due' : 'late' },
      { label: 'First reply', value: stats.hasData ? stats.medianMinutes.toFixed(0) : '—', unit: 'min median', note: stats.hasData ? `${stats.sampleCount} replies measured` : 'No replies measured yet', tone: !stats.hasData ? 'neutral' : stats.medianMinutes <= config.settings.slaMinutes ? 'ok' : 'late' },
    ],
    split,
    queue: queue.slice(0, 60),
    queueTotal: queue.length,
    locations: rollup.entities.map((e) => ({
      name: e.name, waiting: e.awaiting, onTimePercent: e.onTimePercent, accounts: e.accountIds.length,
      tone: e.pastTarget ? 'late' : e.awaiting ? 'due' : 'ok',
    })),
    accounts: config.accounts.map((a) => ({
      id: a.id, name: a.name, channel: a.channel, location: a.location,
      waiting: CHANNELS[a.channel].reads ? awaitingChats(snapshots, a.id, judge).length : null,
      signedOut: ctx.signedOut.has(a.id),
    })),
    reads: ids.length > 0,
  };
}
