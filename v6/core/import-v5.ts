// Brings a v5 install across: its parsed instances.json and settings.json become one v6 config, plus a report
// of what came over, what was guessed and what was left behind. The v5 files are only read, never written, and
// the app keeps running until v6 reaches parity.
import { isNonCustomerConversation, sanitizePreview, type ChatEntry } from './chat-entry.ts';
import { CHANNELS, parseConfig, type ChannelId, type Config } from './config.ts';
import { pruneExpired, type Overrides } from './awaiting-overrides.ts';
import { emptyResponseTimes, pruneResponseTimes, type ResponseTimes } from './response-times.ts';
import { distrustColdScan, type Snapshots } from './snapshot.ts';

export interface ImportReport {
  accounts: number;
  /** v5 archived accounts, deliberately left behind. */
  archived: number;
  /** Rows that could not be read, named as best they could be. */
  skipped: string[];
  locations: string[];
  /** Accounts whose location was guessed from their name rather than set. */
  guessedLocations: string[];
  /** Anything the owner should know rather than discover. */
  notes: string[];
}

/** v5 platform id → v6 channel. Anything unrecognised becomes a plain custom URL rather than a guess. */
const CHANNEL_BY_PLATFORM: Record<string, ChannelId> = {
  whatsapp: 'whatsapp', whatsappbusiness: 'whatsappbusiness', instagram: 'instagram', telegram: 'telegram',
  messenger: 'messenger', metabusinesssuite: 'metabusinesssuite', googlebusiness: 'googlebusiness',
  discord: 'discord', generic: 'custom',
};

export function importV5(instances: unknown, settings: unknown): { config: Config; report: ImportReport } {
  const report: ImportReport = { accounts: 0, archived: 0, skipped: [], locations: [], guessedLocations: [], notes: [] };
  const rows = instanceRows(instances, 'instances');
  report.archived = instanceRows(instances, 'archivedInstances').length;
  if (report.archived) report.notes.push(`${report.archived} archived account(s) were left behind.`);

  const accounts = [];
  for (const row of rows) {
    if (!isObject(row) || !str(row.id)) { report.skipped.push(isObject(row) ? str(row.displayName) || '(no id)' : '(unreadable row)'); continue; }
    const channel = CHANNEL_BY_PLATFORM[str(row.platform).toLowerCase()] ?? 'custom';
    const professional = isProfessional(row.category);
    // Only professional accounts are grouped: a location on a personal account would be noise on every screen.
    const set = str(row.branchKey);
    const guessed = professional && !set ? guessLocation(str(row.displayName)) : '';
    if (guessed) report.guessedLocations.push(`${str(row.displayName)} → ${guessed}`);
    accounts.push({
      id: str(row.id), name: str(row.displayName), channel, url: str(row.startUrl) || CHANNELS[channel].url,
      location: professional ? set || guessed : '', professional, muted: row.notificationsMuted === true,
      sortOrder: typeof row.sortOrder === 'number' ? row.sortOrder : 0, notes: str(row.notes),
    });
  }

  const s = isObject(settings) ? settings : {};
  const locations = importLocations(s.workspaceProfiles, accounts.map((a) => a.location), report);
  const { config, dropped } = parseConfig({ accounts, locations, settings: importSettings(s, report) });
  report.accounts = config.accounts.length;
  if (dropped) report.skipped.push(`${dropped} account(s) were unreadable or repeated`);
  report.locations = config.locations.map((l) => l.name);
  return { config, report };
}

// ---- history ------------------------------------------------------------------------------------
// The three files worth carrying: what was on screen, how fast replies went out, and which chats the owner
// had already dealt with. The last one matters most — without it, work they closed comes back as waiting.

export interface HistoryReport {
  chats: number;
  samples: number;
  pending: number;
  handled: number;
  snoozed: number;
  /** History belonging to accounts that did not come across. Restoring it against nothing would be a lie. */
  orphaned: string[];
  /** Rows that could not be read. Counted, never silently dropped. */
  dropped: number;
  notes: string[];
}

export interface V5History { snapshot?: unknown; responseTimes?: unknown; overrides?: unknown }

