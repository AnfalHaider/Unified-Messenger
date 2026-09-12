// Port of Services/Oversight/BusinessHoursCalculator.cs.
// Minutes between two moments that fall inside a location's opening hours, so the reply clock pauses
// overnight (a message at 2 am isn't "late" by 9 am). Falls back to raw minutes when hours are off or invalid.

export interface BusinessHours {
  enabled: boolean;
  openMinutes: number; // minutes after local midnight
  closeMinutes: number;
  workingDays?: number[]; // 0 = Sunday … 6 = Saturday; empty or missing means Monday–Saturday
}

const MINUTE = 60_000;

// ponytail: reads the machine's local time zone, as v5 does. Add a timeZone parameter when a test needs
// a western offset or a DST transition (see the AGENTS.md date-helper gotcha).
/** Whether the location is open at this moment. Hours that are off or invalid mean always open, as above. */
export function isOpen(hours: BusinessHours | null | undefined, now: number): boolean {
  if (!hours?.enabled || hours.closeMinutes <= hours.openMinutes) return true;
  const d = new Date(now);
  const days = hours.workingDays?.length ? hours.workingDays : [1, 2, 3, 4, 5, 6];
  const minute = d.getHours() * 60 + d.getMinutes();
  return days.includes(d.getDay()) && minute >= hours.openMinutes && minute < hours.closeMinutes;
}

export function elapsedBusinessMinutes(start: Date, end: Date, hours?: BusinessHours | null): number {
  const raw = Math.max(0, (end.getTime() - start.getTime()) / MINUTE);
  if (!hours?.enabled || end <= start) return raw;

  const open = Math.min(Math.max(hours.openMinutes, 0), 1440);
  const close = Math.min(Math.max(hours.closeMinutes, 0), 1440);
  if (close <= open) return raw;

  const days = hours.workingDays?.length ? hours.workingDays : [1, 2, 3, 4, 5, 6];
  let total = 0;
  const day = new Date(start.getFullYear(), start.getMonth(), start.getDate());
  for (let guard = 0; day <= end && guard < 800; guard++, day.setDate(day.getDate() + 1)) {
    if (!days.includes(day.getDay())) continue;
    const windowOpen = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 0, open);
    const windowClose = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 0, close);
    const from = Math.max(start.getTime(), windowOpen.getTime());
    const to = Math.min(end.getTime(), windowClose.getTime());
    if (to > from) total += (to - from) / MINUTE;
  }
  return total;
}
