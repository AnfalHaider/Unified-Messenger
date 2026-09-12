// The day's work: the line, a chat docked beside it, what was set aside, and the morning digest.
// The line, the dock and Set aside read the real view model. The digest, and the customer panel's notes and
// saved replies are sample figures until those features are wired.
import { useEffect, useMemo, useState } from 'react';
import type { QueueRow, UiState } from '../../app/view-model.ts';
import { Spark, TheLine, toneInk } from '../charts.tsx';
import { channelIcon, Icon } from '../icons.tsx';
import { bridge, Btn, Chip, Headline, isPreview, Panel, plural, Sample, type ScreenProps, Wait, waitText } from '../parts.tsx';
import { CUSTOMER, OWED, YESTERDAY } from '../sample.ts';

const rowKey = (r: QueueRow) => `${r.accountId}:${r.key}`;
/** Snoozing from the line or the dock is always an hour; the keys hint says so. */
const SNOOZE_MINUTES = 60;

/** Handled or snoozed: the row leaves, so the one after it (or before, at the end) takes its place. */
function takeOffLine(rows: QueueRow[], r: QueueRow, how: 'handled' | 'snooze'): QueueRow | undefined {
  const i = rows.findIndex((x) => rowKey(x) === rowKey(r));
  if (how === 'handled') bridge.markHandled(r.accountId, r.key);
  else bridge.snooze(r.accountId, r.key, SNOOZE_MINUTES);
  return rows[i + 1] ?? rows[i - 1];
}

/** H and S from the keyboard, unless something else has it. */
const actionKey = (e: KeyboardEvent, overlay: unknown): 'handled' | 'snooze' | null =>
  overlay || e.ctrlKey || e.metaKey || e.altKey || (e.target as HTMLElement).closest('input, textarea') ? null
    : e.key === 'h' ? 'handled' : e.key === 's' ? 'snooze' : null;
const statusText = (r: QueueRow, target: number) =>
  r.tone === 'late' ? `${waitText(r.waited - target).join(' ')} past target` : r.tone === 'due' ? `Due in ${Math.max(0, target - r.waited)} min` : 'On time';

/** Locations for the lanes: every configured one, in the order the rollup gives, so an empty lane still shows. */
export const laneNames = (state: UiState) => {
  const names = state.locations.map((l) => l.name);
  for (const r of state.queue) if (!names.includes(r.location || 'No location')) names.push(r.location || 'No location');
  return names.length ? names : ['No location'];
};

export function scoped(state: UiState, scope: string) {
  return scope === 'All' ? state.queue : state.queue.filter((r) => (r.location || 'No location') === scope);
}

// ---- the line ----------------------------------------------------------------------------------------------

