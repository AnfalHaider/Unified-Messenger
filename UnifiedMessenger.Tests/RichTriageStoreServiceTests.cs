using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class RichTriageStoreServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public RichTriageStoreServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, RichTriageStoreService.FileName);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsItems()
    {
        var triage = new MessageTriageService();
        var store = new RichTriageStoreService(_storePath);
        var seed = CreateItem("branch-a", "Alice", TriageInferenceSource.LocalAi, "Book facial Saturday");

        await store.SaveSnapshotForTestsAsync([seed]);

        var items = await RichTriageStoreService.ReadFileForTestsAsync(_storePath);
        Assert.Single(items);
        Assert.Equal("branch-a", items[0].InstanceId);
        Assert.Equal(TriageInferenceSource.LocalAi, items[0].InferenceSource);
        Assert.True(File.Exists(_storePath));
        Assert.False(File.Exists(_storePath + ".tmp"));
    }

    [Fact]
    public void PruneItems_DropsExpiredAndCapsCount()
    {
        var now = DateTimeOffset.UtcNow;
        var items = Enumerable.Range(0, 250)
            .Select(index => CreateItem(
                $"inst-{index}",
                $"Customer {index}",
                TriageInferenceSource.Heuristic,
                "msg",
                now.AddMinutes(-index)))
            .ToList();

        items.Add(CreateItem("old", "Expired", TriageInferenceSource.Heuristic, "old", now.AddHours(-72)));

        var pruned = RichTriageStoreService.PruneItems(items);

        Assert.Equal(200, pruned.Count);
        Assert.DoesNotContain(pruned, item => item.Id == "old");
    }

    [Fact]
    public async Task LoadAsync_RecoversFromCorruptJson()
    {
        await File.WriteAllTextAsync(_storePath, "{ not valid triage json");

        var triage = new MessageTriageService();
        var store = new RichTriageStoreService(_storePath);
        await store.LoadAsync();

        Assert.Empty(triage.GetAllItems());
        Assert.NotEmpty(Directory.GetFiles(_tempDirectory, "triage_v2.json.corrupt-*.bak"));
    }

    private static MessageTriageItem CreateItem(
        string instanceId,
        string customer,
        TriageInferenceSource source,
        string summary,
        DateTimeOffset? timestampUtc = null) =>
        new()
        {
            Id = $"{instanceId}|{Guid.NewGuid():N}",
            InstanceId = instanceId,
            InstanceDisplayName = instanceId,
            Platform = "metabusiness",
            MessagePreview = summary,
            CustomerName = customer,
            UrgencyScore = 55,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow,
            InferenceSource = source,
            CoreSummary = summary,
            ExtractedEntities = new RichTriageExtractedEntities
            {
                ServiceType = "Facial",
                RequestedDate = "Saturday"
            }
        };

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
