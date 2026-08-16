using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-11 — the end-of-day projection scales today's inbound by the share of a typical day that has
/// normally arrived by now. The handoff flagged it as "the one that divides by an hour-fraction, so a
/// 23-hour day is where it breaks". It does skew on a transition day, but by a bounded and small amount;
/// these tests establish the size of that skew rather than assert it away, so the decision to accept it
/// is on the record with a number attached.
///
/// <para>
/// They also cover the ordinary arithmetic, which had no test at all before — the public method reads the
/// wall clock, so an empty shape, a mid-day call and a spike were all unreachable from a test.
/// </para>
/// </summary>
public class EndOfDayProjectionTests
{
    /// <summary>
    /// A plausible inbound shape for a business: quiet overnight, busy from mid-morning, tailing off.
    /// Deliberately not flat — a flat shape would hide the DST skew entirely, since every hour would
    /// carry the same weight.
    /// </summary>
    private static long[] BusinessDayShape()
    {
        long[] hours =
        [
            2, 1, 1, 1, 1, 2, 6, 18, 45, 80, 110, 120,   // 00:00–11:59
            95, 90, 105, 115, 100, 70, 45, 30, 20, 12, 7, 4 // 12:00–23:59
        ];
        return hours;
    }

    private static int Projected(IReadOnlyList<long> shape, int soFar, int nowHour) =>
        MessageAnalyticsService.ProjectFromHourlyShape(shape, soFar, nowHour).Projected;

    // ---- Ordinary arithmetic ------------------------------------------------------------------------

    [Fact]
    public void NothingInYetMeansNoProjectionAtAll()
    {
        var result = MessageAnalyticsService.ProjectFromHourlyShape(BusinessDayShape(), soFar: 0, nowHour: 14);

        Assert.False(result.HasData);
        Assert.Equal(0, result.Projected);
    }

    [Fact]
    public void WithNoLearnedShapeTheProjectionIsJustWhatIsAlreadyIn()
    {
        // A brand-new install has an empty histogram. It must report today's count, not extrapolate from
        // nothing and not divide by zero.
        var result = MessageAnalyticsService.ProjectFromHourlyShape(new long[24], soFar: 12, nowHour: 14);

        Assert.True(result.HasData);
        Assert.Equal(12, result.SoFar);
        Assert.Equal(12, result.Projected);
    }

    [Fact]
    public void TooEarlyInTheDayToExtrapolateReportsTodaySoFarUnscaled()
    {
        // At 02:00 barely any of a normal day has landed, so dividing by that fraction would multiply a
        // couple of messages into a wild number. The 5% guard must hold.
        var result = MessageAnalyticsService.ProjectFromHourlyShape(BusinessDayShape(), soFar: 3, nowHour: 2);

        Assert.Equal(3, result.Projected);
    }

    [Fact]
    public void ByMiddayTheProjectionIsMeaningfullyAboveWhatIsAlreadyIn()
    {
        var shape = BusinessDayShape();
        var throughNoon = shape.Take(13).Sum();
        var total = shape.Sum();
        var expected = (int)Math.Round(60 / (throughNoon / (double)total));

        Assert.Equal(expected, Projected(shape, soFar: 60, nowHour: 12));
        Assert.True(Projected(shape, 60, 12) > 60);
    }

    [Fact]
    public void AtTheEndOfTheDayTheProjectionConvergesOnTheActualCount()
    {
        Assert.Equal(200, Projected(BusinessDayShape(), soFar: 200, nowHour: 23));
    }

    [Fact]
    public void TheProjectionNeverFallsBelowWhatHasAlreadyArrived()
    {
        // A day busier than usual would otherwise project a total lower than the count beside it — the
        // same "visibly self-contradicting card" failure mode as the rounding lie.
        var shape = BusinessDayShape();
        foreach (var hour in Enumerable.Range(0, 24))
        {
            Assert.True(Projected(shape, soFar: 5000, nowHour: hour) >= 5000);
        }
    }

    // ---- The DST skew, measured -------------------------------------------------------------------

    /// <summary>
    /// What the projection would say if the shape knew which hours today actually has. On a 23-hour day
    /// the skipped hour delivers nothing and should not be in either side of the fraction; on a 25-hour
    /// day the repeated hour delivers twice and should count twice.
    /// </summary>
    private static int IdealProjection(IReadOnlyList<long> shape, int soFar, int nowHour, int transitionHour, int delta)
    {
        double total = 0;
        double through = 0;
        for (var h = 0; h < 24; h++)
        {
            var weight = h == transitionHour ? 1 + delta : 1;
            total += shape[h] * weight;
            if (h <= nowHour)
            {
                through += shape[h] * weight;
            }
        }

        return (int)Math.Round(soFar / (through / total));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(18)]
    [InlineData(21)]
    public void OnA23HourDayTheProjectionReadsSlightlyLowAndTheErrorStaysUnderTwoPercent(int nowHour)
    {
        // 02:00 never happens. The shape still credits that hour's historical share to the elapsed part
        // of the day, so the code thinks more of the day has passed than really has and scales down.
        var shape = BusinessDayShape();
        const int soFar = 150;

        var actual = Projected(shape, soFar, nowHour);
        var ideal = IdealProjection(shape, soFar, nowHour, transitionHour: 2, delta: -1);

        Assert.True(actual <= ideal, $"expected a low reading; actual={actual} ideal={ideal}");
        Assert.InRange((ideal - actual) / (double)ideal, 0, 0.02);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(18)]
    [InlineData(21)]
    public void OnA25HourDayTheProjectionReadsSlightlyHighAndTheErrorStaysUnderTwoPercent(int nowHour)
    {
        // 01:00 happens twice. Today's count includes both, the shape counts one, so the code thinks less
        // of the day has passed than really has and scales up.
        var shape = BusinessDayShape();
        const int soFar = 150;

        var actual = Projected(shape, soFar, nowHour);
        var ideal = IdealProjection(shape, soFar, nowHour, transitionHour: 1, delta: 1);

        Assert.True(actual >= ideal, $"expected a high reading; actual={actual} ideal={ideal}");
        Assert.InRange((actual - ideal) / (double)ideal, 0, 0.02);
    }

    [Fact]
    public void TheSkewIsSmallBecauseTransitionsHappenInTheQuietestHoursOfTheDay()
    {
        // This is the whole reason the skew is acceptable, so state it as a test rather than a comment:
        // real zones transition between midnight and 03:00, and those hours carry a negligible share of
        // a business's inbound. If a zone ever transitioned at 10:00 the conclusion would change.
        var shape = BusinessDayShape();
        var total = (double)shape.Sum();
        foreach (var hour in new[] { 0, 1, 2, 3 })
        {
            Assert.InRange(shape[hour] / total, 0, 0.02);
        }
    }

    [Fact]
    public void ATransitionDayNeverProducesAnAbsurdOrNegativeProjection()
    {
        // The bound above is about accuracy. This is about not embarrassing itself: whatever hour of a
        // transition day it is asked on, the answer stays a sane multiple of what has already arrived.
        var shape = BusinessDayShape();
        foreach (var hour in Enumerable.Range(0, 24))
        {
            var result = MessageAnalyticsService.ProjectFromHourlyShape(shape, soFar: 100, nowHour: hour);

            Assert.True(result.Projected >= 100);
            Assert.True(result.Projected <= 100 * 60, $"hour {hour} projected {result.Projected}");
        }
    }
}
