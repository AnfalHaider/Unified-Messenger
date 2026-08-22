using System.Diagnostics;
using UnifiedMessenger.Services.Ai;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Services;

/// <summary>
/// Coordinates hide-to-tray vs quit behavior and durable state flush while WebView sessions stay warm.
/// </summary>
public static class ApplicationLifecycleService
{
    private static readonly TimeSpan WorkerShutdownTimeout = TimeSpan.FromSeconds(2);
    private static int _shutdownStarted;

    public static bool ShouldHideOnClose(bool forceShutdown, bool runInBackgroundOnClose) =>
        !forceShutdown && runInBackgroundOnClose;

    public static void FlushPersistentStateFireAndForget() =>
        _ = FlushPersistentStateAsync();

    public static void TryShutdownOnWindowClosed(bool forceShutdown, bool runInBackgroundOnClose)
    {
        if (!forceShutdown && runInBackgroundOnClose)
        {
            return;
        }

        try
        {
            ShutdownAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lifecycle shutdown on close failed: {ex.Message}");
            AppLogger.LogError("Lifecycle.Shutdown", ex);
        }
    }

    public static async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
        {
            return;
        }

        var services = TryGetServices();

        try
        {
            services.MessageTriage.Shutdown();
            services.AiInferenceQueue.Shutdown();
            services.StateSync.Shutdown();
            BackfillSyncManager.Instance.Shutdown();

            await services.MessageTriage
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
            await services.AiInferenceQueue
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
            await services.StateSync
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lifecycle worker shutdown failed: {ex.Message}");
            AppLogger.LogError("Lifecycle", ex);
        }

        await FlushPersistentStateAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var sessionManager = ApplicationServiceProvider.IsInitialized
                ? ApplicationServiceProvider.Current.SessionManager
                : InstanceSessionManager.Instance;
            await sessionManager.CloseAllSessionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lifecycle WebView session close failed: {ex.Message}");
            AppLogger.LogError("Lifecycle.Sessions", ex);
        }

        try
        {
            services.OllamaRuntime.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lifecycle Ollama shutdown failed: {ex.Message}");
            AppLogger.LogError("Lifecycle.Ollama", ex);
        }
    }

    /// <summary>
    /// Names of the stores that failed to persist during the most recent flush, or an empty list when the
    /// last flush wrote everything. Read this on the next launch to tell the user their state may be stale.
    /// </summary>
    public static IReadOnlyList<string> LastFlushFailures { get; private set; } = [];

    /// <summary>
    /// Persists every durable store, isolating each one so a single failure cannot discard the rest.
    /// </summary>
    /// <remarks>
    /// Each store gets its own try/catch on purpose. These previously shared one try block, so the first
    /// throw unwound past every remaining flush and the app still exited reporting success — silently
    /// losing awaiting-overrides, response-time history and KPI trends. Persistence is the canonical place
    /// where best-effort-per-item beats fail-fast: a store that cannot be written is not a reason to
    /// abandon the six that can. Add new stores to the array below and they inherit the isolation.
    /// </remarks>
    public static async Task FlushPersistentStateAsync(CancellationToken cancellationToken = default)
    {
        var services = TryGetServices();

        (string Name, Func<CancellationToken, Task> Flush)[] stores =
        [
            ("MessageAnalytics", services.MessageAnalytics.FlushAsync),
            ("TriagePersistence", services.TriagePersistence.FlushAsync),
            ("OversightChatSnapshot", OversightChatSnapshotService.Instance.FlushAsync),
            ("ResponseTimeTracker", ResponseTimeTracker.Instance.FlushAsync),
            ("ContactHistory", ContactHistoryStore.Instance.FlushAsync),
            ("AwaitingOverrides", AwaitingOverrideStore.Instance.FlushAsync),
            ("KpiTrends", KpiTrendStore.Instance.FlushAsync),
            ("ReviewHistory", ReviewHistoryStore.Instance.FlushAsync),
            ("ReviewAsks", ReviewAskStore.Instance.FlushAsync)
        ];

        LastFlushFailures = await FlushStoresAsync(stores, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs every flush delegate, isolating failures. Returns the names that failed, in order.
    /// </summary>
    /// <remarks>
    /// Split out from <see cref="FlushPersistentStateAsync"/> purely so the isolation guarantee is
    /// testable without standing up the seven real singleton stores and their file IO.
    /// </remarks>
    /// <param name="logFailure">
    /// Where a per-store failure is recorded. Defaults to <see cref="AppLogger"/>. Tests pass a no-op:
    /// <see cref="AppLogger"/> writes to a fixed path under the real user-data root, so exercising this
    /// method with deliberately-throwing fake stores was appending genuine-looking <c>[ERR]</c> lines —
    /// "Lifecycle.Flush.third", "Lifecycle.Flush.a" — to the user's own app.log. That is the one file
    /// support and the owner are told to consult, so filling it with failures that never happened to them
    /// undermines the diagnostics the rest of this audit added.
    /// </param>
    internal static async Task<IReadOnlyList<string>> FlushStoresAsync(
        IReadOnlyList<(string Name, Func<CancellationToken, Task> Flush)> stores,
        CancellationToken cancellationToken = default,
        Action<string, Exception>? logFailure = null)
    {
        ArgumentNullException.ThrowIfNull(stores);

        logFailure ??= static (scope, ex) => AppLogger.LogError(scope, ex);

        List<string>? failed = null;

        foreach (var (name, flush) in stores)
        {
            try
            {
                await flush(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Deliberately swallowed per store: shutdown must continue so the remaining stores still
                // get their chance to persist. Cancellation is recorded too — a cancelled flush is data
                // that did not reach disk, which is exactly what the user needs warning about.
                Debug.WriteLine($"Lifecycle flush failed for {name}: {ex.Message}");
                logFailure($"Lifecycle.Flush.{name}", ex);
                (failed ??= []).Add(name);
            }
        }

        return failed is null ? [] : failed;
    }

    private static LifecycleServices TryGetServices()
    {
        if (ApplicationServiceProvider.IsInitialized)
        {
            var root = ApplicationServiceProvider.Current;
            return new LifecycleServices(
                root.MessageTriage as MessageTriageService ?? MessageTriageService.Instance,
                root.AiInferenceQueue,
                root.OllamaRuntime,
                root.StateSync,
                root.MessageAnalytics,
                root.TriagePersistence);
        }

        return new LifecycleServices(
            MessageTriageService.Instance,
            AiInferenceQueue.Instance,
            OllamaRuntimeService.Instance,
            UnifiedMessengerStateSyncService.Instance,
            MessageAnalyticsService.Instance,
            TriagePersistenceService.Instance);
    }

    private readonly record struct LifecycleServices(
        MessageTriageService MessageTriage,
        AiInferenceQueue AiInferenceQueue,
        OllamaRuntimeService OllamaRuntime,
        UnifiedMessengerStateSyncService StateSync,
        IMessageAnalyticsService MessageAnalytics,
        TriagePersistenceService TriagePersistence);
}
