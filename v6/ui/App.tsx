// The shell: title bar, sidebar, and whichever screen is open. Screens draw what the main process sends and
// decide nothing: every real figure is computed in core/ and shaped in app/view-model.ts. Where a feature is
// not connected yet, its screen draws sample figures from sample.ts and says so.
//
// Which screen is open is kept here, not in main, so the screens can also be walked in a plain browser. Main
// is told about every move, because it lays the account's real page over the dock.
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { Route, UiState } from '../app/view-model.ts';
import { Icon, type IconName } from './icons.tsx';
import { bridge, isPreview, type LockScreen, Logo, type Nav, onPreviewSettings, type Overlay, type View } from './parts.tsx';
import { AccountDetailScreen, AccountsScreen, LostLoginScreen, ReaderScreen } from './screens/accounts.tsx';
import { AssistantScreen } from './screens/assistant.tsx';
import { needsCount, Overlays } from './screens/overlays.tsx';
import { ReportsScreen, ReviewsScreen } from './screens/reports.tsx';
import { SettingsScreen } from './screens/settings.tsx';
import { DigestScreen, DockScreen, laneNames, LineScreen, SetAsideScreen } from './screens/work.tsx';
import { Lock, OwnerScreen } from './screens/workspace.tsx';

const START: View = { route: 'line', accountId: null, sub: '', overlay: null, lock: null, offline: false };

export function App() {
  const [state, setState] = useState<UiState | null>(null);
  const [view, setView] = useState<View>(START);
  const [scope, setScope] = useState('All');

  useEffect(() => {
    if (!isPreview) {
      bridge.onState(setState);
      bridge.onOpen((route, accountId, sub) => setView((v) => ({ ...v, route, accountId, sub, overlay: null })));
      bridge.ready();
      return;
    }
    onPreviewSettings((patch) => setState((s) => (s ? { ...s, settings: { ...s.settings, ...patch }, theme: (patch.theme as UiState['theme']) ?? s.theme } : s)));
    void import('./preview-state.ts').then((m) => setState(m.PREVIEW_STATE));
  }, []);

  // In the app, main sets nativeTheme from the same setting, so prefers-color-scheme already carries the choice
  // and follows Windows live on "Match Windows". The explicit branches keep the browser preview honest.
  useEffect(() => {
    if (!state) return;
    const query = window.matchMedia('(prefers-color-scheme: dark)');
    const apply = () => {
      const dark = state.theme === 'dark' || (state.theme === 'system' && query.matches);
      document.documentElement.dataset.theme = dark ? 'dark' : 'light';
    };
    apply();
    query.addEventListener('change', apply);
    return () => query.removeEventListener('change', apply);
  }, [state?.theme]);

  // Main shows the account's page only on the dock, and only when nothing is laid over it: a native page always
  // draws above the screens, so an open overlay or a full-window state has to hide it.
  useEffect(() => {
    const covered = view.overlay || view.lock;
    bridge.navigate(covered ? 'line' : view.route, view.accountId);
  }, [view.route, view.accountId, view.overlay, view.lock]);

  const nav = useMemo<Nav>(() => ({
    view,
    go: (route: Route, accountId: string | null = null, sub = '') => setView((v) => ({ ...v, route, accountId, sub, overlay: null })),
    open: (overlay: Overlay | null) => setView((v) => ({ ...v, overlay })),
    lock: (lock: LockScreen | null) => setView((v) => ({ ...v, lock, overlay: null })),
    setOffline: (offline: boolean) => setView((v) => ({ ...v, offline })),
  }), [view]);

  const onKey = useCallback((e: KeyboardEvent) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); nav.open(view.overlay === 'palette' ? null : 'palette'); }
    else if (e.key === 'Escape' && view.overlay) nav.open(null);
    else if (e.key === 'Escape' && view.route === 'dock') nav.go('line');
  }, [nav, view.overlay, view.route]);
  useEffect(() => { window.addEventListener('keydown', onKey); return () => window.removeEventListener('keydown', onKey); }, [onKey]);

  if (!state) return <div className="app"><TitleBar /><div className="body"><nav className="rail" /><main className="main"><p className="sub">Starting…</p></main></div></div>;

  const props = { state, nav };
  return (
    <div className="app" style={{ position: 'relative' }}>
      <TitleBar state={state} nav={nav} scope={scope} onScope={setScope} />
      {view.lock ? <Lock screen={view.lock} {...props} /> : (
        <div className="body">
          <Rail state={state} route={view.route} nav={nav} />
          <div className="screen">
            {view.offline && (
              <div className="offline-bar"><Icon name="offline" size={18} /><span><b style={{ fontWeight: 600 }}>This PC is offline.</b> <span className="sub">Figures are from the last read. Reading starts again by itself when the connection returns.</span></span>
                <button className="btn quiet" onClick={() => nav.setOffline(false)}>Hide</button></div>
            )}
            {isPreview && view.route === 'line' && <div className="offline-bar" style={{ background: 'var(--hover)' }}><Icon name="alert" size={16} /><span className="sub">Preview in a browser: every figure here is sample data.</span><span /></div>}
            <Screen {...props} scope={scope} />
          </div>
        </div>
      )}
      <Overlays {...props} />
    </div>
  );
}

