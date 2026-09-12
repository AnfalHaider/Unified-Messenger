// Settings. Only what the app actually does: every control here writes to config.json and changes behaviour
// on the next read. Assistant, notifications and workspace settings arrive with their phases — a switch that
// saves a preference nothing obeys is worse than no switch.
import type { UiState } from '../app/view-model.ts';
import { Icon } from './icons.tsx';

declare const window: Window & { um: Window['um'] };

const SECTIONS = ['General', 'Accounts', 'Appearance', 'Data & privacy', 'About'] as const;
export type SettingsSection = typeof SECTIONS[number];

export function Settings({ state, section, onSection, set }: {
  state: UiState;
  section: SettingsSection;
  onSection: (s: SettingsSection) => void;
  set: (patch: Record<string, unknown>) => void;
}) {
  return (
    <main className="board-surface">
      <div className="col" style={{ gap: 3 }}>
        <h1>Settings</h1>
        <span className="sub" style={{ fontSize: 12.5 }}>Everything here is kept on this PC, in config.json.</span>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '190px minmax(0,1fr)', gap: 26, flex: 1, minHeight: 0 }}>
        <nav className="col" style={{ gap: 1 }} aria-label="Settings sections">
          {SECTIONS.map((s) => (
            <button key={s} className="settings-tab" aria-current={s === section ? 'page' : undefined} onClick={() => onSection(s)}>{s}</button>
          ))}
        </nav>

        <div className="col" style={{ gap: 16, maxWidth: 760, overflow: 'auto' }}>
          {section === 'General' && <General state={state} set={set} />}
          {section === 'Accounts' && <Accounts state={state} />}
          {section === 'Appearance' && <Appearance state={state} />}
          {section === 'Data & privacy' && <Privacy />}
          {section === 'About' && <About />}
        </div>
      </div>
    </main>
  );
}

function Group({ title, note, children }: { title: string; note?: string; children: React.ReactNode }) {
  return (
    <section className="col" style={{ gap: 8 }}>
      <div className="row"><h3>{title}</h3>{note && <span className="sub spacer" style={{ fontSize: 12 }}>{note}</span>}</div>
      <div className="sheet" style={{ overflow: 'hidden' }}>{children}</div>
    </section>
  );
}

function Row({ title, detail, children }: { title: string; detail?: string; children: React.ReactNode }) {
  return (
    <label className="setting">
      <span className="col" style={{ gap: 2, flex: 1 }}>
        <span style={{ fontWeight: 600, fontSize: 12.5 }}>{title}</span>
        {detail && <span className="sub" style={{ fontSize: 12 }}>{detail}</span>}
      </span>
      {children}
    </label>
  );
}

function General({ state, set }: { state: UiState; set: (patch: Record<string, unknown>) => void }) {
  const s = state.settings;
  return (
    <>
      <Group title="Accounts">
        <Row title="Sleep accounts I'm not using"
          detail="Closes idle pages and keeps their logins. An account whose figures you watch is never slept, whatever this says.">
          <input type="checkbox" checked={s.sleepUnusedAccounts} onChange={(e) => set({ sleepUnusedAccounts: e.target.checked })} />
        </Row>
        <Row title="Sleep after" detail="Only used when sleeping is on">
          <select value={s.sleepAfterMinutes} disabled={!s.sleepUnusedAccounts} onChange={(e) => set({ sleepAfterMinutes: Number(e.target.value) })}>
            {[10, 20, 30, 60, 120].map((m) => <option key={m} value={m}>{m} minutes</option>)}
          </select>
        </Row>
        <Row title="Read every" detail="How often each account is read. Reading is scheduled by the app, not by the pages.">
          <select value={s.readEverySeconds} onChange={(e) => set({ readEverySeconds: Number(e.target.value) })}>
            {[30, 60, 120, 300].map((sec) => <option key={sec} value={sec}>{sec < 60 ? `${sec} seconds` : `${sec / 60} minute${sec === 60 ? '' : 's'}`}</option>)}
          </select>
        </Row>
      </Group>

      <Group title="Reply target">
        <Row title="Reply within" detail="Sets what counts as on time, and when a customer is about to be late.">
          <select value={s.slaMinutes} onChange={(e) => set({ slaMinutes: Number(e.target.value) })}>
            {[5, 10, 15, 20, 30, 45, 60, 90, 120].map((m) => <option key={m} value={m}>{m} minutes</option>)}
          </select>
        </Row>
        <Row title="Backlog after" detail="A customer waiting longer than this is counted as backlog and reported separately, never hidden.">
          <select value={s.backlogAfterDays} onChange={(e) => set({ backlogAfterDays: Number(e.target.value) })}>
            {[1, 3, 7, 14, 30].map((d) => <option key={d} value={d}>{d} day{d === 1 ? '' : 's'}</option>)}
          </select>
        </Row>
        <Row title="Leave out chats that ended the conversation"
          detail="A last message like &quot;ok, thanks&quot; is not someone waiting. Turn this off to see the raw number instead.">
          <input type="checkbox" checked={s.filterClosedConversations} onChange={(e) => set({ filterClosedConversations: e.target.checked })} />
        </Row>
      </Group>

      <p className="sub" style={{ fontSize: 12 }}>
        Opening hours are set per location and pause the reply clock outside them. They come across from v5 and
        are edited per location — that screen arrives with the workspace phase.
      </p>
    </>
  );
}

