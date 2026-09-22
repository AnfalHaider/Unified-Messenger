// What the screens draw. The main process computes this from core and pushes it; the renderer only renders,
// which is what keeps every number in one place and the UI free of rules.
//
// Nothing here decides anything on its own: waiting comes from snapshot, grouping from rollup, elapsed time
// from business-hours, freshness from freshness. This file only shapes their answers for the screen.
import type { ModuleHealth } from '../channels/index.ts';
import { elapsedBusinessMinutes, isOpen } from '../core/business-hours.ts';
import { CHANNELS, hoursFor, type Config } from '../core/config.ts';
import { describeFreshness } from '../core/freshness.ts';
import { dailyResponse, responseStats, type ResponseTimes } from '../core/response-times.ts';
import { buildRollup } from '../core/rollup.ts';
import { readableAccounts } from '../core/schedule.ts';
import { DAY_MS, dayKey, startOfDay } from '../core/days.ts';
import { callsIn, type Calls } from '../core/calls.ts';
import { lostLoginTimeline, readerTimeline, signedOutSince, type Events, type TimelineItem } from '../core/events.ts';
import { customerFor, tagsInUse, type Customers } from '../core/customers.ts';
import { durationText } from '../core/duration.ts';
import { ageMinutes, needingReply, spread, type Reviews } from '../core/reviews.ts';
import { keptRows, keptTotal, sizeText, type KeptRow, type KeptSizes } from '../core/kept.ts';
import { admissionSentence, mayRun, type Admission } from '../core/admission.ts';
import { engineSentence, MODELS, offState, suggestModel, type EngineState, type ModelChoice } from '../core/assistant.ts';
import { morningSplit } from '../core/digest.ts';
import type { History } from '../core/history.ts';
import { buildReport, weekEnding, type Report } from '../core/report.ts';
import { explain } from '../core/reply-need.ts';
import { awaitingChats, awaitingSplit, lastCaptured, notCustomerWhy, setAside, type Judge, type Snapshots } from '../core/snapshot.ts';
import type { Overrides } from '../core/awaiting-overrides.ts';
import type { CloudState } from '../core/cloud-auth.ts';
import type { OwnerView, WorkspaceState } from './workspace.ts';
import type { UpdateState } from '../core/update.ts';

/** How close to the target counts as "due soon". The design's warning window. */
const DUE_SOON_MINUTES = 5;
/** The meter runs to three times the target, so being past it is visible before the number is read. */
const METER_SCALE = 3;

export type Tone = 'ok' | 'due' | 'late' | 'neutral';
/** Every screen in the shell. Main only cares which ones show an account: its page is laid over 'dock'. */
export type Route =
  | 'line' | 'dock' | 'set-aside' | 'digest'
  | 'accounts' | 'account-detail' | 'reader' | 'lost-login'
  | 'reviews' | 'reports' | 'assistant' | 'settings' | 'owner' | 'help';

/** Screens that are about one account, so navigating to them keeps that account in view. */
export const ACCOUNT_ROUTES: readonly Route[] = ['dock', 'account-detail', 'lost-login'];

export interface Figure { label: string; value: string; unit: string; note: string; tone: Tone; trend?: number[] }

/** One range of the Reports screen: the core report plus the sentences and facts drawn from it. */
export interface ReportRange {
  label: string;
  report: Report;
  headline: string;
  summary: string;
  /** Said when the app has recorded fewer days than the range covers, so a short history is not read as a full one. */
  coverage: string | null;
  facts: Figure[];
  /** One label per day, blank where the axis would crowd. */
  dayLabels: string[];
}

/** The weekly report document: every sentence computed from the week's figures, none phrased by a model. */
export interface WeeklyDoc {
  which: 'this' | 'last';
  title: string;
  hasData: boolean;
  coverage: string | null;
  lede: string;
  facts: Figure[];
  dayLabels: string[];
  report: Report;
  lookAt: string[];
  wentWell: string[];
}

export interface DigestView {
  hasData: boolean;
  title: string;
  summary: string;
  /** Still waiting from before the location closed (or before midnight), oldest first, at most 12. */
  owed: { accountId: string; accountName: string; key: string; customer: string; preview: string; since: number }[];
  owedTotal: number;
  /** Missed calls from the last two days that nobody has returned. */
  callsNotReturned: number;
  owedLabel: string;
  overnight: number;
  overnightNote: string;
  /** Yesterday per location, with the 14 days before it as a trend of the days that had replies. */
  yesterday: { name: string; replies: number; onTimePercent: number | null; medianMinutes: number | null; trend: number[] }[];
}

/** A missed call, with who called taken from the snapshot, and whether and how it was returned. */
export interface CallRow {
  accountId: string;
  accountName: string;
  location: string;
  key: string;
  customer: string;
  at: number;
  returnedAt: number | null;
  returnedBy: 'message' | 'call' | null;
}

export interface ReportsView {
  ranges: { today: ReportRange; week: ReportRange; month: ReportRange };
  weekly: { this: WeeklyDoc; last: WeeklyDoc };
  /** The location these reports cover, or null for every location. */
  scope: string | null;
  targetMinutes: number;
  /** Customers waiting over a day now, oldest first. */
  backlog: { accountId: string; accountName: string; key: string; customer: string; preview: string; since: number; waited: number }[];
  /** Missed calls not returned yet, newest first, from the last month. */
  unansweredCalls: CallRow[];
  /** Missed calls in each range, newest first. */
  calls: { today: CallRow[]; week: CallRow[]; month: CallRow[] };
}

export interface QueueRow {
  accountId: string;
  accountName: string;
  location: string;
  channel: string;
  /** The chat's identity within its account. Names repeat; this does not. */
  key: string;
  customer: string;
  preview: string;
  /** Minutes waited inside the location's working hours. */
  waited: number;
  status: string;
  tone: Tone;
  /** 0–100 across the meter, and where the target sits on it. */
  fill: number;
  target: number;
  targetMinutes: number;
  lastActivity: number;
  /** The location is open now, so the wait is growing. */
  open: boolean;
}

