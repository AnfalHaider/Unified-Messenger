using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection("ThreadDisplayOrder")]
public class ThreadDisplayOrderPersistenceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public ThreadDisplayOrderPersistenceTests()
    {
        ThreadDisplayOrderService.Instance.ResetForTests();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, RichTriageStoreService.FileName);
    }

    [Fact]
    public async Task SaveAsync_PersistsDisplayOrders()
    {
        ThreadDisplayOrderService.Instance.ResetForTests();
        var columnKey = ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries);
        ThreadDisplayOrderService.Instance.SuppressPersistence = true;
        ThreadDisplayOrderService.Instance.UpdateColumnOrder(columnKey, ["thread-a", "thread-b"]);

        var store = new RichTriageStoreService(_storePath);
        await store.SaveSnapshotForTestsAsync([]);

        var loaded = await RichTriageStoreService.ReadStoreForTestsAsync(_storePath);
        Assert.NotNull(loaded);
        Assert.Equal(RichTriageStoreFile.CurrentVersion, loaded!.Version);
        Assert.Contains(
            loaded.DisplayOrders,
            entry => entry.ThreadId == "thread-a" && entry.ColumnKey == columnKey && entry.SortIndex == 0);
    }

    public void Dispose()
    {
        ThreadDisplayOrderService.Instance.ResetForTests();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
