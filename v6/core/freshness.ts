// Port of DataFreshness.cs: the one place that phrases "how old is this". Stale numbers shown as current are
// indistinguishable from good news, and the owner acts on them.

/** Past this the background read (every 25-90 s) has been failing, not lagging, so the numbers are flagged. */
export const STALE_AFTER_MS = 30 * 60_000;

export interface Freshness { text: string; isStale: boolean; hasData: boolean }

export function describeFreshness(capturedAt: number | null | undefined, now = Date.now()): Freshness {
  if (capturedAt == null) return { text: 'Nothing read yet — press Read now', isStale: true, hasData: false };
  // A clock change can put the capture in the future, and "Updated in 3 minutes" reads as a bug.
  const age = Math.max(0, now - capturedAt);
  const isStale = age >= STALE_AFTER_MS;
  const phrase = agePhrase(age / 1000);
  return { text: isStale ? `Updated ${phrase} — press Read now for current numbers` : `Updated ${phrase}`, isStale, hasData: true };
}

function agePhrase(s: number): string {
  if (s < 60) return 'just now';
  if (s < 120) return '1 minute ago';
  if (s < 3600) return `${Math.floor(s / 60)} minutes ago`;
  if (s < 7200) return '1 hour ago';
  if (s < 86400) return `${Math.floor(s / 3600)} hours ago`;
  if (s < 172800) return 'yesterday';
  return `${Math.floor(s / 86400)} days ago`;
}
