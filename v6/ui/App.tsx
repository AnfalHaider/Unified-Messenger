// The Command Center. It draws what the main process sends and decides nothing: every number here was
// computed in core/ and shaped in app/view-model.ts, which is what keeps one answer in one place.
//
// Only screens that work are shown. Reviews, Analytics and Reports arrive with their phases rather than
// sitting in the rail as dead items that imply they do something.
import { useEffect, useState } from 'react';
import type { QueueRow, Tone, UiState } from '../app/view-model.ts';
import { channelIcon, Icon } from './icons.tsx';

declare global {
  interface Window {
    um: {
      onState(fn: (state: UiState) => void): void;
      ready(): void;
      show(accountId: string | null): void;
      readNow(): void;
      setTheme(theme: 'system' | 'light' | 'dark'): void;
      windowAction(action: 'minimise' | 'maximise' | 'close'): void;
    };
  }
}

const greeting = () => {
  const hour = new Date().getHours();
  return hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';
};

/** Opened in a plain browser for design work there is no main process to ask, so the screens fall back to
 *  sample data and say so. In the app this is always the real bridge. */
const bridge: Window['um'] = typeof window !== 'undefined' && window.um ? window.um : {
  onState() {}, ready() {}, show() {}, readNow() {}, setTheme() {}, windowAction() {},
};
const isPreview = !(typeof window !== 'undefined' && window.um);

