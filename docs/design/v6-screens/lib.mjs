// Shared look for every artboard.
//
// The design is a departures board, not a dashboard. What matters in this product is time running out on a
// waiting customer, so the waiting list is the only loud thing on any screen: a condensed clock numeral, a bar
// that fills toward the reply target, and a hairline row. Everything else — figures, locations, settings — is
// quiet type on white. Colour is reserved for lateness (on time / due / past) so it always means one thing.
// The rail and title bar are near-black indigo, which gives the app a silhouette instead of grey on grey.
export const W = 1440, H = 900;

const FONTS = 'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@500;600&family=IBM+Plex+Sans+Condensed:wght@600;700&family=IBM+Plex+Sans:wght@400;500;600;700&display=swap';

export const CSS = `
:root{
--shell:#101322;--shell-2:#191D33;--shell-3:#232842;--shell-line:#2A2F4C;--shell-ink:#EAECF7;--shell-ink-2:#9298BE;
--board:#FFFFFF;--field:#F2F4F8;--line:#E4E7EF;--line-2:#CCD2E0;--ink:#141726;--ink-2:#495064;--ink-3:#6C7488;
--brand:#2E3191;--brand-2:#4B4FD6;--wash:#EDEEFB;
--late:#BE3227;--late-w:#FBEAE8;--due:#A96206;--due-w:#FDF1E0;--ontime:#0E7A4E;--ontime-w:#E5F3EB;--info:#1D4ED8;--info-w:#E8EEFC;--neu-w:#EEF0F5;
}
*{box-sizing:border-box}
body{margin:0;background:var(--field);color:var(--ink);font:13.5px/1.5 "IBM Plex Sans","Segoe UI",system-ui,sans-serif;-webkit-font-smoothing:antialiased}
a{color:var(--brand-2)}
.mono{font-family:"IBM Plex Mono",Consolas,monospace;font-variant-numeric:tabular-nums}
.cond{font-family:"IBM Plex Sans Condensed","IBM Plex Sans",sans-serif;font-variant-numeric:tabular-nums;letter-spacing:-.01em}
.app{width:1440px;height:900px;display:grid;grid-template-rows:38px minmax(0,1fr);background:var(--shell);overflow:hidden;position:relative}

/* Title bar — part of the dark shell, not a separate strip */
.tb{display:flex;align-items:center;gap:14px;padding-left:14px;background:var(--shell);color:var(--shell-ink)}
.mark{width:20px;height:20px;border-radius:3px;background:var(--brand-2);display:grid;place-items:center;color:#fff;font-weight:700;font-size:11px}
.tb-name{font-weight:600;font-size:12.5px;letter-spacing:.01em}
.ws{display:flex;align-items:center;gap:6px;height:24px;padding:0 8px;border-radius:4px;background:var(--shell-2);color:var(--shell-ink);font-size:12.5px}
.search{flex:0 1 380px;margin-left:auto;height:24px;display:flex;align-items:center;gap:8px;padding:0 9px;background:var(--shell-2);border-radius:4px;color:var(--shell-ink-2);font-size:12.5px}
.kbd{margin-left:auto;font-size:10.5px;padding:1px 5px;border-radius:3px;background:var(--shell-3);color:var(--shell-ink-2)}
.tb-right{margin-left:auto;display:flex;align-items:center;gap:4px;height:100%}
.tb-btn{height:24px;display:flex;align-items:center;gap:6px;padding:0 8px;border-radius:4px;color:var(--shell-ink-2);font-size:12.5px}
.tb-btn.on{background:var(--brand-2);color:#fff}
.win{display:flex;height:100%;margin-left:8px}
.win span{width:44px;display:grid;place-items:center;color:var(--shell-ink-2)}
.body{display:grid;grid-template-columns:228px minmax(0,1fr);min-height:0;background:var(--shell)}
.body.panel{grid-template-columns:228px minmax(0,1fr) 372px}

/* Rail */
.side{background:var(--shell);padding:10px 10px 12px;display:flex;flex-direction:column;gap:1px;min-height:0;color:var(--shell-ink)}
.nav{display:flex;align-items:center;gap:10px;height:30px;padding:0 10px;border-radius:4px;color:var(--shell-ink-2);font-weight:500}
.nav.on{background:var(--shell-2);color:#fff}
.nav .n{margin-left:auto;font-size:11.5px;font-weight:600}
.sec{margin:16px 10px 6px;font-size:10.5px;font-weight:600;letter-spacing:.1em;text-transform:uppercase;color:#666D95}
.loc{margin:12px 10px 4px;font-size:11.5px;font-weight:600;color:var(--shell-ink-2)}
.acct{display:flex;align-items:center;gap:8px;height:28px;padding:0 10px;border-radius:4px;color:var(--shell-ink);font-size:12.5px}
.acct.on{background:var(--shell-2)}
.acct .n{margin-left:auto;font-size:11.5px;font-weight:600}
.side-foot{margin-top:auto;display:flex;flex-direction:column;gap:1px;border-top:1px solid var(--shell-line);padding-top:8px}

/* The white board the work happens on */
.main{min-width:0;min-height:0;overflow:hidden;background:var(--board);border-radius:8px 0 0 0;padding:22px 26px;display:flex;flex-direction:column;gap:16px}
.h1{font-size:23px;font-weight:600;letter-spacing:-.015em;margin:0}
.h2{font-size:15px;font-weight:600;margin:0}
.h3{font-size:13.5px;font-weight:600;margin:0}
.sub{color:var(--ink-3)}
.row{display:flex;align-items:center;gap:10px}
.col{display:flex;flex-direction:column;gap:10px}
.rule{height:1px;background:var(--line)}

/* Figure band: the day's numbers as type, not as four boxes */
.band{display:flex;align-items:stretch;border-top:1px solid var(--line);border-bottom:1px solid var(--line)}
.fig{flex:1;padding:14px 20px 13px;display:flex;flex-direction:column;gap:3px;border-left:1px solid var(--line)}
.fig:first-child{border-left:0;padding-left:0}
.fig .v{display:flex;align-items:baseline;gap:6px}
.fig .v b{font-family:"IBM Plex Sans Condensed","IBM Plex Sans",sans-serif;font-size:38px;font-weight:600;line-height:1;letter-spacing:-.02em;font-variant-numeric:tabular-nums}
.fig .u{font-size:12.5px;color:var(--ink-3)}
.fig .l{font-size:11px;font-weight:600;letter-spacing:.08em;text-transform:uppercase;color:var(--ink-3)}
.fig .n{font-size:12px;display:flex;align-items:center;gap:5px}

/* The waiting board */
.board{display:flex;flex-direction:column;min-height:0;overflow:hidden}
.brow{display:grid;grid-template-columns:78px 128px minmax(0,1fr) 128px 96px;align-items:center;gap:14px;padding:11px 12px 11px 10px;border-bottom:1px solid var(--line);border-left:3px solid transparent}
.brow.late{border-left-color:var(--late);background:linear-gradient(90deg,var(--late-w),transparent 42%)}
.brow.due{border-left-color:var(--due)}
.bhead{display:grid;grid-template-columns:78px 128px minmax(0,1fr) 128px 96px;gap:14px;padding:0 12px 7px 13px;border-bottom:1px solid var(--line-2);font-size:10.5px;font-weight:600;letter-spacing:.09em;text-transform:uppercase;color:var(--ink-3)}
.clock{font-family:"IBM Plex Sans Condensed","IBM Plex Sans",sans-serif;font-size:25px;font-weight:600;line-height:1;font-variant-numeric:tabular-nums;letter-spacing:-.01em}
.clock small{font-size:12px;font-weight:500;margin-left:4px;color:var(--ink-3)}
.meter{height:5px;border-radius:0;background:var(--field);position:relative;overflow:hidden}
.meter i{position:absolute;left:0;top:0;bottom:0;display:block}
.meter u{position:absolute;top:-2px;bottom:-2px;width:1px;background:var(--ink-3)}
.who{font-weight:600}
.prev{color:var(--ink-2);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}

/* Quiet objects */
.sheet{background:var(--board);border:1px solid var(--line);border-radius:6px}
.card{background:var(--board);border:1px solid var(--line);border-radius:6px}
.pad{padding:15px 16px}
.chip{display:inline-flex;align-items:center;gap:5px;height:21px;padding:0 8px;border-radius:3px;font-size:11.5px;font-weight:600;white-space:nowrap}
.chip.ok{background:var(--ontime-w);color:var(--ontime)} .chip.warn{background:var(--due-w);color:var(--due)} .chip.bad{background:var(--late-w);color:var(--late)}
.chip.info{background:var(--info-w);color:var(--info)} .chip.neu{background:var(--neu-w);color:var(--ink-2)} .chip.ai{background:var(--wash);color:var(--brand)}
.btn{display:inline-flex;align-items:center;justify-content:center;gap:6px;height:30px;padding:0 12px;border-radius:4px;border:1px solid var(--line-2);background:var(--board);color:var(--ink);font-weight:600;font-size:12.5px;white-space:nowrap}
.btn.primary{background:var(--brand);border-color:var(--brand);color:#fff}
.btn.danger{background:var(--late);border-color:var(--late);color:#fff}
.btn.ghost{border-color:transparent;background:transparent;color:var(--ink-2)}
.btn.lg{height:38px;padding:0 18px;font-size:13.5px}
.input{height:32px;display:flex;align-items:center;gap:8px;padding:0 10px;border:1px solid var(--line-2);border-radius:4px;background:var(--board);color:var(--ink-3);font-size:12.5px}
.label{font-size:11px;font-weight:600;letter-spacing:.08em;text-transform:uppercase;color:var(--ink-3)}
.dot{width:7px;height:7px;border-radius:50%;flex:none}
.table{width:100%;border-collapse:collapse}
.table th{font-size:10.5px;font-weight:600;letter-spacing:.09em;text-transform:uppercase;color:var(--ink-3);text-align:left;padding:9px 14px;border-bottom:1px solid var(--line-2)}
.table td{padding:11px 14px;border-bottom:1px solid var(--line);vertical-align:middle}
.avatar{width:26px;height:26px;border-radius:3px;display:grid;place-items:center;font-size:10.5px;font-weight:700;background:var(--field);color:var(--ink-2);flex:none}
.toggle{width:34px;height:19px;border-radius:10px;background:var(--line-2);position:relative;flex:none}
.toggle::after{content:"";position:absolute;top:2px;left:2px;width:15px;height:15px;border-radius:50%;background:#fff}
.toggle.on{background:var(--brand)} .toggle.on::after{left:17px}
.seg{display:inline-flex;padding:2px;background:var(--field);border-radius:5px}
.seg span{height:25px;display:flex;align-items:center;padding:0 11px;border-radius:3px;color:var(--ink-2);font-weight:500;font-size:12.5px}
.seg span.on{background:var(--board);color:var(--ink);box-shadow:0 1px 2px rgba(20,23,38,.10)}
.strip{display:flex;align-items:center;gap:10px;padding:9px 14px;border-left:3px solid;font-size:13px}
.strip.bad{border-color:var(--late);background:var(--late-w);color:var(--late)}
.strip.warn{border-color:var(--due);background:var(--due-w);color:var(--due)}
.strip.info{border-color:var(--info);background:var(--info-w);color:var(--info)}
.strip.neu{border-color:var(--line-2);background:var(--field);color:var(--ink-2)}
.banner{display:flex;align-items:center;gap:10px;padding:10px 14px;border-radius:5px}
.overlay{position:absolute;inset:38px 0 0 0;background:rgba(16,19,34,.46);display:grid;place-items:center}
.dialog{width:520px;background:var(--board);border-radius:8px;box-shadow:0 24px 60px rgba(16,19,34,.32);display:flex;flex-direction:column}
.dialog .hd{padding:20px 22px 0;display:flex;align-items:flex-start;gap:12px}
.dialog .bd{padding:14px 22px 18px;display:flex;flex-direction:column;gap:14px;color:var(--ink-2)}
.dialog .ft{padding:13px 22px;border-top:1px solid var(--line);display:flex;justify-content:flex-end;gap:8px}
.panel-r{background:var(--board);border-left:1px solid var(--line);display:flex;flex-direction:column;min-height:0}
.ghost-bg{opacity:.5;filter:saturate(.5)}
`;

