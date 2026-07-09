using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Locks the oversight-snapshot persistence: a captured snapshot survives a restart (load), so the
/// command center shows last-known numbers immediately instead of going blank until the next scan.
/// </summary>
public class OversightSnapshotPersistenceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public OversightSnapshotPersistenceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "oversight-snapshot.json");
    }

    private static OversightChatSnapshotService.ChatEntry Chat(string key, int unread, bool awaiting) =>
        new(key, "Customer", unread, DateTimeOffset.UtcNow.AddMinutes(-5), "preview", awaiting, false, "923000000000");

    [Fact]
    public async Task Snapshot_SurvivesReload()
    {
        var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        var writer = new OversightChatSnapshotService(_storePath);
        writer.Update("inst-1", [Chat("a@c.us", 2, true), Chat("b@c.us", 0, false)], capturedAt);
        await writer.FlushAsync();

        // Simulate a restart: a fresh service instance reading the same file.
        var reader = new OversightChatSnapshotService(_storePath);
        await reader.LoadAsync();

        Assert.True(reader.TryGetWindowed("inst-1", null, out var active, out var caughtUp));
        Assert.Equal(2, active);
        Assert.Equal(1, caughtUp); // b is caught up, a is awaiting
        Assert.Single(reader.GetAwaiting("inst-1", null));
        Assert.NotNull(reader.LastCapturedUtc);
    }

    [Fact]
    public async Task Update_AfterReload_ReplacesInstanceWithFreshScan()
    {
        var writer = new OversightChatSnapshotService(_storePath);
        writer.Update("inst-1", [Chat("a@c.us", 5, true)], DateTimeOffset.UtcNow.AddMinutes(-10));
        await writer.FlushAsync();

        var reader = new OversightChatSnapshotService(_storePath);
        await reader.LoadAsync();
        // A fresh scan confirms an outbound reply (LastMessageFromMe = true), which clears awaiting.
        // (A bare unread→0 would stay sticky-awaiting, since opening a chat isn't replying.)
        var replied = new OversightChatSnapshotService.ChatEntry(
            "a@c.us", "Customer", 0, DateTimeOffset.UtcNow, "thanks!", IsAwaiting: false,
            LastMessageFromMe: true, ContactPhone: "923000000000");
        reader.Update("inst-1", [replied], DateTimeOffset.UtcNow);

        Assert.Empty(reader.GetAwaiting("inst-1", null));
    }

    [Fact]
    public async Task LoadAsync_RecoversFromCorruptFile()
    {
        await File.WriteAllTextAsync(_storePath, "{ not valid json");

        var service = new OversightChatSnapshotService(_storePath);
        await service.LoadAsync();

        Assert.Null(service.LastCapturedUtc);
        Assert.NotEmpty(Directory.GetFiles(_tempDirectory, "oversight-snapshot.json.corrupt-*.bak"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
