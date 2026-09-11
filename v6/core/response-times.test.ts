// Case-for-case port of ResponseTimeTrackerTests.cs. Times are fixed at a local mid-afternoon so "today"
// cannot flip at midnight while the suite runs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dailyResponse, emptyResponseTimes, observe, responseStats, type ResponseTimes } from './response-times.ts';

const MIN = 60_000, HOUR = 60 * MIN, DAY = 24 * HOUR;
const NOW = new Date(2026, 7, 10, 15, 0).getTime();

// Every test inbound is recent-but-past, so watching is pinned to start well before them.
function tracker(...accounts: string[]): ResponseTimes {
  const state = emptyResponseTimes();
  for (const a of accounts) state.watchStart[a] = NOW - 30 * DAY;
  return state;
}

// One full waiting → answered cycle producing a sample of `minutes`, answered at `answeredAt`.
function record(state: ResponseTimes, account: string, chat: string, answeredAt: number, minutes: number) {
  observe(state, account, chat, true, false, answeredAt - minutes * MIN, NOW);
  observe(state, account, chat, false, true, answeredAt, NOW);
}

test('waiting then replied records the time between the two timestamps', () => {
  const state = tracker('inst-1');
  const inbound = NOW - 30 * MIN;
  observe(state, 'inst-1', 'chat-a', true, false, inbound, NOW);
  observe(state, 'inst-1', 'chat-a', false, true, inbound + 12 * MIN, NOW);
  const stats = responseStats(state, ['inst-1'], 15, { now: NOW });
  assert.equal(stats.hasData, true);
  assert.equal(stats.sampleCount, 1);
  assert.equal(stats.medianMinutes, 12);
  assert.equal(stats.slaPercent, 100);
});

test('SLA compliance counts only replies within the threshold', () => {
  const state = tracker('inst-1', 'inst-2');
  for (const [chat, minutes] of [['c1', 5], ['c2', 10], ['c3', 40]] as const) record(state, 'inst-1', chat, NOW - 2 * HOUR, minutes);
  const stats = responseStats(state, ['inst-1'], 15, { now: NOW });
  assert.equal(stats.sampleCount, 3);
  assert.equal(stats.medianMinutes, 10);
  assert.equal(stats.slaPercent, 67);
});

test('waiting with no reply records nothing', () => {
  const state = tracker('inst-1', 'inst-2');
  observe(state, 'inst-1', 'chat-a', true, false, NOW, NOW);
  const stats = responseStats(state, ['inst-1'], 15, { now: NOW });
  assert.equal(stats.hasData, false);
  assert.equal(stats.sampleCount, 0);
});

test('the earliest unanswered inbound is kept across repeat reads', () => {
  const state = tracker('inst-1', 'inst-2');
  const first = NOW - 60 * MIN;
  observe(state, 'inst-1', 'chat-a', true, false, first, NOW);
  observe(state, 'inst-1', 'chat-a', true, false, first + 20 * MIN, NOW); // must not overwrite
  observe(state, 'inst-1', 'chat-a', false, true, first + 45 * MIN, NOW);
  assert.equal(responseStats(state, ['inst-1'], 60, { now: NOW }).medianMinutes, 45);
});

test("answered today counts only today's replies", () => {
  const state = tracker('inst-1', 'inst-2');
  record(state, 'inst-1', 'c1', NOW - 10 * MIN, 5);
  record(state, 'inst-1', 'c2', NOW - 20 * MIN, 5);
  record(state, 'inst-1', 'c3', NOW - 3 * DAY, 5);
  const stats = responseStats(state, ['inst-1'], 15, { now: NOW });
  assert.equal(stats.answeredToday, 2);
  assert.equal(stats.sampleCount, 3);
});

test('stats are scoped to the accounts asked for', () => {
  const state = tracker('inst-1', 'inst-2');
  record(state, 'inst-1', 'c1', NOW - HOUR, 5);
  record(state, 'inst-2', 'c2', NOW - HOUR, 50);
  const onlyOne = responseStats(state, ['inst-1'], 15, { now: NOW });
  assert.equal(onlyOne.sampleCount, 1);
  assert.equal(onlyOne.medianMinutes, 5);
});

test('backlog from before watching began is excluded', () => {
  const state = emptyResponseTimes();
  state.watchStart['inst-1'] = NOW - HOUR;
  observe(state, 'inst-1', 'chat-old', true, false, NOW - 2 * DAY, NOW);
  observe(state, 'inst-1', 'chat-old', false, true, NOW, NOW);
  assert.equal(responseStats(state, ['inst-1'], 15, { now: NOW }).hasData, false);
});

test('daily series computes the percent within threshold per day and pads empty days', () => {
  const state = tracker('inst-1');
  const todayNoon = new Date(2026, 7, 10, 12).getTime(), yesterdayNoon = new Date(2026, 7, 9, 12).getTime();
  record(state, 'inst-1', 'a', todayNoon, 8);
  record(state, 'inst-1', 'b', todayNoon, 40);
  record(state, 'inst-1', 'c', yesterdayNoon, 5);
  const series = dailyResponse(state, ['inst-1'], 15, { days: 3, now: NOW });
  assert.deepEqual(series.map((p) => [p.percentWithin, p.count]), [[0, 0], [100, 1], [50, 2]]);
});
