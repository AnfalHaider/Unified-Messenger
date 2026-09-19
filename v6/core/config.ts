// All of v6's configuration in one object: accounts, locations, settings. The app layer saves it as a single
// JSON file, and it is the only thing Firebase ever syncs — never oversight data.
//
// Parsing is deliberately forgiving in the same way the chat parser is: this file can be hand-edited, restored
// from a backup, or written by an older build, and none of that may stop the app opening. A field of the wrong
// type falls back to its default and a broken account is dropped and counted, never thrown.
import type { BusinessHours } from './business-hours.ts';

export type ChannelId = 'whatsapp' | 'whatsappbusiness' | 'instagram' | 'telegram' | 'messenger'
  | 'metabusinesssuite' | 'googlebusiness' | 'discord' | 'custom';

/** `reads` is whether this channel has an oversight reader today. A channel that cannot be measured must say so
 *  on screen rather than show a flattering number, so every surface takes the answer from here. */
export const CHANNELS: Record<ChannelId, { name: string; url: string; reads: boolean }> = {
  whatsapp: { name: 'WhatsApp', url: 'https://web.whatsapp.com/', reads: true },
  whatsappbusiness: { name: 'WhatsApp Business', url: 'https://web.whatsapp.com/', reads: true },
  instagram: { name: 'Instagram', url: 'https://www.instagram.com/', reads: true },
  telegram: { name: 'Telegram', url: 'https://web.telegram.org/', reads: false },
  messenger: { name: 'Messenger', url: 'https://www.messenger.com/', reads: false },
  metabusinesssuite: { name: 'Meta Business Suite', url: 'https://business.facebook.com/', reads: false },
  googlebusiness: { name: 'Google Business', url: 'https://business.google.com/reviews', reads: false },
  discord: { name: 'Discord', url: 'https://discord.com/app', reads: false },
  custom: { name: 'Custom URL', url: '', reads: false },
};

export interface Account {
  id: string;
  name: string;
  channel: ChannelId;
  /** Start page: the channel's own unless the owner set another. */
  url: string;
  /** Location name; blank means the account stands alone. */
  location: string;
  /** Only professional accounts are read for oversight. Personal ones are just a signed-in window. */
  professional: boolean;
  muted: boolean;
  sortOrder: number;
  notes: string;
}

export interface Location {
  name: string;
  /** Reply target in minutes; null uses the global one. */
  slaMinutes: number | null;
  hours: BusinessHours | null;
}

export interface Settings {
  /** Reply target in minutes. */
  slaMinutes: number;
  /** A waiting chat older than this is backlog, reported separately rather than hidden. */
  backlogAfterDays: number;
  /** Off restores the raw waiting number, closers included. */
  filterClosedConversations: boolean;
  /** Owner decision: every account stays awake unless this is turned on. */
  sleepUnusedAccounts: boolean;
  sleepAfterMinutes: number;
  readEverySeconds: number;
  /** Closing the window keeps the app reading from the tray. Off, closing quits. */
  closeToBackground: boolean;
  /** The local assistant is off by default, so a low-end PC never pays for it. */
  assistant: { enabled: boolean; model: string; endpoint: string };
  theme: 'system' | 'light' | 'dark';
  quietHours: { enabled: boolean; startHour: number; endHour: number };
  /** Windows notifications, each switchable. Quiet hours hold all of them back. */
  alerts: { nearTarget: boolean; waitedHour: boolean; signedOut: boolean; callNotReturned: boolean };
  /** What the weekly report includes, and whether last week's PDF is saved on Monday morning. Names are off by
   *  default so the report can go to anyone; saving files unasked is off by default too. */
  weeklyReport: { autoSave: boolean; include: WeeklyInclude };
  /** The morning digest opens on the first opening of each day. */
  morningDigest: boolean;
  /** Sentences the owner keeps to hand, to copy into a chat themselves. The app never sends one. */
  savedReplies: SavedReply[];
  /** Chats that are never counted: a word in the name ("Staff") or one of the team's own numbers (digits only). */
  notCustomers: { words: string[]; numbers: string[] };
}

