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
    public async Task GatedMutators_DoNotReenterTheGate_NoDeadlock()
    {
        // Regression: the async mutators take _gate then looked up the instance via the public, gate-taking
        // FindById — re-entering the non-reentrant SemaphoreSlim and deadlocking ("Remove instance gets
        // stuck", and the registry tests that "hang"). Each mutator must complete; a 10s cap fails loudly
        // instead of hanging the test run if the deadlock ever returns.
        var registry = new InstanceRegistryService(_storePath);
        var a = await registry.AddInstanceAsync("A", "whatsapp", null, WorkspaceCategory.Professional);
        var b = await registry.AddInstanceAsync("B", "whatsapp", null, WorkspaceCategory.Professional);

        async Task Exercise()
        {
            await registry.UpdateInstanceCategoryAsync(a.Id, WorkspaceCategory.Personal);
            await registry.UpdateInstanceDisplayNameAsync(a.Id, "A renamed");
            await registry.UpdateInstanceBranchKeyAsync(b.Id, "DHA-2");
            await registry.UpdateInstanceNotificationsMutedAsync(b.Id, true);
            await registry.UpdateInstanceMemoryTierAsync(b.Id, MemoryTierPreference.Low);
            await registry.MoveInstanceAsync(b.Id, -1);
            await registry.RemoveFromSidebarAsync(a.Id);
            await registry.RemovePermanentlyAsync(b.Id);
        }

        await Exercise().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Empty(registry.Instances);
    }

    [Fact]
    public async Task UpdateInstanceMetadataAsync_UpdatesPlatformAndNotes()
    {
        var registry = new InstanceRegistryService(_storePath);
        var instance = await registry.AddInstanceAsync("Work", "whatsapp", null);

        await registry.UpdateInstanceMetadataAsync(
            instance.Id,
            "Work Business",
            "https://web.whatsapp.com/",
            "whatsappbusiness",
            "Primary support channel");

        var updated = registry.FindById(instance.Id);
        Assert.NotNull(updated);
        Assert.Equal("Work Business", updated!.DisplayName);
        Assert.Equal("whatsappbusiness", updated.Platform);
        Assert.Equal("Primary support channel", updated.Notes);
        Assert.StartsWith("https://", updated.StartUrl);
    }

    [Fact]
    public async Task UpdateInstanceMetadataAsync_PersistsBranchKey()
    {
        var registry = new InstanceRegistryService(_storePath);
        var instance = await registry.AddInstanceAsync("Inbox", "whatsappbusiness", null);

        await registry.UpdateInstanceMetadataAsync(
            instance.Id,
            "Business Inbox",
            "https://web.whatsapp.com/",
            "whatsappbusiness",
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
                    Platform = "whatsappbusiness",
                    StartUrl = "https://web.whatsapp.com/",
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
    public async Task UpdateInstanceMemoryTierAsync_PersistsTier()
    {
        var registry = new InstanceRegistryService(_storePath);
        var instance = await registry.AddInstanceAsync("Work", "whatsapp", null);
        Assert.Equal(MemoryTierPreference.Normal, instance.MemoryTier);

        await registry.UpdateInstanceMemoryTierAsync(instance.Id, MemoryTierPreference.Low);

        var updated = registry.FindById(instance.Id);
        Assert.NotNull(updated);
        Assert.Equal(MemoryTierPreference.Low, updated!.MemoryTier);

        var reloaded = new InstanceRegistryService(_storePath);
        await reloaded.LoadAsync();
        var persisted = reloaded.FindById(instance.Id);
        Assert.NotNull(persisted);
        Assert.Equal(MemoryTierPreference.Low, persisted!.MemoryTier);
    }

    [Fact]
    public async Task LoadAsync_PersistsPerCategorySortOrder()
    {
        var registry = new InstanceRegistryService(_storePath);
        await registry.AddInstanceAsync("Personal A", "whatsapp", null);
        await registry.AddInstanceAsync("Personal B", "whatsappbusiness", null);
        await registry.AddInstanceAsync("Pro A", "whatsapp", null, WorkspaceCategory.Professional);

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
