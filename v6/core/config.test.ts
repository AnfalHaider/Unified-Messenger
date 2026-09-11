// The config file is hand-editable and restorable from a backup, so every case here is a file that must not
// stop the app opening.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CHANNELS, defaultSettings, emptyConfig, parseConfig } from './config.ts';

const parse = (raw: unknown) => parseConfig(raw).config;
const account = (o: Record<string, unknown>) => parse({ accounts: [o] }).accounts[0];

test('a missing or unreadable file gives the defaults', () => {
  for (const raw of [null, undefined, 'not-an-object', [], 42]) assert.deepEqual(parse(raw), emptyConfig(), String(raw));
});

test('the defaults match the owner decisions: accounts awake, assistant off, closers filtered', () => {
  const s = defaultSettings();
  assert.equal(s.sleepUnusedAccounts, false);
  assert.equal(s.assistant.enabled, false);
  assert.equal(s.filterClosedConversations, true);
});

test('an account keeps what it has and fills in what it lacks', () => {
  const a = account({ id: 'acc-1', channel: 'whatsapp' });
  assert.deepEqual([a.name, a.url, a.location, a.professional, a.muted, a.sortOrder, a.notes],
    ['WhatsApp', CHANNELS.whatsapp.url, '', false, false, 0, '']);
  assert.equal(account({ id: 'acc-1', channel: 'whatsapp', url: 'https://web.whatsapp.com/other' }).url, 'https://web.whatsapp.com/other');
});

test('an unknown channel becomes a custom URL rather than pretending to be a messenger', () => {
  const a = account({ id: 'acc-1', channel: 'myspace', url: 'https://example.com/' });
  assert.equal(a.channel, 'custom');
  assert.equal(a.url, 'https://example.com/');
});

test('an account with no id, or a repeated one, is dropped and counted', () => {
  const { config, dropped } = parseConfig({ accounts: [{ id: 'a' }, { id: ' ' }, { id: 'A' }, 'a bare string', { id: 'b' }] });
  assert.deepEqual(config.accounts.map((a) => a.id), ['a', 'b']);
  assert.equal(dropped, 3);
});

test('wrong-typed fields fall back instead of throwing', () => {
  const config = parse({ accounts: { not: 'an array' }, locations: 7, settings: { slaMinutes: 'quick', assistant: 'yes', quietHours: [] } });
  assert.deepEqual(config.accounts, []);
  assert.deepEqual(config.locations, []);
  assert.deepEqual(config.settings, defaultSettings());
});

test('numbers are held inside their limits', () => {
  const settings = (o: Record<string, unknown>) => parse({ settings: o }).settings;
  assert.equal(settings({ slaMinutes: 1 }).slaMinutes, 5);
  assert.equal(settings({ slaMinutes: 500 }).slaMinutes, 120);
  assert.equal(settings({ backlogAfterDays: 0 }).backlogAfterDays, 1);
  assert.equal(settings({ readEverySeconds: 5 }).readEverySeconds, 15);
  assert.equal(settings({ quietHours: { startHour: 30 } }).quietHours.startHour, 23);
  assert.equal(settings({ assistant: { endpoint: 'http://127.0.0.1:11434' } }).assistant.endpoint, 'http://127.0.0.1:11434/');
});

test('locations need a name, and keep one entry each', () => {
  const config = parse({ locations: [{ name: 'DHA-2' }, { name: ' ' }, { name: 'dha-2', slaMinutes: 30 }, { name: 'F-11', slaMinutes: 999 }] });
  assert.deepEqual(config.locations.map((l) => [l.name, l.slaMinutes]), [['DHA-2', null], ['F-11', 120]]);
});

test('business hours are repaired rather than rejected', () => {
  const [location] = parse({ locations: [{ name: 'DHA-2', hours: { enabled: true, openMinutes: -5, closeMinutes: 5000, workingDays: [1, 1, 9, 'x'] } }] }).locations;
  assert.deepEqual(location.hours, { enabled: true, openMinutes: 0, closeMinutes: 1440, workingDays: [1] });
  assert.deepEqual(parse({ locations: [{ name: 'F-11', hours: { enabled: true, workingDays: [] } }] }).locations[0].hours,
    { enabled: true, openMinutes: 540, closeMinutes: 1080, workingDays: [1, 2, 3, 4, 5, 6] });
});

test('a saved config reloads as itself', () => {
  const config = parse({
    accounts: [{ id: 'a', name: 'DHA-2 salon', channel: 'whatsapp', location: 'DHA-2', professional: true, sortOrder: 2, notes: 'front desk' }],
    locations: [{ name: 'DHA-2', slaMinutes: 20, hours: { enabled: true, openMinutes: 600, closeMinutes: 1200, workingDays: [1, 2, 3] } }],
    settings: { slaMinutes: 20, assistant: { enabled: true, model: 'gemma3:4b' }, theme: 'dark' },
  });
  assert.deepEqual(parse(JSON.parse(JSON.stringify(config))), config);
});
