// Brings a v5 install across: its parsed instances.json and settings.json become one v6 config, plus a report
// of what came over, what was guessed and what was left behind. The v5 files are only read, never written, and
// the app keeps running until v6 reaches parity.
import { CHANNELS, parseConfig, type ChannelId, type Config } from './config.ts';

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
