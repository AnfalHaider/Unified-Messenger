import { band, chip, clock, fig, ic, meter, shell, titleBar } from './lib.mjs';
import { commandCenterContent } from './screens-a.mjs';

export const Suspended = () => `<div class="app">${titleBar({ sync: 'suspended', bell: 0 })}<div style="display:grid;place-items:center;background:var(--shell);padding:40px">
<div class="sheet" style="width:470px;padding:32px;display:flex;flex-direction:column;gap:13px;align-items:center;text-align:center">
 <div style="width:52px;height:52px;border-radius:26px;background:var(--late-w);display:grid;place-items:center;color:var(--late)">${ic('lock', 22)}</div>
 <h2 class="h2" style="font-size:19px">Membership suspended</h2>
 <span class="sub" style="font-size:12.5px">Glow Salons' access is paused, so accounts are not being read. Everything already on this PC is safe.</span>
 <span class="sub" style="font-size:12.5px">Contact the workspace owner, or reach us at [SUPPORT EMAIL].</span>
 <div class="row"><span class="btn">${ic('refresh', 14)}Check again</span><span class="btn ghost">Sign out</span></div></div></div></div>`;

export const Offline = () => shell({ tb: { sync: 'offline' }, content: commandCenterContent({
  strip: `<div class="strip warn">${ic('wifioff', 15)}<span style="font-weight:600">This PC has been offline for 2 days. These figures stopped at 4:12 pm on Tuesday.</span><span style="color:var(--ink-2)">The app keeps working for 5 more days, then needs to reconnect once.</span></div>` }) });

export const ModuleUnhealthy = () => shell({ content: commandCenterContent({
  strip: `<div class="strip bad">${ic('alert', 15)}<span style="font-weight:600">The Instagram reader stopped working after an Instagram update.</span><span style="color:var(--ink-2)">WhatsApp and Google are unaffected. Instagram figures are hidden, not guessed.</span><span class="btn" style="margin-left:auto;height:26px">Copy diagnostics</span></div>` }) });

export const UpdateReady = () => shell({ content: commandCenterContent({ strip: '' }), overlay:
  `<div style="position:absolute;right:22px;bottom:22px;width:350px;background:var(--board);border:1px solid var(--line);border-radius:6px;box-shadow:0 18px 44px rgba(16,19,34,.22)">
   <div class="row" style="align-items:flex-start;padding:14px 15px">${ic('download', 17, 'var(--brand-ink)')}<div class="col" style="gap:4px">
   <span style="font-weight:600;font-size:12.5px">Update ready · 6.1.0</span><span class="sub" style="font-size:12px">Adds the morning digest. It installs when you restart, and takes about ten seconds.</span>
   <div class="row" style="margin-top:6px"><span class="btn primary" style="height:28px">Restart now</span><span class="btn ghost" style="height:28px">Later</span></div></div></div></div>` });

export const EmptyWorkspace = () => shell({ content: `<div style="flex:1;display:grid;place-items:center">
  <div class="col" style="gap:14px;align-items:center;text-align:center;max-width:480px">
  <div style="width:56px;height:56px;border-radius:10px;background:var(--wash);display:grid;place-items:center;color:var(--brand-ink)">${ic('chat', 26)}</div>
  <h1 class="h1">Add your first account</h1>
  <span class="sub" style="font-size:13.5px">Connect a WhatsApp, Instagram or Google Business profile. The app reads who is waiting and never sends anything.</span>
  <div class="row"><span class="btn primary lg">${ic('plus', 16)}Add account</span><span class="btn lg">${ic('mail', 16)}Invite a colleague</span></div></div></div>` })
  .replace(/<div class="loc">[\s\S]*?(?=<div class="side-foot">)/, '<div class="sub" style="padding:6px 10px;font-size:12px;color:#5D6488">No accounts yet</div>');

export const AllCaughtUp = () => shell({ content: `
<div class="row" style="align-items:flex-end">
  <div class="col" style="gap:3px"><h1 class="h1">Good afternoon, Anfal</h1>
  <span class="sub" style="font-size:12.5px">3 locations · 6 accounts reading · 1 needs sign-in · read 20 seconds ago</span></div>
  <div class="row" style="margin-left:auto"><div class="seg"><span class="on">Today</span><span>This week</span><span>Pick a date</span></div>
  <span class="btn">${ic('refresh', 14)}Re-sync</span></div></div>
${band(
  fig('Waiting now', '0', 'customers', 'Cleared at 4:06 pm', 'ok', [14, 11, 9, 7, 5, 2, 0]),
  fig('Answered on time', '94', '%', 'Above the 90% target', 'ok', [88, 90, 91, 92, 93, 94, 94]),
  fig('First reply', '7', 'min median', 'Best day this week', 'ok', [14, 13, 12, 10, 9, 8, 7]),
  fig('Reviews to answer', '0', 'of 3 new', 'All answered today', 'ok', [2, 1, 1, 0, 1, 0, 0]),
)}
<div style="display:grid;grid-template-columns:minmax(0,1fr) 316px;gap:22px;min-height:0;flex:1">
  <div class="col" style="gap:0">
    <div class="row" style="padding-bottom:10px"><h2 class="h2">Waiting now</h2><span class="sub" style="font-size:12px">longest first</span></div>
    <div class="sheet" style="flex:1;display:grid;place-items:center;border-style:dashed">
      <div class="col" style="gap:10px;align-items:center;text-align:center;padding:24px">
        <div style="width:50px;height:50px;border-radius:25px;background:var(--ok-w);display:grid;place-items:center;color:var(--ok)">${ic('check', 24)}</div>
        <h2 class="h2" style="font-size:17px">Nobody is waiting</h2>
        <span class="sub" style="font-size:12.5px;max-width:34em">Across 6 accounts at 3 locations, every customer has an answer. Read 20 seconds ago.</span>
        <span class="sub" style="font-size:12px">The last one, Zara T. at F-11 Markaz, was answered in 4 minutes.</span></div></div>
  </div>
  <div class="col" style="gap:0">
    <div class="row" style="padding-bottom:10px"><h2 class="h2">Today so far</h2></div>
    <div class="sheet" style="overflow:hidden">
      ${[['F-11 Markaz', 61, '92%'], ['DHA Phase 2', 44, '95%'], ['Gulberg', 23, '96%']].map(([l, c, o]) =>
        `<div class="col" style="gap:7px;padding:13px 14px;border-bottom:1px solid var(--line)">
          <div class="row"><span class="h3">${l}</span><span class="sub" style="margin-left:auto;font-size:12px">${c} answered</span></div>
          ${meter(parseInt(o, 10), 'ok')}<span style="font-size:11.5px;color:var(--ok);font-weight:600">${o} on time</span></div>`).join('')}
      <div class="col" style="padding:12px 14px;gap:5px;background:var(--field)"><span style="font-weight:600;font-size:12.5px">Quietest hour is coming</span><span class="sub" style="font-size:12px">4 to 5 pm is usually your lightest hour. A good time for the 1-star review from yesterday.</span></div>
    </div>
  </div>
</div>` });

