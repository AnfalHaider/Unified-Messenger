using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for T-15 and T-16 — two places that mixed the UTC calendar day with the local one.
///
/// <para>
/// A date is not a point in time, and <c>DateTimeOffset.Date</c> reads it in whatever offset the value
/// happens to carry. <c>DateTimeOffset.UtcNow.Date</c> is therefore the UTC date, and comparing it against
/// a date derived with <c>ToLocalTime()</c> compares two different calendars. East of Greenwich the two
/// disagree over the evening; west of it, over the early morning. The owner's machine runs at UTC+5, where
/// the disagreement covers five hours of every day.
/// </para>
/// </summary>
public class DayBoundaryFrameTests
{
    /// <summary>UTC+5 — the owner's zone. The UTC date lags the local one from midnight to 05:00 local.</summary>
    private static readonly TimeZoneInfo East =
        TimeZoneInfo.CreateCustomTimeZone("um-test-east", TimeSpan.FromHours(5), "UTC+5", "UTC+5");

    /// <summary>UTC-6 — the mirror. The UTC date runs ahead of the local one from 18:00 local.</summary>
    private static readonly TimeZoneInfo West =
        TimeZoneInfo.CreateCustomTimeZone("um-test-west", TimeSpan.FromHours(-6), "UTC-6", "UTC-6");

    private static DateTimeOffset Local(TimeZoneInfo zone, int day, int hour) =>
        new DateTimeOffset(new DateTime(2026, 8, day, hour, 0, 0), zone.BaseUtcOffset).ToUniversalTime();

    // ---- T-15 · the ask-for-a-review panel's "messaged …" label -------------------------------------

    /// <summary>
    /// The case that was wrong for five hours a day at UTC+5: it is 02:00 local, and the customer messaged
    /// at 02:00 local <i>yesterday</i>. Both instants still fall on the same UTC date, so the old arithmetic
    /// returned zero and the panel said "messaged today" about a conversation from the day before — on the
    /// one surface whose job is deciding whether to contact a real person.
    /// </summary>
    [Fact]
    public void YesterdayIsNotLabelledTodayInTheEarlyHoursEastOfGreenwich()
    {
        var label = ReviewAskCandidates.WhenLabel(Local(East, 26, 2), Local(East, 27, 2), East);

        Assert.Equal("yesterday", label);
    }

    [Fact]
    public void EarlierTodayIsStillLabelledTodayEastOfGreenwich()
    {
        var label = ReviewAskCandidates.WhenLabel(Local(East, 27, 1), Local(East, 27, 4), East);

        Assert.Equal("today", label);
    }

    /// <summary>
    /// The mirror. West of Greenwich the UTC date runs <i>ahead</i> over the local evening, so the same
    /// arithmetic erred the other way and called a message sent hours earlier the same evening "yesterday".
    /// </summary>
    [Fact]
    public void TodayIsNotLabelledYesterdayInTheEveningWestOfGreenwich()
    {
        var label = ReviewAskCandidates.WhenLabel(Local(West, 27, 19), Local(West, 27, 22), West);

        Assert.Equal("today", label);
    }

    [Fact]
    public void YesterdayEveningIsLabelledYesterdayWestOfGreenwich()
    {
        var label = ReviewAskCandidates.WhenLabel(Local(West, 26, 19), Local(West, 27, 22), West);

        Assert.Equal("yesterday", label);
    }

    // ---- T-16 · the backfill dedupe day ---------------------------------------------------------------

    /// <summary>
    /// Accepting a row here is what records an inbound message into the analytics daily bucket, and that
    /// bucket is keyed by the local calendar day. When this key used the UTC day, two rows from the same
    /// local day straddled the UTC boundary, both were accepted, and one local day was counted twice.
    /// </summary>
    [Fact]
    public void TwoRowsOnOneLocalDayShareOneDedupeKey()
    {
        var zone = TimeZoneInfo.Local;
        var today = LocalDayBoundary.Today(zone);

        // The first and last hour of the same local day. In any zone with a non-zero offset these land on
        // different UTC dates for part of the year, which is exactly what the old key split on.
        var earlyLocal = LocalDayBoundary.StartOfDay(today, zone).AddHours(1);
        var lateLocal = LocalDayBoundary.EndOfDayExclusive(today, zone).AddHours(-1);

        var early = BackfillDedupeStore.BuildDayKey("acct", "whatsapp", "923000000000@c.us", earlyLocal);
        var late = BackfillDedupeStore.BuildDayKey("acct", "whatsapp", "923000000000@c.us", lateLocal);

        Assert.Equal(early, late);
    }

    /// <summary>
    /// And the converse: rows either side of local midnight must not collapse into one key, which is how
    /// the old key silently dropped a conversation's first row of a new local day.
    /// </summary>
    [Fact]
    public void RowsEitherSideOfLocalMidnightGetDifferentDedupeKeys()
    {
        var zone = TimeZoneInfo.Local;
        var today = LocalDayBoundary.Today(zone);

        var lastHourOfYesterday = LocalDayBoundary.StartOfDay(today, zone).AddMinutes(-30);
        var firstHourOfToday = LocalDayBoundary.StartOfDay(today, zone).AddMinutes(30);

        var before = BackfillDedupeStore.BuildDayKey("acct", "whatsapp", "923000000000@c.us", lastHourOfYesterday);
        var after = BackfillDedupeStore.BuildDayKey("acct", "whatsapp", "923000000000@c.us", firstHourOfToday);

        Assert.NotEqual(before, after);
    }

    /// <summary>
    /// The dedupe day and the analytics bucket day are the same fact and must be derived the same way. This
    /// pins them together so a future change to one is caught rather than quietly re-opening the gap.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(13)]
    [InlineData(19)]
    [InlineData(23)]
    public void TheDedupeDayMatchesTheAnalyticsBucketDay(int localHour)
    {
        var zone = TimeZoneInfo.Local;
        var today = LocalDayBoundary.Today(zone);
        var instant = LocalDayBoundary.StartOfDay(today, zone).AddHours(localHour);

        var bucketDay = LocalDayBoundary.LocalDate(instant, zone).ToString("yyyy-MM-dd");
        var key = BackfillDedupeStore.BuildDayKey("acct", "whatsapp", "chat", instant);

        Assert.EndsWith($"|{bucketDay}", key, StringComparison.Ordinal);
    }
}
