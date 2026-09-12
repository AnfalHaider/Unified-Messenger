// The charts from the approved designs, as components. House rules, unchanged from the design work: one axis,
// thin marks, rounded data ends anchored to the baseline, a recessive grid, and the target always drawn and
// labelled — so a breach is never carried by colour alone. A single series carries no legend; the heading
// names it.
import type { Tone } from '../app/view-model.ts';

/** A bar with square feet and rounded shoulders, so the data end reads and the baseline stays flat. */
function barPath(x: number, y: number, w: number, h: number, r = 4) {
  const rr = Math.max(0, Math.min(r, h, w / 2));
  return `M${x},${y + h}V${y + rr}a${rr},${rr} 0 0 1 ${rr},-${rr}h${(w - 2 * rr).toFixed(1)}a${rr},${rr} 0 0 1 ${rr},${rr}V${y + h}Z`;
}

export interface DayPoint { label: string; median: number; count: number }

/** Median first reply per day against the target. Days with no replies are drawn as a gap, not as zero. */
export function DayBars({ data, target, height = 210 }: { data: DayPoint[]; target: number; height?: number }) {
  const w = 640, padL = 34, padB = 26, padT = 22;
  if (!data.length) return null;
  const top = Math.max(target * 1.4, ...data.map((d) => d.median)) * 1.15 || 1;
  const plot = height - padB - padT;
  const step = (w - padL) / data.length;
  const bw = Math.min(44, step - 14);
  const y = (v: number) => padT + plot - (v / top) * plot;

  return (
    <svg viewBox={`0 0 ${w} ${height}`} width="100%" height={height} role="img"
      aria-label={`Median first reply per day against a ${target} minute target`}>
      <line x1={padL} y1={y(top * 0.75)} x2={w} y2={y(top * 0.75)} stroke="var(--grid)" />
      <line x1={padL} y1={y(target)} x2={w} y2={y(target)} stroke="var(--mark-late)" strokeDasharray="4 4" />
      <text x={padL - 6} y={y(target) + 4} textAnchor="end" fontSize="11" fontFamily="IBM Plex Mono, monospace" fill="var(--late)">{target}</text>
      <text x={padL - 6} y={y(0) + 4} textAnchor="end" fontSize="11" fontFamily="IBM Plex Mono, monospace" fill="var(--ink-3)">0</text>

      {data.map((d, i) => {
        const x = padL + i * step + (step - bw) / 2;
        const late = d.median > target;
        if (!d.count) {
          return (
            <text key={d.label + i} x={x + bw / 2} y={height - 7} textAnchor="middle" fontSize="11" fill="var(--ink-3)">{d.label}</text>
          );
        }
        return (
          <g key={d.label + i}>
            <path d={barPath(x, y(d.median), bw, y(0) - y(d.median))} fill={late ? 'var(--mark-late)' : 'var(--mark-flat)'} />
            <text x={x + bw / 2} y={y(d.median) - 8} textAnchor="middle" fontSize="11.5" fontFamily="IBM Plex Mono, monospace" fontWeight="600" fill={late ? 'var(--late)' : 'var(--ink-2)'}>{d.median}</text>
            <text x={x + bw / 2} y={height - 7} textAnchor="middle" fontSize="11" fill="var(--ink-3)">{d.label}</text>
          </g>
        );
      })}
      <line x1={padL} y1={y(0)} x2={w} y2={y(0)} stroke="var(--axis)" />
    </svg>
  );
}

/** A wait drawn against its target: fill to `fill`, with a notch where the target sits. */
export function Meter({ fill, tone, target }: { fill: number; tone: Tone; target: number }) {
  return (
    <span className="meter">
      <i style={{ width: `${Math.min(100, fill)}%`, background: `var(--mark-${tone === 'neutral' ? 'flat' : tone})` }} />
      <u style={{ left: `${target}%` }} />
    </span>
  );
}
