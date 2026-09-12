// Same cases as UnifiedMessenger.Tests/BusinessHoursCalculatorTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { elapsedBusinessMinutes, isOpen } from './business-hours.ts';

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
