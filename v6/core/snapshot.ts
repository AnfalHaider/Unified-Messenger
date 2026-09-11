// Port of OversightChatSnapshotService.cs, minus the file handling: each account's latest read, and the one
// predicate every "waiting" number comes from. The headline, the list under it, the digest and the split all
// route through isAwaiting, which is what stops a count and its own list disagreeing.
import type { ChatEntry } from './chat-entry.ts';
import { isSuppressed, type Overrides } from './awaiting-overrides.ts';
import { classify, type ReplyNeedVerdict } from './reply-need.ts';
import { observe, type ResponseTimes } from './response-times.ts';

const DAY = 86_400_000;
// A chat is carried as waiting on an unconfirmed read only while it is this recent, so one whose reply we
// never saw cannot stay on the list forever.
const STICKY_MAX_AGE_MS = 7 * DAY;

export interface AccountSnapshot { capturedAt: number; chats: ChatEntry[] }
/** accountId → its latest read. Times are epoch milliseconds. */
export type Snapshots = Record<string, AccountSnapshot>;

export interface Judge {
  now: number;
  overrides: Overrides;
  /** The owner's setting. Off restores the raw number. */
  filterClosed: boolean;
  /** The local model's cached answer for a preview the word rules could not place. Read, never awaited. */
  aiNeedsReply?: (preview: string) => boolean | null | undefined;
}

/** Stores a fresh read and feeds response times the post-sticky state, so they measure real transitions. */
export function recordRead(snapshots: Snapshots, times: ResponseTimes, account: string, incoming: ChatEntry[], capturedAt: number, now = capturedAt) {
  const id = account.trim();
  if (!id) return;
  const chats = keepAwaitingUntilReplied(snapshots[id]?.chats, incoming, now);
  snapshots[id] = { capturedAt, chats };
  for (const c of chats) observe(times, id, c.conversationKey, c.awaiting, c.lastMessageFromMe, c.lastActivity, now);
}

/** Opening a chat clears its unread marker but is not a reply. Only a read showing our message last clears
 *  waiting; an unconfirmed "not waiting" inherits the earlier state while the chat is recent. */
export function keepAwaitingUntilReplied(prior: ChatEntry[] | undefined, incoming: ChatEntry[], now: number): ChatEntry[] {
  const was = new Map(prior?.map((c) => [c.conversationKey, c.awaiting] as const));
  return incoming.map((c) =>
    !c.awaiting && !c.lastMessageFromMe && was.get(c.conversationKey) && now - c.lastActivity <= STICKY_MAX_AGE_MS ? { ...c, awaiting: true } : c);
}

/** A read taken seconds after a reload reports "no last message" for nearly every chat, which read literally
 *  says every message was deleted: 354 real conversations once rendered as 5. When at most half the chats have
 *  a message the claim is withdrawn for all of them. Apply to a snapshot loaded from disk. */
export function distrustColdScan(chats: ChatEntry[]): ChatEntry[] {
  const withMessage = chats.filter((c) => c.hasLastMessage === true).length;
  return chats.length > 0 && withMessage * 2 <= chats.length ? chats.map((c) => ({ ...c, hasLastMessage: null })) : chats;
}

/** Word rules first; the model's cached answer only for what the rules called substantive, and only toward closed. */
export function verdictFor(chat: ChatEntry, judge: Judge): ReplyNeedVerdict {
  const verdict = classify({
    preview: chat.preview, hasLastMessage: chat.hasLastMessage, type: chat.lastMessageType,
    waitingForMs: judge.now - chat.lastActivity, fromMe: chat.lastMessageFromMe, callOutcome: chat.lastCallOutcome,
  });
  return verdict.reason === 'substantive' && judge.aiNeedsReply?.(chat.preview) === false ? { needsReply: false, reason: 'aiJudgedClosed' } : verdict;
}

export const isAutomaticallyClosed = (chat: ChatEntry, judge: Judge) => judge.filterClosed && !verdictFor(chat, judge).needsReply;

/** Waiting, not closed by the classifier, and not marked handled or snoozed by the owner. */
export const isAwaiting = (account: string, chat: ChatEntry, judge: Judge) =>
  chat.awaiting && !isAutomaticallyClosed(chat, judge) && !isSuppressed(judge.overrides, account, chat.conversationKey, chat.lastActivity, judge.now);

/** A waiting chat is current state: always active and never caught up, whatever the window, because a customer
 *  waiting since yesterday still needs a reply today. The window scopes only the caught-up chats. Null without a read. */
