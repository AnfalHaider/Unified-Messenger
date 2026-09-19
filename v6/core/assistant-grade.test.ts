import { test } from 'node:test';
import assert from 'node:assert/strict';
import { grade, inventedNumbers } from './assistant-grade.ts';

const FACTS = [
  'F2. The reply target is 15 minutes.',
  'F3. Customers waiting for a reply now: 10 (7 on WhatsApp, 3 on Instagram).',
  'F5. The longest wait is Sample Customer D, waiting 2 days 5 h.',
  'F6. Answered on time: 82%.',
  'F7. Marked handled: 2.',
].join('\n');

test('a right answer passes, and one missing the figure fails', () => {
  assert.equal(grade('10 customers are waiting.', { question: 'How many are waiting?', must: ['10'] }, FACTS).pass, true);
  assert.deepEqual(grade('Several customers are waiting.', { question: 'How many are waiting?', must: ['10'] }, FACTS), { pass: false, why: 'missing: 10' });
});

test('a number is matched whole: 2 is not found inside 82 (the first grader passed exactly that)', () => {
  const q = { question: 'How many chats were marked handled?', must: ['2'] };
  assert.deepEqual(grade('82% were answered on time.', q, FACTS), { pass: false, why: 'missing: 2' });
  assert.equal(grade('2 chats were marked handled.', q, FACTS).pass, true);
  const b = { question: 'How long has D waited?', must: ['2 days 5 h'] };
  assert.equal(grade('Sample Customer D has waited 2 days 5 h.', b, FACTS).pass, true);
  assert.equal(grade('Sample Customer D has waited 12 days 5 h.', b, FACTS).pass, false);
});

test('any number the facts do not contain fails the answer, even beside the right one', () => {
  assert.deepEqual(inventedNumbers('10 are waiting, about 12 by tonight.', FACTS, 'How many?'), ['12']);
  assert.deepEqual(grade('10 are waiting, about 12 by tonight.', { question: 'How many are waiting?', must: ['10'] }, FACTS), { pass: false, why: 'made-up figure: 12' });
  assert.deepEqual(grade('84% of chats have an answer.', { question: 'What share?', must: ['74'] }, FACTS), { pass: false, why: 'made-up figure: 84' });
});

test('the facts’ own labels and list numbers do not count as figures', () => {
  const facts = ['F12. The reply target is 15 minutes.', '7. Sample Customer A: waiting 3 min.'].join('\n');
  assert.deepEqual(inventedNumbers('12 are waiting, 7 of them late.', facts, 'How many?'), ['12', '7']);
  assert.deepEqual(inventedNumbers('The target is 15 minutes; A has waited 3 min.', facts, 'What is the target?'), []);
});

test('a question the facts do not cover must be declined, with no number at all', () => {
  const q = { question: 'How many reviews have no reply?', notCovered: true };
  assert.equal(grade("The app doesn't have that figure.", q, FACTS).pass, true);
  assert.equal(grade('The app doesn’t have that figure.', q, FACTS).pass, true, 'a curly apostrophe is the same answer');
  assert.equal(grade('3 reviews have no reply.', q, FACTS).pass, false);
  assert.equal(grade("The app doesn't have that figure, but 10 customers are waiting.", q, FACTS).pass, false);
});

test('one of several right wordings is enough, and a wrong name beside the right one fails', () => {
  const q = { question: 'Is Sample Customer A past the target?', anyOf: ['within the target', 'not past'] };
  assert.equal(grade('No, Sample Customer A is within the target.', q, FACTS).pass, true);
  assert.equal(grade('Yes.', q, FACTS).pass, false);
  const p = { question: 'Which customer asked about a price?', must: ['Sample Customer J'], mustNot: ['Sample Customer B'] };
  assert.deepEqual(grade('Sample Customer B and Sample Customer J.', p, FACTS), { pass: false, why: 'should not say: Sample Customer B' });
});
