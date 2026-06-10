using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.Services.Contracts;
using UnifiedMessenger.Services.Ollama;

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
            System.Diagnostics.Debug.WriteLine($"Lifecycle shutdown on close failed: {ex.Message}");
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
            services.Backfill.Shutdown();
            services.MessageTriage.Shutdown();
            services.InsightsEngine.Shutdown();
            services.StateSync.Shutdown();
            services.OllamaInference.Dispose();
            services.Ollama.Dispose();

            await services.MessageTriage
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
            await services.InsightsEngine
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
            await services.StateSync
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lifecycle worker shutdown failed: {ex.Message}");
        }

        await FlushPersistentStateAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task FlushPersistentStateAsync(CancellationToken cancellationToken = default)
    {
        var services = TryGetServices();

        try
        {
            await services.MessageAnalytics.FlushAsync(cancellationToken).ConfigureAwait(false);
            await services.RichTriageStore.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lifecycle flush failed: {ex.Message}");
        }
    }

    private static LifecycleServices TryGetServices()
    {
        if (ApplicationServiceProvider.IsInitialized)
        {
            var root = ApplicationServiceProvider.Current;
            return new LifecycleServices(
                root.Backfill,
                root.MessageTriage as MessageTriageService ?? MessageTriageService.Instance,
                root.InsightsEngine,
                root.StateSync,
                root.OllamaInference,
                root.Ollama,
                root.MessageAnalytics,
                root.RichTriageStore);
        }

        return new LifecycleServices(
            BackfillSyncManager.Instance,
            MessageTriageService.Instance,
            UnifiedMessengerInsightsEngine.Instance,
            UnifiedMessengerStateSyncService.Instance,
            OllamaInferenceCoordinator.Instance,
            OllamaOrchestrationService.Instance,
            MessageAnalyticsService.Instance,
            RichTriageStoreService.Instance);
    }

    private readonly record struct LifecycleServices(
        BackfillSyncManager Backfill,
        MessageTriageService MessageTriage,
        UnifiedMessengerInsightsEngine InsightsEngine,
        UnifiedMessengerStateSyncService StateSync,
        OllamaInferenceCoordinator OllamaInference,
        IOllamaOrchestrationService Ollama,
        IMessageAnalyticsService MessageAnalytics,
        IRichTriageStoreService RichTriageStore);
}
