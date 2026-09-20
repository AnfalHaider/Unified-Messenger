// Reading a Google Business profile's reviews through Google's own API (roadmap 3.1b), instead of the reviews page.
//
// **Switched off until Google approves this app.** Access to these APIs is granted by application; until then every
// call here is refused, so nothing runs unless the build has it on (`settings.googleApi.enabled`) and the owner has
// connected that profile. The page reader keeps working exactly as before in the meantime.
//
// No Electron here, so the tests run it in plain Node against a Google of their own: the host hands in how to
// encrypt what is kept (Windows' own protection, through safeStorage in main) and how to open a browser.
import { readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { authorizeUrl, newAttempt, readCallback, tokenBody, type CloudConfig, type Endpoints } from '../core/cloud-auth.ts';
import { apiError, BUSINESS_SCOPE, GOOGLE_API, locationsUrl, parseAccounts, parseLocations, parseReviews, reviewsUrl, type GoogleApi, type GoogleLocation } from '../core/google-api.ts';
import type { ProfileReviews, ReviewCard } from '../core/reviews.ts';
import { loopback } from './oauth-loopback.ts';

/** One connected profile, as it is kept on this PC. The refresh token is stored encrypted, never in this shape. */
interface Connection { location: string; title: string; connectedAt: number; refreshToken: string }
interface Stored { location: string; title: string; connectedAt: number; refresh: string }

/** What the screens are told about a profile: connected or not, and anything that went wrong in words. */
export interface GoogleApiState { connected: boolean; title: string; connectedAt: number; error?: string }

/** How many pages of reviews one read walks. Google sends 50 at a time; 20 pages is a thousand reviews. */
const MAX_PAGES = 20;

export interface GoogleBusinessHost {
  dataDir: string;
  /** The project's own desktop client, the same one sign-in uses. Null in a build without it: then nothing connects. */
  config: CloudConfig | null;
  endpoints: Endpoints;
  api?: GoogleApi;
  open: (url: string) => Promise<unknown>;
  /** Windows' own protection, through Electron's safeStorage in main; the tests hand in their own. */
  secret: { available: () => boolean; encrypt: (s: string) => string; decrypt: (s: string) => string };
  log: (entry: Record<string, unknown>) => void;
  changed: () => void;
  now?: () => number;
}

export class GoogleBusiness {
  private connections: Record<string, Connection> = {};
  private errors: Record<string, string> = {};
  private cancelWait: (() => void) | null = null;
  private readonly file: string;
  private readonly api: GoogleApi;
  private readonly h: GoogleBusinessHost;

  constructor(host: GoogleBusinessHost) {
    this.h = host;
    this.api = host.api ?? GOOGLE_API;
    this.file = join(host.dataDir, 'google-api.json');
    this.load();
  }

  private get clock() { return this.h.now?.() ?? Date.now(); }

  /** Whether this profile is read through the API rather than its page. Off unless connected and switched on. */
  reads(accountId: string, enabled: boolean): boolean { return enabled && !!this.connections[accountId]; }

  state(accountId: string): GoogleApiState {
    const c = this.connections[accountId];
    return { connected: !!c, title: c?.title ?? '', connectedAt: c?.connectedAt ?? 0, ...(this.errors[accountId] ? { error: this.errors[accountId] } : {}) };
  }

  /** Connects one profile: Google's consent in the owner's browser, then the profile this account will read. */
  async connect(accountId: string, pick?: (locations: GoogleLocation[]) => GoogleLocation | undefined): Promise<{ error?: string }> {
    const config = this.h.config;
    if (!config) return { error: 'This build cannot connect to Google.' };
    const attempt = newAttempt();
    try {
      const reply = await loopback({
        open: this.h.open,
        url: (redirect) => authorizeUrl(this.h.endpoints, config, redirect, attempt, { scope: BUSINESS_SCOPE, offline: true }),
        read: (search) => readCallback(search, attempt.state),
        done: { title: 'Connected', line: 'You can close this tab and go back to Unified Messenger.' },
        onCancel: (cancel) => { this.cancelWait = cancel; },
      });
      this.cancelWait = null;
      if ('error' in reply) return reply.error ? { error: reply.error } : {};

      const res = await fetch(this.h.endpoints.token, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: tokenBody(config, reply.code, attempt.verifier, reply.redirect) });
      const token = (await res.json().catch(() => ({}))) as { refresh_token?: string; access_token?: string };
      if (!token.refresh_token || !token.access_token) return { error: 'Google did not finish connecting. Try again.' };

      const locations = await this.locations(token.access_token);
      if ('error' in locations) return locations;
      const chosen = pick ? pick(locations.rows) : locations.rows[0];
      if (!chosen) return { error: 'That Google account manages no business profile.' };

      this.connections[accountId] = { location: chosen.name, title: chosen.title, connectedAt: this.clock, refreshToken: token.refresh_token };
      this.save();
      delete this.errors[accountId];
      this.h.log({ event: 'google-api-connected', account: accountId, profiles: locations.rows.length });
      this.h.changed();
      return {};
    } catch (e) {
      this.cancelWait = null;
      this.h.log({ event: 'google-api-connect-failed', account: accountId, error: String((e as Error).message).slice(0, 120) });
      return { error: 'Google could not be reached. Check the connection and try again.' };
    }
  }

  /** Stops waiting for the browser. */
  cancel() { this.cancelWait?.(); }

  /** Forgets the connection on this PC. The Google account itself is untouched. */
  disconnect(accountId: string) {
    delete this.connections[accountId];
    delete this.errors[accountId];
    this.save();
    this.h.log({ event: 'google-api-disconnected', account: accountId });
    this.h.changed();
  }

  /** Every review Google has for the connected profile, newest first, with the rating and lifetime total. */
  async read(accountId: string): Promise<{ reviews: ProfileReviews } | { error: string }> {
    const connection = this.connections[accountId];
    if (!connection) return { error: 'This profile is not connected to Google.' };
    const access = await this.accessToken(connection);
    if ('error' in access) return this.failed(accountId, access.error);
    const cards: ReviewCard[] = [];
    let rating: number | null = null, total: number | null = null, next: string | null = null;
    for (let page = 0; page < MAX_PAGES; page++) {
      const url = new URL(reviewsUrl(this.api, connection.location));
      url.searchParams.set('pageSize', '50');
      if (next) url.searchParams.set('pageToken', next);
      const res = await fetch(url, { headers: { Authorization: `Bearer ${access.token}` } });
      const body = await res.json().catch(() => ({}));
      if (!res.ok) return this.failed(accountId, apiError(res.status, body));
      const read = parseReviews(body, this.clock);
      cards.push(...read.cards);
      if (page === 0) { rating = read.rating; total = read.total; }
      next = read.next;
      if (!next) break;
    }
    delete this.errors[accountId];
    this.h.log({ event: 'google-api-read', account: accountId, reviews: cards.length, unanswered: cards.filter((c) => !c.replied).length, more: !!next });
    return { reviews: { capturedAt: this.clock, cards, more: !!next, rating, total, ratingAt: rating === null && total === null ? null : this.clock } };
  }

  private failed(accountId: string, error: string): { error: string } {
    this.errors[accountId] = error;
    this.h.log({ event: 'google-api-read-failed', account: accountId });
    this.h.changed();
    return { error };
  }

  /** A fresh access token from the refresh token Google gave when the profile was connected. */
  private async accessToken(connection: Connection): Promise<{ token: string } | { error: string }> {
    const config = this.h.config;
    if (!config) return { error: 'This build cannot connect to Google.' };
    const res = await fetch(this.h.endpoints.token, {
      method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ client_id: config.oauth.clientId, client_secret: config.oauth.clientSecret, refresh_token: connection.refreshToken, grant_type: 'refresh_token' }),
    });
    const body = (await res.json().catch(() => ({}))) as { access_token?: string };
    if (!res.ok || !body.access_token) return { error: apiError(res.status === 200 ? 401 : res.status, body) };
    return { token: body.access_token };
  }

  /** Every profile this Google account manages, across its accounts. */
  private async locations(access: string): Promise<{ rows: GoogleLocation[] } | { error: string }> {
    const head = { headers: { Authorization: `Bearer ${access}` } };
    const accountsRes = await fetch(this.api.accounts, head);
    const accountsBody = await accountsRes.json().catch(() => ({}));
    if (!accountsRes.ok) return { error: apiError(accountsRes.status, accountsBody) };
    const rows: GoogleLocation[] = [];
    for (const account of parseAccounts(accountsBody)) {
      const url = new URL(locationsUrl(this.api, account.name));
      url.searchParams.set('readMask', 'name,title');
      const res = await fetch(url, head);
      const body = await res.json().catch(() => ({}));
      if (!res.ok) return { error: apiError(res.status, body) };
      rows.push(...parseLocations(body, account.name));
    }
    return { rows };
  }

  private load() {
    try {
      if (!existsSync(this.file) || !this.h.secret.available()) return;
      const kept = JSON.parse(readFileSync(this.file, 'utf8')) as Record<string, Stored>;
      for (const [id, c] of Object.entries(kept)) {
        if (!c?.refresh || !c.location) continue;
        this.connections[id] = { location: c.location, title: c.title ?? '', connectedAt: c.connectedAt ?? 0, refreshToken: this.h.secret.decrypt(c.refresh) };
      }
    } catch (e) {
      this.h.log({ event: 'google-api-load-failed', error: String((e as Error).message).slice(0, 120) });
      this.connections = {};
    }
  }

  private save() {
    if (!Object.keys(this.connections).length) { rmSync(this.file, { force: true }); return; }
    if (!this.h.secret.available()) { this.h.log({ event: 'google-api-not-kept', reason: 'no-encryption' }); return; }
    const kept: Record<string, Stored> = {};
    for (const [id, c] of Object.entries(this.connections)) kept[id] = { location: c.location, title: c.title, connectedAt: c.connectedAt, refresh: this.h.secret.encrypt(c.refreshToken) };
    writeFileSync(this.file, JSON.stringify(kept, null, 2));
  }
}
