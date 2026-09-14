import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChatEntry } from './chat-entry.ts';
import { CALL_WINDOW_DAYS, callsIn, pruneCalls, recordCalls, type Calls } from './calls.ts';

const MIN = 60_000, HOUR = 60 * MIN, DAY = 24 * HOUR;
const NOW = new Date(2026, 8, 14, 15, 0).getTime();

const missed = (key: string, at: number, o: Partial<ChatEntry> = {}): ChatEntry => ({
  conversationKey: key, customerName: 'Caller', unread: 1, lastActivity: at, preview: '', awaiting: true, lastMessageFromMe: false,
  contactPhone: '', hasLastMessage: true, lastMessageType: 'call_log', lastCallOutcome: 'Missed', ...o,
});
const ours = (key: string, at: number, type = 'chat'): ChatEntry =>
  missed(key, at, { awaiting: false, lastMessageFromMe: true, lastMessageType: type, lastCallOutcome: '', unread: 0 });

test('a missed call is recorded once, however many reads see it', () => {
  const calls: Calls = {};
  assert.equal(recordCalls(calls, 'wa', [missed('a', NOW - 10 * MIN)], NOW), true);
  assert.equal(recordCalls(calls, 'wa', [missed('a', NOW - 10 * MIN)], NOW + MIN), false, 'nothing changed');
  assert.deepEqual(Object.values(calls).map((c) => [c.account, c.key, c.at, c.returnedAt]), [['wa', 'a', NOW - 10 * MIN, null]]);
});

test('an answered call, our own call and an ordinary message are not missed calls', () => {
  const calls: Calls = {};
  recordCalls(calls, 'wa', [
    missed('answered', NOW - MIN, { lastCallOutcome: 'Completed' }),
    ours('outgoing', NOW - MIN, 'call_log'),
    missed('text', NOW - MIN, { lastMessageType: 'chat', preview: 'hello', lastCallOutcome: '' }),
  ], NOW);
  assert.deepEqual(calls, {});
});

test('a reply from us after the call returns it, and says how and when', () => {
  const calls: Calls = {};
  recordCalls(calls, 'wa', [missed('a', NOW - 30 * MIN), missed('b', NOW - 20 * MIN)], NOW - 15 * MIN);
  recordCalls(calls, 'wa', [ours('a', NOW - 5 * MIN), ours('b', NOW - 2 * MIN, 'call_log')], NOW);
  const [b, a] = callsIn(calls, ['wa'], 0, NOW + 1); // newest call first
  assert.deepEqual([a.key, a.returnedAt, a.returnedBy], ['a', NOW - 5 * MIN, 'message']);
  assert.deepEqual([b.key, b.returnedAt, b.returnedBy], ['b', NOW - 2 * MIN, 'call']);
});

test('the customer writing again is not a return; our reply after that is', () => {
  const calls: Calls = {};
  recordCalls(calls, 'wa', [missed('a', NOW - 30 * MIN)], NOW - 29 * MIN);
  recordCalls(calls, 'wa', [missed('a', NOW - 20 * MIN, { lastMessageType: 'chat', preview: 'call me back please', lastCallOutcome: '' })], NOW - 19 * MIN);
  assert.equal(Object.values(calls)[0].returnedAt, null);
  recordCalls(calls, 'wa', [ours('a', NOW - 5 * MIN)], NOW);
  assert.equal(Object.values(calls)[0].returnedAt, NOW - 5 * MIN);
});

test('calling twice before a return is two calls, and one reply returns both', () => {
  const calls: Calls = {};
  recordCalls(calls, 'wa', [missed('a', NOW - 30 * MIN)], NOW - 29 * MIN);
  recordCalls(calls, 'wa', [missed('a', NOW - 10 * MIN)], NOW - 9 * MIN);
  recordCalls(calls, 'wa', [ours('a', NOW - MIN)], NOW);
  assert.deepEqual(callsIn(calls, ['wa'], 0, NOW + 1).map((c) => c.returnedAt), [NOW - MIN, NOW - MIN]);
});

test('a first read does not bring in calls older than the window, and old records are dropped', () => {
  const calls: Calls = {};
  recordCalls(calls, 'wa', [missed('old', NOW - (CALL_WINDOW_DAYS + 1) * DAY), missed('recent', NOW - DAY)], NOW);
  assert.deepEqual(Object.values(calls).map((c) => c.key), ['recent']);
  pruneCalls(calls, NOW + 40 * DAY);
  assert.deepEqual(calls, {});
});

test('the list for a range covers only those accounts and times, newest first, and keeps no names', () => {
  const calls: Calls = {};
  recordCalls(calls, 'wa', [missed('a', NOW - 3 * HOUR), missed('b', NOW - HOUR)], NOW);
  recordCalls(calls, 'other', [missed('c', NOW - HOUR)], NOW);
  assert.deepEqual(callsIn(calls, ['wa'], NOW - 2 * HOUR, NOW).map((c) => c.key), ['b']);
  assert.deepEqual(callsIn(calls, ['wa'], 0, NOW).map((c) => c.key), ['b', 'a']);
  assert.ok(!JSON.stringify(calls).includes('Caller'));
});