function Screen({ state, nav, scope }: { state: UiState; nav: Nav; scope: string }) {
  const props = { state, nav };
  switch (nav.view.route) {
    case 'dock': return <DockScreen {...props} scope={scope} />;
    case 'set-aside': return <SetAsideScreen {...props} />;
    case 'digest': return <DigestScreen {...props} />;
    case 'accounts': return <AccountsScreen {...props} />;
    case 'account-detail': return <AccountDetailScreen {...props} />;
    case 'reader': return <ReaderScreen {...props} />;
    case 'lost-login': return <LostLoginScreen {...props} />;
    case 'reviews': return <ReviewsScreen {...props} />;
    case 'reports': return <ReportsScreen {...props} />;
    case 'assistant': return <AssistantScreen {...props} />;
    case 'settings': return <SettingsScreen {...props} />;
    case 'owner': return <OwnerScreen {...props} />;
    default: return <LineScreen {...props} scope={scope} />;
  }
}

function TitleBar({ state, nav, scope, onScope }: { state?: UiState; nav?: Nav; scope?: string; onScope?: (s: string) => void }) {
  const locations = state ? laneNames(state) : [];
  const count = (loc: string) => (state ? (loc === 'All' ? state.queueTotal : state.queue.filter((r) => (r.location || 'No location') === loc).length) : 0);
  const needs = state ? needsCount(state) : 0;
  const themes: ['system' | 'light' | 'dark', IconName, string][] = [['system', 'monitor', 'Match Windows'], ['light', 'sun', 'Light'], ['dark', 'moon', 'Dark']];
  return (
    <header className="tb">
      <Logo size={22} /><span className="tb-name">Unified Messenger</span>
      {state && locations.length > 1 && (
        <div className="scope" role="group" aria-label="Locations">
          {['All', ...locations].map((loc) => (
            <button key={loc} aria-pressed={loc === scope} onClick={() => onScope?.(loc)}>{loc === 'All' ? 'All locations' : loc} <b className="num">{count(loc)}</b></button>
          ))}
        </div>
      )}
      <div className="tb-right">
        {state && <span className="fresh" style={state.freshness.isStale ? { color: 'var(--due)' } : undefined}><i style={{ background: state.freshness.isStale ? 'var(--m-due)' : 'var(--m-ok)' }} />{state.freshness.text}</span>}
        {nav && (
          <button className="needs" aria-expanded={nav.view.overlay === 'needs'} onClick={() => nav.open(nav.view.overlay === 'needs' ? null : 'needs')}>
            <Icon name="bell" size={14} />Needs you {needs > 0 && <span className="dot num">{needs}</span>}
          </button>
        )}
        {nav && <button className="btn quiet" style={{ height: 28 }} onClick={() => nav.open('palette')} title="Find anything (Ctrl K)"><Icon name="search" size={14} /><kbd>Ctrl K</kbd></button>}
        <div className="theme3" role="group" aria-label="Theme">
          {themes.map(([t, icon, label]) => <button key={t} aria-label={label} title={label} aria-pressed={state?.theme === t} onClick={() => bridge.setTheme(t)}><Icon name={icon} size={14} /></button>)}
        </div>
        <div className="win">
          <button aria-label="Minimise" onClick={() => bridge.windowAction('minimise')}><Icon name="min" size={14} /></button>
          <button aria-label="Maximise" onClick={() => bridge.windowAction('maximise')}><Icon name="max" size={13} /></button>
          <button className="close" aria-label={state?.settings.closeToBackground ? 'Close, keep reading in the background' : 'Quit'} title={state?.settings.closeToBackground ? 'Close (keeps reading in the background)' : 'Quit'} onClick={() => bridge.windowAction('close')}><Icon name="x" size={14} /></button>
        </div>
      </div>
    </header>
  );
}

function Rail({ state, route, nav }: { state: UiState; route: Route; nav: Nav }) {
  const groups: Record<string, Route[]> = {
    line: ['line', 'dock', 'set-aside', 'digest'], accounts: ['accounts', 'account-detail', 'reader', 'lost-login'],
    reviews: ['reviews'], reports: ['reports'], assistant: ['assistant'], settings: ['settings', 'owner'],
  };
  const item = (key: Route, icon: IconName, label: string, badge?: number) => (
    <button aria-current={groups[key]?.includes(route) ? 'page' : undefined} onClick={() => nav.go(key)} aria-label={badge ? `${label}, ${badge}` : label}>
      <Icon name={icon} size={20} stroke={1.6} /><span>{label}</span>{badge ? <span className="badge num">{badge}</span> : null}
    </button>
  );
  return (
    <nav className="rail" aria-label="Screens">
      {item('line', 'line', 'The line', state.split.needsReply)}
      {item('accounts', 'grid', 'Accounts')}
      {item('reviews', 'star', 'Reviews')}
      {item('reports', 'chart', 'Reports')}
      {item('assistant', 'spark', 'Assistant')}
      <div className="foot">{item('settings', 'gear', 'Settings')}</div>
    </nav>
  );
}
