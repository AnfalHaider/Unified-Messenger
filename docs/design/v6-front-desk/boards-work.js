// ---- boards -----------------------------------------------------------------------------------------
const boards = [];

boards.push({
  group: 'work', phase: 'Phase 4 · Screens', order: 1,
  title: 'The line',
  note: 'Where the app opens. The headline says what matters in a sentence; the line shows where the lateness is; the list below is worked top to bottom. Selecting a customer, by click or with J and K, offers the three things you would do next.',
  html: app({ active: 'line' }, `<main class="main">
    <div class="headline"><div><h1>${PEOPLE.length} customers are waiting</h1>
      <p><b class="late">${late} are past your ${TARGET}-minute target</b>, ${due} more pass it in the next five minutes. Today ${'82%'} were answered on time, with a median first reply of 11 minutes.</p></div>
      <div class="actions"><button class="btn">${ic('refresh', 14)}Read now</button></div></div>
    ${theLine({ selected: 'Sara M.' })}
    <div class="work"><div class="qhead"><strong>Longest wait first</strong><span>${PEOPLE.length} customers</span>
      <span class="keys"><kbd>J</kbd><kbd>K</kbd> move <kbd>Enter</kbd> open chat <kbd>H</kbd> handled <kbd>S</kbd> snooze 1 hour</span></div>
      <div class="queue">${PEOPLE.slice(0, 7).map((p) => row(p, { sel: p.name === 'Sara M.' })).join('')}</div></div>
  </main>`),
});

const cell = (kind, stateText, dot, body) => `<div class="cell ${kind}"><span class="state"><i style="background:${dot}"></i>${stateText}</span>${body}</div>`;
boards.push({
  group: 'accounts', phase: 'Phase 3 · Channel modules', order: 1,
  title: 'Accounts, as one grid',
  note: 'Every location against every channel, so a missing login or a quiet reader is a gap you can see rather than a line you have to find. Each cell says what to do about itself. Google stays signed in but shows no figures until its reviews reader exists.',
  html: app({ active: 'accounts' }, `<main class="main">
    <div class="headline"><div><h1>Accounts</h1><p>9 accounts at 3 locations. <b>2 are reading</b>, 4 need you to sign in again, and Google reviews has no reader yet.</p></div>
      <div class="actions"><button class="btn">${ic('refresh', 14)}Read all now</button></div></div>
    <div class="board-grid">
      <div class="bg-h" style="border-right:0"></div>
      <div class="bg-h" style="border-left:1px solid var(--line)">${ic('chat', 15)}WhatsApp</div>
      <div class="bg-h" style="border-left:1px solid var(--line)">${ic('ig', 15)}Instagram</div>
      <div class="bg-h" style="border-left:1px solid var(--line)">${ic('star', 15)}Google reviews</div>

      <div class="bg-loc"><b>F-11</b><span>9 waiting</span></div>
      ${cell('', 'Reading', 'var(--m-ok)', `<span class="big num">8<small>waiting</small></span><p>500 chats read 20 s ago, 5 past target</p><button class="btn">${ic('open', 14)}Open page</button>`)}
      ${cell('warn', 'Sign in needed', 'var(--m-due)', `<p>Instagram asked for the password again. Customers there are not being counted.</p><button class="btn primary">${ic('lock', 14)}Sign in</button>`)}
      ${cell('off', 'Signed in', 'var(--line-2)', `<p>No figures yet. The reviews reader has not been built.</p><button class="btn quiet">${ic('open', 14)}Open page</button>`)}

      <div class="bg-loc"><b>DHA-2</b><span>6 waiting</span></div>
      ${cell('warn', 'Sign in needed', 'var(--m-due)', `<p>WhatsApp is showing a QR code. Scan it from the DHA-2 phone: WhatsApp › Linked devices.</p><button class="btn primary">${ic('qr', 14)}Show QR code</button>`)}
      ${cell('warn', 'Sign in needed', 'var(--m-due)', `<p>Instagram asked for the password again.</p><button class="btn primary">${ic('lock', 14)}Sign in</button>`)}
      ${cell('off', 'Signed in', 'var(--line-2)', `<p>No figures yet. The reviews reader has not been built.</p><button class="btn quiet">${ic('open', 14)}Open page</button>`)}

      <div class="bg-loc"><b>Men DHA-2</b><span>4 waiting</span></div>
      ${cell('', 'Reading', 'var(--m-ok)', `<span class="big num">4<small>waiting</small></span><p>500 chats read 40 s ago, 1 past target</p><button class="btn">${ic('open', 14)}Open page</button>`)}
      ${cell('warn', 'Sign in needed', 'var(--m-due)', `<p>Instagram asked for the password again.</p><button class="btn primary">${ic('lock', 14)}Sign in</button>`)}
      ${cell('off', 'Signed in', 'var(--line-2)', `<p>No figures yet. The reviews reader has not been built.</p><button class="btn quiet">${ic('open', 14)}Open page</button>`)}
    </div>
    <div class="readers">
      <div class="reader">${ic('chat', 18)}<span><b>WhatsApp reader</b><br><span style="font-size:12.5px;color:var(--ink-2)">148 good reads since 9:02 am, none failed</span></span><span class="chip ok">${ic('check', 12)}Working</span></div>
      <div class="reader">${ic('ig', 18)}<span><b>Instagram reader</b><br><span style="font-size:12.5px;color:var(--ink-2)">Waiting for a signed-in account to read</span></span><span class="chip neu">Idle</span></div>
    </div>
  </main>`),
});

