// Case-for-case port of DataFreshnessTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { describeFreshness, STALE_AFTER_MS } from './freshness.ts';

const NOW = Date.UTC(2026, 7, 17, 12);

test('never captured says so and offers the fix', () => {
  const v = describeFreshness(null, NOW);
  assert.equal(v.hasData, false);
  assert.equal(v.isStale, true);
  assert.match(v.text, /re-sync/i);
});

test('the age is phrased the way a person would say it', () => {
  for (const [secondsAgo, expected] of [[10, 'just now'], [90, '1 minute ago'], [600, '10 minutes ago'], [5400, '1 hour ago'],
    [18000, '5 hours ago'], [129600, 'yesterday'], [432000, '5 days ago']] as const) {
    assert.ok(describeFreshness(NOW - secondsAgo * 1000, NOW).text.includes(expected), expected);
  }
});

test('fresh data is stamped but not flagged', () => {
  const v = describeFreshness(NOW - 2 * 60_000, NOW);
  assert.equal(v.isStale, false);
  assert.equal(v.hasData, true);
  assert.doesNotMatch(v.text, /re-sync/i);
});

test('old data is flagged and says what to do', () => {
  const v = describeFreshness(NOW - STALE_AFTER_MS, NOW);
  assert.equal(v.isStale, true);
  assert.match(v.text, /re-sync/i);
});

test('the threshold is well clear of the background poll', () => {
  assert.ok(STALE_AFTER_MS >= 10 * 60_000);
});

test('a capture in the future never reads as negative time', () => {
  const v = describeFreshness(NOW + 5 * 60_000, NOW);
  assert.ok(v.text.includes('just now'));
  assert.equal(v.isStale, false);
  assert.ok(!v.text.includes('-'));
});
