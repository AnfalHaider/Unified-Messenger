using System.Text.Json;
using System.Text.Json.Serialization;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class InstanceRegistryServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

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

    [Fact]
    public async Task UpdateInstanceMetadataAsync_PersistsBranchKey()
    {
        var registry = new InstanceRegistryService(_storePath);
        var instance = await registry.AddInstanceAsync("Inbox", "metabusiness", null);

        await registry.UpdateInstanceMetadataAsync(
            instance.Id,
            "Meta Inbox",
            "https://business.facebook.com",
            "metabusiness",
            notes: null,
            branchKey: "DHA-2");

        var updated = registry.FindById(instance.Id);
        Assert.NotNull(updated);
        Assert.Equal("DHA-2", updated!.BranchKey);
        Assert.Equal("DHA-2", BranchWorkspaceHelper.ResolveBranchKey(updated));
    }

    [Fact]
    public async Task LoadAsync_RecoversFromCorruptJson()
    {
        await File.WriteAllTextAsync(_storePath, "{ not valid json");

        var registry = new InstanceRegistryService(_storePath);
        await registry.LoadAsync();

        Assert.NotEmpty(registry.Instances);
        Assert.NotEmpty(Directory.GetFiles(_tempDirectory, "instances.json.corrupt-*.bak"));
    }

    [Fact]
    public async Task ImportInstancesAsync_RepairsDuplicateProfileNames()
    {
        var importPath = Path.Combine(_tempDirectory, "import.json");
        var store = new InstanceStore
        {
            Version = InstanceStore.CurrentVersion,
            Instances =
            [
                new MessengerInstance
                {
                    Id = "a",
                    DisplayName = "One",
                    ProfileName = "shared-profile",
                    Platform = "whatsapp",
                    StartUrl = "https://web.whatsapp.com/",
                    SortOrder = 1
                },
                new MessengerInstance
                {
                    Id = "b",
                    DisplayName = "Two",
                    ProfileName = "shared-profile",
                    Platform = "telegram",
                    StartUrl = "https://web.telegram.org/",
                    SortOrder = 2
                }
            ]
        };

        await File.WriteAllTextAsync(importPath, JsonSerializer.Serialize(store, JsonOptions));

        var registry = new InstanceRegistryService(_storePath);
        var result = await registry.ImportInstancesAsync(importPath);

        Assert.Equal(2, result.ActiveCount);
        var profiles = registry.Instances.Select(i => i.ProfileName).ToList();
        Assert.Equal(2, profiles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task RestoreArchivedInstanceAsync_AssignsSortOrder()
    {
        var registry = new InstanceRegistryService(_storePath);
        var instance = await registry.AddInstanceAsync("Archive Me", "whatsapp", null);
        await registry.RemoveFromSidebarAsync(instance.Id);

        var restored = await registry.RestoreArchivedInstanceAsync(instance.Id);

        Assert.True(restored.SortOrder > 0);
        Assert.NotNull(registry.FindById(instance.Id));
    }

    [Fact]
    public async Task LoadAsync_PersistsPerCategorySortOrder()
    {
        var registry = new InstanceRegistryService(_storePath);
        await registry.AddInstanceAsync("Personal A", "whatsapp", null);
        await registry.AddInstanceAsync("Personal B", "telegram", null);
        await registry.AddInstanceAsync("Pro A", "slack", null, WorkspaceCategory.Professional);

        var reloaded = new InstanceRegistryService(_storePath);
        await reloaded.LoadAsync();

        var personalOrders = reloaded.Instances
            .Where(i => !i.IsProfessional)
            .Select(i => i.SortOrder)
            .ToList();
        var proOrders = reloaded.Instances
            .Where(i => i.IsProfessional)
            .Select(i => i.SortOrder)
            .ToList();

        Assert.Equal([1, 2], personalOrders);
        Assert.Equal([1], proOrders);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
