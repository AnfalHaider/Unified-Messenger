import { band, chip, clock, fig, ic, meter, shell, trend } from './lib.mjs';

// The meter runs to 45 minutes, with the reply target marked at 15. A bar past the tick is a customer the
// business has already let down, and you can see that without reading a single number.
const SCALE = 45, TARGET = 15;
const bar = (mins, kind) => meter((mins / SCALE) * 100, kind, (TARGET / SCALE) * 100);

// name, account, location, minutes waited, last message, tone, status
export const QUEUE = [
  ['Sara M.', 'WhatsApp · Bookings', 'F-11 Markaz', 38, 'Can I move my 5pm appointment to tomorrow?', 'late', 'Past target'],
  ['Bilal R.', 'WhatsApp · Bookings', 'F-11 Markaz', 17, 'Price for keratin treatment?', 'late', 'Past target'],
  ['Hina A.', 'Instagram', 'DHA Phase 2', 13, 'Do you have slots on Saturday', 'due', 'Due in 2 min'],
  ['Omar K.', 'WhatsApp · Front desk', 'DHA Phase 2', 11, 'Voice message · 0:42', 'due', 'Due in 4 min'],
  ['Zara T.', 'WhatsApp · Bookings', 'F-11 Markaz', 6, 'Thanks! Also, is parking available?', 'ok', 'On time'],
  ['Ali H.', 'WhatsApp · Front desk', 'DHA Phase 2', 3, 'Photo', 'ok', 'On time'],
];

const initials = (n) => n.split(' ').map((x) => x[0]).join('');

export const boardHead = () => `<div class="bhead"><span>Waiting</span><span>Against target</span><span>Customer</span><span>Account</span><span></span></div>`;

// Only the customer who has waited longest gets a labelled action. Six identical buttons down the board is
// chrome repeating itself, and it flattens the very hierarchy the clock column exists to create.
export const boardRow = ([n, a, l, w, m, k, s], i = 0) => `<div class="brow ${k === 'ok' ? '' : k}">
  <span>${clock(w, 'min', k)}</span>
  <div class="col" style="gap:5px">${bar(w, k)}<span style="font-size:11.5px;font-weight:600;color:${k === 'late' ? 'var(--late)' : k === 'due' ? 'var(--due)' : 'var(--ink-3)'}">${s}</span></div>
  <div class="col" style="gap:1px;min-width:0"><span class="who">${n}</span><span class="prev">${m}</span></div>
  <div class="col" style="gap:1px;font-size:12px"><span>${a}</span><span class="sub" style="font-size:11.5px">${l}</span></div>
  <span style="text-align:right">${i === 0 ? `<span class="btn" style="height:26px">Open${ic('right', 13)}</span>` : `<span style="color:var(--ink-3)">${ic('right', 15)}</span>`}</span></div>`;

// The target is stated once, in the band at the top of the screen; repeating it on every location row is noise.
const locationRow = (name, waiting, ontime, k) => `<div class="row" style="padding:12px 14px;border-bottom:1px solid var(--line);align-items:flex-start">
  <div class="col" style="gap:6px;flex:1">
    <div class="row"><span class="h3">${name}</span><span style="margin-left:auto;font-size:12px;color:${waiting ? 'var(--ink)' : 'var(--ink-3)'}">${waiting ? `${waiting} waiting` : 'nobody waiting'}</span></div>
    ${meter(parseInt(ontime, 10), k)}
    <span style="font-size:11.5px;color:${k === 'late' ? 'var(--late)' : k === 'due' ? 'var(--due)' : 'var(--ontime)'};font-weight:600">${ontime} answered on time</span>
  </div></div>`;

