// Reviews and Reports. Both are sample figures until the Google reviews reader and the reporting store are
// wired; the layouts, charts and wording are final.
import { useState } from 'react';
import { Heatmap, Histogram, LineChart, Spark } from '../charts.tsx';
import { Icon } from '../icons.tsx';
import { Btn, Chip, Facts, Headline, Panel, Seg, Toggle, type ScreenProps } from '../parts.tsx';
import {
  BACKLOG, BACKLOG_TREND, BUSY, CALLS, CALLS_BY_LOCATION, DAYS, HOURS, ON_TIME_BY_LOCATION, REOPENED, REPLY_BUCKETS,
  REPLY_BY_ACCOUNT, REVIEW_DRAFT, REVIEW_PROFILES, REVIEWS, WEEK_FACTS, WEEKS,
} from '../sample.ts';

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

export function ReportsScreen({ nav }: ScreenProps) {
  const tab = (REPORT_TABS as readonly string[]).includes(nav.view.sub) ? nav.view.sub as Tab : 'Overview';
  const [range, setRange] = useState('7 days');
  const bar = (
    <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
      <Seg label="Report" value={tab} onChange={(t) => nav.go('reports', null, t)} options={REPORT_TABS} />
      <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
        <Seg label="Range" value={range} onChange={setRange} options={['Today', '7 days', '30 days', 'Custom']} />
        <Btn icon="export" disabled title="Export is not connected yet">Export</Btn>
      </div>
    </div>
  );
  if (tab === 'Reply times') return <ReplyTimes bar={bar} />;
  if (tab === 'Backlog and reopened') return <Backlog bar={bar} />;
  if (tab === 'Missed calls') return <Calls bar={bar} />;
  if (tab === 'Weekly report') return <Weekly bar={bar} />;
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline sample title="Last 7 days: slower at F-11 in the afternoons">84% answered on time, 2 points below the week before. The drop is almost all F-11 between 1 and 3 pm.</Headline>
      {bar}
      <Facts facts={WEEK_FACTS} />
      <div className="grid2" style={{ gridTemplateColumns: 'minmax(0,1.1fr) minmax(0,1fr)' }}>
        <Panel title="Answered on time, by location, week by week">
          <LineChart labels={WEEKS} min={60} max={100} ticks={[60, 70, 80, 90, 100]} unit="%" target={90} targetLabel="Goal 90%" series={ON_TIME_BY_LOCATION} height={260} />
        </Panel>
        <Panel title="When customers write" note="Messages per hour, averaged over 4 weeks. Darker is busier.">
          <Heatmap rows={DAYS} cols={HOURS} data={BUSY} />
          <p className="sub" style={{ margin: '6px 0 0' }}>Busiest: Saturday and Sunday, 3 to 5 pm. Weekdays peak at 1 to 2 pm.</p>
        </Panel>
      </div>
    </main>
  );
}

function ReplyTimes({ bar }: { bar: React.ReactNode }) {
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline sample title="Most replies are quick. The slow ones are very slow.">Median first reply <b>11 min</b>, but the slowest one in ten took <b className="late">28 min or more</b>. Instagram is slower than WhatsApp at every location.</Headline>
      {bar}
      <div className="grid2">
        <Panel title="First replies by how long they took" note="486 replies in the last 7 days, counted in opening hours">
          <Histogram buckets={REPLY_BUCKETS} targetIndex={3} targetLabel="Target 15 min" max={180} />
        </Panel>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ padding: '14px 18px 4px' }}><h3 style={{ margin: 0 }}>By account</h3></div>
          <table className="table"><thead><tr><th>Account</th><th className="r">Replies</th><th className="r">Median</th><th className="r">On time</th><th className="r">Slowest 1 in 10</th></tr></thead><tbody>
            {REPLY_BY_ACCOUNT.map((a) => <tr key={a.account}><td><b style={{ fontWeight: 600 }}>{a.account}</b></td><td className="r">{a.replies}</td><td className="r">{a.median} min</td><td className={`r ${a.tone}`}>{a.onTime}%</td><td className={`r ${a.slowest > 30 ? 'late' : ''}`}>{a.slowest} min</td></tr>)}
          </tbody></table>
          <div className="sub" style={{ padding: '12px 18px', borderTop: '1px solid var(--line)' }}>Replies are measured going forward from what the app sees happen. Opening hours and holidays pause the clock.</div>
        </div>
      </div>
    </main>
  );
}

