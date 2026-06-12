namespace UnifiedMessenger.Services;

/// <summary>
/// WinRT WebView2 IAsyncOperation continuations often resume on thread-pool threads even when
/// <see cref="Task.ConfigureAwait(bool)"/> is true. Re-marshal to the UI dispatcher before
/// touching <see cref="Microsoft.Web.WebView2.Core.CoreWebView2"/> or <see cref="Microsoft.UI.Xaml.Controls.WebView2"/> again.
/// </summary>
internal static class WebViewUiAwaiter
{
    public static async Task AwaitAsync(Task task)
    {
        await task.ConfigureAwait(false);
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(false);
    }

    public static async Task<T> AwaitAsync<T>(Task<T> task)
    {
        var result = await task.ConfigureAwait(false);
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(false);
        return result;
    }
}
