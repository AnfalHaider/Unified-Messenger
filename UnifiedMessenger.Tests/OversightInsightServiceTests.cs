using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Tests;

public class OversightInsightServiceTests
{
    [Fact]
    public void Sanitize_ReturnsNullForEmpty()
    {
        Assert.Null(OversightInsightService.Sanitize(null));
        Assert.Null(OversightInsightService.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_CollapsesNewlinesAndTrimsQuotes()
    {
        var result = OversightInsightService.Sanitize("\"Reply to the 3 waiting customers now.\"\n");

        Assert.Equal("Reply to the 3 waiting customers now.", result);
    }

    [Fact]
    public void Sanitize_KeepsOnlyFirstSentenceWhenModelRambles()
    {
        var result = OversightInsightService.Sanitize(
            "Five customers are waiting, reply soon. Here is some extra commentary you did not ask for.");

        Assert.Equal("Five customers are waiting, reply soon.", result);
    }

    [Fact]
    public void Sanitize_StripsLeadingBulletMarkers()
    {
        var result = OversightInsightService.Sanitize("- Clear the 2 unread chats first.");

        Assert.Equal("Clear the 2 unread chats first.", result);
    }

    [Fact]
    public void Sanitize_TruncatesOverlongOutput()
    {
        var raw = new string('x', 250);

        var result = OversightInsightService.Sanitize(raw);

        Assert.NotNull(result);
        Assert.True(result!.Length <= 201);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void BuildPrompt_IncludesCountsAndUnreadPhrasing()
    {
        var facts = new OversightInsightFacts("Downtown Salon", 5, 3, 72, "2 hrs ago");

        var prompt = OversightInsightService.BuildPrompt(facts);

        Assert.Contains("Downtown Salon", prompt);
        Assert.Contains("5 customer(s)", prompt);
        Assert.Contains("3 of them unread", prompt);
        Assert.Contains("2 hrs ago", prompt);
        Assert.Contains("72% caught up", prompt);
    }

    [Fact]
    public void BuildPrompt_PhrasesAllOpenedWhenNoUnread()
    {
        var facts = new OversightInsightFacts("Mall Kiosk", 2, 0, 88, "15 min ago");

        var prompt = OversightInsightService.BuildPrompt(facts);

        Assert.Contains("all already opened but not yet replied", prompt);
    }

    [Fact]
    public void Request_DoesNotGenerateWhenAiDisabled()
    {
        var generated = false;
        var service = new OversightInsightService(
            aiEnabledProvider: () => false,
            modelProvider: () => { generated = true; return "phi3:mini"; });

        service.Request("acct-1", "sig", new OversightInsightFacts("A", 1, 0, 90, "now"), () => { });

        Assert.Null(service.TryGet("acct-1", "sig"));
        Assert.False(generated);
    }
}
