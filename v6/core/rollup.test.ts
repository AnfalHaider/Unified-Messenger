// Port of OversightRollupBuilderTests.cs onto snapshot data. v5's thread-only cases (urgency scores, the GUID
// location key, pre-connect backfill) have no v6 source; their intent is kept where one exists: worst-first
// order, location aggregation, all-stale, the syncing state, and the summary line.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChatEntry } from './chat-entry.ts';
import { buildRollup, type RollupAccount, type RollupOptions } from './rollup.ts';
import type { Judge, Snapshots } from './snapshot.ts';

const MIN = 60_000, HOUR = 60 * MIN, DAY = 24 * HOUR;
const NOW = new Date(2026, 7, 11, 12, 0).getTime(); // Tuesday noon, local

const chat = (key: string, o: Partial<ChatEntry>): ChatEntry => ({
  conversationKey: key, customerName: 'Customer', unread: 0, lastActivity: NOW, preview: '', awaiting: false,
  lastMessageFromMe: false, contactPhone: '', hasLastMessage: null, lastMessageType: '', lastCallOutcome: '', ...o,
});
const waitingFor = (key: string, minutes: number, now = NOW) => chat(key, { awaiting: true, preview: 'kitna charge hoga', lastActivity: now - minutes * MIN });
const repliedAgo = (key: string, minutes: number) => chat(key, { lastMessageFromMe: true, preview: 'ok', lastActivity: NOW - minutes * MIN });
const acct = (id: string, location?: string, o: Partial<RollupAccount> = {}): RollupAccount => ({ id, name: `${id.toUpperCase()} acct`, location, supportsTiming: true, ...o });
const judge = (now = NOW): Judge => ({ now, overrides: {}, filterClosed: true });
const snaps = (byAccount: Record<string, ChatEntry[]>): Snapshots =>
  Object.fromEntries(Object.entries(byAccount).map(([id, chats]) => [id, { capturedAt: NOW, chats }]));
const rollup = (accounts: RollupAccount[], s: Snapshots, o: Partial<RollupOptions> = {}, j = judge()) =>
  buildRollup(accounts, s, j, { groupBy: 'account', slaMinutes: 15, ...o });

const SAMPLE_ACCOUNTS = [acct('a', 'F-11'), acct('b', 'F-11'), acct('c', 'DHA')];
const SAMPLE = snaps({
  a: [repliedAgo('a1', 5), waitingFor('a2', 5)],
  b: [waitingFor('b1', 100)],
  c: [waitingFor('c1', 100), waitingFor('c2', 100)],
});

test('by account: counts, and sorts worst first', () => {
  const r = rollup(SAMPLE_ACCOUNTS, SAMPLE);
  assert.deepEqual(r.entities.map((e) => e.key), ['c', 'b', 'a']);
  assert.equal(r.totalPastTarget, 3);
  assert.equal(r.worstKey, 'c');
  assert.equal(r.summary, '3 customers need a reply now — most urgent at C acct');
  const [c, b, a] = r.entities;
  assert.deepEqual([a.name, a.measured, a.awaiting, a.onTimePercent, a.pastTarget], ['A acct', 2, 1, 50, 0]);
  assert.deepEqual([b.onTimePercent, b.pastTarget, b.atRisk], [0, 1, 1]);
  assert.deepEqual([c.pastTarget, c.atRisk], [2, 2]);
});

test('by location: aggregates accounts and computes on-time', () => {
  const r = rollup(SAMPLE_ACCOUNTS, SAMPLE, { groupBy: 'location' });
  assert.deepEqual(r.entities.map((e) => e.key), ['DHA', 'F-11']);
  const f11 = r.entities[1];
  assert.equal(f11.kind, 'location');
  assert.deepEqual(f11.accountIds, ['a', 'b']);
  assert.deepEqual([f11.measured, f11.awaiting, f11.onTimePercent, f11.pastTarget, f11.atRisk], [3, 2, 33, 1, 1]);
});

test('an account with no location stands alone under its own name', () => {
  const r = rollup([acct('solo')], snaps({ solo: [repliedAgo('x', 5)] }), { groupBy: 'location' });
  assert.deepEqual(r.entities.map((e) => [e.key, e.kind, e.accountIds]), [['SOLO acct', 'location', ['solo']]]);
});

test('all replied says caught up', () => {
  const r = rollup([acct('a')], snaps({ a: [repliedAgo('x', 5)] }));
  assert.equal(r.totalPastTarget, 0);
  assert.equal(r.worstKey, null);
  assert.equal(r.summary, 'All caught up.');
});