export interface WeeklyInclude { figures: boolean; locations: boolean; accounts: boolean; calls: boolean; names: boolean }

export interface SavedReply { title: string; body: string }
export const SAVED_REPLY_MAX = 40, SAVED_REPLY_TITLE_MAX = 40, SAVED_REPLY_BODY_MAX = 1200;

export interface Config { version: number; accounts: Account[]; locations: Location[]; holidays: Holiday[]; settings: Settings }

export const CONFIG_VERSION = 1;

export const defaultSettings = (): Settings => ({
  slaMinutes: 15,
  backlogAfterDays: 7,
  filterClosedConversations: true,
  sleepUnusedAccounts: false,
  closeToBackground: true,
  sleepAfterMinutes: 20,
  readEverySeconds: 60,
  assistant: { enabled: false, model: 'gemma3:4b', endpoint: 'http://127.0.0.1:11434/' },
  theme: 'system',
  quietHours: { enabled: false, startHour: 21, endHour: 8 },
  alerts: { nearTarget: true, waitedHour: true, signedOut: true, callNotReturned: true },
  weeklyReport: { autoSave: false, include: { figures: true, locations: true, accounts: true, calls: true, names: false } },
  morningDigest: true,
  savedReplies: [],
  notCustomers: { words: [], numbers: [] },
});

export const emptyConfig = (): Config => ({ version: CONFIG_VERSION, accounts: [], locations: [], holidays: [], settings: defaultSettings() });

export const SLA_MIN_MINUTES = 5, SLA_MAX_MINUTES = 120;

/** Never throws. `dropped` counts accounts that could not be read; the caller logs it rather than losing it silently. */
export function parseConfig(raw: unknown): { config: Config; dropped: number } {
  const root = isObject(raw) ? raw : {};
  const config = emptyConfig();
  let dropped = 0;

  const seen = new Set<string>();
  for (const row of Array.isArray(root.accounts) ? root.accounts : []) {
    const account = parseAccount(row);
    // A blank or repeated id would collide with another account's session and its stored numbers.
    if (!account || seen.has(account.id.toLowerCase())) { dropped++; continue; }
    seen.add(account.id.toLowerCase());
    config.accounts.push(account);
  }

  const named = new Set<string>();
  for (const row of Array.isArray(root.locations) ? root.locations : []) {
    const location = parseLocation(row);
    if (!location || named.has(location.name.toLowerCase())) continue;
    named.add(location.name.toLowerCase());
    config.locations.push(location);
  }

  // Two accounts that spell the same location differently would otherwise become two rows on every screen:
  // the rollup groups by the name as written, so the location list decides the spelling.
  const canonical = new Map(config.locations.map((l) => [l.name.toLowerCase(), l.name]));
  for (const account of config.accounts) {
    const match = canonical.get(account.location.toLowerCase());
    if (match) account.location = match;
  }

  config.holidays = parseHolidays(root.holidays, config.locations);
  config.settings = parseSettings(root.settings);
  return { config, dropped };
}

function parseAccount(raw: unknown): Account | null {
  if (!isObject(raw)) return null;
  const id = str(raw.id);
  if (!id) return null;
  const channel = isChannel(str(raw.channel)) ? (str(raw.channel) as ChannelId) : 'custom';
  return {
    id,
    name: str(raw.name) || CHANNELS[channel].name,
    channel,
    url: str(raw.url) || CHANNELS[channel].url,
    location: str(raw.location),
    // Defaults to personal: an account is read for oversight only when it was deliberately marked.
    professional: bool(raw.professional, false),
    muted: bool(raw.muted, false),
    sortOrder: clampInt(raw.sortOrder, 0, 9999, 0),
    notes: str(raw.notes),
  };
}

function parseLocation(raw: unknown): Location | null {
  if (!isObject(raw)) return null;
  const name = str(raw.name);
  if (!name) return null;
  const sla = raw.slaMinutes;
  return {
    name,
    slaMinutes: typeof sla === 'number' && Number.isFinite(sla) ? clampInt(sla, SLA_MIN_MINUTES, SLA_MAX_MINUTES, 15) : null,
    hours: parseHours(raw.hours),
  };
}

