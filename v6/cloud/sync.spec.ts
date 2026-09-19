// The workspace sync (app/workspace.ts) against the Firestore emulator, under the real rules (roadmap 6.3). Run by
// `npm run rules:test` with the rules tests. Each "PC" here is its own data folder and config, signed in as an invented
// person with a token the emulator accepts; nothing reaches the real project.
import { after, before, beforeEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { initializeTestEnvironment, type RulesTestEnvironment } from '@firebase/rules-unit-testing';
import { doc, setDoc, updateDoc, type Firestore } from 'firebase/firestore';
import { Workspace } from '../app/workspace.ts';
import { parseConfig, type Config } from '../core/config.ts';
import { applySetup, sharedSetup } from '../core/workspace-sync.ts';

const PROJECT = 'demo-unified-messenger';
const BASE = `http://127.0.0.1:8181/v1/projects/${PROJECT}/databases/(default)`;
let env: RulesTestEnvironment;
const folders: string[] = [];

before(async () => {
  env = await initializeTestEnvironment({ projectId: PROJECT, firestore: { rules: readFileSync(new URL('./firestore.rules', import.meta.url), 'utf8'), host: '127.0.0.1', port: 8181 } });
});
after(async () => { await env?.cleanup(); for (const f of folders) rmSync(f, { recursive: true, force: true }); });
beforeEach(async () => { await env.clearFirestore(); });

/** An unsigned token the emulator accepts, as Firebase would issue it for a Google sign-in. */
const token = (uid: string, email: string) => {
  const b64 = (o: unknown) => Buffer.from(JSON.stringify(o)).toString('base64url');
  const t = Math.floor(Date.now() / 1000);
  return `${b64({ alg: 'none', typ: 'JWT' })}.${b64({ iss: `https://securetoken.google.com/${PROJECT}`, aud: PROJECT, iat: t, exp: t + 3600, auth_time: t, sub: uid, user_id: uid, email, email_verified: true, firebase: { sign_in_provider: 'google.com' } })}.`;
};

const OWNER = { uid: 'owner-uid', email: 'owner@example.com', name: 'Sample Owner' };
const STAFF = { uid: 'staff-uid', email: 'staff@example.com', name: 'Sample Staff' };

const startingConfig = () => parseConfig({
  accounts: [
    { id: 'a1', name: 'Main branch WhatsApp', channel: 'whatsapp', url: 'https://web.whatsapp.com/', location: 'Main branch', professional: true },
    { id: 'a2', name: 'Main branch Instagram', channel: 'instagram', url: 'https://www.instagram.com/', location: 'Main branch', professional: true },
  ],
  locations: [{ name: 'Main branch', slaMinutes: 20, hours: null }],
  settings: { slaMinutes: 15, theme: 'dark' },
}).config;

/** One PC: its own folder and config, and the workspace client wired the way main wires it. */
function pc(user: typeof OWNER, config: Config = parseConfig({}).config) {
  const dataDir = mkdtempSync(join(tmpdir(), 'um-sync-'));
  folders.push(dataDir);
  const here = { config, removed: [] as string[], added: [] as string[] };
  const ws: Workspace = new Workspace({
    base: BASE, dataDir, token: async () => token(user.uid, user.email), user: () => user,
    local: () => sharedSetup(here.config),
    apply: (setup) => {
      const r = applySetup(here.config, setup, ws.synced);
      here.config = r.config; here.added.push(...r.added); here.removed.push(...r.removed);
      return [...r.synced];
    },
    changed: () => {}, log: () => {},
  });
  return Object.assign(here, { ws });
}

test('the owner starts a workspace from this PC, and their second PC gets its accounts and rules', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.check();
  assert.equal(a.ws.state.phase, 'none');
  assert.deepEqual(await a.ws.create('Sample Business'), {});
  assert.equal(a.ws.state.phase, 'member');
  assert.equal((a.ws.state as { role: string }).role, 'admin');

  const b = pc(OWNER);
  await b.ws.check();
  assert.equal(b.ws.state.phase, 'member');
  assert.deepEqual(b.config.accounts.map((x) => x.id), ['a1', 'a2']);
  assert.deepEqual(b.added, ['a1', 'a2']);
  assert.equal(b.config.locations[0].slaMinutes, 20);
  // What this PC keeps to itself did not travel.
  assert.equal(b.config.settings.theme, 'system');
});

