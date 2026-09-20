// The workspace, carried out (roadmap 6.3, 6.4): finding which workspace this signed-in person belongs to, starting one
// from this PC's setup, keeping the shared setup in step, inviting and removing members, and what a PC does when its
// person was removed or has not reached the workspace for a week. core/workspace-sync.ts decides what is shared and
// how it is applied; this file talks to Firestore over its REST API with the signed-in person's own token, so the
// Firestore rules (cloud/firestore.rules) decide every read and write, exactly as they would for any other client.
//
// No Electron here, so cloud/sync.spec.ts runs it against the emulator. Kept on this PC: workspace.json (which
// workspace, the role, which accounts came from it, which version of the setup was last applied, when it last reached
// the workspace) and, after a removal, removal.json (what was wiped, for the screen that says so). The log gets events
// and counts, never a name or an email.
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fromFields, readSetup, setupKey, toFields, type FsValue, type SharedSetup } from '../core/workspace-sync.ts';

export type Role = 'admin' | 'member';
/** A member as the members list shows them. `lastSeen` is when their PC last checked in, in milliseconds. */
export interface Person { uid: string; email: string; name: string; role: Role; status: 'active' | 'removed'; lastSeen: number }
/** An invitation not yet taken up, as admins see it. */
export interface Invite { email: string; role: Role; invitedAt: number }
/** One workspace as the product owner's console lists it: membership and last seen only, never a business's setup. */
export interface OwnerWorkspace { id: string; name: string; status: 'active' | 'suspended'; members: number; admins: string[]; lastSeen: number; statusChangedAt: number }
/** What the product owner's console shows. Everyone else sees isOwner false and no list. */
export interface OwnerView { isOwner: boolean; workspaces: OwnerWorkspace[]; error?: string }

/** An invitation to this signed-in person, from a workspace they have not joined. */
export interface Invitation { id: string; workspaceName: string; role: Role }

export type WorkspaceState =
  | { phase: 'signed-out' }
  | { phase: 'checking' }
  | { phase: 'none'; invitations: Invitation[]; error?: string }
  | { phase: 'member'; id: string; name: string; role: Role; status: 'active' | 'suspended'; syncedAt: number; lastContactAt: number;
      /** Seven days without reaching the workspace: the app asks to reconnect before it shows anything. */
      reconnect: boolean; people: Person[]; invites: Invite[]; note?: string; error?: string }
  | { phase: 'removed'; name: string; at: number; wiped: string[] }
  | { phase: 'error'; error: string };

interface Kept { id: string; name: string; role: Role; synced: string[]; appliedKey: string; appliedUpdateTime: string; checkedInAt: number; syncedAt: number; lastContactAt: number }
interface Removal { name: string; at: number; wiped: string[] }
interface Doc { name: string; fields?: Record<string, FsValue>; updateTime?: string }

const PULL_EVERY_MS = 6 * 60 * 60_000;
const CHECK_IN_EVERY_MS = 24 * 60 * 60_000;
export const RECONNECT_AFTER_MS = 7 * 24 * 60 * 60_000;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export interface WorkspaceHost {
  /** Firestore's REST root for the project: https://firestore.googleapis.com/v1/projects/<id>/databases/(default). */
  base: string;
  dataDir: string;
  token: () => Promise<string | null>;
  user: () => { uid: string; email: string; name: string } | null;
  /** The shared part of this PC's setup, now. */
  local: () => SharedSetup;
  /** Applies a setup from the workspace to this PC; returns the ids of the accounts now held from the workspace. */
  apply: (setup: SharedSetup) => string[];
  /** This person was removed: forget these accounts here, login and all, and sign out. Returns the names wiped. */
  removed: (accountIds: string[]) => Promise<string[]>;
  changed: () => void;
  log: (entry: Record<string, unknown>) => void;
  now?: () => number;
}

export class Workspace {
  state: WorkspaceState = { phase: 'signed-out' };
  /** The product owner's console (6.5). Read from owners/{uid}: nobody else can list workspaces. */
  owner: OwnerView = { isOwner: false, workspaces: [] };
  private kept: Kept | null = null;
  private busy: Promise<void> = Promise.resolve();
  /** After a failed send, the next try waits a minute rather than every tick. */
  private retryAt = 0;
  private readonly file: string;
  private readonly removalFile: string;
  private readonly h: WorkspaceHost;

