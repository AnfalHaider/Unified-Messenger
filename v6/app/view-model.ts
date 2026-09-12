// What the screens draw. The main process computes this from core and pushes it; the renderer only renders,
// which is what keeps every number in one place and the UI free of rules.
//
// Nothing here decides anything on its own: waiting comes from snapshot, grouping from rollup, elapsed time
// from business-hours, freshness from freshness. This file only shapes their answers for the screen.
import { elapsedBusinessMinutes } from '../core/business-hours.ts';
import { CHANNELS, type Config } from '../core/config.ts';
import { describeFreshness } from '../core/freshness.ts';
import { dailyResponse, responseStats, type ResponseTimes } from '../core/response-times.ts';
import { buildRollup } from '../core/rollup.ts';
import { readableAccounts } from '../core/schedule.ts';
import { awaitingChats, awaitingSplit, lastCaptured, type Judge, type Snapshots } from '../core/snapshot.ts';
import type { Overrides } from '../core/awaiting-overrides.ts';

/** How close to the target counts as "due soon". The design's warning window. */
const DUE_SOON_MINUTES = 5;
/** The meter runs to three times the target, so being past it is visible before the number is read. */
const METER_SCALE = 3;

export type Tone = 'ok' | 'due' | 'late' | 'neutral';
export type Route = 'dashboard' | 'account' | 'account-detail' | 'settings';

export interface Figure { label: string; value: string; unit: string; note: string; tone: Tone }

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

export interface AccountDetail {
  id: string;
  name: string;
  location: string;
  channel: string;
  /** False for a channel with no reader today: the screen says so rather than showing zeroes. */
  reads: boolean;
  signedOut: boolean;
  asleep: boolean;
  capturedAt: number | null;
  freshness: { text: string; isStale: boolean; hasData: boolean };
  figures: Figure[];
  /** Median first reply per local day, oldest first, with the target to draw against. */
  daily: { label: string; median: number; count: number }[];
  targetMinutes: number;
  health: { tone: Tone; title: string; detail: string }[];
  queue: QueueRow[];
}

export interface UiState {
  theme: 'system' | 'light' | 'dark';
  route: Route;
  /** Which account the account screens are about, and whose page may be on screen. */
  visible: string | null;
  meta: string;
  freshness: { text: string; isStale: boolean; hasData: boolean };
  strip: { tone: Tone; text: string } | null;
  figures: Figure[];
  split: { needsReply: number; backlog: number; closedAutomatically: number; unreadable: number };
  queue: QueueRow[];
  queueTotal: number;
  locations: { name: string; waiting: number; onTimePercent: number; tone: Tone; accounts: number }[];
  accounts: { id: string; name: string; channel: string; location: string; waiting: number | null; signedOut: boolean; asleep: boolean }[];
  reads: boolean;
  detail: AccountDetail | null;
  settings: Config['settings'];
}

export interface Context {
  now: number;
  route: Route;
  visible: string | null;
  /** Accounts whose last read found a sign-in screen. Their figures are hidden, never guessed. */
  signedOut: Set<string>;
  /** Accounts with no page open right now. Their numbers are the last ones read, not live. */
  asleep: Set<string>;
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
    { groupBy: 'location', slaMinutes: config.settings.slaMinutes, locations: locationRules(config) },
  );

  const queue = readable.flatMap((a) => queueFor(config, snapshots, judge, a.id, now));
  queue.sort((x, y) => y.waited - x.waited);

  const pastTarget = queue.filter((q) => q.tone === 'late').length;
  const dueSoon = queue.filter((q) => q.tone === 'due').length;
  const onTime = rollup.entities.length
    ? Math.round(rollup.entities.reduce((n, e) => n + e.onTimePercent, 0) / rollup.entities.length)
    : 100;

  return {
    theme: config.settings.theme,
    route: ctx.route,
    visible: ctx.visible,
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
      { label: 'Answered on time', value: String(onTime), unit: '%', note: 'Target is 90%', tone: onTime >= 90 ? 'ok' : onTime >= 80 ? 'due' : 'late' },
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
      asleep: ctx.asleep.has(a.id),
    })),
    reads: ids.length > 0,
    detail: ctx.visible ? detailFor(config, snapshots, times, judge, ctx) : null,
    settings: config.settings,
  };
}

const locationRules = (config: Config) =>
  Object.fromEntries(config.locations.map((l) => [l.name, { slaMinutes: l.slaMinutes, hours: l.hours }]));

