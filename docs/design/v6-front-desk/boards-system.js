// ---- settings, continued ---------------------------------------------------------------------------

const stabs = (active) => `<nav class="stabs">${['Look and reading', 'Opening hours', 'Notifications', 'Assistant', 'Workspace', 'Privacy', 'About'].map((t) => `<button aria-current="${t === active ? 'page' : 'false'}">${t}</button>`).join('')}</nav>`;
const settingsPage = (active, inner, sub = 'Kept on this PC, and shared with your workspace where marked.') => app({ active: 'settings' }, `<main class="main"><div class="headline"><div><h1>Settings</h1><p>${sub}</p></div></div><div class="settings">${stabs(active)}<div style="display:grid;gap:20px;align-content:start">${inner}</div></div></main>`);

boards.push({
  group: 'settings', phase: 'Soon after launch · hours and holidays per location', order: 2,
  title: 'Opening hours and holidays',
  note: 'The reply clock only runs while a location is open, so these hours decide what “late” means. Each location keeps its own week, and holidays pause the clock for the whole day. Shared with the workspace, so every PC agrees.',
  html: settingsPage('Opening hours', `
    <div class="sgroup"><div style="display:flex;align-items:center;gap:12px"><h3>Opening hours</h3><div class="seg" style="margin-left:auto"><button aria-pressed="true">F-11</button><button>DHA-2</button><button>Men DHA-2</button></div></div>
      <p>${ic('cloud', 13)} Shared with the Depilex workspace. The clock pauses outside these hours.</p>
      <div class="panel"><div class="hours">
        ${[['Monday', '11:00 am', '9:00 pm'], ['Tuesday', '11:00 am', '9:00 pm'], ['Wednesday', '11:00 am', '9:00 pm'], ['Thursday', '11:00 am', '9:00 pm'], ['Friday', '2:30 pm', '9:30 pm', 'Opens after Jummah'], ['Saturday', '11:00 am', '10:00 pm'], ['Sunday', '12:00 pm', '10:00 pm']].map(([d, o, c, n]) => `<b style="font-weight:600">${d}</b><span class="input">${o}</span><span class="input">${c}</span><span class="sub">${n || ''}</span>`).join('')}
      </div></div></div>
    <div class="sgroup"><h3>Holidays</h3><p>All three locations closed. Customers who write on these days start waiting at the next opening.</p>
      <div class="panel" style="padding:0">
        ${[['12 Rabi ul Awal', 'Sunday 7 September', 'Passed'], ['Quaid-e-Azam Day', 'Thursday 25 December', 'All locations'], ['New Year’s Day', 'Thursday 1 January', 'F-11 and DHA-2 only']].map(([n, d, w], i) => `<div class="srow" style="${i === 0 ? 'opacity:.6' : ''}"><span><b>${n}</b><span>${d}</span></span><span class="sub">${w}</span></div>`).join('')}
        <div class="srow"><span class="sub">Add a holiday or a closed afternoon</span><button class="btn">${ic('cal', 14)}Add</button></div>
      </div></div>`),
});

