// Case-for-case port of ChatEntryParserTests.cs and ChatEntryParserResilienceTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseConversations, sanitizePreview } from './chat-entry.ts';

const parse = (json: string) => parseConversations(JSON.parse(json)).entries;
const root = (conversations: string) => parse(`{"conversations":${conversations}}`);
const GOOD_ROW = `{ "conversationKey": "923001234567@c.us", "customerName": "Ayesha", "unreadCount": 2,
  "lastActivityTimestampUtc": "2026-08-10T09:00:00Z", "lastMessagePreview": "is the salon open today?",
  "awaiting": true, "lastMessageFromMe": false, "contactPhone": "923001234567" }`;

// ---- ChatEntryParserTests

test('reads every field, including lastMessageFromMe', () => {
  const [entry, ...rest] = root(`[{ "conversationKey": "923001234567@c.us", "customerName": "Ayesha", "unreadCount": 2,
    "lastActivityTimestampUtc": "2026-07-10T08:30:00Z", "lastMessagePreview": "are you open today?",
    "awaiting": true, "lastMessageFromMe": false, "contactPhone": "923001234567" }]`);
  assert.equal(rest.length, 0);
  assert.equal(entry.conversationKey, '923001234567@c.us');
  assert.equal(entry.customerName, 'Ayesha');
  assert.equal(entry.unread, 2);
  assert.equal(entry.preview, 'are you open today?');
  assert.equal(entry.awaiting, true);
  assert.equal(entry.lastMessageFromMe, false);
  assert.equal(entry.contactPhone, '923001234567');
  assert.equal(new Date(entry.lastActivity).toISOString(), '2026-07-10T08:30:00.000Z');
});

test('fromMe true round-trips', () => {
  const [entry] = root('[{ "conversationKey": "x@c.us", "lastActivityTimestampUtc": "2026-07-10T08:30:00Z", "awaiting": false, "lastMessageFromMe": true }]');
  assert.equal(entry.lastMessageFromMe, true);
  assert.equal(entry.awaiting, false);
});

test('awaiting absent falls back to unread', () => {
  const entries = root(`[{ "conversationKey": "a@c.us", "unreadCount": 3, "lastActivityTimestampUtc": "2026-07-10T08:30:00Z" },
    { "conversationKey": "b@c.us", "unreadCount": 0, "lastActivityTimestampUtc": "2026-07-10T08:30:00Z" }]`);
  assert.equal(entries[0].awaiting, true);
  assert.equal(entries[1].awaiting, false);
});

test('skips a row with no parseable timestamp', () => {
  const entries = root(`[{ "conversationKey": "good@c.us", "lastActivityTimestampUtc": "2026-07-10T08:30:00Z" },
    { "conversationKey": "no-ts@c.us" }, { "conversationKey": "bad-ts@c.us", "lastActivityTimestampUtc": "not-a-date" }]`);
  assert.deepEqual(entries.map((e) => e.conversationKey), ['good@c.us']);
});

test('no conversations array returns empty', () => {
  assert.deepEqual(parse('{}'), []);
});

test('a shared contact card does not render its raw JID', () => {
  for (const p of ['102074813546715@lid', '7619322347741@lid', '923105325598@c.us', '923105325598@s.whatsapp.net']) {
    assert.equal(sanitizePreview(p), 'Shared a contact');
  }
});

test('ordinary text that contains an @ sign is left alone', () => {
  for (const p of ['meet me at cafe@dha', 'email me at info@depilex.com', '@lid', 'abc123@lid', 'Ok']) {
    assert.equal(sanitizePreview(p), p);
  }
});

// ---- ChatEntryParserResilienceTests

test('parses a well-formed row', () => {
  const [entry, ...rest] = root(`[${GOOD_ROW}]`);
  assert.equal(rest.length, 0);
  assert.equal(entry.customerName, 'Ayesha');
  assert.equal(entry.unread, 2);
  assert.equal(entry.awaiting, true);
});

test('a numeric timestamp skips only that row and does not discard the scan', () => {
  const entries = root(`[{ "conversationKey": "a", "lastActivityTimestampUtc": 1754812800000 }, ${GOOD_ROW}]`);
  assert.deepEqual(entries.map((e) => e.customerName), ['Ayesha']);
});

test('wrong-typed string fields degrade to empty rather than throwing', () => {
  const [entry] = root(`[{ "conversationKey": 12345, "customerName": true, "lastMessagePreview": { "nested": "object" },
    "contactPhone": 923001234567, "unreadCount": 1, "lastActivityTimestampUtc": "2026-08-10T09:00:00Z" }]`);
  assert.equal(entry.conversationKey, '');
  assert.equal(entry.customerName, '');
  assert.equal(entry.preview, '');
  assert.equal(entry.contactPhone, '');
});

test('one bad row among many good ones costs only itself', () => {
  const result = parseConversations(JSON.parse(`{"conversations":[${GOOD_ROW}, { "lastActivityTimestampUtc": "not-a-date" }, ${GOOD_ROW},
    "a bare string where an object belongs", ${GOOD_ROW}]}`));
  assert.equal(result.entries.length, 3);
  assert.equal(result.skipped, 2);
});

test('rows with no timestamp are dropped, not stamped with now', () => {
  assert.deepEqual(parse('{ "conversations": [{ "conversationKey": "a", "unreadCount": 3 }] }'), []);
});

test('missing awaiting falls back to unread, and is reported', () => {
  const withUnread = parseConversations(JSON.parse('{ "conversations": [{ "unreadCount": 4, "lastActivityTimestampUtc": "2026-08-10T09:00:00Z" }] }'));
  assert.equal(withUnread.entries[0].awaiting, true);
  assert.equal(withUnread.awaitingInferred, 1);
  assert.equal(parse('{ "conversations": [{ "unreadCount": 0, "lastActivityTimestampUtc": "2026-08-10T09:00:00Z" }] }')[0].awaiting, false);
});

test('explicit awaiting false beats a non-zero unread count', () => {
  const [entry] = root('[{ "unreadCount": 7, "awaiting": false, "lastMessageFromMe": true, "lastActivityTimestampUtc": "2026-08-10T09:00:00Z" }]');
  assert.equal(entry.awaiting, false);
  assert.equal(entry.lastMessageFromMe, true);
});

test('malformed roots yield an empty list rather than throwing', () => {
  for (const raw of ['{ "conversations": [] }', '{ "conversations": null }', '{ "conversations": "not-an-array" }', '{ "somethingElse": 1 }', '{}', '[]', 'null']) {
    assert.deepEqual(parse(raw), [], raw);
  }
});

test('an unread count that is not an integer defaults to zero', () => {
  const [entry] = root('[{ "unreadCount": "lots", "awaiting": true, "lastActivityTimestampUtc": "2026-08-10T09:00:00Z" }]');
  assert.equal(entry.unread, 0);
  assert.equal(entry.awaiting, true);
});

test('timestamps are normalised to UTC', () => {
  const [entry] = root('[{ "awaiting": true, "lastActivityTimestampUtc": "2026-08-10T14:00:00+05:00" }]');
  assert.equal(new Date(entry.lastActivity).getUTCHours(), 9);
});
