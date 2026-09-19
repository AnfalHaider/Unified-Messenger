// Reviews and Reports. Reviews come from the Google reviews reader (channels/google); every report tab,
// the weekly report and its exports read the day records and measured replies.
import { useState } from 'react';
import type { ReportRange, ReportsView, UiState, WeeklyDoc } from '../../app/view-model.ts';
import { dayKey as dayKeyOf } from '../../core/history.ts';
import { REPLY_BANDS } from '../../core/report.ts';
import { Heatmap, Histogram, LineChart } from '../charts.tsx';
import { Icon } from '../icons.tsx';
import { bridge, Btn, Chip, Facts, Headline, Logo, Panel, Seg, Toggle, waitText, type ScreenProps } from '../parts.tsx';

const Stars = ({ n, size = 13 }: { n: number; size?: number }) => (
  <span className="stars" role="img" aria-label={`${n} of 5 stars`}>
    {[1, 2, 3, 4, 5].map((i) => (
      <svg key={i} width={size} height={size} viewBox="0 0 16 16" className={i <= n ? '' : 'off'} aria-hidden="true">
        <path d="M8 2.3l1.7 3.6 3.9.5-2.9 2.7.8 3.9L8 11.1 4.5 13l.8-3.9-2.9-2.7 3.9-.5z" fill="currentColor" />
      </svg>
    ))}
  </span>
);

