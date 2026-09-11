// Port of OversightChatSnapshotServiceTests.cs, the logic half of AwaitingSplitTests.cs, and
// OversightSnapshotPersistenceTests.cs. The hint and tooltip wording tests move with the screens.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChatEntry } from './chat-entry.ts';
import { markHandled } from './awaiting-overrides.ts';
import { explain } from './reply-need.ts';
import { emptyResponseTimes } from './response-times.ts';
import {
  automaticallyClosed, awaitingChats, awaitingSplit, digest, distrustColdScan, lastCaptured, recordRead, windowed,
  type Judge, type Snapshots,
} from './snapshot.ts';

const HOUR = 3_600_000, DAY = 24 * HOUR;
const NOW = Date.UTC(2026, 7, 10, 9);

const chat = (key: string, o: Partial<ChatEntry> = {}): ChatEntry => ({
  conversationKey: key, customerName: 'Customer', unread: 0, lastActivity: NOW, preview: '', awaiting: false,
  lastMessageFromMe: false, contactPhone: '', hasLastMessage: null, lastMessageType: '', lastCallOutcome: '', ...o,
});
const judge = (o: Partial<Judge> = {}): Judge => ({ now: NOW, overrides: {}, filterClosed: true, ...o });
const waiting = (key: string, preview: string, lastActivity: number) => chat(key, { preview, lastActivity, awaiting: true });

function read(chats: ChatEntry[], snapshots: Snapshots = {}, account = 'inst') {
  recordRead(snapshots, emptyResponseTimes(), account, chats, NOW);
  return snapshots;
}
const keys = (chats: ChatEntry[]) => chats.map((c) => c.conversationKey);

// ---- Sticky awaiting

test('opening a chat is not replying: an unconfirmed read keeps it waiting', () => {
  const s = read([chat('jid-x', { unread: 2, awaiting: true })]);
  assert.equal(awaitingChats(s, 'inst', judge()).length, 1);
  read([chat('jid-x')], s);
  assert.deepEqual(keys(awaitingChats(s, 'inst', judge())), ['jid-x']);
});

test('a confirmed reply clears waiting', () => {
  const s = read([chat('jid-y', { unread: 1, awaiting: true })]);
  read([chat('jid-y', { lastMessageFromMe: true })], s);
  assert.deepEqual(awaitingChats(s, 'inst', judge()), []);
});

test('sticky waiting decays after the maximum age', () => {
  const s = read([chat('jid-old', { unread: 1, awaiting: true, lastActivity: NOW - 10 * DAY })]);
  assert.equal(awaitingChats(s, 'inst', judge()).length, 1);
  read([chat('jid-old', { lastActivity: NOW - 10 * DAY })], s);
  assert.deepEqual(awaitingChats(s, 'inst', judge()), []);
});

test('a chat first seen caught up stays caught up', () => {
  assert.deepEqual(awaitingChats(read([chat('jid-z')]), 'inst', judge()), []);
});

test('reads feed response times from the post-sticky state', () => {
  const s: Snapshots = {}, times = emptyResponseTimes();
  times.watchStart.inst = NOW - DAY;
  recordRead(s, times, 'inst', [chat('c', { awaiting: true, lastActivity: NOW - HOUR })], NOW - HOUR);
  recordRead(s, times, 'inst', [chat('c', { lastMessageFromMe: true, lastActivity: NOW })], NOW);
  assert.deepEqual(times.samples.inst, [{ answeredAt: NOW, minutes: 60 }]);
});

// ---- Windowed counts and the digest

test('windowed counts scope the caught-up chats by last activity', () => {
  const s = read([chat('jid-a'), chat('jid-b', { unread: 2, awaiting: true }), chat('jid-c', { lastActivity: NOW - 3 * DAY })]);
  assert.deepEqual(windowed(s, 'inst', judge(), NOW - DAY), { active: 2, caughtUp: 1 });
  assert.deepEqual(windowed(s, 'inst', judge()), { active: 3, caughtUp: 2 });
  const [only, ...rest] = awaitingChats(s, 'inst', judge());
  assert.equal(rest.length, 0);
  assert.equal(only.conversationKey, 'jid-b');
  assert.equal(only.unread, 2);
});