/** A waiting chat that is off the line without a reply, and why. */
export interface SetAsideRow {
  accountId: string;
  accountName: string;
  key: string;
  customer: string;
  preview: string;
  why: 'Handled' | 'Snoozed' | 'Closed by rule' | 'Not a customer';
  /** Only the owner's own marks can be put back from here; a rule is changed in Settings. */
  canPutBack: boolean;
  /** For a closure, the rule's reason; for a mark, what brings it back. */
  next: string;
  /** When a snooze ends. */
  until: number | null;
  /** When it left the line: the mark, or the message that closed it. */
  at: number;
}

/** Enough rows to check the rule's work without drawing hundreds. */
const SET_ASIDE_ROWS = 200;

export interface ReaderHealth { id: string; name: string; tone: Tone; status: string; detail: string }

/** One line of what the reads did, said the way a person would. Drawn by the lost-login and reader screens. */
export interface TimelineRow { at: string; tone: Tone | 'neutral'; title: string; detail: string }

/** One Google profile on the Reviews screen. Counts cover the latest reviews Google shows, and say so. */
export interface ReviewProfileView {
  accountId: string;
  name: string;
  location: string;
  rating: number | null;
  total: number | null;
  /** How many reviews the last read saw, and whether Google has more beyond them. */
  loaded: number;
  more: boolean;
  unanswered: number;
  /** The latest reviews by stars, five down to one. */
  spread: number[];
  /** When the reviews were last read, or null before the first read. */
  readAt: number | null;
  signedOut: boolean;
}

export interface ReviewRow { id: string; accountId: string; location: string; reviewer: string; text: string; stars: number; age: string; replied: boolean }

export interface ReviewsView {
  /** Accounts on the Google channel, whether or not they have been read yet. */
  profiles: ReviewProfileView[];
  /** Unanswered reviews across the profiles in scope, worst first, then oldest. */
  needing: ReviewRow[];
  /** Every review the reads saw, newest first. */
  recent: ReviewRow[];
}

/** One customer's panel: what the app has seen of them, and what the owner wrote about them. */
export interface CustomerCard {
  /** "Seen by the app": only things the reads actually saw, newest first. */
  seen: { label: string; value: string }[];
  note: string;
  tags: string[];
}

/** The docked account's customers, by conversation key: main does not know which chat the screen has open. */
export interface CustomerView {
  accountId: string;
  byKey: Record<string, CustomerCard>;
  /** Tags already used anywhere, offered so the same word is not typed two ways. */
  suggestions: string[];
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
  /** Everyone waiting per location name ("No location" for none), counted from the whole queue, not the rows drawn. */
  queueByLocation: Record<string, number>;
  setAside: SetAsideRow[];
  setAsideTotal: number;
  /** Built only while Reports is open. */
  reports: ReportsView | null;
  /** Built only while the morning digest is open. */
  digest: DigestView | null;
  locations: { name: string; waiting: number; onTimePercent: number; tone: Tone; accounts: number }[];
  /** Every account. `reads` is whether its channel has a reader; `counted` whether the owner has it counted. */
  accounts: { id: string; name: string; channel: string; location: string; waiting: number | null; signedOut: boolean; asleep: boolean; reads: boolean; counted: boolean }[];
  reads: boolean;
  detail: AccountDetail | null;
  /** One line per channel reader, so a channel that stopped working is named instead of averaged away. */
  modules: ReaderHealth[];
  /** The docked customer's panel. Built only while a chat is docked. */
  customer: CustomerView | null;
  /** Built only while Reviews is open. */
  reviews: ReviewsView | null;
  /** What this PC is holding, for Settings › Privacy. Null on every other screen: it is measured from disk. */
  kept: { rows: KeptRow[]; sizes: string[]; total: string } | null;
  /** Whether this copy has been let in at all, and what to say while it has not (core/admission.ts). */
  gate: { phase: Admission['phase']; mayRun: boolean; sentence: string };
  /** The local assistant: its engine's state in a sentence, and the model suggested for this PC. */
  assistant: { state: EngineState; sentence: string; suggested: ModelChoice; memoryGB: number; models: ModelChoice[] };
  /** Signing in to the workspace: unavailable in a build without the project's config, else signed out, waiting or in. */
  cloud: CloudState;
  /** The workspace this PC belongs to, and whether its setup is in step. */
  workspace: WorkspaceState;
  /** The product owner's console: empty and isOwner false for everyone else. */
  owner: OwnerView;
  /** A newer version, if one has been found. Nothing downloads or installs by itself. */
  update: UpdateState;
  /** The version running now. */
  version: string;
  /** True until the "moving from the previous version" screen has been read. */
  upgraded: boolean;
  /** What led up to this account losing its login, and since when. Built only while that screen is open. */
  lostLogin: { since: number | null; items: TimelineRow[] } | null;
  /** What each channel's reader has been doing, across every account on it. Built only while Readers is open. */
  readerStory: Record<string, TimelineRow[]>;
  settings: Config['settings'];
  /** Settings › Opening hours: each location's hours as written (holidays kept apart), and the holidays. */
  openingHours: { locations: { name: string; accounts: number; hours: Config['locations'][number]['hours'] }[]; holidays: Config['holidays'] };
}

export interface Context {
  now: number;
  /** What this PC is holding, measured from disk. Only while Settings is open. */
  kept?: KeptSizes;
  /** The gate's verdict. Absent in a preview, which is not gated. */
  gate?: Admission;
  /** The day records. Only read while Reports is open. */
  history?: History;
  /** Missed calls and whether they were returned. */
  calls?: Calls;
  /** What each account's reads did. Only read while the lost-login or reader screen is open. */
  events?: Events;
  /** Notes, tags and what the reads have seen of each customer. Only read while a chat is docked. */
  customers?: Customers;
  /** Each Google profile's latest reviews, rating and total. Only read while Reviews is open. */
  reviews?: Reviews;
  /** The local assistant's engine, and this PC's memory for suggesting a model. */
  assistant?: EngineState;
  memoryGB?: number;
  /** Who is signed in to the workspace, if anyone. Never the tokens. */
  cloud?: CloudState;
  workspace?: WorkspaceState;
  owner?: OwnerView;
  update?: UpdateState;
  version?: string;
  /** The first launch after everything came across from v5: the upgrade screen, once. */
  upgraded?: boolean;
  /** The location chosen in the title bar, or null for all. Reports and their exports cover only that location. */
  scope?: string | null;
  route: Route;
  visible: string | null;
  /** Accounts whose last read found a sign-in screen. Their figures are hidden, never guessed. */
  signedOut: Set<string>;
  /** Accounts with no page open right now. Their numbers are the last ones read, not live. */
  asleep: Set<string>;
  modules: ModuleHealth[];
}