const P = {
  grid: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
  star: '<path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z"/>',
  chart: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
  file: '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5M9 13h6M9 17h6"/>',
  gear: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>',
  bell: '<path d="M6 8a6 6 0 1 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10.3 21a1.9 1.9 0 0 0 3.4 0"/>',
  spark: '<path d="M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8zM19 16l.8 2.2L22 19l-2.2.8L19 22l-.8-2.2L16 19l2.2-.8z"/>',
  search: '<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  users: '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
  cloud: '<path d="M17.5 19H7a5 5 0 1 1 1.1-9.9A6 6 0 0 1 19.6 11 4 4 0 0 1 17.5 19z"/>',
  wifioff: '<path d="M2 2l20 20M8.5 16.4a5 5 0 0 1 7 0M5 12.9a10 10 0 0 1 5.2-2.8M19 12.9a10 10 0 0 0-2.5-1.8M12 20h.01"/>',
  check: '<path d="M20 6L9 17l-5-5"/>',
  alert: '<path d="M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0zM12 9v4M12 17h.01"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  down: '<path d="M6 9l6 6 6-6"/>',
  right: '<path d="M9 6l6 6-6 6"/>',
  x: '<path d="M18 6L6 18M6 6l12 12"/>',
  min: '<path d="M5 12h14"/>',
  max: '<rect x="5" y="5" width="14" height="14" rx="1"/>',
  chat: '<path d="M21 11.5a8.4 8.4 0 0 1-9 8.4 8.6 8.6 0 0 1-3.9-.9L3 20l1.1-4.6A8.4 8.4 0 1 1 21 11.5z"/>',
  insta: '<rect x="3" y="3" width="18" height="18" rx="5"/><circle cx="12" cy="12" r="4"/><path d="M17.5 6.5h.01"/>',
  google: '<path d="M20.5 12.2c0-.6-.1-1.2-.2-1.8H12v3.4h4.8a4.1 4.1 0 0 1-1.8 2.7v2.2h2.9c1.7-1.6 2.6-3.9 2.6-6.5z"/><path d="M12 21c2.4 0 4.5-.8 5.9-2.2L15 16.6a5.4 5.4 0 0 1-8.1-2.8H3.9v2.3A9 9 0 0 0 12 21z"/><path d="M6.9 13.8a5.4 5.4 0 0 1 0-3.5V7.9H3.9a9 9 0 0 0 0 8.1z"/><path d="M12 6.6c1.3 0 2.5.5 3.5 1.4l2.6-2.6A9 9 0 0 0 3.9 7.9l3 2.4A5.4 5.4 0 0 1 12 6.6z"/>',
  lock: '<rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
  refresh: '<path d="M21 12a9 9 0 1 1-2.6-6.4M21 3v6h-6"/>',
  download: '<path d="M12 3v12M7 10l5 5 5-5M5 21h14"/>',
  moon: '<path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/>',
  trash: '<path d="M3 6h18M8 6V4h8v2M6 6l1 15h10l1-15"/>',
  edit: '<path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z"/>',
  mail: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/>',
  qr: '<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><path d="M14 14h3v3h-3zM20 14v7M14 20h3"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7.5h.01"/>',
  out: '<path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"/>',
  phone: '<path d="M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2z"/>',
  send: '<path d="M22 2L11 13M22 2l-7 20-4-9-9-4z"/>',
  building: '<rect x="4" y="3" width="16" height="18" rx="1"/><path d="M9 7h1M14 7h1M9 11h1M14 11h1M9 15h1M14 15h1M10 21v-3h4v3"/>',
};
export const ic = (name, size = 16, color = 'currentColor', sw = 1.7) =>
  `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="${color}" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" style="flex:none">${P[name]}</svg>`;

