using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services;

/// <summary>
/// Keeps one live WebView2 per instance (unique profile each). All sessions stay connected
/// for background WebSocket/DOM monitoring; switching only changes which WebView is visible.
/// </summary>
public sealed class InstanceSessionManager
{
    private static readonly Lazy<InstanceSessionManager> LazyInstance = new(() => new InstanceSessionManager());

    private readonly Dictionary<string, WebView2> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _profileOwners = new(StringComparer.OrdinalIgnoreCase);

    private Grid? _host;
    private string? _visibleInstanceId;

    public static InstanceSessionManager Instance => LazyInstance.Value;

    public string? VisibleInstanceId => _visibleInstanceId;

    public event EventHandler<InstanceSessionEventArgs>? SessionInitializing;

    public event EventHandler<InstanceSessionEventArgs>? SessionReady;

    public event EventHandler<InstanceSessionErrorEventArgs>? SessionFailed;

    public void AttachHost(Grid host)
    {
        _host = host;
    }

    /// <summary>
    /// Creates WebViews for every instance so background monitoring starts immediately.
    /// </summary>
    public async Task WarmAllSessionsAsync(
        IEnumerable<MessengerInstance> instances,
        string? visibleInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var instance in instances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureSessionAsync(instance, cancellationToken).ConfigureAwait(true);
        }

        if (!string.IsNullOrWhiteSpace(visibleInstanceId))
        {
            var visible = instances.FirstOrDefault(i =>
                i.Id.Equals(visibleInstanceId, StringComparison.OrdinalIgnoreCase));

            if (visible is not null)
            {
                await SwitchToAsync(visible, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    public async Task SwitchToAsync(MessengerInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (_host is null)
        {
            throw new InvalidOperationException("Instance host grid is not attached.");
        }

        if (_visibleInstanceId == instance.Id)
        {
            return;
        }

        if (_visibleInstanceId is not null && _sessions.TryGetValue(_visibleInstanceId, out var current))
        {
            SetSessionVisualState(current, isForeground: false);
        }

        SessionInitializing?.Invoke(this, new InstanceSessionEventArgs(instance));

        try
        {
            await EnsureSessionAsync(instance, cancellationToken).ConfigureAwait(true);

            if (!_sessions.TryGetValue(instance.Id, out var webView))
            {
                throw new InvalidOperationException($"Session for \"{instance.DisplayName}\" was not created.");
            }

            if (!_host.Children.Contains(webView))
            {
                _host.Children.Add(webView);
            }

            SetSessionVisualState(webView, isForeground: true);
            _visibleInstanceId = instance.Id;

            SessionReady?.Invoke(this, new InstanceSessionEventArgs(instance));
        }
        catch (Exception ex)
        {
            SessionFailed?.Invoke(this, new InstanceSessionErrorEventArgs(instance, ex));
            throw;
        }
    }

    public async Task EnsureSessionAsync(MessengerInstance instance, CancellationToken cancellationToken = default)
    {
        if (_sessions.ContainsKey(instance.Id))
        {
            return;
        }

        SessionInitializing?.Invoke(this, new InstanceSessionEventArgs(instance));

        try
        {
            if (_profileOwners.TryGetValue(instance.ProfileName, out var ownerId) &&
                !ownerId.Equals(instance.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Profile \"{instance.ProfileName}\" is already assigned to another instance.");
            }

            var webView = await CreateConfiguredWebViewAsync(instance, cancellationToken).ConfigureAwait(true);

            _sessions[instance.Id] = webView;
            _profileOwners[instance.ProfileName] = instance.Id;
            InstanceWebViewRegistry.Instance.Register(instance.Id, webView);

            if (_host is not null && !_host.Children.Contains(webView))
            {
                _host.Children.Add(webView);
            }

            SetSessionVisualState(webView, isForeground: false);
            webView.Source = new Uri(instance.StartUrl);

            SessionReady?.Invoke(this, new InstanceSessionEventArgs(instance));
        }
        catch (Exception ex)
        {
            SessionFailed?.Invoke(this, new InstanceSessionErrorEventArgs(instance, ex));
            throw;
        }
    }

    /// <summary>
    /// Hides the visible instance without tearing down its WebView (background monitoring continues).
    /// </summary>
    public Task HideVisibleSessionAsync()
    {
        if (_visibleInstanceId is not null && _sessions.TryGetValue(_visibleInstanceId, out var current))
        {
            SetSessionVisualState(current, isForeground: false);
        }

        _visibleInstanceId = null;
        return Task.CompletedTask;
    }

    public async Task CloseSessionAsync(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var webView))
        {
            return;
        }

        var profileName = webView.CoreWebView2?.Profile.ProfileName;

        if (_host?.Children.Contains(webView) == true)
        {
            _host.Children.Remove(webView);
        }

        if (webView.CoreWebView2 is not null)
        {
            webView.Close();
        }

        _sessions.Remove(instanceId);
        InstanceWebViewRegistry.Instance.Unregister(instanceId);

        if (!string.IsNullOrWhiteSpace(profileName))
        {
            _profileOwners.Remove(profileName);
        }

        if (_visibleInstanceId == instanceId)
        {
            _visibleInstanceId = null;
        }

        await Task.CompletedTask;
    }

    public void ApplyAppWindowState(bool isAppActive)
    {
        foreach (var (instanceId, webView) in _sessions)
        {
            var isForeground = isAppActive &&
                               instanceId.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase);
            SetSessionVisualState(webView, isForeground);
        }
    }

