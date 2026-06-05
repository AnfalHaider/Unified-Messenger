namespace UnifiedMessenger.Tests;

public class AutoDraftScriptTests
{
    [Fact]
    public void AiDraftInjectScript_DoesNotAutoSend()
    {
        var script = ReadScript("ai-draft-inject.js");

        Assert.Contains("__umInjectDraftReply", script, StringComparison.Ordinal);
        Assert.Contains("data-um-draft", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".click()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("taskkill", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InboundMessageMonitor_PostsSelectionMessage()
    {
        var script = ReadScript("inbound-message-monitor.js");

        Assert.Contains("inbound-message-selected", script, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("__umStartInboundMessageMonitor", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AiDraftInjectScript_SupportsRafStreamChunks()
    {
        var script = ReadScript("ai-draft-inject.js");

        Assert.Contains("__umAppendDraftChunk", script, StringComparison.Ordinal);
        Assert.Contains("__umFinalizeDraftStream", script, StringComparison.Ordinal);
        Assert.Contains("requestAnimationFrame", script, StringComparison.Ordinal);
        Assert.Contains("insertText", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", fileName);
        return File.ReadAllText(path);
    }
}
