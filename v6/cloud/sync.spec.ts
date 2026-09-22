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
const PRODUCT_OWNER = { uid: 'product-owner-uid', email: 'product.owner@example.com', name: 'Product Owner' };

const startingConfig = () => parseConfig({
  accounts: [
    { id: 'a1', name: 'Main branch WhatsApp', channel: 'whatsapp', url: 'https://web.whatsapp.com/', location: 'Main branch', professional: true },
    { id: 'a2', name: 'Main branch Instagram', channel: 'instagram', url: 'https://www.instagram.com/', location: 'Main branch', professional: true },
  ],
  locations: [{ name: 'Main branch', slaMinutes: 20, hours: null }],
  settings: { slaMinutes: 15, theme: 'dark' },
}).config;

/** One PC: its own folder and config, and the workspace client wired the way main wires it. */
function pc(user: typeof OWNER, config: Config = parseConfig({}).config, o: { dataDir?: string; base?: string; now?: () => number } = {}) {
  const dataDir = o.dataDir ?? mkdtempSync(join(tmpdir(), 'um-sync-'));
  folders.push(dataDir);
  const here = { config, dataDir, removed: [] as string[], added: [] as string[], wipedOnRemoval: [] as string[] };
  const ws: Workspace = new Workspace({
    base: o.base ?? BASE, dataDir, token: async () => token(user.uid, user.email), user: () => user, now: o.now,
    removed: async (ids) => {
      here.wipedOnRemoval = ids;
      const names = here.config.accounts.filter((a) => ids.includes(a.id)).map((a) => a.name);
      here.config = parseConfig({ ...here.config, accounts: here.config.accounts.filter((a) => !ids.includes(a.id)) }).config;
      return names;
    },
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

const member = (state: Workspace['state']) => state as Extract<Workspace['state'], { phase: 'member' }>;
const invitations = (state: Workspace['state']) => (state as Extract<Workspace['state'], { phase: 'none' }>).invitations;
const ownOnly = () => parseConfig({ accounts: [{ id: 'mine', name: 'My own WhatsApp', channel: 'whatsapp', url: '', location: '', professional: false }] }).config;

test('inviting: the admin invites by address, the person sees which workspace, joins, and gets the setup', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  assert.deepEqual(await a.ws.invite('Staff@Example.com', 'member', null), {});
  assert.deepEqual(member(a.ws.state).invites.map((i) => i.email), ['staff@example.com']);
  assert.match((await a.ws.invite('staff@example.com', 'member', null)).error ?? '', /already invited/);
  assert.match((await a.ws.invite('not an address', 'member', null)).error ?? '', /email address/);

  // The invited person's own PC, with an account of their own that stays theirs.
  const s = pc(STAFF, ownOnly());
  await s.ws.check();
  assert.equal(s.ws.state.phase, 'none');
  const inv = invitations(s.ws.state);
  assert.deepEqual(inv.map((i) => [i.workspaceName, i.role]), [['Sample Business', 'member']]);
  assert.deepEqual(await s.ws.join(inv[0].id), {});
  assert.equal(member(s.ws.state).role, 'member');
  assert.deepEqual(s.config.accounts.map((x) => x.id), ['a1', 'a2', 'mine']);

  // The admin sees them as a member now, and the invitation is gone.
  await a.ws.check();
  assert.deepEqual(member(a.ws.state).people.map((p) => [p.email, p.role, p.status]).sort(), [['owner@example.com', 'admin', 'active'], ['staff@example.com', 'member', 'active']]);
  assert.deepEqual(member(a.ws.state).invites, []);
  // A member who is not an admin cannot invite.
  assert.match((await s.ws.invite('x@example.com', 'member', null)).error ?? '', /Only a workspace admin/);
});

test('removing: the removed PC wipes what it had from the workspace, keeps what was its own, and says so', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  await a.ws.invite('staff@example.com', 'member', null);
  const s = pc(STAFF, ownOnly());
  await s.ws.check();
  await s.ws.join(invitations(s.ws.state)[0].id);

  await a.ws.check();
  const uid = member(a.ws.state).people.find((p) => p.email === STAFF.email)!.uid;
  assert.match((await a.ws.setStatus(OWNER.uid, 'removed')).error ?? '', /cannot remove yourself/);
  assert.deepEqual(await a.ws.setStatus(uid, 'removed'), {});

  await s.ws.check();
  assert.equal(s.ws.state.phase, 'removed');
  assert.deepEqual([...s.wipedOnRemoval].sort(), ['a1', 'a2']);
  assert.deepEqual(s.config.accounts.map((x) => x.id), ['mine']);
  assert.deepEqual([...(s.ws.state as { wiped: string[] }).wiped].sort(), ['Main branch Instagram', 'Main branch WhatsApp']);
  // The notice survives a restart until it is read.
  const again = pc(STAFF, s.config, { dataDir: s.dataDir });
  assert.equal(again.ws.state.phase, 'removed');
  again.ws.acknowledgeRemoval();
  assert.equal(pc(STAFF, s.config, { dataDir: s.dataDir }).ws.state.phase, 'signed-out');

  // Restored by the admin: a member again at their next check.
  assert.deepEqual(await a.ws.setStatus(uid, 'active'), {});
  const back = pc(STAFF);
  await back.ws.check();
  assert.equal(back.ws.state.phase, 'member');
});

test('a PC that has not reached the workspace for a week asks to reconnect; a day offline carries on', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  const closed = 'http://127.0.0.1:9/v1/projects/demo-unified-messenger/databases/(default)';
  const dayLater = pc(OWNER, a.config, { dataDir: a.dataDir, base: closed, now: () => Date.now() + 24 * 3600_000 });
  await dayLater.ws.check();
  assert.equal(member(dayLater.ws.state).reconnect, false);
  assert.match(member(dayLater.ws.state).error ?? '', /could not be reached/);
  const weekLater = pc(OWNER, a.config, { dataDir: a.dataDir, base: closed, now: () => Date.now() + 8 * 24 * 3600_000 });
  await weekLater.ws.check();
  assert.equal(member(weekLater.ws.state).reconnect, true);
  // Reached again: the question goes away.
  const online = pc(OWNER, a.config, { dataDir: a.dataDir, now: () => Date.now() + 8 * 24 * 3600_000 });
  await online.ws.check();
  assert.equal(member(online.ws.state).reconnect, false);
});

