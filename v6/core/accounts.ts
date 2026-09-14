// Adding, editing and removing accounts. Each returns a new config that has been through the config parser, or the
// untouched config and a sentence saying why not, so a bad request never half-applies. Removing an account also
// has to forget everything stored under its id, which `forgetAccount` does in the app layer's stores.
import { CHANNELS, parseConfig, type ChannelId, type Config } from './config.ts';

export interface NewAccount { channel: string; name: string; location: string; url?: string }
export interface AccountEdit { name: string; location: string; professional: boolean }
export interface AccountResult { config: Config; error: string | null }

const isChannel = (v: string): v is ChannelId => Object.hasOwn(CHANNELS, v);

/** The location list's own spelling of a name, adding the location when it is new. */
function withLocation(config: Config, name: string): { location: string; locations: Config['locations'] } {
  const clean = name.trim();
  if (!clean) return { location: '', locations: config.locations };
  const existing = config.locations.find((l) => l.name.toLowerCase() === clean.toLowerCase());
  if (existing) return { location: existing.name, locations: config.locations };
  return { location: clean.slice(0, 60), locations: [...config.locations, { name: clean.slice(0, 60), slaMinutes: null, hours: null }] };
}

const reparsed = (next: Config): Config => parseConfig(next).config;

export function addAccount(config: Config, request: NewAccount, id: string): AccountResult & { id: string } {
  const refuse = (error: string) => ({ config, error, id });
  if (!isChannel(request.channel)) return refuse('Choose a channel.');
  if (config.accounts.some((a) => a.id.toLowerCase() === id.toLowerCase())) return refuse('That account already exists.');
  const channel = request.channel;
  let url = CHANNELS[channel].url;
  if (channel === 'custom') {
    const typed = (request.url ?? '').trim();
    if (!/^https?:\/\/[^\s/]+\.[^\s]+/i.test(typed)) return refuse('Enter the page’s web address, starting with https://');
    url = typed;
  }
  const { location, locations } = withLocation(config, request.location);
  const account = {
    id, channel, url, location,
    name: request.name.trim().slice(0, 60) || CHANNELS[channel].name,
    // A channel with a reader is added to be counted; the owner can switch that off when it is a personal account.
    professional: CHANNELS[channel].reads,
    muted: false, notes: '',
    sortOrder: Math.max(0, ...config.accounts.map((a) => a.sortOrder + 1)),
  };
  return { config: reparsed({ ...config, locations, accounts: [...config.accounts, account] }), error: null, id };
}

export function editAccount(config: Config, id: string, edit: AccountEdit): AccountResult {
  const current = config.accounts.find((a) => a.id === id);
  if (!current) return { config, error: 'That account no longer exists.' };
  const { location, locations } = withLocation(config, edit.location);
  const accounts = config.accounts.map((a) => (a.id === id
    ? { ...a, name: edit.name.trim().slice(0, 60) || CHANNELS[a.channel].name, location, professional: !!edit.professional }
    : a));
  return { config: reparsed({ ...config, locations, accounts }), error: null };
}

/** Removes the account. Its location stays, with its opening hours, for accounts added there later. */
export function removeAccount(config: Config, id: string): AccountResult {
  if (!config.accounts.some((a) => a.id === id)) return { config, error: 'That account no longer exists.' };
  return { config: reparsed({ ...config, accounts: config.accounts.filter((a) => a.id !== id) }), error: null };
}

type Keyed = Record<string, unknown>;
export interface AccountStores {
  snapshots: Keyed; overrides: Keyed; history: Keyed;
  times: { pending: Keyed; watchStart: Keyed; samples: Keyed };
  calls: Record<string, { account: string }>;
  notified: Record<string, number>;
}

/** Deletes everything stored under this account, in place: its chats, marks, day records, reply times, calls and
 *  alerts. In place because the app layer holds these objects and saves the same ones. */
export function forgetAccount(id: string, stores: AccountStores) {
  for (const o of [stores.snapshots, stores.overrides, stores.history, stores.times.pending, stores.times.watchStart, stores.times.samples]) delete o[id];
  for (const [key, call] of Object.entries(stores.calls)) if (call.account === id) delete stores.calls[key];
  // Alert ids carry the account after the kind: "near:<account>:…", "signed-out:<account>".
  for (const key of Object.keys(stores.notified)) if (key.split(':')[1] === id) delete stores.notified[key];
}
