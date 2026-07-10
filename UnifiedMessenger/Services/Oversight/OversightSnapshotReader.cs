using System.Collections.Concurrent;
using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Runs WhatsApp Web's IndexedDB chat-store scan on an instance (start + poll, since ExecuteScriptAsync
/// doesn't await promises) and updates <see cref="OversightChatSnapshotService"/>. Shared by the command
/// center's Re-sync probe and the background <see cref="OversightAlertMonitor"/>.
/// </summary>
public static class OversightSnapshotReader
{
    public readonly record struct RefreshResult(int Active, int CaughtUp, int Awaiting);

    // One scan per instance at a time — the manual Re-sync probe and the background monitor share the
    // single window.__umDbConversationsResult global, so concurrent scans would clobber each other.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<RefreshResult?> RefreshAsync(MessengerInstance instance, bool harvestPreviews = false)
    {
        if (instance is null || string.IsNullOrWhiteSpace(instance.Id))
        {
            return null;
        }

        var gate = Gates.GetOrAdd(instance.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (harvestPreviews)
            {
                await HarvestPreviewsAsync(instance).ConfigureAwait(false);
            }

            return await RunScanAsync(instance).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Scrolls the sidebar to harvest last-message previews for off-screen chats into a persistent JS map
    /// the scan reads. Best-effort: bounded poll, never throws. Message bodies are encrypted at rest, so the
    /// rendered DOM is the only plaintext preview source.
    /// </summary>
    private static async Task HarvestPreviewsAsync(MessengerInstance instance)
    {
        // After a WebView reload the chat list isn't rendered yet; wait for sidebar rows before harvesting
        // (the harvest is a synchronous read of currently-rendered rows). Bounded so it never hangs.
        for (var w = 0; w < 50; w++) // up to ~25s
        {
            var count = await InstanceConnection.Current
                .ExecuteScriptAsync(
                    instance.Id,
                    "(document.querySelectorAll('#pane-side [role=\"row\"], #side [role=\"row\"], [data-testid=\"chat-list\"] [role=\"row\"]').length || 0).toString()")
                .ConfigureAwait(false);

            var raw = count?.Trim('"');
            if (int.TryParse(raw, out var n) && n > 0)
            {
                break;
            }
            await Task.Delay(500).ConfigureAwait(false);
        }

        var started = await InstanceConnection.Current
            .ExecuteScriptAsync(
                instance.Id,
                "window.__umStartPreviewHarvest ? window.__umStartPreviewHarvest() : 'NOFN'")
            .ConfigureAwait(false);

        if (started is null || started.Contains("NOFN", StringComparison.Ordinal))
        {
            return;
        }

        for (var attempt = 0; attempt < 50; attempt++) // ~12.5s, matching the JS watchdog
        {
            await Task.Delay(250).ConfigureAwait(false);
            var done = await InstanceConnection.Current
                .ExecuteScriptAsync(
                    instance.Id,
                    "window.__umIsPreviewHarvestDone ? window.__umIsPreviewHarvestDone() : 'true'")
                .ConfigureAwait(false);

            if (done is not null && done.Contains("true", StringComparison.Ordinal))
            {
                return;
            }
        }
    }

    private static async Task<RefreshResult?> RunScanAsync(MessengerInstance instance)
    {
        await InstanceConnection.Current
            .ExecuteScriptAsync(
                instance.Id,
                "window.__umStartDbConversationScan ? window.__umStartDbConversationScan(2000) : 'NOFN'")
            .ConfigureAwait(false);

        for (var attempt = 0; attempt < 75; attempt++) // ~22s; the scan self-settles via a 20s watchdog
        {
            await Task.Delay(300).ConfigureAwait(false);
            var raw = await InstanceConnection.Current
                .ExecuteScriptAsync(
                    instance.Id,
                    "window.__umGetDbConversationResult ? window.__umGetDbConversationResult() : 'NOFN'")
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw) || raw == "null" || raw == "\"\"")
            {
                continue; // not settled yet
            }

            if (raw.Contains("NOFN", StringComparison.Ordinal))
            {
                return null; // scan function not injected
            }

            try
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Deserialize<string>(raw) ?? "");
                var root = doc.RootElement;
                var stage = root.TryGetProperty("diag", out var diag) && diag.TryGetProperty("stage", out var s)
                    ? s.GetString()
                    : null;

                if (stage != "done")
                {
                    return null; // settled but unusable (e.g. watchdog-timeout) — the account is still loading
                }

                var chats = ParseChatEntries(root);
                OversightChatSnapshotService.Instance.Update(instance.Id, chats, DateTimeOffset.UtcNow);

                var active = chats.Count;
                var caughtUp = chats.Count(c => c.Unread <= 0);
                return new RefreshResult(active, caughtUp, active - caughtUp);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static List<OversightChatSnapshotService.ChatEntry> ParseChatEntries(JsonElement root) =>
        ChatEntryParser.ParseConversations(root);
}
