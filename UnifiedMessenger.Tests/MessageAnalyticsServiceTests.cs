using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class MessageAnalyticsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;
    private readonly int _originalSlaThreshold;

    public MessageAnalyticsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "analytics.json");
        _originalSlaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
    }

    [Fact]
    public async Task LoadAsync_RecoversFromCorruptJson()
    {
        await File.WriteAllTextAsync(_storePath, "{ invalid analytics");

        var service = new MessageAnalyticsService(_storePath);
        await service.LoadAsync();

        Assert.Equal(0, service.GetSentCount("any"));
        Assert.NotEmpty(Directory.GetFiles(_tempDirectory, "analytics.json.corrupt-*.bak"));
    }

    [Fact]
    public void RecalculateSlaBreaches_UsesCurrentThreshold()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.SetReplyLatenciesForTests("inst-1", 30, 10, 45);

        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;
        service.RecalculateSlaBreaches();
        Assert.Equal(2, service.GetSlaBreachCount("inst-1"));

        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 60;
        service.RecalculateSlaBreaches();
        Assert.Equal(0, service.GetSlaBreachCount("inst-1"));
    }

    [Fact]
    public async Task ClearAllDataAsync_RemovesPersistedFile()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("inst-1");
        await Task.Delay(900);

        await service.ClearAllDataAsync();

        Assert.False(File.Exists(_storePath));
        Assert.Equal(0, service.GetReceivedCount("inst-1"));
    }

    [Fact]
    public async Task ExportFilteredJsonAsync_RespectsInstanceFilter()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("inst-1");
        service.RecordMessageSent("inst-1");
        service.RecordMessageReceived("inst-2");
        service.RecordMessageSent("inst-2");
        service.RecordMessageSent("inst-2");

        var exportPath = Path.Combine(_tempDirectory, "filtered-export.json");
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "inst-1",
                DisplayName = "Support",
                Platform = "whatsapp",
                ProfileName = "whatsapp-support",
                StartUrl = "https://web.whatsapp.com/"
            }
        };

        await service.ExportFilteredJsonAsync(instances, exportPath);

        await using var stream = File.OpenRead(exportPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var instanceIds = document.RootElement
            .GetProperty("instances")
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["inst-1"], instanceIds);
        Assert.Equal(1, document.RootElement.GetProperty("instances").GetProperty("inst-1").GetProperty("receivedCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("instances").GetProperty("inst-1").GetProperty("sentCount").GetInt32());
    }

    [Fact]
    public async Task RecordMessageSent_PersistsConversationKey()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageSent("inst-1", chatHint: "Customer", conversationKey: "120363@s.whatsapp.net");

        var exportPath = Path.Combine(_tempDirectory, "sent-export.json");
        await service.ExportFilteredJsonAsync(
            [
                new MessengerInstance
                {
                    Id = "inst-1",
                    DisplayName = "Support",
                    Platform = "whatsapp"
                }
            ],
            exportPath);

        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("120363@s.whatsapp.net", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportCsvAsync_WritesExpectedHeader()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("inst-1");
        service.RecordMessageSent("inst-1");

        var exportPath = Path.Combine(_tempDirectory, "export.csv");
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "inst-1",
                DisplayName = "Support",
                Platform = "whatsapp",
                ProfileName = "whatsapp-support",
                StartUrl = "https://web.whatsapp.com/"
            }
        };

        await service.ExportCsvAsync(instances, exportPath);

        var lines = await File.ReadAllLinesAsync(exportPath);
        Assert.StartsWith("InstanceId,DisplayName,Platform,Sent,Received,SlaBreaches,AvgReplyMinutes", lines[0]);
        Assert.Equal(2, lines.Length);
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = _originalSlaThreshold;

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
