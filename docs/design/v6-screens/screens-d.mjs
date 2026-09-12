import { band, chip, fig, ic, meter, shell, titleBar } from './lib.mjs';

// Before sign-in there is no rail and no board: the dark shell is the whole canvas, and one white sheet holds
// the only thing there is to do.
const bare = (inner, tb = { minimal: true }) =>
  `<div class="app">${titleBar(tb)}<div style="display:grid;place-items:center;min-height:0;background:var(--shell);padding:40px">${inner}</div></div>`;

const googleBtn = `<span class="btn lg" style="gap:10px;width:300px;background:#fff">${ic('google', 17, 'var(--brand-ink)', 1.6)}Continue with Google</span>`;

export const SignIn = () => bare(`<div style="display:grid;grid-template-columns:500px 380px;gap:72px;align-items:center">
  <div class="col" style="gap:20px">
    <div class="mark" style="width:34px;height:34px;border-radius:7px;font-size:16px">U</div>
    <h1 style="margin:0;font-size:40px;line-height:1.12;font-weight:600;letter-spacing:-.02em;color:var(--shell-ink)">Every customer waiting,<br>across every location.</h1>
    <p style="margin:0;font-size:14.5px;line-height:1.6;color:var(--shell-ink-2);max-width:30em">Unified Messenger watches your WhatsApp, Instagram and Google reviews, and shows you who needs an answer first. It never sends anything.</p>
    <div class="row" style="gap:18px;color:var(--shell-ink-2);font-size:12.5px;padding-top:4px">
      <span class="row" style="gap:6px">${ic('check', 14, '#5FBF8F')}Reads, never sends</span>
      <span class="row" style="gap:6px">${ic('check', 14, '#5FBF8F')}Customers stay on your PC</span>
    </div>
  </div>
  <div class="sheet" style="padding:28px;display:flex;flex-direction:column;gap:14px;align-items:center;box-shadow:0 24px 60px rgba(0,0,0,.35)">
    <h2 class="h2" style="font-size:17px">Sign in</h2>
    <span class="sub" style="text-align:center;font-size:12.5px">Your workspace and accounts follow you to any PC.</span>
    ${googleBtn}
    <span class="sub" style="font-size:11.5px;text-align:center">Opens your web browser. We ask Google for your name and email, nothing else.</span>
    <div class="rule" style="align-self:stretch"></div>
    <span class="sub" style="font-size:11.5px;text-align:center">Messages, customers and logins stay on this PC. <a href="#">What syncs</a></span>
  </div></div>`);

export const SignInBrowser = () => bare(`<div class="sheet" style="width:460px;padding:34px;display:flex;flex-direction:column;gap:13px;align-items:center;text-align:center">
  <div style="width:52px;height:52px;border-radius:26px;background:var(--wash);display:grid;place-items:center;color:var(--brand-ink)">${ic('out', 22)}</div>
  <h2 class="h2" style="font-size:17px">Finish signing in in your browser</h2>
  <span class="sub" style="font-size:12.5px">Google's sign-in page is open in your default browser. Come back here when it says you're done.</span>
  <div class="row" style="gap:8px;color:var(--ink-3);font-size:12.5px">${ic('refresh', 14)}<span>Waiting for Google…</span></div>
  <div class="row" style="gap:8px"><span class="btn">Open the page again</span><span class="btn ghost">Cancel</span></div></div>`);