export const TrayAndToast = () => `<div style="width:1440px;height:560px;background:#1B2436;position:relative;font-family:'IBM Plex Sans','Segoe UI',system-ui,sans-serif">
 <div style="position:absolute;left:24px;top:22px;color:#fff;font-size:12.5px;opacity:.75">Windows desktop · outside the app window</div>
 <div style="position:absolute;right:24px;bottom:70px;width:356px;background:#fff;border-radius:6px;box-shadow:0 14px 36px rgba(0,0,0,.4);padding:14px 15px;display:flex;flex-direction:column;gap:7px">
  <div class="row" style="gap:8px;font-size:11.5px;color:#6C7488"><div class="mark" style="width:15px;height:15px;border-radius:3px;font-size:8px">U</div>Unified Messenger<span style="margin-left:auto">${ic('x', 13)}</span></div>
  <div class="row" style="gap:10px;align-items:baseline">${clock(16, 'min', 'late')}<span style="font-weight:600;color:#141726">Sara M. passed the 15-minute target</span></div>
  <span style="color:#495064;font-size:12.5px">WhatsApp · Bookings · F-11 Markaz</span>
  <div class="row" style="margin-top:4px"><span class="btn" style="height:28px;flex:1">Open chat</span><span class="btn" style="height:28px;flex:1">Snooze 10 min</span></div></div>
 <div style="position:absolute;right:410px;bottom:58px;width:250px;background:#fff;border-radius:6px;box-shadow:0 14px 36px rgba(0,0,0,.4);padding:5px;display:flex;flex-direction:column">
  ${[['grid', 'Open Unified Messenger', 0], ['clock', '19 customers waiting', 1], ['refresh', 'Re-sync all accounts', 0], ['moon', 'Pause alerts for 1 hour', 0], ['x', 'Quit', 2]].map(([i, t, k]) =>
    `<div class="row" style="height:32px;padding:0 10px;border-radius:4px;font-size:12.5px;color:${k === 1 ? '#BE3227' : '#141726'};font-weight:${k === 1 ? '600' : '400'};${k === 2 ? 'border-top:1px solid #E4E7EF' : ''}">${ic(i, 14)}<span>${t}</span></div>`).join('')}</div>
 <div style="position:absolute;left:0;right:0;bottom:0;height:46px;background:rgba(241,243,248,.94);display:flex;align-items:center;justify-content:center;gap:10px">
  <div style="position:relative;width:34px;height:34px;border-radius:6px;background:#fff;display:grid;place-items:center;box-shadow:0 0 0 2px #4B4FD6"><div class="mark">U</div>
  <span style="position:absolute;right:-5px;bottom:-5px;min-width:18px;height:18px;border-radius:9px;background:#BE3227;color:#fff;font-size:10px;font-weight:700;display:grid;place-items:center;padding:0 4px;font-family:'IBM Plex Mono',monospace">19</span></div>
  <span style="position:absolute;right:16px;color:#141726;font-size:12px">4:18 pm</span></div></div>`;

const tbRow = (label, tb) => `<div class="col" style="gap:5px"><span class="label">${label}</span><div style="width:1388px;border-radius:5px;overflow:hidden;background:var(--shell)">${titleBar(tb)}</div></div>`;

export const TitleBar = () => `<div style="width:1440px;height:700px;padding:24px;background:var(--board);display:flex;flex-direction:column;gap:16px">
 <div class="col" style="gap:3px"><h1 class="h1" style="font-size:19px">Title bar</h1><span class="sub" style="font-size:12.5px">38 px tall, drawn by the app and part of the dark shell. The empty space and the app name drag the window, double-click maximises, and the window buttons are 44 px wide.</span></div>
 ${tbRow('Default · synced', {})}${tbRow('Syncing workspace settings', { sync: 'syncing' })}${tbRow('Offline, inside the 7-day grace period', { sync: 'offline' })}
 ${tbRow('Assistant open', { aiOn: true, bell: 0 })}${tbRow('Suspended', { sync: 'suspended', bell: 0 })}${tbRow('Before sign-in', { minimal: true })}
</div>`;

// The specimen sheet is its own module: it is the one screen whose job is to show the system itself.
export { Tokens } from './tokens-page.mjs';