export const chip = (kind, text, icon) => `<span class="chip ${kind}">${icon ? ic(icon, 12) : ''}${text}</span>`;

export const TONE = { late: 'var(--late)', due: 'var(--due)', ok: 'var(--ontime)', bad: 'var(--late)', warn: 'var(--due)', neu: 'var(--ink-3)', info: 'var(--info)' };

/** A wait drawn against its target: fill to `pct`, with a tick where the target sits. */
export const meter = (pct, kind = 'ok', target = null) =>
  `<div class="meter"><i style="width:${Math.min(100, pct)}%;background:${TONE[kind]}"></i>${target === null ? '' : `<u style="left:${target}%"></u>`}</div>`;

/** The elapsed wait, the loudest thing on the board. */
export const clock = (value, unit, kind) =>
  `<span class="clock" style="color:${TONE[kind]}">${value}<small>${unit}</small></span>`;

export const trend = (pts, color = 'var(--ink-3)', w = 62, h = 16) => {
  const max = Math.max(...pts), min = Math.min(...pts), step = w / (pts.length - 1);
  const xy = pts.map((p, i) => `${(i * step).toFixed(1)},${(h - 2 - ((p - min) / (max - min || 1)) * (h - 4)).toFixed(1)}`);
  return `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" fill="none" style="flex:none"><polyline points="${xy.join(' ')}" stroke="${color}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>`;
};

