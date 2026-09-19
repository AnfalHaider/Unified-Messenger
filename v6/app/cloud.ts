// Signing in to the workspace, carried out (roadmap 6.1). core/cloud-auth.ts decides every URL, body and reply; this
// file opens the browser, listens once on a loopback port for Google's reply, and keeps the session.
//
// Kept on this PC in cloud.json: the Firebase user id, name, email and sign-in time, and the refresh token encrypted
// with Windows' own data protection (Electron's safeStorage), so another user of the PC cannot read it. Where that
// protection is not available the token is kept only while the app runs, and the owner signs in again next time.
// The log records that a sign-in happened or failed, never the email.
import { safeStorage } from 'electron';
import { createServer, type Server } from 'node:http';
import { readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import {
  authorizeUrl, firebaseSignInBody, newAttempt, readCallback, readFirebaseSignIn, readRefresh, refreshBody, signedInState, tokenBody,
  type CloudConfig, type CloudSession, type CloudState, type Endpoints,
} from '../core/cloud-auth.ts';

const WAIT_MS = 5 * 60_000;
const REFRESH_EVERY_MS = 60 * 60_000;

const PAGE = (title: string, line: string) => `<!doctype html><meta charset="utf-8"><title>Unified Messenger</title>
<body style="font-family:system-ui,sans-serif;display:grid;place-items:center;min-height:90vh;margin:0;color:#1d211e">
<div style="max-width:420px;text-align:center"><h1 style="font-size:20px;font-weight:600">${title}</h1><p style="color:#5f6660">${line}</p></div>`;

interface Stored { uid: string; email: string; name: string; signedInAt: number; refresh: string }

export class Cloud {
  state: CloudState;
  /** The current Firebase id token, in memory only. The workspace calls (6.3) will send it. */
  idToken = '';
  private session: CloudSession | null = null;
  private server: Server | null = null;
  private cancelWait: (() => void) | null = null;
  private timer: NodeJS.Timeout | null = null;
  private readonly file: string;
  private readonly config: CloudConfig | null;
  private readonly ep: Endpoints;
  private readonly open: (url: string) => Promise<unknown>;
  private readonly changed: () => void;
  private readonly log: (entry: Record<string, unknown>) => void;

  constructor(o: { dataDir: string; config: CloudConfig | null; endpoints: Endpoints; open: (url: string) => Promise<unknown>; changed: () => void; log: (entry: Record<string, unknown>) => void }) {
    this.file = join(o.dataDir, 'cloud.json');
    this.config = o.config; this.ep = o.endpoints; this.open = o.open; this.changed = o.changed; this.log = o.log;
    this.state = o.config ? { phase: 'signed-out' } : { phase: 'unavailable' };
  }

  /** At startup: the kept session, if any, then a refresh in the background to learn whether it still holds. */
  load() {
    if (!this.config || !existsSync(this.file)) return;
    try {
      const s = JSON.parse(readFileSync(this.file, 'utf8')) as Stored;
      const refreshToken = s.refresh && safeStorage.isEncryptionAvailable() ? safeStorage.decryptString(Buffer.from(s.refresh, 'base64')) : '';
      if (!s.uid || !refreshToken) { this.forget(); return; }
      this.session = { uid: s.uid, email: s.email, name: s.name, signedInAt: s.signedInAt, refreshToken };
      this.set(signedInState(this.session));
      void this.refresh();
      this.schedule();
    } catch (e) {
      this.log({ event: 'cloud-load-failed', error: String((e as Error).message).slice(0, 120) });
      this.forget();
    }
  }

  /** Opens Google in the owner's browser and waits, up to five minutes, for the reply at a loopback address. */
  async signIn(): Promise<void> {
    if (!this.config || this.state.phase === 'waiting') return;
    const config = this.config;
    const attempt = newAttempt();
    this.set({ phase: 'waiting' });
    try {
      const server = createServer();
      this.server = server;
      await new Promise<void>((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
      const redirect = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
      const reply = await new Promise<{ code: string } | { error: string }>((resolve) => {
        const timer = setTimeout(() => resolve({ error: 'Nothing came back from the browser in five minutes. Try again.' }), WAIT_MS);
        this.cancelWait = () => { clearTimeout(timer); resolve({ error: '' }); };
        server.on('request', (req, res) => {
          const url = new URL(req.url ?? '/', redirect);
          if (url.pathname !== '/') { res.writeHead(404).end(); return; }
          const r = readCallback(url.search, attempt.state);
          res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }).end('code' in r
            ? PAGE('Signed in', 'You can close this tab and go back to Unified Messenger.')
            : PAGE('Not signed in', `${r.error} You can close this tab.`));
          clearTimeout(timer);
          resolve(r);
        });
        void this.open(authorizeUrl(this.ep, config, redirect, attempt)).catch(() => resolve({ error: 'The browser could not be opened.' }));
      });
      this.closeServer();
      if ('error' in reply) { this.fail(reply.error); return; }

      const tokenRes = await fetch(this.ep.token, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: tokenBody(config, reply.code, attempt.verifier, redirect) });
      const token = (await tokenRes.json().catch(() => ({}))) as { id_token?: string };
      if (!token.id_token) { this.fail('Google did not finish the sign-in. Try again.'); return; }

      const fbRes = await fetch(`${this.ep.firebaseSignIn}?key=${encodeURIComponent(config.firebase.apiKey)}`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(firebaseSignInBody(token.id_token)),
      });
      const fb = readFirebaseSignIn(await fbRes.json().catch(() => ({})), Date.now());
      if ('error' in fb) { this.fail(fb.error); return; }
      this.session = fb;
      this.save();
      this.set(signedInState(fb));
      this.log({ event: 'cloud-signed-in' });
      void this.refresh();
      this.schedule();
    } catch (e) {
      this.closeServer();
      this.log({ event: 'cloud-sign-in-failed', error: String((e as Error).message).slice(0, 120) });
      this.fail('The sign-in could not reach Google. Check the connection and try again.');
    }
  }

  /** Stops waiting for the browser. */
  cancel() { this.cancelWait?.(); }

  /** Forgets the session on this PC. The Google account itself is untouched. */
  signOut() {
    this.cancel();
    this.forget();
    this.log({ event: 'cloud-signed-out' });
  }

  /** Swaps the refresh token for a fresh id token. Ends the session only when Firebase says it is over; offline or a
   *  server error keeps it, and it is tried again within the hour. */
  async refresh(): Promise<void> {
    if (!this.config || !this.session) return;
    try {
      const res = await fetch(`${this.ep.firebaseRefresh}?key=${encodeURIComponent(this.config.firebase.apiKey)}`, {
        method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: refreshBody(this.session.refreshToken),
      });
      const r = readRefresh(res.status, await res.json().catch(() => ({})));
      if (r.ok) {
        this.idToken = r.idToken;
        if (r.refreshToken && r.refreshToken !== this.session.refreshToken) { this.session.refreshToken = r.refreshToken; this.save(); }
        this.log({ event: 'cloud-refreshed' });
      } else if (r.final) {
        this.log({ event: 'cloud-session-ended' });
        this.forget(r.error);
      } else this.log({ event: 'cloud-refresh-failed', status: res.status });
    } catch {
      this.log({ event: 'cloud-refresh-failed', status: 0 });
    }
  }

  stop() { this.cancel(); this.closeServer(); if (this.timer) clearInterval(this.timer); }

  private schedule() {
    if (this.timer) clearInterval(this.timer);
    this.timer = setInterval(() => void this.refresh(), REFRESH_EVERY_MS);
    this.timer.unref();
  }

  private fail(error: string) {
    if (error) this.log({ event: 'cloud-sign-in-failed' });
    this.set({ phase: 'signed-out', ...(error ? { error } : {}) });
  }

  private forget(error?: string) {
    this.session = null;
    this.idToken = '';
    if (this.timer) { clearInterval(this.timer); this.timer = null; }
    rmSync(this.file, { force: true });
    this.set({ phase: 'signed-out', ...(error ? { error } : {}) });
  }

  private save() {
    if (!this.session) return;
    if (!safeStorage.isEncryptionAvailable()) { this.log({ event: 'cloud-not-kept', reason: 'no-encryption' }); return; }
    const s = this.session;
    const stored: Stored = { uid: s.uid, email: s.email, name: s.name, signedInAt: s.signedInAt, refresh: safeStorage.encryptString(s.refreshToken).toString('base64') };
    writeFileSync(this.file, JSON.stringify(stored, null, 2));
  }

  private closeServer() { this.server?.close(); this.server = null; this.cancelWait = null; }

  private set(state: CloudState) { this.state = state; this.changed(); }
}
