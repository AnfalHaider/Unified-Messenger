// The Google Business API reader (roadmap 3.1b) against a Google of its own: connect, read every page, and what it
// does when Google refuses. Nothing here reaches the real API, which has not granted this app access yet.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer, type Server } from 'node:http';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { GoogleBusiness } from './google-api.ts';
import type { CloudConfig, Endpoints } from '../core/cloud-auth.ts';

const CONFIG: CloudConfig = { firebase: { apiKey: 'test-key', projectId: 'test-project' }, oauth: { clientId: 'test-client', clientSecret: 'test-secret' } };
const NOW = Date.parse('2026-09-20T12:00:00Z');

/** An invented Google: the consent page sends the browser back to the app, then accounts, locations and reviews. */
async function fakeGoogle(o: { deny?: boolean; refuse?: number; pages?: number } = {}) {
  const calls: { path: string; auth: string; query: URLSearchParams }[] = [];
  let server!: Server;
  server = createServer(async (req, res) => {
    let body = '';
    for await (const chunk of req) body += chunk;
    const url = new URL(req.url ?? '/', 'http://x');
    calls.push({ path: url.pathname, auth: req.headers.authorization ?? '', query: url.searchParams });
    const json = (status: number, v: unknown) => res.writeHead(status, { 'Content-Type': 'application/json' }).end(JSON.stringify(v));
    if (url.pathname === '/authorize') {
      const back = new URL(url.searchParams.get('redirect_uri')!);
      back.search = new URLSearchParams(o.deny ? { error: 'access_denied', state: url.searchParams.get('state')! } : { code: 'test-code', state: url.searchParams.get('state')! }).toString();
      res.writeHead(302, { Location: back.toString() }).end();
    } else if (url.pathname === '/token') {
      json(200, new URLSearchParams(body).get('grant_type') === 'refresh_token'
        ? { access_token: 'fresh-access' }
        : { access_token: 'first-access', refresh_token: 'test-refresh-3311' });
    } else if (o.refuse) json(o.refuse, { error: { message: 'The caller does not have permission' } });
    else if (url.pathname === '/accounts') json(200, { accounts: [{ name: 'accounts/123', accountName: 'Sample Business' }] });
    else if (url.pathname.endsWith('/locations')) json(200, { locations: [{ name: 'locations/456', title: 'Main branch' }, { name: 'locations/789', title: 'North branch' }] });
    else if (url.pathname.endsWith('/reviews')) {
      const page = Number(url.searchParams.get('pageToken') ?? '1');
      const last = page >= (o.pages ?? 1);
      json(200, {
        reviews: [{ reviewId: `r${page}`, reviewer: { displayName: `Reviewer ${page}` }, starRating: page === 1 ? 'ONE' : 'FIVE', comment: 'A review.', createTime: '2026-09-19T12:00:00Z' }],
        ...(page === 1 ? { averageRating: 4.6, totalReviewCount: 991 } : {}),
        ...(last ? {} : { nextPageToken: String(page + 1) }),
      });
    } else res.writeHead(404).end();
  });
  await new Promise<void>((done) => server.listen(0, '127.0.0.1', done));
  const at = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
  const endpoints: Endpoints = { authorize: `${at}/authorize`, token: `${at}/token`, firebaseSignIn: `${at}/signin`, firebaseRefresh: `${at}/refresh` };
  const api = { accounts: `${at}/accounts`, locations: `${at}/{account}/locations`, reviews: `${at}/{location}/reviews` };
  return { endpoints, api, calls, close: () => new Promise<void>((done) => { server.close(() => done()); }) };
}

/** The host main gives it, with plain reversible "encryption" in place of Windows' own. */
function pc(google: Awaited<ReturnType<typeof fakeGoogle>>, o: { dataDir?: string } = {}) {
  const dataDir = o.dataDir ?? mkdtempSync(join(tmpdir(), 'um-gapi-'));
  const opened: string[] = [];
  const reader = new GoogleBusiness({
    dataDir, config: CONFIG, endpoints: google.endpoints, api: google.api,
    open: async (url) => { opened.push(url); await fetch(url); },
    secret: { available: () => true, encrypt: (s) => Buffer.from(s).toString('base64'), decrypt: (s) => Buffer.from(s, 'base64').toString('utf8') },
    log: () => {}, changed: () => {}, now: () => NOW,
  });
  return { reader, dataDir, opened };
}

