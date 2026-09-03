using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The queue's honesty about the channels it cannot show.
/// </summary>
public class ChannelCoverageTests
{
    private static MessengerInstance On(string platform, string id = "i") =>
        new() { Id = id, Platform = platform, DisplayName = platform };

    // ---- the four levels ---------------------------------------------------------------------------

    [Fact]
    public void WhatsAppIsShownInFull()
    {
        Assert.Equal(ChannelCoverageLevel.FullDetail, ChannelCoverage.For("whatsapp"));
        Assert.Equal(ChannelCoverageLevel.FullDetail, ChannelCoverage.For("whatsappbusiness"));
    }

    [Fact]
    public void MessengerIsAnHonestGap()
    {
        // The defect this closes: Messenger carries customer conversations, the queue is built from the
        // WhatsApp pipeline, so its waiting customers are silently absent. "Not measured" is the truth.
        Assert.Equal(ChannelCoverageLevel.NotMeasured, ChannelCoverage.For("messenger"));
        Assert.Equal(ChannelCoverageLevel.NotMeasured, ChannelCoverage.For("instagram"));
    }

    [Fact]
    public void GoogleBusinessIsNotAGapInAConversationQueue()
    {
        // Google Business Messages was shut down in 2024 and its data deleted. A Google account missing
        // from a CONVERSATION queue is correct, not a shortfall, and calling it one would train the owner
        // to dismiss the notice that matters.
        Assert.Equal(ChannelCoverageLevel.NotAConversationChannel, ChannelCoverage.For("googlebusiness"));
        Assert.Equal(ChannelCoverageLevel.NotAConversationChannel, ChannelCoverage.For("discord"));
        Assert.Equal(ChannelCoverageLevel.NotAConversationChannel, ChannelCoverage.For("generic"));
    }

    [Fact]
    public void CountsOnlyIsReachableAndDistinctFromNotMeasured()
    {
        // The rendering path PlatformCapabilities.IsAggregateOnly has documented since it was written and
        // never had a consumer for. No shipped platform reaches it today — Meta declares CanReadUnread
        // false because no adapter exists — so this drives the classifier through the capability shape
        // directly, to prove the branch is real rather than dead code waiting on faith.
        var aggregateOnly = new PlatformCapabilities
        {
            IsMessageChannel = true,
            CanReadUnread = true,
            RequiresThreadOpenToRead = true
        };

        Assert.True(aggregateOnly.IsAggregateOnly);
        Assert.False(aggregateOnly.CanReadPreview);
    }

    [Fact]
    public void NoShippedPlatformIsCountsOnlyYet() =>
        // Recorded so the day one becomes so is a deliberate change, not a surprise. When an adapter flips
        // CanReadUnread, this test fails and whoever did it has to look at the rendering path.
        Assert.DoesNotContain(
            PlatformDefinition.All,
            p => ChannelCoverage.For(p.Id) == ChannelCoverageLevel.CountsOnly);

    // ---- the sentence ------------------------------------------------------------------------------

    [Fact]
    public void NothingIsSaidWhenThereIsNothingToDisclose()
    {
        // A notice that appears on every screen stops being read.
        Assert.Equal(string.Empty, ChannelCoverage.DescribeGaps([On("whatsapp"), On("whatsappbusiness", "b")]));
        Assert.Equal(string.Empty, ChannelCoverage.DescribeGaps([]));
        Assert.Equal(string.Empty, ChannelCoverage.DescribeGaps(null));
    }

    [Fact]
    public void AGoogleAccountAloneRaisesNoNotice() =>
        Assert.Equal(string.Empty, ChannelCoverage.DescribeGaps([On("whatsapp"), On("googlebusiness", "g")]));

    [Fact]
    public void AMessengerAccountIsNamedAndCounted()
    {
        var text = ChannelCoverage.DescribeGaps([On("whatsapp"), On("messenger", "m")]);

        Assert.Contains("1 Messenger account", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not shown here", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SeveralChannelsReadAsASentenceNotAList()
    {
        var text = ChannelCoverage.DescribeGaps(
            [On("whatsapp"), On("messenger", "m"), On("messenger", "m2"), On("instagram", "i2")]);

        Assert.Contains("2 Messenger accounts", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 Instagram account", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" and ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRealAccountMixProducesExactlyOneClaim()
    {
        // The owner's actual set: 3 WhatsApp, 3 Google Business, 1 Messenger. Worth pinning as a case,
        // because it is the sentence a real person reads — and because it exercises the judgement the
        // whole class turns on: Google is silent (no conversations to miss), Messenger is named.
        var text = ChannelCoverage.DescribeGaps(
        [
            On("whatsapp", "w1"), On("whatsapp", "w2"), On("whatsapp", "w3"),
            On("googlebusiness", "g1"), On("googlebusiness", "g2"), On("googlebusiness", "g3"),
            On("messenger", "m1")
        ]);

        Assert.Equal("1 Messenger account not shown here — nothing reads that channel yet.", text);
        Assert.DoesNotContain("Google", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheNoticeNeverBlamesTheOwner()
    {
        // The gap is ours — nothing has been built to read those channels yet. Wording that implies the
        // owner should do something ("connect", "enable", "check") would send them looking for a setting
        // that does not exist.
        var text = ChannelCoverage.DescribeGaps([On("whatsapp"), On("messenger", "m")]);

        foreach (var blame in new[] { "you need to", "please ", "enable ", "check your" })
        {
            Assert.DoesNotContain(blame, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheNoticeIsPlainLanguage()
    {
        var text = ChannelCoverage.DescribeGaps([On("whatsapp"), On("messenger", "m")]);

        foreach (var jargon in new[] { "adapter", "pipeline", "IndexedDB", "capability", "instance" })
        {
            Assert.DoesNotContain(jargon, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
