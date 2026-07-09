using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection("ThreadDisplayOrder")]
public class ThreadDisplayOrderServiceTests
{
    public ThreadDisplayOrderServiceTests()
    {
        ThreadDisplayOrderService.Instance.ResetForTests();
        ThreadDisplayOrderService.Instance.SuppressPersistence = true;
    }

    [Fact]
    public void GetSortIndex_ReturnsUnspecifiedForUnknownThread()
    {
        var sortIndex = ThreadDisplayOrderService.Instance.GetSortIndex(
            ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries),
            "missing-thread");

        Assert.Equal(ThreadDisplayOrderService.UnspecifiedSortIndex, sortIndex);
    }

    [Fact]
    public void UpdateColumnOrder_PersistsManualPriority()
    {
        var columnKey = ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.HangingLeads);
        ThreadDisplayOrderService.Instance.UpdateColumnOrder(columnKey, ["t-2", "t-1", "t-3"]);

        Assert.Equal(0, ThreadDisplayOrderService.Instance.GetSortIndex(columnKey, "t-2"));
        Assert.Equal(1, ThreadDisplayOrderService.Instance.GetSortIndex(columnKey, "t-1"));
        Assert.Equal(2, ThreadDisplayOrderService.Instance.GetSortIndex(columnKey, "t-3"));
    }

    [Fact]
    public void SortThreadsForKanbanColumn_FallsBackToLastMessageTime()
    {
        var now = DateTimeOffset.UtcNow;
        var threads = new[]
        {
            CreateThread("a", now.AddMinutes(-5)),
            CreateThread("b", now.AddMinutes(-1)),
            CreateThread("c", now.AddMinutes(-10))
        };

        var columnKey = ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries);
        ThreadDisplayOrderService.Instance.UpdateColumnOrder(columnKey, ["c", "a"]);

        var sorted = ThreadDisplayOrderService.Instance
            .SortThreadsForKanbanColumn(threads, UnifiedMessengerKanbanColumn.NewInquiries)
            .Select(thread => thread.ThreadId)
            .ToList();

        Assert.Equal(["c", "a", "b"], sorted);
    }

    [Fact]
    public void PruneOrphans_RemovesStaleEntries()
    {
        var columnKey = ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.Resolved);
        ThreadDisplayOrderService.Instance.UpdateColumnOrder(columnKey, ["keep", "drop"]);

        ThreadDisplayOrderService.Instance.PruneOrphans(["keep"]);

        Assert.Equal(0, ThreadDisplayOrderService.Instance.GetSortIndex(columnKey, "keep"));
        Assert.Equal(
            ThreadDisplayOrderService.UnspecifiedSortIndex,
            ThreadDisplayOrderService.Instance.GetSortIndex(columnKey, "drop"));
    }

    [Fact]
    public void Export_RoundTripsThroughLoad()
    {
        var columnKey = ThreadDisplayOrderService.ImmediateColumnKey;
        ThreadDisplayOrderService.Instance.UpdateColumnOrder(columnKey, ["x", "y"]);
        var exported = ThreadDisplayOrderService.Instance.Export();

        ThreadDisplayOrderService.Instance.ResetForTests();
        ThreadDisplayOrderService.Instance.Load(exported);

        Assert.Equal(0, ThreadDisplayOrderService.Instance.GetSortIndex(columnKey, "x"));
        Assert.Equal(1, ThreadDisplayOrderService.Instance.GetSortIndex(columnKey, "y"));
    }

    private static ThreadData CreateThread(string threadId, DateTimeOffset lastMessageTime) =>
        new()
        {
            ThreadId = threadId,
            InstanceId = "inst-1",
            Platform = "whatsapp",
            CustomerName = threadId,
            LastMessageTime = lastMessageTime
        };
}