test('an admin makes a member an admin, and withdraws an invitation', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  await a.ws.invite('staff@example.com', 'member', null);
  await a.ws.invite('later@example.com', 'admin', null);
  assert.deepEqual(await a.ws.withdraw('later@example.com'), {});
  assert.deepEqual(member(a.ws.state).invites.map((i) => i.email), ['staff@example.com']);
  const s = pc(STAFF);
  await s.ws.check();
  await s.ws.join(invitations(s.ws.state)[0].id);
  await a.ws.check();
  const uid = member(a.ws.state).people.find((p) => p.email === STAFF.email)!.uid;
  assert.deepEqual(await a.ws.setRole(uid, 'admin'), {});
  await s.ws.check();
  assert.equal(member(s.ws.state).role, 'admin');
  assert.equal(s.ws.readOnly, false);
});

test('the product owner sees every workspace, suspends one, and its PCs lock without losing anything', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  await a.ws.invite('staff@example.com', 'member', null);
  const s = pc(STAFF);
  await s.ws.check();
  await s.ws.join(invitations(s.ws.state)[0].id);
  const id = member(a.ws.state).id;

  // Not the product owner: no console, and the list of workspaces is refused.
  await a.ws.loadOwner();
  assert.deepEqual(a.ws.owner, { isOwner: false, workspaces: [] });

  // The product owner is an account with its own marker, made by hand in the console.
  await env.withSecurityRulesDisabled(async (ctx) => {
    await setDoc(doc(ctx.firestore() as unknown as Firestore, `owners/${PRODUCT_OWNER.uid}`), { note: 'product owner' });
  });
  const console_ = pc(PRODUCT_OWNER);
  await console_.ws.check();
  assert.equal(console_.ws.owner.isOwner, true);
  assert.deepEqual(console_.ws.owner.workspaces.map((w) => [w.name, w.members, w.status, w.admins]), [['Sample Business', 2, 'active', ['owner@example.com']]]);
  assert.ok(console_.ws.owner.workspaces[0].lastSeen > 0);

  // Suspended: both PCs see it at their next check, and neither loses anything.
  assert.deepEqual(await console_.ws.setWorkspaceStatus(id, 'suspended'), {});
  assert.equal(console_.ws.owner.workspaces[0].status, 'suspended');
  await s.ws.check();
  assert.equal(member(s.ws.state).status, 'suspended');
  assert.deepEqual(s.config.accounts.map((x) => x.id), ['a1', 'a2'], 'nothing wiped');
  // An admin cannot lift it, and cannot change the setup while it is suspended.
  await a.ws.check();
  assert.equal(member(a.ws.state).status, 'suspended');
  assert.equal(a.ws.readOnly, true);
  assert.match((await a.ws.invite('later@example.com', 'member', null)).error ?? '', /suspended/);

  // Restored: back to normal.
  assert.deepEqual(await console_.ws.setWorkspaceStatus(id, 'active'), {});
  await s.ws.check();
  assert.equal(member(s.ws.state).status, 'active');
  assert.deepEqual(s.config.accounts.map((x) => x.id), ['a1', 'a2']);
});

test('the product owner sees membership only, never a workspace’s setup', async () => {
  const a = pc(OWNER, startingConfig());
  await a.ws.create('Sample Business');
  await env.withSecurityRulesDisabled(async (ctx) => {
    await setDoc(doc(ctx.firestore() as unknown as Firestore, `owners/${PRODUCT_OWNER.uid}`), { note: 'product owner' });
  });
  const console_ = pc(PRODUCT_OWNER);
  await console_.ws.check();
  assert.equal(console_.ws.owner.isOwner, true);
  // Their own app has no workspace, and the setup of somebody else's never reaches this PC.
  assert.equal(console_.ws.state.phase, 'none');
  assert.deepEqual(console_.config.accounts, []);
});