test('an admin’s change reaches the other PC at its next pull, including an account removed', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  const b = pc(OWNER);
  await b.ws.check();

  a.config = parseConfig({ ...a.config, accounts: a.config.accounts.filter((x) => x.id !== 'a2').map((x) => ({ ...x, name: 'Front desk WhatsApp' })), settings: { ...a.config.settings, slaMinutes: 25 } }).config;
  a.ws.tick();
  await a.ws.pull(); // waits for the push the tick started, then confirms nothing else changed
  await b.ws.pull();
  assert.deepEqual(b.config.accounts.map((x) => x.name), ['Front desk WhatsApp']);
  assert.equal(b.config.settings.slaMinutes, 25);
  assert.deepEqual(b.removed, ['a2']);
});

test('two PCs changing the setup at once: the second is told, and neither silently overwrites the other', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  const b = pc(OWNER);
  await b.ws.check();

  a.config = parseConfig({ ...a.config, settings: { ...a.config.settings, slaMinutes: 30 } }).config;
  b.config = parseConfig({ ...b.config, settings: { ...b.config.settings, slaMinutes: 45 } }).config;
  a.ws.tick(); await a.ws.pull();
  b.ws.tick(); await b.ws.pull();
  assert.equal(b.config.settings.slaMinutes, 30, 'the first change stands');
  assert.match((b.ws.state as { note?: string }).note ?? '', /Another PC changed the shared setup/);
});

test('a member who is not an admin receives the setup and cannot change it', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  const id = (a.ws.state as { id: string }).id;
  await env.withSecurityRulesDisabled(async (ctx) => {
    await setDoc(doc(ctx.firestore() as unknown as Firestore, `workspaces/${id}/members/${STAFF.uid}`), { email: STAFF.email, name: STAFF.name, role: 'member', status: 'active', joinedAt: new Date(), lastSeen: new Date() });
  });
  const s = pc(STAFF);
  await s.ws.check();
  assert.equal((s.ws.state as { role: string }).role, 'member');
  assert.deepEqual(s.config.accounts.map((x) => x.id), ['a1', 'a2']);
  assert.equal(s.ws.readOnly, true);
  // A change made here anyway is not sent.
  s.config = parseConfig({ ...s.config, settings: { ...s.config.settings, slaMinutes: 99 } }).config;
  s.ws.tick(); await s.ws.pull();
  const back = pc(OWNER); await back.ws.check();
  assert.equal(back.config.settings.slaMinutes, 15);
});

test('a suspended workspace is recognised, and its setup is not read', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  const id = (a.ws.state as { id: string }).id;
  await env.withSecurityRulesDisabled(async (ctx) => { await updateDoc(doc(ctx.firestore() as unknown as Firestore, `workspaces/${id}`), { status: 'suspended' }); });
  const b = pc(OWNER);
  await b.ws.check();
  assert.equal((b.ws.state as { status: string }).status, 'suspended');
  assert.deepEqual(b.config.accounts, []);
});

test('a removed member is told so, and gets nothing', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  const id = (a.ws.state as { id: string }).id;
  await env.withSecurityRulesDisabled(async (ctx) => {
    await setDoc(doc(ctx.firestore() as unknown as Firestore, `workspaces/${id}/members/${STAFF.uid}`), { email: STAFF.email, name: STAFF.name, role: 'member', status: 'removed', joinedAt: new Date(), lastSeen: new Date() });
  });
  const s = pc(STAFF);
  await s.ws.check();
  assert.equal(s.ws.state.phase, 'removed');
  assert.equal((s.ws.state as { name: string }).name, 'Sample Business');
  assert.deepEqual(s.config.accounts, []);
});