export const commandCenterContent = (opts = {}) => `
<div class="row" style="align-items:flex-end">
  <div class="col" style="gap:3px"><h1 class="h1">Good afternoon, Anfal</h1>
  <span class="sub" style="font-size:12.5px">3 locations · 6 accounts reading · 1 needs sign-in · read 20 seconds ago</span></div>
  <div class="row" style="margin-left:auto"><div class="seg"><span class="on">Today</span><span>This week</span><span>Pick a date</span></div>
  <span class="btn">${ic('refresh', 14)}Re-sync</span></div>
</div>
${opts.strip ?? `<div class="strip warn">${ic('clock', 15)}<span style="font-weight:600">Two customers pass the 15-minute target in the next five minutes.</span><span class="btn ghost" style="margin-left:auto;height:24px;color:var(--due)">Show them</span></div>`}
${band(
  fig('Waiting now', '19', 'customers', '4 more than at 9 am', 'due', [8, 11, 9, 14, 12, 15, 19]),
  fig('Answered on time', '82', '%', 'Target is 90%', 'late', [91, 88, 90, 86, 84, 83, 82]),
  fig('First reply', '11', 'min median', 'Inside the target', 'ok', [14, 13, 12, 12, 10, 11, 11]),
  fig('Reviews to answer', '1', 'of 3 new', '1 star · DHA Phase 2', 'late', [0, 1, 0, 2, 1, 1, 1]),
)}
<div style="display:grid;grid-template-columns:minmax(0,1fr) 316px;gap:22px;min-height:0;flex:1">
  <div class="board">
    <div class="row" style="padding-bottom:10px"><h2 class="h2">Waiting now</h2><span class="sub" style="font-size:12px">longest first</span>
    <div class="seg" style="margin-left:auto"><span class="on">All 19</span><span>Past target 7</span><span>WhatsApp</span><span>Instagram</span></div></div>
    ${boardHead()}
    ${QUEUE.map(boardRow).join('')}
    <div class="row" style="padding:11px 12px;color:var(--ink-3);font-size:12.5px"><span>13 more waiting under 3 minutes</span><span class="btn ghost" style="margin-left:auto;height:24px">Show all 19</span></div>
  </div>
  <div class="col" style="gap:0">
    <div class="row" style="padding-bottom:10px"><h2 class="h2">Locations</h2></div>
    <div class="sheet" style="overflow:hidden">
      ${locationRow('F-11 Markaz', 12, '71%', 'late')}
      ${locationRow('DHA Phase 2', 7, '84%', 'due')}
      ${locationRow('Gulberg', 0, '96%', 'ok')}
      <div class="col" style="padding:12px 14px;gap:8px;background:var(--field)">
        <div class="row">${ic('star', 14, 'var(--late)')}<span style="font-weight:600;font-size:12.5px">1 review needs an answer</span></div>
        <span class="sub" style="font-size:12px">A 1-star review at DHA Phase 2, two hours ago. Google is reviews only.</span>
        <span class="btn" style="height:26px;align-self:flex-start">Open Reviews</span>
      </div>
      <div class="col" style="padding:12px 14px;gap:6px">
        <div class="row">${ic('lock', 14, 'var(--ink-3)')}<span style="font-weight:600;font-size:12.5px">Instagram at Gulberg is signed out</span></div>
        <span class="sub" style="font-size:12px">Its figures are hidden rather than guessed.</span>
      </div>
    </div>
  </div>
</div>`;

export const Main = () => shell({ active: 'center', content: commandCenterContent() });

