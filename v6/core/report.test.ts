// Reports read the day records and the measured replies. Pinned to New York, like history.test.ts, so day keys
// are exercised in a zone with clock changes rather than passing vacuously at UTC+5.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dayKey, type DayRecord, type History } from './history.ts';
import { buildReport, REPLY_BANDS, type ReportAccount } from './report.ts';
import { emptyResponseTimes, type ResponseTimes } from './response-times.ts';

process.env.TZ = 'America/New_York';

const MIN = 60_000, HOUR = 60 * MIN;
const local = (y: number, m: number, d: number, h = 12) => new Date(y, m - 1, d, h).getTime();
const NOW = local(2026, 9, 14, 15); // a Monday

const accounts: ReportAccount[] = [
  { id: 'wa-f', name: 'F WhatsApp', location: 'F', targetMinutes: 15 },
  { id: 'ig-f', name: 'F Instagram', location: 'F', targetMinutes: 15 },
  { id: 'wa-d', name: 'D WhatsApp', location: 'D', targetMinutes: 30 },
];

const day = (at: number, o: Partial<DayRecord> = {}): DayRecord => ({
  day: dayKey(at), customersWrote: 0, wroteByHour: Array(24).fill(0), replies: 0, medianReplyMinutes: null,
  repliesWithinTarget: 0, targetMinutes: 15, waitingOverADayAtFirstRead: null, reopened: 0, missedCalls: 0, ...o,
});
const account = (days: DayRecord[], watchStart = local(2026, 8, 1)) => ({ watchStart, days, seen: {} });
const reply = (times: ResponseTimes, id: string, at: number, minutes: number) => ((times.samples[id] ??= []).push({ answeredAt: at, minutes }));

test('the zone really is applied', () => {
  assert.equal(new Date(NOW).getTimezoneOffset(), 240);
});

test('nothing recorded says so, instead of reporting zeroes as a quiet week', () => {
  const r = buildReport({}, emptyResponseTimes(), accounts, 7, NOW);
  assert.equal(r.hasData, false);
  assert.equal(r.totals.onTimePercent, null);
  assert.equal(r.totals.medianMinutes, null);
  assert.equal(r.days.length, 7);
});

test('a range covers whole local days ending today, oldest first', () => {
  const r = buildReport({}, emptyResponseTimes(), accounts, 7, NOW);
  assert.deepEqual([r.days[0].day, r.days[6].day], ['2026-09-08', '2026-09-14']);
  assert.deepEqual(buildReport({}, emptyResponseTimes(), accounts, 1, NOW).days.map((d) => d.day), ['2026-09-14']);
});

test('reply figures come from the replies measured in the range, each against its own account\'s target', () => {
  const times = emptyResponseTimes();
  reply(times, 'wa-f', NOW - HOUR, 10);        // within 15
  reply(times, 'ig-f', NOW - 2 * HOUR, 20);    // past 15
  reply(times, 'wa-d', NOW - 3 * HOUR, 20);    // within 30
  reply(times, 'wa-d', local(2026, 9, 1), 5);  // before the range
  const r = buildReport({}, times, accounts, 7, NOW);
  assert.deepEqual([r.totals.replies, r.totals.withinTarget, r.totals.medianMinutes, r.totals.onTimePercent], [3, 2, 20, 67]);
  assert.equal(r.hasData, true);
});

test('customers, reopened and missed calls add up across accounts and days; the backlog is the latest morning\'s', () => {
  const history: History = {
    'wa-f': account([day(local(2026, 9, 13), { customersWrote: 5, reopened: 1, missedCalls: 2, waitingOverADayAtFirstRead: 9 }), day(NOW, { customersWrote: 3, waitingOverADayAtFirstRead: 7 })]),
    'wa-d': account([day(NOW, { customersWrote: 2, reopened: 2, waitingOverADayAtFirstRead: 4 })]),
    'gone': account([day(NOW, { customersWrote: 100 })]),
  };
  const r = buildReport(history, emptyResponseTimes(), accounts, 7, NOW);
  assert.deepEqual([r.totals.customersWrote, r.totals.reopened, r.totals.missedCalls, r.totals.waitingOverADay], [10, 3, 2, 11]);
  assert.deepEqual(r.days.map((d) => d.waitingOverADay).slice(-2), [9, 11]);
  assert.equal(r.days[0].waitingOverADay, null, 'a day with no morning read is a gap');
});

