// ---- workspace and membership ----------------------------------------------------------------------

const bare = (inner, { needs = 0 } = {}) => `<div class="app"><header class="tb"><div class="mark">U</div><span class="tb-name">Unified Messenger</span><div class="tb-right">${theme3()}<div class="win"><span>${ic('min', 14)}</span><span>${ic('max', 13)}</span><span>${ic('x', 14)}</span></div></div></header>${inner}</div>`;
const google = `<svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true"><path fill="#4285F4" d="M17.6 9.2c0-.6-.1-1.2-.2-1.7H9v3.3h4.8a4.1 4.1 0 0 1-1.8 2.7v2.2h2.9c1.7-1.6 2.7-3.9 2.7-6.5z"/><path fill="#34A853" d="M9 18c2.4 0 4.5-.8 5.9-2.2L12 13.6c-.8.5-1.8.9-3 .9-2.3 0-4.3-1.6-5-3.7H1v2.3A9 9 0 0 0 9 18z"/><path fill="#FBBC05" d="M4 10.8a5.4 5.4 0 0 1 0-3.5V5H1a9 9 0 0 0 0 8.1z"/><path fill="#EA4335" d="M9 3.6c1.3 0 2.5.5 3.4 1.3L15 2.4A9 9 0 0 0 1 5l3 2.3c.7-2.1 2.7-3.7 5-3.7z"/></svg>`;

boards.push({
  group: 'workspace', phase: 'Phase 6 · Cloud and membership', order: 1,
  title: 'Sign in with Google',
  note: 'The first screen on a new install. Sign-in happens in the normal browser, where the Google account is already signed in, and the app asks Google for a name and email only. The page says, before anyone clicks, what goes to the cloud and what never does.',
  html: bare(`<div class="lock"><div class="lock-card">
    <span class="mark" style="width:44px;height:44px;font-size:22px;border-radius:12px">U</span>
    <h1>Sign in to see all your business’s messages in one place</h1>
    <p>Your Google account tells the app which business workspace you belong to. Your WhatsApp, Instagram and Google logins are then made on this PC.</p>
    <button class="btn primary" style="height:44px;font-size:14.5px;justify-content:center;gap:10px">${google}Continue with Google</button>
    <span class="sub">Opens your browser. Unified Messenger asks Google for your name and email address, nothing else.</span>
    <div class="grid2" style="gap:12px;margin-top:10px">
      <div class="panel"><h3 style="display:flex;gap:8px;align-items:center">${ic('cloud', 16)}Kept in the workspace</h3><p class="sub" style="margin:0">Who is a member, and the list of accounts, locations and settings, so a new PC is ready in minutes.</p></div>
      <div class="panel"><h3 style="display:flex;gap:8px;align-items:center">${ic('shield', 16)}Never leaves this PC</h3><p class="sub" style="margin:0">Messages, customer names, reply times, notes, assistant chats and the WhatsApp logins themselves.</p></div>
    </div>
  </div></div>`),
});

