using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The line under a dashboard KPI saying what that figure covers (mockup §02, Increment 133).
///
/// <para>
/// The account cards' chips say what an <i>account</i> can supply. A KPI is an aggregate across accounts,
/// and the answer differs per figure: "Response time" is WhatsApp-only on a dashboard where Instagram
/// contributes to "Backlog". One notice at the bottom of the page cannot tell the owner which of six
/// numbers in front of them is narrower than it looks.
/// </para>
/// </summary>
public class KpiCoverageTests
{
    private static string NewId() => $"kpi-{Guid.NewGuid():N}";

    private static MessengerInstance Account(string platform, bool signedOut = false, bool professional = true)
    {
        var id = NewId();
        if (signedOut)
        {
            InstanceConnectionStatusService.Instance.SetLoggedOut(id, "Sign-in screen");
        }
        else
        {
            InstanceConnectionStatusService.Instance.SetConnected(id, "Signed in");
        }

        return new MessengerInstance
        {
            Id = id,
            DisplayName = platform,
            Platform = platform,
            Category = professional ? WorkspaceCategory.Professional : WorkspaceCategory.Personal
        };
    }

    [Fact]
    public void AFigureThatCoversEveryAccountSaysNothing()
    {
        var accounts = new[] { Account("whatsapp"), Account("whatsapp") };

        // A caption on every tile is furniture, and the tiles that need the disclosure stop standing out.
        Assert.Equal(string.Empty, KpiCoverage.ForConversations(accounts));
        Assert.Equal(string.Empty, KpiCoverage.ForReplyTiming(accounts));
    }

    [Fact]
    public void ReplyTimingIsWhatsAppOnlyOnceInstagramIsConnected()
    {
        var accounts = new[] { Account("whatsapp"), Account("instagram") };

        // Instagram contributes to conversation counts and supplies no reply timing at all, so the two
        // figures have genuinely different scope on the same dashboard.
        Assert.Equal(string.Empty, KpiCoverage.ForConversations(accounts));
        Assert.Equal("WhatsApp only", KpiCoverage.ForReplyTiming(accounts));
    }

    [Fact]
    public void ConversationFiguresNameBothMeasuredChannelsWhenSomethingElseIsConnected()
    {
        var accounts = new[] { Account("whatsapp"), Account("instagram"), Account("googlebusiness") };

        Assert.Equal("Instagram and WhatsApp only", KpiCoverage.ForConversations(accounts));
    }

    [Fact]
    public void SignedOutAccountsAreNamedOnlyWhereTheyCouldHaveContributed()
    {
        var accounts = new[] { Account("whatsapp"), Account("whatsapp", signedOut: true) };

        var timing = KpiCoverage.ForReplyTiming(accounts);

        Assert.Contains("1 signed out", timing, StringComparison.Ordinal);
    }

    [Fact]
    public void ASignedOutAccountOnAnIrrelevantChannelIsNotNamed()
    {
        var accounts = new[] { Account("whatsapp"), Account("googlebusiness", signedOut: true) };

        // Naming it would send the owner to fix something that would not change the number.
        Assert.DoesNotContain("signed out", KpiCoverage.ForReplyTiming(accounts), StringComparison.Ordinal);
    }

    [Fact]
    public void NothingContributingSaysSoRatherThanNamingAnEmptyChannelList()
    {
        var accounts = new[] { Account("googlebusiness"), Account("messenger") };

        // The one case where the number above is not a measurement of anything.
        Assert.Equal("No connected channel supplies this", KpiCoverage.ForConversations(accounts));
    }

    [Fact]
    public void PersonalAccountsAreIgnoredEntirely()
    {
        var accounts = new[] { Account("whatsapp"), Account("googlebusiness", professional: false) };

        // The dashboard measures professional accounts. A personal Google tab is not a gap in a figure
        // that never claimed to include it.
        Assert.Equal(string.Empty, KpiCoverage.ForConversations(accounts));
    }

    [Fact]
    public void AnEmptySetProducesNoLine()
    {
        Assert.Equal(string.Empty, KpiCoverage.ForConversations([]));
        Assert.Equal(string.Empty, KpiCoverage.ForConversations(null));
    }
}
