import { band, chip, clock, fig, hourBars, ic, meter, shareLine, shell } from './lib.mjs';
import { commandCenterContent } from './screens-a.mjs';

const stars = (n) => `<span class="row" style="gap:1px">${[1, 2, 3, 4, 5].map((i) => ic('star', 12, i <= n ? (n <= 2 ? 'var(--late)' : 'var(--due)') : 'var(--line-2)', 1.6)).join('')}</span>`;

const REVIEWS = [
  [1, 'Kamran S.', 'DHA Phase 2', '2 hours ago', 'Waited 40 minutes past my appointment and nobody told me why.', true],
  [2, 'Mehwish A.', 'F-11 Markaz', 'Yesterday', 'The stylist was great, but booking over WhatsApp took a day to get a reply.', false],
  [5, 'Danish R.', 'Gulberg', '2 days ago', 'Best haircut in Lahore. Friendly staff.', false],
  [4, 'Fatima Z.', 'F-11 Markaz', '3 days ago', 'Good service, a bit pricey.', false],
];

export const Reviews = () => shell({ active: 'reviews', content: `
<div class="row" style="align-items:flex-end"><div class="col" style="gap:3px"><h1 class="h1">Reviews</h1><span class="sub" style="font-size:12.5px">3 Google profiles · worst first · all 1,671 reviews read</span></div>
<div class="row" style="margin-left:auto"><div class="seg"><span class="on">Needs an answer</span><span>All</span><span>Answered</span></div></div></div>
${band(
  fig('Rating', '4.6', 'across 3 profiles', 'Steady this month', 'ok', [4.5, 4.6, 4.6, 4.5, 4.6, 4.6, 4.6]),
  fig('Unanswered', '1', 'review', '1 star · 2 hours ago', 'late', [0, 0, 1, 0, 2, 1, 1]),
  fig('New this week', '9', 'reviews', '3 more than last week', 'neu', [1, 2, 1, 0, 2, 1, 2]),
  fig('You answer in', '5', 'hours median', "Google's own figure", 'neu', [9, 7, 6, 6, 5, 5, 5]),
)}
<div style="display:grid;grid-template-columns:minmax(0,1fr) 400px;gap:22px;flex:1;min-height:0">
  <div class="board">
  ${REVIEWS.map(([s, n, l, t, x, sel]) => `<div class="brow" style="grid-template-columns:minmax(0,1fr);border-left-color:${s <= 2 ? 'var(--late)' : 'transparent'};${sel ? 'background:var(--wash)' : ''};padding:13px 14px">
    <div class="col" style="gap:6px">
      <div class="row">${stars(s)}<span class="who">${n}</span><span class="sub" style="font-size:12px">${l} · ${t}</span><span style="margin-left:auto">${s <= 2 ? chip('bad', 'No answer yet', 'clock') : chip('ok', 'Answered', 'check')}</span></div>
      <span style="color:var(--ink-2)">${x}</span></div></div>`).join('')}
  <div class="row" style="padding:11px 14px;color:var(--ink-3);font-size:12.5px">Older reviews are read but not listed here.</div>
  </div>
  <div class="col" style="gap:10px">
    <div class="row"><h2 class="h2">Answer Kamran S.</h2>${chip('ai', 'Drafted on this PC', 'spark')}</div>
    <div class="sheet" style="padding:14px 16px;color:var(--ink-2);line-height:1.65;background:var(--field);border-color:transparent">Hi Kamran, we're sorry you waited so long. That's not the experience we want at DHA Phase 2. Please message us on WhatsApp so we can make your next visit right.</div>
    <div class="row"><span class="btn">${ic('refresh', 14)}Draft again</span><span class="btn">Copy</span><span class="btn primary" style="margin-left:auto">${ic('out', 14)}Open in Google</span></div>
    <span class="sub" style="font-size:12px">You post the answer yourself on Google. The app never publishes anything.</span>
    <div class="rule" style="margin:6px 0"></div>
    <span class="label">Why this review is first</span>
    <div class="col" style="gap:7px;font-size:12.5px;color:var(--ink-2)">
      <div class="row">${ic('star', 14, 'var(--late)')}<span>One star, and the lowest this month</span></div>
      <div class="row">${ic('clock', 14, 'var(--ink-3)')}<span>Two hours old, against a five-hour median</span></div>
      <div class="row">${ic('building', 14, 'var(--ink-3)')}<span>DHA Phase 2 answered every other review within a day</span></div>
    </div>
  </div>
</div>` });

