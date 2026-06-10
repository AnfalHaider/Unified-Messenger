using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Adapters;

/// <summary>
/// No-op adapter returned when a platform module is disabled. Prevents script injection and WebMessage handling.
/// </summary>
internal sealed class NullPlatformAdapter : IPlatformAdapter
{
    private static readonly Lazy<NullPlatformAdapter> LazyInstance = new(() => new NullPlatformAdapter());

    public static NullPlatformAdapter Instance => LazyInstance.Value;

    public string PlatformId => "generic";

    public Task RegisterAsync(
        CoreWebView2 coreWebView,
        MessengerInstance instance,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void HandleWebMessage(string messageJson, NotificationHub hub, MessengerInstance instance)
    {
    }
}
