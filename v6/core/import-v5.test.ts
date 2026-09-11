// The import runs once on a real install's files, so the cases that matter are the ones that would quietly lose
// or invent something: an unreadable row, an archived account, a location that only exists on an account.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { defaultSettings } from './config.ts';
import { importV5 } from './import-v5.ts';

const V5_ACCOUNT = {
  id: 'acc-1', displayName: 'Depilex DHA-2', profileName: 'whatsapp-acc-1', startUrl: 'https://web.whatsapp.com/',
  platform: 'whatsapp', category: 'Professional', sortOrder: 2, notificationsMuted: true, notes: 'front desk', branchKey: 'DHA-2',
};
const V5_SETTINGS = {
  slaThresholdMinutes: 20, awaitingBacklogAfterDays: 14, filterClosedConversations: false, themePreference: 'Dark',
  quietHoursEnabled: true, quietHoursStartHour: 22, quietHoursEndHour: 7, enableLocalAi: true, localAiModelName: 'phi3:mini',
  ollamaEndpoint: 'http://127.0.0.1:11434', idleSessionReapMinutes: 45, enableLazyWebViewLoading: true, startupWarmMode: 'WarmAll',
  workspaceProfiles: [{ locationKey: 'DHA-2', displayName: 'DHA-2', slaThresholdMinutes: 30, hours: { enabled: true, openMinutes: 660, closeMinutes: 1260, workingDays: [1, 2, 3, 4, 5, 6] } }],
};

test('an account comes across whole', () => {
  const { config, report } = importV5({ version: 3, instances: [V5_ACCOUNT] }, V5_SETTINGS);
  assert.deepEqual(config.accounts, [{
    id: 'acc-1', name: 'Depilex DHA-2', channel: 'whatsapp', url: 'https://web.whatsapp.com/', location: 'DHA-2',
    professional: true, muted: true, sortOrder: 2, notes: 'front desk',
  }]);
  assert.equal(report.accounts, 1);
  assert.deepEqual(report.skipped, []);
});

test('the file may be the wrapped store or a bare array', () => {
  assert.equal(importV5([V5_ACCOUNT], {}).config.accounts.length, 1);
  assert.equal(importV5({ instances: [V5_ACCOUNT] }, {}).config.accounts.length, 1);
  assert.equal(importV5(null, null).config.accounts.length, 0);
});

test('archived accounts stay behind, and are counted rather than forgotten', () => {
  const { config, report } = importV5({ instances: [V5_ACCOUNT], archivedInstances: [{ ...V5_ACCOUNT, id: 'old-1' }] }, {});
  assert.deepEqual(config.accounts.map((a) => a.id), ['acc-1']);
  assert.equal(report.archived, 1);
  assert.ok(report.notes.some((n) => n.includes('archived')));
});

test('a custom-URL account keeps its own address', () => {
  const [a] = importV5([{ id: 'c1', displayName: 'Booking site', platform: 'generic', startUrl: 'https://example.com/' }], {}).config.accounts;
  assert.equal(a.channel, 'custom');
  assert.equal(a.url, 'https://example.com/');
  assert.equal(a.professional, false);
});

test('a location is set, guessed from the name, or left alone — and guesses are reported', () => {
  const { config, report } = importV5([
    { ...V5_ACCOUNT, id: 'set' },
    { id: 'guess', displayName: 'Depilex F 11', platform: 'whatsapp', category: 'Professional' },
    { id: 'none', displayName: 'Head office', platform: 'whatsapp', category: 'Professional' },
    { id: 'personal', displayName: 'Depilex DHA-2 personal', platform: 'whatsapp', category: 'Personal' },
  ], {});
  assert.deepEqual(config.accounts.map((a) => a.location), ['DHA-2', 'F-11', '', '']);
  assert.deepEqual(report.guessedLocations, ['Depilex F 11 → F-11']);
});

test('locations come from the v5 profiles, and an account can create one', () => {
  const { config, report } = importV5([V5_ACCOUNT, { id: 'b', displayName: 'F-11 branch', platform: 'whatsapp', category: 'Professional', branchKey: 'F-11' }], V5_SETTINGS);
  assert.deepEqual(config.locations.map((l) => l.name), ['DHA-2', 'F-11']);
  assert.deepEqual(config.locations[0].hours, { enabled: true, openMinutes: 660, closeMinutes: 1260, workingDays: [1, 2, 3, 4, 5, 6] });
  assert.equal(config.locations[0].slaMinutes, 30);
  assert.equal(config.locations[1].slaMinutes, null);
  assert.deepEqual(report.locations, ['DHA-2', 'F-11']);
});

test('a location shown under another name in v5 is reported, not silently renamed', () => {
  const { config, report } = importV5([V5_ACCOUNT], { workspaceProfiles: [{ locationKey: 'DHA-2', displayName: 'DHA Phase 2' }] });
  assert.deepEqual(config.locations.map((l) => l.name), ['DHA-2']);
  assert.ok(report.notes.some((n) => n.includes('DHA Phase 2')));
});

test('settings carry across, and out-of-range values are brought inside the limits', () => {
  const { config } = importV5([], { ...V5_SETTINGS, slaThresholdMinutes: 999 });
  const s = config.settings;
  assert.equal(s.slaMinutes, 120);
  assert.equal(s.backlogAfterDays, 14);
  assert.equal(s.filterClosedConversations, false);
  assert.equal(s.theme, 'dark');
  assert.deepEqual(s.quietHours, { enabled: true, startHour: 22, endHour: 7 });
  assert.deepEqual(s.assistant, { enabled: true, model: 'phi3:mini', endpoint: 'http://127.0.0.1:11434/' });
});

test('accounts stay awake, and the report says what was not imported', () => {
  const { config, report } = importV5([], V5_SETTINGS);
  assert.equal(config.settings.sleepUnusedAccounts, false);
  assert.equal(config.settings.sleepAfterMinutes, 45);
  assert.equal(config.settings.readEverySeconds, defaultSettings().readEverySeconds);
  assert.ok(report.notes.some((n) => n.includes('awake')));
  assert.ok(report.notes.some((n) => n.includes('phi3:mini')));
});

test('a broken row is named and skipped while the rest come across', () => {
  const { config, report } = importV5([V5_ACCOUNT, { displayName: 'No id here' }, 'a bare string', { ...V5_ACCOUNT, id: 'acc-2' }], {});
  assert.deepEqual(config.accounts.map((a) => a.id), ['acc-1', 'acc-2']);
  assert.deepEqual(report.skipped, ['No id here', '(unreadable row)']);
});

test('nothing to import gives a usable empty config', () => {
  const { config, report } = importV5({ instances: [] }, {});
  assert.deepEqual(config.accounts, []);
  assert.deepEqual(config.settings, defaultSettings());
  assert.equal(report.accounts, 0);
});
