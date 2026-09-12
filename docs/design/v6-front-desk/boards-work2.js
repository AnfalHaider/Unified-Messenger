// ---- the day's work, continued ---------------------------------------------------------------------

boards.push({
  group: 'work', phase: 'Phase 4 · Screens · notes and saved replies soon after launch', order: 2,
  title: 'A chat, docked, with the customer beside it',
  note: 'Open chat brings the account’s real WhatsApp page in beside the line, already on Sara’s conversation. The panel on the right is kept on this PC: her earlier visits as the app saw them, a note and tags anyone at the desk can add, and saved replies that copy to the clipboard. The app still never sends.',
  html: app({ active: 'line' }, `<div class="split" style="grid-template-columns:400px minmax(0,1fr)">
    <main class="main" style="gap:14px;padding-right:16px">
      <div class="headline"><div><h1 style="font-size:28px">${PEOPLE.length} waiting</h1><p style="font-size:13.5px"><b class="late">${late} past target</b>, ${due} due soon</p></div></div>
      <div class="queue mini" style="border-radius:12px;border-bottom:1px solid var(--line)">${PEOPLE.slice(0, 11).map((p) => row(p, { mini: true, sel: p.name === 'Sara M.' })).join('')}</div>
    </main>
    <section class="dock" aria-label="F-11 WhatsApp">
      <div class="dock-bar">${ic('chat', 18)}<span class="who"><b>Sara M. · 0300 1234567</b><span>F-11 WhatsApp · waiting 38 min, 23 past target</span></span>
        <div class="actions"><button class="btn">${ic('check', 14)}Handled <kbd>H</kbd></button><button class="btn">${ic('snooze', 14)}Snooze <kbd>S</kbd></button><button class="btn quiet">${ic('x', 14)}Close</button></div></div>
      <div class="dock3">
        <div class="page-mock" style="grid-template-columns:230px minmax(0,1fr)">
          <div class="pm-list">${[['Sara M.', 'Can I move my 5pm to tomorrow?', '4:12', true], ['Rabia N.', 'Voice message', '4:26'], ['Hira A.', 'Any slot on Saturday?', '4:33'], ['Mahnoor S.', 'Is Nabila available?', '4:38'], ['Noor F.', 'Hydrafacial kitna ka hai?', '4:41'], ['Fatima Z.', 'Parking bhi hai?', '4:44']].map(([n, m, t, on]) => `<div class="pm-item ${on ? 'on' : ''}" style="grid-template-columns:34px 1fr auto"><span class="pm-av" style="width:34px;height:34px"></span><span><b style="font-weight:500">${n}</b><i style="max-width:110px">${m}</i></span><small style="opacity:.6">${t}</small></div>`).join('')}</div>
          <div class="pm-chat">
            <div class="bub">Hi, I have a 5pm with Nabila today<small>4:10 pm</small></div>
            <div class="bub">Can I move my 5pm to tomorrow? Same time if possible<small>4:12 pm</small></div>
            <div class="pm-compose"></div>
          </div>
        </div>
        <aside class="cust" aria-label="About Sara M.">
          <div><h4>Seen by the app</h4><div class="history">
            <div><span>Today</span><span>Waiting 38 min</span></div>
            <div><span>2 Sept</span><span>Answered in 6 min</span></div>
            <div><span>19 Aug</span><span>Answered in 41 min</span></div>
            <div><span>Since</span><span>July, 7 conversations</span></div></div></div>
          <div><h4>Tags</h4><div style="display:flex;gap:6px;flex-wrap:wrap"><span class="pill-tag">${ic('tag', 11)}Regular</span><span class="pill-tag">${ic('tag', 11)}Nabila</span><span class="pill-tag" style="color:var(--ink-3)">+ Add</span></div></div>
          <div><h4>Note, kept on this PC</h4><p style="margin:0;font-size:13px;line-height:1.5;padding:9px 10px;border:1px solid var(--line);border-radius:8px;background:var(--raised)">Prefers evening slots. Allergic to wax strips, uses sugaring.</p><span class="sub" style="font-size:11.5px">Edited by Front desk, Friday</span></div>
          <div><h4>Saved replies</h4><div class="saved">
            <div><span><b>Reschedule</b>Of course. Tomorrow at 5pm is free with Nabila…</span><button class="btn" style="height:28px">${ic('copy', 13)}Copy</button></div>
            <div><span><b>Prices</b>Our current price list is attached…</span><button class="btn" style="height:28px">${ic('copy', 13)}Copy</button></div>
            <div><span><b>Location</b>We are at Shop 4, F-11 Markaz…</span><button class="btn" style="height:28px">${ic('copy', 13)}Copy</button></div></div></div>
        </aside>
      </div>
    </section>
  </div>`),
});

