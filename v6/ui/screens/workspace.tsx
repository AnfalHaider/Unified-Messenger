// Workspace and system states: the owner console, and the full-window screens the app shows only in particular
// moments — signing in, a new PC, a removed PC, a paused workspace, and the move from the previous version.
// The new-PC checklist reads the real accounts; the rest are sample until Phase 6 and Phase 7 wire them.
import { channelIcon, Icon } from '../icons.tsx';
import { Btn, Chip, Headline, Panel, plural, Sample, type LockScreen, type Nav, type ScreenProps } from '../parts.tsx';
import { WORKSPACES } from '../sample.ts';

export function OwnerScreen(_: ScreenProps) {
  const members = WORKSPACES.reduce((n, w) => n + w.members, 0);
  const pcs = WORKSPACES.reduce((n, w) => n + w.pcs, 0);
  return (
    <main className="main">
      <Headline sample eyebrow={<span className="phase"><Icon name="key" size={12} /> Owner only</span>} title="All workspaces">
        {WORKSPACES.length} workspaces, {members} members, {pcs} PCs. It reads membership and last-seen only; no workspace’s customer data exists in the cloud to show.
      </Headline>
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Workspace</th><th>Admin</th><th className="r">Members</th><th className="r">PCs</th><th>Last seen</th><th>Status</th><th /></tr></thead><tbody>
          {WORKSPACES.map((w) => (
            <tr key={w.name}>
              <td><b style={{ fontWeight: 600 }}>{w.name}</b></td><td className="sub">{w.admin}</td><td className="r">{w.members}</td><td className="r">{w.pcs}</td><td>{w.seen}</td>
              <td>{w.active ? <Chip tone="ok">Active</Chip> : <Chip tone="late">Suspended</Chip>}</td>
              <td className="r"><Btn kind={w.active ? undefined : 'primary'} disabled title="Not connected yet">{w.active ? 'Suspend' : 'Restore'}</Btn></td>
            </tr>
          ))}
        </tbody></table>
      </div>
      <Panel title="What suspending does"><p className="sub" style={{ margin: 0 }}>Every PC in the workspace locks at its next check, within a day, and shows who to contact. Logins are kept, so restoring brings it straight back. Enforced by the database rules, not by the app, so it cannot be skipped.</p></Panel>
    </main>
  );
}

const GOOGLE = (
  <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
    <path fill="#4285F4" d="M17.6 9.2c0-.6-.1-1.2-.2-1.7H9v3.3h4.8a4.1 4.1 0 0 1-1.8 2.7v2.2h2.9c1.7-1.6 2.7-3.9 2.7-6.5z" />
    <path fill="#34A853" d="M9 18c2.4 0 4.5-.8 5.9-2.2L12 13.6c-.8.5-1.8.9-3 .9-2.3 0-4.3-1.6-5-3.7H1v2.3A9 9 0 0 0 9 18z" />
    <path fill="#FBBC05" d="M4 10.8a5.4 5.4 0 0 1 0-3.5V5H1a9 9 0 0 0 0 8.1z" />
    <path fill="#EA4335" d="M9 3.6c1.3 0 2.5.5 3.4 1.3L15 2.4A9 9 0 0 0 1 5l3 2.3c.7-2.1 2.7-3.7 5-3.7z" />
  </svg>
);

