
const S = {
  chat: '<path d="M3.5 13.5l1-3A5.5 5.5 0 1 1 7 13" /><path d="M3.5 13.5l3-.6"/>',
  ig: '<rect x="2.5" y="2.5" width="11" height="11" rx="3.2"/><circle cx="8" cy="8" r="2.6"/><circle cx="11.3" cy="4.7" r=".6" fill="currentColor"/>',
  star: '<path d="M8 2.3l1.7 3.6 3.9.5-2.9 2.7.8 3.9L8 11.1 4.5 13l.8-3.9-2.9-2.7 3.9-.5z"/>',
  line: '<path d="M2 11h12"/><circle cx="4.5" cy="7" r="1.8"/><circle cx="9" cy="5" r="1.8"/><circle cx="12.5" cy="8" r="1.8"/>',
  grid: '<rect x="2.5" y="2.5" width="4.5" height="4.5" rx="1"/><rect x="9" y="2.5" width="4.5" height="4.5" rx="1"/><rect x="2.5" y="9" width="4.5" height="4.5" rx="1"/><rect x="9" y="9" width="4.5" height="4.5" rx="1"/>',
  gear: '<circle cx="8" cy="8" r="2.2"/><path d="M8 1.8v1.8M8 12.4v1.8M1.8 8h1.8M12.4 8h1.8M3.6 3.6l1.3 1.3M11.1 11.1l1.3 1.3M3.6 12.4l1.3-1.3M11.1 4.9l1.3-1.3"/>',
  sun: '<circle cx="8" cy="8" r="2.8"/><path d="M8 1.5v1.6M8 12.9v1.6M1.5 8h1.6M12.9 8h1.6M3.4 3.4l1.1 1.1M11.5 11.5l1.1 1.1M3.4 12.6l1.1-1.1M11.5 4.5l1.1-1.1"/>',
  moon: '<path d="M12.8 9.6A5.2 5.2 0 0 1 6.4 3.2 5.2 5.2 0 1 0 12.8 9.6z"/>',
  monitor: '<rect x="2" y="3" width="12" height="8" rx="1.3"/><path d="M6 14h4M8 11v3"/>',
  refresh: '<path d="M13 8a5 5 0 1 1-1.5-3.6"/><path d="M13 2.5v3h-3"/>',
  check: '<path d="M3 8.5l3 3 7-7"/>',
  open: '<path d="M9 3h4v4M13 3L7.5 8.5"/><path d="M11 9.5V13H3V5h3.5"/>',
  snooze: '<circle cx="8" cy="8.5" r="5"/><path d="M8 5.8v2.9l2 1.2M3 2.5l2 1.3M13 2.5l-2 1.3"/>',
  lock: '<rect x="3.5" y="7" width="9" height="6.5" rx="1.3"/><path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7"/>',
  alert: '<path d="M8 2.5l6 10.5H2z"/><path d="M8 6.5v3M8 11.3v.2"/>',
  bell: '<path d="M4 11V7a4 4 0 0 1 8 0v4l1.2 1.5H2.8z"/><path d="M6.6 14a1.5 1.5 0 0 0 2.8 0"/>',
  x: '<path d="M4 4l8 8M12 4l-8 8"/>',
  min: '<path d="M3.5 8h9"/>', max: '<rect x="3.5" y="3.5" width="9" height="9" rx="1"/>',
  right: '<path d="M6 3.5L10.5 8 6 12.5"/>', qr: '<rect x="2.5" y="2.5" width="4" height="4"/><rect x="9.5" y="2.5" width="4" height="4"/><rect x="2.5" y="9.5" width="4" height="4"/><path d="M9.5 9.5h1.5v1.5M13.5 9.5v4h-4"/>',
  sleep: '<path d="M12.8 9.6A5.2 5.2 0 0 1 6.4 3.2 5.2 5.2 0 1 0 12.8 9.6z"/><path d="M10 2.5h3l-3 3h3"/>',
};
const ic = (n, s = 16, sw = 1.7) => `<svg width="${s}" height="${s}" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${S[n]}</svg>`;
const chIcon = { wa: 'chat', ig: 'ig', g: 'star' };