export function App() {
  const [state, setState] = useState<UiState | null>(null);

  useEffect(() => {
    if (!isPreview) { bridge.onState(setState); bridge.ready(); return; }
    void import('./preview-state.ts').then((m) => setState(m.PREVIEW_STATE));
  }, []);

  useEffect(() => {
    if (!state) return;
    const dark = state.theme === 'dark'
      || (state.theme === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.dataset.theme = dark ? 'dark' : 'light';
  }, [state?.theme]);

  if (!state) {
    return (
      <div className="app">
        <TitleBar />
        <div className="body"><div className="rail" /><main className="board-surface"><p className="sub">Starting…</p></main></div>
      </div>
    );
  }

  return (
    <div className="app">
      <TitleBar state={state} />
      <div className="body">
        <Rail state={state} />
        {/* When an account is on screen its own page covers this area, so the dashboard is simply not drawn. */}
        {state.visible === null ? <Dashboard state={state} /> : <main className="board-surface" />}
      </div>
    </div>
  );
}

function TitleBar({ state }: { state?: UiState }) {
  const theme = state?.theme ?? 'system';
  return (
    <header className="titlebar">
      <div className="mark">U</div>
      <span className="tb-name">Unified Messenger</span>
      {state && <span className="sub no-drag" style={{ fontSize: 12, color: state.freshness.isStale ? 'var(--due)' : 'var(--shell-ink-2)' }}>{state.freshness.text}</span>}
      <div className="tb-right">
        <div className="theme-switch no-drag" role="group" aria-label="Theme">
          <button aria-pressed={theme === 'light'} aria-label="Light" onClick={() => bridge.setTheme('light')}><Icon name="sun" size={13} /></button>
          <button aria-pressed={theme === 'dark'} aria-label="Dark" onClick={() => bridge.setTheme('dark')}><Icon name="moon" size={13} /></button>
        </div>
        <div className="win no-drag">
          <button aria-label="Minimise" onClick={() => bridge.windowAction('minimise')}><Icon name="minimise" size={14} /></button>
          <button aria-label="Maximise" onClick={() => bridge.windowAction('maximise')}><Icon name="maximise" size={13} /></button>
          <button className="close" aria-label="Close" onClick={() => bridge.windowAction('close')}><Icon name="close" size={14} /></button>
        </div>
      </div>
    </header>
  );
}

function Rail({ state }: { state: UiState }) {
  const byLocation = new Map<string, UiState['accounts']>();
  for (const account of state.accounts) {
    const key = account.location || 'Unassigned';
    byLocation.set(key, [...(byLocation.get(key) ?? []), account]);
  }

  return (
    <nav className="rail" aria-label="Accounts">
      <button className="nav" aria-current={state.visible === null ? 'page' : undefined} onClick={() => bridge.show(null)}>
        <Icon name="grid" size={15} />
        <span>Waiting now</span>
        <span className="count mono">{state.split.needsReply}</span>
      </button>

      <div className="rail-section">Accounts</div>
      {[...byLocation].map(([location, accounts]) => (
        <div key={location}>
          <div className="rail-loc">{location}</div>
          {accounts.map((a) => (
            <button key={a.id} className="nav" aria-current={state.visible === a.id ? 'page' : undefined} onClick={() => bridge.show(a.id)}>
              <Icon name={channelIcon(a.channel)} size={14} />
              <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{a.name}</span>
              <span className="count mono" style={{ color: a.signedOut ? 'var(--shell-ink-2)' : a.waiting ? '#F0867C' : 'var(--shell-ink-2)', fontWeight: a.signedOut ? 500 : 600 }}>
                {a.signedOut ? 'Sign in' : a.waiting === null ? '' : a.waiting}
              </span>
            </button>
          ))}
        </div>
      ))}

      <div className="rail-foot">
        <button className="nav" onClick={() => bridge.readNow()}><Icon name="refresh" size={15} /><span>Re-sync now</span></button>
      </div>
    </nav>
  );
}

function Dashboard({ state }: { state: UiState }) {
  if (!state.reads) {
    return (
      <main className="board-surface">
        <div className="empty">
          <div className="col" style={{ alignItems: 'center', maxWidth: 460 }}>
            <h1>No accounts are being read yet</h1>
            <p className="sub">Add a WhatsApp, Instagram or Google Business account, and sign in to it here. The app reads who is waiting and never sends anything.</p>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="board-surface">
      {isPreview && (
        <div className="strip info">
          <Icon name="alert" size={15} />
          <span>Preview with sample data. No account is being read.</span>
        </div>
      )}
      <div className="row" style={{ alignItems: 'flex-end' }}>
        <div className="col" style={{ gap: 3 }}>
          <h1>{greeting()}</h1>
          <span className="sub" style={{ fontSize: 12.5 }}>{state.meta} · {state.freshness.text.toLowerCase()}</span>
        </div>
        <button className="btn spacer" onClick={() => bridge.readNow()}><Icon name="refresh" size={14} />Re-sync</button>
      </div>

      {state.strip && (
        <div className={`strip ${state.strip.tone}`}>
          <Icon name={state.strip.tone === 'neutral' ? 'lock' : 'clock'} size={15} />
          <span style={{ fontWeight: 600 }}>{state.strip.text}</span>
        </div>
      )}

      <div className="band">
        {state.figures.map((f) => (
          <div className="fig" key={f.label}>
            <span className="label">{f.label}</span>
            <span className="fig-value">
              <b style={{ color: f.tone === 'neutral' ? undefined : `var(--${f.tone})` }}>{f.value}</b>
              <span className="fig-unit">{f.unit}</span>
            </span>
            <span className="fig-note" style={{ color: f.tone === 'neutral' ? 'var(--ink-3)' : `var(--${f.tone})` }}>{f.note}</span>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0,1fr) 316px', gap: 22, minHeight: 0, flex: 1 }}>
        <Queue state={state} />
        <Locations state={state} />
      </div>
    </main>
  );
}

function Queue({ state }: { state: UiState }) {
  return (
    <section className="queue">
      <div className="row" style={{ paddingBottom: 10 }}>
        <h2>Waiting now</h2>
        <span className="sub" style={{ fontSize: 12 }}>longest first</span>
      </div>

      {state.queue.length === 0 ? (
        <div className="sheet empty" style={{ borderStyle: 'dashed' }}>
          <div className="col" style={{ alignItems: 'center', padding: 24 }}>
            <div style={{ width: 50, height: 50, borderRadius: 25, background: 'var(--ok-w)', color: 'var(--ok)', display: 'grid', placeItems: 'center' }}>
              <Icon name="check" size={24} />
            </div>
            <h2>Nobody is waiting</h2>
            <span className="sub" style={{ fontSize: 12.5 }}>Every customer has an answer. {state.freshness.text}.</span>
          </div>
        </div>
      ) : (
        <>
          <div className="queue-head">
            <span>Waiting</span><span>Against target</span><span>Customer</span><span>Account</span><span />
          </div>
          {state.queue.map((row) => <QueueLine key={`${row.accountId}:${row.customer}:${row.waited}`} row={row} />)}
          {state.queueTotal > state.queue.length && (
            <div className="row" style={{ padding: '11px 12px', color: 'var(--ink-3)', fontSize: 12.5 }}>
              {state.queueTotal - state.queue.length} more waiting
            </div>
          )}
        </>
      )}
    </section>
  );
}

function QueueLine({ row }: { row: QueueRow }) {
  return (
    <button className={`queue-row ${row.tone}`} onClick={() => bridge.show(row.accountId)}>
      <span className="clock" style={{ color: toneInk(row.tone) }}>{row.waited}<small>min</small></span>
      <span className="col" style={{ gap: 5 }}>
        <Meter fill={row.fill} tone={row.tone} target={row.target} />
        <span style={{ fontSize: 11.5, fontWeight: 600, color: toneInk(row.tone) }}>{row.status}</span>
      </span>
      <span className="col" style={{ gap: 1, minWidth: 0 }}>
        <span className="who">{row.customer}</span>
        <span className="preview">{row.preview || 'No preview could be read'}</span>
      </span>
      <span className="col" style={{ gap: 1, fontSize: 12 }}>
        <span>{row.accountName}</span>
        <span className="sub" style={{ fontSize: 11.5 }}>{row.location}</span>
      </span>
      <span style={{ textAlign: 'right', color: 'var(--ink-3)' }}><Icon name="right" size={15} /></span>
    </button>
  );
}

function Locations({ state }: { state: UiState }) {
  return (
    <section className="col" style={{ gap: 0 }}>
      <div className="row" style={{ paddingBottom: 10 }}><h2>Locations</h2></div>
      <div className="sheet" style={{ overflow: 'hidden' }}>
        {state.locations.map((l) => (
          <div key={l.name} className="col" style={{ gap: 6, padding: '12px 14px', borderBottom: '1px solid var(--line)' }}>
            <div className="row">
              <h3>{l.name}</h3>
              <span className="spacer" style={{ fontSize: 12, color: l.waiting ? 'var(--ink)' : 'var(--ink-3)' }}>
                {l.waiting ? `${l.waiting} waiting` : 'nobody waiting'}
              </span>
            </div>
            <Meter fill={l.onTimePercent} tone={l.tone} target={90} />
            <span style={{ fontSize: 11.5, fontWeight: 600, color: toneInk(l.tone) }}>{l.onTimePercent}% answered on time</span>
          </div>
        ))}
        <div className="col" style={{ gap: 4, padding: '12px 14px', background: 'var(--field)' }}>
          <span style={{ fontWeight: 600, fontSize: 12.5 }}>{state.split.closedAutomatically} closed by the rules</span>
          <span className="sub" style={{ fontSize: 12 }}>
            Chats whose last message ended the conversation. {state.split.unreadable > 0 && `${state.split.unreadable} could not be read and stay counted.`}
          </span>
        </div>
      </div>
    </section>
  );
}

function Meter({ fill, tone, target }: { fill: number; tone: Tone; target: number }) {
  return (
    <span className="meter">
      <i style={{ width: `${Math.min(100, fill)}%`, background: `var(--mark-${tone === 'neutral' ? 'flat' : tone})` }} />
      <u style={{ left: `${target}%` }} />
    </span>
  );
}

const toneInk = (tone: Tone) => (tone === 'neutral' ? 'var(--ink-3)' : `var(--${tone})`);
