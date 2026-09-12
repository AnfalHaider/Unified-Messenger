// ---- reports: backlog and reopened, missed calls, the weekly report ------------------------------------

boards.push({
  group: 'reports', phase: 'Soon after launch · backlog and reopened trends', order: 3,
  title: 'Backlog and reopened chats',
  note: 'Two quieter failures the line cannot show. Backlog is everyone still owed a reply after a full day, counted separately so it never hides inside “waiting”. Reopened chats are customers who got an answer and then had to write again, which usually means the answer did not settle it.',
  html: app({ active: 'reports' }, `<main class="main" style="gap:16px">
    <div class="headline"><div><h1>The backlog is shrinking. Reopened chats are not.</h1><p><b>12</b> customers have waited more than a day, down from 31 a month ago. But <b>37</b> chats reopened this week, and 21 of them were about prices.</p></div></div>
    ${reportsNav('Backlog and reopened')}
    <div class="grid2">
      <div class="panel"><h3>Waiting more than a day</h3><p class="sub" style="margin:-4px 0 6px">Counted each morning at opening, last 30 days</p>
        ${lineChart({ labels: ['15 Aug', '', '', '', '', '', '', '', '', '', '', '', '', '', '1 Sep', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '13 Sep'], min: 0, max: 40, ticks: [0, 10, 20, 30, 40], w: 640, h: 230,
          series: [{ label: 'Backlog', values: [31, 30, 32, 29, 28, 30, 27, 26, 27, 25, 24, 25, 23, 22, 23, 21, 20, 21, 19, 18, 19, 17, 16, 16, 15, 14, 15, 13, 12, 12] }] })}</div>
      <div class="panel" style="padding:0;overflow:hidden"><div style="padding:14px 18px 4px"><h3 style="margin:0">Why chats reopened</h3><span class="sub">What the customer’s second message was about, from the preview the app read</span></div>
        <table class="table"><tbody>
          ${[['Price or package', 21, 'Answered with a price, then asked what it includes'], ['Booking time', 8, 'Told a slot, then asked to change it'], ['Location or parking', 5, 'Given the address, then asked for directions'], ['Other', 3, '']].map(([why, n, ex]) => `<tr><td><b style="font-weight:600">${why}</b>${ex ? `<div class="sub">${ex}</div>` : ''}</td><td style="width:40%"><span style="display:block;height:8px;border-radius:4px;background:var(--line)"><i style="display:block;height:100%;width:${(n / 21) * 100}%;background:var(--ink);border-radius:4px"></i></span></td><td class="r"><b>${n}</b></td></tr>`).join('')}
        </tbody></table>
        <div style="padding:12px 18px;border-top:1px solid var(--line);display:flex;gap:12px;align-items:center">${ic('copy', 16)}<span class="sub">A saved “Prices” reply that lists what each package includes would likely prevent most of these.</span><button class="btn" style="margin-left:auto">Edit saved replies</button></div></div>
    </div>
    <div class="panel" style="padding:0;overflow:hidden"><table class="table"><thead><tr><th>Still in the backlog</th><th>Account</th><th>First wrote</th><th>Last message</th><th class="r">Waited, opening hours</th></tr></thead><tbody>
      ${[['Nadia F.', 'F-11 Instagram', 'Tuesday 2:10 pm', 'Hello? Is anyone there', '3 days 4 h'], ['Kashif U.', 'Men DHA-2 WhatsApp', 'Wednesday 6:45 pm', 'Do you do scalp treatment', '2 days 1 h'], ['Mariam Y.', 'DHA-2 WhatsApp', 'Thursday 8:30 pm', 'Photo', '1 day 3 h']].map(([n, a, f, m, w]) => `<tr><td><b style="font-weight:600">${n}</b></td><td class="sub">${a}</td><td>${f}</td><td class="sub">${m}</td><td class="r late">${w}</td></tr>`).join('')}
    </tbody></table></div>
  </main>`),
});