function Backlog({ bar }: { bar: React.ReactNode }) {
  const labels = BACKLOG_TREND.map((_, i) => (i === 0 ? '15 Aug' : i === 14 ? '1 Sep' : i === BACKLOG_TREND.length - 1 ? '13 Sep' : ''));
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline sample title="The backlog is shrinking. Reopened chats are not."><b>12</b> customers have waited more than a day, down from 31 a month ago. But <b>37</b> chats reopened this week, and 21 of them were about prices.</Headline>
      {bar}
      <div className="grid2">
        <Panel title="Waiting more than a day" note="Counted each morning at opening, last 30 days">
          <LineChart labels={labels} min={0} max={40} ticks={[0, 10, 20, 30, 40]} series={[{ label: 'Backlog', values: BACKLOG_TREND }]} height={230} />
        </Panel>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ padding: '14px 18px 4px' }}><h3 style={{ margin: 0 }}>Why chats reopened</h3><span className="sub">What the customer’s second message was about</span></div>
          <table className="table"><tbody>
            {REOPENED.map((r) => <tr key={r.why}><td><b style={{ fontWeight: 600 }}>{r.why}</b>{r.example && <div className="sub">{r.example}</div>}</td><td style={{ width: '40%' }}><span style={{ display: 'block', height: 8, borderRadius: 4, background: 'var(--line)' }}><i style={{ display: 'block', height: '100%', width: `${(r.count / REOPENED[0].count) * 100}%`, background: 'var(--ink)', borderRadius: 4 }} /></span></td><td className="r"><b>{r.count}</b></td></tr>)}
          </tbody></table>
        </div>
      </div>
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Still in the backlog</th><th>Account</th><th>First wrote</th><th>Last message</th><th className="r">Waited, opening hours</th></tr></thead><tbody>
          {BACKLOG.map((b) => <tr key={b.who}><td><b style={{ fontWeight: 600 }}>{b.who}</b></td><td className="sub">{b.account}</td><td>{b.first}</td><td className="sub">{b.message}</td><td className="r late">{b.waited}</td></tr>)}
        </tbody></table>
      </div>
    </main>
  );
}

function Calls({ bar }: { bar: React.ReactNode }) {
  return (
    <main className="main" style={{ gap: 16 }}>
      <Headline sample title="3 missed calls today have not been returned">2 of them left no message, so the call is the only way they reached you. Across the week, 23 calls were missed and 14 returned.</Headline>
      {bar}
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Caller</th><th>Account</th><th>Call</th><th>Missed at</th><th>After the call</th><th>Returned</th><th /></tr></thead><tbody>
          {CALLS.map((c) => <tr key={c.who + c.at}><td><b style={{ fontWeight: 600 }}>{c.who}</b></td><td className="sub">{c.account}</td><td><Icon name="phone" size={13} /> {c.kind}</td><td className="num">{c.at}</td><td className="sub">{c.after}</td><td className={c.tone}>{c.back}</td><td className="r">{c.tone === 'late' && <Btn icon="open" disabled title="Not connected yet">Open chat</Btn>}</td></tr>)}
        </tbody></table>
      </div>
      <div className="grid3">
        {CALLS_BY_LOCATION.map((c) => (
          <div key={c.location} className="panel" style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 6, alignItems: 'end' }}>
            <span><span className="sub">{c.location}, last 7 days</span><div className="num" style={{ font: '600 30px/1.1 var(--text)', fontStretch: '80%' }}>{c.missed} missed</div><span className="sub">{c.returned} returned, {c.missed - c.returned} not</span></span>
            <Spark values={c.trend} width={110} height={34} max={3} />
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
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', gap: 20 }}><div><span className="sub">Depilex · Unified Messenger</span><h1>Week of 6 to 12 September</h1></div><span className="mark" style={{ width: 34, height: 34, fontSize: 17 }}>U</span></div>
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
