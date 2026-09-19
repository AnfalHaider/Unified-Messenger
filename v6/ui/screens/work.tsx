// The day's work: the line, a chat docked beside it, what was set aside, and the morning digest.
// All of it reads the real view model, the customer panel included; only the suggested reply is still sample,
// until the assistant is wired in Phase 5.
import { useEffect, useMemo, useRef, useState } from 'react';
import type { QueueRow, UiState } from '../../app/view-model.ts';
import { byChannel, Spark, TheLine, toneInk, waitLabel } from '../charts.tsx';
import { channelIcon, Icon } from '../icons.tsx';
import { bridge, Btn, Chip, Facts, Headline, isPreview, Panel, plural, Sample, type ScreenProps, Wait, waitText, type Fact } from '../parts.tsx';

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

/** The indicators above the line. Counted from the rows on screen, so switching location changes them too;
 *  today's two measured figures are the whole business either way, and say so when a location is chosen. */
function lineFacts(state: UiState, rows: QueueRow[], scope: string, target: number): Fact[] {
  const late = rows.filter((r) => r.tone === 'late').length;
  const due = rows.filter((r) => r.tone === 'due').length;
  const longest = rows[0];
  const onTime = state.figures.find((f) => f.label === 'Answered on time');
  const firstReply = state.figures.find((f) => f.label === 'First reply');
  const caughtUp = state.figures.find((f) => f.label === 'Caught up');
  const everywhere = scope === 'All' ? '' : ', across every location';
  const [longestValue, longestUnit] = longest ? waitText(longest.waited) : ['—', ''];
  return [
    { label: 'Waiting now', value: String(rows.length), unit: rows.length === 1 ? 'customer' : 'customers',
      note: byChannel(rows).map((c) => `${c.count} ${c.name}`).join(' · ') || 'Nobody is waiting',
      tone: late ? 'late' : rows.length ? 'due' : 'ok' },
    { label: 'Past target', value: String(late), unit: `over ${target} min`,
      note: due ? `${due} more in the next 5 min` : 'None due in the next few minutes', tone: late ? 'late' : 'ok' },
    { label: 'Longest wait', value: longestValue, unit: longestUnit,
      note: longest ? `${longest.customer} · ${longest.accountName}` : 'Nobody is waiting',
      tone: longest ? longest.tone : 'ok' },
    { label: 'Caught up', value: caughtUp?.value ?? '—', unit: '%', note: `${caughtUp?.note ?? ''}${everywhere}`, tone: caughtUp?.tone ?? 'neutral' },
    { label: 'Answered on time', value: onTime?.value ?? '—', unit: '%', note: `${onTime?.note ?? ''}${everywhere}`, tone: onTime?.tone ?? 'neutral' },
    { label: 'First reply', value: firstReply?.value ?? '—', unit: 'min median', note: `${firstReply?.note ?? 'No replies measured yet'}${everywhere}`, tone: firstReply?.tone ?? 'neutral' },
  ];
}

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
      if (e.key === 'Enter' && rows[i]) nav.go('dock', rows[i].accountId, rows[i].key);
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
          {onTime && onTime.value !== '—' && <> Today <b>{onTime.value}%</b> of measured replies were within target.</>}
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
        {onTime && onTime.value !== '—' && firstReply && <> Today {onTime.value}% of measured replies were within target, with a median first reply of {firstReply.value} minutes.</>}
      </Headline>
      <Facts facts={lineFacts(state, rows, scope, target)} />
      <TheLine rows={rows} locations={lanes} target={target} selected={selected}
        onSelect={(r) => setSelected(rowKey(r))} onOpen={(r) => { setSelected(rowKey(r)); nav.go('dock', r.accountId, r.key); }} />
      <div className="work">
        <div className="qhead"><strong>Longest wait first</strong><span>{plural(total, 'customer')}</span>
          <span className="keys"><kbd>J</kbd><kbd>K</kbd> move <kbd>Enter</kbd> open chat <kbd>H</kbd> handled <kbd>S</kbd> snooze 1 hour</span></div>
        <div className="queue">
          {rows.map((r) => {
            const key = rowKey(r);
            const sel = key === selected;
            return (
              <div key={key} className={`row ${r.tone} ${sel ? 'sel' : ''}`} onClick={() => setSelected(key)} onDoubleClick={() => nav.go('dock', r.accountId, r.key)}>
                <Wait minutes={r.waited} tone={r.tone} />
                <span className="who"><b>{r.customer}</b><span>{r.preview || 'No preview could be read'}</span></span>
                <span className="acct"><Icon name={channelIcon(r.channel)} size={15} /><span>{r.accountName}</span></span>
                {sel ? (
                  <div className="row-actions">
                    <Btn icon="open" kind="primary" onClick={() => nav.go('dock', r.accountId, r.key)}>Open chat</Btn>
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
  const customer = rows.find((r) => r.accountId === nav.view.accountId && r.key === nav.view.sub)
    ?? rows.find((r) => r.accountId === nav.view.accountId);
  const [panel, setPanel] = useState<'customer' | 'reply'>('customer');
  const late = rows.filter((r) => r.tone === 'late').length;
  const due = rows.filter((r) => r.tone === 'due').length;
  const target = state.settings.slaMinutes;

  // Setting the docked customer aside moves the dock on to the next one waiting, or back to the line when none is.
  const act = (how: 'handled' | 'snooze') => {
    if (!customer) return;
    const next = takeOffLine(rows, customer, how);
    if (next) nav.go('dock', next.accountId, next.key);
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
            <div key={rowKey(r)} className={`row ${r.tone} ${customer && rowKey(customer) === rowKey(r) ? 'sel' : ''}`} onClick={() => nav.go('dock', r.accountId, r.key)}>
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
            <Btn icon="x" kind="quiet" disabled={!customer} title="Staff, the team's own number, a supplier: never counted again, until put back from Set aside"
              onClick={() => {
                if (!customer) return;
                bridge.notCustomer(customer.accountId, customer.key);
                const next = rows.filter((r) => !(r.accountId === customer.accountId && r.key === customer.key))[0];
                if (next) nav.go('dock', next.accountId, next.key); else nav.go('line');
              }}>Not a customer</Btn>
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
            {panel === 'reply' && <Sample />}
            {panel === 'customer' && customer
              ? <CustomerPanel state={state} accountId={customer.accountId} chatKey={customer.key} name={customer.customer} />
              : panel === 'customer'
                ? <p className="sub">Choose a customer from the list to see what the app has seen of them.</p>
                : <ReplyPanel name={customer?.customer ?? 'this customer'} />}
          </aside>
        </div>
      </section>
    </div>
  );
}

/** The panel beside the docked chat. The note and the tags are the owner's, kept on this PC; the lines under
 *  "Seen by the app" are only what the reads saw. Saved replies are copied by hand: the app never sends. */
function CustomerPanel({ state, accountId, chatKey, name }: { state: UiState; accountId: string; chatKey: string; name: string }) {
  const card = state.customer?.byKey[chatKey];
  const saved = state.settings.savedReplies;
  const stored = card?.note ?? '';
  const [note, setNote] = useState(stored);
  const [adding, setAdding] = useState(false);
  const [tag, setTag] = useState('');
  // What the owner is typing is theirs until they leave the field, but the stored note has to be adopted when
  // it arrives or changes — the first state push can land after this panel is already on screen.
  const lastStored = useRef(stored);
  useEffect(() => {
    if (stored !== lastStored.current) { lastStored.current = stored; setNote(stored); }
  }, [stored]);
  // A different chat is a different note.
  useEffect(() => { lastStored.current = stored; setNote(stored); setAdding(false); setTag(''); }, [accountId, chatKey]);
  const save = (text: string) => { if (text !== (card?.note ?? '')) bridge.setNote(accountId, chatKey, text); };
  const toggle = (t: string) => { bridge.toggleTag(accountId, chatKey, t); setTag(''); setAdding(false); };
  const suggestions = (state.customer?.suggestions ?? []).filter((t) => !(card?.tags ?? []).some((have) => have.toLowerCase() === t.toLowerCase()));

  return (
    <>
      <div><h4>Seen by the app</h4>
        {card
          ? <div className="history">{card.seen.map((row) => <div key={`${row.label}:${row.value}`}><span>{row.label}</span><span>{row.value}</span></div>)}</div>
          : <p className="sub" style={{ margin: 0 }}>Nothing has been recorded for {name} yet. Reads from now on are.</p>}
      </div>
      <div><h4>Tags</h4>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
          {(card?.tags ?? []).map((t) => (
            <button key={t} className="pill-tag" aria-label={`Remove the tag ${t}`} title={`Remove the tag ${t}`} onClick={() => toggle(t)}>
              <Icon name="tag" size={11} />{t}<Icon name="x" size={10} />
            </button>
          ))}
          {adding ? (
            <input autoFocus value={tag} placeholder="Tag, then Enter" aria-label="New tag" maxLength={24}
              onChange={(e) => setTag(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter' && tag.trim()) toggle(tag); if (e.key === 'Escape') { setAdding(false); setTag(''); } }}
              onBlur={() => { if (!tag.trim()) setAdding(false); }}
              style={{ height: 26, width: 130, border: '1px solid var(--line-2)', borderRadius: 999, background: 'var(--raised)', padding: '0 10px', font: 'inherit', fontSize: 12, color: 'var(--ink)' }} />
          ) : <button className="pill-tag" style={{ color: 'var(--ink-3)' }} onClick={() => setAdding(true)}>+ Add</button>}
        </div>
        {adding && suggestions.length > 0 && (
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 6 }}>
            {suggestions.slice(0, 6).map((t) => <button key={t} className="pill-tag" style={{ color: 'var(--ink-2)' }} onClick={() => toggle(t)}>{t}</button>)}
          </div>
        )}
      </div>
      <div><h4>Note, kept on this PC</h4>
        <textarea value={note} aria-label={`Note about ${name}`} rows={4} maxLength={2000}
          placeholder="Anything worth remembering next time they write."
          onChange={(e) => setNote(e.target.value)} onBlur={() => save(note)}
          style={{ width: '100%', boxSizing: 'border-box', resize: 'vertical', fontSize: 13, lineHeight: 1.5, padding: '9px 10px', border: '1px solid var(--line)', borderRadius: 8, background: 'var(--raised)', color: 'var(--ink)', font: 'inherit' }} />
        <span className="sub" style={{ fontSize: 11.5 }}>Saved when you click away. Never sent anywhere, and deleted with the account.</span>
      </div>
      <div><h4>Saved replies</h4>
        {saved.length > 0
          ? <div className="saved">{saved.map((s) => (
            <div key={s.title}><span><b>{s.title}</b>{s.body}</span><Btn icon="copy" onClick={() => void navigator.clipboard?.writeText(s.body)}>Copy</Btn></div>
          ))}</div>
          : <p className="sub" style={{ margin: 0 }}>None yet. Keep the sentences you type often in Settings › Saved replies, and copy them from here.</p>}
      </div>
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
        <div className="seg" role="group" aria-label="Show">{['All', 'Snoozed', 'Handled', 'Closed by rule', 'Not a customer'].map((f) => <button key={f} aria-pressed={f === filter} onClick={() => setFilter(f)}>{f}</button>)}</div>}>
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
              <td className="sub">{s.canPutBack ? 'You' : 'Automatic'}, {when(s.at)}</td>
              <td className="r">{s.canPutBack && <Btn icon="reopen" onClick={() => bridge.putBack(s.accountId, s.key)}>Put back</Btn>}</td>
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
  const d = state.digest;
  const eyebrow = <span className="phase"><Icon name="sunrise" size={12} /> {new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })}</span>;
  if (!d) return <main className="main"><Headline eyebrow={eyebrow} title="The morning digest">Gathering the figures…</Headline></main>;
  const since = (ms: number) => {
    const at = new Date(ms);
    const time = at.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
    return at.toDateString() === new Date().toDateString() ? `since ${time}` : `since ${at.toLocaleDateString(undefined, { weekday: 'short' })} ${time}`;
  };
  return (
    <main className="main" style={{ gap: 18 }}>
      <Headline eyebrow={eyebrow} title={d.title} actions={<Btn icon="line" kind="primary" onClick={() => nav.go('line')}>Go to the line</Btn>}>
        {d.summary}
      </Headline>
      <div className="grid2" style={{ gridTemplateColumns: 'minmax(0,1.2fr) minmax(0,1fr)' }}>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ padding: '14px 18px 6px' }}><h3 style={{ margin: 0 }}>Still owed a reply</h3><span className="sub">{d.owedLabel}</span></div>
          <table className="table"><tbody>
            {d.owed.map((o) => (
              <tr key={`${o.accountId}:${o.key}`}>
                <td><b style={{ fontWeight: 600 }}>{o.customer}</b><div className="sub">{o.accountName}{o.preview ? <> · “{o.preview}”</> : null}</div></td>
                <td className="r late">{since(o.since)}</td>
                <td className="r"><Btn icon="open" onClick={() => nav.go('dock', o.accountId, o.key)}>Open chat</Btn></td>
              </tr>
            ))}
            {d.owed.length === 0 && <tr><td className="sub" style={{ padding: 18 }}>Nobody from before is still waiting.</td></tr>}
          </tbody></table>
          {d.owedTotal > d.owed.length && <div className="sub" style={{ padding: '0 18px 10px' }}>And {d.owedTotal - d.owed.length} more on the line.</div>}
          <div style={{ padding: '12px 18px', borderTop: '1px solid var(--line)' }}><b style={{ fontWeight: 600 }}>{plural(d.overnight, 'customer')} wrote since.</b> <span className="sub">{d.overnightNote}</span></div>
        </div>
        <Panel title="Yesterday, by location">
          {d.yesterday.every((y) => y.replies === 0) ? <p className="sub" style={{ margin: 0 }}>No first replies were measured yesterday.</p> : <>
            <table className="table"><thead><tr><th>Location</th><th className="r">On time</th><th>Last 14 days</th><th className="r">Median reply</th></tr></thead><tbody>
              {d.yesterday.map((y) => (
                <tr key={y.name}>
                  <td><b style={{ fontWeight: 600 }}>{y.name}</b></td>
                  <td className="r" style={{ color: y.onTimePercent === null ? undefined : toneInk(y.onTimePercent >= 90 ? 'ok' : y.onTimePercent >= 80 ? 'due' : 'late'), fontWeight: 600 }}>{y.onTimePercent === null ? '—' : `${y.onTimePercent}%`}</td>
                  <td>{y.trend.length > 1 ? <Spark values={y.trend} min={0} max={100} /> : <span className="sub">Not enough days yet</span>}</td>
                  <td className="r">{y.medianMinutes === null ? '—' : `${Math.round(y.medianMinutes)} min`}</td>
                </tr>
              ))}
            </tbody></table>
            <p className="sub" style={{ margin: '10px 0 0' }}>Aiming for 90%.</p>
          </>}
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
