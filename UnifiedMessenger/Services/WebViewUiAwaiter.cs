namespace UnifiedMessenger.Services;

/// <summary>
/// WinRT WebView2 IAsyncOperation continuations often resume on thread-pool threads even when
/// <see cref="Task.ConfigureAwait(bool)"/> is true. Re-marshal to the UI dispatcher before
/// touching <see cref="Microsoft.Web.WebView2.Core.CoreWebView2"/> or <see cref="Microsoft.UI.Xaml.Controls.WebView2"/> again.
/// </summary>
internal static class WebViewUiAwaiter
{
    // A WebView2 IAsyncOperation can occasionally never complete — a busy/loading page, a wedged or crashed
    // renderer, or a profile op contending for a locked folder. Since these are awaited on the UI thread
    // (COM/STA), a never-completing op freezes the whole app (this is what made "Remove instance" / "Refresh
    // WebView" hang). A timeout converts that permanent freeze into a recoverable TimeoutException, which the
    // callers already swallow/log. The underlying op keeps running; we just stop blocking the UI on it.
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(12);

    public static async Task AwaitAsync(Task task)
    {
        await task.WaitAsync(OperationTimeout).ConfigureAwait(false);
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(false);
    }

    public static async Task<T> AwaitAsync<T>(Task<T> task)
    {
        var result = await task.WaitAsync(OperationTimeout).ConfigureAwait(false);
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(false);
        return result;
    }
}