function Accounts({ state }: { state: UiState }) {
  return (
    <>
      <Group title="Channel readers" note="one per channel, not per account">
        {state.modules.length === 0
          ? <div className="setting"><span className="sub" style={{ fontSize: 12.5, flex: 1 }}>None of your accounts are on a channel that has a reader yet.</span></div>
          : state.modules.map((m) => (
            <div key={m.id} className="setting">
              <span className="col" style={{ gap: 2, flex: 1 }}>
                <span style={{ fontWeight: 600, fontSize: 12.5 }}>{m.name}</span>
                <span className="sub" style={{ fontSize: 12 }}>{m.detail}</span>
              </span>
              <span className={`chip ${m.tone}`}>{m.status}</span>
            </div>
          ))}
      </Group>
      <AccountList state={state} />
    </>
  );
}

function AccountList({ state }: { state: UiState }) {
  return (
    <Group title="Accounts and channels" note={`${state.accounts.length} in total`}>
      {state.accounts.map((a) => (
        <div key={a.id} className="setting">
          <span className="col" style={{ gap: 2, flex: 1 }}>
            <span style={{ fontWeight: 600, fontSize: 12.5 }}>{a.name}</span>
            <span className="sub" style={{ fontSize: 12 }}>{a.location || 'No location'} · {a.channel}</span>
          </span>
          {a.signedOut
            ? <span className="chip neutral"><Icon name="lock" size={12} />Sign in needed</span>
            : a.waiting === null
              ? <span className="chip neutral">No figures from this channel</span>
              : <span className="chip ok"><Icon name="check" size={12} />Reading</span>}
        </div>
      ))}
      <div className="setting">
        <span className="sub" style={{ fontSize: 12 }}>
          Adding, renaming and removing accounts arrives with the workspace phase. Until then they come from
          your v5 install, and nothing here can lose one.
        </span>
      </div>
    </Group>
  );
}

function Appearance({ state }: { state: UiState }) {
  return (
    <Group title="Theme" note="Also in the title bar">
      {(['light', 'dark', 'system'] as const).map((t) => (
        <label key={t} className="setting">
          <span className="col" style={{ gap: 2, flex: 1 }}>
            <span style={{ fontWeight: 600, fontSize: 12.5, textTransform: 'capitalize' }}>{t === 'system' ? 'Match Windows' : t}</span>
            {t === 'system' && <span className="sub" style={{ fontSize: 12 }}>Follows the Windows setting as it changes.</span>}
          </span>
          <input type="radio" name="theme" checked={state.theme === t} onChange={() => window.um.setTheme(t)} />
        </label>
      ))}
    </Group>
  );
}

function Privacy() {
  return (
    <>
      <Group title="What stays on this PC">
        <div className="setting">
          <span className="col" style={{ gap: 4, flex: 1 }}>
            <span className="sub" style={{ fontSize: 12.5 }}>Everything the app derives about your customers is written here and nowhere else:</span>
            <ul style={{ margin: 0, paddingLeft: 18, color: 'var(--ink-2)', fontSize: 12.5, lineHeight: 1.8 }}>
              <li>Messages, previews, customer names and numbers</li>
              <li>Waiting times, reply times and history</li>
              <li>The logins for WhatsApp, Instagram and Google</li>
            </ul>
          </span>
        </div>
      </Group>
      <Group title="What the app sends">
        <div className="setting">
          <span className="sub" style={{ fontSize: 12.5, flex: 1 }}>
            Nothing. There is no telemetry, no analytics and no crash reporting. The app reads the pages you are
            signed in to and never sends a message. Workspace sync, which carries settings but never customer
            data, arrives with the cloud phase.
          </span>
        </div>
      </Group>
      <Group title="The log support would ask for">
        <div className="setting">
          <span className="sub" style={{ fontSize: 12.5, flex: 1 }}>
            <code>app.log</code> holds counts and timings only — never a name, a number or message text — so it
            can be sent without sending your customers.
          </span>
        </div>
      </Group>
    </>
  );
}

function About() {
  return (
    <Group title="Unified Messenger 6">
      <div className="setting">
        <span className="col" style={{ gap: 2, flex: 1 }}>
          <span style={{ fontWeight: 600, fontSize: 12.5 }}>In development</span>
          <span className="sub" style={{ fontSize: 12 }}>
            The figures, the reading and the screens you can see are working. Reviews, the assistant, the
            workspace and automatic updates arrive with their phases.
          </span>
        </span>
      </div>
    </Group>
  );
}
