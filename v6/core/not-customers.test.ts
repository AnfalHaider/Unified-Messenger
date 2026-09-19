// Chats that are not customers: the owner's permanent mark, and the rules for staff names and team numbers.
// Every count asks the same question, so these check the line, the split, the caught-up figure and Set aside.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChatEntry } from './chat-entry.ts';
import { clear, markNotCustomer, pruneExpired, type Overrides } from './awaiting-overrides.ts';
import { awaitingChats, awaitingSplit, notCustomerWhy, setAside, windowed, type Judge, type Snapshots } from './snapshot.ts';

const MIN = 60_000;
const NOW = new Date(2026, 8, 19, 15, 0).getTime();

const chat = (key: string, name: string, o: Partial<ChatEntry> = {}): ChatEntry => ({
  conversationKey: key, customerName: name, unread: 1, lastActivity: NOW - 30 * MIN, preview: 'Can you check the stock list?',
  awaiting: true, lastMessageFromMe: false, contactPhone: '', hasLastMessage: true, lastMessageType: 'chat', lastCallOutcome: '', ...o,
});

const snapshots = (): Snapshots => ({ wa: { capturedAt: NOW, chats: [
  chat('923001112233@c.us', 'Sample Customer A'),
  chat('923004445566@c.us', 'Bilal Staff Front Desk'),
  chat('923007778899@c.us', 'Sample Team Member', { contactPhone: '923007778899' }),
  chat('lid-1@lid', 'Staffordshire Supplies'),
] } });

const judge = (overrides: Overrides = {}, words: string[] = [], numbers: string[] = []): Judge =>
  ({ now: NOW, overrides, filterClosed: true, notCustomers: { words, numbers } });

const waitingNames = (j: Judge) => awaitingChats(snapshots(), 'wa', j).map((c) => c.customerName).sort();

test('with no rules and no marks, everyone is a customer', () => {
  assert.equal(waitingNames(judge()).length, 4);
});

test('a word in the name leaves the chat out, as a whole word and in any case', () => {
  const j = judge({}, ['staff']);
  assert.deepEqual(waitingNames(j), ['Sample Customer A', 'Sample Team Member', 'Staffordshire Supplies'],
    '"Staffordshire" is a customer: the word has to stand on its own');
  assert.equal(notCustomerWhy('wa', chat('x', 'Bilal Staff Front Desk'), j), 'Name contains “staff”');
});

test('a team number leaves the chat out however it is written', () => {
  for (const written of ['+92 300 7778899', '0300-7778899', '923007778899']) {
    const j = judge({}, [], [written.replace(/\D/g, '')]);
    assert.ok(!waitingNames(j).includes('Sample Team Member'), written);
    assert.equal(notCustomerWhy('wa', chat('923007778899@c.us', 'Sample Team Member'), j), 'One of the team’s numbers');
  }
  assert.equal(waitingNames(judge({}, [], ['12345'])).length, 4, 'a fragment too short to be a number matches nothing');
});

test('the owner’s mark is permanent: a new message does not bring the chat back, and it never expires', () => {
  const o: Overrides = {};
  markNotCustomer(o, 'wa', '923001112233@c.us', NOW - MIN);
  const later = { ...judge(o), now: NOW + 90 * 24 * 60 * MIN };
  pruneExpired(o, later.now);
  const newer = { wa: { capturedAt: later.now, chats: [chat('923001112233@c.us', 'Sample Customer A', { lastActivity: later.now - MIN })] } };
  assert.deepEqual(awaitingChats(newer, 'wa', later), []);
  clear(o, 'wa', '923001112233@c.us');
  assert.equal(awaitingChats(newer, 'wa', later).length, 1, 'Put back undoes it');
});

test('a chat that is not a customer is in no figure: not waiting, not backlog, not caught up', () => {
  const j = judge({}, ['staff', 'team']);
  const split = awaitingSplit(snapshots(), ['wa'], j, 7);
  assert.equal(split.needsReply + split.backlog + split.closedAutomatically, 2);
  const answered: Snapshots = { wa: { capturedAt: NOW, chats: [chat('s', 'Bilal Staff', { awaiting: false, lastMessageFromMe: true })] } };
  assert.deepEqual(windowed(answered, 'wa', j), { active: 0, caughtUp: 0 }, 'a staff chat does not make the business look caught up');
});

test('Set aside lists every chat left out, with why, and says which ones Put back can undo', () => {
  const o: Overrides = {};
  markNotCustomer(o, 'wa', '923001112233@c.us', NOW - MIN);
  const rows = setAside(snapshots(), ['wa'], judge(o, ['staff'], ['923007778899']))
    .filter((x) => x.kind === 'not-customer')
    .map((x) => x.kind === 'not-customer' ? [x.chat.customerName, x.why, x.marked] : null);
  assert.deepEqual(rows.sort(), [
    ['Bilal Staff Front Desk', 'Name contains “staff”', false],
    ['Sample Customer A', 'Marked as not a customer', true],
    ['Sample Team Member', 'One of the team’s numbers', false],
  ]);
});
