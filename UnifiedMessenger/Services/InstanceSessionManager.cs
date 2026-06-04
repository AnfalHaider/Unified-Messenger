using System.Diagnostics;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services;

/// <summary>
/// Keeps one live WebView2 per instance (unique profile each). Background instances suspend on switch (MEM-01);
/// LRU eviction enforces <see cref="AppSettings.MaxConcurrentWebViews"/> while keeping the visible session (MEM-04).
/// </summary>
public sealed class InstanceSessionManager
{
    private static readonly Lazy<InstanceSessionManager> LazyInstance = new(() => new InstanceSessionManager());

    private readonly Dictionary<string, SessionEntry> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _recentAccessOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _accessNodes = new(StringComparer.OrdinalIgnoreCase);

    private Grid? _host;
    private string? _visibleInstanceId;
    private readonly Dictionary<string, MessengerInstance> _instanceLookup = new(StringComparer.OrdinalIgnoreCase);

    public static InstanceSessionManager Instance => LazyInstance.Value;

    internal InstanceSessionManager()
    {
    }

    public string? VisibleInstanceId => _visibleInstanceId;

    public int ActiveSessionCount => _sessions.Count;

    public event EventHandler<InstanceSessionEventArgs>? SessionInitializing;

    public event EventHandler<InstanceSessionEventArgs>? SessionReady;

    public event EventHandler<InstanceSessionErrorEventArgs>? SessionFailed;

    public void AttachHost(Grid host)
    {
        _host = host;
        UiThreadRunner.Register(host.DispatcherQueue);
    }

    public void SyncInstance(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instanceLookup[instance.Id] = instance;
    }

    /// <summary>
    /// Creates WebViews for every instance so background monitoring starts immediately.
    /// </summary>
    public Task WarmAllSessionsAsync(
        IEnumerable<MessengerInstance> instances,
        string? visibleInstanceId = null,
        CancellationToken cancellationToken = default) =>
        UiThreadRunner.RunAsync(() => WarmAllSessionsCoreAsync(instances, visibleInstanceId, cancellationToken));