const days = [['Thu', 12, 41], ['Fri', 10, 48], ['Sat', 19, 63], ['Sun', 9, 22], ['Mon', 16, 57], ['Tue', 10, 44], ['Wed', 11, 39]];
function dayChart() {
  const w = 700, h = 250, pl = 36, pb = 28, pt = 16, max = 25;
  const y = (v) => pt + (h - pt - pb) * (1 - v / max);
  const bw = 54, gap = (w - pl - days.length * bw) / days.length;
  const grid = [0, 5, 10, 15, 20, 25].map((v) => `<line x1="${pl}" x2="${w}" y1="${y(v)}" y2="${y(v)}" stroke="var(--line)" stroke-width="1"/><text x="${pl - 8}" y="${y(v) + 4}" text-anchor="end" font-size="11" fill="var(--ink-3)">${v}</text>`).join('');
  const bars = days.map(([d, m, n], i) => {
    const bx = pl + gap / 2 + i * (bw + gap), over = m > TARGET;
    return `<path d="M${bx},${y(0)} V${y(m) + 4} q0,-4 4,-4 h${bw - 8} q4,0 4,4 V${y(0)} z" fill="${over ? 'var(--m-late)' : 'var(--m-ok)'}"/>
      <text x="${bx + bw / 2}" y="${y(m) - 7}" text-anchor="middle" font-size="12" font-weight="600" fill="${over ? 'var(--late)' : 'var(--ink)'}">${m}</text>
      <text x="${bx + bw / 2}" y="${h - 8}" text-anchor="middle" font-size="12" fill="var(--ink-2)">${d}</text>`;
  }).join('');
  return `<svg viewBox="0 0 ${w} ${h}" width="100%" role="img" aria-label="Median first reply per day against the 15 minute target">${grid}${bars}
    <line x1="${pl}" x2="${w}" y1="${y(TARGET)}" y2="${y(TARGET)}" stroke="var(--ink)" stroke-width="2"/>
    <text x="${w - 4}" y="${y(TARGET) - 7}" text-anchor="end" font-size="11.5" font-weight="600" fill="var(--ink)">Target 15 min</text></svg>`;
}
boards.push({
  group: 'accounts', phase: 'Phase 4 · Screens', order: 2,
  title: 'One account, in figures',
  note: 'Reached from Accounts or from the account’s dock. The week is drawn against the target so the two slow days name themselves, and the panel on the right answers the question behind every odd number: is this account actually being read?',
  html: app({ active: 'accounts', scope: 'F-11' }, `<main class="main">
    <div class="headline"><div><h1>F-11 WhatsApp</h1><p>Depilex F-11 · signed in on this PC · read 20 s ago</p></div>
      <div class="actions"><button class="btn">${ic('open', 14)}Open page</button><button class="btn">${ic('refresh', 14)}Read now</button><button class="btn quiet">${ic('sleep', 14)}Sleep</button></div></div>
    <div class="facts">
      <div class="fact"><span>Waiting now</span><b class="num">8<small>customers</small></b><em style="color:var(--late)">5 past target</em></div>
      <div class="fact"><span>First reply today</span><b class="num">11<small>min median</small></b><em style="color:var(--ok)">inside the target</em></div>
      <div class="fact"><span>On time, last 7 days</span><b class="num">74<small>%</small></b><em style="color:var(--due)">aiming for 90%</em></div>
      <div class="fact"><span>Replies measured</span><b class="num">314</b><em style="color:var(--ink-3)">since 2 Sept</em></div>
    </div>
    <div class="two">
      <div class="panel"><h3>First reply, last 7 days</h3><p style="margin:-4px 0 6px;font-size:12.5px;color:var(--ink-2)">Median minutes per day, opening hours only. Saturday and Monday went past the target.</p>${dayChart()}</div>
      <div class="panel"><h3>Is it being read?</h3><div class="checks">
        <div class="check"><span style="color:var(--ok)">${ic('check', 16)}</span><span><b>Signed in</b><span>The login is kept on this PC and survives a restart.</span></span></div>
        <div class="check"><span style="color:var(--ok)">${ic('check', 16)}</span><span><b>Read 20 seconds ago</b><span>500 chats, the most one read takes. Read every minute.</span></span></div>
        <div class="check"><span style="color:var(--ok)">${ic('check', 16)}</span><span><b>Page kept open</b><span>So the numbers keep moving while you are elsewhere.</span></span></div>
        <div class="check"><span style="color:var(--ink-3)">${ic('snooze', 16)}</span><span><b>2 chats snoozed</b><span>They come back at 6:10 pm if still unanswered.</span></span></div>
      </div></div>
    </div>
  </main>`),
});

