namespace UnifiedMessenger.Tests;

/// <summary>
/// Ground truth for <see cref="DstTimeZones"/>. These assert on the .NET framework, not on product code.
///
/// <para>
/// They exist because every DST finding in this audit rests on what <see cref="TimeZoneInfo"/> actually
/// does with invalid and ambiguous local times, and that behaviour is easy to remember wrongly. If a
/// future .NET or a future fixture edit changes it, these fail first and loudly, instead of the product
/// tests failing in a way that looks like a product regression.
/// </para>
/// </summary>
public class DstGroundTruthTests
{
    private static DateTime Unspecified(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    [Fact]
    public void TheTwoAmZoneSpringsForwardAndBackOnTheExpectedDaysOf2026()
    {
        var zone = DstTimeZones.TwoAmTransition;

        // Before the March transition, and after the November one, the zone is on standard time.
        Assert.Equal(DstTimeZones.StandardOffset, zone.GetUtcOffset(Unspecified(new DateTime(2026, 3, 8, 1, 0, 0))));
        Assert.Equal(DstTimeZones.DaylightOffset, zone.GetUtcOffset(Unspecified(new DateTime(2026, 3, 8, 3, 0, 0))));
        Assert.Equal(DstTimeZones.DaylightOffset, zone.GetUtcOffset(Unspecified(new DateTime(2026, 10, 31, 12, 0, 0))));
        Assert.Equal(DstTimeZones.StandardOffset, zone.GetUtcOffset(Unspecified(new DateTime(2026, 11, 1, 12, 0, 0))));
    }

    [Fact]
    public void TheTwoAmZoneHasA23HourDayInSpringAndA25HourDayInAutumn()
    {
        var zone = DstTimeZones.TwoAmTransition;

        var springStart = new DateTimeOffset(DstTimeZones.SpringForwardDay, DstTimeZones.StandardOffset);
        var springEnd = new DateTimeOffset(DstTimeZones.SpringForwardDay.AddDays(1), DstTimeZones.DaylightOffset);
        Assert.Equal(23, (springEnd - springStart).TotalHours);
        Assert.Equal(DstTimeZones.StandardOffset, zone.GetUtcOffset(Unspecified(DstTimeZones.SpringForwardDay)));

        var autumnStart = new DateTimeOffset(DstTimeZones.FallBackDay, DstTimeZones.DaylightOffset);
        var autumnEnd = new DateTimeOffset(DstTimeZones.FallBackDay.AddDays(1), DstTimeZones.StandardOffset);
        Assert.Equal(25, (autumnEnd - autumnStart).TotalHours);
    }

    [Fact]
    public void TwoAmZoneMidnightIsNeitherSkippedNorRepeated()
    {
        // The whole point of the second fixture: this zone's day boundary is well-behaved, so any failure
        // it produces is about offset selection, not about a missing or duplicated midnight.
        var zone = DstTimeZones.TwoAmTransition;

        Assert.False(zone.IsInvalidTime(Unspecified(DstTimeZones.SpringForwardDay)));
        Assert.False(zone.IsAmbiguousTime(Unspecified(DstTimeZones.FallBackDay)));
    }

    [Fact]
    public void TheRelativeTransitionAccessorsLandOnRealTransitionDaysInThePast()
    {
        // These feed the tests that walk backwards from today, so a wrong date there shows up as an empty
        // result that reads like a product bug. Assert they are genuinely 23/25 hours long and past.
        var spring = DstTimeZones.LatestPastSpringForward();
        var autumn = DstTimeZones.LatestPastFallBack();

        Assert.True(spring < DateTime.Today);
        Assert.True(autumn < DateTime.Today);
        Assert.True((DateTime.Today - spring).TotalDays < DstTimeZones.LookbackDaysCoveringATransition);
        Assert.True((DateTime.Today - autumn).TotalDays < DstTimeZones.LookbackDaysCoveringATransition);

        Assert.Equal(DstTimeZones.StandardOffset, DstTimeZones.TwoAmTransition.GetUtcOffset(Unspecified(spring)));
        Assert.Equal(DstTimeZones.DaylightOffset, DstTimeZones.TwoAmTransition.GetUtcOffset(Unspecified(spring.AddDays(1))));
        Assert.Equal(DstTimeZones.DaylightOffset, DstTimeZones.TwoAmTransition.GetUtcOffset(Unspecified(autumn)));
        Assert.Equal(DstTimeZones.StandardOffset, DstTimeZones.TwoAmTransition.GetUtcOffset(Unspecified(autumn.AddDays(1))));
    }

    [Fact]
    public void MidnightZoneSkipsMidnightInSpringAndRepeatsItInAutumn()
    {
        var zone = DstTimeZones.MidnightTransition;

        Assert.True(zone.IsInvalidTime(Unspecified(DstTimeZones.SpringForwardDay)));
        Assert.True(zone.IsAmbiguousTime(Unspecified(DstTimeZones.FallBackDay)));
    }

    [Fact]
    public void AnAmbiguousMidnightOffersBothOffsetsAndTheDaylightOneComesFirstInAbsoluteTime()
    {
        // This is the fact the "start of day" fix depends on. The first of the two 00:00s is the one on
        // daylight time (the larger offset); picking the standard one would silently drop the first hour
        // of a 25-hour day out of every "today" window.
        var offsets = DstTimeZones.MidnightTransition
            .GetAmbiguousTimeOffsets(Unspecified(DstTimeZones.FallBackDay));

        Assert.Equal(2, offsets.Length);
        Assert.Contains(DstTimeZones.StandardOffset, offsets);
        Assert.Contains(DstTimeZones.DaylightOffset, offsets);

        var viaDaylight = new DateTimeOffset(DstTimeZones.FallBackDay, DstTimeZones.DaylightOffset);
        var viaStandard = new DateTimeOffset(DstTimeZones.FallBackDay, DstTimeZones.StandardOffset);
        Assert.True(viaDaylight < viaStandard);
    }

    [Fact]
    public void TheFirstValidMinuteAfterASkippedMidnightIsExactlyTheTransitionInstant()
    {
        // How the fix locates the start of a day whose midnight does not exist: walk forward to the first
        // real minute. That minute is 01:00 daylight, which is the same absolute instant 00:00 standard
        // would have been — i.e. no part of the day is lost or double-counted.
        var zone = DstTimeZones.MidnightTransition;
        var probe = Unspecified(DstTimeZones.SpringForwardDay);
        while (zone.IsInvalidTime(probe))
        {
            probe = probe.AddMinutes(1);
        }

        Assert.Equal(new DateTime(2026, 3, 8, 1, 0, 0), probe);

        var dayStart = new DateTimeOffset(probe, zone.GetUtcOffset(probe));
        var nominalMidnight = new DateTimeOffset(DstTimeZones.SpringForwardDay, DstTimeZones.StandardOffset);
        Assert.Equal(nominalMidnight, dayStart);
    }
}