const fakeWhatsApp = (qr) => `<div style="flex:1;border:1px solid var(--line);border-radius:6px;overflow:hidden;display:grid;grid-template-columns:${qr ? '1fr' : '340px 1fr'};background:#fff;min-height:0">
${qr ? `<div style="display:grid;place-items:center;background:#F7F8FA"><div class="row" style="gap:48px;align-items:center">
  <div class="col" style="gap:14px;max-width:380px"><span style="font-size:21px;font-weight:600">Link this account</span>
  <ol style="margin:0;padding-left:18px;color:var(--ink-2);display:flex;flex-direction:column;gap:6px"><li>Open WhatsApp on the business phone</li><li>Tap Menu, then Linked devices, then Link a device</li><li>Point the phone at this code</li></ol>
  <span class="sub" style="font-size:12px">This is the real WhatsApp Web page, shown inside the app.</span></div>
  <div style="width:212px;height:212px;border:10px solid #fff;box-shadow:0 0 0 1px var(--line);display:grid;place-items:center;color:var(--ink)">${ic('qr', 146, 'var(--ink)', 1.2)}</div></div></div>`
: `<div style="border-right:1px solid var(--line);background:#fff;display:flex;flex-direction:column">
  <div style="height:54px;background:#F0F2F5;display:flex;align-items:center;padding:0 14px;gap:10px"><span class="avatar" style="border-radius:50%">GS</span><span style="margin-left:auto;color:var(--ink-3)">${ic('search')}</span></div>
  ${['Sara M.', 'Bilal R.', 'Zara T.', 'Customer 0342…', 'Nadia F.', 'Usman J.', 'Ayesha S.'].map((n, i) => `<div style="height:62px;display:flex;align-items:center;gap:10px;padding:0 14px;border-bottom:1px solid #F0F2F5;${i === 0 ? 'background:#F0F2F5' : ''}"><span class="avatar" style="width:38px;height:38px;border-radius:50%">${n[0]}</span><div class="col" style="gap:2px;min-width:0"><span style="font-weight:600">${n}</span><span class="sub" style="font-size:12px">…</span></div></div>`).join('')}
</div>
<div style="background:#EFEAE2;display:flex;flex-direction:column"><div style="height:54px;background:#F0F2F5;display:flex;align-items:center;padding:0 16px;gap:10px"><span class="avatar" style="border-radius:50%">S</span><span style="font-weight:600">Sara M.</span></div>
<div style="flex:1;padding:22px;display:flex;flex-direction:column;gap:8px;justify-content:flex-end">
  <div style="align-self:flex-start;background:#fff;border-radius:8px;padding:8px 12px;max-width:60%">Hi, can I move my 5pm appointment to tomorrow?</div>
</div><div style="height:56px;background:#F0F2F5;display:flex;align-items:center;padding:0 16px"><div class="input" style="flex:1;border:0;background:#fff">Type a message</div></div></div>`}
</div>`;

export const AccountLive = (qr = false) => shell({
  active: '', acct: 'WhatsApp · BookingsF-11 Markaz',
  content: `<div class="row"><span class="btn ghost" style="padding:0 4px">${ic('right', 14)}</span>
  <div class="col" style="gap:0"><span class="h2">WhatsApp · Bookings</span><span class="sub" style="font-size:12px">F-11 Markaz</span></div>
  ${qr ? chip('neu', 'Sign in needed', 'lock') : chip('ok', 'Reading · 20 s ago', 'check')}
  ${chip('info', 'The app never sends messages', 'shield')}
  <div class="row" style="margin-left:auto"><span class="btn">${ic('refresh', 14)}Reload page</span><span class="btn">${ic('moon', 14)}Sleep this account</span></div></div>
  ${fakeWhatsApp(qr)}`,
  panel: qr ? '' : `<aside class="panel-r"><div class="row" style="padding:14px 16px;border-bottom:1px solid var(--line)"><h2 class="h2">Waiting here</h2><span class="clock" style="margin-left:auto;font-size:20px;color:var(--late)">12</span></div>
  <div class="col" style="padding:4px 0;gap:0">${QUEUE.filter((r) => r[1] === 'WhatsApp · Bookings').concat([[['Customer 0342…'], '', '', 4, 'Hello', 'ok', 'On time']]).map(([n, , , w, m, k, s]) =>
    `<div class="row" style="padding:11px 16px;border-bottom:1px solid var(--line);align-items:flex-start;${n === 'Sara M.' ? 'background:var(--wash)' : ''}">
      <div class="col" style="gap:4px;flex:1;min-width:0"><span class="who">${n}</span><span class="prev" style="font-size:12px">${m}</span>${bar(w, k)}</div>
      <div class="col" style="gap:2px;align-items:flex-end">${clock(w, 'min', k)}<span style="font-size:11px;color:${k === 'late' ? 'var(--late)' : 'var(--ink-3)'}">${s}</span></div></div>`).join('')}</div>
  <div style="margin-top:auto;padding:13px 16px;border-top:1px solid var(--line);font-size:12px" class="sub">Picking a customer finds their chat in the page without marking it read.</div></aside>`,
});

