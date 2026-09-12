// The charts from the Front Desk renders. House rules: one axis, a recessive grid, the target always drawn and
// labelled, and colour only for lateness. Series that are not about lateness are told apart by line style and
// a label at their end, never by hue.
import type { QueueRow, Tone } from '../app/view-model.ts';

// ---- the line ----------------------------------------------------------------------------------------

const OVER = 94; // percent of the track that 0–60 minutes spans; the rest is the 60+ bin

const trackX = (minutes: number) => (minutes > 60 ? OVER + (100 - OVER) / 2 : (minutes / 60) * OVER);
const initials = (name: string) => name.split(/\s+/).map((w) => w[0] ?? '').join('').slice(0, 2).toUpperCase();

/**
 * Everyone waiting, placed by how long they have waited, one lane per location, with the target through it.
 * Tokens that would overlap step down to a second row, so two people a minute apart are both visible.
 */
export function TheLine({ rows, locations, target, selected, onSelect }: {
  rows: QueueRow[]; locations: string[]; target: number; selected?: string | null; onSelect?: (row: QueueRow) => void;
}) {
  const ticks = [0, 5, 10, 15, 20, 30, 40, 50, 60];
  return (
    <section className="line" aria-label="The line">
      <div className="line-top"><strong>The line</strong><span>minutes waited, counted in opening hours only</span>
        <div className="legend">
          <span><i style={{ borderColor: 'var(--m-ok)' }} />On time</span>
          <span><i style={{ borderColor: 'var(--m-due)' }} />Due within 5 min</span>
          <span><i style={{ borderColor: 'var(--m-late)', background: 'var(--m-late)' }} />Past target</span>
        </div>
      </div>
      <div className="lanes">
        <div style={{ position: 'absolute', left: 150, right: 0, top: 26, bottom: 24, pointerEvents: 'none' }}>
          <div className="target" style={{ left: `${trackX(target)}%`, top: 0 }}><span style={{ top: -22 }}>Target {target} min</span></div>
        </div>
        <div style={{ gridColumn: '1 / 3', height: 26 }} />
        {locations.map((location) => {
          const here = rows.filter((r) => (r.location || 'No location') === location).sort((a, b) => a.waited - b.waited);
          const last = [-Infinity, -Infinity];
          const late = here.filter((r) => r.tone === 'late').length;
          return (
            <Lane key={location} label={location} note={here.length ? `${here.length} waiting${late ? `, ${late} late` : ''}` : 'nobody waiting'} target={target}>
              {here.map((r) => {
                const x = trackX(r.waited);
                let slot = last.findIndex((prev) => x - prev >= 2.8);
                if (slot < 0) slot = last[0] <= last[1] ? 0 : 1;
                last[slot] = x;
                const key = `${r.accountId}:${r.customer}`;
                return (
                  <button key={key} className={`tok ${r.tone} ${selected === key ? 'sel' : ''}`}
                    style={{ left: `${x}%`, top: here.length > 1 ? (slot === 0 ? 5 : 33) : 19 }}
                    title={`${r.customer}, ${r.waited} min`} aria-label={`${r.customer}, waiting ${r.waited} minutes`}
                    onClick={() => onSelect?.(r)}>
                    {initials(r.customer)}
                  </button>
                );
              })}
            </Lane>
          );
        })}
        <div className="axis">
          {ticks.map((m) => <span key={m} style={{ left: `${trackX(m)}%` }}>{m === 0 ? '0 min' : m}</span>)}
          <span style={{ left: `${trackX(61)}%` }}>60+</span>
        </div>
      </div>
    </section>
  );
}

function Lane({ label, note, target, children }: { label: string; note: string; target: number; children: React.ReactNode }) {
  const dueFrom = trackX(Math.max(0, target - 5));
  return (
    <>
      <div className="lane-label"><b>{label}</b><span>{note}</span></div>
      <div className="lane">
        <div className="zone-due" style={{ left: `${dueFrom}%`, width: `${trackX(target) - dueFrom}%` }} />
        <div className="zone-late" style={{ left: `${trackX(target)}%`, width: `${OVER - trackX(target)}%` }} />
        <div className="zone-over" style={{ left: `${OVER}%`, right: 0 }} />
        {children}
      </div>
    </>
  );
}

// ---- small multiples ------------------------------------------------------------------------------------

