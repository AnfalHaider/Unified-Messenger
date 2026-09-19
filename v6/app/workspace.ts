// The workspace, carried out (roadmap 6.3): finding which workspace this signed-in person belongs to, starting one
// from this PC's setup, and keeping the shared setup in step. core/workspace-sync.ts decides what is shared and how
// it is applied; this file talks to Firestore over its REST API with the signed-in person's own token, so the
// Firestore rules (cloud/firestore.rules) decide every read and write, exactly as they would for any other client.
//
// No Electron here, so cloud/sync.spec.ts runs it against the emulator. Kept on this PC in workspace.json: which
// workspace, the role, which accounts came from it, and which version of the setup was last applied. The log gets
// events and counts, never a name or an email.
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fromFields, readSetup, setupKey, toFields, type FsValue, type SharedSetup } from '../core/workspace-sync.ts';

export type WorkspaceState =
  | { phase: 'signed-out' }
  | { phase: 'checking' }
  | { phase: 'none'; error?: string }
  | { phase: 'member'; id: string; name: string; role: 'admin' | 'member'; status: 'active' | 'suspended'; syncedAt: number; note?: string; error?: string }
  | { phase: 'removed'; id: string; name: string }
  | { phase: 'error'; error: string };

interface Kept { id: string; name: string; role: 'admin' | 'member'; synced: string[]; appliedKey: string; appliedUpdateTime: string; checkedInAt: number; syncedAt: number }

interface Doc { name: string; fields?: Record<string, FsValue>; updateTime?: string }

const PULL_EVERY_MS = 6 * 60 * 60_000;
const CHECK_IN_EVERY_MS = 24 * 60 * 60_000;

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
  changed: () => void;
  log: (entry: Record<string, unknown>) => void;
}

export class Workspace {
  state: WorkspaceState = { phase: 'signed-out' };
  private kept: Kept | null = null;
  private busy: Promise<void> = Promise.resolve();
  /** After a failed send, the next try waits a minute rather than every tick. */
  private retryAt = 0;
  private readonly file: string;
  private readonly h: WorkspaceHost;

  constructor(host: WorkspaceHost) {
    this.h = host;
    this.file = join(host.dataDir, 'workspace.json');
    try { if (existsSync(this.file)) this.kept = JSON.parse(readFileSync(this.file, 'utf8')) as Kept; } catch { this.kept = null; }
  }

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

