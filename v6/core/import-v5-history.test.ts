// The half of the move that is easy to get wrong quietly: if the snoozed and handled chats do not come across,
// work the owner already dealt with reappears as waiting on their first morning in v6.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { importV5History } from './import-v5.ts';
import { isSuppressed } from './awaiting-overrides.ts';
import { awaitingChats } from './snapshot.ts';
import { responseStats } from './response-times.ts';

const NOW = Date.UTC(2026, 8, 12, 9);
const iso = (msAgo: number) => new Date(NOW - msAgo).toISOString();
const MIN = 60_000, DAY = 24 * 60 * MIN;
const judge = () => ({ now: NOW, overrides: {}, filterClosed: true });

const SNAPSHOT = {
  version: 1,
  instances: {
    'inst-1': {
      capturedAtUtc: iso(2 * MIN),
      chats: [
        { conversationKey: '923001234567@c.us', customerName: 'Ayesha', unread: 2, lastActivityUtc: iso(20 * MIN), preview: 'kitna charge hoga', isAwaiting: true, lastMessageFromMe: false, contactPhone: '923001234567', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '' },
        { conversationKey: '923009999999@c.us', customerName: 'Bilal', unread: 0, lastActivityUtc: iso(40 * MIN), preview: 'ok thanks', isAwaiting: false, lastMessageFromMe: true, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '' },
        { conversationKey: '120363000000@g.us', customerName: 'Staff group', unread: 5, lastActivityUtc: iso(10 * MIN), preview: 'rota', isAwaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '' },
        { conversationKey: 'bad-row@c.us', customerName: 'No timestamp', unread: 1, preview: 'hi', isAwaiting: true },
      ],
    },
  },
};

const TIMES = {
  version: 1,
  instances: {
    'inst-1': {
      samples: [{ answeredAtUtc: iso(3 * DAY), frtMinutes: 8 }, { answeredAtUtc: iso(DAY), frtMinutes: 22 },
        { answeredAtUtc: iso(200 * DAY), frtMinutes: 5 }, { answeredAtUtc: 'not-a-date', frtMinutes: 4 }],
      pending: { '923001234567@c.us': iso(20 * MIN), '': iso(MIN) },
      watchStartUtc: iso(60 * DAY),
    },
  },
};

const OVERRIDES = {
  version: 1,
  instances: {
    'inst-1': {
      'handled@c.us': { kind: 'Handled', handledForActivityUtc: iso(30 * MIN) },
      'snoozed@c.us': { kind: 'Snoozed', snoozeUntilUtc: new Date(NOW + 2 * 60 * MIN).toISOString() },
      'lapsed@c.us': { kind: 'Snoozed', snoozeUntilUtc: iso(5 * MIN) },
      'numbered@c.us': { kind: 0, handledForActivityUtc: iso(MIN) },
      'nonsense@c.us': { kind: 'Handled' },
    },
  },
};

const all = () => importV5History(['inst-1'], { snapshot: SNAPSHOT, responseTimes: TIMES, overrides: OVERRIDES }, NOW);

test('the chats on screen come across, minus what is not a customer', () => {
  const { snapshots, report } = all();
  assert.deepEqual(snapshots['inst-1'].chats.map((c) => c.customerName), ['Ayesha', 'Bilal']);
  assert.equal(snapshots['inst-1'].capturedAt, NOW - 2 * MIN);
  assert.equal(report.chats, 2);
  assert.equal(report.dropped >= 1, true); // the row with no timestamp
});

test('a handled chat stays handled, so yesterday\'s work does not come back as waiting', () => {
  const { overrides, report } = all();
  assert.equal(isSuppressed(overrides, 'inst-1', 'handled@c.us', NOW - 30 * MIN, NOW), true);
  // ...until the customer writes again, which is the whole point of the expiry
  assert.equal(isSuppressed(overrides, 'inst-1', 'handled@c.us', NOW - MIN, NOW), false);
  assert.equal(report.handled, 2); // the named one and the numbered one
});

test('a live snooze comes across and an elapsed one does not', () => {
  const { overrides, report } = all();
  assert.equal(isSuppressed(overrides, 'inst-1', 'snoozed@c.us', NOW, NOW), true);
  assert.equal(overrides['inst-1']['lapsed@c.us'], undefined);
  assert.equal(report.snoozed, 1);
  assert.match(report.notes.join(' '), /1 snooze\(s\) had already elapsed/);
});

test('reply-time history comes across, and the watch start with it', () => {
  const { times, report } = all();
  // The 200-day-old sample is past retention; the unparseable one is dropped and counted.
  assert.equal(report.samples, 2);
  assert.equal(times.watchStart['inst-1'], NOW - 60 * DAY);
  assert.deepEqual(Object.keys(times.pending['inst-1']), ['923001234567@c.us']);
  const stats = responseStats(times, ['inst-1'], 15, { now: NOW });
  assert.equal(stats.hasData, true);
  assert.equal(stats.sampleCount, 2);
  // v5's percentile rule, ported as it was: with an even count the median takes the lower sample.
  assert.equal(stats.medianMinutes, 8);
});

test('history for an account that did not come across is left behind, and said so', () => {
  const { snapshots, overrides, report } = importV5History(['kept'], {
    snapshot: { instances: { gone: { capturedAtUtc: iso(MIN), chats: [] } } },
    overrides: { instances: { gone: { 'c@c.us': { kind: 'Handled', handledForActivityUtc: iso(MIN) } } } },
  }, NOW);
  assert.deepEqual(Object.keys(snapshots), []);
  assert.deepEqual(Object.keys(overrides), []);
  assert.deepEqual(report.orphaned, ['gone']);
  assert.match(report.notes.join(' '), /did not come across was left behind/);
});

test('an imported snapshot does not arrive pre-closed by a cold scan', () => {
  // Nine of ten chats claiming "no last message" is a cold scan, not nine deleted conversations.
  const chats = Array.from({ length: 10 }, (_, i) => ({
    conversationKey: `92300000000${i}@c.us`, customerName: `C${i}`, unread: 1, lastActivityUtc: iso(30 * DAY),
    preview: '', isAwaiting: true, lastMessageFromMe: false, hasLastMessage: i === 0, lastMessageType: 'chat',
  }));
  const { snapshots } = importV5History(['inst-1'], { snapshot: { instances: { 'inst-1': { capturedAtUtc: iso(MIN), chats } } } }, NOW);
  assert.equal(awaitingChats(snapshots, 'inst-1', judge()).length, 10);
});

test('nothing to import is an empty set of stores, not a crash', () => {
  const { snapshots, times, overrides, report } = importV5History([], {}, NOW);
  assert.deepEqual(snapshots, {});
  assert.deepEqual(overrides, {});
  assert.deepEqual(times.samples, {});
  assert.equal(report.chats, 0);
  assert.deepEqual(report.orphaned, []);
});
