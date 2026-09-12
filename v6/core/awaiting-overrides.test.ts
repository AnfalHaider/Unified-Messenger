// Case-for-case port of AwaitingOverrideStoreTests.cs, plus the pruning v5 did inside its save.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { clear, isSuppressed, markHandled, pruneExpired, snooze, type Overrides } from './awaiting-overrides.ts';

const MIN = 60_000, NOW = Date.UTC(2026, 7, 10, 9);

test('mark handled suppresses until a newer message arrives', () => {
  const o: Overrides = {};
  markHandled(o, 'inst-1', 'chat-a', NOW - 5 * MIN);
  assert.equal(isSuppressed(o, 'inst-1', 'chat-a', NOW - 5 * MIN, NOW), true);
  assert.equal(isSuppressed(o, 'inst-1', 'chat-a', NOW + 5 * MIN, NOW), false);
});

test('snooze suppresses until the time passes', () => {
  const o: Overrides = {};
  snooze(o, 'inst-1', 'chat-a', NOW + 60 * MIN);
  assert.equal(isSuppressed(o, 'inst-1', 'chat-a', NOW - MIN, NOW), true);
  assert.equal(isSuppressed(o, 'inst-1', 'chat-a', NOW - MIN, NOW + 120 * MIN), false);
});

test('clear removes the override', () => {
  const o: Overrides = {};
  markHandled(o, 'inst-1', 'chat-a', NOW);
  assert.equal(isSuppressed(o, 'inst-1', 'chat-a', NOW, NOW), true);
  clear(o, 'inst-1', 'chat-a');
  assert.equal(isSuppressed(o, 'inst-1', 'chat-a', NOW, NOW), false);
});

test('an unknown chat is not suppressed', () => {
  assert.equal(isSuppressed({}, 'inst-1', 'nope', NOW, NOW), false);
});

test('pruning drops elapsed snoozes and keeps handled chats', () => {
  const o: Overrides = {};
  snooze(o, 'inst-1', 'lapsed', NOW - MIN);
  snooze(o, 'inst-2', 'lapsed', NOW - MIN);
  markHandled(o, 'inst-1', 'handled', NOW - 90 * 24 * 60 * MIN);
  pruneExpired(o, NOW);
  assert.deepEqual(Object.keys(o), ['inst-1']);
  assert.deepEqual(Object.keys(o['inst-1']), ['handled']);
});

test('a mark remembers when it was made, and one without a date still works', () => {
  const o: Overrides = {};
  markHandled(o, 'inst-1', 'dated', NOW - MIN, NOW);
  snooze(o, 'inst-1', 'snoozed', NOW + MIN, NOW);
  markHandled(o, 'inst-1', 'imported', NOW - MIN);
  assert.equal(o['inst-1'].dated.at, NOW);
  assert.equal(o['inst-1'].snoozed.at, NOW);
  assert.equal('at' in o['inst-1'].imported, false);
  assert.equal(isSuppressed(o, 'inst-1', 'imported', NOW - MIN, NOW), true);
});
