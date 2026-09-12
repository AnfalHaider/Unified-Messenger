// Rebuilds the published design-canvas page from the freshly generated artboards.
//
// The canvas artifact keeps its whole editable state inside one
// <script type="application/json" id="appifact-doc"> block: {"title":…,"content":{"files":{"Main.dc.html":…}}}.
// A republish means swapping the sources inside that record and writing the page back out — not publishing a
// new page, which would hand the owner a different URL.
//
// Run with no argument to inspect the record; run with `write` to produce the new page.
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const SCREENS = join(HERE, '..');
const SOURCE = process.argv[3] || 'C:/Users/anfal/.claude/projects/D--Projects-Unified-Messenger/eb6224ce-8977-42ae-a009-8ea187d82a39/tool-results/artifact-7f3b1b22-1789145223-9bb6.html';
const OUT = join(SCREENS, 'canvas-page.html');

const page = readFileSync(SOURCE, 'utf8');
const tagMatch = /<script[^>]*id="appifact-doc"[^>]*>/.exec(page);
if (!tagMatch) { console.error('no appifact-doc block found'); process.exit(1); }
const bodyStart = tagMatch.index + tagMatch[0].length;
const bodyEnd = page.indexOf('</script>', bodyStart);
const raw = page.slice(bodyStart, bodyEnd);

console.log('tag:', tagMatch[0]);
console.log('record length:', raw.length);

let doc;
try {
  doc = JSON.parse(raw);
} catch (err) {
  console.error('record is not JSON:', err.message);
  process.exit(1);
}

// The files record sits under content.files; older pages kept it at the top level.
const holder = doc.content && typeof doc.content === 'object' && doc.content.files ? doc.content : doc;
if (!holder.files || typeof holder.files !== 'object') { console.error('no files record'); process.exit(1); }
const names = Object.keys(holder.files);
console.log('top-level keys:', Object.keys(doc), '· holder keys:', Object.keys(holder));
console.log(`${names.length} files, first:`, names.slice(0, 4));
console.log('entry type:', typeof holder.files[names[0]]);

if (process.argv[2] !== 'write') process.exit(0);

const generated = readdirSync(SCREENS).filter((f) => f.endsWith('.dc.html'));
let replaced = 0, added = 0;
for (const name of [...generated, 'canvas.json']) {
  const content = readFileSync(join(SCREENS, name), 'utf8');
  const existing = holder.files[name];
  if (existing === undefined) added++; else replaced++;
  holder.files[name] = typeof existing === 'object' && existing !== null ? { ...existing, content } : content;
}

// The record lives inside a <script> tag, so a literal "</script>" in any artboard would close it early.
const encoded = JSON.stringify(doc).replaceAll('</script', '<\\/script');
writeFileSync(OUT, page.slice(0, bodyStart) + encoded + page.slice(bodyEnd), 'utf8');
console.log(`wrote ${OUT}: ${replaced} replaced, ${added} added, ${generated.length} artboards`);
