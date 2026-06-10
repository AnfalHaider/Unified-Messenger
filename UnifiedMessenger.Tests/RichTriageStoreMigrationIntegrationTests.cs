using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class RichTriageStoreMigrationIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public RichTriageStoreMigrationIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, RichTriageStoreService.FileName);
    }

    [Fact]
    public async Task SaveAndLoad_PersistsMetadataAndThreads()
    {
        var store = new RichTriageStoreService(_storePath);
        var item = new MessageTriageItem
        {
            Id = "eli|1",
            InstanceId = "eli",
            InstanceDisplayName = "Depilex Eli-5",
            Platform = "whatsappbusiness",
            MessagePreview = "Bridal package quote",
            CustomerName = "Hina",
            UrgencyScore = 55,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            BranchName = "Eli-5",
            ConversationKey = "Hina"
        };

        ThreadRegistryService.Instance.RestoreThreads([
            new ThreadData
            {
                ThreadId = "eli|Hina",
                Platform = "whatsappbusiness",
                InstanceId = "eli",
                InstanceDisplayName = "Depilex Eli-5",
                BranchName = "Eli-5",
                CustomerName = "Hina",
                ConversationKey = "Hina",
                LastMessageTime = item.TimestampUtc
            }
        ]);

        await store.SaveSnapshotForTestsAsync([item], ThreadRegistryService.Instance.GetAllThreads());

        var loaded = await RichTriageStoreService.ReadStoreForTestsAsync(_storePath);
        Assert.NotNull(loaded);
        Assert.Equal(RichTriageStoreFile.CurrentVersion, loaded!.Version);
        Assert.Contains(loaded.Threads, t => t.ThreadId == "eli|Hina");
        Assert.Contains(loaded.Metadata.Branches, b => b.BranchName == "Eli-5");
    }

    [Fact]
    public async Task SaveAndLoad_PersistsWhatsAppDeliveryStatus()
    {
        var store = new RichTriageStoreService(_storePath);
        var updatedAt = DateTimeOffset.UtcNow;
        var thread = new ThreadData
        {
            ThreadId = "eli|Hina",
            Platform = "whatsappbusiness",
            InstanceId = "eli",
            InstanceDisplayName = "Depilex Eli-5",
            BranchName = "Eli-5",
            CustomerName = "Hina",
            ConversationKey = "Hina",
            WhatsAppDeliveryStatus = WhatsAppDeliveryStatusLabel.Delivered,
            WhatsAppDeliveryUpdatedUtc = updatedAt,
            LastMessageTime = updatedAt
        };

        ThreadRegistryService.Instance.RestoreThreads([thread]);
        await store.SaveSnapshotForTestsAsync([], ThreadRegistryService.Instance.GetAllThreads());

        var loaded = await RichTriageStoreService.ReadStoreForTestsAsync(_storePath);
        var persisted = Assert.Single(loaded!.Threads);
        Assert.Equal(WhatsAppDeliveryStatusLabel.Delivered, persisted.WhatsAppDeliveryStatus);
        Assert.Equal(updatedAt, persisted.WhatsAppDeliveryUpdatedUtc);
    }

    public void Dispose()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
