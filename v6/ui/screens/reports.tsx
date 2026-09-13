// Reviews and Reports. Reviews are sample figures until the Google reviews reader is wired; every report tab,
// the weekly report and its exports read the day records and measured replies.
import { useState } from 'react';
import type { ReportRange, ReportsView, UiState, WeeklyDoc } from '../../app/view-model.ts';
import { dayKey as dayKeyOf } from '../../core/history.ts';
import { REPLY_BANDS } from '../../core/report.ts';
import { Heatmap, Histogram, LineChart, Spark } from '../charts.tsx';
import { Icon } from '../icons.tsx';
import { bridge, Btn, Chip, Facts, Headline, Logo, Panel, Seg, Toggle, waitText, type ScreenProps } from '../parts.tsx';
import { REVIEW_DRAFT, REVIEW_PROFILES, REVIEWS } from '../sample.ts';

const Stars = ({ n, size = 13 }: { n: number; size?: number }) => (
  <span className="stars" aria-label={`${n} of 5 stars`}>
    {[1, 2, 3, 4, 5].map((i) => (
      <svg key={i} width={size} height={size} viewBox="0 0 16 16" className={i <= n ? '' : 'off'} aria-hidden="true">
        <path d="M8 2.3l1.7 3.6 3.9.5-2.9 2.7.8 3.9L8 11.1 4.5 13l.8-3.9-2.9-2.7 3.9-.5z" fill="currentColor" />
      </svg>
    ))}
  </span>
);

export function ReviewsScreen(_: ScreenProps) {
  const [show, setShow] = useState<'needs' | 'all'>('needs');
  const [picked, setPicked] = useState(0);
  const list = show === 'needs' ? REVIEWS.filter((r) => !r.replied) : REVIEWS;
  const unanswered = REVIEWS.filter((r) => !r.replied).length;
  const current = list[picked] ?? list[0];
  return (
    <main className="main">
      <Headline sample title={`${unanswered} unhappy reviews have no reply`}
        actions={<Seg label="Show" value={show} onChange={(v) => { setShow(v); setPicked(0); }} options={[['needs', `Needs a reply ${unanswered}`], ['all', 'All reviews']] as const} />}>
        Oldest from 5 days ago at F-11. Replying within a day is what Google shows next to your rating.
      </Headline>
      <div className="grid3">
        {REVIEW_PROFILES.map((p) => (
          <div key={p.location} className="panel" style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: 16, alignItems: 'center' }}>
            <div><span className="sub">{p.location}</span><div className="num" style={{ font: '600 40px/1 var(--text)', fontStretch: '80%' }}>{p.rating}</div><Stars n={Math.round(p.rating)} size={12} /><div className="sub num" style={{ marginTop: 4 }}>{p.total.toLocaleString()} reviews</div></div>
            <div className="bars5">{p.spread.map((c, i) => <div key={i}><span>{5 - i}</span><span className="b"><i style={{ width: `${(c / p.spread[0]) * 100}%` }} /></span><span className="num" style={{ textAlign: 'right' }}>{c}</span></div>)}</div>
          </div>
        ))}
      </div>
      <div className="two" style={{ gridTemplateColumns: 'minmax(0,1fr) 400px' }}>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          {list.map((r, i) => (
            <div key={r.who} className={`review ${r === current ? 'sel' : ''}`} onClick={() => setPicked(i)} style={{ cursor: 'pointer' }}>
              <div><Stars n={r.stars} /><div className="sub" style={{ marginTop: 4 }}>{r.when}</div></div>
              <div><b style={{ fontWeight: 600 }}>{r.who}</b> <span className="sub">· {r.location}</span><p>{r.text}</p></div>
              {r.replied ? <Chip tone="neutral">Replied</Chip> : <Chip tone="late">No reply</Chip>}
            </div>
          ))}
          <div className="sub" style={{ padding: '10px 18px', borderTop: '1px solid var(--line)' }}>Covers the latest reviews the app could read from each profile. Complete history arrives with Google’s own reviews service.</div>
        </div>
        {current && (
          <Panel title={`Reply to ${current.who}`} style={{ display: 'grid', gap: 10, alignContent: 'start' }}>
            <p className="sub" style={{ margin: 0 }}>A draft written on this PC from this review only. Edit it, copy it, and post it on Google yourself.</p>
            <div style={{ border: '1px solid var(--line-2)', borderRadius: 10, background: 'var(--raised)', padding: 12, fontSize: 13.5, lineHeight: 1.55 }}>{REVIEW_DRAFT}</div>
            <div style={{ display: 'flex', gap: 8 }}><Btn icon="copy" kind="primary" onClick={() => void navigator.clipboard?.writeText(REVIEW_DRAFT)}>Copy reply</Btn><Btn icon="open" disabled title="Not connected yet">Open on Google</Btn></div>
            <div className="sub" style={{ display: 'flex', gap: 6, alignItems: 'center' }}><Icon name="shield" size={13} />Nothing is posted by the app.</div>
          </Panel>
        )}
      </div>
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
  if (tab === 'Missed calls') return <Calls {...props} />;

  const r = range.report;
  const trend = r.days.length > 1;
  const busiest = r.busy.flatMap((row, d) => row.map((v, h) => ({ v, d, h }))).sort((a, b) => b.v - a.v)[0];
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline title={range.headline}>{range.summary} {range.coverage}</Headline>
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