export function importV5History(accountIds: string[], files: V5History, now = Date.now()):
{ snapshots: Snapshots; times: ResponseTimes; overrides: Overrides; report: HistoryReport } {
  const known = new Map(accountIds.map((id) => [id.trim().toLowerCase(), id.trim()]));
  const report: HistoryReport = { chats: 0, samples: 0, pending: 0, handled: 0, snoozed: 0, orphaned: [], dropped: 0, notes: [] };
  const snapshots: Snapshots = {};
  const times = emptyResponseTimes();
  const overrides: Overrides = {};
  const orphaned = new Set<string>();
  const resolve = (id: string) => {
    const hit = known.get(id.trim().toLowerCase());
    if (!hit) orphaned.add(id);
    return hit;
  };

  for (const [id, dto] of instancesOf(files.snapshot)) {
    const account = resolve(id);
    if (!account || !isObject(dto)) continue;
    const capturedAt = ms(dto.capturedAtUtc);
    if (capturedAt === null) { report.dropped++; continue; }
    const chats: ChatEntry[] = [];
    for (const row of Array.isArray(dto.chats) ? dto.chats : []) {
      const chat = chatFromV5(row);
      if (!chat) { report.dropped++; continue; }
      // The same two guards v5 applied on its own load: groups and notice accounts are not customers, and a
      // raw base64 payload is not a message preview.
      if (isNonCustomerConversation(chat.conversationKey)) continue;
      chats.push(chat);
    }
    // A snapshot taken seconds after a reload claims almost every chat has no message; honouring that would
    // close the whole queue on the first launch after the move.
    snapshots[account] = { capturedAt, chats: distrustColdScan(chats) };
    report.chats += chats.length;
  }

  for (const [id, dto] of instancesOf(files.responseTimes)) {
    const account = resolve(id);
    if (!account || !isObject(dto)) continue;
    const samples = [];
    for (const row of Array.isArray(dto.samples) ? dto.samples : []) {
      const answeredAt = isObject(row) ? ms(row.answeredAtUtc) : null;
      const minutes = isObject(row) && typeof row.frtMinutes === 'number' ? row.frtMinutes : null;
      if (answeredAt === null || minutes === null || !(minutes > 0)) { report.dropped++; continue; }
      samples.push({ answeredAt, minutes });
    }
    if (samples.length) times.samples[account] = samples;
    const watchStart = ms(dto.watchStartUtc);
    // Without the watch start, every waiting chat older than the move would be measured from zero and the
    // first reply would look like a days-long response.
    if (watchStart !== null) times.watchStart[account] = watchStart;
    const pending: Record<string, number> = {};
    for (const [chat, at] of Object.entries(isObject(dto.pending) ? dto.pending : {})) {
      const inbound = ms(at);
      if (!chat.trim() || inbound === null) { report.dropped++; continue; }
      pending[chat] = inbound;
    }
    if (Object.keys(pending).length) { times.pending[account] = pending; report.pending += Object.keys(pending).length; }
  }
  pruneResponseTimes(times, now);
  report.samples = Object.values(times.samples).reduce((n, list) => n + list.length, 0);

  let snoozed = 0;
  for (const [id, chats] of instancesOf(files.overrides)) {
    const account = resolve(id);
    if (!account || !isObject(chats)) continue;
    for (const [chat, dto] of Object.entries(chats)) {
      if (!chat.trim() || !isObject(dto)) { report.dropped++; continue; }
      const kind = overrideKind(dto.kind);
      const at = ms(kind === 'handled' ? dto.handledForActivityUtc : dto.snoozeUntilUtc);
      if (!kind || at === null) { report.dropped++; continue; }
      (overrides[account] ??= {})[chat] = kind === 'handled' ? { kind, activity: at } : { kind, until: at };
      if (kind === 'handled') report.handled++; else snoozed++;
    }
  }
  // A snooze that ran out while the owner was moving has done its job; it does not come back.
  pruneExpired(overrides, now);
  report.snoozed = Object.values(overrides).reduce((n, chats) => n + Object.values(chats).filter((o) => o.kind === 'snoozed').length, 0);
  if (snoozed > report.snoozed) report.notes.push(`${snoozed - report.snoozed} snooze(s) had already elapsed and were not restored.`);

  report.orphaned = [...orphaned];
  if (report.orphaned.length) report.notes.push(`History for ${report.orphaned.length} account(s) that did not come across was left behind.`);
  report.notes.push('v5 message analytics and contact history were not imported: v6 has no surface for them yet.');
  return { snapshots, times, overrides, report };
}

/** v5's stored chat row. Its key names differ from the live scan JSON, so it gets its own mapper. */
function chatFromV5(row: unknown): ChatEntry | null {
  if (!isObject(row)) return null;
  const lastActivity = ms(row.lastActivityUtc);
  if (lastActivity === null) return null;
  return {
    conversationKey: str(row.conversationKey),
    customerName: str(row.customerName),
    unread: typeof row.unread === 'number' && Number.isInteger(row.unread) ? row.unread : 0,
    lastActivity,
    preview: sanitizePreview(str(row.preview)),
    awaiting: row.isAwaiting === true,
    lastMessageFromMe: row.lastMessageFromMe === true,
    contactPhone: str(row.contactPhone),
    // Null is "this build did not record it", which is not the same as "there is no message".
    hasLastMessage: typeof row.hasLastMessage === 'boolean' ? row.hasLastMessage : null,
    lastMessageType: str(row.lastMessageType),
    lastCallOutcome: str(row.lastCallOutcome),
  };
}

