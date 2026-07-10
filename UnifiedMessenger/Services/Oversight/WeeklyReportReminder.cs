using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Decides when the in-app weekly-report reminder should surface. The app runs continuously in the tray,
/// so instead of registering an OS scheduled task (persistent system config) it simply nudges the operator
/// once a week from inside the command center. Pure and deterministic given a clock. Fully local.
/// </summary>
public static class WeeklyReportReminder
{
    /// <summary>How long between reminders.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromDays(7);

    /// <summary>
    /// True when the reminder is enabled and at least a week has passed since it was last shown. A null
    /// <see cref="AppSettings.WeeklyReportLastShownUtc"/> means the baseline hasn't been set yet — callers
    /// should stamp <see cref="Baseline"/> on first run so the first nudge lands a week into use, not day one.
    /// </summary>
    public static bool IsDue(AppSettings? settings, DateTimeOffset nowUtc)
    {
        if (settings is null || !settings.WeeklyReportReminderEnabled)
        {
            return false;
        }

        return settings.WeeklyReportLastShownUtc is { } last
            ? nowUtc - last >= Interval
            : false;
    }

    /// <summary>True when the reminder is on but no baseline has been recorded yet (first-run case).</summary>
    public static bool NeedsBaseline(AppSettings? settings) =>
        settings is { WeeklyReportReminderEnabled: true, WeeklyReportLastShownUtc: null };
}
