// Google reviews: a profile's rating and lifetime total, and the latest reviews with whether each has a reply.
// Google is a reviews channel, never a conversation channel (Business Messages was shut down in 2024), so none of
// this touches the line, reply times or the waiting counts.
//
// Two reads, on two pages, because Google splits them:
// - business.google.com/reviews lists the reviews with a Reply button (unanswered) or an Edit button (answered),
//   up to 50 per page. It carries no rating and no total.
// - The Search merchant view, where business.google.com/ redirects, carries the rating and the lifetime total.
//   Its text is parsed here rather than in the page so the rules can be tested; v5 found every one of them live.

export interface ReviewCard {
  reviewer: string;
  /** Empty for a rating with no words: Google's "didn't write a review" placeholder is not a quote. */
  text: string;
  /** 1 to 5, or 0 when the stars could not be read. */
  stars: number;
  /** As Google says it: "5 days ago". */
  age: string;
  replied: boolean;
}

export interface ProfileReviews {
  /** When the reviews page was last read. */
  capturedAt: number;
  /** The newest reviews Google shows first, answered and not, up to one page. */
  cards: ReviewCard[];
  /** Whether Google offers another page after this one: the counts cover the latest reviews, not all of them. */
  more: boolean;
  rating: number | null;
  total: number | null;
  /** When the rating and total were last read. They change slowly, and reading them moves the page. */
  ratingAt: number | null;
}

/** account id → its profile. */
export type Reviews = Record<string, ProfileReviews>;

// ---- rating and lifetime total, from the merchant view's text ----------------------------------------------

/**
 * Rating and total are only trusted as a PAIR from one run of text. innerText runs them together ("4.6239 Google
 * reviews"), so the total is anchored on the rating before it; the gap allows up to 12 non-digits because some
 * profiles print five star glyphs between them ("4.7 ★★★★☆ 435 Google reviews"), and being non-digits it can never
 * step over another number. Two layouts exist, and only one says "Google reviews": the other is "4.6 ★ (991)".
 */
const PAIRED = [
  /([0-5][.,]\d)[^\d]{0,12}([\d,]+)\s+Google\s+reviews/i,
  /([0-5][.,]\d)[^\d(]{0,12}\((\d[\d,]*)\)/i,
];
/** A total with no rating beside it, not sliced out of the middle of another number. */
const SOLO = /(?:^|[^\d.,])([\d,]{1,7})\s+Google\s+reviews/i;
const ARIA = /Rated\s+([0-5][.,]\d)\s+out\s+of\s+5/i;

const num = (s: string) => Number(s.replace(/,/g, ''));
const rate = (s: string) => Number(s.replace(',', '.'));

/**
 * The profile's rating and lifetime total. The rating printed beside the total wins over any "Rated 4.7 out of 5"
 * label: the merchant view carries several, for single reviews and for other businesses nearby, and the first one
 * on the page was wrong for two of v5's three live profiles. A page with no total says so (null) rather than guess.
 */
export function parseProfile(text: string, ariaLabels: string[] = []): { rating: number | null; total: number | null } {
  let rating: number | null = null, total: number | null = null;
  for (const re of PAIRED) {
    const m = re.exec(text);
    if (m) { rating = rate(m[1]); total = num(m[2]); break; }
  }
  if (total === null) {
    const m = SOLO.exec(text);
    if (m) total = num(m[1]);
  }
  if (rating === null) {
    for (const label of ariaLabels) {
      const m = ARIA.exec(label);
      if (m) { rating = rate(m[1]); break; }
    }
  }
  return { rating: rating !== null && rating >= 1 && rating <= 5 ? rating : null, total: total !== null && Number.isFinite(total) ? total : null };
}

// ---- the reviews page ------------------------------------------------------------------------------------

export type ReviewsRead =
  | { ok: true; cards: ReviewCard[]; more: boolean }
  | { ok: false; stage: string };

const str = (v: unknown, max: number) => (typeof v === 'string' ? v.trim().slice(0, max) : '');

/** What the page script returned. Never throws: a page that changed shape costs this read and says where. */
export function parseReviewsRead(raw: unknown): ReviewsRead {
  try {
    const root = (typeof raw === 'string' ? JSON.parse(raw) : raw) as { state?: unknown; cards?: unknown; more?: unknown } | null;
    const state = str(root?.state, 40);
    if (state !== 'done') return { ok: false, stage: state || 'no-answer' };
    const cards: ReviewCard[] = [];
    for (const c of Array.isArray(root?.cards) ? root.cards as Record<string, unknown>[] : []) {
      const stars = typeof c?.stars === 'number' && c.stars >= 1 && c.stars <= 5 ? Math.round(c.stars) : 0;
      const text = str(c?.text, 1200);
      cards.push({
        reviewer: str(c?.reviewer, 60) || 'A reviewer',
        text: /^the user didn'?t write a review/i.test(text) ? '' : text,
        stars, age: str(c?.age, 40), replied: c?.replied === true,
      });
    }
    return { ok: true, cards, more: root?.more === true };
  } catch {
    return { ok: false, stage: 'unreadable' };
  }
}

const UNIT: Record<string, number> = { second: 1 / 60, minute: 1, hour: 60, day: 1440, week: 10080, month: 43830, year: 525960 };

/** "5 days ago" as minutes, roughly: Google only says it that roughly. Null when it cannot be read. */
export function ageMinutes(age: string): number | null {
  const m = /^(a|an|\d+)\s+(second|minute|hour|day|week|month|year)s?\s+ago$/i.exec(age.trim());
  if (!m) return null;
  const n = /^an?$/i.test(m[1]) ? 1 : Number(m[1]);
  return n * UNIT[m[2].toLowerCase()];
}

/** The reviews still waiting for a reply, worst first, then oldest: a one-star review from last week before a
 *  four-star one from today. A review whose stars could not be read sorts with the unhappy ones, not below them. */
export function needingReply(cards: ReviewCard[]): ReviewCard[] {
  const rank = (c: ReviewCard) => (c.stars === 0 ? 2.5 : c.stars);
  return cards.filter((c) => !c.replied).sort((a, b) => rank(a) - rank(b) || (ageMinutes(b.age) ?? 0) - (ageMinutes(a.age) ?? 0));
}

/** How the latest reviews spread over five stars down to one, for the bar chart. */
export const spread = (cards: ReviewCard[]) => [5, 4, 3, 2, 1].map((s) => cards.filter((c) => c.stars === s).length);

/** The rating and total are read at most this often: they barely move, and reading them takes the page away. */
export const RATING_EVERY_MS = 6 * 3_600_000;
/** The reviews page is read at most this often. New reviews arrive a few times a day, not a minute. */
export const REVIEWS_EVERY_MS = 30 * 60_000;
