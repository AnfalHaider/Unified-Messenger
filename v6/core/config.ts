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
  /** The local assistant is off by default, so a low-end PC never pays for it. */
  assistant: { enabled: boolean; model: string; endpoint: string };
  theme: 'system' | 'light' | 'dark';
  quietHours: { enabled: boolean; startHour: number; endHour: number };
}

export interface Config { version: number; accounts: Account[]; locations: Location[]; settings: Settings }

export const CONFIG_VERSION = 1;

export const defaultSettings = (): Settings => ({
  slaMinutes: 15,
  backlogAfterDays: 7,
  filterClosedConversations: true,
  sleepUnusedAccounts: false,
  sleepAfterMinutes: 20,
  readEverySeconds: 60,
  assistant: { enabled: false, model: 'gemma3:4b', endpoint: 'http://127.0.0.1:11434/' },
  theme: 'system',
  quietHours: { enabled: false, startHour: 21, endHour: 8 },
});

export const emptyConfig = (): Config => ({ version: CONFIG_VERSION, accounts: [], locations: [], settings: defaultSettings() });

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
  return {
    enabled: bool(raw.enabled, false),
    openMinutes: clampInt(raw.openMinutes, 0, 1440, 9 * 60),
    closeMinutes: clampInt(raw.closeMinutes, 0, 1440, 18 * 60),
    workingDays: days.length ? [...new Set(days)] : [1, 2, 3, 4, 5, 6],
  };
}

function parseSettings(raw: unknown): Settings {
  const d = defaultSettings();
  if (!isObject(raw)) return d;
  const assistant = isObject(raw.assistant) ? raw.assistant : {};
  const quiet = isObject(raw.quietHours) ? raw.quietHours : {};
  const theme = str(raw.theme);
  return {
    slaMinutes: clampInt(raw.slaMinutes, SLA_MIN_MINUTES, SLA_MAX_MINUTES, d.slaMinutes),
    backlogAfterDays: clampInt(raw.backlogAfterDays, 1, 90, d.backlogAfterDays),
    filterClosedConversations: bool(raw.filterClosedConversations, d.filterClosedConversations),
    sleepUnusedAccounts: bool(raw.sleepUnusedAccounts, d.sleepUnusedAccounts),
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
  };
}

const isObject = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);
const str = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
const bool = (v: unknown, fallback: boolean) => (typeof v === 'boolean' ? v : fallback);
const clampInt = (v: unknown, min: number, max: number, fallback: number) =>
  Math.min(max, Math.max(min, typeof v === 'number' && Number.isFinite(v) ? Math.round(v) : fallback));
const isChannel = (v: string): v is ChannelId => Object.hasOwn(CHANNELS, v);
const endpoint = (url: string) => (url.endsWith('/') ? url : `${url}/`);
