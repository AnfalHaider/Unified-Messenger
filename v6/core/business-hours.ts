// Port of Services/Oversight/BusinessHoursCalculator.cs, extended for v6.
// Minutes between two moments that fall inside a location's opening hours, so the reply clock pauses
// overnight (a message at 2 am isn't "late" by 9 am). Falls back to raw minutes when hours are off or invalid.
//
// v6 adds two things v5 did not have: each weekday can have its own hours (`week`), and whole days can be closed
// (`closedDates`, filled in from the holidays that apply to the location). Hours written by v5, one window for
// every working day, still work unchanged.
import { dayKey } from './days.ts';

export interface DayHours { open: number; close: number }

export interface BusinessHours {
  enabled: boolean;
  openMinutes: number; // minutes after local midnight, used when `week` is absent
  closeMinutes: number;
  workingDays?: number[]; // 0 = Sunday … 6 = Saturday; empty or missing means Monday–Saturday
  /** Seven entries, 0 = Sunday. A day's own window in minutes after midnight, or null when closed all day. */
  week?: (DayHours | null)[];
  /** Local dates ("YYYY-MM-DD") the location is closed all day. */
  closedDates?: string[];
}

const MINUTE = 60_000;

/** The day's opening window in minutes after midnight, or null when closed that day. */
export function windowFor(hours: BusinessHours, date: Date): DayHours | null {
  if (hours.closedDates?.includes(dayKey(date.getTime()))) return null;
  if (hours.week?.length === 7) {
    const day = hours.week[date.getDay()];
    return day && day.close > day.open ? day : null;
  }
  const open = Math.min(Math.max(hours.openMinutes, 0), 1440);
  const close = Math.min(Math.max(hours.closeMinutes, 0), 1440);
  const days = hours.workingDays?.length ? hours.workingDays : [1, 2, 3, 4, 5, 6];
  return close > open && days.includes(date.getDay()) ? { open, close } : null;
}

/** Hours that can never open (every day closed, or a window that ends before it starts) count as switched off,
 *  as v5 did: a clock that never runs would hide every customer. Closed dates do not count here. */
const usable = (hours: BusinessHours | null | undefined): hours is BusinessHours => {
  if (!hours?.enabled) return false;
  if (hours.week?.length === 7) return hours.week.some((d) => d && d.close > d.open);
  return Math.min(hours.closeMinutes, 1440) > Math.max(hours.openMinutes, 0);
};

/** Whether the location is open at this moment. Hours that are off or unusable mean always open, as below. */
export function isOpen(hours: BusinessHours | null | undefined, now: number): boolean {
  if (!usable(hours)) return true;
  const d = new Date(now);
  const window = windowFor(hours, d);
  const minute = d.getHours() * 60 + d.getMinutes();
  return !!window && minute >= window.open && minute < window.close;
}

// ponytail: reads the machine's local time zone, as v5 does. Add a timeZone parameter when a test needs
// a western offset or a DST transition (see the AGENTS.md date-helper gotcha).
export function elapsedBusinessMinutes(start: Date, end: Date, hours?: BusinessHours | null): number {
  const raw = Math.max(0, (end.getTime() - start.getTime()) / MINUTE);
  if (!usable(hours) || end <= start) return raw;

  let total = 0;
  const day = new Date(start.getFullYear(), start.getMonth(), start.getDate());
  for (let guard = 0; day <= end && guard < 800; guard++, day.setDate(day.getDate() + 1)) {
    const window = windowFor(hours, day);
    if (!window) continue;
    const windowOpen = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 0, window.open);
    const windowClose = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 0, window.close);
    const from = Math.max(start.getTime(), windowOpen.getTime());
    const to = Math.min(end.getTime(), windowClose.getTime());
    if (to > from) total += (to - from) / MINUTE;
  }
  return total;
}

/** When the location last closed: the end of the most recent opening window that is already over. Null when the
 *  hours are off or unusable, or nothing closed in the last two weeks. Used to split "owed from before closing"
 *  from "wrote while closed". */
export function lastClosing(hours: BusinessHours | null | undefined, now: number): number | null {
  if (!usable(hours)) return null;
  const today = new Date(now);
  for (let back = 0; back <= 14; back++) {
    const day = new Date(today.getFullYear(), today.getMonth(), today.getDate() - back);
    const window = windowFor(hours, day);
    if (!window) continue;
    const close = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 0, window.close).getTime();
    if (close <= now) return close;
  }
  return null;
}