  /** At startup, after signing in, and on request: which workspace, then its setup. */
  check(): Promise<void> {
    return this.serial(async () => {
      const user = this.h.user();
      if (!user) { this.set({ phase: 'signed-out' }); return; }
      if (this.state.phase !== 'member') this.set({ phase: 'checking' });
      try {
        const mine = await this.query('members', 'email', user.email);
        const entries = mine.map((d) => ({ id: d.name.split('/').slice(-3)[0], uid: d.name.split('/').pop()!, ...(fromFields(d.fields ?? {}) as { role?: string; status?: string }) }))
          .filter((m) => m.uid === user.uid);
        const entry = entries.find((m) => m.status === 'active') ?? entries[0];
        if (!entry) { this.forget(); this.set({ phase: 'none' }); this.h.log({ event: 'workspace-none' }); return; }
        const ws = await this.get(`workspaces/${entry.id}`);
        const w = fromFields(ws?.fields ?? {}) as { name?: string; status?: string };
        const name = w.name ?? '';
        if (entry.status !== 'active') { this.set({ phase: 'removed', id: entry.id, name }); this.h.log({ event: 'workspace-removed' }); return; }
        const role = entry.role === 'admin' ? 'admin' : 'member';
        const status = w.status === 'suspended' ? 'suspended' : 'active';
        if (!this.kept || this.kept.id !== entry.id) this.kept = { id: entry.id, name, role, synced: [], appliedKey: '', appliedUpdateTime: '', checkedInAt: 0, syncedAt: 0 };
        Object.assign(this.kept, { name, role });
        this.save();
        this.set({ phase: 'member', id: entry.id, name, role, status, syncedAt: this.kept.syncedAt });
        this.h.log({ event: 'workspace-found', role, status });
        if (status === 'active') { await this.pullNow(); await this.checkInIfDue(); }
      } catch (e) {
        this.h.log({ event: 'workspace-check-failed', error: String((e as Error).message).slice(0, 120) });
        if (this.state.phase === 'member') this.set({ ...this.state, error: 'The workspace could not be reached. The setup on this PC is used until it can.' });
        else this.set({ phase: 'error', error: 'The workspace could not be reached. Check the connection and try again.' });
      }
    });
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
        this.kept = { id, name: title, role: 'admin', synced: setup.accounts.map((a) => a.id), appliedKey: '', appliedUpdateTime: '', checkedInAt: Date.now(), syncedAt: 0 };
        this.save();
        this.h.log({ event: 'workspace-created', accounts: setup.accounts.length, locations: setup.locations.length });
      } catch (e) {
        this.h.log({ event: 'workspace-create-failed', error: String((e as Error).message).slice(0, 120) });
        return { error: 'The workspace could not be created. Check the connection and try again.' };
      }
      return {};
    }).then(async (r) => { if (!r.error) await this.check(); return r; });
  }

  /** Every six hours, or on request: the setup from the workspace, applied if it changed since the last one here. */
  pull(): Promise<void> { return this.serial(() => this.pullNow()); }

  /** Called on every tick: an admin's change to the shared setup goes up; the six-hourly pull comes down. */
  tick(now = Date.now()) {
    if (this.state.phase !== 'member' || this.state.status !== 'active' || !this.kept) return;
    if (now - this.kept.syncedAt > PULL_EVERY_MS) { void this.pull(); return; }
    if (this.state.role === 'admin' && this.kept.appliedKey && now >= this.retryAt && setupKey(this.h.local()) !== this.kept.appliedKey) void this.serial(() => this.pushNow());
  }

  /** Forgets the workspace on this PC: at sign-out. The workspace itself is untouched. */
  forget() {
    this.kept = null;
    rmSync(this.file, { force: true });
    this.set({ phase: 'signed-out' });
  }

  private async pullNow() {
    if (!this.kept) return;
    const doc = await this.get(`workspaces/${this.kept.id}/config/main`);
    this.kept.syncedAt = Date.now();
    if (doc && doc.updateTime !== this.kept.appliedUpdateTime) {
      const setup = readSetup(doc.fields);
      this.kept.synced = this.h.apply(setup);
      this.kept.appliedUpdateTime = doc.updateTime ?? '';
      this.h.log({ event: 'workspace-pulled', accounts: setup.accounts.length });
    }
    this.kept.appliedKey = setupKey(this.h.local());
    this.save();
    if (this.state.phase === 'member') this.set({ ...this.state, syncedAt: this.kept.syncedAt, error: undefined });
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
      this.kept.syncedAt = Date.now();
      this.save();
      this.h.log({ event: 'workspace-pushed', accounts: setup.accounts.length });
      if (this.state.phase === 'member') this.set({ ...this.state, syncedAt: this.kept.syncedAt, note: undefined, error: undefined });
    } catch (e) {
      if ((e as { status?: number }).status === 409 || /FAILED_PRECONDITION/.test(String((e as Error).message))) {
        this.h.log({ event: 'workspace-conflict' });
        await this.pullNow();
        if (this.state.phase === 'member') this.set({ ...this.state, note: 'Another PC changed the shared setup at the same time. This PC now has that version; make the change again if it is still needed.' });
      } else {
        this.h.log({ event: 'workspace-push-failed', error: String((e as Error).message).slice(0, 120) });
        // Not sent: the setup still differs from the last one applied, so a tick a minute from now tries again.
        this.retryAt = Date.now() + 60_000;
        if (this.state.phase === 'member') this.set({ ...this.state, error: 'The change could not be sent to the workspace. It is kept on this PC and sent when the connection is back.' });
      }
    }
  }

  /** Once a day: last seen, for the members list and the owner console. */
  private async checkInIfDue() {
    const user = this.h.user();
    if (!this.kept || !user || Date.now() - this.kept.checkedInAt < CHECK_IN_EVERY_MS) return;
    await this.commit([{
      update: { name: this.path(`workspaces/${this.kept.id}/members/${user.uid}`), fields: toFields({ name: user.name.slice(0, 80) }) },
      updateMask: { fieldPaths: ['name'] }, updateTransforms: [now('lastSeen')],
    }]);
    this.kept.checkedInAt = Date.now();
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
