using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OccKpiNavigationHelperTests
{
    [Fact]
    public void ResolveTarget_OpenThreads_PicksHighestLatencyUnreplied()
    {
        var threads = new[]
        {
            CreateThread("a", latency: 10, replied: false),
            CreateThread("b", latency: 45, replied: false),
            CreateThread("c", latency: 5, replied: true)
        };

        var target = OccKpiNavigationHelper.ResolveTarget(OccKpiKind.OpenThreads, threads);

        Assert.NotNull(target);
        Assert.Equal("inst-b", target!.InstanceId);
        Assert.Equal("customer-b", target.ConversationKey);
    }

    [Fact]
    public void ResolveTarget_HangingLeads_PicksTopKanbanColumnThread()
    {
        var threads = new[]
        {
            CreateThread("a", latency: 10, hangingLead: true, threadId: "a|1", lastMessageOffsetMinutes: -30),
            CreateThread("b", latency: 30, hangingLead: true, threadId: "b|1", lastMessageOffsetMinutes: -5),
            CreateThread("c", latency: 50, hangingLead: false, threadId: "c|1", lastMessageOffsetMinutes: -1)
        };

        var target = OccKpiNavigationHelper.ResolveTarget(OccKpiKind.HangingLeads, threads);

        Assert.NotNull(target);
        Assert.Equal("inst-b", target!.InstanceId);
    }

    [Fact]
    public void ResolveTarget_Urgent_PicksUrgentQueueLeader()
    {
        var threads = new[]
        {
            CreateThread("a", latency: 10, urgency: 3, threadId: "a|1"),
            CreateThread("b", latency: 5, urgency: 5, threadId: "b|1"),
            CreateThread("c", latency: 50, urgency: 5, threadId: "c|1")
        };

        var target = OccKpiNavigationHelper.ResolveTarget(OccKpiKind.Urgent, threads);

        Assert.NotNull(target);
        Assert.Equal("inst-c", target!.InstanceId);
    }

    [Fact]
    public void ResolveTarget_Urgent_ExcludesSlaOnlyThreads()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;
        var threads = new[]
        {
            CreateThread("a", latency: 40, urgency: 2, threadId: "a|1"),
            CreateThread("b", latency: 5, urgency: 5, threadId: "b|1")
        };

        var target = OccKpiNavigationHelper.ResolveTarget(OccKpiKind.Urgent, threads);

        Assert.NotNull(target);
        Assert.Equal("inst-b", target!.InstanceId);
    }

    [Fact]
    public void ResolveTarget_NeedsAction_PicksImmediateQueueLeader()
    {
        var threads = new[]
        {
            CreateThread("a", latency: 10, urgency: 3, threadId: "a|1"),
            CreateThread("b", latency: 5, urgency: 5, threadId: "b|1"),
            CreateThread("c", latency: 50, urgency: 5, threadId: "c|1")
        };

        var target = OccKpiNavigationHelper.ResolveTarget(OccKpiKind.Urgent, threads);

        Assert.NotNull(target);
        Assert.Equal("inst-c", target!.InstanceId);
    }

    [Fact]
    public void ResolveTarget_SlaBreaches_PicksWorstLatency()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;
        var threads = new[]
        {
            CreateThread("a", latency: 20, threadId: "a|1"),
            CreateThread("b", latency: 55, threadId: "b|1"),
            CreateThread("c", latency: 25, threadId: "c|1", replied: true)
        };

        var target = OccKpiNavigationHelper.ResolveTarget(OccKpiKind.SlaBreaches, threads);

        Assert.NotNull(target);
        Assert.Equal("inst-b", target!.InstanceId);
    }

    [Fact]
    public void ResolveTarget_ReturnsNullWhenNoMatchingThreads()
    {
        var target = OccKpiNavigationHelper.ResolveTarget(
            OccKpiKind.OpenThreads,
            [CreateThread("a", latency: 1, replied: true)]);

        Assert.Null(target);
    }

    private static ThreadData CreateThread(
        string suffix,
        double latency,
        bool replied = false,
        bool hangingLead = false,
        int urgency = 4,
        string? threadId = null,
        double lastMessageOffsetMinutes = 0)
    {
        return new ThreadData
        {
            ThreadId = threadId ?? $"inst-{suffix}|{suffix}",
            InstanceId = $"inst-{suffix}",
            Platform = "whatsappbusiness",
            BranchName = "DHA-2",
            CustomerName = $"Customer {suffix}",
            ConversationKey = $"customer-{suffix}",
            LatencyMinutes = latency,
            IsReplied = replied,
            UrgencyScore = urgency,
            IsRevenueLeakageRisk = hangingLead,
            LastMessageTime = DateTimeOffset.UtcNow.AddMinutes(lastMessageOffsetMinutes)
        };
    }
}
