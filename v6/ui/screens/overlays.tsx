// Everything that opens over a screen: the command palette, Needs you, and the three dialogs. The palette and
// Needs you read the real view model; the dialogs are sample until accounts and members can be edited.
import { useEffect, useMemo, useRef, useState } from 'react';
import type { Route } from '../../app/view-model.ts';
import { channelIcon, Icon, type IconName } from '../icons.tsx';
import { bridge, Btn, Check, type LockScreen, type Nav, type Overlay, Sample, type ScreenProps } from '../parts.tsx';
import { REPORT_TABS } from './reports.tsx';
import { SETTINGS_SECTIONS } from './settings.tsx';

export function Overlays({ state, nav }: ScreenProps) {
  const which = nav.view.overlay;
  if (!which) return null;
  const close = () => nav.open(null);
  return (
    <div className="overlay" onKeyDown={(e) => e.key === 'Escape' && close()}>
      <div className="scrim" onClick={close} />
      {which === 'palette' && <Palette state={state} nav={nav} />}
      {which === 'needs' && <NeedsYou state={state} nav={nav} />}
      {which === 'add-account' && <AddAccount close={close} />}
      {which === 'remove-member' && <RemoveMember close={close} />}
      {which === 'update' && <Update close={close} />}
    </div>
  );
}

// ---- the command palette ------------------------------------------------------------------------------------

type Item = { group: 'Customers' | 'Go to' | 'Do'; icon: IconName; label: string; hint?: string; run: () => void };

function Palette({ state, nav }: ScreenProps) {
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => input.current?.focus(), []);

  const all = useMemo<Item[]>(() => {
    const go = (route: Route, id: string | null = null, sub = '') => () => nav.go(route, id, sub);
    const lock = (s: LockScreen) => () => nav.lock(s);
    const screens: [IconName, string, () => void][] = [
      ['line', 'The line', go('line')], ['sunrise', 'Morning digest', go('digest')], ['reopen', 'Set aside', go('set-aside')],
      ['grid', 'Accounts', go('accounts')], ['star', 'Reviews', go('reviews')], ['spark', 'Assistant', go('assistant')], ['key', 'Owner console', go('owner')],
      ...REPORT_TABS.map((t): [IconName, string, () => void] => ['chart', `Reports: ${t}`, go('reports', null, t)]),
      ...SETTINGS_SECTIONS.map((s): [IconName, string, () => void] => ['gear', `Settings: ${s}`, go('settings', null, s)]),
      ['lock', 'Preview: sign in with Google', lock('sign-in')], ['users', 'Preview: a new PC', lock('new-pc')],
      ['lock', 'Preview: a removed PC', lock('removed')], ['alert', 'Preview: a suspended workspace', lock('suspended')],
      ['download', 'Preview: moving from the previous version', lock('upgrade')],
    ];
    return [
      ...state.queue.map((r): Item => ({ group: 'Customers', icon: channelIcon(r.channel), label: r.customer, hint: `${r.accountName} · waiting ${r.waited} min`, run: go('dock', r.accountId, r.customer) })),
      ...state.accounts.map((a): Item => ({ group: 'Go to', icon: channelIcon(a.channel), label: a.name, hint: 'Accounts', run: go('dock', a.id) })),
      ...screens.map(([icon, label, run]): Item => ({ group: 'Go to', icon, label, run })),
      { group: 'Do', icon: 'refresh', label: 'Read every account now', hint: 'R', run: () => bridge.readNow() },
      { group: 'Do', icon: 'monitor', label: 'Theme: match Windows', run: () => bridge.setTheme('system') },
      { group: 'Do', icon: 'sun', label: 'Theme: light', run: () => bridge.setTheme('light') },
      { group: 'Do', icon: 'moon', label: 'Theme: dark', run: () => bridge.setTheme('dark') },
      { group: 'Do', icon: 'users', label: 'Add an account', run: () => nav.open('add-account') },
    ];
  }, [state, nav]);

  const q = query.trim().toLowerCase();
  const items = (q ? all.filter((i) => `${i.label} ${i.hint ?? ''}`.toLowerCase().includes(q)) : all.filter((i) => i.group !== 'Go to' || !i.label.startsWith('Preview'))).slice(0, 12);
  const run = (i: Item | undefined) => { if (!i) return; nav.open(null); i.run(); };

  return (
    <div className="palette" role="dialog" aria-label="Command palette">
      <div className="q">
        <Icon name="search" size={18} />
        <input ref={input} value={query} placeholder="Find a customer, an account, a screen…" aria-label="Search"
          onChange={(e) => { setQuery(e.target.value); setActive(0); }}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') { setActive((a) => Math.min(items.length - 1, a + 1)); e.preventDefault(); }
            if (e.key === 'ArrowUp') { setActive((a) => Math.max(0, a - 1)); e.preventDefault(); }
            if (e.key === 'Enter') run(items[active]);
            if (e.key === 'Escape') nav.open(null);
          }}
          style={{ flex: 1, border: 0, background: 'transparent', font: 'inherit', color: 'var(--ink)', outline: 'none' }} />
        <kbd>Esc</kbd>
      </div>
      <div style={{ maxHeight: 460, overflow: 'auto', paddingBottom: 8 }}>
        {items.map((i, n) => (
          <div key={`${i.group}:${i.label}:${n}`}>
            {(n === 0 || items[n - 1].group !== i.group) && <h5>{i.group}</h5>}
            <div className={`pal-item ${n === active ? 'on' : ''}`} onMouseEnter={() => setActive(n)} onClick={() => run(i)} style={{ cursor: 'pointer' }}>
              <Icon name={i.icon} size={16} />
              <span>{i.label} {i.hint && <span className="sub">· {i.hint}</span>}</span>
              <span className="sub">{n === active ? <kbd>Enter</kbd> : ''}</span>
            </div>
          </div>
        ))}
        {items.length === 0 && <p className="sub" style={{ padding: '12px 16px', margin: 0 }}>Nothing matches “{query}”.</p>}
      </div>
    </div>
  );
}

