// ---- reviews ----------------------------------------------------------------------------------------

const stars = (n, s = 13) => `<span class="stars" aria-label="${n} of 5 stars">${[1, 2, 3, 4, 5].map((i) => `<span class="${i <= n ? '' : 'off'}">${ic('reviews', s, 0).replace('fill="none"', 'fill="currentColor"')}</span>`).join('')}</span>`;
const REVIEWS = [
  [1, 'Areej S.', 'DHA-2', '2 days ago', 'Waited 40 minutes past my appointment and nobody told me why. The facial was fine but I won’t come back.', true],
  [2, 'Hassan M.', 'Men DHA-2', '3 days ago', 'Called twice to book, no answer. Walked in and they had no slot.', false],
  [2, 'Mehwish T.', 'F-11', '5 days ago', 'Price on WhatsApp was different from what I paid at the counter.', false],
  [3, 'Rida A.', 'F-11', 'last week', 'Good haircut, but the place was very crowded on Sunday.', false],
  [5, 'Saad K.', 'Men DHA-2', 'last week', 'Best beard trim in DHA, Imran is great.', false],
];
boards.push({
  group: 'reviews', phase: 'Phase 3 · Channel modules · full history in Phase 8', order: 1,
  title: 'Reviews, worst and unanswered first',
  note: 'The three Google profiles as one desk. One- and two-star reviews without a reply sit at the top because they are the ones a future customer reads. The rating and lifetime total come from Google’s own profile, and the page says honestly how much of the history the app could read.',
  html: app({ active: 'reviews' }, `<main class="main">
    <div class="headline"><div><h1>3 unhappy reviews have no reply</h1><p>Oldest from 5 days ago at F-11. Replying within a day is what Google shows next to your rating.</p></div>
      <div class="actions"><div class="seg"><button aria-pressed="true">Needs a reply 3</button><button>All reviews</button></div></div></div>
    <div class="grid3">
      ${[['F-11', 4.6, 991, [620, 210, 71, 38, 52], 1], ['DHA-2', 4.6, 1671, [1105, 330, 102, 55, 79], 1], ['Men DHA-2', 4.7, 435, [318, 71, 20, 9, 17], 1]].map(([loc, r, total, dist, open]) => `<div class="panel" style="display:grid;grid-template-columns:auto 1fr;gap:16px;align-items:center">
        <div><span class="sub">${loc}</span><div style="font:600 40px/1 var(--text);font-stretch:80%" class="num">${r}</div>${stars(Math.round(r), 12)}<div class="sub num" style="margin-top:4px">${total.toLocaleString()} reviews</div></div>
        <div class="bars5">${dist.map((c, i) => `<div><span>${5 - i}</span><span class="b"><i style="width:${(c / dist[0]) * 100}%"></i></span><span class="num" style="text-align:right">${c}</span></div>`).join('')}</div></div>`).join('')}
    </div>
    <div class="two" style="grid-template-columns:minmax(0,1fr) 400px">
      <div class="panel" style="padding:0;overflow:hidden">${REVIEWS.map(([n, who, loc, when, text, sel]) => `<div class="review ${sel ? 'sel' : ''}"><div>${stars(n)}<div class="sub" style="margin-top:4px">${when}</div></div><div><b style="font-weight:600">${who}</b> <span class="sub">· ${loc}</span><p>${text}</p></div>${n <= 2 ? '<span class="chip late">No reply</span>' : '<span class="chip neu">Replied</span>'}</div>`).join('')}
        <div style="padding:10px 18px;border-top:1px solid var(--line)" class="sub">Covers the latest 150 reviews across the three profiles. Older ones are on Google; complete history arrives with Google’s own reviews service.</div></div>
      <div class="panel" style="display:grid;gap:10px;align-content:start"><h3 style="margin:0">Reply to Areej S.</h3>
        <p class="sub" style="margin:0">A draft written on this PC from her review only. Edit it, copy it, and post it on Google yourself.</p>
        <div style="border:1px solid var(--line-2);border-radius:10px;background:var(--raised);padding:12px;font-size:13.5px;line-height:1.55">Dear Areej, thank you for telling us, and we’re sorry you were kept waiting 40 minutes without an explanation. That isn’t the visit we want anyone to have. Our DHA-2 manager would like to make it right; please message us on 0300 7654321.</div>
        <div style="display:flex;gap:8px"><button class="btn primary">${ic('copy', 14)}Copy reply</button><button class="btn">${ic('open', 14)}Open on Google</button><button class="btn quiet">${ic('refresh', 14)}Write another</button></div>
        <div class="sub" style="display:flex;gap:6px;align-items:center">${ic('shield', 13)}Drafted by the assistant on this PC. Nothing is posted by the app.</div></div>
    </div>
  </main>`),
});

