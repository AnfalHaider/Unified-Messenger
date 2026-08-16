namespace UnifiedMessenger.Tests;

/// <summary>
/// Synthetic time zones so daylight-saving behaviour can be tested on a machine that has no DST.
///
/// <para>
/// This matters for a specific reason: the development and owner machine runs at <b>UTC+5 (Pakistan),
/// which never observes DST</b>. Every existing day-keying test — <see cref="TrendDayKeyingTests"/>
/// included — is therefore blind to transition days, and so is CI at UTC. Building the zones here means
/// the DST tests discriminate everywhere instead of only on a machine that happens to be in the right
/// place at the right time of year.
/// </para>
/// <para>
/// The rules are modelled on real zones rather than invented, because the awkward cases are real:
/// <list type="bullet">
///   <item><b>02:00 transition</b> (US/EU style) — local midnight always exists and is unambiguous, but
///   the day is 23 or 25 hours long and the UTC offset at midnight differs from the offset at noon.</item>
///   <item><b>Midnight transition</b> (Cuba/Chile style) — local midnight itself is <i>skipped</i> in
///   spring and happens <i>twice</i> in autumn. This is where naive "start of day" arithmetic throws or
///   silently picks the wrong instant.</item>
/// </list>
/// </para>
/// </summary>
internal static class DstTimeZones
{
    /// <summary>Base offset shared by both fixtures, so expected UTC instants stay easy to read.</summary>
    public static readonly TimeSpan StandardOffset = TimeSpan.FromHours(-5);

    public static readonly TimeSpan DaylightOffset = TimeSpan.FromHours(-4);

    /// <summary>
    /// US-style: forward on the 2nd Sunday of March at 02:00, back on the 1st Sunday of November at 02:00.
    /// In 2026 that is 8 March and 1 November.
    /// </summary>
    public static TimeZoneInfo TwoAmTransition { get; } = Build(
        "UM-Test-2am",
        springTimeOfDay: new TimeSpan(2, 0, 0),
        autumnTimeOfDay: new TimeSpan(2, 0, 0));

    /// <summary>
    /// Cuba-style: forward at 00:00 (midnight never happens) and back at 01:00 daylight → 00:00 standard
    /// (midnight happens twice). Both boundaries land exactly on the day boundary the metrics key from.
    /// </summary>
    public static TimeZoneInfo MidnightTransition { get; } = Build(
        "UM-Test-midnight",
        springTimeOfDay: TimeSpan.Zero,
        autumnTimeOfDay: new TimeSpan(1, 0, 0));

    /// <summary>The 23-hour day in 2026 for both fixtures.</summary>
    public static readonly DateTime SpringForwardDay = new(2026, 3, 8);

    /// <summary>The 25-hour day in 2026 for both fixtures.</summary>
    public static readonly DateTime FallBackDay = new(2026, 11, 1);

    /// <summary>
    /// The most recent 23-hour day that is already in the past.
    ///
    /// <para>
    /// Anything that reads samples by walking backwards from <i>today</i> — the response-time day series
    /// does — cannot see a fixed future date. Using the fixed 2026 constants there produced two empty
    /// results that looked like product failures and were not. These accessors keep such tests correct
    /// whatever the calendar says, without making them depend on the machine's own zone.
    /// </para>
    /// </summary>
    public static DateTime LatestPastSpringForward(DateTime? asOf = null) =>
        LatestPastTransition(asOf ?? DateTime.Today, month: 3, occurrence: 2);

    /// <summary>The most recent 25-hour day that is already in the past. See <see cref="LatestPastSpringForward"/>.</summary>
    public static DateTime LatestPastFallBack(DateTime? asOf = null) =>
        LatestPastTransition(asOf ?? DateTime.Today, month: 11, occurrence: 1);

    /// <summary>Days to ask a backward-looking day series for so it reaches either accessor above.</summary>
    public const int LookbackDaysCoveringATransition = 500;

    private static DateTime LatestPastTransition(DateTime asOf, int month, int occurrence)
    {
        for (var year = asOf.Year; year > asOf.Year - 3; year--)
        {
            var candidate = NthSunday(year, month, occurrence);
            // Strictly before today: tests place messages on the day *after* the transition too.
            if (candidate < asOf.Date.AddDays(-1))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No past transition found — the fixture rules changed.");
    }

    private static DateTime NthSunday(int year, int month, int occurrence)
    {
        var day = new DateTime(year, month, 1);
        while (day.DayOfWeek != DayOfWeek.Sunday)
        {
            day = day.AddDays(1);
        }

        return day.AddDays(7 * (occurrence - 1));
    }

    private static TimeZoneInfo Build(string id, TimeSpan springTimeOfDay, TimeSpan autumnTimeOfDay)
    {
        // TransitionTime demands a time-of-day whose date part is 01/01/0001.
        var springStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1) + springTimeOfDay, month: 3, week: 2, dayOfWeek: DayOfWeek.Sunday);
        var autumnEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1) + autumnTimeOfDay, month: 11, week: 1, dayOfWeek: DayOfWeek.Sunday);

        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            daylightDelta: TimeSpan.FromHours(1),
            daylightTransitionStart: springStart,
            daylightTransitionEnd: autumnEnd);

        return TimeZoneInfo.CreateCustomTimeZone(
            id, StandardOffset, id, $"{id} Standard", $"{id} Daylight", [rule]);
    }
}