export function LineScreen({ state, nav, scope }: ScreenProps & { scope: string }) {
  const rows = scoped(state, scope);
  const target = state.settings.slaMinutes;
  const [selected, setSelected] = useState<string | null>(rows[0] ? rowKey(rows[0]) : null);
  const late = rows.filter((r) => r.tone === 'late').length;
  const due = rows.filter((r) => r.tone === 'due').length;
  const total = scope === 'All' ? state.queueTotal : rows.length;
  const lanes = scope === 'All' ? laneNames(state) : [scope];
  const onTime = state.figures.find((f) => f.label === 'Answered on time');
  const firstReply = state.figures.find((f) => f.label === 'First reply');

  const act = (r: QueueRow, how: 'handled' | 'snooze') => {
    const next = takeOffLine(rows, r, how);
    setSelected(next ? rowKey(next) : null);
  };

  // J and K move, Enter opens the chat, H and S set it aside. Ignored while an overlay has the keyboard.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (nav.view.overlay || e.ctrlKey || e.metaKey || (e.target as HTMLElement).closest('input, textarea')) return;
      const i = rows.findIndex((r) => rowKey(r) === selected);
      const how = actionKey(e, nav.view.overlay);
      if (how && rows[i]) { act(rows[i], how); e.preventDefault(); return; }
      if (e.key === 'j' || e.key === 'ArrowDown') { const next = rows[Math.min(rows.length - 1, i + 1)]; if (next) setSelected(rowKey(next)); e.preventDefault(); }
      if (e.key === 'k' || e.key === 'ArrowUp') { const prev = rows[Math.max(0, i - 1)]; if (prev) setSelected(rowKey(prev)); e.preventDefault(); }
      if (e.key === 'Enter' && rows[i]) nav.go('dock', rows[i].accountId, rows[i].customer);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [rows, selected, nav]);

  if (!state.reads) {
    return (
      <main className="main">
        <Headline title="No accounts are being read yet" actions={<Btn icon="grid" kind="primary" onClick={() => nav.go('accounts')}>Go to accounts</Btn>}>
          Sign in to a WhatsApp or Instagram account and the people waiting on it appear here. The app reads who is waiting and never sends anything.
        </Headline>
        <TheLine rows={[]} locations={lanes} target={target} />
      </main>
    );
  }

  if (rows.length === 0) {
    return (
      <main className="main">
        <Headline title="Nobody is waiting" actions={<Btn icon="refresh" onClick={() => bridge.readNow()}>Read now</Btn>}>
          {scope === 'All' ? 'Every customer across your locations has an answer.' : `Every customer at ${scope} has an answer.`} {state.freshness.text}.
          {onTime && <> Today <b>{onTime.value}%</b> were answered on time.</>}
        </Headline>
        <TheLine rows={[]} locations={lanes} target={target} />
        <Panel style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: 16, alignItems: 'center' }}>
          <span style={{ width: 44, height: 44, borderRadius: '50%', background: 'var(--ok-w)', color: 'var(--ok)', display: 'grid', placeItems: 'center' }}><Icon name="check" size={22} stroke={2} /></span>
          <span><b style={{ fontWeight: 600 }}>All caught up</b><br /><span className="sub">The line fills in as soon as someone writes. {state.split.closedAutomatically} chats were closed by the “ended the chat” rule; see Set aside.</span></span>
        </Panel>
      </main>
    );
  }

  return (
    <main className="main">
      <Headline title={`${plural(total, 'customer')} ${total === 1 ? 'is' : 'are'} waiting`} actions={<Btn icon="refresh" onClick={() => bridge.readNow()}>Read now</Btn>}>
        {late > 0 ? <b className="late">{late} {late === 1 ? 'is' : 'are'} past your {target}-minute target</b> : <b>Nobody is past your {target}-minute target</b>}
        {due > 0 ? `, ${due} more pass it in the next five minutes.` : '.'}
        {onTime && firstReply && firstReply.value !== '—' && <> Today {onTime.value}% were answered on time, with a median first reply of {firstReply.value} minutes.</>}
      </Headline>
      <TheLine rows={rows} locations={lanes} target={target} selected={selected} onSelect={(r) => setSelected(rowKey(r))} />
      <div className="work">
        <div className="qhead"><strong>Longest wait first</strong><span>{plural(total, 'customer')}</span>
          <span className="keys"><kbd>J</kbd><kbd>K</kbd> move <kbd>Enter</kbd> open chat <kbd>H</kbd> handled <kbd>S</kbd> snooze 1 hour</span></div>
        <div className="queue">
          {rows.map((r) => {
            const key = rowKey(r);
            const sel = key === selected;
            return (
              <div key={key} className={`row ${r.tone} ${sel ? 'sel' : ''}`} onClick={() => setSelected(key)} onDoubleClick={() => nav.go('dock', r.accountId, r.customer)}>
                <Wait minutes={r.waited} tone={r.tone} />
                <span className="who"><b>{r.customer}</b><span>{r.preview || 'No preview could be read'}</span></span>
                <span className="acct"><Icon name={channelIcon(r.channel)} size={15} /><span>{r.accountName}</span></span>
                {sel ? (
                  <div className="row-actions">
                    <Btn icon="open" kind="primary" onClick={() => nav.go('dock', r.accountId, r.customer)}>Open chat</Btn>
                    <Btn icon="check" title="Answered another way. Returns if they write again." onClick={(e) => { e.stopPropagation(); act(r, 'handled'); }}>Handled</Btn>
                    <Btn icon="snooze" title="Off the line for an hour" onClick={(e) => { e.stopPropagation(); act(r, 'snooze'); }}>Snooze</Btn>
                  </div>
                ) : <span className={`status ${r.tone}`}>{statusText(r, target)}</span>}
              </div>
            );
          })}
          {state.queueTotal > state.queue.length && scope === 'All' && (
            <div className="empty-line" style={{ padding: '12px 18px' }}>{state.queueTotal - state.queue.length} more waiting, shown as the ones above are answered.</div>
          )}
        </div>
      </div>
    </main>
  );
}