function Calls({ view, range, bar, nav }: TabProps) {
  const r = range.report;
  const open = view.unansweredCalls.length;
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline title={open ? `${open} missed call${open === 1 ? ' is' : 's are'} still waiting for an answer` : 'No missed call is waiting for an answer'}>
        {range.label}: {r.totals.missedCalls} missed call{r.totals.missedCalls === 1 ? '' : 's'} recorded. Whether a call was returned is not tracked yet. {range.coverage}
      </Headline>
      {bar}
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Caller</th><th>Account</th><th>Called at</th><th /></tr></thead><tbody>
          {view.unansweredCalls.map((c) => (
            <tr key={`${c.accountId}:${c.customer}:${c.at}`}>
              <td><b style={{ fontWeight: 600 }}>{c.customer}</b></td><td className="sub">{c.accountName}</td>
              <td className="num"><Icon name="phone" size={13} /> {when(c.at)}</td>
              <td className="r"><Btn icon="open" onClick={() => nav.go('dock', c.accountId, c.key)}>Open chat</Btn></td>
            </tr>
          ))}
          {open === 0 && <tr><td colSpan={4} className="sub" style={{ padding: 18 }}>Nobody whose last message was a missed call is waiting now.</td></tr>}
        </tbody></table>
      </div>
      <div className="grid3">
        {r.byLocation.map((l) => (
          <div key={l.name} className="panel" style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 6, alignItems: 'end' }}>
            <span><span className="sub">{l.name}, {range.label.toLowerCase()}</span><div className="num" style={{ font: '600 30px/1.1 var(--text)', fontStretch: '80%' }}>{l.missedCalls} missed</div></span>
            {l.missedDaily.length > 1 && <Spark values={l.missedDaily} width={110} height={34} max={Math.max(3, ...l.missedDaily)} />}
          </div>
        ))}
      </div>
    </main>
  );
}

type Include = UiState['settings']['weeklyReport']['include'];
type Backlog = ReportsView['backlog'];

/** The weekly report page. Drawn in the Weekly report tab and, unchanged, in the hidden window the PDF and the
 *  image are made from, so what is saved is exactly what was on screen. */
export function WeeklyDocument({ doc, include, backlog }: { doc: WeeklyDoc; include: Include; backlog: Backlog }) {
  const r = doc.report;
  const accounts = r.byAccount.filter((a) => a.replies > 0);
  return (
    <article className="doc" data-weekly-doc="">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', gap: 20 }}>
        <div><span className="sub">Unified Messenger · weekly report</span><h1>{doc.title}</h1></div><Logo size={40} />
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
          <WeeklyDocument doc={doc} include={s.include} backlog={view.backlog} />
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
