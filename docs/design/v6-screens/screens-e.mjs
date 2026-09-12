import { chip, dialog, ic, shell } from './lib.mjs';
import { commandCenterContent } from './screens-a.mjs';

const behind = (overlay, active = 'center') =>
  shell({ active, content: `<div class="ghost-bg col" style="gap:16px;flex:1;min-height:0">${commandCenterContent({ strip: '' })}</div>`, overlay });

const field = (label, value, hint = '') => `<div class="col" style="gap:5px"><span class="label">${label}</span><div class="input" style="color:var(--ink)">${value}</div>${hint ? `<span class="sub" style="font-size:12px">${hint}</span>` : ''}</div>`;

export const AddAccount = () => behind(dialog({
  icon: 'plus', title: 'Add an account', body: `
<span>Pick a channel. You sign in to it inside the app next.</span>
<div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px">
${[['chat', 'WhatsApp', 'Who is waiting, reply times, previews', true], ['chat', 'WhatsApp Business', 'The same, for Business app numbers'], ['insta', 'Instagram', 'Who is waiting in your DMs'], ['google', 'Google reviews', 'Ratings and reviews to answer'],
  ['chat', 'Messenger', 'Unread count only'], ['out', 'Other website', 'Opens a site. No figures.']].map(([i, t, s, on]) =>
  `<div class="row" style="align-items:flex-start;padding:11px;border-radius:5px;border:1px solid ${on ? 'var(--brand-ink)' : 'var(--line)'};${on ? 'background:var(--wash)' : ''}">${ic(i, 17, 'var(--ink-2)')}<div class="col" style="gap:1px"><span style="font-weight:600;color:var(--ink);font-size:12.5px">${t}</span><span style="font-size:12px">${s}</span></div></div>`).join('')}
</div>
${field('Location', 'F-11 Markaz ' + ic('down', 13), 'Saved to Glow Salons, so every member sees this account.')}`,
  actions: `<span class="btn ghost">Cancel</span><span class="btn primary">Continue</span>`,
}));

export const EditAccount = () => behind(dialog({
  icon: 'edit', title: 'Edit account', body: `
${field('Name', 'WhatsApp · Bookings')}${field('Location', 'F-11 Markaz ' + ic('down', 13))}
<div class="col" style="gap:5px"><span class="label">Icon</span><div class="row">${['chat', 'phone', 'building', 'star'].map((i, k) => `<span style="width:36px;height:36px;border-radius:5px;display:grid;place-items:center;border:1px solid ${k === 0 ? 'var(--brand-ink)' : 'var(--line)'};color:var(--ink-2)">${ic(i, 17)}</span>`).join('')}<span class="btn ghost">Use the account's own picture</span></div></div>
<div class="row" style="padding-top:12px;border-top:1px solid var(--line)"><div class="col" style="gap:1px"><span style="font-weight:600;color:var(--ink);font-size:12.5px">Keep this account awake</span><span class="sub" style="font-size:12px">It feeds your figures, so sleeping it would stop them</span></div><span class="toggle on" style="margin-left:auto"></span></div>`,
  actions: `<span class="btn ghost" style="margin-right:auto;color:var(--late)">${ic('trash', 14)}Delete account</span><span class="btn ghost">Cancel</span><span class="btn primary">Save</span>`,
}));

export const InviteMember = () => behind(dialog({
  icon: 'mail', title: 'Invite to Glow Salons', body: `
${field('Email', 'hamza.bookings@gmail.com', 'They sign in with this Google account.')}
<div class="col" style="gap:5px"><span class="label">Role</span><div class="seg" style="align-self:flex-start"><span class="on">Member</span><span>Admin</span></div>
<span class="sub" style="font-size:12px">Members see every account. Admins can also add accounts, and invite or remove people.</span></div>`,
  actions: `<span class="btn ghost">Cancel</span><span class="btn primary">Send invite</span>`,
}), 'settings');

export const RemoveMember = () => behind(dialog({
  icon: 'alert', iconColor: 'var(--late)', title: 'Remove Saba B.?', body: `
<span>Saba loses access to Glow Salons straight away.</span>
<div class="col" style="gap:9px;padding:12px;border-radius:5px;background:var(--field)">
 <div class="row" style="align-items:flex-start">${ic('check', 15, 'var(--ok)')}<span>The next time her PC is online, the app signs her out and wipes every account login on it.</span></div>
 <div class="row" style="align-items:flex-start">${ic('alert', 15, 'var(--due)')}<span>If that PC stays offline, its WhatsApp logins keep working. Remove it now under WhatsApp, Linked devices, on each business phone.</span></div>
</div>`,
  actions: `<span class="btn ghost">Cancel</span><span class="btn danger">Remove and wipe</span>`,
}), 'settings');

export const SuspendWorkspace = () => `<div style="position:relative">${behind(dialog({
  icon: 'lock', iconColor: 'var(--late)', title: 'Suspend Northside Motors?', body: `
<span>All 6 members are locked out. Their apps show "Membership suspended" and stop reading accounts the next time they are online.</span>
<span>Nothing is deleted, and you can restore the workspace at any time.</span>
${field('Reason (only you see this)', 'Payment not received for August')}`,
  actions: `<span class="btn ghost">Cancel</span><span class="btn danger">Suspend workspace</span>`,
}))}</div>`;

export const DeleteAccount = () => behind(dialog({
  icon: 'trash', iconColor: 'var(--late)', title: 'Delete WhatsApp · Bookings?', body: `
<span>This removes the account from Glow Salons for every member, wipes its login on this PC, and deletes its history here.</span>
<div class="row" style="align-items:flex-start;padding:11px;border-radius:5px;background:var(--field)">${ic('info', 15, 'var(--ink-3)')}<span style="font-size:12.5px">Also remove this PC under WhatsApp, Linked devices, on the business phone.</span></div>
${field('Type the account name to confirm', 'WhatsApp · Bookings')}`,
  actions: `<span class="btn ghost">Cancel</span><span class="btn danger">Delete account</span>`,
}));