const HOURS = [4, 9, 16, 22, 30, 38, 34, 26, 24, 29, 33, 27, 15, 8];

export const Analytics = () => shell({ active: 'analytics', content: `
<div class="row" style="align-items:flex-end"><div class="col" style="gap:3px"><h1 class="h1">Analytics</h1><span class="sub" style="font-size:12.5px">WhatsApp and Instagram · all 3 locations · 1 to 11 September</span></div>
<div class="row" style="margin-left:auto"><div class="seg"><span>Today</span><span class="on">This week</span><span>This month</span><span>Custom</span></div><span class="btn">${ic('download', 14)}Export</span></div></div>
${band(
  fig('Conversations', '1,284', 'this period', '8% more than last', 'neu', [160, 172, 150, 190, 180, 176, 188]),
  fig('Answered on time', '86', '%', 'Target is 90%', 'due', [84, 85, 88, 86, 87, 85, 86]),
  fig('First reply', '10', 'min median', 'Inside the target', 'ok', [13, 12, 11, 10, 11, 10, 10]),
  fig('Missed calls', '23', 'not returned', '9 of them today', 'late', [2, 4, 3, 5, 3, 2, 4]),
)}
<div style="display:grid;grid-template-columns:minmax(0,1.35fr) minmax(0,1fr);gap:26px;flex:1;min-height:0">
  <div class="col" style="gap:10px"><div class="row"><h2 class="h2">Messages by hour</h2><span class="sub" style="margin-left:auto;font-size:12px">average day · busiest 2 to 3 pm</span></div>
  ${hourBars({ data: HOURS, startHour: 9, peak: 33 })}
  <span class="sub" style="font-size:12px">About half the week's messages arrive between 11 am and 4 pm.</span>
  <div class="rule" style="margin:4px 0"></div>
  <div class="row"><h2 class="h2">Answered on time, last 14 days</h2><span class="sub" style="margin-left:auto;font-size:12px">target 90%</span></div>
  ${shareLine({ data: [91, 90, 92, 88, 87, 89, 86, 88, 85, 84, 87, 86, 85, 86], labels: ['29', '30', '31', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11'], target: 90, h: 190 })}
  <span class="sub" style="font-size:12px">Below target every day since the 2nd, and the gap is widening rather than holding.</span></div>
  <div class="col" style="gap:10px"><h2 class="h2">By location</h2>
  <div class="sheet" style="overflow:hidden">
  ${[['F-11 Markaz', '612', '79%', '14 min', 'late'], ['DHA Phase 2', '438', '88%', '9 min', 'due'], ['Gulberg', '234', '96%', '6 min', 'ok']].map(([l, c, o, m, k]) =>
    `<div class="col" style="gap:7px;padding:13px 14px;border-bottom:1px solid var(--line)">
      <div class="row"><span class="h3">${l}</span><span class="mono sub" style="margin-left:auto;font-size:12px">${c} conversations</span></div>
      ${meter(parseInt(o, 10), k, 90)}
      <div class="row" style="font-size:12px"><span style="color:${k === 'late' ? 'var(--late)' : k === 'due' ? 'var(--due)' : 'var(--ok)'};font-weight:600">${o} on time</span><span class="sub" style="margin-left:auto">${m} median first reply</span></div></div>`).join('')}
  <div class="col" style="padding:12px 14px;gap:4px;background:var(--field)"><span style="font-weight:600;font-size:12.5px">Google is not counted here</span><span class="sub" style="font-size:12px">It has no messages, only reviews, so it lives on the Reviews page.</span></div>
  </div></div>
</div>` });

