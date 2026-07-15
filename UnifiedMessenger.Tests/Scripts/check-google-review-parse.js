// Validates the SHIPPED review-card reader against the exact lines dumped from the live Google page.
// Extracts the JS out of the C# PageHelpers constant so this tests the real string, not a retyped copy.
const fs = require('fs');

const CS = require('path').join(__dirname, '../../UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs');
const src = fs.readFileSync(CS, 'utf8');
const start = src.indexOf('private const string PageHelpers =');
const endMark = 'idx:idx};};";';
const end = src.indexOf(endMark, start);
if (start < 0 || end < 0) { console.log('EXTRACT FAILED'); process.exit(1); }
// Drop // comment lines first — they quote example DOM text ("Rows per page", the dumped card) that would
// otherwise be spliced into the JS as if it were code.
const block = src.slice(start, end + endMark.length)
  .split('\n')
  .filter(line => !line.trim().startsWith('//'))
  .join('\n');

// Concatenate the contents of each C# "..." literal, unescaping C#'s \\, \" and \uXXXX.
let js = '';
const re = /"((?:[^"\\]|\\.)*)"/g;
let m;
while ((m = re.exec(block)) !== null) {
  js += m[1]
    .replace(/\\u([0-9a-fA-F]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16)))
    .replace(/\\"/g, '"')
    .replace(/\\\\/g, '\\');
}

// Minimal window + a fake card whose innerText is the REAL dump. __umGRCard is DOM-bound, so stub it.
const window = {};
global.window = window;
eval(js);

const STAR = String.fromCharCode(59448); // 0xE838 — filled star, from the live dump's codes[3]
const OUTLINE = String.fromCharCode(59449); // an outline glyph: a different codepoint in the same run

function read(lines) {
  window.__umGRCard = () => ({ innerText: lines.join('\n') });
  return window.__umGRRead({}, 0);
}

// The exact card dumped from the live page (business.google.com/reviews, pending review).
const live = [
  'Depilex DHA-2 Islamabad',
  'Jinnah Boulevard, Islamabad',
  'Anjum Afzal',
  STAR.repeat(5) + ' 5 days ago',
  'I had an excellent experience at Deplix Men Saloon DHA Phase 2. From the moment I walked in, I was impressed by the... More',
  'reply',
  'Reply',
  'more_vert',
];

let failures = 0;
function check(name, actual, expected) {
  const ok = actual === expected;
  if (!ok) failures++;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}\n      got: ${JSON.stringify(actual)}` +
    (ok ? '' : `\n      exp: ${JSON.stringify(expected)}`));
}

console.log('--- live card (verbatim from the DevTools dump) ---');
const r = read(live);
check('reviewer is the person, not the location', r.reviewer, 'Anjum Afzal');
check('age split off the star line', r.age, '5 days ago');
check('5 filled glyphs -> 5 stars', r.stars, 5);
check('text: no header, no icon ligatures, no tofu, "... More" -> ellipsis', r.text,
  'I had an excellent experience at Deplix Men Saloon DHA Phase 2. From the moment I walked in, I was impressed by the…');

console.log('\n--- 1-star review (filled-first, then outline glyphs) ---');
const oneStar = read([
  'Depilex Men', 'Sector E DHA Phase II,, Islamabad', 'Muhammad Fahad',
  STAR + OUTLINE.repeat(4) + ' 20 weeks ago',
  'Worst haircut of my life', 'reply', 'Reply', 'more_vert',
]);
check('reviewer', oneStar.reviewer, 'Muhammad Fahad');
check('leading run of 1 -> 1 star (NOT 5)', oneStar.stars, 1);
check('age', oneStar.age, '20 weeks ago');
check('text', oneStar.text, 'Worst haircut of my life');

console.log('\n--- rating-only review (no text) ---');
const noText = read([
  'Depilex Men', 'Sector E DHA Phase II,, Islamabad', 'Man on a Mission',
  STAR.repeat(3) + OUTLINE.repeat(2) + ' 29 weeks ago',
  'reply', 'Reply', 'more_vert',
]);
check('3 filled -> 3 stars', noText.stars, 3);
check('empty text, not junk', noText.text, '');
check('reviewer', noText.reviewer, 'Man on a Mission');

console.log('\n--- expanded review (More was clicked: no truncation marker) ---');
const expanded = read([
  'Depilex DHA-2 Islamabad', 'Jinnah Boulevard, Islamabad', 'Anjum Afzal',
  STAR.repeat(5) + ' 5 days ago',
  'Full text with no More suffix at all.', 'reply', 'Reply', 'more_vert',
]);
check('text kept whole', expanded.text, 'Full text with no More suffix at all.');

console.log('\n--- degraded: no meta line (layout drift) ---');
const drift = read(['Depilex DHA-2 Islamabad', 'Jinnah Boulevard, Islamabad', 'Some review text', 'Reply']);
check('no wrong name invented', drift.reviewer, 'Reviewer');
check('text still surfaced', drift.text, 'Depilex DHA-2 Islamabad Jinnah Boulevard, Islamabad Some review text');

console.log(failures ? `\n${failures} FAILING` : '\nall green');
process.exit(failures ? 1 : 0);
