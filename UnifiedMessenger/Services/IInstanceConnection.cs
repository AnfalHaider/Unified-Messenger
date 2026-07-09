namespace UnifiedMessenger.Services;

/// <summary>
/// The data layer's view of a single account's live session — script execution + reload — without depending
/// on WebView2 / <see cref="InstanceSessionManager"/> directly (§10 A-7, #26). Oversight, review-health, and
/// avatar-import services talk to this interface, so they're testable (swap <see cref="InstanceConnection.Current"/>
/// for a fake) and decoupled from the WebView host. The default implementation delegates to the existing
/// <see cref="WebViewScriptGateway"/> + <see cref="InstanceSessionManager"/>.
/// </summary>
public interface IInstanceConnection
{
    /// <summary>Runs a prepared script in the account's session; returns the JSON-encoded result, or null.</summary>
    Task<string?> ExecuteScriptAsync(string instanceId, string script);

    /// <summary>Reloads the account's session (e.g. to re-inject an updated scraper on document creation).</summary>
    Task ReloadAsync(string instanceId);
}

/// <summary>
/// WebView2-backed <see cref="IInstanceConnection"/> — the production implementation. Behaviour is identical to
/// the previous direct <see cref="InstanceSessionManager"/> calls.
/// </summary>
public sealed class WebViewInstanceConnection : IInstanceConnection
{
    public Task<string?> ExecuteScriptAsync(string instanceId, string script) =>
        WebViewScriptGateway.Instance.ExecutePreparedScriptAsync(instanceId, script);

    public Task ReloadAsync(string instanceId) =>
        InstanceSessionManager.Instance.ReloadSessionAsync(instanceId);
}

/// <summary>Ambient instance-connection used by the data layer. Override <see cref="Current"/> in tests.</summary>
public static class InstanceConnection
{
    public static IInstanceConnection Current { get; set; } = new WebViewInstanceConnection();
}
