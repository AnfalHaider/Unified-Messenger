// Shared look for every artboard.
//
// The design is a departures board, not a dashboard. What matters here is time running out on a waiting
// customer, so the waiting list is the only loud thing on any screen: a condensed clock numeral, a bar
// filling toward the reply target, and a hairline row. The rail stays near-black indigo in both themes;
// the board flips. Colour means exactly one thing — how late a customer is — so it is never decoration.
//
// Colour is computed, not chosen by eye. Mark steps pass the dataviz validator (lightness band, chroma,
// CVD separation, normal-vision floor, contrast) against their own surface; text steps are held to WCAG
// contrast instead, which is why they are different values.
//   light marks  #0E7A4E #C07C12 #A3201C — all checks pass; worst adjacent pair ΔE 20.9 normal vision
//   dark marks   #2F9463 #BC8A2A #B3352C — all checks pass, inside band L .48–.67 on #151926
//   text steps   light due #9A6410 (4.99:1); dark 7–8:1 — never the mark step, which fails as text
export const W = 1440, H = 900;

const FONTS = 'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@500;600&family=IBM+Plex+Sans+Condensed:wght@600;700&family=IBM+Plex+Sans:wght@400;500;600;700&display=swap';

export const CSS = `
:root{
--shell:#0C1018;--shell-2:#161B2B;--shell-3:#222840;--shell-line:#232A40;--shell-ink:#EAECF7;--shell-ink-2:#9298BE;
--board:#FFFFFF;--field:#F2F4F8;--line:#E4E7EF;--line-2:#CCD2E0;--ink:#141726;--ink-2:#495064;--ink-3:#6C7488;
--brand-ink:#2E3191;--brand-fill:#2E3191;--wash:#EDEEFB;
--mark-ok:#0E7A4E;--mark-due:#C07C12;--mark-late:#A3201C;--mark-flat:#C9CCEC;
--ok:#0E7A4E;--due:#9A6410;--late:#A3201C;
--ok-w:#E5F3EB;--due-w:#FBF1E0;--late-w:#FBEAE8;--info:#1D4ED8;--info-w:#E8EEFC;--neu-w:#EEF0F5;
--grid:#EEF1F6;--axis:#CCD2E0;
--elev:0 1px 2px rgba(16,19,34,.05);--elev-2:0 12px 34px rgba(16,19,34,.14);
}
[data-theme="dark"]{
--board:#151926;--field:#1D2334;--line:#29304A;--line-2:#39415C;--ink:#EAECF7;--ink-2:#A8B0C6;--ink-3:#7C8499;
--brand-ink:#9AA0FF;--brand-fill:#4B4FD6;--wash:#22264A;
--mark-ok:#2F9463;--mark-due:#BC8A2A;--mark-late:#B3352C;--mark-flat:#2E3557;
--ok:#5CC38F;--due:#E0A84A;--late:#F0867C;
--ok-w:#16301F;--due-w:#2E2413;--late-w:#33181A;--info:#7DA6FF;--info-w:#17223C;--neu-w:#232A40;
--grid:#232A3E;--axis:#39415C;
--elev:0 1px 2px rgba(0,0,0,.4);--elev-2:0 14px 36px rgba(0,0,0,.5);
}
*{box-sizing:border-box}
body{margin:0;background:var(--field);color:var(--ink);font:13.5px/1.5 "IBM Plex Sans","Segoe UI",system-ui,sans-serif;-webkit-font-smoothing:antialiased}
a{color:var(--brand-ink)}
.mono{font-family:"IBM Plex Mono",Consolas,monospace;font-variant-numeric:tabular-nums}
.cond{font-family:"IBM Plex Sans Condensed","IBM Plex Sans",sans-serif;font-variant-numeric:tabular-nums;letter-spacing:-.01em}
.app{width:1440px;height:900px;display:grid;grid-template-rows:38px minmax(0,1fr);background:var(--shell);overflow:hidden;position:relative;color:var(--ink)}

.tb{display:flex;align-items:center;gap:14px;padding-left:14px;background:var(--shell);color:var(--shell-ink)}
.mark{width:20px;height:20px;border-radius:3px;background:#4B4FD6;display:grid;place-items:center;color:#fff;font-weight:700;font-size:11px}
.tb-name{font-weight:600;font-size:12.5px;letter-spacing:.01em}
.ws{display:flex;align-items:center;gap:6px;height:24px;padding:0 8px;border-radius:4px;background:var(--shell-2);color:var(--shell-ink);font-size:12.5px}
.search{flex:0 1 340px;margin-left:auto;height:24px;display:flex;align-items:center;gap:8px;padding:0 9px;background:var(--shell-2);border-radius:4px;color:var(--shell-ink-2);font-size:12.5px}
.kbd{margin-left:auto;font-size:10.5px;padding:1px 5px;border-radius:3px;background:var(--shell-3);color:var(--shell-ink-2)}
.tb-right{margin-left:auto;display:flex;align-items:center;gap:4px;height:100%}
.tb-btn{height:24px;display:flex;align-items:center;gap:6px;padding:0 8px;border-radius:4px;color:var(--shell-ink-2);font-size:12.5px}
.tb-btn.on{background:#4B4FD6;color:#fff}
.theme{display:flex;align-items:center;gap:1px;height:24px;padding:2px;border-radius:5px;background:var(--shell-2);margin-left:2px}
.theme span{width:26px;height:20px;display:grid;place-items:center;border-radius:3px;color:var(--shell-ink-2)}
.theme span.on{background:var(--shell-3);color:#fff}
.win{display:flex;height:100%;margin-left:8px}
.win span{width:44px;display:grid;place-items:center;color:var(--shell-ink-2)}
.body{display:grid;grid-template-columns:228px minmax(0,1fr);min-height:0;background:var(--shell)}
.body.panel{grid-template-columns:228px minmax(0,1fr) 372px}

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

.main{min-width:0;min-height:0;overflow:hidden;background:var(--board);border-radius:8px 0 0 0;padding:22px 26px;display:flex;flex-direction:column;gap:16px}
.h1{font-size:23px;font-weight:600;letter-spacing:-.015em;margin:0}
.h2{font-size:15px;font-weight:600;margin:0}
.h3{font-size:13.5px;font-weight:600;margin:0}
.sub{color:var(--ink-3)}
.row{display:flex;align-items:center;gap:10px}
.col{display:flex;flex-direction:column;gap:10px}
.rule{height:1px;background:var(--line)}

.band{display:flex;align-items:stretch;border-top:1px solid var(--line);border-bottom:1px solid var(--line)}
.fig{flex:1;padding:14px 20px 13px;display:flex;flex-direction:column;gap:3px;border-left:1px solid var(--line)}
.fig:first-child{border-left:0;padding-left:0}
.fig .v{display:flex;align-items:baseline;gap:6px}
.fig .v b{font-family:"IBM Plex Sans Condensed","IBM Plex Sans",sans-serif;font-size:38px;font-weight:600;line-height:1;letter-spacing:-.02em;font-variant-numeric:tabular-nums}
.fig .u{font-size:12.5px;color:var(--ink-3)}
.fig .l{font-size:11px;font-weight:600;letter-spacing:.08em;text-transform:uppercase;color:var(--ink-3)}
.fig .n{font-size:12px;display:flex;align-items:center;gap:5px}

.board{display:flex;flex-direction:column;min-height:0;overflow:hidden}
.brow{display:grid;grid-template-columns:78px 128px minmax(0,1fr) 128px 96px;align-items:center;gap:14px;padding:11px 12px 11px 10px;border-bottom:1px solid var(--line);border-left:3px solid transparent}
.brow.late{border-left-color:var(--mark-late);background:var(--late-w)}
.brow.due{border-left-color:var(--mark-due)}
.bhead{display:grid;grid-template-columns:78px 128px minmax(0,1fr) 128px 96px;gap:14px;padding:0 12px 7px 13px;border-bottom:1px solid var(--line-2);font-size:10.5px;font-weight:600;letter-spacing:.09em;text-transform:uppercase;color:var(--ink-3)}
.clock{font-family:"IBM Plex Sans Condensed","IBM Plex Sans",sans-serif;font-size:25px;font-weight:600;line-height:1;font-variant-numeric:tabular-nums;letter-spacing:-.01em}
.clock small{font-size:12px;font-weight:500;margin-left:4px;color:var(--ink-3)}
.meter{height:6px;border-radius:3px;background:var(--field);position:relative;overflow:hidden}
.meter i{position:absolute;left:0;top:0;bottom:0;display:block;border-radius:3px}
.meter u{position:absolute;top:-1px;bottom:-1px;width:2px;background:var(--board);box-shadow:0 0 0 1px var(--axis)}
.who{font-weight:600}
.prev{color:var(--ink-2);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}

.sheet{background:var(--board);border:1px solid var(--line);border-radius:6px;box-shadow:var(--elev);color:var(--ink)}
.card{background:var(--board);border:1px solid var(--line);border-radius:6px}
.pad{padding:15px 16px}
.chip{display:inline-flex;align-items:center;gap:5px;height:21px;padding:0 8px;border-radius:3px;font-size:11.5px;font-weight:600;white-space:nowrap}
.chip.ok{background:var(--ok-w);color:var(--ok)} .chip.warn{background:var(--due-w);color:var(--due)} .chip.bad{background:var(--late-w);color:var(--late)}
.chip.info{background:var(--info-w);color:var(--info)} .chip.neu{background:var(--neu-w);color:var(--ink-2)} .chip.ai{background:var(--wash);color:var(--brand-ink)}
.btn{display:inline-flex;align-items:center;justify-content:center;gap:6px;height:30px;padding:0 12px;border-radius:4px;border:1px solid var(--line-2);background:var(--board);color:var(--ink);font-weight:600;font-size:12.5px;white-space:nowrap}
.btn.primary{background:var(--brand-fill);border-color:var(--brand-fill);color:#fff}
.btn.danger{background:var(--mark-late);border-color:var(--mark-late);color:#fff}
.btn.ghost{border-color:transparent;background:transparent;color:var(--ink-2)}
.btn.lg{height:38px;padding:0 18px;font-size:13.5px}
.input{height:32px;display:flex;align-items:center;gap:8px;padding:0 10px;border:1px solid var(--line-2);border-radius:4px;background:var(--board);color:var(--ink-3);font-size:12.5px}
.label{font-size:11px;font-weight:600;letter-spacing:.08em;text-transform:uppercase;color:var(--ink-3)}
.table{width:100%;border-collapse:collapse}
.table th{font-size:10.5px;font-weight:600;letter-spacing:.09em;text-transform:uppercase;color:var(--ink-3);text-align:left;padding:9px 14px;border-bottom:1px solid var(--line-2)}
.table td{padding:11px 14px;border-bottom:1px solid var(--line);vertical-align:middle}
.avatar{width:26px;height:26px;border-radius:3px;display:grid;place-items:center;font-size:10.5px;font-weight:700;background:var(--field);color:var(--ink-2);flex:none}
.toggle{width:34px;height:19px;border-radius:10px;background:var(--line-2);position:relative;flex:none}
.toggle::after{content:"";position:absolute;top:2px;left:2px;width:15px;height:15px;border-radius:50%;background:var(--board)}
.toggle.on{background:var(--brand-fill)} .toggle.on::after{left:17px}
.seg{display:inline-flex;padding:2px;background:var(--field);border-radius:5px}
.seg span{height:25px;display:flex;align-items:center;padding:0 11px;border-radius:3px;color:var(--ink-2);font-weight:500;font-size:12.5px}
.seg span.on{background:var(--board);color:var(--ink);box-shadow:var(--elev)}
.strip{display:flex;align-items:center;gap:10px;padding:9px 14px;border-left:3px solid;font-size:13px}
.strip.bad{border-color:var(--mark-late);background:var(--late-w);color:var(--late)}
.strip.warn{border-color:var(--mark-due);background:var(--due-w);color:var(--due)}
.strip.info{border-color:var(--info);background:var(--info-w);color:var(--info)}
.strip.neu{border-color:var(--line-2);background:var(--field);color:var(--ink-2)}
.banner{display:flex;align-items:center;gap:10px;padding:10px 14px;border-radius:5px}
.overlay{position:absolute;inset:38px 0 0 0;background:rgba(8,10,18,.5);display:grid;place-items:center}
.dialog{width:520px;background:var(--board);border-radius:8px;box-shadow:var(--elev-2);display:flex;flex-direction:column}
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
  sun: '<circle cx="12" cy="12" r="4.2"/><path d="M12 2.4v2M12 19.6v2M4.6 4.6l1.4 1.4M18 18l1.4 1.4M2.4 12h2M19.6 12h2M4.6 19.4L6 18M18 6l1.4-1.4"/>',
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

// Text tone (WCAG-checked) and mark tone (validator-checked) are deliberately different values.
export const TONE = { late: 'var(--late)', due: 'var(--due)', ok: 'var(--ok)', bad: 'var(--late)', warn: 'var(--due)', neu: 'var(--ink-3)', info: 'var(--info)' };
export const MARK = { late: 'var(--mark-late)', due: 'var(--mark-due)', ok: 'var(--mark-ok)', bad: 'var(--mark-late)', warn: 'var(--mark-due)', neu: 'var(--mark-flat)', info: 'var(--info)' };

/** A wait drawn against its target: fill to `pct`, with a notch where the target sits. */
export const meter = (pct, kind = 'ok', target = null) =>
  `<div class="meter"><i style="width:${Math.min(100, pct)}%;background:${MARK[kind]}"></i>${target === null ? '' : `<u style="left:${target}%"></u>`}</div>`;

export const clock = (value, unit, kind) =>
  `<span class="clock" style="color:${TONE[kind]}">${value}<small>${unit}</small></span>`;

/** Sparkline beside a figure: a 2px line, no fill, no markers, no axis. It never stands on its own. */
export const trend = (pts, color = 'var(--ink-3)', w = 62, h = 18) => {
  const max = Math.max(...pts), min = Math.min(...pts), step = w / (pts.length - 1);
  const xy = pts.map((p, i) => `${(i * step).toFixed(1)},${(h - 2 - ((p - min) / (max - min || 1)) * (h - 4)).toFixed(1)}`);
  return `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" fill="none" style="flex:none"><polyline points="${xy.join(' ')}" stroke="${color}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>`;
};

export const fig = (label, value, unit, note, kind = 'neu', pts = null) => `<div class="fig">
  <span class="l">${label}</span>
  <span class="v"><b style="${kind === 'neu' ? '' : `color:${TONE[kind]}`}">${value}</b><span class="u">${unit}</span></span>
  <span class="n" style="color:${kind === 'neu' ? 'var(--ink-3)' : TONE[kind]}">${note}${pts ? `<span style="margin-left:auto">${trend(pts, kind === 'neu' ? 'var(--line-2)' : TONE[kind])}</span>` : ''}</span></div>`;

export const band = (...figs) => `<div class="band">${figs.join('')}</div>`;

// ---- charts ------------------------------------------------------------------------------------
// House rules, from the dataviz method: one axis only, thin marks, 4px rounded data ends anchored to the
// baseline, a gap between neighbouring bars, a recessive grid, and labels only where they earn their
// place. A single series carries no legend — the heading names it. Lateness is never colour alone: the
// target rule is drawn and labelled, and every bar past it carries its value in the late ink.

const barPath = (x, y, w, h, r = 4) => {
  const rr = Math.max(0, Math.min(r, h, w / 2));
  return `M${x},${y + h}V${y + rr}a${rr},${rr} 0 0 1 ${rr},-${rr}h${(w - 2 * rr).toFixed(1)}a${rr},${rr} 0 0 1 ${rr},${rr}V${y + h}Z`;
};

/** Daily medians against the reply target. */
export function dayBars({ data, labels, target, w = 640, h = 236, unit = 'min', hover = null }) {
  const padL = 34, padB = 26, padT = 22;
  const top = Math.max(target * 1.4, ...data) * 1.15;
  const plot = h - padB - padT, step = (w - padL) / data.length, bw = Math.min(44, step - 14);
  const y = (v) => padT + plot - (v / top) * plot;
  const bars = data.map((v, i) => {
    const x = padL + i * step + (step - bw) / 2, late = v > target;
    return `<path d="${barPath(x, y(v), bw, y(0) - y(v))}" fill="${late ? 'var(--mark-late)' : 'var(--mark-flat)'}"/>
      <text x="${(x + bw / 2).toFixed(1)}" y="${(y(v) - 8).toFixed(1)}" text-anchor="middle" font-size="11.5" font-family="IBM Plex Mono" font-weight="600" fill="${late ? 'var(--late)' : 'var(--ink-2)'}">${v}</text>
      <text x="${(x + bw / 2).toFixed(1)}" y="${h - 7}" text-anchor="middle" font-size="11" fill="var(--ink-3)">${labels[i]}</text>`;
  }).join('');
  const tip = hover === null ? '' : (() => {
    const x = padL + hover * step + step / 2;
    return `<line x1="${x.toFixed(1)}" y1="${padT - 6}" x2="${x.toFixed(1)}" y2="${y(0).toFixed(1)}" stroke="var(--axis)" stroke-dasharray="2 3"/>
      <g transform="translate(${(x + 12).toFixed(1)},${(y(data[hover]) - 44).toFixed(1)})">
      <rect width="132" height="40" rx="5" fill="var(--ink)"/>
      <text x="10" y="17" font-size="11.5" font-weight="600" fill="var(--board)">${labels[hover]} · ${data[hover]} ${unit}</text>
      <text x="10" y="31" font-size="11" fill="var(--board)" opacity=".7">${data[hover] > target ? 'past the target' : 'inside the target'}</text></g>`;
  })();
  return `<svg viewBox="0 0 ${w} ${h}" width="100%" height="${h}" role="img">
    <line x1="${padL}" y1="${y(top * 0.75).toFixed(1)}" x2="${w}" y2="${y(top * 0.75).toFixed(1)}" stroke="var(--grid)"/>
    <line x1="${padL}" y1="${y(target).toFixed(1)}" x2="${w}" y2="${y(target).toFixed(1)}" stroke="var(--mark-late)" stroke-dasharray="4 4"/>
    <text x="${padL - 6}" y="${(y(target) + 4).toFixed(1)}" text-anchor="end" font-size="11" font-family="IBM Plex Mono" fill="var(--late)">${target}</text>
    <text x="${padL - 6}" y="${(y(0) + 4).toFixed(1)}" text-anchor="end" font-size="11" font-family="IBM Plex Mono" fill="var(--ink-3)">0</text>
    ${bars}${tip}
    <line x1="${padL}" y1="${y(0).toFixed(1)}" x2="${w}" y2="${y(0).toFixed(1)}" stroke="var(--axis)"/></svg>`;
}

/** A distribution across the working day: one hue, with the busiest hours brought forward. */
export function hourBars({ data, startHour = 9, w = 700, h = 240, peak = 33 }) {
  const padL = 30, padB = 24, padT = 14;
  const top = Math.max(...data) * 1.12, plot = h - padB - padT, step = (w - padL) / data.length, bw = step - 13;
  const y = (v) => padT + plot - (v / top) * plot;
  return `<svg viewBox="0 0 ${w} ${h}" width="100%" height="${h}" role="img">
    ${[Math.round(top / 2), Math.round(top)].map((v) => `<line x1="${padL}" y1="${y(v).toFixed(1)}" x2="${w}" y2="${y(v).toFixed(1)}" stroke="var(--grid)"/><text x="${padL - 6}" y="${(y(v) + 4).toFixed(1)}" text-anchor="end" font-size="10.5" font-family="IBM Plex Mono" fill="var(--ink-3)">${v}</text>`).join('')}
    ${data.map((v, i) => {
      const x = padL + i * step + 6.5;
      return `<path d="${barPath(x, y(v), bw, y(0) - y(v), 3)}" fill="${v >= peak ? 'var(--brand-fill)' : 'var(--mark-flat)'}"/>
        ${i % 2 === 0 ? `<text x="${(x + bw / 2).toFixed(1)}" y="${h - 6}" text-anchor="middle" font-size="10.5" font-family="IBM Plex Mono" fill="var(--ink-3)">${startHour + i}</text>` : ''}`;
    }).join('')}
    <line x1="${padL}" y1="${y(0).toFixed(1)}" x2="${w}" y2="${y(0).toFixed(1)}" stroke="var(--axis)"/></svg>`;
}

/** The on-time share over time against its target: one line, a labelled target rule, last point called out. */
export function shareLine({ data, labels, target, w = 640, h = 210 }) {
  const padL = 34, padB = 24, padT = 18;
  const lo = Math.floor(Math.min(target, ...data) - 5), hi = 100;
  const plot = h - padB - padT, step = (w - padL - 20) / (data.length - 1);
  const y = (v) => padT + plot - ((v - lo) / (hi - lo)) * plot;
  const pts = data.map((v, i) => `${(padL + i * step).toFixed(1)},${y(v).toFixed(1)}`);
  const last = data[data.length - 1], lx = padL + (data.length - 1) * step;
  const tone = last < target ? 'late' : 'ok';
  return `<svg viewBox="0 0 ${w} ${h}" width="100%" height="${h}" role="img">
    ${[hi, target, lo].map((v) => `<line x1="${padL}" y1="${y(v).toFixed(1)}" x2="${w}" y2="${y(v).toFixed(1)}" stroke="${v === target ? 'var(--mark-due)' : 'var(--grid)'}"${v === target ? ' stroke-dasharray="4 4"' : ''}/><text x="${padL - 6}" y="${(y(v) + 4).toFixed(1)}" text-anchor="end" font-size="10.5" font-family="IBM Plex Mono" fill="${v === target ? 'var(--due)' : 'var(--ink-3)'}">${v}</text>`).join('')}
    <polyline points="${pts.join(' ')}" fill="none" stroke="${MARK[tone]}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
    <circle cx="${lx.toFixed(1)}" cy="${y(last).toFixed(1)}" r="4.5" fill="${MARK[tone]}" stroke="var(--board)" stroke-width="2"/>
    <text x="${(lx - 8).toFixed(1)}" y="${(y(last) - 13).toFixed(1)}" text-anchor="end" font-size="12" font-family="IBM Plex Mono" font-weight="600" fill="${TONE[tone]}">${last}%</text>
    ${labels.map((l, i) => (i % 2 === 0 ? `<text x="${(padL + i * step).toFixed(1)}" y="${h - 6}" text-anchor="middle" font-size="10.5" fill="var(--ink-3)">${l}</text>` : '')).join('')}
    <line x1="${padL}" y1="${y(lo).toFixed(1)}" x2="${w}" y2="${y(lo).toFixed(1)}" stroke="var(--axis)"/></svg>`;
}

// ---- chrome ------------------------------------------------------------------------------------

/** Light and dark sit in the title bar, the one strip present on every screen. */
export const themeToggle = (mode = 'light') =>
  `<div class="theme"><span class="${mode === 'light' ? 'on' : ''}">${ic('sun', 13)}</span><span class="${mode === 'dark' ? 'on' : ''}">${ic('moon', 13)}</span></div>`;

export function titleBar(o = {}) {
  const sync = { ok: ['check', 'Synced', 'var(--shell-ink-2)'], syncing: ['refresh', 'Syncing', 'var(--shell-ink-2)'],
    offline: ['wifioff', 'Offline · 5 days left', '#E0A458'], suspended: ['lock', 'Suspended', '#F0867C'] }[o.sync || 'ok'];
  const left = `<div class="mark">U</div><span class="tb-name">Unified Messenger</span>`;
  if (o.minimal) return `<header class="tb">${left}<div class="tb-right">${themeToggle(o.theme)}<div class="win"><span>${ic('min')}</span><span>${ic('max', 14)}</span><span>${ic('x')}</span></div></div></header>`;
  return `<header class="tb">${left}
  <div class="ws">${ic('building', 13)}<span>${o.ws || 'Glow Salons'}</span>${ic('down', 13)}</div>
  <div class="search">${ic('search', 13)}<span>Search customers, accounts, settings</span><span class="kbd mono">Ctrl K</span></div>
  <div class="tb-right">
    <span class="tb-btn" style="color:${sync[2]}">${ic(sync[0], 13)}<span>${sync[1]}</span></span>
    <span class="tb-btn ${o.aiOn ? 'on' : ''}">${ic('spark', 14)}<span style="font-weight:600">Ask</span></span>
    <span class="tb-btn" style="position:relative">${ic('bell', 15)}${o.bell === 0 ? '' : `<span style="position:absolute;top:1px;right:4px;min-width:14px;height:14px;border-radius:7px;background:#D9534A;color:#fff;font-size:9.5px;font-weight:700;display:grid;place-items:center;padding:0 3px">${o.bell ?? 4}</span>`}</span>
    ${themeToggle(o.theme)}
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
      : `<span class="n mono" style="color:${c === 0 ? '#5D6488' : k === 'late' ? '#F0867C' : k === 'due' ? '#E0A84A' : 'var(--shell-ink-2)'}">${c}</span>`}</div>`).join('')).join('');
  return `<aside class="side">${n}<div class="sec">Accounts</div>${accts}
  <div class="side-foot"><div class="nav">${ic('plus', 15)}<span>Add account</span></div><div class="nav ${active === 'settings' ? 'on' : ''}">${ic('gear', 15)}<span>Settings</span></div></div></aside>`;
}

export function shell({ active = 'center', acct = '', content = '', panel = '', overlay = '', tb = {}, theme = 'light' } = {}) {
  return `<div class="app"${theme === 'dark' ? ' data-theme="dark"' : ''}>${titleBar({ ...tb, theme })}<div class="body ${panel ? 'panel' : ''}">${sidebar(active, acct)}<main class="main">${content}</main>${panel}</div>${overlay}</div>`;
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

export const dialog = ({ icon, iconColor = 'var(--brand-ink)', title, body, actions }) => `<div class="overlay"><div class="dialog">
  <div class="hd">${icon ? `<div style="width:32px;height:32px;border-radius:5px;display:grid;place-items:center;background:var(--field);color:${iconColor}">${ic(icon, 17)}</div>` : ''}<div class="col" style="gap:2px"><h2 class="h2" style="font-size:16px">${title}</h2></div><span style="margin-left:auto;color:var(--ink-3)">${ic('x')}</span></div>
  <div class="bd">${body}</div><div class="ft">${actions}</div></div></div>`;
