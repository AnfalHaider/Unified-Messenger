// Shared pieces for the wider board set: more icons, the full sidebar, and the charts.
// Colour rule holds everywhere: lateness gets status colour, every other mark is ink.

Object.assign(S, {
  reviews: '<path d="M8 2.3l1.7 3.6 3.9.5-2.9 2.7.8 3.9L8 11.1 4.5 13l.8-3.9-2.9-2.7 3.9-.5z"/>',
  chart: '<path d="M2.5 13.5h11"/><path d="M4 11V8M7 11V4.5M10 11V6.5M13 11V9"/>',
  spark: '<path d="M8 2v3M8 11v3M2 8h3M11 8h3M4 4l2 2M10 10l2 2M4 12l2-2M10 6l2-2"/>',
  users: '<circle cx="6" cy="5.5" r="2.3"/><path d="M1.8 13.5a4.2 4.2 0 0 1 8.4 0"/><path d="M10.5 3.4a2.3 2.3 0 0 1 0 4.3M12 9.6a4.2 4.2 0 0 1 2.2 3.9"/>',
  cloud: '<path d="M4.5 12.5h7a3 3 0 0 0 .4-6 4 4 0 0 0-7.7 1A2.5 2.5 0 0 0 4.5 12.5z"/>',
  download: '<path d="M8 2.5v8M4.8 7.5L8 10.7l3.2-3.2M3 13.5h10"/>',
  copy: '<rect x="5.5" y="5.5" width="8" height="8" rx="1.4"/><path d="M10.5 5.5V3.2a.7.7 0 0 0-.7-.7H3.2a.7.7 0 0 0-.7.7v6.6c0 .4.3.7.7.7h2.3"/>',
  phone: '<path d="M4.2 2.5h2l1 3-1.5 1a7 7 0 0 0 3.8 3.8l1-1.5 3 1v2a1.5 1.5 0 0 1-1.6 1.5A11 11 0 0 1 2.7 4.1 1.5 1.5 0 0 1 4.2 2.5z"/>',
  note: '<path d="M3.5 2.5h6l3 3v8h-9z"/><path d="M9.5 2.5v3h3M5.5 8.5h5M5.5 11h3.5"/>',
  tag: '<path d="M2.5 2.5h5.3l5.7 5.7-5.3 5.3-5.7-5.7z"/><circle cx="5.3" cy="5.3" r="1"/>',
  cal: '<rect x="2.5" y="3.5" width="11" height="10" rx="1.3"/><path d="M2.5 6.5h11M5.5 2v3M10.5 2v3"/>',
  shield: '<path d="M8 2l5 2v4c0 3-2.2 5.2-5 6-2.8-.8-5-3-5-6V4z"/>',
  offline: '<path d="M2 5.5a9 9 0 0 1 12 0M4.3 8a5.7 5.7 0 0 1 7.4 0M6.6 10.5a2.3 2.3 0 0 1 2.8 0"/><path d="M2.5 2.5l11 11"/>',
  search: '<circle cx="7" cy="7" r="4.3"/><path d="M10.2 10.2l3.3 3.3"/>',
  export: '<path d="M8 10V2.5M4.8 5.7L8 2.5l3.2 3.2"/><path d="M3 9v4.5h10V9"/>',
  sunrise: '<path d="M2 12.5h12M4 12.5a4 4 0 0 1 8 0M8 3v3M3.5 6.5l1.2 1.2M12.5 6.5l-1.2 1.2"/>',
  reopen: '<path d="M3 8a5 5 0 0 1 8.6-3.5L13 6"/><path d="M13 2.5V6H9.5"/><path d="M13 8a5 5 0 0 1-8.6 3.5L3 10"/>',
  key: '<circle cx="5.5" cy="10.5" r="2.8"/><path d="M7.5 8.5l6-6M11 5l2 2M9.5 6.5l1.5 1.5"/>',
  send: '<path d="M2.5 8L13.5 2.5 11 13.5 8 9.5z"/><path d="M8 9.5l5.5-7"/>',
  clock: '<circle cx="8" cy="8" r="5.5"/><path d="M8 5v3.2l2.2 1.3"/>',
  more: '<circle cx="4" cy="8" r=".9" fill="currentColor"/><circle cx="8" cy="8" r=".9" fill="currentColor"/><circle cx="12" cy="8" r=".9" fill="currentColor"/>',
});

function rail(active) {
  const item = (key, icon, label, badge) => `<button aria-current="${active === key ? 'page' : 'false'}">${ic(icon, 20, 1.6)}<span>${label}</span>${badge ? `<span class="badge num">${badge}</span>` : ''}</button>`;
  return `<nav class="rail">${item('line', 'line', 'The line', active === 'empty' ? 0 : PEOPLE.length)}${item('accounts', 'grid', 'Accounts')}${item('reviews', 'reviews', 'Reviews', 3)}${item('reports', 'chart', 'Reports')}${item('assistant', 'spark', 'Assistant')}<div class="foot">${item('settings', 'gear', 'Settings')}</div></nav>`;
}

