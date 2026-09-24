// The marks that a read cannot recover, shared between the PCs of one workspace (owner's decision 2026-09-24).
//
// Replying in the chat needs no sync: waiting is judged by who wrote last, so the next read drops the customer
// from every PC by itself. What no read can see is a customer dealt with **away from the chat** — answered by
// phone, snoozed until the afternoon, or marked as not a customer at all. Without sharing those, two PCs in one
// workspace disagree about the same business: one shows a customer waiting three days who was called back on
// the first morning, and their reply times and caught-up figures drift apart for good.
//
// **This is the one thing the app sends that identifies a customer.** A mark is keyed by the conversation, and a
// WhatsApp conversation key is the customer's number. That was a deliberate decision, it applies only inside a
// workspace, and `site/privacy.html` says so in the workspace section. Nothing else about the customer goes:
// not their name, not a message, not a figure. A PC in no workspace sends nothing at all.
import type { Override, Overrides } from './awaiting-overrides.ts';

/** A mark as the workspace holds it. `cleared` is a put-back: it is how a removal reaches the other PCs. */
export interface SharedMark {
  accountId: string;
  /** The conversation. For WhatsApp this is the customer's number, which is why the policy says so. */
  key: string;
  kind: 'handled' | 'snoozed' | 'excluded' | 'cleared';
  /** When the person pressed the button. Decides which PC wins, so it is the mark's own moment, not the write's. */
  at: number;
  /** A handled mark holds until a newer message than this one. */
  activity?: number;
  /** A snooze holds until this. */
  until?: number;
}

/** What this PC last sent for each mark, so it knows what has changed and what it has to take back. */
export type Pushed = Record<string, number>;

/**
 * The workspace's name for a mark. It carries the account and the conversation because a person reading the
 * database should be able to tell what a row is; the rules match on the **field**, never on this, because a
 * Firestore rule cannot test a document id.
 */
