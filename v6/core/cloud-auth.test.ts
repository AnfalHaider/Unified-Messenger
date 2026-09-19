import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import {
  authorizeUrl, cloudConfigFrom, firebaseSignInBody, GOOGLE_ENDPOINTS, newAttempt, parseCloudConfig, readCallback,
  readFirebaseSignIn, readRefresh, refreshBody, signedInState, tokenBody,
} from './cloud-auth.ts';

const CONFIG = cloudConfigFrom({ apiKey: 'test-key', projectId: 'test-project' }, { installed: { client_id: 'test-client', client_secret: 'test-secret' } });

test('the shipped config is read only when every part is there', () => {
  assert.deepEqual(parseCloudConfig(JSON.stringify(CONFIG)), CONFIG);
  assert.equal(parseCloudConfig('{"firebase":{"apiKey":"k","projectId":"p"}}'), null);
  assert.equal(parseCloudConfig('not json'), null);
  assert.equal(parseCloudConfig(JSON.stringify({ ...CONFIG, oauth: { clientId: '', clientSecret: 's' } })), null);
});

test('the challenge is the SHA-256 of the verifier, and each attempt is new', () => {
  const a = newAttempt(), b = newAttempt();
  const expected = createHash('sha256').update(a.verifier).digest('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  assert.equal(a.challenge, expected);
  assert.notEqual(a.verifier, b.verifier);
  assert.notEqual(a.state, b.state);
  assert.match(a.verifier, /^[A-Za-z0-9_-]{43}$/);
});

test('the browser is asked for name and email only, with PKCE, back to this PC', () => {
  const url = new URL(authorizeUrl(GOOGLE_ENDPOINTS, CONFIG, 'http://127.0.0.1:5123', { challenge: 'c', state: 's' }));
  assert.equal(url.origin + url.pathname, 'https://accounts.google.com/o/oauth2/v2/auth');
  assert.equal(url.searchParams.get('scope'), 'openid email profile');
  assert.equal(url.searchParams.get('redirect_uri'), 'http://127.0.0.1:5123');
  assert.equal(url.searchParams.get('code_challenge_method'), 'S256');
  assert.equal(url.searchParams.get('client_id'), 'test-client');
});

test('Google’s reply is taken only for this attempt, and a refusal is said in words', () => {
  assert.deepEqual(readCallback('?code=abc&state=s1', 's1'), { code: 'abc' });
  assert.match((readCallback('?code=abc&state=other', 's1') as { error: string }).error, /did not match/);
  assert.equal((readCallback('?error=access_denied&state=s1', 's1') as { error: string }).error, 'Sign-in was cancelled in the browser.');
  assert.match((readCallback('?state=s1', 's1') as { error: string }).error, /no sign-in code/);
});

test('the bodies carry the verifier and the id token, and nothing else of note', () => {
  const t = tokenBody(CONFIG, 'code1', 'ver1', 'http://127.0.0.1:1');
  assert.equal(t.get('code_verifier'), 'ver1');
  assert.equal(t.get('grant_type'), 'authorization_code');
  assert.equal(firebaseSignInBody('a.b+c').postBody, 'id_token=a.b%2Bc&providerId=google.com');
  assert.equal(refreshBody('r1').get('refresh_token'), 'r1');
});

test('Firebase’s answer becomes a session or a sentence, never a raw code', () => {
  const s = readFirebaseSignIn({ localId: 'u1', email: 'owner@example.com', displayName: 'Sample Owner', refreshToken: 'r1' }, 1000);
  assert.deepEqual(s, { uid: 'u1', email: 'owner@example.com', name: 'Sample Owner', refreshToken: 'r1', signedInAt: 1000 });
  assert.deepEqual(signedInState(s as never), { phase: 'signed-in', email: 'owner@example.com', name: 'Sample Owner', since: 1000 });
  assert.equal((readFirebaseSignIn({ error: { message: 'USER_DISABLED' } }, 0) as { error: string }).error, 'This Google account has been disabled for Unified Messenger.');
  assert.ok(!('error' in s) || !/[A-Z_]{6,}/.test((s as { error: string }).error));
});

test('a refresh ends the sign-in only when Firebase says it is over; anything else is tried again', () => {
  assert.deepEqual(readRefresh(200, { id_token: 'i', refresh_token: 'r2' }), { ok: true, refreshToken: 'r2', idToken: 'i' });
  assert.equal((readRefresh(400, { error: { message: 'TOKEN_EXPIRED' } }) as { final: boolean }).final, true);
  assert.equal((readRefresh(400, { error: { message: 'USER_DISABLED' } }) as { final: boolean }).final, true);
  assert.equal((readRefresh(503, {}) as { final: boolean }).final, false);
  assert.equal((readRefresh(429, { error: { message: 'QUOTA_EXCEEDED' } }) as { final: boolean }).final, false);
});
