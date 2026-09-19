// The business setup a workspace shares between its PCs (roadmap 6.3), and nothing else. Pure: what is shared, how a
// setup that arrives is applied to this PC's config, and how it is written in Firestore's REST format.
//
// Shared: the accounts (without their login, which never leaves the PC), the locations with their hours and targets,
// holidays, and the business-wide rules (reply target, backlog, the closed-chat filter, saved replies, who is not a
// customer). Kept on each PC: everything personal or about this machine — theme, notifications, quiet hours, sleep,
// the assistant, the digest, the weekly report, and whether an account is muted here. Oversight data is not part of
// the config at all, so it cannot reach this file.
import { parseConfig, type Account, type Config, type Holiday, type Location, type SavedReply } from './config.ts';

export interface SharedAccount { id: string; name: string; channel: string; url: string; location: string; professional: boolean; sortOrder: number; notes: string }
export interface SharedSettings {
  slaMinutes: number; backlogAfterDays: number; filterClosedConversations: boolean;
  savedReplies: SavedReply[]; notCustomers: { words: string[]; numbers: string[] }; holidays: Holiday[];
}
/** Exactly the three fields the Firestore rules accept for `config/main`, besides who changed it and when. */
export interface SharedSetup { accounts: SharedAccount[]; locations: Location[]; settings: SharedSettings }

export function sharedSetup(config: Config): SharedSetup {
  const s = config.settings;
  return {
    accounts: config.accounts.map((a) => ({ id: a.id, name: a.name, channel: a.channel, url: a.url, location: a.location, professional: a.professional, sortOrder: a.sortOrder, notes: a.notes })),
    locations: config.locations.map((l) => ({ name: l.name, slaMinutes: l.slaMinutes, hours: l.hours })),
    settings: {
      slaMinutes: s.slaMinutes, backlogAfterDays: s.backlogAfterDays, filterClosedConversations: s.filterClosedConversations,
      savedReplies: s.savedReplies, notCustomers: s.notCustomers, holidays: config.holidays,
    },
  };
}

/** A fingerprint of the shared part, to tell whether it changed. Key order does not matter. */
export function setupKey(setup: SharedSetup): string {
  const sort = (v: unknown): unknown => Array.isArray(v) ? v.map(sort)
    : v && typeof v === 'object' ? Object.fromEntries(Object.keys(v).sort().map((k) => [k, sort((v as Record<string, unknown>)[k])])) : v;
  return JSON.stringify(sort(setup));
}

/**
 * Applies a setup from the workspace to this PC's config. The workspace wins for everything it shares. Accounts:
 * those it lists are added or updated (this PC's mute is kept); an account this PC had from the workspace before
 * (`synced`) and that the workspace no longer lists was removed on another PC, so it goes here too; an account that
 * only ever existed on this PC is left alone. Runs through the config parser, so a setup from a newer or broken
 * build cannot put anything invalid into this PC's config.
 */
export function applySetup(config: Config, setup: SharedSetup, synced: ReadonlySet<string>) {
  const remote = new Map(setup.accounts.map((a) => [a.id, a]));
  const local = new Map(config.accounts.map((a) => [a.id, a]));
  const removed = config.accounts.filter((a) => synced.has(a.id) && !remote.has(a.id)).map((a) => a.id);
  const added = setup.accounts.filter((a) => !local.has(a.id)).map((a) => a.id);
  const accounts: Account[] = [
    ...setup.accounts.map((r) => ({ ...(local.get(r.id) ?? { muted: false }), ...r }) as Account),
    ...config.accounts.filter((a) => !remote.has(a.id) && !synced.has(a.id)),
  ];
  const { holidays, ...rules } = setup.settings;
  const { config: next } = parseConfig({ ...config, accounts, locations: setup.locations, holidays, settings: { ...config.settings, ...rules } });
  // Accounts the parser refused are not counted as synced, so a later pull does not "remove" what never arrived.
  const arrived = new Set(next.accounts.map((a) => a.id));
  return { config: next, added: added.filter((id) => arrived.has(id)), removed, synced: new Set(setup.accounts.map((a) => a.id).filter((id) => arrived.has(id))) };
}

// ---- Firestore's REST format: every value is tagged with its type. -------------------------------------------------

export type FsValue =
  | { nullValue: null } | { booleanValue: boolean } | { integerValue: string } | { doubleValue: number } | { stringValue: string }
  | { timestampValue: string } | { arrayValue: { values?: FsValue[] } } | { mapValue: { fields?: Record<string, FsValue> } };

export function toFs(v: unknown): FsValue {
  if (v === null || v === undefined) return { nullValue: null };
  if (typeof v === 'boolean') return { booleanValue: v };
  if (typeof v === 'number') return Number.isInteger(v) ? { integerValue: String(v) } : { doubleValue: v };
  if (typeof v === 'string') return { stringValue: v };
  if (v instanceof Date) return { timestampValue: v.toISOString() };
  if (Array.isArray(v)) return { arrayValue: { values: v.map(toFs) } };
  return { mapValue: { fields: toFields(v as Record<string, unknown>) } };
}

export const toFields = (o: Record<string, unknown>): Record<string, FsValue> =>
  Object.fromEntries(Object.entries(o).filter(([, v]) => v !== undefined).map(([k, v]) => [k, toFs(v)]));

/** Back to plain values. Timestamps become milliseconds since 1970, which is how the app keeps time everywhere. */
export function fromFs(v: FsValue | undefined): unknown {
  if (!v) return null;
  if ('nullValue' in v) return null;
  if ('booleanValue' in v) return v.booleanValue;
  if ('integerValue' in v) return Number(v.integerValue);
  if ('doubleValue' in v) return v.doubleValue;
  if ('stringValue' in v) return v.stringValue;
  if ('timestampValue' in v) return Date.parse(v.timestampValue);
  if ('arrayValue' in v) return (v.arrayValue.values ?? []).map(fromFs);
  if ('mapValue' in v) return fromFields(v.mapValue.fields ?? {});
  return null;
}

export const fromFields = (f: Record<string, FsValue>): Record<string, unknown> =>
  Object.fromEntries(Object.entries(f).map(([k, v]) => [k, fromFs(v)]));

/** A setup read from Firestore, taken apart defensively: anything missing is empty, never a crash. */
export function readSetup(fields: Record<string, FsValue> | undefined): SharedSetup & { updatedAt: number; updatedBy: string } {
  const o = fromFields(fields ?? {});
  const settings = (o.settings && typeof o.settings === 'object' ? o.settings : {}) as Partial<SharedSettings>;
  return {
    accounts: Array.isArray(o.accounts) ? (o.accounts as SharedAccount[]) : [],
    locations: Array.isArray(o.locations) ? (o.locations as Location[]) : [],
    settings: {
      slaMinutes: settings.slaMinutes ?? 15, backlogAfterDays: settings.backlogAfterDays ?? 7,
      filterClosedConversations: settings.filterClosedConversations ?? true, savedReplies: settings.savedReplies ?? [],
      notCustomers: settings.notCustomers ?? { words: [], numbers: [] }, holidays: settings.holidays ?? [],
    },
    updatedAt: typeof o.updatedAt === 'number' ? o.updatedAt : 0,
    updatedBy: typeof o.updatedBy === 'string' ? o.updatedBy : '',
  };
}
