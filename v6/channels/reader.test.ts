// Every module, against made-up page output. The fixtures are invented: real customer data is only ever read
// live, on the owner's machine, and never checked in.
//
// The property that matters most here is containment. A page that changed shape overnight must cost its own
// read and nothing else, so `parse` is asked to survive everything a broken page could hand back.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { MODULES, moduleFor } from './index.ts';
import { looksUnsynced } from './instagram/index.ts';

const scan = (rows: unknown[]) => JSON.stringify({ conversations: rows, diag: { stage: 'ok' } });

const GOOD = scan([
  {
    conversationKey: '923001234567@c.us', customerName: 'Ayesha', unreadCount: 2,
    lastActivityTimestampUtc: '2026-09-12T09:00:00Z', lastMessagePreview: 'kitna charge hoga',
    awaiting: true, lastMessageFromMe: false, contactPhone: '923001234567', hasLastMessage: true, lastMessageType: 'chat',
  },
  {
    conversationKey: '923009999999@c.us', customerName: 'Bilal', unreadCount: 0,
    lastActivityTimestampUtc: '2026-09-12T08:30:00Z', lastMessagePreview: 'ok thanks',
    awaiting: false, lastMessageFromMe: true, hasLastMessage: true, lastMessageType: 'chat',
  },
  // Not a customer: a group. Every module funnels through the same filter, so none of them can let it in.
  {
    conversationKey: '120363000000@g.us', customerName: 'Staff', unreadCount: 9,
    lastActivityTimestampUtc: '2026-09-12T08:00:00Z', lastMessagePreview: 'rota', awaiting: true,
  },
]);

/** Instagram's reader has its own shape: thread metadata from the Relay prefetch, and the client's badge. */
const ig = (rows: unknown[], extra: Record<string, unknown> = {}) =>
  JSON.stringify({ conversations: rows, unreadBadge: 1, unreadBadgeCapped: false, diag: { stage: 'done' }, ...extra });
const GOOD_IG = ig([
  { key: '340282366841710300949128', name: 'Ayesha', username: 'ayesha.k', unread: 1, awaiting: true, lastActivityMs: 1_757_667_600_000 },
  { key: '340282366841710300949129', name: 'Bilal', username: 'bilal', unread: 0, awaiting: false, lastActivityMs: 1_757_665_800_000 },
]);
const GOOD_FOR: Record<string, string> = { whatsapp: GOOD, whatsappbusiness: GOOD, instagram: GOOD_IG };

/** Anything a page could hand back when it has changed, failed, or not finished loading. */
const HOSTILE: unknown[] = [
  '', null, undefined, 0, false,
  'not json at all',
  '{"conversations": "not an array"}',
  '{"conversations": [null, 4, "five"]}',
  '{}', '[]', 'null',
  '{"conversations":[{"conversationKey":"x@c.us"}]}', // no timestamp: unusable row
  { conversations: [] }, // already-parsed object rather than a string
];

for (const [channel, module] of Object.entries(MODULES)) {
  test(`${channel}: a good read becomes customer chats`, () => {
    const { entries, skipped } = module.parse(GOOD_FOR[channel]);
    assert.deepEqual(entries.map((e) => e.customerName), ['Ayesha', 'Bilal']);
    assert.equal(entries[0].awaiting, true);
    if (channel !== 'instagram') assert.equal(entries[0].preview, 'kitna charge hoga');
    assert.equal(skipped, 0);
  });

  test(`${channel}: nothing a broken page returns can throw`, () => {
    for (const raw of HOSTILE) {
      const result = module.parse(raw);
      assert.ok(Array.isArray(result.entries), `entries missing for ${JSON.stringify(raw)}`);
      assert.ok(result.skipped >= 0);
    }
  });

  test(`${channel}: an empty page is an empty read, not a bad one`, () => {
    const result = module.parse('');
    assert.deepEqual(result.entries, []);
    assert.equal(result.skipped, 0);
  });

  test(`${channel}: a page with no reader installed yet is not a failed read`, () => {
    // The shell counts a health failure from an empty read. A page that has not finished bringing its reader
    // up must say so, or every start would report the channel as broken for its first few seconds.
    assert.equal(module.parse('').notReady, true);
    assert.ok(!module.parse(GOOD_FOR[channel]).notReady);
  });

  test(`${channel}: the page script and both probes are present`, () => {
    const injected = module.inject((file) => `/* ${file} */`);
    assert.match(injected, /\/\* .*\.js \*\//);
    assert.ok(module.scan.length > 0);
    assert.match(module.signedOutProbe, /qr|login/);
  });
}

test('one broken module cannot take another down', () => {
  // The shell reads account by account; this pins the boundary the shell relies on. A module handed rubbish
  // still returns a result, so the loop moves on to the next account instead of ending the pass.
  const broken = moduleFor('whatsapp')!.parse('<html>Something went very wrong</html>');
  const fine = moduleFor('instagram')!.parse(GOOD_IG);
  assert.equal(broken.entries.length, 0);
  assert.equal(broken.skipped, 1);
  assert.equal(fine.entries.length, 2);
});

test('a channel with no reader is absent rather than empty', () => {
  // Google is reviews only, and Telegram has no reader yet. Asking for one must come back undefined so the
  // screens can say "no figures from this channel" instead of showing a confident zero.
  assert.equal(moduleFor('googlebusiness'), undefined);
  assert.equal(moduleFor('telegram'), undefined);
  assert.equal(moduleFor('whatsappbusiness')?.name, 'WhatsApp Business');
});

test('WhatsApp Web that has not built its stores yet is not a broken reader', () => {
  // The bridge names the stage it stopped at. Before WhatsApp's own models exist there is nothing to read,
  // which is a page still loading, not a reader that changed shape.
  const whatsapp = moduleFor('whatsapp')!;
  const early = (stage: string) => whatsapp.parse(JSON.stringify({ conversations: [], diag: { stage } }));
  assert.equal(early('no-store').notReady, true);
  assert.equal(early('no-models').notReady, true);
  // Loaded, scanned, and genuinely nothing waiting: that IS an answer, and must be treated as one.
  assert.ok(!early('empty').notReady);
});

test('Instagram: a thread with no title falls back to the handle, then the key', () => {
  const { entries } = moduleFor('instagram')!.parse(ig([
    { key: 'k1', name: '', username: 'new.customer', awaiting: false, lastActivityMs: 1 },
    { key: 'k2', name: '', username: '', awaiting: false, lastActivityMs: 1 },
    { name: 'No key at all', awaiting: true },
  ]));
  assert.deepEqual(entries.map((e) => e.customerName), ['@new.customer', 'k2']);
});

test('Instagram: a read taken before read state has synced is dropped, not believed', () => {
  // Measured in v5: 15 of 15 threads flagged unread against a badge of 2, a minute after launch.
  const early = moduleFor('instagram')!.parse(ig(
    Array.from({ length: 15 }, (_, i) => ({ key: `k${i}`, name: `C${i}`, awaiting: true, lastActivityMs: 1 })),
    { unreadBadge: 2 },
  ));
  assert.equal(early.notReady, true);
  assert.deepEqual(early.entries, []);
  assert.equal(looksUnsynced(15, 20, false), false, 'fewer than the badge is the top of Primary, not a fault');
  assert.equal(looksUnsynced(15, 9, true), false, 'a capped badge cannot be compared');
});

test('Instagram: a read that did not complete is a failed read', () => {
  const r = moduleFor('instagram')!.parse(JSON.stringify({ conversations: [], diag: { stage: 'no-relay' } }));
  assert.equal(r.skipped, 1);
  assert.ok(!r.notReady);
});