export function ReviewsScreen({ state, nav }: ScreenProps) {
  const view = state.reviews;
  const [show, setShow] = useState<'needs' | 'all'>('needs');
  const [picked, setPicked] = useState(0);
  if (!view) return <main className="main"><Headline title="Reviews">Gathering the reviews…</Headline></main>;
  if (view.profiles.length === 0) {
    return (
      <main className="main">
        <Headline title="No Google profile yet" actions={<Btn icon="users" kind="primary" onClick={() => nav.open('add-account')}>Add an account</Btn>}>
          Add each location's Google Business profile as a Google Business account, and sign in on its page. The app then reads each
          profile's rating, how many reviews it has, and which recent ones have no reply. It never posts anything.
        </Headline>
      </main>
    );
  }
  const list = show === 'needs' ? view.needing : view.recent;
  const current = list[picked] ?? list[0];
  const unread = view.profiles.filter((p) => p.readAt === null);
  const unanswered = view.needing.length;
  const unhappy = view.needing.filter((r) => r.stars >= 1 && r.stars <= 3).length;
  return (
    <main className="main">
      <Headline title={unread.length === view.profiles.length ? 'Reading your Google profiles' : unanswered ? `${unanswered} recent review${unanswered === 1 ? ' has' : 's have'} no reply` : 'Every recent review has a reply'}
        actions={<Seg label="Show" value={show} onChange={(v) => { setShow(v); setPicked(0); }} options={[['needs', `Needs a reply ${unanswered}`], ['all', 'All recent']] as const} />}>
        {unread.length === view.profiles.length
          ? 'Each profile is read within a few minutes of the app opening, then every half hour, never while its page is on screen.'
          : <>{unhappy > 0 && <b>{unhappy} of them {unhappy === 1 ? 'is' : 'are'} three stars or fewer. </b>}Unhappy ones first, then oldest. Counts cover the latest reviews Google shows on each profile.</>}
      </Headline>
      <div className="grid3">
        {view.profiles.map((p) => (
          <div key={p.accountId} className="panel" style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: 16, alignItems: 'center' }}>
            <div>
              <span className="sub">{p.location}</span>
              <div className="num" style={{ font: '600 40px/1 var(--text)', fontStretch: '80%' }}>{p.rating ?? '—'}</div>
              {p.rating !== null && <Stars n={Math.round(p.rating)} size={12} />}
              <div className="sub num" style={{ marginTop: 4 }}>{p.total !== null ? `${p.total.toLocaleString()} reviews` : p.readAt ? 'Total not read yet' : 'Not read yet'}</div>
            </div>
            <div style={{ display: 'grid', gap: 6 }}>
              {p.signedOut ? <span className="sub">Asking for a Google sign-in. <button className="help-link" onClick={() => nav.go('dock', p.accountId)}>Sign in</button></span>
                : p.loaded > 0 ? (
                  <>
                    <div className="bars5" aria-label={`The latest ${p.loaded} reviews by stars`}>{p.spread.map((c, i) => <div key={i}><span>{5 - i}</span><span className="b"><i style={{ width: `${(c / Math.max(1, ...p.spread)) * 100}%` }} /></span><span className="num" style={{ textAlign: 'right' }}>{c}</span></div>)}</div>
                    <span className="sub">{p.unanswered ? <b className="late">{p.unanswered} without a reply</b> : 'All replied'} · latest {p.loaded}{p.more ? '' : ', all of them'}</span>
                  </>
                ) : <span className="sub">{p.readAt ? 'No reviews on the page.' : 'Reading soon.'}</span>}
            </div>
          </div>
        ))}
      </div>
      {list.length > 0 ? (
        <div className="two" style={{ gridTemplateColumns: 'minmax(0,1fr) 400px' }}>
          <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
            {list.map((r, i) => (
              <div key={r.id} className={`review ${r === current ? 'sel' : ''}`} onClick={() => setPicked(i)} style={{ cursor: 'pointer' }}>
                <div>{r.stars ? <Stars n={r.stars} /> : <span className="sub">Stars not read</span>}<div className="sub" style={{ marginTop: 4 }}>{r.age}</div></div>
                <div><b style={{ fontWeight: 600 }}>{r.reviewer}</b> <span className="sub">· {r.location}</span><p>{r.text || <span className="sub">A rating with no words.</span>}</p></div>
                {r.replied ? <Chip tone="neutral">Replied</Chip> : <Chip tone="late">No reply</Chip>}
              </div>
            ))}
            <div className="sub" style={{ padding: '10px 18px', borderTop: '1px solid var(--line)' }}>The latest reviews on each profile, as Google lists them. Older ones are not read.</div>
          </div>
          {current && (
            <Panel title={`${current.reviewer}, ${current.location}`} style={{ display: 'grid', gap: 10, alignContent: 'start' }}>
              <div>{current.stars ? <Stars n={current.stars} size={16} /> : null} <span className="sub">{current.age}</span></div>
              <div style={{ border: '1px solid var(--line-2)', borderRadius: 10, background: 'var(--raised)', padding: 12, fontSize: 13.5, lineHeight: 1.55 }}>
                {current.text || <span className="sub">This reviewer left a rating and no words.</span>}
              </div>
              <div style={{ display: 'flex', gap: 8 }}><Btn icon="open" kind="primary" onClick={() => nav.go('dock', current.accountId)}>Open on Google</Btn></div>
              <div className="sub" style={{ display: 'flex', gap: 6, alignItems: 'center' }}><Icon name="shield" size={13} />Reply on Google yourself. The app never posts anything; drafted replies arrive with the assistant.</div>
            </Panel>
          )}
        </div>
      ) : (
        <Panel><p className="sub" style={{ margin: 0 }}>{show === 'needs' ? 'Every review the app has read has a reply.' : 'No reviews have been read yet.'}</p></Panel>
      )}
    </main>
  );
}

// ---- reports ------------------------------------------------------------------------------------------------

export const REPORT_TABS = ['Overview', 'Reply times', 'Backlog and reopened', 'Missed calls', 'Weekly report'] as const;
type Tab = typeof REPORT_TABS[number];
const RANGES = [['today', 'Today'], ['week', '7 days'], ['month', '30 days']] as const;
type RangeKey = typeof RANGES[number][0];

const HOUR_LABELS = Array.from({ length: 24 }, (_, h) => `${h % 12 || 12}${h < 12 ? 'a' : 'p'}`);
const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

/** A time said the way a person would: "4:12 pm" today, "Tue 13 Sep, 4:12 pm" otherwise. */
function when(ms: number) {
  const d = new Date(ms);
  const time = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  return d.toDateString() === new Date().toDateString() ? time : `${d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' })}, ${time}`;
}

