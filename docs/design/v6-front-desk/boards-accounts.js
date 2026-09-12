// ---- accounts and readers ---------------------------------------------------------------------------

const reads = seeded(40, 11).map((r, i) => (i > 33 ? 0 : Math.round(420 + r * 80)));
boards.push({
  group: 'accounts', phase: 'Phase 3 · Channel modules · health light', order: 3,
  title: 'A reader that stopped working',
  note: 'When Instagram changes its page overnight, the Instagram reader fails for all three locations at once, so it is reported once, as the reader, not as three broken accounts. WhatsApp keeps reading untouched beside it. Its figures are hidden rather than shown as zero, and the evidence is here for whoever fixes it.',
  html: app({ active: 'accounts', needs: 5 }, `<main class="main">
    <div class="headline"><div><h1>The Instagram reader stopped working</h1>
      <p>Since <b>10:14 am</b> every read of an Instagram inbox has come back in a shape the app does not recognise. <b>WhatsApp is unaffected</b> and still reading all three locations.</p></div>
      <div class="actions"><button class="btn">${ic('refresh', 14)}Try again now</button><button class="btn primary">${ic('export', 14)}Save a report for support</button></div></div>
    <div class="grid2" style="grid-template-columns:minmax(0,1.25fr) minmax(0,1fr)">
      <div class="panel"><h3>Reads, last 2 hours</h3><p class="sub" style="margin:-4px 0 10px">Chats found per read across the three Instagram accounts. A good read finds the same inbox again; these fell to nothing at 10:14.</p>
        <svg viewBox="0 0 640 170" width="100%" role="img" aria-label="Instagram chats per read over two hours">
          ${[0, 250, 500].map((v) => `<line x1="40" x2="630" y1="${140 - v * 0.24}" y2="${140 - v * 0.24}" stroke="var(--line)"/><text x="32" y="${144 - v * 0.24}" text-anchor="end" font-size="11" fill="var(--ink-3)">${v}</text>`).join('')}
          ${reads.map((v, i) => `<rect x="${44 + i * 14.6}" y="${v ? 140 - v * 0.24 : 132}" width="9" height="${v ? v * 0.24 : 8}" rx="2" fill="${v ? 'var(--ink)' : 'var(--m-late)'}" opacity="${v ? 0.55 : 1}"/>`).join('')}
          <line x1="${44 + 34 * 14.6 - 3}" x2="${44 + 34 * 14.6 - 3}" y1="10" y2="146" stroke="var(--ink)" stroke-width="1.5" stroke-dasharray="3 3"/>
          <text x="${44 + 34 * 14.6 - 8}" y="20" text-anchor="end" font-size="11.5" font-weight="600" fill="var(--ink)">10:14 am</text>
          ${['8:15', '8:45', '9:15', '9:45', '10:15'].map((t, i) => `<text x="${44 + i * 8.5 * 14.6}" y="162" font-size="11" fill="var(--ink-3)">${t}</text>`).join('')}
        </svg></div>
      <div class="panel"><h3>What the app did</h3><div class="tl-wrap"><div class="tl">
        <div class="tl-row"><time>10:14</time><i style="border-color:var(--m-late)"></i><div><b>First unrecognised read</b><span class="sub">F-11 Instagram. The page loaded and was signed in, but the inbox list had a new layout.</span></div></div>
        <div class="tl-row"><time>10:15</time><i style="border-color:var(--m-late)"></i><div><b>Same at DHA-2 and Men DHA-2</b><span class="sub">Three accounts, one cause. Reported as the reader.</span></div></div>
        <div class="tl-row"><time>10:16</time><i></i><div><b>Instagram figures hidden</b><span class="sub">Waiting counts for Instagram show “not reading” instead of 0.</span></div></div>
        <div class="tl-row"><time>10:16</time><i style="border-color:var(--m-ok)"></i><div><b>WhatsApp checked separately</b><span class="sub">148 good reads since, none failed.</span></div></div>
        <div class="tl-row"><time>12:10</time><i></i><div><b>Still retrying every 5 minutes</b><span class="sub">It will recover by itself if Instagram changes back.</span></div></div>
      </div></div></div>
    </div>
    <div class="readers"><div class="reader">${ic('ig', 18)}<span><b>Instagram reader</b><br><span class="sub">0 good reads of 23 since 10:14 am · last problem: inbox list not found</span></span><span class="chip late">${ic('alert', 12)}Not reading</span></div>
      <div class="reader">${ic('chat', 18)}<span><b>WhatsApp reader</b><br><span class="sub">148 good reads since 9:02 am, none failed</span></span><span class="chip ok">${ic('check', 12)}Working</span></div></div>
  </main>`),
});

