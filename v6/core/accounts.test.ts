import { test } from 'node:test';
import assert from 'node:assert/strict';
import { addAccount, editAccount, forgetAccount, removeAccount } from './accounts.ts';
import { parseConfig, type Config } from './config.ts';

const base = (): Config => parseConfig({
  accounts: [{ id: 'a1', name: 'Front desk', channel: 'whatsapp', professional: true, location: 'Main' }],
  locations: [{ name: 'Main', slaMinutes: 20 }],
}).config;

test('an account on a channel with a reader is counted; one without is only a page', () => {
  const { config, id, error } = addAccount(base(), { channel: 'instagram', name: 'Insta desk', location: 'Main' }, 'new-1');
  assert.equal(error, null);
  assert.equal(id, 'new-1');
  const added = config.accounts.find((a) => a.id === 'new-1')!;
  assert.deepEqual([added.name, added.channel, added.location, added.professional, added.url], ['Insta desk', 'instagram', 'Main', true, 'https://www.instagram.com/']);
  const page = addAccount(base(), { channel: 'telegram', name: '', location: '' }, 'new-2').config.accounts.find((a) => a.id === 'new-2')!;
  assert.deepEqual([page.name, page.professional], ['Telegram', false]);
});

test('a new location is created, and an existing one keeps its spelling and its rules', () => {
  const fresh = addAccount(base(), { channel: 'whatsapp', name: 'North desk', location: 'North' }, 'n').config;
  assert.deepEqual(fresh.locations.map((l) => l.name), ['Main', 'North']);
  const same = addAccount(base(), { channel: 'whatsapp', name: 'Second', location: '  main ' }, 's').config;
  assert.deepEqual(same.locations.map((l) => [l.name, l.slaMinutes]), [['Main', 20]]);
  assert.equal(same.accounts.find((a) => a.id === 's')!.location, 'Main');
});

test('another page needs a web address, and only a web address', () => {
  assert.match(addAccount(base(), { channel: 'custom', name: 'Booking site', location: '' }, 'c').error ?? '', /address/);
  assert.match(addAccount(base(), { channel: 'custom', name: 'x', location: '', url: 'file:///C:/secret' }, 'c').error ?? '', /address/);
  const ok = addAccount(base(), { channel: 'custom', name: 'Booking site', location: '', url: 'https://example.com/book' }, 'c');
  assert.equal(ok.error, null);
  assert.equal(ok.config.accounts.find((a) => a.id === 'c')!.url, 'https://example.com/book');
});

test('an unknown channel or a used id is refused, and the config is left as it was', () => {
  const before = base();
  const bad = addAccount(before, { channel: 'myspace', name: 'x', location: '' }, 'z');
  assert.ok(bad.error);
  assert.deepEqual(bad.config, before);
  assert.ok(addAccount(before, { channel: 'whatsapp', name: 'x', location: '' }, 'A1').error, 'ids are compared without case');
});

test('editing renames, moves and switches counting, and creates the location it moves to', () => {
  const { config, error } = editAccount(base(), 'a1', { name: '  Reception  ', location: 'East', professional: false });
  assert.equal(error, null);
  assert.deepEqual([config.accounts[0].name, config.accounts[0].location, config.accounts[0].professional], ['Reception', 'East', false]);
  assert.deepEqual(config.locations.map((l) => l.name), ['Main', 'East'], 'a location keeps its hours even with no accounts left');
  assert.ok(editAccount(base(), 'nope', { name: 'x', location: '', professional: true }).error);
  assert.equal(editAccount(base(), 'a1', { name: '', location: 'Main', professional: true }).config.accounts[0].name, 'WhatsApp', 'a blank name falls back to the channel');
});

test('removing an account takes it out of the config and out of every store keyed by it', () => {
  const { config, error } = removeAccount(base(), 'a1');
  assert.equal(error, null);
  assert.deepEqual(config.accounts, []);
  const stores = {
    snapshots: { a1: 1, b: 2 },
    overrides: { a1: {}, b: {} },
    history: { a1: {}, b: {} },
    times: { pending: { a1: {}, b: {} }, watchStart: { a1: 1, b: 2 }, samples: { a1: [], b: [] } },
    calls: { 'a1|k|1': { account: 'a1' }, 'b|k|1': { account: 'b' } },
    notified: { 'near:a1:k:1': 1, 'signed-out:a1': 2, 'call:a1:k:1': 3, 'near:b:k:1': 4 },
    events: { a1: [], b: [] },
  };
  forgetAccount('a1', stores);
  assert.deepEqual(stores, {
    snapshots: { b: 2 }, overrides: { b: {} }, history: { b: {} },
    times: { pending: { b: {} }, watchStart: { b: 2 }, samples: { b: [] } },
    calls: { 'b|k|1': { account: 'b' } }, notified: { 'near:b:k:1': 4 }, events: { b: [] },
  });
});
