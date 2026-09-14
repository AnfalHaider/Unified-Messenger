// Everything that opens over a screen: the command palette, Needs you, and the dialogs. The palette, Needs you and
// the account dialogs are real; removing a member and the update dialog are sample until Phases 6 and 7.
import { useEffect, useMemo, useRef, useState } from 'react';
import type { Route } from '../../app/view-model.ts';
import { channelIcon, Icon, type IconName } from '../icons.tsx';
import { bridge, Btn, Check, type LockScreen, type Nav, type Overlay, Sample, type ScreenProps, Toggle } from '../parts.tsx';
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
      {which === 'add-account' && <AddAccount state={state} nav={nav} close={close} />}
      {which === 'edit-account' && <EditAccount state={state} nav={nav} close={close} />}
      {which === 'remove-account' && <RemoveAccount state={state} nav={nav} close={close} />}
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
      ...state.queue.map((r): Item => ({ group: 'Customers', icon: channelIcon(r.channel), label: r.customer, hint: `${r.accountName} · waiting ${r.waited} min`, run: go('dock', r.accountId, r.key) })),
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

const CHANNEL_CHOICES: { channel: string; icon: IconName; name: string; detail: string }[] = [
  { channel: 'whatsapp', icon: 'chat', name: 'WhatsApp or WhatsApp Business', detail: 'Who is waiting, reply times, previews, missed calls' },
  { channel: 'instagram', icon: 'ig', name: 'Instagram', detail: 'Who is waiting in Direct. No previews.' },
  { channel: 'googlebusiness', icon: 'star', name: 'Google Business', detail: 'Opens the page. No figures until the reviews reader.' },
  { channel: 'messenger', icon: 'more', name: 'Messenger', detail: 'Opens the page. No figures.' },
  { channel: 'telegram', icon: 'more', name: 'Telegram', detail: 'Opens the page. No figures.' },
  { channel: 'custom', icon: 'open', name: 'Another page', detail: 'Any web address. No figures.' },
];

/** The location field: free text, with the existing locations offered, so a typo does not start a new location. */
function LocationField({ value, onChange, locations }: { value: string; onChange: (v: string) => void; locations: string[] }) {
  return (
    <label className="field"><span>Location</span>
      <input value={value} list="um-locations" maxLength={60} placeholder="For example, Main branch" onChange={(e) => onChange(e.target.value)} />
      <datalist id="um-locations">{locations.map((l) => <option key={l} value={l} />)}</datalist>
    </label>
  );
}

const locationNames = (state: ScreenProps['state']) => [...new Set([...state.openingHours.locations.map((l) => l.name), ...state.accounts.map((a) => a.location).filter(Boolean)])];

function AddAccount({ state, nav, close }: ScreenProps & { close: () => void }) {
  const [channel, setChannel] = useState('whatsapp');
  const [name, setName] = useState('');
  const [location, setLocation] = useState('');
  const [url, setUrl] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const submit = async () => {
    setBusy(true); setError('');
    const result = await bridge.addAccount({ channel, name, location, url: channel === 'custom' ? url : undefined });
    setBusy(false);
    if (result.error || !result.id) { setError(result.error ?? 'The account could not be added.'); return; }
    close();
    // Straight to its page, docked, to sign in there.
    nav.go('dock', result.id);
  };
  return (
    <div className="dialog" role="dialog" aria-label="Add an account" style={{ width: 600 }}>
      <h3>Add an account</h3>
      <div className="field"><span>Channel</span>
        <div style={{ display: 'grid', gap: 6 }} role="radiogroup" aria-label="Channel">
          {CHANNEL_CHOICES.map((c) => (
            <button key={c.channel} role="radio" aria-checked={c.channel === channel} onClick={() => setChannel(c.channel)} style={{ display: 'grid', gridTemplateColumns: '22px 1fr auto', gap: 10, alignItems: 'center', padding: '9px 12px', border: `1px solid ${c.channel === channel ? 'var(--ink)' : 'var(--line-2)'}`, borderRadius: 9, boxShadow: c.channel === channel ? 'inset 0 0 0 1px var(--ink)' : undefined, textAlign: 'left' }}>
              <Icon name={c.icon} size={17} /><span><b style={{ fontWeight: 600 }}>{c.name}</b><br /><span className="sub">{c.detail}</span></span>{c.channel === channel && <Icon name="check" size={16} stroke={2.2} />}
            </button>
          ))}
        </div>
      </div>
      {channel === 'custom' && <label className="field"><span>Web address</span><input value={url} placeholder="https://" onChange={(e) => setUrl(e.target.value)} /></label>}
      <div className="grid2" style={{ gap: 12 }}>
        <label className="field"><span>Name</span><input value={name} maxLength={60} placeholder="For example, Front desk WhatsApp" onChange={(e) => setName(e.target.value)} /></label>
        <LocationField value={location} onChange={setLocation} locations={locationNames(state)} />
      </div>
      <p>Next, the account’s page opens beside the line. Sign in there; the login stays on this PC.</p>
      {error && <p className="late" role="alert" style={{ margin: 0 }}>{error}</p>}
      <div className="foot"><Btn kind="quiet" onClick={close}>Cancel</Btn><Btn kind="primary" disabled={busy} onClick={() => void submit()}>Add and sign in</Btn></div>
    </div>
  );
}