  constructor(host: WorkspaceHost) {
    this.h = host;
    this.file = join(host.dataDir, 'workspace.json');
    this.removalFile = join(host.dataDir, 'removal.json');
    try { if (existsSync(this.file)) this.kept = JSON.parse(readFileSync(this.file, 'utf8')) as Kept; } catch { this.kept = null; }
    const removal = this.removal();
    if (removal) this.state = { phase: 'removed', ...removal };
  }

  private get clock() { return this.h.now?.() ?? Date.now(); }

  /** Accounts this PC holds from the workspace, so a pull can tell "removed on another PC" from "only ever here". */
  get synced(): ReadonlySet<string> { return new Set(this.kept?.synced ?? []); }

  /** Whether the shared setup may be changed on this PC: not by a member who is not an admin. */
  get readOnly(): boolean { return this.state.phase === 'member' && (this.state.role !== 'admin' || this.state.status !== 'active'); }

  /** One call at a time: a check, a pull and a push must never interleave. */
  private serial<T>(fn: () => Promise<T>): Promise<T> {
    const run = this.busy.then(fn, fn);
    this.busy = run.then(() => undefined, () => undefined);
    return run;
  }

  /** At startup, after signing in, and on request: which workspace, then its setup and its people. */
  check(): Promise<void> { return this.serial(() => this.checkNow()); }

  private async checkNow(): Promise<void> {
    const user = this.h.user();
    if (!user) { if (this.state.phase !== 'removed') this.set({ phase: 'signed-out' }); return; }
    if (this.state.phase !== 'member') this.set({ phase: 'checking' });
    try {
      const mine = await this.query('members', 'email', user.email);
      const entries = mine.map((d) => ({ id: d.name.split('/').slice(-3)[0], uid: d.name.split('/').pop()!, ...(fromFields(d.fields ?? {}) as { role?: string; status?: string }) }))
        .filter((m) => m.uid === user.uid);
      const entry = entries.find((m) => m.status === 'active') ?? entries[0];
      if (!entry) {
        const invitations = (await this.query('invites', 'email', user.email)).map((d) => {
          const f = fromFields(d.fields ?? {}) as { workspaceName?: string; role?: string };
          return { id: d.name.split('/').slice(-3)[0], workspaceName: f.workspaceName ?? '', role: (f.role === 'admin' ? 'admin' : 'member') as Role };
        });
        this.dropKept();
        this.set({ phase: 'none', invitations });
        this.h.log({ event: 'workspace-none', invitations: invitations.length });
        await this.loadOwner();
        return;
      }
      const ws = await this.get(`workspaces/${entry.id}`);
      const w = fromFields(ws?.fields ?? {}) as { name?: string; status?: string };
      const name = w.name ?? '';
      if (entry.status !== 'active') { await this.removedHere(name); return; }
      const role: Role = entry.role === 'admin' ? 'admin' : 'member';
      const status = w.status === 'suspended' ? 'suspended' : 'active';
      if (!this.kept || this.kept.id !== entry.id) this.kept = { id: entry.id, name, role, synced: [], appliedKey: '', appliedUpdateTime: '', checkedInAt: 0, syncedAt: 0, lastContactAt: 0 };
      Object.assign(this.kept, { name, role, lastContactAt: this.clock });
      this.save();
      this.set({ phase: 'member', id: entry.id, name, role, status, syncedAt: this.kept.syncedAt, lastContactAt: this.kept.lastContactAt, reconnect: false, people: [], invites: [] });
      this.h.log({ event: 'workspace-found', role, status });
      if (status === 'active') { await this.pullNow(); await this.checkInIfDue(); }
      await this.loadPeople();
      await this.loadOwner();
    } catch (e) {
      this.h.log({ event: 'workspace-check-failed', error: String((e as Error).message).slice(0, 120) });
      this.unreachable();
    }
  }

