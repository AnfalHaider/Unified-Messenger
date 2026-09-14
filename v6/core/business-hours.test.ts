// Same cases as UnifiedMessenger.Tests/BusinessHoursCalculatorTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { elapsedBusinessMinutes, isOpen, lastClosing } from './business-hours.ts';

const local = (y: number, mo: number, d: number, h: number, mi: number) => new Date(y, mo - 1, d, h, mi);
const allWeek = { enabled: true, openMinutes: 9 * 60, closeMinutes: 18 * 60, workingDays: [0, 1, 2, 3, 4, 5, 6] };

test('disabled hours return raw elapsed minutes', () => {
  assert.equal(elapsedBusinessMinutes(local(2026, 6, 15, 17, 0), local(2026, 6, 15, 17, 30), { ...allWeek, enabled: false }), 30);
});

test('no hours return raw elapsed minutes', () => {
  assert.equal(elapsedBusinessMinutes(local(2026, 6, 15, 0, 0), local(2026, 6, 15, 1, 0), null), 60);
});

test('pauses outside hours across two days', () => {
  assert.equal(elapsedBusinessMinutes(local(2026, 6, 15, 17, 0), local(2026, 6, 16, 10, 0), allWeek), 120);
});

test('entirely outside hours counts zero', () => {
  assert.equal(elapsedBusinessMinutes(local(2026, 6, 15, 19, 0), local(2026, 6, 15, 23, 0), allWeek), 0);
});

test('a non-working day is skipped', () => {
  const start = local(2026, 6, 15, 10, 0);
  const workingDays = [0, 1, 2, 3, 4, 5, 6].filter((d) => d !== start.getDay());
  assert.equal(elapsedBusinessMinutes(start, local(2026, 6, 15, 12, 0), { ...allWeek, workingDays }), 0);
});

test('open now: inside the window on a working day, and always when hours are off', () => {
  const monday = local(2026, 6, 15, 10, 0); // a Monday
  const weekdays = { ...allWeek, workingDays: [1, 2, 3, 4, 5] };
  assert.equal(isOpen(weekdays, monday.getTime()), true);
  assert.equal(isOpen(weekdays, local(2026, 6, 15, 18, 0).getTime()), false, 'closing time is closed');
  assert.equal(isOpen(weekdays, local(2026, 6, 14, 10, 0).getTime()), false, 'Sunday is not a working day');
  assert.equal(isOpen({ ...allWeek, enabled: false }, local(2026, 6, 15, 3, 0).getTime()), true);
  assert.equal(isOpen(null, monday.getTime()), true);
});

// ---- per-day hours and closed dates (v6 additions)

const week = (o: Partial<Record<number, { open: number; close: number } | null>> = {}) => {
  const days = Array.from({ length: 7 }, (_, d) => (d in o ? o[d]! : { open: 11 * 60, close: 21 * 60 }));
  return { enabled: true, openMinutes: 0, closeMinutes: 0, week: days };
};

test('each day can have its own hours, and a day set to null is closed', () => {
  // 17 July 2026 is a Friday: open 14:30 to 21:30; the Saturday after is closed.
  const hours = week({ 5: { open: 14 * 60 + 30, close: 21 * 60 + 30 }, 6: null });
  assert.equal(elapsedBusinessMinutes(local(2026, 7, 17, 12, 0), local(2026, 7, 17, 15, 0), hours), 30);
  assert.equal(isOpen(hours, local(2026, 7, 17, 13, 0).getTime()), false);
  assert.equal(isOpen(hours, local(2026, 7, 17, 21, 0).getTime()), true);
  // Friday 21:00 to Sunday 12:00: half an hour on Friday, nothing on Saturday, an hour on Sunday.
  assert.equal(elapsedBusinessMinutes(local(2026, 7, 17, 21, 0), local(2026, 7, 19, 12, 0), hours), 90);
});

test('a closed date stops the clock for the whole day, and only that day', () => {
  const hours = { ...week(), closedDates: ['2026-07-15'] };
  assert.equal(elapsedBusinessMinutes(local(2026, 7, 15, 10, 0), local(2026, 7, 15, 20, 0), hours), 0);
  assert.equal(isOpen(hours, local(2026, 7, 15, 12, 0).getTime()), false);
  // A message the evening before waits through the holiday and starts counting again the next morning.
  assert.equal(elapsedBusinessMinutes(local(2026, 7, 14, 20, 0), local(2026, 7, 16, 12, 0), hours), 60 + 60);
});

test('the uniform hours v5 wrote still work, and closed dates apply to them too', () => {
  const uniform = { ...allWeek, closedDates: ['2026-06-15'] };
  assert.equal(elapsedBusinessMinutes(local(2026, 6, 15, 10, 0), local(2026, 6, 15, 12, 0), uniform), 0);
  assert.equal(elapsedBusinessMinutes(local(2026, 6, 16, 10, 0), local(2026, 6, 16, 12, 0), uniform), 120);
});

test('hours switched off ignore closed dates too: the clock runs around the clock', () => {
  assert.equal(elapsedBusinessMinutes(local(2026, 7, 15, 10, 0), local(2026, 7, 15, 11, 0), { ...week(), enabled: false, closedDates: ['2026-07-15'] }), 60);
});

test('the last closing is the end of the latest window already over, skipping closed days', () => {
  const hours = week({ 0: null }); // 11:00 to 21:00, Sunday closed
  // Monday 20 July 2026, 10:00, before opening: the last closing was Saturday 21:00, since Sunday was closed.
  assert.equal(lastClosing(hours, local(2026, 7, 20, 10, 0).getTime()), local(2026, 7, 18, 21, 0).getTime());
  // Open now: still the previous evening's closing, not today's.
  assert.equal(lastClosing(hours, local(2026, 7, 21, 15, 0).getTime()), local(2026, 7, 20, 21, 0).getTime());
  // After today's closing: today's.
  assert.equal(lastClosing(hours, local(2026, 7, 21, 22, 0).getTime()), local(2026, 7, 21, 21, 0).getTime());
  assert.equal(lastClosing({ ...hours, enabled: false }, local(2026, 7, 21, 22, 0).getTime()), null, 'hours off have no closing');
  assert.equal(lastClosing(null, local(2026, 7, 21, 22, 0).getTime()), null);
});