/** The one judge: the owner's marks, the closed-chat setting and the not-a-customer rules. Main uses it too, so
 *  the day records and alerts cannot count a chat the screens leave out. */
export const judgeFor = (config: Config, overrides: Overrides, now: number): Judge =>
  ({ now, overrides, filterClosed: config.settings.filterClosedConversations, notCustomers: config.settings.notCustomers });

// A signed-out account's last snapshot is history, not a live queue: its chats stay off the line and out of
// the counts until it reads again, exactly as its figures are hidden rather than guessed.
const liveIds = (config: Config, signedOut: Set<string>) =>
  readableAccounts(config).map((a) => a.id).filter((id) => !signedOut.has(id));

/** Everyone waiting now, longest first and not cut to what the screen draws: the alerts need the newest too. */
export function waitingQueue(config: Config, snapshots: Snapshots, overrides: Overrides, now: number, signedOut: Set<string>): QueueRow[] {
  const judge = judgeFor(config, overrides, now);
  return liveIds(config, signedOut).flatMap((id) => queueFor(config, snapshots, judge, id, now)).sort((x, y) => y.waited - x.waited);
}

export function buildUiState(config: Config, snapshots: Snapshots, times: ResponseTimes, overrides: Overrides, ctx: Context): UiState {
  const { now } = ctx;
  const judge = judgeFor(config, overrides, now);
  const readable = readableAccounts(config);
  const ids = readable.map((a) => a.id);
  const live = liveIds(config, ctx.signedOut);
  const split = awaitingSplit(snapshots, live, judge, config.settings.backlogAfterDays);
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

  const queue = waitingQueue(config, snapshots, overrides, now, ctx.signedOut);
  const asideRows = setAside(snapshots, live, judge);

  const pastTarget = queue.filter((q) => q.tone === 'late').length;
  const dueSoon = queue.filter((q) => q.tone === 'due').length;
  // The share of active chats that have an answer. Not the same as answering within the target, and not labelled
  // as if it were: a business can be caught up by the evening and still have kept everyone waiting an hour.
  const caughtUp = rollup.entities.length
    ? Math.round(rollup.entities.reduce((n, e) => n + e.onTimePercent, 0) / rollup.entities.length)
    : 100;
  const broken = ctx.modules.filter((m) => m.lastError && m.ok === 0);

  return {
    theme: config.settings.theme,
    route: ctx.route,
    visible: ctx.visible,
    meta: `${config.locations.length} location${config.locations.length === 1 ? '' : 's'} · ${readable.length} account${readable.length === 1 ? '' : 's'} read${ctx.signedOut.size ? ` · ${ctx.signedOut.size} needs sign-in` : ''}`,
    freshness,
    // A reader that has never worked is said first: it means figures are missing, not that nobody is waiting.
    strip: broken.length
      ? { tone: 'late', text: `The ${broken.map((m) => m.name).join(' and ')} reader stopped working. Those figures are hidden rather than guessed; the other channels are unaffected.` }
      : dueSoon > 0
        ? { tone: 'due', text: `${dueSoon} customer${dueSoon === 1 ? '' : 's'} pass${dueSoon === 1 ? 'es' : ''} the ${config.settings.slaMinutes}-minute target within ${DUE_SOON_MINUTES} minutes.` }
        : ctx.signedOut.size
          ? { tone: 'neutral', text: `${ctx.signedOut.size} account${ctx.signedOut.size === 1 ? '' : 's'} need signing in again. Their figures are hidden rather than guessed.` }
          : null,
    figures: [
      { label: 'Waiting now', value: String(split.needsReply), unit: split.needsReply === 1 ? 'customer' : 'customers', note: split.backlog ? `${split.backlog} more in backlog` : 'Nothing older than the backlog line', tone: pastTarget ? 'late' : split.needsReply ? 'due' : 'ok' },
      { label: 'Past target', value: String(pastTarget), unit: `over ${config.settings.slaMinutes} min`, note: dueSoon ? `${dueSoon} due within ${DUE_SOON_MINUTES} min` : 'None due in the next few minutes', tone: pastTarget ? 'late' : 'ok' },
      { label: 'Caught up', value: String(caughtUp), unit: '%', note: 'Of the chats active today, those with an answer', tone: caughtUp >= 90 ? 'ok' : caughtUp >= 80 ? 'due' : 'late' },
      // Measured replies against the target — never a zero standing in for "nothing measured yet".
      { label: 'Answered on time', value: stats.hasData ? String(stats.slaPercent) : '—', unit: '%',
        note: stats.hasData ? `${stats.sampleCount} replies measured, target 90%` : 'No replies measured yet',
        tone: !stats.hasData ? 'neutral' : stats.slaPercent >= 90 ? 'ok' : stats.slaPercent >= 80 ? 'due' : 'late' },
      { label: 'First reply', value: stats.hasData ? stats.medianMinutes.toFixed(0) : '—', unit: 'min median', note: stats.hasData ? `${stats.sampleCount} replies measured` : 'No replies measured yet', tone: !stats.hasData ? 'neutral' : stats.medianMinutes <= config.settings.slaMinutes ? 'ok' : 'late' },
    ],
    split,
    queue: queue.slice(0, 60),
    queueTotal: queue.length,
    queueByLocation: queue.reduce<Record<string, number>>((n, q) => { const k = q.location || 'No location'; n[k] = (n[k] ?? 0) + 1; return n; }, {}),
    setAside: asideRows.slice(0, SET_ASIDE_ROWS).map((x) => {
      const account = config.accounts.find((a) => a.id === x.account);
      return {
        accountId: x.account, accountName: account?.name ?? x.account, key: x.chat.conversationKey,
        customer: x.chat.customerName || x.chat.contactPhone || 'Unknown number', preview: x.chat.preview, at: x.at,
        why: x.kind === 'closed' ? 'Closed by rule' : x.kind === 'not-customer' ? 'Not a customer' : x.kind === 'handled' ? 'Handled' : 'Snoozed',
        canPutBack: x.kind === 'handled' || x.kind === 'snoozed' || (x.kind === 'not-customer' && x.marked),
        next: x.kind === 'closed' ? explain(x.verdict.reason)
          : x.kind === 'not-customer' ? (x.marked ? `${x.why}. Never counted until put back` : `${x.why}: change it in Settings › Look and reading`)
          : x.kind === 'handled' ? 'Returns if they write again' : 'Returns when the snooze ends',
        until: x.kind === 'snoozed' && x.override.kind === 'snoozed' ? x.override.until : null,
      };
    }),
    setAsideTotal: asideRows.length,
    locations: rollup.entities.map((e) => ({
      name: e.name, waiting: e.awaiting, onTimePercent: e.onTimePercent, accounts: e.accountIds.length,
      tone: e.pastTarget ? 'late' : e.awaiting ? 'due' : 'ok',
    })),
    accounts: config.accounts.map((a) => ({
      id: a.id, name: a.name, channel: a.channel, location: a.location,
      waiting: CHANNELS[a.channel].reads && a.professional ? waitingNow(config, snapshots, judge, a.id).length : null,
      reads: CHANNELS[a.channel].reads,
      counted: a.professional,
      signedOut: ctx.signedOut.has(a.id),
      asleep: ctx.asleep.has(a.id),
    })),
    reads: ids.length > 0,
    detail: ctx.visible ? detailFor(config, snapshots, times, judge, ctx) : null,
    reports: ctx.route === 'reports' ? reportsFor(config, snapshots, times, judge, ctx, live) : null,
    digest: ctx.route === 'digest' ? digestFor(config, snapshots, times, judge, ctx, live) : null,
    modules: ctx.modules.map(readerHealth),
    customer: ctx.route === 'dock' && ctx.visible ? customerView(config, snapshots, ctx) : null,
    reviews: ctx.route === 'reviews' ? reviewsView(config, ctx) : null,
    // The screen shows what it is given: the rows, each size already in words, and the total.
    kept: ctx.kept ? { rows: keptRows(ctx.kept), sizes: keptRows(ctx.kept).map((r) => sizeText(r.bytes)), total: sizeText(keptTotal(ctx.kept)) } : null,
    assistant: {
      state: ctx.assistant ?? offState(config.settings.assistant.model),
      sentence: engineSentence(ctx.assistant ?? offState(config.settings.assistant.model)),
      suggested: suggestModel(ctx.memoryGB ?? 8), memoryGB: Math.round(ctx.memoryGB ?? 0), models: MODELS,
    },
    cloud: ctx.cloud ?? { phase: 'unavailable' },
    gate: {
      phase: ctx.gate?.phase ?? 'open',
      mayRun: ctx.gate ? mayRun(ctx.gate) : true,
      sentence: ctx.gate ? admissionSentence(ctx.gate, ctx.cloud?.phase === 'signed-in' ? ctx.cloud.email : null) : '',
    },
    workspace: ctx.workspace ?? { phase: 'signed-out' },
    owner: ctx.owner ?? { isOwner: false, workspaces: [] },
    update: ctx.update ?? { phase: 'none' },
    version: ctx.version ?? '',
    upgraded: ctx.upgraded ?? false,
    lostLogin: ctx.route === 'lost-login' && ctx.visible
      ? { since: signedOutSince(ctx.events ?? {}, ctx.visible), items: timelineRows(lostLoginTimeline(ctx.events ?? {}, ctx.visible, ctx.now), ctx.now) }
      : null,
    readerStory: ctx.route === 'reader'
      ? Object.fromEntries(ctx.modules.map((m) => [m.id, timelineRows(readerTimeline(ctx.events ?? {}, config.accounts.filter((a) => a.channel === m.id).map((a) => a.id), ctx.now), ctx.now)]))
      : {},
    settings: config.settings,
    openingHours: {
      locations: config.locations.map((l) => ({ name: l.name, accounts: config.accounts.filter((a) => a.location === l.name).length, hours: l.hours })),
      holidays: config.holidays,
    },
  };
}