function EditAccount({ state, nav, close }: ScreenProps & { close: () => void }) {
  const account = state.accounts.find((a) => a.id === nav.view.accountId);
  const [name, setName] = useState(account?.name ?? '');
  const [location, setLocation] = useState(account?.location ?? '');
  const [counted, setCounted] = useState(account?.counted ?? true);
  const [error, setError] = useState('');
  if (!account) return <div className="dialog" role="dialog" aria-label="Edit account"><h3>That account no longer exists</h3><div className="foot"><Btn onClick={close}>Close</Btn></div></div>;
  const save = async () => {
    const result = await bridge.editAccount(account.id, { name, location, professional: counted });
    if (result.error) { setError(result.error); return; }
    close();
  };
  return (
    <div className="dialog" role="dialog" aria-label="Edit account" style={{ width: 560 }}>
      <h3>Edit {account.name}</h3>
      <div className="grid2" style={{ gap: 12 }}>
        <label className="field"><span>Name</span><input value={name} maxLength={60} onChange={(e) => setName(e.target.value)} /></label>
        <LocationField value={location} onChange={setLocation} locations={locationNames(state)} />
      </div>
      {account.reads && (
        <div className="srow" style={{ padding: 0, border: 0 }}>
          <span><b>Count its customers</b><span>Off for a personal account: its page stays open, but nobody on it is counted, alerted or reported.</span></span>
          <Toggle label="Count its customers" on={counted} onChange={setCounted} />
        </div>
      )}
      {error && <p className="late" role="alert" style={{ margin: 0 }}>{error}</p>}
      <div className="foot" style={{ justifyContent: 'space-between' }}>
        <Btn kind="danger" icon="x" onClick={() => nav.open('remove-account')}>Remove account</Btn>
        <span style={{ display: 'flex', gap: 8 }}><Btn kind="quiet" onClick={close}>Cancel</Btn><Btn kind="primary" onClick={() => void save()}>Save</Btn></span>
      </div>
    </div>
  );
}

/** Where the owner removes this PC on the phone, per channel, so removing here does not leave a session alive there. */
const LINKED_DEVICE: Record<string, string> = {
  whatsapp: 'On the phone: WhatsApp › Linked devices, and remove this PC.',
  whatsappbusiness: 'On the phone: WhatsApp Business › Linked devices, and remove this PC.',
  instagram: 'In Instagram: Accounts Center › Password and security › Where you’re logged in, and log out this PC.',
};

function RemoveAccount({ state, nav, close }: ScreenProps & { close: () => void }) {
  const account = state.accounts.find((a) => a.id === nav.view.accountId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  if (!account) return <div className="dialog" role="dialog" aria-label="Remove account"><h3>That account no longer exists</h3><div className="foot"><Btn onClick={close}>Close</Btn></div></div>;
  const remove = async () => {
    setBusy(true);
    const result = await bridge.removeAccount(account.id);
    setBusy(false);
    if (result.error) { setError(result.error); return; }
    close();
    nav.go('accounts');
  };
  return (
    <div className="dialog" role="dialog" aria-label="Remove account" style={{ width: 560 }}>
      <h3>Remove {account.name}?</h3>
      <div className="panel" style={{ display: 'grid', gap: 4 }}>
        <Check icon="lock" tone="neutral" title="The login on this PC is wiped">Adding it again means signing in again.</Check>
        <Check icon="alert" tone="due" title="Its figures are deleted">Who was waiting, marks, reply times, day records and missed calls for this account. Reports lose its past days.</Check>
        {LINKED_DEVICE[account.channel] && <Check icon="phone" tone="neutral" title="The phone still lists this PC">{LINKED_DEVICE[account.channel]}</Check>}
      </div>
      <p className="sub">Its location and opening hours stay. This cannot be undone.</p>
      {error && <p className="late" role="alert" style={{ margin: 0 }}>{error}</p>}
      <div className="foot"><Btn kind="quiet" onClick={close}>Keep it</Btn><Btn kind="danger" disabled={busy} onClick={() => void remove()}>Remove and wipe login</Btn></div>
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