export const CreateWorkspace = () => bare(`<div class="col" style="gap:22px;width:740px">
  <div class="col" style="gap:5px;align-items:center;text-align:center">
    <span class="avatar" style="width:40px;height:40px;border-radius:20px;background:var(--shell-3);color:var(--shell-ink);font-size:13px">AH</span>
    <h1 style="margin:0;font-size:26px;font-weight:600;color:var(--shell-ink);letter-spacing:-.015em">Welcome, Anfal</h1>
    <span style="color:var(--shell-ink-2);font-size:12.5px">anfal@glowsalons.pk · <a href="#" style="color:var(--shell-ink-2)">not you?</a></span></div>
  <div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px">
    <div class="sheet" style="padding:16px;display:flex;flex-direction:column;gap:12px">
      <div class="row">${ic('mail', 17, 'var(--brand-ink)')}<h3 class="h3">You have been invited</h3></div>
      <div class="row" style="padding:12px;border-radius:5px;background:var(--field)"><div class="col" style="gap:1px"><span style="font-weight:600">Glow Salons</span><span class="sub" style="font-size:12px">Invited by Nida R. · 7 accounts · 3 locations</span></div><span class="btn primary" style="margin-left:auto">Join</span></div>
      <span class="sub" style="font-size:12px">Your accounts appear straight away. You sign in to each one on this PC.</span></div>
    <div class="sheet" style="padding:16px;display:flex;flex-direction:column;gap:12px">
      <div class="row">${ic('building', 17, 'var(--brand-ink)')}<h3 class="h3">Start a new workspace</h3></div>
      <div class="col" style="gap:5px"><span class="label">Business name</span><div class="input" style="color:var(--ink)">Glow Salons</div></div>
      <div class="col" style="gap:5px"><span class="label">First location</span><div class="input">for example, DHA Phase 2</div></div>
      <span class="btn" style="align-self:flex-start">Create workspace</span></div>
  </div></div>`);

export const NewDevice = () => bare(`<div class="sheet" style="width:700px;padding:24px;display:flex;flex-direction:column;gap:16px">
  <div class="col" style="gap:4px"><h1 class="h1" style="font-size:21px">Your accounts are here. Sign in to each one on this PC.</h1>
  <span class="sub" style="font-size:12.5px">Glow Salons synced 7 accounts. Logins never move between PCs, so each one is scanned here once.</span></div>
  <div class="col" style="gap:6px">${meter(43, 'ok')}<span class="sub" style="font-size:12px">3 of 7 done</span></div>
  <div class="col" style="gap:0">
  ${[['chat', 'WhatsApp · Front desk', 'DHA Phase 2', 'ok'], ['insta', 'Instagram', 'DHA Phase 2', 'ok'], ['google', 'Google reviews', 'DHA Phase 2', 'ok'], ['chat', 'WhatsApp · Bookings', 'F-11 Markaz', 'next'],
     ['google', 'Google reviews', 'F-11 Markaz', 'todo'], ['chat', 'WhatsApp · Front desk', 'Gulberg', 'todo'], ['insta', 'Instagram', 'Gulberg', 'todo']].map(([i, a, l, s]) =>
    `<div class="row" style="padding:10px 2px;border-top:1px solid var(--line)">${ic(i, 17, 'var(--ink-2)')}<div class="col" style="gap:0"><span style="font-weight:600;font-size:12.5px">${a}</span><span class="sub" style="font-size:12px">${l}</span></div>
    <span style="margin-left:auto">${s === 'ok' ? chip('ok', 'Signed in', 'check') : s === 'next' ? `<span class="btn primary" style="height:28px">${ic('qr', 13)}Sign in</span>` : `<span class="btn" style="height:28px">Sign in</span>`}</span></div>`).join('')}
  </div>
  <div class="row"><span class="sub" style="font-size:12px">You can do the rest later; the app works with what is signed in.</span><span class="btn ghost" style="margin-left:auto">Do the rest later</span></div></div>`);

const OWNER_WS = [['Glow Salons', 'anfal@glowsalons.pk', 5, 7, 'Now', 'ok'], ['Crown Dental Clinics', 'admin@crowndental.pk', 12, 14, '4 minutes ago', 'ok'],
  ['Urban Fitness', 'ops@urbanfit.pk', 3, 4, '2 days ago', 'ok'], ['Bloom Florists', 'hello@bloomflorists.pk', 2, 2, '18 days ago', 'warn'], ['Northside Motors', 'it@northside.pk', 6, 9, '1 month ago', 'bad']];

const ownerShell = (content) => `<div class="app">${titleBar({ ws: 'Owner console', bell: 0 })}<div class="body">
<aside class="side"><div class="sec" style="margin-top:2px">Owner</div>
<div class="nav on">${ic('building', 15)}<span>Workspaces</span><span class="n mono">5</span></div>
<div class="nav">${ic('users', 15)}<span>All members</span><span class="n mono">28</span></div>
<div class="nav">${ic('chart', 15)}<span>Sign-ins</span></div>
<div class="side-foot"><div class="nav">${ic('right', 15)}<span>Back to my workspace</span></div></div></aside><main class="main">${content}</main></div></div>`;