// ---- the dock ----------------------------------------------------------------------------------------------

export function DockScreen({ state, nav, scope }: ScreenProps & { scope: string }) {
  const rows = scoped(state, scope);
  const d = state.detail;
  const customer = rows.find((r) => r.accountId === nav.view.accountId && r.customer === nav.view.sub)
    ?? rows.find((r) => r.accountId === nav.view.accountId);
  const [panel, setPanel] = useState<'customer' | 'reply'>('customer');
  const late = rows.filter((r) => r.tone === 'late').length;
  const due = rows.filter((r) => r.tone === 'due').length;
  const target = state.settings.slaMinutes;

  // Setting the docked customer aside moves the dock on to the next one waiting, or back to the line when none is.
  const act = (how: 'handled' | 'snooze') => {
    if (!customer) return;
    const next = takeOffLine(rows, customer, how);
    if (next) nav.go('dock', next.accountId, next.customer);
    else nav.go('line');
  };
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const how = actionKey(e, nav.view.overlay);
      if (how && customer) { act(how); e.preventDefault(); }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  return (
    <div className="split">
      <main className="main" style={{ gap: 14, paddingRight: 16 }}>
        <Headline title={`${rows.length} waiting`}><b className="late">{late} past target</b>, {due} due soon</Headline>
        <div className="queue mini">
          {rows.map((r) => (
            <div key={rowKey(r)} className={`row ${r.tone} ${customer && rowKey(customer) === rowKey(r) ? 'sel' : ''}`} onClick={() => nav.go('dock', r.accountId, r.customer)}>
              <Wait minutes={r.waited} tone={r.tone} />
              <span className="who"><b>{r.customer}</b><span><Icon name={channelIcon(r.channel)} size={12} /> {r.location} · {r.preview}</span></span>
              <span style={{ color: 'var(--ink-3)' }}><Icon name="right" size={15} /></span>
            </div>
          ))}
          {rows.length === 0 && <div className="empty-line" style={{ padding: 16 }}>Nobody is waiting.</div>}
        </div>
      </main>
      <section className="dock" aria-label={d?.name ?? 'Account'}>
        <div className="dock-bar">
          <Icon name={channelIcon(d?.channel ?? '')} size={18} />
          <span className="who">
            <b>{customer?.customer ?? d?.name ?? 'Account'}</b>
            <span>{d?.name}{customer ? ` · waiting ${waitText(customer.waited).join(' ')}${customer.tone === 'late' ? `, ${waitText(customer.waited - target).join(' ')} past target` : ''}` : d ? ` · ${d.freshness.text}` : ''}</span>
          </span>
          <div className="actions">
            <Btn icon="check" disabled={!customer} title="Answered another way. Returns if they write again." onClick={() => act('handled')}>Handled <kbd>H</kbd></Btn>
            <Btn icon="snooze" disabled={!customer} title="Off the line for an hour" onClick={() => act('snooze')}>Snooze <kbd>S</kbd></Btn>
            {d && <Btn icon="refresh" kind="quiet" onClick={() => bridge.reloadAccount(d.id)}>Reload</Btn>}
            <Btn icon="x" kind="quiet" onClick={() => nav.go('line')}>Close</Btn>
          </div>
        </div>
        <div className="dock3">
          {/* The account's real page is laid over this slot by the main process. */}
          <div className="page-slot">{isPreview ? 'The account’s own page appears here.' : d?.signedOut ? 'Sign in on the page to start reading.' : ''}</div>
          <aside className="cust" aria-label="About this customer">
            <div className="seg" role="group" aria-label="Panel" style={{ alignSelf: 'start' }}>
              <button aria-pressed={panel === 'customer'} onClick={() => setPanel('customer')}>Customer</button>
              <button aria-pressed={panel === 'reply'} onClick={() => setPanel('reply')}><Icon name="spark" size={12} /> Suggest a reply</button>
            </div>
            <Sample />
            {panel === 'customer' ? <CustomerPanel /> : <ReplyPanel name={customer?.customer ?? 'this customer'} />}
          </aside>
        </div>
      </section>
    </div>
  );
}