test('caught-up chats are windowed, but waiting is always current state', () => {
  const s = read([
    chat('a', { unread: 1, awaiting: true, lastActivity: NOW - 10 * DAY }),
    chat('b', { unread: 1, awaiting: true, lastActivity: NOW - 5 * DAY }),
    chat('c'),
  ]);
  assert.deepEqual(windowed(s, 'inst', judge(), NOW - 7 * DAY, NOW - 2 * DAY), { active: 2, caughtUp: 0 });
  assert.deepEqual(keys(awaitingChats(s, 'inst', judge())).sort(), ['a', 'b']);
});

test('the digest counts new, total and oldest', () => {
  const s = read([chat('a', { unread: 2, awaiting: true }), chat('b', { unread: 1, awaiting: true, lastActivity: NOW - 2 * DAY }), chat('c')]);
  assert.deepEqual(digest(s, ['inst'], judge(), NOW - DAY), { hasData: true, totalAwaiting: 2, newAwaiting: 1, accountsWithAwaiting: 1, oldestActivity: NOW - 2 * DAY });
});

test('with no read, the digest has no data and windowed counts are null', () => {
  const d = digest({}, ['missing'], judge(), null);
  assert.equal(d.hasData, false);
  assert.equal(d.totalAwaiting, 0);
  assert.equal(windowed({}, 'missing', judge()), null);
});

// ---- The awaiting split

test('closers come out of the count and aged ones move to backlog', () => {
  const s = read([
    waiting('live-ask', 'kitna charge hoga', NOW - 2 * HOUR), waiting('live-ask-2', 'can I book for tomorrow?', NOW - DAY),
    waiting('old-ask', 'do you do bridal makeup', NOW - 40 * DAY), waiting('closer', 'Ok thanks', NOW - 3 * HOUR),
    waiting('closer-old', 'ji', NOW - 60 * DAY),
  ]);
  const split = awaitingSplit(s, ['inst'], judge(), 7);
  assert.deepEqual([split.needsReply, split.backlog, split.closedAutomatically], [2, 1, 2]);
});

test('every waiting chat lands in exactly one bucket', () => {
  const chats = [
    waiting('a', 'kya rate hai', NOW), waiting('b', 'ok', NOW), waiting('c', '', NOW - 30 * DAY), waiting('d', 'Photo', NOW - 2 * DAY),
    waiting('e', '👍', NOW - 90 * DAY), waiting('f', 'Walaikum us salam', NOW), waiting('g', 'near chandni chok', NOW - 9 * DAY),
    chat('not-awaiting', { preview: 'we replied' }),
  ];
  const split = awaitingSplit(read(chats), ['inst'], judge(), 7);
  assert.equal(split.needsReply + split.backlog + split.closedAutomatically, chats.filter((c) => c.awaiting).length);
});

test('a chat with no readable preview is counted and reported as unreadable', () => {
  const s = read([waiting('blank-1', '', NOW - HOUR), waiting('blank-2', '   ', NOW - 2 * HOUR), waiting('readable', 'kitna time lagega', NOW - 3 * HOUR)]);
  const split = awaitingSplit(s, ['inst'], judge(), 7);
  assert.deepEqual([split.needsReply, split.unreadable, split.closedAutomatically], [3, 2, 0]);
});

test('unreadable counts only the live queue, not the backlog', () => {
  const split = awaitingSplit(read([waiting('blank-live', '', NOW - HOUR), waiting('blank-old', '', NOW - 30 * DAY)]), ['inst'], judge(), 7);
  assert.deepEqual([split.unreadable, split.needsReply, split.backlog], [1, 1, 1]);
});

test('a manually handled chat is not reported as automatically closed', () => {
  const s = read([waiting('handled', 'kitna charge', NOW - HOUR)]);
  assert.equal(awaitingSplit(s, ['inst'], judge(), 7).needsReply, 1);
  const j = judge();
  markHandled(j.overrides, 'inst', 'handled', NOW - HOUR);
  const split = awaitingSplit(s, ['inst'], j, 7);
  assert.deepEqual([split.needsReply, split.closedAutomatically], [0, 0]);
});

