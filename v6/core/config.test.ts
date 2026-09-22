// The config file is hand-editable and restorable from a backup, so every case here is a file that must not
// stop the app opening.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CHANNELS, defaultSettings, emptyConfig, hoursFor, parseConfig, WHATSAPP_CHATS_DEFAULT, WHATSAPP_CHATS_MAX, WHATSAPP_CHATS_MIN } from './config.ts';

const parse = (raw: unknown) => parseConfig(raw).config;
const account = (o: Record<string, unknown>) => parse({ accounts: [o] }).accounts[0];

test('a missing or unreadable file gives the defaults', () => {
  for (const raw of [null, undefined, 'not-an-object', [], 42]) assert.deepEqual(parse(raw), emptyConfig(), String(raw));
});

test('the defaults match the owner decisions: accounts awake, assistant off, closers filtered', () => {
  const s = defaultSettings();
  assert.equal(s.sleepUnusedAccounts, false);
  assert.equal(s.closeToBackground, true, 'closing keeps the app reading unless the owner turns it off');
  assert.equal(s.assistant.enabled, false);
  // Google's own API is off until Google grants this app access (3.1b): reviews come from the page until then.
  assert.equal(s.googleApi.enabled, false);
  assert.equal(s.filterClosedConversations, true);
  assert.equal(s.readLimits.whatsappChats, WHATSAPP_CHATS_DEFAULT, 'what v5 always read, until the owner says otherwise');
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
  assert.equal(settings({ googleApi: 'yes' }).googleApi.enabled, false, 'nonsense leaves it off');
  assert.equal(settings({ googleApi: { enabled: true } }).googleApi.enabled, true);
  // The chats one WhatsApp read takes in: the owner chooses, but never outside what the reader can stand.
  assert.equal(settings({ readLimits: { whatsappChats: 1 } }).readLimits.whatsappChats, WHATSAPP_CHATS_MIN);
  assert.equal(settings({ readLimits: { whatsappChats: 99999 } }).readLimits.whatsappChats, WHATSAPP_CHATS_MAX);
  assert.equal(settings({ readLimits: 'lots' }).readLimits.whatsappChats, WHATSAPP_CHATS_DEFAULT, 'nonsense leaves the default');
  assert.equal(settings({ readLimits: { whatsappChats: 1000 } }).readLimits.whatsappChats, 1000);
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
    accounts: [{ id: 'a', name: 'DHA-2 front desk', channel: 'whatsapp', location: 'DHA-2', professional: true, sortOrder: 2, notes: 'front desk' }],
    locations: [{ name: 'DHA-2', slaMinutes: 20, hours: { enabled: true, openMinutes: 600, closeMinutes: 1200, workingDays: [1, 2, 3] } }],
    settings: { slaMinutes: 20, assistant: { enabled: true, model: 'gemma3:4b' }, theme: 'dark' },
  });
  assert.deepEqual(parse(JSON.parse(JSON.stringify(config))), config);
});

test('accounts that spell a location differently land in one group', () => {
  // Real data: the WhatsApp account guessed "Men-DHA-2" and the Instagram one "Men-Dha-2", which the
  // rollup would have shown as two branches.
  const { config } = parseConfig({
    accounts: [
      { id: 'a', name: 'A', channel: 'whatsapp', location: 'Men-DHA-2', professional: true },
      { id: 'b', name: 'B', channel: 'instagram', location: 'men-dha-2', professional: true },
    ],
    locations: [{ name: 'Men-DHA-2' }],
  });
  assert.deepEqual(config.accounts.map((a) => a.location), ['Men-DHA-2', 'Men-DHA-2']);
});

test('alerts are on by default, and each can be switched off on its own', () => {
  assert.deepEqual(defaultSettings().alerts, { nearTarget: true, waitedHour: true, signedOut: true, callNotReturned: true, readerStopped: true, unhappyReview: true });
  assert.deepEqual(parse({ settings: { alerts: { waitedHour: false, signedOut: 'no' } } }).settings.alerts, { nearTarget: true, waitedHour: false, signedOut: true, callNotReturned: true, readerStopped: true, unhappyReview: true });
});

test('the weekly report leaves names out and saves nothing on its own until asked', () => {
  const d = defaultSettings().weeklyReport;
  assert.equal(d.autoSave, false);
  assert.equal(d.include.names, false);
  const parsed = parse({ settings: { weeklyReport: { autoSave: true, include: { names: true, calls: 'yes' } } } }).settings.weeklyReport;
  assert.deepEqual(parsed, { autoSave: true, include: { figures: true, locations: true, accounts: true, calls: true, names: true } });
});

test('per-day hours are read, kept inside the day, and a day that closes before it opens is closed', () => {
  const [loc] = parse({ locations: [{ name: 'Main', hours: { enabled: true, week: [
    null, { open: 660, close: 1260 }, { open: 900, close: 600 }, { open: -5, close: 2000 }, 'x', { open: 660, close: 1260 }, { open: 660, close: 1260 },
  ] } }] }).locations;
  assert.deepEqual(loc.hours?.week, [null, { open: 660, close: 1260 }, null, { open: 0, close: 1440 }, null, { open: 660, close: 1260 }, { open: 660, close: 1260 }]);
});

test('holidays keep a name, a real date and where they apply; anything else is dropped', () => {
  const { holidays } = parse({ locations: [{ name: 'Main' }, { name: 'North' }], holidays: [
    { name: 'National day', date: '2026-12-25', locations: [] },
    { name: 'Staff day', date: '2026-10-02', locations: ['main', 'Nowhere'] },
    { name: 'Bad date', date: '25/12/2026' },
    { name: '', date: '2026-11-01' },
    { name: 'Duplicate', date: '2026-12-25', locations: [] },
  ] });
  assert.deepEqual(holidays, [
    { name: 'Staff day', date: '2026-10-02', locations: ['Main'] },
    { name: 'National day', date: '2026-12-25', locations: [] },
  ]);
});

test('the dates a location is closed come from the holidays that apply to it', () => {
  const config = parse({ locations: [{ name: 'Main', hours: { enabled: true } }, { name: 'North' }], holidays: [
    { name: 'Everyone', date: '2026-12-25', locations: [] }, { name: 'Main only', date: '2026-10-02', locations: ['Main'] },
  ] });
  assert.deepEqual(hoursFor(config, 'Main')?.closedDates, ['2026-10-02', '2026-12-25']);
  assert.equal(hoursFor(config, 'North'), null, 'a location with no hours counts around the clock');
  assert.equal(hoursFor(config, ''), null);
});
