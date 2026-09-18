import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChatEntry } from './chat-entry.ts';
import { customerFor, customerId, forgetAccountCustomers, pruneCustomers, recordCustomers, setNote, tagsInUse, toggleTag, type Customers } from './customers.ts';

const MIN = 60_000, DAY = 24 * 60 * MIN;
const NOW = new Date(2026, 8, 18, 15, 0).getTime();

const waiting = (key: string, at: number): ChatEntry => ({
  conversationKey: key, customerName: 'Someone', unread: 1, lastActivity: at, preview: 'Hello', awaiting: true,
  lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '',
});
const answered = (key: string, at: number): ChatEntry =>
  ({ ...waiting(key, at), awaiting: false, lastMessageFromMe: true, unread: 0 });

test('a note and tags are kept per account and conversation, and tags toggle off', () => {
  const customers: Customers = {};
  setNote(customers, 'wa', 'a@c.us', '  Prefers evening appointments.  ', NOW);
  toggleTag(customers, 'wa', 'a@c.us', 'Regular', NOW);
  toggleTag(customers, 'wa', 'a@c.us', 'Evenings', NOW);
  toggleTag(customers, 'wa', 'a@c.us', ' regular ', NOW); // the same tag, said differently: removes it
  const r = customerFor(customers, 'wa', 'a@c.us')!;
  assert.deepEqual([r.note, r.tags], ['Prefers evening appointments.', ['Evenings']]);
  assert.equal(customerFor(customers, 'ig', 'a@c.us'), null, 'the same key on another account is another customer');
  assert.ok(Object.keys(customers).includes(customerId('wa', 'a@c.us')));
});

test('tags already used are offered, most used first', () => {
  const customers: Customers = {};
  toggleTag(customers, 'wa', 'a', 'Regular', NOW);
  toggleTag(customers, 'wa', 'b', 'Regular', NOW);
  toggleTag(customers, 'wa', 'b', 'Wholesale', NOW);
  assert.deepEqual(tagsInUse(customers), ['Regular', 'Wholesale']);
});

test('reads count the times a customer came to the line, and how fast each was answered', () => {
  const customers: Customers = {};
  recordCustomers(customers, 'wa', undefined, [waiting('a', NOW - 30 * MIN)], NOW - 29 * MIN);
  recordCustomers(customers, 'wa', [waiting('a', NOW - 30 * MIN)], [answered('a', NOW - 20 * MIN)], NOW - 19 * MIN);
  recordCustomers(customers, 'wa', [answered('a', NOW - 20 * MIN)], [waiting('a', NOW - 10 * MIN)], NOW - 9 * MIN);
  recordCustomers(customers, 'wa', [waiting('a', NOW - 10 * MIN)], [answered('a', NOW - 8 * MIN)], NOW);
  const r = customerFor(customers, 'wa', 'a')!;
  assert.equal(r.conversations, 2);
  assert.deepEqual(r.answers.map((a) => Math.round(a.minutes)), [10, 2]);
  assert.equal(r.firstSeen, NOW - 29 * MIN);
});

test('a chat nobody is waiting on and nobody wrote about gets no record, but an existing one is kept', () => {
  const customers: Customers = {};
  recordCustomers(customers, 'wa', undefined, [answered('quiet', NOW - MIN)], NOW);
  assert.deepEqual(customers, {});
  setNote(customers, 'wa', 'quiet', 'Always pays late', NOW);
  recordCustomers(customers, 'wa', undefined, [answered('quiet', NOW - MIN)], NOW);
  assert.equal(customerFor(customers, 'wa', 'quiet')!.note, 'Always pays late');
});

test('an answer that took more than a week was answered somewhere else, and is not counted', () => {
  const customers: Customers = {};
  recordCustomers(customers, 'wa', undefined, [waiting('a', NOW - 9 * DAY)], NOW - 9 * DAY);
  recordCustomers(customers, 'wa', [waiting('a', NOW - 9 * DAY)], [answered('a', NOW)], NOW);
  const r = customerFor(customers, 'wa', 'a')!;
  assert.deepEqual([r.answers, r.conversations], [[], 1]);
});

test('a read that changes nothing says so, so the file is not written every minute', () => {
  const customers: Customers = {};
  assert.equal(recordCustomers(customers, 'wa', undefined, [waiting('a', NOW - MIN)], NOW), true);
  assert.equal(recordCustomers(customers, 'wa', [waiting('a', NOW - MIN)], [waiting('a', NOW - MIN)], NOW), false);
});

test('only records the owner never wrote on are pruned, and an account can be forgotten', () => {
  const customers: Customers = {};
  recordCustomers(customers, 'wa', undefined, [waiting('old', NOW - 200 * DAY)], NOW - 200 * DAY);
  setNote(customers, 'wa', 'kept', 'Reads every message twice', NOW - 200 * DAY);
  recordCustomers(customers, 'ig', undefined, [waiting('other', NOW)], NOW);
  pruneCustomers(customers, NOW);
  assert.deepEqual(Object.keys(customers).sort(), ['ig|other', 'wa|kept']);
  forgetAccountCustomers(customers, 'wa');
  assert.deepEqual(Object.keys(customers), ['ig|other']);
});

test('nothing stored carries a name, a number or message text', () => {
  const customers: Customers = {};
  recordCustomers(customers, 'wa', [waiting('a', NOW - MIN)], [answered('a', NOW)], NOW);
  const text = JSON.stringify(customers);
  for (const forbidden of ['Someone', 'Hello', '+92']) assert.ok(!text.includes(forbidden), forbidden);
});