  /** The workspace could not be reached. With a workspace kept here, carry on with its setup, until a week has passed
   *  without reaching it: then ask to reconnect, so a removed person cannot keep reading by staying offline. */
  private unreachable() {
    if (!this.kept) { this.set({ phase: 'error', error: 'The workspace could not be reached. Check the connection and try again.' }); return; }
    const k = this.kept;
    const reconnect = !!k.lastContactAt && this.clock - k.lastContactAt > RECONNECT_AFTER_MS;
    const prior = this.state.phase === 'member' ? this.state : null;
    this.set({
      phase: 'member', id: k.id, name: k.name, role: k.role, status: prior?.status ?? 'active', syncedAt: k.syncedAt, lastContactAt: k.lastContactAt,
      reconnect, people: prior?.people ?? [], invites: prior?.invites ?? [],
      error: 'The workspace could not be reached. The setup on this PC is used until it can.',
    });
  }

  /** Removed by an admin: the accounts this PC had from the workspace are forgotten, login and all; what was only ever
   *  on this PC stays. Then the person is signed out, and a screen says what happened until they close it. */
  private async removedHere(name: string) {
    const ids = [...(this.kept?.synced ?? [])];
    const wiped = await this.h.removed(ids);
    const removal: Removal = { name, at: this.clock, wiped };
    writeFileSync(this.removalFile, JSON.stringify(removal, null, 2));
    this.dropKept();
    this.set({ phase: 'removed', ...removal });
    this.h.log({ event: 'workspace-removed', wiped: wiped.length });
  }

  /** The removal screen was read: back to a signed-out app. */
  acknowledgeRemoval() {
    rmSync(this.removalFile, { force: true });
    this.set({ phase: 'signed-out' });
  }

  private removal(): Removal | null {
    try { return existsSync(this.removalFile) ? JSON.parse(readFileSync(this.removalFile, 'utf8')) as Removal : null; } catch { return null; }
  }

  /** Starts a workspace from this PC's setup, with the signed-in person as its first admin. */
  create(name: string): Promise<{ error?: string }> {
    return this.serial(async () => {
      const user = this.h.user();
      const title = String(name ?? '').trim().slice(0, 80);
      if (!user) return { error: 'Sign in first.' };
      if (!title) return { error: 'Give the workspace a name: the business’s name is usual.' };
      const id = crypto.randomUUID().replace(/-/g, '').slice(0, 20);
      try {
        // The rules want the workspace and its first admin in one write, and the setup after it, once the admin exists.
        await this.commit([
          { update: { name: this.path(`workspaces/${id}`), fields: toFields({ name: title, status: 'active', createdBy: user.uid }) },
            currentDocument: { exists: false }, updateTransforms: [now('createdAt')] },
          { update: { name: this.path(`workspaces/${id}/members/${user.uid}`), fields: toFields({ email: user.email, name: user.name.slice(0, 80), role: 'admin', status: 'active' }) },
            currentDocument: { exists: false }, updateTransforms: [now('joinedAt'), now('lastSeen')] },
        ]);
        const setup = this.h.local();
        await this.commit([{ update: { name: this.path(`workspaces/${id}/config/main`), fields: toFields({ ...setup, updatedBy: user.uid }) }, updateTransforms: [now('updatedAt')] }]);
        this.kept = { id, name: title, role: 'admin', synced: setup.accounts.map((a) => a.id), appliedKey: '', appliedUpdateTime: '', checkedInAt: this.clock, syncedAt: 0, lastContactAt: this.clock };
        this.save();
        this.h.log({ event: 'workspace-created', accounts: setup.accounts.length, locations: setup.locations.length });
      } catch (e) {
        this.h.log({ event: 'workspace-create-failed', error: String((e as Error).message).slice(0, 120) });
        return { error: 'The workspace could not be created. Check the connection and try again.' };
      }
      await this.checkNow();
      return {};
    });
  }