/** Ticks and a top for a chart of counts, so small numbers are not drawn against an axis of hundreds. */
function countAxis(values: (number | null)[]) {
  const top = Math.max(4, ...values.map((v) => v ?? 0));
  const step = Math.ceil(top / 4);
  return { max: step * 4, ticks: [0, step, 2 * step, 3 * step, 4 * step] };
}

const Empty = ({ children }: { children: React.ReactNode }) => <p className="sub" style={{ margin: 0, padding: '18px 0' }}>{children}</p>;

export function ReportsScreen({ state, nav }: ScreenProps) {
  const tab = (REPORT_TABS as readonly string[]).includes(nav.view.sub) ? nav.view.sub as Tab : 'Overview';
  const [rangeKey, setRange] = useState<RangeKey>('week');
  const [saved, setSaved] = useState('');
  const days = rangeKey === 'today' ? 1 : rangeKey === 'week' ? 7 : 30;
  const bar = (
    <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
      <Seg label="Report" value={tab} onChange={(t) => nav.go('reports', null, t)} options={REPORT_TABS} />
      {tab !== 'Weekly report' && saved && <span className="sub" role="status" style={{ overflowWrap: 'anywhere' }}>{saved}</span>}
      {tab !== 'Weekly report' && (
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <Seg label="Range" value={rangeKey} onChange={setRange} options={RANGES} />
          <Btn icon="export" title="Save this range's figures as CSV" onClick={() => void bridge.exportReport({ format: 'csv', days }).then((r) => setSaved(exportMessage(r)))}>Export</Btn>
        </div>
      )}
    </div>
  );
  if (tab === 'Weekly report') return <Weekly state={state} bar={bar} />;
  const view = state.reports;
  if (!view) return <main className="main"><Headline title="Reports">Gathering the figures…</Headline>{bar}</main>;
  const range = view.ranges[rangeKey];
  const props = { view, range, bar, nav };
  if (tab === 'Reply times') return <ReplyTimes {...props} />;
  if (tab === 'Backlog and reopened') return <Backlog {...props} />;
  if (tab === 'Missed calls') return <Calls {...props} rangeKey={rangeKey} />;

  const r = range.report;
  const trend = r.days.length > 1;
  const busiest = r.busy.flatMap((row, d) => row.map((v, h) => ({ v, d, h }))).sort((a, b) => b.v - a.v)[0];
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline eyebrow={<span className="phase">{view.scope ?? 'All locations'}</span>} title={range.headline}>{range.summary} {range.coverage}</Headline>
      {bar}
      <Facts facts={range.facts} />
      <div className="grid2" style={{ gridTemplateColumns: 'minmax(0,1.1fr) minmax(0,1fr)' }}>
        <Panel title="Answered on time, by location, day by day">
          {!trend ? <Empty>Choose 7 or 30 days to see the trend.</Empty>
            : r.byLocation.every((l) => l.daily.every((v) => v === null)) ? <Empty>No replies measured in this range yet.</Empty>
              : <LineChart labels={range.dayLabels} min={0} max={100} ticks={[0, 25, 50, 75, 100]} unit="%" target={90} targetLabel="Goal 90%"
                series={r.byLocation.map((l, i) => ({ label: l.name, values: l.daily, dash: ['', '6 4', '1.5 3.5'][i % 3] || undefined, nudge: [0, 8, -8][i % 3] }))} height={260} />}
        </Panel>
        <Panel title="When customers write" note={`Customers per hour, averaged per weekday over the recorded days.`}>
          {!busiest?.v ? <Empty>No customers recorded writing in this range yet.</Empty> : <>
            <Heatmap rows={WEEKDAYS} cols={HOUR_LABELS} data={r.busy} />
            <p className="sub" style={{ margin: '6px 0 0' }}>Busiest: {WEEKDAYS[busiest.d]} around {HOUR_LABELS[busiest.h]}.</p>
          </>}
        </Panel>
      </div>
    </main>
  );
}