/** A small trend: ink line, soft area, the latest value emphasised. */
export function Spark({ values, width = 120, height = 30, min = 0, max }: { values: number[]; width?: number; height?: number; min?: number; max?: number }) {
  if (values.length < 2) return null;
  const top = max ?? Math.max(...values);
  const x = (i: number) => 2 + (i * (width - 4)) / (values.length - 1);
  const y = (v: number) => height - 3 - ((v - min) / (top - min || 1)) * (height - 6);
  const pts = values.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
  const end = values.length - 1;
  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} aria-hidden="true">
      <polygon points={`${x(0)},${height} ${pts} ${x(end)},${height}`} fill="var(--ink)" opacity={0.07} />
      <polyline points={pts} fill="none" stroke="var(--ink)" strokeWidth={1.6} strokeLinejoin="round" />
      <circle cx={x(end)} cy={y(values[end])} r={2.6} fill="var(--ink)" />
    </svg>
  );
}

export interface Series { label: string; values: number[]; dash?: string; width?: number; nudge?: number }

/** Lines over time against an optional goal. Series differ by dash and an end label, not colour. */
export function LineChart({ series, labels, min = 0, max = 100, ticks, unit = '', target, targetLabel = '', width = 700, height = 250 }: {
  series: Series[]; labels: string[]; min?: number; max?: number; ticks: number[]; unit?: string; target?: number; targetLabel?: string; width?: number; height?: number;
}) {
  const pl = 38, pr = 110, pt = 14, pb = 28;
  const x = (i: number) => pl + (i * (width - pl - pr)) / (labels.length - 1);
  const y = (v: number) => pt + (height - pt - pb) * (1 - (v - min) / (max - min));
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width="100%" role="img" aria-label={series.map((s) => s.label).join(', ')}>
      {ticks.map((t) => (
        <g key={t}>
          <line x1={pl} x2={width - pr} y1={y(t)} y2={y(t)} stroke="var(--line)" />
          <text x={pl - 8} y={y(t) + 4} textAnchor="end" fontSize={11} fill="var(--ink-3)">{t}{unit}</text>
        </g>
      ))}
      {target !== undefined && (
        <g>
          <line x1={pl} x2={width - pr} y1={y(target)} y2={y(target)} stroke="var(--m-late)" strokeWidth={1.5} strokeDasharray="2 3" />
          <text x={pl + 6} y={y(target) - 6} fontSize={11} fontWeight={600} fill="var(--late)">{targetLabel}</text>
        </g>
      )}
      {series.map((s) => {
        const end = s.values.length - 1;
        return (
          <g key={s.label}>
            <polyline points={s.values.map((v, i) => `${x(i)},${y(v)}`).join(' ')} fill="none" stroke="var(--ink)" strokeWidth={s.width ?? 2} strokeDasharray={s.dash} strokeLinejoin="round" />
            <circle cx={x(end)} cy={y(s.values[end])} r={3.2} fill="var(--surface)" stroke="var(--ink)" strokeWidth={2} />
            <text x={x(end) + 9} y={y(s.values[end]) + (s.nudge ?? 0) + 4} fontSize={11.5} fontWeight={600} fill="var(--ink)">{s.label} {s.values[end]}{unit}</text>
          </g>
        );
      })}
      {labels.map((l, i) => l && <text key={i} x={x(i)} y={height - 8} textAnchor="middle" fontSize={11} fill="var(--ink-3)">{l}</text>)}
    </svg>
  );
}

/** Volume by day and hour. Volume is magnitude, so it is one ink ramp, light to dark. */
export function Heatmap({ rows, cols, data, width = 600, cell = 30 }: { rows: string[]; cols: string[]; data: number[][]; width?: number; cell?: number }) {
  const pl = 44, pt = 20;
  const top = Math.max(...data.flat());
  const cw = (width - pl) / cols.length;
  return (
    <svg viewBox={`0 0 ${width} ${pt + rows.length * cell + 4}`} width="100%" role="img" aria-label="Messages by day and hour">
      {cols.map((c, i) => i % 2 === 0 && <text key={c} x={pl + i * cw + cw / 2} y={12} textAnchor="middle" fontSize={11} fill="var(--ink-3)">{c}</text>)}
      {rows.map((r, ri) => (
        <g key={r}>
          <text x={pl - 8} y={pt + ri * cell + cell / 2 + 4} textAnchor="end" fontSize={11} fill="var(--ink-3)">{r}</text>
          {data[ri].map((v, ci) => (
            <rect key={ci} x={pl + ci * cw + 1} y={pt + ri * cell + 1} width={cw - 2} height={cell - 2} rx={3} fill="var(--ink)" opacity={0.05 + 0.85 * (v / top)}>
              <title>{`${r} ${cols[ci]}: ${v} messages`}</title>
            </rect>
          ))}
        </g>
      ))}
    </svg>
  );
}

