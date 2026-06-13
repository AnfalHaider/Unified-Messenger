using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

public class OccDateRangeFilterTests
{
    [Fact]
    public void IsWithinRange_RespectsInclusiveBounds()
    {
        var from = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 6, 10, 23, 59, 59, TimeSpan.Zero);
        var inside = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2026, 5, 31, 23, 0, 0, TimeSpan.Zero);
        var after = new DateTimeOffset(2026, 6, 11, 0, 0, 1, TimeSpan.Zero);

        Assert.True(OccDateRangeFilterHelper.IsWithinRange(inside, from, to));
        Assert.False(OccDateRangeFilterHelper.IsWithinRange(before, from, to));
        Assert.False(OccDateRangeFilterHelper.IsWithinRange(after, from, to));
    }

    [Fact]
    public void BuildDailySeriesForRange_ReturnsOrderedPoints()
    {
        var sent = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["2026-06-08"] = 2,
            ["2026-06-09"] = 4
        };
        var received = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["2026-06-08"] = 1,
            ["2026-06-09"] = 3
        };

        var localOffset = DateTimeOffset.Now.Offset;
        var from = OccDateRangeFilterState.NormalizeStartOfDay(
            new DateTimeOffset(2026, 6, 8, 0, 0, 0, localOffset))!.Value;
        var to = OccDateRangeFilterState.NormalizeEndOfDay(
            new DateTimeOffset(2026, 6, 9, 0, 0, 0, localOffset))!.Value;
        var series = OccDateRangeFilterHelper.BuildDailySeriesForRange(sent, received, from, to);

        Assert.Equal(2, series.Count);
        Assert.Equal(2, series[0].Sent);
        Assert.Equal(4, series[1].Sent);
        Assert.Equal(3, series[1].Received);
    }

    [Fact]
    public void ResetToDefaultWindow_SetsSevenDayInclusiveRange()
    {
        var filter = OccDateRangeFilterState.CreateForTests();
        filter.FromUtc = DateTimeOffset.Now.AddDays(-30);
        filter.ToUtc = DateTimeOffset.Now.AddDays(-20);

        filter.ResetToDefaultWindow();

        Assert.True(filter.HasActiveFilter);
        Assert.NotNull(filter.FromUtc);
        Assert.NotNull(filter.ToUtc);
        var spanDays = (filter.ToUtc!.Value.Date - filter.FromUtc!.Value.Date).Days + 1;
        Assert.Equal(OccDateRangeFilterState.DefaultWindowDays, spanDays);
    }

    [Fact]
    public void Clear_ResetsToDefaultWindow()
    {
        var filter = OccDateRangeFilterState.CreateForTests();
        filter.FromUtc = DateTimeOffset.Now.AddDays(-30);
        filter.ToUtc = DateTimeOffset.Now.AddDays(-20);

        filter.Clear();

        Assert.True(filter.HasActiveFilter);
        var spanDays = (filter.ToUtc!.Value.Date - filter.FromUtc!.Value.Date).Days + 1;
        Assert.Equal(OccDateRangeFilterState.DefaultWindowDays, spanDays);
    }

    [Fact]
    public void FormatScopeLabel_AppendsModeAndRangeToBranchScope()
    {
        var from = new DateTimeOffset(2026, 6, 7, 0, 0, 0, DateTimeOffset.Now.Offset);
        var to = new DateTimeOffset(2026, 6, 13, 0, 0, 0, DateTimeOffset.Now.Offset);

        var label = OccDateRangeFilterHelper.FormatScopeLabel(
            "Showing: All Branches",
            from,
            to,
            OccViewMode.Live);

        Assert.StartsWith("Showing: All Branches · Live workload ·", label, StringComparison.Ordinal);
        Assert.Contains("Jun", label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExceedsChartDisplayCap_DetectsRangesBeyondThirtyOneDays()
    {
        var from = DateTimeOffset.Now.AddDays(-40);
        var to = DateTimeOffset.Now;

        Assert.True(OccDateRangeFilterHelper.ExceedsChartDisplayCap(from, to));
        Assert.False(OccDateRangeFilterHelper.ExceedsChartDisplayCap(
            DateTimeOffset.Now.AddDays(-6),
            DateTimeOffset.Now));
    }

    [Fact]
    public void ParseMessageKind_MapsTelemetryKinds()
    {
        Assert.Equal(Models.InboundMessageKind.Catalog, WhatsAppIngressHandler.ParseMessageKind("catalog"));
        Assert.Equal(Models.InboundMessageKind.Audio, WhatsAppIngressHandler.ParseMessageKind("audio"));
    }

    [Fact]
    public void CollectBranchKeys_IgnoresOrphanThreadBranches()
    {
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "dha",
                DisplayName = "Depilex DHA-2",
                Platform = "whatsappbusiness",
                Category = WorkspaceCategory.Professional
            }
        };

        var threads = new[]
        {
            new ThreadData
            {
                ThreadId = "orphan",
                InstanceId = "missing-instance",
                Platform = "whatsappbusiness",
                BranchName = "F-11",
                ConversationKey = "orphan",
                CustomerName = "Orphan",
                LastMessageTime = DateTimeOffset.UtcNow
            },
            new ThreadData
            {
                ThreadId = "dha|active",
                InstanceId = "dha",
                Platform = "whatsappbusiness",
                BranchName = "DHA-2",
                ConversationKey = "active",
                CustomerName = "Active",
                LastMessageTime = DateTimeOffset.UtcNow
            }
        };

        var keys = BranchWorkspaceHelper.CollectBranchKeys(instances, threads);

        Assert.Single(keys);
        Assert.Equal("DHA-2", keys[0]);
    }

    [Fact]
    public void HandleTelemetry_DoesNotIncrementAnalyticsCounts()
    {
        var instanceId = $"telemetry-{Guid.NewGuid():N}";
        var instance = new MessengerInstance
        {
            Id = instanceId,
            DisplayName = "Telemetry Test",
            Platform = "whatsappbusiness"
        };

        var payload = JsonSerializer.Serialize(new
        {
            type = "whatsapp-telemetry",
            conversationKey = "customer-1",
            customerName = "Customer",
            lastReceivedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            lastReceivedKind = "text",
            lastSentAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            lastSentKind = "text"
        });

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var receivedBefore = MessageAnalyticsService.Instance.GetReceivedCount(instanceId);
        var sentBefore = MessageAnalyticsService.Instance.GetSentCount(instanceId);

        Assert.True(WhatsAppIngressHandler.TryHandle("whatsapp-telemetry", root, instance));
        Assert.True(WhatsAppIngressHandler.TryHandle("whatsapp-telemetry", root, instance));

        Assert.Equal(receivedBefore, MessageAnalyticsService.Instance.GetReceivedCount(instanceId));
        Assert.Equal(sentBefore, MessageAnalyticsService.Instance.GetSentCount(instanceId));
    }
}
