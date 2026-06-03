using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// Tracks live WebView2 controls keyed by instance id for lifecycle operations such as profile deletion.
/// </summary>
public sealed class InstanceWebViewRegistry
{
    private static readonly Lazy<InstanceWebViewRegistry> LazyInstance = new(() => new InstanceWebViewRegistry());

    private readonly Dictionary<string, WebView2> _webViews = new(StringComparer.OrdinalIgnoreCase);

    public static InstanceWebViewRegistry Instance => LazyInstance.Value;

    public void Register(string instanceId, WebView2 webView)
    {
        _webViews[instanceId] = webView;
    }

    public void Unregister(string instanceId)
    {
        _webViews.Remove(instanceId);
    }

    public WebView2? TryGet(string instanceId)
    {
        _webViews.TryGetValue(instanceId, out var webView);
        return webView;
    }

    public IEnumerable<WebView2> All => _webViews.Values;
}