test('connecting: Google’s own consent, the business scope, and a refresh token kept encrypted', async () => {
  const google = await fakeGoogle();
  const { reader, dataDir, opened } = pc(google);
  try {
    assert.deepEqual(reader.state('g1'), { connected: false, title: '', connectedAt: 0 });
    assert.deepEqual(await reader.connect('g1'), {});
    const asked = new URL(opened[0]).searchParams;
    assert.equal(asked.get('scope'), 'https://www.googleapis.com/auth/business.manage');
    assert.equal(asked.get('access_type'), 'offline', 'or Google sends no refresh token');
    assert.equal(asked.get('prompt'), 'consent');
    assert.match(asked.get('redirect_uri')!, /^http:\/\/127\.0\.0\.1:\d+$/);
    assert.deepEqual(reader.state('g1'), { connected: true, title: 'Main branch', connectedAt: NOW });

    // Kept on this PC, and not in the clear.
    const kept = readFileSync(join(dataDir, 'google-api.json'), 'utf8');
    assert.ok(kept.includes('accounts/123/locations/456'));
    assert.ok(!kept.includes('test-refresh-3311'));
    // It survives a restart, and the connection is used rather than asked for again.
    const again = pc(google, { dataDir });
    assert.equal(again.reader.state('g1').connected, true);
    assert.equal(again.reader.reads('g1', true), true);
    // Off unless the build has it switched on.
    assert.equal(again.reader.reads('g1', false), false);
    assert.equal(again.reader.reads('other', true), false);
  } finally {
    rmSync(dataDir, { recursive: true, force: true });
    await google.close();
  }
});

test('the owner chooses which profile when the account manages several', async () => {
  const google = await fakeGoogle();
  const { reader, dataDir } = pc(google);
  try {
    await reader.connect('g1', (rows) => rows.find((r) => r.title === 'North branch'));
    assert.equal(reader.state('g1').title, 'North branch');
  } finally { rmSync(dataDir, { recursive: true, force: true }); await google.close(); }
});

test('reading: every page, the rating and total from the first, and the cards the app already draws', async () => {
  const google = await fakeGoogle({ pages: 3 });
  const { reader, dataDir } = pc(google);
  try {
    await reader.connect('g1');
    const result = await reader.read('g1');
    assert.ok('reviews' in result);
    const { reviews } = result;
    assert.deepEqual(reviews.cards.map((c) => c.reviewer), ['Reviewer 1', 'Reviewer 2', 'Reviewer 3']);
    assert.deepEqual([reviews.rating, reviews.total, reviews.more], [4.6, 991, false]);
    assert.equal(reviews.cards[0].age, '1 day ago');
    assert.equal(reviews.capturedAt, NOW);
    // Each call carried a fresh access token, asked for with the refresh token.
    const reads = google.calls.filter((c) => c.path.endsWith('/reviews'));
    assert.equal(reads.length, 3);
    assert.ok(reads.every((c) => c.auth === 'Bearer fresh-access'));
    assert.deepEqual(reads.map((c) => c.query.get('pageToken')), [null, '2', '3']);
  } finally { rmSync(dataDir, { recursive: true, force: true }); await google.close(); }
});

test('a refusal is said in words and kept beside the profile, and the read gives nothing', async () => {
  const open = await fakeGoogle();
  const { reader, dataDir } = pc(open);
  try {
    await reader.connect('g1');
    await open.close();
    const refusing = await fakeGoogle({ refuse: 403 });
    const second = pc(refusing, { dataDir });
    const result = await second.reader.read('g1');
    assert.ok('error' in result);
    assert.match(result.error, /cannot manage that business profile/);
    assert.equal(second.reader.state('g1').error, result.error);
    await refusing.close();
  } finally { rmSync(dataDir, { recursive: true, force: true }); }
});

test('cancelled in the browser, and disconnecting, leave nothing behind', async () => {
  const google = await fakeGoogle({ deny: true });
  const { reader, dataDir } = pc(google);
  try {
    assert.match((await reader.connect('g1')).error ?? '', /cancelled in the browser/);
    assert.equal(reader.state('g1').connected, false);
    assert.ok(!readdirSync(dataDir).includes('google-api.json'));

    await google.close();
    const ok = await fakeGoogle();
    const second = pc(ok, { dataDir });
    await second.reader.connect('g1');
    second.reader.disconnect('g1');
    assert.equal(second.reader.state('g1').connected, false);
    assert.ok(!readdirSync(dataDir).includes('google-api.json'), 'the file goes with the last connection');
    await ok.close();
  } finally { rmSync(dataDir, { recursive: true, force: true }); }
});
