using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>Serializes test classes that swap the shared <see cref="InstanceConnection.Current"/> static,
/// so xUnit's per-class parallelism can't race them.</summary>
[CollectionDefinition("InstanceConnection")]
public sealed class InstanceConnectionCollection { }

/// <summary>
/// Exercises the oversight chat-store scan through the <see cref="IInstanceConnection"/> abstraction (#26):
/// with a fake connection the start/poll + parse path is testable without a live WebView. Locks the
/// caught-up / awaiting tallies <see cref="OversightSnapshotReader"/> derives from the scan JSON.
/// </summary>
[Collection("InstanceConnection")]
public class OversightSnapshotReaderTests
{
    // Mimics WebView2: a JS string result comes back JSON-encoded, so the reader unwraps with
    // JsonSerializer.Deserialize<string>. The start call's result is ignored; the getter yields the payload.
    private sealed class FakeConnection : IInstanceConnection
    {
        private readonly string _encodedPayload;

        public FakeConnection(string innerJson) => _encodedPayload = JsonSerializer.Serialize(innerJson);

        public Task<string?> ExecuteScriptAsync(string instanceId, string script) =>
            Task.FromResult<string?>(
                script.Contains("__umGetDbConversationResult", StringComparison.Ordinal) ? _encodedPayload : null);

        public Task ReloadAsync(string instanceId) => Task.CompletedTask;
    }

    [Fact]
    public async Task RefreshAsync_ParsesCaughtUpAndAwaitingFromScan()
    {
        const string payload =
            "{\"diag\":{\"stage\":\"done\"},\"conversations\":[" +
            "{\"conversationKey\":\"a@c.us\",\"customerName\":\"A\",\"unreadCount\":0,\"lastActivityTimestampUtc\":\"2026-06-28T10:00:00Z\",\"awaiting\":false}," +
            "{\"conversationKey\":\"b@c.us\",\"customerName\":\"B\",\"unreadCount\":2,\"lastActivityTimestampUtc\":\"2026-06-28T11:00:00Z\",\"awaiting\":true}," +
            "{\"conversationKey\":\"c@c.us\",\"customerName\":\"C\",\"unreadCount\":1,\"lastActivityTimestampUtc\":\"2026-06-28T12:00:00Z\",\"awaiting\":true}]}";

        var original = InstanceConnection.Current;
        InstanceConnection.Current = new FakeConnection(payload);
        try
        {
            var instance = new MessengerInstance { Id = "osr-1", DisplayName = "Reader One", Platform = "whatsapp" };
            var result = await OversightSnapshotReader.RefreshAsync(instance);

            Assert.NotNull(result);
            Assert.Equal(3, result!.Value.Active);
            Assert.Equal(1, result.Value.CaughtUp);   // only the unread==0 chat
            Assert.Equal(2, result.Value.Awaiting);    // active - caughtUp
        }
        finally
        {
            InstanceConnection.Current = original;
        }
    }

    [Fact]
    public async Task RefreshAsync_ScanStillLoading_ReturnsNull()
    {
        // Settled but not "done" (e.g. watchdog timeout) → the account is still loading, no usable tally.
        var original = InstanceConnection.Current;
        InstanceConnection.Current = new FakeConnection("{\"diag\":{\"stage\":\"timeout\"},\"conversations\":[]}");
        try
        {
            var instance = new MessengerInstance { Id = "osr-2", DisplayName = "Reader Two", Platform = "whatsapp" };
            var result = await OversightSnapshotReader.RefreshAsync(instance);
            Assert.Null(result);
        }
        finally
        {
            InstanceConnection.Current = original;
        }
    }
}