  /** Takes up an invitation: this person becomes a member with the role it gave, and the setup arrives. Accounts that
   *  were only on this PC stay. */
  join(workspaceId: string): Promise<{ error?: string }> {
    return this.serial(async () => {
      const user = this.h.user();
      if (!user || this.state.phase !== 'none') return { error: 'Sign in first.' };
      const invitation = this.state.invitations.find((i) => i.id === workspaceId);
      if (!invitation) return { error: 'That invitation is no longer there. Ask the admin to invite you again.' };
      try {
        await this.commit([{
          update: { name: this.path(`workspaces/${workspaceId}/members/${user.uid}`), fields: toFields({ email: user.email, name: user.name.slice(0, 80), role: invitation.role, status: 'active' }) },
          currentDocument: { exists: false }, updateTransforms: [now('joinedAt'), now('lastSeen')],
        }]);
        // The invitation is spent; clearing it is tidiness, so a failure here does not undo the join.
        await this.commit([{ delete: this.path(`workspaces/${workspaceId}/invites/${user.email}`) }]).catch(() => undefined);
        this.h.log({ event: 'workspace-joined', role: invitation.role });
      } catch (e) {
        this.h.log({ event: 'workspace-join-failed', error: String((e as Error).message).slice(0, 120) });
        return { error: 'Joining did not work. The invitation may have been withdrawn, or the workspace suspended. Ask the admin.' };
      }
      await this.checkNow();
      return {};
    });
  }

  /** An admin invites someone by their Google address. The app sends nothing: the admin tells them to sign in. */
  invite(email: string, role: Role): Promise<{ error?: string }> {
    return this.admin(async (k) => {
      const address = String(email ?? '').trim().toLowerCase();
      if (!EMAIL.test(address)) return { error: 'That does not look like an email address.' };
      if (this.state.phase === 'member' && this.state.people.some((p) => p.email === address && p.status === 'active')) return { error: 'They are already a member.' };
      const removedPerson = this.state.phase === 'member' ? this.state.people.find((p) => p.email === address && p.status === 'removed') : undefined;
      if (removedPerson) return { error: 'They were removed from this workspace. Use Restore beside their name instead.' };
      await this.commit([{
        update: { name: this.path(`workspaces/${k.id}/invites/${address}`), fields: toFields({ email: address, role: role === 'admin' ? 'admin' : 'member', invitedBy: this.h.user()!.uid, workspaceName: k.name }) },
        currentDocument: { exists: false }, updateTransforms: [now('invitedAt')],
      }]).catch((e) => { throw /409|ALREADY_EXISTS/.test(String(e.message)) ? new Error('already invited') : e; });
      this.h.log({ event: 'member-invited', role });
      return {};
    }, (e) => /already invited/.test(e) ? 'They are already invited.' : 'The invitation could not be saved. Check the connection and try again.');
  }

  withdraw(email: string): Promise<{ error?: string }> {
    return this.admin(async (k) => {
      await this.commit([{ delete: this.path(`workspaces/${k.id}/invites/${String(email).toLowerCase()}`) }]);
      this.h.log({ event: 'invite-withdrawn' });
      return {};
    });
  }

  /** Removal is a status, not a delete: their PC reads it at its next check and wipes what it had from the workspace. */
  setStatus(uid: string, status: 'active' | 'removed'): Promise<{ error?: string }> {
    return this.admin(async (k) => {
      if (uid === this.h.user()?.uid) return { error: 'You cannot remove yourself. Another admin can.' };
      await this.commit([{ update: { name: this.path(`workspaces/${k.id}/members/${uid}`), fields: toFields({ status }) }, updateMask: { fieldPaths: ['status'] } }]);
      this.h.log({ event: status === 'removed' ? 'member-removed' : 'member-restored' });
      return {};
    });
  }

  setRole(uid: string, role: Role): Promise<{ error?: string }> {
    return this.admin(async (k) => {
      if (uid === this.h.user()?.uid) return { error: 'Another admin changes your role.' };
      await this.commit([{ update: { name: this.path(`workspaces/${k.id}/members/${uid}`), fields: toFields({ role }) }, updateMask: { fieldPaths: ['role'] } }]);
      this.h.log({ event: 'member-role-changed', role });
      return {};
    });
  }