// ---- reports: overview ------------------------------------------------------------------------------

const weeks = ['2 Aug', '', '16 Aug', '', '30 Aug', '', '13 Sep'];
const hours = ['11', '12', '1', '2', '3', '4', '5', '6', '7', '8', '9'];
const dayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const heat = dayNames.map((d, di) => hours.map((h, hi) => Math.round(4 + 22 * Math.exp(-((hi - (di >= 5 ? 5 : 3)) ** 2) / 6) + (di >= 5 ? 9 : 0) + seeded(1, di * 13 + hi)[0] * 6)));
const facts = (list) => `<div class="facts" style="grid-template-columns:repeat(${list.length},minmax(0,1fr))">${list.map(([label, value, unit, note, tone, trend]) => `<div class="fact"><span>${label}</span><div style="display:flex;align-items:end;justify-content:space-between;gap:8px"><b class="num">${value}<small>${unit}</small></b>${trend ? spark(trend, { w: 70, h: 26 }) : ''}</div><em style="color:var(--${tone})">${note}</em></div>`).join('')}</div>`;

const reportsNav = (active) => `<div style="display:flex;gap:14px;align-items:center"><div class="seg">${['Overview', 'Reply times', 'Backlog and reopened', 'Missed calls', 'Weekly report'].map((t) => `<button aria-pressed="${t === active}">${t}</button>`).join('')}</div>
  <div style="margin-left:auto;display:flex;gap:8px"><div class="seg"><button>Today</button><button aria-pressed="true">7 days</button><button>30 days</button><button>${ic('cal', 13)} Custom</button></div><button class="btn">${ic('export', 14)}Export</button></div></div>`;

boards.push({
  group: 'reports', phase: 'Phase 4 · Analytics and Reports', order: 1,
  title: 'Reports: how the week went',
  note: 'One page that answers “are we getting better?”. Each figure carries its own trend and says what it is compared with. The location lines are told apart by line style and a label at the end, so the only colour is the 90% goal. The heat map shows when the desk is swamped, in opening hours.',
  html: app({ active: 'reports' }, `<main class="main" style="gap:16px">
    <div class="headline"><div><h1>Last 7 days: slower at F-11 in the afternoons</h1><p>84% answered on time, 2 points below the week before. The drop is almost all F-11 between 1 and 3 pm.</p></div></div>
    ${reportsNav('Overview')}
    ${facts([['Answered on time', 84, '%', '2 points down', 'due', [88, 87, 89, 86, 86, 85, 84]], ['Median first reply', 11, 'min', '1 min slower', 'due', [9, 10, 9, 10, 11, 10, 11]], ['Customers who wrote', '1,284', '', '6% more', 'ink-3', [160, 172, 181, 170, 190, 205, 206]], ['Waiting over a day', 12, '', '4 fewer', 'ok', [18, 17, 16, 16, 15, 13, 12]], ['Reopened', 37, '', 'waiting again after a reply', 'ink-3', [4, 6, 5, 5, 6, 5, 6]], ['Missed calls', 23, '', '9 not called back', 'late', [2, 4, 3, 3, 4, 3, 4]]])}
    <div class="grid2" style="grid-template-columns:minmax(0,1.1fr) minmax(0,1fr)">
      <div class="panel"><h3>Answered on time, by location, week by week</h3>${lineChart({ labels: weeks, min: 60, max: 100, ticks: [60, 70, 80, 90, 100], unit: '%', target: 90, targetLabel: 'Goal 90%', w: 700, h: 260,
        series: [{ label: 'Men DHA-2', values: [86, 88, 87, 90, 89, 91, 92], nudge: -6 }, { label: 'DHA-2', values: [89, 87, 88, 86, 85, 84, 83], dash: '6 4', nudge: 2 }, { label: 'F-11', values: [85, 86, 83, 82, 80, 79, 77], dash: '1.5 3.5', width: 2.4, nudge: 6 }] })}</div>
      <div class="panel"><h3>When customers write</h3><p class="sub" style="margin:-4px 0 8px">Messages per hour, averaged over 4 weeks. Darker is busier.</p>${heatmap({ rows: dayNames, cols: hours, data: heat, w: 600, cell: 30 })}
        <p class="sub" style="margin:6px 0 0">Busiest: Saturday and Sunday, 3 to 5 pm. Weekdays peak at 1 to 2 pm, when F-11 is slowest to reply.</p></div>
    </div>
  </main>`),
});

// ---- reports: reply times -----------------------------------------------------------------------------