/** A small trend: ink line, soft area, emphasised last point. */
function spark(values, { w = 120, h = 30, max = Math.max(...values), min = 0, color = 'var(--ink)' } = {}) {
  const x = (i) => 2 + (i * (w - 4)) / (values.length - 1);
  const y = (v) => h - 3 - ((v - min) / (max - min || 1)) * (h - 6);
  const pts = values.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
  const last = values.length - 1;
  return `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" aria-hidden="true"><polygon points="${x(0)},${h} ${pts} ${x(last)},${h}" fill="var(--ink)" opacity=".07"/><polyline points="${pts}" fill="none" stroke="${color}" stroke-width="1.6" stroke-linejoin="round"/><circle cx="${x(last)}" cy="${y(values[last])}" r="2.6" fill="${color}"/></svg>`;
}

/**
 * Lines over time. Series are told apart by dash pattern and a direct label at the end, never by colour.
 * series: [{ label, values, dash }], labels: x labels, target: optional horizontal line.
 */
function lineChart({ series, labels, w = 760, h = 250, min = 0, max = 100, unit = '', ticks = [0, 25, 50, 75, 100], target = null, targetLabel = '' }) {
  const pl = 38, pr = 110, pt = 14, pb = 28;
  const x = (i) => pl + (i * (w - pl - pr)) / (labels.length - 1);
  const y = (v) => pt + (h - pt - pb) * (1 - (v - min) / (max - min));
  const grid = ticks.map((t) => `<line x1="${pl}" x2="${w - pr}" y1="${y(t)}" y2="${y(t)}" stroke="var(--line)"/><text x="${pl - 8}" y="${y(t) + 4}" text-anchor="end" font-size="11" fill="var(--ink-3)">${t}${unit}</text>`).join('');
  const xl = labels.map((l, i) => (l ? `<text x="${x(i)}" y="${h - 8}" text-anchor="middle" font-size="11" fill="var(--ink-3)">${l}</text>` : '')).join('');
  const tgt = target === null ? '' : `<line x1="${pl}" x2="${w - pr}" y1="${y(target)}" y2="${y(target)}" stroke="var(--m-late)" stroke-width="1.5" stroke-dasharray="2 3"/><text x="${pl + 6}" y="${y(target) - 6}" font-size="11" font-weight="600" fill="var(--late)">${targetLabel}</text>`;
  const lines = series.map((s) => {
    const pts = s.values.map((v, i) => `${x(i)},${y(v)}`).join(' ');
    const last = s.values.length - 1;
    return `<polyline points="${pts}" fill="none" stroke="var(--ink)" stroke-width="${s.width || 2}" stroke-dasharray="${s.dash || ''}" stroke-linejoin="round" opacity="${s.opacity || 1}"/>
      <circle cx="${x(last)}" cy="${y(s.values[last])}" r="3.2" fill="var(--surface)" stroke="var(--ink)" stroke-width="2"/>
      <text x="${x(last) + 9}" y="${y(s.values[last]) + (s.nudge || 0) + 4}" font-size="11.5" font-weight="600" fill="var(--ink)">${s.label} ${s.values[last]}${unit}</text>`;
  }).join('');
  return `<svg viewBox="0 0 ${w} ${h}" width="100%" role="img" aria-label="${series.map((s) => s.label).join(', ')}">${grid}${tgt}${lines}${xl}</svg>`;
}

/** Messages by weekday and hour. Volume is magnitude, so it is one ink ramp, light to dark. */
function heatmap({ rows, cols, data, w = 760, cell = 26 }) {
  const pl = 44, pt = 20;
  const max = Math.max(...data.flat());
  const cw = (w - pl) / cols.length;
  const cells = data.map((r, ri) => r.map((v, ci) => `<rect x="${pl + ci * cw + 1}" y="${pt + ri * cell + 1}" width="${cw - 2}" height="${cell - 2}" rx="3" fill="var(--ink)" opacity="${(0.05 + 0.85 * (v / max)).toFixed(2)}"><title>${rows[ri]} ${cols[ci]}: ${v} messages</title></rect>`).join('')).join('');
  const rl = rows.map((r, i) => `<text x="${pl - 8}" y="${pt + i * cell + cell / 2 + 4}" text-anchor="end" font-size="11" fill="var(--ink-3)">${r}</text>`).join('');
  const cl = cols.map((c, i) => (i % 2 === 0 ? `<text x="${pl + i * cw + cw / 2}" y="12" text-anchor="middle" font-size="11" fill="var(--ink-3)">${c}</text>` : '')).join('');
  return `<svg viewBox="0 0 ${w} ${pt + rows.length * cell + 4}" width="100%" role="img" aria-label="Messages by day and hour">${cl}${rl}${cells}</svg>`;
}

/** Deterministic pseudo-data, so every board shows the same invented week. */
function seeded(n, seed = 7) { let s = seed; return Array.from({ length: n }, () => ((s = (s * 9301 + 49297) % 233280) / 233280)); }

const phaseChip = (p) => `<span class="phase">${p}</span>`;
