// Every screen has a help page, every page is listed, and nothing a page points at is missing: a link to another
// page, or a picture. A new screen without a page, or a picture renamed, fails here rather than on the owner's PC.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { HELP_FOR_ROUTE, HELP_PAGES } from './help-index.ts';

const HELP = join(dirname(fileURLToPath(import.meta.url)), '..', 'help');
const read = (id: string) => readFileSync(join(HELP, `${id}.md`), 'utf8');
const ids = HELP_PAGES.map((p) => p.id);

test('every screen opens a page that exists', () => {
  for (const [route, id] of Object.entries(HELP_FOR_ROUTE)) assert.ok(ids.includes(id), `${route} → ${id}`);
});

test('every page file is listed, and every listed page has a file whose heading is its title', () => {
  const files = readdirSync(HELP).filter((f) => f.endsWith('.md')).map((f) => f.slice(0, -3)).sort();
  assert.deepEqual(files, [...ids].sort());
  for (const p of HELP_PAGES) assert.equal(read(p.id).split(/\r?\n/)[0], `# ${p.title}`, p.id);
});

test('every link goes to a page and every picture exists', () => {
  for (const p of HELP_PAGES) {
    const text = read(p.id);
    for (const [, target] of text.matchAll(/\]\(help:([a-z-]+)\)/g)) assert.ok(ids.includes(target), `${p.id} links to ${target}`);
    for (const [, shot] of text.matchAll(/\]\(shot:([a-z-]+)\)/g)) assert.ok(existsSync(join(HELP, 'shots', `${shot}.jpg`)), `${p.id} shows ${shot}.jpg`);
  }
});

test('the pages use the product’s words: accounts, never instances, and business, never a trade', () => {
  for (const p of HELP_PAGES) {
    const text = read(p.id).toLowerCase();
    for (const word of ['instance', 'salon', 'beauty', 'haircut']) assert.ok(!text.includes(word), `${p.id} says ${word}`);
  }
});