/** The Reviews screen, for the location chosen in the title bar. Nothing here is guessed: a profile not read yet
 *  says so, and the counts are of the reviews Google showed, with whether it has more. */
function reviewsView(config: Config, ctx: Context): ReviewsView {
  const store = ctx.reviews ?? {};
  const accounts = config.accounts.filter((a) => a.channel === 'googlebusiness' && inScope(a.location, ctx.scope ?? null));
  const rows = (a: (typeof accounts)[number]) => (store[a.id]?.cards ?? []).map((c, i): ReviewRow => ({
    id: `${a.id}:${i}`, accountId: a.id, location: a.location || a.name, reviewer: c.reviewer, text: c.text, stars: c.stars, age: c.age, replied: c.replied,
  }));
  const profiles = accounts.map((a): ReviewProfileView => {
    const p = store[a.id];
    const cards = p?.cards ?? [];
    return {
      accountId: a.id, name: a.name, location: a.location || a.name, rating: p?.rating ?? null, total: p?.total ?? null,
      loaded: cards.length, more: p?.more ?? false, unanswered: cards.filter((c) => !c.replied).length, spread: spread(cards),
      readAt: p?.capturedAt || null, signedOut: ctx.signedOut.has(a.id),
    };
  });
  const all = accounts.flatMap(rows);
  const byAge = (r: ReviewRow) => ageMinutes(r.age) ?? Number.MAX_SAFE_INTEGER;
  const needing = needingReply(all.map((r) => ({ ...r }))) as ReviewRow[];
  return { profiles, needing, recent: [...all].sort((x, y) => byAge(x) - byAge(y)) };
}

