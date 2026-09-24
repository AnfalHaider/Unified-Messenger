// Two people at two PCs, one business. Every case here is one of them pressing something the other cannot see.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { markId, mergeMarks, staleCleared, type Pushed, type SharedMark } from './mark-sync.ts';
import type { Overrides } from './awaiting-overrides.ts';

const NOW = new Date(2026, 8, 24, 11, 0).getTime();
const MIN = 60_000;

const merge = (o: Partial<Parameters<typeof mergeMarks>[0]> = {}) =>
  mergeMarks({ overrides: {}, remote: [], pushed: {}, visible: null, now: NOW, ...o });

test('a mark made on the other PC arrives here', () => {
  const remote: SharedMark[] = [{ accountId: 'wa', key: '923001234567@c.us', kind: 'handled', at: NOW - 5 * MIN, activity: NOW - 9 * MIN }];
  const r = merge({ remote });
  assert.deepEqual(r.overrides, { wa: { '923001234567@c.us': { kind: 'handled', activity: NOW - 9 * MIN, at: NOW - 5 * MIN } } });
  assert.deepEqual(r.push, [], 'nothing to say back about a mark we just took');
});

test('a mark made here goes up once, and not again', () => {
  const overrides: Overrides = { wa: { 'k@c.us': { kind: 'snoozed', until: NOW + 60 * MIN, at: NOW - MIN } } };
  const first = merge({ overrides });
  assert.deepEqual(first.push, [{ accountId: 'wa', key: 'k@c.us', kind: 'snoozed', until: NOW + 60 * MIN, at: NOW - MIN }]);
  // Second pass with nothing changed: the workspace already has it.
  const second = merge({ overrides, pushed: first.pushed });
  assert.deepEqual(second.push, []);
});

test('the newer press wins, whichever PC made it', () => {
  const overrides: Overrides = { wa: { k: { kind: 'handled', activity: 1, at: NOW - 10 * MIN } } };
  // Theirs is newer: it lands here.
  const theirs = merge({ overrides, remote: [{ accountId: 'wa', key: 'k', kind: 'excluded', at: NOW - MIN }] });
  assert.deepEqual(theirs.overrides.wa.k, { kind: 'excluded', at: NOW - MIN });
  // Theirs is older: ours stands, and goes up to correct them.
  const ours = merge({ overrides, remote: [{ accountId: 'wa', key: 'k', kind: 'excluded', at: NOW - 30 * MIN }] });
  assert.deepEqual(ours.overrides.wa.k, { kind: 'handled', activity: 1, at: NOW - 10 * MIN });
  assert.equal(ours.push.length, 1);
  assert.equal(ours.push[0].kind, 'handled');
});

test('a put-back here reaches the other PCs, instead of being handed straight back', () => {
  // We had marked it and sent it; now it is gone from this PC.
  const pushed: Pushed = { [markId('wa', 'k')]: NOW - 20 * MIN };
  const r = merge({ overrides: {}, pushed });
  assert.deepEqual(r.push, [{ accountId: 'wa', key: 'k', kind: 'cleared', at: NOW }]);
  // And with the mark still standing in the workspace, which is the real case: this PC must not take its own
  // mark back from the workspace and quietly re-apply it.
  const still = merge({ overrides: {}, pushed, remote: [{ accountId: 'wa', key: 'k', kind: 'handled', activity: 1, at: NOW - 20 * MIN }] });
  assert.deepEqual(still.overrides, {}, 'it stays put back here');
  assert.deepEqual(still.push, [{ accountId: 'wa', key: 'k', kind: 'cleared', at: NOW }]);
  // And a cleared mark coming the other way removes it here.
  const back = merge({
    overrides: { wa: { k: { kind: 'handled', activity: 1, at: NOW - 30 * MIN } } },
    remote: [{ accountId: 'wa', key: 'k', kind: 'cleared', at: NOW - MIN }],
  });
  assert.deepEqual(back.overrides, {}, 'the account is dropped once its last mark goes');
});

test('a put-back the workspace already knows about is not said twice', () => {
  const id = markId('wa', 'k');
  const r = merge({ overrides: {}, pushed: { [id]: NOW - 20 * MIN }, remote: [{ accountId: 'wa', key: 'k', kind: 'cleared', at: NOW - MIN }] });
  assert.deepEqual(r.push, []);
});

test('a member only carries marks for the accounts they can see', () => {
  const remote: SharedMark[] = [
    { accountId: 'dha-wa', key: 'a', kind: 'excluded', at: NOW - MIN },
    { accountId: 'f11-wa', key: 'b', kind: 'excluded', at: NOW - MIN },
  ];
  const r = merge({ remote, visible: ['dha-wa'] });
  assert.deepEqual(Object.keys(r.overrides), ['dha-wa'], 'a mark for a branch they cannot see is not theirs to hold');

  // And nothing of theirs goes up for an account they do not have.
  const up = merge({ overrides: { 'f11-wa': { b: { kind: 'excluded', at: NOW } } }, visible: ['dha-wa'] });
  assert.deepEqual(up.push, []);
});

test('a mark that came across from v5 has no moment, so anything dated beats it', () => {
  const overrides: Overrides = { wa: { k: { kind: 'excluded' } } };
  const r = merge({ overrides, remote: [{ accountId: 'wa', key: 'k', kind: 'cleared', at: 1 }] });
  assert.deepEqual(r.overrides, {}, 'a dated put-back wins over an undated mark');
});

test('two PCs settle: whatever each does, a second pass changes nothing', () => {
  const overrides: Overrides = { wa: { k: { kind: 'handled', activity: 5, at: NOW - MIN } } };
  const first = merge({ overrides, remote: [{ accountId: 'wa', key: 'j', kind: 'excluded', at: NOW - 2 * MIN }] });
  // Feed its own result back, with what it pushed now in the workspace.
  const settled = merge({
    overrides: first.overrides,
    remote: [{ accountId: 'wa', key: 'j', kind: 'excluded', at: NOW - 2 * MIN }, ...first.push],
    pushed: first.pushed,
  });
  assert.deepEqual(settled.push, [], 'nothing new to say');
  assert.deepEqual(settled.overrides, first.overrides, 'and nothing moves');
});

test('a mark is named so a person can tell what it is, and never breaks a document id', () => {
  assert.equal(markId('wa-1', '923001234567@c.us'), 'wa-1__923001234567@c.us');
  assert.ok(!markId('wa', 'a/b/c').includes('/'), 'a slash would make it a path');
  assert.ok(!markId('wa', 'x'.repeat(2000)).length || markId('wa', 'x'.repeat(2000)).length <= 300);
  assert.ok(!markId('.', '..').startsWith('.'), 'a leading dot is not a name Firestore takes');
});

test('cleared marks are forgotten after a month, once every PC has seen them', () => {
  const day = 24 * 60 * 60_000;
  const remote: SharedMark[] = [
    { accountId: 'wa', key: 'old', kind: 'cleared', at: NOW - 40 * day },
    { accountId: 'wa', key: 'recent', kind: 'cleared', at: NOW - 2 * day },
    { accountId: 'wa', key: 'live', kind: 'excluded', at: NOW - 40 * day },
  ];
  assert.deepEqual(staleCleared(remote, NOW).map((m) => m.key), ['old'], 'a live mark is never stale, however old');
});
