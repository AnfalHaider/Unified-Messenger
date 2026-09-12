// What each day looked like, per account, for the reports and the morning digest. A snapshot holds only the
// latest read, so anything a report says about last Tuesday has to be written down on the day. The app layer
// calls recordHistory after every read and saves the object as JSON.
//
// Only what the app saw happen after it began watching an account is counted: the chats already on screen at the
// first read are backlog, not that day's traffic. Days are local calendar days, keyed "YYYY-MM-DD".
import type { ChatEntry } from './chat-entry.ts';
import { startOfDay } from './days.ts';
import { classify } from './reply-need.ts';
import type { Sample } from './response-times.ts';

export const HISTORY_DAYS = 400;

export interface DayRecord {
  day: string;
  /** Customers whose own message or call the app saw that day, each counted once. */
  customersWrote: number;
  /** First replies measured that day, their median, and how many were within the target in force then. */
  replies: number;
  medianReplyMinutes: number | null;
  repliesWithinTarget: number;
  targetMinutes: number;
  /** Customers waiting more than a day when the day's first read happened. Null until that read. */
  waitingOverADayAtFirstRead: number | null;
  /** Customers who wrote again after being answered. */
  reopened: number;
  missedCalls: number;
}

export interface AccountHistory {
  /** When recording began for this account. Earlier activity is never counted. */
  watchStart: number;
  /** Oldest first. */
  days: DayRecord[];
  /** day → who was already counted, so a chat seen on every read counts once. Kept for today and yesterday only. */
  seen: Record<string, { wrote?: string[]; reopened?: string[]; calls?: string[] }>;
}

/** accountId → its history. */
export type History = Record<string, AccountHistory>;

export interface RecordContext {
  now: number;
  /** The account's measured first replies (response-times samples). */
  samples: Sample[];
  targetMinutes: number;
  /** Customers waiting more than a day right now, by the same rule as the line. Used at the day's first read. */
  waitingOverADay: number;
}

const pad = (n: number) => String(n).padStart(2, '0');
export const dayKey = (at: number) => { const d = new Date(at); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`; };

const emptyDay = (day: string, targetMinutes: number): DayRecord => ({
  day, customersWrote: 0, replies: 0, medianReplyMinutes: null, repliesWithinTarget: 0, targetMinutes,
  waitingOverADayAtFirstRead: null, reopened: 0, missedCalls: 0,
});

/** Call after every read with the chats before and after it. Never throws on odd input. */
export function recordHistory(history: History, account: string, prior: ChatEntry[] | undefined, chats: ChatEntry[], ctx: RecordContext) {
  const id = account.trim();
  if (!id) return;
  const { now, targetMinutes } = ctx;
  const h = (history[id] ??= { watchStart: now, days: [], seen: {} });
  const yesterday = dayKey(startOfDay(now, 1));

  const dayOf = (key: string) => {
    let d = h.days.find((x) => x.day === key);
    if (!d) { d = emptyDay(key, targetMinutes); h.days.push(d); h.days.sort((a, b) => a.day.localeCompare(b.day)); }
    return d;
  };
  /** Adds `who` to the day's list once and returns the day to count on, or null when already counted, from before
   *  watching began, or older than yesterday (past recounting). */
  const firstTime = (at: number, list: 'wrote' | 'reopened' | 'calls', who: string) => {
    const key = dayKey(at);
    if (at < h.watchStart || key < yesterday) return null;
    const seen = ((h.seen[key] ??= {})[list] ??= []);
    if (seen.includes(who)) return null;
    seen.push(who);
    return dayOf(key);
  };

  const todayRecord = dayOf(dayKey(now));
  if (todayRecord.waitingOverADayAtFirstRead === null) todayRecord.waitingOverADayAtFirstRead = ctx.waitingOverADay;

  const before = new Map(prior?.map((c) => [c.conversationKey, c]));
  for (const c of chats) {
    if (c.lastMessageFromMe) continue;
    const call = classify({ preview: c.preview, type: c.lastMessageType, fromMe: false, callOutcome: c.lastCallOutcome }).reason;
    if (call === 'missedCall') { const d = firstTime(c.lastActivity, 'calls', `${c.conversationKey}:${c.lastActivity}`); if (d) d.missedCalls++; }
    const d = firstTime(c.lastActivity, 'wrote', c.conversationKey);
    if (d) d.customersWrote++;
    const was = before.get(c.conversationKey);
    if (c.awaiting && was?.lastMessageFromMe && c.lastActivity > was.lastActivity) {
      const r = firstTime(c.lastActivity, 'reopened', c.conversationKey);
      if (r) r.reopened++;
    }
  }

  // Reply figures are recomputed while the day can still change: today, and yesterday until its last late read.
  for (const d of h.days) {
    if (d.day < yesterday) continue;
    const minutes = ctx.samples.filter((s) => dayKey(s.answeredAt) === d.day).map((s) => s.minutes).sort((a, b) => a - b);
    d.replies = minutes.length;
    d.medianReplyMinutes = minutes.length ? minutes[Math.ceil(minutes.length / 2) - 1] : null;
    d.repliesWithinTarget = minutes.filter((m) => m <= targetMinutes).length;
    d.targetMinutes = targetMinutes;
  }

  const oldest = dayKey(startOfDay(now, HISTORY_DAYS));
  h.days = h.days.filter((d) => d.day >= oldest);
  for (const key of Object.keys(h.seen)) if (key < yesterday) delete h.seen[key];
}
