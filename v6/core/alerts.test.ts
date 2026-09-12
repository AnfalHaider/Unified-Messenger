import { test } from 'node:test';
import assert from 'node:assert/strict';
import { alertsDue, pruneNotified, type AlertInput, type Notified, type WaitingRow } from './alerts.ts';
import { defaultSettings, parseConfig } from './config.ts';

const MIN = 60_000;
const NOW = new Date(2026, 7, 11, 14, 0).getTime();

const row = (key: string, waited: number, o: Partial<WaitingRow> = {}): WaitingRow => ({
  accountId: 'acct', accountName: 'Front desk', key, customer: `Customer ${key}`,
  waited, targetMinutes: 15, lastActivity: NOW - waited * MIN, open: true, ...o,
});
const input = (rows: WaitingRow[], o: Partial<AlertInput> = {}): AlertInput =>
  ({ rows, signedOut: [], settings: defaultSettings(), now: NOW, ...o });
const kinds = (list: { kind: string }[]) => list.map((a) => a.kind);

test('a chat two minutes from its target alerts once, and not again on the next pass', () => {
  const notified: Notified = {};
  assert.deepEqual(kinds(alertsDue(input([row('a', 13)]), notified)), ['near-target']);
  assert.deepEqual(alertsDue(input([row('a', 14, { lastActivity: NOW - 13 * MIN })]), notified), []);
});

test('outside the window there is nothing to say: too early, or already past', () => {
  assert.deepEqual(alertsDue(input([row('early', 12), row('late', 15)]), {}), []);
});

test('a new message from the same customer starts a new wait, which can alert again', () => {
  const notified: Notified = {};
  alertsDue(input([row('a', 13)]), notified);
  assert.equal(alertsDue(input([row('a', 13, { lastActivity: NOW - 2 * MIN })]), notified).length, 1);
});

test('a closed location does not alert, and the location\'s own target is used', () => {
  assert.deepEqual(alertsDue(input([row('closed', 13, { open: false })]), {}), []);
  assert.deepEqual(kinds(alertsDue(input([row('slow', 28, { targetMinutes: 30 })]), {})), ['near-target']);
});

test('an hour\'s wait alerts once per customer, only as it crosses the hour', () => {
  const notified: Notified = {};
  assert.deepEqual(kinds(alertsDue(input([row('a', 60)]), notified)), ['waited-hour']);
  assert.deepEqual(alertsDue(input([row('a', 61, { lastActivity: NOW - 60 * MIN })]), notified), []);
  // Opening the app on a long backlog does not replay every hour that already passed.
  assert.deepEqual(alertsDue(input([row('old', 300)]), {}), []);
});

test('a signed-out account alerts once, and again only after it signed back in', () => {
  const notified: Notified = {};
  const out = [{ id: 'acct', name: 'Front desk' }];
  assert.deepEqual(kinds(alertsDue(input([], { signedOut: out }), notified)), ['signed-out']);
  assert.deepEqual(alertsDue(input([], { signedOut: out }), notified), []);
  alertsDue(input([]), notified);
  assert.equal(alertsDue(input([], { signedOut: out }), notified).length, 1);
});

test('quiet hours and switched-off alerts stay silent, and do not use up the alert', () => {
  const quiet = parseConfig({ settings: { quietHours: { enabled: true, startHour: 13, endHour: 15 } } }).config.settings;
  const notified: Notified = {};
  assert.deepEqual(alertsDue(input([row('a', 13)], { settings: quiet }), notified), []);
  assert.equal(alertsDue(input([row('a', 13)]), notified).length, 1);

  const off = parseConfig({ settings: { alerts: { nearTarget: false, waitedHour: false, signedOut: false } } }).config.settings;
  assert.deepEqual(alertsDue(input([row('b', 13), row('c', 60)], { settings: off, signedOut: [{ id: 'x', name: 'X' }] }), {}), []);
});

test('many at once become one alert that counts them, and each is still remembered', () => {
  const notified: Notified = {};
  const rows = [row('a', 13), row('b', 13.5), row('c', 14), row('d', 14.5)];
  const shown = alertsDue(input(rows), notified);
  assert.equal(shown.length, 1);
  assert.equal(shown[0].kind, 'near-target');
  assert.match(shown[0].title, /^4 customers/);
  assert.equal(shown[0].accountId, null, 'a summary opens the line, not one chat');
  assert.deepEqual(alertsDue(input(rows), notified), []);
});

test('an alert names the customer and account, never the message', () => {
  const [alert] = alertsDue(input([row('a', 13, { customer: 'Sample Customer' })]), {});
  assert.match(`${alert.title} ${alert.body}`, /Sample Customer/);
  assert.match(alert.body, /Front desk/);
  assert.equal(alert.accountId, 'acct');
  assert.equal(alert.key, 'a');
});

test('remembered chat alerts are forgotten after two days', () => {
  const notified: Notified = { old: NOW - 3 * 24 * 60 * MIN, recent: NOW - MIN };
  pruneNotified(notified, NOW);
  assert.deepEqual(Object.keys(notified), ['recent']);
});