export const Reports = () => shell({ active: 'reports', content: `
<div class="row" style="align-items:flex-end"><div class="col" style="gap:3px"><h1 class="h1">Weekly report</h1><span class="sub" style="font-size:12.5px">Week of 1 September · Glow Salons</span></div>
<div class="row" style="margin-left:auto"><span class="btn">${ic('file', 14)}Previous weeks</span><span class="btn primary">${ic('download', 14)}Save as PDF</span></div></div>
<div style="display:grid;grid-template-columns:minmax(0,1fr) 300px;gap:34px;flex:1;min-height:0;padding-top:4px">
  <div class="col" style="gap:18px">
    <p style="margin:0;font-size:25px;line-height:1.35;font-weight:500;letter-spacing:-.01em;max-width:32em">Replies were on time for <span style="color:var(--due)">86%</span> of customers. F-11 Markaz fell to <span style="color:var(--late)">79%</span> on Monday and Tuesday, when bookings doubled at midday.</p>
    ${band(
      fig('Customers helped', '1,284', 'this week', '8% more than last week', 'neu'),
      fig('Answered on time', '86', '%', '2 points lower', 'due'),
      fig('New 5-star reviews', '6', 'reviews', '2 more than last week', 'ok'),
    )}
    <div class="col" style="gap:10px">
      <span class="label">What changed</span>
      <div class="col" style="gap:10px;color:var(--ink-2);max-width:44em">
        <div class="row" style="align-items:flex-start;gap:12px"><span class="mono" style="color:var(--late);font-weight:600;font-size:12.5px;padding-top:2px">MON</span><span>F-11 Markaz took three times its usual messages between 12 and 2 pm, and eleven customers waited more than half an hour.</span></div>
        <div class="row" style="align-items:flex-start;gap:12px"><span class="mono" style="color:var(--ok);font-weight:600;font-size:12.5px;padding-top:2px">ALL</span><span>DHA Phase 2 answered every review within a day.</span></div>
        <div class="row" style="align-items:flex-start;gap:12px"><span class="mono sub" style="font-weight:600;font-size:12.5px;padding-top:2px">GAP</span><span>Gulberg's Instagram was signed out for two days, so it is left out of these figures rather than counted as quiet.</span></div>
      </div>
    </div>
  </div>
  <div class="col" style="gap:12px;border-left:1px solid var(--line);padding-left:26px">
    <span class="label">Unusual this week</span>
    ${[['bad', 'Spike', 'F-11 Markaz, Monday midday'], ['warn', 'Dip', 'On time at F-11 fell 9 points'], ['neu', 'Gap', 'Gulberg Instagram offline 2 days']].map(([k, a, b]) =>
      `<div class="col" style="gap:3px;padding-bottom:11px;border-bottom:1px solid var(--line)">${chip(k, a)}<span style="color:var(--ink-2);font-size:12.5px">${b}</span></div>`).join('')}
    <span class="sub" style="font-size:12px;margin-top:auto">The app calculates every number here. The sentences are written from them, not by AI.</span>
  </div>
</div>` });

export const Assistant = () => shell({ tb: { aiOn: true }, active: 'center', content: commandCenterContent({ strip: '' }),
  panel: `<aside class="panel-r"><div class="row" style="padding:13px 16px;border-bottom:1px solid var(--line)">${ic('spark', 17, 'var(--brand-ink)')}<h2 class="h2">Ask</h2>${chip('neu', 'On this PC · gemma3 4B')}<span style="margin-left:auto;color:var(--ink-3)">${ic('x')}</span></div>
  <div class="col" style="flex:1;padding:16px;gap:14px;overflow:hidden">
    <div style="align-self:flex-end;background:var(--brand-ink);color:#fff;border-radius:8px 8px 2px 8px;padding:8px 12px;max-width:85%">Who has been waiting the longest?</div>
    <div class="col" style="gap:10px"><div style="background:var(--field);border-radius:8px 8px 8px 2px;padding:11px 13px;color:var(--ink);line-height:1.6">Sara M. on WhatsApp · Bookings at F-11 Markaz has waited 38 minutes. She wants to move her 5 pm appointment to tomorrow. Bilal R. is next, at 17 minutes.</div>
    <div class="sheet" style="padding:11px 13px;display:flex;flex-direction:column;gap:8px"><span class="label">Counted by the app, not the AI</span>
    <div class="row"><span style="font-weight:600">Sara M.</span><span style="margin-left:auto">${clock(38, 'min', 'late')}</span></div>
    <div class="row"><span style="font-weight:600">Bilal R.</span><span style="margin-left:auto">${clock(17, 'min', 'late')}</span></div></div>
    <div class="row" style="gap:6px"><span class="btn" style="height:27px">Open Sara's chat</span><span class="btn" style="height:27px">How should I reply?</span></div></div>
  </div>
  <div class="col" style="padding:12px 16px;border-top:1px solid var(--line);gap:8px">
    <div class="row" style="gap:6px;flex-wrap:wrap">${['How many are waiting?', 'Which location is slowest today?', 'Summarise unanswered reviews'].map((s) => `<span class="chip neu" style="height:25px">${s}</span>`).join('')}</div>
    <div class="input" style="height:38px"><span>Ask about your customers…</span><span style="margin-left:auto;color:var(--brand-ink)">${ic('send', 16)}</span></div>
    <span class="sub" style="font-size:11.5px">Runs on this PC. It can read your figures but can never send a message.</span></div></aside>` });

