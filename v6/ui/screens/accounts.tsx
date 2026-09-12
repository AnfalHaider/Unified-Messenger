// Accounts and readers: the grid of every location against every channel, one account in figures, a reader
// that stopped working, and the record of a lost login. The grid and the figures read the real view model;
// the two timelines are sample figures until the shell keeps those records on screen.
import type { UiState } from '../../app/view-model.ts';
import { DayBars, toneInk } from '../charts.tsx';
import { channelIcon, Icon, type IconName } from '../icons.tsx';
import { bridge, Btn, Check, Chip, Facts, Headline, Panel, plural, type ScreenProps, Timeline } from '../parts.tsx';
import { LOST_LOGIN, READER_TIMELINE } from '../sample.ts';

type Column = { key: string; name: string; icon: IconName; channels: string[] };
const COLUMNS: Column[] = [
  { key: 'whatsapp', name: 'WhatsApp', icon: 'chat', channels: ['whatsapp', 'whatsappbusiness'] },
  { key: 'instagram', name: 'Instagram', icon: 'ig', channels: ['instagram'] },
  { key: 'google', name: 'Google reviews', icon: 'star', channels: ['googlebusiness'] },
];

const moduleFor = (state: UiState, channel: string) =>
  state.modules.find((m) => m.id === channel || (channel === 'whatsappbusiness' && m.id === 'whatsapp'));

export function AccountsScreen({ state, nav }: ScreenProps) {
  const locations = [...new Set(state.accounts.map((a) => a.location || 'No location'))];
  const others = state.accounts.filter((a) => !COLUMNS.some((c) => c.channels.includes(a.channel)));
  const columns = others.length ? [...COLUMNS, { key: 'other', name: 'Other pages', icon: 'more' as IconName, channels: [...new Set(others.map((a) => a.channel))] }] : COLUMNS;
  const reading = state.accounts.filter((a) => a.waiting !== null && !a.signedOut).length;
  const signIn = state.accounts.filter((a) => a.signedOut).length;
  const noReader = state.accounts.filter((a) => a.waiting === null && !a.signedOut).length;

  return (
    <main className="main">
      <Headline title="Accounts" actions={<><Btn icon="refresh" onClick={() => bridge.readNow()}>Read all now</Btn><Btn icon="users" kind="primary" onClick={() => nav.open('add-account')}>Add an account</Btn></>}>
        {plural(state.accounts.length, 'account')} at {plural(locations.length, 'location')}. <b>{reading} reading</b>
        {signIn > 0 && <>, <b className="late">{signIn} need signing in</b></>}{noReader > 0 && <>, {noReader} with no reader yet</>}.
      </Headline>
      <div className="board-grid" style={{ gridTemplateColumns: `170px repeat(${columns.length}, minmax(0, 1fr))` }}>
        <div className="bg-h" />
        {columns.map((c) => <div key={c.key} className="bg-h" style={{ borderLeft: '1px solid var(--line)' }}><Icon name={c.icon} size={15} />{c.name}</div>)}
        {locations.map((location) => {
          const here = state.accounts.filter((a) => (a.location || 'No location') === location);
          const waiting = here.reduce((n, a) => n + (a.waiting ?? 0), 0);
          return [
            <div key={location} className="bg-loc"><b>{location}</b><span>{waiting ? `${waiting} waiting` : 'nobody waiting'}</span></div>,
            ...columns.map((c) => {
              const inCell = here.filter((a) => c.channels.includes(a.channel));
              if (!inCell.length) return <div key={location + c.key} className="cell off"><p>No account here.</p></div>;
              return (
                <div key={location + c.key} style={{ display: 'grid', borderTop: '1px solid var(--line)', borderLeft: '1px solid var(--line)' }}>
                  {inCell.map((a) => {
                    const module = moduleFor(state, a.channel);
                    const broken = module && module.tone === 'late';
                    const kind = a.signedOut ? 'warn' : a.waiting === null ? 'off' : '';
                    const [dot, label] = a.signedOut ? ['var(--m-due)', 'Sign in needed'] : broken ? ['var(--m-late)', 'Reader not working'] : a.waiting === null ? ['var(--line-2)', 'Open, no figures'] : ['var(--m-ok)', a.asleep ? 'Asleep' : 'Reading'];
                    return (
                      <div key={a.id} className={`cell ${kind}`} style={{ borderTop: 0, borderLeft: 0 }}>
                        <span className="state"><i style={{ background: dot }} />{label}</span>
                        {inCell.length > 1 && <b style={{ fontWeight: 600, fontSize: 13 }}>{a.name}</b>}
                        {a.signedOut ? <p>The page is asking for a login, so its customers are not being counted.</p>
                          : a.waiting === null ? <p>This channel has no reader yet, so it shows no figures rather than zeroes.</p>
                            : <span className="big num">{a.waiting}<small>waiting</small></span>}
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                          <Btn icon={a.signedOut ? 'qr' : 'open'} kind={a.signedOut ? 'primary' : undefined} onClick={() => nav.go('dock', a.id)}>{a.signedOut ? 'Sign in' : 'Open page'}</Btn>
                          {a.waiting !== null && !a.signedOut && <Btn kind="quiet" onClick={() => nav.go('account-detail', a.id)}>Figures</Btn>}
                          {a.signedOut && <Btn kind="quiet" onClick={() => nav.go('lost-login', a.id)}>What happened</Btn>}
                        </div>
                      </div>
                    );
                  })}
                </div>
              );
            }),
          ];
        })}
      </div>
      <div className="readers">
        {state.modules.map((m) => (
          <button key={m.id} className="reader" style={{ textAlign: 'left' }} onClick={() => nav.go('reader', null, m.id)}>
            <Icon name={channelIcon(m.id)} size={18} />
            <span><b>{m.name} reader</b><br /><span className="sub">{m.detail}</span></span>
            <Chip tone={m.tone} icon={m.tone === 'ok' ? 'check' : m.tone === 'late' ? 'alert' : undefined}>{m.status}</Chip>
          </button>
        ))}
        {state.modules.length === 0 && <p className="sub">None of these accounts is on a channel with a reader yet.</p>}
      </div>
    </main>
  );
}

