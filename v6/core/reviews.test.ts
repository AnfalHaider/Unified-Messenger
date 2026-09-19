// The rating and total rules are v5's (GoogleProfileTotalParsingTests), each found against a live profile. The
// page text here is invented: the layouts are real, the business names are not.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ageMinutes, needingReply, parseProfile, parseReviewsRead, spread, type ReviewCard } from './reviews.ts';

const total = (text: string) => parseProfile(text).total;

test('each real layout yields the lifetime total', () => {
  assert.equal(total('Sample Business North\n4.6 ★ (991) · Business\nSample City'), 991, 'bracketed, with no "Google reviews" anywhere');
  assert.equal(total('Sample Business East\n4.6 ★ (244) · Business'), 244);
  assert.equal(total('Sample Business Men\n4.7 ★★★★☆ 435 Google reviews'), 435, 'labelled, five glyphs between the numbers');
});

test('run-together text still splits: the rating is not swallowed into the count', () => {
  assert.equal(total('4.6239 Google reviews'), 239);
  assert.equal(total('4.8 ★ (1,234) · Business'), 1234, 'a thousands separator is not a stopping point');
});

test('an earlier bracketed number, or another business, is not taken for the count', () => {
  assert.equal(total('Open now (closes 9 PM)\n4.6 ★ (991) · Business'), 991);
  assert.equal(total('4.9 ★ open\n4.2 ★ 88 Google reviews'), 88);
});

test('a labelled count with no rating beside it still parses; a page with no count says nothing', () => {
  assert.equal(total('Reviews\n239 Google reviews'), 239);
  assert.equal(total('Sample Business North\nBusiness · Sample City\nOpen now'), null);
});

test('the rating beside the count beats a stray "Rated" label from another review or business', () => {
  assert.equal(parseProfile('4.6 ★ (991) · Business', ['Rated 4.7 out of 5,']).rating, 4.6);
  assert.equal(parseProfile('4.7 ★★★★☆ 435 Google reviews', ['Rated 3.0 out of 5,']).rating, 4.7);
  assert.equal(parseProfile('Reviews\n239 Google reviews', ['Rated 4.4 out of 5,']).rating, 4.4, 'the label fills in when nothing sits beside the count');
  assert.equal(parseProfile('Reviews', ['Rated 4,5 out of 5']).rating, 4.5, 'a decimal comma is read');
});

test('the reviews page read keeps what it can and says where it stopped', () => {
  const read = parseReviewsRead(JSON.stringify({ state: 'done', more: true, cards: [
    { reviewer: 'Sample Reviewer A', text: 'Waited a long time.', stars: 1, age: '2 days ago', replied: false },
    { reviewer: '', text: "The user didn't write a review, and has left just a rating.", stars: 5, age: 'a week ago', replied: true },
    { reviewer: 'Sample Reviewer C', text: 'Fine', stars: 9, age: '3 hours ago', replied: false },
  ] }));
  assert.ok(read.ok);
  if (!read.ok) return;
  assert.equal(read.more, true);
  assert.deepEqual(read.cards.map((c) => [c.reviewer, c.text, c.stars, c.replied]), [
    ['Sample Reviewer A', 'Waited a long time.', 1, false],
    ['A reviewer', '', 5, true],
    ['Sample Reviewer C', 'Fine', 0, false],
  ]);
  assert.deepEqual(parseReviewsRead(JSON.stringify({ state: 'notreviews' })), { ok: false, stage: 'notreviews' });
  assert.deepEqual(parseReviewsRead('not json'), { ok: false, stage: 'unreadable' });
  assert.deepEqual(parseReviewsRead(''), { ok: false, stage: 'unreadable' });
});

test('ages are read the way Google writes them', () => {
  assert.equal(ageMinutes('5 days ago'), 5 * 1440);
  assert.equal(ageMinutes('a week ago'), 10080);
  assert.equal(ageMinutes('an hour ago'), 60);
  assert.equal(ageMinutes('Edited 2 days ago'), null);
});

test('the reviews waiting for a reply come worst first, then oldest; the spread counts the latest page', () => {
  const card = (stars: number, age: string, replied = false): ReviewCard => ({ reviewer: `R${stars}${age}`, text: '', stars, age, replied });
  const cards = [card(4, 'a day ago'), card(1, '2 days ago'), card(1, '5 days ago'), card(5, '3 days ago', true), card(0, 'an hour ago')];
  assert.deepEqual(needingReply(cards).map((c) => c.reviewer), ['R15 days ago', 'R12 days ago', 'R0an hour ago', 'R4a day ago']);
  assert.deepEqual(spread(cards), [1, 1, 0, 0, 2]);
});