    private async Task WarmAllSessionsCoreAsync(
        IEnumerable<MessengerInstance> instances,
        string? visibleInstanceId,
        CancellationToken cancellationToken)
    {
        var list = instances.ToList();
        foreach (var instance in list)
        {
            SyncInstance(instance);
        }

        var settings = AppSettingsService.Instance.Settings;
        var warmMode = settings.EnableLazyWebViewLoading
            ? StartupWarmMode.Lazy
            : settings.StartupWarmMode;

        switch (warmMode)
        {
            case StartupWarmMode.Lazy:
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

    public Task SwitchToAsync(MessengerInstance instance, CancellationToken cancellationToken = default) =>
        UiThreadRunner.RunAsync(() => SwitchToCoreAsync(instance, cancellationToken));

    private async Task SwitchToCoreAsync(MessengerInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        SyncInstance(instance);

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
            SetSessionVisualState(previousInstanceId, current.WebView, isForeground: false);

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

            if (!_sessions.TryGetValue(instance.Id, out var entry))
            {
                throw new InvalidOperationException($"Session for \"{instance.DisplayName}\" was not created.");
            }

            if (!_host.Children.Contains(entry.WebView))
            {
                _host.Children.Add(entry.WebView);
            }

            SetSessionVisualState(instance.Id, entry.WebView, isForeground: true);
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

    public Task EnsureSessionAsync(MessengerInstance instance, CancellationToken cancellationToken = default) =>
        UiThreadRunner.RunAsync(() => EnsureSessionCoreAsync(instance, cancellationToken));

    private async Task EnsureSessionCoreAsync(MessengerInstance instance, CancellationToken cancellationToken)
    {
        SyncInstance(instance);

        if (_sessions.ContainsKey(instance.Id))
        {
            TouchAccessOrder(instance.Id);
            return;
        }

        await EnforceSessionCapAsync(instance.Id, cancellationToken).ConfigureAwait(true);

        SessionInitializing?.Invoke(this, new InstanceSessionEventArgs(instance));

        SessionEntry? entry = null;
        try
        {
            if (InstanceWebViewRegistry.Instance.IsProfileOwnedByOther(instance.ProfileName, instance.Id))
            {
                throw new InvalidOperationException(
                    $"Profile \"{instance.ProfileName}\" is already assigned to another instance.");
            }

            entry = await CreateSessionEntryAsync(instance, cancellationToken).ConfigureAwait(true);
            _sessions[instance.Id] = entry;
            InstanceWebViewRegistry.Instance.Register(instance.Id, instance.ProfileName, entry.WebView);

            if (_host is not null && !_host.Children.Contains(entry.WebView))
            {
                _host.Children.Add(entry.WebView);
            }

            SetSessionVisualState(instance.Id, entry.WebView, isForeground: false);
            entry.WebView.Source = new Uri(instance.StartUrl);
            TouchAccessOrder(instance.Id);

            SessionReady?.Invoke(this, new InstanceSessionEventArgs(instance));
        }
        catch (Exception ex)
        {
            if (entry is not null)
            {
                await DisposeSessionEntryAsync(instance.Id, entry, unregister: true).ConfigureAwait(true);
            }

            SessionFailed?.Invoke(this, new InstanceSessionErrorEventArgs(instance, ex));
            throw;
        }
    }

    /// <summary>
    /// Hides the visible instance without tearing down its WebView (background monitoring continues).
    /// </summary>
    public Task HideVisibleSessionAsync() =>
        UiThreadRunner.RunAsync(HideVisibleSessionCoreAsync);

    private async Task HideVisibleSessionCoreAsync()
    {
        if (_visibleInstanceId is not null && _sessions.TryGetValue(_visibleInstanceId, out var current))
        {
            SetSessionVisualState(_visibleInstanceId, current.WebView, isForeground: false);
            await TrySuspendSessionAsync(_visibleInstanceId).ConfigureAwait(true);
        }

        _visibleInstanceId = null;
    }

    public Task CloseSessionAsync(string instanceId) =>
        UiThreadRunner.RunAsync(() => CloseSessionCoreAsync(instanceId));

    private async Task CloseSessionCoreAsync(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry))
        {
            return;
        }

        await DisposeSessionEntryAsync(instanceId, entry, unregister: true).ConfigureAwait(true);

        if (_visibleInstanceId == instanceId)
        {
            _visibleInstanceId = null;
        }

        InstanceConnectionStatusService.Instance.Remove(instanceId);
        RemoveAccessTracking(instanceId);
    }

    public async Task ReloadSessionAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) ||
            entry.WebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            entry.WebView.CoreWebView2.Reload();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView reload failed: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public async Task ReloadAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var instanceId in _sessions.Keys.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReloadSessionAsync(instanceId, cancellationToken).ConfigureAwait(true);
        }
    }

    public Task RecoverStaleAdapterAsync(string instanceId, CancellationToken cancellationToken = default) =>
        UiThreadRunner.RunAsync(() => RecoverStaleAdapterCoreAsync(instanceId, cancellationToken));

    private async Task RecoverStaleAdapterCoreAsync(string instanceId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) ||
            entry.WebView.CoreWebView2 is null ||
            !_instanceLookup.TryGetValue(instanceId, out var instance))
        {
            await ReloadSessionAsync(instanceId, cancellationToken).ConfigureAwait(true);
            return;
        }

        try
        {
            var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
            if (adapter is BasePlatformAdapter platformAdapter)
            {
                await platformAdapter.ReinjectAsync(entry.WebView.CoreWebView2, instance, cancellationToken)
                    .ConfigureAwait(true);
                AdapterHealthMonitor.Instance.MarkReady(instanceId, platformAdapter.PlatformId);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Adapter reinject failed: {ex.Message}");
        }

        await ReloadSessionAsync(instanceId, cancellationToken).ConfigureAwait(true);
    }

    public async Task TrySuspendSessionAsync(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) ||
            entry.WebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await entry.WebView.CoreWebView2.TrySuspendAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView suspend failed: {ex.Message}");
        }
    }

    public async Task TryResumeSessionAsync(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) ||
            entry.WebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            entry.WebView.CoreWebView2.Resume();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView resume failed: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    internal string? SelectLruEvictionCandidate(string incomingInstanceId)
    {
        for (var node = _recentAccessOrder.Last; node is not null; node = node.Previous)
        {
            var candidateId = node.Value;
            if (candidateId.Equals(incomingInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidateId.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return candidateId;
        }

        return null;
    }

    internal void TrackAccessForTests(string instanceId) => TouchAccessOrder(instanceId);

    internal void SetVisibleInstanceForTests(string? instanceId) => _visibleInstanceId = instanceId;

    private async Task EnforceSessionCapAsync(string incomingInstanceId, CancellationToken cancellationToken)
    {
        var cap = AppSettingsService.Instance.Settings.MaxConcurrentWebViews;
        if (cap <= 0)
        {
            return;
        }

        while (_sessions.Count >= cap)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var evictionCandidate = SelectLruEvictionCandidate(incomingInstanceId);
            if (string.IsNullOrWhiteSpace(evictionCandidate))
            {
                break;
            }

            await CloseSessionAsync(evictionCandidate).ConfigureAwait(true);
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

    private void RemoveAccessTracking(string instanceId)
    {
        if (_accessNodes.Remove(instanceId, out var node))
        {
            _recentAccessOrder.Remove(node);
        }
        else
        {
            _recentAccessOrder.Remove(instanceId);
        }
    }

    public void ApplyAppWindowState(bool isAppActive)
    {
        UiThreadRunner.VerifyThreadAccess();

        foreach (var (instanceId, entry) in _sessions)
        {
            var isForeground = isAppActive &&
                               instanceId.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase);
            SetSessionVisualState(instanceId, entry.WebView, isForeground);
        }
    }

    public WebView2? TryGetWebView(string instanceId)
    {
        return _sessions.TryGetValue(instanceId, out var entry) ? entry.WebView : null;
    }

    public IEnumerable<WebView2> AllActiveWebViews =>
        _sessions.Values.Select(entry => entry.WebView);

    public async Task ExecuteScriptOnInstanceAsync(string instanceId, string script)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) ||
            entry.WebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await entry.WebView.CoreWebView2.ExecuteScriptAsync(script).AsTask().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Instance script execution failed: {ex.Message}");
        }
    }

    public Task BroadcastAdapterSettingsAsync() =>
        UiThreadRunner.RunAsync(BroadcastAdapterSettingsCoreAsync);

    private async Task BroadcastAdapterSettingsCoreAsync()
    {
        var includeMuted = AppSettingsService.Instance.Settings.IncludeMutedChatBadges ? "true" : "false";
        var script =
            $"window.__umIncludeMutedBadges = {includeMuted}; " +
            "if (window.__unifiedMessengerPublishBadge) { window.__unifiedMessengerPublishBadge(); }";

        foreach (var entry in _sessions.Values)
        {
            if (entry.WebView.CoreWebView2 is null)
            {
                continue;
            }

            try
            {
                await entry.WebView.CoreWebView2.ExecuteScriptAsync(script).AsTask().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Adapter settings broadcast failed: {ex.Message}");
            }
        }
    }

    private async Task DisposeSessionEntryAsync(string instanceId, SessionEntry entry, bool unregister)
    {
        DetachMessageHandler(entry);

        if (_host?.Children.Contains(entry.WebView) == true)
        {
            _host.Children.Remove(entry.WebView);
        }

        if (entry.WebView.CoreWebView2 is not null)
        {
            entry.WebView.Close();
        }

        _sessions.Remove(instanceId);

        if (unregister)
        {
            InstanceWebViewRegistry.Instance.Unregister(instanceId);
        }

        await Task.CompletedTask;
    }

    private static void DetachMessageHandler(SessionEntry entry)
    {
        if (entry.WebView.CoreWebView2 is null || entry.MessageHandler is null)
        {
            return;
        }

        entry.WebView.CoreWebView2.WebMessageReceived -= entry.MessageHandler;
        entry.MessageHandler = null;
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

        return InstanceSessionManager.Instance.TryGetInstanceMemoryTier(instanceId) switch
        {
            MemoryTierPreference.High => CoreWebView2MemoryUsageTargetLevel.Normal,
            MemoryTierPreference.Low => CoreWebView2MemoryUsageTargetLevel.Low,
            _ => CoreWebView2MemoryUsageTargetLevel.Low
        };
    }

    internal MemoryTierPreference TryGetInstanceMemoryTier(string instanceId)
    {
        return _instanceLookup.TryGetValue(instanceId, out var instance)
            ? instance.MemoryTier
            : MemoryTierPreference.Normal;
    }

    private static async Task<SessionEntry> CreateSessionEntryAsync(
        MessengerInstance instance,
        CancellationToken cancellationToken)
    {
        var webView = await WebViewProfileManager.Instance
            .CreateWebViewAsync(instance.ProfileName, cancellationToken);

        webView.HorizontalAlignment = HorizontalAlignment.Stretch;
        webView.VerticalAlignment = VerticalAlignment.Stretch;

        var coreWebView = webView.CoreWebView2
            ?? throw new InvalidOperationException("CoreWebView2 was not initialized.");

        var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
        TypedEventHandler<CoreWebView2, CoreWebView2WebMessageReceivedEventArgs> messageHandler = (_, args) =>
        {
            adapter.HandleWebMessage(args.WebMessageAsJson, NotificationHub.Instance, instance);
        };

        coreWebView.WebMessageReceived += messageHandler;

        await WebViewChromeStyleInjector.InjectAsync(coreWebView, instance.Platform, cancellationToken);
        await adapter.RegisterAsync(coreWebView, instance, cancellationToken);

        return new SessionEntry
        {
            WebView = webView,
            MessageHandler = messageHandler
        };
    }

    private sealed class SessionEntry
    {
        public required WebView2 WebView { get; init; }

        public TypedEventHandler<CoreWebView2, CoreWebView2WebMessageReceivedEventArgs>? MessageHandler { get; set; }
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
