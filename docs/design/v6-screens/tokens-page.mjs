// The design system's own specimen sheet. It shows the palette on both boards the app ships, because a
// swatch on the wrong surface proves nothing — dark steps are selected and validated against the dark
// board, not flipped from the light ones.
import { chip, clock, dayBars, ic, meter, shareLine, titleBar } from './lib.mjs';

const swatch = (c, n) => `<div class="col" style="gap:3px">
  <div style="height:34px;border-radius:4px;background:${c};border:1px solid rgba(128,128,128,.25)"></div>
  <span style="font-size:11px;font-weight:600">${n}</span><span class="mono sub" style="font-size:10.5px">${c}</span></div>`;

const WAITS = [[38, 'late', 'Past target'], [13, 'due', 'Due in 2 min'], [6, 'ok', 'On time']];

const specimen = (theme) => {
  const dark = theme === 'dark';
  return `<div class="sheet"${dark ? ' data-theme="dark"' : ''} style="padding:15px 16px;display:flex;flex-direction:column;gap:12px;background:var(--board)">
  <div class="row"><h3 class="h3">${dark ? 'Dark' : 'Light'}</h3><span class="sub" style="margin-left:auto;font-size:11.5px">board ${dark ? '#151926' : '#FFFFFF'}</span></div>
  <div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px">
    ${swatch(dark ? '#2F9463' : '#0E7A4E', 'on time')}${swatch(dark ? '#BC8A2A' : '#C07C12', 'due soon')}${swatch(dark ? '#B3352C' : '#A3201C', 'past target')}</div>
  <div class="col" style="gap:9px">
    ${WAITS.map(([w, k, st]) => `<div class="row" style="gap:12px">${clock(w, 'min', k)}
      <div class="col" style="gap:4px;flex:1">${meter((w / 45) * 100, k, (15 / 45) * 100)}
      <span style="font-size:11.5px;font-weight:600;color:var(--${k})">${st}</span></div></div>`).join('')}</div>
  <div class="row" style="flex-wrap:wrap;gap:6px">${chip('ok', 'On time', 'check')}${chip('warn', 'Due in 2 min', 'clock')}${chip('bad', 'Past target', 'clock')}${chip('neu', 'Sign in needed', 'lock')}</div>
  <div class="row" style="flex-wrap:wrap;gap:8px"><span class="btn primary">Save changes</span><span class="btn">Re-sync</span><span class="btn ghost">Cancel</span></div>
  <div class="strip warn" style="font-size:12.5px">${ic('clock', 14)}Two customers are about to be late</div></div>`;
};

const TYPE = [['23 · Page title', 23, 600], ['17 · Section', 17, 600], ['15 · Heading', 15, 600],
  ['13.5 · Body', 13.5, 400], ['12.5 · Secondary', 12.5, 400], ['11 · Label', 11, 600]];

const ICONS = ['grid', 'star', 'chart', 'file', 'gear', 'bell', 'spark', 'search', 'plus', 'users', 'shield', 'cloud',
  'wifioff', 'check', 'alert', 'clock', 'chat', 'insta', 'google', 'lock', 'refresh', 'download', 'sun', 'moon',
  'trash', 'edit', 'mail', 'qr', 'info', 'out', 'phone', 'send', 'building'];

const DAYS = { data: [12, 10, 11, 9, 18, 10, 9], labels: ['Thu', 'Fri', 'Sat', 'Sun', 'Mon', 'Tue', 'Wed'], target: 15 };
const SHARE = { data: [91, 90, 92, 88, 87, 89, 86, 88, 85, 84, 87, 86, 85, 86],
  labels: ['29', '30', '31', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11'], target: 90 };

export const Tokens = () => `<div style="width:1440px;height:900px;padding:26px;background:var(--board);display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px;align-content:start">
 <div class="col" style="grid-column:1/-1;gap:3px"><h1 class="h1" style="font-size:19px">Tokens and components</h1>
 <span class="sub" style="font-size:12.5px">One tokens.css, two themes. Colour carries one meaning only: how late a customer is. Mark steps are validated for colour-vision separation against their own board; text steps are held to WCAG contrast, which is why the two sets are different values.</span></div>

 ${specimen('light')}
 ${specimen('dark')}

 <div class="sheet" style="padding:15px 16px;display:flex;flex-direction:column;gap:9px"><h3 class="h3">Type</h3>
 <div class="cond" style="font-size:38px;font-weight:600;line-height:1">38 · 19 waiting</div>
 <span class="sub" style="font-size:11.5px">Plex Sans Condensed carries every figure the eye should land on</span>
 ${TYPE.map(([t, sz, w]) => `<div style="font-size:${sz}px;font-weight:${w};line-height:1.25;${sz === 11 ? 'letter-spacing:.08em;text-transform:uppercase;color:var(--ink-3)' : ''}">${t}</div>`).join('')}
 <div class="mono" style="font-size:15px;font-weight:500">1,284 · 82% · 4:12 pm</div>
 <span class="sub" style="font-size:11.5px">IBM Plex Sans, Mono for figures and times</span></div>

 <div class="sheet" style="padding:15px 16px;display:flex;flex-direction:column;gap:10px;grid-column:span 2">
 <div class="row"><h3 class="h3">Charts</h3><span class="sub" style="margin-left:auto;font-size:11.5px">one axis · thin marks · rounded data ends · recessive grid · the target always drawn and labelled</span></div>
 <div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px">
  <div class="col" style="gap:6px"><span class="label">Daily median against target</span>${dayBars({ ...DAYS, w: 420, h: 180 })}</div>
  <div class="col" style="gap:6px"><span class="label">Share against target, over time</span>${shareLine({ ...SHARE, w: 420, h: 180 })}</div>
 </div></div>

 <div class="sheet" style="padding:15px 16px;display:flex;flex-direction:column;gap:10px"><h3 class="h3">Title bar</h3>
 <span class="sub" style="font-size:11.5px">Light and dark live here, on every screen, beside the account avatar.</span>
 <div style="border-radius:5px;overflow:hidden;background:var(--shell)">${titleBar({ theme: 'light', bell: 0 })}</div>
 <div style="border-radius:5px;overflow:hidden;background:var(--shell)">${titleBar({ theme: 'dark', bell: 0 })}</div>
 <h3 class="h3" style="margin-top:4px">Icons · 15 and 19 px, 1.7 stroke</h3>
 <div class="row" style="flex-wrap:wrap;gap:14px;color:var(--ink-2)">${ICONS.map((i) => ic(i, 19)).join('')}</div></div>
</div>`;
