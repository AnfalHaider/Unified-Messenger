// Updates (roadmap 7.3). Owner's decision 2026-09-20: keep the Inno Setup installer, and have the app ask GitHub
// Releases whether a newer one exists. Nothing is installed behind the owner's back: the app downloads the new Setup
// and waits for them to say when, because installing closes the app and every account page with it.
//
// Pure here: which release is newer, what to make of GitHub's answer, and what the owner is told. app/update.ts does
// the asking and the downloading. The check sends nothing: it is a plain GET, with no identifier of any kind.
export interface Release { version: string; notes: string[]; url: string; size: number }

/** The releases of this repository. A release's tag is `v6.1.0`; its Setup is attached to it. */
export const RELEASES_URL = 'https://api.github.com/repos/AnfalHaider/Unified-Messenger/releases/latest';
/** The installer's name, as scripts/dist.mjs builds it and the release carries it. */
export const SETUP_NAME = 'UnifiedMessenger6Setup.exe';

/** 6.1.0 → [6, 1, 0]. Anything unreadable sorts lowest, so it can never look newer than what is installed. */
const parts = (v: string) => (/^v?\d+(\.\d+)*$/.test(v.trim()) ? v.trim().replace(/^v/, '').split('.').map(Number) : [-1]);

/** Whether `candidate` is a later version than `current`. Equal is not newer; unreadable is never newer. */
export function isNewer(candidate: string, current: string): boolean {
  const a = parts(candidate), b = parts(current);
  if (a[0] < 0) return false;
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const x = a[i] ?? 0, y = b[i] ?? 0;
    if (x !== y) return x > y;
  }
  return false;
}

/**
 * GitHub's answer, taken apart defensively: a release with no Windows Setup attached is not an update, a draft or a
 * pre-release is not offered, and the notes are the release's own lines, trimmed to what a drawer can show.
 */
export function readRelease(raw: unknown, current: string, releasesUrl: string = RELEASES_URL): Release | null {
  const r = (raw ?? {}) as { tag_name?: unknown; name?: unknown; draft?: unknown; prerelease?: unknown; body?: unknown; assets?: unknown };
  if (r.draft === true || r.prerelease === true) return null;
  const version = String(r.tag_name ?? r.name ?? '').trim().replace(/^v/, '');
  if (!version || !isNewer(version, current)) return null;
  const asset = (Array.isArray(r.assets) ? r.assets as Record<string, unknown>[] : [])
    .find((a) => String(a.name ?? '').toLowerCase() === SETUP_NAME.toLowerCase());
  const url = String(asset?.browser_download_url ?? '');
  if (!fromSamePlace(url, releasesUrl)) return null;
  return { version, notes: notesFrom(String(r.body ?? '')), url, size: Number(asset?.size) || 0 };
}

/** The release's lines as a short list: bullets if it has them, else its first sentences. Six at most. */
export function notesFrom(body: string): string[] {
  const lines = body.split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
  const bullets = lines.filter((l) => /^[-*]\s+/.test(l)).map((l) => l.replace(/^[-*]\s+/, ''));
  const rest = lines.filter((l) => !/^[-*#>]/.test(l));
  return (bullets.length ? bullets : rest).slice(0, 6).map((l) => l.slice(0, 160));
}

/** Where a download may come from: GitHub, or the same place the release list came from. */
function fromSamePlace(url: string, releasesUrl: string): boolean {
  if (/^https:\/\/([a-z0-9-]+\.)*github(usercontent)?\.com\//i.test(url)) return true;
  try { return new URL(url).origin === new URL(releasesUrl).origin; } catch { return false; }
}

export type UpdateState =
  | { phase: 'none' }
  | { phase: 'found'; release: Release }
  | { phase: 'downloading'; release: Release; progress: number }
  | { phase: 'ready'; release: Release; file: string }
  | { phase: 'failed'; error: string };

/** What the owner is told, in one line. */
export function updateSentence(state: UpdateState): string {
  switch (state.phase) {
    case 'found': return `Version ${state.release.version} is ready to download.`;
    case 'downloading': return `Downloading version ${state.release.version}… ${Math.round(state.progress * 100)}%`;
    case 'ready': return `Version ${state.release.version} is ready to install.`;
    case 'failed': return state.error;
    default: return 'Unified Messenger is up to date.';
  }
}

/** How often the app asks. Rarely: a business does not need a new version the minute it exists. */
export const CHECK_EVERY_MS = 6 * 60 * 60_000;
/** The first check waits, so starting the app is never slowed by it. */
export const FIRST_CHECK_MS = 2 * 60_000;
