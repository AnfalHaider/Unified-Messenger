import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answerFrom, answerQuestion, buildFacts, checkPrompt, NOT_COVERED, parseCheck, parseSelection, selectionPrompt, type Ask, type SummaryInput } from './assistant-summary.ts';

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

test('every figure is worked out here, one to a fact, with the names beside the counts', () => {
  const texts = buildFacts(input(), NOW).map((f) => f.text);
  for (const line of [
    '5 customers are waiting for a reply.',
    '4 customers are waiting on WhatsApp.',
    '1 customer is waiting on Instagram.',
    '3 customers are past the 15-minute target: Sample Customer D, Sample Customer K, Sample Customer H.',
    '1 customer will pass the target within 5 minutes: Sample Customer B.',
    '1 customer is still within the target: Sample Customer A.',
    'The longest wait is Sample Customer D, 2 days 5 h, on Main branch WhatsApp at Main branch.',
    'Today 35% of measured replies were within the target (65 replies measured, target 90%).',
    '2 chats were marked handled.',
    'Main branch: 4 customers waiting (3 on WhatsApp, 1 on Instagram), 2 past the target; the longest wait there is Sample Customer D, 2 days 5 h.',
    'The location with the most customers waiting is Main branch, with 4.',
    'North branch WhatsApp needs signing in again; until then its customers are not counted.',
    'Readers with problems: Instagram (intermittent).',
    'Sample Customer D is waiting on the account Main branch WhatsApp at Main branch, for 2 days 5 h, and is past the target; their last message was "Is the offer still on?".',
    'The backlog is 3 customers: those who have waited longer than 7 days, counted separately from the line.',
  ]) assert.ok(texts.includes(line), `missing: ${line}`);
});

test('customer facts carry their customer, figures do not', () => {
  const facts = buildFacts(input(), NOW);
  assert.equal(facts.find((f) => f.id === 'C1')?.customer, 'Sample Customer D');
  assert.equal(facts.find((f) => f.id === 'F1')?.customer, undefined);
});

test('the model only chooses: its answer becomes the app’s own facts, word for word', () => {
  const facts = buildFacts(input(), NOW);
  const prompt = selectionPrompt(facts);
  assert.match(prompt, /Reply with JSON only/);
  assert.ok(prompt.includes('C1: Sample Customer D is waiting on the account'));
  const waiting = facts.find((f) => f.text === '5 customers are waiting for a reply.')!;
  assert.equal(answerFrom(parseSelection(JSON.stringify({ facts: [waiting.id] }), facts)), '5 customers are waiting for a reply.');
  assert.equal(answerFrom(parseSelection('{"facts": []}', facts)), NOT_COVERED);
  assert.equal(answerFrom(parseSelection('{"facts": ["F999", "nonsense"]}', facts)), NOT_COVERED, 'a made-up id is ignored');
  assert.equal(parseSelection('I think C1 answers it.', facts)[0]?.customer, 'Sample Customer D', 'ids are found even outside JSON');
  assert.equal(parseSelection(JSON.stringify({ facts: ['F1', 'F2', 'F3', 'F4', 'F5', 'F6'] }), facts).length, 4, 'at most four');
});

test('nothing measured and nobody waiting are said, not left blank or zero', () => {
  const texts = buildFacts(input({ queue: [], queueTotal: 0, figures: [{ label: 'Answered on time', value: '—', unit: '%', note: '' }] }), NOW).map((f) => f.text);
  assert.ok(texts.includes('Nobody is waiting.'));
  assert.ok(texts.includes('No replies have been measured yet today, so there is no on-time figure.'));
  assert.ok(!texts.some((t) => t.startsWith('The location with the most customers waiting')));
});

test('a fact the check turns down is taken off the list, and the model chooses again', async () => {
  const facts = buildFacts(input(), NOW);
  const waiting = facts.find((f) => f.text === '5 customers are waiting for a reply.')!;
  const aside = facts.find((f) => f.text.startsWith('3 chats are set aside'))!;
  const offered: string[][] = [];
  // The model first chooses the wrong fact, which the check turns down; offered again without it, it chooses right.
  const ask: Ask = async (messages) => {
    const text = messages[0].content;
    if (text.startsWith('Does the fact below')) return JSON.stringify({ answers: text.endsWith(waiting.text) });
    offered.push(text.split('\n').filter((l) => /^[FC]\d+: /.test(l)).map((l) => l.split(':')[0]));
    return JSON.stringify({ facts: [offered.length === 1 ? aside.id : waiting.id] });
  };
  assert.deepEqual(await answerQuestion('How many are waiting?', facts, ask), [waiting]);
  assert.ok(!offered[1].includes(aside.id), 'the turned-down fact is not offered again');
  // Turned down every round, the answer is "not covered", never the closest fact.
  const never: Ask = async (m) => (m[0].content.startsWith('Does') ? '{"answers": false}' : JSON.stringify({ facts: ['F1'] }));
  assert.equal(answerFrom(await answerQuestion('How many missed calls?', facts, never)), NOT_COVERED);
});

test('the check asks one yes-or-no question, and anything but a clear yes is a no', () => {
  const fact = buildFacts(input(), NOW).find((f) => f.text.startsWith('Today 35%'))!;
  const prompt = checkPrompt('What was our on-time percentage last week?', fact);
  assert.match(prompt, /today is not yesterday or last week/);
  assert.ok(prompt.endsWith(`Fact: ${fact.text}`));
  assert.equal(parseCheck('{"answers": true}'), true);
  assert.equal(parseCheck('{"answers": false}'), false);
  assert.equal(parseCheck('maybe'), false);
  assert.equal(parseCheck(''), false);
});
