using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// The line under the notification hub's header saying why it might be quieter than expected
/// (mockup §07).
/// </summary>
/// <remarks>
/// <para>
/// <b>The ambiguity this removes.</b> An empty hub has two completely different meanings — <i>nothing
/// happened</i> and <i>we are not telling you</i> — and until now it rendered the same either way. Quiet
/// hours lived only in Settings, so an owner who turned it on weeks ago and then had a silent evening had
/// no way, from the hub, to tell a calm night from a suppressed one.
/// </para>
/// <para>
/// The same applies to a signed-out account: it will never raise an alert, so its silence is not evidence
/// of anything. Naming both is what makes the hub's quiet trustworthy on the nights it is genuine.
/// </para>
/// </remarks>
public static class NotificationHubStatus
{
    /// <summary>
    /// The status line, or null when there is nothing to disclose — a notice on every screen stops being
    /// read, so a hub with notifications flowing normally says nothing at all.
    /// </summary>
    public static string? Describe(AppSettings? settings, int signedOutCount, int localHour)
    {
        var clauses = new List<string>();

        if (settings is not null && QuietHours.IsQuiet(settings, localHour))
        {
            // Says when it ends, because "quiet hours are on" leaves the owner to work out whether that is
            // why the evening is silent and how long it will stay that way.
            clauses.Add($"Quiet hours until {FormatHour(settings.QuietHoursEndHour)} — alerts are held, not lost");
        }

        if (signedOutCount > 0)
        {
            clauses.Add(signedOutCount == 1
                ? "1 account is signed out and cannot raise alerts"
                : $"{signedOutCount} accounts are signed out and cannot raise alerts");
        }

        return clauses.Count == 0 ? null : string.Join(" · ", clauses);
    }

    /// <summary>
    /// "8am" / "9pm" / "12 noon" — the hour as an owner would say it, not as a 24-hour number.
    /// </summary>
    private static string FormatHour(int hour24)
    {
        var hour = ((hour24 % 24) + 24) % 24;

        return hour switch
        {
            0 => "midnight",
            12 => "noon",
            < 12 => $"{hour}am",
            _ => $"{hour - 12}pm"
        };
    }
}
