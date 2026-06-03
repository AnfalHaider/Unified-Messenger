using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class InstanceRegistryServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public InstanceRegistryServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "instances.json");
    }

    [Fact]
    public async Task ReorderInstanceBeforeAsync_MovesSourceBeforeTarget()
    {
        var registry = new InstanceRegistryService(_storePath);
        await registry.AddInstanceAsync("First", "whatsapp", null);
        await registry.AddInstanceAsync("Second", "telegram", null);
        var third = await registry.AddInstanceAsync("Third", "messenger", null);
        var first = registry.Instances.First(i => i.DisplayName == "First");

        await registry.ReorderInstanceBeforeAsync(third.Id, first.Id);

        var ordered = registry.GetOrderedInstances().Select(i => i.DisplayName).ToList();
        Assert.Equal(["Third", "First", "Second"], ordered);
    }

    [Fact]
    public async Task UpdateInstanceMetadataAsync_UpdatesPlatformAndNotes()
    {
        var registry = new InstanceRegistryService(_storePath);
        var instance = await registry.AddInstanceAsync("Work", "whatsapp", null);

        await registry.UpdateInstanceMetadataAsync(
            instance.Id,
            "Work Slack",
            "https://app.slack.com/client/T123",
            "slack",
            "Primary support channel");

        var updated = registry.FindById(instance.Id);
        Assert.NotNull(updated);
        Assert.Equal("Work Slack", updated!.DisplayName);
        Assert.Equal("slack", updated.Platform);
        Assert.Equal("Primary support channel", updated.Notes);
        Assert.StartsWith("https://", updated.StartUrl);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
