using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The card chip vocabulary (Increment 120).
///
/// <para>
/// The words live on <see cref="ChannelCoverage"/> rather than in the panel because the same idea has to
/// appear on the account card, in the queue's branch header and in the leaderboard. Three surfaces
/// inventing three phrasings is how "not shown", "unavailable" and "no data" came to mean one thing in
/// different places, each reading to the owner as a different problem.
/// </para>
/// </summary>
public class ChannelCoverageChipTests
{
    [Theory]
    [InlineData(ChannelCoverageLevel.CountsOnly)]
    [InlineData(ChannelCoverageLevel.NotMeasured)]
    [InlineData(ChannelCoverageLevel.NotAConversationChannel)]
    public void EveryLevelWorthShowingHasBothALabelAndAnExplanation(ChannelCoverageLevel level)
    {
        Assert.True(ChannelCoverage.ShouldShowChip(level));
        Assert.False(string.IsNullOrWhiteSpace(ChannelCoverage.ChipLabel(level)));

        // A two-word badge cannot carry the meaning on its own. The tooltip is also what a screen reader
        // announces, so an empty one leaves the chip unreadable rather than merely terse (WCAG 1.4.1).
        Assert.False(string.IsNullOrWhiteSpace(ChannelCoverage.ChipTooltip(level)));
    }

    [Fact]
    public void AFullyMeasuredAccountShowsNoChip()
    {
        // A badge on every card is decoration, and the eye stops reading one that is always there. The
        // chip has to mean "something is missing here" to be worth the space.
        Assert.False(ChannelCoverage.ShouldShowChip(ChannelCoverageLevel.FullDetail));
    }

    [Fact]
    public void LabelsAreShortEnoughForACardChip()
    {
        foreach (var level in Enum.GetValues<ChannelCoverageLevel>())
        {
            var label = ChannelCoverage.ChipLabel(level);
            Assert.True(
                label.Length <= 20,
                $"'{label}' is too long for a chip beside an account name; the sentence belongs in the tooltip.");
        }
    }

    [Fact]
    public void GoogleIsDescribedAsAReviewChannelRatherThanAsAGap()
    {
        var google = Instance("googlebusiness");
        var level = ChannelCoverage.For(google);

        Assert.Equal(ChannelCoverageLevel.NotAConversationChannel, level);

        // Google Business Messages was shut down in 2024 and the data deleted. Labelling it "not measured"
        // would read as a shortfall the app might one day fix, and it never will.
        Assert.Equal("Reviews only", ChannelCoverage.ChipLabel(level));
        Assert.Contains("2024", ChannelCoverage.ChipTooltip(level), StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppNeedsNoChipBecauseEveryRowIsPresent()
    {
        Assert.Equal(ChannelCoverageLevel.FullDetail, ChannelCoverage.For(Instance("whatsapp")));
        Assert.False(ChannelCoverage.ShouldShowChip(ChannelCoverage.For(Instance("whatsapp"))));
    }

    [Fact]
    public void AChannelNothingReadsYetSaysSo()
    {
        var level = ChannelCoverage.For(Instance("messenger"));

        Assert.Equal(ChannelCoverageLevel.NotMeasured, level);
        Assert.Equal("Not measured", ChannelCoverage.ChipLabel(level));
        Assert.Contains("contributes no figures", ChannelCoverage.ChipTooltip(level), StringComparison.Ordinal);
    }

    [Fact]
    public void TooltipsReadAsSentencesRatherThanFragments()
    {
        foreach (var level in Enum.GetValues<ChannelCoverageLevel>())
        {
            var tooltip = ChannelCoverage.ChipTooltip(level);
            if (string.IsNullOrEmpty(tooltip))
            {
                continue;
            }

            Assert.EndsWith(".", tooltip, StringComparison.Ordinal);
            Assert.True(char.IsUpper(tooltip[0]), $"'{tooltip}' should start as a sentence.");
        }
    }

    private static MessengerInstance Instance(string platform) => new()
    {
        Id = $"cov-{platform}",
        DisplayName = platform,
        Platform = platform
    };
}