boards.push({
  group: 'work', phase: 'Phase 4 · Screens', order: 3,
  title: 'Find anything: the command palette',
  note: 'Ctrl K from anywhere. Typing a name finds the customer on every account at once; the same box jumps to an account, a location, a report, or runs an action. Arrow keys and Enter, so the desk never needs the mouse.',
  html: `<div style="position:relative">${app({ active: 'line' }, `<main class="main">
    <div class="headline"><div><h1>${PEOPLE.length} customers are waiting</h1><p><b class="late">${late} are past your ${TARGET}-minute target</b>, ${due} more pass it in the next five minutes.</p></div></div>
    ${theLine()}</main>`)}
    <div class="scrim"></div>
    <div class="palette" role="dialog" aria-label="Command palette">
      <div class="q">${ic('search', 18)}<span>sa</span><span class="caret"></span><span style="margin-left:auto"><kbd>Esc</kbd></span></div>
      <h5>Customers</h5>
      <div class="pal-item on">${ic('chat', 16)}<span><b style="font-weight:600">Sa</b>ra M. <span class="sub">· F-11 WhatsApp · waiting 38 min</span></span><span class="sub"><kbd>Enter</kbd> open chat</span></div>
      <div class="pal-item">${ic('ig', 16)}<span><b style="font-weight:600">Sa</b>na Q. <span class="sub">· DHA-2 Instagram · waiting 19 min</span></span><span class="sub"></span></div>
      <div class="pal-item">${ic('chat', 16)}<span>Hi<b style="font-weight:600">sa</b>m A. <span class="sub">· Men DHA-2 WhatsApp · answered 2 h ago</span></span><span class="sub"></span></div>
      <h5>Go to</h5>
      <div class="pal-item">${ic('chart', 16)}<span>Weekly report, <b style="font-weight:600">Sa</b>turday to Friday</span><span class="sub">Reports</span></div>
      <div class="pal-item">${ic('grid', 16)}<span>Men DHA-2 WhatsApp</span><span class="sub">Accounts</span></div>
      <h5>Do</h5>
      <div class="pal-item">${ic('snooze', 16)}<span><b style="font-weight:600">S</b>nooze the selected chat for 1 hour</span><span class="sub"><kbd>S</kbd></span></div>
      <div class="pal-item" style="padding-bottom:12px">${ic('refresh', 16)}<span>Read every account now</span><span class="sub"><kbd>R</kbd></span></div>
    </div></div>`,
});

