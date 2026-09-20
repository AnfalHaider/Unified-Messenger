import { test } from 'node:test';
import assert from 'node:assert/strict';
import { agoText, apiError, BUSINESS_SCOPE, GOOGLE_API, locationsUrl, parseAccounts, parseLocations, parseReviews, reviewsUrl } from './google-api.ts';
import { ageMinutes, needingReply } from './reviews.ts';

const NOW = Date.parse('2026-09-20T12:00:00Z');

test('the addresses are Google’s own, and the scope is the only one it offers', () => {
  assert.equal(locationsUrl(GOOGLE_API, 'accounts/123'), 'https://mybusinessbusinessinformation.googleapis.com/v1/accounts/123/locations');
  assert.equal(reviewsUrl(GOOGLE_API, 'accounts/123/locations/456'), 'https://mybusiness.googleapis.com/v4/accounts/123/locations/456/reviews');
  assert.equal(BUSINESS_SCOPE, 'https://www.googleapis.com/auth/business.manage');
});

test('accounts and locations are read, and a location is kept under its account', () => {
  assert.deepEqual(parseAccounts({ accounts: [{ name: 'accounts/123', accountName: 'Sample Business' }, { accountName: 'no name' }] }),
    [{ name: 'accounts/123', title: 'Sample Business' }]);
  assert.deepEqual(parseLocations({ locations: [{ name: 'locations/456', title: 'Main branch' }] }, 'accounts/123'),
    [{ name: 'accounts/123/locations/456', title: 'Main branch' }]);
  // Already full: left alone.
  assert.equal(parseLocations({ locations: [{ name: 'accounts/123/locations/456', title: 'x' }] }, 'accounts/123')[0].name, 'accounts/123/locations/456');
  assert.deepEqual(parseAccounts(null), []);
  assert.deepEqual(parseLocations('nonsense', 'accounts/123'), []);
});

test('a page of reviews becomes the cards every screen already reads', () => {
  const page = parseReviews({
    reviews: [
      { reviewId: 'r1', reviewer: { displayName: 'Sample Reviewer' }, starRating: 'FIVE', comment: 'Quick and friendly.', createTime: '2026-09-18T12:00:00Z',
        reviewReply: { comment: 'Thank you!', updateTime: '2026-09-18T18:00:00Z' } },
      { reviewId: 'r2', reviewer: { displayName: 'Another Reviewer' }, starRating: 'ONE', createTime: '2026-09-20T11:30:00Z' },
      { reviewId: 'r3', starRating: 'STAR_RATING_UNSPECIFIED', createTime: 'not a date' },
    ],
    averageRating: 4.6428, totalReviewCount: 991, nextPageToken: 'page-2',
  }, NOW);
  assert.deepEqual(page.cards[0], { reviewer: 'Sample Reviewer', text: 'Quick and friendly.', stars: 5, age: '2 days ago', replied: true });
  assert.deepEqual(page.cards[1], { reviewer: 'Another Reviewer', text: '', stars: 1, age: '30 minutes ago', replied: false });
  // A star Google does not name, and a date it did not send: 0 and blank, never a guess.
  assert.deepEqual([page.cards[2].stars, page.cards[2].age, page.cards[2].reviewer], [0, '', 'A reviewer']);
  assert.deepEqual([page.rating, page.total, page.next], [4.6, 991, 'page-2']);
  // The rest of the app reads these as it reads the page reader's own.
  assert.equal(ageMinutes(page.cards[0].age), 2880);
  assert.deepEqual(needingReply(page.cards).map((c) => c.reviewer), ['Another Reviewer', 'A reviewer'], 'both unanswered ones, including the review with no stars');
});

test('nothing invented when Google sends nothing', () => {
  const page = parseReviews({}, NOW);
  assert.deepEqual([page.cards, page.next, page.rating, page.total], [[], null, null, null]);
  assert.equal(parseReviews({ averageRating: 0, totalReviewCount: 0 }, NOW).rating, null, 'no reviews is not a rating of zero');
  assert.equal(parseReviews({ totalReviewCount: 0 }, NOW).total, 0, 'but no reviews is a total of none');
});

test('ages are said the way the app reads them', () => {
  const cases: [number, string][] = [
    [30_000, '1 minute ago'], [90 * 60_000, '1 hour ago'], [26 * 3_600_000, '1 day ago'], [3 * 86_400_000, '3 days ago'],
    [9 * 86_400_000, '1 week ago'], [40 * 86_400_000, '1 month ago'], [400 * 86_400_000, '1 year ago'],
  ];
  for (const [ms, said] of cases) {
    assert.equal(agoText(ms), said);
    assert.ok(ageMinutes(said) !== null, `${said} reads back`);
  }
});

test('Google’s refusals are said in words, never a code', () => {
  assert.match(apiError(403, { error: { message: 'The caller does not have permission' } }), /cannot manage that business profile/);
  assert.match(apiError(403, {}), /has not granted this app access/);
  assert.match(apiError(401, {}), /Connect the profile again/);
  assert.match(apiError(429, {}), /fewer requests/);
  assert.match(apiError(503, {}), /did not answer/);
  for (const status of [400, 401, 403, 404, 429, 500]) assert.ok(!/[A-Z_]{6,}|\b\d{3}\b/.test(apiError(status, { error: { message: 'PERMISSION_DENIED' } })));
});
