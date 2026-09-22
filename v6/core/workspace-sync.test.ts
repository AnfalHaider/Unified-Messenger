import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseConfig, type Config } from './config.ts';
import { accountsAllowed, applySetup, fromFs, readSetup, setupFor, setupKey, sharedSetup, toFields, toFs } from './workspace-sync.ts';

const config = (o: Record<string, unknown> = {}): Config => parseConfig({
  accounts: [
    { id: 'a1', name: 'Main branch WhatsApp', channel: 'whatsapp', url: 'https://web.whatsapp.com/', location: 'Main branch', professional: true, muted: true },
    { id: 'a2', name: 'Main branch Instagram', channel: 'instagram', url: 'https://www.instagram.com/', location: 'Main branch', professional: true },
  ],
  locations: [{ name: 'Main branch', slaMinutes: 20, hours: null }],
  holidays: [{ name: 'Sample Holiday', date: '2026-12-25', locations: [] }],
  settings: { slaMinutes: 15, theme: 'dark', quietHours: { enabled: true, startHour: 22, endHour: 7 }, savedReplies: [{ title: 'Hours', body: 'We open at 11.' }] },
  ...o,
}).config;

test('only the business setup is shared: never the theme, quiet hours, the assistant, or an account’s mute', () => {
  const s = sharedSetup(config());
  assert.deepEqual(Object.keys(s).sort(), ['accounts', 'locations', 'settings']);
  assert.deepEqual(Object.keys(s.settings).sort(), ['backlogAfterDays', 'filterClosedConversations', 'holidays', 'notCustomers', 'savedReplies', 'slaMinutes']);
  assert.ok(!('muted' in s.accounts[0]));
  const text = JSON.stringify(s);
  for (const personal of ['theme', 'quietHours', 'assistant', 'alerts', 'morningDigest', 'sleepUnusedAccounts', 'muted']) assert.ok(!text.includes(personal), personal);
  assert.equal(s.settings.holidays[0].name, 'Sample Holiday');
});

test('the fingerprint ignores key order and changes with any shared value', () => {
  const a = sharedSetup(config());
  const b = JSON.parse(JSON.stringify(a, Object.keys(a).reverse()));
  assert.equal(setupKey(a), setupKey({ ...b, accounts: a.accounts, locations: a.locations, settings: a.settings }));
  assert.notEqual(setupKey(a), setupKey({ ...a, settings: { ...a.settings, slaMinutes: 30 } }));
});

test('a setup from the workspace wins for what it shares, and this PC keeps its own settings and mutes', () => {
  const here = config();
  const remote = sharedSetup(config({ settings: { slaMinutes: 30 } }));
  remote.accounts[0].name = 'Renamed on another PC';
  const { config: next, added, removed } = applySetup(here, remote, new Set(['a1', 'a2']));
  assert.equal(next.settings.slaMinutes, 30);
  assert.equal(next.accounts[0].name, 'Renamed on another PC');
  assert.equal(next.accounts[0].muted, true, 'mute stays with this PC');
  assert.equal(next.settings.theme, 'dark', 'personal settings stay');
  assert.equal(next.settings.quietHours.startHour, 22);
  assert.deepEqual([added, removed], [[], []]);
});

test('accounts: new ones arrive, ones removed elsewhere go, and ones only ever on this PC stay', () => {
  const here = config({ accounts: [
    { id: 'a1', name: 'Main branch WhatsApp', channel: 'whatsapp', url: '', location: 'Main branch', professional: true },
    { id: 'a2', name: 'Main branch Instagram', channel: 'instagram', url: '', location: 'Main branch', professional: true },
    { id: 'local', name: 'Personal WhatsApp', channel: 'whatsapp', url: '', location: '', professional: false },
  ] });
  const remote = sharedSetup(here);
  remote.accounts = remote.accounts.filter((a) => a.id === 'a1');
  remote.accounts.push({ id: 'a3', name: 'North branch WhatsApp', channel: 'whatsapp', url: 'https://web.whatsapp.com/', location: 'Main branch', professional: true, sortOrder: 3, notes: '' });
  const r = applySetup(here, remote, new Set(['a1', 'a2']));
  assert.deepEqual(r.config.accounts.map((a) => a.id), ['a1', 'a3', 'local']);
  assert.deepEqual(r.added, ['a3']);
  assert.deepEqual(r.removed, ['a2']);
  assert.deepEqual([...r.synced].sort(), ['a1', 'a3']);
});