boards.push({
  group: 'work', phase: 'Phase 3 · Channel modules', order: 6,
  title: 'Needs you',
  note: 'Anything the app cannot fix by itself, collected in one place and kept out of the customer list. Each item says exactly what to do. The badge in the title bar is the only reminder: no banners across the screen.',
  html: `<div style="position:relative">${app({ active: 'line', needsOpen: true }, `<main class="main">
    <div class="headline"><div><h1>${PEOPLE.length} customers are waiting</h1><p><b class="late">${late} are past your ${TARGET}-minute target</b>, ${due} more pass it in the next five minutes.</p></div></div>
    ${theLine()}
  </main>`)}<div class="scrim"></div>
    <aside class="drawer" aria-label="Needs you">
      <h3>Needs you</h3>
      <h4>Sign in again</h4>
      <div class="todo"><span style="color:var(--due)">${ic('qr', 18)}</span><span><b>DHA-2 WhatsApp</b><span>Showing a QR code since 11:23 pm. Scan it from the DHA-2 phone. Its customers are not being counted until then.</span></span><button class="btn primary">Show QR</button></div>
      <div class="todo"><span style="color:var(--due)">${ic('lock', 18)}</span><span><b>F-11 Instagram</b><span>Asked for the password.</span></span><button class="btn">Sign in</button></div>
      <div class="todo"><span style="color:var(--due)">${ic('lock', 18)}</span><span><b>DHA-2 Instagram</b><span>Asked for the password.</span></span><button class="btn">Sign in</button></div>
      <div class="todo"><span style="color:var(--due)">${ic('lock', 18)}</span><span><b>Men DHA-2 Instagram</b><span>Asked for the password.</span></span><button class="btn">Sign in</button></div>
      <h4>Nothing else</h4>
      <div class="todo" style="grid-template-columns:22px 1fr"><span style="color:var(--ok)">${ic('check', 18)}</span><span><b>Both readers working</b><span>No failed reads today.</span></span></div>
    </aside></div>`,
});

boards.push({
  group: 'work', phase: 'Phase 4 · Screens', order: 7,
  title: 'Nobody waiting',
  note: 'The empty line is still a line, so an all-clear looks like a real reading rather than a blank screen. It says when the last customer was answered, which is how you tell “quiet” from “broken”.',
  html: app({ active: 'line', needs: 0 }, `<main class="main">
    <div class="headline"><div><h1>Nobody is waiting</h1>
      <p>The last customer, at <b>Men DHA-2 WhatsApp</b>, was answered 4 minutes ago. Today 91% were answered on time, with a median first reply of 8 minutes.</p></div></div>
    ${theLine({ people: [] })}
    <div class="panel" style="display:grid;grid-template-columns:auto 1fr auto;gap:16px;align-items:center">
      <span style="width:44px;height:44px;border-radius:50%;background:var(--ok-w);color:var(--ok);display:grid;place-items:center">${ic('check', 22, 2)}</span>
      <span><b style="font-weight:600">All caught up across 3 locations</b><br><span style="font-size:13px;color:var(--ink-2)">Both WhatsApp accounts were read in the last minute. The line fills in as soon as someone writes.</span></span>
      <button class="btn">${ic('refresh', 14)}Read now</button>
    </div>
  </main>`),
});