/** A full-window state. Every one of them has a way back while the shell is being reviewed. */
export function Lock({ screen, state, nav }: ScreenProps & { screen: LockScreen }) {
  const back = <Btn kind="quiet" onClick={() => nav.lock(null)}>Back to the app</Btn>;
  if (screen === 'sign-in') return (
    <LockCard>
      <span className="mark" style={{ width: 44, height: 44, fontSize: 22, borderRadius: 12 }}>U</span>
      <h1>Sign in to see all your business’s messages in one place</h1>
      <p>Your Google account tells the app which workspace you belong to. Your WhatsApp, Instagram and Google logins are then made on this PC.</p>
      <button className="btn primary" style={{ height: 44, fontSize: 14.5, justifyContent: 'center', gap: 10 }} disabled title="Sign-in is not connected yet">{GOOGLE}Continue with Google</button>
      <span className="sub">Opens your browser. Unified Messenger asks Google for your name and email address, nothing else.</span>
      <div className="grid2" style={{ gap: 12, marginTop: 10 }}>
        <Panel title={<span style={{ display: 'flex', gap: 8, alignItems: 'center' }}><Icon name="cloud" size={16} />Kept in the workspace</span>}><p className="sub" style={{ margin: 0 }}>Who is a member, and the list of accounts, locations and settings, so a new PC is ready in minutes.</p></Panel>
        <Panel title={<span style={{ display: 'flex', gap: 8, alignItems: 'center' }}><Icon name="shield" size={16} />Never leaves this PC</span>}><p className="sub" style={{ margin: 0 }}>Messages, customer names, reply times, notes, assistant chats and the account logins themselves.</p></Panel>
      </div>
      <div><Sample /> {back}</div>
    </LockCard>
  );
  if (screen === 'new-pc') return <NewPc state={state} nav={nav} />;
  if (screen === 'removed') return (
    <LockCard>
      <span style={{ width: 52, height: 52, borderRadius: '50%', background: 'var(--hover)', display: 'grid', placeItems: 'center', boxShadow: 'inset 0 0 0 1px var(--line-2)' }}><Icon name="lock" size={24} /></span>
      <h1>This PC is no longer part of the workspace</h1>
      <p>An admin removed <b>desk.dha2@example.com</b> on 13 September at 10:41 am. The account logins saved on this PC, and the history the app kept here, were wiped at 10:44 am.</p>
      <Panel><dl className="kv"><dt>Logins wiped</dt><dd>DHA-2 WhatsApp, DHA-2 Instagram, DHA-2 Google</dd><dt>History wiped</dt><dd>Waiting times, notes and assistant chats on this PC</dd><dt>Still here</dt><dd>The app itself, signed out</dd></dl></Panel>
      <p className="sub">If this is a mistake, ask a workspace admin to invite this email again.</p>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}><Btn disabled title="Not connected yet">Sign in with a different account</Btn><Sample />{back}</div>
    </LockCard>
  );
  if (screen === 'suspended') return (
    <LockCard>
      <span style={{ width: 52, height: 52, borderRadius: '50%', background: 'var(--due-w)', color: 'var(--due)', display: 'grid', placeItems: 'center' }}><Icon name="alert" size={24} /></span>
      <h1>The Brightway Tutors workspace is paused</h1>
      <p>Unified Messenger was suspended for this workspace on 25 August. Nothing on this PC has been deleted: the logins and history are kept, and everything returns as it was once the workspace is restored.</p>
      <Panel><dl className="kv"><dt>Workspace</dt><dd>Trial: Brightway Tutors</dd><dt>Admin</dt><dd>owner@example.com</dd><dt>What to do</dt><dd>Ask the workspace admin to contact Unified Messenger</dd></dl></Panel>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}><Btn icon="refresh" kind="primary" disabled title="Not connected yet">Check again</Btn><Sample />{back}</div>
    </LockCard>
  );
  return (
    <LockCard wide>
      <span className="phase"><Icon name="download" size={12} /> Unified Messenger 6.0</span>
      <h1>Everything came across. Some logins need one more sign-in.</h1>
      <p>The new version keeps its logins separately from the old one, so WhatsApp may treat this PC as a new linked device. It is a one-time step, about 30 seconds per number, done from each phone.</p>
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}><table className="table"><tbody>
        <tr><td><b style={{ fontWeight: 600 }}>{plural(state.accounts.length, 'account')}</b><div className="sub">Names, locations, opening hours and targets</div></td><td className="r ok"><Icon name="check" size={13} stroke={2} /> Moved</td></tr>
        <tr><td><b style={{ fontWeight: 600 }}>Waiting and reply-time history</b><div className="sub">So reports are complete from day one</div></td><td className="r ok"><Icon name="check" size={13} stroke={2} /> Moved</td></tr>
        <tr><td><b style={{ fontWeight: 600 }}>{plural(state.accounts.filter((a) => !a.signedOut).length, 'login')} carried over</b><div className="sub">Reading already</div></td><td className="r ok"><Icon name="check" size={13} stroke={2} /> Moved</td></tr>
        <tr><td><b style={{ fontWeight: 600 }}>{plural(state.accounts.filter((a) => a.signedOut).length, 'login')} to sign in</b><div className="sub">{state.accounts.filter((a) => a.signedOut).map((a) => a.name).join(', ') || 'None'}</div></td><td className="r due">Sign in once</td></tr>
      </tbody></table></div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}><Btn kind="primary" onClick={() => { nav.lock(null); nav.go('accounts'); }}>Sign in the accounts</Btn>{back}</div>
    </LockCard>
  );
}

const LockCard = ({ children, wide }: { children: React.ReactNode; wide?: boolean }) => (
  <div className="lock lock-screen"><div className="lock-card" style={wide ? { width: 680 } : undefined}>{children}</div></div>
);

function NewPc({ state, nav }: { state: ScreenProps['state']; nav: Nav }) {
  const ready = state.accounts.filter((a) => !a.signedOut).length;
  const locations = [...new Set(state.accounts.map((a) => a.location || 'No location'))];
  return (
    <div className="lock-screen" style={{ padding: '32px 48px' }}>
      <main className="main" style={{ maxWidth: 1080, margin: '0 auto', overflow: 'visible' }}>
        <Headline title={`${ready} of ${state.accounts.length} accounts are ready`} actions={<Btn kind="quiet" onClick={() => nav.lock(null)}>Back to the app</Btn>}>
          Your workspace’s accounts and locations are already here. Each account still marked below needs signing in on this PC before it can be read.
        </Headline>
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0,1fr) auto', gap: 14, alignItems: 'center' }}>
          <div className="progress"><i style={{ width: `${state.accounts.length ? (ready / state.accounts.length) * 100 : 0}%` }} /></div>
          <span className="sub num">{ready} of {state.accounts.length}</span>
        </div>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          {locations.map((loc, i) => (
            <div key={loc}>
              <div className="loc-head" style={i === 0 ? { borderTop: 0 } : undefined}>{loc}</div>
              {state.accounts.filter((a) => (a.location || 'No location') === loc).map((a) => (
                <div key={a.id} className="signin">
                  <Icon name={channelIcon(a.channel)} size={18} />
                  <b>{a.name}</b>
                  <span style={{ color: a.signedOut ? 'var(--due)' : 'var(--ok)', fontWeight: 600 }}>{a.signedOut ? 'Needs signing in' : a.waiting === null ? 'Open, no figures' : 'Signed in, reading'}</span>
                  {a.signedOut ? <Btn kind="primary" onClick={() => { nav.lock(null); nav.go('dock', a.id); }}>Sign in</Btn> : <span />}
                </div>
              ))}
            </div>
          ))}
        </div>
        <p className="sub" style={{ margin: 0 }}>The app only reads who is waiting. It never sends a message, and nothing it reads leaves this PC.</p>
      </main>
    </div>
  );
}
