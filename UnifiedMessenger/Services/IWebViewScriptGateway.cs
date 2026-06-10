using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Executes WebView JavaScript using JSON-serialized arguments to avoid injection.
/// </summary>
public interface IWebViewScriptGateway
{
    Task ExecuteAsync(
        string instanceId,
        string functionName,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default);

    Task<string?> ExecutePreparedScriptAsync(
        string instanceId,
        string script,
        CancellationToken cancellationToken = default);
}

public sealed class WebViewScriptGateway : IWebViewScriptGateway
{
    private static readonly Lazy<WebViewScriptGateway> LazyInstance = new(() => new WebViewScriptGateway());

    public static WebViewScriptGateway Instance => LazyInstance.Value;

    public Task ExecuteAsync(
        string instanceId,
        string functionName,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(arguments);

        var script = WebViewScriptBuilder.BuildFunctionCall(functionName, arguments);
        return ExecutePreparedScriptAsync(instanceId, script, cancellationToken);
    }

    public Task<string?> ExecutePreparedScriptAsync(
        string instanceId,
        string script,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        return InstanceSessionManager.Instance.TryExecuteScriptOnInstanceAsync(instanceId, script);
    }
}

internal static class WebViewScriptBuilder
{
    public static string BuildFunctionCall(string functionName, IReadOnlyList<object?> arguments)
    {
        var serializedName = JsonSerializer.Serialize(functionName);
        var serializedArgs = string.Join(
            ", ",
            arguments.Select(arg => JsonSerializer.Serialize(arg)));

        return $"window[{serializedName}]({serializedArgs});";
    }
}