const members = [
  ['Anfal Haider', 'you@example.com', 'Admin', 'Office PC, Laptop', 'Now', 'ok'],
  ['Front desk F-11', 'desk.f11@example.com', 'Member', 'F-11 reception', '6 min ago', 'ok'],
  ['Front desk DHA-2', 'desk.dha2@example.com', 'Member', 'DHA-2 reception', '1 h ago', 'ok'],
  ['Men DHA-2 manager', 'men.dha2@example.com', 'Member', 'Manager laptop', 'Yesterday', 'ok'],
  ['Sadia (left in August)', 'sadia.k@example.com', 'Removed', 'F-11 back office', 'Logins wiped 28 Aug', 'neu'],
];
boards.push({
  group: 'workspace', phase: 'Phase 6 · Cloud and membership', order: 2,
  title: 'Members of the workspace',
  note: 'Who can use Depilex’s setup, on which PCs, and when each was last online. Admins invite by email and remove people; members just work. A removed person stays listed with the date their logins were wiped, so the question “did that actually happen?” has an answer.',
  html: app({ active: 'settings' }, `<main class="main">
    <div class="headline"><div><h1>Depilex workspace</h1><p>4 members on 5 PCs. Accounts, locations and settings stay in step across all of them.</p></div>
      <div class="actions"><button class="btn primary">${ic('users', 14)}Invite someone</button></div></div>
    <div class="settings">
      <nav class="stabs"><button>Look and reading</button><button>Opening hours</button><button>Notifications</button><button>Assistant</button><button aria-current="page">Workspace</button><button>Privacy</button><button>About</button></nav>
      <div style="display:grid;gap:18px;align-content:start;max-width:none">
        <div class="panel" style="padding:0;overflow:hidden"><table class="table"><thead><tr><th>Member</th><th>Role</th><th>PCs</th><th>Last online</th><th></th></tr></thead><tbody>
          ${members.map(([n, e, role, pcs, seen, tone]) => `<tr style="${tone === 'neu' ? 'opacity:.7' : ''}"><td><b style="font-weight:600">${n}</b><div class="sub">${e}</div></td><td>${role === 'Removed' ? '<span class="chip neu">Removed</span>' : `<span class="chip ${role === 'Admin' ? 'ok' : 'neu'}">${role}</span>`}</td><td class="sub">${pcs}</td><td>${seen}</td><td class="r">${role === 'Removed' ? '' : `<button class="btn quiet">${ic('more', 14)}</button>`}</td></tr>`).join('')}
          <tr><td><b style="font-weight:600">ayesha.ops@example.com</b><div class="sub">Invited 2 days ago</div></td><td><span class="chip due">Invite waiting</span></td><td class="sub">Not signed in yet</td><td>—</td><td class="r"><button class="btn">Resend</button></td></tr>
        </tbody></table></div>
        <div class="grid2" style="gap:14px">
          <div class="panel"><h3>If a PC goes offline for a week</h3><p class="sub" style="margin:0">After 7 days without checking in, the app asks that PC to reconnect before it shows anything. A removed member cannot keep reading by staying offline.</p></div>
          <div class="panel"><h3>What syncs</h3><p class="sub" style="margin:0">9 accounts, 3 locations, opening hours, holidays, targets and saved replies. Last synced 20 seconds ago. Customer data never syncs.</p></div>
        </div>
      </div>
    </div>
  </main>`),
});

boards.push({
  group: 'workspace', phase: 'Phase 6 · Cloud and membership · leavers', order: 3,
  title: 'Removing someone who left',
  note: 'The dialog says exactly what removal does and what it cannot do. The next time that PC is online it signs out and wipes every login saved on it. If the PC stays offline, only the phones can cut it off, so the dialog names that step instead of implying the job is done.',
  html: `<div style="position:relative">${app({ active: 'settings' }, `<main class="main"><div class="headline"><div><h1>Depilex workspace</h1><p>4 members on 5 PCs.</p></div></div></main>`)}<div class="scrim"></div>
    <div class="dialog" role="dialog" aria-label="Remove member" style="width:560px">
      <h3>Remove Front desk DHA-2?</h3>
      <p>desk.dha2@example.com loses access to the Depilex workspace on every PC they use.</p>
      <div class="panel" style="display:grid;gap:10px">
        <div class="check" style="border:0;padding:0"><span>${ic('check', 16)}</span><span><b>When DHA-2 reception is next online</b><span>The app signs out and wipes the WhatsApp, Instagram and Google logins saved on that PC. It was last online 1 hour ago.</span></span></div>
        <div class="check" style="padding:10px 0 0"><span style="color:var(--due)">${ic('alert', 16)}</span><span><b>To cut access right now</b><span>On the DHA-2 phone: WhatsApp › Linked devices › remove “DHA-2 reception”. Do the same for Instagram’s login activity.</span></span></div>
      </div>
      <p class="sub">Their history on that PC is wiped with the logins. Figures on your own PCs are not affected.</p>
      <div class="foot"><button class="btn quiet">Cancel</button><button class="btn danger">Remove and wipe logins</button></div>
    </div></div>`,
});

boards.push({
  group: 'workspace', phase: 'Phase 6 · Cloud and membership · leavers', order: 4,
  title: 'On the PC of someone removed',
  note: 'What the removed person sees the next time their app goes online. The wipe has already happened before this screen is drawn, and it says so plainly, without blame, and points to the one thing they might legitimately need: asking an admin.',
  html: bare(`<div class="lock"><div class="lock-card">
    <span style="width:52px;height:52px;border-radius:50%;background:var(--hover);display:grid;place-items:center;box-shadow:inset 0 0 0 1px var(--line-2)">${ic('lock', 24)}</span>
    <h1>This PC is no longer part of the Depilex workspace</h1>
    <p>An admin removed <b>desk.dha2@example.com</b> on 13 September at 10:41 am. The WhatsApp, Instagram and Google logins saved on this PC, and the history the app kept here, were wiped at 10:44 am.</p>
    <div class="panel"><dl class="kv"><dt>Logins wiped</dt><dd>DHA-2 WhatsApp, DHA-2 Instagram, DHA-2 Google</dd><dt>History wiped</dt><dd>Waiting times, notes and assistant chats on this PC</dd><dt>Still here</dt><dd>The app itself, signed out</dd></dl></div>
    <p class="sub">If this is a mistake, ask a Depilex admin to invite this email again.</p>
    <div style="display:flex;gap:8px"><button class="btn">Sign in with a different account</button><button class="btn quiet">Close the app</button></div>
  </div></div>`),
});