/** v5 wrote the enum as a name; an older file may hold the number. */
function overrideKind(v: unknown): 'handled' | 'snoozed' | null {
  const name = typeof v === 'string' ? v.trim().toLowerCase() : v === 0 ? 'handled' : v === 1 ? 'snoozed' : '';
  return name === 'handled' || name === 'snoozed' ? name : null;
}

const instancesOf = (raw: unknown): [string, unknown][] => {
  const instances = isObject(raw) ? raw.instances : undefined;
  return isObject(instances) ? Object.entries(instances) : [];
};

/** v5 wrote times as ISO strings; a hand-edited file may hold epoch milliseconds. */
function ms(v: unknown): number | null {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v !== 'string') return null;
  const at = Date.parse(v);
  return Number.isNaN(at) ? null : at;
}

// ---- config -------------------------------------------------------------------------------------

function importLocations(profiles: unknown, used: string[], report: ImportReport) {
  const locations: { name: string; slaMinutes?: unknown; hours?: unknown }[] = [];
  const named = new Set<string>();
  for (const p of Array.isArray(profiles) ? profiles : []) {
    if (!isObject(p)) continue;
    // Accounts point at the location KEY, so that is the name v6 keeps; a different display name is reported
    // rather than silently chosen, because picking it would orphan every account pointing at the key.
    const name = str(p.locationKey);
    if (!name || named.has(name.toLowerCase())) continue;
    named.add(name.toLowerCase());
    const shown = str(p.displayName);
    if (shown && shown.toLowerCase() !== name.toLowerCase()) report.notes.push(`Location "${name}" was shown as "${shown}" in v5; rename it if you prefer that.`);
    locations.push({ name, slaMinutes: p.slaThresholdMinutes, hours: p.hours });
  }
  // A location an account names but v5 had no profile for still has to exist, or the account loses its group.
  for (const name of used) {
    if (!name || named.has(name.toLowerCase())) continue;
    named.add(name.toLowerCase());
    locations.push({ name });
  }
  return locations;
}

function importSettings(s: Record<string, unknown>, report: ImportReport) {
  const idle = typeof s.idleSessionReapMinutes === 'number' ? s.idleSessionReapMinutes : 20;
  report.notes.push('Every account stays awake in v6 — v5\'s startup warm and lazy-loading settings were not imported. Turn on sleeping in Settings if you want it.');
  report.notes.push('Reading runs on a fixed schedule in v6, so v5\'s adaptive poll interval has no equivalent.');
  report.notes.push('v5 settings with no v6 equivalent yet (notifications, backfill, dashboard layout) were left behind.');
  if (s.enableLocalAi === true) report.notes.push(`The assistant was on in v5 with model "${str(s.localAiModelName) || 'phi3:mini'}"; make sure that model is installed, or pick another in Settings.`);
  return {
    slaMinutes: s.slaThresholdMinutes,
    backlogAfterDays: s.awaitingBacklogAfterDays,
    filterClosedConversations: s.filterClosedConversations,
    sleepUnusedAccounts: false,
    sleepAfterMinutes: idle,
    assistant: { enabled: s.enableLocalAi, model: str(s.localAiModelName), endpoint: str(s.ollamaEndpoint) },
    theme: str(s.themePreference).toLowerCase(),
    quietHours: { enabled: s.quietHoursEnabled, startHour: s.quietHoursStartHour, endHour: s.quietHoursEndHour },
  };
}

/** v5 wrote `{ version, instances, archivedInstances }`, but a hand-edited or older file can be a bare array. */
function instanceRows(raw: unknown, key: string): unknown[] {
  if (Array.isArray(raw)) return key === 'instances' ? raw : [];
  const value = isObject(raw) ? raw[key] : undefined;
  return Array.isArray(value) ? value : [];
}

/** v5's own rule, and specific to this owner's branches — which is why it lives in the importer and not in the
 *  app. It only ever suggests: a name with no branch token leaves the account standing alone. */
function guessLocation(displayName: string): string {
  const match = /\b(DHA-?\s*\d+|Men\s+DHA-?\s*\d+|F-?\s*\d+|IgnitePro|Eli-?\s*\d+)\b/i.exec(displayName);
  return match ? match[1].trim().replace(/\s+/g, '-') : '';
}

const isObject = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);
const str = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
// v5 wrote the enum as a name, but an older file may hold the number.
const isProfessional = (v: unknown) => (typeof v === 'string' ? v.trim().toLowerCase() === 'professional' : v === 1);
