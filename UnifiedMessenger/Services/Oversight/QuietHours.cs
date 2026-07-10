using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>Decides whether a given local time falls inside the user's quiet hours (toast suppression).</summary>
public static class QuietHours
{
    /// <summary>True when quiet hours are on and <paramref name="localHour"/> is inside the window,
    /// handling windows that wrap past midnight (e.g. 21 → 8).</summary>
    public static bool IsQuiet(AppSettings settings, int localHour)
    {
        if (settings is null || !settings.QuietHoursEnabled)
        {
            return false;
        }

        var start = Clamp(settings.QuietHoursStartHour);
        var end = Clamp(settings.QuietHoursEndHour);
        if (start == end)
        {
            return false; // zero-length window
        }

        return start < end
            ? localHour >= start && localHour < end          // same-day window
            : localHour >= start || localHour < end;          // wraps past midnight
    }

    public static bool IsQuietNow(AppSettings settings) => IsQuiet(settings, DateTime.Now.Hour);

    private static int Clamp(int hour) => Math.Clamp(hour, 0, 23);
}
