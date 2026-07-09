using Microsoft.UI.Dispatching;

namespace UnifiedMessenger.Services;

/// <summary>
/// Marshals async work onto the WinUI dispatcher. WebView2 and XAML objects require this.
/// WinRT awaitables often resume on thread-pool threads even when ConfigureAwait(true) is used.
/// </summary>
internal static class UiThreadRunner
{
    private static DispatcherQueue? _dispatcher;

    public static void Register(DispatcherQueue dispatcher) =>
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    public static DispatcherQueue GetDispatcher() =>
        _dispatcher
        ?? App.CurrentWindow?.DispatcherQueue
        ?? DispatcherQueue.GetForCurrentThread()
        ?? throw new InvalidOperationException("No UI dispatcher is available.");

    /// <summary>
    /// Ensures subsequent UI-bound work runs on the dispatcher thread.
    /// Call after awaits that may touch WebView2 or XAML.
    /// </summary>
    public static Task YieldToUiAsync()
    {
        var dispatcher = GetDispatcher();
        if (dispatcher.HasThreadAccess)
        {
            return Task.CompletedTask;
        }

        return RunAsync(static () => Task.CompletedTask);
    }

    public static async Task RunAsync(Func<Task> action) =>
        await RunAsync(async () =>
        {
            await action().ConfigureAwait(true);
            return 0;
        }).ConfigureAwait(true);

    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = GetDispatcher();
        if (dispatcher.HasThreadAccess)
        {
            return await action().ConfigureAwait(true);
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enqueued = dispatcher.TryEnqueue(
            DispatcherQueuePriority.Normal,
            () => BeginEnqueue(action, tcs));

        if (!enqueued)
        {
            throw new InvalidOperationException("Failed to enqueue work on the UI thread.");
        }

        return await tcs.Task.ConfigureAwait(true);
    }

    private static void BeginEnqueue<T>(Func<Task<T>> action, TaskCompletionSource<T> tcs)
    {
        _ = RunEnqueuedAsync(action, tcs);
    }

    private static async Task RunEnqueuedAsync<T>(Func<Task<T>> action, TaskCompletionSource<T> tcs)
    {
        try
        {
            tcs.SetResult(await action().ConfigureAwait(true));
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    }
}