/** One number in the figure band. No box: a label, a condensed numeral, and one line of plain English. */
export const fig = (label, value, unit, note, kind = 'neu', pts = null) => `<div class="fig">
  <span class="l">${label}</span>
  <span class="v"><b style="${kind === 'neu' ? '' : `color:${TONE[kind]}`}">${value}</b><span class="u">${unit}</span></span>
  <span class="n" style="color:${kind === 'neu' ? 'var(--ink-3)' : TONE[kind]}">${note}${pts ? `<span style="margin-left:auto">${trend(pts, kind === 'neu' ? 'var(--line-2)' : TONE[kind])}</span>` : ''}</span></div>`;

export const band = (...figs) => `<div class="band">${figs.join('')}</div>`;

// Title bar. opts: ws, sync ('ok'|'syncing'|'offline'|'suspended'), aiOn, bell, user, minimal
export function titleBar(o = {}) {
  const sync = { ok: ['check', 'Synced', 'var(--shell-ink-2)'], syncing: ['refresh', 'Syncing', 'var(--shell-ink-2)'],
    offline: ['wifioff', 'Offline · 5 days left', '#E0A458'], suspended: ['lock', 'Suspended', '#E88A80'] }[o.sync || 'ok'];
  const left = `<div class="mark">U</div><span class="tb-name">Unified Messenger</span>`;
  if (o.minimal) return `<header class="tb">${left}<div class="tb-right"><div class="win"><span>${ic('min')}</span><span>${ic('max', 14)}</span><span>${ic('x')}</span></div></div></header>`;
  return `<header class="tb">${left}
  <div class="ws">${ic('building', 13)}<span>${o.ws || 'Glow Salons'}</span>${ic('down', 13)}</div>
  <div class="search">${ic('search', 13)}<span>Search customers, accounts, settings</span><span class="kbd mono">Ctrl K</span></div>
  <div class="tb-right">
    <span class="tb-btn" style="color:${sync[2]}">${ic(sync[0], 13)}<span>${sync[1]}</span></span>
    <span class="tb-btn ${o.aiOn ? 'on' : ''}">${ic('spark', 14)}<span style="font-weight:600">Ask</span></span>
    <span class="tb-btn" style="position:relative">${ic('bell', 15)}${o.bell === 0 ? '' : `<span style="position:absolute;top:1px;right:4px;min-width:14px;height:14px;border-radius:7px;background:var(--late);color:#fff;font-size:9.5px;font-weight:700;display:grid;place-items:center;padding:0 3px">${o.bell ?? 4}</span>`}</span>
    <span class="avatar" style="width:22px;height:22px;background:var(--shell-3);color:var(--shell-ink)">${o.user || 'AH'}</span>
    <div class="win"><span>${ic('min')}</span><span>${ic('max', 14)}</span><span>${ic('x')}</span></div>
  </div></header>`;
}

