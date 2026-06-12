namespace UnifiedMessenger.Tests;

public class AdapterCoreScriptTests
{
    private static string ReadAdapterCoreScript()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "adapter-core.js");
        Assert.True(File.Exists(scriptPath), $"Missing adapter core script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }

    [Fact]
    public void AdapterCoreScript_ExistsInOutput()
    {
        ReadAdapterCoreScript();
    }

    [Fact]
    public void AdapterCoreScript_ContainsMutedBadgePlaceholder()
    {
        var script = ReadAdapterCoreScript();

        Assert.Contains("__INCLUDE_MUTED_BADGES__", script, StringComparison.Ordinal);
        Assert.Contains("__umShouldIncludeMutedBadges", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterCoreScript_ResetClearsInterceptorAndListenerState()
    {
        var script = ReadAdapterCoreScript();

        Assert.Contains("__umResetAdapterRuntime", script, StringComparison.Ordinal);
        Assert.Contains("delete window.__umNotificationInterceptorInstalled", script, StringComparison.Ordinal);
        Assert.Contains("removeEventListener('keydown'", script, StringComparison.Ordinal);
        Assert.Contains("__umOriginalNotification", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterCoreScript_ResetClearsSecondaryScriptInstallGuards()
    {
        var script = ReadAdapterCoreScript();

        string[] secondaryInstallGuards =
        [
            "delete window.__umInboundMonitorInstalled",
            "delete window.__umConversationContextInstalled",
            "delete window.__umConnectionHandshakeInstalled",
            "delete window.__umAiDraftInjectInstalled",
            "delete window.__umVoiceMonitorInstalled",
            "delete window.__umThreadStatusAuditorCore",
            "delete window.__umThreadStatusAuditorInstalls",
            "delete window.__umWhatsAppAuditorInstalled"
        ];

        foreach (var guard in secondaryInstallGuards)
        {
            Assert.Contains(guard, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AdapterCoreScript_SupportsAdapterDisposeHook()
    {
        var script = ReadAdapterCoreScript();

        Assert.Contains("__umAdapterDispose", script, StringComparison.Ordinal);
        Assert.Contains("__umRegisterDisposable", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterCoreScript_SupportsDomReplyDetection()
    {
        var script = ReadAdapterCoreScript();

        Assert.Contains("__umEmitMessageSent", script, StringComparison.Ordinal);
        Assert.Contains("__umInstallOutgoingDomReplyMonitor", script, StringComparison.Ordinal);
        Assert.Contains("dom-outgoing", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterCoreScript_PrunesPreviewThrottleMap()
    {
        var script = ReadAdapterCoreScript();

        Assert.Contains("previewMaxEntries", script, StringComparison.Ordinal);
        Assert.Contains("pruneRecentPreviews", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterCoreScript_ExposesCanonicalActiveChatJidResolver()
    {
        var script = ReadAdapterCoreScript();

        Assert.Contains("__umResolveActiveChatJid", script, StringComparison.Ordinal);
        Assert.Contains("conversation-info-header", script, StringComparison.Ordinal);
    }
}
