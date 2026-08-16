using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// WCAG 1.4.1 — colour must never be the only visual means of conveying information.
///
/// <para>
/// This is the other half of <see cref="StatusContrastTests"/>, and the measurement there is what makes
/// it necessary rather than merely good practice: success and danger differ by <b>1.04:1</b> in light and
/// <b>1.21:1</b> in dark. In greyscale they are the same colour, and red/green is the commonest
/// colour-vision deficiency. So for a meaningful share of owners, a red pill and a green pill are the
/// same pill — and that cannot be fixed by tuning, because both colours must also clear 4.5:1 against the
/// same background, which forces them into the same narrow luminance band.
/// </para>
/// <para>
/// Every status that changes colour must therefore also change <i>words</i>. These tests pin that where
/// the decision is reachable without a UI thread.
/// </para>
/// </summary>
public class StatusCueTests
{
    private static readonly InstanceConnectionStatus[] AllStatuses =
    [
        InstanceConnectionStatus.Connected,
        InstanceConnectionStatus.Initializing,
        InstanceConnectionStatus.LoggedOut,
        InstanceConnectionStatus.Error
    ];

    [Fact]
    public void AnyTwoConnectionStatesGivenDifferentDotColoursAlsoReadDifferently()
    {
        // The sidebar's status dot is an 8px Ellipse with nothing but a Fill. If two states are painted
        // differently, the text a user reads — the tooltip and the accessible name both come from
        // ResolveStatusSubtitle — has to distinguish them too, or the dot is the only signal.
        foreach (var a in AllStatuses)
        {
            foreach (var b in AllStatuses)
            {
                if (a == b)
                {
                    continue;
                }

                var colourA = WorkspaceSidebarHelper.ResolveConnectionIndicatorColor(a, AdapterHealthState.Healthy);
                var colourB = WorkspaceSidebarHelper.ResolveConnectionIndicatorColor(b, AdapterHealthState.Healthy);
                if (colourA == colourB)
                {
                    continue; // same colour, so colour is not carrying a distinction here
                }

                var textA = WorkspaceSidebarHelper.ResolveStatusSubtitle(a, AdapterHealthState.Healthy, false);
                var textB = WorkspaceSidebarHelper.ResolveStatusSubtitle(b, AdapterHealthState.Healthy, false);

                Assert.True(
                    textA != textB,
                    $"{a} and {b} are drawn in different colours but both read '{textA}' — the dot is the " +
                    "only thing telling them apart.");
            }
        }
    }

    [Fact]
    public void EveryProblemStateSaysSoInWordsOnTheRowItself()
    {
        // Not just in the tooltip: the visible row subtitle must name the problem, because a tooltip
        // requires knowing there is something to hover.
        foreach (var status in new[] { InstanceConnectionStatus.LoggedOut, InstanceConnectionStatus.Error })
        {
            var subtitle = WorkspaceSidebarHelper.ComposeRowSubtitle("whatsapp", status, notificationsMuted: false);

            Assert.False(string.IsNullOrWhiteSpace(subtitle));
            Assert.NotEqual("WhatsApp", subtitle);
        }
    }

    [Fact]
    public void AnOfflineAccountNamesTheCauseRatherThanJustColouringTheDotRed()
    {
        var subtitle = WorkspaceSidebarHelper.ComposeRowSubtitle(
            "whatsapp", InstanceConnectionStatus.Error, notificationsMuted: false, connectionDetail: "HostNameNotResolved");

        Assert.Contains("internet", subtitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MutedIsStatedInWordsToo()
    {
        // Muted is otherwise signalled by a dimmed row, which is a colour/opacity cue on its own.
        var subtitle = WorkspaceSidebarHelper.ComposeRowSubtitle("whatsapp", InstanceConnectionStatus.Connected, notificationsMuted: true);

        Assert.Contains("muted", subtitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EachCaughtUpStateHasItsOwnHeadlineNotJustItsOwnAccentColour()
    {
        // The hero paints a coloured rail — green, caution, or red — beside the headline. The three states
        // must be distinguishable with the rail ignored entirely.
        var clean = CaughtUpClaim.Resolve([Health("a")], 0);
        var incomplete = CaughtUpClaim.Resolve([Health("a"), Health("b", readFailed: true)], 0);
        var backlog = CaughtUpClaim.Resolve([Health("a", historical: 5)], 0);

        var headlines = new[] { CaughtUpClaim.Headline(clean), CaughtUpClaim.Headline(incomplete), CaughtUpClaim.Headline(backlog) };

        Assert.Equal(3, headlines.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(headlines, h => Assert.False(string.IsNullOrWhiteSpace(h)));
    }

    [Fact]
    public void TheIncompleteStateExplainsItselfInWordsAndNotOnlyByTurningTheRailAmber()
    {
        var verdict = CaughtUpClaim.Resolve([Health("a"), Health("b", readFailed: true)], 0);

        Assert.NotEmpty(CaughtUpClaim.IncompleteClause(verdict));
    }

    private static OversightEntityHealth Health(string key, bool readFailed = false, int historical = 0) =>
        new()
        {
            Key = key,
            DisplayName = key,
            Kind = OversightEntityKind.Instance,
            AccountCount = 1,
            MeasuredCount = 10,
            HasChatData = true,
            ReadFailed = readFailed,
            HistoricalOpenCount = historical,
            OnTimePercent = 100,
            MemberInstanceIds = [key]
        };
}
