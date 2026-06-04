using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    private const string PublishBadgeScript =
        "if (window.__unifiedMessengerPublishBadge) { window.__unifiedMessengerPublishBadge(); }";

    private static readonly ConditionalWeakTable<CoreWebView2, object> RegisteredHosts = new();
    private static readonly Dictionary<string, string> ScriptTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object ScriptCacheLock = new();

    protected abstract string ScriptFileName { get; }

    public abstract string PlatformId { get; }

    public async Task RegisterAsync(
        CoreWebView2 coreWebView,
        MessengerInstance instance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);
        ArgumentNullException.ThrowIfNull(instance);

        if (!TryMarkRegistered(coreWebView))
        {
            return;
        }

        coreWebView.Settings.IsWebMessageEnabled = true;

        await InjectScriptAsync(coreWebView, "adapter-core.js", instance, cancellationToken).ConfigureAwait(false);
        await InjectScriptAsync(coreWebView, ScriptFileName, instance, cancellationToken).ConfigureAwait(false);

        RegisterNavigationHooks(coreWebView);
    }

    public async Task ReinjectAsync(
        CoreWebView2 coreWebView,
        MessengerInstance instance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);
        ArgumentNullException.ThrowIfNull(instance);

        try
        {
            await ExecuteScriptSafeAsync(
                    coreWebView,
                    "if (window.__umResetAdapterRuntime) { window.__umResetAdapterRuntime(); }",
                    cancellationToken)
                .ConfigureAwait(true);

            var coreScript = PrepareScript(await LoadScriptTemplateAsync("adapter-core.js", cancellationToken), instance);
            await ExecuteScriptSafeAsync(coreWebView, coreScript, cancellationToken).ConfigureAwait(true);

            var adapterScript = PrepareScript(await LoadScriptTemplateAsync(ScriptFileName, cancellationToken), instance);
            await ExecuteScriptSafeAsync(coreWebView, adapterScript, cancellationToken).ConfigureAwait(true);

            await ExecuteScriptSafeAsync(coreWebView, BuildAdapterSettingsScript(), cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Adapter reinject failed for {instance.Id}: {ex.Message}");
            throw;
        }
    }

    public void HandleWebMessage(string messageJson, NotificationHub hub, MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(instance);

        try
        {
            using var document = WebMessageParser.Parse(messageJson);
            var root = document.RootElement;

            if (!WebMessageParser.MatchesInstance(root, instance))
            {
                return;
            }

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            if (HandleStandardMessage(type, root, hub, instance))
            {
                return;
            }

            if (!AdapterMessageTypes.IsKnownType(type))
            {
                Debug.WriteLine($"Ignoring unknown adapter message type '{type}' for {instance.Id}.");
                return;
            }

            HandleCustomMessage(type, root, hub, instance);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"WebMessage parse failed: {ex.Message} | Raw: {messageJson}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebMessage handling failed: {ex.Message} | Raw: {messageJson}");
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

            _ = ExecutePublishBadgeAsync(coreWebView);
        };
    }

    protected virtual bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance) => false;

    protected static DateTimeOffset ParseMessageTimestamp(JsonElement root) =>
        WebMessageParser.ReadTimestampUtc(root, DateTimeOffset.UtcNow);

    private bool HandleStandardMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        if (!AdapterMessageTypes.IsStandardType(type))
        {
            return false;
        }

        var health = AdapterHealthMonitor.Instance;
        var muted = instance.NotificationsMuted;

        switch (type)
        {
            case AdapterMessageTypes.BadgeCount:
                if (muted)
                {
                    return true;
                }

                hub.UpdateBadgeCount(instance.Id, WebMessageParser.ReadNonNegativeInt(root, "count"));
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

                hub.AddAlert(NotificationAlert.Create(
                    instance.Id,
                    instance.DisplayName,
                    instance.Platform,
                    title,
                    body,
                    instance.IconGlyph));

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
                if (IsMetaBusinessPlatform(instance.Platform))
                {
                    ProfessionalWorkspaceService.Instance.HandleMetaReplySent(
                        instance.Id,
                        DateTimeOffset.UtcNow);
                }

                return true;

            default:
                return false;
        }
    }

    private static bool IsMetaBusinessPlatform(string platformId) =>
        platformId.Equals("metabusiness", StringComparison.OrdinalIgnoreCase);

    private static bool TryMarkRegistered(CoreWebView2 coreWebView)
    {
        if (RegisteredHosts.TryGetValue(coreWebView, out _))
        {
            return false;
        }

        RegisteredHosts.Add(coreWebView, null!);
        return true;
    }

    private static async Task InjectScriptAsync(
        CoreWebView2 coreWebView,
        string scriptFileName,
        MessengerInstance instance,
        CancellationToken cancellationToken)
    {
        var script = PrepareScript(await LoadScriptTemplateAsync(scriptFileName, cancellationToken), instance);
        await coreWebView
            .AddScriptToExecuteOnDocumentCreatedAsync(script)
            .AsTask()
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string PrepareScript(string script, MessengerInstance instance)
    {
        var includeMutedBadges = AppSettingsService.Instance.Settings.IncludeMutedChatBadges;
        return script
            .Replace("__INSTANCE_ID__", instance.Id, StringComparison.Ordinal)
            .Replace("__PLATFORM__", PlatformDefinition.NormalizePlatformId(instance.Platform), StringComparison.Ordinal)
            .Replace("__INCLUDE_MUTED_BADGES__", includeMutedBadges ? "true" : "false", StringComparison.Ordinal);
    }

    private static async Task<string> LoadScriptTemplateAsync(
        string scriptFileName,
        CancellationToken cancellationToken)
    {
        lock (ScriptCacheLock)
        {
            if (ScriptTemplates.TryGetValue(scriptFileName, out var cached))
            {
                return cached;
            }
        }

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Adapter script not found: {scriptPath}");
        }

        var content = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);

        lock (ScriptCacheLock)
        {
            ScriptTemplates[scriptFileName] = content;
        }

        return content;
    }

    private static string BuildAdapterSettingsScript()
    {
        var includeMuted = AppSettingsService.Instance.Settings.IncludeMutedChatBadges ? "true" : "false";
        return $"window.__umIncludeMutedBadges = {includeMuted}; {PublishBadgeScript}";
    }

    private static async Task ExecuteScriptSafeAsync(
        CoreWebView2 coreWebView,
        string script,
        CancellationToken cancellationToken)
    {
        await coreWebView.ExecuteScriptAsync(script).AsTask().WaitAsync(cancellationToken).ConfigureAwait(true);
    }

    protected static async Task ExecutePublishBadgeAsync(CoreWebView2 coreWebView)
    {
        try
        {
            await coreWebView.ExecuteScriptAsync(PublishBadgeScript).AsTask().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Publish badge script failed: {ex.Message}");
        }
    }
}

