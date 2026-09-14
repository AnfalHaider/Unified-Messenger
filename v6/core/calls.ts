// Missed calls, and whether each was returned. A snapshot only holds each chat's latest message, so a call has to be
// written down while it is the latest thing in the chat, and its return noticed on a later read: our own message or
// call after the call time. The app layer calls recordCalls after every read and saves the object as JSON.
//
// Keys and times only, never a name or a number: the screens take the caller's name from the snapshot when they draw.
import type { ChatEntry } from './chat-entry.ts';
import { classify } from './reply-need.ts';

/** How far back a first read may bring calls in. Older calls were probably dealt with before the app was watching. */
export const CALL_WINDOW_DAYS = 7;
const KEEP_DAYS = 31;
const DAY = 86_400_000;

export interface CallRecord {
  account: string;
  key: string;
  /** When the customer called. */
  at: number;
  /** When we answered after the call, by message or by calling back; null while not returned. */
  returnedAt: number | null;
  returnedBy: 'message' | 'call' | null;
}

/** "account|key|at" → the call. */
export type Calls = Record<string, CallRecord>;

const isMissedCall = (c: ChatEntry) => !c.lastMessageFromMe
  && classify({ preview: c.preview, type: c.lastMessageType, fromMe: false, callOutcome: c.lastCallOutcome }).reason === 'missedCall';

/** Records new missed calls and marks returned ones. Returns whether anything changed, so the caller saves only then. */
export function recordCalls(calls: Calls, account: string, chats: ChatEntry[], now: number): boolean {
  let changed = false;
  const byKey = new Map(chats.map((c) => [c.conversationKey, c]));
  for (const chat of chats) {
    if (!isMissedCall(chat) || chat.lastActivity < now - CALL_WINDOW_DAYS * DAY) continue;
    const id = `${account}|${chat.conversationKey}|${chat.lastActivity}`;
    if (calls[id]) continue;
    calls[id] = { account, key: chat.conversationKey, at: chat.lastActivity, returnedAt: null, returnedBy: null };
    changed = true;
  }
  for (const call of Object.values(calls)) {
    if (call.account !== account || call.returnedAt !== null) continue;
    const chat = byKey.get(call.key);
    // Returned only by something from us after the call. A customer writing again, or calling again, is not.
    if (!chat || !chat.lastMessageFromMe || chat.lastActivity <= call.at) continue;
    call.returnedAt = chat.lastActivity;
    call.returnedBy = /^call/.test(chat.lastMessageType) ? 'call' : 'message';
    changed = true;
  }
  return changed;
}

/** Calls on these accounts from `from` up to `to`, newest first. */
export const callsIn = (calls: Calls, accounts: string[], from: number, to: number): CallRecord[] =>
  Object.values(calls).filter((c) => accounts.includes(c.account) && c.at >= from && c.at < to).sort((a, b) => b.at - a.at);

/** Drops calls older than a month. Call before saving. */
export function pruneCalls(calls: Calls, now: number) {
  for (const [id, call] of Object.entries(calls)) if (call.at < now - KEEP_DAYS * DAY) delete calls[id];
}
