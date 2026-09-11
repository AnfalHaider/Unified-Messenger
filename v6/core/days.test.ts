// Port of LocalDayBoundaryTests.cs. This machine runs at UTC+5 with no DST, so a test in the machine zone
// would pass vacuously. These pin zones that transition: New York at 02:00, Havana at midnight itself.
// Node applies a TZ change immediately, and node --test runs each file in its own process.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { calendarDaysBetween, endOfDayExclusive, startOfDay } from './days.ts';

const HOUR = 3_600_000;
const iso = (ms: number) => new Date(ms).toISOString();
const local = (y: number, m: number, d: number, h = 0, min = 0) => new Date(y, m - 1, d, h, min).getTime();
const inZones = (name: string, fn: () => void) => {
  for (const zone of ['America/New_York', 'America/Havana']) test(`${name} (${zone})`, () => { process.env.TZ = zone; fn(); });
};
const newYork = (name: string, fn: () => void) => test(name, () => { process.env.TZ = 'America/New_York'; fn(); });

newYork('the zone really is applied, so nothing below passes vacuously', () => {
  assert.equal(new Date(Date.UTC(2026, 5, 15, 12)).getTimezoneOffset(), 240);
  assert.equal(new Date(Date.UTC(2026, 0, 15, 12)).getTimezoneOffset(), 300);
});

inZones('the spring-forward day starts at standard midnight, not an hour earlier', () => {
  assert.equal(iso(startOfDay(local(2026, 3, 8, 14))), '2026-03-08T05:00:00.000Z');
});

inZones('the fall-back day starts at daylight midnight, not an hour later', () => {
  assert.equal(iso(startOfDay(local(2026, 11, 1, 14))), '2026-11-01T04:00:00.000Z');
});

newYork('yesterday evening stays out of today, and the first hour of today stays in', () => {
  // The two failures of v5's legacy expression, stated as outcomes.
  assert.ok(local(2026, 3, 7, 23, 30) < startOfDay(local(2026, 3, 8, 14)));
  assert.ok(local(2026, 11, 1, 0, 30) >= startOfDay(local(2026, 11, 1, 14)));
});

newYork('an ordinary day keeps its ordinary boundary', () => {
  assert.equal(iso(startOfDay(local(2026, 6, 15, 14))), '2026-06-15T04:00:00.000Z');
  assert.equal(iso(startOfDay(local(2026, 1, 20, 14))), '2026-01-20T05:00:00.000Z');
});

inZones('a transition day is 23 or 25 hours long rather than an assumed 24', () => {
  const length = (at: number) => (endOfDayExclusive(at) - startOfDay(at)) / HOUR;
  assert.equal(length(local(2026, 3, 8, 12)), 23);
  assert.equal(length(local(2026, 11, 1, 12)), 25);
  assert.equal(length(local(2026, 6, 15, 12)), 24);
});

newYork('the seven-day window stays seven calendar days wide across a transition', () => {
  const fallBack = local(2026, 11, 1, 14);
  assert.equal(iso(startOfDay(fallBack, 6)), '2026-10-26T04:00:00.000Z');
  assert.equal((endOfDayExclusive(fallBack) - startOfDay(fallBack, 6)) / HOUR, 6 * 24 + 25);
  const spring = local(2026, 3, 8, 14);
  assert.equal((endOfDayExclusive(spring) - startOfDay(spring, 6)) / HOUR, 6 * 24 + 23);
});

newYork('a 23- or 25-hour day is still one calendar day ago', () => {
  assert.equal(calendarDaysBetween(local(2026, 3, 7, 23, 59), local(2026, 3, 8, 23, 1)), 1);
  assert.equal(calendarDaysBetween(local(2026, 10, 31, 12), local(2026, 11, 1, 23)), 1);
  assert.equal(calendarDaysBetween(local(2026, 11, 1, 23), local(2026, 11, 1, 0, 5)), 0);
});
