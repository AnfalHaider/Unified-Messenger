using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

/// <summary>
/// Coordinates hide-to-tray vs quit behavior and durable state flush while WebView sessions stay warm.
/// </summary>
public static class ApplicationLifecycleService
{
    private static readonly TimeSpan WorkerShutdownTimeout = TimeSpan.FromSeconds(2);

    public static bool ShouldHideOnClose(bool forceShutdown, bool runInBackgroundOnClose) =>
        !forceShutdown && runInBackgroundOnClose;

    public static void FlushPersistentStateFireAndForget() =>
        _ = FlushPersistentStateAsync();

    public static async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            BackfillSyncManager.Instance.Shutdown();
            MessageTriageService.Instance.Shutdown();
            UnifiedMessengerInsightsEngine.Instance.Shutdown();
            UnifiedMessengerStateSyncService.Instance.Shutdown();
            OllamaInferenceCoordinator.Instance.Dispose();

            await MessageTriageService.Instance
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
            await UnifiedMessengerInsightsEngine.Instance
                .WaitForShutdownAsync(WorkerShutdownTimeout)
                .ConfigureAwait(false);
            await UnifiedMessengerStateSyncService.Instance
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
        try
        {
            await MessageAnalyticsService.Instance.FlushAsync(cancellationToken).ConfigureAwait(false);
            await RichTriageStoreService.Instance.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lifecycle flush failed: {ex.Message}");
        }
    }
}
