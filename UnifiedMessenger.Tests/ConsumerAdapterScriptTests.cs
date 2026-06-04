namespace UnifiedMessenger.Tests;

public class ConsumerAdapterScriptTests
{
    public static TheoryData<string> ConsumerAdapters => new()
    {
        "whatsapp-adapter.js",
        "telegram-adapter.js",
        "messenger-adapter.js",
        "slack-adapter.js",
        "discord-adapter.js",
        "signal-adapter.js",
        "teams-adapter.js"
    };

    [Theory]
    [MemberData(nameof(ConsumerAdapters))]
    public void ConsumerAdapter_RegistersDisposeHook(string scriptFileName)
    {
        var script = ReadScript(scriptFileName);

        Assert.Contains("__umAdapterDispose", script, StringComparison.Ordinal);
        Assert.Contains("__umRegisterDisposable", script, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ConsumerAdapters))]
    public void ConsumerAdapter_DebouncesDomUpdates(string scriptFileName)
    {
        var script = ReadScript(scriptFileName);

        Assert.Contains("schedulePublishBadgeCount", script, StringComparison.Ordinal);
        Assert.Contains("publishBadgeCountImmediate", script, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ConsumerAdapters))]
    public void ConsumerAdapter_HooksSpaNavigation(string scriptFileName)
    {
        var script = ReadScript(scriptFileName);

        Assert.Contains("hookSpaNavigation", script, StringComparison.Ordinal);
        Assert.Contains("history.pushState", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppAdapter_UsesIndexedDbAndDomFallbacks()
    {
        var script = ReadScript("whatsapp-adapter.js");

        Assert.Contains("readChatsFromIndexedDb", script, StringComparison.Ordinal);
        Assert.Contains("countFromDomBadges", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TelegramAdapter_SupportsWebKAndWebZ()
    {
        var script = ReadScript("telegram-adapter.js");

        Assert.Contains("countWebK", script, StringComparison.Ordinal);
        Assert.Contains("countWebZ", script, StringComparison.Ordinal);
        Assert.Contains("__umShouldIncludeMutedBadges", script, StringComparison.Ordinal);
    }

    [Fact]
    public void MessengerAdapter_UsesResilientUnreadSelectors()
    {
        var script = ReadScript("messenger-adapter.js");

        Assert.Contains("mw_unread_count", script, StringComparison.Ordinal);
        Assert.Contains("MWUnreadBadge", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SignalAdapter_ObservesTitleAndDomFallbacks()
    {
        var script = ReadScript("signal-adapter.js");

        Assert.Contains("observeTitle", script, StringComparison.Ordinal);
        Assert.Contains("__umCountFromTitle", script, StringComparison.Ordinal);
        Assert.Contains("countFromDom", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TeamsAdapter_UsesActivityBadgeSelectors()
    {
        var script = ReadScript("teams-adapter.js");

        Assert.Contains("activity-feed-badge", script, StringComparison.Ordinal);
        Assert.Contains("team-channels-unread-count", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscordAdapter_DeduplicatesBadgeNodes()
    {
        var script = ReadScript("discord-adapter.js");

        Assert.Contains("seen", script, StringComparison.Ordinal);
        Assert.Contains("unreadBadge", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string scriptFileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        Assert.True(File.Exists(scriptPath), $"Missing adapter script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }
}