export function windowed(snapshots: Snapshots, account: string, judge: Judge, from: number | null = null, to: number | null = null) {
  const id = account.trim(), snap = snapshots[id];
  if (!snap) return null;
  let active = 0, caughtUp = 0;
  for (const c of snap.chats) {
    if (isAwaiting(id, c, judge)) active++;
    else if ((from === null || c.lastActivity >= from) && (to === null || c.lastActivity <= to)) { active++; caughtUp++; }
  }
  return { active, caughtUp };
}

/** Every chat waiting on this account: most unread first, then most recent. */
export function awaitingChats(snapshots: Snapshots, account: string, judge: Judge): ChatEntry[] {
  const id = account.trim();
  return (snapshots[id]?.chats ?? []).filter((c) => isAwaiting(id, c, judge)).sort((a, b) => b.unread - a.unread || b.lastActivity - a.lastActivity);
}

export interface AwaitingSplit { needsReply: number; backlog: number; closedAutomatically: number; unreadable: number }

/** "466 waiting" was true and useless; the same data split read 79 to reply to, 283 backlog, 104 closed. Every
 *  waiting chat lands in exactly one bucket. Unreadable counts only the live queue, where it is actionable. */
export function awaitingSplit(snapshots: Snapshots, accounts: string[], judge: Judge, backlogAfterDays: number): AwaitingSplit {
  const cutoff = judge.now - Math.max(1, backlogAfterDays) * DAY;
  const split: AwaitingSplit = { needsReply: 0, backlog: 0, closedAutomatically: 0, unreadable: 0 };
  for (const [id, c] of chatsOf(snapshots, accounts)) {
    // Mark-handled is the owner's decision, not the classifier's, so it leaves every bucket.
    if (!c.awaiting || isSuppressed(judge.overrides, id, c.conversationKey, c.lastActivity, judge.now)) continue;
    if (isAutomaticallyClosed(c, judge)) split.closedAutomatically++;
    else if (c.lastActivity < cutoff) split.backlog++;
    else {
      split.needsReply++;
      if (verdictFor(c, judge).reason === 'noPreviewAvailable') split.unreadable++;
    }
  }
  return split;
}

/** The chats the classifier excluded, with why, newest first, so the owner can check its work. */
export function automaticallyClosed(snapshots: Snapshots, accounts: string[], judge: Judge) {
  const closed: { account: string; chat: ChatEntry; verdict: ReplyNeedVerdict }[] = [];
  for (const [account, chat] of chatsOf(snapshots, accounts)) {
    if (!chat.awaiting || isSuppressed(judge.overrides, account, chat.conversationKey, chat.lastActivity, judge.now)) continue;
    const verdict = verdictFor(chat, judge);
    if (!verdict.needsReply) closed.push({ account, chat, verdict });
  }
  return closed.sort((a, b) => b.chat.lastActivity - a.chat.lastActivity);
}

export interface Digest { newAwaiting: number; totalAwaiting: number; accountsWithAwaiting: number; oldestActivity: number | null; hasData: boolean }

/** "Since you were last here". hasData stays false until at least one account has a read. */
export function digest(snapshots: Snapshots, accounts: string[], judge: Judge, since: number | null): Digest {
  const d: Digest = { newAwaiting: 0, totalAwaiting: 0, accountsWithAwaiting: 0, oldestActivity: null, hasData: false };
  for (const account of accounts) {
    const id = account.trim(), snap = snapshots[id];
    if (!snap) continue;
    d.hasData = true;
    const waiting = snap.chats.filter((c) => isAwaiting(id, c, judge));
    if (waiting.length) d.accountsWithAwaiting++;
    for (const c of waiting) {
      d.totalAwaiting++;
      if (since === null || c.lastActivity > since) d.newAwaiting++;
      if (d.oldestActivity === null || c.lastActivity < d.oldestActivity) d.oldestActivity = c.lastActivity;
    }
  }
  return d;
}

/** The newest capture across accounts: the "as of" stamp. */
export const lastCaptured = (snapshots: Snapshots) =>
  Object.values(snapshots).reduce<number | null>((max, s) => (max === null || s.capturedAt > max ? s.capturedAt : max), null);

const chatsOf = (snapshots: Snapshots, accounts: string[]) =>
  accounts.flatMap((a) => (snapshots[a.trim()]?.chats ?? []).map((c) => [a.trim(), c] as const));
