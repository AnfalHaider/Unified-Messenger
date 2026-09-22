import { test } from 'node:test';
import assert from 'node:assert/strict';
import { keptRows, keptTotal, sizeText, type KeptSizes } from './kept.ts';

const sizes = (o: Partial<KeptSizes> = {}): KeptSizes =>
  ({ logins: 509_607_936, recorded: 39_845_888, customers: 204_800, reviews: 51_200, log: 3_238_677, model: null, ...o });

test('a size is said the way a person reads it, and bytes are never shown', () => {
  assert.equal(sizeText(0), '0 B');
  assert.equal(sizeText(999), '999 B');
  assert.equal(sizeText(204_800), '200 KB');
  assert.equal(sizeText(509_607_936), '486 MB');
  assert.equal(sizeText(3_543_348_019), '3.3 GB');
});

test('what could not be measured says so, because nothing found and nothing measured are different answers', () => {
  assert.equal(sizeText(null), 'not measured');
  const rows = keptRows(sizes({ logins: null }));
  assert.equal(rows[0].bytes, null);
  assert.notEqual(rows[0].bytes, 0, 'an unmeasured folder must never read as an empty one');
});

test('every row says where it is managed, so the screen is not a dead end', () => {
  for (const row of keptRows(sizes())) {
    assert.ok(row.what.length > 0);
    assert.ok(row.kept.length > 0);
    assert.ok(row.where.length > 0, `${row.what} says where`);
  }
});

test('the model row says nothing is downloaded rather than showing a size the PC does not hold', () => {
  const off = keptRows(sizes({ model: null })).find((r) => r.what.includes('model'))!;
  assert.equal(off.bytes, null);
  assert.match(off.where, /Nothing downloaded/);
  const on = keptRows(sizes({ model: 3_543_348_019 })).find((r) => r.what.includes('model'))!;
  assert.match(on.where, /Ollama/);
});

test('the total adds up what is known, and is unknown only when nothing at all could be measured', () => {
  assert.equal(keptTotal(sizes({ model: 1_000 })), 509_607_936 + 39_845_888 + 204_800 + 51_200 + 3_238_677 + 1_000);
  assert.equal(keptTotal({ logins: null, recorded: null, customers: null, reviews: null, log: null, model: null }), null);
  assert.equal(keptTotal(sizes({ logins: null, recorded: null, customers: null, reviews: null, log: null })), null,
    'nothing measured but an absent model is still nothing measured');
});