interface TabProps { view: ReportsView; range: ReportRange; bar: React.ReactNode; nav: ScreenProps['nav'] }

function ReplyTimes({ view, range, bar }: TabProps) {
  const r = range.report;
  const t = r.totals;
  const slowest = r.byAccount.filter((a) => a.p90Minutes !== null).sort((a, b) => (b.p90Minutes ?? 0) - (a.p90Minutes ?? 0))[0];
  const targetIndex = Math.max(1, REPLY_BANDS.findIndex((_, i) => i > 0 && REPLY_BANDS[i - 1][1] >= view.targetMinutes));
  const top = countAxis(r.bands.map(([, n]) => n)).max;
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline title={t.medianMinutes === null ? `${range.label}: no replies measured yet` : `${range.label}: median first reply ${Math.round(t.medianMinutes)} min`}>
        {t.replies ? <>{t.replies} first repl{t.replies === 1 ? 'y' : 'ies'} measured, {t.onTimePercent}% within target.</> : 'Replies are measured going forward from what the app sees happen.'}
        {slowest && <> The slowest one in ten at <b>{slowest.name}</b> took {Math.round(slowest.p90Minutes ?? 0)} min or more.</>} {range.coverage}
      </Headline>
      {bar}
      <div className="grid2">
        <Panel title="First replies by how long they took" note={`${t.replies} repl${t.replies === 1 ? 'y' : 'ies'} in range`}>
          {t.replies ? <Histogram buckets={r.bands} targetIndex={targetIndex} targetLabel={`Target ${view.targetMinutes} min`} max={top} /> : <Empty>No replies measured in this range yet.</Empty>}
        </Panel>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ padding: '14px 18px 4px' }}><h3 style={{ margin: 0 }}>By account</h3></div>
          <table className="table"><thead><tr><th>Account</th><th className="r">Replies</th><th className="r">Median</th><th className="r">On time</th><th className="r">Slowest 1 in 10</th></tr></thead><tbody>
            {r.byAccount.map((a) => {
              const tone = a.onTimePercent === null ? '' : a.onTimePercent >= 90 ? 'ok' : a.onTimePercent >= 80 ? 'due' : 'late';
              return <tr key={a.id}><td><b style={{ fontWeight: 600 }}>{a.name}</b></td><td className="r">{a.replies}</td><td className="r">{a.medianMinutes === null ? '—' : `${Math.round(a.medianMinutes)} min`}</td><td className={`r ${tone}`}>{a.onTimePercent === null ? '—' : `${a.onTimePercent}%`}</td><td className="r">{a.p90Minutes === null ? '—' : `${Math.round(a.p90Minutes)} min`}</td></tr>;
            })}
          </tbody></table>
          <div className="sub" style={{ padding: '12px 18px', borderTop: '1px solid var(--line)' }}>Measured going forward from what the app sees happen, each against its location’s target.</div>
        </div>
      </div>
    </main>
  );
}

