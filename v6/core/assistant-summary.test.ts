import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildSummary, type SummaryInput } from './assistant-summary.ts';

const NOW = new Date(2026, 8, 19, 15, 41);

const row = (customer: string, location: string, channel: string, waited: number, tone: 'ok' | 'due' | 'late', preview = '') =>
  ({ customer, accountName: `${location} ${channel === 'instagram' ? 'Instagram' : 'WhatsApp'}`, location, channel, waited, tone, preview });

const input = (o: Partial<SummaryInput> = {}): SummaryInput => ({
  freshness: { text: 'Updated just now' },
  settings: { slaMinutes: 15, backlogAfterDays: 7 },
  queue: [
    row('Sample Customer D', 'Main branch', 'whatsapp', 3180, 'late', 'Is the offer still on?'),
    row('Sample Customer K', 'North branch', 'whatsapp', 180, 'late'),
    row('Sample Customer H', 'Main branch', 'instagram', 18, 'late'),
    row('Sample Customer B', 'Main branch', 'whatsapp', 12, 'due'),
    row('Sample Customer A', 'Main branch', 'whatsapp', 3, 'ok'),
  ],
  queueTotal: 5,
  split: { needsReply: 5, backlog: 3, closedAutomatically: 4 },
  figures: [
    { label: 'Answered on time', value: '35', unit: '%', note: '65 replies measured, target 90%' },
    { label: 'First reply', value: '20', unit: 'min median', note: '65 replies measured' },
    { label: 'Caught up', value: '7', unit: '%', note: '' },
  ],
  setAside: [{ why: 'Handled' }, { why: 'Handled' }, { why: 'Not a customer' }],
  setAsideTotal: 3,
  accounts: [
    { name: 'Main branch WhatsApp', channel: 'whatsapp', location: 'Main branch', signedOut: false, counted: true, reads: true },
    { name: 'Main branch Instagram', channel: 'instagram', location: 'Main branch', signedOut: false, counted: true, reads: true },
    { name: 'North branch WhatsApp', channel: 'whatsapp', location: 'North branch', signedOut: true, counted: true, reads: true },
    { name: 'North branch Google', channel: 'googlebusiness', location: 'North branch', signedOut: false, counted: false, reads: false },
  ],
  modules: [{ name: 'WhatsApp', status: 'Healthy', tone: 'ok' }, { name: 'Instagram', status: 'Intermittent', tone: 'due' }],
  ...o,
});

test('every figure the assistant may quote is worked out here, so the model never counts', () => {
  const text = buildSummary(input(), NOW);
  for (const line of [
    'Customers waiting for a reply now: 5 (4 on WhatsApp, 1 on Instagram).',
    'Customers past the 15-minute target: 3. Customers who will pass it within 5 minutes: 1.',
    'The longest wait is Sample Customer D, waiting 2 days 5 h, on Main branch WhatsApp at Main branch.',
    'Answered on time: 35% (65 replies measured, target 90%).',
    'Median first reply: 20 minutes (65 replies measured).',
    'Backlog: 3 customers have waited longer than 7 days',
    'Set aside (off the line without a reply): 3 in all: 2 handled, 0 snoozed, 0 closed by rule, 1 not a customer.',
    'Main branch: 4 customers waiting (3 on WhatsApp, 1 on Instagram), 2 past the target; longest Sample Customer D, 2 days 5 h.',
    'North branch: 1 customer waiting (1 on WhatsApp), 1 past the target; longest Sample Customer K, 3 h.',
    'Accounts that need signing in again, so their customers are not being counted: North branch WhatsApp.',
    'Readers with problems: Instagram (intermittent).',
    '1. Sample Customer D: waiting 2 days 5 h on Main branch WhatsApp at Main branch, past the target; last message: "Is the offer still on?".',
  ]) assert.ok(text.includes(line), `missing: ${line}`);
});

test('the rules come first: only the facts, exact figures, and a set answer when the facts do not cover it', () => {
  const text = buildSummary(input(), NOW);
  assert.match(text, /Answer ONLY from the facts below/);
  assert.match(text, /The app doesn't have that figure\./);
  assert.ok(text.indexOf('FACTS:') > text.indexOf('Answer ONLY'));
});

test('nothing measured and nobody waiting are said, not left blank or zero', () => {
  const text = buildSummary(input({ queue: [], queueTotal: 0, figures: [{ label: 'Answered on time', value: '—', unit: '%', note: '' }] }), NOW);
  assert.ok(text.includes('Nobody is waiting.'));
  assert.ok(text.includes('Answered on time: no replies have been measured yet.'));
  assert.ok(text.includes('WAITING CUSTOMERS: none.'));
});

test('a long line is cut to the first customers and says how many there are in all', () => {
  const many = Array.from({ length: 40 }, (_, i) => row(`Sample Customer ${i}`, 'Main branch', 'whatsapp', 100 - i, 'late'));
  const text = buildSummary(input({ queue: many, queueTotal: 40 }), NOW, 25);
  assert.ok(text.includes('WAITING CUSTOMERS, longest first (the first 25 of 40):'));
  assert.ok(text.includes('25. Sample Customer 24'));
  assert.ok(!text.includes('26. '));
});