const barPath = (x: number, y: number, w: number, base: number) => `M${x},${base} V${y + 4} q0,-4 4,-4 h${w - 8} q4,0 4,4 V${base} z`;

/** Counts in bands of time, with the target drawn between the bands it separates. */
export function Histogram({ buckets, targetIndex, targetLabel, max, width = 640, height = 230 }: {
  buckets: readonly (readonly [string, number])[]; targetIndex: number; targetLabel: string; max: number; width?: number; height?: number;
}) {
  const pl = 36, pb = 30, pt = 18;
  const bw = (width - pl) / buckets.length;
  const y = (v: number) => pt + (height - pt - pb) * (1 - v / max);
  const gridTicks = [0, max / 3, (2 * max) / 3, max].map(Math.round);
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width="100%" role="img" aria-label="Replies by how long they took">
      {gridTicks.map((v) => <g key={v}><line x1={pl} x2={width} y1={y(v)} y2={y(v)} stroke="var(--line)" /><text x={pl - 8} y={y(v) + 4} textAnchor="end" fontSize={11} fill="var(--ink-3)">{v}</text></g>)}
      {buckets.map(([label, v], i) => {
        const late = i >= targetIndex;
        const x = pl + i * bw + 6;
        return (
          <g key={label}>
            <path d={barPath(x, y(v), bw - 12, y(0))} fill={late ? 'var(--m-late)' : 'var(--m-ok)'} />
            <text x={x + (bw - 12) / 2} y={y(v) - 6} textAnchor="middle" fontSize={12} fontWeight={600} fill={late ? 'var(--late)' : 'var(--ink)'}>{v}</text>
            <text x={x + (bw - 12) / 2} y={height - 10} textAnchor="middle" fontSize={11.5} fill="var(--ink-2)">{label}</text>
          </g>
        );
      })}
      <line x1={pl + targetIndex * bw} x2={pl + targetIndex * bw} y1={pt - 6} y2={y(0)} stroke="var(--ink)" strokeWidth={2} />
      <text x={pl + targetIndex * bw + 6} y={pt + 4} fontSize={11.5} fontWeight={600} fill="var(--ink)">{targetLabel}</text>
    </svg>
  );
}

export interface DayPoint { label: string; median: number; count: number }

/** Median first reply per day against the target. A day with no replies is a gap, not a zero. */
export function DayBars({ data, target, width = 700, height = 250 }: { data: DayPoint[]; target: number; width?: number; height?: number }) {
  const pl = 36, pb = 28, pt = 16;
  const max = Math.max(target * 1.6, ...data.map((d) => d.median)) || 1;
  const step = Math.max(5, Math.ceil(max / 5 / 5) * 5);
  const top = step * 5;
  const y = (v: number) => pt + (height - pt - pb) * (1 - v / top);
  const bw = Math.min(54, (width - pl) / data.length - 16);
  const gap = (width - pl - data.length * bw) / data.length;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width="100%" role="img" aria-label={`Median first reply per day against the ${target} minute target`}>
      {[0, 1, 2, 3, 4, 5].map((k) => <g key={k}><line x1={pl} x2={width} y1={y(k * step)} y2={y(k * step)} stroke="var(--line)" /><text x={pl - 8} y={y(k * step) + 4} textAnchor="end" fontSize={11} fill="var(--ink-3)">{k * step}</text></g>)}
      {data.map((d, i) => {
        const x = pl + gap / 2 + i * (bw + gap);
        const over = d.median > target;
        return (
          <g key={d.label + i}>
            {d.count > 0 && <path d={barPath(x, y(d.median), bw, y(0))} fill={over ? 'var(--m-late)' : 'var(--m-ok)'} />}
            {d.count > 0 && <text x={x + bw / 2} y={y(d.median) - 7} textAnchor="middle" fontSize={12} fontWeight={600} fill={over ? 'var(--late)' : 'var(--ink)'}>{d.median}</text>}
            <text x={x + bw / 2} y={height - 8} textAnchor="middle" fontSize={12} fill="var(--ink-2)">{d.label}</text>
          </g>
        );
      })}
      <line x1={pl} x2={width} y1={y(target)} y2={y(target)} stroke="var(--ink)" strokeWidth={2} />
      <text x={width - 4} y={y(target) - 7} textAnchor="end" fontSize={11.5} fontWeight={600} fill="var(--ink)">Target {target} min</text>
    </svg>
  );
}

export const toneInk = (tone: Tone | 'neutral') => (tone === 'neutral' ? 'var(--ink-3)' : `var(--${tone})`);