// ---- needs you ----------------------------------------------------------------------------------------------

export const needsCount = (state: ScreenProps['state']) =>
  state.accounts.filter((a) => a.signedOut).length + state.modules.filter((m) => m.tone === 'late').length;

function NeedsYou({ state, nav }: ScreenProps) {
  const signedOut = state.accounts.filter((a) => a.signedOut);
  const broken = state.modules.filter((m) => m.tone === 'late' || m.tone === 'due');
  return (
    <aside className="drawer" aria-label="Needs you">
      <h3>Needs you</h3>
      {signedOut.length > 0 && <h4>Sign in again</h4>}
      {signedOut.map((a) => (
        <div key={a.id} className="todo">
          <span style={{ color: 'var(--due)' }}><Icon name={a.channel.startsWith('whatsapp') ? 'qr' : 'lock'} size={18} /></span>
          <span><b>{a.name}</b><span>{a.channel.startsWith('whatsapp') ? 'Showing a QR code. Scan it from the phone that owns this number.' : 'Asked for the password.'} Its customers are not being counted until then.</span></span>
          <Btn kind="primary" onClick={() => { nav.open(null); nav.go('dock', a.id); }}>Sign in</Btn>
        </div>
      ))}
      {broken.length > 0 && <h4>Readers</h4>}
      {broken.map((m) => (
        <div key={m.id} className="todo">
          <span style={{ color: m.tone === 'late' ? 'var(--late)' : 'var(--due)' }}><Icon name="alert" size={18} /></span>
          <span><b>{m.name} reader: {m.status}</b><span>{m.detail}</span></span>
          <Btn onClick={() => { nav.open(null); nav.go('reader', null, m.id); }}>Details</Btn>
        </div>
      ))}
      {signedOut.length === 0 && broken.length === 0 && (
        <div className="todo" style={{ gridTemplateColumns: '22px 1fr' }}><span style={{ color: 'var(--ok)' }}><Icon name="check" size={18} /></span><span><b>Nothing needs you</b><span>Every account is signed in and every reader is working.</span></span></div>
      )}
    </aside>
  );
}

// ---- dialogs --------------------------------------------------------------------------------------------------