public sealed class PlatformAdapterFactory
{
    private static readonly WhatsAppAdapter WhatsApp = new();
    private static readonly TelegramAdapter Telegram = new();
    private static readonly MessengerAdapter Messenger = new();
    private static readonly MetaBusinessAdapter MetaBusiness = new();
    private static readonly GoogleBusinessAdapter GoogleBusiness = new();
    private static readonly SlackAdapter Slack = new();
    private static readonly DiscordAdapter Discord = new();
    private static readonly SignalAdapter Signal = new();
    private static readonly TeamsAdapter Teams = new();
    private static readonly GenericWebAdapter GenericWeb = new();

    public static IPlatformAdapter Resolve(string platformId) =>
        PlatformDefinition.NormalizePlatformId(platformId) switch
        {
            "whatsapp" => WhatsApp,
            "telegram" => Telegram,
            "messenger" => Messenger,
            "metabusiness" => MetaBusiness,
            "googlebusiness" => GoogleBusiness,
            "slack" => Slack,
            "discord" => Discord,
            "signal" => Signal,
            "teams" => Teams,
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

    protected override void RegisterNavigationHooks(CoreWebView2 coreWebView)
    {
        base.RegisterNavigationHooks(coreWebView);

        coreWebView.HistoryChanged += (sender, args) => _ = ExecutePublishBadgeAsync(coreWebView);
    }
}

public sealed class MetaBusinessAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "meta_business_scraper.js";

    public override string PlatformId => "metabusiness";

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        if (!AdapterMessageTypes.MetaInboundMessage.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ProfessionalWorkspaceService.Instance.HandleMetaInboundMessage(
            instance.Id,
            ParseMessageTimestamp(root),
            WebMessageParser.ReadNonNegativeInt(root, "unreadCount"));

        return true;
    }
}

public sealed class GoogleBusinessAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "google_business_scraper.js";

    public override string PlatformId => "googlebusiness";

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        switch (type)
        {
            case AdapterMessageTypes.GoogleReviewSnapshot:
                var unreplied = WebMessageParser.ReadNonNegativeInt(root, "unrepliedCount");

                ProfessionalWorkspaceService.Instance.HandleGoogleReviewSnapshot(
                    instance.Id,
                    instance.DisplayName,
                    unreplied);

                if (!instance.NotificationsMuted)
                {
                    hub.UpdateBadgeCount(instance.Id, unreplied);
                }

                return true;

            case AdapterMessageTypes.GoogleReviewAlert:
                if (instance.NotificationsMuted)
                {
                    return true;
                }

                var reviewId = root.TryGetProperty("reviewId", out var reviewIdElement)
                    ? reviewIdElement.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N");
                var reviewer = root.TryGetProperty("reviewerName", out var reviewerElement)
                    ? reviewerElement.GetString() ?? "Customer"
                    : "Customer";
                var snippet = root.TryGetProperty("snippet", out var snippetElement)
                    ? snippetElement.GetString() ?? string.Empty
                    : string.Empty;
                var location = root.TryGetProperty("locationLabel", out var locationElement)
                    ? locationElement.GetString() ?? instance.DisplayName
                    : instance.DisplayName;
                var rating = WebMessageParser.ReadNonNegativeInt(root, "rating");
                var detectedAt = ParseMessageTimestamp(root);

                ProfessionalWorkspaceService.Instance.HandleGoogleReviewAlert(
                    instance.Id,
                    instance.DisplayName,
                    reviewId,
                    reviewer,
                    snippet,
                    location,
                    rating,
                    detectedAt);

                hub.AddAlert(NotificationAlert.Create(
                    instance.Id,
                    instance.DisplayName,
                    instance.Platform,
                    $"{reviewer} · review",
                    snippet,
                    instance.IconGlyph));

                return true;

            default:
                return false;
        }
    }
}

public sealed class SlackAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "slack-adapter.js";

    public override string PlatformId => "slack";
}

public sealed class DiscordAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "discord-adapter.js";

    public override string PlatformId => "discord";
}

public sealed class SignalAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "signal-adapter.js";

    public override string PlatformId => "signal";
}

public sealed class TeamsAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "teams-adapter.js";

    public override string PlatformId => "teams";
}

public sealed class GenericWebAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "generic-adapter.js";

    public override string PlatformId => "generic";
}
