// The release notes for a version, unwrapped, ready to paste into the GitHub release form.
//
//   npm run release:notes            -> the newest entry in CHANGELOG.md
//   npm run release:notes v6.0.0     -> that entry
//
// Why unwrapped: the update drawer shows the release's own bullets, and every copy installed before this fix reads
// every wrapped line as a bullet of its own, so a wrapped body reaches those owners cut mid-sentence. The
// repository's CHANGELOG stays wrapped, because it is read as a file; what goes to GitHub does not.
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const changelog = readFileSync(join(REPO, 'CHANGELOG.md'), 'utf8');

const asked = process.argv[2];
const entries = changelog.split(/^## /m).slice(1);
const entry = asked ? entries.find((e) => e.split(/\r?\n/)[0].trim() === asked.replace(/^v?/, 'v')) : entries[0];
if (!entry) {
  console.error(asked ? `CHANGELOG.md has no entry for ${asked}.` : 'CHANGELOG.md has no entries.');
  process.exit(1);
}

const [version, ...rest] = entry.split(/\r?\n/);

/** Wrapped lines gathered back into the blocks they were written as, the same rule core/update.ts reads by. */
const starts = /^([-*]\s+|#|>)/;
const blocks = [];
for (const raw of rest) {
  const line = raw.trim();
  if (!line) { blocks.push(''); continue; }
  const last = blocks[blocks.length - 1];
  if (starts.test(line) || !last) blocks.push(line);
  else blocks[blocks.length - 1] = `${last} ${line}`;
}

// A quote wrapped over several lines is several blocks, each still carrying its own `>`; join them back into one.
const body = [];
for (const block of blocks) {
  const previous = body[body.length - 1];
  if (block.startsWith('>') && previous?.startsWith('>')) body[body.length - 1] = `${previous} ${block.replace(/^>\s*/, '')}`;
  else body.push(block);
}

const text = body.join('\n').replace(/\n{3,}/g, '\n\n').trim();
const out = join(REPO, 'v6', 'dist', `release-notes-${version.trim()}.md`);
writeFileSync(out, text);
console.log(text);
console.log(`\n\n== ${version.trim()}: ${text.length} characters, also written to ${out}`);
console.log('== Paste this as the release body. Each bullet must stay on one line.');