const spaces = [
  ['Depilex', 'Anfal Haider', 4, 5, 'Now', 'Active'],
  ['Northside Pharmacy', 'Mehreen A.', 2, 2, '3 h ago', 'Active'],
  ['Clifton Auto Service', 'Z. Siddiqui', 3, 3, 'Yesterday', 'Active'],
  ['Trial: Brightway Tutors', 'owner@example.com', 1, 1, '19 days ago', 'Suspended'],
];
boards.push({
  group: 'workspace', phase: 'Phase 6 · Cloud and membership · Owner screen', order: 6,
  title: 'The owner console',
  note: 'Shown only to the product owner’s Google account. Every workspace, how many members and PCs, when it was last seen, and a switch to suspend or restore it. It reads membership and last-seen only: no workspace’s customer data exists in the cloud to show. The other workspaces here are invented.',
  html: app({ active: 'settings' }, `<main class="main">
    <div class="headline"><div><span class="phase" style="margin-bottom:6px">${ic('key', 12)} Owner only</span><h1>All workspaces</h1><p>4 workspaces, 10 members, 11 PCs. Free plan usage today: 1,240 of 50,000 reads.</p></div>
      <div class="actions"><div class="field" style="grid-template-columns:auto"><span class="input" style="width:260px;gap:8px;color:var(--ink-3)">${ic('search', 14)}Find a workspace or email</span></div></div></div>
    <div class="panel" style="padding:0;overflow:hidden"><table class="table"><thead><tr><th>Workspace</th><th>Admin</th><th class="r">Members</th><th class="r">PCs</th><th>Last seen</th><th>Status</th><th></th></tr></thead><tbody>
      ${spaces.map(([w, a, m, p, seen, st]) => `<tr><td><b style="font-weight:600">${w}</b></td><td class="sub">${a}</td><td class="r">${m}</td><td class="r">${p}</td><td>${seen}</td><td>${st === 'Active' ? '<span class="chip ok">Active</span>' : '<span class="chip late">Suspended</span>'}</td><td class="r"><button class="btn ${st === 'Active' ? '' : 'primary'}">${st === 'Active' ? 'Suspend' : 'Restore'}</button></td></tr>`).join('')}
    </tbody></table></div>
    <div class="grid2">
      <div class="panel"><h3>Clifton Auto Service</h3><dl class="kv"><dt>Created</dt><dd>4 August</dd><dt>Members</dt><dd>Z. Siddiqui (admin), 2 members</dd><dt>Accounts</dt><dd>5, at 2 locations</dd><dt>App versions</dt><dd>6.0.2 on all 3 PCs</dd></dl></div>
      <div class="panel"><h3>What suspending does</h3><p class="sub" style="margin:0">Every PC in the workspace locks at its next check, within a day, and shows who to contact. Logins are kept, so restoring brings it straight back. Enforced by the database rules, not by the app, so it cannot be skipped.</p></div>
    </div>
  </main>`),
});

boards.push({
  group: 'workspace', phase: 'Phase 6 · Cloud and membership · suspension', order: 7,
  title: 'A suspended workspace',
  note: 'What every PC in a suspended workspace shows at its next check. Unlike removal, nothing is wiped: logins and history wait on the PC, and the screen says so, so a paused trial or an unpaid month is not a lost setup.',
  html: bare(`<div class="lock"><div class="lock-card">
    <span style="width:52px;height:52px;border-radius:50%;background:var(--due-w);color:var(--due);display:grid;place-items:center">${ic('alert', 24)}</span>
    <h1>The Brightway Tutors workspace is paused</h1>
    <p>Unified Messenger was suspended for this workspace on 25 August. Nothing on this PC has been deleted: the logins and history are kept, and everything returns as it was once the workspace is restored.</p>
    <div class="panel"><dl class="kv"><dt>Workspace</dt><dd>Trial: Brightway Tutors</dd><dt>Admin</dt><dd>owner@example.com</dd><dt>What to do</dt><dd>Ask the workspace admin to contact Unified Messenger</dd></dl></div>
    <div style="display:flex;gap:8px"><button class="btn primary">${ic('refresh', 14)}Check again</button><button class="btn quiet">Sign out</button></div>
  </div></div>`),
});
