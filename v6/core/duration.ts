// How long something took or has lasted, said the way a person would. One rule for every screen, alert and
// timeline, so the line, the dock, reports and notifications never describe the same wait two ways.
//
//   under an hour     38 min
//   under a day       5 h, 2 h 5 min (minutes dropped from 10 h on: nobody needs "13 h 7 min")
//   under three days  1 day 22 h, 2 days
//   under two weeks   6 days
//   under two months  3 weeks
//   under a year      4 months
//   beyond            1 year, 2 years
const HOUR = 60, DAY = 24 * HOUR;

const plural = (n: number, one: string) => (n === 1 ? one : `${one}s`);

/** The number and its unit apart, for screens that draw the number large: ['1', 'day 22 h']. */
export function durationParts(minutes: number): [string, string] {
  const m = Math.max(0, Math.round(minutes));
  if (m < HOUR) return [String(m), 'min'];
  if (m < DAY) {
    const h = Math.floor(m / HOUR), rest = m % HOUR;
    return h >= 10 || rest === 0 ? [String(h), 'h'] : [String(h), `h ${rest} min`];
  }
  if (m < 3 * DAY) {
    const d = Math.floor(m / DAY), h = Math.floor((m % DAY) / HOUR);
    return [String(d), h ? `${plural(d, 'day')} ${h} h` : plural(d, 'day')];
  }
  const days = m / DAY;
  if (days < 14) { const d = Math.round(days); return [String(d), plural(d, 'day')]; }
  if (days < 60) { const w = Math.round(days / 7); return [String(w), plural(w, 'week')]; }
  if (days < 365) { const mo = Math.round(days / 30.44); return [String(mo), plural(mo, 'month')]; }
  const y = Math.round(days / 365.25);
  return [String(y), plural(y, 'year')];
}

/** The same, as one phrase: "1 day 22 h". */
export const durationText = (minutes: number) => durationParts(minutes).join(' ');
