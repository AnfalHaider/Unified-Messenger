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
    private readonly LinkedList<string> _recentAccessOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _accessNodes = new(StringComparer.OrdinalIgnoreCase);

    private Grid? _host;
    private string? _visibleInstanceId;
    private readonly Dictionary<string, MessengerInstance> _instanceLookup = new(StringComparer.OrdinalIgnoreCase);

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
        var list = instances.ToList();
        foreach (var instance in list)
        {
            _instanceLookup[instance.Id] = instance;
        }

        var settings = AppSettingsService.Instance.Settings;
        var warmMode = settings.EnableLazyWebViewLoading
            ? StartupWarmMode.Lazy
            : settings.StartupWarmMode;

        switch (warmMode)
        {
            case StartupWarmMode.Lazy:
                if (!string.IsNullOrWhiteSpace(visibleInstanceId))
                {
                    var visible = list.FirstOrDefault(i =>
                        i.Id.Equals(visibleInstanceId, StringComparison.OrdinalIgnoreCase));
                    if (visible is not null)
                    {
                        await EnsureSessionAsync(visible, cancellationToken).ConfigureAwait(true);
                        await SwitchToAsync(visible, cancellationToken).ConfigureAwait(true);
                    }
                }

                return;

            case StartupWarmMode.VisibleOnly:
                if (!string.IsNullOrWhiteSpace(visibleInstanceId))
                {
                    var visible = list.FirstOrDefault(i =>
                        i.Id.Equals(visibleInstanceId, StringComparison.OrdinalIgnoreCase));
                    if (visible is not null)
                    {
                        await EnsureSessionAsync(visible, cancellationToken).ConfigureAwait(true);
                        await SwitchToAsync(visible, cancellationToken).ConfigureAwait(true);
                    }
                }

                return;
        }

        foreach (var instance in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureSessionAsync(instance, cancellationToken).ConfigureAwait(true);
        }

        if (!string.IsNullOrWhiteSpace(visibleInstanceId))
        {
            var visible = list.FirstOrDefault(i =>
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
            var previousInstanceId = _visibleInstanceId;
            SetSessionVisualState(previousInstanceId, current, isForeground: false);

            if (AppSettingsService.Instance.Settings.EnablePerInstanceSleepUnload)
            {
                await CloseSessionAsync(previousInstanceId).ConfigureAwait(true);
            }
            else
            {
                await TrySuspendSessionAsync(previousInstanceId).ConfigureAwait(true);
            }
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

            SetSessionVisualState(instance.Id, webView, isForeground: true);
            _visibleInstanceId = instance.Id;
            TouchAccessOrder(instance.Id);

            await TryResumeSessionAsync(instance.Id).ConfigureAwait(true);

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
        _instanceLookup[instance.Id] = instance;

        if (_sessions.ContainsKey(instance.Id))
        {
            TouchAccessOrder(instance.Id);
            return;
        }

        await EnforceSessionCapAsync(instance.Id, cancellationToken).ConfigureAwait(true);

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

            SetSessionVisualState(instance.Id, webView, isForeground: false);
            webView.Source = new Uri(instance.StartUrl);
            TouchAccessOrder(instance.Id);

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
            SetSessionVisualState(_visibleInstanceId, current, isForeground: false);
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

        _accessNodes.Remove(instanceId);
        _recentAccessOrder.Remove(instanceId);
        _instanceLookup.Remove(instanceId);

        await Task.CompletedTask;
    }

    public async Task ReloadSessionAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(instanceId, out var webView) ||
            webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            webView.CoreWebView2.Reload();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView reload failed: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public async Task ReloadAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var instanceId in _sessions.Keys.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReloadSessionAsync(instanceId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RecoverStaleAdapterAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(instanceId, out var webView) ||
            webView.CoreWebView2 is null ||
            !_instanceLookup.TryGetValue(instanceId, out var instance))
        {
            await ReloadSessionAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
            if (adapter is BasePlatformAdapter platformAdapter)
            {
                await platformAdapter.ReinjectAsync(webView.CoreWebView2, instance, cancellationToken)
                    .ConfigureAwait(false);
                AdapterHealthMonitor.Instance.MarkReady(instanceId, platformAdapter.PlatformId);
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Adapter reinject failed: {ex.Message}");
        }

        await ReloadSessionAsync(instanceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task TrySuspendSessionAsync(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var webView) ||
            webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await webView.CoreWebView2.TrySuspendAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView suspend failed: {ex.Message}");
        }
    }

    public async Task TryResumeSessionAsync(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var webView) ||
            webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            webView.CoreWebView2.Resume();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView resume failed: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task EnforceSessionCapAsync(string incomingInstanceId, CancellationToken cancellationToken)
    {
        var cap = AppSettingsService.Instance.Settings.MaxConcurrentWebViews;
        if (cap <= 0 || _sessions.Count < cap)
        {
            return;
        }

        while (_sessions.Count >= cap)
        {
            var evictionCandidate = _recentAccessOrder
                .FirstOrDefault(id =>
                    !id.Equals(incomingInstanceId, StringComparison.OrdinalIgnoreCase) &&
                    !id.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(evictionCandidate))
            {
                break;
            }

            await CloseSessionAsync(evictionCandidate).ConfigureAwait(false);
        }
    }

    private void TouchAccessOrder(string instanceId)
    {
        if (_accessNodes.TryGetValue(instanceId, out var existingNode))
        {
            _recentAccessOrder.Remove(existingNode);
        }

        var node = _recentAccessOrder.AddFirst(instanceId);
        _accessNodes[instanceId] = node;
    }

    public void ApplyAppWindowState(bool isAppActive)
    {
        foreach (var (instanceId, webView) in _sessions)
        {
            var isForeground = isAppActive &&
                               instanceId.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase);
            SetSessionVisualState(instanceId, webView, isForeground);
        }
    }

    public WebView2? TryGetWebView(string instanceId)
    {
        _sessions.TryGetValue(instanceId, out var webView);
        return webView;
    }

    public IEnumerable<WebView2> AllActiveWebViews => _sessions.Values;

    public async Task ExecuteScriptOnInstanceAsync(string instanceId, string script)
    {
        if (!_sessions.TryGetValue(instanceId, out var webView) ||
            webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(script).AsTask().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Instance script execution failed: {ex.Message}");
        }
    }

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

    private static void SetSessionVisualState(string instanceId, WebView2 webView, bool isForeground)
    {
        webView.Visibility = isForeground ? Visibility.Visible : Visibility.Collapsed;

        if (webView.CoreWebView2 is null)
        {
            return;
        }

        webView.CoreWebView2.MemoryUsageTargetLevel = ResolveMemoryTarget(instanceId, isForeground);
    }

    private static CoreWebView2MemoryUsageTargetLevel ResolveMemoryTarget(string instanceId, bool isForeground)
    {
        if (isForeground)
        {
            return CoreWebView2MemoryUsageTargetLevel.Normal;
        }

        if (InstanceSessionManager.Instance.TryGetInstanceMemoryTier(instanceId) == MemoryTierPreference.High)
        {
            return CoreWebView2MemoryUsageTargetLevel.Normal;
        }

        if (InstanceSessionManager.Instance.TryGetInstanceMemoryTier(instanceId) == MemoryTierPreference.Low)
        {
            return CoreWebView2MemoryUsageTargetLevel.Low;
        }

        return CoreWebView2MemoryUsageTargetLevel.Low;
    }

    internal MemoryTierPreference TryGetInstanceMemoryTier(string instanceId)
    {
        return _instanceLookup.TryGetValue(instanceId, out var instance)
            ? instance.MemoryTier
            : MemoryTierPreference.Normal;
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