const setAside = [
  ['Snoozed', 'Maryam D.', 'DHA-2 WhatsApp', 'Running 10 min late, sorry', 'Back at 5:40 pm', 'Front desk, 4:40 pm'],
  ['Snoozed', 'Kinza W.', 'DHA-2 WhatsApp', 'Ok, and the address?', 'Back tomorrow 11:00 am', 'You, 4:12 pm'],
  ['Handled', 'Tariq S.', 'Men DHA-2 WhatsApp', 'Thank you bhai', 'Returns if they write again', 'You, 3:58 pm'],
  ['Handled', 'Laiba R.', 'F-11 Instagram', 'Reacted with a heart', 'Returns if they write again', 'Front desk, 2:31 pm'],
  ['Closed by rule', 'Nida K.', 'F-11 WhatsApp', 'ok thanks', 'Last message ended the chat', 'Automatic, 1:05 pm'],
  ['Closed by rule', 'Faraz M.', 'Men DHA-2 WhatsApp', 'Done 👍', 'Last message ended the chat', 'Automatic, 12:48 pm'],
];
boards.push({
  group: 'work', phase: 'Phase 4 · Screens', order: 4,
  title: 'Set aside: nothing disappears silently',
  note: 'Every chat that left the line without a reply from the app’s point of view, with who moved it and when. Snoozed chats come back on their own; handled ones return if the customer writes again; the “ended the chat” rule can be undone per chat. This is what keeps “19 waiting” trustworthy.',
  html: app({ active: 'line' }, `<main class="main">
    <div class="headline"><div><h1>Set aside today</h1><p>2 snoozed, 2 marked handled, and 41 closed by the “last message ended the chat” rule, of which 6 are shown.</p></div>
      <div class="actions"><div class="seg"><button aria-pressed="true">All</button><button>Snoozed 2</button><button>Handled 2</button><button>Closed by rule 41</button></div></div></div>
    <div class="panel" style="padding:0;overflow:hidden"><table class="table"><thead><tr><th>Why it left the line</th><th>Customer</th><th>Last message</th><th>What happens next</th><th>Moved by</th><th></th></tr></thead><tbody>
      ${setAside.map(([why, who, acct, msg, next, by]) => `<tr><td><span class="chip ${why === 'Snoozed' ? 'due' : 'neu'}">${ic(why === 'Snoozed' ? 'snooze' : why === 'Handled' ? 'check' : 'more', 12)}${why}</span></td><td><b style="font-weight:600">${who}</b><div class="sub">${acct}</div></td><td class="sub">${msg}</td><td>${next}</td><td class="sub">${by}</td><td class="r"><button class="btn">${ic('reopen', 13)}Put back</button></td></tr>`).join('')}
    </tbody></table></div>
    <p class="sub" style="margin:0">Marks are kept on this PC and shared with nobody. A chat put back returns to the line at the position its real wait time gives it.</p>
  </main>`),
});