export const OwnerWorkspaces = () => ownerShell(`
<div class="row" style="align-items:flex-end"><div class="col" style="gap:3px"><h1 class="h1">Workspaces</h1><span class="sub" style="font-size:12.5px">Visible to your account only · enforced by Firebase rules, not by this app</span></div>
<div class="input" style="margin-left:auto;width:270px">${ic('search', 14)}Search workspaces or emails</div></div>
${band(
  fig('Workspaces', '5', 'businesses', 'One suspended', 'neu'),
  fig('Members', '28', 'people', '19 active today', 'neu'),
  fig('Signed in today', '19', 'of 28', 'Normal for a Thursday', 'ok'),
  fig('Suspended', '1', 'workspace', 'Northside Motors', 'late'),
)}
<div class="sheet" style="overflow:hidden"><table class="table"><thead><tr><th>Workspace</th><th>Owner</th><th>Members</th><th>Accounts</th><th>Last active</th><th>Status</th><th></th></tr></thead><tbody>
${OWNER_WS.map(([n, o, m, a, s, k]) => `<tr><td style="font-weight:600">${n}</td><td class="sub" style="font-size:12.5px">${o}</td><td class="mono">${m}</td><td class="mono">${a}</td><td>${k === 'warn' ? chip('warn', s) : s}</td>
<td>${k === 'bad' ? chip('bad', 'Suspended', 'lock') : chip('ok', 'Active', 'check')}</td><td style="text-align:right"><span class="btn" style="height:26px">${k === 'bad' ? 'Restore' : 'Suspend'}</span></td></tr>`).join('')}
</tbody></table></div>
<div class="strip neu">${ic('info', 15)}<span>Firebase free plan · 1,940 of 50,000 reads and 212 of 20,000 writes used today.</span></div>`);

export const OwnerWorkspace = () => ownerShell(`
<div class="row"><span class="btn ghost" style="padding:0 4px">${ic('right', 14)}</span><div class="col" style="gap:0"><h1 class="h1" style="font-size:21px">Crown Dental Clinics</h1><span class="sub" style="font-size:12.5px">Created 3 August 2026 · owner admin@crowndental.pk</span></div>
${chip('ok', 'Active', 'check')}<span class="btn danger" style="margin-left:auto">${ic('lock', 14)}Suspend workspace</span></div>
<div class="sheet" style="overflow:hidden"><table class="table"><thead><tr><th>Member</th><th>Role</th><th>First signed in</th><th>Last seen</th><th>PCs</th><th>Status</th><th></th></tr></thead><tbody>
${[['admin@crowndental.pk', 'Owner', '3 Aug', '4 minutes ago', 2, 'ok'], ['reception.f7@gmail.com', 'Member', '4 Aug', 'Today', 1, 'ok'], ['dr.sana@crowndental.pk', 'Admin', '4 Aug', 'Yesterday', 1, 'ok'],
   ['frontdesk2@gmail.com', 'Member', '10 Aug', '9 days ago', 1, 'warn'], ['temp.staff@gmail.com', 'Member', '15 Aug', '20 Aug', 1, 'bad']].map(([e, r, f, l, p, k]) =>
  `<tr><td style="font-weight:600">${e}</td><td>${r}</td><td>${f}</td><td>${k === 'warn' ? chip('warn', l) : l}</td><td class="mono">${p}</td><td>${k === 'bad' ? chip('bad', 'Suspended', 'lock') : chip('ok', 'Active')}</td>
  <td style="text-align:right">${r === 'Owner' ? '' : `<span class="btn" style="height:26px">${k === 'bad' ? 'Restore' : 'Suspend'}</span>`}</td></tr>`).join('')}
</tbody></table></div>
<span class="sub" style="font-size:12px">Suspending blocks sign-in and locks the app on their PCs the next time they are online. Nothing on their PCs is deleted.</span>`);