boards.push({
  group: 'settings', phase: 'Phase 4 · Screens', order: 1,
  title: 'Settings: look and reading',
  note: 'The theme choice says plainly that it reaches everything, including the pages of the accounts, and shows what each option looks like. Numbers are set with steppers, never free text, so a target cannot be typed wrong.',
  html: app({ active: 'settings', needs: 4 }, `<main class="main">
    <div class="headline"><div><h1>Settings</h1><p>Kept on this PC.</p></div></div>
    <div class="settings">
      <nav class="stabs"><button aria-current="page">Look and reading</button><button>Opening hours</button><button>Notifications</button><button>Assistant</button><button>Workspace</button><button>Privacy</button><button>About</button></nav>
      <div style="display:grid;gap:26px">
        <div class="sgroup"><h3>Appearance</h3><p>Applies to the whole app: title bar, sidebar, menus, scrollbars, and the WhatsApp and Instagram pages inside it.</p>
          <div class="themes">
            <button class="tcard" aria-pressed="true"><span class="prev s"><span class="l"></span><span class="r"><i style="width:70%"></i><i></i><i style="width:50%"></i></span><span class="r" style="background:#1B1F1C"><i style="width:70%;background:#3E4540"></i><i style="background:#3E4540"></i><i style="width:50%;background:#3E4540"></i></span></span><label>${ic('monitor', 15)}Match Windows</label></button>
            <button class="tcard" aria-pressed="false"><span class="prev"><span class="l"></span><span class="r"><i style="width:70%"></i><i></i><i style="width:50%"></i></span></span><label>${ic('sun', 15)}Light</label></button>
            <button class="tcard" aria-pressed="false"><span class="prev d"><span class="l"></span><span class="r"><i style="width:70%"></i><i></i><i style="width:50%"></i></span></span><label>${ic('moon', 15)}Dark</label></button>
          </div></div>
        <div class="sgroup"><h3>Reading</h3>
          <div class="panel" style="padding:0">
            <div class="srow"><span><b>Reply target</b><span>A customer waiting longer than this is past target. Counted in opening hours.</span></span><div class="stepper"><button>−</button><span class="num">15 min</span><button>+</button></div></div>
            <div class="srow"><span><b>Read each account every</b><span>Reading is quiet and never opens a chat.</span></span><div class="stepper"><button>−</button><span class="num">1 min</span><button>+</button></div></div>
            <div class="srow"><span><b>Leave out chats that ended themselves</b><span>A last message like “ok thanks” is not someone waiting.</span></span><span class="toggle"></span></div>
          </div></div>
      </div>
    </div>
  </main>`),
});

const signins = [
  ['F-11', [['wa', 'F-11 WhatsApp', 'Signed in, reading', 'ok'], ['ig', 'F-11 Instagram', 'Needs the password', 'due'], ['g', 'F-11 Google', 'Signed in', 'ok']]],
  ['DHA-2', [['wa', 'DHA-2 WhatsApp', 'Scan the QR code', 'due'], ['ig', 'DHA-2 Instagram', 'Needs the password', 'due'], ['g', 'DHA-2 Google', 'Signed in', 'ok']]],
  ['Men DHA-2', [['wa', 'Men DHA-2 WhatsApp', 'Signed in, reading', 'ok'], ['ig', 'Men DHA-2 Instagram', 'Needs the password', 'due'], ['g', 'Men DHA-2 Google', 'Signed in', 'ok']]],
];
boards.push({
  group: 'workspace', phase: 'Phase 6 · Cloud and membership', order: 1.5,
  title: 'A new PC: bring the accounts in',
  note: 'On a new PC, or after the move from the previous version, the workspace’s accounts arrive marked Sign in needed and become a checklist instead of a wall of sign-in screens. Logins that came across are ticked; each one that did not has a single button that opens exactly that account’s sign-in, docked, and ticks itself when it lands.',
  html: app({ active: 'accounts', needs: 4 }, `<main class="main" style="max-width:1080px">
    <div class="headline"><div><h1>5 of 9 accounts are ready</h1><p>Your workspace’s accounts and locations are already here. Four logins still need signing in on this PC before those accounts can be read.</p></div></div>
    <div style="display:grid;grid-template-columns:minmax(0,1fr) auto;gap:14px;align-items:center"><div class="progress"><i style="width:55.5%"></i></div><span class="num" style="font-size:13px;color:var(--ink-2)">5 of 9</span></div>
    <div class="panel" style="padding:0;overflow:hidden">
      ${signins.map(([loc, rows]) => `<div class="loc-head">Depilex ${loc}</div>${rows.map(([ch, name, st, tone]) => `<div class="signin"><span style="color:var(--ink)">${ic(chIcon[ch], 18)}</span><b>${name}</b><span style="color:var(--${tone === 'ok' ? 'ok' : 'due'});font-weight:600">${tone === 'ok' ? ic('check', 13, 2) : ''} ${st}</span>${tone === 'ok' ? '<span></span>' : `<button class="btn ${ch === 'wa' ? 'primary' : ''}">${ch === 'wa' ? 'Show QR' : 'Sign in'}</button>`}</div>`).join('')}`).join('').replace(/<div class="loc-head">/, '<div class="loc-head" style="border-top:0">')}
    </div>
    <p style="margin:0;font-size:13px;color:var(--ink-2)">The app only reads who is waiting. It never sends a message, and nothing it reads leaves this PC.</p>
  </main>`),
});

