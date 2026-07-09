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
/// All sessions share the persistent user data folder from <see cref="WebViewProfileManager.UserDataFolder"/>.
/// WhatsApp Business instances target <c>https://web.whatsapp.com</c> with auditor scripts for reply verification.
/// </summary>
public sealed class InstanceSessionManager : IInstanceSessionManager
{
    private static readonly Lazy<InstanceSessionManager> LazyInstance = new(() => new InstanceSessionManager());

    private readonly Dictionary<string, SessionEntry> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _recentAccessOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _accessNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastAccessUtc = new(StringComparer.OrdinalIgnoreCase);

    // The cap is only enforced when a *new* session is created; without this, a handful of briefly-visited
    // accounts stay live (holding RAM) indefinitely. This timer closes idle non-visible personal sessions.
    private static readonly TimeSpan IdleReapInterval = TimeSpan.FromMinutes(1);
    private Timer? _idleReapTimer;

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
        _idleReapTimer ??= new Timer(_ => _ = ReapIdleSessionsAsync(), null, IdleReapInterval, IdleReapInterval);
    }

    public void SyncInstance(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instanceLookup[instance.Id] = instance;
    }

    public void RefreshMemoryTarget(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry))
        {
            return;
        }

        var isForeground = instanceId.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase);
        SetSessionVisualState(instanceId, entry.WebView, isForeground);
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
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

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
        }

        SessionInitializing?.Invoke(this, new InstanceSessionEventArgs(instance));

        try
        {
            await EnsureSessionAsync(instance, cancellationToken).ConfigureAwait(true);
            await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

            if (!_sessions.TryGetValue(instance.Id, out var entry))
            {
                throw new InvalidOperationException($"Session for \"{instance.DisplayName}\" was not created.");
            }

            if (_host is not null && !_host.Children.Contains(entry.WebView))
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
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

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
            await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
            await AttachSessionEntryAsync(instance, entry).ConfigureAwait(true);
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

    private Task HideVisibleSessionCoreAsync()
    {
        if (_visibleInstanceId is not null && _sessions.TryGetValue(_visibleInstanceId, out var current))
        {
            SetSessionVisualState(_visibleInstanceId, current.WebView, isForeground: false);
        }

        _visibleInstanceId = null;
        return Task.CompletedTask;
    }

    public Task CloseSessionAsync(string instanceId) =>
        UiThreadRunner.RunAsync(() => CloseSessionCoreAsync(instanceId));

    public Task CloseAllSessionsAsync(CancellationToken cancellationToken = default) =>
        UiThreadRunner.RunAsync(() => CloseAllSessionsCoreAsync(cancellationToken));

    private async Task CloseAllSessionsCoreAsync(CancellationToken cancellationToken)
    {
        foreach (var instanceId in _sessions.Keys.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CloseSessionCoreAsync(instanceId).ConfigureAwait(true);
        }

        _visibleInstanceId = null;
    }

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

    public Task ReloadSessionAsync(string instanceId, CancellationToken cancellationToken = default) =>
        UiThreadRunner.RunAsync(() => ReloadSessionCoreAsync(instanceId, cancellationToken));

    private async Task ReloadSessionCoreAsync(string instanceId, CancellationToken cancellationToken)
    {
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

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

    public Task TrySuspendSessionAsync(string instanceId) =>
        UiThreadRunner.RunAsync(() => TrySuspendSessionCoreAsync(instanceId));

    private async Task TrySuspendSessionCoreAsync(string instanceId)
    {
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

        if (!_sessions.TryGetValue(instanceId, out var entry) ||
            entry.WebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await WebViewUiAwaiter
                .AwaitAsync(entry.WebView.CoreWebView2.TrySuspendAsync().AsTask())
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView suspend failed: {ex.Message}");
        }
    }

    public Task TryResumeSessionAsync(string instanceId) =>
        UiThreadRunner.RunAsync(() => TryResumeSessionCoreAsync(instanceId));

    private Task TryResumeSessionCoreAsync(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) ||
            entry.WebView.CoreWebView2 is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            entry.WebView.CoreWebView2.Resume();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView resume failed: {ex.Message}");
        }

        return Task.CompletedTask;
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

    internal void RemoveAccessForTests(string instanceId) => RemoveAccessTracking(instanceId);

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
        _lastAccessUtc[instanceId] = DateTimeOffset.UtcNow;
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

        _lastAccessUtc.Remove(instanceId);
    }

    private Task ReapIdleSessionsAsync() => UiThreadRunner.RunAsync(ReapIdleSessionsCoreAsync);

    /// <summary>
    /// Closes non-visible sessions that have sat idle past the configured TTL, to reclaim RAM. Professional
    /// accounts are exempt — they must stay live so background oversight keeps reading them. Closing a
    /// session does not sign it out (the profile's on-disk data persists); it reloads on next open.
    /// </summary>
    private async Task ReapIdleSessionsCoreAsync()
    {
        var minutes = AppSettingsService.Instance.Settings.IdleSessionReapMinutes;
        if (minutes <= 0)
        {
            return;
        }

        var ttl = TimeSpan.FromMinutes(minutes);
        var now = DateTimeOffset.UtcNow;

        foreach (var instanceId in _sessions.Keys.ToList())
        {
            if (instanceId.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isProfessional = _instanceLookup.TryGetValue(instanceId, out var instance) && instance.IsProfessional;
            DateTimeOffset? lastAccess = _lastAccessUtc.TryGetValue(instanceId, out var stamp) ? stamp : null;

            if (!IsReapEligible(isVisible: false, isProfessional, lastAccess, now, ttl))
            {
                continue;
            }

            await CloseSessionCoreAsync(instanceId).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Pure idle-reap eligibility decision (unit-testable). A session may be closed only when it is not the
    /// visible one, not a professional account (those stay live for background oversight), and has been
    /// idle at least <paramref name="ttl"/>. A null <paramref name="lastAccess"/> is treated as never-touched
    /// and therefore not eligible (we only reap sessions we've actually seen accessed).
    /// </summary>
    internal static bool IsReapEligible(
        bool isVisible,
        bool isProfessional,
        DateTimeOffset? lastAccess,
        DateTimeOffset now,
        TimeSpan ttl)
    {
        if (isVisible || isProfessional || lastAccess is not { } last)
        {
            return false;
        }

        return now - last >= ttl;
    }

    public void ApplyAppWindowState(bool isAppActive)
    {
        _ = UiThreadRunner.RunAsync(() =>
        {
            foreach (var (instanceId, entry) in _sessions)
            {
                var isForeground = isAppActive &&
                                   instanceId.Equals(_visibleInstanceId, StringComparison.OrdinalIgnoreCase);
                SetSessionVisualState(instanceId, entry.WebView, isForeground);
            }

            return Task.CompletedTask;
        });
    }

    public WebView2? TryGetWebView(string instanceId)
    {
        return _sessions.TryGetValue(instanceId, out var entry) ? entry.WebView : null;
    }

    public IEnumerable<WebView2> AllActiveWebViews =>
        _sessions.Values.Select(entry => entry.WebView);

    public Task ExecuteScriptOnInstanceAsync(string instanceId, string script) =>
        TryExecuteScriptOnInstanceAsync(instanceId, script);

    public async Task<string?> TryExecuteScriptOnInstanceAsync(string instanceId, string script) =>
        await UiThreadRunner.RunAsync(async () =>
        {
            if (!_sessions.TryGetValue(instanceId, out var entry) ||
                entry.WebView.CoreWebView2 is null)
            {
                return null;
            }

            try
            {
                return await WebViewUiAwaiter
                    .AwaitAsync(entry.WebView.CoreWebView2.ExecuteScriptAsync(script).AsTask())
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Instance script execution failed: {ex.Message}");
                return null;
            }
        }).ConfigureAwait(false);

    public Task BroadcastAdapterSettingsAsync() =>
        UiThreadRunner.RunAsync(BroadcastAdapterSettingsCoreAsync);

    private async Task BroadcastAdapterSettingsCoreAsync()
    {
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

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
                await WebViewUiAwaiter
                    .AwaitAsync(entry.WebView.CoreWebView2.ExecuteScriptAsync(script).AsTask())
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Adapter settings broadcast failed: {ex.Message}");
            }
        }
    }

    private Task DisposeSessionEntryAsync(string instanceId, SessionEntry entry, bool unregister) =>
        UiThreadRunner.RunAsync(() => DisposeSessionEntryCoreAsync(instanceId, entry, unregister));

    private async Task DisposeSessionEntryCoreAsync(string instanceId, SessionEntry entry, bool unregister)
    {
        DetachMessageHandler(entry);
        DetachNavigationHandler(entry);

        if (_host?.Children.Contains(entry.WebView) == true)
        {
            _host.Children.Remove(entry.WebView);
        }

        if (entry.WebView.CoreWebView2 is not null)
        {
            WebViewNavigationGuard.Detach(entry.WebView.CoreWebView2);

            // Close directly — do NOT TrySuspendAsync first. Suspend is a memory optimization for sessions
            // we'll resume; before a permanent Close it's pointless, and on a busy/loading WebView the
            // suspend await could hang the close and freeze the UI (the "Remove instance gets stuck" bug).
            entry.WebView.Close();
        }

        _sessions.Remove(instanceId);

        if (unregister)
        {
            InstanceWebViewRegistry.Instance.Unregister(instanceId);
        }

        await Task.CompletedTask;
    }

    private static void DetachNavigationHandler(SessionEntry entry)
    {
        if (entry.WebView.CoreWebView2 is null)
        {
            return;
        }

        PlatformNavigationHooks.Detach(entry.WebView.CoreWebView2);
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
        // A Custom-URL (generic) tab monitors a user-chosen arbitrary website; don't constrain its navigation
        // to a fixed origin (that blanked custom URLs on cross-domain redirects). Known platforms keep the guard.
        var allowAllNavigation =
            PlatformDefinition.NormalizePlatformId(instance.Platform).Equals("generic", StringComparison.OrdinalIgnoreCase);

        var webView = await WebViewProfileManager.Instance
            .CreateWebViewAsync(instance.ProfileName, instance.StartUrl, cancellationToken, allowAllNavigation)
            .ConfigureAwait(true);

        return await UiThreadRunner.RunAsync(async () =>
        {
            webView.HorizontalAlignment = HorizontalAlignment.Stretch;
            webView.VerticalAlignment = VerticalAlignment.Stretch;

            var coreWebView = webView.CoreWebView2
                ?? throw new InvalidOperationException("CoreWebView2 was not initialized.");

            WebViewPlatformConfigurator.Apply(coreWebView, instance.Platform);

            var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
            TypedEventHandler<CoreWebView2, CoreWebView2WebMessageReceivedEventArgs> messageHandler = (_, args) =>
            {
                WebMessageIngressService.Instance.Enqueue(args.WebMessageAsJson, instance);
            };

            coreWebView.WebMessageReceived += messageHandler;

            await WebViewChromeStyleInjector.InjectAsync(coreWebView, instance.Platform, cancellationToken)
                .ConfigureAwait(true);
            await adapter.RegisterAsync(coreWebView, instance, cancellationToken).ConfigureAwait(true);
            await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

            return new SessionEntry
            {
                WebView = webView,
                MessageHandler = messageHandler
            };
        }).ConfigureAwait(true);
    }

    private Task AttachSessionEntryAsync(MessengerInstance instance, SessionEntry entry) =>
        UiThreadRunner.RunAsync(() =>
        {
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
            return Task.CompletedTask;
        });

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