function Backlog({ view, range, bar, nav }: TabProps) {
  const r = range.report;
  const trend = r.days.length > 1;
  const backlogAxis = countAxis(r.days.map((d) => d.waitingOverADay));
  const reopenedAxis = countAxis(r.days.map((d) => d.reopened));
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline title={view.backlog.length ? `${view.backlog.length === 20 ? '20 or more' : view.backlog.length} customer${view.backlog.length === 1 ? ' has' : 's have'} waited more than a day` : 'Nobody has waited more than a day'}>
        {range.label}: <b>{r.totals.reopened}</b> chat{r.totals.reopened === 1 ? '' : 's'} reopened after a reply. {range.coverage}
      </Headline>
      {bar}
      <div className="grid2">
        <Panel title="Waiting more than a day" note="Counted at each day’s first read">
          {!trend ? <Empty>Choose 7 or 30 days to see the trend.</Empty>
            : r.days.every((d) => d.waitingOverADay === null) ? <Empty>No morning counts recorded in this range yet.</Empty>
              : <LineChart labels={range.dayLabels} min={0} max={backlogAxis.max} ticks={backlogAxis.ticks} series={[{ label: 'Backlog', values: r.days.map((d) => d.waitingOverADay) }]} height={230} />}
        </Panel>
        <Panel title="Reopened, day by day" note="Customers who wrote again after being answered">
          {!trend ? <Empty>Choose 7 or 30 days to see the trend.</Empty>
            : <LineChart labels={range.dayLabels} min={0} max={reopenedAxis.max} ticks={reopenedAxis.ticks} series={[{ label: 'Reopened', values: r.days.map((d) => (r.recordingSince !== null && d.day >= dayKeyOf(r.recordingSince) ? d.reopened : null)) }]} height={230} />}
        </Panel>
      </div>
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Still waiting</th><th>Account</th><th>Waiting since</th><th>Last message</th><th className="r">Waited, opening hours</th><th /></tr></thead><tbody>
          {view.backlog.map((b) => (
            <tr key={`${b.accountId}:${b.customer}:${b.since}`}>
              <td><b style={{ fontWeight: 600 }}>{b.customer}</b></td><td className="sub">{b.accountName}</td><td>{when(b.since)}</td>
              <td className="sub">{b.preview || 'No preview could be read'}</td><td className="r late">{waitText(b.waited).join(' ')}</td>
              <td className="r"><Btn icon="open" onClick={() => nav.go('dock', b.accountId, b.key)}>Open chat</Btn></td>
            </tr>
          ))}
          {view.backlog.length === 0 && <tr><td colSpan={6} className="sub" style={{ padding: 18 }}>Every waiting customer wrote within the last day.</td></tr>}
        </tbody></table>
      </div>
    </main>
  );
}

function Calls({ view, range, rangeKey, bar, nav }: TabProps & { rangeKey: 'today' | 'week' | 'month' }) {
  const calls = view.calls[rangeKey];
  const notReturned = calls.filter((c) => c.returnedAt === null).length;
  const returned = calls.filter((c) => c.returnedAt !== null);
  const minutes = returned.map((c) => (c.returnedAt! - c.at) / 60_000).sort((x, y) => x - y);
  const median = minutes.length ? Math.round(minutes[Math.ceil(minutes.length / 2) - 1]) : null;
  const locations = [...new Set(calls.map((c) => c.location || 'No location'))];
  const later = (c: typeof calls[number]) => {
    const m = Math.max(1, Math.round((c.returnedAt! - c.at) / 60_000));
    return `${c.returnedBy === 'call' ? 'Called back' : 'Answered by message'} ${waitText(m).join(' ')} later`;
  };
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline title={notReturned ? `${notReturned} missed call${notReturned === 1 ? ' has' : 's have'} not been returned` : calls.length ? 'Every missed call was returned' : 'No missed calls'}>
        {range.label}: {calls.length} missed call{calls.length === 1 ? '' : 's'}{calls.length ? `, ${returned.length} returned` : ''}{median !== null ? `, a median ${waitText(median).join(' ')} later` : ''}. A call counts as returned when a message or call from you follows it. Missed calls are read from WhatsApp only.
      </Headline>
      {bar}
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Caller</th><th>Account</th><th>Called at</th><th>Returned</th><th /></tr></thead><tbody>
          {calls.map((c) => (
            <tr key={`${c.accountId}:${c.key}:${c.at}`}>
              <td><b style={{ fontWeight: 600 }}>{c.customer}</b></td><td className="sub">{c.accountName}</td>
              <td className="num"><Icon name="phone" size={13} /> {when(c.at)}</td>
              <td className={c.returnedAt === null ? 'late' : 'ok'}>{c.returnedAt === null ? 'Not returned' : later(c)}</td>
              <td className="r">{c.returnedAt === null && <Btn icon="open" onClick={() => nav.go('dock', c.accountId, c.key)}>Open chat</Btn>}</td>
            </tr>
          ))}
          {calls.length === 0 && <tr><td colSpan={5} className="sub" style={{ padding: 18 }}>No missed calls were seen in this range.</td></tr>}
        </tbody></table>
      </div>
      {locations.length > 0 && (
        <div className="grid3">
          {locations.map((name) => {
            const here = calls.filter((c) => (c.location || 'No location') === name);
            const back = here.filter((c) => c.returnedAt !== null).length;
            return (
              <div key={name} className="panel" style={{ display: 'grid', gap: 6 }}>
                <span className="sub">{name}, {range.label.toLowerCase()}</span>
                <div className="num" style={{ font: '600 30px/1.1 var(--text)', fontStretch: '80%' }}>{here.length} missed</div>
                <span className="sub">{back} returned, {here.length - back} not</span>
              </div>
            );
          })}
        </div>
      )}
    </main>
  );
}


