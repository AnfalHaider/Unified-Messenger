using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Locks the "handled elsewhere / snooze" suppression rules: both self-expire so the backlog can't be
/// permanently faked — a handled chat re-appears when a newer message arrives, a snooze lapses on time.
/// </summary>
public class AwaitingOverrideStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;

    public AwaitingOverrideStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "awaiting-overrides.json");
    }

    [Fact]
    public void MarkHandled_SuppressesUntilNewerMessageArrives()
    {
        var store = new AwaitingOverrideStore(_storePath);
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-5);

        store.MarkHandled("inst-1", "chat-a", lastActivity);

        // Same last-activity → still handled (suppressed).
        Assert.True(store.IsSuppressed("inst-1", "chat-a", lastActivity, now));
        // A newer customer message (later activity) → re-appears.
        Assert.False(store.IsSuppressed("inst-1", "chat-a", lastActivity.AddMinutes(10), now));
    }

    [Fact]
    public void Snooze_SuppressesUntilTimePasses()
    {
        var store = new AwaitingOverrideStore(_storePath);
        var now = DateTimeOffset.UtcNow;

        store.Snooze("inst-1", "chat-a", now.AddHours(1));

        Assert.True(store.IsSuppressed("inst-1", "chat-a", now.AddMinutes(-1), now));
        Assert.False(store.IsSuppressed("inst-1", "chat-a", now.AddMinutes(-1), now.AddHours(2)));
    }

    [Fact]
    public void Clear_RemovesOverride()
    {
        var store = new AwaitingOverrideStore(_storePath);
        var now = DateTimeOffset.UtcNow;
        store.MarkHandled("inst-1", "chat-a", now);
        Assert.True(store.IsSuppressed("inst-1", "chat-a", now, now));

        store.Clear("inst-1", "chat-a");
        Assert.False(store.IsSuppressed("inst-1", "chat-a", now, now));
    }

    [Fact]
    public void IsSuppressed_UnknownChat_IsFalse()
    {
        var store = new AwaitingOverrideStore(_storePath);
        Assert.False(store.IsSuppressed("inst-1", "nope", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
