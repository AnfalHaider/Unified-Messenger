// The Reports screen's figures for a range of whole local days ending today. Reply measures come from the measured
// first replies (response-times samples), judged against each account's current target, as the line does;
// traffic, reopened chats, missed calls and the morning backlog come from the day records (history).
//
// Nothing is guessed: a figure with nothing behind it is null, and the report says from when it has been recording.
import { dayKey, type History } from './history.ts';
import { startOfDay } from './days.ts';
import { honestPercent } from './percent.ts';
import type { ResponseTimes } from './response-times.ts';

export interface ReportAccount { id: string; name: string; location: string; targetMinutes: number }

export interface ReportTotals {
  customersWrote: number;
  replies: number;
  withinTarget: number;
  medianMinutes: number | null;
  onTimePercent: number | null;
  reopened: number;
  missedCalls: number;
  /** Customers waiting over a day at the latest recorded morning in the range. */
  waitingOverADay: number | null;
}

export interface ReportDay {
  day: string;
  customersWrote: number;
  replies: number;
  onTimePercent: number | null;
  waitingOverADay: number | null;
  reopened: number;
  missedCalls: number;
}

export interface Report {
  /** True when anything at all was recorded or measured in the range. */
  hasData: boolean;
  /** When the earliest of these accounts began recording, and how many of the range's days that covers. */
  recordingSince: number | null;
  daysRecorded: number;
  totals: ReportTotals;
  /** The same figures for the equal range just before this one. */
  previous: ReportTotals;
  days: ReportDay[];
  byLocation: { name: string; replies: number; onTimePercent: number | null; daily: (number | null)[]; missedCalls: number; missedDaily: number[] }[];
  byAccount: { id: string; name: string; replies: number; medianMinutes: number | null; onTimePercent: number | null; p90Minutes: number | null }[];
  /** First replies by how long they took; a band holds replies up to and including its upper minute. */
  bands: [string, number][];
  /** Customers who wrote per hour, averaged per weekday over the recorded days. Monday first, 24 hours each. */
  busy: number[][];
}

export const REPLY_BANDS: readonly [string, number][] = [
  ['0–5', 5], ['5–10', 10], ['10–15', 15], ['15–30', 30], ['30–60', 60], ['1–2 h', 120], ['2 h +', Infinity],
];

const NO_LOCATION = 'No location';

const percentile = (sorted: number[], fraction: number) =>
  sorted.length ? sorted[Math.min(sorted.length - 1, Math.max(0, Math.ceil(fraction * sorted.length) - 1))] : null;