export const AccountDetail = () => shell({
  active: '', acct: 'WhatsApp · Front deskDHA Phase 2',
  content: `<div class="row"><div style="width:36px;height:36px;border-radius:6px;background:var(--ontime-w);display:grid;place-items:center;color:var(--ontime)">${ic('chat', 19)}</div>
  <div class="col" style="gap:0"><h1 class="h1" style="font-size:19px">WhatsApp · Front desk</h1><span class="sub" style="font-size:12.5px">DHA Phase 2 · linked on this PC · read every 30 seconds</span></div>
  <div class="row" style="margin-left:auto"><span class="btn">${ic('out', 14)}Open live page</span><span class="btn">${ic('edit', 14)}Edit</span></div></div>
  <div class="seg" style="align-self:flex-start"><span class="on">Overview</span><span>Waiting</span><span>Reply times</span><span>Health</span></div>
  ${band(
    fig('Waiting now', '5', 'customers', '2 due within 5 min', 'due', [2, 3, 3, 4, 6, 5, 5]),
    fig('Answered on time', '84', '% this week', 'Target is 90%', 'due', [80, 82, 85, 83, 86, 84, 84]),
    fig('First reply', '9', 'min median', 'Inside the target', 'ok', [12, 10, 11, 9, 9, 10, 9]),
    fig('Conversations', '146', 'this week', '12% more than last week', 'neu', [18, 22, 19, 25, 21, 20, 21]),
  )}
  <div style="display:grid;grid-template-columns:minmax(0,1fr) 330px;gap:22px;flex:1;min-height:0">
    <div class="col" style="gap:10px"><div class="row"><h2 class="h2">First reply, last 7 days</h2><span class="sub" style="margin-left:auto;font-size:12px">median minutes · target 15</span></div>
    <svg viewBox="0 0 640 230" width="100%" height="230">
    <line x1="34" y1="70" x2="636" y2="70" stroke="#BE3227" stroke-dasharray="3 4"/><text x="30" y="74" text-anchor="end" font-size="11" fill="#6C7488" font-family="IBM Plex Mono">15</text>
    ${[12, 10, 11, 9, 18, 10, 9].map((v, i) => `<rect x="${58 + i * 84}" y="${200 - v * 8.7}" width="42" height="${v * 8.7}" fill="${v > 15 ? '#BE3227' : '#2E3191'}"/><text x="${79 + i * 84}" y="${192 - v * 8.7}" text-anchor="middle" font-size="11.5" fill="#141726" font-family="IBM Plex Mono" font-weight="600">${v}</text><text x="${79 + i * 84}" y="220" text-anchor="middle" font-size="11" fill="#6C7488">${['Thu', 'Fri', 'Sat', 'Sun', 'Mon', 'Tue', 'Wed'][i]}</text>`).join('')}
    <line x1="34" y1="200" x2="636" y2="200" stroke="#CCD2E0"/></svg>
    <span class="sub" style="font-size:12px">Monday is the one day this account missed the target, when bookings doubled at midday.</span></div>
    <div class="col" style="gap:10px"><h2 class="h2">Health</h2>
    <div class="sheet" style="overflow:hidden">
    ${[['check', 'ok', 'Reader working', 'WhatsApp module · last read 20 s ago'], ['check', 'ok', 'Login saved on this PC', 'Survives restarts and sleep'], ['check', 'ok', 'Message previews', '96% of waiting chats'], ['info', 'neu', 'Awake', 'Every account stays awake']].map(([i, k, t, s]) =>
      `<div class="row" style="align-items:flex-start;padding:12px 14px;border-bottom:1px solid var(--line)"><span style="color:${k === 'neu' ? 'var(--ink-3)' : 'var(--ontime)'}">${ic(i, 15)}</span><div class="col" style="gap:1px"><span style="font-weight:600;font-size:12.5px">${t}</span><span class="sub" style="font-size:11.5px">${s}</span></div></div>`).join('')}
    </div></div></div>`,
});