test('customers waiting inside the target are named, not called caught up', () => {
  const r = rollup([acct('a')], snaps({ a: [waitingFor('x', 5)] }));
  assert.equal(r.summary, '1 customer waiting');
  assert.equal(r.worstKey, 'a');
});

test('one customer past target reads in the singular', () => {
  assert.equal(rollup([acct('a')], snaps({ a: [waitingFor('x', 20)] })).summary, '1 customer needs a reply now — most urgent at A acct');
});

test('an account with no read shows syncing, with no figures and no trend', () => {
  const r = rollup([acct('a')], {});
  const [a] = r.entities;
  assert.deepEqual([a.hasChatData, a.measured, a.awaiting, a.onTimePercent, a.trend, a.lastActivity], [false, 0, 0, 100, [], null]);
  assert.equal(r.summary, 'Nothing has been read yet.');
});

test('the window scopes caught-up chats while waiting ones always count', () => {
  const s = snaps({ a: [repliedAgo('today', 60), repliedAgo('old', 3 * 24 * 60), waitingFor('old-wait', 3 * 24 * 60)] });
  const [a] = rollup([acct('a')], s, { from: NOW - 6 * HOUR }).entities;
  assert.deepEqual([a.measured, a.awaiting], [2, 1]);
});

test('stale and signed out need every member; read failed needs only one', () => {
  const s = snaps({ a: [], b: [] });
  const mixed = rollup([acct('a', 'L', { stale: true, signedOut: true, readFailed: true }), acct('b', 'L')], s, { groupBy: 'location' }).entities[0];
  assert.deepEqual([mixed.stale, mixed.signedOut, mixed.readFailed], [false, false, true]);
  const all = rollup([acct('a', 'L', { stale: true, signedOut: true }), acct('b', 'L', { stale: true, signedOut: true })], s, { groupBy: 'location' }).entities[0];
  assert.deepEqual([all.stale, all.signedOut, all.readFailed], [true, true, false]);
});

test('the target clock pauses outside business hours', () => {
  const mondayClose = new Date(2026, 7, 10, 17, 55).getTime(), tuesdayOpen = new Date(2026, 7, 11, 9, 5).getTime();
  const s = snaps({ a: [chat('x', { awaiting: true, preview: 'kitna charge hoga', lastActivity: mondayClose })] });
  const hours = { enabled: true, openMinutes: 9 * 60, closeMinutes: 18 * 60, workingDays: [1, 2, 3, 4, 5, 6] };
  assert.equal(rollup([acct('a', 'F-11')], s, { locations: { 'F-11': { hours } } }, judge(tuesdayOpen)).totalPastTarget, 0);
  assert.equal(rollup([acct('a', 'F-11')], s, {}, judge(tuesdayOpen)).totalPastTarget, 1);
});

test("a location's own target overrides the global one, within limits", () => {
  const s = snaps({ a: [waitingFor('x', 40)] });
  assert.deepEqual([rollup([acct('a', 'F-11')], s).totalPastTarget, rollup([acct('a', 'F-11')], s).totalAtRisk], [1, 1]);
  const own = rollup([acct('a', 'F-11')], s, { locations: { 'F-11': { slaMinutes: 60 } } });
  assert.deepEqual([own.totalPastTarget, own.totalAtRisk], [0, 0]);
  const tiny = snaps({ a: [waitingFor('four', 4), waitingFor('six', 6)] });
  assert.equal(rollup([acct('a', 'F-11')], tiny, { locations: { 'F-11': { slaMinutes: 1 } } }).totalPastTarget, 1);
});

test('a channel that cannot time replies is never scored past target', () => {
  const s = snaps({ a: [waitingFor('x', 100)], b: [waitingFor('y', 100)] });
  const untimed = rollup([acct('a', 'L', { supportsTiming: false })], s, { groupBy: 'location' }).entities[0];
  assert.deepEqual([untimed.pastTarget, untimed.supportsTiming, untimed.awaiting], [0, false, 1]);
  const mixed = rollup([acct('a', 'L', { supportsTiming: false }), acct('b', 'L')], s, { groupBy: 'location' }).entities[0];
  assert.deepEqual([mixed.pastTarget, mixed.supportsTiming], [1, true]);
});

test('the trend buckets chats by local day over the last seven days', () => {
  const s = snaps({ a: [repliedAgo('today', 0), repliedAgo('three', 3 * 24 * 60), repliedAgo('eight', 8 * 24 * 60)] });
  const [a] = rollup([acct('a')], s).entities;
  assert.deepEqual(a.trend, [0, 0, 0, 1, 0, 0, 1]);
  assert.equal(a.lastActivity, NOW);
  assert.equal(DAY, 24 * HOUR);
});