// ---- invented customers, real locations ----------------------------------------------------------
const TARGET = 15;
const LOCS = [
  { key: 'F-11', name: 'Depilex F-11' },
  { key: 'DHA-2', name: 'Depilex DHA-2' },
  { key: 'Men DHA-2', name: 'Depilex Men DHA-2' },
];
const PEOPLE = [
  ['Zainab T.', 'DHA-2', 'wa', 71, 'Still waiting to hear if 3pm is confirmed'],
  ['Ayesha K.', 'F-11', 'ig', 52, 'Bridal package price? Date is 14 Nov'],
  ['Sara M.', 'F-11', 'wa', 38, 'Can I move my 5pm to tomorrow?'],
  ['Bilal R.', 'Men DHA-2', 'wa', 29, 'Beard trim walk-in possible?'],
  ['Rabia N.', 'F-11', 'wa', 24, 'Voice message · 0:42'],
  ['Sana Q.', 'DHA-2', 'ig', 19, 'Do you do keratin for short hair?'],
  ['Hira A.', 'F-11', 'wa', 17, 'Any slot on Saturday morning?'],
  ['Maryam D.', 'DHA-2', 'wa', 14, 'Running 10 min late, sorry'],
  ['Omar K.', 'Men DHA-2', 'wa', 13, 'Price for hair colour?'],
  ['Mahnoor S.', 'F-11', 'wa', 12, 'Is Nabila available today?'],
  ['Hamza I.', 'Men DHA-2', 'wa', 11, 'Open on Sunday?'],
  ['Noor F.', 'F-11', 'wa', 9, 'Hydrafacial kitna ka hai?'],
  ['Kinza W.', 'DHA-2', 'wa', 7, 'Ok, and the address?'],
  ['Fatima Z.', 'F-11', 'wa', 6, 'Thanks! Parking bhi hai?'],
  ['Usman J.', 'Men DHA-2', 'wa', 5, 'Book me for 8pm please'],
  ['Areeba P.', 'DHA-2', 'ig', 4, 'Sticker'],
  ['Amna H.', 'F-11', 'ig', 3, 'Photo'],
  ['Hafsa L.', 'DHA-2', 'wa', 2, 'Is the 20% offer still on?'],
  ['Iqra B.', 'F-11', 'wa', 1, 'Hello'],
].map(([name, loc, ch, min, pv]) => ({ name, loc, ch, min, pv, tone: min > TARGET ? 'late' : min >= TARGET - 5 ? 'due' : 'ok' }));
const initials = (n) => n.split(/\s+/).map((w) => w[0]).join('').slice(0, 2);
const acctName = (p) => `${p.loc} ${p.ch === 'ig' ? 'Instagram' : 'WhatsApp'}`;
const statusText = (p) => p.tone === 'late' ? `${p.min - TARGET} min past target` : p.tone === 'due' ? `Due in ${TARGET - p.min} min` : 'On time';

// ---- theme: one setting drives the page and every board ------------------------------------------
let theme = 'system';
try { theme = localStorage.getItem('fd-theme') || 'system'; } catch (e) {}
function applyTheme(t) {
  theme = t;
  if (t === 'system') delete document.documentElement.dataset.theme; else document.documentElement.dataset.theme = t;
  try { localStorage.setItem('fd-theme', t); } catch (e) {}
  document.querySelectorAll('.theme3').forEach((g) => g.querySelectorAll('button').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.t === t))));
}
const theme3 = () => `<div class="theme3" role="group" aria-label="Theme">${[['system', 'monitor', 'Match Windows'], ['light', 'sun', 'Light'], ['dark', 'moon', 'Dark']].map(([t, i, l]) => `<button data-t="${t}" aria-label="${l}" title="${l}" aria-pressed="false">${ic(i, 14)}</button>`).join('')}</div>`;

// ---- shell ------------------------------------------------------------------------------------------
function titleBar({ scope = 'All', needsOpen = false, needs = 4 } = {}) {
  const counts = { All: PEOPLE.length };
  LOCS.forEach((l) => (counts[l.key] = PEOPLE.filter((p) => p.loc === l.key).length));
  return `<header class="tb">
    <div class="mark">U</div><span class="tb-name">Unified Messenger</span>
    <div class="scope" role="group" aria-label="Locations">${['All', ...LOCS.map((l) => l.key)].map((k) => `<button aria-pressed="${k === scope}">${k === 'All' ? 'All locations' : k} <b class="num">${counts[k]}</b></button>`).join('')}</div>
    <div class="tb-right">
      <span class="fresh"><i></i>Read 20 s ago</span>
      <button class="needs" aria-expanded="${needsOpen}">${ic('bell', 14)}Needs you <span class="dot num">${needs}</span></button>
      ${theme3()}
      <div class="win"><span>${ic('min', 14)}</span><span>${ic('max', 13)}</span><span>${ic('x', 14)}</span></div>
    </div>
  </header>`;
}
function rail(active) {
  const item = (key, icon, label, badge) => `<button aria-current="${active === key ? 'page' : 'false'}">${ic(icon, 20, 1.6)}<span>${label}</span>${badge ? `<span class="badge num">${badge}</span>` : ''}</button>`;
  return `<nav class="rail">${item('line', 'line', 'The line', PEOPLE.length)}${item('accounts', 'grid', 'Accounts')}<div class="foot">${item('settings', 'gear', 'Settings')}</div></nav>`;
}
const app = (o, inner) => `<div class="app">${titleBar(o)}<div class="body">${rail(o.active)}${inner}</div></div>`;