test('the previous period is the same length just before, for "up" or "down" notes', () => {
  const times = emptyResponseTimes();
  reply(times, 'wa-f', local(2026, 9, 3), 30);
  reply(times, 'wa-f', local(2026, 9, 12), 5);
  const history: History = { 'wa-f': account([day(local(2026, 9, 3), { customersWrote: 4 }), day(local(2026, 9, 12), { customersWrote: 6 })]) };
  const r = buildReport(history, times, accounts, 7, NOW);
  assert.deepEqual([r.totals.customersWrote, r.totals.onTimePercent], [6, 100]);
  assert.deepEqual([r.previous.customersWrote, r.previous.onTimePercent], [4, 0]);
});

test('by location, on time per day with gaps where nothing was answered', () => {
  const times = emptyResponseTimes();
  reply(times, 'wa-f', NOW - HOUR, 10);
  reply(times, 'ig-f', NOW - HOUR, 40);
  reply(times, 'wa-d', local(2026, 9, 13), 10);
  const r = buildReport({}, times, accounts, 7, NOW);
  const f = r.byLocation.find((l) => l.name === 'F')!;
  assert.deepEqual([f.replies, f.onTimePercent, f.daily.at(-1), f.daily.at(-2)], [2, 50, 50, null]);
  assert.deepEqual(r.byLocation.map((l) => l.name), ['F', 'D']);
});

test('by account, the busiest first, with the slowest one in ten', () => {
  const times = emptyResponseTimes();
  for (const m of [1, 2, 3, 4, 5, 6, 7, 8, 9, 50]) reply(times, 'wa-d', NOW - HOUR, m);
  reply(times, 'ig-f', NOW - HOUR, 12);
  const r = buildReport({}, times, accounts, 7, NOW);
  assert.deepEqual(r.byAccount.map((a) => [a.id, a.replies, a.medianMinutes, a.p90Minutes]), [['wa-d', 10, 5, 9], ['ig-f', 1, 12, 12], ['wa-f', 0, null, null]]);
});

test('replies fall into the time bands, the last band open-ended', () => {
  const times = emptyResponseTimes();
  for (const m of [0.5, 5, 7, 14.9, 16, 45, 90, 500]) reply(times, 'wa-f', NOW - HOUR, m);
  const r = buildReport({}, times, accounts, 7, NOW);
  assert.deepEqual(r.bands.map(([, n]) => n), [2, 1, 1, 1, 1, 1, 1]);
  assert.equal(r.bands.length, REPLY_BANDS.length);
});

test('busy hours average each weekday over the recorded days, Monday first', () => {
  const byHour = (h: number, n: number) => Array.from({ length: 24 }, (_, i) => (i === h ? n : 0));
  const history: History = {
    'wa-f': account([day(local(2026, 9, 7), { wroteByHour: byHour(13, 4) }), day(NOW, { wroteByHour: byHour(13, 2) })]),
    'wa-d': account([day(NOW, { wroteByHour: byHour(13, 4) })]),
  };
  const r = buildReport(history, emptyResponseTimes(), accounts, 30, NOW);
  // Two Mondays recorded: (4 + (2 + 4)) / 2.
  assert.equal(r.busy[0][13], 5);
  assert.equal(r.busy[6][13], 0);
});

test('the report says from when it knows, so a short history is not read as a full range', () => {
  const history: History = { 'wa-f': account([day(NOW)], local(2026, 9, 12, 9)) };
  const r = buildReport(history, emptyResponseTimes(), accounts, 30, NOW);
  assert.equal(r.recordingSince, local(2026, 9, 12, 9));
  assert.equal(r.daysRecorded, 3);
});

test('missed calls per location, day by day', () => {
  const history: History = {
    'wa-f': account([day(local(2026, 9, 13), { missedCalls: 2 }), day(NOW, { missedCalls: 1 })]),
    'ig-f': account([day(NOW, { missedCalls: 1 })]),
    'wa-d': account([day(NOW, { missedCalls: 4 })]),
  };
  const r = buildReport(history, emptyResponseTimes(), accounts, 7, NOW);
  const f = r.byLocation.find((l) => l.name === 'F')!;
  assert.deepEqual([f.missedCalls, f.missedDaily.slice(-2)], [4, [2, 2]]);
  assert.equal(r.byLocation.find((l) => l.name === 'D')!.missedCalls, 4);
});