test('a broken setup cannot put anything invalid into this PC’s config', () => {
  const remote = sharedSetup(config());
  remote.accounts.push({ id: '', name: '', channel: 'nonsense', url: '', location: '', professional: true, sortOrder: 0, notes: '' });
  remote.settings.slaMinutes = 100000;
  const r = applySetup(config(), remote, new Set());
  assert.ok(r.config.accounts.every((a) => a.id));
  assert.ok(r.config.settings.slaMinutes <= 120);
  assert.ok(!r.synced.has(''));
});

test('Firestore values go there and back unchanged', () => {
  const setup = sharedSetup(config());
  const back = readSetup({ ...toFields(setup as unknown as Record<string, unknown>), updatedAt: { timestampValue: '2026-09-19T10:00:00Z' }, updatedBy: { stringValue: 'u1' } });
  assert.deepEqual({ accounts: back.accounts, locations: back.locations, settings: back.settings }, JSON.parse(JSON.stringify(setup)));
  assert.equal(back.updatedAt, Date.parse('2026-09-19T10:00:00Z'));
  assert.deepEqual(toFs(1.5), { doubleValue: 1.5 });
  assert.equal(fromFs({ integerValue: '42' }), 42);
  assert.deepEqual(readSetup(undefined).accounts, []);
});

test('a member sees the accounts their invitation named, and the branches those accounts are at', () => {
  const whole = sharedSetup(config({
    accounts: [
      { id: 'a1', name: 'DHA-2 WhatsApp', channel: 'whatsapp', url: 'https://web.whatsapp.com/', location: 'DHA-2', professional: true },
      { id: 'a2', name: 'DHA-2 Instagram', channel: 'instagram', url: 'https://www.instagram.com/', location: 'DHA-2', professional: true },
      { id: 'a3', name: 'F-11 WhatsApp', channel: 'whatsapp', url: 'https://web.whatsapp.com/', location: 'F-11', professional: true },
    ],
    locations: [{ name: 'DHA-2', slaMinutes: 20 }, { name: 'F-11', slaMinutes: 30 }],
  }));
  const dha = setupFor(whole, ['a1', 'a2']);
  assert.deepEqual(dha.accounts.map((a) => a.id), ['a1', 'a2']);
  assert.deepEqual(dha.locations.map((l) => l.name), ['DHA-2'], 'a branch with none of their accounts is not shown empty');
  // One account of a branch is enough to see that branch, and only that branch.
  assert.deepEqual(setupFor(whole, ['a3']).locations.map((l) => l.name), ['F-11']);

  // The business rules are not assets: everyone measures the same customer the same way.
  assert.deepEqual(dha.settings, whole.settings);

  // null is everything; an empty list is nothing, because an admin who ticked nothing has said something.
  assert.deepEqual(setupFor(whole, null), whole);
  assert.deepEqual(setupFor(whole, []).accounts, []);
  assert.deepEqual(setupFor(whole, []).locations, []);
});

test('what an admin ticked is stored as ids the workspace knows, once each, in its own order', () => {
  const whole = sharedSetup(config());
  assert.deepEqual(accountsAllowed(['a2', 'a1', 'a2'], whole), ['a1', 'a2'], 'no repeats, and the setup decides the order');
  assert.deepEqual(accountsAllowed(['a1', 'not-an-account'], whole), ['a1'], 'an id the workspace never had cannot be granted');
  assert.deepEqual(accountsAllowed(null, whole), null, 'not set means the whole business');
  assert.deepEqual(accountsAllowed('everything', whole), [], 'nonsense grants nothing rather than everything');
});