    public WebView2? TryGetWebView(string instanceId)
    {
        _sessions.TryGetValue(instanceId, out var webView);
        return webView;
    }

    public IEnumerable<WebView2> AllActiveWebViews => _sessions.Values;

    public async Task BroadcastAdapterSettingsAsync()
    {
        var includeMuted = AppSettingsService.Instance.Settings.IncludeMutedChatBadges ? "true" : "false";
        var script =
            $"window.__umIncludeMutedBadges = {includeMuted}; " +
            "if (window.__unifiedMessengerPublishBadge) { window.__unifiedMessengerPublishBadge(); }";

        foreach (var webView in _sessions.Values)
        {
            if (webView.CoreWebView2 is null)
            {
                continue;
            }

            try
            {
                await webView.CoreWebView2.ExecuteScriptAsync(script).AsTask().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Adapter settings broadcast failed: {ex.Message}");
            }
        }
    }

    private static void SetSessionVisualState(WebView2 webView, bool isForeground)
    {
        webView.Visibility = isForeground ? Visibility.Visible : Visibility.Collapsed;

        if (webView.CoreWebView2 is null)
        {
            return;
        }

        webView.CoreWebView2.MemoryUsageTargetLevel = isForeground
            ? CoreWebView2MemoryUsageTargetLevel.Normal
            : CoreWebView2MemoryUsageTargetLevel.Low;
    }

    private static async Task<WebView2> CreateConfiguredWebViewAsync(
        MessengerInstance instance,
        CancellationToken cancellationToken)
    {
        var webView = await WebViewProfileManager.Instance
            .CreateWebViewAsync(instance.ProfileName, cancellationToken);

        webView.HorizontalAlignment = HorizontalAlignment.Stretch;
        webView.VerticalAlignment = VerticalAlignment.Stretch;

        var coreWebView = webView.CoreWebView2
            ?? throw new InvalidOperationException("CoreWebView2 was not initialized.");

        if (!coreWebView.Profile.ProfileName.Equals(instance.ProfileName, StringComparison.OrdinalIgnoreCase))
        {
            webView.Close();
            throw new InvalidOperationException(
                $"Profile mismatch. Expected \"{instance.ProfileName}\" but got \"{coreWebView.Profile.ProfileName}\".");
        }

        var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
        coreWebView.WebMessageReceived += (_, args) =>
        {
            adapter.HandleWebMessage(args.WebMessageAsJson, NotificationHub.Instance, instance);
        };

        await WebViewChromeStyleInjector.InjectAsync(coreWebView, instance.Platform, cancellationToken);
        await adapter.RegisterAsync(coreWebView, instance, cancellationToken);

        return webView;
    }
}

public sealed class InstanceSessionEventArgs(MessengerInstance instance) : EventArgs
{
    public MessengerInstance Instance { get; } = instance;
}

public sealed class InstanceSessionErrorEventArgs(MessengerInstance instance, Exception error) : EventArgs
{
    public MessengerInstance Instance { get; } = instance;

    public Exception Error { get; } = error;
}