export function buildReport(history: History, times: ResponseTimes, accounts: ReportAccount[], days: number, now: number): Report {
  const span = Math.max(1, Math.round(days));
  const keys = Array.from({ length: span }, (_, i) => dayKey(startOfDay(now, span - 1 - i)));
  const previousKeys = Array.from({ length: span }, (_, i) => dayKey(startOfDay(now, 2 * span - 1 - i)));

  /** Every measured reply of these accounts, with its day and whether it met that account's target. */
  const replies = accounts.flatMap((a) => (times.samples[a.id] ?? []).map((s) => ({
    account: a, day: dayKey(s.answeredAt), minutes: s.minutes, within: s.minutes <= a.targetMinutes,
  })));
  const records = accounts.flatMap((a) => (history[a.id]?.days ?? []).map((d) => ({ account: a, record: d })));

  const replyFigures = (list: typeof replies) => {
    const sorted = list.map((r) => r.minutes).sort((x, y) => x - y);
    const within = list.filter((r) => r.within).length;
    return { replies: list.length, withinTarget: within, medianMinutes: percentile(sorted, 0.5), p90Minutes: percentile(sorted, 0.9), onTimePercent: list.length ? honestPercent(within, list.length) : null };
  };
  const backlogOn = (key: string) => {
    const mornings = records.filter((r) => r.record.day === key && r.record.waitingOverADayAtFirstRead !== null);
    return mornings.length ? mornings.reduce((n, r) => n + (r.record.waitingOverADayAtFirstRead ?? 0), 0) : null;
  };
  const totalsFor = (range: string[]): ReportTotals => {
    const inRange = new Set(range);
    const recs = records.filter((r) => inRange.has(r.record.day)).map((r) => r.record);
    const { replies: count, withinTarget, medianMinutes, onTimePercent } = replyFigures(replies.filter((r) => inRange.has(r.day)));
    const latestMorning = [...range].reverse().map(backlogOn).find((v) => v !== null) ?? null;
    return {
      customersWrote: recs.reduce((n, d) => n + d.customersWrote, 0), replies: count, withinTarget, medianMinutes, onTimePercent,
      reopened: recs.reduce((n, d) => n + d.reopened, 0), missedCalls: recs.reduce((n, d) => n + d.missedCalls, 0),
      waitingOverADay: latestMorning,
    };
  };

  const locations = [...new Set(accounts.map((a) => a.location || NO_LOCATION))];
  const inRange = new Set(keys);
  const rangeReplies = replies.filter((r) => inRange.has(r.day));

  // Busy hours: sum each recorded day's hours onto its weekday, then divide by how many of that weekday were recorded.
  const busy = Array.from({ length: 7 }, () => Array(24).fill(0) as number[]);
  const weekdayDays = Array(7).fill(0) as number[];
  for (const key of keys) {
    const recs = records.filter((r) => r.record.day === key);
    if (!recs.length) continue;
    const [y, m, d] = key.split('-').map(Number);
    const weekday = (new Date(y, m - 1, d).getDay() + 6) % 7;
    weekdayDays[weekday]++;
    for (const { record } of recs) record.wroteByHour?.forEach((n, h) => { busy[weekday][h] += n; });
  }
  busy.forEach((row, w) => { if (weekdayDays[w]) row.forEach((n, h) => { row[h] = Math.round((n / weekdayDays[w]) * 10) / 10; }); });

  const starts = accounts.map((a) => history[a.id]?.watchStart).filter((t): t is number => typeof t === 'number');
  const recordingSince = starts.length ? Math.min(...starts) : null;

  return {
    hasData: rangeReplies.length > 0 || records.some((r) => inRange.has(r.record.day)),
    recordingSince,
    daysRecorded: recordingSince === null ? 0 : keys.filter((k) => k >= dayKey(recordingSince)).length,
    totals: totalsFor(keys),
    previous: totalsFor(previousKeys),
    days: keys.map((key) => {
      const recs = records.filter((r) => r.record.day === key).map((r) => r.record);
      const figures = replyFigures(rangeReplies.filter((r) => r.day === key));
      return {
        day: key, customersWrote: recs.reduce((n, d) => n + d.customersWrote, 0), replies: figures.replies,
        onTimePercent: figures.onTimePercent, waitingOverADay: backlogOn(key),
        reopened: recs.reduce((n, d) => n + d.reopened, 0), missedCalls: recs.reduce((n, d) => n + d.missedCalls, 0),
      };
    }),
    byLocation: locations.map((name) => {
      const here = rangeReplies.filter((r) => (r.account.location || NO_LOCATION) === name);
      const { replies: count, onTimePercent } = replyFigures(here);
      const missedDaily = keys.map((key) => records
        .filter((r) => r.record.day === key && (r.account.location || NO_LOCATION) === name)
        .reduce((n, r) => n + r.record.missedCalls, 0));
      return {
        name, replies: count, onTimePercent, daily: keys.map((key) => replyFigures(here.filter((r) => r.day === key)).onTimePercent),
        missedCalls: missedDaily.reduce((a, b) => a + b, 0), missedDaily,
      };
    }),
    byAccount: accounts
      .map((a) => ({ id: a.id, name: a.name, ...replyFigures(rangeReplies.filter((r) => r.account.id === a.id)) }))
      .map(({ withinTarget: _w, ...rest }) => rest)
      .sort((x, y) => y.replies - x.replies),
    bands: REPLY_BANDS.map(([label, upper], i) => [label, rangeReplies.filter((r) => r.minutes <= upper && (i === 0 || r.minutes > REPLY_BANDS[i - 1][1])).length]),
    busy,
  };
}