  /** Runs an admin's change and then reloads the members list. Anything refused comes back as a sentence. */
  private admin(fn: (k: Kept) => Promise<{ error?: string }>, words = (_: string) => 'That did not work. Check the connection and try again.'): Promise<{ error?: string }> {
    return this.serial(async () => {
      if (!this.kept || this.state.phase !== 'member' || this.state.role !== 'admin') return { error: 'Only a workspace admin can do that.' };
      if (this.state.status !== 'active') return { error: 'The workspace is suspended, so its members cannot be changed.' };
      let result: { error?: string };
      try { result = await fn(this.kept); } catch (e) {
        this.h.log({ event: 'member-change-failed', error: String((e as Error).message).slice(0, 120) });
        result = { error: words(String((e as Error).message)) };
      }
      await this.loadPeople().catch(() => undefined);
      return result;
    });
  }

  /** The members, and for admins the invitations waiting. Everyone in the workspace may see who else is in it. */
  private async loadPeople() {
    if (!this.kept || this.state.phase !== 'member') return;
    const people = (await this.list(`workspaces/${this.kept.id}/members`)).map((d): Person => {
      const f = fromFields(d.fields ?? {}) as Partial<Person>;
      return { uid: d.name.split('/').pop()!, email: f.email ?? '', name: f.name ?? '', role: f.role === 'admin' ? 'admin' : 'member', status: f.status === 'removed' ? 'removed' : 'active', lastSeen: Number(f.lastSeen) || 0 };
    });
    const invites = this.state.role === 'admin'
      ? (await this.list(`workspaces/${this.kept.id}/invites`)).map((d): Invite => {
        const f = fromFields(d.fields ?? {}) as { email?: string; role?: string; invitedAt?: number };
        return { email: f.email ?? '', role: f.role === 'admin' ? 'admin' : 'member', invitedAt: Number(f.invitedAt) || 0 };
      })
      : [];
    if (this.state.phase === 'member') this.set({ ...this.state, people, invites });
  }

  /** The product owner's own marker, then every workspace: how many members, who the admins are, when any of their
   *  PCs was last seen, and whether it is suspended. Never a workspace's setup, which the rules refuse the owner. */
  async loadOwner(): Promise<void> {
    const user = this.h.user();
    if (!user) { this.owner = { isOwner: false, workspaces: [] }; return; }
    try {
      const marker = await this.get(`owners/${user.uid}`);
      if (!marker) { this.owner = { isOwner: false, workspaces: [] }; this.h.changed(); return; }
      const workspaces: OwnerWorkspace[] = [];
      for (const d of await this.list('workspaces')) {
        const f = fromFields(d.fields ?? {}) as { name?: string; status?: string; statusChangedAt?: number };
        const id = d.name.split('/').pop()!;
        const people = (await this.list(`workspaces/${id}/members`)).map((m) => fromFields(m.fields ?? {}) as { email?: string; role?: string; status?: string; lastSeen?: number });
        const live = people.filter((p) => p.status !== 'removed');
        workspaces.push({
          id, name: f.name ?? '', status: f.status === 'suspended' ? 'suspended' : 'active', statusChangedAt: Number(f.statusChangedAt) || 0,
          members: live.length, admins: live.filter((p) => p.role === 'admin').map((p) => p.email ?? ''),
          lastSeen: Math.max(0, ...live.map((p) => Number(p.lastSeen) || 0)),
        });
      }
      workspaces.sort((a, b) => a.name.localeCompare(b.name));
      this.owner = { isOwner: true, workspaces };
      this.h.log({ event: 'owner-console-read', workspaces: workspaces.length });
    } catch (e) {
      this.h.log({ event: 'owner-console-failed', error: String((e as Error).message).slice(0, 120) });
      this.owner = { isOwner: this.owner.isOwner, workspaces: this.owner.workspaces, error: 'The workspaces could not be read. Check the connection and try again.' };
    }
    this.h.changed();
  }

