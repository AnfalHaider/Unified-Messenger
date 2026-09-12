import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loadJson, saveJson } from './store.ts';

const dir = () => mkdtempSync(join(tmpdir(), 'um-store-'));

test('a saved file loads back as itself', () => {
  const f = join(dir(), 'x.json');
  saveJson(f, { a: 1, nested: ['b'] });
  assert.deepEqual(loadJson(f, null), { a: 1, nested: ['b'] });
});

test('a missing file is the fallback, and is not a problem', () => {
  let told = '';
  assert.deepEqual(loadJson(join(dir(), 'nope.json'), { empty: true }, (w) => { told = w; }), { empty: true });
  assert.equal(told, '');
});

test('an unreadable file keeps its bytes before anything overwrites them', () => {
  const f = join(dir(), 'broken.json');
  writeFileSync(f, '{ not json');
  let told = '';
  assert.deepEqual(loadJson(f, { fresh: true }, (w) => { told = w; }), { fresh: true });
  assert.match(told, /unparseable/);
  const kept = told.slice(told.indexOf('kept ') + 5);
  assert.equal(readFileSync(kept, 'utf8'), '{ not json');
});
