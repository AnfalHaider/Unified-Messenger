// Day keys follow the machine's local calendar. This machine runs at UTC+5 with no DST, so every test here pins
// New York, whose clocks change; node --test runs each file in its own process, so the zone stays put.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChatEntry } from './chat-entry.ts';
import { dayKey, HISTORY_DAYS, recordHistory, type History, type RecordContext } from './history.ts';
import type { Sample } from './response-times.ts';

process.env.TZ = 'America/New_York';

const MIN = 60_000, HOUR = 60 * MIN;
const local = (y: number, m: number, d: number, h = 0, min = 0) => new Date(y, m - 1, d, h, min).getTime();
const NOW = local(2026, 9, 14, 15);

const chat = (key: string, o: Partial<ChatEntry> = {}): ChatEntry => ({
  conversationKey: key, customerName: 'Customer', unread: 0, lastActivity: NOW - 10 * MIN, preview: 'Is Friday free?',
  awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '', ...o,
});
const ctx = (o: Partial<RecordContext> = {}): RecordContext => ({ now: NOW, samples: [], targetMinutes: 15, waitingOverADay: 0, ...o });
const today = (h: History, account = 'acct') => h[account].days.find((d) => d.day === dayKey(NOW))!;

test('the zone really is applied, so the day tests below do not pass vacuously', () => {
  assert.equal(new Date(NOW).getTimezoneOffset(), 240);
});

test('a customer who writes twice in a day is one customer, and a new day counts again', () => {
  const h: History = {};
  recordHistory(h, 'acct', undefined, [chat('a', { lastActivity: NOW - 2 * HOUR })], ctx({ now: NOW - 2 * HOUR }));
  recordHistory(h, 'acct', undefined, [chat('a', { lastActivity: NOW - MIN }), chat('b', { lastActivity: NOW - MIN })], ctx());
  assert.equal(today(h).customersWrote, 2);

  const tomorrow = local(2026, 9, 15, 10);
  recordHistory(h, 'acct', undefined, [chat('a', { lastActivity: tomorrow - MIN })], ctx({ now: tomorrow }));
  assert.deepEqual(h.acct.days.map((d) => [d.day, d.customersWrote]), [['2026-09-14', 2], ['2026-09-15', 1]]);
});

test('only what happened after watching began is counted: the first read\'s backlog is not today\'s traffic', () => {
  const h: History = {};
  recordHistory(h, 'acct', undefined, [chat('old', { lastActivity: NOW - 3 * HOUR }), chat('older', { lastActivity: NOW - 30 * HOUR })], ctx());
  assert.equal(today(h).customersWrote, 0);
  assert.equal(h.acct.days.length, 1, 'no records are invented for days before watching began');
});

test('a message written while the app was closed still lands on its own day', () => {
  const h: History = {};
  const evening = local(2026, 9, 13, 20);
  recordHistory(h, 'acct', undefined, [], ctx({ now: evening }));
  recordHistory(h, 'acct', undefined, [chat('night', { lastActivity: local(2026, 9, 13, 23, 30) }), chat('morning', { lastActivity: local(2026, 9, 14, 0, 30) })], ctx());
  assert.deepEqual(h.acct.days.map((d) => [d.day, d.customersWrote]), [['2026-09-13', 1], ['2026-09-14', 1]]);
});

test('the day follows the local calendar across the autumn clock change', () => {
  const h: History = {};
  const start = local(2026, 10, 31, 12);
  recordHistory(h, 'acct', undefined, [], ctx({ now: start }));
  // 1 November is 25 hours long in New York: 23:30 on the 1st is still the 1st.
  const lateOnFirst = local(2026, 11, 1, 23, 30);
  recordHistory(h, 'acct', undefined, [chat('a', { lastActivity: lateOnFirst })], ctx({ now: lateOnFirst + MIN }));
  assert.deepEqual(h.acct.days.map((d) => d.day), ['2026-10-31', '2026-11-01']);
});