function AddAccount({ close }: { close: () => void }) {
  const [channel, setChannel] = useState(0);
  const options: [IconName, string, string][] = [
    ['chat', 'WhatsApp or WhatsApp Business', 'Who is waiting, reply times, previews'],
    ['ig', 'Instagram', 'Who is waiting in the inbox. Previews are shorter.'],
    ['star', 'Google reviews', 'Rating, total and unanswered reviews'],
    ['more', 'Messenger, Telegram, other page', 'Opens the page. No figures.'],
  ];
  return (
    <div className="dialog" role="dialog" aria-label="Add an account" style={{ width: 600 }}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}><h3>Add an account</h3><Sample /></div>
      <div className="field"><span>Channel</span>
        <div style={{ display: 'grid', gap: 6 }}>
          {options.map(([icon, name, detail], i) => (
            <button key={name} onClick={() => setChannel(i)} aria-pressed={i === channel} style={{ display: 'grid', gridTemplateColumns: '22px 1fr auto', gap: 10, alignItems: 'center', padding: '10px 12px', border: `1px solid ${i === channel ? 'var(--ink)' : 'var(--line-2)'}`, borderRadius: 9, boxShadow: i === channel ? 'inset 0 0 0 1px var(--ink)' : undefined, textAlign: 'left' }}>
              <Icon name={icon} size={17} /><span><b style={{ fontWeight: 600 }}>{name}</b><br /><span className="sub">{detail}</span></span>{i === channel && <Icon name="check" size={16} stroke={2.2} />}
            </button>
          ))}
        </div>
      </div>
      <div className="grid2" style={{ gap: 12 }}>
        <label className="field"><span>Name</span><input placeholder="For example, Front desk WhatsApp" /></label>
        <label className="field"><span>Location</span><input placeholder="For example, Main branch" /></label>
      </div>
      <p>Next, the account’s page opens beside the line. Sign in there; the login stays on this PC.</p>
      <div className="foot"><Btn kind="quiet" onClick={close}>Cancel</Btn><Btn kind="primary" disabled title="Adding accounts is not connected yet">Add and sign in</Btn></div>
    </div>
  );
}

function RemoveMember({ close }: { close: () => void }) {
  return (
    <div className="dialog" role="dialog" aria-label="Remove member" style={{ width: 560 }}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}><h3>Remove Front desk DHA-2?</h3><Sample /></div>
      <p>desk.dha2@example.com loses access to the workspace on every PC they use.</p>
      <div className="panel" style={{ display: 'grid', gap: 4 }}>
        <Check icon="check" tone="neutral" title="When DHA-2 reception is next online">The app signs out and wipes the account logins saved on that PC. It was last online 1 hour ago.</Check>
        <Check icon="alert" tone="due" title="To cut access right now">On the phone: WhatsApp › Linked devices › remove “DHA-2 reception”. Do the same for Instagram’s login activity.</Check>
      </div>
      <p className="sub">Their history on that PC is wiped with the logins. Figures on your own PCs are not affected.</p>
      <div className="foot"><Btn kind="quiet" onClick={close}>Cancel</Btn><Btn kind="danger" disabled title="Removing members is not connected yet">Remove and wipe logins</Btn></div>
    </div>
  );
}

function Update({ close }: { close: () => void }) {
  return (
    <div className="drawer" role="dialog" aria-label="Update ready" style={{ right: 180 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}><Icon name="download" size={18} /><h3>Version 6.1 is ready</h3><Sample /></div>
      <ul style={{ margin: 0, paddingLeft: 18, display: 'grid', gap: 6, fontSize: 13.5 }}>
        <li>Missed calls show whether a customer wrote after calling.</li><li>The Instagram reader copes with the new inbox layout.</li><li>Reports export to CSV with location names.</li>
      </ul>
      <p className="sub" style={{ margin: 0 }}>Restarting takes about 10 seconds. Logins and waiting customers are kept.</p>
      <div style={{ display: 'flex', gap: 8 }}><Btn icon="refresh" kind="primary" disabled title="Updates are not connected yet">Restart now</Btn><Btn onClick={close}>When the business closes</Btn></div>
    </div>
  );
}

export type { Overlay };