boards.push({
  group: 'settings', phase: 'Phase 4 · Screens · alerts, quiet hours and digests', order: 3,
  title: 'Notifications',
  note: 'A short list of alerts, each one a thing worth being interrupted for, with the choice of which locations it covers. Quiet hours stop all of them. The digest and the weekly report are chosen here too, so nothing arrives that nobody asked for.',
  html: settingsPage('Notifications', `
    <div class="sgroup"><h3>Tell me when</h3>
      <div class="panel" style="padding:0">
        ${[['A customer is about to pass the target', '2 minutes before, while the location is open', 'All locations', true], ['Someone has waited over an hour', 'Once per customer', 'All locations', true], ['An account needs signing in again', 'As soon as the app notices', 'All locations', true], ['A channel reader stops working', 'After 3 failed reads in a row', 'All locations', true], ['A one- or two-star review arrives', 'Within an hour of it appearing', 'F-11, DHA-2', false], ['A missed call has not been returned', 'After 30 minutes', 'Men DHA-2', false]].map(([t, d, where, on]) => `<div class="srow" style="grid-template-columns:minmax(0,1fr) 150px auto"><span><b>${t}</b><span>${d}</span></span><span class="sub">${where}</span><span class="toggle ${on ? '' : 'off'}"></span></div>`).join('')}
      </div></div>
    <div class="grid2" style="gap:16px">
      <div class="sgroup"><h3>Quiet hours</h3><div class="panel" style="padding:0"><div class="srow"><span><b>No alerts between</b><span>The tray count still updates.</span></span><span class="sub num">10:30 pm – 10:30 am</span></div><div class="srow"><span><b>Also on holidays</b><span>From Opening hours</span></span><span class="toggle"></span></div></div></div>
      <div class="sgroup"><h3>Summaries</h3><div class="panel" style="padding:0"><div class="srow"><span><b>Morning digest</b><span>When the app first opens each day</span></span><span class="toggle"></span></div><div class="srow"><span><b>Weekly report</b><span>Monday, 10:00 am, as a PDF</span></span><span class="toggle"></span></div></div></div>
    </div>`),
});

boards.push({
  group: 'settings', phase: 'Phase 4 · Screens', order: 4,
  title: 'Privacy and data on this PC',
  note: 'What the app keeps, where, and for how long, with a size beside each so it is concrete. The support log is shown for what it is: counts and timings only, safe to send. Every wipe action names exactly what it removes.',
  html: settingsPage('Privacy', `
    <div class="sgroup"><h3>Kept on this PC</h3>
      <div class="panel" style="padding:0;overflow:hidden"><table class="table"><thead><tr><th>What</th><th>Kept for</th><th class="r">Size</th><th></th></tr></thead><tbody>
        ${[['Account logins', 'Until you sign out or wipe', '486 MB', 'Wipe an account'], ['Who was waiting, reply times', '90 days, then summarised', '38 MB', 'Clear history'], ['Notes, tags and saved replies', 'Until deleted', '0.2 MB', ''], ['Assistant conversations', '30 days', '1.1 MB', 'Clear'], ['Assistant model', 'Until the assistant is turned off', '3.3 GB', 'Remove']].map(([w, k, s, a]) => `<tr><td><b style="font-weight:600">${w}</b></td><td class="sub">${k}</td><td class="r">${s}</td><td class="r">${a ? `<button class="btn quiet">${a}</button>` : ''}</td></tr>`).join('')}
      </tbody></table></div></div>
    <div class="grid2" style="gap:16px">
      <div class="panel"><h3>Sent off this PC</h3><p class="sub" style="margin:0">Your name and email to Google at sign-in, and the workspace setup to Depilex’s workspace: accounts, locations, hours, targets and saved replies. No messages, customers, figures or logins. No analytics, no crash reports.</p></div>
      <div class="panel"><h3>The support log</h3><p class="sub" style="margin:0 0 10px">app.log holds counts and timings only, never a name, number or message, so it can be sent to support as it is.</p><div style="display:flex;gap:8px"><button class="btn">${ic('open', 14)}Show the file</button><button class="btn quiet">What is in it</button></div></div>
    </div>`),
});

// ---- shipping and system states -------------------------------------------------------------------------

