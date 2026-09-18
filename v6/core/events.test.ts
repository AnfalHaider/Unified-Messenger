import { test } from 'node:test';
import assert from 'node:assert/strict';
import { KEEP_EVENTS, lostLoginTimeline, readerTimeline, recordEvent, signedOutSince, type Events, type Outcome, type ReadEvent } from './events.ts';

const MIN = 60_000;
const NOW = new Date(2026, 8, 14, 23, 30).getTime();

const event = (account: string, at: number, outcome: Outcome, o: Partial<ReadEvent> = {}): ReadEvent =>
  ({ account, channel: 'whatsapp', at, outcome, chats: outcome === 'read' ? 500 : null, waiting: outcome === 'read' ? 6 : null, stage: null, ...o });

const add = (events: Events, ...list: ReadEvent[]) => { for (const e of list) recordEvent(events, e); return events; };

test('only the newest events are kept, per account', () => {
  const events: Events = {};
  for (let i = 0; i < KEEP_EVENTS + 10; i++) add(events, event('wa', NOW - (KEEP_EVENTS + 10 - i) * MIN, 'read'));
  add(events, event('ig', NOW, 'read'));
  assert.equal(events.wa.length, KEEP_EVENTS);
  assert.equal(events.wa[0].at, NOW - KEEP_EVENTS * MIN);
  assert.equal(events.ig.length, 1);
});

test('a sign-out is dated from where the run of sign-outs began, not the latest one', () => {
  const events = add({}, event('wa', NOW - 20 * MIN, 'read'), event('wa', NOW - 10 * MIN, 'signed-out'), event('wa', NOW - MIN, 'signed-out'));
  assert.equal(signedOutSince(events, 'wa'), NOW - 10 * MIN);
  assert.equal(signedOutSince(add({}, event('wa', NOW, 'read')), 'wa'), null);
  assert.equal(signedOutSince({}, 'wa'), null);
});

test('the lost-login timeline shows the reads before it, the sign-out, and how long since', () => {
  const events = add({},
    ...[9, 8, 7, 6, 5].map((m) => event('wa', NOW - m * MIN, 'read')),
    event('wa', NOW - 4 * MIN, 'reload'),
    event('wa', NOW - 3 * MIN, 'signed-out'),
    event('wa', NOW - MIN, 'signed-out'),
  );
  const items = lostLoginTimeline(events, 'wa', NOW);
  assert.deepEqual(items.map((i) => i.title), ['5 good reads', 'Page reloaded', 'Sign-in screen', 'Still signed out, 3 minutes']);
  assert.equal(items[0].detail, 'The last saw 500 chats read, 6 waiting.');
  assert.equal(items.at(-1)!.at, null);
  assert.deepEqual(items.map((i) => i.tone), ['ok', 'due', 'late', 'neutral']);
});

test('the timeline says the hours when a sign-out is old, and stops at the given number of lines', () => {
  const events = add({}, ...Array.from({ length: 12 }, (_, i) => event('wa', NOW - (30 - i) * MIN, i % 2 ? 'read' : 'empty')), event('wa', NOW - 9 * 60 * MIN + 24 * MIN, 'signed-out'));
  const items = lostLoginTimeline(events, 'wa', NOW, 3);
  assert.equal(items.length, 4, 'three lines plus the one about now');
  assert.match(items.at(-1)!.title, /^Still signed out, 8 h 36 min$/);
});

test('an account that is signed in still gets its last few reads, and an unknown one gets nothing', () => {
  const events = add({}, event('wa', NOW - 2 * MIN, 'read'), event('wa', NOW - MIN, 'not-ready', { stage: 'no-store' }));
  const items = lostLoginTimeline(events, 'wa', NOW);
  assert.deepEqual(items.map((i) => i.title), ['Good read', 'Reader not ready']);
  assert.match(items[1].detail, /had not been built yet/);
  assert.deepEqual(lostLoginTimeline(events, 'nobody', NOW), []);
});

test('the reader timeline reports one cause across accounts, not one per account', () => {
  const events = add({},
    event('a', NOW - 9 * MIN, 'read'), event('b', NOW - 9 * MIN, 'read'),
    event('a', NOW - 8 * MIN, 'empty'), event('b', NOW - 7 * MIN, 'empty'),
    event('a', NOW - MIN, 'empty'), event('b', NOW - MIN, 'empty'),
  );
  const items = readerTimeline(events, ['a', 'b'], NOW);
  assert.equal(items[0].title, '2 accounts stopped reading');
  assert.equal(items[0].at, NOW - 8 * MIN);
  assert.deepEqual(items.slice(1).map((i) => i.title), ['2 good reads', 'Nothing came back, 4 times']);
  assert.equal(items.at(-1)!.at, NOW - MIN, 'the newest event is the last line while reading is still being tried');
});

test('a reader that has not read for a while says so; a healthy one does not', () => {
  const quiet = add({}, event('a', NOW - 40 * MIN, 'read'));
  assert.equal(readerTimeline(quiet, ['a'], NOW).at(-1)!.title, 'Nothing read for 40 minutes');
  const fine = add({}, event('a', NOW - 30_000, 'read'));
  assert.deepEqual(readerTimeline(fine, ['a'], NOW).map((i) => i.title), ['Good read']);
  assert.deepEqual(readerTimeline({}, ['a'], NOW), []);
});

test('nothing on a timeline can carry a customer name, a number or message text', () => {
  const events = add({}, event('wa', NOW - MIN, 'read', { chats: 500, waiting: 6 }), event('wa', NOW, 'signed-out'));
  const text = JSON.stringify([...lostLoginTimeline(events, 'wa', NOW), ...readerTimeline(events, ['wa'], NOW)]);
  for (const forbidden of ['@c.us', '+92', 'Sara', 'preview']) assert.ok(!text.includes(forbidden));
});
