// Google reviews. Not a ChannelModule: Google has no conversations (Business Messages ended in 2024), so its reader
// produces reviews, not chats, and never feeds the line, reply times or the waiting counts. Main reads it every
// half hour, never while its page is on screen, because reading the rating takes the page to another address.
//
// The two expressions run the functions google-reviews.js installs; the script is sent with them each time, so a
// page that reloaded since the last read still has them. Both answer a JSON string, parsed in core/reviews.ts.

// The tests point these at invented pages on disk, so no test ever reaches Google. Customers never set them.
export const GOOGLE_REVIEWS_URL = process.env.UM_GOOGLE_REVIEWS_URL || 'https://business.google.com/reviews';
/** business.google.com's root redirects a single-profile account to the Search merchant view, which alone carries
 *  the rating and lifetime total. Following Google's own redirect beats guessing a search address. */
export const GOOGLE_PROFILE_URL = process.env.UM_GOOGLE_PROFILE_URL || 'https://business.google.com/';

export const google = {
  id: 'googlebusiness',
  name: 'Google reviews',
  script: (load: (file: string) => string) => load('google/google-reviews.js'),
  reviews: 'window.__umGoogleReviews ? window.__umGoogleReviews() : JSON.stringify({ state: "reader-absent" })',
  profile: 'window.__umGoogleProfile ? window.__umGoogleProfile() : JSON.stringify({ state: "reader-absent" })',
};

/** What the merchant view sent back: its text and rating labels, or why it has none. */
export function parseProfileRead(raw: unknown): { ok: true; text: string; aria: string[] } | { ok: false; stage: string } {
  try {
    const root = (typeof raw === 'string' ? JSON.parse(raw) : raw) as { state?: unknown; text?: unknown; aria?: unknown } | null;
    if (root?.state !== 'done') return { ok: false, stage: typeof root?.state === 'string' ? root.state : 'no-answer' };
    return {
      ok: true, text: typeof root.text === 'string' ? root.text : '',
      aria: Array.isArray(root.aria) ? root.aria.filter((a): a is string => typeof a === 'string') : [],
    };
  } catch {
    return { ok: false, stage: 'unreadable' };
  }
}
