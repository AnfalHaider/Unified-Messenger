import { chip, ic, meter, shell } from './lib.mjs';

const SECTIONS = [['general', 'gear', 'General'], ['accounts', 'chat', 'Accounts & channels'], ['assistant', 'spark', 'Assistant'], ['workspace', 'users', 'Workspace & members'],
  ['notifications', 'bell', 'Notifications'], ['appearance', 'moon', 'Appearance'], ['privacy', 'shield', 'Data & privacy'], ['about', 'info', 'About & updates']];

const setting = (title, desc, control) => `<div class="row" style="padding:13px 16px;border-bottom:1px solid var(--line);align-items:center;gap:24px">
  <div class="col" style="gap:2px;flex:1"><span style="font-weight:600;font-size:12.5px">${title}</span>${desc ? `<span class="sub" style="font-size:12px">${desc}</span>` : ''}</div>${control}</div>`;

const group = (title, inner, note = '') => `<div class="col" style="gap:8px">
  <div class="row"><h3 class="h3">${title}</h3>${note ? `<span style="margin-left:auto">${note}</span>` : ''}</div>
  <div class="sheet" style="overflow:hidden">${inner}</div></div>`;

const settingsShell = (key, title, sub, inner) => shell({ active: 'settings', content: `
<div class="col" style="gap:3px"><h1 class="h1">Settings</h1><span class="sub" style="font-size:12.5px">Anything marked ${chip('info', 'Workspace', 'building')} follows every PC in Glow Salons. The rest stays on this one.</span></div>
<div style="display:grid;grid-template-columns:206px minmax(0,1fr);gap:26px;flex:1;min-height:0">
  <div class="col" style="gap:1px">${SECTIONS.map(([k, i, t]) => `<div class="row" style="height:30px;padding:0 10px;border-radius:4px;gap:10px;font-weight:${k === key ? '600' : '500'};font-size:12.5px;color:${k === key ? 'var(--brand)' : 'var(--ink-2)'};background:${k === key ? 'var(--wash)' : 'transparent'}">${ic(i, 15)}<span>${t}</span></div>`).join('')}</div>
  <div class="col" style="gap:16px;min-width:0;max-width:840px"><div class="col" style="gap:2px"><h2 class="h2" style="font-size:17px">${title}</h2>${sub ? `<span class="sub" style="font-size:12.5px">${sub}</span>` : ''}</div>${inner}</div>
</div>` });

export const SettingsGeneral = () => settingsShell('general', 'General', 'How the app starts, and which accounts stay awake.', `
${group('Accounts', setting('Keep every account awake', 'Each account page stays open and keeps reading. Uses more memory.', '<span class="toggle on"></span>')
  + setting("Sleep accounts I'm not using", 'Closes idle pages and keeps their logins. Accounts that feed your figures are never slept.', '<span class="toggle"></span>')
  + setting('Sleep after', 'Only used when sleeping is on', '<div class="input" style="width:150px;opacity:.5">20 minutes' + ic('down', 13) + '</div>'))}
${group('Startup', setting('Start with Windows', 'Opens minimised to the tray', '<span class="toggle on"></span>')
  + setting('When I close the window', '', '<div class="seg"><span class="on">Keep running in the tray</span><span>Quit</span></div>'))}
${group('Reply target', setting('Reply within', 'Sets what counts as on time, and when a customer is about to be late', '<div class="input" style="width:150px">15 minutes' + ic('down', 13) + '</div>')
  + setting('Warn me before the target', '', '<div class="input" style="width:150px">5 minutes before' + ic('down', 13) + '</div>')
  + setting('Opening hours', 'Waiting time stops outside these hours', '<span class="btn">Edit per location</span>'), chip('info', 'Workspace', 'building'))}`);

export const SettingsAccounts = () => settingsShell('accounts', 'Accounts & channels', 'Everything the app reads, grouped by location.', `
<div class="row"><span class="btn primary">${ic('plus', 14)}Add account</span><span class="btn">${ic('building', 14)}Manage locations</span><span style="margin-left:auto">${chip('info', 'Workspace', 'building')}</span></div>
<div class="sheet" style="overflow:hidden"><table class="table"><thead><tr><th>Account</th><th>Location</th><th>On this PC</th><th>Reader</th><th></th></tr></thead><tbody>
${[['chat', 'WhatsApp · Front desk', 'DHA Phase 2', ['ok', 'Linked', 'check'], ['ok', 'Working']], ['insta', 'Instagram', 'DHA Phase 2', ['ok', 'Linked', 'check'], ['ok', 'Working']],
   ['google', 'Google reviews', 'DHA Phase 2', ['ok', 'Linked', 'check'], ['ok', 'Working']], ['chat', 'WhatsApp · Bookings', 'F-11 Markaz', ['ok', 'Linked', 'check'], ['warn', 'Previews off']],
   ['google', 'Google reviews', 'F-11 Markaz', ['ok', 'Linked', 'check'], ['ok', 'Working']], ['chat', 'WhatsApp · Front desk', 'Gulberg', ['ok', 'Linked', 'check'], ['ok', 'Working']],
   ['insta', 'Instagram', 'Gulberg', ['neu', 'Sign in needed', 'lock'], ['neu', 'Waiting for sign-in']]].map(([i, a, l, s, r]) =>
  `<tr><td><div class="row">${ic(i, 15, 'var(--ink-2)')}<span style="font-weight:600">${a}</span></div></td><td>${l}</td><td>${chip(s[0], s[1], s[2])}</td><td>${chip(r[0], r[1])}</td><td style="text-align:right;color:var(--ink-3)">${ic('edit', 15)}</td></tr>`).join('')}
</tbody></table></div>
${group('Channel readers', [['WhatsApp', '6.0.3 · read 20 seconds ago'], ['Instagram', '6.0.1 · read 1 minute ago'], ['Google reviews', '6.0.0 · read 6 minutes ago']].map(([t, s]) => setting(t, s, chip('ok', 'Healthy', 'check'))).join(''))}`);