export function AccountDetailScreen({ state, nav }: ScreenProps) {
  const d = state.detail;
  if (!d) return <main className="main"><Headline title="Choose an account">Open an account from Accounts to see its figures.</Headline></main>;
  return (
    <main className="main">
      <Headline title={d.name} actions={<>
        <Btn icon="open" onClick={() => nav.go('dock', d.id)}>Open page</Btn>
        <Btn icon="refresh" onClick={() => bridge.readNow()}>Read now</Btn>
        <Btn icon="sleep" kind="quiet" onClick={() => bridge.sleepAccount(d.id)}>Sleep</Btn>
      </>}>
        {d.location || 'No location'} · {d.signedOut ? 'signed out' : 'signed in on this PC'} · {d.freshness.text.toLowerCase()}
      </Headline>
      {d.reads ? <Facts facts={d.figures} /> : <Panel><p className="sub" style={{ margin: 0 }}>{d.health[0]?.detail}</p></Panel>}
      {d.reads && (
        <div className="two">
          <Panel title="First reply, last 7 days" note={`Median minutes per day, opening hours only, against the ${d.targetMinutes}-minute target.`}>
            {d.daily.some((x) => x.count) ? <DayBars data={d.daily} target={d.targetMinutes} /> : <p className="sub">No replies measured yet. Reply times are measured going forward, from what the app sees happen.</p>}
          </Panel>
          <Panel title="Is it being read?">
            <div className="checks">
              {d.health.map((h) => <Check key={h.title} tone={h.tone} icon={h.tone === 'ok' ? 'check' : h.tone === 'late' ? 'alert' : h.tone === 'due' ? 'clock' : 'lock'} title={h.title}>{h.detail}</Check>)}
            </div>
          </Panel>
        </div>
      )}
      {d.queue.length > 0 && (
        <div className="queue">
          {d.queue.map((r) => (
            <div key={r.customer + r.waited} className={`row ${r.tone}`} onClick={() => nav.go('dock', d.id, r.customer)} style={{ gridTemplateColumns: '96px minmax(0,1fr) 200px' }}>
              <span className={`wait ${r.tone}`}>{r.waited}<small>min</small></span>
              <span className="who"><b>{r.customer}</b><span>{r.preview}</span></span>
              <span className={`status ${r.tone}`}>{r.status}</span>
            </div>
          ))}
        </div>
      )}
    </main>
  );
}