boards.push({
  group: 'accounts', phase: 'Phase 2 · Foundation · lost-login record', order: 4,
  title: 'The record of a lost login',
  note: 'When an account signs itself out, the app keeps what it saw just before, so “why did DHA-2 go quiet last night?” has an answer. The last good read, the moment the QR code appeared, and what to check on the phone are all in one place.',
  html: app({ active: 'accounts', scope: 'DHA-2' }, `<main class="main">
    <div class="headline"><div><h1>DHA-2 WhatsApp signed out at 11:26 pm</h1>
      <p>It was reading normally until then. Nobody signed it out from this app. The usual cause is the phone removing this PC under <b>Linked devices</b>, or WhatsApp ending a link that had not been used from the phone for 14 days.</p></div>
      <div class="actions"><button class="btn primary">${ic('qr', 14)}Show QR code</button></div></div>
    <div class="grid2" style="grid-template-columns:minmax(0,1.3fr) minmax(0,1fr)">
      <div class="panel"><h3>Just before it happened</h3><div class="tl-wrap"><div class="tl">
        <div class="tl-row"><time>11:23 pm</time><i style="border-color:var(--m-ok)"></i><div><b>Good read</b><span class="sub">500 chats, 6 waiting. Nothing unusual.</span></div></div>
        <div class="tl-row"><time>11:24 pm</time><i style="border-color:var(--m-ok)"></i><div><b>Good read</b><span class="sub">500 chats, 6 waiting.</span></div></div>
        <div class="tl-row"><time>11:25 pm</time><i style="border-color:var(--m-due)"></i><div><b>Page reloaded itself</b><span class="sub">WhatsApp refreshed the page. The app did not ask it to.</span></div></div>
        <div class="tl-row"><time>11:26 pm</time><i style="border-color:var(--m-late)"></i><div><b>QR code on screen</b><span class="sub">The link-a-device screen replaced the chats. Marked Sign in needed; figures hidden.</span></div></div>
        <div class="tl-row"><time>Now</time><i></i><div><b>Still signed out, 9 h 36 min</b><span class="sub">Messages sent to DHA-2 since then are not counted anywhere.</span></div></div>
      </div></div></div>
      <div style="display:grid;gap:16px;align-content:start">
        <div class="panel"><h3>Check on the DHA-2 phone</h3><ol style="margin:0;padding-left:18px;font-size:13.5px;display:grid;gap:6px">
          <li>WhatsApp › Settings › Linked devices.</li><li>If this PC is missing, it was removed: scan the new QR code here.</li><li>If it is listed, remove it, then scan again.</li></ol></div>
        <div class="panel"><h3>This account’s logins</h3><dl class="kv"><dt>Signed in</dt><dd>2 Sept, 11:04 am</dd><dt>Lost</dt><dd>Once before, 20 Aug</dd><dt>Sessions</dt><dd>Kept on this PC only</dd></dl></div>
      </div>
    </div>
  </main>`),
});

boards.push({
  group: 'accounts', phase: 'Phase 6 · Cloud and membership · accounts sync to the workspace', order: 5,
  title: 'Add an account',
  note: 'Three choices and a name. The channel list says plainly what each channel measures today, so nobody adds Messenger expecting figures. The login itself happens next, in the account’s own page, and stays on this PC.',
  html: `<div style="position:relative">${app({ active: 'accounts' }, `<main class="main"><div class="headline"><div><h1>Accounts</h1><p>9 accounts at 3 locations.</p></div></div></main>`)}<div class="scrim"></div>
    <div class="dialog" style="width:600px" role="dialog" aria-label="Add an account">
      <h3>Add an account</h3>
      <div class="field"><span>Channel</span>
        <div style="display:grid;gap:6px">
          ${[['chat', 'WhatsApp or WhatsApp Business', 'Who is waiting, reply times, previews', true], ['ig', 'Instagram', 'Who is waiting in the inbox. Previews are shorter.', false], ['reviews', 'Google reviews', 'Rating, total and unanswered reviews', false], ['more', 'Messenger, Telegram, other page', 'Opens the page. No figures.', false]].map(([i, n, d, on]) => `<div style="display:grid;grid-template-columns:22px 1fr auto;gap:10px;align-items:center;padding:10px 12px;border:1px solid ${on ? 'var(--ink)' : 'var(--line-2)'};border-radius:9px;${on ? 'box-shadow:inset 0 0 0 1px var(--ink)' : ''}">${ic(i, 17)}<span><b style="font-weight:600">${n}</b><br><span class="sub">${d}</span></span>${on ? ic('check', 16, 2.2) : ''}</div>`).join('')}
        </div></div>
      <div class="grid2" style="gap:12px"><label class="field"><span>Name</span><span class="input">Men DHA-2 WhatsApp 2</span></label><label class="field"><span>Location</span><span class="input" style="justify-content:space-between">Men DHA-2 <span style="transform:rotate(90deg)">${ic('right', 13)}</span></span></label></div>
      <p class="sub">Next, the account’s page opens beside this list. Scan its QR code from the phone that owns the number. The login stays on this PC; other members sign in on their own.</p>
      <div class="foot"><button class="btn quiet">Cancel</button><button class="btn primary">Add and sign in</button></div>
    </div></div>`,
});
