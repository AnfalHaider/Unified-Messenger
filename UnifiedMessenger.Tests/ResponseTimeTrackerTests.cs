using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Locks the forward-tracked First Response Time logic: an awaiting→replied transition records FRT from the
/// real inbound/reply timestamps, the median/SLA-compliance/answered-today aggregates are correct, and
/// ambiguous reads don't fabricate or lose samples.
/// </summary>
public class ResponseTimeTrackerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;

    public ResponseTimeTrackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "response-times.json");
    }

    private static MessengerInstance Inst(string id) =>
        new() { Id = id, DisplayName = id, Platform = "whatsapp" };

    // All test inbounds are recent-but-past; pin watch-start earlier so they count as in-window.
    private static ResponseTimeTracker NewTracker(string path, params string[] instanceIds)
    {
        var tracker = new ResponseTimeTracker(path);
        foreach (var id in instanceIds)
        {
            tracker.SetWatchStartForTests(id, DateTimeOffset.UtcNow.AddDays(-30));
        }

        return tracker;
    }

    [Fact]
    public void Observe_AwaitingThenReplied_RecordsFrtFromTimestamps()
    {
        var tracker = NewTracker(_storePath, "inst-1");
        var inbound = DateTimeOffset.UtcNow.AddMinutes(-30);

        // Customer message arrives (awaiting), then we reply 12 minutes later (last message from us).
        tracker.Observe("inst-1", "chat-a", isAwaiting: true, lastMessageFromMe: false, inbound);
        tracker.Observe("inst-1", "chat-a", isAwaiting: false, lastMessageFromMe: true, inbound.AddMinutes(12));

        var stats = tracker.GetStats([Inst("inst-1")], fromUtc: null, toUtc: null, slaThresholdMinutes: 15);

        Assert.True(stats.HasData);
        Assert.Equal(1, stats.SampleCount);
        Assert.Equal(12, stats.MedianMinutes, precision: 1);
        Assert.Equal(100, stats.SlaCompliancePercent); // 12 <= 15
    }

    [Fact]
    public void GetStats_SlaCompliance_CountsOnlyWithinThreshold()
    {
        var tracker = NewTracker(_storePath, "inst-1", "inst-2");
        var t0 = DateTimeOffset.UtcNow.AddHours(-2);

        // Three responses: 5 min, 10 min, 40 min. With a 15-min SLA, 2 of 3 comply → 67%.
        Record(tracker, "inst-1", "c1", t0, 5);
        Record(tracker, "inst-1", "c2", t0, 10);
        Record(tracker, "inst-1", "c3", t0, 40);

        var stats = tracker.GetStats([Inst("inst-1")], null, null, slaThresholdMinutes: 15);

        Assert.Equal(3, stats.SampleCount);
        Assert.Equal(10, stats.MedianMinutes, precision: 1);
        Assert.Equal(67, stats.SlaCompliancePercent);
    }

    [Fact]
    public void Observe_OnlyAwaiting_NoReply_RecordsNothing()
    {
        var tracker = NewTracker(_storePath, "inst-1", "inst-2");
        tracker.Observe("inst-1", "chat-a", isAwaiting: true, lastMessageFromMe: false, DateTimeOffset.UtcNow);

        var stats = tracker.GetStats([Inst("inst-1")], null, null, slaThresholdMinutes: 15);

        Assert.False(stats.HasData);
        Assert.Equal(0, stats.SampleCount);
    }

    [Fact]
    public void Observe_KeepsEarliestUnansweredInbound_AcrossRepeatReads()
    {
        var tracker = NewTracker(_storePath, "inst-1", "inst-2");
        var first = DateTimeOffset.UtcNow.AddMinutes(-60);

        // Customer messages, stays awaiting across several syncs (later inbound activity), then we reply.
        tracker.Observe("inst-1", "chat-a", true, false, first);
        tracker.Observe("inst-1", "chat-a", true, false, first.AddMinutes(20)); // must NOT overwrite pending
        tracker.Observe("inst-1", "chat-a", false, true, first.AddMinutes(45));

        var stats = tracker.GetStats([Inst("inst-1")], null, null, slaThresholdMinutes: 60);

        // FRT measured from the FIRST unanswered inbound (45 min), not the later read (25 min).
        Assert.Equal(45, stats.MedianMinutes, precision: 1);
    }

    [Fact]
    public void GetStats_AnsweredToday_CountsOnlyTodaysReplies()
    {
        var tracker = NewTracker(_storePath, "inst-1", "inst-2");
        var now = DateTimeOffset.Now;

        Record(tracker, "inst-1", "c1", now.AddMinutes(-10), 5);  // answered today
        Record(tracker, "inst-1", "c2", now.AddMinutes(-20), 5);  // answered today
        Record(tracker, "inst-1", "c3", now.AddDays(-3), 5);      // answered earlier

        var stats = tracker.GetStats([Inst("inst-1")], null, null, slaThresholdMinutes: 15);

        Assert.Equal(2, stats.AnsweredToday);
        Assert.Equal(3, stats.SampleCount);
    }

    [Fact]
    public void GetStats_ScopesToProvidedInstances()
    {
        var tracker = NewTracker(_storePath, "inst-1", "inst-2");
        var t0 = DateTimeOffset.UtcNow.AddHours(-1);
        Record(tracker, "inst-1", "c1", t0, 5);
        Record(tracker, "inst-2", "c2", t0, 50);

        var onlyOne = tracker.GetStats([Inst("inst-1")], null, null, slaThresholdMinutes: 15);

        Assert.Equal(1, onlyOne.SampleCount);
        Assert.Equal(5, onlyOne.MedianMinutes, precision: 1);
    }

    [Fact]
    public void Observe_PreWatchBacklog_IsExcluded()
    {
        // A chat whose customer message arrived BEFORE we started watching (backlog the owner may have
        // already handled on their phone) must not be counted — otherwise the first sync reports a huge FRT.
        var tracker = new ResponseTimeTracker(_storePath);
        tracker.SetWatchStartForTests("inst-1", DateTimeOffset.UtcNow.AddHours(-1));

        var oldInbound = DateTimeOffset.UtcNow.AddDays(-2); // arrived 2 days ago, before watch-start
        tracker.Observe("inst-1", "chat-old", isAwaiting: true, lastMessageFromMe: false, oldInbound);
        tracker.Observe("inst-1", "chat-old", isAwaiting: false, lastMessageFromMe: true, DateTimeOffset.UtcNow);

        var stats = tracker.GetStats([Inst("inst-1")], null, null, slaThresholdMinutes: 15);
        Assert.False(stats.HasData); // the 2-day "response" is backlog, not a measured reply
    }

    // Helper: drive one full awaiting→replied cycle producing a sample of the given FRT, answered at answeredAt.
    private static void Record(ResponseTimeTracker tracker, string instanceId, string conv, DateTimeOffset answeredAt, double frtMinutes)
    {
        var inbound = answeredAt.AddMinutes(-frtMinutes);
        tracker.Observe(instanceId, conv, isAwaiting: true, lastMessageFromMe: false, inbound);
        tracker.Observe(instanceId, conv, isAwaiting: false, lastMessageFromMe: true, answeredAt);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
