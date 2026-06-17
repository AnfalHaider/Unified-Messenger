using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Computes elapsed minutes that fall inside a location's business hours, so the SLA clock pauses
/// outside working hours (a message at 2am isn't "late" by 9am). Pure and deterministic given a
/// fixed time zone; hours are interpreted in local (oversight-machine) time. When hours are disabled
/// or misconfigured it falls back to raw elapsed minutes, keeping behaviour identical to before.
/// </summary>
public static class BusinessHoursCalculator
{
    public static double ElapsedBusinessMinutes(DateTimeOffset startUtc, DateTimeOffset endUtc, BusinessHours? hours)
    {
        var rawMinutes = Math.Max(0, (endUtc - startUtc).TotalMinutes);

        if (hours is null || !hours.Enabled || endUtc <= startUtc)
        {
            return rawMinutes;
        }

        var open = Math.Clamp(hours.OpenMinutes, 0, 24 * 60);
        var close = Math.Clamp(hours.CloseMinutes, 0, 24 * 60);
        if (close <= open)
        {
            return rawMinutes;
        }

        var workingDays = (hours.WorkingDays is { Count: > 0 }) ? hours.WorkingDays : [1, 2, 3, 4, 5, 6];

        var startLocal = startUtc.ToLocalTime().DateTime;
        var endLocal = endUtc.ToLocalTime().DateTime;

        double total = 0;
        var day = startLocal.Date;
        var guard = 0;
        while (day <= endLocal.Date && guard++ < 800)
        {
            if (workingDays.Contains((int)day.DayOfWeek))
            {
                var windowOpen = day.AddMinutes(open);
                var windowClose = day.AddMinutes(close);
                var segStart = startLocal > windowOpen ? startLocal : windowOpen;
                var segEnd = endLocal < windowClose ? endLocal : windowClose;
                if (segEnd > segStart)
                {
                    total += (segEnd - segStart).TotalMinutes;
                }
            }

            day = day.AddDays(1);
        }

        return total;
    }
}
