using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ChartSeriesBuilderTests
{
    // ── ComputeDelta ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeDelta_NoBaseline_ReturnsNone()
    {
        // A first-week metric has nothing to compare against; a percent change off zero is meaningless.
        Assert.False(ChartSeriesBuilder.ComputeDelta(120, 0, MetricPolarity.HigherIsBetter).HasData);
    }

    [Fact]
    public void ComputeDelta_ReportsSignedPercentAndDirection()
    {
        var up = ChartSeriesBuilder.ComputeDelta(120, 100, MetricPolarity.Neutral);
        Assert.Equal(20, up.Percent);
        Assert.Equal(DeltaDirection.Up, up.Direction);

        var down = ChartSeriesBuilder.ComputeDelta(80, 100, MetricPolarity.Neutral);
        Assert.Equal(20, down.Percent);
        Assert.Equal(DeltaDirection.Down, down.Direction);
    }

    [Fact]
    public void ComputeDelta_SentimentFollowsPolarityNotSign()
    {
        // Response time DOWN is good; UP is bad.
        Assert.Equal(DeltaSentiment.Favourable, ChartSeriesBuilder.ComputeDelta(30, 40, MetricPolarity.LowerIsBetter).Sentiment);
        Assert.Equal(DeltaSentiment.Adverse, ChartSeriesBuilder.ComputeDelta(40, 30, MetricPolarity.LowerIsBetter).Sentiment);

        // Volume UP is good; DOWN is bad — for a higher-is-better metric.
        Assert.Equal(DeltaSentiment.Favourable, ChartSeriesBuilder.ComputeDelta(120, 100, MetricPolarity.HigherIsBetter).Sentiment);
        Assert.Equal(DeltaSentiment.Adverse, ChartSeriesBuilder.ComputeDelta(80, 100, MetricPolarity.HigherIsBetter).Sentiment);

        // Raw volume (Neutral polarity) is never judged good or bad in EITHER direction — this is the
        // mockup's "volume down is neutral, not red" rule.
        Assert.Equal(DeltaSentiment.Neutral, ChartSeriesBuilder.ComputeDelta(120, 100, MetricPolarity.Neutral).Sentiment);
        Assert.Equal(DeltaSentiment.Neutral, ChartSeriesBuilder.ComputeDelta(80, 100, MetricPolarity.Neutral).Sentiment);
    }

    // ── BuildShareSlices ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildShareSlices_PercentagesSumToExactly100()
    {
        // 1/1/1 would naively round to 33/33/33 = 99; largest-remainder must recover the missing point.
        var slices = ChartSeriesBuilder.BuildShareSlices(
        [
            ("A", "#111", 1),
            ("B", "#222", 1),
            ("C", "#333", 1)
        ]);

        Assert.Equal(3, slices.Count);
        Assert.Equal(100, slices.Sum(s => s.Percent));
    }

    [Fact]
    public void BuildShareSlices_DropsZeroValueRows()
    {
        var slices = ChartSeriesBuilder.BuildShareSlices(
        [
            ("WhatsApp", "#25D366", 62),
            ("Google", "#4285F4", 28),
            ("Silent", "#000", 0)
        ]);

        Assert.DoesNotContain(slices, s => s.Label == "Silent");
        Assert.Equal(100, slices.Sum(s => s.Percent));
    }

    [Fact]
    public void BuildShareSlices_AllZero_ReturnsEmpty()
    {
        Assert.Empty(ChartSeriesBuilder.BuildShareSlices([("A", "#111", 0), ("B", "#222", 0)]));
    }

    // ── BuildSlaBreakdown ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildSlaBreakdown_UnmeasurableEntityCountsOnlyAsNoSla()
    {
        var entities = new[]
        {
            Entity(measured: 10, onTime: 80, supportsTiming: true, hasChat: true),   // 8 met / 2 missed
            Entity(measured: 0, onTime: 100, supportsTiming: true, hasChat: true, accounts: 1), // no data → no-SLA
            Entity(measured: 5, onTime: 100, supportsTiming: false, hasChat: true)   // untimeable → no-SLA, not 5 met
        };

        var b = ChartSeriesBuilder.BuildSlaBreakdown(entities);

        Assert.Equal(8, b.Met);
        Assert.Equal(2, b.Missed);
        // 1 account with no data + 5 untimeable threads.
        Assert.Equal(6, b.NoSla);
        Assert.True(b.HasData);
    }

    [Fact]
    public void BuildSlaBreakdown_Empty_HasNoData()
    {
        Assert.False(ChartSeriesBuilder.BuildSlaBreakdown([]).HasData);
    }

    // ── RankTopPerformers ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RankTopPerformers_UnmeasuredAccountIsNotCrownedNumberOne()
    {
        // The whole point: OnTimePercent defaults to 100 for an unsynced account. A naive inversion would
        // put it first. It must not even appear.
        var entities = new[]
        {
            Entity(key: "unsynced", measured: 0, onTime: 100, supportsTiming: true, hasChat: false),
            Entity(key: "real", measured: 20, onTime: 92, supportsTiming: true, hasChat: true)
        };

        var ranked = ChartSeriesBuilder.RankTopPerformers(entities);

        Assert.Single(ranked);
        Assert.Equal("real", ranked[0].Key);
    }

    [Fact]
    public void RankTopPerformers_SmallerBacklogBreaksTheTieButNeverRewritesOnTime()
    {
        var entities = new[]
        {
            Entity(key: "clean", measured: 10, onTime: 90, supportsTiming: true, hasChat: true, awaiting: 0),
            Entity(key: "swamped", measured: 10, onTime: 90, supportsTiming: true, hasChat: true, awaiting: 50)
        };

        var ranked = ChartSeriesBuilder.RankTopPerformers(entities);

        Assert.Equal("clean", ranked[0].Key);
        // Backlog orders the tie; it must not be folded into the reported percentage. Both really did
        // reply on time 90% of the time, and that is what the leaderboard shows.
        Assert.Equal(90, ranked.Single(p => p.Key == "swamped").OnTimePercent);
        Assert.Equal(50, ranked.Single(p => p.Key == "swamped").AwaitingCount);
    }

    [Fact]
    public void RankTopPerformers_RanksByOnTimePercentNotABlendedScore()
    {
        // The regression that made the shipped leaderboard read "0%" for every account: a low on-time %
        // minus a flat backlog penalty floored everything at zero, losing the ordering entirely.
        var entities = new[]
        {
            Entity(key: "better", measured: 10, onTime: 18, supportsTiming: true, hasChat: true, awaiting: 30),
            Entity(key: "worse", measured: 10, onTime: 5, supportsTiming: true, hasChat: true, awaiting: 0)
        };

        var ranked = ChartSeriesBuilder.RankTopPerformers(entities);

        Assert.Equal("better", ranked[0].Key);
        Assert.Equal(18, ranked[0].OnTimePercent);
    }

    [Fact]
    public void RankTopPerformers_HonoursTheMaxLimit()
    {
        var entities = Enumerable.Range(0, 8)
            .Select(i => Entity(key: $"a{i}", measured: 5, onTime: 80 + i, supportsTiming: true, hasChat: true))
            .ToArray();

        Assert.Equal(3, ChartSeriesBuilder.RankTopPerformers(entities, max: 3).Count);
    }

    // ── BuildZeroPaddedDailySeries ───────────────────────────────────────────────────────────────

    [Fact]
    public void BuildZeroPaddedDailySeries_IsIndexAlignedToDatesAndSliceable()
    {
        var today = new DateTime(2026, 8, 10);
        var map = new Dictionary<string, int>
        {
            ["2026-08-10"] = 5, // today
            ["2026-08-08"] = 3, // 2 days ago; 08-09 intentionally missing
        };

        var series = ChartSeriesBuilder.BuildZeroPaddedDailySeries(map, today, days: 4);

        // oldest→newest across 08-07..08-10, with the gap zero-filled.
        Assert.Equal(new[] { 0, 3, 0, 5 }, series.ToArray());
    }

    // ── FormatAxisCount ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "0")]
    [InlineData(750, "750")]
    [InlineData(1200, "1.2K")]
    [InlineData(15000, "15K")]
    [InlineData(1_100_000, "1.1M")]
    public void FormatAxisCount_UsesKAndMSuffixes(double value, string expected)
    {
        Assert.Equal(expected, ChartSeriesBuilder.FormatAxisCount(value));
    }

    private static OversightEntityHealth Entity(
        int measured,
        int onTime,
        bool supportsTiming,
        bool hasChat,
        string key = "e",
        int awaiting = 0,
        int accounts = 1) =>
        new()
        {
            Key = key,
            DisplayName = key,
            AccountCount = accounts,
            MeasuredCount = measured,
            OnTimePercent = onTime,
            SupportsResponseTiming = supportsTiming,
            HasChatData = hasChat,
            AwaitingCount = awaiting
        };
}