  /** The product owner suspends a workspace or restores it. Every PC in it locks at its next check, wiping nothing. */
  setWorkspaceStatus(id: string, status: 'active' | 'suspended'): Promise<{ error?: string }> {
    return this.serial(async () => {
      if (!this.owner.isOwner) return { error: 'Only the product owner can do that.' };
      try {
        await this.commit([{
          update: { name: this.path(`workspaces/${id}`), fields: toFields({ status }) },
          updateMask: { fieldPaths: ['status', 'statusChangedAt'] }, updateTransforms: [now('statusChangedAt')],
        }]);
        this.h.log({ event: status === 'suspended' ? 'workspace-suspended' : 'workspace-restored' });
      } catch (e) {
        this.h.log({ event: 'workspace-status-failed', error: String((e as Error).message).slice(0, 120) });
        return { error: 'That did not work. Check the connection and try again.' };
      }
      await this.loadOwner();
      // This PC may be in the workspace it just changed.
      if (this.kept?.id === id) await this.checkNow();
      return {};
    });
  }

  /** Every six hours, or on request: the setup from the workspace, applied if it changed since the last one here. */
  pull(): Promise<void> { return this.serial(() => this.pullNow()); }

  /** Called on every tick: an admin's change to the shared setup goes up; the six-hourly check comes down. */
  tick(now = this.clock) {
    if (this.state.phase !== 'member' || !this.kept) return;
    if (now - this.kept.syncedAt > PULL_EVERY_MS && now >= this.retryAt) { this.retryAt = now + 60_000; void this.check(); return; }
    if (this.state.status !== 'active') return;
    if (this.state.role === 'admin' && this.kept.appliedKey && now >= this.retryAt && setupKey(this.h.local()) !== this.kept.appliedKey) void this.serial(() => this.pushNow());
  }

  /** Forgets the workspace on this PC: at sign-out. The workspace itself is untouched. A removal notice stays. */
  forget() {
    this.dropKept();
    if (this.state.phase !== 'removed') this.set({ phase: 'signed-out' });
  }

  private dropKept() {
    this.kept = null;
    rmSync(this.file, { force: true });
  }

  private async pullNow() {
    if (!this.kept) return;
    try {
      const doc = await this.get(`workspaces/${this.kept.id}/config/main`);
      this.kept.syncedAt = this.clock;
      this.kept.lastContactAt = this.clock;
      if (doc && doc.updateTime !== this.kept.appliedUpdateTime) {
        const setup = readSetup(doc.fields);
        this.kept.synced = this.h.apply(setup);
        this.kept.appliedUpdateTime = doc.updateTime ?? '';
        this.h.log({ event: 'workspace-pulled', accounts: setup.accounts.length });
      }
      this.kept.appliedKey = setupKey(this.h.local());
      this.save();
      if (this.state.phase === 'member') this.set({ ...this.state, syncedAt: this.kept.syncedAt, lastContactAt: this.kept.lastContactAt, reconnect: false, error: undefined });
    } catch (e) {
      this.h.log({ event: 'workspace-pull-failed', error: String((e as Error).message).slice(0, 120) });
      this.unreachable();
    }
  }

  /** Sends this PC's shared setup, only if nobody changed the workspace's copy since this PC last applied it. When
   *  someone did, the workspace's version wins here and the owner is told, rather than one PC overwriting another. */
  private async pushNow() {
    const user = this.h.user();
    if (!this.kept || !user || this.state.phase !== 'member' || this.state.role !== 'admin') return;
    const setup = this.h.local();
    const key = setupKey(setup);
    if (key === this.kept.appliedKey) return;
    try {
      const res = await this.commit([{
        update: { name: this.path(`workspaces/${this.kept.id}/config/main`), fields: toFields({ ...setup, updatedBy: user.uid }) },
        ...(this.kept.appliedUpdateTime ? { currentDocument: { updateTime: this.kept.appliedUpdateTime } } : {}),
        updateTransforms: [now('updatedAt')],
      }]);
      this.kept.appliedUpdateTime = res.writeResults?.[0]?.updateTime ?? this.kept.appliedUpdateTime;
      this.kept.appliedKey = key;
      this.kept.synced = setup.accounts.map((a) => a.id);
      this.kept.syncedAt = this.clock;
      this.kept.lastContactAt = this.clock;
      this.save();
      this.h.log({ event: 'workspace-pushed', accounts: setup.accounts.length });
      if (this.state.phase === 'member') this.set({ ...this.state, syncedAt: this.kept.syncedAt, lastContactAt: this.kept.lastContactAt, note: undefined, error: undefined });
    } catch (e) {
      if ((e as { status?: number }).status === 409 || /FAILED_PRECONDITION/.test(String((e as Error).message))) {
        this.h.log({ event: 'workspace-conflict' });
        await this.pullNow();
        if (this.state.phase === 'member') this.set({ ...this.state, note: 'Another PC changed the shared setup at the same time. This PC now has that version; make the change again if it is still needed.' });
      } else {
        this.h.log({ event: 'workspace-push-failed', error: String((e as Error).message).slice(0, 120) });
        // Not sent: the setup still differs from the last one applied, so a tick a minute from now tries again.
        this.retryAt = this.clock + 60_000;
        if (this.state.phase === 'member') this.set({ ...this.state, error: 'The change could not be sent to the workspace. It is kept on this PC and sent when the connection is back.' });
      }
    }
  }