export const SettingsAssistant = () => settingsShell('assistant', 'Assistant', 'A local AI you can ask about your customers. Off until you turn it on.', `
${group('Assistant', setting('Turn on the assistant', 'Downloads the engine and a model sized for this PC. Nothing leaves this PC.', '<span class="toggle on"></span>'))}
<div class="sheet" style="padding:15px 16px;display:flex;flex-direction:column;gap:11px">
  <div class="row"><h3 class="h3">Setting up</h3><span style="margin-left:auto">${chip('info', 'Downloading')}</span></div>
  <div class="row"><span style="font-weight:600">gemma3 4B</span><span class="sub" style="font-size:12px">chosen for 16 GB of memory</span><span class="mono" style="margin-left:auto;font-size:12.5px">2.1 of 3.3 GB</span></div>
  ${meter(64, 'info')}
  <div class="row"><span class="sub" style="font-size:12px">About 4 minutes left</span><span class="btn ghost" style="margin-left:auto;height:26px">Pause</span></div></div>
${group('Model', setting('Model size', 'Smaller is faster on an older PC; larger answers better', '<div class="seg"><span>Small · 1B</span><span class="on">Standard · 4B</span><span>Large · 8B</span></div>')
  + setting('Draft review answers', '', '<span class="toggle on"></span>')
  + setting('Suggest how to reply', 'Reads only the chat you pick', '<span class="toggle on"></span>'))}
<div class="strip info">${ic('shield', 15)}<span>The assistant reads your figures and the chat you choose. It has no way to send, post or delete anything.</span></div>`);

export const SettingsWorkspace = () => settingsShell('workspace', 'Workspace & members', 'Glow Salons · 5 members · created 12 September 2026', `
<div class="row"><span class="btn primary">${ic('mail', 14)}Invite member</span><span class="sub" style="margin-left:auto;font-size:12.5px">You are an admin</span></div>
<div class="sheet" style="overflow:hidden"><table class="table"><thead><tr><th>Member</th><th>Role</th><th>Last seen</th><th>PCs</th><th></th></tr></thead><tbody>
${[['AH', 'Anfal Haider', 'anfal@glowsalons.pk', 'Owner', 'Now', '2', 'ok'], ['NR', 'Nida R.', 'nida@glowsalons.pk', 'Admin', '12 minutes ago', '1', 'ok'],
   ['UK', 'Usman K.', 'usman.frontdesk@gmail.com', 'Member', 'Today, 9:14', '1', 'ok'], ['SB', 'Saba B.', 'saba.b@gmail.com', 'Member', '3 days ago', '1', 'warn'],
   ['—', 'hamza.bookings@gmail.com', 'Invited · has not signed in yet', 'Member', '—', '0', 'neu']].map(([a, n, e, r, s, p, k]) =>
  `<tr><td><div class="row"><span class="avatar">${a}</span><div class="col" style="gap:0"><span style="font-weight:600">${n}</span><span class="sub" style="font-size:12px">${e}</span></div></div></td>
  <td><div class="input" style="height:26px;width:104px">${r}${ic('down', 12)}</div></td><td>${k === 'warn' ? chip('warn', s) : k === 'neu' ? chip('neu', 'Invite sent') : s}</td><td class="mono">${p}</td>
  <td style="text-align:right">${r === 'Owner' ? '' : `<span class="btn ghost" style="height:26px;color:var(--late)">Remove</span>`}</td></tr>`).join('')}
</tbody></table></div>
<div class="strip neu">${ic('info', 15)}<span>Removing someone signs them out and wipes every account login on their PCs the next time they are online. Also remove their PC under WhatsApp, Linked devices.</span></div>`);