boards.push({
  group: 'work', phase: 'Soon after launch · digests', order: 5,
  title: 'The morning digest',
  note: 'The first time the app opens each day it leads with what happened while the business was closed, before the live line. It names who wrote overnight, who is still owed a reply from yesterday, and how yesterday went at each location, then gets out of the way.',
  html: app({ active: 'line', needs: 1 }, `<main class="main" style="gap:18px">
    <div class="headline"><div><span class="phase" style="margin-bottom:6px">${ic('sunrise', 12)} Saturday 13 September, 9:02 am</span><h1>Good morning. 14 people wrote while you were closed.</h1>
      <p>Answer the 3 still owed from yesterday first. Yesterday <b>81%</b> were answered on time across all three locations, down from 86% the Friday before.</p></div>
      <div class="actions"><button class="btn primary">${ic('line', 14)}Go to the line</button></div></div>
    <div class="grid2" style="grid-template-columns:minmax(0,1.2fr) minmax(0,1fr)">
      <div class="panel" style="padding:0;overflow:hidden"><div style="padding:14px 18px 6px"><h3 style="margin:0;font:650 16px/1.2 var(--display)">Owed from yesterday</h3><span class="sub">Wrote before closing and never got a reply</span></div>
        <table class="table"><tbody>
          <tr><td><b style="font-weight:600">Zainab T.</b><div class="sub">DHA-2 WhatsApp · “Still waiting to hear if 3pm is confirmed”</div></td><td class="r late">since 8:40 pm</td></tr>
          <tr><td><b style="font-weight:600">Ayesha K.</b><div class="sub">F-11 Instagram · “Bridal package price?”</div></td><td class="r late">since 9:55 pm</td></tr>
          <tr><td><b style="font-weight:600">Bilal R.</b><div class="sub">Men DHA-2 WhatsApp · “Beard trim walk-in possible?”</div></td><td class="r late">since 10:31 pm</td></tr>
        </tbody></table>
        <div style="padding:12px 18px;border-top:1px solid var(--line)"><b style="font-weight:600">11 wrote overnight</b> <span class="sub">— 6 at F-11, 3 at DHA-2, 2 at Men DHA-2. The wait clock starts at opening time, 11:00 am.</span></div></div>
      <div class="panel"><h3>Yesterday, by location</h3>
        <table class="table"><thead><tr><th>Location</th><th class="r">On time</th><th>Last 14 days</th><th class="r">Median reply</th></tr></thead><tbody>
          <tr><td><b style="font-weight:600">F-11</b></td><td class="r due">78%</td><td>${spark([84, 86, 83, 88, 85, 82, 79, 81, 84, 80, 77, 83, 80, 78], { min: 60, max: 100 })}</td><td class="r">13 min</td></tr>
          <tr><td><b style="font-weight:600">DHA-2</b></td><td class="r due">80%</td><td>${spark([90, 88, 91, 87, 86, 89, 84, 85, 83, 86, 82, 84, 81, 80], { min: 60, max: 100 })}</td><td class="r">12 min</td></tr>
          <tr><td><b style="font-weight:600">Men DHA-2</b></td><td class="r ok">91%</td><td>${spark([86, 84, 88, 87, 89, 90, 88, 91, 89, 92, 90, 88, 93, 91], { min: 60, max: 100 })}</td><td class="r">7 min</td></tr>
        </tbody></table>
        <p class="sub" style="margin:10px 0 0">Aiming for 90%. F-11 has slipped four days running, mostly between 1 and 3 pm.</p></div>
    </div>
    <div class="panel" style="display:grid;grid-template-columns:auto 1fr auto;gap:14px;align-items:center">${ic('lock', 20)}<span><b style="font-weight:600">One thing needs you:</b> <span class="sub">DHA-2 WhatsApp lost its login at 11:26 pm. Its overnight messages are not counted above until it is signed in.</span></span><button class="btn">Show QR</button></div>
  </main>`),
});

boards.push({
  group: 'work', phase: 'Phase 4 · Screens · “about to breach” warnings', order: 8,
  title: 'About to breach, outside the app',
  note: 'A Windows notification two minutes before a customer passes the target, not after, and only while the location is open. It offers the two useful moves. Quiet hours and per-location choices live in Settings; the tray icon carries the count of people waiting.',
  html: `<div class="app" style="display:block;position:relative;background:none"><div class="desk"></div>
    <div style="position:absolute;left:70px;top:70px;width:860px;height:560px;border-radius:10px;background:#F4F5F7;box-shadow:0 20px 60px rgba(0,0,0,.4);opacity:.95;display:grid;place-items:center;color:#667;font:14px var(--text)">Another app you are working in</div>
    <div class="toast"><div style="display:flex;align-items:center;gap:8px;opacity:.8;font-size:12px"><span class="mark" style="width:16px;height:16px;font-size:9px;background:#E6E9E4;color:#121513">U</span>Unified Messenger · now</div>
      <b style="font-size:14px">Hira A. passes the 15-minute target in 2 minutes</b>
      <span style="opacity:.85">F-11 WhatsApp · “Any slot on Saturday morning?” · 3 others at F-11 are already late.</span>
      <div class="btns"><span>Open chat</span><span>Snooze 1 hour</span></div></div>
    <div class="taskbar"><span style="display:flex;align-items:center;gap:6px;background:rgba(255,255,255,.1);padding:4px 8px;border-radius:6px"><span class="mark" style="width:16px;height:16px;font-size:9px;background:#E6E9E4;color:#121513">U</span><b class="num">19</b></span><span>${ic('offline', 14).replace('M2.5 2.5l11 11', '')}</span><span class="num">4:58 pm<br>13/09/2026</span></div>
  </div>`,
});
