using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Holds every declared navigation to the three rules that make one trustworthy: arrival is proven
/// independently, a customer-visible side effect cannot happen without a person asking, and the retry
/// budget is a stated bound.
/// </summary>
public class NavigationOperationTests
{
    [Fact]
    public void EveryOperationHasAnIdAPlatformAndADescription()
    {
        Assert.NotEmpty(NavigationOperations.All);

        foreach (var op in NavigationOperations.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(op.Id));
            Assert.False(string.IsNullOrWhiteSpace(op.PlatformId));
            Assert.False(string.IsNullOrWhiteSpace(op.Description));
            Assert.NotNull(PlatformDefinition.FindById(op.PlatformId));
        }
    }

    [Fact]
    public void OperationIdsAreUnique() =>
        Assert.Equal(
            NavigationOperations.All.Count,
            NavigationOperations.All.Select(o => o.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    [Fact]
    public void EveryRunnerDrivenOperationProvesArrivalIndependently()
    {
        // The rule the whole file exists for. An operation the runner performs must name an anchor whose
        // presence means the view is really on screen — because clicking is not opening, and every click
        // path reports success whether or not anything happened.
        foreach (var op in NavigationOperations.All.Where(o => !o.ImplementedInPage))
        {
            Assert.True(
                op.ReadbackAnchors.Count > 0,
                $"'{op.Id}' has no readback: it would be trusting its own return value.");
        }
    }

    [Fact]
    public void AnOperationWithoutAReadbackSaysHowItProvesArrival()
    {
        // The only permitted exception is an operation whose arrival is proven some other way, and it has
        // to say so — otherwise "no readback" becomes the easy default.
        foreach (var op in NavigationOperations.All.Where(o => o.ReadbackAnchors.Count == 0))
        {
            Assert.True(op.ImplementedInPage, $"'{op.Id}' has no readback and is not marked as in-page.");
            Assert.False(string.IsNullOrWhiteSpace(op.Notes), $"'{op.Id}' must explain how arrival is proven.");
        }
    }

    [Fact]
    public void EveryReadbackAnchorExistsInThePlatformManifest()
    {
        // A readback naming an anchor the manifest does not have would fall through to __umPick's built-in
        // and quietly stop being manifest-driven — or, worse, resolve nothing and fail every navigation.
        foreach (var op in NavigationOperations.All.Where(o => o.ReadbackAnchors.Count > 0))
        {
            var manifest = SelectorManifestLoader.ForPlatform(op.PlatformId);
            Assert.NotNull(manifest);

            foreach (var anchor in op.ReadbackAnchors)
            {
                Assert.True(
                    manifest!.Anchors.ContainsKey(anchor),
                    $"'{op.Id}' reads back '{anchor}', which is not in the {op.PlatformId} manifest.");
            }
        }
    }

    [Fact]
    public void RetryBudgetsAreStatedAndBounded()
    {
        foreach (var op in NavigationOperations.All.Where(o => !o.ImplementedInPage))
        {
            Assert.InRange(op.MaxAttempts, 1, 40);
            Assert.InRange(op.RetryDelayMs, 100, 5000);

            // A cold WebView needs several seconds to restore its session and render the chat list; the
            // budget that preceded this was 2.5s and expired first, so focus reported "nothing" on an
            // account that was merely still waking up.
            Assert.True(
                op.Budget >= TimeSpan.FromSeconds(2),
                $"'{op.Id}' gives up in {op.Budget.TotalSeconds:0.#}s, which a cold WebView will outlast.");
            Assert.True(op.Budget <= TimeSpan.FromSeconds(30), $"'{op.Id}' would hang the caller.");
        }
    }

    // ---- the side-effect gate ----------------------------------------------------------------------

    [Fact]
    public void OpeningAConversationRequiresAPersonToHaveAskedForIt()
    {
        // Opening a conversation marks it read. On Meta that fires a receipt the customer sees and which
        // cannot be withdrawn; on WhatsApp it is still a side effect. The flag exists so the constraint is
        // in place BEFORE an adapter can violate it — the same reason RequiresThreadOpenToRead was
        // declared before Meta had a scraper.
        var focus = NavigationOperations.Require(NavigationOperations.FocusConversation);

        Assert.True(focus.RequiresUserIntent);
        Assert.False(NavigationOperations.MayRun(focus, userInitiated: false));
        Assert.True(NavigationOperations.MayRun(focus, userInitiated: true));
    }

    [Fact]
    public void AReadOnlyOperationDoesNotNeedUserIntent()
    {
        // Opening the archived LIST is not opening a conversation: nothing is marked read. Requiring intent
        // here would be cargo-culting the flag rather than applying it.
        var archived = NavigationOperations.Require(NavigationOperations.ShowArchived);

        Assert.False(archived.RequiresUserIntent);
        Assert.True(NavigationOperations.MayRun(archived, userInitiated: false));
    }

    [Fact]
    public void RefusalIsNotSilentlyASuccess()
    {
        var refused = NavigationOutcome.Refused(NavigationOperations.FocusConversation);

        Assert.False(refused.Arrived);
        Assert.False(refused.IdentityVerified);
    }

    // ---- the readback script -----------------------------------------------------------------------

    [Fact]
    public void TheFocusReadbackRequiresTheComposer()
    {
        // The composer exists ONLY while a chat is open, which is what separates "my selector is stale"
        // from "the click did nothing". A readback on the header alone cannot tell those apart.
        var script = NavigationOperations.BuildReadbackScript(
            NavigationOperations.Require(NavigationOperations.FocusConversation));

        Assert.Contains("composer", script, StringComparison.Ordinal);
        Assert.Contains("contenteditable", script, StringComparison.Ordinal);
        Assert.Contains("&&", script, StringComparison.Ordinal);   // every anchor must hold, not any
    }

    [Fact]
    public void TheReadbackFallsBackWhenNoManifestIsLoaded()
    {
        // This is the "the manifest failed" path, so it cannot itself depend on the manifest.
        var script = NavigationOperations.BuildReadbackScript(
            NavigationOperations.Require(NavigationOperations.ShowArchived));

        Assert.Contains("window.__umPick?", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelector(", script, StringComparison.Ordinal);
        Assert.Contains("archived-chatlist", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReadbackCannotThrowIntoTheCaller()
    {
        foreach (var op in NavigationOperations.All.Where(o => o.ReadbackAnchors.Count > 0))
        {
            Assert.Contains("catch(e){return false;}", NavigationOperations.BuildReadbackScript(op), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AnInPageOperationBuildsNoReadbackScript() =>
        Assert.Equal(
            string.Empty,
            NavigationOperations.BuildReadbackScript(NavigationOperations.Require(NavigationOperations.OpenReviewsManager)));

    // ---- the one Google rule that has already cost a release ---------------------------------------

    [Fact]
    public void TheGoogleReturnGuardAcceptsAnyGoogleHost()
    {
        // The rating scrape parks this same WebView on www.google.com/search — that is the only place the
        // rating and lifetime total exist. A business.google.com-only guard strands it there, the reviews
        // scrape that runs next reports 'notreviews' and gives up, and the symptom is that MANUAL Re-sync
        // is the path that fails to refresh review counts while the background pass looks fine.
        var script = (string?)typeof(GoogleReviewSnapshotService)
            .GetField("KickoffScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.GetRawConstantValue();

        Assert.False(string.IsNullOrWhiteSpace(script));
        Assert.Contains(@"google\.com$", script!, StringComparison.Ordinal);
        Assert.Contains("business.google.com/reviews", script!, StringComparison.Ordinal);
    }
}