/** The docked account's customers. Every line under "Seen by the app" is something a read saw: nothing is
 *  inferred, and a chat the app has only just met says so rather than showing an empty list. Only chats that are
 *  waiting or that the owner has written about are included: an account has hundreds of quiet ones.
 */
function customerView(config: Config, snapshots: Snapshots, ctx: Context): CustomerView | null {
  const account = config.accounts.find((a) => a.id === ctx.visible);
  if (!account) return null;
  const customers = ctx.customers ?? {};
  const day = (at: number) => new Date(at).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
  const time = (at: number) => new Date(at).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  const spell = durationText;
  const byKey: Record<string, CustomerCard> = {};
  for (const chat of snapshots[account.id]?.chats ?? []) {
    const record = customerFor(customers, account.id, chat.conversationKey);
    if (!record && !chat.awaiting) continue;
    const seen: { label: string; value: string }[] = [
      { label: 'Now', value: chat.awaiting ? `Waiting since ${time(chat.lastActivity)}` : 'Answered' },
    ];
    for (const a of [...(record?.answers ?? [])].reverse()) seen.push({ label: day(a.at), value: `Answered in ${spell(a.minutes)}` });
    if (!record?.answers.length) seen.push({ label: 'Replies', value: 'None measured yet on this chat' });
    if (record) {
      // The count only exists from the read that first saw them waiting; before that, say when we met them.
      seen.push({
        label: 'First seen',
        value: record.conversations
          ? `${day(record.firstSeen)}, ${record.conversations} time${record.conversations === 1 ? '' : 's'} on the line since`
          : day(record.firstSeen),
      });
    }
    byKey[chat.conversationKey] = { seen, note: record?.note ?? '', tags: record?.tags ?? [] };
  }
  return { accountId: account.id, byKey, suggestions: tagsInUse(customers) };
}

/** The time a line happened, said as a person would: "4:12 pm" today, "Tue, 4:12 pm" before that, "Now" for a line
 *  about the present. */
function timelineRows(items: TimelineItem[], now: number): TimelineRow[] {
  const today = new Date(now).toDateString();
  return items.map((i) => {
    if (i.at === null) return { ...i, at: 'Now' };
    const d = new Date(i.at);
    const time = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
    return { ...i, at: d.toDateString() === today ? time : `${d.toLocaleDateString(undefined, { weekday: 'short' })}, ${time}` };
  });
}

/** A reader that fails sometimes is not the same as one that never works, and neither is one never used. */
function readerHealth(m: ModuleHealth): ReaderHealth {
  if (m.lastError && m.ok === 0) {
    return { id: m.id, name: m.name, tone: 'late', status: 'Not reading', detail: `Nothing has been read. Last problem: ${m.lastError.replace(/\.$/, '')}.` };
  }
  if (m.lastError) {
    return { id: m.id, name: m.name, tone: 'due', status: 'Intermittent', detail: `${m.ok} good read${m.ok === 1 ? '' : 's'}, ${m.failed} failed. Last problem: ${m.lastError?.replace(/\.$/, '')}.` };
  }
  if (m.ok) return { id: m.id, name: m.name, tone: 'ok', status: 'Healthy', detail: `${m.ok} good read${m.ok === 1 ? '' : 's'} since the app started` };
  return { id: m.id, name: m.name, tone: 'neutral', status: 'Idle', detail: 'No read yet' };
}

/**
 * Customers waiting now: awaiting, and not older than the backlog line. Older ones are backlog, counted
 * separately by awaitingSplit with the same cutoff, so "waiting now" means the same thing on every screen.
 */
const waitingNow = (config: Config, snapshots: Snapshots, judge: Judge, accountId: string) => {
  const cutoff = judge.now - Math.max(1, config.settings.backlogAfterDays) * DAY_MS;
  return awaitingChats(snapshots, accountId, judge).filter((chat) => chat.lastActivity >= cutoff);
};

/** An account's reply target: its location's, or the app-wide one. */
export const targetFor = (config: Config, account: { location: string }) =>
  Math.max(1, config.locations.find((l) => l.name === account.location)?.slaMinutes ?? config.settings.slaMinutes);

const locationRules = (config: Config) =>
  Object.fromEntries(config.locations.map((l) => [l.name, { slaMinutes: l.slaMinutes, hours: hoursFor(config, l.name) }]));

function queueFor(config: Config, snapshots: Snapshots, judge: Judge, accountId: string, now: number): QueueRow[] {
  const account = config.accounts.find((a) => a.id === accountId);
  if (!account) return [];
  const target = targetFor(config, account);
  return waitingNow(config, snapshots, judge, account.id).map((chat) => {
    // The clock only runs inside the location's working hours, so a message at closing time is not late by morning.
    const waited = Math.round(elapsedBusinessMinutes(new Date(chat.lastActivity), new Date(now), hoursFor(config, account.location)));
    const remaining = target - waited;
    return {
      accountId: account.id, accountName: account.name, location: account.location, channel: account.channel,
      key: chat.conversationKey,
      customer: chat.customerName || chat.contactPhone || 'Unknown number',
      preview: chat.preview,
      waited,
      status: remaining < 0 ? 'Past target' : remaining <= DUE_SOON_MINUTES ? `Due in ${remaining} min` : 'On time',
      tone: (remaining < 0 ? 'late' : remaining <= DUE_SOON_MINUTES ? 'due' : 'ok') as Tone,
      fill: Math.min(100, (waited / (target * METER_SCALE)) * 100),
      target: 100 / METER_SCALE,
      targetMinutes: target,
      lastActivity: chat.lastActivity,
      open: isOpen(hoursFor(config, account.location), now),
    };
  });
}

