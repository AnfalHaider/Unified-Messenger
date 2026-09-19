// Signing in to the workspace (roadmap 6.1): Google in the owner's own browser, then Firebase. Google requires a
// desktop app to use the system browser with a loopback redirect and PKCE, and blocks sign-in inside embedded pages,
// so the app never shows Google's sign-in itself. Firebase is reached over its REST API, not the SDK: the app needs
// four calls, and the REST API is what the Phase 1 proof passed with.
//
// Everything here is pure (URLs, request bodies, parsing), so it is tested without a network. app/cloud.ts carries it
// out. What is sent: the Google code for tokens, and Google's id token to Firebase. What is kept on this PC: the
// Firebase user id, name, email and refresh token (encrypted). No oversight data is involved at any step.
import { createHash, randomBytes } from 'node:crypto';

/** The project's public identifiers, shipped with the app. Google does not treat a desktop client's secret as
 *  confidential, and a Firebase web key only names the project; the rules decide what anyone may do. */
export interface CloudConfig {
  firebase: { apiKey: string; projectId: string };
  oauth: { clientId: string; clientSecret: string };
}

/** Where each call goes. Tests point these at a fake server inside the test; nothing else changes them. */
export interface Endpoints { authorize: string; token: string; firebaseSignIn: string; firebaseRefresh: string }
export const GOOGLE_ENDPOINTS: Endpoints = {
  authorize: 'https://accounts.google.com/o/oauth2/v2/auth',
  token: 'https://oauth2.googleapis.com/token',
  firebaseSignIn: 'https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp',
  firebaseRefresh: 'https://securetoken.googleapis.com/v1/token',
};

/** Who is signed in, as kept on this PC. The refresh token is stored encrypted, never in this plain shape. */
export interface CloudSession { uid: string; email: string; name: string; refreshToken: string; signedInAt: number }

/** What the screens are told. Never the tokens. */
export type CloudState =
  | { phase: 'unavailable' }
  | { phase: 'signed-out'; error?: string }
  | { phase: 'waiting' }
  | { phase: 'signed-in'; email: string; name: string; since: number };

/** Reads the shipped config, or null when this build has none (a build made without the project's files). */
export function parseCloudConfig(text: string): CloudConfig | null {
  try {
    const j = JSON.parse(text) as Partial<CloudConfig>;
    const ok = (v: unknown) => typeof v === 'string' && v.length > 0;
    if (!ok(j.firebase?.apiKey) || !ok(j.firebase?.projectId) || !ok(j.oauth?.clientId) || !ok(j.oauth?.clientSecret)) return null;
    return { firebase: { apiKey: j.firebase!.apiKey, projectId: j.firebase!.projectId }, oauth: { clientId: j.oauth!.clientId, clientSecret: j.oauth!.clientSecret } };
  } catch { return null; }
}

/** The project's own two files, as Google and Firebase hand them out, turned into the shipped config. */
export function cloudConfigFrom(firebaseConfig: { apiKey: string; projectId: string }, oauthClient: { installed: { client_id: string; client_secret: string } }): CloudConfig {
  return {
    firebase: { apiKey: firebaseConfig.apiKey, projectId: firebaseConfig.projectId },
    oauth: { clientId: oauthClient.installed.client_id, clientSecret: oauthClient.installed.client_secret },
  };
}

const b64url = (buf: Buffer) => buf.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

/** One sign-in attempt's secrets: the PKCE verifier and its challenge, and the state that ties the reply to it. */
export function newAttempt(random = randomBytes) {
  const verifier = b64url(random(32));
  return { verifier, challenge: b64url(createHash('sha256').update(verifier).digest()), state: b64url(random(16)) };
}

