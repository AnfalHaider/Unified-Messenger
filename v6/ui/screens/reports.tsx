// Reviews and Reports. Reviews and the weekly report document are sample figures until the Google reviews reader
// and the report export are wired; the other report tabs read the day records and measured replies.
import { useState } from 'react';
import type { ReportRange, ReportsView } from '../../app/view-model.ts';
import { dayKey as dayKeyOf } from '../../core/history.ts';
import { REPLY_BANDS } from '../../core/report.ts';
import { Heatmap, Histogram, LineChart, Spark } from '../charts.tsx';
import { Icon } from '../icons.tsx';
import { Btn, Chip, Facts, Headline, Logo, Panel, Seg, Toggle, waitText, type ScreenProps } from '../parts.tsx';
import { ON_TIME_BY_LOCATION, REVIEW_DRAFT, REVIEW_PROFILES, REVIEWS, WEEK_FACTS, WEEKS } from '../sample.ts';

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
  const bar = (
    <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
      <Seg label="Report" value={tab} onChange={(t) => nav.go('reports', null, t)} options={REPORT_TABS} />
      <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
        {tab !== 'Weekly report' && <Seg label="Range" value={rangeKey} onChange={setRange} options={RANGES} />}
        <Btn icon="export" disabled title="Export is not connected yet">Export</Btn>
      </div>
    </div>
  );
  if (tab === 'Weekly report') return <Weekly bar={bar} />;
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
              <td className="r"><Btn icon="open" onClick={() => nav.go('dock', b.accountId, b.customer)}>Open chat</Btn></td>
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
              <td className="r"><Btn icon="open" onClick={() => nav.go('dock', c.accountId, c.customer)}>Open chat</Btn></td>
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

function Weekly({ bar }: { bar: React.ReactNode }) {
  const [parts, setParts] = useState<Record<string, boolean>>({ 'Summary and figures': true, 'On time by location': true, 'Reply times by account': true, Reviews: true, 'Missed calls': true, 'Customer names': false });
  const [weekly, setWeekly] = useState(true);
  return (
    <div className="split" style={{ gridTemplateColumns: 'minmax(0,1fr) 300px' }}>
      <main className="main" style={{ gap: 16 }}>
        <Headline sample title="The weekly report">Written for someone who was not watching. Every sentence is computed from the figures; nothing is phrased by a model.</Headline>
        {bar}
        <div className="doc-wrap" style={{ overflow: 'visible' }}>
          <article className="doc">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', gap: 20 }}><div><span className="sub">Depilex · Unified Messenger</span><h1>Week of 6 to 12 September</h1></div><Logo size={40} /></div>
            <p className="lede">1,284 customers wrote to the three locations. 84% got a first reply within 15 minutes, two points down on the week before. Men DHA-2 hit the 90% goal; F-11 fell to 77%, almost all between 1 and 3 pm.</p>
            <Facts facts={WEEK_FACTS.filter((f) => ['Answered on time', 'Median first reply', 'Waiting over a day', 'Missed calls'].includes(f.label)).map(({ trend: _t, ...f }) => f)} />
            <div><h3>On time, by location</h3><LineChart labels={WEEKS} min={60} max={100} ticks={[60, 70, 80, 90, 100]} unit="%" target={90} targetLabel="Goal 90%" series={ON_TIME_BY_LOCATION} width={716} height={200} /></div>
            <div className="grid2" style={{ gap: 28 }}>
              <div><h3>What to look at</h3><p>F-11 between 1 and 2 pm: 38 replies took a median 24 minutes. And 3 one- and two-star reviews from this week still have no reply.</p></div>
              <div><h3>What went well</h3><p>Men DHA-2 answered 92% on time, its best week since July, and the backlog is at its lowest this month.</p></div>
            </div>
          </article>
        </div>
      </main>
      <aside className="cust" style={{ gap: 14 }}>
        <h3 style={{ margin: 0, font: '650 17px/1.2 var(--display)' }}>Save or send</h3>
        <div style={{ display: 'grid', gap: 8 }}>
          <Btn icon="export" kind="primary" disabled title="Not connected yet">Save as PDF</Btn>
          <Btn icon="export" disabled title="Not connected yet">Save figures as CSV</Btn>
          <Btn icon="copy" disabled title="Not connected yet">Copy as image</Btn>
        </div>
        <div><h4>Includes</h4><div style={{ display: 'grid', gap: 8, fontSize: 13 }}>
          {Object.entries(parts).map(([k, on]) => <label key={k} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>{k}<Toggle label={k} on={on} onChange={(v) => setParts({ ...parts, [k]: v })} /></label>)}
        </div><p className="sub" style={{ margin: '8px 0 0' }}>Customer names are left out by default, so the report can go to anyone.</p></div>
        <div><h4>Every week</h4><div className="srow" style={{ padding: 0, border: 0 }}><span><b>Prepare on Monday at 10 am</b><span>Saved to Documents</span></span><Toggle label="Prepare every week" on={weekly} onChange={setWeekly} /></div></div>
      </aside>
    </div>
  );
}
