namespace UnifiedMessenger.Services;

/// <summary>
/// Coordinates hide-to-tray vs quit behavior and durable state flush while WebView sessions stay warm.
/// </summary>
public static class ApplicationLifecycleService
{
    public static bool ShouldHideOnClose(bool forceShutdown, bool runInBackgroundOnClose) =>
        !forceShutdown && runInBackgroundOnClose;

    public static void FlushPersistentStateFireAndForget() =>
        _ = FlushPersistentStateAsync();

    public static async Task FlushPersistentStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await RichTriageStoreService.Instance.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lifecycle flush failed: {ex.Message}");
        }
    }
}