type Include = UiState['settings']['weeklyReport']['include'];
type Backlog = ReportsView['backlog'];

/** The weekly report page. Drawn in the Weekly report tab and, unchanged, in the hidden window the PDF and the
 *  image are made from, so what is saved is exactly what was on screen. */
export function WeeklyDocument({ doc, include, backlog, scope }: { doc: WeeklyDoc; include: Include; backlog: Backlog; scope: string | null }) {
  const r = doc.report;
  const accounts = r.byAccount.filter((a) => a.replies > 0);
  return (
    <article className="doc" data-weekly-doc="">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', gap: 20 }}>
        <div><span className="sub">Unified Messenger · weekly report · {scope ?? 'all locations'}</span><h1>{doc.title}</h1></div><Logo size={40} />
      </div>
      <p className="lede">{doc.lede}{doc.coverage && <> <span className="sub">{doc.coverage}</span></>}</p>
      {doc.hasData && include.figures && <Facts facts={doc.facts} />}
      {doc.hasData && include.locations && (
        <div><h3>On time, by location</h3>
          {r.byLocation.every((l) => l.daily.every((v) => v === null)) ? <p>No first replies were measured this week.</p>
            : <LineChart labels={doc.dayLabels} min={0} max={100} ticks={[0, 25, 50, 75, 100]} unit="%" target={90} targetLabel="Goal 90%"
              series={r.byLocation.map((l, i) => ({ label: l.name, values: l.daily, dash: ['', '6 4', '1.5 3.5'][i % 3] || undefined, nudge: [0, 8, -8][i % 3] }))} width={716} height={200} />}
        </div>
      )}
      {doc.hasData && (
        <div className="grid2" style={{ gap: 28 }}>
          <div><h3>What to look at</h3>{doc.lookAt.map((s) => <p key={s}>{s}</p>)}</div>
          <div><h3>What went well</h3>{doc.wentWell.map((s) => <p key={s}>{s}</p>)}</div>
        </div>
      )}
      {doc.hasData && include.accounts && accounts.length > 0 && (
        <div><h3>Reply times by account</h3>
          <table className="table"><thead><tr><th>Account</th><th className="r">Replies</th><th className="r">Median</th><th className="r">On time</th><th className="r">Slowest 1 in 10</th></tr></thead><tbody>
            {accounts.map((a) => <tr key={a.id}><td>{a.name}</td><td className="r">{a.replies}</td><td className="r">{a.medianMinutes === null ? '—' : `${Math.round(a.medianMinutes)} min`}</td><td className="r">{a.onTimePercent === null ? '—' : `${a.onTimePercent}%`}</td><td className="r">{a.p90Minutes === null ? '—' : `${Math.round(a.p90Minutes)} min`}</td></tr>)}
          </tbody></table>
        </div>
      )}
      {doc.hasData && include.calls && (
        <div><h3>Missed calls</h3>
          <p>{r.totals.missedCalls ? r.byLocation.filter((l) => l.missedCalls).map((l) => `${l.name}: ${l.missedCalls}`).join(' · ') : 'No missed calls were recorded this week.'}</p>
        </div>
      )}
      {include.names && (
        <div><h3>Waiting more than a day when this report was made</h3>
          {backlog.length ? <table className="table"><tbody>{backlog.map((b) => <tr key={`${b.accountId}:${b.key}`}><td>{b.customer}</td><td className="sub">{b.accountName}</td><td className="r">{waitText(b.waited).join(' ')}</td></tr>)}</tbody></table>
            : <p>Nobody.</p>}
        </div>
      )}
    </article>
  );
}