const ACCOUNTS = [
  ['F-11 Markaz', [['chat', 'WhatsApp · Bookings', 12, 'late'], ['google', 'Google reviews', 0, 'ok']]],
  ['DHA Phase 2', [['chat', 'WhatsApp · Front desk', 5, 'due'], ['insta', 'Instagram', 2, 'due'], ['google', 'Google reviews', 1, 'late']]],
  ['Gulberg', [['chat', 'WhatsApp · Front desk', 0, 'ok'], ['insta', 'Instagram', null, 'neu']]],
];

export function sidebar(active = 'center', activeAcct = '') {
  const nav = [['center', 'grid', 'Waiting now', '19'], ['reviews', 'star', 'Reviews', '1'], ['analytics', 'chart', 'Analytics', ''], ['reports', 'file', 'Reports', '']];
  const n = nav.map(([k, i, t, c]) => `<div class="nav ${active === k ? 'on' : ''}">${ic(i, 15)}<span>${t}</span>${c ? `<span class="n mono" style="color:${k === 'center' ? 'var(--shell-ink)' : 'var(--shell-ink-2)'}">${c}</span>` : ''}</div>`).join('');
  const accts = ACCOUNTS.map(([loc, list]) => `<div class="loc">${loc}</div>` + list.map(([i, t, c, k]) =>
    `<div class="acct ${activeAcct === t + loc ? 'on' : ''}">${ic(i, 14, 'var(--shell-ink-2)')}<span style="white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${t}</span>${c === null
      ? `<span class="n" style="color:var(--shell-ink-2);font-weight:500">Sign in</span>`
      : `<span class="n mono" style="color:${c === 0 ? '#5D6488' : k === 'late' ? '#F08A80' : k === 'due' ? '#E0A458' : 'var(--shell-ink-2)'}">${c}</span>`}</div>`).join('')).join('');
  return `<aside class="side">${n}<div class="sec">Accounts</div>${accts}
  <div class="side-foot"><div class="nav">${ic('plus', 15)}<span>Add account</span></div><div class="nav ${active === 'settings' ? 'on' : ''}">${ic('gear', 15)}<span>Settings</span></div></div></aside>`;
}

export function shell({ active = 'center', acct = '', content = '', panel = '', overlay = '', tb = {} } = {}) {
  return `<div class="app">${titleBar(tb)}<div class="body ${panel ? 'panel' : ''}">${sidebar(active, acct)}<main class="main">${content}</main>${panel}</div>${overlay}</div>`;
}

export function artboard(markup) {
  return `<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
  <link rel="stylesheet" href="${FONTS}">
  <style>${CSS}</style>
</helmet>
${markup}
</x-dc>
</body>
</html>
`;
}

export const dialog = ({ icon, iconColor = 'var(--brand)', title, body, actions }) => `<div class="overlay"><div class="dialog">
  <div class="hd">${icon ? `<div style="width:32px;height:32px;border-radius:5px;display:grid;place-items:center;background:var(--field);color:${iconColor}">${ic(icon, 17)}</div>` : ''}<div class="col" style="gap:2px"><h2 class="h2" style="font-size:16px">${title}</h2></div><span style="margin-left:auto;color:var(--ink-3)">${ic('x')}</span></div>
  <div class="bd">${body}</div><div class="ft">${actions}</div></div></div>`;