export function parseHours(raw: unknown): BusinessHours | null {
  if (!isObject(raw)) return null;
  const days = (Array.isArray(raw.workingDays) ? raw.workingDays : [])
    .filter((d): d is number => typeof d === 'number' && Number.isInteger(d) && d >= 0 && d <= 6);
  const hours: BusinessHours = {
    enabled: bool(raw.enabled, false),
    openMinutes: clampInt(raw.openMinutes, 0, 1440, 9 * 60),
    closeMinutes: clampInt(raw.closeMinutes, 0, 1440, 18 * 60),
    workingDays: days.length ? [...new Set(days)] : [1, 2, 3, 4, 5, 6],
  };
  // Per-day hours, 0 = Sunday. A day that is not a window, or closes before it opens, is a closed day.
  if (Array.isArray(raw.week) && raw.week.length === 7) {
    hours.week = raw.week.map((d) => {
      if (!isObject(d)) return null;
      const open = clampInt(d.open, 0, 1440, -1), close = clampInt(d.close, 0, 1440, -1);
      return open >= 0 && close > open ? { open, close } : null;
    });
  }
  return hours;
}

export interface Holiday { name: string; date: string; /** Location names; empty means every location. */ locations: string[] }

/** Holidays with a name and a real date, one per date and set of locations, locations spelled as the location
 *  list spells them (unknown ones dropped), in date order. */
function parseHolidays(raw: unknown, locations: Location[]): Holiday[] {
  const canonical = new Map(locations.map((l) => [l.name.toLowerCase(), l.name]));
  const seen = new Set<string>();
  const out: Holiday[] = [];
  for (const row of Array.isArray(raw) ? raw.slice(0, 500) : []) {
    if (!isObject(row)) continue;
    const name = str(row.name), date = str(row.date);
    const [y, m, d] = date.split('-').map(Number);
    const real = /^\d{4}-\d{2}-\d{2}$/.test(date) && new Date(y, m - 1, d).getDate() === d;
    if (!name || !real) continue;
    const wanted = Array.isArray(row.locations) ? row.locations.map((l) => canonical.get(str(l).toLowerCase())) : [];
    const where = [...new Set(wanted.filter((l): l is string => !!l))].sort();
    // Named locations that no longer exist leave a holiday that applies nowhere: drop it rather than widen it.
    if (Array.isArray(row.locations) && row.locations.length && !where.length) continue;
    const id = `${date}|${where.join('|')}`;
    if (seen.has(id)) continue;
    seen.add(id);
    out.push({ name: name.slice(0, 80), date, locations: where });
  }
  return out.sort((a, b) => a.date.localeCompare(b.date));
}

/** A location's hours with its holidays filled in as closed dates. Null when it has none: its clock never stops. */
export function hoursFor(config: Config, locationName: string): BusinessHours | null {
  const location = config.locations.find((l) => l.name === locationName);
  if (!location?.hours) return null;
  const closedDates = config.holidays.filter((h) => !h.locations.length || h.locations.includes(location.name)).map((h) => h.date);
  return { ...location.hours, closedDates: [...new Set(closedDates)].sort() };
}

/** A bad row costs only itself, like everywhere else: a saved reply with no words is dropped, not fatal. */
function parseSavedReplies(raw: unknown): SavedReply[] {
  if (!Array.isArray(raw)) return [];
  const out: SavedReply[] = [];
  for (const row of raw) {
    if (!isObject(row)) continue;
    const title = String(row.title ?? '').trim().slice(0, SAVED_REPLY_TITLE_MAX);
    const body = String(row.body ?? '').trim().slice(0, SAVED_REPLY_BODY_MAX);
    if (body) out.push({ title: title || body.slice(0, 24), body });
    if (out.length === SAVED_REPLY_MAX) break;
  }
  return out;
}

/** Words are kept as typed (trimmed, each once); numbers keep their digits only, and anything too short to be a
 *  phone number is dropped rather than left to match half the address book. */
