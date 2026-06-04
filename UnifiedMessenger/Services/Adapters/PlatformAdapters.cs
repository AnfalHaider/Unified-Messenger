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

    protected virtual bool SupportsInboundAutoDraft => false;

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

        var coreScript = PrepareScript(
            await LoadScriptTemplateAsync("adapter-core.js", cancellationToken).ConfigureAwait(false),
            instance);
        var handshakeScript = PrepareScript(
            await LoadScriptTemplateAsync("connection-handshake.js", cancellationToken).ConfigureAwait(false),
            instance);
        var adapterScript = PrepareScript(
            await LoadScriptTemplateAsync(ScriptFileName, cancellationToken).ConfigureAwait(false),
            instance);
        var conversationContextScript = PrepareScript(
            await LoadScriptTemplateAsync("conversation-context-scraper.js", cancellationToken).ConfigureAwait(false),
            instance);
        string? draftInjectScript = null;
        string? inboundMonitorScript = null;

        if (SupportsInboundAutoDraft)
        {
            draftInjectScript = PrepareScript(
                await LoadScriptTemplateAsync("ai-draft-inject.js", cancellationToken).ConfigureAwait(false),
                instance);
            inboundMonitorScript = PrepareScript(
                await LoadScriptTemplateAsync("inbound-message-monitor.js", cancellationToken).ConfigureAwait(false),
                instance);
        }

        await UiThreadRunner.RunAsync(async () =>
        {
            coreWebView.Settings.IsWebMessageEnabled = true;
            await AddDocumentCreatedScriptAsync(coreWebView, coreScript, cancellationToken).ConfigureAwait(true);
            await AddDocumentCreatedScriptAsync(coreWebView, handshakeScript, cancellationToken).ConfigureAwait(true);
            await AddDocumentCreatedScriptAsync(coreWebView, adapterScript, cancellationToken).ConfigureAwait(true);
            await AddDocumentCreatedScriptAsync(coreWebView, conversationContextScript, cancellationToken)
                .ConfigureAwait(true);

            if (draftInjectScript is not null)
            {
                await AddDocumentCreatedScriptAsync(coreWebView, draftInjectScript, cancellationToken)
                    .ConfigureAwait(true);
            }

            if (inboundMonitorScript is not null)
            {
                await AddDocumentCreatedScriptAsync(coreWebView, inboundMonitorScript, cancellationToken)
                    .ConfigureAwait(true);
            }

            RegisterNavigationHooks(coreWebView, instance);
        }).ConfigureAwait(true);
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
            var coreScript = PrepareScript(
                await LoadScriptTemplateAsync("adapter-core.js", cancellationToken).ConfigureAwait(false),
                instance);
            var adapterScript = PrepareScript(
                await LoadScriptTemplateAsync(ScriptFileName, cancellationToken).ConfigureAwait(false),
                instance);
            var settingsScript = BuildAdapterSettingsScript();

            await UiThreadRunner.RunAsync(async () =>
            {
                await ExecuteScriptSafeAsync(
                        coreWebView,
                        "if (window.__umResetAdapterRuntime) { window.__umResetAdapterRuntime(); }",
                        cancellationToken)
                    .ConfigureAwait(true);
                await ExecuteScriptSafeAsync(coreWebView, coreScript, cancellationToken).ConfigureAwait(true);
                await ExecuteScriptSafeAsync(coreWebView, adapterScript, cancellationToken).ConfigureAwait(true);
                await ExecuteScriptSafeAsync(coreWebView, settingsScript, cancellationToken).ConfigureAwait(true);
            }).ConfigureAwait(true);
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

            if (AdapterMessageTypes.InboundMessageSelected.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                if (!SupportsInboundAutoDraft)
                {
                    Debug.WriteLine(
                        $"Ignoring inbound message for {instance.Id}; adapter does not support inbound monitoring.");
                    return;
                }

                TryHandleInboundMessageSelected(type, root, instance);
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

    protected virtual void RegisterNavigationHooks(CoreWebView2 coreWebView, MessengerInstance instance)
    {
        coreWebView.NavigationCompleted += (sender, args) =>
        {
            if (!args.IsSuccess)
            {
                InstanceConnectionStatusService.Instance.SetError(
                    instance.Id,
                    args.WebErrorStatus.ToString());
                return;
            }

            _ = OnNavigationCompletedAsync(coreWebView, instance);
        };
    }

    private async Task OnNavigationCompletedAsync(CoreWebView2 coreWebView, MessengerInstance instance)
    {
        InstanceConnectionStatusService.Instance.SetInitializing(instance.Id, "Loading workspace");

        try
        {
            await ExecuteScriptSafeAsync(
                    coreWebView,
                    BuildConnectionHandshakeScript(instance),
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (SupportsInboundAutoDraft)
            {
                await ExecuteScriptSafeAsync(
                        coreWebView,
                        "if (window.__umStartInboundMessageMonitor) { window.__umStartInboundMessageMonitor(); }",
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }

            await ExecutePublishBadgeAsync(coreWebView).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Connection handshake failed for {instance.Id}: {ex.Message}");
            InstanceConnectionStatusService.Instance.SetError(instance.Id, ex.Message);
        }
    }

    private static string BuildConnectionHandshakeScript(MessengerInstance instance)
    {
        var instanceId = JsonSerializer.Serialize(instance.Id);
        var platform = JsonSerializer.Serialize(PlatformDefinition.NormalizePlatformId(instance.Platform));
        return $"window.__umStartConnectionHandshake({instanceId}, {platform});";
    }

    protected virtual bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance) => false;

    protected static DateTimeOffset ParseMessageTimestamp(JsonElement root) =>
        WebMessageParser.ReadTimestampUtc(root, DateTimeOffset.UtcNow);

    protected static void HandleDashboardScrapeStatus(JsonElement root, MessengerInstance instance) =>
        DashboardScrapeStatusHandler.Apply(root, instance);

    protected static bool TryHandleInboundMessageSelected(
        string? type,
        JsonElement root,
        MessengerInstance instance)
    {
        if (!AdapterMessageTypes.InboundMessageSelected.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var messageText = root.TryGetProperty("messageText", out var messageElement)
            ? messageElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(messageText))
        {
            return true;
        }

        var customerName = root.TryGetProperty("customerName", out var customerElement)
            ? customerElement.GetString() ?? "Customer"
            : "Customer";
        var conversationHint = root.TryGetProperty("conversationHint", out var hintElement)
            ? hintElement.GetString() ?? string.Empty
            : string.Empty;

        MessageAnalyticsService.Instance.RecordMessageReceived(instance.Id);

        var selection = new InboundMessageSelection
        {
            InstanceId = instance.Id,
            Platform = instance.Platform,
            MessageText = messageText,
            CustomerName = customerName,
            ConversationHint = conversationHint,
            TimestampUtc = ParseMessageTimestamp(root)
        };

        MessageTriageService.Instance.Enqueue(selection, instance.DisplayName);
        AutoDraftOrchestrator.Instance.HandleInboundMessage(selection);

        return true;
    }

    private static void ApplyConnectionStatus(MessengerInstance instance, string? statusRaw, JsonElement root)
    {
        var detail = root.TryGetProperty("detail", out var detailElement)
            ? detailElement.GetString()
            : null;

        var messageInstanceId = root.TryGetProperty("instanceId", out var instanceIdElement)
            ? instanceIdElement.GetString()
            : null;
        var targetId = string.IsNullOrWhiteSpace(messageInstanceId) ? instance.Id : messageInstanceId.Trim();

        InstanceConnectionStatusService.Instance.ApplyConnectionStatus(targetId, statusRaw, detail);
        instance.Status = InstanceConnectionStatusService.Instance.GetStatus(targetId);

        if (instance.Status == InstanceConnectionStatus.Connected)
        {
            AdapterHealthMonitor.Instance.MarkReady(
                targetId,
                PlatformDefinition.NormalizePlatformId(instance.Platform));
        }
    }

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

            case AdapterMessageTypes.ConnectionStatus:
                var statusRaw = root.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString()
                    : null;
                ApplyConnectionStatus(instance, statusRaw, root);
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

    private static async Task AddDocumentCreatedScriptAsync(
        CoreWebView2 coreWebView,
        string script,
        CancellationToken cancellationToken)
    {
        await coreWebView
            .AddScriptToExecuteOnDocumentCreatedAsync(script)
            .AsTask()
            .WaitAsync(cancellationToken)
            .ConfigureAwait(true);
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

    protected override bool SupportsInboundAutoDraft => true;

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

    protected override void RegisterNavigationHooks(CoreWebView2 coreWebView, MessengerInstance instance)
    {
        base.RegisterNavigationHooks(coreWebView, instance);

        coreWebView.HistoryChanged += (sender, e) =>
        {
            _ = ExecutePublishBadgeAsync(coreWebView);
        };
    }
}

public sealed class MetaBusinessAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "meta_business_scraper.js";

    protected override bool SupportsInboundAutoDraft => true;

    public override string PlatformId => "metabusiness";

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        if (AdapterMessageTypes.DashboardScrapeStatus.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleDashboardScrapeStatus(root, instance);
            return true;
        }

        if (AdapterMessageTypes.MetaTelemetrySnapshot.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            var avgMinutes = WebMessageParser.ReadOptionalDouble(root, "averageResponseMinutes");
            var slaHints = WebMessageParser.ReadNonNegativeInt(root, "slaBreachHints");
            var unread = WebMessageParser.ReadNonNegativeInt(root, "unreadCount");
            ProfessionalWorkspaceService.Instance.HandleMetaTelemetrySnapshot(
                instance.Id,
                avgMinutes,
                slaHints,
                unread);
            return true;
        }

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

    protected override bool SupportsInboundAutoDraft => true;

    public override string PlatformId => "googlebusiness";

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        if (AdapterMessageTypes.DashboardScrapeStatus.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleDashboardScrapeStatus(root, instance);
            return true;
        }

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

                if (!string.IsNullOrWhiteSpace(snippet))
                {
                    var selection = new InboundMessageSelection
                    {
                        InstanceId = instance.Id,
                        Platform = instance.Platform,
                        MessageText = snippet,
                        CustomerName = reviewer,
                        ConversationHint = location,
                        TimestampUtc = detectedAt
                    };

                    MessageTriageService.Instance.Enqueue(selection, instance.DisplayName);
                    AutoDraftOrchestrator.Instance.HandleInboundMessage(selection);
                }

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
