using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-04 — "SLA met %" must not claim 100% while replies are missing the target.
///
/// Same defect class as the caught-up percentage fixed in v4.99.9, in a metric the README advertises:
/// <c>Math.Round(withinSla * 100.0 / count)</c> turns 499 of 500 (99.8%) into <b>100</b>, so the KPI tile
/// reads "SLA met 100%" beside a reply count that includes a breach. It also rounds 1 of 501 down to
/// <b>0</b>, reporting that nothing met the target when something did.
/// </summary>
public class SlaPercentRoundingTests : IDisposable
{
    private const int SlaMinutes = 15;

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "um-sla-rounding-tests", Guid.NewGuid().ToString("N"));

    private readonly ResponseTimeTracker _tracker;

    public SlaPercentRoundingTests()
    {
        Directory.CreateDirectory(_dir);
        _tracker = new ResponseTimeTracker(Path.Combine(_dir, "response-times.json"));
        _tracker.SetWatchStartForTests("acct", DateTimeOffset.UtcNow.AddDays(-30));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test cleanup only.
        }

        GC.SuppressFinalize(this);
    }

    private static readonly List<MessengerInstance> Accounts =
        [new() { Id = "acct", DisplayName = "acct", ProfileName = "acct", Platform = "whatsapp" }];

    /// <summary>Records one completed reply with the given response time.</summary>
    private void RecordReply(int index, double responseMinutes)
    {
        var key = $"chat-{index}";
        var inbound = DateTimeOffset.UtcNow.AddDays(-1).AddSeconds(index);

        _tracker.Observe("acct", key, isAwaiting: true, lastMessageFromMe: false, inbound);
        _tracker.Observe("acct", key, isAwaiting: false, lastMessageFromMe: true,
            inbound.AddMinutes(responseMinutes));
    }

    private ResponseTimeTracker.ResponseStats Stats() =>
        _tracker.GetStats(Accounts, fromUtc: null, toUtc: null, slaThresholdMinutes: SlaMinutes);

    [Fact]
    public void OneBreachAmongManyKeepsSlaBelowOneHundred()
    {
        // 499 fast + 1 slow = 99.8%, which rounds to 100.
        for (var i = 0; i < 499; i++)
        {
            RecordReply(i, 5);
        }

        RecordReply(499, 60);

        var stats = Stats();

        Assert.Equal(500, stats.SampleCount);
        Assert.True(
            stats.SlaCompliancePercent < 100,
            $"a reply breached the {SlaMinutes}-minute target but SLA met reads {stats.SlaCompliancePercent}%");
    }

    [Fact]
    public void EveryReplyWithinTargetStillReportsOneHundred()
    {
        // The guard must not cost the honest case its 100%.
        for (var i = 0; i < 50; i++)
        {
            RecordReply(i, 5);
        }

        Assert.Equal(100, Stats().SlaCompliancePercent);
    }

    [Fact]
    public void ASingleReplyWithinTargetDoesNotRoundAwayToZero()
    {
        // 1 of 501 is 0.2%, which rounds to 0 — reporting that nothing met the target when one did.
        RecordReply(0, 5);
        for (var i = 1; i < 501; i++)
        {
            RecordReply(i, 60);
        }

        var stats = Stats();

        Assert.Equal(501, stats.SampleCount);
        Assert.True(
            stats.SlaCompliancePercent >= 1,
            $"one reply met the target but SLA met reads {stats.SlaCompliancePercent}%");
    }

    [Fact]
    public void NoReplyWithinTargetIsStillZero()
    {
        for (var i = 0; i < 10; i++)
        {
            RecordReply(i, 60);
        }

        Assert.Equal(0, Stats().SlaCompliancePercent);
    }

    [Fact]
    public void NoSamplesReportsNoDataRatherThanAPerfectScore()
    {
        // An account with nothing measured must not read as 100% compliant.
        var stats = Stats();

        Assert.False(stats.HasData);
        Assert.Equal(0, stats.SampleCount);
    }

    [Fact]
    public void ASingleSampleGivesACoherentMedianAndPercentile()
    {
        // Single-sample boundary: median and p90 must both be that sample, not 0 or an index error.
        RecordReply(0, 8);

        var stats = Stats();

        Assert.True(stats.HasData);
        Assert.Equal(1, stats.SampleCount);
        Assert.Equal(8, stats.MedianMinutes, 1);
        Assert.Equal(8, stats.P90Minutes, 1);
        Assert.Equal(100, stats.SlaCompliancePercent);
    }

    [Fact]
    public void DailyWithinThresholdAppliesTheSameHonestyRule()
    {
        // The per-day trend series shares the defect and must share the fix.
        for (var i = 0; i < 499; i++)
        {
            RecordReply(i, 5);
        }

        RecordReply(499, 60);

        var points = _tracker.GetDailyWithinThreshold(Accounts, SlaMinutes, days: 7);
        var withSamples = points.Where(p => p.Count > 0).ToList();

        Assert.NotEmpty(withSamples);
        Assert.All(withSamples, p => Assert.True(
            p.Percent < 100,
            $"day '{p.Label}' had a breach among {p.Count} replies but reads {p.Percent}%"));
    }
}