function detailFor(config: Config, snapshots: Snapshots, times: ResponseTimes, judge: Judge, ctx: Context): AccountDetail | null {
  const account = config.accounts.find((a) => a.id === ctx.visible);
  if (!account) return null;
  const reads = CHANNELS[account.channel].reads;
  const snap = snapshots[account.id];
  const target = targetFor(config, account);
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

// ---- reports -------------------------------------------------------------------------------------------------

const RANGE_LABELS = { today: 'Today', week: 'Last 7 days', month: 'Last 30 days' } as const;

const shortDate = (at: number) => new Date(at).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
const dayDate = (key: string) => { const [y, m, d] = key.split('-').map(Number); return new Date(y, m - 1, d).getTime(); };

/** "4 points up", "6% fewer", or nothing to compare with. */
function change(now: number | null, before: number | null, unit: 'points' | 'percent', up: string, down: string): string | null {
  if (now === null || before === null) return null;
  if (unit === 'points') {
    const diff = now - before;
    return diff === 0 ? 'Same as the period before' : `${Math.abs(diff)} point${Math.abs(diff) === 1 ? '' : 's'} ${diff > 0 ? up : down}`;
  }
  if (before === 0) return null;
  const pct = Math.round(((now - before) / before) * 100);
  return pct === 0 ? 'Same as the period before' : `${Math.abs(pct)}% ${pct > 0 ? up : down}`;
}

function reportRange(key: keyof typeof RANGE_LABELS, report: Report, target: number, calls: CallRow[]): ReportRange {
  const unanswered = calls.filter((c) => c.returnedAt === null).length;
  const label = RANGE_LABELS[key];
  const { totals: t, previous: p } = report;
  const span = report.days.length;
  const hadBefore = p.replies > 0 || p.customersWrote > 0;

  const earlierReplies = report.repliesSince !== null && report.recordingSince !== null && dayKey(report.repliesSince) < dayKey(report.recordingSince);
  const coverage = report.recordingSince === null && report.repliesSince === null
    ? 'Nothing has been recorded yet. Reports fill in as the app reads.'
    : report.recordingSince !== null && report.daysRecorded < span
      ? `Customers, backlog and calls are recorded from ${shortDate(report.recordingSince)}, so they cover ${report.daysRecorded} of ${span} day${span === 1 ? '' : 's'}${earlierReplies ? `; reply times go back to ${shortDate(report.repliesSince!)}` : ''}.`
      : null;

  const located = report.byLocation.filter((l) => l.onTimePercent !== null);
  const lowest = located.length > 1 ? [...located].sort((a, b) => (a.onTimePercent ?? 0) - (b.onTimePercent ?? 0))[0] : null;
  const headline = !report.hasData ? `${label}: nothing recorded yet`
    : t.onTimePercent !== null ? `${label}: ${t.onTimePercent}% answered on time`
      : `${label}: ${t.customersWrote} customer${t.customersWrote === 1 ? '' : 's'} wrote, no replies measured yet`;
  const summary = !report.hasData ? 'Figures appear here as the app reads each account.'
    : [
      t.medianMinutes !== null ? `Median first reply ${Math.round(t.medianMinutes)} minutes across ${t.replies} repl${t.replies === 1 ? 'y' : 'ies'}.` : '',
      lowest ? `Lowest: ${lowest.name} at ${lowest.onTimePercent}%.` : '',
      hadBefore ? change(t.onTimePercent, p.onTimePercent, 'points', 'up on the period before', 'down on the period before') + '.' : '',
    ].filter((s) => s && s !== 'null.').join(' ');

  const onTimeTone: Tone = t.onTimePercent === null ? 'neutral' : t.onTimePercent >= 90 ? 'ok' : t.onTimePercent >= 80 ? 'due' : 'late';
  const facts: Figure[] = [
    { label: 'Answered on time', value: t.onTimePercent === null ? '—' : String(t.onTimePercent), unit: '%', tone: onTimeTone,
      note: (hadBefore && change(t.onTimePercent, p.onTimePercent, 'points', 'up', 'down')) || `Within the ${target}-minute target` },
    { label: 'Median first reply', value: t.medianMinutes === null ? '—' : String(Math.round(t.medianMinutes)), unit: 'min',
      tone: t.medianMinutes === null ? 'neutral' : t.medianMinutes <= target ? 'ok' : 'late',
      note: t.replies ? `${t.replies} repl${t.replies === 1 ? 'y' : 'ies'} measured` : 'No replies measured yet' },
    { label: 'Customers who wrote', value: t.customersWrote.toLocaleString('en-GB'), unit: '', tone: 'neutral',
      note: (hadBefore && change(t.customersWrote, p.customersWrote, 'percent', 'more', 'fewer')) || 'Each counted once a day',
      trend: span > 1 ? report.days.map((d) => d.customersWrote) : undefined },
    { label: 'Waiting over a day', value: t.waitingOverADay === null ? '—' : String(t.waitingOverADay), unit: '',
      tone: t.waitingOverADay ? 'late' : 'neutral', note: 'At the latest morning read' },
    { label: 'Reopened', value: String(t.reopened), unit: '', tone: 'neutral', note: 'Waiting again after a reply',
      trend: span > 1 ? report.days.map((d) => d.reopened) : undefined },
    { label: 'Missed calls', value: String(calls.length), unit: '', tone: unanswered ? 'late' : 'neutral',
      note: !calls.length ? 'None in this range' : unanswered ? `${unanswered} not returned` : 'All returned' },
  ];

  const dayLabels = report.days.map((d, i) => {
    const at = dayDate(d.day);
    if (span <= 7) return new Date(at).toLocaleDateString('en-GB', { weekday: 'short' });
    return i % 7 === (span - 1) % 7 ? shortDate(at) : '';
  });
  return { label, report, headline, summary, coverage, facts, dayLabels };
}

function reportsFor(config: Config, snapshots: Snapshots, times: ResponseTimes, judge: Judge, ctx: Context, live: string[]): ReportsView {
  const { now } = ctx;
  const scope = ctx.scope ?? null;
  const accounts = reportAccounts(config, scope);
  const history = ctx.history ?? {};
  const name = (id: string) => config.accounts.find((a) => a.id === id)?.name ?? id;
  const who = (c: { customerName: string; contactPhone: string }) => c.customerName || c.contactPhone || 'Unknown number';
  const here = live.filter((id) => inScope(config.accounts.find((a) => a.id === id)?.location ?? '', scope));

  const waiting = here.flatMap((id) => awaitingChats(snapshots, id, judge).map((chat) => ({ id, chat })));
  const callsBetween = (from: number, to: number) => callRows(config, snapshots, ctx.calls ?? {}, here, from, to, judge);
  const unansweredCalls = callsBetween(now - 31 * DAY_MS, now + 1).filter((c) => c.returnedAt === null);
  const callsFor = (days: number, end = now) => callsBetween(startOfDay(end, days - 1), startOfDay(end, -1));
  const backlog = waiting
    .filter(({ chat }) => chat.lastActivity < now - DAY_MS)
    .sort((a, b) => a.chat.lastActivity - b.chat.lastActivity)
    .slice(0, 20)
    .map(({ id, chat }) => {
      const account = config.accounts.find((a) => a.id === id);
      const hours = hoursFor(config, account?.location ?? '');
      return {
        accountId: id, accountName: name(id), key: chat.conversationKey, customer: who(chat), preview: chat.preview, since: chat.lastActivity,
        waited: Math.round(elapsedBusinessMinutes(new Date(chat.lastActivity), new Date(now), hours)),
      };
    });

  const target = config.settings.slaMinutes;
  const range = (key: keyof typeof RANGE_LABELS, days: number) =>
    reportRange(key, buildReport(history, times, accounts, days, now), target, callsFor(days));
  const week = (which: 'this' | 'last') =>
    weeklyDoc(which, buildReport(history, times, accounts, 7, weekEnding(now, which)), target, callsFor(7, weekEnding(now, which)));
  return {
    ranges: { today: range('today', 1), week: range('week', 7), month: range('month', 30) },
    weekly: { this: week('this'), last: week('last') },
    scope,
    targetMinutes: target, backlog, unansweredCalls,
    calls: { today: callsFor(1), week: callsFor(7), month: callsFor(30) },
  };
}

/** Whether an account's location is inside the chosen scope. Accounts with no location sit under "No location". */
export const inScope = (location: string, scope: string | null) => !scope || (location || 'No location') === scope;

/** The accounts a report covers, with each one's target. Shared by the screen and the exports. */
export const reportAccounts = (config: Config, scope: string | null = null) =>
  readableAccounts(config).filter((a) => inScope(a.location, scope))
    .map((a) => ({ id: a.id, name: a.name, location: a.location, targetMinutes: targetFor(config, a) }));

const pointsOn = (diff: number, when: string) =>
  diff === 0 ? `the same as ${when}` : `${Math.abs(diff)} point${Math.abs(diff) === 1 ? '' : 's'} ${diff > 0 ? 'up' : 'down'} on ${when}`;
const pointsOnWeekBefore = (diff: number) => pointsOn(diff, 'the week before');

const plainCount =(n: number, one: string, many = `${one}s`) => `${n.toLocaleString('en-GB')} ${n === 1 ? one : many}`;

function weeklyDoc(which: 'this' | 'last', report: Report, target: number, calls: CallRow[]): WeeklyDoc {
  const base = reportRange('week', report, target, calls);
  const notReturned = calls.filter((c) => c.returnedAt === null).length;
  const { totals: t, previous: p } = report;
  const first = dayDate(report.days[0].day), last = dayDate(report.days.at(-1)!.day);
  const sameMonth = new Date(first).getMonth() === new Date(last).getMonth();
  const from = new Date(first).toLocaleDateString('en-GB', sameMonth ? { day: 'numeric' } : { day: 'numeric', month: 'long' });
  const to = new Date(last).toLocaleDateString('en-GB', { day: 'numeric', month: 'long' });
  const title = `${which === 'this' ? 'This week so far' : 'Week of'}${which === 'this' ? ':' : ''} ${from} to ${to}`;

  const located = report.byLocation.filter((l) => l.onTimePercent !== null).sort((a, b) => (b.onTimePercent ?? 0) - (a.onTimePercent ?? 0));
  const hadBefore = p.replies > 0;
  const locationsWithAccounts = report.byLocation.length;

  const lede = !report.hasData ? 'Nothing was recorded for this week.'
    : [
      `${plainCount(t.customersWrote, 'customer')} wrote to ${plainCount(locationsWithAccounts, 'location')}.`,
      t.onTimePercent === null ? 'No first replies were measured.'
        : `${t.onTimePercent}% got a first reply within target${hadBefore && p.onTimePercent !== null ? `, ${pointsOnWeekBefore(t.onTimePercent - p.onTimePercent)}` : ''}.`,
      located.length > 1 ? `${located[0].name} answered ${located[0].onTimePercent}% on time; ${located.at(-1)!.name} ${located.at(-1)!.onTimePercent}%.` : '',
    ].filter(Boolean).join(' ');

  const lookAt: string[] = [];
  const weakest = located.at(-1);
  if (weakest && (weakest.onTimePercent ?? 100) < 90) lookAt.push(`${weakest.name} answered ${weakest.onTimePercent}% on time across ${plainCount(weakest.replies, 'reply', 'replies')}.`);
  const slowest = [...report.byAccount].filter((a) => a.p90Minutes !== null).sort((a, b) => (b.p90Minutes ?? 0) - (a.p90Minutes ?? 0))[0];
  if (slowest && (slowest.p90Minutes ?? 0) > 2 * target) lookAt.push(`At ${slowest.name}, the slowest one in ten first replies took ${Math.round(slowest.p90Minutes ?? 0)} minutes or more.`);
  if (t.waitingOverADay) lookAt.push(`${plainCount(t.waitingOverADay, 'customer')} had waited more than a day at the latest morning read.`);
  if (calls.length) lookAt.push(`${plainCount(calls.length, 'missed call')}, ${notReturned ? `${notReturned} not returned` : 'all returned'}.`);

  const wentWell: string[] = [];
  for (const l of located) if ((l.onTimePercent ?? 0) >= 90) wentWell.push(`${l.name} answered ${l.onTimePercent}% on time.`);
  if (hadBefore && t.onTimePercent !== null && p.onTimePercent !== null && t.onTimePercent > p.onTimePercent) wentWell.push(`On time rose from ${p.onTimePercent}% to ${t.onTimePercent}%.`);
  if (t.waitingOverADay !== null && p.waitingOverADay !== null && t.waitingOverADay < p.waitingOverADay) wentWell.push(`Fewer customers waited more than a day: ${t.waitingOverADay}, down from ${p.waitingOverADay}.`);

  const keep = ['Answered on time', 'Median first reply', 'Waiting over a day', 'Missed calls'];
  return {
    which, title, hasData: report.hasData, coverage: base.coverage, lede, report,
    // A report about a week says what happened that week, not what is true this minute.
    facts: base.facts.filter((f) => keep.includes(f.label)).map(({ trend: _t, ...f }) =>
      f.label === 'Missed calls' ? { ...f, tone: 'neutral' as Tone, note: 'Recorded this week' } : f),
    dayLabels: report.days.map((d) => new Date(dayDate(d.day)).toLocaleDateString('en-GB', { weekday: 'short' })),
    lookAt: report.hasData ? (lookAt.length ? lookAt : ['Nothing stood out.']) : [],
    wentWell: report.hasData ? (wentWell.length ? wentWell : [hadBefore ? 'Nothing improved on the week before.' : 'This is the first week recorded, so there is nothing to compare with yet.']) : [],
  };
}

// ---- the morning digest --------------------------------------------------------------------------------------

function digestFor(config: Config, snapshots: Snapshots, times: ResponseTimes, judge: Judge, ctx: Context, live: string[]): DigestView {
  const { now } = ctx;
  const split = morningSplit(config, snapshots, judge, live, now);
  const callsNotReturned = callRows(config, snapshots, ctx.calls ?? {}, live, now - 2 * DAY_MS, now + 1, judge).filter((c) => c.returnedAt === null).length;
  const accounts = reportAccounts(config);
  const history = ctx.history ?? {};
  const yesterdayNoon = startOfDay(now, 1) + 12 * 3_600_000;
  const yesterday = buildReport(history, times, accounts, 1, yesterdayNoon);
  const fortnight = buildReport(history, times, accounts, 14, yesterdayNoon);
  const name = (id: string) => config.accounts.find((a) => a.id === id)?.name ?? id;

  const hour = new Date(now).getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';
  const whenClosed = split.byHours ? 'while you were closed' : 'since midnight';
  const owed = split.owed.length;

  const title = !split.hasData ? `${greeting}. Nothing has been read yet.`
    : split.overnight ? `${greeting}. ${plainCount(split.overnight, 'customer')} wrote ${whenClosed}.`
      : owed ? `${greeting}. ${plainCount(owed, 'customer')} ${owed === 1 ? 'is' : 'are'} still owed a reply.`
        : `${greeting}. Nobody is waiting.`;

  const y = yesterday.totals, before = yesterday.previous;
  const summary = [
    owed ? `Answer the ${owed} still owed from ${split.byHours ? 'before closing' : 'yesterday'} first.` : '',
    callsNotReturned ? `${plainCount(callsNotReturned, 'missed call')} from the last two days ${callsNotReturned === 1 ? 'has' : 'have'} not been returned.` : '',
    y.onTimePercent !== null
      ? `Yesterday ${y.onTimePercent}% were answered on time${before.onTimePercent !== null ? `, ${pointsOn(y.onTimePercent - before.onTimePercent, 'the day before')}` : ''}.`
      : yesterday.recordingSince !== null ? 'No first replies were measured yesterday.' : '',
  ].filter(Boolean).join(' ') || 'The line fills in as the accounts are read.';

  return {
    hasData: split.hasData, title, summary,
    owed: split.owed.slice(0, 12).map(({ account, chat }) => ({
      accountId: account, accountName: name(account), key: chat.conversationKey,
      customer: chat.customerName || chat.contactPhone || 'Unknown number', preview: chat.preview, since: chat.lastActivity,
    })),
    owedTotal: owed,
    callsNotReturned,
    owedLabel: split.byHours ? 'Wrote before closing and never got a reply' : 'Wrote before midnight and never got a reply',
    overnight: split.overnight,
    overnightNote: split.byHours ? 'The wait clock starts at opening time.' : 'Opening hours are off, so these waits count from when they wrote.',
    yesterday: yesterday.byLocation.map((l) => ({
      name: l.name, replies: l.replies, onTimePercent: l.onTimePercent, medianMinutes: l.medianMinutes,
      trend: (fortnight.byLocation.find((f) => f.name === l.name)?.daily ?? []).filter((v): v is number => v !== null),
    })),
  };
}

/** Missed calls on these accounts between two times, newest first, named from the snapshot (the store keeps no names). */
/** Missed calls in a range. A call from someone who is not a customer — staff, the team's own number — is not a
 *  missed customer call, and is left out by the same rule as the line. */
function callRows(config: Config, snapshots: Snapshots, calls: Calls, accounts: string[], from: number, to: number, judge: Judge): CallRow[] {
  return callsIn(calls, accounts, from, to).flatMap((c) => {
    const account = config.accounts.find((a) => a.id === c.account);
    const chat = snapshots[c.account]?.chats.find((x) => x.conversationKey === c.key);
    if (chat && notCustomerWhy(c.account, chat, judge)) return [];
    return [{
      accountId: c.account, accountName: account?.name ?? c.account, location: account?.location ?? '', key: c.key,
      customer: chat?.customerName || chat?.contactPhone || 'Unknown caller', at: c.at, returnedAt: c.returnedAt, returnedBy: c.returnedBy,
    }];
  });
}
