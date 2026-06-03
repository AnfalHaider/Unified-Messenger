using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Services.Adapters;

public interface IPlatformAdapter
{
    string PlatformId { get; }

    Task RegisterAsync(CoreWebView2 coreWebView, MessengerInstance instance, CancellationToken cancellationToken = default);

    void HandleWebMessage(string messageJson, NotificationHub hub, MessengerInstance instance);
}

public abstract class BasePlatformAdapter : IPlatformAdapter
{
    protected abstract string ScriptFileName { get; }

    public abstract string PlatformId { get; }

    public async Task RegisterAsync(
        CoreWebView2 coreWebView,
        MessengerInstance instance,
        CancellationToken cancellationToken = default)
    {
        coreWebView.Settings.IsWebMessageEnabled = true;

        await InjectScriptAsync(coreWebView, "adapter-core.js", instance, cancellationToken);
        await InjectScriptAsync(coreWebView, ScriptFileName, instance, cancellationToken);

        RegisterNavigationHooks(coreWebView);
    }

    public void HandleWebMessage(string messageJson, NotificationHub hub, MessengerInstance instance)
    {
        try
        {
            using var document = WebMessageParser.Parse(messageJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            if (HandleStandardMessage(type, root, hub, instance))
            {
                return;
            }

            HandleCustomMessage(type, root, hub, instance);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebMessage parse failed: {ex.Message} | Raw: {messageJson}");
        }
    }

    protected virtual void RegisterNavigationHooks(CoreWebView2 coreWebView)
    {
        coreWebView.NavigationCompleted += (sender, args) =>
        {
            if (!args.IsSuccess)
            {
                return;
            }

            _ = coreWebView.ExecuteScriptAsync(
                "if (window.__unifiedMessengerPublishBadge) { window.__unifiedMessengerPublishBadge(); }");
        };
    }

    protected virtual bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance) => false;

    private bool HandleStandardMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        var health = AdapterHealthMonitor.Instance;
        var muted = instance.NotificationsMuted;

        switch (type)
        {
            case AdapterMessageTypes.BadgeCount:
                if (muted)
                {
                    return true;
                }

                if (root.TryGetProperty("count", out var countElement))
                {
                    hub.UpdateBadgeCount(instance.Id, countElement.GetInt32());
                }

                return true;

            case AdapterMessageTypes.NotificationPreview:
                if (muted)
                {
                    return true;
                }

                var title = root.TryGetProperty("title", out var titleElement)
                    ? titleElement.GetString() ?? instance.DisplayName
                    : instance.DisplayName;
                var body = root.TryGetProperty("body", out var bodyElement)
                    ? bodyElement.GetString() ?? string.Empty
                    : string.Empty;

                MessageAnalyticsService.Instance.RecordMessageReceived(instance.Id);

                hub.AddAlert(new NotificationAlert
                {
                    Id = Guid.NewGuid().ToString("N"),
                    InstanceId = instance.Id,
                    InstanceDisplayName = instance.DisplayName,
                    Platform = instance.Platform,
                    IconGlyph = instance.IconGlyph,
                    Title = title,
                    Body = body
                });

                return true;

            case AdapterMessageTypes.AdapterReady:
                var adapterId = root.TryGetProperty("adapterId", out var adapterElement)
                    ? adapterElement.GetString() ?? PlatformId
                    : PlatformId;
                health.MarkReady(instance.Id, adapterId);
                return true;

            case AdapterMessageTypes.Heartbeat:
                var heartbeatAdapterId = root.TryGetProperty("adapterId", out var heartbeatAdapterElement)
                    ? heartbeatAdapterElement.GetString()
                    : null;
                health.RecordHeartbeat(instance.Id, heartbeatAdapterId ?? PlatformId);
                return true;

            case AdapterMessageTypes.MessageSent:
                var chatHint = root.TryGetProperty("chatHint", out var chatHintElement)
                    ? chatHintElement.GetString()
                    : null;
                MessageAnalyticsService.Instance.RecordMessageSent(instance.Id, chatHint);
                return true;

            default:
                return false;
        }
    }

    private static async Task InjectScriptAsync(
        CoreWebView2 coreWebView,
        string scriptFileName,
        MessengerInstance instance,
        CancellationToken cancellationToken)
    {
        var script = await LoadScriptTemplateAsync(scriptFileName, cancellationToken);
        var includeMutedBadges = AppSettingsService.Instance.Settings.IncludeMutedChatBadges;
        script = script
            .Replace("__INSTANCE_ID__", instance.Id, StringComparison.Ordinal)
            .Replace("__PLATFORM__", instance.Platform, StringComparison.Ordinal)
            .Replace("__INCLUDE_MUTED_BADGES__", includeMutedBadges ? "true" : "false", StringComparison.Ordinal);

        await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private static async Task<string> LoadScriptTemplateAsync(
        string scriptFileName,
        CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Adapter script not found: {scriptPath}");
        }

        return await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PlatformAdapterFactory
{
    private static readonly WhatsAppAdapter WhatsApp = new();
    private static readonly TelegramAdapter Telegram = new();
    private static readonly MessengerAdapter Messenger = new();
    private static readonly GenericWebAdapter GenericWeb = new();

    public static IPlatformAdapter Resolve(string platformId) =>
        platformId.ToLowerInvariant() switch
        {
            "whatsapp" => WhatsApp,
            "telegram" => Telegram,
            "messenger" => Messenger,
            _ => GenericWeb
        };
}

public sealed class WhatsAppAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "whatsapp-adapter.js";

    public override string PlatformId => "whatsapp";
}

public sealed class TelegramAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "telegram-adapter.js";

    public override string PlatformId => "telegram";
}

public sealed class MessengerAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "messenger-adapter.js";

    public override string PlatformId => "messenger";
}

public sealed class GenericWebAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "generic-adapter.js";

    public override string PlatformId => "generic";
}

internal static class WebMessageParser
{
    public static JsonDocument Parse(string raw)
    {
        var document = JsonDocument.Parse(raw);
        if (document.RootElement.ValueKind == JsonValueKind.String)
        {
            var inner = document.RootElement.GetString() ?? "{}";
            document.Dispose();
            return JsonDocument.Parse(inner);
        }

        return document;
    }
}