const calls = [
  ['Imran A.', 'F-11 WhatsApp', 'Voice call', '4:31 pm', 'Wrote after: “Called about bridal trial”', 'Not called back', 'late'],
  ['Unknown number', 'DHA-2 WhatsApp', 'Voice call', '3:58 pm', 'No message after', 'Not called back', 'late'],
  ['Hina A.', 'F-11 WhatsApp', 'Video call', '2:12 pm', 'No message after', 'Not called back', 'late'],
  ['Omar K.', 'Men DHA-2 WhatsApp', 'Voice call', '1:40 pm', 'Wrote after: “Price for hair colour?”', 'Answered by message, 1:52 pm', 'ok'],
  ['Saba N.', 'DHA-2 WhatsApp', 'Voice call, twice', '12:05 pm', 'No message after', 'Called back, 12:20 pm', 'ok'],
];
boards.push({
  group: 'reports', phase: 'Soon after launch · missed-call callback list', order: 4,
  title: 'Missed calls to call back',
  note: 'WhatsApp calls that nobody picked up, read from the call entries in each chat. A missed call with no message after it is the easiest customer to lose, so those lead. Calling back happens on the phone; the app only notices that it happened and ticks the row.',
  html: app({ active: 'reports' }, `<main class="main" style="gap:16px">
    <div class="headline"><div><h1>3 missed calls today have not been returned</h1><p>2 of them left no message, so the call is the only way they reached you. Across the week, 23 calls were missed and 14 returned.</p></div></div>
    ${reportsNav('Missed calls')}
    <div class="panel" style="padding:0;overflow:hidden"><table class="table"><thead><tr><th>Caller</th><th>Account</th><th>Call</th><th>Missed at</th><th>After the call</th><th>Returned</th><th></th></tr></thead><tbody>
      ${calls.map(([who, acct, kind, at, after, back, tone]) => `<tr><td><b style="font-weight:600">${who}</b></td><td class="sub">${acct}</td><td>${ic('phone', 13)} ${kind}</td><td class="num">${at}</td><td class="sub">${after}</td><td class="${tone}">${back}</td><td class="r">${tone === 'late' ? `<button class="btn">${ic('open', 13)}Open chat</button>` : ''}</td></tr>`).join('')}
    </tbody></table></div>
    <div class="grid3">
      ${[['F-11', 11, 6, [1, 2, 2, 1, 2, 1, 2]], ['DHA-2', 8, 5, [1, 1, 2, 1, 1, 1, 1]], ['Men DHA-2', 4, 3, [0, 1, 0, 1, 1, 0, 1]]].map(([loc, missed, back, t]) => `<div class="panel" style="display:grid;grid-template-columns:1fr auto;gap:6px;align-items:end"><span><span class="sub">${loc}, last 7 days</span><div style="font:600 30px/1.1 var(--text);font-stretch:80%" class="num">${missed} missed</div><span class="sub">${back} returned, ${missed - back} not</span></span>${spark(t, { w: 110, h: 34, max: 3 })}</div>`).join('')}
    </div>
  </main>`),
});

boards.push({
  group: 'reports', phase: 'Phase 4 · Analytics and Reports, with export', order: 5,
  title: 'The weekly report, ready to send',
  note: 'The week written as a short document for someone who was not watching: a plain-language lede, the figures that moved, and what to do about them. Every sentence is computed from the figures; nothing is phrased by a model. It saves as PDF, CSV or an image, and can be prepared every Monday morning.',
  html: app({ active: 'reports' }, `<div class="split" style="grid-template-columns:minmax(0,1fr) 300px">
    <div class="doc-wrap"><article class="doc">
      <div style="display:flex;justify-content:space-between;align-items:start;gap:20px"><div><span class="sub">Depilex · Unified Messenger</span><h1>Week of 6 to 12 September</h1></div><span class="mark" style="width:34px;height:34px;font-size:17px">U</span></div>
      <p class="lede">1,284 customers wrote to the three locations. 84% got a first reply within 15 minutes, two points down on the week before. Men DHA-2 hit the 90% goal; F-11 fell to 77%, almost all between 1 and 3 pm.</p>
      ${facts([['On time', 84, '%', '2 points down', 'due'], ['Median first reply', 11, 'min', '1 min slower', 'due'], ['Backlog', 12, '', '4 fewer', 'ok'], ['Missed calls', 23, '', '9 not returned', 'late']])}
      <div><h3>On time, by location</h3>${lineChart({ labels: weeks, min: 60, max: 100, ticks: [60, 70, 80, 90, 100], unit: '%', target: 90, targetLabel: 'Goal 90%', w: 716, h: 200,
        series: [{ label: 'Men DHA-2', values: [86, 88, 87, 90, 89, 91, 92], nudge: -6 }, { label: 'DHA-2', values: [89, 87, 88, 86, 85, 84, 83], dash: '6 4', nudge: 2 }, { label: 'F-11', values: [85, 86, 83, 82, 80, 79, 77], dash: '1.5 3.5', width: 2.4, nudge: 6 }] })}</div>
      <div class="grid2" style="gap:28px"><div><h3>What to look at</h3><p>F-11 between 1 and 2 pm: 38 replies took a median 24 minutes. And 3 one- and two-star reviews from this week still have no reply.</p></div><div><h3>What went well</h3><p>Men DHA-2 answered 92% on time, its best week since July, and the backlog is at its lowest this month.</p></div></div>
    </article></div>
    <aside class="cust" style="gap:14px"><h3 style="margin:0;font:650 17px/1.2 var(--display)">Save or send</h3>
      <div style="display:grid;gap:8px"><button class="btn primary" style="justify-content:flex-start">${ic('export', 14)}Save as PDF</button><button class="btn" style="justify-content:flex-start">${ic('export', 14)}Save figures as CSV</button><button class="btn" style="justify-content:flex-start">${ic('copy', 14)}Copy as image</button></div>
      <div><h4>Includes</h4><div style="display:grid;gap:8px;font-size:13px">${['Summary and figures', 'On time by location', 'Reply times by account', 'Reviews', 'Missed calls', 'Customer names'].map((t, i) => `<label style="display:flex;justify-content:space-between;align-items:center">${t}<span class="toggle ${i === 5 ? 'off' : ''}"></span></label>`).join('')}</div>
        <p class="sub" style="margin:8px 0 0">Customer names are left out by default, so the report can go to anyone.</p></div>
      <div><h4>Every week</h4><div class="srow" style="padding:0;border:0"><span><b>Prepare on Monday at 10 am</b><span>Saved to Documents › Depilex reports</span></span><span class="toggle"></span></div></div>
    </aside>
  </div>`),
});
