using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-FIRSTRUN-01 — the dashboard's opening screen on a clean install.
///
/// Measured on a genuinely clean machine (data directory moved aside, fresh install), the first screen a
/// stranger saw said all of these at once:
///   "Welcome back"                    — to someone who had never opened the app
///   "1 personal account connected."   — with no accounts of their own
///   "No accounts connected yet"       — directly contradicting the line above
///
/// Both wrong lines trace to the registry seeding a placeholder <c>whatsapp-default</c> account on first
/// run. It is a real registry entry, so it counted — but the owner connected nothing and signed into
/// nothing.
/// </summary>
public class FirstRunGreetingTests
{
    private static MessengerInstance Instance(string id, bool professional = false) =>
        new()
        {
            Id = id,
            DisplayName = id,
            ProfileName = id,
            Platform = "whatsapp",
            Category = professional ? WorkspaceCategory.Professional : WorkspaceCategory.Personal
        };

    [Fact]
    public void TheSeededPlaceholderAloneCountsAsNothingConnected()
    {
        var instances = new[] { Instance(DashboardPageHelper.SeededDefaultInstanceId) };

        Assert.True(DashboardPageHelper.HasOnlySeededDefaultAccount(instances));
    }

    [Fact]
    public void AnAccountTheOwnerAddedIsNotTreatedAsSeeded()
    {
        var instances = new[] { Instance("whatsapp-depilex-f-11-whatsapp-e20960") };

        Assert.False(DashboardPageHelper.HasOnlySeededDefaultAccount(instances));
    }

    [Fact]
    public void TheSeededPlaceholderAlongsideARealAccountIsNoLongerAFirstRun()
    {
        // Once the owner has added anything, the screen must go back to the normal greeting and counts.
        var instances = new[]
        {
            Instance(DashboardPageHelper.SeededDefaultInstanceId),
            Instance("whatsapp-real", professional: true)
        };

        Assert.False(DashboardPageHelper.HasOnlySeededDefaultAccount(instances));
    }

    [Fact]
    public void NoAccountsAtAllIsNotReportedAsTheSeededCase()
    {
        // Distinct state — handled by BuildWelcomeSubtitle's (0,0) branch, which already reads correctly.
        Assert.False(DashboardPageHelper.HasOnlySeededDefaultAccount([]));
        Assert.False(DashboardPageHelper.HasOnlySeededDefaultAccount(null));
    }

    [Fact]
    public void TheSeededIdIsMatchedCaseInsensitively()
    {
        var instances = new[] { Instance("WhatsApp-Default") };

        Assert.True(DashboardPageHelper.HasOnlySeededDefaultAccount(instances));
    }

    [Fact]
    public void TheNoAccountsSubtitleDoesNotClaimAnythingIsConnected()
    {
        var subtitle = DashboardPageHelper.BuildWelcomeSubtitle(professionalCount: 0, personalCount: 0);

        Assert.DoesNotContain("connected.", subtitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheGreetingStopsSayingConnectedWhenAnAccountIsSignedOut()
    {
        // Found by installing the build and looking at it. The line read "8 professional accounts
        // connected." on a machine where one of the eight was sitting on a WhatsApp QR screen - the
        // first sentence on the first screen, asserting the one thing the app had just established was
        // false. The count was never wrong; the verb was.
        var subtitle = DashboardPageHelper.BuildWelcomeSubtitle(
            professionalCount: 8, personalCount: 0, signedOutCount: 1);

        Assert.DoesNotContain("8 professional accounts connected", subtitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("7 accounts reading", subtitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 signed out", subtitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheGreetingIsUnchangedWhenEverythingIsSignedIn()
    {
        // Guard against over-correcting: the ordinary case must stay the ordinary sentence.
        Assert.Equal(
            DashboardPageHelper.BuildWelcomeSubtitle(8, 3),
            DashboardPageHelper.BuildWelcomeSubtitle(8, 3, signedOutCount: 0));
    }

    [Fact]
    public void TheGreetingSaysNothingIsBeingReadWhenEveryAccountIsSignedOut()
    {
        var subtitle = DashboardPageHelper.BuildWelcomeSubtitle(
            professionalCount: 2, personalCount: 0, signedOutCount: 2);

        Assert.DoesNotContain("connected", subtitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nothing is being read", subtitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RealAccountsAreStillCountedNormally()
    {
        // Guard against over-correcting into "never report counts".
        var subtitle = DashboardPageHelper.BuildWelcomeSubtitle(professionalCount: 8, personalCount: 3);

        Assert.Equal("8 professional and 3 personal accounts connected.", subtitle);
    }
}