boards.push({
  group: 'system', phase: 'Phase 7 · Ship v6 · upgrade from v5', order: 1,
  title: 'Moving from the previous version',
  note: 'The one screen an existing customer sees when v6 replaces v5. It confirms what came across, is honest that WhatsApp will likely need one more QR scan per number, and explains why in a sentence rather than letting it look like a fault.',
  html: bare(`<div class="lock"><div class="lock-card" style="width:680px">
    <span class="phase">${ic('download', 12)} Unified Messenger 6.0</span>
    <h1>Everything came across. Each WhatsApp number needs one more scan.</h1>
    <p>The new version keeps its logins separately from the old one, so WhatsApp treats this PC as a new linked device. It is a one-time step, about 30 seconds per number, done from each phone.</p>
    <div class="panel" style="padding:0;overflow:hidden"><table class="table"><tbody>
      ${[['9 accounts at 3 locations', 'Names, locations, opening hours, targets', 'ok', 'Moved'], ['2,710 chats and 453 reply times', 'So this week’s reports are complete from day one', 'ok', 'Moved'], ['Handled and snoozed chats', '14 marks, with their times', 'ok', 'Moved'], ['2 WhatsApp logins', 'F-11 and Men DHA-2 carried over and are reading', 'ok', 'Moved'], ['4 logins', 'DHA-2 WhatsApp and the three Instagram accounts', 'due', 'Sign in once']].map(([w, d, t, s]) => `<tr><td><b style="font-weight:600">${w}</b><div class="sub">${d}</div></td><td class="r ${t}">${t === 'ok' ? ic('check', 13, 2) : ''} ${s}</td></tr>`).join('')}
    </tbody></table></div>
    <div style="display:flex;gap:8px"><button class="btn primary">Sign in the 4 accounts</button><button class="btn quiet">Later</button></div>
    <span class="sub">The previous version is left untouched until you uninstall it.</span>
  </div></div>`),
});

boards.push({
  group: 'system', phase: 'Phase 7 · Ship v6 · automatic updates', order: 2,
  title: 'An update, when it suits the desk',
  note: 'Updates download quietly and never restart the app in the middle of the day. The title bar gets a small “Restart to update” pill; the notes say in plain words what changed for the person using it, not for the developer.',
  html: `<div style="position:relative">${app({ active: 'line' }, `<main class="main">
    <div class="headline"><div><h1>${PEOPLE.length} customers are waiting</h1><p><b class="late">${late} are past your ${TARGET}-minute target</b>, ${due} more pass it in the next five minutes.</p></div></div>${theLine()}</main>`)}
    <div class="drawer" style="top:52px;right:180px;width:400px" role="dialog" aria-label="Update ready"><div style="display:flex;align-items:center;gap:10px">${ic('download', 18)}<h3>Version 6.1 is ready</h3></div>
      <ul style="margin:0;padding-left:18px;display:grid;gap:6px;font-size:13.5px"><li>Missed calls now show whether a customer wrote after calling.</li><li>The Instagram reader copes with the new inbox layout.</li><li>Reports export to CSV with location names.</li></ul>
      <p class="sub" style="margin:0">Restarting takes about 10 seconds. Logins and waiting customers are kept.</p>
      <div style="display:flex;gap:8px"><button class="btn primary">${ic('refresh', 14)}Restart now</button><button class="btn">When the business closes</button></div></div></div>`,
});

boards.push({
  group: 'system', phase: 'Phase 4 · Screens · states', order: 3,
  title: 'Offline',
  note: 'With no internet the pages cannot receive messages, so the numbers freeze. The line fades to show it is a snapshot, the headline says when it was taken, and Read now is replaced by what will happen when the connection returns. Nothing claims to be live.',
  html: app({ active: 'line' }, `<main class="main">
    <div class="panel" style="display:grid;grid-template-columns:auto 1fr auto;gap:14px;align-items:center;background:var(--due-w);border-color:transparent">${ic('offline', 20)}<span><b style="font-weight:600">This PC is offline since 4:52 pm.</b> <span class="sub">Figures below are from then. Reading starts again by itself as soon as the connection returns.</span></span><span class="sub num">Offline 6 min</span></div>
    <div class="headline"><div><h1>${PEOPLE.length} were waiting at 4:52 pm</h1><p>New messages since then cannot be seen until the connection is back, so the real number may be higher.</p></div></div>
    <div style="opacity:.55;filter:grayscale(.6)">${theLine()}</div>
    <p class="sub" style="margin:0">Waiting times keep counting from what was last seen, so the order of who to answer first stays right.</p>
  </main>`),
});