test('the excluded list says which chats and why', () => {
  const closed = automaticallyClosed(read([waiting('ack', 'Ok thanks', NOW), waiting('emoji', '👍', NOW), waiting('real', 'kab open hota hai', NOW)]), ['inst'], judge());
  assert.equal(closed.length, 2);
  assert.ok(!closed.some((c) => c.chat.conversationKey === 'real'));
  assert.ok(closed.every((c) => explain(c.verdict.reason).trim()));
  assert.deepEqual(closed.map((c) => c.verdict.reason).sort(), ['acknowledgement', 'emojiOnly']);
});

test('the headline count and the list underneath it always agree', () => {
  const s = read([waiting('ask', 'price kya hai', NOW), waiting('ack', 'ok', NOW), waiting('blank', '', NOW), waiting('emoji', '😍', NOW)]);
  const counts = windowed(s, 'inst', judge())!;
  const listed = awaitingChats(s, 'inst', judge());
  assert.equal(counts.active - counts.caughtUp, listed.length);
  assert.equal(listed.length, 2);
});

test('the local model can only close a chat, and the setting restores the raw number', () => {
  const s = read([waiting('unplaced', 'near chandni chok', NOW), waiting('closer', 'ok', NOW)]);
  assert.deepEqual(keys(awaitingChats(s, 'inst', judge({ aiNeedsReply: () => false }))), []);
  assert.deepEqual(keys(awaitingChats(s, 'inst', judge({ aiNeedsReply: () => true }))), ['unplaced']);
  assert.deepEqual(automaticallyClosed(s, ['inst'], judge({ aiNeedsReply: () => false })).map((c) => c.verdict.reason).sort(), ['acknowledgement', 'aiJudgedClosed']);
  assert.deepEqual(keys(awaitingChats(s, 'inst', judge({ filterClosed: false }))).sort(), ['closer', 'unplaced']);
});

// ---- The cold-scan trap

test('a cold scan cannot close the whole queue', () => {
  const chats = Array.from({ length: 10 }, (_, i) => chat(`9200000000${i}@c.us`,
    { awaiting: true, lastActivity: NOW - 30 * DAY, lastMessageType: 'chat', hasLastMessage: i === 0 }));
  assert.equal(awaitingChats({ acct: { capturedAt: NOW, chats: distrustColdScan(chats) } }, 'acct', judge()).length, 10);
});

test('a warm scan is still trusted to close the ones that are gone', () => {
  const chats = Array.from({ length: 10 }, (_, i) => chat(`9230000000${i}@c.us`, {
    awaiting: true, lastActivity: NOW - 30 * DAY, lastMessageType: 'chat', hasLastMessage: i !== 0, preview: i === 0 ? '' : 'kitna charge hoga',
  }));
  assert.equal(awaitingChats({ acct: { capturedAt: NOW, chats: distrustColdScan(chats) } }, 'acct', judge()).length, 9);
});

// ---- Persistence (the file itself belongs to the app layer)

test('a snapshot survives a save and reload', () => {
  const written = read([chat('a@c.us', { unread: 2, awaiting: true, preview: 'preview' }), chat('b@c.us', { preview: 'preview' })]);
  const reloaded: Snapshots = JSON.parse(JSON.stringify(written));
  assert.deepEqual(windowed(reloaded, 'inst', judge()), { active: 2, caughtUp: 1 });
  assert.equal(awaitingChats(reloaded, 'inst', judge()).length, 1);
  assert.equal(lastCaptured(reloaded), NOW);
});

test('a fresh read after reload replaces the account', () => {
  const reloaded: Snapshots = JSON.parse(JSON.stringify(read([chat('a@c.us', { unread: 5, awaiting: true, preview: 'preview' })])));
  read([chat('a@c.us', { preview: 'thanks!', lastMessageFromMe: true })], reloaded);
  assert.deepEqual(awaitingChats(reloaded, 'inst', judge()), []);
});
