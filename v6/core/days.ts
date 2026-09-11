// Port of LocalDayBoundary.cs. That file resolved a local midnight by hand because .NET pairs a date with
// whatever offset it is given. JavaScript's Date constructor already does it right (ECMA-262 local-time
// rules): a skipped midnight becomes the first instant that exists, a repeated midnight the first of the two.
// So a local day is new Date(y, m, d), and stepping back walks the calendar rather than 24-hour blocks,
// which keeps a 23- or 25-hour day one day long. All times are epoch milliseconds.

export const DAY_MS = 86_400_000;

/** First instant of the local day `daysBack` calendar days before `at`'s day. */
export function startOfDay(at: number, daysBack = 0): number {
  const d = new Date(at);
  return new Date(d.getFullYear(), d.getMonth(), d.getDate() - daysBack).getTime();
}

/** First instant of the next local day: the exclusive end of `at`'s day. */
export const endOfDayExclusive = (at: number) => startOfDay(at, -1);

/** Calendar days from `earlier`'s local day to `later`'s. Rounded, so a 23- or 25-hour day still counts as one. */
export const calendarDaysBetween = (earlier: number, later: number) =>
  Math.round((startOfDay(later) - startOfDay(earlier)) / DAY_MS);