// ---- the line --------------------------------------------------------------------------------------
function theLine({ selected = null, people = PEOPLE } = {}) {
  const W = 1100, OVER = 1030;                       // track width; 0–60 min spans 0..OVER, 60+ bin beyond
  const x = (m) => (m > 60 ? OVER + (W - OVER) / 2 : (m / 60) * OVER);
  const lanes = LOCS.map((l) => {
    const here = people.filter((p) => p.loc === l.key).sort((a, b) => a.min - b.min);
    const rows = [-Infinity, -Infinity];
    const toks = here.map((p) => {
      const px = x(p.min);
      let r = rows.findIndex((last) => px - last >= 31);
      if (r < 0) r = rows[0] <= rows[1] ? 0 : 1;
      rows[r] = px;
      const top = here.length > 1 ? (r === 0 ? 5 : 33) : 19;
      return `<span class="tok ${p.tone} ${selected === p.name ? 'sel' : ''}" style="left:${px}px;top:${top}px" title="${p.name}, ${p.min} min">${initials(p.name)}</span>`;
    }).join('');
    const late = here.filter((p) => p.tone === 'late').length;
    return `<div class="lane-label"><b>${l.key}</b><span>${here.length ? `${here.length} waiting${late ? `, ${late} late` : ''}` : 'nobody waiting'}</span></div>
      <div class="lane"><div class="zone-due" style="left:${x(10)}px;width:${x(15) - x(10)}px"></div><div class="zone-late" style="left:${x(15)}px;width:${OVER - x(15)}px"></div><div class="zone-over" style="left:${OVER}px;right:0"></div>${toks}</div>`;
  }).join('');
  const ticks = [0, 5, 10, 15, 20, 30, 40, 50, 60].map((m) => `<span style="left:${x(m)}px">${m === 0 ? '0 min' : m}</span>`).join('') + `<span style="left:${x(61)}px">60+</span>`;
  return `<section class="line" aria-label="The line">
    <div class="line-top"><strong>The line</strong><span>minutes waited, counted in opening hours only</span>
      <div class="legend"><span><i style="border-color:var(--m-ok)"></i>On time</span><span><i style="border-color:var(--m-due)"></i>Due within 5 min</span><span><i style="border-color:var(--m-late);background:var(--m-late)"></i>Past target</span></div></div>
    <div class="lanes">
      <div style="position:absolute;left:150px;top:26px;bottom:24px;width:${W}px;pointer-events:none"><div class="target" style="left:${x(15)}px;top:-0px"><span style="top:-22px">Target 15 min</span></div></div>
      <div style="grid-column:1/3;height:26px"></div>
      ${lanes}
      <div class="axis">${ticks}</div>
    </div>
  </section>`;
}
function row(p, { sel = false, mini = false } = {}) {
  if (mini) {
    return `<div class="row ${p.tone} ${sel ? 'sel' : ''}"><span class="wait ${p.tone}">${p.min}<small>min</small></span>
      <span class="who"><b>${p.name}</b><span>${ic(chIcon[p.ch], 12)} ${p.loc} · ${p.pv}</span></span><span style="color:var(--ink-3)">${ic('right', 15)}</span></div>`;
  }
  const tail = sel
    ? `<div class="row-actions"><button class="btn primary">${ic('open', 14)}Open chat</button><button class="btn">${ic('check', 14)}Handled</button><button class="btn">${ic('snooze', 14)}Snooze</button></div>`
    : `<span class="status ${p.tone}">${statusText(p)}</span>`;
  return `<div class="row ${p.tone} ${sel ? 'sel' : ''}"><span class="wait ${p.tone}">${p.min}<small>min</small></span>
    <span class="who"><b>${p.name}</b><span>${p.pv}</span></span>
    <span class="acct">${ic(chIcon[p.ch], 15)}${acctName(p)}</span>${tail}</div>`;
}
const late = PEOPLE.filter((p) => p.tone === 'late').length;
const due = PEOPLE.filter((p) => p.tone === 'due').length;