test('reply figures for a day come from the replies measured that day', () => {
  const h: History = {};
  const samples: Sample[] = [
    { answeredAt: NOW - HOUR, minutes: 5 }, { answeredAt: NOW - 2 * HOUR, minutes: 12 }, { answeredAt: NOW - 3 * HOUR, minutes: 40 },
    { answeredAt: NOW - 30 * HOUR, minutes: 99 },
  ];
  recordHistory(h, 'acct', undefined, [], ctx({ samples }));
  const d = today(h);
  assert.deepEqual([d.replies, d.medianReplyMinutes, d.repliesWithinTarget, d.targetMinutes], [3, 12, 2, 15]);
});

test('a day with no replies says so rather than reporting a zero-minute median', () => {
  const h: History = {};
  recordHistory(h, 'acct', undefined, [], ctx());
  assert.equal(today(h).medianReplyMinutes, null);
});

test('waiting over a day is counted at the first read of the day only', () => {
  const h: History = {};
  recordHistory(h, 'acct', undefined, [], ctx({ now: NOW - 5 * HOUR, waitingOverADay: 7 }));
  recordHistory(h, 'acct', undefined, [], ctx({ waitingOverADay: 2 }));
  assert.equal(today(h).waitingOverADayAtFirstRead, 7);
});

test('a chat answered and then waiting again is reopened, once per customer per day', () => {
  const h: History = {};
  recordHistory(h, 'acct', undefined, [], ctx({ now: NOW - HOUR }));
  const answered = [chat('a', { awaiting: false, lastMessageFromMe: true, lastActivity: NOW - 30 * MIN })];
  const again = [chat('a', { lastActivity: NOW - 10 * MIN })];
  recordHistory(h, 'acct', answered, again, ctx());
  recordHistory(h, 'acct', again, [chat('a', { lastActivity: NOW - 5 * MIN })], ctx());
  recordHistory(h, 'acct', answered, again, ctx());
  assert.equal(today(h).reopened, 1);
  // Waiting before and waiting after is not a reopening.
  recordHistory(h, 'acct', [chat('b', { lastActivity: NOW - 20 * MIN })], [chat('b', { lastActivity: NOW - 2 * MIN })], ctx());
  assert.equal(today(h).reopened, 1);
});

test('a missed call counts once; an answered call and our own call do not', () => {
  const h: History = {};
  recordHistory(h, 'acct', undefined, [], ctx({ now: NOW - HOUR }));
  const calls = [
    chat('missed', { lastMessageType: 'call_log', preview: '', lastCallOutcome: 'Missed' }),
    chat('answered', { lastMessageType: 'call_log', preview: '', lastCallOutcome: 'Completed' }),
    chat('ours', { lastMessageType: 'call_log', preview: '', lastMessageFromMe: true, awaiting: false }),
  ];
  recordHistory(h, 'acct', undefined, calls, ctx());
  recordHistory(h, 'acct', undefined, calls, ctx());
  assert.equal(today(h).missedCalls, 1);
});

test('a week of history survives a save and reload, and keeps counting without double counting', () => {
  const h: History = {};
  for (let back = 7; back >= 0; back--) {
    const at = local(2026, 9, 14 - back, 12);
    recordHistory(h, 'acct', undefined, back === 7 ? [] : [chat(`c${back}`, { lastActivity: at - MIN })], ctx({ now: at }));
  }
  const reloaded: History = JSON.parse(JSON.stringify(h));
  assert.equal(reloaded.acct.days.length, 8);
  recordHistory(reloaded, 'acct', undefined, [chat('c0', { lastActivity: local(2026, 9, 14, 12) - MIN })], ctx({ now: local(2026, 9, 14, 13) }));
  assert.equal(reloaded.acct.days.at(-1)!.customersWrote, 1);
});

test('records older than the retention are dropped, and identities are kept only while they can still matter', () => {
  const h: History = {};
  const longAgo = local(2025, 8, 1, 12);
  recordHistory(h, 'acct', undefined, [], ctx({ now: longAgo }));
  recordHistory(h, 'acct', undefined, [chat('a', { lastActivity: NOW - MIN })], ctx());
  assert.deepEqual(h.acct.days.map((d) => d.day), [dayKey(NOW)]);
  assert.ok(HISTORY_DAYS >= 400);
  assert.deepEqual(Object.keys(h.acct.seen), [dayKey(NOW)]);
});