function parseNotCustomers(raw: unknown): Settings['notCustomers'] {
  const r = isObject(raw) ? raw : {};
  const list = (v: unknown) => (Array.isArray(v) ? v.map((x) => String(x ?? '')) : []);
  const words: string[] = [];
  for (const w of list(r.words).map((x) => x.trim().slice(0, 30))) {
    if (w && !words.some((have) => have.toLowerCase() === w.toLowerCase())) words.push(w);
  }
  const numbers = [...new Set(list(r.numbers).map((x) => x.replace(/\D/g, '')).filter((d) => d.length >= 7 && d.length <= 15))];
  return { words: words.slice(0, 40), numbers: numbers.slice(0, 200) };
}

function parseSettings(raw: unknown): Settings {
  const d = defaultSettings();
  if (!isObject(raw)) return d;
  const assistant = isObject(raw.assistant) ? raw.assistant : {};
  const quiet = isObject(raw.quietHours) ? raw.quietHours : {};
  const alerts = isObject(raw.alerts) ? raw.alerts : {};
  const weekly = isObject(raw.weeklyReport) ? raw.weeklyReport : {};
  const include = isObject(weekly.include) ? weekly.include : {};
  const di = d.weeklyReport.include;
  const theme = str(raw.theme);
  return {
    slaMinutes: clampInt(raw.slaMinutes, SLA_MIN_MINUTES, SLA_MAX_MINUTES, d.slaMinutes),
    backlogAfterDays: clampInt(raw.backlogAfterDays, 1, 90, d.backlogAfterDays),
    filterClosedConversations: bool(raw.filterClosedConversations, d.filterClosedConversations),
    sleepUnusedAccounts: bool(raw.sleepUnusedAccounts, d.sleepUnusedAccounts),
    closeToBackground: bool(raw.closeToBackground, d.closeToBackground),
    sleepAfterMinutes: clampInt(raw.sleepAfterMinutes, 0, 240, d.sleepAfterMinutes),
    readEverySeconds: clampInt(raw.readEverySeconds, 15, 600, d.readEverySeconds),
    assistant: {
      enabled: bool(assistant.enabled, d.assistant.enabled),
      model: str(assistant.model) || d.assistant.model,
      endpoint: endpoint(str(assistant.endpoint) || d.assistant.endpoint),
    },
    theme: theme === 'light' || theme === 'dark' || theme === 'system' ? theme : d.theme,
    quietHours: {
      enabled: bool(quiet.enabled, d.quietHours.enabled),
      startHour: clampInt(quiet.startHour, 0, 23, d.quietHours.startHour),
      endHour: clampInt(quiet.endHour, 0, 23, d.quietHours.endHour),
    },
    alerts: {
      nearTarget: bool(alerts.nearTarget, d.alerts.nearTarget),
      waitedHour: bool(alerts.waitedHour, d.alerts.waitedHour),
      signedOut: bool(alerts.signedOut, d.alerts.signedOut),
      callNotReturned: bool(alerts.callNotReturned, d.alerts.callNotReturned),
    },
    morningDigest: bool(raw.morningDigest, d.morningDigest),
    savedReplies: parseSavedReplies(raw.savedReplies),
    notCustomers: parseNotCustomers(raw.notCustomers),
    weeklyReport: {
      autoSave: bool(weekly.autoSave, d.weeklyReport.autoSave),
      include: {
        figures: bool(include.figures, di.figures), locations: bool(include.locations, di.locations),
        accounts: bool(include.accounts, di.accounts), calls: bool(include.calls, di.calls), names: bool(include.names, di.names),
      },
    },
  };
}

const isObject = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);
const str = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
const bool = (v: unknown, fallback: boolean) => (typeof v === 'boolean' ? v : fallback);
const clampInt = (v: unknown, min: number, max: number, fallback: number) =>
  Math.min(max, Math.max(min, typeof v === 'number' && Number.isFinite(v) ? Math.round(v) : fallback));
const isChannel = (v: string): v is ChannelId => Object.hasOwn(CHANNELS, v);
const endpoint = (url: string) => (url.endsWith('/') ? url : `${url}/`);
