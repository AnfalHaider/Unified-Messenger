// Port of ResponseTimeTracker.cs, minus the file handling: the app layer saves the state object as JSON.
// First response time, measured going forward. A web client keeps only current state, so when a chat is seen
// waiting its inbound time is remembered, and when it is next seen answered by us the gap is one sample.
// Early data is sparse, which is honest. All times are epoch milliseconds.
import { startOfDay } from './days.ts';
import { honestPercent } from './percent.ts';

const MINUTE = 60_000, DAY = 86_400_000;
const MAX_CREDIBLE_MS = 7 * DAY; // longer is an abandoned chat answered out of band; it would distort the median
const MAX_SAMPLES_PER_ACCOUNT = 1000;
const RETENTION_MS = 120 * DAY;

export interface Sample { answeredAt: number; minutes: number }

export interface ResponseTimes {
  /** account → chat → inbound time of the oldest unanswered customer message */
  pending: Record<string, Record<string, number>>;
  /** account → when it first came under observation. Earlier messages are backlog, never samples. */
  watchStart: Record<string, number>;
  samples: Record<string, Sample[]>;
}

export const emptyResponseTimes = (): ResponseTimes => ({ pending: {}, watchStart: {}, samples: {} });

/** Call for every chat on every read. `lastActivity` is the inbound time while waiting and the reply time once answered. */
export function observe(state: ResponseTimes, account: string, chat: string, awaiting: boolean, fromMe: boolean, lastActivity: number, now = Date.now()) {
  const id = account.trim();
  if (!id || !chat.trim()) return;
  const pending = (state.pending[id] ??= {});
  // Backlog from before we watched may already have been handled on the phone; counting it would make the
  // first read report a huge response time.
  const watchStart = (state.watchStart[id] ??= now);

  if (fromMe) {
    const inbound = pending[chat];
    if (inbound === undefined) return;
    delete pending[chat];
    const gap = lastActivity - inbound;
    if (inbound >= watchStart && gap > 0 && gap <= MAX_CREDIBLE_MS) {
      const list = (state.samples[id] ??= []);
      list.push({ answeredAt: lastActivity, minutes: gap / MINUTE });
      if (list.length > MAX_SAMPLES_PER_ACCOUNT) list.splice(0, list.length - MAX_SAMPLES_PER_ACCOUNT);
    }
    return;
  }
  // Keep the earliest unanswered inbound. Not waiting and not confirmed answered is an ambiguous read: leave it.
  if (awaiting && lastActivity >= watchStart && pending[chat] === undefined) pending[chat] = lastActivity;
}

export interface ResponseStats {
  hasData: boolean; sampleCount: number; medianMinutes: number; averageMinutes: number; p90Minutes: number;
  slaPercent: number; answeredToday: number;
}

/** SLA is judged against the current setting, not one baked in when the sample was taken. */
export function responseStats(state: ResponseTimes, accounts: string[], slaMinutes: number,
  { from = null, to = null, now = Date.now() }: { from?: number | null; to?: number | null; now?: number } = {}): ResponseStats {
  const today = startOfDay(now);
  const values: number[] = [];
  let within = 0, answeredToday = 0;
  for (const s of samplesFor(state, accounts)) {
    // Counted before the range filter: "answered today" is about today, not today-inside-the-selected-range.
    if (startOfDay(s.answeredAt) === today) answeredToday++;
    if ((from !== null && s.answeredAt < from) || (to !== null && s.answeredAt > to)) continue;
    values.push(s.minutes);
    if (slaMinutes <= 0 || s.minutes <= slaMinutes) within++;
  }
  if (!values.length) return { hasData: false, sampleCount: 0, medianMinutes: 0, averageMinutes: 0, p90Minutes: 0, slaPercent: 0, answeredToday };
  values.sort((a, b) => a - b);
  return {
    hasData: true, sampleCount: values.length, medianMinutes: percentile(values, 0.5),
    averageMinutes: values.reduce((a, b) => a + b, 0) / values.length, p90Minutes: percentile(values, 0.9),
    slaPercent: honestPercent(within, values.length), answeredToday,
  };
}

export interface DailyResponse { label: string; count: number; medianMinutes: number; percentWithin: number }

/** One point per local day, oldest first. Empty days are kept with count 0 so a chart's axis is continuous,
 *  and the count lets it dim a day with no replies rather than read its 0% as a miss. */
export function dailyResponse(state: ResponseTimes, accounts: string[], thresholdMinutes: number,
  { days = 7, now = Date.now() }: { days?: number; now?: number } = {}): DailyResponse[] {
  const byDay = new Map<number, number[]>();
  for (const s of samplesFor(state, accounts)) {
    const day = startOfDay(s.answeredAt);
    byDay.set(day, byDay.get(day) ?? []).get(day)!.push(s.minutes);
  }
  const points: DailyResponse[] = [];
  for (let back = days - 1; back >= 0; back--) {
    const day = startOfDay(now, back);
    const values = (byDay.get(day) ?? []).sort((a, b) => a - b);
    const within = values.filter((m) => thresholdMinutes <= 0 || m <= thresholdMinutes).length;
    points.push({
      label: new Date(day).toLocaleDateString('en-US', { weekday: 'short' }), count: values.length,
      medianMinutes: percentile(values, 0.5), percentWithin: values.length ? honestPercent(within, values.length) : 0,
    });
  }
  return points;
}

/** Drops samples past retention. Call after loading and before saving. */
export function pruneResponseTimes(state: ResponseTimes, now = Date.now()) {
  for (const id of Object.keys(state.samples)) state.samples[id] = state.samples[id].filter((s) => s.answeredAt >= now - RETENTION_MS);
}

const samplesFor = (state: ResponseTimes, accounts: string[]) => accounts.flatMap((a) => state.samples[a.trim()] ?? []);

function percentile(sorted: number[], fraction: number): number {
  if (!sorted.length) return 0;
  return sorted[Math.min(sorted.length - 1, Math.max(0, Math.ceil(fraction * sorted.length) - 1))];
}
