// What the app knows about one customer: the owner's own note and tags, and the plain facts its reads have seen —
// when this conversation was first read, how many times it has come back to the line, and how quickly the last few
// were answered. Keys and times only, as everywhere else: the name beside them on screen comes from the snapshot.
//
// The reply-time store cannot answer "how fast was this customer answered", because its samples carry no
// conversation key (one account's samples are a flat list), so this keeps its own.
import type { ChatEntry } from './chat-entry.ts';

const MINUTE = 60_000, DAY = 86_400_000;
/** Longer than this and the chat was answered somewhere else; counting it would say we took nine days. */
const MAX_CREDIBLE_MS = 7 * DAY;
const KEEP_ANSWERS = 5;
const NOTE_MAX = 2000;
const TAG_MAX = 24;
const TAGS_PER_CUSTOMER = 8;
/** A record with nothing written by the owner is dropped once the conversation has been quiet this long. */
const QUIET_DAYS = 120;

export interface Answer { at: number; minutes: number }

export interface CustomerRecord {
  note: string;
  tags: string[];
  /** The first read that saw this conversation. Not when the customer first wrote: nobody can know that. */
  firstSeen: number;
  lastSeen: number;
  /** How many times they have come to the line: seen waiting after having been answered. */
  conversations: number;
  /** The newest few answers, newest last. */
  answers: Answer[];
  /** When the owner last wrote a note or changed a tag; 0 when they never have. */
  editedAt: number;
}

export type Customers = Record<string, CustomerRecord>;

export const customerId = (account: string, key: string) => `${account}|${key}`;

const blank = (now: number): CustomerRecord =>
  ({ note: '', tags: [], firstSeen: now, lastSeen: now, conversations: 0, answers: [], editedAt: 0 });

/** The record as it stands, without creating one. */
export const customerFor = (customers: Customers, account: string, key: string): CustomerRecord | null =>
  customers[customerId(account, key)] ?? null;

const record = (customers: Customers, account: string, key: string, now: number): CustomerRecord =>
  (customers[customerId(account, key)] ??= blank(now));

export function setNote(customers: Customers, account: string, key: string, note: string, now: number) {
  const r = record(customers, account, key, now);
  r.note = note.slice(0, NOTE_MAX).trim();
  r.editedAt = now;
}

/** Adds the tag, or removes it when it is already there. Spelling is the owner's; matching ignores case. */
export function toggleTag(customers: Customers, account: string, key: string, tag: string, now: number) {
  const clean = tag.trim().replace(/\s+/g, ' ').slice(0, TAG_MAX);
  if (!clean) return;
  const r = record(customers, account, key, now);
  const at = r.tags.findIndex((t) => t.toLowerCase() === clean.toLowerCase());
  if (at >= 0) r.tags.splice(at, 1);
  else if (r.tags.length < TAGS_PER_CUSTOMER) r.tags.push(clean);
  r.editedAt = now;
}

/** Every tag the owner has used, most used first, so the same word is not typed two ways. */
export function tagsInUse(customers: Customers): string[] {
  const counts = new Map<string, { tag: string; n: number }>();
  for (const r of Object.values(customers)) {
    for (const t of r.tags) {
      const seen = counts.get(t.toLowerCase());
      if (seen) seen.n++;
      else counts.set(t.toLowerCase(), { tag: t, n: 1 });
    }
  }
  return [...counts.values()].sort((a, b) => b.n - a.n || a.tag.localeCompare(b.tag)).map((c) => c.tag);
}

/**
 * What this read saw. Called with the chats as they were before it and as they are now, so the two transitions
 * that matter can be told apart: a customer coming back to the line, and us answering them.
 */
export function recordCustomers(customers: Customers, account: string, prior: ChatEntry[] | undefined, chats: ChatEntry[], now: number): boolean {
  const was = new Map(prior?.map((c) => [c.conversationKey, c] as const));
  let changed = false;
  for (const c of chats) {
    const before = was.get(c.conversationKey);
    const existing = customerFor(customers, account, c.conversationKey);
    // A conversation nobody has written anything about, and which is not waiting, is not worth a record yet:
    // every account has hundreds of quiet chats and none of them has anything to say.
    if (!existing && !c.awaiting) continue;
    const r = record(customers, account, c.conversationKey, now);
    if (!existing) changed = true;
    r.lastSeen = now;
    if (c.awaiting && (!before || !before.awaiting)) { r.conversations++; changed = true; }
    if (before?.awaiting && c.lastMessageFromMe) {
      const gap = c.lastActivity - before.lastActivity;
      if (gap > 0 && gap <= MAX_CREDIBLE_MS) {
        r.answers.push({ at: c.lastActivity, minutes: gap / MINUTE });
        if (r.answers.length > KEEP_ANSWERS) r.answers.splice(0, r.answers.length - KEEP_ANSWERS);
        changed = true;
      }
    }
  }
  return changed;
}

/** Drops records the owner never wrote on, once their conversation has been quiet for months. */
export function pruneCustomers(customers: Customers, now: number) {
  for (const [id, r] of Object.entries(customers)) {
    if (!r.editedAt && !r.note && r.tags.length === 0 && now - r.lastSeen > QUIET_DAYS * DAY) delete customers[id];
  }
}

/** Everything under one account forgotten, for when an account is removed. */
export function forgetAccountCustomers(customers: Customers, account: string) {
  const prefix = `${account}|`;
  for (const id of Object.keys(customers)) if (id.startsWith(prefix)) delete customers[id];
}
