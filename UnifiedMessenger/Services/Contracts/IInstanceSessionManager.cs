using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IInstanceSessionManager
{
    string? VisibleInstanceId { get; }

    int ActiveSessionCount { get; }

    event EventHandler<InstanceSessionEventArgs>? SessionInitializing;

    event EventHandler<InstanceSessionEventArgs>? SessionReady;

    event EventHandler<InstanceSessionErrorEventArgs>? SessionFailed;

    void AttachHost(Grid host);

    void SyncInstance(MessengerInstance instance);

    Task WarmAllSessionsAsync(
        IEnumerable<MessengerInstance> instances,
        string? visibleInstanceId = null,
        CancellationToken cancellationToken = default);

    Task SwitchToAsync(MessengerInstance instance, CancellationToken cancellationToken = default);

    Task EnsureSessionAsync(MessengerInstance instance, CancellationToken cancellationToken = default);

    Task HideVisibleSessionAsync();

    Task CloseSessionAsync(string instanceId);

    Task ReloadSessionAsync(string instanceId, CancellationToken cancellationToken = default);

    Task ReloadAllSessionsAsync(CancellationToken cancellationToken = default);

    Task RecoverStaleAdapterAsync(string instanceId, CancellationToken cancellationToken = default);

    Task TrySuspendSessionAsync(string instanceId);

    Task TryResumeSessionAsync(string instanceId);

    void ApplyAppWindowState(bool isAppActive);

    WebView2? TryGetWebView(string instanceId);

    IEnumerable<WebView2> AllActiveWebViews { get; }

    Task ExecuteScriptOnInstanceAsync(string instanceId, string script);

    Task<string?> TryExecuteScriptOnInstanceAsync(string instanceId, string script);

    Task BroadcastAdapterSettingsAsync();
}
