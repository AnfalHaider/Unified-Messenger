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

        // Only channels that participate in the WhatsApp IndexedDB pipeline have a conversation scraper.
        // Without this gate, callers that filter merely on "professional and connected" — which is what
        // OversightAlertMonitor does — ran the WhatsApp scan against Google Business accounts on every
        // cycle. Observed on real data: three googlebusiness instances logging
        // "Conversation scan function is not injected on this page" indefinitely. They were behaving
        // exactly as designed (Google is reviews + Q&A only, permanently — it has no conversation
        // scraper and never will), so the warning was pure noise, and a log full of routine warnings
        // trains people to ignore the real ones.
        //
        // Returning null WITHOUT recording a read failure is the important part: these accounts are not
        // failing, the scan simply does not apply to them. Recording a failure here would light up
        // "can't read this account — click Re-sync" on three perfectly healthy Google accounts, which is
        // the precise false positive AccountReadHealth was written to avoid.
        if (!PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
        {
            return null;
        }

        var gate = Gates.GetOrAdd(instance.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Preferred path: WhatsApp Web's in-memory model collections. They are already decrypted (so
            // every chat carries a real preview, not just the ~60 rendered sidebar rows) and never lag a
            // reply sent from the phone. Returns null when the bridge can't reach the collections — e.g.
            // WhatsApp changed its module layout, or the page hasn't booted yet — and we fall through.
            if (AppSettingsService.Instance.Settings.UseStoreBridge)
            {
                var viaBridge = await RunStoreBridgeScanAsync(instance).ConfigureAwait(false);
                if (viaBridge is not null)
                {
                    AccountReadHealth.RecordSuccess(instance.Id);
                    return viaBridge;
                }
            }

            // Fallback: the persisted IndexedDB chat store, plus a sidebar-DOM preview harvest (the only
            // plaintext preview source available to that path, since bodies are encrypted at rest).
            if (harvestPreviews)
            {
                await HarvestPreviewsAsync(instance).ConfigureAwait(false);
            }

            var (viaScan, pageNotReady) = await RunScanAsync(instance).ConfigureAwait(false);

            // This is the only place that knows the FINAL outcome — after every fallback has been tried.
            // Recording it here is what lets the command centre tell "this account is quiet" apart from
            // "the app can no longer read this account", which previously rendered identically.
            if (viaScan is not null)
            {
                AccountReadHealth.RecordSuccess(instance.Id);
            }
            else if (!pageNotReady)
            {
                AccountReadHealth.RecordFailure(instance.Id, "No ingestion path returned usable data.");
            }

            // pageNotReady is deliberately neither success nor failure: with lazy WebView loading on (the
            // default), a background account simply has not navigated yet. Recording a failure would show
            // "can't read this account — click Re-sync", and Re-sync cannot load a page that lazy loading
            // intentionally left unloaded — so the advice would be both alarming and useless. Leaving the
            // health state untouched keeps the account reading as "syncing…" until it genuinely loads.
            return viaScan;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Runs the in-memory store-bridge scan. The read itself is synchronous inside the page, but module
    /// discovery can miss while WhatsApp Web is still booting, so this keeps the start/poll shape and
    /// retries briefly. Returns null on any failure so the caller falls back to the IndexedDB scan.
    /// </summary>
    private static async Task<RefreshResult?> RunStoreBridgeScanAsync(MessengerInstance instance)
    {
        for (var attempt = 0; attempt < 6; attempt++) // ~3s of retries while the page finishes booting
        {
            var started = await InstanceConnection.Current
                .ExecuteScriptAsync(
                    instance.Id,
                    "window.__umStartStoreScan ? window.__umStartStoreScan(2000) : 'NOFN'")
                .ConfigureAwait(false);

            if (started is not null && started.Contains("NOFN", StringComparison.Ordinal))
            {
                RecordBridgeFailure(instance.Id, "not-injected");
                return null; // bridge script isn't present on this page at all
            }

            var raw = await InstanceConnection.Current
                .ExecuteScriptAsync(
                    instance.Id,
                    "window.__umGetStoreScanResult ? window.__umGetStoreScanResult() : 'NOFN'")
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw) || raw == "null" || raw == "\"\"")
            {
                await Task.Delay(500).ConfigureAwait(false);
                continue;
            }

            if (raw.Contains("NOFN", StringComparison.Ordinal))
            {
                RecordBridgeFailure(instance.Id, "not-injected");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Deserialize<string>(raw) ?? "");
                var root = doc.RootElement;
                var diag = root.TryGetProperty("diag", out var d) ? d : default;
                var stage = diag.ValueKind == JsonValueKind.Object &&
                            diag.TryGetProperty("stage", out var s)
                    ? s.GetString() ?? "unknown"
                    : "unknown";

                if (stage != "done")
                {
                    // 'no-store' means discovery failed (WhatsApp shape changed, or still booting);
                    // 'empty' means it resolved but found nothing yet. Retry briefly, then fall back.
                    await Task.Delay(500).ConfigureAwait(false);
                    if (attempt == 5)
                    {
                        RecordBridgeFailure(instance.Id, stage);
                    }
                    continue;
                }

                var chats = ChatEntryParser.ParseConversations(root);
                if (chats.Count == 0)
                {
                    RecordBridgeFailure(instance.Id, "parsed-empty");
                    return null;
                }

                OversightChatSnapshotService.Instance.Update(instance.Id, chats, DateTimeOffset.UtcNow);

                StoreBridgeHealth.Record(instance.Id, new StoreBridgeHealth.Entry(
                    Succeeded: true,
                    Stage: stage,
                    Strategy: ReadDiagString(diag, "strategy"),
                    Conversations: chats.Count,
                    WithPreview: ReadDiagInt(diag, "withPreview"),
                    AtUtc: DateTimeOffset.UtcNow));

                var result = OversightChatSnapshotService.Instance.TryGetWindowed(
                    instance.Id, null, out var active, out var caughtUp)
                    ? new RefreshResult(active, caughtUp, active - caughtUp)
                    : new RefreshResult(chats.Count, chats.Count, 0);

                PublishSnapshotEvent(instance, result, "store-bridge");
                return result;
            }
            catch (JsonException)
            {
                RecordBridgeFailure(instance.Id, "parse-error");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"StoreBridge.{instance.Id}", $"Bridge scan failed: {ex.Message}");
                RecordBridgeFailure(instance.Id, "exception");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Announces a fresh read so consumers (dashboard, rollup) can react immediately instead of waiting
    /// for their own redraw timer. Carries the source so a "why is this stale?" question is answerable.
    /// </summary>
    private static void PublishSnapshotEvent(MessengerInstance instance, RefreshResult result, string source) =>
        ChannelEventBus.Instance.Publish(new ChannelSnapshotEvent(
            instance.Id,
            PlatformDefinition.NormalizePlatformId(instance.Platform),
            DateTimeOffset.UtcNow,
            result.Active,
            result.CaughtUp,
            result.Awaiting,
            source));

    private static void RecordBridgeFailure(string instanceId, string stage) =>
        StoreBridgeHealth.Record(instanceId, new StoreBridgeHealth.Entry(
            Succeeded: false,
            Stage: stage,
            Strategy: string.Empty,
            Conversations: 0,
            WithPreview: 0,
            AtUtc: DateTimeOffset.UtcNow));

    private static string ReadDiagString(JsonElement diag, string name) =>
        diag.ValueKind == JsonValueKind.Object &&
        diag.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadDiagInt(JsonElement diag, string name) =>
        diag.ValueKind == JsonValueKind.Object &&
        diag.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number &&
        v.TryGetInt32(out var i)
            ? i
            : 0;

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

    private static async Task<(RefreshResult? Result, bool PageNotReady)> RunScanAsync(MessengerInstance instance)
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
                // Both paths are now unavailable: the store bridge already declined or this is the
                // fallback. Say so — this used to return null in silence.
                AppLogger.LogWarning(
                    $"IndexedDbScan.{instance.Id}",
                    "Conversation scan function is not injected on this page — the account's page has "
                    + "probably not loaded yet. Open the account once to finish loading.");

                // Not injected means the adapter script never ran, which on a lazily-loaded account means
                // the page was never navigated. That is "not loaded", not "broken".
                return (null, true);
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
                    // Distinguish "this page has not finished loading" from "this account is broken".
                    //
                    // With lazy WebView loading on (the default), a background account's page has not
                    // navigated to WhatsApp Web yet, so indexedDB.open blocks and the JS watchdog settles
                    // at 'watchdog-timeout' after 20s — or the model-storage database simply is not there.
                    // Neither means a fault: the account just needs opening once. Telling the owner
                    // "can't read this account — click Re-sync" is wrong advice, because Re-sync cannot
                    // load a page that lazy loading has deliberately not loaded.
                    //
                    // This is the same false-positive class as the Google Business one fixed in v4.99.18,
                    // reached by a different route.
                    var pageNotReady = IsPageNotReadyStage(stage);

                    AppLogger.LogWarning(
                        $"IndexedDbScan.{instance.Id}",
                        pageNotReady
                            ? $"Conversation scan could not run yet (stage '{stage}') — this account's page is "
                              + "not loaded. Open the account once to finish loading."
                            : $"Conversation scan settled at stage '{stage ?? "unknown"}' instead of 'done'; no oversight data was read.");

                    return (null, pageNotReady);
                }

                var chats = ParseChatEntries(root);

                // A scan that completes but yields nothing is ambiguous: it looks identical to a genuinely
                // quiet account, yet it is also exactly what a WhatsApp Web schema change produces. The UI
                // correctly refuses to invent a number here (MeasuredCount stays 0, so the card shows
                // "no activity" and the caught-up tile shows "—" rather than a false 100%), but without
                // this line there would be nothing anywhere to distinguish the two cases.
                if (chats.Count == 0)
                {
                    AppLogger.LogWarning(
                        $"IndexedDbScan.{instance.Id}",
                        "Conversation scan completed but parsed 0 conversations — either the account is empty "
                        + "or the scraped shape no longer matches the parser.");
                }
                OversightChatSnapshotService.Instance.Update(instance.Id, chats, DateTimeOffset.UtcNow);

                // Report the SAME direction-based, sticky, override-aware number the dashboard shows (via
                // TryGetWindowed) — not the raw unread badge. Unread is per-device read-state: reading a chat
                // on the phone clears it without anyone replying, and it lags per linked device, so two installs
                // of the same accounts disagree. Direction (last message fromMe) is message content, so it syncs
                // identically to every device. Update() ran synchronously above, so the snapshot is present.
                var result = OversightChatSnapshotService.Instance.TryGetWindowed(
                    instance.Id, null, out var active, out var caughtUp)
                    ? new RefreshResult(active, caughtUp, active - caughtUp)
                    : new RefreshResult(chats.Count, chats.Count, 0);

                PublishSnapshotEvent(instance, result, "indexeddb");
                return (result, false);
            }
            catch (Exception ex)
            {
                // This was a bare `catch { return null; }`. The IndexedDB scan is the LAST-RESORT path —
                // when it fails the account contributes nothing at all — and it was the one path that
                // recorded no log line and no health entry, so a total oversight failure left no evidence
                // anywhere. The store-bridge path above already reports its failures; this now matches.
                AppLogger.LogWarning(
                    $"IndexedDbScan.{instance.Id}",
                    $"Conversation scan failed: {ex.GetType().Name}: {ex.Message}");

                // A genuine exception IS a failure worth flagging to the owner.
                return (null, false);
            }
        }

        // Polling ran out without the scan ever settling — the page never got far enough to answer.
        return (null, true);
    }

    /// <summary>
    /// True when a settled-but-not-'done' scan stage means "this page has not loaded yet" rather than
    /// "this account is broken".
    /// </summary>
    /// <remarks>
    /// Lazy WebView loading is on by default, so a background account's page has never navigated to
    /// WhatsApp Web. The adapter is therefore absent and <c>indexedDB.open</c> blocks until the JS
    /// watchdog fires at 20 s. Treating that as a failure renders "can't read this account — click
    /// Re-sync", which is wrong advice twice over: nothing is broken, and Re-sync cannot load a page that
    /// lazy loading deliberately left unloaded. The account just needs opening once.
    ///
    /// Stages NOT listed here — <c>no-chat-store</c>, <c>getall-chat-error</c>, <c>chat-exception</c>,
    /// <c>promise-error</c> — mean the page WAS reachable and the read still failed. Those are genuine
    /// faults and must keep flagging.
    /// </remarks>
    internal static bool IsPageNotReadyStage(string? stage) =>
        stage is "watchdog-timeout"     // JS watchdog fired: indexedDB.open never returned
            or "no-model-storage"       // WhatsApp Web's database is absent — the page never loaded it
            or "no-indexeddb"           // no IndexedDB on the page at all
            or "no-databases-api"
            or "databases-rejected";

    public static List<OversightChatSnapshotService.ChatEntry> ParseChatEntries(JsonElement root) =>
        ChatEntryParser.ParseConversations(root);
}