function CustomerPanel() {
  return (
    <>
      <div><h4>Seen by the app</h4><div className="history">{CUSTOMER.history.map(([a, b]) => <div key={a}><span>{a}</span><span>{b}</span></div>)}</div></div>
      <div><h4>Tags</h4><div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>{CUSTOMER.tags.map((t) => <span key={t} className="pill-tag"><Icon name="tag" size={11} />{t}</span>)}<span className="pill-tag" style={{ color: 'var(--ink-3)' }}>+ Add</span></div></div>
      <div><h4>Note, kept on this PC</h4><p style={{ margin: 0, fontSize: 13, lineHeight: 1.5, padding: '9px 10px', border: '1px solid var(--line)', borderRadius: 8, background: 'var(--raised)' }}>{CUSTOMER.note}</p></div>
      <div><h4>Saved replies</h4><div className="saved">{CUSTOMER.saved.map((s) => <div key={s.title}><span><b>{s.title}</b>{s.body}</span><Btn icon="copy" onClick={() => void navigator.clipboard?.writeText(s.body)}>Copy</Btn></div>)}</div></div>
    </>
  );
}

function ReplyPanel({ name }: { name: string }) {
  const drafts = [
    ['Warm and complete', `Thank you for your message, ${name.split(' ')[0]}. We can certainly help with that — shall I hold a time for you today or tomorrow?`],
    ['Short', 'Thanks for writing! Which time suits you?'],
  ];
  return (
    <>
      <p className="sub" style={{ margin: 0 }}>Drafted on this PC from this chat’s last few messages only. Copy it and send it yourself.</p>
      {drafts.map(([title, body], i) => (
        <div key={title} style={{ border: `1px solid ${i === 0 ? 'var(--ink)' : 'var(--line-2)'}`, borderRadius: 10, padding: 12, background: 'var(--raised)', display: 'grid', gap: 8 }}>
          <b style={{ fontWeight: 600, fontSize: 13 }}>{title}</b><span style={{ fontSize: 13.5, lineHeight: 1.55 }}>{body}</span>
          <div style={{ display: 'flex', gap: 6 }}><Btn icon="copy" kind={i === 0 ? 'primary' : undefined} onClick={() => void navigator.clipboard?.writeText(body)}>Copy</Btn></div>
        </div>
      ))}
      <div className="sub" style={{ display: 'flex', gap: 6, alignItems: 'center', marginTop: 'auto' }}><Icon name="shield" size={13} />The app cannot send messages.</div>
    </>
  );
}

// ---- set aside ---------------------------------------------------------------------------------------------

/** A time said the way a person would: "4:12 pm" today, "Tue 4:12 pm" otherwise. */
function when(ms: number) {
  const d = new Date(ms);
  const time = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  return d.toDateString() === new Date().toDateString() ? time : `${d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' })}, ${time}`;
}