type ExportResult = Awaited<ReturnType<Window['um']['exportReport']>>;
const exportMessage = (r: ExportResult) =>
  r.error ? `Could not save: ${r.error}` : r.cancelled ? '' : r.copied ? 'Copied. Paste it into a message or a document.' : r.saved ? `Saved to ${r.saved}` : '';

function Weekly({ state, bar }: { state: UiState; bar: React.ReactNode }) {
  const view = state.reports;
  const s = state.settings.weeklyReport;
  const [picked, setWhich] = useState<'this' | 'last' | null>(null);
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(false);
  if (!view) return <main className="main"><Headline title="The weekly report">Gathering the figures…</Headline>{bar}</main>;
  const which = picked ?? (view.weekly.last.hasData ? 'last' : 'this');
  const doc = view.weekly[which];
  const setInclude = (key: keyof Include, on: boolean) => bridge.setSettings({ weeklyReport: { ...s, include: { ...s.include, [key]: on } } });
  const run = async (format: 'pdf' | 'csv' | 'png') => {
    setBusy(true); setStatus('');
    try { setStatus(exportMessage(await bridge.exportReport({ format, week: which }))); } finally { setBusy(false); }
  };
  const parts: [keyof Include, string][] = [['figures', 'Summary and figures'], ['locations', 'On time by location'], ['accounts', 'Reply times by account'], ['calls', 'Missed calls'], ['names', 'Customer names']];
  return (
    <div className="split" style={{ gridTemplateColumns: 'minmax(0,1fr) 300px' }}>
      <main className="main" style={{ gap: 16 }}>
        <Headline title="The weekly report" actions={<Seg label="Week" value={which} onChange={setWhich} options={[['last', 'Last week'], ['this', 'This week']] as const} />}>
          Written for someone who was not watching. Every sentence is computed from the figures; nothing is phrased by a model.
        </Headline>
        {bar}
        <div className="doc-wrap" style={{ overflow: 'visible' }}>
          <WeeklyDocument doc={doc} include={s.include} backlog={view.backlog} scope={view.scope} />
        </div>
      </main>
      <aside className="cust" style={{ gap: 14 }}>
        <h3 style={{ margin: 0, font: '650 17px/1.2 var(--display)' }}>Save or send</h3>
        <div style={{ display: 'grid', gap: 8 }}>
          <Btn icon="export" kind="primary" disabled={busy} onClick={() => void run('pdf')}>Save as PDF</Btn>
          <Btn icon="export" disabled={busy} onClick={() => void run('csv')}>Save figures as CSV</Btn>
          <Btn icon="copy" disabled={busy} onClick={() => void run('png')}>Copy as image</Btn>
        </div>
        {status && <p className="sub" role="status" style={{ margin: 0, overflowWrap: 'anywhere' }}>{status}</p>}
        <div><h4>Includes</h4><div style={{ display: 'grid', gap: 8, fontSize: 13 }}>
          {parts.map(([k, label]) => <label key={k} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>{label}<Toggle label={label} on={s.include[k]} onChange={(v) => setInclude(k, v)} /></label>)}
          <label style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', color: 'var(--ink-3)' }}>Reviews<span className="sub">Not connected yet</span></label>
        </div><p className="sub" style={{ margin: '8px 0 0' }}>Customer names are left out by default, so the report can go to anyone. The CSV never has names.</p></div>
        <div><h4>Every week</h4><div className="srow" style={{ padding: 0, border: 0 }}>
          <span><b>Save last week’s PDF on Monday</b><span>From 10 am, to Documents › Unified Messenger reports, while the app is running</span></span>
          <Toggle label="Save last week’s PDF on Monday" on={s.autoSave} onChange={(v) => bridge.setSettings({ weeklyReport: { ...s, autoSave: v } })} />
        </div></div>
      </aside>
    </div>
  );
}
