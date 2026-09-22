// The one thing that matters here: a report can be sent to a stranger without anyone reading it first.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { supportReport, unsafeIn, type SupportInput } from './support-report.ts';
import { defaultSettings } from './config.ts';

const NOW = new Date(2026, 8, 22, 14, 0).getTime();

const input = (o: Partial<SupportInput> = {}): SupportInput => ({
  version: '6.0.1', electron: '44.3.0', node: '24.20.0', platform: 'win32 10.0.26200',
  now: NOW, settings: defaultSettings(),
  accounts: [{
    id: 'acct-1', name: 'DHA-2 WhatsApp', channel: 'whatsapp', location: 'DHA-2',
    awake: true, signedOut: false, lastReadAt: NOW - 90_000, chats: 500, waiting: 51,
    recent: ['read', 'read', 'failed'],
  }],
  modules: [{ id: 'whatsapp', name: 'WhatsApp', ok: 148, failed: 2, lastError: 'the page did not answer in time' }],
  log: ['{"t":"2026-09-22T09:00:00.000Z","event":"read","account":"acct-1","chats":500,"awaiting":51}'],
  ...o,
});

test('the report says what support needs: the build, each account, each reader, and the log', () => {
  const text = supportReport(input());
  assert.match(text, /Unified Messenger — report for support/);
  assert.match(text, /App 6\.0\.1 · Electron 44\.3\.0/);
  assert.match(text, /DHA-2 WhatsApp — whatsapp at DHA-2 — awake/);
  assert.match(text, /last read 2 min ago · 500 chats, 51 waiting/);
  assert.match(text, /recent: read read failed/);
  assert.match(text, /WhatsApp — 148 good, 2 failed · last error: the page did not answer in time/);
  assert.match(text, /500 chats per WhatsApp read/, 'the read limit explains a slow pass');
});

test('an account that has not read yet says so, rather than reading as a quiet one', () => {
  const text = supportReport(input({ accounts: [{ ...input().accounts[0], lastReadAt: null, chats: null, waiting: null, signedOut: true }] }));
  assert.match(text, /SIGNED OUT/);
  assert.match(text, /last read not yet this run/);
  assert.doesNotMatch(text, /\d+ chats, \d+ waiting/, 'nothing read is not nothing waiting');
});

test('nothing a customer wrote, is called, or signed in with can reach the report', () => {
  // Everything below is in the app somewhere; none of it is the report's business.
  const forbidden = ['Ayesha Khan', '+92 300 1234567', 'ok thanks see you tomorrow', 'desk@example.com', 'ya29.a0Ae'];
  const text = supportReport(input({
    accounts: [{ ...input().accounts[0], name: 'DHA-2 WhatsApp' }],
    log: ['{"t":"2026-09-22T09:00:00.000Z","event":"read","account":"acct-1","chats":500,"awaiting":51}'],
  }));
  assert.deepEqual(unsafeIn(text, forbidden), []);
  // And the guard itself works, or the assertion above proves nothing.
  assert.deepEqual(unsafeIn(`${text}\nAyesha Khan`, forbidden), ['Ayesha Khan']);
});

test('the report ends by saying what it does and does not carry', () => {
  const text = supportReport(input());
  assert.match(text, /no customer\s+names or numbers, no message or review text, and no logins/);
});