export function SetAsideScreen({ state }: ScreenProps) {
  const [filter, setFilter] = useState('All');
  const list = useMemo(() => state.setAside.filter((s) => filter === 'All' || s.why === filter), [state.setAside, filter]);
  const closed = state.setAside.filter((s) => s.why === 'Closed by rule').length;
  return (
    <main className="main">
      <Headline title="Set aside" actions={
        <div className="seg" role="group" aria-label="Show">{['All', 'Snoozed', 'Handled', 'Closed by rule'].map((f) => <button key={f} aria-pressed={f === filter} onClick={() => setFilter(f)}>{f}</button>)}</div>}>
        Customers still waiting who are off the line without a reply, newest first. The “ended the chat” rule closed <b>{state.split.closedAutomatically}</b> on its own; turn it off in Settings to count them again.
      </Headline>
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Why it left the line</th><th>Customer</th><th>Last message</th><th>What happens next</th><th>Moved by</th><th /></tr></thead><tbody>
          {list.map((s) => (
            <tr key={`${s.accountId}:${s.key}`}>
              <td><Chip tone={s.why === 'Snoozed' ? 'due' : 'neutral'} icon={s.why === 'Snoozed' ? 'snooze' : s.why === 'Handled' ? 'check' : 'more'}>{s.why}</Chip></td>
              <td><b style={{ fontWeight: 600 }}>{s.customer}</b><div className="sub">{s.accountName}</div></td>
              <td className="sub">{s.preview || 'No preview could be read'}</td>
              <td>{s.until ? `Back ${when(s.until)}` : s.next}</td>
              <td className="sub">{s.why === 'Closed by rule' ? 'Automatic' : 'You'}, {when(s.at)}</td>
              <td className="r">{s.why !== 'Closed by rule' && <Btn icon="reopen" onClick={() => bridge.putBack(s.accountId, s.key)}>Put back</Btn>}</td>
            </tr>
          ))}
          {list.length === 0 && <tr><td colSpan={6} className="sub" style={{ padding: 18 }}>{filter === 'All' ? 'Nothing is set aside. Every waiting customer is on the line.' : `No chats are ${filter === 'Closed by rule' ? 'closed by the rule' : filter.toLowerCase()}.`}</td></tr>}
        </tbody></table>
      </div>
      <p className="sub" style={{ margin: 0 }}>
        {state.setAsideTotal > state.setAside.length && <>Showing the newest {state.setAside.length} of {state.setAsideTotal}; {closed} of those were closed by the rule. </>}
        Marks are kept on this PC. A chat put back returns to the line at the position its real wait gives it.
      </p>
    </main>
  );
}

// ---- the morning digest ------------------------------------------------------------------------------------

export function DigestScreen({ state, nav }: ScreenProps) {
  const signedOut = state.accounts.filter((a) => a.signedOut);
  return (
    <main className="main" style={{ gap: 18 }}>
      <Headline sample eyebrow={<span className="phase"><Icon name="sunrise" size={12} /> {new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })}</span>}
        title="Good morning. 14 people wrote while you were closed."
        actions={<Btn icon="line" kind="primary" onClick={() => nav.go('line')}>Go to the line</Btn>}>
        Answer the 3 still owed from yesterday first. Yesterday <b>81%</b> were answered on time across all locations, down from 86% the week before.
      </Headline>
      <div className="grid2" style={{ gridTemplateColumns: 'minmax(0,1.2fr) minmax(0,1fr)' }}>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ padding: '14px 18px 6px' }}><h3 style={{ margin: 0 }}>Owed from yesterday</h3><span className="sub">Wrote before closing and never got a reply</span></div>
          <table className="table"><tbody>{OWED.map((o) => <tr key={o.who}><td><b style={{ fontWeight: 600 }}>{o.who}</b><div className="sub">{o.account} · “{o.message}”</div></td><td className="r late">{o.since}</td></tr>)}</tbody></table>
          <div style={{ padding: '12px 18px', borderTop: '1px solid var(--line)' }}><b style={{ fontWeight: 600 }}>11 wrote overnight.</b> <span className="sub">The wait clock starts at opening time.</span></div>
        </div>
        <Panel title="Yesterday, by location">
          <table className="table"><thead><tr><th>Location</th><th className="r">On time</th><th>Last 14 days</th><th className="r">Median reply</th></tr></thead><tbody>
            {YESTERDAY.map((y) => <tr key={y.location}><td><b style={{ fontWeight: 600 }}>{y.location}</b></td><td className="r" style={{ color: toneInk(y.onTime >= 90 ? 'ok' : 'due'), fontWeight: 600 }}>{y.onTime}%</td><td><Spark values={y.trend} min={60} max={100} /></td><td className="r">{y.median} min</td></tr>)}
          </tbody></table>
          <p className="sub" style={{ margin: '10px 0 0' }}>Aiming for 90%.</p>
        </Panel>
      </div>
      {signedOut.length > 0 && (
        <Panel style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 14, alignItems: 'center' }}>
          <Icon name="lock" size={20} />
          <span><b style={{ fontWeight: 600 }}>{plural(signedOut.length, 'account')} {signedOut.length === 1 ? 'needs' : 'need'} you:</b> <span className="sub">{signedOut.map((a) => a.name).join(', ')}. Their messages are not counted above until signed in.</span></span>
          <Btn onClick={() => nav.open('needs')}>See what to do</Btn>
        </Panel>
      )}
    </main>
  );
}
