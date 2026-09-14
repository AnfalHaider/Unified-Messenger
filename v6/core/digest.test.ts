import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChatEntry } from './chat-entry.ts';
import { parseConfig } from './config.ts';
import { digestDue, morningSplit } from './digest.ts';
import type { Judge, Snapshots } from './snapshot.ts';

const MIN = 60_000, HOUR = 60 * MIN;
const local = (y: number, m: number, d: number, h = 0, min = 0) => new Date(y, m - 1, d, h, min).getTime();
const NOW = local(2026, 7, 21, 10, 0); // Tuesday, before an 11:00 opening

const chat = (key: string, lastActivity: number, o: Partial<ChatEntry> = {}): ChatEntry => ({
  conversationKey: key, customerName: `Customer ${key}`, unread: 1, lastActivity, preview: 'Can I book for Friday?', awaiting: true,
  lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '', ...o,
});
const judge: Judge = { now: NOW, overrides: {}, filterClosed: true };
const config = (hours: Record<string, unknown> | null) => parseConfig({
  accounts: [{ id: 'wa', channel: 'whatsapp', professional: true, location: 'Main' }, { id: 'loose', channel: 'whatsapp', professional: true }],
  locations: [{ name: 'Main', hours }],
}).config;
const everyDay = { enabled: true, week: Array(7).fill({ open: 11 * 60, close: 21 * 60 }) };

test('with opening hours, owed means wrote before last night\'s closing, and overnight means after it', () => {
  const snapshots: Snapshots = { wa: { capturedAt: NOW, chats: [
    chat('before-close', local(2026, 7, 20, 20, 30)),
    chat('earlier', local(2026, 7, 20, 12, 0)),
    chat('after-close', local(2026, 7, 20, 22, 15)),
    chat('this-morning', local(2026, 7, 21, 8, 0)),
    chat('answered', local(2026, 7, 20, 19, 0), { awaiting: false, lastMessageFromMe: true }),
  ] } };
  const split = morningSplit(config(everyDay), snapshots, judge, ['wa'], NOW);
  assert.deepEqual(split.owed.map((o) => o.chat.conversationKey), ['earlier', 'before-close'], 'oldest first');
  assert.equal(split.overnight, 2);
  assert.equal(split.byHours, true);
  assert.equal(split.hasData, true);
});

test('without opening hours, the day starts at midnight', () => {
  const snapshots: Snapshots = { loose: { capturedAt: NOW, chats: [chat('yesterday', local(2026, 7, 20, 23, 50)), chat('today', local(2026, 7, 21, 0, 10))] } };
  const split = morningSplit(config(null), snapshots, judge, ['loose'], NOW);
  assert.deepEqual(split.owed.map((o) => o.chat.conversationKey), ['yesterday']);
  assert.equal(split.overnight, 1);
  assert.equal(split.byHours, false);
});

test('backlog older than the backlog line, and chats closed by the rule, are not owed', () => {
  const snapshots: Snapshots = { loose: { capturedAt: NOW, chats: [
    chat('old', NOW - 10 * 24 * HOUR), chat('thanks', local(2026, 7, 20, 18, 0), { preview: 'ok thanks' }),
  ] } };
  const split = morningSplit(config(null), snapshots, judge, ['loose'], NOW);
  assert.deepEqual(split.owed, []);
});

test('no read at all is not a quiet morning', () => {
  assert.equal(morningSplit(config(null), {}, judge, ['loose'], NOW).hasData, false);
});

test('the digest is due once a local day, only when switched on', () => {
  assert.equal(digestDue(NOW, null, true), true);
  assert.equal(digestDue(NOW, '2026-07-21', true), false);
  assert.equal(digestDue(local(2026, 7, 22, 0, 5), '2026-07-21', true), true);
  assert.equal(digestDue(NOW, null, false), false);
});