export const SettingsNotifications = () => settingsShell('notifications', 'Notifications', 'What gets your attention, and when.', `
${group('Tell me when', setting('A customer is about to pass the reply target', '', '<span class="toggle on"></span>')
  + setting('A customer has passed the reply target', '', '<span class="toggle on"></span>')
  + setting('A review arrives with 1 or 2 stars', '', '<span class="toggle on"></span>')
  + setting('An account needs signing in again', '', '<span class="toggle on"></span>')
  + setting('Missed calls have not been returned', '', '<span class="toggle"></span>'))}
${group('How', setting('Windows notifications', '', '<span class="toggle on"></span>') + setting('Taskbar badge', 'Shows how many customers are waiting', '<span class="toggle on"></span>')
  + setting('Quiet hours', 'No pop-ups between these times. Reading carries on.', '<div class="input" style="width:190px">10:00 pm to 8:00 am' + ic('down', 13) + '</div>')
  + setting('Morning digest', 'A short summary when you open the app each day', '<span class="toggle on"></span>'))}`);

export const SettingsAppearance = () => settingsShell('appearance', 'Appearance', 'Stays on this PC.', `
${group('Theme', setting('Theme', '', '<div class="seg"><span>Light</span><span>Dark</span><span class="on">Match Windows</span></div>')
  + setting('High contrast', 'Follows the Windows setting on its own', chip('neu', 'Automatic')))}
${group('Layout', setting('Density', '', '<div class="seg"><span class="on">Comfortable</span><span>Compact</span></div>')
  + setting('Text size', '', '<div class="seg"><span>Smaller</span><span class="on">Default</span><span>Larger</span></div>')
  + setting('Show message previews in the waiting list', 'Turn this off if other people can see your screen', '<span class="toggle on"></span>'))}
<div class="row" style="gap:14px">${['Light', 'Dark'].map((t, i) => `<div class="sheet" style="flex:1;padding:11px;${i === 0 ? 'outline:2px solid var(--brand);outline-offset:-1px' : ''}">
  <div style="height:112px;border-radius:4px;background:${i ? '#101322' : '#F2F4F8'};display:grid;grid-template-columns:52px 1fr;gap:5px;padding:5px">
    <div style="background:${i ? '#191D33' : '#101322'};border-radius:3px"></div>
    <div style="display:grid;grid-template-rows:18px 1fr;gap:5px"><div style="background:${i ? '#232842' : '#fff'};border-radius:3px"></div><div style="background:${i ? '#191D33' : '#fff'};border-radius:3px"></div></div></div>
  <div style="margin-top:8px;font-weight:600;font-size:12.5px">${t}</div></div>`).join('')}</div>`);

export const SettingsPrivacy = () => settingsShell('privacy', 'Data & privacy', 'Exactly what leaves this PC, and what never does.', `
<div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px">
  <div class="sheet" style="padding:15px 16px;display:flex;flex-direction:column;gap:9px"><div class="row">${ic('cloud', 17, 'var(--brand)')}<h3 class="h3">Kept in your workspace</h3></div><span class="sub" style="font-size:12px">So it follows you to a new PC.</span>
  <ul style="margin:0;padding-left:17px;display:flex;flex-direction:column;gap:5px;color:var(--ink-2);font-size:12.5px"><li>Account names and channels</li><li>Locations and opening hours</li><li>Shared settings</li><li>Who is a member</li></ul></div>
  <div class="sheet" style="padding:15px 16px;display:flex;flex-direction:column;gap:9px;background:var(--ontime-w);border-color:transparent"><div class="row">${ic('shield', 17, 'var(--ontime)')}<h3 class="h3">Never leaves this PC</h3></div><span class="sub" style="font-size:12px;color:var(--ink-2)">Not even to the workspace.</span>
  <ul style="margin:0;padding-left:17px;display:flex;flex-direction:column;gap:5px;color:var(--ink-2);font-size:12.5px"><li>Messages and previews</li><li>Customer names and numbers</li><li>Waiting times, reports and history</li><li>Assistant chats</li><li>WhatsApp, Instagram and Google logins</li></ul></div>
</div>
${group('On this PC', setting('Keep history for', '', '<div class="input" style="width:150px">12 months' + ic('down', 13) + '</div>')
  + setting('Export my data', 'History and settings, as one file', '<span class="btn">' + ic('download', 14) + 'Export</span>')
  + setting('Wipe this PC', 'Signs out, and removes every login and all history from this PC', '<span class="btn" style="color:var(--late)">' + ic('trash', 14) + 'Wipe</span>'))}`);

export const SettingsAbout = () => settingsShell('about', 'About & updates', '', `
<div class="sheet" style="padding:15px 16px;display:flex;align-items:center;gap:14px"><div class="mark" style="width:40px;height:40px;border-radius:8px;font-size:19px">U</div>
<div class="col" style="gap:0"><span class="h2">Unified Messenger 6.0.3</span><span class="sub" style="font-size:12.5px">Up to date · checked 2 hours ago</span></div><span class="btn" style="margin-left:auto">${ic('refresh', 14)}Check for updates</span></div>
${group('Updates', setting('Install updates automatically', 'Downloads in the background and installs when you next restart', '<span class="toggle on"></span>') + setting('Release notes', 'What changed in 6.0.3', '<span class="btn">Open</span>'))}
${group('Help', setting('Copy diagnostics', 'App version, reader health and recent errors. No customer data.', '<span class="btn">Copy</span>') + setting('Open-source notices', 'Electron, React, Ollama, Ferdium recipes and others', '<span class="btn">View</span>'))}`);