const buckets = [['0–5', 164], ['5–10', 131], ['10–15', 88], ['15–30', 57], ['30–60', 29], ['1–2 h', 11], ['2 h +', 6]];
function histogram() {
  const w = 640, h = 230, pl = 36, pb = 30, pt = 18, max = 180;
  const bw = (w - pl) / buckets.length;
  const y = (v) => pt + (h - pt - pb) * (1 - v / max);
  return `<svg viewBox="0 0 ${w} ${h}" width="100%" role="img" aria-label="First replies by how long they took">
    ${[0, 60, 120, 180].map((v) => `<line x1="${pl}" x2="${w}" y1="${y(v)}" y2="${y(v)}" stroke="var(--line)"/><text x="${pl - 8}" y="${y(v) + 4}" text-anchor="end" font-size="11" fill="var(--ink-3)">${v}</text>`).join('')}
    ${buckets.map(([l, v], i) => { const late = i >= 3; const x = pl + i * bw + 6; return `<path d="M${x},${y(0)} V${y(v) + 4} q0,-4 4,-4 h${bw - 20} q4,0 4,4 V${y(0)} z" fill="${late ? 'var(--m-late)' : 'var(--m-ok)'}"/><text x="${x + (bw - 12) / 2}" y="${y(v) - 6}" text-anchor="middle" font-size="12" font-weight="600" fill="${late ? 'var(--late)' : 'var(--ink)'}">${v}</text><text x="${x + (bw - 12) / 2}" y="${h - 10}" text-anchor="middle" font-size="11.5" fill="var(--ink-2)">${l}</text>`; }).join('')}
    <line x1="${pl + 3 * bw}" x2="${pl + 3 * bw}" y1="${pt - 6}" y2="${y(0)}" stroke="var(--ink)" stroke-width="2"/><text x="${pl + 3 * bw + 6}" y="${pt + 4}" font-size="11.5" font-weight="600" fill="var(--ink)">Target 15 min</text>
  </svg>`;
}
const acctRows = [['F-11 WhatsApp', 188, 13, 77, 41, 'late'], ['F-11 Instagram', 36, 22, 61, 95, 'late'], ['DHA-2 WhatsApp', 152, 12, 83, 38, 'due'], ['DHA-2 Instagram', 29, 16, 72, 70, 'late'], ['Men DHA-2 WhatsApp', 81, 7, 92, 19, 'ok']];
boards.push({
  group: 'reports', phase: 'Phase 4 · Analytics and Reports', order: 2,
  title: 'Reply times in depth',
  note: 'A median hides the long tail, so the distribution is drawn in full against the target: 103 of 486 first replies took longer than 15 minutes, and 17 took over an hour. The table ranks accounts by the slowest one in ten, which is the customer who writes the angry review.',
  html: app({ active: 'reports' }, `<main class="main" style="gap:16px">
    <div class="headline"><div><h1>Most replies are quick. The slow ones are very slow.</h1><p>Median first reply <b>11 min</b>, but the slowest one in ten took <b class="late">28 min or more</b>. Instagram is slower than WhatsApp at every location.</p></div></div>
    ${reportsNav('Reply times')}
    <div class="grid2" style="grid-template-columns:minmax(0,1fr) minmax(0,1fr)">
      <div class="panel"><h3>First replies by how long they took</h3><p class="sub" style="margin:-4px 0 6px">486 replies in the last 7 days, counted in opening hours</p>${histogram()}</div>
      <div class="panel" style="padding:0;overflow:hidden"><div style="padding:14px 18px 4px"><h3 style="margin:0">By account</h3></div>
        <table class="table"><thead><tr><th>Account</th><th class="r">Replies</th><th class="r">Median</th><th class="r">On time</th><th class="r">Slowest 1 in 10</th></tr></thead><tbody>
        ${acctRows.map(([a, n, med, on, p90, tone]) => `<tr><td><b style="font-weight:600">${a}</b></td><td class="r">${n}</td><td class="r">${med} min</td><td class="r ${tone}">${on}%</td><td class="r ${p90 > 30 ? 'late' : ''}">${p90} min</td></tr>`).join('')}
        </tbody></table>
        <div style="padding:12px 18px;border-top:1px solid var(--line)" class="sub">Replies are measured going forward from what the app sees happen. Opening hours and holidays pause the clock.</div></div>
    </div>
    <div class="panel" style="display:grid;grid-template-columns:auto 1fr auto;gap:16px;align-items:center">${ic('clock', 20)}<span><b style="font-weight:600">Slowest hour: 1 to 2 pm at F-11,</b> <span class="sub">median 24 minutes across 38 replies. That is also F-11’s lunch break on the roster.</span></span><button class="btn">See the hour-by-hour view</button></div>
  </main>`),
});