/** The page the owner's browser opens. Name and email only: `openid email profile`, nothing more. */
export function authorizeUrl(ep: Endpoints, config: CloudConfig, redirect: string, attempt: { challenge: string; state: string }): string {
  const url = new URL(ep.authorize);
  url.search = new URLSearchParams({
    client_id: config.oauth.clientId, redirect_uri: redirect, response_type: 'code', scope: 'openid email profile',
    code_challenge: attempt.challenge, code_challenge_method: 'S256', state: attempt.state, prompt: 'select_account',
  }).toString();
  return url.toString();
}

/** Google's reply at the loopback address: the code, or why there is none, in words for the owner. */
export function readCallback(search: string, state: string): { code: string } | { error: string } {
  const p = new URLSearchParams(search);
  if (p.get('state') !== state) return { error: 'The reply from Google did not match this sign-in. Try again.' };
  const err = p.get('error');
  if (err === 'access_denied') return { error: 'Sign-in was cancelled in the browser.' };
  if (err) return { error: `Google did not sign you in (${err}).` };
  const code = p.get('code');
  return code ? { code } : { error: 'Google sent no sign-in code. Try again.' };
}

export const tokenBody = (config: CloudConfig, code: string, verifier: string, redirect: string) => new URLSearchParams({
  code, client_id: config.oauth.clientId, client_secret: config.oauth.clientSecret, code_verifier: verifier,
  grant_type: 'authorization_code', redirect_uri: redirect,
});

export const firebaseSignInBody = (idToken: string) => ({
  postBody: `id_token=${encodeURIComponent(idToken)}&providerId=google.com`, requestUri: 'http://localhost', returnSecureToken: true,
});

export const refreshBody = (refreshToken: string) => new URLSearchParams({ grant_type: 'refresh_token', refresh_token: refreshToken });

/** Firebase's sign-in reply, or the reason in words. The raw codes are Firebase's own; the owner sees sentences. */
export function readFirebaseSignIn(reply: unknown, now: number): CloudSession | { error: string } {
  const r = (reply ?? {}) as { localId?: string; email?: string; displayName?: string; fullName?: string; refreshToken?: string; error?: { message?: string } };
  if (r.localId && r.refreshToken) return { uid: r.localId, email: r.email ?? '', name: r.displayName || r.fullName || '', refreshToken: r.refreshToken, signedInAt: now };
  return { error: firebaseError(r.error?.message) };
}

/** A refresh: still signed in (possibly with a new refresh token), signed out for good, or not reachable now. */
export function readRefresh(status: number, reply: unknown): { ok: true; refreshToken: string; idToken: string } | { ok: false; final: boolean; error: string } {
  const r = (reply ?? {}) as { refresh_token?: string; id_token?: string; error?: { message?: string } };
  if (status === 200 && r.id_token) return { ok: true, refreshToken: r.refresh_token ?? '', idToken: r.id_token };
  const code = r.error?.message ?? '';
  // Only these mean the sign-in itself is over. Anything else (a server error, a quota) is tried again later.
  const final = /TOKEN_EXPIRED|USER_DISABLED|USER_NOT_FOUND|INVALID_REFRESH_TOKEN|MISSING_REFRESH_TOKEN/.test(code);
  return { ok: false, final, error: firebaseError(code) };
}

function firebaseError(code?: string): string {
  if (!code) return 'The workspace service did not answer as expected. Try again.';
  if (/USER_DISABLED/.test(code)) return 'This Google account has been disabled for Unified Messenger.';
  if (/TOKEN_EXPIRED|INVALID_REFRESH_TOKEN|USER_NOT_FOUND/.test(code)) return 'Your sign-in has ended. Sign in again.';
  if (/OPERATION_NOT_ALLOWED/.test(code)) return 'Google sign-in is switched off for this workspace service.';
  if (/INVALID_IDP_RESPONSE|INVALID_ID_TOKEN/.test(code)) return 'Google’s sign-in was not accepted. Try again.';
  return 'The workspace service refused the sign-in. Try again.';
}

/** The screens' view of a session: who, and since when. Tokens never leave the main process. */
export const signedInState = (s: CloudSession): CloudState => ({ phase: 'signed-in', email: s.email, name: s.name, since: s.signedInAt });