export const Notifications = () => shell({ active: 'center', content: commandCenterContent({ strip: '' }),
  panel: `<aside class="panel-r"><div class="row" style="padding:13px 16px;border-bottom:1px solid var(--line)"><h2 class="h2">Notifications</h2><span class="btn ghost" style="margin-left:auto;height:26px">Mark all read</span></div>
  <div class="col" style="gap:0">
  ${[['clock', 'bad', 'Sara M. passed the reply target', 'WhatsApp · Bookings · F-11 Markaz', '2 min ago', true], ['star', 'bad', 'New 1-star review', 'Google · DHA Phase 2', '2 hours ago', true],
     ['lock', 'neu', 'Instagram needs sign-in', 'Gulberg · the login was lost after a restart', '5 hours ago', true], ['phone', 'warn', '3 missed calls not returned', 'WhatsApp · Front desk · DHA Phase 2', 'Today', false],
     ['check', 'ok', 'All caught up at Gulberg', 'Nobody waiting', 'Yesterday', false], ['download', 'info', 'Update ready · 6.1.0', 'Restart to install', 'Yesterday', false]].map(([i, k, t, s, w, un]) =>
    `<div class="row" style="align-items:flex-start;padding:12px 16px;border-bottom:1px solid var(--line);border-left:3px solid ${un ? (k === 'bad' ? 'var(--late)' : k === 'warn' ? 'var(--due)' : 'var(--line-2)') : 'transparent'}">
    <span style="color:${k === 'bad' ? 'var(--late)' : k === 'warn' ? 'var(--due)' : k === 'ok' ? 'var(--ok)' : k === 'info' ? 'var(--info)' : 'var(--ink-3)'}">${ic(i, 15)}</span>
    <div class="col" style="gap:1px;flex:1"><span style="font-weight:600;font-size:12.5px">${t}</span><span class="sub" style="font-size:12px">${s}</span></div><span class="sub" style="font-size:11.5px;white-space:nowrap">${w}</span></div>`).join('')}
  </div></aside>` });

export const CommandPalette = () => shell({ active: 'center', content: commandCenterContent({ strip: '' }),
  overlay: `<div class="overlay" style="place-items:start center;padding-top:84px"><div class="dialog" style="width:620px">
  <div class="row" style="padding:13px 16px;border-bottom:1px solid var(--line)">${ic('search', 17, 'var(--ink-3)')}<span style="font-size:15px">sara</span><span class="kbd mono" style="margin-left:auto;background:var(--field);color:var(--ink-3)">Esc</span></div>
  <div class="col" style="padding:6px;gap:1px">
   <span class="label" style="margin:8px 10px 4px">Customers</span>
   ${[['chat', 'Sara M.', 'WhatsApp · Bookings · waiting 38 min', true], ['insta', 'sara.style', 'Instagram · DHA Phase 2 · answered']].map(([i, t, s, on]) => `<div class="row" style="height:40px;padding:0 10px;border-radius:4px;${on ? 'background:var(--wash)' : ''}">${ic(i, 15, 'var(--ink-2)')}<span style="font-weight:600">${t}</span><span class="sub" style="font-size:12.5px">${s}</span>${on ? `<span class="kbd mono" style="margin-left:auto;background:var(--field);color:var(--ink-3)">Enter</span>` : ''}</div>`).join('')}
   <span class="label" style="margin:12px 10px 4px">Actions</span>
   ${[['refresh', 'Re-sync all accounts'], ['plus', 'Add account'], ['moon', "Sleep accounts I'm not using"], ['spark', 'Ask the assistant']].map(([i, t]) => `<div class="row" style="height:36px;padding:0 10px;border-radius:4px">${ic(i, 15, 'var(--ink-2)')}<span>${t}</span></div>`).join('')}
  </div></div></div>` });
