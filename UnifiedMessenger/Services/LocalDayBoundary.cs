namespace UnifiedMessenger.Services;

/// <summary>
/// Turns a local calendar day into the absolute-time window it actually occupies, correctly on the two
/// days a year when that window is not 24 hours long.
///
/// <para>
/// <b>Why this exists.</b> Every date filter in the product was written as
/// <c>new DateTimeOffset(someLocalDate, DateTimeOffset.Now.Offset)</c> — a local midnight paired with the
/// UTC offset of a <i>different</i> instant (usually "now"). On an ordinary day those agree and the
/// expression is harmless. On a DST transition day they do not: at 14:00 on a spring-forward day the
/// current offset is already the daylight one, so pairing it with midnight names an instant an hour
/// <i>before</i> the day began, and "Today" silently swallows the last hour of yesterday. In autumn it
/// runs the other way and the first hour of today is silently excluded.
/// </para>
/// <para>
/// <b>Two hard cases, both real.</b> Most zones transition at 02:00, where midnight is well behaved. But
/// Cuba and Chile transition <i>at midnight</i>, so local 00:00 either never happens (spring) or happens
/// twice (autumn). <c>new DateTimeOffset(midnight, zone.GetUtcOffset(midnight))</c> is wrong for both:
/// for an ambiguous midnight it picks the second occurrence and loses the day's first hour, and for a
/// skipped one it depends on undocumented invalid-time behaviour. Those cases are handled explicitly
/// below.
/// </para>
/// <para>
/// The zone is a parameter rather than an implicit <see cref="TimeZoneInfo.Local"/> read purely so this
/// is testable — this machine runs at UTC+5, which has no DST, so a test that could only use the local
/// zone would pass vacuously. Production callers omit it.
/// </para>
/// </summary>
public static class LocalDayBoundary
{
    /// <summary>
    /// The first instant of <paramref name="localDate"/>'s calendar day in <paramref name="zone"/>
    /// (default: the machine's zone). Inclusive: a window starting here contains the whole day.
    /// </summary>
    public static DateTimeOffset StartOfDay(DateTime localDate, TimeZoneInfo? zone = null)
    {
        var tz = zone ?? TimeZoneInfo.Local;
        var midnight = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);

        if (tz.IsInvalidTime(midnight))
        {
            // Midnight was skipped by a forward transition, so the day begins at the first wall-clock
            // minute that exists. Walking forward finds it without relying on what GetUtcOffset chooses
            // to report for a time that never happened. The gap is hours at most, so this is bounded.
            var probe = midnight;
            while (tz.IsInvalidTime(probe))
            {
                probe = probe.AddMinutes(1);
            }

            return new DateTimeOffset(probe, tz.GetUtcOffset(probe));
        }

        if (tz.IsAmbiguousTime(midnight))
        {
            // Midnight happens twice. The day starts at the FIRST one, which is the occurrence still on
            // daylight time — the larger offset. Taking the other would drop the first hour of a 25-hour
            // day out of every "today" figure on the one day of the year it matters.
            var offsets = tz.GetAmbiguousTimeOffsets(midnight);
            return new DateTimeOffset(midnight, offsets.Max());
        }

        return new DateTimeOffset(midnight, tz.GetUtcOffset(midnight));
    }

    /// <summary>
    /// The first instant of the <i>following</i> day — the exclusive upper bound of
    /// <paramref name="localDate"/>. Prefer this to <see cref="EndOfDay"/> for half-open comparisons.
    /// </summary>
    public static DateTimeOffset EndOfDayExclusive(DateTime localDate, TimeZoneInfo? zone = null) =>
        StartOfDay(localDate.Date.AddDays(1), zone);

    /// <summary>
    /// The last representable instant inside <paramref name="localDate"/>, for the inclusive
    /// "through end of day" semantics the date-range pickers use.
    /// </summary>
    public static DateTimeOffset EndOfDay(DateTime localDate, TimeZoneInfo? zone = null) =>
        EndOfDayExclusive(localDate, zone).AddTicks(-1);

    /// <summary>
    /// How long <paramref name="localDate"/> really is: 24 hours, or 23/25 across a transition. Callers
    /// that pro-rate anything by "how much of the day has elapsed" need this rather than a hard-coded 24.
    /// </summary>
    public static TimeSpan LengthOfDay(DateTime localDate, TimeZoneInfo? zone = null) =>
        EndOfDayExclusive(localDate, zone) - StartOfDay(localDate, zone);

    /// <summary>
    /// Start of the day <paramref name="daysBack"/> calendar days before <paramref name="localDate"/>.
    /// The subtraction is on the calendar, not on elapsed hours, so a window that spans a transition is
    /// still the requested number of days wide.
    /// </summary>
    public static DateTimeOffset StartOfDaysAgo(DateTime localDate, int daysBack, TimeZoneInfo? zone = null) =>
        StartOfDay(localDate.Date.AddDays(-daysBack), zone);

    /// <summary>
    /// The calendar day <paramref name="instant"/> falls on in <paramref name="zone"/>. Equivalent to
    /// <c>instant.LocalDateTime.Date</c> when the zone is the machine's, but takes the zone as a
    /// parameter so day-bucketing code can be exercised against a zone that actually observes DST.
    /// </summary>
    public static DateTime LocalDate(DateTimeOffset instant, TimeZoneInfo? zone = null) =>
        TimeZoneInfo.ConvertTime(instant, zone ?? TimeZoneInfo.Local).Date;

    /// <summary>Wall-clock now in <paramref name="zone"/>, offset included.</summary>
    public static DateTimeOffset Now(TimeZoneInfo? zone = null) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone ?? TimeZoneInfo.Local);

    /// <summary>Today's calendar date in <paramref name="zone"/> — the injectable form of <c>DateTime.Today</c>.</summary>
    public static DateTime Today(TimeZoneInfo? zone = null) => Now(zone).Date;
}
