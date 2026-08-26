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

    /// <summary>
    /// Whether the calling thread may touch UI objects. Never throws.
    /// </summary>
    /// <remarks>
    /// Exists because getting this wrong is not a catchable mistake: reading a UI-thread-only WinRT
    /// property such as <c>Application.Current.RequestedTheme</c> from a thread-pool thread terminates the
    /// process with an <c>AccessViolationException</c> inside CoreCLR rather than raising something a
    /// <c>try</c> could contain. Any shared helper that a background task might reach has to be able to ask.
    /// </remarks>
    public static bool HasUiAccess
    {
        get
        {
            try
            {
                var dispatcher = _dispatcher ?? App.CurrentWindow?.DispatcherQueue;
                return dispatcher?.HasThreadAccess == true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static DispatcherQueue GetDispatcher() =>
        _dispatcher
        ?? App.CurrentWindow?.DispatcherQueue
        ?? DispatcherQueue.GetForCurrentThread()
        ?? throw new InvalidOperationException("No UI dispatcher is available.");

    /// <summary>
    /// Queues UI-bound work onto the dispatcher and returns immediately.
    /// </summary>
    /// <remarks>
    /// For callers that cannot await and cannot report a failure — WinRT event handlers, which Windows
    /// raises on a background thread. <see cref="RunAsync(Func{Task})"/> is wrong for those: it blocks the
    /// raising thread on a <c>TaskCompletionSource</c>, and its <c>GetDispatcher</c> throws once the window
    /// is gone. This never throws; work queued after teardown is dropped with a log line instead. The
    /// action always runs on the dispatcher, never inline, so behaviour does not depend on which thread
    /// happened to raise the event.
    /// </remarks>
    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        DispatcherQueue? dispatcher;
        try
        {
            dispatcher = _dispatcher ?? App.CurrentWindow?.DispatcherQueue;
        }
        catch
        {
            // Reading CurrentWindow can itself fail once the window is torn down.
            return;
        }

        if (dispatcher?.TryEnqueue(() => action()) != true)
        {
            AppLogger.LogWarning("UiThread", "Dropped UI work: no dispatcher available.");
        }
    }

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
