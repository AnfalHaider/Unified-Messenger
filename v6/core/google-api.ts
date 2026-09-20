// Google's official Business Profile API as a reviews source (roadmap 3.1b), instead of reading the reviews page.
// Built and switched off: Google grants access to these APIs by application, and until that is granted every call
// here would be refused, so nothing runs unless the owner's build has it switched on (settings.googleApi.enabled)
// and the profile has been connected. The page reader stays exactly as it is until then.
//
// Pure: the addresses, and the shapes Google returns turned into the same ReviewCard the page reader produces, so
// every screen, count and report works the same whichever read them. app/google-api.ts makes the calls.
//
// What the API gives that the page cannot: every review rather than the latest fifty, the reply and when it was
// written, the star rating as a number rather than the colour of a glyph, and the profile's rating and lifetime
// total without moving the page. What it costs: Google's approval, and a scope ("business.manage") that is the only
// one offered — the app still only reads, which the consent screen and the privacy policy both say.
import type { ReviewCard } from './reviews.ts';

/** The one scope Google offers for these APIs. There is no read-only version; the app never writes. */
export const BUSINESS_SCOPE = 'https://www.googleapis.com/auth/business.manage';

/** Where the calls go. Tests point these at a server of their own. */
export interface GoogleApi { accounts: string; locations: string; reviews: string }
export const GOOGLE_API: GoogleApi = {
  accounts: 'https://mybusinessaccountmanagement.googleapis.com/v1/accounts',
  locations: 'https://mybusinessbusinessinformation.googleapis.com/v1/{account}/locations',
  reviews: 'https://mybusiness.googleapis.com/v4/{location}/reviews',
};

/** One Google account the signed-in person manages: `accounts/123`, and what Google calls it. */
export interface GoogleAccount { name: string; title: string }
/** One profile under that account: `accounts/123/locations/456`, and the business's name there. */
export interface GoogleLocation { name: string; title: string }

export const locationsUrl = (api: GoogleApi, account: string) => api.locations.replace('{account}', account);
export const reviewsUrl = (api: GoogleApi, location: string) => api.reviews.replace('{location}', location);

const text = (v: unknown, max: number) => (typeof v === 'string' ? v.trim().slice(0, max) : '');
const rows = (v: unknown) => (Array.isArray(v) ? (v as Record<string, unknown>[]) : []);

export function parseAccounts(raw: unknown): GoogleAccount[] {
  return rows((raw as { accounts?: unknown })?.accounts)
    .map((a) => ({ name: text(a.name, 120), title: text(a.accountName, 120) }))
    .filter((a) => a.name);
}

export function parseLocations(raw: unknown, account: string): GoogleLocation[] {
  return rows((raw as { locations?: unknown })?.locations)
    // Business Information returns `locations/456`; reviews want it under its account.
    .map((l) => ({ name: text(l.name, 120), title: text(l.title, 120) }))
    .filter((l) => l.name)
    .map((l) => ({ ...l, name: l.name.startsWith('accounts/') ? l.name : `${account}/${l.name}` }));
}

/** Google's star words. Anything else is 0, the same as a star the page reader could not read. */
const STARS: Record<string, number> = { ONE: 1, TWO: 2, THREE: 3, FOUR: 4, FIVE: 5 };

export interface ReviewsPage { cards: ReviewCard[]; next: string | null; rating: number | null; total: number | null }

/**
 * One page of reviews. Never throws: a shape Google changes costs this read, not the app. `now` makes the ages
 * ("5 days ago") the app already understands, because the API gives a timestamp and the screens read words.
 */
export function parseReviews(raw: unknown, now: number): ReviewsPage {
  const root = (raw ?? {}) as { reviews?: unknown; nextPageToken?: unknown; averageRating?: unknown; totalReviewCount?: unknown };
  const cards: ReviewCard[] = rows(root.reviews).map((r) => {
    const reviewer = (r.reviewer ?? {}) as { displayName?: unknown };
    const reply = (r.reviewReply ?? {}) as { comment?: unknown };
    const written = Date.parse(text(r.createTime, 40) || text(r.updateTime, 40));
    return {
      reviewer: text(reviewer.displayName, 60) || 'A reviewer',
      text: text(r.comment, 1200),
      stars: STARS[text(r.starRating, 20).toUpperCase()] ?? 0,
      age: Number.isFinite(written) ? agoText(now - written) : '',
      replied: text(reply.comment, 1).length > 0,
    };
  });
  const rating = typeof root.averageRating === 'number' && root.averageRating > 0 ? Math.round(root.averageRating * 10) / 10 : null;
  const total = typeof root.totalReviewCount === 'number' && root.totalReviewCount >= 0 ? root.totalReviewCount : null;
  return { cards, next: text(root.nextPageToken, 4000) || null, rating, total };
}

/** "5 days ago", in the words `ageMinutes` in core/reviews.ts reads back. Google's own pages say it this way. */
export function agoText(ms: number): string {
  const minutes = Math.max(0, ms) / 60_000;
  const [unit, size] = minutes < 60 ? ['minute', 1]
    : minutes < 1440 ? ['hour', 60]
    : minutes < 10_080 ? ['day', 1440]
    : minutes < 43_830 ? ['week', 10_080]
    : minutes < 525_960 ? ['month', 43_830]
    : ['year', 525_960];
  const n = Math.max(1, Math.floor(minutes / (size as number)));
  return `${n} ${unit}${n === 1 ? '' : 's'} ago`;
}

/** Google's error replies, in words for the owner. The codes are Google's; the owner sees a sentence. */
export function apiError(status: number, raw: unknown): string {
  const message = text(((raw ?? {}) as { error?: { message?: unknown } }).error?.message, 200);
  if (status === 401) return 'Google did not accept the connection. Connect the profile again.';
  if (status === 403 && /insufficient|permission/i.test(message)) return 'This Google account cannot manage that business profile.';
  if (status === 403) return 'Google has not granted this app access to the Business Profile API yet.';
  if (status === 404) return 'Google has no profile at that address any more.';
  if (status === 429) return 'Google asked for fewer requests. The next read waits a while.';
  if (status >= 500) return 'Google’s service did not answer. The next read tries again.';
  return 'Google refused the request.';
}