  /** Once a day: last seen, for the members list and the owner console. */
  private async checkInIfDue() {
    const user = this.h.user();
    if (!this.kept || !user || this.clock - this.kept.checkedInAt < CHECK_IN_EVERY_MS) return;
    await this.commit([{
      update: { name: this.path(`workspaces/${this.kept.id}/members/${user.uid}`), fields: toFields({ name: user.name.slice(0, 80) }) },
      updateMask: { fieldPaths: ['name'] }, updateTransforms: [now('lastSeen')],
    }]);
    this.kept.checkedInAt = this.clock;
    this.save();
  }

  // ---- REST -------------------------------------------------------------------------------------------------------

  private path(rel: string) { return `${this.h.base.replace(/^https?:\/\/[^/]+\/v1\//, '')}/documents/${rel}`; }

  private async call(url: string, init: RequestInit = {}): Promise<Response> {
    const token = await this.h.token();
    if (!token) throw new Error('not signed in');
    return fetch(url, { ...init, headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...(init.headers ?? {}) } });
  }

  private async get(rel: string): Promise<Doc | null> {
    const res = await this.call(`${this.h.base}/documents/${rel}`);
    if (res.status === 404) return null;
    if (!res.ok) throw Object.assign(new Error(`get ${res.status}`), { status: res.status });
    return (await res.json()) as Doc;
  }

  /** Every document in one collection (a workspace has tens of members at most, so one page is enough). */
  private async list(rel: string): Promise<Doc[]> {
    const res = await this.call(`${this.h.base}/documents/${rel}?pageSize=300`);
    if (!res.ok) throw Object.assign(new Error(`list ${res.status}`), { status: res.status });
    return ((await res.json()) as { documents?: Doc[] }).documents ?? [];
  }

  private async query(collectionId: string, field: string, value: string): Promise<Doc[]> {
    const res = await this.call(`${this.h.base}/documents:runQuery`, { method: 'POST', body: JSON.stringify({
      structuredQuery: { from: [{ collectionId, allDescendants: true }], where: { fieldFilter: { field: { fieldPath: field }, op: 'EQUAL', value: { stringValue: value } } } },
    }) });
    if (!res.ok) throw Object.assign(new Error(`query ${res.status}`), { status: res.status });
    return ((await res.json()) as { document?: Doc }[]).map((r) => r.document).filter((d): d is Doc => !!d);
  }

  private async commit(writes: unknown[]): Promise<{ writeResults?: { updateTime?: string }[] }> {
    const res = await this.call(`${this.h.base}/documents:commit`, { method: 'POST', body: JSON.stringify({ writes }) });
    const body = (await res.json().catch(() => ({}))) as { error?: { status?: string }; writeResults?: { updateTime?: string }[] };
    if (!res.ok) throw Object.assign(new Error(`commit ${res.status} ${body.error?.status ?? ''}`), { status: res.status });
    return body;
  }

  private save() { if (this.kept) writeFileSync(this.file, JSON.stringify(this.kept, null, 2)); }

  private set(state: WorkspaceState) { this.state = state; this.h.changed(); }
}

const now = (fieldPath: string) => ({ fieldPath, setToServerValue: 'REQUEST_TIME' });