export function ReaderScreen({ state, nav }: ScreenProps) {
  const m = state.modules.find((x) => x.id === nav.view.sub) ?? state.modules.find((x) => x.tone === 'late') ?? state.modules[0];
  const others = state.modules.filter((x) => x !== m);
  const broken = m?.tone === 'late';
  return (
    <main className="main">
      <Headline sample={!broken} title={m ? (broken ? `The ${m.name} reader stopped working` : `The ${m.name} reader`) : 'Channel readers'}
        actions={<><Btn icon="refresh" onClick={() => bridge.readNow()}>Try again now</Btn><Btn icon="export" kind="primary" disabled title="Support reports are not connected yet">Save a report for support</Btn></>}>
        {m ? <>{m.detail}. {others.length > 0 && <>{others.map((o) => o.name).join(' and ')} {others.length === 1 ? 'is' : 'are'} checked separately.</>}</> : 'No account is on a channel with a reader yet.'}
      </Headline>
      <div className="grid2" style={{ gridTemplateColumns: 'minmax(0,1.25fr) minmax(0,1fr)' }}>
        <Panel title="What happened" note="When a page changes shape, every account on that channel fails at once, so it is reported once, as the reader."><Timeline items={READER_TIMELINE} /></Panel>
        <Panel title="Every reader">
          <div className="checks">
            {state.modules.map((x) => <Check key={x.id} tone={x.tone} icon={x.tone === 'ok' ? 'check' : x.tone === 'late' ? 'alert' : 'clock'} title={`${x.name}: ${x.status}`}>{x.detail}</Check>)}
          </div>
        </Panel>
      </div>
    </main>
  );
}

export function LostLoginScreen({ state, nav }: ScreenProps) {
  const a = state.accounts.find((x) => x.id === nav.view.accountId);
  return (
    <main className="main">
      <Headline sample title={`${a?.name ?? 'This account'} ${a?.signedOut ? 'is signed out' : 'lost its login'}`}
        actions={<Btn icon="qr" kind="primary" onClick={() => a && nav.go('dock', a.id)}>Sign in again</Btn>}>
        Nobody signed it out from this app. The usual cause is the phone removing this PC under <b>Linked devices</b>, or WhatsApp ending a link that had not been used from the phone for 14 days.
      </Headline>
      <div className="grid2" style={{ gridTemplateColumns: 'minmax(0,1.3fr) minmax(0,1fr)' }}>
        <Panel title="Just before it happened"><Timeline items={LOST_LOGIN} /></Panel>
        <div style={{ display: 'grid', gap: 16, alignContent: 'start' }}>
          <Panel title="Check on the phone">
            <ol style={{ margin: 0, paddingLeft: 18, fontSize: 13.5, display: 'grid', gap: 6 }}>
              <li>WhatsApp › Settings › Linked devices.</li><li>If this PC is missing, it was removed: scan the new QR code here.</li><li>If it is listed, remove it, then scan again.</li>
            </ol>
          </Panel>
          <Panel title="Where the login lives"><p className="sub" style={{ margin: 0, color: toneInk('neutral') }}>In this account’s own session on this PC. It is never copied to the workspace or to another PC.</p></Panel>
        </div>
      </div>
    </main>
  );
}
