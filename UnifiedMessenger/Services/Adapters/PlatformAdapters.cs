using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;
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

    protected virtual IReadOnlyList<string> AdditionalScriptFileNames => [];

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
        var additionalScripts = new List<string>(AdditionalScriptFileNames.Count);
        foreach (var additionalScript in AdditionalScriptFileNames)
        {
            additionalScripts.Add(PrepareScript(
                await LoadScriptTemplateAsync(additionalScript, cancellationToken).ConfigureAwait(false),
                instance));
        }

        await UiThreadRunner.RunAsync(async () =>
        {
            coreWebView.Settings.IsWebMessageEnabled = true;
            await AddDocumentCreatedScriptAsync(coreWebView, coreScript, cancellationToken).ConfigureAwait(true);
            await AddDocumentCreatedScriptAsync(coreWebView, handshakeScript, cancellationToken).ConfigureAwait(true);
            await AddDocumentCreatedScriptAsync(coreWebView, adapterScript, cancellationToken).ConfigureAwait(true);

            foreach (var script in additionalScripts)
            {
                await AddDocumentCreatedScriptAsync(coreWebView, script, cancellationToken).ConfigureAwait(true);
            }

            RegisterNavigationHooks(coreWebView, instance);
        }).ConfigureAwait(true);

        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
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
            var additionalScripts = new List<string>();
            foreach (var additionalScript in AdditionalScriptFileNames)
            {
                additionalScripts.Add(PrepareScript(
                    await LoadScriptTemplateAsync(additionalScript, cancellationToken).ConfigureAwait(false),
                    instance));
            }

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

                foreach (var additionalScript in additionalScripts)
                {
                    await ExecuteScriptSafeAsync(coreWebView, additionalScript, cancellationToken)
                        .ConfigureAwait(true);
                }

                await ExecuteScriptSafeAsync(coreWebView, settingsScript, cancellationToken).ConfigureAwait(true);
                await ExecuteScriptSafeAsync(
                        coreWebView,
                        BuildConnectionHandshakeScript(instance),
                        cancellationToken)
                    .ConfigureAwait(true);

                if (SupportsInboundAutoDraft)
                {
                    await ExecuteScriptSafeAsync(
                            coreWebView,
                            "if (window.__umStartInboundMessageMonitor) { window.__umStartInboundMessageMonitor(); }",
                            cancellationToken)
                        .ConfigureAwait(true);
                }

                await ExecutePublishBadgeAsync(coreWebView).ConfigureAwait(true);
            }).ConfigureAwait(true);

            await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
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

            if (AdapterMessageTypes.UpdateThreadStatus.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                HandleUpdateThreadStatus(root, instance);
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

            _ = UiThreadRunner.RunAsync(() => OnNavigationCompletedAsync(coreWebView, instance));
        };
    }

    private async Task OnNavigationCompletedAsync(CoreWebView2 coreWebView, MessengerInstance instance)
    {
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

        InstanceConnectionStatusService.Instance.SetInitializing(instance.Id, "Loading workspace");

        try
        {
            await ExecuteScriptSafeAsync(
                    coreWebView,
                    BuildConnectionHandshakeScript(instance),
                    CancellationToken.None)
                .ConfigureAwait(true);

            await ExecutePublishBadgeAsync(coreWebView).ConfigureAwait(true);

            if (instance.IsProfessional &&
                PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
            {
                BackfillSyncManager.Instance.Schedule(instance);
            }
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

    protected static void HandleUpdateThreadStatus(JsonElement root, MessengerInstance instance)
    {
        try
        {
            var status = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;

            if (!"RESOLVED".Equals(status, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var conversationKey = root.TryGetProperty("conversationKey", out var keyElement)
                ? keyElement.GetString()
                : null;
            var customerName = root.TryGetProperty("customerName", out var customerElement)
                ? customerElement.GetString()
                : null;
            var source = root.TryGetProperty("source", out var sourceElement)
                ? sourceElement.GetString() ?? "webview"
                : "webview";

            UnifiedMessengerStateSyncService.Instance.EnqueueThreadResolved(
                instance.Id,
                conversationKey,
                customerName,
                ParseMessageTimestamp(root),
                source,
                instance.Platform);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Thread status update failed for {instance.Id}: {ex.Message}");
        }
    }

    protected static void HandleDashboardScrapeStatus(JsonElement root, MessengerInstance instance)
    {
    }

    protected static bool TryHandleInboundMessageSelected(
        string? type,
        JsonElement root,
        MessengerInstance instance)
    {
        if (!AdapterMessageTypes.InboundMessageSelected.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (instance.NotificationsMuted)
        {
            return true;
        }

        var messageText = root.TryGetProperty("messageText", out var messageElement)
            ? messageElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(messageText))
        {
            return true;
        }

        var conversationHint = root.TryGetProperty("conversationHint", out var hintElement)
            ? hintElement.GetString() ?? string.Empty
            : string.Empty;

        var conversationKeyRaw = root.TryGetProperty("conversationKey", out var keyElement)
            ? keyElement.GetString() ?? string.Empty
            : string.Empty;

        var resolvedKey = ConversationKeyResolver.Resolve(
            instance.Platform,
            conversationKeyRaw,
            conversationHint,
            root.TryGetProperty("customerName", out var nameProbe) ? nameProbe.GetString() : null,
            messageText);

        if (!BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, resolvedKey, messageText))
        {
            return true;
        }

        var customerName = root.TryGetProperty("customerName", out var customerElement)
            ? customerElement.GetString() ?? "Customer"
            : "Customer";
        var timestamp = ParseMessageTimestamp(root);
        MessageAnalyticsService.Instance.RecordMessageReceived(instance.Id, resolvedKey, timestamp);

        var selection = WhatsAppOperationalContextBuilder.IsWhatsAppPlatform(instance.Platform)
            ? WhatsAppIngressHandler.BuildInboundSelection(
                root, instance, resolvedKey, customerName, messageText, timestamp)
            : new InboundMessageSelection
            {
                InstanceId = instance.Id,
                Platform = instance.Platform,
                MessageText = messageText,
                CustomerName = customerName,
                ConversationKey = resolvedKey,
                ConversationHint = conversationHint,
                TimestampUtc = timestamp
            };

        MessageTriageService.Instance.Enqueue(
            selection,
            instance.DisplayName,
            BranchWorkspaceHelper.ResolveBranchKey(instance),
            skipDedupeCheck: true);

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
                var badgeCount = WebMessageParser.ReadNonNegativeInt(root, "count");
                if (muted)
                {
                    return true;
                }

                hub.UpdateBadgeCount(instance.Id, badgeCount);
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
                var previewConversationKeyRaw = root.TryGetProperty("conversationKey", out var previewKeyElement)
                    ? previewKeyElement.GetString() ?? string.Empty
                    : string.Empty;
                var previewCustomerName = root.TryGetProperty("customerName", out var previewCustomerElement)
                    ? previewCustomerElement.GetString()
                    : null;
                var resolvedPreviewKey = ConversationKeyResolver.Resolve(
                    instance.Platform,
                    previewConversationKeyRaw,
                    previewCustomerName ?? title,
                    previewCustomerName,
                    body);

                if (ShouldRecordPreviewForAnalytics(instance.Platform, title, body))
                {
                    MessageAnalyticsService.Instance.RecordMessageReceived(
                        instance.Id,
                        resolvedPreviewKey);
                }

                hub.AddAlert(NotificationAlert.Create(
                    instance.Id,
                    instance.DisplayName,
                    instance.Platform,
                    title,
                    body,
                    instance.IconGlyph,
                    conversationKey: resolvedPreviewKey,
                    customerName: previewCustomerName ?? title));

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
                var sentConversationKeyRaw = root.TryGetProperty("conversationKey", out var sentKeyElement)
                    ? sentKeyElement.GetString()
                    : null;
                var resolvedSentKey = ConversationKeyResolver.Resolve(
                    instance.Platform,
                    sentConversationKeyRaw,
                    chatHint,
                    chatHint,
                    null);
                MessageAnalyticsService.Instance.RecordMessageSent(instance.Id, chatHint);

                if (!WhatsAppOperationalContextBuilder.IsWhatsAppPlatform(instance.Platform))
                {
                    UnifiedMessengerStateSyncService.Instance.EnqueueThreadResolved(
                        instance.Id,
                        resolvedSentKey,
                        chatHint,
                        DateTimeOffset.UtcNow,
                        source: "message-sent",
                        platform: instance.Platform);
                }

                return true;

            default:
                return false;
        }
    }

    private static bool ShouldRecordPreviewForAnalytics(string platform, string title, string body) =>
        false;

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
        await WebViewUiAwaiter
            .AwaitAsync(
                coreWebView
                    .AddScriptToExecuteOnDocumentCreatedAsync(script)
                    .AsTask()
                    .WaitAsync(cancellationToken))
            .ConfigureAwait(true);
    }

    private static string PrepareScript(string script, MessengerInstance instance)
    {
        var includeMutedBadges = AppSettingsService.Instance.Settings.IncludeMutedChatBadges;
        return script
            .Replace("__INSTANCE_ID__", JsonSerializer.Serialize(instance.Id), StringComparison.Ordinal)
            .Replace(
                "__PLATFORM__",
                JsonSerializer.Serialize(PlatformDefinition.NormalizePlatformId(instance.Platform)),
                StringComparison.Ordinal)
            .Replace("__INCLUDE_MUTED_BADGES__", includeMutedBadges ? "true" : "false", StringComparison.Ordinal)
            .Replace("__NOTIFICATIONS_MUTED__", instance.NotificationsMuted ? "true" : "false", StringComparison.Ordinal)
            .Replace("__ENABLE_VOICE_NOTES__", "false", StringComparison.Ordinal)
            .Replace("__VOICE_NOTE_MAX_SECONDS__", "60", StringComparison.Ordinal);
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
        await WebViewUiAwaiter
            .AwaitAsync(coreWebView.ExecuteScriptAsync(script).AsTask().WaitAsync(cancellationToken))
            .ConfigureAwait(true);
    }

    protected static async Task ExecutePublishBadgeAsync(CoreWebView2 coreWebView)
    {
        try
        {
            await WebViewUiAwaiter
                .AwaitAsync(coreWebView.ExecuteScriptAsync(PublishBadgeScript).AsTask())
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Publish badge script failed: {ex.Message}");
        }
    }
}
