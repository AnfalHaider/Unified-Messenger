using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-10 — every date window in the product was built by pairing a local midnight with the UTC
/// offset of a different instant, which is off by an hour on both DST transition days.
///
/// <para>
/// The offending expression, repeated at nine sites, was
/// <c>new DateTimeOffset(nowLocal.Date, nowLocal.Offset)</c>. <c>nowLocal.Offset</c> is the offset
/// <i>right now</i>, not the offset at midnight. <see cref="LegacyStartOfDay"/> below reproduces it
/// exactly so the defect is demonstrated rather than described, and so it stays demonstrated: if anyone
/// reintroduces the pattern, these tests say what breaks and on which day.
/// </para>
/// <para>
/// The consequence is not abstract. "Today" and "Last 7 days" drive the caught-up percentage, the
/// awaiting counts, SLA-met %, and the account cards. An hour of conversations moving in or out of the
/// window changes all of them, on a day when nobody is looking for a date bug.
/// </para>
/// </summary>
public class LocalDayBoundaryTests
{
    /// <summary>Exactly what the product used to do: local midnight + the offset in force at <paramref name="observedAtLocal"/>.</summary>
    private static DateTimeOffset LegacyStartOfDay(DateTime localDate, DateTime observedAtLocal, TimeZoneInfo zone)
    {
        var offsetNow = zone.GetUtcOffset(DateTime.SpecifyKind(observedAtLocal, DateTimeKind.Unspecified));
        return new DateTimeOffset(DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified), offsetNow);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour) =>
        new(new DateTime(year, month, day, hour, 0, 0), TimeSpan.Zero);

    // ---- The correct boundary, on the well-behaved (02:00) transition zone -------------------------

    [Fact]
    public void TheSpringForwardDayStartsAtStandardMidnightNotAnHourEarlier()
    {
        var start = LocalDayBoundary.StartOfDay(DstTimeZones.SpringForwardDay, DstTimeZones.TwoAmTransition);

        Assert.Equal(Utc(2026, 3, 8, 5), start);
        Assert.Equal(DstTimeZones.StandardOffset, start.Offset);
    }

    [Fact]
    public void TheFallBackDayStartsAtDaylightMidnightNotAnHourLater()
    {
        var start = LocalDayBoundary.StartOfDay(DstTimeZones.FallBackDay, DstTimeZones.TwoAmTransition);

        Assert.Equal(Utc(2026, 11, 1, 4), start);
        Assert.Equal(DstTimeZones.DaylightOffset, start.Offset);
    }

    // ---- The defect, stated as the difference from the legacy expression ---------------------------

    [Fact]
    public void TheOldExpressionPulledAnExtraHourOfYesterdayIntoTodayEverySpringForward()
    {
        // Owner opens the dashboard at 14:00 on the transition day. By then the clock is on daylight
        // time, so the legacy expression names 04:00Z — an hour before the day actually started. Every
        // conversation between 23:00 and 23:59 the previous evening was counted as "today".
        var noonish = DstTimeZones.SpringForwardDay.AddHours(14);
        var legacy = LegacyStartOfDay(DstTimeZones.SpringForwardDay, noonish, DstTimeZones.TwoAmTransition);
        var correct = LocalDayBoundary.StartOfDay(DstTimeZones.SpringForwardDay, DstTimeZones.TwoAmTransition);

        Assert.Equal(TimeSpan.FromHours(-1), legacy - correct);

        var lastNightAt2330 = new DateTimeOffset(
            new DateTime(2026, 3, 7, 23, 30, 0), DstTimeZones.StandardOffset);
        Assert.True(lastNightAt2330 >= legacy, "the legacy window is expected to (wrongly) include it");
        Assert.True(lastNightAt2330 < correct, "yesterday evening must fall outside today");
    }

    [Fact]
    public void TheOldExpressionDroppedTheFirstHourOfTodayEveryFallBack()
    {
        // The mirror case, and the more damaging direction: real conversations from the first hour of the
        // day were excluded from today's counts entirely.
        var noonish = DstTimeZones.FallBackDay.AddHours(14);
        var legacy = LegacyStartOfDay(DstTimeZones.FallBackDay, noonish, DstTimeZones.TwoAmTransition);
        var correct = LocalDayBoundary.StartOfDay(DstTimeZones.FallBackDay, DstTimeZones.TwoAmTransition);

        Assert.Equal(TimeSpan.FromHours(1), legacy - correct);

        var thisMorningAt0030 = new DateTimeOffset(
            new DateTime(2026, 11, 1, 0, 30, 0), DstTimeZones.DaylightOffset);
        Assert.True(thisMorningAt0030 < legacy, "the legacy window is expected to (wrongly) exclude it");
        Assert.True(thisMorningAt0030 >= correct, "this morning must fall inside today");
    }

    [Fact]
    public void OnAnOrdinaryDayTheOldAndNewExpressionsAgree()
    {
        // Control. The fix must not move the boundary on the other 363 days, or it trades a rare wrong
        // number for a constant one.
        foreach (var day in new[] { new DateTime(2026, 6, 15), new DateTime(2026, 1, 20) })
        {
            var legacy = LegacyStartOfDay(day, day.AddHours(14), DstTimeZones.TwoAmTransition);
            var correct = LocalDayBoundary.StartOfDay(day, DstTimeZones.TwoAmTransition);
            Assert.Equal(correct, legacy);
        }
    }

    // ---- The zones that transition at midnight itself ----------------------------------------------

    [Fact]
    public void ADaySkippedMidnightStartsAtTheTransitionInstant()
    {
        // Cuba-style. Local 00:00 never happened; the day begins at 01:00 daylight, which is the same
        // absolute instant 00:00 standard would have been — so no hour is lost or double-counted.
        var start = LocalDayBoundary.StartOfDay(DstTimeZones.SpringForwardDay, DstTimeZones.MidnightTransition);

        Assert.Equal(Utc(2026, 3, 8, 5), start);
    }

    [Fact]
    public void ARepeatedMidnightResolvesToTheFirstOfTheTwo()
    {
        // The other Cuba-style case. Both 00:00s are valid; taking the later one would have silently
        // dropped the first hour of a 25-hour day. Assert the earlier one and, explicitly, that the
        // in-between hour is inside the window.
        var start = LocalDayBoundary.StartOfDay(DstTimeZones.FallBackDay, DstTimeZones.MidnightTransition);

        Assert.Equal(Utc(2026, 11, 1, 4), start);

        var firstMidnight = new DateTimeOffset(DstTimeZones.FallBackDay, DstTimeZones.DaylightOffset);
        var secondMidnight = new DateTimeOffset(DstTimeZones.FallBackDay, DstTimeZones.StandardOffset);
        Assert.True(firstMidnight >= start);
        Assert.True(secondMidnight > start);
    }

    // ---- Day length and multi-day windows ----------------------------------------------------------

    [Theory]
    [InlineData(23)]
    [InlineData(25)]
    public void ATransitionDayIsReported23Or25HoursLongRatherThanAssumed24(int expectedHours)
    {
        var day = expectedHours == 23 ? DstTimeZones.SpringForwardDay : DstTimeZones.FallBackDay;

        Assert.Equal(
            expectedHours,
            LocalDayBoundary.LengthOfDay(day, DstTimeZones.TwoAmTransition).TotalHours);
        Assert.Equal(
            expectedHours,
            LocalDayBoundary.LengthOfDay(day, DstTimeZones.MidnightTransition).TotalHours);
    }

    [Fact]
    public void AnOrdinaryDayIsStill24Hours()
    {
        Assert.Equal(24, LocalDayBoundary.LengthOfDay(new DateTime(2026, 6, 15), DstTimeZones.TwoAmTransition).TotalHours);
    }

    [Fact]
    public void EndOfDayIsTheLastTickBeforeTheNextDayStarts()
    {
        foreach (var day in new[] { DstTimeZones.SpringForwardDay, DstTimeZones.FallBackDay, new DateTime(2026, 6, 15) })
        {
            var end = LocalDayBoundary.EndOfDay(day, DstTimeZones.TwoAmTransition);
            var nextStart = LocalDayBoundary.StartOfDay(day.AddDays(1), DstTimeZones.TwoAmTransition);

            Assert.Equal(nextStart.UtcTicks - 1, end.UtcTicks);
        }
    }

    [Fact]
    public void TheSevenDayWindowStaysSevenCalendarDaysWideAcrossATransition()
    {
        // "Last 7 days" must not become 6 days 23 hours (or 7 days 1 hour) of calendar coverage just
        // because a transition fell inside it. Measured as: start of the window, through end of today.
        var start = LocalDayBoundary.StartOfDaysAgo(DstTimeZones.FallBackDay, 6, DstTimeZones.TwoAmTransition);
        var end = LocalDayBoundary.EndOfDayExclusive(DstTimeZones.FallBackDay, DstTimeZones.TwoAmTransition);

        Assert.Equal(Utc(2026, 10, 26, 4), start);
        // Six ordinary days plus one 25-hour day.
        Assert.Equal(6 * 24 + 25, (end - start).TotalHours);

        var springStart = LocalDayBoundary.StartOfDaysAgo(DstTimeZones.SpringForwardDay, 6, DstTimeZones.TwoAmTransition);
        var springEnd = LocalDayBoundary.EndOfDayExclusive(DstTimeZones.SpringForwardDay, DstTimeZones.TwoAmTransition);
        Assert.Equal(6 * 24 + 23, (springEnd - springStart).TotalHours);
    }

    // ---- Defaulting to the machine zone -------------------------------------------------------------

    [Fact]
    public void OmittingTheZoneUsesTheMachineZoneAndAgreesWithPassingItExplicitly()
    {
        // Production callers omit the zone. This is the only test that touches TimeZoneInfo.Local, and it
        // deliberately asserts only equivalence — it must stay meaningful on a machine with no DST.
        var today = DateTime.Today;

        Assert.Equal(LocalDayBoundary.StartOfDay(today, TimeZoneInfo.Local), LocalDayBoundary.StartOfDay(today));
        Assert.Equal(LocalDayBoundary.EndOfDay(today, TimeZoneInfo.Local), LocalDayBoundary.EndOfDay(today));
        Assert.Equal(today, LocalDayBoundary.StartOfDay(today).LocalDateTime.Date);
    }
}
