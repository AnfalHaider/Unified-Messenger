// The Firestore rules, tested against the emulator (roadmap 6.2). Run with `npm run rules:test`, which starts the
// emulator, runs this file and stops it. Named .spec, not .test, so `npm test` (no emulator) does not pick it up.
//
// Every person here is invented. Each test starts from an empty database and seeds what it needs with the rules off.
import { after, before, beforeEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { assertFails, assertSucceeds, initializeTestEnvironment, type RulesTestEnvironment } from '@firebase/rules-unit-testing';
import {
  collection, collectionGroup, deleteDoc, doc, getDoc, getDocs, query, serverTimestamp, setDoc, updateDoc, where, writeBatch, type Firestore,
} from 'firebase/firestore';

let env: RulesTestEnvironment;

before(async () => {
  env = await initializeTestEnvironment({
    projectId: 'demo-unified-messenger',
    firestore: { rules: readFileSync(new URL('./firestore.rules', import.meta.url), 'utf8'), host: '127.0.0.1', port: 8181 },
  });
});
after(async () => { await env?.cleanup(); });
beforeEach(async () => { await env.clearFirestore(); });

/** A signed-in person, as Google sign-in gives them to Firebase: a verified address. */
const as = (uid: string, email: string, verified = true): Firestore =>
  env.authenticatedContext(uid, { email, email_verified: verified }).firestore() as unknown as Firestore;
const OWNER = () => as('owner-uid', 'product.owner@example.com');
const ADMIN = () => as('admin-uid', 'admin@example.com');
const STAFF = () => as('staff-uid', 'staff@example.com');
const STRANGER = () => as('stranger-uid', 'stranger@example.com');

/** Seeds with the rules off: a workspace with an admin and a member, the product owner, and the setup. */
async function seed(status: 'active' | 'suspended' = 'active') {
  await env.withSecurityRulesDisabled(async (ctx) => {
    const db = ctx.firestore() as unknown as Firestore;
    await setDoc(doc(db, 'owners/owner-uid'), { note: 'product owner' });
    await setDoc(doc(db, 'workspaces/w1'), { name: 'Sample Business', status, createdBy: 'admin-uid', createdAt: new Date() });
    await setDoc(doc(db, 'workspaces/w1/members/admin-uid'), { email: 'admin@example.com', name: 'Sample Admin', role: 'admin', status: 'active', joinedAt: new Date(), lastSeen: new Date() });
    await setDoc(doc(db, 'workspaces/w1/members/staff-uid'), { email: 'staff@example.com', name: 'Sample Staff', role: 'member', status: 'active', joinedAt: new Date(), lastSeen: new Date() });
    await setDoc(doc(db, 'workspaces/w1/config/main'), { accounts: [{ id: 'a1', name: 'Main branch WhatsApp' }], locations: [{ name: 'Main branch' }], settings: { slaMinutes: 15 }, updatedAt: new Date(), updatedBy: 'admin-uid' });
  });
}
const config = (updatedBy: string) => ({ accounts: [], locations: [], settings: {}, updatedAt: serverTimestamp(), updatedBy });

test('nobody signed out reads or writes anything', async () => {
  await seed();
  const anon = env.unauthenticatedContext().firestore() as unknown as Firestore;
  await assertFails(getDoc(doc(anon, 'workspaces/w1')));
  await assertFails(getDoc(doc(anon, 'workspaces/w1/config/main')));
  await assertFails(setDoc(doc(anon, 'workspaces/w2'), { name: 'x', status: 'active', createdBy: 'x', createdAt: serverTimestamp() }));
});

test('starting a workspace: only as its first admin, in the same write as the member entry', async () => {
  const db = as('new-uid', 'new@example.com');
  const b = writeBatch(db);
  b.set(doc(db, 'workspaces/w2'), { name: 'New Business', status: 'active', createdBy: 'new-uid', createdAt: serverTimestamp() });
  b.set(doc(db, 'workspaces/w2/members/new-uid'), { email: 'new@example.com', name: 'New Owner', role: 'admin', status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() });
  await assertSucceeds(b.commit());

  // Alone, without the member entry; suspended from the start; or in someone else's name: refused.
  await assertFails(setDoc(doc(db, 'workspaces/w3'), { name: 'Alone', status: 'active', createdBy: 'new-uid', createdAt: serverTimestamp() }));
  const b2 = writeBatch(db);
  b2.set(doc(db, 'workspaces/w4'), { name: 'Other', status: 'active', createdBy: 'someone-else', createdAt: serverTimestamp() });
  b2.set(doc(db, 'workspaces/w4/members/new-uid'), { email: 'new@example.com', name: 'x', role: 'admin', status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() });
  await assertFails(b2.commit());
  // An address Google has not verified cannot start one.
  const unverified = as('u2', 'u2@example.com', false);
  const b3 = writeBatch(unverified);
  b3.set(doc(unverified, 'workspaces/w5'), { name: 'x', status: 'active', createdBy: 'u2', createdAt: serverTimestamp() });
  b3.set(doc(unverified, 'workspaces/w5/members/u2'), { email: 'u2@example.com', name: 'x', role: 'admin', status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() });
  await assertFails(b3.commit());
});

test('nobody joins an existing workspace as admin without an invitation', async () => {
  await seed();
  await assertFails(setDoc(doc(STRANGER(), 'workspaces/w1/members/stranger-uid'), { email: 'stranger@example.com', name: 'x', role: 'admin', status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() }));
  await assertFails(setDoc(doc(STRANGER(), 'workspaces/w1/members/stranger-uid'), { email: 'stranger@example.com', name: 'x', role: 'member', status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() }));
});

test('members read their workspace, its members and its setup; strangers read none of it', async () => {
  await seed();
  for (const db of [ADMIN(), STAFF()]) {
    await assertSucceeds(getDoc(doc(db, 'workspaces/w1')));
    await assertSucceeds(getDoc(doc(db, 'workspaces/w1/members/admin-uid')));
    await assertSucceeds(getDoc(doc(db, 'workspaces/w1/config/main')));
  }
  await assertFails(getDoc(doc(STRANGER(), 'workspaces/w1')));
  await assertFails(getDoc(doc(STRANGER(), 'workspaces/w1/members/admin-uid')));
  await assertFails(getDoc(doc(STRANGER(), 'workspaces/w1/config/main')));
});

test('only an admin changes the setup, and only its named fields', async () => {
  await seed();
  await assertSucceeds(setDoc(doc(ADMIN(), 'workspaces/w1/config/main'), config('admin-uid')));
  await assertFails(setDoc(doc(STAFF(), 'workspaces/w1/config/main'), config('staff-uid')));
  // Anything beyond accounts, locations and settings (customer data, figures) is refused.
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/config/main'), { ...config('admin-uid'), customers: [{ name: 'Sample Customer A' }] }));
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/config/main'), { ...config('admin-uid'), waiting: 12 }));
  // Signed as someone else, or another document in config: refused.
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/config/main'), config('staff-uid')));
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/config/other'), config('admin-uid')));
});

test('inviting: an admin invites by email; the person sees it, joins with that role, and clears it', async () => {
  await seed();
  const invite = (role: string) => ({ email: 'new.staff@example.com', role, invitedBy: 'admin-uid', invitedAt: serverTimestamp(), workspaceName: 'Sample Business' });
  await assertFails(setDoc(doc(STAFF(), 'workspaces/w1/invites/new.staff@example.com'), { ...invite('member'), invitedBy: 'staff-uid' }));
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/invites/New.Staff@example.com'), { ...invite('member'), email: 'New.Staff@example.com' })); // keyed in lower case
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/invites/new.staff@example.com'), { ...invite('member'), workspaceName: 'Another Business' })); // the name must be the workspace's own
  await assertSucceeds(setDoc(doc(ADMIN(), 'workspaces/w1/invites/new.staff@example.com'), invite('member')));

  // Signed in with a capitalised address, as Google may give it: still theirs.
  const invitee = as('new-uid', 'New.Staff@example.com');
  const mine = await assertSucceeds(getDocs(query(collectionGroup(invitee, 'invites'), where('email', '==', 'new.staff@example.com'))));
  assert.equal(mine.size, 1);
  await assertFails(getDocs(query(collectionGroup(STRANGER(), 'invites'), where('email', '==', 'new.staff@example.com'))));

  const member = (role: string) => ({ email: 'new.staff@example.com', name: 'New Staff', role, status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() });
  await assertFails(setDoc(doc(invitee, 'workspaces/w1/members/new-uid'), member('admin'))); // not a role the invitation gave
  await assertFails(setDoc(doc(invitee, 'workspaces/w1/members/other-uid'), member('member'))); // not under someone else’s id
  await assertSucceeds(setDoc(doc(invitee, 'workspaces/w1/members/new-uid'), member('member')));
  await assertSucceeds(getDoc(doc(invitee, 'workspaces/w1/config/main')));
  await assertSucceeds(deleteDoc(doc(invitee, 'workspaces/w1/invites/new.staff@example.com')));
});

test('an invitation cannot be used by someone else, or with an address Google has not verified', async () => {
  await seed();
  await assertSucceeds(setDoc(doc(ADMIN(), 'workspaces/w1/invites/new.staff@example.com'), { email: 'new.staff@example.com', role: 'member', invitedBy: 'admin-uid', invitedAt: serverTimestamp(), workspaceName: 'Sample Business' }));
  const member = { email: 'new.staff@example.com', name: 'x', role: 'member', status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() };
  await assertFails(setDoc(doc(STRANGER(), 'workspaces/w1/members/stranger-uid'), member));
  await assertFails(setDoc(doc(as('u9', 'new.staff@example.com', false), 'workspaces/w1/members/u9'), member));
});

test('a member checks in (last seen, name) and changes nothing else about themselves', async () => {
  await seed();
  await assertSucceeds(updateDoc(doc(STAFF(), 'workspaces/w1/members/staff-uid'), { lastSeen: serverTimestamp(), name: 'Sample Staff' }));
  // With a valid check-in beside it, so the role is the only thing that can be refused.
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1/members/staff-uid'), { lastSeen: serverTimestamp(), role: 'admin' })); // no promoting oneself
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1/members/staff-uid'), { lastSeen: new Date(2020, 0, 1) })); // last seen is the server’s time
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1/members/admin-uid'), { lastSeen: serverTimestamp() })); // not someone else’s
});

test('removing: an admin removes a member, who can then read only that they were removed', async () => {
  await seed();
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1/members/admin-uid'), { status: 'removed' })); // members cannot remove
  await assertFails(updateDoc(doc(ADMIN(), 'workspaces/w1/members/admin-uid'), { status: 'removed' })); // an admin cannot remove themselves
  await assertFails(deleteDoc(doc(ADMIN(), 'workspaces/w1/members/staff-uid'))); // removal is a status, never a delete
  await assertSucceeds(updateDoc(doc(ADMIN(), 'workspaces/w1/members/staff-uid'), { status: 'removed' }));

  const removed = STAFF();
  const self = await assertSucceeds(getDoc(doc(removed, 'workspaces/w1/members/staff-uid')));
  assert.equal(self.data()?.status, 'removed');
  await assertSucceeds(getDoc(doc(removed, 'workspaces/w1')));
  await assertFails(getDoc(doc(removed, 'workspaces/w1/config/main')));
  await assertFails(getDoc(doc(removed, 'workspaces/w1/members/admin-uid')));
  await assertFails(updateDoc(doc(removed, 'workspaces/w1/members/staff-uid'), { lastSeen: serverTimestamp() }));
  await assertFails(updateDoc(doc(removed, 'workspaces/w1/members/staff-uid'), { status: 'active' })); // no putting oneself back
});

test('suspending: only the product owner suspends or restores, and a suspended workspace keeps its setup but shows it to nobody', async () => {
  await seed();
  await assertFails(updateDoc(doc(ADMIN(), 'workspaces/w1'), { status: 'suspended', statusChangedAt: serverTimestamp() })); // an admin cannot suspend or restore
  await assertSucceeds(updateDoc(doc(OWNER(), 'workspaces/w1'), { status: 'suspended', statusChangedAt: serverTimestamp() }));

  // Members still see the workspace, so their PC can say it is suspended; the setup is closed to everyone.
  const w = await assertSucceeds(getDoc(doc(STAFF(), 'workspaces/w1')));
  assert.equal(w.data()?.status, 'suspended');
  await assertFails(getDoc(doc(STAFF(), 'workspaces/w1/config/main')));
  await assertFails(getDoc(doc(ADMIN(), 'workspaces/w1/config/main')));
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/config/main'), config('admin-uid')));
  await assertFails(updateDoc(doc(ADMIN(), 'workspaces/w1/members/staff-uid'), { status: 'removed' }));
  await assertFails(setDoc(doc(ADMIN(), 'workspaces/w1/invites/x@example.com'), { email: 'x@example.com', role: 'member', invitedBy: 'admin-uid', invitedAt: serverTimestamp(), workspaceName: 'Sample Business' }));
  await assertFails(updateDoc(doc(ADMIN(), 'workspaces/w1'), { name: 'Renamed' }));
  await assertFails(updateDoc(doc(ADMIN(), 'workspaces/w1'), { status: 'active', statusChangedAt: serverTimestamp() })); // the admin cannot lift it

  await assertSucceeds(updateDoc(doc(OWNER(), 'workspaces/w1'), { status: 'active', statusChangedAt: serverTimestamp() }));
  await assertSucceeds(getDoc(doc(STAFF(), 'workspaces/w1/config/main')));
});

test('the product owner sees workspaces and members, never a business’s setup, and changes nothing else', async () => {
  await seed();
  const owner = OWNER();
  await assertSucceeds(getDoc(doc(owner, 'owners/owner-uid')));
  await assertSucceeds(getDoc(doc(owner, 'workspaces/w1')));
  await assertSucceeds(getDoc(doc(owner, 'workspaces/w1/members/staff-uid')));
  await assertFails(getDoc(doc(owner, 'workspaces/w1/config/main')));
  await assertFails(updateDoc(doc(owner, 'workspaces/w1'), { name: 'Renamed' }));
  await assertFails(updateDoc(doc(owner, 'workspaces/w1/members/staff-uid'), { status: 'removed' }));
  // Nobody makes themselves the product owner, or reads who is.
  await assertFails(setDoc(doc(STRANGER(), 'owners/stranger-uid'), { note: 'me' }));
  await assertFails(getDoc(doc(STRANGER(), 'owners/owner-uid')));
});

test('an admin renames an active workspace; a member cannot, and nobody deletes one', async () => {
  await seed();
  await assertSucceeds(updateDoc(doc(ADMIN(), 'workspaces/w1'), { name: 'Sample Business Ltd' }));
  await assertFails(updateDoc(doc(ADMIN(), 'workspaces/w1'), { name: '' }));
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1'), { name: 'Mine now' }));
  await assertFails(deleteDoc(doc(ADMIN(), 'workspaces/w1')));
  await assertFails(deleteDoc(doc(OWNER(), 'workspaces/w1')));
});

test('the app finds its own membership by email, and nobody else’s', async () => {
  await seed();
  const mine = await assertSucceeds(getDocs(query(collectionGroup(STAFF(), 'members'), where('email', '==', 'staff@example.com'))));
  assert.equal(mine.size, 1);
  // Someone else's address, or every member: refused.
  await assertFails(getDocs(query(collectionGroup(STRANGER(), 'members'), where('email', '==', 'staff@example.com'))));
  await assertFails(getDocs(collectionGroup(STAFF(), 'members')));
  // Nobody can put my address on an entry under their own id, so what I find is mine.
  await assertFails(setDoc(doc(STRANGER(), 'workspaces/w1/members/stranger-uid'), { email: 'staff@example.com', name: 'x', role: 'member', status: 'active', joinedAt: serverTimestamp(), lastSeen: serverTimestamp() }));
});

test('access to accounts is an admin’s to give: a member cannot widen their own, and a joiner takes what the invitation named', async () => {
  await seed();
  await env.withSecurityRulesDisabled(async (ctx) => {
    const db = ctx.firestore() as unknown as Firestore;
    // The member was given one account of the two the workspace has.
    await updateDoc(doc(db, 'workspaces/w1/members/staff-uid'), { accounts: ['a1'] });
    // And someone invited, to one account only.
    await setDoc(doc(db, 'workspaces/w1/invites/new@example.com'), {
      email: 'new@example.com', role: 'member', invitedBy: 'admin-uid', invitedAt: new Date(), workspaceName: 'Sample Business', accounts: ['a1'],
    });
  });

  // The member may still check in, which is the write they are allowed, and it leaves their access alone.
  await assertSucceeds(updateDoc(doc(STAFF(), 'workspaces/w1/members/staff-uid'), { lastSeen: serverTimestamp(), name: 'Sample Staff' }));
  // What they must not do: give themselves the rest of the business, on its own or alongside a check-in.
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1/members/staff-uid'), { accounts: ['a1', 'a2'] }));
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1/members/staff-uid'), { lastSeen: serverTimestamp(), accounts: ['a1', 'a2'] }));
  // Nor may another member hand it to them.
  await assertFails(updateDoc(doc(STAFF(), 'workspaces/w1/members/admin-uid'), { accounts: ['a1', 'a2'] }));
  // An admin may, and that is the whole point of the feature.
  await assertSucceeds(updateDoc(doc(ADMIN(), 'workspaces/w1/members/staff-uid'), { accounts: ['a1', 'a2'] }));

  // Joining: the accounts come from the invitation, not from the person joining.
  const newcomer = as('new-uid', 'new@example.com');
  const joinWith = (accounts: string[]) => setDoc(doc(newcomer, 'workspaces/w1/members/new-uid'), {
    email: 'new@example.com', name: 'A Newcomer', role: 'member', status: 'active',
    joinedAt: serverTimestamp(), lastSeen: serverTimestamp(), accounts,
  });
  await assertFails(joinWith(['a1', 'a2']));
  await assertSucceeds(joinWith(['a1']));
});

test('an invitation may carry a line from the admin, bounded, and nothing else new', async () => {
  await seed();
  const invite = (fields: Record<string, unknown>) => setDoc(doc(ADMIN(), 'workspaces/w1/invites/new@example.com'), {
    email: 'new@example.com', role: 'member', invitedBy: 'admin-uid', invitedAt: serverTimestamp(), workspaceName: 'Sample Business', ...fields,
  });
  await assertSucceeds(invite({ note: 'Welcome — you are on the DHA-2 desk.' }));
  await deleteDoc(doc(ADMIN(), 'workspaces/w1/invites/new@example.com'));
  // With the accounts it names, which is what the live rules refused until they were deployed.
  await assertSucceeds(invite({ accounts: ['a1'], note: 'DHA-2 only.' }));
  await deleteDoc(doc(ADMIN(), 'workspaces/w1/invites/new@example.com'));
  // An invitation with neither is still fine: both are optional.
  await assertSucceeds(invite({}));
  await deleteDoc(doc(ADMIN(), 'workspaces/w1/invites/new@example.com'));
  // A note is one line from an admin, not a letter, and not a place to smuggle another shape.
  await assertFails(invite({ note: 'x'.repeat(301) }));
  await assertFails(invite({ note: 42 }));
  await assertFails(invite({ note: 'fine', somethingElse: 'no' }));
});

test('marks: a member carries only the ones for accounts they may see, and nothing else may be stored on one', async () => {
  await seed();
  await env.withSecurityRulesDisabled(async (ctx) => {
    const db = ctx.firestore() as unknown as Firestore;
    // The member may see one account of the two; the admin sees everything.
    await updateDoc(doc(db, 'workspaces/w1/members/staff-uid'), { accounts: ['a1'] });
    await setDoc(doc(db, 'workspaces/w1/marks/a1__mine'), { accountId: 'a1', key: 'mine', kind: 'handled', at: 1, activity: 0 });
    await setDoc(doc(db, 'workspaces/w1/marks/a2__theirs'), { accountId: 'a2', key: 'theirs', kind: 'excluded', at: 1 });
  });

  // Reading: their own account yes, the other no.
  await assertSucceeds(getDoc(doc(STAFF(), 'workspaces/w1/marks/a1__mine')));
  await assertFails(getDoc(doc(STAFF(), 'workspaces/w1/marks/a2__theirs')));
  // The admin sees both, because their accounts are not limited.
  await assertSucceeds(getDoc(doc(ADMIN(), 'workspaces/w1/marks/a2__theirs')));

  // Listing: everything fails on the mark they may not see; asking for their own account succeeds.
  await assertFails(getDocs(query(collection(STAFF(), 'workspaces/w1/marks'))));
  await assertSucceeds(getDocs(query(collection(STAFF(), 'workspaces/w1/marks'), where('accountId', '==', 'a1'))));

  // Writing: their own account only, and only the shape a mark has.
  const mark = (db: Firestore, id: string, fields: Record<string, unknown>) => setDoc(doc(db, `workspaces/w1/marks/${id}`), fields);
  await assertSucceeds(mark(STAFF(), 'a1__new', { accountId: 'a1', key: 'new', kind: 'snoozed', at: 2, until: 99 }));
  await assertFails(mark(STAFF(), 'a2__new', { accountId: 'a2', key: 'new', kind: 'snoozed', at: 2, until: 99 }));
  // The id is not what decides it: a mark claiming an account they may not see is refused whatever it is called.
  await assertFails(mark(STAFF(), 'a1__sneaky', { accountId: 'a2', key: 'sneaky', kind: 'excluded', at: 2 }));
  // Nothing about the customer beyond the conversation itself.
  await assertFails(mark(STAFF(), 'a1__named', { accountId: 'a1', key: 'k', kind: 'handled', at: 2, customer: 'Ayesha Khan' }));
  await assertFails(mark(STAFF(), 'a1__texted', { accountId: 'a1', key: 'k', kind: 'handled', at: 2, preview: 'see you Friday' }));
  await assertFails(mark(STAFF(), 'a1__odd', { accountId: 'a1', key: 'k', kind: 'something-else', at: 2 }));

  // A put-back is a mark like any other, and tidying one away is allowed for an account they may see.
  await assertSucceeds(mark(STAFF(), 'a1__gone', { accountId: 'a1', key: 'gone', kind: 'cleared', at: 3 }));
  await assertSucceeds(deleteDoc(doc(STAFF(), 'workspaces/w1/marks/a1__gone')));
  await assertFails(deleteDoc(doc(STAFF(), 'workspaces/w1/marks/a2__theirs')));

  // A stranger gets nothing, and a suspended workspace shows its marks to nobody.
  await assertFails(getDoc(doc(STRANGER(), 'workspaces/w1/marks/a1__mine')));
});
