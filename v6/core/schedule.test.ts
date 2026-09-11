import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseConfig, type Config } from './config.ts';
import { accountsToSleep, dueForRead, inQuietHours, nextReadDueAt, readableAccounts } from './schedule.ts';

const MIN = 60_000;
const NOW = new Date(2026, 7, 11, 14, 0).getTime();
const config = (accounts: Record<string, unknown>[], settings: Record<string, unknown> = {}): Config =>
  parseConfig({ accounts, settings }).config;
const watched = (id: string, channel = 'whatsapp') => ({ id, channel, professional: true });

test('only professional accounts on a channel with a reader are read', () => {
  const c = config([
    watched('wa'), watched('ig', 'instagram'),
    { id: 'personal', channel: 'whatsapp', professional: false },
    // Registered but unmeasured: offering the channel is honest, inventing numbers for it is not.
    watched('tg', 'telegram'), watched('custom', 'custom'),
  ]);
  assert.deepEqual(readableAccounts(c).map((a) => a.id), ['wa', 'ig']);
});

test('an account is due when its interval has passed, longest-waiting first', () => {
  const c = config([watched('a'), watched('b'), watched('c')], { readEverySeconds: 60 });
  assert.deepEqual(dueForRead(c, { a: NOW - 90 * 1000, b: NOW - 30 * 1000 }, NOW), ['c', 'a']);
  assert.deepEqual(dueForRead(c, { a: NOW, b: NOW, c: NOW }, NOW), []);
  assert.deepEqual(dueForRead(c, { a: NOW, b: NOW, c: NOW }, NOW + 60 * 1000), ['a', 'b', 'c']);
});

test('the next deadline is the earliest one, and null when nothing is read', () => {
  const c = config([watched('a'), watched('b')], { readEverySeconds: 60 });
  assert.equal(nextReadDueAt(c, { a: NOW, b: NOW + 10 * 1000 }, NOW), NOW + 60 * 1000);
  assert.ok(nextReadDueAt(c, {}, NOW)! < NOW);
  assert.equal(nextReadDueAt(config([{ id: 'personal', channel: 'whatsapp' }]), {}, NOW), null);
});

test('nothing sleeps by default', () => {
  const c = config([{ id: 'personal', channel: 'whatsapp' }]);
  assert.equal(c.settings.sleepUnusedAccounts, false);
  assert.deepEqual(accountsToSleep(c, {}, NOW), []);
});

test('an account that feeds oversight is never slept, however idle', () => {
  // Sleeping it would stop its reads while the dashboard kept showing yesterday's numbers as today's.
  const c = config([watched('wa'), { id: 'personal', channel: 'whatsapp' }], { sleepUnusedAccounts: true, sleepAfterMinutes: 20 });
  assert.deepEqual(accountsToSleep(c, { wa: NOW - 5 * 24 * 60 * MIN, personal: NOW - 5 * 24 * 60 * MIN }, NOW), ['personal']);
});

test('sleeping waits for the idle time, spares the account on screen, and switches off at zero', () => {
  const c = config([{ id: 'p1', channel: 'whatsapp' }, { id: 'p2', channel: 'whatsapp' }], { sleepUnusedAccounts: true, sleepAfterMinutes: 20 });
  assert.deepEqual(accountsToSleep(c, { p1: NOW - 21 * MIN, p2: NOW - 5 * MIN }, NOW), ['p1']);
  assert.deepEqual(accountsToSleep(c, { p1: NOW - 21 * MIN, p2: NOW - 5 * MIN }, NOW, 'p1'), []);
  const off = config([{ id: 'p1', channel: 'whatsapp' }], { sleepUnusedAccounts: true, sleepAfterMinutes: 0 });
  assert.deepEqual(accountsToSleep(off, { p1: 0 }, NOW), []);
});

test('quiet hours wrap past midnight and never stop a read', () => {
  const at = (hour: number, quiet: Record<string, unknown>) =>
    inQuietHours(config([], { quietHours: quiet }).settings, new Date(2026, 7, 11, hour).getTime());
  const overnight = { enabled: true, startHour: 21, endHour: 8 };
  assert.deepEqual([at(22, overnight), at(3, overnight), at(8, overnight), at(14, overnight)], [true, true, false, false]);
  const daytime = { enabled: true, startHour: 9, endHour: 17 };
  assert.deepEqual([at(10, daytime), at(18, daytime)], [true, false]);
  assert.equal(at(22, { enabled: false, startHour: 21, endHour: 8 }), false);
  assert.equal(at(22, { enabled: true, startHour: 21, endHour: 21 }), false);
  // Reads are scheduled by the interval alone, so the numbers stay current through the night.
  const c = config([watched('a')], { readEverySeconds: 60, quietHours: overnight });
  assert.deepEqual(dueForRead(c, {}, new Date(2026, 7, 11, 3).getTime()), ['a']);
});
