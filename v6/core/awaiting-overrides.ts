// Port of AwaitingOverrideStore.cs, minus the file handling: the app layer saves this object as JSON.
// The owner hides a waiting chat they dealt with elsewhere (phone, call, in person). Both kinds expire on
// their own so the backlog can never be faked for good: "handled" lasts until a newer customer message
// arrives, "snoozed" until a time.

/** `at` is when the mark was made, for the Set aside list. Marks imported from v5 have none. */
export type Override = ({ kind: 'handled'; activity: number } | { kind: 'snoozed'; until: number }) & { at?: number };
/** accountId → conversationKey → override. Times are epoch milliseconds. */
export type Overrides = Record<string, Record<string, Override>>;

function put(o: Overrides, account: string, chat: string, value: Override) {
  if (!account.trim() || !chat.trim()) return;
  (o[account.trim()] ??= {})[chat] = value;
}

const dated = (at?: number) => (at === undefined ? {} : { at });

export const markHandled = (o: Overrides, account: string, chat: string, lastActivity: number, at?: number) =>
  put(o, account, chat, { kind: 'handled', activity: lastActivity, ...dated(at) });

export const snooze = (o: Overrides, account: string, chat: string, until: number, at?: number) =>
  put(o, account, chat, { kind: 'snoozed', until, ...dated(at) });

export function clear(o: Overrides, account: string, chat: string) {
  delete o[account.trim()]?.[chat];
}

export function isSuppressed(o: Overrides, account: string, chat: string, lastActivity: number, now: number): boolean {
  const ov = o[account.trim()]?.[chat];
  if (!ov) return false;
  return ov.kind === 'handled' ? lastActivity <= ov.activity : now < ov.until;
}

/** Drops elapsed snoozes and emptied accounts. Call after loading and before saving. */
export function pruneExpired(o: Overrides, now: number) {
  for (const [account, chats] of Object.entries(o)) {
    for (const [chat, ov] of Object.entries(chats)) if (ov.kind === 'snoozed' && now >= ov.until) delete chats[chat];
    if (Object.keys(chats).length === 0) delete o[account];
  }
}
