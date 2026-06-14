using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection("ThreadDisplayOrder")]
public class UnifiedMessengerDashboardDisplayOrderTests
{
    public UnifiedMessengerDashboardDisplayOrderTests()
    {
        ThreadDisplayOrderService.Instance.ResetForTests();
        ThreadDisplayOrderService.Instance.SuppressPersistence = true;
    }

    [Fact]
    public void BuildSnapshot_AppliesManualKanbanOrderWithinColumn()
    {
        var now = DateTimeOffset.UtcNow;
        var threads = new[]
        {
            CreateThread("t-new-1", now.AddMinutes(-1), isReplied: false, isLeakage: false),
            CreateThread("t-new-2", now.AddMinutes(-5), isReplied: false, isLeakage: false),
            CreateThread("t-new-3", now.AddMinutes(-10), isReplied: false, isLeakage: false)
        };

        ThreadRegistryService.Instance.RestoreThreads(threads);
        ThreadRegistryService.Instance.RefreshOperationalFlags();

        var columnKey = ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries);
        ThreadDisplayOrderService.Instance.UpdateColumnOrder(columnKey, ["t-new-3", "t-new-1", "t-new-2"]);

        var snapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot(
        [
            new MessengerInstance
            {
                Id = "inst-1",
                Category = WorkspaceCategory.Professional,
                Platform = "whatsapp",
                DisplayName = "Meta"
            }
        ]);

        var orderedIds = snapshot.AllThreads
            .Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.NewInquiries)
            .Select(thread => thread.ThreadId)
            .ToList();

        Assert.Equal(["t-new-3", "t-new-1", "t-new-2"], orderedIds);
    }

    private static ThreadData CreateThread(
        string threadId,
        DateTimeOffset lastMessageTime,
        bool isReplied,
        bool isLeakage) =>
        new()
        {
            ThreadId = threadId,
            InstanceId = "inst-1",
            Platform = "whatsapp",
            CustomerName = threadId,
            LastMessageTime = lastMessageTime,
            IsReplied = isReplied,
            IsRevenueLeakageRisk = isLeakage
        };
}
