import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseDrafts, replyPrompt, transcript, type ChatLine } from './assistant-reply.ts';

const line = (fromMe: boolean, text: string, t = 0): ChatLine => ({ fromMe, text, t });

test('the conversation is written oldest first, as the customer and us', () => {
  const { text, used } = transcript([line(false, 'Hi, is the order ready?'), line(true, 'Checking now.'), line(false, '[photo] this one')]);
  assert.equal(text, 'Customer: Hi, is the order ready?\nUs: Checking now.\nCustomer: [photo] this one');
  assert.equal(used, 3);
});

test('a long chat keeps its newest messages and drops the oldest to fit', () => {
  const lines = Array.from({ length: 100 }, (_, i) => line(i % 2 === 0, `message ${i} ${'x'.repeat(40)}`));
  const { text, used } = transcript(lines, 600);
  assert.ok(used < 100 && used > 0);
  assert.ok(text.endsWith(`message 99 ${'x'.repeat(40)}`));
  assert.ok(!text.includes('message 0 '));
  assert.ok(text.length <= 600);
});

test('a single message longer than the budget is still sent, rather than nothing', () => {
  assert.equal(transcript([line(false, 'y'.repeat(50))], 10).used, 1);
});

test('the request names the business, forbids invented facts, and asks for the two-draft form', () => {
  const { system, user } = replyPrompt([line(false, 'How much is the full package?')], 'Main branch WhatsApp');
  assert.match(system, /"Main branch WhatsApp"/);
  assert.match(system, /Never invent prices, times, availability/);
  assert.match(system, /\[price\]/);
  assert.match(system, /WARM:\n.*\nSHORT:/s);
  assert.match(user, /Customer: How much is the full package\?$/);
});

test('drafts come back in the asked form, and a model that ignores the form still gives one draft', () => {
  assert.deepEqual(parseDrafts('WARM:\nThank you for asking. The full package is [price].\nSHORT:\nIt is [price].'), [
    { title: 'Warm and complete', body: 'Thank you for asking. The full package is [price].' },
    { title: 'Short', body: 'It is [price].' },
  ]);
  assert.deepEqual(parseDrafts('Sure, it is ready for collection.'), [{ title: 'Draft', body: 'Sure, it is ready for collection.' }]);
  assert.deepEqual(parseDrafts('   '), []);
});
