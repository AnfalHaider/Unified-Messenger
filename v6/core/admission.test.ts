// The gate decides who may use the app at all, so every case here is one somebody could be standing in.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { admission, admissionSentence, mayRun, type AdmissionInput } from './admission.ts';

const input = (o: Partial<AdmissionInput> = {}): AdmissionInput => ({
  cloudAvailable: true, signedIn: true, uid: 'uid-1', isOwner: false,
  workspace: 'none', invitations: 0, admittedBefore: null, ...o,
});

test('a stranger who installs the app and signs in gets no further', () => {
  const a = admission(input());
  assert.equal(a.phase, 'refused');
  assert.equal(mayRun(a), false, 'nothing loads for them');
  assert.match(admissionSentence(a, 'someone@example.com'), /someone@example\.com has not been invited/);
});

test('nobody signed in means the sign-in screen and nothing else', () => {
  const a = admission(input({ signedIn: false, uid: null }));
  assert.equal(a.phase, 'signed-out');
  assert.equal(mayRun(a), false);
});

test('the product owner is never locked out, whatever else is true', () => {
  // The first person ever to sign in has no workspace to be a member of, and no invitation: without this rule
  // they could not open their own app.
  for (const o of [{}, { workspace: 'none' as const }, { workspace: 'unreachable' as const }, { invitations: 0 }]) {
    const a = admission(input({ isOwner: true, ...o }));
    assert.equal(a.phase, 'admitted', JSON.stringify(o));
    assert.equal(a.because, 'product-owner');
    assert.equal(mayRun(a), true);
  }
});

test('a member of a workspace is in', () => {
  const a = admission(input({ workspace: 'member' }));
  assert.deepEqual(a, { phase: 'admitted', because: 'workspace-member' });
});

test('an invitation not yet accepted sends them to Join, not to the door', () => {
  const a = admission(input({ invitations: 1 }));
  assert.equal(a.phase, 'invited');
  assert.equal(mayRun(a), false, 'joining comes first');
  assert.match(admissionSentence(a, 'them@example.com'), /Join the workspace/);
});

test('a member whose workspace cannot be reached today still opens the app', () => {
  // Losing membership is a thing the workspace says, not a thing a failed request implies.
  const reachedBefore = admission(input({ workspace: 'unreachable', admittedBefore: 'uid-1' }));
  assert.deepEqual(reachedBefore, { phase: 'admitted', because: 'admitted-before' });
  // But the admission is that person's, not the PC's: a different account gets nothing from it.
  const someoneElse = admission(input({ workspace: 'unreachable', admittedBefore: 'uid-1', uid: 'uid-2' }));
  assert.equal(someoneElse.phase, 'unreachable');
  assert.equal(mayRun(someoneElse), false);
  // A first launch with no network and nothing remembered waits rather than letting anyone in.
  assert.equal(admission(input({ workspace: 'unreachable' })).phase, 'unreachable');
});

test('a removed PC is not refused here: its own screen says what happened', () => {
  const a = admission(input({ workspace: 'removed' }));
  assert.equal(a.phase, 'admitted', 'the removed screen stands in front of everything and says more');
});

test('while the workspace is still answering, the gate waits rather than refusing', () => {
  const a = admission(input({ workspace: 'checking' }));
  assert.equal(a.phase, 'checking');
  assert.equal(mayRun(a), false);
});

test('a build with no cloud config cannot ask anyone, so it gates nobody', () => {
  const a = admission(input({ cloudAvailable: false, signedIn: false, uid: null }));
  assert.deepEqual(a, { phase: 'open', because: 'no-cloud-in-this-build' });
  assert.equal(mayRun(a), true);
});
