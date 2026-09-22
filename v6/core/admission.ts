// Who may use this copy of the app (owner's decision, 2026-09-22: **invitation only**).
//
// A fresh install reads nothing and opens no account page until the person at it has signed in with Google and
// the workspace has said they belong. That is what makes an invite list mean something: a stranger who finds the
// installer gets as far as the sign-in screen and no further.
//
// Three ways in that are not an invitation, and all are deliberate:
//   - **the product's own address**, baked into the build below. It opens the app's screens and nothing more:
//     power over workspaces comes from the `owners/{uid}` record, which the security rules enforce and a
//     client cannot fake. This is so the person who makes the app can never be shut out of it — offline, on a
//     new PC, or with the database unreachable.
//   - **the product owner**, whose `owners/{uid}` marker admits them anywhere. Without this the first person
//     ever to sign in would be locked out of their own app, because no workspace exists yet to invite them.
//   - **a build with no cloud config**, which cannot ask anyone. That is a developer's build and the tests'.
//
// Nothing here talks to the network: `app/workspace.ts` gathers the answers, this decides what they mean.

export type AdmissionPhase =
  /** The build cannot ask (no cloud config). Nothing to gate with, so nothing is gated. */
  | 'open'
  /** Nobody is signed in. The sign-in screen, and nothing else. */
  | 'signed-out'
  /** Signed in; the workspace has not answered yet. */
  | 'checking'
  /** In. `why` is for the log, so a refusal that should not have happened can be traced. */
  | 'admitted'
  /** Signed in, holds an invitation, has not joined it yet. The Join screen does the rest. */
  | 'invited'
  /** Signed in, and nothing admits them. */
  | 'refused'
  /** Signed in, but the workspace could not be reached and this PC has never been admitted. */
  | 'unreachable';

export type AdmittedBecause = 'no-cloud-in-this-build' | 'product-owner' | 'product-owner-address' | 'workspace-member' | 'admitted-before';

/** The address the app belongs to. Admits only to the app's own screens; see the note at the top. */
export const PRODUCT_OWNER_EMAIL = 'anfalhaider@gmail.com';

export interface AdmissionInput {
  /** False in a build with no cloud config: there is nobody to ask. */
  cloudAvailable: boolean;
  signedIn: boolean;
  /** Who is signed in, so an admission remembered on this PC is matched to them and not to the next person. */
  uid: string | null;
  /** The `owners/{uid}` marker: the product owner is never locked out. */
  isOwner: boolean;
  /** The signed-in address, matched against the product's own. */
  email: string | null;
  /** What the workspace check has made of this account so far. */
  workspace: 'checking' | 'member' | 'removed' | 'none' | 'unreachable';
  /** Invitations waiting for this address. */
  invitations: number;
  /** The uid this PC was admitted for before, so a week without the network does not lock a member out. */
  admittedBefore: string | null;
}

export interface Admission { phase: AdmissionPhase; because: AdmittedBecause | null }

/**
 * The verdict. Order matters: the product owner first, so no rule below can shut them out; then membership;
 * then an invitation to accept; and only then the refusals.
 *
 * `removed` is not refused here. That PC has its own full-window screen saying what was wiped and by whom,
 * which is a better answer than "ask for an invitation", and it already stands in front of everything.
 */
export function admission(i: AdmissionInput): Admission {
  if (!i.cloudAvailable) return { phase: 'open', because: 'no-cloud-in-this-build' };
  if (!i.signedIn) return { phase: 'signed-out', because: null };
  if (i.isOwner) return { phase: 'admitted', because: 'product-owner' };
  if (i.email && i.email.trim().toLowerCase() === PRODUCT_OWNER_EMAIL) return { phase: 'admitted', because: 'product-owner-address' };
  if (i.workspace === 'member' || i.workspace === 'removed') return { phase: 'admitted', because: 'workspace-member' };
  // Admitted here before: a member whose workspace cannot be reached today still opens the app, exactly as one
  // whose workspace is simply slow does. Losing membership is not decided by a failed request; `removed` is.
  if (i.workspace === 'unreachable') {
    return i.uid && i.admittedBefore === i.uid
      ? { phase: 'admitted', because: 'admitted-before' }
      : { phase: 'unreachable', because: null };
  }
  if (i.workspace === 'checking') return { phase: 'checking', because: null };
  if (i.invitations > 0) return { phase: 'invited', because: null };
  return { phase: 'refused', because: null };
}

/** Whether the app may open account pages, read, count and announce. Everything else waits. */
export const mayRun = (a: Admission): boolean => a.phase === 'open' || a.phase === 'admitted';

/** What the person is told while they cannot get in. One sentence, in their words, never a status code. */
export function admissionSentence(a: Admission, email: string | null): string {
  switch (a.phase) {
    case 'signed-out': return 'Sign in with Google to use Unified Messenger on this PC.';
    case 'checking': return 'Checking whether this account has been invited…';
    case 'invited': return 'You have been invited. Join the workspace to start.';
    case 'refused': return `${email ?? 'This Google account'} has not been invited to a workspace. Ask an admin to invite this address, then check again.`;
    case 'unreachable': return 'Unified Messenger could not be reached to check this account. It will keep trying.';
    default: return '';
  }
}
