using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class BranchWorkspaceHelperTests
{
    [Fact]
    public void FilterByBranchKey_IncludesAllInboxesForBranch()
    {
        var instances = new[]
        {
            CreateInstance("wa-dha", "Depilex DHA-2", "whatsappbusiness"),
            CreateInstance("meta-dha", "Depilex DHA-2 Meta", "metabusiness"),
            CreateInstance("f11", "Depilex F-11", "googlebusiness")
        };

        var filtered = BranchWorkspaceHelper.FilterByBranchKey(instances, "DHA-2").ToList();

        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, instance => instance.Id == "wa-dha");
        Assert.Contains(filtered, instance => instance.Id == "meta-dha");
    }

    [Fact]
    public void BuildBranchMetrics_AggregatesThreadsAcrossInboxes()
    {
        var instances = new[]
        {
            CreateInstance("wa-dha", "Depilex DHA-2", "whatsappbusiness"),
            CreateInstance("meta-dha", "Depilex DHA-2 Meta", "metabusiness")
        };

        var threads = new[]
        {
            CreateThread("wa-dha", "DHA-2", "whatsappbusiness", "Sara"),
            CreateThread("meta-dha", "DHA-2", "metabusiness", "Inbox")
        };

        var metrics = BranchWorkspaceHelper.BuildBranchMetrics("DHA-2", threads, instances);

        Assert.Equal(2, metrics.UnresolvedCount);
        Assert.Equal(2, metrics.InboxCount);
        Assert.Contains("WA 1", metrics.PlatformBreakdown, StringComparison.Ordinal);
        Assert.Contains("Meta 1", metrics.PlatformBreakdown, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectBranchKeys_IncludesConfiguredBranchesWithoutThreads()
    {
        var instances = new[]
        {
            CreateInstance("wa-dha", "Depilex DHA-2", "whatsappbusiness"),
            CreateInstance("f11", "Depilex F-11", "googlebusiness")
        };

        var keys = BranchWorkspaceHelper.CollectBranchKeys(instances, []);

        Assert.Equal(2, keys.Count);
        Assert.Contains("DHA-2", keys);
        Assert.Contains("F-11", keys);
    }

    [Fact]
    public void BuildBranchFilterEntries_GroupsInboxesByBranch()
    {
        var instances = new[]
        {
            CreateInstance("wa-dha", "Depilex DHA-2", "whatsappbusiness"),
            CreateInstance("meta-dha", "Depilex DHA-2 Meta", "metabusiness"),
            CreateInstance("f11", "Depilex F-11", "googlebusiness")
        };

        var entries = BranchWorkspaceHelper.BuildBranchFilterEntries(instances);

        Assert.Equal(3, entries.Count);
        Assert.True(entries[0].IsAllBranches);
        Assert.Equal("DHA-2 (2 inboxes)", entries[1].DisplayName);
        Assert.Equal("F-11", entries[2].DisplayName);
    }

    private static MessengerInstance CreateInstance(string id, string displayName, string platform) =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Platform = platform,
            Category = WorkspaceCategory.Professional
        };

    private static ThreadData CreateThread(string instanceId, string branch, string platform, string customer) =>
        new()
        {
            ThreadId = $"{instanceId}|{customer}",
            Platform = platform,
            InstanceId = instanceId,
            BranchName = branch,
            CustomerName = customer,
            LastMessageTime = DateTimeOffset.UtcNow,
            LatencyMinutes = 5
        };
}
