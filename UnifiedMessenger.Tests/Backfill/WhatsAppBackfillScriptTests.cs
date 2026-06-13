namespace UnifiedMessenger.Tests.Backfill;

public class WhatsAppBackfillScriptTests
{
    [Fact]
    public void WhatsAppAdapter_ExposesBackfillCollectionApi()
    {
        var script = ReadScript("whatsapp-adapter.js");

        Assert.Contains("__umCollectBackfillCandidates", script, StringComparison.Ordinal);
        Assert.Contains("__umCommitInboundBaseline", script, StringComparison.Ordinal);
        Assert.Contains("__umSetBackfillOptions", script, StringComparison.Ordinal);
        Assert.Contains("isEligibleChatForBackfill", script, StringComparison.Ordinal);
        Assert.Contains("lastMessageBody", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppAdapter_BackfillSupportsUnreadRecentAndAllModes()
    {
        var script = ReadScript("whatsapp-adapter.js");

        Assert.Contains("mode === 'unread'", script, StringComparison.Ordinal);
        Assert.Contains("mode === 'recent'", script, StringComparison.Ordinal);
        Assert.Contains("mode === 'all'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppAdapter_ExposesSidebarSnapshotAndDailyAggregates()
    {
        var script = ReadScript("whatsapp-adapter.js");

        Assert.Contains("__umCollectSidebarSnapshot", script, StringComparison.Ordinal);
        Assert.Contains("__umCollectMessageDailyAggregates", script, StringComparison.Ordinal);
        Assert.Contains("whatsapp-sidebar-snapshot", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppAdapter_ExposesHistoryScrollBackAndDeepWalk()
    {
        var script = ReadScript("whatsapp-adapter.js");

        Assert.Contains("__umScrollBackOpenChatHistory", script, StringComparison.Ordinal);
        Assert.Contains("whatsapp-history-chunk", script, StringComparison.Ordinal);
        Assert.Contains("__umRunDeepBackfillWalk", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string scriptFileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        Assert.True(File.Exists(scriptPath), $"Missing adapter script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }
}