function queueFor(config: Config, snapshots: Snapshots, judge: Judge, accountId: string, now: number): QueueRow[] {
  const account = config.accounts.find((a) => a.id === accountId);
  if (!account) return [];
  const rules = config.locations.find((l) => l.name === account.location);
  const target = Math.max(1, rules?.slaMinutes ?? config.settings.slaMinutes);
  return awaitingChats(snapshots, account.id, judge).map((chat) => {
    // The clock only runs inside the location's working hours, so a message at closing time is not late by morning.
    const waited = Math.round(elapsedBusinessMinutes(new Date(chat.lastActivity), new Date(now), rules?.hours));
    const remaining = target - waited;
    return {
      accountId: account.id, accountName: account.name, location: account.location, channel: account.channel,
      customer: chat.customerName || chat.contactPhone || 'Unknown number',
      preview: chat.preview,
      waited,
      status: remaining < 0 ? 'Past target' : remaining <= DUE_SOON_MINUTES ? `Due in ${remaining} min` : 'On time',
      tone: (remaining < 0 ? 'late' : remaining <= DUE_SOON_MINUTES ? 'due' : 'ok') as Tone,
      fill: Math.min(100, (waited / (target * METER_SCALE)) * 100),
      target: 100 / METER_SCALE,
    };
  });
}

function detailFor(config: Config, snapshots: Snapshots, times: ResponseTimes, judge: Judge, ctx: Context): AccountDetail | null {
  const account = config.accounts.find((a) => a.id === ctx.visible);
  if (!account) return null;
  const reads = CHANNELS[account.channel].reads;
  const snap = snapshots[account.id];
  const rules = config.locations.find((l) => l.name === account.location);
  const target = Math.max(1, rules?.slaMinutes ?? config.settings.slaMinutes);
  const stats = responseStats(times, [account.id], target, { now: ctx.now });
  const queue = reads ? queueFor(config, snapshots, judge, account.id, ctx.now) : [];
  queue.sort((a, b) => b.waited - a.waited);
  const pastTarget = queue.filter((q) => q.tone === 'late').length;
  const signedOut = ctx.signedOut.has(account.id);

  return {
    id: account.id, name: account.name, location: account.location, channel: account.channel, reads, signedOut,
    asleep: ctx.asleep.has(account.id),
    capturedAt: snap?.capturedAt ?? null,
    freshness: describeFreshness(snap?.capturedAt ?? null, ctx.now),
    figures: reads ? [
      { label: 'Waiting now', value: String(queue.length), unit: queue.length === 1 ? 'customer' : 'customers', note: pastTarget ? `${pastTarget} past the ${target}-minute target` : 'None past target', tone: pastTarget ? 'late' : queue.length ? 'due' : 'ok' },
      { label: 'First reply', value: stats.hasData ? stats.medianMinutes.toFixed(0) : '—', unit: 'min median', note: stats.hasData ? `${stats.sampleCount} replies measured` : 'No replies measured yet', tone: !stats.hasData ? 'neutral' : stats.medianMinutes <= target ? 'ok' : 'late' },
      { label: 'Within target', value: stats.hasData ? String(stats.slaPercent) : '—', unit: '%', note: `Target is ${target} minutes`, tone: !stats.hasData ? 'neutral' : stats.slaPercent >= 90 ? 'ok' : stats.slaPercent >= 80 ? 'due' : 'late' },
      { label: 'Chats read', value: String(snap?.chats.length ?? 0), unit: 'in the last read', note: snap ? describeFreshness(snap.capturedAt, ctx.now).text : 'Never read', tone: 'neutral' },
    ] : [],
    daily: reads ? dailyResponse(times, [account.id], target, { days: 7, now: ctx.now }).map((d) => ({ label: d.label, median: Math.round(d.medianMinutes), count: d.count })) : [],
    targetMinutes: target,
    health: health(account.channel, reads, signedOut, ctx.asleep.has(account.id), snap?.chats.length ?? 0, snap ? describeFreshness(snap.capturedAt, ctx.now) : null),
    queue: queue.slice(0, 40),
  };
}

function health(channel: string, reads: boolean, signedOut: boolean, asleep: boolean, chats: number, freshness: { text: string; isStale: boolean } | null) {
  const lines: { tone: Tone; title: string; detail: string }[] = [];
  if (!reads) {
    lines.push({ tone: 'neutral', title: 'No reader for this channel yet', detail: `${CHANNELS[channel as keyof typeof CHANNELS].name} has no oversight reader, so this account shows no figures rather than zeroes.` });
    return lines;
  }
  lines.push(signedOut
    ? { tone: 'late', title: 'Signed out', detail: 'The page is showing a sign-in screen, so nothing can be read. Its figures are hidden rather than guessed.' }
    : { tone: 'ok', title: 'Signed in on this PC', detail: 'The login is kept in this account\'s own session and survives a restart.' });
  lines.push(freshness?.isStale
    ? { tone: 'due', title: 'Reads are behind', detail: `${freshness.text}. If this persists, the reader may have stopped working.` }
    : { tone: freshness ? 'ok' : 'neutral', title: freshness ? 'Reader working' : 'Never read yet', detail: freshness ? `${chats} chats in the last read · ${freshness.text.toLowerCase()}` : 'This account has not been read since it was added.' });
  lines.push(asleep
    ? { tone: 'neutral', title: 'Asleep', detail: 'The page is closed to save memory. Its login is kept, and it wakes when opened.' }
    : { tone: 'ok', title: 'Awake', detail: 'The page stays open so the numbers keep moving.' });
  return lines;
}