export function markId(accountId: string, key: string): string {
  // Firestore forbids '/' in an id and reserves names wrapped in double underscores; ids are capped well above
  // anything a conversation key reaches, but a runaway key is cut rather than refused at the write.
  const safe = `${accountId}__${key}`.replace(/\//g, '-').replace(/^\.+/, '');
  return safe.slice(0, 300);
}

/** Marks with no `at` came across from v5, which did not record one. They lose to anything dated. */
const when = (o: Override): number => o.at ?? 0;

const shared = (accountId: string, key: string, o: Override): SharedMark => ({
  accountId, key, at: when(o),
  ...(o.kind === 'handled' ? { kind: 'handled' as const, activity: o.activity }
    : o.kind === 'snoozed' ? { kind: 'snoozed' as const, until: o.until }
    : { kind: 'excluded' as const }),
});

/** A shared mark as this PC keeps it. `cleared` has no local form: it is an absence. */
const local = (m: SharedMark): Override | null =>
  m.kind === 'handled' ? { kind: 'handled', activity: Number(m.activity) || 0, at: m.at }
  : m.kind === 'snoozed' ? { kind: 'snoozed', until: Number(m.until) || 0, at: m.at }
  : m.kind === 'excluded' ? { kind: 'excluded', at: m.at }
  : null;

export interface MergeInput {
  /** This PC's marks, as it has them now. */
  overrides: Overrides;
  /** What the workspace holds, for the accounts this member may see. */
  remote: SharedMark[];
  /** What this PC last sent. */
  pushed: Pushed;
  /** The accounts this member may see; null is the whole business. A mark for anything else is not ours to carry. */
  visible: string[] | null;
  now: number;
}

export interface MergeResult {
  /** The marks this PC should now hold. */
  overrides: Overrides;
  /** What to write to the workspace. Empty when this PC has nothing new to say. */
  push: SharedMark[];
  /** What this PC has sent once the push goes through. */
  pushed: Pushed;
}

/**
 * Brings one PC's marks and the workspace's together.
 *
 * The rule is the mark's own moment: the newer press wins, whichever PC made it. A put-back is a `cleared`
 * mark with its own moment, so undoing on one PC reaches the others rather than being quietly re-applied by
 * them — which is what a plain "take the union" would do.
 *
 * Both clocks are Windows' own, so a few seconds of skew is possible and would decide a tie the wrong way.
 * That is accepted: the marks are minutes apart in practice, and the alternative — the server's clock — cannot
 * be compared against a mark made while offline.
 */
export function mergeMarks({ overrides, remote, pushed, visible, now }: MergeInput): MergeResult {
  const mine = visible === null ? null : new Set(visible);
  const canSee = (accountId: string) => mine === null || mine.has(accountId);

  const merged: Overrides = {};
  for (const [accountId, chats] of Object.entries(overrides)) {
    for (const [key, o] of Object.entries(chats)) (merged[accountId] ??= {})[key] = o;
  }

  const push: SharedMark[] = [];
  const nextPushed: Pushed = {};

  // What the workspace says, applied wherever it is newer than what this PC has.
  const byId = new Map<string, SharedMark>();
  for (const m of remote) {
    if (!canSee(m.accountId)) continue;
    const id = markId(m.accountId, m.key);
    const held = byId.get(id);
    if (!held || m.at > held.at) byId.set(id, m);
  }
  for (const [id, m] of byId) {
    const here = merged[m.accountId]?.[m.key];
    if (here && when(here) >= m.at) continue;
    // Gone from this PC, and this is the very mark this PC sent: it was put back here, and saying so is the
    // job of the loop below. Without this, a PC would hand itself back every mark it had just undone.
    if (!here && pushed[id] === m.at) continue;
    const next = local(m);
    if (next) (merged[m.accountId] ??= {})[m.key] = next;
    else if (merged[m.accountId]) delete merged[m.accountId][m.key];
    // Taken from the workspace, so this PC has nothing to say about it.
    nextPushed[id] = m.at;
  }

  // What this PC has that the workspace does not, or has newer.
  for (const [accountId, chats] of Object.entries(merged)) {
    if (!canSee(accountId)) continue;
    for (const [key, o] of Object.entries(chats)) {
      const id = markId(accountId, key);
      const at = when(o);
      if (nextPushed[id] === at) continue;
      if (pushed[id] === at) { nextPushed[id] = at; continue; }
      push.push(shared(accountId, key, o));
      nextPushed[id] = at;
    }
  }

  // Put back here: the workspace is told, or every other PC would hand the mark straight back.
  for (const [id, at] of Object.entries(pushed)) {
    if (nextPushed[id] !== undefined) continue;
    const m = byId.get(id);
    // Already gone from the workspace too: nothing to say, and nothing to keep saying it about.
    if (m && m.kind === 'cleared') continue;
    const [accountId, key] = splitId(id);
    if (!canSee(accountId)) continue;
    push.push({ accountId, key, kind: 'cleared', at: Math.max(now, at + 1) });
    nextPushed[id] = Math.max(now, at + 1);
  }

  for (const [accountId, chats] of Object.entries(merged)) if (!Object.keys(chats).length) delete merged[accountId];
  return { overrides: merged, push, pushed: nextPushed };
}

/** The account and conversation back out of a mark's name, for a put-back this PC no longer holds. */
function splitId(id: string): [string, string] {
  const at = id.indexOf('__');
  return at < 0 ? [id, ''] : [id.slice(0, at), id.slice(at + 2)];
}

/** A cleared mark older than this is dropped from the workspace: it has done its job on every PC by then. */
export const FORGET_CLEARED_AFTER_MS = 30 * 24 * 60 * 60_000;

/** The cleared marks worth deleting, so the collection does not grow for ever. */
export const staleCleared = (remote: SharedMark[], now: number): SharedMark[] =>
  remote.filter((m) => m.kind === 'cleared' && now - m.at > FORGET_CLEARED_AFTER_MS);
