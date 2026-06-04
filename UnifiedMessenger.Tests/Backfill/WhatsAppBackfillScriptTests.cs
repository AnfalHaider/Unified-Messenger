namespace UnifiedMessenger.Tests.Backfill;

public class WhatsAppBackfillScriptTests
{
    [Fact]
    public void WhatsAppAdapter_ExposesBackfillCollectionApi()
    {
        var script = ReadScript("whatsapp-adapter.js");

        Assert.Contains("__umCollectBackfillCandidates", script, StringComparison.Ordinal);
        Assert.Contains("__umCommitInboundBaseline", script, StringComparison.Ordinal);
        Assert.Contains("snapshotsInitialized = true", script, StringComparison.Ordinal);
        Assert.Contains("lastMessageBody", script, StringComparison.Ordinal);
        Assert.Contains("unreadCount", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppAdapter_BackfillUsesIndexedDbUnreadChats()
    {
        var script = ReadScript("whatsapp-adapter.js");

        Assert.Contains("readChatsFromIndexedDb", script, StringComparison.Ordinal);
        Assert.Contains("chat.unreadCount", script, StringComparison.Ordinal);
        Assert.Contains("chatSnapshots[chatKey]", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string scriptFileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        Assert.True(File.Exists(scriptPath), $"Missing adapter script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }
}
